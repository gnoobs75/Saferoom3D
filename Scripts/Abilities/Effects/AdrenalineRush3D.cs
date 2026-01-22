using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Adrenaline Rush ability - Become immune to crowd control effects.
/// Anti-CC defensive ability.
/// </summary>
public partial class AdrenalineRush3D : Ability3D
{
    public override string AbilityId => "adrenaline_rush";
    public override string AbilityName => "Adrenaline Rush";
    public override string Description => "A surge of adrenaline makes you immune to stuns, slows, and fear for 8 seconds.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 120f; // 2 minute cooldown
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 14;

    // Adrenaline Rush stats
    public float Duration { get; private set; } = 8f;
    public float MovementBonus { get; private set; } = 0.15f; // 15% movement speed bonus

    private AdrenalineRushEffect? _effect;

    protected override void Activate()
    {
        if (Player == null) return;

        // Apply CC immunity
        SetCCImmune(true);

        // Apply movement speed bonus
        ApplyMovementBonus(true);

        // Create visual effect
        _effect = new AdrenalineRushEffect();
        _effect.Name = "AdrenalineRushEffect";
        _effect.Initialize(Player);
        GetTree().Root.AddChild(_effect);

        // Break any existing CC
        ClearExistingCC();

        SetActiveWithDuration(Duration);

        GD.Print($"[AdrenalineRush3D] Activated - CC immune for {Duration}s");
    }

    private void SetCCImmune(bool immune)
    {
        if (Player == null) return;

        if (Player.HasMethod("SetCCImmune"))
        {
            Player.Call("SetCCImmune", immune);
        }
    }

    private void ApplyMovementBonus(bool apply)
    {
        if (Player == null) return;

        if (Player.HasMethod("SetMovementSpeedMultiplier"))
        {
            float multiplier = apply ? (1f + MovementBonus) : 1f;
            Player.Call("SetMovementSpeedMultiplier", multiplier);
        }
    }

    private void ClearExistingCC()
    {
        if (Player == null) return;

        // Clear stun
        if (Player.HasMethod("SetStunned"))
        {
            Player.Call("SetStunned", false);
        }

        // Clear slow
        if (Player.HasMethod("ClearSlow"))
        {
            Player.Call("ClearSlow");
        }

        // Clear fear
        if (Player.HasMethod("SetFeared"))
        {
            Player.Call("SetFeared", false);
        }

        // Clear root
        if (Player.HasMethod("SetRooted"))
        {
            Player.Call("SetRooted", false);
        }
    }

    protected override void OnActiveTick(float dt)
    {
        // Flash warning at 2s remaining
        if (DurationRemaining <= 2f && DurationRemaining > 1.9f)
        {
            _effect?.StartWarning();
        }
    }

    protected override void OnDeactivate()
    {
        SetCCImmune(false);
        ApplyMovementBonus(false);

        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print("[AdrenalineRush3D] Deactivated");
    }
}

/// <summary>
/// Visual effect for Adrenaline Rush - energetic yellow/orange aura.
/// </summary>
public partial class AdrenalineRushEffect : Node3D
{
    private Node3D? _player;
    private MeshInstance3D? _auraMesh;
    private GpuParticles3D? _particles;
    private GpuParticles3D? _speedLines;
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
        CreateInitialBurst();
        CreateAura();
        CreateParticles();
        CreateSpeedLines();
        CreateLight();
    }

    private void CreateInitialBurst()
    {
        // Explosive burst on activation
        var burst = new GpuParticles3D();
        burst.GlobalPosition = _player?.GlobalPosition ?? GlobalPosition;
        burst.GlobalPosition += Vector3.Up;
        burst.Amount = 40;
        burst.Lifetime = 0.5f;
        burst.Explosiveness = 1f;
        burst.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.5f;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 180f;
        mat.InitialVelocityMin = 8f;
        mat.InitialVelocityMax = 15f;
        mat.Gravity = new Vector3(0, -5, 0);
        mat.ScaleMin = 0.08f;
        mat.ScaleMax = 0.15f;
        mat.Color = new Color(1f, 0.8f, 0.2f, 0.9f);
        burst.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.08f;
        mesh.Height = 0.16f;
        burst.DrawPass1 = mesh;

        GetTree().Root.AddChild(burst);

        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => burst.QueueFree();
    }

    private void CreateAura()
    {
        _auraMesh = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 1.3f;
        cylinder.BottomRadius = 1.3f;
        cylinder.Height = 2.3f;
        _auraMesh.Mesh = cylinder;

        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(1f, 0.7f, 0.1f, 0.1f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(1f, 0.6f, 0f);
        _material.EmissionEnergyMultiplier = 1f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _auraMesh.MaterialOverride = _material;

        AddChild(_auraMesh);
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 20;
        _particles.Lifetime = 0.8f;
        _particles.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.8f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 30f;
        mat.InitialVelocityMin = 3f;
        mat.InitialVelocityMax = 5f;
        mat.Gravity = new Vector3(0, 2, 0);
        mat.ScaleMin = 0.05f;
        mat.ScaleMax = 0.1f;
        mat.Color = new Color(1f, 0.8f, 0.2f, 0.8f);
        _particles.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        _particles.DrawPass1 = mesh;

        _particles.Position = Vector3.Up;
        AddChild(_particles);
    }

    private void CreateSpeedLines()
    {
        // Horizontal speed lines for motion feel
        _speedLines = new GpuParticles3D();
        _speedLines.Amount = 12;
        _speedLines.Lifetime = 0.3f;
        _speedLines.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        mat.EmissionRingRadius = 1.2f;
        mat.EmissionRingInnerRadius = 1f;
        mat.EmissionRingHeight = 1f;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 0f;
        mat.InitialVelocityMin = 0f;
        mat.InitialVelocityMax = 0f;
        mat.ScaleMin = 0.02f;
        mat.ScaleMax = 0.05f;
        mat.Color = new Color(1f, 0.9f, 0.5f, 0.5f);
        _speedLines.ProcessMaterial = mat;

        // Elongated capsules for speed lines
        var capsule = new CapsuleMesh();
        capsule.Radius = 0.02f;
        capsule.Height = 0.4f;
        _speedLines.DrawPass1 = capsule;

        _speedLines.Position = Vector3.Up;
        AddChild(_speedLines);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(1f, 0.7f, 0.2f);
        _light.LightEnergy = 1.2f;
        _light.OmniRange = 5f;
        _light.Position = Vector3.Up * 1f;
        AddChild(_light);
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

        // Fast pulse effect for energy feel
        float pulse = (Mathf.Sin(_timer * 8f) + 1f) * 0.5f;

        if (_material != null)
        {
            if (_isWarning)
            {
                // Faster flash when about to expire
                float flash = (Mathf.Sin(_timer * 15f) + 1f) * 0.5f;
                _material.AlbedoColor = new Color(1f, 0.5f + flash * 0.3f, 0.1f, 0.1f + flash * 0.15f);
            }
            else
            {
                _material.AlbedoColor = new Color(1f, 0.7f, 0.1f, 0.08f + pulse * 0.07f);
            }
            _material.EmissionEnergyMultiplier = 0.8f + pulse * 0.6f;
        }

        if (_light != null)
        {
            _light.LightEnergy = 0.8f + pulse * 0.6f;
        }

        // Rotate speed lines
        if (_speedLines != null)
        {
            _speedLines.Rotation = new Vector3(0, _timer * 3f, 0);
        }
    }

    public void FadeOut()
    {
        if (_particles != null)
        {
            _particles.Emitting = false;
        }
        if (_speedLines != null)
        {
            _speedLines.Emitting = false;
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
