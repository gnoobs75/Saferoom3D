using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Dead Man's Rally ability - Toggle that grants damage bonus at low health.
/// Risk/reward toggle ability that prevents healing but increases damage.
/// </summary>
public partial class DeadMansRally3D : Ability3D
{
    public override string AbilityId => "dead_mans_rally";
    public override string AbilityName => "Dead Man's Rally";
    public override string Description => "Toggle: Gain damage bonus at low health, but prevent healing.";
    public override AbilityType Type => AbilityType.Toggle;

    public override float DefaultCooldown => 0f; // No cooldown, toggle
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 4;

    // Dead Man's Rally stats
    public float LowHealthThreshold { get; private set; } = 0.3f; // 30% HP
    public float CriticalThreshold { get; private set; } = 0.15f; // 15% HP
    public float LowHealthBonus { get; private set; } = 2f; // +100% damage
    public float CriticalBonus { get; private set; } = 3f; // +200% damage

    private DeadMansRallyEffect3D? _effect;

    protected override void Activate()
    {
        // Create visual effect on player
        _effect = new DeadMansRallyEffect3D();
        _effect.Name = "DeadMansRallyEffect";
        _effect.Initialize(this);

        if (Player != null)
        {
            Player.AddChild(_effect);
            _effect.Position = Vector3.Zero;
        }

        SetActiveToggle();

        GD.Print("[DeadMansRally3D] Toggled ON - Damage bonus at low HP, healing disabled");
    }

    protected override void OnDeactivate()
    {
        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print("[DeadMansRally3D] Toggled OFF");
    }

    /// <summary>
    /// Get the current damage bonus multiplier based on health.
    /// </summary>
    public float GetDamageBonus()
    {
        if (!IsActive || Player == null) return 1f;

        float healthPercent = (float)Player.CurrentHealth / Player.MaxHealth;

        if (healthPercent <= CriticalThreshold)
        {
            return CriticalBonus;
        }
        else if (healthPercent <= LowHealthThreshold)
        {
            return LowHealthBonus;
        }

        return 1f; // No bonus above threshold
    }

    /// <summary>
    /// Check if healing is blocked (always blocked when active).
    /// </summary>
    public bool IsHealingBlocked()
    {
        return IsActive;
    }

    /// <summary>
    /// Get the current state for visual feedback.
    /// </summary>
    public DeadMansRallyState GetCurrentState()
    {
        if (!IsActive || Player == null) return DeadMansRallyState.Inactive;

        float healthPercent = (float)Player.CurrentHealth / Player.MaxHealth;

        if (healthPercent <= CriticalThreshold)
        {
            return DeadMansRallyState.Critical;
        }
        else if (healthPercent <= LowHealthThreshold)
        {
            return DeadMansRallyState.LowHealth;
        }

        return DeadMansRallyState.Active;
    }
}

public enum DeadMansRallyState
{
    Inactive,
    Active, // Above threshold, no bonus yet
    LowHealth, // 2x damage
    Critical // 3x damage
}

/// <summary>
/// Visual effect for Dead Man's Rally.
/// </summary>
public partial class DeadMansRallyEffect3D : Node3D
{
    private DeadMansRally3D? _ability;
    private OmniLight3D? _light;
    private GpuParticles3D? _particles;
    private float _pulseTimer;
    private DeadMansRallyState _lastState;

    public void Initialize(DeadMansRally3D ability)
    {
        _ability = ability;
    }

    public override void _Ready()
    {
        CreateLight();
        CreateParticles();
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.8f, 0.2f, 0.2f);
        _light.LightEnergy = 0.5f;
        _light.OmniRange = 3f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 20;
        _particles.Lifetime = 0.6f;
        _particles.Explosiveness = 0f;
        _particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.4f;
        material.Direction = new Vector3(0, -1, 0);
        material.Spread = 45f;
        material.InitialVelocityMin = 0.5f;
        material.InitialVelocityMax = 1f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.03f;
        material.ScaleMax = 0.08f;
        material.Color = new Color(0.8f, 0.1f, 0.1f, 0.8f);

        _particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.1f, 0.1f);
        _particles.DrawPass1 = quadMesh;

        _particles.Position = new Vector3(0, 0.5f, 0);
        AddChild(_particles);
    }

    public override void _Process(double delta)
    {
        if (_ability == null) return;

        _pulseTimer += (float)delta;
        var state = _ability.GetCurrentState();

        // Update visuals based on state
        if (state != _lastState)
        {
            UpdateStateVisuals(state);
            _lastState = state;
        }

        // Pulse intensity based on state
        float pulseSpeed = state switch
        {
            DeadMansRallyState.Critical => 12f,
            DeadMansRallyState.LowHealth => 6f,
            _ => 3f
        };

        float pulse = (Mathf.Sin(_pulseTimer * pulseSpeed) + 1f) * 0.5f;

        if (_light != null)
        {
            float baseEnergy = state switch
            {
                DeadMansRallyState.Critical => 3f,
                DeadMansRallyState.LowHealth => 2f,
                _ => 0.5f
            };
            _light.LightEnergy = baseEnergy + pulse * 2f;
        }
    }

    private void UpdateStateVisuals(DeadMansRallyState state)
    {
        Color color = state switch
        {
            DeadMansRallyState.Critical => new Color(1f, 0f, 0f),
            DeadMansRallyState.LowHealth => new Color(0.9f, 0.2f, 0.1f),
            _ => new Color(0.6f, 0.2f, 0.2f)
        };

        if (_light != null)
        {
            _light.LightColor = color;
        }

        if (_particles?.ProcessMaterial is ParticleProcessMaterial mat)
        {
            mat.Color = new Color(color.R, color.G, color.B, 0.8f);

            // More particles at lower health
            _particles.Amount = state switch
            {
                DeadMansRallyState.Critical => 50,
                DeadMansRallyState.LowHealth => 35,
                _ => 15
            };
        }

        // Log state change
        string stateStr = state switch
        {
            DeadMansRallyState.Critical => "CRITICAL (3x damage)",
            DeadMansRallyState.LowHealth => "LOW HEALTH (2x damage)",
            _ => "active (no bonus yet)"
        };
        GD.Print($"[DeadMansRally] State: {stateStr}");
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
