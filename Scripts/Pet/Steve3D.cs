using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.Enemies;
using SafeRoom3D.Combat;
using SafeRoom3D.UI;

namespace SafeRoom3D.Pet;

/// <summary>
/// Steve the Chihuahua - Player's loyal companion.
/// Follows the player, hops around near their feet, and has two abilities:
/// - Heal: Heals the player when they take damage (10s cooldown)
/// - Magic Missile: Attacks enemies that are attacking the player (10s cooldown)
/// </summary>
public partial class Steve3D : Node3D
{
    public static Steve3D? Instance { get; private set; }

    // GLB Model support
    public const string GlbModelPath = "res://Assets/Models/steve.glb";
    public static bool UseGlbModel { get; set; } = false;
    private Node3D? _glbModelInstance;
    private AnimationPlayer? _glbAnimPlayer;

    // Stats
    public const float HealAmount = 50f;
    public const float MagicMissileDamage = 25f;
    public const float AbilityCooldown = 10f;
    public const float FollowDistance = 1.5f;
    public const float HopHeight = 0.3f;
    public const float HopSpeed = 3f;
    public const float MoveSpeed = 6f;

    // State
    private float _healCooldown;
    private float _missileCooldown;
    private float _hopTimer;
    private float _idleHopTimer;
    private bool _isHopping;
    private Vector3 _targetPosition;
    private Node3D? _player;

    // Roaming state
    private float _roamTimer;
    private float _roamInterval = 3f;     // Time between random roam decisions
    private Vector3 _roamOffset;          // Current roaming offset from player
    private const float MaxRoamDistance = 3f;  // Max distance from player when roaming

    // Visual
    private MeshInstance3D? _bodyMesh;
    private MeshInstance3D? _headMesh;
    private Node3D? _earLeft;
    private Node3D? _earRight;
    private Node3D? _tail;
    private OmniLight3D? _glowLight;
    private float _tailWagTimer;
    private float _earWiggleTimer;

    // Colors - Dark gray chihuahua
    private static readonly Color BodyColor = new(0.30f, 0.28f, 0.26f);   // Dark gray chihuahua
    private static readonly Color BellyColor = new(0.40f, 0.38f, 0.36f);   // Slightly lighter gray belly
    private static readonly Color EyeColor = new(0.15f, 0.1f, 0.08f);    // Dark eyes
    private static readonly Color NoseColor = new(0.1f, 0.08f, 0.08f);   // Black nose
    private static readonly Color TongueColor = new(1.0f, 0.5f, 0.6f);   // Pink tongue
    private static readonly Color HealGlowColor = new(0.3f, 1f, 0.5f);   // Green heal
    private static readonly Color MissileGlowColor = new(0.4f, 0.6f, 1f); // Blue missile

    // Signals
    [Signal] public delegate void HealedPlayerEventHandler(float amount);
    [Signal] public delegate void FiredMissileEventHandler(Node3D target);

    // === TRANSFORMATION STATE ===
    public string CurrentForm { get; private set; } = "steve";
    public bool IsTransformed => CurrentForm != "steve";

    // Attack stats (modified when transformed)
    public float AttackDamage { get; private set; } = MagicMissileDamage;
    public float AttackRange { get; private set; } = 10f;
    public float AttackCooldown { get; private set; } = AbilityCooldown;
    public bool IsRangedAttacker { get; private set; } = true;
    private float _transformAttackTimer;

    // LimbNodes for transformed monster mesh (for animation)
    private MonsterMeshFactory.LimbNodes? _transformLimbs;

    public override void _Ready()
    {
        Instance = this;

        CreateMesh();
        CreateGlowLight();

        // Find player
        CallDeferred(nameof(FindPlayer));

        GD.Print("[Steve3D] Steve the Chihuahua is ready to help!");
    }

    private void FindPlayer()
    {
        _player = FPSController.Instance;
        if (_player != null)
        {
            // Position near player
            GlobalPosition = _player.GlobalPosition + new Vector3(1f, 0, 1f);
            _targetPosition = GlobalPosition;

            // Listen for player damage
            if (_player is FPSController fps)
            {
                fps.HealthChanged += OnPlayerHealthChanged;
            }
        }
    }

