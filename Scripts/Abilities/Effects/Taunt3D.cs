using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Taunt ability - Force a targeted enemy to attack you.
/// Tank/aggro control ability.
/// </summary>
public partial class Taunt3D : Ability3D, ITargetedAbility
{
    public override string AbilityId => "taunt";
    public override string AbilityName => "Taunt";
    public override string Description => "Force an enemy to attack you for 5 seconds. The enemy deals reduced damage.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 15f;
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 12;

    // Taunt stats
    public float Duration { get; private set; } = 5f;
    public float DamageReduction { get; private set; } = 0.25f; // Taunted enemy deals 25% less damage
    public float Range { get; private set; } = 20f;

    // ITargetedAbility
    public float TargetRadius => 2f;
    public Color TargetColor => new(1f, 0.3f, 0.1f); // Orange-red

    private Vector3 _targetPosition;
    private Node3D? _tauntedEnemy;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        if (Player == null) return;

        // Find enemy near target position
        _tauntedEnemy = FindEnemyNearPoint(_targetPosition, 3f);

        if (_tauntedEnemy == null)
        {
            GD.Print("[Taunt3D] No valid target");
            return;
        }

        // Apply taunt
        ApplyTaunt(_tauntedEnemy);

        // Create visual effect
        CreateTauntEffect(_tauntedEnemy);

        // Create link between player and enemy
        CreateTauntLink(_tauntedEnemy);

        SetActiveWithDuration(Duration);

        GD.Print($"[Taunt3D] Taunted {_tauntedEnemy.Name} for {Duration}s");
    }

    private Node3D? FindEnemyNearPoint(Vector3 point, float maxRange)
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

    private void ApplyTaunt(Node3D enemy)
    {
        // Force enemy to target player
        if (enemy.HasMethod("SetTarget"))
        {
            enemy.Call("SetTarget", Player);
        }

        // Reduce enemy damage
        if (enemy.HasMethod("SetDamageMultiplier"))
        {
            enemy.Call("SetDamageMultiplier", 1f - DamageReduction);
        }

        // Mark as taunted (for visual indicator)
        if (enemy.HasMethod("SetTaunted"))
        {
            enemy.Call("SetTaunted", true);
        }
    }

    private void RemoveTaunt(Node3D enemy)
    {
        if (!IsInstanceValid(enemy)) return;

        // Restore enemy damage
        if (enemy.HasMethod("SetDamageMultiplier"))
        {
            enemy.Call("SetDamageMultiplier", 1f);
        }

        // Remove taunt marker
        if (enemy.HasMethod("SetTaunted"))
        {
            enemy.Call("SetTaunted", false);
        }
    }

    private void CreateTauntEffect(Node3D enemy)
    {
        // Exclamation mark above enemy
        var marker = new MeshInstance3D();
        var capsule = new CapsuleMesh();
        capsule.Radius = 0.15f;
        capsule.Height = 0.6f;
        marker.Mesh = capsule;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.2f, 0.1f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.3f, 0.1f);
        mat.EmissionEnergyMultiplier = 2f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        marker.MaterialOverride = mat;

        marker.Name = "TauntMarker";
        marker.Position = Vector3.Up * 3f;
        enemy.AddChild(marker);

        // Angry aura around enemy
        var aura = new GpuParticles3D();
        aura.Name = "TauntAura";
        aura.Amount = 15;
        aura.Lifetime = 1f;
        aura.Emitting = true;

        var auraMat = new ParticleProcessMaterial();
        auraMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        auraMat.EmissionSphereRadius = 1f;
        auraMat.Direction = new Vector3(0, 1, 0);
        auraMat.Spread = 45f;
        auraMat.InitialVelocityMin = 0.5f;
        auraMat.InitialVelocityMax = 1.5f;
        auraMat.Gravity = new Vector3(0, 0.5f, 0);
        auraMat.ScaleMin = 0.05f;
        auraMat.ScaleMax = 0.1f;
        auraMat.Color = new Color(1f, 0.3f, 0.1f, 0.6f);
        aura.ProcessMaterial = auraMat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        aura.DrawPass1 = mesh;

        aura.Position = Vector3.Up;
        enemy.AddChild(aura);
    }

    private void CreateTauntLink(Node3D enemy)
    {
        // Create a visual chain/link between player and enemy
        var link = new TauntLinkEffect();
        link.Name = "TauntLink";
        link.Initialize(Player!, enemy, Duration);
        GetTree().Root.AddChild(link);
    }

    protected override void OnDeactivate()
    {
        if (_tauntedEnemy != null && IsInstanceValid(_tauntedEnemy))
        {
            RemoveTaunt(_tauntedEnemy);

            // Remove visual markers
            var marker = _tauntedEnemy.GetNodeOrNull("TauntMarker");
            marker?.QueueFree();

            var aura = _tauntedEnemy.GetNodeOrNull("TauntAura");
            aura?.QueueFree();
        }

        _tauntedEnemy = null;

        GD.Print("[Taunt3D] Taunt expired");
    }
}

/// <summary>
/// Visual link between player and taunted enemy.
/// </summary>
public partial class TauntLinkEffect : Node3D
{
    private Node3D? _player;
    private Node3D? _enemy;
    private float _duration;
    private float _timer;
    private MeshInstance3D? _linkMesh;
    private StandardMaterial3D? _material;

    public void Initialize(Node3D player, Node3D enemy, float duration)
    {
        _player = player;
        _enemy = enemy;
        _duration = duration;
    }

    public override void _Ready()
    {
        CreateLinkVisual();
    }

    private void CreateLinkVisual()
    {
        _linkMesh = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 0.05f;
        cylinder.BottomRadius = 0.05f;
        cylinder.Height = 1f;
        _linkMesh.Mesh = cylinder;

        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(1f, 0.3f, 0.1f, 0.5f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(1f, 0.2f, 0f);
        _material.EmissionEnergyMultiplier = 1.5f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _linkMesh.MaterialOverride = _material;

        AddChild(_linkMesh);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _timer += dt;

        if (_timer >= _duration || _player == null || _enemy == null ||
            !IsInstanceValid(_player) || !IsInstanceValid(_enemy))
        {
            QueueFree();
            return;
        }

        // Update link position and rotation
        Vector3 start = _player.GlobalPosition + Vector3.Up;
        Vector3 end = _enemy.GlobalPosition + Vector3.Up;
        Vector3 midpoint = (start + end) / 2;
        float distance = start.DistanceTo(end);

        if (_linkMesh != null)
        {
            _linkMesh.GlobalPosition = midpoint;

            // Rotate to connect the two points
            if (distance > 0.1f)
            {
                _linkMesh.LookAt(end);
                _linkMesh.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);
            }

            // Update cylinder height
            if (_linkMesh.Mesh is CylinderMesh cyl)
            {
                cyl.Height = distance;
            }
        }

        // Pulse effect
        if (_material != null)
        {
            float pulse = (Mathf.Sin(_timer * 6f) + 1f) * 0.5f;
            _material.AlbedoColor = new Color(1f, 0.3f, 0.1f, 0.3f + pulse * 0.2f);
            _material.EmissionEnergyMultiplier = 1f + pulse * 1f;
        }
    }
}
