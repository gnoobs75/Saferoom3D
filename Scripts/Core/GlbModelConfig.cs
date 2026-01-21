using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeRoom3D.Core;

/// <summary>
/// Central configuration for GLB model paths per monster/prop type.
/// Allows switching between procedural meshes and custom GLB models on a per-type basis.
/// </summary>
public static class GlbModelConfig
{
    private const string ConfigPath = "user://glb_config.json";

    /// <summary>
    /// GLB paths for monster types. Key = monster type (lowercase), Value = GLB path.
    /// </summary>
    public static Dictionary<string, string> MonsterGlbPaths { get; private set; } = new();

    /// <summary>
    /// GLB paths for prop types. Key = prop type (lowercase), Value = GLB path.
    /// </summary>
    public static Dictionary<string, string> PropGlbPaths { get; private set; } = new();

    /// <summary>
    /// GLB paths for NPC types. Key = NPC type (lowercase), Value = GLB path.
    /// </summary>
    public static Dictionary<string, string> NpcGlbPaths { get; private set; } = new();

    /// <summary>
    /// Y offset for monster GLB models. Key = monster type (lowercase), Value = Y offset.
    /// Use this to adjust models whose origin is not at their feet.
    /// </summary>
    public static Dictionary<string, float> MonsterGlbOffsets { get; private set; } = new();

    /// <summary>
    /// Y offset for prop GLB models. Key = prop type (lowercase), Value = Y offset.
    /// </summary>
    public static Dictionary<string, float> PropGlbOffsets { get; private set; } = new();

    /// <summary>
    /// Y offset for NPC GLB models. Key = NPC type (lowercase), Value = Y offset.
    /// </summary>
    public static Dictionary<string, float> NpcGlbOffsets { get; private set; } = new();

    private static bool _initialized;

