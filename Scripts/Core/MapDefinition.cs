using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeRoom3D.Core;

/// <summary>
/// Data classes for custom map definitions. Maps can be loaded from JSON files
/// created by Claude from dungeon images, or manually edited by users.
/// </summary>

/// <summary>
/// Root container for a complete map definition.
/// </summary>
public class MapDefinition
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Custom Dungeon";

    /// <summary>
    /// Floor level of this dungeon (1-based). Affects quest difficulty and rewards.
    /// If not specified, attempts to infer from map name (e.g., "Floor 2", "level_3").
    /// </summary>
    [JsonPropertyName("floorLevel")]
    public int FloorLevel { get; set; } = 1;

    /// <summary>
    /// Map mode: "rooms" for traditional room-based maps, "tiles" for WYSIWYG tile-painted maps.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "rooms";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 80;

    [JsonPropertyName("depth")]
    public int Depth { get; set; } = 80;

    [JsonPropertyName("spawnRoom")]
    public int SpawnRoom { get; set; } = 0;

    /// <summary>
    /// Spawn position for tile-mode maps. Ignored for room-mode maps.
    /// </summary>
    [JsonPropertyName("spawnPosition")]
    public PositionData? SpawnPosition { get; set; }

    /// <summary>
    /// RLE+Base64 encoded tile data for tile-mode maps.
    /// Each tile is 0 (void) or 1 (floor). Ignored for room-mode maps.
    /// </summary>
    [JsonPropertyName("tileData")]
    public string? TileData { get; set; }

    [JsonPropertyName("rooms")]
    public List<RoomDefinition> Rooms { get; set; } = new();

    [JsonPropertyName("corridors")]
    public List<CorridorDefinition> Corridors { get; set; } = new();

    [JsonPropertyName("enemies")]
    public List<EnemyPlacement> Enemies { get; set; } = new();

    [JsonPropertyName("monsterGroups")]
    public List<MonsterGroupPlacement> MonsterGroups { get; set; } = new();

    [JsonPropertyName("groupTemplates")]
    public List<MonsterGroupTemplate> GroupTemplates { get; set; } = new();

    /// <summary>
    /// Custom tile overrides. Each entry specifies a tile position and whether it's floor (1) or void (0).
    /// This allows manually editing tiles that differ from the room/corridor-generated layout.
    /// Used in room-mode for manual edits. In tile-mode, use TileData instead.
    /// </summary>
    [JsonPropertyName("customTiles")]
    public List<TileOverride> CustomTiles { get; set; } = new();

    /// <summary>
    /// Player-placed props from In-Map Edit Mode.
    /// These are placed in first-person view and saved with the map.
    /// </summary>
    [JsonPropertyName("placedProps")]
    public List<PropPlacement> PlacedProps { get; set; } = new();

    /// <summary>
    /// NPC placements (shopkeepers and other non-combat characters).
    /// </summary>
    [JsonPropertyName("npcs")]
    public List<NpcPlacement> Npcs { get; set; } = new();

    /// <summary>
    /// Whether to use procedural prop generation for rooms.
    /// If false, only player-placed props are used.
    /// </summary>
    [JsonPropertyName("useProceduralProps")]
    public bool UseProceduralProps { get; set; } = true;

    /// <summary>
    /// Returns true if this is a tile-mode map (WYSIWYG painted).
    /// </summary>
    [JsonIgnore]
    public bool IsTileMode => Mode == "tiles";

    /// <summary>
    /// Gets the effective floor level, inferring from map name if FloorLevel is default (1).
    /// </summary>
    [JsonIgnore]
    public int EffectiveFloorLevel
    {
        get
        {
            // If explicitly set to something other than 1, use that
            if (FloorLevel > 1) return FloorLevel;

            // Try to infer from map name
            return InferFloorLevelFromName(Name);
        }
    }

    /// <summary>
    /// Infers floor level from map name patterns like "Floor 2", "level_3", "dungeon-4", etc.
    /// </summary>
    private static int InferFloorLevelFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 1;

        var lowerName = name.ToLower();

        // Try patterns like "floor 2", "floor_3", "floor-4"
        var patterns = new[]
        {
            @"floor[\s_-]?(\d+)",
            @"level[\s_-]?(\d+)",
            @"dungeon[\s_-]?(\d+)",
            @"f(\d+)",
            @"l(\d+)"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lowerName, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int level))
            {
                return Math.Max(1, Math.Min(level, 10)); // Clamp to 1-10
            }
        }

        return 1;
    }

    /// <summary>
    /// Loads a map definition from a JSON file.
    /// </summary>
    public static MapDefinition? LoadFromJson(string path)
    {
        try
        {
            string jsonContent;

            // Handle both user:// and res:// paths
            if (path.StartsWith("user://") || path.StartsWith("res://"))
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr($"[MapDefinition] Failed to open file: {path}");
                    return null;
                }
                jsonContent = file.GetAsText();
            }
            else
            {
                // Assume it's a system path
                if (!System.IO.File.Exists(path))
                {
                    GD.PrintErr($"[MapDefinition] File not found: {path}");
                    return null;
                }
                jsonContent = System.IO.File.ReadAllText(path);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var map = JsonSerializer.Deserialize<MapDefinition>(jsonContent, options);
            if (map != null)
            {
                GD.Print($"[MapDefinition] Loaded map '{map.Name}' with {map.Rooms.Count} rooms, {map.Enemies.Count} enemies");
            }
            return map;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MapDefinition] Error loading map from {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the project's maps folder path (actual filesystem path).
    /// </summary>
    public static string GetProjectMapsPath()
    {
        // Use ProjectSettings to get the actual filesystem path for res://maps
        string resPath = ProjectSettings.GlobalizePath("res://maps");
        GD.Print($"[MapDefinition] Project maps path: {resPath}");
        return resPath;
    }

    /// <summary>
    /// Saves this map definition to a JSON file in the project's maps folder.
    /// </summary>
    public bool SaveToJson(string path)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string jsonContent = JsonSerializer.Serialize(this, options);
            GD.Print($"[MapDefinition] Serialized map '{Name}', JSON length: {jsonContent.Length}");

            string fullPath;

            // Convert res:// or user:// paths to actual filesystem paths
            if (path.StartsWith("res://"))
            {
                fullPath = ProjectSettings.GlobalizePath(path);
            }
            else if (path.StartsWith("user://"))
            {
                // Redirect user:// to project maps folder
                string fileName = System.IO.Path.GetFileName(path.Replace("user://maps/", ""));
                fullPath = System.IO.Path.Combine(GetProjectMapsPath(), fileName);
            }
            else
            {
                fullPath = path;
            }

            string directory = System.IO.Path.GetDirectoryName(fullPath)!;
            GD.Print($"[MapDefinition] Full path: {fullPath}");

            // Ensure directory exists
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                GD.Print($"[MapDefinition] Created directory: {directory}");
            }

            // Write using .NET file I/O
            System.IO.File.WriteAllText(fullPath, jsonContent);
            GD.Print($"[MapDefinition] File written: {fullPath}");
            GD.Print($"[MapDefinition] Saved map '{Name}' to {fullPath}");
            return true;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MapDefinition] Error saving map to {path}: {ex.Message}");
            GD.PrintErr($"[MapDefinition] Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a map file from disk.
    /// </summary>
    public static bool DeleteMap(string path)
    {
        try
        {
            string fullPath;

            if (path.StartsWith("res://"))
            {
                fullPath = ProjectSettings.GlobalizePath(path);
            }
            else if (path.StartsWith("user://"))
            {
                // Redirect to project maps folder
                string fileName = System.IO.Path.GetFileName(path.Replace("user://maps/", ""));
                fullPath = System.IO.Path.Combine(GetProjectMapsPath(), fileName);
            }
            else
            {
                fullPath = path;
            }

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
                GD.Print($"[MapDefinition] Deleted map: {fullPath}");
                return true;
            }
            else
            {
                GD.PrintErr($"[MapDefinition] Map file not found: {fullPath}");
                return false;
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MapDefinition] Error deleting map {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a deep copy of this map definition.
    /// </summary>
    public MapDefinition Clone()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(this, options);
        return JsonSerializer.Deserialize<MapDefinition>(json, options) ?? new MapDefinition();
    }

    /// <summary>
    /// Gets a list of all saved maps from the project's maps folder.
    /// Returns tuples of (displayName, fullPath).
    /// </summary>
    public static List<(string Name, string Path)> GetSavedMapFilesWithPaths()
    {
        var maps = new List<(string Name, string Path)>();
        GD.Print("[MapDefinition] GetSavedMapFilesWithPaths() called");

        // Get the project's maps folder path
        string mapsPath = GetProjectMapsPath();
        GD.Print($"[MapDefinition] Scanning project maps folder: {mapsPath}");

        if (System.IO.Directory.Exists(mapsPath))
        {
            foreach (var filePath in System.IO.Directory.GetFiles(mapsPath, "*.json"))
            {
                string fileName = System.IO.Path.GetFileName(filePath);
                string displayName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                // Use res:// path for consistency
                string resPath = $"res://maps/{fileName}";
                GD.Print($"[MapDefinition] Found map: {displayName} -> {resPath}");
                maps.Add((displayName, resPath));
            }
        }
        else
        {
            GD.Print($"[MapDefinition] Maps folder does not exist: {mapsPath}");
            // Try to create it
            try
            {
                System.IO.Directory.CreateDirectory(mapsPath);
                GD.Print($"[MapDefinition] Created maps folder: {mapsPath}");
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[MapDefinition] Failed to create maps folder: {ex.Message}");
            }
        }

        GD.Print($"[MapDefinition] Total maps found: {maps.Count}");
        maps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return maps;
    }

    /// <summary>
    /// Gets a list of all saved map names (for backward compatibility).
    /// </summary>
    public static List<string> GetSavedMapFiles()
    {
        return GetSavedMapFilesWithPaths().Select(m => m.Name + ".json").ToList();
    }
}

/// <summary>
/// Defines a room's position, size, and type.
/// </summary>
public class RoomDefinition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("position")]
    public PositionData Position { get; set; } = new();

    [JsonPropertyName("size")]
    public SizeData Size { get; set; } = new();

    [JsonPropertyName("type")]
    public string Type { get; set; } = "storage";

    [JsonPropertyName("isSpawnRoom")]
    public bool IsSpawnRoom { get; set; } = false;

    /// <summary>
    /// Helper to get position as Vector3I (grid coordinates).
    /// </summary>
    [JsonIgnore]
    public Vector3I GridPosition => new Vector3I(Position.X, 0, Position.Z);

    /// <summary>
    /// Helper to get size as Vector3I.
    /// </summary>
    [JsonIgnore]
    public Vector3I GridSize => new Vector3I(Size.X, 1, Size.Z);
}

