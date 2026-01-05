using Godot;
using System;

namespace SafeRoom3D.Core;

/// <summary>
/// Manages gameplay settings like game speed.
/// Settings reset to defaults each time the game starts.
/// </summary>
public static class GameConfig
{
    // Default: 75% speed for more cinematic combat
    private static float _gameSpeed = 0.75f;

    /// <summary>
    /// Event fired when any config value changes.
    /// </summary>
    public static event Action? ConfigChanged;

    /// <summary>
    /// Reset to defaults.
    /// </summary>
    public static void ResetToDefaults()
    {
        _gameSpeed = 0.75f;
        ConfigChanged?.Invoke();
        GD.Print("[GameConfig] Reset to defaults (speed: 75%)");
    }

    /// <summary>
    /// Game speed multiplier (0.5 to 1.25).
    /// Affects physics, animations, and gameplay timing.
    /// </summary>
    public static float GameSpeed
    {
        get => _gameSpeed;
        set
        {
            float newSpeed = Mathf.Clamp(value, 0.5f, 1.25f);
            if (Mathf.Abs(_gameSpeed - newSpeed) > 0.01f)
            {
                _gameSpeed = newSpeed;
                GD.Print($"[GameConfig] Game speed set to {(int)(_gameSpeed * 100)}%");

                // Apply immediately if game is running (not paused/frozen)
                if (Engine.TimeScale > 0.01f)
                {
                    Engine.TimeScale = _gameSpeed;
                }

                ConfigChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Game speed as percentage (50-125).
    /// </summary>
    public static int GameSpeedPercent
    {
        get => (int)(_gameSpeed * 100);
        set => GameSpeed = value / 100f;
    }

    /// <summary>
    /// Resume game to normal configured speed.
    /// Call this instead of Engine.TimeScale = 1.0 when unpausing.
    /// </summary>
    public static void ResumeToNormalSpeed()
    {
        Engine.TimeScale = _gameSpeed;
        GD.Print($"[GameConfig] Resumed to {GameSpeedPercent}% speed");
    }

    /// <summary>
    /// Check if current TimeScale matches configured speed.
    /// </summary>
    public static bool IsAtNormalSpeed => Mathf.Abs(Engine.TimeScale - _gameSpeed) < 0.01f;
}
