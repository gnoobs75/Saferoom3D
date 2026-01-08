using System;
using System.Collections.Generic;
using SafeRoom3D.Core;
using SafeRoom3D.NPC;

namespace SafeRoom3D.Items;

/// <summary>
/// Material tiers for shop weapons and armor.
/// </summary>
public enum MaterialTier
{
    Rusty,      // Tier 1 - cheapest
    Iron,       // Tier 2
    Steel,      // Tier 3
    Adamantium, // Tier 4
    Diamond     // Tier 5 - most expensive
}

/// <summary>
/// Factory for generating shop items with tiered stats and pricing.
/// </summary>
public static class ShopItemFactory
{
    // Tier pricing
    private static readonly Dictionary<MaterialTier, int> TierBasePrices = new()
    {
        { MaterialTier.Rusty, 500 },
        { MaterialTier.Iron, 1500 },
        { MaterialTier.Steel, 4000 },
        { MaterialTier.Adamantium, 10000 },
        { MaterialTier.Diamond, 20000 }
    };

    // Tier stat bonuses
    private static readonly Dictionary<MaterialTier, (int str, int con, int cha)> WeaponTierStats = new()
    {
        { MaterialTier.Rusty, (1, 0, 0) },
        { MaterialTier.Iron, (3, 2, 0) },
        { MaterialTier.Steel, (5, 4, 2) },
        { MaterialTier.Adamantium, (7, 6, 5) },
        { MaterialTier.Diamond, (10, 10, 10) }
    };

    // Armor stat focus by type
    // Robes: INT, CON, DEX (mage)
    // Leather: DEX, CON, STR (rogue)
    // Plate: STR, CON (tank)
    private static readonly Dictionary<MaterialTier, (int primary, int secondary, int tertiary)> ArmorTierStats = new()
    {
        { MaterialTier.Rusty, (1, 0, 0) },
        { MaterialTier.Iron, (3, 2, 0) },
        { MaterialTier.Steel, (5, 4, 2) },
        { MaterialTier.Adamantium, (7, 6, 5) },
        { MaterialTier.Diamond, (10, 10, 10) }
    };

    /// <summary>
    /// Get tier display name
    /// </summary>
    public static string GetTierName(MaterialTier tier) => tier switch
    {
        MaterialTier.Rusty => "Rusty",
        MaterialTier.Iron => "Iron",
        MaterialTier.Steel => "Steel",
        MaterialTier.Adamantium => "Adamantium",
        MaterialTier.Diamond => "Diamond",
        _ => "Unknown"
    };

    /// <summary>
    /// Generate all consumable shop items
    /// </summary>
    public static List<ShopItem> GetConsumables()
    {
        return new List<ShopItem>
        {
            // Health Potions
            new ShopItem { ItemId = "health_potion_small", DisplayName = "Small Health Potion", BuyPrice = 25, Stock = -1 },
            new ShopItem { ItemId = "health_potion_medium", DisplayName = "Medium Health Potion", BuyPrice = 75, Stock = -1 },
            new ShopItem { ItemId = "health_potion_large", DisplayName = "Large Health Potion", BuyPrice = 150, Stock = -1 },

            // Mana Potions
            new ShopItem { ItemId = "mana_potion_small", DisplayName = "Small Mana Potion", BuyPrice = 30, Stock = -1 },
            new ShopItem { ItemId = "mana_potion_medium", DisplayName = "Medium Mana Potion", BuyPrice = 90, Stock = -1 },
            new ShopItem { ItemId = "mana_potion_large", DisplayName = "Large Mana Potion", BuyPrice = 180, Stock = -1 },

            // Special Consumables
            new ShopItem { ItemId = "viewers_choice_grenade", DisplayName = "Viewer's Choice Grenade", BuyPrice = 200, Stock = 5 },
            new ShopItem { ItemId = "liquid_courage", DisplayName = "Liquid Courage", BuyPrice = 300, Stock = 3 },
            new ShopItem { ItemId = "recall_beacon", DisplayName = "Recall Beacon", BuyPrice = 150, Stock = 5 },
            new ShopItem { ItemId = "monster_musk", DisplayName = "Monster Musk", BuyPrice = 250, Stock = 3 },
        };
    }

