using Godot;
using System.Collections.Generic;
using SafeRoom3D.Environment;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.Core;

/// <summary>
/// 3D dungeon generator. Creates rooms, corridors, and populates with props.
/// Ported from 2D TileMap system to 3D GridMap/MeshInstance approach.
/// </summary>
public partial class DungeonGenerator3D : Node3D
{
    // Room data structure
    public class Room
    {
        public Vector3I Position;     // Grid position (x, y, z) - y is floor level
        public Vector3I Size;         // Room dimensions in tiles
        public string Type = "";      // "crypt", "cathedral", "storage", "library"
        public bool IsCleared;
        public List<Node3D> Props = new();
        public List<Node3D> Enemies = new();
    }

    // Map data
    private int[,,]? _mapData;         // 0=void, 1=floor, 2=wall, 3=door
    public List<Room> Rooms { get; private set; } = new();
    private Room? _spawnRoom;

    // Generated geometry
    private Node3D? _floorContainer;
    private Node3D? _wallContainer;
    private Node3D? _propContainer;
    private Node3D? _lightContainer;
    private Node3D? _enemyContainer;
    private OccluderInstance3D? _occluderInstance;
    private List<Vector3> _occluderVertices = new();
    private List<int> _occluderIndices = new();

    // Dragon gate and Badlama area
    private Node3D? _dragonGate;
    private StaticBody3D? _dragonGateCollision;
    private BossEnemy3D? _dragonBoss;
    private bool _dragonGateOpen = false;
    private Vector3 _dragonRoomCenter;
    private float _dragonRoomSize;

    // Materials
    private StandardMaterial3D? _floorMaterial;
    private StandardMaterial3D? _wallMaterial;
    private StandardMaterial3D? _ceilingMaterial;
    private StandardMaterial3D? _trimMaterial; // Baseboard/crown molding

    // Configuration - made larger for better gameplay
    [Export] public int DungeonWidth { get; set; } = 80;
    [Export] public int DungeonDepth { get; set; } = 80;
    [Export] public int MinRooms { get; set; } = 5;
    [Export] public int MaxRooms { get; set; } = 8;
    [Export] public int MinRoomSize { get; set; } = 8;
    [Export] public int MaxRoomSize { get; set; } = 16;
    [Export] public int CorridorWidth { get; set; } = 5;  // Much wider corridors
    [Export] public float TileSize { get; set; } = 1f;
    [Export] public float WallHeight { get; set; } = 9f;  // Tripled from 3 to 9

    // Random state
    private RandomNumberGenerator _rng = new();
    public ulong Seed { get; set; }

    // Performance optimization - limit shadow-casting lights
    private int _shadowLightCount;
    private const int MaxShadowLights = 4; // Reduced from 8 for better performance

    // Signals
    [Signal] public delegate void GenerationCompleteEventHandler();
    [Signal] public delegate void RoomEnteredEventHandler(string roomType);

    /// <summary>
    /// Creates an optimized OmniLight3D with distance fade for performance.
    /// </summary>
    private OmniLight3D CreateOptimizedLight(Vector3 position, Color color, float energy, float range, bool enableShadow = false)
    {
        var light = new OmniLight3D();
        light.Position = position;
        light.LightColor = color;
        light.LightEnergy = energy;
        light.OmniRange = range;
        light.OmniAttenuation = 1.5f;

        // Shadow management
        if (enableShadow && _shadowLightCount < MaxShadowLights)
        {
            light.ShadowEnabled = true;
            _shadowLightCount++;
        }
        else
        {
            light.ShadowEnabled = false;
        }

        // PERFORMANCE: Distance fade for automatic culling
        light.DistanceFadeEnabled = true;
        light.DistanceFadeBegin = Constants.LightVisibilityEnd * 0.6f;
        light.DistanceFadeLength = Constants.LightVisibilityEnd * 0.4f;
        light.DistanceFadeShadow = Constants.LightVisibilityEnd * 0.4f;

        return light;
    }

