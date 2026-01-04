using Godot;
using System;

namespace SafeRoom3D.Broadcaster;

/// <summary>
/// Avatar expression states
/// </summary>
public enum AvatarExpression
{
    Idle,
    Bored,
    Smug,
    FakeExcited,
    Excited,
    Impressed,
    SlightlyImpressed,
    Disappointed,
    FakeWorried,
    Surprised,
    Angry,
    Talking
}

/// <summary>
/// Procedurally generated holographic AI avatar for the broadcaster.
/// Features animated eyes, eyebrows, and mouth with multiple expressions.
/// Styled as a retro-futuristic display with scan lines and glitch effects.
/// </summary>
public partial class BroadcasterAvatar : Node3D
{
    // Face components
    private Node3D? _faceRoot;
    private MeshInstance3D? _leftEye;
    private MeshInstance3D? _rightEye;
    private MeshInstance3D? _leftPupil;
    private MeshInstance3D? _rightPupil;
    private MeshInstance3D? _leftEyebrow;
    private MeshInstance3D? _rightEyebrow;
    private MeshInstance3D? _mouth;
    private MeshInstance3D? _faceScreen;
    private MeshInstance3D? _scanLines;

    // Camera for the viewport
    private Camera3D? _camera;

    // Materials
    private StandardMaterial3D? _eyeMaterial;
    private StandardMaterial3D? _pupilMaterial;
    private StandardMaterial3D? _eyebrowMaterial;
    private StandardMaterial3D? _mouthMaterial;
    private StandardMaterial3D? _screenMaterial;
    private StandardMaterial3D? _scanLineMaterial;

    // Animation state
    private AvatarExpression _currentExpression = AvatarExpression.Idle;
    private bool _isTalking = false;
    private float _talkTimer = 0f;
    private float _blinkTimer = 0f;
    private float _nextBlinkTime = 3f;
    private bool _isBlinking = false;
    private float _idleTimer = 0f;
    private float _glitchTimer = 0f;
    private float _nextGlitchTime = 5f;

    // Expression targets
    private float _targetLeftEyebrowAngle = 0f;
    private float _targetRightEyebrowAngle = 0f;
    private float _targetMouthOpenness = 0f;
    private float _targetMouthWidth = 1f;
    private float _targetMouthCurve = 0f;  // Positive = smile, negative = frown
    private Vector3 _targetPupilOffset = Vector3.Zero;
    private float _targetEyeSquint = 0f;

    // Current values (for lerping)
    private float _currentLeftEyebrowAngle = 0f;
    private float _currentRightEyebrowAngle = 0f;
    private float _currentMouthOpenness = 0f;
    private float _currentMouthWidth = 1f;
    private float _currentMouthCurve = 0f;
    private Vector3 _currentPupilOffset = Vector3.Zero;
    private float _currentEyeSquint = 0f;

    // Configuration
    private const float LerpSpeed = 8f;
    private const float TalkMouthSpeed = 15f;
    private const float BlinkDuration = 0.15f;
    private const float EyeRadius = 0.15f;
    private const float PupilRadius = 0.06f;
    private const float FaceWidth = 1.0f;
    private const float EyeSpacing = 0.25f;
    private const float EyeHeight = 0.1f;
    private const float MouthHeight = -0.2f;

    // Colors
    private static readonly Color EyeColor = new(0.7f, 0.9f, 1.0f);
    private static readonly Color PupilColor = new(0.1f, 0.3f, 0.5f);
    private static readonly Color HoloColor = new(0.3f, 0.7f, 1.0f, 0.9f);
    private static readonly Color EmissionColor = new(0.4f, 0.8f, 1.0f);

    public override void _Ready()
    {
        BuildAvatar();
        SetExpression(AvatarExpression.Idle);

        // Randomize initial timers
        _nextBlinkTime = GD.Randf() * 2f + 2f;
        _nextGlitchTime = GD.Randf() * 3f + 4f;
    }

