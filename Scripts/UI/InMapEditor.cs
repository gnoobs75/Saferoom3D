using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;
using SafeRoom3D.Environment;
using SafeRoom3D.Enemies;
using SafeRoom3D.NPC;
using SafeRoom3D.Player;

namespace SafeRoom3D.UI;

/// <summary>
/// In-Map Edit Mode controller. Allows first-person placement of props and monsters
/// in the dungeon. Monsters spawn in idle state (no AI). Props are saved to MapDefinition.
/// </summary>
public partial class InMapEditor : Control
{
    public static InMapEditor? Instance { get; private set; }

    /// <summary>
    /// Returns true if the selector pane (Z-key menu) is open and requires mouse input.
    /// </summary>
    public bool IsSelectorPaneOpen => _propPalette?.Visible == true;

    // Edit mode state
    public enum EditState { Idle, PlacingProp, PlacingMonster, PlacingNpc, Selecting }
    private EditState _currentState = EditState.Idle;

    // Surface snapping
    private enum SnapSurface { Floor, Wall, Ceiling, Invalid }
    private SnapSurface _currentSnapSurface = SnapSurface.Floor;

    // Current map being edited
    private MapDefinition? _currentMap;
    private string? _currentMapPath;
    private bool _enteredFromEditor = true; // Track if we entered from editor (vs from gameplay)

    // Placement state
    private string? _selectedPropType;
    private string? _selectedMonsterType;
    private string? _selectedNpcType;
    private Node3D? _placementPreview;
    private bool _isPreviewValid = false;

    // Selection state
    private Node3D? _selectedObject;
    private List<Node3D> _placedObjects = new();

    // UI Components
    private Control? _editHUD;
    private Label? _modeLabel;
    private Label? _instructionsLabel;
    private Label? _selectedLabel;
    private Button? _saveExitButton;
    private Control? _propPalette;
    private ItemList? _propList;
    private ItemList? _monsterList;
    private ItemList? _npcList;
    private CheckBox? _proceduralPropsToggle;
    private CheckBox? _roamerToggle;

    // Hotbar for quick prop access
    private string?[] _hotbarProps = new string?[10];
    private Button?[] _hotbarButtons = new Button?[10];

    // Available prop types (alphabetical)
    private static readonly string[] PropTypes = {
        "abandoned_campfire", "altar_stone", "ancient_scroll", "barrel", "blood_pool",
        "bone_pile", "bookshelf", "brazier_fire", "broken_barrel", "broken_pottery",
        "campfire", "cauldron", "chair", "chest", "coiled_chains",
        "cracked_tiles", "crate", "crystal", "discarded_sword", "engraved_rune",
        "forgotten_shield", "glowing_mushrooms", "manacles", "moldy_bread", "moss_patch",
        "pillar", "pot", "rat_nest", "rubble_heap", "scattered_coins",
        "shattered_potion", "skull_pile", "spike_trap", "statue", "table",
        "thorny_vines", "torch", "treasure_chest", "water_puddle"
    };

    // Monster types for placement
    private static readonly string[] MonsterTypes = {
        "goblin", "goblin_shaman", "goblin_thrower", "slime", "eye",
        "mushroom", "spider", "lizard", "skeleton", "wolf", "badlama", "bat", "dragon",
        "crawler_killer", "shadow_stalker", "flesh_golem", "plague_bearer", "living_armor",
        "camera_drone", "shock_drone", "advertiser_bot", "clockwork_spider",
        "lava_elemental", "ice_wraith", "gelatinous_cube", "void_spawn", "mimic", "dungeon_rat"
    };

    private static readonly string[] BossTypes = {
        "skeleton_lord", "dragon_king", "spider_queen",
        "the_butcher", "mordecai_the_traitor", "princess_donut", "mongo_the_destroyer", "zev_the_loot_goblin"
    };

    // NPC types for placement
    private static readonly string[] NpcTypes = {
        "steve", "bopca", "mordecai",
        "crawler_rex", "crawler_lily", "crawler_chad", "crawler_shade", "crawler_hank"
    };

