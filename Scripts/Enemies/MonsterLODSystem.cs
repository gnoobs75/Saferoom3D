using Godot;
using System;
using System.Collections.Generic;
using SafeRoom3D.Core;

namespace SafeRoom3D.Enemies
{
    /// <summary>
    /// Level of detail enum for monster rendering
    /// </summary>
    public enum LODLevel
    {
        High,      // 0-15m: Full detail with all features
        Medium,    // 15-30m: Reduced segments, simplified limbs
        Low,       // 30-50m: Basic silhouette only
        Billboard  // 50+m: 2D impostor (placeholder)
    }

    /// <summary>
    /// Configuration for LOD distance thresholds
    /// </summary>
    public class LODConfig
    {
        public float HighDistance { get; set; } = 15f;
        public float MediumDistance { get; set; } = 30f;
        public float LowDistance { get; set; } = 50f;

        // Segment counts for different LOD levels
        public int HighSegments { get; set; } = 32;
        public int MediumSegments { get; set; } = 16;
        public int LowSegments { get; set; } = 8;

        // Whether to use billboard at max distance
        public bool UseBillboard { get; set; } = true;
    }

    /// <summary>
    /// Static factory for creating LOD meshes for procedural monsters.
    /// Provides distance-based level of detail management.
    /// </summary>
    public static class MonsterLODSystem
    {
        private static LODConfig s_config = new LODConfig();

        /// <summary>
        /// Get the appropriate LOD level based on camera distance
        /// </summary>
        public static LODLevel GetLODLevel(float distanceToCamera)
        {
            if (distanceToCamera < s_config.HighDistance)
                return LODLevel.High;
            else if (distanceToCamera < s_config.MediumDistance)
                return LODLevel.Medium;
            else if (distanceToCamera < s_config.LowDistance)
                return LODLevel.Low;
            else if (s_config.UseBillboard)
                return LODLevel.Billboard;
            else
                return LODLevel.Low;
        }

        /// <summary>
        /// Get segment count for sphere/cylinder meshes based on LOD level
        /// </summary>
        public static int GetSegmentCount(LODLevel level)
        {
            return level switch
            {
                LODLevel.High => s_config.HighSegments,
                LODLevel.Medium => s_config.MediumSegments,
                LODLevel.Low => s_config.LowSegments,
                LODLevel.Billboard => 4, // Minimal for billboard
                _ => s_config.MediumSegments
            };
        }

        /// <summary>
        /// Get ring count for spheres based on LOD level
        /// </summary>
        public static int GetRingCount(LODLevel level)
        {
            return level switch
            {
                LODLevel.High => 16,
                LODLevel.Medium => 8,
                LODLevel.Low => 4,
                LODLevel.Billboard => 2,
                _ => 8
            };
        }

        /// <summary>
        /// Create an LOD-appropriate mesh for a given monster type
        /// </summary>
        public static Mesh CreateLODMesh(string monsterType, LODLevel level, Color baseColor)
        {
            // Billboard level - create simple quad impostor
            if (level == LODLevel.Billboard)
            {
                return CreateBillboardMesh(monsterType, baseColor);
            }

            // For other levels, delegate to appropriate mesh creator
            return monsterType.ToLowerInvariant() switch
            {
                "goblin" => CreateGoblinLOD(level, baseColor),
                "goblin_shaman" => CreateGoblinShamanLOD(level, baseColor),
                "goblin_thrower" => CreateGoblinThrowerLOD(level, baseColor),
                "goblin_warlord" => CreateGoblinWarlordLOD(level, baseColor),
                "slime" => CreateSlimeLOD(level, baseColor),
                "eye" => CreateEyeLOD(level, baseColor),
                "mushroom" => CreateMushroomLOD(level, baseColor),
                "spider" => CreateSpiderLOD(level, baseColor),
                "lizard" => CreateLizardLOD(level, baseColor),
                "skeleton" => CreateSkeletonLOD(level, baseColor),
                "wolf" => CreateWolfLOD(level, baseColor),
                "bat" => CreateBatLOD(level, baseColor),
                "dragon" => CreateDragonLOD(level, baseColor),
                "torchbearer" => CreateGoblinLOD(level, baseColor), // Reuse goblin
                _ => CreateDefaultLOD(level, baseColor)
            };
        }