/// <summary>
/// 2D position data for JSON serialization.
/// </summary>
public class PositionData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("z")]
    public int Z { get; set; }
}

/// <summary>
/// 2D size data for JSON serialization.
/// </summary>
public class SizeData
{
    [JsonPropertyName("x")]
    public int X { get; set; } = 10;

    [JsonPropertyName("z")]
    public int Z { get; set; } = 10;
}

/// <summary>
/// Override for a single tile. Used to manually fix corridor/room tiles.
/// </summary>
public class TileOverride
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("z")]
    public int Z { get; set; }

    [JsonPropertyName("floor")]
    public bool IsFloor { get; set; }
}

/// <summary>
/// A prop placed by the player in In-Map Edit Mode.
/// </summary>
public class PropPlacement
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "barrel";

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }

    [JsonPropertyName("rotationY")]
    public float RotationY { get; set; }

    [JsonPropertyName("scale")]
    public float Scale { get; set; } = 1f;
}

/// <summary>
/// NPC placement (shopkeepers, quest givers, etc.)
/// </summary>
public class NpcPlacement
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "bopca";

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }

    [JsonPropertyName("rotationY")]
    public float RotationY { get; set; }

    /// <summary>
    /// Unique ID for this placement (used in editor).
    /// </summary>
    [JsonIgnore]
    public string PlacementId { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>
    /// Helper to get position as Vector3.
    /// </summary>
    [JsonIgnore]
    public Vector3 WorldPosition => new Vector3(X, 0, Z);
}

