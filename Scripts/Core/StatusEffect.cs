using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Types of status effects that can be applied to entities.
/// </summary>
public enum StatusEffectType
{
    None = 0,
    Burning,        // Fire damage over time, orange/red particles
    Electrified,    // Lightning damage, sparks and crackling
    Frozen,         // Slowed movement, ice crystals, blue tint
    Poisoned,       // Damage over time, green bubbles
    Bleeding,       // Damage over time, red drips
    Petrified,      // Cannot move, gray stone texture
    Cursed,         // Debuffed stats, dark purple aura
    Blessed,        // Buffed stats, golden glow
    Confused,       // Random movement, spiral particles
    Enraged         // Increased damage, red glow/veins
}

/// <summary>
/// Configuration for a status effect type.
/// </summary>
public class StatusEffectConfig
{
    public StatusEffectType Type { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float Duration { get; set; } = 5f;
    public float TickInterval { get; set; } = 1f;
    public float DamagePerTick { get; set; } = 0f;
    public float SpeedModifier { get; set; } = 1f;
    public float DamageModifier { get; set; } = 1f;
    public float DefenseModifier { get; set; } = 1f;
    public Color PrimaryColor { get; set; } = Colors.White;
    public Color SecondaryColor { get; set; } = Colors.White;
    public bool PreventMovement { get; set; } = false;
    public bool PreventAttack { get; set; } = false;
}

/// <summary>
/// Represents an active status effect on an entity.
/// </summary>
public class ActiveStatusEffect
{
    public StatusEffectType Type { get; set; }
    public float RemainingDuration { get; set; }
    public float TickTimer { get; set; }
    public int StackCount { get; set; } = 1;
    public Node3D? VisualEffect { get; set; }

    public ActiveStatusEffect(StatusEffectType type, float duration)
    {
        Type = type;
        RemainingDuration = duration;
        TickTimer = 0f;
    }
}

/// <summary>
/// Manages status effect definitions and provides utility methods.
/// </summary>
public static class StatusEffectManager
{
    private static readonly Dictionary<StatusEffectType, StatusEffectConfig> _configs = new();

    static StatusEffectManager()
    {
        InitializeConfigs();
    }

