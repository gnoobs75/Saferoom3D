using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Broadcaster;

/// <summary>
/// Notification item for loot/achievements
/// </summary>
public class NotificationItem
{
    public string Text { get; set; } = "";
    public Color Color { get; set; } = Colors.White;
    public float TimeRemaining { get; set; } = 5f;
    public float Alpha { get; set; } = 1f;
    public Label? Label { get; set; }
}

/// <summary>
/// The UI panel that displays the AI broadcaster.
/// Now positioned top-left with notification columns for loot and achievements.
/// Includes fake viewer metrics with combat-reactive ratings gauge.
/// </summary>
public partial class BroadcasterUI : Control
{
    // Signals
    [Signal] public delegate void MutePressedEventHandler();
    [Signal] public delegate void MinimizePressedEventHandler();

    // UI elements - Main panel
    private PanelContainer? _mainPanel;
    private SubViewportContainer? _avatarViewportContainer;
    private SubViewport? _avatarViewport;
    private RichTextLabel? _commentLabel;
    private Button? _muteButton;
    private Button? _minimizeButton;
    private HBoxContainer? _buttonBar;
    private Control? _avatarContainer;

    // Viewer metrics UI
    private PanelContainer? _metricsPanel;
    private Label? _viewsCountLabel;
    private Label? _followersCountLabel;
    private ProgressBar? _ratingsGauge;
    private Label? _ratingsLabel;

    // Notification columns
    private VBoxContainer? _lootColumn;
    private VBoxContainer? _achievementColumn;
    private List<NotificationItem> _lootNotifications = new();
    private List<NotificationItem> _achievementNotifications = new();

    // State
    private bool _isMuted = false;
    private bool _isMinimized = false;
    private Tween? _slideTween;
    private Tween? _textTween;

    // Metrics state - Views always go up, Followers fluctuate (views always > followers)
    private long _totalViews = 1247;
    private float _viewsAccumulator = 0f;
    private float _viewsPerSecond = 2.5f;
    private int _followers = 312;
    private float _followerTimer = 0f;
    private float _nextFollowerChange = 1f;
    private float _currentRating = 0.3f;   // 0-1 scale
    private float _targetRating = 0.3f;
    private float _ratingDecayTimer = 0f;
    private bool _isInCombat = false;

