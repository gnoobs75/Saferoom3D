using Godot;
using SafeRoom3D.Core;

namespace SafeRoom3D.Player;

/// <summary>
/// Handles player movement: WASD, jumping, sprinting, and head bobbing.
/// Extracted from FPSController for better separation of concerns.
/// </summary>
public class PlayerMovement
{
    private readonly CharacterBody3D _body;
    private readonly Node3D? _head;

    // Movement state
    private Vector3 _velocity;
    private bool _isSprinting;
    private float _headBobTime;

    // Head bob settings
    private const float HeadBobFrequency = 12f;
    private const float HeadBobAmplitude = 0.03f;
    private const float HeadBobSprintMultiplier = 1.5f;
    private float _originalHeadY;

    public bool IsSprinting => _isSprinting;
    public Vector3 Velocity => _velocity;

    public PlayerMovement(CharacterBody3D body, Node3D? head)
    {
        _body = body;
        _head = head;
        _originalHeadY = head?.Position.Y ?? 0.8f;
    }

    /// <summary>
    /// Process movement input and physics.
    /// </summary>
    public void Process(float delta, bool canMove)
    {
        if (!canMove)
        {
            // Still apply gravity when not moving
            ApplyGravity(delta);
            return;
        }

        // Get input direction
        var inputDir = GetInputDirection();

        // Update sprint state
        _isSprinting = Input.IsActionPressed("sprint") && inputDir.Y < 0; // Only sprint forward

        // Calculate movement
        var speed = _isSprinting ? Constants.PlayerSprintSpeed : Constants.PlayerMoveSpeed;
        var targetVelocity = CalculateTargetVelocity(inputDir, speed);

        // Apply movement with acceleration/friction
        ApplyMovement(targetVelocity, delta);

        // Apply gravity
        ApplyGravity(delta);

        // Handle jumping
        if (Input.IsActionJustPressed("jump") && _body.IsOnFloor())
        {
            _velocity.Y = Constants.PlayerJumpVelocity;
        }

        // Move the body
        _body.Velocity = _velocity;
        _body.MoveAndSlide();
        _velocity = _body.Velocity;

        // Update head bob
        UpdateHeadBob(delta, inputDir);
    }

    private Vector2 GetInputDirection()
    {
        return Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
    }

    private Vector3 CalculateTargetVelocity(Vector2 inputDir, float speed)
    {
        // Transform input to world space based on body rotation
        var forward = -_body.Transform.Basis.Z;
        var right = _body.Transform.Basis.X;

        forward.Y = 0;
        right.Y = 0;
        forward = forward.Normalized();
        right = right.Normalized();

        var direction = (forward * -inputDir.Y + right * inputDir.X).Normalized();

        return direction * speed;
    }

    private void ApplyMovement(Vector3 targetVelocity, float delta)
    {
        var horizontalVelocity = new Vector3(_velocity.X, 0, _velocity.Z);

        if (targetVelocity.LengthSquared() > 0.01f)
        {
            // Accelerate toward target
            horizontalVelocity = horizontalVelocity.MoveToward(targetVelocity, Constants.PlayerAcceleration * delta);
        }
        else
        {
            // Decelerate when no input
            horizontalVelocity = horizontalVelocity.MoveToward(Vector3.Zero, Constants.PlayerFriction * delta);
        }

        _velocity.X = horizontalVelocity.X;
        _velocity.Z = horizontalVelocity.Z;
    }

    private void ApplyGravity(float delta)
    {
        if (!_body.IsOnFloor())
        {
            _velocity.Y -= Constants.Gravity * delta;
        }
    }

    private void UpdateHeadBob(float delta, Vector2 inputDir)
    {
        if (_head == null) return;

        var isMoving = inputDir.LengthSquared() > 0.1f && _body.IsOnFloor();

        if (isMoving)
        {
            var freq = HeadBobFrequency * (_isSprinting ? HeadBobSprintMultiplier : 1f);
            _headBobTime += delta * freq;

            var bobOffset = Mathf.Sin(_headBobTime) * HeadBobAmplitude;
            _head.Position = new Vector3(_head.Position.X, _originalHeadY + bobOffset, _head.Position.Z);
        }
        else
        {
            // Smoothly return to original position
            var pos = _head.Position;
            pos.Y = Mathf.MoveToward(pos.Y, _originalHeadY, delta * 2f);
            _head.Position = pos;
            _headBobTime = 0;
        }
    }

    /// <summary>
    /// Set the velocity directly (for teleports, knockback, etc.)
    /// </summary>
    public void SetVelocity(Vector3 velocity)
    {
        _velocity = velocity;
    }

    /// <summary>
    /// Apply an impulse to the velocity.
    /// </summary>
    public void ApplyImpulse(Vector3 impulse)
    {
        _velocity += impulse;
    }

    /// <summary>
    /// Reset movement state.
    /// </summary>
    public void Reset()
    {
        _velocity = Vector3.Zero;
        _isSprinting = false;
        _headBobTime = 0;
    }
}
