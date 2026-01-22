using Godot;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Abilities;
using SafeRoom3D.Player;

namespace SafeRoom3D.UI;

/// <summary>
/// UI that appears on level up, letting the player:
/// 1. Distribute attribute points (Strength, Dexterity, Vitality, Energy)
/// 2. Choose one spell OR one ability to unlock
/// </summary>
public partial class LevelUpChoiceUI3D : CanvasLayer
{
    public static LevelUpChoiceUI3D? Instance { get; private set; }

    // UI Elements
    private PanelContainer? _mainPanel;
    private Label? _titleLabel;
    private Label? _attributePointsLabel;
    private VBoxContainer? _spellsColumn;
    private VBoxContainer? _abilitiesColumn;
    private Button? _confirmButton;

    // Attribute controls
    private Label? _strValueLabel;
    private Label? _dexValueLabel;
    private Label? _vitValueLabel;
    private Label? _eneValueLabel;
    private Button? _strPlusButton;
    private Button? _dexPlusButton;
    private Button? _vitPlusButton;
    private Button? _enePlusButton;
    private Button? _strMinusButton;
    private Button? _dexMinusButton;
    private Button? _vitMinusButton;
    private Button? _eneMinusButton;

    // State
    private int _currentLevel;
    private int _pendingAttributePoints;
    private int _pendingStr, _pendingDex, _pendingVit, _pendingEne;
    private List<Ability3D> _availableSpells = new();
    private List<Ability3D> _availableAbilities = new();
    private Dictionary<string, Button> _choiceButtons = new();
    private string? _selectedAbilityId;

    // Visual settings
    private const float PanelWidth = 850f;
    private const float PanelHeight = 620f;

    public override void _Ready()
    {
        Instance = this;
        Layer = 150;
        ProcessMode = ProcessModeEnum.Always;

        BuildUI();
        Hide();

        // Subscribe to level up event
        if (FPSController.Instance != null)
        {
            FPSController.Instance.LeveledUp += OnLeveledUp;
        }

        GD.Print("[LevelUpChoiceUI3D] Ready");
    }

    private void BuildUI()
    {
        // Dark overlay background
        var overlay = new ColorRect();
        overlay.Color = new Color(0, 0, 0, 0.8f);
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);

