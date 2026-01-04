using System;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Equipment slot types for the 8-slot equipment system.
/// </summary>
public enum EquipmentSlot
{
    Head,       // Helmets, hoods, crowns
    Chest,      // Body armor, robes
    Hands,      // Gloves, gauntlets
    Feet,       // Boots, greaves
    MainHand,   // Primary weapon
    OffHand,    // Shield, off-hand weapon, or empty for 2H
    Ring1,      // First ring slot
    Ring2       // Second ring slot
}

/// <summary>
/// Item rarity tiers with increasing power and rarity.
/// </summary>
public enum ItemRarity
{
    Normal,     // White - No affixes (60% drop chance)
    Magic,      // Blue - 1-2 affixes (25% drop chance)
    Rare,       // Yellow - 3-4 affixes (12% drop chance)
    Unique,     // Gold - Special fixed affixes (2.5% drop chance)
    Set         // Green - Part of a set with bonus (0.5% drop chance)
}

/// <summary>
/// Weapon archetype for determining base stats and animations.
/// </summary>
public enum WeaponType
{
    None,
    Dagger,         // Fast, low damage
    ShortSword,     // Balanced one-handed
    LongSword,      // Standard one-handed
    Axe,            // High damage one-handed
    BattleAxe,      // Two-handed axe
    Spear,          // Two-handed reach
    Mace,           // One-handed crushing
    WarHammer,      // Two-handed crushing
    Staff,          // Two-handed magic
    Bow,            // Two-handed ranged
    Club,           // Primitive one-handed
    Scythe          // Two-handed slashing
}

/// <summary>
/// Armor type for determining base defense values.
/// </summary>
public enum ArmorType
{
    None,
    Cloth,      // Low armor, may have magic bonuses
    Leather,    // Light armor
    Chain,      // Medium armor
    Plate       // Heavy armor
}

/// <summary>
/// Represents a piece of equipment that can be worn by the player.
/// Extends the base InventoryItem with equipment-specific properties.
/// </summary>
public class EquipmentItem
{
    // Identity
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string BaseItemId { get; set; } = ""; // Reference to base item template

    // Classification
    public EquipmentSlot Slot { get; set; }
    public ItemRarity Rarity { get; set; }
    public WeaponType WeaponType { get; set; } = WeaponType.None;
    public ArmorType ArmorType { get; set; } = ArmorType.None;

    // Level requirements
    public int ItemLevel { get; set; } = 1;      // Determines affix tier rolls
    public int RequiredLevel { get; set; } = 1;   // Player level to equip

    // Weapon base stats
    public int MinDamage { get; set; }
    public int MaxDamage { get; set; }
    public float AttackSpeed { get; set; } = 1.0f;  // Attacks per second multiplier
    public float WeaponRange { get; set; } = 2.5f;  // Melee range
    public bool IsTwoHanded { get; set; }           // Blocks off-hand slot

    // Armor base stats
    public int Armor { get; set; }
    public int BlockChance { get; set; }  // For shields (percentage)
    public int BlockAmount { get; set; }  // Damage blocked when successful

    // Affixes (magic properties rolled on the item)
    public List<ItemAffix> Affixes { get; set; } = new();

    // Set information (if part of a set)
    public string? SetId { get; set; }
    public string? SetName { get; set; }

    // Unique item properties
    public bool IsUnique { get; set; }
    public string? UniqueId { get; set; }  // Reference to unique item definition

    /// <summary>
    /// Calculate the total value of a specific stat from all affixes.
    /// </summary>
    public float GetAffixTotal(AffixType type)
    {
        float total = 0;
        foreach (var affix in Affixes)
        {
            if (affix.Type == type)
            {
                total += affix.CurrentValue;
            }
        }
        return total;
    }

    /// <summary>
    /// Check if item has a specific affix type.
    /// </summary>
    public bool HasAffix(AffixType type)
    {
        foreach (var affix in Affixes)
        {
            if (affix.Type == type) return true;
        }
        return false;
    }

    /// <summary>
    /// Get display color based on rarity.
    /// </summary>
    public Godot.Color GetRarityColor()
    {
        return Rarity switch
        {
            ItemRarity.Normal => new Godot.Color(0.9f, 0.9f, 0.9f),    // White
            ItemRarity.Magic => new Godot.Color(0.4f, 0.6f, 1.0f),     // Blue
            ItemRarity.Rare => new Godot.Color(1.0f, 1.0f, 0.3f),      // Yellow
            ItemRarity.Unique => new Godot.Color(1.0f, 0.7f, 0.2f),    // Gold
            ItemRarity.Set => new Godot.Color(0.3f, 1.0f, 0.3f),       // Green
            _ => new Godot.Color(0.6f, 0.6f, 0.6f)
        };
    }

