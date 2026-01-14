using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeRoom3D.Core;

/// <summary>
/// Interface for objects that can be saved and loaded.
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// Unique identifier for this saveable object.
    /// </summary>
    string SaveId { get; }

    /// <summary>
    /// Collect data to be saved.
    /// </summary>
    Dictionary<string, object> Save();

    /// <summary>
    /// Restore data from a saved state.
    /// </summary>
    void Load(Dictionary<string, object> data);
}

/// <summary>
/// Manages saving and loading game state.
/// </summary>
public class SaveManager
{
    private static SaveManager? _instance;
    public static SaveManager Instance => _instance ??= new SaveManager();

    private readonly List<ISaveable> _saveables = new();
    private const string SaveFileName = "user://savegame.json";
    private const string AutosaveFileName = "user://autosave.json";

    /// <summary>
    /// Register a saveable object with the save system.
    /// </summary>
    public void Register(ISaveable saveable)
    {
        if (!_saveables.Contains(saveable))
        {
            _saveables.Add(saveable);
            GD.Print($"[SaveManager] Registered: {saveable.SaveId}");
        }
    }

    /// <summary>
    /// Unregister a saveable object.
    /// </summary>
    public void Unregister(ISaveable saveable)
    {
        _saveables.Remove(saveable);
    }

    /// <summary>
    /// Save all registered objects to file.
    /// </summary>
    public bool SaveGame(string? customPath = null)
    {
        try
        {
            var saveData = new SaveData
            {
                Version = 1,
                Timestamp = DateTime.Now.ToString("o"),
                GameData = new Dictionary<string, Dictionary<string, object>>()
            };

            foreach (var saveable in _saveables)
            {
                try
                {
                    var data = saveable.Save();
                    saveData.GameData[saveable.SaveId] = data;
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[SaveManager] Error saving {saveable.SaveId}: {ex.Message}");
                }
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new ObjectToInferredTypesConverter() }
            };

            string json = JsonSerializer.Serialize(saveData, options);
            string path = customPath ?? SaveFileName;

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"[SaveManager] Failed to open file for writing: {path}");
                return false;
            }

            file.StoreString(json);
            GD.Print($"[SaveManager] Game saved to {path}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveManager] Save failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load all registered objects from file.
    /// </summary>
    public bool LoadGame(string? customPath = null)
    {
        try
        {
            string path = customPath ?? SaveFileName;

            if (!FileAccess.FileExists(path))
            {
                GD.Print($"[SaveManager] No save file found at {path}");
                return false;
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[SaveManager] Failed to open file for reading: {path}");
                return false;
            }

            string json = file.GetAsText();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new ObjectToInferredTypesConverter() }
            };

            var saveData = JsonSerializer.Deserialize<SaveData>(json, options);
            if (saveData?.GameData == null)
            {
                GD.PrintErr("[SaveManager] Invalid save data format");
                return false;
            }

            foreach (var saveable in _saveables)
            {
                if (saveData.GameData.TryGetValue(saveable.SaveId, out var data))
                {
                    try
                    {
                        saveable.Load(data);
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[SaveManager] Error loading {saveable.SaveId}: {ex.Message}");
                    }
                }
            }

            GD.Print($"[SaveManager] Game loaded from {path} (saved at {saveData.Timestamp})");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveManager] Load failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Create an autosave.
    /// </summary>
    public bool Autosave()
    {
        return SaveGame(AutosaveFileName);
    }

    /// <summary>
    /// Load from autosave.
    /// </summary>
    public bool LoadAutosave()
    {
        return LoadGame(AutosaveFileName);
    }

    /// <summary>
    /// Check if a save file exists.
    /// </summary>
    public bool HasSaveGame(string? customPath = null)
    {
        return FileAccess.FileExists(customPath ?? SaveFileName);
    }

    /// <summary>
    /// Check if an autosave exists.
    /// </summary>
    public bool HasAutosave()
    {
        return FileAccess.FileExists(AutosaveFileName);
    }

    /// <summary>
    /// Delete the save file.
    /// </summary>
    public bool DeleteSave(string? customPath = null)
    {
        string path = customPath ?? SaveFileName;
        if (FileAccess.FileExists(path))
        {
            var err = DirAccess.RemoveAbsolute(path);
            return err == Error.Ok;
        }
        return false;
    }

    /// <summary>
    /// Get save file metadata without loading the full save.
    /// </summary>
    public SaveMetadata? GetSaveMetadata(string? customPath = null)
    {
        try
        {
            string path = customPath ?? SaveFileName;
            if (!FileAccess.FileExists(path)) return null;

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return null;

            string json = file.GetAsText();
            var saveData = JsonSerializer.Deserialize<SaveData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (saveData == null) return null;

            return new SaveMetadata
            {
                Version = saveData.Version,
                Timestamp = saveData.Timestamp,
                FilePath = path
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clear all registered saveables.
    /// </summary>
    public void ClearRegistrations()
    {
        _saveables.Clear();
    }
}

/// <summary>
/// Container for save file data.
/// </summary>
public class SaveData
{
    public int Version { get; set; }
    public string Timestamp { get; set; } = "";
    public Dictionary<string, Dictionary<string, object>> GameData { get; set; } = new();
}

/// <summary>
/// Metadata about a save file (for display without full load).
/// </summary>
public class SaveMetadata
{
    public int Version { get; set; }
    public string Timestamp { get; set; } = "";
    public string FilePath { get; set; } = "";
}

/// <summary>
/// JSON converter to handle dynamic object types in save data.
/// </summary>
public class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out long l))
                    return l;
                return reader.GetDouble();
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.StartArray:
                var list = new List<object?>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add(Read(ref reader, typeof(object), options));
                }
                return list;
            case JsonTokenType.StartObject:
                var dict = new Dictionary<string, object>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string? key = reader.GetString();
                        reader.Read();
                        if (key != null)
                        {
                            dict[key] = Read(ref reader, typeof(object), options)!;
                        }
                    }
                }
                return dict;
            case JsonTokenType.Null:
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);
    }
}

