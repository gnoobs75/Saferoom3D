using System;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Affix type categories for organizing stat bonuses.
/// </summary>
public enum AffixCategory
{
    Offensive,      // Damage, crit, attack speed
    Defensive,      // Armor, health, block
    Utility,        // Movement, magic find, XP
    Elemental,      // Fire/ice/lightning damage and resistance
    Resource,       // Mana, health/mana regen
    Attribute       // Primary stats (STR, DEX, VIT, ENE)
}

/// <summary>
/// Specific affix types that can roll on equipment.
/// </summary>
public enum AffixType
{
    // === Attributes ===
    Strength,           // +X Strength
    Dexterity,          // +X Dexterity
    Vitality,           // +X Vitality
    Energy,             // +X Energy
    AllAttributes,      // +X to all attributes

    // === Offensive ===
    FlatDamage,         // +X Damage (min and max)
    DamagePercent,      // +X% Damage
    AttackSpeed,        // +X% Attack Speed
    CriticalChance,     // +X% Critical Chance
    CriticalDamage,     // +X% Critical Damage
    LifeOnHit,          // +X Life on Hit
    ManaOnHit,          // +X Mana on Hit
    ArmorPenetration,   // +X% Armor Penetration

    // === Defensive ===
    FlatArmor,          // +X Armor
    ArmorPercent,       // +X% Armor
    FlatHealth,         // +X Health
    HealthPercent,      // +X% Health
    BlockChance,        // +X% Block Chance
    BlockAmount,        // +X Block Amount
    DodgeChance,        // +X% Dodge Chance
    DamageReduction,    // +X% Damage Reduction

    // === Resource ===
    FlatMana,           // +X Mana
    ManaPercent,        // +X% Mana
    HealthRegen,        // +X Health/sec
    ManaRegen,          // +X Mana/sec
    AbilityCostReduction, // -X% Ability Cost

    // === Utility ===
    MovementSpeed,      // +X% Movement Speed
    MagicFind,          // +X% Magic Find
    ExperienceBonus,    // +X% Experience
    GoldFind,           // +X% Gold Find
    ItemQuantity,       // +X% Item Quantity

    // === Elemental Damage ===
    FireDamage,         // +X Fire Damage
    ColdDamage,         // +X Cold Damage
    LightningDamage,    // +X Lightning Damage
    PoisonDamage,       // +X Poison Damage
    FireDamagePercent,  // +X% Fire Damage
    ColdDamagePercent,  // +X% Cold Damage
    LightningDamagePercent, // +X% Lightning Damage
    PoisonDamagePercent,    // +X% Poison Damage

    // === Elemental Resistance ===
    FireResistance,     // +X% Fire Resistance
    ColdResistance,     // +X% Cold Resistance
    LightningResistance, // +X% Lightning Resistance
    PoisonResistance,   // +X% Poison Resistance
    AllResistance       // +X% All Resistances
}

