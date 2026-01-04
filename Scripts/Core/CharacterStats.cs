using System;
using Godot;

namespace SafeRoom3D.Core;

/// <summary>
/// Character stats system with primary attributes and derived stats.
/// Inspired by Diablo 2's attribute point system.
/// </summary>
public class CharacterStats
{
    // === PRIMARY ATTRIBUTES (can be increased with points) ===
    // Base values from level 1
    private int _baseStrength = 10;
    private int _baseDexterity = 10;
    private int _baseVitality = 10;
    private int _baseEnergy = 10;

    // Points spent by player (Diablo 2 style)
    public int StrengthPoints { get; private set; }
    public int DexterityPoints { get; private set; }
    public int VitalityPoints { get; private set; }
    public int EnergyPoints { get; private set; }

    // Bonus from equipment
    private int _equipStrength;
    private int _equipDexterity;
    private int _equipVitality;
    private int _equipEnergy;

    // Total primary attributes (base + points + equipment)
    public int Strength => _baseStrength + StrengthPoints + _equipStrength;
    public int Dexterity => _baseDexterity + DexterityPoints + _equipDexterity;
    public int Vitality => _baseVitality + VitalityPoints + _equipVitality;
    public int Energy => _baseEnergy + EnergyPoints + _equipEnergy;

    // Unspent attribute points (5 per level)
    public int UnspentAttributePoints { get; private set; }

    // === DERIVED STATS (calculated from attributes + equipment) ===

    // Health & Mana
    public int MaxHealth { get; private set; } = 100;
    public int MaxMana { get; private set; } = 50;
    public float HealthRegen { get; private set; }  // per second
    public float ManaRegen { get; private set; }    // per second

    // Damage
    public int MinDamage { get; private set; } = 1;
    public int MaxDamage { get; private set; } = 5;
    public float AttackSpeed { get; private set; } = 1.0f;
    public float CriticalChance { get; private set; } = 0.05f;  // 5% base
    public float CriticalDamage { get; private set; } = 1.5f;   // 150% base

    // Defense
    public int Armor { get; private set; }
    public float DamageReduction { get; private set; }  // Calculated from armor
    public float BlockChance { get; private set; }
    public int BlockAmount { get; private set; }
    public float DodgeChance { get; private set; }

    // Utility
    public float MovementSpeed { get; private set; } = 1.0f;  // Multiplier
    public float MagicFind { get; private set; }
    public float ExperienceBonus { get; private set; }
    public float GoldFind { get; private set; }

    // Elemental
    public int FireDamage { get; private set; }
    public int ColdDamage { get; private set; }
    public int LightningDamage { get; private set; }
    public int PoisonDamage { get; private set; }
    public float FireResistance { get; private set; }
    public float ColdResistance { get; private set; }
    public float LightningResistance { get; private set; }
    public float PoisonResistance { get; private set; }

    // Resource bonuses from equipment
    private float _equipHealthPercent;
    private float _equipManaPercent;
    private int _equipFlatHealth;
    private int _equipFlatMana;
    private float _equipHealthRegen;
    private float _equipManaRegen;

    // Combat bonuses from equipment
    private int _equipFlatDamage;
    private float _equipDamagePercent;
    private float _equipAttackSpeed;
    private float _equipCritChance;
    private float _equipCritDamage;
    private int _equipArmor;
    private float _equipArmorPercent;
    private float _equipBlockChance;
    private int _equipBlockAmount;
    private float _equipDodgeChance;

    // Utility bonuses from equipment
    private float _equipMovementSpeed;
    private float _equipMagicFind;
    private float _equipExpBonus;
    private float _equipGoldFind;

    // Elemental bonuses from equipment
    private int _equipFireDamage;
    private int _equipColdDamage;
    private int _equipLightningDamage;
    private int _equipPoisonDamage;
    private float _equipFireDamagePercent;
    private float _equipColdDamagePercent;
    private float _equipLightningDamagePercent;
    private float _equipPoisonDamagePercent;
    private float _equipFireResist;
    private float _equipColdResist;
    private float _equipLightningResist;
    private float _equipPoisonResist;
    private float _equipAllResist;

