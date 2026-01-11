using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Core;
using SafeRoom3D.Enemies;
using SafeRoom3D.Items;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC.AI;

/// <summary>
/// AI state machine states for Crawler NPCs.
/// </summary>
public enum AIState
{
    Idle,               // Standing around, evaluating options
    Patrolling,         // Moving randomly in area
    Investigating,      // Moving toward a target (corpse or enemy)
    Combat,             // Actively fighting an enemy
    Fleeing,            // Running away from danger
    Looting,            // Picking up items from a corpse
    ReturningToSafeZone,// Heading back to Bopca
    AtSafeZone,         // Healing and selling at Bopca
    Dead                // Permanent death
}

/// <summary>
/// Available actions for utility scoring.
/// </summary>
public enum AIAction
{
    Attack,
    Loot,
    ReturnToSafeZone,
    Flee,
    Patrol,
    Idle
}

/// <summary>
/// Base class for all Crawler NPCs with AI behavior.
/// Implements Utility AI for decision-making and state machine for behavior.
/// </summary>
public abstract partial class CrawlerAIBase : CharacterBody3D
{
    // === Abstract Properties (must be implemented by subclasses) ===
    public abstract string CrawlerName { get; }
    public abstract CrawlerPersonality Personality { get; }

    // === Stats ===
    public int MaxHealth => Personality.MaxHealth;
    public int CurrentHealth { get; protected set; }
    public float Damage => Personality.BaseDamage;
    public float AttackRange => Personality.AttackRange;
    public float AttackCooldown => Personality.AttackCooldown;
    public float MoveSpeed => Personality.MoveSpeed;

    // === State Machine ===
    public AIState CurrentState { get; protected set; } = AIState.Idle;
    private AIState _previousState = AIState.Idle;

    // === Components ===
    protected NavigationAgent3D? _navAgent;
    protected CrawlerInventory _inventory = null!;
    protected CrawlerAILogger _logger = null!;
    protected CrawlerUtilityScorer _utilityScorer = null!;

    /// <summary>Public accessor for inventory (for utility scorer).</summary>
    public CrawlerInventory Inventory => _inventory;

    // === Visual Components ===
    protected Node3D? _meshRoot;
    protected Label3D? _nameplate;
    protected Label3D? _healthLabel;
    protected Label3D? _promptLabel;
    protected MeshInstance3D? _meshInstance;
    protected MonsterMeshFactory.LimbNodes? _limbs;

    // === Targets ===
    protected BasicEnemy3D? _currentEnemy;
    protected Corpse3D? _currentCorpse;
    protected Vector3 _patrolTarget;
    protected Vector3 _spawnPosition;

    // === Timers ===
    private float _decisionTimer;
    private float _attackCooldownTimer;
    private float _stateTimer;
    private float _healTimer;
    private float _idleTimer;
    private float _quipCooldown;
    private float _debugTimer;
    private float _stuckTimer;
    private Vector3 _lastPatrolPosition;

    // === Constants ===
    private const float PatrolRadius = 40f;  // Larger patrol area for exploration
    private const float HealTickInterval = 1.0f;
    private const int HealPerTick = 10;
    private const float QuipCooldown = 15f;
    private const float ExploreGridSize = 4f;  // Size of exploration grid cells

    // === Exploration (Fog of War) ===
    private HashSet<Vector2I> _visitedCells = new();
    private float _markVisitedTimer;

    // === Collision ===
    private CollisionShape3D? _collisionShape;

    public override void _Ready()
    {
        // Initialize components
        _inventory = new CrawlerInventory(CrawlerName);
        _logger = new CrawlerAILogger(CrawlerName);
        _utilityScorer = new CrawlerUtilityScorer(this);

        // Store spawn position
        _spawnPosition = GlobalPosition;

        // Initialize health
        CurrentHealth = MaxHealth;

        // Setup navigation
        SetupNavigation();

        // Create mesh root
        _meshRoot = new Node3D();
        _meshRoot.Name = "MeshRoot";
        AddChild(_meshRoot);

        // Create mesh and visuals
        CreateMesh();
        CreateCollisionBody();
        CreateNameplate();
        CreateHealthLabel();
        CreatePromptLabel();

        // Add to groups
        AddToGroup("NPCs");
        AddToGroup("Crawlers");
        AddToGroup("CrawlerAI");

        // Load AI config
        CrawlerAIConfig.Load();

        // Initialize with a patrol target so we start moving
        _lastPatrolPosition = GlobalPosition;
        PickNewPatrolTarget();
        CurrentState = AIState.Patrolling;

        GD.Print($"[{CrawlerName}] AI Ready (HP: {CurrentHealth}/{MaxHealth}, starting patrol)");
    }

