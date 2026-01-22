using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Engine of Tomorrow ability - Slow all enemies to 50% speed.
/// Global debuff that affects all enemies for a duration.
/// </summary>
public partial class EngineOfTomorrow3D : Ability3D
{
    // Singleton for checking slow state
    public static EngineOfTomorrow3D? Instance { get; private set; }

    public override string AbilityId => "engine_of_tomorrow";
    public override string AbilityName => "Engine of Tomorrow";
    public override string Description => "Slow all enemies to half speed.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 120f; // 2 minutes
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 6;

    // Engine of Tomorrow stats
    public float Duration { get; private set; } = 15f;
    public float SlowMultiplier { get; private set; } = 0.5f;

    private EngineOfTomorrowEffect3D? _effect;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
    }

    protected override void Activate()
    {
        // Create visual effect on player
        _effect = new EngineOfTomorrowEffect3D();
        _effect.Name = "EngineOfTomorrowEffect";

        if (Player != null)
        {
            Player.AddChild(_effect);
            _effect.Position = Vector3.Zero;
        }

        // Apply visual effect to all enemies
        ApplySlowVisuals();

        SetActiveWithDuration(Duration);

        GD.Print($"[EngineOfTomorrow3D] Activated - All enemies slowed to {SlowMultiplier * 100}%");
    }

    private void ApplySlowVisuals()
    {
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D enemy)
            {
                ApplySlowVisualToEnemy(enemy);
            }
        }
    }

    private void ApplySlowVisualToEnemy(Node3D enemy)
    {
        // Add purple tint to enemy
        foreach (var child in enemy.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
            {
                // Blend toward purple
                mat.AlbedoColor = mat.AlbedoColor.Lerp(new Color(0.6f, 0.4f, 0.8f), 0.3f);
            }
        }

        // Add slow particle effect
        var particles = CreateSlowParticles();
        particles.Name = "SlowParticles";
        enemy.AddChild(particles);
    }

    private GpuParticles3D CreateSlowParticles()
    {
        var particles = new GpuParticles3D();
        particles.Amount = 10;
        particles.Lifetime = 0.8f;
        particles.Explosiveness = 0f;
        particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.3f;
        material.Direction = new Vector3(0, -1, 0);
        material.Spread = 30f;
        material.InitialVelocityMin = 0.5f;
        material.InitialVelocityMax = 1f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.1f;
        material.Color = new Color(0.6f, 0.4f, 0.9f, 0.7f);

        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.1f, 0.1f);
        particles.DrawPass1 = quadMesh;

        particles.Position = new Vector3(0, 1f, 0);

        return particles;
    }

    protected override void OnDeactivate()
    {
        // Remove slow visuals from all enemies
        RemoveSlowVisuals();

        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print("[EngineOfTomorrow3D] Deactivated - Enemies returned to normal speed");
    }

    private void RemoveSlowVisuals()
    {
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D enemy)
            {
                // Remove slow particles
                var slowParticles = enemy.GetNodeOrNull<GpuParticles3D>("SlowParticles");
                if (slowParticles != null)
                {
                    slowParticles.QueueFree();
                }
            }
        }
    }

    /// <summary>
    /// Get the speed multiplier for enemies (0.5 when active).
    /// </summary>
    public float GetSlowMultiplier()
    {
        return IsActive ? SlowMultiplier : 1f;
    }

    /// <summary>
    /// Static check for current slow multiplier.
    /// </summary>
    public static float GetGlobalSlowMultiplier()
    {
        if (Instance != null && IsInstanceValid(Instance) && Instance.IsActive)
        {
            return Instance.SlowMultiplier;
        }
        return 1f;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Instance == this) Instance = null;
    }
}

/// <summary>
/// Visual effect for Engine of Tomorrow (shown on player).
/// </summary>
public partial class EngineOfTomorrowEffect3D : Node3D
{
    private OmniLight3D? _light;
    private MeshInstance3D? _clockRing;
    private float _rotationAngle;

    public override void _Ready()
    {
        CreateLight();
        CreateClockRing();
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.6f, 0.4f, 0.9f);
        _light.LightEnergy = 1.5f;
        _light.OmniRange = 5f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);
    }

    private void CreateClockRing()
    {
        _clockRing = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 1.5f;
        torus.OuterRadius = 1.7f;
        torus.Rings = 32;
        torus.RingSegments = 4;
        _clockRing.Mesh = torus;

        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.6f, 0.4f, 0.9f, 0.5f);
        material.EmissionEnabled = true;
        material.Emission = new Color(0.5f, 0.3f, 0.8f);
        material.EmissionEnergyMultiplier = 1f;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _clockRing.MaterialOverride = material;

        _clockRing.Position = new Vector3(0, 0.1f, 0);
        _clockRing.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(_clockRing);
    }

    public override void _Process(double delta)
    {
        // Slowly rotate the clock ring (backwards - time slowing effect)
        _rotationAngle -= (float)delta * 0.5f;
        if (_clockRing != null)
        {
            _clockRing.Rotation = new Vector3(Mathf.Pi / 2, _rotationAngle, 0);
        }

        // Pulse light
        float pulse = (Mathf.Sin(_rotationAngle * 4f) + 1f) * 0.5f;
        if (_light != null)
        {
            _light.LightEnergy = 1f + pulse * 0.5f;
        }
    }

    public void FadeOut()
    {
        var tween = CreateTween();
        tween.SetParallel(true);

        if (_clockRing?.MaterialOverride is StandardMaterial3D mat)
        {
            tween.TweenProperty(mat, "albedo_color:a", 0f, 0.3f);
        }
        if (_light != null)
        {
            tween.TweenProperty(_light, "light_energy", 0f, 0.3f);
        }

        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
