using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Represents a quest objective type.
/// </summary>
public enum QuestObjectiveType
{
    CollectItem,    // Collect X of an item
    KillMonster,    // Kill X of a monster type
    KillBoss        // Kill a specific boss
}

/// <summary>
/// A single objective within a quest.
/// </summary>
public class QuestObjective
{
    public QuestObjectiveType Type { get; set; }
    public string TargetId { get; set; } = "";        // Item ID or Monster type
    public string TargetName { get; set; } = "";      // Display name
    public int RequiredCount { get; set; } = 1;
    public int CurrentCount { get; set; } = 0;

    public bool IsComplete => CurrentCount >= RequiredCount;

    public string GetProgressText()
    {
        return $"{CurrentCount}/{RequiredCount} {TargetName}";
    }
}

/// <summary>
/// Quest reward data.
/// </summary>
public class QuestReward
{
    public int Gold { get; set; }
    public int Experience { get; set; }
    public List<string> ItemIds { get; set; } = new();
}

/// <summary>
/// Quest status enum.
/// </summary>
public enum QuestStatus
{
    Available,      // Can be accepted
    Active,         // Currently being worked on
    ReadyToTurnIn,  // Objectives complete, needs turn-in
    Completed       // Turned in and done
}

/// <summary>
/// Represents a quest that can be given by NPCs like Mordecai.
/// </summary>
public class Quest
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string GiverNpcId { get; set; } = "";      // "mordecai", "bopca", etc.
    public int RequiredLevel { get; set; } = 1;

    public QuestStatus Status { get; set; } = QuestStatus.Available;
    public List<QuestObjective> Objectives { get; set; } = new();
    public QuestReward Reward { get; set; } = new();

    /// <summary>
    /// Check if all objectives are complete.
    /// </summary>
    public bool AreObjectivesComplete()
    {
        foreach (var obj in Objectives)
        {
            if (!obj.IsComplete) return false;
        }
        return true;
    }

    /// <summary>
    /// Update objective progress for item collection.
    /// </summary>
    public void UpdateItemProgress(string itemId, int count)
    {
        foreach (var obj in Objectives)
        {
            if (obj.Type == QuestObjectiveType.CollectItem && obj.TargetId == itemId)
            {
                obj.CurrentCount = count;
                GD.Print($"[Quest] Updated '{Title}' objective: {obj.GetProgressText()}");

                if (AreObjectivesComplete() && Status == QuestStatus.Active)
                {
                    Status = QuestStatus.ReadyToTurnIn;
                    GD.Print($"[Quest] Quest '{Title}' is ready to turn in!");
                }
            }
        }
    }

    /// <summary>
    /// Update objective progress for monster kills.
    /// </summary>
    public void UpdateKillProgress(string monsterType, bool isBoss)
    {
        foreach (var obj in Objectives)
        {
            if (obj.Type == QuestObjectiveType.KillMonster && obj.TargetId == monsterType)
            {
                obj.CurrentCount++;
                GD.Print($"[Quest] Updated '{Title}' kill objective: {obj.GetProgressText()}");
            }
            else if (obj.Type == QuestObjectiveType.KillBoss && isBoss && obj.TargetId == monsterType)
            {
                obj.CurrentCount++;
                GD.Print($"[Quest] Updated '{Title}' boss kill objective: {obj.GetProgressText()}");
            }
        }

        if (AreObjectivesComplete() && Status == QuestStatus.Active)
        {
            Status = QuestStatus.ReadyToTurnIn;
            GD.Print($"[Quest] Quest '{Title}' is ready to turn in!");
        }
    }

    /// <summary>
    /// Get a summary of the quest for display.
    /// </summary>
    public string GetSummary()
    {
        var summary = $"{Title}\n{Description}\n\nObjectives:";
        foreach (var obj in Objectives)
        {
            string checkmark = obj.IsComplete ? "[X]" : "[ ]";
            summary += $"\n{checkmark} {obj.GetProgressText()}";
        }
        summary += $"\n\nRewards: {Reward.Gold}g, {Reward.Experience} XP";
        return summary;
    }
}

