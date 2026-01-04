---
name: dungeon-architect
description: Expert dungeon level designer for Godot 4.3 C#. Creates procedural room layouts, corridor systems, spawn logic, prop placement, and atmospheric lighting. Specializes in dungeon crawlers with performance-optimized chunked generation.
---

# Dungeon Architect - Procedural Level Design

Expert at creating procedural dungeon layouts with rooms, corridors, spawn systems, and atmospheric elements using Godot 4.3.

---

## Core Data Structures

### Room Definition
```csharp
public class Room
{
    public Vector3I Position;     // Grid position (x, y, z) - y is floor level
    public Vector3I Size;         // Room dimensions in tiles
    public string Type = "";      // "crypt", "cathedral", "storage", etc.
    public bool IsCleared;
    public List<Node3D> Props = new();
    public List<Node3D> Enemies = new();
}
```

### Map Data (3D Grid)
```csharp
private int[,,] _mapData;  // 0=void, 1=floor, 2=wall, 3=door
```

---

## Room Types & Prop Distribution

| Room Type | Props | Lighting | Enemy Density |
|-----------|-------|----------|---------------|
| storage | Barrels, crates, pots | Dim, few torches | Low |
| library | Bookshelves, tables, pots | Moderate, candles | Low |
| armory | Chests, barrels, crates | Moderate | Medium |
| cathedral | Pillars, statues, torches, cauldrons | Bright, dramatic | Medium |
| crypt | Pots, chests, crystals, statues | Dark, eerie | High |
| barracks | Beds, tables, weapon racks | Moderate | High (goblin clusters) |
| throne | Throne, pillars, braziers | Dramatic | Boss + guards |

---

## Generation Parameters

### Dungeon Configuration
```csharp
[Export] public int DungeonWidth { get; set; } = 80;
[Export] public int DungeonDepth { get; set; } = 80;
[Export] public int MinRooms { get; set; } = 5;
[Export] public int MaxRooms { get; set; } = 8;
[Export] public int MinRoomSize { get; set; } = 8;
[Export] public int MaxRoomSize { get; set; } = 16;
[Export] public int CorridorWidth { get; set; } = 5;
[Export] public float TileSize { get; set; } = 1f;
[Export] public float WallHeight { get; set; } = 9f;
```

### Large Dungeon Mode
```csharp
LargeDungeonWidth = 400   // tiles
LargeDungeonDepth = 400   // tiles
LargeDungeonRooms = 50    // rooms
ChunkSize = 50            // tiles per chunk
```

---

## Room Generation Algorithm

### Phase 1: Room Placement
```csharp
for (int attempt = 0; attempt < MaxAttempts; attempt++)
{
    // Random room size
    int width = _rng.RandiRange(MinRoomSize, MaxRoomSize);
    int depth = _rng.RandiRange(MinRoomSize, MaxRoomSize);

    // Random position (with margin from edges)
    int x = _rng.RandiRange(2, DungeonWidth - width - 2);
    int z = _rng.RandiRange(2, DungeonDepth - depth - 2);

    // Check overlap with existing rooms (include buffer zone)
    if (!RoomOverlaps(x, z, width, depth, buffer: 3))
    {
        var room = new Room { Position = new Vector3I(x, 0, z), Size = new Vector3I(width, 1, depth) };
        Rooms.Add(room);
        CarveRoom(room);
    }
}
```

### Phase 2: Corridor Connection (L-Shaped)
```csharp
void ConnectRooms(Room from, Room to)
{
    Vector3I start = GetRoomCenter(from);
    Vector3I end = GetRoomCenter(to);

    // L-shaped corridor: horizontal then vertical
    // First segment: X direction
    CarveCorridor(start.X, start.Z, end.X, start.Z, CorridorWidth);

    // Second segment: Z direction
    CarveCorridor(end.X, start.Z, end.X, end.Z, CorridorWidth);
}
```

