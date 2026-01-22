using Godot;
using SafeRoom3D.Player;
using SafeRoom3D.UI;
using SafeRoom3D.Abilities;
using SafeRoom3D.Items;
using SafeRoom3D.Pet;
using SafeRoom3D.Environment;
using SafeRoom3D.Broadcaster;

namespace SafeRoom3D.Core;

/// <summary>
/// Main game manager for 3D version. Handles game state, dungeon generation,
/// enemy spawning, and UI coordination. Ported from 2D GameManager.
/// </summary>
public partial class GameManager3D : Node
{
    // Singleton
    public static GameManager3D? Instance { get; private set; }

    // Core components
    private DungeonGenerator3D? _dungeonGenerator;
    private FPSController? _player;
    private Node3D? _enemyContainer;
    private SoundManager3D? _soundManager;
    private AbilityManager3D? _abilityManager;
    private Inventory3D? _inventory;
    private Steve3D? _steve;
    private InspectMode3D? _inspectMode;
    private InMapEditor? _inMapEditor;

    // Game state
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsPaused { get; private set; }

    // Nameplate visibility (N key toggle) - starts ON
    public static bool NameplatesEnabled { get; private set; } = true;
    [Signal] public delegate void NameplatesToggledEventHandler(bool enabled);

    // Edit mode flag - when true, monsters are in stasis and combat is disabled
    public static bool IsEditMode { get; private set; } = false;

    /// <summary>
    /// Set global edit mode. When enabled, monsters enter stasis and combat is disabled.
    /// </summary>
    public static void SetEditMode(bool enabled)
    {
        IsEditMode = enabled;
        GD.Print($"[GameManager3D] Edit mode: {(enabled ? "ENABLED" : "DISABLED")}");
    }

    // Floor timer
    public float FloorTimeRemaining { get; private set; }
    public bool IsTimerRunning { get; private set; }

    // Hit stop (time freeze effect)
    private float _hitStopTimeRemaining;
    private bool _isHitStopped;

    // Signals
    [Signal] public delegate void FloorTimerUpdatedEventHandler(float timeRemaining);
    [Signal] public delegate void FloorTimerExpiredEventHandler();
    [Signal] public delegate void GameOverEventHandler(string reason);
    [Signal] public delegate void VictoryTriggeredEventHandler();
    [Signal] public delegate void EnemyKilledEventHandler(int remaining);

    public override void _Ready()
    {
        Instance = this;
        FloorTimeRemaining = Constants.FloorTimerSeconds;
        IsTimerRunning = true;

        // CRITICAL: Process even when game is paused so key handling still works
        ProcessMode = ProcessModeEnum.Always;

        // Initialize data-driven configs early
        DataLoader.Initialize();

        GD.Print("[GameManager3D] Initializing...");

        // Initialize game deferred (after scene tree is ready)
        CallDeferred(nameof(InitializeGame));
    }

