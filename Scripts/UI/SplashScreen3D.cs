using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;

namespace SafeRoom3D.UI;

/// <summary>
/// Splash screen for Safe Room 3D. Shows animated splash with "Test Dungeon" button.
/// </summary>
public partial class SplashScreen3D : Control
{
    /// <summary>
    /// Static property to store dungeon mode selection from splash screen.
    /// True = Test Map (large 160x160 dungeon), False = Training Grounds (small 80x80)
    /// </summary>
    public static bool UseTestMap { get; private set; } = true;

    /// <summary>
    /// Path to a custom map file to load. If set, overrides UseTestMap.
    /// </summary>
    public static string? CustomMapPath { get; set; } = null;

    /// <summary>
    /// If true, enter In-Map Edit Mode after the map loads.
    /// </summary>
    public static bool EnterEditModeAfterLoad { get; set; } = false;

    /// <summary>
    /// The map definition to use for edit mode.
    /// </summary>
    public static MapDefinition? EditModeMap { get; set; } = null;

    /// <summary>
    /// The path of the map being edited.
    /// </summary>
    public static string? EditModeMapPath { get; set; } = null;

    /// <summary>
    /// If true, return to the EditorScreen MapEditorTab after loading.
    /// Used by InMapEditor when exiting edit mode.
    /// </summary>
    public static bool ReturnToEditorAfterPlay { get; set; } = false;

    // Animation frames
    private Texture2D[]? _animationFrames;
    private int _currentFrame;
    private double _frameTimer;
    private const double FrameDuration = 1.0 / 12.0; // 12 fps
    private const int TotalFrames = 73;

    // UI elements
    private TextureRect? _splashImage;
    private Button? _testDungeonButton;
    private Button? _customMapButton;
    private Button? _musicButton;
    private Button? _soundButton;
    private Button? _editorButton;
    private Button? _optionsButton;
    private Button? _exitButton;
    private PanelContainer? _mapSelectorPopup;
    private ItemList? _mapSelectorList;
    private PanelContainer? _optionsPopup;
    private List<string> _mapFilePaths = new();  // Store full paths for map selection

    // State
    private bool _isAnimating;
    private bool _animationComplete;

    // Image container for button positioning
    private Control? _imageContainer;

    public override void _Ready()
    {
        // Check GPU and relaunch with OpenGL if Intel integrated graphics
        // Must be first - if relaunch happens, we return early
        if (GpuDetector.CheckAndRelaunchIfNeeded(this))
            return;

        // CRITICAL: Ensure proper state for splash screen
        // These can get stuck from editor/gameplay returning improperly
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Start splash music (create singleton if needed, deferred to ensure tree is ready)
        CallDeferred(nameof(StartSplashMusic));

        // Check if we need to return to the editor (from InMapEditor)
        if (ReturnToEditorAfterPlay)
        {
            ReturnToEditorAfterPlay = false;
            string? mapPath = CustomMapPath;
            CustomMapPath = null;

            // Go to EditorScreen and open the map
            CallDeferred(nameof(OpenEditorWithMap), mapPath ?? "");
            return;
        }

        // Create full-screen container
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Set dark background first (behind everything)
        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = new Color(0.02f, 0.02f, 0.05f);
        AddChild(bg);

        // Create centered container for the splash image
        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centerContainer);

        // Container to hold image and button overlay - will be sized to fit screen
        _imageContainer = new Control();
        centerContainer.AddChild(_imageContainer);

        // Splash image (static initially, then animated)
        _splashImage = new TextureRect();
        _splashImage.Texture = GD.Load<Texture2D>("res://Assets/Splash/Splash.png");
        _splashImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _splashImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _imageContainer.AddChild(_splashImage);

        // Calculate scaled size to fit screen (viewport is 1920x1080)
        // Image is 464x688, scale to fit height with some padding
        float targetHeight = 1000f;
        float scale = targetHeight / 688f;
        float scaledWidth = 464f * scale;
        float scaledHeight = targetHeight;