/// <summary>
/// Defines a corridor connecting two rooms.
/// </summary>
public class CorridorDefinition
{
    [JsonPropertyName("from")]
    public int FromRoom { get; set; }

    [JsonPropertyName("to")]
    public int ToRoom { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = 5;
}

/// <summary>
/// Placement of an individual enemy in the map.
/// </summary>
public class EnemyPlacement
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "goblin";

    [JsonPropertyName("roomId")]
    public int RoomId { get; set; } = -1;  // -1 = corridor/unassigned

    [JsonPropertyName("position")]
    public PositionData Position { get; set; } = new();

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("isBoss")]
    public bool IsBoss { get; set; } = false;

    [JsonPropertyName("rotationY")]
    public float RotationY { get; set; } = 0f;

    /// <summary>
    /// If true, this enemy patrols a much larger area (25 units vs 8).
    /// Roamers wander farther from their spawn point.
    /// </summary>
    [JsonPropertyName("isRoamer")]
    public bool IsRoamer { get; set; } = false;

    /// <summary>
    /// Unique ID for this placement (used in editor).
    /// </summary>
    [JsonIgnore]
    public string PlacementId { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>
    /// Helper to get position as Vector3.
    /// </summary>
    [JsonIgnore]
    public Vector3 WorldPosition => new Vector3(Position.X, 0, Position.Z);
}