/// <summary>
/// Represents a magic affix (modifier) on an equipment item.
/// </summary>
public class ItemAffix
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public AffixType Type { get; set; }
    public AffixCategory Category { get; set; }

    // Value range for this affix tier
    public float MinValue { get; set; }
    public float MaxValue { get; set; }

    // The rolled value for this instance
    public float CurrentValue { get; set; }

    // Minimum item level required for this affix tier
    public int TierRequired { get; set; } = 1;

    // Whether this is a prefix or suffix (for name generation)
    public bool IsPrefix { get; set; }

    // Name part for item naming (e.g., "of the Bear" or "Fiery")
    public string NamePart { get; set; } = "";

    /// <summary>
    /// Get display text for the affix (e.g., "+15 Strength" or "+25% Damage")
    /// </summary>
    public string GetDisplayText()
    {
        string sign = CurrentValue >= 0 ? "+" : "";
        bool isPercent = IsPercentageAffix();

        if (isPercent)
        {
            return $"{sign}{CurrentValue:F0}% {GetStatName()}";
        }
        else
        {
            return $"{sign}{CurrentValue:F0} {GetStatName()}";
        }
    }

    /// <summary>
    /// Check if this affix type uses percentage values.
    /// </summary>
    public bool IsPercentageAffix()
    {
        return Type switch
        {
            AffixType.DamagePercent => true,
            AffixType.AttackSpeed => true,
            AffixType.CriticalChance => true,
            AffixType.CriticalDamage => true,
            AffixType.ArmorPercent => true,
            AffixType.HealthPercent => true,
            AffixType.BlockChance => true,
            AffixType.DodgeChance => true,
            AffixType.DamageReduction => true,
            AffixType.ManaPercent => true,
            AffixType.AbilityCostReduction => true,
            AffixType.MovementSpeed => true,
            AffixType.MagicFind => true,
            AffixType.ExperienceBonus => true,
            AffixType.GoldFind => true,
            AffixType.ItemQuantity => true,
            AffixType.FireDamagePercent => true,
            AffixType.ColdDamagePercent => true,
            AffixType.LightningDamagePercent => true,
            AffixType.PoisonDamagePercent => true,
            AffixType.FireResistance => true,
            AffixType.ColdResistance => true,
            AffixType.LightningResistance => true,
            AffixType.PoisonResistance => true,
            AffixType.AllResistance => true,
            AffixType.ArmorPenetration => true,
            _ => false
        };
    }

    /// <summary>
    /// Get human-readable stat name.
    /// </summary>
    public string GetStatName()
    {
        return Type switch
        {
            AffixType.Strength => "Strength",
            AffixType.Dexterity => "Dexterity",
            AffixType.Vitality => "Vitality",
            AffixType.Energy => "Energy",
            AffixType.AllAttributes => "All Attributes",
            AffixType.FlatDamage => "Damage",
            AffixType.DamagePercent => "Damage",
            AffixType.AttackSpeed => "Attack Speed",
            AffixType.CriticalChance => "Critical Chance",
            AffixType.CriticalDamage => "Critical Damage",
            AffixType.LifeOnHit => "Life on Hit",
            AffixType.ManaOnHit => "Mana on Hit",
            AffixType.ArmorPenetration => "Armor Penetration",
            AffixType.FlatArmor => "Armor",
            AffixType.ArmorPercent => "Armor",
            AffixType.FlatHealth => "Health",
            AffixType.HealthPercent => "Health",
            AffixType.BlockChance => "Block Chance",
            AffixType.BlockAmount => "Block Amount",
            AffixType.DodgeChance => "Dodge Chance",
            AffixType.DamageReduction => "Damage Reduction",
            AffixType.FlatMana => "Mana",
            AffixType.ManaPercent => "Mana",
            AffixType.HealthRegen => "Health/sec",
            AffixType.ManaRegen => "Mana/sec",
            AffixType.AbilityCostReduction => "Ability Cost",
            AffixType.MovementSpeed => "Movement Speed",
            AffixType.MagicFind => "Magic Find",
            AffixType.ExperienceBonus => "Experience",
            AffixType.GoldFind => "Gold Find",
            AffixType.ItemQuantity => "Item Quantity",
            AffixType.FireDamage => "Fire Damage",
            AffixType.ColdDamage => "Cold Damage",
            AffixType.LightningDamage => "Lightning Damage",
            AffixType.PoisonDamage => "Poison Damage",
            AffixType.FireDamagePercent => "Fire Damage",
            AffixType.ColdDamagePercent => "Cold Damage",
            AffixType.LightningDamagePercent => "Lightning Damage",
            AffixType.PoisonDamagePercent => "Poison Damage",
            AffixType.FireResistance => "Fire Resistance",
            AffixType.ColdResistance => "Cold Resistance",
            AffixType.LightningResistance => "Lightning Resistance",
            AffixType.PoisonResistance => "Poison Resistance",
            AffixType.AllResistance => "All Resistances",
            _ => Type.ToString()
        };
    }

    /// <summary>
    /// Create a deep copy of this affix.
    /// </summary>
    public ItemAffix Clone()
    {
        return new ItemAffix
        {
            Id = Id,
            DisplayName = DisplayName,
            Type = Type,
            Category = Category,
            MinValue = MinValue,
            MaxValue = MaxValue,
            CurrentValue = CurrentValue,
            TierRequired = TierRequired,
            IsPrefix = IsPrefix,
            NamePart = NamePart
        };
    }
}

/// <summary>
/// Template for generating affixes at different tiers.
/// </summary>
public class AffixTemplate
{
    public string Id { get; set; } = "";
    public AffixType Type { get; set; }
    public AffixCategory Category { get; set; }
    public bool IsPrefix { get; set; }

