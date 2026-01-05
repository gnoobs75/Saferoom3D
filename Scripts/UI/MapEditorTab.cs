using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;

namespace SafeRoom3D.UI;

/// <summary>
/// WYSIWYG Map Painter for EditorScreen3D. Provides a 2D overhead view of dungeon maps
/// with tile painting, room drawing, monster placement, and full undo/redo support.
/// </summary>
public partial class MapEditorTab : Control
{
    // Current map being edited
    private MapDefinition? _currentMap;
    private string? _currentMapPath;

    // UI Components
    private Control? _mapViewport;
    private Control? _mapDrawArea;
    private ItemList? _savedMapsList;
    private ItemList? _monsterPalette;
    private ItemList? _groupPalette;
    private Label? _mapInfoLabel;
    private Label? _selectedItemLabel;
    private LineEdit? _mapNameInput;
    private Button? _loadButton;
    private Button? _saveButton;
    private Button? _newMapButton;
    private Button? _deleteMapButton;
    private Button? _playButton;
    private Button? _clearButton;

    // Delete confirmation dialog
    private Control? _deleteConfirmDialog;
    private string? _mapPathToDelete;

    // Map file paths (indexed same as _savedMapsList)
    private List<string> _mapFilePaths = new();

    // View state
    private Vector2 _viewOffset = Vector2.Zero;
    private float _viewScale = 4f;  // Pixels per tile
    private bool _isPanning = false;
    private Vector2 _lastMousePos;

    // Placement state
    private string? _selectedMonsterType;
    private MonsterGroupTemplate? _selectedGroupTemplate;
    private bool _isPlacingMonster = false;
    private bool _isPlacingGroup = false;
    private Vector2 _placementPreviewPos;

    // === WYSIWYG TILE PAINTER STATE ===

    // Tool types
    private enum PaintTool { Floor, Erase, Room, Spawn, Monster }
    private PaintTool _currentTool = PaintTool.Floor;

    // Brush sizes (1x1, 3x3, 5x5, 10x10, 20x20)
    private static readonly int[] BrushSizes = { 1, 3, 5, 10, 20 };
    private int _currentBrushIndex = 1; // Default to 3x3
    private int CurrentBrushSize => BrushSizes[_currentBrushIndex];

    // Tool panel UI
    private Button? _floorToolButton;
    private Button? _eraseToolButton;
    private Button? _roomToolButton;
    private Button? _spawnToolButton;
    private Label? _brushSizeLabel;
    private Button? _brushSize1Button;
    private Button? _brushSize3Button;
    private Button? _brushSize5Button;
    private Button? _brushSize10Button;
    private Button? _brushSize20Button;

    // Room tool state (click-drag to define rectangle)
    private bool _isDrawingRoom = false;
    private Vector2I _roomStartTile;
    private Vector2I _roomEndTile;
    private string _roomType = "storage";
    private OptionButton? _roomTypeDropdown;

    // Spawn point
    private Vector2I? _spawnPoint;

    // Tile grid (0=void, 1=floor)
    private int[,]? _tileGrid;
    private bool _tileEditMode = true; // Now always on for tile-mode maps

    // Painting state
    private bool _isPaintingFloor = false;
    private bool _isPaintingVoid = false;

    // Undo/Redo system
    private class TileEditCommand
    {
        public List<(int x, int z, int oldValue, int newValue)> Changes = new();
    }
    private Stack<TileEditCommand> _undoStack = new();
    private Stack<TileEditCommand> _redoStack = new();
    private TileEditCommand? _currentCommand;
    private const int MaxUndoHistory = 50;

    // UI labels
    private Button? _tileEditButton;
    private Label? _tileEditLabel;
    private Label? _tileCoordLabel;
    private Button? _undoButton;
    private Button? _redoButton;

    // New map dialog
    private Control? _newMapDialog;
    private SpinBox? _newMapWidthInput;
    private SpinBox? _newMapDepthInput;
    private OptionButton? _newMapModeDropdown;

    // Room selection state
    private int _selectedRoomIndex = -1; // -1 = no room selected
    private Button? _deleteRoomButton;
    private Button? _clearAllRoomsButton;
    private ItemList? _roomList;

    // Monster types available for placement
    private static readonly string[] MonsterTypes = {
        // Original monsters
        "goblin", "goblin_shaman", "goblin_thrower", "slime", "eye",
        "mushroom", "spider", "lizard", "skeleton", "wolf", "badlama", "bat", "dragon",
        // DCC monsters
        "crawler_killer", "shadow_stalker", "flesh_golem", "plague_bearer", "living_armor",
        "camera_drone", "shock_drone", "advertiser_bot", "clockwork_spider",
        "lava_elemental", "ice_wraith", "gelatinous_cube", "void_spawn", "mimic", "dungeon_rat"
    };

    private static readonly string[] BossTypes = {
        // Original bosses
        "skeleton_lord", "dragon_king", "spider_queen",
        // DCC bosses
        "the_butcher", "mordecai_the_traitor", "princess_donut", "mongo_the_destroyer", "zev_the_loot_goblin"
    };

    // Colors for map display
    private static readonly Color VoidColor = new(0.05f, 0.05f, 0.08f);
    private static readonly Color FloorColor = new(0.35f, 0.32f, 0.3f);
    private static readonly Color RoomColor = new(0.45f, 0.42f, 0.38f);
    private static readonly Color SpawnRoomColor = new(0.3f, 0.5f, 0.3f);
    private static readonly Color CorridorColor = new(0.28f, 0.26f, 0.24f);
    private static readonly Color EnemyColor = new(0.9f, 0.2f, 0.2f);
    private static readonly Color BossColor = new(1f, 0.5f, 0.1f);
    private static readonly Color GroupColor = new(0.8f, 0.3f, 0.8f);
    private static readonly Color GridColor = new(0.15f, 0.15f, 0.18f);
    private static readonly Color SelectionColor = new(1f, 0.85f, 0.2f);
    private static readonly Color WallColor = new(0.8f, 0.2f, 0.2f);  // Red for walls
    private static readonly Color TileFloorColor = new(0.3f, 0.6f, 0.3f);  // Green for floor tiles
    private static readonly Color TileVoidColor = new(0.1f, 0.1f, 0.15f);  // Dark for void

    public override void _Ready()
    {
        GD.Print("[MapEditorTab] _Ready() CALLED!");
        // Don't use anchors - let parent container handle layout
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;  // Block input from reaching controls behind this panel
        ProcessMode = ProcessModeEnum.Always;  // Work even when game is paused
        CreateUI();
        RefreshSavedMapsList();
        GD.Print("[MapEditorTab] _Ready() COMPLETE - UI created");
    }

    public override void _EnterTree()
    {
        GD.Print("[MapEditorTab] _EnterTree() called");
    }

    public override void _ExitTree()
    {
        GD.Print("[MapEditorTab] _ExitTree() called");
    }

    /// <summary>
    /// Returns the current map name for display in the editor title.
    /// </summary>
    public string? GetCurrentMapName()
    {
        return _currentMap?.Name;
    }

    private void CreateUI()
    {
        // Add a background color so we can see the panel
        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = new Color(0.05f, 0.05f, 0.08f);
        AddChild(bg);

        // Main horizontal layout
        var mainHBox = new HBoxContainer();
        mainHBox.SetAnchorsPreset(LayoutPreset.FullRect);
        mainHBox.AddThemeConstantOverride("separation", 0);
        AddChild(mainHBox);

        // Left panel - Saved maps and controls
        CreateLeftPanel(mainHBox);

        // Center - Map viewport
        CreateMapViewport(mainHBox);

        // Right panel - Monster palette
        CreateRightPanel(mainHBox);
    }

    private void CreateLeftPanel(HBoxContainer parent)
    {
        var leftPanel = new PanelContainer();
        leftPanel.CustomMinimumSize = new Vector2(240, 0);
        leftPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var leftStyle = new StyleBoxFlat();
        leftStyle.BgColor = new Color(0.08f, 0.08f, 0.1f);
        leftStyle.BorderWidthRight = 2;
        leftStyle.BorderColor = new Color(0.2f, 0.2f, 0.25f);
        leftPanel.AddThemeStyleboxOverride("panel", leftStyle);
        parent.AddChild(leftPanel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        leftPanel.AddChild(scroll);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.AddChild(vbox);

        // === PAINTING TOOLS SECTION ===
        var toolsTitle = new Label();
        toolsTitle.Text = "Painting Tools";
        toolsTitle.AddThemeFontSizeOverride("font_size", 16);
        toolsTitle.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f));
        vbox.AddChild(toolsTitle);

        // Tool buttons in a grid
        var toolGrid = new GridContainer();
        toolGrid.Columns = 2;
        toolGrid.AddThemeConstantOverride("h_separation", 4);
        toolGrid.AddThemeConstantOverride("v_separation", 4);
        vbox.AddChild(toolGrid);

        _floorToolButton = CreateToolButton("Floor (F)", true);
        _floorToolButton.Pressed += () => SelectTool(PaintTool.Floor);
        toolGrid.AddChild(_floorToolButton);

        _eraseToolButton = CreateToolButton("Erase (E)", false);
        _eraseToolButton.Pressed += () => SelectTool(PaintTool.Erase);
        toolGrid.AddChild(_eraseToolButton);

        _roomToolButton = CreateToolButton("Room (R)", false);
        _roomToolButton.Pressed += () => SelectTool(PaintTool.Room);
        toolGrid.AddChild(_roomToolButton);

        _spawnToolButton = CreateToolButton("Spawn (P)", false);
        _spawnToolButton.Pressed += () => SelectTool(PaintTool.Spawn);
        toolGrid.AddChild(_spawnToolButton);

        // Room type dropdown (for Room tool)
        _roomTypeDropdown = new OptionButton();
        _roomTypeDropdown.AddItem("storage");
        _roomTypeDropdown.AddItem("library");
        _roomTypeDropdown.AddItem("armory");
        _roomTypeDropdown.AddItem("cathedral");
        _roomTypeDropdown.AddItem("crypt");
        _roomTypeDropdown.AddItem("barracks");
        _roomTypeDropdown.AddItem("throne");
        _roomTypeDropdown.ItemSelected += (idx) => _roomType = _roomTypeDropdown.GetItemText((int)idx);
        _roomTypeDropdown.CustomMinimumSize = new Vector2(0, 28);
        _roomTypeDropdown.Visible = false;
        vbox.AddChild(_roomTypeDropdown);

        // Brush size section
        _brushSizeLabel = new Label();
        _brushSizeLabel.Text = "Brush: 3x3";
        _brushSizeLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(_brushSizeLabel);

        var brushGrid = new GridContainer();
        brushGrid.Columns = 5;
        brushGrid.AddThemeConstantOverride("h_separation", 4);
        vbox.AddChild(brushGrid);

        _brushSize1Button = CreateBrushButton("1", false);
        _brushSize1Button.Pressed += () => SetBrushSize(0);
        brushGrid.AddChild(_brushSize1Button);

        _brushSize3Button = CreateBrushButton("3", true);
        _brushSize3Button.Pressed += () => SetBrushSize(1);
        brushGrid.AddChild(_brushSize3Button);

        _brushSize5Button = CreateBrushButton("5", false);
        _brushSize5Button.Pressed += () => SetBrushSize(2);
        brushGrid.AddChild(_brushSize5Button);

        _brushSize10Button = CreateBrushButton("10", false);
        _brushSize10Button.Pressed += () => SetBrushSize(3);
        brushGrid.AddChild(_brushSize10Button);

        _brushSize20Button = CreateBrushButton("20", false);
        _brushSize20Button.Pressed += () => SetBrushSize(4);
        brushGrid.AddChild(_brushSize20Button);

        // Undo/Redo buttons
        var undoRedoBox = new HBoxContainer();
        undoRedoBox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(undoRedoBox);

        _undoButton = CreateButton("Undo (Ctrl+Z)");
        _undoButton.Pressed += Undo;
        _undoButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        undoRedoBox.AddChild(_undoButton);

