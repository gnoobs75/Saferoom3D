using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SafeRoom3D.Core;

/// <summary>
/// Lightweight statistics tracking for dungeon runs.
/// Tracks kills, damage, abilities, and other combat metrics.
/// </summary>
public partial class GameStats : Node
{
    // Singleton instance
    public static GameStats? Instance { get; private set; }

    // Kill tracking
    private Dictionary<string, int> _killsByMonsterType = new();
    private int _totalKills = 0;
    private int _bossKills = 0;
    private int _multiKills = 0;  // 3+ kills within 2 seconds
    private string _mostKilledMonster = "";
    private int _mostKilledCount = 0;

    // Damage tracking
    private float _totalDamageDealt = 0f;
    private float _totalDamageTaken = 0f;
    private float _largestHit = 0f;
    private string _largestHitTarget = "";

    // Ability tracking
    private Dictionary<string, int> _abilityCasts = new();
    private int _totalAbilityCasts = 0;
    private string _favoriteAbility = "";
    private int _favoriteAbilityCount = 0;

    // Combat tracking
    private int _meleeAttacks = 0;
    private int _rangedAttacks = 0;
    private int _criticalHits = 0;
    private int _dodges = 0;
    private int _blockedHits = 0;

    // Session tracking
    private int _deathCount = 0;
    private int _floorsCleared = 0;
    private int _itemsLooted = 0;
    private int _goldCollected = 0;
    private float _sessionTime = 0f;

    // Recent activity for AI commentary context
    private Queue<string> _recentKills = new();  // Last 10 monster types killed
    private const int MaxRecentKills = 10;