        /// <summary>
        /// Create a simplified collision shape for a monster type
        /// </summary>
        public static Shape3D CreateCollisionShape(string monsterType)
        {
            // Most monsters use capsule collision
            float radius = monsterType.ToLowerInvariant() switch
            {
                "goblin" or "goblin_shaman" or "goblin_thrower" or "torchbearer" => 0.4f,
                "goblin_warlord" => 0.6f,
                "slime" => 0.5f,
                "eye" => 0.3f,
                "mushroom" => 0.35f,
                "spider" => 0.4f,
                "lizard" => 0.5f,
                "skeleton" => 0.35f,
                "wolf" => 0.45f,
                "bat" => 0.25f,
                "dragon" => 1.0f,
                _ => 0.4f
            };

            float height = monsterType.ToLowerInvariant() switch
            {
                "goblin" or "goblin_shaman" or "goblin_thrower" or "torchbearer" => 1.2f,
                "goblin_warlord" => 1.8f,
                "slime" => 0.8f,
                "eye" => 0.6f,
                "mushroom" => 1.0f,
                "spider" => 0.6f,
                "lizard" => 1.0f,
                "skeleton" => 1.4f,
                "wolf" => 0.8f,
                "bat" => 0.5f,
                "dragon" => 2.5f,
                _ => 1.2f
            };

            var capsule = new CapsuleShape3D
            {
                Radius = radius,
                Height = height
            };

            return capsule;
        }

        /// <summary>
        /// Update a MeshInstance3D's mesh based on camera distance
        /// Returns true if LOD level changed
        /// </summary>
        public static bool UpdateLOD(MeshInstance3D meshInstance, Vector3 cameraPosition, string monsterType, Color baseColor, ref LODLevel currentLevel)
        {
            if (!GodotObject.IsInstanceValid(meshInstance))
                return false;

            float distance = meshInstance.GlobalPosition.DistanceTo(cameraPosition);
            LODLevel newLevel = GetLODLevel(distance);

            // Only update if level changed
            if (newLevel != currentLevel)
            {
                currentLevel = newLevel;
                meshInstance.Mesh = CreateLODMesh(monsterType, newLevel, baseColor);
                return true;
            }

            return false;
        }

        #region Billboard Mesh

        private static Mesh CreateBillboardMesh(string monsterType, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            // Create a simple quad facing camera
            float size = 1.5f;

            // Set color
            surfaceTool.SetColor(baseColor);

            // Create quad vertices
            surfaceTool.SetUV(new Vector2(0, 0));
            surfaceTool.AddVertex(new Vector3(-size/2, 0, 0));

            surfaceTool.SetUV(new Vector2(1, 0));
            surfaceTool.AddVertex(new Vector3(size/2, 0, 0));

            surfaceTool.SetUV(new Vector2(1, 1));
            surfaceTool.AddVertex(new Vector3(size/2, size * 1.5f, 0));

            surfaceTool.SetUV(new Vector2(0, 0));
            surfaceTool.AddVertex(new Vector3(-size/2, 0, 0));

            surfaceTool.SetUV(new Vector2(1, 1));
            surfaceTool.AddVertex(new Vector3(size/2, size * 1.5f, 0));

            surfaceTool.SetUV(new Vector2(0, 1));
            surfaceTool.AddVertex(new Vector3(-size/2, size * 1.5f, 0));

            surfaceTool.GenerateNormals();
            var mesh = surfaceTool.Commit();

            // Apply simple material
            var material = new StandardMaterial3D
            {
                AlbedoColor = baseColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
            };
            mesh.SurfaceSetMaterial(0, material);

            return mesh;
        }

        #endregion

        #region Goblin LOD

        private static Mesh CreateGoblinLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Body - always present
            AddSphere(surfaceTool, Vector3.Up * 0.8f, 0.4f, segments, rings, baseColor);

            // Head
            AddSphere(surfaceTool, Vector3.Up * 1.4f, 0.3f, segments, rings, baseColor);