    /// <summary>
    /// Initialize the config system. Call once at game startup.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        LoadConfig();
        _initialized = true;
    }

    /// <summary>
    /// Try to get GLB path for a monster type.
    /// </summary>
    public static bool TryGetMonsterGlbPath(string monsterType, out string? path)
    {
        if (!_initialized) Initialize();
        return MonsterGlbPaths.TryGetValue(monsterType.ToLower(), out path);
    }

    /// <summary>
    /// Try to get GLB path for a prop type.
    /// </summary>
    public static bool TryGetPropGlbPath(string propType, out string? path)
    {
        if (!_initialized) Initialize();
        return PropGlbPaths.TryGetValue(propType.ToLower(), out path);
    }

    /// <summary>
    /// Try to get GLB path for an NPC type.
    /// </summary>
    public static bool TryGetNpcGlbPath(string npcType, out string? path)
    {
        if (!_initialized) Initialize();
        return NpcGlbPaths.TryGetValue(npcType.ToLower(), out path);
    }

    /// <summary>
    /// Check if a monster type has a GLB model configured.
    /// </summary>
    public static bool HasMonsterGlb(string monsterType)
    {
        if (!_initialized) Initialize();
        return MonsterGlbPaths.ContainsKey(monsterType.ToLower());
    }

    /// <summary>
    /// Check if a prop type has a GLB model configured.
    /// </summary>
    public static bool HasPropGlb(string propType)
    {
        if (!_initialized) Initialize();
        return PropGlbPaths.ContainsKey(propType.ToLower());
    }

    /// <summary>
    /// Check if an NPC type has a GLB model configured.
    /// </summary>
    public static bool HasNpcGlb(string npcType)
    {
        if (!_initialized) Initialize();
        return NpcGlbPaths.ContainsKey(npcType.ToLower());
    }

    /// <summary>
    /// Get the GLB path for a monster type, or null if not set.
    /// </summary>
    public static string? GetMonsterGlbPath(string monsterType)
    {
        if (!_initialized) Initialize();
        return MonsterGlbPaths.GetValueOrDefault(monsterType.ToLower());
    }

    /// <summary>
    /// Get the GLB path for a prop type, or null if not set.
    /// </summary>
    public static string? GetPropGlbPath(string propType)
    {
        if (!_initialized) Initialize();
        return PropGlbPaths.GetValueOrDefault(propType.ToLower());
    }

    /// <summary>
    /// Get the GLB path for an NPC type, or null if not set.
    /// </summary>
    public static string? GetNpcGlbPath(string npcType)
    {
        if (!_initialized) Initialize();
        return NpcGlbPaths.GetValueOrDefault(npcType.ToLower());
    }

    /// <summary>
    /// Set GLB path for a monster type. Pass empty string to clear.
    /// </summary>
    public static void SetMonsterGlbPath(string monsterType, string path)
    {
        if (!_initialized) Initialize();
        string key = monsterType.ToLower();

        if (string.IsNullOrWhiteSpace(path))
        {
            MonsterGlbPaths.Remove(key);
            GD.Print($"[GlbModelConfig] Cleared GLB path for monster: {key}");
        }
        else
        {
            MonsterGlbPaths[key] = path;
            GD.Print($"[GlbModelConfig] Set monster GLB: {key} -> {path}");
        }

        SaveConfig();
    }

    /// <summary>
    /// Set GLB path for a prop type. Pass empty string to clear.
    /// </summary>
    public static void SetPropGlbPath(string propType, string path)
    {
        if (!_initialized) Initialize();
        string key = propType.ToLower();

        if (string.IsNullOrWhiteSpace(path))
        {
            PropGlbPaths.Remove(key);
            GD.Print($"[GlbModelConfig] Cleared GLB path for prop: {key}");
        }
        else
        {
            PropGlbPaths[key] = path;
            GD.Print($"[GlbModelConfig] Set prop GLB: {key} -> {path}");
        }

        SaveConfig();
    }

    /// <summary>
    /// Clear GLB path for a monster type (revert to procedural).
    /// </summary>
    public static void ClearMonsterGlbPath(string monsterType)
    {
        SetMonsterGlbPath(monsterType, "");
    }

    /// <summary>
    /// Clear GLB path for a prop type (revert to procedural).
    /// </summary>
    public static void ClearPropGlbPath(string propType)
    {
        SetPropGlbPath(propType, "");
    }

    /// <summary>
    /// Set GLB path for an NPC type. Pass empty string to clear.
    /// </summary>
    public static void SetNpcGlbPath(string npcType, string path)
    {
        if (!_initialized) Initialize();
        string key = npcType.ToLower();

        if (string.IsNullOrWhiteSpace(path))
        {
            NpcGlbPaths.Remove(key);
            GD.Print($"[GlbModelConfig] Cleared GLB path for NPC: {key}");
        }
        else
        {
            NpcGlbPaths[key] = path;
            GD.Print($"[GlbModelConfig] Set NPC GLB: {key} -> {path}");
        }

        SaveConfig();
    }

    /// <summary>
    /// Clear GLB path for an NPC type (revert to procedural).
    /// </summary>
    public static void ClearNpcGlbPath(string npcType)
    {
        SetNpcGlbPath(npcType, "");
    }

    /// <summary>
    /// Get Y offset for a monster type (default 0).
    /// </summary>
    public static float GetMonsterYOffset(string monsterType)
    {
        if (!_initialized) Initialize();
        return MonsterGlbOffsets.GetValueOrDefault(monsterType.ToLower(), 0f);
    }

    /// <summary>
    /// Get Y offset for a prop type (default 0).
    /// </summary>
    public static float GetPropYOffset(string propType)
    {
        if (!_initialized) Initialize();
        return PropGlbOffsets.GetValueOrDefault(propType.ToLower(), 0f);
    }

    /// <summary>
    /// Get Y offset for an NPC type (default 0).
    /// </summary>
    public static float GetNpcYOffset(string npcType)
    {
        if (!_initialized) Initialize();
        return NpcGlbOffsets.GetValueOrDefault(npcType.ToLower(), 0f);
    }

    /// <summary>
    /// Set Y offset for a monster GLB model.
    /// </summary>
    public static void SetMonsterYOffset(string monsterType, float offset)
    {
        if (!_initialized) Initialize();
        string key = monsterType.ToLower();

        if (offset == 0f)
        {
            MonsterGlbOffsets.Remove(key);
        }
        else
        {
            MonsterGlbOffsets[key] = offset;
        }

        SaveConfig();
    }

    /// <summary>
    /// Set Y offset for a prop GLB model.
    /// </summary>
    public static void SetPropYOffset(string propType, float offset)
    {
        if (!_initialized) Initialize();
        string key = propType.ToLower();

        if (offset == 0f)
        {
            PropGlbOffsets.Remove(key);
        }
        else
        {
            PropGlbOffsets[key] = offset;
        }

        SaveConfig();
    }

    /// <summary>
    /// Set Y offset for an NPC GLB model.
    /// </summary>
    public static void SetNpcYOffset(string npcType, float offset)
    {
        if (!_initialized) Initialize();
        string key = npcType.ToLower();

        if (offset == 0f)
        {
            NpcGlbOffsets.Remove(key);
        }
        else
        {
            NpcGlbOffsets[key] = offset;
        }

        SaveConfig();
    }

    /// <summary>
    /// Save config to user://glb_config.json
    /// </summary>
    public static void SaveConfig()
    {
        try
        {
            var configData = new GlbConfigData
            {
                Monsters = MonsterGlbPaths,
                Props = PropGlbPaths,
                Npcs = NpcGlbPaths,
                MonsterOffsets = MonsterGlbOffsets,
                PropOffsets = PropGlbOffsets,
                NpcOffsets = NpcGlbOffsets
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string json = JsonSerializer.Serialize(configData, options);

            using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
                GD.Print($"[GlbModelConfig] Saved config: {MonsterGlbPaths.Count} monsters, {PropGlbPaths.Count} props, {NpcGlbPaths.Count} NPCs");
            }
            else
            {
                GD.PrintErr($"[GlbModelConfig] Failed to open {ConfigPath} for writing");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[GlbModelConfig] Error saving config: {ex.Message}");
        }
    }

    /// <summary>
    /// Load config from user://glb_config.json
    /// </summary>
    public static void LoadConfig()
    {
        try
        {
            if (!FileAccess.FileExists(ConfigPath))
            {
                GD.Print("[GlbModelConfig] No config file found, using defaults");
                return;
            }

            using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[GlbModelConfig] Failed to open {ConfigPath} for reading");
                return;
            }

            string json = file.GetAsText();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var configData = JsonSerializer.Deserialize<GlbConfigData>(json, options);
            if (configData != null)
            {
                MonsterGlbPaths = configData.Monsters ?? new Dictionary<string, string>();
                PropGlbPaths = configData.Props ?? new Dictionary<string, string>();
                NpcGlbPaths = configData.Npcs ?? new Dictionary<string, string>();
                MonsterGlbOffsets = configData.MonsterOffsets ?? new Dictionary<string, float>();
                PropGlbOffsets = configData.PropOffsets ?? new Dictionary<string, float>();
                NpcGlbOffsets = configData.NpcOffsets ?? new Dictionary<string, float>();
                GD.Print($"[GlbModelConfig] Loaded config: {MonsterGlbPaths.Count} monsters, {PropGlbPaths.Count} props, {NpcGlbPaths.Count} NPCs");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[GlbModelConfig] Error loading config: {ex.Message}");
            MonsterGlbPaths = new Dictionary<string, string>();
            PropGlbPaths = new Dictionary<string, string>();
            NpcGlbPaths = new Dictionary<string, string>();
            MonsterGlbOffsets = new Dictionary<string, float>();
            PropGlbOffsets = new Dictionary<string, float>();
            NpcGlbOffsets = new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Check if a GLB file exists at the given path.
    /// Handles res://, user://, and filesystem paths.
    /// </summary>
    public static bool GlbFileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // Handle Godot resource paths
        if (path.StartsWith("res://") || path.StartsWith("user://"))
        {
            return ResourceLoader.Exists(path);
        }

        // Handle filesystem paths
        return System.IO.File.Exists(path);
    }

    /// <summary>
    /// Find AnimationPlayer in a loaded GLB model hierarchy.
    /// Searches direct children first, then recursively.
    /// </summary>
    public static AnimationPlayer? FindAnimationPlayer(Node3D? glbRoot)
    {
        if (glbRoot == null) return null;

        // Try direct child first (common export pattern)
        var direct = glbRoot.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (direct != null) return direct;

        // Search recursively
        return glbRoot.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
    }

    /// <summary>
    /// Load a GLB model as a Node3D. Returns null if loading fails.
    /// </summary>
    public static Node3D? LoadGlbModel(string path, float scale = 1f)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            GD.PrintErr("[GlbModelConfig] Cannot load GLB: empty path");
            return null;
        }

        try
        {
            // Ensure path uses res:// format for Godot loading
            string resourcePath = path;
            if (!path.StartsWith("res://") && !path.StartsWith("user://"))
            {
                // Convert relative path to res:// format
                resourcePath = "res://" + path.Replace("\\", "/");
            }

            if (!ResourceLoader.Exists(resourcePath))
            {
                GD.PrintErr($"[GlbModelConfig] GLB file not found: {resourcePath}");
                return null;
            }

            var scene = GD.Load<PackedScene>(resourcePath);
            if (scene == null)
            {
                GD.PrintErr($"[GlbModelConfig] Failed to load GLB as PackedScene: {resourcePath}");
                return null;
            }

            var instance = scene.Instantiate<Node3D>();
            if (instance != null && scale != 1f)
            {
                instance.Scale = new Vector3(scale, scale, scale);
            }

            GD.Print($"[GlbModelConfig] Loaded GLB model: {resourcePath}");
            return instance;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[GlbModelConfig] Error loading GLB {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Data structure for JSON serialization.
    /// </summary>
    private class GlbConfigData
    {
        [JsonPropertyName("monsters")]
        public Dictionary<string, string>? Monsters { get; set; }

        [JsonPropertyName("props")]
        public Dictionary<string, string>? Props { get; set; }

        [JsonPropertyName("npcs")]
        public Dictionary<string, string>? Npcs { get; set; }

        [JsonPropertyName("monsterOffsets")]
        public Dictionary<string, float>? MonsterOffsets { get; set; }

        [JsonPropertyName("propOffsets")]
        public Dictionary<string, float>? PropOffsets { get; set; }

        [JsonPropertyName("npcOffsets")]
        public Dictionary<string, float>? NpcOffsets { get; set; }
    }
}
