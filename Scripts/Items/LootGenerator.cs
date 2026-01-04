using System;
using System.Collections.Generic;
using Godot;
using SafeRoom3D.Core;

namespace SafeRoom3D.Items;

/// <summary>
/// Generates random equipment with appropriate stats and affixes.
/// </summary>
public static class LootGenerator
{
    private static readonly Random _random = new();

    // Base item tables
    private static readonly List<BaseItemTemplate> _weaponTemplates = new();
    private static readonly List<BaseItemTemplate> _armorTemplates = new();
    private static readonly List<BaseItemTemplate> _accessoryTemplates = new();

    private static bool _initialized = false;

    /// <summary>
    /// Initialize the loot generator with base item templates.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        InitializeWeapons();
        InitializeArmor();
        InitializeAccessories();

        GD.Print($"[LootGenerator] Initialized with {_weaponTemplates.Count} weapons, {_armorTemplates.Count} armor, {_accessoryTemplates.Count} accessories");
    }

    private static void InitializeWeapons()
    {
        // === DAGGERS ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "rusty_dagger", Name = "Rusty Dagger", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Dagger, MinDamage = 2, MaxDamage = 5, AttackSpeed = 1.5f,
            MinItemLevel = 1, MaxItemLevel = 10
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "iron_dagger", Name = "Iron Dagger", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Dagger, MinDamage = 4, MaxDamage = 8, AttackSpeed = 1.5f,
            MinItemLevel = 8, MaxItemLevel = 20
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "steel_stiletto", Name = "Steel Stiletto", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Dagger, MinDamage = 7, MaxDamage = 14, AttackSpeed = 1.6f,
            MinItemLevel = 18, MaxItemLevel = 35
        });

        // === SHORT SWORDS ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "short_sword", Name = "Short Sword", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.ShortSword, MinDamage = 4, MaxDamage = 8, AttackSpeed = 1.2f,
            MinItemLevel = 1, MaxItemLevel = 12
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "gladius", Name = "Gladius", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.ShortSword, MinDamage = 6, MaxDamage = 12, AttackSpeed = 1.2f,
            MinItemLevel = 10, MaxItemLevel = 25
        });

        // === LONG SWORDS ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "long_sword", Name = "Long Sword", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.LongSword, MinDamage = 8, MaxDamage = 14, AttackSpeed = 1.0f,
            MinItemLevel = 5, MaxItemLevel = 20
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "bastard_sword", Name = "Bastard Sword", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.LongSword, MinDamage = 12, MaxDamage = 20, AttackSpeed = 1.0f,
            MinItemLevel = 15, MaxItemLevel = 35
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "warlord_blade", Name = "Warlord Blade", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.LongSword, MinDamage = 18, MaxDamage = 30, AttackSpeed = 1.0f,
            MinItemLevel = 25, MaxItemLevel = 50
        });

        // === AXES ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "hand_axe", Name = "Hand Axe", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Axe, MinDamage = 6, MaxDamage = 12, AttackSpeed = 0.9f,
            MinItemLevel = 1, MaxItemLevel = 15
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "war_axe", Name = "War Axe", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Axe, MinDamage = 10, MaxDamage = 18, AttackSpeed = 0.85f,
            MinItemLevel = 12, MaxItemLevel = 30
        });

        // === BATTLE AXES (2H) ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "battle_axe", Name = "Battle Axe", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.BattleAxe, MinDamage = 14, MaxDamage = 26, AttackSpeed = 0.7f,
            IsTwoHanded = true, MinItemLevel = 10, MaxItemLevel = 25
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "executioner_axe", Name = "Executioner's Axe", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.BattleAxe, MinDamage = 25, MaxDamage = 45, AttackSpeed = 0.65f,
            IsTwoHanded = true, MinItemLevel = 25, MaxItemLevel = 50
        });

        // === MACES ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "wooden_club", Name = "Wooden Club", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Club, MinDamage = 5, MaxDamage = 10, AttackSpeed = 0.95f,
            MinItemLevel = 1, MaxItemLevel = 10
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "iron_mace", Name = "Iron Mace", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Mace, MinDamage = 8, MaxDamage = 16, AttackSpeed = 0.9f,
            MinItemLevel = 8, MaxItemLevel = 22
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "flanged_mace", Name = "Flanged Mace", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Mace, MinDamage = 14, MaxDamage = 24, AttackSpeed = 0.85f,
            MinItemLevel = 18, MaxItemLevel = 38
        });

        // === WAR HAMMERS (2H) ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "war_hammer", Name = "War Hammer", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.WarHammer, MinDamage = 18, MaxDamage = 35, AttackSpeed = 0.6f,
            IsTwoHanded = true, MinItemLevel = 15, MaxItemLevel = 35
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "doom_hammer", Name = "Doom Hammer", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.WarHammer, MinDamage = 30, MaxDamage = 55, AttackSpeed = 0.55f,
            IsTwoHanded = true, MinItemLevel = 30, MaxItemLevel = 50
        });

        // === SPEARS (2H) ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "spear", Name = "Spear", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Spear, MinDamage = 10, MaxDamage = 18, AttackSpeed = 0.8f,
            WeaponRange = 3.5f, IsTwoHanded = true, MinItemLevel = 5, MaxItemLevel = 20
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "pike", Name = "Pike", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Spear, MinDamage = 16, MaxDamage = 28, AttackSpeed = 0.75f,
            WeaponRange = 4f, IsTwoHanded = true, MinItemLevel = 18, MaxItemLevel = 40
        });

        // === STAVES (2H) ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "apprentice_staff", Name = "Apprentice Staff", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Staff, MinDamage = 4, MaxDamage = 10, AttackSpeed = 0.9f,
            IsTwoHanded = true, MinItemLevel = 1, MaxItemLevel = 15
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "wizard_staff", Name = "Wizard Staff", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Staff, MinDamage = 8, MaxDamage = 18, AttackSpeed = 0.85f,
            IsTwoHanded = true, MinItemLevel = 12, MaxItemLevel = 30
        });
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "archmage_staff", Name = "Archmage Staff", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Staff, MinDamage = 14, MaxDamage = 28, AttackSpeed = 0.8f,
            IsTwoHanded = true, MinItemLevel = 25, MaxItemLevel = 50
        });

        // === SCYTHES (2H) ===
        _weaponTemplates.Add(new BaseItemTemplate
        {
            Id = "scythe", Name = "Scythe", Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Scythe, MinDamage = 20, MaxDamage = 38, AttackSpeed = 0.65f,
            WeaponRange = 3f, IsTwoHanded = true, MinItemLevel = 20, MaxItemLevel = 45
        });
    }

    private static void InitializeArmor()
    {
        // === HEAD ===
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "cloth_hood", Name = "Cloth Hood", Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Cloth, Armor = 3, MinItemLevel = 1, MaxItemLevel = 15
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "leather_cap", Name = "Leather Cap", Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Leather, Armor = 8, MinItemLevel = 5, MaxItemLevel = 20
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "chain_coif", Name = "Chain Coif", Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Chain, Armor = 15, MinItemLevel = 12, MaxItemLevel = 30
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "plate_helm", Name = "Plate Helm", Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Plate, Armor = 25, MinItemLevel = 22, MaxItemLevel = 45
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "great_helm", Name = "Great Helm", Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Plate, Armor = 35, MinItemLevel = 35, MaxItemLevel = 50
        });

        // === CHEST ===
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "tattered_robe", Name = "Tattered Robe", Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Cloth, Armor = 5, MinItemLevel = 1, MaxItemLevel = 12
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "cloth_robe", Name = "Cloth Robe", Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Cloth, Armor = 10, MinItemLevel = 8, MaxItemLevel = 25
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "leather_armor", Name = "Leather Armor", Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Leather, Armor = 20, MinItemLevel = 5, MaxItemLevel = 22
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "studded_leather", Name = "Studded Leather", Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Leather, Armor = 30, MinItemLevel = 15, MaxItemLevel = 35
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "chain_mail", Name = "Chain Mail", Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Chain, Armor = 45, MinItemLevel = 12, MaxItemLevel = 32
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "plate_armor", Name = "Plate Armor", Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Plate, Armor = 65, MinItemLevel = 25, MaxItemLevel = 45
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "full_plate", Name = "Full Plate", Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Plate, Armor = 85, MinItemLevel = 38, MaxItemLevel = 50
        });

        // === HANDS ===
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "cloth_gloves", Name = "Cloth Gloves", Slot = EquipmentSlot.Hands,
            ArmorType = ArmorType.Cloth, Armor = 2, MinItemLevel = 1, MaxItemLevel = 15
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "leather_gloves", Name = "Leather Gloves", Slot = EquipmentSlot.Hands,
            ArmorType = ArmorType.Leather, Armor = 5, MinItemLevel = 5, MaxItemLevel = 22
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "chain_gloves", Name = "Chain Gloves", Slot = EquipmentSlot.Hands,
            ArmorType = ArmorType.Chain, Armor = 10, MinItemLevel = 12, MaxItemLevel = 32
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "plate_gauntlets", Name = "Plate Gauntlets", Slot = EquipmentSlot.Hands,
            ArmorType = ArmorType.Plate, Armor = 18, MinItemLevel = 25, MaxItemLevel = 50
        });

        // === FEET ===
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "cloth_shoes", Name = "Cloth Shoes", Slot = EquipmentSlot.Feet,
            ArmorType = ArmorType.Cloth, Armor = 2, MinItemLevel = 1, MaxItemLevel = 15
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "leather_boots", Name = "Leather Boots", Slot = EquipmentSlot.Feet,
            ArmorType = ArmorType.Leather, Armor = 6, MinItemLevel = 5, MaxItemLevel = 22
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "chain_boots", Name = "Chain Boots", Slot = EquipmentSlot.Feet,
            ArmorType = ArmorType.Chain, Armor = 12, MinItemLevel = 12, MaxItemLevel = 32
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "plate_greaves", Name = "Plate Greaves", Slot = EquipmentSlot.Feet,
            ArmorType = ArmorType.Plate, Armor = 20, MinItemLevel = 25, MaxItemLevel = 50
        });

        // === SHIELDS (Off-Hand) ===
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "wooden_buckler", Name = "Wooden Buckler", Slot = EquipmentSlot.OffHand,
            ArmorType = ArmorType.Leather, Armor = 5, BlockChance = 10, BlockAmount = 5,
            MinItemLevel = 1, MaxItemLevel = 12
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "round_shield", Name = "Round Shield", Slot = EquipmentSlot.OffHand,
            ArmorType = ArmorType.Chain, Armor = 12, BlockChance = 15, BlockAmount = 10,
            MinItemLevel = 8, MaxItemLevel = 25
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "kite_shield", Name = "Kite Shield", Slot = EquipmentSlot.OffHand,
            ArmorType = ArmorType.Plate, Armor = 22, BlockChance = 20, BlockAmount = 18,
            MinItemLevel = 18, MaxItemLevel = 38
        });
        _armorTemplates.Add(new BaseItemTemplate
        {
            Id = "tower_shield", Name = "Tower Shield", Slot = EquipmentSlot.OffHand,
            ArmorType = ArmorType.Plate, Armor = 35, BlockChance = 28, BlockAmount = 30,
            MinItemLevel = 30, MaxItemLevel = 50
        });
    }

    private static void InitializeAccessories()
    {
        // === RINGS ===
        _accessoryTemplates.Add(new BaseItemTemplate
        {
            Id = "iron_ring", Name = "Iron Ring", Slot = EquipmentSlot.Ring1,
            MinItemLevel = 1, MaxItemLevel = 50
        });
        _accessoryTemplates.Add(new BaseItemTemplate
        {
            Id = "silver_ring", Name = "Silver Ring", Slot = EquipmentSlot.Ring1,
            MinItemLevel = 10, MaxItemLevel = 50
        });
        _accessoryTemplates.Add(new BaseItemTemplate
        {
            Id = "gold_ring", Name = "Gold Ring", Slot = EquipmentSlot.Ring1,
            MinItemLevel = 20, MaxItemLevel = 50
        });
    }

    /// <summary>
    /// Generate a random piece of equipment appropriate for the monster level.
    /// </summary>
    public static EquipmentItem? GenerateEquipment(int monsterLevel, float magicFind = 0)
    {
        Initialize();

        // Determine item level (monster level +/- 2)
        int itemLevel = monsterLevel + _random.Next(-2, 3);
        itemLevel = Math.Clamp(itemLevel, 1, 50);

        // Roll rarity (affected by magic find)
        ItemRarity rarity = RollRarity(magicFind);

        // Pick random equipment slot
        EquipmentSlot slot = GetRandomSlot();

        // Pick base item for that slot
        var baseItem = GetRandomBaseItem(slot, itemLevel);
        if (baseItem == null)
        {
            GD.Print($"[LootGenerator] No base item found for slot {slot} at level {itemLevel}");
            return null;
        }

        // Generate affixes based on rarity
        int affixCount = GetAffixCount(rarity);
        var affixes = AffixDatabase.GenerateAffixes(slot, itemLevel, affixCount);

        // Generate name
        string name = GenerateName(baseItem.Name, affixes, rarity);

        // Create the equipment item
        var item = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = GenerateDescription(baseItem, rarity),
            BaseItemId = baseItem.Id,
            Slot = slot,
            Rarity = rarity,
            WeaponType = baseItem.WeaponType,
            ArmorType = baseItem.ArmorType,
            ItemLevel = itemLevel,
            RequiredLevel = Math.Max(1, itemLevel - 5),
            MinDamage = baseItem.MinDamage,
            MaxDamage = baseItem.MaxDamage,
            AttackSpeed = baseItem.AttackSpeed,
            WeaponRange = baseItem.WeaponRange,
            IsTwoHanded = baseItem.IsTwoHanded,
            Armor = baseItem.Armor,
            BlockChance = baseItem.BlockChance,
            BlockAmount = baseItem.BlockAmount,
            Affixes = affixes
        };

        GD.Print($"[LootGenerator] Generated: {item.Name} ({rarity}) iLvl {itemLevel} with {affixes.Count} affixes");

        return item;
    }

    /// <summary>
    /// Generate equipment for a specific slot.
    /// </summary>
    public static EquipmentItem? GenerateForSlot(EquipmentSlot slot, int monsterLevel, float magicFind = 0)
    {
        Initialize();

        int itemLevel = monsterLevel + _random.Next(-2, 3);
        itemLevel = Math.Clamp(itemLevel, 1, 50);

        ItemRarity rarity = RollRarity(magicFind);

        var baseItem = GetRandomBaseItem(slot, itemLevel);
        if (baseItem == null) return null;

        int affixCount = GetAffixCount(rarity);
        var affixes = AffixDatabase.GenerateAffixes(slot, itemLevel, affixCount);

        string name = GenerateName(baseItem.Name, affixes, rarity);

        return new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = GenerateDescription(baseItem, rarity),
            BaseItemId = baseItem.Id,
            Slot = slot,
            Rarity = rarity,
            WeaponType = baseItem.WeaponType,
            ArmorType = baseItem.ArmorType,
            ItemLevel = itemLevel,
            RequiredLevel = Math.Max(1, itemLevel - 5),
            MinDamage = baseItem.MinDamage,
            MaxDamage = baseItem.MaxDamage,
            AttackSpeed = baseItem.AttackSpeed,
            WeaponRange = baseItem.WeaponRange,
            IsTwoHanded = baseItem.IsTwoHanded,
            Armor = baseItem.Armor,
            BlockChance = baseItem.BlockChance,
            BlockAmount = baseItem.BlockAmount,
            Affixes = affixes
        };
    }

    private static ItemRarity RollRarity(float magicFind)
    {
        float roll = _random.NextSingle() * 100f;
        float mfBonus = 1f + (magicFind / 100f);

        // Adjusted chances with magic find
        if (roll < 0.5f * mfBonus) return ItemRarity.Set;
        if (roll < 3f * mfBonus) return ItemRarity.Unique;
        if (roll < 15f * mfBonus) return ItemRarity.Rare;
        if (roll < 40f * mfBonus) return ItemRarity.Magic;
        return ItemRarity.Normal;
    }

    private static EquipmentSlot GetRandomSlot()
    {
        var slots = new[]
        {
            EquipmentSlot.Head,
            EquipmentSlot.Chest,
            EquipmentSlot.Hands,
            EquipmentSlot.Feet,
            EquipmentSlot.MainHand,
            EquipmentSlot.OffHand,
            EquipmentSlot.Ring1
        };
        return slots[_random.Next(slots.Length)];
    }

    private static BaseItemTemplate? GetRandomBaseItem(EquipmentSlot slot, int itemLevel)
    {
        List<BaseItemTemplate> pool;

        if (slot == EquipmentSlot.MainHand)
        {
            pool = _weaponTemplates.FindAll(t => t.MinItemLevel <= itemLevel && t.MaxItemLevel >= itemLevel);
        }
        else if (slot == EquipmentSlot.Ring1 || slot == EquipmentSlot.Ring2)
        {
            pool = _accessoryTemplates.FindAll(t => t.MinItemLevel <= itemLevel && t.MaxItemLevel >= itemLevel);
        }
        else
        {
            pool = _armorTemplates.FindAll(t =>
                t.Slot == slot && t.MinItemLevel <= itemLevel && t.MaxItemLevel >= itemLevel);
        }

        if (pool.Count == 0) return null;

        return pool[_random.Next(pool.Count)];
    }

    private static int GetAffixCount(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Normal => 0,
            ItemRarity.Magic => _random.Next(1, 3),  // 1-2
            ItemRarity.Rare => _random.Next(3, 5),   // 3-4
            ItemRarity.Unique => 3,                   // Fixed 3 for uniques
            ItemRarity.Set => 2,                      // Fixed 2 for set items
            _ => 0
        };
    }

    private static string GenerateName(string baseName, List<ItemAffix> affixes, ItemRarity rarity)
    {
        if (rarity == ItemRarity.Normal || affixes.Count == 0)
        {
            return baseName;
        }

        // Find prefix and suffix
        string? prefix = null;
        string? suffix = null;

        foreach (var affix in affixes)
        {
            if (affix.IsPrefix && prefix == null)
            {
                prefix = affix.NamePart;
            }
            else if (!affix.IsPrefix && suffix == null)
            {
                suffix = affix.NamePart;
            }
        }

        var parts = new List<string>();
        if (prefix != null) parts.Add(prefix);
        parts.Add(baseName);
        if (suffix != null) parts.Add(suffix);

        return string.Join(" ", parts);
    }

    private static string GenerateDescription(BaseItemTemplate baseItem, ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Unique => "A legendary item of great power.",
            ItemRarity.Set => "Part of an ancient set.",
            ItemRarity.Rare => "A finely crafted item with multiple enchantments.",
            ItemRarity.Magic => "An enchanted item.",
            _ => "A common item."
        };
    }

    /// <summary>
    /// Roll whether to drop equipment based on monster level and drop chance.
    /// </summary>
    public static bool ShouldDropEquipment(int monsterLevel, bool isBoss = false)
    {
        // Base drop chance: 15% for regular enemies, 100% for bosses
        float baseChance = isBoss ? 1.0f : 0.15f;

        // Higher level monsters have slightly better drop rates
        float levelBonus = Math.Min(0.15f, monsterLevel * 0.005f);

        return _random.NextSingle() < (baseChance + levelBonus);
    }
}