    private void InitializeGame()
    {
        GD.Print("[GameManager3D] InitializeGame starting...");

        // Create performance monitor (F3 to toggle)
        var perfMonitor = new PerformanceMonitor();
        perfMonitor.Name = "PerformanceMonitor";
        AddChild(perfMonitor);

        // Load saved keybindings
        KeybindingsManager.LoadKeybindings();

        // Create sound manager first
        _soundManager = new SoundManager3D();
        _soundManager.Name = "SoundManager";
        AddChild(_soundManager);

        // Create stats tracker
        var gameStats = new GameStats();
        gameStats.Name = "GameStats";
        AddChild(gameStats);

        // Fade out splash music - DungeonRadio will start after welcome sounds
        SplashMusic.Instance?.FadeOut(3.0f);

        // Create dungeon generator
        _dungeonGenerator = new DungeonGenerator3D();
        _dungeonGenerator.Name = "DungeonGenerator";
        AddChild(_dungeonGenerator);

        // Check for custom map first
        if (!string.IsNullOrEmpty(UI.SplashScreen3D.CustomMapPath))
        {
            GD.Print($"[GameManager3D] Loading custom map: {UI.SplashScreen3D.CustomMapPath}");
            var customMap = MapDefinition.LoadFromJson(UI.SplashScreen3D.CustomMapPath);
            if (customMap != null)
            {
                _dungeonGenerator.GenerateFromMapDefinition(customMap);
                // Clear the path so it doesn't persist to next game
                UI.SplashScreen3D.CustomMapPath = null;
            }
            else
            {
                GD.PrintErr($"[GameManager3D] Failed to load custom map, falling back to procedural");
                _dungeonGenerator.UseLargeDungeon = UI.SplashScreen3D.UseTestMap;
                _dungeonGenerator.Generate();
            }
        }
        else
        {
            // Use splash screen selection for dungeon type
            _dungeonGenerator.UseLargeDungeon = UI.SplashScreen3D.UseTestMap;
            GD.Print($"[GameManager3D] Using Test Map: {UI.SplashScreen3D.UseTestMap}");

            // Generate dungeon
            _dungeonGenerator.Generate();
        }

        // Create player at spawn point
        CreatePlayer();

        // Create ability manager
        CreateAbilityManager();

        // Create inventory
        CreateInventory();

        // Create quest manager
        CreateQuestManager();

        // Create enemy container
        _enemyContainer = new Node3D { Name = "Enemies" };
        AddChild(_enemyContainer);

        // TODO: Spawn enemies in rooms

        // Create UI
        CreateUI();

        // Create world environment for atmosphere
        CreateEnvironment();

        // Create Steve the pet companion
        CreateSteve();

        // Create InspectMode
        CreateInspectMode();

        // Create Dungeon Radio
        CreateDungeonRadio();

        // Check for In-Map Edit Mode (from Map Editor)
        if (UI.SplashScreen3D.EnterEditModeAfterLoad)
        {
            CreateInMapEditor();
            UI.SplashScreen3D.EnterEditModeAfterLoad = false;
        }
        else
        {
            // Schedule dungeon start sound (3 seconds after load) - only in play mode
            ScheduleDungeonStartSound();
        }

        // Create Kill Cam system
        var killCam = new KillCam3D();
        AddChild(killCam);

        GD.Print("[GameManager3D] Game initialized successfully");
    }

    private void ScheduleDungeonStartSound()
    {
        // Play welcome sound 3 seconds after initialization
        var welcomeTimer = GetTree().CreateTimer(3.0);
        welcomeTimer.Timeout += () =>
        {
            SoundManager3D.Instance?.PlayWelcomeSound();
            GD.Print("[GameManager3D] Welcome sound played");

            // Wait for welcome sound to complete (estimate ~3 sec) + 2 more seconds, then play kill kill kill
            var killTimer = GetTree().CreateTimer(5.0);
            killTimer.Timeout += () =>
            {
                SoundManager3D.Instance?.PlayDungeonStartSound();
                GD.Print("[GameManager3D] Dungeon start sound played");

                // Start Dungeon Radio after kill sound finishes (~4 more seconds)
                var radioTimer = GetTree().CreateTimer(4.0);
                radioTimer.Timeout += () =>
                {
                    UI.DungeonRadio.Instance?.Play();
                    GD.Print("[GameManager3D] Dungeon Radio started");
                };
            };
        };
    }

    private void CreatePlayer()
    {
        _player = new FPSController();
        _player.Name = "Player";

        // Position at spawn point
        if (_dungeonGenerator != null)
        {
            _player.Position = _dungeonGenerator.GetSpawnPosition();
        }

        AddChild(_player);

        // Connect player signals
        _player.Died += OnPlayerDied;
        _player.HealthChanged += OnPlayerHealthChanged;

        GD.Print($"[GameManager3D] Player spawned at {_player.Position}");
    }

    private void CreateAbilityManager()
    {
        _abilityManager = new AbilityManager3D();
        _abilityManager.Name = "AbilityManager";
        AddChild(_abilityManager);

        GD.Print("[GameManager3D] Ability Manager created");
    }

    private void CreateInventory()
    {
        _inventory = new Inventory3D();
        _inventory.Name = "Inventory";
        AddChild(_inventory);

        GD.Print("[GameManager3D] Inventory created");
    }

    private void CreateQuestManager()
    {
        var questManager = new QuestManager();
        questManager.Name = "QuestManager";
        AddChild(questManager);

        GD.Print("[GameManager3D] Quest Manager created");
    }

