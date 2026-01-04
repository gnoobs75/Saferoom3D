using Godot;

namespace SafeRoom3D.Items;

/// <summary>
/// Factory class for creating all item types.
/// </summary>
public static class ItemDatabase
{
    // === POTIONS ===

    public static InventoryItem CreateHealthPotion()
    {
        return new InventoryItem("health_potion", "Health Potion",
            "Restores 100 HP instantly.", ItemType.Consumable, 10)
        {
            HealAmount = 100,
            ManaAmount = 0
        };
    }

    public static InventoryItem CreateManaPotion()
    {
        return new InventoryItem("mana_potion", "Mana Potion",
            "Restores 50 MP instantly.", ItemType.Consumable, 10)
        {
            HealAmount = 0,
            ManaAmount = 50
        };
    }

    // === SPECIAL CONSUMABLES ===

    public static InventoryItem CreateViewersChoiceGrenade()
    {
        return new InventoryItem("viewers_choice_grenade", "Viewer's Choice Grenade",
            "A chaotic grenade with random effects. Could heal you, could hurt enemies, could do something weird!",
            ItemType.Consumable, 5);
    }

    public static InventoryItem CreateLiquidCourage()
    {
        return new InventoryItem("liquid_courage", "Liquid Courage",
            "Grants 3 seconds of complete invulnerability. You can still attack but take no damage.",
            ItemType.Consumable, 5);
    }

    public static InventoryItem CreateRecallBeacon()
    {
        return new InventoryItem("recall_beacon", "Recall Beacon",
            "Teleport back to the center of the arena. Useful for escaping dangerous situations.",
            ItemType.Consumable, 3);
    }

    public static InventoryItem CreateMonsterMusk()
    {
        return new InventoryItem("monster_musk", "Monster Musk",
            "Attracts all enemies toward you for 10 seconds. Dangerous but great for grouping enemies!",
            ItemType.Consumable, 5);
    }

    // === LOOT ITEMS ===

    public static InventoryItem CreateGoldCoins()
    {
        return new InventoryItem("gold_coins", "Gold Coins",
            "Shiny gold coins. The currency of adventurers.", ItemType.Material, 9999);
    }

    public static InventoryItem CreateGoblinTooth()
    {
        return new InventoryItem("goblin_tooth", "Goblin Tooth",
            "A jagged tooth from a goblin. Crafting material.", ItemType.Material, 50);
    }

    public static InventoryItem CreateRatTail()
    {
        return new InventoryItem("rat_tail", "Rat Tail",
            "A disgusting tail from a dungeon rat. Surprisingly valuable to alchemists.",
            ItemType.Material, 50);
    }

    public static InventoryItem CreateBrokenArrow()
    {
        return new InventoryItem("broken_arrow", "Broken Arrow",
            "A snapped arrow shaft. The fletching is still usable.", ItemType.Material, 50);
    }

    public static InventoryItem CreateMysteryMeat()
    {
        return new InventoryItem("mystery_meat", "Mystery Meat",
            "Questionable meat from an unknown creature. Eat at your own risk.",
            ItemType.Consumable, 20)
        {
            HealAmount = 25
        };
    }

    public static InventoryItem CreateEnchantedDust()
    {
        return new InventoryItem("enchanted_dust", "Enchanted Dust",
            "Sparkling magical residue. Used in enchanting and alchemy.",
            ItemType.Material, 99);
    }

    public static InventoryItem CreateGemstone()
    {
        return new InventoryItem("gemstone", "Gemstone",
            "A beautiful gemstone of unknown origin. Worth a fair bit of gold.",
            ItemType.Material, 20);
    }

    public static InventoryItem CreateAncientRelic()
    {
        return new InventoryItem("ancient_relic", "Ancient Relic",
            "A mysterious artifact from a forgotten age. Scholars would pay handsomely for this.",
            ItemType.Quest, 5);
    }

    public static InventoryItem CreateDragonScale()
    {
        return new InventoryItem("dragon_scale", "Dragon Scale",
            "A single scale from a powerful dragon. Extremely rare and valuable.",
            ItemType.Material, 10);
    }

    // === ITEM ICONS ===

    /// <summary>
    /// Get a procedurally generated icon for an item.
    /// </summary>
    public static ImageTexture GetIcon(string itemId)
    {
        return ItemIcons.GetIcon(itemId);
    }

