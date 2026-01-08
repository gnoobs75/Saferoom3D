using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Combat;
using SafeRoom3D.Abilities;
using SafeRoom3D.Items;
using SafeRoom3D.UI;
using SafeRoom3D.Broadcaster;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.Player;

/// <summary>
/// First-person player controller with mouse look, WASD movement, jumping, and sprinting.
/// Skyrim-inspired immersive dungeon exploration.
/// </summary>
public partial class FPSController : CharacterBody3D
{
    // Singleton for easy access
    public static FPSController? Instance { get; private set; }

    // Components
    private Camera3D? _camera;
    private Node3D? _head;           // Pivot for vertical look
    private CollisionShape3D? _collider;
    private RayCast3D? _interactRay; // For picking up items, opening doors
    private Node3D? _weaponHolder;   // Attach weapon models here

    // First-person weapon display
    private Node3D? _torchNode;      // Torch in left hand (when equipped)
    private Node3D? _weaponNode;     // Weapon in right hand (dynamic based on equipment)
    private Node3D? _leftHandNode;   // Left hand item: torch, shield, or dual-wield weapon
    private OmniLight3D? _torchLight;
    private float _torchFlickerTimer;
    private float _weaponSwingAngle;  // Current swing angle for attack animation
    private bool _isWeaponSwinging;   // Whether weapon is mid-swing
    private float _weaponSwingTimer;
    private Vector3 _weaponRestPos;
    private Vector3 _weaponSwingPos;
    private Core.WeaponType _currentWeaponType = Core.WeaponType.None;  // Track current weapon to avoid unnecessary rebuilds

    // Left hand / dual wield state
    private enum LeftHandItemType { None, Torch, Shield, Weapon }
    private LeftHandItemType _leftHandType = LeftHandItemType.None;
    private bool _isLeftHandSwinging;
    private float _leftHandSwingTimer;
    private Vector3 _leftHandRestPos;
    private Vector3 _leftHandSwingPos;
    private bool _lastSwingWasRight = true;  // For alternating dual wield
    private float _attackHoldTimer;           // For detecting hold vs click
    private const float AttackHoldThreshold = 0.15f; // 150ms to trigger dual swing

    // Movement state
    private Vector3 _velocity;
    private bool _isSprinting;
    private float _headBobTime;

    // Camera state
    private float _cameraPitch;      // Vertical rotation (stored separately)
    private float _targetFov;
    private float _currentFov;

    // Stats - now backed by CharacterStats system
    public CharacterStats Stats { get; private set; } = new();
    public EquipmentManager Equipment { get; private set; } = new();

    public int CurrentHealth { get; private set; }
    public int MaxHealth => Stats.MaxHealth;
    public int CurrentMana { get; private set; }
    public int MaxMana => Stats.MaxMana;
    public int CurrentExperience { get; private set; }
    public int ExperienceToLevel { get; private set; }
    public int PlayerLevel { get; private set; } = 1;

    // Regeneration timers
    private float _healthRegenTimer;
    private float _manaRegenTimer;
    private const float RegenTickInterval = 0.5f;

    // Random for combat rolls
    private readonly System.Random _combatRandom = new();

    // Combat state
    private float _attackCooldown;
    private bool _isAttacking;
    private float _attackTimer;

    // Charged attack state
    private bool _isChargingAttack;
    private float _chargeTimer;
    private const float StrongAttackChargeTime = 0.5f; // Hold for 0.5 seconds for strong attack
    private const float StrongAttackDamageMultiplier = 2f;
    private const float StrongAttackKnockbackMultiplier = 3f;
    private MeshInstance3D? _chargeIndicator;

    // Screen shake
    private float _shakeTimer;
    private float _shakeIntensity;
    private Vector2 _shakeOffset;

    // Safety respawn (prevent falling through world)
    private Vector3 _lastSafePosition;
    private float _safePositionTimer;
    private const float SafePositionInterval = 0.5f;
    private const float FallDeathHeight = -20f;

    // Mouse control lock - when true, FPSController won't auto-recapture mouse
    // Used by AbilityManager during targeting
    public bool MouseControlLocked { get; set; }

    // Inspect mode - player can look but not move
    private bool _isInspectMode;

    // Edit mode - player can move but not attack
    private bool _isEditMode;

    // Signals
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void ManaChangedEventHandler(int current, int max);
    [Signal] public delegate void ExperienceChangedEventHandler(int current, int toLevel, int level);
    [Signal] public delegate void LeveledUpEventHandler(int newLevel);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void AttackedEventHandler();

    public override void _Ready()
    {
        Instance = this;

        // Process even when time is paused (for mouse recapture during tactical targeting)
        ProcessMode = ProcessModeEnum.Always;

        // Add to Player group so enemies can find us
        AddToGroup("Player");

        // Initialize stats system
        Stats = new CharacterStats();
        Equipment = new EquipmentManager();
        Equipment.Initialize(Stats);
        Equipment.OnEquipmentChanged += OnEquipmentChanged;

        // Initialize affix database
        AffixDatabase.Initialize();

        // Equip starter gear (Normal/gray items)
        Equipment.EquipStarterGear();

        // Set initial level and recalculate
        Stats.SetLevel(1);
        CurrentHealth = Stats.MaxHealth;
        CurrentMana = Stats.MaxMana;
        CurrentExperience = Constants.StartingExperience;
        ExperienceToLevel = Constants.ExperienceToLevel;

        // Set up node hierarchy
        SetupComponents();

        // Capture mouse for FPS control
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _targetFov = Constants.CameraFov;
        _currentFov = Constants.CameraFov;

        // Initialize safe position
        _lastSafePosition = GlobalPosition;

        // Connect to window focus signals for robust mouse recapture
        GetTree().Root.FocusEntered += OnWindowFocusEntered;

        GD.Print("[FPSController] Ready - First-person player initialized");
    }

    private void SetupComponents()
    {
        // Create head pivot (for vertical look)
        _head = new Node3D();
        _head.Name = "Head";
        _head.Position = new Vector3(0, Constants.PlayerEyeHeight, 0);
        AddChild(_head);

        // Create camera
        _camera = new Camera3D();
        _camera.Name = "Camera3D";
        _camera.Fov = Constants.CameraFov;
        _camera.Near = Constants.CameraNear;
        _camera.Far = Constants.CameraFar;
        _camera.Current = true;
        _head.AddChild(_camera);

        // Create interact raycast (for picking up items, etc.)
        _interactRay = new RayCast3D();
        _interactRay.Name = "InteractRay";
        _interactRay.TargetPosition = new Vector3(0, 0, -3); // 3 meters forward
        _interactRay.Enabled = true;
        _camera.AddChild(_interactRay);

        // Create weapon holder (for first-person weapon display)
        _weaponHolder = new Node3D();
        _weaponHolder.Name = "WeaponHolder";
        _weaponHolder.Position = new Vector3(0, 0, 0);
        _camera.AddChild(_weaponHolder);

        // Create first-person torch (left hand)
        CreateFirstPersonTorch();

        // Create first-person weapon (right hand) based on equipped item
        CreateFirstPersonWeapon();

        // Create collision shape (capsule for player body)
        // Larger radius to keep enemies from clipping into player
        _collider = new CollisionShape3D();
        _collider.Name = "Collider";
        var capsule = new CapsuleShape3D();
        capsule.Radius = 0.8f;  // Doubled from 0.4 to prevent enemy clipping
        capsule.Height = 1.8f;
        _collider.Shape = capsule;
        _collider.Position = new Vector3(0, 0.9f, 0); // Center at half height
        AddChild(_collider);

        // Set collision layers
        // Layer 1 = Player, Layer 5 = Obstacle (bit 4 = 16), Layer 8 = Wall (bit 7 = 128)
        CollisionLayer = 1;  // Player layer
        CollisionMask = 1 | 16 | 128; // Player, Obstacle, Wall (layer 8)
    }