    // Public accessors for stats
    public int TotalKills => _totalKills;
    public int BossKills => _bossKills;
    public int MultiKills => _multiKills;
    public int Deaths => _deathCount;
    public int FloorsCleared => _floorsCleared;
    public float TotalDamageDealt => _totalDamageDealt;
    public float TotalDamageTaken => _totalDamageTaken;
    public float LargestHit => _largestHit;
    public string LargestHitTarget => _largestHitTarget;
    public int TotalAbilityCasts => _totalAbilityCasts;
    public string FavoriteAbility => _favoriteAbility;
    public string MostKilledMonster => _mostKilledMonster;
    public int ItemsLooted => _itemsLooted;
    public int GoldCollected => _goldCollected;
    public float SessionTime => _sessionTime;
    public int MeleeAttacks => _meleeAttacks;
    public int RangedAttacks => _rangedAttacks;
    public int CriticalHits => _criticalHits;

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[GameStats] Statistics tracking initialized");
    }

    public override void _Process(double delta)
    {
        _sessionTime += (float)delta;
    }

    // ================== KILL TRACKING ==================

    /// <summary>
    /// Record a monster kill
    /// </summary>
    public void RecordKill(string monsterType, bool isBoss = false)
    {
        _totalKills++;

        // Track by type
        if (_killsByMonsterType.ContainsKey(monsterType))
            _killsByMonsterType[monsterType]++;
        else
            _killsByMonsterType[monsterType] = 1;

        // Update most killed
        if (_killsByMonsterType[monsterType] > _mostKilledCount)
        {
            _mostKilledCount = _killsByMonsterType[monsterType];
            _mostKilledMonster = monsterType;
        }

        // Track boss kills
        if (isBoss)
            _bossKills++;

        // Track recent kills
        _recentKills.Enqueue(monsterType);
        if (_recentKills.Count > MaxRecentKills)
            _recentKills.Dequeue();
    }

    /// <summary>
    /// Record a multi-kill
    /// </summary>
    public void RecordMultiKill(int killCount)
    {
        if (killCount >= 3)
            _multiKills++;
    }

    /// <summary>
    /// Get kill count for a specific monster type
    /// </summary>
    public int GetKillCount(string monsterType)
    {
        return _killsByMonsterType.TryGetValue(monsterType, out int count) ? count : 0;
    }

    /// <summary>
    /// Get top N killed monster types
    /// </summary>
    public List<(string type, int count)> GetTopKills(int n = 5)
    {
        return _killsByMonsterType
            .OrderByDescending(kvp => kvp.Value)
            .Take(n)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }

    // ================== DAMAGE TRACKING ==================

    /// <summary>
    /// Record damage dealt to an enemy
    /// </summary>
    public void RecordDamageDealt(float damage, string targetType = "")
    {
        _totalDamageDealt += damage;

        if (damage > _largestHit)
        {
            _largestHit = damage;
            _largestHitTarget = targetType;
        }
    }

    /// <summary>
    /// Record damage taken by player
    /// </summary>
    public void RecordDamageTaken(float damage)
    {
        _totalDamageTaken += damage;
    }

    /// <summary>
    /// Record a critical hit
    /// </summary>
    public void RecordCriticalHit()
    {
        _criticalHits++;
    }

    // ================== ABILITY TRACKING ==================

    /// <summary>
    /// Record an ability cast
    /// </summary>
    public void RecordAbilityCast(string abilityName)
    {
        _totalAbilityCasts++;

        if (_abilityCasts.ContainsKey(abilityName))
            _abilityCasts[abilityName]++;
        else
            _abilityCasts[abilityName] = 1;

        // Update favorite
        if (_abilityCasts[abilityName] > _favoriteAbilityCount)
        {
            _favoriteAbilityCount = _abilityCasts[abilityName];
            _favoriteAbility = abilityName;
        }
    }

    /// <summary>
    /// Get cast count for a specific ability
    /// </summary>
    public int GetAbilityCastCount(string abilityName)
    {
        return _abilityCasts.TryGetValue(abilityName, out int count) ? count : 0;
    }

    // ================== COMBAT TRACKING ==================

    /// <summary>
    /// Record a melee attack
    /// </summary>
    public void RecordMeleeAttack()
    {
        _meleeAttacks++;
    }

    /// <summary>
    /// Record a ranged attack
    /// </summary>
    public void RecordRangedAttack()
    {
        _rangedAttacks++;
    }

    /// <summary>
    /// Record a successful dodge
    /// </summary>
    public void RecordDodge()
    {
        _dodges++;
    }

    /// <summary>
    /// Record a blocked hit
    /// </summary>
    public void RecordBlock()
    {
        _blockedHits++;
    }

    // ================== SESSION TRACKING ==================

    /// <summary>
    /// Record player death
    /// </summary>
    public void RecordDeath()
    {
        _deathCount++;
    }

    /// <summary>
    /// Record floor cleared
    /// </summary>
    public void RecordFloorCleared()
    {
        _floorsCleared++;
    }

    /// <summary>
    /// Record item looted
    /// </summary>
    public void RecordItemLooted(int goldValue = 0)
    {
        _itemsLooted++;
        _goldCollected += goldValue;
    }

    // ================== AI COMMENTARY HELPERS ==================

    /// <summary>
    /// Get a random recent kill type for commentary context
    /// </summary>
    public string GetRandomRecentKill()
    {
        if (_recentKills.Count == 0) return "";
        var kills = _recentKills.ToArray();
        return kills[GD.RandRange(0, kills.Length - 1)];
    }

    /// <summary>
    /// Check if player is on a kill streak (many kills, no deaths recently)
    /// </summary>
    public bool IsOnKillStreak()
    {
        return _totalKills > 20 && _deathCount == 0;
    }

    /// <summary>
    /// Check if player is struggling (many deaths)
    /// </summary>
    public bool IsStruggling()
    {
        return _deathCount >= 3;
    }

    /// <summary>
    /// Check if player prefers melee
    /// </summary>
    public bool PrefersMelee()
    {
        return _meleeAttacks > _rangedAttacks * 2;
    }

    /// <summary>
    /// Check if player prefers ranged
    /// </summary>
    public bool PrefersRanged()
    {
        return _rangedAttacks > _meleeAttacks * 2;
    }

    /// <summary>
    /// Get damage per kill average
    /// </summary>
    public float GetDamagePerKill()
    {
        return _totalKills > 0 ? _totalDamageDealt / _totalKills : 0f;
    }

    /// <summary>
    /// Get kills per minute
    /// </summary>
    public float GetKillsPerMinute()
    {
        float minutes = _sessionTime / 60f;
        return minutes > 0 ? _totalKills / minutes : 0f;
    }

    // ================== STAT SUMMARY ==================

    /// <summary>
    /// Get a formatted summary of current stats
    /// </summary>
    public string GetStatSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== DUNGEON RUN STATS ===");
        summary.AppendLine($"Time: {FormatTime(_sessionTime)}");
        summary.AppendLine($"Kills: {_totalKills} (Bosses: {_bossKills})");
        summary.AppendLine($"Deaths: {_deathCount}");
        summary.AppendLine($"Damage Dealt: {_totalDamageDealt:F0}");
        summary.AppendLine($"Damage Taken: {_totalDamageTaken:F0}");
        summary.AppendLine($"Abilities Cast: {_totalAbilityCasts}");
        summary.AppendLine($"Items Looted: {_itemsLooted}");
        summary.AppendLine($"Gold: {_goldCollected}");

        if (!string.IsNullOrEmpty(_mostKilledMonster))
            summary.AppendLine($"Most Killed: {_mostKilledMonster} ({_mostKilledCount})");

        if (!string.IsNullOrEmpty(_favoriteAbility))
            summary.AppendLine($"Favorite Ability: {_favoriteAbility}");

        return summary.ToString();
    }

    private string FormatTime(float seconds)
    {
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return $"{mins}:{secs:D2}";
    }

    // ================== RESET ==================

    /// <summary>
    /// Reset all stats for a new run
    /// </summary>
    public void ResetStats()
    {
        _killsByMonsterType.Clear();
        _totalKills = 0;
        _bossKills = 0;
        _multiKills = 0;
        _mostKilledMonster = "";
        _mostKilledCount = 0;

        _totalDamageDealt = 0f;
        _totalDamageTaken = 0f;
        _largestHit = 0f;
        _largestHitTarget = "";

        _abilityCasts.Clear();
        _totalAbilityCasts = 0;
        _favoriteAbility = "";
        _favoriteAbilityCount = 0;

        _meleeAttacks = 0;
        _rangedAttacks = 0;
        _criticalHits = 0;
        _dodges = 0;
        _blockedHits = 0;

        _deathCount = 0;
        _floorsCleared = 0;
        _itemsLooted = 0;
        _goldCollected = 0;
        _sessionTime = 0f;

        _recentKills.Clear();

        GD.Print("[GameStats] Stats reset for new run");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }
}
