using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SafeRoom3D.Tests;

/// <summary>
/// Tests for MapDefinition JSON serialization.
/// Uses standalone data classes that mirror the game's MapDefinition structure.
/// </summary>
public class MapDefinitionTests
{
    #region Test Data Classes (mirror game code)

    public class MapDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "New Map";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("width")]
        public int Width { get; set; } = 50;

        [JsonPropertyName("depth")]
        public int Depth { get; set; } = 50;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "room";

        [JsonPropertyName("tileData")]
        public string? TileData { get; set; }

        [JsonPropertyName("spawnRoom")]
        public int SpawnRoom { get; set; } = 0;

        [JsonPropertyName("spawnPosition")]
        public PositionData? SpawnPosition { get; set; }

        [JsonPropertyName("useProceduralProps")]
        public bool UseProceduralProps { get; set; } = true;

        [JsonPropertyName("rooms")]
        public List<RoomDefinition> Rooms { get; set; } = new();

        [JsonPropertyName("corridors")]
        public List<CorridorDefinition> Corridors { get; set; } = new();

        [JsonPropertyName("enemies")]
        public List<EnemyPlacement> Enemies { get; set; } = new();

        [JsonPropertyName("monsterGroups")]
        public List<MonsterGroupPlacement> MonsterGroups { get; set; } = new();

        [JsonPropertyName("placedProps")]
        public List<PropPlacement> PlacedProps { get; set; } = new();

        [JsonPropertyName("customTiles")]
        public List<CustomTile> CustomTiles { get; set; } = new();

        public bool IsTileMode => Mode == "tile";
    }

    public class PositionData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("z")]
        public int Z { get; set; }
    }

    public class RoomDefinition
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "normal";

        [JsonPropertyName("position")]
        public PositionData Position { get; set; } = new();

        [JsonPropertyName("size")]
        public PositionData Size { get; set; } = new();

        [JsonPropertyName("isSpawnRoom")]
        public bool IsSpawnRoom { get; set; }
    }

    public class CorridorDefinition
    {
        [JsonPropertyName("fromRoom")]
        public int FromRoom { get; set; }

        [JsonPropertyName("toRoom")]
        public int ToRoom { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; } = 3;
    }

    public class EnemyPlacement
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "goblin";

        [JsonPropertyName("roomId")]
        public int RoomId { get; set; } = -1;

        [JsonPropertyName("position")]
        public PositionData Position { get; set; } = new();

        [JsonPropertyName("level")]
        public int Level { get; set; } = 1;

        [JsonPropertyName("isBoss")]
        public bool IsBoss { get; set; } = false;

        [JsonPropertyName("rotationY")]
        public float RotationY { get; set; } = 0f;
    }

    public class MonsterGroupPlacement
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("center")]
        public PositionData Center { get; set; } = new();

        [JsonPropertyName("roomId")]
        public int RoomId { get; set; } = -1;

        [JsonPropertyName("monsters")]
        public List<GroupedMonster> Monsters { get; set; } = new();
    }

    public class GroupedMonster
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
        public bool IsBoss { get; set; }
    }

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

    public class CustomTile
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("z")]
        public int Z { get; set; }

        [JsonPropertyName("isFloor")]
        public bool IsFloor { get; set; }
    }

    #endregion

    #region Tests

    [Fact]
    public void Serialize_EmptyMap_ProducesValidJson()
    {
        // Arrange
        var map = new MapDefinition
        {
            Name = "Test Map",
            Width = 30,
            Depth = 30
        };

        // Act
        string json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"Test Map\"", json);
        Assert.Contains("\"width\"", json);
        Assert.Contains("30", json);
    }

    [Fact]
    public void RoundTrip_EmptyMap_PreservesData()
    {
        // Arrange
        var original = new MapDefinition
        {
            Name = "Test Map",
            Author = "Test Author",
            Width = 40,
            Depth = 60,
            Mode = "tile"
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Author, deserialized.Author);
        Assert.Equal(original.Width, deserialized.Width);
        Assert.Equal(original.Depth, deserialized.Depth);
        Assert.Equal(original.Mode, deserialized.Mode);
        Assert.True(deserialized.IsTileMode);
    }

    [Fact]
    public void RoundTrip_WithRooms_PreservesData()
    {
        // Arrange
        var original = new MapDefinition
        {
            Name = "Room Test",
            Rooms = new List<RoomDefinition>
            {
                new RoomDefinition
                {
                    Id = 0,
                    Type = "spawn",
                    Position = new PositionData { X = 5, Z = 5 },
                    Size = new PositionData { X = 10, Z = 10 },
                    IsSpawnRoom = true
                },
                new RoomDefinition
                {
                    Id = 1,
                    Type = "storage",
                    Position = new PositionData { X = 20, Z = 20 },
                    Size = new PositionData { X = 8, Z = 8 }
                }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Rooms.Count);
        Assert.Equal("spawn", deserialized.Rooms[0].Type);
        Assert.True(deserialized.Rooms[0].IsSpawnRoom);
        Assert.Equal(5, deserialized.Rooms[0].Position.X);
        Assert.Equal(10, deserialized.Rooms[0].Size.X);
    }

    [Fact]
    public void RoundTrip_WithCorridors_PreservesData()
    {
        // Arrange
        var original = new MapDefinition
        {
            Corridors = new List<CorridorDefinition>
            {
                new CorridorDefinition { FromRoom = 0, ToRoom = 1, Width = 3 },
                new CorridorDefinition { FromRoom = 1, ToRoom = 2, Width = 5 }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Corridors.Count);
        Assert.Equal(0, deserialized.Corridors[0].FromRoom);
        Assert.Equal(1, deserialized.Corridors[0].ToRoom);
        Assert.Equal(5, deserialized.Corridors[1].Width);
    }

    [Fact]
    public void RoundTrip_WithEnemies_PreservesRotation()
    {
        // Arrange
        float expectedRotation = 1.5708f; // ~90 degrees in radians
        var original = new MapDefinition
        {
            Enemies = new List<EnemyPlacement>
            {
                new EnemyPlacement
                {
                    Type = "goblin",
                    Position = new PositionData { X = 10, Z = 15 },
                    Level = 3,
                    RotationY = expectedRotation
                },
                new EnemyPlacement
                {
                    Type = "skeleton_lord",
                    Position = new PositionData { X = 25, Z = 25 },
                    IsBoss = true,
                    RotationY = 3.14159f
                }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Enemies.Count);
        Assert.Equal("goblin", deserialized.Enemies[0].Type);
        Assert.Equal(expectedRotation, deserialized.Enemies[0].RotationY, 4);
        Assert.True(deserialized.Enemies[1].IsBoss);
        Assert.Equal(3.14159f, deserialized.Enemies[1].RotationY, 4);
    }

    [Fact]
    public void RoundTrip_WithProps_PreservesRotationAndScale()
    {
        // Arrange
        var original = new MapDefinition
        {
            PlacedProps = new List<PropPlacement>
            {
                new PropPlacement
                {
                    Type = "barrel",
                    X = 10.5f,
                    Y = 0f,
                    Z = 15.3f,
                    RotationY = 0.785f, // 45 degrees
                    Scale = 1.5f
                },
                new PropPlacement
                {
                    Type = "torch",
                    X = 20f,
                    Y = 2f,
                    Z = 25f,
                    RotationY = 1.57f,
                    Scale = 0.8f
                }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.PlacedProps.Count);

        var barrel = deserialized.PlacedProps[0];
        Assert.Equal("barrel", barrel.Type);
        Assert.Equal(10.5f, barrel.X, 2);
        Assert.Equal(0.785f, barrel.RotationY, 3);
        Assert.Equal(1.5f, barrel.Scale, 2);

        var torch = deserialized.PlacedProps[1];
        Assert.Equal("torch", torch.Type);
        Assert.Equal(2f, torch.Y, 2);
        Assert.Equal(0.8f, torch.Scale, 2);
    }

    [Fact]
    public void RoundTrip_WithMonsterGroups_PreservesData()
    {
        // Arrange
        var original = new MapDefinition
        {
            MonsterGroups = new List<MonsterGroupPlacement>
            {
                new MonsterGroupPlacement
                {
                    Id = "group1",
                    Center = new PositionData { X = 15, Z = 15 },
                    RoomId = 0,
                    Monsters = new List<GroupedMonster>
                    {
                        new GroupedMonster { Type = "goblin", OffsetX = -2f, OffsetZ = 0f },
                        new GroupedMonster { Type = "goblin", OffsetX = 2f, OffsetZ = 0f },
                        new GroupedMonster { Type = "goblin_shaman", OffsetX = 0f, OffsetZ = 2f, Level = 2 }
                    }
                }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Single(deserialized.MonsterGroups);
        Assert.Equal(3, deserialized.MonsterGroups[0].Monsters.Count);
        Assert.Equal("goblin_shaman", deserialized.MonsterGroups[0].Monsters[2].Type);
        Assert.Equal(2, deserialized.MonsterGroups[0].Monsters[2].Level);
    }

    [Fact]
    public void RoundTrip_WithSpawnPosition_PreservesData()
    {
        // Arrange
        var original = new MapDefinition
        {
            SpawnPosition = new PositionData { X = 25, Z = 30 },
            SpawnRoom = 2
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.SpawnPosition);
        Assert.Equal(25, deserialized.SpawnPosition.X);
        Assert.Equal(30, deserialized.SpawnPosition.Z);
        Assert.Equal(2, deserialized.SpawnRoom);
    }

    [Fact]
    public void RoundTrip_WithCustomTiles_PreservesData()
    {
        // Arrange
        var original = new MapDefinition
        {
            Mode = "room",
            CustomTiles = new List<CustomTile>
            {
                new CustomTile { X = 5, Z = 5, IsFloor = true },
                new CustomTile { X = 6, Z = 5, IsFloor = true },
                new CustomTile { X = 10, Z = 10, IsFloor = false }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.CustomTiles.Count);
        Assert.True(deserialized.CustomTiles[0].IsFloor);
        Assert.False(deserialized.CustomTiles[2].IsFloor);
    }

    [Fact]
    public void RoundTrip_CompleteMap_AllDataPreserved()
    {
        // Arrange - Complete map with all features
        var original = new MapDefinition
        {
            Name = "Complete Test Map",
            Author = "Unit Test",
            Description = "A map with all features",
            Version = 2,
            Width = 100,
            Depth = 100,
            Mode = "tile",
            TileData = "H4sIAAAAAAAA...", // Fake encoded data
            UseProceduralProps = false,
            SpawnPosition = new PositionData { X = 50, Z = 50 },
            Rooms = new List<RoomDefinition>
            {
                new RoomDefinition { Id = 0, Type = "spawn", IsSpawnRoom = true }
            },
            Enemies = new List<EnemyPlacement>
            {
                new EnemyPlacement { Type = "dragon", IsBoss = true, RotationY = 1.57f }
            },
            PlacedProps = new List<PropPlacement>
            {
                new PropPlacement { Type = "crystal", X = 50, Z = 50, RotationY = 0.5f }
            }
        };

        // Act
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<MapDefinition>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.True(deserialized.IsTileMode);
        Assert.False(deserialized.UseProceduralProps);
        Assert.Single(deserialized.Rooms);
        Assert.Single(deserialized.Enemies);
        Assert.Equal(1.57f, deserialized.Enemies[0].RotationY, 2);
        Assert.Single(deserialized.PlacedProps);
    }

    #endregion
}