    /// <summary>
    /// Reload the model (switch between procedural and GLB)
    /// </summary>
    public void ReloadModel()
    {
        // Clear existing model
        ClearModel();

        // Recreate based on current form
        if (IsTransformed)
        {
            CreateTransformedMesh(CurrentForm);
        }
        else
        {
            CreateMesh();
        }
        CreateGlowLight();

        GD.Print($"[Steve3D] Model reloaded, UseGlb: {UseGlbModel}, Form: {CurrentForm}");
    }

    /// <summary>
    /// Transform Steve into a monster form
    /// </summary>
    public void TransformInto(string monsterType)
    {
        CurrentForm = monsterType.ToLower();
        SetAttackStats(CurrentForm);

        // Clear current mesh and create monster mesh
        ClearModel();
        CreateTransformedMesh(CurrentForm);
        CreateGlowLight();

        // Flash the glow light to indicate transformation
        if (_glowLight != null)
        {
            _glowLight.LightColor = new Color(1f, 0.8f, 0.2f); // Golden flash
            _glowLight.LightEnergy = 5f;
        }

        GD.Print($"[Steve3D] Transformed into {CurrentForm}!");
    }

    /// <summary>
    /// Revert Steve back to normal chihuahua form
    /// </summary>
    public void RevertToNormal()
    {
        CurrentForm = "steve";

        // Reset to normal attack stats
        AttackDamage = MagicMissileDamage;
        AttackRange = 10f;
        AttackCooldown = AbilityCooldown;
        IsRangedAttacker = true;
        _transformLimbs = null;

        // Recreate normal Steve mesh
        ClearModel();
        CreateMesh();
        CreateGlowLight();

        // Flash the glow light
        if (_glowLight != null)
        {
            _glowLight.LightColor = HealGlowColor;
            _glowLight.LightEnergy = 3f;
        }

        GD.Print("[Steve3D] Reverted to normal form!");
    }

    /// <summary>
    /// Set attack stats based on monster type
    /// </summary>
    private void SetAttackStats(string monsterType)
    {
        // Configure attack behavior based on monster type
        (AttackDamage, AttackRange, AttackCooldown, IsRangedAttacker) = monsterType switch
        {
            // Ranged attackers
            "goblin_shaman" => (15f, 12f, 2.5f, true),
            "goblin_thrower" => (12f, 10f, 1.8f, true),
            "eye" => (10f, 15f, 2f, true),

            // Strong melee
            "dragon" or "dragon_king" => (35f, 3f, 2f, false),
            "the_butcher" => (40f, 3f, 2.5f, false),
            "badlama" => (30f, 3f, 1.8f, false),
            "mongo" or "mongo_the_destroyer" => (45f, 4f, 3f, false),

            // Fast melee
            "spider" or "spider_queen" => (12f, 2f, 0.8f, false),
            "wolf" => (14f, 2.5f, 0.9f, false),
            "bat" => (8f, 1.5f, 0.6f, false),
            "dungeon_rat" => (6f, 1.5f, 0.5f, false),

            // Standard melee
            "goblin" => (10f, 2f, 1f, false),
            "goblin_warlord" => (18f, 2.5f, 1.5f, false),
            "skeleton" or "skeleton_lord" => (12f, 2f, 1.2f, false),
            "slime" or "slime_king" => (8f, 1.5f, 1f, false),
            "mushroom" or "sporeling_elder" => (10f, 2f, 1.3f, false),
            "lizard" => (14f, 2f, 1.1f, false),
            "mimic" => (20f, 2f, 1.5f, false),
            "rabbidd" => (8f, 2f, 0.7f, false),

            // Default melee for unknown types
            _ => (15f, 2f, 1.2f, false)
        };

        GD.Print($"[Steve3D] Attack stats set for {monsterType}: Dmg={AttackDamage}, Range={AttackRange}, CD={AttackCooldown}, Ranged={IsRangedAttacker}");
    }

