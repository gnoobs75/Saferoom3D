using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeRoom3D.Core;

/// <summary>
/// Loads game data from JSON files in the Data/ folder.
/// Provides centralized access to monster, item, and ability configurations.
/// </summary>
public static class DataLoader
{
    private static MonsterDatabase? _monsterDatabase;
    private static ItemDataDatabase? _itemDatabase;
    private static AbilityDatabase? _abilityDatabase;
    private static bool _initialized;

    /// <summary>
    /// Initialize the data loader. Must be called before accessing data.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        LoadMonsterData();
        LoadItemData();
        LoadAbilityData();

        _initialized = true;
        GD.Print("[DataLoader] Initialized with all game data");
    }

    #region Monster Data

    private static void LoadMonsterData()
    {
        try
        {
            string path = "res://Data/monsters.json";
            if (!FileAccess.FileExists(path))
            {
                GD.PrintErr($"[DataLoader] Monster data file not found: {path}");
                _monsterDatabase = new MonsterDatabase();
                return;
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            _monsterDatabase = JsonSerializer.Deserialize<MonsterDatabase>(json, options) ?? new MonsterDatabase();
            GD.Print($"[DataLoader] Loaded {_monsterDatabase.Monsters?.Count ?? 0} monsters, {_monsterDatabase.Bosses?.Count ?? 0} bosses");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DataLoader] Failed to load monster data: {ex.Message}");
            _monsterDatabase = new MonsterDatabase();
        }
    }

    /// <summary>
    /// Get monster configuration by type ID.
    /// </summary>
    public static MonsterConfig? GetMonster(string monsterType)
    {
        if (!_initialized) Initialize();

        var type = monsterType.ToLower();
        return _monsterDatabase?.Monsters?.Find(m => m.Id == type);
    }

    /// <summary>
    /// Get boss configuration by ID.
    /// </summary>
    public static BossConfig? GetBoss(string bossId)
    {
        if (!_initialized) Initialize();

        var id = bossId.ToLower();
        return _monsterDatabase?.Bosses?.Find(b => b.Id == id);
    }

    /// <summary>
    /// Get all monster types.
    /// </summary>
    public static List<MonsterConfig> GetAllMonsters()
    {
        if (!_initialized) Initialize();
        return _monsterDatabase?.Monsters ?? new List<MonsterConfig>();
    }

    /// <summary>
    /// Get all boss types.
    /// </summary>
    public static List<BossConfig> GetAllBosses()
    {
        if (!_initialized) Initialize();
        return _monsterDatabase?.Bosses ?? new List<BossConfig>();
    }

    /// <summary>
    /// Get default monster values.
    /// </summary>
    public static MonsterDefaults GetMonsterDefaults()
    {
        if (!_initialized) Initialize();
        return _monsterDatabase?.Defaults ?? new MonsterDefaults();
    }

    /// <summary>
    /// Get level scaling configuration.
    /// </summary>
    public static LevelScaling GetLevelScaling()
    {
        if (!_initialized) Initialize();
        return _monsterDatabase?.LevelScaling ?? new LevelScaling();
    }

    #endregion

    #region Item Data

    private static void LoadItemData()
    {
        try
        {
            string path = "res://Data/items.json";
            if (!FileAccess.FileExists(path))
            {
                GD.PrintErr($"[DataLoader] Item data file not found: {path}");
                _itemDatabase = new ItemDataDatabase();
                return;
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            _itemDatabase = JsonSerializer.Deserialize<ItemDataDatabase>(json, options) ?? new ItemDataDatabase();
            GD.Print($"[DataLoader] Loaded {_itemDatabase.Consumables?.Count ?? 0} consumables, {_itemDatabase.Materials?.Count ?? 0} materials");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DataLoader] Failed to load item data: {ex.Message}");
            _itemDatabase = new ItemDataDatabase();
        }
    }

    /// <summary>
    /// Get consumable item data by ID.
    /// </summary>
    public static ConsumableData? GetConsumable(string itemId)
    {
        if (!_initialized) Initialize();
        return _itemDatabase?.Consumables?.Find(c => c.Id == itemId);
    }

    /// <summary>
    /// Get material item data by ID.
    /// </summary>
    public static MaterialData? GetMaterial(string itemId)
    {
        if (!_initialized) Initialize();
        return _itemDatabase?.Materials?.Find(m => m.Id == itemId);
    }

    /// <summary>
    /// Get monster part data by ID.
    /// </summary>
    public static MonsterPartData? GetMonsterPart(string partId)
    {
        if (!_initialized) Initialize();
        if (_itemDatabase?.MonsterParts == null) return null;

        return _itemDatabase.MonsterParts.TryGetValue(partId, out var part) ? part : null;
    }

    /// <summary>
    /// Get all consumable items.
    /// </summary>
    public static List<ConsumableData> GetAllConsumables()
    {
        if (!_initialized) Initialize();
        return _itemDatabase?.Consumables ?? new List<ConsumableData>();
    }

    #endregion

    #region Ability Data

    private static void LoadAbilityData()
    {
        try
        {
            string path = "res://Data/abilities.json";
            if (!FileAccess.FileExists(path))
            {
                GD.PrintErr($"[DataLoader] Ability data file not found: {path}");
                _abilityDatabase = new AbilityDatabase();
                return;
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            _abilityDatabase = JsonSerializer.Deserialize<AbilityDatabase>(json, options) ?? new AbilityDatabase();
            GD.Print($"[DataLoader] Loaded {_abilityDatabase.Abilities?.Count ?? 0} abilities");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DataLoader] Failed to load ability data: {ex.Message}");
            _abilityDatabase = new AbilityDatabase();
        }
    }

    /// <summary>
    /// Get ability configuration by ID.
    /// </summary>
    public static AbilityData? GetAbility(string abilityId)
    {
        if (!_initialized) Initialize();
        return _abilityDatabase?.Abilities?.Find(a => a.Id == abilityId);
    }

    /// <summary>
    /// Get all abilities.
    /// </summary>
    public static List<AbilityData> GetAllAbilities()
    {
        if (!_initialized) Initialize();
        return _abilityDatabase?.Abilities ?? new List<AbilityData>();
    }

    /// <summary>
    /// Get damage type color.
    /// </summary>
    public static Color GetDamageTypeColor(string damageType)
    {
        if (!_initialized) Initialize();

        if (_abilityDatabase?.DamageTypes != null &&
            _abilityDatabase.DamageTypes.TryGetValue(damageType, out var data))
        {
            return data.Color.ToGodotColor();
        }

        return Colors.White;
    }

    #endregion

    /// <summary>
    /// Force reload all data (useful for hot-reloading during development).
    /// </summary>
    public static void Reload()
    {
        _initialized = false;
        _monsterDatabase = null;
        _itemDatabase = null;
        _abilityDatabase = null;
        Initialize();
    }
}