    /// <summary>
    /// Get color associated with an item type.
    /// </summary>
    public static Color GetItemColor(string itemId)
    {
        return itemId switch
        {
            "health_potion" => new Color(0.9f, 0.2f, 0.2f),
            "mana_potion" => new Color(0.2f, 0.4f, 0.9f),
            "viewers_choice_grenade" => new Color(1f, 0.8f, 0.2f),
            "liquid_courage" => new Color(1f, 0.7f, 0.3f),
            "recall_beacon" => new Color(0.4f, 0.8f, 1f),
            "monster_musk" => new Color(0.5f, 0.8f, 0.3f),
            // Loot items
            "gold_coins" => new Color(1f, 0.85f, 0.2f),
            "goblin_tooth" => new Color(0.85f, 0.8f, 0.7f),
            "rat_tail" => new Color(0.6f, 0.45f, 0.4f),
            "broken_arrow" => new Color(0.5f, 0.4f, 0.3f),
            "mystery_meat" => new Color(0.7f, 0.3f, 0.3f),
            "enchanted_dust" => new Color(0.8f, 0.6f, 1f),
            "gemstone" => new Color(0.4f, 0.9f, 0.7f),
            "ancient_relic" => new Color(0.7f, 0.5f, 0.2f),
            "dragon_scale" => new Color(0.8f, 0.2f, 0.2f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };
    }
}

/// <summary>
/// Generates procedural icons for items.
/// </summary>
public static class ItemIcons
{
    private static readonly System.Collections.Generic.Dictionary<string, ImageTexture> _iconCache = new();
    public const int IconSize = 48;

    public static ImageTexture GetIcon(string itemId)
    {
        if (_iconCache.TryGetValue(itemId, out var cached))
            return cached;

        var icon = CreateIcon(itemId);
        _iconCache[itemId] = icon;
        return icon;
    }

    private static ImageTexture CreateIcon(string itemId)
    {
        var image = Image.CreateEmpty(IconSize, IconSize, false, Image.Format.Rgba8);

        var color = ItemDatabase.GetItemColor(itemId);
        var darkColor = color.Darkened(0.3f);
        var lightColor = color.Lightened(0.3f);

        // Fill background with gradient
        for (int y = 0; y < IconSize; y++)
        {
            for (int x = 0; x < IconSize; x++)
            {
                float t = (float)y / IconSize;
                var bgColor = color.Lerp(darkColor, t * 0.5f);
                float edgeDist = Mathf.Min(Mathf.Min(x, y), Mathf.Min(IconSize - x - 1, IconSize - y - 1));
                float edgeFade = Mathf.Clamp(edgeDist / 6f, 0, 1);
                bgColor = bgColor.Darkened(1f - edgeFade * 0.3f);
                image.SetPixel(x, y, bgColor);
            }
        }

        // Draw symbol based on item type
        DrawItemSymbol(image, itemId, lightColor);

        // Add border
        DrawBorder(image, darkColor);

        return ImageTexture.CreateFromImage(image);
    }

    private static void DrawItemSymbol(Image image, string itemId, Color color)
    {
        int cx = IconSize / 2;
        int cy = IconSize / 2;

        switch (itemId)
        {
            case "health_potion":
                DrawPotion(image, cx, cy, color, new Color(0.9f, 0.3f, 0.3f));
                break;
            case "mana_potion":
                DrawPotion(image, cx, cy, color, new Color(0.3f, 0.5f, 0.9f));
                break;
            case "viewers_choice_grenade":
                DrawGrenade(image, cx, cy, color);
                break;
            case "liquid_courage":
                DrawFlask(image, cx, cy, color);
                break;
            case "recall_beacon":
                DrawBeacon(image, cx, cy, color);
                break;
            case "monster_musk":
                DrawMusk(image, cx, cy, color);
                break;
            default:
                DrawQuestion(image, cx, cy, color);
                break;
        }
    }