    private void CreateFirstPersonTorch()
    {
        // Torch in left hand - positioned at left side
        _torchNode = new Node3D();
        _torchNode.Name = "FirstPersonTorch";
        _torchNode.Position = new Vector3(-0.5f, -0.65f, -0.35f); // Left side, lowered so flame is centered

        // Constants for torch geometry
        float handleHeight = 0.5f;
        float headRadius = 0.04f;

        // Torch handle (wooden stick) - mostly vertical
        var handle = new MeshInstance3D();
        var handleMesh = new CylinderMesh();
        handleMesh.TopRadius = 0.016f;
        handleMesh.BottomRadius = 0.022f;
        handleMesh.Height = handleHeight;
        handle.Mesh = handleMesh;
        var handleMat = new StandardMaterial3D();
        handleMat.AlbedoColor = new Color(0.4f, 0.28f, 0.15f); // Wood color
        handleMat.Roughness = 0.9f;
        handle.MaterialOverride = handleMat;
        // Position handle center, slightly tilted toward player view
        handle.Position = new Vector3(0, handleHeight / 2f, 0);
        _torchNode.AddChild(handle);

        // Fire position - exactly at top of handle
        Vector3 firePos = new Vector3(0, handleHeight + headRadius * 0.5f, 0);

        // Torch head wrapping (cloth/tar) - at top of handle
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh();
        headMesh.Radius = headRadius;
        headMesh.Height = headRadius * 2f;
        head.Mesh = headMesh;
        var headMat = new StandardMaterial3D();
        headMat.AlbedoColor = new Color(0.2f, 0.12f, 0.06f);
        headMat.Roughness = 1f;
        head.MaterialOverride = headMat;
        head.Position = firePos;
        _torchNode.AddChild(head);

        // Create fire particles - emit from torch head (same position as head)
        var fireParticles = new GpuParticles3D();
        fireParticles.Name = "FireParticles";
        fireParticles.Position = firePos;  // Exactly at torch head position
        fireParticles.Amount = 30;
        fireParticles.Lifetime = 0.5f;
        fireParticles.Explosiveness = 0.1f;
        fireParticles.Randomness = 0.3f;

        var fireMaterial = new ParticleProcessMaterial();
        fireMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        fireMaterial.EmissionSphereRadius = 0.03f;
        fireMaterial.Direction = new Vector3(0, 1, 0);
        fireMaterial.Spread = 15f;
        fireMaterial.InitialVelocityMin = 0.3f;
        fireMaterial.InitialVelocityMax = 0.6f;
        fireMaterial.Gravity = new Vector3(0, 0.5f, 0); // Flames rise
        fireMaterial.ScaleMin = 0.6f;
        fireMaterial.ScaleMax = 1.2f;
        fireMaterial.Color = new Color(1f, 0.6f, 0.1f);
        var fireColorRamp = new Gradient();
        fireColorRamp.SetColor(0, new Color(1f, 0.9f, 0.4f)); // Bright yellow core
        fireColorRamp.SetColor(1, new Color(1f, 0.3f, 0.0f, 0f)); // Fade to transparent orange
        var fireTexture = new GradientTexture1D();
        fireTexture.Gradient = fireColorRamp;
        fireMaterial.ColorRamp = fireTexture;
        fireParticles.ProcessMaterial = fireMaterial;

        // Fire particle mesh (small billboarded quads)
        var fireQuad = new QuadMesh();
        fireQuad.Size = new Vector2(0.04f, 0.06f);
        fireParticles.DrawPass1 = fireQuad;

        // Fire material for particle mesh
        var fireDrawMat = new StandardMaterial3D();
        fireDrawMat.AlbedoColor = new Color(1f, 0.7f, 0.2f);
        fireDrawMat.EmissionEnabled = true;
        fireDrawMat.Emission = new Color(1f, 0.5f, 0.1f);
        fireDrawMat.EmissionEnergyMultiplier = 5f;
        fireDrawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        fireDrawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        fireDrawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        fireParticles.MaterialOverride = fireDrawMat;

        _torchNode.AddChild(fireParticles);

        // Create smoke particles
        var smokeParticles = new GpuParticles3D();
        smokeParticles.Name = "SmokeParticles";
        smokeParticles.Position = firePos + new Vector3(0, 0.05f, 0);  // Slightly above fire
        smokeParticles.Amount = 12;
        smokeParticles.Lifetime = 1.2f;
        smokeParticles.Explosiveness = 0.05f;
        smokeParticles.Randomness = 0.5f;

        var smokeMaterial = new ParticleProcessMaterial();
        smokeMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        smokeMaterial.EmissionSphereRadius = 0.02f;
        smokeMaterial.Direction = new Vector3(0, 1, 0);
        smokeMaterial.Spread = 20f;
        smokeMaterial.InitialVelocityMin = 0.15f;
        smokeMaterial.InitialVelocityMax = 0.3f;
        smokeMaterial.Gravity = new Vector3(0, 0.2f, 0); // Smoke rises slowly
        smokeMaterial.ScaleMin = 0.3f;
        smokeMaterial.ScaleMax = 1.0f;
        var smokeColorRamp = new Gradient();
        smokeColorRamp.SetColor(0, new Color(0.3f, 0.25f, 0.2f, 0.4f)); // Gray smoke
        smokeColorRamp.SetColor(1, new Color(0.2f, 0.18f, 0.15f, 0f)); // Fade out
        var smokeTexture = new GradientTexture1D();
        smokeTexture.Gradient = smokeColorRamp;
        smokeMaterial.ColorRamp = smokeTexture;
        smokeParticles.ProcessMaterial = smokeMaterial;

        var smokeQuad = new QuadMesh();
        smokeQuad.Size = new Vector2(0.06f, 0.08f);
        smokeParticles.DrawPass1 = smokeQuad;

        var smokeDrawMat = new StandardMaterial3D();
        smokeDrawMat.AlbedoColor = new Color(0.3f, 0.28f, 0.25f, 0.5f);
        smokeDrawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        smokeDrawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        smokeDrawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        smokeParticles.MaterialOverride = smokeDrawMat;

        _torchNode.AddChild(smokeParticles);

        // Smaller static flame core for visual base
        var flameCore = new MeshInstance3D();
        var flameMesh = new SphereMesh();
        flameMesh.Radius = 0.04f;
        flameMesh.Height = 0.10f;
        flameCore.Mesh = flameMesh;
        flameCore.Name = "FlameCore";
        var flameMat = new StandardMaterial3D();
        flameMat.AlbedoColor = new Color(1f, 0.8f, 0.3f);
        flameMat.EmissionEnabled = true;
        flameMat.Emission = new Color(1f, 0.6f, 0.2f);
        flameMat.EmissionEnergyMultiplier = 4f;
        flameMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        flameCore.MaterialOverride = flameMat;
        flameCore.Position = firePos;  // At torch head position
        _torchNode.AddChild(flameCore);

        _weaponHolder?.AddChild(_torchNode);

        // Create dynamic torch light
        _torchLight = new OmniLight3D();
        _torchLight.Position = firePos;  // At torch head/flame position
        _torchLight.LightColor = new Color(1f, 0.7f, 0.35f);
        _torchLight.LightEnergy = 2.5f; // Brighter for better visibility
        _torchLight.OmniRange = 12f;    // Extended range to light up more dungeon
        _torchLight.OmniAttenuation = 1.2f; // Smoother falloff
        _torchLight.ShadowEnabled = false;
        _torchNode.AddChild(_torchLight);

        GD.Print("[FPSController] First-person torch with fire/smoke particles created");
    }

    /// <summary>
    /// Create the first-person weapon based on currently equipped MainHand item.
    /// Uses WeaponFactory for procedural weapon meshes.
    /// </summary>
    private void CreateFirstPersonWeapon()
    {
        // Get equipped weapon
        var mainHand = Equipment.GetEquippedItem(EquipmentSlot.MainHand);
        var weaponType = mainHand?.WeaponType ?? Core.WeaponType.ShortSword;

        // Skip if same weapon type already equipped
        if (weaponType == _currentWeaponType && _weaponNode != null)
            return;

        // Remove old weapon if exists
        if (_weaponNode != null)
        {
            _weaponNode.QueueFree();
            _weaponNode = null;
        }

        _currentWeaponType = weaponType;

        // Map Core.WeaponType to WeaponFactory.WeaponType
        var factoryType = MapToFactoryWeaponType(weaponType);

        // Get positioning data for this weapon type
        var (restPos, swingPos, restRotation, scale) = GetWeaponPositioning(weaponType);
        _weaponRestPos = restPos;
        _weaponSwingPos = swingPos;

        // Create weapon container
        _weaponNode = new Node3D();
        _weaponNode.Name = "FirstPersonWeapon";
        _weaponNode.Position = _weaponRestPos;
        _weaponNode.RotationDegrees = restRotation;

        // Create weapon mesh from factory
        var weaponMesh = WeaponFactory.CreateWeapon(factoryType, scale);
        weaponMesh.Name = "WeaponMesh";

        // Rotate to convert from WeaponFactory's +Z forward to first-person view orientation
        // WeaponFactory weapons point along +Z, we need them pointing up (+Y) in rest pose
        weaponMesh.RotationDegrees = new Vector3(-90, 0, 0);

        _weaponNode.AddChild(weaponMesh);
        _weaponHolder?.AddChild(_weaponNode);

        GD.Print($"[FPSController] First-person weapon created: {weaponType}");
    }

    /// <summary>
    /// Map Core.WeaponType to WeaponFactory.WeaponType
    /// </summary>
    private static WeaponFactory.WeaponType MapToFactoryWeaponType(Core.WeaponType coreType)
    {
        return coreType switch
        {
            Core.WeaponType.Dagger => WeaponFactory.WeaponType.Dagger,
            Core.WeaponType.ShortSword => WeaponFactory.WeaponType.ShortSword,
            Core.WeaponType.LongSword => WeaponFactory.WeaponType.LongSword,
            Core.WeaponType.Axe => WeaponFactory.WeaponType.Axe,
            Core.WeaponType.BattleAxe => WeaponFactory.WeaponType.BattleAxe,
            Core.WeaponType.Spear => WeaponFactory.WeaponType.Spear,
            Core.WeaponType.Mace => WeaponFactory.WeaponType.Mace,
            Core.WeaponType.WarHammer => WeaponFactory.WeaponType.WarHammer,
            Core.WeaponType.Staff => WeaponFactory.WeaponType.Staff,
            Core.WeaponType.Bow => WeaponFactory.WeaponType.Bow,
            Core.WeaponType.Club => WeaponFactory.WeaponType.Club,
            Core.WeaponType.Scythe => WeaponFactory.WeaponType.Scythe,
            _ => WeaponFactory.WeaponType.ShortSword
        };
    }

    /// <summary>
    /// Get positioning data for each weapon type in first-person view.
    /// Returns: (restPosition, swingPosition, restRotation, scale)
    /// </summary>
    private static (Vector3 restPos, Vector3 swingPos, Vector3 restRotation, float scale) GetWeaponPositioning(Core.WeaponType type)
    {
        return type switch
        {
            // One-handed weapons - right side, diagonal hold
            Core.WeaponType.Dagger => (
                new Vector3(0.55f, -0.25f, -0.3f),   // Rest: closer in, lower
                new Vector3(-0.15f, 0.0f, -0.45f),  // Swing: quick jab motion
                new Vector3(-25, 30, 45),           // More compact angle
                0.8f                                 // Smaller scale for dagger
            ),
            Core.WeaponType.ShortSword => (
                new Vector3(0.65f, -0.2f, -0.35f),  // Rest
                new Vector3(-0.2f, 0.0f, -0.5f),   // Swing
                new Vector3(-35, 25, 55),          // Classic sword angle
                0.9f
            ),
            Core.WeaponType.LongSword => (
                new Vector3(0.7f, -0.15f, -0.4f),   // Rest: slightly further out
                new Vector3(-0.25f, 0.05f, -0.55f), // Swing: wider arc
                new Vector3(-30, 20, 50),
                1.0f
            ),
            Core.WeaponType.Axe => (
                new Vector3(0.6f, -0.25f, -0.35f),  // Rest
                new Vector3(-0.3f, 0.1f, -0.5f),   // Swing: chopping motion
                new Vector3(-40, 35, 60),          // More angled for chopping
                0.9f
            ),
            // Two-handed weapons - more centered, both hands
            Core.WeaponType.BattleAxe => (
                new Vector3(0.35f, -0.3f, -0.5f),   // Rest: centered
                new Vector3(-0.1f, 0.15f, -0.65f), // Swing: overhead chop
                new Vector3(-45, 15, 35),          // More vertical
                1.1f
            ),
            Core.WeaponType.Spear => (
                new Vector3(0.3f, -0.15f, -0.6f),   // Rest: centered, forward
                new Vector3(0.3f, 0.0f, -0.9f),    // Swing: thrust forward
                new Vector3(-15, 0, 15),           // More horizontal/thrust ready
                1.0f
            ),
            Core.WeaponType.Mace => (
                new Vector3(0.6f, -0.25f, -0.35f),  // Similar to axe
                new Vector3(-0.25f, 0.1f, -0.5f),
                new Vector3(-40, 30, 55),
                0.9f
            ),
            Core.WeaponType.WarHammer => (
                new Vector3(0.4f, -0.3f, -0.45f),   // Centered, heavy
                new Vector3(-0.15f, 0.2f, -0.6f),  // Overhead swing
                new Vector3(-50, 20, 40),          // More overhead angle
                1.0f
            ),
            Core.WeaponType.Staff => (
                new Vector3(0.25f, -0.2f, -0.55f),  // Centered
                new Vector3(0.2f, 0.1f, -0.7f),    // Thrust/swing
                new Vector3(-20, 5, 20),           // Mostly vertical
                0.9f
            ),
            Core.WeaponType.Bow => (
                new Vector3(0.4f, -0.1f, -0.5f),    // Left-center (held sideways)
                new Vector3(0.4f, 0.0f, -0.6f),    // Draw back slightly
                new Vector3(0, 0, 90),             // Held vertically
                0.85f
            ),
            Core.WeaponType.Club => (
                new Vector3(0.6f, -0.3f, -0.35f),   // Similar to mace
                new Vector3(-0.25f, 0.05f, -0.5f),
                new Vector3(-35, 30, 50),
                0.85f
            ),
            Core.WeaponType.Scythe => (
                new Vector3(0.3f, -0.25f, -0.5f),   // Centered, two-handed
                new Vector3(-0.2f, 0.1f, -0.65f),  // Sweeping motion
                new Vector3(-35, 25, 30),
                1.0f
            ),
            // Default fallback
            _ => (
                new Vector3(0.65f, -0.2f, -0.35f),
                new Vector3(-0.2f, 0.0f, -0.5f),
                new Vector3(-35, 25, 55),
                0.9f
            )
        };
    }

