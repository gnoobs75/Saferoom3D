using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Factory for creating procedural weapon meshes with consistent attachment points.
/// Used by MonsterMeshFactory for humanoid enemies and EditorScreen3D for previewing.
///
/// === WEAPON COORDINATE SYSTEM ===
/// All weapons are built in "ready to swing" orientation as held by a character:
/// - Origin (0,0,0): Center of the grip where hand holds
/// - +Z: Forward toward enemy (blade/point direction)
/// - -Z: Backward (pommel direction)
/// - +Y: Up (toward sky when character stands)
/// - +X: Right side
///
/// The handle runs along the Z-axis (back toward pommel).
/// The blade/head extends forward along +Z.
///
/// === ATTACHMENT ===
/// When attaching to a humanoid, the weapon is positioned at the hand with
/// the blade pointing in the character's forward direction. The Hand node
/// should be positioned at the palm with no rotation needed - weapons attach
/// directly and will point in the correct direction.
/// </summary>
public static class WeaponFactory
{
    /// <summary>
    /// Available weapon types for the factory.
    /// </summary>
    public enum WeaponType
    {
        Dagger,
        ShortSword,
        LongSword,
        Axe,
        BattleAxe,
        Spear,
        Mace,
        WarHammer,
        Staff,
        Bow,
        Club,
        Scythe
    }

    /// <summary>
    /// Weapon stats and attachment configuration.
    /// </summary>
    public struct WeaponData
    {
        public string Name;
        public WeaponType Type;
        public float DamageBonus;
        public float SpeedModifier;  // 1.0 = normal, <1 = slower, >1 = faster
        public float Range;          // Attack range in meters
        public bool IsTwoHanded;

        public static WeaponData Default => new()
        {
            Name = "Unknown",
            Type = WeaponType.ShortSword,
            DamageBonus = 0f,
            SpeedModifier = 1f,
            Range = 2f,
            IsTwoHanded = false
        };
    }

    // Cached weapon data for all types
    private static readonly Dictionary<WeaponType, WeaponData> _weaponStats = new()
    {
        [WeaponType.Dagger] = new WeaponData
        {
            Name = "Dagger",
            Type = WeaponType.Dagger,
            DamageBonus = 5f,
            SpeedModifier = 1.4f,
            Range = 1.2f,
            IsTwoHanded = false
        },
        [WeaponType.ShortSword] = new WeaponData
        {
            Name = "Short Sword",
            Type = WeaponType.ShortSword,
            DamageBonus = 10f,
            SpeedModifier = 1.2f,
            Range = 1.8f,
            IsTwoHanded = false
        },
        [WeaponType.LongSword] = new WeaponData
        {
            Name = "Long Sword",
            Type = WeaponType.LongSword,
            DamageBonus = 18f,
            SpeedModifier = 0.9f,
            Range = 2.5f,
            IsTwoHanded = true
        },
        [WeaponType.Axe] = new WeaponData
        {
            Name = "Axe",
            Type = WeaponType.Axe,
            DamageBonus = 15f,
            SpeedModifier = 1.0f,
            Range = 1.8f,
            IsTwoHanded = false
        },
        [WeaponType.BattleAxe] = new WeaponData
        {
            Name = "Battle Axe",
            Type = WeaponType.BattleAxe,
            DamageBonus = 25f,
            SpeedModifier = 0.7f,
            Range = 2.2f,
            IsTwoHanded = true
        },
        [WeaponType.Spear] = new WeaponData
        {
            Name = "Spear",
            Type = WeaponType.Spear,
            DamageBonus = 14f,
            SpeedModifier = 0.85f,
            Range = 3.0f,
            IsTwoHanded = true
        },
        [WeaponType.Mace] = new WeaponData
        {
            Name = "Mace",
            Type = WeaponType.Mace,
            DamageBonus = 16f,
            SpeedModifier = 0.95f,
            Range = 1.6f,
            IsTwoHanded = false
        },
        [WeaponType.WarHammer] = new WeaponData
        {
            Name = "War Hammer",
            Type = WeaponType.WarHammer,
            DamageBonus = 28f,
            SpeedModifier = 0.65f,
            Range = 2.0f,
            IsTwoHanded = true
        },
        [WeaponType.Staff] = new WeaponData
        {
            Name = "Staff",
            Type = WeaponType.Staff,
            DamageBonus = 6f,
            SpeedModifier = 1.1f,
            Range = 2.5f,
            IsTwoHanded = true
        },
        [WeaponType.Bow] = new WeaponData
        {
            Name = "Bow",
            Type = WeaponType.Bow,
            DamageBonus = 12f,
            SpeedModifier = 0.8f,
            Range = 15f,
            IsTwoHanded = true
        },
        [WeaponType.Club] = new WeaponData
        {
            Name = "Club",
            Type = WeaponType.Club,
            DamageBonus = 8f,
            SpeedModifier = 1.1f,
            Range = 1.5f,
            IsTwoHanded = false
        },
        [WeaponType.Scythe] = new WeaponData
        {
            Name = "Scythe",
            Type = WeaponType.Scythe,
            DamageBonus = 22f,
            SpeedModifier = 0.75f,
            Range = 2.8f,
            IsTwoHanded = true
        }
    };

    /// <summary>
    /// Get all available weapon type names for UI display.
    /// </summary>
    public static string[] GetWeaponTypes()
    {
        var types = Enum.GetValues<WeaponType>();
        var names = new string[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            names[i] = _weaponStats[types[i]].Name;
        }
        return names;
    }

    /// <summary>
    /// Get weapon data for a specific type.
    /// </summary>
    public static WeaponData GetWeaponData(WeaponType type)
    {
        return _weaponStats.TryGetValue(type, out var data) ? data : WeaponData.Default;
    }

