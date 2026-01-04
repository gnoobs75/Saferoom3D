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
        var boneColor = new Color(0.9f, 0.85f, 0.7f);

        // Skull in center
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, 0.15f * s, 0), 0.12f * s);
        // Eye sockets (small indents simulated by darker spheres overlaid in material)

        // Scattered bones (cylinders for long bones)
        for (int i = 0; i < 8; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.1f, 0.4f) * s;
            float boneLen = rng.RandfRange(0.15f, 0.3f) * s;
            float boneRad = rng.RandfRange(0.02f, 0.04f) * s;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, boneRad, Mathf.Sin(angle) * dist);
            // Simple bone as elongated box
            float rotAngle = rng.Randf() * Mathf.Tau;
            AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(boneLen, boneRad * 2, boneRad * 2));
        }

        // Rib cage pieces
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 0.08f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.08f * s, -0.15f * s), new Vector3(0.02f * s, 0.12f * s, 0.02f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D { AlbedoColor = boneColor, Roughness = 0.9f };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateCoiledChainsMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Create chain links as connected tori (simplified as small cylinders)
        int links = 12;
        for (int i = 0; i < links; i++)
        {
            float angle = i * 0.5f + rng.Randf() * 0.2f;
            float radius = 0.15f * s + i * 0.015f * s;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = 0.02f * s + (i % 2) * 0.02f * s;
            // Link as small elongated box
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z), new Vector3(0.05f * s, 0.02f * s, 0.03f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.32f, 0.28f), // Rusted iron
            Roughness = 0.8f,
            Metallic = 0.6f
        };
        mesh.SurfaceSetMaterial(0, material);
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
        // Base cylinder section (lying down)
        int segments = 8;
        float radius = 0.35f * s;
        float height = 0.5f * s;

        // Create partial barrel (broken, so only 2/3 of it)
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;
            // Skip some segments for broken look
            if (i == 2 || i == 3) continue;

            float y1 = Mathf.Cos(angle1) * radius;
            float z1 = Mathf.Sin(angle1) * radius;
            float y2 = Mathf.Cos(angle2) * radius;
            float z2 = Mathf.Sin(angle2) * radius;

            // Side panel
            surfaceTool.AddVertex(new Vector3(-height/2, y1, z1));
            surfaceTool.AddVertex(new Vector3(height/2, y1, z1));
            surfaceTool.AddVertex(new Vector3(height/2, y2, z2));

            surfaceTool.AddVertex(new Vector3(-height/2, y1, z1));
            surfaceTool.AddVertex(new Vector3(height/2, y2, z2));
            surfaceTool.AddVertex(new Vector3(-height/2, y2, z2));
        }

        // Liquid pool beneath
        for (int i = 0; i < 6; i++)
        {
            float angle1 = i * Mathf.Tau / 6;
            float angle2 = (i + 1) * Mathf.Tau / 6;
            float r = 0.3f * s;
            surfaceTool.AddVertex(new Vector3(0.2f * s, 0.01f, 0));
            surfaceTool.AddVertex(new Vector3(0.2f * s + Mathf.Cos(angle1) * r, 0.01f, Mathf.Sin(angle1) * r));
            surfaceTool.AddVertex(new Vector3(0.2f * s + Mathf.Cos(angle2) * r, 0.01f, Mathf.Sin(angle2) * r));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateWoodMaterial(new Color(0.5f, 0.35f, 0.2f));
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateAltarStoneMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Main altar slab with beveled edges
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.25f * s, 0), new Vector3(1.2f * s, 0.5f * s, 0.7f * s));

        // Base pedestal (wider at bottom)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, 0), new Vector3(1.3f * s, 0.04f * s, 0.8f * s));

        // Ritual circle groove
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12f;
            float circleR = 0.35f * s;
            float x = Mathf.Cos(angle) * circleR;
            float z = Mathf.Sin(angle) * circleR;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.505f * s, z), new Vector3(0.05f * s, 0.01f * s, 0.02f * s));
        }

        // Rune grooves (cross pattern)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.505f * s, 0), new Vector3(0.9f * s, 0.01f * s, 0.025f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.505f * s, 0), new Vector3(0.025f * s, 0.01f * s, 0.55f * s));

        // Corner candle holders
        float[] cornerX = { -0.5f * s, 0.5f * s, -0.5f * s, 0.5f * s };
        float[] cornerZ = { -0.3f * s, -0.3f * s, 0.3f * s, 0.3f * s };
        for (int i = 0; i < 4; i++)
        {
            // Holder base
            AddBoxToSurfaceTool(surfaceTool, new Vector3(cornerX[i], 0.52f * s, cornerZ[i]), new Vector3(0.06f * s, 0.02f * s, 0.06f * s));
            // Candle
            AddBoxToSurfaceTool(surfaceTool, new Vector3(cornerX[i], 0.58f * s, cornerZ[i]), new Vector3(0.025f * s, 0.1f * s, 0.025f * s));
        }

        // Blood stains (asymmetric for realism)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.08f * s, 0.505f * s, 0.05f * s), new Vector3(0.18f * s, 0.01f * s, 0.15f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.12f * s, 0.505f * s, -0.08f * s), new Vector3(0.1f * s, 0.01f * s, 0.08f * s));

        // Offering bowl in center
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, 0.54f * s, 0), 0.08f * s);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.28f, 0.26f),
            Roughness = 0.85f
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

        // Irregular blood pool
        int segments = 8;
        Vector3 center = Vector3.Zero;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;
            float r1 = (0.2f + rng.Randf() * 0.15f) * s;
            float r2 = (0.2f + rng.Randf() * 0.15f) * s;
            surfaceTool.AddVertex(center);
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * r1, 0, Mathf.Sin(angle1) * r1));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r2, 0, Mathf.Sin(angle2) * r2));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.05f, 0.05f),
            Roughness = 0.3f,
            Metallic = 0.2f
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

        // Blade (lying flat)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, 0), new Vector3(0.04f * s, 0.02f * s, 0.6f * s));
        // Crossguard
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, -0.25f * s), new Vector3(0.15f * s, 0.03f * s, 0.03f * s));
        // Handle
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, -0.35f * s), new Vector3(0.03f * s, 0.03f * s, 0.12f * s));

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.48f, 0.45f), // Rusty metal
            Roughness = 0.7f,
            Metallic = 0.5f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateShatteredPotionMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Glass shards scattered
        for (int i = 0; i < 6; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.15f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            // Shard as small triangle
            Vector3 base1 = new Vector3(x, 0.01f, z);
            Vector3 base2 = new Vector3(x + 0.03f * s, 0.01f, z + 0.02f * s);
            Vector3 top = new Vector3(x + 0.01f * s, 0.04f * s, z + 0.01f * s);
            surfaceTool.AddVertex(base1); surfaceTool.AddVertex(base2); surfaceTool.AddVertex(top);
        }

        // Glowing residue pool
        for (int i = 0; i < 6; i++)
        {
            float angle1 = i * Mathf.Tau / 6;
            float angle2 = (i + 1) * Mathf.Tau / 6;
            float r = 0.1f * s;
            surfaceTool.AddVertex(Vector3.Zero);
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * r, 0.005f, Mathf.Sin(angle1) * r));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r, 0.005f, Mathf.Sin(angle2) * r));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.8f, 0.3f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0.9f, 0.2f),
            EmissionEnergyMultiplier = 1f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateAncientScrollMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Main scroll body (partially unrolled)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.01f * s, 0), new Vector3(0.4f * s, 0.01f * s, 0.3f * s));
        // Rolled ends
        AddSphereToSurfaceTool(surfaceTool, new Vector3(-0.18f * s, 0.025f * s, 0), 0.03f * s);
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0.18f * s, 0.025f * s, 0), 0.03f * s);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.8f, 0.65f), // Aged parchment
            Roughness = 0.9f
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

        // Messy pile of twigs, cloth, bones
        int items = rng.RandiRange(12, 18);
        for (int i = 0; i < items; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0f, 0.25f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.01f, 0.15f) * s;
            float itemLen = rng.RandfRange(0.05f, 0.12f) * s;
            float itemW = rng.RandfRange(0.01f, 0.03f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z), new Vector3(itemLen, itemW, itemW));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.35f, 0.28f), // Dirty brown
            Roughness = 0.95f
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

        // Gold and silver coins scattered
        int coins = rng.RandiRange(8, 15);
        for (int i = 0; i < coins; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.02f, 0.25f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float coinR = rng.RandfRange(0.02f, 0.035f) * s;
            // Coin as flat cylinder (simplified as very short box)
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.005f * s, z), new Vector3(coinR * 2, 0.005f * s, coinR * 2));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.7f, 0.2f), // Gold
            Roughness = 0.3f,
            Metallic = 0.8f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateForgottenShieldMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // Shield lying flat (round shield)
        int segments = 10;
        float radius = 0.3f * s;
        Vector3 center = new Vector3(0, 0.02f * s, 0);
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;
            surfaceTool.AddVertex(center);
            surfaceTool.AddVertex(center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius));
            surfaceTool.AddVertex(center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius));
        }

        // Boss (center bump)
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, 0.05f * s, 0), 0.08f * s);

        // Metal rim
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            float x = Mathf.Cos(angle) * (radius - 0.02f * s);
            float z = Mathf.Sin(angle) * (radius - 0.02f * s);
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.025f * s, z), new Vector3(0.04f * s, 0.02f * s, 0.04f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.4f, 0.3f), // Worn wood
            Roughness = 0.8f
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