    /// <summary>
    /// Create a monster mesh for transformed Steve
    /// </summary>
    private void CreateTransformedMesh(string monsterType)
    {
        // Use MonsterMeshFactory to create monster appearance
        _transformLimbs = MonsterMeshFactory.CreateMonsterMesh(this, monsterType);

        // Scale down to pet size (40% of normal monster size)
        Scale = new Vector3(0.4f, 0.4f, 0.4f);

        GD.Print($"[Steve3D] Created transformed mesh for {monsterType}");
    }

    private void ClearModel()
    {
        // Reset scale to normal
        Scale = Vector3.One;

        // Clear transform limbs reference
        _transformLimbs = null;

        // Remove GLB instance if exists
        if (_glbModelInstance != null)
        {
            _glbModelInstance.QueueFree();
            _glbModelInstance = null;
        }
        _glbAnimPlayer = null;

        // Remove procedural meshes
        _bodyMesh?.QueueFree();
        _bodyMesh = null;
        _headMesh?.QueueFree();
        _headMesh = null;
        _earLeft?.QueueFree();
        _earLeft = null;
        _earRight?.QueueFree();
        _earRight = null;
        _tail?.QueueFree();
        _tail = null;
        _glowLight?.QueueFree();
        _glowLight = null;

        // Remove all other mesh children (snout, nose, eyes, tongue, legs, monster parts)
        foreach (var child in GetChildren())
        {
            if (child is MeshInstance3D || child is Node3D)
            {
                child.QueueFree();
            }
        }
    }

    private void CreateMesh()
    {
        // Check if we should use GLB model
        if (UseGlbModel && ResourceLoader.Exists(GlbModelPath))
        {
            var scene = GD.Load<PackedScene>(GlbModelPath);
            if (scene != null)
            {
                _glbModelInstance = scene.Instantiate<Node3D>();
                // Scale the GLB model appropriately (adjust as needed for your model)
                _glbModelInstance.Scale = new Vector3(0.15f, 0.15f, 0.15f);
                AddChild(_glbModelInstance);
                _glbAnimPlayer = GlbModelConfig.FindAnimationPlayer(_glbModelInstance);
                GD.Print($"[Steve3D] Using GLB model: {GlbModelPath}, AnimPlayer: {_glbAnimPlayer != null}");
                return;
            }
        }

        // Procedural mesh fallback
        // Main body (small, round chihuahua body)
        _bodyMesh = new MeshInstance3D();
        var bodyShape = new SphereMesh { Radius = 0.12f, Height = 0.24f };
        _bodyMesh.Mesh = bodyShape;
        _bodyMesh.Scale = new Vector3(1f, 0.8f, 1.2f); // Slightly elongated

        var bodyMat = new StandardMaterial3D
        {
            AlbedoColor = BodyColor,
            Roughness = 0.9f
        };
        _bodyMesh.MaterialOverride = bodyMat;
        _bodyMesh.Position = new Vector3(0, 0.15f, 0);
        AddChild(_bodyMesh);

        // Head (big chihuahua head - they're known for big heads!)
        // IMPORTANT: Set Height = 2*Radius for proper sphere proportions
        _headMesh = new MeshInstance3D();
        var headShape = new SphereMesh { Radius = 0.1f, Height = 0.2f };
        _headMesh.Mesh = headShape;
        _headMesh.Scale = new Vector3(1.0f, 0.9f, 0.95f); // Slightly wider than tall

        var headMat = new StandardMaterial3D
        {
            AlbedoColor = BodyColor,
            Roughness = 0.9f
        };
        _headMesh.MaterialOverride = headMat;
        _headMesh.Position = new Vector3(0, 0.22f, 0.12f);
        AddChild(_headMesh);

        // Snout
        var snout = new MeshInstance3D();
        var snoutShape = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        snout.Mesh = snoutShape;
        snout.Scale = new Vector3(0.8f, 0.6f, 1.2f);
        snout.MaterialOverride = headMat;
        snout.Position = new Vector3(0, 0.18f, 0.2f);
        AddChild(snout);

        // Nose
        var nose = new MeshInstance3D();
        var noseShape = new SphereMesh { Radius = 0.015f, Height = 0.03f };
        nose.Mesh = noseShape;
        var noseMat = new StandardMaterial3D { AlbedoColor = NoseColor, Roughness = 0.3f };
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, 0.18f, 0.24f);
        AddChild(nose);

