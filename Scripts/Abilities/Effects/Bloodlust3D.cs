using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Bloodlust ability - Heal a percentage of max HP for each kill during duration.
/// Sustain ability for aggressive playstyles.
/// </summary>
public partial class Bloodlust3D : Ability3D
{
    public override string AbilityId => "bloodlust";
    public override string AbilityName => "Bloodlust";
    public override string Description => "For the next 15 seconds, heal 5% of your max HP with each enemy kill.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 45f;
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 11;

    // Bloodlust stats
    public float Duration { get; private set; } = 15f;
    public float HealPercentPerKill { get; private set; } = 0.05f; // 5% max HP per kill

    private BloodlustEffect? _effect;
    private int _killsDuringEffect;

    protected override void Activate()
    {
        if (Player == null) return;

        _killsDuringEffect = 0;

        // Subscribe to enemy death events
        SubscribeToKills();

        // Create visual effect
        _effect = new BloodlustEffect();
        _effect.Name = "BloodlustEffect";
        _effect.Initialize(Player);
        GetTree().Root.AddChild(_effect);

        SetActiveWithDuration(Duration);

        GD.Print($"[Bloodlust3D] Activated - {HealPercentPerKill * 100}% heal per kill for {Duration}s");
    }

    private void SubscribeToKills()
    {
        // Connect to the GameManager's enemy killed signal if available
        var gameManager = GetTree().Root.GetNodeOrNull("GameManager3D");
        if (gameManager != null && gameManager.HasSignal("EnemyKilled"))
        {
            if (!gameManager.IsConnected("EnemyKilled", Callable.From<Node3D>(OnEnemyKilled)))
            {
                gameManager.Connect("EnemyKilled", Callable.From<Node3D>(OnEnemyKilled));
            }
        }
    }

    private void UnsubscribeFromKills()
    {
        var gameManager = GetTree().Root.GetNodeOrNull("GameManager3D");
        if (gameManager != null && gameManager.HasSignal("EnemyKilled"))
        {
            if (gameManager.IsConnected("EnemyKilled", Callable.From<Node3D>(OnEnemyKilled)))
            {
                gameManager.Disconnect("EnemyKilled", Callable.From<Node3D>(OnEnemyKilled));
            }
        }
    }

    private void OnEnemyKilled(Node3D enemy)
    {
        if (!IsActive || Player == null) return;

        _killsDuringEffect++;

        // Calculate heal amount
        float maxHealth = 100f;
        if (Player.HasMethod("GetMaxHealth"))
        {
            maxHealth = (float)Player.Call("GetMaxHealth");
        }
        else if (Player.Get("MaxHealth").VariantType != Variant.Type.Nil)
        {
            maxHealth = (float)Player.Get("MaxHealth");
        }

        int healAmount = (int)(maxHealth * HealPercentPerKill);
        HealPlayer(healAmount);

        // Visual feedback
        _effect?.OnKillHeal(healAmount);

        GD.Print($"[Bloodlust3D] Kill heal! +{healAmount} HP (Kill #{_killsDuringEffect})");
    }

    protected override void OnActiveTick(float dt)
    {
        // Flash warning at 3s remaining
        if (DurationRemaining <= 3f && DurationRemaining > 2.9f)
        {
            _effect?.StartWarning();
        }
    }

    protected override void OnDeactivate()
    {
        UnsubscribeFromKills();

        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print($"[Bloodlust3D] Deactivated - Total kills: {_killsDuringEffect}");
    }
}

/// <summary>
/// Visual effect for Bloodlust - red aura and heal pulses on kills.
/// </summary>
public partial class BloodlustEffect : Node3D
{
    private Node3D? _player;
    private MeshInstance3D? _auraMesh;
    private GpuParticles3D? _particles;
    private OmniLight3D? _light;
    private StandardMaterial3D? _material;
    private float _timer;
    private bool _isWarning;

    public void Initialize(Node3D player)
    {
        _player = player;
    }

    public override void _Ready()
    {
        CreateAura();
        CreateParticles();
        CreateLight();
    }