/// <summary>
/// Maps monster types to their droppable quest items.
/// </summary>
public static class MonsterPartDatabase
{
    /// <summary>
    /// Get the quest item ID that drops from a monster type.
    /// </summary>
    public static string GetMonsterPartId(string monsterType)
    {
        return monsterType switch
        {
            // Basic monsters
            "goblin" => "goblin_ear",
            "goblin_shaman" => "shaman_staff_fragment",
            "goblin_thrower" => "goblin_throwing_knife",
            "goblin_warlord" or "boss_goblin" => "warlord_banner",
            "slime" => "slime_core",
            "skeleton" => "bone_fragment",
            "wolf" => "wolf_fang",
            "bat" => "bat_wing",
            "dragon" => "dragon_scale",
            "eye" => "evil_eye_lens",
            "mushroom" => "mushroom_cap",
            "spider" => "spider_silk",
            "lizard" => "lizard_scale",
            "badlama" => "badlama_wool",
            "dungeon_rat" => "rat_whisker",

            // Bosses
            "slime_king" or "boss_slime" => "royal_slime_essence",
            "skeleton_lord" or "boss_skeleton" => "ancient_bone",
            "dragon_king" or "boss_dragon" => "dragon_heart",
            "spider_queen" or "boss_spider" => "queen_venom_sac",
            "sporeling_elder" or "boss_mushroom" => "elder_spore",

            // Special monsters
            "crawler_killer" => "crawler_mandible",
            "shadow_stalker" => "shadow_essence",
            "flesh_golem" => "stitched_flesh",
            "plague_bearer" => "plague_vial",
            "living_armor" => "enchanted_plate",

            // Mechanical
            "camera_drone" => "drone_lens",
            "shock_drone" => "shock_capacitor",
            "advertiser_bot" => "ad_crystal",
            "clockwork_spider" => "clockwork_gear",

            // Elemental
            "lava_elemental" => "lava_core",
            "ice_wraith" => "frozen_essence",

            // Other
            "gelatinous_cube" => "cube_residue",
            "void_spawn" => "void_fragment",
            "mimic" => "mimic_tongue",

            // Major bosses
            "the_butcher" or "boss_butcher" => "butcher_cleaver_shard",
            "syndicate_enforcer" or "boss_enforcer" => "syndicate_badge",
            "hive_mother" or "boss_hive" => "hive_queen_egg",
            "architects_favorite" or "boss_architect" => "architect_blueprint",
            "mordecais_shadow" or "boss_mordecai" => "shadow_crystal",

            _ => "monster_essence"  // Generic fallback
        };
    }

    /// <summary>
    /// Get display name for a monster part.
    /// </summary>
    public static string GetMonsterPartName(string partId)
    {
        return partId switch
        {
            "goblin_ear" => "Goblin Ear",
            "shaman_staff_fragment" => "Shaman Staff Fragment",
            "goblin_throwing_knife" => "Goblin Throwing Knife",
            "warlord_banner" => "Warlord Banner Piece",
            "slime_core" => "Slime Core",
            "bone_fragment" => "Bone Fragment",
            "wolf_fang" => "Wolf Fang",
            "bat_wing" => "Bat Wing",
            "dragon_scale" => "Dragon Scale",
            "evil_eye_lens" => "Evil Eye Lens",
            "mushroom_cap" => "Mushroom Cap",
            "spider_silk" => "Spider Silk",
            "lizard_scale" => "Lizard Scale",
            "badlama_wool" => "Badlama Wool",
            "rat_whisker" => "Rat Whisker",
            "royal_slime_essence" => "Royal Slime Essence",
            "ancient_bone" => "Ancient Bone",
            "dragon_heart" => "Dragon Heart",
            "queen_venom_sac" => "Queen Venom Sac",
            "elder_spore" => "Elder Spore",
            "crawler_mandible" => "Crawler Mandible",
            "shadow_essence" => "Shadow Essence",
            "stitched_flesh" => "Stitched Flesh",
            "plague_vial" => "Plague Vial",
            "enchanted_plate" => "Enchanted Plate",
            "drone_lens" => "Drone Lens",
            "shock_capacitor" => "Shock Capacitor",
            "ad_crystal" => "Ad Crystal",
            "clockwork_gear" => "Clockwork Gear",
            "lava_core" => "Lava Core",
            "frozen_essence" => "Frozen Essence",
            "cube_residue" => "Cube Residue",
            "void_fragment" => "Void Fragment",
            "mimic_tongue" => "Mimic Tongue",
            "butcher_cleaver_shard" => "Butcher's Cleaver Shard",
            "syndicate_badge" => "Syndicate Badge",
            "hive_queen_egg" => "Hive Queen Egg",
            "architect_blueprint" => "Architect Blueprint",
            "shadow_crystal" => "Shadow Crystal",
            "monster_essence" => "Monster Essence",
            _ => partId.Replace("_", " ").ToLower()
        };
    }