        _imageContainer.CustomMinimumSize = new Vector2(scaledWidth, scaledHeight);
        _imageContainer.CallDeferred(Control.MethodName.SetSize, new Vector2(scaledWidth, scaledHeight));
        _splashImage.SetAnchorsPreset(LayoutPreset.FullRect);
        _splashImage.CallDeferred(Control.MethodName.SetSize, new Vector2(scaledWidth, scaledHeight));

        // Invisible button positioned over "Enter Dungeon" text on splash image
        // Original: Position (127, 615), Size (210, 45) on 464x688 image
        var enterButton = new Button();
        enterButton.Text = "";
        enterButton.Position = new Vector2(127 * scale, 615 * scale);
        enterButton.Size = new Vector2(210 * scale, 45 * scale);
        enterButton.Flat = true;
        enterButton.MouseDefaultCursorShape = CursorShape.PointingHand;

        // Make button transparent but still clickable
        var transparentStyle = new StyleBoxEmpty();
        enterButton.AddThemeStyleboxOverride("normal", transparentStyle);
        enterButton.AddThemeStyleboxOverride("hover", transparentStyle);
        enterButton.AddThemeStyleboxOverride("pressed", transparentStyle);
        enterButton.AddThemeStyleboxOverride("focus", transparentStyle);

        enterButton.Pressed += OnEnterDungeonPressed;
        _imageContainer.AddChild(enterButton);

        // "Test Dungeon" button below "Enter Dungeon"
        CreateTestDungeonButton(scale);

        // "Custom Map" button below "Test Dungeon"
        CreateCustomMapButton(scale);

        // Create map selector popup (hidden initially)
        CreateMapSelectorPopup();

        // Editor button in top-right corner
        CreateEditorButton();

        // Audio toggle buttons in top-left corner
        CreateAudioButtons();

        // Exit Game button in bottom-left corner
        CreateExitButton();

        // Options popup (hidden initially)
        CreateOptionsPopup();

        // Title label for 3D version
        var titleLabel = new Label();
        titleLabel.Text = "SAFE ROOM 3D";
        titleLabel.SetAnchorsPreset(LayoutPreset.CenterTop);
        titleLabel.Position = new Vector2(-150, 20);
        titleLabel.AddThemeFontSizeOverride("font_size", 32);
        titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        AddChild(titleLabel);

        // Load animation frames
        LoadAnimationFrames();

        // Show mouse cursor on splash screen
        Input.MouseMode = Input.MouseModeEnum.Visible;

