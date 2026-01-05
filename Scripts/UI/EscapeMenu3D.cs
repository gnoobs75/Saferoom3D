using Godot;
using SafeRoom3D.Core;

namespace SafeRoom3D.UI;

// Note: Keybindings are saved/loaded via KeybindingsManager

/// <summary>
/// Escape menu with game options, restart, and editor access.
/// Uses CanvasLayer to ensure it's always on top of other UI.
/// </summary>
public partial class EscapeMenu3D : CanvasLayer
{
    public static EscapeMenu3D? Instance { get; private set; }

    // Root control for all UI
    private Control? _rootControl;

    // UI Elements
    private PanelContainer? _menuPanel;
    private VBoxContainer? _buttonContainer;
    private Button? _resumeButton;
    private Button? _restartButton;
    private Button? _optionsButton;
    private Button? _returnToMenuButton;
    private Button? _quitButton;
    private Label? _titleLabel;

    // Options panel
    private PanelContainer? _optionsPanel;
    private HSlider? _musicSlider;
    private HSlider? _soundSlider;
    private CheckBox? _musicToggle;
    private CheckBox? _soundToggle;
    private HSlider? _sensitivitySlider;
    private OptionButton? _lodDropdown;
    private HSlider? _gameSpeedSlider;
    private Label? _gameSpeedValueLabel;
    private Button? _optionsBackButton;

    // Keybindings panel
    private PanelContainer? _keybindingsPanel;
    private Button? _keybindingsBackButton;
    private VBoxContainer? _keybindingsContainer;
    private Button? _waitingForKeyButton;
    private string? _waitingForAction;

    private bool _showingOptions = false;
    private bool _showingKeybindings = false;

    public override void _Ready()
    {
        Instance = this;
        Layer = 200;  // Very high layer to be on top of everything (Broadcaster is 50)
        ProcessMode = ProcessModeEnum.Always;

        BuildUI();

        Visible = false;
        GD.Print("[EscapeMenu3D] Ready");
    }

    private void BuildUI()
    {
        // Root control to hold all UI (CanvasLayer needs a Control child for anchors)
        _rootControl = new Control();
        _rootControl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _rootControl.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_rootControl);