    /// <summary>
    /// Generate all weapon shop items (all types × all tiers)
    /// </summary>
    public static List<ShopItem> GetWeapons()
    {
        var weapons = new List<ShopItem>();

        // All weapon types
        var weaponTypes = new[]
        {
            (WeaponType.Dagger, "Dagger", false),
            (WeaponType.ShortSword, "Short Sword", false),
            (WeaponType.LongSword, "Long Sword", false),
            (WeaponType.Axe, "Axe", false),
            (WeaponType.Mace, "Mace", false),
            (WeaponType.Club, "Club", false),
            (WeaponType.BattleAxe, "Battle Axe", true),
            (WeaponType.Spear, "Spear", true),
            (WeaponType.WarHammer, "War Hammer", true),
            (WeaponType.Staff, "Staff", true),
            (WeaponType.Bow, "Bow", true),
            (WeaponType.Scythe, "Scythe", true),
        };

        // All tiers
        foreach (MaterialTier tier in Enum.GetValues<MaterialTier>())
        {
            foreach (var (weaponType, name, isTwoHanded) in weaponTypes)
            {
                string tierName = GetTierName(tier);
                string fullName = $"{tierName} {name}";
                string itemId = $"weapon_{tier.ToString().ToLower()}_{weaponType.ToString().ToLower()}";
                int price = TierBasePrices[tier];

                // Two-handed weapons cost 50% more
                if (isTwoHanded) price = (int)(price * 1.5f);

                weapons.Add(new ShopItem
                {
                    ItemId = itemId,
                    DisplayName = fullName,
                    BuyPrice = price,
                    Stock = -1 // Unlimited
                });
            }
        }

        return weapons;
    }

    /// <summary>
    /// Generate all armor shop items (Robes, Leather, Plate × all tiers)
    /// </summary>
    public static List<ShopItem> GetArmor()
    {
        var armor = new List<ShopItem>();

        var armorTypes = new[]
        {
            ("Robes", "robes"),
            ("Leather Armor", "leather"),
            ("Plate Armor", "plate"),
        };

        foreach (MaterialTier tier in Enum.GetValues<MaterialTier>())
        {
            foreach (var (displayName, typeId) in armorTypes)
            {
                string tierName = GetTierName(tier);
                string fullName = $"{tierName} {displayName}";
                string itemId = $"armor_{tier.ToString().ToLower()}_{typeId}";
                int price = TierBasePrices[tier];

                armor.Add(new ShopItem
                {
                    ItemId = itemId,
                    DisplayName = fullName,
                    BuyPrice = price,
                    Stock = -1 // Unlimited
                });
            }
        }

        return armor;
    }

    /// <summary>
    /// Create an EquipmentItem from a shop weapon ID
    /// </summary>
    public static EquipmentItem? CreateWeaponFromShopId(string shopItemId)
    {
        // Parse the ID: weapon_tier_type
        if (!shopItemId.StartsWith("weapon_")) return null;

        var parts = shopItemId.Split('_');
        if (parts.Length < 3) return null;

        // Parse tier
        if (!Enum.TryParse<MaterialTier>(parts[1], true, out var tier)) return null;

        // Parse weapon type
        string typeStr = string.Join("", parts[2..]);
        if (!Enum.TryParse<WeaponType>(typeStr, true, out var weaponType)) return null;

        return CreateTieredWeapon(weaponType, tier);
    }

