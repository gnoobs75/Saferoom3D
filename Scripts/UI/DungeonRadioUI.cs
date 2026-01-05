using Godot;

namespace SafeRoom3D.UI;

/// <summary>
/// Dungeon Radio UI - A cheesy Winamp-style music player interface.
/// Old school LCD display with scrolling text, retro buttons, and dungeon theme.
/// </summary>
public partial class DungeonRadioUI : CanvasLayer
{
    private Control? _panel;
    private Label? _titleDisplay;
    private Label? _artistDisplay;
    private Label? _timeDisplay;
    private Label? _statusLabel;
    private HSlider? _progressBar;
    private HSlider? _volumeSlider;
    private Button? _playPauseBtn;
    private Button? _stopBtn;
    private Button? _prevBtn;
    private Button? _nextBtn;
    private ColorRect? _visualizer;

    // Scrolling text
    private string _currentDisplayText = "";
    private float _scrollOffset;
    private const float ScrollSpeed = 40f;
    private const int DisplayChars = 28;

    // Visualizer animation
    private float[] _visualizerBars = new float[16];
    private ColorRect[] _visualizerBarRects = new ColorRect[16];
    private float _visualizerTime;

    // Colors - Dungeon/Retro theme
    private readonly Color _frameColor = new(0.15f, 0.12f, 0.1f);       // Dark stone
    private readonly Color _metalColor = new(0.35f, 0.3f, 0.25f);       // Aged metal
    private readonly Color _lcdBgColor = new(0.05f, 0.08f, 0.05f);      // Dark LCD
    private readonly Color _lcdTextColor = new(0.4f, 1f, 0.4f);         // Green LCD
    private readonly Color _lcdDimColor = new(0.15f, 0.3f, 0.15f);      // Dim LCD
    private readonly Color _buttonColor = new(0.25f, 0.22f, 0.2f);      // Stone button
    private readonly Color _glowColor = new(1f, 0.6f, 0.2f);            // Torch glow
    private readonly Color _accentColor = new(0.8f, 0.2f, 0.1f);        // Blood red accent

    public new bool IsVisible => _panel?.Visible ?? false;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;

        CreateUI();

