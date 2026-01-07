using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Items;

/// <summary>
/// Factory for creating procedural shield meshes with detailed geometry.
/// Shields are built facing forward (+Z) as they would appear when held.
///
/// === SHIELD COORDINATE SYSTEM ===
/// - Origin (0,0,0): Center of the shield face
/// - +Z: Forward (direction shield faces, toward enemies)
/// - -Z: Backward (toward holder's arm)
/// - +Y: Up
/// - +X: Right side
/// </summary>
public static class ShieldFactory
{
    /// <summary>
    /// Available shield types for the factory.
    /// </summary>
    public enum ShieldType
    {
        Buckler,      // Small, fast, low block
        RoundShield,  // Viking-style, balanced
        KiteShield,   // Teardrop, high block
        TowerShield,  // Massive rectangle, highest block
        SpikedShield  // Offensive shield with spikes
    }

    /// <summary>
    /// Shield stats and configuration.
    /// </summary>
    public struct ShieldData
    {
        public string Name;
        public ShieldType Type;
        public int Armor;
        public float BlockChance;    // 0.10 to 0.40 (10-40%)
        public int BlockAmount;      // Flat damage blocked
        public float MovementPenalty; // 0 to 0.15 (0-15%)
        public bool IsOffensive;     // Can deal damage (spiked)
        public float Scale;          // Default display scale

        public static ShieldData Default => new()
        {
            Name = "Unknown Shield",
            Type = ShieldType.RoundShield,
            Armor = 5,
            BlockChance = 0.15f,
            BlockAmount = 5,
            MovementPenalty = 0f,
            IsOffensive = false,
            Scale = 1f
        };
    }

    // Cached shield data for all types
    private static readonly Dictionary<ShieldType, ShieldData> _shieldStats = new()
    {
        [ShieldType.Buckler] = new ShieldData
        {
            Name = "Buckler",
            Type = ShieldType.Buckler,
            Armor = 3,
            BlockChance = 0.12f,
            BlockAmount = 3,
            MovementPenalty = 0f,
            IsOffensive = false,
            Scale = 0.6f
        },
        [ShieldType.RoundShield] = new ShieldData
        {
            Name = "Round Shield",
            Type = ShieldType.RoundShield,
            Armor = 6,
            BlockChance = 0.20f,
            BlockAmount = 8,
            MovementPenalty = 0.02f,
            IsOffensive = false,
            Scale = 1f
        },
        [ShieldType.KiteShield] = new ShieldData
        {
            Name = "Kite Shield",
            Type = ShieldType.KiteShield,
            Armor = 10,
            BlockChance = 0.28f,
            BlockAmount = 12,
            MovementPenalty = 0.05f,
            IsOffensive = false,
            Scale = 1.1f
        },
        [ShieldType.TowerShield] = new ShieldData
        {
            Name = "Tower Shield",
            Type = ShieldType.TowerShield,
            Armor = 15,
            BlockChance = 0.40f,
            BlockAmount = 20,
            MovementPenalty = 0.12f,
            IsOffensive = false,
            Scale = 1.3f
        },
        [ShieldType.SpikedShield] = new ShieldData
        {
            Name = "Spiked Shield",
            Type = ShieldType.SpikedShield,
            Armor = 7,
            BlockChance = 0.18f,
            BlockAmount = 6,
            MovementPenalty = 0.03f,
            IsOffensive = true,
            Scale = 0.9f
        }
    };

    /// <summary>
    /// Get all available shield types.
    /// </summary>
    public static IEnumerable<ShieldType> GetAllTypes() => _shieldStats.Keys;

    /// <summary>
    /// Get shield data for a specific type.
    /// </summary>
    public static ShieldData GetShieldData(ShieldType type)
    {
        return _shieldStats.TryGetValue(type, out var data) ? data : ShieldData.Default;
    }