    private void CreateUI()
    {
        // Create HUD layer
        var hudLayer = new CanvasLayer();
        hudLayer.Name = "HUDLayer";
        hudLayer.Layer = 10;
        AddChild(hudLayer);

        // Create HUD
        var hud = new HUD3D();
        hud.Name = "HUD";
        hudLayer.AddChild(hud);

        // Create Spell Book (B key)
        var spellBook = new SpellBook3D();
        spellBook.Name = "SpellBook";
        hudLayer.AddChild(spellBook);

        // Create Abilities Menu (K key)
        var abilitiesUI = new AbilitiesUI3D();
        abilitiesUI.Name = "AbilitiesUI";
        hudLayer.AddChild(abilitiesUI);

        // Create Inventory UI
        var inventoryUI = new InventoryUI3D();
        inventoryUI.Name = "InventoryUI";
        hudLayer.AddChild(inventoryUI);

        // Create Escape Menu
        var escapeMenu = new EscapeMenu3D();
        escapeMenu.Name = "EscapeMenu";
        hudLayer.AddChild(escapeMenu);

        // Create Loot UI
        var lootUI = new LootUI3D();
        lootUI.Name = "LootUI";
        hudLayer.AddChild(lootUI);

        // Create Shop UI (for NPC shopkeepers like Bopca)
        var shopUI = new ShopUI3D();
        shopUI.Name = "ShopUI";
        hudLayer.AddChild(shopUI);

        // Create Quest UI (for quest givers like Mordecai)
        var questUI = new QuestUI3D();
        questUI.Name = "QuestUI";
        hudLayer.AddChild(questUI);

        // Create Character Sheet UI
        var characterSheet = new CharacterSheetUI();
        characterSheet.Name = "CharacterSheet";
        hudLayer.AddChild(characterSheet);

        // Create Editor Screen
        var editor = new EditorScreen3D();
        editor.Name = "EditorScreen";
        hudLayer.AddChild(editor);

        // Create Floor Selector (Enter key menu)
        var floorSelector = new FloorSelector3D();
        floorSelector.Name = "FloorSelector";
        hudLayer.AddChild(floorSelector);

        // Create AI Broadcaster (snarky commentary system)
        var broadcaster = new AIBroadcaster();
        broadcaster.Name = "AIBroadcaster";
        hudLayer.AddChild(broadcaster);

        // Create UI Editor Mode (press X to edit HUD layout)
        var uiEditor = new UIEditorMode();
        uiEditor.Name = "UIEditorMode";
        hudLayer.AddChild(uiEditor);

        GD.Print("[GameManager3D] UI created");
    }

    private void CreateEnvironment()
    {
        // Create WorldEnvironment for atmospheric dungeon feel
        var worldEnv = new WorldEnvironment();
        var env = new Godot.Environment();

        // Very dark ambient - torches should be the primary light source
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor = new Color(0.04f, 0.03f, 0.05f); // Near-black with slight purple
        env.AmbientLightEnergy = 0.15f; // Very dim - let torches dominate

        // Atmospheric fog - warm brown/amber to match torchlight
        env.FogEnabled = true;
        env.FogLightColor = new Color(0.12f, 0.08f, 0.05f); // Warm brown fog
        env.FogDensity = 0.025f; // Slightly denser for atmosphere
        env.FogAerialPerspective = 0.3f; // Add depth haze

        // Background color (visible through fog at distance)
        env.BackgroundMode = Godot.Environment.BGMode.Color;
        env.BackgroundColor = new Color(0.02f, 0.015f, 0.025f); // Very dark purple-black

        // Volumetric fog for dramatic light shafts (if performance allows)
        env.VolumetricFogEnabled = true;
        env.VolumetricFogDensity = 0.02f;
        env.VolumetricFogAlbedo = new Color(0.9f, 0.85f, 0.7f); // Warm scatter color
        env.VolumetricFogEmission = new Color(0f, 0f, 0f);
        env.VolumetricFogEmissionEnergy = 0f;
        env.VolumetricFogAnisotropy = 0.6f; // Forward scattering for light shafts
        env.VolumetricFogLength = 50f;

        // SSAO for depth and ambient occlusion in corners
        // Use moderate quality settings for performance balance
        env.SsaoEnabled = true;
        env.SsaoRadius = 0.5f;           // Smaller radius = better performance
        env.SsaoIntensity = 1.5f;        // Visible but not overwhelming
        env.SsaoPower = 1.2f;            // Slightly soft falloff
        env.SsaoDetail = 0.3f;           // Moderate detail
        env.SsaoHorizon = 0.04f;         // Small horizon angle
        env.SsaoSharpness = 0.9f;        // Sharp edges
        env.SsaoLightAffect = 0.1f;      // Minimal light bleeding

        // Enable subtle glow for torch flames and magic effects
        env.GlowEnabled = true;
        env.GlowIntensity = 0.8f;
        env.GlowStrength = 0.8f;
        env.GlowBloom = 0.1f;
        env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight;
        env.GlowHdrThreshold = 1.2f; // Only very bright things glow

        // Tonemap for cinematic dungeon look
        env.TonemapMode = Godot.Environment.ToneMapper.Filmic;
        env.TonemapExposure = 0.9f; // Slightly darker
        env.TonemapWhite = 2.0f; // Preserve highlights

        // Adjustments for darker shadows and richer colors
        env.AdjustmentEnabled = true;
        env.AdjustmentBrightness = 0.95f;
        env.AdjustmentContrast = 1.1f; // Boost contrast for drama
        env.AdjustmentSaturation = 0.9f; // Slightly desaturated for grim dungeon

        worldEnv.Environment = env;
        AddChild(worldEnv);

        GD.Print("[GameManager3D] Atmospheric dungeon environment created");
    }