        // Main panel centered
        _mainPanel = new PanelContainer();
        _mainPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _mainPanel.Position = new Vector2(-PanelWidth / 2, -PanelHeight / 2);
        _mainPanel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.06f, 0.06f, 0.1f, 0.98f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.8f, 0.7f, 0.3f);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetContentMarginAll(20);
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_mainPanel);

        var mainVBox = new VBoxContainer();
        mainVBox.AddThemeConstantOverride("separation", 12);
        _mainPanel.AddChild(mainVBox);

        // Title
        _titleLabel = new Label();
        _titleLabel.Text = "LEVEL UP!";
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", 32);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        mainVBox.AddChild(_titleLabel);

        mainVBox.AddChild(new HSeparator());

        // === ATTRIBUTE SECTION ===
        CreateAttributeSection(mainVBox);

        mainVBox.AddChild(new HSeparator());

        // === SPELL/ABILITY CHOICE SECTION ===
        var choiceLabel = new Label();
        choiceLabel.Text = "Choose ONE Spell or Ability to Unlock:";
        choiceLabel.HorizontalAlignment = HorizontalAlignment.Center;
        choiceLabel.AddThemeFontSizeOverride("font_size", 16);
        choiceLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        mainVBox.AddChild(choiceLabel);

        // Two-column layout
        var columnsHBox = new HBoxContainer();
        columnsHBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        columnsHBox.AddThemeConstantOverride("separation", 20);
        mainVBox.AddChild(columnsHBox);

        // Spells column
        var spellsContainer = CreateColumn("SPELLS", new Color(0.3f, 0.5f, 1f), out _spellsColumn);
        columnsHBox.AddChild(spellsContainer);

        // Abilities column
        var abilitiesContainer = CreateColumn("ABILITIES", new Color(0.3f, 0.8f, 0.4f), out _abilitiesColumn);
        columnsHBox.AddChild(abilitiesContainer);

        // Confirm button
        var bottomHBox = new HBoxContainer();
        bottomHBox.Alignment = BoxContainer.AlignmentMode.Center;
        mainVBox.AddChild(bottomHBox);

        _confirmButton = new Button();
        _confirmButton.Text = "Confirm & Continue";
        _confirmButton.CustomMinimumSize = new Vector2(220, 45);
        _confirmButton.Pressed += OnConfirmPressed;

        var confirmStyle = new StyleBoxFlat();
        confirmStyle.BgColor = new Color(0.2f, 0.5f, 0.3f);
        confirmStyle.SetCornerRadiusAll(8);
        confirmStyle.SetBorderWidthAll(2);
        confirmStyle.BorderColor = new Color(0.4f, 0.8f, 0.5f);
        _confirmButton.AddThemeStyleboxOverride("normal", confirmStyle);

        var confirmHoverStyle = new StyleBoxFlat();
        confirmHoverStyle.BgColor = new Color(0.3f, 0.6f, 0.4f);
        confirmHoverStyle.SetCornerRadiusAll(8);
        confirmHoverStyle.SetBorderWidthAll(2);
        confirmHoverStyle.BorderColor = new Color(0.5f, 0.9f, 0.6f);
        _confirmButton.AddThemeStyleboxOverride("hover", confirmHoverStyle);
        _confirmButton.AddThemeFontSizeOverride("font_size", 16);
        bottomHBox.AddChild(_confirmButton);
    }

    private void CreateAttributeSection(VBoxContainer parent)
    {
        // Attribute points header
        var attrHeader = new HBoxContainer();
        attrHeader.Alignment = BoxContainer.AlignmentMode.Center;
        parent.AddChild(attrHeader);

        var attrTitle = new Label();
        attrTitle.Text = "Distribute Attribute Points:  ";
        attrTitle.AddThemeFontSizeOverride("font_size", 16);
        attrTitle.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        attrHeader.AddChild(attrTitle);

        _attributePointsLabel = new Label();
        _attributePointsLabel.Text = "0 Points";
        _attributePointsLabel.AddThemeFontSizeOverride("font_size", 18);
        _attributePointsLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
        attrHeader.AddChild(_attributePointsLabel);

        // Attribute grid
        var attrGrid = new HBoxContainer();
        attrGrid.Alignment = BoxContainer.AlignmentMode.Center;
        attrGrid.AddThemeConstantOverride("separation", 30);
        parent.AddChild(attrGrid);

        CreateAttributeControl(attrGrid, "STR", new Color(0.9f, 0.4f, 0.3f),
            out _strValueLabel, out _strPlusButton, out _strMinusButton, "str");
        CreateAttributeControl(attrGrid, "DEX", new Color(0.3f, 0.8f, 0.4f),
            out _dexValueLabel, out _dexPlusButton, out _dexMinusButton, "dex");
        CreateAttributeControl(attrGrid, "VIT", new Color(0.9f, 0.5f, 0.2f),
            out _vitValueLabel, out _vitPlusButton, out _vitMinusButton, "vit");
        CreateAttributeControl(attrGrid, "ENE", new Color(0.4f, 0.6f, 1f),
            out _eneValueLabel, out _enePlusButton, out _eneMinusButton, "ene");
    }

    private void CreateAttributeControl(HBoxContainer parent, string label, Color color,
        out Label valueLabel, out Button plusButton, out Button minusButton, string attrId)
    {
        var container = new VBoxContainer();
        container.Alignment = BoxContainer.AlignmentMode.Center;
        parent.AddChild(container);

        var nameLabel = new Label();
        nameLabel.Text = label;
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", color);
        container.AddChild(nameLabel);

        var controlRow = new HBoxContainer();
        controlRow.Alignment = BoxContainer.AlignmentMode.Center;
        controlRow.AddThemeConstantOverride("separation", 5);
        container.AddChild(controlRow);

        minusButton = new Button();
        minusButton.Text = "-";
        minusButton.CustomMinimumSize = new Vector2(30, 30);
        string capturedId = attrId;
        minusButton.Pressed += () => OnAttributeMinus(capturedId);
        controlRow.AddChild(minusButton);

        valueLabel = new Label();
        valueLabel.Text = "10";
        valueLabel.CustomMinimumSize = new Vector2(40, 0);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        valueLabel.AddThemeFontSizeOverride("font_size", 18);
        valueLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        controlRow.AddChild(valueLabel);

        plusButton = new Button();
        plusButton.Text = "+";
        plusButton.CustomMinimumSize = new Vector2(30, 30);
        plusButton.Pressed += () => OnAttributePlus(capturedId);
        controlRow.AddChild(plusButton);
    }

    private VBoxContainer CreateColumn(string title, Color headerColor, out VBoxContainer contentColumn)
    {
        var columnContainer = new VBoxContainer();
        columnContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        columnContainer.AddThemeConstantOverride("separation", 6);

        var header = new Label();
        header.Text = title;
        header.HorizontalAlignment = HorizontalAlignment.Center;
        header.AddThemeFontSizeOverride("font_size", 16);
        header.AddThemeColorOverride("font_color", headerColor);
        columnContainer.AddChild(header);

        columnContainer.AddChild(new HSeparator());

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.CustomMinimumSize = new Vector2(380, 220);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        columnContainer.AddChild(scroll);

        contentColumn = new VBoxContainer();
        contentColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        contentColumn.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(contentColumn);

        return columnContainer;
    }

    private void OnLeveledUp(int newLevel)
    {
        _currentLevel = newLevel;

        // Reset pending changes
        _pendingStr = _pendingDex = _pendingVit = _pendingEne = 0;
        _selectedAbilityId = null;

        // Get available points
        _pendingAttributePoints = FPSController.Instance?.Stats.UnspentAttributePoints ?? 0;

        // Get available spells and abilities
        _availableSpells = AbilityManager3D.Instance?.GetSpellsAvailableAtLevel(newLevel).ToList() ?? new();
        _availableAbilities = AbilityManager3D.Instance?.GetAbilitiesAvailableAtLevel(newLevel).ToList() ?? new();

        // Update UI
        UpdateAttributeDisplay();
        PopulateChoices();

        if (_titleLabel != null)
        {
            _titleLabel.Text = $"LEVEL {newLevel}!";
        }

        Show();
        Input.MouseMode = Input.MouseModeEnum.Visible;

        GD.Print($"[LevelUpChoiceUI3D] Level {newLevel}: {_pendingAttributePoints} attribute points, {_availableSpells.Count} spells, {_availableAbilities.Count} abilities");
    }

    private void UpdateAttributeDisplay()
    {
        var stats = FPSController.Instance?.Stats;
        if (stats == null) return;

        int remaining = _pendingAttributePoints - (_pendingStr + _pendingDex + _pendingVit + _pendingEne);
        _attributePointsLabel!.Text = $"{remaining} Points Remaining";

        _strValueLabel!.Text = (stats.Strength + _pendingStr).ToString();
        _dexValueLabel!.Text = (stats.Dexterity + _pendingDex).ToString();
        _vitValueLabel!.Text = (stats.Vitality + _pendingVit).ToString();
        _eneValueLabel!.Text = (stats.Energy + _pendingEne).ToString();

        // Update button states
        bool canAdd = remaining > 0;
        _strPlusButton!.Disabled = !canAdd;
        _dexPlusButton!.Disabled = !canAdd;
        _vitPlusButton!.Disabled = !canAdd;
        _enePlusButton!.Disabled = !canAdd;

        _strMinusButton!.Disabled = _pendingStr <= 0;
        _dexMinusButton!.Disabled = _pendingDex <= 0;
        _vitMinusButton!.Disabled = _pendingVit <= 0;
        _eneMinusButton!.Disabled = _pendingEne <= 0;
    }

    private void OnAttributePlus(string attr)
    {
        int remaining = _pendingAttributePoints - (_pendingStr + _pendingDex + _pendingVit + _pendingEne);
        if (remaining <= 0) return;

        switch (attr)
        {
            case "str": _pendingStr++; break;
            case "dex": _pendingDex++; break;
            case "vit": _pendingVit++; break;
            case "ene": _pendingEne++; break;
        }

        UpdateAttributeDisplay();
    }

    private void OnAttributeMinus(string attr)
    {
        switch (attr)
        {
            case "str": if (_pendingStr > 0) _pendingStr--; break;
            case "dex": if (_pendingDex > 0) _pendingDex--; break;
            case "vit": if (_pendingVit > 0) _pendingVit--; break;
            case "ene": if (_pendingEne > 0) _pendingEne--; break;
        }

        UpdateAttributeDisplay();
    }

    private void PopulateChoices()
    {
        _choiceButtons.Clear();
        ClearColumn(_spellsColumn);
        ClearColumn(_abilitiesColumn);

        foreach (var spell in _availableSpells)
        {
            var btn = CreateChoiceButton(spell, true);
            _spellsColumn?.AddChild(btn);
            _choiceButtons[spell.AbilityId] = btn;
        }

        if (_availableSpells.Count == 0 && _spellsColumn != null)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "No new spells available";
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _spellsColumn.AddChild(emptyLabel);
        }

        foreach (var ability in _availableAbilities)
        {
            var btn = CreateChoiceButton(ability, false);
            _abilitiesColumn?.AddChild(btn);
            _choiceButtons[ability.AbilityId] = btn;
        }

        if (_availableAbilities.Count == 0 && _abilitiesColumn != null)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "No new abilities available";
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _abilitiesColumn.AddChild(emptyLabel);
        }
    }

    private void ClearColumn(VBoxContainer? column)
    {
        if (column == null) return;
        foreach (var child in column.GetChildren())
        {
            child.QueueFree();
        }
    }

    private Button CreateChoiceButton(Ability3D ability, bool isSpell)
    {
        var btn = new Button();
        btn.CustomMinimumSize = new Vector2(360, 55);
        btn.ClipText = false;
        btn.ToggleMode = true;

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.AddChild(vbox);

        var nameLabel = new Label();
        nameLabel.Text = ability.AbilityName;
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", isSpell ? new Color(0.5f, 0.7f, 1f) : new Color(0.5f, 0.9f, 0.5f));
        vbox.AddChild(nameLabel);

        var descLabel = new Label();
        descLabel.Text = ability.Description;
        descLabel.AddThemeFontSizeOverride("font_size", 11);
        descLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
        descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        vbox.AddChild(descLabel);

        var statsLabel = new Label();
        if (isSpell)
        {
            statsLabel.Text = $"Mana: {ability.DefaultManaCost} | CD: {ability.DefaultCooldown}s";
        }
        else
        {
            statsLabel.Text = $"Cooldown: {ability.DefaultCooldown}s";
        }
        statsLabel.AddThemeFontSizeOverride("font_size", 10);
        statsLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        vbox.AddChild(statsLabel);

        // Normal style
        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.1f, 0.1f, 0.15f);
        normalStyle.SetBorderWidthAll(1);
        normalStyle.BorderColor = isSpell ? new Color(0.25f, 0.35f, 0.55f) : new Color(0.25f, 0.45f, 0.3f);
        normalStyle.SetCornerRadiusAll(5);
        normalStyle.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        // Pressed (selected) style
        var pressedStyle = new StyleBoxFlat();
        pressedStyle.BgColor = isSpell ? new Color(0.15f, 0.2f, 0.35f) : new Color(0.15f, 0.3f, 0.2f);
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = isSpell ? new Color(0.4f, 0.6f, 1f) : new Color(0.4f, 0.8f, 0.5f);
        pressedStyle.SetCornerRadiusAll(5);
        pressedStyle.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);

        // Hover style
        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(0.15f, 0.15f, 0.22f);
        hoverStyle.SetBorderWidthAll(1);
        hoverStyle.BorderColor = isSpell ? new Color(0.35f, 0.5f, 0.8f) : new Color(0.35f, 0.65f, 0.4f);
        hoverStyle.SetCornerRadiusAll(5);
        hoverStyle.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        string capturedId = ability.AbilityId;
        btn.Toggled += (pressed) => OnChoiceToggled(capturedId, pressed);

        return btn;
    }

    private void OnChoiceToggled(string abilityId, bool pressed)
    {
        if (pressed)
        {
            // Deselect other buttons
            _selectedAbilityId = abilityId;
            foreach (var (id, btn) in _choiceButtons)
            {
                if (id != abilityId)
                {
                    btn.ButtonPressed = false;
                }
            }
            GD.Print($"[LevelUpChoiceUI3D] Selected: {abilityId}");
        }
        else if (_selectedAbilityId == abilityId)
        {
            _selectedAbilityId = null;
        }
    }

    private void OnConfirmPressed()
    {
        var stats = FPSController.Instance?.Stats;
        if (stats == null)
        {
            CloseUI();
            return;
        }

        // Apply attribute points
        for (int i = 0; i < _pendingStr; i++) stats.SpendAttributePoint("str");
        for (int i = 0; i < _pendingDex; i++) stats.SpendAttributePoint("dex");
        for (int i = 0; i < _pendingVit; i++) stats.SpendAttributePoint("vit");
        for (int i = 0; i < _pendingEne; i++) stats.SpendAttributePoint("ene");

        int totalSpent = _pendingStr + _pendingDex + _pendingVit + _pendingEne;
        if (totalSpent > 0)
        {
            GD.Print($"[LevelUpChoiceUI3D] Spent {totalSpent} attribute points: STR+{_pendingStr}, DEX+{_pendingDex}, VIT+{_pendingVit}, ENE+{_pendingEne}");
        }

        // Unlock selected ability
        if (_selectedAbilityId != null)
        {
            bool unlocked = AbilityManager3D.Instance?.UnlockAbility(_selectedAbilityId) ?? false;
            if (unlocked)
            {
                var ability = AbilityManager3D.Instance?.GetAbility(_selectedAbilityId);
                GD.Print($"[LevelUpChoiceUI3D] Unlocked: {ability?.AbilityName ?? _selectedAbilityId}");

                // Refresh spell book and abilities UI
                SpellBook3D.Instance?.RefreshSpellList();
                AbilitiesUI3D.Instance?.RefreshAbilityList();
            }
        }

        CloseUI();
    }

    private void CloseUI()
    {
        Hide();

        if (!FPSController.IsAnyUIOpen())
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        // Block ESC from immediately closing - force confirm
        if (@event.IsActionPressed("escape"))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        Instance = null;

        if (FPSController.Instance != null)
        {
            FPSController.Instance.LeveledUp -= OnLeveledUp;
        }
    }
}