/// <summary>
/// Placement of a monster group in the map.
/// </summary>
public class MonsterGroupPlacement
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("roomId")]
    public int RoomId { get; set; } = -1;

    [JsonPropertyName("center")]
    public PositionData Center { get; set; } = new();

    [JsonPropertyName("monsters")]
    public List<GroupMonster> Monsters { get; set; } = new();

    /// <summary>
    /// Helper to get center as Vector3.
    /// </summary>
    [JsonIgnore]
    public Vector3 WorldCenter => new Vector3(Center.X, 0, Center.Z);
}

/// <summary>
/// A monster within a group, with offset from group center.
/// </summary>
public class GroupMonster
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "goblin";

    [JsonPropertyName("offsetX")]
    public float OffsetX { get; set; }

    [JsonPropertyName("offsetZ")]
    public float OffsetZ { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("isBoss")]
    public bool IsBoss { get; set; } = false;
}

/// <summary>
/// A reusable template for monster groups.
/// </summary>
public class MonsterGroupTemplate
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("monsters")]
    public List<GroupMonster> Monsters { get; set; } = new();

    /// <summary>
    /// Creates a placement from this template at the given position.
    /// </summary>
    public MonsterGroupPlacement CreatePlacement(int x, int z, int roomId = -1)
    {
        return new MonsterGroupPlacement
        {
            Id = System.Guid.NewGuid().ToString(),
            Name = Name,
            RoomId = roomId,
            Center = new PositionData { X = x, Z = z },
            Monsters = Monsters.ConvertAll(m => new GroupMonster
            {
                Type = m.Type,
                OffsetX = m.OffsetX,
                OffsetZ = m.OffsetZ,
                Level = m.Level,
                IsBoss = m.IsBoss
            })
        };
    }
}