    private void BuildAvatar()
    {
        // Create camera for viewport rendering
        _camera = new Camera3D
        {
            Name = "AvatarCamera",
            Position = new Vector3(0, 0, 2f),
            Fov = 30,
        };
        AddChild(_camera);

        // Create root for the face
        _faceRoot = new Node3D { Name = "FaceRoot" };
        AddChild(_faceRoot);

        // Create materials
        CreateMaterials();

        // Build face components
        CreateFaceScreen();
        CreateEyes();
        CreateEyebrows();
        CreateMouth();
        CreateScanLines();

        // Add ambient light
        var light = new DirectionalLight3D
        {
            Position = new Vector3(0.5f, 0.5f, 1f),
            LightColor = new Color(0.8f, 0.9f, 1.0f),
            LightEnergy = 0.8f,
        };
        AddChild(light);
    }

    private void CreateMaterials()
    {
        // Eye material (glowing white)
        _eyeMaterial = new StandardMaterial3D
        {
            AlbedoColor = EyeColor,
            EmissionEnabled = true,
            Emission = EmissionColor,
            EmissionEnergyMultiplier = 1.5f,
        };

        // Pupil material
        _pupilMaterial = new StandardMaterial3D
        {
            AlbedoColor = PupilColor,
        };

        // Eyebrow material
        _eyebrowMaterial = new StandardMaterial3D
        {
            AlbedoColor = HoloColor,
            EmissionEnabled = true,
            Emission = EmissionColor,
            EmissionEnergyMultiplier = 1.0f,
        };

        // Mouth material
        _mouthMaterial = new StandardMaterial3D
        {
            AlbedoColor = HoloColor,
            EmissionEnabled = true,
            Emission = EmissionColor,
            EmissionEnergyMultiplier = 1.0f,
        };

        // Screen material (hologram background)
        _screenMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.1f, 0.15f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        // Scan line material
        _scanLineMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.4f, 0.6f, 0.3f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
    }

    private void CreateFaceScreen()
    {
        // Holographic screen background
        var screenMesh = new BoxMesh
        {
            Size = new Vector3(FaceWidth * 1.2f, 0.8f, 0.05f),
        };

        _faceScreen = new MeshInstance3D
        {
            Name = "FaceScreen",
            Mesh = screenMesh,
            MaterialOverride = _screenMaterial,
            Position = new Vector3(0, 0, -0.1f),
        };
        _faceRoot?.AddChild(_faceScreen);
    }

    private void CreateEyes()
    {
        // Left eye (white part)
        var eyeMesh = new SphereMesh
        {
            Radius = EyeRadius,
            Height = EyeRadius * 2f,
            RadialSegments = 24,
            Rings = 12,
        };

        _leftEye = new MeshInstance3D
        {
            Name = "LeftEye",
            Mesh = eyeMesh,
            MaterialOverride = _eyeMaterial,
            Position = new Vector3(-EyeSpacing, EyeHeight, 0),
        };
        _faceRoot?.AddChild(_leftEye);

        _rightEye = new MeshInstance3D
        {
            Name = "RightEye",
            Mesh = eyeMesh,
            MaterialOverride = _eyeMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight, 0),
        };
        _faceRoot?.AddChild(_rightEye);

        // Pupils
        var pupilMesh = new SphereMesh
        {
            Radius = PupilRadius,
            Height = PupilRadius * 2f,
            RadialSegments = 16,
            Rings = 8,
        };

        _leftPupil = new MeshInstance3D
        {
            Name = "LeftPupil",
            Mesh = pupilMesh,
            MaterialOverride = _pupilMaterial,
            Position = new Vector3(-EyeSpacing, EyeHeight, EyeRadius * 0.7f),
        };
        _faceRoot?.AddChild(_leftPupil);

