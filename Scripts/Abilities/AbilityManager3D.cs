using Godot;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.UI;
using SafeRoom3D.Abilities.Effects;

namespace SafeRoom3D.Abilities;

/// <summary>
/// Manages all player spells and abilities: registration, input handling, cooldowns, unlocks.
/// Central hub for the spell/ability system.
/// Spells = mana cost + cooldown, Abilities = cooldown only.
/// </summary>
public partial class AbilityManager3D : Node
{
    // Singleton
    public static AbilityManager3D? Instance { get; private set; }

    // All registered spells and abilities
    private readonly Dictionary<string, Ability3D> _abilities = new();

    // Unlock tracking
    private readonly HashSet<string> _unlockedIds = new();

    // Three hotbars: Bar 0 = 1-0 keys, Bar 1 = Shift+1-0, Bar 2 = Alt+1-0
    private readonly Ability3D?[,] _hotbars = new Ability3D?[3, 10];

    // Targeting state
    private bool _isTargeting;
    private Ability3D? _targetingAbility;
    private TargetingIndicator3D? _targetingIndicator;
    private bool _wasTimePaused; // Track if we paused time for targeting

    // References
    private FPSController? _player;

    // Signals
    [Signal] public delegate void AbilityRegisteredEventHandler(string abilityId, string abilityName);
    [Signal] public delegate void AbilityActivatedEventHandler(string abilityId);
    [Signal] public delegate void AbilityCooldownUpdatedEventHandler(string abilityId, float remaining, float total);
    [Signal] public delegate void HotbarSlotChangedEventHandler(int row, int slot, string? abilityId);
    [Signal] public delegate void TargetingStartedEventHandler(string abilityId, float radius, Color color);
    [Signal] public delegate void TargetingEndedEventHandler();
    [Signal] public delegate void AbilityUnlockedEventHandler(string abilityId, string category);

    public override void _Ready()
    {
        Instance = this;

        // Allow processing even when game time is paused (for targeting)
        ProcessMode = ProcessModeEnum.Always;

        // Find player when ready
        CallDeferred(nameof(Initialize));
    }

    private void Initialize()
    {
        _player = FPSController.Instance;

        // Create all spells and abilities
        CreateAbilities();

        // Set up starter unlocks
        SetupStarterUnlocks();

        // Set up default hotbar
        SetupDefaultHotbar();

        // Create targeting indicator
        CreateTargetingIndicator();

        // Connect to game manager for enemy kill events (Audience Favorite)
        if (GameManager3D.Instance != null)
        {
            GameManager3D.Instance.EnemyKilled += OnEnemyKilled;
        }

        GD.Print($"[AbilityManager3D] Initialized with {_abilities.Count} spells/abilities ({_unlockedIds.Count} unlocked)");
    }

    private void SetupStarterUnlocks()
    {
        // Player starts at level 0 with only Heal spell unlocked
        UnlockAbility("heal");
    }