    public override void _Ready()
    {
        // Initialize materials
        CreateMaterials();

        // Create containers for organization
        _floorContainer = new Node3D { Name = "Floors" };
        _wallContainer = new Node3D { Name = "Walls" };
        _propContainer = new Node3D { Name = "Props" };
        _lightContainer = new Node3D { Name = "Lights" };
        _enemyContainer = new Node3D { Name = "Enemies" };

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_propContainer);
        AddChild(_lightContainer);
        AddChild(_enemyContainer);
    }

    private void CreateMaterials()
    {
        // Floor: worn stone tiles - warm brown/gray tones like ancient dungeon flagstones
        _floorMaterial = new StandardMaterial3D();
        _floorMaterial.AlbedoColor = new Color(0.35f, 0.30f, 0.25f); // Warm brown-gray stone
        _floorMaterial.Roughness = 0.92f; // Very rough, matte stone - no shine
        _floorMaterial.Metallic = 0f; // Stone is NOT metallic
        _floorMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _floorMaterial.AlbedoTexture = CreateStoneFloorTexture();
        _floorMaterial.Uv1Scale = new Vector3(0.25f, 0.25f, 1f); // Tile the texture
        _floorMaterial.NormalEnabled = true;
        _floorMaterial.NormalTexture = CreateStoneFloorNormalMap();
        _floorMaterial.NormalScale = 1.8f; // Moderate depth - visible but not overdone

        // Walls: ancient brick/stone blocks - darker with moss undertones
        _wallMaterial = new StandardMaterial3D();
        _wallMaterial.AlbedoColor = new Color(0.38f, 0.33f, 0.28f); // Warm gray-brown brick
        _wallMaterial.Roughness = 0.88f; // Rough old brick
        _wallMaterial.Metallic = 0f; // Stone is NOT metallic
        _wallMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _wallMaterial.AlbedoTexture = CreateBrickWallTexture();
        _wallMaterial.Uv1Scale = new Vector3(0.5f, 0.15f, 1f); // Stretch for wall height
        _wallMaterial.NormalEnabled = true;
        _wallMaterial.NormalTexture = CreateBrickWallNormalMap();
        _wallMaterial.NormalScale = 2.0f; // Good brick depth

        // Ceiling: dark rough stone - like natural cave or ancient masonry
        _ceilingMaterial = new StandardMaterial3D();
        _ceilingMaterial.AlbedoColor = new Color(0.25f, 0.22f, 0.18f); // Dark warm brown
        _ceilingMaterial.Roughness = 0.95f; // Very rough ceiling
        _ceilingMaterial.Metallic = 0f; // Stone is NOT metallic
        _ceilingMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _ceilingMaterial.AlbedoTexture = CreateStoneCeilingTexture();
        _ceilingMaterial.Uv1Scale = new Vector3(0.25f, 0.25f, 1f);
        _ceilingMaterial.NormalEnabled = true;
        _ceilingMaterial.NormalTexture = CreateCeilingNormalMap();
        _ceilingMaterial.NormalScale = 1.5f; // Subtle cave-like texture

        // Trim/molding: darker accent for transitions between surfaces
        _trimMaterial = new StandardMaterial3D();
        _trimMaterial.AlbedoColor = new Color(0.18f, 0.15f, 0.12f); // Dark brown-gray
        _trimMaterial.Roughness = 0.75f; // Slightly smoother than walls
        _trimMaterial.Metallic = 0f;
        _trimMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
    }

    private ImageTexture CreateStoneFloorTexture()
    {
        int size = 256;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        // Create stone tile pattern - warm brown/tan flagstones
        int tileSize = 64;
        var baseColor = new Color(0.32f, 0.27f, 0.22f);    // Warm brown-tan stone
        var mortarColor = new Color(0.18f, 0.14f, 0.10f);  // Dark brown mortar

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Check if on tile edge (mortar line)
                bool onMortar = (x % tileSize < 3) || (y % tileSize < 3);

                // Add some noise for variation
                float noise = (float)GD.RandRange(-0.05, 0.05);

                Color pixelColor;
                if (onMortar)
                {
                    pixelColor = mortarColor;
                }
                else
                {
                    // Add subtle variation within tiles
                    float tileNoise = (float)GD.RandRange(-0.03, 0.03);
                    pixelColor = new Color(
                        Mathf.Clamp(baseColor.R + tileNoise + noise, 0, 1),
                        Mathf.Clamp(baseColor.G + tileNoise + noise, 0, 1),
                        Mathf.Clamp(baseColor.B + tileNoise + noise, 0, 1)
                    );
                }

                image.SetPixel(x, y, pixelColor);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    private ImageTexture CreateStoneFloorNormalMap()
    {
        int size = 256;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        int tileSize = 64;
        int mortarWidth = 3;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Check mortar positions
                bool onMortarX = (x % tileSize < mortarWidth);
                bool onMortarY = (y % tileSize < mortarWidth);
                bool nearMortarX = (x % tileSize < mortarWidth + 2) || (x % tileSize >= tileSize - 2);
                bool nearMortarY = (y % tileSize < mortarWidth + 2) || (y % tileSize >= tileSize - 2);

                Vector3 normal;

                if (onMortarX && onMortarY)
                {
                    // Corner groove - points diagonally up
                    normal = new Vector3(0.3f, 0.3f, 0.9f).Normalized();
                }
                else if (onMortarX)
                {
                    // Vertical mortar groove - normal tilts toward positive X
                    normal = new Vector3(0.4f, 0f, 0.9f).Normalized();
                }
                else if (onMortarY)
                {
                    // Horizontal mortar groove - normal tilts toward positive Y
                    normal = new Vector3(0f, 0.4f, 0.9f).Normalized();
                }
                else if (nearMortarX && !onMortarX)
                {
                    // Edge near vertical mortar - slight slope toward groove
                    float slopeDir = (x % tileSize < mortarWidth + 2) ? -0.15f : 0.15f;
                    normal = new Vector3(slopeDir, 0f, 0.99f).Normalized();
                }
                else if (nearMortarY && !onMortarY)
                {
                    // Edge near horizontal mortar - slight slope toward groove
                    float slopeDir = (y % tileSize < mortarWidth + 2) ? -0.15f : 0.15f;
                    normal = new Vector3(0f, slopeDir, 0.99f).Normalized();
                }
                else
                {
                    // Stone surface with subtle bumpy variation
                    float noiseX = (float)GD.RandRange(-0.08, 0.08);
                    float noiseY = (float)GD.RandRange(-0.08, 0.08);
                    normal = new Vector3(noiseX, noiseY, 1f).Normalized();
                }

                // Convert normal to color (normal map format: X*0.5+0.5, Y*0.5+0.5, Z*0.5+0.5)
                image.SetPixel(x, y, new Color(
                    normal.X * 0.5f + 0.5f,
                    normal.Y * 0.5f + 0.5f,
                    normal.Z * 0.5f + 0.5f
                ));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private ImageTexture CreateBrickWallTexture()
    {
        int size = 512; // Higher resolution for more detail
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        // Varied stone block sizes for ancient dungeon look
        int[] blockWidths = { 48, 64, 56, 40, 72 };
        int[] blockHeights = { 24, 32, 28, 20 };
        int mortarWidth = 4;

        // Color palette for varied stones
        var stoneColors = new Color[] {
            new Color(0.38f, 0.30f, 0.22f),  // Warm sandstone
            new Color(0.32f, 0.26f, 0.20f),  // Darker stone
            new Color(0.42f, 0.34f, 0.26f),  // Lighter stone
            new Color(0.35f, 0.28f, 0.22f),  // Medium brown
            new Color(0.28f, 0.24f, 0.20f),  // Dark weathered
            new Color(0.40f, 0.32f, 0.24f),  // Tan stone
        };
        var mortarColor = new Color(0.18f, 0.14f, 0.10f);
        var mossColor = new Color(0.15f, 0.22f, 0.12f);  // Dark green moss
        var wetColor = new Color(0.20f, 0.18f, 0.16f);   // Wet/damp stone
        var crackColor = new Color(0.10f, 0.08f, 0.06f); // Dark cracks

        // Pre-calculate block layout with varied sizes
        var rng = new RandomNumberGenerator();
        rng.Seed = 12345; // Consistent pattern

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Determine which block we're in using varied sizes
                int blockRow = 0;
                int yAccum = 0;
                int currentBlockHeight = blockHeights[0];
                while (yAccum + currentBlockHeight + mortarWidth <= y && blockRow < 20)
                {
                    yAccum += currentBlockHeight + mortarWidth;
                    blockRow++;
                    currentBlockHeight = blockHeights[(blockRow * 7) % blockHeights.Length];
                }
                int yInBlock = y - yAccum;
                bool onHorizontalMortar = yInBlock >= currentBlockHeight;

                // Varied offset per row
                int rowOffset = (blockRow % 2 == 0) ? 0 : blockWidths[(blockRow * 3) % blockWidths.Length] / 2;
                rowOffset += (blockRow * 17) % 20; // Extra randomness

                int blockCol = 0;
                int xAccum = -rowOffset;
                int currentBlockWidth = blockWidths[(blockRow + blockCol) % blockWidths.Length];
                while (xAccum + currentBlockWidth + mortarWidth <= x && blockCol < 20)
                {
                    xAccum += currentBlockWidth + mortarWidth;
                    blockCol++;
                    currentBlockWidth = blockWidths[(blockRow + blockCol * 5) % blockWidths.Length];
                }
                int xInBlock = x - xAccum;
                bool onVerticalMortar = xInBlock >= currentBlockWidth || xInBlock < 0;

                bool onMortar = onHorizontalMortar || onVerticalMortar;

                Color pixelColor;
                if (onMortar)
                {
                    // Mortar with slight variation
                    float mortarNoise = (float)GD.RandRange(-0.02, 0.02);
                    pixelColor = new Color(
                        mortarColor.R + mortarNoise,
                        mortarColor.G + mortarNoise,
                        mortarColor.B + mortarNoise
                    );
                }
                else
                {
                    // Select stone color based on block position
                    int stoneIndex = ((blockRow * 7 + blockCol * 11) % stoneColors.Length);
                    Color baseStone = stoneColors[stoneIndex];

                    // Add per-pixel noise for texture
                    float noise = (float)GD.RandRange(-0.03, 0.03);

                    // Large-scale variation within blocks (weathering patterns)
                    float weathering = Mathf.Sin(xInBlock * 0.15f) * Mathf.Cos(yInBlock * 0.12f) * 0.04f;

                    // Edge darkening (wear on block edges)
                    float edgeDist = Mathf.Min(
                        Mathf.Min(xInBlock, currentBlockWidth - xInBlock),
                        Mathf.Min(yInBlock, currentBlockHeight - yInBlock)
                    );
                    float edgeDarken = edgeDist < 4 ? -0.03f * (4 - edgeDist) / 4f : 0f;

                    // Random cracks (thin dark lines)
                    bool isCrack = false;
                    int crackSeed = (blockRow * 100 + blockCol) % 17;
                    if (crackSeed < 3) // 3 in 17 blocks have cracks
                    {
                        // Diagonal crack pattern
                        int crackX = (xInBlock + yInBlock * 2 + crackSeed * 7) % currentBlockWidth;
                        if (crackX >= 0 && crackX < 2)
                            isCrack = true;
                    }

                    // Moss patches (bottom of some blocks, damp areas)
                    bool hasMoss = false;
                    int mossSeed = (blockRow * 31 + blockCol * 13) % 11;
                    if (mossSeed < 2) // Some blocks have moss
                    {
                        // Moss grows on lower parts and edges
                        if (yInBlock > currentBlockHeight * 0.6f ||
                            (xInBlock < 8 && yInBlock > currentBlockHeight * 0.3f))
                            hasMoss = GD.Randf() > 0.4f;
                    }

                    // Wet/damp patches
                    bool isWet = false;
                    if ((x + y * 3) % 97 < 5)
                        isWet = true;

                    // Combine effects
                    if (isCrack)
                    {
                        pixelColor = crackColor;
                    }
                    else if (hasMoss)
                    {
                        float mossBlend = 0.5f + (float)GD.RandRange(0, 0.3);
                        pixelColor = baseStone.Lerp(mossColor, mossBlend);
                    }
                    else if (isWet)
                    {
                        pixelColor = baseStone.Lerp(wetColor, 0.4f);
                    }
                    else
                    {
                        pixelColor = new Color(
                            Mathf.Clamp(baseStone.R + noise + weathering + edgeDarken, 0, 1),
                            Mathf.Clamp(baseStone.G + noise + weathering + edgeDarken, 0, 1),
                            Mathf.Clamp(baseStone.B + noise + weathering + edgeDarken, 0, 1)
                        );
                    }
                }

                image.SetPixel(x, y, pixelColor);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    private ImageTexture CreateBrickWallNormalMap()
    {
        int size = 512; // Match texture resolution
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        // Match the varied block sizes from texture
        int[] blockWidths = { 48, 64, 56, 40, 72 };
        int[] blockHeights = { 24, 32, 28, 20 };
        int mortarWidth = 4;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Calculate block position (same algorithm as texture)
                int blockRow = 0;
                int yAccum = 0;
                int currentBlockHeight = blockHeights[0];
                while (yAccum + currentBlockHeight + mortarWidth <= y && blockRow < 20)
                {
                    yAccum += currentBlockHeight + mortarWidth;
                    blockRow++;
                    currentBlockHeight = blockHeights[(blockRow * 7) % blockHeights.Length];
                }
                int yInBlock = y - yAccum;
                bool onHorizontalMortar = yInBlock >= currentBlockHeight;

                int rowOffset = (blockRow % 2 == 0) ? 0 : blockWidths[(blockRow * 3) % blockWidths.Length] / 2;
                rowOffset += (blockRow * 17) % 20;

                int blockCol = 0;
                int xAccum = -rowOffset;
                int currentBlockWidth = blockWidths[(blockRow + blockCol) % blockWidths.Length];
                while (xAccum + currentBlockWidth + mortarWidth <= x && blockCol < 20)
                {
                    xAccum += currentBlockWidth + mortarWidth;
                    blockCol++;
                    currentBlockWidth = blockWidths[(blockRow + blockCol * 5) % blockWidths.Length];
                }
                int xInBlock = x - xAccum;
                bool onVerticalMortar = xInBlock >= currentBlockWidth || xInBlock < 0;

                bool nearVerticalMortar = xInBlock >= currentBlockWidth - 3 || xInBlock < 3;
                bool nearHorizontalMortar = yInBlock >= currentBlockHeight - 3 || yInBlock < 3;

                Vector3 normal;

                if (onVerticalMortar && onHorizontalMortar)
                {
                    normal = new Vector3(0.25f, 0.25f, 0.93f).Normalized();
                }
                else if (onVerticalMortar)
                {
                    normal = new Vector3(0.5f, 0f, 0.87f).Normalized();
                }
                else if (onHorizontalMortar)
                {
                    normal = new Vector3(0f, 0.5f, 0.87f).Normalized();
                }
                else if (nearVerticalMortar && nearHorizontalMortar)
                {
                    // Corner bevels
                    float sx = (xInBlock < 3) ? -0.15f : 0.15f;
                    float sy = (yInBlock < 3) ? -0.15f : 0.15f;
                    normal = new Vector3(sx, sy, 0.97f).Normalized();
                }
                else if (nearVerticalMortar)
                {
                    float slopeDir = (xInBlock < 3) ? -0.25f : 0.25f;
                    normal = new Vector3(slopeDir, 0f, 0.97f).Normalized();
                }
                else if (nearHorizontalMortar)
                {
                    float slopeDir = (yInBlock < 3) ? -0.25f : 0.25f;
                    normal = new Vector3(0f, slopeDir, 0.97f).Normalized();
                }
                else
                {
                    // Stone surface with weathering bumps
                    // Multi-scale noise for realistic stone
                    float largeNoise = Mathf.Sin(x * 0.08f + blockRow) * Mathf.Cos(y * 0.06f + blockCol) * 0.08f;
                    float medNoise = Mathf.Sin(x * 0.2f) * Mathf.Cos(y * 0.25f) * 0.04f;
                    float fineNoise = (float)GD.RandRange(-0.05, 0.05);

                    // Chisel marks / tool marks on some blocks
                    int toolMarkSeed = (blockRow * 7 + blockCol * 13) % 5;
                    float toolMark = 0f;
                    if (toolMarkSeed == 0)
                    {
                        toolMark = Mathf.Sin(xInBlock * 0.4f) * 0.06f;
                    }
                    else if (toolMarkSeed == 1)
                    {
                        toolMark = Mathf.Sin(yInBlock * 0.5f) * 0.05f;
                    }

                    float nx = largeNoise + medNoise * 0.5f + fineNoise + toolMark;
                    float ny = largeNoise * 0.7f + medNoise + fineNoise;
                    normal = new Vector3(nx, ny, 1f).Normalized();
                }

                image.SetPixel(x, y, new Color(
                    normal.X * 0.5f + 0.5f,
                    normal.Y * 0.5f + 0.5f,
                    normal.Z * 0.5f + 0.5f
                ));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private ImageTexture CreateStoneCeilingTexture()
    {
        int size = 256;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        // Create rough stone texture - dark cave ceiling with warm undertones
        var baseColor = new Color(0.22f, 0.18f, 0.14f); // Dark warm brown

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Simple noise-based stone
                float noise = (float)GD.RandRange(-0.06, 0.06);

                // Add some larger variation patches
                float patchNoise = 0;
                if ((x / 32 + y / 32) % 3 == 0)
                    patchNoise = -0.02f;

                Color pixelColor = new Color(
                    Mathf.Clamp(baseColor.R + noise + patchNoise, 0, 1),
                    Mathf.Clamp(baseColor.G + noise + patchNoise, 0, 1),
                    Mathf.Clamp(baseColor.B + noise + patchNoise, 0, 1)
                );

                image.SetPixel(x, y, pixelColor);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    private ImageTexture CreateCeilingNormalMap()
    {
        int size = 256;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        // Create rough cave-like normal map with organic variation
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Multi-scale noise for organic cave feel
                float noise1 = Mathf.Sin(x * 0.15f) * Mathf.Cos(y * 0.12f) * 0.3f;
                float noise2 = Mathf.Sin(x * 0.3f + y * 0.2f) * 0.15f;
                float noise3 = (float)GD.RandRange(-0.1, 0.1);

                // Combine for organic bumpy surface
                float noiseX = noise1 + noise3;
                float noiseY = noise2 + noise3;

                // Add occasional sharper ridges (like rock formations)
                if ((x + y * 3) % 47 < 3)
                {
                    noiseX += 0.2f;
                }
                if ((x * 2 + y) % 53 < 3)
                {
                    noiseY += 0.2f;
                }

                Vector3 normal = new Vector3(noiseX, noiseY, 1f).Normalized();

                image.SetPixel(x, y, new Color(
                    normal.X * 0.5f + 0.5f,
                    normal.Y * 0.5f + 0.5f,
                    normal.Z * 0.5f + 0.5f
                ));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Create a subdivided floor mesh with actual height variation for uneven ground feel.
    /// Includes stone block patterns, shallow trenches, and subtle grade changes.
    /// </summary>
    private ArrayMesh CreateUnevenFloorMesh(float width, float depth, int subdivisionsPerUnit = 2)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        // Calculate subdivisions - more = smoother but heavier
        int xVerts = (int)(width * subdivisionsPerUnit) + 1;
        int zVerts = (int)(depth * subdivisionsPerUnit) + 1;
        float xStep = width / (xVerts - 1);
        float zStep = depth / (zVerts - 1);

        // Height variation parameters (subtle - nothing you'd need to jump over)
        float stoneBlockHeight = 0.03f;    // Stones slightly raised from mortar
        float randomVariation = 0.02f;      // Random bumps within stones
        float trenchDepth = 0.04f;          // Shallow trenches/cracks
        float gradeVariation = 0.05f;       // Larger gradual elevation changes
        float tileSize = 2.0f;              // Size of stone blocks in world units

        // Pre-generate height map for consistent heights
        float[,] heightMap = new float[xVerts, zVerts];

        for (int z = 0; z < zVerts; z++)
        {
            for (int x = 0; x < xVerts; x++)
            {
                float worldX = x * xStep - width / 2;
                float worldZ = z * zStep - depth / 2;

                float height = 0f;

                // Large-scale grade variation (gentle slopes across the floor)
                height += Mathf.Sin(worldX * 0.1f) * Mathf.Cos(worldZ * 0.08f) * gradeVariation;
                height += Mathf.Sin(worldX * 0.05f + worldZ * 0.07f) * gradeVariation * 0.5f;

                // Stone block pattern - blocks are slightly raised from mortar grooves
                float tileX = worldX / tileSize;
                float tileZ = worldZ / tileSize;
                float mortarX = Mathf.Abs(tileX - Mathf.Round(tileX));
                float mortarZ = Mathf.Abs(tileZ - Mathf.Round(tileZ));
                float mortarWidth = 0.08f; // Width of mortar groove relative to tile

                bool inMortar = mortarX < mortarWidth || mortarZ < mortarWidth;
                if (!inMortar)
                {
                    // Stone surface - raised with slight random variation per stone
                    int stoneIdX = (int)Mathf.Floor(tileX + 1000); // Offset to handle negatives
                    int stoneIdZ = (int)Mathf.Floor(tileZ + 1000);
                    float stoneVariation = ((stoneIdX * 73 + stoneIdZ * 137) % 100) / 100f;
                    height += stoneBlockHeight * (0.7f + stoneVariation * 0.6f);

                    // Subtle bumps within each stone
                    height += (float)(_rng.Randf() - 0.5f) * randomVariation;
                }
                else
                {
                    // Mortar grooves are lower
                    height -= stoneBlockHeight * 0.3f;
                }

                // Occasional shallow trenches/cracks (using deterministic pattern)
                float crackPattern1 = Mathf.Sin(worldX * 2.3f + worldZ * 0.7f);
                float crackPattern2 = Mathf.Sin(worldX * 0.5f + worldZ * 2.1f);
                if (Mathf.Abs(crackPattern1) < 0.1f)
                    height -= trenchDepth * (1f - Mathf.Abs(crackPattern1) * 10f);
                if (Mathf.Abs(crackPattern2) < 0.08f)
                    height -= trenchDepth * 0.7f * (1f - Mathf.Abs(crackPattern2) * 12.5f);

                // Small rocks/bumps (scattered using deterministic pattern)
                int rockSeed = (int)(worldX * 17.3f) ^ (int)(worldZ * 23.7f);
                if ((rockSeed & 0xFF) < 8) // ~3% chance of small rock bump
                {
                    height += 0.025f;
                }

                heightMap[x, z] = height;
            }
        }

        // Generate vertices with calculated heights and proper normals
        for (int z = 0; z < zVerts; z++)
        {
            for (int x = 0; x < xVerts; x++)
            {
                float worldX = x * xStep - width / 2;
                float worldZ = z * zStep - depth / 2;
                float height = heightMap[x, z];

                // Calculate normal from height differences (central difference)
                float hL = x > 0 ? heightMap[x - 1, z] : height;
                float hR = x < xVerts - 1 ? heightMap[x + 1, z] : height;
                float hD = z > 0 ? heightMap[x, z - 1] : height;
                float hU = z < zVerts - 1 ? heightMap[x, z + 1] : height;

                surfaceTool.SetUV(new Vector2(worldX / 4f + 0.5f, worldZ / 4f + 0.5f)); // UV for texture tiling
                surfaceTool.AddVertex(new Vector3(worldX, height, worldZ));
            }
        }

        // Generate triangles
        for (int z = 0; z < zVerts - 1; z++)
        {
            for (int x = 0; x < xVerts - 1; x++)
            {
                int topLeft = z * xVerts + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * xVerts + x;
                int bottomRight = bottomLeft + 1;

                // First triangle (top-left, bottom-left, top-right)
                surfaceTool.AddIndex(topLeft);
                surfaceTool.AddIndex(bottomLeft);
                surfaceTool.AddIndex(topRight);

                // Second triangle (top-right, bottom-left, bottom-right)
                surfaceTool.AddIndex(topRight);
                surfaceTool.AddIndex(bottomLeft);
                surfaceTool.AddIndex(bottomRight);
            }
        }

        surfaceTool.GenerateNormals();
        surfaceTool.GenerateTangents();
        return surfaceTool.Commit();
    }

    // Test room mode
    [Export] public bool UseTestRoom { get; set; } = true;
    [Export] public int TestRoomSize { get; set; } = 50;

    // Large dungeon mode - 4x size with all monster types
    [Export] public bool UseLargeDungeon { get; set; } = true;
    [Export] public int LargeDungeonWidth { get; set; } = 160;  // 160x160 = 4x current 80x80
    [Export] public int LargeDungeonDepth { get; set; } = 160;
    [Export] public int LargeDungeonRooms { get; set; } = 20;   // 4x the ~5 rooms = 20 rooms

    // All monster types for large dungeon (matching EditorScreen3D Monsters tab exactly)
    private static readonly string[] AllMonsterTypes = {
        "goblin", "goblin_shaman", "goblin_thrower", "slime", "eye", "mushroom",
        "spider", "lizard", "skeleton", "wolf", "badlama", "bat", "dragon"
    };

    // Boss types for large dungeon (matching EditorScreen3D bosses section)
    private static readonly string[] BossMonsterTypes = {
        "skeleton_lord", "dragon_king", "spider_queen"
    };

    /// <summary>
    /// Generate a new dungeon with the given seed
    /// </summary>
    public void Generate(ulong seed = 0)
    {
        Seed = seed == 0 ? (ulong)GD.Randi() : seed;
        _rng.Seed = Seed;

        // Use large dungeon mode for extensive exploration
        if (UseLargeDungeon)
        {
            GenerateLargeDungeon();
            return;
        }

        // Use test room mode for testing abilities
        if (UseTestRoom)
        {
            GenerateTestRoom();
            return;
        }

        GD.Print($"[DungeonGenerator3D] Generating dungeon with seed: {Seed}");

        // Clear existing
        ClearDungeon();

        // Initialize map data
        _mapData = new int[DungeonWidth, 2, DungeonDepth]; // 2 floors for now
        Rooms.Clear();

        // Generate rooms
        int roomCount = _rng.RandiRange(MinRooms, MaxRooms);
        for (int i = 0; i < roomCount * 3; i++) // Try more times to place rooms
        {
            if (Rooms.Count >= roomCount) break;
            TryPlaceRoom();
        }

        // Connect rooms with corridors
        ConnectRooms();

        // Build 3D geometry
        BuildGeometry();

        // Populate with props and lights
        PopulateRooms();

        // Spawn enemies in rooms (except spawn room)
        SpawnEnemies();

        // Add ambient lighting
        AddAmbientLighting();

        GD.Print($"[DungeonGenerator3D] Generated {Rooms.Count} rooms");
        EmitSignal(SignalName.GenerationComplete);
    }

    /// <summary>
    /// Generate a simple test room for ability testing
    /// One large room with monster groups in each corner
    /// Plus 4 hallways leading to side rooms with unique monsters
    /// </summary>
    private void GenerateTestRoom()
    {
        GD.Print("[DungeonGenerator3D] Generating TEST ROOM for ability testing");

        ClearDungeon();

        int size = TestRoomSize * 2; // Double the size (100 instead of 50)
        _mapData = new int[size + 50, 2, size + 50]; // Extra space for hallways
        Rooms.Clear();

        // Create one big room
        var room = new Room
        {
            Position = new Vector3I(2, 0, 2),
            Size = new Vector3I(size, 1, size),
            Type = "arena"
        };
        Rooms.Add(room);
        _spawnRoom = room;

        // Fill map data for the room
        for (int x = 2; x < size + 2; x++)
        {
            for (int z = 2; z < size + 2; z++)
            {
                _mapData[x, 0, z] = 1; // Floor
            }
        }

        // Build floor
        BuildTestRoomFloor(size);

        // Build ceiling
        BuildTestRoomCeiling(size);

        // Build walls with openings for hallways
        BuildTestRoomWallsWithOpenings(size);

        // Build 4 hallways and side rooms
        BuildHallwaysAndSideRooms(size);

        // Add lighting
        AddTestRoomLighting(size);

        // Spawn monster groups in corners (but not too close to center spawn)
        SpawnTestRoomMonsters(size);

        GD.Print($"[DungeonGenerator3D] Test room generated: {size}x{size} with 4 side rooms");
        EmitSignal(SignalName.GenerationComplete);
    }

    private void BuildTestRoomFloor(int size)
    {
        // Create a floor mesh with actual geometry height variation for uneven ground feel
        float floorSize = size * TileSize + 2f;  // Add 1 unit on each side
        var floor = new MeshInstance3D();

        // Use subdivided mesh with height variation instead of flat plane
        // 2 subdivisions per unit gives good detail without too many triangles
        floor.Mesh = CreateUnevenFloorMesh(floorSize, floorSize, 2);
        floor.MaterialOverride = _floorMaterial;
        floor.Position = new Vector3(size * TileSize / 2, 0, size * TileSize / 2);
        _floorContainer?.AddChild(floor);

        // Add collision - thick box to prevent falling through
        // We keep the collision flat for smooth player movement
        // The visual unevenness creates the atmosphere without affecting gameplay
        var floorBody = new StaticBody3D();
        floorBody.Position = floor.Position;
        var floorShape = new CollisionShape3D();
        var boxShape = new BoxShape3D();
        boxShape.Size = new Vector3(floorSize, 0.5f, floorSize);
        floorShape.Shape = boxShape;
        floorShape.Position = new Vector3(0, -0.25f, 0);
        floorBody.AddChild(floorShape);
        floorBody.CollisionLayer = 128; // Layer 8 = bit 7 = value 128
        floorBody.CollisionMask = 0;
        _floorContainer?.AddChild(floorBody);
    }

    private void BuildTestRoomCeiling(int size)
    {
        // Create a ceiling mesh
        var ceiling = new MeshInstance3D();
        var planeMesh = new PlaneMesh();
        planeMesh.Size = new Vector2(size * TileSize, size * TileSize);
        ceiling.Mesh = planeMesh;
        ceiling.MaterialOverride = _ceilingMaterial;
        ceiling.Position = new Vector3(size * TileSize / 2, WallHeight, size * TileSize / 2);
        ceiling.RotationDegrees = new Vector3(180, 0, 0); // Flip to face downward
        _floorContainer?.AddChild(ceiling);
    }

    private void BuildTestRoomWalls(int size)
    {
        float halfSize = size * TileSize / 2;
        float center = halfSize;

        // Four walls
        CreateTestWall(new Vector3(center, WallHeight / 2, 0), new Vector3(size * TileSize, WallHeight, 0.5f)); // Front
        CreateTestWall(new Vector3(center, WallHeight / 2, size * TileSize), new Vector3(size * TileSize, WallHeight, 0.5f)); // Back
        CreateTestWall(new Vector3(0, WallHeight / 2, center), new Vector3(0.5f, WallHeight, size * TileSize)); // Left
        CreateTestWall(new Vector3(size * TileSize, WallHeight / 2, center), new Vector3(0.5f, WallHeight, size * TileSize)); // Right
    }

    private void BuildTestRoomWallsWithOpenings(int size)
    {
        float center = size * TileSize / 2;
        float wallLength = size * TileSize;
        float openingWidth = 8f; // Width of hallway opening
        float halfOpening = openingWidth / 2;
        float segmentLength = (wallLength - openingWidth) / 2;

        // Front wall (Z = 0) - opening in center
        CreateTestWall(new Vector3(segmentLength / 2, WallHeight / 2, 0), new Vector3(segmentLength, WallHeight, 0.5f));
        CreateTestWall(new Vector3(wallLength - segmentLength / 2, WallHeight / 2, 0), new Vector3(segmentLength, WallHeight, 0.5f));

        // Back wall (Z = size * TileSize) - opening in center
        CreateTestWall(new Vector3(segmentLength / 2, WallHeight / 2, wallLength), new Vector3(segmentLength, WallHeight, 0.5f));
        CreateTestWall(new Vector3(wallLength - segmentLength / 2, WallHeight / 2, wallLength), new Vector3(segmentLength, WallHeight, 0.5f));

        // Left wall (X = 0) - opening in center
        CreateTestWall(new Vector3(0, WallHeight / 2, segmentLength / 2), new Vector3(0.5f, WallHeight, segmentLength));
        CreateTestWall(new Vector3(0, WallHeight / 2, wallLength - segmentLength / 2), new Vector3(0.5f, WallHeight, segmentLength));

        // Right wall (X = size * TileSize) - opening in center
        CreateTestWall(new Vector3(wallLength, WallHeight / 2, segmentLength / 2), new Vector3(0.5f, WallHeight, segmentLength));
        CreateTestWall(new Vector3(wallLength, WallHeight / 2, wallLength - segmentLength / 2), new Vector3(0.5f, WallHeight, segmentLength));
    }

    private void BuildHallwaysAndSideRooms(int size)
    {
        float center = size * TileSize / 2;
        float wallLength = size * TileSize;
        float hallwayLength = 20f;
        float hallwayWidth = 8f;
        float roomSize = 20f;

        // Monster types for each direction - use Dragon_King for the final boss room
        string[] monsters = { "Skeleton", "Wolf", "Bat", "Dragon_King" };

        // North hallway and room (Z negative from main room, starts at Z=0)
        // Hallway center is at Z = -hallwayLength/2 (extending from Z=0 to Z=-hallwayLength)
        BuildHallwayAndRoom(
            new Vector3(center, 0, -hallwayLength / 2),  // Hallway center
            new Vector3(hallwayWidth, 0, hallwayLength), // Hallway size
            new Vector3(center, 0, -hallwayLength - roomSize / 2), // Room center
            roomSize,
            monsters[0],
            "north"
        );

        // South hallway and room (Z positive from main room, starts at Z=wallLength)
        // Hallway center is at Z = wallLength + hallwayLength/2
        BuildHallwayAndRoom(
            new Vector3(center, 0, wallLength + hallwayLength / 2),
            new Vector3(hallwayWidth, 0, hallwayLength),
            new Vector3(center, 0, wallLength + hallwayLength + roomSize / 2),
            roomSize,
            monsters[1],
            "south"
        );

        // West hallway and room (X negative from main room, starts at X=0)
        // Hallway center is at X = -hallwayLength/2
        BuildHallwayAndRoom(
            new Vector3(-hallwayLength / 2, 0, center),
            new Vector3(hallwayLength, 0, hallwayWidth),
            new Vector3(-hallwayLength - roomSize / 2, 0, center),
            roomSize,
            monsters[2],
            "west"
        );

        // East hallway and room (X positive from main room, starts at X=wallLength)
        // Hallway center is at X = wallLength + hallwayLength/2
        // This is the DRAGON room - build with special gate and Badlama area
        BuildDragonRoomWithGate(
            new Vector3(wallLength + hallwayLength / 2, 0, center),
            new Vector3(hallwayLength, 0, hallwayWidth),
            new Vector3(wallLength + hallwayLength + roomSize / 2, 0, center),
            roomSize,
            monsters[3]
        );
    }

    private void BuildHallwayAndRoom(Vector3 hallwayCenter, Vector3 hallwaySize, Vector3 roomCenter, float roomSize, string monsterType, string direction)
    {
        // Build hallway floor with uneven terrain - add extra overlap to ensure no gaps
        var hallwayFloor = new MeshInstance3D();

        // hallwaySize.X is X extent, hallwaySize.Z is Z extent
        // Add overlap: +6 on length (3 each end for main room and side room connection), +2 on width
        float floorX = hallwaySize.X + (hallwaySize.X > hallwaySize.Z ? 6f : 2f);
        float floorZ = hallwaySize.Z + (hallwaySize.Z > hallwaySize.X ? 6f : 2f);

        // Use uneven floor mesh for terrain feel
        hallwayFloor.Mesh = CreateUnevenFloorMesh(floorX, floorZ, 2);
        hallwayFloor.MaterialOverride = _floorMaterial;
        hallwayFloor.Position = hallwayCenter;
        _floorContainer?.AddChild(hallwayFloor);

        // Hallway floor collision - thick box to prevent falling through
        var hallwayBody = new StaticBody3D();
        hallwayBody.Position = hallwayCenter;
        var hallwayShape = new CollisionShape3D();
        var hallwayBoxShape = new BoxShape3D();
        hallwayBoxShape.Size = new Vector3(floorX, 0.5f, floorZ);
        hallwayShape.Shape = hallwayBoxShape;
        hallwayShape.Position = new Vector3(0, -0.25f, 0);
        hallwayBody.AddChild(hallwayShape);
        hallwayBody.CollisionLayer = 128; // Layer 8 = bit 7 = value 128
        hallwayBody.CollisionMask = 0;
        _floorContainer?.AddChild(hallwayBody);

        // Hallway walls (sides only, ends are open)
        float hw = hallwaySize.X > hallwaySize.Z ? hallwaySize.Z / 2 : hallwaySize.X / 2;
        float hl = hallwaySize.X > hallwaySize.Z ? hallwaySize.X : hallwaySize.Z;

        if (direction == "north" || direction == "south")
        {
            // Side walls for N/S hallway
            CreateTestWall(new Vector3(hallwayCenter.X - hw, WallHeight / 2, hallwayCenter.Z), new Vector3(0.5f, WallHeight, hl));
            CreateTestWall(new Vector3(hallwayCenter.X + hw, WallHeight / 2, hallwayCenter.Z), new Vector3(0.5f, WallHeight, hl));
        }
        else
        {
            // Side walls for E/W hallway
            CreateTestWall(new Vector3(hallwayCenter.X, WallHeight / 2, hallwayCenter.Z - hw), new Vector3(hl, WallHeight, 0.5f));
            CreateTestWall(new Vector3(hallwayCenter.X, WallHeight / 2, hallwayCenter.Z + hw), new Vector3(hl, WallHeight, 0.5f));
        }

        // Build side room floor with uneven terrain - extend more for seamless connection with hallway
        float extendedRoomSize = roomSize + 4f;
        var roomFloor = new MeshInstance3D();
        // Use uneven floor mesh for terrain feel
        roomFloor.Mesh = CreateUnevenFloorMesh(extendedRoomSize, extendedRoomSize, 2);
        roomFloor.MaterialOverride = _floorMaterial;
        roomFloor.Position = roomCenter;
        _floorContainer?.AddChild(roomFloor);

        // Room floor collision - thick box to prevent falling through
        var roomBody = new StaticBody3D();
        roomBody.Position = roomCenter;
        var roomShape = new CollisionShape3D();
        var roomBoxShape = new BoxShape3D();
        roomBoxShape.Size = new Vector3(extendedRoomSize, 0.5f, extendedRoomSize);
        roomShape.Shape = roomBoxShape;
        roomShape.Position = new Vector3(0, -0.25f, 0);
        roomBody.AddChild(roomShape);
        roomBody.CollisionLayer = 128; // Layer 8 = bit 7 = value 128
        roomBody.CollisionMask = 0;
        _floorContainer?.AddChild(roomBody);

        // Room walls (3 walls, one open for hallway entrance)
        float rs = roomSize / 2;
        float openingWidth = 8f;
        float sideSegment = (roomSize - openingWidth) / 2;

        if (direction == "north")
        {
            // North room - opening on south side
            CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z - rs), new Vector3(roomSize, WallHeight, 0.5f)); // North wall
            CreateTestWall(new Vector3(roomCenter.X - rs, WallHeight / 2, roomCenter.Z), new Vector3(0.5f, WallHeight, roomSize)); // West wall
            CreateTestWall(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z), new Vector3(0.5f, WallHeight, roomSize)); // East wall
            // South wall with opening
            CreateTestWall(new Vector3(roomCenter.X - rs + sideSegment / 2, WallHeight / 2, roomCenter.Z + rs), new Vector3(sideSegment, WallHeight, 0.5f));
            CreateTestWall(new Vector3(roomCenter.X + rs - sideSegment / 2, WallHeight / 2, roomCenter.Z + rs), new Vector3(sideSegment, WallHeight, 0.5f));
        }
        else if (direction == "south")
        {
            // South room - opening on north side
            CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z + rs), new Vector3(roomSize, WallHeight, 0.5f)); // South wall
            CreateTestWall(new Vector3(roomCenter.X - rs, WallHeight / 2, roomCenter.Z), new Vector3(0.5f, WallHeight, roomSize)); // West wall
            CreateTestWall(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z), new Vector3(0.5f, WallHeight, roomSize)); // East wall
            // North wall with opening
            CreateTestWall(new Vector3(roomCenter.X - rs + sideSegment / 2, WallHeight / 2, roomCenter.Z - rs), new Vector3(sideSegment, WallHeight, 0.5f));
            CreateTestWall(new Vector3(roomCenter.X + rs - sideSegment / 2, WallHeight / 2, roomCenter.Z - rs), new Vector3(sideSegment, WallHeight, 0.5f));
        }
        else if (direction == "west")
        {
            // West room - opening on east side
            CreateTestWall(new Vector3(roomCenter.X - rs, WallHeight / 2, roomCenter.Z), new Vector3(0.5f, WallHeight, roomSize)); // West wall
            CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z - rs), new Vector3(roomSize, WallHeight, 0.5f)); // North wall
            CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z + rs), new Vector3(roomSize, WallHeight, 0.5f)); // South wall
            // East wall with opening
            CreateTestWall(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z - rs + sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
            CreateTestWall(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z + rs - sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
        }
        else // east
        {
            // East room - opening on west side
            CreateTestWall(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z), new Vector3(0.5f, WallHeight, roomSize)); // East wall
            CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z - rs), new Vector3(roomSize, WallHeight, 0.5f)); // North wall
            CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z + rs), new Vector3(roomSize, WallHeight, 0.5f)); // South wall
            // West wall with opening
            CreateTestWall(new Vector3(roomCenter.X - rs, WallHeight / 2, roomCenter.Z - rs + sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
            CreateTestWall(new Vector3(roomCenter.X - rs, WallHeight / 2, roomCenter.Z + rs - sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
        }

        // Add light to the room - brighter for better visibility
        var light = new OmniLight3D();
        light.Position = new Vector3(roomCenter.X, WallHeight * 0.8f, roomCenter.Z);
        light.LightEnergy = 3.5f; // Brighter room lights
        light.LightColor = new Color(1f, 0.92f, 0.85f); // Warmer torch glow
        light.OmniRange = roomSize * 1.8f; // Wider coverage
        light.OmniAttenuation = 1.2f; // Softer falloff
        light.ShadowEnabled = _shadowLightCount < MaxShadowLights;
        if (light.ShadowEnabled) _shadowLightCount++;
        _lightContainer?.AddChild(light);

        // Spawn the unique monster
        var enemy = BasicEnemy3D.Create(monsterType, new Vector3(roomCenter.X, 0.5f, roomCenter.Z));
        enemy.AddToGroup("Enemies");
        _enemyContainer?.AddChild(enemy);

        GD.Print($"[DungeonGenerator3D] Created {direction} room with {monsterType}");
    }

    /// <summary>
    /// Special dragon room with locked iron gate leading to Badlama area.
    /// Gate opens when dragon boss is killed.
    /// </summary>
    private void BuildDragonRoomWithGate(Vector3 hallwayCenter, Vector3 hallwaySize, Vector3 roomCenter, float roomSize, string monsterType)
    {
        // Store room info for later use
        _dragonRoomCenter = roomCenter;
        _dragonRoomSize = roomSize;

        // Build hallway floor with uneven terrain
        var hallwayFloor = new MeshInstance3D();
        float floorX = hallwaySize.X + 6f;
        float floorZ = hallwaySize.Z + 2f;
        hallwayFloor.Mesh = CreateUnevenFloorMesh(floorX, floorZ, 2);
        hallwayFloor.MaterialOverride = _floorMaterial;
        hallwayFloor.Position = hallwayCenter;
        _floorContainer?.AddChild(hallwayFloor);

        // Hallway floor collision
        var hallwayBody = new StaticBody3D();
        hallwayBody.Position = hallwayCenter;
        var hallwayShape = new CollisionShape3D();
        var hallwayBoxShape = new BoxShape3D();
        hallwayBoxShape.Size = new Vector3(floorX, 0.5f, floorZ);
        hallwayShape.Shape = hallwayBoxShape;
        hallwayShape.Position = new Vector3(0, -0.25f, 0);
        hallwayBody.AddChild(hallwayShape);
        hallwayBody.CollisionLayer = 128;
        hallwayBody.CollisionMask = 0;
        _floorContainer?.AddChild(hallwayBody);

        // Hallway side walls (E/W hallway)
        float hw = hallwaySize.Z / 2;
        float hl = hallwaySize.X;
        CreateTestWall(new Vector3(hallwayCenter.X, WallHeight / 2, hallwayCenter.Z - hw), new Vector3(hl, WallHeight, 0.5f));
        CreateTestWall(new Vector3(hallwayCenter.X, WallHeight / 2, hallwayCenter.Z + hw), new Vector3(hl, WallHeight, 0.5f));

        // Build dragon room floor
        float extendedRoomSize = roomSize + 4f;
        var roomFloor = new MeshInstance3D();
        roomFloor.Mesh = CreateUnevenFloorMesh(extendedRoomSize, extendedRoomSize, 2);
        roomFloor.MaterialOverride = _floorMaterial;
        roomFloor.Position = roomCenter;
        _floorContainer?.AddChild(roomFloor);

        // Room floor collision
        var roomBody = new StaticBody3D();
        roomBody.Position = roomCenter;
        var roomShape = new CollisionShape3D();
        var roomBoxShape = new BoxShape3D();
        roomBoxShape.Size = new Vector3(extendedRoomSize, 0.5f, extendedRoomSize);
        roomShape.Shape = roomBoxShape;
        roomShape.Position = new Vector3(0, -0.25f, 0);
        roomBody.AddChild(roomShape);
        roomBody.CollisionLayer = 128;
        roomBody.CollisionMask = 0;
        _floorContainer?.AddChild(roomBody);

        float rs = roomSize / 2;
        float openingWidth = 8f;
        float sideSegment = (roomSize - openingWidth) / 2;

        // East room walls - MODIFIED: East wall now has gate instead of solid wall
        // North wall (solid)
        CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z - rs), new Vector3(roomSize, WallHeight, 0.5f));
        // South wall (solid)
        CreateTestWall(new Vector3(roomCenter.X, WallHeight / 2, roomCenter.Z + rs), new Vector3(roomSize, WallHeight, 0.5f));
        // East wall with GATE opening in center (iron grate gate blocks passage until dragon dies)
        CreateTestWall(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z - rs + sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
        CreateTestWall(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z + rs - sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
        // West wall with opening for entrance from main area
        CreateTestWall(new Vector3(roomCenter.X - rs, WallHeight / 2, roomCenter.Z - rs + sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
        CreateTestWall(new Vector3(roomCenter.X - rs, WallHeight / 2, roomCenter.Z + rs - sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));

        // Create the iron grate gate (locked until dragon dies)
        CreateDragonGate(new Vector3(roomCenter.X + rs, WallHeight / 2, roomCenter.Z), openingWidth);

        // Add light to dragon room (red/orange for dragon lair) - shadow if under limit
        var light = new OmniLight3D();
        light.Position = new Vector3(roomCenter.X, WallHeight * 0.8f, roomCenter.Z);
        light.LightEnergy = 2.5f;
        light.LightColor = new Color(1f, 0.6f, 0.3f); // Orange dragon fire glow
        light.OmniRange = roomSize * 1.5f;
        light.ShadowEnabled = _shadowLightCount < MaxShadowLights;
        if (light.ShadowEnabled) _shadowLightCount++;
        _lightContainer?.AddChild(light);

        // Spawn the DRAGON BOSS (not a basic enemy)
        _dragonBoss = BossEnemy3D.Create(monsterType, new Vector3(roomCenter.X, 0.5f, roomCenter.Z));
        _dragonBoss.AddToGroup("Enemies");
        _dragonBoss.AddToGroup("DragonBoss");
        _dragonBoss.Died += OnDragonBossKilled;
        _enemyContainer?.AddChild(_dragonBoss);

        GD.Print($"[DungeonGenerator3D] Created Dragon room with locked gate");

        // Build the secret Badlama area behind the gate
        BuildBadlamaArea(roomCenter, roomSize);
    }

    /// <summary>
    /// Creates an iron grate gate that blocks the passage until the dragon is killed.
    /// </summary>
    private void CreateDragonGate(Vector3 position, float width)
    {
        // Gate container
        _dragonGate = new Node3D();
        _dragonGate.Name = "DragonGate";
        _dragonGate.Position = position;
        _wallContainer?.AddChild(_dragonGate);

        // Iron grate material - dark metal with slight rust
        var ironMat = new StandardMaterial3D();
        ironMat.AlbedoColor = new Color(0.25f, 0.22f, 0.2f); // Dark iron
        ironMat.Roughness = 0.7f;
        ironMat.Metallic = 0.8f;

        // Grate frame (top and bottom horizontal bars)
        var frameTop = new MeshInstance3D();
        var frameTopMesh = new BoxMesh();
        frameTopMesh.Size = new Vector3(0.4f, 0.3f, width);
        frameTop.Mesh = frameTopMesh;
        frameTop.MaterialOverride = ironMat;
        frameTop.Position = new Vector3(0, WallHeight / 2 - 0.15f, 0);
        _dragonGate.AddChild(frameTop);

        var frameBottom = new MeshInstance3D();
        var frameBottomMesh = new BoxMesh();
        frameBottomMesh.Size = new Vector3(0.4f, 0.3f, width);
        frameBottom.Mesh = frameBottomMesh;
        frameBottom.MaterialOverride = ironMat;
        frameBottom.Position = new Vector3(0, -WallHeight / 2 + 0.15f, 0);
        _dragonGate.AddChild(frameBottom);

        // Vertical iron bars
        int barCount = (int)(width / 0.5f);
        float barSpacing = width / (barCount + 1);
        for (int i = 1; i <= barCount; i++)
        {
            var bar = new MeshInstance3D();
            var barMesh = new CylinderMesh();
            barMesh.TopRadius = 0.08f;
            barMesh.BottomRadius = 0.08f;
            barMesh.Height = WallHeight;
            bar.Mesh = barMesh;
            bar.MaterialOverride = ironMat;
            bar.Position = new Vector3(0, 0, -width / 2 + i * barSpacing);
            _dragonGate.AddChild(bar);
        }

        // Horizontal cross bars (decorative)
        for (int h = 1; h < 3; h++)
        {
            var crossBar = new MeshInstance3D();
            var crossMesh = new BoxMesh();
            crossMesh.Size = new Vector3(0.15f, 0.15f, width);
            crossBar.Mesh = crossMesh;
            crossBar.MaterialOverride = ironMat;
            crossBar.Position = new Vector3(0, -WallHeight / 2 + h * (WallHeight / 3), 0);
            _dragonGate.AddChild(crossBar);
        }

        // Gate collision (blocks passage)
        _dragonGateCollision = new StaticBody3D();
        _dragonGateCollision.Position = position;
        var gateShape = new CollisionShape3D();
        var gateBox = new BoxShape3D();
        gateBox.Size = new Vector3(0.5f, WallHeight, width);
        gateShape.Shape = gateBox;
        _dragonGateCollision.AddChild(gateShape);
        _dragonGateCollision.CollisionLayer = 128; // Wall layer
        _dragonGateCollision.CollisionMask = 0;
        _wallContainer?.AddChild(_dragonGateCollision);

        // Add a red glow behind the gate (ominous Badlama area)
        var gateLight = new OmniLight3D();
        gateLight.Position = new Vector3(position.X + 5f, WallHeight * 0.5f, position.Z);
        gateLight.LightEnergy = 1.5f;
        gateLight.LightColor = new Color(1f, 0.3f, 0.2f); // Ominous red
        gateLight.OmniRange = 15f;
        _lightContainer?.AddChild(gateLight);

        GD.Print($"[DungeonGenerator3D] Created dragon gate at {position}");
    }

    /// <summary>
    /// Called when the dragon boss is killed - opens the gate to the Badlama area.
    /// </summary>
    private void OnDragonBossKilled(BossEnemy3D boss)
    {
        if (_dragonGateOpen) return;
        _dragonGateOpen = true;

        GD.Print("[DungeonGenerator3D] DRAGON SLAIN! The gate opens...");

        // Animate the gate opening (slide up into ceiling)
        if (_dragonGate != null)
        {
            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(_dragonGate, "position:y", _dragonGate.Position.Y + WallHeight + 1f, 2f);

            // Play gate opening sound
            SoundManager3D.Instance?.PlaySound("door_open");
        }

        // Remove gate collision
        if (_dragonGateCollision != null)
        {
            var collisionTween = CreateTween();
            collisionTween.TweenInterval(0.5f); // Wait for gate to start moving
            collisionTween.TweenCallback(Callable.From(() =>
            {
                _dragonGateCollision.CollisionLayer = 0;
                _dragonGateCollision.CollisionMask = 0;
            }));
        }

        GD.Print("[DungeonGenerator3D] Gate opened! Badlama area accessible!");
    }

    /// <summary>
    /// Builds the secret Badlama area behind the dragon's gate.
    /// A mirrored dungeon area filled with dangerous Badlamas.
    /// </summary>
    private void BuildBadlamaArea(Vector3 dragonRoomCenter, float dragonRoomSize)
    {
        float hallwayLength = 25f;
        float hallwayWidth = 8f;
        float badlamaRoomSize = 30f; // Larger room for Badlama herd

        // Position the hallway starting from the dragon room's east wall
        float startX = dragonRoomCenter.X + dragonRoomSize / 2;
        Vector3 hallwayCenter = new Vector3(startX + hallwayLength / 2, 0, dragonRoomCenter.Z);
        Vector3 badlamaRoomCenter = new Vector3(startX + hallwayLength + badlamaRoomSize / 2, 0, dragonRoomCenter.Z);

        // Build hallway floor
        var hallwayFloor = new MeshInstance3D();
        hallwayFloor.Mesh = CreateUnevenFloorMesh(hallwayLength + 4f, hallwayWidth + 2f, 2);
        hallwayFloor.MaterialOverride = _floorMaterial;
        hallwayFloor.Position = hallwayCenter;
        _floorContainer?.AddChild(hallwayFloor);

        // Hallway floor collision
        var hallwayBody = new StaticBody3D();
        hallwayBody.Position = hallwayCenter;
        var hallwayShape = new CollisionShape3D();
        var hallwayBoxShape = new BoxShape3D();
        hallwayBoxShape.Size = new Vector3(hallwayLength + 4f, 0.5f, hallwayWidth + 2f);
        hallwayShape.Shape = hallwayBoxShape;
        hallwayShape.Position = new Vector3(0, -0.25f, 0);
        hallwayBody.AddChild(hallwayShape);
        hallwayBody.CollisionLayer = 128;
        hallwayBody.CollisionMask = 0;
        _floorContainer?.AddChild(hallwayBody);

        // Hallway walls
        float hw = hallwayWidth / 2;
        CreateTestWall(new Vector3(hallwayCenter.X, WallHeight / 2, hallwayCenter.Z - hw), new Vector3(hallwayLength, WallHeight, 0.5f));
        CreateTestWall(new Vector3(hallwayCenter.X, WallHeight / 2, hallwayCenter.Z + hw), new Vector3(hallwayLength, WallHeight, 0.5f));

        // Build Badlama room floor
        float extendedSize = badlamaRoomSize + 4f;
        var roomFloor = new MeshInstance3D();
        roomFloor.Mesh = CreateUnevenFloorMesh(extendedSize, extendedSize, 2);
        roomFloor.MaterialOverride = _floorMaterial;
        roomFloor.Position = badlamaRoomCenter;
        _floorContainer?.AddChild(roomFloor);

        // Room floor collision
        var roomBody = new StaticBody3D();
        roomBody.Position = badlamaRoomCenter;
        var roomShape = new CollisionShape3D();
        var roomBoxShape = new BoxShape3D();
        roomBoxShape.Size = new Vector3(extendedSize, 0.5f, extendedSize);
        roomShape.Shape = roomBoxShape;
        roomShape.Position = new Vector3(0, -0.25f, 0);
        roomBody.AddChild(roomShape);
        roomBody.CollisionLayer = 128;
        roomBody.CollisionMask = 0;
        _floorContainer?.AddChild(roomBody);

        float rs = badlamaRoomSize / 2;
        float openingWidth = 8f;
        float sideSegment = (badlamaRoomSize - openingWidth) / 2;

        // Badlama room walls - opening on west side for hallway entrance
        CreateTestWall(new Vector3(badlamaRoomCenter.X + rs, WallHeight / 2, badlamaRoomCenter.Z), new Vector3(0.5f, WallHeight, badlamaRoomSize)); // East wall (solid)
        CreateTestWall(new Vector3(badlamaRoomCenter.X, WallHeight / 2, badlamaRoomCenter.Z - rs), new Vector3(badlamaRoomSize, WallHeight, 0.5f)); // North wall
        CreateTestWall(new Vector3(badlamaRoomCenter.X, WallHeight / 2, badlamaRoomCenter.Z + rs), new Vector3(badlamaRoomSize, WallHeight, 0.5f)); // South wall
        // West wall with opening
        CreateTestWall(new Vector3(badlamaRoomCenter.X - rs, WallHeight / 2, badlamaRoomCenter.Z - rs + sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));
        CreateTestWall(new Vector3(badlamaRoomCenter.X - rs, WallHeight / 2, badlamaRoomCenter.Z + rs - sideSegment / 2), new Vector3(0.5f, WallHeight, sideSegment));

        // Add dramatic lighting for Badlama room (fiery orange/red) - shadow if under limit
        var mainLight = new OmniLight3D();
        mainLight.Position = new Vector3(badlamaRoomCenter.X, WallHeight * 0.8f, badlamaRoomCenter.Z);
        mainLight.LightEnergy = 3f;
        mainLight.LightColor = new Color(1f, 0.5f, 0.2f); // Fiery orange
        mainLight.OmniRange = badlamaRoomSize * 1.2f;
        mainLight.ShadowEnabled = _shadowLightCount < MaxShadowLights;
        if (mainLight.ShadowEnabled) _shadowLightCount++;
        _lightContainer?.AddChild(mainLight);

        // Add corner fire lights - no shadows for performance
        Vector3[] cornerPositions = {
            new Vector3(badlamaRoomCenter.X - rs * 0.7f, WallHeight * 0.5f, badlamaRoomCenter.Z - rs * 0.7f),
            new Vector3(badlamaRoomCenter.X + rs * 0.7f, WallHeight * 0.5f, badlamaRoomCenter.Z - rs * 0.7f),
            new Vector3(badlamaRoomCenter.X - rs * 0.7f, WallHeight * 0.5f, badlamaRoomCenter.Z + rs * 0.7f),
            new Vector3(badlamaRoomCenter.X + rs * 0.7f, WallHeight * 0.5f, badlamaRoomCenter.Z + rs * 0.7f)
        };
        foreach (var pos in cornerPositions)
        {
            var cornerLight = new OmniLight3D();
            cornerLight.Position = pos;
            cornerLight.LightEnergy = 1.5f;
            cornerLight.LightColor = new Color(1f, 0.3f, 0.1f); // Reddish fire
            cornerLight.OmniRange = 10f;
            cornerLight.ShadowEnabled = false; // No shadows on corner lights
            _lightContainer?.AddChild(cornerLight);
        }

        // Spawn a HERD of Badlamas (8-12)
        int badlamaCount = 8 + (int)(GD.Randi() % 5);
        for (int i = 0; i < badlamaCount; i++)
        {
            // Spread badlamas around the room
            float angle = i * (Mathf.Pi * 2f / badlamaCount);
            float radius = rs * 0.5f + GD.Randf() * rs * 0.3f;
            float x = badlamaRoomCenter.X + Mathf.Cos(angle) * radius;
            float z = badlamaRoomCenter.Z + Mathf.Sin(angle) * radius;

            var badlama = BasicEnemy3D.Create("badlama", new Vector3(x, 0.5f, z));
            badlama.AddToGroup("Enemies");
            badlama.AddToGroup("BadlamaHerd");
            _enemyContainer?.AddChild(badlama);
        }

        GD.Print($"[DungeonGenerator3D] Created Badlama area with {badlamaCount} Badlamas behind dragon gate");
    }

    private void CreateTestWall(Vector3 position, Vector3 size)
    {
        var wall = new MeshInstance3D();
        var boxMesh = new BoxMesh();
        boxMesh.Size = size;
        wall.Mesh = boxMesh;
        wall.MaterialOverride = _wallMaterial;
        wall.Position = position;
        _wallContainer?.AddChild(wall);

        // Collision
        var wallBody = new StaticBody3D();
        wallBody.Position = position;
        var wallShape = new CollisionShape3D();
        var wallBoxShape = new BoxShape3D();
        wallBoxShape.Size = size;
        wallShape.Shape = wallBoxShape;
        wallBody.AddChild(wallShape);
        wallBody.CollisionLayer = 128; // Wall layer
        wallBody.CollisionMask = 0;
        _wallContainer?.AddChild(wallBody);
    }

    private void AddTestRoomLighting(int size)
    {
        float center = size * TileSize / 2;

        // Central bright light - only enable shadow if under limit
        var mainLight = CreateOptimizedLight(
            new Vector3(center, WallHeight - 1f, center),
            new Color(1f, 0.95f, 0.9f),
            3f,
            size * TileSize,
            enableShadow: true
        );
        _lightContainer?.AddChild(mainLight);

        // Corner lights (dimmer warm lights) - no shadows for performance
        float offset = size * TileSize * 0.35f;
        Vector3[] cornerPositions = {
            new Vector3(center - offset, WallHeight * 0.7f, center - offset),
            new Vector3(center + offset, WallHeight * 0.7f, center - offset),
            new Vector3(center - offset, WallHeight * 0.7f, center + offset),
            new Vector3(center + offset, WallHeight * 0.7f, center + offset)
        };

        foreach (var pos in cornerPositions)
        {
            var light = CreateOptimizedLight(pos, new Color(1f, 0.8f, 0.6f), 1.5f, 20f, enableShadow: false);
            _lightContainer?.AddChild(light);
        }

        // Add soft blue ambient lights scattered around the room
        AddSoftBlueLights(size);
    }

    private void AddSoftBlueLights(int size)
    {
        float center = size * TileSize / 2;
        float spread = size * TileSize * 0.4f;

        // Soft blue lights at various positions for atmosphere
        Vector3[] bluePositions = {
            // Along walls at mid-height
            new Vector3(center - spread, WallHeight * 0.4f, center),
            new Vector3(center + spread, WallHeight * 0.4f, center),
            new Vector3(center, WallHeight * 0.4f, center - spread),
            new Vector3(center, WallHeight * 0.4f, center + spread),
            // Near floor level for eerie glow
            new Vector3(center - spread * 0.7f, 1f, center - spread * 0.7f),
            new Vector3(center + spread * 0.7f, 1f, center - spread * 0.7f),
            new Vector3(center - spread * 0.7f, 1f, center + spread * 0.7f),
            new Vector3(center + spread * 0.7f, 1f, center + spread * 0.7f),
            // High up near ceiling
            new Vector3(center - spread * 0.5f, WallHeight * 0.85f, center - spread * 0.5f),
            new Vector3(center + spread * 0.5f, WallHeight * 0.85f, center + spread * 0.5f),
        };

        foreach (var pos in bluePositions)
        {
            var blueLight = CreateOptimizedLight(pos, new Color(0.4f, 0.6f, 1f), 0.6f, 12f, enableShadow: false);
            _lightContainer?.AddChild(blueLight);
        }

        GD.Print($"[DungeonGenerator3D] Added {bluePositions.Length} soft blue lights");
    }

    private void SpawnTestRoomMonsters(int size)
    {
        float center = size * TileSize / 2;
        float cornerOffset = size * TileSize * 0.35f; // Distance from center to corners

        // Spawn groups in each corner
        Vector3[] cornerCenters = {
            new Vector3(center - cornerOffset, 0, center - cornerOffset),
            new Vector3(center + cornerOffset, 0, center - cornerOffset),
            new Vector3(center - cornerOffset, 0, center + cornerOffset),
            new Vector3(center + cornerOffset, 0, center + cornerOffset)
        };

        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 groupCenter = cornerCenters[corner];

            // Spawn furniture around the goblin camp
            SpawnGoblinCampFurniture(groupCenter);

            // Spawn a random Editor boss as leader of each group
            string bossType = BossMonsterTypes[_rng.RandiRange(0, BossMonsterTypes.Length - 1)];
            var boss = BossEnemy3D.Create(bossType, groupCenter + new Vector3(0, 0.5f, 0));
            boss.AddToGroup("Enemies");
            _enemyContainer?.AddChild(boss);

            // Spawn 4 random monsters from Editor list (keeping group size at 5)
            for (int i = 0; i < 4; i++)
            {
                float offsetX = _rng.RandfRange(-4f, 4f);
                float offsetZ = _rng.RandfRange(-4f, 4f);
                Vector3 pos = groupCenter + new Vector3(offsetX, 0.5f, offsetZ);

                string monsterType = AllMonsterTypes[_rng.RandiRange(0, AllMonsterTypes.Length - 1)];
                var enemy = CreateMonsterByType(monsterType, pos, 1);
                enemy.AddToGroup("Enemies");
                _enemyContainer?.AddChild(enemy);
            }

            GD.Print($"[DungeonGenerator3D] Spawned mixed group at corner {corner + 1}: 1 {bossType} boss + 4 random monsters");
        }
    }

    /// <summary>
    /// Spawn furniture and props around a goblin camp location
    /// Creates a lived-in feeling with tables, chairs, chests, skull piles, and a campfire
    /// </summary>
    private void SpawnGoblinCampFurniture(Vector3 campCenter)
    {
        // Campfire in the center - the heart of the goblin camp
        var campfire = Cosmetic3D.Create("campfire",
            new Color(0.35f, 0.2f, 0.1f),   // Wood color
            new Color(0.15f, 0.1f, 0.08f),  // Charred wood
            new Color(0.4f, 0.4f, 0.42f),   // Rock color
            _rng.Randf(), 1.2f, true);
        campfire.Position = campCenter;
        _propContainer?.AddChild(campfire);

        // Table offset from center (not on the campfire!)
        var table = Cosmetic3D.Create("table",
            new Color(0.4f, 0.28f, 0.18f),  // Dark wood
            new Color(0.3f, 0.2f, 0.12f),
            new Color(0.35f, 0.35f, 0.4f),
            _rng.Randf(), 1f, true);
        float tableAngle = _rng.Randf() * Mathf.Tau;
        table.Position = campCenter + new Vector3(Mathf.Cos(tableAngle) * 3f, 0, Mathf.Sin(tableAngle) * 3f);
        table.RotateY(_rng.Randf() * Mathf.Tau);
        _propContainer?.AddChild(table);

        // 2-4 chairs around the table
        int chairCount = _rng.RandiRange(2, 4);
        for (int i = 0; i < chairCount; i++)
        {
            float angle = (Mathf.Tau / chairCount) * i + _rng.RandfRange(-0.3f, 0.3f);
            float dist = _rng.RandfRange(1.5f, 2.5f);
            Vector3 chairPos = campCenter + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);

            var chair = Cosmetic3D.Create("chair",
                new Color(0.45f, 0.3f, 0.2f),
                new Color(0.35f, 0.22f, 0.14f),
                new Color(0.3f, 0.3f, 0.35f),
                _rng.Randf(), 1f, true);
            chair.Position = chairPos;
            // Face chair towards table
            chair.RotateY(angle + Mathf.Pi + _rng.RandfRange(-0.3f, 0.3f));
            _propContainer?.AddChild(chair);
        }

        // Treasure chest nearby
        var chest = Cosmetic3D.Create("chest",
            new Color(0.5f, 0.35f, 0.2f),
            new Color(0.4f, 0.28f, 0.15f),
            new Color(0.7f, 0.6f, 0.2f),  // Gold accent
            _rng.Randf(), 1.2f, true);
        chest.Position = campCenter + new Vector3(_rng.RandfRange(3f, 5f), 0, _rng.RandfRange(-2f, 2f));
        chest.RotateY(_rng.Randf() * Mathf.Tau);
        _propContainer?.AddChild(chest);

        // Skull pile for atmosphere
        var skulls = Cosmetic3D.Create("skull_pile",
            new Color(0.9f, 0.85f, 0.75f),  // Bone color
            new Color(0.8f, 0.75f, 0.65f),
            new Color(0.7f, 0.65f, 0.55f),
            _rng.Randf(), 1f, true);
        skulls.Position = campCenter + new Vector3(_rng.RandfRange(-4f, -2f), 0, _rng.RandfRange(-3f, 3f));
        _propContainer?.AddChild(skulls);

        // Barrel or crate for supplies
        string[] supplyTypes = { "barrel", "crate", "barrel" };
        int supplyCount = _rng.RandiRange(2, 4);
        for (int i = 0; i < supplyCount; i++)
        {
            string supplyType = supplyTypes[_rng.RandiRange(0, supplyTypes.Length - 1)];
            var supply = Cosmetic3D.Create(supplyType,
                new Color(0.55f, 0.38f, 0.22f),
                new Color(0.45f, 0.3f, 0.18f),
                new Color(0.4f, 0.4f, 0.45f),
                _rng.Randf(), _rng.RandfRange(0.8f, 1.2f), true);

            float angle = _rng.Randf() * Mathf.Tau;
            float dist = _rng.RandfRange(4f, 6f);
            supply.Position = campCenter + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
            supply.RotateY(_rng.Randf() * Mathf.Tau);
            _propContainer?.AddChild(supply);
        }

        // Cauldron for cooking
        if (_rng.Randf() > 0.3f)
        {
            var cauldron = Cosmetic3D.Create("cauldron",
                new Color(0.25f, 0.22f, 0.2f),
                new Color(0.2f, 0.18f, 0.15f),
                new Color(0.35f, 0.35f, 0.4f),
                _rng.Randf(), 1f, true);
            cauldron.Position = campCenter + new Vector3(_rng.RandfRange(2f, 4f), 0, _rng.RandfRange(-2f, 2f));
            _propContainer?.AddChild(cauldron);
        }

        GD.Print($"[DungeonGenerator3D] Spawned goblin camp furniture at {campCenter}");
    }

    // ============================================
    // LARGE DUNGEON GENERATION (10x Size)
    // ============================================

    /// <summary>
    /// Generate a massive dungeon with many rooms, all monster types,
    /// floor/wall/ceiling variety, and atmospheric props.
    /// </summary>
    private void GenerateLargeDungeon()
    {
        GD.Print($"[DungeonGenerator3D] Generating LARGE DUNGEON: {LargeDungeonWidth}x{LargeDungeonDepth} with {LargeDungeonRooms} rooms");

        ClearDungeon();

        // Initialize large map data
        _mapData = new int[LargeDungeonWidth, 2, LargeDungeonDepth];
        Rooms.Clear();

        // Store original dungeon dimensions
        int origWidth = DungeonWidth;
        int origDepth = DungeonDepth;
        DungeonWidth = LargeDungeonWidth;
        DungeonDepth = LargeDungeonDepth;

        // Generate many rooms with varied sizes
        GenerateLargeDungeonRooms();

        // Connect rooms with wide corridors
        ConnectRoomsLarge();

        // Build 3D geometry with tile variety
        BuildLargeDungeonGeometry();

        // Add ceilings to all areas
        CreateLargeDungeonCeilings();

        // Populate rooms with themed props (including new dungeon props)
        PopulateLargeDungeonRooms();

        // Spawn varied monsters with level scaling
        SpawnLargeDungeonEnemies();

        // Add atmospheric effects
        AddLargeDungeonAtmosphere();

        // Restore original dimensions
        DungeonWidth = origWidth;
        DungeonDepth = origDepth;

        GD.Print($"[DungeonGenerator3D] Large dungeon generated: {Rooms.Count} rooms");
        EmitSignal(SignalName.GenerationComplete);
    }

    private void GenerateLargeDungeonRooms()
    {
        int attempts = LargeDungeonRooms * 5;
        int minSize = 10;
        int maxSize = 30;

        // Create spawn room first (larger, in center area)
        var spawnRoom = new Room
        {
            Position = new Vector3I(LargeDungeonWidth / 2 - 15, 0, LargeDungeonDepth / 2 - 15),
            Size = new Vector3I(30, 1, 30),
            Type = "spawn"
        };
        Rooms.Add(spawnRoom);
        _spawnRoom = spawnRoom;
        MarkRoomOnMap(spawnRoom);

        // Generate regular rooms
        for (int i = 0; i < attempts && Rooms.Count < LargeDungeonRooms; i++)
        {
            int width = _rng.RandiRange(minSize, maxSize);
            int depth = _rng.RandiRange(minSize, maxSize);
            int x = _rng.RandiRange(5, LargeDungeonWidth - width - 5);
            int z = _rng.RandiRange(5, LargeDungeonDepth - depth - 5);

            var newRoom = new Room
            {
                Position = new Vector3I(x, 0, z),
                Size = new Vector3I(width, 1, depth),
                Type = GetRandomLargeRoomType()
            };

            // Check for overlap with padding
            bool overlaps = false;
            foreach (var room in Rooms)
            {
                if (RoomsOverlap(newRoom, room, 4))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                Rooms.Add(newRoom);
                MarkRoomOnMap(newRoom);
            }
        }

        GD.Print($"[DungeonGenerator3D] Created {Rooms.Count} rooms");
    }

    private void MarkRoomOnMap(Room room)
    {
        for (int rx = room.Position.X; rx < room.Position.X + room.Size.X; rx++)
        {
            for (int rz = room.Position.Z; rz < room.Position.Z + room.Size.Z; rz++)
            {
                if (rx >= 0 && rx < LargeDungeonWidth && rz >= 0 && rz < LargeDungeonDepth)
                {
                    _mapData![rx, 0, rz] = 1;
                }
            }
        }
    }

    private string GetRandomLargeRoomType()
    {
        string[] types = {
            "crypt", "cathedral", "storage", "library", "armory",
            "torture", "throne", "prison", "alchemy", "barracks"
        };
        return types[_rng.RandiRange(0, types.Length - 1)];
    }

    private void ConnectRoomsLarge()
    {
        int corridorW = 6; // Wide corridors

        // Connect each room to nearest few rooms using MST-like approach
        for (int i = 1; i < Rooms.Count; i++)
        {
            // Connect to previous room
            ConnectTwoRooms(Rooms[i - 1], Rooms[i], corridorW);

            // Sometimes connect to a random earlier room for loops
            if (_rng.Randf() > 0.6f && i > 2)
            {
                int randomIdx = _rng.RandiRange(0, i - 2);
                ConnectTwoRooms(Rooms[randomIdx], Rooms[i], corridorW);
            }
        }
    }

    private void ConnectTwoRooms(Room from, Room to, int corridorWidth)
    {
        Vector3I fromCenter = from.Position + from.Size / 2;
        Vector3I toCenter = to.Position + to.Size / 2;

        // L-shaped corridor
        int minX = Mathf.Min(fromCenter.X, toCenter.X);
        int maxX = Mathf.Max(fromCenter.X, toCenter.X);
        for (int x = minX; x <= maxX; x++)
        {
            for (int w = -corridorWidth / 2; w <= corridorWidth / 2; w++)
            {
                int z = fromCenter.Z + w;
                if (z >= 0 && z < LargeDungeonDepth && x >= 0 && x < LargeDungeonWidth)
                {
                    _mapData![x, 0, z] = 1;
                }
            }
        }

        int minZ = Mathf.Min(fromCenter.Z, toCenter.Z);
        int maxZ = Mathf.Max(fromCenter.Z, toCenter.Z);
        for (int z = minZ; z <= maxZ; z++)
        {
            for (int w = -corridorWidth / 2; w <= corridorWidth / 2; w++)
            {
                int x = toCenter.X + w;
                if (z >= 0 && z < LargeDungeonDepth && x >= 0 && x < LargeDungeonWidth)
                {
                    _mapData![x, 0, z] = 1;
                }
            }
        }
    }

    private void BuildLargeDungeonGeometry()
    {
        // Use chunked floor generation for performance
        int chunkSize = 50;

        for (int chunkX = 0; chunkX < LargeDungeonWidth; chunkX += chunkSize)
        {
            for (int chunkZ = 0; chunkZ < LargeDungeonDepth; chunkZ += chunkSize)
            {
                BuildChunk(chunkX, chunkZ, Mathf.Min(chunkSize, LargeDungeonWidth - chunkX),
                    Mathf.Min(chunkSize, LargeDungeonDepth - chunkZ));
            }
        }
    }

    private void BuildChunk(int startX, int startZ, int width, int depth)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int z = startZ; z < startZ + depth; z++)
            {
                if (_mapData![x, 0, z] == 1)
                {
                    CreateFloorTile(x, z);

                    // Walls at void boundaries
                    bool needsLeft = x > 0 && _mapData[x - 1, 0, z] == 0;
                    bool needsRight = x < LargeDungeonWidth - 1 && _mapData[x + 1, 0, z] == 0;
                    bool needsBack = z > 0 && _mapData[x, 0, z - 1] == 0;
                    bool needsFront = z < LargeDungeonDepth - 1 && _mapData[x, 0, z + 1] == 0;

                    if (needsLeft) CreateWall(x, z, Vector3.Left);
                    if (needsRight) CreateWall(x, z, Vector3.Right);
                    if (needsBack) CreateWall(x, z, Vector3.Back);
                    if (needsFront) CreateWall(x, z, Vector3.Forward);
                }
            }
        }
    }

    private void CreateLargeDungeonCeilings()
    {
        // Create ceiling sections for each room
        foreach (var room in Rooms)
        {
            var ceiling = new MeshInstance3D();
            var planeMesh = new PlaneMesh();
            planeMesh.Size = new Vector2(room.Size.X * TileSize, room.Size.Z * TileSize);
            ceiling.Mesh = planeMesh;
            ceiling.MaterialOverride = _ceilingMaterial;

            Vector3 center = new Vector3(
                (room.Position.X + room.Size.X / 2f) * TileSize,
                WallHeight,
                (room.Position.Z + room.Size.Z / 2f) * TileSize
            );
            ceiling.Position = center;
            ceiling.RotationDegrees = new Vector3(180, 0, 0);
            _floorContainer?.AddChild(ceiling);
        }
    }

    private void PopulateLargeDungeonRooms()
    {
        foreach (var room in Rooms)
        {
            if (room == _spawnRoom) continue;

            // Props based on room type
            int propCount = GetLargeRoomPropCount(room);
            for (int i = 0; i < propCount; i++)
            {
                TryPlaceLargeDungeonProp(room);
            }

            // Torches for lighting
            int torchCount = (room.Size.X + room.Size.Z) / 8;
            PlaceTorches(room, torchCount);

            // Add new dungeon props randomly
            TryPlaceNewDungeonProps(room);
        }
    }

    private int GetLargeRoomPropCount(Room room)
    {
        return room.Type switch
        {
            "storage" => _rng.RandiRange(8, 15),
            "library" => _rng.RandiRange(6, 12),
            "cathedral" => _rng.RandiRange(4, 8),
            "throne" => _rng.RandiRange(3, 6),
            "torture" => _rng.RandiRange(5, 10),
            "prison" => _rng.RandiRange(4, 8),
            "alchemy" => _rng.RandiRange(6, 12),
            "barracks" => _rng.RandiRange(6, 10),
            _ => _rng.RandiRange(4, 10)
        };
    }

    private void TryPlaceLargeDungeonProp(Room room)
    {
        int x = room.Position.X + _rng.RandiRange(2, room.Size.X - 3);
        int z = room.Position.Z + _rng.RandiRange(2, room.Size.Z - 3);

        string propType = GetLargeRoomPropType(room);
        Color primary = GetPropColor(propType);

        var prop = Cosmetic3D.Create(propType, primary, primary.Darkened(0.2f),
            new Color(0.4f, 0.4f, 0.45f), _rng.Randf(), 1f, true);
        prop.Position = new Vector3(x * TileSize + TileSize / 2f, 0, z * TileSize + TileSize / 2f);
        prop.RotateY(_rng.Randf() * Mathf.Tau);

        _propContainer?.AddChild(prop);
        room.Props.Add(prop);
    }

    private string GetLargeRoomPropType(Room room)
    {
        string[] props = room.Type switch
        {
            "storage" => new[] { "barrel", "crate", "crate", "pot", "broken_barrel" },
            "library" => new[] { "bookshelf", "table", "chair", "ancient_scroll", "pot" },
            "armory" => new[] { "chest", "discarded_sword", "forgotten_shield", "crate" },
            "cathedral" => new[] { "pillar", "statue", "torch", "altar_stone", "brazier_fire" },
            "crypt" => new[] { "bone_pile", "skull_pile", "chest", "crystal", "blood_pool" },
            "torture" => new[] { "manacles", "spike_trap", "blood_pool", "coiled_chains", "bone_pile" },
            "throne" => new[] { "pillar", "statue", "treasure_chest", "scattered_coins", "brazier_fire" },
            "prison" => new[] { "manacles", "coiled_chains", "rat_nest", "broken_pottery", "moldy_bread" },
            "alchemy" => new[] { "cauldron", "shattered_potion", "glowing_mushrooms", "pot", "table" },
            "barracks" => new[] { "crate", "barrel", "table", "chair", "discarded_sword", "forgotten_shield" },
            _ => new[] { "barrel", "crate", "pot" }
        };
        return props[_rng.RandiRange(0, props.Length - 1)];
    }

    private void TryPlaceNewDungeonProps(Room room)
    {
        // Random chance to place atmospheric floor props
        int floorPropCount = _rng.RandiRange(1, 4);
        string[] floorProps = { "moss_patch", "water_puddle", "cracked_tiles", "rubble_heap", "thorny_vines" };

        for (int i = 0; i < floorPropCount; i++)
        {
            if (_rng.Randf() > 0.4f) continue;

            int x = room.Position.X + _rng.RandiRange(1, room.Size.X - 2);
            int z = room.Position.Z + _rng.RandiRange(1, room.Size.Z - 2);

            string propType = floorProps[_rng.RandiRange(0, floorProps.Length - 1)];
            Color primary = GetNewPropColor(propType);

            var prop = Cosmetic3D.Create(propType, primary, primary.Darkened(0.2f),
                new Color(0.4f, 0.4f, 0.45f), _rng.Randf(), 1f, true);
            prop.Position = new Vector3(x * TileSize + TileSize / 2f, 0, z * TileSize + TileSize / 2f);

            _propContainer?.AddChild(prop);
        }

        // Chance for special glowing props
        if (_rng.Randf() > 0.7f)
        {
            int x = room.Position.X + _rng.RandiRange(1, room.Size.X - 2);
            int z = room.Position.Z + _rng.RandiRange(1, room.Size.Z - 2);

            string[] glowProps = { "glowing_mushrooms", "engraved_rune", "shattered_potion" };
            string propType = glowProps[_rng.RandiRange(0, glowProps.Length - 1)];
            Color primary = GetNewPropColor(propType);

            var prop = Cosmetic3D.Create(propType, primary, primary.Darkened(0.2f),
                new Color(0.4f, 0.4f, 0.45f), _rng.Randf(), 1f, true);
            prop.Position = new Vector3(x * TileSize + TileSize / 2f, 0, z * TileSize + TileSize / 2f);

            _propContainer?.AddChild(prop);
        }
    }

    private Color GetNewPropColor(string propType)
    {
        return propType switch
        {
            "bone_pile" => new Color(0.9f, 0.85f, 0.7f),
            "coiled_chains" => new Color(0.35f, 0.32f, 0.28f),
            "moss_patch" => new Color(0.2f, 0.45f, 0.15f),
            "water_puddle" => new Color(0.2f, 0.3f, 0.4f),
            "treasure_chest" => new Color(0.55f, 0.4f, 0.2f),
            "broken_barrel" => new Color(0.5f, 0.35f, 0.2f),
            "altar_stone" => new Color(0.35f, 0.33f, 0.3f),
            "glowing_mushrooms" => new Color(0.3f, 0.8f, 0.7f),
            "blood_pool" => new Color(0.4f, 0.05f, 0.05f),
            "spike_trap" => new Color(0.2f, 0.2f, 0.22f),
            "discarded_sword" => new Color(0.5f, 0.48f, 0.45f),
            "shattered_potion" => new Color(0.2f, 0.8f, 0.3f),
            "ancient_scroll" => new Color(0.85f, 0.8f, 0.65f),
            "brazier_fire" => new Color(0.25f, 0.23f, 0.2f),
            "manacles" => new Color(0.4f, 0.35f, 0.3f),
            "cracked_tiles" => new Color(0.35f, 0.33f, 0.3f),
            "rubble_heap" => new Color(0.4f, 0.38f, 0.35f),
            "thorny_vines" => new Color(0.25f, 0.35f, 0.2f),
            "engraved_rune" => new Color(0.35f, 0.32f, 0.3f),
            "rat_nest" => new Color(0.4f, 0.35f, 0.28f),
            "broken_pottery" => new Color(0.7f, 0.55f, 0.4f),
            "scattered_coins" => new Color(0.85f, 0.7f, 0.2f),
            "forgotten_shield" => new Color(0.5f, 0.4f, 0.3f),
            "moldy_bread" => new Color(0.55f, 0.5f, 0.4f),
            _ => new Color(0.5f, 0.4f, 0.3f)
        };
    }

    private void SpawnLargeDungeonEnemies()
    {
        int totalEnemies = 0;
        int totalBosses = 0;

        for (int i = 0; i < Rooms.Count; i++)
        {
            var room = Rooms[i];
            if (room == _spawnRoom) continue;

            // Calculate room distance from spawn for level scaling
            Vector3I spawnCenter = _spawnRoom!.Position + _spawnRoom.Size / 2;
            Vector3I roomCenter = room.Position + room.Size / 2;
            float distance = Mathf.Sqrt(
                Mathf.Pow(roomCenter.X - spawnCenter.X, 2) +
                Mathf.Pow(roomCenter.Z - spawnCenter.Z, 2)
            );

            // Level based on distance from spawn (1-10)
            int roomLevel = Mathf.Clamp((int)(distance / 40f) + 1, 1, 10);

            // Select monster type for this room
            string monsterType = AllMonsterTypes[i % AllMonsterTypes.Length];

            // Fixed enemy count: 4-5 enemies per room (one group)
            int roomArea = room.Size.X * room.Size.Z;
            int enemyCount = _rng.RandiRange(4, 5);

            // Special handling for barracks/throne rooms - spawn goblin cluster instead
            if (room.Type == "barracks" || room.Type == "throne")
            {
                SpawnGoblinCluster(room, roomLevel);
                totalEnemies += 5; // Goblin cluster is always 5 enemies
                continue;
            }

            // Large rooms get a boss as part of the group (replacing one regular enemy)
            bool hasBoss = roomArea > 100 && _rng.Randf() > 0.7f;
            int regularCount = hasBoss ? enemyCount - 1 : enemyCount;

            // Spawn regular enemies
            for (int e = 0; e < regularCount; e++)
            {
                int x = room.Position.X + _rng.RandiRange(2, room.Size.X - 3);
                int z = room.Position.Z + _rng.RandiRange(2, room.Size.Z - 3);
                Vector3 pos = new Vector3(x * TileSize + TileSize / 2f, 0.5f, z * TileSize + TileSize / 2f);

                var enemy = CreateMonsterByType(monsterType, pos, roomLevel);
                enemy.AddToGroup("Enemies");
                _enemyContainer?.AddChild(enemy);
                room.Enemies.Add(enemy);
                totalEnemies++;
            }

            // Spawn boss as part of the group
            if (hasBoss)
            {
                int bossX = room.Position.X + room.Size.X / 2;
                int bossZ = room.Position.Z + room.Size.Z / 2;
                Vector3 bossPos = new Vector3(bossX * TileSize, 0.5f, bossZ * TileSize);

                string bossType = BossMonsterTypes[i % BossMonsterTypes.Length];
                var boss = BossEnemy3D.Create(bossType, bossPos);
                boss.AddToGroup("Enemies");
                _enemyContainer?.AddChild(boss);
                room.Enemies.Add(boss);
                totalBosses++;
            }
        }

        GD.Print($"[DungeonGenerator3D] Spawned {totalEnemies} enemies and {totalBosses} bosses");
    }

    private void SpawnGoblinCluster(Room room, int level = 1)
    {
        Vector3 center = new Vector3(
            (room.Position.X + room.Size.X / 2f) * TileSize,
            0.5f,
            (room.Position.Z + room.Size.Z / 2f) * TileSize
        );

        // Mixed group: 5 enemies total (1 boss + 4 regular monsters from Editor)

        // Random Editor boss
        string bossType = BossMonsterTypes[_rng.RandiRange(0, BossMonsterTypes.Length - 1)];
        var boss = BossEnemy3D.Create(bossType, center);
        boss.AddToGroup("Enemies");
        _enemyContainer?.AddChild(boss);
        room.Enemies.Add(boss);

        // 4 random regular monsters from Editor list
        for (int m = 0; m < 4; m++)
        {
            string monsterType = AllMonsterTypes[_rng.RandiRange(0, AllMonsterTypes.Length - 1)];
            Vector3 offset = new Vector3(_rng.RandfRange(-4f, 4f), 0, _rng.RandfRange(-4f, 4f));
            var enemy = CreateMonsterByType(monsterType, center + offset, level);
            enemy.AddToGroup("Enemies");
            _enemyContainer?.AddChild(enemy);
            room.Enemies.Add(enemy);
        }
    }

    /// <summary>
    /// Create a monster by type name, handling special cases for GoblinShaman and GoblinThrower
    /// which are separate classes from BasicEnemy3D.
    /// </summary>
    private Node3D CreateMonsterByType(string monsterType, Vector3 position, int level = 1)
    {
        switch (monsterType.ToLower())
        {
            case "goblin_shaman":
                var shaman = new Enemies.GoblinShaman();
                shaman.Position = position;
                return shaman;

            case "goblin_thrower":
                var thrower = new Enemies.GoblinThrower();
                thrower.Position = position;
                return thrower;

            default:
                var enemy = BasicEnemy3D.Create(monsterType, position, level);
                return enemy;
        }
    }

    private void AddLargeDungeonAtmosphere()
    {
        // Ambient directional light - brighter warm glow
        var ambient = new DirectionalLight3D();
        ambient.LightColor = new Color(0.22f, 0.18f, 0.12f); // Warmer, brighter amber
        ambient.LightEnergy = 0.3f; // Much brighter for visibility
        ambient.Rotation = new Vector3(Mathf.DegToRad(-60), Mathf.DegToRad(30), 0);
        ambient.ShadowEnabled = false;
        _lightContainer?.AddChild(ambient);

        // Secondary fill light for depth
        var fillAmbient = new DirectionalLight3D();
        fillAmbient.LightColor = new Color(0.12f, 0.15f, 0.2f); // Cool blue fill
        fillAmbient.LightEnergy = 0.12f;
        fillAmbient.Rotation = new Vector3(Mathf.DegToRad(20), Mathf.DegToRad(-90), 0);
        fillAmbient.ShadowEnabled = false;
        _lightContainer?.AddChild(fillAmbient);

        // Add warm torchlight ambient in corridors - like flickering wall sconces
        // Sample random corridor positions
        int lightCount = 0;
        for (int attempts = 0; attempts < 200 && lightCount < 50; attempts++)
        {
            int x = _rng.RandiRange(0, LargeDungeonWidth - 1);
            int z = _rng.RandiRange(0, LargeDungeonDepth - 1);

            if (_mapData![x, 0, z] == 1)
            {
                // Check if it's in a corridor (not in a room)
                bool inRoom = false;
                foreach (var room in Rooms)
                {
                    if (x >= room.Position.X && x < room.Position.X + room.Size.X &&
                        z >= room.Position.Z && z < room.Position.Z + room.Size.Z)
                    {
                        inRoom = true;
                        break;
                    }
                }

                if (!inRoom && _rng.Randf() > 0.6f) // More frequent lights
                {
                    // Warm torchlight colors - vary between orange and yellow-orange
                    float warmth = _rng.Randf() * 0.15f;
                    var torchColor = new Color(0.95f, 0.65f + warmth, 0.3f + warmth * 0.5f);

                    var light = CreateOptimizedLight(
                        new Vector3(x * TileSize, WallHeight * 0.5f, z * TileSize),
                        torchColor,
                        1.2f, // Much brighter corridor lights
                        14f,  // Larger range for better coverage
                        enableShadow: false
                    );
                    _lightContainer?.AddChild(light);
                    lightCount++;
                }
            }
        }

        GD.Print($"[DungeonGenerator3D] Added {lightCount} atmospheric corridor lights");

        // Add wall-mounted torches for dramatic lighting
        AddWallTorches();

        // Add dramatic shadow-casting lights at key intersections
        AddDramaticLighting();
    }

    /// <summary>
    /// Places wall-mounted torches along corridor walls at regular intervals.
    /// Creates pools of warm light with dark spaces between for atmosphere.
    /// </summary>
    private void AddWallTorches()
    {
        int torchCount = 0;
        int torchSpacing = 8; // Place torches every 8 tiles for good light pooling

        // Find wall-adjacent corridor tiles for torch placement
        for (int x = 1; x < LargeDungeonWidth - 1; x++)
        {
            for (int z = 1; z < LargeDungeonDepth - 1; z++)
            {
                // Skip if not a floor tile
                if (_mapData![x, 0, z] != 1) continue;

                // Check if we're at a torch spacing interval
                if ((x + z) % torchSpacing != 0) continue;

                // Skip if inside a room (rooms get their own lighting)
                bool inRoom = false;
                foreach (var room in Rooms)
                {
                    if (x >= room.Position.X && x < room.Position.X + room.Size.X &&
                        z >= room.Position.Z && z < room.Position.Z + room.Size.Z)
                    {
                        inRoom = true;
                        break;
                    }
                }
                if (inRoom) continue;

                // Check adjacent walls and place torch on wall
                Vector3? torchPos = null;
                float torchRotation = 0f;

                // Check each direction for a wall
                if (_mapData[x - 1, 0, z] == 0) // Wall to west
                {
                    torchPos = new Vector3((x - 0.4f) * TileSize, WallHeight * 0.45f, z * TileSize);
                    torchRotation = Mathf.Pi * 0.5f; // Face east
                }
                else if (_mapData[x + 1, 0, z] == 0) // Wall to east
                {
                    torchPos = new Vector3((x + 0.4f) * TileSize, WallHeight * 0.45f, z * TileSize);
                    torchRotation = -Mathf.Pi * 0.5f; // Face west
                }
                else if (_mapData[x, 0, z - 1] == 0) // Wall to north
                {
                    torchPos = new Vector3(x * TileSize, WallHeight * 0.45f, (z - 0.4f) * TileSize);
                    torchRotation = 0f; // Face south
                }
                else if (_mapData[x, 0, z + 1] == 0) // Wall to south
                {
                    torchPos = new Vector3(x * TileSize, WallHeight * 0.45f, (z + 0.4f) * TileSize);
                    torchRotation = Mathf.Pi; // Face north
                }

                if (torchPos.HasValue)
                {
                    // Create flickering torch light
                    var flickerLight = new FlickeringTorchLight();
                    flickerLight.Position = torchPos.Value;

                    // Vary torch color slightly for realism
                    float warmth = _rng.Randf() * 0.1f;
                    flickerLight.BaseColor = new Color(1f, 0.65f + warmth, 0.3f + warmth * 0.5f);
                    flickerLight.BaseEnergy = 1.8f + _rng.Randf() * 0.4f;
                    flickerLight.LightRange = 12f;

                    // Every 4th torch gets shadows for dramatic effect
                    flickerLight.EnableShadows = (torchCount % 4 == 0) && _shadowLightCount < MaxShadowLights;
                    if (flickerLight.EnableShadows) _shadowLightCount++;

                    _lightContainer?.AddChild(flickerLight);
                    torchCount++;

                    // Add a simple torch mesh (bracket + flame glow)
                    var torchMesh = CreateSimpleTorchMesh();
                    torchMesh.Position = torchPos.Value - new Vector3(0, 0.3f, 0);
                    torchMesh.Rotation = new Vector3(0, torchRotation, 0);
                    _propContainer?.AddChild(torchMesh);
                }
            }
        }

        GD.Print($"[DungeonGenerator3D] Added {torchCount} wall torches");
    }

    /// <summary>
    /// Creates a simple torch mesh with bracket and flame effect.
    /// </summary>
    private Node3D CreateSimpleTorchMesh()
    {
        var torch = new Node3D();

        // Iron bracket
        var bracketMesh = new MeshInstance3D();
        var bracket = new BoxMesh();
        bracket.Size = new Vector3(0.08f, 0.3f, 0.15f);
        bracketMesh.Mesh = bracket;
        bracketMesh.Position = new Vector3(0, 0, 0.1f);

        var bracketMat = new StandardMaterial3D();
        bracketMat.AlbedoColor = new Color(0.15f, 0.12f, 0.1f);
        bracketMat.Roughness = 0.7f;
        bracketMat.Metallic = 0.3f;
        bracketMesh.MaterialOverride = bracketMat;
        torch.AddChild(bracketMesh);

        // Torch handle
        var handleMesh = new MeshInstance3D();
        var handle = new CylinderMesh();
        handle.TopRadius = 0.03f;
        handle.BottomRadius = 0.04f;
        handle.Height = 0.4f;
        handleMesh.Mesh = handle;
        handleMesh.Position = new Vector3(0, 0.05f, 0);
        handleMesh.Rotation = new Vector3(Mathf.Pi * 0.15f, 0, 0); // Angle outward

        var handleMat = new StandardMaterial3D();
        handleMat.AlbedoColor = new Color(0.25f, 0.18f, 0.1f);
        handleMat.Roughness = 0.9f;
        handleMesh.MaterialOverride = handleMat;
        torch.AddChild(handleMesh);

        // Flame glow (emissive sphere)
        var flameMesh = new MeshInstance3D();
        var flame = new SphereMesh();
        flame.Radius = 0.12f;
        flame.Height = 0.24f;
        flameMesh.Mesh = flame;
        flameMesh.Position = new Vector3(0, 0.35f, -0.15f);

        var flameMat = new StandardMaterial3D();
        flameMat.AlbedoColor = new Color(1f, 0.6f, 0.1f);
        flameMat.EmissionEnabled = true;
        flameMat.Emission = new Color(1f, 0.5f, 0.1f);
        flameMat.EmissionEnergyMultiplier = 3f;
        flameMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        flameMat.AlbedoColor = new Color(1f, 0.7f, 0.2f, 0.7f);
        flameMesh.MaterialOverride = flameMat;
        torch.AddChild(flameMesh);

        return torch;
    }

    /// <summary>
    /// Adds dramatic shadow-casting lights at corridor intersections and key points.
    /// </summary>
    private void AddDramaticLighting()
    {
        int dramaticLightCount = 0;

        // Find corridor intersections (tiles with 3+ open neighbors)
        for (int x = 2; x < LargeDungeonWidth - 2; x += 6) // Check every 6 tiles
        {
            for (int z = 2; z < LargeDungeonDepth - 2; z += 6)
            {
                if (_mapData![x, 0, z] != 1) continue;

                // Count open neighbors
                int openNeighbors = 0;
                if (_mapData[x - 1, 0, z] == 1) openNeighbors++;
                if (_mapData[x + 1, 0, z] == 1) openNeighbors++;
                if (_mapData[x, 0, z - 1] == 1) openNeighbors++;
                if (_mapData[x, 0, z + 1] == 1) openNeighbors++;

                // Intersections get dramatic overhead lights
                if (openNeighbors >= 3 && _shadowLightCount < MaxShadowLights)
                {
                    var light = new OmniLight3D();
                    light.Position = new Vector3(x * TileSize, WallHeight * 0.7f, z * TileSize);
                    light.LightColor = new Color(1f, 0.8f, 0.5f); // Warm golden
                    light.LightEnergy = 4f; // Much brighter
                    light.OmniRange = 18f; // Wider coverage
                    light.OmniAttenuation = 1.1f; // Softer falloff
                    light.ShadowEnabled = true;
                    light.DistanceFadeEnabled = true;
                    light.DistanceFadeBegin = 45f;
                    light.DistanceFadeLength = 25f;

                    _lightContainer?.AddChild(light);
                    _shadowLightCount++;
                    dramaticLightCount++;
                }
            }
        }

        // Add subtle blue-ish rim lights near ceiling for depth
        for (int i = 0; i < 20; i++)
        {
            int x = _rng.RandiRange(5, LargeDungeonWidth - 5);
            int z = _rng.RandiRange(5, LargeDungeonDepth - 5);

            if (_mapData![x, 0, z] == 1)
            {
                var rimLight = CreateOptimizedLight(
                    new Vector3(x * TileSize, WallHeight * 0.9f, z * TileSize),
                    new Color(0.4f, 0.5f, 0.7f), // Cool blue for contrast
                    0.3f, // Very subtle
                    6f,
                    enableShadow: false
                );
                _lightContainer?.AddChild(rimLight);
            }
        }

        GD.Print($"[DungeonGenerator3D] Added {dramaticLightCount} dramatic shadow lights + 20 rim lights");

        // Add recessed ceiling lights
        AddRecessedCeilingLights();
    }

    /// <summary>
    /// Adds recessed light fixtures in the ceiling that shine downward.
    /// Creates dramatic pools of light on the floor.
    /// </summary>
    private void AddRecessedCeilingLights()
    {
        int lightCount = 0;
        int spacing = 12; // Every 12 tiles

        for (int x = spacing / 2; x < LargeDungeonWidth; x += spacing)
        {
            for (int z = spacing / 2; z < LargeDungeonDepth; z += spacing)
            {
                if (_mapData![x, 0, z] != 1) continue;

                // Skip if in a room (rooms have their own lighting)
                bool inRoom = false;
                foreach (var room in Rooms)
                {
                    if (x >= room.Position.X && x < room.Position.X + room.Size.X &&
                        z >= room.Position.Z && z < room.Position.Z + room.Size.Z)
                    {
                        inRoom = true;
                        break;
                    }
                }
                if (inRoom) continue;

                // Create recessed light fixture
                var fixture = CreateRecessedLightFixture();
                fixture.Position = new Vector3(x * TileSize, WallHeight - 0.1f, z * TileSize);
                _propContainer?.AddChild(fixture);

                // Create downward-facing spot light
                var spotLight = new SpotLight3D();
                spotLight.Position = new Vector3(x * TileSize, WallHeight - 0.2f, z * TileSize);
                spotLight.Rotation = new Vector3(Mathf.Pi / 2f, 0, 0); // Point straight down

                // Warm light color with slight variation
                float warmth = _rng.Randf() * 0.1f;
                spotLight.LightColor = new Color(1f, 0.8f + warmth, 0.5f + warmth);
                spotLight.LightEnergy = 1.5f + _rng.Randf() * 0.5f;
                spotLight.SpotRange = 15f;
                spotLight.SpotAngle = 35f; // Focused cone
                spotLight.SpotAttenuation = 0.8f;

                // Performance settings
                spotLight.DistanceFadeEnabled = true;
                spotLight.DistanceFadeBegin = 30f;
                spotLight.DistanceFadeLength = 15f;
                spotLight.ShadowEnabled = false; // Performance

                _lightContainer?.AddChild(spotLight);
                lightCount++;
            }
        }

        GD.Print($"[DungeonGenerator3D] Added {lightCount} recessed ceiling lights");
    }

    /// <summary>
    /// Creates a recessed light fixture mesh for the ceiling.
    /// </summary>
    private Node3D CreateRecessedLightFixture()
    {
        var fixture = new Node3D();

        // Recessed housing (dark metal ring)
        var housingMesh = new MeshInstance3D();
        var housing = new TorusMesh();
        housing.InnerRadius = 0.25f;
        housing.OuterRadius = 0.4f;
        housing.Rings = 16;
        housing.RingSegments = 12;
        housingMesh.Mesh = housing;
        housingMesh.Rotation = new Vector3(Mathf.Pi / 2f, 0, 0); // Horizontal

        var housingMat = new StandardMaterial3D();
        housingMat.AlbedoColor = new Color(0.12f, 0.10f, 0.08f);
        housingMat.Roughness = 0.6f;
        housingMat.Metallic = 0.4f;
        housingMesh.MaterialOverride = housingMat;
        fixture.AddChild(housingMesh);

        // Inner reflector (brighter metal)
        var reflectorMesh = new MeshInstance3D();
        var reflector = new CylinderMesh();
        reflector.TopRadius = 0.3f;
        reflector.BottomRadius = 0.2f;
        reflector.Height = 0.15f;
        reflectorMesh.Mesh = reflector;
        reflectorMesh.Position = new Vector3(0, 0.08f, 0);

        var reflectorMat = new StandardMaterial3D();
        reflectorMat.AlbedoColor = new Color(0.5f, 0.45f, 0.35f);
        reflectorMat.Roughness = 0.3f;
        reflectorMat.Metallic = 0.6f;
        reflectorMesh.MaterialOverride = reflectorMat;
        fixture.AddChild(reflectorMesh);

        // Glowing center (emissive)
        var glowMesh = new MeshInstance3D();
        var glow = new SphereMesh();
        glow.Radius = 0.12f;
        glow.Height = 0.24f;
        glowMesh.Mesh = glow;
        glowMesh.Position = new Vector3(0, 0.05f, 0);

        var glowMat = new StandardMaterial3D();
        glowMat.AlbedoColor = new Color(1f, 0.9f, 0.7f, 0.9f);
        glowMat.EmissionEnabled = true;
        glowMat.Emission = new Color(1f, 0.85f, 0.5f);
        glowMat.EmissionEnergyMultiplier = 2f;
        glowMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        glowMesh.MaterialOverride = glowMat;
        fixture.AddChild(glowMesh);

        return fixture;
    }

    private void ClearDungeon()
    {
        // Reset shadow light counter
        _shadowLightCount = 0;

        foreach (var child in _floorContainer?.GetChildren() ?? new Godot.Collections.Array<Node>())
            child.QueueFree();
        foreach (var child in _wallContainer?.GetChildren() ?? new Godot.Collections.Array<Node>())
            child.QueueFree();
        foreach (var child in _propContainer?.GetChildren() ?? new Godot.Collections.Array<Node>())
            child.QueueFree();
        foreach (var child in _lightContainer?.GetChildren() ?? new Godot.Collections.Array<Node>())
            child.QueueFree();
        foreach (var child in _enemyContainer?.GetChildren() ?? new Godot.Collections.Array<Node>())
            child.QueueFree();

        // Clear occluder
        _occluderInstance?.QueueFree();
        _occluderInstance = null;
        _occluderVertices.Clear();
        _occluderIndices.Clear();

        // Invalidate enemy prop cache when dungeon changes
        BasicEnemy3D.InvalidatePropCache();
    }

    private bool TryPlaceRoom()
    {
        int width = _rng.RandiRange(MinRoomSize, MaxRoomSize);
        int depth = _rng.RandiRange(MinRoomSize, MaxRoomSize);
        int x = _rng.RandiRange(1, DungeonWidth - width - 1);
        int z = _rng.RandiRange(1, DungeonDepth - depth - 1);

        // Check for overlap with existing rooms (with padding)
        var newRoom = new Room
        {
            Position = new Vector3I(x, 0, z),
            Size = new Vector3I(width, 1, depth),
            Type = GetRandomRoomType()
        };

        foreach (var room in Rooms)
        {
            if (RoomsOverlap(newRoom, room, 2)) // 2 tile padding
            {
                return false;
            }
        }

        // Place room
        Rooms.Add(newRoom);

        // Mark tiles as floor
        for (int rx = x; rx < x + width; rx++)
        {
            for (int rz = z; rz < z + depth; rz++)
            {
                _mapData[rx, 0, rz] = 1; // Floor
            }
        }

        // First room is spawn room
        if (Rooms.Count == 1)
        {
            _spawnRoom = newRoom;
        }

        return true;
    }

    private bool RoomsOverlap(Room a, Room b, int padding)
    {
        return !(a.Position.X + a.Size.X + padding < b.Position.X ||
                 b.Position.X + b.Size.X + padding < a.Position.X ||
                 a.Position.Z + a.Size.Z + padding < b.Position.Z ||
                 b.Position.Z + b.Size.Z + padding < a.Position.Z);
    }

    private string GetRandomRoomType()
    {
        string[] types = { "crypt", "cathedral", "storage", "library", "armory" };
        return types[_rng.RandiRange(0, types.Length - 1)];
    }

    private void ConnectRooms()
    {
        // Connect each room to the next with L-shaped corridors
        for (int i = 1; i < Rooms.Count; i++)
        {
            var from = Rooms[i - 1];
            var to = Rooms[i];

            // Get center points
            Vector3I fromCenter = from.Position + from.Size / 2;
            Vector3I toCenter = to.Position + to.Size / 2;

            // L-shaped corridor: horizontal then vertical
            // Horizontal segment
            int minX = Mathf.Min(fromCenter.X, toCenter.X);
            int maxX = Mathf.Max(fromCenter.X, toCenter.X);
            for (int x = minX; x <= maxX; x++)
            {
                for (int w = -CorridorWidth / 2; w <= CorridorWidth / 2; w++)
                {
                    int z = fromCenter.Z + w;
                    if (z >= 0 && z < DungeonDepth && x >= 0 && x < DungeonWidth)
                    {
                        _mapData[x, 0, z] = 1;
                    }
                }
            }

            // Vertical segment
            int minZ = Mathf.Min(fromCenter.Z, toCenter.Z);
            int maxZ = Mathf.Max(fromCenter.Z, toCenter.Z);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int w = -CorridorWidth / 2; w <= CorridorWidth / 2; w++)
                {
                    int x = toCenter.X + w;
                    if (z >= 0 && z < DungeonDepth && x >= 0 && x < DungeonWidth)
                    {
                        _mapData[x, 0, z] = 1;
                    }
                }
            }
        }
    }

    private void BuildGeometry()
    {
        // For deferred tile building, only build near spawn initially
        if (_useDeferredTileBuilding && _spawnRoom != null)
        {
            Vector3 spawnPos = GetSpawnPosition();
            int spawnTileX = (int)(spawnPos.X / TileSize);
            int spawnTileZ = (int)(spawnPos.Z / TileSize);
            int tileRadius = (int)(_deferredTileRadius / TileSize);

            int tilesBuilt = 0;
            for (int x = Mathf.Max(0, spawnTileX - tileRadius); x < Mathf.Min(DungeonWidth, spawnTileX + tileRadius); x++)
            {
                for (int z = Mathf.Max(0, spawnTileZ - tileRadius); z < Mathf.Min(DungeonDepth, spawnTileZ + tileRadius); z++)
                {
                    if (_mapData[x, 0, z] == 1)
                    {
                        BuildTileAt(x, z);
                        tilesBuilt++;
                    }
                }
            }
            _lastTileBuildPosition = spawnPos;
            GD.Print($"[DungeonGenerator3D] Built {tilesBuilt} tiles near spawn (deferred mode)");

            // Create ceiling and occluder
            CreateCeiling();
            BuildOccluder();
            return;
        }

        // Build floors and walls based on map data (full build for small maps)
        for (int x = 0; x < DungeonWidth; x++)
        {
            for (int z = 0; z < DungeonDepth; z++)
            {
                if (_mapData[x, 0, z] == 1) // Floor tile
                {
                    BuildTileAt(x, z);
                }
            }
        }

        // Create ceiling
        CreateCeiling();

        // PERF: Build occlusion culling mesh from collected wall geometry
        BuildOccluder();
    }

    /// <summary>
    /// Build geometry for a single tile (floor, walls, corner pillars).
    /// Used by both full build and deferred building.
    /// </summary>
    private void BuildTileAt(int x, int z)
    {
        // Skip if already built (for deferred building)
        if (_builtTiles != null)
        {
            if (_builtTiles.Contains((x, z))) return;
            _builtTiles.Add((x, z));
        }

        CreateFloorTile(x, z);

        // Check for walls (adjacent void tiles)
        bool needsLeft = x > 0 && _mapData[x - 1, 0, z] == 0;
        bool needsRight = x < DungeonWidth - 1 && _mapData[x + 1, 0, z] == 0;
        bool needsBack = z > 0 && _mapData[x, 0, z - 1] == 0;
        bool needsFront = z < DungeonDepth - 1 && _mapData[x, 0, z + 1] == 0;

        if (needsLeft) CreateWall(x, z, Vector3.Left);
        if (needsRight) CreateWall(x, z, Vector3.Right);
        if (needsBack) CreateWall(x, z, Vector3.Back);
        if (needsFront) CreateWall(x, z, Vector3.Forward);

        // Create corner pillars to fill gaps where walls meet
        bool voidTopLeft = (x > 0 && z > 0) && _mapData[x - 1, 0, z - 1] == 0;
        bool voidTopRight = (x < DungeonWidth - 1 && z > 0) && _mapData[x + 1, 0, z - 1] == 0;
        bool voidBottomLeft = (x > 0 && z < DungeonDepth - 1) && _mapData[x - 1, 0, z + 1] == 0;
        bool voidBottomRight = (x < DungeonWidth - 1 && z < DungeonDepth - 1) && _mapData[x + 1, 0, z + 1] == 0;

        // Corner pillar needed when diagonal is void and at least one adjacent edge needs wall
        if (voidTopLeft && (needsLeft || needsBack))
            CreateCornerPillar(x, z, -1, -1);
        if (voidTopRight && (needsRight || needsBack))
            CreateCornerPillar(x, z, 1, -1);
        if (voidBottomLeft && (needsLeft || needsFront))
            CreateCornerPillar(x, z, -1, 1);
        if (voidBottomRight && (needsRight || needsFront))
            CreateCornerPillar(x, z, 1, 1);
    }

    /// <summary>
    /// Creates an OccluderInstance3D from collected wall geometry.
    /// This enables occlusion culling - objects behind walls won't be rendered.
    /// </summary>
    private void BuildOccluder()
    {
        if (_occluderVertices.Count == 0 || _occluderIndices.Count == 0)
        {
            GD.Print("[DungeonGenerator3D] No occluder geometry to build");
            return;
        }

        // Create ArrayOccluder3D from collected vertices and indices
        var arrayOccluder = new ArrayOccluder3D();
        arrayOccluder.SetArrays(_occluderVertices.ToArray(), _occluderIndices.ToArray());

        // Create OccluderInstance3D and assign the occluder
        _occluderInstance = new OccluderInstance3D();
        _occluderInstance.Name = "WallOccluder";
        _occluderInstance.Occluder = arrayOccluder;
        AddChild(_occluderInstance);

        GD.Print($"[DungeonGenerator3D] Built occluder: {_occluderVertices.Count} vertices, {_occluderIndices.Count / 3} triangles");

        // Clear the temporary lists to free memory
        _occluderVertices.Clear();
        _occluderIndices.Clear();
    }

    private void CreateCornerPillar(int x, int z, int offsetX, int offsetZ)
    {
        var mesh = new MeshInstance3D();
        var boxMesh = new BoxMesh();
        boxMesh.Size = new Vector3(0.25f, WallHeight, 0.25f);
        mesh.Mesh = boxMesh;
        mesh.MaterialOverride = _wallMaterial;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        // Position at corner
        Vector3 basePos = new Vector3(
            x * TileSize + TileSize / 2f + offsetX * (TileSize / 2f - 0.025f),
            WallHeight / 2f,
            z * TileSize + TileSize / 2f + offsetZ * (TileSize / 2f - 0.025f)
        );
        mesh.Position = basePos;

        _wallContainer?.AddChild(mesh);

        // Add collision for corner pillar
        var body = new StaticBody3D();
        var collider = new CollisionShape3D();
        var shape = new BoxShape3D();
        shape.Size = boxMesh.Size;
        collider.Shape = shape;
        body.AddChild(collider);
        body.Position = mesh.Position;
        body.CollisionLayer = 128; // Layer 8 = bit 7 = value 128
        _wallContainer?.AddChild(body);
    }

    private void CreateFloorTile(int x, int z)
    {
        var mesh = new MeshInstance3D();
        var planeMesh = new PlaneMesh();
        planeMesh.Size = new Vector2(TileSize, TileSize);
        mesh.Mesh = planeMesh;
        mesh.MaterialOverride = _floorMaterial;
        mesh.Position = new Vector3(x * TileSize + TileSize / 2f, 0, z * TileSize + TileSize / 2f);

        // Add slight variation in height for worn stone look
        mesh.Position += new Vector3(0, (_rng.Randf() - 0.5f) * 0.02f, 0);

        _floorContainer?.AddChild(mesh);

        // Add floor collision - make it thicker to prevent falling through
        var body = new StaticBody3D();
        var collider = new CollisionShape3D();
        var shape = new BoxShape3D();
        shape.Size = new Vector3(TileSize, 0.5f, TileSize); // Thicker collision box
        collider.Shape = shape;
        collider.Position = new Vector3(0, -0.25f, 0); // Center it below the floor surface
        body.AddChild(collider);
        body.Position = mesh.Position;
        body.CollisionLayer = 128; // Layer 8 = bit 7 = value 128
        body.CollisionMask = 0; // Static body doesn't need to detect
        _floorContainer?.AddChild(body);
    }

    private void CreateWall(int x, int z, Vector3 direction)
    {
        // Make walls thicker (1.0) to ensure solid collision
        var wallThickness = 1.0f;
        var boxMesh = new BoxMesh();

        // Size depends on direction - for left/right walls, swap width/depth
        Vector3 size;
        if (direction == Vector3.Left || direction == Vector3.Right)
        {
            size = new Vector3(wallThickness, WallHeight, TileSize + 0.2f);
        }
        else
        {
            size = new Vector3(TileSize + 0.2f, WallHeight, wallThickness);
        }
        boxMesh.Size = size;

        var mesh = new MeshInstance3D();
        mesh.Mesh = boxMesh;
        mesh.MaterialOverride = _wallMaterial;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        // Position wall at edge of tile - walls should block the tile boundary
        Vector3 basePos = new Vector3(x * TileSize + TileSize / 2f, WallHeight / 2f, z * TileSize + TileSize / 2f);
        // Position the wall exactly at the edge
        Vector3 wallPos = basePos + direction * (TileSize / 2f + wallThickness / 2f);
        mesh.Position = wallPos;

        _wallContainer?.AddChild(mesh);

        // Add baseboard trim at floor-wall junction
        CreateWallTrim(wallPos, size, direction, isBaseboard: true);
        // Add crown molding at ceiling-wall junction
        CreateWallTrim(wallPos, size, direction, isBaseboard: false);

        // Add wall collision using StaticBody3D with the shape as a child
        // Use layer bit 7 (value 128) for layer 8, matching what player/enemies expect
        var body = new StaticBody3D();
        body.Name = $"WallBody_{x}_{z}_{direction}";
        body.CollisionLayer = 128; // Layer 8 = bit 7 = value 128
        body.CollisionMask = 0; // Walls don't detect anything, they just block

        var collider = new CollisionShape3D();
        var shape = new BoxShape3D();
        shape.Size = boxMesh.Size;
        collider.Shape = shape;
        body.AddChild(collider);

        body.Position = wallPos;
        _wallContainer?.AddChild(body);

        // PERF: Add occluder geometry for this wall (box occluder)
        AddBoxOccluder(wallPos, size);
    }

    /// <summary>
    /// Creates decorative trim (baseboard or crown molding) at wall-floor/ceiling junctions.
    /// Creates a smooth visual transition between surfaces.
    /// </summary>
    private void CreateWallTrim(Vector3 wallPos, Vector3 wallSize, Vector3 direction, bool isBaseboard)
    {
        // Trim dimensions
        float trimHeight = isBaseboard ? 0.15f : 0.12f;
        float trimDepth = isBaseboard ? 0.08f : 0.06f;

        // Calculate trim size based on wall orientation
        Vector3 trimSize;
        if (direction == Vector3.Left || direction == Vector3.Right)
        {
            trimSize = new Vector3(trimDepth, trimHeight, wallSize.Z);
        }
        else
        {
            trimSize = new Vector3(wallSize.X, trimHeight, trimDepth);
        }

        var trimMesh = new BoxMesh();
        trimMesh.Size = trimSize;

        var mesh = new MeshInstance3D();
        mesh.Mesh = trimMesh;
        mesh.MaterialOverride = _trimMaterial;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; // Small detail, no shadow

        // Position trim at junction
        float yPos;
        if (isBaseboard)
        {
            // Baseboard: at floor level, extending slightly out from wall
            yPos = trimHeight / 2f;
        }
        else
        {
            // Crown molding: at ceiling level
            yPos = WallHeight - trimHeight / 2f;
        }

        // Offset trim slightly toward the room (away from void)
        Vector3 trimOffset = -direction * (trimDepth / 2f + 0.01f);
        mesh.Position = new Vector3(wallPos.X, yPos, wallPos.Z) + trimOffset;

        _wallContainer?.AddChild(mesh);
    }

    /// <summary>
    /// Adds a box to the occluder mesh. The occluder will hide objects behind walls.
    /// </summary>
    private void AddBoxOccluder(Vector3 center, Vector3 size)
    {
        // Box vertices (8 corners)
        Vector3 half = size / 2f;
        int baseIndex = _occluderVertices.Count;

        // Add 8 corners of the box
        _occluderVertices.Add(center + new Vector3(-half.X, -half.Y, -half.Z)); // 0: bottom-left-back
        _occluderVertices.Add(center + new Vector3( half.X, -half.Y, -half.Z)); // 1: bottom-right-back
        _occluderVertices.Add(center + new Vector3( half.X, -half.Y,  half.Z)); // 2: bottom-right-front
        _occluderVertices.Add(center + new Vector3(-half.X, -half.Y,  half.Z)); // 3: bottom-left-front
        _occluderVertices.Add(center + new Vector3(-half.X,  half.Y, -half.Z)); // 4: top-left-back
        _occluderVertices.Add(center + new Vector3( half.X,  half.Y, -half.Z)); // 5: top-right-back
        _occluderVertices.Add(center + new Vector3( half.X,  half.Y,  half.Z)); // 6: top-right-front
        _occluderVertices.Add(center + new Vector3(-half.X,  half.Y,  half.Z)); // 7: top-left-front

        // Add 12 triangles (2 per face, 6 faces)
        // Front face (z+)
        _occluderIndices.Add(baseIndex + 3); _occluderIndices.Add(baseIndex + 2); _occluderIndices.Add(baseIndex + 6);
        _occluderIndices.Add(baseIndex + 3); _occluderIndices.Add(baseIndex + 6); _occluderIndices.Add(baseIndex + 7);
        // Back face (z-)
        _occluderIndices.Add(baseIndex + 1); _occluderIndices.Add(baseIndex + 0); _occluderIndices.Add(baseIndex + 4);
        _occluderIndices.Add(baseIndex + 1); _occluderIndices.Add(baseIndex + 4); _occluderIndices.Add(baseIndex + 5);
        // Left face (x-)
        _occluderIndices.Add(baseIndex + 0); _occluderIndices.Add(baseIndex + 3); _occluderIndices.Add(baseIndex + 7);
        _occluderIndices.Add(baseIndex + 0); _occluderIndices.Add(baseIndex + 7); _occluderIndices.Add(baseIndex + 4);
        // Right face (x+)
        _occluderIndices.Add(baseIndex + 2); _occluderIndices.Add(baseIndex + 1); _occluderIndices.Add(baseIndex + 5);
        _occluderIndices.Add(baseIndex + 2); _occluderIndices.Add(baseIndex + 5); _occluderIndices.Add(baseIndex + 6);
        // Top face (y+)
        _occluderIndices.Add(baseIndex + 7); _occluderIndices.Add(baseIndex + 6); _occluderIndices.Add(baseIndex + 5);
        _occluderIndices.Add(baseIndex + 7); _occluderIndices.Add(baseIndex + 5); _occluderIndices.Add(baseIndex + 4);
        // Bottom face (y-)
        _occluderIndices.Add(baseIndex + 0); _occluderIndices.Add(baseIndex + 1); _occluderIndices.Add(baseIndex + 2);
        _occluderIndices.Add(baseIndex + 0); _occluderIndices.Add(baseIndex + 2); _occluderIndices.Add(baseIndex + 3);
    }

    private void CreateCeiling()
    {
        // Create one large ceiling plane over the entire dungeon
        var mesh = new MeshInstance3D();
        var planeMesh = new PlaneMesh();
        planeMesh.Size = new Vector2(DungeonWidth * TileSize, DungeonDepth * TileSize);
        mesh.Mesh = planeMesh;
        mesh.MaterialOverride = _ceilingMaterial;
        mesh.Position = new Vector3(DungeonWidth * TileSize / 2f, WallHeight, DungeonDepth * TileSize / 2f);
        mesh.RotateX(Mathf.Pi); // Flip to face down

        _wallContainer?.AddChild(mesh);
    }

    private void PopulateRooms()
    {
        foreach (var room in Rooms)
        {
            int propCount = GetPropCountForRoom(room);

            for (int i = 0; i < propCount; i++)
            {
                TryPlaceProp(room);
            }

            // Add torches based on room size
            int torchCount = (room.Size.X + room.Size.Z) / 4;
            PlaceTorches(room, torchCount);

            // Add glowing fungi/lichen for ambient light in dark areas
            int fungiCount = _rng.RandiRange(2, 5);
            PlaceGlowingFungi(room, fungiCount);
        }

        // Also add fungi in corridors for ambient lighting
        PlaceCorridorFungi();
    }

    private void PlaceGlowingFungi(Room room, int count)
    {
        string[] floorFungi = { "floor_spores", "bioluminescent_veins", "green_lichen", "cyan_lichen" };
        string[] wallFungi = { "orange_fungus", "blue_fungus", "pink_fungus", "purple_lichen" };
        string[] ceilingFungi = { "ceiling_tendrils", "glowing_mushrooms" };

        for (int i = 0; i < count; i++)
        {
            float roll = _rng.Randf();

            if (roll < 0.4f)
            {
                // Floor placement (40%)
                int x = room.Position.X + _rng.RandiRange(1, room.Size.X - 2);
                int z = room.Position.Z + _rng.RandiRange(1, room.Size.Z - 2);
                string fungiType = floorFungi[_rng.RandiRange(0, floorFungi.Length - 1)];

                var fungi = Cosmetic3D.Create(fungiType, Colors.White, Colors.White, Colors.White,
                    _rng.Randf(), 0.8f + _rng.Randf() * 0.4f, false);
                fungi.Position = new Vector3(x * TileSize + TileSize / 2f, 0.02f, z * TileSize + TileSize / 2f);
                fungi.RotateY(_rng.Randf() * Mathf.Tau);
                _propContainer?.AddChild(fungi);
            }
            else if (roll < 0.75f)
            {
                // Wall placement (35%)
                bool onXWall = _rng.Randf() > 0.5f;
                int x, z;
                float yOffset = 0.5f + _rng.Randf() * 1.5f; // Random height on wall

                if (onXWall)
                {
                    x = room.Position.X + (_rng.Randf() > 0.5f ? 0 : room.Size.X - 1);
                    z = room.Position.Z + _rng.RandiRange(1, room.Size.Z - 2);
                }
                else
                {
                    x = room.Position.X + _rng.RandiRange(1, room.Size.X - 2);
                    z = room.Position.Z + (_rng.Randf() > 0.5f ? 0 : room.Size.Z - 1);
                }

                string fungiType = wallFungi[_rng.RandiRange(0, wallFungi.Length - 1)];
                var fungi = Cosmetic3D.Create(fungiType, Colors.White, Colors.White, Colors.White,
                    _rng.Randf(), 0.6f + _rng.Randf() * 0.4f, false);
                fungi.Position = new Vector3(x * TileSize + TileSize / 2f, yOffset, z * TileSize + TileSize / 2f);
                fungi.RotateY(_rng.Randf() * Mathf.Tau);
                _propContainer?.AddChild(fungi);
            }
            else
            {
                // Ceiling placement (25%)
                int x = room.Position.X + _rng.RandiRange(1, room.Size.X - 2);
                int z = room.Position.Z + _rng.RandiRange(1, room.Size.Z - 2);
                string fungiType = ceilingFungi[_rng.RandiRange(0, ceilingFungi.Length - 1)];

                var fungi = Cosmetic3D.Create(fungiType, Colors.White, Colors.White, Colors.White,
                    _rng.Randf(), 0.7f + _rng.Randf() * 0.5f, false);
                fungi.Position = new Vector3(x * TileSize + TileSize / 2f, WallHeight - 0.1f, z * TileSize + TileSize / 2f);
                fungi.RotateY(_rng.Randf() * Mathf.Tau);
                _propContainer?.AddChild(fungi);
            }
        }
    }

    private void PlaceCorridorFungi()
    {
        string[] corridorFungi = { "cyan_lichen", "green_lichen", "floor_spores", "bioluminescent_veins" };
        int fungiPlaced = 0;

        // Iterate through map and find corridor tiles
        for (int x = 0; x < DungeonWidth; x++)
        {
            for (int z = 0; z < DungeonDepth; z++)
            {
                if (_mapData[x, 0, z] == 2) // Corridor tile
                {
                    // 15% chance to place glowing fungi on corridor
                    if (_rng.Randf() < 0.15f)
                    {
                        string fungiType = corridorFungi[_rng.RandiRange(0, corridorFungi.Length - 1)];
                        var fungi = Cosmetic3D.Create(fungiType, Colors.White, Colors.White, Colors.White,
                            _rng.Randf(), 0.5f + _rng.Randf() * 0.5f, false);
                        fungi.Position = new Vector3(x * TileSize + TileSize / 2f, 0.02f, z * TileSize + TileSize / 2f);
                        fungi.RotateY(_rng.Randf() * Mathf.Tau);
                        _propContainer?.AddChild(fungi);
                        fungiPlaced++;
                    }
                }
            }
        }

        GD.Print($"[DungeonGenerator3D] Placed {fungiPlaced} glowing fungi in corridors");
    }

    private int GetPropCountForRoom(Room room)
    {
        return room.Type switch
        {
            "storage" => _rng.RandiRange(8, 15),
            "library" => _rng.RandiRange(4, 8),
            "armory" => _rng.RandiRange(5, 10),
            "cathedral" => _rng.RandiRange(2, 5),
            _ => _rng.RandiRange(3, 8)
        };
    }

    private void TryPlaceProp(Room room)
    {
        // Random position within room (avoid edges)
        int x = room.Position.X + _rng.RandiRange(1, room.Size.X - 2);
        int z = room.Position.Z + _rng.RandiRange(1, room.Size.Z - 2);

        // Get appropriate prop type for room
        string propType = GetPropTypeForRoom(room);
        Color primary = GetPropColor(propType);
        Color secondary = primary.Darkened(0.2f);
        Color accent = new Color(0.4f, 0.4f, 0.45f);

        var prop = Cosmetic3D.Create(propType, primary, secondary, accent,
            _rng.Randf(), 1f, true);
        prop.Position = new Vector3(x * TileSize + TileSize / 2f, 0, z * TileSize + TileSize / 2f);

        // Random rotation
        prop.RotateY(_rng.Randf() * Mathf.Tau);

        _propContainer?.AddChild(prop);
        room.Props.Add(prop);
    }

    private string GetPropTypeForRoom(Room room)
    {
        string[] props = room.Type switch
        {
            "storage" => new[] { "barrel", "barrel", "crate", "crate", "pot" },
            "library" => new[] { "bookshelf", "table", "pot", "torch" },
            "armory" => new[] { "chest", "barrel", "crate" },
            "cathedral" => new[] { "pillar", "statue", "torch", "cauldron" },
            "crypt" => new[] { "pot", "chest", "crystal", "statue" },
            _ => new[] { "barrel", "crate", "pot" }
        };
        return props[_rng.RandiRange(0, props.Length - 1)];
    }

    private Color GetPropColor(string propType)
    {
        return propType switch
        {
            "barrel" => new Color(0.55f, 0.35f, 0.2f),  // Wood brown
            "crate" => new Color(0.5f, 0.38f, 0.22f),   // Lighter wood
            "pot" => new Color(0.6f, 0.45f, 0.35f),     // Clay
            "pillar" => new Color(0.45f, 0.43f, 0.4f),  // Stone gray
            "bookshelf" => new Color(0.4f, 0.28f, 0.18f), // Dark wood
            "crystal" => new Color(0.4f, 0.7f, 0.9f),   // Blue crystal
            "statue" => new Color(0.5f, 0.5f, 0.48f),   // Stone
            _ => new Color(0.5f, 0.4f, 0.3f)
        };
    }

    private void PlaceTorches(Room room, int count)
    {
        // Place torches along walls
        for (int i = 0; i < count; i++)
        {
            bool onXWall = _rng.Randf() > 0.5f;
            int x, z;

            if (onXWall)
            {
                x = room.Position.X + (i % 2 == 0 ? 0 : room.Size.X - 1);
                z = room.Position.Z + _rng.RandiRange(1, room.Size.Z - 2);
            }
            else
            {
                x = room.Position.X + _rng.RandiRange(1, room.Size.X - 2);
                z = room.Position.Z + (i % 2 == 0 ? 0 : room.Size.Z - 1);
            }

            var torch = Cosmetic3D.Create("torch",
                new Color(0.4f, 0.25f, 0.15f),
                new Color(0.3f, 0.2f, 0.1f),
                new Color(0.3f, 0.3f, 0.35f),
                _rng.Randf(), 1f, false);

            torch.HasLighting = true;
            torch.LightColor = new Color(1f, 0.7f, 0.4f);
            torch.LightRadius = Constants.TorchRange;
            torch.Position = new Vector3(x * TileSize + TileSize / 2f, 1.5f, z * TileSize + TileSize / 2f);

            _propContainer?.AddChild(torch);
        }
    }

    private void AddAmbientLighting()
    {
        // Brighter ambient light for better visibility
        var ambient = new DirectionalLight3D();
        ambient.LightColor = new Color(0.25f, 0.22f, 0.18f); // Warmer, brighter
        ambient.LightEnergy = 0.35f; // Significantly brighter
        ambient.Rotation = new Vector3(Mathf.DegToRad(-45), Mathf.DegToRad(45), 0);
        ambient.ShadowEnabled = false;
        _lightContainer?.AddChild(ambient);

        // Add a secondary fill light from below for dungeon atmosphere
        var fillLight = new DirectionalLight3D();
        fillLight.LightColor = new Color(0.15f, 0.18f, 0.22f); // Cool blue undertone
        fillLight.LightEnergy = 0.15f;
        fillLight.Rotation = new Vector3(Mathf.DegToRad(30), Mathf.DegToRad(-120), 0);
        fillLight.ShadowEnabled = false;
        _lightContainer?.AddChild(fillLight);
    }

    /// <summary>
    /// Get the spawn position for the player.
    /// For tile-mode maps, uses SpawnPosition from map definition.
    /// For room-mode maps, uses center of spawn room.
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        // Check for tile-mode map with explicit spawn position
        if (_customMapDefinition != null &&
            _customMapDefinition.IsTileMode &&
            _customMapDefinition.SpawnPosition != null)
        {
            var spawnPos = _customMapDefinition.SpawnPosition;
            return new Vector3(
                spawnPos.X * TileSize + TileSize / 2f,
                0.1f,
                spawnPos.Z * TileSize + TileSize / 2f
            );
        }

        // Fallback to spawn room center
        if (_spawnRoom == null) return Vector3.Zero;

        Vector3I center = _spawnRoom.Position + _spawnRoom.Size / 2;
        return new Vector3(center.X * TileSize + TileSize / 2f, 0.1f, center.Z * TileSize + TileSize / 2f);
    }

    /// <summary>
    /// Check if a position is on a floor tile
    /// </summary>
    public bool IsFloor(Vector3 worldPos)
    {
        int x = (int)(worldPos.X / TileSize);
        int z = (int)(worldPos.Z / TileSize);

        if (x < 0 || x >= DungeonWidth || z < 0 || z >= DungeonDepth)
            return false;

        return _mapData[x, 0, z] == 1;
    }

    /// <summary>
    /// Get 2D map data for minimap. Returns a 2D array (x,z) where:
    /// 0 = wall/void, 1 = corridor, 2 = room floor
    /// </summary>
    public int[,]? GetMapData()
    {
        if (_mapData == null) return null;

        int[,] map2D = new int[DungeonWidth, DungeonDepth];

        for (int x = 0; x < DungeonWidth; x++)
        {
            for (int z = 0; z < DungeonDepth; z++)
            {
                map2D[x, z] = _mapData[x, 0, z];
            }
        }

        // Mark rooms with different tile type
        foreach (var room in Rooms)
        {
            for (int rx = 0; rx < room.Size.X; rx++)
            {
                for (int rz = 0; rz < room.Size.Z; rz++)
                {
                    int x = room.Position.X + rx;
                    int z = room.Position.Z + rz;
                    if (x >= 0 && x < DungeonWidth && z >= 0 && z < DungeonDepth)
                    {
                        if (map2D[x, z] == 1)
                            map2D[x, z] = 2; // Room floor
                    }
                }
            }
        }

        return map2D;
    }

    // ============================================
    // Enemy Spawning System
    // ============================================

    // Monster types that spawn in rooms (all types from EditorScreen3D Monsters tab)
    private static readonly string[] MonsterTypes = {
        "goblin", "goblin_shaman", "goblin_thrower", "slime", "eye", "mushroom",
        "spider", "lizard", "skeleton", "wolf", "badlama", "bat", "dragon"
    };

    // Boss types (all types from EditorScreen3D Bosses section)
    private static readonly string[] EditorBossTypes = {
        "skeleton_lord", "dragon_king", "spider_queen"
    };

    private void SpawnEnemies()
    {
        int totalEnemies = 0;
        int totalBosses = 0;

        for (int i = 0; i < Rooms.Count; i++)
        {
            var room = Rooms[i];

            // Skip spawn room (first room)
            if (room == _spawnRoom)
            {
                GD.Print($"[DungeonGenerator3D] Skipping spawn room");
                continue;
            }

            // Determine enemy type for this room
            string monsterType = MonsterTypes[i % MonsterTypes.Length];
            GD.Print($"[DungeonGenerator3D] Room {i}: type={monsterType}");

            // Fixed enemy count: 4-5 enemies per room (one group)
            int roomArea = room.Size.X * room.Size.Z;
            int enemyCount = _rng.RandiRange(4, 5);

            // Large rooms get a boss as part of the group (replacing one regular enemy)
            bool hasBoss = roomArea >= 64 && i != 0;
            int regularCount = hasBoss ? enemyCount - 1 : enemyCount;

            // Spawn regular enemies
            for (int e = 0; e < regularCount; e++)
            {
                // Wolf rooms include some badlamas in the pack
                string spawnType = monsterType;
                if (monsterType == "wolf" && e < regularCount / 2)
                {
                    spawnType = "badlama";
                }
                SpawnEnemy(room, spawnType, false);
                totalEnemies++;
            }

            // Spawn boss in larger rooms (as part of the group) - use Editor boss types only
            if (hasBoss)
            {
                string bossType = EditorBossTypes[_rng.RandiRange(0, EditorBossTypes.Length - 1)];
                SpawnEnemy(room, bossType, true);
                totalBosses++;
            }

            room.Enemies = new List<Node3D>();
        }

        GD.Print($"[DungeonGenerator3D] Spawned {totalEnemies} enemies and {totalBosses} bosses");
    }

    private void SpawnEnemy(Room room, string monsterType, bool isBoss)
    {
        // Random position within room (avoid edges)
        int x = room.Position.X + _rng.RandiRange(2, room.Size.X - 3);
        int z = room.Position.Z + _rng.RandiRange(2, room.Size.Z - 3);

        Vector3 position = new Vector3(
            x * TileSize + TileSize / 2f,
            0.1f, // Slightly above floor
            z * TileSize + TileSize / 2f
        );

        Node3D enemy;
        if (isBoss)
        {
            var boss = BossEnemy3D.Create(monsterType, position);
            enemy = boss;
            GD.Print($"[DungeonGenerator3D] Spawned boss: {boss.BossName} at {position}");
        }
        else
        {
            var basic = BasicEnemy3D.Create(monsterType, position);
            enemy = basic;
        }

        _enemyContainer?.AddChild(enemy);
        room.Enemies.Add(enemy);
    }

    /// <summary>
    /// Get all living enemies in the dungeon
    /// </summary>
    public List<Node3D> GetAllEnemies()
    {
        var enemies = new List<Node3D>();
        if (_enemyContainer == null) return enemies;

        foreach (var child in _enemyContainer.GetChildren())
        {
            if (child is BasicEnemy3D basic && basic.CurrentState != BasicEnemy3D.State.Dead)
            {
                enemies.Add(basic);
            }
            else if (child is BossEnemy3D boss && boss.CurrentState != BossEnemy3D.State.Dead)
            {
                enemies.Add(boss);
            }
        }

        return enemies;
    }

    /// <summary>
    /// Get enemy count statistics
    /// </summary>
    public (int alive, int dead, int bosses) GetEnemyStats()
    {
        int alive = 0;
        int dead = 0;
        int bosses = 0;

        if (_enemyContainer == null) return (0, 0, 0);

        foreach (var child in _enemyContainer.GetChildren())
        {
            if (child is BasicEnemy3D basic)
            {
                if (basic.CurrentState == BasicEnemy3D.State.Dead)
                    dead++;
                else
                    alive++;
            }
            else if (child is BossEnemy3D boss)
            {
                bosses++;
                if (boss.CurrentState == BossEnemy3D.State.Dead)
                    dead++;
                else
                    alive++;
            }
        }

        return (alive, dead, bosses);
    }
}

