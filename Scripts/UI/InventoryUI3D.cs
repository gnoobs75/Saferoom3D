using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;
using SafeRoom3D.Items;
using SafeRoom3D.Player;

namespace SafeRoom3D.UI;

/// <summary>
/// Inventory UI panel showing items in a 5x5 grid.
/// Items can be clicked to use, or right-clicked for info.
/// </summary>
public partial class InventoryUI3D : Control
{
    public static InventoryUI3D? Instance { get; private set; }

    // UI Elements
    private PanelContainer? _mainPanel;
    private GridContainer? _itemGrid;
    private Label? _titleLabel;
    private Button? _closeButton;
    private PanelContainer? _tooltipPanel;
    private Label? _tooltipName;
    private Label? _tooltipDesc;
    private Label? _tooltipCount;

    // Item slots
    private readonly List<ItemSlot> _slots = new();

    // Tab filtering
    private HBoxContainer? _tabBar;
    private Button? _tabAll;
    private Button? _tabWeapons;
    private Button? _tabArmor;
    private Button? _tabConsumables;
    private Button? _tabJunk;
    private InventoryTab _currentTab = InventoryTab.All;

    // Panel size
    private const float PanelWidth = 350;
    private const float PanelHeight = 490;
    private const int SlotSize = 54;
    private const int SlotGap = 6;

    // Window Dragging
    private const string WindowName = "Inventory";
    private bool _isDragging;
    private Vector2 _dragOffset;

    // Item Dragging
    private int _draggedSlotIndex = -1;
    private Control? _dragPreview;

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        CreateUI();
        CreateTooltip();

        // Connect to inventory changes
        CallDeferred(nameof(ConnectInventorySignals));