    private static void DrawPotion(Image image, int cx, int cy, Color color, Color liquid)
    {
        // Bottle shape
        for (int y = -12; y <= 12; y++)
        {
            int width;
            if (y < -8) width = 3; // Neck
            else if (y < -5) width = 3 + (y + 8); // Shoulder
            else width = 8; // Body

            for (int x = -width; x <= width; x++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                {
                    if (Mathf.Abs(x) == width || y == 12 || y == -12)
                        image.SetPixel(px, py, color);
                    else if (y > -5)
                        image.SetPixel(px, py, liquid);
                }
            }
        }
    }

    private static void DrawGrenade(Image image, int cx, int cy, Color color)
    {
        // Round body
        for (int y = -8; y <= 8; y++)
        {
            for (int x = -8; x <= 8; x++)
            {
                if (x * x + y * y <= 64)
                {
                    int px = cx + x;
                    int py = cy + y + 2;
                    if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                        image.SetPixel(px, py, new Color(0.4f, 0.4f, 0.4f));
                }
            }
        }

        // Question mark
        DrawLine(image, cx - 3, cy - 4, cx + 3, cy - 4, color, 2);
        DrawLine(image, cx + 3, cy - 4, cx + 3, cy, color, 2);
        DrawLine(image, cx + 3, cy, cx, cy, color, 2);
        DrawLine(image, cx, cy, cx, cy + 3, color, 2);
        FillCircle(image, cx, cy + 6, 2, color);

        // Fuse
        DrawLine(image, cx, cy - 7, cx, cy - 12, color, 1);
        FillCircle(image, cx, cy - 13, 2, new Color(1f, 0.8f, 0.3f));
    }

    private static void DrawFlask(Image image, int cx, int cy, Color color)
    {
        // Bottle shape with cork
        for (int y = -12; y <= 10; y++)
        {
            int width;
            if (y < -10) width = 2; // Cork
            else if (y < -6) width = 3; // Neck
            else width = (int)(4 + (y + 6) * 0.5f); // Widening body

            for (int x = -width; x <= width; x++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                {
                    if (y < -10)
                        image.SetPixel(px, py, new Color(0.5f, 0.35f, 0.2f)); // Cork
                    else
                        image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static void DrawBeacon(Image image, int cx, int cy, Color color)
    {
        // Beacon rings
        for (int r = 4; r <= 12; r += 4)
        {
            for (float a = 0; a < Mathf.Tau; a += 0.1f)
            {
                int x = cx + (int)(Mathf.Cos(a) * r);
                int y = cy + (int)(Mathf.Sin(a) * r);
                if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
                    image.SetPixel(x, y, color);
            }
        }

        // Center dot
        FillCircle(image, cx, cy, 3, color);
    }

    private static void DrawMusk(Image image, int cx, int cy, Color color)
    {
        // Cloud puffs
        FillCircle(image, cx - 5, cy - 3, 5, color);
        FillCircle(image, cx + 5, cy - 3, 5, color);
        FillCircle(image, cx, cy + 3, 6, color);
        FillCircle(image, cx - 8, cy + 2, 4, color);
        FillCircle(image, cx + 8, cy + 2, 4, color);
    }

    private static void DrawQuestion(Image image, int cx, int cy, Color color)
    {
        DrawLine(image, cx - 4, cy - 8, cx + 4, cy - 8, color, 1);
        DrawLine(image, cx + 4, cy - 8, cx + 4, cy - 2, color, 1);
        DrawLine(image, cx + 4, cy - 2, cx, cy - 2, color, 1);
        DrawLine(image, cx, cy - 2, cx, cy + 2, color, 1);
        FillCircle(image, cx, cy + 6, 2, color);
    }

    private static void DrawLine(Image image, int x1, int y1, int x2, int y2, Color color, int thickness)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = Mathf.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
            {
                for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                {
                    int px = x1 + tx;
                    int py = y1 + ty;
                    if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                        image.SetPixel(px, py, color);
                }
            }

            if (x1 == x2 && y1 == y2) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x1 += sx; }
            if (e2 < dx) { err += dx; y1 += sy; }
        }
    }

    private static void FillCircle(Image image, int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                        image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static void DrawBorder(Image image, Color color)
    {
        for (int i = 0; i < IconSize; i++)
        {
            image.SetPixel(i, 0, color);
            image.SetPixel(i, IconSize - 1, color);
            image.SetPixel(0, i, color);
            image.SetPixel(IconSize - 1, i, color);
        }
    }
}
