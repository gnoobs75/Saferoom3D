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
        public Node3D? LeftArm { get; set; }
        public Node3D? RightArm { get; set; }
        public Node3D? LeftLeg { get; set; }
        public Node3D? RightLeg { get; set; }
        public Node3D? Weapon { get; set; }
        public Node3D? Torch { get; set; }
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

        // Pointy ears
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.05f, Height = 0.15f };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        leftEar.Position = new Vector3(-0.2f, 0.02f, 0);
        leftEar.RotationDegrees = new Vector3(0, 0, -60);
        headNode.AddChild(leftEar);

        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = skinMat };
        rightEar.Position = new Vector3(0.2f, 0.02f, 0);
        rightEar.RotationDegrees = new Vector3(0, 0, 60);
        headNode.AddChild(rightEar);

        // Hood (partial) - Height = 2*Radius for proper sphere
        var hood = new MeshInstance3D();
        var hoodMesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
        hood.Mesh = hoodMesh;
        hood.MaterialOverride = robeMat;
        hood.Position = new Vector3(0, 0.08f, -0.05f);
        hood.Scale = new Vector3(1f, 0.6f, 0.8f);
        headNode.AddChild(hood);

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

        // Glowing orb on staff
        var orb = new MeshInstance3D();
        var orbMesh = new SphereMesh { Radius = 0.1f * scale, Height = 0.2f * scale };
        orb.Mesh = orbMesh;
        orb.MaterialOverride = glowMat;
        orb.Position = new Vector3(0, 0.25f * scale, 0);
        staffNode.AddChild(orb);

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

        // Right arm (throwing arm)
        var rightArmNode = new Node3D();
        rightArmNode.Position = new Vector3(shoulderX, shoulderY, 0);
        parent.AddChild(rightArmNode);
        limbs.RightArm = rightArmNode;

        var rightArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = skinMat };
        rightArm.RotationDegrees = new Vector3(-20, 0, -35);
        rightArmNode.AddChild(rightArm);

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

        // Chest armor plate
        var chestPlate = new MeshInstance3D();
        var plateMesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.5f * scale, 0.08f * scale) };
        chestPlate.Mesh = plateMesh;
        chestPlate.MaterialOverride = armorMat;
        chestPlate.Position = new Vector3(0, 0.65f * scale, 0.2f * scale);
        parent.AddChild(chestPlate);

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
            st.SetNormal(Vector3.Up);
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
            st.SetNormal(Vector3.Up);
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

        // Back spines - scale ridges
        if (lodLevel < 2 && spineCount > 0)
        {
            var spineMesh = new CylinderMesh {
                TopRadius = 0.005f * scale,
                BottomRadius = 0.025f * scale,
                Height = 0.12f * scale
            };
            for (int i = 0; i < spineCount; i++)
            {
                var spine = new MeshInstance3D { Mesh = spineMesh, MaterialOverride = hornMat };
                float zPos = 0.35f - i * 0.11f;
                spine.Position = new Vector3(0, 0.92f * scale - i * 0.03f * scale, zPos * scale);
                spine.RotationDegrees = new Vector3(-15 - i * 3, 0, 0);
                parent.AddChild(spine);
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
        float headCounterRotation = -60f - (neckSegments * 12f); // Increased tilt-back for better forward gaze
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

        // Wing membrane
        var wingBoxMesh = new BoxMesh {
            Size = new Vector3(0.55f * scale * wingScale, 0.015f * scale, 0.38f * scale)
        };
        var leftWingMesh = new MeshInstance3D { Mesh = wingBoxMesh, MaterialOverride = wingMat };
        leftWingMesh.Position = new Vector3(-0.32f * scale * wingScale, 0, 0);
        leftWingMesh.RotationDegrees = new Vector3(0, 0, 28);
        leftWing.AddChild(leftWingMesh);

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
        Color capColor = colorOverride ?? new Color(0.6f, 0.4f, 0.5f);
        Color stemColor = new Color(0.9f, 0.85f, 0.75f);
        Color spotColor = Colors.White;
        Color gillColor = capColor.Darkened(0.3f);

        var capMat = new StandardMaterial3D { AlbedoColor = capColor, Roughness = 0.8f };
        var stemMat = new StandardMaterial3D { AlbedoColor = stemColor, Roughness = 0.9f };
        var spotMat = new StandardMaterial3D { AlbedoColor = spotColor };
        var gillMat = new StandardMaterial3D { AlbedoColor = gillColor, Roughness = 0.9f };
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
            AlbedoColor = new Color(0.8f, 0.9f, 0.5f),
            EmissionEnabled = true,
            Emission = new Color(0.6f, 0.8f, 0.3f) * 0.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        // === BODY GEOMETRY ===
        float stemTopRadius = 0.12f;
        float stemBottomRadius = 0.18f;
        float stemHeight = 0.5f;
        float stemCenterY = 0.25f;
        float stemTop = stemCenterY + stemHeight / 2f;

        // Cap (head) geometry
        float capRadius = 0.35f;
        float capHeight = 0.7f;
        float capScaleY = 0.5f;

        // Stem with texture rings
        var stem = new MeshInstance3D();
        var stemMesh = new CylinderMesh { TopRadius = stemTopRadius, BottomRadius = stemBottomRadius, Height = stemHeight };
        stem.Mesh = stemMesh;
        stem.MaterialOverride = stemMat;
        stem.Position = new Vector3(0, stemCenterY, 0);
        parent.AddChild(stem);

        // Stem rings for texture
        for (int i = 0; i < 3; i++)
        {
            var ring = new MeshInstance3D();
            var ringMesh = new TorusMesh { InnerRadius = 0.11f + i * 0.015f, OuterRadius = 0.14f + i * 0.01f };
            ring.Mesh = ringMesh;
            ring.MaterialOverride = new StandardMaterial3D { AlbedoColor = stemColor.Darkened(0.1f) };
            ring.Position = new Vector3(0, 0.12f + i * 0.12f, 0);
            ring.Scale = new Vector3(1, 0.3f, 1);
            parent.AddChild(ring);
        }

        // Stem bulge at base
        var bulge = new MeshInstance3D();
        var bulgeMesh = new SphereMesh { Radius = 0.15f, Height = 0.3f };
        bulge.Mesh = bulgeMesh;
        bulge.MaterialOverride = stemMat;
        bulge.Position = new Vector3(0, 0.05f, 0);
        bulge.Scale = new Vector3(1.2f, 0.6f, 1.2f);
        parent.AddChild(bulge);

        // === STEM-CAP JOINT ===
        float jointRadius = stemTopRadius;
        float jointOverlap = CalculateOverlap(jointRadius);
        var stemCapJoint = CreateJointMesh(stemMat, jointRadius);
        stemCapJoint.Position = new Vector3(0, stemTop - jointOverlap * 0.3f, 0);
        parent.AddChild(stemCapJoint);

        // Cap (head for animation) - positioned with overlap
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

        // Cap edge/lip
        var capEdge = new MeshInstance3D();
        var capEdgeMesh = new TorusMesh { InnerRadius = 0.28f, OuterRadius = 0.36f };
        capEdge.Mesh = capEdgeMesh;
        capEdge.MaterialOverride = new StandardMaterial3D { AlbedoColor = capColor.Darkened(0.15f), Roughness = 0.85f };
        capEdge.Position = new Vector3(0, -0.02f, 0);
        capEdge.Scale = new Vector3(1, 0.4f, 1);
        headNode.AddChild(capEdge);

        // Gills under cap
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.Pi * 2f / 16f;
            var gill = new MeshInstance3D();
            var gillMesh = new BoxMesh { Size = new Vector3(0.01f, 0.08f, 0.2f) };
            gill.Mesh = gillMesh;
            gill.MaterialOverride = gillMat;
            gill.Position = new Vector3(Mathf.Cos(angle) * 0.12f, -0.06f, Mathf.Sin(angle) * 0.12f);
            gill.RotationDegrees = new Vector3(0, -angle * 57.3f, 0);
            headNode.AddChild(gill);
        }

        // Spots on cap - varied sizes
        float[] spotSizes = { 0.06f, 0.045f, 0.07f, 0.05f, 0.055f, 0.04f, 0.065f, 0.038f };
        float[] spotAngles = { 0, 0.9f, 1.6f, 2.5f, 3.3f, 4.0f, 4.9f, 5.6f };
        float[] spotRadii = { 0.2f, 0.25f, 0.18f, 0.22f, 0.15f, 0.28f, 0.2f, 0.24f };
        for (int i = 0; i < 8; i++)
        {
            var spot = new MeshInstance3D();
            var spotMesh = new SphereMesh { Radius = spotSizes[i], Height = spotSizes[i] * 2f };
            spot.Mesh = spotMesh;
            spot.MaterialOverride = spotMat;
            spot.Position = new Vector3(
                Mathf.Cos(spotAngles[i]) * spotRadii[i],
                0.1f + (i % 2) * 0.04f,
                Mathf.Sin(spotAngles[i]) * spotRadii[i]
            );
            spot.Scale = new Vector3(1, 0.5f, 1);
            headNode.AddChild(spot);
        }

        // Top spot
        var topSpot = new MeshInstance3D();
        var topSpotMesh = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        topSpot.Mesh = topSpotMesh;
        topSpot.MaterialOverride = spotMat;
        topSpot.Position = new Vector3(0, 0.2f, 0);
        topSpot.Scale = new Vector3(1, 0.4f, 1);
        headNode.AddChild(topSpot);

        // Eyes on stem - using standardized Cute proportions based on stem width
        // The stem top radius (0.12f) serves as reference for eye size
        float mushroomEyeRef = stemTopRadius * 2f; // Use stem diameter as reference
        var (mushEyeR, mushPupilR, _, mushEyeX, _, _) = CalculateEyeGeometry(mushroomEyeRef, EyeProportions.Cute);
        float mushEyeY = 0.38f;
        float mushEyeZ = 0.08f;
        float mushPupilZ = mushEyeZ + mushEyeR * 0.7f;

        var eyeMesh = new SphereMesh { Radius = mushEyeR, Height = mushEyeR * 2f, RadialSegments = 16, Rings = 12 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-mushEyeX, mushEyeY, mushEyeZ);
        parent.AddChild(leftEye);

        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(mushEyeX, mushEyeY, mushEyeZ);
        parent.AddChild(rightEye);

        // Pupils
        var pupilMesh = new SphereMesh { Radius = mushPupilR, Height = mushPupilR * 2f, RadialSegments = 12, Rings = 8 };
        var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-mushEyeX, mushEyeY, mushPupilZ);
        parent.AddChild(leftPupil);

        var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(mushEyeX, mushEyeY, mushPupilZ);
        parent.AddChild(rightPupil);

        // Mouth - Height = 2*Radius for proper sphere
        var mouth = new MeshInstance3D();
        var mouthMesh = new SphereMesh { Radius = 0.03f, Height = 0.06f };
        mouth.Mesh = mouthMesh;
        mouth.MaterialOverride = mouthMat;
        mouth.Position = new Vector3(0, 0.3f, 0.11f);
        mouth.Scale = new Vector3(1.5f, 0.6f, 0.8f);
        parent.AddChild(mouth);

        // Little arms
        var armMesh = new CapsuleMesh { Radius = 0.025f, Height = 0.12f };

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.12f, 0.28f, 0.02f);
        leftArmNode.RotationDegrees = new Vector3(0, 0, -45);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;
        var leftArm = new MeshInstance3D { Mesh = armMesh, MaterialOverride = stemMat };
        leftArmNode.AddChild(leftArm);

        // Left hand
        var handMesh = new SphereMesh { Radius = 0.02f, Height = 0.04f };
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

        // Little legs with feet
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

        // Spores floating around
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Pi * 2f / 5f + 0.5f;
            var spore = new MeshInstance3D();
            var sporeMesh = new SphereMesh { Radius = 0.015f, Height = 0.03f };
            spore.Mesh = sporeMesh;
            spore.MaterialOverride = sporeMat;
            spore.Position = new Vector3(
                Mathf.Cos(angle) * 0.4f,
                0.4f + i * 0.08f,
                Mathf.Sin(angle) * 0.4f
            );
            parent.AddChild(spore);
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
            Emission = new Color(0.2f, 1f, 0.4f) * 0.8f
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

        // Cape
        var cape = new MeshInstance3D();
        var capeMesh = new BoxMesh { Size = new Vector3(0.6f * scale, 0.8f * scale, 0.05f * scale) };
        cape.Mesh = capeMesh;
        cape.MaterialOverride = capeMat;
        cape.Position = new Vector3(0, 0.9f * scale, -0.15f * scale);
        parent.AddChild(cape);

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

        // Right hand holds a sword
        if (!isLeft)
        {
            var swordMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.7f, 0.7f, 0.75f),
                Metallic = 0.9f,
                Roughness = 0.3f
            };
            var sword = new MeshInstance3D();
            var swordMesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.6f * scale, 0.01f * scale) };
            sword.Mesh = swordMesh;
            sword.MaterialOverride = swordMat;
            sword.Position = new Vector3(0, -0.8f * scale, 0);
            armNode.AddChild(sword);
            limbs.Weapon = sword;
        }
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

        // Broken horn tip (optional dramatic flair)
        // Could add a jagged break to one of the horns
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

        // Crown of small spikes
        var spikeMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.02f * scale, Height = 0.1f * scale };
        for (int i = 0; i < 6; i++)
        {
            var spike = new MeshInstance3D { Mesh = spikeMesh, MaterialOverride = markingMat };
            float angle = i * Mathf.Pi * 2f / 6f;
            spike.Position = new Vector3(Mathf.Cos(angle) * 0.15f * scale, 0.18f * scale, Mathf.Sin(angle) * 0.15f * scale);
            headNode.AddChild(spike);
        }

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

        // 8 long legs
        var legMesh = new CylinderMesh { TopRadius = 0.025f * scale, BottomRadius = 0.02f * scale, Height = 0.45f * scale };
        float[] legAngles = { 25f, 55f, 125f, 155f };

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

                // Lower leg segment
                var lowerLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = bodyMat };
                lowerLeg.Position = new Vector3(side * 0.42f * scale, -0.15f * scale, 0);
                lowerLeg.RotationDegrees = new Vector3(0, 0, side * -30);
                legNode.AddChild(lowerLeg);

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
    /// </summary>
    private static void CreateBadlamaMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride)
    {
        // Llama colors - brownish fur with cream/white markings
        Color furColor = skinColorOverride ?? new Color(0.55f, 0.4f, 0.25f);
        Color lightFur = furColor.Lightened(0.35f);
        Color darkFur = furColor.Darkened(0.2f);
        Color hoofColor = new Color(0.2f, 0.15f, 0.1f);
        Color eyeColor = new Color(0.95f, 0.4f, 0.1f); // Fiery orange eyes
        Color noseColor = new Color(0.18f, 0.12f, 0.1f);
        Color teethColor = new Color(0.95f, 0.92f, 0.85f);

        var furMat = new StandardMaterial3D { AlbedoColor = furColor, Roughness = 0.95f };
        var lightFurMat = new StandardMaterial3D { AlbedoColor = lightFur, Roughness = 0.9f };
        var darkFurMat = new StandardMaterial3D { AlbedoColor = darkFur, Roughness = 0.95f };
        var hoofMat = new StandardMaterial3D { AlbedoColor = hoofColor, Roughness = 0.7f };
        var noseMat = new StandardMaterial3D { AlbedoColor = noseColor, Roughness = 0.6f };
        var teethMat = new StandardMaterial3D { AlbedoColor = teethColor, Roughness = 0.5f };
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
        float bodyLength = 0.85f;  // This becomes Z-length when rotated
        float bodyCenterY = 0.7f;
        float bodyFrontZ = bodyLength / 2f;  // Front of body in Z
        float bodyTopY = bodyCenterY + bodyRadius;  // Top of body

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

        // Fluffy neck wool - slightly larger, same position/rotation for seamless look
        var neckWool = new MeshInstance3D();
        var neckWoolMesh = new CylinderMesh { TopRadius = neckTopRadius + 0.02f, BottomRadius = neckBottomRadius + 0.02f, Height = neckLength * 0.85f };
        neckWool.Mesh = neckWoolMesh;
        neckWool.MaterialOverride = lightFurMat;
        neckWool.Position = new Vector3(0, neckCenterY - 0.02f, neckCenterZ);
        neckWool.RotationDegrees = new Vector3(neckAngle, 0, 0);
        parent.AddChild(neckWool);

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

        // Snout/muzzle (llamas have elongated faces)
        var snout = new MeshInstance3D();
        var snoutMesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.06f, Height = 0.12f };
        snout.Mesh = snoutMesh;
        snout.MaterialOverride = lightFurMat;
        snout.Position = new Vector3(0, -0.03f, 0.16f);
        snout.RotationDegrees = new Vector3(-80, 0, 0);
        headNode.AddChild(snout);

        // Upper lip (slightly split like llama)
        var lipMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        var upperLip = new MeshInstance3D { Mesh = lipMesh, MaterialOverride = lightFurMat };
        upperLip.Position = new Vector3(0, -0.08f, 0.22f);
        upperLip.Scale = new Vector3(1.2f, 0.7f, 0.8f);
        headNode.AddChild(upperLip);

        // Nose
        var nose = new MeshInstance3D();
        var noseMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = noseMat;
        nose.Position = new Vector3(0, -0.06f, 0.25f);
        headNode.AddChild(nose);

        // Nostrils (smoke may come from here!)
        var nostrilMesh = new SphereMesh { Radius = 0.008f, Height = 0.016f };
        var nostrilMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.Black,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f) * 0.3f // Slight orange glow
        };
        var leftNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
        leftNostril.Position = new Vector3(-0.015f, -0.065f, 0.26f);
        headNode.AddChild(leftNostril);
        var rightNostril = new MeshInstance3D { Mesh = nostrilMesh, MaterialOverride = nostrilMat };
        rightNostril.Position = new Vector3(0.015f, -0.065f, 0.26f);
        headNode.AddChild(rightNostril);

        // Big front teeth (for biting!)
        var toothMesh = new BoxMesh { Size = new Vector3(0.015f, 0.025f, 0.01f) };
        var leftTooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = teethMat };
        leftTooth.Position = new Vector3(-0.02f, -0.1f, 0.2f);
        headNode.AddChild(leftTooth);
        var rightTooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = teethMat };
        rightTooth.Position = new Vector3(0.02f, -0.1f, 0.2f);
        headNode.AddChild(rightTooth);

        // Fiery eyes with pupils - Goblin-style explicit positioning for visibility
        // Llama head is elongated (CapsuleMesh rotated -70°), so eyes are on the sides
        var badlamaEyeMesh = new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 16, Rings = 12 };
        var badlamaPupilMesh = new SphereMesh { Radius = 0.012f, Height = 0.024f, RadialSegments = 12, Rings = 8 };

        // Left eye - positioned on side of elongated head
        var leftEye = new MeshInstance3D { Mesh = badlamaEyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.06f, 0.03f, 0.08f);
        headNode.AddChild(leftEye);
        var leftPupil = new MeshInstance3D { Mesh = badlamaPupilMesh, MaterialOverride = pupilMat };
        leftPupil.Position = new Vector3(-0.07f, 0.03f, 0.095f);
        headNode.AddChild(leftPupil);

        // Right eye
        var rightEye = new MeshInstance3D { Mesh = badlamaEyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.06f, 0.03f, 0.08f);
        headNode.AddChild(rightEye);
        var rightPupil = new MeshInstance3D { Mesh = badlamaPupilMesh, MaterialOverride = pupilMat };
        rightPupil.Position = new Vector3(0.07f, 0.03f, 0.095f);
        headNode.AddChild(rightPupil);

        // Long pointy ears (llama ears)
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.03f, Height = 0.12f };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        leftEar.Position = new Vector3(-0.08f, 0.12f, -0.02f);
        leftEar.RotationDegrees = new Vector3(-15, 0, -25);
        headNode.AddChild(leftEar);
        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        rightEar.Position = new Vector3(0.08f, 0.12f, -0.02f);
        rightEar.RotationDegrees = new Vector3(-15, 0, 25);
        headNode.AddChild(rightEar);

        // Inner ear (pink)
        var innerEarMesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.018f, Height = 0.08f };
        var innerEarMat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.6f, 0.6f), Roughness = 0.9f };
        var leftInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = innerEarMat };
        leftInnerEar.Position = new Vector3(-0.08f, 0.11f, 0f);
        leftInnerEar.RotationDegrees = new Vector3(-15, 0, -25);
        headNode.AddChild(leftInnerEar);
        var rightInnerEar = new MeshInstance3D { Mesh = innerEarMesh, MaterialOverride = innerEarMat };
        rightInnerEar.Position = new Vector3(0.08f, 0.11f, 0f);
        rightInnerEar.RotationDegrees = new Vector3(-15, 0, 25);
        headNode.AddChild(rightInnerEar);

        // Fluffy forelock (tuft of fur on head)
        var forelockMesh = new SphereMesh { Radius = 0.06f, Height = 0.12f };
        var forelock = new MeshInstance3D { Mesh = forelockMesh, MaterialOverride = lightFurMat };
        forelock.Position = new Vector3(0, 0.12f, 0.02f);
        forelock.Scale = new Vector3(0.8f, 1f, 0.7f);
        headNode.AddChild(forelock);

        // Tail (llamas have short fluffy tails)
        var tailNode = new Node3D();
        tailNode.Position = new Vector3(0, 0.75f, -0.4f);
        parent.AddChild(tailNode);

        var tailMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.05f, Height = 0.15f };
        var tail = new MeshInstance3D { Mesh = tailMesh, MaterialOverride = furMat };
        tail.RotationDegrees = new Vector3(30, 0, 0);
        tailNode.AddChild(tail);

        var tailFluff = new MeshInstance3D();
        var tailFluffMesh = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        tailFluff.Mesh = tailFluffMesh;
        tailFluff.MaterialOverride = lightFurMat;
        tailFluff.Position = new Vector3(0, 0.12f, 0);
        tailNode.AddChild(tailFluff);

        // 4 legs (llamas have long spindly legs)
        CreateBadlamaLeg(parent, limbs, furMat, hoofMat, true, true);   // Front left
        CreateBadlamaLeg(parent, limbs, furMat, hoofMat, false, true);  // Front right
        CreateBadlamaLeg(parent, limbs, furMat, hoofMat, true, false);  // Back left
        CreateBadlamaLeg(parent, limbs, furMat, hoofMat, false, false); // Back right
    }

    private static void CreateBadlamaLeg(Node3D parent, LimbNodes limbs, StandardMaterial3D furMat, StandardMaterial3D hoofMat, bool isLeft, bool isFront)
    {
        float xOffset = isLeft ? -0.18f : 0.18f;
        float zOffset = isFront ? 0.25f : -0.28f;

        var legNode = new Node3D();
        legNode.Position = new Vector3(xOffset, 0.55f, zOffset);
        parent.AddChild(legNode);

        // Upper leg
        var upperLeg = new MeshInstance3D();
        var upperLegMesh = new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.04f, Height = 0.28f };
        upperLeg.Mesh = upperLegMesh;
        upperLeg.MaterialOverride = furMat;
        upperLeg.Position = new Vector3(0, -0.14f, 0);
        legNode.AddChild(upperLeg);

        // Lower leg (thinner)
        var lowerLeg = new MeshInstance3D();
        var lowerLegMesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.025f, Height = 0.25f };
        lowerLeg.Mesh = lowerLegMesh;
        lowerLeg.MaterialOverride = furMat;
        lowerLeg.Position = new Vector3(0, -0.4f, 0);
        legNode.AddChild(lowerLeg);

        // Hoof
        var hoof = new MeshInstance3D();
        var hoofMesh = new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = 0.06f };
        hoof.Mesh = hoofMesh;
        hoof.MaterialOverride = hoofMat;
        hoof.Position = new Vector3(0, -0.55f, 0);
        legNode.AddChild(hoof);

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

        // Crown/spikes on top
        var crownMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.7f, 0.2f), // Gold
            Metallic = 0.8f,
            Roughness = 0.3f,
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.7f, 0.2f) * 0.2f
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
            spike.Position = new Vector3(
                Mathf.Cos(angle) * crownRadius,
                0.75f * bossScale,
                Mathf.Sin(angle) * crownRadius
            );
            spike.RotationDegrees = new Vector3(-10, 0, 0);
            spike.Scale = new Vector3(1f, 1f + (i % 2) * 0.3f, 1f);
            parent.AddChild(spike);
        }

        // Crown band
        var crownBand = new MeshInstance3D();
        var bandMesh = new CylinderMesh { TopRadius = 0.38f * bossScale, BottomRadius = 0.42f * bossScale, Height = 0.08f };
        crownBand.Mesh = bandMesh;
        crownBand.MaterialOverride = crownMat;
        crownBand.Position = new Vector3(0, 0.68f * bossScale, 0);
        parent.AddChild(crownBand);

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

        // Glowing eyes (slits)
        var eyeMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.02f * scale, 0.02f * scale) };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        leftEye.Position = new Vector3(-0.05f * scale, 0.03f * scale, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        rightEye.Position = new Vector3(0.05f * scale, 0.03f * scale, 0.1f * scale);
        headNode.AddChild(rightEye);

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

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-shoulderX, shoulderY, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftUpperArm = new MeshInstance3D();
        leftUpperArm.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.05f * scale, Height = 0.25f * scale, RadialSegments = radialSegs };
        leftUpperArm.MaterialOverride = metalMat;
        leftUpperArm.Position = new Vector3(0, -0.12f * scale, 0);
        leftArmNode.AddChild(leftUpperArm);

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

        // Weapon arm blade
        var bladeMesh = new BoxMesh { Size = new Vector3(0.02f * scale, 0.25f * scale, 0.06f * scale) };
        var blade = new MeshInstance3D { Mesh = bladeMesh, MaterialOverride = metalMat };
        blade.Position = new Vector3(0, -0.3f * scale, 0.08f * scale);
        rightArmNode.AddChild(blade);
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

        // Piercing white eyes - Height = 2*Radius for proper sphere
        var eyeMesh = new SphereMesh { Radius = 0.03f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        leftEye.Position = new Vector3(-0.04f * scale, 0.02f * scale, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = glowMat };
        rightEye.Position = new Vector3(0.04f * scale, 0.02f * scale, 0.1f * scale);
        headNode.AddChild(rightEye);

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

        // Stitches across body (visual detail)
        var stitchMesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.015f * scale, 0.02f * scale) };
        for (int i = 0; i < 4; i++)
        {
            var stitch = new MeshInstance3D { Mesh = stitchMesh, MaterialOverride = stitchMat };
            stitch.Position = new Vector3(0.05f * scale * (i % 2 == 0 ? 1 : -1), 0.4f * scale + i * 0.12f * scale, 0.35f * scale);
            stitch.RotationDegrees = new Vector3(0, 0, (i % 2 == 0 ? 10 : -10));
            parent.AddChild(stitch);
        }

        // Exposed metal bolts/spikes in shoulders
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

        // Sunken glowing eyes
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.9f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.6f, 0.8f, 0.2f)
        };
        var eyeMesh = new SphereMesh { Radius = 0.025f * scale, Height = 0.05f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.05f * scale, 0, 0.1f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.05f * scale, 0, 0.1f * scale);
        headNode.AddChild(rightEye);

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

        // Visor slit with glow
        var visor = new MeshInstance3D();
        visor.Mesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.03f * scale, 0.02f * scale) };
        visor.MaterialOverride = glowMat;
        visor.Position = new Vector3(0, 0, 0.11f * scale);
        headNode.AddChild(visor);

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

        var leftArmNode = new Node3D();
        leftArmNode.Position = new Vector3(-0.28f * scale, 0.75f * scale, 0);
        parent.AddChild(leftArmNode);
        limbs.LeftArm = leftArmNode;

        var leftGauntlet = new MeshInstance3D();
        leftGauntlet.Mesh = new BoxMesh { Size = new Vector3(0.1f * scale, 0.35f * scale, 0.1f * scale) };
        leftGauntlet.MaterialOverride = armorMat;
        leftGauntlet.Position = new Vector3(0, -0.17f * scale, 0);
        leftArmNode.AddChild(leftGauntlet);

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

        int radialSegs = GetRadialSegments(lod);
        int rings = GetRings(lod);

        var bodyMat = new StandardMaterial3D { AlbedoColor = bodyColor, Metallic = 0.6f, Roughness = 0.4f };
        var lensMat = new StandardMaterial3D { AlbedoColor = lensColor, Metallic = 0.9f, Roughness = 0.1f };
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

        // Camera lens
        var lens = new MeshInstance3D();
        lens.Mesh = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.1f * scale, Height = 0.08f * scale, RadialSegments = radialSegs };
        lens.MaterialOverride = lensMat;
        lens.Position = new Vector3(0, floatY, 0.15f * scale);
        lens.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(lens);

        // Recording light - Height = 2*Radius for proper sphere
        var recLight = new MeshInstance3D();
        recLight.Mesh = new SphereMesh { Radius = 0.02f * scale, Height = 0.04f * scale, RadialSegments = radialSegs / 2, Rings = rings / 2 };
        recLight.MaterialOverride = lightMat;
        recLight.Position = new Vector3(0.1f * scale, floatY + 0.1f * scale, 0.1f * scale);
        parent.AddChild(recLight);

        // Head node (the whole drone is the "head" for animation)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, floatY, 0);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        // Propeller arms (4)
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4f + Mathf.Tau / 8f;
            float armX = Mathf.Cos(angle) * 0.18f * scale;
            float armZ = Mathf.Sin(angle) * 0.18f * scale;

            var arm = new MeshInstance3D();
            arm.Mesh = new BoxMesh { Size = new Vector3(0.04f * scale, 0.02f * scale, 0.15f * scale) };
            arm.MaterialOverride = bodyMat;
            arm.Position = new Vector3(armX, floatY, armZ);
            // Calculate rotation manually (LookAt requires node to be in tree)
            var armTarget = new Vector3(0, floatY, 0);
            var armDir = (armTarget - arm.Position).Normalized();
            if (armDir.LengthSquared() > 0.001f)
            {
                float armYaw = Mathf.Atan2(armDir.X, armDir.Z);
                arm.Rotation = new Vector3(0, armYaw, 0);
            }
            parent.AddChild(arm);

            // Propeller disc
            var prop = new MeshInstance3D();
            prop.Mesh = new CylinderMesh { TopRadius = 0.06f * scale, BottomRadius = 0.06f * scale, Height = 0.01f * scale };
            prop.MaterialOverride = bodyMat;
            prop.Position = new Vector3(armX * 1.5f, floatY + 0.03f * scale, armZ * 1.5f);
            parent.AddChild(prop);

            // Propeller blades
            var bladeMesh = new BoxMesh { Size = new Vector3(0.12f * scale, 0.005f * scale, 0.015f * scale) };
            var blade = new MeshInstance3D { Mesh = bladeMesh, MaterialOverride = bodyMat };
            blade.Position = new Vector3(armX * 1.5f, floatY + 0.04f * scale, armZ * 1.5f);
            blade.RotationDegrees = new Vector3(0, i * 45, 0);
            parent.AddChild(blade);
        }

        // Secondary recording light (green status)
        var statusLight = new MeshInstance3D();
        statusLight.Mesh = new CylinderMesh { TopRadius = 0.015f * scale, BottomRadius = 0.015f * scale, Height = 0.008f * scale, RadialSegments = radialSegs / 2 };
        var greenMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 1f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 1f, 0.3f),
            EmissionEnergyMultiplier = 1.5f
        };
        statusLight.MaterialOverride = greenMat;
        statusLight.Position = new Vector3(-0.1f * scale, floatY + 0.1f * scale, 0.1f * scale);
        parent.AddChild(statusLight);

        // Lens glass reflection ring
        var lensRing = new MeshInstance3D();
        var lensRingMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.5f), Metallic = 1f, Roughness = 0.1f };
        lensRing.Mesh = new CylinderMesh { TopRadius = 0.095f * scale, BottomRadius = 0.095f * scale, Height = 0.015f * scale, RadialSegments = radialSegs };
        lensRing.MaterialOverride = lensRingMat;
        lensRing.Position = new Vector3(0, floatY, 0.19f * scale);
        lensRing.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(lensRing);

        // Antenna on top
        var droneAntenna = new MeshInstance3D();
        droneAntenna.Mesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.01f * scale, Height = 0.06f * scale, RadialSegments = radialSegs / 2 };
        droneAntenna.MaterialOverride = bodyMat;
        droneAntenna.Position = new Vector3(0, floatY + 0.22f * scale, 0);
        parent.AddChild(droneAntenna);
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
    /// Mimic - Disguised chest that attacks
    /// Chest with teeth and tongue
    /// </summary>
    private static void CreateMimicMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 1.0f;
        Color woodColor = colorOverride ?? new Color(0.45f, 0.3f, 0.2f); // Wood
        Color metalColor = new Color(0.6f, 0.55f, 0.3f); // Gold trim
        Color tongueColor = new Color(0.8f, 0.3f, 0.4f); // Red tongue

        var woodMat = new StandardMaterial3D { AlbedoColor = woodColor, Roughness = 0.8f };
        var metalMat = new StandardMaterial3D { AlbedoColor = metalColor, Metallic = 0.7f, Roughness = 0.4f };
        var tongueMat = new StandardMaterial3D { AlbedoColor = tongueColor, Roughness = 0.6f };
        var toothMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.9f, 0.85f), Roughness = 0.5f };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.6f, 0.1f)
        };

        // Chest body (bottom half)
        var body = new MeshInstance3D();
        body.Mesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.25f * scale, 0.35f * scale) };
        body.MaterialOverride = woodMat;
        body.Position = new Vector3(0, 0.125f * scale, 0);
        parent.AddChild(body);

        // Metal bands
        var bandMesh = new BoxMesh { Size = new Vector3(0.52f * scale, 0.03f * scale, 0.37f * scale) };
        var band1 = new MeshInstance3D { Mesh = bandMesh, MaterialOverride = metalMat };
        band1.Position = new Vector3(0, 0.08f * scale, 0);
        parent.AddChild(band1);
        var band2 = new MeshInstance3D { Mesh = bandMesh, MaterialOverride = metalMat };
        band2.Position = new Vector3(0, 0.2f * scale, 0);
        parent.AddChild(band2);

        // Lid (head node - opens/closes)
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.25f * scale, -0.15f * scale); // Pivot at back
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var lid = new MeshInstance3D();
        lid.Mesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.15f * scale, 0.35f * scale) };
        lid.MaterialOverride = woodMat;
        lid.Position = new Vector3(0, 0.075f * scale, 0.15f * scale);
        headNode.AddChild(lid);

        // Teeth along opening edge
        var toothMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.02f * scale, Height = 0.06f * scale };
        for (int i = 0; i < 8; i++)
        {
            float toothX = -0.2f * scale + i * 0.058f * scale;

            // Top teeth (on lid)
            var topTooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
            topTooth.Position = new Vector3(toothX, 0, 0.32f * scale);
            topTooth.RotationDegrees = new Vector3(180, 0, 0);
            headNode.AddChild(topTooth);

            // Bottom teeth
            var bottomTooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
            bottomTooth.Position = new Vector3(toothX, 0.25f * scale, 0.15f * scale);
            parent.AddChild(bottomTooth);
        }

        // Tongue
        var tongue = new MeshInstance3D();
        tongue.Mesh = new BoxMesh { Size = new Vector3(0.15f * scale, 0.03f * scale, 0.2f * scale) };
        tongue.MaterialOverride = tongueMat;
        tongue.Position = new Vector3(0, 0.15f * scale, 0.25f * scale);
        parent.AddChild(tongue);

        // Eyes inside - Height must equal 2*Radius for proper spheres
        var eyeMesh = new SphereMesh { Radius = 0.04f * scale, Height = 0.08f * scale, RadialSegments = 16, Rings = 12 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.1f * scale, 0.2f * scale, 0.1f * scale);
        parent.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.1f * scale, 0.2f * scale, 0.1f * scale);
        parent.AddChild(rightEye);

        // Clawed feet/legs (4 total)
        var legMat = new StandardMaterial3D { AlbedoColor = woodColor.Darkened(0.2f), Roughness = 0.7f };
        var legMesh = new CylinderMesh { TopRadius = 0.025f * scale, BottomRadius = 0.02f * scale, Height = 0.1f * scale };
        var clawMesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.015f * scale, Height = 0.04f * scale };

        var frontRightLeg = new Node3D();
        frontRightLeg.Position = new Vector3(0.2f * scale, 0.05f * scale, 0.12f * scale);
        parent.AddChild(frontRightLeg);
        limbs.RightArm = frontRightLeg;
        var frLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        frLeg.Position = new Vector3(0, -0.05f * scale, 0);
        frontRightLeg.AddChild(frLeg);
        var frClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = toothMat };
        frClaw.Position = new Vector3(0, -0.12f * scale, 0.02f * scale);
        frClaw.RotationDegrees = new Vector3(30, 0, 0);
        frontRightLeg.AddChild(frClaw);

        var frontLeftLeg = new Node3D();
        frontLeftLeg.Position = new Vector3(-0.2f * scale, 0.05f * scale, 0.12f * scale);
        parent.AddChild(frontLeftLeg);
        limbs.LeftArm = frontLeftLeg;
        var flLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        flLeg.Position = new Vector3(0, -0.05f * scale, 0);
        frontLeftLeg.AddChild(flLeg);
        var flClaw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = toothMat };
        flClaw.Position = new Vector3(0, -0.12f * scale, 0.02f * scale);
        flClaw.RotationDegrees = new Vector3(30, 0, 0);
        frontLeftLeg.AddChild(flClaw);

        var rearRightLeg = new Node3D();
        rearRightLeg.Position = new Vector3(0.2f * scale, 0.05f * scale, -0.12f * scale);
        parent.AddChild(rearRightLeg);
        limbs.RightLeg = rearRightLeg;
        var rrLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        rrLeg.Position = new Vector3(0, -0.05f * scale, 0);
        rearRightLeg.AddChild(rrLeg);

        var rearLeftLeg = new Node3D();
        rearLeftLeg.Position = new Vector3(-0.2f * scale, 0.05f * scale, -0.12f * scale);
        parent.AddChild(rearLeftLeg);
        limbs.LeftLeg = rearLeftLeg;
        var rlLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = legMat };
        rlLeg.Position = new Vector3(0, -0.05f * scale, 0);
        rearLeftLeg.AddChild(rlLeg);

        // Lock (fake)
        var lockMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.08f * scale, 0.02f * scale) };
        var lockMat = new StandardMaterial3D { AlbedoColor = metalColor.Darkened(0.1f), Metallic = 0.8f };
        var fakeLock = new MeshInstance3D { Mesh = lockMesh, MaterialOverride = lockMat };
        fakeLock.Position = new Vector3(0, 0.12f * scale, 0.185f * scale);
        parent.AddChild(fakeLock);

        // Drool drips
        var droolMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.85f, 0.8f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var droolMesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.003f * scale, Height = 0.08f * scale };
        for (int i = 0; i < 3; i++)
        {
            var drool = new MeshInstance3D { Mesh = droolMesh, MaterialOverride = droolMat };
            drool.Position = new Vector3((i - 1) * 0.08f * scale, 0.2f * scale, 0.16f * scale);
            parent.AddChild(drool);
        }

        // Gold coins inside (lure)
        var coinMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.3f), Metallic = 0.9f };
        var coinMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.005f * scale };
        for (int i = 0; i < 5; i++)
        {
            var coin = new MeshInstance3D { Mesh = coinMesh, MaterialOverride = coinMat };
            coin.Position = new Vector3(
                (GD.Randf() - 0.5f) * 0.2f * scale,
                0.08f * scale,
                (GD.Randf() - 0.5f) * 0.15f * scale
            );
            coin.RotationDegrees = new Vector3(GD.Randf() * 30, GD.Randf() * 360, GD.Randf() * 30);
            parent.AddChild(coin);
        }
    }

    /// <summary>
    /// Dungeon Rat - Small pack creature
    /// Rat-like quadruped
    /// </summary>
    private static void CreateDungeonRatMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 0.5f; // Small enemy
        Color furColor = colorOverride ?? new Color(0.35f, 0.3f, 0.28f); // Gray-brown

        var furMat = new StandardMaterial3D { AlbedoColor = furColor, Roughness = 0.85f };
        var noseMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.4f, 0.4f) };
        var eyeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.1f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.1f, 0.1f) * 0.3f
        };

        // Body (elongated)
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.1f * scale, BottomRadius = 0.12f * scale, Height = 0.3f * scale };
        body.MaterialOverride = furMat;
        body.Position = new Vector3(0, 0.12f * scale, 0);
        body.RotationDegrees = new Vector3(90, 0, 0);
        parent.AddChild(body);

        // Head node
        var headNode = new Node3D();
        headNode.Position = new Vector3(0, 0.12f * scale, 0.2f * scale);
        parent.AddChild(headNode);
        limbs.Head = headNode;

        var head = new MeshInstance3D();
        head.Mesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.08f * scale, Height = 0.12f * scale };
        head.MaterialOverride = furMat;
        head.RotationDegrees = new Vector3(90, 0, 0);
        headNode.AddChild(head);

        // Snout
        var snout = new MeshInstance3D();
        snout.Mesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.03f * scale, Height = 0.05f * scale };
        snout.MaterialOverride = noseMat;
        snout.Position = new Vector3(0, 0, 0.08f * scale);
        snout.RotationDegrees = new Vector3(90, 0, 0);
        headNode.AddChild(snout);

        // Eyes - must have Height = 2*Radius for proper sphere
        var eyeMesh = new SphereMesh { Radius = 0.015f * scale, Height = 0.03f * scale, RadialSegments = 12, Rings = 8 };
        var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        leftEye.Position = new Vector3(-0.03f * scale, 0.02f * scale, 0.04f * scale);
        headNode.AddChild(leftEye);
        var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
        rightEye.Position = new Vector3(0.03f * scale, 0.02f * scale, 0.04f * scale);
        headNode.AddChild(rightEye);

        // Ears
        var earMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.025f * scale, Height = 0.03f * scale };
        var leftEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        leftEar.Position = new Vector3(-0.04f * scale, 0.04f * scale, 0);
        headNode.AddChild(leftEar);
        var rightEar = new MeshInstance3D { Mesh = earMesh, MaterialOverride = furMat };
        rightEar.Position = new Vector3(0.04f * scale, 0.04f * scale, 0);
        headNode.AddChild(rightEar);

        // Legs (4 short legs)
        var legMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.015f * scale, Height = 0.1f * scale };
        float legY = 0.05f * scale;

        var frontRightLegNode = new Node3D();
        frontRightLegNode.Position = new Vector3(0.08f * scale, legY, 0.1f * scale);
        parent.AddChild(frontRightLegNode);
        limbs.RightArm = frontRightLegNode;
        var frLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        frLeg.Position = new Vector3(0, -0.05f * scale, 0);
        frontRightLegNode.AddChild(frLeg);

        var frontLeftLegNode = new Node3D();
        frontLeftLegNode.Position = new Vector3(-0.08f * scale, legY, 0.1f * scale);
        parent.AddChild(frontLeftLegNode);
        limbs.LeftArm = frontLeftLegNode;
        var flLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        flLeg.Position = new Vector3(0, -0.05f * scale, 0);
        frontLeftLegNode.AddChild(flLeg);

        var backRightLegNode = new Node3D();
        backRightLegNode.Position = new Vector3(0.08f * scale, legY, -0.1f * scale);
        parent.AddChild(backRightLegNode);
        limbs.RightLeg = backRightLegNode;
        var brLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        brLeg.Position = new Vector3(0, -0.05f * scale, 0);
        backRightLegNode.AddChild(brLeg);

        var backLeftLegNode = new Node3D();
        backLeftLegNode.Position = new Vector3(-0.08f * scale, legY, -0.1f * scale);
        parent.AddChild(backLeftLegNode);
        limbs.LeftLeg = backLeftLegNode;
        var blLeg = new MeshInstance3D { Mesh = legMesh, MaterialOverride = furMat };
        blLeg.Position = new Vector3(0, -0.05f * scale, 0);
        backLeftLegNode.AddChild(blLeg);

        // Tail
        var tail = new MeshInstance3D();
        tail.Mesh = new CylinderMesh { TopRadius = 0.01f * scale, BottomRadius = 0.02f * scale, Height = 0.2f * scale };
        tail.MaterialOverride = noseMat;
        tail.Position = new Vector3(0, 0.1f * scale, -0.2f * scale);
        tail.RotationDegrees = new Vector3(70, 0, 0);
        parent.AddChild(tail);

        // Whiskers
        var whiskerMesh = new CylinderMesh { TopRadius = 0.001f * scale, BottomRadius = 0.002f * scale, Height = 0.04f * scale };
        var whiskerMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.85f) };
        for (int side = 0; side < 2; side++)
        {
            for (int i = 0; i < 3; i++)
            {
                var whisker = new MeshInstance3D { Mesh = whiskerMesh, MaterialOverride = whiskerMat };
                float sideX = side == 0 ? -1f : 1f;
                whisker.Position = new Vector3(sideX * 0.025f * scale, -0.01f * scale, 0.065f * scale);
                whisker.RotationDegrees = new Vector3(0, 0, sideX * (70 + i * 15));
                headNode.AddChild(whisker);
            }
        }

        // Clawed feet
        var clawMesh = new CylinderMesh { TopRadius = 0.002f * scale, BottomRadius = 0.006f * scale, Height = 0.02f * scale };
        var clawMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.18f, 0.15f) };
        foreach (var legNode in new[] { frontRightLegNode, frontLeftLegNode, backRightLegNode, backLeftLegNode })
        {
            for (int c = 0; c < 3; c++)
            {
                var claw = new MeshInstance3D { Mesh = clawMesh, MaterialOverride = clawMat };
                claw.Position = new Vector3((c - 1) * 0.01f * scale, -0.1f * scale, 0.01f * scale);
                claw.RotationDegrees = new Vector3(30, 0, (c - 1) * 15);
                legNode.AddChild(claw);
            }
        }

        // Dirty fur patches (different color)
        var dirtyFurMat = new StandardMaterial3D { AlbedoColor = furColor.Darkened(0.2f), Roughness = 0.9f };
        var patchMesh = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.03f * scale, Height = 0.02f * scale };
        for (int i = 0; i < 3; i++)
        {
            var patch = new MeshInstance3D { Mesh = patchMesh, MaterialOverride = dirtyFurMat };
            patch.Position = new Vector3((i - 1) * 0.05f * scale, 0.13f * scale, (i % 2) * 0.05f * scale);
            patch.RotationDegrees = new Vector3(GD.Randf() * 30, GD.Randf() * 360, GD.Randf() * 30);
            parent.AddChild(patch);
        }

        // Teeth (visible when mouth open in aggro)
        var toothMesh = new CylinderMesh { TopRadius = 0.002f * scale, BottomRadius = 0.004f * scale, Height = 0.01f * scale };
        var toothMat = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.9f, 0.8f) };
        for (int i = 0; i < 4; i++)
        {
            var tooth = new MeshInstance3D { Mesh = toothMesh, MaterialOverride = toothMat };
            tooth.Position = new Vector3((i - 1.5f) * 0.008f * scale, -0.02f * scale, 0.06f * scale);
            tooth.RotationDegrees = new Vector3(180, 0, 0);
            headNode.AddChild(tooth);
        }
    }

    // ========================================================================
    // NEW DCC-THEMED BOSSES - 5 NEW TYPES
    // ========================================================================

    /// <summary>
    /// The Butcher - Massive orc with meat cleaver
    /// </summary>
    private static void CreateButcherMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 2.0f; // Boss scale
        Color skinColor = colorOverride ?? new Color(0.5f, 0.25f, 0.2f); // Dark red skin
        Color apronColor = new Color(0.9f, 0.85f, 0.8f); // Bloodstained white

        var skinMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.85f };
        var apronMat = new StandardMaterial3D { AlbedoColor = apronColor, Roughness = 0.7f };
        var metalMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.5f, 0.55f), Metallic = 0.8f, Roughness = 0.4f };

        // Massive body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.35f * scale, BottomRadius = 0.4f * scale, Height = 0.6f * scale };
        body.MaterialOverride = skinMat;
        body.Position = new Vector3(0, 0.6f * scale, 0);
        parent.AddChild(body);

        // Apron
        var apron = new MeshInstance3D();
        apron.Mesh = new BoxMesh { Size = new Vector3(0.5f * scale, 0.5f * scale, 0.05f * scale) };
        apron.MaterialOverride = apronMat;
        apron.Position = new Vector3(0, 0.5f * scale, 0.25f * scale);
        parent.AddChild(apron);

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

        // Meat hook on belt
        var hookMat = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.42f), Metallic = 0.9f, Roughness = 0.3f };
        var hookMesh = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.02f * scale, Height = 0.15f * scale };
        var hook = new MeshInstance3D { Mesh = hookMesh, MaterialOverride = hookMat };
        hook.Position = new Vector3(-0.35f * scale, 0.35f * scale, 0.2f * scale);
        hook.RotationDegrees = new Vector3(0, 0, 30);
        parent.AddChild(hook);
        var hookTip = new MeshInstance3D();
        hookTip.Mesh = new CylinderMesh { TopRadius = 0.005f * scale, BottomRadius = 0.02f * scale, Height = 0.08f * scale };
        hookTip.MaterialOverride = hookMat;
        hookTip.Position = new Vector3(-0.38f * scale, 0.28f * scale, 0.2f * scale);
        hookTip.RotationDegrees = new Vector3(45, 0, 60);
        parent.AddChild(hookTip);

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
    /// Syndicate Enforcer - Combat robot boss
    /// </summary>
    private static void CreateSyndicateEnforcerMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
    {
        float scale = 2.2f;
        Color bodyColor = colorOverride ?? new Color(0.35f, 0.38f, 0.42f);
        Color lightColor = new Color(0.3f, 0.5f, 0.9f);

        var metalMat = new StandardMaterial3D { AlbedoColor = bodyColor, Metallic = 0.85f, Roughness = 0.3f };
        var darkMat = new StandardMaterial3D { AlbedoColor = bodyColor.Darkened(0.3f), Metallic = 0.8f, Roughness = 0.4f };
        var lightMat = new StandardMaterial3D
        {
            AlbedoColor = lightColor,
            EmissionEnabled = true,
            Emission = lightColor,
            EmissionEnergyMultiplier = 2f
        };

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
    /// Hive Mother - Insect queen boss
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

        // Large abdomen - Height = 2*Radius for proper sphere
        var abdomen = new MeshInstance3D();
        abdomen.Mesh = new SphereMesh { Radius = 0.4f * scale, Height = 0.8f * scale };
        abdomen.MaterialOverride = chitinMat;
        abdomen.Position = new Vector3(0, 0.5f * scale, -0.3f * scale);
        abdomen.Scale = new Vector3(1f, 0.8f, 1.4f);
        parent.AddChild(abdomen);

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
    /// Architect's Favorite - Angelic horror boss
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
    /// Mordecai's Shadow - Corrupted sponsor champion boss
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
    /// Mordecai the Traitor - Cunning dual-wielding rogue
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
    /// Princess Donut - Cat boss that's fast and agile
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
        }

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
    /// Mongo the Destroyer - Massive titan boss
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

        // Massive body
        var body = new MeshInstance3D();
        body.Mesh = new CylinderMesh { TopRadius = 0.35f * scale, BottomRadius = 0.4f * scale, Height = 0.7f * scale, RadialSegments = radialSegs };
        body.MaterialOverride = skinMat;
        body.Position = new Vector3(0, 0.7f * scale, 0);
        parent.AddChild(body);

        // Chest armor plate
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
    /// Zev the Loot Goblin - Fast greedy goblin lord
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

        // Money bag in right hand! - Height = 2*Radius for proper sphere
        var moneyBag = new MeshInstance3D();
        moneyBag.Mesh = new SphereMesh { Radius = 0.12f * scale, Height = 0.24f * scale, RadialSegments = radialSegs, Rings = rings };
        moneyBag.MaterialOverride = bagMat;
        moneyBag.Position = new Vector3(0.08f * scale, -0.35f * scale, 0.1f * scale);
        moneyBag.Scale = new Vector3(1f, 1.2f, 0.8f);
        rightArmNode.AddChild(moneyBag);
        limbs.Weapon = rightArmNode;

        // Dollar sign on bag
        var dollarMesh = new BoxMesh { Size = new Vector3(0.06f * scale, 0.08f * scale, 0.01f * scale) };
        var dollarSign = new MeshInstance3D { Mesh = dollarMesh, MaterialOverride = goldMat };
        dollarSign.Position = new Vector3(0.08f * scale, -0.35f * scale, 0.2f * scale);
        rightArmNode.AddChild(dollarSign);

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
}

