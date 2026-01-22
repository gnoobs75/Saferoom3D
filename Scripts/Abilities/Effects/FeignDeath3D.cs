using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Feign Death ability - Play dead to drop all aggro.
/// Survival/escape ability.
/// </summary>
public partial class FeignDeath3D : Ability3D
{
    public override string AbilityId => "feign_death";
    public override string AbilityName => "Feign Death";
    public override string Description => "Play dead to make enemies lose interest. Any action cancels the effect.";
    public override AbilityType Type => AbilityType.Toggle;

    public override float DefaultCooldown => 90f;
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 9;

    // Feign Death stats
    public float AggroDropRadius { get; private set; } = 30f;
    public float MaxDuration { get; private set; } = 30f; // Auto-cancel after 30s

    private FeignDeathEffect? _effect;
    private float _feignTimer;

    protected override void Activate()
    {
        if (Player == null) return;

        // Drop all enemy aggro
        DropAggro();

        // Disable player input temporarily
        SetPlayerFeigning(true);

        // Create visual effect (player "falls down")
        _effect = new FeignDeathEffect();
        _effect.Name = "FeignDeathEffect";
        _effect.Initialize(Player);
        GetTree().Root.AddChild(_effect);

        _feignTimer = 0f;
        IsActive = true;

        GD.Print("[FeignDeath3D] Playing dead...");
    }

    private void DropAggro()
    {
        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy && Player != null)
            {
                float dist = enemy.GlobalPosition.DistanceTo(Player.GlobalPosition);
                if (dist <= AggroDropRadius)
                {
                    if (enemy.HasMethod("DropAggro"))
                    {
                        enemy.Call("DropAggro");
                    }
                    else if (enemy.HasMethod("SetTarget"))
                    {
                        enemy.Call("SetTarget", new Variant());
                    }
                }
            }
        }
    }

    private void SetPlayerFeigning(bool feigning)
    {
        if (Player == null) return;

        if (Player.HasMethod("SetFeigning"))
        {
            Player.Call("SetFeigning", feigning);
        }

        // Also make player invincible while feigning
        if (Player.HasMethod("SetInvincible"))
        {
            Player.Call("SetInvincible", feigning);
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!IsActive) return;

        float dt = (float)delta;
        _feignTimer += dt;

        // Check if player moved/attacked (cancel feign)
        if (PlayerActed() || _feignTimer >= MaxDuration)
        {
            Deactivate();
        }
    }

    private bool PlayerActed()
    {
        // Check for any action that should cancel feign death
        if (Input.IsActionJustPressed("attack") ||
            Input.IsActionJustPressed("secondary_attack") ||
            Input.IsActionJustPressed("jump") ||
            Input.GetVector("move_left", "move_right", "move_forward", "move_backward").Length() > 0.3f)
        {
            return true;
        }
        return false;
    }

    protected override void OnDeactivate()
    {
        SetPlayerFeigning(false);

        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.StandUp();
        }
        _effect = null;

        GD.Print("[FeignDeath3D] Getting back up");
    }
}

/// <summary>
/// Visual effect for Feign Death - player appears to fall down.
/// </summary>
public partial class FeignDeathEffect : Node3D
{
    private Node3D? _player;
    private float _originalHeight;
    private bool _standingUp;

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

        // Create "falling" particles
        CreateFallEffect();

        // Create ghost/soul effect above body
        CreateSoulEffect();
    }

    private void CreateFallEffect()
    {
        var dust = new GpuParticles3D();
        dust.GlobalPosition = _player?.GlobalPosition ?? GlobalPosition;
        dust.Amount = 15;
        dust.Lifetime = 0.5f;
        dust.Explosiveness = 1f;
        dust.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.5f;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 180f;
        mat.InitialVelocityMin = 1f;
        mat.InitialVelocityMax = 3f;
        mat.Gravity = new Vector3(0, -5, 0);
        mat.ScaleMin = 0.1f;
        mat.ScaleMax = 0.2f;
        mat.Color = new Color(0.6f, 0.5f, 0.4f, 0.6f);
        dust.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.1f;
        mesh.Height = 0.2f;
        dust.DrawPass1 = mesh;
        GetTree().Root.AddChild(dust);

        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => dust.QueueFree();
    }

    private void CreateSoulEffect()
    {
        // Ghostly aura above the "dead" player
        var ghost = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.3f;
        sphere.Height = 0.6f;
        ghost.Mesh = sphere;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.3f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.7f, 0.8f, 1f);
        mat.EmissionEnergyMultiplier = 1f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        ghost.MaterialOverride = mat;

        ghost.Position = Vector3.Up * 2f;
        AddChild(ghost);

        // Gentle bobbing animation
        var tween = ghost.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(ghost, "position:y", 2.5f, 1f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(ghost, "position:y", 2f, 1f).SetTrans(Tween.TransitionType.Sine);
    }

    public override void _Process(double delta)
    {
        // Follow player position
        if (_player != null && IsInstanceValid(_player))
        {
            GlobalPosition = _player.GlobalPosition;
        }
    }

    public void StandUp()
    {
        _standingUp = true;

        // Quick dust puff when standing
        var dust = new GpuParticles3D();
        dust.GlobalPosition = GlobalPosition;
        dust.Amount = 10;
        dust.Lifetime = 0.3f;
        dust.Explosiveness = 1f;
        dust.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.3f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 60f;
        mat.InitialVelocityMin = 2f;
        mat.InitialVelocityMax = 4f;
        mat.Gravity = new Vector3(0, -3, 0);
        mat.ScaleMin = 0.05f;
        mat.ScaleMax = 0.1f;
        mat.Color = new Color(0.6f, 0.5f, 0.4f, 0.5f);
        dust.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        dust.DrawPass1 = mesh;
        GetTree().Root.AddChild(dust);

        // Cleanup
        var timer = GetTree().CreateTimer(0.5f);
        timer.Timeout += () =>
        {
            dust.QueueFree();
            QueueFree();
        };
    }
}