    // Life/Mana on hit
    public int LifeOnHit { get; private set; }
    public int ManaOnHit { get; private set; }

    // Current player level (for calculations)
    public int Level { get; private set; } = 1;

    // Base weapon stats (from equipped weapon, or unarmed defaults)
    private int _weaponMinDamage = 1;
    private int _weaponMaxDamage = 5;
    private float _weaponAttackSpeed = 1.0f;

    // Random for calculations
    private readonly Random _random = new();

    // Events
    public event Action? OnStatsChanged;

    /// <summary>
    /// Initialize stats for a new character.
    /// </summary>
    public CharacterStats()
    {
        RecalculateAll();
    }

    /// <summary>
    /// Set the player's level and grant attribute points.
    /// Called when player levels up.
    /// </summary>
    public void SetLevel(int newLevel)
    {
        int oldLevel = Level;
        Level = newLevel;

        // Grant 5 attribute points per level gained
        int levelsGained = newLevel - oldLevel;
        if (levelsGained > 0)
        {
            UnspentAttributePoints += levelsGained * 5;
            GD.Print($"[CharacterStats] Level {newLevel}: Gained {levelsGained * 5} attribute points. Unspent: {UnspentAttributePoints}");
        }

        RecalculateAll();
    }

    /// <summary>
    /// Spend an attribute point on a primary stat.
    /// Returns true if successful.
    /// </summary>
    public bool SpendAttributePoint(string attribute)
    {
        if (UnspentAttributePoints <= 0) return false;

        switch (attribute.ToLower())
        {
            case "strength":
            case "str":
                StrengthPoints++;
                break;
            case "dexterity":
            case "dex":
                DexterityPoints++;
                break;
            case "vitality":
            case "vit":
                VitalityPoints++;
                break;
            case "energy":
            case "ene":
                EnergyPoints++;
                break;
            default:
                return false;
        }

        UnspentAttributePoints--;
        RecalculateAll();
        GD.Print($"[CharacterStats] Spent point on {attribute}. Remaining: {UnspentAttributePoints}");
        return true;
    }

    /// <summary>
    /// Set base weapon stats from equipped weapon.
    /// </summary>
    public void SetWeaponStats(int minDamage, int maxDamage, float attackSpeed)
    {
        _weaponMinDamage = Math.Max(1, minDamage);
        _weaponMaxDamage = Math.Max(_weaponMinDamage, maxDamage);
        _weaponAttackSpeed = Math.Max(0.1f, attackSpeed);
        RecalculateAll();
    }

    /// <summary>
    /// Clear all equipment bonuses before recalculating from equipment.
    /// </summary>
    public void ClearEquipmentBonuses()
    {
        _equipStrength = 0;
        _equipDexterity = 0;
        _equipVitality = 0;
        _equipEnergy = 0;

        _equipFlatHealth = 0;
        _equipHealthPercent = 0;
        _equipFlatMana = 0;
        _equipManaPercent = 0;
        _equipHealthRegen = 0;
        _equipManaRegen = 0;

        _equipFlatDamage = 0;
        _equipDamagePercent = 0;
        _equipAttackSpeed = 0;
        _equipCritChance = 0;
        _equipCritDamage = 0;
        _equipArmor = 0;
        _equipArmorPercent = 0;
        _equipBlockChance = 0;
        _equipBlockAmount = 0;
        _equipDodgeChance = 0;

        _equipMovementSpeed = 0;
        _equipMagicFind = 0;
        _equipExpBonus = 0;
        _equipGoldFind = 0;

        _equipFireDamage = 0;
        _equipColdDamage = 0;
        _equipLightningDamage = 0;
        _equipPoisonDamage = 0;
        _equipFireDamagePercent = 0;
        _equipColdDamagePercent = 0;
        _equipLightningDamagePercent = 0;
        _equipPoisonDamagePercent = 0;
        _equipFireResist = 0;
        _equipColdResist = 0;
        _equipLightningResist = 0;
        _equipPoisonResist = 0;
        _equipAllResist = 0;

        LifeOnHit = 0;
        ManaOnHit = 0;

        // Reset weapon to unarmed
        _weaponMinDamage = 1;
        _weaponMaxDamage = 5;
        _weaponAttackSpeed = 1.0f;
    }

