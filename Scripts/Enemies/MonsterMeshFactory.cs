using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Factory for creating monster meshes. Shared between BasicEnemy3D and EditorScreen3D
/// to ensure consistent appearance between game and editor preview.
///
/// === BODY PROPORTION CONVENTIONS ===
///
/// Origin: Y=0 is ground level for all monsters.
///
/// Mesh Geometry Rules:
/// - SphereMesh/CapsuleMesh: Center is at node origin. Use Height/2 for top/bottom.
/// - CylinderMesh: Center is at node origin. Use Height/2 for top/bottom.
/// - Position nodes so mesh centers account for mesh dimensions.
///
/// Attachment Point Calculations:
/// - Body Top    = Body.Y + (Height * ScaleY / 2)
/// - Body Bottom = Body.Y - (Height * ScaleY / 2)
/// - ShoulderY   = Body.Y + (Height * ScaleY * 0.3)  -- 30% up from center
/// - HipY        = Body.Y - (Height * ScaleY * 0.3)  -- 30% down from center
///
/// Overlap Rules:
/// - Adjacent meshes should OVERLAP by 15-25% of the smaller dimension.
/// - Example: Head radius 0.2, neck top radius 0.1 → overlap 0.02-0.03 units.
/// - This prevents visible gaps between body parts.
///
/// Joint Spheres:
/// - Add small sphere meshes at attachment points (shoulder, hip, neck base).
/// - Joint radius = 30-50% of limb radius for seamless connection.
///
/// Segmented Chains (spine, neck, tail):
/// - Use parent-child hierarchy: each segment parents the next.
/// - Position each segment relative to previous, with 15% overlap.
/// - Avoids accumulating position errors in flat hierarchies.
///
/// Scale Application:
/// - All dimension values should be multiplied by the scale factor.
/// - Scale is typically 1.0f for normal enemies, 1.5-3x for bosses.
/// </summary>
public static class MonsterMeshFactory
{
    // ========================================================================
    // HELPER STRUCTURES FOR BODY GEOMETRY
    // ========================================================================

    /// <summary>
    /// Calculates attachment points based on actual mesh dimensions.
    /// Use this to position limbs, heads, and other body parts correctly.
    /// </summary>
    private struct BodyGeometry
    {
        /// <summary>Y position of mesh center in world space</summary>
        public float CenterY;
        /// <summary>Mesh radius (for spheres/capsules)</summary>
        public float Radius;
        /// <summary>Mesh height before scaling</summary>
        public float Height;
        /// <summary>Y scale factor applied to mesh</summary>
        public float ScaleY;

        /// <summary>World Y of mesh top surface</summary>
        public readonly float Top => CenterY + (Height * ScaleY / 2f);
        /// <summary>World Y of mesh bottom surface</summary>
        public readonly float Bottom => CenterY - (Height * ScaleY / 2f);
        /// <summary>Y position for shoulder attachments (30% up from center)</summary>
        public readonly float ShoulderY => CenterY + (Height * ScaleY * 0.3f);
        /// <summary>Y position for hip attachments (30% down from center)</summary>
        public readonly float HipY => CenterY - (Height * ScaleY * 0.3f);
        /// <summary>Effective radius accounting for horizontal scale</summary>
        public readonly float EffectiveRadius(float scaleX) => Radius * scaleX;

        public BodyGeometry(float centerY, float radius, float height, float scaleY = 1f)
        {
            CenterY = centerY;
            Radius = radius;
            Height = height;
            ScaleY = scaleY;
        }
    }

    /// <summary>
    /// Creates a small sphere mesh for smooth joints between body parts.
    /// </summary>
    /// <param name="material">Material to apply</param>
    /// <param name="radius">Joint sphere radius (typically 30-50% of limb radius)</param>
    /// <param name="segments">Radial segments (8 for joints is sufficient)</param>
    /// <returns>MeshInstance3D configured as a joint sphere</returns>
    private static MeshInstance3D CreateJointMesh(StandardMaterial3D material, float radius, int segments = 8)
    {
        var joint = new MeshInstance3D();
        var mesh = new SphereMesh
        {
            Radius = radius,
            Height = radius * 2f,
            RadialSegments = segments,
            Rings = segments / 2
        };
        joint.Mesh = mesh;
        joint.MaterialOverride = material;
        return joint;
    }

    /// <summary>
    /// Calculates the overlap distance for seamless mesh connections.
    /// </summary>
    /// <param name="smallerDimension">The smaller of the two connecting radii/dimensions</param>
    /// <param name="overlapPercent">Overlap percentage (0.15-0.25 recommended)</param>
    /// <returns>Distance to overlap meshes</returns>
    private static float CalculateOverlap(float smallerDimension, float overlapPercent = 0.2f)
    {
        return smallerDimension * overlapPercent;
    }

    // ========================================================================
    // EYE GEOMETRY STANDARDS
    // ========================================================================
    //
    // Industry-standard eye proportions for procedural characters:
    //
    // EYE SIZE RULES:
    // - Eye radius = 10-15% of head radius (12% is ideal for most creatures)
    // - Pupil radius = 40-50% of eye radius
    // - Larger eyes (up to 20%) for cute/cartoony creatures (slimes, cats)
    // - Smaller eyes (8-10%) for menacing creatures (skeletons, armor)
    //
    // EYE POSITIONING RULES:
    // - Eyes placed at Y = 15-25% up from head center (0.15-0.25 * headRadius)
    // - Eye horizontal spacing = 35-45% of head radius from center each side
    // - Eye Z (forward) = 70-85% of head radius (ensures eyes stay inside head)
    // - Pupil Z = Eye Z + (eye radius * 0.6) to sit on front of eyeball
    //
    // VALIDATION:
    // - Eye center distance from head center must be < (headRadius - eyeRadius)
    // - This ensures the entire eyeball stays within the head sphere
    //
    // ========================================================================

    /// <summary>
    /// Standard eye proportions for different creature types
    /// </summary>
    private struct EyeProportions
    {
        public float EyeRadiusRatio;     // Eye radius as fraction of head radius (0.10-0.20)
        public float PupilRadiusRatio;   // Pupil radius as fraction of eye radius (0.40-0.50)
        public float EyeYRatio;          // Eye Y position as fraction of head radius (0.15-0.25)
        public float EyeXSpacing;        // Eye X offset as fraction of head radius (0.35-0.45)
        public float EyeZRatio;          // Eye Z position as fraction of head radius (0.70-0.85)

        /// <summary>Standard humanoid eyes (goblins, skeletons, humanoids)</summary>
        /// <remarks>
        /// Based on working Goblin eyes (radius 0.04 for head radius 0.21 = 19%).
        /// EyeZRatio near 1.0 means eye center is near head surface for visibility.
        /// </remarks>
        public static EyeProportions Humanoid => new()
        {
            EyeRadiusRatio = 0.19f,    // 19% of head radius (was 12% - too small!)
            PupilRadiusRatio = 0.45f,
            EyeYRatio = 0.19f,         // Eye Y matches working goblin (0.04/0.21)
            EyeXSpacing = 0.38f,       // Eye X spacing (0.08/0.21)
            EyeZRatio = 0.76f          // Eye Z position (0.16/0.21) - forward enough to be visible
        };

        /// <summary>Cute/cartoony eyes (slimes, cats, friendly creatures)</summary>
        /// <remarks>Large expressive eyes for cute creatures.</remarks>
        public static EyeProportions Cute => new()
        {
            EyeRadiusRatio = 0.24f,    // 24% of head - large cute eyes (was 18%)
            PupilRadiusRatio = 0.50f,
            EyeYRatio = 0.22f,
            EyeXSpacing = 0.32f,
            EyeZRatio = 0.72f          // Slightly more forward for visibility
        };

        /// <summary>Menacing eyes (skeletons, demons, scary creatures)</summary>
        /// <remarks>Smaller, intense eyes that still need to be visible.</remarks>
        public static EyeProportions Menacing => new()
        {
            EyeRadiusRatio = 0.16f,    // 16% of head (was 10% - invisible!)
            PupilRadiusRatio = 0.40f,
            EyeYRatio = 0.18f,
            EyeXSpacing = 0.40f,
            EyeZRatio = 0.78f
        };

        /// <summary>Beast eyes (wolves, lizards, animals)</summary>
        /// <remarks>Forward-facing predator eyes, need to be visible on elongated snouts.</remarks>
        public static EyeProportions Beast => new()
        {
            EyeRadiusRatio = 0.22f,    // 22% of head (was 14% - too small for beast heads!)
            PupilRadiusRatio = 0.45f,
            EyeYRatio = 0.15f,
            EyeXSpacing = 0.42f,
            EyeZRatio = 0.75f          // More forward to appear on head surface
        };

        /// <summary>Wide-set spider/insect eyes</summary>
        /// <remarks>Multiple small eyes arranged in clusters.</remarks>
        public static EyeProportions Arachnid => new()
        {
            EyeRadiusRatio = 0.15f,    // 15% of head (was 8% - way too small!)
            PupilRadiusRatio = 0.50f,
            EyeYRatio = 0.25f,
            EyeXSpacing = 0.45f,
            EyeZRatio = 0.80f
        };
    }

    /// <summary>
    /// Calculates proper eye dimensions and positions based on head size.
    /// Ensures eyes stay within head bounds using industry-standard proportions.
    /// </summary>
    /// <param name="headRadius">The radius of the head sphere</param>
    /// <param name="proportions">Eye proportion preset to use</param>
    /// <param name="scale">Overall scale factor</param>
    /// <returns>Tuple of (eyeRadius, pupilRadius, eyeY, eyeX, eyeZ, pupilZ)</returns>
    private static (float eyeRadius, float pupilRadius, float eyeY, float eyeX, float eyeZ, float pupilZ)
        CalculateEyeGeometry(float headRadius, EyeProportions proportions, float scale = 1f)
    {
        // Calculate eye dimensions based on head size and proportions
        float eyeRadius = headRadius * proportions.EyeRadiusRatio;
        float pupilRadius = eyeRadius * proportions.PupilRadiusRatio;
        float eyeY = headRadius * proportions.EyeYRatio;
        float eyeX = headRadius * proportions.EyeXSpacing;
        float eyeZ = headRadius * proportions.EyeZRatio;

        // Pupil should protrude slightly from eye surface for visibility
        float pupilZ = eyeZ + (eyeRadius * 0.7f);

        // IMPORTANT: Eyes should protrude from head surface for cartoon/stylized look.
        // We do NOT want eyes fully contained inside the head sphere - that makes them invisible.
        // For stylized 3D characters, eyes typically sit ON the head surface with half the eye
        // protruding outward. The Z position places the eye center, so the front of the eye
        // (at eyeZ + eyeRadius) should extend beyond the head surface (at headRadius on Z axis).

        // Only apply minimal safety check - prevent eyes from being placed behind head center
        if (eyeZ < eyeRadius)
        {
            eyeZ = eyeRadius; // Minimum Z: eye center at least one eye-radius forward
            pupilZ = eyeZ + (eyeRadius * 0.7f);
        }

        return (eyeRadius, pupilRadius, eyeY, eyeX, eyeZ, pupilZ);
    }

    /// <summary>
    /// Creates a pair of eyes with pupils for a head, using proper proportional geometry.
    /// </summary>
    /// <param name="headNode">The head node to attach eyes to</param>
    /// <param name="headRadius">Radius of the head</param>
    /// <param name="eyeMat">Material for the eyeball (usually white)</param>
    /// <param name="pupilMat">Material for the pupil (usually black)</param>
    /// <param name="proportions">Eye proportion preset</param>
    /// <param name="lod">Level of detail</param>
    /// <param name="scale">Scale factor (default 1.0)</param>
    private static void CreateEyePair(
        Node3D headNode,
        float headRadius,
        StandardMaterial3D eyeMat,
        StandardMaterial3D pupilMat,
        EyeProportions proportions,
        LODLevel lod,
        float scale = 1f)
    {
        var (eyeRadius, pupilRadius, eyeY, eyeX, eyeZ, pupilZ) =
            CalculateEyeGeometry(headRadius, proportions, scale);

        int eyeSegs = lod == LODLevel.High ? 16 : (lod == LODLevel.Medium ? 12 : 8);
        int pupilSegs = Mathf.Max(6, eyeSegs / 2);

        // Create eye meshes - SphereMesh with Height = 2*Radius for perfect sphere
        var eyeMesh = new SphereMesh
        {
            Radius = eyeRadius,
            Height = eyeRadius * 2f,
            RadialSegments = eyeSegs,
            Rings = eyeSegs / 2
        };

        var pupilMesh = new SphereMesh
        {
            Radius = pupilRadius,
            Height = pupilRadius * 2f,
            RadialSegments = pupilSegs,
            Rings = pupilSegs / 2
        };

        // Left eye
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-eyeX, eyeY, eyeZ);
        headNode.AddChild(leftEye);

        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-eyeX, eyeY, pupilZ);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(eyeX, eyeY, eyeZ);
        headNode.AddChild(rightEye);

        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(eyeX, eyeY, pupilZ);
        headNode.AddChild(rightPupil);
    }

    /// <summary>
    /// Creates a single centered eye with pupil (for cyclops-type creatures).
    /// </summary>
    private static void CreateSingleEye(
        Node3D headNode,
        float headRadius,
        StandardMaterial3D eyeMat,
        StandardMaterial3D pupilMat,
        EyeProportions proportions,
        LODLevel lod,
        float scale = 1f)
    {
        var (eyeRadius, pupilRadius, eyeY, _, eyeZ, pupilZ) =
            CalculateEyeGeometry(headRadius, proportions, scale);

        int eyeSegs = lod == LODLevel.High ? 16 : (lod == LODLevel.Medium ? 12 : 8);
        int pupilSegs = Mathf.Max(6, eyeSegs / 2);

        var eyeMesh = new SphereMesh
        {
            Radius = eyeRadius,
            Height = eyeRadius * 2f,
            RadialSegments = eyeSegs,
            Rings = eyeSegs / 2
        };

        var pupilMesh = new SphereMesh
        {
            Radius = pupilRadius,
            Height = pupilRadius * 2f,
            RadialSegments = pupilSegs,
            Rings = pupilSegs / 2
        };

        var eye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        eye.Position = new Vector3(0, eyeY, eyeZ);
        headNode.AddChild(eye);

        var pupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        pupil.Position = new Vector3(0, eyeY, pupilZ);
        headNode.AddChild(pupil);
    }

    // ========================================================================
    // LIMB NODES AND LOD
    // ========================================================================

    /// <summary>
    /// Container for limb nodes used in animation
    /// </summary>
    public class LimbNodes
    {
        public Node3D? Head { get; set; }
        public Node3D? Body { get; set; }        // Torso/body for sway animations
        public Node3D? LeftArm { get; set; }
        public Node3D? RightArm { get; set; }
        public Node3D? LeftLeg { get; set; }
        public Node3D? RightLeg { get; set; }
        public Node3D? Tail { get; set; }        // Tail for wag/sway animations
        public Node3D? Weapon { get; set; }
        public Node3D? Torch { get; set; }

        /// <summary>
        /// Check if this is a quadruped (4 legs, no arms used for walking)
        /// Can be set explicitly or detected via naming convention
        /// </summary>
        public bool IsQuadruped { get; set; } = false;
    }

    /// <summary>
    /// Level of detail for monster meshes
    /// </summary>
    public enum LODLevel
    {
        High,    // Full detail: 24-32 segments, all features
        Medium,  // Simplified: 16 segments, reduced details
        Low      // Basic silhouette: 8 segments, minimal features
    }

    /// <summary>
    /// Get radial segment count based on LOD level
    /// </summary>
    private static int GetRadialSegments(LODLevel lod)
    {
        return lod switch
        {
            LODLevel.High => 32,
            LODLevel.Medium => 16,
            LODLevel.Low => 8,
            _ => 16
        };
    }

    /// <summary>
    /// Get ring count based on LOD level
    /// </summary>
    private static int GetRings(LODLevel lod)
    {
        return lod switch
        {
            LODLevel.High => 16,
            LODLevel.Medium => 8,
            LODLevel.Low => 4,
            _ => 8
        };
    }

    /// <summary>
    /// Create a monster mesh and add it to the parent node.
    /// Returns limb nodes for animation purposes.
    /// LOD level defaults to the current GraphicsConfig setting.
    /// </summary>
    public static LimbNodes CreateMonsterMesh(Node3D parent, string monsterType, Color? skinColorOverride = null, LODLevel? lodOverride = null)
    {
        var limbs = new LimbNodes();
        var lod = lodOverride ?? SafeRoom3D.Core.GraphicsConfig.CurrentLODLevel;

        switch (monsterType.ToLower())
        {
            // Original monsters - use existing signatures (some have LOD, some don't)
            case "goblin":
                CreateGoblinMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "goblin_shaman":
                CreateGoblinShamanMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "goblin_thrower":
                CreateGoblinThrowerMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "goblin_warlord":
            case "boss_goblin":
                CreateGoblinWarlordMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "slime":
                CreateSlimeMesh(parent, limbs, skinColorOverride);
                break;
            case "skeleton":
                CreateSkeletonMesh(parent, limbs, lod);
                break;
            case "wolf":
                CreateWolfMesh(parent, limbs, lod);
                break;
            case "bat":
                CreateBatMesh(parent, limbs);
                break;
            case "dragon":
                CreateDragonMesh(parent, limbs);
                break;
            case "eye":
                CreateEyeMesh(parent, limbs, skinColorOverride);
                break;
            case "mushroom":
                CreateMushroomMesh(parent, limbs, skinColorOverride);
                break;
            case "spider":
                CreateSpiderMesh(parent, limbs, skinColorOverride);
                break;
            case "lizard":
                CreateLizardMesh(parent, limbs, skinColorOverride);
                break;
            case "badlama":
                CreateBadlamaMesh(parent, limbs, skinColorOverride);
                break;
            // Original Bosses
            case "slime_king":
            case "boss_slime":
                CreateSlimeKingMesh(parent, limbs, skinColorOverride);
                break;
            case "skeleton_lord":
            case "boss_skeleton":
                CreateSkeletonLordMesh(parent, limbs);
                break;
            case "dragon_king":
            case "boss_dragon":
                CreateDragonKingMesh(parent, limbs);
                break;
            case "spider_queen":
            case "boss_spider":
                CreateSpiderQueenMesh(parent, limbs);
                break;
            case "sporeling_elder":
            case "boss_mushroom":
                CreateMushroomMesh(parent, limbs, skinColorOverride); // TODO: Integrate full SporelingElderMesh from ENHANCED_MUSHROOM_CODE.txt
                break;

            // ========================================================================
            // NEW DCC-THEMED MONSTERS (15 new types) - all have LOD support
            // ========================================================================

            // Humanoid monsters
            case "crawler_killer":
                CreateCrawlerKillerMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "shadow_stalker":
                CreateShadowStalkerMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "flesh_golem":
                CreateFleshGolemMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "plague_bearer":
                CreatePlagueBearerMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "living_armor":
                CreateLivingArmorMesh(parent, limbs, skinColorOverride, lod);
                break;

            // Machine monsters
            case "camera_drone":
                CreateCameraDroneMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "shock_drone":
                CreateShockDroneMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "advertiser_bot":
                CreateAdvertiserBotMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "clockwork_spider":
                CreateClockworkSpiderMesh(parent, limbs, skinColorOverride, lod);
                break;

            // Elemental monsters
            case "lava_elemental":
                CreateLavaElementalMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "ice_wraith":
                CreateIceWraithMesh(parent, limbs, skinColorOverride, lod);
                break;

            // Aberration monsters
            case "gelatinous_cube":
                CreateGelatinousCubeMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "void_spawn":
                CreateVoidSpawnMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "mimic":
                CreateMimicMesh(parent, limbs, skinColorOverride, lod);
                break;

            // Beast monsters
            case "dungeon_rat":
                CreateDungeonRatMesh(parent, limbs, skinColorOverride, lod);
                break;

            // ========================================================================
            // NEW DCC-THEMED BOSSES (5 new types) - all have LOD support
            // ========================================================================
            case "the_butcher":
            case "boss_butcher":
                CreateButcherMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "syndicate_enforcer":
            case "boss_enforcer":
                CreateSyndicateEnforcerMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "hive_mother":
            case "boss_hive":
                CreateHiveMotherMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "architects_favorite":
            case "boss_architect":
                CreateArchitectsFavoriteMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "mordecais_shadow":
            case "boss_mordecai":
                CreateMordecaisShadowMesh(parent, limbs, skinColorOverride, lod);
                break;

            // ADDITIONAL DCC BOSSES - Unique character models
            case "mordecai_the_traitor":
            case "boss_mordecai_traitor":
                CreateMordecaiTheTraitorMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "princess_donut":
            case "boss_princess_donut":
                CreatePrincessDonutMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "mongo_the_destroyer":
            case "boss_mongo":
                CreateMongoTheDestroyerMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "zev_the_loot_goblin":
            case "boss_zev":
                CreateZevTheLootGoblinMesh(parent, limbs, skinColorOverride, lod);
                break;

            // ========================================================================
            // NPCs (non-combatant characters)
            // ========================================================================
            case "bopca":
                CreateBopcaMesh(parent, limbs, skinColorOverride, lod);
                break;
            case "mordecai":
                CreateMordecaiMesh(parent, limbs, skinColorOverride, lod);
                break;

            default:
                CreateSlimeMesh(parent, limbs, skinColorOverride);
                break;
        }

        return limbs;
    }

    private static void CreateGoblinMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride, LODLevel lod = LODLevel.High, float sizeVariation = 1.0f)
    {
        // Apply random hue shift for variation
        Color baseSkin = skinColorOverride ?? new Color(0.45f, 0.55f, 0.35f);
        float hueShift = (GD.Randf() - 0.5f) * 0.1f; // +/- 5% hue
        Color skinColor = baseSkin.Lightened(hueShift);
        Color darkSkinColor = skinColor.Darkened(0.2f);
        Color clothColor = new Color(0.4f, 0.3f, 0.2f);
        Color eyeColor = new Color(0.9f, 0.85f, 0.2f);
        Color toothColor = new Color(0.85f, 0.82f, 0.7f);

        // LOD-based segment counts
        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.8f };
        var darkSkinMat = new StandardMaterial3D { AlbedoColor = darkSkinColor, Roughness = 0.85f };
        var clothMat = new StandardMaterial3D { AlbedoColor = clothColor, Roughness = 0.9f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.3f
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        var toothMat = new StandardMaterial3D { AlbedoColor = toothColor, Roughness = 0.6f };

        // Apply size variation
        float scale = sizeVariation;

        // === BODY GEOMETRY CALCULATION ===
        // Body: SphereMesh centered at Y=0.5, height=0.48, scaleY=1.2
        float bodyRadius = 0.24f * scale;
        float bodyHeight = 0.48f * scale;
        float bodyCenterY = 0.5f * scale;
        float bodyScaleY = 1.2f;
        var bodyGeom = new BodyGeometry(bodyCenterY, bodyRadius, bodyHeight, bodyScaleY);

        // Head: SphereMesh radius=0.21, height=0.42
        float headRadius = 0.21f * scale;
        float headHeight = 0.42f * scale;

        // Body - muscular torso with asymmetry
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = bodyRadius, Height = bodyHeight, RadialSegments = radialSegs, Rings = rings };
        body.Mesh = bodyMesh;
        body.MaterialOverride = clothMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.Scale = new Vector3(0.92f, bodyScaleY, 0.78f); // Asymmetric for organic feel
        parent.AddChild(body);

        // Belly showing under clothes (only in High LOD)
        if (lod == LODLevel.High)
        {
            var belly = new MeshInstance3D();
            var bellyMesh = new SphereMesh { Radius = 0.13f * scale, Height = 0.26f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
            belly.Mesh = bellyMesh;
            belly.MaterialOverride = skinMat;
            belly.Position = new Vector3(0, 0.38f * scale, 0.09f * scale);
            parent.AddChild(belly);
        }

        // Cloth/loincloth texture detail - ragged edges (High LOD)
        if (lod == LODLevel.High)
        {
            var ragMat = new StandardMaterial3D { AlbedoColor = clothColor.Darkened(0.15f), Roughness = 0.95f };
            // Hanging cloth strips
            for (int i = 0; i < 4; i++)
            {
                float angle = (i - 1.5f) * 0.4f;
                var stripMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.15f * scale, 0.02f * scale) };
                var strip = new MeshInstance3D { Mesh = stripMesh, MaterialOverride = ragMat };
                strip.Position = new Vector3(Mathf.Sin(angle) * 0.18f * scale, 0.22f * scale, Mathf.Cos(angle) * 0.12f * scale);
                strip.RotationDegrees = new Vector3(5 + i * 3, angle * 20f, i % 2 == 0 ? 5 : -5);
                parent.AddChild(strip);
            }
            // Belt/rope around waist
            var beltMesh = new CylinderMesh { TopRadius = 0.22f * scale, BottomRadius = 0.22f * scale, Height = 0.04f * scale, RadialSegments = radialSegs / 2 };
            var belt = new MeshInstance3D { Mesh = beltMesh, MaterialOverride = ragMat };
            belt.Position = new Vector3(0, 0.32f * scale, 0);
            parent.AddChild(belt);
        }

        // === NECK JOINT - bridges body top to head bottom ===
        // Neck connects body top to head bottom with overlap
        float neckRadius = 0.08f * scale;
        float neckOverlap = CalculateOverlap(neckRadius);
        // Position head so its bottom overlaps into body top
        float headCenterY = bodyGeom.Top + (headHeight / 2f) - neckOverlap;

        // Add neck joint sphere for seamless connection
        var neckJoint = CreateJointMesh(skinMat, neckRadius);
        neckJoint.Position = new Vector3(0, bodyGeom.Top - neckOverlap * 0.5f, 0.02f * scale);
        parent.AddChild(neckJoint);

        // Head (in Node3D for animation) - positioned with overlap
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0.02f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Randomize head asymmetry
        float headAsymX = 0.98f + (GD.Randf() - 0.5f) * 0.08f; // 0.94-1.02
        float headAsymY = 0.92f + (GD.Randf() - 0.5f) * 0.06f; // 0.89-0.95

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.21f * scale, Height = 0.42f * scale, RadialSegments = radialSegs, Rings = rings };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        head.Scale = new Vector3(headAsymX, headAsymY, 0.94f); // Asymmetry for organic feel
        headNode.AddChild(head);

        // Warty skin texture - small bumps on face and head (High LOD only)
        if (lod == LODLevel.High)
        {
            var wartMat = new StandardMaterial3D { AlbedoColor = skinColor.Darkened(0.1f), Roughness = 0.9f };
            var wartMesh = new SphereMesh { Radius = 0.012f * scale, Height = 0.024f * scale, RadialSegments = 6, Rings = 4 };
            // Warts on cheeks and forehead
            float[] wartAngles = { -0.5f, 0.3f, 0.7f, -0.2f, 0.5f };
            float[] wartHeights = { 0.02f, 0.06f, -0.04f, 0.08f, 0.0f };
            for (int i = 0; i < wartAngles.Length; i++)
            {
                var wart = new MeshInstance3D { Mesh = wartMesh, MaterialOverride = wartMat };
                wart.Position = new Vector3(
                    wartAngles[i] * 0.15f * scale,
                    wartHeights[i] * scale,
                    0.16f * scale + Mathf.Abs(wartAngles[i]) * 0.03f * scale
                );
                headNode.AddChild(wart);
            }
            // Larger wart near nose
            var bigWartMesh = new SphereMesh { Radius = 0.018f * scale, Height = 0.036f * scale, RadialSegments = 6, Rings = 4 };
            var bigWart = new MeshInstance3D { Mesh = bigWartMesh, MaterialOverride = wartMat };
            bigWart.Position = new Vector3(0.06f * scale, -0.01f * scale, 0.17f * scale);
            headNode.AddChild(bigWart);
        }

        // Prominent brow ridge (menacing goblin face)
        if (lod >= LODLevel.Medium)
        {
            var browMesh = new BoxMesh { Size = new Vector3(0.2f * scale, 0.045f * scale, 0.09f * scale) };
            var brow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkSkinMat };
            brow.Position = new Vector3(0, 0.09f * scale, 0.15f * scale);
            headNode.AddChild(brow);
        }

        // Eyes with pupils - sized appropriately for goblin head (radius 0.21)
        int eyeSegs = lod == LODLevel.High ? 16 : (lod == LODLevel.Medium ? 12 : 8);
        var eyeMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = eyeSegs, Rings = eyeSegs / 2 };
        var pupilMesh = new SphereMesh { Radius = 0.018f * scale, Height = 0.036f * scale, RadialSegments = eyeSegs / 2, Rings = eyeSegs / 4 };

        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.08f * scale, 0.04f * scale, 0.16f * scale);
        headNode.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-0.08f * scale, 0.04f * scale, 0.19f * scale);
        headNode.AddChild(leftPupil);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.08f * scale, 0.04f * scale, 0.16f * scale);
        headNode.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(0.08f * scale, 0.04f * scale, 0.19f * scale);
        headNode.AddChild(rightPupil);

        // Nose with nostrils - more prominent and hooked
        // CRITICAL: SphereMesh MUST have Height = 2*Radius for proper sphere rendering
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.055f * scale, Height = 0.11f * scale, RadialSegments = 16, Rings = 12 };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = skinMat;
        nose.Position = new Vector3(0, -0.02f * scale, 0.18f * scale);
        nose.Scale = new Vector3(1.05f, 0.85f, 0.75f); // Wider, flatter
        headNode.AddChild(nose);

        // Nostrils - larger and more visible
        var nostrilMesh = new SphereMesh { Radius = 0.018f * scale, Height = 0.036f * scale, RadialSegments = 12, Rings = 8 };
        var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = darkSkinMat };
        leftNostril.Position = new Vector3(-0.028f * scale, -0.04f * scale, 0.21f * scale);
        leftNostril.Scale = new Vector3(0.9f, 1.1f, 0.8f);
        headNode.AddChild(leftNostril);
        var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = darkSkinMat };
        rightNostril.Position = new Vector3(0.028f * scale, -0.04f * scale, 0.21f * scale);
        rightNostril.Scale = new Vector3(0.9f, 1.1f, 0.8f);
        headNode.AddChild(rightNostril);

        // Mouth area
        var mouthMesh = new BoxMesh { Size = new Vector3(0.1f, 0.03f, 0.04f) };
        var mouth = new MeshInstance3D { Mesh = mouthMesh, MaterialOverride = darkSkinMat };
        mouth.Position = new Vector3(0, -0.1f, 0.15f);
        headNode.AddChild(mouth);

        // Teeth (snaggle-toothed goblin look)
        var toothMesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.012f, Height = 0.04f };
        // Lower fangs sticking up
        var tooth1 = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
        tooth1.Position = new Vector3(-0.03f, -0.08f, 0.17f);
        tooth1.RotationDegrees = new Vector3(10, 0, 5);
        headNode.AddChild(tooth1);
        var tooth2 = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
        tooth2.Position = new Vector3(0.025f, -0.085f, 0.16f);
        tooth2.RotationDegrees = new Vector3(8, 0, -8);
        headNode.AddChild(tooth2);
        // Upper teeth
        var smallToothMesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.008f, Height = 0.025f };
        var tooth3 = new MeshInstance3D { Mesh = smallToothMesh, MaterialOverride = toothMat };
        tooth3.Position = new Vector3(-0.015f, -0.095f, 0.17f);
        tooth3.RotationDegrees = new Vector3(-170, 0, 0);
        headNode.AddChild(tooth3);
        var tooth4 = new MeshInstance3D { Mesh = smallToothMesh, MaterialOverride = toothMat };
        tooth4.Position = new Vector3(0.01f, -0.093f, 0.168f);
        tooth4.RotationDegrees = new Vector3(-175, 0, 0);
        headNode.AddChild(tooth4);

        // Large pointed ears with variation - key goblin feature!
        float earSizeVar = 0.95f + GD.Randf() * 0.15f; // 0.95-1.1x variation
        int earSegs = lod == LODLevel.High ? radialSegs / 2 : Mathf.Max(8, radialSegs / 4);
        var earMesh = new CylinderMesh {
            TopRadius = 0.01f * scale * earSizeVar,
            BottomRadius = 0.06f * scale * earSizeVar,
            Height = 0.24f * scale * earSizeVar,
            RadialSegments = earSegs,
            Rings = Mathf.Max(4, earSegs / 2)
        };

        // Asymmetric ear angles for organic feel
        float leftEarAngle = -56f + (GD.Randf() - 0.5f) * 8f; // -60 to -52
        float rightEarAngle = 54f + (GD.Randf() - 0.5f) * 8f;  // 50 to 58

        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.21f * scale, 0.02f * scale, -0.02f * scale);
        leftEar.RotationDegrees = new Vector3(12, 20, leftEarAngle);
        leftEar.Scale = new Vector3(1.08f, 1f, 0.96f);
        headNode.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.21f * scale, 0.02f * scale, -0.02f * scale);
        rightEar.RotationDegrees = new Vector3(12, -17, rightEarAngle);
        rightEar.Scale = new Vector3(0.96f, 1f, 1.04f);
        headNode.AddChild(rightEar);

        // Inner ear detail (Medium and High LOD only)
        if (lod >= LODLevel.Medium)
        {
            var innerEarMesh = new CylinderMesh {
                TopRadius = 0.005f * scale * earSizeVar,
                BottomRadius = 0.032f * scale * earSizeVar,
                Height = 0.13f * scale * earSizeVar,
                RadialSegments = Mathf.Max(8, earSegs / 2),
                Rings = Mathf.Max(3, earSegs / 4)
            };
            var leftInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = darkSkinMat };
            leftInnerEar.Position = new Vector3(-0.19f * scale, 0.02f * scale, 0);
            leftInnerEar.RotationDegrees = new Vector3(12, 20, leftEarAngle);
            headNode.AddChild(leftInnerEar);
            var rightInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = darkSkinMat };
            rightInnerEar.Position = new Vector3(0.19f * scale, 0.02f * scale, 0);
            rightInnerEar.RotationDegrees = new Vector3(12, -17, rightEarAngle);
            headNode.AddChild(rightInnerEar);
        }

        // === ARMS - positioned at shoulder height from body geometry ===
        int armSegs = lod == LODLevel.High ? radialSegs / 2 : Mathf.Max(8, radialSegs / 4);
        float upperArmRadius = 0.052f * scale;
        var upperArmMesh = new CapsuleMesh {
            Radius = upperArmRadius,
            Height = 0.22f * scale,
            RadialSegments = armSegs,
            Rings = Mathf.Max(4, armSegs / 2)
        };
        var lowerArmMesh = new CapsuleMesh {
            Radius = 0.045f * scale,
            Height = 0.2f * scale,
            RadialSegments = armSegs,
            Rings = Mathf.Max(3, armSegs / 3)
        };
        var handMesh = new SphereMesh {
            Radius = 0.042f * scale,
            Height = 0.084f * scale,
            RadialSegments = Mathf.Max(8, armSegs),
            Rings = Mathf.Max(4, armSegs / 2)
        };
        var fingerMesh = new CylinderMesh {
            TopRadius = 0.01f * scale,
            BottomRadius = 0.014f * scale,
            Height = 0.065f * scale,
            RadialSegments = Mathf.Max(6, armSegs / 2),
            Rings = 2
        };
        // Pointed claw mesh for fingernails
        var clawMat = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.22f, 0.18f), Roughness = 0.4f };
        var clawMesh = new CylinderMesh {
            TopRadius = 0.002f * scale,
            BottomRadius = 0.008f * scale,
            Height = 0.03f * scale,
            RadialSegments = 6,
            Rings = 1
        };

        // Calculate shoulder position from body geometry
        float shoulderY = bodyGeom.ShoulderY;
        float shoulderX = bodyGeom.EffectiveRadius(0.92f) + upperArmRadius * 0.3f; // Slightly outside body
        float shoulderJointRadius = upperArmRadius * 0.6f;

        // Left arm with shoulder joint
        var leftShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius);
        leftShoulderJoint.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftShoulderJoint);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -26);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = skinMat };
        leftUpperArm.Position = new Vector3(0, -0.09f * scale, 0);
        leftArmNode.AddChild(leftUpperArm);
        var leftLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = skinMat };
        leftLowerArm.Position = new Vector3(0, -0.27f * scale, 0.02f * scale);
        leftLowerArm.RotationDegrees = new Vector3(-16, 0, 0);
        leftArmNode.AddChild(leftLowerArm);
        var leftHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = skinMat };
        leftHand.Position = new Vector3(0, -0.41f * scale, 0.045f * scale);
        leftHand.Scale = new Vector3(1f, 0.72f, 1.25f);
        leftArmNode.AddChild(leftHand);
        // Fingers (3-finger goblin hands, only High LOD)
        if (lod == LODLevel.High)
        {
            for (int f = 0; f < 3; f++)
            {
                var finger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = skinMat };
                finger.Position = new Vector3((-0.022f + f * 0.022f) * scale, -0.45f * scale, 0.065f * scale);
                finger.RotationDegrees = new Vector3(-62, 0, -12 + f * 12);
                leftArmNode.AddChild(finger);
                // Pointed claw at fingertip
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.022f + f * 0.022f) * scale, -0.48f * scale, 0.095f * scale);
                claw.RotationDegrees = new Vector3(-45, 0, -12 + f * 12);
                leftArmNode.AddChild(claw);
            }
        }

        // Right arm with shoulder joint
        var rightShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius);
        rightShoulderJoint.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightShoulderJoint);

        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0);
        rightArmNode.RotationDegrees = new Vector3(0, 0, 26);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = skinMat };
        rightUpperArm.Position = new Vector3(0, -0.09f * scale, 0);
        rightArmNode.AddChild(rightUpperArm);
        var rightLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = skinMat };
        rightLowerArm.Position = new Vector3(0, -0.27f * scale, 0.02f * scale);
        rightLowerArm.RotationDegrees = new Vector3(-16, 0, 0);
        rightArmNode.AddChild(rightLowerArm);
        var rightHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = skinMat };
        rightHand.Position = new Vector3(0, -0.41f * scale, 0.045f * scale);
        rightHand.Scale = new Vector3(1f, 0.72f, 1.25f);
        rightArmNode.AddChild(rightHand);
        // Fingers (3-finger goblin hands, only High LOD)
        if (lod == LODLevel.High)
        {
            for (int f = 0; f < 3; f++)
            {
                var finger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = skinMat };
                finger.Position = new Vector3((-0.022f + f * 0.022f) * scale, -0.45f * scale, 0.065f * scale);
                finger.RotationDegrees = new Vector3(-62, 0, -12 + f * 12);
                rightArmNode.AddChild(finger);
                // Pointed claw at fingertip
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.022f + f * 0.022f) * scale, -0.48f * scale, 0.095f * scale);
                claw.RotationDegrees = new Vector3(-45, 0, -12 + f * 12);
                rightArmNode.AddChild(claw);
            }
        }

        // Hand attachment point for weapons
        // Weapon origin is at grip center, blade extends forward (+Z)
        // Position Hand at the palm location
        // Rotate so weapon points forward-up for natural swing arc
        var rightHandAttach = new Node3D();
        rightHandAttach.Name = "Hand";
        rightHandAttach.Position = new Vector3(0.02f * scale, -0.44f * scale, 0.08f * scale);
        // X rotation tilts blade up/down, Y rotates blade left/right
        // -30 X tilts blade slightly upward for overhead swing
        // -26 Z counters arm's outward tilt
        rightHandAttach.RotationDegrees = new Vector3(-30f, 0f, -26f);
        rightArmNode.AddChild(rightHandAttach);

        // === LEGS - positioned at hip height from body geometry ===
        int legSegs = lod == LODLevel.High ? radialSegs / 2 : Mathf.Max(8, radialSegs / 4);
        float legRadius = 0.063f * scale;
        var legMesh = new CapsuleMesh {
            Radius = legRadius,
            Height = 0.3f * scale,
            RadialSegments = legSegs,
            Rings = Mathf.Max(5, legSegs / 2)
        };
        var footMesh = new SphereMesh {
            Radius = 0.052f * scale,
            Height = 0.104f * scale,
            RadialSegments = Mathf.Max(8, legSegs),
            Rings = Mathf.Max(4, legSegs / 2)
        };
        var toeMesh = new CylinderMesh {
            TopRadius = 0.012f * scale,
            BottomRadius = 0.017f * scale,
            Height = 0.045f * scale,
            RadialSegments = Mathf.Max(6, legSegs / 2),
            Rings = 2
        };

        // Calculate hip position from body geometry
        float hipY = bodyGeom.HipY;
        float hipX = 0.11f * scale; // Horizontal offset for leg spread
        float hipJointRadius = legRadius * 0.5f;

        // Left leg with hip joint
        var leftHipJoint = CreateJointMesh(skinMat, hipJointRadius);
        leftHipJoint.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftHipJoint);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        leftLegNode.AddChild(leftLeg);
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = skinMat };
        leftFoot.Position = new Vector3(0, -0.17f * scale, 0.045f * scale);
        leftFoot.Scale = new Vector3(0.82f, 0.52f, 1.35f);
        leftLegNode.AddChild(leftFoot);
        // Toes (3-toe goblin feet, only High LOD)
        if (lod == LODLevel.High)
        {
            for (int t = 0; t < 3; t++)
            {
                var toe = new MeshInstance3D { Mesh = toeMesh, MaterialOverride = skinMat };
                toe.Position = new Vector3((-0.022f + t * 0.022f) * scale, -0.19f * scale, 0.095f * scale);
                toe.RotationDegrees = new Vector3(-72, 0, -12 + t * 12);
                leftLegNode.AddChild(toe);
            }
        }

        // Right leg with hip joint
        var rightHipJoint = CreateJointMesh(skinMat, hipJointRadius);
        rightHipJoint.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightHipJoint);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        rightLegNode.AddChild(rightLeg);
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = skinMat };
        rightFoot.Position = new Vector3(0, -0.17f * scale, 0.045f * scale);
        rightFoot.Scale = new Vector3(0.82f, 0.52f, 1.35f);
        rightLegNode.AddChild(rightFoot);
        // Toes (3-toe goblin feet, only High LOD)
        if (lod == LODLevel.High)
        {
            for (int t = 0; t < 3; t++)
            {
                var toe = new MeshInstance3D { Mesh = toeMesh, MaterialOverride = skinMat };
                toe.Position = new Vector3((-0.022f + t * 0.022f) * scale, -0.19f * scale, 0.095f * scale);
                toe.RotationDegrees = new Vector3(-72, 0, -12 + t * 12);
                rightLegNode.AddChild(toe);
            }
        }

        // Add simple weapon (dagger/club) for basic goblin - only High LOD
        if (lod == LODLevel.High)
        {
            var weaponNode = new Node3D();
            weaponNode.Position = new Vector3(0.08f * scale, -0.42f * scale, 0.06f * scale);
            rightArmNode.AddChild(weaponNode);
            limbs.Weapon = weaponNode;

            // Simple crude dagger
            var daggerHandle = new MeshInstance3D();
            var handleMesh = new CylinderMesh {
                TopRadius = 0.012f * scale,
                BottomRadius = 0.012f * scale,
                Height = 0.12f * scale,
                RadialSegments = 8
            };
            daggerHandle.Mesh = handleMesh;
            daggerHandle.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.2f, 0.15f), Roughness = 0.8f };
            daggerHandle.RotationDegrees = new Vector3(-75, 0, 0);
            weaponNode.AddChild(daggerHandle);

            var daggerBlade = new MeshInstance3D();
            var bladeMesh = new CylinderMesh {
                TopRadius = 0.004f * scale,
                BottomRadius = 0.018f * scale,
                Height = 0.16f * scale,
                RadialSegments = 8
            };
            daggerBlade.Mesh = bladeMesh;
            daggerBlade.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.6f, 0.65f), Metallic = 0.5f, Roughness = 0.5f };
            daggerBlade.Position = new Vector3(0, 0.12f, -0.05f);
            daggerBlade.RotationDegrees = new Vector3(-75, 0, 0);
            weaponNode.AddChild(daggerBlade);
        }
    }

    private static void CreateGoblinShamanMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride, LODLevel lod = LODLevel.High, float sizeVariation = 1.0f)
    {
        // Shaman is a magical goblin with robes and a staff
        Color baseSkin = skinColorOverride ?? new Color(0.35f, 0.45f, 0.55f); // Blue-green tint
        float hueShift = (GD.Randf() - 0.5f) * 0.08f;
        Color skinColor = baseSkin.Lightened(hueShift);
        Color robeColor = new Color(0.25f, 0.15f, 0.4f); // Purple robes
        Color glowColor = new Color(0.4f, 0.8f, 1f); // Magical cyan glow
        Color eyeColor = new Color(0.4f, 0.9f, 1f); // Glowing cyan eyes

        // LOD-based segment counts
        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);
        float scale = sizeVariation;

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.8f };
        var robeMat = new StandardMaterial3D { AlbedoColor = robeColor, Roughness = 0.9f };
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = glowColor,
            EmissionEnabled = true,
            Emission = glowColor,
            EmissionEnergyMultiplier = 1.5f
        };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.8f
        };

        // === BODY GEOMETRY ===
        float bodyTopRadius = 0.2f * scale;
        float bodyBottomRadius = 0.35f * scale;
        float bodyHeight = 0.7f * scale;
        float bodyCenterY = 0.35f * scale;
        var bodyGeom = new BodyGeometry(bodyCenterY, bodyTopRadius, bodyHeight);

        // Head geometry
        float headRadius = 0.2f * scale;
        float headHeight = 0.4f * scale;

        // Body in long robes
        var body = new MeshInstance3D();
        var bodyMesh = new CylinderMesh { TopRadius = bodyTopRadius, BottomRadius = bodyBottomRadius, Height = bodyHeight };
        body.Mesh = bodyMesh;
        body.MaterialOverride = robeMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        parent.AddChild(body);

        // === NECK JOINT ===
        float neckRadius = 0.08f * scale;
        float neckOverlap = CalculateOverlap(neckRadius);
        var neckJoint = CreateJointMesh(skinMat, neckRadius);
        neckJoint.Position = new Vector3(0, bodyGeom.Top - neckOverlap * 0.5f, 0);
        parent.AddChild(neckJoint);

        // Head - positioned with overlap into body top
        float headCenterY = bodyGeom.Top + headHeight / 2f - neckOverlap;
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = headRadius, Height = headHeight };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        headNode.AddChild(head);

        // Brow ridge (menacing magical look)
        if (lod >= LODLevel.Medium)
        {
            var browMesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.04f * scale, 0.08f * scale) };
            var darkSkinMat = new StandardMaterial3D { AlbedoColor = skinColor.Darkened(0.2f), Roughness = 0.85f };
            var brow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkSkinMat };
            brow.Position = new Vector3(0, 0.08f * scale, 0.14f * scale);
            headNode.AddChild(brow);
        }

        // Glowing eyes with magical pupils - Goblin-style (cyan for shaman)
        int eyeSegs = lod == LODLevel.High ? 16 : (lod == LODLevel.Medium ? 12 : 8);
        var shamanEyeMesh = new SphereMesh { Radius = 0.038f * scale, Height = 0.076f * scale, RadialSegments = eyeSegs, Rings = eyeSegs / 2 };
        // Magical glowing pupil (brighter cyan core)
        var magicPupilMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 1f, 1f),
            EmissionEnabled = true,
            Emission = new Color(0.6f, 1f, 1f),
            EmissionEnergyMultiplier = 2f
        };
        var shamanPupilMesh = new SphereMesh { Radius = 0.016f * scale, Height = 0.032f * scale, RadialSegments = eyeSegs / 2, Rings = eyeSegs / 4 };

        // Left eye
        var leftEye = new MeshInstance3D { Mesh = shamanEyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.075f * scale, 0.035f * scale, 0.15f * scale);
        headNode.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = shamanPupilMesh, MaterialOverride = magicPupilMat };
        leftPupil.Position = new Vector3(-0.075f * scale, 0.035f * scale, 0.18f * scale);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightEye = new MeshInstance3D { Mesh = shamanEyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.075f * scale, 0.035f * scale, 0.15f * scale);
        headNode.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = shamanPupilMesh, MaterialOverride = magicPupilMat };
        rightPupil.Position = new Vector3(0.075f * scale, 0.035f * scale, 0.18f * scale);
        headNode.AddChild(rightPupil);

        // Pointy ears with elaborate piercings
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.05f, Height = 0.15f };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.2f, 0.02f, 0);
        leftEar.RotationDegrees = new Vector3(0, 0, -60);
        headNode.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.2f, 0.02f, 0);
        rightEar.RotationDegrees = new Vector3(0, 0, 60);
        headNode.AddChild(rightEar);

        // Ear piercings - bone rings (High LOD)
        if (lod == LODLevel.High)
        {
            var boneMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), Roughness = 0.6f };
            var ringMesh = new CylinderMesh { TopRadius = 0.018f * scale, BottomRadius = 0.018f * scale, Height = 0.008f * scale, RadialSegments = 8 };
            // Left ear rings (2)
            var leftRing1 = new MeshInstance3D { Mesh = ringMesh, MaterialOverride = boneMat };
            leftRing1.Position = new Vector3(-0.26f * scale, -0.02f * scale, 0.02f * scale);
            leftRing1.RotationDegrees = new Vector3(0, 90, -60);
            headNode.AddChild(leftRing1);
            var leftRing2 = new MeshInstance3D { Mesh = ringMesh, MaterialOverride = boneMat };
            leftRing2.Position = new Vector3(-0.23f * scale, 0.04f * scale, 0.02f * scale);
            leftRing2.RotationDegrees = new Vector3(0, 90, -60);
            headNode.AddChild(leftRing2);
            // Right ear ring
            var rightRing = new MeshInstance3D { Mesh = ringMesh, MaterialOverride = boneMat };
            rightRing.Position = new Vector3(0.26f * scale, -0.02f * scale, 0.02f * scale);
            rightRing.RotationDegrees = new Vector3(0, 90, 60);
            headNode.AddChild(rightRing);
        }

        // Hood (partial) - Height = 2*Radius for proper sphere
        var hood = new MeshInstance3D();
        var hoodMesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
        hood.Mesh = hoodMesh;
        hood.MaterialOverride = robeMat;
        hood.Position = new Vector3(0, 0.08f, -0.05f);
        hood.Scale = new Vector3(1f, 0.6f, 0.8f);
        headNode.AddChild(hood);

        // Feathers and bones headdress (High and Medium LOD)
        if (lod >= LODLevel.Medium)
        {
            var boneMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), Roughness = 0.6f };
            var featherMat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.1f, 0.25f), Roughness = 0.8f };
            var featherTipMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.2f, 0.3f), Roughness = 0.7f };

            // Central bone spike on headdress
            var boneSpikeMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.02f * scale, Height = 0.12f * scale, RadialSegments = 6 };
            var centralBone = new MeshInstance3D { Mesh = boneSpikeMesh, MaterialOverride = boneMat };
            centralBone.Position = new Vector3(0, 0.18f * scale, -0.02f * scale);
            centralBone.RotationDegrees = new Vector3(-15, 0, 0);
            headNode.AddChild(centralBone);

            // Side feathers (3 on each side)
            var featherMesh = new CylinderMesh { TopRadius = 0.003f * scale, BottomRadius = 0.012f * scale, Height = 0.15f * scale, RadialSegments = 6 };
            var featherTipMesh = new BoxMesh { Size = new Vector3(0.025f * scale, 0.06f * scale, 0.004f * scale) };
            for (int i = 0; i < 3; i++)
            {
                // Left feathers
                var leftFeather = new MeshInstance3D { Mesh = featherMesh, MaterialOverride = featherMat };
                leftFeather.Position = new Vector3(-0.08f * scale - i * 0.04f * scale, 0.14f * scale, -0.04f * scale);
                leftFeather.RotationDegrees = new Vector3(-20 - i * 10, 0, -25 - i * 8);
                headNode.AddChild(leftFeather);
                var leftTip = new MeshInstance3D { Mesh = featherTipMesh, MaterialOverride = featherTipMat };
                leftTip.Position = new Vector3(-0.1f * scale - i * 0.06f * scale, 0.2f * scale + i * 0.02f * scale, -0.06f * scale);
                leftTip.RotationDegrees = new Vector3(-20 - i * 10, 0, -25 - i * 8);
                headNode.AddChild(leftTip);

                // Right feathers
                var rightFeather = new MeshInstance3D { Mesh = featherMesh, MaterialOverride = featherMat };
                rightFeather.Position = new Vector3(0.08f * scale + i * 0.04f * scale, 0.14f * scale, -0.04f * scale);
                rightFeather.RotationDegrees = new Vector3(-20 - i * 10, 0, 25 + i * 8);
                headNode.AddChild(rightFeather);
                var rightTip = new MeshInstance3D { Mesh = featherTipMesh, MaterialOverride = featherTipMat };
                rightTip.Position = new Vector3(0.1f * scale + i * 0.06f * scale, 0.2f * scale + i * 0.02f * scale, -0.06f * scale);
                rightTip.RotationDegrees = new Vector3(-20 - i * 10, 0, 25 + i * 8);
                headNode.AddChild(rightTip);
            }

            // Small bones hanging from headdress
            var smallBoneMesh = new CylinderMesh { TopRadius = 0.004f * scale, BottomRadius = 0.008f * scale, Height = 0.05f * scale, RadialSegments = 5 };
            for (int i = 0; i < 2; i++)
            {
                var bone = new MeshInstance3D { Mesh = smallBoneMesh, MaterialOverride = boneMat };
                bone.Position = new Vector3((i == 0 ? -0.12f : 0.12f) * scale, 0.05f * scale, -0.08f * scale);
                bone.RotationDegrees = new Vector3(0, 0, i == 0 ? -20 : 20);
                headNode.AddChild(bone);
            }
        }

        // Tribal markings on face (colored stripe boxes) - High LOD
        if (lod == LODLevel.High)
        {
            var markingMat = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.3f, 0.2f), Roughness = 0.9f };
            // Forehead marking
            var foreheadMark = new MeshInstance3D();
            foreheadMark.Mesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.015f * scale, 0.01f * scale) };
            foreheadMark.MaterialOverride = markingMat;
            foreheadMark.Position = new Vector3(0, 0.1f * scale, 0.17f * scale);
            headNode.AddChild(foreheadMark);
            // Cheek markings (stripes)
            for (int i = 0; i < 2; i++)
            {
                var cheekMark = new MeshInstance3D();
                cheekMark.Mesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.008f * scale, 0.01f * scale) };
                cheekMark.MaterialOverride = markingMat;
                cheekMark.Position = new Vector3(-0.11f * scale, -0.02f * scale - i * 0.02f * scale, 0.12f * scale);
                cheekMark.RotationDegrees = new Vector3(0, 25, 10);
                headNode.AddChild(cheekMark);
                var cheekMark2 = new MeshInstance3D();
                cheekMark2.Mesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.008f * scale, 0.01f * scale) };
                cheekMark2.MaterialOverride = markingMat;
                cheekMark2.Position = new Vector3(0.11f * scale, -0.02f * scale - i * 0.02f * scale, 0.12f * scale);
                cheekMark2.RotationDegrees = new Vector3(0, -25, -10);
                headNode.AddChild(cheekMark2);
            }
        }

        // === ARMS - positioned at shoulder height ===
        float armRadius = 0.06f * scale;
        float shoulderY = bodyGeom.ShoulderY;
        float shoulderX = bodyTopRadius + armRadius * 0.5f;
        float shoulderJointRadius = armRadius * 0.5f;
        var armMesh = new CapsuleMesh { Radius = armRadius, Height = 0.3f * scale };

        // Right shoulder joint
        var rightShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius);
        rightShoulderJoint.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightShoulderJoint);

        // Staff arm (right)
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = armMesh;
        rightArm.MaterialOverride = skinMat;
        rightArm.RotationDegrees = new Vector3(0, 0, -30);
        rightArmNode.AddChild(rightArm);

        // Hand attachment point for weapons
        // Weapon origin is at grip center, blade extends forward (+Z)
        // Rotate for natural casting pose with staff forward
        var rightHandAttach = new Node3D();
        rightHandAttach.Name = "Hand";
        rightHandAttach.Position = new Vector3(0.12f * scale, -0.20f * scale, 0.06f * scale);
        rightHandAttach.RotationDegrees = new Vector3(-20f, 0f, 0f);
        rightArmNode.AddChild(rightHandAttach);

        // Magical staff
        var staffNode = new Node3D();
        staffNode.Position = new Vector3(0.15f * scale, -0.1f * scale, 0);
        rightArmNode.AddChild(staffNode);
        limbs.Weapon = staffNode;

        var staff = new MeshInstance3D();
        var staffMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.03f * scale, Height = 1.2f * scale };
        staff.Mesh = staffMesh;
        staff.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.2f, 0.15f) };
        staff.Position = new Vector3(0, -0.4f * scale, 0);
        staffNode.AddChild(staff);

        // Glowing orb on staff with inner glow detail
        var orb = new MeshInstance3D();
        var orbMesh = new SphereMesh { Radius = 0.1f * scale, Height = 0.2f * scale, RadialSegments = radialSegs, Rings = rings };
        orb.Mesh = orbMesh;
        // Outer orb is semi-transparent
        var outerOrbMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(glowColor.R, glowColor.G, glowColor.B, 0.6f),
            EmissionEnabled = true,
            Emission = glowColor * 0.8f,
            EmissionEnergyMultiplier = 1.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        orb.MaterialOverride = outerOrbMat;
        orb.Position = new Vector3(0, 0.25f * scale, 0);
        staffNode.AddChild(orb);
        // Inner core with bright glow
        var innerCore = new MeshInstance3D();
        var innerCoreMesh = new SphereMesh { Radius = 0.05f * scale, Height = 0.1f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var innerCoreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 1f, 1f),
            EmissionEnergyMultiplier = 3f
        };
        innerCore.Mesh = innerCoreMesh;
        innerCore.MaterialOverride = innerCoreMat;
        innerCore.Position = new Vector3(0, 0.25f * scale, 0);
        staffNode.AddChild(innerCore);
        // Swirling energy wisps inside (High LOD)
        if (lod == LODLevel.High)
        {
            var wispMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.008f * scale, Height = 0.06f * scale, RadialSegments = 6 };
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.Tau / 3f;
                var wisp = new MeshInstance3D { Mesh = wispMesh, MaterialOverride = glowMat };
                wisp.Position = new Vector3(Mathf.Cos(angle) * 0.04f * scale, 0.25f * scale, Mathf.Sin(angle) * 0.04f * scale);
                wisp.RotationDegrees = new Vector3(45 + i * 30, i * 60, 0);
                staffNode.AddChild(wisp);
            }
        }

        // Pouches on belt (High and Medium LOD)
        if (lod >= LODLevel.Medium)
        {
            var pouchMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.28f, 0.18f), Roughness = 0.85f };
            var pouchMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.07f * scale, 0.04f * scale) };
            // Left pouch
            var leftPouch = new MeshInstance3D { Mesh = pouchMesh, MaterialOverride = pouchMat };
            leftPouch.Position = new Vector3(-0.22f * scale, 0.08f * scale, 0.12f * scale);
            leftPouch.RotationDegrees = new Vector3(0, 20, 0);
            parent.AddChild(leftPouch);
            // Right pouch (larger)
            var largePouchMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.09f * scale, 0.05f * scale) };
            var rightPouch = new MeshInstance3D { Mesh = largePouchMesh, MaterialOverride = pouchMat };
            rightPouch.Position = new Vector3(0.2f * scale, 0.06f * scale, 0.14f * scale);
            rightPouch.RotationDegrees = new Vector3(0, -15, 0);
            parent.AddChild(rightPouch);
            // Pouch flap details
            var flapMesh = new BoxMesh { Size = new Vector3(0.065f * scale, 0.02f * scale, 0.045f * scale) };
            var flapMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.24f, 0.15f), Roughness = 0.9f };
            var leftFlap = new MeshInstance3D { Mesh = flapMesh, MaterialOverride = flapMat };
            leftFlap.Position = new Vector3(-0.22f * scale, 0.12f * scale, 0.12f * scale);
            leftFlap.RotationDegrees = new Vector3(-15, 20, 0);
            parent.AddChild(leftFlap);
        }

        // Left shoulder joint
        var leftShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius);
        leftShoulderJoint.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftShoulderJoint);

        // Left arm
        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        leftArm.RotationDegrees = new Vector3(0, 0, 30);
        leftArmNode.AddChild(leftArm);
    }

    private static void CreateGoblinThrowerMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride, LODLevel lod = LODLevel.High, float sizeVariation = 1.0f)
    {
        // Thrower is a ranged goblin with a bandolier and throwing weapons
        Color baseSkin = skinColorOverride ?? new Color(0.5f, 0.45f, 0.35f); // Tan/brown
        float hueShift = (GD.Randf() - 0.5f) * 0.08f;
        Color skinColor = baseSkin.Lightened(hueShift);
        Color clothColor = new Color(0.35f, 0.25f, 0.2f); // Leather
        Color metalColor = new Color(0.5f, 0.5f, 0.55f); // Steel
        Color eyeColor = new Color(0.9f, 0.75f, 0.2f);
        float scale = sizeVariation;

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.8f };
        var clothMat = new StandardMaterial3D { AlbedoColor = clothColor, Roughness = 0.9f };
        var metalMat = new StandardMaterial3D { AlbedoColor = metalColor, Metallic = 0.6f, Roughness = 0.4f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.3f
        };

        // === BODY GEOMETRY ===
        float bodyRadius = 0.22f * scale;
        float bodyHeight = 0.44f * scale;
        float bodyCenterY = 0.5f * scale;
        float bodyScaleY = 1.1f;
        var bodyGeom = new BodyGeometry(bodyCenterY, bodyRadius, bodyHeight, bodyScaleY);

        // Head geometry
        float headRadius = 0.2f * scale;
        float headHeight = 0.4f * scale;

        // Body - lean and athletic
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = bodyRadius, Height = bodyHeight };
        body.Mesh = bodyMesh;
        body.MaterialOverride = clothMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.Scale = new Vector3(0.9f, bodyScaleY, 0.8f);
        parent.AddChild(body);

        // Bandolier (diagonal strap with spears)
        var bandolier = new MeshInstance3D();
        var bandMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.6f * scale, 0.04f * scale) };
        bandolier.Mesh = bandMesh;
        bandolier.MaterialOverride = clothMat;
        bandolier.Position = new Vector3(0.05f * scale, 0.5f * scale, 0.12f * scale);
        bandolier.RotationDegrees = new Vector3(0, 0, 30);
        parent.AddChild(bandolier);

        // Mini spears on bandolier
        for (int i = 0; i < 3; i++)
        {
            var miniSpear = new MeshInstance3D();
            var miniSpearMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.02f * scale, Height = 0.25f * scale };
            miniSpear.Mesh = miniSpearMesh;
            miniSpear.MaterialOverride = metalMat;
            miniSpear.Position = new Vector3((-0.08f + i * 0.08f) * scale, (0.55f + i * 0.08f) * scale, 0.18f * scale);
            miniSpear.RotationDegrees = new Vector3(20, 0, 30);
            parent.AddChild(miniSpear);
        }

        // === NECK JOINT ===
        float neckRadius = 0.08f * scale;
        float neckOverlap = CalculateOverlap(neckRadius);
        var neckJoint = CreateJointMesh(skinMat, neckRadius);
        neckJoint.Position = new Vector3(0, bodyGeom.Top - neckOverlap * 0.5f, 0);
        parent.AddChild(neckJoint);

        // Head - positioned with overlap
        float headCenterY = bodyGeom.Top + headHeight / 2f - neckOverlap;
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = headRadius, Height = headHeight };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        headNode.AddChild(head);

        // Brow ridge (menacing look like regular goblin)
        if (lod >= LODLevel.Medium)
        {
            var browMesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.04f * scale, 0.08f * scale) };
            var darkSkinMat = new StandardMaterial3D { AlbedoColor = skinColor.Darkened(0.15f), Roughness = 0.85f };
            var brow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkSkinMat };
            brow.Position = new Vector3(0, 0.08f * scale, 0.14f * scale);
            headNode.AddChild(brow);
        }

        // Eyes with pupils - Goblin-style (golden/amber for thrower)
        int throwerEyeSegs = lod == LODLevel.High ? 16 : (lod == LODLevel.Medium ? 12 : 8);
        var throwerEyeMesh = new SphereMesh { Radius = 0.038f * scale, Height = 0.076f * scale, RadialSegments = throwerEyeSegs, Rings = throwerEyeSegs / 2 };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        var throwerPupilMesh = new SphereMesh { Radius = 0.016f * scale, Height = 0.032f * scale, RadialSegments = throwerEyeSegs / 2, Rings = throwerEyeSegs / 4 };

        // Left eye
        var leftEye = new MeshInstance3D { Mesh = throwerEyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.075f * scale, 0.035f * scale, 0.15f * scale);
        headNode.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = throwerPupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-0.075f * scale, 0.035f * scale, 0.18f * scale);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightEye = new MeshInstance3D { Mesh = throwerEyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.075f * scale, 0.035f * scale, 0.15f * scale);
        headNode.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = throwerPupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(0.075f * scale, 0.035f * scale, 0.18f * scale);
        headNode.AddChild(rightPupil);

        // Pointy nose
        var nose = new MeshInstance3D();
        var noseMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.04f, Height = 0.12f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = skinMat;
        nose.Position = new Vector3(0, -0.02f, 0.18f);
        nose.RotationDegrees = new Vector3(-70, 0, 0);
        headNode.AddChild(nose);

        // Ears
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.055f, Height = 0.16f };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.2f, 0.02f, 0);
        leftEar.RotationDegrees = new Vector3(0, 0, -55);
        headNode.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.2f, 0.02f, 0);
        rightEar.RotationDegrees = new Vector3(0, 0, 55);
        headNode.AddChild(rightEar);

        // Headband
        var headband = new MeshInstance3D();
        var hbMesh = new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.19f };
        headband.Mesh = hbMesh;
        headband.MaterialOverride = clothMat;
        headband.Position = new Vector3(0, 0.08f, 0);
        headband.Scale = new Vector3(1.1f, 0.3f, 1.1f);
        headNode.AddChild(headband);

        // War paint markings on face (High LOD)
        if (lod == LODLevel.High)
        {
            var warPaintMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.15f, 0.1f), Roughness = 0.95f };
            // Horizontal stripe across eyes
            var eyeStripe = new MeshInstance3D();
            eyeStripe.Mesh = new BoxMesh { Size = new Vector3(0.22f * scale, 0.025f * scale, 0.01f * scale) };
            eyeStripe.MaterialOverride = warPaintMat;
            eyeStripe.Position = new Vector3(0, 0.035f * scale, 0.17f * scale);
            headNode.AddChild(eyeStripe);
            // Chin markings (vertical stripes)
            for (int i = 0; i < 3; i++)
            {
                var chinMark = new MeshInstance3D();
                chinMark.Mesh = new BoxMesh { Size = new Vector3(0.012f * scale, 0.04f * scale, 0.008f * scale) };
                chinMark.MaterialOverride = warPaintMat;
                chinMark.Position = new Vector3((-0.03f + i * 0.03f) * scale, -0.12f * scale, 0.14f * scale);
                headNode.AddChild(chinMark);
            }
        }

        // Scars on face and body (darker line boxes) - High LOD
        if (lod == LODLevel.High)
        {
            var scarMat = new StandardMaterial3D { AlbedoColor = skinColor.Darkened(0.35f), Roughness = 0.7f };
            // Scar across left cheek
            var cheekScar = new MeshInstance3D();
            cheekScar.Mesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.008f * scale, 0.008f * scale) };
            cheekScar.MaterialOverride = scarMat;
            cheekScar.Position = new Vector3(-0.1f * scale, -0.02f * scale, 0.14f * scale);
            cheekScar.RotationDegrees = new Vector3(0, 15, -20);
            headNode.AddChild(cheekScar);
            // Scar on forehead
            var foreheadScar = new MeshInstance3D();
            foreheadScar.Mesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.006f * scale, 0.006f * scale) };
            foreheadScar.MaterialOverride = scarMat;
            foreheadScar.Position = new Vector3(0.05f * scale, 0.1f * scale, 0.16f * scale);
            foreheadScar.RotationDegrees = new Vector3(0, 0, 35);
            headNode.AddChild(foreheadScar);
            // Body scar (on chest)
            var bodyScar = new MeshInstance3D();
            bodyScar.Mesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.01f * scale, 0.01f * scale) };
            bodyScar.MaterialOverride = scarMat;
            bodyScar.Position = new Vector3(-0.05f * scale, 0.55f * scale, 0.18f * scale);
            bodyScar.RotationDegrees = new Vector3(0, 10, 25);
            parent.AddChild(bodyScar);
        }

        // === ARMS - positioned at shoulder height ===
        float armRadius = 0.065f * scale;
        float shoulderY = bodyGeom.ShoulderY;
        float shoulderX = bodyGeom.EffectiveRadius(0.9f) + armRadius * 0.5f;
        float shoulderJointRadius = armRadius * 0.5f;
        var armMesh = new CapsuleMesh { Radius = armRadius, Height = 0.28f * scale };

        // Right shoulder joint
        var rightShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius);
        rightShoulderJoint.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightShoulderJoint);

        // Right arm (throwing arm) - stronger/more muscular
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        // Throwing arm is larger/more muscular
        var throwingArmMesh = new CapsuleMesh { Radius = armRadius * 1.15f, Height = 0.28f * scale };
        var rightArm = new MeshInstance3D { Mesh = throwingArmMesh, MaterialOverride = skinMat };
        rightArm.RotationDegrees = new Vector3(-20, 0, -35);
        rightArmNode.AddChild(rightArm);

        // Muscle bulge on throwing arm (bicep/tricep detail)
        if (lod >= LODLevel.Medium)
        {
            var muscleMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = 12, Rings = 8 };
            // Bicep bulge
            var bicep = new MeshInstance3D { Mesh = muscleMesh, MaterialOverride = skinMat };
            bicep.Position = new Vector3(0.02f * scale, -0.06f * scale, 0.04f * scale);
            bicep.Scale = new Vector3(1.2f, 1.4f, 1f);
            bicep.RotationDegrees = new Vector3(-20, 0, -35);
            rightArmNode.AddChild(bicep);
            // Forearm muscle
            var forearmMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = 10, Rings = 6 };
            var forearm = new MeshInstance3D { Mesh = forearmMesh, MaterialOverride = skinMat };
            forearm.Position = new Vector3(0.04f * scale, -0.16f * scale, 0.06f * scale);
            forearm.Scale = new Vector3(1.1f, 1.5f, 0.9f);
            forearm.RotationDegrees = new Vector3(-30, 0, -35);
            rightArmNode.AddChild(forearm);
        }

        // Armguard on throwing arm (leather bracer with metal studs)
        if (lod >= LODLevel.Medium)
        {
            var armguardMat = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.22f, 0.15f), Roughness = 0.75f };
            var armguardMesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.055f * scale, Height = 0.1f * scale, RadialSegments = 12 };
            var armguard = new MeshInstance3D { Mesh = armguardMesh, MaterialOverride = armguardMat };
            armguard.Position = new Vector3(0.05f * scale, -0.18f * scale, 0.06f * scale);
            armguard.RotationDegrees = new Vector3(-30, 0, -35);
            rightArmNode.AddChild(armguard);
            // Metal studs on armguard
            if (lod == LODLevel.High)
            {
                var studMesh = new CylinderMesh { TopRadius = 0.008f * scale, BottomRadius = 0.01f * scale, Height = 0.015f * scale, RadialSegments = 6 };
                for (int i = 0; i < 3; i++)
                {
                    var stud = new MeshInstance3D { Mesh = studMesh, MaterialOverride = metalMat };
                    stud.Position = new Vector3(0.08f * scale, -0.15f * scale + i * 0.03f * scale, 0.1f * scale);
                    stud.RotationDegrees = new Vector3(60, 0, 0);
                    rightArmNode.AddChild(stud);
                }
            }
        }

        // Hand attachment point for weapons
        // Weapon origin is at grip center, blade extends forward (+Z)
        // Thrower holds weapon ready to throw (angled up)
        var rightHandAttach = new Node3D();
        rightHandAttach.Name = "Hand";
        rightHandAttach.Position = new Vector3(0.1f * scale, -0.24f * scale, 0.10f * scale);
        rightHandAttach.RotationDegrees = new Vector3(-40f, 0f, 0f);
        rightArmNode.AddChild(rightHandAttach);

        // Spear in hand
        var weaponNode = new Node3D();
        weaponNode.Position = new Vector3(0.15f * scale, -0.15f * scale, 0.1f * scale);
        rightArmNode.AddChild(weaponNode);
        limbs.Weapon = weaponNode;

        var spear = new MeshInstance3D();
        var spearMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.02f * scale, Height = 0.7f * scale };
        spear.Mesh = spearMesh;
        spear.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.3f, 0.2f) };
        spear.RotationDegrees = new Vector3(-60, 0, 0);
        weaponNode.AddChild(spear);

        var spearHead = new MeshInstance3D();
        var shMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.04f * scale, Height = 0.12f * scale };
        spearHead.Mesh = shMesh;
        spearHead.MaterialOverride = metalMat;
        spearHead.Position = new Vector3(0, 0.38f * scale, -0.15f * scale);
        spearHead.RotationDegrees = new Vector3(-60, 0, 0);
        weaponNode.AddChild(spearHead);

        // Left shoulder joint
        var leftShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius);
        leftShoulderJoint.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftShoulderJoint);

        // Left arm
        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        leftArm.RotationDegrees = new Vector3(0, 0, 25);
        leftArmNode.AddChild(leftArm);

        // === LEGS - positioned at hip height ===
        float legRadius = 0.07f * scale;
        float hipY = bodyGeom.HipY;
        float hipX = 0.1f * scale;
        float hipJointRadius = legRadius * 0.5f;
        var legMesh = new CapsuleMesh { Radius = legRadius, Height = 0.35f * scale };

        // Left hip joint
        var leftHipJoint = CreateJointMesh(skinMat, hipJointRadius);
        leftHipJoint.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftHipJoint);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        leftLegNode.AddChild(leftLeg);

        // Right hip joint
        var rightHipJoint = CreateJointMesh(skinMat, hipJointRadius);
        rightHipJoint.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightHipJoint);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        rightLegNode.AddChild(rightLeg);
    }

    private static void CreateGoblinWarlordMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride, LODLevel lod = LODLevel.High, float sizeVariation = 1.5f)
    {
        // Warlord is a BOSS goblin - larger, heavily armored, intimidating
        Color baseSkin = skinColorOverride ?? new Color(0.35f, 0.45f, 0.3f); // Dark green
        float hueShift = (GD.Randf() - 0.5f) * 0.05f;
        Color skinColor = baseSkin.Darkened(0.1f + hueShift); // Darker than normal goblins
        Color armorColor = new Color(0.25f, 0.25f, 0.28f); // Dark steel
        Color goldColor = new Color(0.85f, 0.75f, 0.3f); // Gold trim
        Color eyeColor = new Color(1f, 0.2f, 0.1f); // Menacing red eyes

        // LOD-based segment counts
        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);
        float scale = sizeVariation; // Default 1.5x for boss

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.8f };
        var darkSkinMat = new StandardMaterial3D { AlbedoColor = skinColor.Darkened(0.2f), Roughness = 0.85f };
        var armorMat = new StandardMaterial3D { AlbedoColor = armorColor, Metallic = 0.7f, Roughness = 0.4f };
        var goldMat = new StandardMaterial3D { AlbedoColor = goldColor, Metallic = 0.8f, Roughness = 0.3f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.6f
        };

        // === BODY GEOMETRY ===
        float bodyRadius = 0.32f * scale;
        float bodyHeight = 0.64f * scale;
        float bodyCenterY = 0.6f * scale;
        float bodyScaleY = 1.25f;
        float bodyScaleX = 1.1f;
        var bodyGeom = new BodyGeometry(bodyCenterY, bodyRadius, bodyHeight, bodyScaleY);

        // Head geometry
        float headRadius = 0.24f * scale;
        float headHeight = 0.48f * scale;

        // Massive muscular body with armor plating
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = bodyRadius, Height = bodyHeight, RadialSegments = radialSegs, Rings = rings };
        body.Mesh = bodyMesh;
        body.MaterialOverride = skinMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.Scale = new Vector3(bodyScaleX, bodyScaleY, 0.95f); // Broad-shouldered
        parent.AddChild(body);

        // Bulging muscle definition (emphasizes strength)
        if (lod >= LODLevel.Medium)
        {
            var muscleMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale, RadialSegments = 12, Rings = 8 };
            // Pectoral muscles
            var leftPec = new MeshInstance3D { Mesh = muscleMesh, MaterialOverride = skinMat };
            leftPec.Position = new Vector3(-0.12f * scale, 0.7f * scale, 0.22f * scale);
            leftPec.Scale = new Vector3(1.2f, 0.7f, 0.6f);
            parent.AddChild(leftPec);
            var rightPec = new MeshInstance3D { Mesh = muscleMesh, MaterialOverride = skinMat };
            rightPec.Position = new Vector3(0.12f * scale, 0.7f * scale, 0.22f * scale);
            rightPec.Scale = new Vector3(1.2f, 0.7f, 0.6f);
            parent.AddChild(rightPec);

            // Bicep bulges (visible below pauldrons)
            var bicepMesh = new SphereMesh { Radius = 0.06f * scale, Height = 0.12f * scale, RadialSegments = 10, Rings = 6 };
            var leftBicep = new MeshInstance3D { Mesh = bicepMesh, MaterialOverride = skinMat };
            leftBicep.Position = new Vector3(-0.38f * scale, 0.6f * scale, 0.02f * scale);
            leftBicep.Scale = new Vector3(1.3f, 1f, 1f);
            parent.AddChild(leftBicep);
            var rightBicep = new MeshInstance3D { Mesh = bicepMesh, MaterialOverride = skinMat };
            rightBicep.Position = new Vector3(0.38f * scale, 0.6f * scale, 0.02f * scale);
            rightBicep.Scale = new Vector3(1.3f, 1f, 1f);
            parent.AddChild(rightBicep);
        }

        // Battle scars (dark healed wounds)
        if (lod == LODLevel.High)
        {
            var scarMat = new StandardMaterial3D
            {
                AlbedoColor = skinColor.Darkened(0.35f),
                Roughness = 0.9f
            };
            var scarMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.12f * scale, 0.01f * scale) };

            // Diagonal slash scar across chest
            var chestScar1 = new MeshInstance3D { Mesh = scarMesh, MaterialOverride = scarMat };
            chestScar1.Position = new Vector3(0.08f * scale, 0.72f * scale, 0.28f * scale);
            chestScar1.RotationDegrees = new Vector3(0, 0, 35);
            parent.AddChild(chestScar1);

            var chestScar2 = new MeshInstance3D { Mesh = scarMesh, MaterialOverride = scarMat };
            chestScar2.Position = new Vector3(0.14f * scale, 0.66f * scale, 0.28f * scale);
            chestScar2.RotationDegrees = new Vector3(0, 0, 35);
            parent.AddChild(chestScar2);

            // Face scar (visible through helmet)
            var faceScar = new MeshInstance3D { Mesh = scarMesh, MaterialOverride = scarMat };
            faceScar.Position = new Vector3(-0.04f * scale, 1.1f * scale, 0.28f * scale);
            faceScar.RotationDegrees = new Vector3(0, 0, -25);
            faceScar.Scale = new Vector3(0.8f, 0.6f, 1f);
            parent.AddChild(faceScar);
        }

        // Chest armor plate
        var chestPlate = new MeshInstance3D();
        var plateMesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.5f * scale, 0.08f * scale) };
        chestPlate.Mesh = plateMesh;
        chestPlate.MaterialOverride = armorMat;
        chestPlate.Position = new Vector3(0, 0.65f * scale, 0.2f * scale);
        parent.AddChild(chestPlate);

        // War trophies (skull tokens on belt/chest)
        if (lod >= LODLevel.Medium)
        {
            var boneMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.85f, 0.82f, 0.75f),
                Roughness = 0.8f
            };
            var skullMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = 10, Rings = 6 };

            // Skulls hanging from belt
            for (int s = 0; s < 3; s++)
            {
                var skull = new MeshInstance3D { Mesh = skullMesh, MaterialOverride = boneMat };
                skull.Position = new Vector3((-0.12f + s * 0.12f) * scale, 0.38f * scale, 0.2f * scale);
                skull.Scale = new Vector3(0.8f, 1f, 0.7f);
                parent.AddChild(skull);

                // Eye sockets (dark holes)
                var socketMesh = new SphereMesh { Radius = 0.008f * scale, Height = 0.016f * scale };
                var socketMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
                var leftSocket = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = socketMat };
                leftSocket.Position = new Vector3((-0.13f + s * 0.12f) * scale, 0.39f * scale, 0.22f * scale);
                parent.AddChild(leftSocket);
                var rightSocket = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = socketMat };
                rightSocket.Position = new Vector3((-0.11f + s * 0.12f) * scale, 0.39f * scale, 0.22f * scale);
                parent.AddChild(rightSocket);
            }

            // Tooth necklace
            var toothMesh = new CylinderMesh {
                TopRadius = 0.004f * scale,
                BottomRadius = 0.01f * scale,
                Height = 0.03f * scale
            };
            for (int t = 0; t < 6; t++)
            {
                float tAngle = (-0.4f + t * 0.16f);
                var tooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = boneMat };
                tooth.Position = new Vector3(tAngle * 0.5f * scale, 0.88f * scale, 0.28f * scale);
                tooth.RotationDegrees = new Vector3(180, 0, tAngle * 20);
                parent.AddChild(tooth);
            }
        }

        // Shoulder pauldrons
        var leftPauldron = new MeshInstance3D();
        var pauldronMesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        leftPauldron.Mesh = pauldronMesh;
        leftPauldron.MaterialOverride = armorMat;
        leftPauldron.Position = new Vector3(-0.35f * scale, 0.75f * scale, 0.05f * scale);
        leftPauldron.Scale = new Vector3(1f, 0.6f, 1f);
        parent.AddChild(leftPauldron);

        var rightPauldron = new MeshInstance3D { Mesh = pauldronMesh, MaterialOverride = armorMat };
        rightPauldron.Position = new Vector3(0.35f * scale, 0.75f * scale, 0.05f * scale);
        rightPauldron.Scale = new Vector3(1f, 0.6f, 1f);
        parent.AddChild(rightPauldron);

        // Gold trim on pauldrons (High LOD only)
        if (lod == LODLevel.High)
        {
            var leftTrim = new MeshInstance3D();
            var trimMesh = new TorusMesh { InnerRadius = 0.12f * scale, OuterRadius = 0.14f * scale };
            leftTrim.Mesh = trimMesh;
            leftTrim.MaterialOverride = goldMat;
            leftTrim.Position = new Vector3(-0.35f * scale, 0.75f * scale, 0.05f * scale);
            leftTrim.Scale = new Vector3(1f, 0.4f, 1f);
            parent.AddChild(leftTrim);

            var rightTrim = new MeshInstance3D { Mesh = trimMesh, MaterialOverride = goldMat };
            rightTrim.Position = new Vector3(0.35f * scale, 0.75f * scale, 0.05f * scale);
            rightTrim.Scale = new Vector3(1f, 0.4f, 1f);
            parent.AddChild(rightTrim);
        }

        // === NECK JOINT ===
        float neckRadius = 0.1f * scale;
        float neckOverlap = CalculateOverlap(neckRadius);
        var neckJoint = CreateJointMesh(skinMat, neckRadius, radialSegs / 2);
        neckJoint.Position = new Vector3(0, bodyGeom.Top - neckOverlap * 0.5f, 0.05f * scale);
        parent.AddChild(neckJoint);

        // Large head with intimidating features - positioned with overlap
        float headCenterY = bodyGeom.Top + headHeight / 2f - neckOverlap;
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0.05f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = headRadius, Height = headHeight, RadialSegments = radialSegs, Rings = rings };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        head.Scale = new Vector3(1.08f, 0.9f, 0.96f);
        headNode.AddChild(head);

        // Heavy helmet with spike
        var helmet = new MeshInstance3D();
        var helmetMesh = new SphereMesh { Radius = 0.26f * scale, Height = 0.35f * scale, RadialSegments = radialSegs, Rings = rings / 2 };
        helmet.Mesh = helmetMesh;
        helmet.MaterialOverride = armorMat;
        helmet.Position = new Vector3(0, 0.1f * scale, 0);
        helmet.Scale = new Vector3(1.1f, 0.7f, 1f);
        headNode.AddChild(helmet);

        // Helmet spike (intimidation factor!)
        var spike = new MeshInstance3D();
        var spikeMesh = new CylinderMesh {
            TopRadius = 0.01f * scale,
            BottomRadius = 0.04f * scale,
            Height = 0.25f * scale,
            RadialSegments = Mathf.Max(8, radialSegs / 4)
        };
        spike.Mesh = spikeMesh;
        spike.MaterialOverride = armorMat;
        spike.Position = new Vector3(0, 0.35f * scale, 0);
        headNode.AddChild(spike);

        // Helmet face guard (intimidating grille)
        if (lod >= LODLevel.Medium)
        {
            var faceGuardMat = new StandardMaterial3D
            {
                AlbedoColor = armorColor.Darkened(0.1f),
                Metallic = 0.75f,
                Roughness = 0.35f
            };
            var barMesh = new CylinderMesh {
                TopRadius = 0.008f * scale,
                BottomRadius = 0.008f * scale,
                Height = 0.12f * scale,
                RadialSegments = 6
            };
            // Vertical bars on face guard
            for (int b = 0; b < 4; b++)
            {
                var bar = new MeshInstance3D { Mesh = barMesh, MaterialOverride = faceGuardMat };
                bar.Position = new Vector3((-0.06f + b * 0.04f) * scale, -0.08f * scale, 0.2f * scale);
                headNode.AddChild(bar);
            }
            // Horizontal brow guard
            var browMesh = new BoxMesh { Size = new Vector3(0.2f * scale, 0.02f * scale, 0.03f * scale) };
            var brow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = faceGuardMat };
            brow.Position = new Vector3(0, -0.02f * scale, 0.19f * scale);
            headNode.AddChild(brow);
        }

        // Side helmet horns
        var sideHornMesh = new CylinderMesh {
            TopRadius = 0.005f * scale,
            BottomRadius = 0.025f * scale,
            Height = 0.12f * scale
        };
        var leftHorn = new MeshInstance3D { Mesh = sideHornMesh, MaterialOverride = armorMat };
        leftHorn.Position = new Vector3(-0.2f * scale, 0.1f * scale, -0.02f * scale);
        leftHorn.RotationDegrees = new Vector3(25, 0, -45);
        headNode.AddChild(leftHorn);

        var rightHorn = new MeshInstance3D { Mesh = sideHornMesh, MaterialOverride = armorMat };
        rightHorn.Position = new Vector3(0.2f * scale, 0.1f * scale, -0.02f * scale);
        rightHorn.RotationDegrees = new Vector3(25, 0, 45);
        headNode.AddChild(rightHorn);

        // Menacing red eyes (glowing) - using standardized proportions
        var (warlordEyeR, _, warlordEyeY, warlordEyeX, warlordEyeZ, _) = CalculateEyeGeometry(headRadius, EyeProportions.Menacing, scale);
        int eyeSegs = lod == LODLevel.High ? 16 : (lod == LODLevel.Medium ? 12 : 8);
        var eyeMesh = new SphereMesh { Radius = warlordEyeR, Height = warlordEyeR * 2f, RadialSegments = eyeSegs, Rings = eyeSegs / 2 };

        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-warlordEyeX, warlordEyeY, warlordEyeZ);
        headNode.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(warlordEyeX, warlordEyeY, warlordEyeZ);
        headNode.AddChild(rightEye);

        // Large pointed ears
        if (lod >= LODLevel.Medium)
        {
            int earSegs = lod == LODLevel.High ? radialSegs / 2 : radialSegs / 4;
            var earMesh = new CylinderMesh {
                TopRadius = 0.015f * scale,
                BottomRadius = 0.07f * scale,
                Height = 0.28f * scale,
                RadialSegments = earSegs,
                Rings = Mathf.Max(4, earSegs / 2)
            };

            var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
            leftEar.Position = new Vector3(-0.24f * scale, -0.02f * scale, -0.05f * scale);
            leftEar.RotationDegrees = new Vector3(15, 25, -60);
            headNode.AddChild(leftEar);

            var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
            rightEar.Position = new Vector3(0.24f * scale, -0.02f * scale, -0.05f * scale);
            rightEar.RotationDegrees = new Vector3(15, -25, 60);
            headNode.AddChild(rightEar);
        }

        // === ARMS - positioned at shoulder height ===
        int armSegs = lod == LODLevel.High ? radialSegs / 2 : Mathf.Max(8, radialSegs / 4);
        float armRadius = 0.08f * scale;
        float shoulderY = bodyGeom.ShoulderY;
        float shoulderX = bodyGeom.EffectiveRadius(bodyScaleX) + armRadius * 0.5f;
        float shoulderJointRadius = armRadius * 0.6f;

        var upperArmMesh = new CapsuleMesh {
            Radius = armRadius,
            Height = 0.3f * scale,
            RadialSegments = armSegs,
            Rings = Mathf.Max(4, armSegs / 2)
        };
        var lowerArmMesh = new CapsuleMesh {
            Radius = 0.07f * scale,
            Height = 0.28f * scale,
            RadialSegments = armSegs,
            Rings = Mathf.Max(3, armSegs / 3)
        };

        // Right shoulder joint
        var rightShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius, armSegs);
        rightShoulderJoint.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightShoulderJoint);

        // Right arm with big sword
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0);
        rightArmNode.RotationDegrees = new Vector3(0, 0, 25);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = skinMat };
        rightUpperArm.Position = new Vector3(0, -0.12f * scale, 0);
        rightArmNode.AddChild(rightUpperArm);

        var rightLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = skinMat };
        rightLowerArm.Position = new Vector3(0, -0.35f * scale, 0.02f * scale);
        rightLowerArm.RotationDegrees = new Vector3(-20, 0, 0);
        rightArmNode.AddChild(rightLowerArm);

        // Hand attachment point for weapons
        // Weapon origin is at grip center, blade extends forward (+Z)
        // Warlord holds big sword ready for overhead swing
        var rightHandAttach = new Node3D();
        rightHandAttach.Name = "Hand";
        rightHandAttach.Position = new Vector3(0.02f * scale, -0.54f * scale, 0.10f * scale);
        // -35 X tilts blade up, -25 Z counters arm tilt
        rightHandAttach.RotationDegrees = new Vector3(-35f, 0f, -25f);
        rightArmNode.AddChild(rightHandAttach);

        // Massive sword (warlord weapon)
        var weaponNode = new Node3D();
        weaponNode.Position = new Vector3(0.1f * scale, -0.5f * scale, 0.08f * scale);
        rightArmNode.AddChild(weaponNode);
        limbs.Weapon = weaponNode;

        var swordHandle = new MeshInstance3D();
        var handleMesh = new CylinderMesh {
            TopRadius = 0.035f * scale,
            BottomRadius = 0.035f * scale,
            Height = 0.3f * scale,
            RadialSegments = Mathf.Max(8, armSegs)
        };
        swordHandle.Mesh = handleMesh;
        swordHandle.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.15f, 0.1f), Roughness = 0.8f };
        swordHandle.RotationDegrees = new Vector3(-85, 0, 0);
        weaponNode.AddChild(swordHandle);

        var swordGuard = new MeshInstance3D();
        var guardMesh = new BoxMesh { Size = new Vector3(0.25f * scale, 0.04f * scale, 0.04f * scale) };
        swordGuard.Mesh = guardMesh;
        swordGuard.MaterialOverride = goldMat;
        swordGuard.Position = new Vector3(0, 0.13f, -0.05f);
        swordGuard.RotationDegrees = new Vector3(-85, 0, 0);
        weaponNode.AddChild(swordGuard);

        var swordBlade = new MeshInstance3D();
        var bladeMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.8f * scale, 0.02f * scale) };
        swordBlade.Mesh = bladeMesh;
        swordBlade.MaterialOverride = armorMat;
        swordBlade.Position = new Vector3(0, 0.55f, -0.05f);
        swordBlade.RotationDegrees = new Vector3(-85, 0, 0);
        weaponNode.AddChild(swordBlade);

        // Blood notches on blade (High LOD only)
        if (lod == LODLevel.High)
        {
            for (int n = 0; n < 3; n++)
            {
                var notch = new MeshInstance3D();
                var notchMesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.03f * scale, 0.025f * scale) };
                notch.Mesh = notchMesh;
                notch.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.15f, 0.1f), Roughness = 0.6f };
                notch.Position = new Vector3(0.06f * scale, 0.3f + n * 0.2f, -0.05f);
                notch.RotationDegrees = new Vector3(-85, 0, 25);
                weaponNode.AddChild(notch);
            }
        }

        // Left shoulder joint
        var leftShoulderJoint = CreateJointMesh(skinMat, shoulderJointRadius, armSegs);
        leftShoulderJoint.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftShoulderJoint);

        // Left arm
        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -25);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = skinMat };
        leftUpperArm.Position = new Vector3(0, -0.12f * scale, 0);
        leftArmNode.AddChild(leftUpperArm);

        var leftLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = skinMat };
        leftLowerArm.Position = new Vector3(0, -0.35f * scale, 0.02f * scale);
        leftLowerArm.RotationDegrees = new Vector3(-20, 0, 0);
        leftArmNode.AddChild(leftLowerArm);

        // === LEGS - positioned at hip height ===
        int legSegs = lod == LODLevel.High ? radialSegs / 2 : Mathf.Max(8, radialSegs / 4);
        float legRadius = 0.1f * scale;
        float hipY = bodyGeom.HipY;
        float hipX = 0.15f * scale;
        float hipJointRadius = legRadius * 0.5f;

        var legMesh = new CapsuleMesh {
            Radius = legRadius,
            Height = 0.4f * scale,
            RadialSegments = legSegs,
            Rings = Mathf.Max(5, legSegs / 2)
        };

        // Left hip joint
        var leftHipJoint = CreateJointMesh(skinMat, hipJointRadius, legSegs);
        leftHipJoint.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftHipJoint);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        leftLegNode.AddChild(leftLeg);

        // Armored boots
        var leftBoot = new MeshInstance3D();
        var bootMesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.12f * scale, 0.22f * scale) };
        leftBoot.Mesh = bootMesh;
        leftBoot.MaterialOverride = armorMat;
        leftBoot.Position = new Vector3(0, -0.24f * scale, 0.06f * scale);
        leftLegNode.AddChild(leftBoot);

        // Right hip joint
        var rightHipJoint = CreateJointMesh(skinMat, hipJointRadius, legSegs);
        rightHipJoint.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightHipJoint);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = skinMat };
        rightLegNode.AddChild(rightLeg);

        var rightBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = armorMat };
        rightBoot.Position = new Vector3(0, -0.24f * scale, 0.06f * scale);
        rightLegNode.AddChild(rightBoot);
    }

    private static void CreateSlimeMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride)
    {
        Color slimeColor = skinColorOverride ?? new Color(0.3f, 0.8f, 0.4f);
        Color bubbleColor = slimeColor.Lightened(0.3f);
        Color highlightColor = slimeColor.Lightened(0.5f);

        var slimeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(slimeColor.R, slimeColor.G, slimeColor.B, 0.75f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.15f,
            Metallic = 0.15f,
            RimEnabled = true,
            Rim = 0.3f,
            RimTint = 0.2f
        };

        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(slimeColor.Darkened(0.35f).R, slimeColor.Darkened(0.35f).G, slimeColor.Darkened(0.35f).B, 0.9f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.25f
        };

        var bubbleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(bubbleColor.R, bubbleColor.G, bubbleColor.B, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.1f,
            Metallic = 0.2f
        };

        var highlightMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(highlightColor.R, highlightColor.G, highlightColor.B, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f
        };

        // Main body - slightly flattened blob with high poly count for organic smoothness
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.38f, Height = 0.76f, RadialSegments = 32, Rings = 24 };
        body.Mesh = bodyMesh;
        body.MaterialOverride = slimeMat;
        body.Position = new Vector3(0, 0.38f, 0);
        body.Scale = new Vector3(1.08f, 0.73f, 1.06f); // Slightly asymmetric for organic feel
        parent.AddChild(body);
        limbs.Head = body; // Use body for bobbing animation

        // Outer membrane layer for depth - extra smooth
        var membrane = new MeshInstance3D();
        var membraneMesh = new SphereMesh { Radius = 0.4f, Height = 0.8f, RadialSegments = 32, Rings = 24 };
        membrane.Mesh = membraneMesh;
        var membraneMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(slimeColor.R, slimeColor.G, slimeColor.B, 0.25f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Front, // Render inside
            Roughness = 0.08f,
            Metallic = 0.1f // Add slight sheen for realism
        };
        membrane.MaterialOverride = membraneMat;
        membrane.Position = new Vector3(0, 0.38f, 0);
        membrane.Scale = new Vector3(1.1f, 0.76f, 1.09f);
        parent.AddChild(membrane);

        // Core - nucleus inside the slime with smooth geometry
        var core = new MeshInstance3D();
        var coreMesh = new SphereMesh { Radius = 0.15f, Height = 0.3f, RadialSegments = 24, Rings = 18 };
        core.Mesh = coreMesh;
        core.MaterialOverride = coreMat;
        core.Position = new Vector3(0.01f, 0.33f, -0.01f); // Slightly off-center for organic feel
        core.Scale = new Vector3(1.02f, 0.88f, 0.98f);
        parent.AddChild(core);

        // Inner glow around core - brighter and more visible
        var glow = new MeshInstance3D();
        var glowMesh = new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 20, Rings = 16 };
        glow.Mesh = glowMesh;
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(slimeColor.R, slimeColor.G, slimeColor.B, 0.35f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = slimeColor * 0.25f, // Brighter emission
            BlendMode = BaseMaterial3D.BlendModeEnum.Add
        };
        glow.MaterialOverride = glowMat;
        glow.Position = new Vector3(0.01f, 0.33f, -0.01f);
        parent.AddChild(glow);

        // Bubbles floating inside - more varied and organic
        var bubbleMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 16, Rings = 12 };
        float[] bubbleX = { -0.15f, 0.18f, -0.08f, 0.14f, -0.22f, 0.06f, -0.11f, 0.09f };
        float[] bubbleY = { 0.45f, 0.38f, 0.28f, 0.52f, 0.35f, 0.24f, 0.48f, 0.31f };
        float[] bubbleZ = { 0.1f, -0.08f, 0.15f, 0.05f, -0.12f, -0.1f, 0.08f, -0.15f };
        float[] bubbleScale = { 1.1f, 0.75f, 0.55f, 0.85f, 0.65f, 0.45f, 0.9f, 0.5f };

        for (int i = 0; i < 8; i++) // More bubbles
        {
            var bubble = new MeshInstance3D { Mesh = bubbleMesh, MaterialOverride = bubbleMat };
            bubble.Position = new Vector3(bubbleX[i], bubbleY[i], bubbleZ[i]);
            bubble.Scale = Vector3.One * bubbleScale[i];
            parent.AddChild(bubble);
        }

        // Highlight/shine spots on surface
        var highlightMesh = new SphereMesh { Radius = 0.06f, Height = 0.12f };
        var highlight1 = new MeshInstance3D { Mesh = highlightMesh, MaterialOverride = highlightMat };
        highlight1.Position = new Vector3(-0.18f, 0.55f, 0.22f);
        highlight1.Scale = new Vector3(0.8f, 0.5f, 0.3f);
        parent.AddChild(highlight1);

        var highlight2 = new MeshInstance3D { Mesh = highlightMesh, MaterialOverride = highlightMat };
        highlight2.Position = new Vector3(0.15f, 0.5f, 0.18f);
        highlight2.Scale = new Vector3(0.5f, 0.3f, 0.2f);
        parent.AddChild(highlight2);

        // Surface wobble bulges - organic deformations for natural slime look
        var wobbleMesh = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 16, Rings = 12 };
        float[] wobbleAngles = { 35f, 105f, 180f, 250f, 320f };
        float[] wobbleY = { 0.28f, 0.42f, 0.32f, 0.45f, 0.35f };
        float[] wobbleScales = { 0.7f, 0.55f, 0.65f, 0.5f, 0.6f };
        for (int i = 0; i < 5; i++)
        {
            var wobble = new MeshInstance3D { Mesh = wobbleMesh, MaterialOverride = slimeMat };
            float wAngle = wobbleAngles[i] * Mathf.Pi / 180f;
            wobble.Position = new Vector3(Mathf.Cos(wAngle) * 0.32f, wobbleY[i], Mathf.Sin(wAngle) * 0.32f);
            wobble.Scale = new Vector3(wobbleScales[i], wobbleScales[i] * 0.7f, wobbleScales[i] * 0.8f);
            parent.AddChild(wobble);
        }

        // Internal vein/tendril structures visible through translucent body
        var veinMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(slimeColor.Darkened(0.25f).R, slimeColor.Darkened(0.25f).G, slimeColor.Darkened(0.25f).B, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.4f
        };
        var internalVeinMesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.015f, Height = 0.18f, RadialSegments = 8 };
        for (int i = 0; i < 4; i++)
        {
            var vein = new MeshInstance3D { Mesh = internalVeinMesh, MaterialOverride = veinMat };
            float vAngle = i * Mathf.Pi / 2f + 0.3f;
            vein.Position = new Vector3(Mathf.Cos(vAngle) * 0.12f, 0.28f + (i % 2) * 0.08f, Mathf.Sin(vAngle) * 0.12f);
            vein.RotationDegrees = new Vector3(25 - i * 8, vAngle * 57.3f, 15);
            parent.AddChild(vein);
        }

        // Drips/tendrils at base
        var dripMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.06f, Height = 0.12f };
        float[] dripAngles = { 0f, 72f, 144f, 216f, 288f };
        for (int i = 0; i < 5; i++)
        {
            var drip = new MeshInstance3D { Mesh = dripMesh, MaterialOverride = slimeMat };
            float angle = dripAngles[i] * Mathf.Pi / 180f;
            drip.Position = new Vector3(Mathf.Cos(angle) * 0.28f, 0.08f, Mathf.Sin(angle) * 0.28f);
            drip.RotationDegrees = new Vector3(0, 0, Mathf.Sin(angle * 2) * 15f);
            drip.Scale = new Vector3(1f, 0.6f + (i % 3) * 0.3f, 1f);
            parent.AddChild(drip);
        }

        // Eyes - using standardized Cute proportions for blob creature
        // Slime body radius is 0.38f, treat as "head" for eye proportions
        float slimeBodyRadius = 0.38f;
        var (slimeEyeR, slimePupilR, _, slimeEyeX, _, _) = CalculateEyeGeometry(slimeBodyRadius, EyeProportions.Cute);
        // Slime eyes are positioned higher on the body and further forward
        float slimeEyeY = 0.48f; // Fixed Y position on the blob
        float slimeEyeZ = slimeBodyRadius * 0.71f; // 71% forward
        float slimePupilZ = slimeEyeZ + slimeEyeR * 0.7f;

        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            EmissionEnabled = true,
            Emission = Colors.White * 0.3f,
            Roughness = 0.2f
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };

        var eyeMesh = new SphereMesh { Radius = slimeEyeR, Height = slimeEyeR * 2f, RadialSegments = 20, Rings = 16 };
        var pupilMesh = new SphereMesh { Radius = slimePupilR, Height = slimePupilR * 2f, RadialSegments = 16, Rings = 12 };

        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-slimeEyeX, slimeEyeY, slimeEyeZ);
        leftEye.Scale = new Vector3(0.98f, 1.12f, 0.82f); // Asymmetric for character
        parent.AddChild(leftEye);

        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-slimeEyeX, slimeEyeY, slimePupilZ);
        leftPupil.Scale = new Vector3(1.05f, 0.95f, 1f); // Slightly different
        parent.AddChild(leftPupil);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(slimeEyeX, slimeEyeY, slimeEyeZ);
        rightEye.Scale = new Vector3(1.02f, 1.08f, 0.8f); // Slightly different from left
        parent.AddChild(rightEye);

        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(slimeEyeX, slimeEyeY, slimePupilZ);
        rightPupil.Scale = new Vector3(0.95f, 1.05f, 1f);
        parent.AddChild(rightPupil);

        // Small mouth - Height = 2*Radius for proper sphere
        var mouthMesh = new SphereMesh { Radius = 0.03f, Height = 0.06f };
        var mouthMat = new StandardMaterial3D { AlbedoColor = slimeColor.Darkened(0.5f) };
        var mouth = new MeshInstance3D { Mesh = mouthMesh, MaterialOverride = mouthMat };
        mouth.Position = new Vector3(0, 0.38f, 0.35f);
        mouth.Scale = new Vector3(1.5f, 0.8f, 0.5f);
        parent.AddChild(mouth);
    }

    private static void CreateSkeletonMesh(Node3D parent, LimbNodes limbs, LODLevel lod = LODLevel.High, float sizeVariation = 1.0f, Color? boneColorOverride = null, float damageLevel = 0.0f)
    {
        // Variation parameters for aged/damaged bones
        float yellowFactor = Mathf.Clamp(damageLevel + (GD.Randf() * 0.2f), 0f, 1f);
        Color baseBoneColor = boneColorOverride ?? new Color(0.9f, 0.88f, 0.8f);
        Color boneColor = baseBoneColor.Lerp(new Color(0.85f, 0.82f, 0.65f), yellowFactor);
        Color darkBone = boneColor.Darkened(0.15f);
        Color toothColor = new Color(0.95f, 0.92f, 0.85f);

        // LOD-based segment counts
        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var boneMat = new StandardMaterial3D { AlbedoColor = boneColor, Roughness = 0.85f };
        var darkBoneMat = new StandardMaterial3D { AlbedoColor = darkBone, Roughness = 0.9f };
        var toothMat = new StandardMaterial3D { AlbedoColor = toothColor, Roughness = 0.7f };

        // Soul fire in eye sockets - red/orange flames
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.2f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f) * 0.6f
        };

        float scale = sizeVariation;

        // === SKELETON BODY GEOMETRY ===
        // Build skeleton from pelvis up to ensure proper connections
        // Pelvis center at Y=0.65, spine builds upward, neck builds upward, head on top

        // Pelvis geometry
        float pelvisCenterY = 0.65f * scale;
        float pelvisHeight = 0.24f * scale * 0.5f; // scaleY = 0.5
        float pelvisTop = pelvisCenterY + pelvisHeight / 2f;

        // Spine geometry (5 vertebrae, each 0.08 height with 15% overlap)
        float spineVertHeight = 0.08f * scale;
        float spineOverlap = CalculateOverlap(spineVertHeight, 0.15f);
        float spineSegmentStep = spineVertHeight - spineOverlap;
        float spineBaseY = pelvisTop + spineVertHeight / 2f; // First vertebra center
        float spineTopY = spineBaseY + 4 * spineSegmentStep; // Top of 5th vertebra

        // Neck geometry (3 vertebrae, each 0.06 height with 15% overlap)
        float neckVertHeight = 0.06f * scale;
        float neckOverlap = CalculateOverlap(neckVertHeight, 0.15f);
        float neckSegmentStep = neckVertHeight - neckOverlap;
        float neckBaseY = spineTopY + spineVertHeight / 2f + neckVertHeight / 2f - neckOverlap; // Connect to spine top
        float neckTopY = neckBaseY + 2 * neckSegmentStep + neckVertHeight / 2f;

        // Skull geometry - position so bottom overlaps into neck top
        float skullRadius = 0.17f * scale;
        float skullHeight = 0.34f * scale;
        float skullOverlap = CalculateOverlap(0.025f * scale, 0.25f); // Overlap with neck top radius
        float skullCenterY = neckTopY + skullHeight / 2f - skullOverlap;

        // Skull
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, skullCenterY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Main skull
        var skull = new MeshInstance3D();
        var skullMesh = new SphereMesh { Radius = 0.17f, Height = 0.34f };
        skull.Mesh = skullMesh;
        skull.MaterialOverride = boneMat;
        skull.Scale = new Vector3(0.88f, 1f, 0.82f);
        headNode.AddChild(skull);

        // Brow ridge
        var browMesh = new BoxMesh { Size = new Vector3(0.16f, 0.03f, 0.06f) };
        var brow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = boneMat };
        brow.Position = new Vector3(0, 0.06f, 0.1f);
        headNode.AddChild(brow);

        // Cheekbones
        var cheekMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        var leftCheek = new MeshInstance3D { Mesh = cheekMesh, MaterialOverride = boneMat };
        leftCheek.Position = new Vector3(-0.1f, -0.02f, 0.1f);
        leftCheek.Scale = new Vector3(0.8f, 0.6f, 0.5f);
        headNode.AddChild(leftCheek);
        var rightCheek = new MeshInstance3D { Mesh = cheekMesh, MaterialOverride = boneMat };
        rightCheek.Position = new Vector3(0.1f, -0.02f, 0.1f);
        rightCheek.Scale = new Vector3(0.8f, 0.6f, 0.5f);
        headNode.AddChild(rightCheek);

        // Eye sockets (dark recesses)
        var socketMesh = new SphereMesh { Radius = 0.035f, Height = 0.07f };
        var socketMat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.08f, 0.08f) };
        var leftSocketDark = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = socketMat };
        leftSocketDark.Position = new Vector3(-0.055f, 0.02f, 0.11f);
        headNode.AddChild(leftSocketDark);
        var rightSocketDark = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = socketMat };
        rightSocketDark.Position = new Vector3(0.055f, 0.02f, 0.11f);
        headNode.AddChild(rightSocketDark);

        // Glowing eye flames
        var flameMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f };
        var leftFlame = new MeshInstance3D { Mesh = flameMesh, MaterialOverride = eyeMat };
        leftFlame.Position = new Vector3(-0.055f, 0.025f, 0.12f);
        headNode.AddChild(leftFlame);
        var rightFlame = new MeshInstance3D { Mesh = flameMesh, MaterialOverride = eyeMat };
        rightFlame.Position = new Vector3(0.055f, 0.025f, 0.12f);
        headNode.AddChild(rightFlame);

        // Nasal cavity
        var nasalMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.025f, Height = 0.04f };
        var nasal = new MeshInstance3D { Mesh = nasalMesh, MaterialOverride = socketMat };
        nasal.Position = new Vector3(0, -0.02f, 0.14f);
        nasal.RotationDegrees = new Vector3(-80, 0, 0);
        headNode.AddChild(nasal);

        // Upper jaw with teeth
        var upperJawMesh = new BoxMesh { Size = new Vector3(0.1f, 0.025f, 0.06f) };
        var upperJaw = new MeshInstance3D { Mesh = upperJawMesh, MaterialOverride = boneMat };
        upperJaw.Position = new Vector3(0, -0.08f, 0.08f);
        headNode.AddChild(upperJaw);

        // Upper teeth
        var toothMesh = new BoxMesh { Size = new Vector3(0.012f, 0.025f, 0.015f) };
        for (int i = 0; i < 6; i++)
        {
            var tooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
            tooth.Position = new Vector3(-0.03f + i * 0.012f, -0.1f, 0.085f);
            headNode.AddChild(tooth);
        }

        // Lower jaw (mandible)
        var jawNode = new Node3D();
        jawNode.Position = new Vector3(0, -0.12f, 0.04f);
        headNode.AddChild(jawNode);

        var jawMesh = new BoxMesh { Size = new Vector3(0.1f, 0.03f, 0.07f) };
        var jaw = new MeshInstance3D { Mesh = jawMesh, MaterialOverride = boneMat };
        jaw.Position = new Vector3(0, 0, 0.02f);
        jawNode.AddChild(jaw);

        // Jaw corners (ramus)
        var ramusMesh = new BoxMesh { Size = new Vector3(0.02f, 0.06f, 0.03f) };
        var leftRamus = new MeshInstance3D { Mesh = ramusMesh, MaterialOverride = boneMat };
        leftRamus.Position = new Vector3(-0.055f, 0.03f, -0.01f);
        jawNode.AddChild(leftRamus);
        var rightRamus = new MeshInstance3D { Mesh = ramusMesh, MaterialOverride = boneMat };
        rightRamus.Position = new Vector3(0.055f, 0.03f, -0.01f);
        jawNode.AddChild(rightRamus);

        // Lower teeth
        for (int i = 0; i < 6; i++)
        {
            var tooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
            tooth.Position = new Vector3(-0.03f + i * 0.012f, 0.018f, 0.045f);
            jawNode.AddChild(tooth);
        }

        // === NECK VERTEBRAE - positioned from calculated neckBaseY upward ===
        var vertebraMesh = new CylinderMesh { TopRadius = 0.025f * scale, BottomRadius = 0.03f * scale, Height = neckVertHeight };
        for (int i = 0; i < 3; i++)
        {
            var vert = new MeshInstance3D { Mesh = vertebraMesh, MaterialOverride = boneMat };
            // Position from top down (i=0 is closest to skull)
            float vertY = neckBaseY + (2 - i) * neckSegmentStep;
            vert.Position = new Vector3(0, vertY, -0.02f * scale);
            parent.AddChild(vert);
        }

        // === SPINE VERTEBRAE - positioned from calculated spineBaseY upward ===
        var spineVertMesh = new CylinderMesh { TopRadius = 0.028f * scale, BottomRadius = 0.032f * scale, Height = spineVertHeight };
        for (int i = 0; i < 5; i++)
        {
            var vert = new MeshInstance3D { Mesh = spineVertMesh, MaterialOverride = boneMat };
            // Position from top down (i=0 is closest to neck)
            float vertY = spineBaseY + (4 - i) * spineSegmentStep;
            vert.Position = new Vector3(0, vertY, 0);
            parent.AddChild(vert);

            // Spinous process (back spike)
            var processMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.01f * scale, Height = 0.04f * scale };
            var process = new MeshInstance3D { Mesh = processMesh, MaterialOverride = boneMat };
            process.Position = new Vector3(0, vertY, -0.04f * scale);
            process.RotationDegrees = new Vector3(-30, 0, 0);
            parent.AddChild(process);
        }

        // === RIBS - attached to spine vertebrae ===
        for (int i = 0; i < 5; i++)
        {
            // Position ribs at each spine vertebra level
            float vertY = spineBaseY + (4 - i) * spineSegmentStep;
            float ribLength = (0.18f - i * 0.015f) * scale;
            var ribMesh = new CylinderMesh { TopRadius = 0.012f * scale, BottomRadius = 0.014f * scale, Height = ribLength };

            // Left ribs
            var leftRib1 = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            leftRib1.Position = new Vector3(-0.08f * scale, vertY, 0.04f * scale);
            leftRib1.RotationDegrees = new Vector3(15, 0, -50);
            parent.AddChild(leftRib1);

            var leftRib2 = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            leftRib2.Position = new Vector3(-0.16f * scale, vertY - 0.04f * scale, 0.06f * scale);
            leftRib2.RotationDegrees = new Vector3(10, 20, -30);
            parent.AddChild(leftRib2);

            // Right ribs
            var rightRib1 = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            rightRib1.Position = new Vector3(0.08f * scale, vertY, 0.04f * scale);
            rightRib1.RotationDegrees = new Vector3(15, 0, 50);
            parent.AddChild(rightRib1);

            var rightRib2 = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            rightRib2.Position = new Vector3(0.16f * scale, vertY - 0.04f * scale, 0.06f * scale);
            rightRib2.RotationDegrees = new Vector3(10, -20, 30);
            parent.AddChild(rightRib2);
        }

        // === STERNUM - centered on ribcage ===
        float sternumCenterY = spineBaseY + 2 * spineSegmentStep; // Middle of spine
        var sternumMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.25f * scale, 0.02f * scale) };
        var sternum = new MeshInstance3D { Mesh = sternumMesh, MaterialOverride = boneMat };
        sternum.Position = new Vector3(0, sternumCenterY, 0.12f * scale);
        parent.AddChild(sternum);

        // === PELVIS - positioned at calculated pelvisCenterY ===
        var pelvisMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.24f * scale };
        var pelvis = new MeshInstance3D { Mesh = pelvisMesh, MaterialOverride = boneMat };
        pelvis.Position = new Vector3(0, pelvisCenterY, 0);
        pelvis.Scale = new Vector3(1.1f, 0.5f, 0.6f);
        parent.AddChild(pelvis);

        // Hip bones - connect pelvis to leg attachments
        float hipY = pelvisCenterY - pelvisHeight * 0.2f;
        var hipMesh = new SphereMesh { Radius = 0.06f * scale, Height = 0.12f * scale };
        var leftHip = new MeshInstance3D { Mesh = hipMesh, MaterialOverride = boneMat };
        leftHip.Position = new Vector3(-0.1f * scale, hipY, 0.02f * scale);
        leftHip.Scale = new Vector3(0.8f, 1.2f, 0.6f);
        parent.AddChild(leftHip);
        var rightHip = new MeshInstance3D { Mesh = hipMesh, MaterialOverride = boneMat };
        rightHip.Position = new Vector3(0.1f * scale, hipY, 0.02f * scale);
        rightHip.Scale = new Vector3(0.8f, 1.2f, 0.6f);
        parent.AddChild(rightHip);

        // Calculate shoulder position from spine geometry
        float shoulderY = spineTopY; // Shoulders at top of spine

        // Arms - pass calculated positions
        CreateSkeletonArm(parent, limbs, boneMat, toothMat, true, shoulderY, scale);
        CreateSkeletonArm(parent, limbs, boneMat, toothMat, false, shoulderY, scale);

        // Legs - pass calculated hip position
        CreateSkeletonLeg(parent, limbs, boneMat, true, hipY, scale);
        CreateSkeletonLeg(parent, limbs, boneMat, false, hipY, scale);
    }

    private static void CreateSkeletonArm(Node3D parent, LimbNodes limbs, StandardMaterial3D material, StandardMaterial3D fingerMat, bool isLeft, float shoulderY, float scale = 1.0f)
    {
        float side = isLeft ? -1 : 1;

        // Add shoulder joint sphere
        float shoulderJointRadius = 0.03f * scale;
        var shoulderJoint = CreateJointMesh(material, shoulderJointRadius);
        shoulderJoint.Position = new Vector3(side * 0.18f * scale, shoulderY, 0);
        parent.AddChild(shoulderJoint);

        var armNode = new Node3D();
        armNode.Position = new Vector3(side * 0.18f * scale, shoulderY, 0);
        parent.AddChild(armNode);

        if (isLeft) limbs.LeftArm = armNode;
        else limbs.RightArm = armNode;

        // Shoulder joint
        var shoulderMesh = new SphereMesh { Radius = 0.03f, Height = 0.06f };
        var shoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = material };
        armNode.AddChild(shoulder);

        // Humerus (upper arm)
        var humerusMesh = new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.018f, Height = 0.22f };
        var humerus = new MeshInstance3D { Mesh = humerusMesh, MaterialOverride = material };
        humerus.Position = new Vector3(0, -0.12f, 0);
        armNode.AddChild(humerus);

        // Elbow joint
        var elbowMesh = new SphereMesh { Radius = 0.022f, Height = 0.044f };
        var elbow = new MeshInstance3D { Mesh = elbowMesh, MaterialOverride = material };
        elbow.Position = new Vector3(0, -0.24f, 0);
        armNode.AddChild(elbow);

        // Radius and Ulna (forearm - two bones)
        var forearmMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.012f, Height = 0.2f };
        var radius = new MeshInstance3D { Mesh = forearmMesh, MaterialOverride = material };
        radius.Position = new Vector3(side * 0.01f, -0.36f, 0);
        armNode.AddChild(radius);
        var ulna = new MeshInstance3D { Mesh = forearmMesh, MaterialOverride = material };
        ulna.Position = new Vector3(side * -0.01f, -0.36f, 0.01f);
        armNode.AddChild(ulna);

        // Wrist bones
        var wristMesh = new BoxMesh { Size = new Vector3(0.035f, 0.02f, 0.025f) };
        var wrist = new MeshInstance3D { Mesh = wristMesh, MaterialOverride = material };
        wrist.Position = new Vector3(0, -0.47f, 0);
        armNode.AddChild(wrist);

        // Hand metacarpals
        var metacarpalMesh = new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.025f) };
        var palm = new MeshInstance3D { Mesh = metacarpalMesh, MaterialOverride = material };
        palm.Position = new Vector3(0, -0.51f, 0);
        armNode.AddChild(palm);

        // Finger bones (phalanges) - 4 fingers
        var phalanxMesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.008f, Height = 0.04f };
        var tipMesh = new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.006f, Height = 0.025f };
        for (int f = 0; f < 4; f++)
        {
            float fx = -0.015f + f * 0.01f;
            // Proximal phalanx
            var prox = new MeshInstance3D { Mesh = phalanxMesh, MaterialOverride = fingerMat };
            prox.Position = new Vector3(fx, -0.55f, 0.01f);
            prox.RotationDegrees = new Vector3(-20, 0, 0);
            armNode.AddChild(prox);
            // Distal phalanx
            var dist = new MeshInstance3D { Mesh = tipMesh, MaterialOverride = fingerMat };
            dist.Position = new Vector3(fx, -0.58f, 0.025f);
            dist.RotationDegrees = new Vector3(-35, 0, 0);
            armNode.AddChild(dist);
        }

        // Thumb
        var thumbMesh = new CylinderMesh { TopRadius = 0.007f, BottomRadius = 0.009f, Height = 0.035f };
        var thumb = new MeshInstance3D { Mesh = thumbMesh, MaterialOverride = fingerMat };
        thumb.Position = new Vector3(side * 0.025f, -0.52f, 0.015f);
        thumb.RotationDegrees = new Vector3(-30, side * 40, 0);
        armNode.AddChild(thumb);

        // Hand attachment point for weapons
        // Weapon origin is at grip center, blade extends forward (+Z)
        // Skeleton holds weapon ready for slashing swing
        var handAttach = new Node3D();
        handAttach.Name = "Hand";
        handAttach.Position = new Vector3(0, -0.54f * scale, 0.05f * scale);
        // Tilt blade slightly up for natural sword pose
        handAttach.RotationDegrees = new Vector3(-25f, 0f, 0f);
        armNode.AddChild(handAttach);
    }

    private static void CreateSkeletonLeg(Node3D parent, LimbNodes limbs, StandardMaterial3D material, bool isLeft, float hipY, float scale = 1.0f)
    {
        float side = isLeft ? -1 : 1;

        // Add hip joint sphere at calculated position
        float hipJointRadius = 0.035f * scale;
        var hipJointSphere = CreateJointMesh(material, hipJointRadius);
        hipJointSphere.Position = new Vector3(side * 0.08f * scale, hipY, 0);
        parent.AddChild(hipJointSphere);

        var legNode = new Node3D();
        legNode.Position = new Vector3(side * 0.08f * scale, hipY, 0);
        parent.AddChild(legNode);

        if (isLeft) limbs.LeftLeg = legNode;
        else limbs.RightLeg = legNode;

        // Hip joint (visual detail)
        var hipJointMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale };
        var hipJoint = new MeshInstance3D { Mesh = hipJointMesh, MaterialOverride = material };
        legNode.AddChild(hipJoint);

        // Femur (thigh bone)
        var femurMesh = new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.022f, Height = 0.28f };
        var femur = new MeshInstance3D { Mesh = femurMesh, MaterialOverride = material };
        femur.Position = new Vector3(0, -0.15f, 0);
        legNode.AddChild(femur);

        // Knee cap (patella)
        var kneeMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f };
        var knee = new MeshInstance3D { Mesh = kneeMesh, MaterialOverride = material };
        knee.Position = new Vector3(0, -0.3f, 0.02f);
        knee.Scale = new Vector3(0.8f, 0.6f, 0.5f);
        legNode.AddChild(knee);

        // Tibia and Fibula (lower leg)
        var tibiaMesh = new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.018f, Height = 0.26f };
        var tibia = new MeshInstance3D { Mesh = tibiaMesh, MaterialOverride = material };
        tibia.Position = new Vector3(0, -0.45f, 0);
        legNode.AddChild(tibia);

        var fibulaMesh = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.01f, Height = 0.24f };
        var fibula = new MeshInstance3D { Mesh = fibulaMesh, MaterialOverride = material };
        fibula.Position = new Vector3(side * 0.02f, -0.44f, -0.01f);
        legNode.AddChild(fibula);

        // Ankle bones
        var ankleMesh = new SphereMesh { Radius = 0.02f, Height = 0.04f };
        var ankle = new MeshInstance3D { Mesh = ankleMesh, MaterialOverride = material };
        ankle.Position = new Vector3(0, -0.58f, 0);
        legNode.AddChild(ankle);

        // Foot bones (tarsals + metatarsals)
        var footMesh = new BoxMesh { Size = new Vector3(0.05f, 0.02f, 0.1f) };
        var foot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = material };
        foot.Position = new Vector3(0, -0.6f, 0.04f);
        legNode.AddChild(foot);

        // Toe bones
        var toeMesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.008f, Height = 0.03f };
        for (int t = 0; t < 4; t++)
        {
            var toe = new MeshInstance3D { Mesh = toeMesh, MaterialOverride = material };
            toe.Position = new Vector3(-0.015f + t * 0.01f, -0.6f, 0.1f);
            toe.RotationDegrees = new Vector3(-80, 0, 0);
            legNode.AddChild(toe);
        }
    }

    /// <summary>
    /// Create enhanced wolf mesh with LOD support, variation, and animation-ready structure.
    /// Features realistic wolf anatomy with digitigrade legs, elongated snout, and bushy tail.
    /// </summary>
    private static void CreateWolfMesh(Node3D parent, LimbNodes limbs, LODLevel lod = LODLevel.High, float sizeVariation = 1.0f, Color? furColorOverride = null)
    {
        // Variation parameters
        float earSizeVariation = 0.9f + (GD.Randf() * 0.2f); // 0.9-1.1
        float tailBushiness = 0.85f + (GD.Randf() * 0.3f);   // 0.85-1.15

        // Fur color variations (gray-brown wolf)
        Color baseFurColor = furColorOverride ?? new Color(0.5f, 0.45f, 0.4f);
        Color furColor = baseFurColor;
        Color darkFur = furColor.Darkened(0.25f);
        Color lightFur = furColor.Lightened(0.15f);
        Color toothColor = new Color(0.95f, 0.93f, 0.88f);
        Color clawColor = new Color(0.2f, 0.18f, 0.15f);

        // LOD-based segment counts
        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        // Apply size variation
        float scale = sizeVariation;

        var furMat = new StandardMaterial3D { AlbedoColor = baseFurColor, Roughness = 0.95f };
        var darkFurMat = new StandardMaterial3D { AlbedoColor = darkFur, Roughness = 0.9f };
        var lightFurMat = new StandardMaterial3D { AlbedoColor = lightFur, Roughness = 0.95f };
        var noseMat = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.1f, 0.08f), Roughness = 0.6f, Metallic = 0.2f }; // Wet nose
        var toothMat = new StandardMaterial3D { AlbedoColor = toothColor, Roughness = 0.7f };
        var clawMat = new StandardMaterial3D { AlbedoColor = clawColor, Roughness = 0.8f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.75f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.7f, 0.1f) * 0.4f
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };

        // === BODY GEOMETRY (horizontal quadruped) ===
        // For a horizontal wolf, the body capsule is rotated 90° on Z axis
        // Body "height" becomes body length along Z axis when rotated
        float bodyRadius = 0.23f * scale;
        float bodyLength = 0.75f * scale;  // This becomes Z-length when rotated
        float bodyCenterY = 0.48f * scale;
        float bodyFrontZ = bodyLength / 2f;  // Front of body in Z
        float bodyBackZ = -bodyLength / 2f;  // Back of body in Z
        float bodyTopY = bodyCenterY + bodyRadius;  // Top of body (for shoulder hump)

        // Head geometry
        float headRadius = 0.16f * scale;
        float headHeight = 0.32f * scale;

        // ===== BODY - Realistic wolf anatomy =====

        // Main body - elongated horizontal capsule tapering from chest to hindquarters
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = bodyRadius, Height = bodyLength, RadialSegments = radialSegs, Rings = rings };
        body.Mesh = bodyMesh;
        body.MaterialOverride = furMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(body);

        // Chest/underbelly (lighter colored) - barrel chest
        var chest = new MeshInstance3D();
        var chestMesh = new CapsuleMesh { Radius = 0.15f * scale, Height = 0.4f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        chest.Mesh = chestMesh;
        chest.MaterialOverride = lightFurMat;
        chest.Position = new Vector3(0, 0.4f * scale, 0.08f * scale);
        chest.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(chest);

        // Muscular shoulder hump (wolves have prominent shoulders)
        var shoulderMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.24f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var shoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = furMat };
        shoulder.Position = new Vector3(0, 0.58f * scale, 0.15f * scale);
        shoulder.Scale = new Vector3(1.2f, 0.8f, 0.9f);
        parent.AddChild(shoulder);

        // Narrow waist (wolves taper from chest to hindquarters) - Medium+ LOD
        if (lod >= LODLevel.Medium)
        {
            var waistMesh = new CapsuleMesh { Radius = 0.12f * scale, Height = 0.25f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
            var waist = new MeshInstance3D { Mesh = waistMesh, MaterialOverride = furMat };
            waist.Position = new Vector3(0, 0.46f * scale, -0.25f * scale);
            waist.RotationDegrees = new Vector3(0, 0, 90);
            waist.Scale = new Vector3(0.85f, 1f, 0.9f); // Narrower than chest
            parent.AddChild(waist);
        }

        // === NECK JOINT ===
        float neckRadius = 0.1f * scale;
        float neckOverlap = CalculateOverlap(neckRadius);
        float neckAttachY = bodyCenterY + bodyRadius * 0.4f;  // Upper part of body
        float neckAttachZ = bodyFrontZ - neckOverlap;  // Front of body

        var neckJoint = CreateJointMesh(furMat, neckRadius, radialSegs / 2);
        neckJoint.Position = new Vector3(0, neckAttachY, neckAttachZ);
        parent.AddChild(neckJoint);

        // Muscular neck
        var neckMesh = new CylinderMesh { TopRadius = neckRadius, BottomRadius = 0.14f * scale, Height = 0.15f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var neck = new MeshInstance3D { Mesh = neckMesh, MaterialOverride = furMat };
        neck.Position = new Vector3(0, neckAttachY + 0.06f * scale, neckAttachZ + 0.1f * scale);
        neck.RotationDegrees = new Vector3(-50, 0, 0);
        parent.AddChild(neck);

        // Neck/chest fur ruff - thick fur around neck and chest (wolf mane)
        var ruffMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.04f * scale, Height = 0.06f * scale, RadialSegments = 8 };
        for (int r = 0; r < 8; r++)
        {
            var ruff = new MeshInstance3D { Mesh = ruffMesh, MaterialOverride = darkFurMat };
            float rAngle = r * Mathf.Pi / 4f;
            float rZ = neckAttachZ + 0.05f * scale + Mathf.Cos(rAngle) * 0.08f * scale;
            float rX = Mathf.Sin(rAngle) * 0.1f * scale;
            ruff.Position = new Vector3(rX, neckAttachY + 0.02f * scale, rZ);
            ruff.RotationDegrees = new Vector3(-30 + Mathf.Cos(rAngle) * 15, rAngle * 57.3f, Mathf.Sin(rAngle) * 20);
            parent.AddChild(ruff);
        }

        // Head - positioned to connect to neck with overlap
        float headCenterZ = neckAttachZ + 0.2f * scale;  // Forward from neck
        float headCenterY = neckAttachY + 0.08f * scale;  // Slightly above neck
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, headCenterZ);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = headRadius, Height = headHeight };
        head.Mesh = headMesh;
        head.MaterialOverride = furMat;
        head.Scale = new Vector3(0.88f, 0.9f, 1.05f);
        headNode.AddChild(head);

        // Forehead fur tuft
        var tuftMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.08f, Height = 0.08f };
        var tuft = new MeshInstance3D { Mesh = tuftMesh, MaterialOverride = darkFurMat };
        tuft.Position = new Vector3(0, 0.12f, 0.02f);
        tuft.RotationDegrees = new Vector3(20, 0, 0);
        headNode.AddChild(tuft);

        // Cheek fur
        var cheekMesh = new SphereMesh { Radius = 0.06f, Height = 0.12f };
        var leftCheek = new MeshInstance3D { Mesh = cheekMesh, MaterialOverride = lightFurMat };
        leftCheek.Position = new Vector3(-0.1f, -0.02f, 0.08f);
        leftCheek.Scale = new Vector3(0.6f, 0.8f, 0.5f);
        headNode.AddChild(leftCheek);
        var rightCheek = new MeshInstance3D { Mesh = cheekMesh, MaterialOverride = lightFurMat };
        rightCheek.Position = new Vector3(0.1f, -0.02f, 0.08f);
        rightCheek.Scale = new Vector3(0.6f, 0.8f, 0.5f);
        headNode.AddChild(rightCheek);

        // Snout
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 0.16f };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = furMat;
        snout.Position = new Vector3(0, -0.04f, 0.16f);
        snout.RotationDegrees = new Vector3(-75, 0, 0);
        headNode.AddChild(snout);

        // Snout top (bridge of nose)
        var bridgeMesh = new BoxMesh { Size = new Vector3(0.06f, 0.03f, 0.12f) };
        var bridge = new MeshInstance3D { Mesh = bridgeMesh, MaterialOverride = darkFurMat };
        bridge.Position = new Vector3(0, 0.02f, 0.18f);
        headNode.AddChild(bridge);

        // Nose (wet, shiny)
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.035f, Height = 0.07f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, -0.06f, 0.24f);
        nose.Scale = new Vector3(1.1f, 0.9f, 0.8f);
        headNode.AddChild(nose);

        // Nostrils
        var nostrilMesh = new SphereMesh { Radius = 0.01f, Height = 0.02f };
        var nostrilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
        leftNostril.Position = new Vector3(-0.015f, -0.06f, 0.26f);
        headNode.AddChild(leftNostril);
        var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
        rightNostril.Position = new Vector3(0.015f, -0.06f, 0.26f);
        headNode.AddChild(rightNostril);

        // Upper jaw/lip
        var lipMesh = new BoxMesh { Size = new Vector3(0.08f, 0.02f, 0.06f) };
        var upperLip = new MeshInstance3D { Mesh = lipMesh, MaterialOverride = darkFurMat };
        upperLip.Position = new Vector3(0, -0.08f, 0.2f);
        headNode.AddChild(upperLip);

        // Fangs (upper canines)
        var fangMesh = new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.01f, Height = 0.045f };
        var leftFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = toothMat };
        leftFang.Position = new Vector3(-0.025f, -0.1f, 0.2f);
        leftFang.RotationDegrees = new Vector3(10, 0, 5);
        headNode.AddChild(leftFang);
        var rightFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = toothMat };
        rightFang.Position = new Vector3(0.025f, -0.1f, 0.2f);
        rightFang.RotationDegrees = new Vector3(10, 0, -5);
        headNode.AddChild(rightFang);

        // Lower jaw
        var jawMesh = new BoxMesh { Size = new Vector3(0.06f, 0.025f, 0.08f) };
        var jaw = new MeshInstance3D { Mesh = jawMesh, MaterialOverride = furMat };
        jaw.Position = new Vector3(0, -0.1f, 0.14f);
        headNode.AddChild(jaw);

        // Lower fangs
        var lowerFangMesh = new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.007f, Height = 0.025f };
        var leftLowerFang = new MeshInstance3D { Mesh = lowerFangMesh, MaterialOverride = toothMat };
        leftLowerFang.Position = new Vector3(-0.018f, -0.085f, 0.18f);
        leftLowerFang.RotationDegrees = new Vector3(-170, 0, 0);
        headNode.AddChild(leftLowerFang);
        var rightLowerFang = new MeshInstance3D { Mesh = lowerFangMesh, MaterialOverride = toothMat };
        rightLowerFang.Position = new Vector3(0.018f, -0.085f, 0.18f);
        rightLowerFang.RotationDegrees = new Vector3(-170, 0, 0);
        headNode.AddChild(rightLowerFang);

        // Eyes with pupils - using standardized Beast proportions
        CreateEyePair(headNode, headRadius, eyeMat, pupilMat, EyeProportions.Beast, lod, scale);

        // Brow ridge
        var browMesh = new BoxMesh { Size = new Vector3(0.05f, 0.02f, 0.03f) };
        var leftBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkFurMat };
        leftBrow.Position = new Vector3(-0.07f, 0.08f, 0.09f);
        leftBrow.RotationDegrees = new Vector3(0, 0, 10);
        headNode.AddChild(leftBrow);
        var rightBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkFurMat };
        rightBrow.Position = new Vector3(0.07f, 0.08f, 0.09f);
        rightBrow.RotationDegrees = new Vector3(0, 0, -10);
        headNode.AddChild(rightBrow);

        // Ears - pointed and alert
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.05f, Height = 0.12f };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        leftEar.Position = new Vector3(-0.09f, 0.16f, -0.02f);
        leftEar.RotationDegrees = new Vector3(10, 0, -20);
        headNode.AddChild(leftEar);
        var innerEarMesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.025f, Height = 0.06f };
        var leftInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = lightFurMat };
        leftInnerEar.Position = new Vector3(-0.085f, 0.14f, 0.01f);
        leftInnerEar.RotationDegrees = new Vector3(10, 0, -20);
        headNode.AddChild(leftInnerEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        rightEar.Position = new Vector3(0.09f, 0.16f, -0.02f);
        rightEar.RotationDegrees = new Vector3(10, 0, 20);
        headNode.AddChild(rightEar);
        var rightInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = lightFurMat };
        rightInnerEar.Position = new Vector3(0.085f, 0.14f, 0.01f);
        rightInnerEar.RotationDegrees = new Vector3(10, 0, 20);
        headNode.AddChild(rightInnerEar);

        // Tail - bushy, extends backward and curves up
        // CylinderMesh: TopRadius at +Y, BottomRadius at -Y
        // With 90°-50°=40° X rotation, +Y becomes backward-up direction
        // So TopRadius (narrow) should point away from body (toward tail tip)
        var tailBase = new MeshInstance3D();
        var tailBaseMesh = new CylinderMesh {
            TopRadius = 0.03f,      // Narrow end toward tail tip
            BottomRadius = 0.07f,   // Wide end toward body
            Height = 0.2f
        };
        tailBase.Mesh = tailBaseMesh;
        tailBase.MaterialOverride = furMat;
        tailBase.Position = new Vector3(0, 0.52f, -0.38f);
        // Rotate to point backward and slightly up (wolf tail curls up)
        tailBase.RotationDegrees = new Vector3(130, 0, 0);  // 90+40 = point backward-up
        parent.AddChild(tailBase);

        var tailTip = new MeshInstance3D();
        var tailTipMesh = new CylinderMesh {
            TopRadius = 0.015f,     // Narrow end (tail tip)
            BottomRadius = 0.04f,   // Wide end connects to base
            Height = 0.15f
        };
        tailTip.Mesh = tailTipMesh;
        tailTip.MaterialOverride = darkFurMat;
        tailTip.Position = new Vector3(0, 0.55f, -0.52f);
        tailTip.RotationDegrees = new Vector3(145, 0, 0);  // Continue curve upward
        parent.AddChild(tailTip);

        // Legs
        CreateWolfLeg(parent, limbs, furMat, lightFurMat, clawMat, true, true);   // Front left
        CreateWolfLeg(parent, limbs, furMat, lightFurMat, clawMat, false, true);  // Front right
        CreateWolfLeg(parent, limbs, furMat, lightFurMat, clawMat, true, false);  // Back left
        CreateWolfLeg(parent, limbs, furMat, lightFurMat, clawMat, false, false); // Back right
    }

    private static void CreateWolfLeg(Node3D parent, LimbNodes limbs, StandardMaterial3D furMat, StandardMaterial3D lightFurMat, StandardMaterial3D clawMat, bool isLeft, bool isFront)
    {
        float side = isLeft ? -1 : 1;
        float front = isFront ? 1 : -1;

        var legNode = new Node3D();
        legNode.Position = new Vector3(side * 0.14f, 0.25f, front * 0.18f);
        parent.AddChild(legNode);

        // Store front legs as arms for animation
        if (isFront)
        {
            if (isLeft) limbs.LeftArm = legNode;
            else limbs.RightArm = legNode;
        }
        else
        {
            if (isLeft) limbs.LeftLeg = legNode;
            else limbs.RightLeg = legNode;
        }

        // Upper leg
        var upperLegMesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.035f, Height = 0.15f };
        var upperLeg = new MeshInstance3D { Mesh = upperLegMesh, MaterialOverride = furMat };
        upperLeg.Position = new Vector3(0, -0.06f, 0);
        legNode.AddChild(upperLeg);

        // Lower leg
        var lowerLegMesh = new CylinderMesh { TopRadius = 0.032f, BottomRadius = 0.025f, Height = 0.12f };
        var lowerLeg = new MeshInstance3D { Mesh = lowerLegMesh, MaterialOverride = furMat };
        lowerLeg.Position = new Vector3(0, -0.18f, 0);
        legNode.AddChild(lowerLeg);

        // Paw
        var pawMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        var paw = new MeshInstance3D { Mesh = pawMesh, MaterialOverride = lightFurMat };
        paw.Position = new Vector3(0, -0.25f, 0.02f);
        paw.Scale = new Vector3(0.9f, 0.5f, 1.1f);
        legNode.AddChild(paw);

        // Paw pads - central large pad and 4 toe pads
        var padMat = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.2f, 0.18f), Roughness = 0.4f };
        var centralPadMesh = new SphereMesh { Radius = 0.018f, Height = 0.036f, RadialSegments = 10, Rings = 8 };
        var centralPad = new MeshInstance3D { Mesh = centralPadMesh, MaterialOverride = padMat };
        centralPad.Position = new Vector3(0, -0.27f, 0.015f);
        centralPad.Scale = new Vector3(1.2f, 0.4f, 1.4f);
        legNode.AddChild(centralPad);

        // Toe pads
        var toePadMesh = new SphereMesh { Radius = 0.008f, Height = 0.016f, RadialSegments = 8, Rings = 6 };
        for (int t = 0; t < 4; t++)
        {
            var toePad = new MeshInstance3D { Mesh = toePadMesh, MaterialOverride = padMat };
            toePad.Position = new Vector3(-0.015f + t * 0.01f, -0.27f, 0.038f);
            toePad.Scale = new Vector3(1f, 0.5f, 1.2f);
            legNode.AddChild(toePad);
        }

        // Fur tufts on upper leg
        var tuftMesh = new CylinderMesh { TopRadius = 0.002f, BottomRadius = 0.012f, Height = 0.03f, RadialSegments = 6 };
        for (int t = 0; t < 3; t++)
        {
            var tuft = new MeshInstance3D { Mesh = tuftMesh, MaterialOverride = furMat };
            float tAngle = (t - 1) * 0.4f;
            tuft.Position = new Vector3(side * 0.03f, -0.04f - t * 0.03f, 0.02f);
            tuft.RotationDegrees = new Vector3(15, 0, side * (20 + t * 5));
            legNode.AddChild(tuft);
        }

        // Claws
        var clawMesh = new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.008f, Height = 0.025f };
        for (int c = 0; c < 4; c++)
        {
            var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
            claw.Position = new Vector3(-0.018f + c * 0.012f, -0.27f, 0.045f);
            claw.RotationDegrees = new Vector3(-60, 0, 0);
            legNode.AddChild(claw);
        }
    }

    /// <summary>
    /// Creates an enhanced realistic Bat mesh with LOD support and variation parameters.
    /// Features membranous wings with finger bones, large ears, furry body.
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="limbs">Limb nodes for animation</param>
    /// <param name="lodLevel">Level of detail: 0=High (full detail), 1=Medium (simplified), 2=Low (silhouette)</param>
    /// <param name="sizeVariation">Overall size multiplier (0.8-1.2)</param>
    /// <param name="wingSpanVariation">Wing span multiplier (0.8-1.3)</param>
    /// <param name="furColorType">Fur color: 0=Brown, 1=Black, 2=Gray, 3=Dark Purple</param>
    private static void CreateBatMesh(Node3D parent, LimbNodes limbs, int lodLevel = 0, float sizeVariation = 1.0f, float wingSpanVariation = 1.0f, int furColorType = 0)
    {
        // Fur color variations
        Color bodyColor = furColorType switch
        {
            1 => new Color(0.12f, 0.10f, 0.12f), // Black
            2 => new Color(0.28f, 0.26f, 0.28f), // Gray
            3 => new Color(0.22f, 0.15f, 0.25f), // Dark Purple
            _ => new Color(0.22f, 0.18f, 0.22f)  // Brown (default)
        };
        Color wingColor = bodyColor.Darkened(0.2f);
        Color earInnerColor = new Color(0.4f, 0.25f, 0.3f);
        Color fangColor = new Color(0.95f, 0.93f, 0.88f);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.9f };
        var wingMat = new StandardMaterial3D
        {
            AlbedoColor = wingColor,
            Roughness = 0.6f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var earInnerMat = new StandardMaterial3D { AlbedoColor = earInnerColor, Roughness = 0.7f };
        var fangMat = new StandardMaterial3D { AlbedoColor = fangColor, Roughness = 0.5f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.25f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.1f) * 0.6f
        };

        // Apply size variation
        float baseSize = sizeVariation;
        float wingSpan = wingSpanVariation;

        // === BODY GEOMETRY ===
        float bodyRadius = 0.13f * baseSize;
        float bodyHeight = 0.26f * baseSize;
        float bodyCenterY = 0.78f * baseSize;
        float bodyScaleY = 1.15f;
        var bodyGeom = new BodyGeometry(bodyCenterY, bodyRadius, bodyHeight, bodyScaleY);

        // Head geometry
        float headRadius = 0.09f * baseSize;
        float headHeight = 0.18f * baseSize;

        // Body (small furry ball) - adjusted for size variation
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh
        {
            Radius = bodyRadius,
            Height = bodyHeight,
            RadialSegments = lodLevel == 2 ? 8 : (lodLevel == 1 ? 16 : 32),
            Rings = lodLevel == 2 ? 4 : (lodLevel == 1 ? 8 : 16)
        };
        body.Mesh = bodyMesh;
        body.MaterialOverride = bodyMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.Scale = new Vector3(0.95f, bodyScaleY, 0.85f);
        parent.AddChild(body);

        // Belly (lighter) - skip on low LOD
        if (lodLevel < 2)
        {
            var bellyMat = new StandardMaterial3D { AlbedoColor = bodyColor.Lightened(0.1f), Roughness = 0.9f };
            var bellyMesh = new SphereMesh
            {
                Radius = 0.08f * baseSize,
                Height = 0.16f * baseSize,
                RadialSegments = lodLevel == 1 ? 12 : 24
            };
            var belly = new MeshInstance3D { Mesh = bellyMesh, MaterialOverride = bellyMat };
            belly.Position = new Vector3(0, 0.72f * baseSize, 0.06f * baseSize);
            parent.AddChild(belly);
        }

        // Fur texture tufts on body - gives fuzzy bat appearance
        if (lodLevel == 0)
        {
            var furTuftMesh = new CylinderMesh { TopRadius = 0.002f * baseSize, BottomRadius = 0.008f * baseSize, Height = 0.025f * baseSize, RadialSegments = 6 };
            var furTuftMat = new StandardMaterial3D { AlbedoColor = bodyColor.Lightened(0.05f), Roughness = 0.95f };
            for (int f = 0; f < 12; f++)
            {
                var furTuft = new MeshInstance3D { Mesh = furTuftMesh, MaterialOverride = furTuftMat };
                float fAngle = f * Mathf.Pi / 6f;
                float fY = 0.75f + (f % 3) * 0.04f;
                furTuft.Position = new Vector3(
                    Mathf.Cos(fAngle) * 0.1f * baseSize,
                    fY * baseSize,
                    Mathf.Sin(fAngle) * 0.08f * baseSize
                );
                furTuft.RotationDegrees = new Vector3(Mathf.Cos(fAngle) * 30, fAngle * 57.3f, Mathf.Sin(fAngle) * 20);
                parent.AddChild(furTuft);
            }
        }

        // === NECK JOINT ===
        float neckRadius = 0.05f * baseSize;
        float neckOverlap = CalculateOverlap(neckRadius);
        int neckSegs = lodLevel == 2 ? 6 : (lodLevel == 1 ? 8 : 12);
        var neckJoint = CreateJointMesh(bodyMat, neckRadius, neckSegs);
        neckJoint.Position = new Vector3(0, bodyGeom.Top - neckOverlap * 0.5f, 0.04f * baseSize);
        parent.AddChild(neckJoint);

        // Head - animation-ready node, positioned with overlap
        float headCenterY = bodyGeom.Top + headHeight / 2f - neckOverlap;
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0.06f * baseSize);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh
        {
            Radius = headRadius,
            Height = headHeight,
            RadialSegments = lodLevel == 2 ? 8 : (lodLevel == 1 ? 16 : 32)
        };
        head.Mesh = headMesh;
        head.MaterialOverride = bodyMat;
        head.Scale = new Vector3(1f, 0.95f, 0.9f);
        headNode.AddChild(head);

        // Facial features - skip fine details on low LOD
        if (lodLevel < 2)
        {
            // Snout/nose
            var snoutMesh = new SphereMesh
            {
                Radius = 0.035f * baseSize,
                Height = 0.07f * baseSize,
                RadialSegments = lodLevel == 1 ? 8 : 16
            };
            var snout = new MeshInstance3D { Mesh = snoutMesh, MaterialOverride = bodyMat };
            snout.Position = new Vector3(0, -0.02f * baseSize, 0.07f * baseSize);
            snout.Scale = new Vector3(0.8f, 0.6f, 0.9f);
            headNode.AddChild(snout);

            // Nose leaf (bat has upturned nose) - high LOD only
            if (lodLevel == 0)
            {
                var noseLeafMesh = new CylinderMesh
                {
                    TopRadius = 0.02f * baseSize,
                    BottomRadius = 0.008f * baseSize,
                    Height = 0.03f * baseSize
                };
                var noseLeaf = new MeshInstance3D { Mesh = noseLeafMesh, MaterialOverride = earInnerMat };
                noseLeaf.Position = new Vector3(0, 0.01f * baseSize, 0.1f * baseSize);
                noseLeaf.RotationDegrees = new Vector3(-60, 0, 0);
                headNode.AddChild(noseLeaf);

                // Nostrils - high LOD only
                var nostrilMesh = new SphereMesh { Radius = 0.008f * baseSize, Height = 0.016f * baseSize };
                var nostrilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
                var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
                leftNostril.Position = new Vector3(-0.012f * baseSize, -0.01f * baseSize, 0.1f * baseSize);
                headNode.AddChild(leftNostril);
                var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
                rightNostril.Position = new Vector3(0.012f * baseSize, -0.01f * baseSize, 0.1f * baseSize);
                headNode.AddChild(rightNostril);
            }

            // Mouth
            var mouthMesh = new BoxMesh { Size = new Vector3(0.04f * baseSize, 0.01f * baseSize, 0.02f * baseSize) };
            var mouthMat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.08f, 0.1f) };
            var mouth = new MeshInstance3D { Mesh = mouthMesh, MaterialOverride = mouthMat };
            mouth.Position = new Vector3(0, -0.04f * baseSize, 0.08f * baseSize);
            headNode.AddChild(mouth);

            // Fangs
            var fangMesh = new CylinderMesh
            {
                TopRadius = 0.002f * baseSize,
                BottomRadius = 0.006f * baseSize,
                Height = 0.025f * baseSize
            };
            var leftFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = fangMat };
            leftFang.Position = new Vector3(-0.012f * baseSize, -0.05f * baseSize, 0.08f * baseSize);
            leftFang.RotationDegrees = new Vector3(5, 0, 3);
            headNode.AddChild(leftFang);
            var rightFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = fangMat };
            rightFang.Position = new Vector3(0.012f * baseSize, -0.05f * baseSize, 0.08f * baseSize);
            rightFang.RotationDegrees = new Vector3(5, 0, -3);
            headNode.AddChild(rightFang);
        }

        // Big ears (distinctive bat feature) - ANIMATION-READY with separate nodes for twitching
        // Left Ear Node (for animation control)
        var leftEarNode = new Node3D();
        leftEarNode.Position = new Vector3(-0.06f * baseSize, 0.12f * baseSize, -0.02f * baseSize);
        leftEarNode.RotationDegrees = new Vector3(10, 0, -25);
        headNode.AddChild(leftEarNode);

        // Left ear mesh
        var earMesh = new CylinderMesh
        {
            TopRadius = 0.01f * baseSize,
            BottomRadius = 0.045f * baseSize,
            Height = 0.14f * baseSize,
            RadialSegments = lodLevel == 2 ? 6 : (lodLevel == 1 ? 8 : 12)
        };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = bodyMat };
        leftEarNode.AddChild(leftEar);

        // Left inner ear - skip on low LOD
        if (lodLevel < 2)
        {
            var innerEarMesh = new CylinderMesh
            {
                TopRadius = 0.005f * baseSize,
                BottomRadius = 0.025f * baseSize,
                Height = 0.08f * baseSize,
                RadialSegments = lodLevel == 1 ? 6 : 10
            };
            var leftInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = earInnerMat };
            leftInnerEar.Position = new Vector3(0.005f * baseSize, -0.02f * baseSize, 0.03f * baseSize);
            leftEarNode.AddChild(leftInnerEar);
        }

        // Right Ear Node (for animation control)
        var rightEarNode = new Node3D();
        rightEarNode.Position = new Vector3(0.06f * baseSize, 0.12f * baseSize, -0.02f * baseSize);
        rightEarNode.RotationDegrees = new Vector3(10, 0, 25);
        headNode.AddChild(rightEarNode);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = bodyMat };
        rightEarNode.AddChild(rightEar);

        // Right inner ear - skip on low LOD
        if (lodLevel < 2)
        {
            var innerEarMesh = new CylinderMesh
            {
                TopRadius = 0.005f * baseSize,
                BottomRadius = 0.025f * baseSize,
                Height = 0.08f * baseSize,
                RadialSegments = lodLevel == 1 ? 6 : 10
            };
            var rightInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = earInnerMat };
            rightInnerEar.Position = new Vector3(-0.005f * baseSize, -0.02f * baseSize, 0.03f * baseSize);
            rightEarNode.AddChild(rightInnerEar);
        }

        // Glowing red eyes
        var eyeMesh = new SphereMesh
        {
            Radius = 0.022f * baseSize,
            Height = 0.044f * baseSize,
            RadialSegments = lodLevel == 2 ? 6 : (lodLevel == 1 ? 10 : 16)
        };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.035f * baseSize, 0.02f * baseSize, 0.06f * baseSize);
        headNode.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.035f * baseSize, 0.02f * baseSize, 0.06f * baseSize);
        headNode.AddChild(rightEye);

        // Wings with membrane and finger bones - ANIMATION-READY for flapping
        var leftWing = new Node3D();
        leftWing.Position = new Vector3(-0.12f * baseSize, 0.78f * baseSize, 0);
        parent.AddChild(leftWing);
        limbs.LeftArm = leftWing;

        // Wing arm bone (humerus)
        var wingBoneMesh = new CylinderMesh
        {
            TopRadius = 0.012f * baseSize,
            BottomRadius = 0.008f * baseSize,
            Height = 0.15f * baseSize,
            RadialSegments = lodLevel == 2 ? 4 : (lodLevel == 1 ? 6 : 8)
        };
        var leftWingArm = new MeshInstance3D { Mesh = wingBoneMesh, MaterialOverride = bodyMat };
        leftWingArm.Position = new Vector3(-0.07f * baseSize * wingSpan, 0, 0);
        leftWingArm.RotationDegrees = new Vector3(0, 0, 70);
        leftWing.AddChild(leftWingArm);

        // Wing finger bones (phalanges) - variable count based on LOD
        int fingerCount = lodLevel == 2 ? 2 : 4;
        if (lodLevel < 2)
        {
            var fingerBoneMesh = new CylinderMesh
            {
                TopRadius = 0.006f * baseSize,
                BottomRadius = 0.004f * baseSize,
                Height = 0.18f * baseSize,
                RadialSegments = lodLevel == 1 ? 4 : 6
            };
            for (int f = 0; f < fingerCount; f++)
            {
                var finger = new MeshInstance3D { Mesh = fingerBoneMesh, MaterialOverride = bodyMat };
                finger.Position = new Vector3(
                    (-0.15f - f * 0.03f) * baseSize * wingSpan,
                    (0.02f - f * 0.015f) * baseSize,
                    (-0.06f + f * 0.04f) * baseSize * wingSpan
                );
                finger.RotationDegrees = new Vector3(0, -15 + f * 10, 80 - f * 5);
                leftWing.AddChild(finger);
            }
        }

        // Wing membrane - enhanced with better shape
        if (lodLevel == 2)
        {
            // Low LOD - simple triangle
            var simpleMembrane = new MeshInstance3D { MaterialOverride = wingMat };
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            st.AddVertex(new Vector3(0, 0, 0));
            st.AddVertex(new Vector3(-0.25f * baseSize * wingSpan, 0.05f * baseSize, -0.1f * baseSize * wingSpan));
            st.AddVertex(new Vector3(-0.25f * baseSize * wingSpan, -0.05f * baseSize, 0.1f * baseSize * wingSpan));
            st.GenerateNormals();
            simpleMembrane.Mesh = st.Commit();
            leftWing.AddChild(simpleMembrane);
        }
        else
        {
            // Medium/High LOD - detailed membrane
            var membraneMesh = new BoxMesh
            {
                Size = new Vector3(0.28f * baseSize * wingSpan, 0.008f * baseSize, 0.22f * baseSize * wingSpan)
            };
            var leftMembrane = new MeshInstance3D { Mesh = membraneMesh, MaterialOverride = wingMat };
            leftMembrane.Position = new Vector3(-0.18f * baseSize * wingSpan, 0, 0);
            leftMembrane.RotationDegrees = new Vector3(0, -10, 20);
            leftWing.AddChild(leftMembrane);
        }

        // Right wing - mirror of left wing
        var rightWing = new Node3D();
        rightWing.Position = new Vector3(0.12f * baseSize, 0.78f * baseSize, 0);
        parent.AddChild(rightWing);
        limbs.RightArm = rightWing;

        // Right wing arm bone
        var rightWingArm = new MeshInstance3D { Mesh = wingBoneMesh, MaterialOverride = bodyMat };
        rightWingArm.Position = new Vector3(0.07f * baseSize * wingSpan, 0, 0);
        rightWingArm.RotationDegrees = new Vector3(0, 0, -70);
        rightWing.AddChild(rightWingArm);

        // Right wing finger bones
        if (lodLevel < 2)
        {
            var fingerBoneMesh = new CylinderMesh
            {
                TopRadius = 0.006f * baseSize,
                BottomRadius = 0.004f * baseSize,
                Height = 0.18f * baseSize,
                RadialSegments = lodLevel == 1 ? 4 : 6
            };
            for (int f = 0; f < fingerCount; f++)
            {
                var finger = new MeshInstance3D { Mesh = fingerBoneMesh, MaterialOverride = bodyMat };
                finger.Position = new Vector3(
                    (0.15f + f * 0.03f) * baseSize * wingSpan,
                    (0.02f - f * 0.015f) * baseSize,
                    (-0.06f + f * 0.04f) * baseSize * wingSpan
                );
                finger.RotationDegrees = new Vector3(0, 15 - f * 10, -80 + f * 5);
                rightWing.AddChild(finger);
            }
        }

        // Right wing membrane
        if (lodLevel == 2)
        {
            // Low LOD - simple triangle
            var simpleMembrane = new MeshInstance3D { MaterialOverride = wingMat };
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            st.AddVertex(new Vector3(0, 0, 0));
            st.AddVertex(new Vector3(0.25f * baseSize * wingSpan, 0.05f * baseSize, -0.1f * baseSize * wingSpan));
            st.AddVertex(new Vector3(0.25f * baseSize * wingSpan, -0.05f * baseSize, 0.1f * baseSize * wingSpan));
            st.GenerateNormals();
            simpleMembrane.Mesh = st.Commit();
            rightWing.AddChild(simpleMembrane);
        }
        else
        {
            // Medium/High LOD - detailed membrane
            var membraneMesh = new BoxMesh
            {
                Size = new Vector3(0.28f * baseSize * wingSpan, 0.008f * baseSize, 0.22f * baseSize * wingSpan)
            };
            var rightMembrane = new MeshInstance3D { Mesh = membraneMesh, MaterialOverride = wingMat };
            rightMembrane.Position = new Vector3(0.18f * baseSize * wingSpan, 0, 0);
            rightMembrane.RotationDegrees = new Vector3(0, 10, -20);
            rightWing.AddChild(rightMembrane);
        }

        // Tiny legs with claws - skip claws on low LOD
        var legMesh = new CylinderMesh
        {
            TopRadius = 0.012f * baseSize,
            BottomRadius = 0.008f * baseSize,
            Height = 0.08f * baseSize,
            RadialSegments = lodLevel == 2 ? 4 : (lodLevel == 1 ? 6 : 8)
        };
        var clawMesh = new CylinderMesh
        {
            TopRadius = 0.002f * baseSize,
            BottomRadius = 0.005f * baseSize,
            Height = 0.02f * baseSize,
            RadialSegments = lodLevel == 1 ? 4 : 6
        };
        var clawMat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.12f, 0.1f) };

        // Left leg
        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.04f * baseSize, 0.62f * baseSize, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;
        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = bodyMat };
        leftLegNode.AddChild(leftLeg);

        // Claws - skip on low LOD
        if (lodLevel < 2)
        {
            for (int c = 0; c < 3; c++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.01f + c * 0.01f) * baseSize, -0.05f * baseSize, 0);
                claw.RotationDegrees = new Vector3(0, 0, -10 + c * 10);
                leftLegNode.AddChild(claw);
            }
        }

        // Right leg
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.04f * baseSize, 0.62f * baseSize, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;
        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = bodyMat };
        rightLegNode.AddChild(rightLeg);

        // Claws - skip on low LOD
        if (lodLevel < 2)
        {
            for (int c = 0; c < 3; c++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.01f + c * 0.01f) * baseSize, -0.05f * baseSize, 0);
                claw.RotationDegrees = new Vector3(0, 0, -10 + c * 10);
                rightLegNode.AddChild(claw);
            }
        }
    }

    /// <summary>
    /// Creates an enhanced majestic Dragon mesh with LOD support and variation parameters.
    /// Features serpentine neck chain, articulated wings, segmented tail for animation.
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="limbs">Limb nodes for animation</param>
    /// <param name="lodLevel">Level of detail: 0=High (full detail), 1=Medium (simplified), 2=Low (silhouette)</param>
    /// <param name="scaleColorType">Scale color type: 0=Red, 1=Black, 2=Green, 3=Blue, 4=Gold</param>
    /// <param name="hornStyle">Horn style: 0=Curved, 1=Straight, 2=Branching</param>
    /// <param name="wingSpan">Wing span multiplier (0.8-1.5)</param>
    /// <param name="tailStyle">Tail style: 0=Spade, 1=Spikes, 2=Plain</param>
    /// <param name="sizeScale">Overall size multiplier (default 1.5x enemy size, 2-3x for bosses)</param>
    /// <param name="bossPresence">Enable boss-level effects (chest glow, smoke, etc.)</param>
    private static void CreateDragonMesh(Node3D parent, LimbNodes limbs, int lodLevel = 0,
        int scaleColorType = 0, int hornStyle = 0, float wingSpan = 1.0f, int tailStyle = 1,
        float sizeScale = 1.5f, bool bossPresence = false)
    {
        // Scale color variations
        Color scaleColor = scaleColorType switch
        {
            1 => new Color(0.15f, 0.15f, 0.18f), // Black dragon
            2 => new Color(0.2f, 0.5f, 0.15f),   // Green dragon
            3 => new Color(0.15f, 0.3f, 0.6f),   // Blue dragon
            4 => new Color(0.8f, 0.65f, 0.2f),   // Gold dragon
            _ => new Color(0.7f, 0.2f, 0.15f)    // Red dragon (default)
        };

        Color bellyColor = scaleColorType == 4 ?
            new Color(0.95f, 0.85f, 0.5f) : // Gold belly for gold dragon
            new Color(0.9f, 0.7f, 0.4f);
        Color wingColor = scaleColor.Darkened(0.2f);
        Color hornColor = scaleColor.Darkened(0.3f);
        Color clawColor = new Color(0.2f, 0.15f, 0.12f);

        // Glow colors based on scale type
        Color glowColor = scaleColorType switch
        {
            1 => new Color(0.6f, 0.2f, 0.8f),  // Black = purple fire
            2 => new Color(0.2f, 0.8f, 0.2f),  // Green = poison fire
            3 => new Color(0.3f, 0.5f, 1.0f),  // Blue = ice/lightning
            4 => new Color(1.0f, 0.9f, 0.4f),  // Gold = divine fire
            _ => new Color(1.0f, 0.6f, 0.1f)   // Red = classic fire
        };

        // Materials
        var scaleMat = new StandardMaterial3D
        {
            AlbedoColor = scaleColor,
            Roughness = 0.6f,
            Metallic = scaleColorType == 4 ? 0.5f : 0.2f // Gold is more metallic
        };
        var bellyMat = new StandardMaterial3D { AlbedoColor = bellyColor, Roughness = 0.7f };
        var hornMat = new StandardMaterial3D { AlbedoColor = hornColor, Roughness = 0.5f, Metallic = 0.1f };
        var clawMat = new StandardMaterial3D { AlbedoColor = clawColor, Roughness = 0.6f };
        var wingMat = new StandardMaterial3D
        {
            AlbedoColor = wingColor,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = lodLevel == 2 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
        };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.2f),
            EmissionEnabled = true,
            Emission = glowColor * (bossPresence ? 0.8f : 0.6f)
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        var toothMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.93f, 0.85f), Roughness = 0.5f };

        // Chest glow material for boss presence (fire within)
        var chestGlowMat = new StandardMaterial3D
        {
            AlbedoColor = glowColor,
            EmissionEnabled = true,
            Emission = glowColor * 1.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        float scale = sizeScale; // Default 1.5x, 2-3x for bosses

        // LOD-based mesh quality
        int bodySegments = lodLevel == 0 ? 24 : (lodLevel == 1 ? 16 : 8);
        int bodyRings = lodLevel == 0 ? 12 : (lodLevel == 1 ? 8 : 4);
        int headSegments = lodLevel == 0 ? 28 : (lodLevel == 1 ? 16 : 10);
        int headRings = lodLevel == 0 ? 20 : (lodLevel == 1 ? 12 : 8);
        int hornSegments = lodLevel == 0 ? 16 : (lodLevel == 1 ? 10 : 6);
        int spineCount = lodLevel == 0 ? 8 : (lodLevel == 1 ? 5 : 0);
        int neckSpineCount = lodLevel == 0 ? 3 : (lodLevel == 1 ? 2 : 0);
        int wingFingers = lodLevel == 0 ? 4 : (lodLevel == 1 ? 3 : 0);
        int neckSegments = lodLevel == 0 ? 3 : (lodLevel == 1 ? 2 : 1);
        int tailSegments = lodLevel == 0 ? 5 : (lodLevel == 1 ? 3 : 1);

        // === BODY ===
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh {
            Radius = 0.34f * scale,
            Height = 0.98f * scale,
            RadialSegments = bodySegments,
            Rings = bodyRings
        };
        body.Mesh = bodyMesh;
        body.MaterialOverride = scaleMat;
        body.Position = new Vector3(0, 0.7f * scale, 0);
        body.RotationDegrees = new Vector3(0, 0, 90);
        body.Scale = new Vector3(1f, 1f, 1.02f);
        parent.AddChild(body);

        // Belly - vulnerable underbelly
        if (lodLevel < 2)
        {
            var belly = new MeshInstance3D();
            var bellyMesh = new CapsuleMesh {
                Radius = 0.25f * scale,
                Height = 0.58f * scale,
                RadialSegments = bodySegments - 4,
                Rings = bodyRings - 2
            };
            belly.Mesh = bellyMesh;
            belly.MaterialOverride = bellyMat;
            belly.Position = new Vector3(0, 0.55f * scale, 0.13f * scale);
            belly.RotationDegrees = new Vector3(0, 0, 90);
            parent.AddChild(belly);

            // Boss presence: Glowing chest (fire within)
            if (bossPresence)
            {
                var chestGlow = new MeshInstance3D();
                var glowMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.24f * scale };
                chestGlow.Mesh = glowMesh;
                chestGlow.MaterialOverride = chestGlowMat;
                chestGlow.Position = new Vector3(0, 0.65f * scale, 0.18f * scale);
                parent.AddChild(chestGlow);

                // Pulsing light from chest
                var chestLight = new OmniLight3D();
                chestLight.LightColor = glowColor;
                chestLight.LightEnergy = 0.8f;
                chestLight.OmniRange = 4f * scale;
                chestLight.Position = new Vector3(0, 0.65f * scale, 0.18f * scale);
                parent.AddChild(chestLight);
            }
        }

        // Back spines - scale ridges (enhanced with double row and varying sizes)
        if (lodLevel < 2 && spineCount > 0)
        {
            var spineMesh = new CylinderMesh {
                TopRadius = 0.005f * scale,
                BottomRadius = 0.025f * scale,
                Height = 0.12f * scale
            };
            var largeSpineMesh = new CylinderMesh {
                TopRadius = 0.007f * scale,
                BottomRadius = 0.032f * scale,
                Height = 0.16f * scale
            };
            for (int i = 0; i < spineCount; i++)
            {
                // Main central spine (larger on every other)
                var spine = new MeshInstance3D {
                    Mesh = (i % 2 == 0) ? largeSpineMesh : spineMesh,
                    MaterialOverride = hornMat
                };
                float zPos = 0.35f - i * 0.11f;
                spine.Position = new Vector3(0, 0.92f * scale - i * 0.03f * scale, zPos * scale);
                spine.RotationDegrees = new Vector3(-15 - i * 3, 0, 0);
                parent.AddChild(spine);

                // Side scale ridges (triangular scale plates flanking the spine)
                if (lodLevel == 0)
                {
                    var ridgeMesh = new CylinderMesh {
                        TopRadius = 0.003f * scale,
                        BottomRadius = 0.018f * scale,
                        Height = 0.06f * scale
                    };
                    var leftRidge = new MeshInstance3D { Mesh = ridgeMesh, MaterialOverride = scaleMat };
                    leftRidge.Position = new Vector3(-0.08f * scale, 0.88f * scale - i * 0.03f * scale, zPos * scale);
                    leftRidge.RotationDegrees = new Vector3(-10 - i * 2, 0, 25);
                    parent.AddChild(leftRidge);

                    var rightRidge = new MeshInstance3D { Mesh = ridgeMesh, MaterialOverride = scaleMat };
                    rightRidge.Position = new Vector3(0.08f * scale, 0.88f * scale - i * 0.03f * scale, zPos * scale);
                    rightRidge.RotationDegrees = new Vector3(-10 - i * 2, 0, -25);
                    parent.AddChild(rightRidge);
                }
            }

            // Additional body scale texture (small raised scales across body)
            if (lodLevel == 0)
            {
                var scaleRidgeMesh = new CylinderMesh {
                    TopRadius = 0.002f * scale,
                    BottomRadius = 0.012f * scale,
                    Height = 0.035f * scale
                };
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        float angle = (col * 45f + row * 22f) * Mathf.Pi / 180f;
                        float yOffset = 0.65f + row * 0.08f;
                        float xPos = Mathf.Sin(angle) * 0.28f * scale;
                        float zOffset = Mathf.Cos(angle) * 0.15f * scale;

                        var scaleRidge = new MeshInstance3D { Mesh = scaleRidgeMesh, MaterialOverride = scaleMat };
                        scaleRidge.Position = new Vector3(xPos, yOffset * scale, zOffset);
                        scaleRidge.RotationDegrees = new Vector3(0, 0, Mathf.Sin(angle) * 30f);
                        parent.AddChild(scaleRidge);
                    }
                }
            }
        }

        // === SERPENTINE NECK (Animation Chain) ===
        // Neck geometry with proper overlap between segments
        float neckSegmentHeight = 0.22f * scale;
        float neckTopRadius = 0.11f * scale; // Top of first segment
        float neckBottomRadius = 0.17f * scale; // Bottom of first segment
        float neckOverlap = CalculateOverlap(neckTopRadius, 0.20f); // 20% overlap
        float neckSegmentStep = neckSegmentHeight - neckOverlap; // Distance between segment centers

        var neckChain = new Node3D();
        // Position neck at front-top of body - body is horizontal capsule at Y=0.7, radius 0.34
        // Body top is at Y = 0.7 + 0.34 = 1.04, body front is at Z = 0.49 (half of 0.98 height)
        // Neck attaches at upper-front of body, with initial forward tilt
        float bodyTopY = 0.7f * scale + 0.34f * scale; // Top of body capsule
        float bodyFrontZ = 0.98f * scale / 2f;          // Front of body capsule
        // Position neck at front of body, slightly lower than top for natural shoulder connection
        neckChain.Position = new Vector3(0, bodyTopY - 0.15f * scale, bodyFrontZ + 0.02f * scale);
        parent.AddChild(neckChain);

        Vector3 neckPos = Vector3.Zero;
        // Initial neck angle: positive = tilts forward, negative = tilts backward
        // Dragon neck should curve UP and FORWARD in an S-curve, not lean back
        float neckAngle = 35f; // Tilt forward initially (was -25 which tilted backward!)
        Node3D previousNeckSegment = neckChain;

        for (int i = 0; i < neckSegments; i++)
        {
            var neckSegNode = new Node3D();
            neckSegNode.Position = neckPos;
            neckSegNode.RotationDegrees = new Vector3(neckAngle, 0, 0);
            previousNeckSegment.AddChild(neckSegNode);

            // Neck segment radii taper toward head
            float segTopRadius = (0.11f - i * 0.02f) * scale;
            float segBottomRadius = (0.17f - i * 0.02f) * scale;

            var neckSeg = new MeshInstance3D();
            var neckMesh = new CylinderMesh {
                TopRadius = segTopRadius,
                BottomRadius = segBottomRadius,
                Height = neckSegmentHeight
            };
            neckSeg.Mesh = neckMesh;
            neckSeg.MaterialOverride = scaleMat;
            neckSeg.Position = new Vector3(0, neckSegmentHeight / 2f, 0); // Center mesh at half height
            neckSegNode.AddChild(neckSeg);

            // Neck spines on each segment
            if (lodLevel == 0 && neckSpineCount > 0 && i < neckSegments - 1)
            {
                var spineMesh = new CylinderMesh {
                    TopRadius = 0.004f * scale,
                    BottomRadius = 0.02f * scale,
                    Height = 0.1f * scale
                };
                var neckSpine = new MeshInstance3D { Mesh = spineMesh, MaterialOverride = hornMat };
                neckSpine.Position = new Vector3(0, neckSegmentHeight * 0.7f, -0.03f * scale);
                neckSpine.RotationDegrees = new Vector3(-15, 0, 0);
                neckSegNode.AddChild(neckSpine);
            }

            // Position next segment with overlap (not full height)
            neckPos = new Vector3(0, neckSegmentStep, 0);
            // S-curve neck: starts forward (35°), then curves upward toward head
            // Each segment gradually becomes more vertical, creating a natural serpentine curve
            neckAngle = 25f - i * 12f; // 35° -> 25° -> 13° -> 1° (progressively more upright)
            previousNeckSegment = neckSegNode;
        }

        // === HEAD (Animation Node) ===
        // Head positioned at top of last neck segment with overlap
        float headRadius = 0.18f * scale;
        float headHeight = 0.36f * scale;
        float headNeckOverlap = CalculateOverlap(neckTopRadius - (neckSegments - 1) * 0.02f * scale, 0.25f);
        float headY = neckSegmentStep - headNeckOverlap + headHeight * 0.3f; // Position head bottom into neck top

        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headY, 0);
        // Counter-rotate the head to face forward despite neck curvature
        // The neck segments accumulate forward tilt, so we need to tilt the head back
        // to make the dragon look forward/slightly up instead of at the ground
        float headCounterRotation = -75f - (neckSegments * 15f); // More aggressive tilt-back for proper forward gaze
        headNode.RotationDegrees = new Vector3(headCounterRotation, 0, 0);
        previousNeckSegment.AddChild(headNode);
        limbs.Head = headNode;

        // Head mesh
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh {
            Radius = 0.18f * scale,
            Height = 0.36f * scale,
            RadialSegments = headSegments,
            Rings = headRings
        };
        head.Mesh = headMesh;
        head.MaterialOverride = scaleMat;
        head.Scale = new Vector3(0.86f, 0.76f, 1.18f); // Elongated dragon skull
        headNode.AddChild(head);

        // Head crest (dorsal ridge)
        if (lodLevel < 2)
        {
            var crestMesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.04f * scale, 0.1f * scale) };
            var crest = new MeshInstance3D { Mesh = crestMesh, MaterialOverride = scaleMat };
            crest.Position = new Vector3(0, 0.12f * scale, -0.05f * scale);
            crest.RotationDegrees = new Vector3(10, 0, 0);
            headNode.AddChild(crest);

            // Brow ridges
            var browMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.025f * scale, 0.04f * scale) };
            var leftBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = scaleMat };
            leftBrow.Position = new Vector3(-0.08f * scale, 0.08f * scale, 0.08f * scale);
            leftBrow.RotationDegrees = new Vector3(0, 0, 15);
            headNode.AddChild(leftBrow);
            var rightBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = scaleMat };
            rightBrow.Position = new Vector3(0.08f * scale, 0.08f * scale, 0.08f * scale);
            rightBrow.RotationDegrees = new Vector3(0, 0, -15);
            headNode.AddChild(rightBrow);
        }

        // Long snout
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh {
            TopRadius = 0.055f * scale,
            BottomRadius = 0.09f * scale,
            Height = 0.22f * scale
        };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = scaleMat;
        snout.Position = new Vector3(0, -0.02f * scale, 0.18f * scale);
        snout.RotationDegrees = new Vector3(-72, 0, 0);
        headNode.AddChild(snout);

        // Nostrils
        if (lodLevel < 2)
        {
            var nostrilMesh = new SphereMesh { Radius = 0.02f * scale, Height = 0.04f * scale };
            var nostrilMat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.05f, 0.05f) };
            var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
            leftNostril.Position = new Vector3(-0.035f * scale, -0.02f * scale, 0.28f * scale);
            headNode.AddChild(leftNostril);
            var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
            rightNostril.Position = new Vector3(0.035f * scale, -0.02f * scale, 0.28f * scale);
            headNode.AddChild(rightNostril);

            // Smoke/ember wisps from nostrils (boss presence)
            if (bossPresence)
            {
                var smokeMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(glowColor.R, glowColor.G, glowColor.B, 0.4f),
                    EmissionEnabled = true,
                    Emission = glowColor * 0.5f,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                };
                var smokeMesh = new SphereMesh { Radius = 0.015f * scale, Height = 0.03f * scale };
                var smokeLeft = new MeshInstance3D { Mesh = smokeMesh, MaterialOverride = smokeMat };
                smokeLeft.Position = new Vector3(-0.04f * scale, 0.01f * scale, 0.32f * scale);
                headNode.AddChild(smokeLeft);
                var smokeRight = new MeshInstance3D { Mesh = smokeMesh, MaterialOverride = smokeMat };
                smokeRight.Position = new Vector3(0.04f * scale, 0.01f * scale, 0.32f * scale);
                headNode.AddChild(smokeRight);

                // Rising smoke trail particles
                var trailMesh = new SphereMesh { Radius = 0.01f * scale, Height = 0.02f * scale };
                for (int p = 0; p < 3; p++)
                {
                    float yOff = 0.02f + p * 0.025f;
                    float zOff = 0.33f + p * 0.015f;
                    float alphaFade = 0.35f - p * 0.1f;
                    var trailMatL = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(glowColor.R, glowColor.G, glowColor.B, alphaFade),
                        EmissionEnabled = true,
                        Emission = glowColor * (0.4f - p * 0.1f),
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                    };
                    var trailL = new MeshInstance3D { Mesh = trailMesh, MaterialOverride = trailMatL };
                    trailL.Position = new Vector3(-0.04f * scale + p * 0.005f * scale, yOff * scale, zOff * scale);
                    trailL.Scale = Vector3.One * (1f - p * 0.15f);
                    headNode.AddChild(trailL);

                    var trailR = new MeshInstance3D { Mesh = trailMesh, MaterialOverride = trailMatL };
                    trailR.Position = new Vector3(0.04f * scale - p * 0.005f * scale, yOff * scale, zOff * scale);
                    trailR.Scale = Vector3.One * (1f - p * 0.15f);
                    headNode.AddChild(trailR);
                }

                // Small ember particles
                var emberMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.5f, 0.1f, 0.7f),
                    EmissionEnabled = true,
                    Emission = new Color(1f, 0.4f, 0.05f) * 1.2f,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                };
                var emberMesh = new SphereMesh { Radius = 0.004f * scale, Height = 0.008f * scale };
                for (int e = 0; e < 4; e++)
                {
                    float angle = e * 0.8f;
                    var ember = new MeshInstance3D { Mesh = emberMesh, MaterialOverride = emberMat };
                    ember.Position = new Vector3(
                        (-0.03f + e * 0.02f) * scale,
                        (0.015f + Mathf.Sin(angle) * 0.02f) * scale,
                        (0.34f + e * 0.01f) * scale
                    );
                    headNode.AddChild(ember);
                }
            }
        }

        // Jaw (animation-ready for roaring)
        if (lodLevel < 2)
        {
            var jawMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.02f * scale, 0.08f * scale) };
            var upperJaw = new MeshInstance3D { Mesh = jawMesh, MaterialOverride = scaleMat };
            upperJaw.Position = new Vector3(0, -0.06f * scale, 0.22f * scale);
            headNode.AddChild(upperJaw);

            var lowerJaw = new MeshInstance3D { Mesh = jawMesh, MaterialOverride = scaleMat };
            lowerJaw.Position = new Vector3(0, -0.09f * scale, 0.20f * scale);
            headNode.AddChild(lowerJaw);
        }

        // Sharp fangs - visible and menacing
        var fangMesh = new CylinderMesh {
            TopRadius = 0.004f * scale,
            BottomRadius = 0.012f * scale,
            Height = 0.05f * scale
        };
        var leftFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = toothMat };
        leftFang.Position = new Vector3(-0.035f * scale, -0.09f * scale, 0.24f * scale);
        leftFang.RotationDegrees = new Vector3(10, 0, 5);
        headNode.AddChild(leftFang);
        var rightFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = toothMat };
        rightFang.Position = new Vector3(0.035f * scale, -0.09f * scale, 0.24f * scale);
        rightFang.RotationDegrees = new Vector3(10, 0, -5);
        headNode.AddChild(rightFang);

        // Additional teeth (high LOD only)
        if (lodLevel == 0)
        {
            var smallToothMesh = new CylinderMesh {
                TopRadius = 0.002f * scale,
                BottomRadius = 0.006f * scale,
                Height = 0.025f * scale
            };
            for (int t = 0; t < 4; t++)
            {
                var tooth = new MeshInstance3D { Mesh = smallToothMesh, MaterialOverride = toothMat };
                tooth.Position = new Vector3((-0.015f + t * 0.01f) * scale, -0.075f * scale, 0.23f * scale);
                tooth.RotationDegrees = new Vector3(8, 0, 0);
                headNode.AddChild(tooth);
            }
        }

        // Glowing reptilian eyes with slit pupils
        var eyeMesh = new SphereMesh {
            Radius = 0.048f * scale,
            Height = 0.096f * scale,
            RadialSegments = 20,
            Rings = 16
        };
        var pupilMesh = new CylinderMesh {
            TopRadius = 0.009f * scale,
            BottomRadius = 0.009f * scale,
            Height = 0.025f * scale,
            RadialSegments = 12
        };

        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.095f * scale, 0.05f * scale, 0.1f * scale);
        leftEye.Scale = new Vector3(0.98f, 1f, 1.02f);
        headNode.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-0.095f * scale, 0.05f * scale, 0.145f * scale);
        leftPupil.RotationDegrees = new Vector3(90, 0, 0);
        leftPupil.Scale = new Vector3(0.9f, 1f, 3.2f); // Narrow vertical slit
        headNode.AddChild(leftPupil);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.09f * scale, 0.05f * scale, 0.1f * scale);
        rightEye.Scale = new Vector3(1.02f, 1f, 0.98f);
        headNode.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(0.09f * scale, 0.05f * scale, 0.145f * scale);
        rightPupil.RotationDegrees = new Vector3(90, 0, 0);
        rightPupil.Scale = new Vector3(1.1f, 1f, 3.2f);
        headNode.AddChild(rightPupil);

        // === HORNS (Style Variations) ===
        if (lodLevel < 2)
        {
            switch (hornStyle)
            {
                case 0: // Curved (default)
                    {
                        var hornMesh = new CylinderMesh {
                            TopRadius = 0.008f * scale,
                            BottomRadius = 0.042f * scale,
                            Height = 0.24f * scale,
                            RadialSegments = hornSegments,
                            Rings = 8
                        };
                        var leftHorn = new MeshInstance3D { Mesh = hornMesh, MaterialOverride = hornMat };
                        leftHorn.Position = new Vector3(-0.1f * scale, 0.15f * scale, -0.04f * scale);
                        leftHorn.RotationDegrees = new Vector3(28, 18, -28);
                        leftHorn.Scale = new Vector3(1.05f, 1f, 0.98f);
                        headNode.AddChild(leftHorn);

                        var rightHorn = new MeshInstance3D { Mesh = hornMesh, MaterialOverride = hornMat };
                        rightHorn.Position = new Vector3(0.1f * scale, 0.15f * scale, -0.04f * scale);
                        rightHorn.RotationDegrees = new Vector3(25, -15, 25);
                        rightHorn.Scale = new Vector3(0.98f, 1f, 1.02f);
                        headNode.AddChild(rightHorn);

                        // Secondary horns
                        var smallHornMesh = new CylinderMesh {
                            TopRadius = 0.004f * scale,
                            BottomRadius = 0.022f * scale,
                            Height = 0.12f * scale,
                            RadialSegments = hornSegments - 4,
                            Rings = 5
                        };
                        var leftSmallHorn = new MeshInstance3D { Mesh = smallHornMesh, MaterialOverride = hornMat };
                        leftSmallHorn.Position = new Vector3(-0.13f * scale, 0.06f * scale, -0.02f * scale);
                        leftSmallHorn.RotationDegrees = new Vector3(42, 22, -42);
                        headNode.AddChild(leftSmallHorn);
                        var rightSmallHorn = new MeshInstance3D { Mesh = smallHornMesh, MaterialOverride = hornMat };
                        rightSmallHorn.Position = new Vector3(0.12f * scale, 0.06f * scale, -0.02f * scale);
                        rightSmallHorn.RotationDegrees = new Vector3(38, -18, 38);
                        headNode.AddChild(rightSmallHorn);
                    }
                    break;

                case 1: // Straight (forward-pointing)
                    {
                        var hornMesh = new CylinderMesh {
                            TopRadius = 0.005f * scale,
                            BottomRadius = 0.04f * scale,
                            Height = 0.28f * scale,
                            RadialSegments = hornSegments,
                            Rings = 6
                        };
                        var leftHorn = new MeshInstance3D { Mesh = hornMesh, MaterialOverride = hornMat };
                        leftHorn.Position = new Vector3(-0.09f * scale, 0.14f * scale, -0.03f * scale);
                        leftHorn.RotationDegrees = new Vector3(-12, 5, -8);
                        headNode.AddChild(leftHorn);

                        var rightHorn = new MeshInstance3D { Mesh = hornMesh, MaterialOverride = hornMat };
                        rightHorn.Position = new Vector3(0.09f * scale, 0.14f * scale, -0.03f * scale);
                        rightHorn.RotationDegrees = new Vector3(-12, -5, 8);
                        headNode.AddChild(rightHorn);
                    }
                    break;

                case 2: // Branching (antler-like)
                    {
                        var mainHornMesh = new CylinderMesh {
                            TopRadius = 0.012f * scale,
                            BottomRadius = 0.04f * scale,
                            Height = 0.2f * scale,
                            RadialSegments = hornSegments,
                            Rings = 5
                        };
                        var branchMesh = new CylinderMesh {
                            TopRadius = 0.004f * scale,
                            BottomRadius = 0.015f * scale,
                            Height = 0.1f * scale,
                            RadialSegments = hornSegments - 4
                        };

                        // Left main horn
                        var leftMainHorn = new MeshInstance3D { Mesh = mainHornMesh, MaterialOverride = hornMat };
                        leftMainHorn.Position = new Vector3(-0.1f * scale, 0.15f * scale, -0.04f * scale);
                        leftMainHorn.RotationDegrees = new Vector3(20, 10, -20);
                        headNode.AddChild(leftMainHorn);

                        // Left branches
                        var leftBranch1 = new MeshInstance3D { Mesh = branchMesh, MaterialOverride = hornMat };
                        leftBranch1.Position = new Vector3(-0.12f * scale, 0.24f * scale, -0.08f * scale);
                        leftBranch1.RotationDegrees = new Vector3(35, 15, -35);
                        headNode.AddChild(leftBranch1);
                        var leftBranch2 = new MeshInstance3D { Mesh = branchMesh, MaterialOverride = hornMat };
                        leftBranch2.Position = new Vector3(-0.14f * scale, 0.28f * scale, -0.06f * scale);
                        leftBranch2.RotationDegrees = new Vector3(15, 20, -45);
                        headNode.AddChild(leftBranch2);

                        // Right main horn
                        var rightMainHorn = new MeshInstance3D { Mesh = mainHornMesh, MaterialOverride = hornMat };
                        rightMainHorn.Position = new Vector3(0.1f * scale, 0.15f * scale, -0.04f * scale);
                        rightMainHorn.RotationDegrees = new Vector3(20, -10, 20);
                        headNode.AddChild(rightMainHorn);

                        // Right branches
                        var rightBranch1 = new MeshInstance3D { Mesh = branchMesh, MaterialOverride = hornMat };
                        rightBranch1.Position = new Vector3(0.12f * scale, 0.24f * scale, -0.08f * scale);
                        rightBranch1.RotationDegrees = new Vector3(35, -15, 35);
                        headNode.AddChild(rightBranch1);
                        var rightBranch2 = new MeshInstance3D { Mesh = branchMesh, MaterialOverride = hornMat };
                        rightBranch2.Position = new Vector3(0.14f * scale, 0.28f * scale, -0.06f * scale);
                        rightBranch2.RotationDegrees = new Vector3(15, -20, 45);
                        headNode.AddChild(rightBranch2);
                    }
                    break;
            }
        }

        // === MASSIVE WINGS (Animation-Ready with Fingers) ===
        float wingScale = wingSpan;

        // Left Wing
        var leftWing = new Node3D();
        leftWing.Position = new Vector3(-0.33f * scale, 0.88f * scale, -0.1f * scale);
        parent.AddChild(leftWing);
        limbs.LeftArm = leftWing;

        if (lodLevel < 2)
        {
            // Wing arm bone
            var wingBoneMesh = new CylinderMesh {
                TopRadius = 0.02f * scale,
                BottomRadius = 0.03f * scale,
                Height = 0.25f * scale * wingScale
            };
            var leftWingArm = new MeshInstance3D { Mesh = wingBoneMesh, MaterialOverride = scaleMat };
            leftWingArm.Position = new Vector3(-0.12f * scale * wingScale, 0, 0);
            leftWingArm.RotationDegrees = new Vector3(0, 0, 60);
            leftWing.AddChild(leftWingArm);

            // Wing fingers (bat-like structure)
            if (wingFingers > 0)
            {
                var fingerMesh = new CylinderMesh {
                    TopRadius = 0.008f * scale,
                    BottomRadius = 0.015f * scale,
                    Height = 0.35f * scale * wingScale
                };
                for (int f = 0; f < wingFingers; f++)
                {
                    var finger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = scaleMat };
                    finger.Position = new Vector3(
                        (-0.28f - f * 0.08f) * scale * wingScale,
                        0.02f * scale,
                        (-0.05f + f * 0.08f) * scale
                    );
                    finger.RotationDegrees = new Vector3(0, -10 + f * 12, 70);
                    leftWing.AddChild(finger);
                }
            }
        }

        // Wing membrane with vein details
        var wingBoxMesh = new BoxMesh {
            Size = new Vector3(0.55f * scale * wingScale, 0.015f * scale, 0.38f * scale)
        };
        var leftWingMesh = new MeshInstance3D { Mesh = wingBoxMesh, MaterialOverride = wingMat };
        leftWingMesh.Position = new Vector3(-0.32f * scale * wingScale, 0, 0);
        leftWingMesh.RotationDegrees = new Vector3(0, 0, 28);
        leftWing.AddChild(leftWingMesh);

        // Wing membrane veins (dark lines radiating from wing bone)
        if (lodLevel == 0)
        {
            var veinMat = new StandardMaterial3D {
                AlbedoColor = scaleColor.Darkened(0.4f),
                Roughness = 0.7f
            };
            var veinMesh = new CylinderMesh {
                TopRadius = 0.003f * scale,
                BottomRadius = 0.006f * scale,
                Height = 0.28f * scale * wingScale
            };
            // Left wing veins
            for (int v = 0; v < 5; v++)
            {
                var vein = new MeshInstance3D { Mesh = veinMesh, MaterialOverride = veinMat };
                vein.Position = new Vector3(
                    (-0.18f - v * 0.07f) * scale * wingScale,
                    0.008f * scale,
                    (-0.08f + v * 0.06f) * scale
                );
                vein.RotationDegrees = new Vector3(0, -5 + v * 8, 65 + v * 3);
                leftWing.AddChild(vein);
            }
        }

        // Right Wing
        var rightWing = new Node3D();
        rightWing.Position = new Vector3(0.33f * scale, 0.88f * scale, -0.1f * scale);
        parent.AddChild(rightWing);
        limbs.RightArm = rightWing;

        if (lodLevel < 2)
        {
            var rightWingArm = new MeshInstance3D {
                Mesh = new CylinderMesh {
                    TopRadius = 0.02f * scale,
                    BottomRadius = 0.03f * scale,
                    Height = 0.25f * scale * wingScale
                },
                MaterialOverride = scaleMat
            };
            rightWingArm.Position = new Vector3(0.12f * scale * wingScale, 0, 0);
            rightWingArm.RotationDegrees = new Vector3(0, 0, -60);
            rightWing.AddChild(rightWingArm);

            if (wingFingers > 0)
            {
                var fingerMesh = new CylinderMesh {
                    TopRadius = 0.008f * scale,
                    BottomRadius = 0.015f * scale,
                    Height = 0.35f * scale * wingScale
                };
                for (int f = 0; f < wingFingers; f++)
                {
                    var finger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = scaleMat };
                    finger.Position = new Vector3(
                        (0.28f + f * 0.08f) * scale * wingScale,
                        0.02f * scale,
                        (-0.05f + f * 0.08f) * scale
                    );
                    finger.RotationDegrees = new Vector3(0, 10 - f * 12, -70);
                    rightWing.AddChild(finger);
                }
            }
        }

        var rightWingMesh = new MeshInstance3D { Mesh = wingBoxMesh, MaterialOverride = wingMat };
        rightWingMesh.Position = new Vector3(0.32f * scale * wingScale, 0, 0);
        rightWingMesh.RotationDegrees = new Vector3(0, 0, -28);
        rightWing.AddChild(rightWingMesh);

        // Right wing veins
        if (lodLevel == 0)
        {
            var veinMat = new StandardMaterial3D {
                AlbedoColor = scaleColor.Darkened(0.4f),
                Roughness = 0.7f
            };
            var veinMesh = new CylinderMesh {
                TopRadius = 0.003f * scale,
                BottomRadius = 0.006f * scale,
                Height = 0.28f * scale * wingScale
            };
            for (int v = 0; v < 5; v++)
            {
                var vein = new MeshInstance3D { Mesh = veinMesh, MaterialOverride = veinMat };
                vein.Position = new Vector3(
                    (0.18f + v * 0.07f) * scale * wingScale,
                    0.008f * scale,
                    (-0.08f + v * 0.06f) * scale
                );
                vein.RotationDegrees = new Vector3(0, 5 - v * 8, -65 - v * 3);
                rightWing.AddChild(vein);
            }
        }

        // === POWERFUL LEGS (All Four Separate) ===
        CreateDragonLeg(parent, limbs, scaleMat, clawMat, scale, true, lodLevel);  // Front left
        CreateDragonLeg(parent, limbs, scaleMat, clawMat, scale, false, lodLevel); // Front right

        // Rear legs (slightly back and smaller)
        if (lodLevel < 2)
        {
            var rearLeftLeg = new Node3D();
            rearLeftLeg.Position = new Vector3(-0.22f * scale, 0.28f * scale, -0.25f * scale);
            parent.AddChild(rearLeftLeg);

            var rearLegMesh = new CylinderMesh {
                TopRadius = 0.07f * scale,
                BottomRadius = 0.055f * scale,
                Height = 0.25f * scale
            };
            var rearLeftUpper = new MeshInstance3D { Mesh = rearLegMesh, MaterialOverride = scaleMat };
            rearLeftUpper.Position = new Vector3(0, -0.12f * scale, 0);
            rearLeftLeg.AddChild(rearLeftUpper);

            var footMesh = new BoxMesh { Size = new Vector3(0.09f * scale, 0.03f * scale, 0.13f * scale) };
            var rearLeftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = scaleMat };
            rearLeftFoot.Position = new Vector3(0, -0.26f * scale, 0.04f * scale);
            rearLeftLeg.AddChild(rearLeftFoot);

            // Rear claws
            var clawMesh = new CylinderMesh {
                TopRadius = 0.003f * scale,
                BottomRadius = 0.012f * scale,
                Height = 0.05f * scale
            };
            for (int c = 0; c < 3; c++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.03f + c * 0.03f) * scale, -0.28f * scale, 0.11f * scale);
                claw.RotationDegrees = new Vector3(-55, 0, -8 + c * 8);
                rearLeftLeg.AddChild(claw);
            }

            // Rear right leg (mirrored)
            var rearRightLeg = new Node3D();
            rearRightLeg.Position = new Vector3(0.22f * scale, 0.28f * scale, -0.25f * scale);
            parent.AddChild(rearRightLeg);

            var rearRightUpper = new MeshInstance3D { Mesh = rearLegMesh, MaterialOverride = scaleMat };
            rearRightUpper.Position = new Vector3(0, -0.12f * scale, 0);
            rearRightLeg.AddChild(rearRightUpper);

            var rearRightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = scaleMat };
            rearRightFoot.Position = new Vector3(0, -0.26f * scale, 0.04f * scale);
            rearRightLeg.AddChild(rearRightFoot);

            for (int c = 0; c < 3; c++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.03f + c * 0.03f) * scale, -0.28f * scale, 0.11f * scale);
                claw.RotationDegrees = new Vector3(-55, 0, -8 + c * 8);
                rearRightLeg.AddChild(claw);
            }
        }

        // === LONG TAIL (Segmented Chain for Animation) ===
        // Note: Tail extends backward (-Z) from body. We use a chain where each segment
        // is positioned along the -Z axis (backward) with a slight droop (-Y).
        // CylinderMesh: TopRadius is at +Y, BottomRadius is at -Y.
        // We rotate 90° on X to orient the cylinder along Z axis (tip pointing -Z).
        var tailChain = new Node3D();
        tailChain.Position = new Vector3(0, 0.55f * scale, -0.4f * scale);
        parent.AddChild(tailChain);

        Node3D previousTailSegment = tailChain;
        float tailRadius = 0.12f;

        for (int i = 0; i < tailSegments; i++)
        {
            var tailSegNode = new Node3D();
            // Position each segment extending backward (-Z) with slight droop (-Y)
            tailSegNode.Position = new Vector3(0, -0.04f * scale, -0.18f * scale);
            // Slight curve downward as tail extends
            tailSegNode.RotationDegrees = new Vector3(5 + i * 3, 0, 0);
            previousTailSegment.AddChild(tailSegNode);

            // Wide end (base) toward body, narrow end (tip) away from body
            // Since cylinder extends along Y axis and we rotate 90° on X,
            // BottomRadius (-Y before rotation = +Z after) faces body
            // TopRadius (+Y before rotation = -Z after) faces tail tip
            float baseRadius = (tailRadius - i * 0.015f) * scale;  // Wider end toward body
            float tipRadius = (tailRadius * 0.7f - i * 0.015f) * scale;  // Narrower end toward tip
            var tailSegMesh = new CylinderMesh {
                TopRadius = tipRadius,      // Narrow end (will point -Z after rotation)
                BottomRadius = baseRadius,  // Wide end (will point +Z after rotation, toward body)
                Height = 0.2f * scale
            };

            var tailSeg = new MeshInstance3D { Mesh = tailSegMesh, MaterialOverride = scaleMat };
            // Rotate 90° on X to orient cylinder along Z axis instead of Y axis
            tailSeg.RotationDegrees = new Vector3(90, 0, 0);
            tailSeg.Position = new Vector3(0, 0, -0.1f * scale);
            tailSegNode.AddChild(tailSeg);

            // Tail spines (style variation) - pointing up/back from tail
            if (lodLevel == 0 && tailStyle == 1 && i < tailSegments - 1) // Spikes style
            {
                var tailSpineMesh = new CylinderMesh {
                    TopRadius = 0.004f * scale,
                    BottomRadius = 0.018f * scale,
                    Height = 0.08f * scale
                };
                var tailSpine = new MeshInstance3D { Mesh = tailSpineMesh, MaterialOverride = hornMat };
                tailSpine.Position = new Vector3(0, baseRadius * 0.8f, -0.1f * scale);
                tailSpine.RotationDegrees = new Vector3(-15, 0, 0);
                tailSegNode.AddChild(tailSpine);
            }

            previousTailSegment = tailSegNode;
            tailRadius -= 0.018f;
        }

        // Tail end (style variation)
        switch (tailStyle)
        {
            case 0: // Spade - flat diamond shape at tail tip
                {
                    var spadeMesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.02f * scale, 0.12f * scale) };
                    var spade = new MeshInstance3D { Mesh = spadeMesh, MaterialOverride = scaleMat };
                    spade.Position = new Vector3(0, 0, -0.12f * scale);
                    spade.Scale = new Vector3(1f, 1f, 1.5f);
                    previousTailSegment.AddChild(spade);
                }
                break;

            case 1: // Large spike pointing backward
                {
                    var tailSpikeMesh = new CylinderMesh {
                        TopRadius = 0.003f * scale,
                        BottomRadius = 0.025f * scale,
                        Height = 0.12f * scale
                    };
                    var tailSpike = new MeshInstance3D { Mesh = tailSpikeMesh, MaterialOverride = hornMat };
                    // Rotate to point backward (-Z direction)
                    tailSpike.RotationDegrees = new Vector3(90, 0, 0);
                    tailSpike.Position = new Vector3(0, 0, -0.1f * scale);
                    previousTailSegment.AddChild(tailSpike);
                }
                break;

            case 2: // Plain tapered end (already created by segments)
                break;
        }
    }

    private static void CreateDragonLeg(Node3D parent, LimbNodes limbs, StandardMaterial3D scaleMat,
        StandardMaterial3D clawMat, float scale, bool isLeft, int lodLevel = 0)
    {
        float side = isLeft ? -1 : 1;

        var legNode = new Node3D();
        legNode.Position = new Vector3(side * 0.24f * scale, 0.32f * scale, 0.05f * scale);
        parent.AddChild(legNode);

        if (isLeft) limbs.LeftLeg = legNode;
        else limbs.RightLeg = legNode;

        int legSegments = lodLevel == 0 ? 12 : (lodLevel == 1 ? 8 : 6);

        // Upper leg (thigh)
        var upperLegMesh = new CylinderMesh {
            TopRadius = 0.08f * scale,
            BottomRadius = 0.065f * scale,
            Height = 0.2f * scale,
            RadialSegments = legSegments
        };
        var upperLeg = new MeshInstance3D { Mesh = upperLegMesh, MaterialOverride = scaleMat };
        upperLeg.Position = new Vector3(0, -0.08f * scale, 0);
        legNode.AddChild(upperLeg);

        // Lower leg (shin)
        var lowerLegMesh = new CylinderMesh {
            TopRadius = 0.06f * scale,
            BottomRadius = 0.05f * scale,
            Height = 0.18f * scale,
            RadialSegments = legSegments
        };
        var lowerLeg = new MeshInstance3D { Mesh = lowerLegMesh, MaterialOverride = scaleMat };
        lowerLeg.Position = new Vector3(0, -0.22f * scale, 0);
        legNode.AddChild(lowerLeg);

        // Foot
        var footMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.035f * scale, 0.15f * scale) };
        var foot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = scaleMat };
        foot.Position = new Vector3(0, -0.32f * scale, 0.04f * scale);
        legNode.AddChild(foot);

        // Sharp claws
        var clawMesh = new CylinderMesh {
            TopRadius = 0.003f * scale,
            BottomRadius = 0.012f * scale,
            Height = 0.05f * scale
        };
        for (int c = 0; c < 3; c++)
        {
            var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
            claw.Position = new Vector3((-0.03f + c * 0.03f) * scale, -0.34f * scale, 0.12f * scale);
            claw.RotationDegrees = new Vector3(-55, 0, -8 + c * 8);
            legNode.AddChild(claw);
        }

        // Knee spike (high LOD only)
        if (lodLevel == 0)
        {
            var kneeSpikeMesh = new CylinderMesh {
                TopRadius = 0.004f * scale,
                BottomRadius = 0.015f * scale,
                Height = 0.06f * scale
            };
            var kneeSpike = new MeshInstance3D { Mesh = kneeSpikeMesh, MaterialOverride = scaleMat };
            kneeSpike.Position = new Vector3(side * 0.04f * scale, -0.16f * scale, -0.02f * scale);
            kneeSpike.RotationDegrees = new Vector3(0, 0, side * 45);
            legNode.AddChild(kneeSpike);
        }
    }


    /// <summary>
    /// Creates an enhanced anatomically accurate floating Eye creature with LOD support.
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="limbs">Limb nodes for animation</param>
    /// <param name="colorOverride">Optional iris color override</param>
    /// <param name="lodLevel">Level of detail: 0=High, 1=Medium, 2=Low</param>
    /// <param name="pupilShape">Pupil shape: 0=Round, 1=Slit, 2=Star</param>
    /// <param name="bloodshotIntensity">Blood vessel intensity 0-1</param>
    /// <param name="nerveTendrilCount">Number of trailing nerve tendrils (3-8)</param>
    /// <param name="sizeVariation">Size multiplier (0.8-1.5)</param>
    private static void CreateEyeMesh(Node3D parent, LimbNodes limbs, Color? colorOverride = null,
        int lodLevel = 0, int pupilShape = 1, float bloodshotIntensity = 0.7f,
        int nerveTendrilCount = 6, float sizeVariation = 1.0f)
    {
        // Vary iris color for different eye types
        Color irisColor = colorOverride ?? GetRandomIrisColor();
        Color veinColor = new Color(0.8f, 0.2f, 0.2f) * bloodshotIntensity;

        // Scale all dimensions
        float scale = sizeVariation;

        // LOD-based mesh quality
        int scleraSegments = lodLevel == 0 ? 32 : (lodLevel == 1 ? 20 : 12);
        int scleraRings = lodLevel == 0 ? 24 : (lodLevel == 1 ? 16 : 10);
        int irisSegments = lodLevel == 0 ? 24 : (lodLevel == 1 ? 16 : 10);
        int irisRings = lodLevel == 0 ? 18 : (lodLevel == 1 ? 12 : 8);

        // Materials
        var scleraMat = new StandardMaterial3D {
            AlbedoColor = new Color(0.95f, 0.93f, 0.88f), // Slightly off-white, more realistic
            Roughness = 0.3f,
            Metallic = 0.05f
        };
        var irisMat = new StandardMaterial3D
        {
            AlbedoColor = irisColor,
            EmissionEnabled = true,
            Emission = irisColor * 0.5f,
            Roughness = 0.4f
        };
        var pupilMat = new StandardMaterial3D {
            AlbedoColor = Colors.Black,
            Roughness = 0.1f
        };
        var corneaMat = new StandardMaterial3D {
            AlbedoColor = new Color(0.9f, 0.9f, 0.95f, 0.3f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f,
            Metallic = 0.1f
        };
        var veinMat = new StandardMaterial3D {
            AlbedoColor = veinColor,
            EmissionEnabled = bloodshotIntensity > 0.5f,
            Emission = veinColor * 0.2f
        };
        var eyelidMat = new StandardMaterial3D {
            AlbedoColor = new Color(0.65f, 0.45f, 0.45f),
            Roughness = 0.8f
        };
        var nerveMat = new StandardMaterial3D {
            AlbedoColor = new Color(0.75f, 0.55f, 0.55f),
            Roughness = 0.7f
        };

        // SCLERA (white outer sphere) - slightly asymmetric for creepy effect
        var sclera = new MeshInstance3D();
        var scleraMesh = new SphereMesh {
            Radius = 0.36f * scale,
            Height = 0.72f * scale,
            RadialSegments = scleraSegments,
            Rings = scleraRings
        };
        sclera.Mesh = scleraMesh;
        sclera.MaterialOverride = scleraMat;
        sclera.Position = new Vector3(0, 0.6f * scale, 0);
        sclera.Scale = new Vector3(1.03f, 0.97f, 1.02f); // Asymmetric for unnatural look
        parent.AddChild(sclera);
        limbs.Head = sclera;

        // BLOOD VESSELS - elaborate network on sclera surface (LOD-dependent)
        int veinCount = lodLevel == 0 ? 16 : (lodLevel == 1 ? 10 : 6);
        if (bloodshotIntensity > 0.1f)
        {
            for (int i = 0; i < veinCount; i++)
            {
                float angle = i * Mathf.Pi * 2f / veinCount;
                float yOffset = (i % 2 == 0) ? 0.1f : -0.05f;

                // Main vein with pulsing appearance
                var vein = new MeshInstance3D();
                var veinMesh = new CylinderMesh {
                    TopRadius = 0.003f * scale * bloodshotIntensity,
                    BottomRadius = 0.015f * scale * bloodshotIntensity,
                    Height = 0.2f * scale
                };
                vein.Mesh = veinMesh;
                vein.MaterialOverride = veinMat;
                vein.Position = new Vector3(
                    Mathf.Cos(angle) * 0.28f * scale,
                    0.6f * scale + yOffset * scale,
                    Mathf.Sin(angle) * 0.28f * scale
                );
                // Calculate rotation manually instead of using LookAt (which requires being in tree)
                var targetPos = new Vector3(0, 0.6f * scale, 0);
                var direction = (targetPos - vein.Position).Normalized();
                if (direction.LengthSquared() > 0.001f)
                {
                    float yaw = Mathf.Atan2(direction.X, direction.Z);
                    float pitch = -Mathf.Asin(direction.Y);
                    vein.Rotation = new Vector3(pitch + Mathf.Pi / 2f, yaw, 0);
                }
                parent.AddChild(vein);

                // Branch veins (only on high/medium LOD)
                if (lodLevel <= 1 && i % 2 == 0)
                {
                    var branch = new MeshInstance3D();
                    var branchMesh = new CylinderMesh {
                        TopRadius = 0.002f * scale * bloodshotIntensity,
                        BottomRadius = 0.008f * scale * bloodshotIntensity,
                        Height = 0.1f * scale
                    };
                    branch.Mesh = branchMesh;
                    branch.MaterialOverride = veinMat;
                    branch.Position = new Vector3(
                        Mathf.Cos(angle + 0.25f) * 0.22f * scale,
                        0.6f * scale + yOffset * scale + 0.08f * scale,
                        Mathf.Sin(angle + 0.25f) * 0.22f * scale
                    );
                    branch.RotationDegrees = new Vector3(30, angle * 57.3f, 20);
                    parent.AddChild(branch);

                    // Capillaries (only high LOD)
                    if (lodLevel == 0)
                    {
                        var capillary = new MeshInstance3D();
                        var capMesh = new CylinderMesh {
                            TopRadius = 0.001f * scale * bloodshotIntensity,
                            BottomRadius = 0.004f * scale * bloodshotIntensity,
                            Height = 0.06f * scale
                        };
                        capillary.Mesh = capMesh;
                        capillary.MaterialOverride = veinMat;
                        capillary.Position = new Vector3(
                            Mathf.Cos(angle - 0.2f) * 0.25f * scale,
                            0.6f * scale + yOffset * scale + 0.05f * scale,
                            Mathf.Sin(angle - 0.2f) * 0.25f * scale
                        );
                        capillary.RotationDegrees = new Vector3(-25, angle * 57.3f, -15);
                        parent.AddChild(capillary);
                    }
                }
            }
        }

        // IRIS - colored disk with radial pattern detail
        var iris = new MeshInstance3D();
        var irisMesh = new SphereMesh {
            Radius = 0.18f * scale,
            Height = 0.36f * scale,
            RadialSegments = irisSegments,
            Rings = irisRings
        };
        iris.Mesh = irisMesh;
        iris.MaterialOverride = irisMat;
        iris.Position = new Vector3(0, 0.6f * scale, 0.26f * scale);
        parent.AddChild(iris);

        // Iris radial pattern rings (only high/medium LOD)
        if (lodLevel <= 1)
        {
            int ringCount = lodLevel == 0 ? 3 : 2;
            for (int r = 0; r < ringCount; r++)
            {
                var irisRing = new MeshInstance3D();
                float ringRadius = (0.11f + r * 0.03f) * scale;
                var irisRingMesh = new TorusMesh {
                    InnerRadius = ringRadius,
                    OuterRadius = (ringRadius + 0.015f * scale),
                    RingSegments = irisSegments,
                    Rings = 8
                };
                irisRing.Mesh = irisRingMesh;
                irisRing.MaterialOverride = new StandardMaterial3D {
                    AlbedoColor = irisColor.Darkened(0.2f + r * 0.15f),
                    Roughness = 0.4f,
                    EmissionEnabled = true,
                    Emission = irisColor.Darkened(0.5f) * 0.3f
                };
                irisRing.Position = new Vector3(0, 0.6f * scale, 0.28f * scale);
                irisRing.RotationDegrees = new Vector3(90, 0, 0);
                irisRing.Scale = new Vector3(1, 1, 0.2f);
                parent.AddChild(irisRing);
            }
        }

        // PUPIL - separate node for dilation animation, shape varies
        var pupilNode = new Node3D();
        pupilNode.Name = "Pupil";
        pupilNode.Position = new Vector3(0, 0.6f * scale, 0.32f * scale);
        parent.AddChild(pupilNode);

        var pupil = new MeshInstance3D();
        var pupilMesh = new SphereMesh {
            Radius = 0.07f * scale,
            Height = 0.14f * scale,
            RadialSegments = lodLevel == 0 ? 16 : 10,
            Rings = lodLevel == 0 ? 12 : 8
        };
        pupil.Mesh = pupilMesh;
        pupil.MaterialOverride = pupilMat;

        // Shape pupil based on type
        switch (pupilShape)
        {
            case 0: // Round
                pupil.Scale = new Vector3(1f, 1f, 1f);
                break;
            case 1: // Slit (cat-like, menacing)
                pupil.Scale = new Vector3(0.3f, 1.5f, 1f);
                break;
            case 2: // Star (alien/eldritch)
                pupil.Scale = new Vector3(1f, 1f, 1f);
                // Add star points with extra geometry (high LOD only)
                if (lodLevel == 0)
                {
                    for (int s = 0; s < 5; s++)
                    {
                        float starAngle = s * Mathf.Pi * 2f / 5f;
                        var point = new MeshInstance3D();
                        var pointMesh = new CylinderMesh {
                            TopRadius = 0.002f * scale,
                            BottomRadius = 0.015f * scale,
                            Height = 0.04f * scale
                        };
                        point.Mesh = pointMesh;
                        point.MaterialOverride = pupilMat;
                        point.Position = new Vector3(
                            Mathf.Cos(starAngle) * 0.05f * scale,
                            0,
                            Mathf.Sin(starAngle) * 0.05f * scale
                        );
                        point.RotationDegrees = new Vector3(0, 0, starAngle * 57.3f);
                        pupilNode.AddChild(point);
                    }
                }
                break;
        }
        pupilNode.AddChild(pupil);

        // CORNEA - clear bulge over iris (only high LOD for performance)
        if (lodLevel == 0)
        {
            var cornea = new MeshInstance3D();
            var corneaMesh = new SphereMesh {
                Radius = 0.19f * scale,
                Height = 0.38f * scale,
                RadialSegments = 20,
                Rings = 16
            };
            cornea.Mesh = corneaMesh;
            cornea.MaterialOverride = corneaMat;
            cornea.Position = new Vector3(0, 0.6f * scale, 0.3f * scale);
            cornea.Scale = new Vector3(1f, 1f, 0.6f); // Flattened dome
            parent.AddChild(cornea);
        }

        // EYELID FRAGMENTS - torn, ragged, hanging (creepy effect)
        if (lodLevel <= 1)
        {
            int lidFragments = lodLevel == 0 ? 8 : 5;
            for (int i = 0; i < lidFragments; i++)
            {
                float angle = i * Mathf.Pi * 2f / lidFragments;
                bool isUpper = i % 2 == 0;
                float yPos = isUpper ? 0.78f : 0.42f;
                float hangAmount = 0.02f + (i % 3) * 0.015f;

                var lidFrag = new MeshInstance3D();
                var fragMesh = new BoxMesh {
                    Size = new Vector3(0.1f * scale, 0.08f * scale, 0.05f * scale)
                };
                lidFrag.Mesh = fragMesh;
                lidFrag.MaterialOverride = eyelidMat;
                lidFrag.Position = new Vector3(
                    Mathf.Cos(angle) * 0.32f * scale,
                    yPos * scale - hangAmount * scale,
                    Mathf.Sin(angle) * 0.32f * scale
                );
                lidFrag.RotationDegrees = new Vector3(
                    isUpper ? -15 : 15,
                    angle * 57.3f,
                    (i % 2) * 10 - 5
                );
                lidFrag.Scale = new Vector3(1f, 1f + hangAmount * 3f, 0.8f); // Stretched/torn
                parent.AddChild(lidFrag);
            }
        }

        // NERVE TENDRILS - trailing optic nerve fragments (like jellyfish tentacles)
        int tendrilCount = Mathf.Clamp(nerveTendrilCount, 3, 8);
        for (int i = 0; i < tendrilCount; i++)
        {
            float angle = i * Mathf.Pi * 2f / tendrilCount + Mathf.Pi / tendrilCount;
            float tendrilLength = (0.25f + (i % 3) * 0.05f) * scale;

            // Main nerve tendril - tapers from thick to thin
            var tendril = new MeshInstance3D();
            var tendrilMesh = new CylinderMesh {
                TopRadius = 0.04f * scale,
                BottomRadius = 0.008f * scale,
                Height = tendrilLength
            };
            tendril.Mesh = tendrilMesh;
            tendril.MaterialOverride = nerveMat;
            tendril.Position = new Vector3(
                Mathf.Cos(angle) * 0.12f * scale,
                0.24f * scale,
                Mathf.Sin(angle) * 0.12f * scale
            );
            tendril.RotationDegrees = new Vector3(
                12 * Mathf.Cos(angle * 2f),
                angle * 57.3f,
                12 * Mathf.Sin(angle * 2f)
            );
            parent.AddChild(tendril);

            // Pulsing veins on nerve tendrils (high LOD only)
            if (lodLevel == 0)
            {
                int veinSegments = 3;
                for (int j = 0; j < veinSegments; j++)
                {
                    var tendrilVein = new MeshInstance3D();
                    var veinSegMesh = new CylinderMesh {
                        TopRadius = 0.002f * scale,
                        BottomRadius = 0.006f * scale,
                        Height = 0.06f * scale
                    };
                    tendrilVein.Mesh = veinSegMesh;
                    tendrilVein.MaterialOverride = veinMat;
                    float veinY = 0.32f * scale - j * 0.07f * scale;
                    tendrilVein.Position = new Vector3(
                        Mathf.Cos(angle) * 0.12f * scale + Mathf.Cos(angle + Mathf.Pi / 2f) * 0.03f * scale,
                        veinY,
                        Mathf.Sin(angle) * 0.12f * scale + Mathf.Sin(angle + Mathf.Pi / 2f) * 0.03f * scale
                    );
                    tendrilVein.RotationDegrees = new Vector3(0, angle * 57.3f, 25);
                    parent.AddChild(tendrilVein);
                }
            }

            // Tendril tips with slight bulb (only medium/high LOD)
            if (lodLevel <= 1)
            {
                var tip = new MeshInstance3D();
                var tipMesh = new SphereMesh {
                    Radius = 0.015f * scale,
                    Height = 0.03f * scale,
                    RadialSegments = 8,
                    Rings = 6
                };
                tip.Mesh = tipMesh;
                tip.MaterialOverride = new StandardMaterial3D {
                    AlbedoColor = nerveMat.AlbedoColor.Darkened(0.3f),
                    Roughness = 0.6f
                };
                tip.Position = new Vector3(
                    Mathf.Cos(angle) * 0.12f * scale,
                    0.24f * scale - tendrilLength / 2f - 0.02f * scale,
                    Mathf.Sin(angle) * 0.12f * scale
                );
                parent.AddChild(tip);
            }
        }

        // SMALL SECONDARY EYES - unnatural growths around main eye (only high/medium LOD)
        if (lodLevel <= 1)
        {
            int smallEyeCount = lodLevel == 0 ? 5 : 3;
            var smallEyeMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.88f, 0.88f, 0.83f),
                Roughness = 0.35f
            };

            for (int i = 0; i < smallEyeCount; i++)
            {
                float angle = i * Mathf.Pi * 2f / smallEyeCount + Mathf.Pi / 4f;
                float eyeSize = (0.05f + (i % 2) * 0.015f) * scale;

                var smallEye = new MeshInstance3D();
                var smallEyeMesh = new SphereMesh {
                    Radius = eyeSize,
                    Height = eyeSize * 2f,
                    RadialSegments = lodLevel == 0 ? 12 : 8,
                    Rings = lodLevel == 0 ? 10 : 6
                };
                smallEye.Mesh = smallEyeMesh;
                smallEye.MaterialOverride = smallEyeMat;
                smallEye.Position = new Vector3(
                    Mathf.Cos(angle) * 0.3f * scale,
                    0.72f * scale + (i % 2) * 0.05f * scale,
                    Mathf.Sin(angle) * 0.3f * scale
                );
                smallEye.Scale = new Vector3(1f, 0.95f, 1.02f); // Slightly deformed
                parent.AddChild(smallEye);

                // Small iris
                var smallIris = new MeshInstance3D();
                var smallIrisMesh = new SphereMesh {
                    Radius = eyeSize * 0.6f,
                    Height = eyeSize * 1.2f,
                    RadialSegments = 8,
                    Rings = 6
                };
                smallIris.Mesh = smallIrisMesh;
                smallIris.MaterialOverride = new StandardMaterial3D {
                    AlbedoColor = irisColor,
                    EmissionEnabled = true,
                    Emission = irisColor * 0.3f
                };
                smallIris.Position = new Vector3(
                    Mathf.Cos(angle) * 0.32f * scale,
                    0.72f * scale + (i % 2) * 0.05f * scale,
                    Mathf.Sin(angle) * 0.32f * scale
                );
                parent.AddChild(smallIris);

                // Small pupil
                var smallPupil = new MeshInstance3D();
                var smallPupilMesh = new SphereMesh {
                    Radius = eyeSize * 0.3f,
                    Height = eyeSize * 0.6f,
                    RadialSegments = 6,
                    Rings = 4
                };
                smallPupil.Mesh = smallPupilMesh;
                smallPupil.MaterialOverride = pupilMat;
                smallPupil.Position = new Vector3(
                    Mathf.Cos(angle) * 0.34f * scale,
                    0.72f * scale + (i % 2) * 0.05f * scale,
                    Mathf.Sin(angle) * 0.34f * scale
                );
                parent.AddChild(smallPupil);
            }
        }

        // OPTIC NERVE STUMP - thick nerve bundle at rear of eye
        var opticNerve = new MeshInstance3D();
        var opticNerveMesh = new CylinderMesh {
            TopRadius = 0.09f * scale,
            BottomRadius = 0.06f * scale,
            Height = 0.18f * scale,
            RadialSegments = lodLevel == 0 ? 16 : 10
        };
        opticNerve.Mesh = opticNerveMesh;
        opticNerve.MaterialOverride = nerveMat;
        opticNerve.Position = new Vector3(0, 0.6f * scale, -0.4f * scale);
        opticNerve.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(opticNerve);

        // Nerve fiber details on optic nerve (high LOD only)
        if (lodLevel == 0)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.Pi * 2f / 6f;
                var fiber = new MeshInstance3D();
                var fiberMesh = new CylinderMesh {
                    TopRadius = 0.008f * scale,
                    BottomRadius = 0.005f * scale,
                    Height = 0.15f * scale
                };
                fiber.Mesh = fiberMesh;
                fiber.MaterialOverride = new StandardMaterial3D {
                    AlbedoColor = nerveMat.AlbedoColor.Lightened(0.15f),
                    Roughness = 0.6f
                };
                fiber.Position = new Vector3(
                    Mathf.Cos(angle) * 0.05f * scale,
                    0.6f * scale,
                    -0.4f * scale
                );
                fiber.RotationDegrees = new Vector3(90, 0, 0);
                parent.AddChild(fiber);
            }
        }
    }

    /// <summary>
    /// Helper method to generate random iris colors for variety
    /// </summary>
    private static Color GetRandomIrisColor()
    {
        int colorType = GD.RandRange(0, 4);
        return colorType switch
        {
            0 => new Color(0.9f, 0.2f, 0.2f),    // Red (default)
            1 => new Color(0.9f, 0.85f, 0.2f),   // Yellow/Gold
            2 => new Color(0.2f, 0.8f, 0.3f),    // Green
            3 => new Color(0.6f, 0.2f, 0.9f),    // Purple
            4 => new Color(0.2f, 0.6f, 0.9f),    // Blue
            _ => new Color(0.9f, 0.2f, 0.2f)
        };
    }

    private static void CreateMushroomMesh(Node3D parent, LimbNodes limbs, Color? colorOverride)
    {
        // === MATERIALS ===
        Color capColor = colorOverride ?? new Color(0.6f, 0.4f, 0.5f);
        Color stemColor = new Color(0.9f, 0.85f, 0.75f);
        Color stemColorDark = stemColor.Darkened(0.15f);
        Color spotColor = Colors.White;
        Color spotColorCream = new Color(0.95f, 0.92f, 0.85f);
        Color gillColor = capColor.Darkened(0.4f);
        Color rootColor = stemColor.Darkened(0.25f);

        var capMat = new StandardMaterial3D { AlbedoColor = capColor, Roughness = 0.8f };
        var capMatDark = new StandardMaterial3D { AlbedoColor = capColor.Darkened(0.15f), Roughness = 0.85f };
        var stemMat = new StandardMaterial3D { AlbedoColor = stemColor, Roughness = 0.9f };
        var stemMatDark = new StandardMaterial3D { AlbedoColor = stemColorDark, Roughness = 0.85f };
        var spotMat = new StandardMaterial3D { AlbedoColor = spotColor, Roughness = 0.7f };
        var spotMatCream = new StandardMaterial3D { AlbedoColor = spotColorCream, Roughness = 0.75f };
        var gillMat = new StandardMaterial3D { AlbedoColor = gillColor, Roughness = 0.95f };
        var rootMat = new StandardMaterial3D { AlbedoColor = rootColor, Roughness = 0.95f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.8f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.8f, 0.3f) * 0.3f
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        var mouthMat = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.15f, 0.15f) };
        var sporeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.9f, 0.5f, 0.7f),
            EmissionEnabled = true,
            Emission = new Color(0.6f, 0.8f, 0.3f) * 0.3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        // === BODY GEOMETRY ===
        float stemTopRadius = 0.12f;
        float stemBottomRadius = 0.18f;
        float stemHeight = 0.5f;
        float stemCenterY = 0.25f;
        float stemTop = stemCenterY + stemHeight / 2f;
        float capRadius = 0.35f;
        float capHeight = 0.7f;
        float capScaleY = 0.5f;

        // === LUMPY ORGANIC STEM ===
        // Main stem cylinder with slight taper
        var stem = new MeshInstance3D();
        var stemMesh = new CylinderMesh { TopRadius = stemTopRadius, BottomRadius = stemBottomRadius, Height = stemHeight };
        stem.Mesh = stemMesh;
        stem.MaterialOverride = stemMat;
        stem.Position = new Vector3(0, stemCenterY, 0);
        parent.AddChild(stem);

        // Lumpy bulges along stem for organic look (using spheres instead of torus)
        float[] bulgeHeights = { 0.08f, 0.18f, 0.28f, 0.38f };
        float[] bulgeSizes = { 0.16f, 0.14f, 0.13f, 0.12f };
        for (int i = 0; i < bulgeHeights.Length; i++)
        {
            float bulgeY = bulgeHeights[i];
            float bulgeSize = bulgeSizes[i];
            // Main bulge ring using flattened sphere
            var bulgeMesh = new SphereMesh { Radius = bulgeSize, Height = bulgeSize * 2f };
            var bulgeNode = new MeshInstance3D { Mesh = bulgeMesh, MaterialOverride = stemMat };
            bulgeNode.Position = new Vector3(0, bulgeY, 0);
            bulgeNode.Scale = new Vector3(1.1f, 0.25f, 1.1f);
            parent.AddChild(bulgeNode);
        }

        // Large basal bulge at stem base
        var baseBulge = new MeshInstance3D();
        var baseBulgeMesh = new SphereMesh { Radius = 0.18f, Height = 0.36f };
        baseBulge.Mesh = baseBulgeMesh;
        baseBulge.MaterialOverride = stemMat;
        baseBulge.Position = new Vector3(0, 0.03f, 0);
        baseBulge.Scale = new Vector3(1.3f, 0.5f, 1.3f);
        parent.AddChild(baseBulge);

        // Asymmetric side bulges for organic feel
        var sideBulge1 = new MeshInstance3D();
        sideBulge1.Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f };
        sideBulge1.MaterialOverride = stemMat;
        sideBulge1.Position = new Vector3(0.1f, 0.22f, 0.03f);
        sideBulge1.Scale = new Vector3(1f, 0.8f, 1f);
        parent.AddChild(sideBulge1);

        var sideBulge2 = new MeshInstance3D();
        sideBulge2.Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        sideBulge2.MaterialOverride = stemMat;
        sideBulge2.Position = new Vector3(-0.08f, 0.15f, -0.05f);
        sideBulge2.Scale = new Vector3(1f, 0.9f, 1f);
        parent.AddChild(sideBulge2);

        // === ROOT TENDRILS AT BASE ===
        int numRoots = 6;
        for (int i = 0; i < numRoots; i++)
        {
            float angle = i * Mathf.Pi * 2f / numRoots + (i % 2) * 0.3f;
            float rootLength = 0.12f + (i % 3) * 0.04f;
            float rootRadius = 0.015f + (i % 2) * 0.005f;

            // Root base cylinder spreading outward
            var rootNode = new Node3D();
            rootNode.Position = new Vector3(Mathf.Cos(angle) * 0.12f, 0.02f, Mathf.Sin(angle) * 0.12f);
            rootNode.RotationDegrees = new Vector3(60 + (i % 3) * 10, -Mathf.RadToDeg(angle), 0);
            parent.AddChild(rootNode);

            var rootMesh = new CylinderMesh { TopRadius = rootRadius * 0.5f, BottomRadius = rootRadius, Height = rootLength };
            var root = new MeshInstance3D { Mesh = rootMesh, MaterialOverride = rootMat };
            root.Position = new Vector3(0, -rootLength / 2f, 0);
            rootNode.AddChild(root);

            // Root tip bulge
            var rootTipMesh = new SphereMesh { Radius = rootRadius * 0.6f, Height = rootRadius * 1.2f };
            var rootTip = new MeshInstance3D { Mesh = rootTipMesh, MaterialOverride = rootMat };
            rootTip.Position = new Vector3(0, -rootLength, 0);
            rootNode.AddChild(rootTip);
        }

        // === STEM-CAP JOINT ===
        float jointRadius = stemTopRadius;
        float jointOverlap = CalculateOverlap(jointRadius);
        var stemCapJoint = CreateJointMesh(stemMat, jointRadius);
        stemCapJoint.Position = new Vector3(0, stemTop - jointOverlap * 0.3f, 0);
        parent.AddChild(stemCapJoint);

        // === MUSHROOM CAP (HEAD) ===
        float capCenterY = stemTop + (capHeight * capScaleY / 2f) - jointOverlap;
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, capCenterY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var cap = new MeshInstance3D();
        var capMesh = new SphereMesh { Radius = capRadius, Height = capHeight };
        cap.Mesh = capMesh;
        cap.MaterialOverride = capMat;
        cap.Scale = new Vector3(1f, capScaleY, 1f);
        headNode.AddChild(cap);

        // Cap edge/lip using flattened cylinder ring segments (replacing TorusMesh)
        int lipSegments = 16;
        float lipInnerR = 0.28f;
        float lipOuterR = 0.36f;
        for (int i = 0; i < lipSegments; i++)
        {
            float angle = i * Mathf.Pi * 2f / lipSegments;
            float midR = (lipInnerR + lipOuterR) / 2f;
            var lipSeg = new MeshInstance3D();
            var lipMesh = new BoxMesh { Size = new Vector3(0.06f, 0.04f, 0.04f) };
            lipSeg.Mesh = lipMesh;
            lipSeg.MaterialOverride = capMatDark;
            lipSeg.Position = new Vector3(Mathf.Cos(angle) * midR, -0.02f, Mathf.Sin(angle) * midR);
            lipSeg.RotationDegrees = new Vector3(0, -Mathf.RadToDeg(angle), 0);
            headNode.AddChild(lipSeg);
        }

        // === ENHANCED GILLS UNDER CAP ===
        int numGillRings = 3;
        int gillsPerRing = 20;
        for (int ring = 0; ring < numGillRings; ring++)
        {
            float ringRadius = 0.08f + ring * 0.06f;
            float gillHeight = 0.06f + ring * 0.02f;
            for (int i = 0; i < gillsPerRing; i++)
            {
                float angle = i * Mathf.Pi * 2f / gillsPerRing + ring * 0.1f;
                var gill = new MeshInstance3D();
                var gillMesh = new BoxMesh { Size = new Vector3(0.008f, gillHeight, 0.05f + ring * 0.02f) };
                gill.Mesh = gillMesh;
                gill.MaterialOverride = gillMat;
                gill.Position = new Vector3(Mathf.Cos(angle) * ringRadius, -0.04f - ring * 0.015f, Mathf.Sin(angle) * ringRadius);
                gill.RotationDegrees = new Vector3(5, -Mathf.RadToDeg(angle), 0);
                headNode.AddChild(gill);
            }
        }

        // === CAP SPOTS WITH DEPTH ===
        // Main spots - raised bumps
        float[] spotSizes = { 0.055f, 0.04f, 0.065f, 0.045f, 0.05f, 0.035f, 0.06f, 0.032f, 0.048f, 0.038f };
        float[] spotAngles = { 0, 0.8f, 1.5f, 2.3f, 3.1f, 3.8f, 4.6f, 5.3f, 5.9f, 0.4f };
        float[] spotRadii = { 0.18f, 0.24f, 0.16f, 0.21f, 0.13f, 0.27f, 0.19f, 0.23f, 0.11f, 0.25f };
        float[] spotHeightOffsets = { 0.12f, 0.09f, 0.14f, 0.08f, 0.16f, 0.06f, 0.11f, 0.07f, 0.17f, 0.1f };

        for (int i = 0; i < spotSizes.Length; i++)
        {
            // Raised spot (main body)
            float spotR = spotSizes[i];
            var spot = new MeshInstance3D();
            var spotMesh = new SphereMesh { Radius = spotR, Height = spotR * 2f };
            spot.Mesh = spotMesh;
            spot.MaterialOverride = (i % 3 == 0) ? spotMat : spotMatCream;
            spot.Position = new Vector3(
                Mathf.Cos(spotAngles[i]) * spotRadii[i],
                spotHeightOffsets[i],
                Mathf.Sin(spotAngles[i]) * spotRadii[i]
            );
            spot.Scale = new Vector3(1f, 0.35f, 1f); // Flattened for raised look
            headNode.AddChild(spot);

            // Darker ring around spot for depth illusion
            var spotRing = new MeshInstance3D();
            var spotRingMesh = new SphereMesh { Radius = spotR * 1.15f, Height = spotR * 2.3f };
            spotRing.Mesh = spotRingMesh;
            spotRing.MaterialOverride = capMatDark;
            spotRing.Position = spot.Position - new Vector3(0, 0.005f, 0);
            spotRing.Scale = new Vector3(1f, 0.15f, 1f);
            headNode.AddChild(spotRing);
        }

        // Top crown spot
        var topSpot = new MeshInstance3D();
        var topSpotMesh = new SphereMesh { Radius = 0.055f, Height = 0.11f };
        topSpot.Mesh = topSpotMesh;
        topSpot.MaterialOverride = spotMat;
        topSpot.Position = new Vector3(0, 0.19f, 0);
        topSpot.Scale = new Vector3(1f, 0.4f, 1f);
        headNode.AddChild(topSpot);

        // === FACE FEATURES ON STEM ===
        // Eyes - using standardized Cute proportions
        float mushroomEyeRef = stemTopRadius * 2f;
        var (mushEyeR, mushPupilR, _, mushEyeX, _, _) = CalculateEyeGeometry(mushroomEyeRef, EyeProportions.Cute);
        float mushEyeY = 0.38f;
        float mushEyeZ = 0.09f;
        float mushPupilZ = mushEyeZ + mushEyeR * 0.75f;

        var eyeMesh = new SphereMesh { Radius = mushEyeR, Height = mushEyeR * 2f, RadialSegments = 16, Rings = 12 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-mushEyeX, mushEyeY, mushEyeZ);
        parent.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(mushEyeX, mushEyeY, mushEyeZ);
        parent.AddChild(rightEye);

        // Eye highlights (small white spheres)
        var highlightMat = new StandardMaterial3D { AlbedoColor = Colors.White };
        var highlightMesh = new SphereMesh { Radius = mushEyeR * 0.25f, Height = mushEyeR * 0.5f };
        var leftHighlight = new MeshInstance3D { Mesh = highlightMesh, MaterialOverride = highlightMat };
        leftHighlight.Position = new Vector3(-mushEyeX + mushEyeR * 0.3f, mushEyeY + mushEyeR * 0.25f, mushEyeZ + mushEyeR * 0.7f);
        parent.AddChild(leftHighlight);
        var rightHighlight = new MeshInstance3D { Mesh = highlightMesh, MaterialOverride = highlightMat };
        rightHighlight.Position = new Vector3(mushEyeX + mushEyeR * 0.3f, mushEyeY + mushEyeR * 0.25f, mushEyeZ + mushEyeR * 0.7f);
        parent.AddChild(rightHighlight);

        // Pupils
        var pupilMesh = new SphereMesh { Radius = mushPupilR, Height = mushPupilR * 2f, RadialSegments = 12, Rings = 8 };
        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-mushEyeX, mushEyeY, mushPupilZ);
        parent.AddChild(leftPupil);

        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(mushEyeX, mushEyeY, mushPupilZ);
        parent.AddChild(rightPupil);

        // Creepy mouth on stem
        var mouth = new MeshInstance3D();
        var mouthMesh = new SphereMesh { Radius = 0.035f, Height = 0.07f };
        mouth.Mesh = mouthMesh;
        mouth.MaterialOverride = mouthMat;
        mouth.Position = new Vector3(0, 0.28f, 0.12f);
        mouth.Scale = new Vector3(1.8f, 0.5f, 0.7f);
        parent.AddChild(mouth);

        // Mouth inner darkness
        var mouthInner = new MeshInstance3D();
        var mouthInnerMesh = new SphereMesh { Radius = 0.02f, Height = 0.04f };
        mouthInner.Mesh = mouthInnerMesh;
        mouthInner.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.05f, 0.05f) };
        mouthInner.Position = new Vector3(0, 0.28f, 0.115f);
        mouthInner.Scale = new Vector3(1.5f, 0.4f, 0.5f);
        parent.AddChild(mouthInner);

        // === LITTLE ARMS (using cylinder instead of capsule) ===
        float armRadius = 0.025f;
        float armLength = 0.12f;
        var armMesh = new CylinderMesh { TopRadius = armRadius * 0.7f, BottomRadius = armRadius, Height = armLength };

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.12f, 0.28f, 0.02f);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -45);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;
        var leftArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = stemMat };
        leftArmNode.AddChild(leftArm);

        // Left hand
        var handMesh = new SphereMesh { Radius = 0.022f, Height = 0.044f };
        var leftHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = stemMat };
        leftHand.Position = new Vector3(0, -0.07f, 0);
        leftArmNode.AddChild(leftHand);

        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.12f, 0.28f, 0.02f);
        rightArmNode.RotationDegrees = new Vector3(0, 0, 45);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;
        var rightArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = stemMat };
        rightArmNode.AddChild(rightArm);

        var rightHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = stemMat };
        rightHand.Position = new Vector3(0, -0.07f, 0);
        rightArmNode.AddChild(rightHand);

        // === LITTLE LEGS WITH FEET ===
        var legMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.15f };
        var footMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.1f, 0.08f, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;
        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = stemMat };
        leftLegNode.AddChild(leftLeg);
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = stemMat };
        leftFoot.Position = new Vector3(0, -0.08f, 0.02f);
        leftFoot.Scale = new Vector3(1, 0.5f, 1.3f);
        leftLegNode.AddChild(leftFoot);

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.1f, 0.08f, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;
        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = stemMat };
        rightLegNode.AddChild(rightLeg);
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = stemMat };
        rightFoot.Position = new Vector3(0, -0.08f, 0.02f);
        rightFoot.Scale = new Vector3(1, 0.5f, 1.3f);
        rightLegNode.AddChild(rightFoot);

        // === ENHANCED SPORE PARTICLES ===
        // Multiple layers of floating spores at different distances
        int[] sporeCountPerLayer = { 5, 7, 4 };
        float[] sporeLayerRadii = { 0.35f, 0.5f, 0.65f };
        float[] sporeLayerBaseY = { 0.35f, 0.5f, 0.3f };
        float[] sporeSizes = { 0.018f, 0.012f, 0.015f };

        for (int layer = 0; layer < sporeCountPerLayer.Length; layer++)
        {
            int count = sporeCountPerLayer[layer];
            float radius = sporeLayerRadii[layer];
            float baseY = sporeLayerBaseY[layer];
            float size = sporeSizes[layer];

            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.Pi * 2f / count + layer * 0.5f;
                var spore = new MeshInstance3D();
                var sporeMesh = new SphereMesh { Radius = size, Height = size * 2f };
                spore.Mesh = sporeMesh;
                spore.MaterialOverride = sporeMat;
                spore.Position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    baseY + i * 0.06f + layer * 0.1f,
                    Mathf.Sin(angle) * radius
                );
                parent.AddChild(spore);
            }
        }

        // Extra large ambient spores floating higher
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.Pi * 2f / 3f + 1.2f;
            var bigSpore = new MeshInstance3D();
            var bigSporeMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f };
            bigSpore.Mesh = bigSporeMesh;
            bigSpore.MaterialOverride = sporeMat;
            bigSpore.Position = new Vector3(
                Mathf.Cos(angle) * 0.55f,
                0.75f + i * 0.12f,
                Mathf.Sin(angle) * 0.55f
            );
            parent.AddChild(bigSpore);
        }
    }

    /// <summary>
    /// Create an anatomically accurate Spider mesh with proper body segments, 8 legs,
    /// 8 eyes, chelicerae (fangs), pedipalps, and spinnerets.
    /// Supports size/color variation and multiple abdomen pattern types.
    /// </summary>
    private static void CreateSpiderMesh(Node3D parent, LimbNodes limbs, Color? colorOverride)
    {
        // Default color with variation support
        Color bodyColor = colorOverride ?? new Color(0.2f, 0.2f, 0.25f);

        // Add random color variation for natural look
        Random rng = new Random();
        float colorVariation = (float)(rng.NextDouble() * 0.1 - 0.05); // ±5% variation
        bodyColor = new Color(
            Mathf.Clamp(bodyColor.R + colorVariation, 0f, 1f),
            Mathf.Clamp(bodyColor.G + colorVariation, 0f, 1f),
            Mathf.Clamp(bodyColor.B + colorVariation, 0f, 1f)
        );

        Color hairColor = bodyColor.Lightened(0.15f);
        Color patternColor = bodyColor.Lightened(0.3f);
        Color darkBodyColor = bodyColor.Darkened(0.2f);

        // Size variation (0.85x to 1.15x)
        float sizeVariation = 0.85f + (float)(rng.NextDouble() * 0.3);

        // Leg thickness variation
        float legThickness = 0.9f + (float)(rng.NextDouble() * 0.2);

        // Pattern type (0-2: different abdomen patterns)
        int patternType = rng.Next(0, 3);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.7f };
        var hairMat = new StandardMaterial3D { AlbedoColor = hairColor, Roughness = 0.9f };
        var darkBodyMat = new StandardMaterial3D { AlbedoColor = darkBodyColor, Roughness = 0.75f };
        var patternMat = new StandardMaterial3D { AlbedoColor = patternColor, Roughness = 0.8f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.2f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.2f) * 0.5f,
            Metallic = 0.3f
        };
        var fangMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.08f, 0.08f),
            Metallic = 0.3f,
            Roughness = 0.5f
        };

        // === BODY GEOMETRY ===
        float scale = sizeVariation;
        float abdomenRadius = 0.24f * scale;
        float abdomenHeight = 0.48f * scale;
        float abdomenCenterY = 0.32f * scale;
        float abdomenCenterZ = -0.24f * scale;
        float abdomenFrontZ = abdomenCenterZ + abdomenRadius * 1.18f / 2f;  // Account for Z scale

        // Cephalothorax (head) geometry
        float cephaloRadius = 0.17f * scale;
        float cephaloHeight = 0.34f * scale;
        float cephaloCenterZ = 0.09f * scale;

        // ABDOMEN (back body section) - larger, bulbous, anatomically accurate
        var abdomen = new MeshInstance3D();
        var abdomenMesh = new SphereMesh { Radius = abdomenRadius, Height = abdomenHeight, RadialSegments = 28, Rings = 20 };
        abdomen.Mesh = abdomenMesh;
        abdomen.MaterialOverride = bodyMat;
        abdomen.Position = new Vector3(0, abdomenCenterY, abdomenCenterZ);
        abdomen.Scale = new Vector3(0.95f, 0.75f, 1.18f); // More bulbous
        parent.AddChild(abdomen);

        // Abdomen breathing/animation offset
        abdomen.Position += new Vector3(0, (float)(rng.NextDouble() * 0.01 - 0.005), 0);

        // Abdomen pattern markings (3 different pattern types)
        switch (patternType)
        {
            case 0: // Classic stripe pattern
                var stripeMesh = new SphereMesh { Radius = 0.07f * sizeVariation, Height = 0.14f * sizeVariation, RadialSegments = 16, Rings = 12 };
                for (int i = 0; i < 3; i++)
                {
                    var stripe = new MeshInstance3D { Mesh = stripeMesh, MaterialOverride = patternMat };
                    stripe.Position = new Vector3(0, 0.38f * sizeVariation, (-0.28f - i * 0.08f) * sizeVariation);
                    stripe.Scale = new Vector3(0.8f, 0.4f, 0.6f);
                    parent.AddChild(stripe);
                }
                break;
            case 1: // Spotted pattern (widow-like)
                var spotMesh = new SphereMesh { Radius = 0.05f * sizeVariation, Height = 0.1f * sizeVariation };
                var centerSpot = new MeshInstance3D { Mesh = spotMesh, MaterialOverride = patternMat };
                centerSpot.Position = new Vector3(0, 0.4f * sizeVariation, -0.32f * sizeVariation);
                parent.AddChild(centerSpot);
                // Side spots
                var leftSpot = new MeshInstance3D { Mesh = spotMesh, MaterialOverride = patternMat };
                leftSpot.Position = new Vector3(-0.08f * sizeVariation, 0.36f * sizeVariation, -0.38f * sizeVariation);
                leftSpot.Scale = new Vector3(0.7f, 0.7f, 0.7f);
                parent.AddChild(leftSpot);
                var rightSpot = new MeshInstance3D { Mesh = spotMesh, MaterialOverride = patternMat };
                rightSpot.Position = new Vector3(0.08f * sizeVariation, 0.36f * sizeVariation, -0.38f * sizeVariation);
                rightSpot.Scale = new Vector3(0.7f, 0.7f, 0.7f);
                parent.AddChild(rightSpot);
                break;
            case 2: // Banded pattern (tarantula-like)
                var bandMesh = new CylinderMesh { TopRadius = 0.18f * sizeVariation, BottomRadius = 0.18f * sizeVariation, Height = 0.06f * sizeVariation };
                var band1 = new MeshInstance3D { Mesh = bandMesh, MaterialOverride = darkBodyMat };
                band1.Position = new Vector3(0, 0.38f * sizeVariation, -0.3f * sizeVariation);
                band1.RotationDegrees = new Vector3(90, 0, 0);
                band1.Scale = new Vector3(0.9f, 1.2f, 0.7f);
                parent.AddChild(band1);
                var band2 = new MeshInstance3D { Mesh = bandMesh, MaterialOverride = darkBodyMat };
                band2.Position = new Vector3(0, 0.32f * sizeVariation, -0.4f * sizeVariation);
                band2.RotationDegrees = new Vector3(90, 0, 0);
                band2.Scale = new Vector3(0.85f, 1f, 0.6f);
                parent.AddChild(band2);
                break;
        }

        // Spinnerets at rear (3 pairs for anatomical accuracy)
        var spinneretMesh = new CylinderMesh { TopRadius = 0.012f * sizeVariation, BottomRadius = 0.022f * sizeVariation, Height = 0.05f * sizeVariation, RadialSegments = 8 };
        for (int i = 0; i < 3; i++)
        {
            float xOffset = (i - 1) * 0.025f * sizeVariation;
            var spinneret = new MeshInstance3D { Mesh = spinneretMesh, MaterialOverride = bodyMat };
            spinneret.Position = new Vector3(xOffset, 0.26f * sizeVariation, -0.42f * sizeVariation);
            spinneret.RotationDegrees = new Vector3(-75 + i * 5, 0, 0);
            parent.AddChild(spinneret);
        }

        // === PEDICEL JOINT (narrow waist between abdomen and cephalothorax) ===
        float pedicelRadius = 0.06f * scale;
        float pedicelZ = (abdomenFrontZ + cephaloCenterZ) / 2f;  // Midpoint
        var pedicelJoint = CreateJointMesh(bodyMat, pedicelRadius);
        pedicelJoint.Position = new Vector3(0, abdomenCenterY, pedicelZ);
        parent.AddChild(pedicelJoint);

        // CEPHALOTHORAX (front body/head section) - proper shape and detail
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, abdomenCenterY, cephaloCenterZ);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var cephalo = new MeshInstance3D();
        var cephaloMesh = new SphereMesh { Radius = cephaloRadius, Height = cephaloHeight, RadialSegments = 24, Rings = 18 };
        cephalo.Mesh = cephaloMesh;
        cephalo.MaterialOverride = bodyMat;
        cephalo.Scale = new Vector3(1.05f, 0.8f, 1.15f); // Wider, flatter
        headNode.AddChild(cephalo);

        // Carapace detail (darker top section)
        var carapace = new MeshInstance3D();
        var carapaceMesh = new SphereMesh { Radius = 0.14f * sizeVariation, Height = 0.28f * sizeVariation, RadialSegments = 20, Rings = 14 };
        carapace.Mesh = carapaceMesh;
        carapace.MaterialOverride = darkBodyMat;
        carapace.Position = new Vector3(0, 0.04f, 0);
        carapace.Scale = new Vector3(0.9f, 0.6f, 0.95f);
        headNode.AddChild(carapace);

        // 8 EYES - proper spider arrangement (2 large primary, 6 smaller secondary)
        // CRITICAL: Height must equal 2*Radius for proper spheres
        var largeEyeMesh = new SphereMesh { Radius = 0.032f * sizeVariation, Height = 0.064f * sizeVariation, RadialSegments = 20, Rings = 14 };
        var mediumEyeMesh = new SphereMesh { Radius = 0.024f * sizeVariation, Height = 0.048f * sizeVariation, RadialSegments = 16, Rings = 12 };
        var smallEyeMesh = new SphereMesh { Radius = 0.018f * sizeVariation, Height = 0.036f * sizeVariation, RadialSegments = 12, Rings = 10 };

        // Front row - 2 large principal eyes (forward-facing)
        var eye1 = new MeshInstance3D { Mesh = largeEyeMesh, MaterialOverride = eyeMat };
        eye1.Position = new Vector3(-0.045f * sizeVariation, 0.05f, 0.13f * sizeVariation);
        headNode.AddChild(eye1);
        var eye2 = new MeshInstance3D { Mesh = largeEyeMesh, MaterialOverride = eyeMat };
        eye2.Position = new Vector3(0.045f * sizeVariation, 0.05f, 0.13f * sizeVariation);
        headNode.AddChild(eye2);

        // Middle row - 2 medium lateral eyes
        var eye3 = new MeshInstance3D { Mesh = mediumEyeMesh, MaterialOverride = eyeMat };
        eye3.Position = new Vector3(-0.08f * sizeVariation, 0.07f, 0.09f * sizeVariation);
        headNode.AddChild(eye3);
        var eye4 = new MeshInstance3D { Mesh = mediumEyeMesh, MaterialOverride = eyeMat };
        eye4.Position = new Vector3(0.08f * sizeVariation, 0.07f, 0.09f * sizeVariation);
        headNode.AddChild(eye4);

        // Back row - 4 small posterior eyes
        var eye5 = new MeshInstance3D { Mesh = smallEyeMesh, MaterialOverride = eyeMat };
        eye5.Position = new Vector3(-0.07f * sizeVariation, 0.1f, 0.04f * sizeVariation);
        headNode.AddChild(eye5);
        var eye6 = new MeshInstance3D { Mesh = smallEyeMesh, MaterialOverride = eyeMat };
        eye6.Position = new Vector3(-0.025f * sizeVariation, 0.11f, 0.05f * sizeVariation);
        headNode.AddChild(eye6);
        var eye7 = new MeshInstance3D { Mesh = smallEyeMesh, MaterialOverride = eyeMat };
        eye7.Position = new Vector3(0.025f * sizeVariation, 0.11f, 0.05f * sizeVariation);
        headNode.AddChild(eye7);
        var eye8 = new MeshInstance3D { Mesh = smallEyeMesh, MaterialOverride = eyeMat };
        eye8.Position = new Vector3(0.07f * sizeVariation, 0.1f, 0.04f * sizeVariation);
        headNode.AddChild(eye8);

        // CHELICERAE (fangs) - anatomically accurate with joints
        // Base chelicerae segment
        var cheliceraeMesh = new CylinderMesh { TopRadius = 0.018f * sizeVariation, BottomRadius = 0.024f * sizeVariation, Height = 0.06f * sizeVariation, RadialSegments = 12 };
        var leftChelicerae = new MeshInstance3D { Mesh = cheliceraeMesh, MaterialOverride = darkBodyMat };
        leftChelicerae.Position = new Vector3(-0.035f * sizeVariation, -0.01f, 0.13f * sizeVariation);
        leftChelicerae.RotationDegrees = new Vector3(15, 0, 0);
        headNode.AddChild(leftChelicerae);
        var rightChelicerae = new MeshInstance3D { Mesh = cheliceraeMesh, MaterialOverride = darkBodyMat };
        rightChelicerae.Position = new Vector3(0.035f * sizeVariation, -0.01f, 0.13f * sizeVariation);
        rightChelicerae.RotationDegrees = new Vector3(15, 0, 0);
        headNode.AddChild(rightChelicerae);

        // Fangs (curved, pointed)
        var fangMesh = new CylinderMesh { TopRadius = 0.006f * sizeVariation, BottomRadius = 0.016f * sizeVariation, Height = 0.09f * sizeVariation, RadialSegments = 10 };
        var leftFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = fangMat };
        leftFang.Position = new Vector3(-0.035f * sizeVariation, -0.04f, 0.14f * sizeVariation);
        leftFang.RotationDegrees = new Vector3(30, 0, 8);
        headNode.AddChild(leftFang);
        var rightFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = fangMat };
        rightFang.Position = new Vector3(0.035f * sizeVariation, -0.04f, 0.14f * sizeVariation);
        rightFang.RotationDegrees = new Vector3(30, 0, -8);
        headNode.AddChild(rightFang);

        // PEDIPALPS (sensory appendages near mouth) - 2 segments each
        var palpSegment1Mesh = new CylinderMesh { TopRadius = 0.014f * sizeVariation, BottomRadius = 0.019f * sizeVariation, Height = 0.07f * sizeVariation, RadialSegments = 10 };
        var palpSegment2Mesh = new CylinderMesh { TopRadius = 0.01f * sizeVariation, BottomRadius = 0.013f * sizeVariation, Height = 0.06f * sizeVariation, RadialSegments = 8 };

        // Left pedipalp
        var leftPalp1 = new MeshInstance3D { Mesh = palpSegment1Mesh, MaterialOverride = bodyMat };
        leftPalp1.Position = new Vector3(-0.07f * sizeVariation, -0.01f, 0.11f * sizeVariation);
        leftPalp1.RotationDegrees = new Vector3(-25, -15, 0);
        headNode.AddChild(leftPalp1);
        var leftPalp2 = new MeshInstance3D { Mesh = palpSegment2Mesh, MaterialOverride = bodyMat };
        leftPalp2.Position = new Vector3(-0.1f * sizeVariation, -0.04f, 0.13f * sizeVariation);
        leftPalp2.RotationDegrees = new Vector3(-35, -10, 0);
        headNode.AddChild(leftPalp2);

        // Right pedipalp
        var rightPalp1 = new MeshInstance3D { Mesh = palpSegment1Mesh, MaterialOverride = bodyMat };
        rightPalp1.Position = new Vector3(0.07f * sizeVariation, -0.01f, 0.11f * sizeVariation);
        rightPalp1.RotationDegrees = new Vector3(-25, 15, 0);
        headNode.AddChild(rightPalp1);
        var rightPalp2 = new MeshInstance3D { Mesh = palpSegment2Mesh, MaterialOverride = bodyMat };
        rightPalp2.Position = new Vector3(0.1f * sizeVariation, -0.04f, 0.13f * sizeVariation);
        rightPalp2.RotationDegrees = new Vector3(-35, 10, 0);
        headNode.AddChild(rightPalp2);

        // 8 LEGS - anatomically accurate with 3 segments each (coxa, femur, tarsus)
        // Proper spider leg placement and proportions
        var coxaMesh = new CylinderMesh {
            TopRadius = 0.026f * sizeVariation * legThickness,
            BottomRadius = 0.022f * sizeVariation * legThickness,
            Height = 0.18f * sizeVariation,
            RadialSegments = 14, Rings = 6
        };
        var femurMesh = new CylinderMesh {
            TopRadius = 0.022f * sizeVariation * legThickness,
            BottomRadius = 0.015f * sizeVariation * legThickness,
            Height = 0.24f * sizeVariation,
            RadialSegments = 12, Rings = 8
        };
        var tarsusMesh = new CylinderMesh {
            TopRadius = 0.013f * sizeVariation * legThickness,
            BottomRadius = 0.008f * sizeVariation * legThickness,
            Height = 0.14f * sizeVariation,
            RadialSegments = 10, Rings = 6
        };

        // Leg positions (front to back attachment points)
        float[] legZPositions = { 0.07f, 0.02f, -0.04f, -0.1f };
        float[] legOutAngles = { 60f, 45f, 45f, 60f }; // Spread angle
        float[] legForwardAngles = { 25f, 10f, -5f, -25f }; // Forward/back tilt

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                // Main leg node (root joint)
                var legNode = new Node3D();
                legNode.Position = new Vector3(side * 0.11f * sizeVariation, 0.32f * sizeVariation, legZPositions[i] * sizeVariation);
                legNode.RotationDegrees = new Vector3(legForwardAngles[i], 0, side * legOutAngles[i]);
                parent.AddChild(legNode);

                // Coxa (base segment attached to body)
                var coxa = new MeshInstance3D { Mesh = coxaMesh, MaterialOverride = bodyMat };
                coxa.Position = new Vector3(side * 0.09f * sizeVariation, 0, 0);
                legNode.AddChild(coxa);

                // Coxa-femur joint
                var joint1Mesh = new SphereMesh { Radius = 0.024f * sizeVariation * legThickness, Height = 0.048f * sizeVariation * legThickness, RadialSegments = 12, Rings = 8 };
                var joint1 = new MeshInstance3D { Mesh = joint1Mesh, MaterialOverride = darkBodyMat };
                joint1.Position = new Vector3(side * 0.18f * sizeVariation, -0.01f, 0);
                legNode.AddChild(joint1);

                // Femur (main leg segment)
                var femur = new MeshInstance3D { Mesh = femurMesh, MaterialOverride = bodyMat };
                femur.Position = new Vector3(side * 0.3f * sizeVariation, -0.08f, 0);
                femur.RotationDegrees = new Vector3(0, 0, side * -22);
                legNode.AddChild(femur);

                // Femur-tarsus joint
                var joint2Mesh = new SphereMesh { Radius = 0.018f * sizeVariation * legThickness, Height = 0.036f * sizeVariation * legThickness, RadialSegments = 10, Rings = 6 };
                var joint2 = new MeshInstance3D { Mesh = joint2Mesh, MaterialOverride = darkBodyMat };
                joint2.Position = new Vector3(side * 0.41f * sizeVariation, -0.17f, 0);
                legNode.AddChild(joint2);

                // Tarsus (foot/tip segment)
                var tarsus = new MeshInstance3D { Mesh = tarsusMesh, MaterialOverride = bodyMat };
                tarsus.Position = new Vector3(side * 0.48f * sizeVariation, -0.25f, 0);
                tarsus.RotationDegrees = new Vector3(0, 0, side * -30);
                legNode.AddChild(tarsus);

                // Foot claws (tiny hooks at tip)
                var clawMesh = new CylinderMesh { TopRadius = 0.002f * sizeVariation, BottomRadius = 0.005f * sizeVariation, Height = 0.02f * sizeVariation };
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = fangMat };
                claw.Position = new Vector3(side * 0.55f * sizeVariation, -0.32f, 0);
                claw.RotationDegrees = new Vector3(0, 0, side * -45);
                legNode.AddChild(claw);

                // Hair/spines on legs (detailed)
                for (int h = 0; h < 4; h++)
                {
                    var hairMesh = new CylinderMesh {
                        TopRadius = 0.001f * sizeVariation,
                        BottomRadius = 0.003f * sizeVariation,
                        Height = 0.025f * sizeVariation,
                        RadialSegments = 6
                    };
                    var hair = new MeshInstance3D { Mesh = hairMesh, MaterialOverride = hairMat };
                    float hairPos = 0.1f + h * 0.12f;
                    hair.Position = new Vector3(side * hairPos * sizeVariation, 0.01f, (float)(rng.NextDouble() * 0.01 - 0.005));
                    hair.RotationDegrees = new Vector3(0, 0, side * 15 + (float)(rng.NextDouble() * 10));
                    legNode.AddChild(hair);
                }

                // Store first and last pair for animation
                if (i == 0)
                {
                    if (side == -1) limbs.LeftArm = legNode;
                    else limbs.RightArm = legNode;
                }
                else if (i == 3)
                {
                    if (side == -1) limbs.LeftLeg = legNode;
                    else limbs.RightLeg = legNode;
                }
            }
        }
    }

    private static void CreateLizardMesh(Node3D parent, LimbNodes limbs, Color? colorOverride)
    {
        // Variation parameters
        var random = new Random();
        float sizeVariation = 0.9f + (float)random.NextDouble() * 0.25f; // 0.9-1.15x
        bool hasFrill = random.NextDouble() > 0.5f;
        float spineRidgeSize = 0.8f + (float)random.NextDouble() * 0.4f; // 0.8-1.2x
        float tailLength = 0.9f + (float)random.NextDouble() * 0.3f; // 0.9-1.2x

        // Color variations
        Color scaleColor = colorOverride ?? GetRandomLizardScaleColor(random);
        Color bellyColor = scaleColor.Lightened(0.3f);
        Color clawColor = new Color(0.2f, 0.15f, 0.1f);
        Color tongueColor = new Color(0.9f, 0.3f, 0.4f);

        // LOD determination (based on distance - placeholder, would be set by caller)
        int lodLevel = 0; // 0 = high, 1 = medium, 2 = low

        var scaleMat = new StandardMaterial3D { AlbedoColor = scaleColor, Roughness = 0.6f, Metallic = 0.1f };
        var bellyMat = new StandardMaterial3D { AlbedoColor = bellyColor, Roughness = 0.7f };
        var darkScaleMat = new StandardMaterial3D { AlbedoColor = scaleColor.Darkened(0.2f), Roughness = 0.5f, Metallic = 0.15f };
        var clawMat = new StandardMaterial3D { AlbedoColor = clawColor, Roughness = 0.4f };
        var tongueMat = new StandardMaterial3D { AlbedoColor = tongueColor, Roughness = 0.3f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.8f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.7f, 0.1f) * 0.3f
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };

        // === BODY GEOMETRY ===
        float scale = sizeVariation;
        float bodyRadius = 0.2f * scale;
        float bodyHeight = 0.6f * scale;
        float bodyCenterY = 0.5f * scale;
        float bodyScaleY = 1f;
        var bodyGeom = new BodyGeometry(bodyCenterY, bodyRadius, bodyHeight, bodyScaleY);

        // Head geometry
        float headRadius = 0.15f * scale;
        float headHeight = 0.3f * scale;

        // Body - muscular humanoid torso
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = bodyRadius, Height = bodyHeight };
        body.Mesh = bodyMesh;
        body.MaterialOverride = scaleMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.Scale = new Vector3(0.8f, bodyScaleY, 1f);
        parent.AddChild(body);

        // Belly - lighter colored underbelly
        var belly = new MeshInstance3D();
        var bellyMesh = new CapsuleMesh { Radius = 0.15f * sizeVariation, Height = 0.4f * sizeVariation };
        belly.Mesh = bellyMesh;
        belly.MaterialOverride = bellyMat;
        belly.Position = new Vector3(0, 0.45f * sizeVariation, 0.08f * sizeVariation);
        belly.Scale = new Vector3(0.8f, 1f, 0.6f);
        parent.AddChild(belly);

        // Enhanced spine ridges along back (LOD high/medium only)
        if (lodLevel < 2)
        {
            int ridgeCount = lodLevel == 0 ? 8 : 5; // More detail at high LOD
            for (int i = 0; i < ridgeCount; i++)
            {
                var ridge = new MeshInstance3D();
                float ridgeSize = (0.005f + i * 0.002f) * spineRidgeSize * sizeVariation;
                var ridgeMesh = new CylinderMesh
                {
                    TopRadius = ridgeSize * 0.3f,
                    BottomRadius = ridgeSize * 1.2f,
                    Height = 0.06f * spineRidgeSize * sizeVariation
                };
                ridge.Mesh = ridgeMesh;
                ridge.MaterialOverride = darkScaleMat;
                ridge.Position = new Vector3(0, (0.65f - i * 0.05f) * sizeVariation, (-0.15f + i * 0.015f) * sizeVariation);
                ridge.RotationDegrees = new Vector3(-25 - i * 2, 0, 0);
                parent.AddChild(ridge);
            }
        }

        // Scale texture suggestion (visual detail bumps) - high LOD only
        if (lodLevel == 0)
        {
            // Add small scale bumps on shoulders and chest
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    var scaleBump = new MeshInstance3D();
                    var bumpMesh = new SphereMesh { Radius = 0.015f * sizeVariation, Height = 0.008f * sizeVariation };
                    scaleBump.Mesh = bumpMesh;
                    scaleBump.MaterialOverride = darkScaleMat;
                    scaleBump.Position = new Vector3(sx * (0.12f + i * 0.02f) * sizeVariation, (0.62f - i * 0.04f) * sizeVariation, 0.06f * sizeVariation);
                    parent.AddChild(scaleBump);
                }
            }
        }

        // === NECK JOINT ===
        float neckRadius = 0.1f * scale;
        float neckOverlap = CalculateOverlap(neckRadius);
        var neckJoint = CreateJointMesh(scaleMat, neckRadius);
        neckJoint.Position = new Vector3(0, bodyGeom.Top - neckOverlap * 0.5f, 0.05f * scale);
        parent.AddChild(neckJoint);

        // Head - reptilian with separate neck, positioned with overlap
        float headCenterY = bodyGeom.Top + headHeight / 2f - neckOverlap + 0.05f * scale;
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0.1f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Neck (separate for animation)
        var neck = new MeshInstance3D();
        var neckMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = neckRadius, Height = 0.12f * scale };
        neck.Mesh = neckMesh;
        neck.MaterialOverride = scaleMat;
        neck.Position = new Vector3(0, -0.08f * scale, -0.02f * scale);
        headNode.AddChild(neck);

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = headRadius, Height = headHeight };
        head.Mesh = headMesh;
        head.MaterialOverride = scaleMat;
        head.Scale = new Vector3(0.9f, 0.8f, 1.1f);
        headNode.AddChild(head);

        // Neck frill (optional variation)
        if (hasFrill && lodLevel < 2)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    var frillSegment = new MeshInstance3D();
                    var frillMesh = new CylinderMesh
                    {
                        TopRadius = 0.001f * sizeVariation,
                        BottomRadius = 0.025f * sizeVariation,
                        Height = 0.08f * sizeVariation
                    };
                    frillSegment.Mesh = frillMesh;
                    frillSegment.MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(scaleColor.Lightened(0.2f), 0.8f),
                        Roughness = 0.5f,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                    };
                    frillSegment.Position = new Vector3(side * (0.08f + i * 0.02f) * sizeVariation, -0.02f * sizeVariation, -0.05f * sizeVariation);
                    frillSegment.RotationDegrees = new Vector3(10, side * (60 + i * 15), 0);
                    headNode.AddChild(frillSegment);
                }
            }
        }

        // Head ridges - more prominent
        if (lodLevel < 2)
        {
            int ridgeCount = lodLevel == 0 ? 5 : 3;
            for (int i = 0; i < ridgeCount; i++)
            {
                var ridge = new MeshInstance3D();
                var ridgeMesh = new CylinderMesh
                {
                    TopRadius = (0.005f + i * 0.003f) * spineRidgeSize * sizeVariation,
                    BottomRadius = (0.012f + i * 0.005f) * spineRidgeSize * sizeVariation,
                    Height = 0.05f * spineRidgeSize * sizeVariation
                };
                ridge.Mesh = ridgeMesh;
                ridge.MaterialOverride = darkScaleMat;
                ridge.Position = new Vector3(0, (0.08f - i * 0.015f) * sizeVariation, (-0.02f - i * 0.03f) * sizeVariation);
                ridge.RotationDegrees = new Vector3(-15 - i * 5, 0, 0);
                headNode.AddChild(ridge);
            }
        }

        // Snout - prominent reptilian
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh
        {
            TopRadius = 0.04f * sizeVariation,
            BottomRadius = 0.08f * sizeVariation,
            Height = 0.15f * sizeVariation
        };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = scaleMat;
        snout.Position = new Vector3(0, -0.02f * sizeVariation, 0.12f * sizeVariation);
        snout.RotationDegrees = new Vector3(-70, 0, 0);
        headNode.AddChild(snout);

        // Dewlap (throat pouch) - extendable throat fan under chin
        if (lodLevel < 2)
        {
            var dewlapMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(scaleColor.Lightened(0.1f).R, scaleColor.Lightened(0.1f).G + 0.15f, scaleColor.Lightened(0.1f).B, 0.85f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.5f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            // Main dewlap membrane
            var dewlapMesh = new SphereMesh { Radius = 0.06f * sizeVariation, Height = 0.12f * sizeVariation, RadialSegments = 12, Rings = 8 };
            var dewlap = new MeshInstance3D { Mesh = dewlapMesh, MaterialOverride = dewlapMat };
            dewlap.Position = new Vector3(0, -0.1f * sizeVariation, 0.06f * sizeVariation);
            dewlap.Scale = new Vector3(0.6f, 1.2f, 0.4f); // Thin, vertical membrane
            headNode.AddChild(dewlap);

            // Dewlap support ridges (cartilage-like)
            if (lodLevel == 0)
            {
                var dewlapRidgeMesh = new CylinderMesh { TopRadius = 0.002f * sizeVariation, BottomRadius = 0.004f * sizeVariation, Height = 0.05f * sizeVariation, RadialSegments = 6 };
                for (int d = 0; d < 3; d++)
                {
                    var dewlapRidge = new MeshInstance3D { Mesh = dewlapRidgeMesh, MaterialOverride = scaleMat };
                    dewlapRidge.Position = new Vector3((d - 1) * 0.015f * sizeVariation, -0.08f * sizeVariation, 0.065f * sizeVariation);
                    dewlapRidge.RotationDegrees = new Vector3(15, 0, (d - 1) * 10);
                    headNode.AddChild(dewlapRidge);
                }
            }
        }

        // Nostrils - high LOD only
        if (lodLevel == 0)
        {
            var nostrilMesh = new SphereMesh { Radius = 0.012f * sizeVariation, Height = 0.024f * sizeVariation };
            var nostrilMat = new StandardMaterial3D { AlbedoColor = scaleColor.Darkened(0.4f) };
            var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
            leftNostril.Position = new Vector3(-0.025f * sizeVariation, 0.02f * sizeVariation, 0.22f * sizeVariation);
            headNode.AddChild(leftNostril);

            var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
            rightNostril.Position = new Vector3(0.025f * sizeVariation, 0.02f * sizeVariation, 0.22f * sizeVariation);
            headNode.AddChild(rightNostril);

            // Forked tongue - high LOD only
            var tongue = new MeshInstance3D();
            var tongueMesh = new CylinderMesh
            {
                TopRadius = 0.003f * sizeVariation,
                BottomRadius = 0.008f * sizeVariation,
                Height = 0.08f * sizeVariation
            };
            tongue.Mesh = tongueMesh;
            tongue.MaterialOverride = tongueMat;
            tongue.Position = new Vector3(0, -0.04f * sizeVariation, 0.2f * sizeVariation);
            tongue.RotationDegrees = new Vector3(-80, 0, 0);
            headNode.AddChild(tongue);

            // Tongue forks
            var forkMesh = new CylinderMesh
            {
                TopRadius = 0.001f * sizeVariation,
                BottomRadius = 0.003f * sizeVariation,
                Height = 0.03f * sizeVariation
            };
            var leftFork = new MeshInstance3D { Mesh = forkMesh, MaterialOverride = tongueMat };
            leftFork.Position = new Vector3(-0.008f * sizeVariation, -0.04f * sizeVariation, 0.26f * sizeVariation);
            leftFork.RotationDegrees = new Vector3(-75, 0, -20);
            headNode.AddChild(leftFork);

            var rightFork = new MeshInstance3D { Mesh = forkMesh, MaterialOverride = tongueMat };
            rightFork.Position = new Vector3(0.008f * sizeVariation, -0.04f * sizeVariation, 0.26f * sizeVariation);
            rightFork.RotationDegrees = new Vector3(-75, 0, 20);
            headNode.AddChild(rightFork);
        }

        // Eyes with slit pupils (reptilian) - using standardized Beast proportions
        var (lizEyeR, lizPupilR, lizEyeY, lizEyeX, lizEyeZ, lizPupilZ) = CalculateEyeGeometry(headRadius, EyeProportions.Beast, sizeVariation);
        var eyeMesh = new SphereMesh { Radius = lizEyeR, Height = lizEyeR * 2f, RadialSegments = 16, Rings = 12 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-lizEyeX, lizEyeY, lizEyeZ);
        headNode.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(lizEyeX, lizEyeY, lizEyeZ);
        headNode.AddChild(rightEye);

        // Slit pupils - vertical like a reptile
        var slitMesh = new SphereMesh { Radius = lizPupilR, Height = lizPupilR * 2f, RadialSegments = 12, Rings = 8 };
        var leftPupil = new MeshInstance3D { Mesh = slitMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-lizEyeX, lizEyeY, lizPupilZ);
        leftPupil.Scale = new Vector3(0.3f, 1.2f, 1f); // Vertical slit
        headNode.AddChild(leftPupil);

        var rightPupil = new MeshInstance3D { Mesh = slitMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(lizEyeX, lizEyeY, lizPupilZ);
        rightPupil.Scale = new Vector3(0.3f, 1.2f, 1f); // Vertical slit
        headNode.AddChild(rightPupil);

        // Tail - multi-segment for balance animation
        // Tail extends backward (-Z) and downward (-Y) from body
        // We orient cylinders along the tail direction by rotating 90° on X,
        // then applying the tail angle rotation
        float tailY = 0.3f * sizeVariation;
        float tailZ = -0.2f * sizeVariation;
        float tailAngle = 50f;  // Angle from horizontal (positive = tip curves down)
        int tailSegs = lodLevel == 0 ? 6 : (lodLevel == 1 ? 4 : 2);

        for (int i = 0; i < tailSegs; i++)
        {
            var tailSeg = new MeshInstance3D();
            float baseRadius = (0.08f - i * 0.012f) * sizeVariation * tailLength;  // Wide end toward body
            float tipRadius = baseRadius * 0.75f;  // Narrow end toward tail tip
            var tailMesh = new CylinderMesh
            {
                // After 90° X rotation: TopRadius (+Y→-Z) points away, BottomRadius (-Y→+Z) points toward body
                TopRadius = tipRadius,
                BottomRadius = baseRadius,
                Height = 0.12f * sizeVariation * tailLength
            };
            tailSeg.Mesh = tailMesh;
            tailSeg.MaterialOverride = scaleMat;
            tailSeg.Position = new Vector3(0, tailY, tailZ);
            // Rotate 90° on X to orient along Z, then apply tail curve angle
            tailSeg.RotationDegrees = new Vector3(90 - tailAngle, 0, 0);
            parent.AddChild(tailSeg);

            // Add small spine ridges along tail (high LOD only)
            if (lodLevel == 0 && i % 2 == 0 && i < 4)
            {
                var tailRidge = new MeshInstance3D();
                var tailRidgeMesh = new CylinderMesh
                {
                    TopRadius = 0.002f * sizeVariation * spineRidgeSize,
                    BottomRadius = 0.008f * sizeVariation * spineRidgeSize,
                    Height = 0.03f * sizeVariation * spineRidgeSize
                };
                tailRidge.Mesh = tailRidgeMesh;
                tailRidge.MaterialOverride = darkScaleMat;
                tailRidge.Position = new Vector3(0, tailY + 0.05f * sizeVariation, tailZ);
                tailRidge.RotationDegrees = new Vector3(70 - tailAngle, 0, 0);
                parent.AddChild(tailRidge);
            }

            // Update for next segment - move along tail direction
            float segLength = 0.1f * sizeVariation * tailLength;
            float angleRad = Mathf.DegToRad(tailAngle);
            tailY -= segLength * Mathf.Sin(angleRad);
            tailZ -= segLength * Mathf.Cos(angleRad);
            tailAngle += 5f;  // Curve more as we go
        }

        // Tail tip - sharp point extending in tail direction
        var tailTip = new MeshInstance3D();
        var tipMesh = new CylinderMesh
        {
            TopRadius = 0.002f * sizeVariation,
            BottomRadius = 0.015f * sizeVariation * tailLength,
            Height = 0.1f * sizeVariation * tailLength
        };
        tailTip.Mesh = tipMesh;
        tailTip.MaterialOverride = scaleMat;
        tailTip.Position = new Vector3(0, tailY, tailZ);
        tailTip.RotationDegrees = new Vector3(90 - tailAngle, 0, 0);
        parent.AddChild(tailTip);

        // Arms with claws - muscular, separate elbows for animation
        var armMesh = new CapsuleMesh { Radius = 0.04f * sizeVariation, Height = 0.2f * sizeVariation };
        var forearmMesh = new CapsuleMesh { Radius = 0.03f * sizeVariation, Height = 0.12f * sizeVariation };

        // Left arm
        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.18f * sizeVariation, 0.48f * sizeVariation, 0.05f * sizeVariation);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -40);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;
        var leftArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = scaleMat };
        leftArmNode.AddChild(leftArm);

        // Left shoulder muscle (high LOD only)
        if (lodLevel == 0)
        {
            var leftShoulder = new MeshInstance3D();
            var shoulderMesh = new SphereMesh { Radius = 0.05f * sizeVariation, Height = 0.1f * sizeVariation };
            leftShoulder.Mesh = shoulderMesh;
            leftShoulder.MaterialOverride = scaleMat;
            leftShoulder.Position = new Vector3(0, 0.02f * sizeVariation, 0);
            leftArmNode.AddChild(leftShoulder);
        }

        // Left forearm with elbow joint
        var leftForearm = new MeshInstance3D { Mesh = forearmMesh, MaterialOverride = scaleMat };
        leftForearm.Position = new Vector3(-0.04f * sizeVariation, -0.12f * sizeVariation, 0);
        leftForearm.RotationDegrees = new Vector3(0, 0, 30);
        leftArmNode.AddChild(leftForearm);
        AddLizardClaws(leftArmNode, clawMat, new Vector3(-0.06f * sizeVariation, -0.2f * sizeVariation, 0), true, sizeVariation, lodLevel);

        // Right arm
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.18f * sizeVariation, 0.48f * sizeVariation, 0.05f * sizeVariation);
        rightArmNode.RotationDegrees = new Vector3(0, 0, 40);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;
        var rightArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = scaleMat };
        rightArmNode.AddChild(rightArm);

        // Right shoulder muscle (high LOD only)
        if (lodLevel == 0)
        {
            var rightShoulder = new MeshInstance3D();
            var shoulderMesh = new SphereMesh { Radius = 0.05f * sizeVariation, Height = 0.1f * sizeVariation };
            rightShoulder.Mesh = shoulderMesh;
            rightShoulder.MaterialOverride = scaleMat;
            rightShoulder.Position = new Vector3(0, 0.02f * sizeVariation, 0);
            rightArmNode.AddChild(rightShoulder);
        }

        // Right forearm with elbow joint
        var rightForearm = new MeshInstance3D { Mesh = forearmMesh, MaterialOverride = scaleMat };
        rightForearm.Position = new Vector3(0.04f * sizeVariation, -0.12f * sizeVariation, 0);
        rightForearm.RotationDegrees = new Vector3(0, 0, -30);
        rightArmNode.AddChild(rightForearm);
        AddLizardClaws(rightArmNode, clawMat, new Vector3(0.06f * sizeVariation, -0.2f * sizeVariation, 0), false, sizeVariation, lodLevel);

        // Legs - digitigrade (lizard-like) with separate knee joints
        var legMesh = new CapsuleMesh { Radius = 0.05f * sizeVariation, Height = 0.22f * sizeVariation };
        var calfMesh = new CapsuleMesh { Radius = 0.04f * sizeVariation, Height = 0.15f * sizeVariation };

        // Left leg - digitigrade stance
        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.14f * sizeVariation, 0.22f * sizeVariation, -0.05f * sizeVariation);
        leftLegNode.RotationDegrees = new Vector3(15, 0, -15);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;
        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = scaleMat };
        leftLegNode.AddChild(leftLeg);

        // Left knee joint (high LOD only)
        if (lodLevel == 0)
        {
            var leftKnee = new MeshInstance3D();
            var kneeMesh = new SphereMesh { Radius = 0.045f * sizeVariation, Height = 0.09f * sizeVariation };
            leftKnee.Mesh = kneeMesh;
            leftKnee.MaterialOverride = scaleMat;
            leftKnee.Position = new Vector3(-0.02f * sizeVariation, -0.11f * sizeVariation, 0);
            leftLegNode.AddChild(leftKnee);
        }

        // Left calf - angled for digitigrade posture
        var leftCalf = new MeshInstance3D { Mesh = calfMesh, MaterialOverride = scaleMat };
        leftCalf.Position = new Vector3(-0.02f * sizeVariation, -0.15f * sizeVariation, 0.03f * sizeVariation);
        leftCalf.RotationDegrees = new Vector3(-35, 0, 10);
        leftLegNode.AddChild(leftCalf);

        // Left foot - elongated lizard foot
        var footMesh = new SphereMesh { Radius = 0.04f * sizeVariation, Height = 0.08f * sizeVariation };
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = scaleMat };
        leftFoot.Position = new Vector3(-0.02f * sizeVariation, -0.22f * sizeVariation, 0.06f * sizeVariation);
        leftFoot.Scale = new Vector3(1, 0.5f, 1.8f); // Longer for digitigrade
        leftLegNode.AddChild(leftFoot);
        AddLizardClaws(leftLegNode, clawMat, new Vector3(-0.02f * sizeVariation, -0.24f * sizeVariation, 0.1f * sizeVariation), true, sizeVariation, lodLevel);

        // Right leg - digitigrade stance
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.14f * sizeVariation, 0.22f * sizeVariation, -0.05f * sizeVariation);
        rightLegNode.RotationDegrees = new Vector3(15, 0, 15);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;
        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = scaleMat };
        rightLegNode.AddChild(rightLeg);

        // Right knee joint (high LOD only)
        if (lodLevel == 0)
        {
            var rightKnee = new MeshInstance3D();
            var kneeMesh = new SphereMesh { Radius = 0.045f * sizeVariation, Height = 0.09f * sizeVariation };
            rightKnee.Mesh = kneeMesh;
            rightKnee.MaterialOverride = scaleMat;
            rightKnee.Position = new Vector3(0.02f * sizeVariation, -0.11f * sizeVariation, 0);
            rightLegNode.AddChild(rightKnee);
        }

        // Right calf - angled for digitigrade posture
        var rightCalf = new MeshInstance3D { Mesh = calfMesh, MaterialOverride = scaleMat };
        rightCalf.Position = new Vector3(0.02f * sizeVariation, -0.15f * sizeVariation, 0.03f * sizeVariation);
        rightCalf.RotationDegrees = new Vector3(-35, 0, -10);
        rightLegNode.AddChild(rightCalf);

        // Right foot - elongated lizard foot
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = scaleMat };
        rightFoot.Position = new Vector3(0.02f * sizeVariation, -0.22f * sizeVariation, 0.06f * sizeVariation);
        rightFoot.Scale = new Vector3(1, 0.5f, 1.8f); // Longer for digitigrade
        rightLegNode.AddChild(rightFoot);
        AddLizardClaws(rightLegNode, clawMat, new Vector3(0.02f * sizeVariation, -0.24f * sizeVariation, 0.1f * sizeVariation), false, sizeVariation, lodLevel);
    }

    private static void AddLizardClaws(Node3D parent, StandardMaterial3D clawMat, Vector3 basePos, bool isLeft, float sizeVariation = 1f, int lodLevel = 0)
    {
        // 3-4 claws depending on LOD
        int clawCount = lodLevel < 2 ? 4 : 3;
        var clawMesh = new CylinderMesh
        {
            TopRadius = 0.002f * sizeVariation,
            BottomRadius = 0.008f * sizeVariation,
            Height = 0.03f * sizeVariation
        };
        float[] xOffsets = { -0.02f, -0.007f, 0.007f, 0.02f };
        float[] zOffsets = { 0.008f, 0.015f, 0.015f, 0.008f };

        for (int i = 0; i < clawCount; i++)
        {
            var claw = new MeshInstance3D();
            claw.Mesh = clawMesh;
            claw.MaterialOverride = clawMat;
            float xOff = isLeft ? xOffsets[i] : -xOffsets[i];
            claw.Position = basePos + new Vector3(xOff * sizeVariation, 0, zOffsets[i] * sizeVariation);
            claw.RotationDegrees = new Vector3(-60, 0, isLeft ? -15 + i * 10 : 15 - i * 10);
            parent.AddChild(claw);
        }
    }

    private static Color GetRandomLizardScaleColor(Random random)
    {
        // Color variations: green, brown, blue, red
        int colorType = random.Next(0, 4);
        return colorType switch
        {
            0 => new Color(0.3f + (float)random.NextDouble() * 0.2f, 0.5f + (float)random.NextDouble() * 0.2f, 0.25f), // Green
            1 => new Color(0.45f + (float)random.NextDouble() * 0.15f, 0.35f + (float)random.NextDouble() * 0.15f, 0.2f), // Brown
            2 => new Color(0.2f, 0.35f + (float)random.NextDouble() * 0.2f, 0.5f + (float)random.NextDouble() * 0.2f), // Blue
            _ => new Color(0.55f + (float)random.NextDouble() * 0.15f, 0.25f, 0.2f) // Red
        };
    }

    // ==========================================
    // BOSS MONSTERS
    // ==========================================

    private static void CreateSkeletonLordMesh(Node3D parent, LimbNodes limbs)
    {
        // Larger, more menacing skeleton with crown and cape
        Color boneColor = new Color(0.85f, 0.82f, 0.7f);
        Color crownColor = new Color(0.9f, 0.75f, 0.2f);
        Color capeColor = new Color(0.3f, 0.1f, 0.15f);

        var boneMat = new StandardMaterial3D { AlbedoColor = boneColor, Roughness = 0.8f };
        var crownMat = new StandardMaterial3D
        {
            AlbedoColor = crownColor,
            Metallic = 0.8f,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = crownColor * 0.2f
        };
        var capeMat = new StandardMaterial3D { AlbedoColor = capeColor, Roughness = 0.9f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 1f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 1f, 0.4f) * 2.0f,
            EmissionEnergyMultiplier = 2.5f
        };

        float scale = 1.4f;

        // Create base skeleton (scaled up)
        // Skull
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.5f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var skull = new MeshInstance3D();
        var skullMesh = new SphereMesh { Radius = 0.22f * scale, Height = 0.44f * scale };
        skull.Mesh = skullMesh;
        skull.MaterialOverride = boneMat;
        skull.Scale = new Vector3(0.9f, 1f, 0.85f);
        headNode.AddChild(skull);

        // Crown
        var crown = new MeshInstance3D();
        var crownMesh = new CylinderMesh { TopRadius = 0.18f * scale, BottomRadius = 0.15f * scale, Height = 0.12f * scale };
        crown.Mesh = crownMesh;
        crown.MaterialOverride = crownMat;
        crown.Position = new Vector3(0, 0.18f * scale, 0);
        headNode.AddChild(crown);

        // Crown spikes
        var spikeMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.03f * scale, Height = 0.15f * scale };
        for (int i = 0; i < 5; i++)
        {
            var spike = new MeshInstance3D { Mesh = spikeMesh, MaterialOverride = crownMat };
            float angle = i * Mathf.Pi * 2f / 5f;
            spike.Position = new Vector3(Mathf.Cos(angle) * 0.12f * scale, 0.28f * scale, Mathf.Sin(angle) * 0.12f * scale);
            headNode.AddChild(spike);
        }

        // Glowing green eyes
        var socketMesh = new SphereMesh { Radius = 0.06f * scale, Height = 0.12f * scale };
        var leftSocket = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = eyeMat };
        leftSocket.Position = new Vector3(-0.07f * scale, 0.02f, 0.14f * scale);
        headNode.AddChild(leftSocket);

        var rightSocket = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = eyeMat };
        rightSocket.Position = new Vector3(0.07f * scale, 0.02f, 0.14f * scale);
        headNode.AddChild(rightSocket);

        // Spine
        var spineMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.04f * scale, Height = 0.6f * scale };
        var spine = new MeshInstance3D { Mesh = spineMesh, MaterialOverride = boneMat };
        spine.Position = new Vector3(0, 1.0f * scale, 0);
        parent.AddChild(spine);

        // Cape with tattered edges
        var cape = new MeshInstance3D();
        var capeMesh = new BoxMesh { Size = new Vector3(0.6f * scale, 0.8f * scale, 0.05f * scale) };
        cape.Mesh = capeMesh;
        cape.MaterialOverride = capeMat;
        cape.Position = new Vector3(0, 0.9f * scale, -0.15f * scale);
        parent.AddChild(cape);

        // Tattered cape edges (hanging strips)
        var tatterMat = new StandardMaterial3D
        {
            AlbedoColor = capeColor.Darkened(0.15f),
            Roughness = 0.95f
        };
        var tatterMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.25f * scale, 0.02f * scale) };
        for (int t = 0; t < 6; t++)
        {
            float xPos = (-0.25f + t * 0.1f) * scale;
            float height = 0.2f + (t % 2) * 0.08f;
            var tatter = new MeshInstance3D { Mesh = tatterMesh, MaterialOverride = tatterMat };
            tatter.Position = new Vector3(xPos, 0.4f * scale, -0.17f * scale);
            tatter.Scale = new Vector3(0.8f + (t % 3) * 0.15f, height, 1f);
            tatter.RotationDegrees = new Vector3(-5 + (t % 2) * 10, 0, (t - 3) * 4);
            parent.AddChild(tatter);
        }

        // Cape collar (ornate)
        var collarMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.7f, 0.25f),
            Metallic = 0.7f,
            Roughness = 0.3f
        };
        var collarMesh = new CylinderMesh {
            TopRadius = 0.2f * scale,
            BottomRadius = 0.18f * scale,
            Height = 0.06f * scale,
            RadialSegments = 16
        };
        var collar = new MeshInstance3D { Mesh = collarMesh, MaterialOverride = collarMat };
        collar.Position = new Vector3(0, 1.32f * scale, -0.02f * scale);
        parent.AddChild(collar);

        // Ribs (bigger)
        for (int i = 0; i < 5; i++)
        {
            float y = 1.2f * scale - i * 0.1f * scale;
            var ribMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.25f * scale };

            var leftRib = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            leftRib.Position = new Vector3(-0.12f * scale, y, 0.05f * scale);
            leftRib.RotationDegrees = new Vector3(0, 0, -45);
            parent.AddChild(leftRib);

            var rightRib = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            rightRib.Position = new Vector3(0.12f * scale, y, 0.05f * scale);
            rightRib.RotationDegrees = new Vector3(0, 0, 45);
            parent.AddChild(rightRib);
        }

        // Pelvis
        float pelvisCenterY = 0.6f * scale;
        float pelvisHeight = 0.12f * scale;
        var pelvisMesh = new BoxMesh { Size = new Vector3(0.3f * scale, pelvisHeight, 0.12f * scale) };
        var pelvis = new MeshInstance3D { Mesh = pelvisMesh, MaterialOverride = boneMat };
        pelvis.Position = new Vector3(0, pelvisCenterY, 0);
        parent.AddChild(pelvis);

        // Arms with sword
        CreateSkeletonLordArm(parent, limbs, boneMat, scale, true);
        CreateSkeletonLordArm(parent, limbs, boneMat, scale, false);

        // Legs - position at bottom of pelvis
        float hipY = pelvisCenterY - pelvisHeight / 2f;
        CreateSkeletonLeg(parent, limbs, boneMat, true, hipY, scale);
        CreateSkeletonLeg(parent, limbs, boneMat, false, hipY, scale);
    }

    private static void CreateSkeletonLordArm(Node3D parent, LimbNodes limbs, StandardMaterial3D material, float scale, bool isLeft)
    {
        float side = isLeft ? -1 : 1;
        var armNode = new Node3D();
        armNode.Position = new Vector3(side * 0.25f * scale, 1.2f * scale, 0);
        parent.AddChild(armNode);

        if (isLeft) limbs.LeftArm = armNode;
        else limbs.RightArm = armNode;

        var armMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.025f * scale, Height = 0.3f * scale };

        var upperArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = material };
        upperArm.Position = new Vector3(0, -0.15f * scale, 0);
        armNode.AddChild(upperArm);

        var lowerArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = material };
        lowerArm.Position = new Vector3(0, -0.4f * scale, 0);
        armNode.AddChild(lowerArm);

        // Right hand holds a massive runed greatsword
        if (!isLeft)
        {
            var swordMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.5f, 0.5f, 0.55f),
                Metallic = 0.95f,
                Roughness = 0.25f
            };
            var runeMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 1f, 0.4f),
                EmissionEnabled = true,
                Emission = new Color(0.2f, 1f, 0.4f) * 1.5f
            };

            // Larger sword blade
            var sword = new MeshInstance3D();
            var swordMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.9f * scale, 0.015f * scale) };
            sword.Mesh = swordMesh;
            sword.MaterialOverride = swordMat;
            sword.Position = new Vector3(0, -0.95f * scale, 0);
            armNode.AddChild(sword);
            limbs.Weapon = sword;

            // Sword handle
            var handleMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.15f, 0.1f, 0.08f),
                Roughness = 0.85f
            };
            var handleMesh = new CylinderMesh {
                TopRadius = 0.02f * scale,
                BottomRadius = 0.02f * scale,
                Height = 0.18f * scale
            };
            var handle = new MeshInstance3D { Mesh = handleMesh, MaterialOverride = handleMat };
            handle.Position = new Vector3(0, -0.42f * scale, 0);
            armNode.AddChild(handle);

            // Sword crossguard
            var guardMesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.03f * scale, 0.025f * scale) };
            var guard = new MeshInstance3D { Mesh = guardMesh, MaterialOverride = swordMat };
            guard.Position = new Vector3(0, -0.52f * scale, 0);
            armNode.AddChild(guard);

            // Glowing runes on blade
            var runeMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.08f * scale, 0.02f * scale) };
            for (int r = 0; r < 4; r++)
            {
                var rune = new MeshInstance3D { Mesh = runeMesh, MaterialOverride = runeMat };
                rune.Position = new Vector3(0, (-0.65f - r * 0.15f) * scale, 0.012f * scale);
                armNode.AddChild(rune);
            }

            // Skull pommel
            var skullPommelMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale };
            var pommel = new MeshInstance3D { Mesh = skullPommelMesh, MaterialOverride = material };
            pommel.Position = new Vector3(0, -0.33f * scale, 0);
            pommel.Scale = new Vector3(0.9f, 1f, 0.8f);
            armNode.AddChild(pommel);
        }

        // Shoulder armor pauldron
        var armorMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.38f, 0.42f),
            Metallic = 0.7f,
            Roughness = 0.4f
        };
        var pauldronMesh = new SphereMesh { Radius = 0.06f * scale, Height = 0.12f * scale };
        var pauldron = new MeshInstance3D { Mesh = pauldronMesh, MaterialOverride = armorMat };
        pauldron.Position = new Vector3(side * 0.03f * scale, 0.02f * scale, 0);
        pauldron.Scale = new Vector3(1.2f, 0.6f, 1f);
        armNode.AddChild(pauldron);
    }

    /// <summary>
    /// Creates the Dragon King boss - a massive, imposing dragon with crown and battle scars.
    /// Uses the enhanced CreateDragonMesh with boss-level parameters (3x scale, branching horns, max wingspan).
    /// </summary>
    private static void CreateDragonKingMesh(Node3D parent, LimbNodes limbs)
    {
        // Dragon King: Massive black dragon with branching horns, max wing span, and boss presence
        // scaleColorType: 1=Black (dark menacing appearance)
        // hornStyle: 2=Branching (antler-like, regal appearance)
        // wingSpan: 1.3f (30% larger wings for dramatic silhouette)
        // tailStyle: 1=Spikes (dangerous tail)
        // sizeScale: 3.0f (3x normal size for true boss presence)
        // bossPresence: true (glowing chest, smoke effects)
        CreateDragonMesh(
            parent,
            limbs,
            lodLevel: 0,                // High detail for boss
            scaleColorType: 1,          // Black dragon with purple fire
            hornStyle: 2,               // Branching antler horns (regal)
            wingSpan: 1.3f,             // 30% larger wingspan
            tailStyle: 1,               // Spiked tail
            sizeScale: 3.0f,            // 3x scale = massive boss
            bossPresence: true          // Chest glow, smoke, enhanced effects
        );

        // Add battle scars (only on high-detail boss)
        AddDragonKingBattleScars(parent, limbs, 3.0f);

        // Add golden crown (boss identifier)
        AddDragonKingCrown(parent, limbs, 3.0f);
    }

    /// <summary>
    /// Adds battle scars to Dragon King showing it's a veteran of many fights.
    /// </summary>
    private static void AddDragonKingBattleScars(Node3D parent, LimbNodes limbs, float scale)
    {
        // Battle scar material (darker, rougher)
        var scarMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.08f, 0.12f),
            Roughness = 0.9f
        };

        // Wing tear/scar (left wing)
        if (limbs.LeftArm != null)
        {
            var tearMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.02f * scale, 0.08f * scale) };
            var tear = new MeshInstance3D { Mesh = tearMesh, MaterialOverride = scarMat };
            tear.Position = new Vector3(-0.25f * scale, 0.05f * scale, 0.1f * scale);
            tear.RotationDegrees = new Vector3(15, 0, 35);
            limbs.LeftArm.AddChild(tear);
        }

        // Body scar (slash across body)
        var bodyScarMesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.025f * scale, 0.35f * scale) };
        var bodyScar = new MeshInstance3D { Mesh = bodyScarMesh, MaterialOverride = scarMat };
        bodyScar.Position = new Vector3(0.1f * scale, 0.75f * scale, 0.05f * scale);
        bodyScar.RotationDegrees = new Vector3(0, 25, 35);
        parent.AddChild(bodyScar);

        // Additional battle scars (claw marks pattern)
        var clawScarMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.02f * scale, 0.18f * scale) };
        for (int i = 0; i < 3; i++)
        {
            var clawScar = new MeshInstance3D { Mesh = clawScarMesh, MaterialOverride = scarMat };
            clawScar.Position = new Vector3(-0.15f * scale + i * 0.08f * scale, 0.62f * scale, 0.12f * scale);
            clawScar.RotationDegrees = new Vector3(0, -15, 45 + i * 8);
            parent.AddChild(clawScar);
        }

        // Ornate scale plates (royal armor-like patterns on body)
        var royalScaleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.15f, 0.25f),
            Metallic = 0.3f,
            Roughness = 0.5f
        };
        var ornateScaleMesh = new CylinderMesh {
            TopRadius = 0.015f * scale,
            BottomRadius = 0.04f * scale,
            Height = 0.08f * scale
        };

        // Chest plate scales (ornate patterns)
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                float xOffset = (-0.12f + col * 0.06f) * scale;
                float yOffset = (0.5f + row * 0.08f) * scale;
                float zOffset = 0.25f * scale;

                var ornateScale = new MeshInstance3D { Mesh = ornateScaleMesh, MaterialOverride = royalScaleMat };
                ornateScale.Position = new Vector3(xOffset, yOffset, zOffset);
                ornateScale.RotationDegrees = new Vector3(-25, 0, (col - 2) * 8);
                parent.AddChild(ornateScale);
            }
        }

        // Royal gold accents on wing edges
        var goldAccentMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.7f, 0.25f),
            Metallic = 0.85f,
            Roughness = 0.25f,
            EmissionEnabled = true,
            Emission = new Color(0.85f, 0.7f, 0.25f) * 0.2f
        };
        var accentMesh = new CylinderMesh {
            TopRadius = 0.008f * scale,
            BottomRadius = 0.008f * scale,
            Height = 0.15f * scale
        };

        if (limbs.LeftArm != null)
        {
            for (int i = 0; i < 4; i++)
            {
                var accent = new MeshInstance3D { Mesh = accentMesh, MaterialOverride = goldAccentMat };
                accent.Position = new Vector3(-0.15f * scale - i * 0.12f * scale, 0.02f * scale, 0.05f * scale);
                accent.RotationDegrees = new Vector3(0, 0, 75);
                limbs.LeftArm.AddChild(accent);
            }
        }

        if (limbs.RightArm != null)
        {
            for (int i = 0; i < 4; i++)
            {
                var accent = new MeshInstance3D { Mesh = accentMesh, MaterialOverride = goldAccentMat };
                accent.Position = new Vector3(0.15f * scale + i * 0.12f * scale, 0.02f * scale, 0.05f * scale);
                accent.RotationDegrees = new Vector3(0, 0, -75);
                limbs.RightArm.AddChild(accent);
            }
        }

        // Neck rings (royal collar)
        var collarMesh = new CylinderMesh {
            TopRadius = 0.14f * scale,
            BottomRadius = 0.15f * scale,
            Height = 0.025f * scale,
            RadialSegments = 20
        };
        for (int r = 0; r < 2; r++)
        {
            var collar = new MeshInstance3D { Mesh = collarMesh, MaterialOverride = goldAccentMat };
            collar.Position = new Vector3(0, 1.05f * scale + r * 0.04f * scale, 0.48f * scale);
            collar.RotationDegrees = new Vector3(35, 0, 0);
            parent.AddChild(collar);
        }
    }

    /// <summary>
    /// Adds ornate golden crown to Dragon King's head.
    /// </summary>
    private static void AddDragonKingCrown(Node3D parent, LimbNodes limbs, float scale)
    {
        if (limbs.Head == null) return;

        var crownColor = new Color(0.9f, 0.75f, 0.2f);
        var crownMat = new StandardMaterial3D
        {
            AlbedoColor = crownColor,
            Metallic = 0.9f,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = crownColor * 0.4f
        };

        // Crown base (circular band)
        var crownBase = new MeshInstance3D();
        var crownBaseMesh = new CylinderMesh {
            TopRadius = 0.19f * scale,
            BottomRadius = 0.16f * scale,
            Height = 0.1f * scale,
            RadialSegments = 24
        };
        crownBase.Mesh = crownBaseMesh;
        crownBase.MaterialOverride = crownMat;
        crownBase.Position = new Vector3(0, 0.22f * scale, -0.08f * scale);
        limbs.Head.AddChild(crownBase);

        // Crown spikes (7 majestic points)
        var spikeMesh = new CylinderMesh {
            TopRadius = 0.008f * scale,
            BottomRadius = 0.032f * scale,
            Height = 0.15f * scale,
            RadialSegments = 12
        };

        for (int i = 0; i < 7; i++)
        {
            var spike = new MeshInstance3D { Mesh = spikeMesh, MaterialOverride = crownMat };
            float angle = i * Mathf.Pi * 2f / 7f;
            float radius = 0.14f * scale;
            spike.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                0.33f * scale,
                -0.08f * scale + Mathf.Sin(angle) * radius
            );
            spike.RotationDegrees = new Vector3(8, 0, 0); // Slight outward tilt
            limbs.Head.AddChild(spike);

            // Glowing orbs on alternating spikes
            if (i % 2 == 0)
            {
                var orbMesh = new SphereMesh { Radius = 0.022f * scale, Height = 0.044f * scale };
                var orb = new MeshInstance3D { Mesh = orbMesh, MaterialOverride = crownMat };
                orb.Position = new Vector3(
                    Mathf.Cos(angle) * radius * 0.95f,
                    0.40f * scale,
                    -0.08f * scale + Mathf.Sin(angle) * radius * 0.95f
                );
                limbs.Head.AddChild(orb);

                // Add point light to orb for extra drama
                var orbLight = new OmniLight3D();
                orbLight.LightColor = crownColor;
                orbLight.LightEnergy = 0.4f;
                orbLight.OmniRange = 2f * scale;
                orbLight.Position = orb.Position;
                limbs.Head.AddChild(orbLight);
            }
        }

        // Center crown jewel
        var jewelMesh = new SphereMesh { Radius = 0.045f * scale, Height = 0.09f * scale, RadialSegments = 20 };
        var jewelMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.2f, 0.9f), // Purple gem
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.2f, 0.9f) * 1.2f,
            Metallic = 0.3f,
            Roughness = 0.1f
        };
        var jewel = new MeshInstance3D { Mesh = jewelMesh, MaterialOverride = jewelMat };
        jewel.Position = new Vector3(0, 0.42f * scale, 0.05f * scale);
        limbs.Head.AddChild(jewel);

        // Jewel light
        var jewelLight = new OmniLight3D();
        jewelLight.LightColor = new Color(0.8f, 0.2f, 0.9f);
        jewelLight.LightEnergy = 0.6f;
        jewelLight.OmniRange = 3f * scale;
        jewelLight.Position = jewel.Position;
        limbs.Head.AddChild(jewelLight);
    }

    private static void CreateSpiderQueenMesh(Node3D parent, LimbNodes limbs)
    {
        Color bodyColor = new Color(0.15f, 0.05f, 0.1f); // Dark purple-red
        Color markingColor = new Color(0.8f, 0.2f, 0.3f);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.6f, Metallic = 0.1f };
        var markingMat = new StandardMaterial3D
        {
            AlbedoColor = markingColor,
            EmissionEnabled = true,
            Emission = markingColor * 0.3f
        };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.8f, 0.2f) * 0.6f
        };

        float scale = 1.8f;

        // === BODY GEOMETRY ===
        float abdomenRadius = 0.35f * scale;
        float abdomenHeight = 0.7f * scale;
        float abdomenCenterY = 0.45f * scale;
        float abdomenCenterZ = -0.3f * scale;
        float abdomenFrontZ = abdomenCenterZ + abdomenRadius * 1.3f / 2f;  // Account for Z scale

        // Cephalothorax (head) geometry
        float cephaloRadius = 0.22f * scale;
        float cephaloCenterZ = 0.2f * scale;

        // Large abdomen
        var abdomen = new MeshInstance3D();
        var abdomenMesh = new SphereMesh { Radius = abdomenRadius, Height = abdomenHeight };
        abdomen.Mesh = abdomenMesh;
        abdomen.MaterialOverride = bodyMat;
        abdomen.Position = new Vector3(0, abdomenCenterY, abdomenCenterZ);
        abdomen.Scale = new Vector3(1f, 0.8f, 1.3f);
        parent.AddChild(abdomen);

        // Red markings on abdomen
        var markMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale };
        var mark1 = new MeshInstance3D { Mesh = markMesh, MaterialOverride = markingMat };
        mark1.Position = new Vector3(0, 0.55f * scale, -0.5f * scale);
        parent.AddChild(mark1);

        var mark2 = new MeshInstance3D { Mesh = markMesh, MaterialOverride = markingMat };
        mark2.Position = new Vector3(-0.1f * scale, 0.45f * scale, -0.55f * scale);
        mark2.Scale = new Vector3(0.7f, 0.7f, 0.7f);
        parent.AddChild(mark2);

        var mark3 = new MeshInstance3D { Mesh = markMesh, MaterialOverride = markingMat };
        mark3.Position = new Vector3(0.1f * scale, 0.45f * scale, -0.55f * scale);
        mark3.Scale = new Vector3(0.7f, 0.7f, 0.7f);
        parent.AddChild(mark3);

        // === EGG SAC (hanging from back of abdomen) ===
        var eggSacMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.92f, 0.85f, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.4f
        };
        var eggSacMesh = new SphereMesh { Radius = 0.18f * scale, Height = 0.36f * scale };
        var eggSac = new MeshInstance3D { Mesh = eggSacMesh, MaterialOverride = eggSacMat };
        eggSac.Position = new Vector3(0, 0.25f * scale, -0.65f * scale);
        eggSac.Scale = new Vector3(1f, 0.85f, 1.1f);
        parent.AddChild(eggSac);

        // Eggs visible inside sac (darker spots)
        var eggMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.55f, 0.5f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var eggMesh = new SphereMesh { Radius = 0.03f * scale, Height = 0.06f * scale };
        for (int e = 0; e < 8; e++)
        {
            float eAngle = e * Mathf.Pi * 2f / 8f;
            float eRadius = 0.1f + (e % 2) * 0.03f;
            var egg = new MeshInstance3D { Mesh = eggMesh, MaterialOverride = eggMat };
            egg.Position = new Vector3(
                Mathf.Cos(eAngle) * eRadius * scale * 0.6f,
                (0.22f + (e % 3) * 0.04f) * scale,
                (-0.65f + Mathf.Sin(eAngle) * eRadius * 0.5f) * scale
            );
            parent.AddChild(egg);
        }

        // Web strands connecting egg sac to abdomen
        var webMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.88f, 0.82f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var webMesh = new CylinderMesh {
            TopRadius = 0.004f * scale,
            BottomRadius = 0.004f * scale,
            Height = 0.12f * scale
        };
        for (int w = 0; w < 4; w++)
        {
            float wAngle = (w - 1.5f) * 0.4f;
            var web = new MeshInstance3D { Mesh = webMesh, MaterialOverride = webMat };
            web.Position = new Vector3(wAngle * 0.08f * scale, 0.35f * scale, -0.52f * scale);
            web.RotationDegrees = new Vector3(-25 + w * 5, wAngle * 15, 0);
            parent.AddChild(web);
        }

        // Royal purple shimmer on abdomen (royal coloring)
        var royalShimmerMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.15f, 0.45f, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.15f, 0.45f) * 0.15f
        };
        var shimmerMesh = new SphereMesh { Radius = 0.32f * scale, Height = 0.64f * scale };
        var shimmer = new MeshInstance3D { Mesh = shimmerMesh, MaterialOverride = royalShimmerMat };
        shimmer.Position = new Vector3(0, 0.48f * scale, -0.28f * scale);
        shimmer.Scale = new Vector3(0.95f, 0.75f, 1.25f);
        parent.AddChild(shimmer);

        // === PEDICEL JOINT (narrow waist between abdomen and cephalothorax) ===
        float pedicelRadius = 0.08f * scale;
        float pedicelZ = (abdomenFrontZ + cephaloCenterZ) / 2f;  // Midpoint
        var pedicelJoint = CreateJointMesh(bodyMat, pedicelRadius);
        pedicelJoint.Position = new Vector3(0, abdomenCenterY, pedicelZ);
        parent.AddChild(pedicelJoint);

        // Cephalothorax (front body/head)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, abdomenCenterY, cephaloCenterZ);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var cephalo = new MeshInstance3D();
        var cephaloMesh = new SphereMesh { Radius = cephaloRadius, Height = cephaloRadius * 2f };
        cephalo.Mesh = cephaloMesh;
        cephalo.MaterialOverride = bodyMat;
        headNode.AddChild(cephalo);

        // Royal tiara (ornate crown with jewels)
        var tiaraMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.75f, 0.25f),
            Metallic = 0.9f,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = new Color(0.85f, 0.75f, 0.25f) * 0.3f
        };
        var jewelMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.15f, 0.25f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.15f, 0.25f) * 0.8f
        };

        // Tiara band
        var tiaraBandMesh = new CylinderMesh {
            TopRadius = 0.17f * scale,
            BottomRadius = 0.15f * scale,
            Height = 0.04f * scale,
            RadialSegments = 20
        };
        var tiaraBand = new MeshInstance3D { Mesh = tiaraBandMesh, MaterialOverride = tiaraMat };
        tiaraBand.Position = new Vector3(0, 0.2f * scale, 0);
        headNode.AddChild(tiaraBand);

        // Tiara spikes with alternating heights
        var spikeMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.02f * scale, Height = 0.1f * scale };
        var tallSpikeMesh = new CylinderMesh { TopRadius = 0.006f * scale, BottomRadius = 0.025f * scale, Height = 0.14f * scale };
        for (int i = 0; i < 6; i++)
        {
            bool isTall = (i % 2 == 0);
            var spike = new MeshInstance3D {
                Mesh = isTall ? tallSpikeMesh : spikeMesh,
                MaterialOverride = tiaraMat
            };
            float angle = i * Mathf.Pi * 2f / 6f;
            spike.Position = new Vector3(Mathf.Cos(angle) * 0.15f * scale, (isTall ? 0.25f : 0.22f) * scale, Mathf.Sin(angle) * 0.15f * scale);
            headNode.AddChild(spike);

            // Jewels on tall spikes
            if (isTall)
            {
                var jewelMesh = new SphereMesh { Radius = 0.015f * scale, Height = 0.03f * scale };
                var jewel = new MeshInstance3D { Mesh = jewelMesh, MaterialOverride = jewelMat };
                jewel.Position = new Vector3(Mathf.Cos(angle) * 0.15f * scale, 0.34f * scale, Mathf.Sin(angle) * 0.15f * scale);
                headNode.AddChild(jewel);
            }
        }

        // Central front jewel (largest)
        var centralJewelMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale };
        var centralJewel = new MeshInstance3D { Mesh = centralJewelMesh, MaterialOverride = jewelMat };
        centralJewel.Position = new Vector3(0, 0.26f * scale, 0.14f * scale);
        headNode.AddChild(centralJewel);

        // 8 glowing eyes - using standardized Arachnid proportions
        var (spiderEyeR, _, _, _, _, _) = CalculateEyeGeometry(cephaloRadius, EyeProportions.Arachnid, scale);
        var eyeMesh = new SphereMesh { Radius = spiderEyeR, Height = spiderEyeR * 2f, RadialSegments = 12, Rings = 8 };
        float[] eyeX = { -0.1f, -0.05f, 0.05f, 0.1f };
        float[] eyeY = { 0.08f, 0.12f, 0.12f, 0.08f };
        foreach (int i in new int[] { 0, 1, 2, 3 })
        {
            var eye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
            eye.Position = new Vector3(eyeX[i] * scale, eyeY[i] * scale, 0.15f * scale);
            headNode.AddChild(eye);
        }
        // Second row of smaller eyes
        float smallEyeR = spiderEyeR * 0.6f;
        var smallEyeMesh = new SphereMesh { Radius = smallEyeR, Height = smallEyeR * 2f, RadialSegments = 10, Rings = 6 };
        var eye5 = new MeshInstance3D { Mesh = smallEyeMesh, MaterialOverride = eyeMat };
        eye5.Position = new Vector3(-0.07f * scale, 0.02f * scale, 0.18f * scale);
        headNode.AddChild(eye5);
        var eye6 = new MeshInstance3D { Mesh = smallEyeMesh, MaterialOverride = eyeMat };
        eye6.Position = new Vector3(0.07f * scale, 0.02f * scale, 0.18f * scale);
        headNode.AddChild(eye6);

        // Fangs
        var fangMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.025f * scale, Height = 0.12f * scale };
        var leftFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = markingMat };
        leftFang.Position = new Vector3(-0.06f * scale, -0.08f * scale, 0.15f * scale);
        leftFang.RotationDegrees = new Vector3(20, 0, 10);
        headNode.AddChild(leftFang);

        var rightFang = new MeshInstance3D { Mesh = fangMesh, MaterialOverride = markingMat };
        rightFang.Position = new Vector3(0.06f * scale, -0.08f * scale, 0.15f * scale);
        rightFang.RotationDegrees = new Vector3(20, 0, -10);
        headNode.AddChild(rightFang);

        // 8 long legs with royal stripes
        var legMesh = new CylinderMesh { TopRadius = 0.025f * scale, BottomRadius = 0.02f * scale, Height = 0.45f * scale };
        float[] legAngles = { 25f, 55f, 125f, 155f };

        // Leg stripe material (gold bands)
        var stripeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.7f, 0.25f),
            Metallic = 0.6f,
            Roughness = 0.35f
        };
        var stripeMesh = new CylinderMesh {
            TopRadius = 0.027f * scale,
            BottomRadius = 0.027f * scale,
            Height = 0.015f * scale
        };

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                var legNode = new Node3D();
                float angle = legAngles[i] * Mathf.Pi / 180f;
                legNode.Position = new Vector3(side * 0.15f * scale, 0.45f * scale, Mathf.Cos(angle) * 0.12f * scale);
                legNode.RotationDegrees = new Vector3(0, 0, side * (35 + i * 12));
                parent.AddChild(legNode);

                var leg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = bodyMat };
                leg.Position = new Vector3(side * 0.22f * scale, 0, 0);
                legNode.AddChild(leg);

                // Stripes on upper leg
                for (int s = 0; s < 3; s++)
                {
                    var stripe = new MeshInstance3D { Mesh = stripeMesh, MaterialOverride = stripeMat };
                    stripe.Position = new Vector3(side * 0.22f * scale, (0.1f - s * 0.12f) * scale, 0);
                    stripe.RotationDegrees = new Vector3(0, 0, -side * (35 + i * 12));
                    legNode.AddChild(stripe);
                }

                // Lower leg segment
                var lowerLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = bodyMat };
                lowerLeg.Position = new Vector3(side * 0.42f * scale, -0.15f * scale, 0);
                lowerLeg.RotationDegrees = new Vector3(0, 0, side * -30);
                legNode.AddChild(lowerLeg);

                // Stripes on lower leg
                for (int s = 0; s < 2; s++)
                {
                    var stripe = new MeshInstance3D { Mesh = stripeMesh, MaterialOverride = stripeMat };
                    stripe.Position = new Vector3(side * 0.42f * scale, (-0.05f - s * 0.12f) * scale, 0);
                    stripe.RotationDegrees = new Vector3(0, 0, side * -30 - side * (35 + i * 12));
                    legNode.AddChild(stripe);
                }

                // Pointed leg tips (claws)
                var clawMesh = new CylinderMesh {
                    TopRadius = 0.003f * scale,
                    BottomRadius = 0.015f * scale,
                    Height = 0.06f * scale
                };
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = markingMat };
                claw.Position = new Vector3(side * 0.42f * scale, -0.38f * scale, 0);
                claw.RotationDegrees = new Vector3(0, 0, side * -30);
                legNode.AddChild(claw);

                if (i == 0)
                {
                    if (side == -1) limbs.LeftArm = legNode;
                    else limbs.RightArm = legNode;
                }
                else if (i == 3)
                {
                    if (side == -1) limbs.LeftLeg = legNode;
                    else limbs.RightLeg = legNode;
                }
            }
        }
    }

    /// <summary>
    /// Create a Badlama mesh - a fire-breathing llama monster.
    /// Badlamas are ornery creatures that can breathe fire and have a nasty bite.
    /// ENHANCED: Fluffy layered neck wool, distinctive llama face with split lip, banana-shaped ears,
    /// large expressive eyes with sclera, tufted tail end, woolly body texture, detailed cloven hooves.
    /// </summary>
    private static void CreateBadlamaMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride)
    {
        // Llama colors - brownish fur with cream/white markings
        Color furColor = skinColorOverride ?? new Color(0.55f, 0.4f, 0.25f);
        Color lightFur = furColor.Lightened(0.35f);
        Color darkFur = furColor.Darkened(0.2f);
        Color hoofColor = new Color(0.2f, 0.15f, 0.1f);
        Color hoofSplitColor = new Color(0.12f, 0.08f, 0.05f); // Darker for hoof split
        Color eyeColor = new Color(0.95f, 0.4f, 0.1f); // Fiery orange eyes
        Color eyeWhiteColor = new Color(0.98f, 0.96f, 0.92f); // Eye white/sclera
        Color noseColor = new Color(0.18f, 0.12f, 0.1f);
        Color teethColor = new Color(0.95f, 0.92f, 0.85f);
        Color woolColor = furColor.Lightened(0.25f); // Slightly lighter wool

        var furMat = new StandardMaterial3D { AlbedoColor = furColor, Roughness = 0.95f };
        var lightFurMat = new StandardMaterial3D { AlbedoColor = lightFur, Roughness = 0.9f };
        var darkFurMat = new StandardMaterial3D { AlbedoColor = darkFur, Roughness = 0.95f };
        var woolMat = new StandardMaterial3D { AlbedoColor = woolColor, Roughness = 1.0f }; // Very fluffy
        var hoofMat = new StandardMaterial3D { AlbedoColor = hoofColor, Roughness = 0.7f };
        var hoofSplitMat = new StandardMaterial3D { AlbedoColor = hoofSplitColor, Roughness = 0.6f };
        var noseMat = new StandardMaterial3D { AlbedoColor = noseColor, Roughness = 0.6f };
        var teethMat = new StandardMaterial3D { AlbedoColor = teethColor, Roughness = 0.5f };
        var eyeWhiteMat = new StandardMaterial3D { AlbedoColor = eyeWhiteColor, Roughness = 0.3f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor * 0.6f,
            EmissionEnergyMultiplier = 1.5f
        };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };

        // === BODY GEOMETRY (horizontal quadruped) ===
        float bodyRadius = 0.28f;
        float bodyLength = 0.85f;
        float bodyCenterY = 0.7f;
        float bodyFrontZ = bodyLength / 2f;
        float bodyTopY = bodyCenterY + bodyRadius;

        // Head geometry
        float headRadius = 0.1f;
        float headHeight = 0.25f;

        // Body (horizontal oval) - llama body is longer and barrel-shaped
        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = bodyRadius, Height = bodyLength };
        body.Mesh = bodyMesh;
        body.MaterialOverride = furMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(body);

        // Woolly body texture - small bump spheres for wool effect
        var woolBumpMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12f;
            float bumpZ = (i % 3 - 1) * 0.2f;
            float bumpX = Mathf.Cos(angle) * (bodyRadius - 0.02f);
            float bumpY = bodyCenterY + Mathf.Sin(angle) * (bodyRadius * 0.6f);
            var woolBump = new MeshInstance3D { Mesh = woolBumpMesh, MaterialOverride = woolMat };
            woolBump.Position = new Vector3(bumpX, bumpY, bumpZ);
            woolBump.Scale = new Vector3(0.8f + GD.Randf() * 0.4f, 0.6f + GD.Randf() * 0.3f, 0.8f + GD.Randf() * 0.4f);
            parent.AddChild(woolBump);
        }

        // Fluffy underbelly/chest
        var chest = new MeshInstance3D();
        var chestMesh = new CapsuleMesh { Radius = 0.2f, Height = 0.5f };
        chest.Mesh = chestMesh;
        chest.MaterialOverride = lightFurMat;
        chest.Position = new Vector3(0, 0.6f, 0.1f);
        chest.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(chest);

        // Back hump (llamas have a slight hump)
        var humpMesh = new SphereMesh { Radius = 0.15f, Height = 0.3f };
        var hump = new MeshInstance3D { Mesh = humpMesh, MaterialOverride = furMat };
        hump.Position = new Vector3(0, 0.85f, 0);
        hump.Scale = new Vector3(1f, 0.7f, 0.9f);
        parent.AddChild(hump);

        // === NECK - Long llama neck with seamless geometry ===
        // Llamas have very long necks, we need overlapping segments to avoid gaps
        float neckBottomRadius = 0.12f;
        float neckTopRadius = 0.08f;
        float neckLength = 0.6f;
        float neckAngle = -20f; // Slight backward tilt for natural llama pose

        // Neck base joint - connects body to neck with overlap
        float neckOverlap = CalculateOverlap(neckBottomRadius);
        float neckAttachY = bodyTopY - neckOverlap * 0.5f;
        float neckAttachZ = bodyFrontZ;

        var neckJoint = CreateJointMesh(furMat, neckBottomRadius * 1.1f);
        neckJoint.Position = new Vector3(0, neckAttachY, neckAttachZ);
        parent.AddChild(neckJoint);

        // Main neck cylinder - positioned to overlap with joint at bottom
        // Cylinder mesh is centered, so position at half-height above joint
        var neck = new MeshInstance3D();
        var neckMesh = new CylinderMesh { TopRadius = neckTopRadius, BottomRadius = neckBottomRadius, Height = neckLength };
        neck.Mesh = neckMesh;
        neck.MaterialOverride = furMat;
        // When rotated -20° on X, the cylinder tilts backward
        // Position: center of cylinder after rotation to connect with joint
        float neckCenterY = neckAttachY + (neckLength / 2f) * Mathf.Cos(Mathf.DegToRad(-neckAngle));
        float neckCenterZ = neckAttachZ + (neckLength / 2f) * Mathf.Sin(Mathf.DegToRad(-neckAngle));
        neck.Position = new Vector3(0, neckCenterY, neckCenterZ);
        neck.RotationDegrees = new Vector3(neckAngle, 0, 0);
        parent.AddChild(neck);

        // FLUFFY NECK WOOL - Layered spheres around neck for woolly appearance
        var neckWoolSphere = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        int woolRings = 5;
        int woolPerRing = 8;
        for (int ring = 0; ring < woolRings; ring++)
        {
            float ringT = (float)ring / (woolRings - 1); // 0 to 1 along neck
            float ringRadius = Mathf.Lerp(neckBottomRadius + 0.03f, neckTopRadius + 0.02f, ringT);
            float ringY = neckAttachY + ringT * neckLength * Mathf.Cos(Mathf.DegToRad(-neckAngle));
            float ringZ = neckAttachZ + ringT * neckLength * Mathf.Sin(Mathf.DegToRad(-neckAngle));

            for (int w = 0; w < woolPerRing; w++)
            {
                float woolAngle = w * Mathf.Tau / woolPerRing + ring * 0.3f; // Offset each ring
                float woolX = Mathf.Cos(woolAngle) * ringRadius;
                float woolYOffset = Mathf.Sin(woolAngle) * ringRadius * 0.5f;
                var woolPuff = new MeshInstance3D { Mesh = neckWoolSphere, MaterialOverride = woolMat };
                woolPuff.Position = new Vector3(woolX, ringY + woolYOffset, ringZ);
                float puffScale = 0.7f + GD.Randf() * 0.4f;
                woolPuff.Scale = new Vector3(puffScale, puffScale * 0.8f, puffScale);
                parent.AddChild(woolPuff);
            }
        }

        // Neck top joint - connects neck to head
        float neckTopY = neckAttachY + neckLength * Mathf.Cos(Mathf.DegToRad(-neckAngle));
        float neckTopZ = neckAttachZ + neckLength * Mathf.Sin(Mathf.DegToRad(-neckAngle));
        var neckTopJoint = CreateJointMesh(furMat, neckTopRadius * 1.2f);
        neckTopJoint.Position = new Vector3(0, neckTopY, neckTopZ);
        parent.AddChild(neckTopJoint);

        // Head (llama head is elongated) - positioned at neck top with overlap
        float headCenterY = neckTopY + headHeight * 0.3f;  // Overlap into neck top
        float headCenterZ = neckTopZ + 0.05f;  // Slightly forward from neck top
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, headCenterZ);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        var headMesh = new CapsuleMesh { Radius = headRadius, Height = headHeight };
        head.Mesh = headMesh;
        head.MaterialOverride = furMat;
        head.RotationDegrees = new Vector3(-70, 0, 0);
        headNode.AddChild(head);

        // ELONGATED SNOUT - Distinctive llama face
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.065f, Height = 0.14f };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = lightFurMat;
        snout.Position = new Vector3(0, -0.03f, 0.17f);
        snout.RotationDegrees = new Vector3(-80, 0, 0);
        headNode.AddChild(snout);

        // SPLIT UPPER LIP - Distinctive llama feature
        var splitLipMesh = new SphereMesh { Radius = 0.035f, Height = 0.07f };
        var leftLip = new MeshInstance3D { Mesh = splitLipMesh, MaterialOverride = lightFurMat };
        leftLip.Position = new Vector3(-0.018f, -0.09f, 0.24f);
        leftLip.Scale = new Vector3(0.8f, 0.6f, 0.7f);
        headNode.AddChild(leftLip);
        var rightLip = new MeshInstance3D { Mesh = splitLipMesh, MaterialOverride = lightFurMat };
        rightLip.Position = new Vector3(0.018f, -0.09f, 0.24f);
        rightLip.Scale = new Vector3(0.8f, 0.6f, 0.7f);
        headNode.AddChild(rightLip);

        // Lip cleft (dark line between lip halves)
        var cleftMesh = new BoxMesh { Size = new Vector3(0.008f, 0.02f, 0.03f) };
        var lipCleft = new MeshInstance3D { Mesh = cleftMesh, MaterialOverride = noseMat };
        lipCleft.Position = new Vector3(0, -0.085f, 0.23f);
        headNode.AddChild(lipCleft);

        // Nose (larger and more prominent)
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.028f, Height = 0.056f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, -0.055f, 0.26f);
        nose.Scale = new Vector3(1.1f, 0.8f, 0.9f);
        headNode.AddChild(nose);

        // Nostrils (smoke may come from here!)
        var nostrilMesh = new SphereMesh { Radius = 0.01f, Height = 0.02f };
        var nostrilMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.Black,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f) * 0.3f
        };
        var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
        leftNostril.Position = new Vector3(-0.018f, -0.06f, 0.275f);
        headNode.AddChild(leftNostril);
        var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
        rightNostril.Position = new Vector3(0.018f, -0.06f, 0.275f);
        headNode.AddChild(rightNostril);

        // Big front teeth (for biting!)
        var toothMesh = new BoxMesh { Size = new Vector3(0.015f, 0.028f, 0.012f) };
        var leftTooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = teethMat };
        leftTooth.Position = new Vector3(-0.022f, -0.105f, 0.21f);
        leftTooth.RotationDegrees = new Vector3(-5, 0, 3);
        headNode.AddChild(leftTooth);
        var rightTooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = teethMat };
        rightTooth.Position = new Vector3(0.022f, -0.105f, 0.21f);
        rightTooth.RotationDegrees = new Vector3(-5, 0, -3);
        headNode.AddChild(rightTooth);

        // === LARGE EXPRESSIVE EYES with sclera (eye whites) ===
        var eyeScleraMesh = new SphereMesh { Radius = 0.032f, Height = 0.064f, RadialSegments = 16, Rings = 12 };
        var badlamaEyeMesh = new SphereMesh { Radius = 0.022f, Height = 0.044f, RadialSegments = 16, Rings = 12 };
        var badlamaPupilMesh = new SphereMesh { Radius = 0.01f, Height = 0.02f, RadialSegments = 12, Rings = 8 };

        // Left eye - sclera (white), iris (fiery), pupil (black)
        var leftSclera = new MeshInstance3D { Mesh = eyeScleraMesh, MaterialOverride = eyeWhiteMat };
        leftSclera.Position = new Vector3(-0.065f, 0.035f, 0.075f);
        leftSclera.Scale = new Vector3(0.6f, 1f, 0.8f);
        headNode.AddChild(leftSclera);
        var leftEye = new MeshInstance3D { Mesh = badlamaEyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.068f, 0.035f, 0.09f);
        headNode.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = badlamaPupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-0.072f, 0.035f, 0.105f);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightSclera = new MeshInstance3D { Mesh = eyeScleraMesh, MaterialOverride = eyeWhiteMat };
        rightSclera.Position = new Vector3(0.065f, 0.035f, 0.075f);
        rightSclera.Scale = new Vector3(0.6f, 1f, 0.8f);
        headNode.AddChild(rightSclera);
        var rightEye = new MeshInstance3D { Mesh = badlamaEyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.068f, 0.035f, 0.09f);
        headNode.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = badlamaPupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(0.072f, 0.035f, 0.105f);
        headNode.AddChild(rightPupil);

        // Eyelids (for expression)
        var eyelidMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f };
        var leftUpperLid = new MeshInstance3D { Mesh = eyelidMesh, MaterialOverride = furMat };
        leftUpperLid.Position = new Vector3(-0.065f, 0.055f, 0.08f);
        leftUpperLid.Scale = new Vector3(0.8f, 0.4f, 0.6f);
        headNode.AddChild(leftUpperLid);
        var rightUpperLid = new MeshInstance3D { Mesh = eyelidMesh, MaterialOverride = furMat };
        rightUpperLid.Position = new Vector3(0.065f, 0.055f, 0.08f);
        rightUpperLid.Scale = new Vector3(0.8f, 0.4f, 0.6f);
        headNode.AddChild(rightUpperLid);

        // === BANANA-SHAPED EARS - curved using angled cylinder segments ===
        var earBaseMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.035f, Height = 0.06f };
        var earMidMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.02f, Height = 0.05f };
        var earTipMesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.015f, Height = 0.04f };

        // Left ear - base
        var leftEarBase = new MeshInstance3D { Mesh = earBaseMesh, MaterialOverride = furMat };
        leftEarBase.Position = new Vector3(-0.075f, 0.1f, -0.02f);
        leftEarBase.RotationDegrees = new Vector3(-10, 0, -20);
        headNode.AddChild(leftEarBase);
        // Left ear - middle (curves outward)
        var leftEarMid = new MeshInstance3D { Mesh = earMidMesh, MaterialOverride = furMat };
        leftEarMid.Position = new Vector3(-0.085f, 0.145f, -0.015f);
        leftEarMid.RotationDegrees = new Vector3(-5, 10, -35);
        headNode.AddChild(leftEarMid);
        // Left ear - tip (curves back inward like banana)
        var leftEarTip = new MeshInstance3D { Mesh = earTipMesh, MaterialOverride = furMat };
        leftEarTip.Position = new Vector3(-0.09f, 0.18f, -0.005f);
        leftEarTip.RotationDegrees = new Vector3(5, 15, -25);
        headNode.AddChild(leftEarTip);

        // Right ear - base
        var rightEarBase = new MeshInstance3D { Mesh = earBaseMesh, MaterialOverride = furMat };
        rightEarBase.Position = new Vector3(0.075f, 0.1f, -0.02f);
        rightEarBase.RotationDegrees = new Vector3(-10, 0, 20);
        headNode.AddChild(rightEarBase);
        // Right ear - middle
        var rightEarMid = new MeshInstance3D { Mesh = earMidMesh, MaterialOverride = furMat };
        rightEarMid.Position = new Vector3(0.085f, 0.145f, -0.015f);
        rightEarMid.RotationDegrees = new Vector3(-5, -10, 35);
        headNode.AddChild(rightEarMid);
        // Right ear - tip
        var rightEarTip = new MeshInstance3D { Mesh = earTipMesh, MaterialOverride = furMat };
        rightEarTip.Position = new Vector3(0.09f, 0.18f, -0.005f);
        rightEarTip.RotationDegrees = new Vector3(5, -15, 25);
        headNode.AddChild(rightEarTip);

        // Inner ear (pink) - follows curve
        var innerEarMesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.015f, Height = 0.07f };
        var innerEarMat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.6f, 0.6f), Roughness = 0.9f };
        var leftInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = innerEarMat };
        leftInnerEar.Position = new Vector3(-0.078f, 0.12f, 0f);
        leftInnerEar.RotationDegrees = new Vector3(-8, 5, -28);
        headNode.AddChild(leftInnerEar);
        var rightInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = innerEarMat };
        rightInnerEar.Position = new Vector3(0.078f, 0.12f, 0f);
        rightInnerEar.RotationDegrees = new Vector3(-8, -5, 28);
        headNode.AddChild(rightInnerEar);

        // Fluffy forelock (tuft of fur on head)
        var forelockMesh = new SphereMesh { Radius = 0.055f, Height = 0.11f };
        var forelock = new MeshInstance3D { Mesh = forelockMesh, MaterialOverride = woolMat };
        forelock.Position = new Vector3(0, 0.11f, 0.02f);
        forelock.Scale = new Vector3(0.9f, 1f, 0.7f);
        headNode.AddChild(forelock);

        // Extra forelock tufts
        var tuftMesh = new SphereMesh { Radius = 0.03f, Height = 0.06f };
        var leftTuft = new MeshInstance3D { Mesh = tuftMesh, MaterialOverride = woolMat };
        leftTuft.Position = new Vector3(-0.035f, 0.1f, 0.015f);
        headNode.AddChild(leftTuft);
        var rightTuft = new MeshInstance3D { Mesh = tuftMesh, MaterialOverride = woolMat };
        rightTuft.Position = new Vector3(0.035f, 0.1f, 0.015f);
        headNode.AddChild(rightTuft);

        // === TAIL with TUFTED END ===
        var tailNode = new Node3D();
        tailNode.Position = new Vector3(0, 0.75f, -0.4f);
        parent.AddChild(tailNode);

        // Tail base
        var tailBaseMesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.05f, Height = 0.08f };
        var tailBase = new MeshInstance3D { Mesh = tailBaseMesh, MaterialOverride = furMat };
        tailBase.RotationDegrees = new Vector3(25, 0, 0);
        tailNode.AddChild(tailBase);

        // Tail mid
        var tailMidMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.025f, Height = 0.08f };
        var tailMid = new MeshInstance3D { Mesh = tailMidMesh, MaterialOverride = furMat };
        tailMid.Position = new Vector3(0, 0.065f, -0.03f);
        tailMid.RotationDegrees = new Vector3(35, 0, 0);
        tailNode.AddChild(tailMid);

        // TUFTED TAIL END - cluster of fluffy spheres
        var tailTuftMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        var tailTuft1 = new MeshInstance3D { Mesh = tailTuftMesh, MaterialOverride = lightFurMat };
        tailTuft1.Position = new Vector3(0, 0.12f, -0.06f);
        tailNode.AddChild(tailTuft1);
        var tailTuft2 = new MeshInstance3D { Mesh = tailTuftMesh, MaterialOverride = woolMat };
        tailTuft2.Position = new Vector3(-0.02f, 0.115f, -0.055f);
        tailTuft2.Scale = new Vector3(0.8f, 0.8f, 0.8f);
        tailNode.AddChild(tailTuft2);
        var tailTuft3 = new MeshInstance3D { Mesh = tailTuftMesh, MaterialOverride = woolMat };
        tailTuft3.Position = new Vector3(0.02f, 0.115f, -0.055f);
        tailTuft3.Scale = new Vector3(0.8f, 0.8f, 0.8f);
        tailNode.AddChild(tailTuft3);
        var tailTuft4 = new MeshInstance3D { Mesh = tailTuftMesh, MaterialOverride = lightFurMat };
        tailTuft4.Position = new Vector3(0, 0.135f, -0.07f);
        tailTuft4.Scale = new Vector3(0.7f, 0.7f, 0.7f);
        tailNode.AddChild(tailTuft4);

        // 4 legs with detailed hooves
        CreateBadlamaLeg(parent, limbs, furMat, woolMat, hoofMat, hoofSplitMat, true, true);
        CreateBadlamaLeg(parent, limbs, furMat, woolMat, hoofMat, hoofSplitMat, false, true);
        CreateBadlamaLeg(parent, limbs, furMat, woolMat, hoofMat, hoofSplitMat, true, false);
        CreateBadlamaLeg(parent, limbs, furMat, woolMat, hoofMat, hoofSplitMat, false, false);
    }

    private static void CreateBadlamaLeg(Node3D parent, LimbNodes limbs, StandardMaterial3D furMat, StandardMaterial3D woolMat, StandardMaterial3D hoofMat, StandardMaterial3D hoofSplitMat, bool isLeft, bool isFront)
    {
        float xOffset = isLeft ? -0.18f : 0.18f;
        float zOffset = isFront ? 0.25f : -0.28f;

        var legNode = new Node3D();
        legNode.Position = new Vector3(xOffset, 0.55f, zOffset);
        parent.AddChild(legNode);

        // Upper leg with wool texture
        var upperLeg = new MeshInstance3D();
        var upperLegMesh = new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.04f, Height = 0.28f };
        upperLeg.Mesh = upperLegMesh;
        upperLeg.MaterialOverride = furMat;
        upperLeg.Position = new Vector3(0, -0.14f, 0);
        legNode.AddChild(upperLeg);

        // Wool puffs on upper leg
        var legWoolMesh = new SphereMesh { Radius = 0.03f, Height = 0.06f };
        for (int i = 0; i < 3; i++)
        {
            float woolY = -0.05f - i * 0.08f;
            float woolAngle = i * 1.5f;
            var legWool = new MeshInstance3D { Mesh = legWoolMesh, MaterialOverride = woolMat };
            legWool.Position = new Vector3(Mathf.Cos(woolAngle) * 0.04f, woolY, Mathf.Sin(woolAngle) * 0.04f);
            legWool.Scale = new Vector3(0.8f + GD.Randf() * 0.3f, 0.7f, 0.8f + GD.Randf() * 0.3f);
            legNode.AddChild(legWool);
        }

        // Lower leg (thinner)
        var lowerLeg = new MeshInstance3D();
        var lowerLegMesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.025f, Height = 0.25f };
        lowerLeg.Mesh = lowerLegMesh;
        lowerLeg.MaterialOverride = furMat;
        lowerLeg.Position = new Vector3(0, -0.4f, 0);
        legNode.AddChild(lowerLeg);

        // === DETAILED HOOF with split ===
        // Main hoof body
        var hoofMesh = new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.035f, Height = 0.06f };
        var hoof = new MeshInstance3D { Mesh = hoofMesh, MaterialOverride = hoofMat };
        hoof.Position = new Vector3(0, -0.55f, 0);
        legNode.AddChild(hoof);

        // Hoof split (dark line down center, llamas have cloven hooves)
        var hoofSplitMesh = new BoxMesh { Size = new Vector3(0.006f, 0.065f, 0.04f) };
        var hoofSplit = new MeshInstance3D { Mesh = hoofSplitMesh, MaterialOverride = hoofSplitMat };
        hoofSplit.Position = new Vector3(0, -0.55f, 0.01f);
        legNode.AddChild(hoofSplit);

        // Two hoof "toes"
        var hoofToeMesh = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.016f, Height = 0.02f };
        var leftToe = new MeshInstance3D { Mesh = hoofToeMesh, MaterialOverride = hoofMat };
        leftToe.Position = new Vector3(-0.012f, -0.575f, 0.01f);
        legNode.AddChild(leftToe);
        var rightToe = new MeshInstance3D { Mesh = hoofToeMesh, MaterialOverride = hoofMat };
        rightToe.Position = new Vector3(0.012f, -0.575f, 0.01f);
        legNode.AddChild(rightToe);

        // Store limb references
        if (isFront)
        {
            if (isLeft) limbs.LeftArm = legNode;
            else limbs.RightArm = legNode;
        }
        else
        {
            if (isLeft) limbs.LeftLeg = legNode;
            else limbs.RightLeg = legNode;
        }
    }

    /// <summary>
    /// Create Slime King boss mesh - larger, more menacing slime with crown and toxic effects
    /// </summary>
    private static void CreateSlimeKingMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride = null)
    {
        float bossScale = 2.5f; // 2.5x larger than normal slime
        Color bossColor = skinColorOverride ?? new Color(0.15f, 0.6f, 0.25f); // Darker, more toxic green
        Color toxicGlow = new Color(0.4f, 1f, 0.2f); // Bright toxic green
        Color bubbleColor = bossColor.Lightened(0.3f);

        int bodySegments = 40; // Extra smooth for boss
        int bodyRings = 32;

        var slimeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(bossColor.R, bossColor.G, bossColor.B, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.12f,
            Metallic = 0.2f,
            RimEnabled = true,
            Rim = 0.4f,
            RimTint = 0.3f
        };

        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(bossColor.Darkened(0.4f).R, bossColor.Darkened(0.4f).G, bossColor.Darkened(0.4f).B, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = toxicGlow * 0.15f
        };

        // Main body - massive asymmetric blob
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.38f, Height = 0.76f, RadialSegments = bodySegments, Rings = bodyRings };
        body.Mesh = bodyMesh;
        body.MaterialOverride = slimeMat;
        body.Position = new Vector3(0, 0.38f * bossScale, 0);
        body.Scale = new Vector3(bossScale * 1.15f, bossScale * 0.8f, bossScale * 1.1f);
        parent.AddChild(body);
        limbs.Head = body;

        // Multiple overlapping body layers for depth
        for (int layer = 0; layer < 3; layer++)
        {
            var bodyLayer = new MeshInstance3D();
            var layerMesh = new SphereMesh { Radius = 0.36f - layer * 0.03f, Height = 0.72f - layer * 0.06f, RadialSegments = bodySegments, Rings = bodyRings };
            bodyLayer.Mesh = layerMesh;
            var layerMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(bossColor.R, bossColor.G, bossColor.B, 0.4f - layer * 0.1f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.15f + layer * 0.05f,
                Metallic = 0.15f
            };
            bodyLayer.MaterialOverride = layerMat;
            bodyLayer.Position = new Vector3(layer * 0.08f - 0.08f, 0.38f * bossScale, layer * 0.05f - 0.05f);
            bodyLayer.Scale = new Vector3(bossScale * (1.1f - layer * 0.05f), bossScale * (0.85f + layer * 0.05f), bossScale * (1.05f - layer * 0.03f));
            parent.AddChild(bodyLayer);
        }

        // Multiple nuclei cores - menacing
        for (int i = 0; i < 3; i++)
        {
            var core = new MeshInstance3D();
            var coreMesh = new SphereMesh { Radius = 0.18f - i * 0.03f, Height = 0.36f - i * 0.06f, RadialSegments = 28, Rings = 22 };
            core.Mesh = coreMesh;
            core.MaterialOverride = coreMat;

            float angle = i * 120f * Mathf.Pi / 180f;
            core.Position = new Vector3(
                Mathf.Cos(angle) * 0.15f * bossScale,
                (0.35f + i * 0.05f) * bossScale,
                Mathf.Sin(angle) * 0.15f * bossScale
            );
            core.Scale = Vector3.One * bossScale * (0.9f + i * 0.1f);
            parent.AddChild(core);

            // Glow around each nucleus
            var glow = new MeshInstance3D();
            var glowMesh = new SphereMesh { Radius = 0.24f - i * 0.03f, Height = 0.48f - i * 0.06f, RadialSegments = 24, Rings = 18 };
            glow.Mesh = glowMesh;
            var glowMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(toxicGlow.R, toxicGlow.G, toxicGlow.B, 0.4f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = toxicGlow * 0.4f,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add
            };
            glow.MaterialOverride = glowMat;
            glow.Position = core.Position;
            glow.Scale = Vector3.One * bossScale * 1.1f;
            parent.AddChild(glow);
        }

        // Enhanced internal glow core
        var innerGlowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(toxicGlow.R, toxicGlow.G, toxicGlow.B, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = toxicGlow * 0.8f,
            EmissionEnergyMultiplier = 2f
        };
        var innerGlowMesh = new SphereMesh { Radius = 0.25f, Height = 0.5f, RadialSegments = 24, Rings = 18 };
        var innerGlow = new MeshInstance3D { Mesh = innerGlowMesh, MaterialOverride = innerGlowMat };
        innerGlow.Position = new Vector3(0, 0.4f * bossScale, 0);
        innerGlow.Scale = Vector3.One * bossScale * 0.6f;
        parent.AddChild(innerGlow);

        // Add OmniLight for real illumination
        var coreLight = new OmniLight3D();
        coreLight.LightColor = toxicGlow;
        coreLight.LightEnergy = 1.2f;
        coreLight.OmniRange = 4f * bossScale;
        coreLight.Position = new Vector3(0, 0.4f * bossScale, 0);
        parent.AddChild(coreLight);

        // Crown embedded in slime (partially submerged)
        var crownMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.7f, 0.2f),
            Metallic = 0.85f,
            Roughness = 0.25f,
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.7f, 0.2f) * 0.3f
        };

        // Slime coating on crown
        var slimeCoatMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(bossColor.R, bossColor.G, bossColor.B, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.15f
        };

        int spikeCount = 8;
        for (int i = 0; i < spikeCount; i++)
        {
            var spike = new MeshInstance3D();
            var spikeMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.06f, Height = 0.25f };
            spike.Mesh = spikeMesh;
            spike.MaterialOverride = crownMat;

            float angle = i * (360f / spikeCount) * Mathf.Pi / 180f;
            float crownRadius = 0.35f * bossScale;
            // Crown sits lower, partially embedded in slime
            spike.Position = new Vector3(
                Mathf.Cos(angle) * crownRadius,
                0.58f * bossScale,
                Mathf.Sin(angle) * crownRadius
            );
            spike.RotationDegrees = new Vector3(-10 - (i % 3) * 5, 0, (i % 2) * 8 - 4);
            spike.Scale = new Vector3(1f, 1f + (i % 2) * 0.3f, 1f);
            parent.AddChild(spike);

            // Slime dripping from spike tips
            if (i % 2 == 0)
            {
                var dripMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.005f, Height = 0.08f };
                var drip = new MeshInstance3D { Mesh = dripMesh, MaterialOverride = slimeCoatMat };
                drip.Position = new Vector3(
                    Mathf.Cos(angle) * crownRadius * 1.02f,
                    0.72f * bossScale,
                    Mathf.Sin(angle) * crownRadius * 1.02f
                );
                parent.AddChild(drip);
            }
        }

        // Crown band (lower, embedded)
        var crownBand = new MeshInstance3D();
        var bandMesh = new CylinderMesh { TopRadius = 0.38f * bossScale, BottomRadius = 0.42f * bossScale, Height = 0.08f };
        crownBand.Mesh = bandMesh;
        crownBand.MaterialOverride = crownMat;
        crownBand.Position = new Vector3(0, 0.52f * bossScale, 0);
        parent.AddChild(crownBand);

        // Slime coating over crown band
        var crownCoat = new MeshInstance3D();
        var coatMesh = new CylinderMesh { TopRadius = 0.4f * bossScale, BottomRadius = 0.44f * bossScale, Height = 0.1f };
        crownCoat.Mesh = coatMesh;
        crownCoat.MaterialOverride = slimeCoatMat;
        crownCoat.Position = new Vector3(0, 0.51f * bossScale, 0);
        parent.AddChild(crownCoat);

        // Many bubbles
        var bubbleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(bubbleColor.R, bubbleColor.G, bubbleColor.B, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.08f,
            Metallic = 0.25f
        };

        for (int i = 0; i < 20; i++)
        {
            var bubble = new MeshInstance3D();
            var bubbleMesh = new SphereMesh { Radius = 0.04f + GD.Randf() * 0.06f, Height = 0.08f + GD.Randf() * 0.12f, RadialSegments = 16, Rings = 12 };
            bubble.Mesh = bubbleMesh;
            bubble.MaterialOverride = bubbleMat;

            float angle = GD.Randf() * 360f;
            float radius = GD.Randf() * 0.3f;
            bubble.Position = new Vector3(
                Mathf.Cos(angle * Mathf.Pi / 180f) * radius * bossScale,
                (0.2f + GD.Randf() * 0.5f) * bossScale,
                Mathf.Sin(angle * Mathf.Pi / 180f) * radius * bossScale
            );
            bubble.Scale = Vector3.One * (0.8f + GD.Randf() * 0.6f);
            parent.AddChild(bubble);
        }

        // Toxic dripping tendrils
        var dripMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(bossColor.R, bossColor.G, bossColor.B, 0.8f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = toxicGlow * 0.1f,
            Roughness = 0.2f
        };

        for (int i = 0; i < 12; i++)
        {
            var drip = new MeshInstance3D();
            float dripHeight = 0.15f + GD.Randf() * 0.25f;
            var dripMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.08f, Height = dripHeight };
            drip.Mesh = dripMesh;
            drip.MaterialOverride = dripMat;

            float angle = (i / 12f * 360f + GD.Randf() * 20f) * Mathf.Pi / 180f;
            float dripDist = 0.32f + GD.Randf() * 0.15f;
            drip.Position = new Vector3(
                Mathf.Cos(angle) * dripDist * bossScale,
                0.1f * bossScale,
                Mathf.Sin(angle) * dripDist * bossScale
            );
            drip.RotationDegrees = new Vector3(0, 0, Mathf.Sin(angle * 3) * 20f);
            drip.Scale = new Vector3(1f + GD.Randf() * 0.5f, 1f, 1f + GD.Randf() * 0.5f);
            parent.AddChild(drip);
        }

        // Menacing eyes - three red glowing eyes
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.3f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.1f) * 0.6f,
            Roughness = 0.15f
        };
        var pupilMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.Black,
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0f, 0f) * 0.3f
        };

        // Slime King body radius scaled = 0.38 * 2.5 = 0.95
        // Using Cute proportions for proper slime eyes
        float slimeKingBodyR = 0.38f * bossScale;
        var (skEyeR, skPupilR, _, skEyeX, _, _) = CalculateEyeGeometry(slimeKingBodyR, EyeProportions.Cute, bossScale);
        // Custom positions for 3-eye slime king layout
        float skEyeY = 0.52f * bossScale;
        float skEyeZ = 0.28f * bossScale;
        float skPupilZ = skEyeZ + skEyeR * 0.65f;

        var eyeMesh = new SphereMesh { Radius = skEyeR, Height = skEyeR * 2f, RadialSegments = 24, Rings = 20 };
        var pupilMesh = new SphereMesh { Radius = skPupilR, Height = skPupilR * 2f, RadialSegments = 20, Rings = 16 };

        // Left eye
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-skEyeX, skEyeY, skEyeZ);
        leftEye.Scale = new Vector3(1.1f, 1.2f, 0.9f);
        parent.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-skEyeX, skEyeY, skPupilZ);
        leftPupil.Scale = new Vector3(1.2f, 1.1f, 1f);
        parent.AddChild(leftPupil);

        // Right eye
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(skEyeX, skEyeY, skEyeZ);
        rightEye.Scale = new Vector3(1.15f, 1.15f, 0.85f);
        parent.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(skEyeX, skEyeY, skPupilZ);
        rightPupil.Scale = new Vector3(1.1f, 1.2f, 1f);
        parent.AddChild(rightPupil);

        // Center eye (third eye) - higher position
        var centerEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        centerEye.Position = new Vector3(0, 0.68f * bossScale, 0.22f * bossScale);
        centerEye.Scale = new Vector3(1f, 1.1f, 0.8f);
        parent.AddChild(centerEye);
        var centerPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        centerPupil.Position = new Vector3(0, 0.68f * bossScale, 0.22f * bossScale + skEyeR * 0.65f);
        centerPupil.Scale = new Vector3(1f, 1.1f, 1f);
        parent.AddChild(centerPupil);

        // Large menacing mouth with teeth - Height = 2*Radius for proper sphere
        var mouthMesh = new SphereMesh { Radius = 0.08f, Height = 0.16f };
        var mouthMat = new StandardMaterial3D
        {
            AlbedoColor = bossColor.Darkened(0.6f),
            EmissionEnabled = true,
            Emission = toxicGlow * 0.05f
        };
        var mouth = new MeshInstance3D { Mesh = mouthMesh, MaterialOverride = mouthMat };
        mouth.Position = new Vector3(0, 0.42f * bossScale, 0.38f * bossScale);
        mouth.Scale = new Vector3(2.2f, 1f, 0.6f);
        parent.AddChild(mouth);

        // Teeth
        var toothMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.8f), Roughness = 0.6f };
        var toothMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.025f, Height = 0.08f };

        for (int i = 0; i < 8; i++)
        {
            var tooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
            float toothX = -0.14f + i * 0.04f;
            tooth.Position = new Vector3(toothX * bossScale, 0.42f * bossScale, 0.42f * bossScale);
            tooth.RotationDegrees = new Vector3(90, 0, 0);
            tooth.Scale = new Vector3(1f, 1f, 0.8f + (i % 2) * 0.4f);
            parent.AddChild(tooth);
        }
    }

    // ========================================================================
    // NEW DCC-THEMED MONSTERS - 15 NEW TYPES
    // ========================================================================

    /// <summary>
    /// Crawler Killer - Robot assassin sent to eliminate crawlers
    /// Metallic humanoid with glowing red eyes and angular design
    /// </summary>
    private static void CreateCrawlerKillerMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.0f;
        Color metalColor = colorOverride ?? new Color(0.3f, 0.32f, 0.35f); // Gunmetal gray
        Color accentColor = new Color(0.8f, 0.1f, 0.1f); // Red accents
        Color glowColor = new Color(1f, 0.2f, 0.2f); // Red glow

        int radialSegs = GetRadialSegments(lod);

        var metalMat = new StandardMaterial3D { AlbedoColor = metalColor, Metallic = 0.9f, Roughness = 0.3f };
        var darkMetalMat = new StandardMaterial3D { AlbedoColor = metalColor.Darkened(0.3f), Metallic = 0.8f, Roughness = 0.4f };
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = glowColor,
            EmissionEnabled = true,
            Emission = glowColor,
            EmissionEnergyMultiplier = 2f
        };

        // Body - angular box torso
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh { Size = new Vector3(0.4f * scale, 0.5f * scale, 0.25f * scale) };
        body.MaterialOverride = metalMat;
        body.Position = new Vector3(0, 0.7f * scale, 0);
        parent.AddChild(body);

        // Mechanical panel lines on body (High LOD)
        if (lod == LODLevel.High)
        {
            var panelLineMat = new StandardMaterial3D { AlbedoColor = metalColor.Darkened(0.5f), Metallic = 0.7f, Roughness = 0.6f };
            // Horizontal panel lines
            for (int i = 0; i < 3; i++)
            {
                var hLine = new MeshInstance3D();
                hLine.Mesh = new BoxMesh { Size = new Vector3(0.38f * scale, 0.008f * scale, 0.01f * scale) };
                hLine.MaterialOverride = panelLineMat;
                hLine.Position = new Vector3(0, 0.55f * scale + i * 0.15f * scale, 0.13f * scale);
                parent.AddChild(hLine);
            }
            // Vertical panel lines
            for (int i = 0; i < 2; i++)
            {
                var vLine = new MeshInstance3D();
                vLine.Mesh = new BoxMesh { Size = new Vector3(0.008f * scale, 0.45f * scale, 0.01f * scale) };
                vLine.MaterialOverride = panelLineMat;
                vLine.Position = new Vector3(-0.12f * scale + i * 0.24f * scale, 0.7f * scale, 0.13f * scale);
                parent.AddChild(vLine);
            }
            // Side panel lines
            for (int side = -1; side <= 1; side += 2)
            {
                var sideLine = new MeshInstance3D();
                sideLine.Mesh = new BoxMesh { Size = new Vector3(0.01f * scale, 0.35f * scale, 0.008f * scale) };
                sideLine.MaterialOverride = panelLineMat;
                sideLine.Position = new Vector3(side * 0.2f * scale, 0.7f * scale, 0);
                parent.AddChild(sideLine);
            }
        }

        // Chest plate accent
        var chestPlate = new MeshInstance3D();
        chestPlate.Mesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.3f * scale, 0.05f * scale) };
        chestPlate.MaterialOverride = darkMetalMat;
        chestPlate.Position = new Vector3(0, 0.75f * scale, 0.15f * scale);
        parent.AddChild(chestPlate);

        // Head node for animation
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.1f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Head - angular box
        var head = new MeshInstance3D();
        head.Mesh = new BoxMesh { Size = new Vector3(0.22f * scale, 0.25f * scale, 0.2f * scale) };
        head.MaterialOverride = metalMat;
        headNode.AddChild(head);

        // LED eyes with enhanced glow effect
        var eyeMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.02f * scale, 0.02f * scale) };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        leftEye.Position = new Vector3(-0.05f * scale, 0.03f * scale, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        rightEye.Position = new Vector3(0.05f * scale, 0.03f * scale, 0.1f * scale);
        headNode.AddChild(rightEye);

        // LED eye light halo effect (High LOD)
        if (lod == LODLevel.High)
        {
            var haloMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(glowColor.R, glowColor.G, glowColor.B, 0.3f),
                EmissionEnabled = true,
                Emission = glowColor * 0.5f,
                EmissionEnergyMultiplier = 1f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            var haloMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.03f * scale, 0.01f * scale) };
            var leftHalo = new MeshInstance3D { Mesh = haloMesh, MaterialOverride = haloMat };
            leftHalo.Position = new Vector3(-0.05f * scale, 0.03f * scale, 0.11f * scale);
            headNode.AddChild(leftHalo);
            var rightHalo = new MeshInstance3D { Mesh = haloMesh, MaterialOverride = haloMat };
            rightHalo.Position = new Vector3(0.05f * scale, 0.03f * scale, 0.11f * scale);
            headNode.AddChild(rightHalo);
        }

        // Arms
        float shoulderY = 0.85f * scale;
        float shoulderX = 0.25f * scale;

        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightUpperArm = new MeshInstance3D();
        rightUpperArm.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.05f * scale, Height = 0.25f * scale, RadialSegments = radialSegs };
        rightUpperArm.MaterialOverride = metalMat;
        rightUpperArm.Position = new Vector3(0, -0.12f * scale, 0);
        rightArmNode.AddChild(rightUpperArm);

        // Servo joint at right elbow (cylinder with rings)
        if (lod >= LODLevel.Medium)
        {
            var servoMat = new StandardMaterial3D { AlbedoColor = metalColor.Lightened(0.1f), Metallic = 0.95f, Roughness = 0.2f };
            var servoMesh = new CylinderMesh { TopRadius = 0.035f * scale, BottomRadius = 0.035f * scale, Height = 0.04f * scale, RadialSegments = radialSegs };
            var rightElbow = new MeshInstance3D { Mesh = servoMesh, MaterialOverride = servoMat };
            rightElbow.Position = new Vector3(0, -0.26f * scale, 0);
            rightElbow.RotationDegrees = new Vector3(90, 0, 0);
            rightArmNode.AddChild(rightElbow);
            // Servo ring detail
            if (lod == LODLevel.High)
            {
                var ringMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.04f * scale, Height = 0.008f * scale, RadialSegments = radialSegs };
                var elbowRing = new MeshInstance3D { Mesh = ringMesh, MaterialOverride = darkMetalMat };
                elbowRing.Position = new Vector3(0, -0.26f * scale, 0);
                elbowRing.RotationDegrees = new Vector3(90, 0, 0);
                rightArmNode.AddChild(elbowRing);
            }
        }

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftUpperArm = new MeshInstance3D();
        leftUpperArm.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.05f * scale, Height = 0.25f * scale, RadialSegments = radialSegs };
        leftUpperArm.MaterialOverride = metalMat;
        leftUpperArm.Position = new Vector3(0, -0.12f * scale, 0);
        leftArmNode.AddChild(leftUpperArm);

        // Servo joint at left elbow
        if (lod >= LODLevel.Medium)
        {
            var servoMat = new StandardMaterial3D { AlbedoColor = metalColor.Lightened(0.1f), Metallic = 0.95f, Roughness = 0.2f };
            var servoMesh = new CylinderMesh { TopRadius = 0.035f * scale, BottomRadius = 0.035f * scale, Height = 0.04f * scale, RadialSegments = radialSegs };
            var leftElbow = new MeshInstance3D { Mesh = servoMesh, MaterialOverride = servoMat };
            leftElbow.Position = new Vector3(0, -0.26f * scale, 0);
            leftElbow.RotationDegrees = new Vector3(90, 0, 0);
            leftArmNode.AddChild(leftElbow);
        }

        // Legs
        float hipY = 0.45f * scale;
        float hipX = 0.1f * scale;

        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.04f * scale, Height = 0.4f * scale, RadialSegments = radialSegs };
        rightLeg.MaterialOverride = metalMat;
        rightLeg.Position = new Vector3(0, -0.2f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.04f * scale, Height = 0.4f * scale, RadialSegments = radialSegs };
        leftLeg.MaterialOverride = metalMat;
        leftLeg.Position = new Vector3(0, -0.2f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Servo joints at knees (Medium and High LOD)
        if (lod >= LODLevel.Medium)
        {
            var kneeServoMat = new StandardMaterial3D { AlbedoColor = metalColor.Lightened(0.1f), Metallic = 0.95f, Roughness = 0.2f };
            var kneeServoMesh = new CylinderMesh { TopRadius = 0.045f * scale, BottomRadius = 0.045f * scale, Height = 0.035f * scale, RadialSegments = radialSegs };
            // Right knee servo
            var rightKnee = new MeshInstance3D { Mesh = kneeServoMesh, MaterialOverride = kneeServoMat };
            rightKnee.Position = new Vector3(0, -0.22f * scale, 0.03f * scale);
            rightKnee.RotationDegrees = new Vector3(90, 0, 0);
            rightLegNode.AddChild(rightKnee);
            // Left knee servo
            var leftKnee = new MeshInstance3D { Mesh = kneeServoMesh, MaterialOverride = kneeServoMat };
            leftKnee.Position = new Vector3(0, -0.22f * scale, 0.03f * scale);
            leftKnee.RotationDegrees = new Vector3(90, 0, 0);
            leftLegNode.AddChild(leftKnee);
        }

        // Feet with armored plating
        var footMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.04f * scale, 0.12f * scale) };
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = darkMetalMat };
        rightFoot.Position = new Vector3(0, -0.42f * scale, 0.02f * scale);
        rightLegNode.AddChild(rightFoot);
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = darkMetalMat };
        leftFoot.Position = new Vector3(0, -0.42f * scale, 0.02f * scale);
        leftLegNode.AddChild(leftFoot);

        // Shoulder armor plates
        var shoulderMesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.06f * scale, 0.1f * scale) };
        var rightShoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = darkMetalMat };
        rightShoulder.Position = new Vector3(shoulderX + 0.05f * scale, shoulderY + 0.05f * scale, 0);
        rightShoulder.RotationDegrees = new Vector3(0, 0, -15);
        parent.AddChild(rightShoulder);
        var leftShoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = darkMetalMat };
        leftShoulder.Position = new Vector3(-shoulderX - 0.05f * scale, shoulderY + 0.05f * scale, 0);
        leftShoulder.RotationDegrees = new Vector3(0, 0, 15);
        parent.AddChild(leftShoulder);

        // Antenna on head
        var antennaMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.01f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2 };
        var antenna = new MeshInstance3D { Mesh = antennaMesh, MaterialOverride = metalMat };
        antenna.Position = new Vector3(0, 0.15f * scale, 0);
        headNode.AddChild(antenna);
        var antennaTip = new MeshInstance3D();
        antennaTip.Mesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.015f * scale, Height = 0.02f * scale, RadialSegments = radialSegs / 2 };
        antennaTip.MaterialOverride = glowMat;
        antennaTip.Position = new Vector3(0, 0.2f * scale, 0);
        headNode.AddChild(antennaTip);

        // Weapon arm blade - enhanced with multiple segments
        var bladeMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.52f, 0.55f), Metallic = 0.95f, Roughness = 0.15f };
        // Main blade
        var bladeMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.25f * scale, 0.06f * scale) };
        var blade = new MeshInstance3D { Mesh = bladeMesh, MaterialOverride = bladeMat };
        blade.Position = new Vector3(0, -0.3f * scale, 0.08f * scale);
        rightArmNode.AddChild(blade);
        // Blade edge (sharper detail)
        if (lod >= LODLevel.Medium)
        {
            var edgeMesh = new BoxMesh { Size = new Vector3(0.005f * scale, 0.24f * scale, 0.07f * scale) };
            var bladeEdge = new MeshInstance3D { Mesh = edgeMesh, MaterialOverride = metalMat };
            bladeEdge.Position = new Vector3(0, -0.3f * scale, 0.115f * scale);
            rightArmNode.AddChild(bladeEdge);
            // Blade back reinforcement
            var backMesh = new BoxMesh { Size = new Vector3(0.025f * scale, 0.2f * scale, 0.02f * scale) };
            var bladeBack = new MeshInstance3D { Mesh = backMesh, MaterialOverride = darkMetalMat };
            bladeBack.Position = new Vector3(0, -0.28f * scale, 0.045f * scale);
            rightArmNode.AddChild(bladeBack);
        }
        // Blade mounting point
        if (lod == LODLevel.High)
        {
            var mountMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.035f * scale, Height = 0.04f * scale, RadialSegments = radialSegs / 2 };
            var bladeMount = new MeshInstance3D { Mesh = mountMesh, MaterialOverride = darkMetalMat };
            bladeMount.Position = new Vector3(0, -0.16f * scale, 0.06f * scale);
            bladeMount.RotationDegrees = new Vector3(90, 0, 0);
            rightArmNode.AddChild(bladeMount);
        }
        limbs.Weapon = rightArmNode;
    }

    /// <summary>
    /// Shadow Stalker - Dark creature that hides in shadows
    /// Elongated dark humanoid with glowing white eyes
    /// </summary>
    private static void CreateShadowStalkerMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.0f;
        Color shadowColor = colorOverride ?? new Color(0.05f, 0.05f, 0.08f); // Near black
        Color glowColor = new Color(0.9f, 0.95f, 1f); // White glow

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var shadowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(shadowColor.R, shadowColor.G, shadowColor.B, 0.85f),
            Roughness = 0.95f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = glowColor,
            EmissionEnabled = true,
            Emission = glowColor,
            EmissionEnergyMultiplier = 3f
        };

        // Elongated body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.15f * scale, Height = 0.6f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = shadowMat;
        body.Position = new Vector3(0, 0.6f * scale, 0);
        parent.AddChild(body);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.1f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Small angular head
        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.12f * scale, Height = 0.2f * scale, RadialSegments = radialSegs };
        head.MaterialOverride = shadowMat;
        headNode.AddChild(head);

        // Piercing white eyes with enhanced glow - Height = 2*Radius for proper sphere
        var eyeMesh = new SphereMesh { Radius = 0.03f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        leftEye.Position = new Vector3(-0.04f * scale, 0.02f * scale, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        rightEye.Position = new Vector3(0.04f * scale, 0.02f * scale, 0.1f * scale);
        headNode.AddChild(rightEye);

        // Eye glow halos (bright aura in darkness) - High LOD
        if (lod == LODLevel.High)
        {
            var eyeHaloMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(glowColor.R, glowColor.G, glowColor.B, 0.25f),
                EmissionEnabled = true,
                Emission = glowColor * 0.6f,
                EmissionEnergyMultiplier = 2f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            var haloMesh = new SphereMesh { Radius = 0.045f * scale, Height = 0.09f * scale, RadialSegments = 10, Rings = 6 };
            var leftHalo = new MeshInstance3D { Mesh = haloMesh, MaterialOverride = eyeHaloMat };
            leftHalo.Position = new Vector3(-0.04f * scale, 0.02f * scale, 0.1f * scale);
            headNode.AddChild(leftHalo);
            var rightHalo = new MeshInstance3D { Mesh = haloMesh, MaterialOverride = eyeHaloMat };
            rightHalo.Position = new Vector3(0.04f * scale, 0.02f * scale, 0.1f * scale);
            headNode.AddChild(rightHalo);
        }

        // Smoky texture layers (semi-transparent wisps around body) - High and Medium LOD
        if (lod >= LODLevel.Medium)
        {
            var smokeMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(shadowColor.R, shadowColor.G, shadowColor.B, 0.3f),
                Roughness = 0.95f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            // Outer smoke layer around body
            var smokeMesh = new CylinderMesh { TopRadius = 0.16f * scale, BottomRadius = 0.2f * scale, Height = 0.5f * scale, RadialSegments = radialSegs };
            var smokeLayer = new MeshInstance3D { Mesh = smokeMesh, MaterialOverride = smokeMat };
            smokeLayer.Position = new Vector3(0, 0.6f * scale, 0);
            parent.AddChild(smokeLayer);
            // Head smoke wisp
            var headSmokeMesh = new SphereMesh { Radius = 0.14f * scale, Height = 0.28f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
            var headSmoke = new MeshInstance3D { Mesh = headSmokeMesh, MaterialOverride = smokeMat };
            headSmoke.Position = new Vector3(0, 0, 0);
            headNode.AddChild(headSmoke);
        }

        // Tattered edges (ragged shadow pieces trailing off body) - High LOD
        if (lod == LODLevel.High)
        {
            var tatteredMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(shadowColor.R, shadowColor.G, shadowColor.B, 0.5f),
                Roughness = 0.95f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            var ragMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.12f * scale, 0.01f * scale) };
            // Tattered pieces around bottom of body
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.Tau / 6f;
                var rag = new MeshInstance3D { Mesh = ragMesh, MaterialOverride = tatteredMat };
                rag.Position = new Vector3(Mathf.Cos(angle) * 0.14f * scale, 0.22f * scale, Mathf.Sin(angle) * 0.14f * scale);
                rag.RotationDegrees = new Vector3(15 + GD.Randf() * 20, angle * 57.3f, GD.Randf() * 20 - 10);
                parent.AddChild(rag);
            }
        }

        // Shadow tendrils extending from body
        var tendrilMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(shadowColor.R, shadowColor.G, shadowColor.B, 0.6f),
            Roughness = 0.9f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var tendrilMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.03f * scale, Height = 0.25f * scale, RadialSegments = radialSegs / 2 };
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4f + 0.3f;
            var tendril = new MeshInstance3D { Mesh = tendrilMesh, MaterialOverride = tendrilMat };
            tendril.Position = new Vector3(Mathf.Cos(angle) * 0.12f * scale, 0.4f * scale, Mathf.Sin(angle) * 0.12f * scale);
            tendril.RotationDegrees = new Vector3(Mathf.Cos(angle) * 30, 0, Mathf.Sin(angle) * 30);
            parent.AddChild(tendril);
        }

        // Long thin arms
        float shoulderY = 0.8f * scale;
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.18f * scale, shoulderY, 0);
        rightArmNode.RotationDegrees = new Vector3(0, 0, 15);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.03f * scale, Height = 0.4f * scale, RadialSegments = radialSegs };
        rightArm.MaterialOverride = shadowMat;
        rightArm.Position = new Vector3(0, -0.2f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.18f * scale, shoulderY, 0);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -15);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.03f * scale, Height = 0.4f * scale, RadialSegments = radialSegs };
        leftArm.MaterialOverride = shadowMat;
        leftArm.Position = new Vector3(0, -0.2f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Shadowy claws on hands (High and Medium LOD)
        if (lod >= LODLevel.Medium)
        {
            var clawMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.02f, 0.02f, 0.05f, 0.9f),
                Roughness = 0.7f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            var clawMesh = new CylinderMesh { TopRadius = 0.002f * scale, BottomRadius = 0.012f * scale, Height = 0.08f * scale, RadialSegments = 6 };
            // Right hand claws (4 fingers)
            for (int i = 0; i < 4; i++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.015f + i * 0.01f) * scale, -0.44f * scale, 0.02f * scale);
                claw.RotationDegrees = new Vector3(-50 - i * 5, 0, -10 + i * 7);
                rightArmNode.AddChild(claw);
            }
            // Left hand claws (4 fingers)
            for (int i = 0; i < 4; i++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((-0.015f + i * 0.01f) * scale, -0.44f * scale, 0.02f * scale);
                claw.RotationDegrees = new Vector3(-50 - i * 5, 0, -10 + i * 7);
                leftArmNode.AddChild(claw);
            }
        }

        // Ethereal trailing wisps behind body (High LOD)
        if (lod == LODLevel.High)
        {
            var wispMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(shadowColor.R * 1.5f, shadowColor.G * 1.5f, shadowColor.B * 1.5f, 0.2f),
                Roughness = 0.95f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            var wispMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.005f * scale, Height = 0.2f * scale, RadialSegments = 6 };
            // Trailing wisps behind the creature
            for (int i = 0; i < 5; i++)
            {
                var wisp = new MeshInstance3D { Mesh = wispMesh, MaterialOverride = wispMat };
                wisp.Position = new Vector3((GD.Randf() - 0.5f) * 0.15f * scale, 0.4f * scale + i * 0.08f * scale, -0.15f * scale - i * 0.04f * scale);
                wisp.RotationDegrees = new Vector3(30 + GD.Randf() * 20, GD.Randf() * 30, 0);
                parent.AddChild(wisp);
            }
        }

        // No legs - floating/tapered bottom
        var tail = new MeshInstance3D();
        tail.Mesh = new CylinderMesh { TopRadius = 0.15f * scale, BottomRadius = 0.02f * scale, Height = 0.3f * scale, RadialSegments = radialSegs };
        tail.MaterialOverride = shadowMat;
        tail.Position = new Vector3(0, 0.15f * scale, 0);
        parent.AddChild(tail);
    }

    /// <summary>
    /// Flesh Golem - Stitched-together corpse parts
    /// Bulky humanoid with mismatched limbs
    /// </summary>
    private static void CreateFleshGolemMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.2f; // Larger than normal
        Color fleshColor = colorOverride ?? new Color(0.5f, 0.45f, 0.4f); // Sickly gray-tan
        Color stitchColor = new Color(0.2f, 0.15f, 0.1f); // Dark brown stitches

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var fleshMat = new StandardMaterial3D { AlbedoColor = fleshColor, Roughness = 0.9f };
        var darkFleshMat = new StandardMaterial3D { AlbedoColor = fleshColor.Darkened(0.2f), Roughness = 0.85f };
        var stitchMat = new StandardMaterial3D { AlbedoColor = stitchColor, Roughness = 0.8f };

        // Massive body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.35f * scale, BottomRadius = 0.4f * scale, Height = 0.7f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = fleshMat;
        body.Position = new Vector3(0, 0.6f * scale, 0);
        parent.AddChild(body);

        // Hunched shoulders - Height = 2*Radius for proper sphere
        var leftShoulder = new MeshInstance3D();
        leftShoulder.Mesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale, RadialSegments = radialSegs, Rings = rings };
        leftShoulder.MaterialOverride = darkFleshMat;
        leftShoulder.Position = new Vector3(-0.3f * scale, 0.85f * scale, 0);
        parent.AddChild(leftShoulder);

        var rightShoulder = new MeshInstance3D();
        rightShoulder.Mesh = new SphereMesh { Radius = 0.18f * scale, Height = 0.36f * scale, RadialSegments = radialSegs, Rings = rings }; // Asymmetric
        rightShoulder.MaterialOverride = darkFleshMat;
        rightShoulder.Position = new Vector3(0.32f * scale, 0.9f * scale, 0);
        parent.AddChild(rightShoulder);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0.05f * scale, 1.0f * scale, 0.1f * scale); // Offset head
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.18f * scale, Height = 0.36f * scale, RadialSegments = radialSegs, Rings = rings };
        head.MaterialOverride = fleshMat;
        head.Scale = new Vector3(0.9f, 1.1f, 0.85f);
        headNode.AddChild(head);

        // Sunken eyes - Height = 2*Radius for proper sphere
        var eyeMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.5f, 0.3f) };
        var eyeMesh = new SphereMesh { Radius = 0.03f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.06f * scale, 0.02f * scale, 0.12f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.06f * scale, 0.04f * scale, 0.11f * scale); // Uneven
        headNode.AddChild(rightEye);

        // Mismatched arms
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.4f * scale, 0.75f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.1f * scale, BottomRadius = 0.12f * scale, Height = 0.45f * scale, RadialSegments = radialSegs };
        rightArm.MaterialOverride = fleshMat;
        rightArm.Position = new Vector3(0, -0.22f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.38f * scale, 0.7f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale, RadialSegments = radialSegs }; // Different size
        leftArm.MaterialOverride = darkFleshMat;
        leftArm.Position = new Vector3(0, -0.25f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Thick legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.15f * scale, 0.25f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.1f * scale, Height = 0.45f * scale, RadialSegments = radialSegs };
        rightLeg.MaterialOverride = fleshMat;
        rightLeg.Position = new Vector3(0, -0.22f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.15f * scale, 0.25f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.11f * scale, BottomRadius = 0.09f * scale, Height = 0.42f * scale, RadialSegments = radialSegs };
        leftLeg.MaterialOverride = darkFleshMat;
        leftLeg.Position = new Vector3(0, -0.21f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Stitches across body (visual detail) with zigzag pattern
        var stitchMesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.015f * scale, 0.02f * scale) };
        for (int i = 0; i < 4; i++)
        {
            var stitch = new MeshInstance3D { Mesh = stitchMesh, MaterialOverride = stitchMat };
            stitch.Position = new Vector3(0.05f * scale * (i % 2 == 0 ? 1 : -1), 0.4f * scale + i * 0.12f * scale, 0.35f * scale);
            stitch.RotationDegrees = new Vector3(0, 0, (i % 2 == 0 ? 10 : -10));
            parent.AddChild(stitch);
        }

        // Enhanced zigzag suture lines (High LOD)
        if (lod == LODLevel.High)
        {
            var sutureMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.008f * scale, 0.01f * scale) };
            // Zigzag across chest
            for (int i = 0; i < 8; i++)
            {
                var suture = new MeshInstance3D { Mesh = sutureMesh, MaterialOverride = stitchMat };
                float xOffset = (i % 2 == 0) ? -0.03f : 0.03f;
                suture.Position = new Vector3(xOffset * scale, 0.5f * scale + i * 0.04f * scale, 0.38f * scale);
                suture.RotationDegrees = new Vector3(0, 0, (i % 2 == 0) ? 45 : -45);
                parent.AddChild(suture);
            }
            // Sutures on face
            var faceSuture1 = new MeshInstance3D { Mesh = sutureMesh, MaterialOverride = stitchMat };
            faceSuture1.Position = new Vector3(-0.08f * scale, -0.02f * scale, 0.14f * scale);
            faceSuture1.RotationDegrees = new Vector3(0, 0, 30);
            headNode.AddChild(faceSuture1);
            var faceSuture2 = new MeshInstance3D { Mesh = sutureMesh, MaterialOverride = stitchMat };
            faceSuture2.Position = new Vector3(0.1f * scale, 0.06f * scale, 0.13f * scale);
            faceSuture2.RotationDegrees = new Vector3(0, 0, -25);
            headNode.AddChild(faceSuture2);
        }

        // Mismatched skin patches (color variation) - High and Medium LOD
        if (lod >= LODLevel.Medium)
        {
            var patchMat1 = new StandardMaterial3D { AlbedoColor = fleshColor.Lightened(0.15f), Roughness = 0.85f };
            var patchMat2 = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.4f, 0.45f), Roughness = 0.9f }; // Slightly purple tint
            var patchMat3 = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.45f, 0.35f), Roughness = 0.9f }; // Greenish tint
            // Body patches
            var patchMesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.15f * scale, 0.02f * scale) };
            var patch1 = new MeshInstance3D { Mesh = patchMesh, MaterialOverride = patchMat1 };
            patch1.Position = new Vector3(-0.15f * scale, 0.55f * scale, 0.32f * scale);
            patch1.RotationDegrees = new Vector3(0, 10, 5);
            parent.AddChild(patch1);
            var patch2 = new MeshInstance3D { Mesh = patchMesh, MaterialOverride = patchMat2 };
            patch2.Position = new Vector3(0.18f * scale, 0.7f * scale, 0.3f * scale);
            patch2.RotationDegrees = new Vector3(0, -8, -3);
            parent.AddChild(patch2);
            // Arm patches
            var armPatchMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.1f * scale, 0.015f * scale) };
            var armPatch = new MeshInstance3D { Mesh = armPatchMesh, MaterialOverride = patchMat3 };
            armPatch.Position = new Vector3(0.02f * scale, -0.15f * scale, 0.1f * scale);
            rightArmNode.AddChild(armPatch);
        }

        // Exposed muscle/sinew areas (red tissue showing through) - High LOD
        if (lod == LODLevel.High)
        {
            var muscleMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.25f, 0.2f), Roughness = 0.7f };
            var muscleMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.06f * scale, 0.01f * scale) };
            // Exposed muscle on shoulder
            var muscle1 = new MeshInstance3D { Mesh = muscleMesh, MaterialOverride = muscleMat };
            muscle1.Position = new Vector3(0.28f * scale, 0.88f * scale, 0.12f * scale);
            muscle1.RotationDegrees = new Vector3(0, -20, 15);
            parent.AddChild(muscle1);
            // Exposed muscle on leg
            var muscle2 = new MeshInstance3D { Mesh = muscleMesh, MaterialOverride = muscleMat };
            muscle2.Position = new Vector3(0.02f * scale, -0.1f * scale, 0.08f * scale);
            rightLegNode.AddChild(muscle2);
            // Neck muscle
            var neckMuscle = new MeshInstance3D { Mesh = muscleMesh, MaterialOverride = muscleMat };
            neckMuscle.Position = new Vector3(-0.05f * scale, 0.9f * scale, 0.25f * scale);
            parent.AddChild(neckMuscle);
        }

        // Exposed metal bolts/spikes and plates
        var boltMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.025f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2 };
        var boltMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.45f), Metallic = 0.8f, Roughness = 0.4f };
        var leftBolt = new MeshInstance3D { Mesh = boltMesh, MaterialOverride = boltMat };
        leftBolt.Position = new Vector3(-0.28f * scale, 0.92f * scale, 0.08f * scale);
        leftBolt.RotationDegrees = new Vector3(60, 0, -20);
        parent.AddChild(leftBolt);
        var rightBolt = new MeshInstance3D { Mesh = boltMesh, MaterialOverride = boltMat };
        rightBolt.Position = new Vector3(0.3f * scale, 0.95f * scale, 0.06f * scale);
        rightBolt.RotationDegrees = new Vector3(55, 0, 25);
        parent.AddChild(rightBolt);

        // Additional metal plates and bolts (High and Medium LOD)
        if (lod >= LODLevel.Medium)
        {
            // Metal plate on torso (reinforcement)
            var plateMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.35f, 0.4f), Metallic = 0.7f, Roughness = 0.5f };
            var plateMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.08f * scale, 0.015f * scale) };
            var chestPlate = new MeshInstance3D { Mesh = plateMesh, MaterialOverride = plateMat };
            chestPlate.Position = new Vector3(0.12f * scale, 0.65f * scale, 0.36f * scale);
            chestPlate.RotationDegrees = new Vector3(0, -10, 5);
            parent.AddChild(chestPlate);
            // Neck bolt
            var neckBolt = new MeshInstance3D { Mesh = boltMesh, MaterialOverride = boltMat };
            neckBolt.Position = new Vector3(0.08f * scale, 0.95f * scale, 0.15f * scale);
            neckBolt.RotationDegrees = new Vector3(70, 0, 15);
            parent.AddChild(neckBolt);
            // Head bolt (Frankenstein style)
            var headBoltMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.02f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2 };
            var headBoltL = new MeshInstance3D { Mesh = headBoltMesh, MaterialOverride = boltMat };
            headBoltL.Position = new Vector3(-0.14f * scale, 0, 0);
            headBoltL.RotationDegrees = new Vector3(0, 0, -90);
            headNode.AddChild(headBoltL);
            var headBoltR = new MeshInstance3D { Mesh = headBoltMesh, MaterialOverride = boltMat };
            headBoltR.Position = new Vector3(0.14f * scale, 0, 0);
            headBoltR.RotationDegrees = new Vector3(0, 0, 90);
            headNode.AddChild(headBoltR);
        }

        // Massive fists
        var fistMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.1f * scale, Height = 0.12f * scale, RadialSegments = radialSegs };
        var rightFist = new MeshInstance3D { Mesh = fistMesh, MaterialOverride = fleshMat };
        rightFist.Position = new Vector3(0, -0.5f * scale, 0);
        rightArmNode.AddChild(rightFist);
        var leftFist = new MeshInstance3D { Mesh = fistMesh, MaterialOverride = darkFleshMat };
        leftFist.Position = new Vector3(0, -0.52f * scale, 0);
        leftArmNode.AddChild(leftFist);
    }

    /// <summary>
    /// Plague Bearer - Diseased humanoid spreading infection
    /// Hunched figure with boils and dripping ooze
    /// </summary>
    private static void CreatePlagueBearerMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.0f;
        Color plagueColor = colorOverride ?? new Color(0.4f, 0.5f, 0.3f); // Sickly green
        Color boilColor = new Color(0.6f, 0.4f, 0.3f); // Pustule color
        Color oozeColor = new Color(0.3f, 0.45f, 0.2f); // Dripping ooze

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var plagueMat = new StandardMaterial3D { AlbedoColor = plagueColor, Roughness = 0.85f };
        var boilMat = new StandardMaterial3D
        {
            AlbedoColor = boilColor,
            EmissionEnabled = true,
            Emission = boilColor * 0.2f
        };

        // Hunched body
        var body = new MeshInstance3D();
        body.Mesh = new SphereMesh { Radius = 0.25f * scale, Height = 0.5f * scale, RadialSegments = radialSegs, Rings = rings };
        body.MaterialOverride = plagueMat;
        body.Position = new Vector3(0, 0.55f * scale, 0.05f * scale);
        body.Scale = new Vector3(1f, 1.2f, 0.9f);
        parent.AddChild(body);

        // Boils on body (reduce count at lower LOD) - Height = 2*Radius for proper sphere
        int boilCount = lod == LODLevel.High ? 5 : (lod == LODLevel.Medium ? 3 : 2);
        var boilMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        for (int i = 0; i < boilCount; i++)
        {
            var boil = new MeshInstance3D { Mesh = boilMesh, MaterialOverride = boilMat };
            float angle = i * Mathf.Tau / boilCount;
            boil.Position = new Vector3(
                Mathf.Cos(angle) * 0.2f * scale,
                0.5f * scale + (i % 2) * 0.15f * scale,
                Mathf.Sin(angle) * 0.15f * scale + 0.1f * scale
            );
            parent.AddChild(boil);
        }

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.95f * scale, 0.1f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale, RadialSegments = radialSegs, Rings = rings };
        head.MaterialOverride = plagueMat;
        head.Scale = new Vector3(1f, 0.9f, 0.85f);
        headNode.AddChild(head);

        // Sunken glowing eyes with sickly glow
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.9f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.6f, 0.8f, 0.2f),
            EmissionEnergyMultiplier = 1.5f
        };
        var eyeMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.05f * scale, 0, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.05f * scale, 0, 0.1f * scale);
        headNode.AddChild(rightEye);

        // Sickly glow aura (High LOD)
        if (lod == LODLevel.High)
        {
            var sickGlowMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.4f, 0.5f, 0.2f, 0.15f),
                EmissionEnabled = true,
                Emission = new Color(0.3f, 0.4f, 0.15f),
                EmissionEnergyMultiplier = 0.5f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            var auraMesh = new SphereMesh { Radius = 0.32f * scale, Height = 0.64f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
            var aura = new MeshInstance3D { Mesh = auraMesh, MaterialOverride = sickGlowMat };
            aura.Position = new Vector3(0, 0.55f * scale, 0.05f * scale);
            parent.AddChild(aura);
        }

        // Additional pustules/boils on head and limbs (High and Medium LOD)
        if (lod >= LODLevel.Medium)
        {
            var smallBoilMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = 8, Rings = 4 };
            // Head pustules
            var headBoil1 = new MeshInstance3D { Mesh = smallBoilMesh, MaterialOverride = boilMat };
            headBoil1.Position = new Vector3(-0.08f * scale, 0.05f * scale, 0.06f * scale);
            headNode.AddChild(headBoil1);
            var headBoil2 = new MeshInstance3D { Mesh = smallBoilMesh, MaterialOverride = boilMat };
            headBoil2.Position = new Vector3(0.06f * scale, -0.03f * scale, 0.08f * scale);
            headNode.AddChild(headBoil2);
        }

        // Arms
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.25f * scale, 0.7f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.04f * scale, Height = 0.3f * scale, RadialSegments = radialSegs };
        rightArm.MaterialOverride = plagueMat;
        rightArm.Position = new Vector3(0, -0.15f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.25f * scale, 0.7f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.04f * scale, Height = 0.3f * scale, RadialSegments = radialSegs };
        leftArm.MaterialOverride = plagueMat;
        leftArm.Position = new Vector3(0, -0.15f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.1f * scale, 0.3f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.05f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        rightLeg.MaterialOverride = plagueMat;
        rightLeg.Position = new Vector3(0, -0.17f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.1f * scale, 0.3f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.05f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        leftLeg.MaterialOverride = plagueMat;
        leftLeg.Position = new Vector3(0, -0.17f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Dripping ooze tendrils
        var oozeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(oozeColor.R, oozeColor.G, oozeColor.B, 0.7f),
            Roughness = 0.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var oozeMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.005f * scale, Height = 0.15f * scale, RadialSegments = radialSegs / 2 };
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5f;
            var ooze = new MeshInstance3D { Mesh = oozeMesh, MaterialOverride = oozeMat };
            ooze.Position = new Vector3(
                Mathf.Cos(angle) * 0.22f * scale,
                0.35f * scale,
                Mathf.Sin(angle) * 0.18f * scale
            );
            parent.AddChild(ooze);
        }

        // Mouth with drool
        var mouthMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.02f * scale, RadialSegments = radialSegs / 2 };
        var mouth = new MeshInstance3D { Mesh = mouthMesh, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.15f, 0.1f) } };
        mouth.Position = new Vector3(0, -0.05f * scale, 0.12f * scale);
        mouth.RotationDegrees = new Vector3(90, 0, 0);
        headNode.AddChild(mouth);

        // Hunched spine bumps
        var spineMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.04f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 2 };
        for (int i = 0; i < 4; i++)
        {
            var spine = new MeshInstance3D { Mesh = spineMesh, MaterialOverride = plagueMat };
            spine.Position = new Vector3(0, 0.65f * scale + i * 0.08f * scale, -0.18f * scale);
            spine.RotationDegrees = new Vector3(-25 - i * 5, 0, 0);
            parent.AddChild(spine);
        }

        // Bandaged areas (wrapping around limbs and body) - High and Medium LOD
        if (lod >= LODLevel.Medium)
        {
            var bandageMat = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.65f, 0.55f), Roughness = 0.95f };
            // Arm bandage (wrap effect using stacked cylinders)
            var bandageMesh = new CylinderMesh { TopRadius = 0.055f * scale, BottomRadius = 0.055f * scale, Height = 0.03f * scale, RadialSegments = radialSegs / 2 };
            for (int i = 0; i < 3; i++)
            {
                var bandage = new MeshInstance3D { Mesh = bandageMesh, MaterialOverride = bandageMat };
                bandage.Position = new Vector3(0, -0.08f * scale - i * 0.04f * scale, 0);
                bandage.RotationDegrees = new Vector3(0, i * 20, 0);
                leftArmNode.AddChild(bandage);
            }
            // Torso bandage
            var torsoBandageMesh = new BoxMesh { Size = new Vector3(0.25f * scale, 0.06f * scale, 0.02f * scale) };
            var torsoBandage = new MeshInstance3D { Mesh = torsoBandageMesh, MaterialOverride = bandageMat };
            torsoBandage.Position = new Vector3(0.05f * scale, 0.7f * scale, 0.22f * scale);
            torsoBandage.RotationDegrees = new Vector3(0, 0, 15);
            parent.AddChild(torsoBandage);
        }

        // Infected wounds (open sores with sick coloring) - High LOD
        if (lod == LODLevel.High)
        {
            var woundMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.3f, 0.25f), Roughness = 0.6f };
            var infectedMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 0.4f, 0.2f),
                EmissionEnabled = true,
                Emission = new Color(0.2f, 0.3f, 0.1f) * 0.3f
            };
            var woundMesh = new BoxMesh { Size = new Vector3(0.05f * scale, 0.03f * scale, 0.008f * scale) };
            // Wound on torso
            var torsoWound = new MeshInstance3D { Mesh = woundMesh, MaterialOverride = woundMat };
            torsoWound.Position = new Vector3(-0.12f * scale, 0.5f * scale, 0.23f * scale);
            parent.AddChild(torsoWound);
            var torsoInfection = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.02f * scale, Height = 0.04f * scale }, MaterialOverride = infectedMat };
            torsoInfection.Position = new Vector3(-0.12f * scale, 0.5f * scale, 0.24f * scale);
            parent.AddChild(torsoInfection);
            // Wound on leg
            var legWound = new MeshInstance3D { Mesh = woundMesh, MaterialOverride = woundMat };
            legWound.Position = new Vector3(0.02f * scale, -0.1f * scale, 0.05f * scale);
            rightLegNode.AddChild(legWound);
        }
    }

    /// <summary>
    /// Living Armor - Animated empty suit of armor
    /// Floating armor pieces with glowing core
    /// </summary>
    private static void CreateLivingArmorMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.1f;
        Color armorColor = colorOverride ?? new Color(0.4f, 0.42f, 0.45f); // Steel gray
        Color glowColor = new Color(0.3f, 0.5f, 0.9f); // Blue magical glow

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var armorMat = new StandardMaterial3D { AlbedoColor = armorColor, Metallic = 0.8f, Roughness = 0.4f };
        var darkArmorMat = new StandardMaterial3D { AlbedoColor = armorColor.Darkened(0.25f), Metallic = 0.7f, Roughness = 0.5f };
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = glowColor,
            EmissionEnabled = true,
            Emission = glowColor,
            EmissionEnergyMultiplier = 1.5f
        };

        // Chest plate
        var chest = new MeshInstance3D();
        chest.Mesh = new BoxMesh { Size = new Vector3(0.45f * scale, 0.5f * scale, 0.25f * scale) };
        chest.MaterialOverride = armorMat;
        chest.Position = new Vector3(0, 0.7f * scale, 0);
        parent.AddChild(chest);

        // Plate separation lines on chest (High LOD)
        if (lod == LODLevel.High)
        {
            var platLineMat = new StandardMaterial3D { AlbedoColor = armorColor.Darkened(0.4f), Metallic = 0.6f, Roughness = 0.6f };
            // Horizontal plate lines
            for (int i = 0; i < 3; i++)
            {
                var hLine = new MeshInstance3D();
                hLine.Mesh = new BoxMesh { Size = new Vector3(0.42f * scale, 0.01f * scale, 0.01f * scale) };
                hLine.MaterialOverride = platLineMat;
                hLine.Position = new Vector3(0, 0.5f * scale + i * 0.15f * scale, 0.13f * scale);
                parent.AddChild(hLine);
            }
            // Vertical center line
            var vLine = new MeshInstance3D();
            vLine.Mesh = new BoxMesh { Size = new Vector3(0.01f * scale, 0.45f * scale, 0.01f * scale) };
            vLine.MaterialOverride = platLineMat;
            vLine.Position = new Vector3(0, 0.7f * scale, 0.13f * scale);
            parent.AddChild(vLine);
        }

        // Glowing core in chest - Height = 2*Radius for proper sphere
        var core = new MeshInstance3D();
        core.Mesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale, RadialSegments = radialSegs, Rings = rings };
        core.MaterialOverride = glowMat;
        core.Position = new Vector3(0, 0.7f * scale, 0.1f * scale);
        parent.AddChild(core);

        // Head node (helmet)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.1f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var helmet = new MeshInstance3D();
        helmet.Mesh = new BoxMesh { Size = new Vector3(0.25f * scale, 0.28f * scale, 0.22f * scale) };
        helmet.MaterialOverride = armorMat;
        headNode.AddChild(helmet);

        // Visor slit with glow (enhanced with depth)
        var visor = new MeshInstance3D();
        visor.Mesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.03f * scale, 0.02f * scale) };
        visor.MaterialOverride = glowMat;
        visor.Position = new Vector3(0, 0, 0.11f * scale);
        headNode.AddChild(visor);

        // Visor detail - dark backing and frame (High LOD)
        if (lod == LODLevel.High)
        {
            // Dark void behind visor
            var visorVoidMat = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.02f, 0.05f), Roughness = 0.95f };
            var visorVoid = new MeshInstance3D();
            visorVoid.Mesh = new BoxMesh { Size = new Vector3(0.16f * scale, 0.025f * scale, 0.03f * scale) };
            visorVoid.MaterialOverride = visorVoidMat;
            visorVoid.Position = new Vector3(0, 0, 0.08f * scale);
            headNode.AddChild(visorVoid);
            // Visor frame/rim
            var visorFrameMesh = new BoxMesh { Size = new Vector3(0.2f * scale, 0.008f * scale, 0.02f * scale) };
            var topFrame = new MeshInstance3D { Mesh = visorFrameMesh, MaterialOverride = darkArmorMat };
            topFrame.Position = new Vector3(0, 0.02f * scale, 0.11f * scale);
            headNode.AddChild(topFrame);
            var bottomFrame = new MeshInstance3D { Mesh = visorFrameMesh, MaterialOverride = darkArmorMat };
            bottomFrame.Position = new Vector3(0, -0.02f * scale, 0.11f * scale);
            headNode.AddChild(bottomFrame);
            // Helmet crest/ridge
            var helmetCrestMesh = new BoxMesh { Size = new Vector3(0.03f * scale, 0.1f * scale, 0.2f * scale) };
            var helmetCrest = new MeshInstance3D { Mesh = helmetCrestMesh, MaterialOverride = darkArmorMat };
            helmetCrest.Position = new Vector3(0, 0.18f * scale, 0);
            headNode.AddChild(helmetCrest);
        }

        // Ghostly inner glow from armor gaps (High and Medium LOD)
        if (lod >= LODLevel.Medium)
        {
            var ghostGlowMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(glowColor.R, glowColor.G, glowColor.B, 0.4f),
                EmissionEnabled = true,
                Emission = glowColor * 0.6f,
                EmissionEnergyMultiplier = 1f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            // Glow from neck gap
            var neckGlow = new MeshInstance3D();
            neckGlow.Mesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.1f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2 };
            neckGlow.MaterialOverride = ghostGlowMat;
            neckGlow.Position = new Vector3(0, 0.98f * scale, 0);
            parent.AddChild(neckGlow);
            // Glow from waist gap
            var waistGlow = new MeshInstance3D();
            waistGlow.Mesh = new CylinderMesh { TopRadius = 0.15f * scale, BottomRadius = 0.12f * scale, Height = 0.04f * scale, RadialSegments = radialSegs / 2 };
            waistGlow.MaterialOverride = ghostGlowMat;
            waistGlow.Position = new Vector3(0, 0.44f * scale, 0);
            parent.AddChild(waistGlow);
        }

        // Pauldrons (shoulder armor)
        var leftPauldron = new MeshInstance3D();
        leftPauldron.Mesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.12f * scale, 0.15f * scale) };
        leftPauldron.MaterialOverride = darkArmorMat;
        leftPauldron.Position = new Vector3(-0.28f * scale, 0.88f * scale, 0);
        parent.AddChild(leftPauldron);

        var rightPauldron = new MeshInstance3D();
        rightPauldron.Mesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.12f * scale, 0.15f * scale) };
        rightPauldron.MaterialOverride = darkArmorMat;
        rightPauldron.Position = new Vector3(0.28f * scale, 0.88f * scale, 0);
        parent.AddChild(rightPauldron);

        // Gauntlets (arms)
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.28f * scale, 0.75f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightGauntlet = new MeshInstance3D();
        rightGauntlet.Mesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.35f * scale, 0.1f * scale) };
        rightGauntlet.MaterialOverride = armorMat;
        rightGauntlet.Position = new Vector3(0, -0.17f * scale, 0);
        rightArmNode.AddChild(rightGauntlet);

        // Gauntlet detail - elbow guard and wrist (High LOD)
        if (lod == LODLevel.High)
        {
            var elbowGuard = new MeshInstance3D();
            elbowGuard.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.08f * scale, 0.08f * scale) };
            elbowGuard.MaterialOverride = darkArmorMat;
            elbowGuard.Position = new Vector3(0, 0.02f * scale, -0.02f * scale);
            rightArmNode.AddChild(elbowGuard);
            // Wrist cuff
            var wristCuff = new MeshInstance3D();
            wristCuff.Mesh = new BoxMesh { Size = new Vector3(0.11f * scale, 0.04f * scale, 0.11f * scale) };
            wristCuff.MaterialOverride = darkArmorMat;
            wristCuff.Position = new Vector3(0, -0.32f * scale, 0);
            rightArmNode.AddChild(wristCuff);
        }

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.28f * scale, 0.75f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftGauntlet = new MeshInstance3D();
        leftGauntlet.Mesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.35f * scale, 0.1f * scale) };
        leftGauntlet.MaterialOverride = armorMat;
        leftGauntlet.Position = new Vector3(0, -0.17f * scale, 0);
        leftArmNode.AddChild(leftGauntlet);

        // Left gauntlet detail (High LOD)
        if (lod == LODLevel.High)
        {
            var leftElbowGuard = new MeshInstance3D();
            leftElbowGuard.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.08f * scale, 0.08f * scale) };
            leftElbowGuard.MaterialOverride = darkArmorMat;
            leftElbowGuard.Position = new Vector3(0, 0.02f * scale, -0.02f * scale);
            leftArmNode.AddChild(leftElbowGuard);
            var leftWristCuff = new MeshInstance3D();
            leftWristCuff.Mesh = new BoxMesh { Size = new Vector3(0.11f * scale, 0.04f * scale, 0.11f * scale) };
            leftWristCuff.MaterialOverride = darkArmorMat;
            leftWristCuff.Position = new Vector3(0, -0.32f * scale, 0);
            leftArmNode.AddChild(leftWristCuff);
        }

        // Greaves (legs)
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.12f * scale, 0.4f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightGreave = new MeshInstance3D();
        rightGreave.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.4f * scale, 0.12f * scale) };
        rightGreave.MaterialOverride = armorMat;
        rightGreave.Position = new Vector3(0, -0.2f * scale, 0);
        rightLegNode.AddChild(rightGreave);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.12f * scale, 0.4f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftGreave = new MeshInstance3D();
        leftGreave.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.4f * scale, 0.12f * scale) };
        leftGreave.MaterialOverride = armorMat;
        leftGreave.Position = new Vector3(0, -0.2f * scale, 0);
        leftLegNode.AddChild(leftGreave);

        // Floating magical particles between armor pieces
        var particleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(glowColor.R, glowColor.G, glowColor.B, 0.5f),
            EmissionEnabled = true,
            Emission = glowColor,
            EmissionEnergyMultiplier = 1f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var particleMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.01f * scale, Height = 0.03f * scale, RadialSegments = 6 };
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6f;
            var particle = new MeshInstance3D { Mesh = particleMesh, MaterialOverride = particleMat };
            particle.Position = new Vector3(
                Mathf.Cos(angle) * 0.15f * scale,
                0.95f * scale,
                Mathf.Sin(angle) * 0.15f * scale
            );
            parent.AddChild(particle);
        }

        // Armored boots
        var bootMesh = new BoxMesh { Size = new Vector3(0.14f * scale, 0.08f * scale, 0.18f * scale) };
        var rightBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = darkArmorMat };
        rightBoot.Position = new Vector3(0, -0.44f * scale, 0.02f * scale);
        rightLegNode.AddChild(rightBoot);
        var leftBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = darkArmorMat };
        leftBoot.Position = new Vector3(0, -0.44f * scale, 0.02f * scale);
        leftLegNode.AddChild(leftBoot);

        // Sword in right hand
        var swordMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.6f, 0.65f), Metallic = 0.9f, Roughness = 0.2f };
        var bladeMesh = new BoxMesh { Size = new Vector3(0.03f * scale, 0.5f * scale, 0.08f * scale) };
        var sword = new MeshInstance3D { Mesh = bladeMesh, MaterialOverride = swordMat };
        sword.Position = new Vector3(0.08f * scale, -0.4f * scale, 0);
        rightArmNode.AddChild(sword);
        limbs.Weapon = rightArmNode;

        // Shield on left arm
        var shieldMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.35f * scale, 0.25f * scale) };
        var shield = new MeshInstance3D { Mesh = shieldMesh, MaterialOverride = armorMat };
        shield.Position = new Vector3(-0.1f * scale, -0.25f * scale, 0.08f * scale);
        leftArmNode.AddChild(shield);
        var shieldBoss = new MeshInstance3D();
        shieldBoss.Mesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.06f * scale, Height = 0.03f * scale, RadialSegments = radialSegs };
        shieldBoss.MaterialOverride = darkArmorMat;
        shieldBoss.Position = new Vector3(-0.12f * scale, -0.25f * scale, 0.16f * scale);
        shieldBoss.RotationDegrees = new Vector3(90, 0, 0);
        leftArmNode.AddChild(shieldBoss);

        // Helmet crest
        var crestMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.12f * scale, 0.18f * scale) };
        var crest = new MeshInstance3D { Mesh = crestMesh, MaterialOverride = darkArmorMat };
        crest.Position = new Vector3(0, 0.18f * scale, 0);
        headNode.AddChild(crest);
    }

    /// <summary>
    /// Camera Drone - Flying surveillance drone
    /// Spherical body with lens and propellers
    /// </summary>
    private static void CreateCameraDroneMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 0.6f; // Small flying enemy
        Color bodyColor = colorOverride ?? new Color(0.3f, 0.3f, 0.35f);
        Color lensColor = new Color(0.1f, 0.1f, 0.15f);
        Color lightColor = new Color(1f, 0.2f, 0.2f); // Red recording light
        Color accentColor = new Color(0.4f, 0.4f, 0.45f);

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Metallic = 0.6f, Roughness = 0.4f };
        var accentMat = new StandardMaterial3D { AlbedoColor = accentColor, Metallic = 0.7f, Roughness = 0.35f };
        var lensMat = new StandardMaterial3D { AlbedoColor = lensColor, Metallic = 0.9f, Roughness = 0.1f };
        var glassMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.25f, 0.35f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Metallic = 0.8f,
            Roughness = 0.05f
        };
        var lightMat = new StandardMaterial3D
        {
            AlbedoColor = lightColor,
            EmissionEnabled = true,
            Emission = lightColor,
            EmissionEnergyMultiplier = 2f
        };

        // Floating offset
        float floatY = 1.2f;

        // Main body (sphere) - Height = 2*Radius for proper sphere
        var body = new MeshInstance3D();
        body.Mesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.4f * scale, RadialSegments = radialSegs, Rings = rings };
        body.MaterialOverride = bodyMat;
        body.Position = new Vector3(0, floatY, 0);
        parent.AddChild(body);

        // Body accent ring (equatorial)
        var bodyRing = new MeshInstance3D();
        bodyRing.Mesh = new CylinderMesh { TopRadius = 0.205f * scale, BottomRadius = 0.205f * scale, Height = 0.02f * scale, RadialSegments = radialSegs };
        bodyRing.MaterialOverride = accentMat;
        bodyRing.Position = new Vector3(0, floatY, 0);
        parent.AddChild(bodyRing);

        // === CAMERA GIMBAL SYSTEM ===
        // Gimbal mount (Y-axis rotation)
        var gimbalMount = new MeshInstance3D();
        gimbalMount.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.06f * scale, Height = 0.02f * scale, RadialSegments = radialSegs };
        gimbalMount.MaterialOverride = accentMat;
        gimbalMount.Position = new Vector3(0, floatY - 0.1f * scale, 0.1f * scale);
        gimbalMount.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(gimbalMount);

        // Gimbal arm (connects to camera housing)
        var gimbalArm = new MeshInstance3D();
        gimbalArm.Mesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.06f * scale, 0.02f * scale) };
        gimbalArm.MaterialOverride = bodyMat;
        gimbalArm.Position = new Vector3(0, floatY - 0.12f * scale, 0.12f * scale);
        parent.AddChild(gimbalArm);

        // Camera housing (box around lens)
        var cameraHousing = new MeshInstance3D();
        cameraHousing.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.1f * scale, 0.08f * scale) };
        cameraHousing.MaterialOverride = bodyMat;
        cameraHousing.Position = new Vector3(0, floatY - 0.12f * scale, 0.18f * scale);
        parent.AddChild(cameraHousing);

        // Camera lens (main) - multi-element lens detail
        var lensOuter = new MeshInstance3D();
        lensOuter.Mesh = new CylinderMesh { TopRadius = 0.045f * scale, BottomRadius = 0.05f * scale, Height = 0.04f * scale, RadialSegments = radialSegs };
        lensOuter.MaterialOverride = lensMat;
        lensOuter.Position = new Vector3(0, floatY - 0.12f * scale, 0.24f * scale);
        lensOuter.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(lensOuter);

        // Lens glass element - Height = 2*Radius for proper sphere
        var lensGlass = new MeshInstance3D();
        lensGlass.Mesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = radialSegs, Rings = rings };
        lensGlass.MaterialOverride = glassMat;
        lensGlass.Position = new Vector3(0, floatY - 0.12f * scale, 0.27f * scale);
        lensGlass.Scale = new Vector3(1f, 1f, 0.3f);
        parent.AddChild(lensGlass);

        // Inner lens ring (aperture detail)
        var apertureRing = new MeshInstance3D();
        apertureRing.Mesh = new CylinderMesh { TopRadius = 0.025f * scale, BottomRadius = 0.03f * scale, Height = 0.01f * scale, RadialSegments = radialSegs };
        apertureRing.MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Black, Roughness = 0.9f };
        apertureRing.Position = new Vector3(0, floatY - 0.12f * scale, 0.22f * scale);
        apertureRing.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(apertureRing);

        // Lens hood
        var lensHood = new MeshInstance3D();
        lensHood.Mesh = new CylinderMesh { TopRadius = 0.055f * scale, BottomRadius = 0.05f * scale, Height = 0.015f * scale, RadialSegments = radialSegs };
        lensHood.MaterialOverride = lensMat;
        lensHood.Position = new Vector3(0, floatY - 0.12f * scale, 0.285f * scale);
        lensHood.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(lensHood);

        // Recording light on camera housing - Height = 2*Radius for proper sphere
        var recLight = new MeshInstance3D();
        recLight.Mesh = new SphereMesh { Radius = 0.015f * scale, Height = 0.03f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        recLight.MaterialOverride = lightMat;
        recLight.Position = new Vector3(0.05f * scale, floatY - 0.08f * scale, 0.22f * scale);
        parent.AddChild(recLight);

        // Head node (the whole drone is the "head" for animation)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, floatY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // === PROPELLER ARMS WITH HOVER JETS ===
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4f + Mathf.Tau / 8f;
            float armX = Mathf.Cos(angle) * 0.18f * scale;
            float armZ = Mathf.Sin(angle) * 0.18f * scale;

            // Arm structure
            var arm = new MeshInstance3D();
            arm.Mesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.025f * scale, 0.15f * scale) };
            arm.MaterialOverride = bodyMat;
            arm.Position = new Vector3(armX, floatY, armZ);
            var armTarget = new Vector3(0, floatY, 0);
            var armDir = (armTarget - arm.Position).Normalized();
            if (armDir.LengthSquared() > 0.001f)
            {
                float armYaw = Mathf.Atan2(armDir.X, armDir.Z);
                arm.Rotation = new Vector3(0, armYaw, 0);
            }
            parent.AddChild(arm);

            // Motor housing
            var motorHousing = new MeshInstance3D();
            motorHousing.Mesh = new CylinderMesh { TopRadius = 0.035f * scale, BottomRadius = 0.04f * scale, Height = 0.03f * scale, RadialSegments = radialSegs };
            motorHousing.MaterialOverride = accentMat;
            motorHousing.Position = new Vector3(armX * 1.5f, floatY + 0.015f * scale, armZ * 1.5f);
            parent.AddChild(motorHousing);

            // Propeller hub
            var propHub = new MeshInstance3D();
            propHub.Mesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.02f * scale, RadialSegments = radialSegs };
            propHub.MaterialOverride = bodyMat;
            propHub.Position = new Vector3(armX * 1.5f, floatY + 0.04f * scale, armZ * 1.5f);
            parent.AddChild(propHub);

            // Propeller blades (3 per motor)
            for (int b = 0; b < 3; b++)
            {
                var bladeMesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.004f * scale, 0.018f * scale) };
                var blade = new MeshInstance3D { Mesh = bladeMesh, MaterialOverride = accentMat };
                blade.Position = new Vector3(armX * 1.5f, floatY + 0.045f * scale, armZ * 1.5f);
                blade.RotationDegrees = new Vector3(0, i * 45 + b * 120, 5);
                parent.AddChild(blade);
            }

            // Hover jet glow under each motor (emissive)
            var jetGlowMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.3f, 0.6f, 1f, 0.6f),
                EmissionEnabled = true,
                Emission = new Color(0.4f, 0.7f, 1f),
                EmissionEnergyMultiplier = 1.5f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            var jetGlow = new MeshInstance3D();
            jetGlow.Mesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.02f * scale, Height = 0.04f * scale, RadialSegments = radialSegs / 2 };
            jetGlow.MaterialOverride = jetGlowMat;
            jetGlow.Position = new Vector3(armX * 1.5f, floatY - 0.02f * scale, armZ * 1.5f);
            parent.AddChild(jetGlow);

            // LED status light on each arm
            var armLightMat = new StandardMaterial3D
            {
                AlbedoColor = i % 2 == 0 ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.5f, 0.1f),
                EmissionEnabled = true,
                Emission = i % 2 == 0 ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.5f, 0.1f),
                EmissionEnergyMultiplier = 1.5f
            };
            var armLight = new MeshInstance3D();
            armLight.Mesh = new CylinderMesh { TopRadius = 0.008f * scale, BottomRadius = 0.008f * scale, Height = 0.005f * scale, RadialSegments = 6 };
            armLight.MaterialOverride = armLightMat;
            armLight.Position = new Vector3(armX * 1.2f, floatY + 0.015f * scale, armZ * 1.2f);
            parent.AddChild(armLight);
        }

        // === ANTENNA ARRAY (dual antennas) ===
        var antennaMat = new StandardMaterial3D { AlbedoColor = bodyColor.Darkened(0.2f), Metallic = 0.5f, Roughness = 0.5f };

        // Main antenna
        var mainAntenna = new MeshInstance3D();
        mainAntenna.Mesh = new CylinderMesh { TopRadius = 0.004f * scale, BottomRadius = 0.01f * scale, Height = 0.08f * scale, RadialSegments = 6 };
        mainAntenna.MaterialOverride = antennaMat;
        mainAntenna.Position = new Vector3(0, floatY + 0.24f * scale, 0);
        parent.AddChild(mainAntenna);

        // Antenna tip ball - Height = 2*Radius for proper sphere
        var antennaTip = new MeshInstance3D();
        antennaTip.Mesh = new SphereMesh { Radius = 0.012f * scale, Height = 0.024f * scale, RadialSegments = 8, Rings = 6 };
        antennaTip.MaterialOverride = lightMat;
        antennaTip.Position = new Vector3(0, floatY + 0.29f * scale, 0);
        parent.AddChild(antennaTip);

        // Secondary antenna (shorter, angled)
        var secondAntenna = new MeshInstance3D();
        secondAntenna.Mesh = new CylinderMesh { TopRadius = 0.003f * scale, BottomRadius = 0.006f * scale, Height = 0.05f * scale, RadialSegments = 6 };
        secondAntenna.MaterialOverride = antennaMat;
        secondAntenna.Position = new Vector3(-0.04f * scale, floatY + 0.2f * scale, -0.02f * scale);
        secondAntenna.RotationDegrees = new Vector3(-15, 0, 20);
        parent.AddChild(secondAntenna);

        // === STATUS LIGHTS PANEL ===
        // Green power light
        var greenMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 1f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 1f, 0.3f),
            EmissionEnergyMultiplier = 1.5f
        };
        var statusLight1 = new MeshInstance3D();
        statusLight1.Mesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.01f * scale, Height = 0.005f * scale, RadialSegments = 8 };
        statusLight1.MaterialOverride = greenMat;
        statusLight1.Position = new Vector3(-0.08f * scale, floatY + 0.12f * scale, 0.12f * scale);
        statusLight1.RotationDegrees = new Vector3(45, 0, 0);
        parent.AddChild(statusLight1);

        // Blue WiFi/signal light
        var blueMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.5f, 1f),
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.5f, 1f),
            EmissionEnergyMultiplier = 1.5f
        };
        var statusLight2 = new MeshInstance3D();
        statusLight2.Mesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.01f * scale, Height = 0.005f * scale, RadialSegments = 8 };
        statusLight2.MaterialOverride = blueMat;
        statusLight2.Position = new Vector3(0.08f * scale, floatY + 0.12f * scale, 0.12f * scale);
        statusLight2.RotationDegrees = new Vector3(45, 0, 0);
        parent.AddChild(statusLight2);

        // Yellow battery light
        var yellowMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.8f, 0.2f),
            EmissionEnergyMultiplier = 1.2f
        };
        var statusLight3 = new MeshInstance3D();
        statusLight3.Mesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.01f * scale, Height = 0.005f * scale, RadialSegments = 8 };
        statusLight3.MaterialOverride = yellowMat;
        statusLight3.Position = new Vector3(0, floatY + 0.14f * scale, 0.14f * scale);
        statusLight3.RotationDegrees = new Vector3(45, 0, 0);
        parent.AddChild(statusLight3);

        // === SENSOR ARRAY (back) ===
        var sensorMat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.15f, 0.2f), Metallic = 0.7f, Roughness = 0.3f };
        var sensorPanel = new MeshInstance3D();
        sensorPanel.Mesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.06f * scale, 0.02f * scale) };
        sensorPanel.MaterialOverride = sensorMat;
        sensorPanel.Position = new Vector3(0, floatY, -0.18f * scale);
        parent.AddChild(sensorPanel);

        // Sensor dots on panel - Height = 2*Radius for proper sphere
        var sensorDotMesh = new SphereMesh { Radius = 0.008f * scale, Height = 0.016f * scale, RadialSegments = 6, Rings = 4 };
        for (int i = 0; i < 3; i++)
        {
            var sensorDot = new MeshInstance3D { Mesh = sensorDotMesh, MaterialOverride = lensMat };
            sensorDot.Position = new Vector3((i - 1) * 0.03f * scale, floatY + 0.01f * scale, -0.19f * scale);
            parent.AddChild(sensorDot);
        }

        // Cooling vents on sides
        var ventMat = new StandardMaterial3D { AlbedoColor = bodyColor.Darkened(0.3f), Roughness = 0.8f };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int v = 0; v < 3; v++)
            {
                var vent = new MeshInstance3D();
                vent.Mesh = new BoxMesh { Size = new Vector3(0.01f * scale, 0.04f * scale, 0.008f * scale) };
                vent.MaterialOverride = ventMat;
                vent.Position = new Vector3(side * 0.19f * scale, floatY + (v - 1) * 0.03f * scale, -0.05f * scale);
                parent.AddChild(vent);
            }
        }
    }

    /// <summary>
    /// Shock Drone - Flying electric attack drone
    /// Angular body with electric coils
    /// </summary>
    private static void CreateShockDroneMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 0.7f;
        Color bodyColor = colorOverride ?? new Color(0.25f, 0.28f, 0.35f);
        Color electricColor = new Color(0.4f, 0.6f, 1f); // Electric blue

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Metallic = 0.7f, Roughness = 0.35f };
        var electricMat = new StandardMaterial3D
        {
            AlbedoColor = electricColor,
            EmissionEnabled = true,
            Emission = electricColor,
            EmissionEnergyMultiplier = 2.5f
        };

        float floatY = 1.3f;

        // Angular main body
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh { Size = new Vector3(0.3f * scale, 0.15f * scale, 0.25f * scale) };
        body.MaterialOverride = bodyMat;
        body.Position = new Vector3(0, floatY, 0);
        parent.AddChild(body);

        // Electric coils on sides
        var coilMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.04f * scale, Height = 0.1f * scale, RadialSegments = radialSegs };
        var leftCoil = new MeshInstance3D { Mesh = coilMesh, MaterialOverride = electricMat };
        leftCoil.Position = new Vector3(-0.2f * scale, floatY, 0);
        leftCoil.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(leftCoil);

        var rightCoil = new MeshInstance3D { Mesh = coilMesh, MaterialOverride = electricMat };
        rightCoil.Position = new Vector3(0.2f * scale, floatY, 0);
        rightCoil.RotationDegrees = new Vector3(0, 0, 90);
        parent.AddChild(rightCoil);

        // Front weapon barrel
        var barrel = new MeshInstance3D();
        barrel.Mesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.05f * scale, Height = 0.15f * scale, RadialSegments = radialSegs };
        barrel.MaterialOverride = bodyMat;
        barrel.Position = new Vector3(0, floatY - 0.05f * scale, 0.15f * scale);
        barrel.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(barrel);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, floatY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Sensor eyes - Height = 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = electricMat };
        leftEye.Position = new Vector3(-0.08f * scale, 0.05f * scale, 0.12f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = electricMat };
        rightEye.Position = new Vector3(0.08f * scale, 0.05f * scale, 0.12f * scale);
        headNode.AddChild(rightEye);

        // Electric arcs between coils (decorative)
        var arcMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(electricColor.R, electricColor.G, electricColor.B, 0.6f),
            EmissionEnabled = true,
            Emission = electricColor,
            EmissionEnergyMultiplier = 3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var arcMesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.01f * scale, 0.01f * scale) };
        for (int i = 0; i < 3; i++)
        {
            var arc = new MeshInstance3D { Mesh = arcMesh, MaterialOverride = arcMat };
            arc.Position = new Vector3(0, floatY + (i - 1) * 0.03f * scale, 0);
            arc.RotationDegrees = new Vector3(0, 0, (i - 1) * 8);
            parent.AddChild(arc);
        }

        // Rear exhaust vents
        var ventMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.04f * scale, 0.02f * scale) };
        var ventMat = new StandardMaterial3D { AlbedoColor = bodyColor.Darkened(0.3f), Metallic = 0.6f, Roughness = 0.5f };
        for (int i = 0; i < 2; i++)
        {
            var vent = new MeshInstance3D { Mesh = ventMesh, MaterialOverride = ventMat };
            vent.Position = new Vector3((i == 0 ? -0.08f : 0.08f) * scale, floatY - 0.02f * scale, -0.14f * scale);
            parent.AddChild(vent);
        }

        // Stabilizer fins
        var finMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.1f * scale, 0.08f * scale) };
        var leftFin = new MeshInstance3D { Mesh = finMesh, MaterialOverride = bodyMat };
        leftFin.Position = new Vector3(-0.16f * scale, floatY + 0.05f * scale, -0.1f * scale);
        leftFin.RotationDegrees = new Vector3(0, 0, 15);
        parent.AddChild(leftFin);
        var rightFin = new MeshInstance3D { Mesh = finMesh, MaterialOverride = bodyMat };
        rightFin.Position = new Vector3(0.16f * scale, floatY + 0.05f * scale, -0.1f * scale);
        rightFin.RotationDegrees = new Vector3(0, 0, -15);
        parent.AddChild(rightFin);
    }

    /// <summary>
    /// Advertiser Bot - Ad-projecting annoying robot
    /// Boxy robot with screen and wheels
    /// </summary>
    private static void CreateAdvertiserBotMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 0.9f;
        Color bodyColor = colorOverride ?? new Color(0.9f, 0.9f, 0.92f); // White plastic
        Color screenColor = new Color(0.2f, 0.6f, 0.9f); // Blue screen

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Roughness = 0.6f };
        var screenMat = new StandardMaterial3D
        {
            AlbedoColor = screenColor,
            EmissionEnabled = true,
            Emission = screenColor,
            EmissionEnergyMultiplier = 1.5f
        };

        // Main body (boxy)
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.5f * scale, 0.25f * scale) };
        body.MaterialOverride = bodyMat;
        body.Position = new Vector3(0, 0.5f * scale, 0);
        parent.AddChild(body);

        // Screen on front
        var screen = new MeshInstance3D();
        screen.Mesh = new BoxMesh { Size = new Vector3(0.28f * scale, 0.25f * scale, 0.02f * scale) };
        screen.MaterialOverride = screenMat;
        screen.Position = new Vector3(0, 0.55f * scale, 0.14f * scale);
        parent.AddChild(screen);

        // Head/antenna
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.85f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var antenna = new MeshInstance3D();
        antenna.Mesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.02f * scale, Height = 0.15f * scale, RadialSegments = radialSegs };
        antenna.MaterialOverride = bodyMat;
        antenna.Position = new Vector3(0, 0.07f * scale, 0);
        headNode.AddChild(antenna);

        // Antenna tip - Height = 2*Radius for proper sphere
        var antennaTip = new MeshInstance3D();
        antennaTip.Mesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        antennaTip.MaterialOverride = screenMat;
        antennaTip.Position = new Vector3(0, 0.15f * scale, 0);
        headNode.AddChild(antennaTip);

        // Arms (small)
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.22f * scale, 0.55f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.2f * scale, 0.06f * scale) };
        rightArm.MaterialOverride = bodyMat;
        rightArm.Position = new Vector3(0, -0.1f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.22f * scale, 0.55f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.2f * scale, 0.06f * scale) };
        leftArm.MaterialOverride = bodyMat;
        leftArm.Position = new Vector3(0, -0.1f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Wheels (legs)
        var wheelMat = new StandardMaterial3D { AlbedoColor = Colors.DarkGray, Roughness = 0.7f };
        var wheelMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.08f * scale, Height = 0.04f * scale, RadialSegments = radialSegs };

        var rightWheelNode = new Node3D();
        rightWheelNode.Position = new Vector3(0.12f * scale, 0.08f * scale, 0);
        parent.AddChild(rightWheelNode);
        limbs.RightLeg = rightWheelNode;

        var rightWheel = new MeshInstance3D { Mesh = wheelMesh, MaterialOverride = wheelMat };
        rightWheel.RotationDegrees = new Vector3(0, 0, 90);
        rightWheelNode.AddChild(rightWheel);

        var leftWheelNode = new Node3D();
        leftWheelNode.Position = new Vector3(-0.12f * scale, 0.08f * scale, 0);
        parent.AddChild(leftWheelNode);
        limbs.LeftLeg = leftWheelNode;

        var leftWheel = new MeshInstance3D { Mesh = wheelMesh, MaterialOverride = wheelMat };
        leftWheel.RotationDegrees = new Vector3(0, 0, 90);
        leftWheelNode.AddChild(leftWheel);

        // Rear caster wheel
        var casterNode = new Node3D();
        casterNode.Position = new Vector3(0, 0.05f * scale, -0.1f * scale);
        parent.AddChild(casterNode);
        var caster = new MeshInstance3D();
        caster.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.04f * scale, Height = 0.025f * scale, RadialSegments = radialSegs };
        caster.MaterialOverride = wheelMat;
        caster.RotationDegrees = new Vector3(0, 0, 90);
        casterNode.AddChild(caster);

        // Speaker grilles on sides
        var grilleMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.2f), Roughness = 0.8f };
        var grilleMesh = new BoxMesh { Size = new Vector3(0.01f * scale, 0.15f * scale, 0.15f * scale) };
        var leftGrille = new MeshInstance3D { Mesh = grilleMesh, MaterialOverride = grilleMat };
        leftGrille.Position = new Vector3(-0.185f * scale, 0.45f * scale, 0);
        parent.AddChild(leftGrille);
        var rightGrille = new MeshInstance3D { Mesh = grilleMesh, MaterialOverride = grilleMat };
        rightGrille.Position = new Vector3(0.185f * scale, 0.45f * scale, 0);
        parent.AddChild(rightGrille);

        // Scrolling ad text bar (decorative)
        var textBarMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.9f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.8f, 0.1f),
            EmissionEnergyMultiplier = 1f
        };
        var textBar = new MeshInstance3D();
        textBar.Mesh = new BoxMesh { Size = new Vector3(0.26f * scale, 0.03f * scale, 0.01f * scale) };
        textBar.MaterialOverride = textBarMat;
        textBar.Position = new Vector3(0, 0.38f * scale, 0.14f * scale);
        parent.AddChild(textBar);

        // Logo plate
        var logoMat = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.2f, 0.2f), Roughness = 0.4f };
        var logo = new MeshInstance3D();
        logo.Mesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.05f * scale, 0.01f * scale) };
        logo.MaterialOverride = logoMat;
        logo.Position = new Vector3(0, 0.72f * scale, 0.14f * scale);
        parent.AddChild(logo);

        // Hands with promotional material
        var handMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.04f * scale, 0.02f * scale) };
        var rightHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = bodyMat };
        rightHand.Position = new Vector3(0, -0.22f * scale, 0);
        rightArmNode.AddChild(rightHand);
        var leftHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = bodyMat };
        leftHand.Position = new Vector3(0, -0.22f * scale, 0);
        leftArmNode.AddChild(leftHand);

        // Flyer in hand
        var flyerMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.08f * scale, 0.002f * scale) };
        var flyerMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 1f, 0.9f) };
        var flyer = new MeshInstance3D { Mesh = flyerMesh, MaterialOverride = flyerMat };
        flyer.Position = new Vector3(0.02f * scale, -0.28f * scale, 0.02f * scale);
        flyer.RotationDegrees = new Vector3(15, 0, 10);
        rightArmNode.AddChild(flyer);
    }

    /// <summary>
    /// Clockwork Spider - Mechanical spider with gears
    /// 8-legged with visible rotating gears
    /// </summary>
    private static void CreateClockworkSpiderMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 0.8f;
        Color brassColor = colorOverride ?? new Color(0.7f, 0.55f, 0.3f); // Brass
        Color gearColor = new Color(0.5f, 0.5f, 0.55f); // Steel gears

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var brassMat = new StandardMaterial3D { AlbedoColor = brassColor, Metallic = 0.8f, Roughness = 0.35f };
        var gearMat = new StandardMaterial3D { AlbedoColor = gearColor, Metallic = 0.9f, Roughness = 0.3f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.3f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f)
        };

        // Main body (spherical) - Height = 2*Radius for proper sphere
        var body = new MeshInstance3D();
        body.Mesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.4f * scale, RadialSegments = radialSegs, Rings = rings };
        body.MaterialOverride = brassMat;
        body.Position = new Vector3(0, 0.3f * scale, 0);
        parent.AddChild(body);

        // Visible gears on body
        var gearMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.08f * scale, Height = 0.02f * scale, RadialSegments = radialSegs };
        var gear1 = new MeshInstance3D { Mesh = gearMesh, MaterialOverride = gearMat };
        gear1.Position = new Vector3(0.12f * scale, 0.3f * scale, 0.1f * scale);
        gear1.RotationDegrees = new Vector3(45, 0, 0);
        parent.AddChild(gear1);

        var gear2 = new MeshInstance3D { Mesh = gearMesh, MaterialOverride = gearMat };
        gear2.Position = new Vector3(-0.1f * scale, 0.35f * scale, 0.08f * scale);
        gear2.RotationDegrees = new Vector3(-30, 20, 0);
        parent.AddChild(gear2);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.35f * scale, 0.15f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Multiple eyes (reduce count at lower LOD) - Height = 2*Radius for proper spheres
        int eyeCount = lod == LODLevel.High ? 4 : (lod == LODLevel.Medium ? 3 : 2);
        var eyeMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        for (int i = 0; i < eyeCount; i++)
        {
            var eye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
            float eyeX = (i - 1.5f) * 0.04f * scale;
            float eyeY = (i % 2) * 0.03f * scale;
            eye.Position = new Vector3(eyeX, eyeY, 0.05f * scale);
            headNode.AddChild(eye);
        }

        // 8 legs (4 on each side)
        var legMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.01f * scale, Height = 0.25f * scale, RadialSegments = radialSegs };
        for (int side = 0; side < 2; side++)
        {
            float sideX = side == 0 ? -1f : 1f;
            for (int i = 0; i < 4; i++)
            {
                float legAngle = (i - 1.5f) * 25f;
                float legZ = (i - 1.5f) * 0.08f * scale;

                var legNode = new Node3D();
                legNode.Position = new Vector3(sideX * 0.15f * scale, 0.25f * scale, legZ);
                legNode.RotationDegrees = new Vector3(legAngle, sideX * 60, sideX * 45);
                parent.AddChild(legNode);

                var leg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = brassMat };
                leg.Position = new Vector3(0, -0.12f * scale, 0);
                legNode.AddChild(leg);

                // Assign first and last legs as limb nodes
                if (i == 0 && side == 0) limbs.LeftLeg = legNode;
                if (i == 0 && side == 1) limbs.RightLeg = legNode;
                if (i == 3 && side == 0) limbs.LeftArm = legNode;
                if (i == 3 && side == 1) limbs.RightArm = legNode;

                // Leg joints (knee)
                var jointMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.02f * scale, RadialSegments = radialSegs / 2 };
                var joint = new MeshInstance3D { Mesh = jointMesh, MaterialOverride = gearMat };
                joint.Position = new Vector3(0, -0.2f * scale, 0);
                joint.RotationDegrees = new Vector3(90, 0, 0);
                legNode.AddChild(joint);

                // Lower leg segment
                var lowerLegMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.008f * scale, Height = 0.18f * scale, RadialSegments = radialSegs / 2 };
                var lowerLeg = new MeshInstance3D { Mesh = lowerLegMesh, MaterialOverride = brassMat };
                lowerLeg.Position = new Vector3(0, -0.3f * scale, 0.05f * scale);
                lowerLeg.RotationDegrees = new Vector3(-45, 0, 0);
                legNode.AddChild(lowerLeg);

                // Foot claw
                var clawMesh = new CylinderMesh { TopRadius = 0.003f * scale, BottomRadius = 0.008f * scale, Height = 0.04f * scale, RadialSegments = 4 };
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = gearMat };
                claw.Position = new Vector3(0, -0.42f * scale, 0.12f * scale);
                claw.RotationDegrees = new Vector3(-70, 0, 0);
                legNode.AddChild(claw);
            }
        }

        // Central gear mechanism on top
        var mainGearMesh = new CylinderMesh { TopRadius = 0.1f * scale, BottomRadius = 0.1f * scale, Height = 0.025f * scale, RadialSegments = radialSegs };
        var mainGear = new MeshInstance3D { Mesh = mainGearMesh, MaterialOverride = gearMat };
        mainGear.Position = new Vector3(0, 0.42f * scale, 0);
        parent.AddChild(mainGear);

        // Wind-up key on back
        var keyShaftMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.015f * scale, Height = 0.1f * scale, RadialSegments = radialSegs / 2 };
        var keyShaft = new MeshInstance3D { Mesh = keyShaftMesh, MaterialOverride = gearMat };
        keyShaft.Position = new Vector3(0, 0.3f * scale, -0.18f * scale);
        keyShaft.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(keyShaft);
        var keyHandleMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.03f * scale, 0.015f * scale) };
        var keyHandle = new MeshInstance3D { Mesh = keyHandleMesh, MaterialOverride = brassMat };
        keyHandle.Position = new Vector3(0, 0.3f * scale, -0.24f * scale);
        parent.AddChild(keyHandle);

        // Mandibles/pincers at front
        var mandibleMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.015f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2 };
        var leftMandible = new MeshInstance3D { Mesh = mandibleMesh, MaterialOverride = brassMat };
        leftMandible.Position = new Vector3(-0.06f * scale, 0.28f * scale, 0.18f * scale);
        leftMandible.RotationDegrees = new Vector3(110, 0, 15);
        parent.AddChild(leftMandible);
        var rightMandible = new MeshInstance3D { Mesh = mandibleMesh, MaterialOverride = brassMat };
        rightMandible.Position = new Vector3(0.06f * scale, 0.28f * scale, 0.18f * scale);
        rightMandible.RotationDegrees = new Vector3(110, 0, -15);
        parent.AddChild(rightMandible);
    }

    /// <summary>
    /// Lava Elemental - Living magma creature
    /// Humanoid made of molten rock with glowing cracks
    /// </summary>
    private static void CreateLavaElementalMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.1f;
        Color rockColor = colorOverride ?? new Color(0.15f, 0.1f, 0.08f); // Dark rock
        Color lavaColor = new Color(1f, 0.4f, 0.1f); // Orange lava
        Color coreColor = new Color(1f, 0.8f, 0.3f); // Bright core

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var rockMat = new StandardMaterial3D { AlbedoColor = rockColor, Roughness = 0.95f };
        var lavaMat = new StandardMaterial3D
        {
            AlbedoColor = lavaColor,
            EmissionEnabled = true,
            Emission = lavaColor,
            EmissionEnergyMultiplier = 2f
        };
        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = coreColor,
            EmissionEnabled = true,
            Emission = coreColor,
            EmissionEnergyMultiplier = 3f
        };

        // Body with glowing core
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.2f * scale, BottomRadius = 0.25f * scale, Height = 0.5f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = rockMat;
        body.Position = new Vector3(0, 0.6f * scale, 0);
        parent.AddChild(body);

        // Lava cracks (stripes) - reduce at lower LOD
        int crackCount = lod == LODLevel.High ? 4 : (lod == LODLevel.Medium ? 3 : 2);
        var crackMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.4f * scale, 0.03f * scale) };
        for (int i = 0; i < crackCount; i++)
        {
            float angle = i * Mathf.Tau / crackCount;
            var crack = new MeshInstance3D { Mesh = crackMesh, MaterialOverride = lavaMat };
            crack.Position = new Vector3(Mathf.Cos(angle) * 0.22f * scale, 0.6f * scale, Mathf.Sin(angle) * 0.22f * scale);
            // Calculate rotation manually (LookAt requires node to be in tree)
            var crackTarget = new Vector3(0, 0.6f * scale, 0);
            var crackDir = (crackTarget - crack.Position).Normalized();
            if (crackDir.LengthSquared() > 0.001f)
            {
                float crackYaw = Mathf.Atan2(crackDir.X, crackDir.Z);
                float crackPitch = -Mathf.Asin(crackDir.Y);
                crack.Rotation = new Vector3(crackPitch + Mathf.Pi / 2f, crackYaw, 0);
            }
            parent.AddChild(crack);
        }

        // Glowing core center - Height = 2*Radius for proper sphere
        var core = new MeshInstance3D();
        core.Mesh = new SphereMesh { Radius = 0.1f * scale, Height = 0.2f * scale, RadialSegments = radialSegs, Rings = rings };
        core.MaterialOverride = coreMat;
        core.Position = new Vector3(0, 0.65f * scale, 0);
        parent.AddChild(core);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.0f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale, RadialSegments = radialSegs, Rings = rings };
        head.MaterialOverride = rockMat;
        head.Scale = new Vector3(1f, 0.8f, 0.9f);
        headNode.AddChild(head);

        // Glowing eyes (hollow sockets) - Height = 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = lavaMat };
        leftEye.Position = new Vector3(-0.06f * scale, 0.02f * scale, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = lavaMat };
        rightEye.Position = new Vector3(0.06f * scale, 0.02f * scale, 0.1f * scale);
        headNode.AddChild(rightEye);

        // Arms
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.28f * scale, 0.75f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.08f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        rightArm.MaterialOverride = rockMat;
        rightArm.Position = new Vector3(0, -0.17f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.28f * scale, 0.75f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.08f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        leftArm.MaterialOverride = rockMat;
        leftArm.Position = new Vector3(0, -0.17f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.12f * scale, 0.35f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.1f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        rightLeg.MaterialOverride = rockMat;
        rightLeg.Position = new Vector3(0, -0.17f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.12f * scale, 0.35f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.1f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        leftLeg.MaterialOverride = rockMat;
        leftLeg.Position = new Vector3(0, -0.17f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Molten drips from arms
        var dripMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.005f * scale, Height = 0.1f * scale, RadialSegments = radialSegs / 2 };
        for (int i = 0; i < 3; i++)
        {
            var drip = new MeshInstance3D { Mesh = dripMesh, MaterialOverride = lavaMat };
            drip.Position = new Vector3((i - 1) * 0.03f * scale, -0.38f * scale, 0);
            rightArmNode.AddChild(drip);
        }

        // Shoulder lava vents
        var ventMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2 };
        var leftVent = new MeshInstance3D { Mesh = ventMesh, MaterialOverride = lavaMat };
        leftVent.Position = new Vector3(-0.32f * scale, 0.82f * scale, 0);
        leftVent.RotationDegrees = new Vector3(0, 0, 25);
        parent.AddChild(leftVent);
        var rightVent = new MeshInstance3D { Mesh = ventMesh, MaterialOverride = lavaMat };
        rightVent.Position = new Vector3(0.32f * scale, 0.82f * scale, 0);
        rightVent.RotationDegrees = new Vector3(0, 0, -25);
        parent.AddChild(rightVent);

        // Rocky fists
        var fistMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.07f * scale, Height = 0.1f * scale, RadialSegments = radialSegs };
        var rightFist = new MeshInstance3D { Mesh = fistMesh, MaterialOverride = rockMat };
        rightFist.Position = new Vector3(0, -0.38f * scale, 0);
        rightArmNode.AddChild(rightFist);
        var leftFist = new MeshInstance3D { Mesh = fistMesh, MaterialOverride = rockMat };
        leftFist.Position = new Vector3(0, -0.38f * scale, 0);
        leftArmNode.AddChild(leftFist);

        // Lava mouth
        var mouthMesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.04f * scale, Height = 0.03f * scale, RadialSegments = radialSegs / 2 };
        var mouth = new MeshInstance3D { Mesh = mouthMesh, MaterialOverride = coreMat };
        mouth.Position = new Vector3(0, -0.08f * scale, 0.12f * scale);
        mouth.RotationDegrees = new Vector3(90, 0, 0);
        headNode.AddChild(mouth);

        // Cooled rock patches on body
        var cooledMat = new StandardMaterial3D { AlbedoColor = rockColor.Lightened(0.2f), Roughness = 0.9f };
        var patchMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.1f * scale, 0.02f * scale) };
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4f + 0.4f;
            var patch = new MeshInstance3D { Mesh = patchMesh, MaterialOverride = cooledMat };
            patch.Position = new Vector3(Mathf.Cos(angle) * 0.24f * scale, 0.5f * scale + i * 0.08f * scale, Mathf.Sin(angle) * 0.24f * scale);
            // Calculate rotation manually (LookAt requires node to be in tree)
            var patchTarget = new Vector3(0, 0.6f * scale, 0);
            var patchDir = (patchTarget - patch.Position).Normalized();
            if (patchDir.LengthSquared() > 0.001f)
            {
                float patchYaw = Mathf.Atan2(patchDir.X, patchDir.Z);
                float patchPitch = -Mathf.Asin(patchDir.Y);
                patch.Rotation = new Vector3(patchPitch + Mathf.Pi / 2f, patchYaw, 0);
            }
            parent.AddChild(patch);
        }
    }

    /// <summary>
    /// Ice Wraith - Frozen floating spirit
    /// Translucent blue wispy figure
    /// </summary>
    private static void CreateIceWraithMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.0f;
        Color iceColor = colorOverride ?? new Color(0.6f, 0.8f, 0.95f); // Ice blue
        Color coreColor = new Color(0.4f, 0.6f, 0.9f);

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var iceMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(iceColor.R, iceColor.G, iceColor.B, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = iceColor * 0.3f,
            Roughness = 0.2f
        };
        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = coreColor,
            EmissionEnabled = true,
            Emission = coreColor,
            EmissionEnergyMultiplier = 2f
        };

        float floatY = 0.3f; // Floating offset

        // Wispy body (tapered cylinder)
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.15f * scale, BottomRadius = 0.05f * scale, Height = 0.6f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = iceMat;
        body.Position = new Vector3(0, 0.6f * scale + floatY, 0);
        parent.AddChild(body);

        // Glowing core - Height = 2*Radius for proper sphere
        var core = new MeshInstance3D();
        core.Mesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale };
        core.MaterialOverride = coreMat;
        core.Position = new Vector3(0, 0.7f * scale + floatY, 0);
        parent.AddChild(core);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.0f * scale + floatY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.24f * scale };
        head.MaterialOverride = iceMat;
        headNode.AddChild(head);

        // Cold eyes
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.95f, 1f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.9f, 1f),
            EmissionEnergyMultiplier = 2f
        };
        var eyeMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = 12, Rings = 8 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.04f * scale, 0, 0.08f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.04f * scale, 0, 0.08f * scale);
        headNode.AddChild(rightEye);

        // Wispy arms
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.18f * scale, 0.85f * scale + floatY, 0);
        rightArmNode.RotationDegrees = new Vector3(0, 0, 30);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.01f * scale, Height = 0.3f * scale };
        rightArm.MaterialOverride = iceMat;
        rightArm.Position = new Vector3(0, -0.15f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.18f * scale, 0.85f * scale + floatY, 0);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -30);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.01f * scale, Height = 0.3f * scale };
        leftArm.MaterialOverride = iceMat;
        leftArm.Position = new Vector3(0, -0.15f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Icicle crown on head
        var icicleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.85f, 1f, 0.8f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.15f
        };
        var icicleMesh = new CylinderMesh { TopRadius = 0.002f * scale, BottomRadius = 0.015f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2 };
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5f;
            var icicle = new MeshInstance3D { Mesh = icicleMesh, MaterialOverride = icicleMat };
            icicle.Position = new Vector3(Mathf.Cos(angle) * 0.08f * scale, 0.1f * scale, Mathf.Sin(angle) * 0.08f * scale);
            icicle.RotationDegrees = new Vector3(Mathf.Cos(angle) * 15, 0, -Mathf.Sin(angle) * 15);
            headNode.AddChild(icicle);
        }

        // Frost particles around body
        var frostMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f, 0.5f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.95f, 1f),
            EmissionEnergyMultiplier = 0.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var frostMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.02f * scale, 0.002f * scale) };
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8f;
            var frost = new MeshInstance3D { Mesh = frostMesh, MaterialOverride = frostMat };
            frost.Position = new Vector3(
                Mathf.Cos(angle) * 0.25f * scale,
                0.6f * scale + floatY + (i % 3) * 0.1f * scale,
                Mathf.Sin(angle) * 0.25f * scale
            );
            frost.RotationDegrees = new Vector3(GD.Randf() * 45, GD.Randf() * 360, GD.Randf() * 45);
            parent.AddChild(frost);
        }

        // Trailing frost wisps below body
        var wispMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.01f * scale, Height = 0.2f * scale, RadialSegments = radialSegs / 2 };
        for (int i = 0; i < 3; i++)
        {
            var wisp = new MeshInstance3D { Mesh = wispMesh, MaterialOverride = iceMat };
            wisp.Position = new Vector3((i - 1) * 0.06f * scale, 0.2f * scale + floatY, 0);
            parent.AddChild(wisp);
        }

        // Ghostly clawed hands
        var clawMesh = new CylinderMesh { TopRadius = 0.003f * scale, BottomRadius = 0.008f * scale, Height = 0.04f * scale, RadialSegments = 4 };
        for (int c = 0; c < 3; c++)
        {
            var rightClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = icicleMat };
            rightClaw.Position = new Vector3((c - 1) * 0.015f * scale, -0.32f * scale, 0.01f * scale);
            rightClaw.RotationDegrees = new Vector3(30, 0, (c - 1) * 10);
            rightArmNode.AddChild(rightClaw);
            var leftClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = icicleMat };
            leftClaw.Position = new Vector3((c - 1) * 0.015f * scale, -0.32f * scale, 0.01f * scale);
            leftClaw.RotationDegrees = new Vector3(30, 0, (c - 1) * 10);
            leftArmNode.AddChild(leftClaw);
        }
    }

    /// <summary>
    /// Gelatinous Cube - Translucent cube that dissolves prey
    /// Large semi-transparent box
    /// </summary>
    private static void CreateGelatinousCubeMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.5f; // Large enemy
        Color gelColor = colorOverride ?? new Color(0.3f, 0.6f, 0.4f); // Green-tinted

        var gelMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(gelColor.R, gelColor.G, gelColor.B, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.1f
        };
        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(gelColor.R * 0.8f, gelColor.G * 0.8f, gelColor.B * 0.8f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        // Main cube body
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh { Size = new Vector3(0.8f * scale, 0.8f * scale, 0.8f * scale) };
        body.MaterialOverride = gelMat;
        body.Position = new Vector3(0, 0.4f * scale, 0);
        parent.AddChild(body);

        // Inner core (denser)
        var core = new MeshInstance3D();
        core.Mesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.5f * scale, 0.5f * scale) };
        core.MaterialOverride = coreMat;
        core.Position = new Vector3(0, 0.4f * scale, 0);
        parent.AddChild(core);

        // Floating debris inside (absorbed items)
        var debrisMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.5f, 0.4f) };
        var debrisMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.05f * scale, 0.06f * scale) };
        for (int i = 0; i < 3; i++)
        {
            var debris = new MeshInstance3D { Mesh = debrisMesh, MaterialOverride = debrisMat };
            debris.Position = new Vector3(
                (GD.Randf() - 0.5f) * 0.4f * scale,
                0.2f * scale + GD.Randf() * 0.4f * scale,
                (GD.Randf() - 0.5f) * 0.4f * scale
            );
            debris.RotationDegrees = new Vector3(GD.Randf() * 360, GD.Randf() * 360, GD.Randf() * 360);
            parent.AddChild(debris);
        }

        // Head node (center of cube for animation)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.5f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Add bones/skull debris inside
        var boneMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.85f, 0.75f), Roughness = 0.7f };
        var skullMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.05f * scale, Height = 0.06f * scale };
        var skull = new MeshInstance3D { Mesh = skullMesh, MaterialOverride = boneMat };
        skull.Position = new Vector3(0.1f * scale, 0.35f * scale, 0.15f * scale);
        skull.RotationDegrees = new Vector3(20, 45, 30);
        parent.AddChild(skull);

        // Ribs
        var ribMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.01f * scale, Height = 0.12f * scale };
        for (int i = 0; i < 3; i++)
        {
            var rib = new MeshInstance3D { Mesh = ribMesh, MaterialOverride = boneMat };
            rib.Position = new Vector3(-0.1f * scale, 0.25f * scale + i * 0.06f * scale, 0.08f * scale);
            rib.RotationDegrees = new Vector3(20, 0, 70);
            parent.AddChild(rib);
        }

        // Dissolved sword
        var swordMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.5f, 0.55f), Metallic = 0.6f, Roughness = 0.5f };
        var swordMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.15f * scale, 0.04f * scale) };
        var sword = new MeshInstance3D { Mesh = swordMesh, MaterialOverride = swordMat };
        sword.Position = new Vector3(-0.15f * scale, 0.5f * scale, -0.1f * scale);
        sword.RotationDegrees = new Vector3(15, 30, 45);
        parent.AddChild(sword);

        // Acid bubbles
        var bubbleMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(gelColor.R + 0.1f, gelColor.G + 0.2f, gelColor.B + 0.1f, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var bubbleMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.03f * scale, Height = 0.03f * scale };
        for (int i = 0; i < 6; i++)
        {
            var bubble = new MeshInstance3D { Mesh = bubbleMesh, MaterialOverride = bubbleMat };
            bubble.Position = new Vector3(
                (GD.Randf() - 0.5f) * 0.6f * scale,
                0.7f * scale + GD.Randf() * 0.1f * scale,
                (GD.Randf() - 0.5f) * 0.6f * scale
            );
            parent.AddChild(bubble);
        }

        // Pseudopods (arms/legs for animation)
        var podMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(gelColor.R, gelColor.G, gelColor.B, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var podMesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.02f * scale, Height = 0.2f * scale };
        var rightPod = new Node3D();
        rightPod.Position = new Vector3(0.35f * scale, 0.3f * scale, 0);
        parent.AddChild(rightPod);
        limbs.RightArm = rightPod;
        var rightPodMesh = new MeshInstance3D { Mesh = podMesh, MaterialOverride = podMat };
        rightPodMesh.Position = new Vector3(0.1f * scale, 0, 0);
        rightPodMesh.RotationDegrees = new Vector3(0, 0, -90);
        rightPod.AddChild(rightPodMesh);

        var leftPod = new Node3D();
        leftPod.Position = new Vector3(-0.35f * scale, 0.3f * scale, 0);
        parent.AddChild(leftPod);
        limbs.LeftArm = leftPod;
        var leftPodMesh = new MeshInstance3D { Mesh = podMesh, MaterialOverride = podMat };
        leftPodMesh.Position = new Vector3(-0.1f * scale, 0, 0);
        leftPodMesh.RotationDegrees = new Vector3(0, 0, 90);
        leftPod.AddChild(leftPodMesh);
    }

    /// <summary>
    /// Void Spawn - Reality-warping creature
    /// Abstract form with tentacles
    /// </summary>
    private static void CreateVoidSpawnMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.0f;
        Color voidColor = colorOverride ?? new Color(0.1f, 0.05f, 0.15f); // Dark purple
        Color starColor = new Color(0.9f, 0.8f, 1f); // Star-like glow

        var voidMat = new StandardMaterial3D
        {
            AlbedoColor = voidColor,
            Roughness = 0.9f,
            EmissionEnabled = true,
            Emission = voidColor * 0.3f
        };
        var starMat = new StandardMaterial3D
        {
            AlbedoColor = starColor,
            EmissionEnabled = true,
            Emission = starColor,
            EmissionEnergyMultiplier = 2f
        };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.3f, 0.8f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.8f),
            EmissionEnergyMultiplier = 3f
        };

        float floatY = 0.5f;

        // Central orb - Height = 2*Radius for proper sphere
        var body = new MeshInstance3D();
        body.Mesh = new SphereMesh { Radius = 0.25f * scale, Height = 0.5f * scale };
        body.MaterialOverride = voidMat;
        body.Position = new Vector3(0, 0.6f * scale + floatY, 0);
        parent.AddChild(body);

        // Star-like spots - Height = 2*Radius for proper sphere
        var starMesh = new SphereMesh { Radius = 0.02f * scale, Height = 0.04f * scale };
        for (int i = 0; i < 8; i++)
        {
            var star = new MeshInstance3D { Mesh = starMesh, MaterialOverride = starMat };
            float angle1 = GD.Randf() * Mathf.Tau;
            float angle2 = GD.Randf() * Mathf.Pi;
            star.Position = new Vector3(
                Mathf.Cos(angle1) * Mathf.Sin(angle2) * 0.22f * scale,
                0.6f * scale + floatY + Mathf.Cos(angle2) * 0.22f * scale,
                Mathf.Sin(angle1) * Mathf.Sin(angle2) * 0.22f * scale
            );
            parent.AddChild(star);
        }

        // Central eye
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.6f * scale + floatY, 0.2f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // CRITICAL: Eye must have Height = 2*Radius for proper sphere rendering
        var eye = new MeshInstance3D();
        eye.Mesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale };
        eye.MaterialOverride = eyeMat;
        headNode.AddChild(eye);

        // Tentacles (4)
        var tentacleMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.01f * scale, Height = 0.4f * scale };
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4f;
            var tentacleNode = new Node3D();
            tentacleNode.Position = new Vector3(
                Mathf.Cos(angle) * 0.2f * scale,
                0.4f * scale + floatY,
                Mathf.Sin(angle) * 0.2f * scale
            );
            tentacleNode.RotationDegrees = new Vector3(Mathf.Cos(angle) * 45, 0, Mathf.Sin(angle) * 45);
            parent.AddChild(tentacleNode);

            var tentacle = new MeshInstance3D { Mesh = tentacleMesh, MaterialOverride = voidMat };
            tentacle.Position = new Vector3(0, -0.2f * scale, 0);
            tentacleNode.AddChild(tentacle);

            // Assign to limb nodes
            if (i == 0) limbs.RightArm = tentacleNode;
            if (i == 1) limbs.LeftArm = tentacleNode;
            if (i == 2) limbs.RightLeg = tentacleNode;
            if (i == 3) limbs.LeftLeg = tentacleNode;

            // Suction cups on tentacles
            var suctionMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.012f * scale, Height = 0.01f * scale };
            for (int s = 0; s < 4; s++)
            {
                var suction = new MeshInstance3D { Mesh = suctionMesh, MaterialOverride = starMat };
                suction.Position = new Vector3(0.02f * scale, -0.08f * scale - s * 0.08f * scale, 0);
                tentacleNode.AddChild(suction);
            }
        }

        // Reality distortion ring
        var ringMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.2f, 0.7f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(0.6f, 0.3f, 0.9f),
            EmissionEnergyMultiplier = 1.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var ringMesh = new CylinderMesh { TopRadius = 0.35f * scale, BottomRadius = 0.35f * scale, Height = 0.02f * scale };
        var ring = new MeshInstance3D { Mesh = ringMesh, MaterialOverride = ringMat };
        ring.Position = new Vector3(0, 0.6f * scale + floatY, 0);
        parent.AddChild(ring);

        // Additional smaller eyes around the main eye
        var smallEyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.4f, 0.9f),
            EmissionEnabled = true,
            Emission = new Color(0.7f, 0.2f, 0.8f),
            EmissionEnergyMultiplier = 2f
        };
        var smallEyeMesh = new SphereMesh { Radius = 0.03f * scale, Height = 0.06f * scale, RadialSegments = 12, Rings = 8 };
        for (int i = 0; i < 4; i++)
        {
            float eyeAngle = i * Mathf.Tau / 4f + 0.4f;
            var smallEye = new MeshInstance3D { Mesh = smallEyeMesh, MaterialOverride = smallEyeMat };
            smallEye.Position = new Vector3(
                Mathf.Cos(eyeAngle) * 0.18f * scale,
                0.7f * scale + floatY,
                0.1f * scale + Mathf.Sin(eyeAngle) * 0.1f * scale
            );
            parent.AddChild(smallEye);
        }

        // Void particles orbiting
        var voidParticleMesh = new BoxMesh { Size = new Vector3(0.015f * scale, 0.015f * scale, 0.015f * scale) };
        for (int i = 0; i < 8; i++)
        {
            float pAngle = i * Mathf.Tau / 8f;
            var voidP = new MeshInstance3D { Mesh = voidParticleMesh, MaterialOverride = starMat };
            voidP.Position = new Vector3(
                Mathf.Cos(pAngle) * 0.4f * scale,
                0.5f * scale + floatY + (i % 3) * 0.15f * scale,
                Mathf.Sin(pAngle) * 0.4f * scale
            );
            voidP.RotationDegrees = new Vector3(45, 45 * i, 0);
            parent.AddChild(voidP);
        }
    }

    /// <summary>
    /// Mimic - Treasure chest monster
    /// Enhanced with detailed features:
    /// - Wood grain texture (darker stripe boxes on chest surface)
    /// - Ornate metal lock plate (detailed center piece with keyhole)
    /// - Corner brackets and hinges (metal details)
    /// - More teeth (16 total, varied sizes)
    /// - Drool/saliva strands (thin translucent cylinders)
    /// - Creepier tongue (longer with bumps/texture)
    /// - Chain attachments on sides
    /// - Glowing eye in the darkness inside
    /// </summary>
    private static void CreateMimicMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.0f;
        Color woodColor = colorOverride ?? new Color(0.45f, 0.3f, 0.2f); // Wood
        Color darkWoodColor = woodColor.Darkened(0.25f); // Wood grain
        Color metalColor = new Color(0.6f, 0.55f, 0.3f); // Gold trim
        Color darkMetalColor = metalColor.Darkened(0.3f); // Aged metal
        Color tongueColor = new Color(0.75f, 0.25f, 0.35f); // Red tongue
        Color tongueBumpColor = new Color(0.85f, 0.35f, 0.45f); // Lighter tongue bumps

        // === MATERIALS ===
        var woodMat = new StandardMaterial3D { AlbedoColor = woodColor, Roughness = 0.8f };
        var darkWoodMat = new StandardMaterial3D { AlbedoColor = darkWoodColor, Roughness = 0.85f };
        var metalMat = new StandardMaterial3D { AlbedoColor = metalColor, Metallic = 0.7f, Roughness = 0.4f };
        var darkMetalMat = new StandardMaterial3D { AlbedoColor = darkMetalColor, Metallic = 0.6f, Roughness = 0.5f };
        var tongueMat = new StandardMaterial3D { AlbedoColor = tongueColor, Roughness = 0.5f };
        var tongueBumpMat = new StandardMaterial3D { AlbedoColor = tongueBumpColor, Roughness = 0.4f };
        var toothMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.9f, 0.85f), Roughness = 0.5f };
        var dirtyToothMat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.8f, 0.7f), Roughness = 0.6f };

        // Glowing eye material (strong emission for creepy effect)
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.6f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.5f, 0.05f),
            EmissionEnergyMultiplier = 1.5f
        };

        // Inner darkness material
        var darknessMat = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.03f, 0.02f), Roughness = 1f };

        // Chain material
        var chainMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.38f, 0.35f), Metallic = 0.5f, Roughness = 0.6f };

        // === CHEST BODY (bottom half) ===
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.25f * scale, 0.35f * scale) };
        body.MaterialOverride = woodMat;
        body.Position = new Vector3(0, 0.125f * scale, 0);
        parent.AddChild(body);

        // === WOOD GRAIN TEXTURE (darker stripe boxes on chest surface) ===
        var grainMesh = new BoxMesh { Size = new Vector3(0.48f * scale, 0.008f * scale, 0.01f * scale) };
        for (int i = 0; i < 6; i++)
        {
            // Front face grain lines
            var grainFront = new MeshInstance3D { Mesh = grainMesh, MaterialOverride = darkWoodMat };
            grainFront.Position = new Vector3(0, 0.04f * scale + i * 0.035f * scale, 0.176f * scale);
            parent.AddChild(grainFront);

            // Back face grain lines
            var grainBack = new MeshInstance3D { Mesh = grainMesh, MaterialOverride = darkWoodMat };
            grainBack.Position = new Vector3(0, 0.04f * scale + i * 0.035f * scale, -0.176f * scale);
            parent.AddChild(grainBack);
        }

        // Side grain (vertical lines)
        var sideGrainMesh = new BoxMesh { Size = new Vector3(0.008f * scale, 0.23f * scale, 0.01f * scale) };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                var sideGrain = new MeshInstance3D { Mesh = sideGrainMesh, MaterialOverride = darkWoodMat };
                sideGrain.Position = new Vector3(side * 0.251f * scale, 0.125f * scale, -0.1f * scale + i * 0.08f * scale);
                parent.AddChild(sideGrain);
            }
        }

        // === METAL BANDS (enhanced) ===
        var bandMesh = new BoxMesh { Size = new Vector3(0.52f * scale, 0.035f * scale, 0.37f * scale) };
        var band1 = new MeshInstance3D { Mesh = bandMesh, MaterialOverride = metalMat };
        band1.Position = new Vector3(0, 0.06f * scale, 0);
        parent.AddChild(band1);
        var band2 = new MeshInstance3D { Mesh = bandMesh, MaterialOverride = metalMat };
        band2.Position = new Vector3(0, 0.22f * scale, 0);
        parent.AddChild(band2);

        // Band rivets (small cylinders on bands)
        float rivetRadius = 0.012f * scale;
        var rivetMesh = new CylinderMesh { TopRadius = rivetRadius, BottomRadius = rivetRadius, Height = 0.008f * scale };
        for (int band = 0; band < 2; band++)
        {
            float bandY = band == 0 ? 0.06f : 0.22f;
            for (int i = 0; i < 6; i++)
            {
                float rivetX = -0.2f * scale + i * 0.08f * scale;
                // Front rivets
                var rivetFront = new MeshInstance3D { Mesh = rivetMesh, MaterialOverride = darkMetalMat };
                rivetFront.Position = new Vector3(rivetX, bandY * scale, 0.186f * scale);
                rivetFront.RotationDegrees = new Vector3(90, 0, 0);
                parent.AddChild(rivetFront);
                // Back rivets
                var rivetBack = new MeshInstance3D { Mesh = rivetMesh, MaterialOverride = darkMetalMat };
                rivetBack.Position = new Vector3(rivetX, bandY * scale, -0.186f * scale);
                rivetBack.RotationDegrees = new Vector3(90, 0, 0);
                parent.AddChild(rivetBack);
            }
        }

        // === CORNER BRACKETS (L-shaped metal pieces at corners) ===
        var bracketMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.08f * scale, 0.015f * scale) };
        var bracketTopMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.015f * scale, 0.04f * scale) };
        for (int xSide = -1; xSide <= 1; xSide += 2)
        {
            for (int zSide = -1; zSide <= 1; zSide += 2)
            {
                // Vertical part of bracket
                var bracketVert = new MeshInstance3D { Mesh = bracketMesh, MaterialOverride = darkMetalMat };
                bracketVert.Position = new Vector3(xSide * 0.23f * scale, 0.04f * scale, zSide * 0.16f * scale);
                parent.AddChild(bracketVert);

                // Horizontal part of bracket (top)
                var bracketHoriz = new MeshInstance3D { Mesh = bracketTopMesh, MaterialOverride = darkMetalMat };
                bracketHoriz.Position = new Vector3(xSide * 0.23f * scale, 0.085f * scale, zSide * 0.155f * scale);
                parent.AddChild(bracketHoriz);
            }
        }

        // === HINGES (at back of chest) ===
        var hingeMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.06f * scale };
        var hingeBaseMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.04f * scale, 0.02f * scale) };
        for (int i = -1; i <= 1; i += 2)
        {
            // Hinge barrel
            var hinge = new MeshInstance3D { Mesh = hingeMesh, MaterialOverride = darkMetalMat };
            hinge.Position = new Vector3(i * 0.15f * scale, 0.25f * scale, -0.175f * scale);
            hinge.RotationDegrees = new Vector3(0, 0, 90);
            parent.AddChild(hinge);

            // Hinge base plate (bottom)
            var hingeBase = new MeshInstance3D { Mesh = hingeBaseMesh, MaterialOverride = metalMat };
            hingeBase.Position = new Vector3(i * 0.15f * scale, 0.23f * scale, -0.165f * scale);
            parent.AddChild(hingeBase);
        }

        // === LID (head node - opens/closes) ===
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.25f * scale, -0.15f * scale); // Pivot at back
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var lid = new MeshInstance3D();
        lid.Mesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.15f * scale, 0.35f * scale) };
        lid.MaterialOverride = woodMat;
        lid.Position = new Vector3(0, 0.075f * scale, 0.15f * scale);
        headNode.AddChild(lid);

        // Lid wood grain
        for (int i = 0; i < 4; i++)
        {
            var lidGrain = new MeshInstance3D { Mesh = grainMesh, MaterialOverride = darkWoodMat };
            lidGrain.Position = new Vector3(0, 0.02f * scale + i * 0.035f * scale, 0.326f * scale);
            headNode.AddChild(lidGrain);
        }

        // Lid metal band
        var lidBandMesh = new BoxMesh { Size = new Vector3(0.52f * scale, 0.025f * scale, 0.37f * scale) };
        var lidBand = new MeshInstance3D { Mesh = lidBandMesh, MaterialOverride = metalMat };
        lidBand.Position = new Vector3(0, 0.14f * scale, 0.15f * scale);
        headNode.AddChild(lidBand);

        // === ORNATE LOCK PLATE (detailed center piece) ===
        // Main lock plate (larger, decorative)
        var lockPlateMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.12f * scale, 0.025f * scale) };
        var lockPlate = new MeshInstance3D { Mesh = lockPlateMesh, MaterialOverride = metalMat };
        lockPlate.Position = new Vector3(0, 0.12f * scale, 0.19f * scale);
        parent.AddChild(lockPlate);

        // Lock plate border (raised edge)
        var lockBorderMesh = new BoxMesh { Size = new Vector3(0.11f * scale, 0.005f * scale, 0.03f * scale) };
        var lockBorderTop = new MeshInstance3D { Mesh = lockBorderMesh, MaterialOverride = darkMetalMat };
        lockBorderTop.Position = new Vector3(0, 0.175f * scale, 0.19f * scale);
        parent.AddChild(lockBorderTop);
        var lockBorderBottom = new MeshInstance3D { Mesh = lockBorderMesh, MaterialOverride = darkMetalMat };
        lockBorderBottom.Position = new Vector3(0, 0.065f * scale, 0.19f * scale);
        parent.AddChild(lockBorderBottom);

        // Keyhole (dark recess)
        var keyholeMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.04f * scale, 0.01f * scale) };
        var keyhole = new MeshInstance3D { Mesh = keyholeMesh, MaterialOverride = darknessMat };
        keyhole.Position = new Vector3(0, 0.1f * scale, 0.205f * scale);
        parent.AddChild(keyhole);

        // Keyhole circle (top of keyhole)
        float keyholeCircleRadius = 0.015f * scale;
        var keyholeCircleMesh = new CylinderMesh { TopRadius = keyholeCircleRadius, BottomRadius = keyholeCircleRadius, Height = 0.01f * scale };
        var keyholeCircle = new MeshInstance3D { Mesh = keyholeCircleMesh, MaterialOverride = darknessMat };
        keyholeCircle.Position = new Vector3(0, 0.125f * scale, 0.205f * scale);
        keyholeCircle.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(keyholeCircle);

        // Lock plate decorative studs
        float studRadius = 0.008f * scale;
        var studMesh = new SphereMesh { Radius = studRadius, Height = studRadius * 2f, RadialSegments = 8, Rings = 6 };
        float[] studXPositions = { -0.035f, 0.035f };
        float[] studYPositions = { 0.08f, 0.16f };
        foreach (float sx in studXPositions)
        {
            foreach (float sy in studYPositions)
            {
                var stud = new MeshInstance3D { Mesh = studMesh, MaterialOverride = darkMetalMat };
                stud.Position = new Vector3(sx * scale, sy * scale, 0.2f * scale);
                parent.AddChild(stud);
            }
        }

        // === TEETH (16 total, varied sizes - 8 top, 8 bottom) ===
        // Large teeth
        var largeToothMesh = new CylinderMesh { TopRadius = 0.008f * scale, BottomRadius = 0.018f * scale, Height = 0.065f * scale };
        // Medium teeth
        var medToothMesh = new CylinderMesh { TopRadius = 0.006f * scale, BottomRadius = 0.014f * scale, Height = 0.05f * scale };
        // Small teeth
        var smallToothMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.01f * scale, Height = 0.035f * scale };

        for (int i = 0; i < 8; i++)
        {
            float toothX = -0.21f * scale + i * 0.06f * scale;
            bool isLarge = (i == 2 || i == 5); // Fangs
            bool isSmall = (i == 0 || i == 7); // Edge teeth

            CylinderMesh currentToothMesh = isLarge ? largeToothMesh : (isSmall ? smallToothMesh : medToothMesh);
            StandardMaterial3D currentToothMat = (i % 3 == 0) ? dirtyToothMat : toothMat;

            // Top teeth (on lid) - pointing down
            var topTooth = new MeshInstance3D { Mesh = currentToothMesh, MaterialOverride = currentToothMat };
            float topToothZ = isLarge ? 0.325f * scale : 0.32f * scale;
            topTooth.Position = new Vector3(toothX, -0.01f * scale, topToothZ);
            topTooth.RotationDegrees = new Vector3(180 + (GD.Randf() * 10 - 5), 0, GD.Randf() * 6 - 3);
            headNode.AddChild(topTooth);

            // Bottom teeth - pointing up
            var bottomTooth = new MeshInstance3D { Mesh = currentToothMesh, MaterialOverride = currentToothMat };
            bottomTooth.Position = new Vector3(toothX, 0.25f * scale, 0.16f * scale);
            bottomTooth.RotationDegrees = new Vector3(GD.Randf() * 8 - 4, 0, GD.Randf() * 6 - 3);
            parent.AddChild(bottomTooth);
        }

        // === CREEPIER TONGUE (longer with bumps/texture) ===
        // Main tongue base (thicker at back)
        var tongueBase = new MeshInstance3D();
        tongueBase.Mesh = new BoxMesh { Size = new Vector3(0.14f * scale, 0.04f * scale, 0.12f * scale) };
        tongueBase.MaterialOverride = tongueMat;
        tongueBase.Position = new Vector3(0, 0.14f * scale, 0.12f * scale);
        parent.AddChild(tongueBase);

        // Tongue middle section
        var tongueMid = new MeshInstance3D();
        tongueMid.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.035f * scale, 0.14f * scale) };
        tongueMid.MaterialOverride = tongueMat;
        tongueMid.Position = new Vector3(0, 0.135f * scale, 0.24f * scale);
        parent.AddChild(tongueMid);

        // Tongue tip (tapered, extends out of mouth)
        var tongueTip = new MeshInstance3D();
        tongueTip.Mesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.05f * scale, Height = 0.15f * scale };
        tongueTip.MaterialOverride = tongueMat;
        tongueTip.Position = new Vector3(0, 0.13f * scale, 0.38f * scale);
        tongueTip.RotationDegrees = new Vector3(85, 0, 0);
        parent.AddChild(tongueTip);

        // Tongue bumps/papillae (small spheres for texture)
        float bumpRadius = 0.012f * scale;
        var bumpMesh = new SphereMesh { Radius = bumpRadius, Height = bumpRadius * 2f, RadialSegments = 6, Rings = 4 };
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                var bump = new MeshInstance3D { Mesh = bumpMesh, MaterialOverride = tongueBumpMat };
                bump.Position = new Vector3(
                    (col - 1.5f) * 0.03f * scale,
                    0.16f * scale,
                    0.15f * scale + row * 0.06f * scale
                );
                bump.Scale = new Vector3(1f, 0.6f, 1f);
                parent.AddChild(bump);
            }
        }

        // Tongue center groove
        var grooveMesh = new BoxMesh { Size = new Vector3(0.015f * scale, 0.008f * scale, 0.25f * scale) };
        var grooveMat = new StandardMaterial3D { AlbedoColor = tongueColor.Darkened(0.2f), Roughness = 0.6f };
        var groove = new MeshInstance3D { Mesh = grooveMesh, MaterialOverride = grooveMat };
        groove.Position = new Vector3(0, 0.165f * scale, 0.24f * scale);
        parent.AddChild(groove);

        // === GLOWING EYE IN THE DARKNESS INSIDE ===
        // Dark interior void
        var interiorMesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.12f * scale, 0.2f * scale) };
        var interior = new MeshInstance3D { Mesh = interiorMesh, MaterialOverride = darknessMat };
        interior.Position = new Vector3(0, 0.18f * scale, -0.02f * scale);
        parent.AddChild(interior);

        // Single creepy eye deep inside - Height = 2*Radius
        float innerEyeRadius = 0.055f * scale;
        var innerEyeMesh = new SphereMesh { Radius = innerEyeRadius, Height = innerEyeRadius * 2f, RadialSegments = 16, Rings = 12 };
        var innerEye = new MeshInstance3D { Mesh = innerEyeMesh, MaterialOverride = eyeMat };
        innerEye.Position = new Vector3(0, 0.18f * scale, 0.02f * scale);
        parent.AddChild(innerEye);

        // Eye pupil (dark slit)
        var pupilMesh = new BoxMesh { Size = new Vector3(0.015f * scale, 0.06f * scale, 0.02f * scale) };
        var pupilMat = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.01f, 0.01f) };
        var pupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        pupil.Position = new Vector3(0, 0.18f * scale, 0.075f * scale);
        parent.AddChild(pupil);

        // Eye highlight
        float highlightRadius = 0.012f * scale;
        var eyeHighlightMesh = new SphereMesh { Radius = highlightRadius, Height = highlightRadius * 2f, RadialSegments = 6, Rings = 4 };
        var highlightMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.95f, 0.9f) };
        var eyeHighlight = new MeshInstance3D { Mesh = eyeHighlightMesh, MaterialOverride = highlightMat };
        eyeHighlight.Position = new Vector3(-0.02f * scale, 0.2f * scale, 0.07f * scale);
        parent.AddChild(eyeHighlight);

        // === CHAIN ATTACHMENTS ON SIDES ===
        float chainLinkRadius = 0.015f * scale;
        var chainLinkMesh = new CylinderMesh { TopRadius = chainLinkRadius, BottomRadius = chainLinkRadius, Height = 0.025f * scale };
        var chainConnectorMesh = new CylinderMesh { TopRadius = 0.008f * scale, BottomRadius = 0.008f * scale, Height = 0.035f * scale };

        for (int side = -1; side <= 1; side += 2)
        {
            // Chain mount plate
            var chainMountMesh = new BoxMesh { Size = new Vector3(0.015f * scale, 0.06f * scale, 0.06f * scale) };
            var chainMount = new MeshInstance3D { Mesh = chainMountMesh, MaterialOverride = darkMetalMat };
            chainMount.Position = new Vector3(side * 0.258f * scale, 0.14f * scale, 0);
            parent.AddChild(chainMount);

            // Chain ring (attached to mount)
            var chainRing = new MeshInstance3D { Mesh = chainLinkMesh, MaterialOverride = chainMat };
            chainRing.Position = new Vector3(side * 0.275f * scale, 0.14f * scale, 0);
            chainRing.RotationDegrees = new Vector3(0, 0, 90);
            parent.AddChild(chainRing);

            // Hanging chain links (3 links dangling)
            for (int link = 0; link < 3; link++)
            {
                var chainLink = new MeshInstance3D { Mesh = chainLinkMesh, MaterialOverride = chainMat };
                float linkY = 0.11f - link * 0.035f;
                float linkAngle = link % 2 == 0 ? 0 : 90;
                chainLink.Position = new Vector3(side * 0.285f * scale, linkY * scale, 0);
                chainLink.RotationDegrees = new Vector3(0, linkAngle, 15 * side);
                parent.AddChild(chainLink);

                // Connector between links
                if (link < 2)
                {
                    var connector = new MeshInstance3D { Mesh = chainConnectorMesh, MaterialOverride = chainMat };
                    connector.Position = new Vector3(side * 0.285f * scale, (linkY - 0.018f) * scale, 0);
                    parent.AddChild(connector);
                }
            }
        }

        // === CLAWED FEET/LEGS (4 total with improved detail) ===
        var legMat = new StandardMaterial3D { AlbedoColor = woodColor.Darkened(0.25f), Roughness = 0.7f };
        var legMesh = new CylinderMesh { TopRadius = 0.028f * scale, BottomRadius = 0.022f * scale, Height = 0.1f * scale };
        var clawMesh = new CylinderMesh { TopRadius = 0.004f * scale, BottomRadius = 0.014f * scale, Height = 0.045f * scale };
        float legJointRadius = 0.018f * scale;
        var legJointMesh = new SphereMesh { Radius = legJointRadius, Height = legJointRadius * 2f, RadialSegments = 8, Rings = 6 };

        var frontRightLeg = new Node3D();
        frontRightLeg.Position = new Vector3(0.2f * scale, 0.05f * scale, 0.12f * scale);
        parent.AddChild(frontRightLeg);
        limbs.RightArm = frontRightLeg;
        var frJoint = new MeshInstance3D { Mesh = legJointMesh, MaterialOverride = legMat };
        frontRightLeg.AddChild(frJoint);
        var frLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        frLeg.Position = new Vector3(0, -0.05f * scale, 0);
        frontRightLeg.AddChild(frLeg);
        for (int c = 0; c < 3; c++)
        {
            var frClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = toothMat };
            frClaw.Position = new Vector3((c - 1) * 0.015f * scale, -0.12f * scale, 0.02f * scale);
            frClaw.RotationDegrees = new Vector3(30, 0, (c - 1) * 15);
            frontRightLeg.AddChild(frClaw);
        }

        var frontLeftLeg = new Node3D();
        frontLeftLeg.Position = new Vector3(-0.2f * scale, 0.05f * scale, 0.12f * scale);
        parent.AddChild(frontLeftLeg);
        limbs.LeftArm = frontLeftLeg;
        var flJoint = new MeshInstance3D { Mesh = legJointMesh, MaterialOverride = legMat };
        frontLeftLeg.AddChild(flJoint);
        var flLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        flLeg.Position = new Vector3(0, -0.05f * scale, 0);
        frontLeftLeg.AddChild(flLeg);
        for (int c = 0; c < 3; c++)
        {
            var flClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = toothMat };
            flClaw.Position = new Vector3((c - 1) * 0.015f * scale, -0.12f * scale, 0.02f * scale);
            flClaw.RotationDegrees = new Vector3(30, 0, (c - 1) * 15);
            frontLeftLeg.AddChild(flClaw);
        }

        var rearRightLeg = new Node3D();
        rearRightLeg.Position = new Vector3(0.2f * scale, 0.05f * scale, -0.12f * scale);
        parent.AddChild(rearRightLeg);
        limbs.RightLeg = rearRightLeg;
        var rrJoint = new MeshInstance3D { Mesh = legJointMesh, MaterialOverride = legMat };
        rearRightLeg.AddChild(rrJoint);
        var rrLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        rrLeg.Position = new Vector3(0, -0.05f * scale, 0);
        rearRightLeg.AddChild(rrLeg);
        for (int c = 0; c < 3; c++)
        {
            var rrClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = toothMat };
            rrClaw.Position = new Vector3((c - 1) * 0.015f * scale, -0.12f * scale, -0.02f * scale);
            rrClaw.RotationDegrees = new Vector3(-30, 0, (c - 1) * 15);
            rearRightLeg.AddChild(rrClaw);
        }

        var rearLeftLeg = new Node3D();
        rearLeftLeg.Position = new Vector3(-0.2f * scale, 0.05f * scale, -0.12f * scale);
        parent.AddChild(rearLeftLeg);
        limbs.LeftLeg = rearLeftLeg;
        var rlJoint = new MeshInstance3D { Mesh = legJointMesh, MaterialOverride = legMat };
        rearLeftLeg.AddChild(rlJoint);
        var rlLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        rlLeg.Position = new Vector3(0, -0.05f * scale, 0);
        rearLeftLeg.AddChild(rlLeg);
        for (int c = 0; c < 3; c++)
        {
            var rlClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = toothMat };
            rlClaw.Position = new Vector3((c - 1) * 0.015f * scale, -0.12f * scale, -0.02f * scale);
            rlClaw.RotationDegrees = new Vector3(-30, 0, (c - 1) * 15);
            rearLeftLeg.AddChild(rlClaw);
        }

        // === DROOL/SALIVA STRANDS (enhanced - more varied) ===
        var droolMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.88f, 0.82f, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.2f
        };

        // Varying drool strands
        for (int i = 0; i < 5; i++)
        {
            float droolLength = (0.06f + GD.Randf() * 0.08f) * scale;
            float droolTopRadius = (0.004f + GD.Randf() * 0.006f) * scale;
            var droolMesh = new CylinderMesh { TopRadius = droolTopRadius, BottomRadius = 0.002f * scale, Height = droolLength };
            var drool = new MeshInstance3D { Mesh = droolMesh, MaterialOverride = droolMat };
            drool.Position = new Vector3(
                (i - 2) * 0.06f * scale + (GD.Randf() * 0.02f - 0.01f) * scale,
                0.22f * scale - droolLength * 0.5f,
                0.17f * scale
            );
            drool.RotationDegrees = new Vector3(GD.Randf() * 10 - 5, 0, GD.Randf() * 8 - 4);
            parent.AddChild(drool);

            // Drool droplet at end (small sphere)
            if (i % 2 == 0)
            {
                float dropRadius = 0.008f * scale;
                var dropMesh = new SphereMesh { Radius = dropRadius, Height = dropRadius * 2f, RadialSegments = 6, Rings = 4 };
                var drop = new MeshInstance3D { Mesh = dropMesh, MaterialOverride = droolMat };
                drop.Position = new Vector3(
                    (i - 2) * 0.06f * scale,
                    0.22f * scale - droolLength,
                    0.17f * scale
                );
                parent.AddChild(drop);
            }
        }

        // === GOLD COINS INSIDE (lure - enhanced) ===
        var coinMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.3f), Metallic = 0.9f, Roughness = 0.3f };
        var coinMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.005f * scale };
        var largeCoinMesh = new CylinderMesh { TopRadius = 0.025f * scale, BottomRadius = 0.025f * scale, Height = 0.006f * scale };
        for (int i = 0; i < 7; i++)
        {
            var coin = new MeshInstance3D { Mesh = i < 2 ? largeCoinMesh : coinMesh, MaterialOverride = coinMat };
            coin.Position = new Vector3(
                (GD.Randf() - 0.5f) * 0.22f * scale,
                0.06f * scale + GD.Randf() * 0.04f * scale,
                (GD.Randf() - 0.5f) * 0.18f * scale
            );
            coin.RotationDegrees = new Vector3(GD.Randf() * 40, GD.Randf() * 360, GD.Randf() * 40);
            parent.AddChild(coin);
        }

        // Gems mixed with coins
        float gemRadius = 0.012f * scale;
        var gemMesh = new SphereMesh { Radius = gemRadius, Height = gemRadius * 2f, RadialSegments = 8, Rings = 6 };
        Color[] gemColors = { new Color(0.9f, 0.1f, 0.1f), new Color(0.1f, 0.8f, 0.2f), new Color(0.2f, 0.3f, 0.9f) };
        for (int i = 0; i < 3; i++)
        {
            var gemMat = new StandardMaterial3D
            {
                AlbedoColor = gemColors[i],
                Metallic = 0.3f,
                Roughness = 0.1f,
                EmissionEnabled = true,
                Emission = gemColors[i] * 0.3f
            };
            var gem = new MeshInstance3D { Mesh = gemMesh, MaterialOverride = gemMat };
            gem.Position = new Vector3(
                (GD.Randf() - 0.5f) * 0.18f * scale,
                0.07f * scale,
                (GD.Randf() - 0.5f) * 0.14f * scale
            );
            parent.AddChild(gem);
        }
    }

    /// <summary>
    /// Dungeon Rat - Small pack creature
    /// Enhanced rat-like quadruped with detailed features:
    /// - Whiskers (4 per side with varied angles)
    /// - Pink nose (prominent sphere at snout tip)
    /// - Detailed cupped ears (larger hemispheres with pink inner)
    /// - Visible front teeth (2 protruding incisors)
    /// - Claws on feet (4 per foot)
    /// - Segmented tail (8-segment chain)
    /// - Hunched posture (forward-tilted body)
    /// - Beady red eyes with emission
    /// </summary>
    private static void CreateDungeonRatMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 0.5f; // Small enemy
        Color furColor = colorOverride ?? new Color(0.35f, 0.3f, 0.28f); // Gray-brown
        Color skinColor = new Color(0.75f, 0.55f, 0.55f); // Pink skin for nose/ears/tail

        // === MATERIALS ===
        var furMat = new StandardMaterial3D { AlbedoColor = furColor, Roughness = 0.85f };
        var darkFurMat = new StandardMaterial3D { AlbedoColor = furColor.Darkened(0.15f), Roughness = 0.9f };
        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.7f };

        // Pink nose material with slight sheen
        var pinkNoseMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.5f, 0.55f),
            Roughness = 0.4f,
            Metallic = 0.1f
        };

        // Beady red eyes with strong emission
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.02f, 0.02f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.1f, 0.1f),
            EmissionEnergyMultiplier = 0.8f
        };

        // White/cream tooth material
        var toothMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.92f, 0.85f), Roughness = 0.4f };

        // Dark claw material
        var clawMat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.12f, 0.1f), Roughness = 0.6f };

        // Whisker material (translucent white)
        var whiskerMat = new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.9f, 0.88f), Roughness = 0.3f };

        // === HUNCHED BODY (angled forward for rodent posture) ===
        // Main body - elongated cylinder tilted forward for hunched look
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.09f * scale, BottomRadius = 0.13f * scale, Height = 0.32f * scale };
        body.MaterialOverride = furMat;
        body.Position = new Vector3(0, 0.13f * scale, -0.02f * scale);
        body.RotationDegrees = new Vector3(75, 0, 0); // Tilted forward for hunched posture
        parent.AddChild(body);

        // Belly bulge (overlapping sphere for rounder body)
        var belly = new MeshInstance3D();
        float bellyRadius = 0.11f * scale;
        belly.Mesh = new SphereMesh { Radius = bellyRadius, Height = bellyRadius * 2f, RadialSegments = 16, Rings = 10 };
        belly.MaterialOverride = furMat;
        belly.Position = new Vector3(0, 0.1f * scale, 0.02f * scale);
        belly.Scale = new Vector3(1f, 0.85f, 1.1f);
        parent.AddChild(belly);

        // Rear haunch (adds bulk to back end)
        var haunch = new MeshInstance3D();
        float haunchRadius = 0.1f * scale;
        haunch.Mesh = new SphereMesh { Radius = haunchRadius, Height = haunchRadius * 2f, RadialSegments = 14, Rings = 8 };
        haunch.MaterialOverride = furMat;
        haunch.Position = new Vector3(0, 0.11f * scale, -0.12f * scale);
        haunch.Scale = new Vector3(1.1f, 0.9f, 1f);
        parent.AddChild(haunch);

        // === HEAD NODE ===
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.14f * scale, 0.22f * scale);
        headNode.RotationDegrees = new Vector3(-8, 0, 0); // Slight downward tilt
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Main head - tapered cylinder for rodent shape
        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh { TopRadius = 0.035f * scale, BottomRadius = 0.075f * scale, Height = 0.13f * scale };
        head.MaterialOverride = furMat;
        head.RotationDegrees = new Vector3(90, 0, 0);
        headNode.AddChild(head);

        // Head top (sphere for rounder cranium)
        var headTop = new MeshInstance3D();
        float headTopRadius = 0.055f * scale;
        headTop.Mesh = new SphereMesh { Radius = headTopRadius, Height = headTopRadius * 2f, RadialSegments = 14, Rings = 8 };
        headTop.MaterialOverride = furMat;
        headTop.Position = new Vector3(0, 0.015f * scale, -0.035f * scale);
        headTop.Scale = new Vector3(1.2f, 0.9f, 1f);
        headNode.AddChild(headTop);

        // === SNOUT ===
        var snout = new MeshInstance3D();
        snout.Mesh = new CylinderMesh { TopRadius = 0.018f * scale, BottomRadius = 0.028f * scale, Height = 0.055f * scale };
        snout.MaterialOverride = skinMat;
        snout.Position = new Vector3(0, -0.005f * scale, 0.085f * scale);
        snout.RotationDegrees = new Vector3(85, 0, 0);
        headNode.AddChild(snout);

        // === PINK NOSE (prominent sphere at snout tip) ===
        var nose = new MeshInstance3D();
        float noseRadius = 0.012f * scale;
        nose.Mesh = new SphereMesh { Radius = noseRadius, Height = noseRadius * 2f, RadialSegments = 10, Rings = 6 };
        nose.MaterialOverride = pinkNoseMat;
        nose.Position = new Vector3(0, -0.008f * scale, 0.115f * scale);
        headNode.AddChild(nose);

        // Nose nostrils (tiny dark spheres)
        var nostrilMesh = new SphereMesh { Radius = 0.003f * scale, Height = 0.006f * scale, RadialSegments = 6, Rings = 4 };
        var nostrilMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.1f, 0.1f) };
        for (int side = -1; side <= 1; side += 2)
        {
            var nostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
            nostril.Position = new Vector3(side * 0.006f * scale, -0.005f * scale, 0.122f * scale);
            headNode.AddChild(nostril);
        }

        // === BEADY RED EYES (protruding with emission) ===
        float eyeRadius = 0.018f * scale;
        var eyeMesh = new SphereMesh { Radius = eyeRadius, Height = eyeRadius * 2f, RadialSegments = 12, Rings = 8 };

        // Eye sockets (dark recesses)
        var socketMat = new StandardMaterial3D { AlbedoColor = furColor.Darkened(0.4f) };
        float socketRadius = 0.02f * scale;
        var socketMesh = new SphereMesh { Radius = socketRadius, Height = socketRadius * 2f, RadialSegments = 10, Rings = 6 };

        for (int side = -1; side <= 1; side += 2)
        {
            // Socket
            var socket = new MeshInstance3D { Mesh = socketMesh, MaterialOverride = socketMat };
            socket.Position = new Vector3(side * 0.032f * scale, 0.022f * scale, 0.04f * scale);
            headNode.AddChild(socket);

            // Eye (protruding from socket - Z >= radius * 0.7)
            var eye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
            eye.Position = new Vector3(side * 0.032f * scale, 0.024f * scale, 0.052f * scale);
            headNode.AddChild(eye);

            // Eye highlight (small white spec)
            var highlightMesh = new SphereMesh { Radius = 0.004f * scale, Height = 0.008f * scale, RadialSegments = 6, Rings = 4 };
            var highlightMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 1f, 1f) };
            var highlight = new MeshInstance3D { Mesh = highlightMesh, MaterialOverride = highlightMat };
            highlight.Position = new Vector3(side * 0.028f * scale, 0.028f * scale, 0.066f * scale);
            headNode.AddChild(highlight);
        }

        // === DETAILED CUPPED EARS (larger hemispheres) ===
        float earOuterRadius = 0.035f * scale;
        var earOuterMesh = new SphereMesh { Radius = earOuterRadius, Height = earOuterRadius * 2f, RadialSegments = 12, Rings = 8 };
        float earInnerRadius = 0.025f * scale;
        var earInnerMesh = new SphereMesh { Radius = earInnerRadius, Height = earInnerRadius * 2f, RadialSegments = 10, Rings = 6 };
        var earInnerMat = new StandardMaterial3D { AlbedoColor = skinColor.Lightened(0.1f), Roughness = 0.5f };

        for (int side = -1; side <= 1; side += 2)
        {
            // Ear base (attachment point)
            var earBase = new MeshInstance3D();
            float earBaseRadius = 0.015f * scale;
            earBase.Mesh = new SphereMesh { Radius = earBaseRadius, Height = earBaseRadius * 2f, RadialSegments = 8, Rings = 6 };
            earBase.MaterialOverride = furMat;
            earBase.Position = new Vector3(side * 0.045f * scale, 0.045f * scale, -0.01f * scale);
            headNode.AddChild(earBase);

            // Outer ear (cupped shape - flattened sphere)
            var earOuter = new MeshInstance3D { Mesh = earOuterMesh, MaterialOverride = skinMat };
            earOuter.Position = new Vector3(side * 0.055f * scale, 0.065f * scale, -0.005f * scale);
            earOuter.Scale = new Vector3(0.5f, 1f, 0.8f);
            earOuter.RotationDegrees = new Vector3(0, side * 25, side * 15);
            headNode.AddChild(earOuter);

            // Inner ear (pink depression)
            var earInner = new MeshInstance3D { Mesh = earInnerMesh, MaterialOverride = earInnerMat };
            earInner.Position = new Vector3(side * 0.052f * scale, 0.065f * scale, 0.005f * scale);
            earInner.Scale = new Vector3(0.4f, 0.85f, 0.6f);
            earInner.RotationDegrees = new Vector3(0, side * 25, side * 15);
            headNode.AddChild(earInner);
        }

        // === VISIBLE FRONT TEETH (2 prominent incisors) ===
        var incisorMesh = new BoxMesh { Size = new Vector3(0.008f * scale, 0.018f * scale, 0.006f * scale) };
        for (int side = -1; side <= 1; side += 2)
        {
            var incisor = new MeshInstance3D { Mesh = incisorMesh, MaterialOverride = toothMat };
            incisor.Position = new Vector3(side * 0.006f * scale, -0.025f * scale, 0.075f * scale);
            incisor.RotationDegrees = new Vector3(-10, 0, 0);
            headNode.AddChild(incisor);
        }

        // Additional smaller teeth behind incisors
        var smallToothMesh = new CylinderMesh { TopRadius = 0.002f * scale, BottomRadius = 0.003f * scale, Height = 0.008f * scale };
        for (int i = 0; i < 4; i++)
        {
            var tooth = new MeshInstance3D { Mesh = smallToothMesh, MaterialOverride = toothMat };
            tooth.Position = new Vector3((i - 1.5f) * 0.007f * scale, -0.018f * scale, 0.065f * scale);
            tooth.RotationDegrees = new Vector3(180, 0, 0);
            headNode.AddChild(tooth);
        }

        // === WHISKERS (4 per side, varied angles) ===
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float whiskerLength = (0.05f + i * 0.008f) * scale;
                var whiskerMesh = new CylinderMesh
                {
                    TopRadius = 0.0005f * scale,
                    BottomRadius = 0.0015f * scale,
                    Height = whiskerLength
                };
                var whisker = new MeshInstance3D { Mesh = whiskerMesh, MaterialOverride = whiskerMat };

                // Position whiskers around snout
                float yOffset = -0.008f + (i - 1.5f) * 0.006f;
                whisker.Position = new Vector3(side * 0.018f * scale, yOffset * scale, 0.09f * scale);

                // Angle whiskers outward and slightly back
                float horizontalAngle = side * (55 + i * 12);
                float verticalAngle = (i - 1.5f) * 8;
                whisker.RotationDegrees = new Vector3(verticalAngle, 0, horizontalAngle);
                headNode.AddChild(whisker);
            }
        }

        // === LEGS (4 legs with proper joints) ===
        var legMesh = new CylinderMesh { TopRadius = 0.018f * scale, BottomRadius = 0.014f * scale, Height = 0.09f * scale };
        var footMesh = new CylinderMesh { TopRadius = 0.016f * scale, BottomRadius = 0.012f * scale, Height = 0.03f * scale };
        float jointRadius = 0.012f * scale;
        var jointMesh = new SphereMesh { Radius = jointRadius, Height = jointRadius * 2f, RadialSegments = 8, Rings = 6 };

        float frontLegY = 0.06f * scale;
        float backLegY = 0.08f * scale; // Back legs higher (hunched)

        // Front Right Leg
        var frontRightLegNode = new Node3D();
        frontRightLegNode.Position = new Vector3(0.075f * scale, frontLegY, 0.1f * scale);
        parent.AddChild(frontRightLegNode);
        limbs.RightArm = frontRightLegNode;

        var frJoint = new MeshInstance3D { Mesh = jointMesh, MaterialOverride = furMat };
        frontRightLegNode.AddChild(frJoint);
        var frLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        frLeg.Position = new Vector3(0, -0.045f * scale, 0);
        frontRightLegNode.AddChild(frLeg);
        var frFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = skinMat };
        frFoot.Position = new Vector3(0, -0.1f * scale, 0.01f * scale);
        frFoot.RotationDegrees = new Vector3(20, 0, 0);
        frontRightLegNode.AddChild(frFoot);

        // Front Left Leg
        var frontLeftLegNode = new Node3D();
        frontLeftLegNode.Position = new Vector3(-0.075f * scale, frontLegY, 0.1f * scale);
        parent.AddChild(frontLeftLegNode);
        limbs.LeftArm = frontLeftLegNode;

        var flJoint = new MeshInstance3D { Mesh = jointMesh, MaterialOverride = furMat };
        frontLeftLegNode.AddChild(flJoint);
        var flLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        flLeg.Position = new Vector3(0, -0.045f * scale, 0);
        frontLeftLegNode.AddChild(flLeg);
        var flFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = skinMat };
        flFoot.Position = new Vector3(0, -0.1f * scale, 0.01f * scale);
        flFoot.RotationDegrees = new Vector3(20, 0, 0);
        frontLeftLegNode.AddChild(flFoot);

        // Back Right Leg (larger for hopping)
        var backRightLegNode = new Node3D();
        backRightLegNode.Position = new Vector3(0.08f * scale, backLegY, -0.1f * scale);
        parent.AddChild(backRightLegNode);
        limbs.RightLeg = backRightLegNode;

        var brJoint = new MeshInstance3D { Mesh = jointMesh, MaterialOverride = furMat };
        backRightLegNode.AddChild(brJoint);
        var brLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        brLeg.Position = new Vector3(0, -0.045f * scale, 0);
        brLeg.Scale = new Vector3(1.1f, 1f, 1.1f);
        backRightLegNode.AddChild(brLeg);
        var brFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = skinMat };
        brFoot.Position = new Vector3(0, -0.1f * scale, 0.015f * scale);
        brFoot.Scale = new Vector3(1.2f, 1f, 1.3f);
        brFoot.RotationDegrees = new Vector3(25, 0, 0);
        backRightLegNode.AddChild(brFoot);

        // Back Left Leg
        var backLeftLegNode = new Node3D();
        backLeftLegNode.Position = new Vector3(-0.08f * scale, backLegY, -0.1f * scale);
        parent.AddChild(backLeftLegNode);
        limbs.LeftLeg = backLeftLegNode;

        var blJoint = new MeshInstance3D { Mesh = jointMesh, MaterialOverride = furMat };
        backLeftLegNode.AddChild(blJoint);
        var blLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        blLeg.Position = new Vector3(0, -0.045f * scale, 0);
        blLeg.Scale = new Vector3(1.1f, 1f, 1.1f);
        backLeftLegNode.AddChild(blLeg);
        var blFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = skinMat };
        blFoot.Position = new Vector3(0, -0.1f * scale, 0.015f * scale);
        blFoot.Scale = new Vector3(1.2f, 1f, 1.3f);
        blFoot.RotationDegrees = new Vector3(25, 0, 0);
        backLeftLegNode.AddChild(blFoot);

        // === CLAWS ON FEET (4 claws per foot) ===
        var clawMesh = new CylinderMesh { TopRadius = 0.001f * scale, BottomRadius = 0.004f * scale, Height = 0.018f * scale };
        foreach (var legNode in new[] { frontRightLegNode, frontLeftLegNode, backRightLegNode, backLeftLegNode })
        {
            for (int c = 0; c < 4; c++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                float xOffset = (c - 1.5f) * 0.008f * scale;
                claw.Position = new Vector3(xOffset, -0.115f * scale, 0.018f * scale);
                claw.RotationDegrees = new Vector3(35, 0, (c - 1.5f) * 12);
                legNode.AddChild(claw);
            }
        }

        // === SEGMENTED TAIL (chain of tapered cylinders) ===
        int tailSegments = 8;
        float tailBaseRadius = 0.018f * scale;
        float tailStartY = 0.1f * scale;
        float tailStartZ = -0.16f * scale;
        float tailAngle = 55f; // Base angle

        for (int i = 0; i < tailSegments; i++)
        {
            float t = (float)i / (tailSegments - 1);
            float segRadius = Mathf.Lerp(tailBaseRadius, 0.003f * scale, t);
            float segLength = Mathf.Lerp(0.035f * scale, 0.025f * scale, t);

            var tailSeg = new MeshInstance3D();
            tailSeg.Mesh = new CylinderMesh
            {
                TopRadius = segRadius * 0.75f,
                BottomRadius = segRadius,
                Height = segLength
            };
            tailSeg.MaterialOverride = i < 2 ? darkFurMat : skinMat;

            // Each segment continues the tail with slight curve
            float angleAdjust = 8f; // Gradual curve upward
            float segY = -segLength * 0.5f * Mathf.Cos(Mathf.DegToRad(tailAngle + angleAdjust * i));
            float segZ = -segLength * 0.5f * Mathf.Sin(Mathf.DegToRad(tailAngle + angleAdjust * i));
            tailSeg.Position = new Vector3(0, tailStartY + segY * i * 0.6f, tailStartZ + segZ * i * 0.8f);
            tailSeg.RotationDegrees = new Vector3(tailAngle + angleAdjust * i + Mathf.Sin(i * 0.5f) * 5f, 0, 0);

            parent.AddChild(tailSeg);

            // Joint sphere between segments (15-25% overlap)
            if (i < tailSegments - 1)
            {
                float jointRad = segRadius * 0.6f;
                var tailJoint = new MeshInstance3D();
                tailJoint.Mesh = new SphereMesh { Radius = jointRad, Height = jointRad * 2f, RadialSegments = 6, Rings = 4 };
                tailJoint.MaterialOverride = skinMat;

                // Position joint at end of segment
                float jAngle = Mathf.DegToRad(tailAngle + 8f * i);
                float jY = tailStartY - segLength * i * 0.4f * Mathf.Cos(jAngle);
                float jZ = tailStartZ - segLength * i * 0.7f * Mathf.Sin(jAngle);
                tailJoint.Position = new Vector3(0, jY, jZ);
                parent.AddChild(tailJoint);
            }
        }

        // Tail tip (tiny sphere)
        var tailTip = new MeshInstance3D();
        float tipRadius = 0.003f * scale;
        tailTip.Mesh = new SphereMesh { Radius = tipRadius, Height = tipRadius * 2f, RadialSegments = 6, Rings = 4 };
        tailTip.MaterialOverride = skinMat;
        tailTip.Position = new Vector3(0, tailStartY - 0.08f * scale, tailStartZ - 0.22f * scale);
        parent.AddChild(tailTip);

        // === DIRTY FUR PATCHES ===
        var dirtyFurMat = new StandardMaterial3D { AlbedoColor = furColor.Darkened(0.25f), Roughness = 0.95f };
        var patchMesh = new CylinderMesh { TopRadius = 0.035f * scale, BottomRadius = 0.025f * scale, Height = 0.015f * scale };
        for (int i = 0; i < 4; i++)
        {
            var patch = new MeshInstance3D { Mesh = patchMesh, MaterialOverride = dirtyFurMat };
            float patchAngle = i * 90f;
            patch.Position = new Vector3(
                Mathf.Cos(Mathf.DegToRad(patchAngle)) * 0.08f * scale,
                0.12f * scale,
                Mathf.Sin(Mathf.DegToRad(patchAngle)) * 0.06f * scale
            );
            patch.RotationDegrees = new Vector3(GD.Randf() * 40 - 20, patchAngle, GD.Randf() * 30 - 15);
            parent.AddChild(patch);
        }

        // Matted fur tufts
        var tuftMesh = new CylinderMesh { TopRadius = 0.002f * scale, BottomRadius = 0.008f * scale, Height = 0.02f * scale };
        for (int i = 0; i < 5; i++)
        {
            var tuft = new MeshInstance3D { Mesh = tuftMesh, MaterialOverride = darkFurMat };
            tuft.Position = new Vector3(
                (GD.Randf() - 0.5f) * 0.15f * scale,
                0.15f * scale,
                (GD.Randf() - 0.5f) * 0.2f * scale
            );
            tuft.RotationDegrees = new Vector3(GD.Randf() * 60 - 30, GD.Randf() * 360, GD.Randf() * 40 - 20);
            parent.AddChild(tuft);
        }
    }

    // ========================================================================
    // NEW DCC-THEMED BOSSES - 5 NEW TYPES
    // ========================================================================

    /// <summary>
    /// The Butcher - DCC horror butcher with bloody apron, stitched skin, meat hooks, gore splatter
    /// </summary>
    private static void CreateButcherMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 2.0f; // Boss scale
        Color skinColor = colorOverride ?? new Color(0.5f, 0.25f, 0.2f); // Dark red skin
        Color apronColor = new Color(0.85f, 0.8f, 0.75f); // Dirty white apron
        Color stitchColor = new Color(0.25f, 0.15f, 0.1f);
        Color goreColor = new Color(0.4f, 0.05f, 0.05f);

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.85f };
        var apronMat = new StandardMaterial3D { AlbedoColor = apronColor, Roughness = 0.7f };
        var metalMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.5f, 0.55f), Metallic = 0.8f, Roughness = 0.4f };
        var stitchMat = new StandardMaterial3D { AlbedoColor = stitchColor, Roughness = 0.9f };
        var goreMat = new StandardMaterial3D { AlbedoColor = goreColor, Roughness = 0.6f };
        var freshBloodMat = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.15f, 0.1f), Roughness = 0.5f };

        // Massive body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.35f * scale, BottomRadius = 0.4f * scale, Height = 0.6f * scale };
        body.MaterialOverride = skinMat;
        body.Position = new Vector3(0, 0.6f * scale, 0);
        parent.AddChild(body);

        // Stitched scars across torso
        var stitchMesh = new BoxMesh { Size = new Vector3(0.22f * scale, 0.012f * scale, 0.008f * scale) };
        for (int si = 0; si < 4; si++)
        {
            var stitch = new MeshInstance3D { Mesh = stitchMesh, MaterialOverride = stitchMat };
            stitch.Position = new Vector3(0.08f * scale, 0.45f * scale + si * 0.1f * scale, 0.36f * scale);
            stitch.RotationDegrees = new Vector3(0, 0, (si % 2 == 0 ? 12 : -8));
            parent.AddChild(stitch);
        }

        // Apron with neck strap and side ties
        var apron = new MeshInstance3D();
        apron.Mesh = new BoxMesh { Size = new Vector3(0.55f * scale, 0.6f * scale, 0.04f * scale) };
        apron.MaterialOverride = apronMat;
        apron.Position = new Vector3(0, 0.45f * scale, 0.28f * scale);
        parent.AddChild(apron);

        // Apron neck strap
        var strapMesh = new BoxMesh { Size = new Vector3(0.05f * scale, 0.22f * scale, 0.015f * scale) };
        var apronStrap = new MeshInstance3D { Mesh = strapMesh, MaterialOverride = apronMat };
        apronStrap.Position = new Vector3(0, 0.85f * scale, 0.2f * scale);
        parent.AddChild(apronStrap);

        // Gore splatter on body - Height = 2*Radius
        var goreSplatterMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = 8, Rings = 6 };
        for (int gi = 0; gi < 3; gi++)
        {
            float goreAngle = gi * Mathf.Pi / 2.5f + Mathf.Pi * 0.4f;
            var gore = new MeshInstance3D { Mesh = goreSplatterMesh, MaterialOverride = goreMat };
            gore.Position = new Vector3(Mathf.Cos(goreAngle) * 0.38f * scale, 0.55f * scale + (gi % 2) * 0.12f * scale, Mathf.Sin(goreAngle) * 0.28f * scale);
            gore.Scale = new Vector3(1.4f, 0.5f, 1f);
            parent.AddChild(gore);
        }

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.1f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Head - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.2f * scale, Height = 0.4f * scale };
        head.MaterialOverride = skinMat;
        head.Scale = new Vector3(1f, 0.85f, 0.9f);
        headNode.AddChild(head);

        // Tusks
        var tuskMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.03f * scale, Height = 0.1f * scale };
        var tuskMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.85f, 0.75f) };
        var leftTusk = new MeshInstance3D { Mesh = tuskMesh, MaterialOverride = tuskMat };
        leftTusk.Position = new Vector3(-0.08f * scale, -0.05f * scale, 0.12f * scale);
        leftTusk.RotationDegrees = new Vector3(30, 20, 0);
        headNode.AddChild(leftTusk);
        var rightTusk = new MeshInstance3D { Mesh = tuskMesh, MaterialOverride = tuskMat };
        rightTusk.Position = new Vector3(0.08f * scale, -0.05f * scale, 0.12f * scale);
        rightTusk.RotationDegrees = new Vector3(30, -20, 0);
        headNode.AddChild(rightTusk);

        // Eyes
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.3f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.1f)
        };
        var eyeMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = 12, Rings = 8 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.07f * scale, 0.03f * scale, 0.12f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.07f * scale, 0.03f * scale, 0.12f * scale);
        headNode.AddChild(rightEye);

        // Massive arms
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.45f * scale, 0.8f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale };
        rightArm.MaterialOverride = skinMat;
        rightArm.Position = new Vector3(0, -0.25f * scale, 0);
        rightArmNode.AddChild(rightArm);

        // Cleaver in right hand
        var cleaver = new MeshInstance3D();
        cleaver.Mesh = new BoxMesh { Size = new Vector3(0.3f * scale, 0.5f * scale, 0.03f * scale) };
        cleaver.MaterialOverride = metalMat;
        cleaver.Position = new Vector3(0.1f * scale, -0.6f * scale, 0);
        rightArmNode.AddChild(cleaver);
        limbs.Weapon = rightArmNode;

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.45f * scale, 0.8f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale };
        leftArm.MaterialOverride = skinMat;
        leftArm.Position = new Vector3(0, -0.25f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Thick legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.18f * scale, 0.3f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale };
        rightLeg.MaterialOverride = skinMat;
        rightLeg.Position = new Vector3(0, -0.25f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.18f * scale, 0.3f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale };
        leftLeg.MaterialOverride = skinMat;
        leftLeg.Position = new Vector3(0, -0.25f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Blood stains on apron
        var bloodMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.1f, 0.1f), Roughness = 0.8f };
        var bloodMesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.2f * scale, 0.01f * scale) };
        for (int i = 0; i < 4; i++)
        {
            var blood = new MeshInstance3D { Mesh = bloodMesh, MaterialOverride = bloodMat };
            blood.Position = new Vector3((i - 1.5f) * 0.12f * scale, 0.4f * scale + (i % 2) * 0.15f * scale, 0.27f * scale);
            blood.Scale = new Vector3(0.7f + GD.Randf() * 0.6f, 0.5f + GD.Randf() * 1f, 1f);
            parent.AddChild(blood);
        }

        // Multiple meat hooks on belt with blood drips
        var hookMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.42f), Metallic = 0.9f, Roughness = 0.3f };
        var hookMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.015f * scale, Height = 0.12f * scale };
        var hookTipMesh = new CylinderMesh { TopRadius = 0.003f * scale, BottomRadius = 0.015f * scale, Height = 0.06f * scale };
        for (int hi = 0; hi < 3; hi++)
        {
            var hook = new MeshInstance3D { Mesh = hookMesh, MaterialOverride = hookMat };
            hook.Position = new Vector3(-0.32f * scale - hi * 0.08f * scale, 0.32f * scale, 0.18f * scale);
            hook.RotationDegrees = new Vector3(0, 0, 25 + hi * 5);
            parent.AddChild(hook);
            var hookTip = new MeshInstance3D { Mesh = hookTipMesh, MaterialOverride = hookMat };
            hookTip.Position = new Vector3(-0.35f * scale - hi * 0.08f * scale, 0.24f * scale, 0.18f * scale);
            hookTip.RotationDegrees = new Vector3(50, 0, 55 + hi * 5);
            parent.AddChild(hookTip);
            // Blood drip on hooks
            if (hi % 2 == 0)
            {
                var hookBloodMesh = new CylinderMesh { TopRadius = 0.004f * scale, BottomRadius = 0.012f * scale, Height = 0.03f * scale };
                var hookBlood = new MeshInstance3D { Mesh = hookBloodMesh, MaterialOverride = freshBloodMat };
                hookBlood.Position = new Vector3(-0.36f * scale - hi * 0.08f * scale, 0.2f * scale, 0.18f * scale);
                parent.AddChild(hookBlood);
            }
        }
        // Right side hook with meat chunk - Height = 2*Radius
        var rightHook = new MeshInstance3D { Mesh = hookMesh, MaterialOverride = hookMat };
        rightHook.Position = new Vector3(0.35f * scale, 0.32f * scale, 0.18f * scale);
        rightHook.RotationDegrees = new Vector3(0, 0, -25);
        parent.AddChild(rightHook);
        var meatMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.25f, 0.2f), Roughness = 0.85f };
        var meatChunk = new MeshInstance3D();
        meatChunk.Mesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = 8, Rings = 6 };
        meatChunk.MaterialOverride = meatMat;
        meatChunk.Position = new Vector3(0.38f * scale, 0.22f * scale, 0.18f * scale);
        meatChunk.Scale = new Vector3(1.2f, 0.8f, 0.6f);
        parent.AddChild(meatChunk);

        // Cleaver blade edge highlight
        var edgeMat = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.8f, 0.85f), Metallic = 0.95f, Roughness = 0.15f };
        var edge = new MeshInstance3D();
        edge.Mesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.48f * scale, 0.035f * scale) };
        edge.MaterialOverride = edgeMat;
        edge.Position = new Vector3(0.25f * scale, -0.6f * scale, 0);
        rightArmNode.AddChild(edge);

        // Thick neck
        var neckMesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.15f * scale, Height = 0.1f * scale };
        var neck = new MeshInstance3D { Mesh = neckMesh, MaterialOverride = skinMat };
        neck.Position = new Vector3(0, 0.95f * scale, 0);
        parent.AddChild(neck);

        // Brow ridge
        var browMesh = new BoxMesh { Size = new Vector3(0.25f * scale, 0.04f * scale, 0.08f * scale) };
        var brow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = skinMat };
        brow.Position = new Vector3(0, 0.1f * scale, 0.12f * scale);
        headNode.AddChild(brow);

        // Massive hands
        var handMesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.06f * scale, Height = 0.12f * scale };
        var rightHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = skinMat };
        rightHand.Position = new Vector3(0, -0.55f * scale, 0);
        rightArmNode.AddChild(rightHand);
        var leftHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = skinMat };
        leftHand.Position = new Vector3(0, -0.55f * scale, 0);
        leftArmNode.AddChild(leftHand);

        // Heavy boots
        var bootMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.18f, 0.15f), Roughness = 0.9f };
        var bootMesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.1f * scale, 0.2f * scale) };
        var rightBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = bootMat };
        rightBoot.Position = new Vector3(0, -0.55f * scale, 0.03f * scale);
        rightLegNode.AddChild(rightBoot);
        var leftBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = bootMat };
        leftBoot.Position = new Vector3(0, -0.55f * scale, 0.03f * scale);
        leftLegNode.AddChild(leftBoot);
    }

    /// <summary>
    /// Syndicate Enforcer - DCC suit/uniform enforcer with sunglasses, earpiece, concealed weapons, intimidating stance
    /// </summary>
    private static void CreateSyndicateEnforcerMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 2.2f;
        Color suitColor = colorOverride ?? new Color(0.12f, 0.12f, 0.15f); // Dark suit
        Color shirtColor = new Color(0.9f, 0.9f, 0.92f); // White shirt
        Color tieColor = new Color(0.5f, 0.1f, 0.1f); // Red tie
        Color glassesColor = new Color(0.05f, 0.05f, 0.08f); // Dark sunglasses
        Color lightColor = new Color(0.3f, 0.5f, 0.9f);

        var suitMat = new StandardMaterial3D { AlbedoColor = suitColor, Roughness = 0.6f };
        var shirtMat = new StandardMaterial3D { AlbedoColor = shirtColor, Roughness = 0.5f };
        var tieMat = new StandardMaterial3D { AlbedoColor = tieColor, Roughness = 0.55f };
        var glassesMat = new StandardMaterial3D { AlbedoColor = glassesColor, Metallic = 0.3f, Roughness = 0.15f };
        var metalMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.38f, 0.42f), Metallic = 0.85f, Roughness = 0.3f };
        var darkMat = new StandardMaterial3D { AlbedoColor = suitColor.Darkened(0.3f), Metallic = 0.8f, Roughness = 0.4f };
        var lightMat = new StandardMaterial3D
        {
            AlbedoColor = lightColor,
            EmissionEnabled = true,
            Emission = lightColor,
            EmissionEnergyMultiplier = 2f
        };
        var earPieceMat = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.15f, 0.18f), Roughness = 0.4f };

        // Large angular body
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh { Size = new Vector3(0.6f * scale, 0.7f * scale, 0.4f * scale) };
        body.MaterialOverride = metalMat;
        body.Position = new Vector3(0, 0.7f * scale, 0);
        parent.AddChild(body);

        // Chest lights
        var chestLight = new MeshInstance3D();
        chestLight.Mesh = new BoxMesh { Size = new Vector3(0.3f * scale, 0.1f * scale, 0.02f * scale) };
        chestLight.MaterialOverride = lightMat;
        chestLight.Position = new Vector3(0, 0.75f * scale, 0.21f * scale);
        parent.AddChild(chestLight);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.2f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        head.Mesh = new BoxMesh { Size = new Vector3(0.3f * scale, 0.25f * scale, 0.25f * scale) };
        head.MaterialOverride = metalMat;
        headNode.AddChild(head);

        // Visor
        var visor = new MeshInstance3D();
        visor.Mesh = new BoxMesh { Size = new Vector3(0.25f * scale, 0.06f * scale, 0.02f * scale) };
        visor.MaterialOverride = lightMat;
        visor.Position = new Vector3(0, 0.02f * scale, 0.13f * scale);
        headNode.AddChild(visor);

        // Heavy arms with weapons
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.4f * scale, 0.85f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.5f * scale, 0.15f * scale) };
        rightArm.MaterialOverride = metalMat;
        rightArm.Position = new Vector3(0, -0.25f * scale, 0);
        rightArmNode.AddChild(rightArm);

        // Weapon barrel
        var barrel = new MeshInstance3D();
        barrel.Mesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.06f * scale, Height = 0.3f * scale };
        barrel.MaterialOverride = darkMat;
        barrel.Position = new Vector3(0, -0.5f * scale, 0.1f * scale);
        barrel.RotationDegrees = new Vector3(90, 0, 0);
        rightArmNode.AddChild(barrel);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.4f * scale, 0.85f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.5f * scale, 0.15f * scale) };
        leftArm.MaterialOverride = metalMat;
        leftArm.Position = new Vector3(0, -0.25f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Reverse-joint legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.2f * scale, 0.35f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.5f * scale, 0.12f * scale) };
        rightLeg.MaterialOverride = metalMat;
        rightLeg.Position = new Vector3(0, -0.25f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.2f * scale, 0.35f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.5f * scale, 0.12f * scale) };
        leftLeg.MaterialOverride = metalMat;
        leftLeg.Position = new Vector3(0, -0.25f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Antenna array on head
        var antennaMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.02f * scale, Height = 0.15f * scale };
        for (int i = 0; i < 3; i++)
        {
            var antenna = new MeshInstance3D { Mesh = antennaMesh, MaterialOverride = darkMat };
            antenna.Position = new Vector3((i - 1) * 0.08f * scale, 0.15f * scale, 0);
            antenna.RotationDegrees = new Vector3(-20, 0, (i - 1) * 15);
            headNode.AddChild(antenna);
        }

        // Shoulder armor plates
        var shoulderMesh = new BoxMesh { Size = new Vector3(0.2f * scale, 0.08f * scale, 0.15f * scale) };
        var rightShoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = darkMat };
        rightShoulder.Position = new Vector3(0.45f * scale, 0.95f * scale, 0);
        rightShoulder.RotationDegrees = new Vector3(0, 0, -20);
        parent.AddChild(rightShoulder);
        var leftShoulder = new MeshInstance3D { Mesh = shoulderMesh, MaterialOverride = darkMat };
        leftShoulder.Position = new Vector3(-0.45f * scale, 0.95f * scale, 0);
        leftShoulder.RotationDegrees = new Vector3(0, 0, 20);
        parent.AddChild(leftShoulder);

        // Status lights on body
        var statusMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.01f * scale };
        for (int i = 0; i < 4; i++)
        {
            var status = new MeshInstance3D { Mesh = statusMesh, MaterialOverride = lightMat };
            status.Position = new Vector3((i - 1.5f) * 0.1f * scale, 0.85f * scale, 0.21f * scale);
            parent.AddChild(status);
        }

        // Hydraulic pistons on legs
        var pistonMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.015f * scale, Height = 0.3f * scale };
        var rightPiston = new MeshInstance3D { Mesh = pistonMesh, MaterialOverride = darkMat };
        rightPiston.Position = new Vector3(0.06f * scale, -0.15f * scale, 0.08f * scale);
        rightLegNode.AddChild(rightPiston);
        var leftPiston = new MeshInstance3D { Mesh = pistonMesh, MaterialOverride = darkMat };
        leftPiston.Position = new Vector3(-0.06f * scale, -0.15f * scale, 0.08f * scale);
        leftLegNode.AddChild(leftPiston);

        // Feet with stabilizers
        var footMesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.05f * scale, 0.25f * scale) };
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = metalMat };
        rightFoot.Position = new Vector3(0, -0.52f * scale, 0.05f * scale);
        rightLegNode.AddChild(rightFoot);
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = metalMat };
        leftFoot.Position = new Vector3(0, -0.52f * scale, 0.05f * scale);
        leftLegNode.AddChild(leftFoot);

        // Targeting laser on weapon arm
        var laserMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.1f, 0f),
            EmissionEnergyMultiplier = 3f
        };
        var laserMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.005f * scale, Height = 0.5f * scale };
        var laser = new MeshInstance3D { Mesh = laserMesh, MaterialOverride = laserMat };
        laser.Position = new Vector3(0, -0.5f * scale, 0.4f * scale);
        laser.RotationDegrees = new Vector3(90, 0, 0);
        rightArmNode.AddChild(laser);
    }

    /// <summary>
    /// Hive Mother - DCC bloated egg-laying queen with multiple appendages, larval attachments, pulsing sacs
    /// </summary>
    private static void CreateHiveMotherMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 2.5f;
        Color chitin = colorOverride ?? new Color(0.25f, 0.2f, 0.15f);
        Color eyeColor = new Color(0.8f, 0.3f, 0.1f);

        var chitinMat = new StandardMaterial3D { AlbedoColor = chitin, Roughness = 0.5f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = eyeColor,
            EmissionEnabled = true,
            Emission = eyeColor
        };

        // Large bloated abdomen - Height = 2*Radius for proper sphere
        var abdomen = new MeshInstance3D();
        abdomen.Mesh = new SphereMesh { Radius = 0.4f * scale, Height = 0.8f * scale };
        abdomen.MaterialOverride = chitinMat;
        abdomen.Position = new Vector3(0, 0.5f * scale, -0.3f * scale);
        abdomen.Scale = new Vector3(1f, 0.8f, 1.4f);
        parent.AddChild(abdomen);

        // Pulsing egg sacs on abdomen - Height = 2*Radius
        var sacMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.5f, 0.35f, 0.8f), Roughness = 0.4f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        var sacMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale, RadialSegments = 12, Rings = 8 };
        for (int si = 0; si < 6; si++)
        {
            float sacAngle = si * Mathf.Tau / 6f;
            var sac = new MeshInstance3D { Mesh = sacMesh, MaterialOverride = sacMat };
            sac.Position = new Vector3(Mathf.Cos(sacAngle) * 0.3f * scale, 0.4f * scale + (si % 2) * 0.1f * scale, -0.35f * scale + Mathf.Sin(sacAngle) * 0.25f * scale);
            sac.Scale = new Vector3(1f + (si % 3) * 0.2f, 0.8f + (si % 2) * 0.3f, 1f);
            parent.AddChild(sac);
        }

        // Larval attachments crawling on body - Height = 2*Radius
        var larvaeMat = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.65f, 0.5f), Roughness = 0.7f };
        var larvaeMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = 8, Rings = 6 };
        for (int li = 0; li < 8; li++)
        {
            float larvaeAngle = li * Mathf.Tau / 8f + Mathf.Pi / 8f;
            var larvae = new MeshInstance3D { Mesh = larvaeMesh, MaterialOverride = larvaeMat };
            larvae.Position = new Vector3(Mathf.Cos(larvaeAngle) * 0.35f * scale, 0.55f * scale, -0.25f * scale + Mathf.Sin(larvaeAngle) * 0.3f * scale);
            larvae.Scale = new Vector3(0.8f, 0.5f, 1.2f);
            parent.AddChild(larvae);
        }

        // Thorax - Height = 2*Radius for proper sphere
        var thorax = new MeshInstance3D();
        thorax.Mesh = new SphereMesh { Radius = 0.25f * scale, Height = 0.5f * scale };
        thorax.MaterialOverride = chitinMat;
        thorax.Position = new Vector3(0, 0.6f * scale, 0.1f * scale);
        parent.AddChild(thorax);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.7f * scale, 0.35f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Head - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale };
        head.MaterialOverride = chitinMat;
        headNode.AddChild(head);

        // Compound eyes - Height = 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.08f * scale, Height = 0.16f * scale, RadialSegments = 16, Rings = 12 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.1f * scale, 0.02f * scale, 0.08f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.1f * scale, 0.02f * scale, 0.08f * scale);
        headNode.AddChild(rightEye);

        // 6 legs
        var legMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.02f * scale, Height = 0.4f * scale };
        for (int side = 0; side < 2; side++)
        {
            float sideX = side == 0 ? -1f : 1f;
            for (int i = 0; i < 3; i++)
            {
                float legZ = (i - 1) * 0.2f * scale;
                var legNode = new Node3D();
                legNode.Position = new Vector3(sideX * 0.25f * scale, 0.5f * scale, legZ);
                legNode.RotationDegrees = new Vector3(0, 0, sideX * 45);
                parent.AddChild(legNode);

                var leg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = chitinMat };
                leg.Position = new Vector3(0, -0.2f * scale, 0);
                legNode.AddChild(leg);

                if (i == 0 && side == 0) limbs.LeftArm = legNode;
                if (i == 0 && side == 1) limbs.RightArm = legNode;
                if (i == 2 && side == 0) limbs.LeftLeg = legNode;
                if (i == 2 && side == 1) limbs.RightLeg = legNode;

                // Leg joints
                var jointMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.04f * scale, Height = 0.03f * scale };
                var joint = new MeshInstance3D { Mesh = jointMesh, MaterialOverride = chitinMat };
                joint.Position = new Vector3(0, -0.35f * scale, 0);
                joint.RotationDegrees = new Vector3(90, 0, 0);
                legNode.AddChild(joint);

                // Leg claws
                var clawMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.015f * scale, Height = 0.06f * scale };
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = chitinMat };
                claw.Position = new Vector3(0, -0.43f * scale, 0);
                legNode.AddChild(claw);
            }
        }

        // Antennae
        var antennaMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.015f * scale, Height = 0.25f * scale };
        var leftAntenna = new MeshInstance3D { Mesh = antennaMesh, MaterialOverride = chitinMat };
        leftAntenna.Position = new Vector3(-0.08f * scale, 0.1f * scale, 0.1f * scale);
        leftAntenna.RotationDegrees = new Vector3(-45, 0, -20);
        headNode.AddChild(leftAntenna);
        var rightAntenna = new MeshInstance3D { Mesh = antennaMesh, MaterialOverride = chitinMat };
        rightAntenna.Position = new Vector3(0.08f * scale, 0.1f * scale, 0.1f * scale);
        rightAntenna.RotationDegrees = new Vector3(-45, 0, 20);
        headNode.AddChild(rightAntenna);

        // Mandibles
        var mandibleMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.025f * scale, Height = 0.1f * scale };
        var leftMandible = new MeshInstance3D { Mesh = mandibleMesh, MaterialOverride = chitinMat };
        leftMandible.Position = new Vector3(-0.06f * scale, -0.08f * scale, 0.1f * scale);
        leftMandible.RotationDegrees = new Vector3(30, 0, 30);
        headNode.AddChild(leftMandible);
        var rightMandible = new MeshInstance3D { Mesh = mandibleMesh, MaterialOverride = chitinMat };
        rightMandible.Position = new Vector3(0.06f * scale, -0.08f * scale, 0.1f * scale);
        rightMandible.RotationDegrees = new Vector3(30, 0, -30);
        headNode.AddChild(rightMandible);

        // Egg sac patterns on abdomen
        var eggMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.85f, 0.7f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.3f
        };
        var eggMesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.08f * scale, Height = 0.1f * scale };
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5f;
            var egg = new MeshInstance3D { Mesh = eggMesh, MaterialOverride = eggMat };
            egg.Position = new Vector3(
                Mathf.Cos(angle) * 0.35f * scale,
                0.4f * scale,
                -0.3f * scale + Mathf.Sin(angle) * 0.3f * scale
            );
            parent.AddChild(egg);
        }

        // Stinger on abdomen
        var stingerMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.03f * scale, Height = 0.15f * scale };
        var stinger = new MeshInstance3D { Mesh = stingerMesh, MaterialOverride = chitinMat };
        stinger.Position = new Vector3(0, 0.3f * scale, -0.7f * scale);
        stinger.RotationDegrees = new Vector3(-110, 0, 0);
        parent.AddChild(stinger);

        // Venom gland glow
        var venomMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.9f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.5f, 0.8f, 0.2f),
            EmissionEnergyMultiplier = 1.5f
        };
        var venomMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.015f * scale, Height = 0.04f * scale };
        var venom = new MeshInstance3D { Mesh = venomMesh, MaterialOverride = venomMat };
        venom.Position = new Vector3(0, 0.25f * scale, -0.8f * scale);
        venom.RotationDegrees = new Vector3(-110, 0, 0);
        parent.AddChild(venom);
    }

    /// <summary>
    /// Architect's Favorite - DCC mechanical/magical hybrid with ornate design, glowing runes, asymmetric form
    /// </summary>
    private static void CreateArchitectsFavoriteMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 2.0f;
        Color whiteColor = colorOverride ?? new Color(0.95f, 0.95f, 0.98f);
        Color goldColor = new Color(1f, 0.85f, 0.4f);

        var whiteMat = new StandardMaterial3D { AlbedoColor = whiteColor, Roughness = 0.3f };
        var goldMat = new StandardMaterial3D
        {
            AlbedoColor = goldColor,
            EmissionEnabled = true,
            Emission = goldColor,
            EmissionEnergyMultiplier = 1.5f,
            Metallic = 0.9f
        };

        float floatY = 0.5f;

        // Elegant body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.15f * scale, BottomRadius = 0.2f * scale, Height = 0.6f * scale };
        body.MaterialOverride = whiteMat;
        body.Position = new Vector3(0, 0.7f * scale + floatY, 0);
        parent.AddChild(body);

        // Head node (featureless)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.15f * scale + floatY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Head - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale };
        head.MaterialOverride = whiteMat;
        headNode.AddChild(head);

        // Halo
        var halo = new MeshInstance3D();
        halo.Mesh = new TorusMesh { InnerRadius = 0.15f * scale, OuterRadius = 0.2f * scale };
        halo.MaterialOverride = goldMat;
        halo.Position = new Vector3(0, 0.25f * scale, 0);
        halo.RotationDegrees = new Vector3(90, 0, 0);
        headNode.AddChild(halo);

        // 6 wings (3 pairs)
        var wingMesh = new BoxMesh { Size = new Vector3(0.6f * scale, 0.02f * scale, 0.3f * scale) };
        for (int pair = 0; pair < 3; pair++)
        {
            float wingY = 0.9f * scale + floatY + pair * 0.15f * scale;
            float wingAngle = 15f + pair * 10f;

            var leftWing = new MeshInstance3D { Mesh = wingMesh, MaterialOverride = whiteMat };
            leftWing.Position = new Vector3(-0.4f * scale, wingY, 0);
            leftWing.RotationDegrees = new Vector3(0, 0, wingAngle);
            parent.AddChild(leftWing);

            var rightWing = new MeshInstance3D { Mesh = wingMesh, MaterialOverride = whiteMat };
            rightWing.Position = new Vector3(0.4f * scale, wingY, 0);
            rightWing.RotationDegrees = new Vector3(0, 0, -wingAngle);
            parent.AddChild(rightWing);
        }

        // Long arms
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.22f * scale, 0.95f * scale + floatY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.5f * scale };
        rightArm.MaterialOverride = whiteMat;
        rightArm.Position = new Vector3(0, -0.25f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.22f * scale, 0.95f * scale + floatY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.5f * scale };
        leftArm.MaterialOverride = whiteMat;
        leftArm.Position = new Vector3(0, -0.25f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Flowing robes (no legs)
        var robes = new MeshInstance3D();
        robes.Mesh = new CylinderMesh { TopRadius = 0.2f * scale, BottomRadius = 0.35f * scale, Height = 0.5f * scale };
        robes.MaterialOverride = whiteMat;
        robes.Position = new Vector3(0, 0.25f * scale + floatY, 0);
        parent.AddChild(robes);

        // Wing feather details
        var featherMesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.01f * scale, 0.08f * scale) };
        for (int pair = 0; pair < 3; pair++)
        {
            float wingY = 0.9f * scale + floatY + pair * 0.15f * scale;
            for (int f = 0; f < 4; f++)
            {
                // Left wing feathers
                var leftFeather = new MeshInstance3D { Mesh = featherMesh, MaterialOverride = whiteMat };
                leftFeather.Position = new Vector3(-0.5f * scale - f * 0.1f * scale, wingY - 0.02f * scale, f * 0.05f * scale);
                leftFeather.RotationDegrees = new Vector3(0, f * 5, 15 + pair * 10);
                parent.AddChild(leftFeather);
                // Right wing feathers
                var rightFeather = new MeshInstance3D { Mesh = featherMesh, MaterialOverride = whiteMat };
                rightFeather.Position = new Vector3(0.5f * scale + f * 0.1f * scale, wingY - 0.02f * scale, f * 0.05f * scale);
                rightFeather.RotationDegrees = new Vector3(0, -f * 5, -15 - pair * 10);
                parent.AddChild(rightFeather);
            }
        }

        // Ethereal hands
        var handMesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.03f * scale, Height = 0.08f * scale };
        var rightHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = whiteMat };
        rightHand.Position = new Vector3(0, -0.54f * scale, 0);
        rightArmNode.AddChild(rightHand);
        var leftHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = whiteMat };
        leftHand.Position = new Vector3(0, -0.54f * scale, 0);
        leftArmNode.AddChild(leftHand);

        // Long ethereal fingers
        var fingerMesh = new CylinderMesh { TopRadius = 0.003f * scale, BottomRadius = 0.01f * scale, Height = 0.1f * scale };
        for (int f = 0; f < 4; f++)
        {
            var rightFinger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = whiteMat };
            rightFinger.Position = new Vector3((f - 1.5f) * 0.02f * scale, -0.62f * scale, 0.02f * scale);
            rightFinger.RotationDegrees = new Vector3(15, 0, (f - 1.5f) * 8);
            rightArmNode.AddChild(rightFinger);
            var leftFinger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = whiteMat };
            leftFinger.Position = new Vector3((f - 1.5f) * 0.02f * scale, -0.62f * scale, 0.02f * scale);
            leftFinger.RotationDegrees = new Vector3(15, 0, (f - 1.5f) * 8);
            leftArmNode.AddChild(leftFinger);
        }

        // Glowing orbs around body
        var orbMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.95f, 0.8f, 0.5f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.9f, 0.6f),
            EmissionEnergyMultiplier = 2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var orbMesh = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.03f * scale, Height = 0.03f * scale };
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6f;
            var orb = new MeshInstance3D { Mesh = orbMesh, MaterialOverride = orbMat };
            orb.Position = new Vector3(
                Mathf.Cos(angle) * 0.4f * scale,
                0.8f * scale + floatY + Mathf.Sin(angle * 2) * 0.1f * scale,
                Mathf.Sin(angle) * 0.4f * scale
            );
            parent.AddChild(orb);
        }

        // Robe trim with gold
        var trimMesh = new CylinderMesh { TopRadius = 0.36f * scale, BottomRadius = 0.37f * scale, Height = 0.03f * scale };
        var trim = new MeshInstance3D { Mesh = trimMesh, MaterialOverride = goldMat };
        trim.Position = new Vector3(0, floatY, 0);
        parent.AddChild(trim);
    }

    /// <summary>
    /// Mordecai's Shadow - DCC ethereal shadow form with wispy edges, glowing eyes, tattered cloak
    /// </summary>
    private static void CreateMordecaisShadowMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.8f;
        Color shadowColor = colorOverride ?? new Color(0.15f, 0.12f, 0.2f);
        Color glitchColor = new Color(0.8f, 0.2f, 0.9f); // Purple glitch

        var shadowMat = new StandardMaterial3D { AlbedoColor = shadowColor, Roughness = 0.7f };
        var glitchMat = new StandardMaterial3D
        {
            AlbedoColor = glitchColor,
            EmissionEnabled = true,
            Emission = glitchColor,
            EmissionEnergyMultiplier = 2f
        };

        // Corrupted humanoid body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.2f * scale, BottomRadius = 0.25f * scale, Height = 0.55f * scale };
        body.MaterialOverride = shadowMat;
        body.Position = new Vector3(0, 0.65f * scale, 0);
        parent.AddChild(body);

        // Glitch effects on body
        var glitchMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.05f * scale, 0.3f * scale) };
        for (int i = 0; i < 3; i++)
        {
            var glitch = new MeshInstance3D { Mesh = glitchMesh, MaterialOverride = glitchMat };
            glitch.Position = new Vector3(
                (GD.Randf() - 0.5f) * 0.3f * scale,
                0.5f * scale + i * 0.2f * scale,
                (GD.Randf() - 0.5f) * 0.2f * scale
            );
            parent.AddChild(glitch);
        }

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.1f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Head - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.18f * scale, Height = 0.36f * scale };
        head.MaterialOverride = shadowMat;
        headNode.AddChild(head);

        // Glitching eyes - Height must equal 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glitchMat };
        leftEye.Position = new Vector3(-0.06f * scale, 0.02f * scale, 0.12f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glitchMat };
        rightEye.Position = new Vector3(0.06f * scale, 0.02f * scale, 0.12f * scale);
        headNode.AddChild(rightEye);
        var thirdEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glitchMat };
        thirdEye.Position = new Vector3(0, 0.1f * scale, 0.1f * scale);
        headNode.AddChild(thirdEye);

        // Arms
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.28f * scale, 0.8f * scale, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.05f * scale, Height = 0.4f * scale };
        rightArm.MaterialOverride = shadowMat;
        rightArm.Position = new Vector3(0, -0.2f * scale, 0);
        rightArmNode.AddChild(rightArm);

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.28f * scale, 0.8f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.05f * scale, Height = 0.4f * scale };
        leftArm.MaterialOverride = shadowMat;
        leftArm.Position = new Vector3(0, -0.2f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.12f * scale, 0.35f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.07f * scale, BottomRadius = 0.06f * scale, Height = 0.45f * scale };
        rightLeg.MaterialOverride = shadowMat;
        rightLeg.Position = new Vector3(0, -0.22f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.12f * scale, 0.35f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.07f * scale, BottomRadius = 0.06f * scale, Height = 0.45f * scale };
        leftLeg.MaterialOverride = shadowMat;
        leftLeg.Position = new Vector3(0, -0.22f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Floating sponsor tags
        var tagMesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.08f * scale, 0.02f * scale) };
        for (int i = 0; i < 4; i++)
        {
            var tag = new MeshInstance3D { Mesh = tagMesh, MaterialOverride = glitchMat };
            float angle = i * Mathf.Tau / 4f + GD.Randf() * 0.5f;
            tag.Position = new Vector3(
                Mathf.Cos(angle) * 0.5f * scale,
                0.7f * scale + (i % 2) * 0.3f * scale,
                Mathf.Sin(angle) * 0.5f * scale
            );
            // Calculate rotation manually (LookAt requires node to be in tree)
            var tagTarget = new Vector3(0, 0.7f * scale, 0);
            var tagDir = (tagTarget - tag.Position).Normalized();
            if (tagDir.LengthSquared() > 0.001f)
            {
                float tagYaw = Mathf.Atan2(tagDir.X, tagDir.Z);
                float tagPitch = -Mathf.Asin(tagDir.Y);
                tag.Rotation = new Vector3(tagPitch + Mathf.Pi / 2f, tagYaw, 0);
            }
            parent.AddChild(tag);
        }

        // Shadow tendrils from body
        var tendrilMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.08f, 0.15f, 0.7f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.9f
        };
        var tendrilMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.03f * scale, Height = 0.25f * scale };
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6f;
            var tendril = new MeshInstance3D { Mesh = tendrilMesh, MaterialOverride = tendrilMat };
            tendril.Position = new Vector3(
                Mathf.Cos(angle) * 0.28f * scale,
                0.5f * scale,
                Mathf.Sin(angle) * 0.22f * scale
            );
            tendril.RotationDegrees = new Vector3(Mathf.Cos(angle) * 30, 0, -Mathf.Sin(angle) * 30);
            parent.AddChild(tendril);
        }

        // Corrupted crown/halo
        var crownMat = new StandardMaterial3D
        {
            AlbedoColor = glitchColor.Darkened(0.3f),
            EmissionEnabled = true,
            Emission = glitchColor,
            EmissionEnergyMultiplier = 1.5f
        };
        var crownMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.15f * scale, 0.02f * scale) };
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5f;
            var spike = new MeshInstance3D { Mesh = crownMesh, MaterialOverride = crownMat };
            spike.Position = new Vector3(
                Mathf.Cos(angle) * 0.12f * scale,
                0.2f * scale,
                Mathf.Sin(angle) * 0.12f * scale
            );
            spike.RotationDegrees = new Vector3(-Mathf.Sin(angle) * 20, 0, Mathf.Cos(angle) * 20);
            headNode.AddChild(spike);
        }

        // Clawed hands
        var clawMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.015f * scale, Height = 0.08f * scale };
        for (int c = 0; c < 4; c++)
        {
            var rightClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = shadowMat };
            rightClaw.Position = new Vector3((c - 1.5f) * 0.02f * scale, -0.45f * scale, 0.02f * scale);
            rightClaw.RotationDegrees = new Vector3(20, 0, (c - 1.5f) * 10);
            rightArmNode.AddChild(rightClaw);
            var leftClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = shadowMat };
            leftClaw.Position = new Vector3((c - 1.5f) * 0.02f * scale, -0.45f * scale, 0.02f * scale);
            leftClaw.RotationDegrees = new Vector3(20, 0, (c - 1.5f) * 10);
            leftArmNode.AddChild(leftClaw);
        }

        // Glitch fragments floating around
        var fragmentMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.04f * scale, 0.01f * scale) };
        for (int i = 0; i < 8; i++)
        {
            var fragment = new MeshInstance3D { Mesh = fragmentMesh, MaterialOverride = glitchMat };
            float angle = i * Mathf.Tau / 8f;
            float radius = 0.35f + (i % 3) * 0.1f;
            fragment.Position = new Vector3(
                Mathf.Cos(angle) * radius * scale,
                0.6f * scale + (i % 4) * 0.15f * scale,
                Mathf.Sin(angle) * radius * scale
            );
            fragment.RotationDegrees = new Vector3(GD.Randf() * 45, GD.Randf() * 360, GD.Randf() * 45);
            parent.AddChild(fragment);
        }

        // Shadowy feet
        var footMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.04f * scale, 0.15f * scale) };
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = shadowMat };
        rightFoot.Position = new Vector3(0, -0.47f * scale, 0.03f * scale);
        rightLegNode.AddChild(rightFoot);
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = shadowMat };
        leftFoot.Position = new Vector3(0, -0.47f * scale, 0.03f * scale);
        leftLegNode.AddChild(leftFoot);
    }

    // ========================================================================
    // ADDITIONAL DCC BOSSES - Unique Character Models
    // ========================================================================

    /// <summary>
    /// Mordecai the Traitor - DCC detailed robes, staff, magical aura, beard, sinister expression
    /// Tall hooded figure with two daggers and a treacherous grin
    /// </summary>
    private static void CreateMordecaiTheTraitorMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.8f; // Boss scale
        Color cloakColor = colorOverride ?? new Color(0.15f, 0.1f, 0.2f); // Dark purple cloak
        Color skinColor = new Color(0.5f, 0.45f, 0.4f); // Pale skin
        Color metalColor = new Color(0.35f, 0.35f, 0.4f); // Dark metal

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var cloakMat = new StandardMaterial3D { AlbedoColor = cloakColor, Roughness = 0.8f };
        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.7f };
        var metalMat = new StandardMaterial3D { AlbedoColor = metalColor, Metallic = 0.7f, Roughness = 0.4f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.3f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.1f, 0.1f),
            EmissionEnergyMultiplier = 1.5f
        };

        // Body - slim robed torso
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.18f * scale, BottomRadius = 0.22f * scale, Height = 0.6f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = cloakMat;
        body.Position = new Vector3(0, 0.7f * scale, 0);
        parent.AddChild(body);

        // Belt
        var beltMesh = new CylinderMesh { TopRadius = 0.22f * scale, BottomRadius = 0.22f * scale, Height = 0.05f * scale, RadialSegments = radialSegs };
        var beltMat = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.25f, 0.2f), Roughness = 0.6f };
        var belt = new MeshInstance3D { Mesh = beltMesh, MaterialOverride = beltMat };
        belt.Position = new Vector3(0, 0.5f * scale, 0);
        parent.AddChild(belt);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.15f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Head - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.13f * scale, Height = 0.26f * scale, RadialSegments = radialSegs, Rings = rings };
        head.MaterialOverride = skinMat;
        headNode.AddChild(head);

        // Hood
        var hood = new MeshInstance3D();
        hood.Mesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.2f * scale, Height = 0.25f * scale, RadialSegments = radialSegs };
        hood.MaterialOverride = cloakMat;
        hood.Position = new Vector3(0, 0.08f * scale, -0.03f * scale);
        headNode.AddChild(hood);

        // Cunning eyes - Height must equal 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.04f * scale, 0, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.04f * scale, 0, 0.1f * scale);
        headNode.AddChild(rightEye);

        // Sinister grin
        var grinMesh = new BoxMesh { Size = new Vector3(0.08f * scale, 0.01f * scale, 0.02f * scale) };
        var grin = new MeshInstance3D { Mesh = grinMesh, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Black } };
        grin.Position = new Vector3(0, -0.06f * scale, 0.1f * scale);
        grin.RotationDegrees = new Vector3(0, 0, 5);
        headNode.AddChild(grin);

        // Arms with daggers
        float shoulderY = 0.9f * scale;
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.22f * scale, shoulderY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        rightArm.MaterialOverride = cloakMat;
        rightArm.Position = new Vector3(0, -0.17f * scale, 0);
        rightArmNode.AddChild(rightArm);

        // Right dagger
        var daggerBlade = new BoxMesh { Size = new Vector3(0.015f * scale, 0.2f * scale, 0.04f * scale) };
        var rightDagger = new MeshInstance3D { Mesh = daggerBlade, MaterialOverride = metalMat };
        rightDagger.Position = new Vector3(0.02f * scale, -0.4f * scale, 0.05f * scale);
        rightDagger.RotationDegrees = new Vector3(-20, 0, 0);
        rightArmNode.AddChild(rightDagger);
        limbs.Weapon = rightArmNode;

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.22f * scale, shoulderY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        leftArm.MaterialOverride = cloakMat;
        leftArm.Position = new Vector3(0, -0.17f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Left dagger
        var leftDagger = new MeshInstance3D { Mesh = daggerBlade, MaterialOverride = metalMat };
        leftDagger.Position = new Vector3(-0.02f * scale, -0.4f * scale, 0.05f * scale);
        leftDagger.RotationDegrees = new Vector3(-20, 0, 0);
        leftArmNode.AddChild(leftDagger);

        // Legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.1f * scale, 0.4f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.04f * scale, Height = 0.4f * scale, RadialSegments = radialSegs };
        rightLeg.MaterialOverride = cloakMat;
        rightLeg.Position = new Vector3(0, -0.2f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.1f * scale, 0.4f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.04f * scale, Height = 0.4f * scale, RadialSegments = radialSegs };
        leftLeg.MaterialOverride = cloakMat;
        leftLeg.Position = new Vector3(0, -0.2f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Cloak flowing cape
        var capeMesh = new BoxMesh { Size = new Vector3(0.4f * scale, 0.5f * scale, 0.05f * scale) };
        var cape = new MeshInstance3D { Mesh = capeMesh, MaterialOverride = cloakMat };
        cape.Position = new Vector3(0, 0.6f * scale, -0.18f * scale);
        parent.AddChild(cape);
    }

    /// <summary>
    /// Princess Donut - DCC cat body with tiara, fluffy fur, collar with gem, regal pose, expressive eyes
    /// Elegant cat with a crown and fluffy tail
    /// </summary>
    private static void CreatePrincessDonutMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.4f; // Boss scale
        Color furColor = colorOverride ?? new Color(0.95f, 0.9f, 0.85f); // White/cream fur
        Color spotColor = new Color(0.85f, 0.55f, 0.35f); // Orange spots
        Color crownColor = new Color(1f, 0.85f, 0.3f); // Gold crown

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var furMat = new StandardMaterial3D { AlbedoColor = furColor, Roughness = 0.85f };
        var spotMat = new StandardMaterial3D { AlbedoColor = spotColor, Roughness = 0.8f };
        var crownMat = new StandardMaterial3D { AlbedoColor = crownColor, Metallic = 0.9f, Roughness = 0.3f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.8f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.7f, 0.3f),
            EmissionEnergyMultiplier = 1.2f
        };
        var noseMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.6f, 0.6f) };

        // Cat body (horizontal/quadruped stance)
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.18f * scale, BottomRadius = 0.2f * scale, Height = 0.5f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = furMat;
        body.Position = new Vector3(0, 0.35f * scale, 0);
        body.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(body);

        // Orange spots on body - Height = 2*Radius for proper sphere
        var spotMesh = new SphereMesh { Radius = 0.06f * scale, Height = 0.12f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var spot1 = new MeshInstance3D { Mesh = spotMesh, MaterialOverride = spotMat };
        spot1.Position = new Vector3(0.12f * scale, 0.4f * scale, 0.1f * scale);
        spot1.Scale = new Vector3(1.2f, 0.8f, 0.6f);
        parent.AddChild(spot1);
        var spot2 = new MeshInstance3D { Mesh = spotMesh, MaterialOverride = spotMat };
        spot2.Position = new Vector3(-0.1f * scale, 0.35f * scale, -0.05f * scale);
        spot2.Scale = new Vector3(1f, 0.7f, 0.5f);
        parent.AddChild(spot2);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.45f * scale, 0.28f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Cat head - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.16f * scale, Height = 0.32f * scale, RadialSegments = radialSegs, Rings = rings };
        head.MaterialOverride = furMat;
        head.Scale = new Vector3(1f, 0.9f, 1.1f);
        headNode.AddChild(head);

        // Cat ears
        var earMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.05f * scale, Height = 0.1f * scale, RadialSegments = radialSegs / 2 };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        leftEar.Position = new Vector3(-0.08f * scale, 0.12f * scale, 0);
        leftEar.RotationDegrees = new Vector3(0, 0, -20);
        headNode.AddChild(leftEar);
        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        rightEar.Position = new Vector3(0.08f * scale, 0.12f * scale, 0);
        rightEar.RotationDegrees = new Vector3(0, 0, 20);
        headNode.AddChild(rightEar);

        // Crown!
        var crownBase = new MeshInstance3D();
        crownBase.Mesh = new CylinderMesh { TopRadius = 0.09f * scale, BottomRadius = 0.1f * scale, Height = 0.04f * scale, RadialSegments = radialSegs };
        crownBase.MaterialOverride = crownMat;
        crownBase.Position = new Vector3(0, 0.18f * scale, 0);
        headNode.AddChild(crownBase);

        // Crown points
        var crownPointMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.02f * scale, Height = 0.05f * scale, RadialSegments = 4 };
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5f;
            var crownPoint = new MeshInstance3D { Mesh = crownPointMesh, MaterialOverride = crownMat };
            crownPoint.Position = new Vector3(Mathf.Cos(angle) * 0.07f * scale, 0.22f * scale, Mathf.Sin(angle) * 0.07f * scale);
            headNode.AddChild(crownPoint);

            // Gem on top of each crown point - Height = 2*Radius
            if (i % 2 == 0)
            {
                var gemMat = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.2f, 0.3f), EmissionEnabled = true, Emission = new Color(0.6f, 0.1f, 0.2f), EmissionEnergyMultiplier = 0.5f };
                var gem = new MeshInstance3D();
                gem.Mesh = new SphereMesh { Radius = 0.012f * scale, Height = 0.024f * scale, RadialSegments = 8, Rings = 6 };
                gem.MaterialOverride = gemMat;
                gem.Position = new Vector3(Mathf.Cos(angle) * 0.07f * scale, 0.26f * scale, Mathf.Sin(angle) * 0.07f * scale);
                headNode.AddChild(gem);
            }
        }

        // Regal collar with gem
        var collarMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.15f, 0.2f), Roughness = 0.5f };
        var collarMesh = new CylinderMesh { TopRadius = 0.1f * scale, BottomRadius = 0.11f * scale, Height = 0.03f * scale, RadialSegments = radialSegs };
        var collar = new MeshInstance3D { Mesh = collarMesh, MaterialOverride = collarMat };
        collar.Position = new Vector3(0, -0.12f * scale, 0);
        headNode.AddChild(collar);

        // Gem pendant on collar - Height = 2*Radius
        var pendantGemMat = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.7f, 0.9f), EmissionEnabled = true, Emission = new Color(0.2f, 0.5f, 0.7f), EmissionEnergyMultiplier = 1f };
        var pendantGem = new MeshInstance3D();
        pendantGem.Mesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = 10, Rings = 8 };
        pendantGem.MaterialOverride = pendantGemMat;
        pendantGem.Position = new Vector3(0, -0.15f * scale, 0.1f * scale);
        headNode.AddChild(pendantGem);

        // Cat eyes with pupils - large expressive cat eyes with vertical slit pupils
        var catEyeMesh = new SphereMesh { Radius = 0.045f * scale, Height = 0.09f * scale, RadialSegments = radialSegs, Rings = rings };
        var catPupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        // Vertical slit pupils (cat-like)
        var catPupilMesh = new BoxMesh { Size = new Vector3(0.008f * scale, 0.04f * scale, 0.01f * scale) };

        // Left eye
        var leftEye = new MeshInstance3D { Mesh = catEyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.06f * scale, 0.02f * scale, 0.11f * scale);
        headNode.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = catPupilMesh, MaterialOverride = catPupilMat };
        leftPupil.Position = new Vector3(-0.06f * scale, 0.02f * scale, 0.15f * scale);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightEye = new MeshInstance3D { Mesh = catEyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.06f * scale, 0.02f * scale, 0.11f * scale);
        headNode.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = catPupilMesh, MaterialOverride = catPupilMat };
        rightPupil.Position = new Vector3(0.06f * scale, 0.02f * scale, 0.15f * scale);
        headNode.AddChild(rightPupil);

        // Cat nose - Height = 2*Radius for proper sphere
        var nose = new MeshInstance3D();
        nose.Mesh = new SphereMesh { Radius = 0.02f * scale, Height = 0.04f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, -0.04f * scale, 0.14f * scale);
        nose.Scale = new Vector3(1.2f, 0.8f, 0.8f);
        headNode.AddChild(nose);

        // Whiskers
        var whiskerMesh = new CylinderMesh { TopRadius = 0.002f * scale, BottomRadius = 0.002f * scale, Height = 0.08f * scale, RadialSegments = 4 };
        var whiskerMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.85f) };
        for (int side = 0; side < 2; side++)
        {
            for (int w = 0; w < 3; w++)
            {
                var whisker = new MeshInstance3D { Mesh = whiskerMesh, MaterialOverride = whiskerMat };
                float sideX = side == 0 ? -1f : 1f;
                whisker.Position = new Vector3(sideX * 0.06f * scale, -0.02f * scale + w * 0.02f * scale, 0.12f * scale);
                whisker.RotationDegrees = new Vector3(0, 0, sideX * (70 + w * 15));
                headNode.AddChild(whisker);
            }
        }

        // Front legs
        var legMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.25f * scale, RadialSegments = radialSegs };
        var frontRightLegNode = new Node3D();
        frontRightLegNode.Position = new Vector3(0.1f * scale, 0.2f * scale, 0.15f * scale);
        parent.AddChild(frontRightLegNode);
        limbs.RightArm = frontRightLegNode;
        var frLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        frLeg.Position = new Vector3(0, -0.12f * scale, 0);
        frontRightLegNode.AddChild(frLeg);

        var frontLeftLegNode = new Node3D();
        frontLeftLegNode.Position = new Vector3(-0.1f * scale, 0.2f * scale, 0.15f * scale);
        parent.AddChild(frontLeftLegNode);
        limbs.LeftArm = frontLeftLegNode;
        var flLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        flLeg.Position = new Vector3(0, -0.12f * scale, 0);
        frontLeftLegNode.AddChild(flLeg);

        // Back legs
        var backRightLegNode = new Node3D();
        backRightLegNode.Position = new Vector3(0.1f * scale, 0.2f * scale, -0.18f * scale);
        parent.AddChild(backRightLegNode);
        limbs.RightLeg = backRightLegNode;
        var brLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        brLeg.Position = new Vector3(0, -0.12f * scale, 0);
        backRightLegNode.AddChild(brLeg);

        var backLeftLegNode = new Node3D();
        backLeftLegNode.Position = new Vector3(-0.1f * scale, 0.2f * scale, -0.18f * scale);
        parent.AddChild(backLeftLegNode);
        limbs.LeftLeg = backLeftLegNode;
        var blLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        blLeg.Position = new Vector3(0, -0.12f * scale, 0);
        backLeftLegNode.AddChild(blLeg);

        // Fluffy tail
        var tailMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.06f * scale, Height = 0.25f * scale, RadialSegments = radialSegs };
        var tail = new MeshInstance3D { Mesh = tailMesh, MaterialOverride = furMat };
        tail.Position = new Vector3(0, 0.35f * scale, -0.35f * scale);
        tail.RotationDegrees = new Vector3(45, 0, 0);
        parent.AddChild(tail);
    }

    /// <summary>
    /// Mongo the Destroyer - DCC massive muscles, primitive armor, war paint, trophy necklace, huge weapon
    /// Huge muscular humanoid with armored plating
    /// </summary>
    private static void CreateMongoTheDestroyerMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 2.5f; // Very large boss
        Color skinColor = colorOverride ?? new Color(0.4f, 0.35f, 0.3f); // Grayish brown
        Color armorColor = new Color(0.25f, 0.25f, 0.28f); // Dark gray armor
        Color glowColor = new Color(1f, 0.4f, 0.1f); // Orange glow

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.9f };
        var armorMat = new StandardMaterial3D { AlbedoColor = armorColor, Metallic = 0.7f, Roughness = 0.5f };
        var glowMat = new StandardMaterial3D
        {
            AlbedoColor = glowColor,
            EmissionEnabled = true,
            Emission = glowColor,
            EmissionEnergyMultiplier = 2f
        };

        // Massive muscular body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.35f * scale, BottomRadius = 0.4f * scale, Height = 0.7f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = skinMat;
        body.Position = new Vector3(0, 0.7f * scale, 0);
        parent.AddChild(body);

        // War paint stripes on body
        var warPaintMat = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.15f, 0.1f), Roughness = 0.8f };
        var warPaintMesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.04f * scale, 0.01f * scale) };
        for (int wi = 0; wi < 4; wi++)
        {
            var warPaint = new MeshInstance3D { Mesh = warPaintMesh, MaterialOverride = warPaintMat };
            warPaint.Position = new Vector3(0, 0.55f * scale + wi * 0.12f * scale, 0.38f * scale);
            warPaint.RotationDegrees = new Vector3(0, 0, (wi % 2 == 0 ? 5 : -5));
            parent.AddChild(warPaint);
        }

        // Trophy necklace with skulls - Height = 2*Radius
        var necklaceMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.35f, 0.25f), Roughness = 0.7f };
        var necklaceMesh = new CylinderMesh { TopRadius = 0.28f * scale, BottomRadius = 0.28f * scale, Height = 0.02f * scale };
        var necklace = new MeshInstance3D { Mesh = necklaceMesh, MaterialOverride = necklaceMat };
        necklace.Position = new Vector3(0, 1.0f * scale, 0.05f * scale);
        necklace.RotationDegrees = new Vector3(75, 0, 0);
        parent.AddChild(necklace);

        // Trophy skulls on necklace - Height = 2*Radius
        var skullMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f), Roughness = 0.6f };
        var skullMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = 8, Rings = 6 };
        for (int ski = 0; ski < 5; ski++)
        {
            float skullAngle = (ski - 2) * Mathf.Pi / 8f;
            var skull = new MeshInstance3D { Mesh = skullMesh, MaterialOverride = skullMat };
            skull.Position = new Vector3(Mathf.Sin(skullAngle) * 0.26f * scale, 0.95f * scale, 0.18f * scale + Mathf.Cos(skullAngle) * 0.08f * scale);
            skull.Scale = new Vector3(0.8f, 1f, 0.7f);
            parent.AddChild(skull);
        }

        // Chest armor plate (primitive)
        var chestPlate = new MeshInstance3D();
        chestPlate.Mesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.4f * scale, 0.1f * scale) };
        chestPlate.MaterialOverride = armorMat;
        chestPlate.Position = new Vector3(0, 0.8f * scale, 0.2f * scale);
        parent.AddChild(chestPlate);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 1.2f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Small head compared to body - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.18f * scale, Height = 0.36f * scale, RadialSegments = radialSegs, Rings = rings };
        head.MaterialOverride = skinMat;
        head.Scale = new Vector3(1f, 0.9f, 1f);
        headNode.AddChild(head);

        // Helmet
        var helmet = new MeshInstance3D();
        helmet.Mesh = new CylinderMesh { TopRadius = 0.1f * scale, BottomRadius = 0.2f * scale, Height = 0.15f * scale, RadialSegments = radialSegs };
        helmet.MaterialOverride = armorMat;
        helmet.Position = new Vector3(0, 0.12f * scale, 0);
        headNode.AddChild(helmet);

        // Glowing eyes - Height must equal 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        leftEye.Position = new Vector3(-0.06f * scale, 0, 0.14f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        rightEye.Position = new Vector3(0.06f * scale, 0, 0.14f * scale);
        headNode.AddChild(rightEye);

        // Massive arms
        float shoulderY = 0.95f * scale;
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.45f * scale, shoulderY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale, RadialSegments = radialSegs };
        rightArm.MaterialOverride = skinMat;
        rightArm.Position = new Vector3(0, -0.25f * scale, 0);
        rightArmNode.AddChild(rightArm);

        // Shoulder armor - Height = 2*Radius for proper sphere
        var shoulderArmor = new MeshInstance3D();
        shoulderArmor.Mesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale, RadialSegments = radialSegs, Rings = rings };
        shoulderArmor.MaterialOverride = armorMat;
        shoulderArmor.Position = new Vector3(0.08f * scale, 0.05f * scale, 0);
        rightArmNode.AddChild(shoulderArmor);

        // Massive fist - Height = 2*Radius for proper sphere
        var fistMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.24f * scale, RadialSegments = radialSegs, Rings = rings };
        var rightFist = new MeshInstance3D { Mesh = fistMesh, MaterialOverride = skinMat };
        rightFist.Position = new Vector3(0, -0.55f * scale, 0);
        rightArmNode.AddChild(rightFist);
        limbs.Weapon = rightArmNode;

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.45f * scale, shoulderY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale, RadialSegments = radialSegs };
        leftArm.MaterialOverride = skinMat;
        leftArm.Position = new Vector3(0, -0.25f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Left shoulder - Height = 2*Radius for proper sphere
        var leftShoulder = new MeshInstance3D();
        leftShoulder.Mesh = new SphereMesh { Radius = 0.15f * scale, Height = 0.3f * scale, RadialSegments = radialSegs, Rings = rings };
        leftShoulder.MaterialOverride = armorMat;
        leftShoulder.Position = new Vector3(-0.08f * scale, 0.05f * scale, 0);
        leftArmNode.AddChild(leftShoulder);

        var leftFist = new MeshInstance3D { Mesh = fistMesh, MaterialOverride = skinMat };
        leftFist.Position = new Vector3(0, -0.55f * scale, 0);
        leftArmNode.AddChild(leftFist);

        // Massive legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.18f * scale, 0.35f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.14f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale, RadialSegments = radialSegs };
        rightLeg.MaterialOverride = skinMat;
        rightLeg.Position = new Vector3(0, -0.25f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.18f * scale, 0.35f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.14f * scale, BottomRadius = 0.1f * scale, Height = 0.5f * scale, RadialSegments = radialSegs };
        leftLeg.MaterialOverride = skinMat;
        leftLeg.Position = new Vector3(0, -0.25f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Armored boots
        var bootMesh = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.14f * scale, Height = 0.15f * scale, RadialSegments = radialSegs };
        var rightBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = armorMat };
        rightBoot.Position = new Vector3(0, -0.55f * scale, 0);
        rightLegNode.AddChild(rightBoot);
        var leftBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = armorMat };
        leftBoot.Position = new Vector3(0, -0.55f * scale, 0);
        leftLegNode.AddChild(leftBoot);
    }

    /// <summary>
    /// Zev the Loot Goblin - DCC bulging bags, coins spilling, merchant clothing, shifty eyes, many pockets
    /// Goblin with gold jewelry and a giant money bag
    /// </summary>
    private static void CreateZevTheLootGoblinMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.6f; // Boss scale
        Color skinColor = colorOverride ?? new Color(0.35f, 0.55f, 0.25f); // Green goblin skin
        Color goldColor = new Color(1f, 0.85f, 0.3f); // Gold
        Color clothColor = new Color(0.5f, 0.15f, 0.15f); // Rich red

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.75f };
        var goldMat = new StandardMaterial3D { AlbedoColor = goldColor, Metallic = 0.9f, Roughness = 0.25f };
        var clothMat = new StandardMaterial3D { AlbedoColor = clothColor, Roughness = 0.7f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = goldColor,
            EmissionEnabled = true,
            Emission = goldColor,
            EmissionEnergyMultiplier = 1.5f
        };
        var bagMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.45f, 0.3f), Roughness = 0.8f };

        // Goblin body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.15f * scale, BottomRadius = 0.2f * scale, Height = 0.4f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = clothMat;
        body.Position = new Vector3(0, 0.55f * scale, 0);
        parent.AddChild(body);

        // Pot belly - Height = 2*Radius for proper sphere
        var belly = new MeshInstance3D();
        belly.Mesh = new SphereMesh { Radius = 0.18f * scale, Height = 0.36f * scale, RadialSegments = radialSegs, Rings = rings };
        belly.MaterialOverride = skinMat;
        belly.Position = new Vector3(0, 0.45f * scale, 0.08f * scale);
        belly.Scale = new Vector3(1f, 0.8f, 0.7f);
        parent.AddChild(belly);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.9f * scale, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Large goblin head - Height = 2*Radius for proper sphere
        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.18f * scale, Height = 0.36f * scale, RadialSegments = radialSegs, Rings = rings };
        head.MaterialOverride = skinMat;
        head.Scale = new Vector3(1.1f, 1f, 0.9f);
        headNode.AddChild(head);

        // Huge pointy ears
        var earMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.06f * scale, Height = 0.2f * scale, RadialSegments = radialSegs / 2 };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.18f * scale, 0, 0);
        leftEar.RotationDegrees = new Vector3(0, 0, -55);
        headNode.AddChild(leftEar);
        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.18f * scale, 0, 0);
        rightEar.RotationDegrees = new Vector3(0, 0, 55);
        headNode.AddChild(rightEar);

        // Gold earrings
        var earringMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.01f * scale, RadialSegments = radialSegs / 2 };
        var leftEarring = new MeshInstance3D { Mesh = earringMesh, MaterialOverride = goldMat };
        leftEarring.Position = new Vector3(-0.22f * scale, -0.05f * scale, 0);
        headNode.AddChild(leftEarring);
        var rightEarring = new MeshInstance3D { Mesh = earringMesh, MaterialOverride = goldMat };
        rightEarring.Position = new Vector3(0.22f * scale, -0.05f * scale, 0);
        headNode.AddChild(rightEarring);

        // Greedy golden eyes - Height must equal 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.06f * scale, 0.02f * scale, 0.14f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.06f * scale, 0.02f * scale, 0.14f * scale);
        headNode.AddChild(rightEye);

        // Large goblin nose - Height = 2*Radius for proper sphere
        var nose = new MeshInstance3D();
        nose.Mesh = new SphereMesh { Radius = 0.05f * scale, Height = 0.1f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        nose.MaterialOverride = skinMat;
        nose.Position = new Vector3(0, -0.02f * scale, 0.16f * scale);
        nose.Scale = new Vector3(0.8f, 0.6f, 1f);
        headNode.AddChild(nose);

        // Greedy grin with gold tooth
        var grinMesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.025f * scale, 0.03f * scale) };
        var grin = new MeshInstance3D { Mesh = grinMesh, MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Black } };
        grin.Position = new Vector3(0, -0.1f * scale, 0.12f * scale);
        headNode.AddChild(grin);
        var goldTooth = new MeshInstance3D();
        goldTooth.Mesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.015f * scale, 0.015f * scale) };
        goldTooth.MaterialOverride = goldMat;
        goldTooth.Position = new Vector3(0.025f * scale, -0.09f * scale, 0.13f * scale);
        headNode.AddChild(goldTooth);

        // Arms
        float shoulderY = 0.7f * scale;
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(0.2f * scale, shoulderY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D();
        rightArm.Mesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.04f * scale, Height = 0.3f * scale, RadialSegments = radialSegs };
        rightArm.MaterialOverride = skinMat;
        rightArm.Position = new Vector3(0, -0.15f * scale, 0);
        rightArmNode.AddChild(rightArm);

        // Bulging money bag in right hand! - Height = 2*Radius for proper sphere
        var moneyBag = new MeshInstance3D();
        moneyBag.Mesh = new SphereMesh { Radius = 0.14f * scale, Height = 0.28f * scale, RadialSegments = radialSegs, Rings = rings };
        moneyBag.MaterialOverride = bagMat;
        moneyBag.Position = new Vector3(0.08f * scale, -0.38f * scale, 0.1f * scale);
        moneyBag.Scale = new Vector3(1.1f, 1.3f, 0.85f);
        rightArmNode.AddChild(moneyBag);
        limbs.Weapon = rightArmNode;

        // Coins spilling from bag - Height = 2*Radius
        var coinMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.015f * scale, Height = 0.004f * scale };
        for (int ci = 0; ci < 8; ci++)
        {
            var coin = new MeshInstance3D { Mesh = coinMesh, MaterialOverride = goldMat };
            float coinAngle = ci * Mathf.Tau / 8f;
            coin.Position = new Vector3(
                0.08f * scale + Mathf.Cos(coinAngle) * 0.08f * scale,
                -0.5f * scale - ci * 0.02f * scale,
                0.1f * scale + Mathf.Sin(coinAngle) * 0.04f * scale
            );
            coin.RotationDegrees = new Vector3(ci * 25, ci * 40, ci * 15);
            rightArmNode.AddChild(coin);
        }

        // Dollar sign on bag
        var dollarMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.08f * scale, 0.01f * scale) };
        var dollarSign = new MeshInstance3D { Mesh = dollarMesh, MaterialOverride = goldMat };
        dollarSign.Position = new Vector3(0.08f * scale, -0.38f * scale, 0.22f * scale);
        rightArmNode.AddChild(dollarSign);

        // Many pockets on vest
        var pocketMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.12f, 0.12f), Roughness = 0.75f };
        var pocketMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.08f * scale, 0.02f * scale) };
        for (int pi = 0; pi < 4; pi++)
        {
            var pocket = new MeshInstance3D { Mesh = pocketMesh, MaterialOverride = pocketMat };
            pocket.Position = new Vector3((pi % 2 == 0 ? -0.12f : 0.12f) * scale, 0.5f * scale - (pi / 2) * 0.1f * scale, 0.18f * scale);
            parent.AddChild(pocket);
        }

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.2f * scale, shoulderY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftArm = new MeshInstance3D();
        leftArm.Mesh = new CylinderMesh { TopRadius = 0.05f * scale, BottomRadius = 0.04f * scale, Height = 0.3f * scale, RadialSegments = radialSegs };
        leftArm.MaterialOverride = skinMat;
        leftArm.Position = new Vector3(0, -0.15f * scale, 0);
        leftArmNode.AddChild(leftArm);

        // Gold bracelet on left arm
        var braceletMesh = new CylinderMesh { TopRadius = 0.045f * scale, BottomRadius = 0.045f * scale, Height = 0.025f * scale, RadialSegments = radialSegs };
        var bracelet = new MeshInstance3D { Mesh = braceletMesh, MaterialOverride = goldMat };
        bracelet.Position = new Vector3(0, -0.32f * scale, 0);
        leftArmNode.AddChild(bracelet);

        // Legs
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(0.08f * scale, 0.35f * scale, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D();
        rightLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.045f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        rightLeg.MaterialOverride = skinMat;
        rightLeg.Position = new Vector3(0, -0.17f * scale, 0);
        rightLegNode.AddChild(rightLeg);

        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-0.08f * scale, 0.35f * scale, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D();
        leftLeg.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.045f * scale, Height = 0.35f * scale, RadialSegments = radialSegs };
        leftLeg.MaterialOverride = skinMat;
        leftLeg.Position = new Vector3(0, -0.17f * scale, 0);
        leftLegNode.AddChild(leftLeg);

        // Gold necklace with large gem
        var necklaceMesh = new CylinderMesh { TopRadius = 0.14f * scale, BottomRadius = 0.14f * scale, Height = 0.02f * scale, RadialSegments = radialSegs };
        var necklace = new MeshInstance3D { Mesh = necklaceMesh, MaterialOverride = goldMat };
        necklace.Position = new Vector3(0, 0.75f * scale, 0.08f * scale);
        necklace.RotationDegrees = new Vector3(80, 0, 0);
        parent.AddChild(necklace);

        // Large ruby on necklace - Height = 2*Radius for proper sphere
        var rubyMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var rubyMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.1f, 0.15f), EmissionEnabled = true, Emission = new Color(0.6f, 0.05f, 0.1f) };
        var ruby = new MeshInstance3D { Mesh = rubyMesh, MaterialOverride = rubyMat };
        ruby.Position = new Vector3(0, 0.72f * scale, 0.18f * scale);
        parent.AddChild(ruby);
    }

    // ========================================================================
    // BOPCA - Gnome shopkeeper NPC with pointy red hat
    // ========================================================================
    private static void CreateBopcaMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride, LODLevel lod = LODLevel.High)
    {
        // Gnome proportions: short and stout, about 0.8m tall
        float scale = 1.0f;

        // Colors
        Color skinColor = skinColorOverride ?? new Color(0.85f, 0.72f, 0.58f); // Warm peachy skin
        Color darkSkinColor = skinColor.Darkened(0.15f);
        Color hatColor = new Color(0.8f, 0.15f, 0.12f); // Bright red hat
        Color hatDarkColor = hatColor.Darkened(0.2f);
        Color tunicColor = new Color(0.25f, 0.45f, 0.22f); // Forest green tunic
        Color apronColor = new Color(0.45f, 0.32f, 0.2f); // Brown leather apron
        Color bootColor = new Color(0.35f, 0.22f, 0.12f); // Dark brown boots
        Color eyeWhiteColor = new Color(0.95f, 0.95f, 0.92f);
        Color eyeColor = new Color(0.3f, 0.5f, 0.3f); // Green eyes
        Color cheekColor = new Color(0.9f, 0.55f, 0.55f); // Rosy cheeks
        Color browColor = new Color(0.9f, 0.9f, 0.88f); // White bushy eyebrows
        Color buckleColor = new Color(0.75f, 0.65f, 0.2f); // Brass buckles

        // LOD-based segment counts
        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        // Materials
        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.75f };
        var darkSkinMat = new StandardMaterial3D { AlbedoColor = darkSkinColor, Roughness = 0.8f };
        var hatMat = new StandardMaterial3D { AlbedoColor = hatColor, Roughness = 0.6f };
        var hatDarkMat = new StandardMaterial3D { AlbedoColor = hatDarkColor, Roughness = 0.65f };
        var tunicMat = new StandardMaterial3D { AlbedoColor = tunicColor, Roughness = 0.85f };
        var apronMat = new StandardMaterial3D { AlbedoColor = apronColor, Roughness = 0.9f };
        var bootMat = new StandardMaterial3D { AlbedoColor = bootColor, Roughness = 0.85f };
        var eyeWhiteMat = new StandardMaterial3D { AlbedoColor = eyeWhiteColor, Roughness = 0.3f };
        var eyeMat = new StandardMaterial3D { AlbedoColor = eyeColor, Roughness = 0.4f };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        var cheekMat = new StandardMaterial3D { AlbedoColor = cheekColor, Roughness = 0.7f };
        var browMat = new StandardMaterial3D { AlbedoColor = browColor, Roughness = 0.9f };
        var buckleMat = new StandardMaterial3D { AlbedoColor = buckleColor, Metallic = 0.6f, Roughness = 0.3f };

        // === BODY - Stout gnome torso ===
        float bodyRadius = 0.16f * scale;
        float bodyHeight = 0.32f * scale;
        float bodyCenterY = 0.38f * scale;
        var bodyGeom = new BodyGeometry(bodyCenterY, bodyRadius, bodyHeight, 1.1f);

        // Main torso (green tunic)
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = bodyRadius, Height = bodyHeight, RadialSegments = radialSegs, Rings = rings };
        body.Mesh = bodyMesh;
        body.MaterialOverride = tunicMat;
        body.Position = new Vector3(0, bodyCenterY, 0);
        body.Scale = new Vector3(1.1f, 1.1f, 0.9f); // Slightly plump
        parent.AddChild(body);

        // Belly bulge
        var belly = new MeshInstance3D();
        var bellyMesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.24f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        belly.Mesh = bellyMesh;
        belly.MaterialOverride = tunicMat;
        belly.Position = new Vector3(0, 0.32f * scale, 0.08f * scale);
        parent.AddChild(belly);

        // Brown leather apron (front of body)
        if (lod >= LODLevel.Medium)
        {
            var apronMain = new MeshInstance3D();
            var apronMesh = new BoxMesh { Size = new Vector3(0.22f * scale, 0.28f * scale, 0.04f * scale) };
            apronMain.Mesh = apronMesh;
            apronMain.MaterialOverride = apronMat;
            apronMain.Position = new Vector3(0, 0.32f * scale, 0.13f * scale);
            parent.AddChild(apronMain);

            // Apron straps
            var strapMesh = new BoxMesh { Size = new Vector3(0.03f * scale, 0.18f * scale, 0.015f * scale) };
            var leftStrap = new MeshInstance3D { Mesh = strapMesh, MaterialOverride = apronMat };
            leftStrap.Position = new Vector3(-0.08f * scale, 0.48f * scale, 0.1f * scale);
            leftStrap.RotationDegrees = new Vector3(-15, 0, -10);
            parent.AddChild(leftStrap);
            var rightStrap = new MeshInstance3D { Mesh = strapMesh, MaterialOverride = apronMat };
            rightStrap.Position = new Vector3(0.08f * scale, 0.48f * scale, 0.1f * scale);
            rightStrap.RotationDegrees = new Vector3(-15, 0, 10);
            parent.AddChild(rightStrap);
        }

        // === HEAD - Large round gnome head ===
        float headRadius = 0.14f * scale;
        float headHeight = 0.28f * scale;
        float headCenterY = 0.68f * scale;

        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = headRadius, Height = headHeight, RadialSegments = radialSegs, Rings = rings };
        head.Mesh = headMesh;
        head.MaterialOverride = skinMat;
        head.Scale = new Vector3(1.05f, 0.95f, 1.0f); // Slightly wide face
        headNode.AddChild(head);

        // === ICONIC RED POINTY HAT ===
        // Hat sits ON TOP of head - head radius is 0.14f, so top of head is at Y=0.12f (due to 0.95 scale)
        float hatBaseY = 0.11f * scale;  // Just above top of head
        float hatHeight = 0.35f * scale;

        // Cone-shaped hat with slight droop
        var hatCone = new MeshInstance3D();
        var hatConeMesh = new CylinderMesh
        {
            TopRadius = 0.005f * scale, // Pointed tip
            BottomRadius = 0.14f * scale, // Match head width
            Height = hatHeight,
            RadialSegments = radialSegs,
            Rings = rings
        };
        hatCone.Mesh = hatConeMesh;
        hatCone.MaterialOverride = hatMat;
        // Position so bottom of cone is at hatBaseY (cone center is at hatBaseY + hatHeight/2)
        hatCone.Position = new Vector3(0.02f * scale, hatBaseY + hatHeight / 2f, -0.02f * scale);
        hatCone.RotationDegrees = new Vector3(-8, 0, 8); // Slight tilt for character
        headNode.AddChild(hatCone);

        // Hat brim (rolled edge) - sits at base of cone
        var hatBrim = new MeshInstance3D();
        var hatBrimMesh = new CylinderMesh
        {
            TopRadius = 0.16f * scale,
            BottomRadius = 0.14f * scale,
            Height = 0.04f * scale,
            RadialSegments = radialSegs,
            Rings = 2
        };
        hatBrim.Mesh = hatBrimMesh;
        hatBrim.MaterialOverride = hatDarkMat;
        hatBrim.Position = new Vector3(0, hatBaseY, 0);
        headNode.AddChild(hatBrim);

        // Wrinkle/fold detail on hat (High LOD)
        if (lod == LODLevel.High)
        {
            var foldMesh = new BoxMesh { Size = new Vector3(0.10f * scale, 0.02f * scale, 0.03f * scale) };
            for (int i = 0; i < 3; i++)
            {
                // Position folds along the hat cone, starting above the brim
                float foldY = hatBaseY + 0.08f + i * 0.08f;
                var fold = new MeshInstance3D { Mesh = foldMesh, MaterialOverride = hatDarkMat };
                fold.Position = new Vector3(0, foldY, 0.05f * scale);
                fold.RotationDegrees = new Vector3(20 + i * 5, i * 15 - 15, 0);
                fold.Scale = new Vector3(1f - i * 0.15f, 1f, 1f);
                headNode.AddChild(fold);
            }
        }

        // === FACE ===
        // Big bulbous nose
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.045f * scale, Height = 0.09f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = skinMat;
        nose.Position = new Vector3(0, -0.02f * scale, 0.13f * scale);
        nose.Scale = new Vector3(1.0f, 0.85f, 0.9f);
        headNode.AddChild(nose);

        // Nose tip (slightly pink)
        var noseTip = new MeshInstance3D();
        var noseTipMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 4, Rings = rings / 4 };
        noseTip.Mesh = noseTipMesh;
        noseTip.MaterialOverride = cheekMat;
        noseTip.Position = new Vector3(0, -0.03f * scale, 0.16f * scale);
        headNode.AddChild(noseTip);

        // Rosy cheeks
        var cheekMeshDef = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = radialSegs / 4, Rings = rings / 4 };
        var leftCheek = new MeshInstance3D { Mesh = cheekMeshDef, MaterialOverride = cheekMat };
        leftCheek.Position = new Vector3(-0.08f * scale, -0.04f * scale, 0.1f * scale);
        leftCheek.Scale = new Vector3(1f, 0.7f, 0.5f);
        headNode.AddChild(leftCheek);
        var rightCheek = new MeshInstance3D { Mesh = cheekMeshDef, MaterialOverride = cheekMat };
        rightCheek.Position = new Vector3(0.08f * scale, -0.04f * scale, 0.1f * scale);
        rightCheek.Scale = new Vector3(1f, 0.7f, 0.5f);
        headNode.AddChild(rightCheek);

        // Eyes - small and friendly
        int eyeSegs = lod == LODLevel.High ? 12 : 8;
        var eyeWhiteMesh = new SphereMesh { Radius = 0.022f * scale, Height = 0.044f * scale, RadialSegments = eyeSegs, Rings = eyeSegs / 2 };
        var irisMesh = new SphereMesh { Radius = 0.012f * scale, Height = 0.024f * scale, RadialSegments = eyeSegs, Rings = eyeSegs / 2 };
        var pupilMesh = new SphereMesh { Radius = 0.006f * scale, Height = 0.012f * scale, RadialSegments = eyeSegs / 2, Rings = eyeSegs / 4 };

        // Left eye
        var leftEyeWhite = new MeshInstance3D { Mesh = eyeWhiteMesh, MaterialOverride = eyeWhiteMat };
        leftEyeWhite.Position = new Vector3(-0.045f * scale, 0.02f * scale, 0.11f * scale);
        headNode.AddChild(leftEyeWhite);
        var leftIris = new MeshInstance3D { Mesh = irisMesh, MaterialOverride = eyeMat };
        leftIris.Position = new Vector3(-0.045f * scale, 0.02f * scale, 0.125f * scale);
        headNode.AddChild(leftIris);
        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-0.045f * scale, 0.02f * scale, 0.133f * scale);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightEyeWhite = new MeshInstance3D { Mesh = eyeWhiteMesh, MaterialOverride = eyeWhiteMat };
        rightEyeWhite.Position = new Vector3(0.045f * scale, 0.02f * scale, 0.11f * scale);
        headNode.AddChild(rightEyeWhite);
        var rightIris = new MeshInstance3D { Mesh = irisMesh, MaterialOverride = eyeMat };
        rightIris.Position = new Vector3(0.045f * scale, 0.02f * scale, 0.125f * scale);
        headNode.AddChild(rightIris);
        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(0.045f * scale, 0.02f * scale, 0.133f * scale);
        headNode.AddChild(rightPupil);

        // Bushy white eyebrows
        if (lod >= LODLevel.Medium)
        {
            var browMesh = new BoxMesh { Size = new Vector3(0.05f * scale, 0.015f * scale, 0.025f * scale) };
            var leftBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = browMat };
            leftBrow.Position = new Vector3(-0.045f * scale, 0.045f * scale, 0.1f * scale);
            leftBrow.RotationDegrees = new Vector3(15, 0, 8);
            headNode.AddChild(leftBrow);
            var rightBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = browMat };
            rightBrow.Position = new Vector3(0.045f * scale, 0.045f * scale, 0.1f * scale);
            rightBrow.RotationDegrees = new Vector3(15, 0, -8);
            headNode.AddChild(rightBrow);

            // Eyebrow tufts (bushier)
            var tuftMesh = new SphereMesh { Radius = 0.012f * scale, Height = 0.024f * scale, RadialSegments = 8, Rings = 4 };
            var leftTuft = new MeshInstance3D { Mesh = tuftMesh, MaterialOverride = browMat };
            leftTuft.Position = new Vector3(-0.06f * scale, 0.05f * scale, 0.09f * scale);
            headNode.AddChild(leftTuft);
            var rightTuft = new MeshInstance3D { Mesh = tuftMesh, MaterialOverride = browMat };
            rightTuft.Position = new Vector3(0.06f * scale, 0.05f * scale, 0.09f * scale);
            headNode.AddChild(rightTuft);
        }

        // Friendly smile
        var smileMesh = new CylinderMesh
        {
            TopRadius = 0.04f * scale,
            BottomRadius = 0.04f * scale,
            Height = 0.008f * scale,
            RadialSegments = radialSegs / 2,
            Rings = 1
        };
        var smile = new MeshInstance3D { Mesh = smileMesh, MaterialOverride = darkSkinMat };
        smile.Position = new Vector3(0, -0.08f * scale, 0.11f * scale);
        smile.RotationDegrees = new Vector3(80, 0, 0);
        smile.Scale = new Vector3(1f, 1f, 0.3f); // Flatten into arc
        headNode.AddChild(smile);

        // Pointy ears (gnome-style, smaller than goblin)
        var earMesh = new CylinderMesh
        {
            TopRadius = 0.008f * scale,
            BottomRadius = 0.035f * scale,
            Height = 0.1f * scale,
            RadialSegments = radialSegs / 4,
            Rings = 2
        };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.13f * scale, 0, 0);
        leftEar.RotationDegrees = new Vector3(10, 25, -50);
        headNode.AddChild(leftEar);
        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.13f * scale, 0, 0);
        rightEar.RotationDegrees = new Vector3(10, -25, 50);
        headNode.AddChild(rightEar);

        // === ARMS - Short stubby arms ===
        float upperArmRadius = 0.04f * scale;
        var upperArmMesh = new CapsuleMesh { Radius = upperArmRadius, Height = 0.12f * scale, RadialSegments = radialSegs / 2, Rings = 2 };
        var lowerArmMesh = new CapsuleMesh { Radius = 0.035f * scale, Height = 0.1f * scale, RadialSegments = radialSegs / 2, Rings = 2 };
        var handMesh = new SphereMesh { Radius = 0.03f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };

        float shoulderY = 0.52f * scale;
        float shoulderX = 0.18f * scale;

        // Left arm
        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0);
        leftArmNode.RotationDegrees = new Vector3(15, 0, -30); // Slightly forward, rubbing hands pose
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = tunicMat };
        leftUpperArm.Position = new Vector3(0, -0.06f * scale, 0);
        leftArmNode.AddChild(leftUpperArm);
        var leftLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = skinMat };
        leftLowerArm.Position = new Vector3(0, -0.15f * scale, 0.02f * scale);
        leftLowerArm.RotationDegrees = new Vector3(-20, 0, 0);
        leftArmNode.AddChild(leftLowerArm);
        var leftHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = skinMat };
        leftHand.Position = new Vector3(0, -0.22f * scale, 0.04f * scale);
        leftArmNode.AddChild(leftHand);

        // Right arm
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0);
        rightArmNode.RotationDegrees = new Vector3(15, 0, 30);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = tunicMat };
        rightUpperArm.Position = new Vector3(0, -0.06f * scale, 0);
        rightArmNode.AddChild(rightUpperArm);
        var rightLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = skinMat };
        rightLowerArm.Position = new Vector3(0, -0.15f * scale, 0.02f * scale);
        rightLowerArm.RotationDegrees = new Vector3(-20, 0, 0);
        rightArmNode.AddChild(rightLowerArm);
        var rightHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = skinMat };
        rightHand.Position = new Vector3(0, -0.22f * scale, 0.04f * scale);
        rightArmNode.AddChild(rightHand);

        // Fingers (High LOD only) - 4 stubby fingers
        if (lod == LODLevel.High)
        {
            var fingerMesh = new CylinderMesh { TopRadius = 0.008f * scale, BottomRadius = 0.01f * scale, Height = 0.035f * scale, RadialSegments = 6, Rings = 1 };
            for (int f = 0; f < 4; f++)
            {
                float fingerX = (-0.015f + f * 0.01f) * scale;
                var leftFinger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = skinMat };
                leftFinger.Position = new Vector3(fingerX, -0.25f * scale, 0.055f * scale);
                leftFinger.RotationDegrees = new Vector3(-50, 0, -8 + f * 5);
                leftArmNode.AddChild(leftFinger);
                var rightFinger = new MeshInstance3D { Mesh = fingerMesh, MaterialOverride = skinMat };
                rightFinger.Position = new Vector3(fingerX, -0.25f * scale, 0.055f * scale);
                rightFinger.RotationDegrees = new Vector3(-50, 0, -8 + f * 5);
                rightArmNode.AddChild(rightFinger);
            }
        }

        // === LEGS - Short stubby legs with big boots ===
        float legRadius = 0.045f * scale;
        var legMesh = new CapsuleMesh { Radius = legRadius, Height = 0.12f * scale, RadialSegments = radialSegs / 2, Rings = 2 };
        var bootMesh = new CapsuleMesh { Radius = 0.055f * scale, Height = 0.1f * scale, RadialSegments = radialSegs / 2, Rings = 2 };
        var footMesh = new SphereMesh { Radius = 0.055f * scale, Height = 0.11f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };

        float hipY = 0.22f * scale;
        float hipX = 0.08f * scale;

        // Left leg
        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = tunicMat };
        leftLeg.Position = new Vector3(0, -0.06f * scale, 0);
        leftLegNode.AddChild(leftLeg);
        var leftBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = bootMat };
        leftBoot.Position = new Vector3(0, -0.14f * scale, 0);
        leftLegNode.AddChild(leftBoot);
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = bootMat };
        leftFoot.Position = new Vector3(0, -0.2f * scale, 0.03f * scale);
        leftFoot.Scale = new Vector3(0.9f, 0.6f, 1.3f); // Big oversized feet
        leftLegNode.AddChild(leftFoot);

        // Right leg
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = tunicMat };
        rightLeg.Position = new Vector3(0, -0.06f * scale, 0);
        rightLegNode.AddChild(rightLeg);
        var rightBoot = new MeshInstance3D { Mesh = bootMesh, MaterialOverride = bootMat };
        rightBoot.Position = new Vector3(0, -0.14f * scale, 0);
        rightLegNode.AddChild(rightBoot);
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = bootMat };
        rightFoot.Position = new Vector3(0, -0.2f * scale, 0.03f * scale);
        rightFoot.Scale = new Vector3(0.9f, 0.6f, 1.3f);
        rightLegNode.AddChild(rightFoot);

        // Boot buckles (High LOD)
        if (lod == LODLevel.High)
        {
            var buckleMesh = new BoxMesh { Size = new Vector3(0.025f * scale, 0.015f * scale, 0.01f * scale) };
            var leftBuckle = new MeshInstance3D { Mesh = buckleMesh, MaterialOverride = buckleMat };
            leftBuckle.Position = new Vector3(0, -0.12f * scale, 0.045f * scale);
            leftLegNode.AddChild(leftBuckle);
            var rightBuckle = new MeshInstance3D { Mesh = buckleMesh, MaterialOverride = buckleMat };
            rightBuckle.Position = new Vector3(0, -0.12f * scale, 0.045f * scale);
            rightLegNode.AddChild(rightBuckle);
        }

        // Set body reference for animations
        limbs.Body = parent;
    }

    /// <summary>
    /// Create Mordecai the Game Guide - a Splinter-style rat humanoid sage with robes and staff.
    /// Wise old rat mentor figure, tall and slender, wearing flowing robes.
    /// </summary>
    private static void CreateMordecaiMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride, LODLevel lod = LODLevel.High)
    {
        // Mordecai proportions: tall and slender rat humanoid, about 1.6m tall (hunched to ~1.4m)
        float scale = 1.0f;

        // Colors - wise sage rat palette
        Color furColor = skinColorOverride ?? new Color(0.55f, 0.48f, 0.42f); // Warm gray-brown fur
        Color darkFurColor = furColor.Darkened(0.2f);
        Color lightFurColor = furColor.Lightened(0.15f);
        Color robeColor = new Color(0.35f, 0.28f, 0.45f); // Deep purple sage robes
        Color robeDarkColor = robeColor.Darkened(0.2f);
        Color robeGoldTrim = new Color(0.75f, 0.6f, 0.2f); // Gold trim
        Color sashColor = new Color(0.65f, 0.55f, 0.3f); // Tan sash
        Color staffColor = new Color(0.4f, 0.3f, 0.2f); // Dark wood
        Color gemColor = new Color(0.3f, 0.8f, 0.6f); // Teal crystal on staff
        Color noseColor = new Color(0.8f, 0.6f, 0.6f); // Pink nose
        Color eyeColor = new Color(0.4f, 0.35f, 0.25f); // Wise brown eyes
        Color whiskerColor = new Color(0.85f, 0.82f, 0.78f); // White whiskers

        // LOD-based segment counts
        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        // Materials
        var furMat = new StandardMaterial3D { AlbedoColor = furColor, Roughness = 0.85f };
        var darkFurMat = new StandardMaterial3D { AlbedoColor = darkFurColor, Roughness = 0.9f };
        var lightFurMat = new StandardMaterial3D { AlbedoColor = lightFurColor, Roughness = 0.85f };
        var robeMat = new StandardMaterial3D { AlbedoColor = robeColor, Roughness = 0.8f };
        var robeDarkMat = new StandardMaterial3D { AlbedoColor = robeDarkColor, Roughness = 0.85f };
        var goldTrimMat = new StandardMaterial3D { AlbedoColor = robeGoldTrim, Metallic = 0.4f, Roughness = 0.4f };
        var sashMat = new StandardMaterial3D { AlbedoColor = sashColor, Roughness = 0.75f };
        var staffMat = new StandardMaterial3D { AlbedoColor = staffColor, Roughness = 0.7f };
        var gemMat = new StandardMaterial3D { AlbedoColor = gemColor, Metallic = 0.2f, Roughness = 0.2f, Emission = gemColor * 0.3f, EmissionEnabled = true };
        var noseMat = new StandardMaterial3D { AlbedoColor = noseColor, Roughness = 0.5f };
        var eyeWhiteMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.93f, 0.9f), Roughness = 0.3f };
        var eyeMat = new StandardMaterial3D { AlbedoColor = eyeColor, Roughness = 0.4f };
        var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        var whiskerMat = new StandardMaterial3D { AlbedoColor = whiskerColor, Roughness = 0.6f };

        // === BODY - Slender robed torso (slightly hunched) ===
        float bodyRadius = 0.14f * scale;
        float bodyHeight = 0.45f * scale;
        float bodyCenterY = 0.65f * scale;

        // Main torso in robes
        var bodyNode = new Node3D();
        bodyNode.Position = new Vector3(0, bodyCenterY, 0);
        bodyNode.RotationDegrees = new Vector3(8, 0, 0); // Slight hunch forward
        parent.AddChild(bodyNode);
        limbs.Body = bodyNode;

        var body = new MeshInstance3D();
        var bodyMesh = new CapsuleMesh { Radius = bodyRadius, Height = bodyHeight, RadialSegments = radialSegs, Rings = rings };
        body.Mesh = bodyMesh;
        body.MaterialOverride = robeMat;
        body.Scale = new Vector3(1.0f, 1.0f, 0.85f); // Slightly thin
        bodyNode.AddChild(body);

        // Robe folds (flowing fabric detail)
        if (lod >= LODLevel.Medium)
        {
            var foldMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.35f * scale, 0.03f * scale) };
            for (int i = 0; i < 5; i++)
            {
                float angle = -60 + i * 30;
                var fold = new MeshInstance3D { Mesh = foldMesh, MaterialOverride = robeDarkMat };
                fold.Position = new Vector3(
                    Mathf.Sin(Mathf.DegToRad(angle)) * 0.12f * scale,
                    -0.08f * scale,
                    Mathf.Cos(Mathf.DegToRad(angle)) * 0.1f * scale
                );
                fold.RotationDegrees = new Vector3(5, angle, 0);
                bodyNode.AddChild(fold);
            }
        }

        // Flowing robe bottom (skirt)
        var robeSkirt = new MeshInstance3D();
        var skirtMesh = new CylinderMesh
        {
            TopRadius = 0.16f * scale,
            BottomRadius = 0.28f * scale,
            Height = 0.5f * scale,
            RadialSegments = radialSegs,
            Rings = rings / 2
        };
        robeSkirt.Mesh = skirtMesh;
        robeSkirt.MaterialOverride = robeMat;
        robeSkirt.Position = new Vector3(0, -0.45f * scale, 0);
        bodyNode.AddChild(robeSkirt);

        // Gold trim at robe hem
        if (lod >= LODLevel.Medium)
        {
            var trimMesh = new CylinderMesh
            {
                TopRadius = 0.285f * scale,
                BottomRadius = 0.29f * scale,
                Height = 0.025f * scale,
                RadialSegments = radialSegs,
                Rings = 1
            };
            var trimHem = new MeshInstance3D { Mesh = trimMesh, MaterialOverride = goldTrimMat };
            trimHem.Position = new Vector3(0, -0.69f * scale, 0);
            bodyNode.AddChild(trimHem);
        }

        // Sash/belt
        var sash = new MeshInstance3D();
        var sashMesh = new CylinderMesh
        {
            TopRadius = 0.145f * scale,
            BottomRadius = 0.15f * scale,
            Height = 0.06f * scale,
            RadialSegments = radialSegs,
            Rings = 1
        };
        sash.Mesh = sashMesh;
        sash.MaterialOverride = sashMat;
        sash.Position = new Vector3(0, -0.15f * scale, 0);
        bodyNode.AddChild(sash);

        // Sash knot/tail
        var sashTail = new MeshInstance3D();
        var sashTailMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.15f * scale, 0.02f * scale) };
        sashTail.Mesh = sashTailMesh;
        sashTail.MaterialOverride = sashMat;
        sashTail.Position = new Vector3(0.1f * scale, -0.2f * scale, 0.1f * scale);
        sashTail.RotationDegrees = new Vector3(15, 20, -10);
        bodyNode.AddChild(sashTail);

        // === HEAD - Rat head with long snout ===
        float headRadius = 0.1f * scale;
        float headHeight = 0.22f * scale;
        float headCenterY = 1.05f * scale;

        var headNode = new Node3D();
        headNode.Position = new Vector3(0, headCenterY, 0.05f * scale); // Slightly forward (hunched)
        headNode.RotationDegrees = new Vector3(-5, 0, 0); // Tilted slightly down (wise contemplative pose)
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Main head (oval rat skull shape)
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = headRadius, Height = headHeight, RadialSegments = radialSegs, Rings = rings };
        head.Mesh = headMesh;
        head.MaterialOverride = furMat;
        head.Scale = new Vector3(0.9f, 1.0f, 1.1f); // Elongated skull
        headNode.AddChild(head);

        // === RAT SNOUT - Long pointed snout ===
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh
        {
            TopRadius = 0.02f * scale,  // Pointed tip
            BottomRadius = 0.07f * scale, // Wide at face
            Height = 0.14f * scale,
            RadialSegments = radialSegs / 2,
            Rings = rings / 2
        };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = furMat;
        snout.Position = new Vector3(0, -0.03f * scale, 0.12f * scale);
        snout.RotationDegrees = new Vector3(-75, 0, 0); // Point forward
        headNode.AddChild(snout);

        // Snout bridge (top of snout)
        var snoutBridge = new MeshInstance3D();
        var bridgeMesh = new BoxMesh { Size = new Vector3(0.05f * scale, 0.12f * scale, 0.04f * scale) };
        snoutBridge.Mesh = bridgeMesh;
        snoutBridge.MaterialOverride = furMat;
        snoutBridge.Position = new Vector3(0, 0, 0.13f * scale);
        snoutBridge.RotationDegrees = new Vector3(-15, 0, 0);
        headNode.AddChild(snoutBridge);

        // Pink nose at snout tip
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 4, Rings = rings / 4 };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, -0.04f * scale, 0.22f * scale);
        nose.Scale = new Vector3(1.2f, 0.8f, 0.9f);
        headNode.AddChild(nose);

        // Nostrils
        var nostrilMesh = new SphereMesh { Radius = 0.008f * scale, Height = 0.016f * scale, RadialSegments = 6, Rings = 3 };
        var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = darkFurMat };
        leftNostril.Position = new Vector3(-0.012f * scale, -0.045f * scale, 0.235f * scale);
        headNode.AddChild(leftNostril);
        var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = darkFurMat };
        rightNostril.Position = new Vector3(0.012f * scale, -0.045f * scale, 0.235f * scale);
        headNode.AddChild(rightNostril);

        // === WHISKERS - Long graceful whiskers ===
        if (lod >= LODLevel.Medium)
        {
            var whiskerMesh = new CylinderMesh
            {
                TopRadius = 0.001f * scale,
                BottomRadius = 0.003f * scale,
                Height = 0.12f * scale,
                RadialSegments = 4,
                Rings = 1
            };

            // 3 whiskers on each side, angled outward
            for (int w = 0; w < 3; w++)
            {
                float whiskerY = -0.02f - w * 0.015f;
                float whiskerAngle = 10 + w * 8;

                // Left whiskers
                var leftWhisker = new MeshInstance3D { Mesh = whiskerMesh, MaterialOverride = whiskerMat };
                leftWhisker.Position = new Vector3(-0.04f * scale, whiskerY * scale, 0.18f * scale);
                leftWhisker.RotationDegrees = new Vector3(whiskerAngle, -70 - w * 10, 0);
                headNode.AddChild(leftWhisker);

                // Right whiskers
                var rightWhisker = new MeshInstance3D { Mesh = whiskerMesh, MaterialOverride = whiskerMat };
                rightWhisker.Position = new Vector3(0.04f * scale, whiskerY * scale, 0.18f * scale);
                rightWhisker.RotationDegrees = new Vector3(whiskerAngle, 70 + w * 10, 0);
                headNode.AddChild(rightWhisker);
            }
        }

        // === LARGE RAT EARS ===
        var earMesh = new SphereMesh { Radius = 0.055f * scale, Height = 0.11f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var earInnerMesh = new SphereMesh { Radius = 0.035f * scale, Height = 0.07f * scale, RadialSegments = radialSegs / 3, Rings = rings / 3 };

        // Left ear
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        leftEar.Position = new Vector3(-0.08f * scale, 0.08f * scale, 0);
        leftEar.RotationDegrees = new Vector3(-15, -30, -25);
        leftEar.Scale = new Vector3(0.6f, 1.0f, 0.15f); // Thin, tall ear
        headNode.AddChild(leftEar);
        var leftEarInner = new MeshInstance3D { Mesh = earInnerMesh, MaterialOverride = noseMat }; // Pink inner ear
        leftEarInner.Position = new Vector3(-0.075f * scale, 0.08f * scale, 0.01f * scale);
        leftEarInner.RotationDegrees = new Vector3(-15, -30, -25);
        leftEarInner.Scale = new Vector3(0.5f, 0.85f, 0.1f);
        headNode.AddChild(leftEarInner);

        // Right ear
        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        rightEar.Position = new Vector3(0.08f * scale, 0.08f * scale, 0);
        rightEar.RotationDegrees = new Vector3(-15, 30, 25);
        rightEar.Scale = new Vector3(0.6f, 1.0f, 0.15f);
        headNode.AddChild(rightEar);
        var rightEarInner = new MeshInstance3D { Mesh = earInnerMesh, MaterialOverride = noseMat };
        rightEarInner.Position = new Vector3(0.075f * scale, 0.08f * scale, 0.01f * scale);
        rightEarInner.RotationDegrees = new Vector3(-15, 30, 25);
        rightEarInner.Scale = new Vector3(0.5f, 0.85f, 0.1f);
        headNode.AddChild(rightEarInner);

        // === WISE EYES - Small, kind eyes ===
        int eyeSegs = lod == LODLevel.High ? 12 : 8;
        var eyeWhiteMesh = new SphereMesh { Radius = 0.018f * scale, Height = 0.036f * scale, RadialSegments = eyeSegs, Rings = eyeSegs / 2 };
        var irisMesh = new SphereMesh { Radius = 0.012f * scale, Height = 0.024f * scale, RadialSegments = eyeSegs, Rings = eyeSegs / 2 };
        var pupilMeshDef = new SphereMesh { Radius = 0.006f * scale, Height = 0.012f * scale, RadialSegments = eyeSegs / 2, Rings = eyeSegs / 4 };

        float eyeY = 0.03f * scale;
        float eyeX = 0.04f * scale;
        float eyeZ = 0.08f * scale;

        // Left eye
        var leftEyeWhite = new MeshInstance3D { Mesh = eyeWhiteMesh, MaterialOverride = eyeWhiteMat };
        leftEyeWhite.Position = new Vector3(-eyeX, eyeY, eyeZ);
        headNode.AddChild(leftEyeWhite);
        var leftIris = new MeshInstance3D { Mesh = irisMesh, MaterialOverride = eyeMat };
        leftIris.Position = new Vector3(-eyeX, eyeY, eyeZ + 0.012f * scale);
        headNode.AddChild(leftIris);
        var leftPupil = new MeshInstance3D { Mesh = pupilMeshDef, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-eyeX, eyeY, eyeZ + 0.018f * scale);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightEyeWhite = new MeshInstance3D { Mesh = eyeWhiteMesh, MaterialOverride = eyeWhiteMat };
        rightEyeWhite.Position = new Vector3(eyeX, eyeY, eyeZ);
        headNode.AddChild(rightEyeWhite);
        var rightIris = new MeshInstance3D { Mesh = irisMesh, MaterialOverride = eyeMat };
        rightIris.Position = new Vector3(eyeX, eyeY, eyeZ + 0.012f * scale);
        headNode.AddChild(rightIris);
        var rightPupil = new MeshInstance3D { Mesh = pupilMeshDef, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(eyeX, eyeY, eyeZ + 0.018f * scale);
        headNode.AddChild(rightPupil);

        // Wise eyebrow ridges (furrowed brow)
        if (lod >= LODLevel.Medium)
        {
            var browMesh = new BoxMesh { Size = new Vector3(0.035f * scale, 0.01f * scale, 0.02f * scale) };
            var leftBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkFurMat };
            leftBrow.Position = new Vector3(-eyeX, eyeY + 0.025f * scale, eyeZ);
            leftBrow.RotationDegrees = new Vector3(15, 0, 10);
            headNode.AddChild(leftBrow);
            var rightBrow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkFurMat };
            rightBrow.Position = new Vector3(eyeX, eyeY + 0.025f * scale, eyeZ);
            rightBrow.RotationDegrees = new Vector3(15, 0, -10);
            headNode.AddChild(rightBrow);
        }

        // Small contemplative mouth
        var mouthMesh = new BoxMesh { Size = new Vector3(0.025f * scale, 0.006f * scale, 0.01f * scale) };
        var mouth = new MeshInstance3D { Mesh = mouthMesh, MaterialOverride = darkFurMat };
        mouth.Position = new Vector3(0, -0.06f * scale, 0.14f * scale);
        headNode.AddChild(mouth);

        // === ARMS - Slender with large paw-hands ===
        float upperArmRadius = 0.035f * scale;
        var upperArmMesh = new CapsuleMesh { Radius = upperArmRadius, Height = 0.18f * scale, RadialSegments = radialSegs / 2, Rings = 2 };
        var lowerArmMesh = new CapsuleMesh { Radius = 0.03f * scale, Height = 0.16f * scale, RadialSegments = radialSegs / 2, Rings = 2 };
        var handMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };

        float shoulderY = 0.92f * scale;
        float shoulderX = 0.16f * scale;

        // Left arm (holding robe)
        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0.03f * scale);
        leftArmNode.RotationDegrees = new Vector3(25, 0, -15);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        // Robe sleeve
        var sleeveMesh = new CylinderMesh
        {
            TopRadius = 0.05f * scale,
            BottomRadius = 0.08f * scale,
            Height = 0.22f * scale,
            RadialSegments = radialSegs / 2,
            Rings = 2
        };
        var leftSleeve = new MeshInstance3D { Mesh = sleeveMesh, MaterialOverride = robeMat };
        leftSleeve.Position = new Vector3(0, -0.11f * scale, 0);
        leftArmNode.AddChild(leftSleeve);

        var leftUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = furMat };
        leftUpperArm.Position = new Vector3(0, -0.09f * scale, 0);
        leftArmNode.AddChild(leftUpperArm);
        var leftLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = furMat };
        leftLowerArm.Position = new Vector3(0, -0.22f * scale, 0.02f * scale);
        leftLowerArm.RotationDegrees = new Vector3(-25, 0, 0);
        leftArmNode.AddChild(leftLowerArm);
        var leftHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = furMat };
        leftHand.Position = new Vector3(0, -0.34f * scale, 0.05f * scale);
        leftHand.Scale = new Vector3(1.1f, 0.7f, 1.3f); // Flat paw shape
        leftArmNode.AddChild(leftHand);

        // Right arm (holding staff)
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0.03f * scale);
        rightArmNode.RotationDegrees = new Vector3(15, 0, 25);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightSleeve = new MeshInstance3D { Mesh = sleeveMesh, MaterialOverride = robeMat };
        rightSleeve.Position = new Vector3(0, -0.11f * scale, 0);
        rightArmNode.AddChild(rightSleeve);

        var rightUpperArm = new MeshInstance3D { Mesh = upperArmMesh, MaterialOverride = furMat };
        rightUpperArm.Position = new Vector3(0, -0.09f * scale, 0);
        rightArmNode.AddChild(rightUpperArm);
        var rightLowerArm = new MeshInstance3D { Mesh = lowerArmMesh, MaterialOverride = furMat };
        rightLowerArm.Position = new Vector3(0, -0.22f * scale, 0.02f * scale);
        rightLowerArm.RotationDegrees = new Vector3(-15, 0, 0);
        rightArmNode.AddChild(rightLowerArm);
        var rightHand = new MeshInstance3D { Mesh = handMesh, MaterialOverride = furMat };
        rightHand.Position = new Vector3(0, -0.32f * scale, 0.03f * scale);
        rightHand.Scale = new Vector3(1.1f, 0.7f, 1.3f);
        rightArmNode.AddChild(rightHand);

        // Paw fingers/claws (High LOD)
        if (lod == LODLevel.High)
        {
            var clawMesh = new CylinderMesh { TopRadius = 0.004f * scale, BottomRadius = 0.008f * scale, Height = 0.03f * scale, RadialSegments = 5, Rings = 1 };
            for (int f = 0; f < 4; f++)
            {
                float fingerX = (-0.018f + f * 0.012f) * scale;
                var leftClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = darkFurMat };
                leftClaw.Position = new Vector3(fingerX, -0.37f * scale, 0.08f * scale);
                leftClaw.RotationDegrees = new Vector3(-50, 0, -6 + f * 4);
                leftArmNode.AddChild(leftClaw);
                var rightClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = darkFurMat };
                rightClaw.Position = new Vector3(fingerX, -0.35f * scale, 0.06f * scale);
                rightClaw.RotationDegrees = new Vector3(-50, 0, -6 + f * 4);
                rightArmNode.AddChild(rightClaw);
            }
        }

        // === WALKING STAFF - Gnarled wooden staff with crystal ===
        var staffNode = new Node3D();
        staffNode.Position = new Vector3(shoulderX + 0.12f * scale, 0.1f * scale, 0.08f * scale);
        staffNode.RotationDegrees = new Vector3(-5, 0, -8);
        parent.AddChild(staffNode);

        // Main staff shaft
        var staffShaft = new MeshInstance3D();
        var staffShaftMesh = new CylinderMesh
        {
            TopRadius = 0.018f * scale,
            BottomRadius = 0.022f * scale,
            Height = 1.3f * scale,
            RadialSegments = radialSegs / 3,
            Rings = rings
        };
        staffShaft.Mesh = staffShaftMesh;
        staffShaft.MaterialOverride = staffMat;
        staffShaft.Position = new Vector3(0, 0.65f * scale, 0);
        staffNode.AddChild(staffShaft);

        // Staff knots/gnarls (High LOD)
        if (lod == LODLevel.High)
        {
            var knotMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = 8, Rings = 4 };
            for (int k = 0; k < 3; k++)
            {
                var knot = new MeshInstance3D { Mesh = knotMesh, MaterialOverride = staffMat };
                knot.Position = new Vector3(0.01f * scale, 0.3f + k * 0.35f, 0);
                knot.Scale = new Vector3(1f, 0.6f, 1f);
                staffNode.AddChild(knot);
            }
        }

        // Staff head (gnarled top)
        var staffHead = new MeshInstance3D();
        var staffHeadMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        staffHead.Mesh = staffHeadMesh;
        staffHead.MaterialOverride = staffMat;
        staffHead.Position = new Vector3(0, 1.32f * scale, 0);
        staffHead.Scale = new Vector3(1f, 1.2f, 1f);
        staffNode.AddChild(staffHead);

        // Glowing crystal/gem at top
        var crystal = new MeshInstance3D();
        var crystalMesh = new PrismMesh { Size = new Vector3(0.05f * scale, 0.08f * scale, 0.05f * scale) };
        crystal.Mesh = crystalMesh;
        crystal.MaterialOverride = gemMat;
        crystal.Position = new Vector3(0, 1.4f * scale, 0);
        crystal.RotationDegrees = new Vector3(0, 45, 0);
        staffNode.AddChild(crystal);

        // === RAT TAIL - Long, segmented tail ===
        var tailNode = new Node3D();
        tailNode.Position = new Vector3(0, 0.25f * scale, -0.2f * scale);
        parent.AddChild(tailNode);
        limbs.Tail = tailNode;

        // Tail segments - getting thinner toward tip
        int tailSegments = lod == LODLevel.High ? 10 : 6;
        for (int t = 0; t < tailSegments; t++)
        {
            float segRadius = (0.025f - t * 0.002f) * scale;
            float segHeight = 0.1f * scale;
            float segY = -t * 0.08f * scale;
            float segZ = -t * 0.04f * scale;
            float segRotX = 8 + t * 4; // Curves downward

            var tailSeg = new MeshInstance3D();
            var tailSegMesh = new CapsuleMesh { Radius = Mathf.Max(segRadius, 0.005f * scale), Height = segHeight, RadialSegments = radialSegs / 3, Rings = 2 };
            tailSeg.Mesh = tailSegMesh;
            tailSeg.MaterialOverride = noseMat; // Pink hairless tail
            tailSeg.Position = new Vector3(0, segY, segZ);
            tailSeg.RotationDegrees = new Vector3(segRotX, 0, Mathf.Sin(t * 0.5f) * 5);
            tailNode.AddChild(tailSeg);
        }

        // Tail rings (detail, High LOD)
        if (lod == LODLevel.High)
        {
            var ringMesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.015f * scale, Height = 0.005f * scale, RadialSegments = 8, Rings = 1 };
            for (int r = 0; r < 8; r++)
            {
                var ring = new MeshInstance3D { Mesh = ringMesh, MaterialOverride = darkFurMat };
                ring.Position = new Vector3(0, -r * 0.08f * scale, -r * 0.04f * scale);
                ring.RotationDegrees = new Vector3(8 + r * 4, 0, 0);
                tailNode.AddChild(ring);
            }
        }

        // === LEGS (Hidden under robes, but needed for animation) ===
        float legRadius = 0.035f * scale;
        var legMesh = new CapsuleMesh { Radius = legRadius, Height = 0.2f * scale, RadialSegments = radialSegs / 2, Rings = 2 };
        var footMesh = new SphereMesh { Radius = 0.045f * scale, Height = 0.09f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };

        float hipY = 0.15f * scale;
        float hipX = 0.07f * scale;

        // Left leg
        var leftLegNode = new Node3D();
        leftLegNode.Position = new Vector3(-hipX, hipY, 0);
        parent.AddChild(leftLegNode);
        limbs.LeftLeg = leftLegNode;

        var leftLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        leftLeg.Position = new Vector3(0, -0.1f * scale, 0);
        leftLegNode.AddChild(leftLeg);
        var leftFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = noseMat }; // Pink paw
        leftFoot.Position = new Vector3(0, -0.22f * scale, 0.03f * scale);
        leftFoot.Scale = new Vector3(1.2f, 0.5f, 1.5f); // Rat paw
        leftLegNode.AddChild(leftFoot);

        // Right leg
        var rightLegNode = new Node3D();
        rightLegNode.Position = new Vector3(hipX, hipY, 0);
        parent.AddChild(rightLegNode);
        limbs.RightLeg = rightLegNode;

        var rightLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        rightLeg.Position = new Vector3(0, -0.1f * scale, 0);
        rightLegNode.AddChild(rightLeg);
        var rightFoot = new MeshInstance3D { Mesh = footMesh, MaterialOverride = noseMat };
        rightFoot.Position = new Vector3(0, -0.22f * scale, 0.03f * scale);
        rightFoot.Scale = new Vector3(1.2f, 0.5f, 1.5f);
        rightLegNode.AddChild(rightFoot);
    }
}