    private static void InitializeConfigs()
    {
        // Burning - Fire damage, fast ticks
        _configs[StatusEffectType.Burning] = new StatusEffectConfig
        {
            Type = StatusEffectType.Burning,
            Name = "Burning",
            Description = "Taking fire damage over time",
            Duration = 5f,
            TickInterval = 0.5f,
            DamagePerTick = 3f,
            PrimaryColor = new Color(1f, 0.4f, 0.1f),
            SecondaryColor = new Color(1f, 0.8f, 0.2f)
        };

        // Electrified - Shock damage, slower ticks
        _configs[StatusEffectType.Electrified] = new StatusEffectConfig
        {
            Type = StatusEffectType.Electrified,
            Name = "Electrified",
            Description = "Crackling with electricity",
            Duration = 4f,
            TickInterval = 0.8f,
            DamagePerTick = 5f,
            SpeedModifier = 0.8f,
            PrimaryColor = new Color(0.4f, 0.7f, 1f),
            SecondaryColor = new Color(0.9f, 0.95f, 1f)
        };

        // Frozen - Slowed, no damage
        _configs[StatusEffectType.Frozen] = new StatusEffectConfig
        {
            Type = StatusEffectType.Frozen,
            Name = "Frozen",
            Description = "Movement severely slowed",
            Duration = 4f,
            TickInterval = 1f,
            DamagePerTick = 0f,
            SpeedModifier = 0.3f,
            PrimaryColor = new Color(0.5f, 0.8f, 1f),
            SecondaryColor = new Color(0.9f, 0.95f, 1f)
        };

        // Poisoned - Damage over time
        _configs[StatusEffectType.Poisoned] = new StatusEffectConfig
        {
            Type = StatusEffectType.Poisoned,
            Name = "Poisoned",
            Description = "Taking poison damage over time",
            Duration = 8f,
            TickInterval = 1f,
            DamagePerTick = 2f,
            PrimaryColor = new Color(0.3f, 0.8f, 0.2f),
            SecondaryColor = new Color(0.5f, 0.9f, 0.3f)
        };

        // Bleeding - Damage over time
        _configs[StatusEffectType.Bleeding] = new StatusEffectConfig
        {
            Type = StatusEffectType.Bleeding,
            Name = "Bleeding",
            Description = "Losing blood rapidly",
            Duration = 6f,
            TickInterval = 0.7f,
            DamagePerTick = 2f,
            PrimaryColor = new Color(0.8f, 0.1f, 0.1f),
            SecondaryColor = new Color(0.5f, 0.05f, 0.05f)
        };

        // Petrified - Cannot move or attack
        _configs[StatusEffectType.Petrified] = new StatusEffectConfig
        {
            Type = StatusEffectType.Petrified,
            Name = "Petrified",
            Description = "Turned to stone, cannot move",
            Duration = 3f,
            TickInterval = 1f,
            DamagePerTick = 0f,
            SpeedModifier = 0f,
            DefenseModifier = 2f,
            PreventMovement = true,
            PreventAttack = true,
            PrimaryColor = new Color(0.5f, 0.5f, 0.5f),
            SecondaryColor = new Color(0.35f, 0.35f, 0.35f)
        };

        // Cursed - Debuffed
        _configs[StatusEffectType.Cursed] = new StatusEffectConfig
        {
            Type = StatusEffectType.Cursed,
            Name = "Cursed",
            Description = "Dark magic weakens you",
            Duration = 10f,
            TickInterval = 2f,
            DamagePerTick = 1f,
            DamageModifier = 0.7f,
            DefenseModifier = 0.7f,
            PrimaryColor = new Color(0.4f, 0.1f, 0.5f),
            SecondaryColor = new Color(0.2f, 0.05f, 0.3f)
        };

        // Blessed - Buffed
        _configs[StatusEffectType.Blessed] = new StatusEffectConfig
        {
            Type = StatusEffectType.Blessed,
            Name = "Blessed",
            Description = "Holy light empowers you",
            Duration = 10f,
            TickInterval = 2f,
            DamagePerTick = -2f, // Healing!
            DamageModifier = 1.2f,
            DefenseModifier = 1.2f,
            PrimaryColor = new Color(1f, 0.95f, 0.6f),
            SecondaryColor = new Color(1f, 1f, 0.9f)
        };

        // Confused - Random movement
        _configs[StatusEffectType.Confused] = new StatusEffectConfig
        {
            Type = StatusEffectType.Confused,
            Name = "Confused",
            Description = "Cannot control movement",
            Duration = 5f,
            TickInterval = 1f,
            DamagePerTick = 0f,
            SpeedModifier = 0.6f,
            PrimaryColor = new Color(0.8f, 0.6f, 1f),
            SecondaryColor = new Color(0.9f, 0.8f, 1f)
        };

        // Enraged - Increased damage, reduced defense
        _configs[StatusEffectType.Enraged] = new StatusEffectConfig
        {
            Type = StatusEffectType.Enraged,
            Name = "Enraged",
            Description = "Berserk fury increases damage",
            Duration = 8f,
            TickInterval = 1f,
            DamagePerTick = 0f,
            SpeedModifier = 1.2f,
            DamageModifier = 1.5f,
            DefenseModifier = 0.5f,
            PrimaryColor = new Color(1f, 0.2f, 0.2f),
            SecondaryColor = new Color(0.8f, 0.1f, 0.1f)
        };
    }

    /// <summary>
    /// Gets the configuration for a status effect type.
    /// </summary>
    public static StatusEffectConfig GetConfig(StatusEffectType type)
    {
        return _configs.TryGetValue(type, out var config) ? config : new StatusEffectConfig { Type = type, Name = type.ToString() };
    }

    /// <summary>
    /// Gets all available status effect types (excluding None).
    /// </summary>
    public static StatusEffectType[] GetAllTypes()
    {
        return new[]
        {
            StatusEffectType.Burning,
            StatusEffectType.Electrified,
            StatusEffectType.Frozen,
            StatusEffectType.Poisoned,
            StatusEffectType.Bleeding,
            StatusEffectType.Petrified,
            StatusEffectType.Cursed,
            StatusEffectType.Blessed,
            StatusEffectType.Confused,
            StatusEffectType.Enraged
        };
    }

    /// <summary>
    /// Gets a formatted display name for a status effect.
    /// </summary>
    public static string GetDisplayName(StatusEffectType type)
    {
        return GetConfig(type).Name;
    }
}