        // Semi-transparent background
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0f, 0f, 0f, 0.7f);
        _rootControl.AddChild(bg);

        // Center container
        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _rootControl.AddChild(centerContainer);

        // Main menu panel
        _menuPanel = new PanelContainer();
        _menuPanel.CustomMinimumSize = new Vector2(400, 500);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.4f, 0.35f, 0.25f);
        panelStyle.SetCornerRadiusAll(15);
        panelStyle.SetContentMarginAll(30);
        _menuPanel.AddThemeStyleboxOverride("panel", panelStyle);
        centerContainer.AddChild(_menuPanel);

        var mainVBox = new VBoxContainer();
        mainVBox.AddThemeConstantOverride("separation", 20);
        _menuPanel.AddChild(mainVBox);

        // Title
        _titleLabel = new Label();
        _titleLabel.Text = "PAUSED";
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", 36);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        mainVBox.AddChild(_titleLabel);

        // Spacer
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 20);
        mainVBox.AddChild(spacer);

        // Button container
        _buttonContainer = new VBoxContainer();
        _buttonContainer.AddThemeConstantOverride("separation", 15);
        mainVBox.AddChild(_buttonContainer);

        // Resume button
        _resumeButton = CreateMenuButton("Resume Game", new Color(0.3f, 0.5f, 0.3f));
        _resumeButton.Pressed += OnResumePressed;
        _buttonContainer.AddChild(_resumeButton);

        // Restart button
        _restartButton = CreateMenuButton("Restart Floor", new Color(0.5f, 0.4f, 0.2f));
        _restartButton.Pressed += OnRestartPressed;
        _buttonContainer.AddChild(_restartButton);

        // Options button
        _optionsButton = CreateMenuButton("Options", new Color(0.4f, 0.4f, 0.4f));
        _optionsButton.Pressed += OnOptionsPressed;
        _buttonContainer.AddChild(_optionsButton);

        // Keybindings button
        var keybindingsButton = CreateMenuButton("Key Bindings", new Color(0.35f, 0.45f, 0.5f));
        keybindingsButton.Pressed += OnKeybindingsPressed;
        _buttonContainer.AddChild(keybindingsButton);

        // Debug button
        var debugButton = CreateMenuButton("Debug Stats", new Color(0.3f, 0.6f, 0.35f));
        debugButton.Pressed += OnDebugPressed;
        _buttonContainer.AddChild(debugButton);

        // Return to Menu button
        _returnToMenuButton = CreateMenuButton("Return to Menu", new Color(0.4f, 0.35f, 0.5f));
        _returnToMenuButton.Pressed += OnReturnToMenuPressed;
        _buttonContainer.AddChild(_returnToMenuButton);

        // Quit button
        _quitButton = CreateMenuButton("Quit to Desktop", new Color(0.5f, 0.2f, 0.2f));
        _quitButton.Pressed += OnQuitPressed;
        _buttonContainer.AddChild(_quitButton);

        // Create options panel (hidden initially)
        CreateOptionsPanel(centerContainer);

        // Create keybindings panel (hidden initially)
        CreateKeybindingsPanel(centerContainer);
    }

    private Button CreateMenuButton(string text, Color bgColor)
    {
        var button = new Button();
        button.Text = text;
        button.CustomMinimumSize = new Vector2(300, 50);
        button.AddThemeFontSizeOverride("font_size", 20);

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = bgColor;
        normalStyle.SetCornerRadiusAll(8);
        normalStyle.SetContentMarginAll(10);
        button.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = bgColor.Lightened(0.2f);
        hoverStyle.SetCornerRadiusAll(8);
        hoverStyle.SetContentMarginAll(10);
        button.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = bgColor.Darkened(0.2f);
        pressedStyle.SetCornerRadiusAll(8);
        pressedStyle.SetContentMarginAll(10);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);

        return button;
    }

    private void CreateOptionsPanel(CenterContainer parent)
    {
        _optionsPanel = new PanelContainer();
        _optionsPanel.CustomMinimumSize = new Vector2(450, 450);
        _optionsPanel.Visible = false;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.4f, 0.35f, 0.25f);
        panelStyle.SetCornerRadiusAll(15);
        panelStyle.SetContentMarginAll(30);
        _optionsPanel.AddThemeStyleboxOverride("panel", panelStyle);
        parent.AddChild(_optionsPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        _optionsPanel.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "OPTIONS";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(title);

        // Music toggle
        var musicRow = new HBoxContainer();
        var musicLabel = new Label { Text = "Music", CustomMinimumSize = new Vector2(120, 0) };
        musicLabel.AddThemeFontSizeOverride("font_size", 18);
        musicRow.AddChild(musicLabel);
        _musicToggle = new CheckBox { ButtonPressed = AudioConfig.IsMusicEnabled };
        _musicToggle.Toggled += (pressed) => AudioConfig.IsMusicEnabled = pressed;
        musicRow.AddChild(_musicToggle);
        vbox.AddChild(musicRow);

        // Music volume
        var musicVolRow = new HBoxContainer();
        var musicVolLabel = new Label { Text = "Music Volume", CustomMinimumSize = new Vector2(120, 0) };
        musicVolLabel.AddThemeFontSizeOverride("font_size", 18);
        musicVolRow.AddChild(musicVolLabel);
        _musicSlider = new HSlider();
        _musicSlider.MinValue = 0;
        _musicSlider.MaxValue = 1;
        _musicSlider.Step = 0.05;
        _musicSlider.Value = AudioConfig.MusicVolume;
        _musicSlider.CustomMinimumSize = new Vector2(200, 0);
        _musicSlider.ValueChanged += (val) => AudioConfig.MusicVolume = (float)val;
        musicVolRow.AddChild(_musicSlider);
        vbox.AddChild(musicVolRow);

        // Sound toggle
        var soundRow = new HBoxContainer();
        var soundLabel = new Label { Text = "Sound", CustomMinimumSize = new Vector2(120, 0) };
        soundLabel.AddThemeFontSizeOverride("font_size", 18);
        soundRow.AddChild(soundLabel);
        _soundToggle = new CheckBox { ButtonPressed = AudioConfig.IsSoundEnabled };
        _soundToggle.Toggled += (pressed) => AudioConfig.IsSoundEnabled = pressed;
        soundRow.AddChild(_soundToggle);
        vbox.AddChild(soundRow);

        // Sound volume
        var soundVolRow = new HBoxContainer();
        var soundVolLabel = new Label { Text = "Sound Volume", CustomMinimumSize = new Vector2(120, 0) };
        soundVolLabel.AddThemeFontSizeOverride("font_size", 18);
        soundVolRow.AddChild(soundVolLabel);
        _soundSlider = new HSlider();
        _soundSlider.MinValue = 0;
        _soundSlider.MaxValue = 1;
        _soundSlider.Step = 0.05;
        _soundSlider.Value = AudioConfig.SoundVolume;
        _soundSlider.CustomMinimumSize = new Vector2(200, 0);
        _soundSlider.ValueChanged += (val) => AudioConfig.SoundVolume = (float)val;
        soundVolRow.AddChild(_soundSlider);
        vbox.AddChild(soundVolRow);

        // Mouse sensitivity
        var sensRow = new HBoxContainer();
        var sensLabel = new Label { Text = "Mouse Sens.", CustomMinimumSize = new Vector2(120, 0) };
        sensLabel.AddThemeFontSizeOverride("font_size", 18);
        sensRow.AddChild(sensLabel);
        _sensitivitySlider = new HSlider();
        _sensitivitySlider.MinValue = 0.1;
        _sensitivitySlider.MaxValue = 2.0;
        _sensitivitySlider.Step = 0.1;
        _sensitivitySlider.Value = 1.0;
        _sensitivitySlider.CustomMinimumSize = new Vector2(200, 0);
        sensRow.AddChild(_sensitivitySlider);
        vbox.AddChild(sensRow);

        // LOD (Level of Detail) dropdown
        var lodRow = new HBoxContainer();
        var lodLabel = new Label { Text = "Graphics LOD", CustomMinimumSize = new Vector2(120, 0) };
        lodLabel.AddThemeFontSizeOverride("font_size", 18);
        lodRow.AddChild(lodLabel);
        _lodDropdown = new OptionButton();
        _lodDropdown.AddItem(GraphicsConfig.GetLODDisplayName(SafeRoom3D.Enemies.MonsterMeshFactory.LODLevel.High), 0);
        _lodDropdown.AddItem(GraphicsConfig.GetLODDisplayName(SafeRoom3D.Enemies.MonsterMeshFactory.LODLevel.Medium), 1);
        _lodDropdown.AddItem(GraphicsConfig.GetLODDisplayName(SafeRoom3D.Enemies.MonsterMeshFactory.LODLevel.Low), 2);
        _lodDropdown.Selected = GraphicsConfig.GetLODIndex();
        _lodDropdown.CustomMinimumSize = new Vector2(200, 0);
        _lodDropdown.ItemSelected += (index) => GraphicsConfig.SetLODFromIndex((int)index);
        lodRow.AddChild(_lodDropdown);
        vbox.AddChild(lodRow);

        // Game speed slider
        var speedRow = new HBoxContainer();
        var speedLabel = new Label { Text = "Game Speed", CustomMinimumSize = new Vector2(120, 0) };
        speedLabel.AddThemeFontSizeOverride("font_size", 18);
        speedRow.AddChild(speedLabel);
        _gameSpeedSlider = new HSlider();
        _gameSpeedSlider.MinValue = 50;
        _gameSpeedSlider.MaxValue = 125;
        _gameSpeedSlider.Step = 5;
        _gameSpeedSlider.Value = GameConfig.GameSpeedPercent;
        _gameSpeedSlider.CustomMinimumSize = new Vector2(150, 0);
        _gameSpeedSlider.ValueChanged += OnGameSpeedChanged;
        speedRow.AddChild(_gameSpeedSlider);
        _gameSpeedValueLabel = new Label { Text = $"{GameConfig.GameSpeedPercent}%" };
        _gameSpeedValueLabel.AddThemeFontSizeOverride("font_size", 16);
        _gameSpeedValueLabel.CustomMinimumSize = new Vector2(50, 0);
        speedRow.AddChild(_gameSpeedValueLabel);
        vbox.AddChild(speedRow);

        // Back button
        _optionsBackButton = CreateMenuButton("Back", new Color(0.4f, 0.3f, 0.3f));
        _optionsBackButton.Pressed += OnOptionsBackPressed;
        vbox.AddChild(_optionsBackButton);
    }

    private void CreateKeybindingsPanel(CenterContainer parent)
    {
        _keybindingsPanel = new PanelContainer();
        _keybindingsPanel.CustomMinimumSize = new Vector2(550, 600);
        _keybindingsPanel.Visible = false;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.4f, 0.35f, 0.25f);
        panelStyle.SetCornerRadiusAll(15);
        panelStyle.SetContentMarginAll(20);
        _keybindingsPanel.AddThemeStyleboxOverride("panel", panelStyle);
        parent.AddChild(_keybindingsPanel);

        var mainVBox = new VBoxContainer();
        mainVBox.AddThemeConstantOverride("separation", 10);
        _keybindingsPanel.AddChild(mainVBox);

        // Title
        var title = new Label();
        title.Text = "KEY BINDINGS";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        mainVBox.AddChild(title);

        // Scroll container for keybindings
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(500, 450);
        scroll.SetDeferred("horizontal_scroll_mode", 0); // Disabled
        mainVBox.AddChild(scroll);

        _keybindingsContainer = new VBoxContainer();
        _keybindingsContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_keybindingsContainer);

        // Add all keybindings
        AddKeybindingRow("move_forward", "Move Forward", "W");
        AddKeybindingRow("move_back", "Move Back", "S");
        AddKeybindingRow("move_left", "Move Left", "A");
        AddKeybindingRow("move_right", "Move Right", "D");
        AddKeybindingRow("jump", "Jump", "Space");
        AddKeybindingRow("sprint", "Sprint", "Shift");
        AddKeybindingRow("attack", "Attack", "LMB");
        AddKeybindingRow("attack_alt", "Alt Attack", "RMB");
        AddKeybindingRow("interact", "Interact", "E");
        AddKeybindingRow("loot", "Loot Corpse", "T");
        AddKeybindingRow("toggle_spellbook", "Spellbook", "Tab");
        AddKeybindingRow("toggle_inventory", "Inventory", "I");
        AddKeybindingRow("toggle_map", "Toggle Map", "M");
        AddKeybindingRow("escape", "Menu", "Escape");
        AddKeybindingRow("ability_q", "Ability Q", "Q");
        AddKeybindingRow("ability_r", "Ability R", "R");
        AddKeybindingRow("ability_f", "Ability F", "F");
        AddKeybindingRow("ability_v", "Ability V", "V");

        // Back button
        _keybindingsBackButton = CreateMenuButton("Back", new Color(0.4f, 0.3f, 0.3f));
        _keybindingsBackButton.Pressed += OnKeybindingsBackPressed;
        mainVBox.AddChild(_keybindingsBackButton);
    }

    private void AddKeybindingRow(string action, string label, string defaultKey)
    {
        if (_keybindingsContainer == null) return;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        // Action label
        var actionLabel = new Label();
        actionLabel.Text = label;
        actionLabel.CustomMinimumSize = new Vector2(150, 0);
        actionLabel.AddThemeFontSizeOverride("font_size", 16);
        actionLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        row.AddChild(actionLabel);

        // Key button
        var keyButton = new Button();
        keyButton.Text = GetCurrentKeyForAction(action);
        keyButton.CustomMinimumSize = new Vector2(120, 35);
        keyButton.AddThemeFontSizeOverride("font_size", 14);

        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = new Color(0.2f, 0.2f, 0.25f);
        btnStyle.BorderColor = new Color(0.5f, 0.5f, 0.55f);
        btnStyle.SetBorderWidthAll(1);
        btnStyle.SetCornerRadiusAll(4);
        keyButton.AddThemeStyleboxOverride("normal", btnStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.25f, 0.25f, 0.3f);
        hoverStyle.BorderColor = new Color(0.7f, 0.6f, 0.4f);
        hoverStyle.SetBorderWidthAll(2);
        hoverStyle.SetCornerRadiusAll(4);
        keyButton.AddThemeStyleboxOverride("hover", hoverStyle);

        keyButton.SetMeta("action", action);
        keyButton.Pressed += () => OnKeybindButtonPressed(keyButton, action);
        row.AddChild(keyButton);

        // Reset button
        var resetButton = new Button();
        resetButton.Text = "Reset";
        resetButton.CustomMinimumSize = new Vector2(60, 35);
        resetButton.AddThemeFontSizeOverride("font_size", 12);

        var resetStyle = new StyleBoxFlat();
        resetStyle.BgColor = new Color(0.35f, 0.25f, 0.25f);
        resetStyle.SetCornerRadiusAll(4);
        resetButton.AddThemeStyleboxOverride("normal", resetStyle);

        resetButton.SetMeta("action", action);
        resetButton.SetMeta("keyButton", keyButton);
        resetButton.SetMeta("defaultKey", defaultKey);
        resetButton.Pressed += () => OnResetKeyPressed(action, keyButton, defaultKey);
        row.AddChild(resetButton);

        _keybindingsContainer.AddChild(row);
    }

    private string GetCurrentKeyForAction(string action)
    {
        var events = InputMap.ActionGetEvents(action);
        if (events.Count > 0)
        {
            var ev = events[0];
            if (ev is InputEventKey key)
            {
                return OS.GetKeycodeString(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
            }
            else if (ev is InputEventMouseButton mouse)
            {
                return mouse.ButtonIndex switch
                {
                    MouseButton.Left => "LMB",
                    MouseButton.Right => "RMB",
                    MouseButton.Middle => "MMB",
                    _ => $"Mouse{(int)mouse.ButtonIndex}"
                };
            }
        }
        return "None";
    }

    private void OnKeybindButtonPressed(Button button, string action)
    {
        _waitingForKeyButton = button;
        _waitingForAction = action;
        button.Text = "Press a key...";
        button.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
    }

    private void OnResetKeyPressed(string action, Button keyButton, string defaultKey)
    {
        // Reset to default (for now just update the display - actual InputMap modification would need more work)
        keyButton.Text = defaultKey;
        GD.Print($"[EscapeMenu3D] Reset {action} to {defaultKey}");
    }

    private void OnKeybindingsPressed()
    {
        _showingKeybindings = true;
        _menuPanel!.Visible = false;
        _keybindingsPanel!.Visible = true;
    }

    private void OnKeybindingsBackPressed()
    {
        _showingKeybindings = false;
        _menuPanel!.Visible = true;
        _keybindingsPanel!.Visible = false;
        _waitingForKeyButton = null;
        _waitingForAction = null;
    }

    public override void _Input(InputEvent @event)
    {
        // CanvasLayer visibility check - critical for input isolation
        if (!Visible) return;

        // Handle key rebinding (must be first to capture all keys)
        if (_waitingForKeyButton != null && _waitingForAction != null)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                // Remap the action
                RemapAction(_waitingForAction, keyEvent);
                _waitingForKeyButton.Text = GetCurrentKeyForAction(_waitingForAction);
                _waitingForKeyButton.RemoveThemeColorOverride("font_color");
                _waitingForKeyButton = null;
                _waitingForAction = null;
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
            {
                // Remap to mouse button
                RemapActionToMouse(_waitingForAction, mouseEvent);
                _waitingForKeyButton.Text = GetCurrentKeyForAction(_waitingForAction);
                _waitingForKeyButton.RemoveThemeColorOverride("font_color");
                _waitingForKeyButton = null;
                _waitingForAction = null;
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event.IsActionPressed("escape"))
        {
            if (_showingOptions)
            {
                OnOptionsBackPressed();
            }
            else if (_showingKeybindings)
            {
                OnKeybindingsBackPressed();
            }
            else
            {
                Hide();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private void RemapAction(string action, InputEventKey keyEvent)
    {
        // Remove existing events
        InputMap.ActionEraseEvents(action);

        // Add new key event
        var newEvent = new InputEventKey();
        newEvent.PhysicalKeycode = keyEvent.PhysicalKeycode;
        newEvent.Keycode = keyEvent.Keycode;
        InputMap.ActionAddEvent(action, newEvent);

        // Save to file
        KeybindingsManager.SaveKeybindings();

        GD.Print($"[EscapeMenu3D] Remapped {action} to {OS.GetKeycodeString(keyEvent.PhysicalKeycode)}");
    }

    private void RemapActionToMouse(string action, InputEventMouseButton mouseEvent)
    {
        // Remove existing events
        InputMap.ActionEraseEvents(action);

        // Add new mouse event
        var newEvent = new InputEventMouseButton();
        newEvent.ButtonIndex = mouseEvent.ButtonIndex;
        InputMap.ActionAddEvent(action, newEvent);

        // Save to file
        KeybindingsManager.SaveKeybindings();

        GD.Print($"[EscapeMenu3D] Remapped {action} to Mouse{(int)mouseEvent.ButtonIndex}");
    }

    public new void Show()
    {
        Visible = true;
        _showingOptions = false;
        _showingKeybindings = false;
        _menuPanel!.Visible = true;
        _optionsPanel!.Visible = false;
        _keybindingsPanel!.Visible = false;
        _waitingForKeyButton = null;
        _waitingForAction = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Pause game
        GetTree().Paused = true;
    }

    public new void Hide()
    {
        Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // Unpause game
        GetTree().Paused = false;
    }

    private void OnResumePressed()
    {
        Hide();
    }

    private void OnRestartPressed()
    {
        Hide();
        GameManager3D.Instance?.RestartFloor();
    }

    private void OnReturnToMenuPressed()
    {
        // Return to splash screen
        Hide();
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().ChangeSceneToFile("res://Scenes/Splash3D.tscn");
    }

    private void OnOptionsPressed()
    {
        _showingOptions = true;
        _menuPanel!.Visible = false;
        _optionsPanel!.Visible = true;
    }

    private void OnGameSpeedChanged(double value)
    {
        GameConfig.GameSpeedPercent = (int)value;
        if (_gameSpeedValueLabel != null)
            _gameSpeedValueLabel.Text = $"{(int)value}%";
    }

    private void OnOptionsBackPressed()
    {
        _showingOptions = false;
        _menuPanel!.Visible = true;
        _optionsPanel!.Visible = false;
    }

    private void OnDebugPressed()
    {
        // Open performance monitor and close escape menu
        Hide();
        if (PerformanceMonitor.Instance != null)
        {
            PerformanceMonitor.Instance.Visible = true;
        }
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}