/// <summary>
/// Helper class for common save/load operations on Godot types.
/// </summary>
public static class SaveHelpers
{
    public static Dictionary<string, object> SerializeVector3(Vector3 v)
    {
        return new Dictionary<string, object>
        {
            ["x"] = v.X,
            ["y"] = v.Y,
            ["z"] = v.Z
        };
    }

    public static Vector3 DeserializeVector3(Dictionary<string, object> data)
    {
        return new Vector3(
            Convert.ToSingle(data.GetValueOrDefault("x", 0f)),
            Convert.ToSingle(data.GetValueOrDefault("y", 0f)),
            Convert.ToSingle(data.GetValueOrDefault("z", 0f))
        );
    }

    public static Dictionary<string, object> SerializeVector2(Vector2 v)
    {
        return new Dictionary<string, object>
        {
            ["x"] = v.X,
            ["y"] = v.Y
        };
    }

    public static Vector2 DeserializeVector2(Dictionary<string, object> data)
    {
        return new Vector2(
            Convert.ToSingle(data.GetValueOrDefault("x", 0f)),
            Convert.ToSingle(data.GetValueOrDefault("y", 0f))
        );
    }

    public static Dictionary<string, object> SerializeColor(Color c)
    {
        return new Dictionary<string, object>
        {
            ["r"] = c.R,
            ["g"] = c.G,
            ["b"] = c.B,
            ["a"] = c.A
        };
    }

    public static Color DeserializeColor(Dictionary<string, object> data)
    {
        return new Color(
            Convert.ToSingle(data.GetValueOrDefault("r", 1f)),
            Convert.ToSingle(data.GetValueOrDefault("g", 1f)),
            Convert.ToSingle(data.GetValueOrDefault("b", 1f)),
            Convert.ToSingle(data.GetValueOrDefault("a", 1f))
        );
    }
}
