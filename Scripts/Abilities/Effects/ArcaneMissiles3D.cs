using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Arcane Missiles spell - Launch multiple homing projectiles.
/// Rapid fire spell that auto-targets enemies.
/// </summary>
public partial class ArcaneMissiles3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "arcane_missiles";
    public override string AbilityName => "Arcane Missiles";
    public override string Description => "Launch 5 homing missiles that seek out enemies.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 0f; // No cooldown, mana-limited
    public override int DefaultManaCost => 20;
    public override int RequiredLevel => 10;

    // Arcane Missiles stats
    public float DamagePerMissile { get; private set; } = 15f;
    public int MissileCount { get; private set; } = 5;
    public float MissileSpeed { get; private set; } = 25f;
    public float HomingStrength { get; private set; } = 10f;

    // ITargetedAbility
    public float TargetRadius => 15f;
    public Color TargetColor => new(0.7f, 0.3f, 1f); // Purple

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        var startPos = GetCameraPosition();

        // Find target enemy near cursor
        Node3D? targetEnemy = FindClosestEnemyToPoint(_targetPosition, 10f);

        // Launch missiles with slight delays
        for (int i = 0; i < MissileCount; i++)
        {
            float delay = i * 0.1f;
            var timer = GetTree().CreateTimer(delay);
            int index = i;
            timer.Timeout += () => LaunchMissile(startPos, targetEnemy, index);
        }

        GD.Print($"[ArcaneMissiles3D] Launching {MissileCount} missiles");
    }

    private void LaunchMissile(Vector3 startPos, Node3D? target, int index)
    {
        var missile = new ArcaneMissileProjectile();
        missile.Name = $"ArcaneMissile_{index}";

        // Offset starting position slightly for visual variety
        float angle = index * Mathf.Pi * 2 / MissileCount;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.3f;
        missile.GlobalPosition = startPos + offset;

        missile.Initialize(target, _targetPosition, DamagePerMissile, MissileSpeed, HomingStrength);
        GetTree().Root.AddChild(missile);
    }

    private Node3D? FindClosestEnemyToPoint(Vector3 point, float maxRange)
    {
        Node3D? closest = null;
        float closestDist = maxRange;

        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(point);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }
        }

        return closest;
    }
}

/// <summary>
/// Individual arcane missile projectile with homing behavior.
/// </summary>
public partial class ArcaneMissileProjectile : Node3D
{
    private Node3D? _target;
    private Vector3 _fallbackPosition;
    private float _damage;
    private float _speed;
    private float _homingStrength;
    private Vector3 _velocity;
    private float _lifetime = 5f;
    private bool _hit;

    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;

    public void Initialize(Node3D? target, Vector3 fallback, float damage, float speed, float homing)
    {
        _target = target;
        _fallbackPosition = fallback;
        _damage = damage;
        _speed = speed;
        _homingStrength = homing;

        // Initial velocity toward target
        Vector3 targetPos = target?.GlobalPosition ?? fallback;
        _velocity = (targetPos - GlobalPosition).Normalized() * _speed;
    }

    public override void _Ready()
    {
        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // Glowing orb
        _mesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.15f;
        sphere.Height = 0.3f;
        _mesh.Mesh = sphere;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.8f, 0.4f, 1f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.6f, 0.2f, 1f);
        mat.EmissionEnergyMultiplier = 3f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _mesh.MaterialOverride = mat;
        AddChild(_mesh);

        // Point light
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.7f, 0.3f, 1f);
        _light.LightEnergy = 1f;
        _light.OmniRange = 3f;
        AddChild(_light);

        // Particle trail
        var trail = new GpuParticles3D();
        trail.Amount = 10;
        trail.Lifetime = 0.3f;

        var pmat = new ParticleProcessMaterial();
        pmat.Direction = new Vector3(0, 0, 0);
        pmat.InitialVelocityMin = 0f;
        pmat.InitialVelocityMax = 0.5f;
        pmat.ScaleMin = 0.05f;
        pmat.ScaleMax = 0.1f;
        pmat.Color = new Color(0.8f, 0.5f, 1f, 0.5f);
        trail.ProcessMaterial = pmat;

        var trailMesh = new SphereMesh();
        trailMesh.Radius = 0.05f;
        trailMesh.Height = 0.1f;
        trail.DrawPass1 = trailMesh;
        AddChild(trail);
    }

    public override void _Process(double delta)
    {
        if (_hit) return;

        float dt = (float)delta;
        _lifetime -= dt;

        if (_lifetime <= 0)
        {
            QueueFree();
            return;
        }

        // Update target position (homing)
        Vector3 targetPos = _fallbackPosition;
        if (_target != null && GodotObject.IsInstanceValid(_target))
        {
            targetPos = _target.GlobalPosition + Vector3.Up * 0.5f;
        }

        // Steer toward target
        Vector3 desiredVelocity = (targetPos - GlobalPosition).Normalized() * _speed;
        _velocity = _velocity.Lerp(desiredVelocity, _homingStrength * dt);
        _velocity = _velocity.Normalized() * _speed;

        // Move
        GlobalPosition += _velocity * dt;

        // Check for hit
        float distToTarget = GlobalPosition.DistanceTo(targetPos);
        if (distToTarget < 1f)
        {
            OnHit();
        }
    }

    private void OnHit()
    {
        _hit = true;

        // Deal damage if we have a target
        if (_target != null && GodotObject.IsInstanceValid(_target))
        {
            if (_target.HasMethod("TakeDamage"))
            {
                _target.Call("TakeDamage", _damage, GlobalPosition, "Arcane Missiles");
            }
        }

        // Impact effect
        CreateImpactEffect();
        QueueFree();
    }

    private void CreateImpactEffect()
    {
        var impact = new GpuParticles3D();
        impact.Name = "ArcaneImpact";
        impact.GlobalPosition = GlobalPosition;
        impact.Amount = 15;
        impact.Lifetime = 0.3f;
        impact.Explosiveness = 1f;
        impact.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.2f;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 180f;
        mat.InitialVelocityMin = 2f;
        mat.InitialVelocityMax = 5f;
        mat.ScaleMin = 0.05f;
        mat.ScaleMax = 0.15f;
        mat.Color = new Color(0.8f, 0.4f, 1f);
        impact.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        impact.DrawPass1 = mesh;

        GetTree().Root.AddChild(impact);

        // Auto-cleanup
        var timer = GetTree().CreateTimer(0.5f);
        timer.Timeout += () => impact.QueueFree();
    }
}
