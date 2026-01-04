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
    Talking,
    EyeRoll,       // Sarcastic eye roll
    Skeptical,     // One eyebrow raised
    Thinking       // Looking up thoughtfully
}

/// <summary>
/// Procedurally generated holographic AI avatar for the broadcaster.
/// Features animated eyes, eyebrows, and mouth with multiple expressions.
/// Styled as a retro-futuristic display with scan lines and glitch effects.
/// Enhanced with head movement, eyelids, hair/antenna, and emotion-driven colors.
/// </summary>
public partial class BroadcasterAvatar : Node3D
{
    // Face components
    private Node3D? _faceRoot;
    private Node3D? _headPivot;  // For head tilting/nodding
    private MeshInstance3D? _leftEye;
    private MeshInstance3D? _rightEye;
    private MeshInstance3D? _leftPupil;
    private MeshInstance3D? _rightPupil;
    private MeshInstance3D? _leftIris;
    private MeshInstance3D? _rightIris;
    private MeshInstance3D? _leftEyelid;
    private MeshInstance3D? _rightEyelid;
    private MeshInstance3D? _leftEyebrow;
    private MeshInstance3D? _rightEyebrow;
    private MeshInstance3D? _mouth;
    private MeshInstance3D? _upperLip;
    private MeshInstance3D? _lowerLip;
    private MeshInstance3D? _teeth;
    private MeshInstance3D? _faceScreen;
    private MeshInstance3D? _scanLines;
    private MeshInstance3D? _nose;
    private MeshInstance3D? _leftCheek;
    private MeshInstance3D? _rightCheek;
    private MeshInstance3D? _chin;
    private MeshInstance3D? _faceOutline;
    private MeshInstance3D? _holoGlow;
    private Node3D? _hairAntenna;
    private Node3D? _staticOverlay;
    private Node3D? _particleContainer;

    // Camera for the viewport
    private Camera3D? _camera;

    // Materials
    private StandardMaterial3D? _eyeMaterial;
    private StandardMaterial3D? _pupilMaterial;
    private StandardMaterial3D? _irisMaterial;
    private StandardMaterial3D? _eyelidMaterial;
    private StandardMaterial3D? _eyebrowMaterial;
    private StandardMaterial3D? _mouthMaterial;
    private StandardMaterial3D? _lipMaterial;
    private StandardMaterial3D? _teethMaterial;
    private StandardMaterial3D? _screenMaterial;
    private StandardMaterial3D? _scanLineMaterial;
    private StandardMaterial3D? _faceDetailMaterial;
    private StandardMaterial3D? _staticMaterial;
    private StandardMaterial3D? _glowMaterial;
    private StandardMaterial3D? _hairMaterial;
    private StandardMaterial3D? _particleMaterial;

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
    private bool _isRollingEyes = false;
    private float _eyeRollProgress = 0f;
    private float _staticTimer = 0f;
    private float _staticIntensity = 0f;
    private float _glowPulseTimer = 0f;
    private float _particleTimer = 0f;

    // Head movement state
    private float _targetHeadTilt = 0f;
    private float _targetHeadNod = 0f;
    private float _targetHeadTurn = 0f;
    private float _currentHeadTilt = 0f;
    private float _currentHeadNod = 0f;
    private float _currentHeadTurn = 0f;
    private bool _isNodding = false;
    private bool _isShaking = false;
    private float _nodProgress = 0f;
    private float _shakeProgress = 0f;

    // Expression targets
    private float _targetLeftEyebrowAngle = 0f;
    private float _targetRightEyebrowAngle = 0f;
    private float _targetLeftEyebrowHeight = 0f;
    private float _targetRightEyebrowHeight = 0f;
    private float _targetMouthOpenness = 0f;
    private float _targetMouthWidth = 1f;
    private float _targetMouthCurve = 0f;  // Positive = smile, negative = frown
    private Vector3 _targetPupilOffset = Vector3.Zero;
    private float _targetEyeSquint = 0f;
    private float _targetPupilDilation = 1f;  // 1.0 = normal, >1 = dilated, <1 = constricted
    private Color _targetEmotionColor = new(0.4f, 0.8f, 1.0f);

    // Current values (for lerping)
    private float _currentLeftEyebrowAngle = 0f;
    private float _currentRightEyebrowAngle = 0f;
    private float _currentLeftEyebrowHeight = 0f;
    private float _currentRightEyebrowHeight = 0f;
    private float _currentMouthOpenness = 0f;
    private float _currentMouthWidth = 1f;
    private float _currentMouthCurve = 0f;
    private Vector3 _currentPupilOffset = Vector3.Zero;
    private float _currentEyeSquint = 0f;
    private float _currentPupilDilation = 1f;
    private Color _currentEmotionColor = new(0.4f, 0.8f, 1.0f);

