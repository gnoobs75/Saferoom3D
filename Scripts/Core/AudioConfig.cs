using Godot;
using System;

namespace SafeRoom3D.Core;

/// <summary>
/// Manages audio settings for the current session.
/// Settings reset to defaults (enabled) each time the game starts.
/// Does NOT persist between sessions.
/// </summary>
public static class AudioConfig
{
    // Default state: both enabled
    private static bool _musicEnabled = true;
    private static bool _soundEnabled = true;
    private static float _musicVolume = 0.7f;
    private static float _soundVolume = 1.0f;

    /// <summary>
    /// Event fired when any config value changes.
    /// </summary>
    public static event Action? ConfigChanged;

    /// <summary>
    /// Reset to defaults (music and sound enabled).
    /// Called automatically at game start.
    /// </summary>
    public static void ResetToDefaults()
    {
        _musicEnabled = true;
        _soundEnabled = true;
        _musicVolume = 0.7f;
        _soundVolume = 1.0f;
        ConfigChanged?.Invoke();
        GD.Print("[AudioConfig] Reset to defaults");
    }

    public static bool IsMusicEnabled
    {
        get => _musicEnabled;
        set
        {
            if (_musicEnabled != value)
            {
                _musicEnabled = value;
                GD.Print($"[AudioConfig] Music {(value ? "enabled" : "disabled")}");
                ConfigChanged?.Invoke();
            }
        }
    }

    public static bool IsSoundEnabled
    {
        get => _soundEnabled;
        set
        {
            if (_soundEnabled != value)
            {
                _soundEnabled = value;
                GD.Print($"[AudioConfig] Sound {(value ? "enabled" : "disabled")}");
                ConfigChanged?.Invoke();
            }
        }
    }

    public static float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Mathf.Clamp(value, 0f, 1f);
            ConfigChanged?.Invoke();
        }
    }

    public static float SoundVolume
    {
        get => _soundVolume;
        set
        {
            _soundVolume = Mathf.Clamp(value, 0f, 1f);
            ConfigChanged?.Invoke();
        }
    }

    public static void ToggleMusic()
    {
        IsMusicEnabled = !IsMusicEnabled;
    }

    public static void ToggleSound()
    {
        IsSoundEnabled = !IsSoundEnabled;
    }
}
