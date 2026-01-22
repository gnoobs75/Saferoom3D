using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Berserk spell - Double movement and attack speed for a duration.
/// Self-buff that makes the player faster and deadlier.
/// </summary>
public partial class Berserk3D : Spell3D
{
    public override string AbilityId => "berserk";
    public override string AbilityName => "Berserk";
    public override string Description => "Double your movement and attack speed.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 120f; // 2 minutes
    public override int DefaultManaCost => 25;
    public override int RequiredLevel => 8;

    // Berserk stats
    public float Duration { get; private set; } = 15f;
    public float SpeedMultiplier { get; private set; } = 2f;
    public float AttackSpeedMultiplier { get; private set; } = 2f; // Cooldowns halved

    private BerserkEffect3D? _effect;

    protected override void Activate()
    {
        // Create visual effect on player
        _effect = new BerserkEffect3D();
        _effect.Name = "BerserkEffect";

        if (Player != null)
        {
            Player.AddChild(_effect);
            _effect.Position = Vector3.Zero;
        }

        SetActiveWithDuration(Duration);

        GD.Print($"[Berserk3D] Activated for {Duration}s - Speed x{SpeedMultiplier}");
    }

    protected override void OnActiveTick(float dt)
    {
        // Flash warning at 3s remaining
        if (DurationRemaining <= 3f && DurationRemaining > 2.9f)
        {
            _effect?.StartWarningFlash();
        }
    }

    protected override void OnDeactivate()
    {
        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print("[Berserk3D] Deactivated");
    }

    /// <summary>
    /// Get the movement speed multiplier (2x when active).
    /// </summary>
    public float GetMoveSpeedMultiplier()
    {
        return IsActive ? SpeedMultiplier : 1f;
    }

    /// <summary>
    /// Get the attack cooldown multiplier (0.5x when active = faster attacks).
    /// </summary>
    public float GetAttackCooldownMultiplier()
    {
        return IsActive ? 1f / AttackSpeedMultiplier : 1f;
    }
}

/// <summary>
/// Visual effect for berserk mode.
/// </summary>
public partial class BerserkEffect3D : Node3D
{
    private OmniLight3D? _auraLight;
    private GpuParticles3D? _particles;
    private float _pulseTimer;
    private bool _isWarning;

    public override void _Ready()
    {
        CreateAuraLight();
        CreateParticles();
    }

    private void CreateAuraLight()
    {
        _auraLight = new OmniLight3D();
        _auraLight.LightColor = new Color(1f, 0.3f, 0.1f);
        _auraLight.LightEnergy = 2f;
        _auraLight.OmniRange = 4f;
        _auraLight.Position = new Vector3(0, 1f, 0);
        AddChild(_auraLight);
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 30;
        _particles.Lifetime = 0.5f;
        _particles.Explosiveness = 0f;
        _particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.5f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 45f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.15f;
        material.Color = new Color(1f, 0.4f, 0.1f);

        _particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.15f, 0.15f);
        _particles.DrawPass1 = quadMesh;

        _particles.Position = new Vector3(0, 0.5f, 0);
        AddChild(_particles);
    }

    public override void _Process(double delta)
    {
        _pulseTimer += (float)delta;

        float pulse = (Mathf.Sin(_pulseTimer * 6f) + 1f) * 0.5f;

        if (_auraLight != null)
        {
            if (_isWarning)
            {
                // Fast flashing when about to expire
                float flash = (Mathf.Sin(_pulseTimer * 15f) + 1f) * 0.5f;
                _auraLight.LightColor = new Color(1f, 0.3f + flash * 0.3f, 0.1f);
                _auraLight.LightEnergy = 1f + flash * 2f;
            }
            else
            {
                _auraLight.LightEnergy = 2f + pulse;
            }
        }
    }

    public void StartWarningFlash()
    {
        _isWarning = true;
    }

    public void FadeOut()
    {
        if (_particles != null) _particles.Emitting = false;

        var tween = CreateTween();
        if (_auraLight != null)
        {
            tween.TweenProperty(_auraLight, "light_energy", 0f, 0.3f);
        }
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