### Phase 3: Wall Generation
```csharp
void GenerateWalls()
{
    for (int x = 0; x < Width; x++)
    {
        for (int z = 0; z < Depth; z++)
        {
            if (_mapData[x, 0, z] == 1) // Floor tile
            {
                // Check 4 neighbors for void
                if (IsVoid(x-1, z)) CreateWall(x, z, Vector3.Left);
                if (IsVoid(x+1, z)) CreateWall(x, z, Vector3.Right);
                if (IsVoid(x, z-1)) CreateWall(x, z, Vector3.Back);
                if (IsVoid(x, z+1)) CreateWall(x, z, Vector3.Forward);
            }
        }
    }
}
```

---

## Spawn System

### Spawn Room Selection
```csharp
// First room is always spawn (safe)
_spawnRoom = Rooms[0];
_spawnRoom.Type = "spawn";

// Player spawns at room center
Vector3 spawnPos = new Vector3(
    (_spawnRoom.Position.X + _spawnRoom.Size.X / 2f) * TileSize,
    0,
    (_spawnRoom.Position.Z + _spawnRoom.Size.Z / 2f) * TileSize
);
```

### Enemy Spawning Rules
```csharp
void SpawnEnemiesInRoom(Room room)
{
    // Skip spawn room
    if (room == _spawnRoom) return;

    // 4-5 enemies per room (1 boss + 3-4 regular)
    int enemyCount = _rng.RandiRange(4, 5);

    // Calculate level based on distance from spawn
    float distance = room.Position.DistanceTo(_spawnRoom.Position);
    int level = Mathf.Clamp((int)(distance / 20f) + 1, 1, 10);

    // Spawn boss first
    SpawnBoss(room, level);

    // Spawn regular enemies
    for (int i = 1; i < enemyCount; i++)
    {
        SpawnEnemy(room, level, GetRandomMonsterType());
    }
}
```

### Monster Level Scaling
```csharp
// HP: BaseHP × (1 + (Level-1) × 0.15)
float hpMultiplier = 1f + (level - 1) * 0.15f;

// Damage: BaseDamage × (1 + (Level-1) × 0.10)
float damageMultiplier = 1f + (level - 1) * 0.10f;

// XP: BaseHealth × (1 + Level × 0.5) / 10
int xpReward = (int)(baseHealth * (1f + level * 0.5f) / 10f);
```

---

## Prop Placement

### Room-Based Prop Spawning
```csharp
void PlacePropsInRoom(Room room)
{
    string[] propTypes = GetPropsForRoomType(room.Type);
    int propCount = _rng.RandiRange(3, 8);

    for (int i = 0; i < propCount; i++)
    {
        // Random position within room (with margin)
        float x = _rng.RandfRange(room.Position.X + 1, room.Position.X + room.Size.X - 1);
        float z = _rng.RandfRange(room.Position.Z + 1, room.Position.Z + room.Size.Z - 1);

        // Avoid center (player path) and near doors
        if (IsNearCenter(x, z, room) || IsNearDoor(x, z)) continue;

        SpawnProp(propTypes[_rng.RandiRange(0, propTypes.Length - 1)], new Vector3(x, 0, z));
    }
}
```

### Wall Prop Placement (Torches)
```csharp
void PlaceWallProps(Room room)
{
    // Place torches along walls at regular intervals
    float spacing = 6f; // tiles between torches

    foreach (Vector3I wallPos in GetWallPositions(room))
    {
        if (wallPos.X % spacing < 1f)
        {
            SpawnTorch(wallPos, GetWallNormal(wallPos));
        }
    }
}
```

---

## Lighting System

### Light Configuration
```csharp
const int MaxShadowLights = 4;  // Performance limit
float TorchRange = 8f;
float TorchEnergy = 2f;
float TorchFlickerSpeed = 8f;
float TorchFlickerAmount = 0.15f;
```

### Optimized Light Creation
```csharp
OmniLight3D CreateOptimizedLight(Vector3 position, Color color, float energy, float range, bool shadow)
{
    var light = new OmniLight3D();
    light.Position = position;
    light.LightColor = color;
    light.LightEnergy = energy;
    light.OmniRange = range;
    light.OmniAttenuation = 1.5f;

    // Shadow budget
    if (shadow && _shadowLightCount < MaxShadowLights)
    {
        light.ShadowEnabled = true;
        _shadowLightCount++;
    }

    // Distance fade for performance
    light.DistanceFadeEnabled = true;
    light.DistanceFadeBegin = range * 3f;
    light.DistanceFadeLength = range * 2f;

    return light;
}
```

