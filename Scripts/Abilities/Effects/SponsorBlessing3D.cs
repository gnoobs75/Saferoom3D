using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Sponsor's Blessing ability - Spawn a health pickup at cursor location.
/// Utility ability for self-sustain during combat.
/// </summary>
public partial class SponsorBlessing3D : Ability3D, ITargetedAbility
{
    public override string AbilityId => "sponsor_blessing";
    public override string AbilityName => "Sponsor's Blessing";
    public override string Description => "Spawn a health pickup that restores 50 HP.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 120f; // 2 minutes
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 2;

    // Sponsor's Blessing stats
    public int HealAmount { get; private set; } = 50;
    public float PickupDuration { get; private set; } = 30f;

    // ITargetedAbility
    public float TargetRadius => 1f; // Small targeting indicator
    public Color TargetColor => new(0.3f, 1f, 0.4f); // Green

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Spawn the health pickup
        var pickup = new SponsorBlessingPickup3D();
        pickup.Name = "SponsorBlessingPickup";
        pickup.GlobalPosition = _targetPosition + new Vector3(0, 0.5f, 0);
        pickup.Initialize(HealAmount, PickupDuration);

        GetTree().Root.AddChild(pickup);

        Core.SoundManager3D.Instance?.PlayMagicSound(_targetPosition);

        GD.Print($"[SponsorBlessing3D] Spawned health pickup (+{HealAmount} HP)");
    }
}

/// <summary>
/// Health pickup spawned by Sponsor's Blessing.
/// </summary>
public partial class SponsorBlessingPickup3D : Area3D
{
    private int _healAmount;
    private float _duration;
    private float _timeRemaining;

    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;
    private StandardMaterial3D? _material;
    private float _bobTimer;
    private float _rotationAngle;

    public void Initialize(int healAmount, float duration)
    {
        _healAmount = healAmount;
        _duration = duration;
        _timeRemaining = duration;
    }

    public override void _Ready()
    {
        CreateVisuals();
        SetupCollision();

        AddToGroup("Pickups");
    }

    private void CreateVisuals()
    {
        // Glowing orb/cross
        _mesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.3f;
        sphere.Height = 0.6f;
        _mesh.Mesh = sphere;

        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(0.3f, 1f, 0.4f, 0.9f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(0.2f, 0.9f, 0.3f);
        _material.EmissionEnergyMultiplier = 2f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _mesh.MaterialOverride = _material;

        AddChild(_mesh);

        // Healing cross overlay
        CreateCross();

        // Light
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.3f, 1f, 0.4f);
        _light.LightEnergy = 2f;
        _light.OmniRange = 4f;
        AddChild(_light);
    }

    private void CreateCross()
    {
        // Vertical bar
        var vBar = new MeshInstance3D();
        var vBox = new BoxMesh();
        vBox.Size = new Vector3(0.08f, 0.4f, 0.08f);
        vBar.Mesh = vBox;

        var crossMat = new StandardMaterial3D();
        crossMat.AlbedoColor = Colors.White;
        crossMat.EmissionEnabled = true;
        crossMat.Emission = Colors.White;
        crossMat.EmissionEnergyMultiplier = 3f;
        crossMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        vBar.MaterialOverride = crossMat;

        AddChild(vBar);

        // Horizontal bar
        var hBar = new MeshInstance3D();
        var hBox = new BoxMesh();
        hBox.Size = new Vector3(0.3f, 0.08f, 0.08f);
        hBar.Mesh = hBox;
        hBar.MaterialOverride = crossMat;

        AddChild(hBar);
    }

    private void SetupCollision()
    {
        var shape = new CollisionShape3D();
        var sphere = new SphereShape3D();
        sphere.Radius = 1f; // Generous pickup radius
        shape.Shape = sphere;
        AddChild(shape);

        CollisionLayer = 32; // Pickup layer
        CollisionMask = 1; // Player layer

        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("Player"))
        {
            // Heal player
            if (body.HasMethod("Heal"))
            {
                body.Call("Heal", _healAmount);
            }
            else if (Player.FPSController.Instance != null)
            {
                Player.FPSController.Instance.Heal(_healAmount);
            }

            GD.Print($"[SponsorBlessingPickup] Healed player for {_healAmount} HP");

            // Pickup effect
            CreatePickupEffect();

            Core.SoundManager3D.Instance?.PlayHealSound(GlobalPosition);

            QueueFree();
        }
    }

    private void CreatePickupEffect()
    {
        // Burst of particles upward
        var particles = new GpuParticles3D();
        particles.Amount = 30;
        particles.Lifetime = 0.5f;
        particles.Explosiveness = 1f;
        particles.OneShot = true;
        particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.3f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 45f;
        material.InitialVelocityMin = 3f;
        material.InitialVelocityMax = 6f;
        material.Gravity = new Vector3(0, -2, 0);
        material.ScaleMin = 0.1f;
        material.ScaleMax = 0.2f;
        material.Color = new Color(0.4f, 1f, 0.5f);

        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.15f, 0.15f);
        particles.DrawPass1 = quadMesh;

        particles.GlobalPosition = GlobalPosition;
        GetTree().Root.AddChild(particles);

        // Auto-cleanup
        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => particles.QueueFree();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        _timeRemaining -= dt;

        // Bob up and down
        _bobTimer += dt * 3f;
        float bob = Mathf.Sin(_bobTimer) * 0.1f;

        if (_mesh != null)
        {
            _mesh.Position = new Vector3(0, bob, 0);
        }

        // Rotate
        _rotationAngle += dt * 2f;
        Rotation = new Vector3(0, _rotationAngle, 0);

        // Pulse light
        float pulse = (Mathf.Sin(_bobTimer * 2f) + 1f) * 0.5f;
        if (_light != null)
        {
            _light.LightEnergy = 1.5f + pulse;
        }

        // Fade out when about to expire
        if (_timeRemaining < 5f)
        {
            float fade = _timeRemaining / 5f;
            if (_material != null)
            {
                float flash = (Mathf.Sin(_bobTimer * 8f) + 1f) * 0.5f;
                _material.AlbedoColor = new Color(0.3f, 1f, 0.4f, 0.5f + flash * 0.4f);
            }
        }

        // Expire
        if (_timeRemaining <= 0)
        {
            ExpireWithEffect();
        }
    }

    private void ExpireWithEffect()
    {
        // Fade out effect
        var tween = CreateTween();
        if (_material != null)
        {
            tween.TweenProperty(_material, "albedo_color:a", 0f, 0.3f);
        }
        if (_light != null)
        {
            tween.Parallel().TweenProperty(_light, "light_energy", 0f, 0.3f);
        }
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