    public override void _Ready()
    {
        Instance = this;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // Let clicks through to game
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        BuildUI();
        GD.Print("[InMapEditor] Ready");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        UpdatePlacementPreview();
        UpdateHUD();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            HandleKeyInput(keyEvent);
        }
        else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            HandleMouseInput(mouseEvent);
        }
    }

    /// <summary>
    /// Enters edit mode for the given map.
    /// </summary>
    /// <param name="map">The map definition to edit</param>
    /// <param name="mapPath">Path to the map file</param>
    /// <param name="fromEditor">True if entered from Map Editor, false if entered during gameplay</param>
    public void EnterEditMode(MapDefinition map, string? mapPath, bool fromEditor = true)
    {
        _currentMap = map;
        _currentMapPath = mapPath;
        _enteredFromEditor = fromEditor;
        _currentState = EditState.Idle;
        _selectedPropType = null;
        _selectedMonsterType = null;

        // Clear any previously tracked placed objects
        _placedObjects.Clear();

        // Update procedural props toggle
        if (_proceduralPropsToggle != null)
            _proceduralPropsToggle.ButtonPressed = map.UseProceduralProps;

        // Show the editor
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // Disable player combat but keep movement
        if (FPSController.Instance != null)
        {
            FPSController.Instance.SetEditMode(true);
        }

        // Set global edit mode flag and put all enemies in stasis
        GameManager3D.SetEditMode(true);
        PutAllEnemiesInStasis();

        // Spawn existing placed props from map definition (if any)
        SpawnExistingPlacedProps();

        GD.Print($"[InMapEditor] Entered edit mode for: {map.Name} ({map.PlacedProps.Count} placed props)");
    }

    /// <summary>
    /// Exits edit mode and returns to map editor or game depending on how we entered.
    /// </summary>
    public void ExitEditMode(bool save = true)
    {
        if (save && _currentMap != null)
        {
            SavePlacedObjects();
        }

        // Clear preview
        ClearPlacementPreview();

        // Re-enable player combat
        if (FPSController.Instance != null)
        {
            FPSController.Instance.SetEditMode(false);
        }

        // Clear global edit mode flag and release all enemies from stasis
        GameManager3D.SetEditMode(false);
        ReleaseAllEnemiesFromStasis();

        Visible = false;

        // Return to map editor or continue gameplay
        if (_enteredFromEditor)
        {
            // Return to map editor via splash screen redirect
            ReturnToMapEditor();
        }
        else
        {
            // Just hide the editor and continue gameplay
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GD.Print("[InMapEditor] Exited edit mode - returning to gameplay");
        }

        GD.Print("[InMapEditor] Exited edit mode");
    }

    private void PutAllEnemiesInStasis()
    {
        var enemies = GetTree().GetNodesInGroup("Enemies");
        int count = 0;
        foreach (var node in enemies)
        {
            if (node is BasicEnemy3D basicEnemy)
            {
                basicEnemy.EnterStasis();
                count++;
            }
            else if (node is BossEnemy3D boss)
            {
                boss.EnterStasis();
                count++;
            }
            else if (node is GoblinShaman shaman)
            {
                shaman.EnterStasis();
                count++;
            }
            else if (node is GoblinThrower thrower)
            {
                thrower.EnterStasis();
                count++;
            }
        }
        GD.Print($"[InMapEditor] Put {count} enemies in stasis");
    }

    private void ReleaseAllEnemiesFromStasis()
    {
        var enemies = GetTree().GetNodesInGroup("Enemies");
        int count = 0;
        foreach (var node in enemies)
        {
            if (node is BasicEnemy3D basicEnemy)
            {
                basicEnemy.ExitStasis();
                count++;
            }
            else if (node is BossEnemy3D boss)
            {
                boss.ExitStasis();
                count++;
            }
            else if (node is GoblinShaman shaman)
            {
                shaman.ExitStasis();
                count++;
            }
            else if (node is GoblinThrower thrower)
            {
                thrower.ExitStasis();
                count++;
            }
        }
        GD.Print($"[InMapEditor] Released {count} enemies from stasis");
    }

    /// <summary>
    /// Spawns props that were previously placed and saved in the map definition.
    /// These props are re-created as 3D objects in the scene so they can be selected/deleted.
    /// </summary>
    private void SpawnExistingPlacedProps()
    {
        if (_currentMap == null) return;

        var dungeon = GameManager3D.Instance?.GetDungeon();
        if (dungeon == null) return;

        int count = 0;
        foreach (var propData in _currentMap.PlacedProps)
        {
            var position = new Vector3(propData.X, propData.Y, propData.Z);
            var prop = Cosmetic3D.Create(
                propData.Type,
                new Color(0.6f, 0.4f, 0.3f),  // Default primary (wood brown)
                new Color(0.4f, 0.3f, 0.2f),  // Default secondary
                new Color(0.5f, 0.5f, 0.5f),  // Default accent
                0f, propData.Scale, true  // With collision
            );
            if (prop != null)
            {
                prop.Position = position;
                prop.RotationDegrees = new Vector3(0, propData.RotationY, 0);

                dungeon.AddChild(prop);
                _placedObjects.Add(prop);
                count++;
            }
        }

        GD.Print($"[InMapEditor] Spawned {count} existing placed props");
    }

    private void ReturnToMapEditor()
    {
        // Store map path for return
        if (!string.IsNullOrEmpty(_currentMapPath))
        {
            SplashScreen3D.CustomMapPath = _currentMapPath;
            SplashScreen3D.ReturnToEditorAfterPlay = true;
        }

        // Return to splash screen which will redirect to editor
        // Use CallDeferred to avoid issues during input handling
        CallDeferred(nameof(ChangeToSplashScreen));
    }

    private void ChangeToSplashScreen()
    {
        var error = GetTree().ChangeSceneToFile("res://Scenes/Splash3D.tscn");
        if (error != Error.Ok)
        {
            GD.PrintErr($"[InMapEditor] Failed to change to splash screen: {error}");
        }
    }

    private void BuildUI()
    {
        // Edit mode HUD panel (top-left)
        _editHUD = new PanelContainer();
        _editHUD.SetAnchorsPreset(LayoutPreset.TopLeft);
        _editHUD.Position = new Vector2(20, 20);
        _editHUD.CustomMinimumSize = new Vector2(300, 150);

        var hudStyle = new StyleBoxFlat();
        hudStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
        hudStyle.SetCornerRadiusAll(8);
        hudStyle.SetContentMarginAll(12);
        _editHUD.AddThemeStyleboxOverride("panel", hudStyle);
        AddChild(_editHUD);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        _editHUD.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "IN-MAP EDIT MODE";
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(title);

        // Mode label
        _modeLabel = new Label();
        _modeLabel.Text = "Mode: Idle";
        _modeLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_modeLabel);

        // Selected item label
        _selectedLabel = new Label();
        _selectedLabel.Text = "Selected: None";
        _selectedLabel.AddThemeFontSizeOverride("font_size", 14);
        _selectedLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1f));
        vbox.AddChild(_selectedLabel);

        // Instructions
        _instructionsLabel = new Label();
        _instructionsLabel.Text = "Z: Selector | Ctrl+Click: Delete | Esc: Exit";
        _instructionsLabel.AddThemeFontSizeOverride("font_size", 12);
        _instructionsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(_instructionsLabel);

        // Procedural props toggle
        _proceduralPropsToggle = new CheckBox();
        _proceduralPropsToggle.Text = "Use Procedural Props";
        _proceduralPropsToggle.ButtonPressed = true;
        _proceduralPropsToggle.Toggled += OnProceduralPropsToggled;
        vbox.AddChild(_proceduralPropsToggle);

        // Save & Exit button
        _saveExitButton = new Button();
        _saveExitButton.Text = "Save & Exit";
        _saveExitButton.Pressed += () => ExitEditMode(true);
        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = new Color(0.2f, 0.4f, 0.2f);
        btnStyle.SetCornerRadiusAll(4);
        _saveExitButton.AddThemeStyleboxOverride("normal", btnStyle);
        vbox.AddChild(_saveExitButton);

        // Prop palette (floating, hidden by default)
        CreatePropPalette();

        // Hotbar for props (bottom center)
        CreateHotbar();
    }

    private void CreatePropPalette()
    {
        // Left-side persistent selector pane (opened with Z key)
        _propPalette = new PanelContainer();
        _propPalette.SetAnchorsPreset(LayoutPreset.LeftWide);
        _propPalette.AnchorLeft = 0;
        _propPalette.AnchorRight = 0;
        _propPalette.AnchorTop = 0;
        _propPalette.AnchorBottom = 1;
        _propPalette.OffsetRight = 280;
        _propPalette.OffsetTop = 180; // Below the edit HUD
        _propPalette.OffsetBottom = -100; // Above bottom
        _propPalette.CustomMinimumSize = new Vector2(280, 400);
        _propPalette.Visible = false;
        _propPalette.MouseFilter = MouseFilterEnum.Stop; // Capture clicks

        var paletteStyle = new StyleBoxFlat();
        paletteStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        paletteStyle.SetCornerRadiusAll(8);
        paletteStyle.SetContentMarginAll(12);
        _propPalette.AddThemeStyleboxOverride("panel", paletteStyle);
        AddChild(_propPalette);

        var paletteVBox = new VBoxContainer();
        paletteVBox.AddThemeConstantOverride("separation", 8);
        _propPalette.AddChild(paletteVBox);

        // Title
        var title = new Label();
        title.Text = "SELECTOR";
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        paletteVBox.AddChild(title);

        // Search box
        var searchBox = new LineEdit();
        searchBox.PlaceholderText = "Search...";
        searchBox.TextChanged += OnSearchTextChanged;
        paletteVBox.AddChild(searchBox);
        _searchBox = searchBox;

        // Tabs for Props vs Monsters vs NPCs
        var tabBar = new TabBar();
        tabBar.AddTab("Props");
        tabBar.AddTab("Monsters");
        tabBar.AddTab("NPCs");
        tabBar.TabChanged += OnPaletteTabChanged;
        paletteVBox.AddChild(tabBar);

        // Props list (scrollable)
        _propList = new ItemList();
        _propList.CustomMinimumSize = new Vector2(0, 300);
        _propList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _propList.ItemSelected += OnPropSelected;
        PopulatePropList("");
        paletteVBox.AddChild(_propList);

        // Monsters list (hidden by default)
        _monsterList = new ItemList();
        _monsterList.CustomMinimumSize = new Vector2(0, 300);
        _monsterList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _monsterList.ItemSelected += OnMonsterSelected;
        _monsterList.Visible = false;
        PopulateMonsterList("");
        paletteVBox.AddChild(_monsterList);

        // Roamer toggle (hidden by default, shows when monster tab is active)
        _roamerToggle = new CheckBox();
        _roamerToggle.Text = "Roamer (Large Patrol Area)";
        _roamerToggle.TooltipText = "Roamers patrol a 25-unit radius around their spawn point instead of 8";
        _roamerToggle.Visible = false;
        paletteVBox.AddChild(_roamerToggle);

        // NPCs list (hidden by default)
        _npcList = new ItemList();
        _npcList.CustomMinimumSize = new Vector2(0, 300);
        _npcList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _npcList.ItemSelected += OnNpcSelected;
        _npcList.Visible = false;
        PopulateNpcList("");
        paletteVBox.AddChild(_npcList);

        // Close button
        var closeBtn = new Button();
        closeBtn.Text = "Close (Z)";
        closeBtn.Pressed += CloseSelectorPane;
        paletteVBox.AddChild(closeBtn);
    }

    private LineEdit? _searchBox;
    private int _currentPaletteTab = 0; // 0 = Props, 1 = Monsters

    private void PopulatePropList(string filter)
    {
        if (_propList == null) return;
        _propList.Clear();
        foreach (var prop in PropTypes)
        {
            string displayName = prop.Replace("_", " ");
            if (string.IsNullOrEmpty(filter) || displayName.ToLower().Contains(filter.ToLower()))
            {
                _propList.AddItem(displayName.Capitalize());
            }
        }
    }

    private void PopulateMonsterList(string filter)
    {
        if (_monsterList == null) return;
        _monsterList.Clear();
        foreach (var monster in MonsterTypes)
        {
            string displayName = monster.Replace("_", " ");
            if (string.IsNullOrEmpty(filter) || displayName.ToLower().Contains(filter.ToLower()))
            {
                _monsterList.AddItem(displayName.Capitalize());
            }
        }
        // Add bosses with different styling
        foreach (var boss in BossTypes)
        {
            string displayName = boss.Replace("_", " ");
            if (string.IsNullOrEmpty(filter) || displayName.ToLower().Contains(filter.ToLower()))
            {
                _monsterList.AddItem($"[BOSS] {displayName.Capitalize()}");
            }
        }
    }

    private void PopulateNpcList(string filter)
    {
        if (_npcList == null) return;
        _npcList.Clear();
        foreach (var npc in NpcTypes)
        {
            string displayName = npc switch
            {
                "steve" => "Steve (Companion)",
                "bopca" => "Bopca (Shopkeeper)",
                "mordecai" => "Mordecai (Quest Giver)",
                "crawler_rex" => "Rex (Veteran Crawler)",
                "crawler_lily" => "Lily (Rookie Crawler)",
                "crawler_chad" => "Chad (Showoff Crawler)",
                "crawler_shade" => "Shade (Mysterious Crawler)",
                "crawler_hank" => "Hank (Comedic Crawler)",
                _ => npc.Replace("_", " ").Capitalize()
            };
            if (string.IsNullOrEmpty(filter) || displayName.ToLower().Contains(filter.ToLower()))
            {
                _npcList.AddItem(displayName);
            }
        }
    }

    private void OnSearchTextChanged(string newText)
    {
        if (_currentPaletteTab == 0)
            PopulatePropList(newText);
        else if (_currentPaletteTab == 1)
            PopulateMonsterList(newText);
        else
            PopulateNpcList(newText);
    }

    private void OpenSelectorPane()
    {
        if (_propPalette == null) return;
        _propPalette.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        // Don't auto-focus search box - user can click on it if needed
        // This prevents the Z key from being typed into the search box
    }

    private void CloseSelectorPane()
    {
        if (_propPalette == null) return;
        _propPalette.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void ToggleSelectorPane()
    {
        if (_propPalette == null) return;
        if (_propPalette.Visible)
            CloseSelectorPane();
        else
            OpenSelectorPane();
    }

    private void CreateHotbar()
    {
        var hotbarPanel = new PanelContainer();
        hotbarPanel.SetAnchorsPreset(LayoutPreset.BottomWide);
        hotbarPanel.CustomMinimumSize = new Vector2(520, 60);
        hotbarPanel.Position = new Vector2(-260, -80);
        hotbarPanel.AnchorLeft = 0.5f;
        hotbarPanel.AnchorRight = 0.5f;
        hotbarPanel.AnchorTop = 1f;
        hotbarPanel.AnchorBottom = 1f;
        hotbarPanel.OffsetLeft = -260;
        hotbarPanel.OffsetRight = 260;
        hotbarPanel.OffsetTop = -80;
        hotbarPanel.OffsetBottom = -20;

        var hotbarStyle = new StyleBoxFlat();
        hotbarStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.8f);
        hotbarStyle.SetCornerRadiusAll(6);
        hotbarPanel.AddThemeStyleboxOverride("panel", hotbarStyle);
        AddChild(hotbarPanel);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hotbarPanel.AddChild(hbox);

        for (int i = 0; i < 10; i++)
        {
            int slot = i;
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(48, 48);
            btn.Text = $"{(i + 1) % 10}";
            btn.TooltipText = "Empty - Drag prop here";
            btn.Pressed += () => UseHotbarSlot(slot);

            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(0.15f, 0.15f, 0.2f);
            btnStyle.SetCornerRadiusAll(4);
            btn.AddThemeStyleboxOverride("normal", btnStyle);
            hbox.AddChild(btn);
            _hotbarButtons[i] = btn;
        }
    }

    private void HandleKeyInput(InputEventKey keyEvent)
    {
        var keycode = keyEvent.Keycode;

        // Z - Toggle selector pane
        if (keycode == Key.Z)
        {
            ToggleSelectorPane();
        }
        // Escape - Cancel placement or exit
        else if (keycode == Key.Escape)
        {
            if (_propPalette != null && _propPalette.Visible)
            {
                _propPalette.Visible = false;
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
            else if (_currentState != EditState.Idle)
            {
                CancelPlacement();
            }
            else
            {
                ExitEditMode(true);
            }
        }
        // Delete - Delete selected object
        else if (keycode == Key.Delete && _selectedObject != null)
        {
            DeleteSelectedObject();
        }
        // R - Rotate preview
        else if (keycode == Key.R && _placementPreview != null)
        {
            _placementPreview.RotateY(Mathf.Pi / 4f); // 45 degree rotation
        }
        // Number keys for hotbar
        else if (keycode >= Key.Key1 && keycode <= Key.Key0)
        {
            int slot = keycode == Key.Key0 ? 9 : (int)(keycode - Key.Key1);
            UseHotbarSlot(slot);
        }
    }

    private void HandleMouseInput(InputEventMouseButton mouseEvent)
    {
        // Don't process mouse clicks when selector pane is open - let UI handle them
        if (_propPalette != null && _propPalette.Visible)
            return;

        // Left click - Place, Select, or Delete (with Ctrl)
        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            // Ctrl+Click = Delete mode
            if (mouseEvent.CtrlPressed)
            {
                TryDeleteObjectAtCrosshair();
                return;
            }

            if (_currentState == EditState.PlacingProp || _currentState == EditState.PlacingMonster || _currentState == EditState.PlacingNpc)
            {
                if (_isPreviewValid)
                {
                    PlaceCurrentItem();
                }
            }
            else if (_currentState == EditState.Idle || _currentState == EditState.Selecting)
            {
                TrySelectObject();
            }
        }
        // Right click - Cancel placement
        else if (mouseEvent.ButtonIndex == MouseButton.Right)
        {
            if (_currentState != EditState.Idle)
            {
                CancelPlacement();
            }
        }
    }

    private void TryDeleteObjectAtCrosshair()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        var spaceState = camera.GetWorld3D().DirectSpaceState;
        var from = camera.GlobalPosition;
        var to = from + camera.GlobalTransform.Basis.Z * -20f;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithBodies = true;
        // Check props (Layer 5 = 16) and enemies (Layer 2 = 2)
        query.CollisionMask = 16 | 2;

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject() as Node3D;
            if (collider != null)
            {
                // Find root node
                var root = FindDeletableRoot(collider);
                if (root != null)
                {
                    // Remove from map definition if it's a placed object
                    RemoveFromMapDefinition(root);

                    // Remove from tracking lists
                    _placedObjects.Remove(root);

                    // Delete the node
                    root.QueueFree();
                    GD.Print($"[InMapEditor] Deleted object: {root.Name}");
                }
            }
        }
    }

    private Node3D? FindDeletableRoot(Node node)
    {
        // Check if this is a prop, enemy, or NPC
        if (node is Cosmetic3D || node is BasicEnemy3D || node is BossEnemy3D ||
            node is GoblinShaman || node is GoblinThrower || node is BaseNPC3D)
        {
            return node as Node3D;
        }

        // Check placed objects list
        if (node is Node3D node3D && _placedObjects.Contains(node3D))
            return node3D;

        var parent = node.GetParent();
        if (parent != null)
            return FindDeletableRoot(parent);

        return null;
    }

    private void RemoveFromMapDefinition(Node3D obj)
    {
        if (_currentMap == null) return;

        var pos = obj.GlobalPosition;

        // Try to remove from placed props
        for (int i = _currentMap.PlacedProps.Count - 1; i >= 0; i--)
        {
            var prop = _currentMap.PlacedProps[i];
            if (Mathf.Abs(prop.X - pos.X) < 0.5f && Mathf.Abs(prop.Z - pos.Z) < 0.5f)
            {
                _currentMap.PlacedProps.RemoveAt(i);
                GD.Print($"[InMapEditor] Removed prop from map definition");
                return;
            }
        }

        // Try to remove from enemies
        int tileX = (int)(pos.X / Constants.TileSize);
        int tileZ = (int)(pos.Z / Constants.TileSize);
        for (int i = _currentMap.Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _currentMap.Enemies[i];
            if (enemy.Position.X == tileX && enemy.Position.Z == tileZ)
            {
                _currentMap.Enemies.RemoveAt(i);
                GD.Print($"[InMapEditor] Removed enemy from map definition");
                return;
            }
        }

        // Try to remove from NPCs
        for (int i = _currentMap.Npcs.Count - 1; i >= 0; i--)
        {
            var npc = _currentMap.Npcs[i];
            if (Mathf.Abs(npc.X - pos.X) < 1f && Mathf.Abs(npc.Z - pos.Z) < 1f)
            {
                _currentMap.Npcs.RemoveAt(i);
                GD.Print($"[InMapEditor] Removed NPC from map definition");
                return;
            }
        }
    }

    private void OnPaletteTabChanged(long tab)
    {
        _currentPaletteTab = (int)tab;
        if (_propList != null && _monsterList != null && _npcList != null)
        {
            _propList.Visible = (tab == 0);
            _monsterList.Visible = (tab == 1);
            _npcList.Visible = (tab == 2);

            // Show roamer toggle only for monsters tab
            if (_roamerToggle != null)
            {
                _roamerToggle.Visible = (tab == 1);
            }

            // Re-apply search filter for the new tab
            string filter = _searchBox?.Text ?? "";
            if (tab == 0)
                PopulatePropList(filter);
            else if (tab == 1)
                PopulateMonsterList(filter);
            else
                PopulateNpcList(filter);
        }
    }

    private void OnPropSelected(long index)
    {
        if (index >= 0 && index < PropTypes.Length)
        {
            _selectedPropType = PropTypes[index];
            _selectedMonsterType = null;
            _currentState = EditState.PlacingProp;

            CreatePlacementPreview();
            _propPalette!.Visible = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;

            GD.Print($"[InMapEditor] Selected prop: {_selectedPropType}");
        }
    }

    private void OnMonsterSelected(long index)
    {
        string monsterType;
        if (index < MonsterTypes.Length)
        {
            monsterType = MonsterTypes[index];
        }
        else
        {
            monsterType = BossTypes[index - MonsterTypes.Length];
        }

        _selectedMonsterType = monsterType;
        _selectedPropType = null;
        _currentState = EditState.PlacingMonster;

        CreatePlacementPreview();
        _propPalette!.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        GD.Print($"[InMapEditor] Selected monster: {_selectedMonsterType}");
    }

    private void OnNpcSelected(long index)
    {
        if (index >= 0 && index < NpcTypes.Length)
        {
            _selectedNpcType = NpcTypes[index];
            _selectedPropType = null;
            _selectedMonsterType = null;
            _currentState = EditState.PlacingNpc;

            CreatePlacementPreview();
            _propPalette!.Visible = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;

            GD.Print($"[InMapEditor] Selected NPC: {_selectedNpcType}");
        }
    }

    private void OnProceduralPropsToggled(bool pressed)
    {
        if (_currentMap != null)
        {
            _currentMap.UseProceduralProps = pressed;
            GD.Print($"[InMapEditor] Procedural props: {pressed}");
        }
    }

    private void UseHotbarSlot(int slot)
    {
        if (_hotbarProps[slot] != null)
        {
            _selectedPropType = _hotbarProps[slot];
            _selectedMonsterType = null;
            _currentState = EditState.PlacingProp;
            CreatePlacementPreview();
            GD.Print($"[InMapEditor] Using hotbar slot {slot}: {_selectedPropType}");
        }
    }

    private void CreatePlacementPreview()
    {
        ClearPlacementPreview();

        if (_currentState == EditState.PlacingProp && _selectedPropType != null)
        {
            // Create prop preview using Cosmetic3D.Create factory
            var cosmetic = Cosmetic3D.Create(
                _selectedPropType,
                new Color(0.6f, 0.4f, 0.3f),  // Primary (wood brown)
                new Color(0.4f, 0.3f, 0.2f),  // Secondary
                new Color(0.5f, 0.5f, 0.5f),  // Accent
                0f, 1f, false  // No collision for preview
            );
            _placementPreview = cosmetic;
            if (_placementPreview != null)
            {
                // Make semi-transparent
                MakePreviewTransparent(_placementPreview);
                GetTree().Root.AddChild(_placementPreview);
            }
        }
        else if (_currentState == EditState.PlacingMonster && _selectedMonsterType != null)
        {
            // Create monster preview (simplified mesh)
            _placementPreview = new Node3D();
            MonsterMeshFactory.CreateMonsterMesh(_placementPreview, _selectedMonsterType, null, null);
            MakePreviewTransparent(_placementPreview);
            GetTree().Root.AddChild(_placementPreview);
        }
        else if (_currentState == EditState.PlacingNpc && _selectedNpcType != null)
        {
            // Create NPC preview using MonsterMeshFactory (for bopca/mordecai) or custom mesh
            _placementPreview = new Node3D();
            if (_selectedNpcType == "bopca" || _selectedNpcType == "mordecai")
            {
                MonsterMeshFactory.CreateMonsterMesh(_placementPreview, _selectedNpcType, null, null);
            }
            else
            {
                // For other NPCs like Steve, create a simple placeholder
                var placeholder = new MeshInstance3D();
                placeholder.Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.5f };
                var mat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.7f, 0.9f) };
                placeholder.MaterialOverride = mat;
                placeholder.Position = new Vector3(0, 0.75f, 0);
                _placementPreview.AddChild(placeholder);
            }
            MakePreviewTransparent(_placementPreview);
            GetTree().Root.AddChild(_placementPreview);
        }
    }

    private void MakePreviewTransparent(Node3D node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                if (mesh.MaterialOverride is StandardMaterial3D mat)
                {
                    var newMat = (StandardMaterial3D)mat.Duplicate();
                    newMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    newMat.AlbedoColor = new Color(newMat.AlbedoColor.R, newMat.AlbedoColor.G, newMat.AlbedoColor.B, 0.5f);
                    mesh.MaterialOverride = newMat;
                }
            }
            if (child is Node3D child3D)
            {
                MakePreviewTransparent(child3D);
            }
        }
    }

    private void ClearPlacementPreview()
    {
        if (_placementPreview != null && IsInstanceValid(_placementPreview))
        {
            _placementPreview.QueueFree();
            _placementPreview = null;
        }
    }

    private void UpdatePlacementPreview()
    {
        if (_placementPreview == null) return;

        // Raycast from camera center to find placement position
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        var spaceState = camera.GetWorld3D().DirectSpaceState;
        var from = camera.GlobalPosition;
        var to = from + camera.GlobalTransform.Basis.Z * -20f; // 20 meters forward

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithBodies = true;
        query.CollisionMask = 0b10000000; // Layer 8 (Wall/Floor)

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var hitPos = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];

            // Detect surface type from hit normal
            float dotUp = hitNormal.Dot(Vector3.Up);
            float dotDown = hitNormal.Dot(Vector3.Down);

            if (dotUp > 0.7f)
            {
                // Floor - snap Y to 0
                _currentSnapSurface = SnapSurface.Floor;
                hitPos.Y = 0;
                _placementPreview.Rotation = new Vector3(0, _placementPreview.Rotation.Y, 0);
            }
            else if (dotDown > 0.7f)
            {
                // Ceiling - snap Y to wall height
                _currentSnapSurface = SnapSurface.Ceiling;
                hitPos.Y = Constants.WallHeight;
                // Flip upside down for ceiling props
                _placementPreview.Rotation = new Vector3(Mathf.Pi, _placementPreview.Rotation.Y, 0);
            }
            else
            {
                // Wall - keep hit position, orient to face away from wall
                _currentSnapSurface = SnapSurface.Wall;
                // Calculate rotation to face away from wall
                float yaw = Mathf.Atan2(hitNormal.X, hitNormal.Z);
                _placementPreview.Rotation = new Vector3(0, yaw, 0);
            }

            _placementPreview.GlobalPosition = hitPos;
            _isPreviewValid = true;

            // Update preview color based on surface type
            UpdatePreviewColor(_currentSnapSurface);
        }
        else
        {
            _currentSnapSurface = SnapSurface.Invalid;
            _isPreviewValid = false;
            UpdatePreviewColor(SnapSurface.Invalid);
        }
    }

    private void UpdatePreviewColor(SnapSurface surface)
    {
        if (_placementPreview == null) return;

        // Set color based on surface type
        Color tintColor = surface switch
        {
            SnapSurface.Floor => new Color(0.3f, 1f, 0.3f, 0.6f),    // Green for floor
            SnapSurface.Wall => new Color(0.3f, 0.5f, 1f, 0.6f),    // Blue for wall
            SnapSurface.Ceiling => new Color(1f, 1f, 0.3f, 0.6f),   // Yellow for ceiling
            _ => new Color(1f, 0.3f, 0.3f, 0.6f)                     // Red for invalid
        };

        ApplyPreviewTint(_placementPreview, tintColor);
    }

    private void ApplyPreviewTint(Node3D node, Color tint)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                if (mesh.MaterialOverride is StandardMaterial3D mat)
                {
                    mat.AlbedoColor = new Color(
                        tint.R * mat.AlbedoColor.R + (1 - mat.AlbedoColor.R) * tint.R * 0.5f,
                        tint.G * mat.AlbedoColor.G + (1 - mat.AlbedoColor.G) * tint.G * 0.5f,
                        tint.B * mat.AlbedoColor.B + (1 - mat.AlbedoColor.B) * tint.B * 0.5f,
                        tint.A
                    );
                }
            }
            if (child is Node3D child3D)
            {
                ApplyPreviewTint(child3D, tint);
            }
        }
    }

    private void PlaceCurrentItem()
    {
        if (_placementPreview == null || _currentMap == null) return;

        var position = _placementPreview.GlobalPosition;
        var rotation = _placementPreview.Rotation.Y;

        if (_currentState == EditState.PlacingProp && _selectedPropType != null)
        {
            // Create actual prop using Cosmetic3D.Create factory
            var prop = Cosmetic3D.Create(
                _selectedPropType,
                new Color(0.6f, 0.4f, 0.3f),  // Primary (wood brown)
                new Color(0.4f, 0.3f, 0.2f),  // Secondary
                new Color(0.5f, 0.5f, 0.5f),  // Accent
                GD.Randf(), 1f, true  // With collision
            );
            if (prop != null)
            {
                prop.GlobalPosition = position;
                prop.Rotation = new Vector3(0, rotation, 0);

                // Add to scene
                var propsContainer = GetTree().Root.FindChild("Props", true, false) as Node3D;
                if (propsContainer != null)
                {
                    propsContainer.AddChild(prop);
                }
                else
                {
                    GetTree().Root.AddChild(prop);
                }

                _placedObjects.Add(prop);

                // Add to map definition
                _currentMap.PlacedProps.Add(new PropPlacement
                {
                    Type = _selectedPropType,
                    X = position.X,
                    Y = position.Y,
                    Z = position.Z,
                    RotationY = rotation
                });

                GD.Print($"[InMapEditor] Placed prop: {_selectedPropType} at {position}");
            }
        }
        else if (_currentState == EditState.PlacingMonster && _selectedMonsterType != null)
        {
            // Create monster in idle mode with rotation
            SpawnIdleMonster(_selectedMonsterType, position, rotation);
        }
        else if (_currentState == EditState.PlacingNpc && _selectedNpcType != null)
        {
            // Place NPC using SpawnNPC method
            SpawnNPC(_selectedNpcType, position, rotation);
        }
    }

    private void SpawnIdleMonster(string monsterType, Vector3 position, float rotationY = 0f)
    {
        if (_currentMap == null) return;

        bool isBoss = System.Array.IndexOf(BossTypes, monsterType) >= 0;
        bool isRoamer = _roamerToggle?.ButtonPressed ?? false;

        // Add to map definition (enemies list)
        int tileX = (int)(position.X / Constants.TileSize);
        int tileZ = (int)(position.Z / Constants.TileSize);

        _currentMap.Enemies.Add(new EnemyPlacement
        {
            Type = monsterType,
            Position = new PositionData { X = tileX, Z = tileZ },
            Level = 1,
            IsBoss = isBoss,
            RoomId = -1,
            RotationY = rotationY,
            IsRoamer = isRoamer
        });

        // Spawn the actual monster in stasis mode (immediate visibility)
        Node3D? spawnedMonster = null;

        if (isBoss)
        {
            var boss = new BossEnemy3D();
            boss.MonsterType = monsterType;
            boss.GlobalPosition = position;
            spawnedMonster = boss;

            // Add to enemies container
            var enemiesContainer = GetTree().Root.FindChild("Enemies", true, false) as Node3D;
            if (enemiesContainer != null)
                enemiesContainer.AddChild(boss);
            else
                GetTree().Root.AddChild(boss);

            // Enter stasis after it's in the scene tree
            boss.CallDeferred("EnterStasis");
        }
        else if (monsterType == "goblin_shaman")
        {
            var shaman = GoblinShaman.Create(position);
            spawnedMonster = shaman;

            var enemiesContainer = GetTree().Root.FindChild("Enemies", true, false) as Node3D;
            if (enemiesContainer != null)
                enemiesContainer.AddChild(shaman);
            else
                GetTree().Root.AddChild(shaman);

            shaman.CallDeferred("EnterStasis");
        }
        else if (monsterType == "goblin_thrower")
        {
            var thrower = GoblinThrower.Create(position);
            spawnedMonster = thrower;

            var enemiesContainer = GetTree().Root.FindChild("Enemies", true, false) as Node3D;
            if (enemiesContainer != null)
                enemiesContainer.AddChild(thrower);
            else
                GetTree().Root.AddChild(thrower);

            thrower.CallDeferred("EnterStasis");
        }
        else
        {
            // Regular enemy
            var enemy = new BasicEnemy3D();
            enemy.MonsterType = monsterType;
            enemy.GlobalPosition = position;
            if (isRoamer) enemy.SetRoamer(true);
            spawnedMonster = enemy;

            var enemiesContainer = GetTree().Root.FindChild("Enemies", true, false) as Node3D;
            if (enemiesContainer != null)
                enemiesContainer.AddChild(enemy);
            else
                GetTree().Root.AddChild(enemy);

            enemy.CallDeferred("EnterStasis");
        }

        // Track the spawned monster for deletion and apply rotation
        if (spawnedMonster != null)
        {
            spawnedMonster.Rotation = new Vector3(0, rotationY, 0);
            _placedObjects.Add(spawnedMonster);
        }

        string roamerSuffix = isRoamer ? " (Roamer)" : "";
        GD.Print($"[InMapEditor] Spawned monster in stasis: {monsterType}{roamerSuffix} at {position} rotation={rotationY}");
    }

    /// <summary>
    /// Spawn an NPC at the given position
    /// </summary>
    public void SpawnNPC(string npcType, Vector3 position, float rotationY = 0f)
    {
        if (_currentMap == null) return;

        // Add to map definition (npcs list)
        _currentMap.Npcs.Add(new NpcPlacement
        {
            Type = npcType,
            X = position.X,
            Z = position.Z,
            RotationY = rotationY
        });

        // Spawn the actual NPC
        Node3D? spawnedNpc = null;

        // Get or create NPCs container
        var npcsContainer = GetTree().Root.FindChild("NPCs", true, false) as Node3D;
        if (npcsContainer == null)
        {
            npcsContainer = new Node3D();
            npcsContainer.Name = "NPCs";
            GetTree().Root.AddChild(npcsContainer);
        }

        if (npcType == "bopca")
        {
            var bopca = new SafeRoom3D.NPC.Bopca3D();
            bopca.GlobalPosition = position;
            bopca.Rotation = new Vector3(0, rotationY, 0);
            spawnedNpc = bopca;
            npcsContainer.AddChild(bopca);
        }
        else if (npcType == "mordecai")
        {
            var mordecai = new SafeRoom3D.NPC.Mordecai3D();
            mordecai.GlobalPosition = position;
            mordecai.Rotation = new Vector3(0, rotationY, 0);
            spawnedNpc = mordecai;
            npcsContainer.AddChild(mordecai);
        }
        else if (npcType == "crawler_rex")
        {
            var rex = new SafeRoom3D.NPC.CrawlerRex();
            rex.GlobalPosition = position;
            rex.Rotation = new Vector3(0, rotationY, 0);
            spawnedNpc = rex;
            npcsContainer.AddChild(rex);
        }
        else if (npcType == "crawler_lily")
        {
            var lily = new SafeRoom3D.NPC.CrawlerLily();
            lily.GlobalPosition = position;
            lily.Rotation = new Vector3(0, rotationY, 0);
            spawnedNpc = lily;
            npcsContainer.AddChild(lily);
        }
        else if (npcType == "crawler_chad")
        {
            var chad = new SafeRoom3D.NPC.CrawlerChad();
            chad.GlobalPosition = position;
            chad.Rotation = new Vector3(0, rotationY, 0);
            spawnedNpc = chad;
            npcsContainer.AddChild(chad);
        }
        else if (npcType == "crawler_shade")
        {
            var shade = new SafeRoom3D.NPC.CrawlerShade();
            shade.GlobalPosition = position;
            shade.Rotation = new Vector3(0, rotationY, 0);
            spawnedNpc = shade;
            npcsContainer.AddChild(shade);
        }
        else if (npcType == "crawler_hank")
        {
            var hank = new SafeRoom3D.NPC.CrawlerHank();
            hank.GlobalPosition = position;
            hank.Rotation = new Vector3(0, rotationY, 0);
            spawnedNpc = hank;
            npcsContainer.AddChild(hank);
        }
        // Steve handled by existing Pet system

        if (spawnedNpc != null)
        {
            _placedObjects.Add(spawnedNpc);
        }

        GD.Print($"[InMapEditor] Spawned NPC: {npcType} at {position} rotation={rotationY}");
    }

    private void TrySelectObject()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        var spaceState = camera.GetWorld3D().DirectSpaceState;
        var from = camera.GlobalPosition;
        var to = from + camera.GlobalTransform.Basis.Z * -20f;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject() as Node3D;
            if (collider != null)
            {
                // Check if this is a placed object
                var root = FindPlacedRoot(collider);
                if (root != null && _placedObjects.Contains(root))
                {
                    _selectedObject = root;
                    _currentState = EditState.Selecting;
                    GD.Print($"[InMapEditor] Selected object: {root.Name}");
                }
            }
        }
    }

    private Node3D? FindPlacedRoot(Node node)
    {
        if (node is Node3D node3D && _placedObjects.Contains(node3D))
            return node3D;

        var parent = node.GetParent();
        if (parent != null)
            return FindPlacedRoot(parent);

        return null;
    }

    private void DeleteSelectedObject()
    {
        if (_selectedObject == null) return;

        // Remove from placed objects list
        _placedObjects.Remove(_selectedObject);

        // Try to find and remove from map definition
        // (This is simplified - would need to match by position)

        _selectedObject.QueueFree();
        _selectedObject = null;
        _currentState = EditState.Idle;

        GD.Print("[InMapEditor] Deleted selected object");
    }

    private void CancelPlacement()
    {
        ClearPlacementPreview();
        _selectedPropType = null;
        _selectedMonsterType = null;
        _selectedNpcType = null;
        _currentState = EditState.Idle;
    }

    private void SavePlacedObjects()
    {
        if (_currentMap == null || string.IsNullOrEmpty(_currentMapPath)) return;

        // PlacedProps are already added to _currentMap during placement
        // Just need to save the map
        _currentMap.SaveToJson(_currentMapPath);
        GD.Print($"[InMapEditor] Saved map with {_currentMap.PlacedProps.Count} placed props");
    }

    private void UpdateHUD()
    {
        if (_modeLabel != null)
        {
            string modeText = _currentState switch
            {
                EditState.Idle => "Mode: Idle (click to select)",
                EditState.PlacingProp => $"Mode: Placing Prop",
                EditState.PlacingMonster => $"Mode: Placing Monster",
                EditState.Selecting => "Mode: Selected (Del to delete)",
                _ => "Mode: Unknown"
            };
            _modeLabel.Text = modeText;
        }

        if (_selectedLabel != null)
        {
            string selectedText = "Selected: None";
            if (_selectedPropType != null)
                selectedText = $"Selected: {_selectedPropType.Replace("_", " ").Capitalize()}";
            else if (_selectedMonsterType != null)
                selectedText = $"Selected: {_selectedMonsterType.Replace("_", " ").Capitalize()}";
            else if (_selectedObject != null)
                selectedText = $"Selected: {_selectedObject.Name}";
            _selectedLabel.Text = selectedText;
        }

        if (_instructionsLabel != null)
        {
            string instructions = _currentState switch
            {
                EditState.Idle => "Z: Selector | Ctrl+Click: Delete | Esc: Exit",
                EditState.PlacingProp => "Click: Place | R: Rotate | Ctrl+Click: Delete | Right-Click: Cancel",
                EditState.PlacingMonster => "Click: Place | Ctrl+Click: Delete | Right-Click: Cancel",
                EditState.Selecting => "Del: Delete | Ctrl+Click: Delete | Esc: Deselect",
                _ => ""
            };
            _instructionsLabel.Text = instructions;
        }
    }
}