    /// <summary>
    /// Get weapon-specific animation parameters for different swing styles.
    /// Returns: (speedMultiplier, yArcDegrees, zRollDegrees, swingStyle)
    /// </summary>
    private static (float speed, float yArc, float zRoll, string style) GetWeaponAnimationStyle(Core.WeaponType type)
    {
        return type switch
        {
            // Quick jabbing weapons - fast, small arc, thrust style
            Core.WeaponType.Dagger => (1.4f, 40f, 25f, "thrust"),

            // Balanced swords - standard slash
            Core.WeaponType.ShortSword => (1.1f, 85f, 45f, "slash"),
            Core.WeaponType.LongSword => (1.0f, 90f, 50f, "slash"),

            // Heavy chopping weapons - slower, downward arc
            Core.WeaponType.Axe => (0.9f, 70f, 55f, "chop"),
            Core.WeaponType.BattleAxe => (0.8f, 75f, 40f, "chop"),

            // Smashing weapons - overhead motion
            Core.WeaponType.Mace => (0.85f, 60f, 45f, "smash"),
            Core.WeaponType.WarHammer => (0.75f, 65f, 35f, "smash"),
            Core.WeaponType.Club => (0.9f, 55f, 40f, "smash"),

            // Thrusting weapons - linear poke
            Core.WeaponType.Spear => (1.1f, 20f, 15f, "thrust"),

            // Sweeping weapons - wide arc
            Core.WeaponType.Staff => (0.95f, 100f, 35f, "sweep"),
            Core.WeaponType.Scythe => (0.85f, 110f, 40f, "sweep"),

            // Bow - unique (probably shouldn't melee but fallback)
            Core.WeaponType.Bow => (1.2f, 30f, 20f, "thrust"),

            // Default balanced slash
            _ => (1.0f, 90f, 50f, "slash")
        };
    }

    /// <summary>
    /// Get the current weapon's pushback multiplier from WeaponFactory.
    /// </summary>
    private float GetCurrentWeaponPushback()
    {
        // Convert Core.WeaponType to WeaponFactory.WeaponType and get pushback
        var factoryType = MapToFactoryWeaponType(_currentWeaponType);
        var weaponData = SafeRoom3D.Enemies.WeaponFactory.GetWeaponData(factoryType);
        return weaponData.Pushback;
    }

    private void UpdateFirstPersonWeapons(float delta)
    {
        // Update torch flicker
        if (_torchLight != null)
        {
            _torchFlickerTimer += delta * 12f;
            float flicker = 1f + Mathf.Sin(_torchFlickerTimer) * 0.12f +
                            Mathf.Sin(_torchFlickerTimer * 2.7f) * 0.08f +
                            (float)GD.RandRange(-0.03, 0.03);

            _torchLight.LightEnergy = 1.5f * flicker;
            float warmth = 0.7f + Mathf.Sin(_torchFlickerTimer * 0.5f) * 0.03f;
            _torchLight.LightColor = new Color(1f, warmth, 0.35f);
        }

        // Update weapon swing animation - overhauled with wind-up, accelerating swing, impact
        if (_isWeaponSwinging && _weaponNode != null)
        {
            _weaponSwingTimer += delta;

            // Get rest rotation for current weapon type
            var (_, _, restRotation, _) = GetWeaponPositioning(_currentWeaponType);

            // Get weapon-specific animation parameters
            var (speedMult, yArc, zRoll, swingStyle) = GetWeaponAnimationStyle(_currentWeaponType);

            // Animation phases - adjust speed based on weapon type
            float baseSpeed = 1f / speedMult; // Higher speedMult = faster animation
            float windupDuration = 0.05f * baseSpeed;
            float swingDuration = 0.12f * baseSpeed;
            float impactDuration = 0.03f;
            float followDuration = 0.08f * baseSpeed;
            float recoveryDuration = 0.15f * baseSpeed;
            float totalDuration = windupDuration + swingDuration + impactDuration + followDuration + recoveryDuration;

            float elapsed = _weaponSwingTimer;

            // Swing style adjustments for thrusting weapons (dagger, spear)
            bool isThrust = swingStyle == "thrust";
            float windupY = isThrust ? -10 : 15;       // Thrust pulls back on Z, not Y
            float windupZ = isThrust ? 0.1f : 0.05f;   // Thrust pulls back more
            float xTilt = isThrust ? 5 : 15;           // Less X tilt for thrust

            if (elapsed < windupDuration)
            {
                // Wind-up phase: pull weapon back slightly (builds tension)
                float t = elapsed / windupDuration;
                _weaponNode.Position = _weaponRestPos + new Vector3(0, 0, windupZ * t);
                _weaponNode.RotationDegrees = new Vector3(
                    restRotation.X,
                    restRotation.Y + windupY * t,
                    restRotation.Z
                );
            }
            else if (elapsed < windupDuration + swingDuration)
            {
                // Swing phase: fast, ACCELERATING attack (ease-in for power feel)
                float t = (elapsed - windupDuration) / swingDuration;
                t = t * t; // Ease-IN = ACCELERATES toward impact

                if (isThrust)
                {
                    // Thrust: move forward on Z axis
                    _weaponNode.Position = _weaponRestPos + new Vector3(0, 0, Mathf.Lerp(windupZ, -0.3f, t));
                    _weaponNode.RotationDegrees = new Vector3(
                        Mathf.Lerp(restRotation.X, restRotation.X + xTilt, t),
                        Mathf.Lerp(restRotation.Y + windupY, restRotation.Y - yArc * 0.3f, t),
                        restRotation.Z
                    );
                }
                else
                {
                    // Slash: arc movement
                    _weaponNode.Position = _weaponRestPos.Lerp(_weaponSwingPos, t);
                    _weaponNode.RotationDegrees = new Vector3(
                        Mathf.Lerp(restRotation.X, restRotation.X + xTilt, t),
                        Mathf.Lerp(restRotation.Y + windupY, restRotation.Y - yArc, t),
                        Mathf.Lerp(restRotation.Z, restRotation.Z - zRoll, t)
                    );
                }

                // Add camera roll during swing for whoosh feel
                if (_camera != null)
                {
                    float rollIntensity = isThrust ? 1f : 2f;
                    float swingRoll = Mathf.Sin(t * Mathf.Pi) * rollIntensity;
                    _camera.RotationDegrees = new Vector3(_cameraPitch, _camera.RotationDegrees.Y, swingRoll);
                }
            }
            else if (elapsed < windupDuration + swingDuration + impactDuration)
            {
                // Impact pause: brief freeze for visceral hit feel
                if (isThrust)
                {
                    _weaponNode.Position = _weaponRestPos + new Vector3(0, 0, -0.3f);
                    _weaponNode.RotationDegrees = new Vector3(
                        restRotation.X + xTilt,
                        restRotation.Y - yArc * 0.3f,
                        restRotation.Z
                    );
                }
                else
                {
                    _weaponNode.Position = _weaponSwingPos;
                    _weaponNode.RotationDegrees = new Vector3(
                        restRotation.X + xTilt,
                        restRotation.Y - yArc,
                        restRotation.Z - zRoll
                    );
                }
            }
            else if (elapsed < windupDuration + swingDuration + impactDuration + followDuration)
            {
                // Follow-through: continue slightly past impact point
                float t = (elapsed - windupDuration - swingDuration - impactDuration) / followDuration;
                if (isThrust)
                {
                    _weaponNode.Position = _weaponRestPos + new Vector3(0, 0, Mathf.Lerp(-0.3f, -0.35f, t));
                }
                else
                {
                    _weaponNode.Position = _weaponSwingPos + new Vector3(-0.05f * t, -0.02f * t, 0);
                    _weaponNode.RotationDegrees = new Vector3(
                        restRotation.X + xTilt + 5 * t,
                        restRotation.Y - yArc - 10 * t,
                        restRotation.Z - zRoll
                    );
                }
            }
            else if (elapsed < totalDuration)
            {
                // Recovery phase: smooth return to ready position
                float t = (elapsed - windupDuration - swingDuration - impactDuration - followDuration) / recoveryDuration;
                t = 1 - (1 - t) * (1 - t); // Ease out for smooth settle

                if (isThrust)
                {
                    Vector3 thrustEndPos = _weaponRestPos + new Vector3(0, 0, -0.35f);
                    _weaponNode.Position = thrustEndPos.Lerp(_weaponRestPos, t);
                    _weaponNode.RotationDegrees = restRotation;
                }
                else
                {
                    Vector3 followEndPos = _weaponSwingPos + new Vector3(-0.05f, -0.02f, 0);
                    Vector3 followEndRot = new Vector3(restRotation.X + xTilt + 5, restRotation.Y - yArc - 10, restRotation.Z - zRoll);
                    _weaponNode.Position = followEndPos.Lerp(_weaponRestPos, t);
                    _weaponNode.RotationDegrees = followEndRot.Lerp(restRotation, t);
                }

                // Reset camera roll
                if (_camera != null)
                {
                    float resetRoll = (1 - t) * 1f;
                    _camera.RotationDegrees = new Vector3(_cameraPitch, _camera.RotationDegrees.Y, resetRoll);
                }
            }
            else
            {
                // Animation complete
                _isWeaponSwinging = false;
                _weaponSwingTimer = 0;
                _weaponNode.Position = _weaponRestPos;
                _weaponNode.RotationDegrees = restRotation;
                if (_camera != null)
                {
                    _camera.RotationDegrees = new Vector3(_cameraPitch, _camera.RotationDegrees.Y, 0);
                }
            }
        }

        // Update LEFT hand swing animation (for dual wield)
        if (_isLeftHandSwinging && _leftHandNode != null && _leftHandType == LeftHandItemType.Weapon)
        {
            _leftHandSwingTimer += delta;

            // Mirrored animation phases
            float windupDuration = 0.05f;
            float swingDuration = 0.12f;
            float impactDuration = 0.03f;
            float followDuration = 0.08f;
            float recoveryDuration = 0.15f;
            float totalDuration = windupDuration + swingDuration + impactDuration + followDuration + recoveryDuration;

            // Get mirrored rest rotation
            Vector3 restRotation = _leftHandNode.RotationDegrees;

            float elapsed = _leftHandSwingTimer;

            if (elapsed < windupDuration)
            {
                float t = elapsed / windupDuration;
                _leftHandNode.Position = _leftHandRestPos + new Vector3(0, 0, 0.05f * t);
                _leftHandNode.RotationDegrees = new Vector3(restRotation.X, restRotation.Y - 15 * t, restRotation.Z);
            }
            else if (elapsed < windupDuration + swingDuration)
            {
                float t = (elapsed - windupDuration) / swingDuration;
                t = t * t; // Accelerating
                _leftHandNode.Position = _leftHandRestPos.Lerp(_leftHandSwingPos, t);
                _leftHandNode.RotationDegrees = new Vector3(
                    Mathf.Lerp(restRotation.X, restRotation.X + 15, t),
                    Mathf.Lerp(restRotation.Y - 15, restRotation.Y + 90, t), // Mirrored arc
                    Mathf.Lerp(restRotation.Z, restRotation.Z + 50, t)       // Mirrored roll
                );
            }
            else if (elapsed < windupDuration + swingDuration + impactDuration)
            {
                _leftHandNode.Position = _leftHandSwingPos;
            }
            else if (elapsed < windupDuration + swingDuration + impactDuration + followDuration)
            {
                float t = (elapsed - windupDuration - swingDuration - impactDuration) / followDuration;
                _leftHandNode.Position = _leftHandSwingPos + new Vector3(0.05f * t, -0.02f * t, 0);
            }
            else if (elapsed < totalDuration)
            {
                float t = (elapsed - windupDuration - swingDuration - impactDuration - followDuration) / recoveryDuration;
                t = 1 - (1 - t) * (1 - t);
                Vector3 followEndPos = _leftHandSwingPos + new Vector3(0.05f, -0.02f, 0);
                _leftHandNode.Position = followEndPos.Lerp(_leftHandRestPos, t);
            }
            else
            {
                _isLeftHandSwinging = false;
                _leftHandSwingTimer = 0;
                _leftHandNode.Position = _leftHandRestPos;
            }
        }

        // Subtle weapon sway when walking
        if (_torchNode != null && !_isWeaponSwinging)
        {
            float sway = Mathf.Sin(_headBobTime * 2f) * 0.01f;
            _torchNode.Position = new Vector3(-0.5f + sway, -0.65f, -0.35f); // Lowered torch position
        }
        if (_weaponNode != null && !_isWeaponSwinging)
        {
            float sway = Mathf.Sin(_headBobTime * 2f + 1f) * 0.008f;
            _weaponNode.Position = _weaponRestPos + new Vector3(sway, sway * 0.5f, 0);
        }
    }

