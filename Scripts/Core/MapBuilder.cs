using Godot;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.Core;

/// <summary>
/// Builds a 3D dungeon from a MapDefinition. Uses DungeonGenerator3D's
/// geometry building methods to create the actual 3D world.
/// </summary>
public class MapBuilder
{
    private readonly DungeonGenerator3D _generator;
    private MapDefinition? _mapDefinition;

    public MapBuilder(DungeonGenerator3D generator)
    {
        _generator = generator;
    }

    /// <summary>
    /// Builds a dungeon from the given map definition.
    /// </summary>
    public bool BuildFromDefinition(MapDefinition map)
    {
        if (map == null)
        {
            GD.PrintErr("[MapBuilder] Cannot build from null map definition");
            return false;
        }

        _mapDefinition = map;

        GD.Print($"[MapBuilder] Building dungeon from map: {map.Name}");
        GD.Print($"[MapBuilder] Size: {map.Width}x{map.Depth}, Rooms: {map.Rooms.Count}");

        // Call the generator's method to build from definition
        _generator.GenerateFromMapDefinition(map);

        return true;
    }

    /// <summary>
    /// Gets the map definition used for the last build.
    /// </summary>
    public MapDefinition? GetMapDefinition() => _mapDefinition;
}

/// <summary>
/// Extension to DungeonGenerator3D to support MapDefinition-based generation.
/// </summary>
public partial class DungeonGenerator3D
{
    // Flag to indicate we're using a custom map
    private MapDefinition? _customMapDefinition;

    // Deferred enemy spawning system
    private const float DeferredSpawnRadius = 60f;  // Spawn enemies within 60m of player
    private const float DeferredCheckInterval = 0.5f;  // Check every 0.5 seconds
    private float _deferredSpawnTimer = 0f;
    private List<EnemyPlacement>? _pendingEnemies;
    private List<(Vector3 Center, List<GroupMonster> Monsters, int RoomId)>? _pendingGroups;
    private HashSet<string>? _spawnedEnemyIds;

    /// <summary>
    /// Generates a dungeon from a map definition instead of procedurally.
    /// Supports both tile-mode (WYSIWYG painted) and room-mode (traditional) maps.
    /// </summary>
    public void GenerateFromMapDefinition(MapDefinition map)
    {
        _customMapDefinition = map;

        GD.Print($"[DungeonGenerator3D] Generating from map definition: {map.Name} (mode={map.Mode})");

        // Check if this is a tile-mode map
        if (map.IsTileMode && !string.IsNullOrEmpty(map.TileData))
        {
            GenerateFromTileData(map);
            return;
        }

        // Room-mode generation (original logic)
        GenerateFromRoomData(map);
    }

    /// <summary>
    /// Generates a dungeon from tile-mode (WYSIWYG painted) map data.
    /// </summary>
    private void GenerateFromTileData(MapDefinition map)
    {
        GD.Print($"[DungeonGenerator3D] Generating from tile data: {map.Width}x{map.Depth}");

        // Clear existing dungeon
        ClearDungeon();

        // Override dimensions
        DungeonWidth = map.Width;
        DungeonDepth = map.Depth;

        // Decode tile data directly into map data
        var tiles = TileDataEncoder.Decode(map.TileData!, map.Width, map.Depth);

        // Initialize 3D map data from 2D tiles
        _mapData = new int[DungeonWidth, 2, DungeonDepth];
        int floorCount = 0;
        for (int x = 0; x < DungeonWidth; x++)
        {
            for (int z = 0; z < DungeonDepth; z++)
            {
                _mapData[x, 0, z] = tiles[x, z];
                if (tiles[x, z] == 1) floorCount++;
            }
        }
        GD.Print($"[DungeonGenerator3D] Loaded {floorCount} floor tiles from tile data");

        // Create rooms from definition (optional for tile-mode, used for prop placement)
        Rooms.Clear();
        foreach (var roomDef in map.Rooms)
        {
            var room = new Room
            {
                Position = new Vector3I(roomDef.Position.X, 0, roomDef.Position.Z),
                Size = new Vector3I(roomDef.Size.X, 1, roomDef.Size.Z),
                Type = roomDef.Type,
                IsCleared = false
            };
            Rooms.Add(room);
        }

        // Set spawn room based on spawn position
        if (map.SpawnPosition != null && Rooms.Count > 0)
        {
            // Find room containing spawn point
            var spawnX = map.SpawnPosition.X;
            var spawnZ = map.SpawnPosition.Z;
            foreach (var room in Rooms)
            {
                if (spawnX >= room.Position.X && spawnX < room.Position.X + room.Size.X &&
                    spawnZ >= room.Position.Z && spawnZ < room.Position.Z + room.Size.Z)
                {
                    _spawnRoom = room;
                    break;
                }
            }
        }

        // If no spawn room found, create a virtual one at spawn position
        if (_spawnRoom == null && map.SpawnPosition != null)
        {
            _spawnRoom = new Room
            {
                Position = new Vector3I(map.SpawnPosition.X - 2, 0, map.SpawnPosition.Z - 2),
                Size = new Vector3I(5, 1, 5),
                Type = "spawn",
                IsCleared = true
            };
        }
        else if (_spawnRoom == null && Rooms.Count > 0)
        {
            _spawnRoom = Rooms[0];
        }

        // Build 3D geometry
        BuildGeometry();

        // Populate with props and lights
        PopulateRooms();

        // Spawn enemies from placements
        SpawnEnemiesFromDefinition(map);

        // Spawn placed props from map definition
        SpawnPropsFromDefinition(map);

        // Add ambient lighting
        AddAmbientLighting();

        GD.Print($"[DungeonGenerator3D] Tile-mode map built: {Rooms.Count} rooms, {map.Enemies.Count + CountGroupMonsters(map)} enemies, {map.PlacedProps.Count} placed props");
        EmitSignal(SignalName.GenerationComplete);
    }