            if (level == LODLevel.High)
            {
                // Full detail - ears, nose, arms, legs
                Color earColor = baseColor.Darkened(0.1f);
                AddSphere(surfaceTool, new Vector3(-0.35f, 1.5f, 0), 0.12f, segments/2, rings/2, earColor);
                AddSphere(surfaceTool, new Vector3(0.35f, 1.5f, 0), 0.12f, segments/2, rings/2, earColor);

                Color noseColor = baseColor.Darkened(0.2f);
                AddSphere(surfaceTool, new Vector3(0, 1.4f, 0.25f), 0.08f, segments/2, rings/2, noseColor);

                // Eyes
                AddSphere(surfaceTool, new Vector3(-0.1f, 1.5f, 0.25f), 0.05f, 8, 4, Colors.White);
                AddSphere(surfaceTool, new Vector3(0.1f, 1.5f, 0.25f), 0.05f, 8, 4, Colors.White);
                AddSphere(surfaceTool, new Vector3(-0.1f, 1.5f, 0.28f), 0.03f, 8, 4, Colors.Black);
                AddSphere(surfaceTool, new Vector3(0.1f, 1.5f, 0.28f), 0.03f, 8, 4, Colors.Black);

                // Arms
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.5f, 0.9f, 0), 0.1f, 0.6f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.5f, 0.9f, 0), 0.1f, 0.6f, segments, limbColor);

                // Legs
                AddCylinder(surfaceTool, new Vector3(-0.2f, 0.3f, 0), 0.12f, 0.6f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.2f, 0.3f, 0), 0.12f, 0.6f, segments, limbColor);
            }
            else if (level == LODLevel.Medium)
            {
                // Medium detail - simplified limbs, basic features
                Color limbColor = baseColor.Darkened(0.15f);

                // Simplified arms and legs
                AddCylinder(surfaceTool, new Vector3(-0.5f, 0.9f, 0), 0.1f, 0.6f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.5f, 0.9f, 0), 0.1f, 0.6f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(-0.2f, 0.3f, 0), 0.12f, 0.6f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.2f, 0.3f, 0), 0.12f, 0.6f, segments, limbColor);

                // Basic eyes
                AddSphere(surfaceTool, new Vector3(-0.1f, 1.5f, 0.25f), 0.05f, 6, 3, Colors.White);
                AddSphere(surfaceTool, new Vector3(0.1f, 1.5f, 0.25f), 0.05f, 6, 3, Colors.White);
            }
            // Low detail - just body and head, no limbs

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateGoblinShamanLOD(LODLevel level, Color baseColor)
        {
            // Start with goblin base
            var mesh = CreateGoblinLOD(level, baseColor);

            if (level == LODLevel.High)
            {
                var surfaceTool = new SurfaceTool();
                surfaceTool.CreateFrom(mesh, 0);
                surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

                // Add staff
                Color staffColor = new Color(0.4f, 0.25f, 0.1f);
                AddCylinder(surfaceTool, new Vector3(0.6f, 1.0f, 0), 0.04f, 1.5f, GetSegmentCount(level), staffColor);

                // Staff orb
                Color orbColor = new Color(0.3f, 0.6f, 1.0f);
                AddSphere(surfaceTool, new Vector3(0.6f, 1.8f, 0), 0.12f, GetSegmentCount(level)/2, GetRingCount(level)/2, orbColor);

                surfaceTool.GenerateNormals();
                return surfaceTool.Commit();
            }

            return mesh;
        }

        private static Mesh CreateGoblinThrowerLOD(LODLevel level, Color baseColor)
        {
            return CreateGoblinLOD(level, baseColor); // Same as regular goblin for now
        }

        private static Mesh CreateGoblinWarlordLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Larger body
            AddSphere(surfaceTool, Vector3.Up * 1.0f, 0.6f, segments, rings, baseColor);
            AddSphere(surfaceTool, Vector3.Up * 1.8f, 0.4f, segments, rings, baseColor);

            if (level == LODLevel.High)
            {
                // Armor details
                Color armorColor = new Color(0.4f, 0.4f, 0.4f);
                AddCylinder(surfaceTool, Vector3.Up * 1.0f, 0.65f, 0.4f, segments, armorColor);

                // Helmet spike
                AddCylinder(surfaceTool, Vector3.Up * 2.3f, 0.08f, 0.5f, segments/2, armorColor);

                // Big sword
                Color swordColor = new Color(0.6f, 0.6f, 0.65f);
                AddBox(surfaceTool, new Vector3(0.8f, 1.2f, 0), new Vector3(0.15f, 0.8f, 0.05f), swordColor);

                // Arms and legs
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.7f, 1.1f, 0), 0.15f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.7f, 1.1f, 0), 0.15f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(-0.25f, 0.4f, 0), 0.18f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.25f, 0.4f, 0), 0.18f, 0.8f, segments, limbColor);
            }
            else if (level == LODLevel.Medium)
            {
                // Simplified armor
                Color armorColor = new Color(0.4f, 0.4f, 0.4f);
                AddCylinder(surfaceTool, Vector3.Up * 1.0f, 0.65f, 0.4f, segments, armorColor);

                // Simplified limbs
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.7f, 1.1f, 0), 0.15f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.7f, 1.1f, 0), 0.15f, 0.8f, segments, limbColor);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        #endregion

        #region Other Monster LODs

        private static Mesh CreateSlimeLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Main blob (squashed sphere)
            AddSphere(surfaceTool, Vector3.Up * 0.4f, 0.5f, segments, rings, baseColor, new Vector3(1, 0.6f, 1));

            if (level == LODLevel.High)
            {
                // Eyes
                AddSphere(surfaceTool, new Vector3(-0.2f, 0.5f, 0.4f), 0.1f, segments/2, rings/2, Colors.White);
                AddSphere(surfaceTool, new Vector3(0.2f, 0.5f, 0.4f), 0.1f, segments/2, rings/2, Colors.White);
                AddSphere(surfaceTool, new Vector3(-0.2f, 0.5f, 0.45f), 0.05f, 8, 4, Colors.Black);
                AddSphere(surfaceTool, new Vector3(0.2f, 0.5f, 0.45f), 0.05f, 8, 4, Colors.Black);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateEyeLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Main eyeball
            AddSphere(surfaceTool, Vector3.Up * 0.8f, 0.5f, segments, rings, Colors.White);

            if (level == LODLevel.High || level == LODLevel.Medium)
            {
                // Iris
                AddSphere(surfaceTool, new Vector3(0, 0.8f, 0.45f), 0.25f, segments/2, rings/2, baseColor);
                // Pupil
                AddSphere(surfaceTool, new Vector3(0, 0.8f, 0.5f), 0.12f, segments/2, rings/2, Colors.Black);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateMushroomLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);

            // Stem
            Color stemColor = new Color(0.9f, 0.9f, 0.85f);
            AddCylinder(surfaceTool, Vector3.Up * 0.4f, 0.15f, 0.8f, segments, stemColor);

            // Cap
            AddSphere(surfaceTool, Vector3.Up * 0.9f, 0.4f, segments, GetRingCount(level), baseColor, new Vector3(1.2f, 0.6f, 1.2f));

            if (level == LODLevel.High)
            {
                // Spots on cap
                Color spotColor = baseColor.Lightened(0.3f);
                AddSphere(surfaceTool, new Vector3(0.2f, 0.95f, 0.2f), 0.08f, 8, 4, spotColor);
                AddSphere(surfaceTool, new Vector3(-0.15f, 0.92f, 0.25f), 0.06f, 8, 4, spotColor);
                AddSphere(surfaceTool, new Vector3(0.1f, 0.88f, -0.2f), 0.07f, 8, 4, spotColor);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateSpiderLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Body segments
            AddSphere(surfaceTool, Vector3.Up * 0.3f, 0.35f, segments, rings, baseColor);
            AddSphere(surfaceTool, new Vector3(0, 0.3f, 0.4f), 0.25f, segments, rings, baseColor);

            if (level == LODLevel.High)
            {
                // 8 legs (simplified)
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.Pi / 4f;
                    Vector3 legPos = new Vector3(Mathf.Cos(angle) * 0.4f, 0.2f, Mathf.Sin(angle) * 0.4f);
                    AddCylinder(surfaceTool, legPos, 0.05f, 0.5f, segments/2, baseColor.Darkened(0.2f));
                }

                // Eyes
                AddSphere(surfaceTool, new Vector3(-0.1f, 0.35f, 0.6f), 0.06f, 8, 4, Colors.Red);
                AddSphere(surfaceTool, new Vector3(0.1f, 0.35f, 0.6f), 0.06f, 8, 4, Colors.Red);
            }
            else if (level == LODLevel.Medium)
            {
                // 4 legs only
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.Pi / 2f;
                    Vector3 legPos = new Vector3(Mathf.Cos(angle) * 0.4f, 0.2f, Mathf.Sin(angle) * 0.4f);
                    AddCylinder(surfaceTool, legPos, 0.05f, 0.5f, segments/2, baseColor.Darkened(0.2f));
                }
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateLizardLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Body
            AddSphere(surfaceTool, Vector3.Up * 0.6f, 0.4f, segments, rings, baseColor, new Vector3(1, 1, 1.5f));

            // Head
            AddSphere(surfaceTool, new Vector3(0, 0.7f, 0.7f), 0.3f, segments, rings, baseColor);

            if (level == LODLevel.High)
            {
                // Tail
                AddCylinder(surfaceTool, new Vector3(0, 0.4f, -0.6f), 0.15f, 0.8f, segments, baseColor.Darkened(0.1f));

                // Legs
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.3f, 0.3f, 0.3f), 0.12f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.3f, 0.3f, 0.3f), 0.12f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(-0.3f, 0.3f, -0.2f), 0.12f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.3f, 0.3f, -0.2f), 0.12f, 0.5f, segments, limbColor);

                // Eyes
                AddSphere(surfaceTool, new Vector3(-0.12f, 0.8f, 0.85f), 0.06f, 8, 4, Colors.Yellow);
                AddSphere(surfaceTool, new Vector3(0.12f, 0.8f, 0.85f), 0.06f, 8, 4, Colors.Yellow);
            }
            else if (level == LODLevel.Medium)
            {
                // Simplified legs
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.3f, 0.3f, 0.3f), 0.12f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.3f, 0.3f, 0.3f), 0.12f, 0.5f, segments, limbColor);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateSkeletonLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            Color boneColor = new Color(0.9f, 0.9f, 0.85f);

            // Ribcage
            AddSphere(surfaceTool, Vector3.Up * 0.9f, 0.35f, segments, rings, boneColor, new Vector3(1, 1.2f, 0.8f));

            // Skull
            AddSphere(surfaceTool, Vector3.Up * 1.5f, 0.25f, segments, rings, boneColor);

            if (level == LODLevel.High)
            {
                // Arms
                AddCylinder(surfaceTool, new Vector3(-0.5f, 1.0f, 0), 0.08f, 0.7f, segments, boneColor);
                AddCylinder(surfaceTool, new Vector3(0.5f, 1.0f, 0), 0.08f, 0.7f, segments, boneColor);

                // Legs
                AddCylinder(surfaceTool, new Vector3(-0.15f, 0.35f, 0), 0.1f, 0.7f, segments, boneColor);
                AddCylinder(surfaceTool, new Vector3(0.15f, 0.35f, 0), 0.1f, 0.7f, segments, boneColor);

                // Eye sockets
                AddSphere(surfaceTool, new Vector3(-0.1f, 1.55f, 0.2f), 0.06f, 8, 4, Colors.Black);
                AddSphere(surfaceTool, new Vector3(0.1f, 1.55f, 0.2f), 0.06f, 8, 4, Colors.Black);
            }
            else if (level == LODLevel.Medium)
            {
                // Simplified limbs
                AddCylinder(surfaceTool, new Vector3(-0.5f, 1.0f, 0), 0.08f, 0.7f, segments, boneColor);
                AddCylinder(surfaceTool, new Vector3(0.5f, 1.0f, 0), 0.08f, 0.7f, segments, boneColor);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateWolfLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Body (horizontal)
            AddSphere(surfaceTool, Vector3.Up * 0.6f, 0.4f, segments, rings, baseColor, new Vector3(1.2f, 1, 1.8f));

            // Head
            AddSphere(surfaceTool, new Vector3(0, 0.7f, 0.9f), 0.3f, segments, rings, baseColor);

            if (level == LODLevel.High)
            {
                // Snout
                AddCylinder(surfaceTool, new Vector3(0, 0.65f, 1.15f), 0.12f, 0.25f, segments, baseColor.Darkened(0.1f));

                // Ears
                AddSphere(surfaceTool, new Vector3(-0.15f, 0.9f, 0.85f), 0.12f, segments/2, rings/2, baseColor);
                AddSphere(surfaceTool, new Vector3(0.15f, 0.9f, 0.85f), 0.12f, segments/2, rings/2, baseColor);

                // Tail
                AddCylinder(surfaceTool, new Vector3(0, 0.5f, -0.7f), 0.1f, 0.6f, segments, baseColor.Darkened(0.1f));

                // Legs
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.3f, 0.25f, 0.5f), 0.1f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.3f, 0.25f, 0.5f), 0.1f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(-0.3f, 0.25f, -0.3f), 0.1f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.3f, 0.25f, -0.3f), 0.1f, 0.5f, segments, limbColor);
            }
            else if (level == LODLevel.Medium)
            {
                // Front legs only
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.3f, 0.25f, 0.5f), 0.1f, 0.5f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.3f, 0.25f, 0.5f), 0.1f, 0.5f, segments, limbColor);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateBatLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Body
            AddSphere(surfaceTool, Vector3.Up * 0.8f, 0.25f, segments, rings, baseColor);

            if (level == LODLevel.High)
            {
                // Wings (simplified as triangular shapes)
                Color wingColor = baseColor.Darkened(0.2f);
                AddBox(surfaceTool, new Vector3(-0.5f, 0.8f, 0), new Vector3(0.4f, 0.05f, 0.3f), wingColor);
                AddBox(surfaceTool, new Vector3(0.5f, 0.8f, 0), new Vector3(0.4f, 0.05f, 0.3f), wingColor);

                // Ears
                AddSphere(surfaceTool, new Vector3(-0.1f, 0.95f, 0.15f), 0.08f, segments/2, rings/2, baseColor);
                AddSphere(surfaceTool, new Vector3(0.1f, 0.95f, 0.15f), 0.08f, segments/2, rings/2, baseColor);

                // Eyes
                AddSphere(surfaceTool, new Vector3(-0.08f, 0.85f, 0.22f), 0.04f, 8, 4, Colors.Red);
                AddSphere(surfaceTool, new Vector3(0.08f, 0.85f, 0.22f), 0.04f, 8, 4, Colors.Red);
            }
            else if (level == LODLevel.Medium)
            {
                // Simplified wings
                Color wingColor = baseColor.Darkened(0.2f);
                AddBox(surfaceTool, new Vector3(-0.4f, 0.8f, 0), new Vector3(0.3f, 0.05f, 0.2f), wingColor);
                AddBox(surfaceTool, new Vector3(0.4f, 0.8f, 0), new Vector3(0.3f, 0.05f, 0.2f), wingColor);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateDragonLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Large body
            AddSphere(surfaceTool, Vector3.Up * 1.2f, 0.8f, segments, rings, baseColor, new Vector3(1, 1, 1.5f));

            // Head
            AddSphere(surfaceTool, new Vector3(0, 1.5f, 1.2f), 0.5f, segments, rings, baseColor);

            if (level == LODLevel.High)
            {
                // Neck
                AddCylinder(surfaceTool, new Vector3(0, 1.3f, 0.6f), 0.4f, 0.8f, segments, baseColor.Darkened(0.1f));

                // Wings
                Color wingColor = baseColor.Darkened(0.2f);
                AddBox(surfaceTool, new Vector3(-1.2f, 1.4f, 0), new Vector3(0.8f, 0.1f, 1.2f), wingColor);
                AddBox(surfaceTool, new Vector3(1.2f, 1.4f, 0), new Vector3(0.8f, 0.1f, 1.2f), wingColor);

                // Tail
                AddCylinder(surfaceTool, new Vector3(0, 0.8f, -1.2f), 0.3f, 1.5f, segments, baseColor.Darkened(0.1f));

                // Horns
                AddCylinder(surfaceTool, new Vector3(-0.3f, 1.9f, 1.1f), 0.08f, 0.4f, segments/2, Colors.DarkGray);
                AddCylinder(surfaceTool, new Vector3(0.3f, 1.9f, 1.1f), 0.08f, 0.4f, segments/2, Colors.DarkGray);

                // Legs
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.6f, 0.4f, 0.4f), 0.25f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.6f, 0.4f, 0.4f), 0.25f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(-0.6f, 0.4f, -0.4f), 0.25f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.6f, 0.4f, -0.4f), 0.25f, 0.8f, segments, limbColor);
            }
            else if (level == LODLevel.Medium)
            {
                // Simplified wings
                Color wingColor = baseColor.Darkened(0.2f);
                AddBox(surfaceTool, new Vector3(-1.0f, 1.4f, 0), new Vector3(0.6f, 0.1f, 1.0f), wingColor);
                AddBox(surfaceTool, new Vector3(1.0f, 1.4f, 0), new Vector3(0.6f, 0.1f, 1.0f), wingColor);

                // Front legs only
                Color limbColor = baseColor.Darkened(0.15f);
                AddCylinder(surfaceTool, new Vector3(-0.6f, 0.4f, 0.4f), 0.25f, 0.8f, segments, limbColor);
                AddCylinder(surfaceTool, new Vector3(0.6f, 0.4f, 0.4f), 0.25f, 0.8f, segments, limbColor);
            }

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        private static Mesh CreateDefaultLOD(LODLevel level, Color baseColor)
        {
            var surfaceTool = new SurfaceTool();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            int segments = GetSegmentCount(level);
            int rings = GetRingCount(level);

            // Simple sphere
            AddSphere(surfaceTool, Vector3.Up * 0.8f, 0.5f, segments, rings, baseColor);

            surfaceTool.GenerateNormals();
            return surfaceTool.Commit();
        }

        #endregion

        #region Helper Methods

        private static void AddSphere(SurfaceTool st, Vector3 position, float radius, int segments, int rings, Color color, Vector3? scale = null)
        {
            Vector3 actualScale = scale ?? Vector3.One;

            for (int i = 0; i < rings; i++)
            {
                float lat0 = Mathf.Pi * (-0.5f + (float)i / rings);
                float z0 = Mathf.Sin(lat0);
                float zr0 = Mathf.Cos(lat0);

                float lat1 = Mathf.Pi * (-0.5f + (float)(i + 1) / rings);
                float z1 = Mathf.Sin(lat1);
                float zr1 = Mathf.Cos(lat1);

                for (int j = 0; j < segments; j++)
                {
                    float lng0 = 2 * Mathf.Pi * (float)j / segments;
                    float lng1 = 2 * Mathf.Pi * (float)(j + 1) / segments;

                    float x00 = Mathf.Cos(lng0) * zr0;
                    float y00 = z0;
                    float z00 = Mathf.Sin(lng0) * zr0;

                    float x01 = Mathf.Cos(lng1) * zr0;
                    float y01 = z0;
                    float z01 = Mathf.Sin(lng1) * zr0;

                    float x10 = Mathf.Cos(lng0) * zr1;
                    float y10 = z1;
                    float z10 = Mathf.Sin(lng0) * zr1;

                    float x11 = Mathf.Cos(lng1) * zr1;
                    float y11 = z1;
                    float z11 = Mathf.Sin(lng1) * zr1;

                    Vector3 v00 = new Vector3(x00 * actualScale.X, y00 * actualScale.Y, z00 * actualScale.Z) * radius + position;
                    Vector3 v01 = new Vector3(x01 * actualScale.X, y01 * actualScale.Y, z01 * actualScale.Z) * radius + position;
                    Vector3 v10 = new Vector3(x10 * actualScale.X, y10 * actualScale.Y, z10 * actualScale.Z) * radius + position;
                    Vector3 v11 = new Vector3(x11 * actualScale.X, y11 * actualScale.Y, z11 * actualScale.Z) * radius + position;

                    Vector3 n00 = new Vector3(x00, y00, z00).Normalized();
                    Vector3 n01 = new Vector3(x01, y01, z01).Normalized();
                    Vector3 n10 = new Vector3(x10, y10, z10).Normalized();
                    Vector3 n11 = new Vector3(x11, y11, z11).Normalized();

                    st.SetColor(color);

                    if (i > 0)
                    {
                        st.AddVertex(v00);
                        st.AddVertex(v01);
                        st.AddVertex(v10);
                    }

                    if (i < rings - 1)
                    {
                        st.AddVertex(v01);
                        st.AddVertex(v11);
                        st.AddVertex(v10);
                    }
                }
            }
        }

        private static void AddCylinder(SurfaceTool st, Vector3 position, float radius, float height, int segments, Color color)
        {
            Vector3 bottom = position - Vector3.Up * (height / 2);
            Vector3 top = position + Vector3.Up * (height / 2);

            // Sides
            for (int i = 0; i < segments; i++)
            {
                float angle0 = 2 * Mathf.Pi * i / segments;
                float angle1 = 2 * Mathf.Pi * (i + 1) / segments;

                float x0 = Mathf.Cos(angle0) * radius;
                float z0 = Mathf.Sin(angle0) * radius;
                float x1 = Mathf.Cos(angle1) * radius;
                float z1 = Mathf.Sin(angle1) * radius;

                Vector3 v0Bottom = bottom + new Vector3(x0, 0, z0);
                Vector3 v1Bottom = bottom + new Vector3(x1, 0, z1);
                Vector3 v0Top = top + new Vector3(x0, 0, z0);
                Vector3 v1Top = top + new Vector3(x1, 0, z1);

                Vector3 n0 = new Vector3(x0, 0, z0).Normalized();
                Vector3 n1 = new Vector3(x1, 0, z1).Normalized();

                st.SetColor(color);

                st.AddVertex(v0Bottom);
                st.AddVertex(v1Bottom);
                st.AddVertex(v0Top);

                st.AddVertex(v1Bottom);
                st.AddVertex(v1Top);
                st.AddVertex(v0Top);
            }

            // Caps
            for (int i = 0; i < segments; i++)
            {
                float angle0 = 2 * Mathf.Pi * i / segments;
                float angle1 = 2 * Mathf.Pi * (i + 1) / segments;

                float x0 = Mathf.Cos(angle0) * radius;
                float z0 = Mathf.Sin(angle0) * radius;
                float x1 = Mathf.Cos(angle1) * radius;
                float z1 = Mathf.Sin(angle1) * radius;

                st.SetColor(color);

                // Bottom cap
                st.AddVertex(bottom);
                st.AddVertex(bottom + new Vector3(x1, 0, z1));
                st.AddVertex(bottom + new Vector3(x0, 0, z0));

                // Top cap
                st.AddVertex(top);
                st.AddVertex(top + new Vector3(x0, 0, z0));
                st.AddVertex(top + new Vector3(x1, 0, z1));
            }
        }

        private static void AddBox(SurfaceTool st, Vector3 center, Vector3 size, Color color)
        {
            Vector3 halfSize = size / 2;

            Vector3[] vertices = new Vector3[]
            {
                center + new Vector3(-halfSize.X, -halfSize.Y, -halfSize.Z),
                center + new Vector3(halfSize.X, -halfSize.Y, -halfSize.Z),
                center + new Vector3(halfSize.X, halfSize.Y, -halfSize.Z),
                center + new Vector3(-halfSize.X, halfSize.Y, -halfSize.Z),
                center + new Vector3(-halfSize.X, -halfSize.Y, halfSize.Z),
                center + new Vector3(halfSize.X, -halfSize.Y, halfSize.Z),
                center + new Vector3(halfSize.X, halfSize.Y, halfSize.Z),
                center + new Vector3(-halfSize.X, halfSize.Y, halfSize.Z)
            };

            int[][] faces = new int[][]
            {
                new int[] {0, 1, 2, 3}, // Back
                new int[] {5, 4, 7, 6}, // Front
                new int[] {4, 0, 3, 7}, // Left
                new int[] {1, 5, 6, 2}, // Right
                new int[] {3, 2, 6, 7}, // Top
                new int[] {4, 5, 1, 0}  // Bottom
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.Back,
                Vector3.Forward,
                Vector3.Left,
                Vector3.Right,
                Vector3.Up,
                Vector3.Down
            };

            st.SetColor(color);

            for (int i = 0; i < faces.Length; i++)
            {
                int[] face = faces[i];

                st.AddVertex(vertices[face[0]]);
                st.AddVertex(vertices[face[1]]);
                st.AddVertex(vertices[face[2]]);

                st.AddVertex(vertices[face[0]]);
                st.AddVertex(vertices[face[2]]);
                st.AddVertex(vertices[face[3]]);
            }
        }

        #endregion
    }
}