    /// <summary>
    /// Trigger weapon swing animation (right hand)
    /// </summary>
    public void SwingWeapon()
    {
        if (!_isWeaponSwinging)
        {
            _isWeaponSwinging = true;
            _weaponSwingTimer = 0;
        }
    }

    /// <summary>
    /// Trigger left hand swing animation (dual wield)
    /// </summary>
    public void SwingLeftHand()
    {
        if (!_isLeftHandSwinging && _leftHandType == LeftHandItemType.Weapon)
        {
            _isLeftHandSwinging = true;
            _leftHandSwingTimer = 0;
        }
    }

    /// <summary>
    /// Swing both weapons simultaneously (dual wield hold attack)
    /// </summary>
    public void SwingBothWeapons()
    {
        SwingWeapon();
        SwingLeftHand();
    }

    /// <summary>
    /// Check if player is currently dual wielding
    /// </summary>
    public bool IsDualWielding => _leftHandType == LeftHandItemType.Weapon;

    /// <summary>
    /// Legacy method for backwards compatibility
    /// </summary>
    public void SwingSword() => SwingWeapon();

    public override void _Input(InputEvent @event)
    {
        // PANIC KEY: F5 forces complete input state reset
        // This is the ultimate escape hatch for stuck input
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.F5)
        {
            GD.Print("[FPSController] F5 PANIC KEY: Force closing all UIs and resetting input");
            ForceCloseAllUIs();
            ResetInputState();
            GetViewport().SetInputAsHandled();
            return;
        }

