using Godot;
using System;

namespace SafeRoom3D.Core;

/// <summary>
/// Centralized event bus for cross-system communication.
/// Provides loose coupling between game systems through static events.
/// </summary>
public static class GameEvents
{
    // === Combat Events ===

    /// <summary>
    /// Fired when a monster is killed.
    /// Parameters: monsterType, experienceValue, position
    /// </summary>
    public static event Action<string, int, Vector3>? MonsterKilled;

    /// <summary>
    /// Fired when a boss is killed.
    /// Parameters: bossId, bossName, position
    /// </summary>
    public static event Action<string, string, Vector3>? BossKilled;

    /// <summary>
    /// Fired when the player takes damage.
    /// Parameters: damage, currentHealth, maxHealth, source
    /// </summary>
    public static event Action<float, int, int, string>? PlayerDamaged;

    /// <summary>
    /// Fired when the player dies.
    /// </summary>
    public static event Action? PlayerDied;

    /// <summary>
    /// Fired when the player's health changes.
    /// Parameters: currentHealth, maxHealth
    /// </summary>
    public static event Action<int, int>? PlayerHealthChanged;

    /// <summary>
    /// Fired when the player's mana changes.
    /// Parameters: currentMana, maxMana
    /// </summary>
    public static event Action<int, int>? PlayerManaChanged;

    // === Item Events ===

    /// <summary>
    /// Fired when an item is picked up.
    /// Parameters: itemId, itemName, quantity
    /// </summary>
    public static event Action<string, string, int>? ItemPickedUp;

    /// <summary>
    /// Fired when a rare/legendary item is looted.
    /// Parameters: itemName, rarity
    /// </summary>
    public static event Action<string, string>? RareItemLooted;

    /// <summary>
    /// Fired when gold is collected.
    /// Parameters: amount, totalGold
    /// </summary>
    public static event Action<int, int>? GoldCollected;

    // === Quest Events ===

    /// <summary>
    /// Fired when a quest is accepted.
    /// Parameters: questId, questName
    /// </summary>
    public static event Action<string, string>? QuestAccepted;

    /// <summary>
    /// Fired when a quest is completed.
    /// Parameters: questId, questName, goldReward, xpReward
    /// </summary>
    public static event Action<string, string, int, int>? QuestCompleted;

    /// <summary>
    /// Fired when quest progress is made.
    /// Parameters: questId, currentProgress, requiredProgress
    /// </summary>
    public static event Action<string, int, int>? QuestProgressUpdated;

    // === Ability Events ===

    /// <summary>
    /// Fired when an ability is used.
    /// Parameters: abilityId, abilityName, manaCost
    /// </summary>
    public static event Action<string, string, int>? AbilityUsed;

    /// <summary>
    /// Fired when an ability goes on cooldown.
    /// Parameters: abilityId, cooldownDuration
    /// </summary>
    public static event Action<string, float>? AbilityCooldownStarted;

    // === Game State Events ===

    /// <summary>
    /// Fired when a new floor is entered.
    /// Parameters: floorNumber
    /// </summary>
    public static event Action<int>? FloorEntered;

    /// <summary>
    /// Fired when the floor timer updates.
    /// Parameters: remainingSeconds
    /// </summary>
    public static event Action<float>? FloorTimerUpdated;

    /// <summary>
    /// Fired when a boss encounter begins.
    /// Parameters: bossId, bossName
    /// </summary>
    public static event Action<string, string>? BossEncounterStarted;

    /// <summary>
    /// Fired when the game is won.
    /// </summary>
    public static event Action? Victory;

    /// <summary>
    /// Fired when the game is lost.
    /// </summary>
    public static event Action? GameOver;

    // === Experience/Level Events ===

    /// <summary>
    /// Fired when the player gains experience.
    /// Parameters: amount, currentXp, xpToLevel
    /// </summary>
    public static event Action<int, int, int>? ExperienceGained;

    /// <summary>
    /// Fired when the player levels up.
    /// Parameters: newLevel
    /// </summary>
    public static event Action<int>? PlayerLeveledUp;

    // === Raise Methods ===

    public static void RaiseMonsterKilled(string monsterType, int xp, Vector3 position)
        => MonsterKilled?.Invoke(monsterType, xp, position);

    public static void RaiseBossKilled(string bossId, string bossName, Vector3 position)
        => BossKilled?.Invoke(bossId, bossName, position);

    public static void RaisePlayerDamaged(float damage, int currentHealth, int maxHealth, string source)
        => PlayerDamaged?.Invoke(damage, currentHealth, maxHealth, source);

    public static void RaisePlayerDied()
        => PlayerDied?.Invoke();

    public static void RaisePlayerHealthChanged(int current, int max)
        => PlayerHealthChanged?.Invoke(current, max);

    public static void RaisePlayerManaChanged(int current, int max)
        => PlayerManaChanged?.Invoke(current, max);

    public static void RaiseItemPickedUp(string itemId, string itemName, int quantity)
        => ItemPickedUp?.Invoke(itemId, itemName, quantity);

    public static void RaiseRareItemLooted(string itemName, string rarity)
        => RareItemLooted?.Invoke(itemName, rarity);

    public static void RaiseGoldCollected(int amount, int total)
        => GoldCollected?.Invoke(amount, total);

    public static void RaiseQuestAccepted(string questId, string questName)
        => QuestAccepted?.Invoke(questId, questName);

    public static void RaiseQuestCompleted(string questId, string questName, int gold, int xp)
        => QuestCompleted?.Invoke(questId, questName, gold, xp);

    public static void RaiseQuestProgressUpdated(string questId, int current, int required)
        => QuestProgressUpdated?.Invoke(questId, current, required);

    public static void RaiseAbilityUsed(string abilityId, string abilityName, int manaCost)
        => AbilityUsed?.Invoke(abilityId, abilityName, manaCost);

    public static void RaiseAbilityCooldownStarted(string abilityId, float duration)
        => AbilityCooldownStarted?.Invoke(abilityId, duration);

    public static void RaiseFloorEntered(int floor)
        => FloorEntered?.Invoke(floor);

    public static void RaiseFloorTimerUpdated(float remaining)
        => FloorTimerUpdated?.Invoke(remaining);

    public static void RaiseBossEncounterStarted(string bossId, string bossName)
        => BossEncounterStarted?.Invoke(bossId, bossName);

    public static void RaiseVictory()
        => Victory?.Invoke();

    public static void RaiseGameOver()
        => GameOver?.Invoke();

    public static void RaiseExperienceGained(int amount, int current, int toLevel)
        => ExperienceGained?.Invoke(amount, current, toLevel);

    public static void RaisePlayerLeveledUp(int newLevel)
        => PlayerLeveledUp?.Invoke(newLevel);

    /// <summary>
    /// Clear all event subscriptions. Call during scene cleanup.
    /// </summary>
    public static void ClearAllSubscriptions()
    {
        MonsterKilled = null;
        BossKilled = null;
        PlayerDamaged = null;
        PlayerDied = null;
        PlayerHealthChanged = null;
        PlayerManaChanged = null;
        ItemPickedUp = null;
        RareItemLooted = null;
        GoldCollected = null;
        QuestAccepted = null;
        QuestCompleted = null;
        QuestProgressUpdated = null;
        AbilityUsed = null;
        AbilityCooldownStarted = null;
        FloorEntered = null;
        FloorTimerUpdated = null;
        BossEncounterStarted = null;
        Victory = null;
        GameOver = null;
        ExperienceGained = null;
        PlayerLeveledUp = null;

        GD.Print("[GameEvents] All subscriptions cleared");
    }
}
