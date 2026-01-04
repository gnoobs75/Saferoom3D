using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Parses dungeon map images to create MapDefinition objects.
/// Uses flood-fill to detect rooms from image data.
///
/// Image format:
/// - White (>230 brightness): Floor tile
/// - Black (<30 brightness): Wall/void
/// - Gray (100-200): Corridor
/// - 1 pixel = 1 tile
/// </summary>
public static class MapParser
{
    private const int FLOOR_THRESHOLD = 100;  // Brightness above this = floor
    private const int ROOM_MIN_SIZE = 16;     // Minimum pixels to count as a room
    private const int CORRIDOR_WIDTH_MAX = 8; // Max width to consider a corridor vs room

    /// <summary>
    /// Parses a dungeon map image and creates a MapDefinition.
    /// </summary>
    public static MapDefinition? ParseFromImage(string imagePath)
    {
        GD.Print($"[MapParser] Loading image: {imagePath}");

        var image = Image.LoadFromFile(imagePath);
        if (image == null)
        {
            GD.PrintErr($"[MapParser] Failed to load image: {imagePath}");
            return null;
        }

        int width = image.GetWidth();
        int height = image.GetHeight();

        GD.Print($"[MapParser] Image size: {width}x{height}");

        // Convert image to floor/wall grid
        bool[,] floorGrid = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var pixel = image.GetPixel(x, y);
                float brightness = (pixel.R + pixel.G + pixel.B) / 3f;
                floorGrid[x, y] = brightness > (FLOOR_THRESHOLD / 255f);
            }
        }

        // Detect rooms using flood fill
        var rooms = DetectRooms(floorGrid, width, height);

        if (rooms.Count == 0)
        {
            GD.PrintErr("[MapParser] No rooms detected in image");
            return null;
        }

        GD.Print($"[MapParser] Detected {rooms.Count} rooms");

        // Create map definition
        var map = new MapDefinition
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(imagePath),
            Width = width,
            Depth = height,
            SpawnRoom = 0
        };

        // Add room definitions
        for (int i = 0; i < rooms.Count; i++)
        {
            var (minX, minY, maxX, maxY) = rooms[i];
            map.Rooms.Add(new RoomDefinition
            {
                Id = i,
                Position = new PositionData { X = minX, Z = minY },
                Size = new SizeData { X = maxX - minX + 1, Z = maxY - minY + 1 },
                Type = GetRoomType(i, rooms.Count),
                IsSpawnRoom = (i == 0)
            });
        }

        // Detect corridors by finding connected floor areas between rooms
        var corridors = DetectCorridors(floorGrid, rooms, width, height);
        map.Corridors.AddRange(corridors);

        GD.Print($"[MapParser] Detected {corridors.Count} corridors");

        return map;
    }

    /// <summary>
    /// Detects rooms using flood fill algorithm.
    /// Returns list of room bounds (minX, minY, maxX, maxY).
    /// </summary>
    private static List<(int minX, int minY, int maxX, int maxY)> DetectRooms(
        bool[,] floorGrid, int width, int height)
    {
        var rooms = new List<(int, int, int, int)>();
        var visited = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (floorGrid[x, y] && !visited[x, y])
                {
                    // Flood fill to find connected region
                    var (minX, minY, maxX, maxY, pixelCount) = FloodFill(floorGrid, visited, x, y, width, height);

                    // Check if this is a room (large enough and roughly rectangular)
                    int regionWidth = maxX - minX + 1;
                    int regionHeight = maxY - minY + 1;

                    // Consider it a room if it's larger than minimum and not too narrow
                    if (pixelCount >= ROOM_MIN_SIZE &&
                        regionWidth > CORRIDOR_WIDTH_MAX &&
                        regionHeight > CORRIDOR_WIDTH_MAX)
                    {
                        rooms.Add((minX, minY, maxX, maxY));
                    }
                }
            }
        }

        // Sort rooms by position (top-left first)
        rooms.Sort((a, b) =>
        {
            int yCompare = a.Item2.CompareTo(b.Item2);  // minY
            return yCompare != 0 ? yCompare : a.Item1.CompareTo(b.Item1);  // minX
        });

        return rooms;
    }

    /// <summary>
    /// Flood fill from a starting point, returns bounds and pixel count.
    /// </summary>
    private static (int minX, int minY, int maxX, int maxY, int count) FloodFill(
        bool[,] grid, bool[,] visited, int startX, int startY, int width, int height)
    {
        int minX = startX, maxX = startX;
        int minY = startY, maxY = startY;
        int count = 0;

        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();

            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (!grid[x, y] || visited[x, y]) continue;

            visited[x, y] = true;
            count++;

            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);

            // Add neighbors (4-connected)
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }

        return (minX, minY, maxX, maxY, count);
    }

    /// <summary>
    /// Detects corridors connecting rooms.
    /// </summary>
    private static List<CorridorDefinition> DetectCorridors(
        bool[,] floorGrid, List<(int minX, int minY, int maxX, int maxY)> rooms,
        int width, int height)
    {
        var corridors = new List<CorridorDefinition>();
        var connected = new HashSet<(int, int)>();

        // For each pair of rooms, check if they're connected
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (connected.Contains((i, j))) continue;

                // Check if rooms are connected via floor tiles
                if (AreRoomsConnected(floorGrid, rooms[i], rooms[j], width, height))
                {
                    corridors.Add(new CorridorDefinition
                    {
                        FromRoom = i,
                        ToRoom = j,
                        Width = 5
                    });
                    connected.Add((i, j));
                }
            }
        }

        return corridors;
    }

    /// <summary>
    /// Checks if two rooms are connected by floor tiles.
    /// </summary>
    private static bool AreRoomsConnected(
        bool[,] floorGrid,
        (int minX, int minY, int maxX, int maxY) room1,
        (int minX, int minY, int maxX, int maxY) room2,
        int width, int height)
    {
        // Simple check: see if there's a floor path from room1 edge to room2 edge
        // This is a simplified version - a more robust implementation would use pathfinding

        // Get room centers
        int cx1 = (room1.minX + room1.maxX) / 2;
        int cy1 = (room1.minY + room1.maxY) / 2;
        int cx2 = (room2.minX + room2.maxX) / 2;
        int cy2 = (room2.minY + room2.maxY) / 2;

        // Check if rooms are adjacent (within reasonable distance)
        int distX = Mathf.Abs(cx1 - cx2);
        int distY = Mathf.Abs(cy1 - cy2);

        // If rooms are too far apart, they're not directly connected
        if (distX > 50 || distY > 50) return false;

        // Check for floor continuity along a line between rooms
        // Walk from room1 edge toward room2 and see if we hit floor
        int steps = Mathf.Max(distX, distY);
        if (steps == 0) return true;

        for (int s = 0; s <= steps; s++)
        {
            int x = cx1 + (cx2 - cx1) * s / steps;
            int y = cy1 + (cy2 - cy1) * s / steps;

            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                if (!floorGrid[x, y]) return false; // Hit a wall
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a room type based on position in the dungeon.
    /// </summary>
    private static string GetRoomType(int roomIndex, int totalRooms)
    {
        // First room is spawn
        if (roomIndex == 0) return "spawn";

        // Distribute room types
        string[] types = { "storage", "library", "armory", "crypt", "cathedral" };
        return types[(roomIndex - 1) % types.Length];
    }

    /// <summary>
    /// Validates a map definition for playability.
    /// </summary>
    public static (bool isValid, string message) ValidateMap(MapDefinition map)
    {
        if (map == null)
            return (false, "Map is null");

        if (map.Rooms.Count == 0)
            return (false, "Map has no rooms");

        if (map.Width < 20 || map.Depth < 20)
            return (false, "Map is too small (minimum 20x20)");

        if (map.Width > 500 || map.Depth > 500)
            return (false, "Map is too large (maximum 500x500)");

        // Check for spawn room
        bool hasSpawnRoom = false;
        foreach (var room in map.Rooms)
        {
            if (room.IsSpawnRoom || room.Id == map.SpawnRoom)
            {
                hasSpawnRoom = true;
                break;
            }
        }

        if (!hasSpawnRoom && map.Rooms.Count > 0)
        {
            // Auto-set first room as spawn
            map.Rooms[0].IsSpawnRoom = true;
        }

        return (true, "Map is valid");
    }

    /// <summary>
    /// Creates a sample map definition for testing.
    /// </summary>
    public static MapDefinition CreateSampleMap()
    {
        var map = new MapDefinition
        {
            Name = "Sample Dungeon",
            Width = 80,
            Depth = 80,
            SpawnRoom = 0
        };

        // Central spawn room
        map.Rooms.Add(new RoomDefinition
        {
            Id = 0,
            Position = new PositionData { X = 30, Z = 30 },
            Size = new SizeData { X = 20, Z = 20 },
            Type = "spawn",
            IsSpawnRoom = true
        });

        // Northern room
        map.Rooms.Add(new RoomDefinition
        {
            Id = 1,
            Position = new PositionData { X = 32, Z = 5 },
            Size = new SizeData { X = 16, Z = 16 },
            Type = "armory"
        });

        // Eastern room
        map.Rooms.Add(new RoomDefinition
        {
            Id = 2,
            Position = new PositionData { X = 60, Z = 32 },
            Size = new SizeData { X = 15, Z = 15 },
            Type = "library"
        });

        // Southern room
        map.Rooms.Add(new RoomDefinition
        {
            Id = 3,
            Position = new PositionData { X = 32, Z = 58 },
            Size = new SizeData { X = 16, Z = 18 },
            Type = "crypt"
        });

        // Western room
        map.Rooms.Add(new RoomDefinition
        {
            Id = 4,
            Position = new PositionData { X = 5, Z = 32 },
            Size = new SizeData { X = 18, Z = 16 },
            Type = "cathedral"
        });

        // Corridors
        map.Corridors.Add(new CorridorDefinition { FromRoom = 0, ToRoom = 1, Width = 5 });
        map.Corridors.Add(new CorridorDefinition { FromRoom = 0, ToRoom = 2, Width = 5 });
        map.Corridors.Add(new CorridorDefinition { FromRoom = 0, ToRoom = 3, Width = 5 });
        map.Corridors.Add(new CorridorDefinition { FromRoom = 0, ToRoom = 4, Width = 5 });

        // Add some enemies
        map.Enemies.Add(new EnemyPlacement
        {
            Type = "goblin",
            RoomId = 1,
            Position = new PositionData { X = 38, Z = 10 },
            Level = 1
        });

        map.Enemies.Add(new EnemyPlacement
        {
            Type = "skeleton",
            RoomId = 3,
            Position = new PositionData { X = 38, Z = 65 },
            Level = 2
        });

        // Add a monster group
        map.MonsterGroups.Add(new MonsterGroupPlacement
        {
            Id = "g1",
            Name = "Goblin Patrol",
            RoomId = 2,
            Center = new PositionData { X = 67, Z = 38 },
            Monsters = new List<GroupMonster>
            {
                new() { Type = "goblin", OffsetX = -1.5f, OffsetZ = 0, Level = 1 },
                new() { Type = "goblin", OffsetX = 1.5f, OffsetZ = 0, Level = 1 },
                new() { Type = "goblin_shaman", OffsetX = 0, OffsetZ = 2, Level = 2 }
            }
        });

        // Add default group templates
        map.GroupTemplates.AddRange(DefaultGroupTemplates.GetDefaultTemplates());

        return map;
    }
}
