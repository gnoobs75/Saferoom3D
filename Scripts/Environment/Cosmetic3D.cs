using Godot;
using SafeRoom3D.Core;

namespace SafeRoom3D.Environment;

/// <summary>
/// 3D procedural cosmetic prop. Generates low-poly meshes at runtime
/// based on CosmeticData, matching the 2D procedural drawing system.
/// Supports: barrel, crate, pot, pillar, crystal, torch, chest, table, bookshelf, cauldron, statue, chair, skull_pile, campfire
/// New props: bone_pile, coiled_chains, moss_patch, water_puddle, treasure_chest, broken_barrel, altar_stone,
/// glowing_mushrooms, blood_pool, spike_trap, discarded_sword, shattered_potion, ancient_scroll, brazier_fire,
/// manacles, cracked_tiles, rubble_heap, thorny_vines, engraved_rune, rat_nest, broken_pottery, scattered_coins,
/// forgotten_shield, moldy_bread
/// Glowing fungi/lichen: cyan_lichen, purple_lichen, green_lichen, orange_fungus, blue_fungus, pink_fungus,
/// ceiling_tendrils, floor_spores, bioluminescent_veins
/// </summary>
public partial class Cosmetic3D : StaticBody3D
{
    // Cosmetic configuration
    public string ShapeType { get; set; } = "barrel";
    public Color PrimaryColor { get; set; } = new(0.6f, 0.4f, 0.2f);   // Wood brown
    public Color SecondaryColor { get; set; } = new(0.5f, 0.35f, 0.15f); // Darker wood
    public Color AccentColor { get; set; } = new(0.4f, 0.4f, 0.45f);   // Metal gray
    public float VariationSeed { get; set; }
    public float PropScale { get; set; } = 1f;
    public bool HasCollision { get; set; } = true;
    public bool HasLighting { get; set; } = false;
    public Color LightColor { get; set; } = new(1f, 0.8f, 0.5f);
    public float LightRadius { get; set; } = 5f;

    // Components
    private MeshInstance3D? _meshInstance;
    private CollisionShape3D? _collisionShape;
    private OmniLight3D? _light;

    // For torch flicker
    private float _flickerTime;
    private float _baseLightEnergy;

    // PERFORMANCE: Distance-based processing
    private float _processTimer;
    private const float ProcessInterval = 0.1f; // Only process flicker every 100ms
    private const float MaxFlickerDistance = 30f; // Only flicker when within 30 meters

    public override void _Ready()
    {
        GenerateMesh();

        if (HasLighting)
        {
            CreateLight();
        }

        // PERFORMANCE: Stagger processing to avoid frame spikes
        _processTimer = GD.Randf() * ProcessInterval;
    }

    public override void _Process(double delta)
    {
        // PERFORMANCE: Early exit if no lighting
        if (_light == null || !HasLighting) return;

        // PERFORMANCE: Only process periodically, not every frame
        _processTimer -= (float)delta;
        if (_processTimer > 0) return;
        _processTimer = ProcessInterval;

        // PERFORMANCE: Distance-based culling for flicker effect
        var player = Player.FPSController.Instance;
        if (player != null)
        {
            float distSq = GlobalPosition.DistanceSquaredTo(player.GlobalPosition);
            if (distSq > MaxFlickerDistance * MaxFlickerDistance)
            {
                return; // Too far, skip flicker
            }
        }

        // Torch flicker effect
        _flickerTime += ProcessInterval * Constants.TorchFlickerSpeed;
        float flicker = 1f + Mathf.Sin(_flickerTime * 3.7f) * Constants.TorchFlickerAmount
                          + Mathf.Sin(_flickerTime * 5.3f) * Constants.TorchFlickerAmount * 0.5f;
        _light.LightEnergy = _baseLightEnergy * flicker;
    }

    /// <summary>
    /// Generate the 3D mesh based on ShapeType
    /// </summary>
    public void GenerateMesh()
    {
        // Clear existing mesh
        _meshInstance?.QueueFree();
        _collisionShape?.QueueFree();

        // Apply random variation based on seed
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)(VariationSeed * 1000000);
        float scaleVar = 1f + (rng.Randf() - 0.5f) * 0.1f; // ±5% size variation

        ArrayMesh? mesh = null;
        Shape3D? collision = null;
        Vector3 meshOffset = Vector3.Zero;

        float s = PropScale * scaleVar; // Combined scale factor

        switch (ShapeType.ToLower())
        {
            case "barrel":
                mesh = CreateBarrelMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = Constants.BarrelRadius * s,
                    Height = Constants.BarrelHeight * s
                };
                meshOffset = new Vector3(0, Constants.BarrelHeight * s / 2f, 0);
                break;

