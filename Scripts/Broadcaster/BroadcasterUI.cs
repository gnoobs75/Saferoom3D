using Godot;
using System;

namespace SafeRoom3D.Broadcaster;

/// <summary>
/// The UI panel that displays the AI broadcaster.
/// Styled like a video call overlay in the corner of the screen.
/// </summary>
public partial class BroadcasterUI : Control
{
    // Signals
    [Signal] public delegate void MutePressedEventHandler();
    [Signal] public delegate void MinimizePressedEventHandler();

    // UI elements
    private PanelContainer? _mainPanel;
    private SubViewportContainer? _avatarViewportContainer;
    private SubViewport? _avatarViewport;
    private RichTextLabel? _commentLabel;
    private Button? _muteButton;
    private Button? _minimizeButton;
    private HBoxContainer? _buttonBar;
    private Control? _avatarContainer;

    // State
    private bool _isMuted = false;
    private bool _isMinimized = false;
    private Tween? _slideTween;
    private Tween? _textTween;

    // Configuration
    private const float PanelWidth = 280f;
    private const float PanelHeight = 200f;
    private const float MinimizedWidth = 60f;
    private const float MinimizedHeight = 60f;
    private const float AvatarHeight = 120f;
    private const float Margin = 15f;
    private const float SlideSpeed = 0.3f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);

        BuildUI();
        PositionPanel(false);
    }

    private void BuildUI()
    {
        // Main panel container - styled like a video call
        _mainPanel = new PanelContainer
        {
            Name = "BroadcasterPanel",
            CustomMinimumSize = new Vector2(PanelWidth, PanelHeight),
        };
        AddChild(_mainPanel);

        // Style the panel
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f),
            BorderColor = new Color(0.3f, 0.6f, 0.9f, 0.8f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 8,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
        };
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);

        // VBox for layout
        var vbox = new VBoxContainer
        {
            Name = "VBox",
        };
        vbox.AddThemeConstantOverride("separation", 8);
        _mainPanel.AddChild(vbox);

        // Top bar with title and buttons
        var topBar = new HBoxContainer { Name = "TopBar" };
        vbox.AddChild(topBar);

        var titleLabel = new Label
        {
            Text = "BROADCAST LIVE",
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 11);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
        topBar.AddChild(titleLabel);

        // Live indicator (pulsing dot)
        var liveIndicator = new ColorRect
        {
            CustomMinimumSize = new Vector2(8, 8),
            Color = new Color(0.9f, 0.2f, 0.2f),
        };
        topBar.AddChild(liveIndicator);

        // Button bar
        _buttonBar = new HBoxContainer { Name = "ButtonBar" };
        _buttonBar.AddThemeConstantOverride("separation", 4);
        topBar.AddChild(_buttonBar);

        // Mute button
        _muteButton = new Button
        {
            Text = "M",
            CustomMinimumSize = new Vector2(24, 24),
            TooltipText = "Mute/Unmute AI Voice",
        };
        _muteButton.Pressed += () => EmitSignal(SignalName.MutePressed);
        _buttonBar.AddChild(_muteButton);

        // Minimize button
        _minimizeButton = new Button
        {
            Text = "_",
            CustomMinimumSize = new Vector2(24, 24),
            TooltipText = "Minimize/Restore",
        };
        _minimizeButton.Pressed += () => EmitSignal(SignalName.MinimizePressed);
        _buttonBar.AddChild(_minimizeButton);

        // Avatar container (will hold the SubViewport for 3D avatar)
        _avatarContainer = new Control
        {
            Name = "AvatarContainer",
            CustomMinimumSize = new Vector2(PanelWidth - 16, AvatarHeight),
        };
        vbox.AddChild(_avatarContainer);

        // Create SubViewport for 3D avatar rendering
        _avatarViewportContainer = new SubViewportContainer
        {
            Name = "AvatarViewportContainer",
            Stretch = true,
            CustomMinimumSize = new Vector2(PanelWidth - 16, AvatarHeight),
        };
        _avatarContainer.AddChild(_avatarViewportContainer);

        _avatarViewport = new SubViewport
        {
            Name = "AvatarViewport",
            Size = new Vector2I((int)(PanelWidth - 16), (int)AvatarHeight),
            TransparentBg = true,
            HandleInputLocally = false,
            GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        _avatarViewportContainer.AddChild(_avatarViewport);

        // Comment text area
        _commentLabel = new RichTextLabel
        {
            Name = "CommentLabel",
            BbcodeEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollActive = false,
        };
        _commentLabel.AddThemeFontSizeOverride("normal_font_size", 12);
        _commentLabel.AddThemeColorOverride("default_color", new Color(0.9f, 0.9f, 0.9f));
        vbox.AddChild(_commentLabel);
    }

    private void PositionPanel(bool animate)
    {
        if (_mainPanel == null) return;

        // Position in top-right corner
        Vector2 targetPos;
        Vector2 targetSize;

        if (_isMinimized)
        {
            targetPos = new Vector2(
                GetViewportRect().Size.X - MinimizedWidth - Margin,
                Margin
            );
            targetSize = new Vector2(MinimizedWidth, MinimizedHeight);
        }
        else
        {
            targetPos = new Vector2(
                GetViewportRect().Size.X - PanelWidth - Margin,
                Margin
            );
            targetSize = new Vector2(PanelWidth, PanelHeight);
        }

        if (animate)
        {
            _slideTween?.Kill();
            _slideTween = CreateTween();
            _slideTween.SetEase(Tween.EaseType.Out);
            _slideTween.SetTrans(Tween.TransitionType.Back);
            _slideTween.TweenProperty(_mainPanel, "position", targetPos, SlideSpeed);
            _slideTween.Parallel().TweenProperty(_mainPanel, "custom_minimum_size", targetSize, SlideSpeed);
        }
        else
        {
            _mainPanel.Position = targetPos;
            _mainPanel.CustomMinimumSize = targetSize;
        }
    }

    /// <summary>
    /// Set the 3D avatar to display in the viewport
    /// </summary>
    public void SetAvatar(BroadcasterAvatar avatar)
    {
        if (_avatarViewport == null) return;

        _avatarViewport.AddChild(avatar);
    }

    /// <summary>
    /// Display a comment from the AI
    /// </summary>
    public void ShowComment(string text)
    {
        if (_commentLabel == null) return;

        // Animate text appearance with typewriter effect
        _textTween?.Kill();

        _commentLabel.Text = "";
        _commentLabel.VisibleCharacters = 0;

        // Use BBCode for styling
        string styledText = $"[color=#aaccff]\"{text}\"[/color]";
        _commentLabel.Text = styledText;

        // Typewriter animation
        _textTween = CreateTween();
        _textTween.TweenProperty(_commentLabel, "visible_characters", text.Length + 20, text.Length * 0.03f);
    }

    /// <summary>
    /// Set muted state (affects button appearance)
    /// </summary>
    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        if (_muteButton != null)
        {
            _muteButton.Text = muted ? "X" : "M";
            _muteButton.Modulate = muted ? new Color(0.9f, 0.3f, 0.3f) : Colors.White;
        }
    }

    /// <summary>
    /// Set minimized state
    /// </summary>
    public void SetMinimized(bool minimized)
    {
        _isMinimized = minimized;

        // Hide/show elements
        if (_avatarContainer != null)
            _avatarContainer.Visible = !minimized;
        if (_commentLabel != null)
            _commentLabel.Visible = !minimized;
        if (_buttonBar != null)
            _buttonBar.Visible = !minimized;

        if (_minimizeButton != null)
        {
            _minimizeButton.Text = minimized ? "+" : "_";
        }

        PositionPanel(true);
    }

    /// <summary>
    /// Slide panel in from off-screen
    /// </summary>
    public void SlideIn()
    {
        if (_mainPanel == null) return;

        // Start off-screen to the right
        _mainPanel.Position = new Vector2(GetViewportRect().Size.X + 50, Margin);

        // Animate in
        PositionPanel(true);
    }

    /// <summary>
    /// Flash the panel border for important events
    /// </summary>
    public void FlashBorder(Color color)
    {
        if (_mainPanel == null) return;

        var style = _mainPanel.GetThemeStylebox("panel") as StyleBoxFlat;
        if (style == null) return;

        var originalColor = style.BorderColor;

        var tween = CreateTween();
        tween.TweenProperty(style, "border_color", color, 0.1f);
        tween.TweenProperty(style, "border_color", originalColor, 0.3f);
    }

    public override void _Notification(int what)
    {
        // Handle window resize
        if (what == NotificationWMSizeChanged)
        {
            PositionPanel(false);
        }
    }
}