#region Data Classes

// === Monster Data Classes ===

public class MonsterDatabase
{
    public List<MonsterConfig>? Monsters { get; set; }
    public List<BossConfig>? Bosses { get; set; }
    public LevelScaling? LevelScaling { get; set; }
    public MonsterDefaults? Defaults { get; set; }
}

public class MonsterConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "original";
    public int MaxHealth { get; set; } = 50;
    public float MoveSpeed { get; set; } = 3f;
    public float Damage { get; set; } = 10f;
    public float AttackRange { get; set; } = 2f;
    public float AggroRange { get; set; } = 15f;
    public float MinStopDistance { get; set; } = 2.5f;
    public JsonColor Color { get; set; } = new();
    public bool CanHaveWeapon { get; set; }
    public bool HasTorch { get; set; }
    public string Notes { get; set; } = "";

    public Color GetGodotColor() => Color.ToGodotColor();
}

public class BossConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string BaseMonster { get; set; } = "";
    public int MaxHealth { get; set; } = 500;
    public float MoveSpeed { get; set; } = 3f;
    public float Damage { get; set; } = 40f;
    public float AttackRange { get; set; } = 3.5f;
    public float AggroRange { get; set; } = 30f;
    public string Special { get; set; } = "";
}

public class LevelScaling
{
    public float HealthMultiplierPerLevel { get; set; } = 0.15f;
    public float DamageMultiplierPerLevel { get; set; } = 0.10f;
    public string XpBaseFormula { get; set; } = "baseHealth * (1 + level * 0.5) / 10";
}