/// <summary>
/// Default monster group templates available in the editor.
/// </summary>
public static class DefaultGroupTemplates
{
    public static List<MonsterGroupTemplate> GetDefaultTemplates()
    {
        return new List<MonsterGroupTemplate>
        {
            new MonsterGroupTemplate
            {
                Name = "Goblin Patrol",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "goblin", OffsetX = -1.5f, OffsetZ = 0, Level = 1 },
                    new() { Type = "goblin", OffsetX = 1.5f, OffsetZ = 0, Level = 1 },
                    new() { Type = "goblin_shaman", OffsetX = 0, OffsetZ = 2, Level = 2 }
                }
            },
            new MonsterGroupTemplate
            {
                Name = "Skeleton Guard",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "skeleton", OffsetX = -2, OffsetZ = 0, Level = 2 },
                    new() { Type = "skeleton", OffsetX = 2, OffsetZ = 0, Level = 2 },
                    new() { Type = "skeleton_lord", OffsetX = 0, OffsetZ = 2, Level = 3, IsBoss = true }
                }
            },
            new MonsterGroupTemplate
            {
                Name = "Spider Nest",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "spider", OffsetX = -1, OffsetZ = -1, Level = 1 },
                    new() { Type = "spider", OffsetX = 1, OffsetZ = -1, Level = 1 },
                    new() { Type = "spider", OffsetX = -1, OffsetZ = 1, Level = 1 },
                    new() { Type = "spider", OffsetX = 1, OffsetZ = 1, Level = 1 },
                    new() { Type = "spider_queen", OffsetX = 0, OffsetZ = 0, Level = 3, IsBoss = true }
                }
            },
            new MonsterGroupTemplate
            {
                Name = "Dragon Lair",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "dragon_king", OffsetX = 0, OffsetZ = 0, Level = 5, IsBoss = true },
                    new() { Type = "bat", OffsetX = -3, OffsetZ = -2, Level = 2 },
                    new() { Type = "bat", OffsetX = 3, OffsetZ = -2, Level = 2 }
                }
            },
            new MonsterGroupTemplate
            {
                Name = "Slime Colony",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "slime", OffsetX = 0, OffsetZ = 0, Level = 1 },
                    new() { Type = "slime", OffsetX = -1.5f, OffsetZ = 1, Level = 1 },
                    new() { Type = "slime", OffsetX = 1.5f, OffsetZ = 1, Level = 1 },
                    new() { Type = "slime", OffsetX = 0, OffsetZ = 2, Level = 2 }
                }
            },
            new MonsterGroupTemplate
            {
                Name = "Goblin Thrower Squad",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "goblin_thrower", OffsetX = -2, OffsetZ = 0, Level = 2 },
                    new() { Type = "goblin_thrower", OffsetX = 2, OffsetZ = 0, Level = 2 },
                    new() { Type = "goblin", OffsetX = 0, OffsetZ = -2, Level = 1 }
                }
            },
            new MonsterGroupTemplate
            {
                Name = "Wolf Pack",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "wolf", OffsetX = 0, OffsetZ = 0, Level = 2 },
                    new() { Type = "wolf", OffsetX = -2, OffsetZ = 1, Level = 1 },
                    new() { Type = "wolf", OffsetX = 2, OffsetZ = 1, Level = 1 }
                }
            },
            new MonsterGroupTemplate
            {
                Name = "Mushroom Circle",
                Monsters = new List<GroupMonster>
                {
                    new() { Type = "mushroom", OffsetX = 0, OffsetZ = -1.5f, Level = 1 },
                    new() { Type = "mushroom", OffsetX = 1.3f, OffsetZ = 0.75f, Level = 1 },
                    new() { Type = "mushroom", OffsetX = -1.3f, OffsetZ = 0.75f, Level = 1 }
                }
            }
        };
    }
}
