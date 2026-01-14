using Godot;
using SafeRoom3D.Core;

namespace SafeRoom3D.Player;

/// <summary>
/// Handles player camera: mouse look, FOV changes, and screen shake effects.
/// Extracted from FPSController for better separation of concerns.
/// </summary>
public class PlayerCamera
{
    private readonly CharacterBody3D _body;
    private readonly Node3D? _head;
    private readonly Camera3D? _camera;

    // Camera state
    private float _cameraPitch;  // Vertical rotation
    private float _targetFov;
    private float _currentFov;

    // Screen shake
    private float _shakeTimer;
    private float _shakeIntensity;
    private Vector2 _shakeOffset;

    // Settings
    private const float PitchLimit = 85f;  // Degrees
    private const float FovTransitionSpeed = 8f;
    private const float DefaultFov = 75f;
    private const float SprintFov = 85f;
    private const float AbilityFov = 65f;

    public float CameraPitch => _cameraPitch;
    public Vector2 ShakeOffset => _shakeOffset;

    public PlayerCamera(CharacterBody3D body, Node3D? head, Camera3D? camera)
    {
        _body = body;
        _head = head;
        _camera = camera;
        _currentFov = DefaultFov;
        _targetFov = DefaultFov;
    }

    /// <summary>
    /// Process mouse look input.
    /// </summary>
    public void ProcessMouseLook(InputEventMouseMotion motion, float sensitivity)
    {
        // Horizontal rotation (yaw) - rotate the body
        _body.RotateY(-motion.Relative.X * sensitivity);

        // Vertical rotation (pitch) - rotate the head
        _cameraPitch -= motion.Relative.Y * sensitivity;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -Mathf.DegToRad(PitchLimit), Mathf.DegToRad(PitchLimit));

        if (_head != null)
        {
            _head.Rotation = new Vector3(_cameraPitch, 0, 0);
        }
    }

    /// <summary>
    /// Update camera effects (FOV, screen shake).
    /// </summary>
    public void Process(float delta, bool isSprinting, bool isAbilityCasting)
    {
        // Update target FOV based on state
        if (isAbilityCasting)
        {
            _targetFov = AbilityFov;
        }
        else if (isSprinting)
        {
            _targetFov = SprintFov;
        }
        else
        {
            _targetFov = DefaultFov;
        }

        // Smoothly transition FOV
        UpdateFov(delta);

        // Process screen shake
        ProcessScreenShake(delta);
    }

    private void UpdateFov(float delta)
    {
        if (_camera == null) return;

        _currentFov = Mathf.MoveToward(_currentFov, _targetFov, FovTransitionSpeed * delta * 10f);
        _camera.Fov = _currentFov;
    }

    private void ProcessScreenShake(float delta)
    {
        if (_shakeTimer > 0)
        {
            _shakeTimer -= delta;

            // Generate random shake offset
            var random = new System.Random();
            _shakeOffset = new Vector2(
                (float)(random.NextDouble() * 2 - 1) * _shakeIntensity,
                (float)(random.NextDouble() * 2 - 1) * _shakeIntensity
            );

            // Apply to head rotation (additive)
            if (_head != null)
            {
                var shake = new Vector3(
                    Mathf.DegToRad(_shakeOffset.Y),
                    Mathf.DegToRad(_shakeOffset.X),
                    0
                );
                _head.Rotation = new Vector3(_cameraPitch + shake.X, shake.Y, 0);
            }

            // Fade out shake
            _shakeIntensity *= 0.9f;
        }
        else
        {
            _shakeOffset = Vector2.Zero;
            // Ensure clean rotation when not shaking
            if (_head != null)
            {
                _head.Rotation = new Vector3(_cameraPitch, 0, 0);
            }
        }
    }

    /// <summary>
    /// Request screen shake effect.
    /// </summary>
    public void RequestScreenShake(float duration, float intensity)
    {
        if (duration > _shakeTimer)
        {
            _shakeTimer = duration;
        }
        if (intensity > _shakeIntensity)
        {
            _shakeIntensity = intensity;
        }
    }

    /// <summary>
    /// Set the camera pitch directly (for resets, teleports).
    /// </summary>
    public void SetPitch(float pitch)
    {
        _cameraPitch = pitch;
        if (_head != null)
        {
            _head.Rotation = new Vector3(_cameraPitch, 0, 0);
        }
    }

    /// <summary>
    /// Set FOV directly.
    /// </summary>
    public void SetFov(float fov)
    {
        _targetFov = fov;
        _currentFov = fov;
        if (_camera != null)
        {
            _camera.Fov = fov;
        }
    }

    /// <summary>
    /// Reset camera state.
    /// </summary>
    public void Reset()
    {
        _cameraPitch = 0;
        _targetFov = DefaultFov;
        _currentFov = DefaultFov;
        _shakeTimer = 0;
        _shakeIntensity = 0;
        _shakeOffset = Vector2.Zero;

        if (_head != null)
        {
            _head.Rotation = Vector3.Zero;
        }
        if (_camera != null)
        {
            _camera.Fov = DefaultFov;
        }
    }
}