    private void SetupNavigation()
    {
        _navAgent = new NavigationAgent3D();
        _navAgent.PathDesiredDistance = 0.5f;
        _navAgent.TargetDesiredDistance = 1.5f;
        _navAgent.AvoidanceEnabled = false;  // Performance
        _navAgent.Radius = 0.5f;
        _navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.Corridorfunnel;
        AddChild(_navAgent);
    }

    /// <summary>
    /// Create the procedural mesh for this crawler.
    /// Must be implemented by subclasses.
    /// </summary>
    protected abstract void CreateMesh();

    private void CreateNameplate()
    {
        _nameplate = new Label3D();
        _nameplate.Text = $"{CrawlerName} \"{Personality.Title}\"";
        _nameplate.FontSize = 32;
        _nameplate.Modulate = new Color(0.3f, 1.0f, 0.3f);  // Green for friendly
        _nameplate.Position = new Vector3(0, GetNameplateHeight(), 0);
        _nameplate.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _nameplate.NoDepthTest = true;
        AddChild(_nameplate);
    }

    private void CreateHealthLabel()
    {
        _healthLabel = new Label3D();
        UpdateHealthLabel();
        _healthLabel.FontSize = 24;
        _healthLabel.Position = new Vector3(0, GetNameplateHeight() - 0.25f, 0);
        _healthLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _healthLabel.NoDepthTest = true;
        AddChild(_healthLabel);
    }

    private void UpdateHealthLabel()
    {
        if (_healthLabel == null) return;

        float hpPercent = (float)CurrentHealth / MaxHealth;
        _healthLabel.Text = $"HP: {CurrentHealth}/{MaxHealth}";

        // Color based on HP percentage
        _healthLabel.Modulate = hpPercent switch
        {
            > 0.6f => new Color(0.3f, 1.0f, 0.3f),   // Green
            > 0.3f => new Color(1.0f, 1.0f, 0.3f),   // Yellow
            _ => new Color(1.0f, 0.3f, 0.3f)          // Red
        };
    }

    protected virtual float GetNameplateHeight() => 2.0f;
    protected virtual float GetInteractionRange() => 4.0f;

    private void CreateCollisionBody()
    {
        // CharacterBody3D needs its own collision shape for floor detection and physics
        _collisionShape = new CollisionShape3D();
        _collisionShape.Name = "CollisionShape";
        var capsule = new CapsuleShape3D();
        capsule.Radius = 0.4f;
        capsule.Height = 1.6f;
        _collisionShape.Shape = capsule;
        _collisionShape.Position = new Vector3(0, 0.8f, 0);  // Center the capsule
        AddChild(_collisionShape);

        // Set collision layers for this CharacterBody3D
        // Layers are 1-indexed but bitmask is 0-indexed: Layer N = bit (N-1) = 1 << (N-1)
        CollisionLayer = 1 << 1;  // Layer 2 (Enemy/NPC) = bit 1 = 2
        CollisionMask = (1 << 7) | (1 << 4);  // Wall (layer 8) + Obstacle (layer 5) = 128 + 16 = 144
    }

    private void CreatePromptLabel()
    {
        _promptLabel = new Label3D();
        _promptLabel.Text = "Press T to Talk";
        _promptLabel.FontSize = 24;
        _promptLabel.Modulate = new Color(1f, 1f, 0.6f);  // Yellow
        _promptLabel.Position = new Vector3(0, GetNameplateHeight() - 0.5f, 0);
        _promptLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _promptLabel.NoDepthTest = true;
        _promptLabel.Visible = false;
        AddChild(_promptLabel);
    }

    private void UpdatePromptVisibility()
    {
        if (_promptLabel == null) return;

        var player = SafeRoom3D.Player.FPSController.Instance;
        if (player == null)
        {
            _promptLabel.Visible = false;
            return;
        }

        float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
        _promptLabel.Visible = distance <= GetInteractionRange();
    }

    /// <summary>
    /// Called when player interacts with this Crawler (T key).
    /// Opens dialogue UI.
    /// </summary>
    public virtual void Interact(Node3D player)
    {
        GD.Print($"[{CrawlerName}] Player interacting");
        CrawlerDialogueUI3D.Instance?.Open(this, GetDialogueKey());
    }