            case "crate":
                mesh = CreateCrateMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(Constants.CrateSize, Constants.CrateSize, Constants.CrateSize) * s
                };
                meshOffset = new Vector3(0, Constants.CrateSize * s / 2f, 0);
                break;

            case "pot":
                mesh = CreatePotMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = Constants.PotRadius * 1.1f * s,
                    Height = Constants.PotHeight * s
                };
                meshOffset = new Vector3(0, Constants.PotHeight * s / 2f, 0);
                break;

            case "pillar":
                mesh = CreatePillarMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.3f * s,
                    Height = Constants.WallHeight * s
                };
                meshOffset = new Vector3(0, Constants.WallHeight * s / 2f, 0);
                break;

            case "crystal":
                mesh = CreateCrystalMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.2f * s,
                    Height = 0.6f * s
                };
                meshOffset = new Vector3(0, 0.3f * s, 0);
                HasLighting = true;
                LightColor = PrimaryColor.Lightened(0.3f);
                break;

            case "torch":
                mesh = CreateTorchMesh(rng);
                collision = new BoxShape3D { Size = new Vector3(0.1f, 0.5f, 0.1f) * PropScale };
                // Torch mesh starts at Y=0, extends to ~0.5f, center at 0.25f
                meshOffset = new Vector3(0, 0.25f * PropScale, 0);
                HasLighting = true;
                break;

            case "chest":
                mesh = CreateChestMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(0.7f, 0.5f, 0.5f) * s
                };
                meshOffset = new Vector3(0, 0.25f * s, 0);
                break;

            case "table":
                mesh = CreateTableMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(1f, 0.8f, 0.6f) * s
                };
                meshOffset = new Vector3(0, 0.4f * s, 0);
                break;

            case "bookshelf":
                mesh = CreateBookshelfMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(1.2f, 2f, 0.4f) * s
                };
                meshOffset = new Vector3(0, 1f * s, 0);
                break;

            case "cauldron":
                mesh = CreateCauldronMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.4f * s,
                    Height = 0.5f * s
                };
                meshOffset = new Vector3(0, 0.25f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.2f, 0.8f, 0.3f); // Eerie green
                break;

            case "statue":
                mesh = CreateStatueMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.3f * s,
                    Height = 1.8f * s
                };
                meshOffset = new Vector3(0, 0.9f * s, 0);
                break;

            case "chair":
                mesh = CreateChairMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(0.5f, 0.9f, 0.5f) * s
                };
                meshOffset = new Vector3(0, 0.45f * s, 0);
                break;

            case "skull_pile":
                mesh = CreateSkullPileMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.5f * s,
                    Height = 0.4f * s
                };
                meshOffset = new Vector3(0, 0.2f * s, 0);
                break;

            case "campfire":
                mesh = CreateCampfireMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.5f * s,
                    Height = 0.3f * s
                };
                meshOffset = new Vector3(0, 0.15f * s, 0);
                HasLighting = true;
                LightColor = new Color(1f, 0.6f, 0.2f); // Warm fire orange
                LightRadius = 8f;
                // Fire and smoke particles created in special method
                break;

            case "bone_pile":
                mesh = CreateBonePileMesh(rng);
                collision = new CylinderShape3D { Radius = 0.6f * s, Height = 0.3f * s };
                meshOffset = new Vector3(0, 0.15f * s, 0);
                break;

            case "coiled_chains":
                mesh = CreateCoiledChainsMesh(rng);
                collision = new CylinderShape3D { Radius = 0.4f * s, Height = 0.2f * s };
                meshOffset = new Vector3(0, 0.1f * s, 0);
                break;

            case "moss_patch":
                mesh = CreateMossPatchMesh(rng);
                collision = null; // No collision for floor decal
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                break;

            case "water_puddle":
                mesh = CreateWaterPuddleMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.01f * s, 0);
                break;

            case "treasure_chest":
                mesh = CreateTreasureChestMesh(rng);
                collision = new BoxShape3D { Size = new Vector3(0.8f, 0.6f, 0.5f) * s };
                meshOffset = new Vector3(0, 0.3f * s, 0);
                break;

            case "broken_barrel":
                mesh = CreateBrokenBarrelMesh(rng);
                collision = new CylinderShape3D { Radius = 0.45f * s, Height = 0.4f * s };
                meshOffset = new Vector3(0, 0.2f * s, 0);
                break;

            case "altar_stone":
                mesh = CreateAltarStoneMesh(rng);
                collision = new BoxShape3D { Size = new Vector3(1.2f, 0.5f, 0.8f) * s };
                meshOffset = new Vector3(0, 0.25f * s, 0);
                break;

            case "glowing_mushrooms":
                mesh = CreateGlowingMushroomsMesh(rng);
                collision = new CylinderShape3D { Radius = 0.3f * s, Height = 0.4f * s };
                meshOffset = new Vector3(0, 0.2f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.2f, 0.8f, 0.6f); // Blue-green glow
                LightRadius = 4f;
                break;

            case "blood_pool":
                mesh = CreateBloodPoolMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.01f * s, 0);
                break;

            case "spike_trap":
                mesh = CreateSpikeTrapMesh(rng);
                collision = new BoxShape3D { Size = new Vector3(1f, 0.5f, 1f) * s };
                meshOffset = new Vector3(0, 0.25f * s, 0);
                break;

            case "discarded_sword":
                mesh = CreateDiscardedSwordMesh(rng);
                collision = new BoxShape3D { Size = new Vector3(0.1f, 0.1f, 0.8f) * s };
                meshOffset = new Vector3(0, 0.05f * s, 0);
                break;

            case "shattered_potion":
                mesh = CreateShatteredPotionMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.2f, 1f, 0.3f); // Glowing green residue
                LightRadius = 2f;
                break;

            case "ancient_scroll":
                mesh = CreateAncientScrollMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                break;

            case "brazier_fire":
                mesh = CreateBrazierFireMesh(rng);
                collision = new CylinderShape3D { Radius = 0.4f * s, Height = 0.8f * s };
                meshOffset = new Vector3(0, 0.4f * s, 0);
                HasLighting = true;
                LightColor = new Color(1f, 0.6f, 0.2f);
                LightRadius = 10f;
                break;

            case "manacles":
                mesh = CreateManaclesMesh(rng);
                collision = null;
                HasCollision = false;
                // Wall-mounted shackles - attach at Z+ to wall
                meshOffset = new Vector3(0, 0.05f * s, 0);
                break;

            case "cracked_tiles":
                mesh = CreateCrackedTilesMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                break;

            case "rubble_heap":
                mesh = CreateRubbleHeapMesh(rng);
                collision = new CylinderShape3D { Radius = 0.7f * s, Height = 0.5f * s };
                meshOffset = new Vector3(0, 0.25f * s, 0);
                break;

            case "thorny_vines":
                mesh = CreateThornyVinesMesh(rng);
                collision = new BoxShape3D { Size = new Vector3(1f, 0.3f, 1f) * s };
                meshOffset = new Vector3(0, 0.15f * s, 0);
                break;

            case "engraved_rune":
                mesh = CreateEngravedRuneMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                HasLighting = true;
                LightColor = new Color(1f, 0.2f, 0.2f); // Red magical glow
                LightRadius = 3f;
                break;

            case "abandoned_campfire":
                mesh = CreateAbandonedCampfireMesh(rng);
                collision = new CylinderShape3D { Radius = 0.5f * s, Height = 0.2f * s };
                meshOffset = new Vector3(0, 0.1f * s, 0);
                break;

            case "rat_nest":
                mesh = CreateRatNestMesh(rng);
                collision = new CylinderShape3D { Radius = 0.4f * s, Height = 0.25f * s };
                meshOffset = new Vector3(0, 0.12f * s, 0);
                break;

            case "broken_pottery":
                mesh = CreateBrokenPotteryMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.05f * s, 0);
                break;

            case "scattered_coins":
                mesh = CreateScatteredCoinsMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                break;

            case "forgotten_shield":
                mesh = CreateForgottenShieldMesh(rng);
                collision = new BoxShape3D { Size = new Vector3(0.6f, 0.1f, 0.6f) * s };
                meshOffset = new Vector3(0, 0.05f * s, 0);
                break;

            case "moldy_bread":
                mesh = CreateMoldyBreadMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.05f * s, 0);
                break;

            // === GLOWING FUNGI AND LICHEN (for dark areas) ===
            case "cyan_lichen":
                mesh = CreateLichenPatchMesh(rng, new Color(0.1f, 0.7f, 0.8f));
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.2f, 0.8f, 0.9f);
                LightRadius = 3f;
                break;

            case "purple_lichen":
                mesh = CreateLichenPatchMesh(rng, new Color(0.6f, 0.2f, 0.8f));
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.7f, 0.3f, 0.9f);
                LightRadius = 3f;
                break;

            case "green_lichen":
                mesh = CreateLichenPatchMesh(rng, new Color(0.2f, 0.8f, 0.3f));
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.3f, 0.9f, 0.4f);
                LightRadius = 3f;
                break;

            case "orange_fungus":
                mesh = CreateWallFungusMesh(rng, new Color(1f, 0.5f, 0.1f));
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.1f * s, 0);
                HasLighting = true;
                LightColor = new Color(1f, 0.6f, 0.2f);
                LightRadius = 4f;
                break;

            case "blue_fungus":
                mesh = CreateWallFungusMesh(rng, new Color(0.2f, 0.4f, 1f));
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.1f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.3f, 0.5f, 1f);
                LightRadius = 4f;
                break;

            case "pink_fungus":
                mesh = CreateWallFungusMesh(rng, new Color(1f, 0.3f, 0.6f));
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.1f * s, 0);
                HasLighting = true;
                LightColor = new Color(1f, 0.4f, 0.7f);
                LightRadius = 4f;
                break;

            case "ceiling_tendrils":
                mesh = CreateCeilingTendrilsMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, -0.3f * s, 0); // Hangs down from ceiling
                HasLighting = true;
                LightColor = new Color(0.4f, 0.9f, 0.5f);
                LightRadius = 5f;
                break;

            case "floor_spores":
                mesh = CreateFloorSporesMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.05f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.9f, 0.8f, 0.2f);
                LightRadius = 3f;
                break;

            case "bioluminescent_veins":
                mesh = CreateBioluminescentVeinsMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0.01f * s, 0);
                HasLighting = true;
                LightColor = new Color(0.2f, 0.9f, 0.7f);
                LightRadius = 4f;
                break;

            default:
                // Fallback to barrel
                mesh = CreateBarrelMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = Constants.BarrelRadius * PropScale,
                    Height = Constants.BarrelHeight * PropScale
                };
                meshOffset = new Vector3(0, Constants.BarrelHeight * PropScale / 2f, 0);
                break;
        }

        // Create mesh instance
        if (mesh != null)
        {
            _meshInstance = new MeshInstance3D();
            _meshInstance.Mesh = mesh;
            _meshInstance.Position = meshOffset;
            _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

            // PERFORMANCE: Set visibility range for automatic draw distance culling
            _meshInstance.VisibilityRangeBegin = Constants.PropVisibilityBegin;
            _meshInstance.VisibilityRangeEnd = Constants.PropVisibilityEnd;
            _meshInstance.VisibilityRangeBeginMargin = Constants.VisibilityFadeMargin;
            _meshInstance.VisibilityRangeEndMargin = Constants.VisibilityFadeMargin;
            _meshInstance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;

            AddChild(_meshInstance);
        }

        // Create collision
        if (HasCollision && collision != null)
        {
            _collisionShape = new CollisionShape3D();
            _collisionShape.Shape = collision;
            _collisionShape.Position = meshOffset;
            AddChild(_collisionShape);

            CollisionLayer = 16; // Obstacle layer
            CollisionMask = 0;
        }
    }

    private ArrayMesh CreateBarrelMesh(RandomNumberGenerator rng)
    {
        float radius = Constants.BarrelRadius * PropScale;
        float height = Constants.BarrelHeight * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        int segments = 16; // More segments for smoother barrel
        float angleStep = Mathf.Tau / segments;

        // Create barrel body with bulge in the middle
        int heightSegments = 8;
        for (int h = 0; h <= heightSegments; h++)
        {
            float t = (float)h / heightSegments;
            float y = -height / 2f + t * height;

            // Bulge calculation - wider in middle
            float bulge = 1f + 0.15f * Mathf.Sin(t * Mathf.Pi);
            float currentRadius = radius * bulge;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * currentRadius;
                float z = Mathf.Sin(angle) * currentRadius;

                Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(new Vector2((float)i / segments, t));
                surfaceTool.AddVertex(new Vector3(x, y, z));
            }
        }

        // Generate barrel body triangles
        for (int h = 0; h < heightSegments; h++)
        {
            for (int i = 0; i < segments; i++)
            {
                int baseIdx = h * (segments + 1) + i;
                surfaceTool.AddIndex(baseIdx);
                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 1);

                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 2);
                surfaceTool.AddIndex(baseIdx + segments + 1);
            }
        }

        // Add metal bands around barrel (3 bands)
        float[] bandPositions = { 0.2f, 0.5f, 0.8f };
        int baseVertexCount = (heightSegments + 1) * (segments + 1);
        int bandVertexOffset = baseVertexCount;

        foreach (float bandT in bandPositions)
        {
            float bandY = -height / 2f + bandT * height;
            float bandHeight = 0.03f * PropScale;
            float bulge = 1f + 0.15f * Mathf.Sin(bandT * Mathf.Pi);
            float bandRadius = radius * bulge * 1.02f; // Slightly larger than barrel

            int bandStart = bandVertexOffset;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * bandRadius;
                float z = Mathf.Sin(angle) * bandRadius;
                Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).Normalized();

                // Bottom of band
                surfaceTool.SetNormal(normal);
                surfaceTool.AddVertex(new Vector3(x, bandY - bandHeight / 2, z));

                // Top of band
                surfaceTool.SetNormal(normal);
                surfaceTool.AddVertex(new Vector3(x, bandY + bandHeight / 2, z));

                if (i < segments)
                {
                    int idx = bandStart + i * 2;
                    surfaceTool.AddIndex(idx);
                    surfaceTool.AddIndex(idx + 1);
                    surfaceTool.AddIndex(idx + 2);

                    surfaceTool.AddIndex(idx + 1);
                    surfaceTool.AddIndex(idx + 3);
                    surfaceTool.AddIndex(idx + 2);
                }
            }

            bandVertexOffset += (segments + 1) * 2;
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();

        // Enhanced wood material with better PBR
        var material = ProceduralMesh3D.CreateWoodMaterial(PrimaryColor.Darkened(0.1f));
        material.Roughness = 0.8f;
        mesh.SurfaceSetMaterial(0, material);

        return mesh;
    }

    private ArrayMesh CreateCrateMesh(RandomNumberGenerator rng)
    {
        float size = Constants.CrateSize * PropScale;
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        // Create main crate body with beveled edges
        float bevel = 0.02f * PropScale;
        Vector3 half = new Vector3(size / 2f - bevel, size / 2f - bevel, size / 2f - bevel);

        // Main box
        AddBoxToSurfaceTool(surfaceTool, Vector3.Zero, new Vector3(size - bevel * 2, size - bevel * 2, size - bevel * 2));

        // Add wooden planks on each face (3 horizontal planks per face)
        float plankWidth = size * 0.25f;
        float plankThickness = 0.015f * PropScale;
        float spacing = size * 0.3f;

        // Front and back faces - vertical planks
        for (int i = -1; i <= 1; i++)
        {
            float x = i * spacing;
            // Front planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0, half.Z + plankThickness),
                new Vector3(plankWidth * 0.8f, size * 0.9f, plankThickness * 2));
            // Back planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0, -half.Z - plankThickness),
                new Vector3(plankWidth * 0.8f, size * 0.9f, plankThickness * 2));
        }

        // Left and right faces - vertical planks
        for (int i = -1; i <= 1; i++)
        {
            float z = i * spacing;
            // Right planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(half.X + plankThickness, 0, z),
                new Vector3(plankThickness * 2, size * 0.9f, plankWidth * 0.8f));
            // Left planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(-half.X - plankThickness, 0, z),
                new Vector3(plankThickness * 2, size * 0.9f, plankWidth * 0.8f));
        }

        // Top planks - horizontal
        for (int i = -1; i <= 1; i++)
        {
            float z = i * spacing;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, half.Y + plankThickness, z),
                new Vector3(size * 0.9f, plankThickness * 2, plankWidth * 0.8f));
        }

        // Add corner reinforcements (metal brackets)
        float bracketSize = size * 0.15f;
        float bracketThickness = 0.02f * PropScale;
        Vector3[] corners = new Vector3[]
        {
            new Vector3( half.X,  half.Y,  half.Z),
            new Vector3(-half.X,  half.Y,  half.Z),
            new Vector3( half.X,  half.Y, -half.Z),
            new Vector3(-half.X,  half.Y, -half.Z),
            new Vector3( half.X, -half.Y,  half.Z),
            new Vector3(-half.X, -half.Y,  half.Z),
            new Vector3( half.X, -half.Y, -half.Z),
            new Vector3(-half.X, -half.Y, -half.Z),
        };

        foreach (var corner in corners)
        {
            float sx = Mathf.Sign(corner.X);
            float sy = Mathf.Sign(corner.Y);
            float sz = Mathf.Sign(corner.Z);

            // Vertical edge bracket
            AddBoxToSurfaceTool(surfaceTool, corner + new Vector3(0, -sy * bracketSize / 2, 0),
                new Vector3(bracketThickness, bracketSize, bracketThickness));

            // Horizontal brackets
            AddBoxToSurfaceTool(surfaceTool, corner + new Vector3(-sx * bracketSize / 2, 0, 0),
                new Vector3(bracketSize, bracketThickness, bracketThickness));
            AddBoxToSurfaceTool(surfaceTool, corner + new Vector3(0, 0, -sz * bracketSize / 2),
                new Vector3(bracketThickness, bracketThickness, bracketSize));
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();

        var material = ProceduralMesh3D.CreateWoodMaterial(PrimaryColor);
        material.Roughness = 0.85f;
        mesh.SurfaceSetMaterial(0, material);

        return mesh;
    }

    private ArrayMesh CreatePotMesh(RandomNumberGenerator rng)
    {
        float topR = Constants.PotRadius * PropScale;
        float botR = Constants.PotRadius * 0.7f * PropScale;
        float h = Constants.PotHeight * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        int segments = 16; // More segments for smoother curves
        float angleStep = Mathf.Tau / segments;
        int heightSegments = 10;

        // Create pot body with slight texture variation
        for (int hs = 0; hs <= heightSegments; hs++)
        {
            float t = (float)hs / heightSegments;
            float y = -h / 2f + t * h;
            float radius = Mathf.Lerp(botR, topR, t);

            // Add subtle bumps for handcrafted look
            float wobble = 0.02f * PropScale * Mathf.Sin(t * Mathf.Pi * 3);

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float r = radius + wobble * Mathf.Sin(angle * 4);
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                Vector3 normal = new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle)).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(new Vector2((float)i / segments, t));
                surfaceTool.AddVertex(new Vector3(x, y, z));
            }
        }

        // Generate triangles
        for (int hs = 0; hs < heightSegments; hs++)
        {
            for (int i = 0; i < segments; i++)
            {
                int baseIdx = hs * (segments + 1) + i;
                surfaceTool.AddIndex(baseIdx);
                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 1);

                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 2);
                surfaceTool.AddIndex(baseIdx + segments + 1);
            }
        }

        // Add decorative rim with carved pattern
        float rimHeight = h * 0.08f;
        float rimTop = h / 2f;
        int rimStart = (heightSegments + 1) * (segments + 1);

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            // Carved pattern - alternating depth
            float carveDepth = (i % 2 == 0) ? 0.01f : 0f;
            float rimR = topR * 1.1f - carveDepth * PropScale;

            float x = Mathf.Cos(angle) * rimR;
            float z = Mathf.Sin(angle) * rimR;

            surfaceTool.SetNormal(Vector3.Up);
            surfaceTool.AddVertex(new Vector3(x, rimTop, z));
            surfaceTool.SetNormal(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)));
            surfaceTool.AddVertex(new Vector3(x, rimTop - rimHeight, z));

            if (i < segments)
            {
                int idx = rimStart + i * 2;
                surfaceTool.AddIndex(idx);
                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + 2);

                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + 3);
                surfaceTool.AddIndex(idx + 2);
            }
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();

        var material = ProceduralMesh3D.CreateMaterial(PrimaryColor, 0.75f);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreatePillarMesh(RandomNumberGenerator rng)
    {
        float radius = 0.25f * PropScale;
        float height = Constants.WallHeight * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        int segments = 12;
        float angleStep = Mathf.Tau / segments;

        // Base pedestal (wider, shorter)
        float baseHeight = height * 0.12f;
        float baseRadius = radius * 1.4f;
        int baseSegments = 3;

        for (int hs = 0; hs <= baseSegments; hs++)
        {
            float t = (float)hs / baseSegments;
            float y = t * baseHeight;
            float r = Mathf.Lerp(baseRadius, radius, t);

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(new Vector2((float)i / segments, t));
                surfaceTool.AddVertex(new Vector3(x, y, z));
            }
        }

        // Triangles for base
        for (int hs = 0; hs < baseSegments; hs++)
        {
            for (int i = 0; i < segments; i++)
            {
                int baseIdx = hs * (segments + 1) + i;
                surfaceTool.AddIndex(baseIdx);
                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 1);

                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 2);
                surfaceTool.AddIndex(baseIdx + segments + 1);
            }
        }

        // Main column with fluting (vertical grooves)
        float mainStart = baseHeight;
        float mainEnd = height * 0.88f;
        float mainHeight = mainEnd - mainStart;
        int mainSegments = 12;

        for (int hs = 0; hs <= mainSegments; hs++)
        {
            float t = (float)hs / mainSegments;
            float y = mainStart + t * mainHeight;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                // Fluting - create grooves
                float flutingDepth = 0.03f * PropScale * Mathf.Abs(Mathf.Sin(angle * segments / 2f));
                float r = radius - flutingDepth;

                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(new Vector2((float)i / segments, t + 0.3f));
                surfaceTool.AddVertex(new Vector3(x, y, z));
            }
        }

        // Triangles for main column
        int mainStartIdx = (baseSegments + 1) * (segments + 1);
        for (int hs = 0; hs < mainSegments; hs++)
        {
            for (int i = 0; i < segments; i++)
            {
                int baseIdx = mainStartIdx + hs * (segments + 1) + i;
                surfaceTool.AddIndex(baseIdx);
                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 1);

                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 2);
                surfaceTool.AddIndex(baseIdx + segments + 1);
            }
        }

        // Capital (decorative top)
        float capitalStart = mainEnd;
        float capitalHeight = height * 0.12f;
        int capitalSegments = 3;

        for (int hs = 0; hs <= capitalSegments; hs++)
        {
            float t = (float)hs / capitalSegments;
            float y = capitalStart + t * capitalHeight;
            float r = Mathf.Lerp(radius, radius * 1.3f, t);

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                Vector3 normal = new Vector3(Mathf.Cos(angle), 0.3f, Mathf.Sin(angle)).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(new Vector2((float)i / segments, t + 0.8f));
                surfaceTool.AddVertex(new Vector3(x, y, z));
            }
        }

        // Triangles for capital
        int capitalStartIdx = mainStartIdx + (mainSegments + 1) * (segments + 1);
        for (int hs = 0; hs < capitalSegments; hs++)
        {
            for (int i = 0; i < segments; i++)
            {
                int baseIdx = capitalStartIdx + hs * (segments + 1) + i;
                surfaceTool.AddIndex(baseIdx);
                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 1);

                surfaceTool.AddIndex(baseIdx + 1);
                surfaceTool.AddIndex(baseIdx + segments + 2);
                surfaceTool.AddIndex(baseIdx + segments + 1);
            }
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();

        var material = ProceduralMesh3D.CreateMaterial(PrimaryColor, 0.85f);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateCrystalMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float radius = 0.15f * PropScale;
        float height = 0.5f * PropScale;
        int sides = 8; // More facets for better crystal appearance

        float halfHeight = height / 2f;
        Vector3 topPoint = new Vector3(0, halfHeight, 0);
        Vector3 bottomPoint = new Vector3(0, -halfHeight, 0);

        // Create middle ring with varied heights for natural crystal look
        float angleStep = Mathf.Tau / sides;
        Vector3[] midPoints = new Vector3[sides];
        float[] midHeights = new float[sides];

        for (int i = 0; i < sides; i++)
        {
            float angle = i * angleStep;
            // Vary the radius and height slightly for irregular crystal
            float radiusVar = radius * (0.9f + rng.Randf() * 0.2f);
            midHeights[i] = (rng.Randf() - 0.5f) * 0.15f * PropScale;

            midPoints[i] = new Vector3(
                Mathf.Cos(angle) * radiusVar,
                midHeights[i],
                Mathf.Sin(angle) * radiusVar
            );
        }

        // Create faceted triangles from top
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 v1 = midPoints[i];
            Vector3 v2 = midPoints[next];

            Vector3 edge1 = v1 - topPoint;
            Vector3 edge2 = v2 - topPoint;
            Vector3 normal = edge2.Cross(edge1).Normalized();

            surfaceTool.SetNormal(normal);
            surfaceTool.AddVertex(topPoint);
            surfaceTool.SetNormal(normal);
            surfaceTool.AddVertex(v1);
            surfaceTool.SetNormal(normal);
            surfaceTool.AddVertex(v2);
        }

        // Create faceted triangles from bottom
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 v1 = midPoints[i];
            Vector3 v2 = midPoints[next];

            Vector3 edge1 = v1 - bottomPoint;
            Vector3 edge2 = v2 - bottomPoint;
            Vector3 normal = edge1.Cross(edge2).Normalized();

            surfaceTool.SetNormal(normal);
            surfaceTool.AddVertex(bottomPoint);
            surfaceTool.SetNormal(normal);
            surfaceTool.AddVertex(v2);
            surfaceTool.SetNormal(normal);
            surfaceTool.AddVertex(v1);
        }

        // Add small crystal clusters at base
        int numClusters = 3;
        for (int c = 0; c < numClusters; c++)
        {
            float clusterAngle = c * Mathf.Tau / numClusters + rng.Randf() * 0.3f;
            float clusterDist = radius * 0.7f;
            Vector3 clusterBase = new Vector3(
                Mathf.Cos(clusterAngle) * clusterDist,
                -halfHeight * 0.8f,
                Mathf.Sin(clusterAngle) * clusterDist
            );

            float clusterSize = radius * 0.3f;
            Vector3 clusterTop = clusterBase + new Vector3(0, clusterSize * 1.5f, 0);

            // Small crystal pyramid
            int clusterSides = 4;
            for (int i = 0; i < clusterSides; i++)
            {
                float a1 = i * Mathf.Tau / clusterSides;
                float a2 = (i + 1) * Mathf.Tau / clusterSides;

                Vector3 p1 = clusterBase + new Vector3(Mathf.Cos(a1) * clusterSize * 0.3f, 0, Mathf.Sin(a1) * clusterSize * 0.3f);
                Vector3 p2 = clusterBase + new Vector3(Mathf.Cos(a2) * clusterSize * 0.3f, 0, Mathf.Sin(a2) * clusterSize * 0.3f);

                Vector3 normal = (clusterTop - p1).Cross(clusterTop - p2).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.AddVertex(clusterTop);
                surfaceTool.AddVertex(p1);
                surfaceTool.AddVertex(p2);
            }
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();

        // Enhanced crystal material with better transparency and glow
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(PrimaryColor.R, PrimaryColor.G, PrimaryColor.B, 0.7f);
        material.Roughness = 0.05f;
        material.Metallic = 0.1f;
        material.EmissionEnabled = true;
        material.Emission = PrimaryColor.Lightened(0.3f);
        material.EmissionEnergyMultiplier = 1.2f;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateTorchMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;

        // Wooden handle - starts from Y=0 (ground/wall mount point)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.2f * s, 0), new Vector3(0.04f * s, 0.4f * s, 0.04f * s));

        // Wall bracket (L-shape) - attaches to wall at Z+
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.35f * s, 0.04f * s), new Vector3(0.06f * s, 0.02f * s, 0.1f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.28f * s, 0.08f * s), new Vector3(0.06f * s, 0.16f * s, 0.02f * s));

        // Flame holder (cup shape at top)
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6f;
            float radius = 0.05f * s;
            float holderY = 0.42f * s;

            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, holderY, Mathf.Sin(angle) * radius);
            AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(0.012f * s, 0.06f * s, 0.012f * s));
        }

        // Flame base (glowing ember)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.46f * s, 0), new Vector3(0.04f * s, 0.02f * s, 0.04f * s));

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateWoodMaterial(SecondaryColor);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateChestMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;
        Vector3 size = new Vector3(0.7f, 0.45f, 0.45f) * s;

        // Main chest body
        AddBoxToSurfaceTool(surfaceTool, Vector3.Zero, size);

        // Lid (slightly arched)
        float lidThickness = 0.08f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, size.Y / 2f + lidThickness / 2f, 0),
            new Vector3(size.X, lidThickness, size.Z));

        // Metal bands (3 horizontal)
        float bandThickness = 0.02f * s;
        float bandWidth = 0.04f * s;
        for (int i = 0; i < 3; i++)
        {
            float y = -size.Y / 2f + (i + 1) * size.Y / 4f;
            // Front band
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, y, size.Z / 2f + bandThickness / 2f),
                new Vector3(size.X * 1.05f, bandWidth, bandThickness));
            // Back band
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, y, -size.Z / 2f - bandThickness / 2f),
                new Vector3(size.X * 1.05f, bandWidth, bandThickness));
        }

        // Lock (center front)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0, size.Z / 2f + 0.03f * s),
            new Vector3(0.08f * s, 0.1f * s, 0.02f * s));

        // Hinges (back top, 2 hinges)
        for (int i = -1; i <= 1; i += 2)
        {
            AddBoxToSurfaceTool(surfaceTool, new Vector3(i * size.X * 0.3f, size.Y / 2f + 0.02f * s, -size.Z / 2f - 0.01f * s),
                new Vector3(0.1f * s, 0.04f * s, 0.04f * s));
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateWoodMaterial(PrimaryColor);
        material.Roughness = 0.8f;
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateTableMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;

        // Tabletop with worn edges
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.75f * s, 0), new Vector3(1f * s, 0.08f * s, 0.6f * s));

        // Four detailed legs with cross-bracing
        float legWidth = 0.06f * s;
        float legHeight = 0.7f * s;
        Vector3[] legPositions = new Vector3[]
        {
            new Vector3(-0.45f * s, legHeight / 2f, -0.25f * s),
            new Vector3(0.45f * s, legHeight / 2f, -0.25f * s),
            new Vector3(-0.45f * s, legHeight / 2f, 0.25f * s),
            new Vector3(0.45f * s, legHeight / 2f, 0.25f * s),
        };

        foreach (var pos in legPositions)
        {
            // Main leg
            AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(legWidth, legHeight, legWidth));
            // Decorative cap
            AddBoxToSurfaceTool(surfaceTool, pos + new Vector3(0, legHeight / 2f + 0.02f * s, 0),
                new Vector3(legWidth * 1.3f, 0.04f * s, legWidth * 1.3f));
        }

        // Cross braces
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.25f * s, -0.25f * s),
            new Vector3(0.9f * s, 0.03f * s, 0.04f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.25f * s, 0.25f * s),
            new Vector3(0.9f * s, 0.03f * s, 0.04f * s));

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateWoodMaterial(PrimaryColor);
        material.Roughness = 0.75f;
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateBookshelfMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;

        // Shelf frame
        AddBoxToSurfaceTool(surfaceTool, Vector3.Zero, new Vector3(1.2f * s, 2f * s, 0.35f * s));

        // Shelves (4 horizontal)
        for (int i = 0; i < 4; i++)
        {
            float y = -0.9f * s + i * 0.6f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, y, 0), new Vector3(1.15f * s, 0.03f * s, 0.32f * s));
        }

        // Individual books on shelves
        for (int shelf = 0; shelf < 4; shelf++)
        {
            float shelfY = -0.87f * s + shelf * 0.6f * s;
            int numBooks = 6 + (int)(rng.Randf() * 3);

            for (int book = 0; book < numBooks; book++)
            {
                float bookX = -0.5f * s + (book * 1f * s / numBooks);
                float bookWidth = 0.12f * s + rng.Randf() * 0.05f * s;
                float bookHeight = 0.3f * s + rng.Randf() * 0.15f * s;
                float tilt = (rng.Randf() - 0.5f) * 0.1f;

                // Simplified book as box with slight tilt
                var bookPos = new Vector3(bookX, shelfY + bookHeight / 2f, 0.05f * s * (rng.Randf() - 0.5f));
                AddBoxToSurfaceTool(surfaceTool, bookPos, new Vector3(bookWidth, bookHeight, 0.06f * s));
            }
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateWoodMaterial(PrimaryColor.Darkened(0.2f));
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateCauldronMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;
        float radius = 0.4f * s;
        float height = 0.45f * s;
        int segments = 16;

        // Main cauldron body (pot shape with handles)
        float angleStep = Mathf.Tau / segments;
        for (int h = 0; h <= 8; h++)
        {
            float t = h / 8f;
            float y = -height / 2f + t * height;
            float r = Mathf.Lerp(radius * 0.85f, radius, Mathf.Sin(t * Mathf.Pi));

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.AddVertex(new Vector3(x, y, z));
            }
        }

        // Generate triangles for cauldron body
        for (int h = 0; h < 8; h++)
        {
            for (int i = 0; i < segments; i++)
            {
                int idx = h * (segments + 1) + i;
                surfaceTool.AddIndex(idx);
                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + segments + 1);

                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + segments + 2);
                surfaceTool.AddIndex(idx + segments + 1);
            }
        }

        // Add rim with decorative edge
        float rimY = height / 2f;
        int rimStartIdx = (8 + 1) * (segments + 1);

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float r1 = radius;
            float r2 = radius * 1.08f;

            surfaceTool.SetNormal(Vector3.Up);
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle) * r1, rimY, Mathf.Sin(angle) * r1));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle) * r2, rimY + 0.03f * s, Mathf.Sin(angle) * r2));

            if (i < segments)
            {
                int idx = rimStartIdx + i * 2;
                surfaceTool.AddIndex(idx);
                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + 2);

                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + 3);
                surfaceTool.AddIndex(idx + 2);
            }
        }

        // Handles (2 opposite sides)
        for (int h = 0; h < 2; h++)
        {
            float angle = h * Mathf.Pi;
            Vector3 handleBase = new Vector3(Mathf.Cos(angle) * radius * 0.9f, 0, Mathf.Sin(angle) * radius * 0.9f);
            Vector3 handleEnd = handleBase + new Vector3(Mathf.Cos(angle) * 0.15f * s, 0.05f * s, Mathf.Sin(angle) * 0.15f * s);

            // Simple handle as curved box
            AddBoxToSurfaceTool(surfaceTool, (handleBase + handleEnd) / 2f, new Vector3(0.12f * s, 0.03f * s, 0.03f * s));
        }

        // Three legs
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.Tau / 3f;
            Vector3 legBase = new Vector3(Mathf.Cos(angle) * radius * 0.7f, -height / 2f - 0.15f * s, Mathf.Sin(angle) * radius * 0.7f);
            AddBoxToSurfaceTool(surfaceTool, legBase, new Vector3(0.05f * s, 0.3f * s, 0.05f * s));
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateMetalMaterial(AccentColor, 0.45f);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateStatueMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;
        int segments = 10;

        // Pedestal base
        float baseHeight = 0.3f * s;
        float baseRadius = 0.35f * s;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;

            Vector3 b1 = new Vector3(Mathf.Cos(angle1) * baseRadius, 0, Mathf.Sin(angle1) * baseRadius);
            Vector3 b2 = new Vector3(Mathf.Cos(angle2) * baseRadius, 0, Mathf.Sin(angle2) * baseRadius);
            Vector3 t1 = new Vector3(Mathf.Cos(angle1) * 0.25f * s, baseHeight, Mathf.Sin(angle1) * 0.25f * s);
            Vector3 t2 = new Vector3(Mathf.Cos(angle2) * 0.25f * s, baseHeight, Mathf.Sin(angle2) * 0.25f * s);

            Vector3 normal = ((b1 + b2) / 2f).Normalized();
            surfaceTool.SetNormal(normal);
            surfaceTool.AddVertex(b1);
            surfaceTool.AddVertex(b2);
            surfaceTool.AddVertex(t2);

            surfaceTool.AddVertex(b1);
            surfaceTool.AddVertex(t2);
            surfaceTool.AddVertex(t1);
        }

        // Main statue body (humanoid cylinder with variations)
        float bodyHeight = 1.3f * s;
        for (int h = 0; h <= 10; h++)
        {
            float t = h / 10f;
            float y = baseHeight + t * bodyHeight;
            float r = 0.25f * s;
            if (t > 0.7f) r *= 0.9f; // Narrower at shoulders/head
            if (t > 0.4f && t < 0.6f) r *= 1.1f; // Wider at chest

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.Tau / segments;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).Normalized();
                surfaceTool.SetNormal(normal);
                surfaceTool.AddVertex(new Vector3(x, y, z));
            }
        }

        // Generate triangles for body
        int bodyStartIdx = segments * 6; // After pedestal
        for (int h = 0; h < 10; h++)
        {
            for (int i = 0; i < segments; i++)
            {
                int idx = bodyStartIdx + h * (segments + 1) + i;
                surfaceTool.AddIndex(idx);
                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + segments + 1);

                surfaceTool.AddIndex(idx + 1);
                surfaceTool.AddIndex(idx + segments + 2);
                surfaceTool.AddIndex(idx + segments + 1);
            }
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateMaterial(PrimaryColor, 0.7f, 0.1f);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateChairMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;

        // Seat with slightly curved top
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.4f * s, 0), new Vector3(0.45f * s, 0.06f * s, 0.45f * s));

        // Backrest with vertical slats
        float backWidth = 0.45f * s;
        float backHeight = 0.5f * s;
        float backY = 0.7f * s;
        float backZ = -0.2f * s;

        // Backrest frame
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, backY, backZ), new Vector3(backWidth, backHeight, 0.04f * s));

        // Vertical slats (5 slats)
        int numSlats = 5;
        for (int i = 0; i < numSlats; i++)
        {
            float slatX = -backWidth / 2f + (i + 0.5f) * backWidth / numSlats;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(slatX, backY, backZ - 0.01f * s),
                new Vector3(0.04f * s, backHeight * 0.9f, 0.02f * s));
        }

        // Four legs with turned detail
        float legWidth = 0.045f * s;
        float legHeight = 0.4f * s;
        Vector3[] legPositions = new Vector3[]
        {
            new Vector3(-0.18f * s, legHeight / 2f, -0.18f * s),
            new Vector3(0.18f * s, legHeight / 2f, -0.18f * s),
            new Vector3(-0.18f * s, legHeight / 2f, 0.18f * s),
            new Vector3(0.18f * s, legHeight / 2f, 0.18f * s),
        };

        foreach (var pos in legPositions)
        {
            // Main leg
            AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(legWidth, legHeight, legWidth));

            // Turned sections (decorative bulges)
            AddBoxToSurfaceTool(surfaceTool, pos + new Vector3(0, legHeight * 0.2f, 0),
                new Vector3(legWidth * 1.3f, legHeight * 0.1f, legWidth * 1.3f));
            AddBoxToSurfaceTool(surfaceTool, pos - new Vector3(0, legHeight * 0.3f, 0),
                new Vector3(legWidth * 1.2f, legHeight * 0.08f, legWidth * 1.2f));
        }

        // Cross braces (support between legs)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.15f * s, -0.18f * s),
            new Vector3(0.35f * s, 0.03f * s, 0.03f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.15f * s, 0.18f * s),
            new Vector3(0.35f * s, 0.03f * s, 0.03f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.18f * s, 0.15f * s, 0),
            new Vector3(0.03f * s, 0.03f * s, 0.35f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.18f * s, 0.15f * s, 0),
            new Vector3(0.03f * s, 0.03f * s, 0.35f * s));

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateWoodMaterial(PrimaryColor);
        material.Roughness = 0.8f;
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateSkullPileMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;
        var boneColor = new Color(0.9f, 0.85f, 0.75f);

        // Define skull positions with random rotations
        Vector3[] skullPositions = new Vector3[]
        {
            // Base layer - larger skulls
            new Vector3(-0.15f * s, 0.1f * s, -0.1f * s),
            new Vector3(0.15f * s, 0.1f * s, -0.1f * s),
            new Vector3(0f * s, 0.1f * s, 0.15f * s),
            new Vector3(-0.2f * s, 0.08f * s, 0.12f * s),
            new Vector3(0.18f * s, 0.09f * s, 0.1f * s),
            // Top layer - smaller skulls
            new Vector3(0f * s, 0.25f * s, 0f * s),
            new Vector3(-0.1f * s, 0.22f * s, 0.08f * s),
            new Vector3(0.1f * s, 0.23f * s, -0.05f * s),
        };

        float[] skullSizes = { 0.12f, 0.11f, 0.13f, 0.1f, 0.11f, 0.1f, 0.08f, 0.09f };

        for (int i = 0; i < skullPositions.Length; i++)
        {
            Vector3 pos = skullPositions[i];
            float size = skullSizes[i] * s;
            float angle = rng.Randf() * Mathf.Tau;

            // Skull cranium (main sphere, slightly elongated)
            AddSphereToSurfaceTool(surfaceTool, pos, size);

            // Eye sockets (darker indentations)
            Vector3 faceDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 rightEye = pos + faceDir * size * 0.4f + new Vector3(-Mathf.Sin(angle), 0.1f * size, Mathf.Cos(angle)) * size * 0.3f;
            Vector3 leftEye = pos + faceDir * size * 0.4f + new Vector3(Mathf.Sin(angle), 0.1f * size, -Mathf.Cos(angle)) * size * 0.3f;

            // Add small spheres for eye depth
            AddSphereToSurfaceTool(surfaceTool, rightEye, size * 0.15f);
            AddSphereToSurfaceTool(surfaceTool, leftEye, size * 0.15f);

            // Jaw bone (smaller sphere below)
            Vector3 jawPos = pos + faceDir * size * 0.3f - new Vector3(0, size * 0.5f, 0);
            AddSphereToSurfaceTool(surfaceTool, jawPos, size * 0.4f);

            // Teeth (small boxes)
            for (int t = 0; t < 4; t++)
            {
                float toothX = -size * 0.15f + t * size * 0.1f;
                Vector3 toothPos = jawPos + faceDir * size * 0.3f + new Vector3(toothX * Mathf.Cos(angle), 0, toothX * Mathf.Sin(angle));
                AddBoxToSurfaceTool(surfaceTool, toothPos, new Vector3(size * 0.05f, size * 0.08f, size * 0.05f));
            }
        }

        // Add scattered bones between skulls
        for (int b = 0; b < 5; b++)
        {
            Vector3 bonePos = new Vector3(
                (rng.Randf() - 0.5f) * 0.4f * s,
                rng.Randf() * 0.15f * s,
                (rng.Randf() - 0.5f) * 0.4f * s
            );

            float boneAngle = rng.Randf() * Mathf.Pi;
            Vector3 boneDir = new Vector3(Mathf.Cos(boneAngle), 0, Mathf.Sin(boneAngle));

            // Long bone (femur-like)
            AddBoxToSurfaceTool(surfaceTool, bonePos, new Vector3(0.08f * s, 0.03f * s, 0.02f * s));

            // Bone ends (knobs)
            AddSphereToSurfaceTool(surfaceTool, bonePos + boneDir * 0.04f * s, 0.02f * s);
            AddSphereToSurfaceTool(surfaceTool, bonePos - boneDir * 0.04f * s, 0.02f * s);
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D();
        material.AlbedoColor = boneColor;
        material.Roughness = 0.85f;
        material.Metallic = 0f;
        mesh.SurfaceSetMaterial(0, material);

        return mesh;
    }

    private ArrayMesh CreateCampfireMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float s = PropScale;
        var woodColor = new Color(0.35f, 0.2f, 0.1f);
        var charColor = new Color(0.15f, 0.1f, 0.08f);
        var rockColor = new Color(0.4f, 0.4f, 0.42f);

        // Stone ring around the fire
        int numRocks = 8;
        for (int i = 0; i < numRocks; i++)
        {
            float angle = i * Mathf.Pi * 2f / numRocks;
            float rockX = Mathf.Cos(angle) * 0.4f * s;
            float rockZ = Mathf.Sin(angle) * 0.4f * s;
            float rockSize = (0.1f + rng.Randf() * 0.05f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(rockX, rockSize * 0.5f, rockZ), rockSize);
        }

        // Logs in the fire (crossed pattern)
        // Log 1 - diagonal
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(-0.15f * s, 0.08f * s, -0.1f * s),
            new Vector3(0.2f * s, 0.08f * s, 0.15f * s), 0.04f * s);
        // Log 2 - other diagonal
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(0.15f * s, 0.08f * s, -0.1f * s),
            new Vector3(-0.15f * s, 0.1f * s, 0.12f * s), 0.045f * s);
        // Log 3 - vertical-ish
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(0f * s, 0.05f * s, 0.18f * s),
            new Vector3(0.05f * s, 0.2f * s, -0.05f * s), 0.035f * s);
        // Log 4 - leaning
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(-0.2f * s, 0.05f * s, 0.1f * s),
            new Vector3(0.1f * s, 0.15f * s, 0f * s), 0.04f * s);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D();
        material.AlbedoColor = woodColor.Lerp(charColor, 0.5f);
        material.Roughness = 0.95f;
        mesh.SurfaceSetMaterial(0, material);

        // Create fire particles (called after mesh is added)
        CallDeferred(nameof(CreateCampfireParticles));

        return mesh;
    }

    private void AddCylinderToSurfaceTool(SurfaceTool st, Vector3 start, Vector3 end, float radius)
    {
        // Create a simple cylinder between two points using quads
        Vector3 dir = (end - start).Normalized();
        float length = start.DistanceTo(end);

        // Find perpendicular vectors
        Vector3 perp1 = dir.Cross(Vector3.Up).Normalized();
        if (perp1.LengthSquared() < 0.01f) perp1 = dir.Cross(Vector3.Right).Normalized();
        Vector3 perp2 = dir.Cross(perp1).Normalized();

        int segments = 6;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Pi * 2f / segments;
            float angle2 = (i + 1) * Mathf.Pi * 2f / segments;

            Vector3 offset1 = (perp1 * Mathf.Cos(angle1) + perp2 * Mathf.Sin(angle1)) * radius;
            Vector3 offset2 = (perp1 * Mathf.Cos(angle2) + perp2 * Mathf.Sin(angle2)) * radius;

            Vector3 v1 = start + offset1;
            Vector3 v2 = start + offset2;
            Vector3 v3 = end + offset2;
            Vector3 v4 = end + offset1;

            // Two triangles for the quad
            st.AddVertex(v1);
            st.AddVertex(v2);
            st.AddVertex(v3);

            st.AddVertex(v1);
            st.AddVertex(v3);
            st.AddVertex(v4);
        }
    }

    private void CreateCampfireParticles()
    {
        // Fire particles
        var fireParticles = new GpuParticles3D();
        fireParticles.Name = "FireParticles";
        fireParticles.Amount = 50;
        fireParticles.Lifetime = 1.0;
        fireParticles.Preprocess = 0.5;
        fireParticles.Position = new Vector3(0, 0.25f * PropScale, 0);

        var fireMaterial = new ParticleProcessMaterial();
        fireMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        fireMaterial.EmissionSphereRadius = 0.15f * PropScale;
        fireMaterial.Direction = new Vector3(0, 1, 0);
        fireMaterial.Spread = 15f;
        fireMaterial.InitialVelocityMin = 0.5f;
        fireMaterial.InitialVelocityMax = 1.5f;
        fireMaterial.Gravity = new Vector3(0, 2f, 0); // Rise up
        fireMaterial.ScaleMin = 0.1f;
        fireMaterial.ScaleMax = 0.3f;
        fireMaterial.Color = new Color(1f, 0.6f, 0.1f);
        // Color ramp from yellow to red to transparent
        var colorRamp = new Gradient();
        colorRamp.SetColor(0, new Color(1f, 0.9f, 0.3f, 1f));
        colorRamp.SetColor(1, new Color(1f, 0.3f, 0.1f, 0f));
        var colorTexture = new GradientTexture1D();
        colorTexture.Gradient = colorRamp;
        fireMaterial.ColorRamp = colorTexture;

        fireParticles.ProcessMaterial = fireMaterial;

        // Use a simple quad mesh for fire particles
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.2f, 0.3f);
        var fireMat = new StandardMaterial3D();
        fireMat.AlbedoColor = new Color(1f, 0.6f, 0.2f);
        fireMat.EmissionEnabled = true;
        fireMat.Emission = new Color(1f, 0.5f, 0.1f);
        fireMat.EmissionEnergyMultiplier = 2f;
        fireMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        fireMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        fireMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        quadMesh.Material = fireMat;
        fireParticles.DrawPass1 = quadMesh;

        AddChild(fireParticles);

        // Smoke particles (above fire)
        var smokeParticles = new GpuParticles3D();
        smokeParticles.Name = "SmokeParticles";
        smokeParticles.Amount = 20;
        smokeParticles.Lifetime = 3.0;
        smokeParticles.Preprocess = 1.0;
        smokeParticles.Position = new Vector3(0, 0.6f * PropScale, 0);

        var smokeMaterial = new ParticleProcessMaterial();
        smokeMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        smokeMaterial.EmissionSphereRadius = 0.1f * PropScale;
        smokeMaterial.Direction = new Vector3(0, 1, 0);
        smokeMaterial.Spread = 20f;
        smokeMaterial.InitialVelocityMin = 0.3f;
        smokeMaterial.InitialVelocityMax = 0.8f;
        smokeMaterial.Gravity = new Vector3(0, 0.5f, 0);
        smokeMaterial.ScaleMin = 0.2f;
        smokeMaterial.ScaleMax = 0.6f;
        // Smoke color ramp
        var smokeRamp = new Gradient();
        smokeRamp.SetColor(0, new Color(0.3f, 0.3f, 0.3f, 0.4f));
        smokeRamp.SetColor(1, new Color(0.2f, 0.2f, 0.2f, 0f));
        var smokeTexture = new GradientTexture1D();
        smokeTexture.Gradient = smokeRamp;
        smokeMaterial.ColorRamp = smokeTexture;

        smokeParticles.ProcessMaterial = smokeMaterial;

        var smokeQuad = new QuadMesh();
        smokeQuad.Size = new Vector2(0.4f, 0.4f);
        var smokeMat = new StandardMaterial3D();
        smokeMat.AlbedoColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        smokeMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        smokeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        smokeMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        smokeQuad.Material = smokeMat;
        smokeParticles.DrawPass1 = smokeQuad;

        AddChild(smokeParticles);

        // Ember/spark particles
        var emberParticles = new GpuParticles3D();
        emberParticles.Name = "EmberParticles";
        emberParticles.Amount = 15;
        emberParticles.Lifetime = 2.0;
        emberParticles.Position = new Vector3(0, 0.3f * PropScale, 0);

        var emberMaterial = new ParticleProcessMaterial();
        emberMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        emberMaterial.EmissionSphereRadius = 0.2f * PropScale;
        emberMaterial.Direction = new Vector3(0, 1, 0);
        emberMaterial.Spread = 45f;
        emberMaterial.InitialVelocityMin = 1f;
        emberMaterial.InitialVelocityMax = 3f;
        emberMaterial.Gravity = new Vector3(0, -1f, 0); // Embers float then fall
        emberMaterial.ScaleMin = 0.02f;
        emberMaterial.ScaleMax = 0.05f;
        // Ember color
        var emberRamp = new Gradient();
        emberRamp.SetColor(0, new Color(1f, 0.8f, 0.2f, 1f));
        emberRamp.SetColor(1, new Color(1f, 0.3f, 0.1f, 0f));
        var emberTexture = new GradientTexture1D();
        emberTexture.Gradient = emberRamp;
        emberMaterial.ColorRamp = emberTexture;

        emberParticles.ProcessMaterial = emberMaterial;

        var emberMesh = new SphereMesh();
        emberMesh.Radius = 0.02f;
        emberMesh.Height = 0.04f;
        var emberMat = new StandardMaterial3D();
        emberMat.AlbedoColor = new Color(1f, 0.7f, 0.2f);
        emberMat.EmissionEnabled = true;
        emberMat.Emission = new Color(1f, 0.5f, 0.1f);
        emberMat.EmissionEnergyMultiplier = 3f;
        emberMesh.Material = emberMat;
        emberParticles.DrawPass1 = emberMesh;

        AddChild(emberParticles);
    }

    private void AddBoxToSurfaceTool(SurfaceTool st, Vector3 center, Vector3 size)
    {
        // Helper to add a box to the surface tool
        Vector3 half = size / 2f;

        // Define the 8 vertices
        Vector3[] verts = {
            center + new Vector3(-half.X, -half.Y, -half.Z),
            center + new Vector3(half.X, -half.Y, -half.Z),
            center + new Vector3(half.X, half.Y, -half.Z),
            center + new Vector3(-half.X, half.Y, -half.Z),
            center + new Vector3(-half.X, -half.Y, half.Z),
            center + new Vector3(half.X, -half.Y, half.Z),
            center + new Vector3(half.X, half.Y, half.Z),
            center + new Vector3(-half.X, half.Y, half.Z),
        };

        // Define the 6 faces (2 triangles each)
        int[][] faces = {
            new[] {0, 1, 2, 0, 2, 3}, // front
            new[] {5, 4, 7, 5, 7, 6}, // back
            new[] {4, 0, 3, 4, 3, 7}, // left
            new[] {1, 5, 6, 1, 6, 2}, // right
            new[] {3, 2, 6, 3, 6, 7}, // top
            new[] {4, 5, 1, 4, 1, 0}, // bottom
        };

        foreach (var face in faces)
        {
            foreach (var idx in face)
            {
                st.AddVertex(verts[idx]);
            }
        }
    }

    private void AddRotatedBoxToSurfaceTool(SurfaceTool st, Vector3 center, Vector3 size, float yRotation)
    {
        // Helper to add a Y-axis rotated box to the surface tool
        Vector3 half = size / 2f;
        float cos = Mathf.Cos(yRotation);
        float sin = Mathf.Sin(yRotation);

        // Define the 8 vertices (local space, unrotated)
        Vector3[] localVerts = {
            new Vector3(-half.X, -half.Y, -half.Z),
            new Vector3(half.X, -half.Y, -half.Z),
            new Vector3(half.X, half.Y, -half.Z),
            new Vector3(-half.X, half.Y, -half.Z),
            new Vector3(-half.X, -half.Y, half.Z),
            new Vector3(half.X, -half.Y, half.Z),
            new Vector3(half.X, half.Y, half.Z),
            new Vector3(-half.X, half.Y, half.Z),
        };

        // Rotate and translate vertices
        Vector3[] verts = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            float rotX = localVerts[i].X * cos - localVerts[i].Z * sin;
            float rotZ = localVerts[i].X * sin + localVerts[i].Z * cos;
            verts[i] = center + new Vector3(rotX, localVerts[i].Y, rotZ);
        }

        // Define the 6 faces (2 triangles each)
        int[][] faces = {
            new[] {0, 1, 2, 0, 2, 3}, // front
            new[] {5, 4, 7, 5, 7, 6}, // back
            new[] {4, 0, 3, 4, 3, 7}, // left
            new[] {1, 5, 6, 1, 6, 2}, // right
            new[] {3, 2, 6, 3, 6, 7}, // top
            new[] {4, 5, 1, 4, 1, 0}, // bottom
        };

        foreach (var face in faces)
        {
            foreach (var idx in face)
            {
                st.AddVertex(verts[idx]);
            }
        }
    }

    private void AddSphereToSurfaceTool(SurfaceTool st, Vector3 center, float radius)
    {
        // Create a low-poly sphere (icosahedron-like)
        int rings = 4;
        int sectors = 6;

        for (int r = 0; r < rings; r++)
        {
            float theta1 = Mathf.Pi * r / rings;
            float theta2 = Mathf.Pi * (r + 1) / rings;

            for (int s = 0; s < sectors; s++)
            {
                float phi1 = 2 * Mathf.Pi * s / sectors;
                float phi2 = 2 * Mathf.Pi * (s + 1) / sectors;

                Vector3 v1 = center + new Vector3(
                    radius * Mathf.Sin(theta1) * Mathf.Cos(phi1),
                    radius * Mathf.Cos(theta1),
                    radius * Mathf.Sin(theta1) * Mathf.Sin(phi1));
                Vector3 v2 = center + new Vector3(
                    radius * Mathf.Sin(theta1) * Mathf.Cos(phi2),
                    radius * Mathf.Cos(theta1),
                    radius * Mathf.Sin(theta1) * Mathf.Sin(phi2));
                Vector3 v3 = center + new Vector3(
                    radius * Mathf.Sin(theta2) * Mathf.Cos(phi2),
                    radius * Mathf.Cos(theta2),
                    radius * Mathf.Sin(theta2) * Mathf.Sin(phi2));
                Vector3 v4 = center + new Vector3(
                    radius * Mathf.Sin(theta2) * Mathf.Cos(phi1),
                    radius * Mathf.Cos(theta2),
                    radius * Mathf.Sin(theta2) * Mathf.Sin(phi1));

                // Two triangles for the quad
                st.AddVertex(v1);
                st.AddVertex(v2);
                st.AddVertex(v3);

                st.AddVertex(v1);
                st.AddVertex(v3);
                st.AddVertex(v4);
            }
        }
    }

    // ============================================
    // NEW DUNGEON PROPS - Mesh Creation Methods
    // ============================================

    private ArrayMesh CreateBonePileMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Bone color with aged yellowing variation
        // Note: Using single material, variety through geometry placement

        // === DETAILED SKULL ===
        float skullY = 0.12f * s;
        float skullRadius = 0.1f * s;

        // Main cranium (slightly elongated sphere)
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, skullY + 0.02f * s, 0), skullRadius);

        // Forehead ridge
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, skullY + 0.08f * s, 0.04f * s), skullRadius * 0.5f);

        // Eye sockets (recessed areas created by surrounding bone)
        float eyeSpacing = 0.04f * s;
        float eyeSocketRadius = 0.025f * s;
        // Left eye socket rim
        AddSphereToSurfaceTool(surfaceTool, new Vector3(-eyeSpacing, skullY + 0.02f * s, 0.07f * s), eyeSocketRadius * 1.3f);
        // Right eye socket rim
        AddSphereToSurfaceTool(surfaceTool, new Vector3(eyeSpacing, skullY + 0.02f * s, 0.07f * s), eyeSocketRadius * 1.3f);

        // Nasal cavity (triangular approximation)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, skullY - 0.01f * s, 0.08f * s),
            new Vector3(0.02f * s, 0.03f * s, 0.015f * s));

        // Cheekbones (zygomatic arch)
        AddSphereToSurfaceTool(surfaceTool, new Vector3(-0.055f * s, skullY - 0.01f * s, 0.05f * s), 0.02f * s);
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0.055f * s, skullY - 0.01f * s, 0.05f * s), 0.02f * s);

        // Jaw bone (mandible) - separate piece
        float jawY = skullY - 0.06f * s;
        // Main jaw curve
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, jawY, 0.05f * s),
            new Vector3(0.07f * s, 0.025f * s, 0.02f * s));
        // Jaw sides (rami)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.04f * s, jawY + 0.015f * s, 0.02f * s),
            new Vector3(0.015f * s, 0.035f * s, 0.02f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.04f * s, jawY + 0.015f * s, 0.02f * s),
            new Vector3(0.015f * s, 0.035f * s, 0.02f * s));

        // Teeth (row of small boxes)
        for (int t = -3; t <= 3; t++)
        {
            float toothX = t * 0.008f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(toothX, jawY + 0.018f * s, 0.065f * s),
                new Vector3(0.006f * s, 0.01f * s, 0.005f * s));
        }

        // === ANATOMICALLY-SHAPED FEMURS (long bones with knobby ends) ===
        int numFemurs = rng.RandiRange(3, 5);
        for (int i = 0; i < numFemurs; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.12f, 0.35f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float boneLen = rng.RandfRange(0.18f, 0.28f) * s;
            float boneRad = rng.RandfRange(0.015f, 0.022f) * s;
            float boneY = boneRad + rng.RandfRange(0, 0.02f) * s;

            // Shaft (main bone body)
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, boneY, z),
                new Vector3(boneLen, boneRad * 2f, boneRad * 2f));

            // Proximal end (ball joint - larger knob)
            float knobOffset = boneLen / 2f - boneRad;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x + knobOffset, boneY, z), boneRad * 1.6f);

            // Distal end (condyles - two smaller knobs)
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x - knobOffset, boneY + boneRad * 0.3f, z + boneRad * 0.4f), boneRad * 1.2f);
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x - knobOffset, boneY + boneRad * 0.3f, z - boneRad * 0.4f), boneRad * 1.2f);
        }

        // === CURVED RIBS (using angled box segments) ===
        int numRibs = rng.RandiRange(4, 7);
        float ribBaseZ = -0.2f * s;
        for (int i = 0; i < numRibs; i++)
        {
            float ribX = (i - numRibs / 2f) * 0.05f * s + rng.RandfRange(-0.01f, 0.01f) * s;
            float ribY = 0.03f * s + rng.RandfRange(0, 0.03f) * s;
            float ribThick = rng.RandfRange(0.008f, 0.012f) * s;

            // Create curved rib with 3-4 segments
            int segments = 3 + (i % 2);
            float segmentLen = 0.04f * s;
            float curveAngle = 0.2f + (i % 3) * 0.1f;

            Vector3 segStart = new Vector3(ribX, ribY, ribBaseZ);
            for (int seg = 0; seg < segments; seg++)
            {
                float segAngle = -Mathf.Pi / 4f + seg * curveAngle;
                float nextX = segStart.X + Mathf.Sin(segAngle) * segmentLen * 0.3f;
                float nextY = segStart.Y + Mathf.Cos(segAngle) * segmentLen * 0.5f;
                float nextZ = segStart.Z + segmentLen * 0.4f;

                // Rib segment
                AddBoxToSurfaceTool(surfaceTool, segStart,
                    new Vector3(ribThick * 2f, segmentLen * 0.6f, ribThick * 2f));

                segStart = new Vector3(nextX, nextY, nextZ);
            }
        }

        // === VERTEBRAE CHAIN (small cylinders with disc shapes) ===
        int numVertebrae = rng.RandiRange(4, 7);
        float vertX = rng.RandfRange(-0.15f, 0.15f) * s;
        float vertZ = rng.RandfRange(-0.1f, 0.1f) * s;
        for (int i = 0; i < numVertebrae; i++)
        {
            float vertY = 0.015f * s + i * 0.022f * s;
            float vertRadius = 0.018f * s - i * 0.001f * s;

            // Vertebral body (disc-like)
            AddBoxToSurfaceTool(surfaceTool, new Vector3(vertX, vertY, vertZ),
                new Vector3(vertRadius * 2f, 0.012f * s, vertRadius * 2f));

            // Spinous process (small projection)
            AddBoxToSurfaceTool(surfaceTool, new Vector3(vertX, vertY + 0.01f * s, vertZ - vertRadius * 0.8f),
                new Vector3(0.005f * s, 0.015f * s, 0.01f * s));

            // Transverse processes (side projections)
            AddBoxToSurfaceTool(surfaceTool, new Vector3(vertX - vertRadius * 0.7f, vertY, vertZ),
                new Vector3(0.012f * s, 0.006f * s, 0.006f * s));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(vertX + vertRadius * 0.7f, vertY, vertZ),
                new Vector3(0.012f * s, 0.006f * s, 0.006f * s));
        }

        // === PELVIS SUGGESTION (curved box arrangement) ===
        float pelvisX = rng.RandfRange(-0.2f, 0.2f) * s;
        float pelvisZ = rng.RandfRange(0.1f, 0.25f) * s;
        float pelvisY = 0.04f * s;

        // Sacrum (central triangular piece)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(pelvisX, pelvisY, pelvisZ),
            new Vector3(0.06f * s, 0.03f * s, 0.04f * s));

        // Iliac crests (wing-like projections)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(pelvisX - 0.06f * s, pelvisY + 0.015f * s, pelvisZ + 0.01f * s),
            new Vector3(0.05f * s, 0.025f * s, 0.03f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(pelvisX + 0.06f * s, pelvisY + 0.015f * s, pelvisZ + 0.01f * s),
            new Vector3(0.05f * s, 0.025f * s, 0.03f * s));

        // Ischium (lower curves)
        AddSphereToSurfaceTool(surfaceTool, new Vector3(pelvisX - 0.04f * s, pelvisY - 0.01f * s, pelvisZ + 0.02f * s), 0.02f * s);
        AddSphereToSurfaceTool(surfaceTool, new Vector3(pelvisX + 0.04f * s, pelvisY - 0.01f * s, pelvisZ + 0.02f * s), 0.02f * s);

        // === SMALL BONE FRAGMENTS (scattered around) ===
        int fragments = rng.RandiRange(6, 10);
        for (int i = 0; i < fragments; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.15f, 0.4f) * s;
            float fragX = Mathf.Cos(angle) * dist;
            float fragZ = Mathf.Sin(angle) * dist;
            float fragLen = rng.RandfRange(0.02f, 0.06f) * s;
            float fragRad = rng.RandfRange(0.005f, 0.012f) * s;

            AddBoxToSurfaceTool(surfaceTool, new Vector3(fragX, fragRad, fragZ),
                new Vector3(fragLen, fragRad * 2f, fragRad * 2f));

            // Some fragments have knobby ends
            if (rng.Randf() > 0.5f)
            {
                AddSphereToSurfaceTool(surfaceTool, new Vector3(fragX + fragLen / 2f, fragRad, fragZ), fragRad * 1.3f);
            }
        }

        // === ADDITIONAL SCATTERED BONES FOR DENSITY ===
        // Finger/toe bones (phalanges)
        int numPhalanges = rng.RandiRange(5, 10);
        for (int i = 0; i < numPhalanges; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.08f, 0.35f) * s;
            float phalX = Mathf.Cos(angle) * dist;
            float phalZ = Mathf.Sin(angle) * dist;
            float phalLen = rng.RandfRange(0.015f, 0.025f) * s;
            float phalRad = 0.004f * s;

            // Small bone cylinder
            AddBoxToSurfaceTool(surfaceTool, new Vector3(phalX, phalRad, phalZ),
                new Vector3(phalLen, phalRad * 2f, phalRad * 2f));

            // Tiny knob at end
            AddSphereToSurfaceTool(surfaceTool, new Vector3(phalX + phalLen / 2f, phalRad, phalZ), phalRad * 1.2f);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        // Bone material with aged yellowing
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.88f, 0.82f, 0.68f), // Aged bone yellow
            Roughness = 0.9f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateCoiledChainsMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // ENHANCED: Rounded oval chain links in a coiled pile
        int links = 14;
        for (int i = 0; i < links; i++)
        {
            float angle = i * 0.45f + rng.Randf() * 0.15f;
            float radius = 0.12f * s + i * 0.012f * s;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = 0.015f * s + (i % 3) * 0.015f * s;
            float linkAngle = angle + Mathf.Pi / 2f + rng.Randf() * 0.3f;

            // Each link is an oval shape - create with two spheres and a connecting box
            float linkLen = 0.045f * s;
            float linkWidth = 0.025f * s;
            float linkThickness = 0.012f * s;

            // Link body (elongated oval box)
            Vector3 linkPos = new Vector3(x, y, z);
            float cosA = Mathf.Cos(linkAngle);
            float sinA = Mathf.Sin(linkAngle);

            // Main link body - oval shaped
            AddBoxToSurfaceTool(surfaceTool, linkPos, new Vector3(linkLen, linkThickness, linkWidth));

            // Rounded ends using spheres
            Vector3 end1 = linkPos + new Vector3(cosA * linkLen * 0.4f, 0, sinA * linkLen * 0.4f);
            Vector3 end2 = linkPos - new Vector3(cosA * linkLen * 0.4f, 0, sinA * linkLen * 0.4f);
            AddSphereToSurfaceTool(surfaceTool, end1, linkThickness * 0.8f);
            AddSphereToSurfaceTool(surfaceTool, end2, linkThickness * 0.8f);
        }

        // END HOOKS - curved hooks at the chain ends
        // Hook 1 at start of chain
        float hookX1 = Mathf.Cos(0.2f) * 0.1f * s;
        float hookZ1 = Mathf.Sin(0.2f) * 0.1f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(hookX1, 0.025f * s, hookZ1), new Vector3(0.04f * s, 0.015f * s, 0.015f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(hookX1 - 0.025f * s, 0.015f * s, hookZ1), new Vector3(0.015f * s, 0.03f * s, 0.015f * s));
        AddSphereToSurfaceTool(surfaceTool, new Vector3(hookX1 - 0.025f * s, 0.005f * s, hookZ1), 0.01f * s);

        // Hook 2 at end of chain
        float hookX2 = Mathf.Cos(links * 0.45f) * (0.12f * s + links * 0.012f * s);
        float hookZ2 = Mathf.Sin(links * 0.45f) * (0.12f * s + links * 0.012f * s);
        AddBoxToSurfaceTool(surfaceTool, new Vector3(hookX2, 0.04f * s, hookZ2), new Vector3(0.04f * s, 0.015f * s, 0.015f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(hookX2 + 0.025f * s, 0.03f * s, hookZ2), new Vector3(0.015f * s, 0.035f * s, 0.015f * s));
        AddSphereToSurfaceTool(surfaceTool, new Vector3(hookX2 + 0.025f * s, 0.015f * s, hookZ2), 0.012f * s);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        // Base rusted iron material
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.38f, 0.34f, 0.3f), // Rusted iron base
            Roughness = 0.85f,
            Metallic = 0.55f
        };
        mesh.SurfaceSetMaterial(0, material);

        // Note: Rust color patches would require a second surface/material
        // For simplicity, the base color already suggests rust

        return mesh;
    }

    private ArrayMesh CreateMossPatchMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Irregular moss patch as flat displaced plane
        int segments = 8;
        Vector3 center = Vector3.Zero;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;
            float r1 = (0.3f + rng.Randf() * 0.2f) * s;
            float r2 = (0.3f + rng.Randf() * 0.2f) * s;
            Vector3 v1 = new Vector3(Mathf.Cos(angle1) * r1, 0, Mathf.Sin(angle1) * r1);
            Vector3 v2 = new Vector3(Mathf.Cos(angle2) * r2, 0, Mathf.Sin(angle2) * r2);
            surfaceTool.AddVertex(center);
            surfaceTool.AddVertex(v1);
            surfaceTool.AddVertex(v2);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.45f, 0.15f), // Lush green
            Roughness = 1f,
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.2f, 0.05f),
            EmissionEnergyMultiplier = 0.3f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateWaterPuddleMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Irregular puddle shape
        int segments = 10;
        Vector3 center = Vector3.Zero;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;
            float r1 = (0.25f + rng.Randf() * 0.15f) * s;
            float r2 = (0.25f + rng.Randf() * 0.15f) * s;
            Vector3 v1 = new Vector3(Mathf.Cos(angle1) * r1, 0, Mathf.Sin(angle1) * r1);
            Vector3 v2 = new Vector3(Mathf.Cos(angle2) * r2, 0, Mathf.Sin(angle2) * r2);
            surfaceTool.AddVertex(center);
            surfaceTool.AddVertex(v1);
            surfaceTool.AddVertex(v2);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.3f, 0.4f, 0.7f),
            Roughness = 0.1f,
            Metallic = 0.3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateTreasureChestMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Main chest body with wood grain
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.2f * s, 0), new Vector3(0.7f * s, 0.4f * s, 0.45f * s));

        // Lid (slightly arched and open)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.45f * s, -0.1f * s), new Vector3(0.72f * s, 0.12f * s, 0.47f * s));

        // Decorative metal bands (3 horizontal, 2 vertical)
        float bandThickness = 0.02f * s;
        for (int i = 0; i < 3; i++)
        {
            float y = 0.05f * s + i * 0.15f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, y, 0.235f * s),
                new Vector3(0.72f * s, 0.04f * s, bandThickness));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, y, -0.235f * s),
                new Vector3(0.72f * s, 0.04f * s, bandThickness));
        }

        // Vertical corner bands
        for (int i = -1; i <= 1; i += 2)
        {
            AddBoxToSurfaceTool(surfaceTool, new Vector3(i * 0.33f * s, 0.2f * s, 0),
                new Vector3(bandThickness, 0.4f * s, 0.45f * s));
        }

        // Ornate lock (center front)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.2f * s, 0.24f * s),
            new Vector3(0.12f * s, 0.15f * s, 0.03f * s));
        // Keyhole
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.2f * s, 0.27f * s),
            new Vector3(0.03f * s, 0.05f * s, 0.01f * s));

        // Gold coins inside (multiple layers for richness)
        int numCoins = 15;
        for (int i = 0; i < numCoins; i++)
        {
            float x = (rng.Randf() - 0.5f) * 0.35f * s;
            float z = (rng.Randf() - 0.5f) * 0.15f * s;
            float coinY = 0.32f * s + rng.Randf() * 0.08f * s;

            // Coin (flattened sphere)
            Vector3 coinPos = new Vector3(x, coinY, z);
            float coinRadius = 0.035f * s;

            // Create coin as thin cylinder
            for (int seg = 0; seg < 8; seg++)
            {
                float angle1 = seg * Mathf.Tau / 8f;
                float angle2 = (seg + 1) * Mathf.Tau / 8f;

                Vector3 p1 = coinPos + new Vector3(Mathf.Cos(angle1) * coinRadius, 0, Mathf.Sin(angle1) * coinRadius);
                Vector3 p2 = coinPos + new Vector3(Mathf.Cos(angle2) * coinRadius, 0, Mathf.Sin(angle2) * coinRadius);
                Vector3 p1Top = p1 + new Vector3(0, 0.01f * s, 0);
                Vector3 p2Top = p2 + new Vector3(0, 0.01f * s, 0);

                surfaceTool.SetNormal(Vector3.Up);
                surfaceTool.AddVertex(coinPos);
                surfaceTool.AddVertex(p1Top);
                surfaceTool.AddVertex(p2Top);
            }
        }

        // Gemstones (3-5 gems scattered among coins)
        int numGems = 3 + (int)(rng.Randf() * 3);
        Color[] gemColors = new Color[]
        {
            new Color(1f, 0.2f, 0.2f), // Ruby
            new Color(0.2f, 0.5f, 1f), // Sapphire
            new Color(0.3f, 1f, 0.3f), // Emerald
            new Color(0.8f, 0.2f, 1f), // Amethyst
        };

        for (int i = 0; i < numGems; i++)
        {
            float x = (rng.Randf() - 0.5f) * 0.3f * s;
            float z = (rng.Randf() - 0.5f) * 0.1f * s;
            Vector3 gemPos = new Vector3(x, 0.38f * s, z);

            // Small gem (tiny double pyramid)
            float gemSize = 0.03f * s;
            Vector3 gemTop = gemPos + new Vector3(0, gemSize * 0.8f, 0);
            Vector3 gemBottom = gemPos - new Vector3(0, gemSize * 0.5f, 0);

            for (int j = 0; j < 6; j++)
            {
                float a1 = j * Mathf.Tau / 6f;
                float a2 = (j + 1) * Mathf.Tau / 6f;

                Vector3 edge1 = gemPos + new Vector3(Mathf.Cos(a1) * gemSize * 0.5f, 0, Mathf.Sin(a1) * gemSize * 0.5f);
                Vector3 edge2 = gemPos + new Vector3(Mathf.Cos(a2) * gemSize * 0.5f, 0, Mathf.Sin(a2) * gemSize * 0.5f);

                // Top pyramid
                surfaceTool.AddVertex(gemTop);
                surfaceTool.AddVertex(edge1);
                surfaceTool.AddVertex(edge2);

                // Bottom pyramid
                surfaceTool.AddVertex(gemBottom);
                surfaceTool.AddVertex(edge2);
                surfaceTool.AddVertex(edge1);
            }
        }

        // Lid hinges (back)
        for (int i = -1; i <= 1; i += 2)
        {
            AddBoxToSurfaceTool(surfaceTool, new Vector3(i * 0.25f * s, 0.42f * s, -0.24f * s),
                new Vector3(0.08f * s, 0.04f * s, 0.05f * s));
        }

        // Skip GenerateTangents() - not needed without normal maps, saves ~2-5ms per prop
        var mesh = surfaceTool.Commit();

        // Enhanced wood material with metallic accent
        var material = ProceduralMesh3D.CreateWoodMaterial(PrimaryColor.Darkened(0.15f));
        material.Roughness = 0.7f;
        mesh.SurfaceSetMaterial(0, material);

        return mesh;
    }

    private ArrayMesh CreateBrokenBarrelMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Barrel on its side, broken
        int segments = 12; // More segments for smoother barrel
        float radius = 0.35f * s;
        float height = 0.55f * s;
        float barrelY = radius * 0.3f; // Slight offset so it rests on floor

        // === MAIN BARREL BODY - partial staves ===
        // Create partial barrel (broken, with missing staves)
        int[] missingStaves = { 2, 3, 10 }; // Staves that are broken off
        for (int i = 0; i < segments; i++)
        {
            // Check if this stave is missing
            bool isMissing = false;
            foreach (int m in missingStaves)
            {
                if (i == m) { isMissing = true; break; }
            }
            if (isMissing) continue;

            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;

            float y1 = Mathf.Cos(angle1) * radius + barrelY;
            float z1 = Mathf.Sin(angle1) * radius;
            float y2 = Mathf.Cos(angle2) * radius + barrelY;
            float z2 = Mathf.Sin(angle2) * radius;

            // Stave width variation (individual plank look)
            float staveInset = 0.005f * s;
            float innerY1 = Mathf.Cos(angle1) * (radius - staveInset) + barrelY;
            float innerZ1 = Mathf.Sin(angle1) * (radius - staveInset);
            float innerY2 = Mathf.Cos(angle2) * (radius - staveInset) + barrelY;
            float innerZ2 = Mathf.Sin(angle2) * (radius - staveInset);

            // Outer surface of stave
            surfaceTool.AddVertex(new Vector3(-height / 2, y1, z1));
            surfaceTool.AddVertex(new Vector3(height / 2, y1, z1));
            surfaceTool.AddVertex(new Vector3(height / 2, y2, z2));

            surfaceTool.AddVertex(new Vector3(-height / 2, y1, z1));
            surfaceTool.AddVertex(new Vector3(height / 2, y2, z2));
            surfaceTool.AddVertex(new Vector3(-height / 2, y2, z2));

            // Inner surface visible (gives thickness to staves)
            surfaceTool.AddVertex(new Vector3(-height / 2 + 0.02f * s, innerY1, innerZ1));
            surfaceTool.AddVertex(new Vector3(height / 2 - 0.02f * s, innerY2, innerZ2));
            surfaceTool.AddVertex(new Vector3(height / 2 - 0.02f * s, innerY1, innerZ1));

            surfaceTool.AddVertex(new Vector3(-height / 2 + 0.02f * s, innerY1, innerZ1));
            surfaceTool.AddVertex(new Vector3(-height / 2 + 0.02f * s, innerY2, innerZ2));
            surfaceTool.AddVertex(new Vector3(height / 2 - 0.02f * s, innerY2, innerZ2));
        }

        // === SPLINTERED STAVE ENDS - jagged broken edges ===
        for (int i = 0; i < 3; i++)
        {
            float splinterAngle = missingStaves[i % 3] * Mathf.Tau / segments + rng.RandfRange(-0.1f, 0.1f);
            float splinterY = Mathf.Cos(splinterAngle) * radius + barrelY;
            float splinterZ = Mathf.Sin(splinterAngle) * radius;

            // Jagged splinter pointing outward
            for (int j = 0; j < 3; j++)
            {
                float splinterX = -height / 2 + rng.RandfRange(0.1f, 0.4f) * height;
                float splinterLen = rng.RandfRange(0.04f, 0.1f) * s;
                float splinterWidth = rng.RandfRange(0.01f, 0.025f) * s;
                float outDir = rng.RandfRange(0.3f, 0.8f);

                // Splinter as elongated triangle pointing outward
                surfaceTool.AddVertex(new Vector3(splinterX - splinterWidth, splinterY, splinterZ));
                surfaceTool.AddVertex(new Vector3(splinterX + splinterWidth, splinterY, splinterZ));
                surfaceTool.AddVertex(new Vector3(splinterX, splinterY + splinterLen * outDir, splinterZ + splinterLen * (1 - outDir)));
            }
        }

        // === METAL BANDS - two bands with damage ===
        float bandWidth = 0.04f * s;
        float bandThickness = 0.015f * s;
        float[] bandPositions = { -height * 0.35f, height * 0.35f };

        foreach (float bandX in bandPositions)
        {
            // Band segments (wrapping around barrel)
            int bandSegs = 10;
            for (int i = 0; i < bandSegs; i++)
            {
                float angle1 = i * Mathf.Tau / bandSegs;
                float angle2 = (i + 1) * Mathf.Tau / bandSegs;

                // Skip segment for damage
                if (i == 2 && bandX > 0) continue; // Broken band on one side

                float y1 = Mathf.Cos(angle1) * (radius + bandThickness) + barrelY;
                float z1 = Mathf.Sin(angle1) * (radius + bandThickness);
                float y2 = Mathf.Cos(angle2) * (radius + bandThickness) + barrelY;
                float z2 = Mathf.Sin(angle2) * (radius + bandThickness);

                // Band segment
                surfaceTool.AddVertex(new Vector3(bandX - bandWidth / 2, y1, z1));
                surfaceTool.AddVertex(new Vector3(bandX + bandWidth / 2, y1, z1));
                surfaceTool.AddVertex(new Vector3(bandX + bandWidth / 2, y2, z2));

                surfaceTool.AddVertex(new Vector3(bandX - bandWidth / 2, y1, z1));
                surfaceTool.AddVertex(new Vector3(bandX + bandWidth / 2, y2, z2));
                surfaceTool.AddVertex(new Vector3(bandX - bandWidth / 2, y2, z2));
            }

            // Loose/bent band end (hanging down)
            if (bandX > 0)
            {
                float bentAngle = 2 * Mathf.Tau / bandSegs;
                float bentY = Mathf.Cos(bentAngle) * (radius + bandThickness) + barrelY - 0.05f * s;
                float bentZ = Mathf.Sin(bentAngle) * (radius + bandThickness) + 0.08f * s;
                surfaceTool.AddVertex(new Vector3(bandX - bandWidth / 2, bentY, bentZ));
                surfaceTool.AddVertex(new Vector3(bandX + bandWidth / 2, bentY, bentZ));
                surfaceTool.AddVertex(new Vector3(bandX, bentY - 0.06f * s, bentZ + 0.03f * s));
            }
        }

        // === METAL BAND RIVETS/NAILS ===
        int rivetCount = 6;
        for (int i = 0; i < rivetCount; i++)
        {
            float rivetAngle = i * Mathf.Tau / rivetCount;
            // Skip rivets on missing staves
            if (i == 1 || i == 2) continue;

            float rivetY = Mathf.Cos(rivetAngle) * (radius + bandThickness + 0.005f * s) + barrelY;
            float rivetZ = Mathf.Sin(rivetAngle) * (radius + bandThickness + 0.005f * s);

            // Rivet on each band
            foreach (float bandX in bandPositions)
            {
                float rivetR = 0.012f * s;
                for (int j = 0; j < 4; j++)
                {
                    float ra1 = j * Mathf.Tau / 4;
                    float ra2 = (j + 1) * Mathf.Tau / 4;
                    surfaceTool.AddVertex(new Vector3(bandX, rivetY, rivetZ));
                    surfaceTool.AddVertex(new Vector3(bandX + rivetR * 0.5f, rivetY + Mathf.Cos(ra1) * rivetR, rivetZ + Mathf.Sin(ra1) * rivetR));
                    surfaceTool.AddVertex(new Vector3(bandX + rivetR * 0.5f, rivetY + Mathf.Cos(ra2) * rivetR, rivetZ + Mathf.Sin(ra2) * rivetR));
                }
            }
        }

        // === SEPARATED STAVES - fallen pieces nearby ===
        int looseStaveCount = rng.RandiRange(1, 2);
        for (int i = 0; i < looseStaveCount; i++)
        {
            float staveX = rng.RandfRange(0.3f, 0.5f) * s * (rng.Randf() > 0.5f ? 1 : -1);
            float staveZ = rng.RandfRange(0.25f, 0.4f) * s * (rng.Randf() > 0.5f ? 1 : -1);
            float staveLen = rng.RandfRange(0.35f, 0.5f) * height;
            float staveWidth = 0.06f * s;
            float staveAngle = rng.Randf() * Mathf.Tau;

            // Curved stave lying on ground
            float dx = Mathf.Cos(staveAngle) * staveLen / 2;
            float dz = Mathf.Sin(staveAngle) * staveLen / 2;

            // Simple flat representation of curved stave
            surfaceTool.AddVertex(new Vector3(staveX - dx, 0.02f * s, staveZ - dz - staveWidth / 2));
            surfaceTool.AddVertex(new Vector3(staveX + dx, 0.02f * s, staveZ + dz - staveWidth / 2));
            surfaceTool.AddVertex(new Vector3(staveX + dx, 0.025f * s, staveZ + dz + staveWidth / 2));

            surfaceTool.AddVertex(new Vector3(staveX - dx, 0.02f * s, staveZ - dz - staveWidth / 2));
            surfaceTool.AddVertex(new Vector3(staveX + dx, 0.025f * s, staveZ + dz + staveWidth / 2));
            surfaceTool.AddVertex(new Vector3(staveX - dx, 0.025f * s, staveZ - dz + staveWidth / 2));
        }

        // === SPILLED LIQUID POOL ===
        int poolSegs = 8;
        float poolCenterX = height * 0.3f;
        for (int i = 0; i < poolSegs; i++)
        {
            float angle1 = i * Mathf.Tau / poolSegs;
            float angle2 = (i + 1) * Mathf.Tau / poolSegs;
            float r1 = (0.22f + rng.Randf() * 0.12f) * s;
            float r2 = (0.22f + rng.Randf() * 0.12f) * s;
            surfaceTool.AddVertex(new Vector3(poolCenterX, 0.008f, 0));
            surfaceTool.AddVertex(new Vector3(poolCenterX + Mathf.Cos(angle1) * r1, 0.005f, Mathf.Sin(angle1) * r1));
            surfaceTool.AddVertex(new Vector3(poolCenterX + Mathf.Cos(angle2) * r2, 0.005f, Mathf.Sin(angle2) * r2));
        }

        // Liquid trail from barrel opening
        float trailLen = 0.15f * s;
        surfaceTool.AddVertex(new Vector3(height / 2, 0.01f, -0.04f * s));
        surfaceTool.AddVertex(new Vector3(height / 2, 0.01f, 0.04f * s));
        surfaceTool.AddVertex(new Vector3(height / 2 + trailLen, 0.005f, 0));

        // === WOOD GRAIN DETAIL on staves ===
        int grainCount = rng.RandiRange(4, 6);
        for (int g = 0; g < grainCount; g++)
        {
            float grainAngle = rng.Randf() * Mathf.Tau;
            // Avoid missing staves
            bool onMissing = false;
            foreach (int m in missingStaves)
            {
                float mAngle = m * Mathf.Tau / segments;
                if (Mathf.Abs(grainAngle - mAngle) < 0.3f) { onMissing = true; break; }
            }
            if (onMissing) continue;

            float grainY = Mathf.Cos(grainAngle) * (radius + 0.005f * s) + barrelY;
            float grainZ = Mathf.Sin(grainAngle) * (radius + 0.005f * s);
            float grainX = rng.RandfRange(-height * 0.4f, height * 0.4f);
            float grainLen = rng.RandfRange(0.06f, 0.12f) * s;

            // Grain line as thin box
            AddBoxToSurfaceTool(surfaceTool, new Vector3(grainX, grainY, grainZ), new Vector3(grainLen, 0.003f * s, 0.006f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateWoodMaterial(new Color(0.45f, 0.32f, 0.18f));
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateAltarStoneMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        float altarTop = 0.5f * s;
        float altarHeight = 0.5f * s;

        // === MAIN ALTAR SLAB with beveled edges ===
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.25f * s, 0), new Vector3(1.2f * s, altarHeight, 0.7f * s));

        // Beveled edge detail (chamfered corners)
        float bevelSize = 0.04f * s;
        // Front bevel
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, altarTop - 0.01f * s, 0.35f * s), new Vector3(1.1f * s, 0.02f * s, bevelSize));
        // Back bevel
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, altarTop - 0.01f * s, -0.35f * s), new Vector3(1.1f * s, 0.02f * s, bevelSize));
        // Side bevels
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.6f * s, altarTop - 0.01f * s, 0), new Vector3(bevelSize, 0.02f * s, 0.65f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.6f * s, altarTop - 0.01f * s, 0), new Vector3(bevelSize, 0.02f * s, 0.65f * s));

        // === TIERED BASE PEDESTAL ===
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.03f * s, 0), new Vector3(1.35f * s, 0.06f * s, 0.85f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.08f * s, 0), new Vector3(1.28f * s, 0.04f * s, 0.78f * s));

        // === CARVED RUNE PATTERNS - complex magical symbols ===
        float runeY = altarTop + 0.005f * s;

        // Outer ritual circle
        int circleSegs = 16;
        float outerR = 0.38f * s;
        for (int i = 0; i < circleSegs; i++)
        {
            float angle = i * Mathf.Tau / circleSegs;
            float x = Mathf.Cos(angle) * outerR;
            float z = Mathf.Sin(angle) * outerR;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, runeY, z), new Vector3(0.05f * s, 0.008f * s, 0.018f * s));
        }

        // Inner circle
        float innerR = 0.25f * s;
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12;
            float x = Mathf.Cos(angle) * innerR;
            float z = Mathf.Sin(angle) * innerR;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, runeY, z), new Vector3(0.04f * s, 0.008f * s, 0.015f * s));
        }

        // Pentagram-like star pattern
        for (int i = 0; i < 5; i++)
        {
            float angle1 = i * Mathf.Tau / 5;
            float angle2 = ((i + 2) % 5) * Mathf.Tau / 5;
            float x1 = Mathf.Cos(angle1) * outerR * 0.85f;
            float z1 = Mathf.Sin(angle1) * outerR * 0.85f;
            float x2 = Mathf.Cos(angle2) * outerR * 0.85f;
            float z2 = Mathf.Sin(angle2) * outerR * 0.85f;

            // Line from point to point
            float midX = (x1 + x2) / 2;
            float midZ = (z1 + z2) / 2;
            float lineLen = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (z2 - z1) * (z2 - z1));
            float lineAngle = Mathf.Atan2(z2 - z1, x2 - x1);

            // Create line segment as rotated box
            float dx = Mathf.Cos(lineAngle);
            float dz = Mathf.Sin(lineAngle);
            AddBoxToSurfaceTool(surfaceTool, new Vector3(midX, runeY + 0.001f * s, midZ), new Vector3(lineLen, 0.006f * s, 0.012f * s));
        }

        // Diagonal rune lines
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, runeY, 0), new Vector3(0.95f * s, 0.008f * s, 0.02f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, runeY, 0), new Vector3(0.02f * s, 0.008f * s, 0.58f * s));

        // Small rune glyphs at cardinal points
        float[] glyphX = { 0.4f * s, -0.4f * s, 0, 0 };
        float[] glyphZ = { 0, 0, 0.22f * s, -0.22f * s };
        for (int g = 0; g < 4; g++)
        {
            // Triangle rune
            float gx = glyphX[g];
            float gz = glyphZ[g];
            float gr = 0.025f * s;
            surfaceTool.AddVertex(new Vector3(gx, runeY + 0.002f * s, gz - gr));
            surfaceTool.AddVertex(new Vector3(gx + gr, runeY + 0.002f * s, gz + gr));
            surfaceTool.AddVertex(new Vector3(gx - gr, runeY + 0.002f * s, gz + gr));
        }

        // === BLOOD CHANNELS - grooves for ritual drainage ===
        float channelY = altarTop + 0.003f * s;
        float channelWidth = 0.018f * s;

        // Main channel running down center length
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, channelY, 0.1f * s), new Vector3(channelWidth, 0.01f * s, 0.45f * s));

        // Branch channels from center
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.15f * s, channelY, 0.2f * s), new Vector3(0.25f * s, 0.01f * s, channelWidth));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.18f * s, channelY, 0.15f * s), new Vector3(0.3f * s, 0.01f * s, channelWidth));

        // Channel leading to edge (drainage spout)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.45f * s, channelY, 0.2f * s), new Vector3(0.18f * s, 0.01f * s, channelWidth));
        // Spout notch at edge
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.58f * s, altarTop - 0.02f * s, 0.2f * s), new Vector3(0.04f * s, 0.04f * s, 0.03f * s));

        // === CANDLE HOLDERS with detail ===
        float[] cornerX = { -0.5f * s, 0.5f * s, -0.5f * s, 0.5f * s };
        float[] cornerZ = { -0.28f * s, -0.28f * s, 0.28f * s, 0.28f * s };
        for (int i = 0; i < 4; i++)
        {
            float cx = cornerX[i];
            float cz = cornerZ[i];

            // Holder base (wider)
            AddBoxToSurfaceTool(surfaceTool, new Vector3(cx, altarTop + 0.015f * s, cz), new Vector3(0.07f * s, 0.03f * s, 0.07f * s));

            // Holder lip (ring)
            for (int r = 0; r < 6; r++)
            {
                float ringAngle = r * Mathf.Tau / 6;
                float ringX = Mathf.Cos(ringAngle) * 0.032f * s;
                float ringZ = Mathf.Sin(ringAngle) * 0.032f * s;
                AddBoxToSurfaceTool(surfaceTool, new Vector3(cx + ringX, altarTop + 0.035f * s, cz + ringZ), new Vector3(0.012f * s, 0.01f * s, 0.012f * s));
            }

            // Candle (tapered cylinder approximation)
            float candleH = rng.RandfRange(0.08f, 0.12f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(cx, altarTop + 0.04f * s + candleH / 2, cz), new Vector3(0.022f * s, candleH, 0.022f * s));

            // Wax drips
            int dripCount = rng.RandiRange(1, 3);
            for (int d = 0; d < dripCount; d++)
            {
                float dripAngle = rng.Randf() * Mathf.Tau;
                float dripDist = 0.015f * s;
                float dripX = cx + Mathf.Cos(dripAngle) * dripDist;
                float dripZ = cz + Mathf.Sin(dripAngle) * dripDist;
                float dripLen = rng.RandfRange(0.02f, 0.05f) * s;
                AddBoxToSurfaceTool(surfaceTool, new Vector3(dripX, altarTop + 0.05f * s + dripLen / 2, dripZ), new Vector3(0.008f * s, dripLen, 0.008f * s));
            }
        }

        // === BLOOD STAINS - more organic shapes ===
        int stainCount = rng.RandiRange(3, 5);
        for (int st = 0; st < stainCount; st++)
        {
            float stainX = rng.RandfRange(-0.35f, 0.35f) * s;
            float stainZ = rng.RandfRange(-0.2f, 0.25f) * s;
            float stainR = rng.RandfRange(0.04f, 0.1f) * s;

            // Irregular stain shape
            int stainSegs = 6;
            for (int ss = 0; ss < stainSegs; ss++)
            {
                float sAngle1 = ss * Mathf.Tau / stainSegs;
                float sAngle2 = (ss + 1) * Mathf.Tau / stainSegs;
                float sr1 = stainR * (0.7f + rng.Randf() * 0.5f);
                float sr2 = stainR * (0.7f + rng.Randf() * 0.5f);

                surfaceTool.AddVertex(new Vector3(stainX, runeY + 0.003f * s, stainZ));
                surfaceTool.AddVertex(new Vector3(stainX + Mathf.Cos(sAngle1) * sr1, runeY + 0.002f * s, stainZ + Mathf.Sin(sAngle1) * sr1));
                surfaceTool.AddVertex(new Vector3(stainX + Mathf.Cos(sAngle2) * sr2, runeY + 0.002f * s, stainZ + Mathf.Sin(sAngle2) * sr2));
            }
        }

        // === OFFERING BOWL with blood inside ===
        float bowlX = 0.05f * s;
        float bowlZ = -0.05f * s;
        float bowlR = 0.09f * s;
        float bowlH = 0.04f * s;

        // Bowl outer rim
        for (int b = 0; b < 8; b++)
        {
            float bAngle1 = b * Mathf.Tau / 8;
            float bAngle2 = (b + 1) * Mathf.Tau / 8;
            // Outer wall
            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle1) * bowlR, altarTop + 0.005f * s, bowlZ + Mathf.Sin(bAngle1) * bowlR));
            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle1) * bowlR, altarTop + bowlH, bowlZ + Mathf.Sin(bAngle1) * bowlR));
            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle2) * bowlR, altarTop + bowlH, bowlZ + Mathf.Sin(bAngle2) * bowlR));

            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle1) * bowlR, altarTop + 0.005f * s, bowlZ + Mathf.Sin(bAngle1) * bowlR));
            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle2) * bowlR, altarTop + bowlH, bowlZ + Mathf.Sin(bAngle2) * bowlR));
            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle2) * bowlR, altarTop + 0.005f * s, bowlZ + Mathf.Sin(bAngle2) * bowlR));
        }

        // Blood filling bowl (flat disc at top)
        float bloodInnerR = bowlR * 0.85f;
        for (int b = 0; b < 8; b++)
        {
            float bAngle1 = b * Mathf.Tau / 8;
            float bAngle2 = (b + 1) * Mathf.Tau / 8;
            surfaceTool.AddVertex(new Vector3(bowlX, altarTop + bowlH - 0.01f * s, bowlZ));
            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle1) * bloodInnerR, altarTop + bowlH - 0.01f * s, bowlZ + Mathf.Sin(bAngle1) * bloodInnerR));
            surfaceTool.AddVertex(new Vector3(bowlX + Mathf.Cos(bAngle2) * bloodInnerR, altarTop + bowlH - 0.01f * s, bowlZ + Mathf.Sin(bAngle2) * bloodInnerR));
        }

        // === RITUAL DAGGER (optional) ===
        if (rng.Randf() > 0.3f)
        {
            float daggerX = -0.25f * s;
            float daggerZ = 0.1f * s;
            float daggerAngle = rng.RandfRange(-0.3f, 0.3f);

            // Blade
            float bladeLen = 0.12f * s;
            float dx = Mathf.Cos(daggerAngle) * bladeLen / 2;
            float dz = Mathf.Sin(daggerAngle) * bladeLen / 2;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(daggerX, altarTop + 0.01f * s, daggerZ), new Vector3(bladeLen, 0.008f * s, 0.02f * s));

            // Handle
            float handleLen = 0.06f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(daggerX - dx - Mathf.Cos(daggerAngle) * handleLen / 2, altarTop + 0.012f * s, daggerZ - dz - Mathf.Sin(daggerAngle) * handleLen / 2), new Vector3(handleLen, 0.015f * s, 0.025f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.26f, 0.24f), // Darker aged stone
            Roughness = 0.88f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateGlowingMushroomsMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Multiple mushrooms of varying sizes
        int count = rng.RandiRange(4, 7);
        for (int i = 0; i < count; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.2f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float stemH = rng.RandfRange(0.1f, 0.25f) * s;
            float capR = rng.RandfRange(0.06f, 0.12f) * s;

            // Stem
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, stemH / 2, z), new Vector3(0.03f * s, stemH, 0.03f * s));
            // Cap (sphere on top)
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, stemH + capR * 0.5f, z), capR);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.8f, 0.7f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.9f, 0.6f),
            EmissionEnergyMultiplier = 1.5f,
            Roughness = 0.5f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateBloodPoolMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // === MAIN IRREGULAR BLOOD POOL with darker center gradient ===
        int segments = 12; // More segments for smoother pool
        Vector3 center = Vector3.Zero;

        // Outer pool ring (lighter, more translucent edges)
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;
            float r1 = (0.25f + rng.Randf() * 0.12f) * s;
            float r2 = (0.25f + rng.Randf() * 0.12f) * s;
            float midR1 = r1 * 0.5f;
            float midR2 = r2 * 0.5f;

            // Outer ring triangles (thinner blood at edges)
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * midR1, 0.001f, Mathf.Sin(angle1) * midR1));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * r1, 0, Mathf.Sin(angle1) * r1));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r2, 0, Mathf.Sin(angle2) * r2));

            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * midR1, 0.001f, Mathf.Sin(angle1) * midR1));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r2, 0, Mathf.Sin(angle2) * r2));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * midR2, 0.001f, Mathf.Sin(angle2) * midR2));

            // Inner pool (darker, thicker blood in center)
            surfaceTool.AddVertex(center + new Vector3(0, 0.003f, 0)); // Slightly raised center
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * midR1, 0.001f, Mathf.Sin(angle1) * midR1));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * midR2, 0.001f, Mathf.Sin(angle2) * midR2));
        }

        // === SPLATTER PATTERN - blood droplets radiating outward ===
        int splatCount = rng.RandiRange(5, 8);
        for (int i = 0; i < splatCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.28f, 0.45f) * s;
            float dropX = Mathf.Cos(angle) * dist;
            float dropZ = Mathf.Sin(angle) * dist;
            float dropSize = rng.RandfRange(0.02f, 0.05f) * s;

            // Main splatter drop (elongated toward center - directionality)
            int dropSegs = 6;
            for (int d = 0; d < dropSegs; d++)
            {
                float dAngle1 = d * Mathf.Tau / dropSegs;
                float dAngle2 = (d + 1) * Mathf.Tau / dropSegs;
                // Elongate toward center
                float elongX = -Mathf.Cos(angle) * 0.3f;
                float elongZ = -Mathf.Sin(angle) * 0.3f;
                surfaceTool.AddVertex(new Vector3(dropX, 0.001f, dropZ));
                surfaceTool.AddVertex(new Vector3(dropX + Mathf.Cos(dAngle1) * dropSize * (1 + elongX), 0, dropZ + Mathf.Sin(dAngle1) * dropSize * (1 + elongZ)));
                surfaceTool.AddVertex(new Vector3(dropX + Mathf.Cos(dAngle2) * dropSize * (1 + elongX), 0, dropZ + Mathf.Sin(dAngle2) * dropSize * (1 + elongZ)));
            }

            // Trailing droplets (smaller drops leading to main splatter)
            int trailCount = rng.RandiRange(1, 3);
            for (int t = 0; t < trailCount; t++)
            {
                float trailDist = dist - (t + 1) * 0.06f * s;
                float trailX = Mathf.Cos(angle) * trailDist + rng.RandfRange(-0.02f, 0.02f) * s;
                float trailZ = Mathf.Sin(angle) * trailDist + rng.RandfRange(-0.02f, 0.02f) * s;
                float trailSize = dropSize * 0.4f;

                for (int d = 0; d < 4; d++)
                {
                    float dAngle1 = d * Mathf.Tau / 4;
                    float dAngle2 = (d + 1) * Mathf.Tau / 4;
                    surfaceTool.AddVertex(new Vector3(trailX, 0.001f, trailZ));
                    surfaceTool.AddVertex(new Vector3(trailX + Mathf.Cos(dAngle1) * trailSize, 0, trailZ + Mathf.Sin(dAngle1) * trailSize));
                    surfaceTool.AddVertex(new Vector3(trailX + Mathf.Cos(dAngle2) * trailSize, 0, trailZ + Mathf.Sin(dAngle2) * trailSize));
                }
            }
        }

        // === VISCOSITY TEXTURE - slight ripples/wrinkles in coagulating blood ===
        int rippleCount = rng.RandiRange(3, 5);
        for (int i = 0; i < rippleCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.18f) * s;
            float rippleX = Mathf.Cos(angle) * dist;
            float rippleZ = Mathf.Sin(angle) * dist;
            float rippleLen = rng.RandfRange(0.04f, 0.08f) * s;
            float rippleWidth = 0.008f * s;
            float rippleAngle = rng.Randf() * Mathf.Tau;

            // Ripple as thin raised line
            float dx = Mathf.Cos(rippleAngle) * rippleLen;
            float dz = Mathf.Sin(rippleAngle) * rippleLen;
            float nx = -Mathf.Sin(rippleAngle) * rippleWidth;
            float nz = Mathf.Cos(rippleAngle) * rippleWidth;

            surfaceTool.AddVertex(new Vector3(rippleX - dx + nx, 0.004f, rippleZ - dz + nz));
            surfaceTool.AddVertex(new Vector3(rippleX + dx + nx, 0.004f, rippleZ + dz + nz));
            surfaceTool.AddVertex(new Vector3(rippleX + dx - nx, 0.004f, rippleZ + dz - nz));

            surfaceTool.AddVertex(new Vector3(rippleX - dx + nx, 0.004f, rippleZ - dz + nz));
            surfaceTool.AddVertex(new Vector3(rippleX + dx - nx, 0.004f, rippleZ + dz - nz));
            surfaceTool.AddVertex(new Vector3(rippleX - dx - nx, 0.004f, rippleZ - dz - nz));
        }

        // === DARKER CENTER - coagulated blood clot ===
        float clotRadius = 0.06f * s;
        for (int i = 0; i < 6; i++)
        {
            float angle1 = i * Mathf.Tau / 6;
            float angle2 = (i + 1) * Mathf.Tau / 6;
            float offsetX = rng.RandfRange(-0.03f, 0.03f) * s;
            float offsetZ = rng.RandfRange(-0.03f, 0.03f) * s;
            surfaceTool.AddVertex(new Vector3(offsetX, 0.005f, offsetZ));
            surfaceTool.AddVertex(new Vector3(offsetX + Mathf.Cos(angle1) * clotRadius, 0.004f, offsetZ + Mathf.Sin(angle1) * clotRadius));
            surfaceTool.AddVertex(new Vector3(offsetX + Mathf.Cos(angle2) * clotRadius, 0.004f, offsetZ + Mathf.Sin(angle2) * clotRadius));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.03f, 0.03f), // Darker blood
            Roughness = 0.25f, // Glossy wet blood
            Metallic = 0.25f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateSpikeTrapMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Recessed floor pit
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, -0.03f * s, 0), new Vector3(1f * s, 0.06f * s, 1f * s));

        // Outer frame/edge
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.48f * s, 0.02f * s, 0), new Vector3(0.04f * s, 0.04f * s, 0.96f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.48f * s, 0.02f * s, 0), new Vector3(0.04f * s, 0.04f * s, 0.96f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, -0.48f * s), new Vector3(0.96f * s, 0.04f * s, 0.04f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, 0.48f * s), new Vector3(0.96f * s, 0.04f * s, 0.04f * s));

        // Pressure plate mechanism in center
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.005f * s, 0), new Vector3(0.7f * s, 0.01f * s, 0.7f * s));

        // Spikes (pyramids) - 4x4 grid with random heights
        for (int x = 0; x < 4; x++)
        {
            for (int z = 0; z < 4; z++)
            {
                float sx = (x - 1.5f) * 0.2f * s;
                float sz = (z - 1.5f) * 0.2f * s;
                float spikeH = rng.RandfRange(0.2f, 0.45f) * s;
                float spikeBase = rng.RandfRange(0.035f, 0.05f) * s;

                // Spike as pyramid
                Vector3 top = new Vector3(sx, 0.01f * s + spikeH, sz);
                Vector3 b1 = new Vector3(sx - spikeBase, 0.01f * s, sz - spikeBase);
                Vector3 b2 = new Vector3(sx + spikeBase, 0.01f * s, sz - spikeBase);
                Vector3 b3 = new Vector3(sx + spikeBase, 0.01f * s, sz + spikeBase);
                Vector3 b4 = new Vector3(sx - spikeBase, 0.01f * s, sz + spikeBase);
                // Four faces
                surfaceTool.AddVertex(b1); surfaceTool.AddVertex(b2); surfaceTool.AddVertex(top);
                surfaceTool.AddVertex(b2); surfaceTool.AddVertex(b3); surfaceTool.AddVertex(top);
                surfaceTool.AddVertex(b3); surfaceTool.AddVertex(b4); surfaceTool.AddVertex(top);
                surfaceTool.AddVertex(b4); surfaceTool.AddVertex(b1); surfaceTool.AddVertex(top);
            }
        }

        // Blood drips on some spikes
        for (int i = 0; i < 3; i++)
        {
            float bx = rng.RandfRange(-0.3f, 0.3f) * s;
            float bz = rng.RandfRange(-0.3f, 0.3f) * s;
            float by = rng.RandfRange(0.1f, 0.25f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(bx, by, bz), new Vector3(0.02f * s, 0.05f * s, 0.02f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.18f, 0.2f),
            Roughness = 0.5f,
            Metallic = 0.75f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateDiscardedSwordMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Sword lying flat on ground, slightly angled
        float swordY = 0.015f * s;
        float bladeLen = 0.55f * s;
        float bladeWidth = 0.045f * s;

        // === BLADE with beveled edge ===
        // Main blade body (center is thicker)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, swordY, bladeLen * 0.2f), new Vector3(bladeWidth, 0.018f * s, bladeLen));

        // Beveled cutting edges (angled boxes on each side)
        float edgeOffset = bladeWidth * 0.45f;
        float edgeThickness = 0.008f * s;
        // Left edge
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-edgeOffset, swordY - 0.003f * s, bladeLen * 0.2f),
            new Vector3(0.012f * s, edgeThickness, bladeLen * 0.95f));
        // Right edge
        AddBoxToSurfaceTool(surfaceTool, new Vector3(edgeOffset, swordY - 0.003f * s, bladeLen * 0.2f),
            new Vector3(0.012f * s, edgeThickness, bladeLen * 0.95f));

        // BLOOD GROOVE (fuller) - narrow depression down center of blade
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, swordY + 0.008f * s, bladeLen * 0.15f),
            new Vector3(0.008f * s, 0.003f * s, bladeLen * 0.7f));

        // Blade tip (pointed)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, swordY, bladeLen * 0.72f),
            new Vector3(bladeWidth * 0.6f, 0.012f * s, 0.08f * s));

        // === CROSSGUARD with detail ===
        float guardZ = -0.03f * s;
        float guardLen = 0.16f * s;
        // Main crossguard bar
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, swordY + 0.01f * s, guardZ),
            new Vector3(guardLen, 0.025f * s, 0.025f * s));
        // Crossguard ends (quillons) - slightly curved up
        AddSphereToSurfaceTool(surfaceTool, new Vector3(-guardLen * 0.48f, swordY + 0.015f * s, guardZ), 0.018f * s);
        AddSphereToSurfaceTool(surfaceTool, new Vector3(guardLen * 0.48f, swordY + 0.015f * s, guardZ), 0.018f * s);
        // Guard ricasso (where blade meets guard)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, swordY + 0.01f * s, guardZ + 0.02f * s),
            new Vector3(bladeWidth * 0.8f, 0.022f * s, 0.025f * s));

        // === LEATHER GRIP WRAPPING ===
        float gripZ = -0.12f * s;
        float gripLen = 0.1f * s;
        // Main grip wood core
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, swordY + 0.01f * s, gripZ),
            new Vector3(0.028f * s, 0.028f * s, gripLen));
        // Leather wrapping bands (simulated with small boxes)
        int wrapBands = 6;
        for (int w = 0; w < wrapBands; w++)
        {
            float wrapZ = gripZ + gripLen * 0.4f - w * (gripLen * 0.8f / wrapBands);
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, swordY + 0.023f * s, wrapZ),
                new Vector3(0.032f * s, 0.008f * s, 0.012f * s));
        }

        // POMMEL (end cap)
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, swordY + 0.015f * s, gripZ - gripLen * 0.55f), 0.022f * s);

        // === RUST PATCHES (darker spots on blade) ===
        int rustSpots = rng.RandiRange(2, 4);
        for (int r = 0; r < rustSpots; r++)
        {
            float rustZ = rng.RandfRange(0.05f, 0.4f) * s;
            float rustX = rng.RandfRange(-0.015f, 0.015f) * s;
            float rustSize = rng.RandfRange(0.015f, 0.03f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(rustX, swordY + 0.012f * s, rustZ), rustSize);
        }

        // === BLOOD STAINS (optional dark red spots) ===
        if (rng.Randf() > 0.5f)
        {
            float bloodZ = rng.RandfRange(0.15f, 0.35f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(rng.RandfRange(-0.01f, 0.01f) * s, swordY + 0.01f * s, bloodZ), 0.02f * s);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.52f, 0.5f, 0.48f), // Weathered steel
            Roughness = 0.65f,
            Metallic = 0.55f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateShatteredPotionMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // === BROKEN BOTTLE BASE - remaining flask bottom ===
        float baseRadius = 0.04f * s;
        float baseHeight = 0.03f * s;
        int baseSegs = 8;
        for (int i = 0; i < baseSegs; i++)
        {
            float angle1 = i * Mathf.Tau / baseSegs;
            float angle2 = (i + 1) * Mathf.Tau / baseSegs;
            // Bottom disc
            surfaceTool.AddVertex(new Vector3(0, 0.005f, 0));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * baseRadius, 0.005f, Mathf.Sin(angle1) * baseRadius));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * baseRadius, 0.005f, Mathf.Sin(angle2) * baseRadius));
            // Jagged broken rim (random heights)
            float h1 = baseHeight + rng.RandfRange(0, 0.02f) * s;
            float h2 = baseHeight + rng.RandfRange(0, 0.02f) * s;
            // Side wall
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * baseRadius, 0.005f, Mathf.Sin(angle1) * baseRadius));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * baseRadius * 0.9f, h1, Mathf.Sin(angle1) * baseRadius * 0.9f));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * baseRadius * 0.9f, h2, Mathf.Sin(angle2) * baseRadius * 0.9f));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * baseRadius, 0.005f, Mathf.Sin(angle1) * baseRadius));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * baseRadius * 0.9f, h2, Mathf.Sin(angle2) * baseRadius * 0.9f));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * baseRadius, 0.005f, Mathf.Sin(angle2) * baseRadius));
        }

        // === LARGE GLASS SHARDS - scattered around ===
        int largeShardCount = rng.RandiRange(4, 6);
        for (int i = 0; i < largeShardCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.06f, 0.18f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float shardAngle = rng.Randf() * Mathf.Tau;
            float shardLen = rng.RandfRange(0.03f, 0.06f) * s;
            float shardHeight = rng.RandfRange(0.02f, 0.045f) * s;
            float shardWidth = rng.RandfRange(0.01f, 0.02f) * s;

            // Curved shard (triangular prism)
            float dx = Mathf.Cos(shardAngle) * shardLen;
            float dz = Mathf.Sin(shardAngle) * shardLen;
            float nx = -Mathf.Sin(shardAngle) * shardWidth;
            float nz = Mathf.Cos(shardAngle) * shardWidth;

            // Top face
            surfaceTool.AddVertex(new Vector3(x - dx, 0.01f, z - dz));
            surfaceTool.AddVertex(new Vector3(x + dx, 0.01f, z + dz));
            surfaceTool.AddVertex(new Vector3(x, shardHeight, z));

            // Side faces
            surfaceTool.AddVertex(new Vector3(x - dx, 0.01f, z - dz));
            surfaceTool.AddVertex(new Vector3(x + nx, 0.01f, z + nz));
            surfaceTool.AddVertex(new Vector3(x, shardHeight, z));

            surfaceTool.AddVertex(new Vector3(x + dx, 0.01f, z + dz));
            surfaceTool.AddVertex(new Vector3(x - nx, 0.01f, z - nz));
            surfaceTool.AddVertex(new Vector3(x, shardHeight, z));
        }

        // === SMALL GLASS FRAGMENTS - tiny pieces ===
        int smallShardCount = rng.RandiRange(8, 12);
        for (int i = 0; i < smallShardCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.04f, 0.22f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float fragSize = rng.RandfRange(0.008f, 0.018f) * s;

            // Tiny triangle shard
            surfaceTool.AddVertex(new Vector3(x, 0.008f, z));
            surfaceTool.AddVertex(new Vector3(x + fragSize, 0.008f, z + fragSize * 0.5f));
            surfaceTool.AddVertex(new Vector3(x + fragSize * 0.3f, rng.RandfRange(0.01f, 0.025f) * s, z + fragSize * 0.7f));
        }

        // === LIQUID RESIDUE POOL - larger irregular shape ===
        int poolSegs = 10;
        for (int i = 0; i < poolSegs; i++)
        {
            float angle1 = i * Mathf.Tau / poolSegs;
            float angle2 = (i + 1) * Mathf.Tau / poolSegs;
            float r1 = (0.12f + rng.Randf() * 0.06f) * s;
            float r2 = (0.12f + rng.Randf() * 0.06f) * s;
            surfaceTool.AddVertex(new Vector3(0, 0.003f, 0));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * r1, 0.002f, Mathf.Sin(angle1) * r1));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r2, 0.002f, Mathf.Sin(angle2) * r2));
        }

        // === LIQUID STREAMS - running away from pool ===
        int streamCount = rng.RandiRange(2, 4);
        for (int i = 0; i < streamCount; i++)
        {
            float streamAngle = rng.Randf() * Mathf.Tau;
            float streamLen = rng.RandfRange(0.08f, 0.15f) * s;
            float streamWidth = rng.RandfRange(0.012f, 0.022f) * s;
            float startDist = 0.1f * s;

            float sx = Mathf.Cos(streamAngle) * startDist;
            float sz = Mathf.Sin(streamAngle) * startDist;
            float ex = Mathf.Cos(streamAngle) * (startDist + streamLen);
            float ez = Mathf.Sin(streamAngle) * (startDist + streamLen);
            float nx = -Mathf.Sin(streamAngle) * streamWidth;
            float nz = Mathf.Cos(streamAngle) * streamWidth;

            // Stream quad (tapers to end)
            surfaceTool.AddVertex(new Vector3(sx + nx, 0.002f, sz + nz));
            surfaceTool.AddVertex(new Vector3(sx - nx, 0.002f, sz - nz));
            surfaceTool.AddVertex(new Vector3(ex, 0.001f, ez)); // Tapered end

            surfaceTool.AddVertex(new Vector3(sx + nx, 0.002f, sz + nz));
            surfaceTool.AddVertex(new Vector3(ex, 0.001f, ez));
            surfaceTool.AddVertex(new Vector3(sx - nx, 0.002f, sz - nz));
        }

        // === VAPOR WISPS - rising from the pool ===
        int vaporCount = rng.RandiRange(3, 5);
        for (int i = 0; i < vaporCount; i++)
        {
            float vx = rng.RandfRange(-0.06f, 0.06f) * s;
            float vz = rng.RandfRange(-0.06f, 0.06f) * s;
            float vaporHeight = rng.RandfRange(0.04f, 0.08f) * s;
            float vaporWidth = rng.RandfRange(0.015f, 0.025f) * s;

            // Wisp as thin vertical triangle
            surfaceTool.AddVertex(new Vector3(vx - vaporWidth, 0.01f, vz));
            surfaceTool.AddVertex(new Vector3(vx + vaporWidth, 0.01f, vz));
            surfaceTool.AddVertex(new Vector3(vx + rng.RandfRange(-0.01f, 0.01f) * s, vaporHeight, vz + rng.RandfRange(-0.01f, 0.01f) * s));

            // Second wisp face (perpendicular)
            surfaceTool.AddVertex(new Vector3(vx, 0.01f, vz - vaporWidth));
            surfaceTool.AddVertex(new Vector3(vx, 0.01f, vz + vaporWidth));
            surfaceTool.AddVertex(new Vector3(vx + rng.RandfRange(-0.01f, 0.01f) * s, vaporHeight * 0.9f, vz));
        }

        // === CORK/STOPPER - thrown aside ===
        float corkX = rng.RandfRange(0.1f, 0.18f) * s * (rng.Randf() > 0.5f ? 1 : -1);
        float corkZ = rng.RandfRange(0.1f, 0.18f) * s * (rng.Randf() > 0.5f ? 1 : -1);
        float corkR = 0.018f * s;
        float corkH = 0.025f * s;
        for (int i = 0; i < 6; i++)
        {
            float angle1 = i * Mathf.Tau / 6;
            float angle2 = (i + 1) * Mathf.Tau / 6;
            // Cork lying on side
            surfaceTool.AddVertex(new Vector3(corkX, 0.01f, corkZ));
            surfaceTool.AddVertex(new Vector3(corkX + Mathf.Cos(angle1) * corkR, 0.01f + Mathf.Sin(angle1) * corkR, corkZ));
            surfaceTool.AddVertex(new Vector3(corkX + Mathf.Cos(angle2) * corkR, 0.01f + Mathf.Sin(angle2) * corkR, corkZ));
            // Extend length
            surfaceTool.AddVertex(new Vector3(corkX + Mathf.Cos(angle1) * corkR, 0.01f + Mathf.Sin(angle1) * corkR, corkZ));
            surfaceTool.AddVertex(new Vector3(corkX + Mathf.Cos(angle1) * corkR, 0.01f + Mathf.Sin(angle1) * corkR, corkZ + corkH));
            surfaceTool.AddVertex(new Vector3(corkX + Mathf.Cos(angle2) * corkR, 0.01f + Mathf.Sin(angle2) * corkR, corkZ + corkH));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.85f, 0.35f, 0.75f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.15f, 0.95f, 0.25f),
            EmissionEnergyMultiplier = 1.2f,
            Roughness = 0.15f // Glossy glass/liquid
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateAncientScrollMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // === MAIN SCROLL BODY - partially unrolled parchment ===
        float scrollWidth = 0.4f * s;
        float scrollDepth = 0.32f * s;
        float scrollY = 0.008f * s;

        // Main parchment with slight wave/curl
        int waveSegs = 6;
        for (int i = 0; i < waveSegs; i++)
        {
            float z1 = -scrollDepth / 2 + i * scrollDepth / waveSegs;
            float z2 = -scrollDepth / 2 + (i + 1) * scrollDepth / waveSegs;
            float y1 = scrollY + Mathf.Sin(i * 0.8f) * 0.003f * s; // Slight wave
            float y2 = scrollY + Mathf.Sin((i + 1) * 0.8f) * 0.003f * s;

            // Quad for each wave segment
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2, y1, z1));
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2, y1, z1));
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2, y2, z2));

            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2, y1, z1));
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2, y2, z2));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2, y2, z2));
        }

        // === ROLLED ENDS with decorative caps ===
        float rollRadius = 0.025f * s;
        float rollY = 0.022f * s;

        // Left roll (slightly raised)
        for (int i = 0; i < 8; i++)
        {
            float angle1 = i * Mathf.Tau / 8;
            float angle2 = (i + 1) * Mathf.Tau / 8;
            // Roll cylinder
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 - 0.01f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 + 0.02f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 + 0.02f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));

            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 - 0.01f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 + 0.02f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 - 0.01f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));
        }

        // Right roll
        for (int i = 0; i < 8; i++)
        {
            float angle1 = i * Mathf.Tau / 8;
            float angle2 = (i + 1) * Mathf.Tau / 8;
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2 - 0.02f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2 + 0.01f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2 + 0.01f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));

            surfaceTool.AddVertex(new Vector3(scrollWidth / 2 - 0.02f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2 + 0.01f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));
            surfaceTool.AddVertex(new Vector3(scrollWidth / 2 - 0.02f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));
        }

        // Decorative wooden/bone end caps
        AddSphereToSurfaceTool(surfaceTool, new Vector3(-scrollWidth / 2 - 0.015f * s, rollY, 0), 0.018f * s);
        AddSphereToSurfaceTool(surfaceTool, new Vector3(scrollWidth / 2 + 0.015f * s, rollY, 0), 0.018f * s);

        // === VISIBLE TEXT/RUNES on scroll surface ===
        // Horizontal text lines
        int lineCount = rng.RandiRange(5, 7);
        float lineSpacing = scrollDepth * 0.8f / lineCount;
        for (int line = 0; line < lineCount; line++)
        {
            float lineZ = -scrollDepth * 0.35f + line * lineSpacing;
            float lineY = scrollY + 0.002f * s;

            // Multiple "words" per line
            int wordCount = rng.RandiRange(3, 5);
            float wordStart = -scrollWidth * 0.35f;
            for (int w = 0; w < wordCount; w++)
            {
                float wordLen = rng.RandfRange(0.03f, 0.06f) * s;
                float wordX = wordStart + w * (scrollWidth * 0.65f / wordCount) + rng.RandfRange(-0.01f, 0.01f) * s;

                // Word as thin rectangle
                AddBoxToSurfaceTool(surfaceTool, new Vector3(wordX, lineY, lineZ), new Vector3(wordLen, 0.001f * s, 0.008f * s));
            }
        }

        // === DECORATIVE RUNE SYMBOLS - mystical glyphs ===
        // Large central rune
        float runeY = scrollY + 0.003f * s;
        // Rune circle
        float runeRadius = 0.035f * s;
        for (int i = 0; i < 6; i++)
        {
            float angle1 = i * Mathf.Tau / 6;
            float angle2 = (i + 1) * Mathf.Tau / 6;
            surfaceTool.AddVertex(new Vector3(0, runeY, 0));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * runeRadius, runeY, Mathf.Sin(angle1) * runeRadius));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * runeRadius, runeY, Mathf.Sin(angle2) * runeRadius));
        }
        // Rune lines (star pattern inside)
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.Tau / 3;
            float dx = Mathf.Cos(angle) * runeRadius * 0.8f;
            float dz = Mathf.Sin(angle) * runeRadius * 0.8f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, runeY + 0.001f * s, 0), new Vector3(0.004f * s, 0.001f * s, runeRadius * 1.5f));
        }

        // Corner runes (smaller)
        float[] cornerX = { -scrollWidth * 0.35f, scrollWidth * 0.35f, -scrollWidth * 0.35f, scrollWidth * 0.35f };
        float[] cornerZ = { -scrollDepth * 0.35f, -scrollDepth * 0.35f, scrollDepth * 0.35f, scrollDepth * 0.35f };
        for (int c = 0; c < 4; c++)
        {
            // Small rune triangle
            float cx = cornerX[c];
            float cz = cornerZ[c];
            float sr = 0.015f * s;
            surfaceTool.AddVertex(new Vector3(cx, runeY, cz - sr));
            surfaceTool.AddVertex(new Vector3(cx + sr, runeY, cz + sr));
            surfaceTool.AddVertex(new Vector3(cx - sr, runeY, cz + sr));
        }

        // === WAX SEAL - on one edge ===
        float sealX = scrollWidth * 0.25f;
        float sealZ = scrollDepth * 0.38f;
        float sealRadius = 0.028f * s;

        // Seal base (slightly raised disc)
        for (int i = 0; i < 8; i++)
        {
            float angle1 = i * Mathf.Tau / 8;
            float angle2 = (i + 1) * Mathf.Tau / 8;
            surfaceTool.AddVertex(new Vector3(sealX, scrollY + 0.005f * s, sealZ));
            surfaceTool.AddVertex(new Vector3(sealX + Mathf.Cos(angle1) * sealRadius, scrollY + 0.004f * s, sealZ + Mathf.Sin(angle1) * sealRadius));
            surfaceTool.AddVertex(new Vector3(sealX + Mathf.Cos(angle2) * sealRadius, scrollY + 0.004f * s, sealZ + Mathf.Sin(angle2) * sealRadius));
        }

        // Seal drip marks (irregular edges)
        for (int i = 0; i < 4; i++)
        {
            float dripAngle = rng.Randf() * Mathf.Tau;
            float dripLen = rng.RandfRange(0.01f, 0.02f) * s;
            float dx = Mathf.Cos(dripAngle) * (sealRadius + dripLen / 2);
            float dz = Mathf.Sin(dripAngle) * (sealRadius + dripLen / 2);
            AddBoxToSurfaceTool(surfaceTool, new Vector3(sealX + dx, scrollY + 0.003f * s, sealZ + dz), new Vector3(0.006f * s, 0.002f * s, dripLen));
        }

        // Seal imprint (simple cross)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(sealX, scrollY + 0.006f * s, sealZ), new Vector3(sealRadius * 1.2f, 0.001f * s, 0.005f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(sealX, scrollY + 0.006f * s, sealZ), new Vector3(0.005f * s, 0.001f * s, sealRadius * 1.2f));

        // === TORN/AGED EDGES - irregular border ===
        int tornCount = rng.RandiRange(3, 5);
        for (int i = 0; i < tornCount; i++)
        {
            // Torn notches on edges
            float edgeSide = rng.Randf(); // 0-0.5 = left/right, 0.5-1 = top/bottom
            float tornX, tornZ;
            if (edgeSide < 0.25f)
            {
                tornX = -scrollWidth / 2 + rng.RandfRange(0.01f, 0.04f) * s;
                tornZ = rng.RandfRange(-scrollDepth * 0.4f, scrollDepth * 0.4f);
            }
            else if (edgeSide < 0.5f)
            {
                tornX = scrollWidth / 2 - rng.RandfRange(0.01f, 0.04f) * s;
                tornZ = rng.RandfRange(-scrollDepth * 0.4f, scrollDepth * 0.4f);
            }
            else if (edgeSide < 0.75f)
            {
                tornX = rng.RandfRange(-scrollWidth * 0.4f, scrollWidth * 0.4f);
                tornZ = -scrollDepth / 2 + rng.RandfRange(0.01f, 0.03f) * s;
            }
            else
            {
                tornX = rng.RandfRange(-scrollWidth * 0.4f, scrollWidth * 0.4f);
                tornZ = scrollDepth / 2 - rng.RandfRange(0.01f, 0.03f) * s;
            }

            // Small triangular tear
            float tearSize = rng.RandfRange(0.01f, 0.025f) * s;
            float tearAngle = rng.Randf() * Mathf.Tau;
            surfaceTool.AddVertex(new Vector3(tornX, scrollY + 0.001f * s, tornZ));
            surfaceTool.AddVertex(new Vector3(tornX + Mathf.Cos(tearAngle) * tearSize, scrollY, tornZ + Mathf.Sin(tearAngle) * tearSize));
            surfaceTool.AddVertex(new Vector3(tornX + Mathf.Cos(tearAngle + 1.2f) * tearSize * 0.7f, scrollY, tornZ + Mathf.Sin(tearAngle + 1.2f) * tearSize * 0.7f));
        }

        // === STAIN MARKS - age spots ===
        int stainCount = rng.RandiRange(2, 4);
        for (int i = 0; i < stainCount; i++)
        {
            float stainX = rng.RandfRange(-scrollWidth * 0.35f, scrollWidth * 0.35f);
            float stainZ = rng.RandfRange(-scrollDepth * 0.35f, scrollDepth * 0.35f);
            float stainRadius = rng.RandfRange(0.015f, 0.03f) * s;

            for (int j = 0; j < 5; j++)
            {
                float angle1 = j * Mathf.Tau / 5;
                float angle2 = (j + 1) * Mathf.Tau / 5;
                float r1 = stainRadius * (0.8f + rng.Randf() * 0.4f);
                float r2 = stainRadius * (0.8f + rng.Randf() * 0.4f);
                surfaceTool.AddVertex(new Vector3(stainX, scrollY + 0.001f * s, stainZ));
                surfaceTool.AddVertex(new Vector3(stainX + Mathf.Cos(angle1) * r1, scrollY + 0.001f * s, stainZ + Mathf.Sin(angle1) * r1));
                surfaceTool.AddVertex(new Vector3(stainX + Mathf.Cos(angle2) * r2, scrollY + 0.001f * s, stainZ + Mathf.Sin(angle2) * r2));
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.82f, 0.76f, 0.6f), // Aged yellowed parchment
            Roughness = 0.92f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateBrazierFireMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Iron bowl base
        int segments = 8;
        float bowlR = 0.35f * s;
        float bowlH = 0.3f * s;
        // Bowl as truncated cone
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;
            float r1Top = bowlR, r1Bot = bowlR * 0.6f;
            // Side quad
            Vector3 tl = new Vector3(Mathf.Cos(angle1) * r1Top, bowlH, Mathf.Sin(angle1) * r1Top);
            Vector3 tr = new Vector3(Mathf.Cos(angle2) * r1Top, bowlH, Mathf.Sin(angle2) * r1Top);
            Vector3 bl = new Vector3(Mathf.Cos(angle1) * r1Bot, 0, Mathf.Sin(angle1) * r1Bot);
            Vector3 br = new Vector3(Mathf.Cos(angle2) * r1Bot, 0, Mathf.Sin(angle2) * r1Bot);
            surfaceTool.AddVertex(bl); surfaceTool.AddVertex(tl); surfaceTool.AddVertex(tr);
            surfaceTool.AddVertex(bl); surfaceTool.AddVertex(tr); surfaceTool.AddVertex(br);
        }

        // Legs (3 legs) with decorative feet
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.Tau / 3;
            float x = Mathf.Cos(angle) * bowlR * 0.7f;
            float z = Mathf.Sin(angle) * bowlR * 0.7f;
            // Main leg
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, -0.2f * s, z), new Vector3(0.05f * s, 0.4f * s, 0.05f * s));
            // Decorative claw foot
            float footX = Mathf.Cos(angle) * bowlR * 0.85f;
            float footZ = Mathf.Sin(angle) * bowlR * 0.85f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(footX, -0.38f * s, footZ), new Vector3(0.08f * s, 0.04f * s, 0.08f * s));
            // Leg to bowl connection brace
            float braceX = Mathf.Cos(angle) * bowlR * 0.55f;
            float braceZ = Mathf.Sin(angle) * bowlR * 0.55f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(braceX, 0.05f * s, braceZ), new Vector3(0.04f * s, 0.04f * s, 0.15f * s));
        }

        // Rim decoration (top edge of bowl)
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            float rimX = Mathf.Cos(angle) * (bowlR + 0.02f * s);
            float rimZ = Mathf.Sin(angle) * (bowlR + 0.02f * s);
            AddBoxToSurfaceTool(surfaceTool, new Vector3(rimX, bowlH + 0.02f * s, rimZ), new Vector3(0.03f * s, 0.04f * s, 0.03f * s));
        }

        // Coals at the bottom of the bowl
        for (int i = 0; i < 5; i++)
        {
            float coalAngle = rng.Randf() * Mathf.Tau;
            float coalDist = rng.RandfRange(0.05f, 0.2f) * s;
            float coalX = Mathf.Cos(coalAngle) * coalDist;
            float coalZ = Mathf.Sin(coalAngle) * coalDist;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(coalX, bowlH - 0.05f * s, coalZ), rng.RandfRange(0.03f, 0.06f) * s);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.23f, 0.2f),
            Roughness = 0.7f,
            Metallic = 0.6f
        };
        mesh.SurfaceSetMaterial(0, material);

        // Fire particles will be added via CallDeferred
        CallDeferred(nameof(CreateBrazierFireParticles));

        return mesh;
    }

    private void CreateBrazierFireParticles()
    {
        var fireParticles = new GpuParticles3D();
        fireParticles.Name = "BrazierFire";
        fireParticles.Amount = 40;
        fireParticles.Lifetime = 0.8;
        fireParticles.Position = new Vector3(0, 0.4f * PropScale, 0);

        var fireMaterial = new ParticleProcessMaterial();
        fireMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        fireMaterial.EmissionSphereRadius = 0.15f * PropScale;
        fireMaterial.Direction = new Vector3(0, 1, 0);
        fireMaterial.Spread = 20f;
        fireMaterial.InitialVelocityMin = 0.8f;
        fireMaterial.InitialVelocityMax = 2f;
        fireMaterial.Gravity = new Vector3(0, 3f, 0);
        fireMaterial.ScaleMin = 0.15f;
        fireMaterial.ScaleMax = 0.35f;
        var colorRamp = new Gradient();
        colorRamp.SetColor(0, new Color(1f, 0.9f, 0.4f, 1f));
        colorRamp.SetColor(1, new Color(1f, 0.3f, 0.1f, 0f));
        var colorTexture = new GradientTexture1D { Gradient = colorRamp };
        fireMaterial.ColorRamp = colorTexture;
        fireParticles.ProcessMaterial = fireMaterial;

        var quadMesh = new QuadMesh { Size = new Vector2(0.25f, 0.35f) };
        var fireMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.6f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.5f, 0.1f),
            EmissionEnergyMultiplier = 2.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        quadMesh.Material = fireMat;
        fireParticles.DrawPass1 = quadMesh;
        AddChild(fireParticles);
    }

    private ArrayMesh CreateManaclesMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Wall mounting bracket (at Z+ to attach to wall)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.15f * s, 0.05f * s), new Vector3(0.25f * s, 0.04f * s, 0.03f * s));

        // Mounting ring
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8f;
            float r = 0.04f * s;
            AddBoxToSurfaceTool(surfaceTool,
                new Vector3(Mathf.Cos(angle) * r, 0.15f * s, 0.02f * s + Mathf.Sin(angle) * r * 0.3f),
                new Vector3(0.015f * s, 0.015f * s, 0.01f * s));
        }

        // Chain links hanging down
        for (int i = 0; i < 6; i++)
        {
            float y = 0.12f * s - i * 0.025f * s;
            float xOff = (i % 2 == 0) ? 0.005f * s : -0.005f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(xOff, y, 0), new Vector3(0.02f * s, 0.015f * s, 0.01f * s));
        }

        // Two open cuffs at bottom
        for (int c = 0; c < 2; c++)
        {
            float offsetX = (c - 0.5f) * 0.15f * s;
            // Cuff as partial ring (open)
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.Pi / 5f - Mathf.Pi / 2f;
                float r = 0.05f * s;
                float tr = 0.012f * s;
                AddBoxToSurfaceTool(surfaceTool,
                    new Vector3(offsetX + Mathf.Cos(angle) * r, -0.05f * s + Mathf.Sin(angle) * r, 0),
                    new Vector3(tr * 2, tr * 2, tr * 1.5f));
            }
        }

        // Chain between cuffs
        for (int i = 0; i < 5; i++)
        {
            float x = -0.06f * s + i * 0.03f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, -0.05f * s, 0), new Vector3(0.02f * s, 0.012f * s, 0.015f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.32f, 0.28f),
            Roughness = 0.7f,
            Metallic = 0.6f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateCrackedTilesMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Create broken tile pieces
        int pieces = rng.RandiRange(4, 7);
        for (int i = 0; i < pieces; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.3f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float tileW = rng.RandfRange(0.1f, 0.2f) * s;
            float tileD = rng.RandfRange(0.1f, 0.2f) * s;
            float tileH = rng.RandfRange(0.01f, 0.03f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, tileH / 2, z), new Vector3(tileW, tileH, tileD));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.33f, 0.3f),
            Roughness = 0.9f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateRubbleHeapMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Pile of rocks
        int rocks = rng.RandiRange(8, 15);
        for (int i = 0; i < rocks; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0f, 0.4f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = (0.4f - dist / s) * 0.5f * s; // Higher toward center
            float rockR = rng.RandfRange(0.06f, 0.15f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, y, z), rockR);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.38f, 0.35f),
            Roughness = 0.95f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateThornyVinesMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Twisted vines spreading on floor
        int vines = rng.RandiRange(3, 5);
        for (int v = 0; v < vines; v++)
        {
            float startAngle = rng.Randf() * Mathf.Tau;
            float len = rng.RandfRange(0.3f, 0.5f) * s;

            // Vine as series of connected boxes
            for (int seg = 0; seg < 5; seg++)
            {
                float t = seg / 5f;
                float angle = startAngle + t * 0.5f;
                float dist = t * len;
                float x = Mathf.Cos(angle) * dist;
                float z = Mathf.Sin(angle) * dist;
                float y = 0.02f * s + Mathf.Sin(t * Mathf.Pi) * 0.05f * s;
                AddBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z), new Vector3(0.04f * s, 0.03f * s, 0.08f * s));

                // Add thorns
                if (seg % 2 == 0)
                {
                    Vector3 thornPos = new Vector3(x + 0.02f * s, y + 0.02f * s, z);
                    AddBoxToSurfaceTool(surfaceTool, thornPos, new Vector3(0.01f * s, 0.02f * s, 0.01f * s));
                }
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.35f, 0.2f), // Dark green
            Roughness = 0.85f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateEngravedRuneMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Stone slab with glowing rune pattern
        AddBoxToSurfaceTool(surfaceTool, Vector3.Zero, new Vector3(0.8f * s, 0.03f * s, 0.8f * s));

        // Rune pattern (simplified cross with circle)
        // Center circle
        for (int i = 0; i < 8; i++)
        {
            float angle1 = i * Mathf.Tau / 8;
            float angle2 = (i + 1) * Mathf.Tau / 8;
            float r = 0.2f * s;
            Vector3 c = new Vector3(0, 0.02f * s, 0);
            surfaceTool.AddVertex(c);
            surfaceTool.AddVertex(c + new Vector3(Mathf.Cos(angle1) * r, 0, Mathf.Sin(angle1) * r));
            surfaceTool.AddVertex(c + new Vector3(Mathf.Cos(angle2) * r, 0, Mathf.Sin(angle2) * r));
        }
        // Cross lines
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.025f * s, 0), new Vector3(0.6f * s, 0.01f * s, 0.04f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.025f * s, 0), new Vector3(0.04f * s, 0.01f * s, 0.6f * s));

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.32f, 0.3f),
            Roughness = 0.8f,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.1f),
            EmissionEnergyMultiplier = 0.8f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateAbandonedCampfireMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Stone ring
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8;
            float x = Mathf.Cos(angle) * 0.35f * s;
            float z = Mathf.Sin(angle) * 0.35f * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, 0.05f * s, z), 0.08f * s);
        }

        // Cold ashes in center (dark flat disc)
        for (int i = 0; i < 6; i++)
        {
            float angle1 = i * Mathf.Tau / 6;
            float angle2 = (i + 1) * Mathf.Tau / 6;
            float r = 0.25f * s;
            surfaceTool.AddVertex(new Vector3(0, 0.01f * s, 0));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * r, 0.01f * s, Mathf.Sin(angle1) * r));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r, 0.01f * s, Mathf.Sin(angle2) * r));
        }

        // Charred stick remnants
        for (int i = 0; i < 3; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float len = rng.RandfRange(0.1f, 0.2f) * s;
            AddBoxToSurfaceTool(surfaceTool,
                new Vector3(Mathf.Cos(angle) * 0.1f * s, 0.03f * s, Mathf.Sin(angle) * 0.1f * s),
                new Vector3(len, 0.03f * s, 0.02f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.12f, 0.1f), // Charred dark
            Roughness = 0.95f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateRatNestMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Colors for different nest materials
        var strawColor = new Color(0.65f, 0.55f, 0.3f);      // Golden straw
        var dirtyStrawColor = new Color(0.45f, 0.38f, 0.25f); // Dirty straw
        var fabricColor = new Color(0.35f, 0.3f, 0.35f);      // Faded fabric
        var fabricRedColor = new Color(0.45f, 0.25f, 0.2f);   // Faded red fabric
        var boneColor = new Color(0.85f, 0.8f, 0.7f);         // Bone white
        var droppingColor = new Color(0.15f, 0.12f, 0.08f);   // Dark brown droppings

        // === STRUCTURAL BOWL BASE ===
        // Create bowl-shaped foundation using overlapping curved segments
        int bowlSegments = 16;
        float bowlRadius = 0.28f * s;
        float bowlHeight = 0.08f * s;
        for (int i = 0; i < bowlSegments; i++)
        {
            float angle1 = (i / (float)bowlSegments) * Mathf.Tau;
            float angle2 = ((i + 1) / (float)bowlSegments) * Mathf.Tau;
            float midAngle = (angle1 + angle2) / 2f;

            // Outer rim pieces (thicker at edges, forming bowl lip)
            float rimX = Mathf.Cos(midAngle) * bowlRadius;
            float rimZ = Mathf.Sin(midAngle) * bowlRadius;
            float rimLen = 0.12f * s;
            float rimThick = 0.025f * s;
            // Angle the rim pieces to follow the curve
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(rimX, bowlHeight * 0.6f, rimZ),
                new Vector3(rimLen, rimThick, rimThick * 1.5f), midAngle + Mathf.Pi/2);
        }

        // Inner bowl floor (woven base)
        int floorPieces = 12;
        for (int i = 0; i < floorPieces; i++)
        {
            float angle = (i / (float)floorPieces) * Mathf.Tau + rng.RandfRange(-0.1f, 0.1f);
            float dist = rng.RandfRange(0.02f, 0.18f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float pieceLen = rng.RandfRange(0.08f, 0.16f) * s;
            float pieceThick = rng.RandfRange(0.008f, 0.015f) * s;
            // Woven floor pieces cross at angles
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.015f * s, z),
                new Vector3(pieceLen, pieceThick, pieceThick), angle + rng.RandfRange(-0.3f, 0.3f));
        }

        // === STRAW/HAY STRANDS (thin cylinders approximated with elongated boxes) ===
        int strawCount = rng.RandiRange(25, 35);
        for (int i = 0; i < strawCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0f, 0.26f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            // Straw accumulates higher toward center (bowl depression)
            float heightBase = dist < 0.12f * s ? 0.02f : 0.04f;
            float y = rng.RandfRange(heightBase, heightBase + 0.08f) * s;

            // Very thin, long straw pieces
            float strawLen = rng.RandfRange(0.06f, 0.14f) * s;
            float strawThick = rng.RandfRange(0.004f, 0.008f) * s;

            // Random orientation (straw goes in many directions)
            float strawAngle = rng.Randf() * Mathf.Tau;
            float tilt = rng.RandfRange(-0.3f, 0.3f);

            // Alternate between clean and dirty straw colors
            bool isDirty = rng.Randf() > 0.6f;
            surfaceTool.SetColor(isDirty ? dirtyStrawColor : strawColor);
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                new Vector3(strawLen, strawThick, strawThick), strawAngle);
        }

        // === FABRIC SCRAPS (flat irregular boxes) ===
        int fabricCount = rng.RandiRange(4, 7);
        for (int i = 0; i < fabricCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.22f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.03f, 0.1f) * s;

            // Flat, irregular fabric pieces
            float fabricW = rng.RandfRange(0.03f, 0.06f) * s;
            float fabricL = rng.RandfRange(0.04f, 0.08f) * s;
            float fabricH = rng.RandfRange(0.003f, 0.008f) * s; // Very flat

            float fabricAngle = rng.Randf() * Mathf.Tau;

            // Alternate fabric colors
            bool isRed = rng.Randf() > 0.5f;
            surfaceTool.SetColor(isRed ? fabricRedColor : fabricColor);
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                new Vector3(fabricL, fabricH, fabricW), fabricAngle);

            // Some fabric scraps have frayed edges (tiny boxes around edge)
            if (rng.Randf() > 0.5f)
            {
                for (int f = 0; f < 3; f++)
                {
                    float frayX = x + rng.RandfRange(-fabricL/2, fabricL/2);
                    float frayZ = z + rng.RandfRange(-fabricW/2, fabricW/2);
                    AddBoxToSurfaceTool(surfaceTool, new Vector3(frayX, y, frayZ),
                        new Vector3(0.008f * s, 0.002f * s, 0.003f * s));
                }
            }
        }

        // === BONE FRAGMENTS ===
        int boneCount = rng.RandiRange(3, 6);
        surfaceTool.SetColor(boneColor);
        for (int i = 0; i < boneCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.03f, 0.2f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.02f, 0.07f) * s;

            float boneAngle = rng.Randf() * Mathf.Tau;

            int boneType = rng.RandiRange(0, 2);
            if (boneType == 0)
            {
                // Long bone (like a small femur)
                float boneLen = rng.RandfRange(0.04f, 0.07f) * s;
                float boneThick = 0.008f * s;
                AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                    new Vector3(boneLen, boneThick, boneThick), boneAngle);
                // Bone ends (knobs)
                float endOffset = boneLen / 2f - 0.005f * s;
                AddBoxToSurfaceTool(surfaceTool, new Vector3(x + Mathf.Cos(boneAngle) * endOffset, y, z + Mathf.Sin(boneAngle) * endOffset),
                    new Vector3(0.012f * s, 0.012f * s, 0.012f * s));
                AddBoxToSurfaceTool(surfaceTool, new Vector3(x - Mathf.Cos(boneAngle) * endOffset, y, z - Mathf.Sin(boneAngle) * endOffset),
                    new Vector3(0.01f * s, 0.01f * s, 0.01f * s));
            }
            else if (boneType == 1)
            {
                // Rib fragment (curved approximation with 2 angled pieces)
                float ribLen = 0.025f * s;
                float ribThick = 0.006f * s;
                AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                    new Vector3(ribLen, ribThick, ribThick), boneAngle);
                AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x + 0.02f * s, y + 0.008f * s, z),
                    new Vector3(ribLen, ribThick, ribThick), boneAngle + 0.4f);
            }
            else
            {
                // Skull fragment (flat irregular piece)
                AddBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                    new Vector3(rng.RandfRange(0.015f, 0.025f) * s, 0.005f * s, rng.RandfRange(0.012f, 0.02f) * s));
            }
        }

        // === DROPPINGS (small dark spheres approximated with cubes) ===
        int droppingCount = rng.RandiRange(8, 14);
        surfaceTool.SetColor(droppingColor);
        for (int i = 0; i < droppingCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0f, 0.25f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.005f, 0.04f) * s;

            // Small oval droppings (slightly elongated cubes)
            float dropLen = rng.RandfRange(0.006f, 0.012f) * s;
            float dropW = dropLen * rng.RandfRange(0.5f, 0.7f);
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                new Vector3(dropLen, dropW, dropW));
        }

        // === SCATTERED DEBRIS on top ===
        // Small twigs and bits on the surface
        int debrisCount = rng.RandiRange(6, 10);
        for (int i = 0; i < debrisCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.02f, 0.24f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.06f, 0.12f) * s; // On top of nest

            float debrisLen = rng.RandfRange(0.02f, 0.05f) * s;
            float debrisThick = rng.RandfRange(0.003f, 0.006f) * s;
            float debrisAngle = rng.Randf() * Mathf.Tau;

            surfaceTool.SetColor(rng.Randf() > 0.5f ? strawColor : dirtyStrawColor);
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                new Vector3(debrisLen, debrisThick, debrisThick), debrisAngle);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.42f, 0.3f), // Base nest color
            Roughness = 0.95f,
            VertexColorUseAsAlbedo = true  // Use vertex colors for variety
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateBrokenPotteryMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Scattered pottery shards
        int shards = rng.RandiRange(6, 10);
        for (int i = 0; i < shards; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.3f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            // Curved shard (simplified as angled box)
            float shardW = rng.RandfRange(0.04f, 0.1f) * s;
            float shardH = rng.RandfRange(0.02f, 0.05f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, shardH/2, z), new Vector3(shardW, shardH, shardW * 0.6f));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.55f, 0.4f), // Terracotta
            Roughness = 0.85f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateScatteredCoinsMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // === COIN MATERIALS (gold and silver with variation) ===
        // Note: SurfaceTool uses single material, so we'll use gold as base
        // and create visual variety through geometry

        // === STACKED COIN PILES (2-4 piles with 3-6 coins each) ===
        int numPiles = rng.RandiRange(2, 4);
        for (int pile = 0; pile < numPiles; pile++)
        {
            float pileAngle = pile * Mathf.Tau / numPiles + rng.RandfRange(-0.3f, 0.3f);
            float pileDist = rng.RandfRange(0.08f, 0.18f) * s;
            float pileX = Mathf.Cos(pileAngle) * pileDist;
            float pileZ = Mathf.Sin(pileAngle) * pileDist;

            int coinsInPile = rng.RandiRange(3, 6);
            float baseY = 0.003f * s;
            float coinThickness = 0.004f * s;

            for (int c = 0; c < coinsInPile; c++)
            {
                // Vary coin size (small, medium, large)
                float coinSize = rng.RandfRange(0.018f, 0.032f) * s;
                float coinY = baseY + c * coinThickness * 0.9f; // Slight overlap

                // Small offset for each coin in pile (not perfectly aligned)
                float offsetX = rng.RandfRange(-0.003f, 0.003f) * s;
                float offsetZ = rng.RandfRange(-0.003f, 0.003f) * s;

                // Coin body (short cylinder approximated as octagonal boxes)
                AddBoxToSurfaceTool(surfaceTool,
                    new Vector3(pileX + offsetX, coinY, pileZ + offsetZ),
                    new Vector3(coinSize * 2f, coinThickness, coinSize * 2f));

                // Embossed face detail on top coins (raised center)
                if (c == coinsInPile - 1 || rng.Randf() > 0.6f)
                {
                    // Center emboss (profile/design)
                    AddSphereToSurfaceTool(surfaceTool,
                        new Vector3(pileX + offsetX, coinY + coinThickness * 0.6f, pileZ + offsetZ),
                        coinSize * 0.4f);

                    // Rim detail (raised edge)
                    float rimThickness = coinSize * 0.08f;
                    for (int r = 0; r < 8; r++)
                    {
                        float rimAngle = r * Mathf.Tau / 8f;
                        float rimX = Mathf.Cos(rimAngle) * coinSize * 0.85f;
                        float rimZ = Mathf.Sin(rimAngle) * coinSize * 0.85f;
                        AddBoxToSurfaceTool(surfaceTool,
                            new Vector3(pileX + offsetX + rimX, coinY + coinThickness * 0.55f, pileZ + offsetZ + rimZ),
                            new Vector3(rimThickness * 2f, coinThickness * 0.3f, rimThickness * 2f));
                    }
                }
            }
        }

        // === SCATTERED FLAT COINS (individual coins around piles) ===
        int scatteredCoins = rng.RandiRange(8, 14);
        for (int i = 0; i < scatteredCoins; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.3f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;

            // Varied coin sizes
            int sizeType = rng.RandiRange(0, 2);
            float coinR = sizeType switch
            {
                0 => rng.RandfRange(0.015f, 0.02f) * s,  // Small
                1 => rng.RandfRange(0.022f, 0.028f) * s, // Medium
                _ => rng.RandfRange(0.03f, 0.038f) * s   // Large
            };
            float coinThick = 0.004f * s;

            // Some coins on edge or tilted
            bool onEdge = rng.Randf() > 0.8f;
            bool tilted = !onEdge && rng.Randf() > 0.7f;

            if (onEdge)
            {
                // Coin standing on edge (rotated 90 degrees)
                float edgeAngle = rng.Randf() * Mathf.Tau;
                AddBoxToSurfaceTool(surfaceTool,
                    new Vector3(x, coinR, z),
                    new Vector3(coinThick, coinR * 2f, coinR * 2f));
            }
            else if (tilted)
            {
                // Tilted coin (leaning against something)
                // Approximate with angled box
                float tiltHeight = coinR * 0.6f;
                AddBoxToSurfaceTool(surfaceTool,
                    new Vector3(x, tiltHeight, z),
                    new Vector3(coinR * 1.8f, coinThick * 1.5f, coinR * 1.8f));
                // Add support wedge
                AddBoxToSurfaceTool(surfaceTool,
                    new Vector3(x, tiltHeight * 0.3f, z + coinR * 0.3f),
                    new Vector3(coinR * 0.8f, tiltHeight * 0.5f, coinThick * 2f));
            }
            else
            {
                // Flat coin
                AddBoxToSurfaceTool(surfaceTool,
                    new Vector3(x, coinThick * 0.5f, z),
                    new Vector3(coinR * 2f, coinThick, coinR * 2f));

                // Embossed detail on some flat coins
                if (rng.Randf() > 0.5f)
                {
                    AddSphereToSurfaceTool(surfaceTool,
                        new Vector3(x, coinThick * 1.1f, z),
                        coinR * 0.35f);
                }
            }
        }

        // === GLINTING HIGHLIGHT COINS (special coins with raised detail) ===
        int glintCoins = rng.RandiRange(2, 4);
        for (int i = 0; i < glintCoins; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.1f, 0.25f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float coinR = rng.RandfRange(0.028f, 0.035f) * s;
            float coinThick = 0.005f * s;

            // Main coin body
            AddBoxToSurfaceTool(surfaceTool,
                new Vector3(x, coinThick * 0.5f, z),
                new Vector3(coinR * 2f, coinThick, coinR * 2f));

            // Prominent embossed face (like a royal seal)
            AddSphereToSurfaceTool(surfaceTool,
                new Vector3(x, coinThick * 1.2f, z),
                coinR * 0.5f);

            // Crown/star detail on top
            for (int p = 0; p < 5; p++)
            {
                float pointAngle = p * Mathf.Tau / 5f;
                float pointX = Mathf.Cos(pointAngle) * coinR * 0.35f;
                float pointZ = Mathf.Sin(pointAngle) * coinR * 0.35f;
                AddBoxToSurfaceTool(surfaceTool,
                    new Vector3(x + pointX, coinThick * 1.3f, z + pointZ),
                    new Vector3(coinR * 0.1f, coinThick * 0.4f, coinR * 0.1f));
            }
        }

        // === SMALL SCATTERED BITS (broken coin fragments) ===
        int fragments = rng.RandiRange(3, 6);
        for (int i = 0; i < fragments; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.15f, 0.35f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float fragSize = rng.RandfRange(0.006f, 0.012f) * s;

            AddBoxToSurfaceTool(surfaceTool,
                new Vector3(x, fragSize * 0.5f, z),
                new Vector3(fragSize * rng.RandfRange(0.8f, 1.5f), fragSize * 0.5f, fragSize * rng.RandfRange(0.8f, 1.5f)));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        // Gold material with metallic sheen
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.7f, 0.2f), // Gold
            Roughness = 0.25f,
            Metallic = 0.85f,
            // Add slight emission for glinting effect
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.3f, 0.1f) * 0.15f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateForgottenShieldMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // === MAIN SHIELD BODY - round shield with slight warp/damage ===
        int segments = 14; // More segments for better shape
        float radius = 0.32f * s;
        float shieldY = 0.015f * s;

        // Slight tilt to show it's lying damaged
        float tiltX = rng.RandfRange(-0.05f, 0.05f);
        float tiltZ = rng.RandfRange(-0.03f, 0.03f);

        // Main shield face with bent/warped sections
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;

            // Warp some edges to show damage
            float warp1 = (i == 3 || i == 4) ? -0.015f * s : 0;
            float warp2 = (i == 4 || i == 5) ? -0.012f * s : 0;
            float edgeWarp1 = (i == 8 || i == 9) ? 0.02f * s : 0;
            float edgeWarp2 = (i == 9 || i == 10) ? 0.018f * s : 0;

            float y1 = shieldY + warp1 + Mathf.Cos(angle1) * tiltX + Mathf.Sin(angle1) * tiltZ;
            float y2 = shieldY + warp2 + Mathf.Cos(angle2) * tiltX + Mathf.Sin(angle2) * tiltZ;
            float edgeY1 = shieldY + edgeWarp1 + Mathf.Cos(angle1) * tiltX * 2 + Mathf.Sin(angle1) * tiltZ * 2;
            float edgeY2 = shieldY + edgeWarp2 + Mathf.Cos(angle2) * tiltX * 2 + Mathf.Sin(angle2) * tiltZ * 2;

            // Inner section (near center)
            float innerR = radius * 0.4f;
            surfaceTool.AddVertex(new Vector3(0, shieldY + 0.005f * s, 0));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * innerR, y1, Mathf.Sin(angle1) * innerR));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * innerR, y2, Mathf.Sin(angle2) * innerR));

            // Outer section (with bent edges)
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * innerR, y1, Mathf.Sin(angle1) * innerR));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * radius, edgeY1, Mathf.Sin(angle1) * radius));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * radius, edgeY2, Mathf.Sin(angle2) * radius));

            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * innerR, y1, Mathf.Sin(angle1) * innerR));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * radius, edgeY2, Mathf.Sin(angle2) * radius));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * innerR, y2, Mathf.Sin(angle2) * innerR));
        }

        // === SHIELD BOSS (center) with damage ===
        float bossY = 0.04f * s;
        float bossR = 0.07f * s;
        for (int i = 0; i < 8; i++)
        {
            float angle1 = i * Mathf.Tau / 8;
            float angle2 = (i + 1) * Mathf.Tau / 8;
            // Boss dome
            surfaceTool.AddVertex(new Vector3(0, bossY + 0.025f * s, 0));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * bossR, bossY, Mathf.Sin(angle1) * bossR));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * bossR, bossY, Mathf.Sin(angle2) * bossR));
        }
        // Boss rim
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(Mathf.Cos(angle) * bossR * 1.1f, bossY - 0.005f * s, Mathf.Sin(angle) * bossR * 1.1f), new Vector3(0.02f * s, 0.015f * s, 0.02f * s));
        }

        // === DENTS AND BATTLE DAMAGE ===
        int dentCount = rng.RandiRange(3, 5);
        for (int d = 0; d < dentCount; d++)
        {
            float dentAngle = rng.Randf() * Mathf.Tau;
            float dentDist = rng.RandfRange(0.08f, 0.22f) * s;
            float dentX = Mathf.Cos(dentAngle) * dentDist;
            float dentZ = Mathf.Sin(dentAngle) * dentDist;
            float dentSize = rng.RandfRange(0.02f, 0.04f) * s;
            float dentDepth = rng.RandfRange(0.005f, 0.012f) * s;

            // Dent as recessed disc (inverted dome)
            for (int dd = 0; dd < 5; dd++)
            {
                float da1 = dd * Mathf.Tau / 5;
                float da2 = (dd + 1) * Mathf.Tau / 5;
                surfaceTool.AddVertex(new Vector3(dentX, shieldY - dentDepth, dentZ));
                surfaceTool.AddVertex(new Vector3(dentX + Mathf.Cos(da1) * dentSize, shieldY + 0.002f * s, dentZ + Mathf.Sin(da1) * dentSize));
                surfaceTool.AddVertex(new Vector3(dentX + Mathf.Cos(da2) * dentSize, shieldY + 0.002f * s, dentZ + Mathf.Sin(da2) * dentSize));
            }
        }

        // === METAL RIM with damage ===
        float rimY = 0.02f * s;
        float rimWidth = 0.025f * s;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.Tau / segments;

            // Skip some segments for broken rim
            if (i == 5) continue; // Gap in rim

            float x = Mathf.Cos(angle) * (radius - rimWidth / 2);
            float z = Mathf.Sin(angle) * (radius - rimWidth / 2);

            // Bent rim segments
            float bendY = (i == 4 || i == 6) ? -0.008f * s : 0;
            float bendOut = (i == 9) ? 0.01f * s : 0;

            AddBoxToSurfaceTool(surfaceTool, new Vector3(x + Mathf.Cos(angle) * bendOut, rimY + bendY, z + Mathf.Sin(angle) * bendOut), new Vector3(0.035f * s, 0.018f * s, 0.035f * s));
        }

        // Rivets on rim
        int rivetCount = 8;
        for (int r = 0; r < rivetCount; r++)
        {
            float rivetAngle = r * Mathf.Tau / rivetCount + 0.15f;
            float rivetX = Mathf.Cos(rivetAngle) * (radius - rimWidth);
            float rivetZ = Mathf.Sin(rivetAngle) * (radius - rimWidth);

            // Skip rivets near damage
            if (r == 2 || r == 3) continue;

            AddSphereToSurfaceTool(surfaceTool, new Vector3(rivetX, rimY + 0.01f * s, rivetZ), 0.008f * s);
        }

        // === FADED HERALDRY - simple cross/chevron design ===
        float heraldY = shieldY + 0.003f * s;

        // Main vertical stripe (faded by using thin raised lines)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, heraldY, 0), new Vector3(0.04f * s, 0.004f * s, radius * 0.7f));

        // Horizontal stripe (cross pattern)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, heraldY, 0), new Vector3(radius * 0.5f, 0.004f * s, 0.035f * s));

        // Chevron points (v-shape near bottom)
        float chevronZ = 0.1f * s;
        float chevronLen = 0.08f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.04f * s, heraldY, chevronZ), new Vector3(chevronLen, 0.003f * s, 0.02f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.04f * s, heraldY, chevronZ), new Vector3(chevronLen, 0.003f * s, 0.02f * s));

        // Faded corner emblems (simple squares representing worn-off design)
        float[] emblemX = { -0.12f * s, 0.12f * s };
        float[] emblemZ = { -0.12f * s, 0.12f * s };
        for (int e = 0; e < 2; e++)
        {
            AddBoxToSurfaceTool(surfaceTool, new Vector3(emblemX[e], heraldY, emblemZ[e]), new Vector3(0.035f * s, 0.003f * s, 0.035f * s));
        }

        // === SCRATCHES AND WEAR ===
        int scratchCount = rng.RandiRange(4, 7);
        for (int sc = 0; sc < scratchCount; sc++)
        {
            float scratchAngle = rng.Randf() * Mathf.Tau;
            float scratchDist = rng.RandfRange(0.05f, 0.25f) * s;
            float scratchX = Mathf.Cos(scratchAngle) * scratchDist;
            float scratchZ = Mathf.Sin(scratchAngle) * scratchDist;
            float scratchLen = rng.RandfRange(0.03f, 0.08f) * s;
            float scratchDir = rng.Randf() * Mathf.Tau;

            float dx = Mathf.Cos(scratchDir) * scratchLen / 2;
            float dz = Mathf.Sin(scratchDir) * scratchLen / 2;

            // Scratch as thin groove
            surfaceTool.AddVertex(new Vector3(scratchX - dx, shieldY + 0.004f * s, scratchZ - dz));
            surfaceTool.AddVertex(new Vector3(scratchX + dx, shieldY + 0.004f * s, scratchZ + dz));
            surfaceTool.AddVertex(new Vector3(scratchX, shieldY + 0.002f * s, scratchZ));
        }

        // === LEATHER STRAP REMNANTS (broken arm straps) ===
        float strapX = -0.08f * s;
        float strapZ = 0;
        float strapLen = 0.1f * s;
        // One intact strap
        AddBoxToSurfaceTool(surfaceTool, new Vector3(strapX, shieldY + 0.015f * s, strapZ), new Vector3(0.025f * s, 0.012f * s, strapLen));
        // Broken strap end dangling
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.06f * s, shieldY + 0.008f * s, 0.03f * s), new Vector3(0.02f * s, 0.008f * s, 0.04f * s));

        // === DIRT AND STAINS ===
        int stainCount = rng.RandiRange(2, 4);
        for (int st = 0; st < stainCount; st++)
        {
            float stainAngle = rng.Randf() * Mathf.Tau;
            float stainDist = rng.RandfRange(0.04f, 0.2f) * s;
            float stainX = Mathf.Cos(stainAngle) * stainDist;
            float stainZ = Mathf.Sin(stainAngle) * stainDist;
            float stainR = rng.RandfRange(0.015f, 0.04f) * s;

            for (int ss = 0; ss < 4; ss++)
            {
                float sa1 = ss * Mathf.Tau / 4;
                float sa2 = (ss + 1) * Mathf.Tau / 4;
                surfaceTool.AddVertex(new Vector3(stainX, shieldY + 0.002f * s, stainZ));
                surfaceTool.AddVertex(new Vector3(stainX + Mathf.Cos(sa1) * stainR, shieldY + 0.001f * s, stainZ + Mathf.Sin(sa1) * stainR));
                surfaceTool.AddVertex(new Vector3(stainX + Mathf.Cos(sa2) * stainR, shieldY + 0.001f * s, stainZ + Mathf.Sin(sa2) * stainR));
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.42f, 0.35f, 0.28f), // Weathered wood/leather
            Roughness = 0.85f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateMoldyBreadMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Bread loaf (squashed sphere shape)
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, 0.05f * s, 0), 0.08f * s);
        // Flatten top a bit by adding more geometry
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.03f * s, 0), new Vector3(0.12f * s, 0.04f * s, 0.08f * s));

        // Mold spots (small green spheres)
        for (int i = 0; i < 4; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float x = Mathf.Cos(angle) * 0.04f * s;
            float z = Mathf.Sin(angle) * 0.04f * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, 0.07f * s, z), 0.015f * s);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.5f, 0.4f), // Stale bread
            Roughness = 0.95f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    // === GLOWING FUNGI AND LICHEN MESH CREATION ===

    private ArrayMesh CreateLichenPatchMesh(RandomNumberGenerator rng, Color baseColor)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Create irregular lichen patches - flat organic shapes on surfaces
        int patchCount = rng.RandiRange(5, 10);
        for (int i = 0; i < patchCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.25f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float patchSize = rng.RandfRange(0.05f, 0.12f) * s;

            // Create irregular polygon for each patch
            int segments = rng.RandiRange(5, 8);
            Vector3 center = new Vector3(x, 0.01f * s, z);
            for (int j = 0; j < segments; j++)
            {
                float a1 = j * Mathf.Tau / segments;
                float a2 = (j + 1) * Mathf.Tau / segments;
                float r1 = patchSize * (0.7f + rng.Randf() * 0.6f);
                float r2 = patchSize * (0.7f + rng.Randf() * 0.6f);

                surfaceTool.SetNormal(Vector3.Up);
                surfaceTool.AddVertex(center);
                surfaceTool.AddVertex(center + new Vector3(Mathf.Cos(a1) * r1, 0, Mathf.Sin(a1) * r1));
                surfaceTool.AddVertex(center + new Vector3(Mathf.Cos(a2) * r2, 0, Mathf.Sin(a2) * r2));
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = baseColor,
            EmissionEnabled = true,
            Emission = baseColor.Lightened(0.2f),
            EmissionEnergyMultiplier = 2.0f,
            Roughness = 0.8f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateWallFungusMesh(RandomNumberGenerator rng, Color baseColor)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Create shelf-like fungi that grow from walls
        int fungusCount = rng.RandiRange(3, 6);
        for (int i = 0; i < fungusCount; i++)
        {
            float xOff = rng.RandfRange(-0.15f, 0.15f) * s;
            float yOff = rng.RandfRange(0f, 0.3f) * s;
            float zOff = rng.RandfRange(-0.1f, 0.1f) * s;
            float shelfWidth = rng.RandfRange(0.08f, 0.15f) * s;
            float shelfDepth = rng.RandfRange(0.05f, 0.1f) * s;
            float shelfThick = rng.RandfRange(0.02f, 0.04f) * s;

            // Shelf fungus - semicircular shape
            Vector3 basePos = new Vector3(xOff, yOff, zOff);
            int segments = 6;
            for (int j = 0; j < segments; j++)
            {
                float a1 = (j * Mathf.Pi / segments) - Mathf.Pi / 2;
                float a2 = ((j + 1) * Mathf.Pi / segments) - Mathf.Pi / 2;

                // Top surface
                Vector3 c = basePos + new Vector3(0, shelfThick, 0);
                Vector3 p1 = basePos + new Vector3(Mathf.Cos(a1) * shelfWidth, shelfThick, Mathf.Sin(a1) * shelfDepth);
                Vector3 p2 = basePos + new Vector3(Mathf.Cos(a2) * shelfWidth, shelfThick, Mathf.Sin(a2) * shelfDepth);
                surfaceTool.SetNormal(Vector3.Up);
                surfaceTool.AddVertex(c);
                surfaceTool.AddVertex(p1);
                surfaceTool.AddVertex(p2);

                // Bottom surface (underside)
                Vector3 b1 = basePos + new Vector3(Mathf.Cos(a1) * shelfWidth, 0, Mathf.Sin(a1) * shelfDepth);
                Vector3 b2 = basePos + new Vector3(Mathf.Cos(a2) * shelfWidth, 0, Mathf.Sin(a2) * shelfDepth);
                surfaceTool.SetNormal(Vector3.Down);
                surfaceTool.AddVertex(basePos);
                surfaceTool.AddVertex(b2);
                surfaceTool.AddVertex(b1);

                // Edge
                surfaceTool.SetNormal((p1 - basePos).Normalized());
                surfaceTool.AddVertex(p1);
                surfaceTool.AddVertex(b1);
                surfaceTool.AddVertex(b2);
                surfaceTool.AddVertex(p1);
                surfaceTool.AddVertex(b2);
                surfaceTool.AddVertex(p2);
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = baseColor,
            EmissionEnabled = true,
            Emission = baseColor.Lightened(0.3f),
            EmissionEnergyMultiplier = 1.8f,
            Roughness = 0.6f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateCeilingTendrilsMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Create hanging tendrils/roots from ceiling
        int tendrilCount = rng.RandiRange(5, 10);
        for (int i = 0; i < tendrilCount; i++)
        {
            float xOff = rng.RandfRange(-0.2f, 0.2f) * s;
            float zOff = rng.RandfRange(-0.2f, 0.2f) * s;
            float length = rng.RandfRange(0.3f, 0.8f) * s;
            float thickness = rng.RandfRange(0.015f, 0.03f) * s;

            // Create tendril as series of boxes going down with slight curves
            int segments = rng.RandiRange(3, 5);
            float segLen = length / segments;
            float curX = xOff, curZ = zOff;

            for (int j = 0; j < segments; j++)
            {
                float y = -j * segLen;
                float nextY = -(j + 1) * segLen;

                // Add slight curve
                curX += rng.RandfRange(-0.03f, 0.03f) * s;
                curZ += rng.RandfRange(-0.03f, 0.03f) * s;

                // Taper thickness
                float t = thickness * (1f - j * 0.15f);
                AddBoxToSurfaceTool(surfaceTool, new Vector3(curX, (y + nextY) / 2, curZ),
                    new Vector3(t, segLen, t));

                // Add glowing tip at the end
                if (j == segments - 1)
                {
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(curX, nextY, curZ), t * 1.5f);
                }
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.7f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.95f, 0.5f),
            EmissionEnergyMultiplier = 2.0f,
            Roughness = 0.7f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateFloorSporesMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Create cluster of glowing spore pods on the floor
        int sporeCount = rng.RandiRange(8, 15);
        for (int i = 0; i < sporeCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.02f, 0.2f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float size = rng.RandfRange(0.02f, 0.05f) * s;

            // Small glowing spheres (spore pods)
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, size, z), size);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.7f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.9f, 0.3f),
            EmissionEnergyMultiplier = 2.5f,
            Roughness = 0.3f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateBioluminescentVeinsMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Create branching vein-like patterns
        int veinCount = rng.RandiRange(3, 5);
        for (int v = 0; v < veinCount; v++)
        {
            float startAngle = v * Mathf.Tau / veinCount + rng.Randf() * 0.3f;
            float x = 0, z = 0;
            float angle = startAngle;
            float thickness = 0.02f * s;

            int segments = rng.RandiRange(4, 7);
            for (int i = 0; i < segments; i++)
            {
                float length = rng.RandfRange(0.08f, 0.15f) * s;
                float nextX = x + Mathf.Cos(angle) * length;
                float nextZ = z + Mathf.Sin(angle) * length;

                // Draw vein segment as thin box
                float midX = (x + nextX) / 2;
                float midZ = (z + nextZ) / 2;
                float segLen = new Vector2(nextX - x, nextZ - z).Length();

                // Rotate box to align with vein direction
                AddBoxToSurfaceTool(surfaceTool, new Vector3(midX, 0.01f * s, midZ),
                    new Vector3(thickness, 0.01f * s, segLen));

                x = nextX;
                z = nextZ;
                angle += rng.RandfRange(-0.5f, 0.5f);
                thickness *= 0.85f; // Taper

                // Branch occasionally
                if (rng.Randf() < 0.3f && i > 0)
                {
                    float branchAngle = angle + (rng.Randf() > 0.5f ? 0.8f : -0.8f);
                    float bLen = rng.RandfRange(0.05f, 0.1f) * s;
                    float bx = x + Mathf.Cos(branchAngle) * bLen;
                    float bz = z + Mathf.Sin(branchAngle) * bLen;
                    AddBoxToSurfaceTool(surfaceTool, new Vector3((x + bx) / 2, 0.01f * s, (z + bz) / 2),
                        new Vector3(thickness * 0.7f, 0.01f * s, bLen));
                }
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.8f, 0.6f),
            EmissionEnabled = true,
            Emission = new Color(0.3f, 1f, 0.8f),
            EmissionEnergyMultiplier = 2.2f,
            Roughness = 0.4f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    // Static counter for shadow-enabled lights (for performance)
    private static int _shadowLightCount;
    private const int MaxShadowLights = 6; // Only allow 6 prop lights to cast shadows

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = LightColor;
        _light.LightEnergy = Constants.TorchEnergy;
        _light.OmniRange = LightRadius;

        // PERFORMANCE: Only enable shadows on a limited number of prop lights
        // Most lights should not cast shadows as they're very expensive
        _light.ShadowEnabled = _shadowLightCount < MaxShadowLights;
        if (_light.ShadowEnabled) _shadowLightCount++;

        _light.OmniAttenuation = 1.5f;

        // PERFORMANCE: Set distance fade for lights
        _light.DistanceFadeEnabled = true;
        _light.DistanceFadeBegin = Constants.TorchVisibilityEnd * 0.7f;
        _light.DistanceFadeLength = Constants.TorchVisibilityEnd * 0.3f;
        _light.DistanceFadeShadow = Constants.TorchVisibilityEnd * 0.5f;

        // Position light above the prop
        float lightHeight = ShapeType == "torch" ? 0.5f : 0.8f;
        _light.Position = new Vector3(0, lightHeight * PropScale, 0);

        _baseLightEnergy = _light.LightEnergy;
        AddChild(_light);
    }

    /// <summary>
    /// Static factory method to create a cosmetic from configuration
    /// </summary>
    public static Cosmetic3D Create(string shapeType, Color primary, Color secondary, Color accent,
        float variationSeed = 0f, float propScale = 1f, bool hasCollision = true)
    {
        var cosmetic = new Cosmetic3D();
        cosmetic.ShapeType = shapeType;
        cosmetic.PrimaryColor = primary;
        cosmetic.SecondaryColor = secondary;
        cosmetic.AccentColor = accent;
        cosmetic.VariationSeed = variationSeed;
        cosmetic.PropScale = propScale;
        cosmetic.HasCollision = hasCollision;
        return cosmetic;
    }
}