    /// <summary>
    /// Generate a tooltip description for the item.
    /// </summary>
    public string GetTooltip()
    {
        var sb = new System.Text.StringBuilder();

        // Name with rarity indicator
        sb.AppendLine(Name);
        sb.AppendLine($"[{Rarity}]");
        sb.AppendLine();

        // Weapon stats
        if (Slot == EquipmentSlot.MainHand || Slot == EquipmentSlot.OffHand && WeaponType != WeaponType.None)
        {
            sb.AppendLine($"Damage: {MinDamage}-{MaxDamage}");
            sb.AppendLine($"Attack Speed: {AttackSpeed:F2}");
            if (IsTwoHanded)
                sb.AppendLine("(Two-Handed)");
            sb.AppendLine();
        }

        // Armor stats
        if (Armor > 0)
        {
            sb.AppendLine($"Armor: {Armor}");
        }

        // Shield stats
        if (BlockChance > 0)
        {
            sb.AppendLine($"Block Chance: {BlockChance}%");
            sb.AppendLine($"Block Amount: {BlockAmount}");
        }

        // Affixes
        if (Affixes.Count > 0)
        {
            sb.AppendLine();
            foreach (var affix in Affixes)
            {
                sb.AppendLine(affix.GetDisplayText());
            }
        }

        // Requirements
        sb.AppendLine();
        sb.AppendLine($"Required Level: {RequiredLevel}");
        sb.AppendLine($"Item Level: {ItemLevel}");

        // Set info
        if (!string.IsNullOrEmpty(SetName))
        {
            sb.AppendLine();
            sb.AppendLine($"Set: {SetName}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Create a deep copy of this equipment item.
    /// </summary>
    public EquipmentItem Clone()
    {
        var clone = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name,
            Description = Description,
            BaseItemId = BaseItemId,
            Slot = Slot,
            Rarity = Rarity,
            WeaponType = WeaponType,
            ArmorType = ArmorType,
            ItemLevel = ItemLevel,
            RequiredLevel = RequiredLevel,
            MinDamage = MinDamage,
            MaxDamage = MaxDamage,
            AttackSpeed = AttackSpeed,
            WeaponRange = WeaponRange,
            IsTwoHanded = IsTwoHanded,
            Armor = Armor,
            BlockChance = BlockChance,
            BlockAmount = BlockAmount,
            SetId = SetId,
            SetName = SetName,
            IsUnique = IsUnique,
            UniqueId = UniqueId
        };

        // Deep copy affixes
        foreach (var affix in Affixes)
        {
            clone.Affixes.Add(affix.Clone());
        }

        return clone;
    }
}

/// <summary>
/// Base item template used for generating equipment.
/// </summary>
public class BaseItemTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public EquipmentSlot Slot { get; set; }
    public WeaponType WeaponType { get; set; } = WeaponType.None;
    public ArmorType ArmorType { get; set; } = ArmorType.None;

    // Base stats before affixes
    public int MinDamage { get; set; }
    public int MaxDamage { get; set; }
    public float AttackSpeed { get; set; } = 1.0f;
    public float WeaponRange { get; set; } = 2.5f;
    public bool IsTwoHanded { get; set; }

    public int Armor { get; set; }
    public int BlockChance { get; set; }
    public int BlockAmount { get; set; }

    // Level range for this base item
    public int MinItemLevel { get; set; } = 1;
    public int MaxItemLevel { get; set; } = 50;
}

/// <summary>
/// Unique item definition with fixed affixes and special properties.
/// </summary>
public class UniqueItemDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string BaseItemId { get; set; } = "";
    public int RequiredLevel { get; set; } = 1;

    // Fixed affixes for this unique
    public List<ItemAffix> FixedAffixes { get; set; } = new();

    // Unique special ability description
    public string? SpecialAbility { get; set; }
}

/// <summary>
/// Set definition with bonuses for wearing multiple pieces.
/// </summary>
public class SetDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    // Items in the set (BaseItemIds)
    public List<string> SetItems { get; set; } = new();

    // Bonuses at each piece threshold
    public Dictionary<int, List<ItemAffix>> SetBonuses { get; set; } = new();
}
