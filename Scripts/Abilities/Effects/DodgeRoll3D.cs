using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Dodge Roll ability - Quick dash with brief invincibility frames.
/// Core movement/survival ability.
/// </summary>
public partial class DodgeRoll3D : Ability3D
{
    public override string AbilityId => "dodge_roll";
    public override string AbilityName => "Dodge Roll";
    public override string Description => "Quickly roll in your movement direction, briefly becoming invincible.";
    public override AbilityType Type => AbilityType.Instant;

    public override float DefaultCooldown => 3f;
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 7;

    // Dodge Roll stats
    public float DashDistance { get; private set; } = 6f;
    public float DashDuration { get; private set; } = 0.25f;
    public float IFrameDuration { get; private set; } = 0.3f;

    protected override void Activate()
    {
        if (Player == null) return;

        // Get movement direction or forward direction
        Vector3 dashDirection = GetDashDirection();

        // Create the roll effect
        var rollEffect = new DodgeRollEffect();
        rollEffect.Name = "DodgeRollEffect";
        rollEffect.Initialize(Player, dashDirection, DashDistance, DashDuration, IFrameDuration);
        GetTree().Root.AddChild(rollEffect);

        GD.Print($"[DodgeRoll3D] Rolling {dashDirection}");
    }

    private Vector3 GetDashDirection()
    {
        if (Player == null) return Vector3.Forward;

        // Get input direction
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");

        if (inputDir.LengthSquared() > 0.1f)
        {
            // Use input direction relative to player facing
            var forward = -Player.GlobalTransform.Basis.Z;
            var right = Player.GlobalTransform.Basis.X;
            forward.Y = 0;
            right.Y = 0;
            forward = forward.Normalized();
            right = right.Normalized();

            return (forward * -inputDir.Y + right * inputDir.X).Normalized();
        }
        else
        {
            // Default to forward direction
            var forward = -Player.GlobalTransform.Basis.Z;
            forward.Y = 0;
            return forward.Normalized();
        }
    }
}

/// <summary>
/// The dodge roll effect that moves the player and grants i-frames.
/// </summary>
public partial class DodgeRollEffect : Node3D
{
    private Node3D? _player;
    private Vector3 _direction;
    private float _distance;
    private float _duration;
    private float _iframeDuration;

    private float _timer;
    private float _iframeTimer;
    private Vector3 _startPos;
    private bool _isDashing;
    private GpuParticles3D? _trailParticles;

    public void Initialize(Node3D player, Vector3 direction, float distance, float duration, float iframeDuration)
    {
        _player = player;
        _direction = direction;
        _distance = distance;
        _duration = duration;
        _iframeDuration = iframeDuration;
    }

    public override void _Ready()
    {
        if (_player == null)
        {
            QueueFree();
            return;
        }

        _startPos = _player.GlobalPosition;
        _isDashing = true;
        _iframeTimer = _iframeDuration;

        // Enable invincibility
        SetPlayerInvincible(true);

        // Create visual effects
        CreateTrailEffect();
        CreateDashFlash();
    }

    private void CreateTrailEffect()
    {
        _trailParticles = new GpuParticles3D();
        _trailParticles.Amount = 20;
        _trailParticles.Lifetime = 0.3f;
        _trailParticles.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 45f;
        mat.InitialVelocityMin = 0.5f;
        mat.InitialVelocityMax = 1f;
        mat.Gravity = Vector3.Zero;
        mat.ScaleMin = 0.1f;
        mat.ScaleMax = 0.2f;
        mat.Color = new Color(0.7f, 0.8f, 1f, 0.6f);
        _trailParticles.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        _trailParticles.DrawPass1 = mesh;

        AddChild(_trailParticles);
    }

    private void CreateDashFlash()
    {
        // Brief flash at start position
        var flash = new OmniLight3D();
        flash.GlobalPosition = _startPos + Vector3.Up;
        flash.LightColor = new Color(0.6f, 0.8f, 1f);
        flash.LightEnergy = 3f;
        flash.OmniRange = 4f;
        GetTree().Root.AddChild(flash);

        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "light_energy", 0f, 0.2f);
        tween.TweenCallback(Callable.From(() => flash.QueueFree()));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Handle dash movement
        if (_isDashing && _player != null)
        {
            _timer += dt;
            float progress = _timer / _duration;

            if (progress >= 1f)
            {
                _isDashing = false;
                progress = 1f;
            }

            // Ease out movement
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
            Vector3 targetPos = _startPos + _direction * _distance * easedProgress;

            // Only modify X and Z, keep Y the same
            var newPos = _player.GlobalPosition;
            newPos.X = targetPos.X;
            newPos.Z = targetPos.Z;
            _player.GlobalPosition = newPos;

            // Update trail position
            if (_trailParticles != null)
            {
                _trailParticles.GlobalPosition = _player.GlobalPosition + Vector3.Up;
            }
        }

        // Handle i-frames
        _iframeTimer -= dt;
        if (_iframeTimer <= 0)
        {
            SetPlayerInvincible(false);

            // Stop trail
            if (_trailParticles != null)
            {
                _trailParticles.Emitting = false;
            }

            // Cleanup after particles fade
            var timer = GetTree().CreateTimer(0.5f);
            timer.Timeout += () => QueueFree();
            SetProcess(false);
        }
    }

    private void SetPlayerInvincible(bool invincible)
    {
        if (_player != null && _player.HasMethod("SetInvincible"))
        {
            _player.Call("SetInvincible", invincible);
        }
    }
}
