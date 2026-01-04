using Godot;
using System;

namespace SafeRoom3D.Core;

// Use MonsterMeshFactory's LODLevel enum for graphics settings
using LODLevel = SafeRoom3D.Enemies.MonsterMeshFactory.LODLevel;

/// <summary>
/// Manages graphics settings for the current session.
/// Settings reset to defaults each time the game starts.
/// Does NOT persist between sessions.
/// </summary>
public static class GraphicsConfig
{
    // Default state: High LOD
    private static LODLevel _lodLevel = LODLevel.High;

    /// <summary>
    /// Event fired when any config value changes.
    /// </summary>
    public static event Action? ConfigChanged;

    /// <summary>
    /// Reset to defaults (High LOD).
    /// Called automatically at game start.
    /// </summary>
    public static void ResetToDefaults()
    {
        _lodLevel = LODLevel.High;
        ConfigChanged?.Invoke();
        GD.Print("[GraphicsConfig] Reset to defaults");
    }

    /// <summary>
    /// Current LOD level for monster mesh generation.
    /// </summary>
    public static LODLevel CurrentLODLevel
    {
        get => _lodLevel;
        set
        {
            if (_lodLevel != value)
            {
                _lodLevel = value;
                GD.Print($"[GraphicsConfig] LOD Level changed to: {value}");
                ConfigChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Get LOD level index (0=High, 1=Medium, 2=Low) for UI controls.
    /// </summary>
    public static int GetLODIndex()
    {
        return _lodLevel switch
        {
            LODLevel.High => 0,
            LODLevel.Medium => 1,
            LODLevel.Low => 2,
            _ => 0
        };
    }

    /// <summary>
    /// Set LOD level from index (0=High, 1=Medium, 2=Low).
    /// </summary>
    public static void SetLODFromIndex(int index)
    {
        CurrentLODLevel = index switch
        {
            0 => LODLevel.High,
            1 => LODLevel.Medium,
            2 => LODLevel.Low,
            _ => LODLevel.High
        };
    }

    /// <summary>
    /// Get display name for each LOD level.
    /// </summary>
    public static string GetLODDisplayName(LODLevel level)
    {
        return level switch
        {
            LODLevel.High => "High (32 segments)",
            LODLevel.Medium => "Medium (16 segments)",
            LODLevel.Low => "Low (8 segments)",
            _ => "High"
        };
    }
}
