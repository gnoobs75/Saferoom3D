using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    private static float _deferredEnemyRadius = 100f;  // Spawn enemies within this radius
    private const float DeferredCheckInterval = 0.5f;  // Check every 0.5 seconds
    private static int _maxEnemiesPerFrame = 15;
    private float _deferredSpawnTimer = 0f;
    private List<EnemyPlacement>? _pendingEnemies;
    private List<(Vector3 Center, List<GroupMonster> Monsters, int RoomId)>? _pendingGroups;

    // Roamer waypoints - all enemy positions for group-to-group patrol
    private List<Vector3>? _roamerWaypoints;
    private HashSet<string>? _spawnedEnemyIds;

    // Deferred tile building system (for large maps)
    private static float _deferredTileRadius = 120f;  // Build tiles within this radius
    private static int _maxTilesPerFrame = 100;  // Build max tiles per frame
    private HashSet<(int x, int z)>? _builtTiles;
    private bool _useDeferredTileBuilding = false;
    private Vector3 _lastTileBuildPosition = Vector3.Zero;

    // Deferred prop spawning system (for large maps with many props)
    private static float _deferredPropRadius = 80f;  // Spawn props within this radius
    private static int _maxPropsPerFrame = 10;  // Limit props per frame (they're expensive!)
    private List<PropPlacement>? _pendingProps;
    private HashSet<int>? _spawnedPropIds;

    // Public accessors for settings UI (auto-save on change)
    public static float DeferredEnemyRadius { get => _deferredEnemyRadius; set { _deferredEnemyRadius = Mathf.Max(20f, value); SaveStreamingSettings(); } }
    public static float DeferredTileRadius { get => _deferredTileRadius; set { _deferredTileRadius = Mathf.Max(40f, value); SaveStreamingSettings(); } }
    public static float DeferredPropRadius { get => _deferredPropRadius; set { _deferredPropRadius = Mathf.Max(20f, value); SaveStreamingSettings(); } }
    public static int MaxEnemiesPerFrame { get => _maxEnemiesPerFrame; set { _maxEnemiesPerFrame = Mathf.Clamp(value, 1, 50); SaveStreamingSettings(); } }
    public static int MaxTilesPerFrame { get => _maxTilesPerFrame; set { _maxTilesPerFrame = Mathf.Clamp(value, 10, 500); SaveStreamingSettings(); } }
    public static int MaxPropsPerFrame { get => _maxPropsPerFrame; set { _maxPropsPerFrame = Mathf.Clamp(value, 1, 50); SaveStreamingSettings(); } }

    // Settings persistence
    private const string StreamingSettingsPath = "user://streaming_settings.json";
    private static bool _settingsLoaded = false;

    /// <summary>
    /// Load streaming settings from disk. Called automatically on first access.
    /// </summary>
    public static void LoadStreamingSettings()
    {
        if (_settingsLoaded) return;
        _settingsLoaded = true;

        if (!FileAccess.FileExists(StreamingSettingsPath))
        {
            GD.Print("[MapBuilder] No saved streaming settings found, using defaults");
            return;
        }

        try
        {
            using var file = FileAccess.Open(StreamingSettingsPath, FileAccess.ModeFlags.Read);
            if (file == null) return;

            string json = file.GetAsText();
            var data = JsonSerializer.Deserialize<Dictionary<string, float>>(json);

            if (data != null)
            {
                if (data.TryGetValue("EnemyRadius", out float enemyRadius))
                    _deferredEnemyRadius = Mathf.Max(20f, enemyRadius);
                if (data.TryGetValue("TileRadius", out float tileRadius))
                    _deferredTileRadius = Mathf.Max(40f, tileRadius);
                if (data.TryGetValue("PropRadius", out float propRadius))
                    _deferredPropRadius = Mathf.Max(20f, propRadius);
                if (data.TryGetValue("EnemiesPerFrame", out float enemiesPerFrame))
                    _maxEnemiesPerFrame = Mathf.Clamp((int)enemiesPerFrame, 1, 50);
                if (data.TryGetValue("TilesPerFrame", out float tilesPerFrame))
                    _maxTilesPerFrame = Mathf.Clamp((int)tilesPerFrame, 10, 500);
                if (data.TryGetValue("PropsPerFrame", out float propsPerFrame))
                    _maxPropsPerFrame = Mathf.Clamp((int)propsPerFrame, 1, 50);

                GD.Print($"[MapBuilder] Loaded streaming settings: Enemy={_deferredEnemyRadius}m, Tile={_deferredTileRadius}m, Prop={_deferredPropRadius}m");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MapBuilder] Failed to load streaming settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Save streaming settings to disk.
    /// </summary>
    public static void SaveStreamingSettings()
    {
        if (!_settingsLoaded) return;  // Don't save during initial load

        try
        {
            var data = new Dictionary<string, float>
            {
                ["EnemyRadius"] = _deferredEnemyRadius,
                ["TileRadius"] = _deferredTileRadius,
                ["PropRadius"] = _deferredPropRadius,
                ["EnemiesPerFrame"] = _maxEnemiesPerFrame,
                ["TilesPerFrame"] = _maxTilesPerFrame,
                ["PropsPerFrame"] = _maxPropsPerFrame
            };

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            using var file = FileAccess.Open(StreamingSettingsPath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
                GD.Print("[MapBuilder] Saved streaming settings");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MapBuilder] Failed to save streaming settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a dungeon from a map definition instead of procedurally.
    /// Supports both tile-mode (WYSIWYG painted) and room-mode (traditional) maps.
    /// </summary>
    public void GenerateFromMapDefinition(MapDefinition map)
    {
        // Load streaming settings if not already loaded
        LoadStreamingSettings();

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
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        GD.Print($"[DungeonGenerator3D] Generating from tile data: {map.Width}x{map.Depth}");

        // Clear existing dungeon
        ClearDungeon();
        GD.Print($"[TIMING] ClearDungeon: {stopwatch.ElapsedMilliseconds}ms");

        // Override dimensions
        DungeonWidth = map.Width;
        DungeonDepth = map.Depth;

        // Decode tile data directly into map data
        var tiles = TileDataEncoder.Decode(map.TileData!, map.Width, map.Depth);
        GD.Print($"[TIMING] Decode: {stopwatch.ElapsedMilliseconds}ms");

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
        GD.Print($"[TIMING] MapData init: {stopwatch.ElapsedMilliseconds}ms");

        // For large maps (>100x100), use deferred tile building for faster load
        _useDeferredTileBuilding = (map.Width > 100 || map.Depth > 100);
        if (_useDeferredTileBuilding)
        {
            _builtTiles = new HashSet<(int x, int z)>();
            GD.Print($"[DungeonGenerator3D] Using deferred tile building (map size: {map.Width}x{map.Depth})");
        }

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
        GD.Print($"[TIMING] Before BuildGeometry: {stopwatch.ElapsedMilliseconds}ms");
        BuildGeometry();
        GD.Print($"[TIMING] After BuildGeometry: {stopwatch.ElapsedMilliseconds}ms");

        // Populate with props and lights
        PopulateRooms();
        GD.Print($"[TIMING] After PopulateRooms: {stopwatch.ElapsedMilliseconds}ms");

        // Spawn enemies from placements
        SpawnEnemiesFromDefinition(map);
        GD.Print($"[TIMING] After SpawnEnemies: {stopwatch.ElapsedMilliseconds}ms");

        // Spawn placed props from map definition
        SpawnPropsFromDefinition(map);
        GD.Print($"[TIMING] After SpawnProps: {stopwatch.ElapsedMilliseconds}ms");

        // Spawn NPCs from map definition
        SpawnNPCsFromDefinition(map);
        GD.Print($"[TIMING] After SpawnNPCs: {stopwatch.ElapsedMilliseconds}ms");

        // Add ambient lighting
        AddAmbientLighting();
        GD.Print($"[TIMING] TOTAL: {stopwatch.ElapsedMilliseconds}ms");

        GD.Print($"[DungeonGenerator3D] Tile-mode map built: {Rooms.Count} rooms, {map.Enemies.Count + CountGroupMonsters(map)} enemies, {map.PlacedProps.Count} placed props, {map.Npcs.Count} NPCs");
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

        // Spawn NPCs from map definition
        SpawnNPCsFromDefinition(map);

        // Add ambient lighting
        AddAmbientLighting();

        GD.Print($"[DungeonGenerator3D] Map built: {Rooms.Count} rooms, {map.Enemies.Count + CountGroupMonsters(map)} enemies, {map.PlacedProps.Count} placed props, {map.Npcs.Count} NPCs");
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
    /// Only enemies within _deferredEnemyRadius of the spawn point are created initially.
    /// </summary>
    private void SpawnEnemiesFromDefinition(MapDefinition map)
    {
        // Initialize deferred spawning
        _pendingEnemies = new List<EnemyPlacement>(map.Enemies);
        _pendingGroups = new List<(Vector3, List<GroupMonster>, int)>();
        _spawnedEnemyIds = new HashSet<string>();

        // Collect all enemy positions for roamer waypoints FIRST
        _roamerWaypoints = new List<Vector3>();
        foreach (var enemy in map.Enemies)
        {
            Vector3 position = new Vector3(
                enemy.Position.X * TileSize + TileSize / 2f,
                0,
                enemy.Position.Z * TileSize + TileSize / 2f
            );
            _roamerWaypoints.Add(position);
        }

        // Convert monster groups to pending list and collect positions
        foreach (var group in map.MonsterGroups)
        {
            Vector3 groupCenter = new Vector3(
                group.Center.X * TileSize + TileSize / 2f,
                0,
                group.Center.Z * TileSize + TileSize / 2f
            );
            _pendingGroups.Add((groupCenter, group.Monsters, group.RoomId));

            // Add each monster in group to waypoints
            foreach (var monster in group.Monsters)
            {
                Vector3 position = groupCenter + new Vector3(monster.OffsetX, 0, monster.OffsetZ);
                _roamerWaypoints.Add(position);
            }
        }

        GD.Print($"[DungeonGenerator3D] Collected {_roamerWaypoints.Count} waypoints for roamers");

        // Get spawn position for initial radius check
        Vector3 spawnPos = GetSpawnPosition();

        int spawnedCount = 0;
        int deferredCount = 0;
        var spawnedRoamers = new List<Enemies.BasicEnemy3D>();

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
            if (distance <= _deferredEnemyRadius)
            {
                var spawnedEnemy = SpawnEnemyFromPlacement(enemy.Type, position, enemy.Level, enemy.IsBoss, enemy.RotationY, enemy.IsRoamer);
                if (spawnedEnemy != null)
                {
                    spawnedCount++;
                    _spawnedEnemyIds.Add(enemy.PlacementId);
                    if (enemy.RoomId >= 0 && enemy.RoomId < Rooms.Count)
                    {
                        Rooms[enemy.RoomId].Enemies.Add(spawnedEnemy);
                    }
                    // Track spawned roamers to set waypoints later
                    if (enemy.IsRoamer && spawnedEnemy is Enemies.BasicEnemy3D basicEnemy)
                    {
                        spawnedRoamers.Add(basicEnemy);
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

            if (distance <= _deferredEnemyRadius)
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

        // Pass waypoints to all spawned roamers
        if (_roamerWaypoints.Count > 1)
        {
            foreach (var roamer in spawnedRoamers)
            {
                roamer.SetRoamWaypoints(_roamerWaypoints);
            }
            GD.Print($"[DungeonGenerator3D] Set waypoints for {spawnedRoamers.Count} roamers");
        }

        GD.Print($"[DungeonGenerator3D] Spawned {spawnedCount} enemies initially, {deferredCount} deferred for later");

        // Generate quests based on monsters on this floor
        GenerateQuestsForFloor(map);
    }

    /// <summary>
    /// Generate quests for the current floor based on monster types present.
    /// </summary>
    private void GenerateQuestsForFloor(MapDefinition map)
    {
        var monsterTypes = new System.Collections.Generic.List<string>();

        // Collect monster types from individual enemies
        foreach (var enemy in map.Enemies)
        {
            if (!string.IsNullOrEmpty(enemy.Type))
            {
                monsterTypes.Add(enemy.Type);
            }
        }

        // Collect monster types from groups
        foreach (var group in map.MonsterGroups)
        {
            foreach (var monster in group.Monsters)
            {
                if (!string.IsNullOrEmpty(monster.Type))
                {
                    monsterTypes.Add(monster.Type);
                }
            }
        }

        // Generate quests if we have monsters
        if (monsterTypes.Count > 0)
        {
            int floorLevel = 1; // TODO: Get from map definition or floor counter
            QuestManager.Instance?.GenerateQuestsForFloor(monsterTypes, floorLevel);
            GD.Print($"[DungeonGenerator3D] Generated quests for {monsterTypes.Count} monster types");
        }
    }

    // Startup delay to let GPU stabilize (helps with Intel integrated graphics)
    private float _startupDelay = 0.5f;
    private bool _startupComplete = false;

    /// <summary>
    /// Updates deferred enemy spawning based on player position.
    /// Call this from _Process in GameManager3D.
    /// </summary>
    public void UpdateDeferredSpawning(float delta)
    {
        // Wait for startup delay to complete (helps with Intel GPUs)
        if (!_startupComplete)
        {
            _startupDelay -= delta;
            if (_startupDelay > 0) return;
            _startupComplete = true;
            GD.Print("[MapBuilder] Startup delay complete, beginning deferred spawning");
        }

        // Get player position (needed for both enemy spawning and tile building)
        var player = Player.FPSController.Instance;
        if (player == null) return;
        Vector3 playerPos = player.GlobalPosition;

        // Always update deferred systems, even if no pending enemies
        UpdateDeferredTileBuilding(playerPos);
        UpdateDeferredPropSpawning(playerPos);

        // Check if any enemies need spawning
        bool hasEnemies = (_pendingEnemies?.Count ?? 0) > 0 || (_pendingGroups?.Count ?? 0) > 0;
        if (!hasEnemies)
        {
            // All enemies spawned, clear lists
            _pendingEnemies = null;
            _pendingGroups = null;
            return;
        }

        _deferredSpawnTimer += delta;
        if (_deferredSpawnTimer < DeferredCheckInterval) return;
        _deferredSpawnTimer = 0f;

        int spawnedThisFrame = 0;

        // Check pending individual enemies
        if (_pendingEnemies != null)
        {
            for (int i = _pendingEnemies.Count - 1; i >= 0 && spawnedThisFrame < _maxEnemiesPerFrame; i--)
            {
                var enemy = _pendingEnemies[i];
                Vector3 position = new Vector3(
                    enemy.Position.X * TileSize + TileSize / 2f,
                    0,
                    enemy.Position.Z * TileSize + TileSize / 2f
                );

                float distance = position.DistanceTo(playerPos);
                if (distance <= _deferredEnemyRadius)
                {
                    var spawnedEnemy = SpawnEnemyFromPlacement(enemy.Type, position, enemy.Level, enemy.IsBoss, enemy.RotationY, enemy.IsRoamer);
                    if (spawnedEnemy != null)
                    {
                        spawnedThisFrame++;
                        if (enemy.RoomId >= 0 && enemy.RoomId < Rooms.Count)
                        {
                            Rooms[enemy.RoomId].Enemies.Add(spawnedEnemy);
                        }
                        // Pass waypoints to roamers
                        if (enemy.IsRoamer && spawnedEnemy is Enemies.BasicEnemy3D basicEnemy && _roamerWaypoints?.Count > 1)
                        {
                            basicEnemy.SetRoamWaypoints(_roamerWaypoints);
                        }
                    }
                    _pendingEnemies.RemoveAt(i);
                }
            }
        }

        // Check pending groups
        if (_pendingGroups != null)
        {
            for (int i = _pendingGroups.Count - 1; i >= 0 && spawnedThisFrame < _maxEnemiesPerFrame; i--)
            {
                var (groupCenter, monsters, roomId) = _pendingGroups[i];
                float distance = groupCenter.DistanceTo(playerPos);

                if (distance <= _deferredEnemyRadius)
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
    /// Progressively builds tiles as the player explores large maps.
    /// </summary>
    private void UpdateDeferredTileBuilding(Vector3 playerPos)
    {
        if (!_useDeferredTileBuilding || _builtTiles == null || _mapData == null) return;

        // Only update if player has moved significantly
        float distanceMoved = playerPos.DistanceTo(_lastTileBuildPosition);
        if (distanceMoved < TileSize * 5) return;  // Wait until moved 5 tiles

        _lastTileBuildPosition = playerPos;

        int playerTileX = (int)(playerPos.X / TileSize);
        int playerTileZ = (int)(playerPos.Z / TileSize);
        int tileRadius = (int)(_deferredTileRadius / TileSize);

        int tilesBuiltThisFrame = 0;

        // Build tiles in a spiral pattern from player position
        for (int radius = 1; radius <= tileRadius && tilesBuiltThisFrame < _maxTilesPerFrame; radius++)
        {
            for (int dx = -radius; dx <= radius && tilesBuiltThisFrame < _maxTilesPerFrame; dx++)
            {
                for (int dz = -radius; dz <= radius && tilesBuiltThisFrame < _maxTilesPerFrame; dz++)
                {
                    // Only process the outer ring of this radius
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;

                    int x = playerTileX + dx;
                    int z = playerTileZ + dz;

                    // Bounds check
                    if (x < 0 || x >= DungeonWidth || z < 0 || z >= DungeonDepth) continue;

                    // Skip if already built or not a floor tile
                    if (_builtTiles.Contains((x, z))) continue;
                    if (_mapData[x, 0, z] != 1) continue;

                    BuildTileAt(x, z);
                    tilesBuiltThisFrame++;
                }
            }
        }

        if (tilesBuiltThisFrame > 0)
        {
            GD.Print($"[DungeonGenerator3D] Deferred tile build: {tilesBuiltThisFrame} tiles, {_builtTiles.Count} total");
        }
    }

    /// <summary>
    /// Spawns a single enemy from placement data.
    /// </summary>
    private Node3D? SpawnEnemyFromPlacement(string type, Vector3 position, int level, bool isBoss, float rotationY = 0f, bool isRoamer = false)
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

        // Configure roamer behavior if applicable
        if (isRoamer && enemy is Enemies.BasicEnemy3D basicEnemy)
        {
            basicEnemy.SetRoamer(true);
        }

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
    /// Uses deferred spawning for large prop counts.
    /// </summary>
    private void SpawnPropsFromDefinition(MapDefinition map)
    {
        if (_propContainer == null || map.PlacedProps == null || map.PlacedProps.Count == 0)
        {
            return;
        }

        // For large prop counts, use deferred spawning
        bool useDeferredProps = map.PlacedProps.Count > 50;
        if (useDeferredProps)
        {
            _pendingProps = new List<PropPlacement>(map.PlacedProps);
            _spawnedPropIds = new HashSet<int>();

            // Get spawn position for initial radius check
            Vector3 spawnPos = GetSpawnPosition();

            int spawnedCount = 0;
            for (int i = _pendingProps.Count - 1; i >= 0; i--)
            {
                var propData = _pendingProps[i];
                var position = new Vector3(propData.X, propData.Y, propData.Z);
                float distance = position.DistanceTo(spawnPos);

                if (distance <= _deferredPropRadius)
                {
                    SpawnSingleProp(propData, i);
                    _pendingProps.RemoveAt(i);
                    spawnedCount++;
                }
            }

            GD.Print($"[DungeonGenerator3D] Spawned {spawnedCount} props near spawn, {_pendingProps.Count} deferred for later");
            return;
        }

        // Small prop count - spawn all immediately
        int totalSpawned = 0;
        for (int i = 0; i < map.PlacedProps.Count; i++)
        {
            var propData = map.PlacedProps[i];
            SpawnSingleProp(propData, i);
            totalSpawned++;
        }

        GD.Print($"[DungeonGenerator3D] Spawned {totalSpawned} placed props from map definition");
    }

    /// <summary>
    /// Spawns NPCs from the map definition's Npcs data.
    /// </summary>
    private void SpawnNPCsFromDefinition(MapDefinition map)
    {
        if (map.Npcs == null || map.Npcs.Count == 0)
        {
            return;
        }

        // Create NPC container if it doesn't exist
        Node3D? npcContainer = GetTree().Root.FindChild("NPCs", true, false) as Node3D;
        if (npcContainer == null)
        {
            npcContainer = new Node3D();
            npcContainer.Name = "NPCs";
            AddChild(npcContainer);
        }

        int totalSpawned = 0;
        foreach (var npcData in map.Npcs)
        {
            var position = new Vector3(npcData.X, 0, npcData.Z);

            if (npcData.Type == "bopca")
            {
                var bopca = new SafeRoom3D.NPC.Bopca3D();
                npcContainer.AddChild(bopca);  // Add to tree FIRST
                bopca.GlobalPosition = position;  // Then set position
                bopca.Rotation = new Vector3(0, npcData.RotationY, 0);
                totalSpawned++;
            }
            else if (npcData.Type == "mordecai")
            {
                var mordecai = new SafeRoom3D.NPC.Mordecai3D();
                npcContainer.AddChild(mordecai);  // Add to tree FIRST
                mordecai.GlobalPosition = position;  // Then set position
                mordecai.Rotation = new Vector3(0, npcData.RotationY, 0);
                totalSpawned++;
            }
            else if (npcData.Type == "crawler_rex")
            {
                var npc = new SafeRoom3D.NPC.CrawlerRex();
                npcContainer.AddChild(npc);  // Add to tree FIRST
                npc.GlobalPosition = position;  // Then set position
                npc.Rotation = new Vector3(0, npcData.RotationY, 0);
                totalSpawned++;
            }
            else if (npcData.Type == "crawler_lily")
            {
                var npc = new SafeRoom3D.NPC.CrawlerLily();
                npcContainer.AddChild(npc);  // Add to tree FIRST
                npc.GlobalPosition = position;  // Then set position
                npc.Rotation = new Vector3(0, npcData.RotationY, 0);
                totalSpawned++;
            }
            else if (npcData.Type == "crawler_chad")
            {
                var npc = new SafeRoom3D.NPC.CrawlerChad();
                npcContainer.AddChild(npc);  // Add to tree FIRST
                npc.GlobalPosition = position;  // Then set position
                npc.Rotation = new Vector3(0, npcData.RotationY, 0);
                totalSpawned++;
            }
            else if (npcData.Type == "crawler_shade")
            {
                var npc = new SafeRoom3D.NPC.CrawlerShade();
                npcContainer.AddChild(npc);  // Add to tree FIRST
                npc.GlobalPosition = position;  // Then set position
                npc.Rotation = new Vector3(0, npcData.RotationY, 0);
                totalSpawned++;
            }
            else if (npcData.Type == "crawler_hank")
            {
                var npc = new SafeRoom3D.NPC.CrawlerHank();
                npcContainer.AddChild(npc);  // Add to tree FIRST
                npc.GlobalPosition = position;  // Then set position
                npc.Rotation = new Vector3(0, npcData.RotationY, 0);
                totalSpawned++;
            }
            // Steve is spawned by GameManager separately as a singleton
        }

        GD.Print($"[DungeonGenerator3D] Spawned {totalSpawned} NPCs from map definition");
    }

    /// <summary>
    /// Spawns a single prop from placement data.
    /// </summary>
    private void SpawnSingleProp(PropPlacement propData, int propId)
    {
        if (_propContainer == null) return;

        // Track spawned props to avoid duplicates
        if (_spawnedPropIds != null && _spawnedPropIds.Contains(propId)) return;
        _spawnedPropIds?.Add(propId);

        var position = new Vector3(propData.X, propData.Y, propData.Z);
        Node3D? prop = null;

        // Check for GLB model override first
        string propType = propData.Type.ToLower();
        if (GlbModelConfig.TryGetPropGlbPath(propType, out string? glbPath) && !string.IsNullOrWhiteSpace(glbPath))
        {
            var glbModel = GlbModelConfig.LoadGlbModel(glbPath, propData.Scale);
            if (glbModel != null)
            {
                // Apply Y offset to fix models with origin at center instead of feet
                float yOffset = GlbModelConfig.GetPropYOffset(propType);
                if (yOffset != 0f)
                {
                    glbModel.Position = new Vector3(0, yOffset, 0);
                }

                prop = glbModel;
                GD.Print($"[MapBuilder] Loaded GLB model for prop {propData.Type}: {glbPath}, YOffset: {yOffset}");
            }
            else
            {
                GD.PrintErr($"[MapBuilder] Failed to load GLB for prop {propData.Type}, falling back to procedural");
            }
        }

        // Fall back to procedural mesh if no GLB or GLB failed
        if (prop == null)
        {
            prop = SafeRoom3D.Environment.Cosmetic3D.Create(
                propData.Type,
                new Color(0.6f, 0.4f, 0.3f),  // Default primary color
                new Color(0.4f, 0.3f, 0.2f),  // Default secondary color
                new Color(0.5f, 0.5f, 0.5f),  // Default accent color
                0f,
                propData.Scale,
                true  // Enable light
            );
        }

        if (prop != null)
        {
            prop.Position = position;
            prop.Rotation = new Vector3(0, propData.RotationY, 0);
            _propContainer.AddChild(prop);
        }
    }

    /// <summary>
    /// Updates deferred prop spawning based on player position.
    /// Called from UpdateDeferredTileBuilding.
    /// </summary>
    private void UpdateDeferredPropSpawning(Vector3 playerPos)
    {
        if (_pendingProps == null || _pendingProps.Count == 0) return;

        int spawnedThisFrame = 0;

        for (int i = _pendingProps.Count - 1; i >= 0 && spawnedThisFrame < _maxPropsPerFrame; i--)
        {
            var propData = _pendingProps[i];
            var position = new Vector3(propData.X, propData.Y, propData.Z);
            float distance = position.DistanceTo(playerPos);

            if (distance <= _deferredPropRadius)
            {
                SpawnSingleProp(propData, i);
                _pendingProps.RemoveAt(i);
                spawnedThisFrame++;
            }
        }

        if (spawnedThisFrame > 0)
        {
            GD.Print($"[DungeonGenerator3D] Deferred prop spawn: {spawnedThisFrame} props, {_pendingProps.Count} remaining");
        }
    }
}