    private void CreateAbilities()
    {
        // ========== SPELLS (mana cost + cooldown) ==========
        // Starter spell - available from level 0
        RegisterAbility(new Heal3D());               // Level 0 - Starter (20 HP, 3 mana)

        // Existing spells
        RegisterAbility(new Fireball3D());           // Level 1
        RegisterAbility(new ChainLightning3D());     // Level 2
        RegisterAbility(new SoulLeech3D());          // Level 3
        RegisterAbility(new InfernalGround3D());     // Level 4
        RegisterAbility(new GravityWell3D());        // Level 5
        RegisterAbility(new BansheesWail3D());       // Level 6
        RegisterAbility(new TimestopBubble3D());     // Level 7
        RegisterAbility(new Berserk3D());            // Level 8

        // New spells
        RegisterAbility(new FrostNova3D());          // Level 9
        RegisterAbility(new ArcaneMissiles3D());     // Level 10
        RegisterAbility(new PoisonCloud3D());        // Level 11
        RegisterAbility(new MeteorStrike3D());       // Level 12
        RegisterAbility(new LightningStorm3D());     // Level 13
        RegisterAbility(new DeathCoil3D());          // Level 14
        RegisterAbility(new ManaBurn3D());           // Level 15

        // ========== ABILITIES (cooldown only, no mana) ==========
        // Existing abilities
        RegisterAbility(new ProtectiveShell3D());    // Level 1 - Starter
        RegisterAbility(new SponsorBlessing3D());    // Level 2
        RegisterAbility(new MirrorImage3D());        // Level 3
        RegisterAbility(new DeadMansRally3D());      // Level 4
        RegisterAbility(new AudienceFavorite3D());   // Level 5
        RegisterAbility(new EngineOfTomorrow3D());   // Level 6

        // New abilities (Phase 4)
        RegisterAbility(new DodgeRoll3D());         // Level 7
        RegisterAbility(new WarCry3D());            // Level 8
        RegisterAbility(new FeignDeath3D());        // Level 9
        RegisterAbility(new SecondWind3D());        // Level 10
        RegisterAbility(new Bloodlust3D());         // Level 11
        RegisterAbility(new Taunt3D());             // Level 12
        RegisterAbility(new SmokeBomb3D());         // Level 13
        RegisterAbility(new AdrenalineRush3D());    // Level 14
        RegisterAbility(new MarkedForDeath3D());    // Level 15

        GD.Print($"[AbilityManager3D] Registered {_abilities.Count} spells/abilities");
    }

    private void RegisterAbility(Ability3D ability)
    {
        if (_abilities.ContainsKey(ability.AbilityId))
        {
            GD.PrintErr($"[AbilityManager3D] Duplicate ability ID: {ability.AbilityId}");
            return;
        }

        // Add as child so it receives _Process calls
        AddChild(ability);

        _abilities[ability.AbilityId] = ability;

        // Connect signals
        ability.CooldownUpdated += (remaining, total) =>
            EmitSignal(SignalName.AbilityCooldownUpdated, ability.AbilityId, remaining, total);

        EmitSignal(SignalName.AbilityRegistered, ability.AbilityId, ability.AbilityName);
    }

    private void SetupDefaultHotbar()
    {
        // Player starts with empty hotbars - they must drag spells/abilities from the Spell Book
        // All 3 bars (30 slots total) start empty
        // Bar 0 (1-0 keys), Bar 1 (Shift+1-0), Bar 2 (Alt+1-0)

        GD.Print("[AbilityManager3D] Hotbars initialized empty (player must assign from Spell Book)");
    }

    private void CreateTargetingIndicator()
    {
        _targetingIndicator = new TargetingIndicator3D();
        _targetingIndicator.Name = "TargetingIndicator";
        _targetingIndicator.Visible = false;
        AddChild(_targetingIndicator);
    }

