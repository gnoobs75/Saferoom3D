using Godot;
using System;
using System.Collections.Generic;
using SafeRoom3D.Items;
using SafeRoom3D.Player;
using SafeRoom3D.Core;

namespace SafeRoom3D.UI;

/// <summary>
/// Character Sheet UI showing player stats, attributes, and equipment slots.
/// Press C to open. Left-click equipment in inventory to equip.
/// Features tabs: Portrait (attributes/equipment) and Run Stats (combat statistics).
/// </summary>
public partial class CharacterSheetUI : Control
{
    public static CharacterSheetUI? Instance { get; private set; }

    // UI Elements
    private PanelContainer? _mainPanel;
    private Label? _titleLabel;
    private Button? _closeButton;

    // Tab system
    private enum SheetTab { Portrait, RunStats }
    private SheetTab _currentTab = SheetTab.Portrait;
    private Button? _portraitTabBtn;
    private Button? _statsTabBtn;
    private Control? _portraitContent;
    private Control? _statsContent;

    // Attribute display
    private Label? _levelLabel;
    private Label? _strengthLabel;
    private Label? _dexterityLabel;
    private Label? _vitalityLabel;
    private Label? _energyLabel;
    private Label? _attributePointsLabel;

    // Attribute increase buttons
    private Button? _strButton;
    private Button? _dexButton;
    private Button? _vitButton;
    private Button? _eneButton;

    // Derived stats display
    private Label? _healthLabel;
    private Label? _manaLabel;
    private Label? _damageLabel;
    private Label? _armorLabel;
    private Label? _critLabel;
    private Label? _blockLabel;
    private Label? _dodgeLabel;

    // Equipment slots
    private readonly Dictionary<EquipmentSlot, EquipmentSlotUI> _equipmentSlots = new();

    // Run Stats labels
    private Label? _runTimeLabel;
    private Label? _totalKillsLabel;
    private Label? _bossKillsLabel;
    private Label? _deathsLabel;
    private Label? _damageDealtLabel;
    private Label? _damageTakenLabel;
    private Label? _largestHitLabel;
    private Label? _abilityCastsLabel;
    private Label? _favoriteAbilityLabel;
    private Label? _killsPerMinLabel;
    private Label? _mostKilledLabel;
    private VBoxContainer? _killBreakdownContainer;

    // Tooltip
    private PanelContainer? _tooltipPanel;
    private VBoxContainer? _tooltipContent;

    // Panel dimensions
    private const float PanelWidth = 600;
    private const float PanelHeight = 550;
    private const int EquipSlotSize = 56;

    // Dragging
    private const string WindowName = "CharacterSheet";
    private bool _isDragging;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        CreateUI();
        CreateTooltip();

        // Connect to equipment changes
        CallDeferred(nameof(ConnectSignals));