    private void CreateAura()
    {
        _auraMesh = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 1.2f;
        cylinder.BottomRadius = 1.2f;
        cylinder.Height = 2.2f;
        _auraMesh.Mesh = cylinder;

        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(0.8f, 0.1f, 0.1f, 0.12f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(0.7f, 0f, 0f);
        _material.EmissionEnergyMultiplier = 0.8f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _auraMesh.MaterialOverride = _material;

        AddChild(_auraMesh);
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 15;
        _particles.Lifetime = 1.2f;
        _particles.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.8f;
        mat.Direction = new Vector3(0, -1, 0);
        mat.Spread = 30f;
        mat.InitialVelocityMin = 1f;
        mat.InitialVelocityMax = 2f;
        mat.Gravity = new Vector3(0, 1, 0);
        mat.ScaleMin = 0.05f;
        mat.ScaleMax = 0.1f;
        mat.Color = new Color(0.8f, 0.1f, 0.1f, 0.7f);
        _particles.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        _particles.DrawPass1 = mesh;

        _particles.Position = Vector3.Up * 1.5f;
        AddChild(_particles);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.8f, 0.1f, 0.1f);
        _light.LightEnergy = 0.8f;
        _light.OmniRange = 4f;
        _light.Position = Vector3.Up * 1f;
        AddChild(_light);
    }

    public void OnKillHeal(int healAmount)
    {
        // Flash brighter on kill
        if (_light != null)
        {
            var tween = _light.CreateTween();
            tween.TweenProperty(_light, "light_energy", 3f, 0.1f);
            tween.TweenProperty(_light, "light_energy", 0.8f, 0.3f);
        }

        // Burst of healing particles
        var burst = new GpuParticles3D();
        burst.GlobalPosition = _player?.GlobalPosition ?? GlobalPosition;
        burst.GlobalPosition += Vector3.Up;
        burst.Amount = 20;
        burst.Lifetime = 0.5f;
        burst.Explosiveness = 1f;
        burst.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.3f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 60f;
        mat.InitialVelocityMin = 3f;
        mat.InitialVelocityMax = 6f;
        mat.Gravity = new Vector3(0, 2, 0);
        mat.ScaleMin = 0.08f;
        mat.ScaleMax = 0.15f;
        mat.Color = new Color(0.3f, 0.9f, 0.3f, 0.9f); // Green for healing
        burst.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.08f;
        mesh.Height = 0.16f;
        burst.DrawPass1 = mesh;

        GetTree().Root.AddChild(burst);

        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => burst.QueueFree();
    }

    public void StartWarning()
    {
        _isWarning = true;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _timer += dt;

        // Follow player
        if (_player != null && IsInstanceValid(_player))
        {
            GlobalPosition = _player.GlobalPosition;
        }

        // Pulse effect
        float pulse = (Mathf.Sin(_timer * 3f) + 1f) * 0.5f;

        if (_material != null)
        {
            if (_isWarning)
            {
                // Flash faster when about to expire
                float flash = (Mathf.Sin(_timer * 8f) + 1f) * 0.5f;
                _material.AlbedoColor = new Color(0.8f, 0.1f + flash * 0.3f, 0.1f, 0.15f + flash * 0.1f);
            }
            else
            {
                _material.AlbedoColor = new Color(0.8f, 0.1f, 0.1f, 0.1f + pulse * 0.05f);
            }
            _material.EmissionEnergyMultiplier = 0.6f + pulse * 0.4f;
        }

        if (_light != null && !_isWarning)
        {
            _light.LightEnergy = 0.6f + pulse * 0.4f;
        }
    }

    public void FadeOut()
    {
        if (_particles != null)
        {
            _particles.Emitting = false;
        }

        var tween = CreateTween();
        if (_material != null)
        {
            tween.TweenProperty(_material, "albedo_color:a", 0f, 0.5f);
        }
        if (_light != null)
        {
            tween.Parallel().TweenProperty(_light, "light_energy", 0f, 0.5f);
        }
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