    // Tiers with increasing values (keyed by min item level)
    public List<AffixTier> Tiers { get; set; } = new();

    // Which slots this affix can appear on
    public List<EquipmentSlot> ValidSlots { get; set; } = new();

    // Weight for random selection (higher = more common)
    public int Weight { get; set; } = 100;
}

/// <summary>
/// A tier of an affix template with specific value ranges.
/// </summary>
public class AffixTier
{
    public int MinItemLevel { get; set; }
    public float MinValue { get; set; }
    public float MaxValue { get; set; }
    public string NamePart { get; set; } = "";
}

/// <summary>
/// Static database of all affix templates.
/// </summary>
public static class AffixDatabase
{
    private static readonly List<AffixTemplate> _templates = new();
    private static readonly Random _random = new();
    private static bool _initialized = false;

    /// <summary>
    /// Initialize the affix database with all templates.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // === ATTRIBUTE AFFIXES ===
        AddAffixTemplate(AffixType.Strength, AffixCategory.Attribute, true,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Hands, EquipmentSlot.Feet, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 5, NamePart = "Brawn" },
            new AffixTier { MinItemLevel = 10, MinValue = 6, MaxValue = 10, NamePart = "Might" },
            new AffixTier { MinItemLevel = 20, MinValue = 11, MaxValue = 15, NamePart = "Power" },
            new AffixTier { MinItemLevel = 30, MinValue = 16, MaxValue = 20, NamePart = "Giant's" });

        AddAffixTemplate(AffixType.Dexterity, AffixCategory.Attribute, true,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Hands, EquipmentSlot.Feet, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 5, NamePart = "Nimble" },
            new AffixTier { MinItemLevel = 10, MinValue = 6, MaxValue = 10, NamePart = "Agile" },
            new AffixTier { MinItemLevel = 20, MinValue = 11, MaxValue = 15, NamePart = "Swift" },
            new AffixTier { MinItemLevel = 30, MinValue = 16, MaxValue = 20, NamePart = "Cat's" });

        AddAffixTemplate(AffixType.Vitality, AffixCategory.Attribute, true,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Hands, EquipmentSlot.Feet, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 5, NamePart = "Stout" },
            new AffixTier { MinItemLevel = 10, MinValue = 6, MaxValue = 10, NamePart = "Hardy" },
            new AffixTier { MinItemLevel = 20, MinValue = 11, MaxValue = 15, NamePart = "Stalwart" },
            new AffixTier { MinItemLevel = 30, MinValue = 16, MaxValue = 20, NamePart = "Bear's" });

        AddAffixTemplate(AffixType.Energy, AffixCategory.Attribute, true,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 5, NamePart = "Sage's" },
            new AffixTier { MinItemLevel = 10, MinValue = 6, MaxValue = 10, NamePart = "Wizard's" },
            new AffixTier { MinItemLevel = 20, MinValue = 11, MaxValue = 15, NamePart = "Arcane" },
            new AffixTier { MinItemLevel = 30, MinValue = 16, MaxValue = 20, NamePart = "Dragon's" });

        // === OFFENSIVE AFFIXES ===
        AddAffixTemplate(AffixType.FlatDamage, AffixCategory.Offensive, true,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 3, NamePart = "Sharp" },
            new AffixTier { MinItemLevel = 10, MinValue = 4, MaxValue = 7, NamePart = "Keen" },
            new AffixTier { MinItemLevel = 20, MinValue = 8, MaxValue = 12, NamePart = "Brutal" },
            new AffixTier { MinItemLevel = 30, MinValue = 13, MaxValue = 18, NamePart = "Vicious" });

        AddAffixTemplate(AffixType.DamagePercent, AffixCategory.Offensive, false,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 5, MaxValue = 10, NamePart = "of Harm" },
            new AffixTier { MinItemLevel = 10, MinValue = 11, MaxValue = 20, NamePart = "of Ruin" },
            new AffixTier { MinItemLevel = 20, MinValue = 21, MaxValue = 35, NamePart = "of Destruction" },
            new AffixTier { MinItemLevel = 30, MinValue = 36, MaxValue = 50, NamePart = "of Annihilation" });

        AddAffixTemplate(AffixType.AttackSpeed, AffixCategory.Offensive, false,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.Hands, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 3, MaxValue = 6, NamePart = "of Haste" },
            new AffixTier { MinItemLevel = 10, MinValue = 7, MaxValue = 10, NamePart = "of Speed" },
            new AffixTier { MinItemLevel = 20, MinValue = 11, MaxValue = 15, NamePart = "of Alacrity" },
            new AffixTier { MinItemLevel = 30, MinValue = 16, MaxValue = 20, NamePart = "of Frenzy" });

        AddAffixTemplate(AffixType.CriticalChance, AffixCategory.Offensive, false,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.Hands, EquipmentSlot.Head, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 3, NamePart = "of Precision" },
            new AffixTier { MinItemLevel = 10, MinValue = 4, MaxValue = 6, NamePart = "of Accuracy" },
            new AffixTier { MinItemLevel = 20, MinValue = 7, MaxValue = 10, NamePart = "of the Hawk" },
            new AffixTier { MinItemLevel = 30, MinValue = 11, MaxValue = 15, NamePart = "of the Assassin" });

        AddAffixTemplate(AffixType.CriticalDamage, AffixCategory.Offensive, false,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 10, MinValue = 10, MaxValue = 20, NamePart = "of Ferocity" },
            new AffixTier { MinItemLevel = 20, MinValue = 21, MaxValue = 35, NamePart = "of Brutality" },
            new AffixTier { MinItemLevel = 30, MinValue = 36, MaxValue = 50, NamePart = "of Savagery" });

        AddAffixTemplate(AffixType.LifeOnHit, AffixCategory.Offensive, false,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 5, MinValue = 1, MaxValue = 3, NamePart = "of Leeching" },
            new AffixTier { MinItemLevel = 15, MinValue = 4, MaxValue = 6, NamePart = "of Vampirism" },
            new AffixTier { MinItemLevel = 25, MinValue = 7, MaxValue = 10, NamePart = "of the Vampire" });

        // === DEFENSIVE AFFIXES ===
        AddAffixTemplate(AffixType.FlatArmor, AffixCategory.Defensive, true,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Hands, EquipmentSlot.Feet },
            new AffixTier { MinItemLevel = 1, MinValue = 5, MaxValue = 15, NamePart = "Sturdy" },
            new AffixTier { MinItemLevel = 10, MinValue = 16, MaxValue = 35, NamePart = "Reinforced" },
            new AffixTier { MinItemLevel = 20, MinValue = 36, MaxValue = 60, NamePart = "Fortified" },
            new AffixTier { MinItemLevel = 30, MinValue = 61, MaxValue = 100, NamePart = "Impenetrable" });

        AddAffixTemplate(AffixType.FlatHealth, AffixCategory.Defensive, true,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Feet, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 10, MaxValue = 25, NamePart = "Vital" },
            new AffixTier { MinItemLevel = 10, MinValue = 26, MaxValue = 50, NamePart = "Hearty" },
            new AffixTier { MinItemLevel = 20, MinValue = 51, MaxValue = 80, NamePart = "Robust" },
            new AffixTier { MinItemLevel = 30, MinValue = 81, MaxValue = 120, NamePart = "Titan's" });

        AddAffixTemplate(AffixType.HealthPercent, AffixCategory.Defensive, false,
            new[] { EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 10, MinValue = 3, MaxValue = 6, NamePart = "of Endurance" },
            new AffixTier { MinItemLevel = 20, MinValue = 7, MaxValue = 10, NamePart = "of Fortitude" },
            new AffixTier { MinItemLevel = 30, MinValue = 11, MaxValue = 15, NamePart = "of the Colossus" });

        AddAffixTemplate(AffixType.BlockChance, AffixCategory.Defensive, false,
            new[] { EquipmentSlot.OffHand },
            new AffixTier { MinItemLevel = 1, MinValue = 3, MaxValue = 6, NamePart = "of Blocking" },
            new AffixTier { MinItemLevel = 10, MinValue = 7, MaxValue = 10, NamePart = "of Warding" },
            new AffixTier { MinItemLevel = 20, MinValue = 11, MaxValue = 15, NamePart = "of the Wall" });

        AddAffixTemplate(AffixType.DodgeChance, AffixCategory.Defensive, false,
            new[] { EquipmentSlot.Feet, EquipmentSlot.Chest },
            new AffixTier { MinItemLevel = 10, MinValue = 1, MaxValue = 3, NamePart = "of Evasion" },
            new AffixTier { MinItemLevel = 20, MinValue = 4, MaxValue = 6, NamePart = "of the Wind" },
            new AffixTier { MinItemLevel = 30, MinValue = 7, MaxValue = 10, NamePart = "of the Ghost" });

        // === RESOURCE AFFIXES ===
        AddAffixTemplate(AffixType.FlatMana, AffixCategory.Resource, true,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 5, MaxValue = 12, NamePart = "Mystic" },
            new AffixTier { MinItemLevel = 10, MinValue = 13, MaxValue = 25, NamePart = "Arcane" },
            new AffixTier { MinItemLevel = 20, MinValue = 26, MaxValue = 40, NamePart = "Sorcerer's" });

        AddAffixTemplate(AffixType.HealthRegen, AffixCategory.Resource, false,
            new[] { EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 0.5f, MaxValue = 1.5f, NamePart = "of Regeneration" },
            new AffixTier { MinItemLevel = 15, MinValue = 2f, MaxValue = 3.5f, NamePart = "of Restoration" },
            new AffixTier { MinItemLevel = 25, MinValue = 4f, MaxValue = 6f, NamePart = "of the Phoenix" });

        AddAffixTemplate(AffixType.ManaRegen, AffixCategory.Resource, false,
            new[] { EquipmentSlot.Head, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 0.2f, MaxValue = 0.5f, NamePart = "of Focus" },
            new AffixTier { MinItemLevel = 15, MinValue = 0.6f, MaxValue = 1f, NamePart = "of Clarity" },
            new AffixTier { MinItemLevel = 25, MinValue = 1.1f, MaxValue = 1.5f, NamePart = "of Enlightenment" });

        // === UTILITY AFFIXES ===
        AddAffixTemplate(AffixType.MovementSpeed, AffixCategory.Utility, false,
            new[] { EquipmentSlot.Feet },
            new AffixTier { MinItemLevel = 1, MinValue = 5, MaxValue = 8, NamePart = "of Speed" },
            new AffixTier { MinItemLevel = 15, MinValue = 9, MaxValue = 12, NamePart = "of Velocity" },
            new AffixTier { MinItemLevel = 25, MinValue = 13, MaxValue = 20, NamePart = "of the Wind" });

        AddAffixTemplate(AffixType.MagicFind, AffixCategory.Utility, false,
            new[] { EquipmentSlot.Head, EquipmentSlot.Hands, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 10, MaxValue = 25, NamePart = "of Fortune" },
            new AffixTier { MinItemLevel = 15, MinValue = 26, MaxValue = 50, NamePart = "of Luck" },
            new AffixTier { MinItemLevel = 25, MinValue = 51, MaxValue = 100, NamePart = "of Wealth" });

        AddAffixTemplate(AffixType.ExperienceBonus, AffixCategory.Utility, false,
            new[] { EquipmentSlot.Head, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 3, MaxValue = 6, NamePart = "of Learning" },
            new AffixTier { MinItemLevel = 15, MinValue = 7, MaxValue = 10, NamePart = "of Wisdom" });

        // === ELEMENTAL AFFIXES ===
        AddAffixTemplate(AffixType.FireDamage, AffixCategory.Elemental, true,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 4, NamePart = "Smoldering" },
            new AffixTier { MinItemLevel = 15, MinValue = 5, MaxValue = 10, NamePart = "Fiery" },
            new AffixTier { MinItemLevel = 25, MinValue = 11, MaxValue = 18, NamePart = "Blazing" });

        AddAffixTemplate(AffixType.ColdDamage, AffixCategory.Elemental, true,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 4, NamePart = "Chilled" },
            new AffixTier { MinItemLevel = 15, MinValue = 5, MaxValue = 10, NamePart = "Frozen" },
            new AffixTier { MinItemLevel = 25, MinValue = 11, MaxValue = 18, NamePart = "Glacial" });

        AddAffixTemplate(AffixType.LightningDamage, AffixCategory.Elemental, true,
            new[] { EquipmentSlot.MainHand, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 1, MaxValue = 4, NamePart = "Sparking" },
            new AffixTier { MinItemLevel = 15, MinValue = 5, MaxValue = 10, NamePart = "Shocking" },
            new AffixTier { MinItemLevel = 25, MinValue = 11, MaxValue = 18, NamePart = "Thundering" });

        AddAffixTemplate(AffixType.FireResistance, AffixCategory.Elemental, false,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 5, MaxValue = 15, NamePart = "of Warmth" },
            new AffixTier { MinItemLevel = 15, MinValue = 16, MaxValue = 30, NamePart = "of Fire Warding" });

        AddAffixTemplate(AffixType.ColdResistance, AffixCategory.Elemental, false,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 5, MaxValue = 15, NamePart = "of Thawing" },
            new AffixTier { MinItemLevel = 15, MinValue = 16, MaxValue = 30, NamePart = "of Frost Warding" });

        AddAffixTemplate(AffixType.LightningResistance, AffixCategory.Elemental, false,
            new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 1, MinValue = 5, MaxValue = 15, NamePart = "of Grounding" },
            new AffixTier { MinItemLevel = 15, MinValue = 16, MaxValue = 30, NamePart = "of Lightning Warding" });

        AddAffixTemplate(AffixType.AllResistance, AffixCategory.Elemental, false,
            new[] { EquipmentSlot.Chest, EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
            new AffixTier { MinItemLevel = 20, MinValue = 5, MaxValue = 10, NamePart = "of Warding" },
            new AffixTier { MinItemLevel = 30, MinValue = 11, MaxValue = 15, NamePart = "of the Elements" });

        Godot.GD.Print($"[AffixDatabase] Initialized with {_templates.Count} affix templates");
    }

    private static void AddAffixTemplate(AffixType type, AffixCategory category, bool isPrefix,
        EquipmentSlot[] slots, params AffixTier[] tiers)
    {
        var template = new AffixTemplate
        {
            Id = type.ToString().ToLower(),
            Type = type,
            Category = category,
            IsPrefix = isPrefix,
            ValidSlots = new List<EquipmentSlot>(slots),
            Tiers = new List<AffixTier>(tiers)
        };
        _templates.Add(template);
    }

    /// <summary>
    /// Get a random affix appropriate for the given slot and item level.
    /// </summary>
    public static ItemAffix? GetRandomAffix(EquipmentSlot slot, int itemLevel, List<AffixType>? excludeTypes = null)
    {
        Initialize();

        // Filter to valid templates
        var validTemplates = new List<(AffixTemplate template, AffixTier tier)>();

        foreach (var template in _templates)
        {
            // Check slot validity
            if (!template.ValidSlots.Contains(slot)) continue;

            // Check if excluded
            if (excludeTypes != null && excludeTypes.Contains(template.Type)) continue;

            // Find appropriate tier
            AffixTier? bestTier = null;
            foreach (var tier in template.Tiers)
            {
                if (tier.MinItemLevel <= itemLevel)
                {
                    if (bestTier == null || tier.MinItemLevel > bestTier.MinItemLevel)
                    {
                        bestTier = tier;
                    }
                }
            }

            if (bestTier != null)
            {
                validTemplates.Add((template, bestTier));
            }
        }

        if (validTemplates.Count == 0) return null;

        // Weighted random selection
        int totalWeight = 0;
        foreach (var (template, _) in validTemplates)
        {
            totalWeight += template.Weight;
        }

        int roll = _random.Next(totalWeight);
        int current = 0;

        foreach (var (template, tier) in validTemplates)
        {
            current += template.Weight;
            if (roll < current)
            {
                // Create affix from template and tier
                float value = (float)(_random.NextDouble() * (tier.MaxValue - tier.MinValue) + tier.MinValue);

                return new ItemAffix
                {
                    Id = template.Id,
                    DisplayName = tier.NamePart,
                    Type = template.Type,
                    Category = template.Category,
                    MinValue = tier.MinValue,
                    MaxValue = tier.MaxValue,
                    CurrentValue = value,
                    TierRequired = tier.MinItemLevel,
                    IsPrefix = template.IsPrefix,
                    NamePart = tier.NamePart
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Generate multiple unique affixes for an item.
    /// </summary>
    public static List<ItemAffix> GenerateAffixes(EquipmentSlot slot, int itemLevel, int count)
    {
        var affixes = new List<ItemAffix>();
        var usedTypes = new List<AffixType>();

        for (int i = 0; i < count; i++)
        {
            var affix = GetRandomAffix(slot, itemLevel, usedTypes);
            if (affix != null)
            {
                affixes.Add(affix);
                usedTypes.Add(affix.Type);
            }
        }

        return affixes;
    }
}