    /// <summary>
    /// Get the dialogue database key for this crawler.
    /// Override in subclasses if needed.
    /// </summary>
    protected virtual string GetDialogueKey() => CrawlerName.ToLower().Replace(" ", "_");

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Update logger cooldowns
        _logger.Update(dt);

        // Check if AI is disabled
        if (!CrawlerAIConfig.AIEnabled)
        {
            // Just idle animation, no AI processing
            AnimateIdle(dt);
            return;
        }

        // Dead crawlers don't process
        if (CurrentState == AIState.Dead) return;

        // Update timers
        _decisionTimer -= dt;
        _attackCooldownTimer -= dt;
        _stateTimer += dt;
        _quipCooldown -= dt;
        _debugTimer -= dt;

        // Make decisions at regular intervals
        if (_decisionTimer <= 0 && CurrentState != AIState.Dead)
        {
            EvaluateAndDecide();
            _decisionTimer = CrawlerAIConfig.DecisionInterval;
        }

        // Process current state
        ProcessCurrentState(dt);

        // Apply gravity
        if (!IsOnFloor())
        {
            Velocity += new Vector3(0, -9.8f * dt, 0);
        }

        MoveAndSlide();

        // Update health display
        UpdateHealthLabel();

        // Animate based on state
        AnimateByState(dt);