    // Configuration
    private const float PanelWidth = 260f;
    private const float PanelHeight = 180f;
    private const float MinimizedWidth = 50f;
    private const float MinimizedHeight = 50f;
    private const float AvatarHeight = 100f;
    private const float Margin = 10f;
    private const float SlideSpeed = 0.3f;
    private const float NotificationColumnWidth = 200f;
    private const float NotificationFadeTime = 1.0f;
    private const float NotificationDuration = 4.0f;
    private const int MaxNotificationsPerColumn = 5;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);

        BuildUI();
        BuildNotificationColumns();
        BuildMetricsPanel();
        PositionPanel(false);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        UpdateNotifications(dt);
        UpdateViewerMetrics(dt);
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
            ContentMarginBottom = 6,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
        };
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);

        // VBox for layout
        var vbox = new VBoxContainer { Name = "VBox" };
        vbox.AddThemeConstantOverride("separation", 4);
        _mainPanel.AddChild(vbox);

        // Top bar with title and buttons
        var topBar = new HBoxContainer { Name = "TopBar" };
        vbox.AddChild(topBar);

        var titleLabel = new Label
        {
            Text = "LIVE",
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 10);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
        topBar.AddChild(titleLabel);

        // Live indicator (pulsing dot will be animated)
        var liveIndicator = new ColorRect
        {
            CustomMinimumSize = new Vector2(6, 6),
            Color = new Color(0.9f, 0.2f, 0.2f),
        };
        topBar.AddChild(liveIndicator);

        // Spacer
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        topBar.AddChild(spacer);

        // Button bar
        _buttonBar = new HBoxContainer { Name = "ButtonBar" };
        _buttonBar.AddThemeConstantOverride("separation", 2);
        topBar.AddChild(_buttonBar);

        // Mute button
        _muteButton = new Button
        {
            Text = "M",
            CustomMinimumSize = new Vector2(20, 20),
            TooltipText = "Mute/Unmute",
        };
        _muteButton.AddThemeFontSizeOverride("font_size", 9);
        _muteButton.Pressed += () => EmitSignal(SignalName.MutePressed);
        _buttonBar.AddChild(_muteButton);

        // Minimize button
        _minimizeButton = new Button
        {
            Text = "_",
            CustomMinimumSize = new Vector2(20, 20),
            TooltipText = "Minimize",
        };
        _minimizeButton.AddThemeFontSizeOverride("font_size", 9);
        _minimizeButton.Pressed += () => EmitSignal(SignalName.MinimizePressed);
        _buttonBar.AddChild(_minimizeButton);

        // Avatar container
        _avatarContainer = new Control
        {
            Name = "AvatarContainer",
            CustomMinimumSize = new Vector2(PanelWidth - 12, AvatarHeight),
        };
        vbox.AddChild(_avatarContainer);

        // Create SubViewport for 3D avatar rendering
        _avatarViewportContainer = new SubViewportContainer
        {
            Name = "AvatarViewportContainer",
            Stretch = true,
            CustomMinimumSize = new Vector2(PanelWidth - 12, AvatarHeight),
        };
        _avatarContainer.AddChild(_avatarViewportContainer);

        _avatarViewport = new SubViewport
        {
            Name = "AvatarViewport",
            Size = new Vector2I((int)(PanelWidth - 12), (int)AvatarHeight),
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
            CustomMinimumSize = new Vector2(0, 30),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollActive = false,
        };
        _commentLabel.AddThemeFontSizeOverride("normal_font_size", 10);
        _commentLabel.AddThemeColorOverride("default_color", new Color(0.9f, 0.9f, 0.9f));
        vbox.AddChild(_commentLabel);
    }

    private void BuildNotificationColumns()
    {
        // Loot notifications column (next to broadcaster)
        _lootColumn = new VBoxContainer
        {
            Name = "LootColumn",
            Position = new Vector2(PanelWidth + Margin + 10, Margin),
            CustomMinimumSize = new Vector2(NotificationColumnWidth, 200),
        };
        _lootColumn.AddThemeConstantOverride("separation", 4);
        AddChild(_lootColumn);

        // Header for loot
        var lootHeader = new Label
        {
            Text = "LOOT",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        lootHeader.AddThemeFontSizeOverride("font_size", 9);
        lootHeader.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f, 0.7f));
        _lootColumn.AddChild(lootHeader);

        // Achievement notifications column (next to loot)
        _achievementColumn = new VBoxContainer
        {
            Name = "AchievementColumn",
            Position = new Vector2(PanelWidth + Margin + NotificationColumnWidth + 20, Margin),
            CustomMinimumSize = new Vector2(NotificationColumnWidth, 200),
        };
        _achievementColumn.AddThemeConstantOverride("separation", 4);
        AddChild(_achievementColumn);

        // Header for achievements
        var achievementHeader = new Label
        {
            Text = "ACHIEVEMENTS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        achievementHeader.AddThemeFontSizeOverride("font_size", 9);
        achievementHeader.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.5f, 0.7f));
        _achievementColumn.AddChild(achievementHeader);
    }

    private void BuildMetricsPanel()
    {
        // Small metrics panel below the main broadcaster panel
        _metricsPanel = new PanelContainer
        {
            Name = "MetricsPanel",
            CustomMinimumSize = new Vector2(PanelWidth, 65),
        };
        AddChild(_metricsPanel);

        var metricsStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.08f, 0.9f),
            BorderColor = new Color(0.2f, 0.4f, 0.6f, 0.6f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginBottom = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
        };
        _metricsPanel.AddThemeStyleboxOverride("panel", metricsStyle);

        var metricsVBox = new VBoxContainer();
        metricsVBox.AddThemeConstantOverride("separation", 2);
        _metricsPanel.AddChild(metricsVBox);

        // Views count (always increasing)
        var viewsHBox = new HBoxContainer();
        metricsVBox.AddChild(viewsHBox);

        var viewsIcon = new Label { Text = "👁" };
        viewsIcon.AddThemeFontSizeOverride("font_size", 10);
        viewsHBox.AddChild(viewsIcon);

        _viewsCountLabel = new Label
        {
            Text = "1.2K views",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _viewsCountLabel.AddThemeFontSizeOverride("font_size", 10);
        _viewsCountLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.9f));
        viewsHBox.AddChild(_viewsCountLabel);

        // Followers count (fluctuates)
        var followersHBox = new HBoxContainer();
        metricsVBox.AddChild(followersHBox);

        var followersIcon = new Label { Text = "♥" };
        followersIcon.AddThemeFontSizeOverride("font_size", 10);
        followersIcon.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.5f));
        followersHBox.AddChild(followersIcon);

        _followersCountLabel = new Label
        {
            Text = "312 followers",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _followersCountLabel.AddThemeFontSizeOverride("font_size", 10);
        _followersCountLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.6f, 0.7f));
        followersHBox.AddChild(_followersCountLabel);

        // Ratings gauge (HYPE meter)
        var ratingsHBox = new HBoxContainer();
        metricsVBox.AddChild(ratingsHBox);

        _ratingsLabel = new Label { Text = "HYPE" };
        _ratingsLabel.AddThemeFontSizeOverride("font_size", 9);
        _ratingsLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.6f, 0.2f));
        ratingsHBox.AddChild(_ratingsLabel);

        _ratingsGauge = new ProgressBar
        {
            CustomMinimumSize = new Vector2(150, 12),
            Value = 30,
            MaxValue = 100,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        // Style the progress bar
        var gaugeBg = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.15f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        };
        var gaugeFill = new StyleBoxFlat
        {
            BgColor = new Color(0.9f, 0.5f, 0.1f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        };
        _ratingsGauge.AddThemeStyleboxOverride("background", gaugeBg);
        _ratingsGauge.AddThemeStyleboxOverride("fill", gaugeFill);
        ratingsHBox.AddChild(_ratingsGauge);

        // Position metrics panel
        _metricsPanel.Position = new Vector2(Margin, PanelHeight + Margin + 5);
    }

    private void PositionPanel(bool animate)
    {
        if (_mainPanel == null) return;

        // Position in TOP-LEFT corner
        Vector2 targetPos;
        Vector2 targetSize;

        if (_isMinimized)
        {
            targetPos = new Vector2(Margin, Margin);
            targetSize = new Vector2(MinimizedWidth, MinimizedHeight);
        }
        else
        {
            targetPos = new Vector2(Margin, Margin);
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

        // Update metrics panel position
        if (_metricsPanel != null)
        {
            _metricsPanel.Visible = !_isMinimized;
            _metricsPanel.Position = new Vector2(Margin, PanelHeight + Margin + 5);
        }

        // Update notification columns visibility
        if (_lootColumn != null)
            _lootColumn.Visible = !_isMinimized;
        if (_achievementColumn != null)
            _achievementColumn.Visible = !_isMinimized;
    }

    private void UpdateNotifications(float dt)
    {
        // Update loot notifications
        UpdateNotificationList(_lootNotifications, _lootColumn, dt);

        // Update achievement notifications
        UpdateNotificationList(_achievementNotifications, _achievementColumn, dt);
    }

    private void UpdateNotificationList(List<NotificationItem> notifications, VBoxContainer? column, float dt)
    {
        if (column == null) return;

        for (int i = notifications.Count - 1; i >= 0; i--)
        {
            var notif = notifications[i];
            notif.TimeRemaining -= dt;

            // Fade out at the end
            if (notif.TimeRemaining < NotificationFadeTime)
            {
                notif.Alpha = notif.TimeRemaining / NotificationFadeTime;
                if (notif.Label != null)
                    notif.Label.Modulate = new Color(1, 1, 1, notif.Alpha);
            }

            // Remove expired notifications
            if (notif.TimeRemaining <= 0)
            {
                notif.Label?.QueueFree();
                notifications.RemoveAt(i);
            }
        }
    }

    private void UpdateViewerMetrics(float dt)
    {
        // Decay rating when not in combat
        _ratingDecayTimer += dt;
        if (_ratingDecayTimer > 2f && !_isInCombat)
        {
            _targetRating = Mathf.Max(0.1f, _targetRating - dt * 0.05f);
        }

        // Lerp rating
        _currentRating = Mathf.Lerp(_currentRating, _targetRating, dt * 3f);

        // Views always increase (faster during exciting moments)
        float viewBoost = 1f + _currentRating * 4f; // 1-5x multiplier based on hype
        _viewsAccumulator += _viewsPerSecond * viewBoost * dt;
        if (_viewsAccumulator >= 1f)
        {
            int newViews = (int)_viewsAccumulator;
            _totalViews += newViews;
            _viewsAccumulator -= newViews;
        }

        // Followers fluctuate - occasionally gain or lose (but never exceed views)
        _followerTimer += dt;
        if (_followerTimer >= _nextFollowerChange)
        {
            _followerTimer = 0f;
            _nextFollowerChange = 0.3f + GD.Randf() * 2f; // 0.3 to 2.3 seconds

            // Higher hype = more likely to gain followers
            float gainChance = 0.4f + _currentRating * 0.4f; // 40-80% chance to gain
            if (GD.Randf() < gainChance)
            {
                // Gain 1-5 followers
                int gain = 1 + (int)(GD.Randf() * 4f * (1f + _currentRating));
                _followers += gain;
            }
            else
            {
                // Lose 1-3 followers
                int loss = 1 + (int)(GD.Randf() * 2f);
                _followers = Mathf.Max(1, _followers - loss);
            }

            // Followers can never exceed views
            _followers = Mathf.Min(_followers, (int)(_totalViews * 0.8f));
        }

        // Update UI
        if (_ratingsGauge != null)
        {
            _ratingsGauge.Value = _currentRating * 100f;

            // Color based on rating level
            var fillStyle = _ratingsGauge.GetThemeStylebox("fill") as StyleBoxFlat;
            if (fillStyle != null)
            {
                Color gaugeColor;
                if (_currentRating > 0.7f)
                    gaugeColor = new Color(1f, 0.3f, 0.1f); // Hot red
                else if (_currentRating > 0.4f)
                    gaugeColor = new Color(0.9f, 0.6f, 0.1f); // Orange
                else
                    gaugeColor = new Color(0.3f, 0.5f, 0.7f); // Cool blue

                fillStyle.BgColor = gaugeColor;
            }
        }

        if (_viewsCountLabel != null)
        {
            _viewsCountLabel.Text = $"{FormatCount(_totalViews)} views";
        }

        if (_followersCountLabel != null)
        {
            _followersCountLabel.Text = $"{FormatCount(_followers)} followers";
        }
    }

    private string FormatCount(long count)
    {
        if (count >= 1000000)
            return $"{count / 1000000f:F1}M";
        if (count >= 1000)
            return $"{count / 1000f:F1}K";
        return count.ToString("N0");
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

        _textTween?.Kill();
        _commentLabel.Text = "";
        _commentLabel.VisibleCharacters = 0;

        string styledText = $"[color=#aaccff]\"{text}\"[/color]";
        _commentLabel.Text = styledText;

        _textTween = CreateTween();
        _textTween.TweenProperty(_commentLabel, "visible_characters", text.Length + 20, text.Length * 0.03f);
    }

    /// <summary>
    /// Add a loot notification
    /// </summary>
    public void AddLootNotification(string text, Color? color = null)
    {
        AddNotification(_lootNotifications, _lootColumn, text, color ?? new Color(1f, 0.85f, 0.3f));
    }

    /// <summary>
    /// Add an achievement notification
    /// </summary>
    public void AddAchievementNotification(string text, Color? color = null)
    {
        AddNotification(_achievementNotifications, _achievementColumn, text, color ?? new Color(0.3f, 1f, 0.5f));
    }

    private void AddNotification(List<NotificationItem> list, VBoxContainer? column, string text, Color color)
    {
        if (column == null) return;

        // Limit notifications
        while (list.Count >= MaxNotificationsPerColumn)
        {
            var oldest = list[0];
            oldest.Label?.QueueFree();
            list.RemoveAt(0);
        }

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", color);
        column.AddChild(label);

        var notif = new NotificationItem
        {
            Text = text,
            Color = color,
            TimeRemaining = NotificationDuration,
            Alpha = 1f,
            Label = label,
        };
        list.Add(notif);
    }

    /// <summary>
    /// Spike the ratings (for exciting events like combat, kills, bosses)
    /// </summary>
    public void SpikeRatings(float amount)
    {
        _targetRating = Mathf.Clamp(_targetRating + amount, 0f, 1f);
        _ratingDecayTimer = 0f;

        // Big events can boost views per second temporarily
        _viewsPerSecond = Mathf.Min(20f, _viewsPerSecond + amount * 5f);

        // Exciting moments attract followers (but never exceed 80% of views)
        if (amount > 0.2f)
        {
            int bonusFollowers = (int)(amount * 10f * (1f + GD.Randf()));
            _followers += bonusFollowers;
            _followers = Mathf.Min(_followers, (int)(_totalViews * 0.8f));
        }
    }

    /// <summary>
    /// Set combat state for rating decay
    /// </summary>
    public void SetCombatState(bool inCombat)
    {
        _isInCombat = inCombat;
        if (inCombat)
            _ratingDecayTimer = 0f;
    }

    /// <summary>
    /// Set muted state
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

        if (_avatarContainer != null)
            _avatarContainer.Visible = !minimized;
        if (_commentLabel != null)
            _commentLabel.Visible = !minimized;
        if (_buttonBar != null)
            _buttonBar.Visible = !minimized;

        if (_minimizeButton != null)
            _minimizeButton.Text = minimized ? "+" : "_";

        PositionPanel(true);
    }

    /// <summary>
    /// Slide panel in from off-screen
    /// </summary>
    public void SlideIn()
    {
        if (_mainPanel == null) return;
        _mainPanel.Position = new Vector2(-PanelWidth - 50, Margin);
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
        if (what == NotificationWMSizeChanged)
        {
            PositionPanel(false);
        }
    }
}