    // Configuration
    private const float LerpSpeed = 8f;
    private const float ColorLerpSpeed = 3f;
    private const float HeadLerpSpeed = 5f;
    private const float TalkMouthSpeed = 15f;
    private const float BlinkDuration = 0.15f;
    private const float EyeRadius = 0.15f;
    private const float PupilRadius = 0.06f;
    private const float IrisRadius = 0.09f;
    private const float FaceWidth = 1.0f;
    private const float EyeSpacing = 0.25f;
    private const float EyeHeight = 0.1f;
    private const float MouthHeight = -0.2f;

    // Colors - emotion-based
    private static readonly Color EyeColor = new(0.7f, 0.9f, 1.0f);
    private static readonly Color PupilColor = new(0.05f, 0.15f, 0.3f);
    private static readonly Color IrisColor = new(0.2f, 0.5f, 0.8f);
    private static readonly Color HoloColor = new(0.3f, 0.7f, 1.0f, 0.9f);
    private static readonly Color EmissionColor = new(0.4f, 0.8f, 1.0f);
    private static readonly Color AngryColor = new(1.0f, 0.3f, 0.2f);
    private static readonly Color HappyColor = new(0.3f, 1.0f, 0.5f);
    private static readonly Color SurprisedColor = new(1.0f, 0.9f, 0.3f);
    private static readonly Color BoredColor = new(0.5f, 0.5f, 0.6f);
    private static readonly Color SmugColor = new(0.8f, 0.5f, 1.0f);

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

        // Create head pivot for tilting/nodding
        _headPivot = new Node3D { Name = "HeadPivot" };
        _faceRoot.AddChild(_headPivot);

        // Create materials
        CreateMaterials();

        // Build face components (attach to head pivot)
        CreateHoloGlow();
        CreateFaceScreen();
        CreateFaceOutline();
        CreateHairAntenna();
        CreateEyes();
        CreateEyelids();
        CreateEyebrows();
        CreateNose();
        CreateCheeks();
        CreateMouth();
        CreateChin();
        CreateScanLines();
        CreateStaticOverlay();
        CreateParticleEffects();

        // Add ambient light
        var light = new DirectionalLight3D
        {
            Position = new Vector3(0.5f, 0.5f, 1f),
            LightColor = new Color(0.8f, 0.9f, 1.0f),
            LightEnergy = 0.8f,
        };
        AddChild(light);

        // Add subtle rim light for depth
        var rimLight = new DirectionalLight3D
        {
            Position = new Vector3(-0.5f, 0.2f, -0.5f),
            LightColor = new Color(0.4f, 0.6f, 1.0f),
            LightEnergy = 0.4f,
        };
        AddChild(rimLight);
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

        // Pupil material (dark center)
        _pupilMaterial = new StandardMaterial3D
        {
            AlbedoColor = PupilColor,
        };

        // Iris material (colored ring around pupil)
        _irisMaterial = new StandardMaterial3D
        {
            AlbedoColor = IrisColor,
            EmissionEnabled = true,
            Emission = IrisColor,
            EmissionEnergyMultiplier = 0.8f,
        };

        // Eyelid material
        _eyelidMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.25f, 0.4f, 0.95f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        // Eyebrow material
        _eyebrowMaterial = new StandardMaterial3D
        {
            AlbedoColor = HoloColor,
            EmissionEnabled = true,
            Emission = EmissionColor,
            EmissionEnergyMultiplier = 1.0f,
        };

