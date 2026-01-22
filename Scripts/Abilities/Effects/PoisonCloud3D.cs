using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Poison Cloud spell - Create a toxic zone that damages enemies over time.
/// Targeted DOT zone spell.
/// </summary>
public partial class PoisonCloud3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "poison_cloud";
    public override string AbilityName => "Poison Cloud";
    public override string Description => "Create a toxic cloud that poisons enemies standing in it.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 20f;
    public override int DefaultManaCost => 25;
    public override int RequiredLevel => 11;

    // Poison Cloud stats
    public float DamagePerSecond { get; private set; } = 10f;
    public float Duration { get; private set; } = 10f;
    public float Radius { get; private set; } = 6f;

    // ITargetedAbility
    public float TargetRadius => Radius;
    public Color TargetColor => new(0.4f, 0.8f, 0.2f); // Green

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        var cloud = new PoisonCloudEffect();
        cloud.Name = "PoisonCloud";
        cloud.GlobalPosition = _targetPosition;
        cloud.Initialize(Radius, Duration, DamagePerSecond);
        GetTree().Root.AddChild(cloud);

        GD.Print($"[PoisonCloud3D] Created poison cloud at {_targetPosition}");
    }
}

/// <summary>
/// The poison cloud effect that damages enemies over time.
/// </summary>
public partial class PoisonCloudEffect : Node3D
{
    private float _radius;
    private float _duration;
    private float _dps;
    private float _tickTimer;
    private float _lifeTimer;
    private const float TickInterval = 0.5f;

    private GpuParticles3D? _particles;
    private OmniLight3D? _light;

    public void Initialize(float radius, float duration, float dps)
    {
        _radius = radius;
        _duration = duration;
        _dps = dps;
        _lifeTimer = duration;
    }

    public override void _Ready()
    {
        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // Poison particles
        _particles = new GpuParticles3D();
        _particles.Amount = 80;
        _particles.Lifetime = 2f;
        _particles.Preprocess = 1f;

        var pmat = new ParticleProcessMaterial();
        pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        pmat.EmissionSphereRadius = _radius * 0.8f;
        pmat.Direction = new Vector3(0, 1, 0);
        pmat.Spread = 30f;
        pmat.InitialVelocityMin = 0.5f;
        pmat.InitialVelocityMax = 1.5f;
        pmat.Gravity = new Vector3(0, 0.5f, 0);
        pmat.ScaleMin = 0.3f;
        pmat.ScaleMax = 0.8f;
        pmat.Color = new Color(0.3f, 0.7f, 0.2f, 0.5f);
        _particles.ProcessMaterial = pmat;

        var particleMesh = new SphereMesh();
        particleMesh.Radius = 0.2f;
        particleMesh.Height = 0.4f;
        _particles.DrawPass1 = particleMesh;
        AddChild(_particles);

        // Eerie green light
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.3f, 0.8f, 0.2f);
        _light.LightEnergy = 1f;
        _light.OmniRange = _radius * 1.5f;
        AddChild(_light);

        // Ground fog mesh
        var fog = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = _radius;
        cylinder.BottomRadius = _radius;
        cylinder.Height = 0.3f;
        fog.Mesh = cylinder;
        fog.Position = Vector3.Up * 0.15f;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.2f, 0.5f, 0.1f, 0.3f);
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        fog.MaterialOverride = mat;
        AddChild(fog);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        _lifeTimer -= dt;
        _tickTimer += dt;

        // Damage tick
        if (_tickTimer >= TickInterval)
        {
            _tickTimer = 0f;
            DamageTick();
        }

        // Fade out near end
        if (_lifeTimer <= 2f)
        {
            float alpha = _lifeTimer / 2f;
            if (_light != null)
                _light.LightEnergy = alpha;
        }

        // Cleanup
        if (_lifeTimer <= 0f)
        {
            QueueFree();
        }
    }

    private void DamageTick()
    {
        float tickDamage = _dps * TickInterval;

        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(GlobalPosition);
                if (dist <= _radius)
                {
                    // Deal poison damage
                    if (enemy.HasMethod("TakeDamage"))
                    {
                        enemy.Call("TakeDamage", tickDamage, GlobalPosition, "Poison Cloud");
                    }

                    // Add poison tint effect
                    ApplyPoisonVisual(enemy);
                }
            }
        }
    }

    private void ApplyPoisonVisual(Node3D enemy)
    {
        // Temporary green tint
        var tint = enemy.FindChild("PoisonTint", false) as OmniLight3D;
        if (tint == null)
        {
            tint = new OmniLight3D();
            tint.Name = "PoisonTint";
            tint.LightColor = new Color(0.3f, 0.8f, 0.2f);
            tint.LightEnergy = 0.3f;
            tint.OmniRange = 1.5f;
            enemy.AddChild(tint);

            // Auto-remove after leaving cloud
            var timer = GetTree().CreateTimer(1f);
            timer.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(tint))
                    tint.QueueFree();
            };
        }
    }
}