---

## Occlusion Culling (CRITICAL)

### Adding Occluders
```csharp
void AddBoxOccluder(Vector3 position, Vector3 size)
{
    // Generate box vertices
    Vector3 half = size / 2f;
    int baseIndex = _occluderVertices.Count;

    // 8 corners
    _occluderVertices.Add(position + new Vector3(-half.X, -half.Y, -half.Z));
    _occluderVertices.Add(position + new Vector3(half.X, -half.Y, -half.Z));
    // ... (all 8 corners)

    // 6 faces (12 triangles)
    AddQuadIndices(baseIndex, 0, 1, 2, 3); // Front
    // ... (all 6 faces)
}
```

### Building Occluder
```csharp
void BuildOccluder()
{
    var occluder = new ArrayOccluder3D();
    occluder.Vertices = _occluderVertices.ToArray();
    occluder.Indices = _occluderIndices.ToArray();

    _occluderInstance = new OccluderInstance3D();
    _occluderInstance.Occluder = occluder;
    AddChild(_occluderInstance);
}
```

---

## Chunked Generation (Large Dungeons)

### Chunk-Based Building
```csharp
void BuildGeometryChunked()
{
    int chunkSize = 50;

    for (int chunkX = 0; chunkX < Width; chunkX += chunkSize)
    {
        for (int chunkZ = 0; chunkZ < Depth; chunkZ += chunkSize)
        {
            // Build floor/walls for this chunk
            BuildChunk(chunkX, chunkZ, chunkSize);

            // Yield to prevent frame drops
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
```

---

## Procedural Textures

### Stone Floor Pattern
```csharp
// Create tile pattern with mortar lines
int tileSize = 64;
bool onMortar = (x % tileSize < 3) || (y % tileSize < 3);

if (onMortar)
    pixelColor = mortarColor;  // Darker grooves
else
    pixelColor = baseColor + noise;  // Stone surface
```

### Uneven Floor Mesh
```csharp
float height = 0f;

// Grade variation (gentle slopes)
height += Mathf.Sin(worldX * 0.1f) * Mathf.Cos(worldZ * 0.08f) * 0.05f;

// Stone blocks raised from mortar
if (!inMortar) height += 0.03f;

// Shallow trenches/cracks
if (Mathf.Abs(crackPattern) < 0.1f) height -= 0.04f;
```

---

## Performance Rules

| Rule | Why | Implementation |
|------|-----|----------------|
| Max 4 shadow lights | GPU cost | Track `_shadowLightCount` |
| Distance fade on lights | Culling | `DistanceFadeEnabled = true` |
| Chunked generation | Frame budget | 50x50 tile chunks |
| Occluder for all walls | CPU culling | `AddBoxOccluder()` |
| Shared materials | Draw calls | Static material instances |

---

## Signals

```csharp
[Signal] public delegate void GenerationCompleteEventHandler();
[Signal] public delegate void RoomEnteredEventHandler(string roomType);
```

---

## Common Mistakes to AVOID

| Mistake | Problem | Fix |
|---------|---------|-----|
| No spawn room buffer | Enemies attack immediately | Skip 1-2 rooms near spawn |
| Overlapping rooms | Invalid geometry | Check overlap + buffer zone |
| Corridor dead ends | Inaccessible areas | Connect all rooms in sequence |
| Too many shadow lights | FPS drop | Cap at 4, use distance fade |
| No occluders | Render behind walls | Call `AddBoxOccluder()` for walls |
| Props blocking paths | Player stuck | Margin from room center/doors |

---

## Workflow

1. **Configure Parameters** - Set dungeon size, room counts, corridor width
2. **Generate Rooms** - Random placement with overlap check
3. **Connect Corridors** - L-shaped paths between rooms
4. **Carve Map Data** - Mark floor/wall/door in 3D grid
5. **Build Geometry** - Create mesh instances (chunked for large)
6. **Add Occluders** - Register walls for culling
7. **Place Props** - Room-type based distribution
8. **Spawn Enemies** - Level-scaled, boss + regular mix
9. **Add Lighting** - Torches with shadow budget
10. **Emit Complete** - Signal generation done