    /// <summary>
    /// Create an EquipmentItem from a shop armor ID
    /// </summary>
    public static EquipmentItem? CreateArmorFromShopId(string shopItemId)
    {
        // Parse the ID: armor_tier_type
        if (!shopItemId.StartsWith("armor_")) return null;

        var parts = shopItemId.Split('_');
        if (parts.Length < 3) return null;

        // Parse tier
        if (!Enum.TryParse<MaterialTier>(parts[1], true, out var tier)) return null;

        // Parse armor type
        string typeStr = parts[2];

        return CreateTieredArmor(typeStr, tier);
    }

    /// <summary>
    /// Create a tiered weapon with appropriate stats
    /// </summary>
    public static EquipmentItem CreateTieredWeapon(WeaponType weaponType, MaterialTier tier)
    {
        string tierName = GetTierName(tier);
        string weaponName = GetWeaponTypeName(weaponType);
        var stats = WeaponTierStats[tier];

        // Base weapon stats (scale with tier)
        int tierMultiplier = (int)tier + 1;
        var baseStats = GetWeaponBaseStats(weaponType);

        var weapon = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{tierName} {weaponName}",
            Description = $"A {tierName.ToLower()} quality {weaponName.ToLower()}.",
            Slot = EquipmentSlot.MainHand,
            Rarity = GetRarityForTier(tier),
            WeaponType = weaponType,
            ItemLevel = tierMultiplier * 10,
            RequiredLevel = Math.Max(1, tierMultiplier * 5 - 5),
            MinDamage = baseStats.minDmg * tierMultiplier,
            MaxDamage = baseStats.maxDmg * tierMultiplier,
            AttackSpeed = baseStats.speed,
            WeaponRange = baseStats.range,
            IsTwoHanded = baseStats.twoHanded,
            Affixes = new List<ItemAffix>()
        };

        // Add stat affixes based on tier
        if (stats.str > 0)
        {
            weapon.Affixes.Add(new ItemAffix
            {
                Type = AffixType.Strength,
                CurrentValue = stats.str,
                DisplayName = "Strength"
            });
        }
        if (stats.con > 0)
        {
            weapon.Affixes.Add(new ItemAffix
            {
                Type = AffixType.Vitality,
                CurrentValue = stats.con,
                DisplayName = "Constitution"
            });
        }
        if (stats.cha > 0)
        {
            weapon.Affixes.Add(new ItemAffix
            {
                Type = AffixType.Energy,  // Using Energy as Charisma equivalent
                CurrentValue = stats.cha,
                DisplayName = "Charisma"
            });
        }