        _redoButton = CreateButton("Redo (Ctrl+Y)");
        _redoButton.Pressed += Redo;
        _redoButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        undoRedoBox.AddChild(_redoButton);

        // Tile coordinate label
        _tileCoordLabel = new Label();
        _tileCoordLabel.Text = "Tile: (--, --)";
        _tileCoordLabel.AddThemeFontSizeOverride("font_size", 11);
        _tileCoordLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
        vbox.AddChild(_tileCoordLabel);

        vbox.AddChild(new HSeparator());

        // === SAVED MAPS SECTION ===
        var mapsTitle = new Label();
        mapsTitle.Text = "Saved Maps";
        mapsTitle.AddThemeFontSizeOverride("font_size", 16);
        mapsTitle.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        vbox.AddChild(mapsTitle);

        _savedMapsList = new ItemList();
        _savedMapsList.CustomMinimumSize = new Vector2(0, 120);
        _savedMapsList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _savedMapsList.ItemSelected += OnMapSelected;
        _savedMapsList.ItemActivated += OnMapDoubleClicked;
        vbox.AddChild(_savedMapsList);

        var refreshBtn = CreateButton("Refresh List");
        refreshBtn.Pressed += () => RefreshSavedMapsList();
        vbox.AddChild(refreshBtn);

        _loadButton = CreateButton("Load Map");
        _loadButton.Pressed += OnLoadMapPressed;
        vbox.AddChild(_loadButton);

        _deleteMapButton = CreateButton("Delete Map");
        _deleteMapButton.Pressed += OnDeleteMapPressed;
        vbox.AddChild(_deleteMapButton);

        _newMapButton = CreateButton("New Map...");
        _newMapButton.Pressed += ShowNewMapDialog;
        vbox.AddChild(_newMapButton);

        vbox.AddChild(new HSeparator());

        // === SAVE SECTION ===
        var nameLabel = new Label();
        nameLabel.Text = "Map Name:";
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(nameLabel);

        _mapNameInput = new LineEdit();
        _mapNameInput.PlaceholderText = "Enter map name...";
        vbox.AddChild(_mapNameInput);

        _saveButton = CreateButton("Save Map (Ctrl+S)");
        _saveButton.Pressed += OnSaveMapPressed;
        vbox.AddChild(_saveButton);

        vbox.AddChild(new HSeparator());

