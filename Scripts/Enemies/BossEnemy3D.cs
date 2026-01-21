using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.UI;
using SafeRoom3D.Broadcaster;

namespace SafeRoom3D.Enemies;

/// <summary>
/// 3D Boss enemy with special abilities. Ported from 2D BossEnemy.
/// Larger, more health, special attacks based on monster type.
/// </summary>
public partial class BossEnemy3D : CharacterBody3D
{
    // Boss configuration
    public string MonsterId { get; set; } = "slime_boss";
    public string MonsterType { get; set; } = "Slime";
    public string BossName { get; private set; } = "Slime King";

    // Stats (scaled up from basic enemies)
    public int Level { get; set; } = 1;
    public int MaxHealth { get; private set; } = 300;
    public int CurrentHealth { get; private set; } = 300;
    public float MoveSpeed { get; private set; } = 2.5f;
    public float Damage { get; private set; } = 25f;
    public float AttackCooldown { get; private set; } = 1.5f;
    public float AggroRange { get; private set; } = 20f;
    public float DeaggroRange { get; private set; } = 35f;
    public float AttackRange { get; private set; } = 3f;
    public int ExperienceReward { get; private set; } = 50; // XP awarded on death (high for bosses)

    // State machine
    public enum State { Idle, Aggro, Attacking, SpecialAttack, Stunned, Dead, Stasis }
    public State CurrentState { get; private set; } = State.Idle;

    // Stasis mode (for In-Map Editor - boss plays idle animation but doesn't react)
    private bool _isInStasis;

    // Components
    private MeshInstance3D? _meshInstance;
    private CollisionShape3D? _collider;
    private Node3D? _player;

    // AI state
    private float _attackTimer;
    private float _specialAttackTimer;
    private float _stateTimer;
    private float _stunTimer;

    // Special attack configuration
    private float _specialAttackCooldown = 8f;
    private bool _isEnraged;
    private float _enrageThreshold = 0.3f; // Enrage at 30% health

    // Visual
    private Color _baseColor = new(0.5f, 0.8f, 0.4f);
    private StandardMaterial3D? _material;
    private float _hitFlashTimer;
    private float _pulseTimer;

    // Hit shake effect (model wiggle on damage for haptic feedback)
    private float _hitShakeTimer;
    private float _hitShakeIntensity;
    private Vector3 _hitShakeOffset;
    private const float HitShakeDuration = 0.3f; // Slightly longer for bosses
    private const float HitShakeBaseIntensity = 0.12f; // Bigger shake for bosses

    // Animation support
    private MonsterMeshFactory.LimbNodes? _limbNodes;
    private AnimationPlayer? _animPlayer;
    private AnimationPlayer? _glbAnimPlayer;  // AnimationPlayer from GLB model
    private Dictionary<AnimationType, string>? _glbAnimationNames;  // Cached animation name mapping for GLB
    private AnimationType _currentAnimType = AnimationType.Idle;
    private string _currentAnimName = "";
    private int _currentAnimVariant = 0;

    // Health bar (always visible for boss)
    private Node3D? _healthBarContainer;
    private MeshInstance3D? _healthBarBg;
    private MeshInstance3D? _healthBarFill;
    private Label3D? _bossNameLabel;

    // Signals
    [Signal] public delegate void DiedEventHandler(BossEnemy3D boss);
    [Signal] public delegate void DamagedEventHandler(int damage, int remaining);
    [Signal] public delegate void EnragedEventHandler();
    [Signal] public delegate void SpecialAttackEventHandler(string attackType);

    public override void _Ready()
    {
        LoadBossConfig();
        SetupComponents();

        CurrentHealth = MaxHealth;
        _specialAttackTimer = _specialAttackCooldown * 0.5f; // Start halfway charged

        // Find player - use deferred to ensure player is ready
        CallDeferred(MethodName.FindPlayer);

        // Start in idle
        ChangeState(State.Idle);

        GD.Print($"[BossEnemy3D] Spawned: {BossName} ({MonsterType}) at {GlobalPosition}");
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("Player") as Node3D;
        if (_player == null)
        {
            _player = FPSController.Instance;
        }
    }

