using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.Enemies;
using SafeRoom3D.Abilities;
using SafeRoom3D.Broadcaster;

namespace SafeRoom3D.UI;

/// <summary>
/// 3D HUD with health/mana bars, crosshair, 3 action bars, and minimap.
/// Designed for first-person perspective.
/// </summary>
public partial class HUD3D : Control
{
    // Singleton
    public static HUD3D? Instance { get; private set; }

    // UI Elements
    private ProgressBar? _healthBar;
    private ProgressBar? _manaBar;
    private ColorRect? _playerPortrait;
    private ProgressBar? _expBar;
    private Label? _healthLabel;
    private Label? _manaLabel;
    private Label? _expLabel;
    private Label? _timerLabel;
    private Control? _crosshair;
    private VBoxContainer? _actionBarContainer;
    private Control? _minimapContainer;
    private Label? _interactPrompt;
    private Label? _targetingPrompt;
    private Control? _compassContainer;
    private Control? _shortcutIcons;
    private HBoxContainer? _shortcutRow1;
    private HBoxContainer? _shortcutRow2;
    private Control? _overviewMap;
    private bool _isOverviewMapVisible;
    private float _overviewMapZoom = 1f; // Zoom level for overview map (0.5 to 3.0)

    // Action bars (3 rows of 10 slots each)
    private readonly List<ActionBarRow> _actionBars = new();

    // Minimap settings
    private const int MinimapSize = 180;
    private const float MinimapScale = 0.15f;
    private Vector2 _lastPlayerMinimapPos;
    private const float MinimapUpdateThreshold = 2f;

    // Fog of war - tracks explored tiles as (x,z) packed into long
    private readonly HashSet<long> _exploredTiles = new();
    private const float ExplorationRadius = 15f; // Tiles visible around player

    // Colors
    private readonly Color _healthColor = new(0.8f, 0.2f, 0.2f);
    private readonly Color _manaColor = new(0.2f, 0.4f, 0.9f);
    private readonly Color _crosshairColor = new(1f, 1f, 1f, 0.8f);
    private readonly Color _crosshairHitColor = new(1f, 0.3f, 0.3f);

    // State
    private float _crosshairHitFlash;
    private Panel? _minimapPanel;
    private PanelContainer? _actionBarPanel;
    private bool _leftPanelsPositioned;

    // Performance optimization - compass
    private float _lastCompassYaw;
    private const float CompassUpdateThreshold = 0.02f; // ~1 degree in radians

    // Performance optimization - minimap enemy cache
    private readonly List<Node3D> _cachedEnemies = new();
    private float _enemyCacheTimer;
    private const float EnemyCacheInterval = 0.5f; // Refresh enemy list every 0.5s

    // Performance optimization - viewport size caching
    private Vector2 _cachedViewportSize;

    // Combat Log / Chat window
    private PanelContainer? _chatWindow;
    private TabContainer? _chatTabs;
    private RichTextLabel? _combatLog;
    private RichTextLabel? _chatLog;
    private PanelContainer? _healthManaPanel;
    private const int MaxCombatLogLines = 100;

    // Loot notification system
    private VBoxContainer? _lootNotificationPanel;
    private readonly System.Collections.Generic.List<LootNotification> _lootNotifications = new();
    private const float LootNotificationDuration = 3f;
    private const int MaxLootNotifications = 6;

    // Achievement notification system
    private VBoxContainer? _achievementNotificationPanel;
    private readonly System.Collections.Generic.List<AchievementNotification> _achievementNotifications = new();
    private const float AchievementNotificationDuration = 4f;
    private const int MaxAchievementNotifications = 3;

    // Kill counter for achievements
    private readonly Dictionary<string, int> _killCounts = new();
    private const int KillsPerAchievement = 10;

    // Multi-kill tracking
    private int _recentKillCount;
    private float _multiKillTimer;
    private const float MultiKillWindow = 2f; // Seconds to count as multi-kill

    // Target Frame (shows current target to right of action bar)
    private PanelContainer? _targetFrame;
    private Label? _targetNameLabel;
    private ProgressBar? _targetHealthBar;
    private Label? _targetHealthLabel;
    private ProgressBar? _targetManaBar;
    private Label? _targetManaLabel;
    private ColorRect? _targetPortrait;
    private Node3D? _currentTarget;
    private readonly List<Node3D> _visibleEnemies = new();
    private int _targetIndex = -1;
    private float _targetUpdateTimer;
    private Node3D? _targetMarker3D;  // 3D marker that floats above targeted enemy

    public override void _Ready()
    {
        Instance = this;
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Ensure HUD processes even when game is paused (for map, menus)
        ProcessMode = ProcessModeEnum.Always;

        CreateHealthManaPanel();
        CreateChatWindow();
        CreateCrosshair();
        CreateCompass();
        CreateTimer();
        CreateActionBars();
        CreateTargetFrame();
        CreateShortcutIcons();
        CreateInteractPrompt();
        CreateTargetingPrompt();
        CreateMinimap();
        CreateLootNotificationPanel();
        CreateAchievementNotificationPanel();
        CreateOverviewMap();

        ConnectPlayerSignals();
        ConnectGameManagerSignals();
        ConnectAbilityManagerSignals();

        // Apply saved HUD positions (deferred to ensure elements are sized)
        CallDeferred(nameof(ApplySavedHUDPositions));

        GD.Print("[HUD3D] Ready with 3 action bars, compass, chat window, and shortcut icons");
    }

    /// <summary>
    /// Load and apply saved HUD positions from disk.
    /// </summary>
    private void ApplySavedHUDPositions()
    {
        ApplyPosition(_actionBarPanel, "HUD_ActionBarPanel");
        ApplyPosition(_healthManaPanel, "HUD_HealthManaPanel");
        ApplyPosition(_targetFrame, "HUD_TargetFrame");
        ApplyPosition(_chatWindow, "HUD_ChatWindow");
        ApplyPosition(_minimapPanel, "HUD_MinimapOuterFrame");
        ApplyPosition(_compassContainer, "HUD_Compass");
        ApplyPosition(_shortcutIcons, "HUD_ShortcutIcons");
    }

