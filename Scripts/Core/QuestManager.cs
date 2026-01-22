using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Items;
using SafeRoom3D.NPC;

namespace SafeRoom3D.Core;

/// <summary>
/// Manages all quests in the game. Generates collection and kill quests based on
/// monsters present on the current floor. Uses MordecaiQuestDatabase for funny stories.
/// </summary>
public partial class QuestManager : Node
{
    public static QuestManager? Instance { get; private set; }

    // All quests in the game
    private Dictionary<string, Quest> _allQuests = new();

    // Currently active quests
    private List<Quest> _activeQuests = new();

    // Completed quest IDs (persist across session)
    private HashSet<string> _completedQuestIds = new();

    // Current floor's monster types for contextual quest stories
    private List<string> _currentFloorMonsters = new();

    // Quest generation settings
    private const int MinCollectionAmount = 3;
    private const int MaxCollectionAmount = 10;
    private const int MinKillAmount = 5;
    private const int MaxKillAmount = 15;
    private const int BaseGoldReward = 50;
    private const int BaseXpReward = 100;

    // Events
    [Signal]
    public delegate void QuestAcceptedEventHandler(string questId);

    [Signal]
    public delegate void QuestCompletedEventHandler(string questId);

    [Signal]
    public delegate void QuestProgressUpdatedEventHandler(string questId);

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[QuestManager] Quest Manager initialized");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Generate quests based on monsters present on the current floor.
    /// Called when loading a new map.
    /// </summary>
    public void GenerateQuestsForFloor(List<string> monsterTypes, int floorLevel = 1)
    {
        _allQuests.Clear();

        // Get unique monster types (exclude bosses for collection/kill quests)
        var normalMonsters = monsterTypes
            .Where(m => !m.Contains("boss") && !m.Contains("king") && !m.Contains("queen") && !m.Contains("lord") && !m.Contains("elder"))
            .Distinct()
            .ToList();

        // Store for contextual quest stories
        _currentFloorMonsters = new List<string>(normalMonsters);

        GD.Print($"[QuestManager] Generating quests for {normalMonsters.Count} monster types on floor {floorLevel}");

        // Generate both collection AND kill quests per monster type
        foreach (var monsterType in normalMonsters)
        {
            var partId = MonsterPartDatabase.GetMonsterPartId(monsterType);
            var partName = MonsterPartDatabase.GetMonsterPartName(partId);
            var monsterName = MonsterPartDatabase.GetMonsterDisplayName(monsterType);

            // Get other monsters for contextual stories
            var otherMonsters = normalMonsters.Where(m => m != monsterType).ToList();

            // === KILL QUESTS ===
            // Easy kill quest (5-8 kills)
            GenerateKillQuest(monsterType, monsterName, GD.RandRange(MinKillAmount, 8), floorLevel, "easy", otherMonsters);

            // Medium kill quest (8-12 kills) - floor 2+
            if (floorLevel > 1)
            {
                GenerateKillQuest(monsterType, monsterName, GD.RandRange(8, 12), floorLevel, "medium", otherMonsters);
            }

            // Hard kill quest (12-15 kills) - floor 3+
            if (floorLevel > 2)
            {
                GenerateKillQuest(monsterType, monsterName, GD.RandRange(12, MaxKillAmount), floorLevel, "hard", otherMonsters);
            }

            // === COLLECTION QUESTS ===
            // Easy collection quest (3-5 items)
            GenerateCollectionQuest(monsterType, partId, partName, monsterName, GD.RandRange(MinCollectionAmount, 5), floorLevel, "easy", otherMonsters);

            // Medium collection quest (6-8 items) - floor 2+
            if (floorLevel > 1)
            {
                GenerateCollectionQuest(monsterType, partId, partName, monsterName, GD.RandRange(6, 8), floorLevel, "medium", otherMonsters);
            }

            // Hard collection quest (8-10 items) - floor 3+
            if (floorLevel > 2)
            {
                GenerateCollectionQuest(monsterType, partId, partName, monsterName, GD.RandRange(8, MaxCollectionAmount), floorLevel, "hard", otherMonsters);
            }
        }

        // Generate boss kill quests
        var bossTypes = monsterTypes
            .Where(m => m.Contains("boss") || m.Contains("king") || m.Contains("queen") || m.Contains("lord") || m.Contains("elder"))
            .Distinct()
            .ToList();

        foreach (var bossType in bossTypes)
        {
            GenerateBossQuest(bossType, floorLevel);
        }

        GD.Print($"[QuestManager] Generated {_allQuests.Count} quests total ({normalMonsters.Count} monster types, {bossTypes.Count} bosses)");
    }

