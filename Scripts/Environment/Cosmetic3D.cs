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

            case "floor_grate":
                mesh = CreateFloorGrateMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(1.5f * s, 0.1f, 1.5f * s)
                };
                HasCollision = false; // Walk-through grate
                meshOffset = new Vector3(0, 0.02f * s, 0);
                break;

            case "drain_cover":
                mesh = CreateDrainCoverMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.4f * s,
                    Height = 0.08f * s
                };
                HasCollision = false;
                meshOffset = new Vector3(0, 0.02f * s, 0);
                break;

            case "ceiling_beam":
                mesh = CreateCeilingBeamMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(0.4f * s, 0.5f * s, 6f * s)
                };
                HasCollision = false; // Ceiling beams don't block
                meshOffset = new Vector3(0, 0, 0);
                break;

            case "ceiling_arch":
                mesh = CreateCeilingArchMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 0, 0);
                break;

            case "wall_buttress":
                mesh = CreateWallButtressMesh(rng);
                collision = new BoxShape3D
                {
                    Size = new Vector3(0.8f * s, 3f * s, 0.5f * s)
                };
                meshOffset = new Vector3(0, 1.5f * s, 0);
                break;

            case "wall_column":
                mesh = CreateWallColumnMesh(rng);
                collision = new CylinderShape3D
                {
                    Radius = 0.35f * s,
                    Height = Constants.WallHeight * s
                };
                meshOffset = new Vector3(0, Constants.WallHeight * s / 2f, 0);
                break;

            case "wall_sconce":
                mesh = CreateWallSconceMesh(rng);
                collision = null;
                HasCollision = false;
                meshOffset = new Vector3(0, 2.5f * s, 0);
                HasLighting = true;
                LightColor = new Color(1f, 0.7f, 0.4f);
                LightRadius = 6f;
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

        int segments = 16;
        float angleStep = Mathf.Tau / segments;

        // Wood plank color variations for natural wood look
        Color[] woodColors = {
            new Color(0.5f, 0.32f, 0.18f),   // Medium oak
            new Color(0.42f, 0.27f, 0.14f),  // Dark oak
            new Color(0.55f, 0.38f, 0.22f),  // Light oak
        };
        Color ironBandColor = new Color(0.25f, 0.25f, 0.28f);  // Dark iron
        Color rivetColor = new Color(0.4f, 0.35f, 0.3f);       // Weathered bronze rivets
        Color spigotColor = new Color(0.6f, 0.45f, 0.25f);     // Brass spigot

        // Create barrel body with bulge, wood plank gaps, and wear marks
        int heightSegments = 10;
        int numPlanks = 8;
        float plankAngle = Mathf.Tau / numPlanks;

        for (int h = 0; h <= heightSegments; h++)
        {
            float t = (float)h / heightSegments;
            float y = -height / 2f + t * height;

            float bulge = 1f + 0.15f * Mathf.Sin(t * Mathf.Pi);
            float currentRadius = radius * bulge;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;

                // Wood grain plank effect - slight inset between planks
                float plankPos = (angle % plankAngle) / plankAngle;
                float plankGap = (plankPos < 0.05f || plankPos > 0.95f) ? 0.008f * PropScale : 0f;

                // Subtle wear marks
                float wearMark = rng.Randf() * 0.005f * PropScale * Mathf.Sin(angle * 7 + t * 5);

                float r = currentRadius - plankGap - wearMark;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                // Set vertex color based on which plank this vertex belongs to
                int plankIndex = (int)(angle / plankAngle) % numPlanks;
                Color woodColor = woodColors[plankIndex % woodColors.Length];
                // Add slight variation within each plank
                float colorVariation = rng.RandfRange(-0.03f, 0.03f);
                woodColor = new Color(
                    Mathf.Clamp(woodColor.R + colorVariation, 0, 1),
                    Mathf.Clamp(woodColor.G + colorVariation * 0.7f, 0, 1),
                    Mathf.Clamp(woodColor.B + colorVariation * 0.5f, 0, 1)
                );

                surfaceTool.SetColor(woodColor);
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

        int currentVertexOffset = (heightSegments + 1) * (segments + 1);

        // Iron bands with rivets (3 bands)
        float[] bandPositions = { 0.15f, 0.5f, 0.85f };
        int rivetsPerBand = 8;

        foreach (float bandT in bandPositions)
        {
            float bandY = -height / 2f + bandT * height;
            float bandHeight = 0.035f * PropScale;
            float bulge = 1f + 0.15f * Mathf.Sin(bandT * Mathf.Pi);
            float bandRadius = radius * bulge * 1.025f;

            int bandStart = currentVertexOffset;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * bandRadius;
                float z = Mathf.Sin(angle) * bandRadius;

                // Iron band color
                surfaceTool.SetColor(ironBandColor);
                surfaceTool.AddVertex(new Vector3(x, bandY - bandHeight / 2, z));
                surfaceTool.SetColor(ironBandColor);
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

            currentVertexOffset += (segments + 1) * 2;

            // Rivets on each band
            float rivetRadius = 0.012f * PropScale;
            for (int rv = 0; rv < rivetsPerBand; rv++)
            {
                float rivetAngle = rv * Mathf.Tau / rivetsPerBand;
                float rivetX = Mathf.Cos(rivetAngle) * (bandRadius + rivetRadius * 0.5f);
                float rivetZ = Mathf.Sin(rivetAngle) * (bandRadius + rivetRadius * 0.5f);
                AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(rivetX, bandY, rivetZ),
                    new Vector3(rivetRadius * 2, rivetRadius * 2, rivetRadius * 2), rivetColor);
            }
        }

        // Tap/spigot on front
        float spigotY = -height * 0.1f;
        float spigotRadius = 0.02f * PropScale;
        float spigotLength = 0.08f * PropScale;
        float bulgeAtSpigot = 1f + 0.15f * Mathf.Sin(0.4f * Mathf.Pi);
        float spigotZ = radius * bulgeAtSpigot + spigotLength / 2;

        // Spigot body (brass)
        AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, spigotY, spigotZ),
            new Vector3(spigotRadius * 2, spigotRadius * 2, spigotLength), spigotColor);
        // Spigot handle (brass)
        AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, spigotY + spigotRadius * 2, spigotZ + spigotLength * 0.3f),
            new Vector3(spigotRadius * 3, spigotRadius * 4, spigotRadius * 1.5f), spigotColor);
        // Spigot base plate (iron)
        AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, spigotY, radius * bulgeAtSpigot + 0.005f * PropScale),
            new Vector3(0.05f * PropScale, 0.05f * PropScale, 0.01f * PropScale), ironBandColor);

        // Barrel top cap
        float topY = height / 2f;
        float topBulge = 1f + 0.15f * Mathf.Sin(Mathf.Pi);
        float topRadius = radius * topBulge * 0.98f;

        // Top cap center vertex with wood color
        Color topWoodColor = woodColors[0].Darkened(0.1f);
        surfaceTool.SetColor(topWoodColor);
        surfaceTool.AddVertex(new Vector3(0, topY, 0));
        int topCenterIdx = currentVertexOffset;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            // Vary top cap color slightly by angle
            int plankIndex = (int)(angle / plankAngle) % numPlanks;
            Color capColor = woodColors[plankIndex % woodColors.Length].Darkened(0.05f);
            surfaceTool.SetColor(capColor);
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle) * topRadius, topY, Mathf.Sin(angle) * topRadius));
        }

        for (int i = 0; i < segments; i++)
        {
            surfaceTool.AddIndex(topCenterIdx);
            surfaceTool.AddIndex(topCenterIdx + 1 + i);
            surfaceTool.AddIndex(topCenterIdx + 2 + i);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        // Use vertex colors for albedo
        var material = new StandardMaterial3D();
        material.VertexColorUseAsAlbedo = true;
        material.Roughness = 0.85f;
        mesh.SurfaceSetMaterial(0, material);

        return mesh;
    }

    private ArrayMesh CreateCrateMesh(RandomNumberGenerator rng)
    {
        float size = Constants.CrateSize * PropScale;
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float bevel = 0.02f * PropScale;
        Vector3 half = new Vector3(size / 2f - bevel, size / 2f - bevel, size / 2f - bevel);

        // Main box
        AddBoxToSurfaceTool(surfaceTool, Vector3.Zero, new Vector3(size - bevel * 2, size - bevel * 2, size - bevel * 2));

        // Wooden planks with gaps between them
        float plankWidth = size * 0.22f;
        float plankThickness = 0.018f * PropScale;
        float spacing = size * 0.28f;
        float gapWidth = 0.008f * PropScale;

        // Front and back faces - vertical planks with gaps
        for (int i = -1; i <= 1; i++)
        {
            float x = i * spacing;
            // Front planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0, half.Z + plankThickness),
                new Vector3(plankWidth - gapWidth, size * 0.88f, plankThickness * 2));
            // Back planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0, -half.Z - plankThickness),
                new Vector3(plankWidth - gapWidth, size * 0.88f, plankThickness * 2));
        }

        // Left and right faces - vertical planks with gaps
        for (int i = -1; i <= 1; i++)
        {
            float z = i * spacing;
            // Right planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(half.X + plankThickness, 0, z),
                new Vector3(plankThickness * 2, size * 0.88f, plankWidth - gapWidth));
            // Left planks
            AddBoxToSurfaceTool(surfaceTool, new Vector3(-half.X - plankThickness, 0, z),
                new Vector3(plankThickness * 2, size * 0.88f, plankWidth - gapWidth));
        }

        // Top planks with gaps
        for (int i = -1; i <= 1; i++)
        {
            float z = i * spacing;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, half.Y + plankThickness, z),
                new Vector3(size * 0.88f, plankThickness * 2, plankWidth - gapWidth));
        }

        // Corner reinforcements (metal brackets) with nail heads
        float bracketSize = size * 0.15f;
        float bracketThickness = 0.022f * PropScale;
        float nailSize = 0.012f * PropScale;

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

            // Nail heads at bracket corners
            AddBoxToSurfaceTool(surfaceTool, corner + new Vector3(-sx * bracketSize * 0.8f, 0, 0),
                new Vector3(nailSize, nailSize, nailSize * 1.5f));
            AddBoxToSurfaceTool(surfaceTool, corner + new Vector3(0, 0, -sz * bracketSize * 0.8f),
                new Vector3(nailSize * 1.5f, nailSize, nailSize));
        }

        // Rope bindings around crate (horizontal)
        float ropeRadius = 0.015f * PropScale;
        float ropeOffset = size * 0.35f;
        for (int ry = -1; ry <= 1; ry += 2)
        {
            float ropeY = ry * ropeOffset;
            // Front rope segment
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, ropeY, half.Z + ropeRadius * 2),
                new Vector3(size * 0.95f, ropeRadius * 2, ropeRadius * 2));
            // Back rope segment
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, ropeY, -half.Z - ropeRadius * 2),
                new Vector3(size * 0.95f, ropeRadius * 2, ropeRadius * 2));
            // Left rope segment
            AddBoxToSurfaceTool(surfaceTool, new Vector3(-half.X - ropeRadius * 2, ropeY, 0),
                new Vector3(ropeRadius * 2, ropeRadius * 2, size * 0.95f));
            // Right rope segment
            AddBoxToSurfaceTool(surfaceTool, new Vector3(half.X + ropeRadius * 2, ropeY, 0),
                new Vector3(ropeRadius * 2, ropeRadius * 2, size * 0.95f));
        }

        // Shipping label/brand on front (rectangular plate)
        float labelWidth = size * 0.25f;
        float labelHeight = size * 0.15f;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, size * 0.1f, half.Z + plankThickness * 3),
            new Vector3(labelWidth, labelHeight, 0.005f * PropScale));

        // Additional nail heads on planks
        float[] nailYPositions = { -size * 0.35f, 0f, size * 0.35f };
        foreach (float nailY in nailYPositions)
        {
            // Front face nails
            AddBoxToSurfaceTool(surfaceTool, new Vector3(-spacing, nailY, half.Z + plankThickness * 2.5f),
                new Vector3(nailSize, nailSize, nailSize));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(spacing, nailY, half.Z + plankThickness * 2.5f),
                new Vector3(nailSize, nailSize, nailSize));
        }

        surfaceTool.GenerateNormals();
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

        int segments = 16;
        float angleStep = Mathf.Tau / segments;
        int heightSegments = 12;

        // Create pot body with clay texture and subtle cracks
        for (int hs = 0; hs <= heightSegments; hs++)
        {
            float t = (float)hs / heightSegments;
            float y = -h / 2f + t * h;
            float radius = Mathf.Lerp(botR, topR, t);

            // Clay texture - irregular surface with handcrafted wobble
            float wobble = 0.025f * PropScale * Mathf.Sin(t * Mathf.Pi * 3);

            // Simulate crack lines - occasional insets
            float crackEffect = 0f;
            if (rng.Randf() < 0.15f)
                crackEffect = 0.008f * PropScale;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float r = radius + wobble * Mathf.Sin(angle * 4) - crackEffect;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

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

        int currentVertexOffset = (heightSegments + 1) * (segments + 1);

        // Decorative bands around pot (2 horizontal bands)
        float[] bandHeights = { 0.3f, 0.7f };
        float bandThickness = 0.02f * PropScale;

        foreach (float bandT in bandHeights)
        {
            float bandY = -h / 2f + bandT * h;
            float bandRadius = Mathf.Lerp(botR, topR, bandT) * 1.05f;
            int bandStart = currentVertexOffset;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * bandRadius;
                float z = Mathf.Sin(angle) * bandRadius;

                surfaceTool.AddVertex(new Vector3(x, bandY - bandThickness, z));
                surfaceTool.AddVertex(new Vector3(x, bandY + bandThickness, z));

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
            currentVertexOffset += (segments + 1) * 2;
        }

        // Decorative rim with carved pattern
        float rimHeight = h * 0.1f;
        float rimTop = h / 2f;
        int rimStart = currentVertexOffset;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            // Carved pattern - wave pattern
            float carveDepth = 0.008f * PropScale * Mathf.Sin(angle * 6);
            float rimR = topR * 1.12f - carveDepth;

            float x = Mathf.Cos(angle) * rimR;
            float z = Mathf.Sin(angle) * rimR;

            surfaceTool.AddVertex(new Vector3(x, rimTop, z));
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
        currentVertexOffset += (segments + 1) * 2;

        // Lid (optional - sits on top)
        float lidRadius = topR * 0.85f;
        float lidHeight = h * 0.08f;
        float lidY = rimTop + lidHeight / 2;

        // Lid body (cylinder simulated)
        surfaceTool.AddVertex(new Vector3(0, lidY + lidHeight / 2, 0));
        int lidCenterIdx = currentVertexOffset;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle) * lidRadius, lidY + lidHeight / 2, Mathf.Sin(angle) * lidRadius));
        }

        for (int i = 0; i < segments; i++)
        {
            surfaceTool.AddIndex(lidCenterIdx);
            surfaceTool.AddIndex(lidCenterIdx + 1 + i);
            surfaceTool.AddIndex(lidCenterIdx + 2 + i);
        }

        // Lid handle (small knob on top)
        float handleRadius = lidRadius * 0.2f;
        float handleY = lidY + lidHeight / 2 + handleRadius;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, handleY, 0),
            new Vector3(handleRadius * 2, handleRadius * 2, handleRadius * 2));

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateMaterial(PrimaryColor, 0.8f);
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

        // Weathered stone colors
        Color mainShaftColor = new Color(0.6f, 0.58f, 0.55f);       // Stone gray
        Color capitalBaseColor = new Color(0.65f, 0.62f, 0.58f);    // Slightly lighter
        Color crackColor = new Color(0.4f, 0.38f, 0.35f);           // Darker weathering
        Color mossColor = new Color(0.35f, 0.42f, 0.32f);           // Green tint for moss
        // Stone color variations for natural look
        Color[] stoneVariations = {
            new Color(0.58f, 0.55f, 0.52f), // Warm gray
            new Color(0.52f, 0.52f, 0.54f), // Cool gray
            new Color(0.55f, 0.5f, 0.45f),  // Brown-gray
        };

        // Base pedestal with stone block texture
        float baseHeight = height * 0.12f;
        float baseRadius = radius * 1.4f;
        int baseSegments = 4;

        for (int hs = 0; hs <= baseSegments; hs++)
        {
            float t = (float)hs / baseSegments;
            float y = t * baseHeight;
            float r = Mathf.Lerp(baseRadius, radius, t);

            // Stone block texture - slight irregularity
            float stoneWear = (hs == 0) ? 0.015f * PropScale : 0f; // Wear at base

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float wearOffset = stoneWear * Mathf.Sin(angle * 4);
                float x = Mathf.Cos(angle) * (r - wearOffset);
                float z = Mathf.Sin(angle) * (r - wearOffset);

                // Base uses capital color (lighter)
                surfaceTool.SetColor(capitalBaseColor);
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

        // Main column with fluting and cracks
        float mainStart = baseHeight;
        float mainEnd = height * 0.88f;
        float mainHeight = mainEnd - mainStart;
        int mainSegments = 14;

        // Precompute crack positions (multiple cracks)
        int crackSegment = rng.RandiRange(3, mainSegments - 3);
        int crackAngle = rng.RandiRange(2, segments - 2);
        int crackSegment2 = rng.RandiRange(3, mainSegments - 3);
        int crackAngle2 = rng.RandiRange(2, segments - 2);

        for (int hs = 0; hs <= mainSegments; hs++)
        {
            float t = (float)hs / mainSegments;
            float y = mainStart + t * mainHeight;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                // Fluting - create vertical grooves
                float flutingDepth = 0.035f * PropScale * Mathf.Abs(Mathf.Sin(angle * segments / 2f));

                // Add crack effect
                float crackDepth = 0f;
                bool isCrack = false;
                if (Mathf.Abs(hs - crackSegment) <= 2 && Mathf.Abs(i - crackAngle) <= 1)
                {
                    crackDepth = 0.02f * PropScale;
                    isCrack = true;
                }
                if (Mathf.Abs(hs - crackSegment2) <= 1 && Mathf.Abs(i - crackAngle2) <= 1)
                {
                    crackDepth = 0.015f * PropScale;
                    isCrack = true;
                }

                float r = radius - flutingDepth - crackDepth;

                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                // Color based on position and crack status
                Color vertColor;
                if (isCrack)
                {
                    vertColor = crackColor;
                }
                else
                {
                    // Add subtle variation to main shaft
                    int varIdx = (hs + i) % stoneVariations.Length;
                    vertColor = mainShaftColor.Lerp(stoneVariations[varIdx], 0.15f);
                }

                surfaceTool.SetColor(vertColor);
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

        // Capital (decorative top) with carved details
        float capitalStart = mainEnd;
        float capitalHeight = height * 0.12f;
        int capitalSegments = 4;

        for (int hs = 0; hs <= capitalSegments; hs++)
        {
            float t = (float)hs / capitalSegments;
            float y = capitalStart + t * capitalHeight;
            float r = Mathf.Lerp(radius, radius * 1.35f, t);

            // Carved details on capital
            float carveDepth = 0.01f * PropScale * Mathf.Sin(t * Mathf.Pi * 2);

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * (r - carveDepth);
                float z = Mathf.Sin(angle) * (r - carveDepth);

                // Capital uses lighter color
                surfaceTool.SetColor(capitalBaseColor);
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

        // Moss patches at base (small boxes with greenish tint)
        int numMossPatches = 3 + rng.RandiRange(0, 2);
        for (int m = 0; m < numMossPatches; m++)
        {
            float mossAngle = rng.Randf() * Mathf.Tau;
            float mossHeight = rng.Randf() * baseHeight * 0.8f;
            float mossSize = 0.04f * PropScale * (0.8f + rng.Randf() * 0.4f);
            float mossR = baseRadius * (1f - mossHeight / baseHeight * 0.3f);

            Vector3 mossPos = new Vector3(
                Mathf.Cos(mossAngle) * mossR,
                mossHeight,
                Mathf.Sin(mossAngle) * mossR
            );
            AddColoredBoxToSurfaceTool(surfaceTool, mossPos, new Vector3(mossSize, mossSize * 0.5f, mossSize), mossColor);
        }

        // Stone block lines (horizontal seams) - darker weathering lines
        float[] blockSeams = { 0.25f, 0.5f, 0.75f };
        foreach (float seamT in blockSeams)
        {
            float seamY = mainStart + seamT * mainHeight;
            float seamRadius = radius * 1.01f;

            for (int i = 0; i < 4; i++)
            {
                float seamAngle = i * Mathf.Pi / 2 + rng.Randf() * 0.2f;
                Vector3 seamPos = new Vector3(
                    Mathf.Cos(seamAngle) * seamRadius,
                    seamY,
                    Mathf.Sin(seamAngle) * seamRadius
                );
                AddColoredBoxToSurfaceTool(surfaceTool, seamPos, new Vector3(0.12f * PropScale, 0.015f * PropScale, 0.02f * PropScale), crackColor);
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        // Weathered stone material with vertex colors
        var mat = new StandardMaterial3D();
        mat.VertexColorUseAsAlbedo = true;
        mat.Roughness = 0.9f; // Very matte stone
        mat.Metallic = 0.0f;
        mesh.SurfaceSetMaterial(0, mat);
        return mesh;
    }

    private ArrayMesh CreateCrystalMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float radius = 0.15f * PropScale;
        float height = 0.5f * PropScale;
        int sides = 8; // Faceted surfaces

        float halfHeight = height / 2f;
        Vector3 topPoint = new Vector3(0, halfHeight * 1.1f, 0); // Slightly higher for sharper point

        // Create two middle rings for more faceting
        float angleStep = Mathf.Tau / sides;
        Vector3[] upperPoints = new Vector3[sides];
        Vector3[] lowerPoints = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float angle = i * angleStep;
            float radiusVar = radius * (0.85f + rng.Randf() * 0.3f);
            float heightVar = (rng.Randf() - 0.5f) * 0.1f * PropScale;

            // Upper ring
            upperPoints[i] = new Vector3(
                Mathf.Cos(angle) * radiusVar * 0.7f,
                halfHeight * 0.3f + heightVar,
                Mathf.Sin(angle) * radiusVar * 0.7f
            );

            // Lower ring
            lowerPoints[i] = new Vector3(
                Mathf.Cos(angle) * radiusVar,
                -halfHeight * 0.3f + heightVar,
                Mathf.Sin(angle) * radiusVar
            );
        }

        // Top point to upper ring (faceted top)
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 v1 = upperPoints[i];
            Vector3 v2 = upperPoints[next];

            surfaceTool.AddVertex(topPoint);
            surfaceTool.AddVertex(v1);
            surfaceTool.AddVertex(v2);
        }

        // Upper ring to lower ring (middle facets)
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 u1 = upperPoints[i];
            Vector3 u2 = upperPoints[next];
            Vector3 l1 = lowerPoints[i];
            Vector3 l2 = lowerPoints[next];

            surfaceTool.AddVertex(u1);
            surfaceTool.AddVertex(l1);
            surfaceTool.AddVertex(u2);

            surfaceTool.AddVertex(u2);
            surfaceTool.AddVertex(l1);
            surfaceTool.AddVertex(l2);
        }

        // Lower ring to bottom point
        Vector3 bottomPoint = new Vector3(0, -halfHeight * 0.9f, 0);
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 v1 = lowerPoints[i];
            Vector3 v2 = lowerPoints[next];

            surfaceTool.AddVertex(bottomPoint);
            surfaceTool.AddVertex(v2);
            surfaceTool.AddVertex(v1);
        }

        // Small crystal clusters at base (4-6 clusters)
        int numClusters = 4 + rng.RandiRange(0, 2);
        for (int c = 0; c < numClusters; c++)
        {
            float clusterAngle = c * Mathf.Tau / numClusters + rng.Randf() * 0.4f;
            float clusterDist = radius * (0.6f + rng.Randf() * 0.4f);
            float clusterHeight = rng.Randf() * 0.15f * PropScale;

            Vector3 clusterBase = new Vector3(
                Mathf.Cos(clusterAngle) * clusterDist,
                -halfHeight * 0.7f + clusterHeight,
                Mathf.Sin(clusterAngle) * clusterDist
            );

            float clusterSize = radius * (0.25f + rng.Randf() * 0.15f);
            float clusterHeightVar = clusterSize * (1.2f + rng.Randf() * 0.6f);
            Vector3 clusterTop = clusterBase + new Vector3(
                (rng.Randf() - 0.5f) * clusterSize * 0.3f,
                clusterHeightVar,
                (rng.Randf() - 0.5f) * clusterSize * 0.3f
            );

            // Smaller crystal with 4-6 sides
            int clusterSides = 4 + rng.RandiRange(0, 2);
            float clusterAngleStep = Mathf.Tau / clusterSides;

            for (int i = 0; i < clusterSides; i++)
            {
                float a1 = i * clusterAngleStep;
                float a2 = (i + 1) * clusterAngleStep;

                Vector3 p1 = clusterBase + new Vector3(Mathf.Cos(a1) * clusterSize * 0.35f, 0, Mathf.Sin(a1) * clusterSize * 0.35f);
                Vector3 p2 = clusterBase + new Vector3(Mathf.Cos(a2) * clusterSize * 0.35f, 0, Mathf.Sin(a2) * clusterSize * 0.35f);

                surfaceTool.AddVertex(clusterTop);
                surfaceTool.AddVertex(p1);
                surfaceTool.AddVertex(p2);
            }
        }

        // Internal glow core (smaller crystal inside)
        float coreRadius = radius * 0.3f;
        float coreHeight = height * 0.5f;
        Vector3 coreTop = new Vector3(0, coreHeight * 0.4f, 0);
        Vector3 coreBot = new Vector3(0, -coreHeight * 0.3f, 0);

        for (int i = 0; i < 4; i++)
        {
            float a1 = i * Mathf.Pi / 2;
            float a2 = (i + 1) * Mathf.Pi / 2;

            Vector3 m1 = new Vector3(Mathf.Cos(a1) * coreRadius, 0, Mathf.Sin(a1) * coreRadius);
            Vector3 m2 = new Vector3(Mathf.Cos(a2) * coreRadius, 0, Mathf.Sin(a2) * coreRadius);

            // Top triangles
            surfaceTool.AddVertex(coreTop);
            surfaceTool.AddVertex(m2);
            surfaceTool.AddVertex(m1);

            // Bottom triangles
            surfaceTool.AddVertex(coreBot);
            surfaceTool.AddVertex(m1);
            surfaceTool.AddVertex(m2);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        // Crystal material with internal glow
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(PrimaryColor.R, PrimaryColor.G, PrimaryColor.B, 0.65f);
        material.Roughness = 0.02f;
        material.Metallic = 0.15f;
        material.EmissionEnabled = true;
        material.Emission = PrimaryColor.Lightened(0.4f);
        material.EmissionEnergyMultiplier = 1.5f;
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

        // Wooden handle with wrapped cloth/leather bands
        float handleWidth = 0.045f * s;
        float handleHeight = 0.42f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, handleHeight / 2, 0),
            new Vector3(handleWidth, handleHeight, handleWidth));

        // Wrapped handle bands (leather/cloth wrapping)
        int numWraps = 5;
        float wrapWidth = 0.008f * s;
        float wrapThickness = 0.006f * s;
        for (int w = 0; w < numWraps; w++)
        {
            float wrapY = 0.08f * s + w * 0.06f * s;
            // Slight diagonal offset for wrapping effect
            float offsetX = (w % 2 == 0) ? 0.002f * s : -0.002f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(offsetX, wrapY, handleWidth / 2 + wrapThickness),
                new Vector3(handleWidth * 1.2f, wrapWidth, wrapThickness * 2));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(offsetX, wrapY, -handleWidth / 2 - wrapThickness),
                new Vector3(handleWidth * 1.2f, wrapWidth, wrapThickness * 2));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(handleWidth / 2 + wrapThickness, wrapY, offsetX),
                new Vector3(wrapThickness * 2, wrapWidth, handleWidth * 1.2f));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(-handleWidth / 2 - wrapThickness, wrapY, offsetX),
                new Vector3(wrapThickness * 2, wrapWidth, handleWidth * 1.2f));
        }

        // Metal bracket (more detailed L-shape with mounting plate)
        float bracketThickness = 0.025f * s;
        // Wall mounting plate
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.35f * s, 0.1f * s),
            new Vector3(0.08f * s, 0.12f * s, 0.01f * s));
        // Horizontal arm
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.38f * s, 0.05f * s),
            new Vector3(0.06f * s, bracketThickness, 0.1f * s));
        // Vertical support
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.3f * s, 0.09f * s),
            new Vector3(0.05f * s, 0.14f * s, bracketThickness));
        // Mounting rivets
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.025f * s, 0.38f * s, 0.105f * s),
            new Vector3(0.012f * s, 0.012f * s, 0.012f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.025f * s, 0.32f * s, 0.105f * s),
            new Vector3(0.012f * s, 0.012f * s, 0.012f * s));

        // Flame holder (cup/cage shape at top)
        float holderY = handleHeight + 0.02f * s;
        float holderRadius = 0.055f * s;
        int holderBars = 8;
        for (int i = 0; i < holderBars; i++)
        {
            float angle = i * Mathf.Tau / holderBars;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * holderRadius, holderY, Mathf.Sin(angle) * holderRadius);
            AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(0.01f * s, 0.07f * s, 0.01f * s));
        }
        // Holder ring at top
        for (int i = 0; i < holderBars; i++)
        {
            float angle = i * Mathf.Tau / holderBars + Mathf.Tau / holderBars / 2;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * holderRadius, holderY + 0.03f * s, Mathf.Sin(angle) * holderRadius);
            AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(0.025f * s, 0.008f * s, 0.008f * s));
        }

        // Flame geometry (layered boxes for flame shape)
        float flameBaseY = holderY + 0.02f * s;
        // Outer flame layer
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, flameBaseY + 0.04f * s, 0),
            new Vector3(0.05f * s, 0.08f * s, 0.05f * s));
        // Middle flame layer (taller, narrower)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, flameBaseY + 0.08f * s, 0),
            new Vector3(0.035f * s, 0.1f * s, 0.035f * s));
        // Inner flame tip
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, flameBaseY + 0.14f * s, 0),
            new Vector3(0.02f * s, 0.06f * s, 0.02f * s));

        // Smoke wisps (small boxes above flame)
        float smokeBaseY = flameBaseY + 0.18f * s;
        for (int sw = 0; sw < 3; sw++)
        {
            float smokeAngle = rng.Randf() * Mathf.Tau;
            float smokeDist = 0.02f * s * (1 + sw * 0.3f);
            float smokeY = smokeBaseY + sw * 0.03f * s;
            float smokeSize = 0.015f * s * (1 - sw * 0.2f);

            AddBoxToSurfaceTool(surfaceTool, new Vector3(
                Mathf.Cos(smokeAngle) * smokeDist,
                smokeY,
                Mathf.Sin(smokeAngle) * smokeDist),
                new Vector3(smokeSize, smokeSize * 1.5f, smokeSize));
        }

        // Ember base (glowing coals)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, flameBaseY, 0),
            new Vector3(0.045f * s, 0.015f * s, 0.045f * s));

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

        // Main chest body with wood grain
        AddBoxToSurfaceTool(surfaceTool, Vector3.Zero, size);

        // Wood plank lines on front
        float plankGap = 0.008f * s;
        for (int p = 0; p < 4; p++)
        {
            float plankX = -size.X / 2 + (p + 0.5f) * size.X / 4;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(plankX, 0, size.Z / 2f + plankGap),
                new Vector3(plankGap, size.Y * 0.95f, plankGap));
        }

        // Lid with arch detail
        float lidThickness = 0.09f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, size.Y / 2f + lidThickness / 2f, 0),
            new Vector3(size.X * 1.02f, lidThickness, size.Z * 1.02f));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, size.Y / 2f + lidThickness * 0.9f, 0),
            new Vector3(size.X * 0.9f, lidThickness * 0.3f, size.Z * 0.9f));

        // Metal bands with side bands
        float bandThickness = 0.025f * s;
        float bandWidth = 0.045f * s;
        for (int i = 0; i < 3; i++)
        {
            float y = -size.Y / 2f + (i + 1) * size.Y / 4f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, y, size.Z / 2f + bandThickness / 2f),
                new Vector3(size.X * 1.08f, bandWidth, bandThickness));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, y, -size.Z / 2f - bandThickness / 2f),
                new Vector3(size.X * 1.08f, bandWidth, bandThickness));
        }

        // Metal corner reinforcements
        float cornerSize = 0.06f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(size.X / 2f, -size.Y / 2f, size.Z / 2f), new Vector3(cornerSize, cornerSize, cornerSize));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-size.X / 2f, -size.Y / 2f, size.Z / 2f), new Vector3(cornerSize, cornerSize, cornerSize));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(size.X / 2f, -size.Y / 2f, -size.Z / 2f), new Vector3(cornerSize, cornerSize, cornerSize));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-size.X / 2f, -size.Y / 2f, -size.Z / 2f), new Vector3(cornerSize, cornerSize, cornerSize));

        // Ornate lock with keyhole
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.05f * s, size.Z / 2f + 0.025f * s), new Vector3(0.1f * s, 0.12f * s, 0.015f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0, size.Z / 2f + 0.035f * s), new Vector3(0.06f * s, 0.06f * s, 0.02f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, -0.02f * s, size.Z / 2f + 0.045f * s), new Vector3(0.015f * s, 0.025f * s, 0.01f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.005f * s, size.Z / 2f + 0.045f * s), new Vector3(0.02f * s, 0.02f * s, 0.01f * s));

        // Detailed hinges with rivets
        for (int i = -1; i <= 1; i += 2)
        {
            float hingeX = i * size.X * 0.3f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(hingeX, size.Y / 2f + 0.02f * s, -size.Z / 2f - 0.015f * s), new Vector3(0.12f * s, 0.05f * s, 0.03f * s));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(hingeX, size.Y / 2f + lidThickness / 2, -size.Z / 2f - 0.025f * s), new Vector3(0.1f * s, 0.025f * s, 0.025f * s));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(hingeX - 0.04f * s, size.Y / 2f + 0.02f * s, -size.Z / 2f - 0.03f * s), new Vector3(0.012f * s, 0.012f * s, 0.012f * s));
            AddBoxToSurfaceTool(surfaceTool, new Vector3(hingeX + 0.04f * s, size.Y / 2f + 0.02f * s, -size.Z / 2f - 0.03f * s), new Vector3(0.012f * s, 0.012f * s, 0.012f * s));
        }

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

        // Cross braces - front and back
        float braceY = 0.25f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, braceY, -0.25f * s),
            new Vector3(0.9f * s, 0.035f * s, 0.04f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, braceY, 0.25f * s),
            new Vector3(0.9f * s, 0.035f * s, 0.04f * s));
        // Side cross braces
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.45f * s, braceY, 0),
            new Vector3(0.04f * s, 0.035f * s, 0.5f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.45f * s, braceY, 0),
            new Vector3(0.04f * s, 0.035f * s, 0.5f * s));

        // Wood grain texture - planks on tabletop
        float tableY = 0.75f * s;
        float tableThick = 0.08f * s;
        for (int plank = 0; plank < 5; plank++)
        {
            float plankZ = -0.24f * s + plank * 0.12f * s;
            // Plank gap line
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, tableY + tableThick / 2 + 0.001f, plankZ),
                new Vector3(0.98f * s, 0.002f * s, 0.008f * s));
        }

        // Wear marks on tabletop edges
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.48f * s, tableY + tableThick / 2 + 0.001f, 0),
            new Vector3(0.02f * s, 0.002f * s, 0.55f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.48f * s, tableY + tableThick / 2 + 0.001f, 0),
            new Vector3(0.02f * s, 0.002f * s, 0.55f * s));

        // Items on surface - cup (cylinder approximated with boxes)
        float itemY = tableY + tableThick / 2;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.3f * s, itemY + 0.04f * s, 0.1f * s),
            new Vector3(0.05f * s, 0.08f * s, 0.05f * s));
        // Cup rim
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.3f * s, itemY + 0.075f * s, 0.1f * s),
            new Vector3(0.055f * s, 0.01f * s, 0.055f * s));

        // Plate (flat disc approximated)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.2f * s, itemY + 0.01f * s, -0.05f * s),
            new Vector3(0.12f * s, 0.015f * s, 0.12f * s));
        // Plate rim
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.2f * s, itemY + 0.018f * s, -0.05f * s),
            new Vector3(0.13f * s, 0.005f * s, 0.13f * s));

        // Book on table
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0f * s, itemY + 0.02f * s, 0.15f * s),
            new Vector3(0.1f * s, 0.03f * s, 0.08f * s));
        // Book spine
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.045f * s, itemY + 0.02f * s, 0.15f * s),
            new Vector3(0.01f * s, 0.032f * s, 0.082f * s));

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

        // Color palette for books
        Color[] bookColors = {
            new Color(0.5f, 0.15f, 0.15f),   // Burgundy red
            new Color(0.15f, 0.35f, 0.2f),   // Forest green
            new Color(0.15f, 0.2f, 0.45f),   // Royal blue
            new Color(0.6f, 0.45f, 0.3f),    // Tan leather
            new Color(0.1f, 0.15f, 0.3f),    // Navy
            new Color(0.4f, 0.1f, 0.15f),    // Maroon
            new Color(0.35f, 0.2f, 0.1f),    // Dark brown
            new Color(0.15f, 0.12f, 0.1f),   // Black
        };

        // Material colors
        Color woodColor = new Color(0.4f, 0.25f, 0.15f);    // Dark wood for frame/shelves
        Color goldColor = new Color(0.75f, 0.6f, 0.2f);     // Gold accent for spine bands
        Color creamColor = new Color(0.9f, 0.85f, 0.7f);    // Cream for scroll paper
        Color brassColor = new Color(0.65f, 0.5f, 0.25f);   // Brass for scroll caps
        Color dustColor = new Color(0.6f, 0.55f, 0.5f);     // Dust color

        // Shelf frame
        AddColoredBoxToSurfaceTool(surfaceTool, Vector3.Zero, new Vector3(1.2f * s, 2f * s, 0.35f * s), woodColor);

        // Shelves (4 horizontal)
        for (int i = 0; i < 4; i++)
        {
            float y = -0.9f * s + i * 0.6f * s;
            AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, y, 0), new Vector3(1.15f * s, 0.03f * s, 0.32f * s), woodColor);
        }

        // Individual books on shelves
        for (int shelf = 0; shelf < 4; shelf++)
        {
            float shelfY = -0.87f * s + shelf * 0.6f * s;
            int numBooks = 6 + (int)(rng.Randf() * 3);

            for (int book = 0; book < numBooks; book++)
            {
                float bookX = -0.5f * s + (book * 0.95f * s / numBooks);
                float bookWidth = 0.08f * s + rng.Randf() * 0.06f * s;
                float bookHeight = 0.28f * s + rng.Randf() * 0.18f * s;
                float bookDepth = 0.05f * s + rng.Randf() * 0.02f * s;

                // Pick a random color for this book
                Color bookColor = bookColors[(int)(rng.Randf() * bookColors.Length)];

                // Main book body
                var bookPos = new Vector3(bookX, shelfY + bookHeight / 2f, 0.03f * s);
                AddColoredBoxToSurfaceTool(surfaceTool, bookPos, new Vector3(bookWidth, bookHeight, bookDepth), bookColor);

                // Book spine (slightly protruding) - same color as book
                AddColoredBoxToSurfaceTool(surfaceTool,
                    new Vector3(bookX, shelfY + bookHeight / 2f, 0.03f * s + bookDepth / 2f + 0.005f * s),
                    new Vector3(bookWidth * 0.9f, bookHeight * 0.95f, 0.008f * s), bookColor);

                // Spine decoration band (top) - gold accent
                AddColoredBoxToSurfaceTool(surfaceTool,
                    new Vector3(bookX, shelfY + bookHeight * 0.85f, 0.03f * s + bookDepth / 2f + 0.006f * s),
                    new Vector3(bookWidth * 0.85f, 0.015f * s, 0.003f * s), goldColor);

                // Spine decoration band (bottom) - gold accent
                AddColoredBoxToSurfaceTool(surfaceTool,
                    new Vector3(bookX, shelfY + bookHeight * 0.15f, 0.03f * s + bookDepth / 2f + 0.006f * s),
                    new Vector3(bookWidth * 0.85f, 0.015f * s, 0.003f * s), goldColor);
            }

            // Scroll tube on some shelves
            if (shelf % 2 == 1)
            {
                float scrollX = 0.4f * s + rng.Randf() * 0.1f * s;
                float scrollY = shelfY + 0.04f * s;
                // Scroll paper (cream)
                AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(scrollX, scrollY, 0.05f * s),
                    new Vector3(0.04f * s, 0.04f * s, 0.18f * s), creamColor);
                // Scroll caps (brass)
                AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(scrollX, scrollY, -0.05f * s),
                    new Vector3(0.05f * s, 0.05f * s, 0.02f * s), brassColor);
                AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(scrollX, scrollY, 0.15f * s),
                    new Vector3(0.05f * s, 0.05f * s, 0.02f * s), brassColor);
            }
        }

        // Dust on shelf edges
        for (int i = 0; i < 4; i++)
        {
            float shelfY = -0.9f * s + i * 0.6f * s;
            AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, shelfY + 0.018f * s, 0.15f * s),
                new Vector3(1.1f * s, 0.003f * s, 0.02f * s), dustColor);
        }

        // Top molding
        AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, 1.0f * s, 0),
            new Vector3(1.25f * s, 0.04f * s, 0.38f * s), woodColor);
        AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, 1.03f * s, 0.05f * s),
            new Vector3(1.2f * s, 0.02f * s, 0.25f * s), woodColor);

        // Base molding
        AddColoredBoxToSurfaceTool(surfaceTool, new Vector3(0, -1.0f * s, 0),
            new Vector3(1.25f * s, 0.04f * s, 0.38f * s), woodColor);

        // Generate normals for proper lighting
        surfaceTool.GenerateNormals();

        var mesh = surfaceTool.Commit();
        // Use vertex color material instead of wood material
        var mat = new StandardMaterial3D();
        mat.VertexColorUseAsAlbedo = true;
        mat.Roughness = 0.85f;
        mesh.SurfaceSetMaterial(0, mat);
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

        // Tripod stand with cross bracing
        float standHeight = 0.4f * s;
        float legSpread = radius * 1.1f;
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.Tau / 3f;
            float x = Mathf.Cos(angle) * legSpread;
            float z = Mathf.Sin(angle) * legSpread;
            Vector3 legTop = new Vector3(x * 0.5f, -height / 2f, z * 0.5f);
            Vector3 legBottom = new Vector3(x, -height / 2f - standHeight, z);
            AddBoxToSurfaceTool(surfaceTool, (legTop + legBottom) / 2f, new Vector3(0.04f * s, standHeight * 1.1f, 0.04f * s));
            AddBoxToSurfaceTool(surfaceTool, legBottom, new Vector3(0.06f * s, 0.02f * s, 0.06f * s));
            float nextAngle = ((i + 1) % 3) * Mathf.Tau / 3f;
            float braceY = -height / 2f - standHeight * 0.6f;
            Vector3 braceStart = new Vector3(Mathf.Cos(angle) * legSpread * 0.7f, braceY, Mathf.Sin(angle) * legSpread * 0.7f);
            Vector3 braceEnd = new Vector3(Mathf.Cos(nextAngle) * legSpread * 0.7f, braceY, Mathf.Sin(nextAngle) * legSpread * 0.7f);
            AddBoxToSurfaceTool(surfaceTool, (braceStart + braceEnd) / 2f, new Vector3(radius * 0.8f, 0.025f * s, 0.025f * s));
        }

        // Fire underneath (log pile)
        float fireY = -height / 2f - standHeight * 0.4f;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, fireY, 0), new Vector3(0.25f * s, 0.06f * s, 0.06f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, fireY, 0), new Vector3(0.06f * s, 0.06f * s, 0.22f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.08f * s, fireY + 0.04f * s, 0.05f * s), new Vector3(0.18f * s, 0.04f * s, 0.04f * s));
        float flameY = fireY + 0.1f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, flameY, 0), new Vector3(0.08f * s, 0.15f * s, 0.08f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.06f * s, flameY - 0.02f * s, 0.04f * s), new Vector3(0.05f * s, 0.1f * s, 0.05f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.05f * s, flameY - 0.03f * s, -0.03f * s), new Vector3(0.04f * s, 0.08f * s, 0.04f * s));

        // Bubbling liquid surface
        float liquidY = height / 2f - 0.08f * s;
        float liquidRadius = radius * 0.85f;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, liquidY, 0), new Vector3(liquidRadius * 1.7f, 0.02f * s, liquidRadius * 1.7f));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.1f * s, liquidY + 0.03f * s, 0.05f * s), new Vector3(0.04f * s, 0.04f * s, 0.04f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.08f * s, liquidY + 0.025f * s, -0.1f * s), new Vector3(0.03f * s, 0.03f * s, 0.03f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.02f * s, liquidY + 0.02f * s, 0.12f * s), new Vector3(0.025f * s, 0.025f * s, 0.025f * s));

        // Steam wisps
        float steamY = height / 2f + 0.15f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.05f * s, steamY, 0.02f * s), new Vector3(0.015f * s, 0.2f * s, 0.015f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.08f * s, steamY + 0.05f * s, -0.04f * s), new Vector3(0.012f * s, 0.18f * s, 0.012f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.02f * s, steamY + 0.08f * s, 0.08f * s), new Vector3(0.01f * s, 0.15f * s, 0.01f * s));

        surfaceTool.GenerateNormals();
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

            surfaceTool.AddVertex(b1);
            surfaceTool.AddVertex(b2);
            surfaceTool.AddVertex(t2);

            surfaceTool.AddVertex(b1);
            surfaceTool.AddVertex(t2);
            surfaceTool.AddVertex(t1);
        }

        // Pedestal decorative molding
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, 0), new Vector3(0.72f * s, 0.04f * s, 0.72f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, baseHeight - 0.02f * s, 0), new Vector3(0.52f * s, 0.04f * s, 0.52f * s));

        // Pedestal carved details (front inscription panel)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, baseHeight * 0.5f, baseRadius - 0.02f * s),
            new Vector3(0.25f * s, 0.15f * s, 0.02f * s));

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

        // Warrior pose - Right arm raised with weapon
        float armY = baseHeight + bodyHeight * 0.65f;
        float armLength = 0.5f * s;
        // Right arm (raised)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.35f * s, armY + 0.2f * s, 0),
            new Vector3(0.08f * s, armLength, 0.08f * s));
        // Weapon (sword/staff) in right hand
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.35f * s, armY + 0.55f * s, 0),
            new Vector3(0.04f * s, 0.6f * s, 0.04f * s));
        // Weapon crossguard
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.35f * s, armY + 0.35f * s, 0),
            new Vector3(0.12f * s, 0.025f * s, 0.025f * s));

        // Left arm (holding shield - MISSING/broken)
        // Just a stump to show weathered/missing piece
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.3f * s, armY - 0.05f * s, 0.05f * s),
            new Vector3(0.1f * s, 0.15f * s, 0.08f * s));

        // Head (weathered sphere approximated with box)
        float headY = baseHeight + bodyHeight + 0.1f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, headY, 0),
            new Vector3(0.18f * s, 0.22f * s, 0.16f * s));
        // Helmet/crown detail
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, headY + 0.12f * s, 0),
            new Vector3(0.2f * s, 0.06f * s, 0.18f * s));
        // Helmet crest
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, headY + 0.18f * s, -0.02f * s),
            new Vector3(0.04f * s, 0.1f * s, 0.12f * s));

        // Weathered stone cracks
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.1f * s, baseHeight + bodyHeight * 0.3f, 0.24f * s),
            new Vector3(0.015f * s, 0.25f * s, 0.01f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.15f * s, baseHeight + bodyHeight * 0.5f, 0.22f * s),
            new Vector3(0.01f * s, 0.18f * s, 0.008f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.05f * s, baseHeight + bodyHeight * 0.7f, -0.23f * s),
            new Vector3(0.012f * s, 0.15f * s, 0.008f * s));

        // Missing piece on shoulder (chunk broken off)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.22f * s, armY + 0.05f * s, -0.08f * s),
            new Vector3(0.08f * s, 0.06f * s, 0.06f * s));

        // Fallen debris at base (broken pieces)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.4f * s, 0.03f * s, 0.15f * s),
            new Vector3(0.1f * s, 0.05f * s, 0.08f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.35f * s, 0.02f * s, -0.25f * s),
            new Vector3(0.06f * s, 0.04f * s, 0.07f * s));

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = ProceduralMesh3D.CreateMaterial(PrimaryColor, 0.75f, 0.05f);
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

        // Backrest frame with carved decoration
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, backY, backZ), new Vector3(backWidth, backHeight, 0.04f * s));
        // Top rail with carved crest
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, backY + backHeight / 2f + 0.02f * s, backZ),
            new Vector3(backWidth * 1.05f, 0.06f * s, 0.05f * s));
        // Carved decorative center piece
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, backY + backHeight / 2f + 0.04f * s, backZ - 0.02f * s),
            new Vector3(0.12f * s, 0.08f * s, 0.02f * s));

        // Vertical slats (5 slats) with decorative tops
        int numSlats = 5;
        for (int i = 0; i < numSlats; i++)
        {
            float slatX = -backWidth / 2f + (i + 0.5f) * backWidth / numSlats;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(slatX, backY, backZ - 0.01f * s),
                new Vector3(0.04f * s, backHeight * 0.9f, 0.02f * s));
            // Decorative finial on each slat
            AddBoxToSurfaceTool(surfaceTool, new Vector3(slatX, backY + backHeight * 0.42f, backZ - 0.015f * s),
                new Vector3(0.05f * s, 0.04f * s, 0.025f * s));
        }

        // Cushion on seat
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.44f * s, 0.02f * s),
            new Vector3(0.4f * s, 0.06f * s, 0.38f * s));
        // Cushion edge detail (tufted look)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.47f * s, 0.02f * s),
            new Vector3(0.35f * s, 0.02f * s, 0.33f * s));

        // Armrests
        float armrestY = 0.6f * s;
        float armrestLength = 0.35f * s;
        // Left armrest
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.22f * s, armrestY, 0),
            new Vector3(0.04f * s, 0.04f * s, armrestLength));
        // Left armrest support (front)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.22f * s, 0.5f * s, 0.15f * s),
            new Vector3(0.035f * s, 0.2f * s, 0.035f * s));
        // Left armrest end cap
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.22f * s, armrestY, 0.17f * s),
            new Vector3(0.05f * s, 0.05f * s, 0.03f * s));

        // Right armrest
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.22f * s, armrestY, 0),
            new Vector3(0.04f * s, 0.04f * s, armrestLength));
        // Right armrest support (front)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.22f * s, 0.5f * s, 0.15f * s),
            new Vector3(0.035f * s, 0.2f * s, 0.035f * s));
        // Right armrest end cap
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.22f * s, armrestY, 0.17f * s),
            new Vector3(0.05f * s, 0.05f * s, 0.03f * s));

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

            // Turned sections (3 decorative bulges)
            AddBoxToSurfaceTool(surfaceTool, pos + new Vector3(0, legHeight * 0.3f, 0),
                new Vector3(legWidth * 1.4f, legHeight * 0.08f, legWidth * 1.4f));
            AddBoxToSurfaceTool(surfaceTool, pos,
                new Vector3(legWidth * 1.3f, legHeight * 0.06f, legWidth * 1.3f));
            AddBoxToSurfaceTool(surfaceTool, pos - new Vector3(0, legHeight * 0.3f, 0),
                new Vector3(legWidth * 1.35f, legHeight * 0.07f, legWidth * 1.35f));

            // Leg foot cap
            AddBoxToSurfaceTool(surfaceTool, pos - new Vector3(0, legHeight * 0.48f, 0),
                new Vector3(legWidth * 1.2f, 0.02f * s, legWidth * 1.2f));
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
        var boneColor = new Color(0.9f, 0.85f, 0.75f); // Enhanced skull pile

        // Enhanced skull pile with more skulls in pyramid formation
        Vector3[] skullPositions = new Vector3[]
        {
            // Base layer - larger skulls forming foundation
            new Vector3(-0.2f * s, 0.1f * s, -0.15f * s),
            new Vector3(0.18f * s, 0.1f * s, -0.12f * s),
            new Vector3(0f * s, 0.1f * s, 0.18f * s),
            new Vector3(-0.22f * s, 0.08f * s, 0.1f * s),
            new Vector3(0.2f * s, 0.09f * s, 0.12f * s),
            new Vector3(0.05f * s, 0.08f * s, -0.2f * s),
            // Middle layer
            new Vector3(0f * s, 0.22f * s, 0f * s),
            new Vector3(-0.12f * s, 0.2f * s, 0.08f * s),
            new Vector3(0.1f * s, 0.21f * s, -0.08f * s),
            new Vector3(-0.08f * s, 0.19f * s, -0.1f * s),
            // Top layer - smaller skulls
            new Vector3(0f * s, 0.32f * s, 0.02f * s),
            new Vector3(-0.06f * s, 0.3f * s, 0.05f * s),
        };

        float[] skullSizes = { 0.12f, 0.11f, 0.13f, 0.1f, 0.11f, 0.1f, 0.11f, 0.09f, 0.08f, 0.085f, 0.08f, 0.07f };

        for (int i = 0; i < skullPositions.Length; i++)
        {
            Vector3 pos = skullPositions[i];
            float size = skullSizes[i] * s;
            float angle = rng.Randf() * Mathf.Tau;

            // Skull cranium (main sphere with back bulge)
            AddSphereToSurfaceTool(surfaceTool, pos, size);
            Vector3 backDir = new Vector3(-Mathf.Cos(angle), 0, -Mathf.Sin(angle));
            AddSphereToSurfaceTool(surfaceTool, pos + backDir * size * 0.3f + new Vector3(0, size * 0.1f, 0), size * 0.7f);

            // Brow ridge (prominent forehead)
            Vector3 faceDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 browPos = pos + faceDir * size * 0.5f + new Vector3(0, size * 0.25f, 0);
            AddBoxToSurfaceTool(surfaceTool, browPos, new Vector3(size * 0.6f, size * 0.12f, size * 0.15f));

            // Deep eye sockets
            float eyeSpread = size * 0.25f;
            Vector3 rightDir = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
            Vector3 rightEye = pos + faceDir * size * 0.55f + rightDir * eyeSpread + new Vector3(0, size * 0.1f, 0);
            Vector3 leftEye = pos + faceDir * size * 0.55f - rightDir * eyeSpread + new Vector3(0, size * 0.1f, 0);
            AddSphereToSurfaceTool(surfaceTool, rightEye, size * 0.18f);
            AddSphereToSurfaceTool(surfaceTool, leftEye, size * 0.18f);

            // Nasal cavity
            Vector3 nosePos = pos + faceDir * size * 0.6f - new Vector3(0, size * 0.1f, 0);
            AddBoxToSurfaceTool(surfaceTool, nosePos, new Vector3(size * 0.1f, size * 0.15f, size * 0.08f));

            // Cheekbones
            AddSphereToSurfaceTool(surfaceTool, pos + faceDir * size * 0.35f + rightDir * size * 0.4f - new Vector3(0, size * 0.15f, 0), size * 0.15f);
            AddSphereToSurfaceTool(surfaceTool, pos + faceDir * size * 0.35f - rightDir * size * 0.4f - new Vector3(0, size * 0.15f, 0), size * 0.15f);

            // Jaw bone (mandible) with hinges
            Vector3 jawPos = pos + faceDir * size * 0.2f - new Vector3(0, size * 0.55f, 0);
            AddBoxToSurfaceTool(surfaceTool, jawPos, new Vector3(size * 0.5f, size * 0.2f, size * 0.25f));
            AddSphereToSurfaceTool(surfaceTool, jawPos + faceDir * size * 0.25f, size * 0.12f);
            AddSphereToSurfaceTool(surfaceTool, jawPos - rightDir * size * 0.3f + new Vector3(0, size * 0.15f, 0), size * 0.08f);
            AddSphereToSurfaceTool(surfaceTool, jawPos + rightDir * size * 0.3f + new Vector3(0, size * 0.15f, 0), size * 0.08f);

            // Upper teeth (6 teeth with canines)
            Vector3 upperTeethBase = pos + faceDir * size * 0.5f - new Vector3(0, size * 0.35f, 0);
            for (int t = 0; t < 6; t++)
            {
                float toothOffset = (t - 2.5f) * size * 0.07f;
                Vector3 toothPos = upperTeethBase + rightDir * toothOffset;
                float toothHeight = (t == 0 || t == 5) ? size * 0.06f : size * 0.08f;
                AddBoxToSurfaceTool(surfaceTool, toothPos, new Vector3(size * 0.04f, toothHeight, size * 0.04f));
            }

            // Lower teeth (5 teeth)
            Vector3 lowerTeethBase = jawPos + faceDir * size * 0.2f + new Vector3(0, size * 0.08f, 0);
            for (int t = 0; t < 5; t++)
            {
                float toothOffset = (t - 2f) * size * 0.065f;
                Vector3 toothPos = lowerTeethBase + rightDir * toothOffset;
                AddBoxToSurfaceTool(surfaceTool, toothPos, new Vector3(size * 0.035f, size * 0.06f, size * 0.035f));
            }

            // Random cracks on skulls
            if (rng.Randf() > 0.5f)
            {
                float crackAngle = rng.Randf() * Mathf.Tau;
                Vector3 crackDir = new Vector3(Mathf.Cos(crackAngle), 0.2f, Mathf.Sin(crackAngle));
                Vector3 crackStart = pos + crackDir * size * 0.3f;
                AddBoxToSurfaceTool(surfaceTool, crackStart, new Vector3(size * 0.02f, size * 0.25f, size * 0.02f));
                AddBoxToSurfaceTool(surfaceTool, crackStart + new Vector3(size * 0.05f, size * 0.1f, 0), new Vector3(size * 0.08f, size * 0.015f, size * 0.015f));
            }
        }

        // Scattered long bones (femurs, tibias) - 8 bones
        for (int b = 0; b < 8; b++)
        {
            float boneAngle = rng.Randf() * Mathf.Tau;
            float boneDist = rng.RandfRange(0.1f, 0.4f) * s;
            Vector3 bonePos = new Vector3(Mathf.Cos(boneAngle) * boneDist, rng.Randf() * 0.12f * s, Mathf.Sin(boneAngle) * boneDist);
            float boneRotation = rng.Randf() * Mathf.Pi;
            Vector3 boneDir = new Vector3(Mathf.Cos(boneRotation), 0, Mathf.Sin(boneRotation));
            float boneLength = rng.RandfRange(0.08f, 0.15f) * s;

            AddBoxToSurfaceTool(surfaceTool, bonePos, new Vector3(boneLength, 0.025f * s, 0.02f * s));
            AddSphereToSurfaceTool(surfaceTool, bonePos + boneDir * boneLength * 0.5f, 0.022f * s);
            AddSphereToSurfaceTool(surfaceTool, bonePos - boneDir * boneLength * 0.5f, 0.02f * s);
        }

        // Rib fragments scattered around
        for (int r = 0; r < 5; r++)
        {
            float ribAngle = rng.Randf() * Mathf.Tau;
            Vector3 ribPos = new Vector3(Mathf.Cos(ribAngle) * rng.RandfRange(0.15f, 0.35f) * s, 0.02f * s, Mathf.Sin(ribAngle) * rng.RandfRange(0.15f, 0.35f) * s);
            for (int seg = 0; seg < 4; seg++)
            {
                float segAngle = ribAngle + seg * 0.15f;
                Vector3 segPos = ribPos + new Vector3(Mathf.Cos(segAngle) * seg * 0.02f * s, seg * 0.005f * s, Mathf.Sin(segAngle) * seg * 0.02f * s);
                AddBoxToSurfaceTool(surfaceTool, segPos, new Vector3(0.025f * s, 0.012f * s, 0.01f * s));
            }
        }

        // Small vertebrae scattered
        for (int v = 0; v < 4; v++)
        {
            Vector3 vertPos = new Vector3((rng.Randf() - 0.5f) * 0.3f * s, rng.Randf() * 0.08f * s, (rng.Randf() - 0.5f) * 0.3f * s);
            AddSphereToSurfaceTool(surfaceTool, vertPos, 0.025f * s);
            AddBoxToSurfaceTool(surfaceTool, vertPos + new Vector3(0, 0.02f * s, 0), new Vector3(0.01f * s, 0.03f * s, 0.015f * s));
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

        // Enhanced stone ring around the fire (more stones, varied sizes, some stacked)
        int numRocks = 12;
        for (int i = 0; i < numRocks; i++)
        {
            float angle = i * Mathf.Pi * 2f / numRocks + rng.RandfRange(-0.1f, 0.1f);
            float rockDist = 0.38f + rng.RandfRange(-0.05f, 0.05f);
            float rockX = Mathf.Cos(angle) * rockDist * s;
            float rockZ = Mathf.Sin(angle) * rockDist * s;
            float rockSize = (0.08f + rng.Randf() * 0.06f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(rockX, rockSize * 0.5f, rockZ), rockSize);
            // Some double-stacked stones
            if (rng.Randf() > 0.6f)
            {
                AddSphereToSurfaceTool(surfaceTool, new Vector3(rockX, rockSize * 1.3f, rockZ), rockSize * 0.7f);
            }
        }

        // Logs in teepee arrangement (6 logs leaning inward)
        for (int log = 0; log < 6; log++)
        {
            float logAngle = log * Mathf.Tau / 6f + rng.RandfRange(-0.1f, 0.1f);
            float logRadius = rng.RandfRange(0.035f, 0.05f) * s;
            Vector3 logBase = new Vector3(Mathf.Cos(logAngle) * 0.25f * s, 0.03f * s, Mathf.Sin(logAngle) * 0.25f * s);
            Vector3 logTop = new Vector3(rng.RandfRange(-0.03f, 0.03f) * s, 0.25f * s, rng.RandfRange(-0.03f, 0.03f) * s);
            AddCylinderToSurfaceTool(surfaceTool, logBase, logTop, logRadius);
            // Log bark texture (small boxes along log)
            for (int bark = 0; bark < 3; bark++)
            {
                float t = 0.2f + bark * 0.25f;
                Vector3 barkPos = logBase + (logTop - logBase) * t;
                AddBoxToSurfaceTool(surfaceTool, barkPos, new Vector3(logRadius * 0.3f, logRadius * 0.2f, logRadius * 1.5f));
            }
        }

        // Collapsed/burned logs at base
        for (int bl = 0; bl < 3; bl++)
        {
            float blAngle = rng.Randf() * Mathf.Tau;
            Vector3 blStart = new Vector3(Mathf.Cos(blAngle) * 0.1f * s, 0.04f * s, Mathf.Sin(blAngle) * 0.1f * s);
            Vector3 blEnd = blStart + new Vector3(Mathf.Cos(blAngle + 0.5f) * 0.15f * s, 0.02f * s, Mathf.Sin(blAngle + 0.5f) * 0.15f * s);
            AddCylinderToSurfaceTool(surfaceTool, blStart, blEnd, 0.03f * s);
        }

        // Ash pile at center
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, 0), 0.12f * s);
        for (int ash = 0; ash < 5; ash++)
        {
            float ashAngle = rng.Randf() * Mathf.Tau;
            float ashDist = rng.RandfRange(0.05f, 0.15f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(Mathf.Cos(ashAngle) * ashDist, 0.01f * s, Mathf.Sin(ashAngle) * ashDist), rng.RandfRange(0.02f, 0.04f) * s);
        }

        // Glowing embers (small spheres)
        for (int ember = 0; ember < 8; ember++)
        {
            float emberAngle = rng.Randf() * Mathf.Tau;
            float emberDist = rng.RandfRange(0.02f, 0.12f) * s;
            float emberY = rng.RandfRange(0.02f, 0.08f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(Mathf.Cos(emberAngle) * emberDist, emberY, Mathf.Sin(emberAngle) * emberDist), rng.RandfRange(0.01f, 0.025f) * s);
        }

        // Cooking spit (two Y-shaped sticks with crossbar)
        // Left support
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(-0.35f * s, 0, 0), new Vector3(-0.35f * s, 0.35f * s, 0), 0.015f * s);
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(-0.35f * s, 0.3f * s, 0), new Vector3(-0.38f * s, 0.4f * s, 0.03f * s), 0.01f * s);
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(-0.35f * s, 0.3f * s, 0), new Vector3(-0.32f * s, 0.4f * s, -0.03f * s), 0.01f * s);
        // Right support
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(0.35f * s, 0, 0), new Vector3(0.35f * s, 0.35f * s, 0), 0.015f * s);
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(0.35f * s, 0.3f * s, 0), new Vector3(0.38f * s, 0.4f * s, 0.03f * s), 0.01f * s);
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(0.35f * s, 0.3f * s, 0), new Vector3(0.32f * s, 0.4f * s, -0.03f * s), 0.01f * s);
        // Crossbar
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(-0.35f * s, 0.32f * s, 0), new Vector3(0.35f * s, 0.32f * s, 0), 0.012f * s);

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
        // NOTE: Do NOT set normals here - let GenerateNormals() handle it at the end
        // This ensures compatibility with both Godot 4.3 and 4.5 (no mixed normal format)
        Vector3 dir = (end - start).Normalized();

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

            // Two triangles for the quad - normals calculated by GenerateNormals()
            st.AddVertex(v1);
            st.AddVertex(v2);
            st.AddVertex(v3);

            st.AddVertex(v1);
            st.AddVertex(v3);
            st.AddVertex(v4);
        }
    }

    // Overload for tapered cylinders (different top/bottom radii)
    // NOTE: Do NOT set normals here - let GenerateNormals() handle it at the end
    // This ensures compatibility with both Godot 4.3 and 4.5 (no mixed normal format)
    private void AddCylinderToSurfaceTool(SurfaceTool st, Vector3 center, float bottomRadius, float topRadius, float height)
    {
        Vector3 start = center - new Vector3(0, height / 2f, 0);
        Vector3 end = center + new Vector3(0, height / 2f, 0);

        int segments = 6;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Pi * 2f / segments;
            float angle2 = (i + 1) * Mathf.Pi * 2f / segments;

            Vector3 offsetBottom1 = new Vector3(Mathf.Cos(angle1) * bottomRadius, 0, Mathf.Sin(angle1) * bottomRadius);
            Vector3 offsetBottom2 = new Vector3(Mathf.Cos(angle2) * bottomRadius, 0, Mathf.Sin(angle2) * bottomRadius);
            Vector3 offsetTop1 = new Vector3(Mathf.Cos(angle1) * topRadius, 0, Mathf.Sin(angle1) * topRadius);
            Vector3 offsetTop2 = new Vector3(Mathf.Cos(angle2) * topRadius, 0, Mathf.Sin(angle2) * topRadius);

            Vector3 v1 = start + offsetBottom1;
            Vector3 v2 = start + offsetBottom2;
            Vector3 v3 = end + offsetTop2;
            Vector3 v4 = end + offsetTop1;

            // Two triangles for the quad - normals calculated by GenerateNormals()
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
        // NOTE: Do NOT set normals here - let GenerateNormals() handle it at the end
        // This ensures compatibility with both Godot 4.3 and 4.5 (no mixed normal format)
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

        // Define the 6 faces (2 triangles each) - normals calculated by GenerateNormals()
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

    private void AddColoredBoxToSurfaceTool(SurfaceTool st, Vector3 center, Vector3 size, Color color)
    {
        // Helper to add a colored box to the surface tool (for vertex color materials)
        // NOTE: Do NOT set normals here - let GenerateNormals() handle it at the end
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

        // Define the 6 faces (2 triangles each) - normals calculated by GenerateNormals()
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
                st.SetColor(color);
                st.AddVertex(verts[idx]);
            }
        }
    }

    private void AddRotatedBoxToSurfaceTool(SurfaceTool st, Vector3 center, Vector3 size, float yRotation)
    {
        // Helper to add a Y-axis rotated box to the surface tool
        // NOTE: Do NOT set normals here - let GenerateNormals() handle it at the end
        // This ensures compatibility with both Godot 4.3 and 4.5 (no mixed normal format)
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

        // Define the 6 faces - normals calculated by GenerateNormals()
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
        // NOTE: Do NOT set normals here - let GenerateNormals() handle it at the end
        // This ensures compatibility with both Godot 4.3 and 4.5 (no mixed normal format)
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

                // Two triangles for the quad - normals calculated by GenerateNormals()
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

        // Enhanced irregular moss patch with varied thickness
        int segments = 12;
        Vector3 center = new Vector3(0, 0.01f * s, 0);
        Vector3 upNormal = Vector3.Up; // Flat moss patch faces up
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

        // Spreading tendrils extending outward
        for (int t = 0; t < 8; t++)
        {
            float tendrilAngle = rng.Randf() * Mathf.Tau;
            float tendrilDist = rng.RandfRange(0.35f, 0.5f) * s;
            Vector3 tendrilPos = new Vector3(Mathf.Cos(tendrilAngle) * tendrilDist, 0.005f * s, Mathf.Sin(tendrilAngle) * tendrilDist);
            float tendrilSize = rng.RandfRange(0.03f, 0.06f) * s;
            AddSphereToSurfaceTool(surfaceTool, tendrilPos, tendrilSize);
        }

        // Moss clumps with varied thickness (raised bumps)
        for (int clump = 0; clump < 6; clump++)
        {
            float clumpAngle = rng.Randf() * Mathf.Tau;
            float clumpDist = rng.RandfRange(0.05f, 0.25f) * s;
            Vector3 clumpPos = new Vector3(Mathf.Cos(clumpAngle) * clumpDist, 0.015f * s, Mathf.Sin(clumpAngle) * clumpDist);
            AddSphereToSurfaceTool(surfaceTool, clumpPos, rng.RandfRange(0.04f, 0.08f) * s);
        }

        // Small mushrooms growing in moss
        int numMushrooms = rng.RandiRange(3, 5);
        for (int m = 0; m < numMushrooms; m++)
        {
            float mushAngle = rng.Randf() * Mathf.Tau;
            float mushDist = rng.RandfRange(0.08f, 0.22f) * s;
            Vector3 mushPos = new Vector3(Mathf.Cos(mushAngle) * mushDist, 0, Mathf.Sin(mushAngle) * mushDist);
            float stemH = rng.RandfRange(0.02f, 0.04f) * s;
            float capR = rng.RandfRange(0.015f, 0.025f) * s;
            AddBoxToSurfaceTool(surfaceTool, mushPos + new Vector3(0, stemH / 2, 0), new Vector3(0.008f * s, stemH, 0.008f * s));
            AddSphereToSurfaceTool(surfaceTool, mushPos + new Vector3(0, stemH + capR * 0.4f, 0), capR);
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

        // Enhanced irregular puddle with ripple rings and debris
        int segments = 14;
        Vector3 center = new Vector3(0, 0.002f * s, 0);
        Vector3 upNormal = Vector3.Up; // Flat water surface faces up
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

        // Ripple rings (concentric circles at slightly different heights)
        for (int ring = 0; ring < 3; ring++)
        {
            float ringRadius = (0.1f + ring * 0.08f) * s;
            float ringY = 0.003f * s + ring * 0.001f * s;
            for (int seg = 0; seg < 12; seg++)
            {
                float a1 = seg * Mathf.Tau / 12;
                float a2 = (seg + 1) * Mathf.Tau / 12;
                Vector3 ripple1 = new Vector3(Mathf.Cos(a1) * ringRadius, ringY, Mathf.Sin(a1) * ringRadius);
                Vector3 ripple2 = new Vector3(Mathf.Cos(a2) * ringRadius, ringY, Mathf.Sin(a2) * ringRadius);
                Vector3 inner1 = new Vector3(Mathf.Cos(a1) * (ringRadius - 0.01f * s), ringY - 0.001f * s, Mathf.Sin(a1) * (ringRadius - 0.01f * s));
                Vector3 inner2 = new Vector3(Mathf.Cos(a2) * (ringRadius - 0.01f * s), ringY - 0.001f * s, Mathf.Sin(a2) * (ringRadius - 0.01f * s));
                surfaceTool.AddVertex(inner1);
                surfaceTool.AddVertex(ripple1);
                surfaceTool.AddVertex(ripple2);
                surfaceTool.AddVertex(inner1);
                surfaceTool.AddVertex(ripple2);
                surfaceTool.AddVertex(inner2);
            }
        }

        // Floating debris (small leaves/twigs)
        for (int debris = 0; debris < 4; debris++)
        {
            float debrisAngle = rng.Randf() * Mathf.Tau;
            float debrisDist = rng.RandfRange(0.05f, 0.2f) * s;
            Vector3 debrisPos = new Vector3(Mathf.Cos(debrisAngle) * debrisDist, 0.005f * s, Mathf.Sin(debrisAngle) * debrisDist);
            AddBoxToSurfaceTool(surfaceTool, debrisPos, new Vector3(0.03f * s, 0.002f * s, 0.015f * s));
        }

        // Small pebbles at edge
        for (int pebble = 0; pebble < 5; pebble++)
        {
            float pebbleAngle = rng.Randf() * Mathf.Tau;
            float pebbleDist = rng.RandfRange(0.22f, 0.32f) * s;
            Vector3 pebblePos = new Vector3(Mathf.Cos(pebbleAngle) * pebbleDist, 0.008f * s, Mathf.Sin(pebbleAngle) * pebbleDist);
            AddSphereToSurfaceTool(surfaceTool, pebblePos, rng.RandfRange(0.01f, 0.02f) * s);
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

        // Gold coins inside (multiple layers for richness) - overflowing!
        int numCoins = 25;
        for (int i = 0; i < numCoins; i++)
        {
            float x = (rng.Randf() - 0.5f) * 0.35f * s;
            float z = (rng.Randf() - 0.5f) * 0.2f * s;
            float coinY = 0.32f * s + rng.Randf() * 0.12f * s;

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

                surfaceTool.AddVertex(coinPos);
                surfaceTool.AddVertex(p1Top);
                surfaceTool.AddVertex(p2Top);
            }
        }

        // Coins spilling out the front
        for (int i = 0; i < 8; i++)
        {
            float x = (rng.Randf() - 0.5f) * 0.4f * s;
            float z = 0.25f * s + rng.Randf() * 0.15f * s;
            float coinY = 0.02f * s + rng.Randf() * 0.04f * s;
            Vector3 coinPos = new Vector3(x, coinY, z);
            float coinRadius = 0.035f * s;

            for (int seg = 0; seg < 8; seg++)
            {
                float angle1 = seg * Mathf.Tau / 8f;
                float angle2 = (seg + 1) * Mathf.Tau / 8f;

                Vector3 p1 = coinPos + new Vector3(Mathf.Cos(angle1) * coinRadius, 0, Mathf.Sin(angle1) * coinRadius);
                Vector3 p2 = coinPos + new Vector3(Mathf.Cos(angle2) * coinRadius, 0, Mathf.Sin(angle2) * coinRadius);
                Vector3 p1Top = p1 + new Vector3(0, 0.01f * s, 0);
                Vector3 p2Top = p2 + new Vector3(0, 0.01f * s, 0);

                surfaceTool.AddVertex(coinPos);
                surfaceTool.AddVertex(p1Top);
                surfaceTool.AddVertex(p2Top);
            }
        }

        // Golden goblet (chalice shape) - using boxes per procedural-3d-artist patterns
        Vector3 gobletPos = new Vector3(-0.12f * s, 0.35f * s, 0.05f * s);
        float gobletBaseR = 0.025f * s;
        // Base
        AddBoxToSurfaceTool(surfaceTool, gobletPos, new Vector3(gobletBaseR * 2.6f, 0.015f * s, gobletBaseR * 2.6f));
        // Stem
        AddBoxToSurfaceTool(surfaceTool, gobletPos + new Vector3(0, 0.025f * s, 0), new Vector3(gobletBaseR * 0.8f, 0.03f * s, gobletBaseR * 0.8f));
        // Cup
        AddBoxToSurfaceTool(surfaceTool, gobletPos + new Vector3(0, 0.055f * s, 0), new Vector3(0.08f * s, 0.04f * s, 0.08f * s));

        // Crown sitting on treasure
        Vector3 crownPos = new Vector3(0.1f * s, 0.42f * s, -0.02f * s);
        float crownRadius = 0.05f * s;
        float crownHeight = 0.035f * s;
        // Crown band (octagonal approximated with box)
        AddBoxToSurfaceTool(surfaceTool, crownPos, new Vector3(crownRadius * 2f, crownHeight, crownRadius * 2f));
        // Crown spikes (4 points)
        for (int spike = 0; spike < 4; spike++)
        {
            float spikeAngle = spike * Mathf.Tau / 4f;
            Vector3 spikeBase = crownPos + new Vector3(Mathf.Cos(spikeAngle) * crownRadius * 0.8f, crownHeight * 0.5f, Mathf.Sin(spikeAngle) * crownRadius * 0.8f);
            AddBoxToSurfaceTool(surfaceTool, spikeBase + new Vector3(0, 0.02f * s, 0), new Vector3(0.015f * s, 0.04f * s, 0.015f * s));
            // Jewel on spike tip
            AddSphereToSurfaceTool(surfaceTool, spikeBase + new Vector3(0, 0.045f * s, 0), 0.008f * s);
        }

        // Pearl necklace draped over edge
        for (int pearl = 0; pearl < 7; pearl++)
        {
            float pearlX = -0.2f * s + pearl * 0.025f * s;
            float pearlY = 0.42f * s - Mathf.Abs(pearl - 3) * 0.015f * s;
            float pearlZ = 0.22f * s + Mathf.Sin(pearl * 0.5f) * 0.02f * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(pearlX, pearlY, pearlZ), 0.012f * s);
        }

        // Gemstones (5-7 gems scattered among coins)
        int numGems = 5 + (int)(rng.Randf() * 3);
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
            float z = (rng.Randf() - 0.5f) * 0.15f * s;
            Vector3 gemPos = new Vector3(x, 0.40f * s, z);

            // Small gem (tiny double pyramid)
            float gemSize = 0.035f * s;
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

        // Multiple mushrooms of varying sizes (more variety)
        int count = rng.RandiRange(6, 10);
        for (int i = 0; i < count; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.03f, 0.25f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float sizeVar = rng.RandfRange(0.5f, 1.5f);
            float stemH = rng.RandfRange(0.08f, 0.3f) * s * sizeVar;
            float stemR = 0.02f * s * sizeVar;
            float capR = rng.RandfRange(0.05f, 0.1f) * s * sizeVar;

            // Stem - cylindrical for organic look
            AddCylinderToSurfaceTool(surfaceTool, new Vector3(x, stemH / 2, z), stemR, stemR * 0.8f, stemH);

            // Cap (flattened dome)
            float capY = stemH + capR * 0.3f;
            AddCylinderToSurfaceTool(surfaceTool, new Vector3(x, capY, z), capR * 0.3f, capR, capR * 0.4f);
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, capY + capR * 0.15f, z), capR * 0.6f);

            // Gills under cap
            for (int g = 0; g < 8; g++)
            {
                float gillAngle = g * Mathf.Tau / 8;
                float gillX = x + Mathf.Cos(gillAngle) * capR * 0.5f;
                float gillZ = z + Mathf.Sin(gillAngle) * capR * 0.5f;
                AddBoxToSurfaceTool(surfaceTool, new Vector3(gillX, stemH + 0.005f * s, gillZ),
                    new Vector3(0.003f * s * sizeVar, 0.01f * s * sizeVar, capR * 0.4f));
            }

            // Spots on larger caps
            if (sizeVar > 1.0f)
            {
                int numSpots = rng.RandiRange(3, 6);
                for (int sp = 0; sp < numSpots; sp++)
                {
                    float spotAngle = rng.Randf() * Mathf.Tau;
                    float spotDist = rng.RandfRange(0.2f, 0.7f) * capR;
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(x + Mathf.Cos(spotAngle) * spotDist, capY + capR * 0.3f, z + Mathf.Sin(spotAngle) * spotDist), 0.008f * s * sizeVar);
                }
            }
        }

        // Spore clouds
        for (int sp = 0; sp < rng.RandiRange(8, 15); sp++)
        {
            float sporeAngle = rng.Randf() * Mathf.Tau;
            float sporeDist = rng.RandfRange(0.05f, 0.3f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(Mathf.Cos(sporeAngle) * sporeDist, rng.RandfRange(0.1f, 0.35f) * s, Mathf.Sin(sporeAngle) * sporeDist), rng.RandfRange(0.005f, 0.015f) * s);
        }

        // Ground mycelium
        for (int m = 0; m < rng.RandiRange(4, 8); m++)
        {
            float mycAngle = rng.Randf() * Mathf.Tau;
            float mycDist = rng.RandfRange(0.1f, 0.25f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(Mathf.Cos(mycAngle) * mycDist, 0.002f * s, Mathf.Sin(mycAngle) * mycDist),
                new Vector3(rng.RandfRange(0.02f, 0.06f) * s, 0.003f * s, 0.005f * s));
        }

        // Baby mushrooms
        for (int b = 0; b < rng.RandiRange(3, 6); b++)
        {
            float babyAngle = rng.Randf() * Mathf.Tau;
            float babyDist = rng.RandfRange(0.15f, 0.28f) * s;
            float babyX = Mathf.Cos(babyAngle) * babyDist;
            float babyZ = Mathf.Sin(babyAngle) * babyDist;
            float babyH = rng.RandfRange(0.02f, 0.04f) * s;
            AddCylinderToSurfaceTool(surfaceTool, new Vector3(babyX, babyH / 2, babyZ), 0.008f * s, 0.006f * s, babyH);
            AddSphereToSurfaceTool(surfaceTool, new Vector3(babyX, babyH + 0.01f * s, babyZ), 0.015f * s);
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

        // Recessed floor pit (darker inside)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, -0.05f * s, 0), new Vector3(1f * s, 0.1f * s, 1f * s));

        // Outer frame/edge with beveled corners
        AddBoxToSurfaceTool(surfaceTool, new Vector3(-0.48f * s, 0.02f * s, 0), new Vector3(0.06f * s, 0.06f * s, 0.96f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0.48f * s, 0.02f * s, 0), new Vector3(0.06f * s, 0.06f * s, 0.96f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, -0.48f * s), new Vector3(0.96f * s, 0.06f * s, 0.06f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, 0.48f * s), new Vector3(0.96f * s, 0.06f * s, 0.06f * s));

        // Corner reinforcements
        for (int cx = -1; cx <= 1; cx += 2)
        {
            for (int cz = -1; cz <= 1; cz += 2)
            {
                AddBoxToSurfaceTool(surfaceTool, new Vector3(cx * 0.45f * s, 0.03f * s, cz * 0.45f * s),
                    new Vector3(0.08f * s, 0.08f * s, 0.08f * s));
            }
        }

        // Pressure plate mechanism (articulated center plate)
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.008f * s, 0), new Vector3(0.65f * s, 0.015f * s, 0.65f * s));
        // Pressure plate edge grooves
        for (int side = 0; side < 4; side++)
        {
            float grooveAngle = side * Mathf.Pi / 2;
            float grooveX = Mathf.Cos(grooveAngle) * 0.3f * s;
            float grooveZ = Mathf.Sin(grooveAngle) * 0.3f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(grooveX, 0.002f * s, grooveZ),
                side % 2 == 0 ? new Vector3(0.02f * s, 0.004f * s, 0.5f * s) : new Vector3(0.5f * s, 0.004f * s, 0.02f * s));
        }

        // Visible mechanism gears at corners
        for (int gear = 0; gear < 4; gear++)
        {
            float gearAngle = gear * Mathf.Pi / 2 + Mathf.Pi / 4;
            float gearX = Mathf.Cos(gearAngle) * 0.35f * s;
            float gearZ = Mathf.Sin(gearAngle) * 0.35f * s;
            AddCylinderToSurfaceTool(surfaceTool, new Vector3(gearX, -0.02f * s, gearZ), 0.04f * s, 0.04f * s, 0.015f * s);
            // Gear teeth
            for (int t = 0; t < 6; t++)
            {
                float toothAngle = t * Mathf.Tau / 6;
                AddBoxToSurfaceTool(surfaceTool, new Vector3(gearX + Mathf.Cos(toothAngle) * 0.035f * s, -0.02f * s, gearZ + Mathf.Sin(toothAngle) * 0.035f * s),
                    new Vector3(0.015f * s, 0.012f * s, 0.015f * s));
            }
        }

        // Metal spikes - 5x5 grid with random heights and angles
        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                float sx = (x - 2f) * 0.16f * s;
                float sz = (z - 2f) * 0.16f * s;
                float spikeH = rng.RandfRange(0.25f, 0.5f) * s;
                float spikeBase = rng.RandfRange(0.03f, 0.045f) * s;
                float tilt = rng.RandfRange(-0.03f, 0.03f) * s;

                // Spike as pyramid with slight tilt
                Vector3 top = new Vector3(sx + tilt, 0.01f * s + spikeH, sz + tilt);
                Vector3 b1 = new Vector3(sx - spikeBase, 0.01f * s, sz - spikeBase);
                Vector3 b2 = new Vector3(sx + spikeBase, 0.01f * s, sz - spikeBase);
                Vector3 b3 = new Vector3(sx + spikeBase, 0.01f * s, sz + spikeBase);
                Vector3 b4 = new Vector3(sx - spikeBase, 0.01f * s, sz + spikeBase);
                // Four faces (pyramid)
                surfaceTool.AddVertex(b1);
                surfaceTool.AddVertex(b2);
                surfaceTool.AddVertex(top);

                surfaceTool.AddVertex(b2);
                surfaceTool.AddVertex(b3);
                surfaceTool.AddVertex(top);

                surfaceTool.AddVertex(b3);
                surfaceTool.AddVertex(b4);
                surfaceTool.AddVertex(top);

                surfaceTool.AddVertex(b4);
                surfaceTool.AddVertex(b1);
                surfaceTool.AddVertex(top);

                // Base mounting collar
                AddCylinderToSurfaceTool(surfaceTool, new Vector3(sx, 0.005f * s, sz), spikeBase * 1.2f, spikeBase * 1.2f, 0.01f * s);
            }
        }

        // Blood stains on spikes and dripping down
        for (int i = 0; i < 6; i++)
        {
            float bx = rng.RandfRange(-0.35f, 0.35f) * s;
            float bz = rng.RandfRange(-0.35f, 0.35f) * s;
            float by = rng.RandfRange(0.08f, 0.35f) * s;
            // Blood drip
            AddBoxToSurfaceTool(surfaceTool, new Vector3(bx, by, bz), new Vector3(0.015f * s, rng.RandfRange(0.04f, 0.1f) * s, 0.015f * s));
            // Blood pooling at base
            if (rng.Randf() > 0.5f)
            {
                AddSphereToSurfaceTool(surfaceTool, new Vector3(bx, 0.015f * s, bz), rng.RandfRange(0.02f, 0.04f) * s);
            }
        }

        // Dried blood pool beneath trap
        AddSphereToSurfaceTool(surfaceTool, new Vector3(rng.RandfRange(-0.1f, 0.1f) * s, 0.005f * s, rng.RandfRange(-0.1f, 0.1f) * s), 0.15f * s);

        // Bone fragment on one spike
        float boneX = (rng.RandiRange(0, 4) - 2f) * 0.16f * s;
        float boneZ = (rng.RandiRange(0, 4) - 2f) * 0.16f * s;
        AddCylinderToSurfaceTool(surfaceTool, new Vector3(boneX, 0.18f * s, boneZ), 0.012f * s, 0.008f * s, 0.06f * s);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.15f, 0.18f),
            Roughness = 0.4f,
            Metallic = 0.85f
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

        // Main parchment with slight wave/curl - normals handled by GenerateNormals()
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

        // Left roll (slightly raised) - normals handled by GenerateNormals()
        for (int i = 0; i < 8; i++)
        {
            float angle1 = i * Mathf.Tau / 8;
            float angle2 = (i + 1) * Mathf.Tau / 8;
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 - 0.01f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 + 0.02f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 + 0.02f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));

            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 - 0.01f * s, rollY + Mathf.Sin(angle1) * rollRadius, Mathf.Cos(angle1) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 + 0.02f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));
            surfaceTool.AddVertex(new Vector3(-scrollWidth / 2 - 0.01f * s, rollY + Mathf.Sin(angle2) * rollRadius, Mathf.Cos(angle2) * rollRadius));
        }

        // Right roll - normals handled by GenerateNormals()
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
            surfaceTool.AddVertex(bl);
            surfaceTool.AddVertex(tl);
            surfaceTool.AddVertex(tr);
            surfaceTool.AddVertex(bl);
            surfaceTool.AddVertex(tr);
            surfaceTool.AddVertex(br);
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

        // === LARGE FOUNDATION STONES (irregular pile base) ===
        int largeRocks = rng.RandiRange(4, 6);
        for (int i = 0; i < largeRocks; i++)
        {
            float angle = i * Mathf.Tau / largeRocks + rng.RandfRange(-0.3f, 0.3f);
            float dist = rng.RandfRange(0.15f, 0.35f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.06f, 0.12f) * s;
            float rockR = rng.RandfRange(0.10f, 0.18f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, y, z), rockR);
        }

        // === MEDIUM STONES (varied sizes piling up) ===
        int medRocks = rng.RandiRange(6, 10);
        for (int i = 0; i < medRocks; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0f, 0.3f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = (0.35f - dist / s) * 0.6f * s + rng.RandfRange(0f, 0.08f) * s;
            float rockR = rng.RandfRange(0.05f, 0.10f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, y, z), rockR);
        }

        // === SMALL DEBRIS STONES (scattered around edges) ===
        int smallRocks = rng.RandiRange(8, 12);
        for (int i = 0; i < smallRocks; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.25f, 0.55f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.01f, 0.04f) * s;
            float rockR = rng.RandfRange(0.02f, 0.05f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, y, z), rockR);
        }

        // === BROKEN BRICKS (rectangular debris) ===
        int bricks = rng.RandiRange(3, 6);
        for (int i = 0; i < bricks; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.1f, 0.4f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.02f, 0.15f) * s;
            float brickW = rng.RandfRange(0.06f, 0.12f) * s;
            float brickH = rng.RandfRange(0.03f, 0.06f) * s;
            float brickD = rng.RandfRange(0.04f, 0.08f) * s;
            float rot = rng.Randf() * Mathf.Tau;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z), new Vector3(brickW, brickH, brickD), rot);
        }

        // === WOODEN BEAM FRAGMENTS (charred/broken) ===
        int beams = rng.RandiRange(2, 4);
        for (int i = 0; i < beams; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.35f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.04f, 0.12f) * s;
            float beamLen = rng.RandfRange(0.15f, 0.30f) * s;
            float beamThick = rng.RandfRange(0.02f, 0.04f) * s;
            float rot = rng.Randf() * Mathf.Tau;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z), new Vector3(beamLen, beamThick, beamThick), rot);
            // Splintered end
            float endX = x + Mathf.Cos(rot) * beamLen * 0.4f;
            float endZ = z + Mathf.Sin(rot) * beamLen * 0.4f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(endX, y + beamThick * 0.3f, endZ),
                new Vector3(beamThick * 0.8f, beamThick * 0.6f, beamThick * 0.5f));
        }

        // === DUST MOUNDS (low flat shapes around base) ===
        int dustPiles = rng.RandiRange(4, 7);
        for (int i = 0; i < dustPiles; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.3f, 0.6f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float dustR = rng.RandfRange(0.04f, 0.08f) * s;
            // Flat dust mound (box shape)
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.01f * s, z),
                new Vector3(dustR * 2f, 0.015f * s, dustR * 1.5f));
        }

        // === METAL DEBRIS (small bent pieces) ===
        int metalPieces = rng.RandiRange(1, 3);
        for (int i = 0; i < metalPieces; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.15f, 0.45f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float y = rng.RandfRange(0.02f, 0.08f) * s;
            // Bent metal bar
            float barLen = rng.RandfRange(0.08f, 0.15f) * s;
            float rot = rng.Randf() * Mathf.Tau;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                new Vector3(barLen, 0.015f * s, 0.02f * s), rot);
            // Bent section
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x + 0.02f * s, y + 0.02f * s, z),
                new Vector3(barLen * 0.4f, 0.015f * s, 0.02f * s), rot + 0.8f);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.38f, 0.35f, 0.32f), // Mixed stone/dirt
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

        // === MAIN TWISTED VINE STEMS (floor-crawling with height variation) ===
        int mainVines = rng.RandiRange(4, 6);
        for (int v = 0; v < mainVines; v++)
        {
            float startAngle = v * Mathf.Tau / mainVines + rng.RandfRange(-0.3f, 0.3f);
            float len = rng.RandfRange(0.35f, 0.55f) * s;
            float thickness = rng.RandfRange(0.025f, 0.04f) * s;
            float twistRate = rng.RandfRange(0.4f, 0.8f);

            // More segments for smoother curves
            int segments = rng.RandiRange(7, 10);
            for (int seg = 0; seg < segments; seg++)
            {
                float t = seg / (float)segments;
                float angle = startAngle + t * twistRate;
                float dist = t * len;
                float x = Mathf.Cos(angle) * dist;
                float z = Mathf.Sin(angle) * dist;
                // Undulating height pattern
                float y = 0.015f * s + Mathf.Sin(t * Mathf.Pi * 2f) * 0.04f * s + Mathf.Sin(t * Mathf.Pi) * 0.03f * s;
                float segThick = thickness * (1f - t * 0.4f); // Thinner at tips
                float rot = angle + Mathf.Pi * 0.5f;
                AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                    new Vector3(0.06f * s, segThick, segThick), rot);

                // === SHARP THORNS (alternating sides, pointing outward) ===
                if (seg % 2 == 0 && seg > 0)
                {
                    // Left-side thorn
                    float thornAngle = angle - Mathf.Pi * 0.4f;
                    float thornX = x + Mathf.Cos(thornAngle) * 0.025f * s;
                    float thornZ = z + Mathf.Sin(thornAngle) * 0.025f * s;
                    float thornLen = rng.RandfRange(0.015f, 0.025f) * s;
                    AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(thornX, y + 0.01f * s, thornZ),
                        new Vector3(thornLen, 0.008f * s, 0.006f * s), thornAngle);
                    // Thorn tip (smaller)
                    AddBoxToSurfaceTool(surfaceTool, new Vector3(thornX + Mathf.Cos(thornAngle) * thornLen * 0.4f,
                        y + 0.015f * s, thornZ + Mathf.Sin(thornAngle) * thornLen * 0.4f),
                        new Vector3(0.004f * s, 0.004f * s, 0.004f * s));
                }
                if (seg % 2 == 1)
                {
                    // Right-side thorn
                    float thornAngle = angle + Mathf.Pi * 0.4f;
                    float thornX = x + Mathf.Cos(thornAngle) * 0.025f * s;
                    float thornZ = z + Mathf.Sin(thornAngle) * 0.025f * s;
                    float thornLen = rng.RandfRange(0.012f, 0.022f) * s;
                    AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(thornX, y + 0.008f * s, thornZ),
                        new Vector3(thornLen, 0.007f * s, 0.005f * s), thornAngle);
                }

                // === LEAVES (at random segments, small oval shapes) ===
                if (rng.Randf() < 0.3f && seg > 1)
                {
                    float leafAngle = angle + rng.RandfRange(-0.5f, 0.5f);
                    float leafDist = 0.03f * s;
                    float leafX = x + Mathf.Cos(leafAngle) * leafDist;
                    float leafZ = z + Mathf.Sin(leafAngle) * leafDist;
                    float leafW = rng.RandfRange(0.02f, 0.035f) * s;
                    float leafH = 0.004f * s;
                    float leafD = leafW * 0.6f;
                    AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(leafX, y + 0.01f * s, leafZ),
                        new Vector3(leafW, leafH, leafD), leafAngle);
                }
            }
        }

        // === SECONDARY CLIMBING VINES (vertical tendrils, as if climbing wall) ===
        int climbingVines = rng.RandiRange(2, 4);
        for (int cv = 0; cv < climbingVines; cv++)
        {
            float baseAngle = rng.Randf() * Mathf.Tau;
            float baseDist = rng.RandfRange(0.25f, 0.4f) * s;
            float baseX = Mathf.Cos(baseAngle) * baseDist;
            float baseZ = Mathf.Sin(baseAngle) * baseDist;
            float climbHeight = rng.RandfRange(0.15f, 0.3f) * s;

            // Vertical segments
            int climbSegs = rng.RandiRange(4, 6);
            for (int cs = 0; cs < climbSegs; cs++)
            {
                float t = cs / (float)climbSegs;
                float x = baseX + Mathf.Sin(t * Mathf.Pi * 2f) * 0.03f * s;
                float z = baseZ + Mathf.Cos(t * Mathf.Pi * 3f) * 0.02f * s;
                float y = t * climbHeight + 0.02f * s;
                float segThick = 0.018f * s * (1f - t * 0.5f);
                AddBoxToSurfaceTool(surfaceTool, new Vector3(x, y, z),
                    new Vector3(segThick, 0.04f * s, segThick));

                // Small climbing thorns
                if (cs % 2 == 0)
                {
                    AddBoxToSurfaceTool(surfaceTool, new Vector3(x + 0.015f * s, y, z),
                        new Vector3(0.012f * s, 0.006f * s, 0.005f * s));
                }
            }
        }

        // === BERRIES (small clusters, dark red/purple) ===
        int berryClusters = rng.RandiRange(2, 4);
        for (int bc = 0; bc < berryClusters; bc++)
        {
            float clusterAngle = rng.Randf() * Mathf.Tau;
            float clusterDist = rng.RandfRange(0.1f, 0.35f) * s;
            float clusterX = Mathf.Cos(clusterAngle) * clusterDist;
            float clusterZ = Mathf.Sin(clusterAngle) * clusterDist;
            float clusterY = rng.RandfRange(0.03f, 0.08f) * s;

            // 3-5 berries per cluster
            int berries = rng.RandiRange(3, 5);
            for (int b = 0; b < berries; b++)
            {
                float bAngle = b * Mathf.Tau / berries;
                float bDist = 0.012f * s;
                float bx = clusterX + Mathf.Cos(bAngle) * bDist;
                float bz = clusterZ + Mathf.Sin(bAngle) * bDist;
                float by = clusterY + rng.RandfRange(-0.005f, 0.01f) * s;
                float berryR = rng.RandfRange(0.006f, 0.009f) * s;
                AddSphereToSurfaceTool(surfaceTool, new Vector3(bx, by, bz), berryR);
            }
        }

        // === GROUND TENDRILS (small creeping roots) ===
        int tendrils = rng.RandiRange(5, 8);
        for (int td = 0; td < tendrils; td++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.2f, 0.5f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float tendrilLen = rng.RandfRange(0.04f, 0.08f) * s;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.008f * s, z),
                new Vector3(tendrilLen, 0.008f * s, 0.012f * s), angle);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.32f, 0.18f), // Dark forest green
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

        // === STONE BASE (weathered, irregular edges) ===
        // Main stone slab
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, -0.01f * s, 0), new Vector3(0.85f * s, 0.05f * s, 0.85f * s));
        // Beveled/worn edges (smaller boxes around perimeter)
        float edgeOffset = 0.38f * s;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4;
            float ex = Mathf.Cos(angle) * edgeOffset;
            float ez = Mathf.Sin(angle) * edgeOffset;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(ex, -0.015f * s, ez),
                new Vector3(0.12f * s, 0.04f * s, 0.12f * s));
        }
        // Corner wear/chips
        for (int c = 0; c < 4; c++)
        {
            float cornerAngle = c * Mathf.Tau / 4 + Mathf.Tau / 8;
            float cx = Mathf.Cos(cornerAngle) * 0.52f * s;
            float cz = Mathf.Sin(cornerAngle) * 0.52f * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(cx, -0.02f * s, cz),
                new Vector3(0.08f * s, 0.03f * s, 0.08f * s));
        }

        // === CARVED CHANNEL DEPTH (recessed rune grooves) ===
        float carveDepth = 0.018f * s;
        float carveY = 0.025f * s;

        // Outer circle channel (12 segments for detail)
        float outerR = 0.28f * s;
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12;
            float nextAngle = (i + 1) * Mathf.Tau / 12;
            float midAngle = (angle + nextAngle) / 2f;
            float segX = Mathf.Cos(midAngle) * outerR;
            float segZ = Mathf.Sin(midAngle) * outerR;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(segX, carveY, segZ),
                new Vector3(0.16f * s, carveDepth, 0.035f * s), midAngle + Mathf.Pi * 0.5f);
        }

        // Inner circle (magical core)
        float innerR = 0.12f * s;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8;
            float nextAngle = (i + 1) * Mathf.Tau / 8;
            float midAngle = (angle + nextAngle) / 2f;
            float segX = Mathf.Cos(midAngle) * innerR;
            float segZ = Mathf.Sin(midAngle) * innerR;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(segX, carveY + 0.005f * s, segZ),
                new Vector3(0.10f * s, carveDepth, 0.025f * s), midAngle + Mathf.Pi * 0.5f);
        }

        // === GLOWING RUNE SYMBOLS (arcane patterns) ===
        // Primary cross/cardinal lines
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, carveY + 0.008f * s, 0),
            new Vector3(0.55f * s, 0.012f * s, 0.035f * s));
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, carveY + 0.008f * s, 0),
            new Vector3(0.035f * s, 0.012f * s, 0.55f * s));

        // Diagonal rune lines
        for (int d = 0; d < 4; d++)
        {
            float diagAngle = d * Mathf.Tau / 4 + Mathf.Tau / 8;
            float diagLen = 0.2f * s;
            float diagDist = 0.18f * s;
            float dx = Mathf.Cos(diagAngle) * diagDist;
            float dz = Mathf.Sin(diagAngle) * diagDist;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(dx, carveY + 0.01f * s, dz),
                new Vector3(diagLen, 0.01f * s, 0.025f * s), diagAngle);
        }

        // Rune endpoint symbols (small boxes at line ends)
        float[] runeEndpoints = { 0.25f, -0.25f };
        foreach (float xEnd in runeEndpoints)
        {
            AddBoxToSurfaceTool(surfaceTool, new Vector3(xEnd * s, carveY + 0.012f * s, 0),
                new Vector3(0.04f * s, 0.015f * s, 0.06f * s));
        }
        foreach (float zEnd in runeEndpoints)
        {
            AddBoxToSurfaceTool(surfaceTool, new Vector3(0, carveY + 0.012f * s, zEnd * s),
                new Vector3(0.06f * s, 0.015f * s, 0.04f * s));
        }

        // === CENTER FOCUS GEM (power crystal) ===
        float gemR = 0.035f * s;
        AddSphereToSurfaceTool(surfaceTool, new Vector3(0, carveY + 0.025f * s, 0), gemR);
        // Gem facets (small boxes around gem)
        for (int f = 0; f < 6; f++)
        {
            float facetAngle = f * Mathf.Tau / 6;
            float fx = Mathf.Cos(facetAngle) * gemR * 0.7f;
            float fz = Mathf.Sin(facetAngle) * gemR * 0.7f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(fx, carveY + 0.03f * s, fz),
                new Vector3(0.012f * s, 0.01f * s, 0.008f * s));
        }

        // === MAGICAL PARTICLE MOTES (floating around rune) ===
        int motes = rng.RandiRange(5, 8);
        for (int m = 0; m < motes; m++)
        {
            float mAngle = rng.Randf() * Mathf.Tau;
            float mDist = rng.RandfRange(0.08f, 0.3f) * s;
            float mx = Mathf.Cos(mAngle) * mDist;
            float mz = Mathf.Sin(mAngle) * mDist;
            float my = rng.RandfRange(0.05f, 0.15f) * s;
            float moteR = rng.RandfRange(0.008f, 0.015f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(mx, my, mz), moteR);
        }

        // === DUST/DEBRIS around edges ===
        int dustBits = rng.RandiRange(4, 7);
        for (int db = 0; db < dustBits; db++)
        {
            float dustAngle = rng.Randf() * Mathf.Tau;
            float dustDist = rng.RandfRange(0.35f, 0.5f) * s;
            float dustX = Mathf.Cos(dustAngle) * dustDist;
            float dustZ = Mathf.Sin(dustAngle) * dustDist;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(dustX, -0.005f * s, dustZ),
                new Vector3(0.025f * s, 0.01f * s, 0.02f * s));
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.32f, 0.30f, 0.28f), // Weathered stone
            Roughness = 0.75f,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.15f, 0.08f), // Deep red glow
            EmissionEnergyMultiplier = 1.2f
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateAbandonedCampfireMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // === STONE RING (irregular sizes, some displaced) ===
        int stones = rng.RandiRange(8, 12);
        for (int i = 0; i < stones; i++)
        {
            float angle = i * Mathf.Tau / stones + rng.RandfRange(-0.15f, 0.15f);
            float dist = 0.35f * s + rng.RandfRange(-0.04f, 0.04f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float stoneR = rng.RandfRange(0.06f, 0.10f) * s;
            float stoneY = rng.RandfRange(0.03f, 0.07f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, stoneY, z), stoneR);
        }
        // A few tumbled/rolled away stones
        int fallenStones = rng.RandiRange(2, 4);
        for (int fs = 0; fs < fallenStones; fs++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.42f, 0.55f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float stoneR = rng.RandfRange(0.04f, 0.07f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, stoneR * 0.6f, z), stoneR);
        }

        // === COLD ASH BED (layered, textured) ===
        // Base ash layer
        float ashR = 0.28f * s;
        for (int i = 0; i < 8; i++)
        {
            float angle1 = i * Mathf.Tau / 8;
            float angle2 = (i + 1) * Mathf.Tau / 8;
            surfaceTool.AddVertex(new Vector3(0, 0.008f * s, 0));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * ashR, 0.008f * s, Mathf.Sin(angle1) * ashR));
            surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * ashR, 0.008f * s, Mathf.Sin(angle2) * ashR));
        }
        // Ash mounds (piled up areas)
        int ashMounds = rng.RandiRange(4, 6);
        for (int am = 0; am < ashMounds; am++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.18f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float moundR = rng.RandfRange(0.04f, 0.08f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.015f * s, z),
                new Vector3(moundR * 1.5f, 0.02f * s, moundR));
        }

        // === CHARRED LOGS (main burnt wood pieces) ===
        int logs = rng.RandiRange(3, 5);
        for (int lg = 0; lg < logs; lg++)
        {
            float angle = lg * Mathf.Tau / logs + rng.RandfRange(-0.3f, 0.3f);
            float dist = rng.RandfRange(0.02f, 0.12f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float logLen = rng.RandfRange(0.12f, 0.22f) * s;
            float logR = rng.RandfRange(0.025f, 0.04f) * s;
            float logRot = rng.Randf() * Mathf.Tau;
            float logY = 0.02f * s + logR;
            // Log body
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, logY, z),
                new Vector3(logLen, logR * 2f, logR * 1.8f), logRot);
            // Charred/cracked end
            float endX = x + Mathf.Cos(logRot) * logLen * 0.45f;
            float endZ = z + Mathf.Sin(logRot) * logLen * 0.45f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(endX, logY, endZ),
                new Vector3(logR * 0.8f, logR * 1.5f, logR * 1.2f));
            // Extending past fire ring (partial burn)
            if (rng.Randf() < 0.4f)
            {
                float extX = x - Mathf.Cos(logRot) * logLen * 0.35f;
                float extZ = z - Mathf.Sin(logRot) * logLen * 0.35f;
                AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(extX, logY * 0.8f, extZ),
                    new Vector3(logLen * 0.3f, logR * 1.6f, logR * 1.5f), logRot);
            }
        }

        // === SCATTERED BONES (old meal remnants) ===
        int bones = rng.RandiRange(3, 6);
        for (int bn = 0; bn < bones; bn++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.2f, 0.5f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float boneLen = rng.RandfRange(0.04f, 0.08f) * s;
            float boneR = rng.RandfRange(0.006f, 0.012f) * s;
            float boneRot = rng.Randf() * Mathf.Tau;
            // Bone shaft
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, boneR + 0.005f * s, z),
                new Vector3(boneLen, boneR * 2f, boneR * 2f), boneRot);
            // Bone ends (knobs)
            float end1X = x + Mathf.Cos(boneRot) * boneLen * 0.4f;
            float end1Z = z + Mathf.Sin(boneRot) * boneLen * 0.4f;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(end1X, boneR * 1.5f + 0.005f * s, end1Z), boneR * 1.3f);
            float end2X = x - Mathf.Cos(boneRot) * boneLen * 0.4f;
            float end2Z = z - Mathf.Sin(boneRot) * boneLen * 0.4f;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(end2X, boneR * 1.5f + 0.005f * s, end2Z), boneR * 1.2f);
        }

        // === OLD BEDROLL (tattered cloth/leather roll) ===
        float bedrollAngle = rng.Randf() * Mathf.Tau;
        float bedrollDist = rng.RandfRange(0.45f, 0.6f) * s;
        float brX = Mathf.Cos(bedrollAngle) * bedrollDist;
        float brZ = Mathf.Sin(bedrollAngle) * bedrollDist;
        // Main bedroll body
        AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(brX, 0.04f * s, brZ),
            new Vector3(0.25f * s, 0.05f * s, 0.12f * s), bedrollAngle + Mathf.Pi * 0.5f);
        // Rolled end/pillow
        float pillowX = brX + Mathf.Cos(bedrollAngle + Mathf.Pi * 0.5f) * 0.1f * s;
        float pillowZ = brZ + Mathf.Sin(bedrollAngle + Mathf.Pi * 0.5f) * 0.1f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(pillowX, 0.055f * s, pillowZ),
            new Vector3(0.08f * s, 0.06f * s, 0.1f * s));
        // Unrolled flap
        float flapX = brX - Mathf.Cos(bedrollAngle + Mathf.Pi * 0.5f) * 0.12f * s;
        float flapZ = brZ - Mathf.Sin(bedrollAngle + Mathf.Pi * 0.5f) * 0.12f * s;
        AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(flapX, 0.01f * s, flapZ),
            new Vector3(0.12f * s, 0.015f * s, 0.1f * s), bedrollAngle);

        // === SCATTERED DEBRIS (twigs, small rocks) ===
        int debris = rng.RandiRange(6, 10);
        for (int db = 0; db < debris; db++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.15f, 0.55f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            if (rng.Randf() < 0.6f)
            {
                // Twig
                float twigLen = rng.RandfRange(0.03f, 0.06f) * s;
                AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.008f * s, z),
                    new Vector3(twigLen, 0.008f * s, 0.006f * s), rng.Randf() * Mathf.Tau);
            }
            else
            {
                // Small rock
                float rockR = rng.RandfRange(0.015f, 0.025f) * s;
                AddSphereToSurfaceTool(surfaceTool, new Vector3(x, rockR * 0.7f, z), rockR);
            }
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.14f, 0.11f, 0.09f), // Deep charred/ash
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

        // === MAIN VESSEL REMNANT (partial pot base) ===
        float baseR = rng.RandfRange(0.08f, 0.12f) * s;
        float baseH = rng.RandfRange(0.04f, 0.07f) * s;
        for (int seg = 0; seg < 5; seg++)
        {
            float angle = seg * 0.8f - 0.2f;
            float segX = Mathf.Cos(angle) * baseR * 0.8f;
            float segZ = Mathf.Sin(angle) * baseR * 0.8f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(segX, baseH * 0.5f, segZ),
                new Vector3(0.04f * s, baseH, 0.025f * s));
        }
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.01f * s, 0),
            new Vector3(baseR * 1.4f, 0.015f * s, baseR * 1.2f));

        // === LARGE SHARDS (with decorative patterns) ===
        int largeShards = rng.RandiRange(3, 5);
        for (int ls = 0; ls < largeShards; ls++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.12f, 0.28f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float shardLen = rng.RandfRange(0.06f, 0.12f) * s;
            float shardH = rng.RandfRange(0.025f, 0.045f) * s;
            float shardThick = rng.RandfRange(0.012f, 0.02f) * s;
            float shardRot = rng.Randf() * Mathf.Tau;
            float shardTilt = rng.RandfRange(0f, 0.3f);
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, shardH * 0.5f + shardTilt * 0.02f * s, z),
                new Vector3(shardLen, shardThick, shardH), shardRot);
            if (rng.Randf() < 0.6f)
            {
                float bandY = shardH * 0.3f + shardTilt * 0.02f * s;
                AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, bandY + 0.008f * s, z),
                    new Vector3(shardLen * 0.9f, 0.006f * s, 0.008f * s), shardRot);
            }
        }

        // === MEDIUM SHARDS ===
        int medShards = rng.RandiRange(5, 8);
        for (int ms = 0; ms < medShards; ms++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.08f, 0.35f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float shardW = rng.RandfRange(0.03f, 0.06f) * s;
            float shardH = rng.RandfRange(0.015f, 0.03f) * s;
            float shardD = shardW * rng.RandfRange(0.5f, 0.8f);
            float shardRot = rng.Randf() * Mathf.Tau;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(x, shardH * 0.4f, z),
                new Vector3(shardW, shardH, shardD), shardRot);
        }

        // === TINY FRAGMENTS ===
        int tinyPieces = rng.RandiRange(8, 14);
        for (int tp = 0; tp < tinyPieces; tp++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.4f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float chipSize = rng.RandfRange(0.008f, 0.018f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, chipSize * 0.3f, z),
                new Vector3(chipSize, chipSize * 0.6f, chipSize * 0.8f));
        }

        // === SPILLED CONTENTS (grain/seeds) ===
        float spillAngle = rng.Randf() * Mathf.Tau;
        float spillDist = rng.RandfRange(0.1f, 0.2f) * s;
        float spillX = Mathf.Cos(spillAngle) * spillDist;
        float spillZ = Mathf.Sin(spillAngle) * spillDist;
        AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(spillX, 0.003f * s, spillZ),
            new Vector3(0.12f * s, 0.004f * s, 0.08f * s), spillAngle);
        int grains = rng.RandiRange(6, 12);
        for (int gr = 0; gr < grains; gr++)
        {
            float gAngle = spillAngle + rng.RandfRange(-0.6f, 0.6f);
            float gDist = rng.RandfRange(0.08f, 0.25f) * s;
            float gx = Mathf.Cos(gAngle) * gDist;
            float gz = Mathf.Sin(gAngle) * gDist;
            float grainR = rng.RandfRange(0.004f, 0.008f) * s;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(gx, grainR * 0.6f, gz), grainR);
        }

        // === DUST ACCUMULATION ===
        int dustPatches = rng.RandiRange(4, 7);
        for (int dp = 0; dp < dustPatches; dp++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.15f, 0.38f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float dustW = rng.RandfRange(0.025f, 0.05f) * s;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.002f * s, z),
                new Vector3(dustW, 0.004f * s, dustW * 0.7f));
        }

        // === RIM FRAGMENT ===
        float rimAngle = rng.Randf() * Mathf.Tau;
        float rimDist = rng.RandfRange(0.18f, 0.3f) * s;
        float rimX = Mathf.Cos(rimAngle) * rimDist;
        float rimZ = Mathf.Sin(rimAngle) * rimDist;
        for (int r = 0; r < 3; r++)
        {
            float rOff = (r - 1) * 0.025f * s;
            float rx = rimX + Mathf.Cos(rimAngle + Mathf.Pi * 0.5f) * rOff;
            float rz = rimZ + Mathf.Sin(rimAngle + Mathf.Pi * 0.5f) * rOff;
            AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(rx, 0.02f * s, rz),
                new Vector3(0.025f * s, 0.015f * s, 0.012f * s), rimAngle + r * 0.15f);
        }
        AddRotatedBoxToSurfaceTool(surfaceTool, new Vector3(rimX, 0.028f * s, rimZ),
            new Vector3(0.06f * s, 0.008f * s, 0.018f * s), rimAngle);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.68f, 0.52f, 0.38f), // Weathered terracotta
            Roughness = 0.88f
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

        // Determine lichen type based on color for specialized meshes
        bool isCyan = baseColor.R < 0.3f && baseColor.G > 0.5f && baseColor.B > 0.6f;
        bool isPurple = baseColor.R > 0.4f && baseColor.B > 0.6f;
        bool isGreen = baseColor.G > 0.6f && baseColor.R < 0.4f && baseColor.B < 0.5f;

        // CYAN LICHEN: Crusty texture, spreading pattern, raised bumps
        // PURPLE LICHEN: Branching growth, fuzzy edges, bioluminescent hints
        // GREEN LICHEN: Leafy lobes, layered growth, damp appearance
        int patchCount = isCyan ? rng.RandiRange(8, 14) : rng.RandiRange(5, 10);
        for (int i = 0; i < patchCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.25f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float patchSize = rng.RandfRange(0.05f, 0.12f) * s;

            // Specialized geometry based on lichen type
            if (isCyan) { // Crusty raised bumps
                AddSphereToSurfaceTool(surfaceTool, new Vector3(x, rng.RandfRange(0.01f, 0.025f) * s, z), patchSize * 0.6f);
                for (int sat = 0; sat < rng.RandiRange(2, 5); sat++) {
                    float satAngle = rng.Randf() * Mathf.Tau;
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(x + Mathf.Cos(satAngle) * patchSize * rng.RandfRange(0.8f, 1.4f), patchSize * 0.2f, z + Mathf.Sin(satAngle) * patchSize * rng.RandfRange(0.8f, 1.4f)), patchSize * rng.RandfRange(0.2f, 0.4f));
                }
            } else if (isPurple) { // Fuzzy branching
                for (int f = 0; f < rng.RandiRange(4, 7); f++) {
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(Mathf.Cos(angle + f * 0.3f - 0.5f) * dist * (0.5f + f * 0.15f), rng.RandfRange(0.01f, 0.02f) * s, Mathf.Sin(angle + f * 0.3f - 0.5f) * dist * (0.5f + f * 0.15f)), patchSize * rng.RandfRange(0.3f, 0.6f));
                }
                AddSphereToSurfaceTool(surfaceTool, new Vector3(x, 0.025f * s, z), patchSize * 0.5f);
            } else if (isGreen) { // Leafy lobes
                float layerH = rng.RandfRange(0.005f, 0.015f) * s + (i % 3) * 0.008f * s;
                AddSphereToSurfaceTool(surfaceTool, new Vector3(x + Mathf.Cos(angle) * patchSize * 0.8f, layerH, z + Mathf.Sin(angle) * patchSize * 0.8f), patchSize * 0.5f);
            }
            int segments = rng.RandiRange(5, 8);
            Vector3 center = new Vector3(x, isGreen ? (rng.RandfRange(0.005f, 0.015f) * s + (i % 3) * 0.008f * s) : 0.01f * s, z);
            for (int j = 0; j < segments; j++)
            {
                float a1 = j * Mathf.Tau / segments;
                float a2 = (j + 1) * Mathf.Tau / segments;
                float r1 = patchSize * (0.7f + rng.Randf() * 0.6f);
                float r2 = patchSize * (0.7f + rng.Randf() * 0.6f);

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
            Roughness = isGreen ? 0.5f : 0.8f, // Green lichen looks damp/wet
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

        // Determine fungus type: Orange=shelf brackets, Blue=cup-shaped, Pink=coral branches
        bool isOrange = baseColor.R > 0.8f && baseColor.G > 0.3f && baseColor.G < 0.7f;
        bool isBlue = baseColor.B > 0.7f && baseColor.R < 0.5f;
        bool isPink = baseColor.R > 0.8f && baseColor.B > 0.4f;

        int fungusCount = isBlue ? rng.RandiRange(5, 9) : (isPink ? rng.RandiRange(6, 10) : rng.RandiRange(3, 6));
        for (int i = 0; i < fungusCount; i++)
        {
            // Blue: Cup-shaped with spore dust, slimy texture
            if (isBlue) {
                float cupX = rng.RandfRange(-0.2f, 0.2f) * s;
                float cupZ = rng.RandfRange(-0.15f, 0.15f) * s;
                float cupY = rng.RandfRange(0f, 0.15f) * s;
                float cupSize = rng.RandfRange(0.03f, 0.06f) * s;
                AddSphereToSurfaceTool(surfaceTool, new Vector3(cupX, cupY + cupSize, cupZ), cupSize);
                AddBoxToSurfaceTool(surfaceTool, new Vector3(cupX, cupY + cupSize * 0.3f, cupZ), new Vector3(cupSize * 0.4f, cupSize * 0.6f, cupSize * 0.4f));
                for (int sp = 0; sp < rng.RandiRange(2, 4); sp++)
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(cupX + rng.RandfRange(-0.02f, 0.02f) * s, cupY + cupSize * 1.2f, cupZ + rng.RandfRange(-0.02f, 0.02f) * s), 0.006f * s);
                continue;
            }
            // Pink: Coral-like branches, translucent tips
            if (isPink) {
                float baseX = rng.RandfRange(-0.12f, 0.12f) * s;
                float baseZ = rng.RandfRange(-0.1f, 0.1f) * s;
                float branchH = rng.RandfRange(0.12f, 0.25f) * s;
                float thick = rng.RandfRange(0.012f, 0.02f) * s;
                AddBoxToSurfaceTool(surfaceTool, new Vector3(baseX, branchH / 2, baseZ), new Vector3(thick, branchH, thick));
                AddSphereToSurfaceTool(surfaceTool, new Vector3(baseX, branchH + 0.01f * s, baseZ), thick * 1.5f);
                if (rng.Randf() < 0.7f) {
                    float subAngle = rng.Randf() * Mathf.Tau;
                    float subLen = branchH * 0.4f;
                    AddBoxToSurfaceTool(surfaceTool, new Vector3(baseX + Mathf.Cos(subAngle) * subLen * 0.3f, branchH * 0.6f, baseZ + Mathf.Sin(subAngle) * subLen * 0.3f), new Vector3(thick * 0.6f, subLen, thick * 0.6f));
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(baseX + Mathf.Cos(subAngle) * subLen * 0.5f, branchH * 0.6f + subLen * 0.4f, baseZ + Mathf.Sin(subAngle) * subLen * 0.5f), thick);
                }
                continue;
            }
            // Orange/default: Shelf brackets with porous underside
            float xOff = rng.RandfRange(-0.15f, 0.15f) * s;
            float yOff = rng.RandfRange(0f, 0.3f) * s;
            float zOff = rng.RandfRange(-0.1f, 0.1f) * s;
            float shelfWidth = rng.RandfRange(0.08f, 0.15f) * s;
            float shelfDepth = rng.RandfRange(0.05f, 0.1f) * s;
            float shelfThick = rng.RandfRange(0.02f, 0.04f) * s;

            // Add pore dots for orange fungus
            if (isOrange) {
                for (int p = 0; p < rng.RandiRange(3, 6); p++) {
                    float pAngle = rng.RandfRange(-Mathf.Pi / 2 + 0.3f, Mathf.Pi / 2 - 0.3f);
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(xOff + Mathf.Cos(pAngle) * shelfWidth * 0.5f, yOff - shelfThick * 0.2f, zOff + Mathf.Sin(pAngle) * shelfDepth * 0.5f), 0.008f * s);
                }
            }

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
                surfaceTool.AddVertex(c);
                surfaceTool.AddVertex(p1);
                surfaceTool.AddVertex(p2);

                // Bottom surface (underside)
                Vector3 b1 = basePos + new Vector3(Mathf.Cos(a1) * shelfWidth, 0, Mathf.Sin(a1) * shelfDepth);
                Vector3 b2 = basePos + new Vector3(Mathf.Cos(a2) * shelfWidth, 0, Mathf.Sin(a2) * shelfDepth);
                surfaceTool.AddVertex(basePos);
                surfaceTool.AddVertex(b2);
                surfaceTool.AddVertex(b1);

                // Edge
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
            AlbedoColor = isPink ? new Color(baseColor.R, baseColor.G, baseColor.B, 0.85f) : baseColor,
            EmissionEnabled = true,
            Emission = baseColor.Lightened(0.3f),
            EmissionEnergyMultiplier = isPink ? 2.2f : 1.8f,
            Roughness = isBlue ? 0.3f : 0.6f, // Blue is slimy
            Transparency = isPink ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private ArrayMesh CreateCeilingTendrilsMesh(RandomNumberGenerator rng)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        float s = PropScale;

        // CEILING TENDRILS: Hanging roots/vines, dripping moisture, varied lengths, organic curves
        int tendrilCount = rng.RandiRange(7, 14);
        for (int i = 0; i < tendrilCount; i++)
        {
            float xOff = rng.RandfRange(-0.25f, 0.25f) * s;
            float zOff = rng.RandfRange(-0.25f, 0.25f) * s;
            float length = rng.RandfRange(0.25f, 0.95f) * s; // Varied lengths
            float thickness = rng.RandfRange(0.012f, 0.035f) * s;

            // Organic curved path with acceleration
            int segments = rng.RandiRange(5, 8);
            float segLen = length / segments;
            float curX = xOff, curZ = zOff;
            float curveX = rng.RandfRange(-0.08f, 0.08f) * s;
            float curveZ = rng.RandfRange(-0.08f, 0.08f) * s;

            for (int j = 0; j < segments; j++)
            {
                float y = -j * segLen;
                float nextY = -(j + 1) * segLen;

                // Organic curve with acceleration
                float curveFactor = (float)j / segments;
                curX += curveX * curveFactor + rng.RandfRange(-0.015f, 0.015f) * s;
                curZ += curveZ * curveFactor + rng.RandfRange(-0.015f, 0.015f) * s;

                float t = thickness * (1f - j * 0.12f);
                AddCylinderToSurfaceTool(surfaceTool, new Vector3(curX - curveX * 0.3f, y, curZ - curveZ * 0.3f), new Vector3(curX, nextY, curZ), t);

                // Node bumps at joints
                if (j > 0 && j < segments - 1 && rng.Randf() < 0.4f)
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(curX, y, curZ), t * 1.3f);

                // Sub-tendrils branching off
                if (j > 1 && rng.Randf() < 0.35f) {
                    float subAngle = rng.Randf() * Mathf.Tau;
                    float subLen = rng.RandfRange(0.08f, 0.18f) * s;
                    float subX = curX + Mathf.Cos(subAngle) * subLen * 0.5f;
                    float subZ = curZ + Mathf.Sin(subAngle) * subLen * 0.5f;
                    AddCylinderToSurfaceTool(surfaceTool, new Vector3(curX, y, curZ), new Vector3(subX, y - subLen, subZ), t * 0.5f);
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(subX, y - subLen - 0.01f * s, subZ), t * 0.7f);
                }

                // Glowing tip and dripping moisture
                if (j == segments - 1) {
                    AddSphereToSurfaceTool(surfaceTool, new Vector3(curX, nextY, curZ), t * 1.8f);
                    if (rng.Randf() < 0.5f)
                        AddSphereToSurfaceTool(surfaceTool, new Vector3(curX, nextY - rng.RandfRange(0.02f, 0.05f) * s, curZ), t * 0.5f);
                }
            }
        }

        // Ceiling root mass (where tendrils attach)
        for (int r = 0; r < rng.RandiRange(3, 6); r++)
            AddSphereToSurfaceTool(surfaceTool, new Vector3(rng.RandfRange(-0.15f, 0.15f) * s, 0.02f * s, rng.RandfRange(-0.15f, 0.15f) * s), rng.RandfRange(0.03f, 0.06f) * s);

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

        // FLOOR SPORES: Puffball mushrooms, spore clouds, scattered pattern, soft texture

        // 1. PUFFBALL MUSHROOMS - flattened spheres with stems and openings
        int puffballCount = rng.RandiRange(4, 8);
        for (int i = 0; i < puffballCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.03f, 0.22f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float puffSize = rng.RandfRange(0.03f, 0.07f) * s;

            // Puffball body - slightly flattened sphere (wider than tall)
            float flattenY = rng.RandfRange(0.7f, 0.9f);
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, puffSize * flattenY, z), puffSize);

            // Short stem beneath puffball - using box per procedural-3d-artist patterns
            float stemHeight = puffSize * 0.4f;
            float stemRadius = puffSize * 0.3f;
            AddBoxToSurfaceTool(surfaceTool, new Vector3(x, stemHeight / 2, z), new Vector3(stemRadius * 2, stemHeight, stemRadius * 2));

            // Opening at top (small indent ring) - ring of tiny spheres
            float openingRadius = puffSize * 0.25f;
            int ringPoints = 6;
            for (int r = 0; r < ringPoints; r++)
            {
                float ringAngle = r * Mathf.Tau / ringPoints;
                float rx = x + Mathf.Cos(ringAngle) * openingRadius;
                float rz = z + Mathf.Sin(ringAngle) * openingRadius;
                float topY = puffSize * flattenY * 1.8f;
                AddSphereToSurfaceTool(surfaceTool, new Vector3(rx, topY, rz), puffSize * 0.08f);
            }

            // Spores escaping from opening - tiny floating spheres above
            int escapingSpores = rng.RandiRange(2, 5);
            for (int sp = 0; sp < escapingSpores; sp++)
            {
                float sporeAngle = rng.Randf() * Mathf.Tau;
                float sporeDist = rng.RandfRange(0f, openingRadius * 1.5f);
                float sporeHeight = puffSize * flattenY * 1.8f + rng.RandfRange(0.02f, 0.08f) * s;
                float sporeX = x + Mathf.Cos(sporeAngle) * sporeDist;
                float sporeZ = z + Mathf.Sin(sporeAngle) * sporeDist;
                AddSphereToSurfaceTool(surfaceTool, new Vector3(sporeX, sporeHeight, sporeZ), rng.RandfRange(0.003f, 0.008f) * s);
            }
        }

        // 2. SCATTERED SMALLER SPORE PODS - varied sizes for natural look
        int smallPodCount = rng.RandiRange(10, 18);
        for (int i = 0; i < smallPodCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.05f, 0.28f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float size = rng.RandfRange(0.01f, 0.025f) * s;

            // Simple glowing sphere pods
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, size * 0.8f, z), size);
        }

        // 3. SPORE CLOUD - floating particles in the air
        int cloudSporeCount = rng.RandiRange(15, 30);
        for (int i = 0; i < cloudSporeCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0f, 0.25f) * s;
            float height = rng.RandfRange(0.05f, 0.2f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            float size = rng.RandfRange(0.002f, 0.006f) * s;

            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, height, z), size);
        }

        // 4. GROUND SUBSTRATE - organic base matter
        int substrateCount = rng.RandiRange(5, 10);
        for (int i = 0; i < substrateCount; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist = rng.RandfRange(0.02f, 0.2f) * s;
            float x = Mathf.Cos(angle) * dist;
            float z = Mathf.Sin(angle) * dist;
            AddSphereToSurfaceTool(surfaceTool, new Vector3(x, 0.005f * s, z), rng.RandfRange(0.015f, 0.03f) * s);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.7f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.9f, 0.3f),
            EmissionEnergyMultiplier = 2.5f,
            Roughness = 0.85f // Soft, powdery texture
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

    /// <summary>
    /// Creates a floor grate mesh with metal bars.
    /// </summary>
    private ArrayMesh CreateFloorGrateMesh(RandomNumberGenerator rng)
    {
        float size = 1.5f * PropScale;
        float barWidth = 0.08f * PropScale;
        float barHeight = 0.1f * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Color metalColor = new Color(0.25f, 0.25f, 0.28f);
        Color rustColor = new Color(0.35f, 0.22f, 0.15f);

        // Create grid of bars
        int numBars = 8;
        float spacing = size / numBars;

        // Horizontal bars
        for (int i = 0; i <= numBars; i++)
        {
            float z = -size / 2f + i * spacing;
            Color barColor = rng.Randf() > 0.7f ? rustColor : metalColor;
            AddBox(surfaceTool, new Vector3(0, barHeight / 2f, z),
                new Vector3(size, barHeight, barWidth), barColor);
        }

        // Vertical bars
        for (int i = 0; i <= numBars; i++)
        {
            float x = -size / 2f + i * spacing;
            Color barColor = rng.Randf() > 0.7f ? rustColor : metalColor;
            AddBox(surfaceTool, new Vector3(x, barHeight / 2f, 0),
                new Vector3(barWidth, barHeight, size), barColor);
        }

        // Frame border
        Color frameColor = new Color(0.2f, 0.2f, 0.22f);
        float frameWidth = 0.12f * PropScale;
        AddBox(surfaceTool, new Vector3(0, barHeight / 2f, -size / 2f - frameWidth / 2f),
            new Vector3(size + frameWidth * 2, barHeight * 1.2f, frameWidth), frameColor);
        AddBox(surfaceTool, new Vector3(0, barHeight / 2f, size / 2f + frameWidth / 2f),
            new Vector3(size + frameWidth * 2, barHeight * 1.2f, frameWidth), frameColor);
        AddBox(surfaceTool, new Vector3(-size / 2f - frameWidth / 2f, barHeight / 2f, 0),
            new Vector3(frameWidth, barHeight * 1.2f, size), frameColor);
        AddBox(surfaceTool, new Vector3(size / 2f + frameWidth / 2f, barHeight / 2f, 0),
            new Vector3(frameWidth, barHeight * 1.2f, size), frameColor);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.7f,
            Metallic = 0.6f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    /// <summary>
    /// Creates a circular drain cover mesh.
    /// </summary>
    private ArrayMesh CreateDrainCoverMesh(RandomNumberGenerator rng)
    {
        float radius = 0.4f * PropScale;
        float thickness = 0.08f * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Color metalColor = new Color(0.22f, 0.22f, 0.25f);
        int segments = 16;

        // Outer ring
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;

            Vector3 outer1 = new Vector3(Mathf.Cos(angle1) * radius, thickness, Mathf.Sin(angle1) * radius);
            Vector3 outer2 = new Vector3(Mathf.Cos(angle2) * radius, thickness, Mathf.Sin(angle2) * radius);
            Vector3 inner1 = new Vector3(Mathf.Cos(angle1) * radius * 0.9f, thickness, Mathf.Sin(angle1) * radius * 0.9f);
            Vector3 inner2 = new Vector3(Mathf.Cos(angle2) * radius * 0.9f, thickness, Mathf.Sin(angle2) * radius * 0.9f);

            surfaceTool.SetColor(metalColor);
            surfaceTool.AddVertex(outer1);
            surfaceTool.AddVertex(outer2);
            surfaceTool.AddVertex(inner1);
            surfaceTool.AddVertex(inner1);
            surfaceTool.AddVertex(outer2);
            surfaceTool.AddVertex(inner2);
        }

        // Radial bars
        int numBars = 6;
        for (int i = 0; i < numBars; i++)
        {
            float angle = i * Mathf.Tau / numBars;
            float barWidth = 0.04f * PropScale;

            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 perp = new Vector3(-dir.Z, 0, dir.X);

            Vector3[] corners = {
                dir * radius * 0.1f + perp * barWidth / 2f + Vector3.Up * thickness,
                dir * radius * 0.85f + perp * barWidth / 2f + Vector3.Up * thickness,
                dir * radius * 0.85f - perp * barWidth / 2f + Vector3.Up * thickness,
                dir * radius * 0.1f - perp * barWidth / 2f + Vector3.Up * thickness
            };

            surfaceTool.SetColor(metalColor);
            surfaceTool.AddVertex(corners[0]);
            surfaceTool.AddVertex(corners[1]);
            surfaceTool.AddVertex(corners[2]);
            surfaceTool.AddVertex(corners[0]);
            surfaceTool.AddVertex(corners[2]);
            surfaceTool.AddVertex(corners[3]);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.65f,
            Metallic = 0.7f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    /// <summary>
    /// Creates a ceiling support beam mesh.
    /// </summary>
    private ArrayMesh CreateCeilingBeamMesh(RandomNumberGenerator rng)
    {
        float beamWidth = 0.4f * PropScale;
        float beamHeight = 0.5f * PropScale;
        float beamLength = 6f * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        // Wood colors
        Color woodDark = new Color(0.28f, 0.18f, 0.1f);
        Color woodLight = new Color(0.35f, 0.24f, 0.14f);
        Color woodMid = new Color(0.32f, 0.21f, 0.12f);

        // Main beam
        Color beamColor = rng.Randf() > 0.5f ? woodDark : woodMid;
        AddBox(surfaceTool, Vector3.Zero, new Vector3(beamWidth, beamHeight, beamLength), beamColor);

        // Add wood grain stripes
        int numStripes = rng.RandiRange(3, 6);
        for (int i = 0; i < numStripes; i++)
        {
            float xOffset = rng.RandfRange(-beamWidth * 0.4f, beamWidth * 0.4f);
            float stripeWidth = rng.RandfRange(0.02f, 0.05f) * PropScale;
            Color stripeColor = rng.Randf() > 0.5f ? woodLight : woodDark;
            AddBox(surfaceTool, new Vector3(xOffset, beamHeight / 2f + 0.01f, 0),
                new Vector3(stripeWidth, 0.01f, beamLength), stripeColor);
        }

        // Add decorative brackets at ends
        Color bracketColor = new Color(0.25f, 0.25f, 0.28f);
        float bracketSize = 0.15f * PropScale;
        AddBox(surfaceTool, new Vector3(0, -beamHeight * 0.3f, beamLength / 2f - bracketSize),
            new Vector3(beamWidth * 1.2f, bracketSize, bracketSize), bracketColor);
        AddBox(surfaceTool, new Vector3(0, -beamHeight * 0.3f, -beamLength / 2f + bracketSize),
            new Vector3(beamWidth * 1.2f, bracketSize, bracketSize), bracketColor);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.85f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    /// <summary>
    /// Creates a ceiling arch/rib mesh.
    /// </summary>
    private ArrayMesh CreateCeilingArchMesh(RandomNumberGenerator rng)
    {
        float archWidth = 0.3f * PropScale;
        float archHeight = 0.4f * PropScale;
        float archSpan = 4f * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Color stoneColor = new Color(0.32f, 0.28f, 0.24f);

        // Create arch segments
        int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            float t1 = i / (float)segments;
            float t2 = (i + 1) / (float)segments;
            float angle1 = t1 * Mathf.Pi;
            float angle2 = t2 * Mathf.Pi;

            float y1 = Mathf.Sin(angle1) * archHeight;
            float y2 = Mathf.Sin(angle2) * archHeight;
            float z1 = (t1 - 0.5f) * archSpan;
            float z2 = (t2 - 0.5f) * archSpan;

            // Slight color variation per segment
            Color segColor = stoneColor * (0.9f + rng.Randf() * 0.2f);

            // Draw box segment
            Vector3 center = new Vector3(0, (y1 + y2) / 2f, (z1 + z2) / 2f);
            float segLength = new Vector2(z2 - z1, y2 - y1).Length();
            AddBox(surfaceTool, center, new Vector3(archWidth, archHeight * 0.5f, segLength * 1.1f), segColor);
        }

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.9f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    /// <summary>
    /// Creates a wall buttress/support structure mesh.
    /// </summary>
    private ArrayMesh CreateWallButtressMesh(RandomNumberGenerator rng)
    {
        float width = 0.8f * PropScale;
        float height = 3f * PropScale;
        float depth = 0.5f * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Color stoneBase = new Color(0.35f, 0.30f, 0.25f);
        Color stoneDark = new Color(0.28f, 0.24f, 0.20f);

        // Main buttress body (tapered)
        // Bottom section (wider)
        AddBox(surfaceTool, new Vector3(0, height * 0.15f, depth * 0.1f),
            new Vector3(width, height * 0.3f, depth * 0.8f), stoneBase);

        // Middle section
        AddBox(surfaceTool, new Vector3(0, height * 0.5f, 0),
            new Vector3(width * 0.85f, height * 0.4f, depth * 0.6f), stoneBase * 0.95f);

        // Top section (narrower)
        AddBox(surfaceTool, new Vector3(0, height * 0.85f, -depth * 0.1f),
            new Vector3(width * 0.7f, height * 0.3f, depth * 0.4f), stoneBase * 0.9f);

        // Decorative cap
        AddBox(surfaceTool, new Vector3(0, height, -depth * 0.15f),
            new Vector3(width * 0.9f, height * 0.08f, depth * 0.5f), stoneDark);

        // Base molding
        AddBox(surfaceTool, new Vector3(0, height * 0.03f, depth * 0.2f),
            new Vector3(width * 1.1f, height * 0.06f, depth), stoneDark);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.88f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    /// <summary>
    /// Creates a decorative wall column mesh.
    /// </summary>
    private ArrayMesh CreateWallColumnMesh(RandomNumberGenerator rng)
    {
        float radius = 0.35f * PropScale;
        float height = Constants.WallHeight * PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Color stoneLight = new Color(0.38f, 0.33f, 0.28f);
        Color stoneDark = new Color(0.28f, 0.24f, 0.20f);

        int segments = 12;

        // Column shaft with fluting
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;

            // Fluted radius (alternating)
            float r1 = radius * (i % 2 == 0 ? 1f : 0.9f);
            float r2 = radius * ((i + 1) % 2 == 0 ? 1f : 0.9f);

            Vector3 bottom1 = new Vector3(Mathf.Cos(angle1) * r1, 0.3f * PropScale, Mathf.Sin(angle1) * r1);
            Vector3 bottom2 = new Vector3(Mathf.Cos(angle2) * r2, 0.3f * PropScale, Mathf.Sin(angle2) * r2);
            Vector3 top1 = new Vector3(Mathf.Cos(angle1) * r1 * 0.9f, height - 0.3f * PropScale, Mathf.Sin(angle1) * r1 * 0.9f);
            Vector3 top2 = new Vector3(Mathf.Cos(angle2) * r2 * 0.9f, height - 0.3f * PropScale, Mathf.Sin(angle2) * r2 * 0.9f);

            Color segColor = i % 2 == 0 ? stoneLight : stoneLight * 0.9f;
            surfaceTool.SetColor(segColor);
            surfaceTool.AddVertex(bottom1);
            surfaceTool.AddVertex(top1);
            surfaceTool.AddVertex(bottom2);
            surfaceTool.AddVertex(bottom2);
            surfaceTool.AddVertex(top1);
            surfaceTool.AddVertex(top2);
        }

        // Base
        AddCylinder(surfaceTool, new Vector3(0, 0.15f * PropScale, 0), radius * 1.3f, 0.3f * PropScale, segments, stoneDark);

        // Capital (top)
        AddCylinder(surfaceTool, new Vector3(0, height - 0.15f * PropScale, 0), radius * 1.2f, 0.3f * PropScale, segments, stoneDark);

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.85f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    /// <summary>
    /// Creates a wall-mounted torch sconce mesh.
    /// </summary>
    private ArrayMesh CreateWallSconceMesh(RandomNumberGenerator rng)
    {
        float scale = PropScale;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Color ironColor = new Color(0.2f, 0.2f, 0.22f);
        Color brassColor = new Color(0.5f, 0.4f, 0.25f);

        // Wall plate
        AddBox(surfaceTool, new Vector3(0, 0, -0.05f * scale),
            new Vector3(0.2f * scale, 0.3f * scale, 0.03f * scale), ironColor);

        // Arm
        AddBox(surfaceTool, new Vector3(0, -0.05f * scale, 0.1f * scale),
            new Vector3(0.06f * scale, 0.06f * scale, 0.25f * scale), ironColor);

        // Torch holder ring
        int segments = 8;
        float ringRadius = 0.08f * scale;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;

            Vector3 inner1 = new Vector3(Mathf.Cos(angle1) * ringRadius * 0.7f, -0.05f * scale, 0.22f * scale + Mathf.Sin(angle1) * ringRadius * 0.7f);
            Vector3 inner2 = new Vector3(Mathf.Cos(angle2) * ringRadius * 0.7f, -0.05f * scale, 0.22f * scale + Mathf.Sin(angle2) * ringRadius * 0.7f);
            Vector3 outer1 = new Vector3(Mathf.Cos(angle1) * ringRadius, -0.05f * scale, 0.22f * scale + Mathf.Sin(angle1) * ringRadius);
            Vector3 outer2 = new Vector3(Mathf.Cos(angle2) * ringRadius, -0.05f * scale, 0.22f * scale + Mathf.Sin(angle2) * ringRadius);

            surfaceTool.SetColor(brassColor);
            surfaceTool.AddVertex(inner1);
            surfaceTool.AddVertex(outer1);
            surfaceTool.AddVertex(inner2);
            surfaceTool.AddVertex(inner2);
            surfaceTool.AddVertex(outer1);
            surfaceTool.AddVertex(outer2);
        }

        // Torch (simple cylinder)
        AddCylinder(surfaceTool, new Vector3(0, 0.1f * scale, 0.22f * scale), 0.04f * scale, 0.25f * scale, 6, new Color(0.4f, 0.28f, 0.15f));

        surfaceTool.GenerateNormals();
        var mesh = surfaceTool.Commit();

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.7f,
            Metallic = 0.4f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    // Helper methods for mesh generation

    private void AddBox(SurfaceTool st, Vector3 center, Vector3 size, Color color)
    {
        Vector3 half = size / 2f;
        Vector3[] corners = {
            center + new Vector3(-half.X, -half.Y, -half.Z),
            center + new Vector3(half.X, -half.Y, -half.Z),
            center + new Vector3(half.X, half.Y, -half.Z),
            center + new Vector3(-half.X, half.Y, -half.Z),
            center + new Vector3(-half.X, -half.Y, half.Z),
            center + new Vector3(half.X, -half.Y, half.Z),
            center + new Vector3(half.X, half.Y, half.Z),
            center + new Vector3(-half.X, half.Y, half.Z)
        };

        int[,] faces = {
            {0, 1, 2, 3}, // Front
            {5, 4, 7, 6}, // Back
            {4, 0, 3, 7}, // Left
            {1, 5, 6, 2}, // Right
            {3, 2, 6, 7}, // Top
            {4, 5, 1, 0}  // Bottom
        };

        st.SetColor(color);
        for (int f = 0; f < 6; f++)
        {
            st.AddVertex(corners[faces[f, 0]]);
            st.AddVertex(corners[faces[f, 1]]);
            st.AddVertex(corners[faces[f, 2]]);
            st.AddVertex(corners[faces[f, 0]]);
            st.AddVertex(corners[faces[f, 2]]);
            st.AddVertex(corners[faces[f, 3]]);
        }
    }

    private void AddCylinder(SurfaceTool st, Vector3 center, float radius, float height, int segments, Color color)
    {
        st.SetColor(color);

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = (i + 1) * Mathf.Tau / segments;

            Vector3 bottom1 = center + new Vector3(Mathf.Cos(angle1) * radius, -height / 2f, Mathf.Sin(angle1) * radius);
            Vector3 bottom2 = center + new Vector3(Mathf.Cos(angle2) * radius, -height / 2f, Mathf.Sin(angle2) * radius);
            Vector3 top1 = center + new Vector3(Mathf.Cos(angle1) * radius, height / 2f, Mathf.Sin(angle1) * radius);
            Vector3 top2 = center + new Vector3(Mathf.Cos(angle2) * radius, height / 2f, Mathf.Sin(angle2) * radius);

            // Side
            st.AddVertex(bottom1);
            st.AddVertex(top1);
            st.AddVertex(bottom2);
            st.AddVertex(bottom2);
            st.AddVertex(top1);
            st.AddVertex(top2);

            // Top cap
            st.AddVertex(center + Vector3.Up * height / 2f);
            st.AddVertex(top1);
            st.AddVertex(top2);

            // Bottom cap
            st.AddVertex(center + Vector3.Down * height / 2f);
            st.AddVertex(bottom2);
            st.AddVertex(bottom1);
        }
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