    /// <summary>
    /// Get display name for a monster type.
    /// </summary>
    public static string GetMonsterDisplayName(string monsterType)
    {
        return monsterType switch
        {
            "goblin" => "Goblin",
            "goblin_shaman" => "Goblin Shaman",
            "goblin_thrower" => "Goblin Thrower",
            "goblin_warlord" or "boss_goblin" => "Goblin Warlord",
            "slime" => "Slime",
            "skeleton" => "Skeleton",
            "wolf" => "Wolf",
            "bat" => "Bat",
            "dragon" => "Dragon",
            "eye" => "Evil Eye",
            "mushroom" => "Mushroom",
            "spider" => "Spider",
            "lizard" => "Lizard",
            "badlama" => "Badlama",
            "dungeon_rat" => "Dungeon Rat",
            "slime_king" or "boss_slime" => "Slime King",
            "skeleton_lord" or "boss_skeleton" => "Skeleton Lord",
            "dragon_king" or "boss_dragon" => "Dragon King",
            "spider_queen" or "boss_spider" => "Spider Queen",
            "sporeling_elder" or "boss_mushroom" => "Sporeling Elder",
            "crawler_killer" => "Crawler Killer",
            "shadow_stalker" => "Shadow Stalker",
            "flesh_golem" => "Flesh Golem",
            "plague_bearer" => "Plague Bearer",
            "living_armor" => "Living Armor",
            "camera_drone" => "Camera Drone",
            "shock_drone" => "Shock Drone",
            "advertiser_bot" => "Advertiser Bot",
            "clockwork_spider" => "Clockwork Spider",
            "lava_elemental" => "Lava Elemental",
            "ice_wraith" => "Ice Wraith",
            "gelatinous_cube" => "Gelatinous Cube",
            "void_spawn" => "Void Spawn",
            "mimic" => "Mimic",
            "the_butcher" or "boss_butcher" => "The Butcher",
            "syndicate_enforcer" or "boss_enforcer" => "Syndicate Enforcer",
            "hive_mother" or "boss_hive" => "Hive Mother",
            "architects_favorite" or "boss_architect" => "Architect's Favorite",
            "mordecais_shadow" or "boss_mordecai" => "Mordecai's Shadow",
            _ => monsterType.Replace("_", " ")
        };
    }

    /// <summary>
    /// Get color for a monster part item in UI.
    /// </summary>
    public static Godot.Color GetPartColor(string partId)
    {
        // Boss parts are purple/legendary
        if (partId.Contains("royal") || partId.Contains("ancient") || partId.Contains("dragon_heart") ||
            partId.Contains("queen") || partId.Contains("elder") || partId.Contains("butcher") ||
            partId.Contains("syndicate") || partId.Contains("hive") || partId.Contains("architect") ||
            partId.Contains("shadow_crystal"))
        {
            return new Godot.Color(0.7f, 0.4f, 0.9f); // Purple
        }

        // Elemental parts are their element color
        if (partId.Contains("lava") || partId.Contains("fire"))
            return new Godot.Color(1f, 0.4f, 0.2f); // Orange-red
        if (partId.Contains("frozen") || partId.Contains("ice"))
            return new Godot.Color(0.4f, 0.8f, 1f); // Ice blue
        if (partId.Contains("shadow") || partId.Contains("void"))
            return new Godot.Color(0.3f, 0.2f, 0.4f); // Dark purple

        // Material-based colors
        if (partId.Contains("slime"))
            return new Godot.Color(0.3f, 0.9f, 0.3f); // Green
        if (partId.Contains("bone") || partId.Contains("skeleton"))
            return new Godot.Color(0.9f, 0.9f, 0.85f); // Bone white
        if (partId.Contains("blood") || partId.Contains("flesh"))
            return new Godot.Color(0.7f, 0.2f, 0.2f); // Red

        // Default brownish for organic parts
        return new Godot.Color(0.7f, 0.55f, 0.4f);
    }
}
