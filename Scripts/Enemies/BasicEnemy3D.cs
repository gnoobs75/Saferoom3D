using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.Abilities;
using SafeRoom3D.Abilities.Effects;
using SafeRoom3D.UI;
using SafeRoom3D.Broadcaster;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Basic 3D enemy with AI state machine. Ported from 2D BasicEnemy.
/// States: Idle, Patrolling, Tracking, Aggro, Attacking, Dead
/// </summary>
public partial class BasicEnemy3D : CharacterBody3D
{
    // Enemy configuration
    public string MonsterId { get; set; } = "slime_basic";
    public string MonsterType { get; set; } = "Slime";

    // Stats (loaded from config or defaults)
    public int Level { get; set; } = 1;
    public int ExperienceReward { get; private set; }
    public int MaxHealth { get; private set; } = 75;
    public int CurrentHealth { get; private set; } = 75;
    public float MoveSpeed { get; private set; } = 3f;
    public float Damage { get; private set; } = 10f;
    public float AttackCooldown { get; private set; } = 1f;
    public float AggroRange { get; private set; } = 15f;
    public float DeaggroRange { get; private set; } = 25f;
    public float AttackRange { get; private set; } = 2f;
    public float MinStopDistance { get; private set; } = 3.5f;  // Minimum distance to stop from player
    public float PatrolRadius { get; private set; } = 8f;
    public float PatrolSpeed { get; private set; } = 2f;

    // Roamer system - large patrol radius monsters
    private bool _isRoamer;
    private Vector3 _spawnPosition;
    private const float RoamerPatrolRadius = 25f;

    // State machine
    public enum State { Idle, IdleInteracting, Patrolling, Tracking, Aggro, Attacking, Dead, Sleeping, Stasis }
    public State CurrentState { get; private set; } = State.Idle;

    // Stasis mode (for In-Map Editor - enemies play idle animation but don't react)
    private bool _isInStasis;

    // Components
    private MeshInstance3D? _meshInstance;
    private CollisionShape3D? _collider;
    private NavigationAgent3D? _navAgent;
    private Node3D? _player;

    // AI state
    private Vector3 _patrolTarget;
    private float _patrolWaitTimer;
    private float _attackTimer;
    private float _stateTimer;

    // Ability effects state
    private bool _isFeared;
    private bool _isFrozen;
    private Vector3 _fearFromPosition;

    // Enrage buff (from Goblin Shaman)
    private bool _isEnraged;
    private float _enrageTimer;
    private Node3D? _enrageIndicator;
    private GpuParticles3D? _enrageParticles;

    // Sleep system for performance
    private bool _isSleeping;
    private float _sleepCheckTimer;
    private const float SleepDistance = 40f;
    private const float WakeDistance = 35f;
    private const float SleepCheckInterval = 0.5f;

    // Idle interaction system (for goblins interacting with props)
    private Node3D? _interactionTarget;
    private float _interactionTimer;
    private string _currentInteraction = "";
    private const float InteractionSearchRadius = 8f;

    // Static prop cache for performance (shared across all enemies)
    private static readonly List<Node3D> _propCache = new();
    private static bool _propCacheInitialized;
    private static float _propCacheRefreshTimer;

    // Social chat system (monsters chatting with each other)
    private BasicEnemy3D? _socialChatPartner;
    private MonsterChatBubble? _chatBubble;
    private float _socialChatTimer;
    private bool _isSocialChatting;
    private const float SocialChatCheckInterval = 5f; // Check every 5 seconds

    // Visual
    private Color _baseColor = new(0.4f, 0.7f, 0.3f); // Default slime green
    private StandardMaterial3D? _material;
    private float _hitFlashTimer;

    // Hit shake effect (model wiggle on damage for haptic feedback)
    private float _hitShakeTimer;
    private float _hitShakeIntensity;
    private Vector3 _hitShakeOffset;
    private const float HitShakeDuration = 0.25f;
    private const float HitShakeBaseIntensity = 0.08f;

    // Health bar and floating text
    private Node3D? _healthBarContainer;
    private MeshInstance3D? _healthBarBg;
    private MeshInstance3D? _healthBarFill;
    private BoxMesh? _healthBarFillMesh;
    private StandardMaterial3D? _healthBarFillMat;
    private Label3D? _nameLabel;
    private float _healthBarVisibleTimer;

    // Animation
    private float _animTimer;
    private float _walkBobAmount;
    private float _attackSwingAngle;
    private bool _isAttackAnimating;
    private Node3D? _rightArmNode;
    private Node3D? _leftArmNode;
    private Node3D? _rightLegNode;
    private Node3D? _leftLegNode;
    private Node3D? _weaponNode;
    private float _idleBreathAmount;

    // AnimationPlayer system (new)
    private AnimationPlayer? _animPlayer;
    private MonsterMeshFactory.LimbNodes? _limbNodes;
    private string _currentAnimName = "";
    private int _currentAnimVariant;
    private AnimationType _currentAnimType = AnimationType.Idle;

    // Torch (for torchbearer variant)
    private bool _hasTorch;
    private bool _canHaveWeapon;
    private OmniLight3D? _torchLight;
    private Node3D? _torchNode;
    private float _torchFlickerTimer;

    // Signals
    [Signal] public delegate void DiedEventHandler(BasicEnemy3D enemy);
    [Signal] public delegate void DamagedEventHandler(int damage, int remaining);

    public override void _Ready()
    {
        LoadMonsterConfig();
        SetupComponents();
        SetupNavigation();

        CurrentHealth = MaxHealth;

        // Record spawn position for roamer patrol calculations
        _spawnPosition = GlobalPosition;

        // Find player - use deferred to ensure player is ready
        CallDeferred(MethodName.FindPlayer);

        // Start in idle
        ChangeState(State.Idle);

        GD.Print($"[BasicEnemy3D] Spawned: {MonsterType} at {GlobalPosition}{(_isRoamer ? " (Roamer)" : "")}");
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("Player") as Node3D;
        if (_player == null)
        {
            // Try using singleton
            _player = FPSController.Instance;
        }
        if (_player != null)
        {
            GD.Print($"[BasicEnemy3D] {MonsterType} found player at {_player.GlobalPosition}");
        }
    }

    /// <summary>
    /// Configure this enemy as a roamer with large patrol radius.
    /// Roamers patrol 25 units around their spawn point instead of 8.
    /// </summary>
    public void SetRoamer(bool isRoamer)
    {
        _isRoamer = isRoamer;
        if (isRoamer)
        {
            PatrolRadius = RoamerPatrolRadius;
            GD.Print($"[BasicEnemy3D] {MonsterType} configured as Roamer (patrol radius: {PatrolRadius})");
        }
    }

    /// <summary>
    /// Check if this enemy is a roamer.
    /// </summary>
    public bool IsRoamer => _isRoamer;

    private void LoadMonsterConfig()
    {
        // Load from JSON data via DataLoader
        var config = DataLoader.GetMonster(MonsterType);

        if (config != null)
        {
            // Load all stats from JSON config
            MaxHealth = config.MaxHealth;
            MoveSpeed = config.MoveSpeed;
            Damage = config.Damage;
            AttackRange = config.AttackRange;
            AggroRange = config.AggroRange;
            MinStopDistance = config.MinStopDistance;
            _baseColor = config.GetGodotColor();
            _hasTorch = config.HasTorch;
            _canHaveWeapon = config.CanHaveWeapon;
        }
        else
        {
            // Fallback to defaults if monster type not found in JSON
            var defaults = DataLoader.GetMonsterDefaults();
            AttackRange = defaults.AttackRange;
            AggroRange = defaults.AggroRange;
            MinStopDistance = defaults.MinStopDistance;
            _baseColor = defaults.Color.ToGodotColor();

            GD.PrintErr($"[BasicEnemy3D] Monster type '{MonsterType}' not found in data, using defaults");
        }

        // Set MinStopDistance based on attack style - ranged stay back, melee stop at 3.5
        if (MinStopDistance == 3.5f)  // Still at default, auto-calculate
        {
            if (AttackRange >= 6f)
            {
                // Ranged attackers - stay at their attack range
                MinStopDistance = AttackRange - 1f;
            }
            else
            {
                // Melee - ensure they can attack from stop distance
                MinStopDistance = 3.5f;
                if (AttackRange < MinStopDistance)
                {
                    AttackRange = MinStopDistance + 0.5f;  // Can attack from stop distance
                }
            }
        }

        // Apply level scaling
        if (Level > 1)
        {
            // Store base health for XP calculation
            int baseHealth = MaxHealth;

            // Scale stats by level
            MaxHealth = (int)(MaxHealth * (1 + (Level - 1) * 0.15f));
            Damage = Damage * (1 + (Level - 1) * 0.1f);

            // Calculate XP reward: BaseHealth * (1 + Level * 0.5) / 10
            ExperienceReward = (int)(baseHealth * (1 + Level * 0.5f) / 10f);
        }
        else
        {
            // Level 1: Calculate XP from current MaxHealth
            ExperienceReward = (int)(MaxHealth * 1.5f / 10f);
        }

        CurrentHealth = MaxHealth;
    }

