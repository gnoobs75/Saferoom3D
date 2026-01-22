using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Second Wind ability - Automatically heal when dropping to low health.
/// Passive emergency heal.
/// </summary>
public partial class SecondWind3D : Ability3D
{
    public override string AbilityId => "second_wind";
    public override string AbilityName => "Second Wind";
    public override string Description => "When your health drops below 20%, automatically heal for 50% of max HP.";
    public override AbilityType Type => AbilityType.Passive;

    public override float DefaultCooldown => 180f; // 3 minute cooldown
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 10;

    // Second Wind stats
    public float HealPercent { get; private set; } = 0.50f; // 50% of max HP
    public float TriggerThreshold { get; private set; } = 0.20f; // Triggers at 20% HP

    private bool _isMonitoring;
    private float _checkTimer;
    private const float CheckInterval = 0.1f;

    public override void _Ready()
    {
        base._Ready();
        _isMonitoring = true;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!_isMonitoring || IsOnCooldown) return;

        _checkTimer += (float)delta;
        if (_checkTimer >= CheckInterval)
        {
            _checkTimer = 0f;
            CheckHealthThreshold();
        }
    }

    private void CheckHealthThreshold()
    {
        if (Player == null) return;

        float currentHealth = 0f;
        float maxHealth = 0f;

        // Try to get health values from player
        if (Player.HasMethod("GetCurrentHealth") && Player.HasMethod("GetMaxHealth"))
        {
            currentHealth = (float)Player.Call("GetCurrentHealth");
            maxHealth = (float)Player.Call("GetMaxHealth");
        }
        else if (Player.Get("CurrentHealth").VariantType != Variant.Type.Nil)
        {
            currentHealth = (float)Player.Get("CurrentHealth");
            maxHealth = (float)Player.Get("MaxHealth");
        }
        else
        {
            return; // Can't monitor health
        }

        if (maxHealth <= 0) return;

        float healthPercent = currentHealth / maxHealth;

        if (healthPercent <= TriggerThreshold && healthPercent > 0)
        {
            // Trigger Second Wind!
            TriggerSecondWind(maxHealth);
        }
    }

    private void TriggerSecondWind(float maxHealth)
    {
        int healAmount = (int)(maxHealth * HealPercent);

        // Heal the player
        HealPlayer(healAmount);

        // Create dramatic visual effect
        CreateSecondWindEffect();

        // Start cooldown
        StartCooldown();

        GD.Print($"[SecondWind3D] Triggered! Healed for {healAmount} HP");
    }

    private void CreateSecondWindEffect()
    {
        if (Player == null) return;

        var effect = new SecondWindEffect();
        effect.Name = "SecondWindEffect";
        effect.Initialize(Player);
        GetTree().Root.AddChild(effect);
    }

    // Passive abilities don't have a manual activation - this is auto-triggered
    protected override void Activate()
    {
        // Second Wind auto-triggers via CheckHealthThreshold, not manual activation
        // This method exists to satisfy the abstract requirement
    }

    // Override TryActivate to prevent manual activation (it's passive)
    public override bool TryActivate()
    {
        // Second Wind cannot be manually activated
        GD.Print("[SecondWind3D] This is a passive ability - it triggers automatically at low health");
        return false;
    }
}

/// <summary>
/// Visual effect for Second Wind - dramatic healing surge.
/// </summary>
public partial class SecondWindEffect : Node3D
{
    private Node3D? _player;

    public void Initialize(Node3D player)
    {
        _player = player;
    }

    public override void _Ready()
    {
        if (_player == null)
        {
            QueueFree();
            return;
        }

        GlobalPosition = _player.GlobalPosition;

        CreateHealingBurst();
        CreateRisingEnergy();
        CreateScreenFlash();
        CreateSoundPulse();

        // Auto-cleanup
        var timer = GetTree().CreateTimer(2f);
        timer.Timeout += () => QueueFree();
    }

    private void CreateHealingBurst()
    {
        // Expanding green ring
        var ring = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 0.2f;
        torus.OuterRadius = 0.4f;
        ring.Mesh = torus;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.3f, 1f, 0.4f, 0.9f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.2f, 0.9f, 0.3f);
        mat.EmissionEnergyMultiplier = 4f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        ring.MaterialOverride = mat;

        ring.GlobalPosition = GlobalPosition + Vector3.Up * 0.5f;
        ring.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(ring);

        var tween = ring.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(ring, "scale", Vector3.One * 8f, 0.6f).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(mat, "albedo_color:a", 0f, 0.6f);
    }

    private void CreateRisingEnergy()
    {
        // Healing particles rising up
        var particles = new GpuParticles3D();
        particles.GlobalPosition = GlobalPosition;
        particles.Amount = 50;
        particles.Lifetime = 1f;
        particles.Explosiveness = 0.5f;
        particles.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 1f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 20f;
        mat.InitialVelocityMin = 5f;
        mat.InitialVelocityMax = 10f;
        mat.Gravity = new Vector3(0, 2, 0);
        mat.ScaleMin = 0.1f;
        mat.ScaleMax = 0.2f;
        mat.Color = new Color(0.4f, 1f, 0.5f, 0.9f);
        particles.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.08f;
        mesh.Height = 0.16f;
        particles.DrawPass1 = mesh;

        AddChild(particles);
    }

    private void CreateScreenFlash()
    {
        // Bright flash effect
        var light = new OmniLight3D();
        light.GlobalPosition = GlobalPosition + Vector3.Up;
        light.LightColor = new Color(0.4f, 1f, 0.5f);
        light.LightEnergy = 8f;
        light.OmniRange = 15f;
        AddChild(light);

        var tween = light.CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, 0.8f);
    }

    private void CreateSoundPulse()
    {
        // Visual sound wave
        for (int i = 0; i < 3; i++)
        {
            var wave = new MeshInstance3D();
            var sphere = new SphereMesh();
            sphere.Radius = 0.5f;
            sphere.Height = 1f;
            wave.Mesh = sphere;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.5f, 1f, 0.6f, 0.3f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.4f, 0.9f, 0.5f);
            mat.EmissionEnergyMultiplier = 2f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            wave.MaterialOverride = mat;

            wave.GlobalPosition = GlobalPosition + Vector3.Up;
            AddChild(wave);

            var tween = wave.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(wave, "scale", Vector3.One * (5f + i * 2f), 0.5f + i * 0.15f)
                .SetDelay(i * 0.1f);
            tween.TweenProperty(mat, "albedo_color:a", 0f, 0.5f + i * 0.15f)
                .SetDelay(i * 0.1f);
        }
    }
}
