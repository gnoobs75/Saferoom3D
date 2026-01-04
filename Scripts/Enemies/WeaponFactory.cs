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
    /// Create a dagger - small, quick blade.
    /// </summary>
    public static Node3D CreateDagger(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Dagger";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.7f, 0.7f, 0.75f));
        var handleMat = handleMaterial ?? CreateLeatherMaterial();
        var guardMat = CreateMetalMaterial(new Color(0.5f, 0.45f, 0.4f));

        float handleLength = 0.10f * scale;
        float handleRadius = 0.016f * scale;
        float bladeLength = 0.18f * scale;
        float bladeWidth = 0.024f * scale;

        // Handle (extends backward in -Z)
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

        // Pommel (cylinder cap at back of handle)
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.5f,
            Height = 0.015f * scale,
            RadialSegments = 8
        };
        pommel.MaterialOverride = guardMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.007f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Guard (cross-piece at origin)
        var guard = new MeshInstance3D();
        guard.Mesh = new BoxMesh
        {
            Size = new Vector3(0.05f * scale, 0.015f * scale, 0.012f * scale)
        };
        guard.MaterialOverride = guardMat;
        guard.Position = new Vector3(0, 0, 0.005f * scale);
        weapon.AddChild(guard);

        // Blade (extends forward in +Z) - tapered box
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth, 0.006f * scale, bladeLength)
        };
        blade.MaterialOverride = bladeMat;
        blade.Position = new Vector3(0, 0, 0.01f * scale + bladeLength / 2f);
        weapon.AddChild(blade);

        // Blade tip (tapered cylinder instead of prism)
        var tip = new MeshInstance3D();
        tip.Mesh = new CylinderMesh
        {
            TopRadius = 0.001f * scale,
            BottomRadius = bladeWidth / 2f,
            Height = 0.04f * scale,
            RadialSegments = 4
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(0, 0, 0.01f * scale + bladeLength + 0.02f * scale);
        tip.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(tip);

        return weapon;
    }

    /// <summary>
    /// Create a short sword - balanced one-handed blade.
    /// </summary>
    public static Node3D CreateShortSword(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "ShortSword";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.75f, 0.75f, 0.8f));
        var handleMat = handleMaterial ?? CreateLeatherMaterial();
        var guardMat = CreateMetalMaterial(new Color(0.55f, 0.5f, 0.45f));

        float handleLength = 0.14f * scale;
        float handleRadius = 0.018f * scale;
        float bladeLength = 0.40f * scale;
        float bladeWidth = 0.038f * scale;

        // Handle
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

        // Pommel (cylinder cap)
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.6f,
            Height = 0.02f * scale,
            RadialSegments = 10
        };
        pommel.MaterialOverride = guardMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.01f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Guard (cross-piece)
        var guard = new MeshInstance3D();
        guard.Mesh = new BoxMesh
        {
            Size = new Vector3(0.10f * scale, 0.02f * scale, 0.015f * scale)
        };
        guard.MaterialOverride = guardMat;
        guard.Position = new Vector3(0, 0, 0.005f * scale);
        weapon.AddChild(guard);

        // Blade
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth, 0.008f * scale, bladeLength)
        };
        blade.MaterialOverride = bladeMat;
        blade.Position = new Vector3(0, 0, 0.015f * scale + bladeLength / 2f);
        weapon.AddChild(blade);

        // Blade tip (tapered cylinder)
        var tip = new MeshInstance3D();
        tip.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = bladeWidth / 2f,
            Height = 0.06f * scale,
            RadialSegments = 4
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(0, 0, 0.015f * scale + bladeLength + 0.03f * scale);
        tip.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(tip);

        return weapon;
    }

    /// <summary>
    /// Create a long sword - two-handed blade.
    /// </summary>
    public static Node3D CreateLongSword(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "LongSword";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.8f, 0.8f, 0.85f));
        var handleMat = handleMaterial ?? CreateLeatherMaterial();
        var guardMat = CreateMetalMaterial(new Color(0.6f, 0.55f, 0.5f));

        float handleLength = 0.25f * scale;
        float handleRadius = 0.02f * scale;
        float bladeLength = 0.65f * scale;
        float bladeWidth = 0.048f * scale;

        // Handle
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

        // Pommel (cylinder cap)
        var pommel = new MeshInstance3D();
        pommel.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.4f,
            BottomRadius = handleRadius * 1.8f,
            Height = 0.025f * scale,
            RadialSegments = 12
        };
        pommel.MaterialOverride = guardMat;
        pommel.Position = new Vector3(0, 0, -handleLength - 0.012f * scale);
        pommel.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(pommel);

        // Guard (longer cross-piece)
        var guard = new MeshInstance3D();
        guard.Mesh = new BoxMesh
        {
            Size = new Vector3(0.16f * scale, 0.025f * scale, 0.018f * scale)
        };
        guard.MaterialOverride = guardMat;
        guard.Position = new Vector3(0, 0, 0.008f * scale);
        weapon.AddChild(guard);

        // Blade
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeWidth, 0.01f * scale, bladeLength)
        };
        blade.MaterialOverride = bladeMat;
        blade.Position = new Vector3(0, 0, 0.02f * scale + bladeLength / 2f);
        weapon.AddChild(blade);

        // Blade tip (tapered cylinder)
        var tip = new MeshInstance3D();
        tip.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = bladeWidth / 2f,
            Height = 0.08f * scale,
            RadialSegments = 4
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(0, 0, 0.02f * scale + bladeLength + 0.04f * scale);
        tip.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(tip);

        // Fuller (blood groove) - thin darker line down blade
        var fuller = new MeshInstance3D();
        fuller.Mesh = new BoxMesh
        {
            Size = new Vector3(0.008f * scale, 0.012f * scale, bladeLength * 0.7f)
        };
        fuller.MaterialOverride = CreateMetalMaterial(new Color(0.5f, 0.5f, 0.55f));
        fuller.Position = new Vector3(0, 0, 0.02f * scale + bladeLength * 0.35f);
        weapon.AddChild(fuller);

        return weapon;
    }

    /// <summary>
    /// Create an axe - one-handed chopping weapon.
    /// </summary>
    public static Node3D CreateAxe(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Axe";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.6f, 0.6f, 0.65f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();

        float handleLength = 0.35f * scale;
        float handleRadius = 0.022f * scale;
        float headHeight = 0.12f * scale;   // Height of axe head along shaft
        float headWidth = 0.10f * scale;    // Width extending to the side
        float headThickness = 0.025f * scale;
        float collarHeight = 0.04f * scale;

        // Handle (wooden shaft) - extends back from grip
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

        // Handle butt cap - at very end
        var buttCap = new MeshInstance3D();
        buttCap.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.3f,
            BottomRadius = handleRadius * 1.1f,
            Height = 0.02f * scale,
            RadialSegments = 10
        };
        buttCap.MaterialOverride = CreateMetalMaterial(new Color(0.4f, 0.35f, 0.3f));
        buttCap.Position = new Vector3(0, 0, -handleLength - 0.01f * scale);
        buttCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttCap);

        // Axe head collar - wraps around shaft at top, connects handle to head
        // Position it so it overlaps the handle tip
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

        // Axe head - positioned so it starts at collar and extends forward
        // The head wraps around the shaft, extending to one side
        float headStartZ = collarZ + collarHeight / 2f;
        var head = new MeshInstance3D();
        head.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth, headThickness, headHeight)
        };
        head.MaterialOverride = bladeMat;
        // Center the head so it starts at collar and extends outward (+X)
        head.Position = new Vector3(headWidth / 2f + handleRadius * 0.5f, 0, headStartZ + headHeight / 2f);
        weapon.AddChild(head);

        // Blade edge (sharp part on outer edge)
        var edge = new MeshInstance3D();
        edge.Mesh = new PrismMesh
        {
            Size = new Vector3(0.02f * scale, headThickness * 1.2f, headHeight * 0.9f),
            LeftToRight = 0f
        };
        edge.MaterialOverride = bladeMat;
        edge.Position = new Vector3(headWidth + handleRadius * 0.5f, 0, headStartZ + headHeight / 2f);
        edge.RotationDegrees = new Vector3(0, 0, 90);
        weapon.AddChild(edge);

        // Inner connection piece to shaft
        var inner = new MeshInstance3D();
        inner.Mesh = new BoxMesh
        {
            Size = new Vector3(handleRadius * 2f, headThickness, headHeight * 0.8f)
        };
        inner.MaterialOverride = bladeMat;
        inner.Position = new Vector3(0, 0, headStartZ + headHeight / 2f);
        weapon.AddChild(inner);

        return weapon;
    }

    /// <summary>
    /// Create a battle axe - two-handed double-headed axe.
    /// </summary>
    public static Node3D CreateBattleAxe(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "BattleAxe";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.55f, 0.55f, 0.6f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();

        float handleLength = 0.5f * scale;
        float handleRadius = 0.025f * scale;
        float headHeight = 0.14f * scale;   // Height along shaft
        float headWidth = 0.08f * scale;    // Width to each side
        float headThickness = 0.02f * scale;

        // Long handle - extends back from grip
        var handle = new MeshInstance3D();
        handle.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius,
            BottomRadius = handleRadius * 1.15f,
            Height = handleLength,
            RadialSegments = 10
        };
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0, 0, -handleLength / 2f);
        handle.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(handle);

        // Butt cap - cylinder at end (not sphere)
        var buttCap = new MeshInstance3D();
        buttCap.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.2f,
            BottomRadius = handleRadius * 1.4f,
            Height = 0.02f * scale,
            RadialSegments = 10
        };
        buttCap.MaterialOverride = CreateMetalMaterial(new Color(0.4f, 0.35f, 0.3f));
        buttCap.Position = new Vector3(0, 0, -handleLength - 0.01f * scale);
        buttCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(buttCap);

        // Head collar - central piece connecting both blades to shaft
        float collarHeight = 0.04f * scale;
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.6f,
            BottomRadius = handleRadius * 1.6f,
            Height = collarHeight,
            RadialSegments = 10
        };
        collar.MaterialOverride = bladeMat;
        collar.Position = new Vector3(0, 0, collarHeight / 2f);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Central bar connecting both blade heads
        float headStartZ = collarHeight;
        var central = new MeshInstance3D();
        central.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth * 2f + handleRadius * 2f, headThickness, headHeight * 0.5f)
        };
        central.MaterialOverride = bladeMat;
        central.Position = new Vector3(0, 0, headStartZ + headHeight * 0.25f);
        weapon.AddChild(central);

        // Double-headed axe blades (one on each side in X)
        for (int side = -1; side <= 1; side += 2)
        {
            // Main blade body
            var head = new MeshInstance3D();
            head.Mesh = new BoxMesh
            {
                Size = new Vector3(headWidth, headThickness, headHeight)
            };
            head.MaterialOverride = bladeMat;
            head.Position = new Vector3(side * (headWidth / 2f + handleRadius * 0.6f), 0, headStartZ + headHeight / 2f);
            weapon.AddChild(head);

            // Blade edge - tapered box for sharp edge
            var edge = new MeshInstance3D();
            edge.Mesh = new BoxMesh
            {
                Size = new Vector3(0.015f * scale, headThickness * 0.6f, headHeight * 0.85f)
            };
            edge.MaterialOverride = bladeMat;
            edge.Position = new Vector3(side * (headWidth + handleRadius * 0.6f), 0, headStartZ + headHeight / 2f);
            weapon.AddChild(edge);
        }

        return weapon;
    }

    /// <summary>
    /// Create a spear - long polearm with pointed tip.
    /// </summary>
    public static Node3D CreateSpear(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Spear";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.7f, 0.7f, 0.75f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();

        float shaftLength = 1.0f * scale;
        float shaftRadius = 0.018f * scale;
        float headLength = 0.12f * scale;

        // Shaft - extends back from grip, forward to head
        // Grip is 30% from back end
        float gripOffset = shaftLength * 0.3f;
        float shaftCenterZ = -shaftLength / 2f + gripOffset;
        float shaftFrontZ = shaftCenterZ + shaftLength / 2f;

        var shaft = new MeshInstance3D();
        shaft.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius,
            BottomRadius = shaftRadius,
            Height = shaftLength,
            RadialSegments = 8
        };
        shaft.MaterialOverride = handleMat;
        shaft.Position = new Vector3(0, 0, shaftCenterZ);
        shaft.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(shaft);

        // Metal collar at shaft tip - connects shaft to spearhead
        float collarHeight = 0.03f * scale;
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.4f,
            BottomRadius = shaftRadius * 1.2f,
            Height = collarHeight,
            RadialSegments = 8
        };
        collar.MaterialOverride = bladeMat;
        collar.Position = new Vector3(0, 0, shaftFrontZ + collarHeight / 2f);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Spearhead - tapered cylinder (cone) for the blade
        float headBaseZ = shaftFrontZ + collarHeight;
        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,  // Sharp point
            BottomRadius = shaftRadius * 1.3f,  // Matches collar
            Height = headLength,
            RadialSegments = 6
        };
        head.MaterialOverride = bladeMat;
        head.Position = new Vector3(0, 0, headBaseZ + headLength / 2f);
        head.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(head);

        return weapon;
    }

    /// <summary>
    /// Create a mace - one-handed blunt weapon with flanged head.
    /// </summary>
    public static Node3D CreateMace(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Mace";

        var headMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.5f, 0.5f, 0.55f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();

        float handleLength = 0.28f * scale;
        float handleRadius = 0.02f * scale;
        float headRadius = 0.05f * scale;
        float collarHeight = 0.03f * scale;
        float neckHeight = 0.04f * scale;

        // Handle - extends back from grip
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

        // Collar - where handle meets head, overlapping handle end
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

        // Neck - connects collar to head
        float neckStartZ = collarHeight;
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

        // Mace head - use cylinder with tapered ends instead of sphere
        float headHeight = headRadius * 1.6f;
        float headCenterZ = neckStartZ + neckHeight + headHeight / 2f;

        // Main head body (cylinder)
        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh
        {
            TopRadius = headRadius * 0.9f,
            BottomRadius = headRadius * 0.9f,
            Height = headHeight * 0.6f,
            RadialSegments = 8
        };
        head.MaterialOverride = headMat;
        head.Position = new Vector3(0, 0, headCenterZ);
        head.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(head);

        // Top cap (tapered)
        var topCap = new MeshInstance3D();
        topCap.Mesh = new CylinderMesh
        {
            TopRadius = headRadius * 0.4f,
            BottomRadius = headRadius * 0.9f,
            Height = headHeight * 0.25f,
            RadialSegments = 8
        };
        topCap.MaterialOverride = headMat;
        topCap.Position = new Vector3(0, 0, headCenterZ + headHeight * 0.3f + headHeight * 0.125f);
        topCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(topCap);

        // Bottom cap (tapered)
        var bottomCap = new MeshInstance3D();
        bottomCap.Mesh = new CylinderMesh
        {
            TopRadius = headRadius * 0.9f,
            BottomRadius = headRadius * 0.5f,
            Height = headHeight * 0.2f,
            RadialSegments = 8
        };
        bottomCap.MaterialOverride = headMat;
        bottomCap.Position = new Vector3(0, 0, headCenterZ - headHeight * 0.3f - headHeight * 0.1f);
        bottomCap.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(bottomCap);

        // Flanges (4 ridges around the head)
        for (int i = 0; i < 4; i++)
        {
            var flange = new MeshInstance3D();
            flange.Mesh = new BoxMesh
            {
                Size = new Vector3(0.012f * scale, headRadius * 0.4f, headHeight * 0.7f)
            };
            flange.MaterialOverride = headMat;
            float angle = i * 90f;
            float x = Mathf.Cos(Mathf.DegToRad(angle)) * headRadius * 0.7f;
            float y = Mathf.Sin(Mathf.DegToRad(angle)) * headRadius * 0.7f;
            flange.Position = new Vector3(x, y, headCenterZ);
            flange.RotationDegrees = new Vector3(0, 0, angle);
            weapon.AddChild(flange);
        }

        // Spike on top
        var spike = new MeshInstance3D();
        spike.Mesh = new CylinderMesh
        {
            TopRadius = 0.002f * scale,
            BottomRadius = 0.012f * scale,
            Height = 0.035f * scale,
            RadialSegments = 6
        };
        spike.MaterialOverride = headMat;
        spike.Position = new Vector3(0, 0, headCenterZ + headHeight * 0.5f + 0.015f * scale);
        spike.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(spike);

        return weapon;
    }

    /// <summary>
    /// Create a war hammer - two-handed heavy hammer.
    /// </summary>
    public static Node3D CreateWarHammer(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "WarHammer";

        var headMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.45f, 0.45f, 0.5f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();

        float handleLength = 0.55f * scale;
        float handleRadius = 0.025f * scale;
        float headWidth = 0.16f * scale;      // Total width (both sides)
        float headHeight = 0.10f * scale;     // Height and depth of hammer head
        float collarHeight = 0.04f * scale;

        // Long handle - extends back from grip
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

        // Collar - connects handle to head, overlapping handle end
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.8f,
            BottomRadius = handleRadius * 1.5f,
            Height = collarHeight,
            RadialSegments = 12
        };
        collar.MaterialOverride = headMat;
        collar.Position = new Vector3(0, 0, collarHeight / 2f);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Shaft extension through head
        float headStartZ = collarHeight;
        var shaftCore = new MeshInstance3D();
        shaftCore.Mesh = new CylinderMesh
        {
            TopRadius = handleRadius * 1.4f,
            BottomRadius = handleRadius * 1.6f,
            Height = headHeight,
            RadialSegments = 10
        };
        shaftCore.MaterialOverride = headMat;
        shaftCore.Position = new Vector3(0, 0, headStartZ + headHeight / 2f);
        shaftCore.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(shaftCore);

        // Hammer head - one side (striking face)
        float headCenterZ = headStartZ + headHeight / 2f;
        var hammerFace = new MeshInstance3D();
        hammerFace.Mesh = new BoxMesh
        {
            Size = new Vector3(headWidth / 2f, headHeight * 0.9f, headHeight * 0.9f)
        };
        hammerFace.MaterialOverride = headMat;
        hammerFace.Position = new Vector3(headWidth / 4f + handleRadius, 0, headCenterZ);
        weapon.AddChild(hammerFace);

        // Striking face front
        var strikeFace = new MeshInstance3D();
        strikeFace.Mesh = new BoxMesh
        {
            Size = new Vector3(0.015f * scale, headHeight * 0.95f, headHeight * 0.95f)
        };
        strikeFace.MaterialOverride = CreateMetalMaterial(new Color(0.55f, 0.55f, 0.6f));
        strikeFace.Position = new Vector3(headWidth / 2f + handleRadius, 0, headCenterZ);
        weapon.AddChild(strikeFace);

        // Spike on back (points in -X)
        var spike = new MeshInstance3D();
        spike.Mesh = new CylinderMesh
        {
            TopRadius = 0.003f * scale,
            BottomRadius = 0.025f * scale,
            Height = 0.10f * scale,
            RadialSegments = 8
        };
        spike.MaterialOverride = headMat;
        spike.Position = new Vector3(-handleRadius - 0.05f * scale, 0, headCenterZ);
        spike.RotationDegrees = new Vector3(0, 0, -90);
        weapon.AddChild(spike);

        return weapon;
    }

    /// <summary>
    /// Create a staff - magical wooden staff.
    /// </summary>
    public static Node3D CreateStaff(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Staff";

        var woodMat = handleMaterial ?? CreateWoodMaterial(new Color(0.35f, 0.22f, 0.12f));
        var crystalMat = bladeMaterial ?? new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.6f, 1f, 0.9f),
            Emission = new Color(0.3f, 0.5f, 1f),
            EmissionEnergyMultiplier = 1.5f
        };

        float shaftLength = 0.9f * scale;
        float shaftRadius = 0.02f * scale;
        float crystalSize = 0.04f * scale;

        // Grip is 25% from back end
        float gripOffset = shaftLength * 0.25f;
        float shaftCenterZ = -shaftLength / 2f + gripOffset;
        float shaftFrontZ = shaftCenterZ + shaftLength / 2f;

        // Wooden shaft
        var shaft = new MeshInstance3D();
        shaft.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius,
            BottomRadius = shaftRadius,
            Height = shaftLength,
            RadialSegments = 8
        };
        shaft.MaterialOverride = woodMat;
        shaft.Position = new Vector3(0, 0, shaftCenterZ);
        shaft.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(shaft);

        // Staff head - flared top that holds crystal
        float headHeight = 0.04f * scale;
        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.8f,
            BottomRadius = shaftRadius,
            Height = headHeight,
            RadialSegments = 8
        };
        head.MaterialOverride = woodMat;
        head.Position = new Vector3(0, 0, shaftFrontZ + headHeight / 2f);
        head.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(head);

        // Crystal - use a box rotated 45 degrees for a gem shape (avoids sphere rendering issues)
        var crystal = new MeshInstance3D();
        crystal.Mesh = new BoxMesh
        {
            Size = new Vector3(crystalSize, crystalSize, crystalSize)
        };
        crystal.MaterialOverride = crystalMat;
        crystal.Position = new Vector3(0, 0, shaftFrontZ + headHeight + crystalSize * 0.5f);
        crystal.RotationDegrees = new Vector3(45, 45, 0);  // Rotated to look like a gem
        weapon.AddChild(crystal);

        return weapon;
    }

    /// <summary>
    /// Create a bow - ranged weapon.
    /// </summary>
    public static Node3D CreateBow(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Bow";

        var woodMat = handleMaterial ?? CreateWoodMaterial(new Color(0.45f, 0.28f, 0.15f));
        var stringMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.85f, 0.7f),
            Roughness = 0.9f
        };

        float bowHeight = 0.7f * scale;
        float bowThickness = 0.02f * scale;
        float limbLength = bowHeight * 0.42f;
        float curveAngle = 20f;  // Degrees each limb curves back

        // Grip (center handle) - this is where the hand holds
        float gripHeight = 0.10f * scale;
        var grip = new MeshInstance3D();
        grip.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 1.2f,
            BottomRadius = bowThickness * 1.2f,
            Height = gripHeight,
            RadialSegments = 8
        };
        grip.MaterialOverride = CreateLeatherMaterial();
        grip.Position = Vector3.Zero;
        weapon.AddChild(grip);

        // Upper limb - starts at top of grip, curves backward
        var upperLimb = new MeshInstance3D();
        upperLimb.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness * 0.6f,
            BottomRadius = bowThickness,
            Height = limbLength,
            RadialSegments = 6
        };
        upperLimb.MaterialOverride = woodMat;
        // Position at top of grip, rotate to curve back
        upperLimb.Position = new Vector3(0, gripHeight / 2f + limbLength / 2f * Mathf.Cos(Mathf.DegToRad(curveAngle)),
                                          -limbLength / 2f * Mathf.Sin(Mathf.DegToRad(curveAngle)));
        upperLimb.RotationDegrees = new Vector3(-curveAngle, 0, 0);
        weapon.AddChild(upperLimb);

        // Lower limb - starts at bottom of grip, curves backward
        var lowerLimb = new MeshInstance3D();
        lowerLimb.Mesh = new CylinderMesh
        {
            TopRadius = bowThickness,
            BottomRadius = bowThickness * 0.6f,
            Height = limbLength,
            RadialSegments = 6
        };
        lowerLimb.MaterialOverride = woodMat;
        lowerLimb.Position = new Vector3(0, -gripHeight / 2f - limbLength / 2f * Mathf.Cos(Mathf.DegToRad(curveAngle)),
                                          -limbLength / 2f * Mathf.Sin(Mathf.DegToRad(curveAngle)));
        lowerLimb.RotationDegrees = new Vector3(curveAngle, 0, 0);
        weapon.AddChild(lowerLimb);

        // Calculate string endpoints (tips of the limbs)
        float limbEndY = gripHeight / 2f + limbLength * Mathf.Cos(Mathf.DegToRad(curveAngle));
        float limbEndZ = -limbLength * Mathf.Sin(Mathf.DegToRad(curveAngle));

        // String connects the two limb tips
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

        return weapon;
    }

    /// <summary>
    /// Create a club - simple blunt weapon.
    /// </summary>
    public static Node3D CreateClub(float scale = 1f, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Club";

        var woodMat = handleMaterial ?? CreateWoodMaterial(new Color(0.38f, 0.25f, 0.15f));

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

        return weapon;
    }

    /// <summary>
    /// Create a scythe - two-handed reaping weapon.
    /// </summary>
    public static Node3D CreateScythe(float scale = 1f,
        StandardMaterial3D? bladeMaterial = null, StandardMaterial3D? handleMaterial = null)
    {
        var weapon = new Node3D();
        weapon.Name = "Scythe";

        var bladeMat = bladeMaterial ?? CreateMetalMaterial(new Color(0.5f, 0.5f, 0.55f));
        var handleMat = handleMaterial ?? CreateWoodMaterial();

        float shaftLength = 1.1f * scale;
        float shaftRadius = 0.022f * scale;
        float bladeLength = 0.35f * scale;
        float bladeWidth = 0.08f * scale;
        float collarHeight = 0.04f * scale;
        float gripOffset = 0.3f * scale;

        // Calculate shaft position so grip is at origin
        float shaftCenterZ = -shaftLength / 2f + gripOffset;
        float shaftFrontZ = shaftCenterZ + shaftLength / 2f;

        // Long wooden shaft
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

        // Collar where blade attaches - at top of shaft
        var collar = new MeshInstance3D();
        collar.Mesh = new CylinderMesh
        {
            TopRadius = shaftRadius * 1.6f,
            BottomRadius = shaftRadius * 1.5f,
            Height = collarHeight,
            RadialSegments = 10
        };
        collar.MaterialOverride = bladeMat;
        collar.Position = new Vector3(0, 0, shaftFrontZ + collarHeight / 2f - 0.01f * scale);
        collar.RotationDegrees = new Vector3(90, 0, 0);
        weapon.AddChild(collar);

        // Blade attachment tang (connects collar to blade)
        float tangLength = 0.06f * scale;
        var tang = new MeshInstance3D();
        tang.Mesh = new BoxMesh
        {
            Size = new Vector3(0.025f * scale, 0.015f * scale, tangLength)
        };
        tang.MaterialOverride = bladeMat;
        // Tang extends forward and starts to curve
        tang.Position = new Vector3(0.01f * scale, 0, shaftFrontZ + collarHeight + tangLength / 2f);
        tang.RotationDegrees = new Vector3(0, -15, 10);
        weapon.AddChild(tang);

        // Main scythe blade (curved, extends to the side)
        var blade = new MeshInstance3D();
        blade.Mesh = new BoxMesh
        {
            Size = new Vector3(bladeLength, 0.008f * scale, bladeWidth)
        };
        blade.MaterialOverride = bladeMat;
        // Blade curves outward from tang
        float bladeStartZ = shaftFrontZ + collarHeight + tangLength;
        blade.Position = new Vector3(bladeLength / 2f + 0.02f * scale, -0.01f * scale, bladeStartZ + bladeWidth / 2f);
        blade.RotationDegrees = new Vector3(0, -25, 12);
        weapon.AddChild(blade);

        // Blade edge (sharp curved edge on inner side)
        var edge = new MeshInstance3D();
        edge.Mesh = new PrismMesh
        {
            Size = new Vector3(bladeLength * 0.92f, 0.012f * scale, 0.03f * scale),
            LeftToRight = 0f
        };
        edge.MaterialOverride = bladeMat;
        edge.Position = new Vector3(bladeLength / 2f + 0.02f * scale, -0.025f * scale, bladeStartZ + bladeWidth + 0.01f * scale);
        edge.RotationDegrees = new Vector3(0, -25, 12);
        weapon.AddChild(edge);

        // Blade tip point
        var tip = new MeshInstance3D();
        tip.Mesh = new PrismMesh
        {
            Size = new Vector3(0.04f * scale, 0.01f * scale, 0.06f * scale),
            LeftToRight = 0.5f
        };
        tip.MaterialOverride = bladeMat;
        tip.Position = new Vector3(bladeLength + 0.04f * scale, -0.015f * scale, bladeStartZ + bladeWidth * 0.7f);
        tip.RotationDegrees = new Vector3(-90, 60, 0);
        weapon.AddChild(tip);

        return weapon;
    }
}
