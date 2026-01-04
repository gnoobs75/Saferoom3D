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
/// </summary>
public partial class CharacterSheetUI : Control
{
    public static CharacterSheetUI? Instance { get; private set; }

    // UI Elements
    private PanelContainer? _mainPanel;
    private Label? _titleLabel;
    private Button? _closeButton;

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

        // Header
        CreateHeader(content);
        content.AddChild(new HSeparator());

        // Main area - horizontal split
        var mainArea = new HBoxContainer();
        mainArea.AddThemeConstantOverride("separation", 20);
        content.AddChild(mainArea);

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

        // Primary attributes with + buttons
        CreateAttributeRow(parent, "Strength", out _strengthLabel, out _strButton, "str");
        CreateAttributeRow(parent, "Dexterity", out _dexterityLabel, out _dexButton, "dex");
        CreateAttributeRow(parent, "Vitality", out _vitalityLabel, out _vitButton, "vit");
        CreateAttributeRow(parent, "Energy", out _energyLabel, out _eneButton, "ene");

        parent.AddChild(new HSeparator());

        // Derived stats title
        var derivedTitle = new Label();
        derivedTitle.Text = "Combat Stats";
        derivedTitle.AddThemeFontSizeOverride("font_size", 18);
        derivedTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.6f));
        parent.AddChild(derivedTitle);

        // Derived stats
        _healthLabel = CreateStatLabel(parent, "Health: 100/100", new Color(0.9f, 0.3f, 0.3f));
        _manaLabel = CreateStatLabel(parent, "Mana: 50/50", new Color(0.3f, 0.5f, 0.9f));
        _damageLabel = CreateStatLabel(parent, "Damage: 10-20");
        _armorLabel = CreateStatLabel(parent, "Armor: 0");
        _critLabel = CreateStatLabel(parent, "Crit Chance: 5%", new Color(1f, 0.8f, 0.3f));
        _blockLabel = CreateStatLabel(parent, "Block Chance: 0%");
        _dodgeLabel = CreateStatLabel(parent, "Dodge Chance: 0%");
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

    private void CreateAttributeRow(VBoxContainer parent, string name, out Label valueLabel, out Button plusButton, string attrKey)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label();
        nameLabel.Text = $"{name}:";
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.82f, 0.9f));
        row.AddChild(nameLabel);

        valueLabel = new Label();
        valueLabel.Text = "10";
        valueLabel.CustomMinimumSize = new Vector2(40, 0);
        valueLabel.AddThemeFontSizeOverride("font_size", 14);
        valueLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        row.AddChild(valueLabel);

        plusButton = new Button();
        plusButton.Text = "+";
        plusButton.CustomMinimumSize = new Vector2(28, 24);
        plusButton.AddThemeFontSizeOverride("font_size", 16);
        plusButton.Pressed += () => OnAttributeButtonPressed(attrKey);
        row.AddChild(plusButton);

        parent.AddChild(row);
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