    private void ApplyPosition(Control? element, string elementName)
    {
        if (element == null) return;

        var savedPos = WindowPositionManager.GetPosition(elementName);
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            // Apply saved position, clamped to viewport
            var viewportSize = GetViewportRect().Size;
            var clampedPos = WindowPositionManager.ClampToViewport(savedPos, viewportSize, element.Size);
            element.Position = clampedPos;
            GD.Print($"[HUD3D] Applied saved position for {elementName}: {clampedPos}");
        }
    }

    private void CreateHealthManaPanel()
    {
        _healthManaPanel = new PanelContainer();
        var panel = _healthManaPanel;
        panel.Name = "HealthManaPanel";

        // Match target frame size for visual symmetry (280x130)
        panel.CustomMinimumSize = new Vector2(280, 130);
        panel.Size = new Vector2(280, 130);
        panel.Position = new Vector2(20, 500);  // Will be updated by position method
        panel.ZIndex = 100;

        // Style matching target frame (but with blue/player tint)
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.4f, 0.5f, 0.7f, 0.9f);  // Blue-ish border for player
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);

        // Portrait (Crawler icon - colorful square)
        _playerPortrait = new ColorRect();
        _playerPortrait.CustomMinimumSize = new Vector2(80, 80);
        _playerPortrait.Color = new Color(0.3f, 0.4f, 0.6f);  // Blue-ish for player
        hbox.AddChild(_playerPortrait);

        // Right side - name and bars
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Player name label
        var nameLabel = new Label { Text = "Crawler" };
        nameLabel.AddThemeFontSizeOverride("font_size", 16);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 1f));
        vbox.AddChild(nameLabel);

        // Health bar row
        var healthRow = new HBoxContainer();
        var healthIcon = new Label { Text = "♥", CustomMinimumSize = new Vector2(20, 0) };
        healthIcon.AddThemeColorOverride("font_color", _healthColor);
        healthIcon.AddThemeFontSizeOverride("font_size", 16);
        healthRow.AddChild(healthIcon);

        _healthBar = new ProgressBar();
        _healthBar.CustomMinimumSize = new Vector2(100, 20);
        _healthBar.MaxValue = 100;
        _healthBar.Value = 100;
        _healthBar.ShowPercentage = false;
        _healthBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var healthBgStyle = new StyleBoxFlat();
        healthBgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f);
        _healthBar.AddThemeStyleboxOverride("background", healthBgStyle);

        var healthFill = new StyleBoxFlat();
        healthFill.BgColor = _healthColor;
        _healthBar.AddThemeStyleboxOverride("fill", healthFill);
        healthRow.AddChild(_healthBar);

        _healthLabel = new Label { Text = "100/100" };
        _healthLabel.AddThemeFontSizeOverride("font_size", 12);
        _healthLabel.CustomMinimumSize = new Vector2(55, 0);
        healthRow.AddChild(_healthLabel);
        vbox.AddChild(healthRow);

        // Mana bar row
        var manaRow = new HBoxContainer();
        var manaIcon = new Label { Text = "✦", CustomMinimumSize = new Vector2(20, 0) };
        manaIcon.AddThemeColorOverride("font_color", _manaColor);
        manaIcon.AddThemeFontSizeOverride("font_size", 16);
        manaRow.AddChild(manaIcon);

        _manaBar = new ProgressBar();
        _manaBar.CustomMinimumSize = new Vector2(100, 18);
        _manaBar.MaxValue = 50;
        _manaBar.Value = 50;
        _manaBar.ShowPercentage = false;
        _manaBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var manaBgStyle = new StyleBoxFlat();
        manaBgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f);
        _manaBar.AddThemeStyleboxOverride("background", manaBgStyle);

        var manaFill = new StyleBoxFlat();
        manaFill.BgColor = _manaColor;
        _manaBar.AddThemeStyleboxOverride("fill", manaFill);
        manaRow.AddChild(_manaBar);

        _manaLabel = new Label { Text = "50/50" };
        _manaLabel.AddThemeFontSizeOverride("font_size", 12);
        _manaLabel.CustomMinimumSize = new Vector2(55, 0);
        manaRow.AddChild(_manaLabel);
        vbox.AddChild(manaRow);

        // Experience bar row
        var expRow = new HBoxContainer();
        var expIcon = new Label { Text = "★", CustomMinimumSize = new Vector2(20, 0) };
        expIcon.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
        expIcon.AddThemeFontSizeOverride("font_size", 16);
        expRow.AddChild(expIcon);

        _expBar = new ProgressBar();
        _expBar.CustomMinimumSize = new Vector2(100, 16);
        _expBar.MaxValue = 100;
        _expBar.Value = 35;
        _expBar.ShowPercentage = false;
        _expBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var expBgStyle = new StyleBoxFlat();
        expBgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f);
        _expBar.AddThemeStyleboxOverride("background", expBgStyle);

        var expFill = new StyleBoxFlat();
        expFill.BgColor = new Color(0.9f, 0.8f, 0.2f);
        _expBar.AddThemeStyleboxOverride("fill", expFill);
        expRow.AddChild(_expBar);

        _expLabel = new Label { Text = "Lv 1" };
        _expLabel.AddThemeFontSizeOverride("font_size", 12);
        _expLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
        _expLabel.CustomMinimumSize = new Vector2(55, 0);
        expRow.AddChild(_expLabel);
        vbox.AddChild(expRow);

        hbox.AddChild(vbox);
        panel.AddChild(hbox);
        AddChild(panel);

        GD.Print($"[HUD3D] Health/Mana panel created with portrait - size: {panel.Size}");
    }

    private void UpdateLeftPanelsPosition()
    {
        // Check if user has saved a custom position for chat window - if so, don't override
        var savedPos = WindowPositionManager.GetPosition("HUD_ChatWindow");
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            return; // User has a saved position, don't override it
        }

        var viewportSize = GetViewportRect().Size;

        // Position chat window above the health panel (which is now left of action bar)
        if (_chatWindow != null && _healthManaPanel != null)
        {
            // Chat window goes above health panel
            _chatWindow.Position = new Vector2(
                _healthManaPanel.Position.X,
                _healthManaPanel.Position.Y - _chatWindow.Size.Y - 10
            );
        }
        else if (_chatWindow != null)
        {
            // Fallback if health panel not positioned yet - position at bottom-left with 40px margin
            _chatWindow.Position = new Vector2(20, viewportSize.Y - 440);
        }
    }

    private void UpdateHealthPanelPosition()
    {
        if (_healthManaPanel == null) return;

        // Check if user has saved a custom position - if so, use it
        var savedPos = WindowPositionManager.GetPosition("HUD_HealthManaPanel");
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            return; // User has a saved position, don't override it
        }

        var viewportSize = GetViewportRect().Size;
        float healthPanelWidth = 280;
        float healthPanelHeight = 130;
        float gap = 20;  // Gap between health panel and action bar
        float actionBarWidth = 720;
        float actionBarHeight = 200;

        // Calculate action bar position
        float actionBarX = (viewportSize.X - actionBarWidth) / 2;
        float actionBarY = viewportSize.Y - actionBarHeight - 40;

        // Position health panel to the LEFT of action bar, bottom aligned
        float healthX = actionBarX - healthPanelWidth - gap;
        float healthY = actionBarY + (actionBarHeight - healthPanelHeight) / 2;

        // Clamp to viewport bounds
        healthX = Mathf.Max(healthX, 10);
        healthY = Mathf.Clamp(healthY, 10, viewportSize.Y - healthPanelHeight - 10);

        _healthManaPanel.Position = new Vector2(healthX, healthY);
    }

    private void CreateChatWindow()
    {
        // Chat/Combat Log window on left side, above health panel
        _chatWindow = new PanelContainer();
        _chatWindow.Name = "ChatWindow";

        // Match info panel dimensions (320x400) for visual symmetry
        _chatWindow.CustomMinimumSize = new Vector2(320, 400);
        _chatWindow.Size = new Vector2(320, 400);
        _chatWindow.Position = new Vector2(20, 400);  // Initial position, updated in UpdateLeftPanelsPosition
        _chatWindow.ZIndex = 10;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        panelStyle.SetBorderWidthAll(1);
        panelStyle.BorderColor = new Color(0.3f, 0.3f, 0.4f, 0.7f);
        panelStyle.SetCornerRadiusAll(6);
        _chatWindow.AddThemeStyleboxOverride("panel", panelStyle);

        // Tab container for Chat and Combat Log tabs
        _chatTabs = new TabContainer();
        _chatTabs.TabAlignment = TabBar.AlignmentMode.Left;

        // Combat Log tab (default)
        var combatLogContainer = new VBoxContainer();
        combatLogContainer.Name = "Combat Log";

        _combatLog = new RichTextLabel();
        _combatLog.BbcodeEnabled = true;
        _combatLog.ScrollFollowing = true;
        _combatLog.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _combatLog.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _combatLog.AddThemeFontSizeOverride("normal_font_size", 11);
        _combatLog.AddThemeColorOverride("default_color", new Color(0.8f, 0.8f, 0.85f));
        combatLogContainer.AddChild(_combatLog);
        _chatTabs.AddChild(combatLogContainer);

        // Chat tab (placeholder for future)
        var chatContainer = new VBoxContainer();
        chatContainer.Name = "Chat";

        _chatLog = new RichTextLabel();
        _chatLog.BbcodeEnabled = true;
        _chatLog.ScrollFollowing = true;
        _chatLog.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _chatLog.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _chatLog.AddThemeFontSizeOverride("normal_font_size", 11);
        _chatLog.Text = "[color=#888888]Chat coming soon...[/color]";
        chatContainer.AddChild(_chatLog);
        _chatTabs.AddChild(chatContainer);

        // Set Combat Log as default tab
        _chatTabs.CurrentTab = 0;

        _chatWindow.AddChild(_chatTabs);
        AddChild(_chatWindow);

        // Add initial message
        AddCombatLogMessage("Combat log started...", new Color(0.6f, 0.6f, 0.7f));

        GD.Print("[HUD3D] Chat window with Combat Log created");
    }

    /// <summary>
    /// Add a message to the combat log
    /// </summary>
    public void AddCombatLogMessage(string message, Color color)
    {
        if (_combatLog == null) return;

        string colorHex = color.ToHtml(false);
        _combatLog.AppendText($"[color=#{colorHex}]{message}[/color]\n");

        // Trim old messages if too many
        // RichTextLabel doesn't have easy line count, so we'll just trust scroll following
    }

    /// <summary>
    /// Log damage dealt by player to enemy
    /// </summary>
    public void LogPlayerDamage(string enemyName, int damage, string source = "attack")
    {
        string msg = $"You hit {enemyName} for [color=#ffcc00]{damage}[/color] damage ({source})";
        AddCombatLogMessage(msg, new Color(0.9f, 0.9f, 0.5f));
    }

    /// <summary>
    /// Log damage taken by player
    /// </summary>
    public void LogPlayerTakeDamage(string source, int damage)
    {
        string msg = $"{source} hits you for [color=#ff4444]{damage}[/color] damage!";
        AddCombatLogMessage(msg, new Color(1f, 0.5f, 0.5f));
    }

    /// <summary>
    /// Log spell cast
    /// </summary>
    public void LogSpellCast(string spellName)
    {
        string msg = $"You cast [color=#66ccff]{spellName}[/color]";
        AddCombatLogMessage(msg, new Color(0.5f, 0.8f, 1f));
    }

    /// <summary>
    /// Log spell damage/effect
    /// </summary>
    public void LogSpellDamage(string spellName, string target, int damage)
    {
        string msg = $"[color=#66ccff]{spellName}[/color] hits {target} for [color=#ffcc00]{damage}[/color] damage";
        AddCombatLogMessage(msg, new Color(0.6f, 0.8f, 1f));
    }

    /// <summary>
    /// Log enemy aggro
    /// </summary>
    public void LogEnemyAggro(string enemyName)
    {
        string msg = $"[color=#ff6666]{enemyName}[/color] notices you!";
        AddCombatLogMessage(msg, new Color(1f, 0.6f, 0.4f));
    }

    /// <summary>
    /// Log enemy death
    /// </summary>
    public void LogEnemyDeath(string enemyName, int expGained)
    {
        string msg = $"[color=#ff4444]{enemyName}[/color] defeated! +[color=#ffff00]{expGained}[/color] XP";
        AddCombatLogMessage(msg, new Color(0.4f, 1f, 0.4f));
    }

    /// <summary>
    /// Log loot pickup
    /// </summary>
    public void LogLoot(string itemName, int quantity = 1)
    {
        string qtyStr = quantity > 1 ? $" x{quantity}" : "";
        string msg = $"Looted [color=#ffcc00]{itemName}{qtyStr}[/color]";
        AddCombatLogMessage(msg, new Color(0.8f, 0.7f, 0.3f));
    }

    /// <summary>
    /// Log status effect applied
    /// </summary>
    public void LogStatusEffect(string target, string effectName, bool isPlayer = false)
    {
        string who = isPlayer ? "You are" : $"{target} is";
        string msg = $"{who} affected by [color=#cc88ff]{effectName}[/color]";
        AddCombatLogMessage(msg, new Color(0.8f, 0.5f, 1f));
    }

    /// <summary>
    /// Hide chat window (for map/editor overlays)
    /// </summary>
    public void HideChatWindow()
    {
        if (_chatWindow != null) _chatWindow.Visible = false;
        if (_healthManaPanel != null) _healthManaPanel.Visible = false;
    }

    /// <summary>
    /// Show chat window
    /// </summary>
    public void ShowChatWindow()
    {
        if (_chatWindow != null) _chatWindow.Visible = true;
        if (_healthManaPanel != null) _healthManaPanel.Visible = true;
    }

    private void CreateLootNotificationPanel()
    {
        // Loot notifications are now handled by BroadcasterUI columns
        // We keep a hidden panel for backwards compatibility (some systems may reference it)
        _lootNotificationPanel = new VBoxContainer();
        _lootNotificationPanel.Name = "LootNotifications";
        _lootNotificationPanel.Visible = false; // Hidden - BroadcasterUI handles display
        AddChild(_lootNotificationPanel);
        GD.Print("[HUD3D] Loot notifications forwarded to BroadcasterUI");
    }

    /// <summary>
    /// Show a floating loot notification when items are picked up
    /// </summary>
    public void ShowLootNotification(string itemName, int quantity, Color itemColor)
    {
        // Forward to BroadcasterUI if available
        var broadcasterUI = AIBroadcaster.Instance?.UI;
        if (broadcasterUI != null)
        {
            string qtyStr = quantity > 1 ? $" x{quantity}" : "";
            broadcasterUI.AddLootNotification($"{itemName}{qtyStr}", itemColor);
            return;
        }

        // Fallback to internal display if BroadcasterUI not available
        if (_lootNotificationPanel == null) return;
        _lootNotificationPanel.Visible = true;

        // Remove oldest notifications if at max
        while (_lootNotifications.Count >= MaxLootNotifications)
        {
            var oldest = _lootNotifications[0];
            oldest.Container?.QueueFree();
            _lootNotifications.RemoveAt(0);
        }

        // Check if we can stack with existing notification
        foreach (var existing in _lootNotifications)
        {
            if (existing.ItemName == itemName)
            {
                existing.Quantity += quantity;
                existing.TimeRemaining = LootNotificationDuration;
                existing.UpdateLabel();
                return;
            }
        }

        // Create new notification
        var notification = new LootNotification
        {
            ItemName = itemName,
            Quantity = quantity,
            ItemColor = itemColor,
            TimeRemaining = LootNotificationDuration
        };

        // Create visual
        var container = new PanelContainer();
        container.CustomMinimumSize = new Vector2(240, 28);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.08f, 0.12f, 0.85f);
        style.BorderColor = new Color(0.4f, 0.35f, 0.25f, 0.7f);
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(4);
        container.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        container.AddChild(hbox);

        // Loot icon (simple colored square)
        var icon = new ColorRect();
        icon.CustomMinimumSize = new Vector2(18, 18);
        icon.Color = itemColor;
        hbox.AddChild(icon);

        // Item name and quantity
        var label = new Label();
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        hbox.AddChild(label);

        notification.Container = container;
        notification.Label = label;
        notification.UpdateLabel();

        _lootNotificationPanel.AddChild(container);
        _lootNotifications.Add(notification);

        // Start with slight transparency and animate in
        container.Modulate = new Color(1, 1, 1, 0);
        var tween = container.CreateTween();
        tween.TweenProperty(container, "modulate", new Color(1, 1, 1, 1), 0.15f);
    }

    private void UpdateLootNotifications(float dt)
    {
        for (int i = _lootNotifications.Count - 1; i >= 0; i--)
        {
            var notification = _lootNotifications[i];
            notification.TimeRemaining -= dt;

            if (notification.TimeRemaining <= 0)
            {
                // Fade out and remove
                if (notification.Container != null)
                {
                    var tween = notification.Container.CreateTween();
                    tween.TweenProperty(notification.Container, "modulate:a", 0f, 0.3f);
                    tween.TweenCallback(Callable.From(() => notification.Container.QueueFree()));
                }
                _lootNotifications.RemoveAt(i);
            }
            else if (notification.TimeRemaining < 0.5f && notification.Container != null)
            {
                // Start fading when almost expired
                notification.Container.Modulate = new Color(1, 1, 1, notification.TimeRemaining * 2f);
            }
        }
    }

    private void CreateAchievementNotificationPanel()
    {
        // Achievement notifications are now handled by BroadcasterUI columns
        // We keep a hidden panel for backwards compatibility
        _achievementNotificationPanel = new VBoxContainer();
        _achievementNotificationPanel.Name = "AchievementNotifications";
        _achievementNotificationPanel.Visible = false; // Hidden - BroadcasterUI handles display
        AddChild(_achievementNotificationPanel);
        GD.Print("[HUD3D] Achievement notifications forwarded to BroadcasterUI");
    }

    /// <summary>
    /// Show an achievement notification with comic-style flair
    /// </summary>
    public void ShowAchievementNotification(string title, string description, Color accentColor)
    {
        // Forward to BroadcasterUI if available
        var broadcasterUI = AIBroadcaster.Instance?.UI;
        if (broadcasterUI != null)
        {
            broadcasterUI.AddAchievementNotification($"★ {title} ★ {description}", accentColor);
            SoundManager3D.Instance?.PlayAchievementSound();
            return;
        }

        // Fallback to internal display if BroadcasterUI not available
        if (_achievementNotificationPanel == null) return;
        _achievementNotificationPanel.Visible = true;

        // Remove oldest if at max
        while (_achievementNotifications.Count >= MaxAchievementNotifications)
        {
            var oldest = _achievementNotifications[0];
            oldest.Container?.QueueFree();
            _achievementNotifications.RemoveAt(0);
        }

        var notification = new AchievementNotification
        {
            Title = title,
            Description = description,
            AccentColor = accentColor,
            TimeRemaining = AchievementNotificationDuration
        };

        // Create visual container with comic-style flair
        var container = new PanelContainer();
        container.CustomMinimumSize = new Vector2(280, 50);

        // Background with accent border (comic style)
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.06f, 0.1f, 0.95f);
        style.BorderColor = accentColor;
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(6);
        style.BorderWidthLeft = 6; // Thick left accent bar
        style.SetContentMarginAll(8);
        container.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        container.AddChild(vbox);

        // Title with star/flair
        var titleLabel = new Label();
        titleLabel.Text = $"★ {title} ★";
        titleLabel.AddThemeFontSizeOverride("font_size", 16);
        titleLabel.AddThemeColorOverride("font_color", accentColor);
        vbox.AddChild(titleLabel);

        // Description
        var descLabel = new Label();
        descLabel.Text = description;
        descLabel.AddThemeFontSizeOverride("font_size", 12);
        descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
        vbox.AddChild(descLabel);

        notification.Container = container;
        _achievementNotificationPanel.AddChild(container);
        _achievementNotifications.Add(notification);

        // Animate in with scale and slide
        container.Scale = new Vector2(0.5f, 0.5f);
        container.Modulate = new Color(1, 1, 1, 0);
        container.Position = new Vector2(-50, 0);

        var tween = container.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(container, "scale", Vector2.One, 0.3f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(container, "modulate:a", 1f, 0.2f);
        tween.TweenProperty(container, "position:x", 0f, 0.3f).SetEase(Tween.EaseType.Out);

        // Play achievement sound
        SoundManager3D.Instance?.PlayAchievementSound();
    }

    private void UpdateAchievementNotifications(float dt)
    {
        for (int i = _achievementNotifications.Count - 1; i >= 0; i--)
        {
            var notification = _achievementNotifications[i];
            notification.TimeRemaining -= dt;

            if (notification.TimeRemaining <= 0)
            {
                if (notification.Container != null)
                {
                    var tween = notification.Container.CreateTween();
                    tween.SetParallel(true);
                    tween.TweenProperty(notification.Container, "modulate:a", 0f, 0.4f);
                    tween.TweenProperty(notification.Container, "position:x", -100f, 0.4f);
                    tween.TweenCallback(Callable.From(() => notification.Container?.QueueFree()));
                }
                _achievementNotifications.RemoveAt(i);
            }
        }

        // Update multi-kill timer
        if (_multiKillTimer > 0)
        {
            _multiKillTimer -= dt;
            if (_multiKillTimer <= 0)
            {
                // Check for multi-kill achievement
                if (_recentKillCount >= 5)
                {
                    SoundManager3D.Instance?.PlayMultiKillSound();
                    ShowAchievementNotification(
                        "MULTI-KILL!",
                        $"Slayed {_recentKillCount} monsters at once!",
                        new Color(1f, 0.2f, 0.1f) // Red
                    );
                }
                _recentKillCount = 0;
            }
        }
    }

    /// <summary>
    /// Register a monster kill for achievement tracking
    /// </summary>
    public void RegisterKill(string monsterType, bool isBoss)
    {
        string normalizedType = monsterType.ToLower();

        // Increment kill count
        if (!_killCounts.ContainsKey(normalizedType))
            _killCounts[normalizedType] = 0;
        _killCounts[normalizedType]++;

        int killCount = _killCounts[normalizedType];

        // Check for milestone (every 10 kills)
        if (killCount % KillsPerAchievement == 0)
        {
            string displayName = FormatMonsterNameForDisplay(normalizedType);
            ShowAchievementNotification(
                $"{displayName} Slayer",
                $"Killed {killCount} {displayName}s!",
                new Color(1f, 0.8f, 0.2f) // Gold
            );
        }

        // Track multi-kill
        _recentKillCount++;
        _multiKillTimer = MultiKillWindow;

        // Boss kill sound
        if (isBoss)
        {
            SoundManager3D.Instance?.PlayBossKillSound();
        }

        GD.Print($"[HUD3D] Kill registered: {monsterType} (total: {killCount}, recent: {_recentKillCount})");
    }

    private static string FormatMonsterNameForDisplay(string type)
    {
        // Capitalize first letter of each word
        var words = type.Replace("_", " ").Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
        }
        return string.Join(" ", words);
    }

    private void CreateCrosshair()
    {
        _crosshair = new Control();
        _crosshair.SetAnchorsPreset(LayoutPreset.Center);
        _crosshair.CustomMinimumSize = new Vector2(32, 32);
        _crosshair.Position = new Vector2(-16, -16);

        _crosshair.Draw += () =>
        {
            if (_crosshair == null) return;

            Color color = _crosshairHitFlash > 0 ? _crosshairHitColor : _crosshairColor;
            float size = 16;
            float gap = 4;
            float thickness = 2;

            _crosshair.DrawRect(new Rect2(-size, -thickness / 2, size - gap, thickness), color);
            _crosshair.DrawRect(new Rect2(gap, -thickness / 2, size - gap, thickness), color);
            _crosshair.DrawRect(new Rect2(-thickness / 2, -size, thickness, size - gap), color);
            _crosshair.DrawRect(new Rect2(-thickness / 2, gap, thickness, size - gap), color);
            _crosshair.DrawCircle(Vector2.Zero, 2, color);
        };

        AddChild(_crosshair);
    }

    private void CreateTimer()
    {
        // Timer disabled for now - may return in future
        _timerLabel = new Label();
        _timerLabel.SetAnchorsPreset(LayoutPreset.CenterTop);
        _timerLabel.Position = new Vector2(-50, 20);
        _timerLabel.CustomMinimumSize = new Vector2(100, 30);
        _timerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _timerLabel.AddThemeFontSizeOverride("font_size", 28);
        _timerLabel.Text = "";
        _timerLabel.Visible = false; // Hide timer for now

        AddChild(_timerLabel);
    }

    private void CreateActionBars()
    {
        // Panel container with very visible background
        var actionBarPanel = new PanelContainer();
        actionBarPanel.Name = "ActionBarPanel";

        // Size: 10 slots * 56px + 9 gaps * 6px + modifier label 60px + padding = ~720px wide
        // Height: 3 rows * 56px + 2 gaps * 6px + padding = ~200px
        float barWidth = 720;
        float barHeight = 200;

        // Use fixed size - position will be updated in _Process
        actionBarPanel.CustomMinimumSize = new Vector2(barWidth, barHeight);
        actionBarPanel.Size = new Vector2(barWidth, barHeight);

        // Initial position (will be centered in _Process)
        actionBarPanel.Position = new Vector2(600, 800);

        // VERY visible background - bright with high opacity
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.15f, 0.12f, 0.22f, 0.95f);
        panelStyle.BorderColor = new Color(0.6f, 0.5f, 0.7f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.SetCornerRadiusAll(10);
        panelStyle.SetContentMarginAll(10);
        actionBarPanel.AddThemeStyleboxOverride("panel", panelStyle);
        actionBarPanel.ProcessMode = ProcessModeEnum.Always;  // Process while paused for hover
        AddChild(actionBarPanel);

        // Store reference to update position
        _actionBarPanel = actionBarPanel;

        GD.Print($"[HUD3D] Action bar panel created - size: {barWidth}x{barHeight}");

        // VBox for the 3 rows
        _actionBarContainer = new VBoxContainer();
        _actionBarContainer.AddThemeConstantOverride("separation", 4);
        actionBarPanel.AddChild(_actionBarContainer);

        // Create 3 rows: 1-0, Shift+1-0, Alt+1-0
        string[] modifiers = { "", "Shift+", "Alt+" };
        for (int row = 0; row < 3; row++)
        {
            var barRow = CreateActionBarRow(row, modifiers[row]);
            _actionBars.Add(barRow);
            _actionBarContainer.AddChild(barRow.Container);
        }

        GD.Print($"[HUD3D] Action bars created at bottom center with visible panel");
    }

    private ActionBarRow CreateActionBarRow(int rowIndex, string modifierPrefix)
    {
        var row = new ActionBarRow();
        row.RowIndex = rowIndex;
        row.ModifierPrefix = modifierPrefix;

        row.Container = new HBoxContainer();
        row.Container.AddThemeConstantOverride("separation", 6);

        // Modifier label on left - more visible
        var modLabel = new Label();
        modLabel.Text = string.IsNullOrEmpty(modifierPrefix) ? "    " : modifierPrefix;
        modLabel.CustomMinimumSize = new Vector2(60, 0);
        modLabel.AddThemeFontSizeOverride("font_size", 14);
        modLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.7f));
        modLabel.VerticalAlignment = VerticalAlignment.Center;
        row.Container.AddChild(modLabel);

        // 10 slots
        for (int i = 0; i < 10; i++)
        {
            var slot = CreateActionSlot(i, rowIndex);
            row.Slots.Add(slot);
            row.Container.AddChild(slot.Container);
        }

        return row;
    }

    private ActionSlot CreateActionSlot(int slotIndex, int rowIndex)
    {
        var slot = new ActionSlot();
        slot.SlotIndex = slotIndex;
        slot.RowIndex = rowIndex;

        // Main container - larger slots for visibility
        slot.Container = new PanelContainer();
        slot.Container.CustomMinimumSize = new Vector2(56, 56);

        // Background style - brighter, more visible
        slot.Background = new StyleBoxFlat();
        slot.Background.BgColor = new Color(0.18f, 0.18f, 0.25f, 0.95f);
        slot.Background.BorderColor = new Color(0.45f, 0.45f, 0.5f);
        slot.Background.SetBorderWidthAll(2);
        slot.Background.SetCornerRadiusAll(5);
        slot.Container.AddThemeStyleboxOverride("panel", slot.Background);

        // Stack for layering
        var stack = new Control();
        stack.SetAnchorsPreset(LayoutPreset.FullRect);

        // Icon (centered)
        slot.Icon = new TextureRect();
        slot.Icon.SetAnchorsPreset(LayoutPreset.FullRect);
        slot.Icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        slot.Icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        stack.AddChild(slot.Icon);

        // Cooldown overlay (fills from bottom to top as it recharges)
        slot.CooldownOverlay = new ColorRect();
        slot.CooldownOverlay.Color = new Color(0f, 0f, 0f, 0.7f);
        slot.CooldownOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        slot.CooldownOverlay.Visible = false;
        stack.AddChild(slot.CooldownOverlay);

        // Cooldown timer label
        slot.CooldownLabel = new Label();
        slot.CooldownLabel.Text = "";
        slot.CooldownLabel.HorizontalAlignment = HorizontalAlignment.Center;
        slot.CooldownLabel.VerticalAlignment = VerticalAlignment.Center;
        slot.CooldownLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        slot.CooldownLabel.AddThemeFontSizeOverride("font_size", 14);
        slot.CooldownLabel.AddThemeColorOverride("font_color", Colors.White);
        slot.CooldownLabel.Visible = false;
        stack.AddChild(slot.CooldownLabel);

        // Key number (top-right corner) - larger and more visible
        int keyNum = slotIndex == 9 ? 0 : slotIndex + 1;
        slot.KeyLabel = new Label();
        slot.KeyLabel.Text = keyNum.ToString();
        slot.KeyLabel.SetAnchorsPreset(LayoutPreset.TopRight);
        slot.KeyLabel.Position = new Vector2(-14, 2);
        slot.KeyLabel.AddThemeFontSizeOverride("font_size", 12);
        slot.KeyLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.8f));
        stack.AddChild(slot.KeyLabel);

        // Activation flash overlay
        slot.ActivationFlash = new ColorRect();
        slot.ActivationFlash.Color = new Color(1f, 1f, 1f, 0f);
        slot.ActivationFlash.SetAnchorsPreset(LayoutPreset.FullRect);
        slot.ActivationFlash.MouseFilter = Control.MouseFilterEnum.Ignore;
        stack.AddChild(slot.ActivationFlash);

        slot.Container.AddChild(stack);

        // Enable mouse events for hover detection in inspect mode
        slot.Container.MouseFilter = Control.MouseFilterEnum.Stop;
        slot.Container.ProcessMode = ProcessModeEnum.Always;  // Process while paused
        int capturedRow = rowIndex;
        int capturedSlot = slotIndex;
        slot.Container.MouseEntered += () => OnActionSlotHovered(capturedRow, capturedSlot);
        slot.Container.MouseExited += () => OnActionSlotUnhovered();

        return slot;
    }

    /// <summary>
    /// Called when mouse hovers over an action slot - shows info in inspect mode.
    /// </summary>
    private void OnActionSlotHovered(int row, int slot)
    {
        GD.Print($"[HUD3D] Slot hovered: row={row}, slot={slot}");

        // Only show hover info during inspect mode
        if (InspectMode3D.Instance == null)
        {
            GD.Print("[HUD3D] InspectMode3D.Instance is null");
            return;
        }

        if (!InspectMode3D.Instance.IsActive)
        {
            GD.Print("[HUD3D] InspectMode not active, ignoring hover");
            return;
        }

        if (row < 0 || row >= _actionBars.Count || slot < 0 || slot >= _actionBars[row].Slots.Count)
        {
            GD.Print($"[HUD3D] Invalid slot indices");
            return;
        }

        var actionSlot = _actionBars[row].Slots[slot];
        GD.Print($"[HUD3D] Slot has ability={actionSlot.AbilityId}, consumable={actionSlot.ConsumableId}");

        // Show ability info if slot has an ability
        if (!string.IsNullOrEmpty(actionSlot.AbilityId))
        {
            GD.Print($"[HUD3D] Showing ability info for: {actionSlot.AbilityId}");
            InspectMode3D.Instance.ShowAbilityInfo(actionSlot.AbilityId);
        }
        // Show consumable info if slot has a consumable
        else if (!string.IsNullOrEmpty(actionSlot.ConsumableId))
        {
            GD.Print($"[HUD3D] Showing item info for: {actionSlot.ConsumableId}");
            InspectMode3D.Instance.ShowItemInfo(actionSlot.ConsumableId);
        }
        else
        {
            GD.Print("[HUD3D] Slot is empty");
        }
    }

    /// <summary>
    /// Called when mouse leaves an action slot.
    /// </summary>
    private void OnActionSlotUnhovered()
    {
        // Could clear info panel here, but let it stay so user can read it
    }

    /// <summary>
    /// Get action slot info at a screen position. Used by InspectMode for hover detection.
    /// Returns (row, slot, abilityId, consumableId) or null if no slot at position.
    /// </summary>
    public (int row, int slot, string? abilityId, string? consumableId)? GetSlotAtPosition(Vector2 screenPos)
    {
        if (_actionBarPanel == null) return null;

        // Check if position is within action bar panel
        var panelRect = new Rect2(_actionBarPanel.GlobalPosition, _actionBarPanel.Size);
        if (!panelRect.HasPoint(screenPos))
            return null;

        // Check each slot
        for (int row = 0; row < _actionBars.Count; row++)
        {
            for (int slotIdx = 0; slotIdx < _actionBars[row].Slots.Count; slotIdx++)
            {
                var slot = _actionBars[row].Slots[slotIdx];
                if (slot.Container == null) continue;

                var slotRect = new Rect2(slot.Container.GlobalPosition, slot.Container.Size);
                if (slotRect.HasPoint(screenPos))
                {
                    return (row, slotIdx, slot.AbilityId, slot.ConsumableId);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Create the target frame UI (positioned to right of action bar).
    /// Shows targeted enemy's portrait, name, health, and mana.
    /// </summary>
    private void CreateTargetFrame()
    {
        _targetFrame = new PanelContainer();
        _targetFrame.Name = "TargetFrame";

        // Match player health panel dimensions and style
        _targetFrame.CustomMinimumSize = new Vector2(280, 130);
        _targetFrame.Size = new Vector2(280, 130);
        _targetFrame.ZIndex = 100;

        // Initially hidden until target is selected
        _targetFrame.Visible = false;

        // Match player health panel style
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.7f, 0.4f, 0.4f, 0.9f);  // Red-ish border for enemy
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetContentMarginAll(10);
        _targetFrame.AddThemeStyleboxOverride("panel", panelStyle);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);

        // Portrait (colored square representing enemy type)
        _targetPortrait = new ColorRect();
        _targetPortrait.CustomMinimumSize = new Vector2(80, 80);
        _targetPortrait.Color = new Color(0.5f, 0.3f, 0.3f);  // Default enemy color
        hbox.AddChild(_targetPortrait);

        // Right side - name and bars
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Target name
        _targetNameLabel = new Label();
        _targetNameLabel.Text = "No Target";
        _targetNameLabel.AddThemeFontSizeOverride("font_size", 16);
        _targetNameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.8f));
        vbox.AddChild(_targetNameLabel);

        // Health bar row
        var healthRow = new HBoxContainer();
        var healthIcon = new Label { Text = "♥", CustomMinimumSize = new Vector2(20, 0) };
        healthIcon.AddThemeColorOverride("font_color", new Color(0.8f, 0.2f, 0.2f));
        healthIcon.AddThemeFontSizeOverride("font_size", 16);
        healthRow.AddChild(healthIcon);

        _targetHealthBar = new ProgressBar();
        _targetHealthBar.CustomMinimumSize = new Vector2(120, 20);
        _targetHealthBar.MaxValue = 100;
        _targetHealthBar.Value = 100;
        _targetHealthBar.ShowPercentage = false;
        _targetHealthBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var healthBgStyle = new StyleBoxFlat();
        healthBgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f);
        _targetHealthBar.AddThemeStyleboxOverride("background", healthBgStyle);

        var healthFillStyle = new StyleBoxFlat();
        healthFillStyle.BgColor = new Color(0.8f, 0.2f, 0.2f);
        _targetHealthBar.AddThemeStyleboxOverride("fill", healthFillStyle);

        healthRow.AddChild(_targetHealthBar);

        _targetHealthLabel = new Label { Text = "100/100" };
        _targetHealthLabel.AddThemeFontSizeOverride("font_size", 12);
        _targetHealthLabel.CustomMinimumSize = new Vector2(60, 0);
        healthRow.AddChild(_targetHealthLabel);

        vbox.AddChild(healthRow);

        // Mana bar row (for enemies that have mana, like shamans)
        var manaRow = new HBoxContainer();
        var manaIcon = new Label { Text = "✦", CustomMinimumSize = new Vector2(20, 0) };
        manaIcon.AddThemeColorOverride("font_color", new Color(0.2f, 0.4f, 0.9f));
        manaIcon.AddThemeFontSizeOverride("font_size", 16);
        manaRow.AddChild(manaIcon);

        _targetManaBar = new ProgressBar();
        _targetManaBar.CustomMinimumSize = new Vector2(120, 16);
        _targetManaBar.MaxValue = 100;
        _targetManaBar.Value = 0;
        _targetManaBar.ShowPercentage = false;
        _targetManaBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _targetManaBar.Visible = false;  // Hidden by default, shown for caster enemies

        var manaBgStyle = new StyleBoxFlat();
        manaBgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f);
        _targetManaBar.AddThemeStyleboxOverride("background", manaBgStyle);

        var manaFillStyle = new StyleBoxFlat();
        manaFillStyle.BgColor = new Color(0.2f, 0.4f, 0.9f);
        _targetManaBar.AddThemeStyleboxOverride("fill", manaFillStyle);

        manaRow.AddChild(_targetManaBar);

        _targetManaLabel = new Label { Text = "" };
        _targetManaLabel.AddThemeFontSizeOverride("font_size", 12);
        _targetManaLabel.CustomMinimumSize = new Vector2(60, 0);
        _targetManaLabel.Visible = false;
        manaRow.AddChild(_targetManaLabel);

        vbox.AddChild(manaRow);

        hbox.AddChild(vbox);
        _targetFrame.AddChild(hbox);
        AddChild(_targetFrame);

        // Create 3D target marker
        CreateTargetMarker3D();

        GD.Print("[HUD3D] Target frame created");
    }

    /// <summary>
    /// Create a 3D marker that floats above targeted enemies.
    /// </summary>
    private void CreateTargetMarker3D()
    {
        _targetMarker3D = new Node3D();
        _targetMarker3D.Name = "TargetMarker3D";

        // Create a downward-pointing arrow/chevron
        var arrowMesh = new MeshInstance3D();
        var coneMesh = new CylinderMesh();
        coneMesh.TopRadius = 0f;
        coneMesh.BottomRadius = 0.3f;
        coneMesh.Height = 0.5f;
        arrowMesh.Mesh = coneMesh;
        arrowMesh.Rotation = new Vector3(Mathf.Pi, 0, 0);  // Point downward

        var arrowMat = new StandardMaterial3D();
        arrowMat.AlbedoColor = new Color(1f, 0.8f, 0.2f);  // Gold/yellow
        arrowMat.EmissionEnabled = true;
        arrowMat.Emission = new Color(1f, 0.6f, 0.1f);
        arrowMat.EmissionEnergyMultiplier = 2f;
        arrowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        arrowMesh.MaterialOverride = arrowMat;

        _targetMarker3D.AddChild(arrowMesh);

        // Initially hidden
        _targetMarker3D.Visible = false;

        // Add to scene root immediately (like InspectMode3D does)
        // Don't use CallDeferred as it can cause timing issues
        GD.Print("[HUD3D] Creating 3D target marker");
        GetTree().Root.AddChild(_targetMarker3D);
    }

    /// <summary>
    /// Set the current target (called from FPSController on R key).
    /// </summary>
    public void SetTarget(Node3D? target)
    {
        _currentTarget = target;

        if (target == null)
        {
            GD.Print("[HUD3D] SetTarget: Clearing target");
            if (_targetFrame != null) _targetFrame.Visible = false;
            if (_targetMarker3D != null) _targetMarker3D.Visible = false;
            _targetIndex = -1;
            return;
        }

        GD.Print($"[HUD3D] SetTarget: Setting target to {target.Name}");

        // Show and position the 3D marker
        if (_targetMarker3D != null)
        {
            _targetMarker3D.Visible = true;
            UpdateTargetMarkerPosition();
            GD.Print($"[HUD3D] 3D marker SHOWN at position: {_targetMarker3D.GlobalPosition}");
        }
        else
        {
            GD.PrintErr("[HUD3D] SetTarget: _targetMarker3D is NULL!");
        }

        // Show the target frame UI
        if (_targetFrame != null)
        {
            _targetFrame.Visible = true;
            // Ensure position is updated when showing
            UpdateTargetFramePosition();
            // Bring to front of UI
            _targetFrame.MoveToFront();
            GD.Print($"[HUD3D] Target frame SHOWN: visible={_targetFrame.Visible}, position={_targetFrame.Position}, size={_targetFrame.Size}, z_index={_targetFrame.ZIndex}");
        }
        else
        {
            GD.PrintErr("[HUD3D] SetTarget: _targetFrame is NULL!");
        }
        UpdateTargetFrame();
    }

    /// <summary>
    /// Update the 3D target marker position to float above the current target.
    /// </summary>
    private void UpdateTargetMarkerPosition()
    {
        if (_targetMarker3D == null || _currentTarget == null || !IsInstanceValid(_currentTarget))
            return;

        // Position above the target (offset Y by ~3 units)
        float heightOffset = 3f;
        _targetMarker3D.GlobalPosition = _currentTarget.GlobalPosition + new Vector3(0, heightOffset, 0);

        // Animate bobbing
        float bob = Mathf.Sin((float)Time.GetTicksMsec() / 200f) * 0.15f;
        _targetMarker3D.GlobalPosition += new Vector3(0, bob, 0);
    }

    /// <summary>
    /// Get the current target.
    /// </summary>
    public Node3D? GetCurrentTarget() => _currentTarget;

    /// <summary>
    /// Cycle to the next visible enemy target.
    /// </summary>
    public void CycleNextTarget()
    {
        GD.Print("[HUD3D] CycleNextTarget called");
        UpdateVisibleEnemies();

        GD.Print($"[HUD3D] Found {_visibleEnemies.Count} visible enemies");
        if (_visibleEnemies.Count == 0)
        {
            SetTarget(null);
            return;
        }

        _targetIndex = (_targetIndex + 1) % _visibleEnemies.Count;
        GD.Print($"[HUD3D] Setting target index to {_targetIndex}");
        SetTarget(_visibleEnemies[_targetIndex]);
    }

    /// <summary>
    /// Cycle to the previous visible enemy target.
    /// </summary>
    public void CyclePreviousTarget()
    {
        UpdateVisibleEnemies();

        if (_visibleEnemies.Count == 0)
        {
            SetTarget(null);
            return;
        }

        _targetIndex = _targetIndex <= 0 ? _visibleEnemies.Count - 1 : _targetIndex - 1;
        SetTarget(_visibleEnemies[_targetIndex]);
    }

    /// <summary>
    /// Clear the current target.
    /// </summary>
    public void ClearTarget()
    {
        SetTarget(null);
    }

    /// <summary>
    /// Update list of visible enemies for targeting.
    /// </summary>
    private void UpdateVisibleEnemies()
    {
        _visibleEnemies.Clear();

        var player = FPSController.Instance;
        if (player == null) return;

        var camera = player.GetViewport()?.GetCamera3D();
        if (camera == null) return;

        var enemies = GetTree().GetNodesInGroup("Enemies");
        var playerPos = player.GlobalPosition;

        foreach (var node in enemies)
        {
            if (node is not Node3D enemy3D) continue;
            if (!IsInstanceValid(enemy3D)) continue;

            // Check if alive (has CurrentHealth > 0)
            if (enemy3D.HasMethod("get_CurrentHealth"))
            {
                var health = (int)enemy3D.Call("get_CurrentHealth");
                if (health <= 0) continue;
            }

            // Check distance (within 60m)
            float dist = playerPos.DistanceTo(enemy3D.GlobalPosition);
            if (dist > 60f) continue;

            // Check if on screen
            if (!camera.IsPositionBehind(enemy3D.GlobalPosition))
            {
                var screenPos = camera.UnprojectPosition(enemy3D.GlobalPosition);
                var viewportSize = GetViewportRect().Size;
                if (screenPos.X >= 0 && screenPos.X <= viewportSize.X &&
                    screenPos.Y >= 0 && screenPos.Y <= viewportSize.Y)
                {
                    _visibleEnemies.Add(enemy3D);
                }
            }
        }

        // Sort by distance
        _visibleEnemies.Sort((a, b) =>
            playerPos.DistanceTo(a.GlobalPosition).CompareTo(playerPos.DistanceTo(b.GlobalPosition)));

        // Validate current target index
        if (_currentTarget != null && _visibleEnemies.Contains(_currentTarget))
        {
            _targetIndex = _visibleEnemies.IndexOf(_currentTarget);
        }
        else if (_visibleEnemies.Count > 0 && _targetIndex >= _visibleEnemies.Count)
        {
            _targetIndex = 0;
        }
    }

    /// <summary>
    /// Update the target frame UI with current target's stats.
    /// </summary>
    private void UpdateTargetFrame()
    {
        if (_currentTarget == null || !IsInstanceValid(_currentTarget))
        {
            GD.Print("[HUD3D] UpdateTargetFrame: Target invalid, clearing");
            SetTarget(null);
            return;
        }

        // Get target info by casting to actual types (like InspectMode3D does)
        string name = "Unknown";
        int currentHealth = 0;
        int maxHealth = 100;
        bool hasMana = false;
        int currentMana = 0;
        int maxMana = 0;

        if (_currentTarget is BasicEnemy3D enemy)
        {
            name = FormatMonsterName(enemy.MonsterType);
            currentHealth = enemy.CurrentHealth;
            maxHealth = enemy.MaxHealth;
        }
        else if (_currentTarget is BossEnemy3D boss)
        {
            name = boss.MonsterType;  // Boss names are already formatted
            currentHealth = boss.CurrentHealth;
            maxHealth = boss.MaxHealth;
        }
        else
        {
            GD.Print($"[HUD3D] UpdateTargetFrame: Unknown target type: {_currentTarget.GetType().Name}");
        }

        // Check if dead
        if (currentHealth <= 0)
        {
            GD.Print($"[HUD3D] UpdateTargetFrame: Target {name} is dead (health={currentHealth}), clearing");
            SetTarget(null);
            return;
        }

        // Update UI
        _targetNameLabel!.Text = name;
        _targetHealthBar!.MaxValue = maxHealth;
        _targetHealthBar.Value = currentHealth;
        _targetHealthLabel!.Text = $"{currentHealth}/{maxHealth}";

        // Update health bar color based on percentage
        float healthPercent = (float)currentHealth / maxHealth;
        var fillStyle = _targetHealthBar.GetThemeStylebox("fill") as StyleBoxFlat;
        if (fillStyle != null)
        {
            Color healthColor = healthPercent > 0.6f
                ? new Color(0.2f, 0.9f, 0.2f)    // Green
                : healthPercent > 0.3f
                ? new Color(0.9f, 0.9f, 0.2f)   // Yellow
                : new Color(0.9f, 0.2f, 0.2f);  // Red
            fillStyle.BgColor = healthColor;
        }

        // Mana (hide for most enemies)
        _targetManaBar!.Visible = hasMana;
        _targetManaLabel!.Visible = hasMana;

        // Update portrait color based on enemy type
        Color portraitColor = GetPortraitColorForEnemy(name);
        _targetPortrait!.Color = portraitColor;
    }

    /// <summary>
    /// Get portrait color based on enemy name.
    /// </summary>
    private Color GetPortraitColorForEnemy(string name)
    {
        string lower = name.ToLower();
        if (lower.Contains("goblin")) return new Color(0.45f, 0.55f, 0.35f);
        if (lower.Contains("skeleton")) return new Color(0.9f, 0.88f, 0.8f);
        if (lower.Contains("slime")) return new Color(0.3f, 0.7f, 0.3f);
        if (lower.Contains("spider")) return new Color(0.3f, 0.25f, 0.2f);
        if (lower.Contains("dragon")) return new Color(0.7f, 0.2f, 0.15f);
        if (lower.Contains("rat")) return new Color(0.35f, 0.3f, 0.28f);
        if (lower.Contains("crawler")) return new Color(0.6f, 0.15f, 0.15f);
        if (lower.Contains("shadow")) return new Color(0.1f, 0.1f, 0.15f);
        if (lower.Contains("golem")) return new Color(0.6f, 0.45f, 0.4f);
        if (lower.Contains("armor")) return new Color(0.4f, 0.4f, 0.45f);
        if (lower.Contains("drone")) return new Color(0.7f, 0.7f, 0.75f);
        if (lower.Contains("mimic")) return new Color(0.5f, 0.35f, 0.2f);
        if (lower.Contains("mushroom")) return new Color(0.6f, 0.4f, 0.5f);
        return new Color(0.5f, 0.3f, 0.3f);  // Default enemy red
    }

    /// <summary>
    /// Format monster type name for display.
    /// </summary>
    private string FormatMonsterName(string monsterType)
    {
        // Convert "crawler_killer" to "Crawler Killer"
        var words = monsterType.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }

    /// <summary>
    /// Update a slot with ability info.
    /// </summary>
    public void UpdateActionSlot(int row, int slot, string? abilityId)
    {
        if (row < 0 || row >= _actionBars.Count) return;
        if (slot < 0 || slot >= _actionBars[row].Slots.Count) return;

        var actionSlot = _actionBars[row].Slots[slot];
        actionSlot.AbilityId = abilityId;

        if (actionSlot.Icon != null)
        {
            if (!string.IsNullOrEmpty(abilityId))
            {
                var icon = AbilityIcons.GetIcon(abilityId);
                actionSlot.Icon.Texture = icon;
                GD.Print($"[HUD3D] Slot [{row},{slot}] set icon for {abilityId}: {(icon != null ? "OK" : "NULL")}");
            }
            else
            {
                actionSlot.Icon.Texture = null;
            }
        }

        // Clear consumable if setting ability
        actionSlot.ConsumableId = null;
        if (actionSlot.StackLabel != null)
            actionSlot.StackLabel.Visible = false;

        // Update border color based on whether slot has ability - brighter for filled slots
        if (actionSlot.Background != null)
        {
            actionSlot.Background.BorderColor = string.IsNullOrEmpty(abilityId)
                ? new Color(0.35f, 0.35f, 0.4f)
                : new Color(0.5f, 0.65f, 0.5f);
        }
    }

    /// <summary>
    /// Update a slot with consumable item info.
    /// </summary>
    public void UpdateActionSlotConsumable(int row, int slot, string? consumableId)
    {
        if (row < 0 || row >= _actionBars.Count) return;
        if (slot < 0 || slot >= _actionBars[row].Slots.Count) return;

        var actionSlot = _actionBars[row].Slots[slot];

        // Clear ability if setting consumable
        actionSlot.AbilityId = null;
        actionSlot.ConsumableId = consumableId;

        if (actionSlot.Icon != null)
        {
            if (!string.IsNullOrEmpty(consumableId))
            {
                var icon = Items.ItemDatabase.GetIcon(consumableId);
                actionSlot.Icon.Texture = icon;
                GD.Print($"[HUD3D] Slot [{row},{slot}] set consumable icon for {consumableId}: {(icon != null ? "OK" : "NULL")}");
            }
            else
            {
                actionSlot.Icon.Texture = null;
            }
        }

        // Update stack count display
        UpdateConsumableStackCount(row, slot);

        // Update border color - different color for consumables (orange-ish)
        if (actionSlot.Background != null)
        {
            actionSlot.Background.BorderColor = string.IsNullOrEmpty(consumableId)
                ? new Color(0.35f, 0.35f, 0.4f)
                : new Color(0.7f, 0.55f, 0.3f);  // Orange-brown for consumables
        }
    }

    /// <summary>
    /// Update the stack count display for a consumable slot.
    /// </summary>
    public void UpdateConsumableStackCount(int row, int slot)
    {
        if (row < 0 || row >= _actionBars.Count) return;
        if (slot < 0 || slot >= _actionBars[row].Slots.Count) return;

        var actionSlot = _actionBars[row].Slots[slot];
        if (string.IsNullOrEmpty(actionSlot.ConsumableId)) return;

        // Count how many of this consumable the player has
        int count = Items.Inventory3D.Instance?.CountItem(actionSlot.ConsumableId) ?? 0;

        // Create stack label if needed
        if (actionSlot.StackLabel == null && actionSlot.Container != null)
        {
            actionSlot.StackLabel = new Label();
            actionSlot.StackLabel.AddThemeFontSizeOverride("font_size", 11);
            actionSlot.StackLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
            actionSlot.StackLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
            actionSlot.StackLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            actionSlot.StackLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            actionSlot.StackLabel.HorizontalAlignment = HorizontalAlignment.Right;
            actionSlot.StackLabel.VerticalAlignment = VerticalAlignment.Bottom;
            actionSlot.StackLabel.Position = new Vector2(0, 38);
            actionSlot.StackLabel.Size = new Vector2(52, 16);
            actionSlot.Container.AddChild(actionSlot.StackLabel);
        }

        if (actionSlot.StackLabel != null)
        {
            actionSlot.StackLabel.Text = count.ToString();
            actionSlot.StackLabel.Visible = count > 0;

            // Gray out slot if no items remaining
            if (actionSlot.Icon != null)
            {
                actionSlot.Icon.Modulate = count > 0 ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
        }
    }

    /// <summary>
    /// Update all consumable stack counts on the hotbar.
    /// Call this after inventory changes.
    /// </summary>
    public void RefreshConsumableStacks()
    {
        for (int row = 0; row < _actionBars.Count; row++)
        {
            for (int slot = 0; slot < _actionBars[row].Slots.Count; slot++)
            {
                if (_actionBars[row].Slots[slot].HasConsumable)
                {
                    UpdateConsumableStackCount(row, slot);
                }
            }
        }
    }

    /// <summary>
    /// Find which action bar slot is at a given screen position.
    /// Returns (row, slot) or (-1, -1) if not on a slot.
    /// </summary>
    public (int row, int slot) FindActionBarSlotAt(Vector2 position)
    {
        // Get viewport size for calculation
        var viewportSize = GetViewportRect().Size;

        // Action bar dimensions (must match CreateActionBars)
        float barWidth = 720;
        float barHeight = 200;
        float barX = (viewportSize.X - barWidth) / 2;
        float barY = viewportSize.Y - barHeight - 40;

        // Check if position is within action bar bounds
        if (position.X < barX || position.X > barX + barWidth)
            return (-1, -1);
        if (position.Y < barY || position.Y > barY + barHeight)
            return (-1, -1);

        // Calculate relative position
        float relX = position.X - barX;
        float relY = position.Y - barY;

        // Slot dimensions
        float modifierWidth = 60;
        float slotWidth = 56;
        float slotGap = 6;
        float rowHeight = 56 + 4; // slot height + separation

        // Calculate row (0, 1, or 2)
        int row = (int)(relY / rowHeight);
        if (row < 0 || row >= 3) return (-1, -1);

        // Calculate slot within row
        float slotX = relX - modifierWidth - 10; // Account for modifier label and padding
        if (slotX < 0) return (-1, -1);

        int slot = (int)(slotX / (slotWidth + slotGap));
        if (slot < 0 || slot >= 10) return (-1, -1);

        return (row, slot);
    }

    /// <summary>
    /// Get the action slot at a given row and slot index.
    /// </summary>
    public ActionSlot? GetActionSlot(int row, int slot)
    {
        if (row < 0 || row >= _actionBars.Count) return null;
        if (slot < 0 || slot >= _actionBars[row].Slots.Count) return null;
        return _actionBars[row].Slots[slot];
    }

    /// <summary>
    /// Trigger activation flash on a slot.
    /// </summary>
    public void FlashSlot(int row, int slot)
    {
        if (row < 0 || row >= _actionBars.Count) return;
        if (slot < 0 || slot >= _actionBars[row].Slots.Count) return;

        var actionSlot = _actionBars[row].Slots[slot];
        actionSlot.FlashTimer = 0.2f;

        if (actionSlot.ActivationFlash != null)
        {
            actionSlot.ActivationFlash.Color = new Color(1f, 1f, 0.7f, 0.6f);
        }

        // Pulse border
        if (actionSlot.Background != null)
        {
            actionSlot.Background.BorderColor = new Color(1f, 0.9f, 0.5f);
            actionSlot.Background.SetBorderWidthAll(2);
        }
    }

    /// <summary>
    /// Update cooldown display for a slot.
    /// </summary>
    public void UpdateSlotCooldown(int row, int slot, float remaining, float total)
    {
        if (row < 0 || row >= _actionBars.Count) return;
        if (slot < 0 || slot >= _actionBars[row].Slots.Count) return;

        var actionSlot = _actionBars[row].Slots[slot];
        bool onCooldown = remaining > 0;

        actionSlot.CooldownRemaining = remaining;
        actionSlot.CooldownTotal = total;

        if (actionSlot.CooldownOverlay != null)
        {
            actionSlot.CooldownOverlay.Visible = onCooldown;
            if (onCooldown && total > 0)
            {
                // Adjust height to show progress (fills up as cooldown completes)
                float progress = 1f - (remaining / total);
                actionSlot.CooldownOverlay.AnchorTop = progress;
            }
        }

        if (actionSlot.CooldownLabel != null)
        {
            actionSlot.CooldownLabel.Visible = onCooldown;
            if (onCooldown)
            {
                actionSlot.CooldownLabel.Text = remaining >= 1f ? $"{remaining:F0}" : $"{remaining:F1}";
            }
        }

        // Border color
        if (actionSlot.Background != null && actionSlot.FlashTimer <= 0)
        {
            if (onCooldown)
            {
                actionSlot.Background.BorderColor = new Color(0.3f, 0.3f, 0.3f);
            }
            else if (!string.IsNullOrEmpty(actionSlot.AbilityId))
            {
                actionSlot.Background.BorderColor = new Color(0.5f, 0.7f, 0.5f); // Green when ready
            }
        }
    }

    /// <summary>
    /// Update cooldown for an ability by ID across all bars.
    /// </summary>
    public void UpdateAbilityCooldown(string abilityId, float remaining, float total)
    {
        for (int row = 0; row < _actionBars.Count; row++)
        {
            for (int slot = 0; slot < _actionBars[row].Slots.Count; slot++)
            {
                if (_actionBars[row].Slots[slot].AbilityId == abilityId)
                {
                    UpdateSlotCooldown(row, slot, remaining, total);
                }
            }
        }
    }

    /// <summary>
    /// Flash activation for an ability by ID.
    /// </summary>
    public void FlashAbilityActivation(string abilityId)
    {
        for (int row = 0; row < _actionBars.Count; row++)
        {
            for (int slot = 0; slot < _actionBars[row].Slots.Count; slot++)
            {
                if (_actionBars[row].Slots[slot].AbilityId == abilityId)
                {
                    FlashSlot(row, slot);
                }
            }
        }
    }

    private void CreateInteractPrompt()
    {
        _interactPrompt = new Label();
        _interactPrompt.SetAnchorsPreset(LayoutPreset.Center);
        _interactPrompt.Position = new Vector2(-100, 50);
        _interactPrompt.CustomMinimumSize = new Vector2(200, 30);
        _interactPrompt.HorizontalAlignment = HorizontalAlignment.Center;
        _interactPrompt.AddThemeFontSizeOverride("font_size", 18);
        _interactPrompt.Text = "Press [E] to interact";
        _interactPrompt.Visible = false;

        AddChild(_interactPrompt);
    }

    private void CreateTargetingPrompt()
    {
        _targetingPrompt = new Label();
        _targetingPrompt.SetAnchorsPreset(LayoutPreset.CenterTop);
        _targetingPrompt.Position = new Vector2(-150, 60);
        _targetingPrompt.CustomMinimumSize = new Vector2(300, 30);
        _targetingPrompt.HorizontalAlignment = HorizontalAlignment.Center;
        _targetingPrompt.AddThemeFontSizeOverride("font_size", 16);
        _targetingPrompt.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
        _targetingPrompt.Text = "Click to cast, Right-click to cancel";
        _targetingPrompt.Visible = false;

        AddChild(_targetingPrompt);
    }

    public void ShowTargetingPrompt(string abilityName)
    {
        if (_targetingPrompt != null)
        {
            _targetingPrompt.Text = $"[{abilityName}] Click to cast, Right-click to cancel";
            _targetingPrompt.Visible = true;
        }
    }

    public void HideTargetingPrompt()
    {
        if (_targetingPrompt != null)
        {
            _targetingPrompt.Visible = false;
        }
    }

    private void CreateMinimap()
    {
        // Outer decorative frame
        var outerFrame = new Panel();
        outerFrame.Name = "MinimapOuterFrame";
        outerFrame.Size = new Vector2(MinimapSize + 16, MinimapSize + 40);
        outerFrame.Position = new Vector2(GetViewportRect().Size.X - MinimapSize - 28, 12);

        var outerStyle = new StyleBoxFlat();
        outerStyle.BgColor = new Color(0.08f, 0.07f, 0.1f, 0.95f);
        outerStyle.BorderColor = new Color(0.45f, 0.35f, 0.25f); // Bronze/copper frame
        outerStyle.SetBorderWidthAll(3);
        outerStyle.SetCornerRadiusAll(8);
        outerFrame.AddThemeStyleboxOverride("panel", outerStyle);
        AddChild(outerFrame);

        // Map title label
        var titleLabel = new Label();
        titleLabel.Text = "MAP";
        titleLabel.Position = new Vector2(8, 4);
        titleLabel.AddThemeFontSizeOverride("font_size", 12);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 0.45f));
        outerFrame.AddChild(titleLabel);

        // Inner panel (actual map background)
        var panel = new Panel();
        panel.Name = "MinimapPanel";
        panel.Size = new Vector2(MinimapSize, MinimapSize);
        panel.Position = new Vector2(8, 28);

        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.03f, 0.025f, 0.04f, 0.98f); // Very dark parchment
        bgStyle.BorderColor = new Color(0.3f, 0.25f, 0.2f);
        bgStyle.SetBorderWidthAll(2);
        bgStyle.SetCornerRadiusAll(4);
        panel.AddThemeStyleboxOverride("panel", bgStyle);
        outerFrame.AddChild(panel);

        _minimapContainer = new Control();
        _minimapContainer.Name = "MinimapDrawArea";
        _minimapContainer.Position = Vector2.Zero;
        _minimapContainer.Size = new Vector2(MinimapSize, MinimapSize);
        _minimapContainer.ClipContents = true;
        _minimapContainer.Draw += DrawMinimap;
        panel.AddChild(_minimapContainer);

        _minimapPanel = outerFrame;
        CallDeferred(nameof(ForceMinimapRedraw));
    }

    private void ForceMinimapRedraw()
    {
        _minimapContainer?.QueueRedraw();
    }

    private void ForceOverviewMapRedraw()
    {
        _overviewMapDrawArea?.QueueRedraw();
        GD.Print($"[HUD3D] Overview map redraw - draw area size: {_overviewMapDrawArea?.Size}");
    }

    private void UpdateActionBarPosition()
    {
        if (_actionBarPanel == null) return;

        // Check if user has saved a custom position - if so, use it
        var savedPos = WindowPositionManager.GetPosition("HUD_ActionBarPanel");
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            return; // User has a saved position, don't override it
        }

        var viewportSize = GetViewportRect().Size;
        float barWidth = 720;
        float barHeight = 200;

        // Center horizontally, position at bottom with 40px margin
        _actionBarPanel.Position = new Vector2(
            (viewportSize.X - barWidth) / 2,
            viewportSize.Y - barHeight - 40
        );
    }

    private void UpdateTargetFramePosition()
    {
        if (_targetFrame == null)
        {
            GD.Print("[HUD3D] UpdateTargetFramePosition: Target frame is null!");
            return;
        }

        // Check if user has saved a custom position - if so, use it
        var savedPos = WindowPositionManager.GetPosition("HUD_TargetFrame");
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            // User has a saved position, don't override it
            return;
        }

        var viewportSize = GetViewportRect().Size;
        float targetFrameWidth = 280;
        float targetFrameHeight = 130;
        float gap = 20;  // Gap between action bar and target frame
        float actionBarWidth = 720;

        // Calculate position based on viewport and action bar layout
        float actionBarX;
        float actionBarY;
        float actionBarHeight = 200;

        if (_actionBarPanel != null && _actionBarPanel.Position.Y > 0)
        {
            // Use actual action bar position if valid
            actionBarX = _actionBarPanel.Position.X;
            actionBarY = _actionBarPanel.Position.Y;
            actionBarHeight = _actionBarPanel.Size.Y;
        }
        else
        {
            // Calculate expected position (same formula as UpdateActionBarPosition)
            actionBarX = (viewportSize.X - actionBarWidth) / 2;
            actionBarY = viewportSize.Y - actionBarHeight - 40;
        }

        // Position target frame to the right of action bar, vertically centered
        float targetX = actionBarX + actionBarWidth + gap;
        float targetY = actionBarY + (actionBarHeight - targetFrameHeight) / 2;

        // Clamp to viewport bounds
        targetX = Mathf.Min(targetX, viewportSize.X - targetFrameWidth - 10);
        targetY = Mathf.Clamp(targetY, 10, viewportSize.Y - targetFrameHeight - 10);

        _targetFrame.Position = new Vector2(targetX, targetY);

        GD.Print($"[HUD3D] Target frame positioned at ({targetX}, {targetY}) for viewport {viewportSize}");
    }

    private void UpdateMinimapPosition()
    {
        if (_minimapPanel != null)
        {
            // Check if user has saved a custom position - if so, use it
            var savedPos = WindowPositionManager.GetPosition("HUD_MinimapOuterFrame");
            if (savedPos != WindowPositionManager.CenterMarker)
            {
                return; // User has a saved position, don't override it
            }

            var viewportSize = GetViewportRect().Size;
            // Account for outer frame size
            _minimapPanel.Position = new Vector2(viewportSize.X - MinimapSize - 36, 12);
        }
    }

    private void UpdateShortcutIconsPosition()
    {
        if (_shortcutIcons == null) return;

        // Check if user has saved a custom position - if so, use it
        var savedPos = WindowPositionManager.GetPosition("HUD_ShortcutIcons");
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            return; // User has a saved position, don't override it
        }

        var viewportSize = GetViewportRect().Size;

        // Position to the LEFT of the health panel
        if (_healthManaPanel != null && _healthManaPanel.Position.X > 0)
        {
            float healthPanelLeft = _healthManaPanel.Position.X;
            _shortcutIcons.Position = new Vector2(
                healthPanelLeft - _shortcutIcons.Size.X - 15,
                _healthManaPanel.Position.Y + (_healthManaPanel.Size.Y - _shortcutIcons.Size.Y) / 2
            );
        }
        else
        {
            // Fallback - far left
            _shortcutIcons.Position = new Vector2(10, viewportSize.Y - 90);
        }
    }

    private void DrawMinimap()
    {
        if (_minimapContainer == null) return;

        var player = FPSController.Instance;
        if (player == null) return;

        var dungeon = GameManager3D.Instance?.GetDungeon();
        if (dungeon == null) return;

        Vector3 playerPos = player.GlobalPosition;
        float centerX = MinimapSize / 2f;
        float centerY = MinimapSize / 2f;

        // Update explored tiles around player
        UpdateExploredTiles(playerPos);

        var mapData = dungeon.GetMapData();
        if (mapData != null)
        {
            int tileSize = 3;
            float viewRange = MinimapSize / 2f / MinimapScale / tileSize;

            // Draw circular map area
            float radius = MinimapSize / 2f - 4;
            _minimapContainer.DrawCircle(new Vector2(centerX, centerY), radius, new Color(0.06f, 0.05f, 0.08f));

            for (int dx = (int)-viewRange; dx <= viewRange; dx++)
            {
                for (int dz = (int)-viewRange; dz <= viewRange; dz++)
                {
                    // TileSize = 1f, so world coords map directly to tile indices
                    int tileX = (int)playerPos.X + dx;
                    int tileZ = (int)playerPos.Z + dz;

                    if (tileX < 0 || tileX >= mapData.GetLength(0) ||
                        tileZ < 0 || tileZ >= mapData.GetLength(1))
                        continue;

                    float drawX = centerX + dx * tileSize;
                    float drawZ = centerY + dz * tileSize;

                    // Check if within circular bounds
                    float distFromCenter = new Vector2(drawX - centerX, drawZ - centerY).Length();
                    if (distFromCenter > radius) continue;

                    // Check fog of war
                    long tileKey = ((long)tileX << 32) | (uint)tileZ;
                    bool explored = _exploredTiles.Contains(tileKey);

                    if (!explored)
                    {
                        // Unexplored - draw dark fog
                        _minimapContainer.DrawRect(
                            new Rect2(drawX - tileSize / 2f, drawZ - tileSize / 2f, tileSize, tileSize),
                            new Color(0.03f, 0.025f, 0.04f)
                        );
                        continue;
                    }

                    int tile = mapData[tileX, tileZ];

                    // Distance-based visibility (brighter near player)
                    float distFromPlayer = Mathf.Sqrt(dx * dx + dz * dz);
                    float visibilityFade = 1f - Mathf.Clamp(distFromPlayer / viewRange, 0f, 0.5f);

                    // Colors - parchment/old map style
                    // Map values: 0 = void/wall, 1 = corridor, 2 = room floor
                    Color tileColor = tile switch
                    {
                        0 => new Color(0.06f, 0.05f, 0.07f),  // Void/Wall - very dark
                        1 => new Color(0.35f, 0.3f, 0.22f),   // Corridor - tan/parchment
                        2 => new Color(0.45f, 0.4f, 0.3f),    // Room floor - lighter tan
                        _ => new Color(0.3f, 0.25f, 0.2f)
                    };

                    // Apply visibility fade
                    tileColor = tileColor.Darkened(1f - visibilityFade);

                    _minimapContainer.DrawRect(
                        new Rect2(drawX - tileSize / 2f, drawZ - tileSize / 2f, tileSize, tileSize),
                        tileColor
                    );
                }
            }
        }

        // Draw circular border
        float borderRadius = MinimapSize / 2f - 2;
        _minimapContainer.DrawArc(new Vector2(centerX, centerY), borderRadius, 0, Mathf.Tau, 64, new Color(0.5f, 0.4f, 0.3f), 2f);

        // Draw enemies from cached list (refreshed every 0.5s, not every frame)
        foreach (var enemy in _cachedEnemies)
        {
            if (!GodotObject.IsInstanceValid(enemy)) continue;

            Vector3 relPos = enemy.GlobalPosition - playerPos;
            float dx = relPos.X * MinimapScale;
            float dz = relPos.Z * MinimapScale;
            float dist = new Vector2(dx, dz).Length();

            if (dist < borderRadius - 8)
            {
                // Pulsing red dot for enemies
                float pulse = 0.8f + 0.2f * Mathf.Sin((float)Time.GetTicksMsec() * 0.01f);
                _minimapContainer.DrawCircle(
                    new Vector2(centerX + dx, centerY + dz),
                    4f,
                    new Color(0.9f * pulse, 0.15f, 0.15f)
                );
            }
        }

        // Draw player marker (arrow pointing direction)
        float playerYaw = player.Rotation.Y;
        Vector2[] triangle = new Vector2[3];
        float size = 7f;

        triangle[0] = new Vector2(centerX, centerY) + new Vector2(
            Mathf.Sin(-playerYaw) * size,
            Mathf.Cos(-playerYaw) * size
        );
        triangle[1] = new Vector2(centerX, centerY) + new Vector2(
            Mathf.Sin(-playerYaw + 2.5f) * size * 0.6f,
            Mathf.Cos(-playerYaw + 2.5f) * size * 0.6f
        );
        triangle[2] = new Vector2(centerX, centerY) + new Vector2(
            Mathf.Sin(-playerYaw - 2.5f) * size * 0.6f,
            Mathf.Cos(-playerYaw - 2.5f) * size * 0.6f
        );

        // Player marker with glow
        _minimapContainer.DrawCircle(new Vector2(centerX, centerY), 10f, new Color(0.2f, 0.8f, 0.3f, 0.3f));
        _minimapContainer.DrawPolygon(triangle, new Color[] { new Color(0.3f, 0.95f, 0.4f) });
        _minimapContainer.DrawPolygon(triangle, new Color[] { new Color(0.1f, 0.3f, 0.15f) }); // Border

        // Draw "N" indicator at top
        _minimapContainer.DrawString(
            ThemeDB.FallbackFont,
            new Vector2(centerX - 4, 14),
            "N",
            HorizontalAlignment.Center,
            -1,
            12,
            new Color(0.8f, 0.7f, 0.5f)
        );
    }

    private void RefreshEnemyCache()
    {
        _cachedEnemies.Clear();
        var player = FPSController.Instance;
        if (player == null) return;

        var enemies = player.GetTree().GetNodesInGroup("Enemies");
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] is Node3D enemy && GodotObject.IsInstanceValid(enemy))
            {
                _cachedEnemies.Add(enemy);
            }
        }
    }

    private void UpdateExploredTiles(Vector3 playerPos)
    {
        var dungeon = GameManager3D.Instance?.GetDungeon();
        if (dungeon == null) return;

        var mapData = dungeon.GetMapData();
        if (mapData == null) return;

        // TileSize is 1f, so world coords map directly to tile indices
        int playerTileX = (int)playerPos.X;
        int playerTileZ = (int)playerPos.Z;

        int exploreRange = (int)ExplorationRadius;

        for (int dx = -exploreRange; dx <= exploreRange; dx++)
        {
            for (int dz = -exploreRange; dz <= exploreRange; dz++)
            {
                int tileX = playerTileX + dx;
                int tileZ = playerTileZ + dz;

                if (tileX < 0 || tileX >= mapData.GetLength(0) ||
                    tileZ < 0 || tileZ >= mapData.GetLength(1))
                    continue;

                // Only explore in a circular radius
                if (dx * dx + dz * dz <= exploreRange * exploreRange)
                {
                    long tileKey = ((long)tileX << 32) | (uint)tileZ;
                    _exploredTiles.Add(tileKey);
                }
            }
        }
    }

    private void ConnectPlayerSignals()
    {
        CallDeferred(nameof(ConnectPlayerSignalsDeferred));
    }

    private void ConnectPlayerSignalsDeferred()
    {
        var player = FPSController.Instance;
        if (player != null)
        {
            player.HealthChanged += OnPlayerHealthChanged;
            player.ManaChanged += OnPlayerManaChanged;
            player.ExperienceChanged += OnPlayerExperienceChanged;
            player.LeveledUp += OnPlayerLeveledUp;
            player.Attacked += OnPlayerAttacked;

            OnPlayerHealthChanged(player.CurrentHealth, player.MaxHealth);
            OnPlayerManaChanged(player.CurrentMana, player.MaxMana);
            OnPlayerExperienceChanged(player.CurrentExperience, player.ExperienceToLevel, player.PlayerLevel);
        }
    }

    private void ConnectGameManagerSignals()
    {
        CallDeferred(nameof(ConnectGameManagerSignalsDeferred));
    }

    private void ConnectGameManagerSignalsDeferred()
    {
        var gm = GameManager3D.Instance;
        if (gm != null)
        {
            gm.FloorTimerUpdated += OnTimerUpdated;
        }
    }

    private void ConnectAbilityManagerSignals()
    {
        CallDeferred(nameof(ConnectAbilityManagerSignalsDeferred));
    }

    private void ConnectAbilityManagerSignalsDeferred()
    {
        var am = AbilityManager3D.Instance;
        if (am != null)
        {
            am.HotbarSlotChanged += OnHotbarSlotChanged;
            am.AbilityCooldownUpdated += OnAbilityCooldownUpdated;
            am.TargetingStarted += OnTargetingStarted;
            am.TargetingEnded += OnTargetingEnded;
            am.AbilityActivated += OnAbilityActivated;

            // Initialize all 3 bars from ability manager
            int abilityCount = 0;
            for (int row = 0; row < 3; row++)
            {
                for (int slot = 0; slot < 10; slot++)
                {
                    var ability = am.GetHotbarAbility(row, slot);
                    if (ability != null)
                    {
                        UpdateActionSlot(row, slot, ability.AbilityId);
                        abilityCount++;
                    }
                }
            }
            GD.Print($"[HUD3D] Initialized {abilityCount} abilities in action bars");
        }
    }

    private void OnHotbarSlotChanged(int row, int slot, string? abilityId)
    {
        UpdateActionSlot(row, slot, abilityId);
    }

    private void OnAbilityCooldownUpdated(string abilityId, float remaining, float total)
    {
        UpdateAbilityCooldown(abilityId, remaining, total);
    }

    private void OnTargetingStarted(string abilityId, float radius, Color color)
    {
        var ability = AbilityManager3D.Instance?.GetAbility(abilityId);
        if (ability != null)
        {
            ShowTargetingPrompt(ability.AbilityName);
        }
    }

    private void OnTargetingEnded()
    {
        HideTargetingPrompt();
    }

    private void OnAbilityActivated(string abilityId)
    {
        FlashAbilityActivation(abilityId);
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = max;
            _healthBar.Value = current;
        }
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"{current}/{max}";
        }
    }

    private void OnPlayerManaChanged(int current, int max)
    {
        if (_manaBar != null)
        {
            _manaBar.MaxValue = max;
            _manaBar.Value = current;
        }
        if (_manaLabel != null)
        {
            _manaLabel.Text = $"{current}/{max}";
        }
    }

    private void OnPlayerAttacked()
    {
        _crosshairHitFlash = 0.1f;
    }

    private void OnPlayerExperienceChanged(int current, int toLevel, int level)
    {
        if (_expBar != null)
        {
            _expBar.MaxValue = toLevel;
            _expBar.Value = current;
        }
        if (_expLabel != null)
        {
            _expLabel.Text = $"Lv {level}";
        }
    }

    private void OnPlayerLeveledUp(int newLevel)
    {
        // Flash the XP bar or show level up effect
        GD.Print($"[HUD3D] Player reached level {newLevel}!");
        // Could add visual celebration here
    }

    private void OnTimerUpdated(float timeRemaining)
    {
        if (_timerLabel != null)
        {
            int minutes = (int)(timeRemaining / 60);
            int seconds = (int)(timeRemaining % 60);
            _timerLabel.Text = $"{minutes}:{seconds:D2}";

            if (timeRemaining <= 60)
            {
                _timerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
            }
            else if (timeRemaining <= 120)
            {
                _timerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
            }
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Position all UI elements once we have a valid viewport
        if (!_leftPanelsPositioned)
        {
            var screenSize = GetViewportRect().Size;
            if (screenSize.X > 0 && screenSize.Y > 0)
            {
                UpdateActionBarPosition();
                UpdateHealthPanelPosition();
                UpdateTargetFramePosition();
                UpdateShortcutIconsPosition();
                UpdateLeftPanelsPosition();
                UpdateMinimapPosition();
                UpdateCompassPosition();
                _leftPanelsPositioned = true;
                _cachedViewportSize = screenSize;
                GD.Print($"[HUD3D] UI positioned for viewport: {screenSize}");
            }
        }

        // Update crosshair flash
        if (_crosshairHitFlash > 0)
        {
            _crosshairHitFlash -= dt;
            _crosshair?.QueueRedraw();
        }

        // Update loot notifications (fade out, remove expired)
        UpdateLootNotifications(dt);

        // Update achievement notifications (fade out, remove expired, check multi-kill)
        UpdateAchievementNotifications(dt);

        // Only update UI positions when viewport size changes (not every frame)
        var currentViewportSize = GetViewportRect().Size;
        if (currentViewportSize != _cachedViewportSize)
        {
            _cachedViewportSize = currentViewportSize;
            UpdateActionBarPosition();
            UpdateHealthPanelPosition();
            UpdateTargetFramePosition();
            UpdateShortcutIconsPosition();
            UpdateLeftPanelsPosition();
            UpdateMinimapPosition();
            UpdateCompassPosition();
        }

        // Update target frame health in real-time and marker position
        _targetUpdateTimer -= dt;
        if (_currentTarget != null)
        {
            // Update 3D marker position every frame for smooth bobbing
            UpdateTargetMarkerPosition();

            if (_targetUpdateTimer <= 0)
            {
                _targetUpdateTimer = 0.1f;  // Update health 10x per second
                UpdateTargetFrame();
            }
        }

        // Update minimap and fog of war when player moves
        var player = FPSController.Instance;
        if (player != null && _minimapContainer != null)
        {
            Vector2 currentPos = new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z);
            if (currentPos.DistanceTo(_lastPlayerMinimapPos) > MinimapUpdateThreshold)
            {
                _lastPlayerMinimapPos = currentPos;
                // Update explored tiles for fog of war
                UpdateExploredTiles(player.GlobalPosition);
                _minimapContainer.QueueRedraw();
            }
        }

        // Update enemy cache periodically (not every frame)
        _enemyCacheTimer -= dt;
        if (_enemyCacheTimer <= 0)
        {
            _enemyCacheTimer = EnemyCacheInterval;
            RefreshEnemyCache();
        }

        // Update compass only when player rotation changes significantly
        if (player != null)
        {
            float currentYaw = player.Rotation.Y;
            if (Mathf.Abs(currentYaw - _lastCompassYaw) > CompassUpdateThreshold)
            {
                _lastCompassYaw = currentYaw;
                _compassContainer?.QueueRedraw();
            }
        }

        // Update slot flash animations
        foreach (var bar in _actionBars)
        {
            foreach (var slot in bar.Slots)
            {
                if (slot.FlashTimer > 0)
                {
                    slot.FlashTimer -= dt;
                    float flashAlpha = slot.FlashTimer / 0.2f;

                    if (slot.ActivationFlash != null)
                    {
                        slot.ActivationFlash.Color = new Color(1f, 1f, 0.7f, flashAlpha * 0.6f);
                    }

                    if (slot.FlashTimer <= 0 && slot.Background != null)
                    {
                        slot.Background.SetBorderWidthAll(1);
                        // Reset border color based on state
                        if (slot.CooldownRemaining > 0)
                        {
                            slot.Background.BorderColor = new Color(0.3f, 0.3f, 0.3f);
                        }
                        else if (!string.IsNullOrEmpty(slot.AbilityId))
                        {
                            slot.Background.BorderColor = new Color(0.5f, 0.7f, 0.5f);
                        }
                    }
                }
            }
        }
    }

    public void ShowInteractPrompt(string text = "Press [E] to interact")
    {
        if (_interactPrompt != null)
        {
            _interactPrompt.Text = text;
            _interactPrompt.Visible = true;
        }
    }

    public void HideInteractPrompt()
    {
        if (_interactPrompt != null)
        {
            _interactPrompt.Visible = false;
        }
    }

    public static void LogCombat(string message, Color color)
    {
        GD.Print($"[Combat] {message}");
    }

    private void CreateCompass()
    {
        // Compass bar at top center of screen - ruler style
        _compassContainer = new Control();
        _compassContainer.Name = "Compass";
        _compassContainer.CustomMinimumSize = new Vector2(600, 45);
        _compassContainer.Size = new Vector2(600, 45);
        // Will be positioned in _Process to stay centered
        _compassContainer.Draw += DrawCompass;
        AddChild(_compassContainer);
    }

    private void UpdateCompassPosition()
    {
        if (_compassContainer != null)
        {
            // Check if user has saved a custom position - if so, use it
            var savedPos = WindowPositionManager.GetPosition("HUD_Compass");
            if (savedPos != WindowPositionManager.CenterMarker)
            {
                return; // User has a saved position, don't override it
            }

            var viewportSize = GetViewportRect().Size;
            _compassContainer.Position = new Vector2(
                (viewportSize.X - 600) / 2,  // Center horizontally
                8  // Small margin from top
            );
        }
    }

    private void DrawCompass()
    {
        if (_compassContainer == null) return;

        var player = FPSController.Instance;
        if (player == null) return;

        float width = _compassContainer.Size.X;
        float height = _compassContainer.Size.Y;
        float centerX = width / 2f;

        // Get player facing angle in degrees (0 = North)
        float playerYaw = -player.Rotation.Y * Mathf.RadToDeg(1);
        playerYaw = (playerYaw % 360 + 360) % 360; // Normalize to 0-360

        // Background bar
        var bgRect = new Rect2(0, 5, width, 30);
        _compassContainer.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.15f, 0.85f));

        // Border
        _compassContainer.DrawRect(bgRect, new Color(0.4f, 0.4f, 0.5f), false, 2f);

        // Cardinal directions
        string[] cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        float[] cardinalAngles = { 0, 45, 90, 135, 180, 225, 270, 315 };

        float pixelsPerDegree = width / 180f; // Show 180 degrees of view

        for (int i = 0; i < cardinals.Length; i++)
        {
            float angleDiff = cardinalAngles[i] - playerYaw;
            // Normalize to -180 to 180
            while (angleDiff > 180) angleDiff -= 360;
            while (angleDiff < -180) angleDiff += 360;

            // Only draw if in visible range
            if (Mathf.Abs(angleDiff) < 90)
            {
                float xPos = centerX + angleDiff * pixelsPerDegree;

                // Cardinal marker
                Color markerColor = (i % 2 == 0) ? new Color(1f, 0.9f, 0.5f) : new Color(0.7f, 0.7f, 0.8f);
                float markerHeight = (i % 2 == 0) ? 20f : 12f;

                _compassContainer.DrawLine(
                    new Vector2(xPos, 10),
                    new Vector2(xPos, 10 + markerHeight),
                    markerColor,
                    (i % 2 == 0) ? 3f : 2f
                );

                // Draw label for cardinal directions
                if (i % 2 == 0)
                {
                    // Create simple text display using drawing
                    _compassContainer.DrawString(
                        ThemeDB.FallbackFont,
                        new Vector2(xPos - 8, 38),
                        cardinals[i],
                        HorizontalAlignment.Center,
                        -1,
                        16,
                        markerColor
                    );
                }
            }
        }

        // Draw tick marks every 15 degrees
        for (int angle = 0; angle < 360; angle += 15)
        {
            if (angle % 45 == 0) continue; // Skip cardinals

            float angleDiff = angle - playerYaw;
            while (angleDiff > 180) angleDiff -= 360;
            while (angleDiff < -180) angleDiff += 360;

            if (Mathf.Abs(angleDiff) < 90)
            {
                float xPos = centerX + angleDiff * pixelsPerDegree;
                _compassContainer.DrawLine(
                    new Vector2(xPos, 15),
                    new Vector2(xPos, 23),
                    new Color(0.5f, 0.5f, 0.55f),
                    1f
                );
            }
        }

        // Center indicator (where player is facing)
        _compassContainer.DrawLine(new Vector2(centerX, 0), new Vector2(centerX, 8), new Color(1f, 0.3f, 0.3f), 3f);
    }

    private void CreateShortcutIcons()
    {
        // Container for shortcut icons - 2 rows of 3, positioned to the LEFT of health panel
        var shortcutContainer = new VBoxContainer();
        shortcutContainer.Name = "ShortcutIcons";

        // Use fixed position - will be updated in _Process relative to health panel
        shortcutContainer.Position = new Vector2(10, 900);  // Initial position, updated later

        shortcutContainer.AddThemeConstantOverride("separation", 4);
        AddChild(shortcutContainer);

        _shortcutIcons = shortcutContainer;

        // Row 1: Spellbook, Inventory, Map
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 6);
        shortcutContainer.AddChild(row1);

        // Row 2: Loot, Debug, Menu
        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 6);
        shortcutContainer.AddChild(row2);

        // Store row references for adding icons
        _shortcutRow1 = row1;
        _shortcutRow2 = row2;

        // Create shortcut icon buttons with actual keybinds from InputMap (3 per row)
        CreateShortcutIcon(GetActionKeyDisplay("toggle_spellbook"), "Spellbook", new Color(0.4f, 0.5f, 0.9f), row1);
        CreateShortcutIcon(GetActionKeyDisplay("toggle_inventory"), "Inventory", new Color(0.5f, 0.7f, 0.4f), row1);
        CreateShortcutIcon(GetActionKeyDisplay("toggle_map"), "Map", new Color(0.7f, 0.6f, 0.4f), row1);
        CreateShortcutIcon(GetActionKeyDisplay("loot"), "Loot", new Color(0.9f, 0.7f, 0.3f), row2);
        CreateShortcutIcon(GetActionKeyDisplay("toggle_debug"), "Debug", new Color(0.3f, 0.8f, 0.4f), row2);
        CreateShortcutIcon(GetActionKeyDisplay("escape"), "Menu", new Color(0.7f, 0.4f, 0.4f), row2);
    }

    private static string GetActionKeyDisplay(string actionName)
    {
        var events = InputMap.ActionGetEvents(actionName);
        if (events.Count == 0) return "?";

        var ev = events[0];
        if (ev is InputEventKey keyEvent)
        {
            // Use physical keycode for display
            var key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
            return GetKeyDisplayName(key);
        }
        else if (ev is InputEventMouseButton mouseEvent)
        {
            return mouseEvent.ButtonIndex switch
            {
                MouseButton.Left => "LMB",
                MouseButton.Right => "RMB",
                MouseButton.Middle => "MMB",
                MouseButton.WheelUp => "MWU",
                MouseButton.WheelDown => "MWD",
                _ => $"M{(int)mouseEvent.ButtonIndex}"
            };
        }

        return "?";
    }

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.Escape => "ESC",
            Key.Tab => "TAB",
            Key.Backspace => "BKSP",
            Key.Enter => "ENT",
            Key.Insert => "INS",
            Key.Delete => "DEL",
            Key.Home => "HOME",
            Key.End => "END",
            Key.Pageup => "PGUP",
            Key.Pagedown => "PGDN",
            Key.Space => "SPC",
            Key.Shift => "SHFT",
            Key.Ctrl => "CTRL",
            Key.Alt => "ALT",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.Left => "←",
            Key.Right => "→",
            Key.F1 => "F1",
            Key.F2 => "F2",
            Key.F3 => "F3",
            Key.F4 => "F4",
            Key.F5 => "F5",
            Key.F6 => "F6",
            Key.F7 => "F7",
            Key.F8 => "F8",
            Key.F9 => "F9",
            Key.F10 => "F10",
            Key.F11 => "F11",
            Key.F12 => "F12",
            _ => OS.GetKeycodeString(key).ToUpper()
        };
    }

    private void CreateShortcutIcon(string key, string label, Color color, HBoxContainer parent)
    {
        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 2);

        // Key box
        var keyBox = new PanelContainer();
        keyBox.CustomMinimumSize = new Vector2(45, 32);

        var keyStyle = new StyleBoxFlat();
        keyStyle.BgColor = color.Darkened(0.5f);
        keyStyle.BorderColor = color;
        keyStyle.SetBorderWidthAll(2);
        keyStyle.SetCornerRadiusAll(4);
        keyBox.AddThemeStyleboxOverride("panel", keyStyle);

        var keyLabel = new Label();
        keyLabel.Text = key;
        keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        keyLabel.VerticalAlignment = VerticalAlignment.Center;
        keyLabel.AddThemeFontSizeOverride("font_size", 13);
        keyLabel.AddThemeColorOverride("font_color", color.Lightened(0.3f));
        keyBox.AddChild(keyLabel);
        container.AddChild(keyBox);

        // Description label
        var descLabel = new Label();
        descLabel.Text = label;
        descLabel.HorizontalAlignment = HorizontalAlignment.Center;
        descLabel.AddThemeFontSizeOverride("font_size", 9);
        descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        container.AddChild(descLabel);

        parent.AddChild(container);
    }

    private void CreateOverviewMap()
    {
        // Full-screen overview map container (hidden by default)
        _overviewMap = new Control();
        _overviewMap.Name = "OverviewMap";
        _overviewMap.SetAnchorsPreset(LayoutPreset.FullRect);
        _overviewMap.Visible = false;
        _overviewMap.ZIndex = 100; // On top of other UI
        AddChild(_overviewMap);

        // Dark overlay background
        var darkOverlay = new ColorRect();
        darkOverlay.Name = "DarkOverlay";
        darkOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        darkOverlay.Color = new Color(0.02f, 0.02f, 0.05f, 0.95f);
        _overviewMap.AddChild(darkOverlay);

        // Main map panel with decorative border - FULLSCREEN centered
        var mapPanel = new PanelContainer();
        mapPanel.Name = "MapPanel";

        // Calculate actual screen-filling size (90% of screen with 5% margin on each side)
        // For a 1920x1080 screen: 1728x972 panel centered
        float margin = 0.05f;
        var screenSize = GetViewportRect().Size;
        if (screenSize == Vector2.Zero) screenSize = new Vector2(1920, 1080); // Fallback

        float panelWidth = screenSize.X * (1f - margin * 2f);
        float panelHeight = screenSize.Y * (1f - margin * 2f);
        float panelX = screenSize.X * margin;
        float panelY = screenSize.Y * margin;

        mapPanel.Position = new Vector2(panelX, panelY);
        mapPanel.Size = new Vector2(panelWidth, panelHeight);
        mapPanel.CustomMinimumSize = new Vector2(panelWidth, panelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.06f, 0.05f, 0.08f, 0.98f);
        panelStyle.BorderColor = new Color(0.5f, 0.4f, 0.3f); // Bronze border
        panelStyle.SetBorderWidthAll(4);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetContentMarginAll(15);
        mapPanel.AddThemeStyleboxOverride("panel", panelStyle);
        _overviewMap.AddChild(mapPanel);

        // Inner frame for parchment effect
        var innerFrame = new PanelContainer();
        innerFrame.Name = "InnerFrame";
        innerFrame.SetAnchorsPreset(LayoutPreset.FullRect);

        var innerStyle = new StyleBoxFlat();
        innerStyle.BgColor = new Color(0.08f, 0.07f, 0.1f);
        innerStyle.BorderColor = new Color(0.35f, 0.3f, 0.25f);
        innerStyle.SetBorderWidthAll(2);
        innerStyle.SetCornerRadiusAll(6);
        innerStyle.SetContentMarginAll(10);
        innerFrame.AddThemeStyleboxOverride("panel", innerStyle);
        mapPanel.AddChild(innerFrame);

        // Layout container
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        innerFrame.AddChild(vbox);

        // Title
        var titleLabel = new Label();
        titleLabel.Name = "MapTitle";
        titleLabel.Text = "⚔ DUNGEON MAP ⚔";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 28);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        vbox.AddChild(titleLabel);

        // Map drawing area - use a Panel to ensure it has proper size and background
        var mapDrawArea = new Panel();
        mapDrawArea.Name = "MapDrawArea";
        mapDrawArea.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        mapDrawArea.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mapDrawArea.CustomMinimumSize = new Vector2(600, 400); // Ensure minimum size
        mapDrawArea.ClipContents = true;

        // Style the panel with dark background
        var drawAreaStyle = new StyleBoxFlat();
        drawAreaStyle.BgColor = new Color(0.04f, 0.035f, 0.05f);
        drawAreaStyle.BorderColor = new Color(0.25f, 0.2f, 0.15f);
        drawAreaStyle.SetBorderWidthAll(1);
        mapDrawArea.AddThemeStyleboxOverride("panel", drawAreaStyle);

        // Create a separate control inside the panel for drawing
        var drawControl = new Control();
        drawControl.Name = "MapDrawControl";
        // Fill the parent panel completely
        drawControl.SetAnchorsPreset(LayoutPreset.FullRect);
        drawControl.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        drawControl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        drawControl.Draw += DrawOverviewMap;
        mapDrawArea.AddChild(drawControl);

        vbox.AddChild(mapDrawArea);

        // Store reference to the draw control for redraw calls
        _overviewMapDrawArea = drawControl;

        GD.Print($"[HUD3D] Overview map draw area created - size: {mapDrawArea.Size}");

        // Legend/Controls panel at bottom
        var controlsHbox = new HBoxContainer();
        controlsHbox.AddThemeConstantOverride("separation", 30);
        controlsHbox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(controlsHbox);

        // Legend
        CreateMapLegend(controlsHbox);

        // Separator
        var sep = new VSeparator();
        sep.CustomMinimumSize = new Vector2(2, 40);
        controlsHbox.AddChild(sep);

        // Close instructions
        var closeLabel = new Label();
        closeLabel.Text = $"Press [{GetActionKeyDisplay("toggle_map")}] or [{GetActionKeyDisplay("escape")}] to close";
        closeLabel.AddThemeFontSizeOverride("font_size", 14);
        closeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
        controlsHbox.AddChild(closeLabel);
    }

    private Control? _overviewMapDrawArea;

    private void CreateMapLegend(HBoxContainer parent)
    {
        // Room floor
        var floorIcon = new ColorRect();
        floorIcon.CustomMinimumSize = new Vector2(16, 16);
        floorIcon.Color = new Color(0.4f, 0.35f, 0.25f);
        parent.AddChild(floorIcon);
        var floorLabel = new Label { Text = "Room" };
        floorLabel.AddThemeFontSizeOverride("font_size", 12);
        floorLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        parent.AddChild(floorLabel);

        // Corridor
        var corridorIcon = new ColorRect();
        corridorIcon.CustomMinimumSize = new Vector2(16, 16);
        corridorIcon.Color = new Color(0.3f, 0.28f, 0.22f);
        parent.AddChild(corridorIcon);
        var corridorLabel = new Label { Text = "Corridor" };
        corridorLabel.AddThemeFontSizeOverride("font_size", 12);
        corridorLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        parent.AddChild(corridorLabel);

        // Player
        var playerIcon = new ColorRect();
        playerIcon.CustomMinimumSize = new Vector2(16, 16);
        playerIcon.Color = new Color(0.3f, 0.9f, 0.4f);
        parent.AddChild(playerIcon);
        var playerLabel = new Label { Text = "You" };
        playerLabel.AddThemeFontSizeOverride("font_size", 12);
        playerLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        parent.AddChild(playerLabel);

        // Unexplored
        var fogIcon = new ColorRect();
        fogIcon.CustomMinimumSize = new Vector2(16, 16);
        fogIcon.Color = new Color(0.03f, 0.025f, 0.04f);
        parent.AddChild(fogIcon);
        var fogLabel = new Label { Text = "Unexplored" };
        fogLabel.AddThemeFontSizeOverride("font_size", 12);
        fogLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        parent.AddChild(fogLabel);
    }

    private void DrawOverviewMap()
    {
        if (_overviewMapDrawArea == null) return;

        var dungeon = GameManager3D.Instance?.GetDungeon();
        var player = FPSController.Instance;
        if (dungeon == null || player == null) return;

        var mapData = dungeon.GetMapData();
        if (mapData == null) return;

        // Get the actual draw area size - try multiple sources
        Vector2 viewSize = _overviewMapDrawArea.Size;
        if (viewSize.X <= 1 || viewSize.Y <= 1)
        {
            // Try to get size from parent panel
            var parent = _overviewMapDrawArea.GetParent<Control>();
            if (parent != null && parent.Size.X > 1 && parent.Size.Y > 1)
            {
                viewSize = parent.Size;
            }
            else
            {
                // Use viewport-based calculation
                var screenSize = GetViewportRect().Size;
                viewSize = new Vector2(screenSize.X * 0.7f, screenSize.Y * 0.6f);
            }
        }

        float centerX = viewSize.X / 2;
        float centerY = viewSize.Y / 2;

        // Calculate tile size to fit the map nicely
        int mapWidth = mapData.GetLength(0);
        int mapHeight = mapData.GetLength(1);

        // Base tile size to fit entire map in view at zoom 1.0
        float baseTileSize = Mathf.Min(
            (viewSize.X - 20) / mapWidth,
            (viewSize.Y - 20) / mapHeight
        );
        // Apply zoom and ensure minimum visibility
        float tileSize = Mathf.Max(2f, baseTileSize * _overviewMapZoom);

        // Calculate player tile position (TileSize = 1f, so direct mapping)
        float playerTileX = player.GlobalPosition.X;
        float playerTileZ = player.GlobalPosition.Z;

        // ALWAYS center the view on the player
        float offsetX = centerX - playerTileX * tileSize;
        float offsetY = centerY - playerTileZ * tileSize;

        // Calculate total map size for grid lines
        float totalMapWidth = mapWidth * tileSize;
        float totalMapHeight = mapHeight * tileSize;

        // Draw dark background
        _overviewMapDrawArea.DrawRect(new Rect2(0, 0, viewSize.X, viewSize.Y), new Color(0.03f, 0.025f, 0.04f));

        // Draw tiles
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                float drawX = offsetX + x * tileSize;
                float drawY = offsetY + z * tileSize;

                // Skip if outside visible area
                if (drawX + tileSize < 0 || drawX > viewSize.X ||
                    drawY + tileSize < 0 || drawY > viewSize.Y)
                    continue;

                // Check fog of war using the same explored tiles as minimap
                long tileKey = ((long)x << 32) | (uint)z;
                bool explored = _exploredTiles.Contains(tileKey);

                if (!explored)
                {
                    // Unexplored - draw dark fog
                    _overviewMapDrawArea.DrawRect(
                        new Rect2(drawX, drawY, tileSize - 1, tileSize - 1),
                        new Color(0.03f, 0.025f, 0.04f)
                    );
                    continue;
                }

                int tile = mapData[x, z];
                // Map data: 0=void, 1=corridor, 2=room floor
                Color tileColor = tile switch
                {
                    0 => new Color(0.08f, 0.07f, 0.1f),   // Void - dark
                    1 => new Color(0.3f, 0.28f, 0.22f),   // Corridor - slightly darker tan
                    2 => new Color(0.4f, 0.35f, 0.25f),   // Room floor - tan/parchment
                    _ => new Color(0.2f, 0.18f, 0.15f)
                };

                _overviewMapDrawArea.DrawRect(
                    new Rect2(drawX, drawY, tileSize - 1, tileSize - 1),
                    tileColor
                );
            }
        }

        // Draw grid lines (subtle)
        Color gridColor = new Color(0.15f, 0.12f, 0.1f, 0.3f);
        for (int x = 0; x <= mapWidth; x++)
        {
            float lineX = offsetX + x * tileSize;
            if (lineX >= 0 && lineX <= viewSize.X)
            {
                _overviewMapDrawArea.DrawLine(
                    new Vector2(lineX, Mathf.Max(0, offsetY)),
                    new Vector2(lineX, Mathf.Min(viewSize.Y, offsetY + totalMapHeight)),
                    gridColor,
                    1f
                );
            }
        }
        for (int z = 0; z <= mapHeight; z++)
        {
            float lineY = offsetY + z * tileSize;
            if (lineY >= 0 && lineY <= viewSize.Y)
            {
                _overviewMapDrawArea.DrawLine(
                    new Vector2(Mathf.Max(0, offsetX), lineY),
                    new Vector2(Mathf.Min(viewSize.X, offsetX + totalMapWidth), lineY),
                    gridColor,
                    1f
                );
            }
        }

        // Draw enemies (only in explored areas)
        var enemies = player.GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D enemy)
            {
                int enemyTileX = (int)enemy.GlobalPosition.X;
                int enemyTileZ = (int)enemy.GlobalPosition.Z;

                // Check if in explored area
                long tileKey = ((long)enemyTileX << 32) | (uint)enemyTileZ;
                if (!_exploredTiles.Contains(tileKey)) continue;

                float enemyDrawX = offsetX + enemyTileX * tileSize + tileSize / 2f;
                float enemyDrawY = offsetY + enemyTileZ * tileSize + tileSize / 2f;

                // Red pulsing dot for enemies
                float pulse = 0.8f + 0.2f * Mathf.Sin((float)Time.GetTicksMsec() * 0.01f);
                _overviewMapDrawArea.DrawCircle(
                    new Vector2(enemyDrawX, enemyDrawY),
                    tileSize * 0.4f,
                    new Color(0.9f * pulse, 0.15f, 0.15f)
                );
            }
        }

        // Draw player position with directional arrow
        float playerDrawX = offsetX + playerTileX * tileSize + tileSize / 2f;
        float playerDrawY = offsetY + playerTileZ * tileSize + tileSize / 2f;

        // Player glow
        _overviewMapDrawArea.DrawCircle(
            new Vector2(playerDrawX, playerDrawY),
            tileSize * 0.8f,
            new Color(0.2f, 0.8f, 0.3f, 0.3f)
        );

        // Player directional arrow
        float playerYaw = player.Rotation.Y;
        float arrowSize = tileSize * 0.6f;
        Vector2[] triangle = new Vector2[3];

        // Point in direction player is facing (accounting for 2D top-down view)
        triangle[0] = new Vector2(playerDrawX, playerDrawY) + new Vector2(
            Mathf.Sin(-playerYaw) * arrowSize,
            Mathf.Cos(-playerYaw) * arrowSize
        );
        triangle[1] = new Vector2(playerDrawX, playerDrawY) + new Vector2(
            Mathf.Sin(-playerYaw + 2.5f) * arrowSize * 0.5f,
            Mathf.Cos(-playerYaw + 2.5f) * arrowSize * 0.5f
        );
        triangle[2] = new Vector2(playerDrawX, playerDrawY) + new Vector2(
            Mathf.Sin(-playerYaw - 2.5f) * arrowSize * 0.5f,
            Mathf.Cos(-playerYaw - 2.5f) * arrowSize * 0.5f
        );

        _overviewMapDrawArea.DrawPolygon(triangle, new Color[] { new Color(0.3f, 0.95f, 0.4f) });

        // Draw compass indicator in corner
        float compassX = viewSize.X - 50;
        float compassY = 50;
        float compassRadius = 35;

        _overviewMapDrawArea.DrawCircle(new Vector2(compassX, compassY), compassRadius, new Color(0.1f, 0.1f, 0.12f, 0.8f));
        _overviewMapDrawArea.DrawArc(new Vector2(compassX, compassY), compassRadius, 0, Mathf.Tau, 32, new Color(0.4f, 0.35f, 0.3f), 2f);

        // N indicator
        _overviewMapDrawArea.DrawString(
            ThemeDB.FallbackFont,
            new Vector2(compassX - 5, compassY - compassRadius + 15),
            "N",
            HorizontalAlignment.Center,
            -1,
            14,
            new Color(0.9f, 0.8f, 0.5f)
        );

        // Player direction on compass
        Vector2 compassDir = new Vector2(
            Mathf.Sin(-playerYaw) * (compassRadius - 10),
            Mathf.Cos(-playerYaw) * (compassRadius - 10)
        );
        _overviewMapDrawArea.DrawLine(
            new Vector2(compassX, compassY),
            new Vector2(compassX, compassY) + compassDir,
            new Color(0.3f, 0.9f, 0.4f),
            3f
        );
    }

    public void ToggleOverviewMap()
    {
        if (_overviewMap == null) return;

        _isOverviewMapVisible = !_isOverviewMapVisible;
        _overviewMap.Visible = _isOverviewMapVisible;

        if (_isOverviewMapVisible)
        {
            // Hide HUD elements that overlap
            HideChatWindow();
            InspectMode3D.Instance?.HideInfoPanel();

            // Pause the game while map is open
            GetTree().Paused = true;

            // Capture mouse for UI
            Input.MouseMode = Input.MouseModeEnum.Visible;

            // Force update explored tiles before showing map
            var player = FPSController.Instance;
            if (player != null)
            {
                UpdateExploredTiles(player.GlobalPosition);
            }

            // Force redraw after a frame to ensure proper sizing
            CallDeferred(nameof(ForceOverviewMapRedraw));

            GD.Print($"[HUD3D] Map opened - explored tiles: {_exploredTiles.Count}");
        }
        else
        {
            // Show HUD elements again
            ShowChatWindow();
            InspectMode3D.Instance?.ShowInfoPanel();

            // Unpause the game
            GetTree().Paused = false;

            // Restore mouse capture for gameplay
            Input.MouseMode = Input.MouseModeEnum.Captured;

            GD.Print("[HUD3D] Map closed - game resumed");
        }
    }

    public void CloseOverviewMap()
    {
        if (_isOverviewMapVisible)
        {
            ToggleOverviewMap();
        }
    }

    public bool IsOverviewMapVisible => _isOverviewMapVisible;

    public override void _Input(InputEvent @event)
    {
        // Toggle overview map with M key
        if (@event.IsActionPressed("toggle_map"))
        {
            ToggleOverviewMap();
            GetViewport().SetInputAsHandled();
        }
        // Close map with ESC key (if map is open)
        else if (@event.IsActionPressed("escape") && _isOverviewMapVisible)
        {
            CloseOverviewMap();
            GetViewport().SetInputAsHandled();
        }
        // Zoom in/out on map with mouse wheel
        else if (_isOverviewMapVisible && @event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    _overviewMapZoom = Mathf.Clamp(_overviewMapZoom * 1.2f, 0.5f, 5f);
                    _overviewMapDrawArea?.QueueRedraw();
                    GetViewport().SetInputAsHandled();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    _overviewMapZoom = Mathf.Clamp(_overviewMapZoom / 1.2f, 0.5f, 5f);
                    _overviewMapDrawArea?.QueueRedraw();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}

/// <summary>
/// Data for a row of action bar slots.
/// </summary>
public class ActionBarRow
{
    public int RowIndex { get; set; }
    public string ModifierPrefix { get; set; } = "";
    public HBoxContainer? Container { get; set; }
    public List<ActionSlot> Slots { get; } = new();
}

/// <summary>
/// Data for a single action bar slot.
/// Can hold either an ability or a consumable item.
/// </summary>
public class ActionSlot
{
    public int SlotIndex { get; set; }
    public int RowIndex { get; set; }
    public string? AbilityId { get; set; }
    public string? ConsumableId { get; set; }  // Item ID for consumables
    public PanelContainer? Container { get; set; }
    public StyleBoxFlat? Background { get; set; }
    public TextureRect? Icon { get; set; }
    public Label? KeyLabel { get; set; }
    public Label? StackLabel { get; set; }  // Shows stack count for consumables
    public ColorRect? CooldownOverlay { get; set; }
    public Label? CooldownLabel { get; set; }
    public ColorRect? ActivationFlash { get; set; }
    public float FlashTimer { get; set; }
    public float CooldownRemaining { get; set; }
    public float CooldownTotal { get; set; }

    /// <summary>
    /// Check if this slot has a consumable assigned.
    /// </summary>
    public bool HasConsumable => !string.IsNullOrEmpty(ConsumableId);

    /// <summary>
    /// Check if this slot has an ability assigned.
    /// </summary>
    public bool HasAbility => !string.IsNullOrEmpty(AbilityId);

    /// <summary>
    /// Clear all assignments from this slot.
    /// </summary>
    public void Clear()
    {
        AbilityId = null;
        ConsumableId = null;
    }
}

/// <summary>
/// Data for a loot notification entry.
/// </summary>
public class LootNotification
{
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }
    public Color ItemColor { get; set; }
    public float TimeRemaining { get; set; }
    public PanelContainer? Container { get; set; }
    public Label? Label { get; set; }

    public void UpdateLabel()
    {
        if (Label != null)
            Label.Text = Quantity > 1 ? $"{ItemName} x{Quantity}" : ItemName;
    }
}

/// <summary>
/// Data for an achievement notification entry.
/// </summary>
public class AchievementNotification
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Color AccentColor { get; set; }
    public float TimeRemaining { get; set; }
    public PanelContainer? Container { get; set; }
}
