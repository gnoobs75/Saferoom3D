using Godot;
using SafeRoom3D.Items;
using SafeRoom3D.NPC;
using SafeRoom3D.Core;
using System.Collections.Generic;

namespace SafeRoom3D.UI;

/// <summary>
/// Modal shop UI for buying and selling items with NPCs.
/// Features tabs for Consumables, Weapons, and Armor.
/// </summary>
public partial class ShopUI3D : Control
{
    public static ShopUI3D? Instance { get; private set; }

    private Bopca3D? _currentShopkeeper;

    // UI elements
    private Panel? _mainPanel;
    private Label? _titleLabel;
    private Label? _goldLabel;
    private TabContainer? _shopTabs;
    private VBoxContainer? _consumablesContainer;
    private VBoxContainer? _weaponsContainer;
    private VBoxContainer? _armorContainer;
    private VBoxContainer? _playerItemsContainer;
    private Button? _closeButton;

    // Shop inventory by category
    private List<ShopItem> _consumables = new();
    private List<ShopItem> _weapons = new();
    private List<ShopItem> _armor = new();

    // Colors
    private static readonly Color HeaderColor = new(0.2f, 0.6f, 0.3f);
    private static readonly Color GoldColor = new(0.9f, 0.8f, 0.2f);
    private static readonly Color BuyButtonColor = new(0.2f, 0.5f, 0.2f);
    private static readonly Color SellButtonColor = new(0.5f, 0.4f, 0.2f);
    private static readonly Color TabActiveColor = new(0.25f, 0.4f, 0.3f);

    public override void _Ready()
    {
        Instance = this;
        Visible = false;
        LoadShopInventories();
        BuildUI();
    }

    private void LoadShopInventories()
    {
        _consumables = ShopItemFactory.GetConsumables();
        _weapons = ShopItemFactory.GetWeapons();
        _armor = ShopItemFactory.GetArmor();

        GD.Print($"[ShopUI3D] Loaded {_consumables.Count} consumables, {_weapons.Count} weapons, {_armor.Count} armor");
    }

    private void BuildUI()
    {
        // Main panel - wider to fit buy/sell buttons without scrollbar
        _mainPanel = new Panel();
        _mainPanel.CustomMinimumSize = new Vector2(900, 550);
        _mainPanel.Size = new Vector2(900, 550);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        panelStyle.BorderColor = HeaderColor;
        panelStyle.SetBorderWidthAll(2);
        panelStyle.SetCornerRadiusAll(8);
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_mainPanel);

        // Main layout
        var mainVBox = new VBoxContainer();
        mainVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mainVBox.AddThemeConstantOverride("separation", 10);
        mainVBox.OffsetLeft = 15;
        mainVBox.OffsetRight = -15;
        mainVBox.OffsetTop = 15;
        mainVBox.OffsetBottom = -15;
        _mainPanel.AddChild(mainVBox);

        // Header row
        var headerRow = new HBoxContainer();
        mainVBox.AddChild(headerRow);

        _titleLabel = new Label();
        _titleLabel.Text = "SHOP";
        _titleLabel.AddThemeFontSizeOverride("font_size", 28);
        _titleLabel.AddThemeColorOverride("font_color", HeaderColor);
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        headerRow.AddChild(_titleLabel);

        _goldLabel = new Label();
        _goldLabel.Text = "Gold: 0";
        _goldLabel.AddThemeFontSizeOverride("font_size", 22);
        _goldLabel.AddThemeColorOverride("font_color", GoldColor);
        headerRow.AddChild(_goldLabel);

        // Spacer
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(20, 0);
        headerRow.AddChild(spacer);

        _closeButton = new Button();
        _closeButton.Text = "X";
        _closeButton.CustomMinimumSize = new Vector2(40, 40);
        _closeButton.Pressed += OnClosePressed;
        headerRow.AddChild(_closeButton);

        // Separator
        var sep = new HSeparator();
        mainVBox.AddChild(sep);