        GD.Print("[InventoryUI3D] Ready");
    }

    private void ConnectInventorySignals()
    {
        if (Inventory3D.Instance != null)
        {
            Inventory3D.Instance.InventoryChanged += RefreshSlots;
            RefreshSlots();
        }
    }

    private void CreateUI()
    {
        // Semi-transparent background
        var background = new ColorRect();
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.Color = new Color(0, 0, 0, 0.5f);
        background.MouseFilter = MouseFilterEnum.Stop;
        AddChild(background);

        // Main panel
        _mainPanel = new PanelContainer();
        _mainPanel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.12f, 0.1f, 0.18f, 0.98f);
        panelStyle.BorderColor = new Color(0.5f, 0.4f, 0.6f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetContentMarginAll(15);
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_mainPanel);

        // Content container
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        _mainPanel.AddChild(content);

        // Header (draggable area)
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        header.CustomMinimumSize = new Vector2(0, 40);
        header.MouseFilter = MouseFilterEnum.Stop;
        header.GuiInput += OnHeaderGuiInput;

        _titleLabel = new Label();
        _titleLabel.Text = "Inventory (drag to move)";
        _titleLabel.AddThemeFontSizeOverride("font_size", 24);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 1f));
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        header.AddChild(_titleLabel);

        _closeButton = new Button();
        _closeButton.Text = "X";
        _closeButton.CustomMinimumSize = new Vector2(32, 32);
        _closeButton.Pressed += () => Hide();
        header.AddChild(_closeButton);

        content.AddChild(header);

        // Separator
        content.AddChild(new HSeparator());

        // Instructions
        var instructions = new Label();
        instructions.Text = "Left-drag to hotbar | Right-click to use";
        instructions.AddThemeFontSizeOverride("font_size", 10);
        instructions.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.7f));
        instructions.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(instructions);

        // Tab bar
        CreateTabBar(content);

        // Item grid
        _itemGrid = new GridContainer();
        _itemGrid.Columns = Inventory3D.GridWidth;
        _itemGrid.AddThemeConstantOverride("h_separation", SlotGap);
        _itemGrid.AddThemeConstantOverride("v_separation", SlotGap);
        content.AddChild(_itemGrid);

        // Create slots
        for (int i = 0; i < Inventory3D.TotalSlots; i++)
        {
            var slot = CreateSlot(i);
            _slots.Add(slot);
            _itemGrid.AddChild(slot.Container);
        }
    }

    private ItemSlot CreateSlot(int index)
    {
        var slot = new ItemSlot();
        slot.Index = index;

        // Container
        slot.Container = new PanelContainer();
        slot.Container.CustomMinimumSize = new Vector2(SlotSize, SlotSize);

        slot.Background = new StyleBoxFlat();
        slot.Background.BgColor = new Color(0.18f, 0.15f, 0.25f, 0.9f);
        slot.Background.BorderColor = new Color(0.35f, 0.3f, 0.4f);
        slot.Background.SetBorderWidthAll(2);
        slot.Background.SetCornerRadiusAll(4);
        slot.Container.AddThemeStyleboxOverride("panel", slot.Background);

        // Click area
        var clickArea = new Control();
        clickArea.SetAnchorsPreset(LayoutPreset.FullRect);
        clickArea.MouseFilter = MouseFilterEnum.Stop;
        clickArea.GuiInput += (e) => OnSlotInput(e, slot);
        slot.Container.AddChild(clickArea);

        // Icon
        slot.Icon = new TextureRect();
        slot.Icon.SetAnchorsPreset(LayoutPreset.FullRect);
        slot.Icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        slot.Icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        slot.Icon.MouseFilter = MouseFilterEnum.Ignore;
        slot.Container.AddChild(slot.Icon);

        // Stack count label
        slot.CountLabel = new Label();
        slot.CountLabel.SetAnchorsPreset(LayoutPreset.BottomRight);
        slot.CountLabel.Position = new Vector2(-20, -18);
        slot.CountLabel.AddThemeFontSizeOverride("font_size", 12);
        slot.CountLabel.AddThemeColorOverride("font_color", Colors.White);
        slot.CountLabel.MouseFilter = MouseFilterEnum.Ignore;
        slot.Container.AddChild(slot.CountLabel);

        return slot;
    }

    private void CreateTooltip()
    {
        _tooltipPanel = new PanelContainer();
        _tooltipPanel.CustomMinimumSize = new Vector2(200, 80);
        _tooltipPanel.Visible = false;
        _tooltipPanel.ZIndex = 10;
        _tooltipPanel.MouseFilter = MouseFilterEnum.Ignore;

        var tooltipStyle = new StyleBoxFlat();
        tooltipStyle.BgColor = new Color(0.1f, 0.08f, 0.15f, 0.98f);
        tooltipStyle.BorderColor = new Color(0.6f, 0.5f, 0.7f);
        tooltipStyle.SetBorderWidthAll(2);
        tooltipStyle.SetCornerRadiusAll(6);
        tooltipStyle.SetContentMarginAll(10);
        _tooltipPanel.AddThemeStyleboxOverride("panel", tooltipStyle);

        var tooltipContent = new VBoxContainer();
        tooltipContent.AddThemeConstantOverride("separation", 4);
        tooltipContent.MouseFilter = MouseFilterEnum.Ignore;

        _tooltipName = new Label();
        _tooltipName.AddThemeFontSizeOverride("font_size", 14);
        _tooltipName.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.85f));
        _tooltipName.MouseFilter = MouseFilterEnum.Ignore;
        tooltipContent.AddChild(_tooltipName);

        _tooltipDesc = new Label();
        _tooltipDesc.AddThemeFontSizeOverride("font_size", 11);
        _tooltipDesc.AddThemeColorOverride("font_color", new Color(0.75f, 0.7f, 0.85f));
        _tooltipDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tooltipDesc.CustomMinimumSize = new Vector2(180, 0);
        _tooltipDesc.MouseFilter = MouseFilterEnum.Ignore;
        tooltipContent.AddChild(_tooltipDesc);

        _tooltipCount = new Label();
        _tooltipCount.AddThemeFontSizeOverride("font_size", 11);
        _tooltipCount.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
        _tooltipCount.MouseFilter = MouseFilterEnum.Ignore;
        tooltipContent.AddChild(_tooltipCount);

        _tooltipPanel.AddChild(tooltipContent);
        AddChild(_tooltipPanel);
    }

    private void CreateTabBar(VBoxContainer content)
    {
        _tabBar = new HBoxContainer();
        _tabBar.AddThemeConstantOverride("separation", 4);
        _tabBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Create tab buttons
        _tabAll = CreateTabButton("All", InventoryTab.All);
        _tabWeapons = CreateTabButton("Wep", InventoryTab.Weapons);
        _tabArmor = CreateTabButton("Arm", InventoryTab.Armor);
        _tabConsumables = CreateTabButton("Con", InventoryTab.Consumables);
        _tabJunk = CreateTabButton("Jnk", InventoryTab.Junk);

        _tabBar.AddChild(_tabAll);
        _tabBar.AddChild(_tabWeapons);
        _tabBar.AddChild(_tabArmor);
        _tabBar.AddChild(_tabConsumables);
        _tabBar.AddChild(_tabJunk);

        content.AddChild(_tabBar);

        // Set initial active tab
        UpdateTabStyles();
    }

    private Button CreateTabButton(string text, InventoryTab tab)
    {
        var button = new Button();
        button.Text = text;
        button.CustomMinimumSize = new Vector2(60, 28);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Pressed += () => SelectTab(tab);
        return button;
    }

    private void SelectTab(InventoryTab tab)
    {
        _currentTab = tab;
        UpdateTabStyles();
        RefreshSlots();
        GD.Print($"[InventoryUI3D] Selected tab: {tab}");
    }

    private void UpdateTabStyles()
    {
        // Create styles for active and inactive tabs
        var activeStyle = new StyleBoxFlat();
        activeStyle.BgColor = new Color(0.35f, 0.3f, 0.45f);
        activeStyle.BorderColor = new Color(0.7f, 0.6f, 0.8f);
        activeStyle.SetBorderWidthAll(2);
        activeStyle.SetCornerRadiusAll(4);

        var inactiveStyle = new StyleBoxFlat();
        inactiveStyle.BgColor = new Color(0.2f, 0.17f, 0.28f);
        inactiveStyle.BorderColor = new Color(0.4f, 0.35f, 0.5f);
        inactiveStyle.SetBorderWidthAll(1);
        inactiveStyle.SetCornerRadiusAll(4);

        // Apply styles based on current tab
        ApplyTabStyle(_tabAll, _currentTab == InventoryTab.All ? activeStyle : inactiveStyle);
        ApplyTabStyle(_tabWeapons, _currentTab == InventoryTab.Weapons ? activeStyle : inactiveStyle);
        ApplyTabStyle(_tabArmor, _currentTab == InventoryTab.Armor ? activeStyle : inactiveStyle);
        ApplyTabStyle(_tabConsumables, _currentTab == InventoryTab.Consumables ? activeStyle : inactiveStyle);
        ApplyTabStyle(_tabJunk, _currentTab == InventoryTab.Junk ? activeStyle : inactiveStyle);
    }

    private void ApplyTabStyle(Button? button, StyleBoxFlat style)
    {
        if (button == null) return;
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
    }

    /// <summary>
    /// Determines if an item matches the current tab filter.
    /// </summary>
    private bool ItemMatchesTab(InventoryItem item)
    {
        if (_currentTab == InventoryTab.All)
            return true;

        switch (_currentTab)
        {
            case InventoryTab.Weapons:
                // Weapons are equipment with MainHand or OffHand slots
                if (item.Type != ItemType.Equipment || item.Equipment == null)
                    return false;
                return item.Equipment.Slot == EquipmentSlot.MainHand ||
                       item.Equipment.Slot == EquipmentSlot.OffHand;

            case InventoryTab.Armor:
                // Armor includes Head, Chest, Hands, Feet, and Rings
                if (item.Type != ItemType.Equipment || item.Equipment == null)
                    return false;
                var slot = item.Equipment.Slot;
                return slot == EquipmentSlot.Head ||
                       slot == EquipmentSlot.Chest ||
                       slot == EquipmentSlot.Hands ||
                       slot == EquipmentSlot.Feet ||
                       slot == EquipmentSlot.Ring1 ||
                       slot == EquipmentSlot.Ring2;

            case InventoryTab.Consumables:
                return item.Type == ItemType.Consumable;

            case InventoryTab.Junk:
                // Junk includes Material and Quest items
                return item.Type == ItemType.Material || item.Type == ItemType.Quest;

            default:
                return true;
        }
    }

    private void OnSlotInput(InputEvent e, ItemSlot slot)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            // Check if slot is valid (not an empty filtered slot)
            if (slot.Index < 0) return;

            var item = Inventory3D.Instance?.GetItem(slot.Index);
            if (item == null) return;

            if (mb.ButtonIndex == MouseButton.Left)
            {
                // Left-click: Start drag for all item types
                // Equipment drags to character sheet, consumables drag to hotbar
                StartItemDrag(slot.Index, item);
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                // Right-click: Use consumable or show tooltip for other items
                if (item.Type == ItemType.Consumable)
                {
                    var player = FPSController.Instance;
                    if (player != null && ConsumableItems3D.UseItem(item.Id, player))
                    {
                        Inventory3D.Instance?.UseItem(slot.Index);
                        RefreshSlots();
                        HUD3D.Instance?.RefreshConsumableStacks();
                    }
                }
                else
                {
                    // Show tooltip for non-consumables
                    ShowTooltip(slot, item);
                }
            }
        }
        else if (e is InputEventMouseMotion)
        {
            // Check if slot is valid (not an empty filtered slot)
            if (slot.Index < 0)
            {
                HideTooltip();
                return;
            }

            var item = Inventory3D.Instance?.GetItem(slot.Index);
            if (item != null)
            {
                ShowTooltip(slot, item);
            }
            else
            {
                HideTooltip();
            }
        }
    }

    private void StartItemDrag(int slotIndex, InventoryItem item)
    {
        _draggedSlotIndex = slotIndex;

        // Create drag preview
        _dragPreview = new PanelContainer();
        _dragPreview.CustomMinimumSize = new Vector2(56, 56);
        _dragPreview.ZIndex = 100;
        _dragPreview.MouseFilter = MouseFilterEnum.Ignore;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.2f, 0.15f, 0.3f, 0.95f);
        style.BorderColor = item.GetRarityColor();
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(6);
        _dragPreview.AddThemeStyleboxOverride("panel", style);

        // Icon
        var icon = new TextureRect();
        icon.Texture = ItemDatabase.GetIcon(item.Id);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.CustomMinimumSize = new Vector2(48, 48);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        _dragPreview.AddChild(icon);

        // Item name label below
        var nameLabel = new Label();
        nameLabel.Text = item.Name;
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.AddThemeColorOverride("font_color", item.GetRarityColor());
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.Position = new Vector2(-20, 56);
        nameLabel.Size = new Vector2(96, 20);
        nameLabel.MouseFilter = MouseFilterEnum.Ignore;
        _dragPreview.AddChild(nameLabel);

        AddChild(_dragPreview);
        _dragPreview.GlobalPosition = GetGlobalMousePosition() - new Vector2(28, 28);

        HideTooltip();
        GD.Print($"[InventoryUI3D] Started dragging {item.Name} from slot {slotIndex}");
    }

    private void EndItemDrag(Vector2 dropPosition)
    {
        if (_draggedSlotIndex < 0) return;

        var item = Inventory3D.Instance?.GetItem(_draggedSlotIndex);
        if (item == null)
        {
            // Cleanup
            _dragPreview?.QueueFree();
            _dragPreview = null;
            _draggedSlotIndex = -1;
            return;
        }

        bool handled = false;

        // Check if dropped on action bar (for consumables)
        if (item.Type == ItemType.Consumable)
        {
            var hud = HUD3D.Instance;
            if (hud != null)
            {
                var (row, slot) = hud.FindActionBarSlotAt(dropPosition);
                if (row >= 0 && slot >= 0)
                {
                    // Assign consumable to hotbar
                    hud.UpdateActionSlotConsumable(row, slot, item.Id);
                    GD.Print($"[InventoryUI3D] Assigned {item.Name} to hotbar [{row},{slot}]");
                    handled = true;
                }
            }
        }

        // Check if dropped on Character Sheet (for equipment)
        if (!handled && item.Equipment != null)
        {
            var charSheet = CharacterSheetUI.Instance;
            if (charSheet != null && charSheet.Visible)
            {
                // Try to equip
                if (charSheet.TryEquipFromInventory(item, _draggedSlotIndex))
                {
                    GD.Print($"[InventoryUI3D] Equipped {item.Name}");
                    handled = true;
                }
            }
        }

        // Cleanup
        _dragPreview?.QueueFree();
        _dragPreview = null;
        _draggedSlotIndex = -1;
    }

    private void ShowTooltip(ItemSlot slot, InventoryItem item)
    {
        if (_tooltipPanel == null) return;

        _tooltipName!.Text = item.Name;
        _tooltipDesc!.Text = item.Description;
        _tooltipCount!.Text = $"Count: {item.StackCount}/{item.MaxStackSize}";

        _tooltipPanel.Visible = true;

        // Position tooltip near the slot
        var slotPos = slot.Container!.GlobalPosition;
        _tooltipPanel.GlobalPosition = new Vector2(slotPos.X + SlotSize + 10, slotPos.Y);
    }

    private void HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.Visible = false;
    }

    private void RefreshSlots()
    {
        var inventory = Inventory3D.Instance;
        if (inventory == null) return;

        // When filtering, we display items in sequence but maintain the mapping to real slots
        // Collect items that match the current filter
        var matchingItems = new List<(int slot, InventoryItem item)>();
        for (int i = 0; i < Inventory3D.TotalSlots; i++)
        {
            var item = inventory.GetItem(i);
            if (item != null && ItemMatchesTab(item))
            {
                matchingItems.Add((i, item));
            }
        }

        // Display matching items in UI slots, empty slots for rest
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];

            if (i < matchingItems.Count)
            {
                var (realSlot, item) = matchingItems[i];
                slot.Index = realSlot;  // Update index to point to real inventory slot
                slot.Icon!.Texture = ItemDatabase.GetIcon(item.Id);
                slot.CountLabel!.Text = item.StackCount > 1 ? item.StackCount.ToString() : "";
                slot.Background!.BorderColor = ItemDatabase.GetItemColor(item.Id);
            }
            else
            {
                slot.Index = -1;  // Mark as empty (no real slot mapped)
                slot.Icon!.Texture = null;
                slot.CountLabel!.Text = "";
                slot.Background!.BorderColor = new Color(0.35f, 0.3f, 0.4f);
            }
        }
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

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        // Handle item drag
        if (_draggedSlotIndex >= 0 && _dragPreview != null)
        {
            if (@event is InputEventMouseMotion motion)
            {
                _dragPreview.GlobalPosition = motion.GlobalPosition - new Vector2(28, 28);
            }
            else if (@event is InputEventMouseButton itemMb && itemMb.ButtonIndex == MouseButton.Left && !itemMb.Pressed)
            {
                EndItemDrag(itemMb.GlobalPosition);
            }
        }

        // Stop window drag if mouse released anywhere
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
            // Cancel item drag first
            if (_draggedSlotIndex >= 0)
            {
                _dragPreview?.QueueFree();
                _dragPreview = null;
                _draggedSlotIndex = -1;
            }
            else
            {
                Hide();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        // Only update position if not dragging (initial position is set in Show)
        // This is now empty since we set position in Show() and during drag
    }

    public new void Show()
    {
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        RefreshSlots();

        // Restore or center position
        if (_mainPanel != null)
        {
            var viewportSize = GetViewportRect().Size;
            var storedPos = WindowPositionManager.GetPosition(WindowName);
            if (storedPos == WindowPositionManager.CenterMarker)
            {
                // Center the panel
                _mainPanel.Position = WindowPositionManager.GetCenteredPosition(viewportSize, new Vector2(PanelWidth, PanelHeight));
            }
            else
            {
                // Use stored position, but clamp to current viewport
                _mainPanel.Position = WindowPositionManager.ClampToViewport(storedPos, viewportSize, new Vector2(PanelWidth, PanelHeight));
            }
        }

        GD.Print("[InventoryUI3D] Opened");
    }

    public new void Hide()
    {
        Visible = false;
        HideTooltip();

        // Only capture mouse if no other UI windows are open
        bool otherUIOpen = (CharacterSheetUI.Instance?.Visible == true) ||
                           (SpellBook3D.Instance?.Visible == true) ||
                           (LootUI3D.Instance?.Visible == true);
        if (!otherUIOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        GD.Print("[InventoryUI3D] Closed");
    }

    public void Toggle()
    {
        if (Visible)
            Hide();
        else
            Show();
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}

/// <summary>
/// Data for an inventory slot.
/// </summary>
public class ItemSlot
{
    public int Index { get; set; }
    public PanelContainer? Container { get; set; }
    public StyleBoxFlat? Background { get; set; }
    public TextureRect? Icon { get; set; }
    public Label? CountLabel { get; set; }
}

/// <summary>
/// Inventory tab filter categories.
/// </summary>
public enum InventoryTab
{
    All,
    Weapons,
    Armor,
    Consumables,
    Junk
}