    /// <summary>
    /// Create a shield mesh of the specified type.
    /// </summary>
    public static Node3D CreateShield(ShieldType type, float scale = 1f,
        StandardMaterial3D? primaryMaterial = null, StandardMaterial3D? accentMaterial = null)
    {
        return type switch
        {
            ShieldType.Buckler => CreateBuckler(scale, primaryMaterial, accentMaterial),
            ShieldType.RoundShield => CreateRoundShield(scale, primaryMaterial, accentMaterial),
            ShieldType.KiteShield => CreateKiteShield(scale, primaryMaterial, accentMaterial),
            ShieldType.TowerShield => CreateTowerShield(scale, primaryMaterial, accentMaterial),
            ShieldType.SpikedShield => CreateSpikedShield(scale, primaryMaterial, accentMaterial),
            _ => CreateRoundShield(scale, primaryMaterial, accentMaterial)
        };
    }

    // ========================================================================
    // MATERIAL HELPERS
    // ========================================================================

    private static StandardMaterial3D CreateMetalMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = 0.8f,
            Roughness = 0.35f
        };
    }

    private static StandardMaterial3D CreateWoodMaterial(Color? color = null)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color ?? new Color(0.45f, 0.30f, 0.18f),
            Metallic = 0f,
            Roughness = 0.8f
        };
    }

    private static StandardMaterial3D CreateLeatherMaterial(Color? color = null)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color ?? new Color(0.40f, 0.25f, 0.12f),
            Metallic = 0f,
            Roughness = 0.65f
        };
    }

    private static StandardMaterial3D CreatePaintMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = 0.1f,
            Roughness = 0.5f
        };
    }

    // ========================================================================
    // SHIELD CREATION METHODS
    // ========================================================================

    /// <summary>
    /// Create a Buckler - small circular shield with central grip, leather-wrapped rim,
    /// and decorative boss.
    /// </summary>
    public static Node3D CreateBuckler(float scale = 1f,
        StandardMaterial3D? primaryMaterial = null, StandardMaterial3D? accentMaterial = null)
    {
        var shield = new Node3D();
        shield.Name = "Buckler";

        var metalMat = primaryMaterial ?? CreateMetalMaterial(new Color(0.55f, 0.50f, 0.45f));
        var leatherMat = accentMaterial ?? CreateLeatherMaterial(new Color(0.35f, 0.22f, 0.12f));
        var brassMat = CreateMetalMaterial(new Color(0.72f, 0.58f, 0.32f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.30f, 0.28f, 0.26f));

        float radius = 0.15f * scale;
        float thickness = 0.015f * scale;

        // === MAIN SHIELD BODY (slightly domed cylinder) ===
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius * 0.98f,
            Height = thickness,
            RadialSegments = 24
        };
        body.MaterialOverride = metalMat;
        body.RotationDegrees = new Vector3(90, 0, 0);
        body.Position = new Vector3(0, 0, thickness / 2);
        shield.AddChild(body);

        // === DOMED CENTER (convex surface) ===
        var dome = new MeshInstance3D();
        dome.Mesh = new SphereMesh
        {
            Radius = radius * 0.7f,
            Height = radius * 0.3f,
            RadialSegments = 16,
            Rings = 8
        };
        dome.MaterialOverride = metalMat;
        dome.Position = new Vector3(0, 0, thickness * 0.8f);
        dome.Scale = new Vector3(1, 1, 0.3f);
        shield.AddChild(dome);

        // === CENTRAL BOSS (prominent bulge) ===
        var boss = new MeshInstance3D();
        boss.Mesh = new SphereMesh
        {
            Radius = radius * 0.25f,
            Height = radius * 0.5f,
            RadialSegments = 12
        };
        boss.MaterialOverride = brassMat;
        boss.Position = new Vector3(0, 0, thickness + radius * 0.12f);
        boss.Scale = new Vector3(1, 1, 0.6f);
        shield.AddChild(boss);

        // === BOSS RIVET RING (decorative studs around boss) ===
        int rivetCount = 8;
        for (int i = 0; i < rivetCount; i++)
        {
            float angle = (float)i / rivetCount * Mathf.Pi * 2;
            float rivetDist = radius * 0.35f;
            var rivet = new MeshInstance3D();
            rivet.Mesh = new SphereMesh
            {
                Radius = 0.008f * scale,
                Height = 0.016f * scale
            };
            rivet.MaterialOverride = brassMat;
            rivet.Position = new Vector3(
                Mathf.Cos(angle) * rivetDist,
                Mathf.Sin(angle) * rivetDist,
                thickness + 0.005f * scale
            );
            shield.AddChild(rivet);
        }

        // === LEATHER RIM (removed torus to avoid ring artifact) ===
        // Edge defined by rivets and shield body cutoff

        // === GRIP HANDLE (back side) ===
        var grip = new MeshInstance3D();
        grip.Mesh = new CylinderMesh
        {
            TopRadius = 0.012f * scale,
            BottomRadius = 0.012f * scale,
            Height = radius * 0.8f,
            RadialSegments = 8
        };
        grip.MaterialOverride = leatherMat;
        grip.RotationDegrees = new Vector3(0, 0, 90);
        grip.Position = new Vector3(0, 0, -thickness);
        shield.AddChild(grip);

        // === GRIP BRACKETS ===
        for (int i = -1; i <= 1; i += 2)
        {
            var bracket = new MeshInstance3D();
            bracket.Mesh = new BoxMesh
            {
                Size = new Vector3(0.02f * scale, 0.025f * scale, 0.008f * scale)
            };
            bracket.MaterialOverride = darkMetalMat;
            bracket.Position = new Vector3(i * radius * 0.35f, 0, -thickness * 0.5f);
            shield.AddChild(bracket);
        }

        return shield;
    }

    /// <summary>
    /// Create a Round Shield - Viking-style with wooden planks, iron rim, central boss,
    /// and leather strap.
    /// </summary>
    public static Node3D CreateRoundShield(float scale = 1f,
        StandardMaterial3D? primaryMaterial = null, StandardMaterial3D? accentMaterial = null)
    {
        var shield = new Node3D();
        shield.Name = "RoundShield";

        var woodMat = primaryMaterial ?? CreateWoodMaterial(new Color(0.50f, 0.35f, 0.20f));
        var ironMat = accentMaterial ?? CreateMetalMaterial(new Color(0.40f, 0.38f, 0.36f));
        var darkWoodMat = CreateWoodMaterial(new Color(0.35f, 0.22f, 0.12f));
        var brassMat = CreateMetalMaterial(new Color(0.65f, 0.52f, 0.28f));

        float radius = 0.28f * scale;
        float thickness = 0.025f * scale;

        // === MAIN WOODEN BODY ===
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius,
            Height = thickness,
            RadialSegments = 32
        };
        body.MaterialOverride = woodMat;
        body.RotationDegrees = new Vector3(90, 0, 0);
        body.Position = new Vector3(0, 0, thickness / 2);
        shield.AddChild(body);

        // === WOODEN PLANK LINES (vertical grain) ===
        int plankCount = 7;
        float plankSpacing = radius * 2 / (plankCount + 1);
        for (int i = 1; i <= plankCount; i++)
        {
            float xPos = -radius + i * plankSpacing;
            // Only draw planks that fit within the circle
            float halfHeight = Mathf.Sqrt(Mathf.Max(0, radius * radius - xPos * xPos));
            if (halfHeight < 0.02f) continue;

            var plankLine = new MeshInstance3D();
            plankLine.Mesh = new BoxMesh
            {
                Size = new Vector3(0.004f * scale, halfHeight * 1.9f, 0.003f * scale)
            };
            plankLine.MaterialOverride = darkWoodMat;
            plankLine.Position = new Vector3(xPos, 0, thickness + 0.002f * scale);
            shield.AddChild(plankLine);
        }

        // === IRON RIM (edge band segments for clean look) ===
        // Removed torus to avoid floating ring artifact

        // === RIM RIVETS ===
        int rimRivetCount = 16;
        for (int i = 0; i < rimRivetCount; i++)
        {
            float angle = (float)i / rimRivetCount * Mathf.Pi * 2;
            var rivet = new MeshInstance3D();
            rivet.Mesh = new SphereMesh
            {
                Radius = 0.008f * scale,
                Height = 0.016f * scale
            };
            rivet.MaterialOverride = ironMat;
            rivet.Position = new Vector3(
                Mathf.Cos(angle) * (radius - 0.01f * scale),
                Mathf.Sin(angle) * (radius - 0.01f * scale),
                thickness + 0.004f * scale
            );
            shield.AddChild(rivet);
        }

        // === CENTRAL IRON BOSS ===
        var boss = new MeshInstance3D();
        boss.Mesh = new SphereMesh
        {
            Radius = radius * 0.22f,
            Height = radius * 0.44f,
            RadialSegments = 16
        };
        boss.MaterialOverride = ironMat;
        boss.Position = new Vector3(0, 0, thickness);
        boss.Scale = new Vector3(1, 1, 0.5f);
        shield.AddChild(boss);

        // === BOSS APEX (pointed center) ===
        var apex = new MeshInstance3D();
        apex.Mesh = new CylinderMesh
        {
            TopRadius = 0.005f * scale,
            BottomRadius = radius * 0.12f,
            Height = 0.025f * scale,
            RadialSegments = 8
        };
        apex.MaterialOverride = brassMat;
        apex.RotationDegrees = new Vector3(90, 0, 0);
        apex.Position = new Vector3(0, 0, thickness + radius * 0.12f);
        shield.AddChild(apex);

        // === BACK GRIP STRAP ===
        var strap = new MeshInstance3D();
        strap.Mesh = new BoxMesh
        {
            Size = new Vector3(radius * 0.7f, 0.04f * scale, 0.015f * scale)
        };
        strap.MaterialOverride = CreateLeatherMaterial();
        strap.Position = new Vector3(0, 0, -thickness * 0.3f);
        shield.AddChild(strap);

        // === STRAP BUCKLES ===
        for (int i = -1; i <= 1; i += 2)
        {
            var buckle = new MeshInstance3D();
            buckle.Mesh = new BoxMesh
            {
                Size = new Vector3(0.025f * scale, 0.05f * scale, 0.012f * scale)
            };
            buckle.MaterialOverride = brassMat;
            buckle.Position = new Vector3(i * radius * 0.28f, 0, -thickness * 0.2f);
            shield.AddChild(buckle);
        }

        return shield;
    }

    /// <summary>
    /// Create a Kite Shield - elongated teardrop shape, heraldic design, reinforced edges.
    /// </summary>
    public static Node3D CreateKiteShield(float scale = 1f,
        StandardMaterial3D? primaryMaterial = null, StandardMaterial3D? accentMaterial = null)
    {
        var shield = new Node3D();
        shield.Name = "KiteShield";

        var woodMat = primaryMaterial ?? CreateWoodMaterial(new Color(0.40f, 0.28f, 0.15f));
        var metalMat = accentMaterial ?? CreateMetalMaterial(new Color(0.45f, 0.42f, 0.40f));
        var paintMat = CreatePaintMaterial(new Color(0.15f, 0.25f, 0.55f)); // Blue heraldic
        var goldMat = CreateMetalMaterial(new Color(0.75f, 0.62f, 0.22f));

        float width = 0.30f * scale;
        float height = 0.50f * scale;
        float thickness = 0.03f * scale;

        // === MAIN BODY (elongated shape using multiple parts) ===
        // Top rounded section
        var topSection = new MeshInstance3D();
        topSection.Mesh = new CylinderMesh
        {
            TopRadius = width / 2,
            BottomRadius = width / 2,
            Height = thickness,
            RadialSegments = 24
        };
        topSection.MaterialOverride = woodMat;
        topSection.RotationDegrees = new Vector3(90, 0, 0);
        topSection.Position = new Vector3(0, height * 0.2f, thickness / 2);
        topSection.Scale = new Vector3(1, 0.5f, 1); // Flatten to half-circle
        shield.AddChild(topSection);

        // Middle rectangular section
        var midSection = new MeshInstance3D();
        midSection.Mesh = new BoxMesh
        {
            Size = new Vector3(width, height * 0.4f, thickness)
        };
        midSection.MaterialOverride = woodMat;
        midSection.Position = new Vector3(0, 0, thickness / 2);
        shield.AddChild(midSection);

        // Bottom tapered section (triangular point)
        var botSection = new MeshInstance3D();
        botSection.Mesh = new PrismMesh
        {
            Size = new Vector3(width, height * 0.4f, thickness),
            LeftToRight = 0.5f
        };
        botSection.MaterialOverride = woodMat;
        botSection.RotationDegrees = new Vector3(90, 0, 0);
        botSection.Position = new Vector3(0, -height * 0.4f, thickness / 2);
        shield.AddChild(botSection);

        // === HERALDIC PAINT (central stripe) ===
        var stripe = new MeshInstance3D();
        stripe.Mesh = new BoxMesh
        {
            Size = new Vector3(width * 0.35f, height * 0.7f, 0.002f * scale)
        };
        stripe.MaterialOverride = paintMat;
        stripe.Position = new Vector3(0, -height * 0.05f, thickness + 0.002f * scale);
        shield.AddChild(stripe);

        // === CENTRAL CROSS (heraldic symbol) ===
        var crossV = new MeshInstance3D();
        crossV.Mesh = new BoxMesh
        {
            Size = new Vector3(width * 0.08f, height * 0.5f, 0.003f * scale)
        };
        crossV.MaterialOverride = goldMat;
        crossV.Position = new Vector3(0, 0, thickness + 0.004f * scale);
        shield.AddChild(crossV);

        var crossH = new MeshInstance3D();
        crossH.Mesh = new BoxMesh
        {
            Size = new Vector3(width * 0.25f, height * 0.06f, 0.003f * scale)
        };
        crossH.MaterialOverride = goldMat;
        crossH.Position = new Vector3(0, height * 0.12f, thickness + 0.004f * scale);
        shield.AddChild(crossH);

        // === METAL EDGE REINFORCEMENT ===
        // Top curved edge (using box instead of torus to avoid ring artifact)
        var topEdge = new MeshInstance3D();
        topEdge.Mesh = new BoxMesh
        {
            Size = new Vector3(width + 0.02f * scale, 0.015f * scale, thickness + 0.01f * scale)
        };
        topEdge.MaterialOverride = metalMat;
        topEdge.Position = new Vector3(0, height * 0.38f, thickness / 2);
        shield.AddChild(topEdge);

        // Side metal strips
        for (int i = -1; i <= 1; i += 2)
        {
            var sideStrip = new MeshInstance3D();
            sideStrip.Mesh = new BoxMesh
            {
                Size = new Vector3(0.012f * scale, height * 0.7f, thickness * 0.5f)
            };
            sideStrip.MaterialOverride = metalMat;
            sideStrip.Position = new Vector3(i * width / 2, -height * 0.05f, thickness / 2);
            shield.AddChild(sideStrip);
        }

        // === CORNER STUDS ===
        float[] studAngles = { 0.3f, 0.5f, 0.7f };
        foreach (float t in studAngles)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var stud = new MeshInstance3D();
                stud.Mesh = new SphereMesh
                {
                    Radius = 0.01f * scale,
                    Height = 0.02f * scale
                };
                stud.MaterialOverride = metalMat;
                stud.Position = new Vector3(
                    side * (width / 2 - 0.005f * scale),
                    height * (0.3f - t * 0.8f),
                    thickness + 0.005f * scale
                );
                shield.AddChild(stud);
            }
        }

        // === ARM STRAPS (back) ===
        for (int i = 0; i < 2; i++)
        {
            var strap = new MeshInstance3D();
            strap.Mesh = new BoxMesh
            {
                Size = new Vector3(width * 0.5f, 0.035f * scale, 0.012f * scale)
            };
            strap.MaterialOverride = CreateLeatherMaterial();
            strap.Position = new Vector3(0, height * (0.1f - i * 0.25f), -thickness * 0.2f);
            shield.AddChild(strap);
        }

        return shield;
    }

    /// <summary>
    /// Create a Tower Shield - massive rectangular shield with thick wood, metal banding,
    /// and a view slit.
    /// </summary>
    public static Node3D CreateTowerShield(float scale = 1f,
        StandardMaterial3D? primaryMaterial = null, StandardMaterial3D? accentMaterial = null)
    {
        var shield = new Node3D();
        shield.Name = "TowerShield";

        var woodMat = primaryMaterial ?? CreateWoodMaterial(new Color(0.38f, 0.25f, 0.12f));
        var metalMat = accentMaterial ?? CreateMetalMaterial(new Color(0.35f, 0.33f, 0.30f));
        var darkWoodMat = CreateWoodMaterial(new Color(0.25f, 0.15f, 0.08f));
        var brassAccent = CreateMetalMaterial(new Color(0.60f, 0.48f, 0.25f));

        float width = 0.40f * scale;
        float height = 0.70f * scale;
        float thickness = 0.045f * scale;

        // === MAIN WOODEN BODY ===
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh
        {
            Size = new Vector3(width, height, thickness)
        };
        body.MaterialOverride = woodMat;
        body.Position = new Vector3(0, 0, thickness / 2);
        shield.AddChild(body);

        // Shield is intentionally flat for cleaner look
        // (curved front caused visual artifacts)

        // === HORIZONTAL METAL BANDS ===
        float[] bandPositions = { 0.38f, 0.15f, -0.15f, -0.38f };
        foreach (float yNorm in bandPositions)
        {
            var band = new MeshInstance3D();
            band.Mesh = new BoxMesh
            {
                Size = new Vector3(width + 0.02f * scale, 0.025f * scale, 0.008f * scale)
            };
            band.MaterialOverride = metalMat;
            band.Position = new Vector3(0, height * yNorm, thickness + 0.002f * scale);
            shield.AddChild(band);

            // Band rivets
            for (int i = -2; i <= 2; i++)
            {
                var rivet = new MeshInstance3D();
                rivet.Mesh = new SphereMesh
                {
                    Radius = 0.008f * scale,
                    Height = 0.016f * scale
                };
                rivet.MaterialOverride = metalMat;
                rivet.Position = new Vector3(
                    i * width * 0.2f,
                    height * yNorm,
                    thickness + 0.008f * scale
                );
                shield.AddChild(rivet);
            }
        }

        // === VERTICAL CENTER BAND ===
        var centerBand = new MeshInstance3D();
        centerBand.Mesh = new BoxMesh
        {
            Size = new Vector3(0.035f * scale, height + 0.015f * scale, 0.01f * scale)
        };
        centerBand.MaterialOverride = metalMat;
        centerBand.Position = new Vector3(0, 0, thickness + 0.003f * scale);
        shield.AddChild(centerBand);

        // === VIEW SLIT (dark opening near top) ===
        var slitFrame = new MeshInstance3D();
        slitFrame.Mesh = new BoxMesh
        {
            Size = new Vector3(width * 0.5f, 0.04f * scale, 0.02f * scale)
        };
        slitFrame.MaterialOverride = metalMat;
        slitFrame.Position = new Vector3(0, height * 0.32f, thickness);
        shield.AddChild(slitFrame);

        var slitDark = new MeshInstance3D();
        slitDark.Mesh = new BoxMesh
        {
            Size = new Vector3(width * 0.45f, 0.02f * scale, 0.015f * scale)
        };
        var darkMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.05f, 0.05f),
            Metallic = 0f,
            Roughness = 1f
        };
        slitDark.MaterialOverride = darkMat;
        slitDark.Position = new Vector3(0, height * 0.32f, thickness + 0.008f * scale);
        shield.AddChild(slitDark);

        // === EDGE METAL TRIM ===
        // Top edge
        var topEdge = new MeshInstance3D();
        topEdge.Mesh = new BoxMesh
        {
            Size = new Vector3(width + 0.015f * scale, 0.02f * scale, thickness + 0.01f * scale)
        };
        topEdge.MaterialOverride = metalMat;
        topEdge.Position = new Vector3(0, height / 2, thickness / 2);
        shield.AddChild(topEdge);

        // Bottom edge
        var botEdge = new MeshInstance3D();
        botEdge.Mesh = new BoxMesh
        {
            Size = new Vector3(width + 0.015f * scale, 0.025f * scale, thickness + 0.01f * scale)
        };
        botEdge.MaterialOverride = metalMat;
        botEdge.Position = new Vector3(0, -height / 2, thickness / 2);
        shield.AddChild(botEdge);

        // Side edges
        for (int i = -1; i <= 1; i += 2)
        {
            var sideEdge = new MeshInstance3D();
            sideEdge.Mesh = new BoxMesh
            {
                Size = new Vector3(0.02f * scale, height, thickness + 0.01f * scale)
            };
            sideEdge.MaterialOverride = metalMat;
            sideEdge.Position = new Vector3(i * width / 2, 0, thickness / 2);
            shield.AddChild(sideEdge);
        }

        // === CORNER REINFORCEMENTS ===
        float[][] corners = {
            new[] { -1f, 1f }, new[] { 1f, 1f },
            new[] { -1f, -1f }, new[] { 1f, -1f }
        };
        foreach (var corner in corners)
        {
            var reinforce = new MeshInstance3D();
            reinforce.Mesh = new BoxMesh
            {
                Size = new Vector3(0.05f * scale, 0.05f * scale, 0.012f * scale)
            };
            reinforce.MaterialOverride = brassAccent;
            reinforce.Position = new Vector3(
                corner[0] * (width / 2 - 0.02f * scale),
                corner[1] * (height / 2 - 0.02f * scale),
                thickness + 0.004f * scale
            );
            shield.AddChild(reinforce);
        }

        // === ARM STRAPS (multiple for support) ===
        for (int i = 0; i < 3; i++)
        {
            var strap = new MeshInstance3D();
            strap.Mesh = new BoxMesh
            {
                Size = new Vector3(width * 0.4f, 0.05f * scale, 0.015f * scale)
            };
            strap.MaterialOverride = CreateLeatherMaterial();
            strap.Position = new Vector3(0, height * (0.2f - i * 0.2f), -thickness * 0.3f);
            shield.AddChild(strap);
        }

        return shield;
    }

    /// <summary>
    /// Create a Spiked Shield - offensive round shield with protruding spikes and
    /// aggressive design.
    /// </summary>
    public static Node3D CreateSpikedShield(float scale = 1f,
        StandardMaterial3D? primaryMaterial = null, StandardMaterial3D? accentMaterial = null)
    {
        var shield = new Node3D();
        shield.Name = "SpikedShield";

        var metalMat = primaryMaterial ?? CreateMetalMaterial(new Color(0.30f, 0.28f, 0.26f));
        var darkMat = accentMaterial ?? CreateMetalMaterial(new Color(0.18f, 0.16f, 0.14f));
        var spikeMat = CreateMetalMaterial(new Color(0.50f, 0.48f, 0.45f));
        var bloodMat = CreateMetalMaterial(new Color(0.35f, 0.08f, 0.05f));

        float radius = 0.24f * scale;
        float thickness = 0.025f * scale;

        // === MAIN SHIELD BODY ===
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius * 0.95f,
            Height = thickness,
            RadialSegments = 24
        };
        body.MaterialOverride = metalMat;
        body.RotationDegrees = new Vector3(90, 0, 0);
        body.Position = new Vector3(0, 0, thickness / 2);
        shield.AddChild(body);

        // === DOMED CENTER ===
        var dome = new MeshInstance3D();
        dome.Mesh = new SphereMesh
        {
            Radius = radius * 0.6f,
            Height = radius * 0.4f,
            RadialSegments = 16
        };
        dome.MaterialOverride = darkMat;
        dome.Position = new Vector3(0, 0, thickness);
        dome.Scale = new Vector3(1, 1, 0.35f);
        shield.AddChild(dome);

        // === CENTRAL SPIKE (large, menacing) ===
        var centralSpike = new MeshInstance3D();
        centralSpike.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = 0.03f * scale,
            Height = 0.12f * scale,
            RadialSegments = 6
        };
        centralSpike.MaterialOverride = spikeMat;
        centralSpike.RotationDegrees = new Vector3(90, 0, 0);
        centralSpike.Position = new Vector3(0, 0, thickness + 0.06f * scale);
        shield.AddChild(centralSpike);

        // === RING OF OUTER SPIKES ===
        int outerSpikeCount = 8;
        for (int i = 0; i < outerSpikeCount; i++)
        {
            float angle = (float)i / outerSpikeCount * Mathf.Pi * 2;
            float spikeDist = radius * 0.7f;

            var spike = new MeshInstance3D();
            spike.Mesh = new CylinderMesh
            {
                TopRadius = 0.001f * scale,
                BottomRadius = 0.018f * scale,
                Height = 0.07f * scale,
                RadialSegments = 5
            };
            spike.MaterialOverride = spikeMat;
            spike.RotationDegrees = new Vector3(90, 0, 0);
            spike.Position = new Vector3(
                Mathf.Cos(angle) * spikeDist,
                Mathf.Sin(angle) * spikeDist,
                thickness + 0.035f * scale
            );
            shield.AddChild(spike);
        }

        // === INNER RING OF SMALLER SPIKES ===
        int innerSpikeCount = 6;
        for (int i = 0; i < innerSpikeCount; i++)
        {
            float angle = (float)i / innerSpikeCount * Mathf.Pi * 2 + 0.3f;
            float spikeDist = radius * 0.4f;

            var spike = new MeshInstance3D();
            spike.Mesh = new CylinderMesh
            {
                TopRadius = 0.001f * scale,
                BottomRadius = 0.012f * scale,
                Height = 0.05f * scale,
                RadialSegments = 5
            };
            spike.MaterialOverride = spikeMat;
            spike.RotationDegrees = new Vector3(90, 0, 0);
            spike.Position = new Vector3(
                Mathf.Cos(angle) * spikeDist,
                Mathf.Sin(angle) * spikeDist,
                thickness + 0.025f * scale
            );
            shield.AddChild(spike);
        }

        // === SERRATED EDGE ===
        int edgeSpikeCount = 12;
        for (int i = 0; i < edgeSpikeCount; i++)
        {
            float angle = (float)i / edgeSpikeCount * Mathf.Pi * 2;

            var edgeSpike = new MeshInstance3D();
            edgeSpike.Mesh = new PrismMesh
            {
                Size = new Vector3(0.025f * scale, 0.04f * scale, thickness),
                LeftToRight = 0.5f
            };
            edgeSpike.MaterialOverride = darkMat;
            edgeSpike.RotationDegrees = new Vector3(0, 0, Mathf.RadToDeg(angle) + 90);
            edgeSpike.Position = new Vector3(
                Mathf.Cos(angle) * (radius + 0.01f * scale),
                Mathf.Sin(angle) * (radius + 0.01f * scale),
                thickness / 2
            );
            shield.AddChild(edgeSpike);
        }

        // === BLOOD STAINS (weathering detail) ===
        var stain1 = new MeshInstance3D();
        stain1.Mesh = new BoxMesh
        {
            Size = new Vector3(0.04f * scale, 0.06f * scale, 0.001f * scale)
        };
        stain1.MaterialOverride = bloodMat;
        stain1.Position = new Vector3(radius * 0.3f, -radius * 0.2f, thickness + 0.001f * scale);
        stain1.RotationDegrees = new Vector3(0, 0, 25);
        shield.AddChild(stain1);

        // === REINFORCED RIM (rivets around edge, no torus to avoid ring artifact) ===
        int rimRivetCount = 12;
        for (int i = 0; i < rimRivetCount; i++)
        {
            float angle = (float)i / rimRivetCount * Mathf.Pi * 2;
            var rivet = new MeshInstance3D();
            rivet.Mesh = new SphereMesh
            {
                Radius = 0.01f * scale,
                Height = 0.02f * scale
            };
            rivet.MaterialOverride = darkMat;
            rivet.Position = new Vector3(
                Mathf.Cos(angle) * (radius - 0.015f * scale),
                Mathf.Sin(angle) * (radius - 0.015f * scale),
                thickness + 0.005f * scale
            );
            shield.AddChild(rivet);
        }

        // === GRIP (aggressive design) ===
        var grip = new MeshInstance3D();
        grip.Mesh = new BoxMesh
        {
            Size = new Vector3(radius * 0.6f, 0.04f * scale, 0.025f * scale)
        };
        grip.MaterialOverride = CreateLeatherMaterial(new Color(0.20f, 0.12f, 0.08f));
        grip.Position = new Vector3(0, 0, -thickness * 0.4f);
        shield.AddChild(grip);

        // === WRIST GUARD ===
        var wristGuard = new MeshInstance3D();
        wristGuard.Mesh = new CylinderMesh
        {
            TopRadius = 0.05f * scale,
            BottomRadius = 0.05f * scale,
            Height = 0.06f * scale,
            RadialSegments = 12
        };
        wristGuard.MaterialOverride = darkMat;
        wristGuard.RotationDegrees = new Vector3(0, 0, 90);
        wristGuard.Position = new Vector3(-radius * 0.25f, 0, -thickness * 0.3f);
        wristGuard.Scale = new Vector3(1, 0.6f, 1);
        shield.AddChild(wristGuard);

        return shield;
    }
}