        // Content area - two columns
        var columnsContainer = new HBoxContainer();
        columnsContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        columnsContainer.AddThemeConstantOverride("separation", 15);
        mainVBox.AddChild(columnsContainer);

        // Left column - Shop tabs
        var shopColumn = new VBoxContainer();
        shopColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        shopColumn.CustomMinimumSize = new Vector2(500, 0);
        columnsContainer.AddChild(shopColumn);

        // Tab container for shop categories
        _shopTabs = new TabContainer();
        _shopTabs.SizeFlagsVertical = SizeFlags.ExpandFill;
        _shopTabs.TabChanged += OnTabChanged;
        shopColumn.AddChild(_shopTabs);

        // Consumables tab
        var consumablesTab = CreateTabContent("Consumables");
        _consumablesContainer = consumablesTab.container;
        _shopTabs.AddChild(consumablesTab.scroll);

        // Weapons tab
        var weaponsTab = CreateTabContent("Weapons");
        _weaponsContainer = weaponsTab.container;
        _shopTabs.AddChild(weaponsTab.scroll);

        // Armor tab
        var armorTab = CreateTabContent("Armor");
        _armorContainer = armorTab.container;
        _shopTabs.AddChild(armorTab.scroll);

        // Vertical separator
        var vsep = new VSeparator();
        columnsContainer.AddChild(vsep);

        // Right column - Player inventory (sellable items)
        var playerColumn = new VBoxContainer();
        playerColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        playerColumn.CustomMinimumSize = new Vector2(320, 0);
        columnsContainer.AddChild(playerColumn);

        var playerHeader = new Label();
        playerHeader.Text = "YOUR ITEMS (Sell)";
        playerHeader.AddThemeFontSizeOverride("font_size", 18);
        playerHeader.AddThemeColorOverride("font_color", SellButtonColor);
        playerColumn.AddChild(playerHeader);

        var playerScroll = new ScrollContainer();
        playerScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        playerColumn.AddChild(playerScroll);