    private void SetupComponents()
    {
        // Create a detailed mesh container
        _meshInstance = new MeshInstance3D();
        _meshInstance.Position = new Vector3(0, 0, 0);
        _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        AddChild(_meshInstance);

        // Check for GLB model override first
        string meshType = MonsterType.ToLower();
        bool usedGlbModel = false;
        if (GlbModelConfig.TryGetMonsterGlbPath(meshType, out string? glbPath) && !string.IsNullOrWhiteSpace(glbPath))
        {
            var glbModel = GlbModelConfig.LoadGlbModel(glbPath, 1f);
            if (glbModel != null)
            {
                _meshInstance.AddChild(glbModel);
                _limbNodes = null; // GLB models don't have procedural LimbNodes
                usedGlbModel = true;
                GD.Print($"[BasicEnemy3D] Loaded GLB model for {MonsterType}: {glbPath}");
            }
            else
            {
                GD.PrintErr($"[BasicEnemy3D] Failed to load GLB for {MonsterType}, falling back to procedural");
            }
        }

        // Create body parts based on monster type using factory (with LOD and AnimationPlayer support)
        // Skip if we already loaded a GLB model
        if (!usedGlbModel) switch (meshType)
        {
            case "torchbearer":
            case "goblin_torchbearer":
                // Torchbearer uses goblin mesh + adds torch light
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, "goblin", _baseColor);
                CreateTorchOnLeftHand();
                AttachRandomWeapon(); // Also give torchbearers a weapon
                break;
            case "badlama":
                // Badlama uses legacy mesh for now (no factory method yet)
                CreateBadlamaMesh();
                break;
            case "goblin":
                // Goblin with random weapon
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, meshType, _baseColor);
                AttachRandomWeapon();
                break;
            case "skeleton":
                // Skeleton with random weapon
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, meshType, _baseColor);
                AttachRandomWeapon();
                break;
            case "wolf":
            case "bat":
            case "dragon":
            case "slime":
            case "mushroom":
            case "spider":
            case "eye":
            case "lizard":
                // Non-humanoid monsters don't use weapons
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, meshType, _baseColor);
                break;
            // DCC Humanoid monsters with weapons
            case "crawler_killer":
            case "living_armor":
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, meshType, _baseColor);
                AttachRandomWeapon();
                break;
            // DCC Non-humanoid monsters
            case "shadow_stalker":
            case "flesh_golem":
            case "plague_bearer":
            case "camera_drone":
            case "shock_drone":
            case "advertiser_bot":
            case "clockwork_spider":
            case "lava_elemental":
            case "ice_wraith":
            case "gelatinous_cube":
            case "void_spawn":
            case "mimic":
            case "dungeon_rat":
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, meshType, _baseColor);
                break;
            default:
                // Unknown monsters default to goblin with weapon
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, "goblin", _baseColor);
                AttachRandomWeapon();
                break;
        }

        // Create AnimationPlayer with procedural animations
        SetupAnimationPlayer();

        // Create collision
        _collider = new CollisionShape3D();
        var shape = new CapsuleShape3D();
        shape.Radius = 0.35f;
        shape.Height = 1.0f;
        _collider.Shape = shape;
        _collider.Position = new Vector3(0, 0.5f, 0);
        AddChild(_collider);

        // Set collision layers
        // Layer 2 = Enemy, Layer 5 = Obstacle (bit 4 = 16), Layer 8 = Wall (bit 7 = 128)
        CollisionLayer = 2; // Enemy layer
        CollisionMask = 1 | 16 | 128; // Player, Obstacle, Wall (layer 8)

        // Add to enemies group
        AddToGroup("Enemies");

        // Create health bar above the monster
        CreateHealthBar();
    }

    private void CreateHealthBar()
    {
        // Container that will billboard toward camera
        _healthBarContainer = new Node3D();
        _healthBarContainer.Name = "HealthBar";
        _healthBarContainer.Position = new Vector3(0, 2.2f, 0); // Above the head
        AddChild(_healthBarContainer);

        // Monster name label above health bar
        _nameLabel = new Label3D();
        _nameLabel.Text = MonsterType;
        _nameLabel.FontSize = 32;
        _nameLabel.OutlineSize = 4;
        _nameLabel.Modulate = new Color(1f, 1f, 1f);
        _nameLabel.Position = new Vector3(0, 0.18f, 0); // Above the health bar
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.VerticalAlignment = VerticalAlignment.Bottom;
        _nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _nameLabel.NoDepthTest = false; // Don't render through walls
        _healthBarContainer.AddChild(_nameLabel);

        // Background (dark gray) - uses material billboard mode for zero-cost billboarding
        _healthBarBg = new MeshInstance3D();
        var bgMesh = new BoxMesh();
        bgMesh.Size = new Vector3(1.2f, 0.15f, 0.02f);
        _healthBarBg.Mesh = bgMesh;
        var bgMat = new StandardMaterial3D();
        bgMat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        bgMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        bgMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled; // PERF: GPU billboarding
        bgMat.NoDepthTest = false; // Don't render through walls
        _healthBarBg.MaterialOverride = bgMat;
        _healthBarBg.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(_healthBarBg);

        // Fill (red/green gradient based on health) - uses material billboard mode
        _healthBarFill = new MeshInstance3D();
        _healthBarFillMesh = new BoxMesh();
        _healthBarFillMesh.Size = new Vector3(1.1f, 0.12f, 0.025f);
        _healthBarFill.Mesh = _healthBarFillMesh;
        _healthBarFillMat = new StandardMaterial3D();
        _healthBarFillMat.AlbedoColor = new Color(0.2f, 0.9f, 0.2f); // Green when full
        _healthBarFillMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _healthBarFillMat.EmissionEnabled = true;
        _healthBarFillMat.Emission = new Color(0.1f, 0.4f, 0.1f);
        _healthBarFillMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled; // PERF: GPU billboarding
        _healthBarFillMat.NoDepthTest = false; // Don't render through walls
        _healthBarFill.MaterialOverride = _healthBarFillMat;
        _healthBarFill.Position = new Vector3(0, 0, 0.01f);
        _healthBarFill.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(_healthBarFill);

        // Show based on nameplate toggle setting
        _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
    }

    /// <summary>
    /// Set up AnimationPlayer with procedural animations for this monster type.
    /// Only creates animations if we have valid limb references.
    /// </summary>
    private void SetupAnimationPlayer()
    {
        // Only create AnimationPlayer if we have limb nodes from the factory
        if (_limbNodes == null || _meshInstance == null)
        {
            // For legacy monsters (CreateGoblinMesh, etc.), skip AnimationPlayer
            // They use the old code-based animation in UpdateAnimation()
            return;
        }

        try
        {
            // Create AnimationPlayer with all 18 animations (6 types x 3 variants)
            _animPlayer = MonsterAnimationSystem.CreateAnimationPlayer(
                _meshInstance,
                MonsterType.ToLower(),
                _limbNodes
            );

            // Pick a random idle variant to start with
            _currentAnimVariant = GD.RandRange(0, 2);
            _currentAnimType = AnimationType.Idle;
            _currentAnimName = MonsterAnimationSystem.GetAnimationName(AnimationType.Idle, _currentAnimVariant);

            if (_animPlayer.HasAnimation(_currentAnimName))
            {
                _animPlayer.Play(_currentAnimName);
            }

            GD.Print($"[BasicEnemy3D] {MonsterType} AnimationPlayer created with {_animPlayer.GetAnimationList().Length} animations");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[BasicEnemy3D] Failed to create AnimationPlayer for {MonsterType}: {e.Message}");
            _animPlayer = null;
        }
    }

    /// <summary>
    /// Play an animation using the AnimationPlayer system.
    /// Falls back to code-based animation if AnimationPlayer not available.
    /// </summary>
    private void PlayAnimation(AnimationType animType, int variant = -1)
    {
        if (_animPlayer == null) return;

        // Use random variant if not specified
        if (variant < 0)
        {
            variant = GD.RandRange(0, 2);
        }

        _currentAnimType = animType;
        _currentAnimVariant = variant;
        _currentAnimName = MonsterAnimationSystem.GetAnimationName(animType, variant);

        if (_animPlayer.HasAnimation(_currentAnimName))
        {
            // For non-looping animations (attack, hit, die), play from start
            if (animType == AnimationType.Attack || animType == AnimationType.Hit || animType == AnimationType.Die)
            {
                _animPlayer.Stop();
                _animPlayer.Play(_currentAnimName);
            }
            else
            {
                // For looping animations, only change if different
                if (_animPlayer.CurrentAnimation != _currentAnimName)
                {
                    _animPlayer.Play(_currentAnimName);
                }
            }
        }
    }

    /// <summary>
    /// Check if the current animation has finished playing.
    /// </summary>
    private bool IsAnimationFinished()
    {
        if (_animPlayer == null) return true;
        return !_animPlayer.IsPlaying() || _animPlayer.CurrentAnimationPosition >= _animPlayer.CurrentAnimationLength - 0.05f;
    }

    // PERFORMANCE: Health bar uses GPU billboard mode - no camera lookups needed

    private void UpdateHealthBar()
    {
        // PERFORMANCE: Billboard mode is now set on materials directly (GPU-based billboarding)
        // No LookAt() calls needed - this method is kept for compatibility but does nothing
        // Health bar visual updates (size, color) are done in UpdateHealthBarVisuals() when health changes
    }

    private void UpdateHealthBarVisuals()
    {
        if (_healthBarFillMesh == null || _healthBarFillMat == null) return;

        // Calculate health percentage
        float healthPercent = (float)CurrentHealth / MaxHealth;
        healthPercent = Mathf.Clamp(healthPercent, 0f, 1f);

        // Update fill width and position using cached mesh reference
        float fullWidth = 1.1f;
        float currentWidth = fullWidth * healthPercent;
        _healthBarFillMesh.Size = new Vector3(currentWidth, 0.12f, 0.025f);

        // Offset to keep left-aligned
        if (_healthBarFill != null)
        {
            _healthBarFill.Position = new Vector3((currentWidth - fullWidth) / 2, 0, 0.01f);
        }

        // Update color based on health (green -> yellow -> red) using cached material reference
        Color healthColor;
        if (healthPercent > 0.6f)
        {
            healthColor = new Color(0.2f, 0.9f, 0.2f); // Green
        }
        else if (healthPercent > 0.3f)
        {
            healthColor = new Color(0.9f, 0.9f, 0.2f); // Yellow
        }
        else
        {
            healthColor = new Color(0.9f, 0.2f, 0.2f); // Red
        }

        _healthBarFillMat.AlbedoColor = healthColor;
        _healthBarFillMat.Emission = healthColor * 0.3f;
    }

    private void SpawnFloatingDamageText(int damage, bool isCrit = false)
    {
        // Cache position (may not be in tree)
        var spawnPosition = IsInsideTree() ? GlobalPosition : Position;

        // Create floating damage number - start lower on the body (waist level)
        var textContainer = new Node3D();
        GetTree().Root.AddChild(textContainer);
        textContainer.GlobalPosition = spawnPosition + new Vector3(0, 0.8f, 0);  // Lower start position

        // Create 3D label
        var label = new Label3D();
        label.Text = isCrit ? $"!{damage}!" : damage.ToString();
        label.FontSize = isCrit ? 96 : 64;  // Bigger font for crits
        label.OutlineSize = isCrit ? 12 : 8;
        label.Modulate = new Color(1f, 0.15f, 0.1f);  // Always red damage
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.NoDepthTest = true;
        textContainer.AddChild(label);

        // Crits get extra bold outline color
        if (isCrit)
        {
            label.OutlineModulate = new Color(1f, 0.9f, 0.2f);  // Gold outline for crits
        }

        // Arc to left or right (rainbow arc pattern)
        bool goRight = GD.Randf() > 0.5f;
        float arcDirection = goRight ? 1f : -1f;
        float arcDistance = (float)GD.RandRange(1.2f, 1.8f);

        // Animation duration
        float duration = isCrit ? 1.2f : 0.9f;  // Crits stay visible longer

        // Animate in a curved arc path
        var tween = textContainer.CreateTween();
        tween.SetParallel(true);

        // Vertical: rise up in an arc (higher in middle)
        float peakHeight = spawnPosition.Y + 2.5f;
        float endHeight = spawnPosition.Y + 1.8f;

        // Use custom tweener for arc motion
        tween.TweenMethod(
            Callable.From((float t) =>
            {
                // Parabolic arc for Y position (peaks at 0.5, ends lower)
                float arcY = -4f * (t - 0.5f) * (t - 0.5f) + 1f;  // Parabola peaking at t=0.5
                float y = Mathf.Lerp(spawnPosition.Y + 0.8f, endHeight, t) + arcY * 0.8f;

                // Horizontal: curve outward
                float x = spawnPosition.X + arcDirection * t * arcDistance;

                textContainer.GlobalPosition = new Vector3(x, y, spawnPosition.Z);
            }),
            0f, 1f, duration
        );

        // Scale: crits pulse bigger
        if (isCrit)
        {
            label.Scale = new Vector3(1.3f, 1.3f, 1f);
            tween.TweenProperty(label, "scale", new Vector3(1f, 1f, 1f), duration * 0.3f);
        }

        // Fade out at the end
        tween.TweenProperty(label, "modulate:a", 0f, duration * 0.4f).SetDelay(duration * 0.6f);

        // Cleanup
        tween.Chain().TweenCallback(Callable.From(() => textContainer.QueueFree()));
    }

    private void CreateGoblinMesh()
    {
        // Goblin colors
        Color skinColor = new Color(0.45f, 0.55f, 0.35f); // Olive green skin
        Color darkSkin = skinColor.Darkened(0.2f);
        Color eyeColor = new Color(0.9f, 0.85f, 0.2f); // Yellow eyes
        Color pupilColor = new Color(0.1f, 0.1f, 0.1f); // Black pupils
        Color clothColor = new Color(0.4f, 0.3f, 0.2f); // Brown cloth/leather
        Color metalColor = new Color(0.5f, 0.45f, 0.4f); // Rusty metal

        // Create materials
        _material = new StandardMaterial3D();
        _material.AlbedoColor = skinColor;
        _material.Roughness = 0.8f;

        var clothMaterial = new StandardMaterial3D();
        clothMaterial.AlbedoColor = clothColor;
        clothMaterial.Roughness = 0.9f;

        var eyeMaterial = new StandardMaterial3D();
        eyeMaterial.AlbedoColor = eyeColor;
        eyeMaterial.Roughness = 0.5f;
        eyeMaterial.EmissionEnabled = true;
        eyeMaterial.Emission = eyeColor * 0.3f;

        var pupilMaterial = new StandardMaterial3D();
        pupilMaterial.AlbedoColor = pupilColor;
        pupilMaterial.Roughness = 0.2f;

        var metalMaterial = new StandardMaterial3D();
        metalMaterial.AlbedoColor = metalColor;
        metalMaterial.Roughness = 0.6f;
        metalMaterial.Metallic = 0.4f;

        // === BODY (torso) ===
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh();
        bodyMesh.Radius = 0.25f;
        bodyMesh.Height = 0.5f;
        body.Mesh = bodyMesh;
        body.MaterialOverride = clothMaterial;
        body.Position = new Vector3(0, 0.5f, 0);
        body.Scale = new Vector3(1f, 1.2f, 0.8f); // Slightly tall, narrow torso
        _meshInstance!.AddChild(body);

        // === BELLY (pot belly) ===
        var belly = new MeshInstance3D();
        var bellyMesh = new SphereMesh();
        bellyMesh.Radius = 0.2f;
        bellyMesh.Height = 0.4f;
        belly.Mesh = bellyMesh;
        belly.MaterialOverride = _material; // Skin showing
        belly.Position = new Vector3(0, 0.4f, 0.08f);
        belly.Scale = new Vector3(0.9f, 0.8f, 0.7f);
        _meshInstance.AddChild(belly);

        // === HEAD ===
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh();
        headMesh.Radius = 0.22f;
        headMesh.Height = 0.44f;
        head.Mesh = headMesh;
        head.MaterialOverride = _material;
        head.Position = new Vector3(0, 0.88f, 0);
        head.Scale = new Vector3(1f, 0.9f, 0.95f); // Slightly flattened
        _meshInstance.AddChild(head);

        // === EARS (large pointed goblin ears) ===
        var leftEar = CreateGoblinEar(_material, true);
        leftEar.Position = new Vector3(-0.22f, 0.92f, 0);
        _meshInstance.AddChild(leftEar);

        var rightEar = CreateGoblinEar(_material, false);
        rightEar.Position = new Vector3(0.22f, 0.92f, 0);
        _meshInstance.AddChild(rightEar);

        // === NOSE (long pointy goblin nose) ===
        var nose = new MeshInstance3D();
        var noseMesh = new CylinderMesh();
        noseMesh.TopRadius = 0.02f;
        noseMesh.BottomRadius = 0.05f;
        noseMesh.Height = 0.15f;
        nose.Mesh = noseMesh;
        nose.MaterialOverride = _material;
        nose.Position = new Vector3(0, 0.85f, 0.2f);
        nose.RotationDegrees = new Vector3(-70, 0, 0); // Point forward and down
        _meshInstance.AddChild(nose);

        // === EYES ===
        // Left eye white
        var leftEye = new MeshInstance3D();
        var eyeMesh = new SphereMesh();
        eyeMesh.Radius = 0.06f;
        eyeMesh.Height = 0.12f;
        leftEye.Mesh = eyeMesh;
        leftEye.MaterialOverride = eyeMaterial;
        leftEye.Position = new Vector3(-0.08f, 0.92f, 0.16f);
        _meshInstance.AddChild(leftEye);

        // Left pupil
        var leftPupil = new MeshInstance3D();
        var pupilMesh = new SphereMesh();
        pupilMesh.Radius = 0.03f;
        pupilMesh.Height = 0.06f;
        leftPupil.Mesh = pupilMesh;
        leftPupil.MaterialOverride = pupilMaterial;
        leftPupil.Position = new Vector3(-0.08f, 0.92f, 0.2f);
        _meshInstance.AddChild(leftPupil);

        // Right eye white
        var rightEye = new MeshInstance3D();
        rightEye.Mesh = eyeMesh;
        rightEye.MaterialOverride = eyeMaterial;
        rightEye.Position = new Vector3(0.08f, 0.92f, 0.16f);
        _meshInstance.AddChild(rightEye);

        // Right pupil
        var rightPupil = new MeshInstance3D();
        rightPupil.Mesh = pupilMesh;
        rightPupil.MaterialOverride = pupilMaterial;
        rightPupil.Position = new Vector3(0.08f, 0.92f, 0.2f);
        _meshInstance.AddChild(rightPupil);

        // === MOUTH (wide grin with teeth) ===
        var mouth = new MeshInstance3D();
        var mouthMesh = new BoxMesh();
        mouthMesh.Size = new Vector3(0.12f, 0.03f, 0.04f);
        mouth.Mesh = mouthMesh;
        var mouthMaterial = new StandardMaterial3D();
        mouthMaterial.AlbedoColor = new Color(0.2f, 0.1f, 0.1f); // Dark mouth
        mouth.MaterialOverride = mouthMaterial;
        mouth.Position = new Vector3(0, 0.78f, 0.18f);
        _meshInstance.AddChild(mouth);

        // Teeth (small white boxes)
        var teethMaterial = new StandardMaterial3D();
        teethMaterial.AlbedoColor = new Color(0.9f, 0.85f, 0.7f);
        for (int i = 0; i < 4; i++)
        {
            var tooth = new MeshInstance3D();
            var toothMesh = new BoxMesh();
            toothMesh.Size = new Vector3(0.02f, 0.025f, 0.02f);
            tooth.Mesh = toothMesh;
            tooth.MaterialOverride = teethMaterial;
            float xOffset = -0.04f + i * 0.027f;
            tooth.Position = new Vector3(xOffset, 0.79f, 0.19f);
            _meshInstance.AddChild(tooth);
        }

        // === ARMS ===
        var leftArm = CreateGoblinArm(_material, true);
        leftArm.Position = new Vector3(-0.3f, 0.5f, 0);
        leftArm.Name = "LeftArm";
        _meshInstance.AddChild(leftArm);
        _leftArmNode = leftArm; // Store for animation

        var rightArm = CreateGoblinArm(_material, false);
        rightArm.Position = new Vector3(0.3f, 0.5f, 0);
        rightArm.Name = "RightArm";
        _meshInstance.AddChild(rightArm);
        _rightArmNode = rightArm; // Store for animation

        // === LEGS ===
        var leftLeg = CreateGoblinLeg(_material, clothMaterial, true);
        leftLeg.Position = new Vector3(-0.1f, 0.25f, 0);
        leftLeg.Name = "LeftLeg";
        _meshInstance.AddChild(leftLeg);
        _leftLegNode = leftLeg; // Store for animation

        var rightLeg = CreateGoblinLeg(_material, clothMaterial, false);
        rightLeg.Position = new Vector3(0.1f, 0.25f, 0);
        rightLeg.Name = "RightLeg";
        _meshInstance.AddChild(rightLeg);
        _rightLegNode = rightLeg; // Store for animation

        // === WEAPON (rusty dagger) ===
        // Attach weapon to right hand - positioned so hilt is in hand, blade points forward/up
        var weapon = CreateWeapon(metalMaterial, clothMaterial);
        // Position at right hand (hand is at approx 0.08, -0.38, 0.1 relative to arm at 0.25f, 0.5f)
        weapon.Position = new Vector3(0.33f, 0.12f, 0.2f); // Hand position
        weapon.RotationDegrees = new Vector3(-45, 0, -15); // Blade pointing forward-up
        weapon.Name = "Weapon";
        _meshInstance.AddChild(weapon);
        _weaponNode = weapon; // Store for animation

        // === LOINCLOTH ===
        var loincloth = new MeshInstance3D();
        var clothMesh = new BoxMesh();
        clothMesh.Size = new Vector3(0.25f, 0.15f, 0.08f);
        loincloth.Mesh = clothMesh;
        loincloth.MaterialOverride = clothMaterial;
        loincloth.Position = new Vector3(0, 0.32f, 0.05f);
        _meshInstance.AddChild(loincloth);
    }

    private Node3D CreateGoblinEar(StandardMaterial3D material, bool isLeft)
    {
        var ear = new Node3D();
        float side = isLeft ? -1f : 1f;

        // Main ear part (elongated cone-like shape using cylinder)
        var earMesh = new MeshInstance3D();
        var cyl = new CylinderMesh();
        cyl.TopRadius = 0.01f;
        cyl.BottomRadius = 0.06f;
        cyl.Height = 0.18f;
        earMesh.Mesh = cyl;
        earMesh.MaterialOverride = material;
        earMesh.RotationDegrees = new Vector3(0, 0, side * 60); // Point outward and up
        ear.AddChild(earMesh);

        return ear;
    }

    private Node3D CreateGoblinArm(StandardMaterial3D material, bool isLeft)
    {
        var arm = new Node3D();
        float side = isLeft ? -1f : 1f;

        // Upper arm
        var upperArm = new MeshInstance3D();
        var upperMesh = new CapsuleMesh();
        upperMesh.Radius = 0.06f;
        upperMesh.Height = 0.25f;
        upperArm.Mesh = upperMesh;
        upperArm.MaterialOverride = material;
        upperArm.RotationDegrees = new Vector3(0, 0, side * 15);
        arm.AddChild(upperArm);

        // Forearm
        var forearm = new MeshInstance3D();
        var foreMesh = new CapsuleMesh();
        foreMesh.Radius = 0.05f;
        foreMesh.Height = 0.22f;
        forearm.Mesh = foreMesh;
        forearm.MaterialOverride = material;
        forearm.Position = new Vector3(side * 0.05f, -0.2f, 0.05f);
        forearm.RotationDegrees = new Vector3(-30, 0, side * 10);
        arm.AddChild(forearm);

        // Hand (small sphere)
        var hand = new MeshInstance3D();
        var handMesh = new SphereMesh();
        handMesh.Radius = 0.05f;
        handMesh.Height = 0.1f;
        hand.Mesh = handMesh;
        hand.MaterialOverride = material;
        hand.Position = new Vector3(side * 0.08f, -0.38f, 0.1f);
        arm.AddChild(hand);

        // Fingers (3 small cylinders)
        for (int i = 0; i < 3; i++)
        {
            var finger = new MeshInstance3D();
            var fingerMesh = new CylinderMesh();
            fingerMesh.TopRadius = 0.01f;
            fingerMesh.BottomRadius = 0.015f;
            fingerMesh.Height = 0.05f;
            finger.Mesh = fingerMesh;
            finger.MaterialOverride = material;
            float angle = (i - 1) * 20f;
            finger.Position = new Vector3(side * 0.08f + (i - 1) * 0.02f, -0.42f, 0.12f);
            finger.RotationDegrees = new Vector3(-60, angle, 0);
            arm.AddChild(finger);
        }

        return arm;
    }

    private Node3D CreateGoblinLeg(StandardMaterial3D skinMaterial, StandardMaterial3D clothMaterial, bool isLeft)
    {
        var leg = new Node3D();
        float side = isLeft ? -1f : 1f;

        // Upper leg (thigh)
        var thigh = new MeshInstance3D();
        var thighMesh = new CapsuleMesh();
        thighMesh.Radius = 0.07f;
        thighMesh.Height = 0.2f;
        thigh.Mesh = thighMesh;
        thigh.MaterialOverride = skinMaterial;
        leg.AddChild(thigh);

        // Lower leg (shin)
        var shin = new MeshInstance3D();
        var shinMesh = new CapsuleMesh();
        shinMesh.Radius = 0.05f;
        shinMesh.Height = 0.18f;
        shin.Mesh = shinMesh;
        shin.MaterialOverride = skinMaterial;
        shin.Position = new Vector3(0, -0.18f, 0);
        leg.AddChild(shin);

        // Foot (elongated sphere)
        var foot = new MeshInstance3D();
        var footMesh = new SphereMesh();
        footMesh.Radius = 0.06f;
        footMesh.Height = 0.12f;
        foot.Mesh = footMesh;
        foot.MaterialOverride = skinMaterial;
        foot.Position = new Vector3(0, -0.3f, 0.04f);
        foot.Scale = new Vector3(0.8f, 0.6f, 1.2f); // Elongated for goblin feet
        leg.AddChild(foot);

        // Toes (2 small spheres)
        for (int i = 0; i < 2; i++)
        {
            var toe = new MeshInstance3D();
            var toeMesh = new SphereMesh();
            toeMesh.Radius = 0.025f;
            toeMesh.Height = 0.05f;
            toe.Mesh = toeMesh;
            toe.MaterialOverride = skinMaterial;
            toe.Position = new Vector3((i - 0.5f) * 0.04f, -0.32f, 0.1f);
            leg.AddChild(toe);
        }

        return leg;
    }

    /// <summary>
    /// Attach a random weapon from WeaponFactory to humanoid monsters (goblin, skeleton)
    /// </summary>
    private void AttachRandomWeapon()
    {
        if (_limbNodes == null) return;

        // Get all weapon types and pick one randomly
        var weaponTypes = System.Enum.GetValues<WeaponFactory.WeaponType>();
        var randomType = weaponTypes[GD.RandRange(0, weaponTypes.Length - 1)];

        // Create the weapon with appropriate scale for the monster
        float weaponScale = MonsterType.ToLower() == "skeleton" ? 0.9f : 0.7f; // Skeletons are taller
        var weapon = WeaponFactory.CreateWeapon(randomType, weaponScale);

        // Attach to the right arm
        WeaponFactory.AttachToHumanoid(weapon, _limbNodes, weaponScale);
        _weaponNode = weapon;

        GD.Print($"[BasicEnemy3D] {MonsterType} equipped with {randomType}");
    }

    private Node3D CreateWeapon(StandardMaterial3D metalMaterial, StandardMaterial3D handleMaterial)
    {
        // Randomly select weapon type for variety
        int weaponType = (int)(GD.Randf() * 4); // 0-3 different weapons
        return weaponType switch
        {
            0 => CreateDagger(metalMaterial, handleMaterial),
            1 => CreateShortSword(metalMaterial, handleMaterial),
            2 => CreateClub(handleMaterial),
            3 => CreateAxe(metalMaterial, handleMaterial),
            _ => CreateDagger(metalMaterial, handleMaterial)
        };
    }

    private Node3D CreateDagger(StandardMaterial3D metalMaterial, StandardMaterial3D handleMaterial)
    {
        var weapon = new Node3D();

        // Handle (at origin - this is where the hand grips)
        var handle = new MeshInstance3D();
        var handleMesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.02f, Height = 0.1f };
        handle.Mesh = handleMesh;
        handle.MaterialOverride = handleMaterial;
        weapon.AddChild(handle);

        // Guard (cross-guard) - just above handle
        var guard = new MeshInstance3D();
        var guardMesh = new BoxMesh { Size = new Vector3(0.07f, 0.015f, 0.02f) };
        guard.Mesh = guardMesh;
        guard.MaterialOverride = metalMaterial;
        guard.Position = new Vector3(0, 0.055f, 0);
        weapon.AddChild(guard);

        // Blade - extends from guard upward
        var blade = new MeshInstance3D();
        var bladeMesh = new PrismMesh { Size = new Vector3(0.035f, 0.2f, 0.015f) };
        blade.Mesh = bladeMesh;
        blade.MaterialOverride = metalMaterial;
        blade.Position = new Vector3(0, 0.16f, 0);
        weapon.AddChild(blade);

        // Pommel at bottom of handle
        var pommel = new MeshInstance3D();
        var pommelMesh = new SphereMesh { Radius = 0.022f, Height = 0.044f };
        pommel.Mesh = pommelMesh;
        pommel.MaterialOverride = metalMaterial;
        pommel.Position = new Vector3(0, -0.06f, 0);
        weapon.AddChild(pommel);

        return weapon;
    }

    private Node3D CreateShortSword(StandardMaterial3D metalMaterial, StandardMaterial3D handleMaterial)
    {
        var weapon = new Node3D();

        // Handle
        var handle = new MeshInstance3D();
        var handleMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.022f, Height = 0.12f };
        handle.Mesh = handleMesh;
        handle.MaterialOverride = handleMaterial;
        weapon.AddChild(handle);

        // Guard (larger cross-guard)
        var guard = new MeshInstance3D();
        var guardMesh = new BoxMesh { Size = new Vector3(0.1f, 0.02f, 0.025f) };
        guard.Mesh = guardMesh;
        guard.MaterialOverride = metalMaterial;
        guard.Position = new Vector3(0, 0.07f, 0);
        weapon.AddChild(guard);

        // Blade - longer sword blade
        var blade = new MeshInstance3D();
        var bladeMesh = new BoxMesh { Size = new Vector3(0.04f, 0.35f, 0.012f) };
        blade.Mesh = bladeMesh;
        blade.MaterialOverride = metalMaterial;
        blade.Position = new Vector3(0, 0.25f, 0);
        weapon.AddChild(blade);

        // Blade tip (pointed)
        var tip = new MeshInstance3D();
        var tipMesh = new PrismMesh { Size = new Vector3(0.04f, 0.08f, 0.012f) };
        tip.Mesh = tipMesh;
        tip.MaterialOverride = metalMaterial;
        tip.Position = new Vector3(0, 0.46f, 0);
        weapon.AddChild(tip);

        // Pommel
        var pommel = new MeshInstance3D();
        var pommelMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f };
        pommel.Mesh = pommelMesh;
        pommel.MaterialOverride = metalMaterial;
        pommel.Position = new Vector3(0, -0.08f, 0);
        weapon.AddChild(pommel);

        return weapon;
    }

    private Node3D CreateClub(StandardMaterial3D woodMaterial)
    {
        var weapon = new Node3D();

        // Handle (tapered wooden stick)
        var handle = new MeshInstance3D();
        var handleMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.02f, Height = 0.25f };
        handle.Mesh = handleMesh;
        handle.MaterialOverride = woodMaterial;
        handle.Position = new Vector3(0, 0.05f, 0);
        weapon.AddChild(handle);

        // Club head (thick end)
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.06f, Height = 0.12f };
        head.Mesh = headMesh;
        head.MaterialOverride = woodMaterial;
        head.Position = new Vector3(0, 0.22f, 0);
        head.Scale = new Vector3(1f, 1.3f, 1f);
        weapon.AddChild(head);

        // Add some knots/bumps on club
        var knotMesh = new SphereMesh { Radius = 0.02f, Height = 0.04f };
        for (int i = 0; i < 3; i++)
        {
            var knot = new MeshInstance3D { Mesh = knotMesh, MaterialOverride = woodMaterial };
            float angle = i * Mathf.Pi * 2f / 3f;
            knot.Position = new Vector3(Mathf.Cos(angle) * 0.05f, 0.2f + i * 0.03f, Mathf.Sin(angle) * 0.05f);
            weapon.AddChild(knot);
        }

        return weapon;
    }

    private Node3D CreateAxe(StandardMaterial3D metalMaterial, StandardMaterial3D handleMaterial)
    {
        var weapon = new Node3D();

        // Handle (wooden shaft)
        var handle = new MeshInstance3D();
        var handleMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.022f, Height = 0.3f };
        handle.Mesh = handleMesh;
        handle.MaterialOverride = handleMaterial;
        handle.Position = new Vector3(0, 0.05f, 0);
        weapon.AddChild(handle);

        // Axe head (curved blade)
        var head = new MeshInstance3D();
        var headMesh = new BoxMesh { Size = new Vector3(0.12f, 0.1f, 0.02f) };
        head.Mesh = headMesh;
        head.MaterialOverride = metalMaterial;
        head.Position = new Vector3(0.04f, 0.22f, 0);
        weapon.AddChild(head);

        // Blade edge (sharpened edge on one side)
        var edge = new MeshInstance3D();
        var edgeMesh = new PrismMesh { Size = new Vector3(0.02f, 0.1f, 0.02f) };
        edge.Mesh = edgeMesh;
        edge.MaterialOverride = metalMaterial;
        edge.Position = new Vector3(0.11f, 0.22f, 0);
        edge.RotationDegrees = new Vector3(0, 0, -90);
        weapon.AddChild(edge);

        // Socket (where handle meets head)
        var socket = new MeshInstance3D();
        var socketMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.04f };
        socket.Mesh = socketMesh;
        socket.MaterialOverride = metalMaterial;
        socket.Position = new Vector3(0, 0.18f, 0);
        weapon.AddChild(socket);

        return weapon;
    }

    private void CreateTorchOnLeftHand()
    {
        // Create torch container attached to left hand position
        // Positioned so the grip is at the hand and torch head extends upward
        _torchNode = new Node3D();
        _torchNode.Name = "Torch";
        _torchNode.Position = new Vector3(-0.33f, 0.15f, 0.2f); // Left hand position (matches right hand)
        _torchNode.RotationDegrees = new Vector3(-30, 15, -25); // Angled to look natural in grip

        // Torch handle (wooden stick) - centered at grip point
        var handle = new MeshInstance3D();
        var handleMesh = new CylinderMesh();
        handleMesh.TopRadius = 0.02f;
        handleMesh.BottomRadius = 0.025f;
        handleMesh.Height = 0.35f;
        handle.Mesh = handleMesh;
        var handleMat = new StandardMaterial3D();
        handleMat.AlbedoColor = new Color(0.35f, 0.25f, 0.15f); // Wood color
        handleMat.Roughness = 0.9f;
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0.1f, 0); // Offset so grip point is at origin
        _torchNode.AddChild(handle);

        // Torch head (wrapped cloth/oil soaked material) - at top of handle
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh();
        headMesh.Radius = 0.05f;
        headMesh.Height = 0.1f;
        head.Mesh = headMesh;
        var headMat = new StandardMaterial3D();
        headMat.AlbedoColor = new Color(0.3f, 0.2f, 0.1f);
        headMat.Roughness = 1f;
        head.MaterialOverride = headMat;
        head.Position = new Vector3(0, 0.3f, 0); // Top of handle
        _torchNode.AddChild(head);

        // Flame mesh (emissive sphere)
        var flame = new MeshInstance3D();
        var flameMesh = new SphereMesh();
        flameMesh.Radius = 0.07f;
        flameMesh.Height = 0.14f;
        flame.Mesh = flameMesh;
        var flameMat = new StandardMaterial3D();
        flameMat.AlbedoColor = new Color(1f, 0.6f, 0.1f);
        flameMat.EmissionEnabled = true;
        flameMat.Emission = new Color(1f, 0.5f, 0.1f);
        flameMat.EmissionEnergyMultiplier = 3f;
        flameMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        flameMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        flameMat.AlbedoColor = new Color(1f, 0.6f, 0.1f, 0.8f);
        flame.MaterialOverride = flameMat;
        flame.Position = new Vector3(0, 0.36f, 0);
        flame.Scale = new Vector3(1f, 1.3f, 1f); // Elongated upward
        _torchNode.AddChild(flame);

        // Inner flame (brighter yellow core)
        var innerFlame = new MeshInstance3D();
        var innerFlameMesh = new SphereMesh();
        innerFlameMesh.Radius = 0.035f;
        innerFlame.Mesh = innerFlameMesh;
        var innerFlameMat = new StandardMaterial3D();
        innerFlameMat.AlbedoColor = new Color(1f, 0.9f, 0.4f);
        innerFlameMat.EmissionEnabled = true;
        innerFlameMat.Emission = new Color(1f, 0.9f, 0.5f);
        innerFlameMat.EmissionEnergyMultiplier = 5f;
        innerFlameMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        innerFlame.MaterialOverride = innerFlameMat;
        innerFlame.Position = new Vector3(0, 0.38f, 0);
        _torchNode.AddChild(innerFlame);

        _meshInstance?.AddChild(_torchNode);

        // Create dynamic light from torch
        _torchLight = new OmniLight3D();
        _torchLight.Position = new Vector3(0, 0.4f, 0);
        _torchLight.LightColor = new Color(1f, 0.7f, 0.3f); // Warm orange
        _torchLight.LightEnergy = 2.5f;
        _torchLight.OmniRange = 12f;
        _torchLight.OmniAttenuation = 1.2f;
        // PERFORMANCE: Moving lights should not cast shadows - very expensive
        _torchLight.ShadowEnabled = false;
        _torchNode.AddChild(_torchLight);

        GD.Print($"[BasicEnemy3D] Created torch for torchbearer at {GlobalPosition}");
    }

    private void UpdateTorchFlicker(float delta)
    {
        if (_torchLight == null || !_hasTorch) return;

        _torchFlickerTimer += delta * 15f;

        // Randomized flickering effect
        float flicker = 1f + Mathf.Sin(_torchFlickerTimer) * 0.15f +
                        Mathf.Sin(_torchFlickerTimer * 2.3f) * 0.1f +
                        (float)GD.RandRange(-0.05, 0.05);

        _torchLight.LightEnergy = 2.5f * flicker;

        // Slight color variation
        float warmth = 0.7f + Mathf.Sin(_torchFlickerTimer * 0.7f) * 0.05f;
        _torchLight.LightColor = new Color(1f, warmth, 0.3f);
    }

    private void SetupNavigation()
    {
        _navAgent = new NavigationAgent3D();
        _navAgent.PathDesiredDistance = 0.5f;
        _navAgent.TargetDesiredDistance = 1.5f;
        // PERFORMANCE: Disable avoidance by default - it's expensive
        // Only enable when actively chasing player in aggro state
        _navAgent.AvoidanceEnabled = false;
        _navAgent.Radius = 0.5f;
        // PERFORMANCE: Reduce path query frequency
        _navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.Corridorfunnel;
        AddChild(_navAgent);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Update global chat cooldown (once per frame, any enemy can do this)
        MonsterChatDatabase.UpdateCooldown(dt);

        // Stasis mode - only play idle animation, no AI processing
        if (_isInStasis)
        {
            UpdateAnimation(dt);
            return;
        }

        // PERFORMANCE: Sleeping enemies do almost nothing
        if (_isSleeping)
        {
            _sleepCheckTimer -= dt;
            if (_sleepCheckTimer <= 0)
            {
                CheckWakeCondition();
                _sleepCheckTimer = SleepCheckInterval;
            }
            return;
        }

        // Dead enemies only decay hit flash
        if (CurrentState == State.Dead)
        {
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= dt;
                UpdateVisuals();
            }
            return;
        }

        // Frozen enemies just update visuals
        if (_isFrozen)
        {
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= dt;
                UpdateVisuals();
            }
            return;
        }

        // Try to find player if we don't have a reference
        if (_player == null)
        {
            _player = FPSController.Instance;
        }

        // Hit flash decay
        if (_hitFlashTimer > 0)
        {
            _hitFlashTimer -= dt;
            UpdateVisuals();
        }

        // PERFORMANCE: Get distance to player once for multiple checks
        float distToPlayerSq = _player != null ? GlobalPosition.DistanceSquaredTo(_player.GlobalPosition) : 10000f;
        const float AnimationCullDistSq = 30f * 30f;  // Don't animate beyond 30m
        const float HealthBarCullDistSq = 25f * 25f;  // Don't update health bar billboard beyond 25m

        // Update health bar visibility (respects nameplate toggle - always visible when enabled)
        if (_healthBarContainer != null)
        {
            bool shouldShow = GameManager3D.NameplatesEnabled && distToPlayerSq < HealthBarCullDistSq;
            _healthBarContainer.Visible = shouldShow;
            if (shouldShow)
            {
                UpdateHealthBar(); // Keep billboarding only when visible
            }
        }

        // Update animations (only if close enough to see)
        if (distToPlayerSq < AnimationCullDistSq)
        {
            UpdateAnimation(dt);
        }

        // Update torch flicker if we have one (only if close)
        if (distToPlayerSq < AnimationCullDistSq)
        {
            UpdateTorchFlicker(dt);
        }

        // Process enrage buff
        ProcessEnrage(dt);

        // Check sleep condition
        CheckSleepCondition(dt);

        // Apply gravity
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Constants.Gravity * dt, Velocity.Z);
        }

        // Update attack cooldown
        if (_attackTimer > 0) _attackTimer -= dt;

        // Update static prop cache timer (only needs one enemy to decrement)
        if (_propCacheRefreshTimer > 0) _propCacheRefreshTimer -= dt;

        // Check for feared state - override normal behavior
        if (_isFeared)
        {
            ProcessFeared(dt);
            MoveAndSlide();
            return;
        }

        // Get speed multiplier from Engine of Tomorrow
        float speedMultiplier = EngineOfTomorrow3D.GetGlobalSlowMultiplier();

        // Process current state
        switch (CurrentState)
        {
            case State.Idle:
                ProcessIdle(dt);
                break;
            case State.IdleInteracting:
                ProcessIdleInteracting(dt);
                break;
            case State.Patrolling:
                ProcessPatrolling(dt);
                break;
            case State.Tracking:
                ProcessTracking(dt);
                break;
            case State.Aggro:
                ProcessAggro(dt);
                break;
            case State.Attacking:
                ProcessAttacking(dt);
                break;
        }

        // Apply movement
        MoveAndSlide();
    }

    private void ProcessIdle(float dt)
    {
        _stateTimer -= dt;
        _socialChatTimer -= dt;

        // Check for player aggro - requires line of sight
        if (CheckPlayerDistance() <= AggroRange && HasLineOfSightToPlayer())
        {
            ChangeState(State.Aggro);
            return;
        }

        // Social chat check (occasional monster-to-monster chat)
        if (_socialChatTimer <= 0 && !_isSocialChatting)
        {
            _socialChatTimer = SocialChatCheckInterval;
            if (TrySocialChat())
            {
                return; // Entered IdleInteracting for chat
            }
        }

        // Goblins have a chance to interact with nearby props
        if (_stateTimer <= 0)
        {
            // 40% chance for goblins to try interacting with props, otherwise patrol
            bool isGoblin = MonsterType.ToLower().Contains("goblin") || MonsterType.ToLower() == "torchbearer";
            if (isGoblin && GD.Randf() < 0.4f)
            {
                if (TryFindInteractionTarget())
                {
                    ChangeState(State.IdleInteracting);
                    return;
                }
            }
            ChangeState(State.Patrolling);
        }
    }

    private void ProcessIdleInteracting(float dt)
    {
        // Check for player aggro first - always takes priority, but requires LOS
        if (CheckPlayerDistance() <= AggroRange && HasLineOfSightToPlayer())
        {
            _interactionTarget = null;
            _currentInteraction = "";
            EndSocialChat(); // Clean up any active social chat
            ChangeState(State.Aggro);
            return;
        }

        // If we have no target, go back to idle
        if (_interactionTarget == null || !IsInstanceValid(_interactionTarget))
        {
            EndSocialChat(); // Clean up any active social chat
            ChangeState(State.Idle);
            return;
        }

        float distToTarget = GlobalPosition.DistanceTo(_interactionTarget.GlobalPosition);

        // If not at target yet, walk toward it
        if (distToTarget > 1.5f)
        {
            Vector3 direction = (_interactionTarget.GlobalPosition - GlobalPosition).Normalized();
            direction.Y = 0;

            // Apply obstacle avoidance
            Vector3 moveDir = AvoidObstacles(direction);

            float slowMult = EngineOfTomorrow3D.GetGlobalSlowMultiplier();
            float speed = PatrolSpeed * slowMult;
            Velocity = new Vector3(moveDir.X * speed, Velocity.Y, moveDir.Z * speed);

            // Face direction of movement
            if (direction.LengthSquared() > 0.01f)
            {
                float targetAngle = Mathf.Atan2(direction.X, direction.Z);
                Rotation = new Vector3(0, targetAngle, 0);
            }
        }
        else
        {
            // At target - perform interaction animation
            Velocity = new Vector3(0, Velocity.Y, 0);

            // Face the prop
            Vector3 toTarget = (_interactionTarget.GlobalPosition - GlobalPosition).Normalized();
            toTarget.Y = 0;
            if (toTarget.LengthSquared() > 0.01f)
            {
                float targetAngle = Mathf.Atan2(toTarget.X, toTarget.Z);
                Rotation = new Vector3(0, targetAngle, 0);
            }

            _interactionTimer -= dt;

            // Interaction complete
            if (_interactionTimer <= 0)
            {
                _interactionTarget = null;
                _currentInteraction = "";
                EndSocialChat(); // Clean up any active social chat

                // 50% chance to patrol, 50% chance to find another interaction
                if (GD.Randf() < 0.5f && TryFindInteractionTarget())
                {
                    // Stay in IdleInteracting with new target
                }
                else
                {
                    ChangeState(State.Patrolling);
                }
            }
        }
    }

    private bool TryFindInteractionTarget()
    {
        // Use static prop cache for performance (avoids tree search per enemy)
        EnsurePropCacheValid();

        Node3D? bestTarget = null;
        float bestDist = InteractionSearchRadius;

        foreach (var prop in _propCache)
        {
            if (!GodotObject.IsInstanceValid(prop)) continue;

            float dist = GlobalPosition.DistanceTo(prop.GlobalPosition);
            if (dist < bestDist)
            {
                // Check if this is an interactable prop type
                string propType = GetPropType(prop);
                if (IsInteractableProp(propType))
                {
                    // Random selection to avoid all goblins going to same prop
                    if (GD.Randf() < 0.5f || bestTarget == null)
                    {
                        bestTarget = prop;
                        bestDist = dist;
                    }
                }
            }
        }

        if (bestTarget != null)
        {
            _interactionTarget = bestTarget;
            _currentInteraction = GetInteractionType(GetPropType(bestTarget));
            _interactionTimer = 2f + (float)GD.Randf() * 3f; // Interact for 2-5 seconds
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ensures the static prop cache is valid. Rebuilds if not initialized or stale.
    /// </summary>
    private void EnsurePropCacheValid()
    {
        // Rebuild cache if not initialized or every 30 seconds
        if (!_propCacheInitialized || _propCacheRefreshTimer <= 0)
        {
            RebuildPropCache();
            _propCacheRefreshTimer = 30f; // Refresh every 30 seconds
            _propCacheInitialized = true;
        }
    }

    /// <summary>
    /// Static method to rebuild the prop cache. Called once, shared by all enemies.
    /// </summary>
    private void RebuildPropCache()
    {
        _propCache.Clear();

        // First try the Props group
        var props = GetTree().GetNodesInGroup("Props");
        if (props.Count > 0)
        {
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] is Node3D prop3D)
                {
                    _propCache.Add(prop3D);
                }
            }
            return;
        }

        // Fallback: search for Cosmetic3D nodes (done once, not per enemy)
        var mainNode = GetTree().Root.GetNode("Main");
        if (mainNode == null) return;

        var allNodes = mainNode.FindChildren("*", "StaticBody3D", true, false);
        if (allNodes != null)
        {
            foreach (var node in allNodes)
            {
                if (node is SafeRoom3D.Environment.Cosmetic3D cosmetic)
                {
                    _propCache.Add(cosmetic);
                }
            }
        }

        GD.Print($"[BasicEnemy3D] Prop cache rebuilt: {_propCache.Count} props found");
    }

    /// <summary>
    /// Call this from GameManager when dungeon is regenerated to invalidate cache.
    /// </summary>
    public static void InvalidatePropCache()
    {
        _propCacheInitialized = false;
        _propCache.Clear();
    }

    private string GetPropType(Node3D prop)
    {
        if (prop is SafeRoom3D.Environment.Cosmetic3D cosmetic)
        {
            return cosmetic.ShapeType;
        }
        return prop.Name.ToString().ToLower();
    }

    private bool IsInteractableProp(string propType)
    {
        // Props that goblins can interact with
        return propType switch
        {
            "chair" => true,
            "table" => true,
            "chest" => true,
            "barrel" => true,
            "cauldron" => true,
            "skull_pile" => true,
            "crate" => true,
            _ => false
        };
    }

    private string GetInteractionType(string propType)
    {
        // What the goblin does at each prop type
        return propType switch
        {
            "chair" => "sitting",
            "table" => "examining",
            "chest" => "rummaging",
            "barrel" => "checking",
            "cauldron" => "stirring",
            "skull_pile" => "admiring",
            "crate" => "searching",
            _ => "looking"
        };
    }

    private void ProcessPatrolling(float dt)
    {
        // Check for player aggro - requires line of sight
        if (CheckPlayerDistance() <= AggroRange && HasLineOfSightToPlayer())
        {
            ChangeState(State.Aggro);
            return;
        }

        // Wait at patrol point
        if (_patrolWaitTimer > 0)
        {
            _patrolWaitTimer -= dt;
            Velocity = new Vector3(0, Velocity.Y, 0);
            if (_patrolWaitTimer <= 0)
            {
                PickNewPatrolTarget();
            }
            return;
        }

        // Move toward patrol target
        Vector3 direction = (_patrolTarget - GlobalPosition).Normalized();
        direction.Y = 0; // Keep on ground

        // Apply slow from Engine of Tomorrow
        float slowMult = EngineOfTomorrow3D.GetGlobalSlowMultiplier();

        if (GlobalPosition.DistanceTo(_patrolTarget) < 1f)
        {
            _patrolWaitTimer = Constants.EnemyPatrolWaitTime;
        }
        else
        {
            // Apply obstacle avoidance
            direction = AvoidObstacles(direction);

            float speed = PatrolSpeed * slowMult;
            Velocity = new Vector3(direction.X * speed, Velocity.Y, direction.Z * speed);

            // Face direction of movement (model's face is at +Z)
            if (direction.LengthSquared() > 0.01f)
            {
                float targetAngle = Mathf.Atan2(direction.X, direction.Z);
                Rotation = new Vector3(0, targetAngle, 0);
            }
        }
    }

    private void ProcessTracking(float dt)
    {
        // Slow movement toward last known player position
        float dist = CheckPlayerDistance();

        // Re-aggro only if player visible
        if (dist <= AggroRange && HasLineOfSightToPlayer())
        {
            ChangeState(State.Aggro);
            return;
        }

        if (dist > DeaggroRange)
        {
            ChangeState(State.Idle);
            return;
        }

        // Move slowly toward player
        if (_player != null)
        {
            Vector3 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
            direction.Y = 0;

            // Apply obstacle avoidance
            Vector3 moveDir = AvoidObstacles(direction);

            float slowMult = EngineOfTomorrow3D.GetGlobalSlowMultiplier();
            float speed = PatrolSpeed * slowMult;
            Velocity = new Vector3(moveDir.X * speed, Velocity.Y, moveDir.Z * speed);

            // Face direction of movement (model's face is at +Z)
            if (direction.LengthSquared() > 0.01f)
            {
                float targetAngle = Mathf.Atan2(direction.X, direction.Z);
                Rotation = new Vector3(0, targetAngle, 0);
            }
        }
    }

    private void ProcessAggro(float dt)
    {
        float dist = CheckPlayerDistance();

        // Deaggro if too far
        if (dist > DeaggroRange)
        {
            ChangeState(State.Tracking);
            return;
        }

        // Attack if in range
        if (dist <= AttackRange && _attackTimer <= 0)
        {
            ChangeState(State.Attacking);
            return;
        }

        // Chase player - but stop at MinStopDistance so player can see health bar and combat text
        if (_player != null)
        {
            Vector3 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
            direction.Y = 0;

            // Only move if we're further than the minimum stop distance
            if (dist > MinStopDistance)
            {
                // Apply obstacle avoidance
                Vector3 moveDir = AvoidObstacles(direction);

                float slowMult = EngineOfTomorrow3D.GetGlobalSlowMultiplier();
                float speed = MoveSpeed * slowMult;
                Velocity = new Vector3(moveDir.X * speed, Velocity.Y, moveDir.Z * speed);
            }
            else
            {
                // Stop moving but maintain facing
                Velocity = new Vector3(0, Velocity.Y, 0);
            }

            // Face player - model's face is at +Z, so point +Z toward player
            if (direction.LengthSquared() > 0.01f)
            {
                float targetAngle = Mathf.Atan2(direction.X, direction.Z);
                Rotation = new Vector3(0, targetAngle, 0);
            }
        }
    }

    private void ProcessFeared(float dt)
    {
        // Run away from fear source (usually player position)
        Vector3 fleeDir = (GlobalPosition - _fearFromPosition).Normalized();
        fleeDir.Y = 0;

        if (fleeDir.LengthSquared() < 0.01f)
        {
            // If at same position, pick random direction
            float angle = GD.Randf() * Mathf.Tau;
            fleeDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        }

        // Apply obstacle avoidance
        fleeDir = AvoidObstacles(fleeDir);

        // Move at normal speed away from fear source
        float slowMult = EngineOfTomorrow3D.GetGlobalSlowMultiplier();
        float speed = MoveSpeed * slowMult;
        Velocity = new Vector3(fleeDir.X * speed, Velocity.Y, fleeDir.Z * speed);

        // Face direction of movement (model's face is at +Z)
        if (fleeDir.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(fleeDir.X, fleeDir.Z);
            Rotation = new Vector3(0, targetAngle, 0);
        }
    }

    private void ProcessAttacking(float dt)
    {
        _stateTimer -= dt;

        if (_stateTimer <= 0)
        {
            // Perform attack
            PerformAttack();
            _attackTimer = AttackCooldown;
            ChangeState(State.Aggro);
        }
    }

    private void PerformAttack()
    {
        if (_player == null) return;

        // Play attack sound
        PlayMonsterSound("attack");

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (dist <= AttackRange * 1.2f)
        {
            // Hit player
            if (_player.HasMethod("TakeDamage"))
            {
                string sourceName = FormatMonsterName(MonsterType);
                _player.Call("TakeDamage", Damage, GlobalPosition, sourceName);
                GD.Print($"[BasicEnemy3D] {MonsterType} attacked player for {Damage} damage");
            }
        }
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

    private void ChangeState(State newState)
    {
        var previousState = CurrentState;
        CurrentState = newState;

        switch (newState)
        {
            case State.Idle:
            case State.IdleInteracting:
                _stateTimer = GD.Randf() * 2f + 1f;
                Velocity = new Vector3(0, Velocity.Y, 0);
                // Play idle animation (variant 0=breathing, 1=alert, 2=aggressive)
                PlayAnimation(AnimationType.Idle, GD.RandRange(0, 1)); // Mostly calm idles
                break;

            case State.Sleeping:
                Velocity = new Vector3(0, Velocity.Y, 0);
                // Sleeping uses breathing idle
                PlayAnimation(AnimationType.Idle, 0);
                break;

            case State.Patrolling:
                PickNewPatrolTarget();
                // Play walk animation
                PlayAnimation(AnimationType.Walk);
                break;

            case State.Tracking:
                // Play walk animation when tracking
                PlayAnimation(AnimationType.Walk);
                break;

            case State.Aggro:
                // Play aggro sound when transitioning from non-combat states
                if (previousState == State.Idle || previousState == State.Patrolling ||
                    previousState == State.Sleeping || previousState == State.IdleInteracting)
                {
                    PlayMonsterSound("aggro");
                }
                // Play run animation when in aggro (chasing player)
                PlayAnimation(AnimationType.Run);
                // Use aggressive idle variant (2) when stationary but aggro'd
                break;

            case State.Attacking:
                _stateTimer = 0.3f; // Wind-up time
                Velocity = new Vector3(0, Velocity.Y, 0);
                // Play attack animation
                PlayAnimation(AnimationType.Attack);
                break;

            case State.Dead:
                Velocity = Vector3.Zero;
                // Play death animation
                PlayAnimation(AnimationType.Die);
                break;
        }
    }

    private void PickNewPatrolTarget()
    {
        float angle = GD.Randf() * Mathf.Tau;
        float dist = GD.Randf() * PatrolRadius;

        // Roamers patrol around their spawn position, normal enemies patrol around current position
        Vector3 patrolCenter = _isRoamer ? _spawnPosition : GlobalPosition;

        _patrolTarget = patrolCenter + new Vector3(
            Mathf.Cos(angle) * dist,
            0,
            Mathf.Sin(angle) * dist
        );
    }

    #region Obstacle Avoidance

    /// <summary>
    /// Apply obstacle avoidance to a desired movement direction.
    /// Returns adjusted direction that steers around obstacles.
    /// </summary>
    private Vector3 AvoidObstacles(Vector3 desiredDirection)
    {
        if (desiredDirection.LengthSquared() < 0.01f)
            return desiredDirection;

        var spaceState = GetWorld3D().DirectSpaceState;
        Vector3 origin = GlobalPosition + Vector3.Up * 0.5f;  // Waist height
        float rayLength = 1.2f;  // Look ahead distance

        // Collision mask: Obstacle (layer 5) + Wall (layer 8)
        uint obstacleMask = (1u << 4) | (1u << 7);

        // Cast forward ray
        bool forwardBlocked = CastObstacleRay(spaceState, origin, desiredDirection, rayLength, obstacleMask);

        if (!forwardBlocked)
        {
            return desiredDirection;  // Path is clear
        }

        // Forward is blocked - try to steer around
        // Calculate left and right directions (45 degrees from forward)
        Vector3 leftDir = new Vector3(
            desiredDirection.X * 0.707f - desiredDirection.Z * 0.707f,
            0,
            desiredDirection.X * 0.707f + desiredDirection.Z * 0.707f
        ).Normalized();

        Vector3 rightDir = new Vector3(
            desiredDirection.X * 0.707f + desiredDirection.Z * 0.707f,
            0,
            -desiredDirection.X * 0.707f + desiredDirection.Z * 0.707f
        ).Normalized();

        bool leftBlocked = CastObstacleRay(spaceState, origin, leftDir, rayLength, obstacleMask);
        bool rightBlocked = CastObstacleRay(spaceState, origin, rightDir, rayLength, obstacleMask);

        // Choose the clearer path
        if (!leftBlocked && !rightBlocked)
        {
            // Both sides clear, pick one based on position for consistency
            return ((int)(GlobalPosition.X + GlobalPosition.Z) % 2 == 0) ? leftDir : rightDir;
        }
        else if (!leftBlocked)
        {
            return leftDir;
        }
        else if (!rightBlocked)
        {
            return rightDir;
        }
        else
        {
            // All directions blocked - try perpendicular or reverse
            Vector3 perpDir = new Vector3(-desiredDirection.Z, 0, desiredDirection.X);
            if (!CastObstacleRay(spaceState, origin, perpDir, rayLength, obstacleMask))
            {
                return perpDir;
            }
            // Completely stuck - stay put
            return Vector3.Zero;
        }
    }

    /// <summary>
    /// Cast a ray to detect obstacles.
    /// </summary>
    private bool CastObstacleRay(PhysicsDirectSpaceState3D spaceState, Vector3 origin, Vector3 direction, float length, uint mask)
    {
        var query = PhysicsRayQueryParameters3D.Create(
            origin,
            origin + direction * length,
            mask
        );
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var result = spaceState.IntersectRay(query);
        return result.Count > 0;
    }

    #endregion

    #region Social Chat System

    /// <summary>
    /// Try to initiate a social chat with a nearby monster.
    /// </summary>
    private bool TrySocialChat()
    {
        // Check global cooldown
        if (!MonsterChatDatabase.CanInitiateChat())
            return false;

        // Find a nearby monster to chat with
        var partner = FindNearbyMonsterForChat();
        if (partner == null)
            return false;

        // Start the chat!
        _socialChatPartner = partner;
        _isSocialChatting = true;

        // Get dialogue lines
        var (line1, line2) = MonsterChatDatabase.GetChatLines(MonsterType, partner.MonsterType);

        // Show chat bubble for this monster
        ShowChatBubble(line1);

        // If partner has a response, show it after a short delay
        if (line2 != null)
        {
            partner._isSocialChatting = true;
            partner._socialChatPartner = this;
            // Partner shows their bubble slightly delayed (using CallDeferred with timer)
            partner.CallDeferred(nameof(ShowChatBubbleDelayed), line2, 1.2f);
        }

        // Log to combat log
        UI.HUD3D.Instance?.AddCombatLogMessage(
            $"[{GetDisplayName()}]: \"{line1}\"",
            new Color(0.7f, 0.55f, 0.85f) // Purple for monster chat
        );
        if (line2 != null)
        {
            UI.HUD3D.Instance?.AddCombatLogMessage(
                $"[{partner.GetDisplayName()}]: \"{line2}\"",
                new Color(0.7f, 0.55f, 0.85f)
            );
        }

        // Start global cooldown
        MonsterChatDatabase.StartChatCooldown();

        // Enter interaction state (face each other)
        _interactionTarget = partner;
        _interactionTimer = MonsterChatDatabase.ChatDuration;
        ChangeState(State.IdleInteracting);

        GD.Print($"[BasicEnemy3D] Social chat: {MonsterType} <-> {partner.MonsterType}");
        return true;
    }

    /// <summary>
    /// Find a nearby monster suitable for chatting.
    /// </summary>
    private BasicEnemy3D? FindNearbyMonsterForChat()
    {
        var enemies = GetTree().GetNodesInGroup("Enemies");
        BasicEnemy3D? bestCandidate = null;
        float bestDist = MonsterChatDatabase.ChatRange;

        foreach (var node in enemies)
        {
            if (node == this) continue;
            if (node is not BasicEnemy3D other) continue;
            if (!IsInstanceValid(other)) continue;

            // Don't chat with dead, sleeping, or already chatting monsters
            if (other.CurrentState == State.Dead || other._isSleeping || other._isSocialChatting)
                continue;

            // Only chat when both are idle or patrolling
            if (other.CurrentState != State.Idle && other.CurrentState != State.Patrolling)
                continue;

            float dist = GlobalPosition.DistanceTo(other.GlobalPosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCandidate = other;
            }
        }

        return bestCandidate;
    }

    /// <summary>
    /// Show a chat bubble above this monster.
    /// </summary>
    private void ShowChatBubble(string text)
    {
        // Remove old bubble if exists
        _chatBubble?.QueueFree();

        // Create new bubble above head
        float bubbleHeight = GetBubbleHeight();
        Vector3 bubblePos = GlobalPosition + new Vector3(0, bubbleHeight, 0);

        _chatBubble = MonsterChatBubble.Create(text, bubblePos, MonsterChatDatabase.ChatDuration);
        GetTree().Root.AddChild(_chatBubble);
    }

    /// <summary>
    /// Show a chat bubble after a delay (called via CallDeferred).
    /// </summary>
    private async void ShowChatBubbleDelayed(string text, float delay)
    {
        await ToSignal(GetTree().CreateTimer(delay), "timeout");
        if (IsInstanceValid(this))
        {
            ShowChatBubble(text);
        }
    }

    /// <summary>
    /// End the social chat interaction.
    /// </summary>
    private void EndSocialChat()
    {
        _isSocialChatting = false;
        _socialChatPartner = null;
        _chatBubble?.QueueFree();
        _chatBubble = null;
    }

    /// <summary>
    /// Get height for chat bubble based on monster type.
    /// </summary>
    private float GetBubbleHeight()
    {
        return MonsterType.ToLower() switch
        {
            "slime" => 1.0f,
            "bat" => 1.5f,
            "spider" => 1.2f,
            "mushroom" => 1.3f,
            "eye" => 1.4f,
            "dragon" or "dragon_king" => 2.5f,
            _ => 1.8f // Default for humanoids
        };
    }

    /// <summary>
    /// Get display name for combat log.
    /// </summary>
    private string GetDisplayName()
    {
        return MonsterType.Replace("_", " ").ToLower();
    }

    #endregion

    private float CheckPlayerDistance()
    {
        if (_player == null) return float.MaxValue;
        return GlobalPosition.DistanceTo(_player.GlobalPosition);
    }

    /// <summary>
    /// Check if there's a clear line of sight to the player (no walls blocking).
    /// Uses raycast from enemy eye height to player chest height.
    /// </summary>
    protected bool HasLineOfSightToPlayer()
    {
        if (_player == null) return false;

        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return false;

        // Raycast from enemy's "eye" position to player's chest
        Vector3 fromPos = GlobalPosition + new Vector3(0, 1.2f, 0);
        Vector3 toPos = _player.GlobalPosition + new Vector3(0, 1.0f, 0);

        var query = PhysicsRayQueryParameters3D.Create(fromPos, toPos);
        // Check against walls (layer 8 = bit 7 = 128) and obstacles (layer 5 = bit 4 = 16)
        query.CollisionMask = 128 | 16; // Wall + Obstacle layers
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);

        // If no hit, line of sight is clear
        return result.Count == 0;
    }

    /// <summary>
    /// Static helper: Check line of sight between two world positions.
    /// Returns true if there's nothing blocking the path (on wall/obstacle layers).
    /// </summary>
    public static bool CheckLineOfSight(Node3D fromNode, Vector3 fromPos, Vector3 toPos)
    {
        var spaceState = fromNode.GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return false;

        var query = PhysicsRayQueryParameters3D.Create(fromPos, toPos);
        // Check against walls (layer 8 = bit 7 = 128) and obstacles (layer 5 = bit 4 = 16)
        query.CollisionMask = 128 | 16;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);
        return result.Count == 0;
    }

    private void CheckSleepCondition(float dt)
    {
        if (CurrentState == State.Aggro || CurrentState == State.Attacking) return;

        float dist = CheckPlayerDistance();
        if (dist > SleepDistance)
        {
            EnterSleep();
        }
    }

    private void CheckWakeCondition()
    {
        float dist = CheckPlayerDistance();
        if (dist < WakeDistance)
        {
            ExitSleep();
        }
    }

    private void EnterSleep()
    {
        _isSleeping = true;
        _sleepCheckTimer = SleepCheckInterval;
    }

    private void ExitSleep()
    {
        _isSleeping = false;
        ChangeState(State.Idle);
    }

    private void UpdateAnimation(float dt)
    {
        _animTimer += dt;

        if (_meshInstance == null) return;

        // Update hit shake effect
        if (_hitShakeTimer > 0)
        {
            _hitShakeTimer -= dt;
            float shakeProgress = _hitShakeTimer / HitShakeDuration;
            float currentIntensity = _hitShakeIntensity * shakeProgress; // Decay over time

            // Rapid random wiggle on X and Z axes
            float shakeFreq = 45f; // Fast vibration
            _hitShakeOffset = new Vector3(
                Mathf.Sin(_animTimer * shakeFreq) * currentIntensity,
                Mathf.Sin(_animTimer * shakeFreq * 1.3f) * currentIntensity * 0.3f, // Less Y movement
                Mathf.Cos(_animTimer * shakeFreq * 0.9f) * currentIntensity
            );
        }
        else
        {
            _hitShakeOffset = Vector3.Zero;
        }

        // Determine animation based on state
        bool isMoving = Velocity.LengthSquared() > 0.1f;

        if (CurrentState == State.Dead)
        {
            // Death pose - fall over
            _meshInstance.Rotation = new Vector3(Mathf.Pi / 2f, _meshInstance.Rotation.Y, 0);
            return;
        }

        if (_isFrozen)
        {
            // Frozen - no animation
            return;
        }

        if (CurrentState == State.Attacking || _isAttackAnimating)
        {
            // Attack animation - swing arm and weapon forward
            _isAttackAnimating = true;
            _attackSwingAngle += dt * 15f;

            if (_attackSwingAngle > Mathf.Pi)
            {
                _attackSwingAngle = 0;
                _isAttackAnimating = false;
            }
            else
            {
                float swingAmount = Mathf.Sin(_attackSwingAngle) * 1.2f;

                // Right arm swings forward with weapon
                if (_rightArmNode != null)
                {
                    _rightArmNode.Rotation = new Vector3(-swingAmount, 0, 0);
                }
                if (_weaponNode != null)
                {
                    _weaponNode.Rotation = new Vector3(-swingAmount * 0.8f, 0, -0.5f);
                }

                // Left arm pulls back slightly during attack
                if (_leftArmNode != null)
                {
                    _leftArmNode.Rotation = new Vector3(swingAmount * 0.3f, 0, 0);
                }

                // Legs plant firmly during attack
                if (_leftLegNode != null)
                {
                    _leftLegNode.Rotation = new Vector3(-0.15f, 0, 0); // Slight forward lean
                }
                if (_rightLegNode != null)
                {
                    _rightLegNode.Rotation = new Vector3(0.1f, 0, 0); // Back leg braces
                }
            }
        }
        else if (isMoving)
        {
            // Walking animation - bob up and down
            _walkBobAmount = Mathf.Sin(_animTimer * 12f) * 0.04f;
            _meshInstance.Position = new Vector3(0, _walkBobAmount + 0.02f, 0) + _hitShakeOffset;

            // Slight side-to-side sway (hips swaying)
            float sway = Mathf.Sin(_animTimer * 6f) * 0.03f;
            _meshInstance.Rotation = new Vector3(0, 0, sway);

            // Arm swing during walk - arms swing opposite to legs
            float armSwingAngle = Mathf.Sin(_animTimer * 12f) * 0.6f;
            float legSwingAngle = Mathf.Sin(_animTimer * 12f) * 0.5f;

            // Right arm swings forward when left leg goes forward
            if (_rightArmNode != null)
            {
                _rightArmNode.Rotation = new Vector3(armSwingAngle, 0, 0);
            }
            // Left arm swings opposite
            if (_leftArmNode != null)
            {
                _leftArmNode.Rotation = new Vector3(-armSwingAngle, 0, 0);
            }

            // Leg swing - alternating forward/back motion
            if (_rightLegNode != null)
            {
                _rightLegNode.Rotation = new Vector3(-legSwingAngle, 0, 0);
            }
            if (_leftLegNode != null)
            {
                _leftLegNode.Rotation = new Vector3(legSwingAngle, 0, 0);
            }
        }
        else if (CurrentState == State.IdleInteracting && _interactionTarget != null)
        {
            // Interaction animations based on what we're doing
            _idleBreathAmount = Mathf.Sin(_animTimer * 2f) * 0.01f;
            _meshInstance.Position = new Vector3(0, _idleBreathAmount, 0) + _hitShakeOffset;

            switch (_currentInteraction)
            {
                case "sitting":
                    // Lean back slightly, arms at rest
                    _meshInstance.Rotation = new Vector3(-0.15f, 0, 0);
                    if (_rightArmNode != null) _rightArmNode.Rotation = new Vector3(0.3f, 0, 0);
                    if (_leftArmNode != null) _leftArmNode.Rotation = new Vector3(0.3f, 0, 0);
                    break;

                case "rummaging":
                case "searching":
                    // Hands moving around like searching
                    float searchSwing = Mathf.Sin(_animTimer * 4f) * 0.4f;
                    if (_rightArmNode != null) _rightArmNode.Rotation = new Vector3(-0.8f + searchSwing, 0, 0);
                    if (_leftArmNode != null) _leftArmNode.Rotation = new Vector3(-0.8f - searchSwing, 0, 0);
                    _meshInstance.Rotation = new Vector3(0.2f, Mathf.Sin(_animTimer * 2f) * 0.1f, 0);
                    break;

                case "stirring":
                    // Circular stirring motion
                    float stirAngle = _animTimer * 3f;
                    if (_rightArmNode != null) _rightArmNode.Rotation = new Vector3(-0.5f + Mathf.Sin(stirAngle) * 0.2f, Mathf.Cos(stirAngle) * 0.2f, 0);
                    if (_leftArmNode != null) _leftArmNode.Rotation = new Vector3(0.1f, 0, 0);
                    break;

                case "examining":
                    // Looking down and around
                    _meshInstance.Rotation = new Vector3(0.15f, Mathf.Sin(_animTimer * 1.5f) * 0.2f, 0);
                    float examineArm = Mathf.Sin(_animTimer * 2f) * 0.2f;
                    if (_rightArmNode != null) _rightArmNode.Rotation = new Vector3(-0.3f + examineArm, 0, 0);
                    if (_leftArmNode != null) _leftArmNode.Rotation = new Vector3(-0.2f, 0, 0);
                    break;

                case "admiring":
                    // Head tilting while admiring skulls
                    _meshInstance.Rotation = new Vector3(0.1f, 0, Mathf.Sin(_animTimer * 1f) * 0.1f);
                    if (_rightArmNode != null) _rightArmNode.Rotation = new Vector3(0.1f, 0, 0);
                    if (_leftArmNode != null) _leftArmNode.Rotation = new Vector3(0.1f, 0, 0);
                    break;

                default:
                    // Generic looking animation
                    _meshInstance.Rotation = new Vector3(0, Mathf.Sin(_animTimer * 2f) * 0.15f, 0);
                    break;
            }

            // Reset leg positions during interaction
            if (_rightLegNode != null) _rightLegNode.Rotation = Vector3.Zero;
            if (_leftLegNode != null) _leftLegNode.Rotation = Vector3.Zero;
        }
        else
        {
            // Idle animation - gentle breathing and subtle sway
            _idleBreathAmount = Mathf.Sin(_animTimer * 2f) * 0.015f;
            _meshInstance.Position = new Vector3(0, _idleBreathAmount, 0) + _hitShakeOffset;

            // Subtle idle sway
            float idleSway = Mathf.Sin(_animTimer * 1.5f) * 0.01f;
            _meshInstance.Rotation = new Vector3(0, 0, idleSway);

            // Subtle arm sway in idle
            float idleArmSway = Mathf.Sin(_animTimer * 1.8f) * 0.05f;
            if (_rightArmNode != null)
            {
                _rightArmNode.Rotation = new Vector3(idleArmSway, 0, 0);
            }
            if (_leftArmNode != null)
            {
                _leftArmNode.Rotation = new Vector3(-idleArmSway * 0.5f, 0, 0);
            }

            // Reset leg positions
            if (_rightLegNode != null)
            {
                _rightLegNode.Rotation = Vector3.Zero;
            }
            if (_leftLegNode != null)
            {
                _leftLegNode.Rotation = Vector3.Zero;
            }

            if (_weaponNode != null)
            {
                _weaponNode.Rotation = new Vector3(0, 0, -0.5f);
            }
        }
    }

    // Overload for Godot's Call() which doesn't support C# optional parameters
    public void TakeDamage(float damage, Vector3 fromPosition, string source)
    {
        TakeDamage(damage, fromPosition, source, false, 1f);
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source, bool isCrit)
    {
        TakeDamage(damage, fromPosition, source, isCrit, 1f);
    }

    /// <summary>
    /// Full TakeDamage with weapon pushback multiplier.
    /// </summary>
    /// <param name="pushbackMultiplier">Weapon-specific pushback (0.3 for bow, 2.0 for war hammer)</param>
    public void TakeDamage(float damage, Vector3 fromPosition, string source, bool isCrit, float pushbackMultiplier)
    {
        if (CurrentState == State.Dead) return;

        int intDamage = (int)damage;
        CurrentHealth -= intDamage;

        // Wake from sleep
        if (_isSleeping) ExitSleep();

        // Aggro on damage
        if (CurrentState != State.Aggro && CurrentState != State.Attacking)
        {
            ChangeState(State.Aggro);
        }

        // Visual feedback
        _hitFlashTimer = 0.15f;

        // Hit shake effect (intensity scales with damage proportion and pushback)
        float damageRatio = Mathf.Min(1f, damage / (MaxHealth * 0.25f)); // 25% HP hit = full intensity
        _hitShakeTimer = HitShakeDuration;
        _hitShakeIntensity = HitShakeBaseIntensity * (0.5f + damageRatio * 0.5f) * Mathf.Clamp(pushbackMultiplier, 0.5f, 1.5f);

        // Play hit reaction animation (doesn't change state)
        if (_animPlayer != null && CurrentState != State.Dead)
        {
            PlayAnimation(AnimationType.Hit);
        }

        // Update health bar visuals (not billboard - that's updated periodically)
        UpdateHealthBarVisuals();

        // Spawn floating damage text with crit info
        SpawnFloatingDamageText(intDamage, isCrit);

        // Play monster-specific hit sound
        PlayMonsterSound("hit");

        // Knockback with weapon pushback multiplier
        Vector3 knockDir = (GlobalPosition - fromPosition).Normalized();
        knockDir.Y = 0;
        Velocity += knockDir * Constants.KnockbackForce * pushbackMultiplier;

        EmitSignal(SignalName.Damaged, intDamage, CurrentHealth);

        GD.Print($"[BasicEnemy3D] {MonsterType} took {intDamage} damage from {source}. HP: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        CurrentState = State.Dead;
        CollisionLayer = 0;
        CollisionMask = 0;

        EmitSignal(SignalName.Died, this);

        // Play death animation
        PlayAnimation(AnimationType.Die);

        // Play monster-specific death sound
        PlayMonsterSound("die");

        GD.Print($"[BasicEnemy3D] {MonsterType} died!");

        // Award XP to player
        if (FPSController.Instance != null && ExperienceReward > 0)
        {
            FPSController.Instance.AddExperience(ExperienceReward);
            GD.Print($"[BasicEnemy3D] Awarded {ExperienceReward} XP to player (Level {Level} {MonsterType})");

            // Log death to combat log
            string displayName = FormatMonsterName(MonsterType);
            HUD3D.Instance?.LogEnemyDeath(displayName, ExperienceReward);

            // Register kill for achievements
            HUD3D.Instance?.RegisterKill(MonsterType, false);

            // Notify AI broadcaster of kill
            AIBroadcaster.Instance?.OnMonsterKilled(MonsterType);

            // Record kill in game stats
            GameStats.Instance?.RecordKill(MonsterType);

            // Update quest progress for monster kills
            bool isBoss = IsBossMonster(MonsterType);
            Core.QuestManager.Instance?.OnMonsterKilled(MonsterType, isBoss);
        }

        // Wait for death animation to complete before spawning corpse
        // Death animations are 2.0-3.0 seconds, use 2.5s as safe delay
        float deathAnimDuration = 2.5f;

        var tween = CreateTween();
        // Wait for death animation to finish
        tween.TweenInterval(deathAnimDuration);
        // Then spawn corpse and remove enemy
        tween.TweenCallback(Callable.From(() =>
        {
            SpawnCorpse();
            QueueFree();
        }));
    }

    private void SpawnCorpse()
    {
        var corpse = Corpse3D.Create(MonsterType, false, GlobalPosition, Rotation.Y, Level);
        GetTree().Root.AddChild(corpse);
        GD.Print($"[BasicEnemy3D] Spawned corpse for {MonsterType} (Level {Level})");
    }

    /// <summary>
    /// Check if a monster type is a boss.
    /// </summary>
    private static bool IsBossMonster(string monsterType)
    {
        return monsterType.ToLower() switch
        {
            "skeleton_lord" or "dragon_king" or "spider_queen" or "the_butcher" or
            "princess_donut" or "mongo" or "zev" or "mordecai" => true,
            _ => false
        };
    }

    /// <summary>
    /// Play a monster-specific sound using the MonsterSounds system.
    /// Falls back to generic sounds if no specific sound is defined.
    /// </summary>
    protected void PlayMonsterSound(string action)
    {
        SoundManager3D.Instance?.PlayMonsterSoundAt(MonsterType.ToLower(), action, GlobalPosition);
    }

    private void UpdateVisuals()
    {
        if (_material == null) return;

        if (_hitFlashTimer > 0)
        {
            // Flash white when hit
            float flash = _hitFlashTimer / 0.15f;
            _material.AlbedoColor = _baseColor.Lerp(Colors.White, flash * 0.7f);
        }
        else if (_isFrozen)
        {
            // Ice blue when frozen
            _material.AlbedoColor = new Color(0.6f, 0.85f, 1f);
        }
        else if (_isFeared)
        {
            // Pale purple when feared
            _material.AlbedoColor = new Color(0.9f, 0.7f, 0.9f);
        }
        else
        {
            _material.AlbedoColor = _baseColor;
        }
    }

    public void SetFeared(bool feared)
    {
        _isFeared = feared;

        // Store fear source position (player position)
        if (feared && BansheesWail3D.Instance != null)
        {
            _fearFromPosition = BansheesWail3D.Instance.GetFleeFromPosition();
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Set frozen state (from Timestop Bubble).
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;

        // Stop all movement when frozen
        if (frozen)
        {
            Velocity = Vector3.Zero;
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Check if enemy is currently frozen.
    /// </summary>
    public bool IsFrozen() => _isFrozen;

    /// <summary>
    /// Check if enemy is currently feared.
    /// </summary>
    public bool IsFeared() => _isFeared;

    /// <summary>
    /// Check if enemy is in stasis (for In-Map Editor).
    /// </summary>
    public bool IsInStasis() => _isInStasis;

    /// <summary>
    /// Enter stasis mode - enemy plays idle animation but doesn't react to player.
    /// Used by In-Map Editor.
    /// </summary>
    public void EnterStasis()
    {
        if (_isInStasis) return;

        _isInStasis = true;
        CurrentState = State.Stasis;
        Velocity = Vector3.Zero;

        // Switch to idle animation
        if (_animPlayer != null)
        {
            PlayAnimation(AnimationType.Idle);
        }

        GD.Print($"[BasicEnemy3D] {MonsterType} entered stasis");
    }

    /// <summary>
    /// Exit stasis mode - enemy resumes normal AI behavior.
    /// </summary>
    public void ExitStasis()
    {
        if (!_isInStasis) return;

        _isInStasis = false;
        ChangeState(State.Idle);

        GD.Print($"[BasicEnemy3D] {MonsterType} exited stasis");
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.Y = 0;
        Velocity += direction.Normalized() * force;
    }

    // ==========================================
    // NEW MONSTER MESH CREATION METHODS
    // ==========================================

    private void CreateSkeletonMesh()
    {
        Color boneColor = new Color(0.9f, 0.88f, 0.8f);
        Color darkBone = boneColor.Darkened(0.2f);

        _material = new StandardMaterial3D();
        _material.AlbedoColor = boneColor;
        _material.Roughness = 0.9f;

        var darkMaterial = new StandardMaterial3D();
        darkMaterial.AlbedoColor = darkBone;

        var eyeMaterial = new StandardMaterial3D();
        eyeMaterial.AlbedoColor = new Color(0.8f, 0.2f, 0.1f);
        eyeMaterial.EmissionEnabled = true;
        eyeMaterial.Emission = new Color(1f, 0.3f, 0.1f) * 0.5f;

        // Skull
        var skull = new MeshInstance3D();
        var skullMesh = new SphereMesh { Radius = 0.18f, Height = 0.36f };
        skull.Mesh = skullMesh;
        skull.MaterialOverride = _material;
        skull.Position = new Vector3(0, 1.5f, 0);
        skull.Scale = new Vector3(0.9f, 1f, 0.85f);
        _meshInstance!.AddChild(skull);

        // Eye sockets (dark holes with glowing eyes inside)
        var leftSocket = new MeshInstance3D();
        var socketMesh = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        leftSocket.Mesh = socketMesh;
        leftSocket.MaterialOverride = eyeMaterial;
        leftSocket.Position = new Vector3(-0.06f, 1.52f, 0.12f);
        _meshInstance.AddChild(leftSocket);

        var rightSocket = new MeshInstance3D();
        rightSocket.Mesh = socketMesh;
        rightSocket.MaterialOverride = eyeMaterial;
        rightSocket.Position = new Vector3(0.06f, 1.52f, 0.12f);
        _meshInstance.AddChild(rightSocket);

        // Jaw
        var jaw = new MeshInstance3D();
        var jawMesh = new BoxMesh { Size = new Vector3(0.12f, 0.05f, 0.1f) };
        jaw.Mesh = jawMesh;
        jaw.MaterialOverride = _material;
        jaw.Position = new Vector3(0, 1.38f, 0.05f);
        _meshInstance.AddChild(jaw);

        // Ribcage (spine + ribs)
        var spine = new MeshInstance3D();
        var spineMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.5f };
        spine.Mesh = spineMesh;
        spine.MaterialOverride = _material;
        spine.Position = new Vector3(0, 1.0f, 0);
        _meshInstance.AddChild(spine);

        // Ribs
        for (int i = 0; i < 4; i++)
        {
            var rib = new MeshInstance3D();
            var ribMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.2f };
            rib.Mesh = ribMesh;
            rib.MaterialOverride = _material;
            float y = 1.15f - i * 0.1f;
            rib.Position = new Vector3(0.1f, y, 0.05f);
            rib.RotationDegrees = new Vector3(0, 0, 45);
            _meshInstance.AddChild(rib);

            var rib2 = new MeshInstance3D();
            rib2.Mesh = ribMesh;
            rib2.MaterialOverride = _material;
            rib2.Position = new Vector3(-0.1f, y, 0.05f);
            rib2.RotationDegrees = new Vector3(0, 0, -45);
            _meshInstance.AddChild(rib2);
        }

        // Pelvis
        var pelvis = new MeshInstance3D();
        var pelvisMesh = new BoxMesh { Size = new Vector3(0.25f, 0.1f, 0.1f) };
        pelvis.Mesh = pelvisMesh;
        pelvis.MaterialOverride = _material;
        pelvis.Position = new Vector3(0, 0.65f, 0);
        _meshInstance.AddChild(pelvis);

        // Arms (bones)
        CreateBoneArm(true, _material);
        CreateBoneArm(false, _material);

        // Legs (bones)
        CreateBoneLeg(true, _material);
        CreateBoneLeg(false, _material);
    }

    private void CreateBoneArm(bool isLeft, StandardMaterial3D material)
    {
        float side = isLeft ? -1 : 1;
        var arm = new Node3D();
        arm.Position = new Vector3(side * 0.2f, 1.15f, 0);

        var upperArm = new MeshInstance3D();
        var armMesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.02f, Height = 0.25f };
        upperArm.Mesh = armMesh;
        upperArm.MaterialOverride = material;
        upperArm.Position = new Vector3(0, -0.12f, 0);
        arm.AddChild(upperArm);

        var lowerArm = new MeshInstance3D();
        lowerArm.Mesh = armMesh;
        lowerArm.MaterialOverride = material;
        lowerArm.Position = new Vector3(0, -0.35f, 0);
        arm.AddChild(lowerArm);

        var hand = new MeshInstance3D();
        var handMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        hand.Mesh = handMesh;
        hand.MaterialOverride = material;
        hand.Position = new Vector3(0, -0.5f, 0);
        arm.AddChild(hand);

        _meshInstance!.AddChild(arm);
        if (isLeft) _leftArmNode = arm; else _rightArmNode = arm;
    }

    private void CreateBoneLeg(bool isLeft, StandardMaterial3D material)
    {
        float side = isLeft ? -1 : 1;
        var leg = new Node3D();
        leg.Position = new Vector3(side * 0.08f, 0.6f, 0);

        var upperLeg = new MeshInstance3D();
        var legMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.025f, Height = 0.3f };
        upperLeg.Mesh = legMesh;
        upperLeg.MaterialOverride = material;
        upperLeg.Position = new Vector3(0, -0.15f, 0);
        leg.AddChild(upperLeg);

        var lowerLeg = new MeshInstance3D();
        lowerLeg.Mesh = legMesh;
        lowerLeg.MaterialOverride = material;
        lowerLeg.Position = new Vector3(0, -0.45f, 0);
        leg.AddChild(lowerLeg);

        var foot = new MeshInstance3D();
        var footMesh = new BoxMesh { Size = new Vector3(0.06f, 0.03f, 0.12f) };
        foot.Mesh = footMesh;
        foot.MaterialOverride = material;
        foot.Position = new Vector3(0, -0.6f, 0.03f);
        leg.AddChild(foot);

        _meshInstance!.AddChild(leg);
        if (isLeft) _leftLegNode = leg; else _rightLegNode = leg;
    }

    private void CreateWolfMesh()
    {
        Color furColor = new Color(0.4f, 0.35f, 0.3f);
        Color darkFur = furColor.Darkened(0.3f);
        Color lightFur = furColor.Lightened(0.2f);

        _material = new StandardMaterial3D();
        _material.AlbedoColor = furColor;
        _material.Roughness = 0.95f;

        var darkMaterial = new StandardMaterial3D();
        darkMaterial.AlbedoColor = darkFur;

        var eyeMaterial = new StandardMaterial3D();
        eyeMaterial.AlbedoColor = new Color(0.9f, 0.7f, 0.2f);
        eyeMaterial.EmissionEnabled = true;
        eyeMaterial.Emission = new Color(0.9f, 0.6f, 0.1f) * 0.3f;

        // Body (horizontal ellipsoid)
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.3f, Height = 0.6f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = _material;
        body.Position = new Vector3(0, 0.45f, 0);
        body.Scale = new Vector3(0.8f, 0.7f, 1.4f);
        _meshInstance!.AddChild(body);

        // Head
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.15f, Height = 0.3f };
        head.Mesh = headMesh;
        head.MaterialOverride = _material;
        head.Position = new Vector3(0, 0.55f, 0.45f);
        head.Scale = new Vector3(0.9f, 0.85f, 1.1f);
        _meshInstance.AddChild(head);

        // Snout
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.08f, Height = 0.2f };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = _material;
        snout.Position = new Vector3(0, 0.5f, 0.6f);
        snout.RotationDegrees = new Vector3(-90, 0, 0);
        _meshInstance.AddChild(snout);

        // Nose
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.03f, Height = 0.06f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = darkMaterial;
        nose.Position = new Vector3(0, 0.52f, 0.72f);
        _meshInstance.AddChild(nose);

        // Eyes
        var leftEye = new MeshInstance3D();
        var eyeMesh = new SphereMesh { Radius = 0.035f, Height = 0.07f };
        leftEye.Mesh = eyeMesh;
        leftEye.MaterialOverride = eyeMaterial;
        leftEye.Position = new Vector3(-0.08f, 0.6f, 0.52f);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D();
        rightEye.Mesh = eyeMesh;
        rightEye.MaterialOverride = eyeMaterial;
        rightEye.Position = new Vector3(0.08f, 0.6f, 0.52f);
        _meshInstance.AddChild(rightEye);

        // Ears (triangular)
        var leftEar = new MeshInstance3D();
        var earMesh = new PrismMesh { Size = new Vector3(0.08f, 0.12f, 0.04f) };
        leftEar.Mesh = earMesh;
        leftEar.MaterialOverride = _material;
        leftEar.Position = new Vector3(-0.1f, 0.72f, 0.4f);
        leftEar.RotationDegrees = new Vector3(20, 0, -15);
        _meshInstance.AddChild(leftEar);

        var rightEar = new MeshInstance3D();
        rightEar.Mesh = earMesh;
        rightEar.MaterialOverride = _material;
        rightEar.Position = new Vector3(0.1f, 0.72f, 0.4f);
        rightEar.RotationDegrees = new Vector3(20, 0, 15);
        _meshInstance.AddChild(rightEar);

        // Tail
        var tail = new MeshInstance3D();
        var tailMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.06f, Height = 0.35f };
        tail.Mesh = tailMesh;
        tail.MaterialOverride = _material;
        tail.Position = new Vector3(0, 0.5f, -0.4f);
        tail.RotationDegrees = new Vector3(-45, 0, 0);
        _meshInstance.AddChild(tail);

        // Legs
        CreateWolfLeg(true, true, _material);  // Front left
        CreateWolfLeg(false, true, _material); // Front right
        CreateWolfLeg(true, false, _material); // Back left
        CreateWolfLeg(false, false, _material); // Back right
    }

    private void CreateWolfLeg(bool isLeft, bool isFront, StandardMaterial3D material)
    {
        float side = isLeft ? -0.15f : 0.15f;
        float front = isFront ? 0.25f : -0.25f;

        var leg = new Node3D();
        leg.Position = new Vector3(side, 0.35f, front);

        var upperLeg = new MeshInstance3D();
        var legMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.03f, Height = 0.2f };
        upperLeg.Mesh = legMesh;
        upperLeg.MaterialOverride = material;
        upperLeg.Position = new Vector3(0, -0.1f, 0);
        leg.AddChild(upperLeg);

        var lowerLeg = new MeshInstance3D();
        lowerLeg.Mesh = legMesh;
        lowerLeg.MaterialOverride = material;
        lowerLeg.Position = new Vector3(0, -0.3f, 0);
        leg.AddChild(lowerLeg);

        var paw = new MeshInstance3D();
        var pawMesh = new SphereMesh { Radius = 0.04f, Height = 0.06f };
        paw.Mesh = pawMesh;
        paw.MaterialOverride = material;
        paw.Position = new Vector3(0, -0.38f, 0.02f);
        paw.Scale = new Vector3(1f, 0.5f, 1.2f);
        leg.AddChild(paw);

        _meshInstance!.AddChild(leg);

        if (isFront && isLeft) _leftArmNode = leg;
        else if (isFront && !isLeft) _rightArmNode = leg;
        else if (!isFront && isLeft) _leftLegNode = leg;
        else _rightLegNode = leg;
    }

    private void CreateBatMesh()
    {
        Color bodyColor = new Color(0.25f, 0.2f, 0.25f);
        Color wingColor = new Color(0.2f, 0.15f, 0.2f);

        _material = new StandardMaterial3D();
        _material.AlbedoColor = bodyColor;
        _material.Roughness = 0.8f;

        var wingMaterial = new StandardMaterial3D();
        wingMaterial.AlbedoColor = wingColor;
        wingMaterial.Roughness = 0.7f;
        wingMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        var eyeMaterial = new StandardMaterial3D();
        eyeMaterial.AlbedoColor = new Color(1f, 0.3f, 0.2f);
        eyeMaterial.EmissionEnabled = true;
        eyeMaterial.Emission = new Color(1f, 0.2f, 0.1f) * 0.5f;

        // Body (small fuzzy ball)
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.15f, Height = 0.3f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = _material;
        body.Position = new Vector3(0, 0.8f, 0);
        body.Scale = new Vector3(1f, 1.2f, 0.9f);
        _meshInstance!.AddChild(body);

        // Head
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.1f, Height = 0.2f };
        head.Mesh = headMesh;
        head.MaterialOverride = _material;
        head.Position = new Vector3(0, 0.95f, 0.08f);
        _meshInstance.AddChild(head);

        // Big ears
        var leftEar = new MeshInstance3D();
        var earMesh = new PrismMesh { Size = new Vector3(0.1f, 0.15f, 0.03f) };
        leftEar.Mesh = earMesh;
        leftEar.MaterialOverride = _material;
        leftEar.Position = new Vector3(-0.08f, 1.08f, 0.05f);
        leftEar.RotationDegrees = new Vector3(0, 0, -20);
        _meshInstance.AddChild(leftEar);

        var rightEar = new MeshInstance3D();
        rightEar.Mesh = earMesh;
        rightEar.MaterialOverride = _material;
        rightEar.Position = new Vector3(0.08f, 1.08f, 0.05f);
        rightEar.RotationDegrees = new Vector3(0, 0, 20);
        _meshInstance.AddChild(rightEar);

        // Glowing red eyes
        var leftEye = new MeshInstance3D();
        var eyeMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f };
        leftEye.Mesh = eyeMesh;
        leftEye.MaterialOverride = eyeMaterial;
        leftEye.Position = new Vector3(-0.04f, 0.97f, 0.14f);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D();
        rightEye.Mesh = eyeMesh;
        rightEye.MaterialOverride = eyeMaterial;
        rightEye.Position = new Vector3(0.04f, 0.97f, 0.14f);
        _meshInstance.AddChild(rightEye);

        // Wings (stretched flat boxes)
        var leftWing = new Node3D();
        leftWing.Position = new Vector3(-0.15f, 0.8f, 0);

        var wingSegment1 = new MeshInstance3D();
        var wing1Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.02f, 0.2f) };
        wingSegment1.Mesh = wing1Mesh;
        wingSegment1.MaterialOverride = wingMaterial;
        wingSegment1.Position = new Vector3(-0.15f, 0, 0);
        wingSegment1.RotationDegrees = new Vector3(0, 0, 15);
        leftWing.AddChild(wingSegment1);

        _meshInstance.AddChild(leftWing);
        _leftArmNode = leftWing;

        var rightWing = new Node3D();
        rightWing.Position = new Vector3(0.15f, 0.8f, 0);

        var wingSegment2 = new MeshInstance3D();
        wingSegment2.Mesh = wing1Mesh;
        wingSegment2.MaterialOverride = wingMaterial;
        wingSegment2.Position = new Vector3(0.15f, 0, 0);
        wingSegment2.RotationDegrees = new Vector3(0, 0, -15);
        rightWing.AddChild(wingSegment2);

        _meshInstance.AddChild(rightWing);
        _rightArmNode = rightWing;

        // Tiny legs
        var leftLeg = new MeshInstance3D();
        var legMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.01f, Height = 0.1f };
        leftLeg.Mesh = legMesh;
        leftLeg.MaterialOverride = _material;
        leftLeg.Position = new Vector3(-0.05f, 0.62f, 0);
        _meshInstance.AddChild(leftLeg);

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = legMesh;
        rightLeg.MaterialOverride = _material;
        rightLeg.Position = new Vector3(0.05f, 0.62f, 0);
        _meshInstance.AddChild(rightLeg);
    }

    private void CreateDragonMesh()
    {
        Color scaleColor = new Color(0.7f, 0.2f, 0.15f);
        Color bellyColor = new Color(0.9f, 0.7f, 0.4f);
        Color wingColor = scaleColor.Darkened(0.2f);

        _material = new StandardMaterial3D();
        _material.AlbedoColor = scaleColor;
        _material.Roughness = 0.6f;
        _material.Metallic = 0.2f;

        var bellyMaterial = new StandardMaterial3D();
        bellyMaterial.AlbedoColor = bellyColor;
        bellyMaterial.Roughness = 0.7f;

        var wingMaterial = new StandardMaterial3D();
        wingMaterial.AlbedoColor = wingColor;
        wingMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        var eyeMaterial = new StandardMaterial3D();
        eyeMaterial.AlbedoColor = new Color(1f, 0.8f, 0.2f);
        eyeMaterial.EmissionEnabled = true;
        eyeMaterial.Emission = new Color(1f, 0.6f, 0.1f) * 0.6f;

        float scale = 1.5f; // Dragons are bigger

        // Body (large horizontal ellipsoid)
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.4f * scale, Height = 0.8f * scale };
        body.Mesh = bodyMesh;
        body.MaterialOverride = _material;
        body.Position = new Vector3(0, 0.6f * scale, 0);
        body.Scale = new Vector3(0.9f, 0.8f, 1.3f);
        _meshInstance!.AddChild(body);

        // Belly plate
        var belly = new MeshInstance3D();
        var bellyMesh = new SphereMesh { Radius = 0.3f * scale, Height = 0.6f * scale };
        belly.Mesh = bellyMesh;
        belly.MaterialOverride = bellyMaterial;
        belly.Position = new Vector3(0, 0.5f * scale, 0.1f * scale);
        belly.Scale = new Vector3(0.7f, 0.6f, 0.5f);
        _meshInstance.AddChild(belly);

        // Neck
        var neck = new MeshInstance3D();
        var neckMesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.18f * scale, Height = 0.4f * scale };
        neck.Mesh = neckMesh;
        neck.MaterialOverride = _material;
        neck.Position = new Vector3(0, 0.9f * scale, 0.35f * scale);
        neck.RotationDegrees = new Vector3(-30, 0, 0);
        _meshInstance.AddChild(neck);

        // Head
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.4f * scale };
        head.Mesh = headMesh;
        head.MaterialOverride = _material;
        head.Position = new Vector3(0, 1.15f * scale, 0.55f * scale);
        head.Scale = new Vector3(0.8f, 0.7f, 1.1f);
        _meshInstance.AddChild(head);

        // Snout
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.12f * scale, Height = 0.25f * scale };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = _material;
        snout.Position = new Vector3(0, 1.1f * scale, 0.75f * scale);
        snout.RotationDegrees = new Vector3(-90, 0, 0);
        _meshInstance.AddChild(snout);

        // Eyes
        var leftEye = new MeshInstance3D();
        var eyeMesh = new SphereMesh { Radius = 0.05f * scale, Height = 0.1f * scale };
        leftEye.Mesh = eyeMesh;
        leftEye.MaterialOverride = eyeMaterial;
        leftEye.Position = new Vector3(-0.1f * scale, 1.2f * scale, 0.6f * scale);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D();
        rightEye.Mesh = eyeMesh;
        rightEye.MaterialOverride = eyeMaterial;
        rightEye.Position = new Vector3(0.1f * scale, 1.2f * scale, 0.6f * scale);
        _meshInstance.AddChild(rightEye);

        // Horns
        var leftHorn = new MeshInstance3D();
        var hornMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.04f * scale, Height = 0.2f * scale };
        leftHorn.Mesh = hornMesh;
        leftHorn.MaterialOverride = bellyMaterial;
        leftHorn.Position = new Vector3(-0.12f * scale, 1.3f * scale, 0.45f * scale);
        leftHorn.RotationDegrees = new Vector3(-30, 0, -20);
        _meshInstance.AddChild(leftHorn);

        var rightHorn = new MeshInstance3D();
        rightHorn.Mesh = hornMesh;
        rightHorn.MaterialOverride = bellyMaterial;
        rightHorn.Position = new Vector3(0.12f * scale, 1.3f * scale, 0.45f * scale);
        rightHorn.RotationDegrees = new Vector3(-30, 0, 20);
        _meshInstance.AddChild(rightHorn);

        // Wings
        var leftWing = new Node3D();
        leftWing.Position = new Vector3(-0.3f * scale, 0.8f * scale, -0.1f * scale);

        var wingBone = new MeshInstance3D();
        var wingBoneMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.03f * scale, Height = 0.6f * scale };
        wingBone.Mesh = wingBoneMesh;
        wingBone.MaterialOverride = _material;
        wingBone.Position = new Vector3(-0.3f * scale, 0, 0);
        wingBone.RotationDegrees = new Vector3(0, 0, 60);
        leftWing.AddChild(wingBone);

        var wingMembrane = new MeshInstance3D();
        var membraneMesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.02f, 0.4f * scale) };
        wingMembrane.Mesh = membraneMesh;
        wingMembrane.MaterialOverride = wingMaterial;
        wingMembrane.Position = new Vector3(-0.25f * scale, 0, 0);
        wingMembrane.RotationDegrees = new Vector3(0, 0, 30);
        leftWing.AddChild(wingMembrane);

        _meshInstance.AddChild(leftWing);
        _leftArmNode = leftWing;

        var rightWing = new Node3D();
        rightWing.Position = new Vector3(0.3f * scale, 0.8f * scale, -0.1f * scale);

        var wingBone2 = new MeshInstance3D();
        wingBone2.Mesh = wingBoneMesh;
        wingBone2.MaterialOverride = _material;
        wingBone2.Position = new Vector3(0.3f * scale, 0, 0);
        wingBone2.RotationDegrees = new Vector3(0, 0, -60);
        rightWing.AddChild(wingBone2);

        var wingMembrane2 = new MeshInstance3D();
        wingMembrane2.Mesh = membraneMesh;
        wingMembrane2.MaterialOverride = wingMaterial;
        wingMembrane2.Position = new Vector3(0.25f * scale, 0, 0);
        wingMembrane2.RotationDegrees = new Vector3(0, 0, -30);
        rightWing.AddChild(wingMembrane2);

        _meshInstance.AddChild(rightWing);
        _rightArmNode = rightWing;

        // Tail
        var tail = new MeshInstance3D();
        var tailMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.15f * scale, Height = 0.7f * scale };
        tail.Mesh = tailMesh;
        tail.MaterialOverride = _material;
        tail.Position = new Vector3(0, 0.4f * scale, -0.5f * scale);
        tail.RotationDegrees = new Vector3(-60, 0, 0);
        _meshInstance.AddChild(tail);

        // Legs
        CreateDragonLeg(true, scale, _material);
        CreateDragonLeg(false, scale, _material);
    }

    private void CreateDragonLeg(bool isLeft, float scale, StandardMaterial3D material)
    {
        float side = isLeft ? -1 : 1;
        var leg = new Node3D();
        leg.Position = new Vector3(side * 0.25f * scale, 0.4f * scale, 0);

        var upperLeg = new MeshInstance3D();
        var legMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.06f * scale, Height = 0.3f * scale };
        upperLeg.Mesh = legMesh;
        upperLeg.MaterialOverride = material;
        upperLeg.Position = new Vector3(0, -0.15f * scale, 0);
        leg.AddChild(upperLeg);

        var lowerLeg = new MeshInstance3D();
        lowerLeg.Mesh = legMesh;
        lowerLeg.MaterialOverride = material;
        lowerLeg.Position = new Vector3(0, -0.4f * scale, 0);
        leg.AddChild(lowerLeg);

        var foot = new MeshInstance3D();
        var footMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.1f * scale };
        foot.Mesh = footMesh;
        foot.MaterialOverride = material;
        foot.Position = new Vector3(0, -0.55f * scale, 0.05f * scale);
        foot.Scale = new Vector3(1f, 0.5f, 1.3f);
        leg.AddChild(foot);

        _meshInstance!.AddChild(leg);
        if (isLeft) _leftLegNode = leg; else _rightLegNode = leg;
    }

    /// <summary>
    /// Apply Enrage buff from Goblin Shaman.
    /// Increases damage and move speed for the duration.
    /// </summary>
    public void ApplyEnrage(float duration)
    {
        _isEnraged = true;
        _enrageTimer = duration;

        // Create visual indicator if not exists
        if (_enrageIndicator == null)
        {
            CreateEnrageIndicator();
        }

        if (_enrageIndicator != null)
            _enrageIndicator.Visible = true;

        if (_enrageParticles != null)
            _enrageParticles.Emitting = true;

        GD.Print($"[BasicEnemy3D] {MonsterType} is now ENRAGED for {duration}s!");
    }

    private void CreateEnrageIndicator()
    {
        // Enrage icon floating above head
        _enrageIndicator = new Node3D();
        _enrageIndicator.Name = "EnrageIndicator";
        _enrageIndicator.Position = new Vector3(0, 2.5f, 0);

        // Red glowing icon mesh
        var iconMesh = new MeshInstance3D();
        var quad = new QuadMesh();
        quad.Size = new Vector2(0.4f, 0.4f);
        iconMesh.Mesh = quad;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.3f, 0.2f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.2f, 0.1f);
        mat.EmissionEnergyMultiplier = 2f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        iconMesh.MaterialOverride = mat;
        _enrageIndicator.AddChild(iconMesh);

        // Add pulsing light
        var light = new OmniLight3D();
        light.LightColor = new Color(1f, 0.3f, 0.1f);
        light.LightEnergy = 0.8f;
        light.OmniRange = 2f;
        _enrageIndicator.AddChild(light);

        // Create rage particles
        _enrageParticles = new GpuParticles3D();
        _enrageParticles.Amount = 15;
        _enrageParticles.Lifetime = 0.8f;
        _enrageParticles.Emitting = false;
        _enrageParticles.Position = new Vector3(0, 1f, 0);

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.5f;
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 45f;
        processMat.InitialVelocityMin = 1f;
        processMat.InitialVelocityMax = 2f;
        processMat.Gravity = new Vector3(0, -1, 0);
        processMat.ScaleMin = 0.1f;
        processMat.ScaleMax = 0.2f;
        _enrageParticles.ProcessMaterial = processMat;

        var particleMesh = new SphereMesh();
        particleMesh.Radius = 0.05f;
        particleMesh.Height = 0.1f;
        var particleMat = new StandardMaterial3D();
        particleMat.AlbedoColor = new Color(1f, 0.4f, 0.2f);
        particleMat.EmissionEnabled = true;
        particleMat.Emission = new Color(1f, 0.3f, 0.1f);
        particleMat.EmissionEnergyMultiplier = 2f;
        particleMesh.Material = particleMat;
        _enrageParticles.DrawPass1 = particleMesh;

        AddChild(_enrageIndicator);
        AddChild(_enrageParticles);
        _enrageIndicator.Visible = false;
    }

    private void ProcessEnrage(float dt)
    {
        if (!_isEnraged) return;

        _enrageTimer -= dt;

        // Pulse the indicator
        if (_enrageIndicator != null)
        {
            float pulse = (Mathf.Sin(_enrageTimer * 8f) + 1f) * 0.5f;
            _enrageIndicator.Scale = Vector3.One * (0.8f + pulse * 0.4f);
        }

        // End enrage
        if (_enrageTimer <= 0)
        {
            _isEnraged = false;
            if (_enrageIndicator != null)
                _enrageIndicator.Visible = false;
            if (_enrageParticles != null)
                _enrageParticles.Emitting = false;
            GD.Print($"[BasicEnemy3D] {MonsterType} enrage ended");
        }
    }

    /// <summary>
    /// Get effective damage, accounting for enrage buff.
    /// </summary>
    protected float GetEffectiveDamage()
    {
        return _isEnraged ? Damage * 1.5f : Damage;
    }

    /// <summary>
    /// Get effective move speed, accounting for enrage buff.
    /// </summary>
    protected float GetEffectiveMoveSpeed()
    {
        return _isEnraged ? MoveSpeed * 1.3f : MoveSpeed;
    }

    /// <summary>
    /// Static factory for creating enemies
    /// </summary>
    public static BasicEnemy3D Create(string monsterType, Vector3 position, int level = 1)
    {
        var enemy = new BasicEnemy3D();
        enemy.MonsterType = monsterType;
        enemy.MonsterId = $"{monsterType.ToLower()}_basic";
        enemy.Position = position;
        enemy.Level = level;
        return enemy;
    }

    private void CreateBadlamaMesh()
    {
        // Use the shared MonsterMeshFactory for consistent Badlama appearance
        MonsterMeshFactory.CreateMonsterMesh(_meshInstance!, "badlama", _baseColor);
    }
}