        // Eyes (big chihuahua eyes! - pushed forward so they're visible, use Height = 2*Radius)
        var eyeShape = new SphereMesh { Radius = 0.022f, Height = 0.044f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = EyeColor,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.2f, 0.15f) * 0.3f
        };

        var leftEye = new MeshInstance3D { Mesh = eyeShape, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.04f, 0.24f, 0.20f);  // More forward (Z=0.20)
        AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeShape, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.04f, 0.24f, 0.20f);  // More forward (Z=0.20)
        AddChild(rightEye);

        // Eye highlights (white reflection spots)
        var highlightShape = new SphereMesh { Radius = 0.008f, Height = 0.016f };
        var highlightMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            EmissionEnabled = true,
            Emission = Colors.White * 0.5f
        };

        var leftHighlight = new MeshInstance3D { Mesh = highlightShape, MaterialOverride = highlightMat };
        leftHighlight.Position = new Vector3(-0.035f, 0.248f, 0.215f);
        AddChild(leftHighlight);

        var rightHighlight = new MeshInstance3D { Mesh = highlightShape, MaterialOverride = highlightMat };
        rightHighlight.Position = new Vector3(0.045f, 0.248f, 0.215f);
        AddChild(rightHighlight);

        // Pink tongue hanging out (cute chihuahua trait!)
        var tongue = new MeshInstance3D();
        var tongueShape = new SphereMesh { Radius = 0.015f, Height = 0.03f };
        tongue.Mesh = tongueShape;
        tongue.Scale = new Vector3(1.0f, 0.4f, 1.5f);  // Flat and elongated
        var tongueMat = new StandardMaterial3D
        {
            AlbedoColor = TongueColor,
            Roughness = 0.4f
        };
        tongue.MaterialOverride = tongueMat;
        tongue.Position = new Vector3(0, 0.15f, 0.245f);  // Below and in front of nose
        AddChild(tongue);

        // Big ears (signature chihuahua feature!)
        var earShape = new PrismMesh { Size = new Vector3(0.06f, 0.12f, 0.02f) };
        var earMat = new StandardMaterial3D { AlbedoColor = BodyColor.Darkened(0.1f), Roughness = 0.9f };

        _earLeft = new Node3D();
        _earLeft.Position = new Vector3(-0.08f, 0.3f, 0.08f);
        _earLeft.RotationDegrees = new Vector3(15, -20, -30);
        AddChild(_earLeft);
        var leftEarMesh = new MeshInstance3D { Mesh = earShape, MaterialOverride = earMat };
        _earLeft.AddChild(leftEarMesh);

        _earRight = new Node3D();
        _earRight.Position = new Vector3(0.08f, 0.3f, 0.08f);
        _earRight.RotationDegrees = new Vector3(15, 20, 30);
        AddChild(_earRight);
        var rightEarMesh = new MeshInstance3D { Mesh = earShape, MaterialOverride = earMat };
        _earRight.AddChild(rightEarMesh);

        // Tail (curly chihuahua tail)
        _tail = new Node3D();
        _tail.Position = new Vector3(0, 0.18f, -0.12f);
        AddChild(_tail);

        var tailShape = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.02f, Height = 0.1f };
        var tailMesh = new MeshInstance3D { Mesh = tailShape, MaterialOverride = bodyMat };
        tailMesh.RotationDegrees = new Vector3(-45, 0, 0);
        _tail.AddChild(tailMesh);

        // Legs (tiny chihuahua legs)
        var legShape = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.012f, Height = 0.1f };
        var legMat = new StandardMaterial3D { AlbedoColor = BodyColor, Roughness = 0.9f };

        float[] legX = { -0.05f, 0.05f, -0.05f, 0.05f };
        float[] legZ = { 0.06f, 0.06f, -0.06f, -0.06f };
        for (int i = 0; i < 4; i++)
        {
            var leg = new MeshInstance3D { Mesh = legShape, MaterialOverride = legMat };
            leg.Position = new Vector3(legX[i], 0.05f, legZ[i]);
            AddChild(leg);
        }
    }

    private void CreateGlowLight()
    {
        _glowLight = new OmniLight3D();
        _glowLight.LightColor = HealGlowColor;
        _glowLight.LightEnergy = 0;
        _glowLight.OmniRange = 2f;
        _glowLight.Position = new Vector3(0, 0.2f, 0);
        AddChild(_glowLight);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Update cooldowns
        if (_healCooldown > 0) _healCooldown -= dt;
        if (_missileCooldown > 0) _missileCooldown -= dt;
        if (_transformAttackTimer > 0) _transformAttackTimer -= dt;

        // Follow player
        UpdateFollowing(dt);

        // Animate
        UpdateAnimation(dt);

        // Check for enemies - behavior differs when transformed
        if (IsTransformed)
        {
            CheckForThreatsTransformed(dt);
        }
        else
        {
            CheckForThreats(dt);
        }

        // Fade glow light
        if (_glowLight != null && _glowLight.LightEnergy > 0)
        {
            _glowLight.LightEnergy = Mathf.MoveToward(_glowLight.LightEnergy, 0, dt * 3f);
        }
    }

    private void UpdateFollowing(float dt)
    {
        if (_player == null) return;

        // Calculate target position near player's feet
        Vector3 playerPos = _player.GlobalPosition;
        Vector3 toPlayer = playerPos - GlobalPosition;
        toPlayer.Y = 0;
        float distance = toPlayer.Length();

        // Update roaming timer
        _roamTimer += dt;
        if (_roamTimer >= _roamInterval)
        {
            _roamTimer = 0;
            _roamInterval = 2f + (float)GD.RandRange(1.0, 4.0);  // Random interval between roams

            // Pick a new random offset to roam to (circle around player)
            float angle = (float)GD.RandRange(0, Mathf.Tau);
            float roamDist = (float)GD.RandRange(0.5, MaxRoamDistance);
            _roamOffset = new Vector3(
                Mathf.Cos(angle) * roamDist,
                0,
                Mathf.Sin(angle) * roamDist
            );
        }

        // Calculate ideal roaming position
        Vector3 roamTarget = playerPos + _roamOffset;
        roamTarget.Y = playerPos.Y;

        // If too far from player, prioritize returning; otherwise roam
        if (distance > MaxRoamDistance + 1f)
        {
            // Too far from player - rush back
            _targetPosition = playerPos - toPlayer.Normalized() * FollowDistance;
            _targetPosition.Y = playerPos.Y;
            _roamOffset = Vector3.Zero;  // Reset roam when catching up
        }
        else if (distance < FollowDistance * 0.4f)
        {
            // Too close, move away slightly
            _targetPosition = playerPos - toPlayer.Normalized() * FollowDistance;
            _targetPosition.Y = playerPos.Y;
        }
        else
        {
            // Within good range - roam around
            _targetPosition = roamTarget;
        }

        // Move toward target
        Vector3 toTarget = _targetPosition - GlobalPosition;
        toTarget.Y = 0;
        if (toTarget.Length() > 0.15f)
        {
            Vector3 moveDir = toTarget.Normalized();
            float currentSpeed = distance > MaxRoamDistance ? MoveSpeed : MoveSpeed * 0.5f;  // Slower when roaming
            GlobalPosition += moveDir * currentSpeed * dt;

            // Face movement direction
            if (moveDir.LengthSquared() > 0.01f)
            {
                var targetRotation = Mathf.Atan2(moveDir.X, moveDir.Z);
                Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetRotation, dt * 8f), 0);
            }

            // Hop while moving (faster hops when rushing back)
            float hopSpeedMod = distance > MaxRoamDistance ? 1.5f : 1f;
            _hopTimer += dt * HopSpeed * hopSpeedMod;
            float hopOffset = Mathf.Abs(Mathf.Sin(_hopTimer * Mathf.Pi)) * HopHeight;
            GlobalPosition = new Vector3(GlobalPosition.X, playerPos.Y + hopOffset, GlobalPosition.Z);
        }
        else
        {
            // Idle hopping
            _idleHopTimer += dt;
            if (_idleHopTimer > 2f)
            {
                // Occasional idle hop
                float idleHop = Mathf.Abs(Mathf.Sin(_idleHopTimer * 2f)) * HopHeight * 0.3f;
                GlobalPosition = new Vector3(GlobalPosition.X, _player.GlobalPosition.Y + idleHop, GlobalPosition.Z);
            }
        }
    }

    private void UpdateAnimation(float dt)
    {
        // GLB animation: use walk if moving, idle if stationary
        if (_glbAnimPlayer != null)
        {
            Vector3 toTarget = _targetPosition - GlobalPosition;
            toTarget.Y = 0;
            bool isMoving = toTarget.Length() > 0.15f;
            PlayGlbAnimation(isMoving ? "walk" : "idle");
            return; // Skip procedural animation if using GLB
        }

        _tailWagTimer += dt * 8f;
        _earWiggleTimer += dt * 3f;

        // Wag tail
        if (_tail != null)
        {
            float wag = Mathf.Sin(_tailWagTimer) * 25f;
            _tail.RotationDegrees = new Vector3(0, wag, 0);
        }

        // Wiggle ears slightly
        if (_earLeft != null)
        {
            float wiggle = Mathf.Sin(_earWiggleTimer) * 5f;
            _earLeft.RotationDegrees = new Vector3(15 + wiggle, -20, -30);
        }
        if (_earRight != null)
        {
            float wiggle = Mathf.Sin(_earWiggleTimer + 0.5f) * 5f;
            _earRight.RotationDegrees = new Vector3(15 + wiggle, 20, 30);
        }
    }

    /// <summary>
    /// Play animation from GLB model's embedded AnimationPlayer.
    /// </summary>
    private void PlayGlbAnimation(string animName)
    {
        if (_glbAnimPlayer?.HasAnimation(animName) == true)
        {
            if (_glbAnimPlayer.CurrentAnimation != animName)
            {
                _glbAnimPlayer.Play(animName);
            }
        }
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        // Player took damage - try to heal if off cooldown
        if (_healCooldown <= 0 && current < max)
        {
            HealPlayer();
        }
    }

    private void HealPlayer()
    {
        if (_player == null) return;

        _healCooldown = AbilityCooldown;

        // Heal the player
        if (_player is FPSController fps)
        {
            fps.Heal((int)HealAmount);
            // Log to combat log
            HUD3D.Instance?.AddCombatLogMessage($"Steve heals you for [color=#44ff44]{(int)HealAmount}[/color] HP!", new Color(0.4f, 0.9f, 0.5f));
        }

        // Visual effect
        if (_glowLight != null)
        {
            _glowLight.LightColor = HealGlowColor;
            _glowLight.LightEnergy = 3f;
        }

        // Play heal sound
        SoundManager3D.Instance?.PlayHealSound(GlobalPosition);

        // Spawn heal particles
        SpawnHealEffect();

        EmitSignal(SignalName.HealedPlayer, HealAmount);
        GD.Print($"[Steve3D] Healed player for {HealAmount}!");
    }

    private void SpawnHealEffect()
    {
        // Create upward-floating particles
        var particles = new GpuParticles3D();
        particles.Amount = 20;
        particles.Lifetime = 1.0f;
        particles.OneShot = true;
        particles.Explosiveness = 0.8f;

        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 30f;
        mat.InitialVelocityMin = 1f;
        mat.InitialVelocityMax = 2f;
        mat.Gravity = new Vector3(0, -0.5f, 0);
        mat.Color = HealGlowColor;
        particles.ProcessMaterial = mat;

        var sphereMesh = new SphereMesh { Radius = 0.05f };
        particles.DrawPass1 = sphereMesh;

        particles.GlobalPosition = GlobalPosition + new Vector3(0, 0.3f, 0);
        GetTree().Root.AddChild(particles);
        particles.Emitting = true;

        // Auto-cleanup
        var timer = GetTree().CreateTimer(2.0);
        timer.Timeout += () => particles.QueueFree();
    }

    private void CheckForThreats(float dt)
    {
        if (_missileCooldown > 0 || _player == null) return;

        // Don't attack during edit mode (map editor)
        if (_player is FPSController fps && fps.IsEditMode) return;

        // Find enemies that are attacking the player
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is BasicEnemy3D enemy)
            {
                // Check if enemy is in aggro/attacking state and close to player
                if ((enemy.CurrentState == BasicEnemy3D.State.Aggro ||
                     enemy.CurrentState == BasicEnemy3D.State.Attacking) &&
                    enemy.GlobalPosition.DistanceTo(_player.GlobalPosition) < 10f)
                {
                    FireMagicMissile(enemy);
                    return;
                }
            }
            else if (node is BossEnemy3D boss)
            {
                if ((boss.CurrentState == BossEnemy3D.State.Aggro ||
                     boss.CurrentState == BossEnemy3D.State.Attacking) &&
                    boss.GlobalPosition.DistanceTo(_player.GlobalPosition) < 15f)
                {
                    FireMagicMissile(boss);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Aggressive combat behavior when transformed into a monster.
    /// Actively seeks and attacks nearby enemies.
    /// </summary>
    private void CheckForThreatsTransformed(float dt)
    {
        if (_transformAttackTimer > 0 || _player == null) return;

        // Don't attack during edit mode
        if (_player is FPSController fps && fps.IsEditMode) return;

        // Find nearest enemy within attack range
        Node3D? nearestTarget = null;
        float nearestDist = float.MaxValue;

        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D enemy && GodotObject.IsInstanceValid(enemy))
            {
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);

                // Check if within attack range and closer than current target
                if (dist <= AttackRange && dist < nearestDist)
                {
                    // Skip dead enemies
                    if (enemy is BasicEnemy3D basic && basic.CurrentState == BasicEnemy3D.State.Dead) continue;
                    if (enemy is BossEnemy3D boss && boss.CurrentState == BossEnemy3D.State.Dead) continue;

                    nearestTarget = enemy;
                    nearestDist = dist;
                }
            }
        }

        // Attack nearest target
        if (nearestTarget != null)
        {
            PerformTransformedAttack(nearestTarget);
        }
    }

    /// <summary>
    /// Execute an attack as the transformed monster form.
    /// </summary>
    private void PerformTransformedAttack(Node3D target)
    {
        _transformAttackTimer = AttackCooldown;

        // Get attack color based on form
        Color attackColor = GetTransformAttackColor();

        // Visual glow effect
        if (_glowLight != null)
        {
            _glowLight.LightColor = attackColor;
            _glowLight.LightEnergy = 2.5f;
        }

        // Get target name for combat log
        string targetName = target.Name;
        if (target is BasicEnemy3D enemy)
            targetName = enemy.MonsterType;
        else if (target is BossEnemy3D boss)
            targetName = boss.BossName;

        if (IsRangedAttacker)
        {
            // Fire a projectile at the target
            Vector3 spawnPos = GlobalPosition + new Vector3(0, 0.25f, 0);
            Vector3 direction = (target.GlobalPosition + new Vector3(0, 0.5f, 0) - spawnPos).Normalized();

            var projectile = Projectile3D.Create(
                spawnPos,
                direction,
                AttackDamage,
                isPlayerProjectile: true,
                color: attackColor,
                source: $"Steve ({CurrentForm})"
            );

            GetTree().Root.AddChild(projectile);
            projectile.Fire(direction);

            SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);
            GD.Print($"[Steve3D] ({CurrentForm}) Fired at {targetName} for {AttackDamage} damage!");
        }
        else
        {
            // Melee attack - deal damage directly
            if (target.HasMethod("TakeDamage"))
            {
                target.Call("TakeDamage", AttackDamage, GlobalPosition, $"Steve ({CurrentForm})");
            }

            SoundManager3D.Instance?.PlayHitSound(GlobalPosition);
            SpawnMeleeAttackEffect(target.GlobalPosition);
            GD.Print($"[Steve3D] ({CurrentForm}) Melee attacked {targetName} for {AttackDamage} damage!");
        }

        // Combat log message
        HUD3D.Instance?.AddCombatLogMessage(
            $"Steve ({CurrentForm}) attacks [color=#ff6666]{targetName}[/color] for [color=#ffaa44]{(int)AttackDamage}[/color]!",
            attackColor);
    }

    /// <summary>
    /// Get attack color based on current monster form.
    /// </summary>
    private Color GetTransformAttackColor()
    {
        return CurrentForm switch
        {
            "goblin_shaman" => new Color(0.5f, 1f, 0.5f),      // Green magic
            "goblin_thrower" => new Color(0.9f, 0.6f, 0.3f),   // Orange thrown
            "eye" => new Color(1f, 0.3f, 0.3f),                // Red eye beam
            "dragon" or "dragon_king" => new Color(1f, 0.5f, 0.1f),  // Fire orange
            "spider" or "spider_queen" => new Color(0.6f, 0.8f, 0.3f), // Poison green
            "slime" or "slime_king" => new Color(0.3f, 0.9f, 0.4f),   // Slime green
            "skeleton" or "skeleton_lord" => new Color(0.8f, 0.8f, 0.9f), // Bone white
            _ => new Color(0.8f, 0.4f, 0.4f)                    // Default red
        };
    }

    /// <summary>
    /// Create a visual effect for melee attacks.
    /// </summary>
    private void SpawnMeleeAttackEffect(Vector3 targetPos)
    {
        // Create slash effect toward target
        Vector3 midPoint = (GlobalPosition + targetPos) / 2f + new Vector3(0, 0.5f, 0);

        var particles = new GpuParticles3D();
        particles.Amount = 8;
        particles.Lifetime = 0.3f;
        particles.OneShot = true;
        particles.Explosiveness = 1f;

        var mat = new ParticleProcessMaterial();
        mat.Direction = (targetPos - GlobalPosition).Normalized();
        mat.Spread = 20f;
        mat.InitialVelocityMin = 3f;
        mat.InitialVelocityMax = 5f;
        mat.Gravity = Vector3.Zero;
        mat.Color = GetTransformAttackColor();
        particles.ProcessMaterial = mat;

        var sphereMesh = new SphereMesh { Radius = 0.08f, Height = 0.16f };
        particles.DrawPass1 = sphereMesh;

        particles.GlobalPosition = midPoint;
        GetTree().Root.AddChild(particles);
        particles.Emitting = true;

        // Auto-cleanup
        var timer = GetTree().CreateTimer(1.0);
        timer.Timeout += () => particles.QueueFree();
    }

    private void FireMagicMissile(Node3D target)
    {
        _missileCooldown = AbilityCooldown;

        // Visual effect
        if (_glowLight != null)
        {
            _glowLight.LightColor = MissileGlowColor;
            _glowLight.LightEnergy = 3f;
        }

        // Create projectile
        Vector3 spawnPos = GlobalPosition + new Vector3(0, 0.25f, 0);
        Vector3 direction = (target.GlobalPosition - spawnPos).Normalized();

        var projectile = Projectile3D.Create(
            spawnPos,
            direction,
            MagicMissileDamage,
            isPlayerProjectile: true,
            color: MissileGlowColor,
            source: "Steve"
        );

        GetTree().Root.AddChild(projectile);
        projectile.Fire(direction);

        // Play magic sound
        SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);

        // Log to combat log
        string targetName = target.Name;
        if (target is BasicEnemy3D enemy)
            targetName = enemy.MonsterType;
        else if (target is BossEnemy3D boss)
            targetName = boss.BossName;
        HUD3D.Instance?.AddCombatLogMessage($"Steve attacks [color=#ff6666]{targetName}[/color]!", new Color(0.6f, 0.8f, 1f));

        EmitSignal(SignalName.FiredMissile, target);
        GD.Print($"[Steve3D] Fired magic missile at {target.Name}!");
    }

    /// <summary>
    /// Get current heal cooldown remaining
    /// </summary>
    public float GetHealCooldown() => Mathf.Max(0, _healCooldown);

    /// <summary>
    /// Get current missile cooldown remaining
    /// </summary>
    public float GetMissileCooldown() => Mathf.Max(0, _missileCooldown);

    public override void _ExitTree()
    {
        // Disconnect signals
        if (_player is FPSController fps)
        {
            fps.HealthChanged -= OnPlayerHealthChanged;
        }
        Instance = null;
    }
}
