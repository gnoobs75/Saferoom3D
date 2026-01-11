using Godot;
using System;
using System.Text.Json;

namespace SafeRoom3D.NPC.AI;

/// <summary>
/// Global configuration for Crawler NPC AI system.
/// Provides toggle to enable/disable AI and logging settings.
/// Follows AudioConfig pattern for consistency.
/// </summary>
public static class CrawlerAIConfig
{
    // === Global Toggle ===
    private static bool _aiEnabled = true;
    private static bool _debugLoggingEnabled = true;

    // === Performance Tuning ===
    private static float _decisionInterval = 0.5f;  // Seconds between utility decisions
    private static float _logInterval = 2.0f;       // Seconds between log entries (throttle)

    /// <summary>
    /// Event fired when any config value changes.
    /// </summary>
    public static event Action? ConfigChanged;

    private const string ConfigPath = "user://crawler_ai_config.json";

    /// <summary>
    /// Master toggle for all Crawler AI behavior.
    /// When disabled, Crawlers revert to idle animation only.
    /// </summary>
    public static bool AIEnabled
    {
        get => _aiEnabled;
        set
        {
            if (_aiEnabled != value)
            {
                _aiEnabled = value;
                GD.Print($"[CrawlerAI] AI {(value ? "ENABLED" : "DISABLED")}");
                ConfigChanged?.Invoke();
                Save();
            }
        }
    }

    /// <summary>
    /// Toggle for decision logging in console and combat log.
    /// </summary>
    public static bool DebugLoggingEnabled
    {
        get => _debugLoggingEnabled;
        set
        {
            if (_debugLoggingEnabled != value)
            {
                _debugLoggingEnabled = value;
                GD.Print($"[CrawlerAI] Debug logging {(value ? "enabled" : "disabled")}");
                ConfigChanged?.Invoke();
                Save();
            }
        }
    }

    /// <summary>
    /// How often Crawlers evaluate their utility scores (seconds).
    /// Lower = more responsive but more CPU. Default 0.5s.
    /// </summary>
    public static float DecisionInterval
    {
        get => _decisionInterval;
        set
        {
            _decisionInterval = Mathf.Clamp(value, 0.1f, 2.0f);
            ConfigChanged?.Invoke();
        }
    }

    /// <summary>
    /// Minimum time between log entries per Crawler (seconds).
    /// Prevents log spam. Default 2.0s.
    /// </summary>
    public static float LogInterval
    {
        get => _logInterval;
        set
        {
            _logInterval = Mathf.Clamp(value, 0.5f, 10.0f);
            ConfigChanged?.Invoke();
        }
    }

    /// <summary>
    /// Toggle AI on/off.
    /// </summary>
    public static void ToggleAI()
    {
        AIEnabled = !AIEnabled;
    }

    /// <summary>
    /// Reset to default values.
    /// </summary>
    public static void ResetToDefaults()
    {
        _aiEnabled = true;
        _debugLoggingEnabled = true;
        _decisionInterval = 0.5f;
        _logInterval = 2.0f;
        ConfigChanged?.Invoke();
        GD.Print("[CrawlerAI] Config reset to defaults");
    }

    /// <summary>
    /// Save config to user:// directory.
    /// </summary>
    public static void Save()
    {
        try
        {
            var config = new CrawlerAIConfigData
            {
                AIEnabled = _aiEnabled,
                DebugLoggingEnabled = _debugLoggingEnabled,
                DecisionInterval = _decisionInterval,
                LogInterval = _logInterval
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
                GD.Print("[CrawlerAI] Config saved");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CrawlerAI] Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Load config from user:// directory.
    /// </summary>
    public static void Load()
    {
        try
        {
            if (!FileAccess.FileExists(ConfigPath))
            {
                GD.Print("[CrawlerAI] No config file found, using defaults");
                return;
            }

            using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
            if (file != null)
            {
                string json = file.GetAsText();
                var config = JsonSerializer.Deserialize<CrawlerAIConfigData>(json);
                if (config != null)
                {
                    _aiEnabled = config.AIEnabled;
                    _debugLoggingEnabled = config.DebugLoggingEnabled;
                    _decisionInterval = config.DecisionInterval;
                    _logInterval = config.LogInterval;
                    GD.Print($"[CrawlerAI] Config loaded (AI={_aiEnabled}, Logging={_debugLoggingEnabled})");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CrawlerAI] Failed to load config: {ex.Message}");
            ResetToDefaults();
        }
    }

    /// <summary>
    /// Internal data class for JSON serialization.
    /// </summary>
    private class CrawlerAIConfigData
    {
        public bool AIEnabled { get; set; } = true;
        public bool DebugLoggingEnabled { get; set; } = true;
        public float DecisionInterval { get; set; } = 0.5f;
        public float LogInterval { get; set; } = 2.0f;
    }
}
