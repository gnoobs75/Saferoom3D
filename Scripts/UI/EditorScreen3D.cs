using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Core;
using SafeRoom3D.Abilities;
using SafeRoom3D.Enemies;
using SafeRoom3D.Environment;
using SafeRoom3D.Items;
using SafeRoom3D.Pet;

namespace SafeRoom3D.UI;

/// <summary>
/// Full-screen 3D editor for monsters, abilities, and dungeon assets.
/// Includes a 3D model viewer for previewing procedural models with mouse controls.
/// </summary>
public partial class EditorScreen3D : Control
{
    public static EditorScreen3D? Instance { get; private set; }

    // UI Elements
    private TabContainer? _tabContainer;
    private PanelContainer? _detailPanel;
    private SubViewportContainer? _viewportContainer;
    private MapEditorTab? _mapEditorOverlay;
    private Control? _mainContentArea;  // The normal editor content (viewport + detail panel)
    private SubViewport? _previewViewport;
    private Node3D? _previewScene;
    private Camera3D? _previewCamera;
    private Node3D? _previewObject;
    private float _previewRotation = 0f;
    private bool _autoRotate = true;

    // Camera control state
    private bool _isDragging = false;
    private Vector2 _lastMousePos;
    private float _cameraDistance = 4f;
    private float _cameraYaw = 0.5f;
    private float _cameraPitch = 0.4f;  // Radians above horizontal (looking down at model)
    private float _cameraTargetY = 0.6f;

    // Animation state
    private bool _cycleAnimations = false;
    private int _currentAnimationIndex = 0;
    private float _animationTimer = 0f;
    private float _animationCycleDuration = 2f;
    // All 18 animations: 6 types × 3 variants each
    private string[] _monsterAnimations = {
        "idle_0", "idle_1", "idle_2",
        "walk_0", "walk_1", "walk_2",
        "run_0", "run_1", "run_2",
        "attack_0", "attack_1", "attack_2",
        "hit_0", "hit_1", "hit_2",
        "die_0", "die_1", "die_2"
    };
    private string _currentAnimation = "idle_0";

    // AnimationPlayer for preview
    private AnimationPlayer? _previewAnimPlayer;
    private MonsterMeshFactory.LimbNodes? _previewLimbNodes;

    // Fallback animation state for preview (when no AnimationPlayer)
    private float _previewAnimTime = 0f;
    private Dictionary<string, Node3D> _fallbackLimbNodes = new();

    // Editor state
    private string? _selectedMonsterId;
    private string? _selectedAbilityId;
    private string? _selectedCosmeticType;
    private int _currentTab = 0;
    private Label? _titleLabel; // Dynamic title based on active tab

    // Monster editor
    private Dictionary<string, Button> _monsterButtons = new();
    private SpinBox? _monsterHealthSpinBox;
    private SpinBox? _monsterDamageSpinBox;
    private SpinBox? _monsterSpeedSpinBox;
    private SpinBox? _monsterAggroSpinBox;
    private ColorPickerButton? _monsterSkinColorPicker;

    // Ability editor
    private Dictionary<string, Button> _abilityButtons = new();
    private SpinBox? _abilityCooldownSpinBox;
    private SpinBox? _abilityDamageSpinBox;
    private SpinBox? _abilityRadiusSpinBox;
    private SpinBox? _abilityDurationSpinBox;
    private SpinBox? _abilityManaCostSpinBox;

    // Cosmetics editor
    private Dictionary<string, Button> _cosmeticButtons = new();
    private SpinBox? _cosmeticScaleSpinBox;
    private CheckBox? _cosmeticHasLightCheckBox;
    private SpinBox? _cosmeticLightRadiusSpinBox;
    private ColorPickerButton? _cosmeticLightColorPicker;

    // NPCs editor
    private Dictionary<string, Button> _npcButtons = new();
    private string? _selectedNpcId;

    // Weapons editor
    private Dictionary<string, Button> _weaponButtons = new();
    private string? _selectedWeaponType;
    private OptionButton? _attachMonsterDropdown;
    private string? _attachedMonsterType;
    private Label? _weaponDamageLabel;
    private Label? _weaponSpeedLabel;
    private Label? _weaponRangeLabel;
    private Label? _weaponHandedLabel;

    // Shields/Armor editor
    private Dictionary<string, Button> _shieldButtons = new();
    private string? _selectedShieldType;
    private Label? _shieldArmorLabel;
    private Label? _shieldBlockChanceLabel;
    private Label? _shieldBlockAmountLabel;
    private Label? _shieldMovePenaltyLabel;

    // Info labels
    private Label? _selectedItemLabel;
    private Label? _viewportHintLabel;
    private Label? _animationLabel;

    // Animation controls
    private CheckBox? _cycleAnimationsCheckBox;
    private OptionButton? _animationDropdown;

    // Status effect controls
    private OptionButton? _statusEffectDropdown;
    private Node3D? _statusEffectVisual;
    private StatusEffectType _currentStatusEffect = StatusEffectType.None;

    // Top bar controls (for tab-specific visibility)
    private HBoxContainer? _animationControlsContainer;
    private HBoxContainer? _statusEffectContainer;
    private CheckBox? _autoRotateCheckBox;

    // Detail panel section containers (for tab-specific visibility)
    private VBoxContainer? _monsterStatsSection;
    private VBoxContainer? _abilityStatsSection;
    private VBoxContainer? _cosmeticPropsSection;
    private VBoxContainer? _weaponStatsSection;
    private VBoxContainer? _npcStatsSection;

    // NPC editor controls
    private ColorPickerButton? _npcSkinColorPicker;
    private SpinBox? _npcScaleSpinBox;
    private CheckBox? _npcUseGlbCheckbox;
    private Label? _npcGlbPathLabel;

    // Close button
    private Button? _closeButton;

    // Sound controls
    private VBoxContainer? _soundSection;
    private Label? _soundStatusLabel;
    private Label? _soundFolderLabel;
    private VBoxContainer? _availableSoundsContainer;
    private string _currentSoundFolder = "res://Assets/Audio/Sounds/Goblin";
    private Dictionary<string, Button> _soundButtons = new();
    private Dictionary<string, Label> _soundFileLabels = new();
    private Dictionary<string, string> _soundAssignments = new(); // action -> file path

    public override void _Ready()
    {
        Instance = this;

        // Make this control fill the entire screen
        SetAnchorsPreset(LayoutPreset.FullRect);
        AnchorLeft = 0;
        AnchorTop = 0;
        AnchorRight = 1;
        AnchorBottom = 1;
        OffsetLeft = 0;
        OffsetTop = 0;
        OffsetRight = 0;
        OffsetBottom = 0;

        // Force update size to viewport
        var viewportSize = GetViewport().GetVisibleRect().Size;
        Size = viewportSize;

        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;

        BuildUI();

        Visible = false;
        GD.Print("[EditorScreen3D] Ready");
    }