    /// <summary>
    /// Generates a dungeon from room-mode (traditional) map data.
    /// </summary>
    private void GenerateFromRoomData(MapDefinition map)
    {
        // Clear existing dungeon
        ClearDungeon();

        // Override dimensions
        DungeonWidth = map.Width;
        DungeonDepth = map.Depth;

        // Initialize map data
        _mapData = new int[DungeonWidth, 2, DungeonDepth];
        Rooms.Clear();

        // Create rooms from definition
        foreach (var roomDef in map.Rooms)
        {
            var room = new Room
            {
                Position = new Vector3I(roomDef.Position.X, 0, roomDef.Position.Z),
                Size = new Vector3I(roomDef.Size.X, 1, roomDef.Size.Z),
                Type = roomDef.Type,
                IsCleared = false
            };
            Rooms.Add(room);

            // Mark spawn room
            if (roomDef.IsSpawnRoom || roomDef.Id == map.SpawnRoom)
            {
                _spawnRoom = room;
            }

            // Fill map data for the room
            for (int x = roomDef.Position.X; x < roomDef.Position.X + roomDef.Size.X; x++)
            {
                for (int z = roomDef.Position.Z; z < roomDef.Position.Z + roomDef.Size.Z; z++)
                {
                    if (x >= 0 && x < DungeonWidth && z >= 0 && z < DungeonDepth)
                    {
                        _mapData[x, 0, z] = 1; // Floor
                    }
                }
            }
        }

        // If no spawn room set, use first room
        if (_spawnRoom == null && Rooms.Count > 0)
        {
            _spawnRoom = Rooms[0];
        }

        // Create corridors from definition
        foreach (var corridor in map.Corridors)
        {
            if (corridor.FromRoom >= 0 && corridor.FromRoom < Rooms.Count &&
                corridor.ToRoom >= 0 && corridor.ToRoom < Rooms.Count)
            {
                var fromRoom = Rooms[corridor.FromRoom];
                var toRoom = Rooms[corridor.ToRoom];

                // Get room centers
                Vector3I fromCenter = fromRoom.Position + fromRoom.Size / 2;
                Vector3I toCenter = toRoom.Position + toRoom.Size / 2;

                // Carve L-shaped corridor
                int corridorWidth = corridor.Width > 0 ? corridor.Width : CorridorWidth;
                CarveCorridorFromDefinition(fromCenter, toCenter, corridorWidth);
            }
        }

        // Apply custom tile overrides (user edits from tile editor)
        foreach (var tile in map.CustomTiles)
        {
            if (tile.X >= 0 && tile.X < DungeonWidth && tile.Z >= 0 && tile.Z < DungeonDepth)
            {
                _mapData[tile.X, 0, tile.Z] = tile.IsFloor ? 1 : 0;
            }
        }
        if (map.CustomTiles.Count > 0)
        {
            GD.Print($"[DungeonGenerator3D] Applied {map.CustomTiles.Count} custom tile overrides");
        }

        // Build 3D geometry
        BuildGeometry();

        // Populate with props and lights (using room types)
        PopulateRooms();

        // Spawn enemies from placements instead of procedurally
        SpawnEnemiesFromDefinition(map);

        // Spawn placed props from map definition
        SpawnPropsFromDefinition(map);

        // Add ambient lighting
        AddAmbientLighting();

        GD.Print($"[DungeonGenerator3D] Map built: {Rooms.Count} rooms, {map.Enemies.Count + CountGroupMonsters(map)} enemies, {map.PlacedProps.Count} placed props");
        EmitSignal(SignalName.GenerationComplete);
    }