/// <summary>
/// A flickering torch light that animates like a real flame.
/// Uses multiple sine waves for organic, natural-looking flicker.
/// </summary>
public partial class FlickeringTorchLight : OmniLight3D
{
    public Color BaseColor { get; set; } = new Color(1f, 0.7f, 0.35f);
    public float BaseEnergy { get; set; } = 2f;
    public float LightRange { get; set; } = 10f;
    public bool EnableShadows { get; set; } = false;

    private float _flickerTime = 0f;
    private float _flickerOffset;

    public override void _Ready()
    {
        // Randomize flicker offset so torches don't sync
        _flickerOffset = GD.Randf() * 100f;

        LightColor = BaseColor;
        LightEnergy = BaseEnergy;
        OmniRange = LightRange;
        OmniAttenuation = 1.3f;
        ShadowEnabled = EnableShadows;

        // Performance: distance fade
        DistanceFadeEnabled = true;
        DistanceFadeBegin = 35f;
        DistanceFadeLength = 15f;
        DistanceFadeShadow = 25f;
    }

    public override void _Process(double delta)
    {
        _flickerTime += (float)delta * 8f;
        float t = _flickerTime + _flickerOffset;

        // Multi-frequency flicker for organic flame effect
        // Primary flicker (slow wave)
        float flicker1 = Mathf.Sin(t * 1.0f) * 0.08f;
        // Secondary flicker (faster)
        float flicker2 = Mathf.Sin(t * 2.7f) * 0.05f;
        // Tertiary flicker (fastest, small amplitude)
        float flicker3 = Mathf.Sin(t * 5.3f) * 0.03f;
        // Random noise for natural variation
        float noise = (float)GD.RandRange(-0.02, 0.02);

        float totalFlicker = 1f + flicker1 + flicker2 + flicker3 + noise;

        // Apply energy flicker
        LightEnergy = BaseEnergy * totalFlicker;

        // Subtle color temperature shift (warmer when brighter)
        float warmth = 0.02f * (totalFlicker - 1f);
        LightColor = new Color(
            Mathf.Clamp(BaseColor.R, 0, 1),
            Mathf.Clamp(BaseColor.G + warmth, 0, 1),
            Mathf.Clamp(BaseColor.B + warmth * 0.5f, 0, 1)
        );
    }
}