    private void CreateSteve()
    {
        if (_player == null) return;

        _steve = new Steve3D();
        _steve.Name = "Steve";
        AddChild(_steve);

        // Position Steve near the player
        _steve.Position = _player.Position + new Vector3(1, 0, 1);

        GD.Print("[GameManager3D] Steve the Chihuahua created");
    }

    private void CreateInspectMode()
    {
        // Add InspectMode to HUD layer
        var hudLayer = GetNode<CanvasLayer>("HUDLayer");
        if (hudLayer == null)
        {
            GD.PrintErr("[GameManager3D] HUDLayer not found for InspectMode");
            return;
        }

        _inspectMode = new InspectMode3D();
        _inspectMode.Name = "InspectMode";
        hudLayer.AddChild(_inspectMode);

        GD.Print("[GameManager3D] InspectMode created");
    }

    private void CreateDungeonRadio()
    {
        // DungeonRadio is a standalone Node (not CanvasLayer child)
        var radio = new UI.DungeonRadio();
        radio.Name = "DungeonRadio";
        AddChild(radio);

        GD.Print("[GameManager3D] DungeonRadio created - Press Y to toggle");
    }

    private void CreateInMapEditor()
    {
        // Add InMapEditor to HUD layer
        var hudLayer = GetNode<CanvasLayer>("HUDLayer");
        if (hudLayer == null)
        {
            GD.PrintErr("[GameManager3D] HUDLayer not found for InMapEditor");
            return;
        }

        _inMapEditor = new InMapEditor();
        _inMapEditor.Name = "InMapEditor";
        hudLayer.AddChild(_inMapEditor);

        // Get the map definition from SplashScreen
        var mapDef = UI.SplashScreen3D.EditModeMap;
        var mapPath = UI.SplashScreen3D.EditModeMapPath;

        if (mapDef != null)
        {
            // Enter edit mode with the map
            _inMapEditor.EnterEditMode(mapDef, mapPath);
            GD.Print($"[GameManager3D] InMapEditor created and entered edit mode for: {mapDef.Name}");
        }
        else
        {
            GD.PrintErr("[GameManager3D] No map definition found for edit mode");
        }

        // Clear the static references
        UI.SplashScreen3D.EditModeMap = null;
        UI.SplashScreen3D.EditModeMapPath = null;
    }

    public override void _Process(double delta)
    {
        // Game logic only - skip when paused/game over
        if (IsGameOver || IsPaused) return;

        float dt = (float)delta;

        // Process hit stop
        if (_isHitStopped)
        {
            _hitStopTimeRemaining -= dt;
            if (_hitStopTimeRemaining <= 0)
            {
                _isHitStopped = false;
                GameConfig.ResumeToNormalSpeed();
            }
            return;
        }

        // Process floor timer
        if (IsTimerRunning)
        {
            FloorTimeRemaining -= dt;
            EmitSignal(SignalName.FloorTimerUpdated, FloorTimeRemaining);

            if (FloorTimeRemaining <= 0)
            {
                FloorTimeRemaining = 0;
                IsTimerRunning = false;
                EmitSignal(SignalName.FloorTimerExpired);
                TriggerGameOver("Time's up!");
            }
        }

        // Update deferred enemy spawning
        _dungeonGenerator?.UpdateDeferredSpawning(dt);
    }

    /// <summary>
    /// Handle input events - this works even when paused due to ProcessMode.Always
    /// Using _Input instead of polling in _Process for more reliable key detection
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (IsGameOver) return;