        // Start hidden
        if (_panel != null) _panel.Visible = false;
    }

    private void CreateUI()
    {
        // Main panel - looks like an old portable radio
        _panel = new Control();
        _panel.Name = "RadioPanel";  // Name for UIEditorMode to find
        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.Size = new Vector2(380, 220);
        AddChild(_panel);

        // Position panel after it's in the tree so we can get viewport size
        CallDeferred(nameof(ApplyPanelPosition));

        // Background frame (stone/metal look)
        var frame = new ColorRect();
        frame.Size = _panel.Size;
        frame.Color = _frameColor;
        _panel.AddChild(frame);

        // Metal border
        var border = new ColorRect();
        border.Position = new Vector2(4, 4);
        border.Size = _panel.Size - new Vector2(8, 8);
        border.Color = _metalColor;
        _panel.AddChild(border);

        // Inner dark area
        var inner = new ColorRect();
        inner.Position = new Vector2(8, 8);
        inner.Size = _panel.Size - new Vector2(16, 16);
        inner.Color = new Color(0.08f, 0.06f, 0.05f);
        _panel.AddChild(inner);

        // Title bar with radio name
        var titleBar = CreateTitleBar();
        titleBar.Position = new Vector2(10, 10);
        _panel.AddChild(titleBar);

        // LCD Display area
        var lcdPanel = CreateLCDDisplay();
        lcdPanel.Position = new Vector2(15, 35);
        _panel.AddChild(lcdPanel);

        // Visualizer (fake spectrum)
        _visualizer = new ColorRect();
        _visualizer.Position = new Vector2(15, 115);
        _visualizer.Size = new Vector2(350, 25);
        _visualizer.Color = _lcdBgColor;
        _panel.AddChild(_visualizer);

        // Create visualizer bars
        float barWidth = 350f / _visualizerBarRects.Length - 2;
        float maxHeight = 23f;
        for (int i = 0; i < _visualizerBarRects.Length; i++)
        {
            var bar = new ColorRect();
            bar.Position = new Vector2(i * (barWidth + 2) + 2, maxHeight);
            bar.Size = new Vector2(barWidth, 2);
            bar.Color = _lcdTextColor;
            _visualizer.AddChild(bar);
            _visualizerBarRects[i] = bar;
        }

        // Progress bar
        _progressBar = new HSlider();
        _progressBar.Position = new Vector2(15, 145);
        _progressBar.Size = new Vector2(350, 16);
        _progressBar.MinValue = 0;
        _progressBar.MaxValue = 100;
        _progressBar.Step = 0.1;
        StyleSlider(_progressBar, _lcdTextColor);
        _progressBar.ValueChanged += OnProgressChanged;
        _panel.AddChild(_progressBar);

        // Control buttons
        CreateControlButtons();

        // Volume slider
        var volLabel = new Label();
        volLabel.Text = "VOL";
        volLabel.Position = new Vector2(270, 170);
        volLabel.AddThemeFontSizeOverride("font_size", 10);
        volLabel.AddThemeColorOverride("font_color", _lcdDimColor);
        _panel.AddChild(volLabel);

        _volumeSlider = new HSlider();
        _volumeSlider.Position = new Vector2(295, 172);
        _volumeSlider.Size = new Vector2(70, 14);
        _volumeSlider.MinValue = 0;
        _volumeSlider.MaxValue = 100;
        _volumeSlider.Value = 70;
        _volumeSlider.Step = 1;
        StyleSlider(_volumeSlider, _glowColor);
        _volumeSlider.ValueChanged += OnVolumeChanged;
        _panel.AddChild(_volumeSlider);

        // Close button (X)
        var closeBtn = new Button();
        closeBtn.Text = "X";
        closeBtn.Position = new Vector2(350, 8);
        closeBtn.Size = new Vector2(22, 22);
        StyleButton(closeBtn, _accentColor);
        closeBtn.Pressed += () => Toggle();
        _panel.AddChild(closeBtn);

        // Status text
        _statusLabel = new Label();
        _statusLabel.Position = new Vector2(15, 195);
        _statusLabel.AddThemeFontSizeOverride("font_size", 10);
        _statusLabel.AddThemeColorOverride("font_color", _lcdDimColor);
        _statusLabel.Text = "Press Y to toggle | DUNGEON RADIO v1.0";
        _panel.AddChild(_statusLabel);
    }

    private Control CreateTitleBar()
    {
        var container = new Control();
        container.Size = new Vector2(360, 22);

        // Decorative torches on sides
        var torchLeft = new Label();
        torchLeft.Text = "*";
        torchLeft.Position = new Vector2(0, 0);
        torchLeft.AddThemeFontSizeOverride("font_size", 18);
        torchLeft.AddThemeColorOverride("font_color", _glowColor);
        container.AddChild(torchLeft);

        var title = new Label();
        title.Text = "~~~ DUNGEON RADIO ~~~";
        title.Position = new Vector2(90, 2);
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", _glowColor);
        container.AddChild(title);

        var torchRight = new Label();
        torchRight.Text = "*";
        torchRight.Position = new Vector2(340, 0);
        torchRight.AddThemeFontSizeOverride("font_size", 18);
        torchRight.AddThemeColorOverride("font_color", _glowColor);
        container.AddChild(torchRight);

        return container;
    }

    private Control CreateLCDDisplay()
    {
        var lcdContainer = new Control();
        lcdContainer.Size = new Vector2(350, 75);

        // LCD background
        var lcdBg = new ColorRect();
        lcdBg.Size = lcdContainer.Size;
        lcdBg.Color = _lcdBgColor;
        lcdContainer.AddChild(lcdBg);

        // LCD border (inset look)
        var lcdBorder = new ColorRect();
        lcdBorder.Position = new Vector2(2, 2);
        lcdBorder.Size = new Vector2(346, 71);
        lcdBorder.Color = new Color(0.02f, 0.04f, 0.02f);
        lcdContainer.AddChild(lcdBorder);

        // Track number indicator
        var trackNum = new Label();
        trackNum.Name = "TrackNum";
        trackNum.Text = "01";
        trackNum.Position = new Vector2(8, 5);
        trackNum.AddThemeFontSizeOverride("font_size", 24);
        trackNum.AddThemeColorOverride("font_color", _lcdTextColor);
        lcdContainer.AddChild(trackNum);

        // Title display (scrolling)
        _titleDisplay = new Label();
        _titleDisplay.Position = new Vector2(55, 8);
        _titleDisplay.Size = new Vector2(285, 25);
        _titleDisplay.ClipText = true;
        _titleDisplay.AddThemeFontSizeOverride("font_size", 16);
        _titleDisplay.AddThemeColorOverride("font_color", _lcdTextColor);
        _titleDisplay.Text = "NO TRACK LOADED";
        lcdContainer.AddChild(_titleDisplay);

        // Artist display
        _artistDisplay = new Label();
        _artistDisplay.Position = new Vector2(8, 35);
        _artistDisplay.Size = new Vector2(230, 20);
        _artistDisplay.AddThemeFontSizeOverride("font_size", 12);
        _artistDisplay.AddThemeColorOverride("font_color", _lcdDimColor);
        _artistDisplay.Text = "";
        lcdContainer.AddChild(_artistDisplay);

        // Time display
        _timeDisplay = new Label();
        _timeDisplay.Position = new Vector2(250, 35);
        _timeDisplay.Size = new Vector2(90, 20);
        _timeDisplay.HorizontalAlignment = HorizontalAlignment.Right;
        _timeDisplay.AddThemeFontSizeOverride("font_size", 14);
        _timeDisplay.AddThemeColorOverride("font_color", _lcdTextColor);
        _timeDisplay.Text = "00:00/00:00";
        lcdContainer.AddChild(_timeDisplay);

        // Bitrate/format display
        var formatLabel = new Label();
        formatLabel.Position = new Vector2(8, 55);
        formatLabel.AddThemeFontSizeOverride("font_size", 10);
        formatLabel.AddThemeColorOverride("font_color", _lcdDimColor);
        formatLabel.Text = "MP3 | 44kHz | STEREO";
        lcdContainer.AddChild(formatLabel);

        return lcdContainer;
    }

    private void CreateControlButtons()
    {
        float btnY = 168;
        float btnSize = 32;
        float startX = 15;
        float spacing = 38;

        // Previous button |<
        _prevBtn = new Button();
        _prevBtn.Text = "|<";
        _prevBtn.Position = new Vector2(startX, btnY);
        _prevBtn.Size = new Vector2(btnSize, btnSize);
        StyleButton(_prevBtn, _buttonColor);
        _prevBtn.Pressed += () => DungeonRadio.Instance?.PreviousTrack();
        _panel?.AddChild(_prevBtn);

        // Play/Pause button
        _playPauseBtn = new Button();
        _playPauseBtn.Text = ">";
        _playPauseBtn.Position = new Vector2(startX + spacing, btnY);
        _playPauseBtn.Size = new Vector2(btnSize, btnSize);
        StyleButton(_playPauseBtn, _lcdTextColor);
        _playPauseBtn.Pressed += OnPlayPausePressed;
        _panel?.AddChild(_playPauseBtn);

        // Stop button
        _stopBtn = new Button();
        _stopBtn.Text = "#";
        _stopBtn.Position = new Vector2(startX + spacing * 2, btnY);
        _stopBtn.Size = new Vector2(btnSize, btnSize);
        StyleButton(_stopBtn, _buttonColor);
        _stopBtn.Pressed += () => DungeonRadio.Instance?.Stop();
        _panel?.AddChild(_stopBtn);

        // Next button >|
        _nextBtn = new Button();
        _nextBtn.Text = ">|";
        _nextBtn.Position = new Vector2(startX + spacing * 3, btnY);
        _nextBtn.Size = new Vector2(btnSize, btnSize);
        StyleButton(_nextBtn, _buttonColor);
        _nextBtn.Pressed += () => DungeonRadio.Instance?.NextTrack();
        _panel?.AddChild(_nextBtn);

        // Shuffle indicator
        var shuffleLabel = new Label();
        shuffleLabel.Text = "[SHUFFLE]";
        shuffleLabel.Position = new Vector2(startX + spacing * 4 + 15, btnY + 8);
        shuffleLabel.AddThemeFontSizeOverride("font_size", 10);
        shuffleLabel.AddThemeColorOverride("font_color", _lcdDimColor);
        _panel?.AddChild(shuffleLabel);
    }

    private void StyleButton(Button btn, Color color)
    {
        var style = new StyleBoxFlat();
        style.BgColor = color;
        style.SetCornerRadiusAll(3);
        style.SetBorderWidthAll(1);
        style.BorderColor = color * 1.3f;
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = color * 1.2f;
        hoverStyle.SetCornerRadiusAll(3);
        hoverStyle.SetBorderWidthAll(1);
        hoverStyle.BorderColor = _glowColor;
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = color * 0.8f;
        pressedStyle.SetCornerRadiusAll(3);
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = _glowColor;
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);

        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.85f));
    }

    private void StyleSlider(HSlider slider, Color color)
    {
        // Track background
        var trackStyle = new StyleBoxFlat();
        trackStyle.BgColor = _lcdBgColor;
        trackStyle.SetCornerRadiusAll(2);
        slider.AddThemeStyleboxOverride("slider", trackStyle);

        // Grabber
        var grabberStyle = new StyleBoxFlat();
        grabberStyle.BgColor = color;
        grabberStyle.SetCornerRadiusAll(4);
        slider.AddThemeStyleboxOverride("grabber_area", grabberStyle);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", grabberStyle);
    }

    public void Toggle()
    {
        if (_panel == null) return;

        _panel.Visible = !_panel.Visible;

        if (_panel.Visible)
        {
            // Show cursor
            Input.MouseMode = Input.MouseModeEnum.Visible;
            // Auto-play if nothing playing
            var radio = DungeonRadio.Instance;
            if (radio != null && !radio.IsPlaying && !radio.IsPaused)
            {
                radio.Play();
            }
            UpdateDisplay();
        }
        else
        {
            // Return cursor to game if no other UI open
            if (EscapeMenu3D.Instance?.Visible != true &&
                InventoryUI3D.Instance?.Visible != true &&
                SpellBook3D.Instance?.Visible != true)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_panel == null || !_panel.Visible) return;

        var radio = DungeonRadio.Instance;
        if (radio == null) return;

        UpdateDisplay();
        UpdateScrollingText((float)delta);
        UpdateVisualizer((float)delta);
        UpdateProgressBar();
    }

    private void UpdateDisplay()
    {
        var radio = DungeonRadio.Instance;
        if (radio == null) return;

        var track = radio.GetCurrentTrack();
        if (track != null)
        {
            // Update track number
            var trackNum = _panel?.FindChild("TrackNum", true, false) as Label;
            if (trackNum != null)
            {
                trackNum.Text = $"{radio.GetCurrentTrackIndex() + 1:D2}";
            }

            // Update scrolling text if track changed
            string newText = $"    {track.Title}    ***    ";
            if (_currentDisplayText != newText)
            {
                _currentDisplayText = newText;
                _scrollOffset = 0;
            }

            // Update artist
            if (_artistDisplay != null)
            {
                _artistDisplay.Text = track.Artist;
            }

            // Update time
            if (_timeDisplay != null)
            {
                float pos = radio.PlaybackPosition;
                float len = radio.TrackLength;
                _timeDisplay.Text = $"{FormatTime(pos)}/{FormatTime(len)}";
            }

            // Update play/pause button
            if (_playPauseBtn != null)
            {
                _playPauseBtn.Text = radio.IsPlaying && !radio.IsPaused ? "||" : ">";
            }
        }
        else
        {
            if (_titleDisplay != null) _titleDisplay.Text = "NO TRACK LOADED";
            if (_artistDisplay != null) _artistDisplay.Text = "";
            if (_timeDisplay != null) _timeDisplay.Text = "00:00/00:00";
        }
    }

    private void UpdateScrollingText(float delta)
    {
        if (_titleDisplay == null || string.IsNullOrEmpty(_currentDisplayText)) return;

        _scrollOffset += ScrollSpeed * delta;
        if (_scrollOffset >= _currentDisplayText.Length * 8) // Approximate char width
        {
            _scrollOffset = 0;
        }

        // Create scrolling effect by offsetting the text
        int charOffset = (int)(_scrollOffset / 8);
        string displayText = "";

        for (int i = 0; i < DisplayChars; i++)
        {
            int idx = (charOffset + i) % _currentDisplayText.Length;
            displayText += _currentDisplayText[idx];
        }

        _titleDisplay.Text = displayText;
    }

    private void UpdateVisualizer(float delta)
    {
        var radio = DungeonRadio.Instance;
        float maxHeight = 23f;
        float barWidth = 350f / _visualizerBarRects.Length - 2;

        if (radio == null || !radio.IsPlaying || radio.IsPaused)
        {
            // Flat bars when not playing
            for (int i = 0; i < _visualizerBarRects.Length; i++)
            {
                if (_visualizerBarRects[i] != null)
                {
                    _visualizerBarRects[i].Size = new Vector2(barWidth, 2);
                    _visualizerBarRects[i].Position = new Vector2(i * (barWidth + 2) + 2, maxHeight - 2);
                }
            }
            return;
        }

        _visualizerTime += delta;

        // Fake spectrum analyzer - bouncing bars
        for (int i = 0; i < _visualizerBars.Length; i++)
        {
            float target = (float)GD.RandRange(0.2f, 1.0f);
            // Add some wave pattern
            target *= 0.5f + 0.5f * Mathf.Sin(_visualizerTime * 3f + i * 0.5f);
            _visualizerBars[i] = Mathf.Lerp(_visualizerBars[i], target, delta * 10f);

            // Update bar rect
            if (_visualizerBarRects[i] != null)
            {
                float height = _visualizerBars[i] * maxHeight;
                _visualizerBarRects[i].Size = new Vector2(barWidth, height);
                _visualizerBarRects[i].Position = new Vector2(i * (barWidth + 2) + 2, maxHeight - height);
            }
        }
    }

    private void UpdateProgressBar()
    {
        var radio = DungeonRadio.Instance;
        if (radio == null || _progressBar == null) return;

        float len = radio.TrackLength;
        if (len > 0)
        {
            float pos = radio.PlaybackPosition;
            _progressBar.SetValueNoSignal((pos / len) * 100);
        }
    }

    private void OnPlayPausePressed()
    {
        DungeonRadio.Instance?.TogglePlayPause();
    }

    private void OnProgressChanged(double value)
    {
        var radio = DungeonRadio.Instance;
        if (radio == null) return;

        float len = radio.TrackLength;
        if (len > 0)
        {
            float pos = (float)(value / 100.0) * len;
            radio.Seek(pos);
        }
    }

    private void OnVolumeChanged(double value)
    {
        DungeonRadio.Instance?.SetVolume((float)(value / 100.0));
    }

    private string FormatTime(float seconds)
    {
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return $"{mins:D2}:{secs:D2}";
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisible) return;

        // Handle escape to close
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                Toggle();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Apply saved position with viewport clamping, or center if no valid position.
    /// Called deferred after panel is in tree so viewport size is available.
    /// </summary>
    private void ApplyPanelPosition()
    {
        if (_panel == null) return;

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var panelSize = _panel.Size;

        // Default: center of screen
        var defaultPos = new Vector2(
            (viewportSize.X - panelSize.X) / 2,
            (viewportSize.Y - panelSize.Y) / 2
        );

        var savedPos = WindowPositionManager.GetPosition("HUD_DungeonRadio");
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            // Check if saved position is remotely valid (on screen)
            bool isValidPosition = savedPos.X > -panelSize.X &&
                                   savedPos.X < viewportSize.X &&
                                   savedPos.Y > -panelSize.Y &&
                                   savedPos.Y < viewportSize.Y;

            if (isValidPosition)
            {
                // Clamp to viewport to be safe
                _panel.Position = WindowPositionManager.ClampToViewport(savedPos, viewportSize, panelSize);
                GD.Print($"[DungeonRadioUI] Applied saved position: {_panel.Position}");
            }
            else
            {
                // Bad position - clear it and use default
                GD.Print($"[DungeonRadioUI] Invalid saved position {savedPos}, clearing and using default");
                WindowPositionManager.ClearPosition("HUD_DungeonRadio");
                _panel.Position = defaultPos;
            }
        }
        else
        {
            _panel.Position = defaultPos;
            GD.Print($"[DungeonRadioUI] Using default position: {_panel.Position}");
        }
    }
}