        // FAILSAFE: Click anywhere to recapture mouse when it shouldn't be visible
        // This is the last resort for when mouse gets stuck
        // IMPORTANT: Don't consume the input - let attacks still register
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Captured && !IsAnyUIOpen())
            {
                GD.Print("[FPSController] FAILSAFE: Click-to-recapture triggered (NOT consuming input)");
                Input.MouseMode = Input.MouseModeEnum.Captured;
                // Don't call SetInputAsHandled() - let the attack still process
            }
        }

        // Mouse look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                // Horizontal rotation (yaw) - rotate the whole body
                RotateY(-mouseMotion.Relative.X * Constants.MouseSensitivity);

                // Vertical rotation (pitch) - rotate just the head
                _cameraPitch -= mouseMotion.Relative.Y * Constants.MouseSensitivity;
                _cameraPitch = Mathf.Clamp(_cameraPitch,
                    Mathf.DegToRad(-Constants.MaxLookAngle),
                    Mathf.DegToRad(Constants.MaxLookAngle));

                if (_head != null)
                {
                    _head.Rotation = new Vector3(_cameraPitch, 0, 0);
                }
            }
        }

        // Escape key is handled by GameManager3D for menu
        // Don't handle it here to avoid conflicts

        // R key for target cycling (works outside inspection mode)
        if (@event is InputEventKey rKey && rKey.Pressed && !rKey.Echo &&
            (rKey.Keycode == Key.R || rKey.PhysicalKeycode == Key.R))
        {
            // Don't handle if any UI is open
            bool uiOpen = IsAnyUIOpen();
            GD.Print($"[FPSController] R key pressed - UI open: {uiOpen}");
            if (!uiOpen)
            {
                if (rKey.ShiftPressed)
                {
                    GD.Print("[FPSController] Cycling to previous target");
                    HUD3D.Instance?.CyclePreviousTarget();
                }
                else
                {
                    GD.Print("[FPSController] Cycling to next target");
                    HUD3D.Instance?.CycleNextTarget();
                }
                GetViewport().SetInputAsHandled();
            }
        }

        // Enter key opens floor selector
        if (@event is InputEventKey enterKey && enterKey.Pressed && !enterKey.Echo && enterKey.Keycode == Key.Enter)
        {
            // Don't open if any other UI is open
            if (!IsAnyUIOpen())
            {
                FloorSelector3D.Instance?.ShowSelector();
                GetViewport().SetInputAsHandled();
            }
        }

        // Y key toggles Dungeon Radio
        if (@event is InputEventKey yKey && yKey.Pressed && !yKey.Echo &&
            (yKey.Keycode == Key.Y || yKey.PhysicalKeycode == Key.Y))
        {
            DungeonRadio.Instance?.ToggleUI();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// Force close all UI panels - used by panic key
    /// </summary>
    private void ForceCloseAllUIs()
    {
        // Close overview map
        if (HUD3D.Instance?.IsOverviewMapVisible == true)
        {
            HUD3D.Instance.CloseOverviewMap();
        }

        // Close escape menu
        if (EscapeMenu3D.Instance?.Visible == true)
        {
            EscapeMenu3D.Instance.Hide();
        }

        // Close spellbook
        if (SpellBook3D.Instance?.Visible == true)
        {
            SpellBook3D.Instance.Hide();
        }

        // Close inventory
        if (InventoryUI3D.Instance?.Visible == true)
        {
            InventoryUI3D.Instance.Hide();
        }

        // Close character sheet
        if (CharacterSheetUI.Instance?.Visible == true)
        {
            CharacterSheetUI.Instance.Hide();
        }

        // Close loot UI
        if (LootUI3D.Instance?.Visible == true)
        {
            LootUI3D.Instance.Close();
        }

        // Close floor selector
        if (FloorSelector3D.Instance?.IsOpen == true)
        {
            FloorSelector3D.Instance.HideSelector();
        }

        // Cancel ability targeting
        if (Abilities.AbilityManager3D.Instance?.IsTargeting == true)
        {
            Abilities.AbilityManager3D.Instance.CancelTargeting();
        }

        // Deactivate inspect mode
        if (InspectMode3D.Instance?.IsActive == true)
        {
            InspectMode3D.Instance.Deactivate();
        }

        // Deactivate UI editor mode
        if (UIEditorMode.Instance?.IsActive == true)
        {
            UIEditorMode.Instance.Deactivate();
        }

        // Close dungeon radio
        if (DungeonRadio.Instance?.IsVisible == true)
        {
            DungeonRadio.Instance.ToggleUI();
        }

        // Force unpause
        GetTree().Paused = false;

        // Force GameManager unpause
        if (GameManager3D.Instance != null)
        {
            GameManager3D.Instance.ResumeGame();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // In inspect mode, skip movement but allow camera updates
        if (_isInspectMode)
        {
            // Just ensure we stay still
            Velocity = Vector3.Zero;
            return;
        }

        // Get input direction
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

        // Transform input to world direction based on player rotation
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        // Check sprint
        _isSprinting = Input.IsActionPressed("sprint") && inputDir.Y < 0; // Only sprint forward
        float speed = _isSprinting ? Constants.PlayerSprintSpeed : Constants.PlayerMoveSpeed;

        // Apply movement with acceleration/friction
        if (direction != Vector3.Zero)
        {
            _velocity.X = Mathf.MoveToward(_velocity.X, direction.X * speed, Constants.PlayerAcceleration * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, direction.Z * speed, Constants.PlayerAcceleration * dt);
        }
        else
        {
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, Constants.PlayerFriction * dt);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, Constants.PlayerFriction * dt);
        }

        // Gravity
        if (!IsOnFloor())
        {
            _velocity.Y -= Constants.Gravity * dt;
        }

        // Jump
        if (Input.IsActionJustPressed("jump") && IsOnFloor())
        {
            _velocity.Y = Constants.PlayerJumpVelocity;
        }

        // Apply velocity
        Velocity = _velocity;
        MoveAndSlide();

        // Update head bob when moving
        if (IsOnFloor() && direction != Vector3.Zero)
        {
            float bobSpeed = _isSprinting ? 14f : 10f;
            float bobAmount = _isSprinting ? 0.08f : 0.04f;
            _headBobTime += dt * bobSpeed;
            if (_head != null)
            {
                float bobY = Mathf.Sin(_headBobTime) * bobAmount;
                float bobX = Mathf.Sin(_headBobTime * 0.5f) * bobAmount * 0.5f;
                _head.Position = new Vector3(bobX, Constants.PlayerEyeHeight + bobY, 0);
            }
        }
        else
        {
            // Reset head position when not moving
            if (_head != null)
            {
                _head.Position = _head.Position.Lerp(new Vector3(0, Constants.PlayerEyeHeight, 0), dt * 10f);
            }
        }

        // FOV change when sprinting
        _targetFov = _isSprinting ? Constants.CameraFov + 10f : Constants.CameraFov;
        _currentFov = Mathf.Lerp(_currentFov, _targetFov, dt * 8f);
        if (_camera != null)
        {
            _camera.Fov = _currentFov;
        }

        // Process screen shake
        ProcessScreenShake(dt);

        // Update first-person weapons (torch flicker, sword animation)
        UpdateFirstPersonWeapons(dt);

        // Process attack cooldown
        if (_attackCooldown > 0)
        {
            _attackCooldown -= dt;
        }

        // Attack input - only allow if not in ability targeting mode, menus, or edit mode
        bool isTargeting = AbilityManager3D.Instance?.IsTargeting ?? false;
        bool isInMenu = Input.MouseMode == Input.MouseModeEnum.Visible;

        // Attack system - handles both single weapon and dual wield
        // Single weapon: hold for strong attack, quick release for normal
        // Dual wield: quick release alternates hands, hold 0.15s for simultaneous
        if (Input.IsActionPressed("attack") && _attackCooldown <= 0 && !isTargeting && !_isInspectMode && !_isEditMode && !isInMenu)
        {
            if (!_isChargingAttack)
            {
                // Start charging/holding
                _isChargingAttack = true;
                _chargeTimer = 0f;
                _attackHoldTimer = 0f;
                if (!IsDualWielding)
                {
                    ShowChargeIndicator();
                }
            }
            else
            {
                // Continue charging
                _chargeTimer += dt;
                _attackHoldTimer += dt;

                // For dual wield: trigger simultaneous attack after hold threshold
                if (IsDualWielding && _attackHoldTimer >= AttackHoldThreshold && !_isWeaponSwinging && !_isLeftHandSwinging)
                {
                    PerformDualWieldSimultaneousAttack();
                    _isChargingAttack = false;
                    _attackHoldTimer = 0f;
                }
                else if (!IsDualWielding)
                {
                    UpdateChargeIndicator();
                }
            }
        }
        else if (_isChargingAttack)
        {
            // Cancel charge if menu opened
            if (isInMenu)
            {
                _isChargingAttack = false;
                _attackHoldTimer = 0f;
                HideChargeIndicator();
            }
            else
            {
                // Released attack button - perform attack based on context
                if (IsDualWielding)
                {
                    // Dual wield: quick release = alternating attack
                    if (_attackHoldTimer < AttackHoldThreshold)
                    {
                        PerformDualWieldAlternatingAttack();
                    }
                    // If held past threshold, attack already performed above
                }
                else
                {
                    // Single weapon: charged attack system
                    if (_chargeTimer >= StrongAttackChargeTime)
                    {
                        PerformStrongAttack();
                    }
                    else
                    {
                        PerformMeleeAttack();
                    }
                }
                _isChargingAttack = false;
                _attackHoldTimer = 0f;
                HideChargeIndicator();
            }
        }

        if (Input.IsActionJustPressed("attack_alt") && _attackCooldown <= 0 && !isInMenu && !_isEditMode)
        {
            PerformRangedAttack();
        }

        // Interact input
        if (Input.IsActionJustPressed("interact"))
        {
            TryInteract();
        }

        // Loot/Interact input (T key) - check NPCs first, then corpses
        if (Input.IsActionJustPressed("loot"))
        {
            // NPCs take priority over corpses
            if (!TryInteractNearbyNPC())
            {
                TryLootNearbyCorpse();
            }
        }

        // Safety: check if fallen through world
        CheckFallSafety(dt);

        // Safety: ensure mouse mode is captured when no UI is open
        CheckMouseMode();

        // Periodic health check for input system
        PerformInputHealthCheck(dt);

        // Process health and mana regeneration
        ProcessRegeneration(dt);
    }

    private int _mouseRecaptureFrames; // Count frames with visible mouse when no UI open
    private bool _lastMouseCaptured = true; // Track state to avoid log spam
    private float _inputHealthCheckTimer = 0; // Timer for periodic input state validation

    private void CheckMouseMode()
    {
        // Check if any UI is genuinely open
        bool uiOpen = IsAnyUIOpen();
        bool isCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;

        if (!uiOpen && !isCaptured)
        {
            _mouseRecaptureFrames++;

            // Only recapture after a few frames to avoid fighting with UI transitions
            // This gives UI systems time to complete their show/hide operations
            if (_mouseRecaptureFrames >= 3)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;

                // Only log on first successful recapture attempt
                if (_mouseRecaptureFrames == 3)
                {
                    GD.Print($"[FPSController] Mouse uncaptured with no UI open - recapturing");
                }
            }
            _lastMouseCaptured = false;
        }
        else if (isCaptured)
        {
            if (_mouseRecaptureFrames >= 3 && !_lastMouseCaptured)
            {
                GD.Print($"[FPSController] Mouse recaptured after {_mouseRecaptureFrames} frames");
            }
            _mouseRecaptureFrames = 0;
            _lastMouseCaptured = true;
        }
        else
        {
            // UI is open, mouse should be visible - reset counter
            _mouseRecaptureFrames = 0;
            _lastMouseCaptured = false;
        }
    }

    /// <summary>
    /// Force a complete input state reset. Call this to recover from stuck input states.
    /// </summary>
    public void ResetInputState()
    {
        // Reset mouse mode based on current UI state
        if (!IsAnyUIOpen())
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GD.Print("[FPSController] Input state reset - mouse recaptured");
        }
        else
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GD.Print("[FPSController] Input state reset - mouse visible (UI open)");
        }

        // Reset internal state tracking
        _mouseRecaptureFrames = 0;
        _lastMouseCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;
        MouseControlLocked = false;
    }

    /// <summary>
    /// Periodic health check to ensure input system is working correctly.
    /// Called every few seconds to catch and fix edge cases.
    /// </summary>
    private void PerformInputHealthCheck(float dt)
    {
        _inputHealthCheckTimer += dt;

        // Check every 1 second for faster recovery
        if (_inputHealthCheckTimer < 1f) return;
        _inputHealthCheckTimer = 0;

        // Check each UI explicitly to see if any are open
        bool mapOpen = HUD3D.Instance?.IsOverviewMapVisible == true;
        bool escapeOpen = EscapeMenu3D.Instance?.Visible == true;
        bool spellbookOpen = SpellBook3D.Instance?.Visible == true;
        bool inventoryOpen = InventoryUI3D.Instance?.Visible == true;
        bool characterSheetOpen = CharacterSheetUI.Instance?.Visible == true;
        bool lootOpen = LootUI3D.Instance?.Visible == true;
        bool editorOpen = EditorScreen3D.Instance?.Visible == true;
        bool isTargeting = Abilities.AbilityManager3D.Instance?.IsTargeting == true;
        bool inMapEditorSelectorOpen = InMapEditor.Instance?.IsSelectorPaneOpen == true;

        bool anyUIOpen = mapOpen || escapeOpen || spellbookOpen || inventoryOpen || characterSheetOpen || lootOpen || editorOpen || isTargeting || inMapEditorSelectorOpen;
        bool isCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;

        // If no UI is open but mouse isn't captured, that's a problem - fix it
        if (!anyUIOpen && !isCaptured)
        {
            GD.Print("[FPSController] Health check: Mouse should be captured but isn't - forcing recapture");
            Input.MouseMode = Input.MouseModeEnum.Captured;
            _mouseRecaptureFrames = 0;
            MouseControlLocked = false; // Also unlock mouse control
        }

        // If MouseControlLocked is true but no targeting is happening, unlock it
        if (MouseControlLocked && !isTargeting && !lootOpen)
        {
            GD.Print("[FPSController] Health check: MouseControlLocked without targeting/loot - unlocking");
            MouseControlLocked = false;
        }

        // If game is paused but no pause-requiring UI is open, unpause
        bool inspectModeActive = InspectMode3D.Instance?.IsActive == true;
        bool shouldBePaused = mapOpen || escapeOpen || inspectModeActive;
        if (GetTree().Paused && !shouldBePaused)
        {
            GD.Print("[FPSController] Health check: Game paused without UI - unpausing");
            GetTree().Paused = false;
        }

        // If game should be paused but isn't (map/escape open), pause it
        if (!GetTree().Paused && shouldBePaused)
        {
            GD.Print("[FPSController] Health check: Pause UI open but game not paused - pausing");
            GetTree().Paused = true;
        }

        // If TimeScale is 0 but nothing needs it paused (no targeting, no floor selector), reset it
        // This catches cases where targeting was interrupted abnormally
        bool floorSelectorOpen = FloorSelector3D.Instance?.IsOpen == true;
        if (Engine.TimeScale < 0.01 && !isTargeting && !floorSelectorOpen)
        {
            GD.Print("[FPSController] Health check: TimeScale stuck at 0 - resetting to game speed");
            GameConfig.ResumeToNormalSpeed();
        }
    }

    /// <summary>
    /// Check if any UI panel is open that requires the mouse to be visible.
    /// This is the AUTHORITATIVE check - all mouse mode decisions should use this.
    /// </summary>
    public static bool IsAnyUIOpen()
    {
        // If mouse control is locked by another system (like AbilityManager during targeting)
        if (Instance?.MouseControlLocked == true) return true;

        // Ability targeting mode
        if (Abilities.AbilityManager3D.Instance?.IsTargeting == true) return true;

        // All UI panels
        if (SpellBook3D.Instance?.Visible == true) return true;
        if (InventoryUI3D.Instance?.Visible == true) return true;
        if (CharacterSheetUI.Instance?.Visible == true) return true;
        if (LootUI3D.Instance?.Visible == true) return true;
        if (EscapeMenu3D.Instance?.Visible == true) return true;
        if (EditorScreen3D.Instance?.Visible == true) return true;
        if (FloorSelector3D.Instance?.IsOpen == true) return true;
        if (DungeonRadio.Instance?.IsVisible == true) return true;

        // In-Map Editor selector pane (Z-key menu)
        if (InMapEditor.Instance?.IsSelectorPaneOpen == true) return true;

        // Overview map (world map screen)
        if (HUD3D.Instance?.IsOverviewMapVisible == true) return true;

        // Inspect mode (paused for target inspection)
        if (InspectMode3D.Instance?.IsActive == true) return true;

        // Game state
        if (GameManager3D.Instance?.IsGameOver == true) return true;
        if (GameManager3D.Instance?.IsPaused == true) return true;

        return false;
    }

    /// <summary>
    /// Force recapture mouse - call this when closing UI or after spell casting.
    /// </summary>
    public void ForceRecaptureMouse()
    {
        if (!IsAnyUIOpen())
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GD.Print("[FPSController] Force recaptured mouse");
        }
    }

    private void CheckFallSafety(float dt)
    {
        // Update safe position periodically when on floor
        if (IsOnFloor())
        {
            _safePositionTimer += dt;
            if (_safePositionTimer >= SafePositionInterval)
            {
                _lastSafePosition = GlobalPosition;
                _safePositionTimer = 0f;
            }
        }

        // Respawn if fallen too far
        if (GlobalPosition.Y < FallDeathHeight)
        {
            GD.Print($"[FPSController] Fell through world! Respawning at last safe position: {_lastSafePosition}");
            GlobalPosition = _lastSafePosition + new Vector3(0, 0.5f, 0); // Slightly above
            _velocity = Vector3.Zero;
        }
    }

    private void ProcessScreenShake(float dt)
    {
        if (_camera == null) return;

        if (_shakeTimer > 0)
        {
            _shakeTimer -= dt;
            float progress = _shakeTimer / Constants.ScreenShakeDuration;
            float currentIntensity = _shakeIntensity * progress;

            _shakeOffset = new Vector2(
                (float)GD.RandRange(-currentIntensity, currentIntensity),
                (float)GD.RandRange(-currentIntensity, currentIntensity)
            );

            // Apply shake as offset from base rotation (not cumulative)
            _camera.Rotation = new Vector3(_shakeOffset.Y, _shakeOffset.X, 0);
        }
        else
        {
            // Ensure camera is level when not shaking (no roll/tilt)
            _camera.Rotation = Vector3.Zero;
        }
    }

    private void PerformMeleeAttack()
    {
        _attackCooldown = Constants.AttackCooldown;
        _isAttacking = true;
        _attackTimer = Constants.AttackWindup;

        EmitSignal(SignalName.Attacked);

        // Trigger first-person sword swing animation
        SwingSword();

        // Play attack sound
        SoundManager3D.Instance?.PlayAttackSound();

        // Create 3D sword slash visual effect
        if (_camera != null)
        {
            MeleeSlashEffect3D.Create(_camera);
        }

        // Use sphere cast for wider melee hit detection
        bool hitSomething = false;

        // Check all enemies in range
        var enemies = GetTree().GetNodesInGroup("Enemies");
        Vector3 attackOrigin = _camera?.GlobalPosition ?? GlobalPosition + new Vector3(0, Constants.PlayerEyeHeight, 0);
        Vector3 attackDir = GetLookDirection();

        foreach (var node in enemies)
        {
            if (node is Node3D enemy3D)
            {
                Vector3 toEnemy = enemy3D.GlobalPosition - attackOrigin;
                // Use horizontal distance (XZ plane) for melee range - ignore height difference
                float horizontalDistance = new Vector2(toEnemy.X, toEnemy.Z).Length();

                // Check if within range (using horizontal distance for fairness)
                if (horizontalDistance > Constants.AttackRange + 1.5f) continue;

                // Check if roughly in front (dot product > 0.5 means within ~60 degree cone)
                float dot = toEnemy.Normalized().Dot(attackDir);
                if (dot > 0.3f) // Wide cone for melee
                {
                    if (enemy3D.HasMethod("TakeDamage"))
                    {
                        // Use CharacterStats for damage calculation
                        var (damage, isCrit) = Stats.CalculateMeleeDamage();
                        float pushback = GetCurrentWeaponPushback();
                        enemy3D.Call("TakeDamage", (float)damage, GlobalPosition, "Melee", isCrit, pushback);
                        hitSomething = true;

                        // Record damage dealt in game stats
                        string targetType = GetEnemyDisplayName(enemy3D);
                        GameStats.Instance?.RecordDamageDealt(damage, targetType);
                        GameStats.Instance?.RecordMeleeAttack();
                        if (isCrit) GameStats.Instance?.RecordCriticalHit();

                        // Life on hit
                        if (Stats.LifeOnHit > 0)
                        {
                            Heal(Stats.LifeOnHit);
                        }
                        if (Stats.ManaOnHit > 0)
                        {
                            RestoreMana(Stats.ManaOnHit);
                        }

                        // Log to combat log
                        string enemyName = targetType;
                        string attackType = isCrit ? "critical melee" : "melee";
                        HUD3D.Instance?.LogPlayerDamage(enemyName, damage, attackType);

                        GD.Print($"[FPSController] Melee hit {enemy3D.Name} for {damage}{(isCrit ? " (CRIT!)" : "")}!");
                    }
                }
            }
        }

        if (hitSomething)
        {
            RequestScreenShake(Constants.ScreenShakeDuration, Constants.ScreenShakeIntensity);
            SoundManager3D.Instance?.PlayHitSound(GetCameraPosition());
        }

        GD.Print("[FPSController] Melee attack!");
    }

    /// <summary>
    /// Dual wield alternating attack - swings one weapon at a time, alternating hands.
    /// Triggered by quick clicks while dual wielding.
    /// </summary>
    private void PerformDualWieldAlternatingAttack()
    {
        _attackCooldown = Constants.AttackCooldown * 0.7f; // Faster attacks when dual wielding
        _isAttacking = true;
        _attackTimer = Constants.AttackWindup;

        EmitSignal(SignalName.Attacked);

        // Alternate between right and left hand
        if (_lastSwingWasRight)
        {
            SwingLeftHand();
            _lastSwingWasRight = false;
        }
        else
        {
            SwingWeapon();
            _lastSwingWasRight = true;
        }

        // Play attack sound
        SoundManager3D.Instance?.PlayAttackSound();

        // Create slash effect
        if (_camera != null)
        {
            MeleeSlashEffect3D.Create(_camera);
        }

        // Hit detection (same as normal melee but slightly reduced damage per hit)
        bool hitSomething = false;
        var enemies = GetTree().GetNodesInGroup("Enemies");
        Vector3 attackOrigin = _camera?.GlobalPosition ?? GlobalPosition + new Vector3(0, Constants.PlayerEyeHeight, 0);
        Vector3 attackDir = GetLookDirection();

        foreach (var node in enemies)
        {
            if (node is Node3D enemy3D)
            {
                Vector3 toEnemy = enemy3D.GlobalPosition - attackOrigin;
                float horizontalDistance = new Vector2(toEnemy.X, toEnemy.Z).Length();

                if (horizontalDistance > Constants.AttackRange + 1.5f) continue;

                float dot = toEnemy.Normalized().Dot(attackDir);
                if (dot > 0.3f)
                {
                    if (enemy3D.HasMethod("TakeDamage"))
                    {
                        var (damage, isCrit) = Stats.CalculateMeleeDamage();
                        // Dual wield single swing = 80% damage but faster overall DPS
                        damage = (int)(damage * 0.8f);
                        float pushback = GetCurrentWeaponPushback() * 0.8f; // Slightly less pushback for lighter swing
                        enemy3D.Call("TakeDamage", (float)damage, GlobalPosition, "Dual Wield", isCrit, pushback);
                        hitSomething = true;

                        string targetType = GetEnemyDisplayName(enemy3D);
                        GameStats.Instance?.RecordDamageDealt(damage, targetType);
                        GameStats.Instance?.RecordMeleeAttack();
                        if (isCrit) GameStats.Instance?.RecordCriticalHit();

                        if (Stats.LifeOnHit > 0) Heal(Stats.LifeOnHit);
                        if (Stats.ManaOnHit > 0) RestoreMana(Stats.ManaOnHit);

                        string attackType = isCrit ? "critical dual wield" : "dual wield";
                        HUD3D.Instance?.LogPlayerDamage(targetType, damage, attackType);
                        GD.Print($"[FPSController] Dual wield hit {enemy3D.Name} for {damage}{(isCrit ? " (CRIT!)" : "")}!");
                    }
                }
            }
        }

        if (hitSomething)
        {
            RequestScreenShake(Constants.ScreenShakeDuration * 0.7f, Constants.ScreenShakeIntensity * 0.8f);
            SoundManager3D.Instance?.PlayHitSound(GetCameraPosition());
        }

        GD.Print($"[FPSController] Dual wield alternating attack ({(_lastSwingWasRight ? "right" : "left")} next)!");
    }

    /// <summary>
    /// Dual wield simultaneous attack - both weapons swing together.
    /// Triggered by holding attack button while dual wielding.
    /// </summary>
    private void PerformDualWieldSimultaneousAttack()
    {
        _attackCooldown = Constants.AttackCooldown * 1.2f; // Slightly longer cooldown for power move
        _isAttacking = true;
        _attackTimer = Constants.AttackWindup;

        EmitSignal(SignalName.Attacked);

        // Swing both weapons
        SwingBothWeapons();

        // Play attack sound (could be louder/different for simultaneous)
        SoundManager3D.Instance?.PlayAttackSound();
        SoundManager3D.Instance?.PlaySound("attack_alt"); // Second sound for power feel

        // Create slash effect (maybe two?)
        if (_camera != null)
        {
            MeleeSlashEffect3D.Create(_camera);
        }

        // Hit detection with bonus damage for simultaneous strike
        bool hitSomething = false;
        var enemies = GetTree().GetNodesInGroup("Enemies");
        Vector3 attackOrigin = _camera?.GlobalPosition ?? GlobalPosition + new Vector3(0, Constants.PlayerEyeHeight, 0);
        Vector3 attackDir = GetLookDirection();

        foreach (var node in enemies)
        {
            if (node is Node3D enemy3D)
            {
                Vector3 toEnemy = enemy3D.GlobalPosition - attackOrigin;
                float horizontalDistance = new Vector2(toEnemy.X, toEnemy.Z).Length();

                if (horizontalDistance > Constants.AttackRange + 1.5f) continue;

                float dot = toEnemy.Normalized().Dot(attackDir);
                if (dot > 0.3f)
                {
                    if (enemy3D.HasMethod("TakeDamage"))
                    {
                        var (damage, isCrit) = Stats.CalculateMeleeDamage();
                        // Simultaneous strike = 160% damage (both weapons hit)
                        damage = (int)(damage * 1.6f);
                        float pushback = GetCurrentWeaponPushback() * 1.5f; // Big pushback for dual strike
                        enemy3D.Call("TakeDamage", (float)damage, GlobalPosition, "Dual Strike", isCrit, pushback);
                        hitSomething = true;

                        string targetType = GetEnemyDisplayName(enemy3D);
                        GameStats.Instance?.RecordDamageDealt(damage, targetType);
                        GameStats.Instance?.RecordMeleeAttack();
                        GameStats.Instance?.RecordMeleeAttack(); // Count as 2 attacks
                        if (isCrit) GameStats.Instance?.RecordCriticalHit();

                        if (Stats.LifeOnHit > 0) Heal(Stats.LifeOnHit * 2); // Double life on hit
                        if (Stats.ManaOnHit > 0) RestoreMana(Stats.ManaOnHit * 2);

                        string attackType = isCrit ? "critical DUAL STRIKE" : "DUAL STRIKE";
                        HUD3D.Instance?.LogPlayerDamage(targetType, damage, attackType);
                        GD.Print($"[FPSController] DUAL STRIKE hit {enemy3D.Name} for {damage}{(isCrit ? " (CRIT!)" : "")}!");
                    }
                }
            }
        }

        if (hitSomething)
        {
            // Bigger impact for simultaneous attack
            RequestScreenShake(Constants.ScreenShakeDuration * 1.5f, Constants.ScreenShakeIntensity * 1.3f);
            SoundManager3D.Instance?.PlayHitSound(GetCameraPosition());
        }

        GD.Print("[FPSController] DUAL WIELD SIMULTANEOUS ATTACK!");
    }

    private void PerformStrongAttack()
    {
        _attackCooldown = Constants.AttackCooldown * 1.5f; // Longer cooldown for strong attack
        _isAttacking = true;
        _attackTimer = Constants.AttackWindup;

        EmitSignal(SignalName.Attacked);

        // Trigger first-person sword swing animation
        SwingSword();

        // Play attack sound (louder/different)
        SoundManager3D.Instance?.PlaySound("attack_alt");

        // Create 3D sword slash visual effect - larger for strong attack
        if (_camera != null)
        {
            MeleeSlashEffect3D.CreateStrongAttack(_camera);
        }

        // Use sphere cast for wider melee hit detection
        bool hitSomething = false;
        // Strong attack uses CharacterStats damage with multiplier
        var (baseDamage, isCrit) = Stats.CalculateMeleeDamage();
        int strongDamage = (int)(baseDamage * StrongAttackDamageMultiplier);

        // Check all enemies in range - wider arc for strong attack
        var enemies = GetTree().GetNodesInGroup("Enemies");
        Vector3 attackOrigin = _camera?.GlobalPosition ?? GlobalPosition + new Vector3(0, Constants.PlayerEyeHeight, 0);
        Vector3 attackDir = GetLookDirection();

        foreach (var node in enemies)
        {
            if (node is Node3D enemy3D)
            {
                Vector3 toEnemy = enemy3D.GlobalPosition - attackOrigin;
                // Use horizontal distance (XZ plane) for melee range - ignore height difference
                float horizontalDistance = new Vector2(toEnemy.X, toEnemy.Z).Length();

                // Check if within range (slightly extended for strong attack)
                if (horizontalDistance > Constants.AttackRange + 2f) continue;

                // Check if roughly in front - wider cone for strong attack
                float dot = toEnemy.Normalized().Dot(attackDir);
                if (dot > 0.2f) // Even wider cone for strong attack
                {
                    if (enemy3D.HasMethod("TakeDamage"))
                    {
                        // Strong attacks get extra pushback multiplier
                        float pushback = GetCurrentWeaponPushback() * StrongAttackKnockbackMultiplier;
                        enemy3D.Call("TakeDamage", (float)strongDamage, GlobalPosition, "StrongMelee", isCrit, pushback);
                        hitSomething = true;

                        // Record damage dealt in game stats
                        string targetType = GetEnemyDisplayName(enemy3D);
                        GameStats.Instance?.RecordDamageDealt(strongDamage, targetType);
                        GameStats.Instance?.RecordMeleeAttack();
                        if (isCrit) GameStats.Instance?.RecordCriticalHit();

                        // Life on hit
                        if (Stats.LifeOnHit > 0)
                        {
                            Heal(Stats.LifeOnHit);
                        }
                        if (Stats.ManaOnHit > 0)
                        {
                            RestoreMana(Stats.ManaOnHit);
                        }

                        // Log to combat log
                        string enemyName = targetType;
                        string attackType = isCrit ? "critical strong melee" : "strong melee";
                        HUD3D.Instance?.LogPlayerDamage(enemyName, strongDamage, attackType);

                        GD.Print($"[FPSController] Strong attack hit {enemy3D.Name} for {strongDamage}{(isCrit ? " (CRIT!)" : "")}!");
                    }
                }
            }
        }

        if (hitSomething)
        {
            // Bigger screen shake for strong attack
            RequestScreenShake(Constants.ScreenShakeDuration * 2f, Constants.ScreenShakeIntensity * 2f);
            SoundManager3D.Instance?.PlaySound("hit_heavy");
        }

        GD.Print("[FPSController] STRONG ATTACK!");
    }

    private void ShowChargeIndicator()
    {
        if (_chargeIndicator != null) return;
        if (_camera == null) return;

        // Create a glowing sphere that grows as you charge
        _chargeIndicator = new MeshInstance3D();
        var sphere = new SphereMesh { Radius = 0.02f, Height = 0.04f };
        _chargeIndicator.Mesh = sphere;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.8f, 0.2f, 0.5f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.6f, 0.1f);
        mat.EmissionEnergyMultiplier = 2f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _chargeIndicator.MaterialOverride = mat;

        // Position near sword
        _chargeIndicator.Position = new Vector3(0.25f, -0.15f, -0.5f);
        _camera.AddChild(_chargeIndicator);
    }

    private void UpdateChargeIndicator()
    {
        if (_chargeIndicator == null) return;

        float chargePercent = Mathf.Clamp(_chargeTimer / StrongAttackChargeTime, 0f, 1f);

        // Scale up as charge increases
        float scale = 1f + chargePercent * 3f;
        _chargeIndicator.Scale = new Vector3(scale, scale, scale);

        // Change color from orange to bright yellow when fully charged
        if (_chargeIndicator.MaterialOverride is StandardMaterial3D mat)
        {
            if (chargePercent >= 1f)
            {
                mat.AlbedoColor = new Color(1f, 1f, 0.3f, 0.7f);
                mat.Emission = new Color(1f, 1f, 0.3f);
                mat.EmissionEnergyMultiplier = 4f;
            }
            else
            {
                mat.AlbedoColor = new Color(1f, 0.5f + chargePercent * 0.3f, 0.2f, 0.3f + chargePercent * 0.4f);
                mat.Emission = new Color(1f, 0.5f + chargePercent * 0.3f, 0.1f);
                mat.EmissionEnergyMultiplier = 2f + chargePercent * 2f;
            }
        }

        // Slight vibration when fully charged
        if (chargePercent >= 1f)
        {
            float shake = Mathf.Sin((float)Time.GetTicksMsec() * 0.05f) * 0.01f;
            _chargeIndicator.Position = new Vector3(0.25f + shake, -0.15f + shake, -0.5f);
        }
    }

    private void HideChargeIndicator()
    {
        if (_chargeIndicator != null)
        {
            _chargeIndicator.QueueFree();
            _chargeIndicator = null;
        }
    }

    private void PerformRangedAttack()
    {
        if (CurrentMana < 5)
        {
            GD.Print("[FPSController] Not enough mana!");
            return;
        }

        _attackCooldown = Constants.RangedCooldown;
        UseMana(5);

        if (_camera != null)
        {
            // Spawn projectile from camera position forward
            var spawnPos = _camera.GlobalPosition + GetLookDirection() * 0.5f; // Slightly in front
            var direction = GetLookDirection();

            var projectile = Projectile3D.Create(
                spawnPos,
                direction,
                Constants.RangedDamage,
                isPlayerProjectile: true,
                color: new Color(0.4f, 0.7f, 1f),
                source: "Player"
            );

            // Add to scene tree
            GetTree().Root.AddChild(projectile);
            projectile.Fire(direction);

            // Play magic sound
            SoundManager3D.Instance?.PlayMagicSound(spawnPos);

            GD.Print($"[FPSController] Ranged attack! Direction: {direction}");
        }

        EmitSignal(SignalName.Attacked);
    }

    private void TryInteract()
    {
        if (_interactRay != null && _interactRay.IsColliding())
        {
            var target = _interactRay.GetCollider() as Node3D;
            if (target != null && target.HasMethod("Interact"))
            {
                target.Call("Interact", this);
                GD.Print($"[FPSController] Interacted with: {target.Name}");
            }
        }
    }

    /// <summary>
    /// Try to interact with a nearby shopkeeper NPC.
    /// Returns true if an NPC was found and interacted with.
    /// </summary>
    private bool TryInteractNearbyNPC()
    {
        const float InteractRange = 3.5f;
        NPC.BaseNPC3D? nearestNPC = null;
        float nearestDist = InteractRange;

        var shopkeepers = GetTree().GetNodesInGroup("Shopkeepers");
        foreach (var node in shopkeepers)
        {
            if (node is NPC.BaseNPC3D npc && IsInstanceValid(npc))
            {
                float dist = GlobalPosition.DistanceTo(npc.GlobalPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestNPC = npc;
                }
            }
        }

        if (nearestNPC != null)
        {
            GD.Print($"[FPSController] Interacting with NPC: {nearestNPC.NPCName}");
            nearestNPC.Interact(this);
            return true;
        }

        return false;
    }

    private void TryLootNearbyCorpse()
    {
        // Find nearest corpse within loot range
        const float LootRange = 3f;
        Enemies.Corpse3D? nearestCorpse = null;
        float nearestDist = LootRange;

        var corpses = GetTree().GetNodesInGroup("Corpses");
        foreach (var node in corpses)
        {
            if (node is Enemies.Corpse3D corpse && !corpse.HasBeenLooted)
            {
                float dist = GlobalPosition.DistanceTo(corpse.GlobalPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestCorpse = corpse;
                }
            }
        }

        if (nearestCorpse != null)
        {
            nearestCorpse.Interact();
        }
        else
        {
            GD.Print("[FPSController] No corpses in range to loot");
        }
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source = "Unknown")
    {
        // Cancel any active ability targeting when taking damage
        // This ensures the player can respond to threats
        if (Abilities.AbilityManager3D.Instance?.IsTargeting == true)
        {
            Abilities.AbilityManager3D.Instance.CancelTargeting();
        }

        int rawDamage = (int)damage;

        // Use CharacterStats for damage reduction (armor, block, dodge)
        bool canBlock = Equipment.GetEquippedItem(EquipmentSlot.OffHand) != null;
        var (finalDamage, blocked, dodged) = Stats.CalculateIncomingDamage(rawDamage, canBlock, canDodge: true);

        if (dodged)
        {
            // Full dodge - no damage
            HUD3D.Instance?.AddCombatLogMessage($"Dodged {source}!", new Color(0.3f, 1f, 0.3f));
            SoundManager3D.Instance?.PlaySound("whoosh");
            GD.Print($"[FPSController] Dodged attack from {source}!");
            return;
        }

        if (blocked)
        {
            HUD3D.Instance?.AddCombatLogMessage($"Blocked {source} ({rawDamage} -> {finalDamage})", new Color(0.3f, 0.8f, 1f));
            SoundManager3D.Instance?.PlaySound("hit"); // Block sound
            GD.Print($"[FPSController] Blocked attack from {source}: {rawDamage} -> {finalDamage}");
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - finalDamage);

        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        RequestScreenShake(Constants.DefaultScreenShakeDuration, Constants.DefaultScreenShakeIntensity);

        // Log to combat log with armor reduction info
        string logMessage = blocked
            ? $"{source} (blocked): {finalDamage}"
            : $"{source}: {finalDamage}";
        if (rawDamage != finalDamage && !blocked)
        {
            logMessage = $"{source}: {finalDamage} ({rawDamage} - armor)";
        }
        HUD3D.Instance?.LogPlayerTakeDamage(source, finalDamage);

        // Play hurt sound
        SoundManager3D.Instance?.PlayPlayerHitSound();

        GD.Print($"[FPSController] Took {finalDamage} damage from {source} (raw: {rawDamage}). Health: {CurrentHealth}/{MaxHealth}");

        // Notify AI broadcaster of damage
        float healthPercent = (float)CurrentHealth / MaxHealth;
        AIBroadcaster.Instance?.OnPlayerDamaged(healthPercent);

        // Record damage taken in game stats
        GameStats.Instance?.RecordDamageTaken(finalDamage);

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        GD.Print($"[FPSController] Healed {amount}. Health: {CurrentHealth}/{MaxHealth}");
    }

    public bool UseMana(int amount)
    {
        if (CurrentMana < amount) return false;
        CurrentMana -= amount;
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
        return true;
    }

    public void RestoreMana(int amount)
    {
        CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
    }

    /// <summary>
    /// Add experience points. Automatically handles level ups.
    /// </summary>
    public void AddExperience(int amount)
    {
        CurrentExperience += amount;
        GD.Print($"[FPSController] Gained {amount} XP. Total: {CurrentExperience}/{ExperienceToLevel}");

        // Check for level up
        while (CurrentExperience >= ExperienceToLevel)
        {
            CurrentExperience -= ExperienceToLevel;
            LevelUp();
        }

        EmitSignal(SignalName.ExperienceChanged, CurrentExperience, ExperienceToLevel, PlayerLevel);
    }

    private void LevelUp()
    {
        PlayerLevel++;

        // Update CharacterStats level (grants attribute points)
        int oldMaxHealth = Stats.MaxHealth;
        int oldMaxMana = Stats.MaxMana;
        Stats.SetLevel(PlayerLevel);

        int healthBonus = Stats.MaxHealth - oldMaxHealth;
        int manaBonus = Stats.MaxMana - oldMaxMana;

        // Full heal on level up
        CurrentHealth = Stats.MaxHealth;
        CurrentMana = Stats.MaxMana;

        EmitSignal(SignalName.LeveledUp, PlayerLevel);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);

        GD.Print($"[FPSController] LEVEL UP! Now level {PlayerLevel}. HP: {MaxHealth} (+{healthBonus}), Mana: {MaxMana} (+{manaBonus})");

        // Play level up sound
        SoundManager3D.Instance?.PlaySound("powerup");
    }

    /// <summary>
    /// Reset health and mana to full (used for restart)
    /// </summary>
    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        CurrentMana = MaxMana;
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
    }

    /// <summary>
    /// Get a display-friendly name for an enemy node
    /// </summary>
    private string GetEnemyDisplayName(Node3D enemy)
    {
        // Check specific enemy types directly (more reliable than Godot reflection)
        if (enemy is SafeRoom3D.Enemies.BasicEnemy3D basicEnemy)
        {
            return FormatMonsterName(basicEnemy.MonsterType);
        }
        if (enemy is SafeRoom3D.Enemies.GoblinShaman)
        {
            return "Goblin Shaman";
        }
        if (enemy is SafeRoom3D.Enemies.GoblinThrower)
        {
            return "Goblin Thrower";
        }
        if (enemy is SafeRoom3D.Enemies.BossEnemy3D bossEnemy)
        {
            return bossEnemy.BossName;
        }

        // Try to get monster type property via Godot reflection (fallback)
        var monsterTypeVar = enemy.Get("MonsterType");
        if (monsterTypeVar.VariantType == Variant.Type.String)
        {
            string monsterType = monsterTypeVar.AsString();
            if (!string.IsNullOrEmpty(monsterType))
            {
                return FormatMonsterName(monsterType);
            }
        }

        // Fallback to node name, formatted nicely
        string nodeName = enemy.Name;

        // Remove numeric suffixes
        int numIdx = nodeName.Length;
        while (numIdx > 0 && char.IsDigit(nodeName[numIdx - 1]))
            numIdx--;

        if (numIdx > 0)
            nodeName = nodeName.Substring(0, numIdx);

        return FormatMonsterName(nodeName);
    }

    /// <summary>
    /// Format a monster type name for display (e.g. "GoblinShaman" -> "Goblin Shaman")
    /// </summary>
    private string FormatMonsterName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Enemy";

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c))
            {
                result.Append(' ');
            }
            result.Append(c);
        }
        return result.ToString();
    }

    private void Die()
    {
        GD.Print("[FPSController] Player died!");

        // Notify AI broadcaster of death
        AIBroadcaster.Instance?.OnPlayerDeath();

        // Record death in game stats
        GameStats.Instance?.RecordDeath();

        // Cancel any active ability targeting
        if (Abilities.AbilityManager3D.Instance?.IsTargeting == true)
        {
            Abilities.AbilityManager3D.Instance.CancelTargeting();
        }

        // Reset all input state to prevent stuck inputs after death
        ResetInputState();

        // Ensure time is running at configured game speed
        GameConfig.ResumeToNormalSpeed();

        SoundManager3D.Instance?.PlayPlayerDeathSound();

        // Play event death sound
        SoundManager3D.Instance?.PlayPlayerDeathEventSound();

        EmitSignal(SignalName.Died);
        // TODO: Death screen, respawn logic
    }

    public void RequestScreenShake(float duration, float intensity)
    {
        _shakeTimer = duration;
        _shakeIntensity = intensity;
    }

    /// <summary>
    /// Get the camera's forward direction for UI raycasting, etc.
    /// </summary>
    public Vector3 GetLookDirection()
    {
        return _camera != null ? -_camera.GlobalTransform.Basis.Z : Vector3.Forward;
    }

    /// <summary>
    /// Get camera position for spawning projectiles, etc.
    /// </summary>
    public Vector3 GetCameraPosition()
    {
        return _camera?.GlobalPosition ?? GlobalPosition + new Vector3(0, Constants.PlayerEyeHeight, 0);
    }

    /// <summary>
    /// Set the safe respawn position (called when spawning)
    /// </summary>
    public void SetSafePosition(Vector3 position)
    {
        _lastSafePosition = position;
    }

    /// <summary>
    /// Teleport player to a position (used for respawning)
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        GlobalPosition = position;
        _velocity = Vector3.Zero;
        _lastSafePosition = position;
    }

    /// <summary>
    /// Called when window regains focus. Re-capture mouse if no UI is open.
    /// </summary>
    private void OnWindowFocusEntered()
    {
        // Brief delay to let the window fully regain focus
        CallDeferred(nameof(RecaptureMouseAfterFocus));
    }

    private void RecaptureMouseAfterFocus()
    {
        if (!IsAnyUIOpen())
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GD.Print("[FPSController] Window focus regained - mouse recaptured");
        }

        // Clear any stuck modifier key states that may have occurred during alt-tab
        // This helps prevent issues where Alt/Shift appear "stuck" after returning from alt-tab
        Input.FlushBufferedEvents();
        GD.Print("[FPSController] Flushed buffered input events after focus regain");
    }

    /// <summary>
    /// Set inspect mode - player can look around but cannot move
    /// </summary>
    public void SetInspectMode(bool enabled)
    {
        _isInspectMode = enabled;
        if (enabled)
        {
            // Stop any current movement
            _velocity = Vector3.Zero;
            Velocity = Vector3.Zero;
        }
    }

    /// <summary>
    /// Check if player is in inspect mode
    /// </summary>
    public bool IsInspectMode => _isInspectMode;

    /// <summary>
    /// Set edit mode - player can move but cannot attack (for In-Map Editor)
    /// </summary>
    public void SetEditMode(bool enabled)
    {
        _isEditMode = enabled;
        GD.Print($"[FPSController] Edit mode: {enabled}");
    }

    /// <summary>
    /// Check if player is in edit mode
    /// </summary>
    public bool IsEditMode => _isEditMode;

    /// <summary>
    /// Called when equipment changes - update HUD and first-person weapon.
    /// </summary>
    private void OnEquipmentChanged()
    {
        // Emit signals to update HUD
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);

        // Update first-person weapon to match equipped weapon
        CreateFirstPersonWeapon();

        // Update left hand item (torch, shield, or dual-wield weapon)
        UpdateLeftHandItem();

        GD.Print($"[FPSController] Equipment changed. Stats: {Stats.GetStatsSummary()}");
    }

    /// <summary>
    /// Update the left hand visual based on OffHand equipment slot.
    /// Can display: torch, shield, or dual-wield weapon.
    /// </summary>
    private void UpdateLeftHandItem()
    {
        // Clear existing left hand visuals
        if (_leftHandNode != null)
        {
            _leftHandNode.QueueFree();
            _leftHandNode = null;
        }
        if (_torchNode != null)
        {
            _torchNode.QueueFree();
            _torchNode = null;
            _torchLight = null;
        }

        // Get OffHand equipment
        var offHand = Equipment.GetEquippedItem(EquipmentSlot.OffHand);

        // Check if main hand is two-handed (no left hand item)
        var mainHand = Equipment.GetEquippedItem(EquipmentSlot.MainHand);
        if (mainHand?.IsTwoHanded == true)
        {
            _leftHandType = LeftHandItemType.None;
            GD.Print("[FPSController] Two-handed weapon equipped, left hand empty");
            return;
        }

        // No off-hand item equipped
        if (offHand == null)
        {
            _leftHandType = LeftHandItemType.None;
            GD.Print("[FPSController] No off-hand item, left hand empty");
            return;
        }

        // Determine what type of item and create appropriate visual
        if (offHand.Name.Contains("Torch", System.StringComparison.OrdinalIgnoreCase))
        {
            // It's a torch - use the existing torch creation
            CreateFirstPersonTorch();
            _leftHandType = LeftHandItemType.Torch;
            GD.Print("[FPSController] Torch equipped in left hand");
        }
        else if (offHand.Slot == EquipmentSlot.OffHand && offHand.BlockChance > 0)
        {
            // It's a shield
            CreateLeftHandShield(offHand);
            _leftHandType = LeftHandItemType.Shield;
            GD.Print($"[FPSController] Shield equipped in left hand: {offHand.Name}");
        }
        else if (offHand.WeaponType != Core.WeaponType.None && !offHand.IsTwoHanded)
        {
            // It's a one-handed weapon for dual wielding
            CreateLeftHandWeapon(offHand);
            _leftHandType = LeftHandItemType.Weapon;
            GD.Print($"[FPSController] Dual-wield weapon in left hand: {offHand.Name}");
        }
        else
        {
            _leftHandType = LeftHandItemType.None;
        }
    }

    /// <summary>
    /// Create a shield in the left hand based on equipment.
    /// </summary>
    private void CreateLeftHandShield(EquipmentItem shield)
    {
        if (_weaponHolder == null) return;

        _leftHandNode = new Node3D();
        _leftHandNode.Name = "LeftHandShield";

        // Determine shield type from name/stats
        ShieldFactory.ShieldType shieldType = ShieldFactory.ShieldType.RoundShield;
        string nameLower = shield.Name.ToLower();
        if (nameLower.Contains("buckler")) shieldType = ShieldFactory.ShieldType.Buckler;
        else if (nameLower.Contains("kite")) shieldType = ShieldFactory.ShieldType.KiteShield;
        else if (nameLower.Contains("tower")) shieldType = ShieldFactory.ShieldType.TowerShield;
        else if (nameLower.Contains("spike")) shieldType = ShieldFactory.ShieldType.SpikedShield;

        // Create shield mesh
        var shieldMesh = ShieldFactory.CreateShield(shieldType, 0.8f);
        _leftHandNode.AddChild(shieldMesh);

        // Position shield in left hand - angled to face slightly forward
        _leftHandRestPos = new Vector3(-0.55f, -0.25f, -0.35f);
        _leftHandNode.Position = _leftHandRestPos;
        _leftHandNode.RotationDegrees = new Vector3(10, 30, -15); // Angled outward

        _weaponHolder.AddChild(_leftHandNode);
    }

    /// <summary>
    /// Create a mirrored weapon in the left hand for dual wielding.
    /// </summary>
    private void CreateLeftHandWeapon(EquipmentItem weapon)
    {
        if (_weaponHolder == null) return;

        _leftHandNode = new Node3D();
        _leftHandNode.Name = "LeftHandWeapon";

        // Map to factory weapon type
        var factoryType = MapToFactoryWeaponType(weapon.WeaponType);
        var weaponMesh = WeaponFactory.CreateWeapon(factoryType, 0.9f);

        // Rotate to first-person orientation
        weaponMesh.RotationDegrees = new Vector3(90, 0, 0);
        _leftHandNode.AddChild(weaponMesh);

        // Get mirrored position from right hand positioning
        var (restPos, swingPos, restRot, _) = GetWeaponPositioning(weapon.WeaponType);

        // Mirror X position for left hand
        _leftHandRestPos = new Vector3(-restPos.X, restPos.Y, restPos.Z);
        _leftHandSwingPos = new Vector3(-swingPos.X, swingPos.Y, swingPos.Z);
        _leftHandNode.Position = _leftHandRestPos;

        // Mirror rotation
        _leftHandNode.RotationDegrees = new Vector3(restRot.X, -restRot.Y, -restRot.Z);

        // Mirror the mesh itself on X axis
        _leftHandNode.Scale = new Vector3(-1, 1, 1);

        _weaponHolder.AddChild(_leftHandNode);
    }

    /// <summary>
    /// Process health and mana regeneration.
    /// Call this from _PhysicsProcess.
    /// </summary>
    private void ProcessRegeneration(float delta)
    {
        // Health regen
        if (CurrentHealth < MaxHealth && Stats.HealthRegen > 0)
        {
            _healthRegenTimer += delta;
            if (_healthRegenTimer >= RegenTickInterval)
            {
                _healthRegenTimer = 0;
                int regenAmount = (int)(Stats.HealthRegen * RegenTickInterval);
                if (regenAmount > 0)
                {
                    CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + regenAmount);
                    EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
                }
            }
        }
        else
        {
            _healthRegenTimer = 0;
        }

        // Mana regen
        if (CurrentMana < MaxMana && Stats.ManaRegen > 0)
        {
            _manaRegenTimer += delta;
            if (_manaRegenTimer >= RegenTickInterval)
            {
                _manaRegenTimer = 0;
                int regenAmount = (int)(Stats.ManaRegen * RegenTickInterval);
                if (regenAmount > 0)
                {
                    CurrentMana = Mathf.Min(MaxMana, CurrentMana + regenAmount);
                    EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
                }
            }
        }
        else
        {
            _manaRegenTimer = 0;
        }
    }

    /// <summary>
    /// Spend an attribute point (called from Character Sheet UI).
    /// </summary>
    public bool SpendAttributePoint(string attribute)
    {
        bool success = Stats.SpendAttributePoint(attribute);
        if (success)
        {
            // Update max health/mana and emit signals
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
            EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
        }
        return success;
    }

    /// <summary>
    /// Get unspent attribute points.
    /// </summary>
    public int UnspentAttributePoints => Stats.UnspentAttributePoints;

    public override void _ExitTree()
    {
        Instance = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Disconnect window focus signal
        if (GetTree()?.Root != null)
        {
            GetTree().Root.FocusEntered -= OnWindowFocusEntered;
        }
    }
}