        GD.Print($"[SplashScreen3D] Ready - click 'Enter Dungeon' or 'Test Dungeon' to start");
    }

    private void LoadAnimationFrames()
    {
        var frameList = new List<Texture2D>();
        for (int i = 1; i <= TotalFrames; i++)
        {
            var path = $"res://Assets/Splash/Splash_frames/frame_{i:D3}.png";
            var texture = GD.Load<Texture2D>(path);
            if (texture != null)
            {
                frameList.Add(texture);
            }
        }
        _animationFrames = frameList.ToArray();
        GD.Print($"[SplashScreen3D] Loaded {_animationFrames.Length} animation frames");
    }

    private void CreateTestDungeonButton(float scale)
    {
        // "Test Map" button positioned below "Enter Dungeon"
        _testDungeonButton = new Button();
        _testDungeonButton.Text = "Test Map";
        _testDungeonButton.Position = new Vector2(127 * scale, 665 * scale);
        _testDungeonButton.Size = new Vector2(210 * scale, 40 * scale);
        _testDungeonButton.MouseDefaultCursorShape = CursorShape.PointingHand;

        // Style the button
        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.15f, 0.3f, 0.15f, 0.9f);
        normalStyle.SetBorderWidthAll(2);
        normalStyle.BorderColor = new Color(0.3f, 0.6f, 0.3f);
        normalStyle.SetCornerRadiusAll(6);
        _testDungeonButton.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.2f, 0.4f, 0.2f, 0.95f);
        hoverStyle.SetBorderWidthAll(2);
        hoverStyle.BorderColor = new Color(0.5f, 1f, 0.5f);
        hoverStyle.SetCornerRadiusAll(6);
        _testDungeonButton.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = new Color(0.1f, 0.25f, 0.1f, 0.95f);
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = new Color(0.5f, 1f, 0.5f);
        pressedStyle.SetCornerRadiusAll(6);
        _testDungeonButton.AddThemeStyleboxOverride("pressed", pressedStyle);

        _testDungeonButton.AddThemeColorOverride("font_color", new Color(0.8f, 1f, 0.8f));
        _testDungeonButton.AddThemeColorOverride("font_hover_color", new Color(0.9f, 1f, 0.9f));
        _testDungeonButton.AddThemeFontSizeOverride("font_size", 16);

        _testDungeonButton.Pressed += OnTestDungeonPressed;
        _imageContainer?.AddChild(_testDungeonButton);
    }

    private void CreateCustomMapButton(float scale)
    {
        // "Custom Map" button positioned ABOVE "Enter Dungeon" area
        _customMapButton = new Button();
        _customMapButton.Text = "Custom Map";
        _customMapButton.Position = new Vector2(127 * scale, 565 * scale);
        _customMapButton.Size = new Vector2(210 * scale, 40 * scale);
        _customMapButton.MouseDefaultCursorShape = CursorShape.PointingHand;

        // Style the button (purple/blue theme for custom)
        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.2f, 0.15f, 0.35f, 0.9f);
        normalStyle.SetBorderWidthAll(2);
        normalStyle.BorderColor = new Color(0.4f, 0.3f, 0.6f);
        normalStyle.SetCornerRadiusAll(6);
        _customMapButton.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.3f, 0.2f, 0.45f, 0.95f);
        hoverStyle.SetBorderWidthAll(2);
        hoverStyle.BorderColor = new Color(0.6f, 0.5f, 1f);
        hoverStyle.SetCornerRadiusAll(6);
        _customMapButton.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = new Color(0.15f, 0.1f, 0.3f, 0.95f);
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = new Color(0.6f, 0.5f, 1f);
        pressedStyle.SetCornerRadiusAll(6);
        _customMapButton.AddThemeStyleboxOverride("pressed", pressedStyle);

        _customMapButton.AddThemeColorOverride("font_color", new Color(0.85f, 0.8f, 1f));
        _customMapButton.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.9f, 1f));
        _customMapButton.AddThemeFontSizeOverride("font_size", 16);

        _customMapButton.Pressed += OnCustomMapPressed;
        _imageContainer?.AddChild(_customMapButton);
    }

    private void CreateMapSelectorPopup()
    {
        // Create popup panel for map selection
        _mapSelectorPopup = new PanelContainer();
        _mapSelectorPopup.SetAnchorsPreset(LayoutPreset.Center);
        _mapSelectorPopup.CustomMinimumSize = new Vector2(400, 350);
        _mapSelectorPopup.Position = new Vector2(-200, -175);
        _mapSelectorPopup.Visible = false;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.98f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.4f, 0.35f, 0.6f);
        panelStyle.SetCornerRadiusAll(10);
        _mapSelectorPopup.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_mapSelectorPopup);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        _mapSelectorPopup.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 15);
        margin.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "SELECT CUSTOM MAP";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(title);

        // Map list
        _mapSelectorList = new ItemList();
        _mapSelectorList.CustomMinimumSize = new Vector2(0, 180);
        _mapSelectorList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _mapSelectorList.ItemActivated += OnMapSelectorItemActivated;
        vbox.AddChild(_mapSelectorList);

        // Buttons
        var buttonBox = new HBoxContainer();
        buttonBox.Alignment = BoxContainer.AlignmentMode.Center;
        buttonBox.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(buttonBox);

        var playBtn = CreateStyledButton("Play");
        playBtn.CustomMinimumSize = new Vector2(100, 40);
        playBtn.Pressed += OnMapSelectorPlayPressed;
        buttonBox.AddChild(playBtn);

        var cancelBtn = CreateStyledButton("Cancel");
        cancelBtn.CustomMinimumSize = new Vector2(100, 40);
        cancelBtn.Pressed += OnMapSelectorCancelPressed;
        buttonBox.AddChild(cancelBtn);
    }

    private void OnCustomMapPressed()
    {
        if (_isAnimating || _animationComplete) return;

        // Refresh and show map selector
        RefreshMapSelectorList();
        if (_mapSelectorPopup != null)
        {
            _mapSelectorPopup.Visible = true;
        }
        GD.Print("[SplashScreen3D] Custom Map clicked - showing map selector");
    }

    private void RefreshMapSelectorList()
    {
        if (_mapSelectorList == null) return;

        _mapSelectorList.Clear();
        _mapFilePaths.Clear();

        var maps = Core.MapDefinition.GetSavedMapFilesWithPaths();
        foreach (var (name, path) in maps)
        {
            _mapSelectorList.AddItem(name);
            _mapFilePaths.Add(path);
        }

        if (maps.Count == 0)
        {
            _mapSelectorList.AddItem("(No maps found - create in Editor)");
        }

        GD.Print($"[SplashScreen3D] Found {maps.Count} custom maps");
    }

    private void OnMapSelectorItemActivated(long index)
    {
        // Double-click to play immediately
        OnMapSelectorPlayPressed();
    }

    private void OnMapSelectorPlayPressed()
    {
        if (_mapSelectorList == null) return;

        var selected = _mapSelectorList.GetSelectedItems();
        if (selected.Length == 0)
        {
            GD.Print("[SplashScreen3D] No map selected");
            return;
        }

        int selectedIndex = selected[0];
        string mapName = _mapSelectorList.GetItemText(selectedIndex);
        if (mapName.StartsWith("("))
        {
            // It's the "no maps found" message
            return;
        }

        // Use the stored full path instead of hardcoding user://maps/
        if (selectedIndex >= 0 && selectedIndex < _mapFilePaths.Count)
        {
            CustomMapPath = _mapFilePaths[selectedIndex];
        }
        else
        {
            CustomMapPath = $"res://maps/{mapName}.json";  // Fallback
        }
        GD.Print($"[SplashScreen3D] Selected custom map: {CustomMapPath}");

        // Hide popup and start animation
        if (_mapSelectorPopup != null)
            _mapSelectorPopup.Visible = false;

        StartAnimation();
    }

    private void OnMapSelectorCancelPressed()
    {
        if (_mapSelectorPopup != null)
            _mapSelectorPopup.Visible = false;
    }

    private void CreateEditorButton()
    {
        var editorContainer = new HBoxContainer();
        editorContainer.SetAnchorsPreset(LayoutPreset.TopRight);
        editorContainer.Position = new Vector2(-260, 20);
        editorContainer.AddThemeConstantOverride("separation", 10);
        AddChild(editorContainer);

        _optionsButton = CreateStyledButton("Options");
        _optionsButton.Pressed += OnOptionsButtonPressed;
        editorContainer.AddChild(_optionsButton);

        _editorButton = CreateStyledButton("Editor");
        _editorButton.Pressed += OnEditorButtonPressed;
        editorContainer.AddChild(_editorButton);
    }

    private void CreateExitButton()
    {
        var exitContainer = new HBoxContainer();
        exitContainer.SetAnchorsPreset(LayoutPreset.BottomLeft);
        exitContainer.Position = new Vector2(20, -70);
        AddChild(exitContainer);

        _exitButton = new Button();
        _exitButton.Text = "Exit Game";
        _exitButton.CustomMinimumSize = new Vector2(120, 40);
        _exitButton.MouseDefaultCursorShape = CursorShape.PointingHand;

        // Red-tinted style for exit button
        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.35f, 0.15f, 0.15f, 0.9f);
        normalStyle.SetBorderWidthAll(2);
        normalStyle.BorderColor = new Color(0.6f, 0.3f, 0.3f);
        normalStyle.SetCornerRadiusAll(6);
        _exitButton.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.5f, 0.2f, 0.2f, 0.95f);
        hoverStyle.SetBorderWidthAll(2);
        hoverStyle.BorderColor = new Color(1f, 0.4f, 0.4f);
        hoverStyle.SetCornerRadiusAll(6);
        _exitButton.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = new Color(0.25f, 0.1f, 0.1f, 0.95f);
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = new Color(1f, 0.4f, 0.4f);
        pressedStyle.SetCornerRadiusAll(6);
        _exitButton.AddThemeStyleboxOverride("pressed", pressedStyle);

        _exitButton.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.8f));
        _exitButton.AddThemeColorOverride("font_hover_color", new Color(1f, 0.9f, 0.9f));

        _exitButton.Pressed += OnExitButtonPressed;
        exitContainer.AddChild(_exitButton);
    }

    private void CreateOptionsPopup()
    {
        // Create popup panel for options
        _optionsPopup = new PanelContainer();
        _optionsPopup.SetAnchorsPreset(LayoutPreset.Center);
        _optionsPopup.CustomMinimumSize = new Vector2(350, 280);
        _optionsPopup.Position = new Vector2(-175, -140);
        _optionsPopup.Visible = false;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.98f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.5f, 0.5f, 0.6f);
        panelStyle.SetCornerRadiusAll(10);
        _optionsPopup.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_optionsPopup);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 25);
        margin.AddThemeConstantOverride("margin_right", 25);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        _optionsPopup.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        margin.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text = "OPTIONS";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(title);

        // Music toggle row
        var musicRow = new HBoxContainer();
        var musicLabel = new Label();
        musicLabel.Text = "Music:";
        musicLabel.CustomMinimumSize = new Vector2(100, 0);
        musicLabel.AddThemeFontSizeOverride("font_size", 18);
        musicRow.AddChild(musicLabel);
        var musicToggle = CreateToggleButton(AudioConfig.IsMusicEnabled ? "ON" : "OFF");
        musicToggle.Pressed += () =>
        {
            AudioConfig.ToggleMusic();
            musicToggle.Text = AudioConfig.IsMusicEnabled ? "ON" : "OFF";
            if (AudioConfig.IsMusicEnabled) SplashMusic.Instance?.Play();
            else SplashMusic.Instance?.Stop();
            UpdateAudioButtonLabels();
        };
        musicRow.AddChild(musicToggle);
        vbox.AddChild(musicRow);

        // Sound toggle row
        var soundRow = new HBoxContainer();
        var soundLabel = new Label();
        soundLabel.Text = "Sound:";
        soundLabel.CustomMinimumSize = new Vector2(100, 0);
        soundLabel.AddThemeFontSizeOverride("font_size", 18);
        soundRow.AddChild(soundLabel);
        var soundToggle = CreateToggleButton(AudioConfig.IsSoundEnabled ? "ON" : "OFF");
        soundToggle.Pressed += () =>
        {
            AudioConfig.ToggleSound();
            soundToggle.Text = AudioConfig.IsSoundEnabled ? "ON" : "OFF";
            UpdateAudioButtonLabels();
        };
        soundRow.AddChild(soundToggle);
        vbox.AddChild(soundRow);

        // Spacer
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 10);
        vbox.AddChild(spacer);

        // Close button
        var closeBtn = CreateStyledButton("Close");
        closeBtn.CustomMinimumSize = new Vector2(100, 40);
        closeBtn.Pressed += () => { if (_optionsPopup != null) _optionsPopup.Visible = false; };
        vbox.AddChild(closeBtn);
    }

    private void OnOptionsButtonPressed()
    {
        if (_optionsPopup != null)
        {
            _optionsPopup.Visible = !_optionsPopup.Visible;
        }
        GD.Print("[SplashScreen3D] Options button clicked");
    }

    private void OnExitButtonPressed()
    {
        GD.Print("[SplashScreen3D] Exit button clicked - quitting game");
        GetTree().Quit();
    }

    private void CreateAudioButtons()
    {
        var audioContainer = new HBoxContainer();
        audioContainer.SetAnchorsPreset(LayoutPreset.TopLeft);
        audioContainer.Position = new Vector2(20, 20);
        audioContainer.AddThemeConstantOverride("separation", 10);
        AddChild(audioContainer);

        // Music toggle button
        _musicButton = CreateToggleButton(AudioConfig.IsMusicEnabled ? "Music: ON" : "Music: OFF");
        _musicButton.Pressed += OnMusicButtonPressed;
        audioContainer.AddChild(_musicButton);

        // Sound toggle button
        _soundButton = CreateToggleButton(AudioConfig.IsSoundEnabled ? "Sound: ON" : "Sound: OFF");
        _soundButton.Pressed += OnSoundButtonPressed;
        audioContainer.AddChild(_soundButton);

        // Subscribe to config changes
        AudioConfig.ConfigChanged += UpdateAudioButtonLabels;
    }

    private Button CreateStyledButton(string text)
    {
        var button = new Button();
        button.Text = text;
        button.CustomMinimumSize = new Vector2(110, 35);
        button.MouseDefaultCursorShape = CursorShape.PointingHand;

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.9f);
        normalStyle.SetBorderWidthAll(2);
        normalStyle.BorderColor = new Color(0.5f, 0.5f, 0.6f);
        normalStyle.SetCornerRadiusAll(6);
        button.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.3f, 0.3f, 0.4f, 0.95f);
        hoverStyle.SetBorderWidthAll(2);
        hoverStyle.BorderColor = new Color(1f, 0.85f, 0.2f);
        hoverStyle.SetCornerRadiusAll(6);
        button.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = new Color(0.25f, 0.25f, 0.35f, 0.95f);
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = new Color(1f, 0.85f, 0.2f);
        pressedStyle.SetCornerRadiusAll(6);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);

        button.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.85f, 0.2f));

        return button;
    }

    private Button CreateToggleButton(string text)
    {
        var button = new Button();
        button.Text = text;
        button.CustomMinimumSize = new Vector2(100, 35);
        button.MouseDefaultCursorShape = CursorShape.PointingHand;

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.2f, 0.2f, 0.3f, 0.9f);
        normalStyle.SetBorderWidthAll(2);
        normalStyle.BorderColor = new Color(0.5f, 0.5f, 0.6f);
        normalStyle.SetCornerRadiusAll(6);
        button.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.3f, 0.3f, 0.4f, 0.95f);
        hoverStyle.SetBorderWidthAll(2);
        hoverStyle.BorderColor = new Color(1f, 0.85f, 0.2f);
        hoverStyle.SetCornerRadiusAll(6);
        button.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = new Color(0.25f, 0.25f, 0.35f, 0.95f);
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = new Color(1f, 0.85f, 0.2f);
        pressedStyle.SetCornerRadiusAll(6);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);

        button.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.85f, 0.2f));

        return button;
    }

    private void OnEnterDungeonPressed()
    {
        if (_isAnimating || _animationComplete) return;
        UseTestMap = false; // Enter Dungeon = smaller training grounds
        GD.Print("[SplashScreen3D] Enter Dungeon clicked - starting Training Grounds");
        StartAnimation();
    }

    private void OnTestDungeonPressed()
    {
        if (_isAnimating || _animationComplete) return;
        UseTestMap = true; // Test Map = large test dungeon
        GD.Print("[SplashScreen3D] Test Map clicked - starting Test Map");
        StartAnimation();
    }

    private void StartAnimation()
    {
        // Hide buttons
        if (_testDungeonButton != null)
            _testDungeonButton.Visible = false;

        _isAnimating = true;
        _currentFrame = 0;
        _frameTimer = 0;
    }

    private void OnMusicButtonPressed()
    {
        // Toggle music directly via AudioConfig (SoundManager3D isn't available on splash screen)
        AudioConfig.ToggleMusic();

        // Start or stop splash music based on new setting
        if (AudioConfig.IsMusicEnabled)
        {
            SplashMusic.Instance?.Play();
        }
        else
        {
            SplashMusic.Instance?.Stop();
        }

        // Update button label
        UpdateAudioButtonLabels();

        GD.Print($"[SplashScreen3D] Music toggled: {AudioConfig.IsMusicEnabled}");
    }

    private void OnSoundButtonPressed()
    {
        // Toggle sound directly via AudioConfig (SoundManager3D isn't available on splash screen)
        AudioConfig.ToggleSound();
        UpdateAudioButtonLabels();
        GD.Print($"[SplashScreen3D] Sound toggled: {AudioConfig.IsSoundEnabled}");
    }

    private void UpdateAudioButtonLabels()
    {
        if (_musicButton != null)
            _musicButton.Text = AudioConfig.IsMusicEnabled ? "Music: ON" : "Music: OFF";
        if (_soundButton != null)
            _soundButton.Text = AudioConfig.IsSoundEnabled ? "Sound: ON" : "Sound: OFF";
    }

    private void OnEditorButtonPressed()
    {
        GD.Print("[SplashScreen3D] Editor button clicked - opening EditorScreen3D");
        // Create and show editor screen
        var editorScreen = new EditorScreen3D();
        AddChild(editorScreen);
        editorScreen.Show();
    }

    public override void _Process(double delta)
    {
        if (!_isAnimating || _animationFrames == null || _splashImage == null) return;

        _frameTimer += delta;
        if (_frameTimer >= FrameDuration)
        {
            _frameTimer -= FrameDuration;
            _currentFrame++;

            if (_currentFrame >= _animationFrames.Length)
            {
                // Animation complete - transition to game
                _isAnimating = false;
                _animationComplete = true;
                GD.Print("[SplashScreen3D] Animation complete - loading game");
                TransitionToGame();
            }
            else
            {
                _splashImage.Texture = _animationFrames[_currentFrame];
            }
        }
    }

    private void TransitionToGame()
    {
        // Start the game - the GameManager will handle initialization
        GetTree().ChangeSceneToFile("res://Scenes/Main3D.tscn");
    }

    /// <summary>
    /// Opens the EditorScreen with the specified map loaded in the Map Editor tab.
    /// Called when returning from InMapEditor.
    /// </summary>
    private void OpenEditorWithMap(string mapPath)
    {
        // Create minimal UI for the editor screen
        SetAnchorsPreset(LayoutPreset.FullRect);
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Create dark background (important - prevents black screen if escape is pressed)
        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = new Color(0.02f, 0.02f, 0.05f);
        AddChild(bg);

        // Create and show the EditorScreen
        var editor = new EditorScreen3D();
        editor.Name = "EditorScreen";
        AddChild(editor);
        editor.Show();

        // If we have a map path, open it in the map editor tab
        if (!string.IsNullOrEmpty(mapPath))
        {
            // Use deferred call to ensure editor is fully initialized
            editor.CallDeferred(nameof(EditorScreen3D.OpenMapInEditor), mapPath);
        }

        GD.Print($"[SplashScreen3D] Opened editor with map: {mapPath}");
    }

    private void StartSplashMusic()
    {
        GD.Print("[SplashScreen3D] StartSplashMusic called");

        if (SplashMusic.Instance == null)
        {
            var splashMusic = new SplashMusic();
            splashMusic.Name = "SplashMusic";
            // Add to Root so it persists across scene changes
            GetTree().Root.AddChild(splashMusic);
            GD.Print("[SplashScreen3D] Created SplashMusic singleton");
        }

        // Play immediately - _Ready() runs synchronously when node is added
        if (SplashMusic.Instance != null)
        {
            SplashMusic.Instance.Play();
            GD.Print("[SplashScreen3D] SplashMusic started playing");
        }
        else
        {
            GD.PrintErr("[SplashScreen3D] SplashMusic.Instance is null after creation!");
        }
    }

    public override void _ExitTree()
    {
        AudioConfig.ConfigChanged -= UpdateAudioButtonLabels;
    }
}