    /// <summary>
    /// Carves an L-shaped corridor between two points.
    /// Uses a wide brush approach to ensure no gaps.
    /// </summary>
    private void CarveCorridorFromDefinition(Vector3I from, Vector3I to, int width)
    {
        int halfWidth = width / 2;

        // Carve using a "wide brush" approach - carve along both X and Z axes
        // with full width to ensure complete coverage

        // First: Carve horizontal segment from from.X to to.X at from.Z level
        int minX = Mathf.Min(from.X, to.X);
        int maxX = Mathf.Max(from.X, to.X);
        for (int x = minX - halfWidth; x <= maxX + halfWidth; x++)
        {
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                int z = from.Z + w;
                if (x >= 0 && x < DungeonWidth && z >= 0 && z < DungeonDepth)
                {
                    _mapData![x, 0, z] = 1;
                }
            }
        }

        // Second: Carve vertical segment from from.Z to to.Z at to.X level
        int minZ = Mathf.Min(from.Z, to.Z);
        int maxZ = Mathf.Max(from.Z, to.Z);
        for (int z = minZ - halfWidth; z <= maxZ + halfWidth; z++)
        {
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                int x = to.X + w;
                if (x >= 0 && x < DungeonWidth && z >= 0 && z < DungeonDepth)
                {
                    _mapData![x, 0, z] = 1;
                }
            }
        }

        // Third: Fill the corner/elbow area generously to ensure no wall segments remain
        // The corner is at (to.X, from.Z) - fill a large square area there
        for (int dx = -halfWidth - 1; dx <= halfWidth + 1; dx++)
        {
            for (int dz = -halfWidth - 1; dz <= halfWidth + 1; dz++)
            {
                int x = to.X + dx;
                int z = from.Z + dz;
                if (x >= 0 && x < DungeonWidth && z >= 0 && z < DungeonDepth)
                {
                    _mapData![x, 0, z] = 1;
                }
            }
        }

        // Fourth: Also fill around the from point to ensure room connection is clean
        for (int dx = -halfWidth - 1; dx <= halfWidth + 1; dx++)
        {
            for (int dz = -halfWidth - 1; dz <= halfWidth + 1; dz++)
            {
                int x = from.X + dx;
                int z = from.Z + dz;
                if (x >= 0 && x < DungeonWidth && z >= 0 && z < DungeonDepth)
                {
                    _mapData![x, 0, z] = 1;
                }
            }
        }

        // Fifth: Fill around the to point as well
        for (int dx = -halfWidth - 1; dx <= halfWidth + 1; dx++)
        {
            for (int dz = -halfWidth - 1; dz <= halfWidth + 1; dz++)
            {
                int x = to.X + dx;
                int z = to.Z + dz;
                if (x >= 0 && x < DungeonWidth && z >= 0 && z < DungeonDepth)
                {
                    _mapData![x, 0, z] = 1;
                }
            }
        }
    }

    /// <summary>
    /// Spawns enemies from the map definition's placement data using deferred spawning.
    /// Only enemies within DeferredSpawnRadius of the spawn point are created initially.
    /// </summary>
    private void SpawnEnemiesFromDefinition(MapDefinition map)
    {
        // Initialize deferred spawning
        _pendingEnemies = new List<EnemyPlacement>(map.Enemies);
        _pendingGroups = new List<(Vector3, List<GroupMonster>, int)>();
        _spawnedEnemyIds = new HashSet<string>();

        // Convert monster groups to pending list
        foreach (var group in map.MonsterGroups)
        {
            Vector3 groupCenter = new Vector3(
                group.Center.X * TileSize + TileSize / 2f,
                0,
                group.Center.Z * TileSize + TileSize / 2f
            );
            _pendingGroups.Add((groupCenter, group.Monsters, group.RoomId));
        }

        // Get spawn position for initial radius check
        Vector3 spawnPos = GetSpawnPosition();

        int spawnedCount = 0;
        int deferredCount = 0;

        // Spawn enemies within initial radius
        for (int i = _pendingEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = _pendingEnemies[i];
            Vector3 position = new Vector3(
                enemy.Position.X * TileSize + TileSize / 2f,
                0,
                enemy.Position.Z * TileSize + TileSize / 2f
            );

            float distance = position.DistanceTo(spawnPos);
            if (distance <= DeferredSpawnRadius)
            {
                var spawnedEnemy = SpawnEnemyFromPlacement(enemy.Type, position, enemy.Level, enemy.IsBoss, enemy.RotationY);
                if (spawnedEnemy != null)
                {
                    spawnedCount++;
                    _spawnedEnemyIds.Add(enemy.PlacementId);
                    if (enemy.RoomId >= 0 && enemy.RoomId < Rooms.Count)
                    {
                        Rooms[enemy.RoomId].Enemies.Add(spawnedEnemy);
                    }
                }
                _pendingEnemies.RemoveAt(i);
            }
            else
            {
                deferredCount++;
            }
        }

        // Spawn groups within initial radius
        for (int i = _pendingGroups.Count - 1; i >= 0; i--)
        {
            var (groupCenter, monsters, roomId) = _pendingGroups[i];
            float distance = groupCenter.DistanceTo(spawnPos);

            if (distance <= DeferredSpawnRadius)
            {
                foreach (var monster in monsters)
                {
                    Vector3 position = groupCenter + new Vector3(monster.OffsetX, 0, monster.OffsetZ);
                    var spawnedEnemy = SpawnEnemyFromPlacement(monster.Type, position, monster.Level, monster.IsBoss);
                    if (spawnedEnemy != null)
                    {
                        spawnedCount++;
                        if (roomId >= 0 && roomId < Rooms.Count)
                        {
                            Rooms[roomId].Enemies.Add(spawnedEnemy);
                        }
                    }
                }
                _pendingGroups.RemoveAt(i);
            }
            else
            {
                deferredCount += monsters.Count;
            }
        }

        GD.Print($"[DungeonGenerator3D] Spawned {spawnedCount} enemies initially, {deferredCount} deferred for later");
    }

    /// <summary>
    /// Updates deferred enemy spawning based on player position.
    /// Call this from _Process in GameManager3D.
    /// </summary>
    public void UpdateDeferredSpawning(float delta)
    {
        if (_pendingEnemies == null && _pendingGroups == null) return;
        if ((_pendingEnemies?.Count ?? 0) == 0 && (_pendingGroups?.Count ?? 0) == 0)
        {
            // All enemies spawned, clear lists
            _pendingEnemies = null;
            _pendingGroups = null;
            return;
        }

        _deferredSpawnTimer += delta;
        if (_deferredSpawnTimer < DeferredCheckInterval) return;
        _deferredSpawnTimer = 0f;

        // Get player position
        var player = Player.FPSController.Instance;
        if (player == null) return;
        Vector3 playerPos = player.GlobalPosition;

        int spawnedThisFrame = 0;
        const int maxSpawnsPerFrame = 10;  // Limit spawns per check to avoid hitches

        // Check pending individual enemies
        if (_pendingEnemies != null)
        {
            for (int i = _pendingEnemies.Count - 1; i >= 0 && spawnedThisFrame < maxSpawnsPerFrame; i--)
            {
                var enemy = _pendingEnemies[i];
                Vector3 position = new Vector3(
                    enemy.Position.X * TileSize + TileSize / 2f,
                    0,
                    enemy.Position.Z * TileSize + TileSize / 2f
                );

                float distance = position.DistanceTo(playerPos);
                if (distance <= DeferredSpawnRadius)
                {
                    var spawnedEnemy = SpawnEnemyFromPlacement(enemy.Type, position, enemy.Level, enemy.IsBoss, enemy.RotationY);
                    if (spawnedEnemy != null)
                    {
                        spawnedThisFrame++;
                        if (enemy.RoomId >= 0 && enemy.RoomId < Rooms.Count)
                        {
                            Rooms[enemy.RoomId].Enemies.Add(spawnedEnemy);
                        }
                    }
                    _pendingEnemies.RemoveAt(i);
                }
            }
        }

        // Check pending groups
        if (_pendingGroups != null)
        {
            for (int i = _pendingGroups.Count - 1; i >= 0 && spawnedThisFrame < maxSpawnsPerFrame; i--)
            {
                var (groupCenter, monsters, roomId) = _pendingGroups[i];
                float distance = groupCenter.DistanceTo(playerPos);

                if (distance <= DeferredSpawnRadius)
                {
                    foreach (var monster in monsters)
                    {
                        Vector3 position = groupCenter + new Vector3(monster.OffsetX, 0, monster.OffsetZ);
                        var spawnedEnemy = SpawnEnemyFromPlacement(monster.Type, position, monster.Level, monster.IsBoss);
                        if (spawnedEnemy != null)
                        {
                            spawnedThisFrame++;
                            if (roomId >= 0 && roomId < Rooms.Count)
                            {
                                Rooms[roomId].Enemies.Add(spawnedEnemy);
                            }
                        }
                    }
                    _pendingGroups.RemoveAt(i);
                }
            }
        }

        if (spawnedThisFrame > 0)
        {
            int remaining = (_pendingEnemies?.Count ?? 0) + (_pendingGroups?.Sum(g => g.Monsters.Count) ?? 0);
            GD.Print($"[DungeonGenerator3D] Deferred spawn: {spawnedThisFrame} enemies, {remaining} remaining");
        }
    }

    /// <summary>
    /// Spawns a single enemy from placement data.
    /// </summary>
    private Node3D? SpawnEnemyFromPlacement(string type, Vector3 position, int level, bool isBoss, float rotationY = 0f)
    {
        if (_enemyContainer == null) return null;

        Node3D enemy;

        if (isBoss)
        {
            // Create boss enemy
            var boss = new BossEnemy3D();
            boss.Position = position;
            boss.MonsterType = type;
            enemy = boss;
        }
        else
        {
            // Use the existing CreateMonsterByType method
            enemy = CreateMonsterByType(type, position, level);
        }

        // Apply rotation from placement
        enemy.Rotation = new Vector3(0, rotationY, 0);

        enemy.AddToGroup("Enemies");
        _enemyContainer.AddChild(enemy);

        return enemy;
    }

    /// <summary>
    /// Counts total monsters in all groups.
    /// </summary>
    private int CountGroupMonsters(MapDefinition map)
    {
        int count = 0;
        foreach (var group in map.MonsterGroups)
        {
            count += group.Monsters.Count;
        }
        return count;
    }

    /// <summary>
    /// Gets the current custom map definition (if any).
    /// </summary>
    public MapDefinition? GetCustomMapDefinition() => _customMapDefinition;

    /// <summary>
    /// Checks if the current dungeon was generated from a custom map.
    /// </summary>
    public bool IsCustomMap => _customMapDefinition != null;

    /// <summary>
    /// Spawns props from the map definition's PlacedProps data.
    /// </summary>
    private void SpawnPropsFromDefinition(MapDefinition map)
    {
        if (_propContainer == null || map.PlacedProps == null || map.PlacedProps.Count == 0)
        {
            return;
        }

        int spawnedCount = 0;
        foreach (var propData in map.PlacedProps)
        {
            var position = new Vector3(propData.X, propData.Y, propData.Z);

            // Create the prop using Cosmetic3D
            var prop = SafeRoom3D.Environment.Cosmetic3D.Create(
                propData.Type,
                new Color(0.6f, 0.4f, 0.3f),  // Default primary color
                new Color(0.4f, 0.3f, 0.2f),  // Default secondary color
                new Color(0.5f, 0.5f, 0.5f),  // Default accent color
                0f,
                propData.Scale,
                true  // Enable light
            );

            if (prop != null)
            {
                prop.Position = position;
                prop.Rotation = new Vector3(0, propData.RotationY, 0);
                _propContainer.AddChild(prop);
                spawnedCount++;
            }
        }

        GD.Print($"[DungeonGenerator3D] Spawned {spawnedCount} placed props from map definition");
    }
}