        // Handle ESC key - close open UIs or show escape menu
        if (@event.IsActionPressed("escape"))
        {
            GD.Print("[GameManager3D] ESC pressed");
            HandleEscapeKey();
        }
        // Toggle spell book on B - works even when paused
        else if (@event.IsActionPressed("toggle_spellbook"))
        {
            GD.Print("[GameManager3D] Spellbook toggle pressed");
            HandleSpellbookToggle();
        }
        // Toggle abilities menu on K - works even when paused
        else if (@event.IsActionPressed("toggle_abilities"))
        {
            GD.Print("[GameManager3D] Abilities menu toggle pressed");
            HandleAbilitiesToggle();
        }
        // Toggle inventory on I - works even when paused
        else if (@event.IsActionPressed("toggle_inventory"))
        {
            GD.Print("[GameManager3D] Inventory toggle pressed");
            HandleInventoryToggle();
        }
        // Toggle character sheet on C key
        else if (@event is InputEventKey charKeyEvent && charKeyEvent.Pressed && charKeyEvent.Keycode == Key.C)
        {
            HandleCharacterSheetToggle();
        }
        // Toggle nameplates on N key
        else if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.N)
        {
            ToggleNameplates();
        }
    }

    private void ToggleNameplates()
    {
        NameplatesEnabled = !NameplatesEnabled;
        EmitSignal(SignalName.NameplatesToggled, NameplatesEnabled);
        GD.Print($"[GameManager3D] Nameplates toggled: {(NameplatesEnabled ? "ON" : "OFF")}");

        // Show feedback to player
        string status = NameplatesEnabled ? "ON" : "OFF";
        HUD3D.Instance?.AddCombatLogMessage($"Nameplates: {status}", new Color(0.8f, 0.8f, 0.8f));
    }

    private void HandleEscapeKey()
    {
        // InspectMode handles its own Escape key to deactivate
        if (InspectMode3D.Instance?.IsActive == true) return;

        // Close loot UI first if open
        if (LootUI3D.Instance != null && LootUI3D.Instance.Visible)
        {
            LootUI3D.Instance.Close();
            return;
        }

        // Close spell book first if open
        if (SpellBook3D.Instance != null && SpellBook3D.Instance.Visible)
        {
            SpellBook3D.Instance.Hide();
            return;
        }

        // Close inventory first if open
        if (InventoryUI3D.Instance != null && InventoryUI3D.Instance.Visible)
        {
            InventoryUI3D.Instance.Hide();
            return;
        }

        // Overview map is handled by HUD3D._Input

        // Show escape menu if not already visible and no other UIs are blocking
        if (EscapeMenu3D.Instance != null && !EscapeMenu3D.Instance.Visible &&
            (EditorScreen3D.Instance == null || !EditorScreen3D.Instance.Visible) &&
            (HUD3D.Instance == null || !HUD3D.Instance.IsOverviewMapVisible))
        {
            EscapeMenu3D.Instance.Show();
        }
    }

    private void HandleSpellbookToggle()
    {
        // Block during inspect mode
        if (InspectMode3D.Instance?.IsActive == true) return;

        if (SpellBook3D.Instance != null &&
            (EscapeMenu3D.Instance == null || !EscapeMenu3D.Instance.Visible) &&
            (EditorScreen3D.Instance == null || !EditorScreen3D.Instance.Visible) &&
            (InventoryUI3D.Instance == null || !InventoryUI3D.Instance.Visible) &&
            (HUD3D.Instance == null || !HUD3D.Instance.IsOverviewMapVisible) &&
            (LootUI3D.Instance == null || !LootUI3D.Instance.Visible))
        {
            SpellBook3D.Instance.Toggle();
        }
    }

    private void HandleAbilitiesToggle()
    {
        // Block during inspect mode
        if (InspectMode3D.Instance?.IsActive == true) return;

        if (AbilitiesUI3D.Instance != null &&
            (EscapeMenu3D.Instance == null || !EscapeMenu3D.Instance.Visible) &&
            (EditorScreen3D.Instance == null || !EditorScreen3D.Instance.Visible) &&
            (InventoryUI3D.Instance == null || !InventoryUI3D.Instance.Visible) &&
            (SpellBook3D.Instance == null || !SpellBook3D.Instance.Visible) &&
            (HUD3D.Instance == null || !HUD3D.Instance.IsOverviewMapVisible) &&
            (LootUI3D.Instance == null || !LootUI3D.Instance.Visible))
        {
            AbilitiesUI3D.Instance.Toggle();
        }
    }

    private void HandleInventoryToggle()
    {
        // Block during inspect mode
        if (InspectMode3D.Instance?.IsActive == true) return;

        // Allow Inventory to open alongside CharacterSheet (for equipping items)
        if (InventoryUI3D.Instance != null &&
            (EscapeMenu3D.Instance == null || !EscapeMenu3D.Instance.Visible) &&
            (EditorScreen3D.Instance == null || !EditorScreen3D.Instance.Visible) &&
            (SpellBook3D.Instance == null || !SpellBook3D.Instance.Visible) &&
            (HUD3D.Instance == null || !HUD3D.Instance.IsOverviewMapVisible) &&
            (LootUI3D.Instance == null || !LootUI3D.Instance.Visible))
        {
            InventoryUI3D.Instance.Toggle();
        }
    }

    private void HandleCharacterSheetToggle()
    {
        // Block during inspect mode
        if (InspectMode3D.Instance?.IsActive == true) return;

        // Allow CharacterSheet to open alongside Inventory (for equipping items)
        if (CharacterSheetUI.Instance != null &&
            (EscapeMenu3D.Instance == null || !EscapeMenu3D.Instance.Visible) &&
            (EditorScreen3D.Instance == null || !EditorScreen3D.Instance.Visible) &&
            (SpellBook3D.Instance == null || !SpellBook3D.Instance.Visible) &&
            (HUD3D.Instance == null || !HUD3D.Instance.IsOverviewMapVisible) &&
            (LootUI3D.Instance == null || !LootUI3D.Instance.Visible))
        {
            CharacterSheetUI.Instance.Toggle();
        }
    }

    /// <summary>
    /// Restart the current floor
    /// </summary>
    public void RestartFloor()
    {
        GD.Print("[GameManager3D] Restarting floor...");

        // Reset game state
        IsGameOver = false;
        IsVictory = false;
        FloorTimeRemaining = Constants.FloorTimerSeconds;
        IsTimerRunning = true;

        // Regenerate dungeon
        _dungeonGenerator?.Generate();

        // Reposition player
        if (_player != null && _dungeonGenerator != null)
        {
            _player.Position = _dungeonGenerator.GetSpawnPosition();
            _player.ResetHealth();
        }

        // Reset time scale to configured game speed
        GameConfig.ResumeToNormalSpeed();
        _isHitStopped = false;

        // Capture mouse
        Input.MouseMode = Input.MouseModeEnum.Captured;

        GD.Print("[GameManager3D] Floor restarted");
    }

    /// <summary>
    /// Request screen shake effect on the player camera
    /// </summary>
    public void RequestScreenShake(float duration, float intensity)
    {
        _player?.RequestScreenShake(duration, intensity);
    }

    /// <summary>
    /// Request hit stop (brief time freeze)
    /// </summary>
    public void RequestHitStop(float duration)
    {
        _isHitStopped = true;
        _hitStopTimeRemaining = duration;
        Engine.TimeScale = 0.05; // Near-freeze
    }

    private void OnPlayerDied()
    {
        TriggerGameOver("You died!");
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        // Could trigger low health warnings here
        if (current <= max * 0.25f && current > 0)
        {
            // TODO: Trigger low health warning sound/visual
        }
    }

    public void TriggerGameOver(string reason)
    {
        if (IsGameOver) return;

        IsGameOver = true;
        IsTimerRunning = false;
        EmitSignal(SignalName.GameOver, reason);

        GD.Print($"[GameManager3D] Game Over: {reason}");

        // Show cursor
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // TODO: Show game over screen
    }

    public void TriggerVictory()
    {
        if (IsVictory) return;

        IsVictory = true;
        IsTimerRunning = false;
        EmitSignal(SignalName.VictoryTriggered);

        GD.Print("[GameManager3D] Victory!");

        // Show cursor
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // TODO: Show victory screen
    }

    public void PauseGame()
    {
        IsPaused = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void ResumeGame()
    {
        IsPaused = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>
    /// Get the player instance
    /// </summary>
    public FPSController? GetPlayer() => _player;

    /// <summary>
    /// Get the dungeon generator
    /// </summary>
    public DungeonGenerator3D? GetDungeon() => _dungeonGenerator;

    public override void _ExitTree()
    {
        Instance = null;
        Engine.TimeScale = 1.0; // Reset to default on exit (not game speed)
    }
}