    /// <summary>
    /// Apply bonuses from an equipment item.
    /// </summary>
    public void ApplyEquipmentBonuses(EquipmentItem item)
    {
        // Apply base weapon stats
        if (item.Slot == EquipmentSlot.MainHand)
        {
            _weaponMinDamage = item.MinDamage;
            _weaponMaxDamage = item.MaxDamage;
            _weaponAttackSpeed = item.AttackSpeed;
        }

        // Apply base armor
        if (item.Armor > 0)
        {
            _equipArmor += item.Armor;
        }

        // Apply base block (shields)
        if (item.BlockChance > 0)
        {
            _equipBlockChance += item.BlockChance;
            _equipBlockAmount = Math.Max(_equipBlockAmount, item.BlockAmount);
        }

        // Apply all affixes
        foreach (var affix in item.Affixes)
        {
            ApplyAffix(affix);
        }
    }

    /// <summary>
    /// Apply a single affix bonus.
    /// </summary>
    private void ApplyAffix(ItemAffix affix)
    {
        float value = affix.CurrentValue;

        switch (affix.Type)
        {
            // Attributes
            case AffixType.Strength:
                _equipStrength += (int)value;
                break;
            case AffixType.Dexterity:
                _equipDexterity += (int)value;
                break;
            case AffixType.Vitality:
                _equipVitality += (int)value;
                break;
            case AffixType.Energy:
                _equipEnergy += (int)value;
                break;
            case AffixType.AllAttributes:
                _equipStrength += (int)value;
                _equipDexterity += (int)value;
                _equipVitality += (int)value;
                _equipEnergy += (int)value;
                break;

            // Offensive
            case AffixType.FlatDamage:
                _equipFlatDamage += (int)value;
                break;
            case AffixType.DamagePercent:
                _equipDamagePercent += value;
                break;
            case AffixType.AttackSpeed:
                _equipAttackSpeed += value;
                break;
            case AffixType.CriticalChance:
                _equipCritChance += value;
                break;
            case AffixType.CriticalDamage:
                _equipCritDamage += value;
                break;
            case AffixType.LifeOnHit:
                LifeOnHit += (int)value;
                break;
            case AffixType.ManaOnHit:
                ManaOnHit += (int)value;
                break;

            // Defensive
            case AffixType.FlatArmor:
                _equipArmor += (int)value;
                break;
            case AffixType.ArmorPercent:
                _equipArmorPercent += value;
                break;
            case AffixType.FlatHealth:
                _equipFlatHealth += (int)value;
                break;
            case AffixType.HealthPercent:
                _equipHealthPercent += value;
                break;
            case AffixType.BlockChance:
                _equipBlockChance += value;
                break;
            case AffixType.BlockAmount:
                _equipBlockAmount += (int)value;
                break;
            case AffixType.DodgeChance:
                _equipDodgeChance += value;
                break;

            // Resource
            case AffixType.FlatMana:
                _equipFlatMana += (int)value;
                break;
            case AffixType.ManaPercent:
                _equipManaPercent += value;
                break;
            case AffixType.HealthRegen:
                _equipHealthRegen += value;
                break;
            case AffixType.ManaRegen:
                _equipManaRegen += value;
                break;

            // Utility
            case AffixType.MovementSpeed:
                _equipMovementSpeed += value;
                break;
            case AffixType.MagicFind:
                _equipMagicFind += value;
                break;
            case AffixType.ExperienceBonus:
                _equipExpBonus += value;
                break;
            case AffixType.GoldFind:
                _equipGoldFind += value;
                break;

            // Elemental Damage
            case AffixType.FireDamage:
                _equipFireDamage += (int)value;
                break;
            case AffixType.ColdDamage:
                _equipColdDamage += (int)value;
                break;
            case AffixType.LightningDamage:
                _equipLightningDamage += (int)value;
                break;
            case AffixType.PoisonDamage:
                _equipPoisonDamage += (int)value;
                break;
            case AffixType.FireDamagePercent:
                _equipFireDamagePercent += value;
                break;
            case AffixType.ColdDamagePercent:
                _equipColdDamagePercent += value;
                break;
            case AffixType.LightningDamagePercent:
                _equipLightningDamagePercent += value;
                break;
            case AffixType.PoisonDamagePercent:
                _equipPoisonDamagePercent += value;
                break;

            // Elemental Resistance
            case AffixType.FireResistance:
                _equipFireResist += value;
                break;
            case AffixType.ColdResistance:
                _equipColdResist += value;
                break;
            case AffixType.LightningResistance:
                _equipLightningResist += value;
                break;
            case AffixType.PoisonResistance:
                _equipPoisonResist += value;
                break;
            case AffixType.AllResistance:
                _equipAllResist += value;
                break;
        }
    }