        // === ROOMS SECTION ===
        var roomsTitle = new Label();
        roomsTitle.Text = "Rooms";
        roomsTitle.AddThemeFontSizeOverride("font_size", 16);
        roomsTitle.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1f));
        vbox.AddChild(roomsTitle);

        _roomList = new ItemList();
        _roomList.CustomMinimumSize = new Vector2(0, 80);
        _roomList.SizeFlagsVertical = SizeFlags.Fill;
        _roomList.ItemSelected += OnRoomListSelected;
        _roomList.AllowReselect = true;
        vbox.AddChild(_roomList);

        var roomButtonsBox = new HBoxContainer();
        roomButtonsBox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(roomButtonsBox);

        _deleteRoomButton = CreateButton("Delete");
        _deleteRoomButton.Pressed += OnDeleteSelectedRoom;
        _deleteRoomButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _deleteRoomButton.Disabled = true;
        roomButtonsBox.AddChild(_deleteRoomButton);

        _clearAllRoomsButton = CreateButton("Clear All");
        _clearAllRoomsButton.Pressed += OnClearAllRooms;
        _clearAllRoomsButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        roomButtonsBox.AddChild(_clearAllRoomsButton);

        vbox.AddChild(new HSeparator());

        // === PLAY AND EDIT ===
        _playButton = CreateButton("Play This Map");
        _playButton.Pressed += OnPlayMapPressed;
        SetButtonColor(_playButton, new Color(0.15f, 0.35f, 0.15f));
        vbox.AddChild(_playButton);

        var editModeButton = CreateButton("Edit In First Person");
        editModeButton.Pressed += OnEnterEditModePressed;
        SetButtonColor(editModeButton, new Color(0.2f, 0.3f, 0.4f));
        vbox.AddChild(editModeButton);

        vbox.AddChild(new HSeparator());

        // === CLEAR ===
        _clearButton = CreateButton("Clear All Tiles");
        _clearButton.Pressed += OnClearAllTilesPressed;
        vbox.AddChild(_clearButton);

        var clearMonstersBtn = CreateButton("Clear Monsters");
        clearMonstersBtn.Pressed += OnClearPlacementsPressed;
        vbox.AddChild(clearMonstersBtn);

        vbox.AddChild(new HSeparator());

        // === MAP INFO ===
        _mapInfoLabel = new Label();
        _mapInfoLabel.Text = "No map loaded";
        _mapInfoLabel.AddThemeFontSizeOverride("font_size", 11);
        _mapInfoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(_mapInfoLabel);

        // Help text
        var helpLabel = new Label();
        helpLabel.Text = "Shortcuts:\nF=Floor E=Erase R=Room P=Spawn\n1-4=Brush size\nCtrl+Z=Undo Ctrl+Y=Redo\nCtrl+S=Save";
        helpLabel.AddThemeFontSizeOverride("font_size", 10);
        helpLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        vbox.AddChild(helpLabel);

        // Create the new map dialog (initially hidden)
        CreateNewMapDialog();
    }

    private Button CreateToolButton(string text, bool active)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(95, 28);
        btn.ToggleMode = true;
        btn.ButtonPressed = active;
        SetButtonColor(btn, active ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        return btn;
    }

    private Button CreateBrushButton(string text, bool active)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(40, 28);
        btn.ToggleMode = true;
        btn.ButtonPressed = active;
        SetButtonColor(btn, active ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        return btn;
    }

    private void SetButtonColor(Button btn, Color bgColor)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bgColor;
        style.SetBorderWidthAll(1);
        style.BorderColor = new Color(0.4f, 0.4f, 0.45f);
        style.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("pressed", style);
    }

    private void SelectTool(PaintTool tool)
    {
        _currentTool = tool;

        // Update button states
        _floorToolButton!.ButtonPressed = tool == PaintTool.Floor;
        _eraseToolButton!.ButtonPressed = tool == PaintTool.Erase;
        _roomToolButton!.ButtonPressed = tool == PaintTool.Room;
        _spawnToolButton!.ButtonPressed = tool == PaintTool.Spawn;

        SetButtonColor(_floorToolButton, tool == PaintTool.Floor ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_eraseToolButton, tool == PaintTool.Erase ? new Color(0.4f, 0.2f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_roomToolButton, tool == PaintTool.Room ? new Color(0.2f, 0.3f, 0.4f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_spawnToolButton, tool == PaintTool.Spawn ? new Color(0.3f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));

        // Show room type dropdown only for Room tool
        if (_roomTypeDropdown != null)
            _roomTypeDropdown.Visible = tool == PaintTool.Room;

        // Deselect monster when switching to paint tools
        if (tool != PaintTool.Monster)
        {
            _selectedMonsterType = null;
            _selectedGroupTemplate = null;
            _isPlacingMonster = false;
            _isPlacingGroup = false;
            _monsterPalette?.DeselectAll();
            _groupPalette?.DeselectAll();
        }

        _mapDrawArea?.QueueRedraw();
        GD.Print($"[MapEditorTab] Selected tool: {tool}");
    }

    private void SetBrushSize(int index)
    {
        _currentBrushIndex = Mathf.Clamp(index, 0, BrushSizes.Length - 1);

        // Update button states
        _brushSize1Button!.ButtonPressed = index == 0;
        _brushSize3Button!.ButtonPressed = index == 1;
        _brushSize5Button!.ButtonPressed = index == 2;
        _brushSize10Button!.ButtonPressed = index == 3;
        _brushSize20Button!.ButtonPressed = index == 4;

        SetButtonColor(_brushSize1Button, index == 0 ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_brushSize3Button, index == 1 ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_brushSize5Button, index == 2 ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_brushSize10Button, index == 3 ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_brushSize20Button, index == 4 ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));

        _brushSizeLabel!.Text = $"Brush: {CurrentBrushSize}x{CurrentBrushSize}";
        _mapDrawArea?.QueueRedraw();
    }

    private void CreateNewMapDialog()
    {
        _newMapDialog = new Control();
        _newMapDialog.SetAnchorsPreset(LayoutPreset.FullRect);
        _newMapDialog.Visible = false;
        _newMapDialog.ZIndex = 100;
        AddChild(_newMapDialog);

        // Dark overlay
        var overlay = new ColorRect();
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.Color = new Color(0, 0, 0, 0.7f);
        overlay.GuiInput += (e) => { if (e is InputEventMouseButton mb && mb.Pressed) HideNewMapDialog(); };
        _newMapDialog.AddChild(overlay);

        // Dialog panel
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(300, 280);
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.Position = new Vector2(-150, -140);
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.12f, 0.12f, 0.15f);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.4f, 0.4f, 0.45f);
        panelStyle.SetCornerRadiusAll(8);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        _newMapDialog.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);
        margin.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "Create New Map";
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // Width
        var widthBox = new HBoxContainer();
        var widthLabel = new Label { Text = "Width:", CustomMinimumSize = new Vector2(60, 0) };
        _newMapWidthInput = new SpinBox { MinValue = 20, MaxValue = 500, Value = 100, Step = 10 };
        _newMapWidthInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        widthBox.AddChild(widthLabel);
        widthBox.AddChild(_newMapWidthInput);
        vbox.AddChild(widthBox);

        // Depth
        var depthBox = new HBoxContainer();
        var depthLabel = new Label { Text = "Depth:", CustomMinimumSize = new Vector2(60, 0) };
        _newMapDepthInput = new SpinBox { MinValue = 20, MaxValue = 500, Value = 100, Step = 10 };
        _newMapDepthInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        depthBox.AddChild(depthLabel);
        depthBox.AddChild(_newMapDepthInput);
        vbox.AddChild(depthBox);

        // Mode dropdown
        var modeBox = new HBoxContainer();
        var modeLabel = new Label { Text = "Mode:", CustomMinimumSize = new Vector2(60, 0) };
        _newMapModeDropdown = new OptionButton();
        _newMapModeDropdown.AddItem("Tile Painter");
        _newMapModeDropdown.AddItem("Room-Based");
        _newMapModeDropdown.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        modeBox.AddChild(modeLabel);
        modeBox.AddChild(_newMapModeDropdown);
        vbox.AddChild(modeBox);

        // Size presets
        var presetsLabel = new Label { Text = "Presets:" };
        presetsLabel.AddThemeFontSizeOverride("font_size", 12);
        vbox.AddChild(presetsLabel);

        var presetsBox = new HBoxContainer();
        presetsBox.AddThemeConstantOverride("separation", 8);
        var preset50 = CreateButton("50x50");
        preset50.Pressed += () => { _newMapWidthInput.Value = 50; _newMapDepthInput.Value = 50; };
        var preset100 = CreateButton("100x100");
        preset100.Pressed += () => { _newMapWidthInput.Value = 100; _newMapDepthInput.Value = 100; };
        var preset200 = CreateButton("200x200");
        preset200.Pressed += () => { _newMapWidthInput.Value = 200; _newMapDepthInput.Value = 200; };
        presetsBox.AddChild(preset50);
        presetsBox.AddChild(preset100);
        presetsBox.AddChild(preset200);
        vbox.AddChild(presetsBox);

        // Buttons
        var btnBox = new HBoxContainer();
        btnBox.AddThemeConstantOverride("separation", 12);
        var cancelBtn = CreateButton("Cancel");
        cancelBtn.Pressed += HideNewMapDialog;
        cancelBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var createBtn = CreateButton("Create");
        createBtn.Pressed += OnCreateNewMap;
        createBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SetButtonColor(createBtn, new Color(0.2f, 0.4f, 0.2f));
        btnBox.AddChild(cancelBtn);
        btnBox.AddChild(createBtn);
        vbox.AddChild(btnBox);
    }

    private void ShowNewMapDialog()
    {
        _newMapDialog!.Visible = true;
    }

    private void HideNewMapDialog()
    {
        _newMapDialog!.Visible = false;
    }

    private void OnCreateNewMap()
    {
        int width = (int)_newMapWidthInput!.Value;
        int depth = (int)_newMapDepthInput!.Value;
        bool tileMode = _newMapModeDropdown!.Selected == 0;

        _currentMap = new MapDefinition
        {
            Name = "New Dungeon",
            Width = width,
            Depth = depth,
            Mode = tileMode ? "tiles" : "rooms",
            Version = "1.1"
        };

        // Initialize tile grid
        _tileGrid = new int[width, depth];
        _spawnPoint = null;
        _currentMapPath = null;

        // Clear undo/redo
        _undoStack.Clear();
        _redoStack.Clear();

        if (_mapNameInput != null)
            _mapNameInput.Text = "New Dungeon";

        UpdateMapInfo();
        CenterView();
        _mapDrawArea?.QueueRedraw();

        HideNewMapDialog();
        GD.Print($"[MapEditorTab] Created new {(tileMode ? "tile-mode" : "room-mode")} map: {width}x{depth}");
    }

    private void OnClearAllTilesPressed()
    {
        if (_tileGrid == null || _currentMap == null) return;

        // Record for undo
        BeginTileEdit();
        int width = _currentMap.Width;
        int depth = _currentMap.Depth;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (_tileGrid[x, z] != 0)
                {
                    RecordTileChange(x, z, _tileGrid[x, z], 0);
                    _tileGrid[x, z] = 0;
                }
            }
        }
        EndTileEdit();

        _spawnPoint = null;
        _mapDrawArea?.QueueRedraw();
        UpdateMapInfo();
        GD.Print("[MapEditorTab] Cleared all tiles");
    }

    // === UNDO/REDO SYSTEM ===

    private void BeginTileEdit()
    {
        _currentCommand = new TileEditCommand();
    }

    private void RecordTileChange(int x, int z, int oldValue, int newValue)
    {
        if (_currentCommand != null && oldValue != newValue)
        {
            _currentCommand.Changes.Add((x, z, oldValue, newValue));
        }
    }

    private void EndTileEdit()
    {
        if (_currentCommand != null && _currentCommand.Changes.Count > 0)
        {
            _undoStack.Push(_currentCommand);
            _redoStack.Clear(); // Clear redo when new edit is made

            // Limit undo history
            while (_undoStack.Count > MaxUndoHistory)
            {
                // Remove oldest items (have to rebuild stack)
                var list = new List<TileEditCommand>(_undoStack);
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--)
                    _undoStack.Push(list[i]);
            }
        }
        _currentCommand = null;
    }

    private void Undo()
    {
        if (_undoStack.Count == 0 || _tileGrid == null) return;

        var command = _undoStack.Pop();
        foreach (var (x, z, oldValue, newValue) in command.Changes)
        {
            _tileGrid[x, z] = oldValue;
        }
        _redoStack.Push(command);

        _mapDrawArea?.QueueRedraw();
        UpdateMapInfo();
        GD.Print($"[MapEditorTab] Undo: {command.Changes.Count} tiles");
    }

    private void Redo()
    {
        if (_redoStack.Count == 0 || _tileGrid == null) return;

        var command = _redoStack.Pop();
        foreach (var (x, z, oldValue, newValue) in command.Changes)
        {
            _tileGrid[x, z] = newValue;
        }
        _undoStack.Push(command);

        _mapDrawArea?.QueueRedraw();
        UpdateMapInfo();
        GD.Print($"[MapEditorTab] Redo: {command.Changes.Count} tiles");
    }

    private void BuildTileGrid()
    {
        if (_currentMap == null) return;

        int width = _currentMap.Width;
        int depth = _currentMap.Depth;
        _tileGrid = new int[width, depth];

        // Initialize all as void
        for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
                _tileGrid[x, z] = 0;

        // Carve rooms
        foreach (var room in _currentMap.Rooms)
        {
            for (int x = room.Position.X; x < room.Position.X + room.Size.X; x++)
            {
                for (int z = room.Position.Z; z < room.Position.Z + room.Size.Z; z++)
                {
                    if (x >= 0 && x < width && z >= 0 && z < depth)
                        _tileGrid[x, z] = 1;
                }
            }
        }

        // Carve corridors using the same algorithm as MapBuilder
        foreach (var corridor in _currentMap.Corridors)
        {
            if (corridor.FromRoom >= 0 && corridor.FromRoom < _currentMap.Rooms.Count &&
                corridor.ToRoom >= 0 && corridor.ToRoom < _currentMap.Rooms.Count)
            {
                var fromRoom = _currentMap.Rooms[corridor.FromRoom];
                var toRoom = _currentMap.Rooms[corridor.ToRoom];

                int fromX = fromRoom.Position.X + fromRoom.Size.X / 2;
                int fromZ = fromRoom.Position.Z + fromRoom.Size.Z / 2;
                int toX = toRoom.Position.X + toRoom.Size.X / 2;
                int toZ = toRoom.Position.Z + toRoom.Size.Z / 2;

                int corridorWidth = corridor.Width > 0 ? corridor.Width : 5;
                CarveTileGridCorridor(fromX, fromZ, toX, toZ, corridorWidth, width, depth);
            }
        }

        // Apply custom tile overrides
        foreach (var tile in _currentMap.CustomTiles)
        {
            if (tile.X >= 0 && tile.X < width && tile.Z >= 0 && tile.Z < depth)
            {
                _tileGrid[tile.X, tile.Z] = tile.IsFloor ? 1 : 0;
            }
        }

        GD.Print($"[MapEditorTab] Built tile grid: {width}x{depth}, {_currentMap.CustomTiles.Count} custom tiles");
    }

    private void CarveTileGridCorridor(int fromX, int fromZ, int toX, int toZ, int corridorWidth, int width, int depth)
    {
        int halfWidth = corridorWidth / 2;

        // Horizontal segment
        int minX = Mathf.Min(fromX, toX);
        int maxX = Mathf.Max(fromX, toX);
        for (int x = minX - halfWidth; x <= maxX + halfWidth; x++)
        {
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                int z = fromZ + w;
                if (x >= 0 && x < width && z >= 0 && z < depth)
                    _tileGrid![x, z] = 1;
            }
        }

        // Vertical segment
        int minZ = Mathf.Min(fromZ, toZ);
        int maxZ = Mathf.Max(fromZ, toZ);
        for (int z = minZ - halfWidth; z <= maxZ + halfWidth; z++)
        {
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                int x = toX + w;
                if (x >= 0 && x < width && z >= 0 && z < depth)
                    _tileGrid![x, z] = 1;
            }
        }

        // Corner fills
        for (int dx = -halfWidth - 1; dx <= halfWidth + 1; dx++)
        {
            for (int dz = -halfWidth - 1; dz <= halfWidth + 1; dz++)
            {
                int x = toX + dx;
                int z = fromZ + dz;
                if (x >= 0 && x < width && z >= 0 && z < depth)
                    _tileGrid![x, z] = 1;

                x = fromX + dx;
                z = fromZ + dz;
                if (x >= 0 && x < width && z >= 0 && z < depth)
                    _tileGrid![x, z] = 1;

                x = toX + dx;
                z = toZ + dz;
                if (x >= 0 && x < width && z >= 0 && z < depth)
                    _tileGrid![x, z] = 1;
            }
        }
    }

    private void CreateMapViewport(HBoxContainer parent)
    {
        _mapViewport = new Control();
        _mapViewport.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _mapViewport.SizeFlagsVertical = SizeFlags.ExpandFill;
        _mapViewport.ClipContents = true;
        parent.AddChild(_mapViewport);

        // Background
        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = VoidColor;
        _mapViewport.AddChild(bg);

        // Draw area
        _mapDrawArea = new Control();
        _mapDrawArea.SetAnchorsPreset(LayoutPreset.FullRect);
        _mapDrawArea.Draw += OnMapDraw;
        _mapDrawArea.GuiInput += OnMapInput;
        _mapViewport.AddChild(_mapDrawArea);

        // Hint label
        var hintLabel = new Label();
        hintLabel.Text = "Pan: Middle-drag | Zoom: Scroll | Place: Left-click | Remove: Right-click";
        hintLabel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        hintLabel.Position = new Vector2(10, -30);
        hintLabel.AddThemeFontSizeOverride("font_size", 12);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        _mapViewport.AddChild(hintLabel);
    }

    private void CreateRightPanel(HBoxContainer parent)
    {
        var rightPanel = new PanelContainer();
        rightPanel.CustomMinimumSize = new Vector2(200, 0);
        rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var rightStyle = new StyleBoxFlat();
        rightStyle.BgColor = new Color(0.08f, 0.08f, 0.1f);
        rightStyle.BorderWidthLeft = 2;
        rightStyle.BorderColor = new Color(0.2f, 0.2f, 0.25f);
        rightPanel.AddThemeStyleboxOverride("panel", rightStyle);
        parent.AddChild(rightPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        rightPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        // Monster palette title
        var monsterTitle = new Label();
        monsterTitle.Text = "Monsters";
        monsterTitle.AddThemeFontSizeOverride("font_size", 16);
        monsterTitle.AddThemeColorOverride("font_color", EnemyColor);
        vbox.AddChild(monsterTitle);

        // Monster palette
        _monsterPalette = new ItemList();
        _monsterPalette.CustomMinimumSize = new Vector2(0, 200);
        _monsterPalette.SizeFlagsVertical = SizeFlags.ExpandFill;
        _monsterPalette.ItemSelected += OnMonsterSelected;

        // Add monsters to palette
        foreach (var type in MonsterTypes)
        {
            _monsterPalette.AddItem(type.Replace("_", " ").Capitalize());
        }
        // Add bosses with different color
        foreach (var type in BossTypes)
        {
            int idx = _monsterPalette.AddItem(type.Replace("_", " ").Capitalize());
            _monsterPalette.SetItemCustomBgColor(idx, new Color(0.3f, 0.15f, 0.1f));
        }
        vbox.AddChild(_monsterPalette);

        // Group templates title
        var groupTitle = new Label();
        groupTitle.Text = "Groups";
        groupTitle.AddThemeFontSizeOverride("font_size", 16);
        groupTitle.AddThemeColorOverride("font_color", GroupColor);
        vbox.AddChild(groupTitle);

        // Group palette
        _groupPalette = new ItemList();
        _groupPalette.CustomMinimumSize = new Vector2(0, 150);
        _groupPalette.ItemSelected += OnGroupSelected;

        // Add default group templates
        var templates = DefaultGroupTemplates.GetDefaultTemplates();
        foreach (var template in templates)
        {
            _groupPalette.AddItem(template.Name);
        }
        vbox.AddChild(_groupPalette);

        // Selected item label
        _selectedItemLabel = new Label();
        _selectedItemLabel.Text = "Select to place";
        _selectedItemLabel.AddThemeFontSizeOverride("font_size", 12);
        _selectedItemLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(_selectedItemLabel);

        // Deselect button
        var deselectBtn = CreateButton("Deselect");
        deselectBtn.Pressed += () =>
        {
            _selectedMonsterType = null;
            _selectedGroupTemplate = null;
            _isPlacingMonster = false;
            _isPlacingGroup = false;
            _monsterPalette?.DeselectAll();
            _groupPalette?.DeselectAll();
            UpdateSelectedLabel();
        };
        vbox.AddChild(deselectBtn);
    }

    private Button CreateButton(string text)
    {
        var button = new Button();
        button.Text = text;
        button.CustomMinimumSize = new Vector2(0, 32);
        button.FocusMode = FocusModeEnum.All;
        button.MouseFilter = MouseFilterEnum.Stop;

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.2f, 0.2f, 0.25f);
        normalStyle.SetBorderWidthAll(1);
        normalStyle.BorderColor = new Color(0.4f, 0.4f, 0.45f);
        normalStyle.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.25f, 0.25f, 0.3f);
        hoverStyle.SetBorderWidthAll(1);
        hoverStyle.BorderColor = new Color(1f, 0.85f, 0.2f);
        hoverStyle.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("hover", hoverStyle);

        GD.Print($"[MapEditorTab] Created button: {text}");
        return button;
    }

    private void RefreshSavedMapsList()
    {
        GD.Print($"[MapEditorTab] RefreshSavedMapsList called, _savedMapsList is {(_savedMapsList == null ? "NULL" : "valid")}");
        if (_savedMapsList == null)
        {
            GD.PrintErr("[MapEditorTab] _savedMapsList is null, cannot refresh");
            return;
        }

        _savedMapsList.Clear();
        _mapFilePaths.Clear();

        GD.Print("[MapEditorTab] Calling GetSavedMapFilesWithPaths...");
        var maps = MapDefinition.GetSavedMapFilesWithPaths();
        GD.Print($"[MapEditorTab] Got {maps.Count} maps from GetSavedMapFilesWithPaths");

        foreach (var (name, path) in maps)
        {
            _savedMapsList.AddItem(name);
            _mapFilePaths.Add(path);
            GD.Print($"[MapEditorTab] Added map to list: {name} at {path}");
        }

        GD.Print($"[MapEditorTab] List now has {_savedMapsList.ItemCount} items");
    }

    private void OnMapSelected(long index)
    {
        // Just selecting, not loading yet
    }

    private void OnMapDoubleClicked(long index)
    {
        // Double-click to load
        OnLoadMapPressed();
    }

    private void OnLoadMapPressed()
    {
        GD.Print("[MapEditorTab] Load Map button pressed!");
        if (_savedMapsList == null)
        {
            GD.Print("[MapEditorTab] savedMapsList is null");
            return;
        }

        var selected = _savedMapsList.GetSelectedItems();
        if (selected.Length == 0)
        {
            GD.Print("[MapEditorTab] No map selected in list");
            return;
        }

        int selectedIndex = selected[0];
        if (selectedIndex < 0 || selectedIndex >= _mapFilePaths.Count)
        {
            GD.Print($"[MapEditorTab] Invalid selection index: {selectedIndex}");
            return;
        }

        string path = _mapFilePaths[selectedIndex];
        string mapName = _savedMapsList.GetItemText(selectedIndex);
        GD.Print($"[MapEditorTab] Loading map '{mapName}' from: {path}");

        LoadMapFromPath(path);
    }

    private void OnDeleteMapPressed()
    {
        if (_savedMapsList == null) return;

        var selected = _savedMapsList.GetSelectedItems();
        if (selected.Length == 0)
        {
            GD.Print("[MapEditorTab] No map selected to delete");
            return;
        }

        int selectedIndex = selected[0];
        if (selectedIndex < 0 || selectedIndex >= _mapFilePaths.Count)
        {
            GD.Print($"[MapEditorTab] Invalid selection index: {selectedIndex}");
            return;
        }

        string path = _mapFilePaths[selectedIndex];
        string mapName = _savedMapsList.GetItemText(selectedIndex);

        // Don't allow deleting bundled maps
        if (path.StartsWith("res://"))
        {
            GD.Print("[MapEditorTab] Cannot delete bundled maps");
            return;
        }

        // Show confirmation dialog
        _mapPathToDelete = path;
        ShowDeleteConfirmDialog(mapName);
    }

    private void ShowDeleteConfirmDialog(string mapName)
    {
        // Create dialog if it doesn't exist
        if (_deleteConfirmDialog == null)
        {
            CreateDeleteConfirmDialog();
        }

        // Update the message
        var messageLabel = _deleteConfirmDialog?.GetNode<Label>("Panel/VBox/Message");
        if (messageLabel != null)
        {
            messageLabel.Text = $"Are you sure you want to delete\n\"{mapName}\"?\n\nThis cannot be undone.";
        }

        _deleteConfirmDialog!.Visible = true;
    }

    private void CreateDeleteConfirmDialog()
    {
        _deleteConfirmDialog = new Control();
        _deleteConfirmDialog.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _deleteConfirmDialog.Visible = false;
        AddChild(_deleteConfirmDialog);

        // Semi-transparent background
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.7f);
        _deleteConfirmDialog.AddChild(bg);

        // Dialog panel
        var panel = new PanelContainer();
        panel.Name = "Panel";
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Size = new Vector2(350, 180);
        panel.Position = new Vector2(-175, -90);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.15f, 0.15f, 0.2f);
        style.BorderColor = new Color(0.8f, 0.3f, 0.3f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        panel.AddThemeStyleboxOverride("panel", style);
        _deleteConfirmDialog.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.Name = "VBox";
        vbox.AddThemeConstantOverride("separation", 15);
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vbox.OffsetLeft = 20;
        vbox.OffsetRight = -20;
        vbox.OffsetTop = 20;
        vbox.OffsetBottom = -20;
        panel.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "Delete Map?";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
        vbox.AddChild(title);

        // Message
        var message = new Label();
        message.Name = "Message";
        message.Text = "Are you sure?";
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(message);

        // Spacer
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        // Buttons
        var btnBox = new HBoxContainer();
        btnBox.AddThemeConstantOverride("separation", 20);
        btnBox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnBox);

        var yesBtn = new Button();
        yesBtn.Text = "Yes, Delete";
        yesBtn.CustomMinimumSize = new Vector2(100, 35);
        yesBtn.AddThemeFontSizeOverride("font_size", 14);
        yesBtn.Pressed += OnDeleteConfirmed;
        btnBox.AddChild(yesBtn);

        var noBtn = new Button();
        noBtn.Text = "No, Cancel";
        noBtn.CustomMinimumSize = new Vector2(100, 35);
        noBtn.AddThemeFontSizeOverride("font_size", 14);
        noBtn.Pressed += OnDeleteCancelled;
        btnBox.AddChild(noBtn);
    }

    private void OnDeleteConfirmed()
    {
        if (_mapPathToDelete != null)
        {
            bool success = MapDefinition.DeleteMap(_mapPathToDelete);
            if (success)
            {
                // If we deleted the currently loaded map, clear it
                if (_currentMapPath == _mapPathToDelete)
                {
                    _currentMap = null;
                    _currentMapPath = null;
                    _tileGrid = null;
                    if (_mapNameInput != null) _mapNameInput.Text = "";
                    _mapDrawArea?.QueueRedraw();
                    UpdateMapInfo();
                }

                RefreshSavedMapsList();
                GD.Print($"[MapEditorTab] Deleted map: {_mapPathToDelete}");
            }
        }

        _mapPathToDelete = null;
        _deleteConfirmDialog!.Visible = false;
    }

    private void OnDeleteCancelled()
    {
        _mapPathToDelete = null;
        _deleteConfirmDialog!.Visible = false;
    }

    /// <summary>
    /// Public method to reload/refresh the map editor with a specific map file.
    /// Used by InMapEditor when returning to the editor after editing in 3D.
    /// </summary>
    public void RefreshFromFile(string mapPath)
    {
        GD.Print($"[MapEditorTab] Refreshing from file: {mapPath}");
        LoadMapFromPath(mapPath);

        // Force redraw
        _mapDrawArea?.QueueRedraw();
    }

    private void LoadMapFromPath(string path)
    {
        _currentMap = MapDefinition.LoadFromJson(path);
        if (_currentMap == null)
        {
            GD.PrintErr($"[MapEditorTab] Failed to load map from: {path}");
            return;
        }

        _currentMapPath = path;
        if (_mapNameInput != null)
            _mapNameInput.Text = _currentMap.Name;

        // Clear undo/redo
        _undoStack.Clear();
        _redoStack.Clear();

        // Load tile grid based on mode
        if (_currentMap.IsTileMode && !string.IsNullOrEmpty(_currentMap.TileData))
        {
            // Decode tile data
            _tileGrid = TileDataEncoder.Decode(_currentMap.TileData, _currentMap.Width, _currentMap.Depth);
            int floorCount = TileDataEncoder.CountFloorTiles(_tileGrid);
            GD.Print($"[MapEditorTab] Decoded tile-mode map: {floorCount} floor tiles");

            // Debug: Log some sample tiles after decoding
            int w = _currentMap.Width;
            int d = _currentMap.Depth;
            GD.Print($"[MapEditorTab] LOAD: Grid size {w}x{d}");
            GD.Print($"[MapEditorTab] LOAD: Corner tiles: (0,0)={_tileGrid[0, 0]}, ({w - 1},0)={_tileGrid[w - 1, 0]}, (0,{d - 1})={_tileGrid[0, d - 1]}, ({w - 1},{d - 1})={_tileGrid[w - 1, d - 1]}");

            // Find first floor tile for reference
            for (int z = 0; z < d && z < 20; z++)
            {
                for (int x = 0; x < w && x < 20; x++)
                {
                    if (_tileGrid[x, z] == 1)
                    {
                        GD.Print($"[MapEditorTab] LOAD: First floor tile at ({x},{z})");
                        goto DoneSearchingLoad;
                    }
                }
            }
            DoneSearchingLoad:;
        }
        else
        {
            // Build tile grid from rooms/corridors
            BuildTileGrid();
            GD.Print($"[MapEditorTab] Built tile grid from room-mode map");
        }

        // Load spawn point
        if (_currentMap.SpawnPosition != null)
        {
            _spawnPoint = new Vector2I(_currentMap.SpawnPosition.X, _currentMap.SpawnPosition.Z);
        }
        else if (_currentMap.Rooms.Count > 0)
        {
            // Default spawn to center of first room
            var firstRoom = _currentMap.Rooms[0];
            _spawnPoint = new Vector2I(
                firstRoom.Position.X + firstRoom.Size.X / 2,
                firstRoom.Position.Z + firstRoom.Size.Z / 2
            );
        }
        else
        {
            _spawnPoint = null;
        }

        // Refresh room list and reset selection
        _selectedRoomIndex = -1;
        if (_deleteRoomButton != null)
            _deleteRoomButton.Disabled = true;
        RefreshRoomList();

        UpdateMapInfo();
        CenterView();
        _mapDrawArea?.QueueRedraw();
        GD.Print($"[MapEditorTab] Loaded map: {_currentMap.Name} (mode={_currentMap.Mode})");
    }

    private void OnLoadTestMapPressed()
    {
        GD.Print("[MapEditorTab] Load Test Map button pressed!");
        string path = "C:/Claude/SafeRoom3D/bugs/dungeon_map.json";
        GD.Print($"[MapEditorTab] Loading test map from: {path}");

        _currentMap = MapDefinition.LoadFromJson(path);
        if (_currentMap != null)
        {
            _currentMapPath = path;
            if (_mapNameInput != null)
                _mapNameInput.Text = _currentMap.Name;
            UpdateMapInfo();
            CenterView();
            _mapDrawArea?.QueueRedraw();
            GD.Print($"[MapEditorTab] Loaded test map: {_currentMap.Name}");
        }
        else
        {
            GD.PrintErr("[MapEditorTab] Failed to load test map");
        }
    }

    private void OnNewMapPressed()
    {
        GD.Print("[MapEditorTab] New Map button pressed!");
        // Create a new sample map
        _currentMap = MapParser.CreateSampleMap();
        _currentMapPath = null;
        if (_mapNameInput != null)
            _mapNameInput.Text = "New Dungeon";
        UpdateMapInfo();
        CenterView();
        _mapDrawArea?.QueueRedraw();
        GD.Print("[MapEditorTab] Created new map");
    }

    private void OnSaveMapPressed()
    {
        GD.Print("[MapEditorTab] === SAVE MAP PRESSED ===");

        if (_currentMap == null)
        {
            GD.PrintErr("[MapEditorTab] No map to save - _currentMap is null");
            return;
        }

        string name = _mapNameInput?.Text ?? "Unnamed";
        GD.Print($"[MapEditorTab] Map name from input: '{name}'");

        if (string.IsNullOrWhiteSpace(name))
            name = "Unnamed";

        _currentMap.Name = name;
        _currentMap.Version = "1.1";

        GD.Print($"[MapEditorTab] Saving map: Name='{name}', Mode='{_currentMap.Mode}', IsTileMode={_currentMap.IsTileMode}, Size={_currentMap.Width}x{_currentMap.Depth}");

        // Handle tile-mode maps
        if (_currentMap.IsTileMode && _tileGrid != null)
        {
            // Debug: Log some sample tiles before encoding
            int w = _currentMap.Width;
            int d = _currentMap.Depth;
            GD.Print($"[MapEditorTab] SAVE: Grid size {w}x{d}");
            GD.Print($"[MapEditorTab] SAVE: Corner tiles: (0,0)={_tileGrid[0, 0]}, ({w - 1},0)={_tileGrid[w - 1, 0]}, (0,{d - 1})={_tileGrid[0, d - 1]}, ({w - 1},{d - 1})={_tileGrid[w - 1, d - 1]}");

            // Find first floor tile for reference
            for (int z = 0; z < d && z < 20; z++)
            {
                for (int x = 0; x < w && x < 20; x++)
                {
                    if (_tileGrid[x, z] == 1)
                    {
                        GD.Print($"[MapEditorTab] SAVE: First floor tile at ({x},{z})");
                        goto DoneSearchingSave;
                    }
                }
            }
            DoneSearchingSave:

            // Encode tile data
            _currentMap.TileData = TileDataEncoder.Encode(_tileGrid);

            // Set spawn position
            if (_spawnPoint.HasValue)
            {
                _currentMap.SpawnPosition = new PositionData
                {
                    X = _spawnPoint.Value.X,
                    Z = _spawnPoint.Value.Y
                };
            }

            // Clear room-mode data (not needed for tile mode)
            // Keep rooms for reference but they're optional
            _currentMap.Corridors.Clear();
            _currentMap.CustomTiles.Clear();

            GD.Print($"[MapEditorTab] Encoded {TileDataEncoder.CountFloorTiles(_tileGrid)} floor tiles");
        }
        else if (_tileGrid != null)
        {
            // Room-mode map with tile edits - save as custom tiles
            _currentMap.CustomTiles.Clear();
            int width = _currentMap.Width;
            int depth = _currentMap.Depth;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    bool isFloor = _tileGrid[x, z] == 1;
                    _currentMap.CustomTiles.Add(new TileOverride { X = x, Z = z, IsFloor = isFloor });
                }
            }
        }

        string path = $"user://maps/{name}.json";
        GD.Print($"[MapEditorTab] Attempting to save to path: {path}");

        bool saveResult = _currentMap.SaveToJson(path);
        GD.Print($"[MapEditorTab] SaveToJson returned: {saveResult}");

        if (saveResult)
        {
            _currentMapPath = path;
            GD.Print($"[MapEditorTab] Calling RefreshSavedMapsList...");
            RefreshSavedMapsList();
            GD.Print($"[MapEditorTab] Saved map to: {path}");
        }
        else
        {
            GD.PrintErr($"[MapEditorTab] FAILED to save map to: {path}");
        }
    }

    private void OnPlayMapPressed()
    {
        if (_currentMap == null)
        {
            GD.PrintErr("[MapEditorTab] No map to play");
            return;
        }

        // Save first if needed
        if (_currentMapPath == null)
        {
            OnSaveMapPressed();
        }

        // Set the custom map path for GameManager to use
        SplashScreen3D.CustomMapPath = _currentMapPath;

        // Close editor and start game
        var editorScreen = GetParent()?.GetParent()?.GetParent()?.GetParent() as EditorScreen3D;
        editorScreen?.Hide();

        // Transition to game
        GetTree().ChangeSceneToFile("res://Scenes/Main3D.tscn");

        GD.Print($"[MapEditorTab] Starting game with map: {_currentMapPath}");
    }

    private void OnEnterEditModePressed()
    {
        if (_currentMap == null)
        {
            GD.PrintErr("[MapEditorTab] No map to edit");
            return;
        }

        // Save first if needed
        if (_currentMapPath == null)
        {
            OnSaveMapPressed();
        }

        // Set the custom map path for GameManager to use
        SplashScreen3D.CustomMapPath = _currentMapPath;

        // Hide editor temporarily
        var editorScreen = GetParent()?.GetParent()?.GetParent()?.GetParent() as EditorScreen3D;
        editorScreen?.Hide();

        // Load the map in game, then enter edit mode
        // We need to start the game scene first, then activate edit mode
        SplashScreen3D.EnterEditModeAfterLoad = true;
        SplashScreen3D.EditModeMap = _currentMap;
        SplashScreen3D.EditModeMapPath = _currentMapPath;
        GetTree().ChangeSceneToFile("res://Scenes/Main3D.tscn");

        GD.Print($"[MapEditorTab] Entering edit mode for map: {_currentMapPath}");
    }

    private void OnClearPlacementsPressed()
    {
        if (_currentMap == null) return;

        _currentMap.Enemies.Clear();
        _currentMap.MonsterGroups.Clear();
        UpdateMapInfo();
        _mapDrawArea?.QueueRedraw();
        GD.Print("[MapEditorTab] Cleared all placements");
    }

    private void OnRoomListSelected(long index)
    {
        _selectedRoomIndex = (int)index;
        if (_deleteRoomButton != null)
            _deleteRoomButton.Disabled = _selectedRoomIndex < 0;
        _mapDrawArea?.QueueRedraw();
        GD.Print($"[MapEditorTab] Selected room {_selectedRoomIndex}");
    }

    private void OnDeleteSelectedRoom()
    {
        if (_currentMap == null || _selectedRoomIndex < 0 || _selectedRoomIndex >= _currentMap.Rooms.Count)
            return;

        var room = _currentMap.Rooms[_selectedRoomIndex];
        GD.Print($"[MapEditorTab] Deleting room {_selectedRoomIndex}: {room.Type} at ({room.Position.X}, {room.Position.Z})");

        _currentMap.Rooms.RemoveAt(_selectedRoomIndex);
        _selectedRoomIndex = -1;

        if (_deleteRoomButton != null)
            _deleteRoomButton.Disabled = true;

        RefreshRoomList();
        UpdateMapInfo();
        _mapDrawArea?.QueueRedraw();
    }

    private void OnClearAllRooms()
    {
        if (_currentMap == null || _currentMap.Rooms.Count == 0)
            return;

        int count = _currentMap.Rooms.Count;
        _currentMap.Rooms.Clear();
        _selectedRoomIndex = -1;

        if (_deleteRoomButton != null)
            _deleteRoomButton.Disabled = true;

        RefreshRoomList();
        UpdateMapInfo();
        _mapDrawArea?.QueueRedraw();
        GD.Print($"[MapEditorTab] Cleared all {count} rooms");
    }

    private void RefreshRoomList()
    {
        if (_roomList == null || _currentMap == null) return;

        _roomList.Clear();
        for (int i = 0; i < _currentMap.Rooms.Count; i++)
        {
            var room = _currentMap.Rooms[i];
            string label = $"Room {i + 1}: {room.Type} ({room.Size.X}x{room.Size.Z})";
            if (room.IsSpawnRoom)
                label += " [SPAWN]";
            _roomList.AddItem(label);
        }
    }

    private void SelectRoomAtTile(int x, int z)
    {
        if (_currentMap == null) return;

        // Find room containing this tile
        for (int i = 0; i < _currentMap.Rooms.Count; i++)
        {
            var room = _currentMap.Rooms[i];
            if (x >= room.Position.X && x < room.Position.X + room.Size.X &&
                z >= room.Position.Z && z < room.Position.Z + room.Size.Z)
            {
                _selectedRoomIndex = i;
                if (_roomList != null && i < _roomList.ItemCount)
                    _roomList.Select(i);
                if (_deleteRoomButton != null)
                    _deleteRoomButton.Disabled = false;
                _mapDrawArea?.QueueRedraw();
                GD.Print($"[MapEditorTab] Clicked on room {i}");
                return;
            }
        }

        // Clicked outside any room - deselect
        _selectedRoomIndex = -1;
        _roomList?.DeselectAll();
        if (_deleteRoomButton != null)
            _deleteRoomButton.Disabled = true;
        _mapDrawArea?.QueueRedraw();
    }

    private void OnMonsterSelected(long index)
    {
        // Deselect group
        _groupPalette?.DeselectAll();
        _selectedGroupTemplate = null;
        _isPlacingGroup = false;

        // Get monster type
        int totalMonsters = MonsterTypes.Length;
        if (index < totalMonsters)
        {
            _selectedMonsterType = MonsterTypes[index];
        }
        else
        {
            _selectedMonsterType = BossTypes[index - totalMonsters];
        }
        _isPlacingMonster = true;

        // Switch to Monster tool when monster is selected
        _currentTool = PaintTool.Monster;
        UpdateToolButtonStates();

        UpdateSelectedLabel();
    }

    private void UpdateToolButtonStates()
    {
        _floorToolButton!.ButtonPressed = _currentTool == PaintTool.Floor;
        _eraseToolButton!.ButtonPressed = _currentTool == PaintTool.Erase;
        _roomToolButton!.ButtonPressed = _currentTool == PaintTool.Room;
        _spawnToolButton!.ButtonPressed = _currentTool == PaintTool.Spawn;

        SetButtonColor(_floorToolButton, _currentTool == PaintTool.Floor ? new Color(0.2f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_eraseToolButton, _currentTool == PaintTool.Erase ? new Color(0.4f, 0.2f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_roomToolButton, _currentTool == PaintTool.Room ? new Color(0.2f, 0.3f, 0.4f) : new Color(0.2f, 0.2f, 0.25f));
        SetButtonColor(_spawnToolButton, _currentTool == PaintTool.Spawn ? new Color(0.3f, 0.4f, 0.2f) : new Color(0.2f, 0.2f, 0.25f));

        if (_roomTypeDropdown != null)
            _roomTypeDropdown.Visible = _currentTool == PaintTool.Room;
    }

    private void OnGroupSelected(long index)
    {
        // Deselect monster
        _monsterPalette?.DeselectAll();
        _selectedMonsterType = null;
        _isPlacingMonster = false;

        // Get group template
        var templates = DefaultGroupTemplates.GetDefaultTemplates();
        if (index >= 0 && index < templates.Count)
        {
            _selectedGroupTemplate = templates[(int)index];
            _isPlacingGroup = true;
        }

        // Switch to Monster tool when group is selected
        _currentTool = PaintTool.Monster;
        UpdateToolButtonStates();

        UpdateSelectedLabel();
    }

    private void UpdateSelectedLabel()
    {
        if (_selectedItemLabel == null) return;

        if (_isPlacingMonster && _selectedMonsterType != null)
        {
            bool isBoss = System.Array.IndexOf(BossTypes, _selectedMonsterType) >= 0;
            _selectedItemLabel.Text = $"Placing: {_selectedMonsterType.Replace("_", " ").Capitalize()}" +
                                      (isBoss ? " (BOSS)" : "");
        }
        else if (_isPlacingGroup && _selectedGroupTemplate != null)
        {
            _selectedItemLabel.Text = $"Placing Group: {_selectedGroupTemplate.Name}\n" +
                                      $"({_selectedGroupTemplate.Monsters.Count} monsters)";
        }
        else
        {
            _selectedItemLabel.Text = "Select to place";
        }
    }

    private void UpdateMapInfo()
    {
        if (_mapInfoLabel == null) return;

        if (_currentMap == null)
        {
            _mapInfoLabel.Text = "No map loaded";
            return;
        }

        int totalEnemies = _currentMap.Enemies.Count;
        foreach (var group in _currentMap.MonsterGroups)
        {
            totalEnemies += group.Monsters.Count;
        }

        int floorCount = _tileGrid != null ? TileDataEncoder.CountFloorTiles(_tileGrid) : 0;
        string modeText = _currentMap.IsTileMode ? "Tile Painter" : "Room-Based";
        string spawnText = _spawnPoint.HasValue ? $"({_spawnPoint.Value.X}, {_spawnPoint.Value.Y})" : "Not set";

        _mapInfoLabel.Text = $"Mode: {modeText}\n" +
                             $"Size: {_currentMap.Width}x{_currentMap.Depth}\n" +
                             $"Floor tiles: {floorCount}\n" +
                             $"Rooms: {_currentMap.Rooms.Count}\n" +
                             $"Spawn: {spawnText}\n" +
                             $"Enemies: {totalEnemies}";
    }

    private void CenterView()
    {
        if (_currentMap == null || _mapViewport == null) return;

        // Center the view on the map
        float mapCenterX = _currentMap.Width / 2f;
        float mapCenterZ = _currentMap.Depth / 2f;

        var viewSize = _mapViewport.Size;
        _viewOffset = new Vector2(
            viewSize.X / 2f - mapCenterX * _viewScale,
            viewSize.Y / 2f - mapCenterZ * _viewScale
        );
    }

    private void OnMapInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _isPanning = mouseButton.Pressed;
                _lastMousePos = mouseButton.Position;
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                // Zoom in
                float oldScale = _viewScale;
                _viewScale = Mathf.Clamp(_viewScale * 1.2f, 1f, 20f);
                var mousePos = mouseButton.Position;
                _viewOffset = mousePos - (mousePos - _viewOffset) * (_viewScale / oldScale);
                _mapDrawArea?.QueueRedraw();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                // Zoom out
                float oldScale = _viewScale;
                _viewScale = Mathf.Clamp(_viewScale / 1.2f, 1f, 20f);
                var mousePos = mouseButton.Position;
                _viewOffset = mousePos - (mousePos - _viewOffset) * (_viewScale / oldScale);
                _mapDrawArea?.QueueRedraw();
            }
            else if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                // Ctrl+Click to select room
                if (mouseButton.Pressed && mouseButton.CtrlPressed)
                {
                    var tile = ScreenToTile(mouseButton.Position);
                    SelectRoomAtTile(tile.X, tile.Y);
                }
                else
                {
                    HandleLeftClick(mouseButton);
                }
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                HandleRightClick(mouseButton);
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            HandleMouseMotion(motion);
        }
    }

    private void HandleLeftClick(InputEventMouseButton mouseButton)
    {
        if (_currentMap == null || _tileGrid == null) return;

        var tile = ScreenToTile(mouseButton.Position);
        GD.Print($"[MapEditorTab] Click at screen ({mouseButton.Position.X:F0},{mouseButton.Position.Y:F0}) -> tile ({tile.X},{tile.Y})");

        if (mouseButton.Pressed)
        {
            switch (_currentTool)
            {
                case PaintTool.Floor:
                    BeginTileEdit();
                    _isPaintingFloor = true;
                    GD.Print($"[MapEditorTab] PAINT: Setting tile ({tile.X},{tile.Y}) = 1");
                    PaintBrushAt(tile.X, tile.Y, 1);
                    break;

                case PaintTool.Erase:
                    BeginTileEdit();
                    _isPaintingVoid = true;
                    PaintBrushAt(tile.X, tile.Y, 0);
                    break;

                case PaintTool.Room:
                    _isDrawingRoom = true;
                    _roomStartTile = tile;
                    _roomEndTile = tile;
                    break;

                case PaintTool.Spawn:
                    PlaceSpawnPoint(tile);
                    break;

                case PaintTool.Monster:
                    PlaceAtMouse(mouseButton.Position);
                    break;
            }
        }
        else // Mouse released
        {
            if (_isPaintingFloor || _isPaintingVoid)
            {
                EndTileEdit();
                _isPaintingFloor = false;
                _isPaintingVoid = false;
            }

            if (_isDrawingRoom)
            {
                FinishRoomDraw();
            }
        }

        _mapDrawArea?.QueueRedraw();
    }

    private void HandleRightClick(InputEventMouseButton mouseButton)
    {
        if (_currentMap == null || _tileGrid == null) return;

        var tile = ScreenToTile(mouseButton.Position);

        if (mouseButton.Pressed)
        {
            // Cancel room drawing
            if (_isDrawingRoom)
            {
                _isDrawingRoom = false;
                _mapDrawArea?.QueueRedraw();
                return;
            }

            // Erase with any tool (convenient shortcut)
            if (_currentTool == PaintTool.Floor || _currentTool == PaintTool.Erase)
            {
                BeginTileEdit();
                _isPaintingVoid = true;
                PaintBrushAt(tile.X, tile.Y, 0);
            }
            else
            {
                // Remove monster
                RemoveAtMouse(mouseButton.Position);
            }
        }
        else
        {
            if (_isPaintingVoid)
            {
                EndTileEdit();
                _isPaintingVoid = false;
            }
        }

        _mapDrawArea?.QueueRedraw();
    }

    private void HandleMouseMotion(InputEventMouseMotion motion)
    {
        if (_isPanning)
        {
            _viewOffset += motion.Position - _lastMousePos;
            _lastMousePos = motion.Position;
            _mapDrawArea?.QueueRedraw();
        }

        _placementPreviewPos = motion.Position;

        if (_currentMap == null || _tileGrid == null) return;

        var tile = ScreenToTile(motion.Position);

        // Paint while dragging
        if (_isPaintingFloor)
        {
            PaintBrushAt(tile.X, tile.Y, 1);
            _mapDrawArea?.QueueRedraw();
        }
        else if (_isPaintingVoid)
        {
            PaintBrushAt(tile.X, tile.Y, 0);
            _mapDrawArea?.QueueRedraw();
        }

        // Update room preview while dragging
        if (_isDrawingRoom)
        {
            _roomEndTile = tile;
            _mapDrawArea?.QueueRedraw();
        }

        // Update coordinate label
        UpdateTileCoordLabel(motion.Position);

        // Redraw for brush/placement preview
        if (_currentTool != PaintTool.Monster || _isPlacingMonster || _isPlacingGroup)
        {
            _mapDrawArea?.QueueRedraw();
        }
    }

    private void PaintBrushAt(int centerX, int centerZ, int value)
    {
        if (_tileGrid == null || _currentMap == null) return;

        int halfBrush = CurrentBrushSize / 2;
        int width = _currentMap.Width;
        int depth = _currentMap.Depth;

        for (int dx = -halfBrush; dx <= halfBrush; dx++)
        {
            for (int dz = -halfBrush; dz <= halfBrush; dz++)
            {
                int x = centerX + dx;
                int z = centerZ + dz;

                if (x >= 0 && x < width && z >= 0 && z < depth)
                {
                    int oldValue = _tileGrid[x, z];
                    if (oldValue != value)
                    {
                        RecordTileChange(x, z, oldValue, value);
                        _tileGrid[x, z] = value;
                    }
                }
            }
        }
    }

    private void PlaceSpawnPoint(Vector2I tile)
    {
        if (_tileGrid == null || _currentMap == null) return;

        // Check if on floor
        if (tile.X >= 0 && tile.X < _currentMap.Width &&
            tile.Y >= 0 && tile.Y < _currentMap.Depth)
        {
            _spawnPoint = tile;
            _currentMap.SpawnPosition = new PositionData { X = tile.X, Z = tile.Y };
            _mapDrawArea?.QueueRedraw();
            GD.Print($"[MapEditorTab] Spawn point set at ({tile.X}, {tile.Y})");
        }
    }

    private void FinishRoomDraw()
    {
        _isDrawingRoom = false;

        if (_tileGrid == null || _currentMap == null) return;

        int minX = Mathf.Min(_roomStartTile.X, _roomEndTile.X);
        int maxX = Mathf.Max(_roomStartTile.X, _roomEndTile.X);
        int minZ = Mathf.Min(_roomStartTile.Y, _roomEndTile.Y);
        int maxZ = Mathf.Max(_roomStartTile.Y, _roomEndTile.Y);

        // Clamp to map bounds
        minX = Mathf.Clamp(minX, 0, _currentMap.Width - 1);
        maxX = Mathf.Clamp(maxX, 0, _currentMap.Width - 1);
        minZ = Mathf.Clamp(minZ, 0, _currentMap.Depth - 1);
        maxZ = Mathf.Clamp(maxZ, 0, _currentMap.Depth - 1);

        // Minimum room size of 3x3
        if (maxX - minX < 2 || maxZ - minZ < 2)
        {
            GD.Print("[MapEditorTab] Room too small (minimum 3x3)");
            return;
        }

        BeginTileEdit();

        // Fill with floor tiles
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                int oldValue = _tileGrid[x, z];
                if (oldValue != 1)
                {
                    RecordTileChange(x, z, oldValue, 1);
                    _tileGrid[x, z] = 1;
                }
            }
        }

        EndTileEdit();

        // Add room to map definition (for room-mode compatibility)
        var room = new RoomDefinition
        {
            Id = _currentMap.Rooms.Count,
            Position = new PositionData { X = minX, Z = minZ },
            Size = new SizeData { X = maxX - minX + 1, Z = maxZ - minZ + 1 },
            Type = _roomType
        };
        _currentMap.Rooms.Add(room);

        // Refresh room list to show new room
        RefreshRoomList();

        _mapDrawArea?.QueueRedraw();
        UpdateMapInfo();
        GD.Print($"[MapEditorTab] Created room: {room.Size.X}x{room.Size.Z} at ({minX}, {minZ}) type={_roomType}");
    }

    private Vector2I ScreenToTile(Vector2 screenPos)
    {
        int tileX = (int)((screenPos.X - _viewOffset.X) / _viewScale);
        int tileZ = (int)((screenPos.Y - _viewOffset.Y) / _viewScale);
        return new Vector2I(tileX, tileZ);
    }

    private Vector2 TileToScreen(int tileX, int tileZ)
    {
        return new Vector2(
            tileX * _viewScale + _viewOffset.X,
            tileZ * _viewScale + _viewOffset.Y
        );
    }

    private void PlaceAtMouse(Vector2 mousePos)
    {
        if (_currentMap == null) return;

        var tile = ScreenToTile(mousePos);

        // Check if tile is within map bounds
        if (tile.X < 0 || tile.X >= _currentMap.Width || tile.Y < 0 || tile.Y >= _currentMap.Depth)
            return;

        // Check if tile is on floor (in a room)
        if (!IsTileFloor(tile.X, tile.Y))
            return;

        if (_isPlacingMonster && _selectedMonsterType != null)
        {
            // Place individual monster
            bool isBoss = System.Array.IndexOf(BossTypes, _selectedMonsterType) >= 0;
            var placement = new EnemyPlacement
            {
                Type = _selectedMonsterType,
                Position = new PositionData { X = tile.X, Z = tile.Y },
                RoomId = GetRoomAtTile(tile.X, tile.Y),
                Level = 1,
                IsBoss = isBoss
            };
            _currentMap.Enemies.Add(placement);
            GD.Print($"[MapEditorTab] Placed {_selectedMonsterType} at ({tile.X}, {tile.Y})");
        }
        else if (_isPlacingGroup && _selectedGroupTemplate != null)
        {
            // Place monster group
            int roomId = GetRoomAtTile(tile.X, tile.Y);
            var groupPlacement = _selectedGroupTemplate.CreatePlacement(tile.X, tile.Y, roomId);
            _currentMap.MonsterGroups.Add(groupPlacement);
            GD.Print($"[MapEditorTab] Placed group '{_selectedGroupTemplate.Name}' at ({tile.X}, {tile.Y})");
        }

        UpdateMapInfo();
        _mapDrawArea?.QueueRedraw();
    }

    private void RemoveAtMouse(Vector2 mousePos)
    {
        if (_currentMap == null) return;

        var tile = ScreenToTile(mousePos);

        // Try to remove enemy at this tile
        for (int i = _currentMap.Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _currentMap.Enemies[i];
            if (enemy.Position.X == tile.X && enemy.Position.Z == tile.Y)
            {
                _currentMap.Enemies.RemoveAt(i);
                GD.Print($"[MapEditorTab] Removed enemy at ({tile.X}, {tile.Y})");
                UpdateMapInfo();
                _mapDrawArea?.QueueRedraw();
                return;
            }
        }

        // Try to remove group near this tile
        for (int i = _currentMap.MonsterGroups.Count - 1; i >= 0; i--)
        {
            var group = _currentMap.MonsterGroups[i];
            if (Mathf.Abs(group.Center.X - tile.X) < 3 && Mathf.Abs(group.Center.Z - tile.Y) < 3)
            {
                _currentMap.MonsterGroups.RemoveAt(i);
                GD.Print($"[MapEditorTab] Removed group at ({tile.X}, {tile.Y})");
                UpdateMapInfo();
                _mapDrawArea?.QueueRedraw();
                return;
            }
        }
    }

    private bool IsTileFloor(int x, int z)
    {
        if (_currentMap == null) return false;

        // First check the tile grid (for tile-mode painted floors)
        if (_tileGrid != null && x >= 0 && x < _currentMap.Width && z >= 0 && z < _currentMap.Depth)
        {
            if (_tileGrid[x, z] == 1)
                return true;
        }

        // Also check if in any room (for room-mode)
        foreach (var room in _currentMap.Rooms)
        {
            if (x >= room.Position.X && x < room.Position.X + room.Size.X &&
                z >= room.Position.Z && z < room.Position.Z + room.Size.Z)
            {
                return true;
            }
        }

        return false;
    }

    private int GetRoomAtTile(int x, int z)
    {
        if (_currentMap == null) return -1;

        for (int i = 0; i < _currentMap.Rooms.Count; i++)
        {
            var room = _currentMap.Rooms[i];
            if (x >= room.Position.X && x < room.Position.X + room.Size.X &&
                z >= room.Position.Z && z < room.Position.Z + room.Size.Z)
            {
                return i;
            }
        }
        return -1;
    }

    private void OnMapDraw()
    {
        if (_mapDrawArea == null) return;

        // Draw grid background
        DrawGrid();

        if (_currentMap == null) return;

        // Draw tile grid if we have one
        if (_tileGrid != null)
        {
            DrawTileGrid();
            DrawWallIndicators();
            DrawRoomOverlays(); // Draw room outlines and labels on top of tiles
        }
        else
        {
            // Fallback: draw rooms for room-mode maps
            foreach (var room in _currentMap.Rooms)
            {
                Color roomColor = room.IsSpawnRoom ? SpawnRoomColor : RoomColor;
                DrawRoom(room, roomColor);
            }

            // Draw corridors
            foreach (var corridor in _currentMap.Corridors)
            {
                if (corridor.FromRoom >= 0 && corridor.FromRoom < _currentMap.Rooms.Count &&
                    corridor.ToRoom >= 0 && corridor.ToRoom < _currentMap.Rooms.Count)
                {
                    var fromRoom = _currentMap.Rooms[corridor.FromRoom];
                    var toRoom = _currentMap.Rooms[corridor.ToRoom];

                    var from = TileToScreen(
                        fromRoom.Position.X + fromRoom.Size.X / 2,
                        fromRoom.Position.Z + fromRoom.Size.Z / 2
                    );
                    var to = TileToScreen(
                        toRoom.Position.X + toRoom.Size.X / 2,
                        toRoom.Position.Z + toRoom.Size.Z / 2
                    );

                    _mapDrawArea.DrawLine(from, to, CorridorColor, corridor.Width * _viewScale * 0.5f);
                }
            }
        }

        // Draw room preview while dragging
        if (_isDrawingRoom)
        {
            DrawRoomPreview();
        }

        // Draw spawn point
        DrawSpawnPoint();

        // Draw brush preview
        DrawBrushPreview();

        // Draw enemies and groups
        DrawEnemies();
        DrawMonsterGroups();

        // Draw monster placement preview
        DrawPlacementPreview();
    }

    private void DrawRoomPreview()
    {
        if (_mapDrawArea == null) return;

        int minX = Mathf.Min(_roomStartTile.X, _roomEndTile.X);
        int maxX = Mathf.Max(_roomStartTile.X, _roomEndTile.X);
        int minZ = Mathf.Min(_roomStartTile.Y, _roomEndTile.Y);
        int maxZ = Mathf.Max(_roomStartTile.Y, _roomEndTile.Y);

        var topLeft = TileToScreen(minX, minZ);
        var size = new Vector2((maxX - minX + 1) * _viewScale, (maxZ - minZ + 1) * _viewScale);

        // Fill with semi-transparent blue
        _mapDrawArea.DrawRect(new Rect2(topLeft, size), new Color(0.2f, 0.4f, 0.6f, 0.4f));
        // Border
        _mapDrawArea.DrawRect(new Rect2(topLeft, size), new Color(0.4f, 0.7f, 1f), false, 2f);

        // Size label
        string sizeText = $"{maxX - minX + 1}x{maxZ - minZ + 1}";
        var labelPos = topLeft + new Vector2(4, 16);
        _mapDrawArea.DrawString(ThemeDB.FallbackFont, labelPos, sizeText, HorizontalAlignment.Left, -1, 14, Colors.White);
    }

    private void DrawSpawnPoint()
    {
        if (_mapDrawArea == null || _spawnPoint == null) return;

        var pos = TileToScreen(_spawnPoint.Value.X, _spawnPoint.Value.Y);
        pos += new Vector2(_viewScale / 2, _viewScale / 2);

        // Green spawn marker
        float size = Mathf.Max(8f, _viewScale * 0.4f);
        _mapDrawArea.DrawCircle(pos, size, new Color(0.2f, 0.8f, 0.2f));
        _mapDrawArea.DrawCircle(pos, size * 0.6f, new Color(0.3f, 1f, 0.3f));

        // "S" label
        if (_viewScale >= 6f)
        {
            _mapDrawArea.DrawString(ThemeDB.FallbackFont, pos - new Vector2(5, -5), "S", HorizontalAlignment.Center, -1, 12, Colors.White);
        }
    }

    private void DrawBrushPreview()
    {
        if (_mapDrawArea == null || _currentMap == null) return;
        if (_currentTool != PaintTool.Floor && _currentTool != PaintTool.Erase) return;

        var tile = ScreenToTile(_placementPreviewPos);
        int halfBrush = CurrentBrushSize / 2;

        int minX = tile.X - halfBrush;
        int minZ = tile.Y - halfBrush;
        int maxX = tile.X + halfBrush;
        int maxZ = tile.Y + halfBrush;

        var topLeft = TileToScreen(minX, minZ);
        var size = new Vector2(CurrentBrushSize * _viewScale, CurrentBrushSize * _viewScale);

        Color brushColor = _currentTool == PaintTool.Floor
            ? new Color(0.3f, 0.8f, 0.3f, 0.3f)
            : new Color(0.8f, 0.3f, 0.3f, 0.3f);

        _mapDrawArea.DrawRect(new Rect2(topLeft, size), brushColor);
        _mapDrawArea.DrawRect(new Rect2(topLeft, size), new Color(brushColor, 0.8f), false, 2f);
    }

    private void DrawGrid()
    {
        if (_mapDrawArea == null || _currentMap == null) return;

        var viewSize = _mapDrawArea.Size;

        // Only draw grid if zoomed in enough
        if (_viewScale >= 4f)
        {
            // Calculate visible tile range
            int startX = Mathf.Max(0, (int)((-_viewOffset.X) / _viewScale) - 1);
            int startZ = Mathf.Max(0, (int)((-_viewOffset.Y) / _viewScale) - 1);
            int endX = Mathf.Min(_currentMap.Width, (int)((viewSize.X - _viewOffset.X) / _viewScale) + 1);
            int endZ = Mathf.Min(_currentMap.Depth, (int)((viewSize.Y - _viewOffset.Y) / _viewScale) + 1);

            // Draw vertical lines
            for (int x = startX; x <= endX; x++)
            {
                var from = TileToScreen(x, startZ);
                var to = TileToScreen(x, endZ);
                _mapDrawArea.DrawLine(from, to, GridColor, 1f);
            }

            // Draw horizontal lines
            for (int z = startZ; z <= endZ; z++)
            {
                var from = TileToScreen(startX, z);
                var to = TileToScreen(endX, z);
                _mapDrawArea.DrawLine(from, to, GridColor, 1f);
            }
        }
    }

    private void DrawRoom(RoomDefinition room, Color color)
    {
        if (_mapDrawArea == null) return;

        var topLeft = TileToScreen(room.Position.X, room.Position.Z);
        var size = new Vector2(room.Size.X * _viewScale, room.Size.Z * _viewScale);

        // Fill
        _mapDrawArea.DrawRect(new Rect2(topLeft, size), color);

        // Border
        _mapDrawArea.DrawRect(new Rect2(topLeft, size), new Color(0.6f, 0.55f, 0.5f), false, 2f);

        // Room label (if zoomed in enough)
        if (_viewScale >= 6f)
        {
            var labelPos = topLeft + new Vector2(4, 14);
            // Draw room type
            _mapDrawArea.DrawString(
                ThemeDB.FallbackFont,
                labelPos,
                room.Type,
                HorizontalAlignment.Left,
                -1,
                12,
                new Color(0.9f, 0.85f, 0.8f)
            );
        }
    }

    private void DrawTileGrid()
    {
        if (_mapDrawArea == null || _tileGrid == null || _currentMap == null) return;

        var viewSize = _mapDrawArea.Size;
        int width = _currentMap.Width;
        int depth = _currentMap.Depth;

        // Calculate visible tile range for optimization
        int startX = Mathf.Max(0, (int)((-_viewOffset.X) / _viewScale) - 1);
        int startZ = Mathf.Max(0, (int)((-_viewOffset.Y) / _viewScale) - 1);
        int endX = Mathf.Min(width, (int)((viewSize.X - _viewOffset.X) / _viewScale) + 2);
        int endZ = Mathf.Min(depth, (int)((viewSize.Y - _viewOffset.Y) / _viewScale) + 2);

        // Draw each visible tile
        for (int x = startX; x < endX; x++)
        {
            for (int z = startZ; z < endZ; z++)
            {
                var screenPos = TileToScreen(x, z);
                var tileRect = new Rect2(screenPos, new Vector2(_viewScale, _viewScale));

                // Floor or void
                Color tileColor = _tileGrid[x, z] == 1 ? TileFloorColor : TileVoidColor;
                _mapDrawArea.DrawRect(tileRect, tileColor);

                // Tile border when zoomed in
                if (_viewScale >= 4f)
                {
                    _mapDrawArea.DrawRect(tileRect, GridColor, false, 1f);
                }
            }
        }

        // Debug: Draw corner markers to help identify coordinate system
        // Top-left (0,0) = RED, Top-right (W-1,0) = BLUE, Bottom-left (0,D-1) = YELLOW, Bottom-right = GREEN
        float markerSize = Mathf.Max(8f, _viewScale * 0.8f);

        // Origin marker (0,0) - RED circle with "O"
        var originPos = TileToScreen(0, 0) + new Vector2(_viewScale / 2, _viewScale / 2);
        _mapDrawArea.DrawCircle(originPos, markerSize, Colors.Red);
        if (_viewScale >= 6f)
            _mapDrawArea.DrawString(ThemeDB.FallbackFont, originPos - new Vector2(4, -4), "O", HorizontalAlignment.Center, -1, 10, Colors.White);

        // Top-right (W-1, 0) - BLUE
        var trPos = TileToScreen(width - 1, 0) + new Vector2(_viewScale / 2, _viewScale / 2);
        _mapDrawArea.DrawCircle(trPos, markerSize, Colors.Blue);
        if (_viewScale >= 6f)
            _mapDrawArea.DrawString(ThemeDB.FallbackFont, trPos - new Vector2(4, -4), "X", HorizontalAlignment.Center, -1, 10, Colors.White);

        // Bottom-left (0, D-1) - YELLOW
        var blPos = TileToScreen(0, depth - 1) + new Vector2(_viewScale / 2, _viewScale / 2);
        _mapDrawArea.DrawCircle(blPos, markerSize, Colors.Yellow);
        if (_viewScale >= 6f)
            _mapDrawArea.DrawString(ThemeDB.FallbackFont, blPos - new Vector2(4, -4), "Z", HorizontalAlignment.Center, -1, 10, Colors.White);

        // Bottom-right (W-1, D-1) - GREEN
        var brPos = TileToScreen(width - 1, depth - 1) + new Vector2(_viewScale / 2, _viewScale / 2);
        _mapDrawArea.DrawCircle(brPos, markerSize, Colors.Green);
    }

    private void DrawWallIndicators()
    {
        if (_mapDrawArea == null || _tileGrid == null || _currentMap == null) return;

        var viewSize = _mapDrawArea.Size;
        int width = _currentMap.Width;
        int depth = _currentMap.Depth;

        // Calculate visible tile range
        int startX = Mathf.Max(1, (int)((-_viewOffset.X) / _viewScale) - 1);
        int startZ = Mathf.Max(1, (int)((-_viewOffset.Y) / _viewScale) - 1);
        int endX = Mathf.Min(width - 1, (int)((viewSize.X - _viewOffset.X) / _viewScale) + 2);
        int endZ = Mathf.Min(depth - 1, (int)((viewSize.Y - _viewOffset.Y) / _viewScale) + 2);

        // Draw wall indicators where floor meets void
        for (int x = startX; x < endX; x++)
        {
            for (int z = startZ; z < endZ; z++)
            {
                if (_tileGrid[x, z] != 1) continue; // Only check floor tiles

                var screenPos = TileToScreen(x, z);
                float wallThickness = Mathf.Max(2f, _viewScale * 0.15f);

                // Check each direction for void
                // North (z-1)
                if (z > 0 && _tileGrid[x, z - 1] == 0)
                {
                    _mapDrawArea.DrawLine(
                        screenPos,
                        screenPos + new Vector2(_viewScale, 0),
                        WallColor, wallThickness);
                }
                // South (z+1)
                if (z < depth - 1 && _tileGrid[x, z + 1] == 0)
                {
                    _mapDrawArea.DrawLine(
                        screenPos + new Vector2(0, _viewScale),
                        screenPos + new Vector2(_viewScale, _viewScale),
                        WallColor, wallThickness);
                }
                // West (x-1)
                if (x > 0 && _tileGrid[x - 1, z] == 0)
                {
                    _mapDrawArea.DrawLine(
                        screenPos,
                        screenPos + new Vector2(0, _viewScale),
                        WallColor, wallThickness);
                }
                // East (x+1)
                if (x < width - 1 && _tileGrid[x + 1, z] == 0)
                {
                    _mapDrawArea.DrawLine(
                        screenPos + new Vector2(_viewScale, 0),
                        screenPos + new Vector2(_viewScale, _viewScale),
                        WallColor, wallThickness);
                }
            }
        }
    }

    /// <summary>
    /// Draws room outlines and labels on top of tile grid for tile-mode maps.
    /// </summary>
    private void DrawRoomOverlays()
    {
        if (_mapDrawArea == null || _currentMap == null) return;

        // Define colors for room overlays
        var roomOutlineColor = new Color(0.3f, 0.6f, 0.9f, 0.9f); // Blue outline
        var spawnRoomOutlineColor = new Color(0.3f, 0.9f, 0.4f, 0.9f); // Green for spawn room
        var selectedOutlineColor = new Color(1f, 0.85f, 0.2f, 1f); // Yellow for selected
        var roomFillColor = new Color(0.3f, 0.6f, 0.9f, 0.1f); // Very light blue fill
        var spawnRoomFillColor = new Color(0.3f, 0.9f, 0.4f, 0.1f); // Very light green fill
        var selectedFillColor = new Color(1f, 0.85f, 0.2f, 0.2f); // Yellow fill for selected
        var labelBgColor = new Color(0.1f, 0.1f, 0.15f, 0.85f); // Dark background for labels

        for (int i = 0; i < _currentMap.Rooms.Count; i++)
        {
            var room = _currentMap.Rooms[i];
            var topLeft = TileToScreen(room.Position.X, room.Position.Z);
            var size = new Vector2(room.Size.X * _viewScale, room.Size.Z * _viewScale);

            // Check if this room is selected
            bool isSelected = (i == _selectedRoomIndex);

            // Choose colors based on selection and spawn room status
            Color outlineColor;
            Color fillColor;
            if (isSelected)
            {
                outlineColor = selectedOutlineColor;
                fillColor = selectedFillColor;
            }
            else if (room.IsSpawnRoom)
            {
                outlineColor = spawnRoomOutlineColor;
                fillColor = spawnRoomFillColor;
            }
            else
            {
                outlineColor = roomOutlineColor;
                fillColor = roomFillColor;
            }

            // Draw semi-transparent fill
            _mapDrawArea.DrawRect(new Rect2(topLeft, size), fillColor);

            // Draw outline (thicker border, even thicker for selected)
            float outlineWidth = isSelected ? Mathf.Max(3f, _viewScale * 0.3f) : Mathf.Max(2f, _viewScale * 0.2f);
            _mapDrawArea.DrawRect(new Rect2(topLeft, size), outlineColor, false, outlineWidth);

            // Draw room label
            string roomLabel = $"Room {i + 1}";
            if (!string.IsNullOrEmpty(room.Type))
                roomLabel += $" ({room.Type})";
            if (room.IsSpawnRoom)
                roomLabel += " [SPAWN]";

            // Calculate label position (centered at top of room)
            var font = ThemeDB.FallbackFont;
            int fontSize = Mathf.Clamp((int)(_viewScale * 1.5f), 10, 16);
            var labelSize = font.GetStringSize(roomLabel, HorizontalAlignment.Left, -1, fontSize);

            // Label background
            float padding = 4f;
            var labelPos = topLeft + new Vector2(size.X / 2 - labelSize.X / 2, 4 + fontSize);
            var labelBgRect = new Rect2(
                labelPos.X - padding,
                labelPos.Y - fontSize - padding + 2,
                labelSize.X + padding * 2,
                fontSize + padding * 2
            );
            _mapDrawArea.DrawRect(labelBgRect, labelBgColor);

            // Label text
            _mapDrawArea.DrawString(font, labelPos, roomLabel, HorizontalAlignment.Left, -1, fontSize, Colors.White);

            // Draw room dimensions at bottom-right
            string dimsLabel = $"{room.Size.X}x{room.Size.Z}";
            int dimsFontSize = Mathf.Clamp((int)(_viewScale * 1.2f), 8, 12);
            var dimsPos = topLeft + size - new Vector2(font.GetStringSize(dimsLabel, HorizontalAlignment.Left, -1, dimsFontSize).X + 4, 4);
            _mapDrawArea.DrawString(font, dimsPos, dimsLabel, HorizontalAlignment.Left, -1, dimsFontSize, outlineColor);
        }
    }

    private void DrawEnemies()
    {
        if (_mapDrawArea == null || _currentMap == null) return;

        foreach (var enemy in _currentMap.Enemies)
        {
            var pos = TileToScreen(enemy.Position.X, enemy.Position.Z);
            Color color = enemy.IsBoss ? BossColor : EnemyColor;
            float size = enemy.IsBoss ? 8f : 5f;
            _mapDrawArea.DrawCircle(pos + new Vector2(_viewScale / 2, _viewScale / 2), size, color);
        }
    }

    private void DrawMonsterGroups()
    {
        if (_mapDrawArea == null || _currentMap == null) return;

        foreach (var group in _currentMap.MonsterGroups)
        {
            var centerPos = TileToScreen(group.Center.X, group.Center.Z);
            centerPos += new Vector2(_viewScale / 2, _viewScale / 2);

            // Draw group circle
            _mapDrawArea.DrawCircle(centerPos, 12f, new Color(GroupColor, 0.3f));
            _mapDrawArea.DrawArc(centerPos, 12f, 0, Mathf.Tau, 16, GroupColor, 2f);

            // Draw individual monsters in group
            foreach (var monster in group.Monsters)
            {
                var monsterPos = centerPos + new Vector2(monster.OffsetX, monster.OffsetZ) * _viewScale;
                Color color = monster.IsBoss ? BossColor : EnemyColor;
                _mapDrawArea.DrawCircle(monsterPos, 4f, color);
            }
        }
    }

    private void DrawPlacementPreview()
    {
        if (_mapDrawArea == null) return;

        if (_isPlacingMonster || _isPlacingGroup)
        {
            var tile = ScreenToTile(_placementPreviewPos);
            var previewPos = TileToScreen(tile.X, tile.Y);
            previewPos += new Vector2(_viewScale / 2, _viewScale / 2);

            if (_isPlacingMonster)
            {
                bool isBoss = _selectedMonsterType != null &&
                             System.Array.IndexOf(BossTypes, _selectedMonsterType) >= 0;
                Color previewColor = new Color(isBoss ? BossColor : EnemyColor, 0.5f);
                _mapDrawArea.DrawCircle(previewPos, isBoss ? 8f : 5f, previewColor);
            }
            else if (_isPlacingGroup && _selectedGroupTemplate != null)
            {
                Color previewColor = new Color(GroupColor, 0.4f);
                _mapDrawArea.DrawCircle(previewPos, 12f, previewColor);

                foreach (var monster in _selectedGroupTemplate.Monsters)
                {
                    var monsterPos = previewPos + new Vector2(monster.OffsetX, monster.OffsetZ) * _viewScale;
                    _mapDrawArea.DrawCircle(monsterPos, 3f, new Color(EnemyColor, 0.5f));
                }
            }
        }
    }

    private void PaintTileAt(Vector2 screenPos, bool setFloor)
    {
        if (_tileGrid == null || _currentMap == null) return;

        var tile = ScreenToTile(screenPos);
        int x = tile.X;
        int z = tile.Y;

        if (x < 0 || x >= _currentMap.Width || z < 0 || z >= _currentMap.Depth)
            return;

        // Check if tile is already the desired state
        int targetValue = setFloor ? 1 : 0;
        if (_tileGrid[x, z] == targetValue)
            return;

        // Set the tile
        _tileGrid[x, z] = targetValue;

        // Update or add custom tile override
        var existingTile = _currentMap.CustomTiles.Find(t => t.X == x && t.Z == z);
        if (existingTile != null)
        {
            existingTile.IsFloor = setFloor;
        }
        else
        {
            _currentMap.CustomTiles.Add(new TileOverride { X = x, Z = z, IsFloor = setFloor });
        }

        _mapDrawArea?.QueueRedraw();
    }

    private void UpdateTileCoordLabel(Vector2 screenPos)
    {
        if (_tileCoordLabel == null || _currentMap == null) return;

        var tile = ScreenToTile(screenPos);
        if (tile.X >= 0 && tile.X < _currentMap.Width && tile.Y >= 0 && tile.Y < _currentMap.Depth)
        {
            bool isFloor = _tileGrid != null && _tileGrid[tile.X, tile.Y] == 1;
            _tileCoordLabel.Text = $"Tile: ({tile.X}, {tile.Y}) - {(isFloor ? "FLOOR" : "VOID")}";
        }
        else
        {
            _tileCoordLabel.Text = "Tile: (outside map)";
        }
    }

    public override void _Process(double delta)
    {
        // Could add animation here if needed
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            bool ctrl = keyEvent.CtrlPressed;

            // Tool shortcuts
            if (!ctrl)
            {
                switch (keyEvent.Keycode)
                {
                    case Key.F:
                        SelectTool(PaintTool.Floor);
                        break;
                    case Key.E:
                        SelectTool(PaintTool.Erase);
                        break;
                    case Key.R:
                        SelectTool(PaintTool.Room);
                        break;
                    case Key.P:
                        SelectTool(PaintTool.Spawn);
                        break;
                    case Key.Key1:
                        SetBrushSize(0);
                        break;
                    case Key.Key2:
                        SetBrushSize(1);
                        break;
                    case Key.Key3:
                        SetBrushSize(2);
                        break;
                    case Key.Key4:
                        SetBrushSize(3);
                        break;
                    case Key.Key5:
                        SetBrushSize(4);
                        break;
                }
            }
            else
            {
                // Ctrl shortcuts
                switch (keyEvent.Keycode)
                {
                    case Key.Z:
                        Undo();
                        break;
                    case Key.Y:
                        Redo();
                        break;
                    case Key.S:
                        OnSaveMapPressed();
                        break;
                }
            }
        }
    }
}
