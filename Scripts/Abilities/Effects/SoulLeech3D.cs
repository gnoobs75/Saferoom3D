using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Soul Leech spell - AOE life drain that heals player.
/// Targeted spell that damages enemies and heals for a percentage.
/// </summary>
public partial class SoulLeech3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "soul_leech";
    public override string AbilityName => "Soul Leech";
    public override string Description => "Drain life from enemies in an area, healing yourself.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 12f;
    public override int DefaultManaCost => 40;
    public override int RequiredLevel => 3;

    // Soul Leech stats
    public float Damage { get; private set; } = 40f;
    public float Radius { get; private set; } = 11.25f; // 180px / 16
    public float HealPercent { get; private set; } = 0.5f; // 50% of damage dealt

    // ITargetedAbility
    public float TargetRadius => Radius;
    public Color TargetColor => new(0.3f, 0.8f, 0.3f); // Green

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Create the soul leech effect
        var effect = new SoulLeechEffect3D();
        effect.Name = "SoulLeechEffect";
        effect.GlobalPosition = _targetPosition;
        effect.Initialize(Damage, Radius, HealPercent, this);

        GetTree().Root.AddChild(effect);

        GD.Print($"[SoulLeech3D] Cast at {_targetPosition}");
    }
}

/// <summary>
/// Visual and damage effect for soul leech.
/// </summary>
public partial class SoulLeechEffect3D : Node3D
{
    private float _damage;
    private float _radius;
    private float _healPercent;
    private SoulLeech3D? _ability;

    private MeshInstance3D? _vortexMesh;
    private OmniLight3D? _light;
    private GpuParticles3D? _particles;

    private float _lifetime = 1.5f;
    private bool _damageApplied;

    public void Initialize(float damage, float radius, float healPercent, SoulLeech3D ability)
    {
        _damage = damage;
        _radius = radius;
        _healPercent = healPercent;
        _ability = ability;
    }

    public override void _Ready()
    {
        CreateVisuals();
        ApplyEffect();
    }

    private void CreateVisuals()
    {
        // Create vortex mesh (cylinder)
        _vortexMesh = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = _radius;
        cylinder.BottomRadius = _radius * 0.5f;
        cylinder.Height = 2f;
        _vortexMesh.Mesh = cylinder;

        // Dark green vortex material
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.2f, 0.5f, 0.2f, 0.5f);
        material.EmissionEnabled = true;
        material.Emission = new Color(0.1f, 0.6f, 0.1f);
        material.EmissionEnergyMultiplier = 2f;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _vortexMesh.MaterialOverride = material;

        _vortexMesh.Position = new Vector3(0, 1f, 0);
        AddChild(_vortexMesh);

        // Create light
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.3f, 0.8f, 0.3f);
        _light.LightEnergy = 2f;
        _light.OmniRange = _radius + 2f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);

        // Create swirling particles
        _particles = CreateSwirlParticles();
        AddChild(_particles);
    }

    private GpuParticles3D CreateSwirlParticles()
    {
        var particles = new GpuParticles3D();
        particles.Amount = 50;
        particles.Lifetime = 1f;
        particles.Explosiveness = 0.3f;
        particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        material.EmissionRingRadius = _radius;
        material.EmissionRingHeight = 0.1f;
        material.EmissionRingInnerRadius = _radius * 0.3f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 10f;
        material.InitialVelocityMin = 2f;
        material.InitialVelocityMax = 4f;
        material.Gravity = new Vector3(0, 0, 0);
        material.OrbitVelocityMin = 0.5f;
        material.OrbitVelocityMax = 1f;
        material.ScaleMin = 0.1f;
        material.ScaleMax = 0.2f;
        material.Color = new Color(0.4f, 1f, 0.4f);

        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.3f, 0.3f);
        particles.DrawPass1 = quadMesh;

        return particles;
    }

    private void ApplyEffect()
    {
        if (_damageApplied) return;
        _damageApplied = true;

        // Find and damage all enemies in radius
        var enemies = GetTree().GetNodesInGroup("Enemies");
        int totalHealing = 0;
        int enemiesHit = 0;

        foreach (var node in enemies)
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(GlobalPosition);
                if (dist <= _radius)
                {
                    if (enemy.HasMethod("TakeDamage"))
                    {
                        enemy.Call("TakeDamage", _damage, GlobalPosition, "Soul Leech");
                        enemiesHit++;
                        totalHealing += (int)(_damage * _healPercent);

                        // Create drain line visual from enemy to center
                        CreateDrainLine(enemy.GlobalPosition);
                    }
                }
            }
        }

        // Heal player
        if (totalHealing > 0 && Player.FPSController.Instance != null)
        {
            Player.FPSController.Instance.Heal(totalHealing);
            GD.Print($"[SoulLeech] Hit {enemiesHit} enemies, healed for {totalHealing} HP");
        }

        // Play sound
        Core.SoundManager3D.Instance?.PlayHealSound(GlobalPosition);
    }

    private void CreateDrainLine(Vector3 enemyPos)
    {
        // Create a line from enemy to vortex center
        var line = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 0.05f;
        cylinder.BottomRadius = 0.05f;

        Vector3 center = GlobalPosition + new Vector3(0, 1f, 0);
        float dist = enemyPos.DistanceTo(center);
        cylinder.Height = dist;

        line.Mesh = cylinder;

        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.4f, 1f, 0.4f, 0.8f);
        material.EmissionEnabled = true;
        material.Emission = new Color(0.3f, 0.8f, 0.3f);
        material.EmissionEnergyMultiplier = 2f;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        line.MaterialOverride = material;

        // Position and orient
        line.GlobalPosition = (enemyPos + center) / 2f;
        line.LookAt(center, Vector3.Up);
        line.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);

        AddChild(line);

        // Fade out
        var tween = line.CreateTween();
        tween.TweenProperty(material, "albedo_color:a", 0f, 0.5f);
        tween.TweenCallback(Callable.From(() => line.QueueFree()));
    }

    public override void _Process(double delta)
    {
        _lifetime -= (float)delta;

        // Rotate vortex
        if (_vortexMesh != null)
        {
            _vortexMesh.RotateY((float)delta * 3f);
        }

        if (_lifetime <= 0)
        {
            // Fade out
            if (_particles != null) _particles.Emitting = false;

            var tween = CreateTween();
            if (_vortexMesh?.MaterialOverride is StandardMaterial3D mat)
            {
                tween.TweenProperty(mat, "albedo_color:a", 0f, 0.3f);
            }
            if (_light != null)
            {
                tween.Parallel().TweenProperty(_light, "light_energy", 0f, 0.3f);
            }
            tween.TweenCallback(Callable.From(() => QueueFree()));

            _lifetime = -999; // Prevent re-triggering
        }
    }
}