        _playerItemsContainer = new VBoxContainer();
        _playerItemsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _playerItemsContainer.AddThemeConstantOverride("separation", 5);
        playerScroll.AddChild(_playerItemsContainer);
    }

    private (ScrollContainer scroll, VBoxContainer container) CreateTabContent(string name)
    {
        var scroll = new ScrollContainer();
        scroll.Name = name;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var container = new VBoxContainer();
        container.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        container.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(container);

        return (scroll, container);
    }

    private void OnTabChanged(long tabIndex)
    {
        // Refresh the current tab
        RefreshCurrentTab();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape || keyEvent.Keycode == Key.T)
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Open the shop UI for the given shopkeeper
    /// </summary>
    public void Open(Bopca3D shopkeeper)
    {
        _currentShopkeeper = shopkeeper;

        // Update title
        if (_titleLabel != null)
        {
            _titleLabel.Text = $"{shopkeeper.NPCName.ToUpper()}'S SHOP";
        }

        // Refresh displays
        RefreshGoldDisplay();
        RefreshCurrentTab();
        RefreshPlayerItems();

        // Position the shop panel - check for saved position or center it
        if (_mainPanel != null)
        {
            var savedPos = WindowPositionManager.GetPosition("ShopUI");
            if (savedPos != WindowPositionManager.CenterMarker)
            {
                // Use saved position, clamped to viewport
                var viewportSize = GetViewport().GetVisibleRect().Size;
                _mainPanel.Position = WindowPositionManager.ClampToViewport(savedPos, viewportSize, _mainPanel.Size);
            }
            else
            {
                // Center on screen
                var viewportSize = GetViewport().GetVisibleRect().Size;
                _mainPanel.Position = (viewportSize - _mainPanel.Size) / 2f;
            }
        }

        // Show and lock mouse
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (Player.FPSController.Instance != null)
        {
            Player.FPSController.Instance.MouseControlLocked = true;
        }

        // Also open the inventory UI so player can see their items
        var inventoryUI = InventoryUI3D.Instance;
        if (inventoryUI != null && !inventoryUI.Visible)
        {
            inventoryUI.Show();
        }

        GD.Print($"[ShopUI3D] Opened shop for {shopkeeper.NPCName}");
    }

    /// <summary>
    /// Close the shop UI
    /// </summary>
    public void Close()
    {
        Visible = false;
        _currentShopkeeper = null;

        // Also close inventory if it was opened with shop
        var inventoryUI = InventoryUI3D.Instance;
        if (inventoryUI != null && inventoryUI.Visible)
        {
            inventoryUI.Hide();
        }

        // Unlock mouse
        Input.MouseMode = Input.MouseModeEnum.Captured;

        if (Player.FPSController.Instance != null)
        {
            Player.FPSController.Instance.MouseControlLocked = false;
        }

        GD.Print("[ShopUI3D] Closed shop");
    }

    /// <summary>
    /// Get the main panel for UI positioning (used by UIEditorMode)
    /// </summary>
    public Panel? GetMainPanel() => _mainPanel;

    private void RefreshGoldDisplay()
    {
        if (_goldLabel == null) return;

        var inventory = Inventory3D.Instance;
        int gold = inventory?.GetGoldCount() ?? 0;
        _goldLabel.Text = $"Gold: {gold:N0}";
    }

    private void RefreshCurrentTab()
    {
        if (_shopTabs == null) return;

        int currentTab = _shopTabs.CurrentTab;
        var inventory = Inventory3D.Instance;
        int playerGold = inventory?.GetGoldCount() ?? 0;

        switch (currentTab)
        {
            case 0: // Consumables
                RefreshItemList(_consumablesContainer, _consumables, playerGold);
                break;
            case 1: // Weapons
                RefreshItemList(_weaponsContainer, _weapons, playerGold);
                break;
            case 2: // Armor
                RefreshItemList(_armorContainer, _armor, playerGold);
                break;
        }
    }

    private void RefreshItemList(VBoxContainer? container, List<ShopItem> items, int playerGold)
    {
        if (container == null) return;

        // Clear existing items
        foreach (var child in container.GetChildren())
        {
            child.QueueFree();
        }

        // Add shop items
        foreach (var item in items)
        {
            var row = CreateShopItemRow(item, playerGold);
            container.AddChild(row);
        }
    }

    private HBoxContainer CreateShopItemRow(ShopItem item, int playerGold)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        // Item name
        var nameLabel = new Label();
        nameLabel.Text = item.DisplayName;
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameLabel.ClipText = true;
        row.AddChild(nameLabel);

        // Stock (if limited)
        if (item.Stock >= 0)
        {
            var stockLabel = new Label();
            stockLabel.Text = $"x{item.Stock}";
            stockLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            stockLabel.CustomMinimumSize = new Vector2(40, 0);
            row.AddChild(stockLabel);
        }

        // Price
        var priceLabel = new Label();
        priceLabel.Text = $"{item.BuyPrice:N0}g";
        priceLabel.AddThemeColorOverride("font_color", GoldColor);
        priceLabel.CustomMinimumSize = new Vector2(80, 0);
        priceLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(priceLabel);

        // Buy button
        var buyBtn = new Button();
        buyBtn.Text = "BUY";
        buyBtn.CustomMinimumSize = new Vector2(70, 28);

        bool canAfford = playerGold >= item.BuyPrice;
        bool inStock = item.Stock != 0;
        bool canBuy = canAfford && inStock;

        if (canBuy)
        {
            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = BuyButtonColor;
            btnStyle.SetCornerRadiusAll(4);
            buyBtn.AddThemeStyleboxOverride("normal", btnStyle);
        }
        else
        {
            buyBtn.Disabled = true;
        }

        buyBtn.Pressed += () => OnBuyItem(item);
        row.AddChild(buyBtn);

        return row;
    }

    private void RefreshPlayerItems()
    {
        if (_playerItemsContainer == null) return;

        // Clear existing items
        foreach (var child in _playerItemsContainer.GetChildren())
        {
            child.QueueFree();
        }

        var inventory = Inventory3D.Instance;
        if (inventory == null) return;

        // Add player items that can be sold
        foreach (var (slot, item) in inventory.GetAllItems())
        {
            // Calculate sell price (50% of base value)
            int sellPrice = GetSellPrice(item);
            if (sellPrice <= 0) continue; // Can't sell worthless items

            var row = CreatePlayerItemRow(slot, item, sellPrice);
            _playerItemsContainer.AddChild(row);
        }
    }

    private int GetSellPrice(InventoryItem item)
    {
        // Base sell prices based on item type/id
        if (item.Id.Contains("health_potion")) return 10;
        if (item.Id.Contains("mana_potion")) return 12;
        if (item.Id.Contains("grenade")) return 75;
        if (item.Id.Contains("courage")) return 100;
        if (item.Id.Contains("beacon")) return 50;
        if (item.Id.Contains("musk")) return 80;

        if (item.Type == ItemType.Equipment && item.Equipment != null)
        {
            // Equipment sells for portion of value based on rarity
            int baseValue = item.Equipment.ItemLevel * 10;
            float rarityMult = item.Equipment.Rarity switch
            {
                ItemRarity.Normal => 0.3f,
                ItemRarity.Magic => 0.5f,
                ItemRarity.Rare => 0.7f,
                ItemRarity.Unique => 1.0f,
                ItemRarity.Set => 1.0f,
                _ => 0.3f
            };
            return (int)(baseValue * rarityMult);
        }

        // Materials
        if (item.Id.Contains("tooth")) return 5;
        if (item.Id.Contains("tail")) return 3;
        if (item.Id.Contains("arrow")) return 2;
        if (item.Id.Contains("meat")) return 5;
        if (item.Id.Contains("dust")) return 15;
        if (item.Id.Contains("gemstone")) return 50;
        if (item.Id.Contains("relic")) return 100;
        if (item.Id.Contains("scale")) return 200;

        return 5; // Default junk price
    }

    private HBoxContainer CreatePlayerItemRow(int slot, InventoryItem item, int sellPrice)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        // Item name
        var nameLabel = new Label();
        string displayName = item.Name ?? item.Id;
        if (item.StackCount > 1)
        {
            displayName += $" x{item.StackCount}";
        }
        nameLabel.Text = displayName;
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameLabel.ClipText = true;

        // Color by rarity
        if (item.Equipment != null)
        {
            nameLabel.AddThemeColorOverride("font_color", item.Equipment.GetRarityColor());
        }
        row.AddChild(nameLabel);

        // Sell price
        var priceLabel = new Label();
        priceLabel.Text = $"+{sellPrice}g";
        priceLabel.AddThemeColorOverride("font_color", GoldColor);
        priceLabel.CustomMinimumSize = new Vector2(70, 0);
        priceLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(priceLabel);

        // Sell button
        var sellBtn = new Button();
        sellBtn.Text = "SELL";
        sellBtn.CustomMinimumSize = new Vector2(70, 28);

        var btnStyle = new StyleBoxFlat();
        btnStyle.BgColor = SellButtonColor;
        btnStyle.SetCornerRadiusAll(4);
        sellBtn.AddThemeStyleboxOverride("normal", btnStyle);

        sellBtn.Pressed += () => OnSellItem(slot, sellPrice);
        row.AddChild(sellBtn);

        return row;
    }

    private void OnBuyItem(ShopItem item)
    {
        var inventory = Inventory3D.Instance;
        if (inventory == null) return;

        // Check if can afford
        if (!inventory.HasGold(item.BuyPrice))
        {
            GD.Print($"[ShopUI3D] Cannot afford {item.DisplayName}");
            return;
        }

        // Check stock
        if (item.Stock == 0)
        {
            GD.Print($"[ShopUI3D] {item.DisplayName} is out of stock");
            return;
        }

        // Spend gold
        if (!inventory.SpendGold(item.BuyPrice))
        {
            return;
        }

        // Create the appropriate item
        InventoryItem? newItem = null;

        if (item.ItemId.StartsWith("weapon_"))
        {
            // Create weapon equipment
            var weapon = ShopItemFactory.CreateWeaponFromShopId(item.ItemId);
            if (weapon != null)
            {
                newItem = InventoryItem.FromEquipment(weapon);
            }
        }
        else if (item.ItemId.StartsWith("armor_"))
        {
            // Create armor equipment
            var armor = ShopItemFactory.CreateArmorFromShopId(item.ItemId);
            if (armor != null)
            {
                newItem = InventoryItem.FromEquipment(armor);
            }
        }
        else
        {
            // Create consumable item
            newItem = CreateConsumableItem(item);
        }

        if (newItem != null && inventory.AddItem(newItem))
        {
            GD.Print($"[ShopUI3D] Bought {item.DisplayName} for {item.BuyPrice}g");

            // Reduce stock if limited
            if (item.Stock > 0)
            {
                item.Stock--;
            }

            // Refresh displays
            RefreshGoldDisplay();
            RefreshCurrentTab();
            RefreshPlayerItems();

            // Play sound
            SoundManager3D.Instance?.PlaySound("coin_pickup");
        }
        else
        {
            // Refund if inventory full
            inventory.AddGold(item.BuyPrice);
            GD.Print($"[ShopUI3D] Inventory full, refunded {item.BuyPrice}g");
        }
    }

    private InventoryItem? CreateConsumableItem(ShopItem shopItem)
    {
        return shopItem.ItemId switch
        {
            "health_potion_small" => new InventoryItem("health_potion_small", "Small Health Potion", "Restores 25 HP.", ItemType.Consumable, 10) { HealAmount = 25 },
            "health_potion_medium" => new InventoryItem("health_potion_medium", "Medium Health Potion", "Restores 50 HP.", ItemType.Consumable, 10) { HealAmount = 50 },
            "health_potion_large" => new InventoryItem("health_potion_large", "Large Health Potion", "Restores 100 HP.", ItemType.Consumable, 10) { HealAmount = 100 },
            "mana_potion_small" => new InventoryItem("mana_potion_small", "Small Mana Potion", "Restores 20 MP.", ItemType.Consumable, 10) { ManaAmount = 20 },
            "mana_potion_medium" => new InventoryItem("mana_potion_medium", "Medium Mana Potion", "Restores 40 MP.", ItemType.Consumable, 10) { ManaAmount = 40 },
            "mana_potion_large" => new InventoryItem("mana_potion_large", "Large Mana Potion", "Restores 75 MP.", ItemType.Consumable, 10) { ManaAmount = 75 },
            "viewers_choice_grenade" => ItemDatabase.CreateViewersChoiceGrenade(),
            "liquid_courage" => ItemDatabase.CreateLiquidCourage(),
            "recall_beacon" => ItemDatabase.CreateRecallBeacon(),
            "monster_musk" => ItemDatabase.CreateMonsterMusk(),
            _ => null
        };
    }

    private void OnSellItem(int slot, int sellPrice)
    {
        var inventory = Inventory3D.Instance;
        if (inventory == null) return;

        // Remove item from inventory
        if (inventory.RemoveItem(slot, 1))
        {
            // Add gold
            inventory.AddGold(sellPrice);

            GD.Print($"[ShopUI3D] Sold item from slot {slot} for {sellPrice}g");

            // Refresh displays
            RefreshGoldDisplay();
            RefreshPlayerItems();

            // Play sound
            SoundManager3D.Instance?.PlaySound("coin_pickup");
        }
    }

    private void OnClosePressed()
    {
        Close();
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}
