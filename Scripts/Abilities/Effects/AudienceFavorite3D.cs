using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Audience Favorite ability - Enemy kills have a chance to reset a random cooldown.
/// Duration buff that rewards aggressive play.
/// </summary>
public partial class AudienceFavorite3D : Ability3D
{
    public override string AbilityId => "audience_favorite";
    public override string AbilityName => "Audience Favorite";
    public override string Description => "Enemy kills have 40% chance to reset a random ability cooldown.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 90f;
    public override int DefaultManaCost => 0;

    // Audience Favorite stats
    public float Duration { get; private set; } = 20f;
    public float ResetChance { get; private set; } = 0.4f; // 40%

    private AudienceFavoriteEffect3D? _effect;

    protected override void Activate()
    {
        // Create visual effect on player
        _effect = new AudienceFavoriteEffect3D();
        _effect.Name = "AudienceFavoriteEffect";

        if (Player != null)
        {
            Player.AddChild(_effect);
            _effect.Position = Vector3.Zero;
        }

        SetActiveWithDuration(Duration);

        GD.Print($"[AudienceFavorite3D] Activated - {ResetChance * 100}% cooldown reset on kill");
    }

    protected override void OnDeactivate()
    {
        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print("[AudienceFavorite3D] Deactivated");
    }

    /// <summary>
    /// Called by AbilityManager when an enemy is killed while this is active.
    /// </summary>
    public void OnEnemyKilled()
    {
        if (!IsActive) return;

        // Roll for cooldown reset
        float roll = GD.Randf();
        if (roll <= ResetChance)
        {
            ResetRandomCooldown();
        }
    }

    private void ResetRandomCooldown()
    {
        var manager = AbilityManager3D.Instance;
        if (manager == null) return;

        var ability = manager.GetRandomAbilityOnCooldown();
        if (ability != null)
        {
            ability.ResetCooldown();

            // Visual feedback
            _effect?.ShowResetEffect();

            GD.Print($"[AudienceFavorite3D] Reset cooldown for {ability.AbilityName}!");
        }
    }
}

/// <summary>
/// Visual effect for Audience Favorite.
/// </summary>
public partial class AudienceFavoriteEffect3D : Node3D
{
    private OmniLight3D? _light;
    private GpuParticles3D? _particles;
    private float _pulseTimer;

    public override void _Ready()
    {
        CreateLight();
        CreateParticles();
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(1f, 0.8f, 0.2f);
        _light.LightEnergy = 1f;
        _light.OmniRange = 3f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 15;
        _particles.Lifetime = 0.6f;
        _particles.Explosiveness = 0f;
        _particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.5f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 30f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.1f;
        material.Color = new Color(1f, 0.8f, 0.2f, 0.8f);

        _particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.1f, 0.1f);
        _particles.DrawPass1 = quadMesh;

        _particles.Position = new Vector3(0, 0.5f, 0);
        AddChild(_particles);
    }

    public override void _Process(double delta)
    {
        _pulseTimer += (float)delta;
        float pulse = (Mathf.Sin(_pulseTimer * 3f) + 1f) * 0.5f;

        if (_light != null)
        {
            _light.LightEnergy = 0.8f + pulse * 0.4f;
        }
    }

    /// <summary>
    /// Show a burst effect when a cooldown is reset.
    /// </summary>
    public void ShowResetEffect()
    {
        // Bright flash
        if (_light != null)
        {
            _light.LightEnergy = 5f;

            var tween = CreateTween();
            tween.TweenProperty(_light, "light_energy", 1f, 0.3f);
        }

        // Burst particles
        var burst = new GpuParticles3D();
        burst.Amount = 30;
        burst.Lifetime = 0.4f;
        burst.Explosiveness = 1f;
        burst.OneShot = true;
        burst.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.3f;
        material.Direction = new Vector3(0, 0, 0);
        material.Spread = 180f;
        material.InitialVelocityMin = 4f;
        material.InitialVelocityMax = 8f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.1f;
        material.ScaleMax = 0.2f;
        material.Color = new Color(1f, 0.9f, 0.3f);

        burst.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.15f, 0.15f);
        burst.DrawPass1 = quadMesh;

        burst.Position = new Vector3(0, 1f, 0);
        AddChild(burst);

        // Cleanup after effect
        var timer = GetTree().CreateTimer(0.5f);
        timer.Timeout += () => burst.QueueFree();

        Core.SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);
    }

    public void FadeOut()
    {
        if (_particles != null) _particles.Emitting = false;

        var tween = CreateTween();
        if (_light != null)
        {
            tween.TweenProperty(_light, "light_energy", 0f, 0.3f);
        }
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
