using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SafeRoom3D.NPC.AI;

/// <summary>
/// Utility AI decision-making system for Crawler NPCs.
/// Evaluates all possible actions and scores them based on current situation.
/// The highest-scoring action is chosen.
/// </summary>
public class CrawlerUtilityScorer
{
    private readonly CrawlerAIBase _crawler;
    private CrawlerPersonality Personality => _crawler.Personality;

    // Base weights for actions
    private const float BaseWeight_Attack = 1.0f;
    private const float BaseWeight_Loot = 0.8f;
    private const float BaseWeight_Return = 0.6f;
    private const float BaseWeight_Flee = 1.5f;
    private const float BaseWeight_Patrol = 0.3f;
    private const float BaseWeight_Idle = 0.1f;

    public CrawlerUtilityScorer(CrawlerAIBase crawler)
    {
        _crawler = crawler;
    }

    /// <summary>
    /// Evaluate all possible actions and return the best one with all scores.
    /// </summary>
    public (AIAction bestAction, Dictionary<string, float> scores) EvaluateBestAction()
    {
        var scores = new Dictionary<string, float>
        {
            ["Attack"] = ScoreAttack(),
            ["Loot"] = ScoreLoot(),
            ["Return"] = ScoreReturnToSafeZone(),
            ["Flee"] = ScoreFlee(),
            ["Patrol"] = ScorePatrol(),
            ["Idle"] = ScoreIdle()
        };

        // Find highest scoring action
        var best = scores.OrderByDescending(kv => kv.Value).First();

        var action = best.Key switch
        {
            "Attack" => AIAction.Attack,
            "Loot" => AIAction.Loot,
            "Return" => AIAction.ReturnToSafeZone,
            "Flee" => AIAction.Flee,
            "Patrol" => AIAction.Patrol,
            _ => AIAction.Idle
        };

        return (action, scores);
    }

    /// <summary>
    /// Score the Attack action.
    /// Higher when: high HP, enemies nearby, low threat, reckless personality
    /// </summary>
    private float ScoreAttack()
    {
        int enemyCount = _crawler.GetNearbyEnemyCount();
        if (enemyCount == 0) return 0f;

        float hpPercent = (float)_crawler.CurrentHealth / _crawler.MaxHealth;

        // Won't fight below minimum HP threshold (unless personality says otherwise)
        if (hpPercent < Personality.EngageMinHealthPercent && !Personality.RecklessCharge)
            return 0f;

        // Too many enemies - might want to flee instead
        float enemyFactor = 1f - (float)enemyCount / (Personality.MaxSimultaneousEnemies + 1);
        enemyFactor = Mathf.Max(0.1f, enemyFactor);

        // Personality bonuses
        float personalityBonus = 0f;
        if (Personality.RecklessCharge) personalityBonus += 0.3f;  // Chad: fight more
        if (Personality.NeverFlees) personalityBonus += 0.2f;       // Chad: really fight more

        // Preference for weak targets reduces score if no weak targets available
        // (The actual targeting filter is in FindNearestEnemy)
        float weakTargetPenalty = Personality.PreferWeakTargets ? 0.3f : 0f;

        float score = BaseWeight_Attack * hpPercent * enemyFactor + personalityBonus - weakTargetPenalty;
        return Mathf.Max(0f, score);
    }

    /// <summary>
    /// Score the Loot action.
    /// Higher when: corpses nearby, inventory has space, low threat
    /// </summary>
    private float ScoreLoot()
    {
        int corpseCount = _crawler.GetNearbyCorpseCount();
        if (corpseCount == 0) return 0f;

        // Don't loot if inventory is full
        float inventorySpace = 1f - _crawler.Inventory.GetFullnessPercent();
        if (inventorySpace <= 0.05f) return 0f;

        // Threat check - don't loot in dangerous areas
        int enemyCount = _crawler.GetNearbyEnemyCount();
        float threatPenalty = enemyCount * 0.15f;

        // Personality modifier
        float personalityMult = Personality.LootPriorityMultiplier;

        float score = BaseWeight_Loot * personalityMult * inventorySpace * (1f - threatPenalty);
        return Mathf.Max(0f, score);
    }

    /// <summary>
    /// Score the ReturnToSafeZone action.
    /// Higher when: low HP, inventory full, far from danger
    /// </summary>
    private float ScoreReturnToSafeZone()
    {
        float hpPercent = (float)_crawler.CurrentHealth / _crawler.MaxHealth;
        float invFullness = _crawler.Inventory.GetFullnessPercent();

        // Urgent return if HP is low
        if (hpPercent <= Personality.ReturnHealthPercent)
            return BaseWeight_Return * 2f;

        // Return if inventory is full
        if (invFullness >= Personality.InventoryFullnessToSell)
            return BaseWeight_Return * 1.5f;

        // Distance factor - closer to safe zone = higher score if we want to return
        float distToSafe = _crawler.GetDistanceToSafeZone();
        float distFactor = Mathf.Clamp(1f - distToSafe / 50f, 0f, 1f);

        // Combined urgency
        float urgency = Mathf.Max(1f - hpPercent, invFullness);

        float score = BaseWeight_Return * urgency * (0.5f + distFactor * 0.5f);
        return Mathf.Max(0f, score);
    }

    /// <summary>
    /// Score the Flee action.
    /// Very high when HP is critically low.
    /// </summary>
    private float ScoreFlee()
    {
        // Never flee personality
        if (Personality.NeverFlees) return 0f;

        float hpPercent = (float)_crawler.CurrentHealth / _crawler.MaxHealth;
        int enemyCount = _crawler.GetNearbyEnemyCount();

        // Must flee if below flee threshold
        if (hpPercent <= Personality.FleeHealthPercent)
            return BaseWeight_Flee * 2f;

        // Consider fleeing if HP is low AND enemies are nearby
        if (hpPercent < 0.4f && enemyCount > 0)
        {
            float threatLevel = (float)enemyCount / Personality.MaxSimultaneousEnemies;
            if (threatLevel > 0.5f)
                return BaseWeight_Flee * threatLevel;
        }

        return 0f;
    }

    /// <summary>
    /// Score the Patrol action.
    /// Default action when nothing else is pressing.
    /// </summary>
    private float ScorePatrol()
    {
        // Don't patrol if enemies nearby
        int enemyCount = _crawler.GetNearbyEnemyCount();
        if (enemyCount > 0) return 0f;

        // Don't patrol if should return
        float hpPercent = (float)_crawler.CurrentHealth / _crawler.MaxHealth;
        if (hpPercent <= Personality.ReturnHealthPercent)
            return 0f;

        // Patrol as default exploration
        return BaseWeight_Patrol;
    }

    /// <summary>
    /// Score the Idle action.
    /// Lowest priority - only chosen when nothing else to do.
    /// </summary>
    private float ScoreIdle()
    {
        return BaseWeight_Idle;
    }
}