        return weapon;
    }

    /// <summary>
    /// Create tiered armor with appropriate stats
    /// </summary>
    public static EquipmentItem CreateTieredArmor(string armorTypeId, MaterialTier tier)
    {
        string tierName = GetTierName(tier);
        var stats = ArmorTierStats[tier];
        int tierMultiplier = (int)tier + 1;

        string displayName;
        ArmorType armorType;
        int baseArmor;
        List<ItemAffix> affixes = new();

        switch (armorTypeId.ToLower())
        {
            case "robes":
                displayName = $"{tierName} Robes";
                armorType = ArmorType.Cloth;
                baseArmor = 5 * tierMultiplier;
                // Robes: INT (Energy), CON (Vitality), DEX (Dexterity)
                if (stats.primary > 0) affixes.Add(new ItemAffix { Type = AffixType.Energy, CurrentValue = stats.primary, DisplayName = "Intelligence" });
                if (stats.secondary > 0) affixes.Add(new ItemAffix { Type = AffixType.Vitality, CurrentValue = stats.secondary, DisplayName = "Constitution" });
                if (stats.tertiary > 0) affixes.Add(new ItemAffix { Type = AffixType.Dexterity, CurrentValue = stats.tertiary, DisplayName = "Dexterity" });
                break;

            case "leather":
                displayName = $"{tierName} Leather Armor";
                armorType = ArmorType.Leather;
                baseArmor = 15 * tierMultiplier;
                // Leather: DEX, CON, STR
                if (stats.primary > 0) affixes.Add(new ItemAffix { Type = AffixType.Dexterity, CurrentValue = stats.primary, DisplayName = "Dexterity" });
                if (stats.secondary > 0) affixes.Add(new ItemAffix { Type = AffixType.Vitality, CurrentValue = stats.secondary, DisplayName = "Constitution" });
                if (stats.tertiary > 0) affixes.Add(new ItemAffix { Type = AffixType.Strength, CurrentValue = stats.tertiary, DisplayName = "Strength" });
                break;

            case "plate":
            default:
                displayName = $"{tierName} Plate Armor";
                armorType = ArmorType.Plate;
                baseArmor = 30 * tierMultiplier;
                // Plate: STR, CON (no tertiary)
                if (stats.primary > 0) affixes.Add(new ItemAffix { Type = AffixType.Strength, CurrentValue = stats.primary, DisplayName = "Strength" });
                if (stats.secondary > 0) affixes.Add(new ItemAffix { Type = AffixType.Vitality, CurrentValue = stats.secondary, DisplayName = "Constitution" });
                break;
        }

        return new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = displayName,
            Description = $"{tierName} quality body armor.",
            Slot = EquipmentSlot.Chest,
            Rarity = GetRarityForTier(tier),
            ArmorType = armorType,
            ItemLevel = tierMultiplier * 10,
            RequiredLevel = Math.Max(1, tierMultiplier * 5 - 5),
            Armor = baseArmor,
            Affixes = affixes
        };
    }

    private static ItemRarity GetRarityForTier(MaterialTier tier) => tier switch
    {
        MaterialTier.Rusty => ItemRarity.Normal,
        MaterialTier.Iron => ItemRarity.Normal,
        MaterialTier.Steel => ItemRarity.Magic,
        MaterialTier.Adamantium => ItemRarity.Rare,
        MaterialTier.Diamond => ItemRarity.Unique,
        _ => ItemRarity.Normal
    };

    private static string GetWeaponTypeName(WeaponType type) => type switch
    {
        WeaponType.Dagger => "Dagger",
        WeaponType.ShortSword => "Short Sword",
        WeaponType.LongSword => "Long Sword",
        WeaponType.Axe => "Axe",
        WeaponType.Mace => "Mace",
        WeaponType.Club => "Club",
        WeaponType.BattleAxe => "Battle Axe",
        WeaponType.Spear => "Spear",
        WeaponType.WarHammer => "War Hammer",
        WeaponType.Staff => "Staff",
        WeaponType.Bow => "Bow",
        WeaponType.Scythe => "Scythe",
        _ => "Weapon"
    };

    private static (int minDmg, int maxDmg, float speed, float range, bool twoHanded) GetWeaponBaseStats(WeaponType type) => type switch
    {
        WeaponType.Dagger => (3, 6, 1.5f, 1.5f, false),
        WeaponType.ShortSword => (4, 8, 1.2f, 2.0f, false),
        WeaponType.LongSword => (6, 12, 1.0f, 2.5f, false),
        WeaponType.Axe => (8, 14, 0.9f, 2.0f, false),
        WeaponType.Mace => (7, 13, 0.95f, 2.0f, false),
        WeaponType.Club => (5, 10, 1.0f, 1.8f, false),
        WeaponType.BattleAxe => (12, 22, 0.7f, 3.0f, true),
        WeaponType.Spear => (8, 16, 0.85f, 4.0f, true),
        WeaponType.WarHammer => (14, 26, 0.6f, 2.5f, true),
        WeaponType.Staff => (6, 12, 0.9f, 3.0f, true),
        WeaponType.Bow => (10, 18, 0.8f, 30f, true),
        WeaponType.Scythe => (10, 20, 0.75f, 3.5f, true),
        _ => (5, 10, 1.0f, 2.0f, false)
    };
}