        // Update interaction prompt visibility
        UpdatePromptVisibility();
    }

    private void EvaluateAndDecide()
    {
        // Don't re-evaluate if in the middle of important actions
        if (CurrentState == AIState.Combat && _currentEnemy != null && IsInstanceValid(_currentEnemy))
            return;
        if (CurrentState == AIState.Looting && _currentCorpse != null)
            return;
        if (CurrentState == AIState.AtSafeZone)
            return;

        // Get best action from utility scorer
        var (action, scores) = _utilityScorer.EvaluateBestAction();

        // Log the decision
        _logger.LogDecision(action.ToString(), scores);

        // Execute the action
        ExecuteAction(action);
    }

    private void ExecuteAction(AIAction action)
    {
        switch (action)
        {
            case AIAction.Attack:
                var enemy = FindNearestEnemy();
                if (enemy != null)
                {
                    _currentEnemy = enemy;
                    ChangeState(AIState.Combat);
                }
                break;

            case AIAction.Loot:
                var corpse = FindNearestCorpse();
                if (corpse != null)
                {
                    _currentCorpse = corpse;
                    ChangeState(AIState.Investigating);
                }
                break;

            case AIAction.ReturnToSafeZone:
                ChangeState(AIState.ReturningToSafeZone);
                break;

            case AIAction.Flee:
                ChangeState(AIState.Fleeing);
                break;

            case AIAction.Patrol:
                // Only pick new target if not already patrolling
                if (CurrentState != AIState.Patrolling)
                {
                    PickNewPatrolTarget();
                    ChangeState(AIState.Patrolling);
                }
                break;

            case AIAction.Idle:
            default:
                ChangeState(AIState.Idle);
                break;
        }
    }

    protected void ChangeState(AIState newState)
    {
        if (CurrentState == newState) return;

        _previousState = CurrentState;
        var oldState = CurrentState;
        CurrentState = newState;
        _stateTimer = 0;

        _logger.LogStateChange(oldState, newState, GetStateChangeReason(newState));

        // State entry logic
        switch (newState)
        {
            case AIState.Fleeing:
                _logger.LogFlee("HP too low", CurrentHealth, MaxHealth);
                ShowQuip(QuipCategory.Fleeing);
                break;

            case AIState.Combat:
                ShowQuip(QuipCategory.Combat);
                break;

            case AIState.ReturningToSafeZone:
                string reason = CurrentHealth < MaxHealth * Personality.ReturnHealthPercent
                    ? "low HP"
                    : "inventory full";
                _logger.LogReturning(reason);
                break;
        }
    }

    private string GetStateChangeReason(AIState newState)
    {
        return newState switch
        {
            AIState.Combat => _currentEnemy != null ? $"engaging {_currentEnemy.MonsterType}" : "enemy detected",
            AIState.Fleeing => $"HP at {CurrentHealth}/{MaxHealth}",
            AIState.Looting => "corpse found",
            AIState.ReturningToSafeZone => $"inv {_inventory.GetFullnessPercent():P0} full, HP {CurrentHealth}/{MaxHealth}",
            AIState.AtSafeZone => "reached Bopca",
            AIState.Patrolling => "exploring",
            AIState.Idle => "waiting",
            _ => ""
        };
    }

    private void ProcessCurrentState(float dt)
    {
        switch (CurrentState)
        {
            case AIState.Idle:
                ProcessIdle(dt);
                break;
            case AIState.Patrolling:
                ProcessPatrolling(dt);
                break;
            case AIState.Investigating:
                ProcessInvestigating(dt);
                break;
            case AIState.Combat:
                ProcessCombat(dt);
                break;
            case AIState.Fleeing:
                ProcessFleeing(dt);
                break;
            case AIState.Looting:
                ProcessLooting(dt);
                break;
            case AIState.ReturningToSafeZone:
                ProcessReturningToSafeZone(dt);
                break;
            case AIState.AtSafeZone:
                ProcessAtSafeZone(dt);
                break;
        }
    }

    private void ProcessIdle(float dt)
    {
        _idleTimer += dt;
        Velocity = new Vector3(0, Velocity.Y, 0);

        // After idling for a bit, start patrolling
        if (_idleTimer > 3f)
        {
            _idleTimer = 0;
            PickNewPatrolTarget();
            ChangeState(AIState.Patrolling);
        }
    }

    private void ProcessPatrolling(float dt)
    {
        // Mark current cell as visited periodically
        _markVisitedTimer -= dt;
        if (_markVisitedTimer <= 0)
        {
            _markVisitedTimer = 0.5f;  // Check twice per second
            MarkCurrentCellVisited();
        }

        _patrolLogTimer -= dt;
        if (_patrolLogTimer <= 0)
        {
            _patrolLogTimer = 2f;
            GD.Print($"[{CrawlerName}] ProcessPatrolling: pos={GlobalPosition}, target={_patrolTarget}, vel={Velocity.Length():F1}, explored={_visitedCells.Count} cells");
        }

        NavigateToPosition(_patrolTarget);

        // Check if reached patrol target
        float distToTarget = GlobalPosition.DistanceTo(_patrolTarget);
        if (distToTarget < 2f)
        {
            GD.Print($"[{CrawlerName}] Reached patrol target, picking new one");
            PickNewPatrolTarget();
            return;
        }

        // Stuck detection: if we haven't moved much in 2 seconds, pick a new target
        _stuckTimer += dt;
        if (_stuckTimer >= 2f)
        {
            float movedDistance = GlobalPosition.DistanceTo(_lastPatrolPosition);
            if (movedDistance < 0.5f)
            {
                GD.Print($"[{CrawlerName}] Stuck detected (moved only {movedDistance:F2}m), picking new target");
                PickNewPatrolTarget();
            }
            else
            {
                // Not stuck, reset tracking
                _stuckTimer = 0;
                _lastPatrolPosition = GlobalPosition;
            }
        }
    }
    private float _patrolLogTimer = 0f;

    private void ProcessInvestigating(float dt)
    {
        if (_currentCorpse == null || !IsInstanceValid(_currentCorpse))
        {
            ChangeState(AIState.Idle);
            return;
        }

        NavigateToPosition(_currentCorpse.GlobalPosition);

        // Check if reached corpse
        if (GlobalPosition.DistanceTo(_currentCorpse.GlobalPosition) < 2f)
        {
            ChangeState(AIState.Looting);
        }
    }

    private void ProcessCombat(float dt)
    {
        // Validate enemy
        if (_currentEnemy == null || !IsInstanceValid(_currentEnemy) || _currentEnemy.CurrentState == BasicEnemy3D.State.Dead)
        {
            // Enemy dead or invalid - look for loot
            ShowQuip(QuipCategory.Victory);
            var nearbyCorpse = FindNearestCorpse();
            if (nearbyCorpse != null)
            {
                _currentCorpse = nearbyCorpse;
                ChangeState(AIState.Looting);
            }
            else
            {
                ChangeState(AIState.Idle);
            }
            _currentEnemy = null;
            return;
        }

        float distToEnemy = GlobalPosition.DistanceTo(_currentEnemy.GlobalPosition);

        // Move toward enemy if not in range
        if (distToEnemy > AttackRange)
        {
            NavigateToPosition(_currentEnemy.GlobalPosition);
        }
        else
        {
            // In range - attack if cooldown is ready
            Velocity = new Vector3(0, Velocity.Y, 0);
            FaceTarget(_currentEnemy.GlobalPosition);

            if (_attackCooldownTimer <= 0)
            {
                PerformAttack();
            }
        }

        // Check flee condition (personality-dependent)
        if (!Personality.NeverFlees && CurrentHealth <= MaxHealth * Personality.FleeHealthPercent)
        {
            ChangeState(AIState.Fleeing);
        }
    }

    private void ProcessFleeing(float dt)
    {
        // Flee toward safe zone (Bopca)
        var safeZonePos = GetSafeZonePosition();
        NavigateToPosition(safeZonePos);

        // If we've put some distance, switch to returning
        if (GlobalPosition.DistanceTo(safeZonePos) < 5f)
        {
            ChangeState(AIState.AtSafeZone);
        }
        else if (_stateTimer > 5f)
        {
            // Been fleeing for a while, switch to returning properly
            ChangeState(AIState.ReturningToSafeZone);
        }
    }

    private void ProcessLooting(float dt)
    {
        if (_currentCorpse == null || !IsInstanceValid(_currentCorpse))
        {
            ShowQuip(QuipCategory.Looting);
            ChangeState(AIState.Idle);
            return;
        }

        // Try to loot items
        if (_currentCorpse.Loot != null)
        {
            // Take gold first
            if (_currentCorpse.Loot.GoldAmount > 0)
            {
                _inventory.AddGold(_currentCorpse.Loot.GoldAmount);
                _currentCorpse.Loot.GoldAmount = 0;  // Clear the gold
            }

            // Take items
            var items = _currentCorpse.Loot.GetAllItems().ToList();
            foreach (var (slot, item) in items)
            {
                int value = CrawlerInventory.EstimateItemValue(item);
                if (value >= Personality.MinItemValueToLoot && _inventory.HasSpace())
                {
                    if (_inventory.AddItem(item))
                    {
                        _currentCorpse.Loot.TakeItem(slot);
                        _logger.LogLoot(item.Name, value);
                    }
                }
            }
        }

        // Done looting
        _currentCorpse = null;
        ChangeState(AIState.Idle);
    }

    private void ProcessReturningToSafeZone(float dt)
    {
        var safeZonePos = GetSafeZonePosition();
        NavigateToPosition(safeZonePos);

        if (GlobalPosition.DistanceTo(safeZonePos) < 3f)
        {
            ChangeState(AIState.AtSafeZone);
        }
    }

    private void ProcessAtSafeZone(float dt)
    {
        Velocity = new Vector3(0, Velocity.Y, 0);

        // Heal over time
        _healTimer -= dt;
        if (_healTimer <= 0 && CurrentHealth < MaxHealth)
        {
            CurrentHealth = Math.Min(CurrentHealth + HealPerTick, MaxHealth);
            _healTimer = HealTickInterval;
            _logger.LogHealing(HealPerTick, CurrentHealth, MaxHealth);
        }

        // Sell items if we have any
        if (_inventory.HasItemsToSell())
        {
            int itemCount = _inventory.GetOccupiedSlotCount();
            int goldEarned = _inventory.SellAllItems();
            _logger.LogSale(itemCount, goldEarned);
        }

        // Check if done - healed and sold
        if (CurrentHealth >= MaxHealth && !_inventory.HasItemsToSell())
        {
            ChangeState(AIState.Idle);
        }
    }

    // === Navigation ===

    private void NavigateToPosition(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - GlobalPosition;
        toTarget.Y = 0;
        float distanceToTarget = toTarget.Length();

        // If we're close enough, stop
        if (distanceToTarget < 1.5f)
        {
            Velocity = new Vector3(0, Velocity.Y, 0);
            return;
        }

        Vector3 desiredDirection = toTarget.Normalized();
        float speed = CurrentState == AIState.Fleeing ? MoveSpeed * 1.3f : MoveSpeed;

        // Obstacle avoidance using raycasts
        Vector3 moveDirection = AvoidObstacles(desiredDirection);

        // Set horizontal velocity
        float vY = Velocity.Y;
        Velocity = new Vector3(moveDirection.X * speed, vY, moveDirection.Z * speed);

        // Face movement direction
        FaceTarget(GlobalPosition + moveDirection);
    }

    /// <summary>
    /// Check for obstacles ahead and steer around them.
    /// </summary>
    private Vector3 AvoidObstacles(Vector3 desiredDirection)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        Vector3 origin = GlobalPosition + Vector3.Up * 0.5f;  // Waist height
        float rayLength = 1.5f;  // Look ahead distance

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
            // Both sides clear, pick one (alternate based on position for variety)
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
            // Completely stuck - move backward slightly
            return -desiredDirection * 0.3f;
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
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };  // Exclude self
        var result = spaceState.IntersectRay(query);
        return result.Count > 0;
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - GlobalPosition).Normalized();
        if (direction.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(direction.X, direction.Z);
            Rotation = new Vector3(0, targetAngle, 0);
        }
    }

    /// <summary>
    /// Convert world position to exploration grid cell.
    /// </summary>
    private Vector2I WorldToCell(Vector3 pos)
    {
        return new Vector2I(
            Mathf.FloorToInt(pos.X / ExploreGridSize),
            Mathf.FloorToInt(pos.Z / ExploreGridSize)
        );
    }

    /// <summary>
    /// Convert grid cell to world position (center of cell).
    /// </summary>
    private Vector3 CellToWorld(Vector2I cell)
    {
        return new Vector3(
            (cell.X + 0.5f) * ExploreGridSize,
            GlobalPosition.Y,
            (cell.Y + 0.5f) * ExploreGridSize
        );
    }

    /// <summary>
    /// Mark current position as visited.
    /// </summary>
    private void MarkCurrentCellVisited()
    {
        var cell = WorldToCell(GlobalPosition);
        if (_visitedCells.Add(cell))
        {
            GD.Print($"[{CrawlerName}] Explored cell {cell} (total: {_visitedCells.Count})");
        }
    }

    private void PickNewPatrolTarget()
    {
        var spaceState = GetWorld3D().DirectSpaceState;

        // First, try to find an unexplored cell within patrol radius
        Vector3? unexploredTarget = FindNearestUnexploredCell(spaceState);
        if (unexploredTarget.HasValue)
        {
            _patrolTarget = unexploredTarget.Value;
            _stuckTimer = 0;
            _lastPatrolPosition = GlobalPosition;
            GD.Print($"[{CrawlerName}] Exploring unexplored area: {_patrolTarget}");
            return;
        }

        // All nearby cells explored - random patrol within radius
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = GD.Randf() * Mathf.Tau;
            float dist = 8f + GD.Randf() * (PatrolRadius - 8f);

            Vector3 candidatePos = GlobalPosition + new Vector3(
                Mathf.Cos(angle) * dist,
                0,
                Mathf.Sin(angle) * dist
            );

            if (TryValidatePatrolPosition(spaceState, ref candidatePos))
            {
                _patrolTarget = candidatePos;
                _stuckTimer = 0;
                _lastPatrolPosition = GlobalPosition;
                GD.Print($"[{CrawlerName}] Random patrol (all explored): {_patrolTarget}");
                return;
            }
        }

        // Fallback: small movement in a random direction
        float fallbackAngle = GD.Randf() * Mathf.Tau;
        _patrolTarget = GlobalPosition + new Vector3(
            Mathf.Cos(fallbackAngle) * 4f,
            0,
            Mathf.Sin(fallbackAngle) * 4f
        );
        _stuckTimer = 0;
        _lastPatrolPosition = GlobalPosition;
        GD.Print($"[{CrawlerName}] Fallback patrol target: {_patrolTarget}");
    }

    /// <summary>
    /// Find the nearest unexplored cell that is reachable.
    /// </summary>
    private Vector3? FindNearestUnexploredCell(PhysicsDirectSpaceState3D spaceState)
    {
        Vector2I currentCell = WorldToCell(GlobalPosition);
        int searchRadius = Mathf.CeilToInt(PatrolRadius / ExploreGridSize);

        // Collect unexplored cells, sorted by distance
        var unexploredCells = new List<(Vector2I cell, float dist)>();

        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dz = -searchRadius; dz <= searchRadius; dz++)
            {
                var cell = new Vector2I(currentCell.X + dx, currentCell.Y + dz);

                // Skip if already visited
                if (_visitedCells.Contains(cell))
                    continue;

                Vector3 cellWorldPos = CellToWorld(cell);
                float dist = GlobalPosition.DistanceTo(cellWorldPos);

                // Skip if too close or too far
                if (dist < 4f || dist > PatrolRadius)
                    continue;

                unexploredCells.Add((cell, dist));
            }
        }

        // Sort by distance (prefer closer unexplored cells)
        unexploredCells.Sort((a, b) => a.dist.CompareTo(b.dist));

        // Try each unexplored cell until we find a reachable one
        foreach (var (cell, dist) in unexploredCells)
        {
            Vector3 candidatePos = CellToWorld(cell);

            if (TryValidatePatrolPosition(spaceState, ref candidatePos))
            {
                return candidatePos;
            }
        }

        return null;
    }

    /// <summary>
    /// Validate a patrol position - check for walls and valid floor.
    /// May adjust candidatePos to stop before a wall.
    /// </summary>
    private bool TryValidatePatrolPosition(PhysicsDirectSpaceState3D spaceState, ref Vector3 candidatePos)
    {
        // Check for walls between us and the candidate
        var wallQuery = PhysicsRayQueryParameters3D.Create(
            GlobalPosition + Vector3.Up * 0.8f,
            candidatePos + Vector3.Up * 0.8f,
            1 << 7  // Wall layer
        );
        var wallResult = spaceState.IntersectRay(wallQuery);

        if (wallResult.Count > 0)
        {
            // Hit a wall - use position just before the wall
            Vector3 hitPos = (Vector3)wallResult["position"];
            float hitDist = GlobalPosition.DistanceTo(hitPos);

            if (hitDist > 3f)
            {
                Vector3 toHit = (hitPos - GlobalPosition).Normalized();
                candidatePos = GlobalPosition + toHit * (hitDist - 1f);
            }
            else
            {
                return false;  // Too close to wall
            }
        }

        // Check for valid floor
        var floorQuery = PhysicsRayQueryParameters3D.Create(
            candidatePos + Vector3.Up * 2f,
            candidatePos + Vector3.Down * 5f,
            (1 << 7) | (1 << 4)  // Wall + Obstacle
        );
        var floorResult = spaceState.IntersectRay(floorQuery);

        if (floorResult.Count > 0)
        {
            Vector3 floorPos = (Vector3)floorResult["position"];
            candidatePos = new Vector3(candidatePos.X, floorPos.Y, candidatePos.Z);
            return true;
        }

        return false;
    }

    // === Combat ===

    private void PerformAttack()
    {
        if (_currentEnemy == null || !IsInstanceValid(_currentEnemy)) return;

        _attackCooldownTimer = AttackCooldown;

        float damage = Damage;
        bool isCrit = false;

        // Personality-based modifiers
        if (Personality.RandomCritChance > 0 && GD.Randf() < Personality.RandomCritChance)
        {
            damage *= Personality.CritDamageMultiplier;
            isCrit = true;
        }

        // Backstab bonus for Shade
        if (Personality.BackstabDamageMultiplier > 1f && !IsEnemyFacingMe())
        {
            damage *= Personality.BackstabDamageMultiplier;
        }

        // Deal damage
        _currentEnemy.TakeDamage(damage, GlobalPosition, CrawlerName, isCrit, 1.0f);
        _logger.LogCombat(isCrit ? "CRIT" : "attacked", _currentEnemy.MonsterType, (int)damage);

        // Hank: trip chance after attack
        if (Personality.TripChance > 0 && GD.Randf() < Personality.TripChance)
        {
            TriggerTrip();
        }
    }

    private bool IsEnemyFacingMe()
    {
        if (_currentEnemy == null) return true;

        Vector3 enemyForward = -_currentEnemy.Transform.Basis.Z;
        Vector3 toMe = (GlobalPosition - _currentEnemy.GlobalPosition).Normalized();
        float dot = enemyForward.Dot(toMe);

        return dot > 0.5f;  // Enemy is facing toward me
    }

    /// <summary>
    /// Take damage from an attack.
    /// </summary>
    public void TakeDamage(float damage, Vector3 fromPosition, string source)
    {
        if (CurrentState == AIState.Dead) return;

        CurrentHealth -= (int)damage;
        _logger.LogDamageTaken((int)damage, source, CurrentHealth, MaxHealth);

        // If not in combat, aggro the attacker
        if (CurrentState != AIState.Combat)
        {
            var attacker = FindEnemyByName(source);
            if (attacker != null)
            {
                _currentEnemy = attacker;
                ChangeState(AIState.Combat);
            }
        }

        // Check death
        if (CurrentHealth <= 0)
        {
            Die();
        }
        // Check flee
        else if (!Personality.NeverFlees && CurrentHealth <= MaxHealth * Personality.FleeHealthPercent)
        {
            ChangeState(AIState.Fleeing);
        }
    }

    /// <summary>
    /// Handle permanent death.
    /// </summary>
    private void Die()
    {
        CurrentState = AIState.Dead;

        // Show last words
        string lastWords = Personality.GetRandomQuip(QuipCategory.Death);
        _logger.LogDeath(lastWords, "unknown");

        // Show death bubble
        ShowChatBubble(lastWords, 5f);

        // Create permanent corpse (handled by subclass or here)
        CreateCorpse();

        // Remove from active crawlers after delay
        var tween = CreateTween();
        tween.TweenInterval(3f);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }

    /// <summary>
    /// Create the permanent memorial corpse.
    /// </summary>
    protected virtual void CreateCorpse()
    {
        var corpse = new CrawlerCorpse();
        corpse.Initialize(CrawlerName, Personality.Title, GlobalPosition, Rotation.Y);
        GetTree().Root.AddChild(corpse);
    }

    /// <summary>
    /// Hank-specific: trip and fall during combat.
    /// </summary>
    protected virtual void TriggerTrip()
    {
        GD.Print($"[{CrawlerName}] *trips*");
        ShowChatBubble("*trips*", 1.5f);
        _attackCooldownTimer += 1.0f;  // Extra delay
    }

    // === Finders ===

    protected BasicEnemy3D? FindNearestEnemy()
    {
        var enemies = GetTree().GetNodesInGroup("Enemies");
        BasicEnemy3D? nearest = null;
        float nearestDist = Personality.AggroRange;

        foreach (var node in enemies)
        {
            if (node is BasicEnemy3D enemy &&
                IsInstanceValid(enemy) &&
                enemy.CurrentState != BasicEnemy3D.State.Dead)
            {
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (dist < nearestDist)
                {
                    // Personality check: Lily only attacks weak enemies
                    if (Personality.PreferWeakTargets)
                    {
                        float enemyHpPercent = (float)enemy.CurrentHealth / enemy.MaxHealth;
                        if (enemyHpPercent > 0.3f)
                            continue;  // Skip healthy enemies
                    }

                    nearest = enemy;
                    nearestDist = dist;
                }
            }
        }

        return nearest;
    }

    protected Corpse3D? FindNearestCorpse()
    {
        var corpses = GetTree().GetNodesInGroup("Corpses");
        Corpse3D? nearest = null;
        float nearestDist = Personality.LootSearchRadius;

        foreach (var node in corpses)
        {
            if (node is Corpse3D corpse && IsInstanceValid(corpse) && !corpse.HasBeenLooted)
            {
                float dist = GlobalPosition.DistanceTo(corpse.GlobalPosition);
                if (dist < nearestDist)
                {
                    nearest = corpse;
                    nearestDist = dist;
                }
            }
        }

        return nearest;
    }

    private BasicEnemy3D? FindEnemyByName(string name)
    {
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is BasicEnemy3D enemy && enemy.MonsterType == name)
                return enemy;
        }
        return null;
    }

    protected Vector3 GetSafeZonePosition()
    {
        // Find Bopca
        var shopkeepers = GetTree().GetNodesInGroup("Shopkeepers");
        foreach (var node in shopkeepers)
        {
            if (node is Node3D shop)
                return shop.GlobalPosition;
        }

        // Fallback to spawn position
        return _spawnPosition;
    }

    // === Utility Accessors ===

    public int GetNearbyEnemyCount()
    {
        int count = 0;
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is BasicEnemy3D enemy &&
                IsInstanceValid(enemy) &&
                enemy.CurrentState != BasicEnemy3D.State.Dead &&
                GlobalPosition.DistanceTo(enemy.GlobalPosition) < Personality.AggroRange)
            {
                count++;
            }
        }
        return count;
    }

    public int GetNearbyCorpseCount()
    {
        int count = 0;
        var corpses = GetTree().GetNodesInGroup("Corpses");
        foreach (var node in corpses)
        {
            if (node is Corpse3D corpse &&
                IsInstanceValid(corpse) &&
                !corpse.HasBeenLooted &&
                GlobalPosition.DistanceTo(corpse.GlobalPosition) < Personality.LootSearchRadius)
            {
                count++;
            }
        }
        return count;
    }

    public float GetDistanceToSafeZone()
    {
        return GlobalPosition.DistanceTo(GetSafeZonePosition());
    }

    // === Animation ===

    protected virtual void AnimateIdle(float dt)
    {
        // Override in subclasses for personality-specific idle animations
    }

    protected virtual void AnimateByState(float dt)
    {
        // Basic animation based on state
        // Subclasses can override for more detailed animations
        AnimateIdle(dt);
    }

    // === Dialogue ===

    private void ShowQuip(QuipCategory category)
    {
        if (_quipCooldown > 0) return;
        _quipCooldown = QuipCooldown;

        string quip = Personality.GetRandomQuip(category);
        _logger.LogQuip(quip);
        ShowChatBubble(quip, 3f);
    }

    private void ShowChatBubble(string text, float duration)
    {
        var bubble = MonsterChatBubble.Create(text, GlobalPosition + Vector3.Up * 2f, duration);
        GetTree().Root.AddChild(bubble);
    }

    public override void _ExitTree()
    {
        // Cleanup
    }
}