    /// <summary>
    /// Check if there's a clear line of sight to the player (no walls blocking).
    /// </summary>
    private bool HasLineOfSightToPlayer()
    {
        if (_player == null) return false;

        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return false;

        Vector3 fromPos = GlobalPosition + new Vector3(0, 1.5f, 0); // Boss is taller
        Vector3 toPos = _player.GlobalPosition + new Vector3(0, 1.0f, 0);

        var query = PhysicsRayQueryParameters3D.Create(fromPos, toPos);
        query.CollisionMask = 128 | 16; // Wall + Obstacle layers
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);
        return result.Count == 0;
    }

    private void LoadBossConfig()
    {
        // Configure based on monster type
        switch (MonsterType.ToLower())
        {
            case "slime":
                BossName = "Slime King";
                MaxHealth = 300;
                MoveSpeed = 2.5f;
                Damage = 25f;
                _baseColor = new Color(0.4f, 0.85f, 0.4f);
                _specialAttackCooldown = 8f;
                break;

            case "eye":
                BossName = "All-Seeing Eye";
                MaxHealth = 250;
                MoveSpeed = 3f;
                Damage = 30f;
                _baseColor = new Color(0.9f, 0.3f, 0.3f);
                _specialAttackCooldown = 6f;
                break;

            case "mushroom":
                BossName = "Sporeling Elder";
                MaxHealth = 350;
                MoveSpeed = 1.8f;
                Damage = 20f;
                _baseColor = new Color(0.7f, 0.5f, 0.6f);
                _specialAttackCooldown = 10f;
                break;

            case "spider":
                BossName = "Broodmother";
                MaxHealth = 220;
                MoveSpeed = 4f;
                Damage = 35f;
                _baseColor = new Color(0.35f, 0.3f, 0.25f);
                _specialAttackCooldown = 5f;
                break;

            case "lizard":
                BossName = "Lizard Chieftain";
                MaxHealth = 400;
                MoveSpeed = 2.8f;
                Damage = 28f;
                _baseColor = new Color(0.4f, 0.6f, 0.35f);
                _specialAttackCooldown = 7f;
                break;

            case "goblin":
                BossName = "Goblin Warlord";
                MaxHealth = 350;
                MoveSpeed = 3.5f;
                Damage = 30f;
                _baseColor = new Color(0.35f, 0.5f, 0.3f); // Darker green
                _specialAttackCooldown = 6f;
                break;

            case "dragon":
                BossName = "Ancient Dragon";
                MaxHealth = 600;
                MoveSpeed = 2.5f;
                Damage = 45f;
                _baseColor = new Color(0.7f, 0.25f, 0.2f); // Deep red
                _specialAttackCooldown = 8f;
                break;

            case "dragon_king":
                BossName = "Dragon King";
                MaxHealth = 900;
                MoveSpeed = 2.8f;
                Damage = 60f;
                _baseColor = new Color(0.85f, 0.2f, 0.15f); // Bright red
                _specialAttackCooldown = 6f;
                break;

            case "skeleton_lord":
                BossName = "Skeleton Lord";
                MaxHealth = 500;
                MoveSpeed = 3.2f;
                Damage = 40f;
                _baseColor = new Color(0.9f, 0.88f, 0.8f); // Bone white
                _specialAttackCooldown = 5f;
                break;

            case "spider_queen":
                BossName = "Spider Queen";
                MaxHealth = 450;
                MoveSpeed = 4.5f;
                Damage = 45f;
                _baseColor = new Color(0.2f, 0.15f, 0.2f); // Dark purple-black
                _specialAttackCooldown = 4f;
                break;

            // DCC Bosses
            case "the_butcher":
                BossName = "The Butcher";
                MaxHealth = 800;
                MoveSpeed = 3.5f;
                Damage = 55f;
                _baseColor = new Color(0.5f, 0.2f, 0.2f); // Blood red
                _specialAttackCooldown = 6f;
                break;

            case "mordecai_the_traitor":
                BossName = "Mordecai the Traitor";
                MaxHealth = 600;
                MoveSpeed = 4f;
                Damage = 40f;
                _baseColor = new Color(0.3f, 0.3f, 0.35f); // Dark steel
                _specialAttackCooldown = 5f;
                break;

            case "princess_donut":
                BossName = "Princess Donut";
                MaxHealth = 400;
                MoveSpeed = 5f;
                Damage = 30f;
                _baseColor = new Color(0.9f, 0.7f, 0.5f); // Golden tabby
                _specialAttackCooldown = 4f;
                break;

            case "mongo_the_destroyer":
                BossName = "Mongo the Destroyer";
                MaxHealth = 1200;
                MoveSpeed = 2.5f;
                Damage = 70f;
                _baseColor = new Color(0.6f, 0.5f, 0.4f); // Stone gray
                _specialAttackCooldown = 8f;
                break;

            case "zev_the_loot_goblin":
                BossName = "Zev the Loot Goblin";
                MaxHealth = 350;
                MoveSpeed = 6f;
                Damage = 25f;
                _baseColor = new Color(0.4f, 0.6f, 0.3f); // Goblin green with gold trim
                _specialAttackCooldown = 3f;
                break;

            default:
                BossName = $"{MonsterType} Boss";
                _baseColor = new Color(0.6f, 0.6f, 0.6f);
                break;
        }

        CurrentHealth = MaxHealth;
    }

    private void SetupComponents()
    {
        _meshInstance = new MeshInstance3D();
        _meshInstance.Position = new Vector3(0, 0, 0);
        _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        AddChild(_meshInstance);

        // Create material with slight glow for boss
        _material = new StandardMaterial3D();
        _material.AlbedoColor = _baseColor;
        _material.Roughness = 0.6f;
        _material.EmissionEnabled = true;
        _material.Emission = _baseColor * 0.3f;
        _material.EmissionEnergyMultiplier = 0.5f;

        // Create specialized mesh based on type using MonsterMeshFactory for animation support
        string meshType = MonsterType.ToLower();

        // Check for GLB model override first
        bool usedGlbModel = false;
        if (GlbModelConfig.TryGetMonsterGlbPath(meshType, out string? glbPath) && !string.IsNullOrWhiteSpace(glbPath))
        {
            var glbModel = GlbModelConfig.LoadGlbModel(glbPath, 1f);
            if (glbModel != null)
            {
                // Apply Y offset to fix models with origin at center instead of feet
                float yOffset = GlbModelConfig.GetMonsterYOffset(meshType);
                if (yOffset != 0f)
                {
                    glbModel.Position = new Vector3(0, yOffset, 0);
                }

                _meshInstance.AddChild(glbModel);
                _limbNodes = null; // GLB models don't have procedural LimbNodes
                _glbAnimPlayer = GlbModelConfig.FindAnimationPlayer(glbModel);
                usedGlbModel = true;

                // Detect and cache animation names from GLB
                if (_glbAnimPlayer != null)
                {
                    CacheGlbAnimationNames();
                }
                GD.Print($"[BossEnemy3D] Loaded GLB for {BossName}, AnimPlayer: {_glbAnimPlayer != null}, YOffset: {yOffset}");
            }
            else
            {
                GD.PrintErr($"[BossEnemy3D] Failed to load GLB for {BossName}, falling back to procedural");
            }
        }

        // Skip procedural mesh creation if GLB was loaded
        if (!usedGlbModel) switch (meshType)
        {
            case "skeleton_lord":
            case "dragon_king":
            case "spider_queen":
            // DCC Bosses - use MonsterMeshFactory for animation support
            case "the_butcher":
            case "mordecai_the_traitor":
            case "princess_donut":
            case "mongo_the_destroyer":
            case "zev_the_loot_goblin":
                // Editor bosses - use MonsterMeshFactory for animation support
                _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance, meshType, _baseColor);
                break;
            case "goblin":
                CreateArmoredGoblinMesh();
                break;
            case "slime":
                CreateSlimeKingMesh();
                break;
            case "eye":
                CreateAllSeeingEyeMesh();
                break;
            case "mushroom":
                CreateSporelingElderMesh();
                break;
            case "spider":
                CreateBroodmotherMesh();
                break;
            case "lizard":
                CreateLizardChieftainMesh();
                break;
            case "dragon":
                CreateDragonMesh();
                break;
            default:
                // Fallback: larger capsule
                var capsule = new CapsuleMesh();
                capsule.Radius = 0.6f;
                capsule.Height = 1.8f;
                var fallbackMesh = new MeshInstance3D();
                fallbackMesh.Mesh = capsule;
                fallbackMesh.MaterialOverride = _material;
                fallbackMesh.Position = new Vector3(0, 0.9f, 0);
                _meshInstance.AddChild(fallbackMesh);
                break;
        }

        // Set up AnimationPlayer for bosses with limb nodes
        SetupAnimationPlayer();

        // Create larger collision
        _collider = new CollisionShape3D();
        var shape = new CapsuleShape3D();
        shape.Radius = 0.6f;
        shape.Height = 1.8f;
        _collider.Shape = shape;
        _collider.Position = new Vector3(0, 0.9f, 0);
        AddChild(_collider);

        // Set collision layers
        CollisionLayer = 2; // Enemy layer
        CollisionMask = 1 | 16 | 128; // Player, Obstacle, Wall (layer 8 = bit 7 = 128)

        // Add to groups
        AddToGroup("Enemies");
        AddToGroup("Boss");

        // Create boss health bar (always visible)
        CreateBossHealthBar();
    }

    private void CreateBossHealthBar()
    {
        // Container that will billboard toward camera
        _healthBarContainer = new Node3D();
        _healthBarContainer.Name = "BossHealthBar";
        _healthBarContainer.Position = new Vector3(0, 3.0f, 0); // Above the boss head
        AddChild(_healthBarContainer);

        // Boss name label
        _bossNameLabel = new Label3D();
        _bossNameLabel.Text = BossName;
        _bossNameLabel.FontSize = 48;
        _bossNameLabel.Position = new Vector3(0, 0.25f, 0);
        _bossNameLabel.Modulate = new Color(1f, 0.85f, 0.4f); // Gold text
        _bossNameLabel.OutlineModulate = new Color(0, 0, 0);
        _bossNameLabel.OutlineSize = 4;
        _bossNameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _bossNameLabel.NoDepthTest = false; // Don't render through walls
        _healthBarContainer.AddChild(_bossNameLabel);

        // Background (dark with border effect)
        _healthBarBg = new MeshInstance3D();
        var bgMesh = new BoxMesh();
        bgMesh.Size = new Vector3(2.0f, 0.25f, 0.02f); // Wider bar for boss
        _healthBarBg.Mesh = bgMesh;
        var bgMat = new StandardMaterial3D();
        bgMat.AlbedoColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        bgMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        bgMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        bgMat.NoDepthTest = false; // Don't render through walls
        _healthBarBg.MaterialOverride = bgMat;
        _healthBarBg.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(_healthBarBg);

        // Border frame
        var borderMesh = new MeshInstance3D();
        var borderBox = new BoxMesh();
        borderBox.Size = new Vector3(2.1f, 0.28f, 0.015f);
        borderMesh.Mesh = borderBox;
        var borderMat = new StandardMaterial3D();
        borderMat.AlbedoColor = new Color(0.6f, 0.5f, 0.3f); // Bronze border
        borderMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        borderMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        borderMat.NoDepthTest = false; // Don't render through walls
        borderMesh.MaterialOverride = borderMat;
        borderMesh.Position = new Vector3(0, 0, -0.01f);
        borderMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(borderMesh);

        // Fill (starts green)
        _healthBarFill = new MeshInstance3D();
        _healthBarFillMesh = new BoxMesh();
        _healthBarFillMesh.Size = new Vector3(1.9f, 0.2f, 0.025f);
        _healthBarFill.Mesh = _healthBarFillMesh;
        _healthBarFillMat = new StandardMaterial3D();
        _healthBarFillMat.AlbedoColor = new Color(0.2f, 0.9f, 0.2f);
        _healthBarFillMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _healthBarFillMat.EmissionEnabled = true;
        _healthBarFillMat.Emission = new Color(0.1f, 0.5f, 0.1f);
        _healthBarFillMat.EmissionEnergyMultiplier = 0.8f;
        _healthBarFillMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        _healthBarFillMat.NoDepthTest = false; // Don't render through walls
        _healthBarFill.MaterialOverride = _healthBarFillMat;
        _healthBarFill.Position = new Vector3(0, 0, 0.01f);
        _healthBarFill.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(_healthBarFill);

        // Boss health bar visibility respects nameplate toggle (always visible when enabled)
        _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
    }

    // Store reference to fill mesh and material for updates
    private BoxMesh? _healthBarFillMesh;
    private StandardMaterial3D? _healthBarFillMat;

    private void UpdateBossHealthBar()
    {
        if (_healthBarContainer == null || _healthBarFill == null) return;

        // Calculate health percentage
        float healthPercent = (float)CurrentHealth / MaxHealth;
        healthPercent = Mathf.Clamp(healthPercent, 0f, 1f);

        // Update fill width by modifying mesh size (same as regular enemies)
        float fullWidth = 1.9f;
        float currentWidth = fullWidth * healthPercent;

        // Modify the mesh directly
        if (_healthBarFillMesh != null)
        {
            _healthBarFillMesh.Size = new Vector3(currentWidth, 0.2f, 0.025f);
        }

        // Offset to keep left-aligned
        _healthBarFill.Position = new Vector3((currentWidth - fullWidth) / 2f, 0, 0.01f);

        // Update color based on health (green -> yellow -> orange -> red)
        Color healthColor;
        if (healthPercent > 0.6f)
        {
            healthColor = new Color(0.2f, 0.9f, 0.2f); // Green
        }
        else if (healthPercent > 0.3f)
        {
            healthColor = new Color(0.95f, 0.8f, 0.2f); // Yellow
        }
        else
        {
            healthColor = new Color(0.95f, 0.2f, 0.2f); // Red - enraged!
        }

        // Update fill color using cached material reference
        if (_healthBarFillMat != null)
        {
            _healthBarFillMat.AlbedoColor = healthColor;
            _healthBarFillMat.Emission = healthColor * 0.4f;
        }

        // Update name color when enraged
        if (_bossNameLabel != null && _isEnraged)
        {
            float pulse = (Mathf.Sin(_pulseTimer * 5f) + 1f) * 0.5f;
            _bossNameLabel.Modulate = new Color(1f, 0.3f + pulse * 0.2f, 0.2f);
        }

        // Note: Billboard mode is set on each material, so no LookAt needed
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Stasis mode - only update visuals and animation, no AI processing
        if (_isInStasis)
        {
            _pulseTimer += dt;
            UpdateVisuals();
            if (_healthBarContainer != null)
            {
                _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
            }
            UpdateBossHealthBar();
            return;
        }

        // Try to find player if we don't have a reference
        if (_player == null)
        {
            _player = FPSController.Instance;
        }

        // Pulse effect for boss
        _pulseTimer += dt;
        UpdateVisuals();

        // Update health bar visibility (respects nameplate toggle - always visible when enabled)
        if (_healthBarContainer != null)
        {
            _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
        }
        UpdateBossHealthBar();

        // Hit flash decay
        if (_hitFlashTimer > 0)
        {
            _hitFlashTimer -= dt;
        }

        // Hit shake effect update
        if (_hitShakeTimer > 0 && _meshInstance != null)
        {
            _hitShakeTimer -= dt;
            float shakeProgress = _hitShakeTimer / HitShakeDuration;
            float currentIntensity = _hitShakeIntensity * shakeProgress;

            // Rapid random wiggle
            float shakeFreq = 40f;
            _hitShakeOffset = new Vector3(
                Mathf.Sin(_pulseTimer * shakeFreq) * currentIntensity,
                Mathf.Sin(_pulseTimer * shakeFreq * 1.3f) * currentIntensity * 0.3f,
                Mathf.Cos(_pulseTimer * shakeFreq * 0.9f) * currentIntensity
            );
            _meshInstance.Position = _hitShakeOffset;
        }
        else if (_meshInstance != null && _hitShakeOffset != Vector3.Zero)
        {
            _hitShakeOffset = Vector3.Zero;
            _meshInstance.Position = Vector3.Zero;
        }

        // Dead bosses don't process AI
        if (CurrentState == State.Dead) return;

        // Apply gravity
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Constants.Gravity * dt, Velocity.Z);
        }

        // Update cooldowns
        if (_attackTimer > 0) _attackTimer -= dt;
        if (_specialAttackTimer > 0) _specialAttackTimer -= dt;
        if (_stunTimer > 0) _stunTimer -= dt;

        // Check for enrage
        CheckEnrage();

        // Process current state
        switch (CurrentState)
        {
            case State.Idle:
                ProcessIdle(dt);
                break;
            case State.Aggro:
                ProcessAggro(dt);
                break;
            case State.Attacking:
                ProcessAttacking(dt);
                break;
            case State.SpecialAttack:
                ProcessSpecialAttack(dt);
                break;
            case State.Stunned:
                ProcessStunned(dt);
                break;
        }

        // Apply movement
        MoveAndSlide();
    }

    private void ProcessIdle(float dt)
    {
        // Check for player aggro - requires line of sight
        if (CheckPlayerDistance() <= AggroRange && HasLineOfSightToPlayer())
        {
            ChangeState(State.Aggro);
        }
    }

    private void ProcessAggro(float dt)
    {
        float dist = CheckPlayerDistance();

        // Deaggro if too far
        if (dist > DeaggroRange)
        {
            ChangeState(State.Idle);
            return;
        }

        // Try special attack first if ready
        if (_specialAttackTimer <= 0 && dist <= AggroRange)
        {
            ChangeState(State.SpecialAttack);
            return;
        }

        // Normal attack if in range
        if (dist <= AttackRange && _attackTimer <= 0)
        {
            ChangeState(State.Attacking);
            return;
        }

        // Chase player
        if (_player != null)
        {
            Vector3 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
            direction.Y = 0;

            float speed = _isEnraged ? MoveSpeed * 1.5f : MoveSpeed;
            Velocity = new Vector3(direction.X * speed, Velocity.Y, direction.Z * speed);

            // Face player using Atan2 for correct facing (models face +Z by default)
            if (direction.LengthSquared() > 0.01f)
            {
                float targetAngle = Mathf.Atan2(direction.X, direction.Z);
                Rotation = new Vector3(0, targetAngle, 0);
            }
        }
    }

    private void ProcessAttacking(float dt)
    {
        _stateTimer -= dt;

        if (_stateTimer <= 0)
        {
            PerformAttack();
            _attackTimer = _isEnraged ? AttackCooldown * 0.7f : AttackCooldown;
            ChangeState(State.Aggro);
        }
    }

    private void ProcessSpecialAttack(float dt)
    {
        _stateTimer -= dt;

        // Stop moving during special
        Velocity = new Vector3(0, Velocity.Y, 0);

        if (_stateTimer <= 0)
        {
            PerformSpecialAttack();
            _specialAttackTimer = _isEnraged ? _specialAttackCooldown * 0.6f : _specialAttackCooldown;
            ChangeState(State.Aggro);
        }
    }

    private void ProcessStunned(float dt)
    {
        Velocity = new Vector3(0, Velocity.Y, 0);

        if (_stunTimer <= 0)
        {
            ChangeState(State.Aggro);
        }
    }

    private void PerformAttack()
    {
        if (_player == null) return;

        // Play attack sound
        PlayMonsterSound("attack");

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (dist <= AttackRange * 1.3f)
        {
            float damage = _isEnraged ? Damage * 1.5f : Damage;

            if (_player.HasMethod("TakeDamage"))
            {
                _player.Call("TakeDamage", damage, GlobalPosition, BossName);
                GD.Print($"[BossEnemy3D] {BossName} attacked player for {damage} damage");
            }
        }
    }

    private void PerformSpecialAttack()
    {
        string attackType = GetSpecialAttackType();
        EmitSignal(SignalName.SpecialAttack, attackType);

        GD.Print($"[BossEnemy3D] {BossName} used {attackType}!");

        switch (attackType)
        {
            case "ground_slam":
                PerformGroundSlam();
                break;
            case "projectile_barrage":
                PerformProjectileBarrage();
                break;
            case "summon_minions":
                PerformSummonMinions();
                break;
            case "charge":
                PerformCharge();
                break;
            case "poison_cloud":
                PerformPoisonCloud();
                break;
        }
    }

    private string GetSpecialAttackType()
    {
        return MonsterType.ToLower() switch
        {
            "slime" => "ground_slam",
            "eye" => "projectile_barrage",
            "mushroom" => "poison_cloud",
            "spider" => "summon_minions",
            "lizard" => "charge",
            _ => "ground_slam"
        };
    }

    private void PerformGroundSlam()
    {
        // AOE damage around boss
        if (_player == null) return;

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
        float slamRadius = 5f;

        if (dist <= slamRadius && _player.HasMethod("TakeDamage"))
        {
            float damage = Damage * 0.8f;
            _player.Call("TakeDamage", damage, GlobalPosition, $"{BossName} (Ground Slam)");
            GD.Print($"[BossEnemy3D] Ground slam hit player for {damage} damage");
        }

        // TODO: Add visual shockwave effect
    }

    private void PerformProjectileBarrage()
    {
        // Fire multiple projectiles at player
        // TODO: Spawn projectile nodes
        GD.Print("[BossEnemy3D] Projectile barrage! (TODO: spawn projectiles)");
    }

    private void PerformSummonMinions()
    {
        // Spawn smaller enemies
        // TODO: Create minion spawning
        GD.Print("[BossEnemy3D] Summoning minions! (TODO: spawn minions)");
    }

    private void PerformCharge()
    {
        // Fast dash toward player
        if (_player == null) return;

        Vector3 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
        direction.Y = 0;
        Velocity = direction * MoveSpeed * 4f;

        // TODO: Add charge damage on impact
        GD.Print("[BossEnemy3D] Charging!");
    }

    private void PerformPoisonCloud()
    {
        // Create poison area
        // TODO: Spawn poison cloud node
        GD.Print("[BossEnemy3D] Poison cloud! (TODO: spawn poison area)");
    }

    private void ChangeState(State newState)
    {
        var previousState = CurrentState;
        CurrentState = newState;

        switch (newState)
        {
            case State.Idle:
                Velocity = new Vector3(0, Velocity.Y, 0);
                PlayAnimation(AnimationType.Idle);
                break;

            case State.Aggro:
                // Play aggro sound when first entering combat
                if (previousState == State.Idle)
                {
                    PlayMonsterSound("aggro");
                    // Notify AI broadcaster of boss encounter
                    AIBroadcaster.Instance?.OnBossEncounter(BossName);
                }
                // Bosses run when in aggro state
                PlayAnimation(AnimationType.Run);
                break;

            case State.Attacking:
                _stateTimer = 0.4f; // Wind-up time
                Velocity = new Vector3(0, Velocity.Y, 0);
                PlayAnimation(AnimationType.Attack);
                break;

            case State.SpecialAttack:
                _stateTimer = 0.8f; // Longer wind-up for special
                Velocity = new Vector3(0, Velocity.Y, 0);
                // Special attacks also play aggro sound for dramatic effect
                PlayMonsterSound("aggro");
                PlayAnimation(AnimationType.Attack);
                break;

            case State.Dead:
                Velocity = Vector3.Zero;
                // Death animation is handled in Die() method
                break;
        }
    }

    /// <summary>
    /// Play a monster-specific sound using the MonsterSounds system.
    /// Uses boss-specific IDs if available (e.g., "dragon_king"), falls back to base type.
    /// </summary>
    private void PlayMonsterSound(string action)
    {
        // Try boss-specific ID first (e.g., "skeleton_lord", "dragon_king")
        string bossId = MonsterId.ToLower().Replace("_boss", "");

        // Map common boss patterns to sound IDs
        if (bossId.Contains("slime"))
            bossId = "slime";
        else if (bossId.Contains("dragon"))
            bossId = MonsterType.ToLower().Contains("king") ? "dragon_king" : "dragon";
        else if (bossId.Contains("skeleton"))
            bossId = MonsterType.ToLower().Contains("lord") ? "skeleton_lord" : "skeleton";
        else if (bossId.Contains("spider"))
            bossId = MonsterType.ToLower().Contains("queen") ? "spider_queen" : "spider";
        else
            bossId = MonsterType.ToLower();

        SoundManager3D.Instance?.PlayMonsterSoundAt(bossId, action, GlobalPosition);
    }

    private float CheckPlayerDistance()
    {
        if (_player == null) return float.MaxValue;
        return GlobalPosition.DistanceTo(_player.GlobalPosition);
    }

    private void CheckEnrage()
    {
        if (_isEnraged) return;

        float healthPercent = (float)CurrentHealth / MaxHealth;
        if (healthPercent <= _enrageThreshold)
        {
            _isEnraged = true;
            EmitSignal(SignalName.Enraged);
            GD.Print($"[BossEnemy3D] {BossName} is ENRAGED!");
        }
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source = "Unknown")
    {
        if (CurrentState == State.Dead) return;

        int intDamage = (int)damage;
        CurrentHealth -= intDamage;

        // Visual feedback
        _hitFlashTimer = 0.2f;

        // Hit shake effect (intensity scales with damage proportion)
        float damageRatio = Mathf.Min(1f, damage / (MaxHealth * 0.1f)); // 10% HP hit = full intensity (bosses are tanky)
        _hitShakeTimer = HitShakeDuration;
        _hitShakeIntensity = HitShakeBaseIntensity * (0.5f + damageRatio * 0.5f);

        // Play hit animation (if not already dying)
        if (CurrentHealth > 0 && _animPlayer != null)
        {
            PlayAnimation(AnimationType.Hit);
        }

        // Play hit sound
        PlayMonsterSound("hit");

        // Slight knockback (bosses resist more)
        Vector3 knockDir = (GlobalPosition - fromPosition).Normalized();
        knockDir.Y = 0;
        Velocity += knockDir * Constants.KnockbackForce * 0.3f;

        // Aggro on damage
        if (CurrentState == State.Idle)
        {
            ChangeState(State.Aggro);
        }

        EmitSignal(SignalName.Damaged, intDamage, CurrentHealth);
        UpdateBossHealthBar();

        GD.Print($"[BossEnemy3D] {BossName} took {intDamage} damage from {source}. HP: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Stun(float duration)
    {
        if (CurrentState == State.Dead) return;

        _stunTimer = duration;
        ChangeState(State.Stunned);
        GD.Print($"[BossEnemy3D] {BossName} stunned for {duration}s");
    }

    /// <summary>
    /// Check if boss is in stasis (for In-Map Editor).
    /// </summary>
    public bool IsInStasis() => _isInStasis;

    /// <summary>
    /// Enter stasis mode - boss plays idle animation but doesn't react to player.
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

        GD.Print($"[BossEnemy3D] {BossName} entered stasis");
    }

    /// <summary>
    /// Exit stasis mode - boss resumes normal AI behavior.
    /// </summary>
    public void ExitStasis()
    {
        if (!_isInStasis) return;

        _isInStasis = false;
        ChangeState(State.Idle);

        GD.Print($"[BossEnemy3D] {BossName} exited stasis");
    }

    private void Die()
    {
        ChangeState(State.Dead);
        CollisionLayer = 0;
        CollisionMask = 0;

        // Spawn blood splatter effect - bosses get bigger splatter
        SpawnBloodSplatter();

        // Play death sound
        PlayMonsterSound("die");

        EmitSignal(SignalName.Died, this);

        GD.Print($"[BossEnemy3D] {BossName} has been defeated!");

        // Notify AI broadcaster of boss defeat
        AIBroadcaster.Instance?.OnBossDefeated(BossName);

        // Record boss kill in game stats
        GameStats.Instance?.RecordKill(MonsterType, isBoss: true);

        // Award XP to player
        if (FPSController.Instance != null && ExperienceReward > 0)
        {
            FPSController.Instance.AddExperience(ExperienceReward);
            HUD3D.Instance?.LogEnemyDeath($"BOSS: {BossName}", ExperienceReward);

            // Register kill for achievements (true = is boss)
            HUD3D.Instance?.RegisterKill(BossName, true);
        }

        // Play death animation
        PlayAnimation(AnimationType.Die);

        // Wait for death animation to complete before spawning corpse
        float deathAnimDuration = 2.5f;

        var tween = CreateTween();
        tween.TweenInterval(deathAnimDuration);
        // Then spawn corpse and clean up
        tween.TweenCallback(Callable.From(() =>
        {
            SpawnCorpse();
            QueueFree();
        }));
    }

    private void SpawnCorpse()
    {
        var corpse = Corpse3D.Create(BossName, true, GlobalPosition, Rotation.Y, Level);
        GetTree().Root.AddChild(corpse);
        GD.Print($"[BossEnemy3D] Spawned boss corpse for {BossName} (Level {Level})");
    }

    /// <summary>
    /// Spawns blood splatter effect at the boss's death location.
    /// Bosses get larger, more dramatic blood splatter.
    /// </summary>
    private void SpawnBloodSplatter()
    {
        var splatter = BloodSplatter3D.Create(GlobalPosition, MonsterType, isBoss: true);
        GetTree().Root.AddChild(splatter);
    }

    /// <summary>
    /// Set up AnimationPlayer with procedural animations for boss monsters
    /// </summary>
    private void SetupAnimationPlayer()
    {
        if (_limbNodes == null || _meshInstance == null)
        {
            GD.Print($"[BossEnemy3D] {BossName} - No limb nodes, skipping animation setup");
            return;
        }

        try
        {
            _animPlayer = MonsterAnimationSystem.CreateAnimationPlayer(
                _meshInstance,
                MonsterType.ToLower(),
                _limbNodes
            );

            if (_animPlayer != null)
            {
                _currentAnimType = AnimationType.Idle;
                _currentAnimName = MonsterAnimationSystem.GetAnimationName(AnimationType.Idle, _currentAnimVariant);

                if (_animPlayer.HasAnimation(_currentAnimName))
                {
                    _animPlayer.Play(_currentAnimName);
                }

                GD.Print($"[BossEnemy3D] {BossName} AnimationPlayer created with {_animPlayer.GetAnimationList().Length} animations");
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[BossEnemy3D] Failed to create AnimationPlayer for {BossName}: {e.Message}");
            _animPlayer = null;
        }
    }

    /// <summary>
    /// Play an animation with optional variant selection
    /// </summary>
    private void PlayAnimation(AnimationType animType, int variant = -1)
    {
        // Try GLB AnimationPlayer first
        if (_glbAnimPlayer != null)
        {
            PlayGlbAnimation(animType);
            return;
        }

        if (_animPlayer == null) return;

        // Pick a random variant if not specified
        if (variant < 0)
        {
            variant = GD.RandRange(0, 2); // 3 variants per type
        }

        _currentAnimType = animType;
        _currentAnimVariant = variant;
        _currentAnimName = MonsterAnimationSystem.GetAnimationName(animType, variant);

        if (_animPlayer.HasAnimation(_currentAnimName))
        {
            // For one-shot animations (attack, hit, die), always restart
            if (animType == AnimationType.Attack || animType == AnimationType.Hit || animType == AnimationType.Die)
            {
                _animPlayer.Stop();
                _animPlayer.Play(_currentAnimName);
            }
            else
            {
                // For looping animations, only switch if different
                if (_animPlayer.CurrentAnimation != _currentAnimName)
                {
                    _animPlayer.Play(_currentAnimName);
                }
            }
        }
    }

    /// <summary>
    /// Cache animation names from GLB model by trying common variants.
    /// GLB files often use different naming conventions: Idle, idle, IDLE, Armature|idle, etc.
    /// </summary>
    private void CacheGlbAnimationNames()
    {
        if (_glbAnimPlayer == null) return;

        _glbAnimationNames = new Dictionary<AnimationType, string>();
        var availableAnims = _glbAnimPlayer.GetAnimationList();

        // Log all available animations for debugging
        GD.Print($"[BossEnemy3D] {BossName} GLB animations: {string.Join(", ", availableAnims)}");

        // Map each animation type to available animations
        _glbAnimationNames[AnimationType.Idle] = FindMatchingAnimation(availableAnims, "idle", "Idle", "IDLE", "rest", "Rest", "breathing", "Breathing");
        _glbAnimationNames[AnimationType.Walk] = FindMatchingAnimation(availableAnims, "walk", "Walk", "WALK", "walking", "Walking");
        _glbAnimationNames[AnimationType.Run] = FindMatchingAnimation(availableAnims, "run", "Run", "RUN", "running", "Running", "sprint", "Sprint");
        _glbAnimationNames[AnimationType.Attack] = FindMatchingAnimation(availableAnims, "attack", "Attack", "ATTACK", "Attack1", "attack1", "bite", "Bite", "hit", "Hit", "slash", "Slash");
        _glbAnimationNames[AnimationType.Hit] = FindMatchingAnimation(availableAnims, "hit", "Hit", "HIT", "damage", "Damage", "hurt", "Hurt", "react", "React");
        _glbAnimationNames[AnimationType.Die] = FindMatchingAnimation(availableAnims, "die", "Die", "DIE", "death", "Death", "dead", "Dead");

        // Log resolved mappings
        foreach (var kvp in _glbAnimationNames)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                GD.Print($"[BossEnemy3D] {BossName} {kvp.Key} -> '{kvp.Value}'");
            }
        }
    }

    /// <summary>
    /// Find the first matching animation name from a list of variants.
    /// Also checks for Armature| prefix which is common in Blender exports.
    /// </summary>
    private string FindMatchingAnimation(string[] availableAnims, params string[] variants)
    {
        foreach (var variant in variants)
        {
            // Try exact match first
            foreach (var anim in availableAnims)
            {
                if (anim == variant)
                    return anim;
            }

            // Try with Armature| prefix (common in Blender exports)
            foreach (var anim in availableAnims)
            {
                if (anim == $"Armature|{variant}")
                    return anim;
            }

            // Try case-insensitive contains match as last resort
            foreach (var anim in availableAnims)
            {
                if (anim.ToLower().Contains(variant.ToLower()))
                    return anim;
            }
        }
        return "";
    }

    /// <summary>
    /// Play animation from GLB model's embedded AnimationPlayer.
    /// Uses cached animation name mapping to handle different naming conventions.
    /// </summary>
    private void PlayGlbAnimation(AnimationType animType)
    {
        if (_glbAnimPlayer == null) return;

        // Get cached animation name or fall back to defaults
        string animName = "";
        if (_glbAnimationNames?.TryGetValue(animType, out var cached) == true && !string.IsNullOrEmpty(cached))
        {
            animName = cached;
        }
        else
        {
            // Fallback to lowercase defaults
            animName = animType switch
            {
                AnimationType.Idle => "idle",
                AnimationType.Walk => "walk",
                AnimationType.Run => "run",
                AnimationType.Attack => "attack",
                AnimationType.Hit => "hit",
                AnimationType.Die => "die",
                _ => "idle"
            };
        }

        _currentAnimType = animType;

        if (!string.IsNullOrEmpty(animName) && _glbAnimPlayer.HasAnimation(animName))
        {
            // For one-shot animations (attack, hit, die), always restart
            if (animType == AnimationType.Attack || animType == AnimationType.Hit || animType == AnimationType.Die)
            {
                _glbAnimPlayer.Stop();
                _glbAnimPlayer.Play(animName);
            }
            else
            {
                // For looping animations, only switch if different
                if (_glbAnimPlayer.CurrentAnimation != animName)
                {
                    _glbAnimPlayer.Play(animName);
                }
            }
        }
    }

    /// <summary>
    /// Check if current animation has finished playing
    /// </summary>
    private bool IsAnimationFinished()
    {
        var player = _glbAnimPlayer ?? _animPlayer;
        if (player == null) return true;
        return !player.IsPlaying() || player.CurrentAnimationPosition >= player.CurrentAnimationLength - 0.05f;
    }

    private void UpdateVisuals()
    {
        if (_material == null) return;

        float pulse = 1f + Mathf.Sin(_pulseTimer * 2f) * 0.1f;

        if (_hitFlashTimer > 0)
        {
            // Flash white when hit
            float flash = _hitFlashTimer / 0.2f;
            _material.AlbedoColor = _baseColor.Lerp(Colors.White, flash * 0.8f);
            _material.EmissionEnergyMultiplier = 1f + flash;
        }
        else if (_isEnraged)
        {
            // Red pulsing when enraged
            Color enragedColor = _baseColor.Lerp(Colors.Red, 0.4f);
            _material.AlbedoColor = enragedColor;
            _material.EmissionEnergyMultiplier = 0.8f + pulse * 0.4f;
        }
        else
        {
            // Normal boss glow
            _material.AlbedoColor = _baseColor;
            _material.EmissionEnergyMultiplier = 0.5f + pulse * 0.2f;
        }
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        // Bosses resist knockback
        direction.Y = 0;
        Velocity += direction.Normalized() * force * 0.3f;
    }

    /// <summary>
    /// Creates a large armored goblin mesh (1.5x scale, with armor pieces)
    /// </summary>
    private void CreateArmoredGoblinMesh()
    {
        float scale = 1.6f; // Boss is 60% larger

        // Materials
        var skinMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            Roughness = 0.8f,
            EmissionEnabled = true,
            Emission = _baseColor * 0.2f
        };

        var armorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.35f, 0.3f),
            Metallic = 0.7f,
            Roughness = 0.4f
        };

        var goldMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.65f, 0.2f),
            Metallic = 0.9f,
            Roughness = 0.3f
        };

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.3f, 0.2f), // Red angry eyes
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.2f) * 0.8f
        };

        // === BODY (larger, armored torso) ===
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.35f * scale, Height = 0.7f * scale };
        body.Mesh = bodyMesh;
        body.MaterialOverride = armorMat;
        body.Position = new Vector3(0, 0.7f * scale, 0);
        body.Scale = new Vector3(1.1f, 1.3f, 0.9f);
        _meshInstance.AddChild(body);

        // === CHEST PLATE ===
        var chestPlate = new MeshInstance3D();
        var chestMesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.4f * scale, 0.15f * scale) };
        chestPlate.Mesh = chestMesh;
        chestPlate.MaterialOverride = armorMat;
        chestPlate.Position = new Vector3(0, 0.7f * scale, 0.18f * scale);
        _meshInstance.AddChild(chestPlate);

        // === HEAD (larger, meaner) ===
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.28f * scale, Height = 0.56f * scale };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        head.Position = new Vector3(0, 1.15f * scale, 0);
        _meshInstance.AddChild(head);

        // === HELMET ===
        var helmet = new MeshInstance3D();
        var helmetMesh = new SphereMesh { Radius = 0.3f * scale, Height = 0.35f * scale };
        helmet.Mesh = helmetMesh;
        helmet.MaterialOverride = armorMat;
        helmet.Position = new Vector3(0, 1.25f * scale, 0);
        helmet.Scale = new Vector3(1.1f, 0.6f, 1.1f);
        _meshInstance.AddChild(helmet);

        // === HELMET SPIKE ===
        var spike = new MeshInstance3D();
        var spikeMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.06f * scale, Height = 0.25f * scale };
        spike.Mesh = spikeMesh;
        spike.MaterialOverride = goldMat;
        spike.Position = new Vector3(0, 1.45f * scale, 0);
        _meshInstance.AddChild(spike);

        // === EYES (angry red) ===
        var eyeMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.1f * scale, 1.18f * scale, 0.2f * scale);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.1f * scale, 1.18f * scale, 0.2f * scale);
        _meshInstance.AddChild(rightEye);

        // === NOSE (big goblin nose) ===
        var nose = new MeshInstance3D();
        var noseMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.07f * scale, Height = 0.2f * scale };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = skinMat;
        nose.Position = new Vector3(0, 1.1f * scale, 0.28f * scale);
        nose.RotationDegrees = new Vector3(-70, 0, 0);
        _meshInstance.AddChild(nose);

        // === EARS (pointed) ===
        var earMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.08f * scale, Height = 0.25f * scale };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.3f * scale, 1.15f * scale, 0);
        leftEar.RotationDegrees = new Vector3(0, 0, -50);
        _meshInstance.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.3f * scale, 1.15f * scale, 0);
        rightEar.RotationDegrees = new Vector3(0, 0, 50);
        _meshInstance.AddChild(rightEar);

        // === SHOULDER PAULDRONS ===
        var pauldronMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.18f * scale };
        var leftPauldron = new MeshInstance3D { Mesh = pauldronMesh, MaterialOverride = armorMat };
        leftPauldron.Position = new Vector3(-0.4f * scale, 0.85f * scale, 0);
        leftPauldron.Scale = new Vector3(1.2f, 0.8f, 1f);
        _meshInstance.AddChild(leftPauldron);

        var rightPauldron = new MeshInstance3D { Mesh = pauldronMesh, MaterialOverride = armorMat };
        rightPauldron.Position = new Vector3(0.4f * scale, 0.85f * scale, 0);
        rightPauldron.Scale = new Vector3(1.2f, 0.8f, 1f);
        _meshInstance.AddChild(rightPauldron);

        // === ARMS (thick, muscular) ===
        var armMesh = new CapsuleMesh { Radius = 0.1f * scale, Height = 0.5f * scale };
        var leftArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        leftArm.Position = new Vector3(-0.4f * scale, 0.5f * scale, 0);
        leftArm.RotationDegrees = new Vector3(0, 0, 20);
        _meshInstance.AddChild(leftArm);

        var rightArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        rightArm.Position = new Vector3(0.4f * scale, 0.5f * scale, 0);
        rightArm.RotationDegrees = new Vector3(0, 0, -20);
        _meshInstance.AddChild(rightArm);

        // === LEGS (armored) ===
        var legMesh = new CapsuleMesh { Radius = 0.1f * scale, Height = 0.4f * scale };
        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = armorMat };
        leftLeg.Position = new Vector3(-0.15f * scale, 0.2f * scale, 0);
        _meshInstance.AddChild(leftLeg);

        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = armorMat };
        rightLeg.Position = new Vector3(0.15f * scale, 0.2f * scale, 0);
        _meshInstance.AddChild(rightLeg);

        // === BIG SWORD ===
        var swordBlade = new MeshInstance3D();
        var bladeMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.8f * scale, 0.02f * scale) };
        swordBlade.Mesh = bladeMesh;
        swordBlade.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.7f, 0.75f),
            Metallic = 0.95f,
            Roughness = 0.2f
        };
        swordBlade.Position = new Vector3(0.55f * scale, 0.5f * scale, 0.2f * scale);
        swordBlade.RotationDegrees = new Vector3(30, 0, -20);
        _meshInstance.AddChild(swordBlade);

        var swordHilt = new MeshInstance3D();
        var hiltMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.04f * scale, Height = 0.2f * scale };
        swordHilt.Mesh = hiltMesh;
        swordHilt.MaterialOverride = goldMat;
        swordHilt.Position = new Vector3(0.48f * scale, 0.15f * scale, 0.15f * scale);
        swordHilt.RotationDegrees = new Vector3(30, 0, -20);
        _meshInstance.AddChild(swordHilt);

        GD.Print("[BossEnemy3D] Created armored goblin warlord mesh");
    }

    /// <summary>
    /// Creates an imposing Slime King with crown, drips, and translucent body (2x normal size)
    /// </summary>
    private void CreateSlimeKingMesh()
    {
        float scale = 2.0f; // Double size for intimidation

        var slimeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(_baseColor.R, _baseColor.G, _baseColor.B, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.2f,
            Metallic = 0.1f,
            EmissionEnabled = true,
            Emission = _baseColor * 0.5f,
            EmissionEnergyMultiplier = 0.8f
        };

        var crownMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.75f, 0.2f),
            Metallic = 0.9f,
            Roughness = 0.25f,
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.75f, 0.2f) * 0.3f
        };

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.1f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.8f, 0.2f),
            EmissionEnergyMultiplier = 1.2f
        };

        // Main body - large translucent blob
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.8f * scale, Height = 1.6f * scale };
        body.Mesh = bodyMesh;
        body.MaterialOverride = slimeMat;
        body.Position = new Vector3(0, 0.9f * scale, 0);
        body.Scale = new Vector3(1.2f, 0.9f, 1.1f); // Squashed sphere
        _meshInstance.AddChild(body);

        // Crown - 5 pointed spikes
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Pi * 2 / 5;
            var spike = new MeshInstance3D();
            var spikeMesh = new CylinderMesh
            {
                TopRadius = 0.01f,
                BottomRadius = 0.08f * scale,
                Height = 0.35f * scale
            };
            spike.Mesh = spikeMesh;
            spike.MaterialOverride = crownMat;
            float radius = 0.65f * scale;
            spike.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                1.5f * scale,
                Mathf.Sin(angle) * radius
            );
            spike.RotationDegrees = new Vector3(0, 0, 15); // Slight tilt outward
            _meshInstance.AddChild(spike);
        }

        // Crown band
        var crownBand = new MeshInstance3D();
        var bandMesh = new TorusMesh
        {
            InnerRadius = 0.55f * scale,
            OuterRadius = 0.65f * scale
        };
        crownBand.Mesh = bandMesh;
        crownBand.MaterialOverride = crownMat;
        crownBand.Position = new Vector3(0, 1.35f * scale, 0);
        crownBand.Scale = new Vector3(1f, 0.3f, 1f);
        _meshInstance.AddChild(crownBand);

        // Eyes - large glowing eyes
        var eyeMesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.3f * scale, 1.0f * scale, 0.6f * scale);
        leftEye.Scale = new Vector3(1.2f, 1f, 0.6f);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.3f * scale, 1.0f * scale, 0.6f * scale);
        rightEye.Scale = new Vector3(1.2f, 1f, 0.6f);
        _meshInstance.AddChild(rightEye);

        // Drip effects - hanging slime drips
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Pi * 2 / 8;
            var drip = new MeshInstance3D();
            var dripMesh = new CylinderMesh
            {
                TopRadius = 0.08f * scale,
                BottomRadius = 0.02f * scale,
                Height = 0.3f * scale
            };
            drip.Mesh = dripMesh;
            drip.MaterialOverride = slimeMat;
            float radius = 0.7f * scale;
            drip.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                0.3f * scale,
                Mathf.Sin(angle) * radius
            );
            _meshInstance.AddChild(drip);
        }

        // Core - darker nucleus visible through translucent body
        var core = new MeshInstance3D();
        var coreMesh = new SphereMesh { Radius = 0.3f * scale, Height = 0.6f * scale };
        core.Mesh = coreMesh;
        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor * 0.5f,
            EmissionEnabled = true,
            Emission = _baseColor * 0.8f,
            EmissionEnergyMultiplier = 1.5f
        };
        core.MaterialOverride = coreMat;
        core.Position = new Vector3(0, 0.8f * scale, 0);
        _meshInstance.AddChild(core);

        GD.Print("[BossEnemy3D] Created Slime King mesh with crown and drips");
    }

    /// <summary>
    /// Creates a terrifying All-Seeing Eye with multiple smaller eyes, veins, and eyelid
    /// </summary>
    private void CreateAllSeeingEyeMesh()
    {
        float scale = 1.8f; // 80% larger

        var eyeballMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.92f, 0.88f),
            Roughness = 0.3f,
            Metallic = 0.0f
        };

        var irisMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            EmissionEnabled = true,
            Emission = _baseColor * 0.8f,
            EmissionEnergyMultiplier = 1.5f
        };

        var pupilMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.05f, 0.05f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.2f),
            EmissionEnergyMultiplier = 2.0f
        };

        var veinMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.2f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.1f, 0.1f),
            EmissionEnergyMultiplier = 0.5f
        };

        // Main eyeball
        var eyeball = new MeshInstance3D();
        var eyeballMesh = new SphereMesh { Radius = 0.7f * scale, Height = 1.4f * scale };
        eyeball.Mesh = eyeballMesh;
        eyeball.MaterialOverride = eyeballMat;
        eyeball.Position = new Vector3(0, 1.2f * scale, 0);
        eyeball.Scale = new Vector3(1.1f, 1f, 1.1f);
        _meshInstance.AddChild(eyeball);

        // Large iris
        var iris = new MeshInstance3D();
        var irisMesh = new SphereMesh { Radius = 0.5f * scale, Height = 0.4f * scale };
        iris.Mesh = irisMesh;
        iris.MaterialOverride = irisMat;
        iris.Position = new Vector3(0, 1.2f * scale, 0.65f * scale);
        iris.Scale = new Vector3(1f, 1f, 0.3f);
        _meshInstance.AddChild(iris);

        // Pupil with intense glow
        var pupil = new MeshInstance3D();
        var pupilMesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.15f * scale };
        pupil.Mesh = pupilMesh;
        pupil.MaterialOverride = pupilMat;
        pupil.Position = new Vector3(0, 1.2f * scale, 0.75f * scale);
        pupil.Scale = new Vector3(1f, 1f, 0.5f);
        _meshInstance.AddChild(pupil);

        // Add point light for pupil glow
        var pupilLight = new OmniLight3D();
        pupilLight.LightColor = new Color(1f, 0.3f, 0.2f);
        pupilLight.LightEnergy = 2.0f;
        pupilLight.OmniRange = 8f;
        pupilLight.Position = new Vector3(0, 1.2f * scale, 0.8f * scale);
        _meshInstance.AddChild(pupilLight);

        // Smaller eyes around the main eye (8 total)
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Pi * 2 / 8;
            var smallEye = new MeshInstance3D();
            var smallEyeMesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale };
            smallEye.Mesh = smallEyeMesh;
            smallEye.MaterialOverride = eyeballMat;
            float radius = 0.75f * scale;
            smallEye.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                1.2f * scale,
                Mathf.Sin(angle) * radius
            );
            _meshInstance.AddChild(smallEye);

            // Small iris
            var smallIris = new MeshInstance3D();
            var smallIrisMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.06f * scale };
            smallIris.Mesh = smallIrisMesh;
            smallIris.MaterialOverride = irisMat;
            smallIris.Position = new Vector3(
                Mathf.Cos(angle) * radius * 1.15f,
                1.2f * scale,
                Mathf.Sin(angle) * radius * 1.15f
            );
            _meshInstance.AddChild(smallIris);
        }

        // Blood veins (tentacle-like)
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Pi * 2 / 12;
            var vein = new MeshInstance3D();
            var veinMesh = new CylinderMesh
            {
                TopRadius = 0.02f * scale,
                BottomRadius = 0.06f * scale,
                Height = 0.5f * scale
            };
            vein.Mesh = veinMesh;
            vein.MaterialOverride = veinMat;
            float startRadius = 0.6f * scale;
            vein.Position = new Vector3(
                Mathf.Cos(angle) * startRadius,
                1.0f * scale,
                Mathf.Sin(angle) * startRadius
            );
            vein.RotationDegrees = new Vector3(
                Mathf.Cos(angle) * 45,
                angle * (180f / Mathf.Pi),
                Mathf.Sin(angle) * 45
            );
            _meshInstance.AddChild(vein);
        }

        // Top eyelid
        var eyelid = new MeshInstance3D();
        var eyelidMesh = new SphereMesh { Radius = 0.75f * scale, Height = 0.8f * scale };
        eyelid.Mesh = eyelidMesh;
        var eyelidMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.3f, 0.3f),
            Roughness = 0.7f
        };
        eyelid.MaterialOverride = eyelidMat;
        eyelid.Position = new Vector3(0, 1.5f * scale, 0);
        eyelid.Scale = new Vector3(1.2f, 0.6f, 1.2f);
        _meshInstance.AddChild(eyelid);

        GD.Print("[BossEnemy3D] Created All-Seeing Eye mesh with multiple eyes and veins");
    }

    /// <summary>
    /// Creates a massive Sporeling Elder mushroom with large cap, gills, and spore particles
    /// </summary>
    private void CreateSporelingElderMesh()
    {
        float scale = 1.9f; // 90% larger

        var capMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            Roughness = 0.6f,
            EmissionEnabled = true,
            Emission = _baseColor * 0.4f,
            EmissionEnergyMultiplier = 0.7f
        };

        var stemMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.8f, 0.75f),
            Roughness = 0.8f
        };

        var gillMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.4f, 0.5f),
            EmissionEnabled = true,
            Emission = new Color(0.5f, 0.3f, 0.4f),
            EmissionEnergyMultiplier = 0.5f
        };

        var sporeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.5f, 0.8f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.7f, 0.5f, 0.8f),
            EmissionEnergyMultiplier = 1.5f
        };

        // Large stem
        var stem = new MeshInstance3D();
        var stemMesh = new CylinderMesh
        {
            TopRadius = 0.35f * scale,
            BottomRadius = 0.45f * scale,
            Height = 1.2f * scale
        };
        stem.Mesh = stemMesh;
        stem.MaterialOverride = stemMat;
        stem.Position = new Vector3(0, 0.6f * scale, 0);
        _meshInstance.AddChild(stem);

        // Massive mushroom cap
        var cap = new MeshInstance3D();
        var capMesh = new SphereMesh { Radius = 1.0f * scale, Height = 1.2f * scale };
        cap.Mesh = capMesh;
        cap.MaterialOverride = capMat;
        cap.Position = new Vector3(0, 1.5f * scale, 0);
        cap.Scale = new Vector3(1.3f, 0.7f, 1.3f); // Flat mushroom cap
        _meshInstance.AddChild(cap);

        // Cap spots (darker circles on top)
        for (int i = 0; i < 7; i++)
        {
            float angle = i * Mathf.Pi * 2 / 7;
            var spot = new MeshInstance3D();
            var spotMesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.1f * scale };
            spot.Mesh = spotMesh;
            var spotMat = new StandardMaterial3D
            {
                AlbedoColor = _baseColor * 0.6f
            };
            spot.MaterialOverride = spotMat;
            float radius = 0.6f * scale;
            spot.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                1.8f * scale,
                Mathf.Sin(angle) * radius
            );
            spot.Scale = new Vector3(1f, 0.3f, 1f);
            _meshInstance.AddChild(spot);
        }

        // Gills underneath cap (radial pattern)
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.Pi * 2 / 16;
            var gill = new MeshInstance3D();
            var gillMesh = new BoxMesh
            {
                Size = new Vector3(0.03f * scale, 0.3f * scale, 0.8f * scale)
            };
            gill.Mesh = gillMesh;
            gill.MaterialOverride = gillMat;
            gill.Position = new Vector3(
                Mathf.Cos(angle) * 0.5f * scale,
                1.3f * scale,
                Mathf.Sin(angle) * 0.5f * scale
            );
            gill.RotationDegrees = new Vector3(0, angle * (180f / Mathf.Pi), 0);
            _meshInstance.AddChild(gill);
        }

        // Spore particles (glowing orbs floating around)
        for (int i = 0; i < 20; i++)
        {
            var spore = new MeshInstance3D();
            var sporeMesh = new SphereMesh { Radius = 0.05f * scale, Height = 0.1f * scale };
            spore.Mesh = sporeMesh;
            spore.MaterialOverride = sporeMat;
            // Random positions around the mushroom
            float angle = (float)GD.RandRange(0, Mathf.Pi * 2);
            float radius = (float)GD.RandRange(0.5f, 1.2f) * scale;
            float height = (float)GD.RandRange(0.5f, 2.0f) * scale;
            spore.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                height,
                Mathf.Sin(angle) * radius
            );
            _meshInstance.AddChild(spore);
        }

        // Root tendrils at base
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Pi * 2 / 6;
            var root = new MeshInstance3D();
            var rootMesh = new CylinderMesh
            {
                TopRadius = 0.08f * scale,
                BottomRadius = 0.03f * scale,
                Height = 0.5f * scale
            };
            root.Mesh = rootMesh;
            root.MaterialOverride = stemMat;
            root.Position = new Vector3(
                Mathf.Cos(angle) * 0.3f * scale,
                0.1f * scale,
                Mathf.Sin(angle) * 0.3f * scale
            );
            root.RotationDegrees = new Vector3(
                Mathf.Cos(angle) * 45,
                angle * (180f / Mathf.Pi),
                0
            );
            _meshInstance.AddChild(root);
        }

        GD.Print("[BossEnemy3D] Created Sporeling Elder mesh with cap, gills, and spores");
    }

    /// <summary>
    /// Creates a terrifying Broodmother spider with egg sac, multiple leg details, and mandibles
    /// </summary>
    private void CreateBroodmotherMesh()
    {
        float scale = 1.7f; // 70% larger

        var bodyMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            Roughness = 0.7f,
            EmissionEnabled = true,
            Emission = _baseColor * 0.2f
        };

        var legMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor * 0.8f,
            Roughness = 0.8f
        };

        var eggMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.65f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.4f,
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.5f, 0.3f),
            EmissionEnergyMultiplier = 0.6f
        };

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.1f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.5f, 0.2f),
            EmissionEnergyMultiplier = 1.5f
        };

        var fangMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.9f, 0.85f),
            Metallic = 0.3f,
            Roughness = 0.4f
        };

        // Main abdomen (large rear body)
        var abdomen = new MeshInstance3D();
        var abdomenMesh = new SphereMesh { Radius = 0.6f * scale, Height = 1.2f * scale };
        abdomen.Mesh = abdomenMesh;
        abdomen.MaterialOverride = bodyMat;
        abdomen.Position = new Vector3(0, 0.7f * scale, -0.4f * scale);
        abdomen.Scale = new Vector3(1.1f, 0.9f, 1.5f);
        _meshInstance.AddChild(abdomen);

        // Egg sac (glowing, translucent)
        var eggSac = new MeshInstance3D();
        var eggSacMesh = new SphereMesh { Radius = 0.5f * scale, Height = 1.0f * scale };
        eggSac.Mesh = eggSacMesh;
        eggSac.MaterialOverride = eggMat;
        eggSac.Position = new Vector3(0, 0.5f * scale, -0.9f * scale);
        eggSac.Scale = new Vector3(0.9f, 1.2f, 1.1f);
        _meshInstance.AddChild(eggSac);

        // Visible eggs inside sac (darker spheres)
        for (int i = 0; i < 12; i++)
        {
            var egg = new MeshInstance3D();
            var eggMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale };
            egg.Mesh = eggMesh;
            var darkEggMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.5f, 0.5f, 0.4f, 0.9f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            egg.MaterialOverride = darkEggMat;
            // Random positions inside egg sac
            float angle = (float)GD.RandRange(0, Mathf.Pi * 2);
            float radius = (float)GD.RandRange(0, 0.3f) * scale;
            float yOffset = (float)GD.RandRange(-0.4f, 0.4f) * scale;
            egg.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                0.5f * scale + yOffset,
                -0.9f * scale + Mathf.Sin(angle) * radius
            );
            _meshInstance.AddChild(egg);
        }

        // Cephalothorax (head/chest)
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.45f * scale, Height = 0.9f * scale };
        head.Mesh = headMesh;
        head.MaterialOverride = bodyMat;
        head.Position = new Vector3(0, 0.8f * scale, 0.5f * scale);
        head.Scale = new Vector3(1f, 0.8f, 1.3f);
        _meshInstance.AddChild(head);

        // 8 spider eyes (4 large, 4 small)
        var eyeMesh = new SphereMesh { Radius = 0.1f * scale, Height = 0.15f * scale };
        // Large front eyes
        var eyeL1 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeL1.Position = new Vector3(-0.15f * scale, 0.9f * scale, 0.85f * scale);
        _meshInstance.AddChild(eyeL1);
        var eyeR1 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeR1.Position = new Vector3(0.15f * scale, 0.9f * scale, 0.85f * scale);
        _meshInstance.AddChild(eyeR1);

        // Medium eyes
        var eyeL2 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeL2.Position = new Vector3(-0.3f * scale, 0.95f * scale, 0.7f * scale);
        eyeL2.Scale = new Vector3(0.7f, 0.7f, 0.7f);
        _meshInstance.AddChild(eyeL2);
        var eyeR2 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeR2.Position = new Vector3(0.3f * scale, 0.95f * scale, 0.7f * scale);
        eyeR2.Scale = new Vector3(0.7f, 0.7f, 0.7f);
        _meshInstance.AddChild(eyeR2);

        // Small top eyes
        var eyeL3 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeL3.Position = new Vector3(-0.2f * scale, 1.05f * scale, 0.75f * scale);
        eyeL3.Scale = new Vector3(0.5f, 0.5f, 0.5f);
        _meshInstance.AddChild(eyeL3);
        var eyeR3 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeR3.Position = new Vector3(0.2f * scale, 1.05f * scale, 0.75f * scale);
        eyeR3.Scale = new Vector3(0.5f, 0.5f, 0.5f);
        _meshInstance.AddChild(eyeR3);

        // Small side eyes
        var eyeL4 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeL4.Position = new Vector3(-0.4f * scale, 0.85f * scale, 0.6f * scale);
        eyeL4.Scale = new Vector3(0.5f, 0.5f, 0.5f);
        _meshInstance.AddChild(eyeL4);
        var eyeR4 = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eyeR4.Position = new Vector3(0.4f * scale, 0.85f * scale, 0.6f * scale);
        eyeR4.Scale = new Vector3(0.5f, 0.5f, 0.5f);
        _meshInstance.AddChild(eyeR4);

        // Mandibles/chelicerae (large fangs)
        var fangMesh = new CylinderMesh
        {
            TopRadius = 0.05f * scale,
            BottomRadius = 0.02f * scale,
            Height = 0.4f * scale
        };
        var leftFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = fangMat };
        leftFang.Position = new Vector3(-0.15f * scale, 0.7f * scale, 0.9f * scale);
        leftFang.RotationDegrees = new Vector3(40, 0, -10);
        _meshInstance.AddChild(leftFang);

        var rightFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = fangMat };
        rightFang.Position = new Vector3(0.15f * scale, 0.7f * scale, 0.9f * scale);
        rightFang.RotationDegrees = new Vector3(40, 0, 10);
        _meshInstance.AddChild(rightFang);

        // 8 detailed legs (4 per side)
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;

            for (int leg = 0; leg < 4; leg++)
            {
                float zPos = 0.4f - (leg * 0.3f);

                // Upper leg segment
                var upperLeg = new MeshInstance3D();
                var upperMesh = new CapsuleMesh
                {
                    Radius = 0.08f * scale,
                    Height = 0.6f * scale
                };
                upperLeg.Mesh = upperMesh;
                upperLeg.MaterialOverride = legMat;
                upperLeg.Position = new Vector3(
                    0.35f * scale * sideMultiplier,
                    0.7f * scale,
                    zPos * scale
                );
                upperLeg.RotationDegrees = new Vector3(
                    -30,
                    45 * sideMultiplier,
                    60 * sideMultiplier
                );
                _meshInstance.AddChild(upperLeg);

                // Middle leg segment
                var middleLeg = new MeshInstance3D();
                var middleMesh = new CapsuleMesh
                {
                    Radius = 0.06f * scale,
                    Height = 0.5f * scale
                };
                middleLeg.Mesh = middleMesh;
                middleLeg.MaterialOverride = legMat;
                middleLeg.Position = new Vector3(
                    0.65f * scale * sideMultiplier,
                    0.5f * scale,
                    zPos * scale
                );
                middleLeg.RotationDegrees = new Vector3(
                    30,
                    0,
                    80 * sideMultiplier
                );
                _meshInstance.AddChild(middleLeg);

                // Lower leg segment (foot)
                var lowerLeg = new MeshInstance3D();
                var lowerMesh = new CapsuleMesh
                {
                    Radius = 0.04f * scale,
                    Height = 0.4f * scale
                };
                lowerLeg.Mesh = lowerMesh;
                lowerLeg.MaterialOverride = legMat;
                lowerLeg.Position = new Vector3(
                    0.85f * scale * sideMultiplier,
                    0.2f * scale,
                    zPos * scale
                );
                lowerLeg.RotationDegrees = new Vector3(
                    -60,
                    0,
                    30 * sideMultiplier
                );
                _meshInstance.AddChild(lowerLeg);
            }
        }

        // Hair/bristles (small spikes on body)
        for (int i = 0; i < 30; i++)
        {
            var hair = new MeshInstance3D();
            var hairMesh = new CylinderMesh
            {
                TopRadius = 0.005f * scale,
                BottomRadius = 0.015f * scale,
                Height = 0.1f * scale
            };
            hair.Mesh = hairMesh;
            hair.MaterialOverride = legMat;
            // Random positions on body
            float angle = (float)GD.RandRange(0, Mathf.Pi * 2);
            float radius = (float)GD.RandRange(0.4f, 0.6f) * scale;
            float yPos = (float)GD.RandRange(0.5f, 1.0f) * scale;
            hair.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                yPos,
                Mathf.Sin(angle) * radius * 0.5f
            );
            hair.RotationDegrees = new Vector3(
                (float)GD.RandRange(-30, 30),
                angle * (180f / Mathf.Pi),
                (float)GD.RandRange(-20, 20)
            );
            _meshInstance.AddChild(hair);
        }

        GD.Print("[BossEnemy3D] Created Broodmother mesh with egg sac, 8 legs, and 8 eyes");
    }

    /// <summary>
    /// Creates an armored Lizard Chieftain with armor plates, tribal markings, and large crest
    /// </summary>
    private void CreateLizardChieftainMesh()
    {
        float scale = 1.8f; // 80% larger

        var skinMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            Roughness = 0.7f,
            EmissionEnabled = true,
            Emission = _baseColor * 0.3f
        };

        var armorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.4f, 0.3f),
            Metallic = 0.6f,
            Roughness = 0.5f
        };

        var crestMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.4f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.3f, 0.1f),
            EmissionEnergyMultiplier = 0.8f
        };

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.8f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.8f, 0.2f),
            EmissionEnergyMultiplier = 1.2f
        };

        var markingMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.7f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.6f, 0.2f),
            EmissionEnergyMultiplier = 1.0f
        };

        // Body (muscular torso)
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = 0.4f * scale, Height = 1.0f * scale };
        body.Mesh = bodyMesh;
        body.MaterialOverride = skinMat;
        body.Position = new Vector3(0, 0.8f * scale, 0);
        body.Scale = new Vector3(1.2f, 1.3f, 0.9f);
        _meshInstance.AddChild(body);

        // Armor plates on chest (overlapping)
        for (int i = 0; i < 5; i++)
        {
            var plate = new MeshInstance3D();
            var plateMesh = new BoxMesh
            {
                Size = new Vector3(0.6f * scale, 0.15f * scale, 0.1f * scale)
            };
            plate.Mesh = plateMesh;
            plate.MaterialOverride = armorMat;
            plate.Position = new Vector3(
                0,
                (1.2f - i * 0.15f) * scale,
                0.35f * scale
            );
            plate.Scale = new Vector3(1f - i * 0.1f, 1f, 1f);
            _meshInstance.AddChild(plate);
        }

        // Head (reptilian)
        var head = new MeshInstance3D();
        var headMesh = new CapsuleMesh { Radius = 0.3f * scale, Height = 0.6f * scale };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        head.Position = new Vector3(0, 1.4f * scale, 0.1f * scale);
        head.Scale = new Vector3(0.9f, 1f, 1.3f);
        head.RotationDegrees = new Vector3(-10, 0, 0);
        _meshInstance.AddChild(head);

        // Snout
        var snout = new MeshInstance3D();
        var snoutMesh = new CapsuleMesh { Radius = 0.15f * scale, Height = 0.4f * scale };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = skinMat;
        snout.Position = new Vector3(0, 1.3f * scale, 0.5f * scale);
        snout.Scale = new Vector3(0.8f, 0.7f, 1.2f);
        snout.RotationDegrees = new Vector3(-60, 0, 0);
        _meshInstance.AddChild(snout);

        // Large crest (mohawk-like ridge)
        for (int i = 0; i < 7; i++)
        {
            var crestSpike = new MeshInstance3D();
            var spikeMesh = new CylinderMesh
            {
                TopRadius = 0.01f,
                BottomRadius = 0.1f * scale,
                Height = (0.4f + i * 0.05f) * scale
            };
            crestSpike.Mesh = spikeMesh;
            crestSpike.MaterialOverride = crestMat;
            float zPos = 0.3f - i * 0.1f;
            crestSpike.Position = new Vector3(
                0,
                1.6f * scale,
                zPos * scale
            );
            crestSpike.RotationDegrees = new Vector3(i * 5, 0, 0);
            _meshInstance.AddChild(crestSpike);
        }

        // Eyes (slitted reptilian)
        var eyeMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.12f * scale };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.15f * scale, 1.45f * scale, 0.35f * scale);
        leftEye.Scale = new Vector3(0.6f, 1.2f, 0.8f);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.15f * scale, 1.45f * scale, 0.35f * scale);
        rightEye.Scale = new Vector3(0.6f, 1.2f, 0.8f);
        _meshInstance.AddChild(rightEye);

        // Tribal markings (glowing stripes on body)
        for (int i = 0; i < 6; i++)
        {
            var marking = new MeshInstance3D();
            var markingMesh = new BoxMesh
            {
                Size = new Vector3(0.08f * scale, 0.25f * scale, 0.05f * scale)
            };
            marking.Mesh = markingMesh;
            marking.MaterialOverride = markingMat;
            float angle = i * 60 * (Mathf.Pi / 180f);
            float radius = 0.35f * scale;
            marking.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                1.0f * scale,
                Mathf.Sin(angle) * radius
            );
            marking.RotationDegrees = new Vector3(0, angle * (180f / Mathf.Pi), 0);
            _meshInstance.AddChild(marking);
        }

        // Shoulder armor
        var shoulderMesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.3f * scale };
        var leftShoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = armorMat };
        leftShoulder.Position = new Vector3(-0.5f * scale, 1.2f * scale, 0);
        leftShoulder.Scale = new Vector3(1.3f, 0.8f, 1f);
        _meshInstance.AddChild(leftShoulder);

        var rightShoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = armorMat };
        rightShoulder.Position = new Vector3(0.5f * scale, 1.2f * scale, 0);
        rightShoulder.Scale = new Vector3(1.3f, 0.8f, 1f);
        _meshInstance.AddChild(rightShoulder);

        // Spikes on shoulders
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;
            for (int i = 0; i < 3; i++)
            {
                var spike = new MeshInstance3D();
                var spikeMesh = new CylinderMesh
                {
                    TopRadius = 0.01f,
                    BottomRadius = 0.05f * scale,
                    Height = 0.2f * scale
                };
                spike.Mesh = spikeMesh;
                spike.MaterialOverride = armorMat;
                spike.Position = new Vector3(
                    (0.5f + i * 0.08f) * scale * sideMultiplier,
                    1.25f * scale,
                    (i - 1) * 0.1f * scale
                );
                spike.RotationDegrees = new Vector3(0, 0, 45 * sideMultiplier);
                _meshInstance.AddChild(spike);
            }
        }

        // Arms (muscular)
        var armMesh = new CapsuleMesh { Radius = 0.12f * scale, Height = 0.7f * scale };
        var leftArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        leftArm.Position = new Vector3(-0.5f * scale, 0.7f * scale, 0);
        leftArm.RotationDegrees = new Vector3(0, 0, 25);
        _meshInstance.AddChild(leftArm);

        var rightArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        rightArm.Position = new Vector3(0.5f * scale, 0.7f * scale, 0);
        rightArm.RotationDegrees = new Vector3(0, 0, -25);
        _meshInstance.AddChild(rightArm);

        // Claws
        var clawMesh = new CylinderMesh
        {
            TopRadius = 0.01f,
            BottomRadius = 0.03f * scale,
            Height = 0.15f * scale
        };
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;
            for (int i = 0; i < 3; i++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = eyeMat };
                claw.Position = new Vector3(
                    (0.55f + i * 0.03f) * scale * sideMultiplier,
                    0.35f * scale,
                    (i - 1) * 0.03f * scale
                );
                claw.RotationDegrees = new Vector3(45, 0, 0);
                _meshInstance.AddChild(claw);
            }
        }

        // Legs (armored)
        var legMesh = new CapsuleMesh { Radius = 0.15f * scale, Height = 0.6f * scale };
        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = armorMat };
        leftLeg.Position = new Vector3(-0.2f * scale, 0.3f * scale, 0);
        _meshInstance.AddChild(leftLeg);

        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = armorMat };
        rightLeg.Position = new Vector3(0.2f * scale, 0.3f * scale, 0);
        _meshInstance.AddChild(rightLeg);

        // Tail with armor plates
        for (int i = 0; i < 4; i++)
        {
            var tailSegment = new MeshInstance3D();
            var tailMesh = new CapsuleMesh
            {
                Radius = (0.12f - i * 0.02f) * scale,
                Height = 0.3f * scale
            };
            tailSegment.Mesh = tailMesh;
            tailSegment.MaterialOverride = skinMat;
            tailSegment.Position = new Vector3(
                0,
                (0.5f - i * 0.15f) * scale,
                (-0.3f - i * 0.25f) * scale
            );
            tailSegment.RotationDegrees = new Vector3(-30 - i * 15, 0, 0);
            _meshInstance.AddChild(tailSegment);

            // Armor plate on tail segment
            if (i < 3)
            {
                var tailPlate = new MeshInstance3D();
                var plateMesh = new BoxMesh
                {
                    Size = new Vector3(0.25f * scale, 0.08f * scale, 0.15f * scale)
                };
                tailPlate.Mesh = plateMesh;
                tailPlate.MaterialOverride = armorMat;
                tailPlate.Position = new Vector3(
                    0,
                    (0.6f - i * 0.15f) * scale,
                    (-0.3f - i * 0.25f) * scale
                );
                tailPlate.RotationDegrees = new Vector3(-30 - i * 15, 0, 0);
                _meshInstance.AddChild(tailPlate);
            }
        }

        GD.Print("[BossEnemy3D] Created Lizard Chieftain mesh with armor, crest, and tribal markings");
    }

    /// <summary>
    /// Creates a massive dragon with wings, horns, scales, and intimidating presence
    /// </summary>
    private void CreateDragonMesh()
    {
        float scale = 2.2f; // More than double size - truly massive
        bool isKing = MonsterType.ToLower().Contains("king");
        if (isKing) scale = 2.5f; // Dragon King is even larger

        var scaleMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            Metallic = 0.3f,
            Roughness = 0.6f,
            EmissionEnabled = true,
            Emission = _baseColor * 0.5f,
            EmissionEnergyMultiplier = 1.0f
        };

        var wingMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor * 0.7f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.5f
        };

        var hornMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.3f, 0.35f),
            Metallic = 0.4f,
            Roughness = 0.5f
        };

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.7f, 0.2f),
            EmissionEnergyMultiplier = 2.0f
        };

        var fireMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.5f, 0.1f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.4f, 0.1f),
            EmissionEnergyMultiplier = 2.5f
        };

        // Massive body
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = 0.6f * scale, Height = 1.4f * scale };
        body.Mesh = bodyMesh;
        body.MaterialOverride = scaleMat;
        body.Position = new Vector3(0, 1.2f * scale, 0);
        body.Scale = new Vector3(1.3f, 1.2f, 1f);
        body.RotationDegrees = new Vector3(-15, 0, 0);
        _meshInstance.AddChild(body);

        // Chest (barrel-chested)
        var chest = new MeshInstance3D();
        var chestMesh = new SphereMesh { Radius = 0.7f * scale, Height = 1.2f * scale };
        chest.Mesh = chestMesh;
        chest.MaterialOverride = scaleMat;
        chest.Position = new Vector3(0, 1.3f * scale, 0.3f * scale);
        chest.Scale = new Vector3(1.1f, 1f, 1.3f);
        _meshInstance.AddChild(chest);

        // Head (draconic)
        var head = new MeshInstance3D();
        var headMesh = new CapsuleMesh { Radius = 0.35f * scale, Height = 0.7f * scale };
        head.Mesh = headMesh;
        head.MaterialOverride = scaleMat;
        head.Position = new Vector3(0, 1.8f * scale, 0.8f * scale);
        head.Scale = new Vector3(0.9f, 0.8f, 1.4f);
        head.RotationDegrees = new Vector3(-30, 0, 0);
        _meshInstance.AddChild(head);

        // Snout (long, fearsome)
        var snout = new MeshInstance3D();
        var snoutMesh = new CapsuleMesh { Radius = 0.2f * scale, Height = 0.6f * scale };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = scaleMat;
        snout.Position = new Vector3(0, 1.65f * scale, 1.3f * scale);
        snout.Scale = new Vector3(0.7f, 0.6f, 1.2f);
        snout.RotationDegrees = new Vector3(-50, 0, 0);
        _meshInstance.AddChild(snout);

        // Large horns (multiple pairs)
        for (int pair = 0; pair < 2; pair++)
        {
            for (int side = 0; side < 2; side++)
            {
                float sideMultiplier = side == 0 ? -1f : 1f;
                var horn = new MeshInstance3D();
                var hornMesh = new CylinderMesh
                {
                    TopRadius = 0.02f * scale,
                    BottomRadius = 0.08f * scale,
                    Height = (0.6f + pair * 0.2f) * scale
                };
                horn.Mesh = hornMesh;
                horn.MaterialOverride = hornMat;
                horn.Position = new Vector3(
                    (0.2f + pair * 0.1f) * scale * sideMultiplier,
                    (2.0f + pair * 0.15f) * scale,
                    (0.7f - pair * 0.2f) * scale
                );
                horn.RotationDegrees = new Vector3(
                    -30 + pair * 15,
                    35 * sideMultiplier,
                    30 * sideMultiplier
                );
                _meshInstance.AddChild(horn);
            }
        }

        // Eyes (intense, glowing)
        var eyeMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.2f * scale };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.2f * scale, 1.85f * scale, 1.1f * scale);
        leftEye.Scale = new Vector3(0.7f, 1f, 0.6f);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.2f * scale, 1.85f * scale, 1.1f * scale);
        rightEye.Scale = new Vector3(0.7f, 1f, 0.6f);
        _meshInstance.AddChild(rightEye);

        // Eye glow lights
        for (int side = 0; side < 2; side++)
        {
            var eyeLight = new OmniLight3D();
            eyeLight.LightColor = new Color(1f, 0.7f, 0.2f);
            eyeLight.LightEnergy = 3.0f;
            eyeLight.OmniRange = 12f;
            float sideMultiplier = side == 0 ? -1f : 1f;
            eyeLight.Position = new Vector3(0.2f * scale * sideMultiplier, 1.85f * scale, 1.2f * scale);
            _meshInstance.AddChild(eyeLight);
        }

        // Fangs/teeth
        var toothMesh = new CylinderMesh
        {
            TopRadius = 0.02f * scale,
            BottomRadius = 0.04f * scale,
            Height = 0.2f * scale
        };
        for (int i = 0; i < 6; i++)
        {
            var tooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = hornMat };
            float xOffset = ((i - 2.5f) * 0.12f) * scale;
            tooth.Position = new Vector3(xOffset, 1.55f * scale, 1.5f * scale);
            tooth.RotationDegrees = new Vector3(-50, 0, 0);
            _meshInstance.AddChild(tooth);
        }

        // Spikes along spine
        for (int i = 0; i < 8; i++)
        {
            var spike = new MeshInstance3D();
            var spikeMesh = new CylinderMesh
            {
                TopRadius = 0.01f,
                BottomRadius = 0.1f * scale,
                Height = (0.5f - i * 0.04f) * scale
            };
            spike.Mesh = spikeMesh;
            spike.MaterialOverride = hornMat;
            spike.Position = new Vector3(
                0,
                (1.8f - i * 0.15f) * scale,
                (0.5f - i * 0.25f) * scale
            );
            spike.RotationDegrees = new Vector3(-15 - i * 5, 0, 0);
            _meshInstance.AddChild(spike);
        }

        // Wings (massive, bat-like)
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;

            // Wing arm (bone structure)
            var wingArm = new MeshInstance3D();
            var armMesh = new CapsuleMesh { Radius = 0.08f * scale, Height = 1.2f * scale };
            wingArm.Mesh = armMesh;
            wingArm.MaterialOverride = scaleMat;
            wingArm.Position = new Vector3(
                0.6f * scale * sideMultiplier,
                1.5f * scale,
                0
            );
            wingArm.RotationDegrees = new Vector3(0, 45 * sideMultiplier, 60 * sideMultiplier);
            _meshInstance.AddChild(wingArm);

            // Wing membrane (triangular)
            var wingMembrane = new MeshInstance3D();
            var membraneMesh = new BoxMesh { Size = new Vector3(1.5f * scale, 0.02f * scale, 1.8f * scale) };
            wingMembrane.Mesh = membraneMesh;
            wingMembrane.MaterialOverride = wingMat;
            wingMembrane.Position = new Vector3(
                1.2f * scale * sideMultiplier,
                1.4f * scale,
                -0.2f * scale
            );
            wingMembrane.RotationDegrees = new Vector3(0, 15 * sideMultiplier, 75 * sideMultiplier);
            wingMembrane.Scale = new Vector3(1f, 1f, 1.2f);
            _meshInstance.AddChild(wingMembrane);

            // Wing fingers (3 per wing)
            for (int finger = 0; finger < 3; finger++)
            {
                var wingFinger = new MeshInstance3D();
                var fingerMesh = new CapsuleMesh { Radius = 0.04f * scale, Height = 0.8f * scale };
                wingFinger.Mesh = fingerMesh;
                wingFinger.MaterialOverride = hornMat;
                wingFinger.Position = new Vector3(
                    (1.1f + finger * 0.2f) * scale * sideMultiplier,
                    1.3f * scale,
                    (-0.5f + finger * 0.3f) * scale
                );
                wingFinger.RotationDegrees = new Vector3(
                    -20 + finger * 10,
                    20 * sideMultiplier,
                    80 * sideMultiplier
                );
                _meshInstance.AddChild(wingFinger);
            }
        }

        // Legs (powerful, clawed)
        var legMesh = new CapsuleMesh { Radius = 0.18f * scale, Height = 0.9f * scale };
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;

            // Upper leg
            var upperLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = scaleMat };
            upperLeg.Position = new Vector3(0.35f * scale * sideMultiplier, 0.7f * scale, 0);
            upperLeg.RotationDegrees = new Vector3(-20, 0, 15 * sideMultiplier);
            _meshInstance.AddChild(upperLeg);

            // Lower leg
            var lowerLeg = new MeshInstance3D();
            var lowerMesh = new CapsuleMesh { Radius = 0.12f * scale, Height = 0.7f * scale };
            lowerLeg.Mesh = lowerMesh;
            lowerLeg.MaterialOverride = scaleMat;
            lowerLeg.Position = new Vector3(0.35f * scale * sideMultiplier, 0.25f * scale, -0.3f * scale);
            lowerLeg.RotationDegrees = new Vector3(40, 0, 0);
            _meshInstance.AddChild(lowerLeg);

            // Claws
            var clawMesh = new CylinderMesh
            {
                TopRadius = 0.01f,
                BottomRadius = 0.05f * scale,
                Height = 0.25f * scale
            };
            for (int claw = 0; claw < 3; claw++)
            {
                var clawInstance = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = hornMat };
                clawInstance.Position = new Vector3(
                    (0.35f + claw * 0.08f - 0.08f) * scale * sideMultiplier,
                    0.05f * scale,
                    -0.2f * scale + claw * 0.1f
                );
                clawInstance.RotationDegrees = new Vector3(60, 0, 0);
                _meshInstance.AddChild(clawInstance);
            }
        }

        // Tail (long, spiked)
        for (int i = 0; i < 6; i++)
        {
            var tailSegment = new MeshInstance3D();
            var tailMesh = new CapsuleMesh
            {
                Radius = (0.18f - i * 0.025f) * scale,
                Height = 0.5f * scale
            };
            tailSegment.Mesh = tailMesh;
            tailSegment.MaterialOverride = scaleMat;
            tailSegment.Position = new Vector3(
                0,
                (0.8f - i * 0.2f) * scale,
                (-0.6f - i * 0.45f) * scale
            );
            tailSegment.RotationDegrees = new Vector3(-40 - i * 10, 0, 0);
            _meshInstance.AddChild(tailSegment);

            // Tail spikes
            if (i % 2 == 0)
            {
                var tailSpike = new MeshInstance3D();
                var spikeMesh = new CylinderMesh
                {
                    TopRadius = 0.01f,
                    BottomRadius = 0.06f * scale,
                    Height = 0.3f * scale
                };
                tailSpike.Mesh = spikeMesh;
                tailSpike.MaterialOverride = hornMat;
                tailSpike.Position = new Vector3(
                    0,
                    (0.95f - i * 0.2f) * scale,
                    (-0.6f - i * 0.45f) * scale
                );
                tailSpike.RotationDegrees = new Vector3(-40 - i * 10, 0, 0);
                _meshInstance.AddChild(tailSpike);
            }
        }

        // Fire/smoke emanating from nostrils (for extra intimidation)
        if (isKing)
        {
            for (int side = 0; side < 2; side++)
            {
                float sideMultiplier = side == 0 ? -1f : 1f;
                var fireGlow = new MeshInstance3D();
                var glowMesh = new SphereMesh { Radius = 0.1f * scale, Height = 0.15f * scale };
                fireGlow.Mesh = glowMesh;
                fireGlow.MaterialOverride = fireMat;
                fireGlow.Position = new Vector3(
                    0.12f * scale * sideMultiplier,
                    1.6f * scale,
                    1.55f * scale
                );
                _meshInstance.AddChild(fireGlow);

                // Fire light
                var fireLight = new OmniLight3D();
                fireLight.LightColor = new Color(1f, 0.5f, 0.1f);
                fireLight.LightEnergy = 2.5f;
                fireLight.OmniRange = 10f;
                fireLight.Position = new Vector3(
                    0.12f * scale * sideMultiplier,
                    1.6f * scale,
                    1.6f * scale
                );
                _meshInstance.AddChild(fireLight);
            }
        }

        // Battle scars (darker lines across body)
        for (int i = 0; i < 5; i++)
        {
            var scar = new MeshInstance3D();
            var scarMesh = new BoxMesh { Size = new Vector3(0.05f * scale, 0.4f * scale, 0.02f * scale) };
            scar.Mesh = scarMesh;
            var scarMat = new StandardMaterial3D
            {
                AlbedoColor = _baseColor * 0.5f
            };
            scar.MaterialOverride = scarMat;
            float angle = (float)GD.RandRange(0, 360);
            float yPos = (float)GD.RandRange(0.8f, 1.5f) * scale;
            scar.Position = new Vector3(
                (float)GD.RandRange(-0.5f, 0.5f) * scale,
                yPos,
                (float)GD.RandRange(-0.3f, 0.5f) * scale
            );
            scar.RotationDegrees = new Vector3(0, angle, (float)GD.RandRange(-30, 30));
            _meshInstance.AddChild(scar);
        }

        GD.Print($"[BossEnemy3D] Created {(isKing ? "Dragon King" : "Dragon")} mesh with wings, horns, and fearsome details");
    }

    private void CreateSkeletonLordMesh()
    {
        float scale = 1.4f;

        // Materials
        var boneMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            Roughness = 0.7f,
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.35f, 0.25f),
            EmissionEnergyMultiplier = 0.3f
        };

        var armorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.18f, 0.15f),
            Metallic = 0.8f,
            Roughness = 0.4f
        };

        var crownMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.5f, 0.3f),
            Metallic = 0.9f,
            Roughness = 0.3f,
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.6f, 0.2f),
            EmissionEnergyMultiplier = 0.5f
        };

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.2f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f),
            EmissionEnergyMultiplier = 3f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        // Skull (elongated, menacing)
        var skull = new MeshInstance3D();
        var skullMesh = new SphereMesh { Radius = 0.25f * scale, Height = 0.35f * scale };
        skull.Mesh = skullMesh;
        skull.MaterialOverride = boneMat;
        skull.Position = new Vector3(0, 2.0f * scale, 0);
        skull.Scale = new Vector3(0.9f, 1.1f, 0.95f);
        _meshInstance!.AddChild(skull);

        // Eye sockets with glowing eyes
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;
            var eyeSocket = new MeshInstance3D();
            var socketMesh = new SphereMesh { Radius = 0.06f * scale, Height = 0.08f * scale };
            eyeSocket.Mesh = socketMesh;
            eyeSocket.MaterialOverride = eyeMat;
            eyeSocket.Position = new Vector3(0.08f * scale * sideMultiplier, 2.05f * scale, 0.18f * scale);
            _meshInstance.AddChild(eyeSocket);
        }

        // Jaw bone
        var jaw = new MeshInstance3D();
        var jawMesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.08f * scale, 0.12f * scale) };
        jaw.Mesh = jawMesh;
        jaw.MaterialOverride = boneMat;
        jaw.Position = new Vector3(0, 1.82f * scale, 0.08f * scale);
        _meshInstance.AddChild(jaw);

        // Crown of bones
        for (int i = 0; i < 5; i++)
        {
            var crownSpike = new MeshInstance3D();
            var spikeMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.03f * scale, Height = 0.25f * scale };
            crownSpike.Mesh = spikeMesh;
            crownSpike.MaterialOverride = crownMat;
            float angle = -60 + i * 30;
            crownSpike.Position = new Vector3(
                Mathf.Sin(Mathf.DegToRad(angle)) * 0.2f * scale,
                2.2f * scale,
                Mathf.Cos(Mathf.DegToRad(angle)) * 0.1f * scale
            );
            crownSpike.RotationDegrees = new Vector3(-15, angle, 0);
            _meshInstance.AddChild(crownSpike);
        }

        // Rib cage with armor plates
        var ribCage = new MeshInstance3D();
        var cageMesh = new CylinderMesh { TopRadius = 0.25f * scale, BottomRadius = 0.2f * scale, Height = 0.6f * scale };
        ribCage.Mesh = cageMesh;
        ribCage.MaterialOverride = boneMat;
        ribCage.Position = new Vector3(0, 1.4f * scale, 0);
        _meshInstance.AddChild(ribCage);

        // Chest armor plate
        var chestArmor = new MeshInstance3D();
        var armorMesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.4f * scale, 0.1f * scale) };
        chestArmor.Mesh = armorMesh;
        chestArmor.MaterialOverride = armorMat;
        chestArmor.Position = new Vector3(0, 1.45f * scale, 0.15f * scale);
        _meshInstance.AddChild(chestArmor);

        // Spine
        var spine = new MeshInstance3D();
        var spineMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.1f * scale, Height = 0.4f * scale };
        spine.Mesh = spineMesh;
        spine.MaterialOverride = boneMat;
        spine.Position = new Vector3(0, 0.95f * scale, 0);
        _meshInstance.AddChild(spine);

        // Pelvis
        var pelvis = new MeshInstance3D();
        var pelvisMesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.15f * scale, 0.2f * scale) };
        pelvis.Mesh = pelvisMesh;
        pelvis.MaterialOverride = boneMat;
        pelvis.Position = new Vector3(0, 0.7f * scale, 0);
        _meshInstance.AddChild(pelvis);

        // Arms with shoulder armor
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;

            // Shoulder armor
            var shoulder = new MeshInstance3D();
            var shoulderMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.15f * scale };
            shoulder.Mesh = shoulderMesh;
            shoulder.MaterialOverride = armorMat;
            shoulder.Position = new Vector3(0.35f * scale * sideMultiplier, 1.6f * scale, 0);
            _meshInstance.AddChild(shoulder);

            // Upper arm bone
            var upperArm = new MeshInstance3D();
            var upperMesh = new CapsuleMesh { Radius = 0.05f * scale, Height = 0.4f * scale };
            upperArm.Mesh = upperMesh;
            upperArm.MaterialOverride = boneMat;
            upperArm.Position = new Vector3(0.4f * scale * sideMultiplier, 1.35f * scale, 0);
            upperArm.RotationDegrees = new Vector3(0, 0, 20 * sideMultiplier);
            _meshInstance.AddChild(upperArm);

            // Lower arm bone
            var lowerArm = new MeshInstance3D();
            var lowerMesh = new CapsuleMesh { Radius = 0.04f * scale, Height = 0.35f * scale };
            lowerArm.Mesh = lowerMesh;
            lowerArm.MaterialOverride = boneMat;
            lowerArm.Position = new Vector3(0.5f * scale * sideMultiplier, 1.0f * scale, 0.1f * scale);
            lowerArm.RotationDegrees = new Vector3(-30, 0, 10 * sideMultiplier);
            _meshInstance.AddChild(lowerArm);

            // Skeletal hand
            var hand = new MeshInstance3D();
            var handMesh = new SphereMesh { Radius = 0.06f * scale, Height = 0.08f * scale };
            hand.Mesh = handMesh;
            hand.MaterialOverride = boneMat;
            hand.Position = new Vector3(0.55f * scale * sideMultiplier, 0.75f * scale, 0.15f * scale);
            hand.Scale = new Vector3(1.2f, 0.6f, 1f);
            _meshInstance.AddChild(hand);
        }

        // Right hand holds giant sword
        var sword = new MeshInstance3D();
        var swordMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 1.8f * scale, 0.02f * scale) };
        sword.Mesh = swordMesh;
        sword.MaterialOverride = armorMat;
        sword.Position = new Vector3(0.6f * scale, 0.9f * scale, 0.2f * scale);
        sword.RotationDegrees = new Vector3(-45, 0, 15);
        _meshInstance.AddChild(sword);

        // Sword hilt
        var hilt = new MeshInstance3D();
        var hiltMesh = new CylinderMesh { TopRadius = 0.025f * scale, BottomRadius = 0.025f * scale, Height = 0.2f * scale };
        hilt.Mesh = hiltMesh;
        hilt.MaterialOverride = crownMat;
        hilt.Position = new Vector3(0.55f * scale, 0.15f * scale, 0.1f * scale);
        hilt.RotationDegrees = new Vector3(-45, 0, 15);
        _meshInstance.AddChild(hilt);

        // Legs
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;

            // Femur
            var femur = new MeshInstance3D();
            var femurMesh = new CapsuleMesh { Radius = 0.06f * scale, Height = 0.45f * scale };
            femur.Mesh = femurMesh;
            femur.MaterialOverride = boneMat;
            femur.Position = new Vector3(0.12f * scale * sideMultiplier, 0.45f * scale, 0);
            _meshInstance.AddChild(femur);

            // Tibia
            var tibia = new MeshInstance3D();
            var tibiaMesh = new CapsuleMesh { Radius = 0.05f * scale, Height = 0.4f * scale };
            tibia.Mesh = tibiaMesh;
            tibia.MaterialOverride = boneMat;
            tibia.Position = new Vector3(0.12f * scale * sideMultiplier, 0.12f * scale, 0);
            _meshInstance.AddChild(tibia);

            // Foot bones
            var foot = new MeshInstance3D();
            var footMesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.05f * scale, 0.2f * scale) };
            foot.Mesh = footMesh;
            foot.MaterialOverride = boneMat;
            foot.Position = new Vector3(0.12f * scale * sideMultiplier, 0.02f * scale, 0.05f * scale);
            _meshInstance.AddChild(foot);
        }

        // Tattered cape (flowing behind)
        var cape = new MeshInstance3D();
        var capeMesh = new BoxMesh { Size = new Vector3(0.6f * scale, 1.2f * scale, 0.05f * scale) };
        cape.Mesh = capeMesh;
        var capeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.1f, 0.12f, 0.8f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.95f
        };
        cape.MaterialOverride = capeMat;
        cape.Position = new Vector3(0, 1.3f * scale, -0.25f * scale);
        _meshInstance.AddChild(cape);

        GD.Print("[BossEnemy3D] Created Skeleton Lord mesh with armor, crown, and sword");
    }

    private void CreateSpiderQueenMesh()
    {
        float scale = 1.5f;

        // Materials
        var bodyMat = new StandardMaterial3D
        {
            AlbedoColor = _baseColor,
            Roughness = 0.6f,
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.1f, 0.35f),
            EmissionEnergyMultiplier = 0.4f
        };

        var legMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.1f, 0.15f),
            Roughness = 0.5f
        };

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.1f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.2f),
            EmissionEnergyMultiplier = 2.5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        var fangMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.9f, 0.85f),
            Metallic = 0.3f,
            Roughness = 0.4f
        };

        var markingMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.1f, 0.15f),
            Roughness = 0.7f
        };

        // Cephalothorax (front body segment)
        var cephalothorax = new MeshInstance3D();
        var cephMesh = new SphereMesh { Radius = 0.45f * scale, Height = 0.6f * scale };
        cephalothorax.Mesh = cephMesh;
        cephalothorax.MaterialOverride = bodyMat;
        cephalothorax.Position = new Vector3(0, 0.6f * scale, 0.2f * scale);
        cephalothorax.Scale = new Vector3(1f, 0.7f, 1.2f);
        _meshInstance!.AddChild(cephalothorax);

        // Abdomen (large, bulbous with markings)
        var abdomen = new MeshInstance3D();
        var abdMesh = new SphereMesh { Radius = 0.7f * scale, Height = 1.0f * scale };
        abdomen.Mesh = abdMesh;
        abdomen.MaterialOverride = bodyMat;
        abdomen.Position = new Vector3(0, 0.65f * scale, -0.7f * scale);
        abdomen.Scale = new Vector3(1f, 0.85f, 1.3f);
        _meshInstance.AddChild(abdomen);

        // Red hourglass marking on abdomen
        var marking = new MeshInstance3D();
        var markMesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.3f * scale };
        marking.Mesh = markMesh;
        marking.MaterialOverride = markingMat;
        marking.Position = new Vector3(0, 0.25f * scale, -0.65f * scale);
        marking.Scale = new Vector3(0.6f, 0.3f, 0.6f);
        _meshInstance.AddChild(marking);

        // Multiple eyes (8 eyes arranged in rows)
        float[] eyeXPositions = { -0.15f, -0.08f, 0.08f, 0.15f, -0.1f, 0.1f, -0.05f, 0.05f };
        float[] eyeYPositions = { 0.68f, 0.7f, 0.7f, 0.68f, 0.62f, 0.62f, 0.75f, 0.75f };
        float[] eyeSizes = { 0.06f, 0.08f, 0.08f, 0.06f, 0.05f, 0.05f, 0.04f, 0.04f };

        for (int i = 0; i < 8; i++)
        {
            var eye = new MeshInstance3D();
            var eyeMesh = new SphereMesh { Radius = eyeSizes[i] * scale, Height = eyeSizes[i] * 1.5f * scale };
            eye.Mesh = eyeMesh;
            eye.MaterialOverride = eyeMat;
            eye.Position = new Vector3(eyeXPositions[i] * scale, eyeYPositions[i] * scale, 0.55f * scale);
            _meshInstance.AddChild(eye);
        }

        // Fangs (chelicerae)
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;

            // Fang base
            var fangBase = new MeshInstance3D();
            var baseMesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.08f * scale, Height = 0.15f * scale };
            fangBase.Mesh = baseMesh;
            fangBase.MaterialOverride = bodyMat;
            fangBase.Position = new Vector3(0.1f * scale * sideMultiplier, 0.5f * scale, 0.5f * scale);
            fangBase.RotationDegrees = new Vector3(45, 0, 20 * sideMultiplier);
            _meshInstance.AddChild(fangBase);

            // Fang (curved, venomous)
            var fang = new MeshInstance3D();
            var fangMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.04f * scale, Height = 0.25f * scale };
            fang.Mesh = fangMesh;
            fang.MaterialOverride = fangMat;
            fang.Position = new Vector3(0.12f * scale * sideMultiplier, 0.35f * scale, 0.6f * scale);
            fang.RotationDegrees = new Vector3(70, 0, 15 * sideMultiplier);
            _meshInstance.AddChild(fang);
        }

        // Pedipalps (small front appendages)
        for (int side = 0; side < 2; side++)
        {
            float sideMultiplier = side == 0 ? -1f : 1f;
            var pedipalp = new MeshInstance3D();
            var palp = new CapsuleMesh { Radius = 0.04f * scale, Height = 0.2f * scale };
            pedipalp.Mesh = palp;
            pedipalp.MaterialOverride = legMat;
            pedipalp.Position = new Vector3(0.2f * scale * sideMultiplier, 0.55f * scale, 0.45f * scale);
            pedipalp.RotationDegrees = new Vector3(30, 30 * sideMultiplier, 0);
            _meshInstance.AddChild(pedipalp);
        }

        // 8 Legs (4 pairs)
        float[] legAngles = { 30, 60, 110, 150 };
        float[] legLengths = { 1.4f, 1.6f, 1.5f, 1.3f };

        for (int leg = 0; leg < 8; leg++)
        {
            int pair = leg / 2;
            float sideMultiplier = (leg % 2 == 0) ? -1f : 1f;
            float angle = legAngles[pair] * sideMultiplier;
            float length = legLengths[pair];

            // Coxa (hip joint)
            var coxa = new MeshInstance3D();
            var coxaMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.1f * scale };
            coxa.Mesh = coxaMesh;
            coxa.MaterialOverride = bodyMat;
            float coxaX = Mathf.Sin(Mathf.DegToRad(angle)) * 0.4f * scale;
            float coxaZ = Mathf.Cos(Mathf.DegToRad(angle)) * 0.35f * scale;
            coxa.Position = new Vector3(coxaX, 0.55f * scale, coxaZ);
            _meshInstance.AddChild(coxa);

            // Femur (upper leg)
            var femur = new MeshInstance3D();
            var femurMesh = new CapsuleMesh { Radius = 0.05f * scale, Height = 0.5f * length * scale };
            femur.Mesh = femurMesh;
            femur.MaterialOverride = legMat;
            float femurX = coxaX + Mathf.Sin(Mathf.DegToRad(angle)) * 0.3f * scale;
            float femurZ = coxaZ + Mathf.Cos(Mathf.DegToRad(angle)) * 0.25f * scale;
            femur.Position = new Vector3(femurX, 0.75f * scale, femurZ);
            femur.RotationDegrees = new Vector3(0, -angle, 60 * sideMultiplier);
            _meshInstance.AddChild(femur);

            // Tibia (lower leg)
            var tibia = new MeshInstance3D();
            var tibiaMesh = new CapsuleMesh { Radius = 0.04f * scale, Height = 0.55f * length * scale };
            tibia.Mesh = tibiaMesh;
            tibia.MaterialOverride = legMat;
            float tibiaX = femurX + Mathf.Sin(Mathf.DegToRad(angle)) * 0.4f * scale;
            float tibiaZ = femurZ + Mathf.Cos(Mathf.DegToRad(angle)) * 0.35f * scale;
            tibia.Position = new Vector3(tibiaX, 0.35f * scale, tibiaZ);
            tibia.RotationDegrees = new Vector3(0, -angle, -30 * sideMultiplier);
            _meshInstance.AddChild(tibia);

            // Tarsus (foot)
            var tarsus = new MeshInstance3D();
            var tarsusMesh = new CapsuleMesh { Radius = 0.025f * scale, Height = 0.2f * length * scale };
            tarsus.Mesh = tarsusMesh;
            tarsus.MaterialOverride = legMat;
            float tarsusX = tibiaX + Mathf.Sin(Mathf.DegToRad(angle)) * 0.25f * scale;
            float tarsusZ = tibiaZ + Mathf.Cos(Mathf.DegToRad(angle)) * 0.2f * scale;
            tarsus.Position = new Vector3(tarsusX, 0.08f * scale, tarsusZ);
            tarsus.RotationDegrees = new Vector3(0, -angle, 10);
            _meshInstance.AddChild(tarsus);
        }

        // Spinnerets (at back of abdomen)
        for (int i = 0; i < 3; i++)
        {
            var spinneret = new MeshInstance3D();
            var spinMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.04f * scale, Height = 0.12f * scale };
            spinneret.Mesh = spinMesh;
            spinneret.MaterialOverride = bodyMat;
            float spinAngle = -20 + i * 20;
            spinneret.Position = new Vector3(
                Mathf.Sin(Mathf.DegToRad(spinAngle)) * 0.15f * scale,
                0.35f * scale,
                -1.3f * scale
            );
            spinneret.RotationDegrees = new Vector3(45, spinAngle, 0);
            _meshInstance.AddChild(spinneret);
        }

        // Crown-like spikes on cephalothorax
        for (int i = 0; i < 4; i++)
        {
            var spike = new MeshInstance3D();
            var spikeMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.03f * scale, Height = 0.15f * scale };
            spike.Mesh = spikeMesh;
            spike.MaterialOverride = bodyMat;
            float spikeAngle = -45 + i * 30;
            spike.Position = new Vector3(
                Mathf.Sin(Mathf.DegToRad(spikeAngle)) * 0.25f * scale,
                0.85f * scale,
                0.15f * scale
            );
            spike.RotationDegrees = new Vector3(-30, spikeAngle, 0);
            _meshInstance.AddChild(spike);
        }

        GD.Print("[BossEnemy3D] Created Spider Queen mesh with 8 eyes, 8 legs, and venomous fangs");
    }

    /// <summary>
    /// Static factory for creating bosses
    /// </summary>
    public static BossEnemy3D Create(string monsterType, Vector3 position)
    {
        var boss = new BossEnemy3D();
        boss.MonsterType = monsterType;
        boss.MonsterId = $"{monsterType.ToLower()}_boss";
        boss.Position = position;
        return boss;
    }
}