    public override void _Input(InputEvent @event)
    {
        // Don't process input if game is over
        if (GameManager3D.Instance?.IsGameOver == true) return;

        // Handle targeting mode input
        if (_isTargeting)
        {
            // Left click to confirm
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                ConfirmTargeting();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Right click to cancel
            if (@event is InputEventMouseButton mb2 && mb2.ButtonIndex == MouseButton.Right && mb2.Pressed)
            {
                CancelTargeting();
                GetViewport().SetInputAsHandled();
                return;
            }

            // ESC to cancel (but don't consume - let GameManager handle it too if needed)
            if (@event.IsActionPressed("escape"))
            {
                CancelTargeting();
                // Don't consume - GameManager might need to handle escape too
                return;
            }
        }

        // Handle hotbar input (number keys 1-0)
        // Skip if modal UIs are open (these block gameplay input)
        bool modalUIOpen = EscapeMenu3D.Instance?.Visible == true ||
                           SpellBook3D.Instance?.Visible == true ||
                           InventoryUI3D.Instance?.Visible == true ||
                           CharacterSheetUI.Instance?.Visible == true ||
                           EditorScreen3D.Instance?.Visible == true ||
                           FloorSelector3D.Instance?.IsOpen == true;

        if (modalUIOpen) return;

        // Check modifiers from the current event to avoid stuck modifier keys after alt-tab
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            int slot = GetHotbarSlotFromKeycode(keyEvent.Keycode);
            if (slot >= 0)
            {
                // Use event modifiers (not Input.IsKeyPressed) for reliable modifier detection
                int hotbarRow = 0;
                if (keyEvent.ShiftPressed)
                {
                    hotbarRow = 1;
                }
                else if (keyEvent.AltPressed)
                {
                    hotbarRow = 2;
                }

                TryActivateHotbarSlot(hotbarRow, slot);
                return;
            }
        }
    }

    public override void _Process(double delta)
    {
        // Update targeting indicator position
        if (_isTargeting && _targetingIndicator != null)
        {
            UpdateTargetingIndicator();
        }
    }

    /// <summary>
    /// Converts a keycode to a hotbar slot index (0-9), or -1 if not a hotbar key.
    /// </summary>
    private static int GetHotbarSlotFromKeycode(Key keycode)
    {
        return keycode switch
        {
            Key.Key1 => 0,
            Key.Key2 => 1,
            Key.Key3 => 2,
            Key.Key4 => 3,
            Key.Key5 => 4,
            Key.Key6 => 5,
            Key.Key7 => 6,
            Key.Key8 => 7,
            Key.Key9 => 8,
            Key.Key0 => 9,
            _ => -1
        };
    }

    private void UpdateTargetingIndicator()
    {
        if (_player == null || _targetingIndicator == null) return;

        var targetPos = GetTargetPositionForAbility();
        if (targetPos.HasValue)
        {
            _targetingIndicator.GlobalPosition = targetPos.Value;
        }
    }

    private Vector3? GetTargetPositionForAbility()
    {
        if (_player == null) return null;

        var spaceState = _player.GetWorld3D().DirectSpaceState;
        var camera = _player.GetViewport().GetCamera3D();
        if (camera == null) return null;

        Vector3 origin;
        Vector3 end;

        // When time is paused, use mouse position to raycast
        // When time is running, use camera center
        if (_wasTimePaused)
        {
            // Get mouse position on screen and project ray from camera
            var mousePos = _player.GetViewport().GetMousePosition();
            origin = camera.ProjectRayOrigin(mousePos);
            var direction = camera.ProjectRayNormal(mousePos);
            end = origin + direction * 100f;
        }
        else
        {
            // Use camera look direction (center of screen)
            origin = _player.GetCameraPosition();
            var direction = _player.GetLookDirection();
            end = origin + direction * 50f;
        }

        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollideWithBodies = true;
        query.CollisionMask = 8 | 16; // Wall, Obstacle (floors/walls)
        query.CollideWithAreas = false;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            return (Vector3)result["position"];
        }

        // Fallback: project to ground plane (Y=0)
        if (_wasTimePaused)
        {
            var mousePos = _player.GetViewport().GetMousePosition();
            var rayOrigin = camera.ProjectRayOrigin(mousePos);
            var rayDir = camera.ProjectRayNormal(mousePos);

            // Intersect with ground plane at player's Y level
            float groundY = _player.GlobalPosition.Y;
            if (Mathf.Abs(rayDir.Y) > 0.001f)
            {
                float t = (groundY - rayOrigin.Y) / rayDir.Y;
                if (t > 0)
                {
                    return rayOrigin + rayDir * t;
                }
            }
        }

        return end;
    }

    /// <summary>
    /// Try to activate ability by ID.
    /// </summary>
    public bool TryActivateAbility(string abilityId)
    {
        if (!_abilities.TryGetValue(abilityId, out var ability))
        {
            GD.PrintErr($"[AbilityManager3D] Unknown ability: {abilityId}");
            return false;
        }

        return TryActivateAbilityInternal(ability);
    }

    /// <summary>
    /// Try to activate ability in hotbar slot (single bar, defaults to bar 0).
    /// </summary>
    public void TryActivateHotbarSlot(int slot)
    {
        TryActivateHotbarSlot(0, slot);
    }

    /// <summary>
    /// Try to activate ability in hotbar slot on specific bar.
    /// Also handles consumable items assigned to the hotbar.
    /// </summary>
    public void TryActivateHotbarSlot(int row, int slot)
    {
        if (row < 0 || row >= 3) return;
        if (slot < 0 || slot >= 10) return;

        // First check if this slot has a consumable
        var actionSlot = UI.HUD3D.Instance?.GetActionSlot(row, slot);
        if (actionSlot?.HasConsumable == true)
        {
            TryUseConsumable(row, slot, actionSlot.ConsumableId!);
            return;
        }

        // Otherwise, try ability
        var ability = _hotbars[row, slot];
        if (ability == null)
        {
            string modifier = row == 0 ? "" : row == 1 ? "Shift+" : "Alt+";
            int keyNum = slot == 9 ? 0 : slot + 1;
            GD.Print($"[AbilityManager3D] Hotbar slot {modifier}{keyNum} is empty");
            return;
        }

        TryActivateAbilityInternal(ability);
    }

    /// <summary>
    /// Try to use a consumable item from the hotbar.
    /// </summary>
    private void TryUseConsumable(int row, int slot, string consumableId)
    {
        if (_player == null) return;

        // Check if player has this item
        if (Items.Inventory3D.Instance?.HasItem(consumableId) != true)
        {
            GD.Print($"[AbilityManager3D] No {consumableId} in inventory");
            return;
        }

        // Try to use it
        if (Items.ConsumableItems3D.UseItem(consumableId, _player))
        {
            // Consume one from inventory
            Items.Inventory3D.Instance?.UseItemById(consumableId);

            // Flash the slot
            UI.HUD3D.Instance?.FlashSlot(row, slot);

            // Update stack count
            UI.HUD3D.Instance?.UpdateConsumableStackCount(row, slot);

            GD.Print($"[AbilityManager3D] Used consumable: {consumableId}");
        }
    }

    private bool TryActivateAbilityInternal(Ability3D ability)
    {
        // Cancel any current targeting
        if (_isTargeting && _targetingAbility != ability)
        {
            CancelTargeting();
        }

        // Check if ability requires targeting
        if (ability.Type == AbilityType.Targeted && !_isTargeting)
        {
            StartTargeting(ability);
            return true;
        }

        // Activate the ability
        bool success = ability.TryActivate();
        if (success)
        {
            EmitSignal(SignalName.AbilityActivated, ability.AbilityId);
            SoundManager3D.Instance?.PlayMagicSound(_player?.GetCameraPosition() ?? Vector3.Zero);

            // Log spell cast to combat log
            HUD3D.Instance?.LogSpellCast(ability.AbilityName);

            // Record ability cast in game stats
            GameStats.Instance?.RecordAbilityCast(ability.AbilityName);
        }

        return success;
    }

    private void StartTargeting(Ability3D ability)
    {
        _isTargeting = true;
        _targetingAbility = ability;

        // Get targeting properties from ability
        float radius = 5f; // Default
        Color color = Colors.Orange;

        if (ability is ITargetedAbility targeted)
        {
            radius = targeted.TargetRadius;
            color = targeted.TargetColor;
        }

        // Show indicator
        if (_targetingIndicator != null)
        {
            _targetingIndicator.SetRadius(radius);
            _targetingIndicator.SetColor(color);
            _targetingIndicator.Visible = true;
        }

        // TACTICAL TIME PAUSE: Pause game time for strategic targeting
        // This gives the player time to carefully position AOE abilities
        _wasTimePaused = true;
        Engine.TimeScale = 0.0; // Freeze time completely

        // Lock mouse control from FPSController and show cursor
        if (_player != null)
        {
            _player.MouseControlLocked = true;
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;

        GD.Print($"[AbilityManager3D] TACTICAL TARGETING for {ability.AbilityName} - TIME PAUSED, MOUSE LOCKED");

        EmitSignal(SignalName.TargetingStarted, ability.AbilityId, radius, color);
    }

    private void ConfirmTargeting()
    {
        if (!_isTargeting || _targetingAbility == null) return;

        var targetPos = GetTargetPositionForAbility();
        if (!targetPos.HasValue)
        {
            CancelTargeting();
            return;
        }

        // Pass target position to ability
        if (_targetingAbility is ITargetedAbility targeted)
        {
            targeted.SetTargetPosition(targetPos.Value);
        }

        // Activate ability
        bool success = _targetingAbility.TryActivate();
        if (success)
        {
            EmitSignal(SignalName.AbilityActivated, _targetingAbility.AbilityId);
            SoundManager3D.Instance?.PlayMagicSound(targetPos.Value);

            // Log spell cast to combat log
            HUD3D.Instance?.LogSpellCast(_targetingAbility.AbilityName);

            // Record ability cast in game stats
            GameStats.Instance?.RecordAbilityCast(_targetingAbility.AbilityName);
        }

        EndTargeting();
    }

    /// <summary>
    /// Cancel current targeting mode. Safe to call even if not targeting.
    /// </summary>
    public void CancelTargeting()
    {
        if (!_isTargeting) return;

        GD.Print("[AbilityManager3D] Targeting cancelled");
        EndTargeting();
    }

    private void EndTargeting()
    {
        GD.Print("[AbilityManager3D] === EndTargeting START ===");
        _isTargeting = false;
        _targetingAbility = null;

        if (_targetingIndicator != null)
        {
            _targetingIndicator.Visible = false;
        }

        // STEP 1: Recapture mouse FIRST while still locked
        // This prevents FPSController from seeing the transition
        Input.MouseMode = Input.MouseModeEnum.Captured;
        GD.Print("[AbilityManager3D] Set MouseMode = Captured");

        // STEP 2: Resume time to configured game speed
        if (_wasTimePaused)
        {
            Core.GameConfig.ResumeToNormalSpeed();
            _wasTimePaused = false;
            GD.Print($"[AbilityManager3D] TIME RESUMED (TimeScale = {Core.GameConfig.GameSpeed})");
        }

        // STEP 3: Unlock mouse control LAST (after mouse is already captured)
        if (_player != null)
        {
            _player.MouseControlLocked = false;
            GD.Print("[AbilityManager3D] MouseControlLocked = FALSE");
        }
        else
        {
            GD.PrintErr("[AbilityManager3D] ERROR: _player is null, cannot unlock mouse!");
        }

        // Emit signal
        EmitSignal(SignalName.TargetingEnded);

        // Deferred recapture as fallback
        CallDeferred(nameof(DeferredMouseRecapture));

        GD.Print("[AbilityManager3D] === EndTargeting END ===");
    }

    private void DeferredMouseRecapture()
    {
        // Use the centralized UI check from FPSController
        bool uiOpen = FPSController.IsAnyUIOpen();
        bool notCaptured = Input.MouseMode != Input.MouseModeEnum.Captured;

        GD.Print($"[AbilityManager3D] DeferredMouseRecapture: uiOpen={uiOpen}, notCaptured={notCaptured}");

        if (!uiOpen && notCaptured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GD.Print("[AbilityManager3D] Deferred mouse recapture DONE");
        }
    }

    /// <summary>
    /// Assign an ability to a hotbar slot (defaults to bar 0).
    /// </summary>
    public void AssignToHotbar(int slot, string abilityId)
    {
        AssignToHotbar(0, slot, abilityId);
    }

    /// <summary>
    /// Assign an ability to a hotbar slot on a specific bar.
    /// </summary>
    public void AssignToHotbar(int row, int slot, string abilityId)
    {
        if (row < 0 || row >= 3) return;
        if (slot < 0 || slot >= 10) return;

        if (!_abilities.TryGetValue(abilityId, out var ability))
        {
            GD.PrintErr($"[AbilityManager3D] Unknown ability: {abilityId}");
            return;
        }

        _hotbars[row, slot] = ability;
        EmitSignal(SignalName.HotbarSlotChanged, row, slot, abilityId);
    }

    /// <summary>
    /// Clear a hotbar slot (defaults to bar 0).
    /// </summary>
    public void ClearHotbarSlot(int slot)
    {
        ClearHotbarSlot(0, slot);
    }

    /// <summary>
    /// Clear a hotbar slot on a specific bar.
    /// </summary>
    public void ClearHotbarSlot(int row, int slot)
    {
        if (row < 0 || row >= 3) return;
        if (slot < 0 || slot >= 10) return;

        _hotbars[row, slot] = null;
        EmitSignal(SignalName.HotbarSlotChanged, row, slot, Variant.From<string?>(null));
    }

    /// <summary>
    /// Get ability by ID.
    /// </summary>
    public Ability3D? GetAbility(string abilityId)
    {
        return _abilities.TryGetValue(abilityId, out var ability) ? ability : null;
    }

    /// <summary>
    /// Get ability in hotbar slot (defaults to bar 0).
    /// </summary>
    public Ability3D? GetHotbarAbility(int slot)
    {
        return GetHotbarAbility(0, slot);
    }

    /// <summary>
    /// Get ability in hotbar slot on a specific bar.
    /// </summary>
    public Ability3D? GetHotbarAbility(int row, int slot)
    {
        if (row < 0 || row >= 3) return null;
        if (slot < 0 || slot >= 10) return null;
        return _hotbars[row, slot];
    }

    /// <summary>
    /// Get all registered abilities.
    /// </summary>
    public IEnumerable<Ability3D> GetAllAbilities() => _abilities.Values;

    /// <summary>
    /// Check if currently in targeting mode.
    /// </summary>
    public bool IsTargeting => _isTargeting;

    /// <summary>
    /// Get move speed multiplier from active buffs.
    /// </summary>
    public float GetMoveSpeedMultiplier()
    {
        float multiplier = 1f;

        // Berserk doubles movement speed
        if (_abilities.TryGetValue("berserk", out var berserk) && berserk.IsActive)
        {
            multiplier *= 2f;
        }

        return multiplier;
    }

    /// <summary>
    /// Get attack cooldown multiplier from active buffs.
    /// </summary>
    public float GetAttackCooldownMultiplier()
    {
        float multiplier = 1f;

        // Berserk halves attack cooldown
        if (_abilities.TryGetValue("berserk", out var berserk) && berserk.IsActive)
        {
            multiplier *= 0.5f;
        }

        return multiplier;
    }

    /// <summary>
    /// Get damage bonus from active buffs.
    /// </summary>
    public float GetDamageMultiplier()
    {
        float multiplier = 1f;

        // Dead Man's Rally increases damage at low HP
        if (_abilities.TryGetValue("dead_mans_rally", out var dmr) && dmr.IsActive)
        {
            if (dmr is DeadMansRally3D deadMansRally)
            {
                multiplier *= deadMansRally.GetDamageBonus();
            }
        }

        return multiplier;
    }

    /// <summary>
    /// Get enemy speed multiplier from debuffs.
    /// </summary>
    public float GetEnemySpeedMultiplier()
    {
        // Engine of Tomorrow slows all enemies
        if (_abilities.TryGetValue("engine_of_tomorrow", out var eot) && eot.IsActive)
        {
            return 0.5f;
        }

        return 1f;
    }

    /// <summary>
    /// Check if player is invulnerable (Protective Shell active).
    /// </summary>
    public bool IsPlayerInvulnerable()
    {
        if (_abilities.TryGetValue("protective_shell", out var shell))
        {
            return shell.IsActive;
        }
        return false;
    }

    /// <summary>
    /// Called when an enemy is killed. Used by Audience Favorite.
    /// </summary>
    private void OnEnemyKilled(int remaining)
    {
        // Notify Audience Favorite
        if (_abilities.TryGetValue("audience_favorite", out var af) && af.IsActive)
        {
            if (af is AudienceFavorite3D audienceFavorite)
            {
                audienceFavorite.OnEnemyKilled();
            }
        }
    }

    /// <summary>
    /// Get a random ability for cooldown reset (Audience Favorite).
    /// </summary>
    public Ability3D? GetRandomAbilityOnCooldown()
    {
        var onCooldown = new List<Ability3D>();

        foreach (var ability in _abilities.Values)
        {
            if (ability.IsOnCooldown && ability.AbilityId != "audience_favorite")
            {
                onCooldown.Add(ability);
            }
        }

        if (onCooldown.Count == 0) return null;

        int index = GD.RandRange(0, onCooldown.Count - 1);
        return onCooldown[index];
    }

    // ============================================
    // UNLOCK SYSTEM
    // ============================================

    /// <summary>
    /// Unlock a spell or ability by ID.
    /// </summary>
    public bool UnlockAbility(string abilityId)
    {
        if (!_abilities.TryGetValue(abilityId, out var ability))
        {
            GD.PrintErr($"[AbilityManager3D] Unknown ability: {abilityId}");
            return false;
        }

        if (_unlockedIds.Contains(abilityId))
        {
            GD.Print($"[AbilityManager3D] {abilityId} already unlocked");
            return false;
        }

        _unlockedIds.Add(abilityId);
        ability.IsUnlocked = true;

        string category = ability.UsesMana ? "Spell" : "Ability";
        EmitSignal(SignalName.AbilityUnlocked, abilityId, category);
        GD.Print($"[AbilityManager3D] Unlocked {category}: {ability.AbilityName}");

        return true;
    }

    /// <summary>
    /// Check if a spell/ability is unlocked.
    /// </summary>
    public bool IsUnlocked(string abilityId)
    {
        return _unlockedIds.Contains(abilityId);
    }

    /// <summary>
    /// Get all spells (UsesMana = true).
    /// </summary>
    public IEnumerable<Ability3D> GetAllSpells()
    {
        return _abilities.Values.Where(a => a.UsesMana);
    }

    /// <summary>
    /// Get all abilities (UsesMana = false).
    /// </summary>
    public IEnumerable<Ability3D> GetOnlyAbilities()
    {
        return _abilities.Values.Where(a => !a.UsesMana);
    }

    /// <summary>
    /// Get unlocked spells.
    /// </summary>
    public IEnumerable<Ability3D> GetUnlockedSpells()
    {
        return _abilities.Values.Where(a => a.UsesMana && a.IsUnlocked);
    }

    /// <summary>
    /// Get unlocked abilities.
    /// </summary>
    public IEnumerable<Ability3D> GetUnlockedAbilities()
    {
        return _abilities.Values.Where(a => !a.UsesMana && a.IsUnlocked);
    }

    /// <summary>
    /// Get spells available to unlock at a given player level.
    /// </summary>
    public IEnumerable<Ability3D> GetSpellsAvailableAtLevel(int level)
    {
        return _abilities.Values
            .Where(a => a.UsesMana && !a.IsUnlocked && a.RequiredLevel <= level)
            .OrderBy(a => a.RequiredLevel);
    }

    /// <summary>
    /// Get abilities available to unlock at a given player level.
    /// </summary>
    public IEnumerable<Ability3D> GetAbilitiesAvailableAtLevel(int level)
    {
        return _abilities.Values
            .Where(a => !a.UsesMana && !a.IsUnlocked && a.RequiredLevel <= level)
            .OrderBy(a => a.RequiredLevel);
    }

    /// <summary>
    /// Get total unlocked count.
    /// </summary>
    public int UnlockedCount => _unlockedIds.Count;

    /// <summary>
    /// Get unlocked spell count.
    /// </summary>
    public int UnlockedSpellCount => _abilities.Values.Count(a => a.UsesMana && a.IsUnlocked);

    /// <summary>
    /// Get unlocked ability count.
    /// </summary>
    public int UnlockedAbilityCount => _abilities.Values.Count(a => !a.UsesMana && a.IsUnlocked);

    /// <summary>
    /// Save unlocked IDs for persistence.
    /// </summary>
    public List<string> GetUnlockedIds()
    {
        return _unlockedIds.ToList();
    }

    /// <summary>
    /// Load unlocked IDs from save data.
    /// </summary>
    public void LoadUnlockedIds(IEnumerable<string> ids)
    {
        _unlockedIds.Clear();

        foreach (var id in ids)
        {
            if (_abilities.TryGetValue(id, out var ability))
            {
                _unlockedIds.Add(id);
                ability.IsUnlocked = true;
            }
        }

        GD.Print($"[AbilityManager3D] Loaded {_unlockedIds.Count} unlocked spells/abilities");
    }

    public override void _ExitTree()
    {
        Instance = null;

        // Disconnect signals
        if (GameManager3D.Instance != null)
        {
            GameManager3D.Instance.EnemyKilled -= OnEnemyKilled;
        }
    }
}

/// <summary>
/// Interface for abilities that require targeting.
/// </summary>
public interface ITargetedAbility
{
    float TargetRadius { get; }
    Color TargetColor { get; }
    void SetTargetPosition(Vector3 position);
}