    /// <summary>
    /// Get weapon type from display name.
    /// </summary>
    public static WeaponType GetWeaponTypeFromName(string name)
    {
        foreach (var kvp in _weaponStats)
        {
            if (kvp.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        return WeaponType.ShortSword;
    }

    /// <summary>
    /// Create a weapon mesh of the specified type.
    /// Weapons point forward along +Z axis, ready to swing.
    /// </summary>
    public static Node3D CreateWeapon(WeaponType type, float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        return type switch
        {
            WeaponType.Dagger => CreateDagger(scale, bladeMaterial, handleMaterial),
            WeaponType.ShortSword => CreateShortSword(scale, bladeMaterial, handleMaterial),
            WeaponType.LongSword => CreateLongSword(scale, bladeMaterial, handleMaterial),
            WeaponType.Axe => CreateAxe(scale, bladeMaterial, handleMaterial),
            WeaponType.BattleAxe => CreateBattleAxe(scale, bladeMaterial, handleMaterial),
            WeaponType.Spear => CreateSpear(scale, bladeMaterial, handleMaterial),
            WeaponType.Mace => CreateMace(scale, bladeMaterial, handleMaterial),
            WeaponType.WarHammer => CreateWarHammer(scale, bladeMaterial, handleMaterial),
            WeaponType.Staff => CreateStaff(scale, bladeMaterial, handleMaterial),
            WeaponType.Bow => CreateBow(scale, bladeMaterial, handleMaterial),
            WeaponType.Club => CreateClub(scale, handleMaterial),
            WeaponType.Scythe => CreateScythe(scale, bladeMaterial, handleMaterial),
            _ => CreateShortSword(scale, bladeMaterial, handleMaterial)
        };
    }

    /// <summary>
    /// Attach a weapon to a humanoid monster's right hand.
    /// </summary>
    public static void AttachToHumanoid(Node3D weapon, MonsterMeshFactory.LimbNodes limbs, float scale = 1f)
    {
        if (limbs.RightArm == null)
        {
            GD.PrintErr("[WeaponFactory] Cannot attach weapon: RightArm is null");
            return;
        }

        // Look for a Hand child node on the arm
        Node3D? attachPoint = limbs.RightArm.GetNodeOrNull<Node3D>("Hand");

        if (attachPoint != null)
        {
            // Hand node exists - attach directly, no offset needed
            weapon.Position = Vector3.Zero;
            weapon.RotationDegrees = Vector3.Zero;
            attachPoint.AddChild(weapon);
            GD.Print($"[WeaponFactory] Attached weapon to Hand node");
        }
        else
        {
            // Fallback: attach to end of arm
            // Position near the end of the arm, weapon pointing forward
            weapon.Position = new Vector3(0f, -0.4f * scale, 0.1f * scale);
            weapon.RotationDegrees = Vector3.Zero;
            limbs.RightArm.AddChild(weapon);
            GD.Print($"[WeaponFactory] Attached weapon with fallback positioning");
        }

        limbs.Weapon = weapon;
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
            Roughness = 0.3f
        };
    }

    private static StandardMaterial3D CreateWoodMaterial(Color? color = null)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color ?? new Color(0.4f, 0.25f, 0.15f),
            Metallic = 0f,
            Roughness = 0.85f
        };
    }

    private static StandardMaterial3D CreateLeatherMaterial(Color? color = null)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color ?? new Color(0.35f, 0.2f, 0.1f),
            Metallic = 0f,
            Roughness = 0.7f
        };
    }

    // ========================================================================
    // WEAPON CREATION METHODS
    // All weapons built with blade/head pointing forward (+Z), handle back (-Z)
    // Cylinders are rotated 90° on X to align with Z axis
    // ========================================================================

    /// <summary>
    /// Create a dagger - small, quick blade with decorative guard, fuller, ornate pommel,
    /// leather-wrapped grip, and blade edge bevels.
    /// </summary>
    public static Node3D CreateDagger(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Dagger";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.7f, 0.7f, 0.75f));
        var handleMat = handleMaterial ?? CreateLeatherMaterial();
        var guardMat = CreateMetalMaterial(new Color(0.5f, 0.45f, 0.4f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.35f, 0.33f, 0.30f)); // For fuller
        var brassMat = CreateMetalMaterial(new Color(0.72f, 0.58f, 0.30f)); // For pommel accents

        float handleLength = 0.10f * scale;
        float handleRadius = 0.016f * scale;
        float bladeLength = 0.18f * scale;
        float bladeWidth = 0.024f * scale;

        // === HANDLE CORE (extends backward in -Z) ===
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 0.9f,
            Height = handleLength,
            RadialSegments = 8
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // === LEATHER GRIP WRAPPING (spiral bands) ===
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float z = -handleLength * 0.85f + t * handleLength * 0.7f;
            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.12f,
                BottomRadius = handleRadius * 1.12f,
                Height = 0.012f * scale,
                RadialSegments = 8
            };
            gripBand.MaterialOverride = handleMat;
            gripBand.Position = new Vector3(0, 0, z);
            gripBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(gripBand);
        }

        // === ORNATE POMMEL (multi-part) ===
        // Main pommel body
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.6f,
            Height = 0.018f * scale,
            RadialSegments = 8
        };
        pommel.MaterialOverride = guardMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.009f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Pommel decorative ring
        var pommelRing = new MeshInstance3D();
        pommelRing.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.7f,
            BottomRadius = handleRadius * 1.7f,
            Height = 0.006f * scale,
            RadialSegments = 10
        };
        pommelRing.MaterialOverride = brassMat;
        pommelRing.Position = new Vector3(0, 0, -handleLength - 0.015f * scale);
        pommelRing.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelRing);

        // Pommel end cap (pointed)
        var pommelCap = new MeshInstance3D();
        pommelCap.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = handleRadius * 1.4f,
            Height = 0.015f * scale,
            RadialSegments = 8
        };
        pommelCap.MaterialOverride = guardMat;
        pommelCap.Position = new Vector3(0, 0, -handleLength - 0.026f * scale);
        pommelCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelCap);

        // === DECORATIVE GUARD/CROSSGUARD ===
        // Main guard (cross-piece at origin)
        var guard = new MeshInstance3D();
        guard.Mesh = new BoxMesh
        {
            Size = new Vector3(0.055f * scale, 0.016f * scale, 0.014f * scale)
        };
        guard.MaterialOverride = guardMat;
        guard.Position = new Vector3(0, 0, 0.006f * scale);
        weapon.AddChild(guard);

        // Guard end caps (decorative spherical ends using cylinders)
        for (int side = -1; side <= 1; side += 2)
        {
            var guardCap = new MeshInstance3D();
            float capRadius = 0.009f * scale;
            guardCap.Mesh = new CylinderMesh
            {
                TopRadius = capRadius * 0.5f,
                BottomRadius = capRadius,
                Height = capRadius * 1.2f,
                RadialSegments = 6
            };
            guardCap.MaterialOverride = brassMat;
            guardCap.Position = new Vector3(side * 0.028f * scale, 0, 0.006f * scale);
            guardCap.RotationDegrees = new Vector3(0, 0, side * -90);
            weapon.AddChild(guardCap);
        }

        // Guard decorative ridge (center accent)
        var guardRidge = new MeshInstance3D();
        guardRidge.Mesh = new BoxMesh
        {
            Size = new Vector3(0.03f * scale, 0.018f * scale, 0.006f * scale)
        };
        guardRidge.MaterialOverride = brassMat;
        guardRidge.Position = new Vector3(0, 0, 0.006f * scale);
        weapon.AddChild(guardRidge);

        // === BLADE (extends forward in +Z) ===
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth, 0.006f * scale, bladeLength)
        };
        blade.MaterialOverride = bladeMat;
        blade.Position = new Vector3(0, 0, 0.015f * scale + bladeLength / 2f);
        weapon.AddChild(blade);

        // === BLADE EDGE BEVELS (sharpened edges on both sides) ===
        for (int side = -1; side <= 1; side += 2)
        {
            var bevel = new MeshInstance3D();
            bevel.Mesh = new BoxMesh
            {
                Size = new Vector3(0.004f * scale, 0.002f * scale, bladeLength * 0.95f)
            };
            bevel.MaterialOverride = bladeMat;
            bevel.Position = new Vector3(side * (bladeWidth / 2f + 0.001f * scale), -0.002f * scale, 0.015f * scale + bladeLength / 2f);
            weapon.AddChild(bevel);
        }

        // === FULLER (blood groove) - thin darker line down blade ===
        var fuller = new MeshInstance3D();
        fuller.Mesh = new BoxMesh
        {
            Size = new Vector3(0.005f * scale, 0.008f * scale, bladeLength * 0.65f)
        };
        fuller.MaterialOverride = darkMetalMat;
        fuller.Position = new Vector3(0, 0, 0.015f * scale + bladeLength * 0.3f);
        weapon.AddChild(fuller);

        // === BLADE TIP (tapered cylinder) ===
        var tip = new MeshInstance3D();
        tip.Mesh = new CylinderMesh
        {
            TopRadius = 0.001f * scale,
            BottomRadius = bladeWidth / 2f,
            Height = 0.04f * scale,
            RadialSegments = 4
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(0, 0, 0.015f * scale + bladeLength + 0.02f * scale);
        tip.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(tip);

        return weapon;
    }

    /// <summary>
    /// Create a short sword - balanced one-handed blade with wider crossguard, ribbed grip,
    /// parrying ring, and blade fuller.
    /// </summary>
    public static Node3D CreateShortSword(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "ShortSword";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.75f, 0.75f, 0.8f));
        var handleMat = handleMaterial ?? CreateLeatherMaterial();
        var guardMat = CreateMetalMaterial(new Color(0.55f, 0.5f, 0.45f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.40f, 0.38f, 0.35f)); // For fuller
        var brassMat = CreateMetalMaterial(new Color(0.70f, 0.55f, 0.28f)); // For accents

        float handleLength = 0.14f * scale;
        float handleRadius = 0.018f * scale;
        float bladeLength = 0.40f * scale;
        float bladeWidth = 0.038f * scale;

        // === HANDLE CORE ===
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 0.95f,
            Height = handleLength,
            RadialSegments = 10
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // === RIBBED GRIP (5 raised rings for finger placement) ===
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            float z = -handleLength * 0.9f + t * handleLength * 0.75f;
            var rib = new MeshInstance3D();
            rib.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.18f,
                BottomRadius = handleRadius * 1.18f,
                Height = 0.008f * scale,
                RadialSegments = 10
            };
            rib.MaterialOverride = handleMat;
            rib.Position = new Vector3(0, 0, z);
            rib.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(rib);
        }

        // === FERRULE (metal ring at top of handle) ===
        var ferrule = new MeshInstance3D();
        ferrule.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.25f,
            BottomRadius = handleRadius * 1.1f,
            Height = 0.012f * scale,
            RadialSegments = 10
        };
        ferrule.MaterialOverride = guardMat;
        ferrule.Position = new Vector3(0, 0, -0.006f * scale);
        ferrule.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(ferrule);

        // === POMMEL (decorative end cap) ===
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.6f,
            Height = 0.022f * scale,
            RadialSegments = 10
        };
        pommel.MaterialOverride = guardMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.011f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Pommel accent ring
        var pommelRing = new MeshInstance3D();
        pommelRing.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.7f,
            BottomRadius = handleRadius * 1.7f,
            Height = 0.005f * scale,
            RadialSegments = 10
        };
        pommelRing.MaterialOverride = brassMat;
        pommelRing.Position = new Vector3(0, 0, -handleLength - 0.018f * scale);
        pommelRing.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelRing);

        // Pommel end button
        var pommelButton = new MeshInstance3D();
        pommelButton.Mesh = new CylinderMesh
        {
            TopRadius = 0.004f * scale,
            BottomRadius = handleRadius * 1.3f,
            Height = 0.012f * scale,
            RadialSegments = 8
        };
        pommelButton.MaterialOverride = guardMat;
        pommelButton.Position = new Vector3(0, 0, -handleLength - 0.028f * scale);
        pommelButton.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelButton);

        // === WIDER CROSSGUARD (distinguishes from dagger) ===
        var guard = new MeshInstance3D();
        guard.Mesh = new BoxMesh
        {
            Size = new Vector3(0.12f * scale, 0.022f * scale, 0.018f * scale)
        };
        guard.MaterialOverride = guardMat;
        guard.Position = new Vector3(0, 0, 0.008f * scale);
        weapon.AddChild(guard);

        // Guard ends (curved down decorative tips)
        for (int side = -1; side <= 1; side += 2)
        {
            var guardTip = new MeshInstance3D();
            guardTip.Mesh = new CylinderMesh
            {
                TopRadius = 0.008f * scale,
                BottomRadius = 0.012f * scale,
                Height = 0.018f * scale,
                RadialSegments = 6
            };
            guardTip.MaterialOverride = guardMat;
            guardTip.Position = new Vector3(side * 0.058f * scale, -0.008f * scale, 0.008f * scale);
            guardTip.RotationDegrees = new Vector3(side * 15, 0, 0);
            weapon.AddChild(guardTip);
        }

        // Guard center accent
        var guardAccent = new MeshInstance3D();
        guardAccent.Mesh = new BoxMesh
        {
            Size = new Vector3(0.04f * scale, 0.024f * scale, 0.008f * scale)
        };
        guardAccent.MaterialOverride = brassMat;
        guardAccent.Position = new Vector3(0, 0, 0.008f * scale);
        weapon.AddChild(guardAccent);

        // === PARRYING RING (unique to short sword) ===
        // Creates a loop on one side of the guard for catching blades
        float ringRadius = 0.025f * scale;
        float ringThickness = 0.005f * scale;

        // Ring outer arc (approximated with boxes)
        for (int i = 0; i < 5; i++)
        {
            float angle = -60 + i * 30; // -60 to 60 degrees
            float rad = Mathf.DegToRad(angle);
            float x = 0.05f * scale + Mathf.Cos(rad) * ringRadius;
            float y = Mathf.Sin(rad) * ringRadius;

            var ringSegment = new MeshInstance3D();
            ringSegment.Mesh = new CylinderMesh
            {
                TopRadius = ringThickness,
                BottomRadius = ringThickness,
                Height = 0.012f * scale,
                RadialSegments = 6
            };
            ringSegment.MaterialOverride = guardMat;
            ringSegment.Position = new Vector3(x, y, 0.008f * scale);
            ringSegment.RotationDegrees = new Vector3(90, 0, angle);
            weapon.AddChild(ringSegment);
        }

        // === BLADE ===
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth, 0.009f * scale, bladeLength)
        };
        blade.MaterialOverride = bladeMat;
        blade.Position = new Vector3(0, 0, 0.018f * scale + bladeLength / 2f);
        weapon.AddChild(blade);

        // === BLADE EDGE BEVELS ===
        for (int side = -1; side <= 1; side += 2)
        {
            var bevel = new MeshInstance3D();
            bevel.Mesh = new BoxMesh
            {
                Size = new Vector3(0.005f * scale, 0.003f * scale, bladeLength * 0.92f)
            };
            bevel.MaterialOverride = bladeMat;
            bevel.Position = new Vector3(side * (bladeWidth / 2f + 0.001f * scale), -0.003f * scale, 0.018f * scale + bladeLength / 2f);
            weapon.AddChild(bevel);
        }

        // === FULLER (blood groove) ===
        var fuller = new MeshInstance3D();
        fuller.Mesh = new BoxMesh
        {
            Size = new Vector3(0.008f * scale, 0.011f * scale, bladeLength * 0.7f)
        };
        fuller.MaterialOverride = darkMetalMat;
        fuller.Position = new Vector3(0, 0, 0.018f * scale + bladeLength * 0.32f);
        weapon.AddChild(fuller);

        // === BLADE TIP (tapered cylinder) ===
        var tip = new MeshInstance3D();
        tip.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = bladeWidth / 2f,
            Height = 0.06f * scale,
            RadialSegments = 4
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(0, 0, 0.018f * scale + bladeLength + 0.03f * scale);
        tip.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(tip);

        return weapon;
    }

    /// <summary>
    /// Create a long sword - two-handed blade with pronounced fuller,
    /// decorative pommel, rain guard, and grip wrapping.
    /// </summary>
    public static Node3D CreateLongSword(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "LongSword";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.8f, 0.8f, 0.85f));
        var handleMat = handleMaterial ?? CreateLeatherMaterial();
        var guardMat = CreateMetalMaterial(new Color(0.6f, 0.55f, 0.5f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.45f, 0.43f, 0.40f)); // For fuller
        var brassMat = CreateMetalMaterial(new Color(0.72f, 0.58f, 0.30f)); // For accents

        float handleLength = 0.25f * scale;
        float handleRadius = 0.02f * scale;
        float bladeLength = 0.65f * scale;
        float bladeWidth = 0.048f * scale;

        // === HANDLE CORE ===
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 0.9f,
            Height = handleLength,
            RadialSegments = 12
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // === GRIP WRAPPING (spiral leather bands) ===
        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f;
            float z = -handleLength * 0.9f + t * handleLength * 0.8f;
            var wrapBand = new MeshInstance3D();
            wrapBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.15f,
                BottomRadius = handleRadius * 1.15f,
                Height = 0.012f * scale,
                RadialSegments = 12
            };
            wrapBand.MaterialOverride = handleMat;
            wrapBand.Position = new Vector3(0, 0, z);
            wrapBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(wrapBand);
        }

        // === DECORATIVE POMMEL (multi-part) ===
        // Pommel collar (transition ring)
        var pommelCollar = new MeshInstance3D();
        pommelCollar.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.1f,
            Height = 0.012f * scale,
            RadialSegments = 12
        };
        pommelCollar.MaterialOverride = guardMat;
        pommelCollar.Position = new Vector3(0, 0, -handleLength - 0.006f * scale);
        pommelCollar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelCollar);

        // Main pommel body
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.5f,
            BottomRadius = handleRadius * 1.9f,
            Height = 0.028f * scale,
            RadialSegments = 12
        };
        pommel.MaterialOverride = guardMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.026f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Pommel decorative ring
        var pommelRing = new MeshInstance3D();
        pommelRing.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 2.0f,
            BottomRadius = handleRadius * 2.0f,
            Height = 0.006f * scale,
            RadialSegments = 12
        };
        pommelRing.MaterialOverride = brassMat;
        pommelRing.Position = new Vector3(0, 0, -handleLength - 0.035f * scale);
        pommelRing.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelRing);

        // Pommel end cap (finial)
        var pommelCap = new MeshInstance3D();
        pommelCap.Mesh = new CylinderMesh
        {
            TopRadius = 0.005f * scale,
            BottomRadius = handleRadius * 1.6f,
            Height = 0.018f * scale,
            RadialSegments = 10
        };
        pommelCap.MaterialOverride = guardMat;
        pommelCap.Position = new Vector3(0, 0, -handleLength - 0.048f * scale);
        pommelCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelCap);

        // === CROSSGUARD ===
        var guard = new MeshInstance3D();
        guard.Mesh = new BoxMesh
        {
            Size = new Vector3(0.18f * scale, 0.028f * scale, 0.02f * scale)
        };
        guard.MaterialOverride = guardMat;
        guard.Position = new Vector3(0, 0, 0.009f * scale);
        weapon.AddChild(guard);

        // Guard decorative ends (tapered tips)
        for (int side = -1; side <= 1; side += 2)
        {
            var guardTip = new MeshInstance3D();
            guardTip.Mesh = new CylinderMesh
            {
                TopRadius = 0.006f * scale,
                BottomRadius = 0.012f * scale,
                Height = 0.02f * scale,
                RadialSegments = 8
            };
            guardTip.MaterialOverride = guardMat;
            guardTip.Position = new Vector3(side * 0.09f * scale, -0.01f * scale, 0.009f * scale);
            guardTip.RotationDegrees = new Vector3(side * 25, 0, 0);
            weapon.AddChild(guardTip);
        }

        // Guard center accent
        var guardCenter = new MeshInstance3D();
        guardCenter.Mesh = new BoxMesh
        {
            Size = new Vector3(0.05f * scale, 0.032f * scale, 0.01f * scale)
        };
        guardCenter.MaterialOverride = brassMat;
        guardCenter.Position = new Vector3(0, 0, 0.009f * scale);
        weapon.AddChild(guardCenter);

        // === RAIN GUARD (short metal collar just above crossguard on blade) ===
        var rainGuard = new MeshInstance3D();
        rainGuard.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth * 1.15f, 0.012f * scale, 0.025f * scale)
        };
        rainGuard.MaterialOverride = guardMat;
        rainGuard.Position = new Vector3(0, 0, 0.025f * scale);
        weapon.AddChild(rainGuard);

        // Rain guard decorative edge
        var rainGuardEdge = new MeshInstance3D();
        rainGuardEdge.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth * 1.2f, 0.014f * scale, 0.006f * scale)
        };
        rainGuardEdge.MaterialOverride = brassMat;
        rainGuardEdge.Position = new Vector3(0, 0, 0.04f * scale);
        weapon.AddChild(rainGuardEdge);

        // === BLADE ===
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth, 0.011f * scale, bladeLength)
        };
        blade.MaterialOverride = bladeMat;
        blade.Position = new Vector3(0, 0, 0.045f * scale + bladeLength / 2f);
        weapon.AddChild(blade);

        // === BLADE EDGE BEVELS ===
        for (int side = -1; side <= 1; side += 2)
        {
            var bevel = new MeshInstance3D();
            bevel.Mesh = new BoxMesh
            {
                Size = new Vector3(0.006f * scale, 0.004f * scale, bladeLength * 0.92f)
            };
            bevel.MaterialOverride = bladeMat;
            bevel.Position = new Vector3(side * (bladeWidth / 2f + 0.001f * scale), -0.004f * scale, 0.045f * scale + bladeLength / 2f);
            weapon.AddChild(bevel);
        }

        // === PRONOUNCED FULLER (blood groove - deeper and more visible) ===
        // Fuller channel (recessed groove)
        var fuller = new MeshInstance3D();
        fuller.Mesh = new BoxMesh
        {
            Size = new Vector3(0.012f * scale, 0.014f * scale, bladeLength * 0.75f)
        };
        fuller.MaterialOverride = darkMetalMat;
        fuller.Position = new Vector3(0, 0, 0.045f * scale + bladeLength * 0.35f);
        weapon.AddChild(fuller);

        // Fuller edges (raised ridges on either side of channel)
        for (int side = -1; side <= 1; side += 2)
        {
            var fullerEdge = new MeshInstance3D();
            fullerEdge.Mesh = new BoxMesh
            {
                Size = new Vector3(0.003f * scale, 0.015f * scale, bladeLength * 0.72f)
            };
            fullerEdge.MaterialOverride = bladeMat;
            fullerEdge.Position = new Vector3(side * 0.008f * scale, 0, 0.045f * scale + bladeLength * 0.35f);
            weapon.AddChild(fullerEdge);
        }

        // === BLADE TIP (tapered cylinder) ===
        var tip = new MeshInstance3D();
        tip.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = bladeWidth / 2f,
            Height = 0.09f * scale,
            RadialSegments = 4
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(0, 0, 0.045f * scale + bladeLength + 0.045f * scale);
        tip.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(tip);

        return weapon;
    }

    /// <summary>
    /// Create an axe - one-handed chopping weapon with pronounced blade edge,
    /// decorative head etchings, leather grip wrap, and lanyard loop.
    /// </summary>
    public static Node3D CreateAxe(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Axe";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.6f, 0.6f, 0.65f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();
        var leatherMat = CreateLeatherMaterial(new Color(0.32f, 0.20f, 0.10f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.35f, 0.33f, 0.30f)); // For etchings

        float handleLength = 0.35f * scale;
        float handleRadius = 0.022f * scale;
        float headHeight = 0.12f * scale;   // Height of axe head along shaft
        float headWidth = 0.10f * scale;    // Width extending to the side
        float headThickness = 0.025f * scale;
        float collarHeight = 0.04f * scale;

        // === HANDLE (wooden shaft) - extends back from grip ===
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 1.1f,
            Height = handleLength,
            RadialSegments = 10
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // === LEATHER GRIP WRAP (spiral bands) ===
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            float z = -handleLength * 0.85f + t * handleLength * 0.5f;
            float bandRadius = Mathf.Lerp(handleRadius * 1.08f, handleRadius * 1.02f, t);

            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = bandRadius * 1.12f,
                BottomRadius = bandRadius * 1.12f,
                Height = 0.012f * scale,
                RadialSegments = 10
            };
            gripBand.MaterialOverride = leatherMat;
            gripBand.Position = new Vector3(0, 0, z);
            gripBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(gripBand);
        }

        // === LANYARD LOOP (at bottom of handle) ===
        float loopRadius = 0.015f * scale;
        float loopThickness = 0.004f * scale;
        for (int i = 0; i < 5; i++)
        {
            float angle = -90 + i * 45; // Semi-circle
            float rad = Mathf.DegToRad(angle);
            float x = Mathf.Cos(rad) * loopRadius;
            float y = Mathf.Sin(rad) * loopRadius;

            var loopSeg = new MeshInstance3D();
            loopSeg.Mesh = new CylinderMesh
            {
                TopRadius = loopThickness,
                BottomRadius = loopThickness,
                Height = 0.01f * scale,
                RadialSegments = 6
            };
            loopSeg.MaterialOverride = leatherMat;
            loopSeg.Position = new Vector3(x, y - handleRadius * 1.2f, -handleLength - 0.01f * scale);
            loopSeg.RotationDegrees = new Vector3(0, 0, angle);
            weapon.AddChild(loopSeg);
        }

        // === HANDLE BUTT CAP ===
        var buttCap = new MeshInstance3D();
        buttCap.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.1f,
            Height = 0.02f * scale,
            RadialSegments = 10
        };
        buttCap.MaterialOverride = darkMetalMat;
        buttCap.Position = new Vector3(0, 0, -handleLength - 0.01f * scale);
        buttCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttCap);

        // === AXE HEAD COLLAR ===
        float collarZ = collarHeight / 2f;
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.6f,
            BottomRadius = handleRadius * 1.6f,
            Height = collarHeight,
            RadialSegments = 10
        };
        collar.MaterialOverride = bladeMat;
        collar.Position = new Vector3(0, 0, collarZ);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // === AXE HEAD MAIN BODY ===
        float headStartZ = collarZ + collarHeight / 2f;
        var head = new MeshInstance3D();
        head.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth, headThickness, headHeight)
        };
        head.MaterialOverride = bladeMat;
        head.Position = new Vector3(headWidth / 2f + handleRadius * 0.5f, 0, headStartZ + headHeight / 2f);
        weapon.AddChild(head);

        // === BLADE EDGE (using tapered box instead of PrismMesh) ===
        // Multiple overlapping boxes to create edge taper effect
        for (int i = 0; i < 3; i++)
        {
            float t = i / 2f;
            float edgeWidth = Mathf.Lerp(0.015f, 0.005f, t) * scale;
            float edgeThickness = Mathf.Lerp(headThickness * 0.9f, headThickness * 0.3f, t);

            var edge = new MeshInstance3D();
            edge.Mesh = new BoxMesh
            {
                Size = new Vector3(edgeWidth, edgeThickness, headHeight * 0.88f)
            };
            edge.MaterialOverride = bladeMat;
            edge.Position = new Vector3(
                headWidth + handleRadius * 0.5f + 0.007f * scale + t * 0.008f * scale,
                0,
                headStartZ + headHeight / 2f
            );
            weapon.AddChild(edge);
        }

        // === DECORATIVE ETCHINGS (box patterns on head) ===
        // Border etching
        var etchBorder = new MeshInstance3D();
        etchBorder.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth * 0.7f, headThickness * 0.3f, headHeight * 0.7f)
        };
        etchBorder.MaterialOverride = darkMetalMat;
        etchBorder.Position = new Vector3(headWidth * 0.45f + handleRadius * 0.5f, headThickness * 0.4f, headStartZ + headHeight / 2f);
        weapon.AddChild(etchBorder);

        // Inner etching pattern (cross pattern with boxes)
        var etchCenter = new MeshInstance3D();
        etchCenter.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth * 0.5f, headThickness * 0.35f, headHeight * 0.5f)
        };
        etchCenter.MaterialOverride = bladeMat;
        etchCenter.Position = new Vector3(headWidth * 0.45f + handleRadius * 0.5f, headThickness * 0.42f, headStartZ + headHeight / 2f);
        weapon.AddChild(etchCenter);

        // Horizontal etching line
        var etchHLine = new MeshInstance3D();
        etchHLine.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth * 0.6f, headThickness * 0.36f, 0.008f * scale)
        };
        etchHLine.MaterialOverride = darkMetalMat;
        etchHLine.Position = new Vector3(headWidth * 0.45f + handleRadius * 0.5f, headThickness * 0.43f, headStartZ + headHeight / 2f);
        weapon.AddChild(etchHLine);

        // Vertical etching line
        var etchVLine = new MeshInstance3D();
        etchVLine.Mesh = new BoxMesh
        {
            Size = new Vector3(0.008f * scale, headThickness * 0.36f, headHeight * 0.4f)
        };
        etchVLine.MaterialOverride = darkMetalMat;
        etchVLine.Position = new Vector3(headWidth * 0.45f + handleRadius * 0.5f, headThickness * 0.43f, headStartZ + headHeight / 2f);
        weapon.AddChild(etchVLine);

        // === INNER CONNECTION PIECE TO SHAFT ===
        var inner = new MeshInstance3D();
        inner.Mesh = new BoxMesh
        {
            Size = new Vector3(handleRadius * 2f, headThickness, headHeight * 0.8f)
        };
        inner.MaterialOverride = bladeMat;
        inner.Position = new Vector3(0, 0, headStartZ + headHeight / 2f);
        weapon.AddChild(inner);

        // === BEARD (bottom extension of axe head) ===
        var beard = new MeshInstance3D();
        beard.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth * 0.4f, headThickness * 0.8f, 0.025f * scale)
        };
        beard.MaterialOverride = bladeMat;
        beard.Position = new Vector3(headWidth * 0.35f + handleRadius * 0.5f, 0, headStartZ - 0.01f * scale);
        weapon.AddChild(beard);

        return weapon;
    }

    /// <summary>
    /// Create a battle axe - two-handed double-headed axe with decorative etching and grip wrapping.
    /// </summary>
    public static Node3D CreateBattleAxe(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "BattleAxe";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.55f, 0.55f, 0.6f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();
        var leatherMat = CreateLeatherMaterial(new Color(0.30f, 0.18f, 0.10f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.38f, 0.36f, 0.34f)); // For etchings

        float handleLength = 0.5f * scale;
        float handleRadius = 0.025f * scale;
        float headHeight = 0.14f * scale;   // Height along shaft
        float headWidth = 0.08f * scale;    // Width to each side
        float headThickness = 0.02f * scale;

        // === LONG HANDLE ===
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 1.15f,
            Height = handleLength,
            RadialSegments = 12
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // === GRIP WRAPPING (leather bands at primary grip) ===
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float z = -handleLength * 0.85f + t * handleLength * 0.35f;
            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.12f,
                BottomRadius = handleRadius * 1.12f,
                Height = 0.018f * scale,
                RadialSegments = 12
            };
            gripBand.MaterialOverride = leatherMat;
            gripBand.Position = new Vector3(0, 0, z);
            gripBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(gripBand);
        }

        // === SECONDARY GRIP BANDS (for two-handed use) ===
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float z = -handleLength * 0.25f + t * handleLength * 0.2f;
            var secondBand = new MeshInstance3D();
            secondBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.10f,
                BottomRadius = handleRadius * 1.10f,
                Height = 0.015f * scale,
                RadialSegments = 12
            };
            secondBand.MaterialOverride = leatherMat;
            secondBand.Position = new Vector3(0, 0, z);
            secondBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(secondBand);
        }

        // === BUTT CAP ===
        var buttCap = new MeshInstance3D();
        buttCap.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.25f,
            BottomRadius = handleRadius * 1.45f,
            Height = 0.025f * scale,
            RadialSegments = 12
        };
        buttCap.MaterialOverride = darkMetalMat;
        buttCap.Position = new Vector3(0, 0, -handleLength - 0.012f * scale);
        buttCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttCap);

        // Butt cap finial
        var buttFinial = new MeshInstance3D();
        buttFinial.Mesh = new CylinderMesh
        {
            TopRadius = 0.005f * scale,
            BottomRadius = handleRadius * 1.2f,
            Height = 0.015f * scale,
            RadialSegments = 10
        };
        buttFinial.MaterialOverride = darkMetalMat;
        buttFinial.Position = new Vector3(0, 0, -handleLength - 0.032f * scale);
        buttFinial.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttFinial);

        // === METAL REINFORCEMENT BANDS ON SHAFT ===
        float[] bandPositions = { -handleLength * 0.48f, -handleLength * 0.22f, -0.02f * scale };
        foreach (float bandZ in bandPositions)
        {
            var metalBand = new MeshInstance3D();
            metalBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.18f,
                BottomRadius = handleRadius * 1.18f,
                Height = 0.01f * scale,
                RadialSegments = 12
            };
            metalBand.MaterialOverride = darkMetalMat;
            metalBand.Position = new Vector3(0, 0, bandZ);
            metalBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(metalBand);
        }

        // === HEAD COLLAR ===
        float collarHeight = 0.045f * scale;
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.7f,
            BottomRadius = handleRadius * 1.5f,
            Height = collarHeight,
            RadialSegments = 12
        };
        collar.MaterialOverride = bladeMat;
        collar.Position = new Vector3(0, 0, collarHeight / 2f);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Collar decorative ring
        var collarRing = new MeshInstance3D();
        collarRing.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.75f,
            BottomRadius = handleRadius * 1.75f,
            Height = 0.008f * scale,
            RadialSegments = 12
        };
        collarRing.MaterialOverride = darkMetalMat;
        collarRing.Position = new Vector3(0, 0, collarHeight);
        collarRing.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collarRing);

        // === CENTRAL BAR CONNECTING BOTH BLADE HEADS ===
        float headStartZ = collarHeight + 0.004f * scale;
        var central = new MeshInstance3D();
        central.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth * 2f + handleRadius * 2.2f, headThickness, headHeight * 0.52f)
        };
        central.MaterialOverride = bladeMat;
        central.Position = new Vector3(0, 0, headStartZ + headHeight * 0.26f);
        weapon.AddChild(central);

        // === DOUBLE-HEADED AXE BLADES ===
        for (int side = -1; side <= 1; side += 2)
        {
            // Main blade body
            var head = new MeshInstance3D();
            head.Mesh = new BoxMesh
            {
                Size = new Vector3(headWidth, headThickness, headHeight)
            };
            head.MaterialOverride = bladeMat;
            head.Position = new Vector3(side * (headWidth / 2f + handleRadius * 0.65f), 0, headStartZ + headHeight / 2f);
            weapon.AddChild(head);

            // Blade edge (tapered using multiple boxes)
            for (int j = 0; j < 3; j++)
            {
                float t = j / 2f;
                float edgeWidth = Mathf.Lerp(0.018f, 0.006f, t) * scale;
                float edgeThickness = Mathf.Lerp(headThickness * 0.75f, headThickness * 0.25f, t);

                var edge = new MeshInstance3D();
                edge.Mesh = new BoxMesh
                {
                    Size = new Vector3(edgeWidth, edgeThickness, headHeight * 0.88f)
                };
                edge.MaterialOverride = bladeMat;
                edge.Position = new Vector3(
                    side * (headWidth + handleRadius * 0.65f + 0.007f * scale + t * 0.008f * scale),
                    0,
                    headStartZ + headHeight / 2f
                );
                weapon.AddChild(edge);
            }

            // === DECORATIVE ETCHINGS ON BLADE ===
            // Border etching
            var etchBorder = new MeshInstance3D();
            etchBorder.Mesh = new BoxMesh
            {
                Size = new Vector3(headWidth * 0.7f, headThickness * 0.35f, headHeight * 0.72f)
            };
            etchBorder.MaterialOverride = darkMetalMat;
            etchBorder.Position = new Vector3(
                side * (headWidth * 0.4f + handleRadius * 0.65f),
                headThickness * 0.38f,
                headStartZ + headHeight / 2f
            );
            weapon.AddChild(etchBorder);

            // Inner etching (lighter metal showing through)
            var etchInner = new MeshInstance3D();
            etchInner.Mesh = new BoxMesh
            {
                Size = new Vector3(headWidth * 0.5f, headThickness * 0.38f, headHeight * 0.55f)
            };
            etchInner.MaterialOverride = bladeMat;
            etchInner.Position = new Vector3(
                side * (headWidth * 0.4f + handleRadius * 0.65f),
                headThickness * 0.40f,
                headStartZ + headHeight / 2f
            );
            weapon.AddChild(etchInner);

            // Cross etching pattern
            var etchH = new MeshInstance3D();
            etchH.Mesh = new BoxMesh
            {
                Size = new Vector3(headWidth * 0.55f, headThickness * 0.40f, 0.01f * scale)
            };
            etchH.MaterialOverride = darkMetalMat;
            etchH.Position = new Vector3(
                side * (headWidth * 0.4f + handleRadius * 0.65f),
                headThickness * 0.41f,
                headStartZ + headHeight / 2f
            );
            weapon.AddChild(etchH);

            var etchV = new MeshInstance3D();
            etchV.Mesh = new BoxMesh
            {
                Size = new Vector3(0.008f * scale, headThickness * 0.40f, headHeight * 0.45f)
            };
            etchV.MaterialOverride = darkMetalMat;
            etchV.Position = new Vector3(
                side * (headWidth * 0.4f + handleRadius * 0.65f),
                headThickness * 0.41f,
                headStartZ + headHeight / 2f
            );
            weapon.AddChild(etchV);

            // Top and bottom decoration (small triangular accents using boxes)
            float[] yOffsets = { headHeight * 0.42f, -headHeight * 0.42f };
            foreach (float yOffset in yOffsets)
            {
                var accent = new MeshInstance3D();
                accent.Mesh = new BoxMesh
                {
                    Size = new Vector3(headWidth * 0.3f, headThickness * 0.45f, 0.015f * scale)
                };
                accent.MaterialOverride = darkMetalMat;
                accent.Position = new Vector3(
                    side * (headWidth * 0.4f + handleRadius * 0.65f),
                    headThickness * 0.42f,
                    headStartZ + headHeight / 2f + yOffset
                );
                weapon.AddChild(accent);
            }
        }

        // === TOP SPIKE (between the two heads) ===
        var topSpike = new MeshInstance3D();
        topSpike.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = 0.015f * scale,
            Height = 0.045f * scale,
            RadialSegments = 6
        };
        topSpike.MaterialOverride = bladeMat;
        topSpike.Position = new Vector3(0, 0, headStartZ + headHeight + 0.02f * scale);
        topSpike.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(topSpike);

        // Spike base
        var spikeBase = new MeshInstance3D();
        spikeBase.Mesh = new CylinderMesh
        {
            TopRadius = 0.016f * scale,
            BottomRadius = 0.02f * scale,
            Height = 0.012f * scale,
            RadialSegments = 8
        };
        spikeBase.MaterialOverride = darkMetalMat;
        spikeBase.Position = new Vector3(0, 0, headStartZ + headHeight);
        spikeBase.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(spikeBase);

        return weapon;
    }

    /// <summary>
    /// Create a spear - long polearm with leaf-shaped blade, decorative collar bands,
    /// and leather shaft wrapping.
    /// </summary>
    public static Node3D CreateSpear(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Spear";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.7f, 0.7f, 0.75f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();
        var leatherMat = CreateLeatherMaterial(new Color(0.30f, 0.18f, 0.09f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.45f, 0.42f, 0.40f)); // For collar accents

        float shaftLength = 1.0f * scale;
        float shaftRadius = 0.018f * scale;
        float headLength = 0.15f * scale;

        // Shaft - extends back from grip, forward to head
        // Grip is 30% from back end
        float gripOffset = shaftLength * 0.3f;
        float shaftCenterZ = -shaftLength / 2f + gripOffset;
        float shaftFrontZ = shaftCenterZ + shaftLength / 2f;
        float shaftBackZ = shaftCenterZ - shaftLength / 2f;

        // === MAIN SHAFT ===
        var shaft = new MeshInstance3D();
        shaft.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius,
            BottomRadius = shaftRadius,
            Height = shaftLength,
            RadialSegments = 10
        };
        shaft.MaterialOverride = handleMat;
        shaft.Position = new Vector3(0, 0, shaftCenterZ);
        shaft.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(shaft);

        // === LEATHER SHAFT WRAPPING (at grip area) ===
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float z = -0.12f * scale + t * 0.24f * scale; // Around origin (grip point)

            var wrapBand = new MeshInstance3D();
            wrapBand.Mesh = new CylinderMesh
            {
                TopRadius = shaftRadius * 1.15f,
                BottomRadius = shaftRadius * 1.15f,
                Height = 0.018f * scale,
                RadialSegments = 10
            };
            wrapBand.MaterialOverride = leatherMat;
            wrapBand.Position = new Vector3(0, 0, z);
            wrapBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(wrapBand);
        }

        // === BUTT CAP (metal end cap at back of shaft) ===
        var buttCap = new MeshInstance3D();
        buttCap.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.3f,
            BottomRadius = shaftRadius * 1.1f,
            Height = 0.025f * scale,
            RadialSegments = 10
        };
        buttCap.MaterialOverride = darkMetalMat;
        buttCap.Position = new Vector3(0, 0, shaftBackZ + 0.012f * scale);
        buttCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttCap);

        // Butt spike (small point at end for grounding)
        var buttSpike = new MeshInstance3D();
        buttSpike.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = shaftRadius * 0.8f,
            Height = 0.025f * scale,
            RadialSegments = 6
        };
        buttSpike.MaterialOverride = darkMetalMat;
        buttSpike.Position = new Vector3(0, 0, shaftBackZ - 0.008f * scale);
        buttSpike.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttSpike);

        // === DECORATIVE COLLAR BANDS ===
        // Lower collar band (transition from shaft)
        float collarHeight = 0.035f * scale;
        var lowerCollarBand = new MeshInstance3D();
        lowerCollarBand.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.3f,
            BottomRadius = shaftRadius * 1.1f,
            Height = 0.012f * scale,
            RadialSegments = 10
        };
        lowerCollarBand.MaterialOverride = darkMetalMat;
        lowerCollarBand.Position = new Vector3(0, 0, shaftFrontZ + 0.006f * scale);
        lowerCollarBand.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(lowerCollarBand);

        // Main collar (socket for blade)
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.5f,
            BottomRadius = shaftRadius * 1.3f,
            Height = collarHeight,
            RadialSegments = 10
        };
        collar.MaterialOverride = bladeMat;
        collar.Position = new Vector3(0, 0, shaftFrontZ + 0.012f * scale + collarHeight / 2f);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Upper collar band (decorative)
        var upperCollarBand = new MeshInstance3D();
        upperCollarBand.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.55f,
            BottomRadius = shaftRadius * 1.55f,
            Height = 0.008f * scale,
            RadialSegments = 10
        };
        upperCollarBand.MaterialOverride = darkMetalMat;
        upperCollarBand.Position = new Vector3(0, 0, shaftFrontZ + 0.012f * scale + collarHeight);
        upperCollarBand.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(upperCollarBand);

        // === LEAF-SHAPED SPEARHEAD ===
        float headBaseZ = shaftFrontZ + 0.012f * scale + collarHeight + 0.008f * scale;

        // Blade base (widest part of leaf shape)
        var bladeBase = new MeshInstance3D();
        float bladeWidth = 0.035f * scale;
        bladeBase.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth, 0.008f * scale, headLength * 0.35f)
        };
        bladeBase.MaterialOverride = bladeMat;
        bladeBase.Position = new Vector3(0, 0, headBaseZ + headLength * 0.15f);
        weapon.AddChild(bladeBase);

        // Blade middle (tapering)
        var bladeMid = new MeshInstance3D();
        bladeMid.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth * 0.8f, 0.007f * scale, headLength * 0.35f)
        };
        bladeMid.MaterialOverride = bladeMat;
        bladeMid.Position = new Vector3(0, 0, headBaseZ + headLength * 0.45f);
        weapon.AddChild(bladeMid);

        // Blade tip (pointed)
        var bladeTip = new MeshInstance3D();
        bladeTip.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = bladeWidth * 0.35f,
            Height = headLength * 0.35f,
            RadialSegments = 4
        };
        bladeTip.MaterialOverride = bladeMat;
        bladeTip.Position = new Vector3(0, 0, headBaseZ + headLength * 0.78f);
        bladeTip.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(bladeTip);

        // Blade spine (central ridge running down blade)
        var bladeSpine = new MeshInstance3D();
        bladeSpine.Mesh = new BoxMesh
        {
            Size = new Vector3(0.006f * scale, 0.012f * scale, headLength * 0.7f)
        };
        bladeSpine.MaterialOverride = bladeMat;
        bladeSpine.Position = new Vector3(0, 0, headBaseZ + headLength * 0.3f);
        weapon.AddChild(bladeSpine);

        // Blade edge bevels (sharpened edges)
        for (int side = -1; side <= 1; side += 2)
        {
            var bevel = new MeshInstance3D();
            bevel.Mesh = new BoxMesh
            {
                Size = new Vector3(0.004f * scale, 0.003f * scale, headLength * 0.5f)
            };
            bevel.MaterialOverride = bladeMat;
            bevel.Position = new Vector3(side * (bladeWidth / 2f + 0.001f * scale), -0.003f * scale, headBaseZ + headLength * 0.25f);
            weapon.AddChild(bevel);
        }

        return weapon;
    }

    /// <summary>
    /// Create a mace - one-handed blunt weapon with grip wrapping and detailed flanges.
    /// </summary>
    public static Node3D CreateMace(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Mace";

        var headMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.5f, 0.5f, 0.55f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();
        var leatherMat = CreateLeatherMaterial(new Color(0.32f, 0.20f, 0.10f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.38f, 0.36f, 0.34f));

        float handleLength = 0.28f * scale;
        float handleRadius = 0.02f * scale;
        float headRadius = 0.05f * scale;
        float collarHeight = 0.03f * scale;
        float neckHeight = 0.04f * scale;

        // === HANDLE CORE ===
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 1.05f,
            Height = handleLength,
            RadialSegments = 10
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // === GRIP WRAPPING (leather bands) ===
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float z = -handleLength * 0.9f + t * handleLength * 0.7f;
            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.18f,
                BottomRadius = handleRadius * 1.18f,
                Height = 0.015f * scale,
                RadialSegments = 10
            };
            gripBand.MaterialOverride = leatherMat;
            gripBand.Position = new Vector3(0, 0, z);
            gripBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(gripBand);
        }

        // === POMMEL (decorative end cap) ===
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.4f,
            BottomRadius = handleRadius * 1.2f,
            Height = 0.02f * scale,
            RadialSegments = 10
        };
        pommel.MaterialOverride = darkMetalMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.01f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Pommel knob
        var pommelKnob = new MeshInstance3D();
        pommelKnob.Mesh = new CylinderMesh
        {
            TopRadius = 0.004f * scale,
            BottomRadius = handleRadius * 1.1f,
            Height = 0.012f * scale,
            RadialSegments = 8
        };
        pommelKnob.MaterialOverride = darkMetalMat;
        pommelKnob.Position = new Vector3(0, 0, -handleLength - 0.026f * scale);
        pommelKnob.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelKnob);

        // === COLLAR ===
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.6f,
            BottomRadius = handleRadius * 1.3f,
            Height = collarHeight,
            RadialSegments = 10
        };
        collar.MaterialOverride = headMat;
        collar.Position = new Vector3(0, 0, collarHeight / 2f);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Collar decorative ring
        var collarRing = new MeshInstance3D();
        collarRing.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.7f,
            BottomRadius = handleRadius * 1.7f,
            Height = 0.006f * scale,
            RadialSegments = 10
        };
        collarRing.MaterialOverride = darkMetalMat;
        collarRing.Position = new Vector3(0, 0, collarHeight);
        collarRing.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collarRing);

        // === NECK ===
        float neckStartZ = collarHeight + 0.003f * scale;
        var neck = new MeshInstance3D();
        neck.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.2f,
            BottomRadius = handleRadius * 1.5f,
            Height = neckHeight,
            RadialSegments = 10
        };
        neck.MaterialOverride = headMat;
        neck.Position = new Vector3(0, 0, neckStartZ + neckHeight / 2f);
        neck.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(neck);

        // === MACE HEAD ===
        float headHeight = headRadius * 1.6f;
        float headCenterZ = neckStartZ + neckHeight + headHeight / 2f;

        // Main head body
        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh
        {
            TopRadius = headRadius * 0.92f,
            BottomRadius = headRadius * 0.92f,
            Height = headHeight * 0.6f,
            RadialSegments = 10
        };
        head.MaterialOverride = headMat;
        head.Position = new Vector3(0, 0, headCenterZ);
        head.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(head);

        // Top cap
        var topCap = new MeshInstance3D();
        topCap.Mesh = new CylinderMesh
        {
            TopRadius = headRadius * 0.45f,
            BottomRadius = headRadius * 0.92f,
            Height = headHeight * 0.25f,
            RadialSegments = 10
        };
        topCap.MaterialOverride = headMat;
        topCap.Position = new Vector3(0, 0, headCenterZ + headHeight * 0.3f + headHeight * 0.125f);
        topCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(topCap);

        // Bottom cap
        var bottomCap = new MeshInstance3D();
        bottomCap.Mesh = new CylinderMesh
        {
            TopRadius = headRadius * 0.92f,
            BottomRadius = headRadius * 0.55f,
            Height = headHeight * 0.2f,
            RadialSegments = 10
        };
        bottomCap.MaterialOverride = headMat;
        bottomCap.Position = new Vector3(0, 0, headCenterZ - headHeight * 0.3f - headHeight * 0.1f);
        bottomCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(bottomCap);

        // === DETAILED FLANGES (6 ridges with sharp edges) ===
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f;
            float rad = Mathf.DegToRad(angle);
            float x = Mathf.Cos(rad) * headRadius * 0.72f;
            float y = Mathf.Sin(rad) * headRadius * 0.72f;

            // Main flange blade
            var flange = new MeshInstance3D();
            flange.Mesh = new BoxMesh
            {
                Size = new Vector3(0.014f * scale, headRadius * 0.45f, headHeight * 0.72f)
            };
            flange.MaterialOverride = headMat;
            flange.Position = new Vector3(x, y, headCenterZ);
            flange.RotationDegrees = new Vector3(0, 0, angle);
            weapon.AddChild(flange);

            // Flange edge (sharp outer edge)
            float edgeX = Mathf.Cos(rad) * (headRadius * 0.72f + 0.01f * scale);
            float edgeY = Mathf.Sin(rad) * (headRadius * 0.72f + 0.01f * scale);

            var flangeEdge = new MeshInstance3D();
            flangeEdge.Mesh = new BoxMesh
            {
                Size = new Vector3(0.006f * scale, headRadius * 0.35f, headHeight * 0.65f)
            };
            flangeEdge.MaterialOverride = headMat;
            flangeEdge.Position = new Vector3(edgeX, edgeY, headCenterZ);
            flangeEdge.RotationDegrees = new Vector3(0, 0, angle);
            weapon.AddChild(flangeEdge);
        }

        // === TOP SPIKE ===
        var spike = new MeshInstance3D();
        spike.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = 0.014f * scale,
            Height = 0.04f * scale,
            RadialSegments = 6
        };
        spike.MaterialOverride = headMat;
        spike.Position = new Vector3(0, 0, headCenterZ + headHeight * 0.5f + 0.018f * scale);
        spike.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(spike);

        // Spike base collar
        var spikeBase = new MeshInstance3D();
        spikeBase.Mesh = new CylinderMesh
        {
            TopRadius = 0.015f * scale,
            BottomRadius = 0.018f * scale,
            Height = 0.008f * scale,
            RadialSegments = 8
        };
        spikeBase.MaterialOverride = darkMetalMat;
        spikeBase.Position = new Vector3(0, 0, headCenterZ + headHeight * 0.5f);
        spikeBase.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(spikeBase);

        return weapon;
    }

    /// <summary>
    /// Create a war hammer - two-handed heavy hammer with grip bands and detailed spike.
    /// </summary>
    public static Node3D CreateWarHammer(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "WarHammer";

        var headMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.45f, 0.45f, 0.5f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();
        var leatherMat = CreateLeatherMaterial(new Color(0.30f, 0.18f, 0.10f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.35f, 0.33f, 0.30f));
        var brightMetalMat = CreateMetalMaterial(new Color(0.55f, 0.55f, 0.6f));

        float handleLength = 0.55f * scale;
        float handleRadius = 0.025f * scale;
        float headWidth = 0.16f * scale;      // Total width (both sides)
        float headHeight = 0.10f * scale;     // Height and depth of hammer head
        float collarHeight = 0.04f * scale;

        // === LONG HANDLE ===
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 1.08f,
            Height = handleLength,
            RadialSegments = 12
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // === GRIP BANDS (leather wrapping at primary grip) ===
        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f;
            float z = -handleLength * 0.85f + t * handleLength * 0.35f;
            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.15f,
                BottomRadius = handleRadius * 1.15f,
                Height = 0.018f * scale,
                RadialSegments = 12
            };
            gripBand.MaterialOverride = leatherMat;
            gripBand.Position = new Vector3(0, 0, z);
            gripBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(gripBand);
        }

        // === SECONDARY GRIP BANDS (for two-handed use) ===
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float z = -handleLength * 0.15f + t * handleLength * 0.2f;
            var secondBand = new MeshInstance3D();
            secondBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.12f,
                BottomRadius = handleRadius * 1.12f,
                Height = 0.015f * scale,
                RadialSegments = 12
            };
            secondBand.MaterialOverride = leatherMat;
            secondBand.Position = new Vector3(0, 0, z);
            secondBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(secondBand);
        }

        // === POMMEL (metal end cap) ===
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.4f,
            BottomRadius = handleRadius * 1.2f,
            Height = 0.025f * scale,
            RadialSegments = 12
        };
        pommel.MaterialOverride = darkMetalMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.012f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Pommel finial
        var pommelFinial = new MeshInstance3D();
        pommelFinial.Mesh = new CylinderMesh
        {
            TopRadius = 0.005f * scale,
            BottomRadius = handleRadius * 1.2f,
            Height = 0.018f * scale,
            RadialSegments = 10
        };
        pommelFinial.MaterialOverride = darkMetalMat;
        pommelFinial.Position = new Vector3(0, 0, -handleLength - 0.033f * scale);
        pommelFinial.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelFinial);

        // === METAL REINFORCEMENT BANDS ON SHAFT ===
        float[] bandPositions = { -handleLength * 0.45f, -handleLength * 0.1f };
        foreach (float bandZ in bandPositions)
        {
            var metalBand = new MeshInstance3D();
            metalBand.Mesh = new CylinderMesh
            {
                TopRadius = handleRadius * 1.22f,
                BottomRadius = handleRadius * 1.22f,
                Height = 0.012f * scale,
                RadialSegments = 12
            };
            metalBand.MaterialOverride = darkMetalMat;
            metalBand.Position = new Vector3(0, 0, bandZ);
            metalBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(metalBand);
        }

        // === COLLAR ===
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.85f,
            BottomRadius = handleRadius * 1.5f,
            Height = collarHeight,
            RadialSegments = 12
        };
        collar.MaterialOverride = headMat;
        collar.Position = new Vector3(0, 0, collarHeight / 2f);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Collar decorative ring
        var collarRing = new MeshInstance3D();
        collarRing.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.95f,
            BottomRadius = handleRadius * 1.95f,
            Height = 0.008f * scale,
            RadialSegments = 12
        };
        collarRing.MaterialOverride = darkMetalMat;
        collarRing.Position = new Vector3(0, 0, collarHeight);
        collarRing.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collarRing);

        // === SHAFT CORE THROUGH HEAD ===
        float headStartZ = collarHeight + 0.004f * scale;
        var shaftCore = new MeshInstance3D();
        shaftCore.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.45f,
            BottomRadius = handleRadius * 1.65f,
            Height = headHeight,
            RadialSegments = 12
        };
        shaftCore.MaterialOverride = headMat;
        shaftCore.Position = new Vector3(0, 0, headStartZ + headHeight / 2f);
        shaftCore.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(shaftCore);

        // === HAMMER HEAD (striking side) ===
        float headCenterZ = headStartZ + headHeight / 2f;
        var hammerFace = new MeshInstance3D();
        hammerFace.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth / 2f, headHeight * 0.92f, headHeight * 0.92f)
        };
        hammerFace.MaterialOverride = headMat;
        hammerFace.Position = new Vector3(headWidth / 4f + handleRadius, 0, headCenterZ);
        weapon.AddChild(hammerFace);

        // Striking face (polished)
        var strikeFace = new MeshInstance3D();
        strikeFace.Mesh = new BoxMesh
        {
            Size = new Vector3(0.018f * scale, headHeight * 0.96f, headHeight * 0.96f)
        };
        strikeFace.MaterialOverride = brightMetalMat;
        strikeFace.Position = new Vector3(headWidth / 2f + handleRadius, 0, headCenterZ);
        weapon.AddChild(strikeFace);

        // Face edge bevel
        var faceBevel = new MeshInstance3D();
        faceBevel.Mesh = new BoxMesh
        {
            Size = new Vector3(0.01f * scale, headHeight * 0.85f, headHeight * 0.85f)
        };
        faceBevel.MaterialOverride = headMat;
        faceBevel.Position = new Vector3(headWidth / 2f + handleRadius + 0.008f * scale, 0, headCenterZ);
        weapon.AddChild(faceBevel);

        // === DETAILED SPIKE ON BACK ===
        // Spike base (flared attachment)
        var spikeBase = new MeshInstance3D();
        spikeBase.Mesh = new CylinderMesh
        {
            TopRadius = 0.028f * scale,
            BottomRadius = 0.035f * scale,
            Height = 0.025f * scale,
            RadialSegments = 8
        };
        spikeBase.MaterialOverride = headMat;
        spikeBase.Position = new Vector3(-handleRadius - 0.012f * scale, 0, headCenterZ);
        spikeBase.RotationDegrees = new Vector3(0, 0, -90);
        weapon.AddChild(spikeBase);

        // Main spike body
        var spike = new MeshInstance3D();
        spike.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = 0.026f * scale,
            Height = 0.11f * scale,
            RadialSegments = 8
        };
        spike.MaterialOverride = headMat;
        spike.Position = new Vector3(-handleRadius - 0.07f * scale, 0, headCenterZ);
        spike.RotationDegrees = new Vector3(0, 0, -90);
        weapon.AddChild(spike);

        // Spike ridges (4 reinforcing edges)
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            var spikeRidge = new MeshInstance3D();
            spikeRidge.Mesh = new BoxMesh
            {
                Size = new Vector3(0.085f * scale, 0.005f * scale, 0.008f * scale)
            };
            spikeRidge.MaterialOverride = headMat;
            float offsetY = Mathf.Sin(Mathf.DegToRad(angle)) * 0.015f * scale;
            float offsetZ = Mathf.Cos(Mathf.DegToRad(angle)) * 0.015f * scale;
            spikeRidge.Position = new Vector3(-handleRadius - 0.06f * scale, offsetY, headCenterZ + offsetZ);
            spikeRidge.RotationDegrees = new Vector3(0, angle, 0);
            weapon.AddChild(spikeRidge);
        }

        // === TOP LANGET (protective strip on shaft) ===
        var langet = new MeshInstance3D();
        langet.Mesh = new BoxMesh
        {
            Size = new Vector3(0.012f * scale, 0.015f * scale, headHeight + 0.04f * scale)
        };
        langet.MaterialOverride = darkMetalMat;
        langet.Position = new Vector3(0, handleRadius * 1.3f, headCenterZ - 0.02f * scale);
        weapon.AddChild(langet);

        return weapon;
    }

    /// <summary>
    /// Create a staff - magical wooden staff with multi-faceted gem, spiral wood grain,
    /// rune engravings on shaft, and leather grip section.
    /// </summary>
    public static Node3D CreateStaff(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Staff";

        var woodMat = handleMaterial ?? CreateWoodMaterial(new Color(0.35f, 0.22f, 0.12f));
        var darkWoodMat = CreateWoodMaterial(new Color(0.25f, 0.15f, 0.08f)); // For spiral grain
        var leatherMat = CreateLeatherMaterial(new Color(0.28f, 0.16f, 0.08f));
        var metalMat = CreateMetalMaterial(new Color(0.55f, 0.50f, 0.45f)); // For metal accents
        var runeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.4f, 0.8f, 0.8f),
            Emission = new Color(0.15f, 0.3f, 0.6f),
            EmissionEnergyMultiplier = 0.8f
        };
        var crystalMat = bladeMaterial ?? new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.6f, 1f, 0.9f),
            Emission = new Color(0.3f, 0.5f, 1f),
            EmissionEnergyMultiplier = 1.5f
        };
        var crystalInnerMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.8f, 1f, 0.95f),
            Emission = new Color(0.5f, 0.7f, 1f),
            EmissionEnergyMultiplier = 2.0f
        };

        float shaftLength = 0.9f * scale;
        float shaftRadius = 0.02f * scale;
        float crystalSize = 0.04f * scale;

        // Grip is 25% from back end
        float gripOffset = shaftLength * 0.25f;
        float shaftCenterZ = -shaftLength / 2f + gripOffset;
        float shaftFrontZ = shaftCenterZ + shaftLength / 2f;
        float shaftBackZ = shaftCenterZ - shaftLength / 2f;

        // === WOODEN SHAFT ===
        var shaft = new MeshInstance3D();
        shaft.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius,
            BottomRadius = shaftRadius,
            Height = shaftLength,
            RadialSegments = 10
        };
        shaft.MaterialOverride = woodMat;
        shaft.Position = new Vector3(0, 0, shaftCenterZ);
        shaft.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(shaft);

        // === SPIRAL WOOD GRAIN (helical bands) ===
        int spiralCount = 8;
        for (int i = 0; i < spiralCount; i++)
        {
            float t = i / (float)(spiralCount - 1);
            float z = shaftBackZ + 0.05f * scale + t * (shaftLength - 0.15f * scale);
            float angle = i * 45f; // Spiral rotation

            var spiralBand = new MeshInstance3D();
            spiralBand.Mesh = new BoxMesh
            {
                Size = new Vector3(shaftRadius * 0.4f, shaftRadius * 2.3f, 0.015f * scale)
            };
            spiralBand.MaterialOverride = darkWoodMat;
            spiralBand.Position = new Vector3(0, 0, z);
            spiralBand.RotationDegrees = new Vector3(0, 0, angle);
            weapon.AddChild(spiralBand);
        }

        // === LEATHER GRIP SECTION ===
        float gripStart = -0.15f * scale;
        float gripEnd = 0.10f * scale;
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float z = gripStart + t * (gripEnd - gripStart);

            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = shaftRadius * 1.15f,
                BottomRadius = shaftRadius * 1.15f,
                Height = 0.018f * scale,
                RadialSegments = 10
            };
            gripBand.MaterialOverride = leatherMat;
            gripBand.Position = new Vector3(0, 0, z);
            gripBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(gripBand);
        }

        // === RUNE ENGRAVINGS (glowing symbols on shaft) ===
        // Upper rune band
        float runeZ1 = shaftFrontZ - 0.15f * scale;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            float rad = Mathf.DegToRad(angle);
            float x = Mathf.Cos(rad) * shaftRadius * 0.9f;
            float y = Mathf.Sin(rad) * shaftRadius * 0.9f;

            var rune = new MeshInstance3D();
            rune.Mesh = new BoxMesh
            {
                Size = new Vector3(0.006f * scale, 0.006f * scale, 0.025f * scale)
            };
            rune.MaterialOverride = runeMat;
            rune.Position = new Vector3(x, y, runeZ1);
            rune.RotationDegrees = new Vector3(0, 0, angle);
            weapon.AddChild(rune);
        }

        // Lower rune band
        float runeZ2 = shaftBackZ + 0.12f * scale;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f + 45f; // Offset from upper
            float rad = Mathf.DegToRad(angle);
            float x = Mathf.Cos(rad) * shaftRadius * 0.9f;
            float y = Mathf.Sin(rad) * shaftRadius * 0.9f;

            var rune = new MeshInstance3D();
            rune.Mesh = new BoxMesh
            {
                Size = new Vector3(0.005f * scale, 0.005f * scale, 0.02f * scale)
            };
            rune.MaterialOverride = runeMat;
            rune.Position = new Vector3(x, y, runeZ2);
            rune.RotationDegrees = new Vector3(0, 0, angle);
            weapon.AddChild(rune);
        }

        // === METAL FERRULES (rings at key points) ===
        // Top ferrule (below head)
        var topFerrule = new MeshInstance3D();
        topFerrule.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.3f,
            BottomRadius = shaftRadius * 1.2f,
            Height = 0.015f * scale,
            RadialSegments = 10
        };
        topFerrule.MaterialOverride = metalMat;
        topFerrule.Position = new Vector3(0, 0, shaftFrontZ - 0.008f * scale);
        topFerrule.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(topFerrule);

        // Bottom ferrule
        var bottomFerrule = new MeshInstance3D();
        bottomFerrule.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.2f,
            BottomRadius = shaftRadius * 1.3f,
            Height = 0.02f * scale,
            RadialSegments = 10
        };
        bottomFerrule.MaterialOverride = metalMat;
        bottomFerrule.Position = new Vector3(0, 0, shaftBackZ + 0.01f * scale);
        bottomFerrule.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(bottomFerrule);

        // === STAFF HEAD - FLARED TOP THAT HOLDS CRYSTAL ===
        float headHeight = 0.05f * scale;
        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 2.0f,
            BottomRadius = shaftRadius * 1.1f,
            Height = headHeight,
            RadialSegments = 10
        };
        head.MaterialOverride = woodMat;
        head.Position = new Vector3(0, 0, shaftFrontZ + headHeight / 2f);
        head.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(head);

        // Head prongs (4 arms that curve up to hold crystal)
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f + 45f;
            float rad = Mathf.DegToRad(angle);
            float x = Mathf.Cos(rad) * shaftRadius * 1.5f;
            float y = Mathf.Sin(rad) * shaftRadius * 1.5f;

            var prong = new MeshInstance3D();
            prong.Mesh = new CylinderMesh
            {
                TopRadius = 0.004f * scale,
                BottomRadius = 0.008f * scale,
                Height = 0.035f * scale,
                RadialSegments = 6
            };
            prong.MaterialOverride = woodMat;
            prong.Position = new Vector3(x * 0.7f, y * 0.7f, shaftFrontZ + headHeight + 0.015f * scale);
            prong.RotationDegrees = new Vector3(angle * 0.15f, 0, 0);
            weapon.AddChild(prong);
        }

        // === MULTI-FACETED GEM (using multiple boxes for facets) ===
        float gemZ = shaftFrontZ + headHeight + crystalSize * 0.6f;

        // Main crystal body (rotated box)
        var crystal = new MeshInstance3D();
        crystal.Mesh = new BoxMesh
        {
            Size = new Vector3(crystalSize, crystalSize, crystalSize)
        };
        crystal.MaterialOverride = crystalMat;
        crystal.Position = new Vector3(0, 0, gemZ);
        crystal.RotationDegrees = new Vector3(45, 45, 0);
        weapon.AddChild(crystal);

        // Inner glow core
        var crystalCore = new MeshInstance3D();
        crystalCore.Mesh = new BoxMesh
        {
            Size = new Vector3(crystalSize * 0.5f, crystalSize * 0.5f, crystalSize * 0.5f)
        };
        crystalCore.MaterialOverride = crystalInnerMat;
        crystalCore.Position = new Vector3(0, 0, gemZ);
        crystalCore.RotationDegrees = new Vector3(45, 45, 0);
        weapon.AddChild(crystalCore);

        // Crystal facets (additional rotated boxes for multi-faceted look)
        var facet1 = new MeshInstance3D();
        facet1.Mesh = new BoxMesh
        {
            Size = new Vector3(crystalSize * 0.8f, crystalSize * 1.1f, crystalSize * 0.8f)
        };
        facet1.MaterialOverride = crystalMat;
        facet1.Position = new Vector3(0, 0, gemZ);
        facet1.RotationDegrees = new Vector3(45, 0, 45);
        weapon.AddChild(facet1);

        var facet2 = new MeshInstance3D();
        facet2.Mesh = new BoxMesh
        {
            Size = new Vector3(crystalSize * 0.7f, crystalSize * 0.7f, crystalSize * 1.15f)
        };
        facet2.MaterialOverride = crystalMat;
        facet2.Position = new Vector3(0, 0, gemZ);
        facet2.RotationDegrees = new Vector3(0, 45, 45);
        weapon.AddChild(facet2);

        // Crystal top point
        var crystalTip = new MeshInstance3D();
        crystalTip.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = crystalSize * 0.4f,
            Height = crystalSize * 0.5f,
            RadialSegments = 4
        };
        crystalTip.MaterialOverride = crystalMat;
        crystalTip.Position = new Vector3(0, 0, gemZ + crystalSize * 0.6f);
        crystalTip.RotationDegrees = new Vector3(90, 0, 45);
        weapon.AddChild(crystalTip);

        return weapon;
    }

    /// <summary>
    /// Create a bow - ranged weapon with leather grip wrapping, arrow rest notch,
    /// limb tip reinforcements, and decorative string nocks.
    /// </summary>
    public static Node3D CreateBow(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Bow";

        var woodMat = handleMaterial ?? CreateWoodMaterial(new Color(0.45f, 0.28f, 0.15f));
        var leatherMat = CreateLeatherMaterial(new Color(0.32f, 0.20f, 0.10f));
        var metalMat = CreateMetalMaterial(new Color(0.50f, 0.48f, 0.45f)); // For reinforcements
        var boneMat = CreateMetalMaterial(new Color(0.85f, 0.82f, 0.75f)); // For string nocks (ivory colored)
        boneMat.Metallic = 0.1f;
        boneMat.Roughness = 0.6f;
        var stringMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.85f, 0.7f),
            Roughness = 0.9f
        };

        float bowHeight = 0.7f * scale;
        float bowThickness = 0.02f * scale;
        float limbLength = bowHeight * 0.42f;
        float curveAngle = 20f;  // Degrees each limb curves back

        // === GRIP CORE (center handle) ===
        float gripHeight = 0.10f * scale;
        var gripCore = new MeshInstance3D();
        gripCore.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 1.1f,
            BottomRadius = bowThickness * 1.1f,
            Height = gripHeight,
            RadialSegments = 8
        };
        gripCore.MaterialOverride = woodMat;
        gripCore.Position = Vector3.Zero;
        weapon.AddChild(gripCore);

        // === LEATHER GRIP WRAPPING (spiral bands) ===
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float y = -gripHeight / 2f + 0.01f * scale + t * (gripHeight - 0.02f * scale);

            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = bowThickness * 1.25f,
                BottomRadius = bowThickness * 1.25f,
                Height = 0.012f * scale,
                RadialSegments = 8
            };
            gripBand.MaterialOverride = leatherMat;
            gripBand.Position = new Vector3(0, y, 0);
            weapon.AddChild(gripBand);
        }

        // === ARROW REST NOTCH (small ledge on the side of grip) ===
        var arrowRest = new MeshInstance3D();
        arrowRest.Mesh = new BoxMesh
        {
            Size = new Vector3(0.012f * scale, 0.015f * scale, 0.02f * scale)
        };
        arrowRest.MaterialOverride = leatherMat;
        arrowRest.Position = new Vector3(bowThickness * 1.3f, gripHeight * 0.15f, 0.01f * scale);
        weapon.AddChild(arrowRest);

        // Arrow rest pad (where arrow sits)
        var arrowRestPad = new MeshInstance3D();
        arrowRestPad.Mesh = new BoxMesh
        {
            Size = new Vector3(0.015f * scale, 0.006f * scale, 0.015f * scale)
        };
        arrowRestPad.MaterialOverride = leatherMat;
        arrowRestPad.Position = new Vector3(bowThickness * 1.35f, gripHeight * 0.15f + 0.01f * scale, 0.01f * scale);
        weapon.AddChild(arrowRestPad);

        // === UPPER LIMB ===
        var upperLimb = new MeshInstance3D();
        upperLimb.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 0.6f,
            BottomRadius = bowThickness,
            Height = limbLength,
            RadialSegments = 8
        };
        upperLimb.MaterialOverride = woodMat;
        upperLimb.Position = new Vector3(0, gripHeight / 2f + limbLength / 2f * Mathf.Cos(Mathf.DegToRad(curveAngle)),
                                          -limbLength / 2f * Mathf.Sin(Mathf.DegToRad(curveAngle)));
        upperLimb.RotationDegrees = new Vector3(-curveAngle, 0, 0);
        weapon.AddChild(upperLimb);

        // === LOWER LIMB ===
        var lowerLimb = new MeshInstance3D();
        lowerLimb.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness,
            BottomRadius = bowThickness * 0.6f,
            Height = limbLength,
            RadialSegments = 8
        };
        lowerLimb.MaterialOverride = woodMat;
        lowerLimb.Position = new Vector3(0, -gripHeight / 2f - limbLength / 2f * Mathf.Cos(Mathf.DegToRad(curveAngle)),
                                          -limbLength / 2f * Mathf.Sin(Mathf.DegToRad(curveAngle)));
        lowerLimb.RotationDegrees = new Vector3(curveAngle, 0, 0);
        weapon.AddChild(lowerLimb);

        // Calculate string endpoints (tips of the limbs)
        float limbEndY = gripHeight / 2f + limbLength * Mathf.Cos(Mathf.DegToRad(curveAngle));
        float limbEndZ = -limbLength * Mathf.Sin(Mathf.DegToRad(curveAngle));

        // === LIMB TIP REINFORCEMENTS (metal caps) ===
        // Upper limb tip reinforcement
        var upperTipReinforcement = new MeshInstance3D();
        upperTipReinforcement.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 0.5f,
            BottomRadius = bowThickness * 0.7f,
            Height = 0.02f * scale,
            RadialSegments = 8
        };
        upperTipReinforcement.MaterialOverride = metalMat;
        upperTipReinforcement.Position = new Vector3(0, limbEndY - 0.005f * scale, limbEndZ);
        upperTipReinforcement.RotationDegrees = new Vector3(-curveAngle, 0, 0);
        weapon.AddChild(upperTipReinforcement);

        // Lower limb tip reinforcement
        var lowerTipReinforcement = new MeshInstance3D();
        lowerTipReinforcement.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 0.7f,
            BottomRadius = bowThickness * 0.5f,
            Height = 0.02f * scale,
            RadialSegments = 8
        };
        lowerTipReinforcement.MaterialOverride = metalMat;
        lowerTipReinforcement.Position = new Vector3(0, -limbEndY + 0.005f * scale, limbEndZ);
        lowerTipReinforcement.RotationDegrees = new Vector3(curveAngle, 0, 0);
        weapon.AddChild(lowerTipReinforcement);

        // === STRING NOCKS (decorative bone/horn pieces where string attaches) ===
        // Upper nock
        var upperNock = new MeshInstance3D();
        upperNock.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = bowThickness * 0.55f,
            Height = 0.018f * scale,
            RadialSegments = 6
        };
        upperNock.MaterialOverride = boneMat;
        upperNock.Position = new Vector3(0, limbEndY + 0.01f * scale, limbEndZ);
        upperNock.RotationDegrees = new Vector3(-curveAngle, 0, 0);
        weapon.AddChild(upperNock);

        // Upper nock groove (for string to sit in)
        var upperNockGroove = new MeshInstance3D();
        upperNockGroove.Mesh = new BoxMesh
        {
            Size = new Vector3(0.004f * scale, 0.008f * scale, bowThickness * 0.8f)
        };
        upperNockGroove.MaterialOverride = boneMat;
        upperNockGroove.Position = new Vector3(0, limbEndY + 0.015f * scale, limbEndZ - 0.002f * scale);
        upperNockGroove.RotationDegrees = new Vector3(-curveAngle, 0, 0);
        weapon.AddChild(upperNockGroove);

        // Lower nock
        var lowerNock = new MeshInstance3D();
        lowerNock.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 0.55f,
            BottomRadius = 0.003f * scale,
            Height = 0.018f * scale,
            RadialSegments = 6
        };
        lowerNock.MaterialOverride = boneMat;
        lowerNock.Position = new Vector3(0, -limbEndY - 0.01f * scale, limbEndZ);
        lowerNock.RotationDegrees = new Vector3(curveAngle, 0, 0);
        weapon.AddChild(lowerNock);

        // Lower nock groove
        var lowerNockGroove = new MeshInstance3D();
        lowerNockGroove.Mesh = new BoxMesh
        {
            Size = new Vector3(0.004f * scale, 0.008f * scale, bowThickness * 0.8f)
        };
        lowerNockGroove.MaterialOverride = boneMat;
        lowerNockGroove.Position = new Vector3(0, -limbEndY - 0.015f * scale, limbEndZ - 0.002f * scale);
        lowerNockGroove.RotationDegrees = new Vector3(curveAngle, 0, 0);
        weapon.AddChild(lowerNockGroove);

        // === LIMB DECORATIVE BANDS (where limb meets grip) ===
        // Upper band
        var upperBand = new MeshInstance3D();
        upperBand.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 1.15f,
            BottomRadius = bowThickness * 1.15f,
            Height = 0.012f * scale,
            RadialSegments = 8
        };
        upperBand.MaterialOverride = metalMat;
        upperBand.Position = new Vector3(0, gripHeight / 2f + 0.015f * scale, -0.005f * scale);
        upperBand.RotationDegrees = new Vector3(-curveAngle * 0.5f, 0, 0);
        weapon.AddChild(upperBand);

        // Lower band
        var lowerBand = new MeshInstance3D();
        lowerBand.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 1.15f,
            BottomRadius = bowThickness * 1.15f,
            Height = 0.012f * scale,
            RadialSegments = 8
        };
        lowerBand.MaterialOverride = metalMat;
        lowerBand.Position = new Vector3(0, -gripHeight / 2f - 0.015f * scale, -0.005f * scale);
        lowerBand.RotationDegrees = new Vector3(curveAngle * 0.5f, 0, 0);
        weapon.AddChild(lowerBand);

        // === BOWSTRING ===
        float stringLength = limbEndY * 2f;
        var bowstring = new MeshInstance3D();
        bowstring.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = 0.003f * scale,
            Height = stringLength,
            RadialSegments = 4
        };
        bowstring.MaterialOverride = stringMat;
        bowstring.Position = new Vector3(0, 0, limbEndZ);
        weapon.AddChild(bowstring);

        // String serving (thicker wrapped section at center for arrow nocking)
        var stringServing = new MeshInstance3D();
        stringServing.Mesh = new CylinderMesh
        {
            TopRadius = 0.005f * scale,
            BottomRadius = 0.005f * scale,
            Height = 0.04f * scale,
            RadialSegments = 6
        };
        stringServing.MaterialOverride = stringMat;
        stringServing.Position = new Vector3(0, 0, limbEndZ);
        weapon.AddChild(stringServing);

        return weapon;
    }

    /// <summary>
    /// Create a club - simple blunt weapon with leather grip, wood knots, metal studs, and reinforcement rings.
    /// </summary>
    public static Node3D CreateClub(float scale = 1f, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Club";

        var woodMat = handleMaterial ?? CreateWoodMaterial(new Color(0.38f, 0.25f, 0.15f));
        var darkWoodMat = CreateWoodMaterial(new Color(0.28f, 0.18f, 0.10f)); // For knots
        var leatherMat = CreateLeatherMaterial(new Color(0.30f, 0.18f, 0.08f));
        var metalMat = CreateMetalMaterial(new Color(0.35f, 0.33f, 0.30f)); // Dull iron for studs/rings

        float length = 0.40f * scale;
        float handleRadius = 0.022f * scale;
        float headRadius = 0.045f * scale;

        // Single tapered club body (thin at grip, thick at head)
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh
        {
            TopRadius = headRadius,       // Front (toward enemy)
            BottomRadius = handleRadius,  // Back (grip end)
            Height = length,
            RadialSegments = 10
        };
        body.MaterialOverride = woodMat;
        body.Position = new Vector3(0, 0, length / 2f);
        body.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(body);

        // Rounded head cap - use a cylinder cap instead of sphere
        var headCap = new MeshInstance3D();
        headCap.Mesh = new CylinderMesh
        {
            TopRadius = headRadius * 0.3f,
            BottomRadius = headRadius,
            Height = headRadius * 0.6f,
            RadialSegments = 10
        };
        headCap.MaterialOverride = woodMat;
        headCap.Position = new Vector3(0, 0, length + headRadius * 0.3f);
        headCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(headCap);

        // === LEATHER GRIP WRAPPING (3 bands) ===
        float gripStart = -0.02f * scale;
        float gripLength = 0.12f * scale;
        for (int i = 0; i < 3; i++)
        {
            float t = i / 2f; // 0, 0.5, 1
            float z = gripStart + t * gripLength;
            float bandRadius = Mathf.Lerp(handleRadius * 0.95f, handleRadius * 1.0f, t * 0.3f);

            var gripBand = new MeshInstance3D();
            gripBand.Mesh = new CylinderMesh
            {
                TopRadius = bandRadius * 1.15f,
                BottomRadius = bandRadius * 1.15f,
                Height = 0.018f * scale,
                RadialSegments = 10
            };
            gripBand.MaterialOverride = leatherMat;
            gripBand.Position = new Vector3(0, 0, z);
            gripBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(gripBand);
        }

        // === WOOD KNOTS/GNARLS (bumps on the surface) ===
        // Knot 1 - near head
        var knot1 = new MeshInstance3D();
        float knot1Radius = 0.012f * scale;
        knot1.Mesh = new CylinderMesh
        {
            TopRadius = knot1Radius * 0.5f,
            BottomRadius = knot1Radius,
            Height = knot1Radius,
            RadialSegments = 6
        };
        knot1.MaterialOverride = darkWoodMat;
        knot1.Position = new Vector3(headRadius * 0.75f, 0.01f * scale, length * 0.7f);
        knot1.RotationDegrees = new Vector3(0, 0, -75);
        weapon.AddChild(knot1);

        // Knot 2 - middle section
        var knot2 = new MeshInstance3D();
        float knot2Radius = 0.010f * scale;
        knot2.Mesh = new CylinderMesh
        {
            TopRadius = knot2Radius * 0.4f,
            BottomRadius = knot2Radius,
            Height = knot2Radius * 0.8f,
            RadialSegments = 6
        };
        knot2.MaterialOverride = darkWoodMat;
        knot2.Position = new Vector3(-handleRadius * 0.6f, handleRadius * 0.5f, length * 0.45f);
        knot2.RotationDegrees = new Vector3(-30, 0, 120);
        weapon.AddChild(knot2);

        // Knot 3 - near head on other side
        var knot3 = new MeshInstance3D();
        float knot3Radius = 0.008f * scale;
        knot3.Mesh = new CylinderMesh
        {
            TopRadius = knot3Radius * 0.3f,
            BottomRadius = knot3Radius,
            Height = knot3Radius * 0.7f,
            RadialSegments = 6
        };
        knot3.MaterialOverride = darkWoodMat;
        knot3.Position = new Vector3(0, -headRadius * 0.7f, length * 0.8f);
        knot3.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(knot3);

        // === METAL STUDS/NAILS (around the head) ===
        float studZ = length * 0.75f;
        float studRadius = Mathf.Lerp(handleRadius, headRadius, 0.65f); // Radius at this Z position
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f;
            float x = Mathf.Cos(Mathf.DegToRad(angle)) * studRadius;
            float y = Mathf.Sin(Mathf.DegToRad(angle)) * studRadius;

            var stud = new MeshInstance3D();
            float studSize = 0.008f * scale;
            stud.Mesh = new CylinderMesh
            {
                TopRadius = studSize * 0.3f,
                BottomRadius = studSize,
                Height = studSize * 1.5f,
                RadialSegments = 6
            };
            stud.MaterialOverride = metalMat;
            stud.Position = new Vector3(x, y, studZ);
            // Rotate to point outward from center
            stud.RotationDegrees = new Vector3(0, 0, angle - 90);
            weapon.AddChild(stud);
        }

        // === REINFORCEMENT RINGS (metal bands) ===
        // Ring 1 - between grip and mid-section
        float ring1Z = 0.12f * scale;
        float ring1Radius = Mathf.Lerp(handleRadius, headRadius, 0.25f);
        var ring1 = new MeshInstance3D();
        ring1.Mesh = new CylinderMesh
        {
            TopRadius = ring1Radius * 1.2f,
            BottomRadius = ring1Radius * 1.2f,
            Height = 0.012f * scale,
            RadialSegments = 12
        };
        ring1.MaterialOverride = metalMat;
        ring1.Position = new Vector3(0, 0, ring1Z);
        ring1.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(ring1);

        // Ring 2 - near the head
        float ring2Z = length * 0.88f;
        float ring2Radius = Mathf.Lerp(handleRadius, headRadius, 0.85f);
        var ring2 = new MeshInstance3D();
        ring2.Mesh = new CylinderMesh
        {
            TopRadius = ring2Radius * 1.1f,
            BottomRadius = ring2Radius * 1.1f,
            Height = 0.015f * scale,
            RadialSegments = 12
        };
        ring2.MaterialOverride = metalMat;
        ring2.Position = new Vector3(0, 0, ring2Z);
        ring2.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(ring2);

        // === POMMEL CAP (metal end cap) ===
        var pommelCap = new MeshInstance3D();
        pommelCap.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.1f,
            Height = 0.015f * scale,
            RadialSegments = 10
        };
        pommelCap.MaterialOverride = metalMat;
        pommelCap.Position = new Vector3(0, 0, -0.008f * scale);
        pommelCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommelCap);

        return weapon;
    }

    /// <summary>
    /// Create a scythe - two-handed reaping weapon with handle wrapping,
    /// blade edge detail, and decorative collar. (No PrismMesh - converted to Box/Cylinder)
    /// </summary>
    public static Node3D CreateScythe(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Scythe";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.5f, 0.5f, 0.55f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();
        var leatherMat = CreateLeatherMaterial(new Color(0.30f, 0.18f, 0.10f));
        var darkMetalMat = CreateMetalMaterial(new Color(0.38f, 0.36f, 0.34f));

        float shaftLength = 1.1f * scale;
        float shaftRadius = 0.022f * scale;
        float bladeLength = 0.35f * scale;
        float bladeWidth = 0.08f * scale;
        float collarHeight = 0.045f * scale;
        float gripOffset = 0.3f * scale;

        // Calculate shaft position so grip is at origin
        float shaftCenterZ = -shaftLength / 2f + gripOffset;
        float shaftFrontZ = shaftCenterZ + shaftLength / 2f;
        float shaftBackZ = shaftCenterZ - shaftLength / 2f;

        // === LONG WOODEN SHAFT ===
        var shaft = new MeshInstance3D();
        shaft.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius,
            BottomRadius = shaftRadius * 1.1f,
            Height = shaftLength,
            RadialSegments = 10
        };
        shaft.MaterialOverride = handleMat;
        shaft.Position = new Vector3(0, 0, shaftCenterZ);
        shaft.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(shaft);

        // === HANDLE WRAPPING (leather bands at grip area) ===
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float z = -0.12f * scale + t * 0.24f * scale;

            var wrapBand = new MeshInstance3D();
            wrapBand.Mesh = new CylinderMesh
            {
                TopRadius = shaftRadius * 1.15f,
                BottomRadius = shaftRadius * 1.15f,
                Height = 0.018f * scale,
                RadialSegments = 10
            };
            wrapBand.MaterialOverride = leatherMat;
            wrapBand.Position = new Vector3(0, 0, z);
            wrapBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(wrapBand);
        }

        // === SECONDARY GRIP (higher on shaft for two-handed use) ===
        float secondGripZ = shaftFrontZ - 0.25f * scale;
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float z = secondGripZ + t * 0.12f * scale;

            var secondBand = new MeshInstance3D();
            secondBand.Mesh = new CylinderMesh
            {
                TopRadius = shaftRadius * 1.12f,
                BottomRadius = shaftRadius * 1.12f,
                Height = 0.015f * scale,
                RadialSegments = 10
            };
            secondBand.MaterialOverride = leatherMat;
            secondBand.Position = new Vector3(0, 0, z);
            secondBand.RotationDegrees = new Vector3(90, 0, 0);
            weapon.AddChild(secondBand);
        }

        // === BUTT CAP ===
        var buttCap = new MeshInstance3D();
        buttCap.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.2f,
            BottomRadius = shaftRadius * 1.0f,
            Height = 0.02f * scale,
            RadialSegments = 10
        };
        buttCap.MaterialOverride = darkMetalMat;
        buttCap.Position = new Vector3(0, 0, shaftBackZ + 0.01f * scale);
        buttCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttCap);

        // === DECORATIVE COLLAR (multi-part) ===
        // Lower collar band
        var lowerCollarBand = new MeshInstance3D();
        lowerCollarBand.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.4f,
            BottomRadius = shaftRadius * 1.2f,
            Height = 0.015f * scale,
            RadialSegments = 10
        };
        lowerCollarBand.MaterialOverride = darkMetalMat;
        lowerCollarBand.Position = new Vector3(0, 0, shaftFrontZ - 0.005f * scale);
        lowerCollarBand.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(lowerCollarBand);

        // Main collar
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.7f,
            BottomRadius = shaftRadius * 1.5f,
            Height = collarHeight,
            RadialSegments = 10
        };
        collar.MaterialOverride = bladeMat;
        collar.Position = new Vector3(0, 0, shaftFrontZ + collarHeight / 2f + 0.005f * scale);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Collar decorative ring
        var collarRing = new MeshInstance3D();
        collarRing.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.8f,
            BottomRadius = shaftRadius * 1.8f,
            Height = 0.008f * scale,
            RadialSegments = 10
        };
        collarRing.MaterialOverride = darkMetalMat;
        collarRing.Position = new Vector3(0, 0, shaftFrontZ + collarHeight + 0.009f * scale);
        collarRing.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collarRing);

        // === BLADE ATTACHMENT TANG ===
        float tangLength = 0.065f * scale;
        var tang = new MeshInstance3D();
        tang.Mesh = new BoxMesh
        {
            Size = new Vector3(0.028f * scale, 0.016f * scale, tangLength)
        };
        tang.MaterialOverride = bladeMat;
        tang.Position = new Vector3(0.012f * scale, 0, shaftFrontZ + collarHeight + 0.015f * scale + tangLength / 2f);
        tang.RotationDegrees = new Vector3(0, -15, 10);
        weapon.AddChild(tang);

        // === MAIN SCYTHE BLADE (curved, extends to the side) ===
        float bladeStartZ = shaftFrontZ + collarHeight + 0.015f * scale + tangLength;
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeLength, 0.009f * scale, bladeWidth)
        };
        blade.MaterialOverride = bladeMat;
        blade.Position = new Vector3(bladeLength / 2f + 0.025f * scale, -0.012f * scale, bladeStartZ + bladeWidth / 2f);
        blade.RotationDegrees = new Vector3(0, -25, 12);
        weapon.AddChild(blade);

        // === BLADE EDGE (using tapered boxes instead of PrismMesh) ===
        // Multiple overlapping boxes to create edge taper
        for (int i = 0; i < 3; i++)
        {
            float t = i / 2f;
            float edgeHeight = Mathf.Lerp(0.010f, 0.004f, t) * scale;
            float edgeDepth = Mathf.Lerp(0.025f, 0.010f, t) * scale;

            var edge = new MeshInstance3D();
            edge.Mesh = new BoxMesh
            {
                Size = new Vector3(bladeLength * 0.9f, edgeHeight, edgeDepth)
            };
            edge.MaterialOverride = bladeMat;
            edge.Position = new Vector3(
                bladeLength / 2f + 0.025f * scale,
                -0.018f * scale - t * 0.006f * scale,
                bladeStartZ + bladeWidth + 0.008f * scale + t * 0.008f * scale
            );
            edge.RotationDegrees = new Vector3(0, -25, 12);
            weapon.AddChild(edge);
        }

        // === BLADE SPINE (raised ridge along back of blade) ===
        var spine = new MeshInstance3D();
        spine.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeLength * 0.85f, 0.012f * scale, 0.01f * scale)
        };
        spine.MaterialOverride = bladeMat;
        spine.Position = new Vector3(bladeLength / 2f + 0.03f * scale, -0.003f * scale, bladeStartZ + bladeWidth * 0.1f);
        spine.RotationDegrees = new Vector3(0, -25, 12);
        weapon.AddChild(spine);

        // === BLADE TIP POINT (using tapered cylinder instead of PrismMesh) ===
        var tip = new MeshInstance3D();
        tip.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = 0.02f * scale,
            Height = 0.06f * scale,
            RadialSegments = 4
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(bladeLength + 0.05f * scale, -0.018f * scale, bladeStartZ + bladeWidth * 0.65f);
        tip.RotationDegrees = new Vector3(30, -60, 90);
        weapon.AddChild(tip);

        // === BLADE HEEL (where blade meets tang) ===
        var heel = new MeshInstance3D();
        heel.Mesh = new BoxMesh
        {
            Size = new Vector3(0.04f * scale, 0.012f * scale, 0.03f * scale)
        };
        heel.MaterialOverride = bladeMat;
        heel.Position = new Vector3(0.03f * scale, -0.01f * scale, bladeStartZ + 0.01f * scale);
        heel.RotationDegrees = new Vector3(0, -20, 10);
        weapon.AddChild(heel);

        return weapon;
    }
}