    /// <summary>
    /// Opens a map file in the Map Editor tab.
    /// Called when returning from InMapEditor.
    /// </summary>
    public void OpenMapInEditor(string mapPath)
    {
        if (string.IsNullOrEmpty(mapPath))
        {
            GD.PrintErr("[EditorScreen3D] Cannot open map: path is empty");
            return;
        }

        // Switch to the Map tab (which is the last tab, index 5)
        if (_tabContainer != null && _mapEditorOverlay != null)
        {
            // Hide normal content, show map editor
            if (_mainContentArea != null) _mainContentArea.Visible = false;
            _mapEditorOverlay.Visible = true;
            _currentTab = 5; // Map tab

            // Load the map in the map editor
            _mapEditorOverlay.RefreshFromFile(mapPath);

            GD.Print($"[EditorScreen3D] Opened map in editor: {mapPath}");
        }
        else
        {
            GD.PrintErr("[EditorScreen3D] Map editor tab not found");
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        // Auto-rotate preview (only if not dragging)
        if (_autoRotate && _previewObject != null && !_isDragging)
        {
            _cameraYaw += (float)delta * 0.5f;
            UpdateCameraPosition();
        }

        // Handle animation cycling
        if (_cycleAnimations && _selectedMonsterId != null)
        {
            _animationTimer += (float)delta;
            if (_animationTimer >= _animationCycleDuration)
            {
                _animationTimer = 0f;
                _currentAnimationIndex = (_currentAnimationIndex + 1) % _monsterAnimations.Length;
                _currentAnimation = _monsterAnimations[_currentAnimationIndex];
                if (_animationDropdown != null)
                {
                    _animationDropdown.Selected = _currentAnimationIndex;
                }

                // Play next animation using AnimationPlayer
                if (_previewAnimPlayer != null && _previewAnimPlayer.HasAnimation(_currentAnimation))
                {
                    _previewAnimPlayer.Stop();
                    _previewAnimPlayer.Play(_currentAnimation);
                }

                UpdateAnimationLabel();
            }
        }

        // AnimationPlayer handles animation playback automatically
        // Only use fallback if no AnimationPlayer
        if (_previewObject != null && _selectedMonsterId != null && _previewAnimPlayer == null)
        {
            _previewAnimTime += (float)delta;
            UpdatePreviewAnimation((float)delta);
        }
    }

    private void BuildUI()
    {
        // Dark background - full coverage
        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = new Color(0.02f, 0.02f, 0.05f, 1f);
        AddChild(bg);

        // Main horizontal split - list on left, preview+details on right
        var mainHBox = new HBoxContainer();
        mainHBox.SetAnchorsPreset(LayoutPreset.FullRect);
        mainHBox.AddThemeConstantOverride("separation", 0);
        AddChild(mainHBox);

        // Left panel - monster/ability/cosmetic list (fixed width)
        CreateLeftPanel(mainHBox);

        // Right side - split into preview (top) and details (bottom)
        var rightVBox = new VBoxContainer();
        rightVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
        rightVBox.AddThemeConstantOverride("separation", 0);
        mainHBox.AddChild(rightVBox);

        // Top bar with title and close button
        CreateTopBar(rightVBox);

        // Main content area - preview on left, details on right (normal editor mode)
        _mainContentArea = new HBoxContainer();
        _mainContentArea.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _mainContentArea.SizeFlagsVertical = SizeFlags.ExpandFill;
        ((HBoxContainer)_mainContentArea).AddThemeConstantOverride("separation", 0);
        rightVBox.AddChild(_mainContentArea);

        // Large 3D preview viewport (takes most of the space)
        CreatePreviewViewport((HBoxContainer)_mainContentArea);

        // Right detail panel
        CreateDetailPanel((HBoxContainer)_mainContentArea);

        // Map editor tab - added directly to rightVBox, hidden by default
        // When shown, it replaces the mainContentArea
        _mapEditorOverlay = new MapEditorTab();
        _mapEditorOverlay.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _mapEditorOverlay.SizeFlagsVertical = SizeFlags.ExpandFill;
        rightVBox.AddChild(_mapEditorOverlay);
        _mapEditorOverlay.Visible = false;
    }

    private void CreateLeftPanel(HBoxContainer parent)
    {
        var leftPanel = new PanelContainer();
        leftPanel.CustomMinimumSize = new Vector2(280, 0);
        leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var leftStyle = new StyleBoxFlat();
        leftStyle.BgColor = new Color(0.05f, 0.05f, 0.08f);
        leftStyle.BorderWidthRight = 2;
        leftStyle.BorderColor = new Color(0.2f, 0.2f, 0.3f);
        leftPanel.AddThemeStyleboxOverride("panel", leftStyle);
        parent.AddChild(leftPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        leftPanel.AddChild(margin);

        _tabContainer = new TabContainer();
        _tabContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        _tabContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tabContainer.AddThemeFontSizeOverride("font_size", 18);
        _tabContainer.TabChanged += OnTabChanged;
        margin.AddChild(_tabContainer);

        // Monsters tab
        var monstersScroll = new ScrollContainer();
        monstersScroll.Name = "Monsters";
        monstersScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        monstersScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _tabContainer.AddChild(monstersScroll);

        var monstersVBox = new VBoxContainer();
        monstersVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        monstersVBox.AddThemeConstantOverride("separation", 4);
        monstersScroll.AddChild(monstersVBox);

        // Regular monsters (original + DCC)
        string[] monsterTypes = {
            // Original monsters
            "goblin", "goblin_shaman", "goblin_thrower", "slime", "eye", "mushroom", "spider", "lizard", "skeleton", "wolf", "badlama", "bat", "dragon",
            // DCC monsters
            "crawler_killer", "shadow_stalker", "flesh_golem", "plague_bearer", "living_armor",
            "camera_drone", "shock_drone", "advertiser_bot", "clockwork_spider",
            "lava_elemental", "ice_wraith", "gelatinous_cube", "void_spawn", "mimic", "dungeon_rat"
        };
        // Bosses (original + DCC)
        string[] bossTypes = {
            "skeleton_lord", "dragon_king", "spider_queen",
            "the_butcher", "mordecai_the_traitor", "princess_donut", "mongo_the_destroyer", "zev_the_loot_goblin"
        };
        foreach (var type in monsterTypes)
        {
            var btn = CreateListButton(type.Capitalize());
            btn.Pressed += () => SelectMonster(type);
            monstersVBox.AddChild(btn);
            _monsterButtons[type] = btn;
        }

        // Boss separator
        var bossLabel = new Label();
        bossLabel.Text = "--- BOSSES ---";
        bossLabel.HorizontalAlignment = HorizontalAlignment.Center;
        bossLabel.AddThemeFontSizeOverride("font_size", 12);
        bossLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.3f));
        monstersVBox.AddChild(bossLabel);

        foreach (var type in bossTypes)
        {
            var displayName = type.Replace("_", " ").Capitalize();
            var btn = CreateListButton(displayName);
            btn.Pressed += () => SelectMonster(type);
            monstersVBox.AddChild(btn);
            _monsterButtons[type] = btn;
        }

        // Abilities tab
        var abilitiesScroll = new ScrollContainer();
        abilitiesScroll.Name = "Abilities";
        abilitiesScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        abilitiesScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _tabContainer.AddChild(abilitiesScroll);

        var abilitiesVBox = new VBoxContainer();
        abilitiesVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        abilitiesVBox.AddThemeConstantOverride("separation", 4);
        abilitiesScroll.AddChild(abilitiesVBox);

        string[] abilities = { "fireball", "chain_lightning", "soul_leech", "protective_shell",
                              "gravity_well", "timestop_bubble", "infernal_ground", "banshees_wail",
                              "berserk", "mirror_image", "dead_mans_rally", "engine_of_tomorrow" };
        foreach (var ability in abilities)
        {
            var displayName = ability.Replace("_", " ").Capitalize();
            var btn = CreateListButton(displayName);
            btn.Pressed += () => SelectAbility(ability);
            abilitiesVBox.AddChild(btn);
            _abilityButtons[ability] = btn;
        }

        // Cosmetics tab
        var cosmeticsScroll = new ScrollContainer();
        cosmeticsScroll.Name = "Props";
        cosmeticsScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        cosmeticsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _tabContainer.AddChild(cosmeticsScroll);

        var cosmeticsVBox = new VBoxContainer();
        cosmeticsVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cosmeticsVBox.AddThemeConstantOverride("separation", 4);
        cosmeticsScroll.AddChild(cosmeticsVBox);

        string[] cosmetics = {
            // Original props
            "barrel", "crate", "pot", "chest", "torch", "pillar", "statue", "crystal",
            "cauldron", "bookshelf", "table", "chair", "skull_pile", "campfire",
            // Dungeon ambiance props
            "bone_pile", "coiled_chains", "moss_patch", "water_puddle", "treasure_chest",
            "broken_barrel", "altar_stone", "glowing_mushrooms", "blood_pool", "spike_trap",
            "discarded_sword", "shattered_potion", "ancient_scroll", "brazier_fire", "manacles",
            "cracked_tiles", "rubble_heap", "thorny_vines", "engraved_rune", "abandoned_campfire",
            "rat_nest", "broken_pottery", "scattered_coins", "forgotten_shield", "moldy_bread",
            // Glowing fungi and lichen (ambient light sources)
            "cyan_lichen", "purple_lichen", "green_lichen",
            "orange_fungus", "blue_fungus", "pink_fungus",
            "ceiling_tendrils", "floor_spores", "bioluminescent_veins"
        };
        foreach (var cosmetic in cosmetics)
        {
            var btn = CreateListButton(cosmetic.Replace("_", " ").Capitalize());
            btn.Pressed += () => SelectCosmetic(cosmetic);
            cosmeticsVBox.AddChild(btn);
            _cosmeticButtons[cosmetic] = btn;
        }

        // NPCs tab
        var npcsScroll = new ScrollContainer();
        npcsScroll.Name = "NPCs";
        npcsScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        npcsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _tabContainer.AddChild(npcsScroll);

        var npcsVBox = new VBoxContainer();
        npcsVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        npcsVBox.AddThemeConstantOverride("separation", 4);
        npcsScroll.AddChild(npcsVBox);

        string[] npcs = { "steve" };
        foreach (var npc in npcs)
        {
            var displayName = npc switch
            {
                "steve" => "Steve the Chihuahua",
                _ => npc.Replace("_", " ").Capitalize()
            };
            var btn = CreateListButton(displayName);
            btn.Pressed += () => SelectNpc(npc);
            npcsVBox.AddChild(btn);
            _npcButtons[npc] = btn;
        }

        // Equipment tab (combined Weapons + Shields)
        var equipmentScroll = new ScrollContainer();
        equipmentScroll.Name = "Equipment";
        equipmentScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        equipmentScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _tabContainer.AddChild(equipmentScroll);

        var equipmentVBox = new VBoxContainer();
        equipmentVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        equipmentVBox.AddThemeConstantOverride("separation", 4);
        equipmentScroll.AddChild(equipmentVBox);

        // --- Shields Section ---
        var shieldsHeader = new Label();
        shieldsHeader.Text = "Shields";
        shieldsHeader.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1.0f));
        shieldsHeader.AddThemeFontSizeOverride("font_size", 14);
        equipmentVBox.AddChild(shieldsHeader);

        // Get all shield types from the factory (alphabetized)
        var shieldTypes = new List<(ShieldFactory.ShieldType type, string name)>();
        foreach (var shieldType in ShieldFactory.GetAllTypes())
        {
            var shieldData = ShieldFactory.GetShieldData(shieldType);
            shieldTypes.Add((shieldType, shieldData.Name));
        }
        shieldTypes.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        foreach (var (shieldType, shieldName) in shieldTypes)
        {
            var btn = CreateListButton(shieldName);
            string capturedName = shieldName;
            var capturedType = shieldType;
            btn.Pressed += () => SelectShield(capturedName, capturedType);
            equipmentVBox.AddChild(btn);
            _shieldButtons[capturedName] = btn;
        }

        // Spacer between sections
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 12);
        equipmentVBox.AddChild(spacer);

        // --- Weapons Section ---
        var weaponsHeader = new Label();
        weaponsHeader.Text = "Weapons";
        weaponsHeader.AddThemeColorOverride("font_color", new Color(1.0f, 0.6f, 0.4f));
        weaponsHeader.AddThemeFontSizeOverride("font_size", 14);
        equipmentVBox.AddChild(weaponsHeader);

        // Get all weapon types from the factory (alphabetized)
        var weaponTypes = WeaponFactory.GetWeaponTypes().ToList();
        weaponTypes.Sort((a, b) => string.Compare(a, b, StringComparison.Ordinal));

        foreach (var weaponName in weaponTypes)
        {
            var btn = CreateListButton(weaponName);
            string capturedName = weaponName;
            btn.Pressed += () => SelectWeapon(capturedName);
            equipmentVBox.AddChild(btn);
            _weaponButtons[weaponName] = btn;
        }

        // Maps tab - just a placeholder label in the tab container
        // The actual map editor is a full-screen overlay
        var mapsPlaceholder = new Control();
        mapsPlaceholder.Name = "Maps";
        _tabContainer.AddChild(mapsPlaceholder);
    }

    private void CreateTopBar(VBoxContainer parent)
    {
        var topBar = new PanelContainer();
        topBar.CustomMinimumSize = new Vector2(0, 60);
        topBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var topStyle = new StyleBoxFlat();
        topStyle.BgColor = new Color(0.03f, 0.03f, 0.06f);
        topStyle.BorderWidthBottom = 2;
        topStyle.BorderColor = new Color(0.2f, 0.2f, 0.3f);
        topBar.AddThemeStyleboxOverride("panel", topStyle);
        parent.AddChild(topBar);

        var topMargin = new MarginContainer();
        topMargin.AddThemeConstantOverride("margin_left", 20);
        topMargin.AddThemeConstantOverride("margin_right", 20);
        topMargin.AddThemeConstantOverride("margin_top", 10);
        topMargin.AddThemeConstantOverride("margin_bottom", 10);
        topBar.AddChild(topMargin);

        var topHBox = new HBoxContainer();
        topHBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topMargin.AddChild(topHBox);

        _titleLabel = new Label();
        _titleLabel.Text = "MONSTER EDITOR";
        _titleLabel.AddThemeFontSizeOverride("font_size", 28);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        topHBox.AddChild(_titleLabel);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topHBox.AddChild(spacer);

        _selectedItemLabel = new Label();
        _selectedItemLabel.Text = "Select an item";
        _selectedItemLabel.AddThemeFontSizeOverride("font_size", 20);
        _selectedItemLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
        topHBox.AddChild(_selectedItemLabel);

        var spacer2 = new Control();
        spacer2.CustomMinimumSize = new Vector2(30, 0);
        topHBox.AddChild(spacer2);

        // Animation controls container (visible only on Monsters tab)
        _animationControlsContainer = new HBoxContainer();
        topHBox.AddChild(_animationControlsContainer);

        // Auto-rotate toggle
        _autoRotateCheckBox = new CheckBox();
        _autoRotateCheckBox.Text = "Auto-Rotate";
        _autoRotateCheckBox.ButtonPressed = _autoRotate;
        _autoRotateCheckBox.AddThemeFontSizeOverride("font_size", 16);
        _autoRotateCheckBox.Toggled += (pressed) => _autoRotate = pressed;
        _animationControlsContainer.AddChild(_autoRotateCheckBox);

        var spacer3 = new Control();
        spacer3.CustomMinimumSize = new Vector2(15, 0);
        _animationControlsContainer.AddChild(spacer3);

        // Animation cycling toggle
        _cycleAnimationsCheckBox = new CheckBox();
        _cycleAnimationsCheckBox.Text = "Cycle Animations";
        _cycleAnimationsCheckBox.ButtonPressed = _cycleAnimations;
        _cycleAnimationsCheckBox.AddThemeFontSizeOverride("font_size", 16);
        _cycleAnimationsCheckBox.Toggled += OnCycleAnimationsToggled;
        _animationControlsContainer.AddChild(_cycleAnimationsCheckBox);

        var spacer3b = new Control();
        spacer3b.CustomMinimumSize = new Vector2(15, 0);
        _animationControlsContainer.AddChild(spacer3b);

        // Animation dropdown (18 animations: 6 types × 3 variants)
        _animationDropdown = new OptionButton();
        _animationDropdown.CustomMinimumSize = new Vector2(140, 35);
        _animationDropdown.AddThemeFontSizeOverride("font_size", 14);
        foreach (var anim in _monsterAnimations)
        {
            // Format: "Idle 0", "Idle 1", etc.
            string[] parts = anim.Split('_');
            string displayName = parts.Length == 2 ? $"{parts[0].Capitalize()} {parts[1]}" : anim.Capitalize();
            _animationDropdown.AddItem(displayName);
        }
        _animationDropdown.Selected = 0;
        _animationDropdown.ItemSelected += OnAnimationSelected;
        _animationControlsContainer.AddChild(_animationDropdown);

        var spacer3c = new Control();
        spacer3c.CustomMinimumSize = new Vector2(10, 0);
        _animationControlsContainer.AddChild(spacer3c);

        // Animation label
        _animationLabel = new Label();
        _animationLabel.Text = "[Idle]";
        _animationLabel.AddThemeFontSizeOverride("font_size", 16);
        _animationLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
        _animationControlsContainer.AddChild(_animationLabel);

        var spacer4 = new Control();
        spacer4.CustomMinimumSize = new Vector2(20, 0);
        _animationControlsContainer.AddChild(spacer4);

        // Status effect container (visible only on Monsters tab)
        _statusEffectContainer = new HBoxContainer();
        topHBox.AddChild(_statusEffectContainer);

        // Status effect label
        var statusLabel = new Label();
        statusLabel.Text = "Status:";
        statusLabel.AddThemeFontSizeOverride("font_size", 16);
        _statusEffectContainer.AddChild(statusLabel);

        var spacer4b = new Control();
        spacer4b.CustomMinimumSize = new Vector2(8, 0);
        _statusEffectContainer.AddChild(spacer4b);

        // Status effect dropdown
        _statusEffectDropdown = new OptionButton();
        _statusEffectDropdown.CustomMinimumSize = new Vector2(130, 35);
        _statusEffectDropdown.AddThemeFontSizeOverride("font_size", 14);
        _statusEffectDropdown.AddItem("None");
        foreach (var effectType in StatusEffectManager.GetAllTypes())
        {
            _statusEffectDropdown.AddItem(StatusEffectManager.GetDisplayName(effectType));
        }
        _statusEffectDropdown.Selected = 0;
        _statusEffectDropdown.ItemSelected += OnStatusEffectSelected;
        _statusEffectContainer.AddChild(_statusEffectDropdown);

        var spacer5 = new Control();
        spacer5.CustomMinimumSize = new Vector2(20, 0);
        topHBox.AddChild(spacer5);

        _closeButton = new Button();
        _closeButton.Text = "X Close";
        _closeButton.CustomMinimumSize = new Vector2(100, 40);
        _closeButton.AddThemeFontSizeOverride("font_size", 18);

        var closeStyle = new StyleBoxFlat();
        closeStyle.BgColor = new Color(0.6f, 0.2f, 0.2f);
        closeStyle.SetCornerRadiusAll(4);
        _closeButton.AddThemeStyleboxOverride("normal", closeStyle);

        var closeHover = new StyleBoxFlat();
        closeHover.BgColor = new Color(0.8f, 0.3f, 0.3f);
        closeHover.SetCornerRadiusAll(4);
        _closeButton.AddThemeStyleboxOverride("hover", closeHover);

        _closeButton.Pressed += Hide;
        topHBox.AddChild(_closeButton);
    }

    private Button CreateListButton(string text)
    {
        var btn = new Button();
        btn.Text = text;
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btn.CustomMinimumSize = new Vector2(0, 40);
        btn.Alignment = HorizontalAlignment.Left;
        btn.AddThemeFontSizeOverride("font_size", 16);

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.08f, 0.08f, 0.12f);
        normalStyle.SetCornerRadiusAll(4);
        normalStyle.ContentMarginLeft = 12;
        normalStyle.ContentMarginTop = 6;
        normalStyle.ContentMarginBottom = 6;
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.15f, 0.15f, 0.22f);
        hoverStyle.SetCornerRadiusAll(4);
        hoverStyle.ContentMarginLeft = 12;
        hoverStyle.ContentMarginTop = 6;
        hoverStyle.ContentMarginBottom = 6;
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        return btn;
    }

    private void CreatePreviewViewport(HBoxContainer parent)
    {
        var viewportPanel = new PanelContainer();
        viewportPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        viewportPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        viewportPanel.SizeFlagsStretchRatio = 2f;

        var viewportStyle = new StyleBoxFlat();
        viewportStyle.BgColor = new Color(0.01f, 0.01f, 0.02f);
        viewportPanel.AddThemeStyleboxOverride("panel", viewportStyle);
        parent.AddChild(viewportPanel);

        var viewportVBox = new VBoxContainer();
        viewportVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        viewportVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
        viewportPanel.AddChild(viewportVBox);

        _viewportContainer = new SubViewportContainer();
        _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        _viewportContainer.Stretch = true;
        _viewportContainer.MouseFilter = MouseFilterEnum.Stop;
        viewportVBox.AddChild(_viewportContainer);

        _previewViewport = new SubViewport();
        _previewViewport.Size = new Vector2I(1200, 900);
        _previewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _previewViewport.TransparentBg = false;
        _previewViewport.OwnWorld3D = true;  // IMPORTANT: Isolate from main game world
        _viewportContainer.AddChild(_previewViewport);

        // Create 3D preview scene
        _previewScene = new Node3D();
        _previewScene.Name = "PreviewScene";
        _previewViewport.AddChild(_previewScene);

        // Add camera directly to scene (orbiting calculated in UpdateCameraPosition)
        _previewCamera = new Camera3D();
        _previewCamera.Fov = 45;
        _previewCamera.Near = 0.1f;
        _previewCamera.Far = 100f;
        _previewScene.AddChild(_previewCamera);

        UpdateCameraPosition();

        // Add lighting - use rotation for directional lights, not LookAt
        var light = new DirectionalLight3D();
        light.RotationDegrees = new Vector3(-45, 45, 0);  // Main light from upper-front-right
        light.LightEnergy = 1.0f;
        light.ShadowEnabled = false;  // Disable shadows for cleaner preview
        _previewScene.AddChild(light);

        var fillLight = new DirectionalLight3D();
        fillLight.RotationDegrees = new Vector3(-30, -135, 0);  // Fill from upper-back-left
        fillLight.LightEnergy = 0.5f;
        _previewScene.AddChild(fillLight);

        // Add ambient/environment light for overall brightness
        var env = new WorldEnvironment();
        var envRes = new Godot.Environment();
        envRes.BackgroundMode = Godot.Environment.BGMode.Color;
        envRes.BackgroundColor = new Color(0.05f, 0.05f, 0.08f);
        envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        envRes.AmbientLightColor = new Color(0.3f, 0.3f, 0.35f);
        envRes.AmbientLightEnergy = 0.5f;
        env.Environment = envRes;
        _previewScene.AddChild(env);

        // Hint label at bottom of viewport
        _viewportHintLabel = new Label();
        _viewportHintLabel.Text = "Drag to rotate | Scroll to zoom | Right-click to reset";
        _viewportHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _viewportHintLabel.AddThemeFontSizeOverride("font_size", 14);
        _viewportHintLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
        _viewportHintLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        viewportVBox.AddChild(_viewportHintLabel);
    }

    private void UpdateCameraPosition()
    {
        if (_previewCamera == null) return;

        // Camera orbits around the target at (_cameraTargetY) height
        // _cameraPitch is the elevation angle (0 = horizontal, positive = looking down from above)
        // _cameraYaw is the horizontal rotation angle

        float horizontalDist = _cameraDistance * Mathf.Cos(_cameraPitch);
        float verticalOffset = _cameraDistance * Mathf.Sin(_cameraPitch);

        float x = horizontalDist * Mathf.Sin(_cameraYaw);
        float y = _cameraTargetY + verticalOffset;
        float z = horizontalDist * Mathf.Cos(_cameraYaw);

        _previewCamera.Position = new Vector3(x, y, z);
        _previewCamera.LookAt(new Vector3(0, _cameraTargetY, 0));
    }

    private void CreateDetailPanel(HBoxContainer parent)
    {
        var detailPanel = new PanelContainer();
        detailPanel.CustomMinimumSize = new Vector2(320, 0);
        detailPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var detailStyle = new StyleBoxFlat();
        detailStyle.BgColor = new Color(0.04f, 0.04f, 0.07f);
        detailStyle.BorderWidthLeft = 2;
        detailStyle.BorderColor = new Color(0.2f, 0.2f, 0.3f);
        detailPanel.AddThemeStyleboxOverride("panel", detailStyle);
        parent.AddChild(detailPanel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        detailPanel.AddChild(scroll);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 15);
        margin.AddThemeConstantOverride("margin_right", 15);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 15);
        margin.AddChild(vbox);

        // Section: Monster Stats
        _monsterStatsSection = CreateSection("Monster Stats");
        vbox.AddChild(_monsterStatsSection);

        _monsterHealthSpinBox = CreateAttributeRow(_monsterStatsSection, "Max Health", 10, 500, 75);
        _monsterDamageSpinBox = CreateAttributeRow(_monsterStatsSection, "Damage", 1, 100, 10);
        _monsterSpeedSpinBox = CreateAttributeRow(_monsterStatsSection, "Move Speed", 0.5, 10, 3);
        _monsterAggroSpinBox = CreateAttributeRow(_monsterStatsSection, "Aggro Range", 5, 50, 15);

        var skinRow = new HBoxContainer();
        skinRow.AddThemeConstantOverride("separation", 10);
        var skinLabel = new Label { Text = "Skin Color" };
        skinLabel.AddThemeFontSizeOverride("font_size", 14);
        skinLabel.CustomMinimumSize = new Vector2(100, 0);
        skinRow.AddChild(skinLabel);
        _monsterSkinColorPicker = new ColorPickerButton();
        _monsterSkinColorPicker.Color = new Color(0.45f, 0.55f, 0.35f);
        _monsterSkinColorPicker.CustomMinimumSize = new Vector2(80, 30);
        _monsterSkinColorPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _monsterSkinColorPicker.ColorChanged += OnMonsterColorChanged;
        skinRow.AddChild(_monsterSkinColorPicker);
        _monsterStatsSection.AddChild(skinRow);

        // Apply/Save button for monster changes
        var saveBtn = new Button();
        saveBtn.Text = "Apply Changes";
        saveBtn.CustomMinimumSize = new Vector2(0, 35);
        saveBtn.AddThemeFontSizeOverride("font_size", 14);
        var saveBtnStyle = new StyleBoxFlat();
        saveBtnStyle.BgColor = new Color(0.3f, 0.5f, 0.3f);
        saveBtnStyle.SetCornerRadiusAll(4);
        saveBtn.AddThemeStyleboxOverride("normal", saveBtnStyle);
        var saveBtnHover = new StyleBoxFlat();
        saveBtnHover.BgColor = new Color(0.4f, 0.6f, 0.4f);
        saveBtnHover.SetCornerRadiusAll(4);
        saveBtn.AddThemeStyleboxOverride("hover", saveBtnHover);
        saveBtn.Pressed += OnApplyChanges;
        _monsterStatsSection.AddChild(saveBtn);

        // Section: Monster Sounds
        _soundSection = CreateSection("Monster Sounds");
        vbox.AddChild(_soundSection);

        // Sound status label
        _soundStatusLabel = new Label();
        _soundStatusLabel.Text = "Select a monster to preview sounds";
        _soundStatusLabel.AddThemeFontSizeOverride("font_size", 12);
        _soundStatusLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
        _soundSection.AddChild(_soundStatusLabel);

        // Sound rows for each action (with play and browse buttons)
        string[] soundActions = MonsterSounds.SoundActions;
        foreach (var action in soundActions)
        {
            var row = CreateSoundRow(action);
            _soundSection.AddChild(row);
        }

        // Sound folder browser section
        var folderRow = new HBoxContainer();
        folderRow.AddThemeConstantOverride("separation", 5);

        var folderLabel = new Label { Text = "Sound Folder:" };
        folderLabel.AddThemeFontSizeOverride("font_size", 12);
        folderLabel.CustomMinimumSize = new Vector2(80, 0);
        folderRow.AddChild(folderLabel);

        _soundFolderLabel = new Label { Text = "Assets/Audio/Sounds/Goblin" };
        _soundFolderLabel.AddThemeFontSizeOverride("font_size", 11);
        _soundFolderLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.7f));
        _soundFolderLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        folderRow.AddChild(_soundFolderLabel);

        var browseFolderBtn = new Button { Text = "Browse..." };
        browseFolderBtn.CustomMinimumSize = new Vector2(70, 28);
        browseFolderBtn.AddThemeFontSizeOverride("font_size", 11);
        browseFolderBtn.Pressed += OnBrowseSoundFolder;
        folderRow.AddChild(browseFolderBtn);

        _soundSection.AddChild(folderRow);

        // Available sounds list (shows files in selected folder)
        _availableSoundsContainer = new VBoxContainer();
        _availableSoundsContainer.AddThemeConstantOverride("separation", 2);
        _soundSection.AddChild(_availableSoundsContainer);
        RefreshAvailableSounds();

        // Play all sounds button
        var playAllBtn = new Button();
        playAllBtn.Text = "Play All Sounds";
        playAllBtn.CustomMinimumSize = new Vector2(0, 35);
        playAllBtn.AddThemeFontSizeOverride("font_size", 14);

        var playAllStyle = new StyleBoxFlat();
        playAllStyle.BgColor = new Color(0.3f, 0.4f, 0.5f);
        playAllStyle.SetCornerRadiusAll(4);
        playAllBtn.AddThemeStyleboxOverride("normal", playAllStyle);

        var playAllHover = new StyleBoxFlat();
        playAllHover.BgColor = new Color(0.4f, 0.5f, 0.6f);
        playAllHover.SetCornerRadiusAll(4);
        playAllBtn.AddThemeStyleboxOverride("hover", playAllHover);

        playAllBtn.Pressed += OnPlayAllSounds;
        _soundSection.AddChild(playAllBtn);

        // Section: Ability Stats
        _abilityStatsSection = CreateSection("Ability Stats");
        vbox.AddChild(_abilityStatsSection);

        _abilityCooldownSpinBox = CreateAttributeRow(_abilityStatsSection, "Cooldown (s)", 0.5, 60, 5);
        _abilityDamageSpinBox = CreateAttributeRow(_abilityStatsSection, "Damage", 0, 500, 50);
        _abilityRadiusSpinBox = CreateAttributeRow(_abilityStatsSection, "Radius", 0.5, 20, 3);
        _abilityDurationSpinBox = CreateAttributeRow(_abilityStatsSection, "Duration (s)", 0, 30, 5);
        _abilityManaCostSpinBox = CreateAttributeRow(_abilityStatsSection, "Mana Cost", 0, 100, 20);

        // Section: Cosmetic Props
        _cosmeticPropsSection = CreateSection("Cosmetic Props");
        vbox.AddChild(_cosmeticPropsSection);

        _cosmeticScaleSpinBox = CreateAttributeRow(_cosmeticPropsSection, "Scale", 0.1, 5, 1);

        var lightRow = new HBoxContainer();
        _cosmeticHasLightCheckBox = new CheckBox { Text = "Has Light" };
        _cosmeticHasLightCheckBox.AddThemeFontSizeOverride("font_size", 14);
        lightRow.AddChild(_cosmeticHasLightCheckBox);
        _cosmeticPropsSection.AddChild(lightRow);

        _cosmeticLightRadiusSpinBox = CreateAttributeRow(_cosmeticPropsSection, "Light Radius", 1, 20, 8);

        var lightColorRow = new HBoxContainer();
        lightColorRow.AddThemeConstantOverride("separation", 10);
        var lightColorLabel = new Label { Text = "Light Color" };
        lightColorLabel.AddThemeFontSizeOverride("font_size", 14);
        lightColorLabel.CustomMinimumSize = new Vector2(100, 0);
        lightColorRow.AddChild(lightColorLabel);
        _cosmeticLightColorPicker = new ColorPickerButton();
        _cosmeticLightColorPicker.Color = new Color(1f, 0.7f, 0.4f);
        _cosmeticLightColorPicker.CustomMinimumSize = new Vector2(80, 30);
        _cosmeticLightColorPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lightColorRow.AddChild(_cosmeticLightColorPicker);
        _cosmeticPropsSection.AddChild(lightColorRow);

        // Section: Weapon Stats
        _weaponStatsSection = CreateSection("Weapon Stats");
        vbox.AddChild(_weaponStatsSection);

        // Weapon info labels (read-only display)
        _weaponDamageLabel = CreateInfoRow(_weaponStatsSection, "Damage Bonus");
        _weaponSpeedLabel = CreateInfoRow(_weaponStatsSection, "Speed Modifier");
        _weaponRangeLabel = CreateInfoRow(_weaponStatsSection, "Attack Range");
        _weaponHandedLabel = CreateInfoRow(_weaponStatsSection, "Grip Type");

        // Attach to Monster dropdown
        var attachRow = new HBoxContainer();
        attachRow.AddThemeConstantOverride("separation", 10);
        var attachLabel = new Label { Text = "Attach To" };
        attachLabel.AddThemeFontSizeOverride("font_size", 14);
        attachLabel.CustomMinimumSize = new Vector2(100, 0);
        attachRow.AddChild(attachLabel);

        _attachMonsterDropdown = new OptionButton();
        _attachMonsterDropdown.CustomMinimumSize = new Vector2(140, 30);
        _attachMonsterDropdown.AddThemeFontSizeOverride("font_size", 12);
        _attachMonsterDropdown.AddItem("(None)");
        _attachMonsterDropdown.AddItem("Goblin");
        _attachMonsterDropdown.AddItem("Skeleton");
        _attachMonsterDropdown.AddItem("Goblin Warlord");
        _attachMonsterDropdown.AddItem("Goblin Shaman");
        _attachMonsterDropdown.AddItem("Goblin Thrower");
        _attachMonsterDropdown.Selected = 0;
        _attachMonsterDropdown.ItemSelected += OnAttachMonsterSelected;
        _attachMonsterDropdown.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        attachRow.AddChild(_attachMonsterDropdown);
        _weaponStatsSection.AddChild(attachRow);

        // Section: NPC Stats
        _npcStatsSection = CreateSection("NPC Stats");
        vbox.AddChild(_npcStatsSection);

        _npcScaleSpinBox = CreateAttributeRow(_npcStatsSection, "Scale", 0.5, 3, 1);

        // GLB Model toggle - styled button that acts as toggle
        var glbRow = new HBoxContainer();
        glbRow.AddThemeConstantOverride("separation", 10);
        var glbLabel = new Label { Text = "Model Type" };
        glbLabel.AddThemeFontSizeOverride("font_size", 14);
        glbLabel.CustomMinimumSize = new Vector2(100, 0);
        glbRow.AddChild(glbLabel);

        _npcUseGlbCheckbox = new CheckBox();
        _npcUseGlbCheckbox.Text = Pet.Steve3D.UseGlbModel ? "GLB Model" : "Procedural";
        _npcUseGlbCheckbox.ButtonPressed = Pet.Steve3D.UseGlbModel;
        _npcUseGlbCheckbox.CustomMinimumSize = new Vector2(120, 30);
        _npcUseGlbCheckbox.AddThemeFontSizeOverride("font_size", 14);
        _npcUseGlbCheckbox.AddThemeColorOverride("font_color", Colors.White);
        _npcUseGlbCheckbox.AddThemeColorOverride("font_pressed_color", new Color(0.5f, 1f, 0.5f));
        _npcUseGlbCheckbox.AddThemeColorOverride("font_hover_color", new Color(0.8f, 0.9f, 1f));
        // Style the checkbox background
        var checkboxStyle = new StyleBoxFlat();
        checkboxStyle.BgColor = new Color(0.15f, 0.15f, 0.2f);
        checkboxStyle.SetCornerRadiusAll(4);
        checkboxStyle.ContentMarginLeft = 8;
        checkboxStyle.ContentMarginRight = 8;
        _npcUseGlbCheckbox.AddThemeStyleboxOverride("normal", checkboxStyle);
        var checkboxHover = new StyleBoxFlat();
        checkboxHover.BgColor = new Color(0.2f, 0.25f, 0.35f);
        checkboxHover.SetCornerRadiusAll(4);
        checkboxHover.ContentMarginLeft = 8;
        checkboxHover.ContentMarginRight = 8;
        _npcUseGlbCheckbox.AddThemeStyleboxOverride("hover", checkboxHover);
        var checkboxPressed = new StyleBoxFlat();
        checkboxPressed.BgColor = new Color(0.2f, 0.4f, 0.3f);
        checkboxPressed.SetCornerRadiusAll(4);
        checkboxPressed.ContentMarginLeft = 8;
        checkboxPressed.ContentMarginRight = 8;
        _npcUseGlbCheckbox.AddThemeStyleboxOverride("pressed", checkboxPressed);
        _npcUseGlbCheckbox.Toggled += OnNpcGlbToggled;
        glbRow.AddChild(_npcUseGlbCheckbox);
        _npcStatsSection.AddChild(glbRow);

        // GLB path info
        var glbPathRow = new HBoxContainer();
        glbPathRow.AddThemeConstantOverride("separation", 10);
        var pathLabel = new Label { Text = "GLB Path:" };
        pathLabel.AddThemeFontSizeOverride("font_size", 12);
        pathLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        glbPathRow.AddChild(pathLabel);
        _npcGlbPathLabel = new Label { Text = Pet.Steve3D.GlbModelPath };
        _npcGlbPathLabel.AddThemeFontSizeOverride("font_size", 12);
        _npcGlbPathLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.9f));
        glbPathRow.AddChild(_npcGlbPathLabel);
        _npcStatsSection.AddChild(glbPathRow);

        var npcSkinRow = new HBoxContainer();
        npcSkinRow.AddThemeConstantOverride("separation", 10);
        var npcSkinLabel = new Label { Text = "Skin Color" };
        npcSkinLabel.AddThemeFontSizeOverride("font_size", 14);
        npcSkinLabel.CustomMinimumSize = new Vector2(100, 0);
        npcSkinRow.AddChild(npcSkinLabel);
        _npcSkinColorPicker = new ColorPickerButton();
        _npcSkinColorPicker.Color = new Color(0.85f, 0.75f, 0.6f);
        _npcSkinColorPicker.CustomMinimumSize = new Vector2(80, 30);
        _npcSkinColorPicker.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        npcSkinRow.AddChild(_npcSkinColorPicker);
        _npcStatsSection.AddChild(npcSkinRow);

        // NPC Apply button
        var npcApplyBtn = new Button();
        npcApplyBtn.Text = "Apply Changes";
        npcApplyBtn.CustomMinimumSize = new Vector2(0, 35);
        npcApplyBtn.AddThemeFontSizeOverride("font_size", 14);
        var npcApplyStyle = new StyleBoxFlat();
        npcApplyStyle.BgColor = new Color(0.3f, 0.5f, 0.3f);
        npcApplyStyle.SetCornerRadiusAll(4);
        npcApplyBtn.AddThemeStyleboxOverride("normal", npcApplyStyle);
        var npcApplyHover = new StyleBoxFlat();
        npcApplyHover.BgColor = new Color(0.4f, 0.6f, 0.4f);
        npcApplyHover.SetCornerRadiusAll(4);
        npcApplyBtn.AddThemeStyleboxOverride("hover", npcApplyHover);
        npcApplyBtn.Pressed += OnApplyChanges;
        _npcStatsSection.AddChild(npcApplyBtn);

        // Apply button
        var applyBtn = new Button();
        applyBtn.Text = "Apply Changes";
        applyBtn.CustomMinimumSize = new Vector2(0, 45);
        applyBtn.AddThemeFontSizeOverride("font_size", 16);

        var applyStyle = new StyleBoxFlat();
        applyStyle.BgColor = new Color(0.2f, 0.5f, 0.3f);
        applyStyle.SetCornerRadiusAll(4);
        applyBtn.AddThemeStyleboxOverride("normal", applyStyle);

        var applyHover = new StyleBoxFlat();
        applyHover.BgColor = new Color(0.3f, 0.6f, 0.4f);
        applyHover.SetCornerRadiusAll(4);
        applyBtn.AddThemeStyleboxOverride("hover", applyHover);

        applyBtn.Pressed += OnApplyChanges;
        vbox.AddChild(applyBtn);
    }

    private VBoxContainer CreateSection(string title)
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 8);

        var label = new Label();
        label.Text = title;
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
        section.AddChild(label);

        var separator = new HSeparator();
        section.AddChild(separator);

        return section;
    }

    /// <summary>
    /// Creates a read-only info row with label and value label.
    /// Returns the value label for updating.
    /// </summary>
    private Label CreateInfoRow(VBoxContainer parent, string labelText)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var lbl = new Label { Text = labelText };
        lbl.AddThemeFontSizeOverride("font_size", 14);
        lbl.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(lbl);

        var valueLabel = new Label { Text = "-" };
        valueLabel.AddThemeFontSizeOverride("font_size", 14);
        valueLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 0.7f));
        valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(valueLabel);

        parent.AddChild(row);
        return valueLabel;
    }

    private HBoxContainer CreateSoundRow(string action)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        row.CustomMinimumSize = new Vector2(0, 32);

        // Action label
        var actionLabel = new Label { Text = action };
        actionLabel.CustomMinimumSize = new Vector2(50, 0);
        actionLabel.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(actionLabel);

        // File label (shows current assigned file)
        var fileLabel = new Label { Text = "(none)" };
        fileLabel.AddThemeFontSizeOverride("font_size", 9);
        fileLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.6f));
        fileLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        fileLabel.ClipText = true;
        row.AddChild(fileLabel);
        _soundFileLabels[action.ToLower()] = fileLabel;

        // Color code by sound type
        Color btnColor = action.ToLower() switch
        {
            "idle" => new Color(0.25f, 0.35f, 0.25f),   // Green for idle
            "aggro" => new Color(0.4f, 0.3f, 0.2f),    // Orange for aggro
            "attack" => new Color(0.4f, 0.25f, 0.25f), // Red for attack
            "hit" => new Color(0.35f, 0.25f, 0.35f),   // Purple for hit
            "die" => new Color(0.2f, 0.2f, 0.3f),      // Dark blue for die
            _ => new Color(0.3f, 0.3f, 0.35f)
        };

        // Assign button (assign selected sound from list below)
        string soundAction = action.ToLower();
        var assignBtn = new Button { Text = "←" };
        assignBtn.CustomMinimumSize = new Vector2(28, 26);
        assignBtn.TooltipText = $"Assign selected sound to {action}";
        assignBtn.AddThemeFontSizeOverride("font_size", 12);

        var assignStyle = new StyleBoxFlat();
        assignStyle.BgColor = new Color(0.25f, 0.3f, 0.4f);
        assignStyle.SetCornerRadiusAll(4);
        assignBtn.AddThemeStyleboxOverride("normal", assignStyle);

        var assignHoverStyle = new StyleBoxFlat();
        assignHoverStyle.BgColor = new Color(0.35f, 0.45f, 0.55f);
        assignHoverStyle.SetCornerRadiusAll(4);
        assignBtn.AddThemeStyleboxOverride("hover", assignHoverStyle);

        assignBtn.Pressed += () => OnAssignSound(soundAction);
        row.AddChild(assignBtn);

        // Play button
        var playBtn = new Button { Text = "▶" };
        playBtn.CustomMinimumSize = new Vector2(28, 26);
        playBtn.TooltipText = $"Play {action} sound";

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = btnColor;
        normalStyle.SetCornerRadiusAll(4);
        playBtn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = btnColor.Lightened(0.15f);
        hoverStyle.SetCornerRadiusAll(4);
        playBtn.AddThemeStyleboxOverride("hover", hoverStyle);

        playBtn.Pressed += () => OnPlayMonsterSound(soundAction);
        row.AddChild(playBtn);
        _soundButtons[soundAction] = playBtn;

        return row;
    }

    private string? _selectedSoundFile = null; // Currently selected sound file from the list

    private void OnAssignSound(string action)
    {
        if (string.IsNullOrEmpty(_selectedSoundFile))
        {
            UpdateSoundStatus("Select a sound file first (click in list below)");
            return;
        }

        // Store the assignment
        _soundAssignments[action] = _selectedSoundFile;

        // Update the file label
        if (_soundFileLabels.TryGetValue(action, out var label))
        {
            label.Text = System.IO.Path.GetFileName(_selectedSoundFile);
            label.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 0.5f)); // Bright green when assigned
        }

        UpdateSoundStatus($"Assigned {System.IO.Path.GetFileName(_selectedSoundFile)} to {action}");
        GD.Print($"[EditorScreen3D] Assigned {_selectedSoundFile} to {action} for {_selectedMonsterId}");
    }

    private void OnBrowseSoundFolder()
    {
        // Scan available sound folders
        var dir = DirAccess.Open("res://Assets/Audio/Sounds");
        if (dir == null)
        {
            UpdateSoundStatus("Could not open Assets/Audio/Sounds folder");
            return;
        }

        var folders = new List<string>();
        dir.ListDirBegin();
        string filename = dir.GetNext();
        while (filename != "")
        {
            if (dir.CurrentIsDir() && !filename.StartsWith("."))
            {
                folders.Add(filename);
            }
            filename = dir.GetNext();
        }
        dir.ListDirEnd();

        if (folders.Count == 0)
        {
            UpdateSoundStatus("No sound folders found");
            return;
        }

        // Cycle through folders (simple UI for now)
        int currentIndex = folders.IndexOf(System.IO.Path.GetFileName(_currentSoundFolder));
        currentIndex = (currentIndex + 1) % folders.Count;
        _currentSoundFolder = $"res://Assets/Audio/Sounds/{folders[currentIndex]}";

        if (_soundFolderLabel != null)
        {
            _soundFolderLabel.Text = folders[currentIndex];
        }

        RefreshAvailableSounds();
        UpdateSoundStatus($"Selected folder: {folders[currentIndex]}");
    }

    private void RefreshAvailableSounds()
    {
        if (_availableSoundsContainer == null) return;

        // Clear old entries
        foreach (var child in _availableSoundsContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Scan folder for audio files
        var dir = DirAccess.Open(_currentSoundFolder);
        if (dir == null)
        {
            var noFilesLabel = new Label { Text = "  No folder found" };
            noFilesLabel.AddThemeFontSizeOverride("font_size", 10);
            noFilesLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.4f, 0.4f));
            _availableSoundsContainer.AddChild(noFilesLabel);
            return;
        }

        var files = new List<string>();
        dir.ListDirBegin();
        string filename = dir.GetNext();
        while (filename != "")
        {
            if (!dir.CurrentIsDir())
            {
                string ext = System.IO.Path.GetExtension(filename).ToLower();
                if (ext == ".m4a" || ext == ".wav" || ext == ".ogg" || ext == ".mp3")
                {
                    files.Add(filename);
                }
            }
            filename = dir.GetNext();
        }
        dir.ListDirEnd();

        if (files.Count == 0)
        {
            var noFilesLabel = new Label { Text = "  No audio files found" };
            noFilesLabel.AddThemeFontSizeOverride("font_size", 10);
            noFilesLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.4f, 0.4f));
            _availableSoundsContainer.AddChild(noFilesLabel);
            return;
        }

        // Header with instructions
        var headerLabel = new Label { Text = "Click ▶ to play, click file name to select for assignment:" };
        headerLabel.AddThemeFontSizeOverride("font_size", 10);
        headerLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.7f));
        _availableSoundsContainer.AddChild(headerLabel);

        foreach (var file in files)
        {
            var fileRow = new HBoxContainer();
            fileRow.AddThemeConstantOverride("separation", 5);

            var playBtn = new Button { Text = "▶" };
            playBtn.CustomMinimumSize = new Vector2(24, 20);
            playBtn.AddThemeFontSizeOverride("font_size", 9);
            string fullPath = $"{_currentSoundFolder}/{file}";
            playBtn.Pressed += () => PlaySoundFile(fullPath);
            fileRow.AddChild(playBtn);

            // Make file name a clickable button to select it for assignment
            var fileBtn = new Button { Text = file };
            fileBtn.AddThemeFontSizeOverride("font_size", 9);
            fileBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // Check format and color accordingly
            string ext = System.IO.Path.GetExtension(file).ToLower();
            Color textColor = ext == ".m4a"
                ? new Color(0.7f, 0.4f, 0.4f)  // Red-ish for unsupported M4A
                : new Color(0.75f, 0.85f, 0.75f); // Green-ish for supported formats

            var fileBtnStyle = new StyleBoxFlat();
            fileBtnStyle.BgColor = new Color(0.15f, 0.15f, 0.18f);
            fileBtnStyle.SetCornerRadiusAll(3);
            fileBtn.AddThemeStyleboxOverride("normal", fileBtnStyle);

            var fileBtnHover = new StyleBoxFlat();
            fileBtnHover.BgColor = new Color(0.25f, 0.3f, 0.35f);
            fileBtnHover.SetCornerRadiusAll(3);
            fileBtn.AddThemeStyleboxOverride("hover", fileBtnHover);

            var fileBtnPressed = new StyleBoxFlat();
            fileBtnPressed.BgColor = new Color(0.2f, 0.4f, 0.3f);
            fileBtnPressed.SetCornerRadiusAll(3);
            fileBtn.AddThemeStyleboxOverride("pressed", fileBtnPressed);

            fileBtn.AddThemeColorOverride("font_color", textColor);
            fileBtn.Pressed += () => SelectSoundFile(fullPath);
            fileRow.AddChild(fileBtn);

            _availableSoundsContainer.AddChild(fileRow);
        }
    }

    private void SelectSoundFile(string path)
    {
        _selectedSoundFile = path;
        string filename = System.IO.Path.GetFileName(path);
        string ext = System.IO.Path.GetExtension(path).ToLower();

        if (ext == ".m4a")
        {
            UpdateSoundStatus($"Selected: {filename} (M4A - needs conversion!)");
        }
        else
        {
            UpdateSoundStatus($"Selected: {filename} - use ← to assign");
        }
        GD.Print($"[EditorScreen3D] Selected sound file: {path}");
    }

    private void PlaySoundFile(string path)
    {
        // Try to load and play the sound file
        try
        {
            string ext = System.IO.Path.GetExtension(path).ToLower();
            string filename = System.IO.Path.GetFileName(path);

            // Check if it's an unsupported format
            if (ext == ".m4a")
            {
                UpdateSoundStatus($"M4A not supported! Convert to OGG/WAV/MP3: {filename}");
                GD.Print($"[EditorScreen3D] M4A format not supported by Godot. Convert {filename} to .ogg, .wav, or .mp3");
                return;
            }

            var stream = GD.Load<AudioStream>(path);
            if (stream == null)
            {
                // For non-imported files, show helpful message
                UpdateSoundStatus($"Not imported: {filename} (restart Godot)");
                GD.Print($"[EditorScreen3D] Could not load {path}. File may need to be imported by Godot.");
                return;
            }

            // Use SoundManager to play it
            if (SoundManager3D.Instance != null)
            {
                var player = new AudioStreamPlayer();
                player.Stream = stream;
                player.VolumeDb = -5f;
                SoundManager3D.Instance.AddChild(player);
                player.Play();
                player.Finished += () => player.QueueFree();

                UpdateSoundStatus($"Playing: {filename}");
            }
        }
        catch (System.Exception e)
        {
            UpdateSoundStatus($"Error: {e.Message}");
            GD.Print($"[EditorScreen3D] Error playing sound: {e.Message}");
        }
    }

    private void OnPlayMonsterSound(string action)
    {
        if (_selectedMonsterId == null)
        {
            UpdateSoundStatus("No monster selected");
            return;
        }

        var soundSet = MonsterSounds.GetSoundSet(_selectedMonsterId);
        if (soundSet == null)
        {
            UpdateSoundStatus($"No sounds for {_selectedMonsterId}");
            return;
        }

        var config = MonsterSounds.GetSoundConfig(_selectedMonsterId, action);
        if (config == null || string.IsNullOrEmpty(config.SoundKey))
        {
            UpdateSoundStatus($"No {action} sound defined");
            return;
        }

        // Check if sound exists in library
        if (SoundManager3D.Instance == null)
        {
            UpdateSoundStatus("Sound manager not available");
            return;
        }

        // Play the sound
        SoundManager3D.Instance.PlayMonsterSound(_selectedMonsterId, action);
        UpdateSoundStatus($"Playing: {action} (pitch: {config.PitchBase:F2})");

        GD.Print($"[EditorScreen3D] Playing {_selectedMonsterId} {action} sound");
    }

    private void OnPlayAllSounds()
    {
        if (_selectedMonsterId == null)
        {
            UpdateSoundStatus("No monster selected");
            return;
        }

        // Start playing sounds sequentially with delays
        PlaySoundSequence(0);
    }

    private async void PlaySoundSequence(int index)
    {
        string[] actions = { "idle", "aggro", "attack", "hit", "die" };

        if (index >= actions.Length) return;

        string action = actions[index];
        OnPlayMonsterSound(action);

        // Wait before playing next sound
        await ToSignal(GetTree().CreateTimer(1.2f), SceneTreeTimer.SignalName.Timeout);

        // Play next if we're still visible and on same monster
        if (Visible && _selectedMonsterId != null)
        {
            PlaySoundSequence(index + 1);
        }
    }

    private void UpdateSoundStatus(string status)
    {
        if (_soundStatusLabel != null)
        {
            _soundStatusLabel.Text = status;
        }
    }

    private void UpdateSoundButtonsForMonster(string monsterId)
    {
        var soundSet = MonsterSounds.GetSoundSet(monsterId);

        if (soundSet == null)
        {
            UpdateSoundStatus($"No sound set defined for {monsterId}");
            return;
        }

        UpdateSoundStatus($"Sounds loaded for {monsterId.Replace("_", " ").Capitalize()}");
    }

    private SpinBox CreateAttributeRow(VBoxContainer parent, string label, double min, double max, double defaultValue)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var lbl = new Label { Text = label };
        lbl.AddThemeFontSizeOverride("font_size", 14);
        lbl.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(lbl);

        var spinBox = new SpinBox();
        spinBox.MinValue = min;
        spinBox.MaxValue = max;
        spinBox.Step = min < 1 ? 0.1 : 1;
        spinBox.Value = defaultValue;
        spinBox.CustomMinimumSize = new Vector2(80, 30);
        spinBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(spinBox);

        parent.AddChild(row);
        return spinBox;
    }

    private void OnTabChanged(long tab)
    {
        _currentTab = (int)tab;
        ClearPreview();

        // Show/hide map editor overlay based on tab
        // Tab indices: 0=Monsters, 1=Abilities, 2=Props, 3=NPCs, 4=Equipment, 5=Maps
        bool isMapTab = (tab == 5);

        if (_mapEditorOverlay != null)
            _mapEditorOverlay.Visible = isMapTab;

        if (_mainContentArea != null)
            _mainContentArea.Visible = !isMapTab;

        // Update title and visibility based on active tab
        UpdateTitle();
        UpdateTabVisibility();

        GD.Print($"[EditorScreen3D] Tab changed to {tab}, map editor visible: {isMapTab}");
    }

    private void UpdateTitle()
    {
        if (_titleLabel == null) return;

        // Tab indices: 0=Monsters, 1=Abilities, 2=Props, 3=NPCs, 4=Equipment, 5=Maps
        string title = _currentTab switch
        {
            0 => "MONSTER EDITOR",
            1 => "ABILITY EDITOR",
            2 => "PROPS EDITOR",
            3 => "NPC EDITOR",
            4 => "EQUIPMENT EDITOR",
            5 => GetMapEditorTitle(),
            _ => "EDITOR"
        };

        _titleLabel.Text = title;
    }

    private string GetMapEditorTitle()
    {
        if (_mapEditorOverlay != null)
        {
            // Try to get map name from the MapEditorTab
            string? mapName = _mapEditorOverlay.GetCurrentMapName();
            if (!string.IsNullOrEmpty(mapName))
                return $"MAP EDITOR - {mapName}";
        }
        return "MAP EDITOR";
    }

    /// <summary>
    /// Updates visibility of UI sections based on the active tab.
    /// Tab indices: 0=Monsters, 1=Abilities, 2=Props, 3=NPCs, 4=Equipment, 5=Maps
    /// </summary>
    private void UpdateTabVisibility()
    {
        bool isMonsters = _currentTab == 0;
        bool isAbilities = _currentTab == 1;
        bool isProps = _currentTab == 2;
        bool isNpcs = _currentTab == 3;
        bool isEquipment = _currentTab == 4;

        // Top bar controls - only show on Monsters tab
        if (_animationControlsContainer != null)
            _animationControlsContainer.Visible = isMonsters;
        if (_statusEffectContainer != null)
            _statusEffectContainer.Visible = isMonsters;

        // Detail panel sections - show only the relevant one
        if (_monsterStatsSection != null)
            _monsterStatsSection.Visible = isMonsters;
        if (_soundSection != null)
            _soundSection.Visible = isMonsters;
        if (_abilityStatsSection != null)
            _abilityStatsSection.Visible = isAbilities;
        if (_cosmeticPropsSection != null)
            _cosmeticPropsSection.Visible = isProps;
        if (_npcStatsSection != null)
            _npcStatsSection.Visible = isNpcs;
        if (_weaponStatsSection != null)
            _weaponStatsSection.Visible = isEquipment;

        // Update viewport hint for context
        UpdateViewportHint();
    }

    /// <summary>
    /// Updates the viewport hint text based on active tab and selection state.
    /// </summary>
    private void UpdateViewportHint()
    {
        if (_viewportHintLabel == null) return;

        string baseHint = "Drag to rotate | Scroll to zoom | Right-click to reset";

        // Tab 4 = Equipment (combined Weapons + Shields)
        bool hasEquipmentSelected = _selectedWeaponType != null || _selectedShieldType != null;

        string contextHint = _currentTab switch
        {
            0 => _selectedMonsterId == null ? "Select a monster to preview" : baseHint,
            1 => _selectedAbilityId == null ? "Select an ability to preview" : baseHint,
            2 => _selectedCosmeticType == null ? "Select a prop to preview" : baseHint,
            3 => _selectedNpcId == null ? "Select an NPC to preview" : baseHint,
            4 => !hasEquipmentSelected ? "Select a weapon or shield to preview" : baseHint,
            _ => baseHint
        };

        _viewportHintLabel.Text = contextHint;
    }

    private void SelectMonster(string monsterId)
    {
        _selectedMonsterId = monsterId;
        _selectedAbilityId = null;
        _selectedCosmeticType = null;

        // Update label
        if (_selectedItemLabel != null)
        {
            _selectedItemLabel.Text = $"Selected: {monsterId.Capitalize()}";
        }

        // Update button highlighting
        foreach (var (id, btn) in _monsterButtons)
        {
            var style = new StyleBoxFlat();
            style.BgColor = id == monsterId ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.08f, 0.08f, 0.12f);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", style);
        }

        // Create preview
        UpdatePreviewMonster(monsterId);

        // Update sound controls
        UpdateSoundButtonsForMonster(monsterId);

        // Update viewport hint
        UpdateViewportHint();

        GD.Print($"[EditorScreen3D] Selected monster: {monsterId}");
    }

    private void SelectAbility(string abilityId)
    {
        _selectedAbilityId = abilityId;
        _selectedMonsterId = null;
        _selectedCosmeticType = null;

        // Update label
        if (_selectedItemLabel != null)
        {
            _selectedItemLabel.Text = $"Selected: {abilityId.Replace("_", " ").Capitalize()}";
        }

        // Update button highlighting
        foreach (var (id, btn) in _abilityButtons)
        {
            var style = new StyleBoxFlat();
            style.BgColor = id == abilityId ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.08f, 0.08f, 0.12f);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", style);
        }

        // Load ability stats
        var ability = AbilityManager3D.Instance?.GetAbility(abilityId);
        if (ability != null && _abilityCooldownSpinBox != null)
        {
            _abilityCooldownSpinBox.Value = ability.DefaultCooldown;
        }

        // Create preview
        UpdatePreviewAbility(abilityId);

        // Update viewport hint
        UpdateViewportHint();

        GD.Print($"[EditorScreen3D] Selected ability: {abilityId}");
    }

    private void SelectCosmetic(string cosmeticType)
    {
        _selectedCosmeticType = cosmeticType;
        _selectedMonsterId = null;
        _selectedAbilityId = null;

        // Update label
        if (_selectedItemLabel != null)
        {
            _selectedItemLabel.Text = $"Selected: {cosmeticType.Replace("_", " ").Capitalize()}";
        }

        // Update button highlighting
        foreach (var (id, btn) in _cosmeticButtons)
        {
            var style = new StyleBoxFlat();
            style.BgColor = id == cosmeticType ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.08f, 0.08f, 0.12f);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", style);
        }

        // Create preview
        UpdatePreviewCosmetic(cosmeticType);

        // Update viewport hint
        UpdateViewportHint();

        GD.Print($"[EditorScreen3D] Selected cosmetic: {cosmeticType}");
    }

    private void SelectNpc(string npcId)
    {
        _selectedNpcId = npcId;
        _selectedMonsterId = null;
        _selectedAbilityId = null;
        _selectedCosmeticType = null;

        // Update label
        if (_selectedItemLabel != null)
        {
            string displayName = npcId switch
            {
                "steve" => "Steve the Chihuahua",
                _ => npcId.Replace("_", " ").Capitalize()
            };
            _selectedItemLabel.Text = $"Selected: {displayName}";
        }

        // Update button highlighting
        foreach (var (id, btn) in _npcButtons)
        {
            var style = new StyleBoxFlat();
            style.BgColor = id == npcId ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.08f, 0.08f, 0.12f);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", style);
        }

        // Create preview
        UpdatePreviewNpc(npcId);

        // Update viewport hint
        UpdateViewportHint();

        GD.Print($"[EditorScreen3D] Selected NPC: {npcId}");
    }

    private void SelectWeapon(string weaponName)
    {
        _selectedWeaponType = weaponName;
        _selectedMonsterId = null;
        _selectedAbilityId = null;
        _selectedCosmeticType = null;
        _selectedNpcId = null;

        // Update label
        if (_selectedItemLabel != null)
        {
            _selectedItemLabel.Text = $"Selected: {weaponName}";
        }

        // Update button highlighting
        foreach (var (name, btn) in _weaponButtons)
        {
            var style = new StyleBoxFlat();
            style.BgColor = name == weaponName ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.08f, 0.08f, 0.12f);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", style);
        }

        // Update weapon stats display
        var weaponType = WeaponFactory.GetWeaponTypeFromName(weaponName);
        var weaponData = WeaponFactory.GetWeaponData(weaponType);

        if (_weaponDamageLabel != null) _weaponDamageLabel.Text = $"+{weaponData.DamageBonus:F0}";
        if (_weaponSpeedLabel != null) _weaponSpeedLabel.Text = $"{weaponData.SpeedModifier:F2}x";
        if (_weaponRangeLabel != null) _weaponRangeLabel.Text = $"{weaponData.Range:F1}m";
        if (_weaponHandedLabel != null) _weaponHandedLabel.Text = weaponData.IsTwoHanded ? "Two-Handed" : "One-Handed";

        // Create preview
        UpdatePreviewWeapon(weaponName);

        // Update viewport hint
        UpdateViewportHint();

        GD.Print($"[EditorScreen3D] Selected weapon: {weaponName}");
    }

    private void UpdatePreviewWeapon(string weaponName)
    {
        ClearPreview();

        _previewObject = new Node3D();
        _previewObject.Name = "WeaponPreview";

        var weaponType = WeaponFactory.GetWeaponTypeFromName(weaponName);

        // Check if we should attach to a monster
        if (!string.IsNullOrEmpty(_attachedMonsterType))
        {
            // Create monster mesh first
            var limbs = MonsterMeshFactory.CreateMonsterMesh(_previewObject, _attachedMonsterType);

            // Create and attach weapon
            var weapon = WeaponFactory.CreateWeapon(weaponType);
            WeaponFactory.AttachToHumanoid(weapon, limbs);

            // Store limb references for animation
            _previewLimbNodes = limbs;
            _fallbackLimbNodes.Clear();
            if (limbs.Head != null) _fallbackLimbNodes["head"] = limbs.Head;
            if (limbs.LeftArm != null) _fallbackLimbNodes["leftArm"] = limbs.LeftArm;
            if (limbs.RightArm != null) _fallbackLimbNodes["rightArm"] = limbs.RightArm;
            if (limbs.LeftLeg != null) _fallbackLimbNodes["leftLeg"] = limbs.LeftLeg;
            if (limbs.RightLeg != null) _fallbackLimbNodes["rightLeg"] = limbs.RightLeg;

            // Adjust camera for monster+weapon preview
            _cameraDistance = 4f;
            _cameraTargetY = 0.8f;
        }
        else
        {
            // Standalone weapon preview
            var weapon = WeaponFactory.CreateWeapon(weaponType);
            _previewObject.AddChild(weapon);

            // Position weapon upright in center
            weapon.Position = new Vector3(0, 0.5f, 0);

            // Adjust camera for weapon preview (closer, lower)
            _cameraDistance = 2f;
            _cameraTargetY = 0.5f;
        }

        _previewScene?.AddChild(_previewObject);
        UpdateCameraPosition();
    }

    private void SelectShield(string shieldName, ShieldFactory.ShieldType shieldType)
    {
        _selectedShieldType = shieldName;
        _selectedMonsterId = null;
        _selectedAbilityId = null;
        _selectedCosmeticType = null;
        _selectedNpcId = null;
        _selectedWeaponType = null;

        // Update label
        if (_selectedItemLabel != null)
        {
            _selectedItemLabel.Text = $"Selected: {shieldName}";
        }

        // Update button highlighting
        foreach (var (name, btn) in _shieldButtons)
        {
            var style = new StyleBoxFlat();
            style.BgColor = name == shieldName ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.08f, 0.08f, 0.12f);
            style.SetCornerRadiusAll(4);
            style.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", style);
        }

        // Update shield stats display
        var shieldData = ShieldFactory.GetShieldData(shieldType);

        if (_shieldArmorLabel != null) _shieldArmorLabel.Text = $"+{shieldData.Armor}";
        if (_shieldBlockChanceLabel != null) _shieldBlockChanceLabel.Text = $"{shieldData.BlockChance * 100:F0}%";
        if (_shieldBlockAmountLabel != null) _shieldBlockAmountLabel.Text = $"{shieldData.BlockAmount}";
        if (_shieldMovePenaltyLabel != null) _shieldMovePenaltyLabel.Text = shieldData.MovementPenalty > 0 ? $"-{shieldData.MovementPenalty * 100:F0}%" : "None";

        // Create preview
        UpdatePreviewShield(shieldType);

        // Update viewport hint
        UpdateViewportHint();

        GD.Print($"[EditorScreen3D] Selected shield: {shieldName}");
    }

    private void UpdatePreviewShield(ShieldFactory.ShieldType shieldType)
    {
        ClearPreview();

        _previewObject = new Node3D();
        _previewObject.Name = "ShieldPreview";

        // Create shield mesh
        var shield = ShieldFactory.CreateShield(shieldType);
        _previewObject.AddChild(shield);

        // Position shield upright, face toward camera
        shield.Position = new Vector3(0, 0.3f, 0);
        shield.RotationDegrees = new Vector3(0, 180, 0); // Face camera

        // Adjust camera for shield preview
        _cameraDistance = 1.5f;
        _cameraTargetY = 0.3f;

        _previewScene?.AddChild(_previewObject);
        UpdateCameraPosition();
    }

    private void OnAttachMonsterSelected(long index)
    {
        _attachedMonsterType = index switch
        {
            0 => null,  // (None)
            1 => "goblin",
            2 => "skeleton",
            3 => "goblin_warlord",
            4 => "goblin_shaman",
            5 => "goblin_thrower",
            _ => null
        };

        // Refresh weapon preview if a weapon is selected
        if (!string.IsNullOrEmpty(_selectedWeaponType))
        {
            UpdatePreviewWeapon(_selectedWeaponType);
        }

        GD.Print($"[EditorScreen3D] Attach monster changed to: {_attachedMonsterType ?? "(none)"}");
    }

    private void ClearPreview()
    {
        ClearStatusEffectVisual();
        if (_previewObject != null)
        {
            _previewObject.QueueFree();
            _previewObject = null;
        }
        _previewAnimPlayer = null;
        _previewLimbNodes = null;
        _fallbackLimbNodes.Clear();
        _previewAnimTime = 0f;
    }

    private void OnCycleAnimationsToggled(bool pressed)
    {
        _cycleAnimations = pressed;
        if (pressed)
        {
            _animationTimer = 0f;
            _currentAnimationIndex = 0;
            _currentAnimation = _monsterAnimations[0];
            if (_animationDropdown != null)
            {
                _animationDropdown.Selected = 0;
            }
        }
        UpdateAnimationLabel();
    }

    private void OnAnimationSelected(long index)
    {
        _currentAnimationIndex = (int)index;
        _currentAnimation = _monsterAnimations[_currentAnimationIndex];
        _animationTimer = 0f;
        _previewAnimTime = 0f;

        // Play the selected animation using AnimationPlayer
        if (_previewAnimPlayer != null && _previewAnimPlayer.HasAnimation(_currentAnimation))
        {
            _previewAnimPlayer.Stop();
            _previewAnimPlayer.Play(_currentAnimation);
        }

        UpdateAnimationLabel();
    }

    private void UpdateAnimationLabel()
    {
        if (_animationLabel != null)
        {
            // Format: "[Idle 0]", "[Walk 1]", etc.
            string[] parts = _currentAnimation.Split('_');
            string displayName = parts.Length == 2 ? $"{parts[0].Capitalize()} {parts[1]}" : _currentAnimation.Capitalize();
            _animationLabel.Text = $"[{displayName}]";
        }
    }

    private void OnStatusEffectSelected(long index)
    {
        // Index 0 is "None", rest are the status effects
        if (index == 0)
        {
            _currentStatusEffect = StatusEffectType.None;
            ClearStatusEffectVisual();
        }
        else
        {
            var allTypes = StatusEffectManager.GetAllTypes();
            if (index - 1 < allTypes.Length)
            {
                _currentStatusEffect = allTypes[index - 1];
                UpdateStatusEffectVisual();
            }
        }
    }

    private void ClearStatusEffectVisual()
    {
        if (_statusEffectVisual != null)
        {
            _statusEffectVisual.QueueFree();
            _statusEffectVisual = null;
        }
    }

    private void UpdateStatusEffectVisual()
    {
        ClearStatusEffectVisual();

        if (_currentStatusEffect == StatusEffectType.None || _previewObject == null)
            return;

        // Get entity height based on monster type
        float entityHeight = 1.5f;
        if (_selectedMonsterId != null)
        {
            switch (_selectedMonsterId.ToLower())
            {
                case "slime":
                    entityHeight = 0.8f;
                    break;
                case "mushroom":
                    entityHeight = 1.0f;
                    break;
                case "eye":
                    entityHeight = 1.0f;
                    break;
                case "spider":
                    entityHeight = 0.8f;
                    break;
                case "bat":
                    entityHeight = 0.7f;
                    break;
                case "wolf":
                    entityHeight = 1.0f;
                    break;
                case "badlama":
                    entityHeight = 1.5f;
                    break;
                case "dragon":
                    entityHeight = 1.2f;
                    break;
                case "skeleton":
                    entityHeight = 1.8f;
                    break;
                default:
                    entityHeight = 1.5f;
                    break;
            }
        }

        _statusEffectVisual = StatusEffectVisuals.CreateVisualEffect(_currentStatusEffect, entityHeight);
        _previewObject.AddChild(_statusEffectVisual);

        GD.Print($"[EditorScreen3D] Applied status effect: {_currentStatusEffect}");
    }

    private void UpdatePreviewAnimation(float delta)
    {
        if (_previewObject == null || _selectedMonsterId == null) return;

        float t = _previewAnimTime;

        switch (_currentAnimation)
        {
            case "idle":
                ApplyIdleAnimation(t);
                break;
            case "walk":
                ApplyWalkAnimation(t);
                break;
            case "attack":
                ApplyAttackAnimation(t);
                break;
            case "hit":
                ApplyHitAnimation(t);
                break;
            case "die":
                ApplyDieAnimation(t);
                break;
        }
    }

    private void ApplyIdleAnimation(float t)
    {
        // Gentle breathing/bobbing motion
        float breathe = Mathf.Sin(t * 2f) * 0.02f;
        if (_previewObject != null)
        {
            _previewObject.Position = new Vector3(0, breathe, 0);
        }

        // Subtle head movement for humanoid monsters
        if (_fallbackLimbNodes.TryGetValue("head", out var head))
        {
            head.RotationDegrees = new Vector3(
                Mathf.Sin(t * 1.5f) * 3f,
                Mathf.Sin(t * 0.8f) * 5f,
                0
            );
        }
    }

    private void ApplyWalkAnimation(float t)
    {
        // Full body bob
        float walkBob = Mathf.Abs(Mathf.Sin(t * 8f)) * 0.05f;
        if (_previewObject != null)
        {
            _previewObject.Position = new Vector3(0, walkBob, 0);
        }

        // Leg swing for humanoids
        float legSwing = Mathf.Sin(t * 8f) * 25f;
        if (_fallbackLimbNodes.TryGetValue("leftLeg", out var leftLeg))
        {
            leftLeg.RotationDegrees = new Vector3(legSwing, 0, 0);
        }
        if (_fallbackLimbNodes.TryGetValue("rightLeg", out var rightLeg))
        {
            rightLeg.RotationDegrees = new Vector3(-legSwing, 0, 0);
        }

        // Arm swing opposite to legs
        float armSwing = Mathf.Sin(t * 8f) * 15f;
        if (_fallbackLimbNodes.TryGetValue("leftArm", out var leftArm))
        {
            leftArm.RotationDegrees = new Vector3(-armSwing, 0, 20);
        }
        if (_fallbackLimbNodes.TryGetValue("rightArm", out var rightArm))
        {
            rightArm.RotationDegrees = new Vector3(armSwing, 0, -20);
        }
    }

    private void ApplyAttackAnimation(float t)
    {
        // Attack wind-up and strike
        float attackPhase = (t % 1.0f);  // 1-second attack cycle

        if (attackPhase < 0.3f)
        {
            // Wind-up
            float windUp = attackPhase / 0.3f;
            if (_fallbackLimbNodes.TryGetValue("rightArm", out var arm))
            {
                arm.RotationDegrees = new Vector3(-60 * windUp, 0, -20 - 30 * windUp);
            }
        }
        else if (attackPhase < 0.5f)
        {
            // Strike
            float strike = (attackPhase - 0.3f) / 0.2f;
            if (_fallbackLimbNodes.TryGetValue("rightArm", out var arm))
            {
                arm.RotationDegrees = new Vector3(-60 + 120 * strike, 0, -50 + 60 * strike);
            }
            // Lunge forward
            if (_previewObject != null)
            {
                _previewObject.Position = new Vector3(0, 0, strike * 0.15f);
            }
        }
        else
        {
            // Recovery
            float recovery = (attackPhase - 0.5f) / 0.5f;
            if (_fallbackLimbNodes.TryGetValue("rightArm", out var arm))
            {
                arm.RotationDegrees = new Vector3(60 - 60 * recovery, 0, 10 - 30 * recovery);
            }
            if (_previewObject != null)
            {
                _previewObject.Position = new Vector3(0, 0, 0.15f - 0.15f * recovery);
            }
        }
    }

    private void ApplyHitAnimation(float t)
    {
        // Flinch/stagger animation
        float hitPhase = (t % 0.6f);

        if (hitPhase < 0.15f)
        {
            // Initial recoil
            float recoil = hitPhase / 0.15f;
            if (_previewObject != null)
            {
                _previewObject.Position = new Vector3(0, 0, -0.1f * recoil);
                _previewObject.RotationDegrees = new Vector3(-10 * recoil, 0, 0);
            }
        }
        else
        {
            // Recovery
            float recovery = (hitPhase - 0.15f) / 0.45f;
            if (_previewObject != null)
            {
                _previewObject.Position = new Vector3(0, 0, -0.1f + 0.1f * recovery);
                _previewObject.RotationDegrees = new Vector3(-10 + 10 * recovery, 0, 0);
            }
        }
    }

    private void ApplyDieAnimation(float t)
    {
        // Death collapse animation
        float deathPhase = Mathf.Min(t, 1.5f) / 1.5f;  // 1.5 seconds to full death

        if (_previewObject != null)
        {
            // Fall backward and down
            float fallAngle = Mathf.Min(deathPhase * 90f, 85f);
            float fallHeight = Mathf.Max(0, (1f - deathPhase) * 0.5f - 0.3f);

            _previewObject.Position = new Vector3(0, fallHeight, -deathPhase * 0.3f);
            _previewObject.RotationDegrees = new Vector3(-fallAngle, 0, deathPhase * 15f);
        }

        // Limbs go limp
        if (_fallbackLimbNodes.TryGetValue("leftArm", out var leftArm))
        {
            leftArm.RotationDegrees = new Vector3(-30 * deathPhase, 0, 20 + 40 * deathPhase);
        }
        if (_fallbackLimbNodes.TryGetValue("rightArm", out var rightArm))
        {
            rightArm.RotationDegrees = new Vector3(-30 * deathPhase, 0, -20 - 40 * deathPhase);
        }
    }

    private void UpdatePreviewMonster(string monsterId)
    {
        ClearPreview();

        // Create a preview model for the selected monster
        _previewObject = new Node3D();
        _previewObject.Name = "MonsterPreview";

        // Use the shared MonsterMeshFactory for consistent appearance with in-game models
        Color? skinColor = _monsterSkinColorPicker?.Color;
        var limbs = MonsterMeshFactory.CreateMonsterMesh(_previewObject, monsterId, skinColor);

        // Store limb references for animation
        _previewLimbNodes = limbs;
        _fallbackLimbNodes.Clear();
        if (limbs.Head != null) _fallbackLimbNodes["head"] = limbs.Head;
        if (limbs.LeftArm != null) _fallbackLimbNodes["leftArm"] = limbs.LeftArm;
        if (limbs.RightArm != null) _fallbackLimbNodes["rightArm"] = limbs.RightArm;
        if (limbs.LeftLeg != null) _fallbackLimbNodes["leftLeg"] = limbs.LeftLeg;
        if (limbs.RightLeg != null) _fallbackLimbNodes["rightLeg"] = limbs.RightLeg;

        _previewScene?.AddChild(_previewObject);

        // Create AnimationPlayer with all 18 animations
        try
        {
            _previewAnimPlayer = MonsterAnimationSystem.CreateAnimationPlayer(_previewObject, monsterId, limbs);

            // Start playing the currently selected animation
            if (_previewAnimPlayer.HasAnimation(_currentAnimation))
            {
                _previewAnimPlayer.Play(_currentAnimation);
            }
            else if (_previewAnimPlayer.HasAnimation("idle_0"))
            {
                _currentAnimation = "idle_0";
                _currentAnimationIndex = 0;
                _previewAnimPlayer.Play("idle_0");
            }

            GD.Print($"[EditorScreen3D] Created AnimationPlayer for {monsterId} with {_previewAnimPlayer.GetAnimationList().Length} animations");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[EditorScreen3D] Failed to create AnimationPlayer: {e.Message}");
            _previewAnimPlayer = null;
        }

        // Reset camera view for this monster
        ResetCameraView();

        // Update stat spinboxes with monster's actual values
        UpdateMonsterStatsDisplay(monsterId);

        // Reapply status effect if one is selected
        if (_currentStatusEffect != StatusEffectType.None)
        {
            UpdateStatusEffectVisual();
        }
    }

    private void ResetCameraView()
    {
        _cameraYaw = 0.5f;
        _cameraPitch = 0.4f;  // Looking down from above
        _cameraDistance = 4f;
        _cameraTargetY = 0.6f;
        UpdateCameraPosition();
    }

    private void UpdateMonsterStatsDisplay(string monsterId)
    {
        // Set default values based on monster type
        float health = 75, damage = 10, speed = 3f, aggro = 15f;
        Color skinColor = new Color(0.45f, 0.55f, 0.35f);

        switch (monsterId.ToLower())
        {
            case "goblin":
                health = 75; damage = 10; speed = 3f; aggro = 15f;
                skinColor = new Color(0.45f, 0.55f, 0.35f);
                break;
            case "goblin_shaman":
                health = 50; damage = 15; speed = 2f; aggro = 18f;
                skinColor = new Color(0.35f, 0.45f, 0.55f); // Blue-green for magic
                break;
            case "goblin_thrower":
                health = 60; damage = 12; speed = 2.5f; aggro = 20f;
                skinColor = new Color(0.5f, 0.45f, 0.35f); // Tan/brown
                break;
            case "slime":
                health = 50; damage = 8; speed = 2f; aggro = 12f;
                skinColor = new Color(0.3f, 0.8f, 0.4f);
                break;
            case "eye":
                health = 40; damage = 15; speed = 4f; aggro = 20f;
                skinColor = new Color(0.9f, 0.2f, 0.2f);
                break;
            case "mushroom":
                health = 60; damage = 12; speed = 1.5f; aggro = 10f;
                skinColor = new Color(0.6f, 0.4f, 0.5f);
                break;
            case "spider":
                health = 55; damage = 14; speed = 4.5f; aggro = 18f;
                skinColor = new Color(0.2f, 0.2f, 0.25f);
                break;
            case "lizard":
                health = 80; damage = 16; speed = 3.5f; aggro = 16f;
                skinColor = new Color(0.3f, 0.5f, 0.25f);
                break;
            case "skeleton":
                health = 60; damage = 15; speed = 2.8f; aggro = 15f;
                skinColor = new Color(0.9f, 0.88f, 0.8f);
                break;
            case "wolf":
                health = 70; damage = 18; speed = 5f; aggro = 20f;
                skinColor = new Color(0.4f, 0.35f, 0.3f);
                break;
            case "bat":
                health = 40; damage = 8; speed = 4f; aggro = 15f;
                skinColor = new Color(0.25f, 0.2f, 0.25f);
                break;
            case "dragon":
                health = 200; damage = 35; speed = 2.5f; aggro = 25f;
                skinColor = new Color(0.7f, 0.2f, 0.15f);
                _cameraDistance = 5f; // Dragons are bigger
                _cameraTargetY = 0.8f;
                UpdateCameraPosition();
                break;
            // Bosses
            case "skeleton_lord":
                health = 500; damage = 40; speed = 2.5f; aggro = 30f;
                skinColor = new Color(0.85f, 0.82f, 0.7f);
                _cameraDistance = 5f;
                _cameraTargetY = 1.0f;
                UpdateCameraPosition();
                break;
            case "dragon_king":
                health = 1000; damage = 60; speed = 2f; aggro = 35f;
                skinColor = new Color(0.15f, 0.1f, 0.25f);
                _cameraDistance = 7f; // Much bigger
                _cameraTargetY = 1.2f;
                UpdateCameraPosition();
                break;
            case "spider_queen":
                health = 600; damage = 45; speed = 3f; aggro = 30f;
                skinColor = new Color(0.15f, 0.05f, 0.1f);
                _cameraDistance = 6f;
                _cameraTargetY = 0.8f;
                UpdateCameraPosition();
                break;
        }

        if (_monsterHealthSpinBox != null) _monsterHealthSpinBox.Value = health;
        if (_monsterDamageSpinBox != null) _monsterDamageSpinBox.Value = damage;
        if (_monsterSpeedSpinBox != null) _monsterSpeedSpinBox.Value = speed;
        if (_monsterAggroSpinBox != null) _monsterAggroSpinBox.Value = aggro;
        if (_monsterSkinColorPicker != null) _monsterSkinColorPicker.Color = skinColor;
    }

    private void CreateGoblinPreviewMesh(MeshInstance3D parent)
    {
        Color skinColor = _monsterSkinColorPicker?.Color ?? new Color(0.45f, 0.55f, 0.35f);
        Color clothColor = new Color(0.4f, 0.3f, 0.2f);
        Color eyeColor = new Color(0.9f, 0.85f, 0.2f);

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.8f };
        var clothMat = new StandardMaterial3D { AlbedoColor = clothColor, Roughness = 0.9f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.3f
        };

        // Body
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = clothMat;
        body.Position = new Vector3(0, 0.5f, 0);
        body.Scale = new Vector3(1f, 1.2f, 0.8f);
        parent.AddChild(body);

        // Head (in a Node3D for animation)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.88f, 0);
        parent.AddChild(headNode);
        _fallbackLimbNodes["head"] = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.22f, Height = 0.44f };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        headNode.AddChild(head);

        // Eyes (attach to head node so they move with head)
        var eyeMesh = new SphereMesh { Radius = 0.06f, Height = 0.12f };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.08f, 0.04f, 0.16f);
        headNode.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.08f, 0.04f, 0.16f);
        headNode.AddChild(rightEye);

        // Nose (attach to head)
        var nose = new MeshInstance3D();
        var noseMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.05f, Height = 0.15f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = skinMat;
        nose.Position = new Vector3(0, -0.03f, 0.2f);
        nose.RotationDegrees = new Vector3(-70, 0, 0);
        headNode.AddChild(nose);

        // Ears (attach to head)
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.06f, Height = 0.18f };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.22f, 0.04f, 0);
        leftEar.RotationDegrees = new Vector3(0, 0, -60);
        headNode.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.22f, 0.04f, 0);
        rightEar.RotationDegrees = new Vector3(0, 0, 60);
        headNode.AddChild(rightEar);

        // Arms (in Node3D containers for animation)
        var armMesh = new CapsuleMesh { Radius = 0.05f, Height = 0.3f };

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.28f, 0.45f, 0);
        leftArmNode.RotationDegrees = new Vector3(0, 0, 20);
        parent.AddChild(leftArmNode);
        _fallbackLimbNodes["leftArm"] = leftArmNode;
        var leftArmMesh = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        leftArmNode.AddChild(leftArmMesh);

        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.28f, 0.45f, 0);
        rightArmNode.RotationDegrees = new Vector3(0, 0, -20);
        parent.AddChild(rightArmNode);
        _fallbackLimbNodes["rightArm"] = rightArmNode;
        var rightArmMesh = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        rightArmNode.AddChild(rightArmMesh);

        // Legs (in Node3D containers for animation)
        var legMesh = new CapsuleMesh { Radius = 0.06f, Height = 0.25f };

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.1f, 0.15f, 0);
        parent.AddChild(leftLegNode);
        _fallbackLimbNodes["leftLeg"] = leftLegNode;
        var leftLegMesh = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        leftLegNode.AddChild(leftLegMesh);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.1f, 0.15f, 0);
        parent.AddChild(rightLegNode);
        _fallbackLimbNodes["rightLeg"] = rightLegNode;
        var rightLegMesh = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        rightLegNode.AddChild(rightLegMesh);
    }

    private void CreateSlimePreviewMesh(MeshInstance3D parent)
    {
        Color slimeColor = new Color(0.3f, 0.8f, 0.4f, 0.8f);
        var slimeMat = new StandardMaterial3D
        {
            AlbedoColor = slimeColor,
            Roughness = 0.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.4f, Height = 0.8f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = slimeMat;
        body.Position = new Vector3(0, 0.3f, 0);
        body.Scale = new Vector3(1f, 0.7f, 1f);
        parent.AddChild(body);

        var eyeMat = new StandardMaterial3D { AlbedoColor = Colors.White };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };

        var eyeMesh = new SphereMesh { Radius = 0.08f };
        var pupilMesh = new SphereMesh { Radius = 0.04f };

        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.12f, 0.4f, 0.25f);
        parent.AddChild(leftEye);

        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-0.12f, 0.4f, 0.32f);
        parent.AddChild(leftPupil);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.12f, 0.4f, 0.25f);
        parent.AddChild(rightEye);

        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(0.12f, 0.4f, 0.32f);
        parent.AddChild(rightPupil);
    }

    private void CreateSkeletonPreviewMesh(MeshInstance3D parent)
    {
        Color boneColor = new Color(0.9f, 0.88f, 0.8f);
        var boneMat = new StandardMaterial3D { AlbedoColor = boneColor, Roughness = 0.7f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.2f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f) * 0.5f
        };

        // Skull (in Node3D for animation)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.5f, 0);
        parent.AddChild(headNode);
        _fallbackLimbNodes["head"] = headNode;

        var skull = new MeshInstance3D();
        var skullMesh = new SphereMesh { Radius = 0.18f };
        skull.Mesh = skullMesh;
        skull.MaterialOverride = boneMat;
        skull.Scale = new Vector3(0.9f, 1f, 0.85f);
        headNode.AddChild(skull);

        // Eye sockets
        var socketMesh = new SphereMesh { Radius = 0.05f };
        var leftSocket = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = eyeMat };
        leftSocket.Position = new Vector3(-0.06f, 0.02f, 0.12f);
        headNode.AddChild(leftSocket);

        var rightSocket = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = eyeMat };
        rightSocket.Position = new Vector3(0.06f, 0.02f, 0.12f);
        headNode.AddChild(rightSocket);

        // Spine
        var spineMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.5f };
        var spine = new MeshInstance3D { Mesh = spineMesh, MaterialOverride = boneMat };
        spine.Position = new Vector3(0, 1.0f, 0);
        parent.AddChild(spine);

        // Ribs
        for (int i = 0; i < 4; i++)
        {
            float y = 1.15f - i * 0.08f;
            var ribMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 0.2f };

            var leftRib = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            leftRib.Position = new Vector3(-0.08f, y, 0.04f);
            leftRib.RotationDegrees = new Vector3(0, 0, 70);
            parent.AddChild(leftRib);

            var rightRib = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            rightRib.Position = new Vector3(0.08f, y, 0.04f);
            rightRib.RotationDegrees = new Vector3(0, 0, -70);
            parent.AddChild(rightRib);
        }

        // Pelvis
        var pelvisMesh = new BoxMesh { Size = new Vector3(0.2f, 0.08f, 0.1f) };
        var pelvis = new MeshInstance3D { Mesh = pelvisMesh, MaterialOverride = boneMat };
        pelvis.Position = new Vector3(0, 0.7f, 0);
        parent.AddChild(pelvis);

        // Arms (in Node3D containers for animation)
        var armMesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.02f, Height = 0.35f };

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.18f, 1.0f, 0);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -15);
        parent.AddChild(leftArmNode);
        _fallbackLimbNodes["leftArm"] = leftArmNode;
        var leftArmMesh = new MeshInstance3D { Mesh = armMesh, MaterialOverride = boneMat };
        leftArmNode.AddChild(leftArmMesh);

        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.18f, 1.0f, 0);
        rightArmNode.RotationDegrees = new Vector3(0, 0, 15);
        parent.AddChild(rightArmNode);
        _fallbackLimbNodes["rightArm"] = rightArmNode;
        var rightArmMesh = new MeshInstance3D { Mesh = armMesh, MaterialOverride = boneMat };
        rightArmNode.AddChild(rightArmMesh);

        // Legs (in Node3D containers for animation)
        var legMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.025f, Height = 0.4f };

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.08f, 0.35f, 0);
        parent.AddChild(leftLegNode);
        _fallbackLimbNodes["leftLeg"] = leftLegNode;
        var leftLegMesh = new MeshInstance3D { Mesh = legMesh, MaterialOverride = boneMat };
        leftLegNode.AddChild(leftLegMesh);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.08f, 0.35f, 0);
        parent.AddChild(rightLegNode);
        _fallbackLimbNodes["rightLeg"] = rightLegNode;
        var rightLegMesh = new MeshInstance3D { Mesh = legMesh, MaterialOverride = boneMat };
        rightLegNode.AddChild(rightLegMesh);
    }

    private void CreateWolfPreviewMesh(MeshInstance3D parent)
    {
        Color furColor = new Color(0.4f, 0.35f, 0.3f);
        var furMat = new StandardMaterial3D { AlbedoColor = furColor, Roughness = 0.9f };
        var noseMat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.12f, 0.1f) };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.7f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.7f, 0.2f) * 0.3f
        };

        // Body
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = 0.25f, Height = 0.8f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = furMat;
        body.Position = new Vector3(0, 0.5f, 0);
        body.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(body);

        // Head
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.18f };
        head.Mesh = headMesh;
        head.MaterialOverride = furMat;
        head.Position = new Vector3(0, 0.65f, 0.35f);
        head.Scale = new Vector3(0.9f, 0.9f, 1.1f);
        parent.AddChild(head);

        // Snout
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 0.18f };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = furMat;
        snout.Position = new Vector3(0, 0.6f, 0.52f);
        snout.RotationDegrees = new Vector3(-80, 0, 0);
        parent.AddChild(snout);

        // Nose
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.04f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, 0.58f, 0.6f);
        parent.AddChild(nose);

        // Ears
        var earMesh = new PrismMesh { Size = new Vector3(0.08f, 0.12f, 0.04f) };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        leftEar.Position = new Vector3(-0.1f, 0.8f, 0.3f);
        leftEar.RotationDegrees = new Vector3(10, 0, -15);
        parent.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        rightEar.Position = new Vector3(0.1f, 0.8f, 0.3f);
        rightEar.RotationDegrees = new Vector3(10, 0, 15);
        parent.AddChild(rightEar);

        // Eyes
        var eyeMesh = new SphereMesh { Radius = 0.04f };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.1f, 0.7f, 0.45f);
        parent.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.1f, 0.7f, 0.45f);
        parent.AddChild(rightEye);

        // Tail
        var tailMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.06f, Height = 0.35f };
        var tail = new MeshInstance3D { Mesh = tailMesh, MaterialOverride = furMat };
        tail.Position = new Vector3(0, 0.55f, -0.4f);
        tail.RotationDegrees = new Vector3(-50, 0, 0);
        parent.AddChild(tail);

        // Legs
        var legMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.035f, Height = 0.3f };
        float[] legX = { -0.12f, 0.12f, -0.12f, 0.12f };
        float[] legZ = { 0.15f, 0.15f, -0.15f, -0.15f };
        for (int i = 0; i < 4; i++)
        {
            var leg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
            leg.Position = new Vector3(legX[i], 0.15f, legZ[i]);
            parent.AddChild(leg);
        }
    }

    private void CreateBatPreviewMesh(MeshInstance3D parent)
    {
        Color batColor = new Color(0.25f, 0.2f, 0.25f);
        var batMat = new StandardMaterial3D { AlbedoColor = batColor, Roughness = 0.8f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.2f) * 0.5f
        };
        var wingMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.15f, 0.2f, 0.9f),
            Roughness = 0.6f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // Body
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.15f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = batMat;
        body.Position = new Vector3(0, 0.6f, 0);
        body.Scale = new Vector3(1f, 1.2f, 0.8f);
        parent.AddChild(body);

        // Head
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.1f };
        head.Mesh = headMesh;
        head.MaterialOverride = batMat;
        head.Position = new Vector3(0, 0.75f, 0.08f);
        parent.AddChild(head);

        // Big ears
        var earMesh = new PrismMesh { Size = new Vector3(0.08f, 0.15f, 0.03f) };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = batMat };
        leftEar.Position = new Vector3(-0.08f, 0.88f, 0.05f);
        leftEar.RotationDegrees = new Vector3(0, 0, -20);
        parent.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = batMat };
        rightEar.Position = new Vector3(0.08f, 0.88f, 0.05f);
        rightEar.RotationDegrees = new Vector3(0, 0, 20);
        parent.AddChild(rightEar);

        // Eyes
        var eyeMesh = new SphereMesh { Radius = 0.03f };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.05f, 0.77f, 0.15f);
        parent.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.05f, 0.77f, 0.15f);
        parent.AddChild(rightEye);

        // Wings
        var wingMesh = new BoxMesh { Size = new Vector3(0.5f, 0.01f, 0.3f) };
        var leftWing = new MeshInstance3D { Mesh = wingMesh, MaterialOverride = wingMat };
        leftWing.Position = new Vector3(-0.35f, 0.6f, 0);
        leftWing.RotationDegrees = new Vector3(0, 0, 15);
        parent.AddChild(leftWing);

        var rightWing = new MeshInstance3D { Mesh = wingMesh, MaterialOverride = wingMat };
        rightWing.Position = new Vector3(0.35f, 0.6f, 0);
        rightWing.RotationDegrees = new Vector3(0, 0, -15);
        parent.AddChild(rightWing);
    }

    private void CreateDragonPreviewMesh(MeshInstance3D parent)
    {
        Color scaleColor = new Color(0.7f, 0.2f, 0.15f);
        Color bellyColor = new Color(0.9f, 0.75f, 0.4f);
        var scaleMat = new StandardMaterial3D { AlbedoColor = scaleColor, Roughness = 0.5f, Metallic = 0.2f };
        var bellyMat = new StandardMaterial3D { AlbedoColor = bellyColor, Roughness = 0.6f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.6f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.6f, 0.1f) * 0.6f
        };

        // Body
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = 0.35f, Height = 1f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = scaleMat;
        body.Position = new Vector3(0, 0.6f, 0);
        body.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(body);

        // Belly
        var belly = new MeshInstance3D();
        var bellyMesh = new CapsuleMesh { Radius = 0.28f, Height = 0.85f };
        belly.Mesh = bellyMesh;
        belly.MaterialOverride = bellyMat;
        belly.Position = new Vector3(0, 0.55f, 0.08f);
        belly.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(belly);

        // Head
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.22f };
        head.Mesh = headMesh;
        head.MaterialOverride = scaleMat;
        head.Position = new Vector3(0, 0.85f, 0.45f);
        head.Scale = new Vector3(0.8f, 0.9f, 1.1f);
        parent.AddChild(head);

        // Snout
        var snout = new MeshInstance3D();
        var snoutMesh = new BoxMesh { Size = new Vector3(0.15f, 0.12f, 0.25f) };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = scaleMat;
        snout.Position = new Vector3(0, 0.78f, 0.68f);
        parent.AddChild(snout);

        // Horns
        var hornMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.04f, Height = 0.18f };
        var hornMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.15f, 0.1f) };
        var leftHorn = new MeshInstance3D { Mesh = hornMesh, MaterialOverride = hornMat };
        leftHorn.Position = new Vector3(-0.12f, 1.02f, 0.35f);
        leftHorn.RotationDegrees = new Vector3(-30, 0, -25);
        parent.AddChild(leftHorn);

        var rightHorn = new MeshInstance3D { Mesh = hornMesh, MaterialOverride = hornMat };
        rightHorn.Position = new Vector3(0.12f, 1.02f, 0.35f);
        rightHorn.RotationDegrees = new Vector3(-30, 0, 25);
        parent.AddChild(rightHorn);

        // Eyes
        var eyeMesh = new SphereMesh { Radius = 0.05f };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.1f, 0.9f, 0.6f);
        parent.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.1f, 0.9f, 0.6f);
        parent.AddChild(rightEye);

        // Wings
        var wingMat = new StandardMaterial3D
        {
            AlbedoColor = scaleColor.Lightened(0.1f),
            Roughness = 0.4f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var wingMesh = new BoxMesh { Size = new Vector3(0.6f, 0.02f, 0.4f) };
        var leftWing = new MeshInstance3D { Mesh = wingMesh, MaterialOverride = wingMat };
        leftWing.Position = new Vector3(-0.45f, 0.8f, -0.05f);
        leftWing.RotationDegrees = new Vector3(0, 0, 25);
        parent.AddChild(leftWing);

        var rightWing = new MeshInstance3D { Mesh = wingMesh, MaterialOverride = wingMat };
        rightWing.Position = new Vector3(0.45f, 0.8f, -0.05f);
        rightWing.RotationDegrees = new Vector3(0, 0, -25);
        parent.AddChild(rightWing);

        // Tail
        var tailMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.12f, Height = 0.6f };
        var tail = new MeshInstance3D { Mesh = tailMesh, MaterialOverride = scaleMat };
        tail.Position = new Vector3(0, 0.5f, -0.5f);
        tail.RotationDegrees = new Vector3(-40, 0, 0);
        parent.AddChild(tail);

        // Legs
        var legMesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.05f, Height = 0.35f };
        float[] legX = { -0.18f, 0.18f, -0.18f, 0.18f };
        float[] legZ = { 0.2f, 0.2f, -0.2f, -0.2f };
        for (int i = 0; i < 4; i++)
        {
            var leg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = scaleMat };
            leg.Position = new Vector3(legX[i], 0.18f, legZ[i]);
            parent.AddChild(leg);
        }
    }

    private void CreateEyePreviewMesh(MeshInstance3D parent)
    {
        // Floating eyeball monster - a giant eye with veins and a slit pupil
        Color eyeColor = new Color(0.95f, 0.9f, 0.85f);
        Color irisColor = new Color(0.9f, 0.2f, 0.2f);
        Color pupilColor = new Color(0.05f, 0.05f, 0.05f);
        Color veinColor = new Color(0.8f, 0.2f, 0.2f);

        var eyeMat = new StandardMaterial3D { AlbedoColor = eyeColor, Roughness = 0.3f };
        var irisMat = new StandardMaterial3D
        {
            AlbedoColor = irisColor,
            EmissionEnabled = true,
            Emission = irisColor * 0.4f
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = pupilColor, Roughness = 0.1f };
        var veinMat = new StandardMaterial3D { AlbedoColor = veinColor, Roughness = 0.6f };

        // Main eyeball (floating, so centered higher)
        var eyeball = new MeshInstance3D();
        var eyeballMesh = new SphereMesh { Radius = 0.4f };
        eyeball.Mesh = eyeballMesh;
        eyeball.MaterialOverride = eyeMat;
        eyeball.Position = new Vector3(0, 0.8f, 0);
        parent.AddChild(eyeball);

        // Iris (front-facing colored ring)
        var iris = new MeshInstance3D();
        var irisMesh = new SphereMesh { Radius = 0.2f };
        iris.Mesh = irisMesh;
        iris.MaterialOverride = irisMat;
        iris.Position = new Vector3(0, 0.8f, 0.3f);
        iris.Scale = new Vector3(1f, 1f, 0.3f);
        parent.AddChild(iris);

        // Pupil (vertical slit)
        var pupil = new MeshInstance3D();
        var pupilMesh = new CapsuleMesh { Radius = 0.04f, Height = 0.2f };
        pupil.Mesh = pupilMesh;
        pupil.MaterialOverride = pupilMat;
        pupil.Position = new Vector3(0, 0.8f, 0.38f);
        parent.AddChild(pupil);

        // Blood vessel tendrils (back of eye)
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Pi / 3f;
            var tendril = new MeshInstance3D();
            var tendrilMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.04f, Height = 0.25f };
            tendril.Mesh = tendrilMesh;
            tendril.MaterialOverride = veinMat;
            float x = Mathf.Cos(angle) * 0.15f;
            float y = 0.8f + Mathf.Sin(angle) * 0.15f;
            tendril.Position = new Vector3(x, y, -0.3f);
            tendril.RotationDegrees = new Vector3(60 + (float)GD.RandRange(-20, 20), 0, i * 60);
            parent.AddChild(tendril);
        }
    }

    private void CreateMushroomPreviewMesh(MeshInstance3D parent)
    {
        // Fantasy mushroom creature with cap and stubby body
        Color capColor = new Color(0.7f, 0.3f, 0.4f);
        Color stemColor = new Color(0.85f, 0.8f, 0.7f);
        Color spotColor = new Color(0.9f, 0.85f, 0.75f);
        Color eyeColor = new Color(0.1f, 0.1f, 0.1f);

        var capMat = new StandardMaterial3D { AlbedoColor = capColor, Roughness = 0.7f };
        var stemMat = new StandardMaterial3D { AlbedoColor = stemColor, Roughness = 0.8f };
        var spotMat = new StandardMaterial3D { AlbedoColor = spotColor, Roughness = 0.7f };
        var eyeMat = new StandardMaterial3D { AlbedoColor = eyeColor, Roughness = 0.2f };

        // Stem/body
        var stem = new MeshInstance3D();
        var stemMesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.25f, Height = 0.5f };
        stem.Mesh = stemMesh;
        stem.MaterialOverride = stemMat;
        stem.Position = new Vector3(0, 0.25f, 0);
        parent.AddChild(stem);

        // Mushroom cap
        var cap = new MeshInstance3D();
        var capMesh = new SphereMesh { Radius = 0.45f, Height = 0.9f };
        cap.Mesh = capMesh;
        cap.MaterialOverride = capMat;
        cap.Position = new Vector3(0, 0.65f, 0);
        cap.Scale = new Vector3(1f, 0.6f, 1f);
        parent.AddChild(cap);

        // Spots on cap
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Pi * 2f / 5f + 0.3f;
            var spot = new MeshInstance3D();
            var spotMesh = new SphereMesh { Radius = 0.08f };
            spot.Mesh = spotMesh;
            spot.MaterialOverride = spotMat;
            float x = Mathf.Cos(angle) * 0.25f;
            float z = Mathf.Sin(angle) * 0.25f;
            spot.Position = new Vector3(x, 0.75f, z);
            parent.AddChild(spot);
        }

        // Eyes (on stem)
        var eyeMesh = new SphereMesh { Radius = 0.06f };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.1f, 0.4f, 0.18f);
        parent.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.1f, 0.4f, 0.18f);
        parent.AddChild(rightEye);

        // Stubby legs
        var legMesh = new CapsuleMesh { Radius = 0.06f, Height = 0.15f };
        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.12f, 0.08f, 0);
        parent.AddChild(leftLegNode);
        _fallbackLimbNodes["leftLeg"] = leftLegNode;
        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = stemMat };
        leftLegNode.AddChild(leftLeg);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.12f, 0.08f, 0);
        parent.AddChild(rightLegNode);
        _fallbackLimbNodes["rightLeg"] = rightLegNode;
        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = stemMat };
        rightLegNode.AddChild(rightLeg);
    }

    private void CreateSpiderPreviewMesh(MeshInstance3D parent)
    {
        // Giant spider with 8 legs
        Color bodyColor = new Color(0.2f, 0.18f, 0.22f);
        Color legColor = new Color(0.25f, 0.22f, 0.28f);
        Color eyeColor = new Color(0.7f, 0.1f, 0.1f);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.8f };
        var legMat = new StandardMaterial3D { AlbedoColor = legColor, Roughness = 0.7f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.5f
        };

        // Abdomen (back section)
        var abdomen = new MeshInstance3D();
        var abdomenMesh = new SphereMesh { Radius = 0.35f };
        abdomen.Mesh = abdomenMesh;
        abdomen.MaterialOverride = bodyMat;
        abdomen.Position = new Vector3(0, 0.4f, -0.3f);
        abdomen.Scale = new Vector3(0.9f, 0.8f, 1.2f);
        parent.AddChild(abdomen);

        // Cephalothorax (front section)
        var cephalothorax = new MeshInstance3D();
        var cephalothoraxMesh = new SphereMesh { Radius = 0.25f };
        cephalothorax.Mesh = cephalothoraxMesh;
        cephalothorax.MaterialOverride = bodyMat;
        cephalothorax.Position = new Vector3(0, 0.35f, 0.15f);
        cephalothorax.Scale = new Vector3(1f, 0.8f, 0.9f);
        parent.AddChild(cephalothorax);

        // Eyes (cluster of 8)
        var eyeMesh = new SphereMesh { Radius = 0.04f };
        float[][] eyePositions = {
            new[] { -0.08f, 0.45f, 0.32f },
            new[] { 0.08f, 0.45f, 0.32f },
            new[] { -0.12f, 0.4f, 0.28f },
            new[] { 0.12f, 0.4f, 0.28f },
            new[] { -0.05f, 0.38f, 0.35f },
            new[] { 0.05f, 0.38f, 0.35f },
            new[] { -0.03f, 0.42f, 0.36f },
            new[] { 0.03f, 0.42f, 0.36f }
        };
        foreach (var pos in eyePositions)
        {
            var eye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
            eye.Position = new Vector3(pos[0], pos[1], pos[2]);
            parent.AddChild(eye);
        }

        // Fangs/mandibles
        var fangMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.03f, Height = 0.12f };
        var leftFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = bodyMat };
        leftFang.Position = new Vector3(-0.06f, 0.28f, 0.3f);
        leftFang.RotationDegrees = new Vector3(-30, 0, 15);
        parent.AddChild(leftFang);

        var rightFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = bodyMat };
        rightFang.Position = new Vector3(0.06f, 0.28f, 0.3f);
        rightFang.RotationDegrees = new Vector3(-30, 0, -15);
        parent.AddChild(rightFang);

        // 8 legs (4 on each side, bent at joints)
        var legSegmentMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.025f, Height = 0.25f };
        float[] legAngles = { 30f, 60f, 100f, 130f };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float xOffset = side * 0.18f;
                float zOffset = 0.1f - i * 0.12f;
                float yawAngle = side * (legAngles[i] - 90f);

                var leg = new MeshInstance3D { Mesh = legSegmentMesh, MaterialOverride = legMat };
                leg.Position = new Vector3(xOffset, 0.35f, zOffset);
                leg.RotationDegrees = new Vector3(70, yawAngle, side * 20);
                parent.AddChild(leg);
            }
        }
    }

    private void CreateLizardPreviewMesh(MeshInstance3D parent)
    {
        // Reptilian humanoid lizardman
        Color scaleColor = new Color(0.3f, 0.5f, 0.25f);
        Color bellyColor = new Color(0.6f, 0.55f, 0.4f);
        Color eyeColor = new Color(0.9f, 0.7f, 0.2f);

        var scaleMat = new StandardMaterial3D { AlbedoColor = scaleColor, Roughness = 0.6f, Metallic = 0.1f };
        var bellyMat = new StandardMaterial3D { AlbedoColor = bellyColor, Roughness = 0.7f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.3f
        };

        // Body
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = 0.25f, Height = 0.6f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = scaleMat;
        body.Position = new Vector3(0, 0.55f, 0);
        parent.AddChild(body);

        // Belly plate
        var belly = new MeshInstance3D();
        var bellyMesh = new CapsuleMesh { Radius = 0.2f, Height = 0.5f };
        belly.Mesh = bellyMesh;
        belly.MaterialOverride = bellyMat;
        belly.Position = new Vector3(0, 0.55f, 0.08f);
        belly.Scale = new Vector3(0.8f, 0.9f, 0.4f);
        parent.AddChild(belly);

        // Head (in Node3D for animation)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.0f, 0);
        parent.AddChild(headNode);
        _fallbackLimbNodes["head"] = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.18f };
        head.Mesh = headMesh;
        head.MaterialOverride = scaleMat;
        head.Scale = new Vector3(0.9f, 1f, 1.1f);
        headNode.AddChild(head);

        // Snout
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.1f, Height = 0.15f };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = scaleMat;
        snout.Position = new Vector3(0, -0.02f, 0.18f);
        snout.RotationDegrees = new Vector3(-80, 0, 0);
        headNode.AddChild(snout);

        // Eyes
        var eyeMesh = new SphereMesh { Radius = 0.04f };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.1f, 0.05f, 0.1f);
        headNode.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.1f, 0.05f, 0.1f);
        headNode.AddChild(rightEye);

        // Crest/spines on head
        for (int i = 0; i < 3; i++)
        {
            var spine = new MeshInstance3D();
            var spineMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.025f, Height = 0.1f };
            spine.Mesh = spineMesh;
            spine.MaterialOverride = scaleMat;
            spine.Position = new Vector3(0, 0.12f + i * 0.03f, -0.05f - i * 0.04f);
            spine.RotationDegrees = new Vector3(-30 - i * 10, 0, 0);
            headNode.AddChild(spine);
        }

        // Arms
        var armMesh = new CapsuleMesh { Radius = 0.06f, Height = 0.3f };
        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.28f, 0.6f, 0);
        leftArmNode.RotationDegrees = new Vector3(0, 0, 20);
        parent.AddChild(leftArmNode);
        _fallbackLimbNodes["leftArm"] = leftArmNode;
        var leftArmMesh = new MeshInstance3D { Mesh = armMesh, MaterialOverride = scaleMat };
        leftArmNode.AddChild(leftArmMesh);

        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.28f, 0.6f, 0);
        rightArmNode.RotationDegrees = new Vector3(0, 0, -20);
        parent.AddChild(rightArmNode);
        _fallbackLimbNodes["rightArm"] = rightArmNode;
        var rightArmMesh = new MeshInstance3D { Mesh = armMesh, MaterialOverride = scaleMat };
        rightArmNode.AddChild(rightArmMesh);

        // Legs
        var legMesh = new CapsuleMesh { Radius = 0.07f, Height = 0.35f };
        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.12f, 0.18f, 0);
        parent.AddChild(leftLegNode);
        _fallbackLimbNodes["leftLeg"] = leftLegNode;
        var leftLegMesh = new MeshInstance3D { Mesh = legMesh, MaterialOverride = scaleMat };
        leftLegNode.AddChild(leftLegMesh);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.12f, 0.18f, 0);
        parent.AddChild(rightLegNode);
        _fallbackLimbNodes["rightLeg"] = rightLegNode;
        var rightLegMesh = new MeshInstance3D { Mesh = legMesh, MaterialOverride = scaleMat };
        rightLegNode.AddChild(rightLegMesh);

        // Tail
        var tailMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.08f, Height = 0.5f };
        var tail = new MeshInstance3D { Mesh = tailMesh, MaterialOverride = scaleMat };
        tail.Position = new Vector3(0, 0.35f, -0.25f);
        tail.RotationDegrees = new Vector3(-50, 0, 0);
        parent.AddChild(tail);
    }

    private void CreateGenericPreviewMesh(MeshInstance3D parent, string monsterId)
    {
        Color color = monsterId.ToLower() switch
        {
            "eye" => new Color(0.9f, 0.2f, 0.2f),
            "mushroom" => new Color(0.6f, 0.4f, 0.5f),
            "spider" => new Color(0.2f, 0.2f, 0.25f),
            "lizard" => new Color(0.3f, 0.5f, 0.25f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };

        var mat = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.7f };

        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.4f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = mat;
        body.Position = new Vector3(0, 0.5f, 0);
        parent.AddChild(body);

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.85f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.85f, 0.2f) * 0.3f
        };
        var eyeMesh = new SphereMesh { Radius = 0.06f };

        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.15f, 0.6f, 0.3f);
        parent.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.15f, 0.6f, 0.3f);
        parent.AddChild(rightEye);
    }

    private void UpdatePreviewAbility(string abilityId)
    {
        ClearPreview();

        _previewObject = new Node3D();
        _previewObject.Name = "AbilityPreview";

        var sphere = new MeshInstance3D();
        var sphereMesh = new SphereMesh { Radius = 0.5f, Height = 1f };
        sphere.Mesh = sphereMesh;

        var mat = new StandardMaterial3D();
        mat.EmissionEnabled = true;

        mat.AlbedoColor = abilityId switch
        {
            "fireball" => new Color(1f, 0.4f, 0.1f),
            "chain_lightning" => new Color(0.3f, 0.5f, 1f),
            "soul_leech" => new Color(0.3f, 0.8f, 0.3f),
            "protective_shell" => new Color(0.5f, 0.7f, 1f),
            "gravity_well" => new Color(0.5f, 0.2f, 0.8f),
            "timestop_bubble" => new Color(0.4f, 0.9f, 0.9f),
            "infernal_ground" => new Color(1f, 0.3f, 0.1f),
            "banshees_wail" => new Color(0.7f, 0.5f, 0.9f),
            "berserk" => new Color(1f, 0.2f, 0.2f),
            _ => new Color(0.8f, 0.8f, 0.8f)
        };

        mat.Emission = mat.AlbedoColor;
        mat.EmissionEnergyMultiplier = 2f;
        sphere.MaterialOverride = mat;
        sphere.Position = new Vector3(0, 0.5f, 0);

        _previewObject.AddChild(sphere);
        _previewScene?.AddChild(_previewObject);
        ResetCameraView();
    }

    private void UpdatePreviewCosmetic(string cosmeticType)
    {
        ClearPreview();

        _previewObject = new Node3D();
        _previewObject.Name = "CosmeticPreview";

        Color primary = cosmeticType switch
        {
            // Original props
            "barrel" => new Color(0.55f, 0.35f, 0.2f),
            "crate" => new Color(0.5f, 0.38f, 0.22f),
            "torch" => new Color(0.4f, 0.25f, 0.15f),
            "chest" => new Color(0.5f, 0.35f, 0.2f),
            "crystal" => new Color(0.4f, 0.7f, 0.9f),
            "skull_pile" => new Color(0.9f, 0.85f, 0.75f),
            "chair" => new Color(0.45f, 0.3f, 0.2f),
            "campfire" => new Color(0.35f, 0.2f, 0.1f),
            // New dungeon props
            "bone_pile" => new Color(0.9f, 0.85f, 0.7f),
            "coiled_chains" => new Color(0.35f, 0.32f, 0.28f),
            "moss_patch" => new Color(0.2f, 0.45f, 0.15f),
            "water_puddle" => new Color(0.2f, 0.3f, 0.4f),
            "treasure_chest" => new Color(0.55f, 0.4f, 0.2f),
            "broken_barrel" => new Color(0.5f, 0.35f, 0.2f),
            "altar_stone" => new Color(0.35f, 0.33f, 0.3f),
            "glowing_mushrooms" => new Color(0.3f, 0.8f, 0.7f),
            "blood_pool" => new Color(0.4f, 0.05f, 0.05f),
            "spike_trap" => new Color(0.2f, 0.2f, 0.22f),
            "discarded_sword" => new Color(0.5f, 0.48f, 0.45f),
            "shattered_potion" => new Color(0.2f, 0.8f, 0.3f),
            "ancient_scroll" => new Color(0.85f, 0.8f, 0.65f),
            "brazier_fire" => new Color(0.25f, 0.23f, 0.2f),
            "manacles" => new Color(0.4f, 0.35f, 0.3f),
            "cracked_tiles" => new Color(0.35f, 0.33f, 0.3f),
            "rubble_heap" => new Color(0.4f, 0.38f, 0.35f),
            "thorny_vines" => new Color(0.25f, 0.35f, 0.2f),
            "engraved_rune" => new Color(0.35f, 0.32f, 0.3f),
            "abandoned_campfire" => new Color(0.15f, 0.12f, 0.1f),
            "rat_nest" => new Color(0.4f, 0.35f, 0.28f),
            "broken_pottery" => new Color(0.7f, 0.55f, 0.4f),
            "scattered_coins" => new Color(0.85f, 0.7f, 0.2f),
            "forgotten_shield" => new Color(0.5f, 0.4f, 0.3f),
            "moldy_bread" => new Color(0.55f, 0.5f, 0.4f),
            // Glowing fungi and lichen
            "cyan_lichen" => new Color(0.1f, 0.7f, 0.8f),
            "purple_lichen" => new Color(0.6f, 0.2f, 0.8f),
            "green_lichen" => new Color(0.2f, 0.8f, 0.3f),
            "orange_fungus" => new Color(1f, 0.5f, 0.1f),
            "blue_fungus" => new Color(0.2f, 0.4f, 1f),
            "pink_fungus" => new Color(1f, 0.3f, 0.6f),
            "ceiling_tendrils" => new Color(0.3f, 0.7f, 0.4f),
            "floor_spores" => new Color(0.8f, 0.7f, 0.2f),
            "bioluminescent_veins" => new Color(0.2f, 0.8f, 0.6f),
            _ => new Color(0.5f, 0.4f, 0.3f)
        };

        var cosmetic = Cosmetic3D.Create(cosmeticType, primary, primary.Darkened(0.2f),
            new Color(0.4f, 0.4f, 0.45f), 0.5f, 1f, true);
        _previewObject.AddChild(cosmetic);

        _previewScene?.AddChild(_previewObject);
        ResetCameraView();
    }

    private void UpdatePreviewNpc(string npcId)
    {
        ClearPreview();

        _previewObject = new Node3D();
        _previewObject.Name = "NpcPreview";

        if (npcId == "steve")
        {
            // Create Steve preview model (same mesh structure as Steve3D but simplified for preview)
            CreateStevePreviewModel();
        }

        _previewScene?.AddChild(_previewObject);
        ResetCameraView();

        // Adjust camera for smaller NPC model
        _cameraDistance = 1.5f;
        _cameraTargetY = 0.15f;
    }

    private void CreateStevePreviewModel()
    {
        if (_previewObject == null) return;

        // Check if we should use GLB model
        if (Pet.Steve3D.UseGlbModel)
        {
            string glbPath = Pet.Steve3D.GlbModelPath;
            if (ResourceLoader.Exists(glbPath))
            {
                var scene = GD.Load<PackedScene>(glbPath);
                if (scene != null)
                {
                    var glbInstance = scene.Instantiate<Node3D>();
                    // Scale the GLB model appropriately (adjust as needed)
                    glbInstance.Scale = new Vector3(0.3f, 0.3f, 0.3f);
                    _previewObject.AddChild(glbInstance);
                    GD.Print($"[EditorScreen3D] Loaded GLB model for Steve: {glbPath}");
                    return;
                }
            }
            else
            {
                GD.PrintErr($"[EditorScreen3D] GLB file not found: {glbPath}");
                // Fall through to procedural mesh
            }
        }

        // Procedural mesh fallback
        // Dark gray chihuahua with pink tongue
        var bodyColor = new Color(0.30f, 0.28f, 0.26f);   // Dark gray chihuahua
        var eyeColor = new Color(0.15f, 0.1f, 0.08f);     // Dark eyes
        var noseColor = new Color(0.1f, 0.08f, 0.08f);    // Black nose
        var tongueColor = new Color(1.0f, 0.5f, 0.6f);    // Pink tongue

        // Body (Height = 2*Radius)
        var bodyMesh = new MeshInstance3D();
        var bodyShape = new SphereMesh { Radius = 0.12f, Height = 0.24f };
        bodyMesh.Mesh = bodyShape;
        bodyMesh.Scale = new Vector3(1f, 0.8f, 1.2f);
        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.9f };
        bodyMesh.MaterialOverride = bodyMat;
        bodyMesh.Position = new Vector3(0, 0.15f, 0);
        _previewObject.AddChild(bodyMesh);

        // Head (Height = 2*Radius for proper sphere)
        var headMesh = new MeshInstance3D();
        var headShape = new SphereMesh { Radius = 0.1f, Height = 0.2f };
        headMesh.Mesh = headShape;
        headMesh.Scale = new Vector3(1.1f, 1f, 1f);
        var headMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.9f };
        headMesh.MaterialOverride = headMat;
        headMesh.Position = new Vector3(0, 0.22f, 0.12f);
        _previewObject.AddChild(headMesh);
        _fallbackLimbNodes["head"] = headMesh;

        // Snout (Height = 2*Radius)
        var snout = new MeshInstance3D();
        var snoutShape = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        snout.Mesh = snoutShape;
        snout.Scale = new Vector3(0.8f, 0.6f, 1.2f);
        snout.MaterialOverride = headMat;
        snout.Position = new Vector3(0, 0.18f, 0.2f);
        _previewObject.AddChild(snout);

        // Nose (Height = 2*Radius)
        var nose = new MeshInstance3D();
        var noseShape = new SphereMesh { Radius = 0.015f, Height = 0.03f };
        nose.Mesh = noseShape;
        var noseMat = new StandardMaterial3D { AlbedoColor = noseColor, Roughness = 0.3f };
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, 0.18f, 0.24f);
        _previewObject.AddChild(nose);

        // Eyes (pushed forward so they're visible above snout)
        var eyeShape = new SphereMesh { Radius = 0.022f, Height = 0.044f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.2f, 0.15f) * 0.3f
        };

        var leftEye = new MeshInstance3D { Mesh = eyeShape, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.04f, 0.24f, 0.20f);  // More forward
        _previewObject.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeShape, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.04f, 0.24f, 0.20f);  // More forward
        _previewObject.AddChild(rightEye);

        // Eye highlights
        var highlightShape = new SphereMesh { Radius = 0.008f, Height = 0.016f };
        var highlightMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            EmissionEnabled = true,
            Emission = Colors.White * 0.5f
        };

        var leftHighlight = new MeshInstance3D { Mesh = highlightShape, MaterialOverride = highlightMat };
        leftHighlight.Position = new Vector3(-0.035f, 0.248f, 0.215f);
        _previewObject.AddChild(leftHighlight);

        var rightHighlight = new MeshInstance3D { Mesh = highlightShape, MaterialOverride = highlightMat };
        rightHighlight.Position = new Vector3(0.045f, 0.248f, 0.215f);
        _previewObject.AddChild(rightHighlight);

        // Pink tongue hanging out
        var tongue = new MeshInstance3D();
        var tongueShape = new SphereMesh { Radius = 0.015f, Height = 0.03f };
        tongue.Mesh = tongueShape;
        tongue.Scale = new Vector3(1.0f, 0.4f, 1.5f);
        var tongueMat = new StandardMaterial3D { AlbedoColor = tongueColor, Roughness = 0.4f };
        tongue.MaterialOverride = tongueMat;
        tongue.Position = new Vector3(0, 0.15f, 0.245f);
        _previewObject.AddChild(tongue);

        // Ears
        var earShape = new PrismMesh { Size = new Vector3(0.06f, 0.12f, 0.02f) };
        var earMat = new StandardMaterial3D { AlbedoColor = bodyColor.Darkened(0.1f), Roughness = 0.9f };

        var earLeft = new Node3D();
        earLeft.Position = new Vector3(-0.08f, 0.3f, 0.08f);
        earLeft.RotationDegrees = new Vector3(15, -20, -30);
        _previewObject.AddChild(earLeft);
        var leftEarMesh = new MeshInstance3D { Mesh = earShape, MaterialOverride = earMat };
        earLeft.AddChild(leftEarMesh);
        _fallbackLimbNodes["ear_left"] = earLeft;

        var earRight = new Node3D();
        earRight.Position = new Vector3(0.08f, 0.3f, 0.08f);
        earRight.RotationDegrees = new Vector3(15, 20, 30);
        _previewObject.AddChild(earRight);
        var rightEarMesh = new MeshInstance3D { Mesh = earShape, MaterialOverride = earMat };
        earRight.AddChild(rightEarMesh);
        _fallbackLimbNodes["ear_right"] = earRight;

        // Tail
        var tail = new Node3D();
        tail.Position = new Vector3(0, 0.18f, -0.12f);
        _previewObject.AddChild(tail);
        var tailShape = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.02f, Height = 0.1f };
        var tailMesh = new MeshInstance3D { Mesh = tailShape, MaterialOverride = bodyMat };
        tailMesh.RotationDegrees = new Vector3(-45, 0, 0);
        tail.AddChild(tailMesh);
        _fallbackLimbNodes["tail"] = tail;

        // Legs
        var legShape = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.012f, Height = 0.1f };
        var legMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.9f };

        float[] legX = { -0.05f, 0.05f, -0.05f, 0.05f };
        float[] legZ = { 0.06f, 0.06f, -0.06f, -0.06f };
        string[] legNames = { "leg_fl", "leg_fr", "leg_bl", "leg_br" };
        for (int i = 0; i < 4; i++)
        {
            var leg = new MeshInstance3D { Mesh = legShape, MaterialOverride = legMat };
            leg.Position = new Vector3(legX[i], 0.05f, legZ[i]);
            _previewObject.AddChild(leg);
            _fallbackLimbNodes[legNames[i]] = leg;
        }
    }

    private void OnNpcGlbToggled(bool pressed)
    {
        // Update the static flag in Steve3D
        Pet.Steve3D.UseGlbModel = pressed;

        // Update checkbox text to reflect current state
        if (_npcUseGlbCheckbox != null)
        {
            _npcUseGlbCheckbox.Text = pressed ? "GLB Model" : "Procedural";
        }

        // Update path label color based on whether file exists
        if (_npcGlbPathLabel != null)
        {
            bool fileExists = ResourceLoader.Exists(Pet.Steve3D.GlbModelPath);
            _npcGlbPathLabel.AddThemeColorOverride("font_color",
                pressed ? (fileExists ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.5f, 0.5f))
                        : new Color(0.5f, 0.5f, 0.5f));
        }

        // Refresh preview
        if (_selectedNpcId == "steve")
        {
            UpdatePreviewNpc("steve");
        }

        // Update live Steve if exists
        if (Pet.Steve3D.Instance != null)
        {
            Pet.Steve3D.Instance.ReloadModel();
        }

        GD.Print($"[EditorScreen3D] Steve model: {(pressed ? "GLB" : "Procedural")}");
    }

    private void OnApplyChanges()
    {
        GD.Print("[EditorScreen3D] Applying changes...");

        if (_selectedMonsterId != null)
        {
            // Refresh the preview with new color/stats
            UpdatePreviewMonster(_selectedMonsterId);
            UpdateSoundStatus($"Changes applied to {_selectedMonsterId}");
        }
    }

    private void OnMonsterColorChanged(Color color)
    {
        // Immediately update preview when color changes
        if (_selectedMonsterId != null && _previewObject != null)
        {
            // Update all mesh materials with new color
            UpdatePreviewMeshColors(color);
        }
    }

    private void UpdatePreviewMeshColors(Color newColor)
    {
        if (_previewObject == null) return;

        // Find all MeshInstance3D children and update their material color
        foreach (var child in _previewObject.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
            {
                // Only update skin-colored materials (not eyes, cloth, etc.)
                // Check if it's a "skin" material by comparing brightness/saturation
                if (mat.AlbedoColor.G > 0.2f && mat.AlbedoColor.G < 0.8f)
                {
                    mat.AlbedoColor = newColor;
                    if (mat.EmissionEnabled)
                    {
                        mat.Emission = newColor * 0.3f;
                    }
                }
            }
            // Also check nested limb nodes
            if (child is Node3D node)
            {
                foreach (var grandchild in node.GetChildren())
                {
                    if (grandchild is MeshInstance3D limbMesh && limbMesh.MaterialOverride is StandardMaterial3D limbMat)
                    {
                        if (limbMat.AlbedoColor.G > 0.2f && limbMat.AlbedoColor.G < 0.8f)
                        {
                            limbMat.AlbedoColor = newColor;
                        }
                    }
                }
            }
        }
    }

    public new void Show()
    {
        // Update size to fill viewport
        var viewportSize = GetViewport().GetVisibleRect().Size;
        Size = viewportSize;

        // Also update the SubViewport size for crisp rendering
        if (_previewViewport != null)
        {
            _previewViewport.Size = new Vector2I((int)(viewportSize.X * 0.5f), (int)(viewportSize.Y * 0.8f));
        }

        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().Paused = true;

        // Hide HUD elements that overlap
        HUD3D.Instance?.HideChatWindow();
        InspectMode3D.Instance?.HideInfoPanel();

        if (_selectedMonsterId == null)
        {
            SelectMonster("goblin");
        }
    }

    public new void Hide()
    {
        Visible = false;

        // If we're on the splash screen (no game running), restore visible mouse
        // Otherwise capture for gameplay
        bool isOnSplashScreen = GameManager3D.Instance == null;
        Input.MouseMode = isOnSplashScreen
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;

        GetTree().Paused = false;

        // Restore HUD elements (only exist during gameplay)
        HUD3D.Instance?.ShowChatWindow();
        InspectMode3D.Instance?.ShowInfoPanel();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event.IsActionPressed("escape"))
        {
            // If we're on splash screen (no game running), reload the splash scene
            // This handles the case where we returned from InMapEditor
            bool isOnSplashScreen = GameManager3D.Instance == null;
            if (isOnSplashScreen)
            {
                // Clear all splash screen static flags to ensure clean state
                SplashScreen3D.ReturnToEditorAfterPlay = false;
                SplashScreen3D.EnterEditModeAfterLoad = false;
                SplashScreen3D.CustomMapPath = null;
                SplashScreen3D.EditModeMap = null;
                SplashScreen3D.EditModeMapPath = null;

                // CRITICAL: Unpause tree before scene change - buttons won't work if paused!
                GetTree().Paused = false;

                // Reload splash screen to show proper UI
                // Use CallDeferred to avoid issues during input handling
                Input.MouseMode = Input.MouseModeEnum.Visible;
                CallDeferred(nameof(ReloadSplashScreen));
            }
            else
            {
                // During gameplay, just hide and return to game
                Hide();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        // Skip viewport handling when on Maps tab (tab 5) - let MapEditorTab handle input
        if (_currentTab == 5)
        {
            return;
        }

        // Handle viewport mouse interaction
        if (_viewportContainer != null)
        {
            var vpRect = _viewportContainer.GetGlobalRect();
            var mousePos = GetGlobalMousePosition();

            if (vpRect.HasPoint(mousePos))
            {
                if (@event is InputEventMouseButton mouseButton)
                {
                    if (mouseButton.ButtonIndex == MouseButton.Left)
                    {
                        if (mouseButton.Pressed)
                        {
                            _isDragging = true;
                            _lastMousePos = mouseButton.Position;
                        }
                        else
                        {
                            _isDragging = false;
                        }
                        GetViewport().SetInputAsHandled();
                    }
                    else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
                    {
                        // Reset camera
                        ResetCameraView();
                        GetViewport().SetInputAsHandled();
                    }
                    else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                    {
                        _cameraDistance = Mathf.Max(1.5f, _cameraDistance - 0.3f);
                        UpdateCameraPosition();
                        GetViewport().SetInputAsHandled();
                    }
                    else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                    {
                        _cameraDistance = Mathf.Min(8f, _cameraDistance + 0.3f);
                        UpdateCameraPosition();
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (@event is InputEventMouseMotion motion && _isDragging)
                {
                    Vector2 delta = motion.Position - _lastMousePos;
                    _lastMousePos = motion.Position;

                    // Inverted controls: drag left = rotate left, drag up = look up
                    _cameraYaw -= delta.X * 0.01f;
                    _cameraPitch = Mathf.Clamp(_cameraPitch + delta.Y * 0.01f, -1.2f, 1.2f);
                    UpdateCameraPosition();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    public override void _ExitTree()
    {
        Instance = null;
    }

    private void ReloadSplashScreen()
    {
        var error = GetTree().ChangeSceneToFile("res://Scenes/Splash3D.tscn");
        if (error != Error.Ok)
        {
            GD.PrintErr($"[EditorScreen3D] Failed to reload splash screen: {error}");
        }
    }
}