    /// <summary>
    /// Recalculate all derived stats from base attributes and equipment.
    /// </summary>
    public void RecalculateAll()
    {
        // === HEALTH ===
        // Base: 100 + (Vitality * 10) + (Level * 15) + flat bonuses
        int baseHealth = 100 + (Vitality * 10) + (Level * 15) + _equipFlatHealth;
        MaxHealth = (int)(baseHealth * (1f + _equipHealthPercent / 100f));

        // === MANA ===
        // Base: 50 + (Energy * 5) + (Level * 5) + flat bonuses
        int baseMana = 50 + (Energy * 5) + (Level * 5) + _equipFlatMana;
        MaxMana = (int)(baseMana * (1f + _equipManaPercent / 100f));

        // === REGENERATION ===
        // Vitality: +0.5 HP/sec per point
        // Energy: +0.2 Mana/sec per point
        HealthRegen = (Vitality * 0.5f) + _equipHealthRegen;
        ManaRegen = (Energy * 0.2f) + _equipManaRegen;

        // === DAMAGE ===
        // Weapon damage + flat damage, scaled by strength and percent bonuses
        float strengthBonus = 1f + (Strength * 0.02f);  // +2% per STR
        float percentBonus = 1f + (_equipDamagePercent / 100f);
        float totalMultiplier = strengthBonus * percentBonus;

        MinDamage = (int)((_weaponMinDamage + _equipFlatDamage) * totalMultiplier);
        MaxDamage = (int)((_weaponMaxDamage + _equipFlatDamage) * totalMultiplier);
        MinDamage = Math.Max(1, MinDamage);
        MaxDamage = Math.Max(MinDamage, MaxDamage);

        // === ATTACK SPEED ===
        // Base weapon speed + dexterity bonus + equipment bonus
        float dexSpeedBonus = Dexterity * 0.01f;  // +1% per DEX
        AttackSpeed = _weaponAttackSpeed * (1f + dexSpeedBonus + _equipAttackSpeed / 100f);

        // === CRITICAL ===
        // Base 5% + (Dexterity * 0.5%) + equipment
        CriticalChance = 0.05f + (Dexterity * 0.005f) + (_equipCritChance / 100f);
        CriticalChance = Mathf.Clamp(CriticalChance, 0f, 0.75f);  // Cap at 75%

        // Base 150% + equipment
        CriticalDamage = 1.5f + (_equipCritDamage / 100f);

        // === ARMOR & DAMAGE REDUCTION ===
        // Armor = base + equipment + percentage bonus
        int baseArmor = _equipArmor;
        Armor = (int)(baseArmor * (1f + _equipArmorPercent / 100f));

        // Damage reduction formula: Armor / (Armor + 100 + Level * 10)
        // This gives diminishing returns as armor increases
        float armorDivisor = Armor + 100f + (Level * 10f);
        DamageReduction = Armor / armorDivisor;
        DamageReduction = Mathf.Clamp(DamageReduction, 0f, 0.75f);  // Cap at 75%

        // === BLOCK & DODGE ===
        BlockChance = _equipBlockChance / 100f;
        BlockChance = Mathf.Clamp(BlockChance, 0f, 0.50f);  // Cap at 50%
        BlockAmount = _equipBlockAmount;

        // Dexterity contributes to dodge
        DodgeChance = (Dexterity * 0.005f) + (_equipDodgeChance / 100f);
        DodgeChance = Mathf.Clamp(DodgeChance, 0f, 0.30f);  // Cap at 30%

        // === UTILITY ===
        MovementSpeed = 1f + (_equipMovementSpeed / 100f);
        MagicFind = _equipMagicFind;
        ExperienceBonus = _equipExpBonus;
        GoldFind = _equipGoldFind;

        // === ELEMENTAL ===
        FireDamage = (int)(_equipFireDamage * (1f + _equipFireDamagePercent / 100f));
        ColdDamage = (int)(_equipColdDamage * (1f + _equipColdDamagePercent / 100f));
        LightningDamage = (int)(_equipLightningDamage * (1f + _equipLightningDamagePercent / 100f));
        PoisonDamage = (int)(_equipPoisonDamage * (1f + _equipPoisonDamagePercent / 100f));

        // Cap resistances at 75%
        FireResistance = Mathf.Clamp((_equipFireResist + _equipAllResist) / 100f, -1f, 0.75f);
        ColdResistance = Mathf.Clamp((_equipColdResist + _equipAllResist) / 100f, -1f, 0.75f);
        LightningResistance = Mathf.Clamp((_equipLightningResist + _equipAllResist) / 100f, -1f, 0.75f);
        PoisonResistance = Mathf.Clamp((_equipPoisonResist + _equipAllResist) / 100f, -1f, 0.75f);

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// Calculate damage for a melee attack (roll between min and max, check crit).
    /// </summary>
    public (int damage, bool isCrit) CalculateMeleeDamage()
    {
        int baseDamage = _random.Next(MinDamage, MaxDamage + 1);

        // Add elemental damage
        int totalElemental = FireDamage + ColdDamage + LightningDamage + PoisonDamage;
        baseDamage += totalElemental;

        // Check critical hit
        bool isCrit = _random.NextSingle() < CriticalChance;
        if (isCrit)
        {
            baseDamage = (int)(baseDamage * CriticalDamage);
        }

        return (Math.Max(1, baseDamage), isCrit);
    }

    /// <summary>
    /// Calculate incoming damage after armor, block, and dodge.
    /// </summary>
    public (int damage, bool blocked, bool dodged) CalculateIncomingDamage(int rawDamage, bool canBlock = true, bool canDodge = true)
    {
        // Dodge check first
        if (canDodge && _random.NextSingle() < DodgeChance)
        {
            return (0, false, true);
        }

        // Block check
        bool blocked = false;
        if (canBlock && _random.NextSingle() < BlockChance)
        {
            rawDamage = Math.Max(1, rawDamage - BlockAmount);
            rawDamage = (int)(rawDamage * 0.5f);  // Block also reduces by 50%
            blocked = true;
        }

        // Armor reduction
        int finalDamage = (int)(rawDamage * (1f - DamageReduction));

        return (Math.Max(1, finalDamage), blocked, false);
    }

    /// <summary>
    /// Get a summary string of current stats for debugging.
    /// </summary>
    public string GetStatsSummary()
    {
        return $"Level {Level} | STR:{Strength} DEX:{Dexterity} VIT:{Vitality} ENE:{Energy}\n" +
               $"HP:{MaxHealth} ({HealthRegen:F1}/s) | Mana:{MaxMana} ({ManaRegen:F1}/s)\n" +
               $"Damage:{MinDamage}-{MaxDamage} | Speed:{AttackSpeed:F2} | Crit:{CriticalChance:P0} ({CriticalDamage:P0})\n" +
               $"Armor:{Armor} ({DamageReduction:P0}) | Block:{BlockChance:P0} | Dodge:{DodgeChance:P0}\n" +
               $"MF:{MagicFind:F0}% | XP:{ExperienceBonus:F0}% | Speed:{MovementSpeed:P0}";
    }
}