    private void GenerateKillQuest(string monsterType, string monsterName, int amount,
        int floorLevel, string difficulty, List<string> otherMonsters)
    {
        string questId = $"kill_{monsterType}_{amount}_{difficulty}";

        // Skip if already completed
        if (_completedQuestIds.Contains(questId)) return;

        // Calculate rewards based on difficulty and floor
        float difficultyMultiplier = difficulty switch
        {
            "easy" => 1f,
            "medium" => 1.5f,
            "hard" => 2f,
            _ => 1f
        };

        int goldReward = (int)(BaseGoldReward * amount * 0.8f * difficultyMultiplier * (1 + floorLevel * 0.2f));
        int xpReward = (int)(BaseXpReward * amount * difficultyMultiplier * (1 + floorLevel * 0.15f));

        // Use MordecaiQuestDatabase for funny titles and descriptions
        var quest = new Quest
        {
            Id = questId,
            Title = MordecaiQuestDatabase.GenerateKillQuestTitle(monsterType, amount),
            Description = MordecaiQuestDatabase.GenerateKillQuestDescription(monsterType, amount, otherMonsters),
            GiverNpcId = "mordecai",
            RequiredLevel = floorLevel,
            Difficulty = difficulty,
            Status = QuestStatus.Available,
            Objectives = new List<QuestObjective>
            {
                new QuestObjective
                {
                    Type = QuestObjectiveType.KillMonster,
                    TargetId = monsterType,
                    TargetName = monsterName,
                    RequiredCount = amount,
                    CurrentCount = 0
                }
            },
            Reward = new QuestReward
            {
                Gold = goldReward,
                Experience = xpReward
            }
        };

        _allQuests[questId] = quest;
    }

    private void GenerateCollectionQuest(string monsterType, string partId, string partName,
        string monsterName, int amount, int floorLevel, string difficulty, List<string> otherMonsters)
    {
        string questId = $"collect_{partId}_{amount}_{difficulty}";

        // Skip if already completed
        if (_completedQuestIds.Contains(questId)) return;

        // Calculate rewards based on difficulty and floor
        float difficultyMultiplier = difficulty switch
        {
            "easy" => 1f,
            "medium" => 1.5f,
            "hard" => 2f,
            _ => 1f
        };

        int goldReward = (int)(BaseGoldReward * amount * difficultyMultiplier * (1 + floorLevel * 0.2f));
        int xpReward = (int)(BaseXpReward * amount * difficultyMultiplier * (1 + floorLevel * 0.15f));

        // Use MordecaiQuestDatabase for funny titles and descriptions
        var quest = new Quest
        {
            Id = questId,
            Title = MordecaiQuestDatabase.GenerateCollectionQuestTitle(monsterType, partName, amount),
            Description = MordecaiQuestDatabase.GenerateCollectionQuestDescription(monsterType, partName, amount, otherMonsters),
            GiverNpcId = "mordecai",
            RequiredLevel = floorLevel,
            Difficulty = difficulty,
            Status = QuestStatus.Available,
            Objectives = new List<QuestObjective>
            {
                new QuestObjective
                {
                    Type = QuestObjectiveType.CollectItem,
                    TargetId = partId,
                    TargetName = partName,
                    RequiredCount = amount,
                    CurrentCount = 0
                }
            },
            Reward = new QuestReward
            {
                Gold = goldReward,
                Experience = xpReward
            }
        };

        _allQuests[questId] = quest;
    }

    private void GenerateBossQuest(string bossType, int floorLevel)
    {
        string questId = $"slay_{bossType}";

        // Skip if already completed
        if (_completedQuestIds.Contains(questId)) return;

        var bossName = MonsterPartDatabase.GetMonsterDisplayName(bossType);
        int goldReward = BaseGoldReward * 10 * floorLevel;
        int xpReward = BaseXpReward * 10 * floorLevel;

        // Use MordecaiQuestDatabase for funny boss quest stories
        var quest = new Quest
        {
            Id = questId,
            Title = MordecaiQuestDatabase.GenerateBossQuestTitle(bossType),
            Description = MordecaiQuestDatabase.GenerateBossQuestDescription(bossType),
            GiverNpcId = "mordecai",
            RequiredLevel = floorLevel,
            Difficulty = "boss",
            Status = QuestStatus.Available,
            Objectives = new List<QuestObjective>
            {
                new QuestObjective
                {
                    Type = QuestObjectiveType.KillBoss,
                    TargetId = bossType,
                    TargetName = bossName,
                    RequiredCount = 1,
                    CurrentCount = 0
                }
            },
            Reward = new QuestReward
            {
                Gold = goldReward,
                Experience = xpReward
            }
        };

        _allQuests[questId] = quest;
    }

