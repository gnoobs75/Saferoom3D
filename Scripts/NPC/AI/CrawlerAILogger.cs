using Godot;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC.AI;

/// <summary>
/// Throttled logging system for Crawler AI decision-making.
/// Logs to both GD.Print (console) and HUD combat log (in-game).
/// Throttled to prevent spam while still providing insight into AI behavior.
/// </summary>
public class CrawlerAILogger
{
    private readonly string _crawlerName;
    private float _logCooldown;
    private float _stateLogCooldown;

    // Colors for different log types
    private static readonly Color DecisionColor = new(0.4f, 0.8f, 1.0f);   // Cyan
    private static readonly Color CombatColor = new(1.0f, 0.6f, 0.2f);     // Orange
    private static readonly Color StateColor = new(0.7f, 0.7f, 0.9f);      // Light purple
    private static readonly Color LootColor = new(1.0f, 0.85f, 0.2f);      // Gold
    private static readonly Color FleeColor = new(1.0f, 0.4f, 0.4f);       // Red
    private static readonly Color DeathColor = new(0.8f, 0.2f, 0.2f);      // Dark red

    public CrawlerAILogger(string crawlerName)
    {
        _crawlerName = crawlerName;
    }

    /// <summary>
    /// Update cooldown timers. Call from _Process.
    /// </summary>
    public void Update(float delta)
    {
        if (_logCooldown > 0) _logCooldown -= delta;
        if (_stateLogCooldown > 0) _stateLogCooldown -= delta;
    }

    /// <summary>
    /// Log a utility AI decision with scores.
    /// Throttled to LogInterval seconds.
    /// </summary>
    public void LogDecision(string decision, Dictionary<string, float> scores)
    {
        if (!CrawlerAIConfig.DebugLoggingEnabled) return;
        if (_logCooldown > 0) return;

        _logCooldown = CrawlerAIConfig.LogInterval;

        // Format scores nicely
        var scoreStr = string.Join(", ", scores
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key}={kv.Value:F2}"));

        // Console log with full details
        GD.Print($"[{_crawlerName}] DECISION: {decision} (scores: {scoreStr})");

        // Combat log with simplified message
        LogToHUD($"[AI] {_crawlerName}: {decision}", DecisionColor);
    }

    /// <summary>
    /// Log a state machine transition.
    /// Slightly less throttled than decisions (more important to see).
    /// </summary>
    public void LogStateChange(AIState fromState, AIState toState, string reason)
    {
        if (!CrawlerAIConfig.DebugLoggingEnabled) return;
        if (_stateLogCooldown > 0) return;

        _stateLogCooldown = CrawlerAIConfig.LogInterval * 0.5f; // Half the throttle

        GD.Print($"[{_crawlerName}] STATE: {fromState} -> {toState} ({reason})");
        LogToHUD($"{_crawlerName}: {fromState} -> {toState}", StateColor);
    }

    /// <summary>
    /// Log combat events. Always logs (important events).
    /// </summary>
    public void LogCombat(string action, string target, int damage)
    {
        // Combat is always logged (not throttled)
        GD.Print($"[{_crawlerName}] COMBAT: {action} {target} for {damage} damage");
        LogToHUD($"{_crawlerName} {action} {target} ({damage} dmg)", CombatColor);
    }

    /// <summary>
    /// Log taking damage.
    /// </summary>
    public void LogDamageTaken(int damage, string source, int currentHP, int maxHP)
    {
        float hpPercent = (float)currentHP / maxHP * 100f;
        GD.Print($"[{_crawlerName}] DAMAGE: Took {damage} from {source} (HP: {currentHP}/{maxHP} = {hpPercent:F0}%)");
        LogToHUD($"{_crawlerName} hit by {source} (-{damage} HP)", CombatColor);
    }

    /// <summary>
    /// Log looting an item.
    /// </summary>
    public void LogLoot(string itemName, int value)
    {
        GD.Print($"[{_crawlerName}] LOOT: Picked up {itemName} (value: {value}g)");
        LogToHUD($"{_crawlerName} looted {itemName}", LootColor);
    }

    /// <summary>
    /// Log selling items at safe zone.
    /// </summary>
    public void LogSale(int totalItems, int totalGold)
    {
        GD.Print($"[{_crawlerName}] SALE: Sold {totalItems} items for {totalGold}g total");
        LogToHUD($"{_crawlerName} sold {totalItems} items for {totalGold}g", LootColor);
    }

    /// <summary>
    /// Log fleeing behavior.
    /// </summary>
    public void LogFlee(string reason, int currentHP, int maxHP)
    {
        float hpPercent = (float)currentHP / maxHP * 100f;
        GD.Print($"[{_crawlerName}] FLEE: {reason} (HP: {hpPercent:F0}%)");
        LogToHUD($"{_crawlerName} is fleeing! ({reason})", FleeColor);
    }

    /// <summary>
    /// Log returning to safe zone.
    /// </summary>
    public void LogReturning(string reason)
    {
        GD.Print($"[{_crawlerName}] RETURNING: Heading to safe zone ({reason})");
        LogToHUD($"{_crawlerName} returning to Bopca ({reason})", StateColor);
    }

    /// <summary>
    /// Log healing at safe zone.
    /// </summary>
    public void LogHealing(int healAmount, int currentHP, int maxHP)
    {
        GD.Print($"[{_crawlerName}] HEAL: +{healAmount} HP (now {currentHP}/{maxHP})");
    }

    /// <summary>
    /// Log death - always shows (critical event).
    /// </summary>
    public void LogDeath(string lastWords, string killedBy)
    {
        GD.Print($"[{_crawlerName}] DEATH: Killed by {killedBy}. Last words: \"{lastWords}\"");
        LogToHUD($"{_crawlerName} has fallen! \"{lastWords}\"", DeathColor);
    }

    /// <summary>
    /// Log a kill (crawler killed a monster).
    /// </summary>
    public void LogKill(string monsterType, int xpGained)
    {
        GD.Print($"[{_crawlerName}] KILL: Defeated {monsterType} (+{xpGained} XP equivalent)");
        LogToHUD($"{_crawlerName} defeated {monsterType}!", CombatColor);
    }

    /// <summary>
    /// Log a personality-specific quip or comment.
    /// </summary>
    public void LogQuip(string quip)
    {
        if (!CrawlerAIConfig.DebugLoggingEnabled) return;
        GD.Print($"[{_crawlerName}] SAYS: \"{quip}\"");
    }

    /// <summary>
    /// Internal helper to log to HUD combat log.
    /// </summary>
    private void LogToHUD(string message, Color color)
    {
        HUD3D.Instance?.AddCombatLogMessage(message, color);
    }
}