        _rightPupil = new MeshInstance3D
        {
            Name = "RightPupil",
            Mesh = pupilMesh,
            MaterialOverride = _pupilMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight, EyeRadius * 0.7f),
        };
        _faceRoot?.AddChild(_rightPupil);
    }

    private void CreateEyebrows()
    {
        // Eyebrows as thin boxes that can rotate for expression
        var browMesh = new BoxMesh
        {
            Size = new Vector3(0.18f, 0.03f, 0.02f),
        };

        _leftEyebrow = new MeshInstance3D
        {
            Name = "LeftEyebrow",
            Mesh = browMesh,
            MaterialOverride = _eyebrowMaterial,
            Position = new Vector3(-EyeSpacing, EyeHeight + EyeRadius + 0.05f, 0.05f),
        };
        _faceRoot?.AddChild(_leftEyebrow);

        _rightEyebrow = new MeshInstance3D
        {
            Name = "RightEyebrow",
            Mesh = browMesh,
            MaterialOverride = _eyebrowMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight + EyeRadius + 0.05f, 0.05f),
        };
        _faceRoot?.AddChild(_rightEyebrow);
    }

    private void CreateMouth()
    {
        // Mouth as a stretched box that can be scaled for expressions
        var mouthMesh = new BoxMesh
        {
            Size = new Vector3(0.25f, 0.04f, 0.02f),
        };

        _mouth = new MeshInstance3D
        {
            Name = "Mouth",
            Mesh = mouthMesh,
            MaterialOverride = _mouthMaterial,
            Position = new Vector3(0, MouthHeight, 0.05f),
        };
        _faceRoot?.AddChild(_mouth);
    }

    private void CreateScanLines()
    {
        // Horizontal scan lines for retro effect
        var scanNode = new Node3D { Name = "ScanLines" };
        _faceRoot?.AddChild(scanNode);

        var lineMesh = new BoxMesh
        {
            Size = new Vector3(FaceWidth * 1.3f, 0.005f, 0.001f),
        };

        for (int i = -8; i <= 8; i += 2)
        {
            var line = new MeshInstance3D
            {
                Mesh = lineMesh,
                MaterialOverride = _scanLineMaterial,
                Position = new Vector3(0, i * 0.05f, 0.15f),
            };
            scanNode.AddChild(line);
        }

        _scanLines = scanNode.GetChild<MeshInstance3D>(0);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        UpdateTimers(dt);
        UpdateExpressionAnimation(dt);
        UpdateTalkingAnimation(dt);
        UpdateIdleAnimation(dt);
        ApplyAnimationValues();
    }

    private void UpdateTimers(float dt)
    {
        _blinkTimer += dt;
        _idleTimer += dt;
        _glitchTimer += dt;

        // Blink check
        if (!_isBlinking && _blinkTimer >= _nextBlinkTime)
        {
            StartBlink();
        }

        // Glitch check
        if (_glitchTimer >= _nextGlitchTime)
        {
            TriggerGlitch();
            _glitchTimer = 0;
            _nextGlitchTime = GD.Randf() * 4f + 3f;
        }
    }

    private void StartBlink()
    {
        _isBlinking = true;
        _blinkTimer = 0;
        _targetEyeSquint = 1f;

        // Schedule blink end
        GetTree().CreateTimer(BlinkDuration).Timeout += EndBlink;
    }

    private void EndBlink()
    {
        _isBlinking = false;
        _targetEyeSquint = GetSquintForExpression(_currentExpression);
        _nextBlinkTime = GD.Randf() * 3f + 2f;
    }

    private void TriggerGlitch()
    {
        // Brief position/color glitch
        if (_faceRoot == null) return;

        Vector3 originalPos = _faceRoot.Position;
        _faceRoot.Position = originalPos + new Vector3(GD.Randf() * 0.05f - 0.025f, 0, 0);

        GetTree().CreateTimer(0.05f).Timeout += () =>
        {
            if (_faceRoot != null)
                _faceRoot.Position = originalPos;
        };

        // Color flash
        if (_eyeMaterial != null)
        {
            var originalEmission = _eyeMaterial.EmissionEnergyMultiplier;
            _eyeMaterial.EmissionEnergyMultiplier = 3f;

            GetTree().CreateTimer(0.08f).Timeout += () =>
            {
                if (_eyeMaterial != null)
                    _eyeMaterial.EmissionEnergyMultiplier = originalEmission;
            };
        }
    }

    private void UpdateExpressionAnimation(float dt)
    {
        // Lerp current values towards targets
        _currentLeftEyebrowAngle = Mathf.Lerp(_currentLeftEyebrowAngle, _targetLeftEyebrowAngle, LerpSpeed * dt);
        _currentRightEyebrowAngle = Mathf.Lerp(_currentRightEyebrowAngle, _targetRightEyebrowAngle, LerpSpeed * dt);
        _currentMouthWidth = Mathf.Lerp(_currentMouthWidth, _targetMouthWidth, LerpSpeed * dt);
        _currentMouthCurve = Mathf.Lerp(_currentMouthCurve, _targetMouthCurve, LerpSpeed * dt);
        _currentPupilOffset = _currentPupilOffset.Lerp(_targetPupilOffset, LerpSpeed * dt);

        if (!_isBlinking)
            _currentEyeSquint = Mathf.Lerp(_currentEyeSquint, _targetEyeSquint, LerpSpeed * dt);
        else
            _currentEyeSquint = Mathf.Lerp(_currentEyeSquint, 1f, LerpSpeed * 3f * dt);
    }

    private void UpdateTalkingAnimation(float dt)
    {
        if (_isTalking)
        {
            _talkTimer += dt * TalkMouthSpeed;
            // Oscillate mouth openness while talking
            _currentMouthOpenness = Mathf.Abs(Mathf.Sin(_talkTimer)) * 0.8f + 0.1f;
        }
        else
        {
            _currentMouthOpenness = Mathf.Lerp(_currentMouthOpenness, _targetMouthOpenness, LerpSpeed * dt);
        }
    }

    private void UpdateIdleAnimation(float dt)
    {
        // Subtle idle movements
        if (_currentExpression == AvatarExpression.Idle || _currentExpression == AvatarExpression.Bored)
        {
            // Slow pupil drift
            float driftX = Mathf.Sin(_idleTimer * 0.5f) * 0.02f;
            float driftY = Mathf.Cos(_idleTimer * 0.3f) * 0.01f;
            _targetPupilOffset = new Vector3(driftX, driftY, 0);
        }
    }

    private void ApplyAnimationValues()
    {
        // Apply eyebrow rotations
        if (_leftEyebrow != null)
            _leftEyebrow.Rotation = new Vector3(0, 0, Mathf.DegToRad(_currentLeftEyebrowAngle));
        if (_rightEyebrow != null)
            _rightEyebrow.Rotation = new Vector3(0, 0, Mathf.DegToRad(-_currentRightEyebrowAngle));

        // Apply mouth scale (width and height for openness)
        if (_mouth != null)
        {
            float openScale = 1f + _currentMouthOpenness * 2f;
            float widthScale = _currentMouthWidth;
            _mouth.Scale = new Vector3(widthScale, openScale, 1);

            // Curve effect via position offset
            _mouth.Position = new Vector3(0, MouthHeight + _currentMouthCurve * 0.03f, 0.05f);
        }

        // Apply pupil positions
        if (_leftPupil != null)
            _leftPupil.Position = new Vector3(-EyeSpacing, EyeHeight, EyeRadius * 0.7f) + _currentPupilOffset;
        if (_rightPupil != null)
            _rightPupil.Position = new Vector3(EyeSpacing, EyeHeight, EyeRadius * 0.7f) + _currentPupilOffset;

        // Apply eye squint (scale Y)
        float eyeScaleY = 1f - _currentEyeSquint * 0.8f;
        if (_leftEye != null)
            _leftEye.Scale = new Vector3(1, eyeScaleY, 1);
        if (_rightEye != null)
            _rightEye.Scale = new Vector3(1, eyeScaleY, 1);
    }

    /// <summary>
    /// Set the avatar's expression
    /// </summary>
    public void SetExpression(AvatarExpression expression)
    {
        _currentExpression = expression;

        switch (expression)
        {
            case AvatarExpression.Idle:
                SetExpressionValues(0, 0, 0, 1, 0, Vector3.Zero, 0);
                break;

            case AvatarExpression.Bored:
                SetExpressionValues(10, 10, 0, 0.8f, -0.3f, new Vector3(0.03f, -0.02f, 0), 0.3f);
                break;

            case AvatarExpression.Smug:
                SetExpressionValues(-15, 20, 0, 0.9f, 0.5f, new Vector3(0.02f, 0.01f, 0), 0.2f);
                break;

            case AvatarExpression.FakeExcited:
                SetExpressionValues(-20, -20, 0.3f, 1.2f, 0.8f, new Vector3(0, 0.02f, 0), 0);
                break;

            case AvatarExpression.Excited:
                SetExpressionValues(-25, -25, 0.4f, 1.3f, 1.0f, new Vector3(0, 0.02f, 0), 0);
                break;

            case AvatarExpression.Impressed:
                SetExpressionValues(-20, -20, 0.2f, 1.0f, 0.6f, new Vector3(0, 0.02f, 0), 0);
                break;

            case AvatarExpression.SlightlyImpressed:
                SetExpressionValues(-10, -10, 0, 1.0f, 0.3f, Vector3.Zero, 0.1f);
                break;

            case AvatarExpression.Disappointed:
                SetExpressionValues(15, 15, 0, 0.7f, -0.8f, new Vector3(0, -0.02f, 0), 0.2f);
                break;

            case AvatarExpression.FakeWorried:
                SetExpressionValues(-30, 10, 0.1f, 0.9f, -0.4f, new Vector3(-0.02f, 0.01f, 0), 0);
                break;

            case AvatarExpression.Surprised:
                SetExpressionValues(-30, -30, 0.5f, 1.4f, 0, new Vector3(0, 0.02f, 0), 0);
                break;

            case AvatarExpression.Angry:
                SetExpressionValues(30, 30, 0.1f, 0.6f, -0.6f, new Vector3(0, 0, 0), 0.4f);
                break;

            case AvatarExpression.Talking:
                // Talking is handled separately
                break;
        }
    }

    private void SetExpressionValues(
        float leftBrow, float rightBrow, float mouthOpen,
        float mouthWidth, float mouthCurve, Vector3 pupilOffset, float eyeSquint)
    {
        _targetLeftEyebrowAngle = leftBrow;
        _targetRightEyebrowAngle = rightBrow;
        _targetMouthOpenness = mouthOpen;
        _targetMouthWidth = mouthWidth;
        _targetMouthCurve = mouthCurve;
        _targetPupilOffset = pupilOffset;
        _targetEyeSquint = eyeSquint;
    }

    private float GetSquintForExpression(AvatarExpression expression)
    {
        return expression switch
        {
            AvatarExpression.Bored => 0.3f,
            AvatarExpression.Smug => 0.2f,
            AvatarExpression.Angry => 0.4f,
            AvatarExpression.Disappointed => 0.2f,
            _ => 0f
        };
    }

    /// <summary>
    /// Start talking animation
    /// </summary>
    public void StartTalking()
    {
        _isTalking = true;
        _talkTimer = 0;
    }

    /// <summary>
    /// Stop talking animation
    /// </summary>
    public void StopTalking()
    {
        _isTalking = false;
    }

    /// <summary>
    /// Look at a specific direction (for tracking player action)
    /// </summary>
    public void LookAt(Vector2 direction)
    {
        _targetPupilOffset = new Vector3(direction.X * 0.04f, direction.Y * 0.03f, 0);
    }

    /// <summary>
    /// Trigger an eye roll (for sarcasm)
    /// </summary>
    public void DoEyeRoll()
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float angle = t * Mathf.Tau;
            _currentPupilOffset = new Vector3(Mathf.Sin(angle) * 0.04f, Mathf.Cos(angle) * 0.03f, 0);
        }), 0f, 1f, 0.8f);
    }
}