public class MonsterDefaults
{
    public float AttackRange { get; set; } = 2f;
    public float AggroRange { get; set; } = 15f;
    public float MinStopDistance { get; set; } = 2.5f;
    public JsonColor Color { get; set; } = new() { R = 0.5f, G = 0.5f, B = 0.5f, A = 1f };
}

// === Item Data Classes ===

public class ItemDataDatabase
{
    public List<ConsumableData>? Consumables { get; set; }
    public List<MaterialData>? Materials { get; set; }
    public List<QuestItemData>? QuestItems { get; set; }
    public Dictionary<string, MonsterPartData>? MonsterParts { get; set; }
    public Dictionary<string, MonsterPartData>? BossParts { get; set; }
}

public class ConsumableData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MaxStack { get; set; } = 10;
    public int HealAmount { get; set; }
    public int ManaAmount { get; set; }
    public int GoldValue { get; set; }
    public JsonColor Color { get; set; } = new();
    public string? Special { get; set; }
    public float Duration { get; set; }
}

public class MaterialData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MaxStack { get; set; } = 50;
    public int GoldValue { get; set; }
    public JsonColor Color { get; set; } = new();
}

public class QuestItemData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MaxStack { get; set; } = 5;
    public JsonColor Color { get; set; } = new();
}

public class MonsterPartData
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string FromMonster { get; set; } = "";
    public string FromBoss { get; set; } = "";
}

// === Ability Data Classes ===

public class AbilityDatabase
{
    public List<AbilityData>? Abilities { get; set; }
    public Dictionary<string, string>? TargetingTypes { get; set; }
    public Dictionary<string, DamageTypeData>? DamageTypes { get; set; }
}

public class AbilityData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "instant";
    public float Cooldown { get; set; }
    public int ManaCost { get; set; }
    public string? DamageType { get; set; }
    public float Damage { get; set; }
    public float Radius { get; set; }
    public float Duration { get; set; }
    public float ProjectileSpeed { get; set; }
    public int BounceCount { get; set; }
    public float BounceRange { get; set; }
    public float DamageFalloff { get; set; } = 1f;
    public float HealPercent { get; set; }
    public float Range { get; set; }
    public float DamageReduction { get; set; }
    public float PullStrength { get; set; }
    public float DamagePerSecond { get; set; }
    public float TickRate { get; set; }
    public float KnockbackForce { get; set; }
    public float FearDuration { get; set; }
    public float SpeedMultiplier { get; set; }
    public float DamageMultiplier { get; set; }
    public int ImageCount { get; set; }
    public float ImageDuration { get; set; }
    public int ImageHealth { get; set; }
    public float HealPercentOnKill { get; set; }
    public float LowHealthThreshold { get; set; }
    public float BonusHealMultiplier { get; set; }
    public float SlowPercent { get; set; }
    public float KillWindow { get; set; }
    public float BonusDamagePerKill { get; set; }
    public int MaxStacks { get; set; }
    public float StackDuration { get; set; }
    public JsonColor Color { get; set; } = new();
    public string Icon { get; set; } = "";
    public int HotbarRow { get; set; }
    public int HotbarSlot { get; set; }
}

public class DamageTypeData
{
    public JsonColor Color { get; set; } = new();
}

// === Shared Classes ===

public class JsonColor
{
    public float R { get; set; } = 0.5f;
    public float G { get; set; } = 0.5f;
    public float B { get; set; } = 0.5f;
    public float A { get; set; } = 1f;

    public Color ToGodotColor() => new(R, G, B, A);
}

#endregion