        GD.Print("[CharacterSheetUI] Ready");
    }

    private void ConnectSignals()
    {
        var player = FPSController.Instance;
        if (player != null)
        {
            player.Equipment.OnEquipmentChanged += RefreshAll;
        }
    }

    private void CreateUI()
    {
        // Semi-transparent background
        var background = new ColorRect();
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.Color = new Color(0, 0, 0, 0.6f);
        background.MouseFilter = MouseFilterEnum.Stop;
        AddChild(background);

        // Main panel
        _mainPanel = new PanelContainer();
        _mainPanel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.08f, 0.14f, 0.98f);
        panelStyle.BorderColor = new Color(0.6f, 0.5f, 0.3f); // Golden border
        panelStyle.SetBorderWidthAll(3);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetContentMarginAll(15);
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_mainPanel);

        // Main content
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        _mainPanel.AddChild(content);

        // Header with title and close
        CreateHeader(content);

        // Tab buttons
        CreateTabButtons(content);
        content.AddChild(new HSeparator());

        // Portrait Content (default tab)
        _portraitContent = new Control();
        _portraitContent.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddChild(_portraitContent);
        CreatePortraitContent(_portraitContent);

        // Run Stats Content (hidden by default)
        _statsContent = new Control();
        _statsContent.SizeFlagsVertical = SizeFlags.ExpandFill;
        _statsContent.Visible = false;
        content.AddChild(_statsContent);
        CreateRunStatsContent(_statsContent);
    }

    private void CreateTabButtons(VBoxContainer parent)
    {
        var tabRow = new HBoxContainer();
        tabRow.AddThemeConstantOverride("separation", 8);
        parent.AddChild(tabRow);

        // Portrait tab button
        _portraitTabBtn = CreateTabButton("Portrait", true);
        _portraitTabBtn.Pressed += () => SwitchTab(SheetTab.Portrait);
        tabRow.AddChild(_portraitTabBtn);

        // Run Stats tab button
        _statsTabBtn = CreateTabButton("Run Stats", false);
        _statsTabBtn.Pressed += () => SwitchTab(SheetTab.RunStats);
        tabRow.AddChild(_statsTabBtn);

        // Spacer
        var spacer = new Control();
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tabRow.AddChild(spacer);
    }

    private Button CreateTabButton(string text, bool active)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(100, 28);
        btn.AddThemeFontSizeOverride("font_size", 13);

        var style = new StyleBoxFlat();
        style.BgColor = active ? new Color(0.25f, 0.2f, 0.35f) : new Color(0.12f, 0.1f, 0.18f);
        style.BorderColor = active ? new Color(0.7f, 0.6f, 0.4f) : new Color(0.3f, 0.25f, 0.4f);
        style.SetBorderWidthAll(active ? 2 : 1);
        style.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = style.Duplicate() as StyleBoxFlat;
        if (hoverStyle != null)
        {
            hoverStyle.BgColor = new Color(0.3f, 0.25f, 0.4f);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
        }

        return btn;
    }

    private void UpdateTabButtonStyles()
    {
        bool portraitActive = _currentTab == SheetTab.Portrait;

        // Portrait button
        var pStyle = new StyleBoxFlat();
        pStyle.BgColor = portraitActive ? new Color(0.25f, 0.2f, 0.35f) : new Color(0.12f, 0.1f, 0.18f);
        pStyle.BorderColor = portraitActive ? new Color(0.7f, 0.6f, 0.4f) : new Color(0.3f, 0.25f, 0.4f);
        pStyle.SetBorderWidthAll(portraitActive ? 2 : 1);
        pStyle.SetCornerRadiusAll(6);
        _portraitTabBtn?.AddThemeStyleboxOverride("normal", pStyle);

        // Stats button
        var sStyle = new StyleBoxFlat();
        sStyle.BgColor = !portraitActive ? new Color(0.25f, 0.2f, 0.35f) : new Color(0.12f, 0.1f, 0.18f);
        sStyle.BorderColor = !portraitActive ? new Color(0.7f, 0.6f, 0.4f) : new Color(0.3f, 0.25f, 0.4f);
        sStyle.SetBorderWidthAll(!portraitActive ? 2 : 1);
        sStyle.SetCornerRadiusAll(6);
        _statsTabBtn?.AddThemeStyleboxOverride("normal", sStyle);
    }

    private void SwitchTab(SheetTab tab)
    {
        if (_currentTab == tab) return;

        _currentTab = tab;
        UpdateTabButtonStyles();

        _portraitContent!.Visible = (tab == SheetTab.Portrait);
        _statsContent!.Visible = (tab == SheetTab.RunStats);

        if (tab == SheetTab.RunStats)
        {
            RefreshRunStats();
        }

        SoundManager3D.Instance?.PlaySound("menu_select");
    }

    private void CreatePortraitContent(Control parent)
    {
        // Main area - horizontal split
        var mainArea = new HBoxContainer();
        mainArea.AddThemeConstantOverride("separation", 20);
        mainArea.SetAnchorsPreset(LayoutPreset.FullRect);
        parent.AddChild(mainArea);

        // Left side - Stats
        var leftPanel = new VBoxContainer();
        leftPanel.AddThemeConstantOverride("separation", 8);
        leftPanel.CustomMinimumSize = new Vector2(240, 0);
        CreateStatsPanel(leftPanel);
        mainArea.AddChild(leftPanel);

        // Right side - Equipment
        var rightPanel = new VBoxContainer();
        rightPanel.AddThemeConstantOverride("separation", 8);
        rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CreateEquipmentPanel(rightPanel);
        mainArea.AddChild(rightPanel);
    }

    private void CreateRunStatsContent(Control parent)
    {
        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        parent.AddChild(scroll);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 12);
        mainVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(mainVbox);

        // Two column layout
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 30);
        mainVbox.AddChild(columns);

        // Left column - Overview stats
        var leftCol = new VBoxContainer();
        leftCol.AddThemeConstantOverride("separation", 6);
        leftCol.CustomMinimumSize = new Vector2(250, 0);
        columns.AddChild(leftCol);

        CreateRunStatsSection(leftCol, "Run Overview", new Color(0.9f, 0.8f, 0.5f));
        _runTimeLabel = CreateRunStatLabel(leftCol, "Time: 0:00");
        _totalKillsLabel = CreateRunStatLabel(leftCol, "Total Kills: 0", new Color(0.9f, 0.4f, 0.4f));
        _bossKillsLabel = CreateRunStatLabel(leftCol, "Bosses Slain: 0", new Color(1f, 0.7f, 0.3f));
        _deathsLabel = CreateRunStatLabel(leftCol, "Deaths: 0", new Color(0.6f, 0.6f, 0.6f));
        _killsPerMinLabel = CreateRunStatLabel(leftCol, "Kills/Minute: 0.0", new Color(0.7f, 0.9f, 0.7f));

        leftCol.AddChild(new HSeparator());

        CreateRunStatsSection(leftCol, "Damage", new Color(0.9f, 0.5f, 0.5f));
        _damageDealtLabel = CreateRunStatLabel(leftCol, "Damage Dealt: 0");
        _damageTakenLabel = CreateRunStatLabel(leftCol, "Damage Taken: 0");
        _largestHitLabel = CreateRunStatLabel(leftCol, "Biggest Hit: 0", new Color(1f, 0.85f, 0.4f));

        leftCol.AddChild(new HSeparator());

        CreateRunStatsSection(leftCol, "Abilities", new Color(0.5f, 0.7f, 1f));
        _abilityCastsLabel = CreateRunStatLabel(leftCol, "Abilities Cast: 0");
        _favoriteAbilityLabel = CreateRunStatLabel(leftCol, "Most Used: -", new Color(0.7f, 0.85f, 1f));

        // Right column - Kill breakdown
        var rightCol = new VBoxContainer();
        rightCol.AddThemeConstantOverride("separation", 6);
        rightCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(rightCol);

        CreateRunStatsSection(rightCol, "Kill Breakdown", new Color(0.9f, 0.6f, 0.6f));
        _mostKilledLabel = CreateRunStatLabel(rightCol, "Most Killed: -", new Color(0.95f, 0.7f, 0.5f));

        rightCol.AddChild(new HSeparator());

        // Container for dynamic kill list
        _killBreakdownContainer = new VBoxContainer();
        _killBreakdownContainer.AddThemeConstantOverride("separation", 3);
        rightCol.AddChild(_killBreakdownContainer);

        // Placeholder text
        var placeholder = new Label();
        placeholder.Text = "No kills yet...";
        placeholder.AddThemeFontSizeOverride("font_size", 11);
        placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.45f, 0.6f));
        _killBreakdownContainer.AddChild(placeholder);
    }

    private void CreateRunStatsSection(VBoxContainer parent, string title, Color color)
    {
        var label = new Label();
        label.Text = title;
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
    }

    private Label CreateRunStatLabel(VBoxContainer parent, string text, Color? color = null)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", color ?? new Color(0.85f, 0.82f, 0.9f));
        parent.AddChild(label);
        return label;
    }

    private void RefreshRunStats()
    {
        var stats = GameStats.Instance;
        if (stats == null) return;

        // Format time
        int mins = (int)(stats.SessionTime / 60);
        int secs = (int)(stats.SessionTime % 60);
        _runTimeLabel!.Text = $"Time: {mins}:{secs:D2}";

        _totalKillsLabel!.Text = $"Total Kills: {stats.TotalKills}";
        _bossKillsLabel!.Text = $"Bosses Slain: {stats.BossKills}";
        _deathsLabel!.Text = $"Deaths: {stats.Deaths}";
        _killsPerMinLabel!.Text = $"Kills/Minute: {stats.GetKillsPerMinute():F1}";

        _damageDealtLabel!.Text = $"Damage Dealt: {stats.TotalDamageDealt:N0}";
        _damageTakenLabel!.Text = $"Damage Taken: {stats.TotalDamageTaken:N0}";

        if (stats.LargestHit > 0)
        {
            string target = string.IsNullOrEmpty(stats.LargestHitTarget) ? "" : $" ({stats.LargestHitTarget})";
            _largestHitLabel!.Text = $"Biggest Hit: {stats.LargestHit:N0}{target}";
        }
        else
        {
            _largestHitLabel!.Text = "Biggest Hit: -";
        }

        _abilityCastsLabel!.Text = $"Abilities Cast: {stats.TotalAbilityCasts}";
        _favoriteAbilityLabel!.Text = string.IsNullOrEmpty(stats.FavoriteAbility)
            ? "Most Used: -"
            : $"Most Used: {stats.FavoriteAbility}";

        _mostKilledLabel!.Text = string.IsNullOrEmpty(stats.MostKilledMonster)
            ? "Most Killed: -"
            : $"Most Killed: {stats.MostKilledMonster}";

        // Update kill breakdown
        RefreshKillBreakdown(stats);
    }

    private void RefreshKillBreakdown(GameStats stats)
    {
        // Clear existing
        foreach (var child in _killBreakdownContainer!.GetChildren())
        {
            child.QueueFree();
        }

        var topKills = stats.GetTopKills(10);
        if (topKills.Count == 0)
        {
            var placeholder = new Label();
            placeholder.Text = "No kills yet...";
            placeholder.AddThemeFontSizeOverride("font_size", 11);
            placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.45f, 0.6f));
            _killBreakdownContainer.AddChild(placeholder);
            return;
        }

        // Get max for bar scaling
        int maxKills = topKills[0].count;

        foreach (var (monsterType, count) in topKills)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            // Monster name
            var nameLabel = new Label();
            nameLabel.Text = FormatMonsterName(monsterType);
            nameLabel.CustomMinimumSize = new Vector2(100, 0);
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.75f, 0.9f));
            row.AddChild(nameLabel);

            // Bar background
            var barBg = new ColorRect();
            barBg.CustomMinimumSize = new Vector2(120, 14);
            barBg.Color = new Color(0.15f, 0.12f, 0.2f);
            row.AddChild(barBg);

            // Bar fill (overlay)
            var barFill = new ColorRect();
            float fillWidth = (float)count / maxKills * 120f;
            barFill.CustomMinimumSize = new Vector2(fillWidth, 14);
            barFill.Color = GetMonsterColor(monsterType);
            barFill.Position = barBg.Position;
            barBg.AddChild(barFill);

            // Count label
            var countLabel = new Label();
            countLabel.Text = count.ToString();
            countLabel.CustomMinimumSize = new Vector2(35, 0);
            countLabel.HorizontalAlignment = HorizontalAlignment.Right;
            countLabel.AddThemeFontSizeOverride("font_size", 11);
            countLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.95f));
            row.AddChild(countLabel);

            _killBreakdownContainer.AddChild(row);
        }
    }

    private static string FormatMonsterName(string monsterType)
    {
        // Convert snake_case or PascalCase to Title Case with spaces
        var result = "";
        for (int i = 0; i < monsterType.Length; i++)
        {
            char c = monsterType[i];
            if (c == '_')
            {
                result += " ";
            }
            else if (i > 0 && char.IsUpper(c) && !char.IsUpper(monsterType[i - 1]))
            {
                result += " " + c;
            }
            else
            {
                result += i == 0 || result.EndsWith(" ") ? char.ToUpper(c) : c;
            }
        }
        return result.Length > 14 ? result.Substring(0, 12) + ".." : result;
    }

    private static Color GetMonsterColor(string monsterType)
    {
        // Color-code by monster type for visual variety
        string lower = monsterType.ToLower();
        if (lower.Contains("boss") || lower.Contains("lord")) return new Color(1f, 0.4f, 0.2f);
        if (lower.Contains("goblin")) return new Color(0.4f, 0.8f, 0.3f);
        if (lower.Contains("skeleton") || lower.Contains("undead")) return new Color(0.9f, 0.9f, 0.8f);
        if (lower.Contains("rat")) return new Color(0.6f, 0.5f, 0.4f);
        if (lower.Contains("spider")) return new Color(0.3f, 0.3f, 0.35f);
        if (lower.Contains("slime") || lower.Contains("ooze")) return new Color(0.3f, 0.9f, 0.5f);
        if (lower.Contains("demon") || lower.Contains("imp")) return new Color(0.9f, 0.2f, 0.3f);
        if (lower.Contains("ghost") || lower.Contains("wraith")) return new Color(0.6f, 0.7f, 0.9f);
        return new Color(0.5f, 0.6f, 0.8f); // Default blue-ish
    }

    private void CreateHeader(VBoxContainer parent)
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        header.CustomMinimumSize = new Vector2(0, 40);
        header.MouseFilter = MouseFilterEnum.Stop;
        header.GuiInput += OnHeaderGuiInput;

        _titleLabel = new Label();
        _titleLabel.Text = "Character Sheet (drag to move)";
        _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.7f));
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        header.AddChild(_titleLabel);

        _closeButton = new Button();
        _closeButton.Text = "X";
        _closeButton.CustomMinimumSize = new Vector2(32, 32);
        _closeButton.Pressed += () => Hide();
        header.AddChild(_closeButton);

        parent.AddChild(header);
    }

    private void OnHeaderGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _isDragging = true;
                _dragOffset = GetGlobalMousePosition() - _mainPanel!.GlobalPosition;
            }
            else
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    WindowPositionManager.SetPosition(WindowName, _mainPanel!.Position);
                }
            }
        }
        else if (@event is InputEventMouseMotion && _isDragging && _mainPanel != null)
        {
            var newPos = GetGlobalMousePosition() - _dragOffset;
            var viewportSize = GetViewportRect().Size;
            var panelSize = _mainPanel.Size;
            _mainPanel.Position = WindowPositionManager.ClampToViewport(newPos, viewportSize, panelSize);
        }
    }

    private void CreateStatsPanel(VBoxContainer parent)
    {
        // Section title
        var statsTitle = new Label();
        statsTitle.Text = "Attributes";
        statsTitle.AddThemeFontSizeOverride("font_size", 18);
        statsTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.6f));
        parent.AddChild(statsTitle);

        // Level
        _levelLabel = CreateStatLabel(parent, "Level: 1");
        _attributePointsLabel = CreateStatLabel(parent, "Attribute Points: 0", new Color(0.4f, 1f, 0.4f));

        parent.AddChild(new HSeparator());

        // Primary attributes with + buttons and tooltips
        CreateAttributeRow(parent, "Strength", out _strengthLabel, out _strButton, "str",
            "STRENGTH",
            "+2% Physical Damage per point\nIncreases melee attack power");
        CreateAttributeRow(parent, "Dexterity", out _dexterityLabel, out _dexButton, "dex",
            "DEXTERITY",
            "+1% Attack Speed per point\n+0.5% Critical Chance per point\n+0.5% Dodge Chance per point");
        CreateAttributeRow(parent, "Vitality", out _vitalityLabel, out _vitButton, "vit",
            "VITALITY",
            "+10 Max Health per point\n+0.5 Health Regen/sec per point");
        CreateAttributeRow(parent, "Energy", out _energyLabel, out _eneButton, "ene",
            "ENERGY",
            "+5 Max Mana per point\n+0.2 Mana Regen/sec per point");

        parent.AddChild(new HSeparator());

        // Derived stats title
        var derivedTitle = new Label();
        derivedTitle.Text = "Combat Stats";
        derivedTitle.AddThemeFontSizeOverride("font_size", 18);
        derivedTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.6f));
        parent.AddChild(derivedTitle);

        // Derived stats with formula tooltips
        _healthLabel = CreateCombatStatLabel(parent, "Health: 100/100", "health", new Color(0.9f, 0.3f, 0.3f));
        _manaLabel = CreateCombatStatLabel(parent, "Mana: 50/50", "mana", new Color(0.3f, 0.5f, 0.9f));
        _damageLabel = CreateCombatStatLabel(parent, "Damage: 10-20", "damage");
        _armorLabel = CreateCombatStatLabel(parent, "Armor: 0", "armor");
        _critLabel = CreateCombatStatLabel(parent, "Crit Chance: 5%", "crit", new Color(1f, 0.8f, 0.3f));
        _blockLabel = CreateCombatStatLabel(parent, "Block Chance: 0%", "block");
        _dodgeLabel = CreateCombatStatLabel(parent, "Dodge Chance: 0%", "dodge");
    }

    private Label CreateStatLabel(VBoxContainer parent, string text, Color? color = null)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", color ?? new Color(0.85f, 0.82f, 0.9f));
        parent.AddChild(label);
        return label;
    }

    private void CreateAttributeRow(VBoxContainer parent, string name, out Label valueLabel, out Button plusButton, string attrKey,
        string tooltipTitle = "", string tooltipDesc = "")
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.MouseFilter = MouseFilterEnum.Stop;

        // Add mouse enter/exit for tooltip
        if (!string.IsNullOrEmpty(tooltipTitle))
        {
            row.MouseEntered += () => ShowTextTooltip(row, tooltipTitle, tooltipDesc, new Color(0.9f, 0.8f, 0.5f));
            row.MouseExited += () => HideTooltip();
        }

        var nameLabel = new Label();
        nameLabel.Text = $"{name}:";
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.82f, 0.9f));
        nameLabel.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(nameLabel);

        valueLabel = new Label();
        valueLabel.Text = "10";
        valueLabel.CustomMinimumSize = new Vector2(40, 0);
        valueLabel.AddThemeFontSizeOverride("font_size", 14);
        valueLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        valueLabel.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(valueLabel);

        plusButton = new Button();
        plusButton.Text = "+";
        plusButton.CustomMinimumSize = new Vector2(28, 24);
        plusButton.AddThemeFontSizeOverride("font_size", 16);
        plusButton.Pressed += () => OnAttributeButtonPressed(attrKey);
        row.AddChild(plusButton);

        parent.AddChild(row);
    }

    /// <summary>
    /// Create a combat stat label with formula tooltip on hover.
    /// </summary>
    private Label CreateCombatStatLabel(VBoxContainer parent, string text, string statKey, Color? color = null)
    {
        var container = new Control();
        container.CustomMinimumSize = new Vector2(0, 20);
        container.MouseFilter = MouseFilterEnum.Stop;
        container.MouseEntered += () => ShowStatFormulaTooltip(container, statKey);
        container.MouseExited += () => HideTooltip();

        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", color ?? new Color(0.85f, 0.82f, 0.9f));
        label.MouseFilter = MouseFilterEnum.Ignore;
        container.AddChild(label);

        parent.AddChild(container);
        return label;
    }

    /// <summary>
    /// Show a simple text tooltip with title and description.
    /// </summary>
    private void ShowTextTooltip(Control source, string title, string description, Color titleColor)
    {
        if (_tooltipPanel == null || _tooltipContent == null) return;

        foreach (var child in _tooltipContent.GetChildren())
            child.QueueFree();

        var titleLabel = new Label();
        titleLabel.Text = title;
        titleLabel.AddThemeFontSizeOverride("font_size", 14);
        titleLabel.AddThemeColorOverride("font_color", titleColor);
        _tooltipContent.AddChild(titleLabel);

        _tooltipContent.AddChild(new HSeparator());

        var descLabel = new Label();
        descLabel.Text = description;
        descLabel.AddThemeFontSizeOverride("font_size", 11);
        descLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.9f, 0.75f));
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.CustomMinimumSize = new Vector2(200, 0);
        _tooltipContent.AddChild(descLabel);

        _tooltipPanel.Visible = true;
        _tooltipPanel.GlobalPosition = source.GlobalPosition + new Vector2(source.Size.X + 10, 0);
    }

    /// <summary>
    /// Show a combat stat formula tooltip with breakdown.
    /// </summary>
    private void ShowStatFormulaTooltip(Control source, string statKey)
    {
        var player = FPSController.Instance;
        if (player == null || _tooltipPanel == null || _tooltipContent == null) return;

        var stats = player.Stats;
        if (stats == null) return;

        foreach (var child in _tooltipContent.GetChildren())
            child.QueueFree();

        string title = "";
        string formula = "";
        string breakdown = "";

        switch (statKey)
        {
            case "health":
                title = "MAX HEALTH";
                formula = "Base + (VIT × 10) + (Level × 15) + Flat + %Bonus";
                int baseHp = 100 + (stats.Vitality * 10) + (stats.Level * 15);
                breakdown = $"100 (base)\n+ {stats.Vitality * 10} (VIT {stats.Vitality} × 10)\n+ {stats.Level * 15} (Level {stats.Level} × 15)\n= {stats.MaxHealth}";
                break;

            case "mana":
                title = "MAX MANA";
                formula = "Base + (ENE × 5) + (Level × 5) + Flat + %Bonus";
                int baseMp = 50 + (stats.Energy * 5) + (stats.Level * 5);
                breakdown = $"50 (base)\n+ {stats.Energy * 5} (ENE {stats.Energy} × 5)\n+ {stats.Level * 5} (Level {stats.Level} × 5)\n= {stats.MaxMana}";
                break;

            case "damage":
                title = "PHYSICAL DAMAGE";
                formula = "(Weapon + Flat) × STR Bonus × %Bonus";
                float strBonus = 1f + (stats.Strength * 0.02f);
                var weapon = player.Equipment.GetEquippedItem(EquipmentSlot.MainHand);
                int wepMin = weapon?.MinDamage ?? 1;
                int wepMax = weapon?.MaxDamage ?? 5;
                breakdown = $"Weapon: {wepMin}-{wepMax}\n× {strBonus:F2} (STR {stats.Strength} × 2%)\n= {stats.MinDamage}-{stats.MaxDamage}";
                break;

            case "armor":
                title = "ARMOR & DAMAGE REDUCTION";
                formula = "Reduction = Armor ÷ (Armor + 100 + Level × 10)";
                float divisor = stats.Armor + 100f + (stats.Level * 10f);
                breakdown = $"Armor: {stats.Armor}\nDivisor: {stats.Armor} + 100 + {stats.Level * 10}\n= {stats.DamageReduction:P1} reduction\n(Capped at 75%)";
                break;

            case "crit":
                title = "CRITICAL STRIKE";
                formula = "Base 5% + (DEX × 0.5%) + Equipment";
                float dexCrit = stats.Dexterity * 0.5f;
                breakdown = $"5% (base)\n+ {dexCrit:F1}% (DEX {stats.Dexterity} × 0.5%)\n= {stats.CriticalChance:P1}\n\nCrit Damage: {stats.CriticalDamage:P0}";
                break;

            case "block":
                title = "BLOCK CHANCE";
                formula = "Shield Block% (Capped at 50%)";
                breakdown = $"From shield: {stats.BlockChance:P1}\nBlock Amount: {stats.BlockAmount}\n\nBlocking reduces damage by 50%\nafter subtracting Block Amount";
                break;

            case "dodge":
                title = "DODGE CHANCE";
                formula = "DEX × 0.5% + Equipment (Capped at 30%)";
                float dexDodge = stats.Dexterity * 0.5f;
                breakdown = $"{dexDodge:F1}% (DEX {stats.Dexterity} × 0.5%)\n= {stats.DodgeChance:P1}\n\nDodge completely avoids damage";
                break;
        }

        // Title
        var titleLabel = new Label();
        titleLabel.Text = title;
        titleLabel.AddThemeFontSizeOverride("font_size", 14);
        titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
        _tooltipContent.AddChild(titleLabel);

        // Formula
        var formulaLabel = new Label();
        formulaLabel.Text = formula;
        formulaLabel.AddThemeFontSizeOverride("font_size", 10);
        formulaLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1f));
        formulaLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        formulaLabel.CustomMinimumSize = new Vector2(220, 0);
        _tooltipContent.AddChild(formulaLabel);

        _tooltipContent.AddChild(new HSeparator());

        // Breakdown
        var breakdownLabel = new Label();
        breakdownLabel.Text = breakdown;
        breakdownLabel.AddThemeFontSizeOverride("font_size", 11);
        breakdownLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.95f, 0.85f));
        breakdownLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        breakdownLabel.CustomMinimumSize = new Vector2(220, 0);
        _tooltipContent.AddChild(breakdownLabel);

        _tooltipPanel.Visible = true;
        _tooltipPanel.GlobalPosition = source.GlobalPosition + new Vector2(source.Size.X + 10, 0);
    }

    private void OnAttributeButtonPressed(string attr)
    {
        var player = FPSController.Instance;
        if (player == null || player.Stats == null) return;

        bool success = attr switch
        {
            "str" => player.Stats.SpendAttributePoint("strength"),
            "dex" => player.Stats.SpendAttributePoint("dexterity"),
            "vit" => player.Stats.SpendAttributePoint("vitality"),
            "ene" => player.Stats.SpendAttributePoint("energy"),
            _ => false
        };

        if (success)
        {
            RefreshStats();
            SoundManager3D.Instance?.PlaySound("menu_select");
        }
    }

    private void CreateEquipmentPanel(VBoxContainer parent)
    {
        var equipTitle = new Label();
        equipTitle.Text = "Equipment";
        equipTitle.AddThemeFontSizeOverride("font_size", 18);
        equipTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.6f));
        parent.AddChild(equipTitle);

        // Equipment layout - grid arrangement
        var equipGrid = new GridContainer();
        equipGrid.Columns = 3;
        equipGrid.AddThemeConstantOverride("h_separation", 8);
        equipGrid.AddThemeConstantOverride("v_separation", 8);
        parent.AddChild(equipGrid);

        // Row 1: empty, Head, empty
        equipGrid.AddChild(CreateEmptySlot());
        CreateEquipmentSlot(equipGrid, EquipmentSlot.Head, "Head");
        equipGrid.AddChild(CreateEmptySlot());

        // Row 2: MainHand, Chest, OffHand
        CreateEquipmentSlot(equipGrid, EquipmentSlot.MainHand, "Weapon");
        CreateEquipmentSlot(equipGrid, EquipmentSlot.Chest, "Chest");
        CreateEquipmentSlot(equipGrid, EquipmentSlot.OffHand, "Off Hand");

        // Row 3: Ring1, Hands, Ring2
        CreateEquipmentSlot(equipGrid, EquipmentSlot.Ring1, "Ring");
        CreateEquipmentSlot(equipGrid, EquipmentSlot.Hands, "Hands");
        CreateEquipmentSlot(equipGrid, EquipmentSlot.Ring2, "Ring");

        // Row 4: empty, Feet, empty
        equipGrid.AddChild(CreateEmptySlot());
        CreateEquipmentSlot(equipGrid, EquipmentSlot.Feet, "Feet");
        equipGrid.AddChild(CreateEmptySlot());

        // Instructions
        var instructions = new Label();
        instructions.Text = "Right-click equipment to unequip\nDrag from inventory to equip";
        instructions.AddThemeFontSizeOverride("font_size", 11);
        instructions.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.7f));
        instructions.HorizontalAlignment = HorizontalAlignment.Center;
        parent.AddChild(instructions);
    }

    private Control CreateEmptySlot()
    {
        var empty = new Control();
        empty.CustomMinimumSize = new Vector2(EquipSlotSize, EquipSlotSize);
        return empty;
    }

    private void CreateEquipmentSlot(GridContainer parent, EquipmentSlot slot, string label)
    {
        var slotUI = new EquipmentSlotUI();
        slotUI.Slot = slot;

        // Container
        slotUI.Container = new PanelContainer();
        slotUI.Container.CustomMinimumSize = new Vector2(EquipSlotSize, EquipSlotSize);

        slotUI.Background = new StyleBoxFlat();
        slotUI.Background.BgColor = new Color(0.15f, 0.12f, 0.2f, 0.9f);
        slotUI.Background.BorderColor = new Color(0.4f, 0.35f, 0.5f);
        slotUI.Background.SetBorderWidthAll(2);
        slotUI.Background.SetCornerRadiusAll(4);
        slotUI.Container.AddThemeStyleboxOverride("panel", slotUI.Background);

        // Content layout
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        slotUI.Container.AddChild(vbox);

        // Slot label
        slotUI.SlotLabel = new Label();
        slotUI.SlotLabel.Text = label;
        slotUI.SlotLabel.AddThemeFontSizeOverride("font_size", 9);
        slotUI.SlotLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.45f, 0.6f));
        slotUI.SlotLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(slotUI.SlotLabel);

        // Item name (when equipped)
        slotUI.ItemLabel = new Label();
        slotUI.ItemLabel.Text = "";
        slotUI.ItemLabel.AddThemeFontSizeOverride("font_size", 10);
        slotUI.ItemLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.8f, 0.9f));
        slotUI.ItemLabel.HorizontalAlignment = HorizontalAlignment.Center;
        slotUI.ItemLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        slotUI.ItemLabel.CustomMinimumSize = new Vector2(EquipSlotSize - 8, 0);
        vbox.AddChild(slotUI.ItemLabel);

        // Click handler
        slotUI.Container.GuiInput += (e) => OnEquipmentSlotInput(e, slotUI);

        // Hide tooltip when mouse exits the slot
        slotUI.Container.MouseExited += () => HideTooltip();

        parent.AddChild(slotUI.Container);
        _equipmentSlots[slot] = slotUI;
    }

    private void OnEquipmentSlotInput(InputEvent e, EquipmentSlotUI slotUI)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            var player = FPSController.Instance;
            if (player == null) return;

            var equippedItem = player.Equipment.GetEquippedItem(slotUI.Slot);

            if (mb.ButtonIndex == MouseButton.Left && equippedItem != null)
            {
                // Show tooltip
                ShowEquipmentTooltip(slotUI, equippedItem);
            }
            else if (mb.ButtonIndex == MouseButton.Right && equippedItem != null)
            {
                // Unequip item
                var unequipped = player.Equipment.Unequip(slotUI.Slot);
                if (unequipped != null)
                {
                    // Add to inventory
                    var invItem = InventoryItem.FromEquipment(unequipped);
                    if (Inventory3D.Instance?.AddItem(invItem) == true)
                    {
                        HUD3D.Instance?.AddCombatLogMessage($"Unequipped {unequipped.Name}", new Color(0.7f, 0.7f, 0.9f));
                        SoundManager3D.Instance?.PlaySound("menu_select");
                    }
                    else
                    {
                        // Re-equip if inventory full
                        player.Equipment.Equip(unequipped);
                        HUD3D.Instance?.AddCombatLogMessage("Inventory full!", new Color(1f, 0.4f, 0.4f));
                    }
                }
                RefreshAll();
            }
        }
        else if (e is InputEventMouseMotion)
        {
            var player = FPSController.Instance;
            var equippedItem = player?.Equipment.GetEquippedItem(slotUI.Slot);
            if (equippedItem != null)
            {
                ShowEquipmentTooltip(slotUI, equippedItem);
            }
            else
            {
                HideTooltip();
            }
        }
    }

    private void CreateTooltip()
    {
        _tooltipPanel = new PanelContainer();
        _tooltipPanel.CustomMinimumSize = new Vector2(250, 100);
        _tooltipPanel.Visible = false;
        _tooltipPanel.ZIndex = 20;
        _tooltipPanel.MouseFilter = MouseFilterEnum.Ignore;

        var tooltipStyle = new StyleBoxFlat();
        tooltipStyle.BgColor = new Color(0.08f, 0.06f, 0.12f, 0.98f);
        tooltipStyle.BorderColor = new Color(0.6f, 0.5f, 0.7f);
        tooltipStyle.SetBorderWidthAll(2);
        tooltipStyle.SetCornerRadiusAll(6);
        tooltipStyle.SetContentMarginAll(10);
        _tooltipPanel.AddThemeStyleboxOverride("panel", tooltipStyle);

        _tooltipContent = new VBoxContainer();
        _tooltipContent.AddThemeConstantOverride("separation", 4);
        _tooltipContent.MouseFilter = MouseFilterEnum.Ignore;
        _tooltipPanel.AddChild(_tooltipContent);

        AddChild(_tooltipPanel);
    }

    private void ShowEquipmentTooltip(EquipmentSlotUI slotUI, EquipmentItem item)
    {
        if (_tooltipPanel == null || _tooltipContent == null) return;

        // Clear previous content
        foreach (var child in _tooltipContent.GetChildren())
        {
            child.QueueFree();
        }

        // Item name with rarity color
        var nameLabel = new Label();
        nameLabel.Text = item.Name;
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", GetRarityColor(item.Rarity));
        _tooltipContent.AddChild(nameLabel);

        // Item type and level
        var typeLabel = new Label();
        typeLabel.Text = $"{item.Slot} - Item Level {item.ItemLevel}";
        typeLabel.AddThemeFontSizeOverride("font_size", 11);
        typeLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.7f));
        _tooltipContent.AddChild(typeLabel);

        // Base stats
        if (item.MinDamage > 0 || item.MaxDamage > 0)
        {
            var dmgLabel = new Label();
            dmgLabel.Text = $"Damage: {item.MinDamage}-{item.MaxDamage}";
            dmgLabel.AddThemeFontSizeOverride("font_size", 12);
            dmgLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.8f));
            _tooltipContent.AddChild(dmgLabel);
        }

        if (item.Armor > 0)
        {
            var armorLabel = new Label();
            armorLabel.Text = $"Armor: {item.Armor}";
            armorLabel.AddThemeFontSizeOverride("font_size", 12);
            armorLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.8f));
            _tooltipContent.AddChild(armorLabel);
        }

        if (item.BlockChance > 0)
        {
            var blockLabel = new Label();
            blockLabel.Text = $"Block Chance: {item.BlockChance}%";
            blockLabel.AddThemeFontSizeOverride("font_size", 12);
            blockLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.8f));
            _tooltipContent.AddChild(blockLabel);
        }

        // Affixes
        if (item.Affixes.Count > 0)
        {
            _tooltipContent.AddChild(new HSeparator());
            foreach (var affix in item.Affixes)
            {
                var affixLabel = new Label();
                affixLabel.Text = $"+{affix.CurrentValue:F0} {FormatAffixName(affix.Type)}";
                affixLabel.AddThemeFontSizeOverride("font_size", 11);
                affixLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1f)); // Blue for magic stats
                _tooltipContent.AddChild(affixLabel);
            }
        }

        // Required level
        if (item.RequiredLevel > 1)
        {
            var reqLabel = new Label();
            reqLabel.Text = $"Required Level: {item.RequiredLevel}";
            reqLabel.AddThemeFontSizeOverride("font_size", 10);
            reqLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 0.6f));
            _tooltipContent.AddChild(reqLabel);
        }

        _tooltipPanel.Visible = true;

        // Position tooltip near the slot
        var slotPos = slotUI.Container!.GlobalPosition;
        _tooltipPanel.GlobalPosition = new Vector2(slotPos.X + EquipSlotSize + 10, slotPos.Y);
    }

    private static Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Normal => new Color(0.8f, 0.8f, 0.8f),
            ItemRarity.Magic => new Color(0.4f, 0.6f, 1.0f),
            ItemRarity.Rare => new Color(1.0f, 1.0f, 0.4f),
            ItemRarity.Unique => new Color(0.8f, 0.5f, 0.2f),
            ItemRarity.Set => new Color(0.3f, 1.0f, 0.3f),
            _ => new Color(0.8f, 0.8f, 0.8f)
        };
    }

    private static string FormatAffixName(AffixType type)
    {
        return type switch
        {
            AffixType.Strength => "Strength",
            AffixType.Dexterity => "Dexterity",
            AffixType.Vitality => "Vitality",
            AffixType.Energy => "Energy",
            AffixType.FlatDamage => "Damage",
            AffixType.DamagePercent => "% Damage",
            AffixType.FlatHealth => "Health",
            AffixType.HealthPercent => "% Health",
            AffixType.FlatMana => "Mana",
            AffixType.ManaPercent => "% Mana",
            AffixType.FlatArmor => "Armor",
            AffixType.ArmorPercent => "% Armor",
            AffixType.CriticalChance => "% Critical Chance",
            AffixType.CriticalDamage => "% Critical Damage",
            AffixType.AttackSpeed => "% Attack Speed",
            AffixType.MovementSpeed => "% Movement Speed",
            AffixType.DodgeChance => "% Dodge",
            AffixType.BlockChance => "% Block",
            AffixType.BlockAmount => "Block Amount",
            AffixType.HealthRegen => "Health/sec",
            AffixType.ManaRegen => "Mana/sec",
            AffixType.LifeOnHit => "Life on Hit",
            AffixType.ManaOnHit => "Mana on Hit",
            AffixType.MagicFind => "% Magic Find",
            AffixType.GoldFind => "% Gold Find",
            AffixType.ExperienceBonus => "% Experience",
            AffixType.FireDamage => "Fire Damage",
            AffixType.ColdDamage => "Cold Damage",
            AffixType.LightningDamage => "Lightning Damage",
            AffixType.PoisonDamage => "Poison Damage",
            AffixType.FireResistance => "% Fire Resist",
            AffixType.ColdResistance => "% Cold Resist",
            AffixType.LightningResistance => "% Lightning Resist",
            AffixType.PoisonResistance => "% Poison Resist",
            AffixType.AllResistance => "% All Resistances",
            AffixType.DamageReduction => "% Damage Reduction",
            _ => type.ToString()
        };
    }

    private void HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.Visible = false;
    }

    private void RefreshAll()
    {
        RefreshStats();
        RefreshEquipment();
    }

    private void RefreshStats()
    {
        var player = FPSController.Instance;
        if (player == null) return;

        var stats = player.Stats;
        if (stats == null) return;

        _levelLabel!.Text = $"Level: {stats.Level}";
        _attributePointsLabel!.Text = $"Attribute Points: {stats.UnspentAttributePoints}";

        // Update + button visibility
        bool hasPoints = stats.UnspentAttributePoints > 0;
        _strButton!.Visible = hasPoints;
        _dexButton!.Visible = hasPoints;
        _vitButton!.Visible = hasPoints;
        _eneButton!.Visible = hasPoints;

        // Primary attributes
        _strengthLabel!.Text = stats.Strength.ToString();
        _dexterityLabel!.Text = stats.Dexterity.ToString();
        _vitalityLabel!.Text = stats.Vitality.ToString();
        _energyLabel!.Text = stats.Energy.ToString();

        // Derived stats
        _healthLabel!.Text = $"Health: {player.CurrentHealth}/{stats.MaxHealth}";
        _manaLabel!.Text = $"Mana: {player.CurrentMana}/{stats.MaxMana}";
        _damageLabel!.Text = $"Damage: {stats.MinDamage}-{stats.MaxDamage}";
        _armorLabel!.Text = $"Armor: {stats.Armor} ({stats.DamageReduction:F1}% reduction)";
        _critLabel!.Text = $"Crit Chance: {stats.CriticalChance * 100:F1}%";
        _blockLabel!.Text = $"Block Chance: {stats.BlockChance:F1}%";
        _dodgeLabel!.Text = $"Dodge Chance: {stats.DodgeChance * 100:F1}%";
    }

    private void RefreshEquipment()
    {
        var player = FPSController.Instance;
        if (player == null) return;

        foreach (var kvp in _equipmentSlots)
        {
            var slot = kvp.Key;
            var slotUI = kvp.Value;
            var item = player.Equipment.GetEquippedItem(slot);

            if (item != null)
            {
                slotUI.ItemLabel!.Text = TruncateName(item.Name, 12);
                slotUI.Background!.BorderColor = GetRarityColor(item.Rarity);
            }
            else
            {
                slotUI.ItemLabel!.Text = "";
                slotUI.Background!.BorderColor = new Color(0.4f, 0.35f, 0.5f);
            }
        }
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 2) + "..";
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        // Stop drag if mouse released anywhere
        if (_isDragging && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
        {
            _isDragging = false;
            if (_mainPanel != null)
            {
                WindowPositionManager.SetPosition(WindowName, _mainPanel.Position);
            }
        }

        if (@event.IsActionPressed("escape"))
        {
            Hide();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        // Position is set in Show() and during drag, no per-frame update needed
    }

    public new void Show()
    {
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        RefreshAll();

        // Restore or center position
        if (_mainPanel != null)
        {
            var viewportSize = GetViewportRect().Size;
            var storedPos = WindowPositionManager.GetPosition(WindowName);
            if (storedPos == WindowPositionManager.CenterMarker)
            {
                _mainPanel.Position = WindowPositionManager.GetCenteredPosition(viewportSize, new Vector2(PanelWidth, PanelHeight));
            }
            else
            {
                _mainPanel.Position = WindowPositionManager.ClampToViewport(storedPos, viewportSize, new Vector2(PanelWidth, PanelHeight));
            }
        }
        GD.Print("[CharacterSheetUI] Opened");
    }

    public new void Hide()
    {
        Visible = false;
        HideTooltip();

        // Only capture mouse if no other UI windows are open
        bool otherUIOpen = (InventoryUI3D.Instance?.Visible == true) ||
                           (SpellBook3D.Instance?.Visible == true) ||
                           (LootUI3D.Instance?.Visible == true);
        if (!otherUIOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        GD.Print("[CharacterSheetUI] Closed");
    }

    public void Toggle()
    {
        if (Visible)
            Hide();
        else
            Show();
    }

    /// <summary>
    /// Try to equip an item from the inventory.
    /// </summary>
    public bool TryEquipFromInventory(InventoryItem item, int inventorySlot)
    {
        if (item.Equipment == null) return false;

        var player = FPSController.Instance;
        if (player == null) return false;

        if (!player.Equipment.CanEquip(item.Equipment))
        {
            HUD3D.Instance?.AddCombatLogMessage($"Cannot equip {item.Name}", new Color(1f, 0.4f, 0.4f));
            return false;
        }

        // Get any item that will be replaced
        var previousItem = player.Equipment.Equip(item.Equipment);

        // Remove equipped item from inventory
        Inventory3D.Instance?.SetItem(inventorySlot, null);

        // If there was a previous item, add it to inventory
        if (previousItem != null)
        {
            var prevInvItem = InventoryItem.FromEquipment(previousItem);
            Inventory3D.Instance?.AddItem(prevInvItem);
        }

        HUD3D.Instance?.AddCombatLogMessage($"Equipped {item.Name}", new Color(0.3f, 1f, 0.5f));
        SoundManager3D.Instance?.PlaySound("menu_select");
        RefreshAll();
        return true;
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}

/// <summary>
/// Data for an equipment slot UI element.
/// </summary>
public class EquipmentSlotUI
{
    public EquipmentSlot Slot { get; set; }
    public PanelContainer? Container { get; set; }
    public StyleBoxFlat? Background { get; set; }
    public Label? SlotLabel { get; set; }
    public Label? ItemLabel { get; set; }
}