        // Mouth interior material
        _mouthMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.1f, 0.2f),
        };

        // Lip material
        _lipMaterial = new StandardMaterial3D
        {
            AlbedoColor = HoloColor,
            EmissionEnabled = true,
            Emission = EmissionColor,
            EmissionEnergyMultiplier = 1.0f,
        };

        // Teeth material
        _teethMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.95f, 1.0f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.9f, 1.0f),
            EmissionEnergyMultiplier = 0.5f,
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

        // Face detail material (subtle highlights for cheeks, nose, chin)
        _faceDetailMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.5f, 0.7f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.6f, 0.8f),
            EmissionEnergyMultiplier = 0.5f,
        };

        // Static overlay material
        _staticMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f, 0.1f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        // Holographic glow material
        _glowMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.6f, 1.0f, 0.2f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = EmissionColor,
            EmissionEnergyMultiplier = 0.3f,
        };

        // Hair/antenna material
        _hairMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.4f, 0.8f),
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.6f, 1.0f),
            EmissionEnergyMultiplier = 1.2f,
        };

        // Particle effect material
        _particleMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.8f, 1.0f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = EmissionColor,
            EmissionEnergyMultiplier = 2.0f,
        };
    }

    private void CreateHoloGlow()
    {
        // Outer holographic glow effect
        var glowMesh = new SphereMesh
        {
            Radius = 0.5f,
            Height = 1.0f,
            RadialSegments = 32,
            Rings = 16,
        };

        _holoGlow = new MeshInstance3D
        {
            Name = "HoloGlow",
            Mesh = glowMesh,
            MaterialOverride = _glowMaterial,
            Position = new Vector3(0, 0, -0.05f),
            Scale = new Vector3(1.3f, 1.1f, 0.5f),
        };
        _headPivot?.AddChild(_holoGlow);
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
        _headPivot?.AddChild(_faceScreen);
    }

    private void CreateFaceOutline()
    {
        // Subtle face oval outline for holographic effect
        var outlineMesh = new TorusMesh
        {
            InnerRadius = 0.28f,
            OuterRadius = 0.32f,
            Rings = 32,
            RingSegments = 32,
        };

        _faceOutline = new MeshInstance3D
        {
            Name = "FaceOutline",
            Mesh = outlineMesh,
            MaterialOverride = _faceDetailMaterial,
            Position = new Vector3(0, 0, -0.02f),
            Rotation = new Vector3(Mathf.Pi / 2f, 0, 0),
            Scale = new Vector3(1.5f, 1f, 2f), // Oval face shape
        };
        _headPivot?.AddChild(_faceOutline);
    }

    private void CreateHairAntenna()
    {
        // Stylized holographic "hair" - antenna-like protrusions
        _hairAntenna = new Node3D { Name = "HairAntenna" };
        _headPivot?.AddChild(_hairAntenna);

        // Central antenna
        var antennaMesh = new CylinderMesh
        {
            TopRadius = 0.01f,
            BottomRadius = 0.02f,
            Height = 0.15f,
            RadialSegments = 8,
        };

        // Main antenna
        var mainAntenna = new MeshInstance3D
        {
            Name = "MainAntenna",
            Mesh = antennaMesh,
            MaterialOverride = _hairMaterial,
            Position = new Vector3(0, 0.35f, 0),
            Rotation = new Vector3(0.2f, 0, 0),
        };
        _hairAntenna.AddChild(mainAntenna);

        // Antenna tip (glowing orb)
        var tipMesh = new SphereMesh
        {
            Radius = 0.025f,
            Height = 0.05f,
            RadialSegments = 12,
            Rings = 6,
        };
        var antennaTip = new MeshInstance3D
        {
            Name = "AntennaTip",
            Mesh = tipMesh,
            MaterialOverride = _particleMaterial,
            Position = new Vector3(0, 0.43f, 0.03f),
        };
        _hairAntenna.AddChild(antennaTip);

        // Side "hair" spikes
        var spikeMesh = new CylinderMesh
        {
            TopRadius = 0.005f,
            BottomRadius = 0.015f,
            Height = 0.08f,
            RadialSegments = 6,
        };

        // Left spikes
        for (int i = 0; i < 3; i++)
        {
            float angle = 0.3f + i * 0.15f;
            var spike = new MeshInstance3D
            {
                Mesh = spikeMesh,
                MaterialOverride = _hairMaterial,
                Position = new Vector3(-0.15f - i * 0.05f, 0.28f - i * 0.02f, 0),
                Rotation = new Vector3(0.1f, 0, -angle),
            };
            _hairAntenna.AddChild(spike);
        }

        // Right spikes (mirrored)
        for (int i = 0; i < 3; i++)
        {
            float angle = 0.3f + i * 0.15f;
            var spike = new MeshInstance3D
            {
                Mesh = spikeMesh,
                MaterialOverride = _hairMaterial,
                Position = new Vector3(0.15f + i * 0.05f, 0.28f - i * 0.02f, 0),
                Rotation = new Vector3(0.1f, 0, angle),
            };
            _hairAntenna.AddChild(spike);
        }
    }

    private void CreateNose()
    {
        // Simple triangular nose suggestion
        var noseMesh = new PrismMesh
        {
            Size = new Vector3(0.06f, 0.12f, 0.04f),
        };

        _nose = new MeshInstance3D
        {
            Name = "Nose",
            Mesh = noseMesh,
            MaterialOverride = _faceDetailMaterial,
            Position = new Vector3(0, -0.02f, 0.08f),
            Rotation = new Vector3(Mathf.Pi, 0, 0), // Point down
        };
        _headPivot?.AddChild(_nose);
    }

    private void CreateCheeks()
    {
        // Subtle cheekbone highlights
        var cheekMesh = new SphereMesh
        {
            Radius = 0.08f,
            Height = 0.16f,
            RadialSegments = 16,
            Rings = 8,
        };

        _leftCheek = new MeshInstance3D
        {
            Name = "LeftCheek",
            Mesh = cheekMesh,
            MaterialOverride = _faceDetailMaterial,
            Position = new Vector3(-0.22f, -0.05f, 0.02f),
            Scale = new Vector3(1.2f, 0.6f, 0.5f),
        };
        _headPivot?.AddChild(_leftCheek);

        _rightCheek = new MeshInstance3D
        {
            Name = "RightCheek",
            Mesh = cheekMesh,
            MaterialOverride = _faceDetailMaterial,
            Position = new Vector3(0.22f, -0.05f, 0.02f),
            Scale = new Vector3(1.2f, 0.6f, 0.5f),
        };
        _headPivot?.AddChild(_rightCheek);
    }

    private void CreateChin()
    {
        // Subtle chin definition
        var chinMesh = new SphereMesh
        {
            Radius = 0.06f,
            Height = 0.12f,
            RadialSegments = 16,
            Rings = 8,
        };

        _chin = new MeshInstance3D
        {
            Name = "Chin",
            Mesh = chinMesh,
            MaterialOverride = _faceDetailMaterial,
            Position = new Vector3(0, MouthHeight - 0.1f, 0.04f),
            Scale = new Vector3(1.5f, 0.8f, 0.6f),
        };
        _headPivot?.AddChild(_chin);
    }

    private void CreateStaticOverlay()
    {
        // Random static noise particles for screen effect
        _staticOverlay = new Node3D { Name = "StaticOverlay" };
        _headPivot?.AddChild(_staticOverlay);

        var staticMesh = new BoxMesh
        {
            Size = new Vector3(0.01f, 0.01f, 0.001f),
        };

        // Create a grid of small static particles
        for (int i = 0; i < 50; i++)
        {
            var particle = new MeshInstance3D
            {
                Mesh = staticMesh,
                MaterialOverride = _staticMaterial,
                Position = new Vector3(
                    GD.Randf() * 0.8f - 0.4f,
                    GD.Randf() * 0.6f - 0.3f,
                    0.12f
                ),
                Visible = false, // Start hidden
            };
            _staticOverlay.AddChild(particle);
        }
    }

    private void CreateParticleEffects()
    {
        // Floating holographic particles around the head
        _particleContainer = new Node3D { Name = "ParticleContainer" };
        _headPivot?.AddChild(_particleContainer);

        var particleMesh = new SphereMesh
        {
            Radius = 0.015f,
            Height = 0.03f,
            RadialSegments = 8,
            Rings = 4,
        };

        // Create orbiting particles
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8f;
            var particle = new MeshInstance3D
            {
                Name = $"Particle_{i}",
                Mesh = particleMesh,
                MaterialOverride = _particleMaterial,
                Position = new Vector3(
                    Mathf.Cos(angle) * 0.4f,
                    0.1f + (i % 3) * 0.1f,
                    Mathf.Sin(angle) * 0.2f - 0.1f
                ),
            };
            _particleContainer.AddChild(particle);
        }
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
        _headPivot?.AddChild(_leftEye);

        _rightEye = new MeshInstance3D
        {
            Name = "RightEye",
            Mesh = eyeMesh,
            MaterialOverride = _eyeMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight, 0),
        };
        _headPivot?.AddChild(_rightEye);

        // Iris (colored ring around pupil)
        var irisMesh = new TorusMesh
        {
            InnerRadius = PupilRadius * 0.9f,
            OuterRadius = IrisRadius,
            Rings = 16,
            RingSegments = 16,
        };

        _leftIris = new MeshInstance3D
        {
            Name = "LeftIris",
            Mesh = irisMesh,
            MaterialOverride = _irisMaterial,
            Position = new Vector3(-EyeSpacing, EyeHeight, EyeRadius * 0.72f),
            Rotation = new Vector3(Mathf.Pi / 2f, 0, 0),
        };
        _headPivot?.AddChild(_leftIris);

        _rightIris = new MeshInstance3D
        {
            Name = "RightIris",
            Mesh = irisMesh,
            MaterialOverride = _irisMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight, EyeRadius * 0.72f),
            Rotation = new Vector3(Mathf.Pi / 2f, 0, 0),
        };
        _headPivot?.AddChild(_rightIris);

        // Pupils (dark center)
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
            Position = new Vector3(-EyeSpacing, EyeHeight, EyeRadius * 0.75f),
        };
        _headPivot?.AddChild(_leftPupil);

        _rightPupil = new MeshInstance3D
        {
            Name = "RightPupil",
            Mesh = pupilMesh,
            MaterialOverride = _pupilMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight, EyeRadius * 0.75f),
        };
        _headPivot?.AddChild(_rightPupil);
    }

    private void CreateEyelids()
    {
        // Eyelids for blinking and expressions
        var eyelidMesh = new BoxMesh
        {
            Size = new Vector3(EyeRadius * 2.2f, EyeRadius * 0.8f, 0.02f),
        };

        _leftEyelid = new MeshInstance3D
        {
            Name = "LeftEyelid",
            Mesh = eyelidMesh,
            MaterialOverride = _eyelidMaterial,
            Position = new Vector3(-EyeSpacing, EyeHeight + EyeRadius * 0.6f, EyeRadius * 0.4f),
        };
        _headPivot?.AddChild(_leftEyelid);

        _rightEyelid = new MeshInstance3D
        {
            Name = "RightEyelid",
            Mesh = eyelidMesh,
            MaterialOverride = _eyelidMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight + EyeRadius * 0.6f, EyeRadius * 0.4f),
        };
        _headPivot?.AddChild(_rightEyelid);
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
        _headPivot?.AddChild(_leftEyebrow);

        _rightEyebrow = new MeshInstance3D
        {
            Name = "RightEyebrow",
            Mesh = browMesh,
            MaterialOverride = _eyebrowMaterial,
            Position = new Vector3(EyeSpacing, EyeHeight + EyeRadius + 0.05f, 0.05f),
        };
        _headPivot?.AddChild(_rightEyebrow);
    }

    private void CreateMouth()
    {
        // Mouth interior (dark)
        var mouthInteriorMesh = new BoxMesh
        {
            Size = new Vector3(0.2f, 0.08f, 0.03f),
        };

        _mouth = new MeshInstance3D
        {
            Name = "MouthInterior",
            Mesh = mouthInteriorMesh,
            MaterialOverride = _mouthMaterial,
            Position = new Vector3(0, MouthHeight, 0.02f),
        };
        _headPivot?.AddChild(_mouth);

        // Upper lip
        var upperLipMesh = new BoxMesh
        {
            Size = new Vector3(0.25f, 0.025f, 0.02f),
        };

        _upperLip = new MeshInstance3D
        {
            Name = "UpperLip",
            Mesh = upperLipMesh,
            MaterialOverride = _lipMaterial,
            Position = new Vector3(0, MouthHeight + 0.04f, 0.05f),
        };
        _headPivot?.AddChild(_upperLip);

        // Lower lip
        var lowerLipMesh = new BoxMesh
        {
            Size = new Vector3(0.22f, 0.03f, 0.02f),
        };

        _lowerLip = new MeshInstance3D
        {
            Name = "LowerLip",
            Mesh = lowerLipMesh,
            MaterialOverride = _lipMaterial,
            Position = new Vector3(0, MouthHeight - 0.04f, 0.05f),
        };
        _headPivot?.AddChild(_lowerLip);

        // Teeth (visible when mouth opens)
        var teethMesh = new BoxMesh
        {
            Size = new Vector3(0.16f, 0.03f, 0.01f),
        };

        _teeth = new MeshInstance3D
        {
            Name = "Teeth",
            Mesh = teethMesh,
            MaterialOverride = _teethMaterial,
            Position = new Vector3(0, MouthHeight + 0.02f, 0.035f),
        };
        _headPivot?.AddChild(_teeth);
    }

    private void CreateScanLines()
    {
        // Horizontal scan lines for retro effect
        var scanNode = new Node3D { Name = "ScanLines" };
        _headPivot?.AddChild(scanNode);

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
        UpdateHeadMovement(dt);
        UpdateEyeRollAnimation(dt);
        UpdateStaticEffect(dt);
        UpdateGlowPulse(dt);
        UpdateParticleOrbit(dt);
        UpdateEmotionColor(dt);
        ApplyAnimationValues();
    }

    private void UpdateHeadMovement(float dt)
    {
        // Update head nod animation
        if (_isNodding)
        {
            _nodProgress += dt * 4f;
            if (_nodProgress >= 1f)
            {
                _isNodding = false;
                _nodProgress = 0f;
                _targetHeadNod = 0f;
            }
            else
            {
                // Nodding motion: down-up-down pattern
                _currentHeadNod = Mathf.Sin(_nodProgress * Mathf.Pi * 2f) * 0.1f;
            }
        }

        // Update head shake animation
        if (_isShaking)
        {
            _shakeProgress += dt * 5f;
            if (_shakeProgress >= 1f)
            {
                _isShaking = false;
                _shakeProgress = 0f;
                _targetHeadTurn = 0f;
            }
            else
            {
                // Shaking motion: left-right-left pattern
                _currentHeadTurn = Mathf.Sin(_shakeProgress * Mathf.Pi * 3f) * 0.12f;
            }
        }

        // Lerp head rotation towards targets
        _currentHeadTilt = Mathf.Lerp(_currentHeadTilt, _targetHeadTilt, HeadLerpSpeed * dt);
        if (!_isNodding)
            _currentHeadNod = Mathf.Lerp(_currentHeadNod, _targetHeadNod, HeadLerpSpeed * dt);
        if (!_isShaking)
            _currentHeadTurn = Mathf.Lerp(_currentHeadTurn, _targetHeadTurn, HeadLerpSpeed * dt);

        // Apply to head pivot
        if (_headPivot != null)
        {
            _headPivot.Rotation = new Vector3(_currentHeadNod, _currentHeadTurn, _currentHeadTilt);
        }
    }

    private void UpdateGlowPulse(float dt)
    {
        _glowPulseTimer += dt;

        if (_holoGlow != null && _glowMaterial != null)
        {
            // Subtle pulsing glow
            float pulse = 0.15f + Mathf.Sin(_glowPulseTimer * 2f) * 0.05f;
            _glowMaterial.AlbedoColor = new Color(
                _currentEmotionColor.R * 0.5f,
                _currentEmotionColor.G * 0.5f,
                _currentEmotionColor.B * 0.5f,
                pulse
            );
            _glowMaterial.Emission = _currentEmotionColor;
        }
    }

    private void UpdateParticleOrbit(float dt)
    {
        _particleTimer += dt;

        if (_particleContainer == null) return;

        // Slowly rotate the particle container
        _particleContainer.Rotation = new Vector3(0, _particleTimer * 0.5f, 0);

        // Make particles bob up and down individually
        int childCount = _particleContainer.GetChildCount();
        for (int i = 0; i < childCount; i++)
        {
            var particle = _particleContainer.GetChild<MeshInstance3D>(i);
            if (particle == null) continue;

            float angle = i * Mathf.Tau / childCount;
            float bobOffset = Mathf.Sin(_particleTimer * 2f + angle) * 0.02f;
            var pos = particle.Position;
            pos.Y = 0.1f + (i % 3) * 0.1f + bobOffset;
            particle.Position = pos;

            // Fade particles based on emotion - update transparency
            float intensity = 0.4f + Mathf.Sin(_particleTimer * 3f + i) * 0.2f;
            particle.Transparency = 1f - intensity;
        }
    }

    private void UpdateEmotionColor(float dt)
    {
        // Smoothly transition between emotion colors
        _currentEmotionColor = _currentEmotionColor.Lerp(_targetEmotionColor, ColorLerpSpeed * dt);

        // Apply emotion color to emissive materials
        if (_eyebrowMaterial != null)
            _eyebrowMaterial.Emission = _currentEmotionColor;
        if (_lipMaterial != null)
            _lipMaterial.Emission = _currentEmotionColor;
        if (_hairMaterial != null)
            _hairMaterial.Emission = _currentEmotionColor;
        if (_irisMaterial != null)
            _irisMaterial.Emission = _currentEmotionColor.Lerp(IrisColor, 0.5f);
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
        _currentLeftEyebrowHeight = Mathf.Lerp(_currentLeftEyebrowHeight, _targetLeftEyebrowHeight, LerpSpeed * dt);
        _currentRightEyebrowHeight = Mathf.Lerp(_currentRightEyebrowHeight, _targetRightEyebrowHeight, LerpSpeed * dt);
        _currentMouthWidth = Mathf.Lerp(_currentMouthWidth, _targetMouthWidth, LerpSpeed * dt);
        _currentMouthCurve = Mathf.Lerp(_currentMouthCurve, _targetMouthCurve, LerpSpeed * dt);
        _currentPupilOffset = _currentPupilOffset.Lerp(_targetPupilOffset, LerpSpeed * dt);
        _currentPupilDilation = Mathf.Lerp(_currentPupilDilation, _targetPupilDilation, LerpSpeed * dt);

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

    private void UpdateEyeRollAnimation(float dt)
    {
        if (!_isRollingEyes) return;

        _eyeRollProgress += dt * 1.5f; // Roll takes ~0.7 seconds

        if (_eyeRollProgress >= 1f)
        {
            _isRollingEyes = false;
            _eyeRollProgress = 0f;
            _currentPupilOffset = Vector3.Zero;
            return;
        }

        // Circular eye roll motion
        float angle = _eyeRollProgress * Mathf.Tau;
        _currentPupilOffset = new Vector3(
            Mathf.Sin(angle) * 0.04f,
            Mathf.Cos(angle) * 0.03f,
            0
        );
    }

    private void UpdateStaticEffect(float dt)
    {
        _staticTimer += dt;

        // Decay static intensity
        _staticIntensity = Mathf.Lerp(_staticIntensity, 0f, dt * 3f);

        // Random static flickers
        if (_staticOverlay == null) return;

        int childCount = _staticOverlay.GetChildCount();
        for (int i = 0; i < childCount; i++)
        {
            var particle = _staticOverlay.GetChild<MeshInstance3D>(i);
            if (particle == null) continue;

            // Show random particles based on intensity
            bool shouldShow = GD.Randf() < _staticIntensity * 0.3f;
            particle.Visible = shouldShow;

            // Randomly reposition visible particles
            if (shouldShow)
            {
                particle.Position = new Vector3(
                    GD.Randf() * 0.8f - 0.4f,
                    GD.Randf() * 0.6f - 0.3f,
                    0.12f
                );
            }
        }
    }

    /// <summary>
    /// Trigger screen static effect
    /// </summary>
    public void TriggerStatic(float intensity = 0.8f)
    {
        _staticIntensity = Mathf.Clamp(intensity, 0f, 1f);
    }

    private void ApplyAnimationValues()
    {
        // Apply eyebrow rotations and heights
        if (_leftEyebrow != null)
        {
            _leftEyebrow.Rotation = new Vector3(0, 0, Mathf.DegToRad(_currentLeftEyebrowAngle));
            var basePos = new Vector3(-EyeSpacing, EyeHeight + EyeRadius + 0.05f, 0.05f);
            _leftEyebrow.Position = basePos + new Vector3(0, _currentLeftEyebrowHeight * 0.03f, 0);
        }
        if (_rightEyebrow != null)
        {
            _rightEyebrow.Rotation = new Vector3(0, 0, Mathf.DegToRad(-_currentRightEyebrowAngle));
            var basePos = new Vector3(EyeSpacing, EyeHeight + EyeRadius + 0.05f, 0.05f);
            _rightEyebrow.Position = basePos + new Vector3(0, _currentRightEyebrowHeight * 0.03f, 0);
        }

        // Apply mouth interior scale
        if (_mouth != null)
        {
            float openScale = 0.5f + _currentMouthOpenness * 1.5f;
            float widthScale = _currentMouthWidth;
            _mouth.Scale = new Vector3(widthScale, openScale, 1);
            _mouth.Position = new Vector3(0, MouthHeight + _currentMouthCurve * 0.02f, 0.02f);
        }

        // Apply lip positions (spread apart when mouth opens)
        float lipSpread = _currentMouthOpenness * 0.04f;
        if (_upperLip != null)
        {
            _upperLip.Position = new Vector3(0, MouthHeight + 0.04f + lipSpread, 0.05f);
            _upperLip.Scale = new Vector3(_currentMouthWidth, 1, 1);
        }
        if (_lowerLip != null)
        {
            _lowerLip.Position = new Vector3(0, MouthHeight - 0.04f - lipSpread, 0.05f);
            _lowerLip.Scale = new Vector3(_currentMouthWidth * 0.9f, 1, 1);
        }

        // Apply teeth visibility (more visible when mouth opens)
        if (_teeth != null)
        {
            float teethVisibility = Mathf.Clamp(_currentMouthOpenness * 2f, 0f, 1f);
            _teeth.Visible = teethVisibility > 0.2f;
            _teeth.Scale = new Vector3(_currentMouthWidth * 0.8f, teethVisibility, 1);
        }

        // Apply pupil positions with dilation
        float pupilScale = _currentPupilDilation;
        if (_leftPupil != null)
        {
            _leftPupil.Position = new Vector3(-EyeSpacing, EyeHeight, EyeRadius * 0.75f) + _currentPupilOffset;
            _leftPupil.Scale = new Vector3(pupilScale, pupilScale, pupilScale);
        }
        if (_rightPupil != null)
        {
            _rightPupil.Position = new Vector3(EyeSpacing, EyeHeight, EyeRadius * 0.75f) + _currentPupilOffset;
            _rightPupil.Scale = new Vector3(pupilScale, pupilScale, pupilScale);
        }

        // Apply iris positions (follow pupils)
        if (_leftIris != null)
            _leftIris.Position = new Vector3(-EyeSpacing, EyeHeight, EyeRadius * 0.72f) + _currentPupilOffset;
        if (_rightIris != null)
            _rightIris.Position = new Vector3(EyeSpacing, EyeHeight, EyeRadius * 0.72f) + _currentPupilOffset;

        // Apply eye squint (scale Y)
        float eyeScaleY = 1f - _currentEyeSquint * 0.8f;
        if (_leftEye != null)
            _leftEye.Scale = new Vector3(1, eyeScaleY, 1);
        if (_rightEye != null)
            _rightEye.Scale = new Vector3(1, eyeScaleY, 1);

        // Apply eyelids (drop down when squinting/blinking)
        float eyelidDrop = _currentEyeSquint * EyeRadius * 0.8f;
        if (_leftEyelid != null)
        {
            var basePos = new Vector3(-EyeSpacing, EyeHeight + EyeRadius * 0.6f, EyeRadius * 0.4f);
            _leftEyelid.Position = basePos - new Vector3(0, eyelidDrop, 0);
        }
        if (_rightEyelid != null)
        {
            var basePos = new Vector3(EyeSpacing, EyeHeight + EyeRadius * 0.6f, EyeRadius * 0.4f);
            _rightEyelid.Position = basePos - new Vector3(0, eyelidDrop, 0);
        }
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
                SetExpressionValues(0, 0, 0, 0, 0, 1, 0, Vector3.Zero, 0, 1f, EmissionColor, 0, 0);
                break;

            case AvatarExpression.Bored:
                SetExpressionValues(10, 10, -0.5f, -0.5f, 0, 0.8f, -0.3f, new Vector3(0.03f, -0.02f, 0), 0.3f, 0.9f, BoredColor, 0.05f, 0);
                break;

            case AvatarExpression.Smug:
                SetExpressionValues(-15, 20, 0.5f, -0.2f, 0, 0.9f, 0.5f, new Vector3(0.02f, 0.01f, 0), 0.2f, 0.95f, SmugColor, -0.03f, 0.02f);
                break;

            case AvatarExpression.FakeExcited:
                SetExpressionValues(-20, -20, 0.8f, 0.8f, 0.3f, 1.2f, 0.8f, new Vector3(0, 0.02f, 0), 0, 1.3f, HappyColor, 0, 0);
                break;

            case AvatarExpression.Excited:
                SetExpressionValues(-25, -25, 1f, 1f, 0.4f, 1.3f, 1.0f, new Vector3(0, 0.02f, 0), 0, 1.4f, HappyColor, 0, 0);
                DoNod();
                break;

            case AvatarExpression.Impressed:
                SetExpressionValues(-20, -20, 0.6f, 0.6f, 0.2f, 1.0f, 0.6f, new Vector3(0, 0.02f, 0), 0, 1.2f, HappyColor, 0.02f, 0);
                break;

            case AvatarExpression.SlightlyImpressed:
                SetExpressionValues(-10, -10, 0.3f, 0.3f, 0, 1.0f, 0.3f, Vector3.Zero, 0.1f, 1.1f, EmissionColor, 0.02f, 0);
                break;

            case AvatarExpression.Disappointed:
                SetExpressionValues(15, 15, -0.3f, -0.3f, 0, 0.7f, -0.8f, new Vector3(0, -0.02f, 0), 0.2f, 0.85f, BoredColor, 0.05f, 0);
                DoHeadShake();
                break;

            case AvatarExpression.FakeWorried:
                SetExpressionValues(-30, 10, 0.5f, -0.2f, 0.1f, 0.9f, -0.4f, new Vector3(-0.02f, 0.01f, 0), 0, 1.1f, SurprisedColor, 0.03f, -0.02f);
                break;

            case AvatarExpression.Surprised:
                SetExpressionValues(-30, -30, 1.2f, 1.2f, 0.5f, 1.4f, 0, new Vector3(0, 0.02f, 0), 0, 1.5f, SurprisedColor, -0.05f, 0);
                break;

            case AvatarExpression.Angry:
                SetExpressionValues(30, 30, -0.5f, -0.5f, 0.1f, 0.6f, -0.6f, new Vector3(0, 0, 0), 0.4f, 0.7f, AngryColor, 0.03f, 0);
                TriggerStatic(0.4f);
                break;

            case AvatarExpression.Talking:
                // Talking is handled separately
                break;

            case AvatarExpression.EyeRoll:
                // Start eye roll animation
                SetExpressionValues(-10, 20, 0.2f, -0.3f, 0, 0.8f, -0.3f, Vector3.Zero, 0.1f, 0.9f, SmugColor, 0.03f, -0.02f);
                DoEyeRoll();
                break;

            case AvatarExpression.Skeptical:
                // One eyebrow raised (classic skepticism)
                SetExpressionValues(-25, 15, 0.8f, -0.3f, 0, 0.85f, 0.2f, new Vector3(0.02f, 0, 0), 0.15f, 0.95f, SmugColor, 0, 0.03f);
                break;

            case AvatarExpression.Thinking:
                // Looking up thoughtfully
                SetExpressionValues(-15, -15, 0.4f, 0.4f, 0, 0.9f, 0.1f, new Vector3(0, 0.03f, 0), 0, 1.05f, EmissionColor, 0.04f, 0);
                break;
        }
    }

    private void SetExpressionValues(
        float leftBrow, float rightBrow, float leftBrowHeight, float rightBrowHeight,
        float mouthOpen, float mouthWidth, float mouthCurve, Vector3 pupilOffset,
        float eyeSquint, float pupilDilation, Color emotionColor, float headTilt, float headTurn)
    {
        _targetLeftEyebrowAngle = leftBrow;
        _targetRightEyebrowAngle = rightBrow;
        _targetLeftEyebrowHeight = leftBrowHeight;
        _targetRightEyebrowHeight = rightBrowHeight;
        _targetMouthOpenness = mouthOpen;
        _targetMouthWidth = mouthWidth;
        _targetMouthCurve = mouthCurve;
        _targetPupilOffset = pupilOffset;
        _targetEyeSquint = eyeSquint;
        _targetPupilDilation = pupilDilation;
        _targetEmotionColor = emotionColor;
        _targetHeadTilt = headTilt;
        _targetHeadTurn = headTurn;
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
        _isRollingEyes = true;
        _eyeRollProgress = 0f;

        // Also trigger static effect for dramatic flair
        TriggerStatic(0.3f);
    }

    /// <summary>
    /// Trigger a head nod (for agreement or acknowledgment)
    /// </summary>
    public void DoNod()
    {
        _isNodding = true;
        _nodProgress = 0f;
    }

    /// <summary>
    /// Trigger a head shake (for disagreement or disappointment)
    /// </summary>
    public void DoHeadShake()
    {
        _isShaking = true;
        _shakeProgress = 0f;
    }

    /// <summary>
    /// Trigger a dramatic glitch with static for emphasis
    /// </summary>
    public void DoGlitchOut()
    {
        TriggerGlitch();
        TriggerStatic(0.6f);
    }
}