    /// <summary>
    /// Get available quests for a specific NPC.
    /// </summary>
    public List<Quest> GetAvailableQuestsForNPC(BaseNPC3D npc)
    {
        string npcId = npc switch
        {
            Mordecai3D => "mordecai",
            _ => ""
        };

        return _allQuests.Values
            .Where(q => q.GiverNpcId == npcId && q.Status == QuestStatus.Available)
            .OrderBy(q => q.RequiredLevel)
            .Take(5) // Limit to 5 available at a time
            .ToList();
    }

    /// <summary>
    /// Get active quests from a specific NPC.
    /// </summary>
    public List<Quest> GetActiveQuestsFromNPC(BaseNPC3D npc)
    {
        string npcId = npc switch
        {
            Mordecai3D => "mordecai",
            _ => ""
        };

        return _activeQuests
            .Where(q => q.GiverNpcId == npcId && q.Status == QuestStatus.Active)
            .ToList();
    }

    /// <summary>
    /// Get completed quests ready to turn in.
    /// </summary>
    public List<Quest> GetCompletedQuestsForNPC(BaseNPC3D npc)
    {
        string npcId = npc switch
        {
            Mordecai3D => "mordecai",
            _ => ""
        };

        return _activeQuests
            .Where(q => q.GiverNpcId == npcId && q.Status == QuestStatus.ReadyToTurnIn)
            .ToList();
    }

    /// <summary>
    /// Accept a quest from an NPC.
    /// </summary>
    public void AcceptQuest(Quest quest)
    {
        if (quest.Status != QuestStatus.Available) return;

        quest.Status = QuestStatus.Active;
        _activeQuests.Add(quest);

        // Check if player already has some of the required items
        UpdateQuestItemProgress(quest);

        EmitSignal(SignalName.QuestAccepted, quest.Id);
        GD.Print($"[QuestManager] Accepted quest: {quest.Title}");
    }

    /// <summary>
    /// Complete a quest and give rewards.
    /// </summary>
    public void CompleteQuest(Quest quest)
    {
        if (quest.Status != QuestStatus.ReadyToTurnIn) return;

        // Remove required items from inventory
        foreach (var obj in quest.Objectives)
        {
            if (obj.Type == QuestObjectiveType.CollectItem)
            {
                Inventory3D.Instance?.RemoveItemById(obj.TargetId, obj.RequiredCount);
            }
        }

        // Give rewards
        Inventory3D.Instance?.AddGold(quest.Reward.Gold);
        // TODO: Add XP to player when XP system exists

        quest.Status = QuestStatus.Completed;
        _activeQuests.Remove(quest);
        _completedQuestIds.Add(quest.Id);

        EmitSignal(SignalName.QuestCompleted, quest.Id);
        GD.Print($"[QuestManager] Completed quest: {quest.Title} (+{quest.Reward.Gold}g, +{quest.Reward.Experience} XP)");

        // Show reward notification
        UI.HUD3D.Instance?.AddCombatLogMessage($"Quest Complete: {quest.Title}", new Color(1f, 0.85f, 0.2f));
        UI.HUD3D.Instance?.AddCombatLogMessage($"+{quest.Reward.Gold} Gold", new Color(1f, 0.85f, 0.2f));
    }

    /// <summary>
    /// Called when player picks up an item. Updates quest progress.
    /// </summary>
    public void OnItemPickedUp(string itemId, int totalCount)
    {
        foreach (var quest in _activeQuests)
        {
            quest.UpdateItemProgress(itemId, totalCount);
        }

        EmitSignal(SignalName.QuestProgressUpdated, "");
    }

    /// <summary>
    /// Called when player kills a monster. Updates quest progress.
    /// </summary>
    public void OnMonsterKilled(string monsterType, bool isBoss)
    {
        foreach (var quest in _activeQuests)
        {
            quest.UpdateKillProgress(monsterType, isBoss);
        }

        EmitSignal(SignalName.QuestProgressUpdated, "");
    }

    /// <summary>
    /// Update quest progress based on current inventory.
    /// </summary>
    private void UpdateQuestItemProgress(Quest quest)
    {
        if (Inventory3D.Instance == null) return;

        foreach (var obj in quest.Objectives)
        {
            if (obj.Type == QuestObjectiveType.CollectItem)
            {
                int count = Inventory3D.Instance.CountItemById(obj.TargetId);
                quest.UpdateItemProgress(obj.TargetId, count);
            }
        }
    }

    /// <summary>
    /// Get all active quests.
    /// </summary>
    public List<Quest> GetAllActiveQuests()
    {
        return new List<Quest>(_activeQuests);
    }

    /// <summary>
    /// Check if a specific quest is active.
    /// </summary>
    public bool IsQuestActive(string questId)
    {
        return _activeQuests.Any(q => q.Id == questId);
    }

    /// <summary>
    /// Get active quest count.
    /// </summary>
    public int GetActiveQuestCount()
    {
        return _activeQuests.Count;
    }
}
