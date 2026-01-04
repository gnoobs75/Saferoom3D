using Godot;
using System;
using SafeRoom3D.Core;

namespace SafeRoom3D.Environment;

/// <summary>
/// Utility class for generating low-poly 3D meshes procedurally.
/// Used for barrels, crates, pots, pillars, etc.
/// Matches the procedural cosmetic style from 2D version.
/// </summary>
public static class ProceduralMesh3D
{
    /// <summary>
    /// Generate a low-poly cylinder mesh (for barrels, pillars)
    /// </summary>
    public static ArrayMesh CreateCylinder(float radius, float height, int segments = 12, bool capped = true)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float halfHeight = height / 2f;
        float angleStep = Mathf.Tau / segments;

        // Generate vertices for cylinder body
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // Bottom vertex
            surfaceTool.SetNormal(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)));
            surfaceTool.SetUV(new Vector2((float)i / segments, 0));
            surfaceTool.AddVertex(new Vector3(x, -halfHeight, z));

            // Top vertex
            surfaceTool.SetNormal(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)));
            surfaceTool.SetUV(new Vector2((float)i / segments, 1));
            surfaceTool.AddVertex(new Vector3(x, halfHeight, z));
        }

        // Generate triangles for body
        for (int i = 0; i < segments; i++)
        {
            int baseIdx = i * 2;
            // First triangle
            surfaceTool.AddIndex(baseIdx);
            surfaceTool.AddIndex(baseIdx + 1);
            surfaceTool.AddIndex(baseIdx + 2);
            // Second triangle
            surfaceTool.AddIndex(baseIdx + 2);
            surfaceTool.AddIndex(baseIdx + 1);
            surfaceTool.AddIndex(baseIdx + 3);
        }

        if (capped)
        {
            int capStartIdx = (segments + 1) * 2;

            // Top cap center
            surfaceTool.SetNormal(Vector3.Up);
            surfaceTool.SetUV(new Vector2(0.5f, 0.5f));
            surfaceTool.AddVertex(new Vector3(0, halfHeight, 0));

            // Top cap ring
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                surfaceTool.SetNormal(Vector3.Up);
                surfaceTool.SetUV(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
                surfaceTool.AddVertex(new Vector3(x, halfHeight, z));
            }

            // Top cap triangles
            for (int i = 0; i < segments; i++)
            {
                surfaceTool.AddIndex(capStartIdx); // Center
                surfaceTool.AddIndex(capStartIdx + 1 + i);
                surfaceTool.AddIndex(capStartIdx + 2 + i);
            }

            int bottomCapStart = capStartIdx + segments + 2;

            // Bottom cap center
            surfaceTool.SetNormal(Vector3.Down);
            surfaceTool.SetUV(new Vector2(0.5f, 0.5f));
            surfaceTool.AddVertex(new Vector3(0, -halfHeight, 0));

            // Bottom cap ring
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                surfaceTool.SetNormal(Vector3.Down);
                surfaceTool.SetUV(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
                surfaceTool.AddVertex(new Vector3(x, -halfHeight, z));
            }

            // Bottom cap triangles (reversed winding)
            for (int i = 0; i < segments; i++)
            {
                surfaceTool.AddIndex(bottomCapStart); // Center
                surfaceTool.AddIndex(bottomCapStart + 2 + i);
                surfaceTool.AddIndex(bottomCapStart + 1 + i);
            }
        }

        surfaceTool.GenerateTangents();
        return surfaceTool.Commit();
    }

    /// <summary>
    /// Generate a low-poly box mesh (for crates, tables)
    /// </summary>
    public static ArrayMesh CreateBox(Vector3 size)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Vector3 half = size / 2f;

        // Define all 8 corners
        Vector3[] corners = new Vector3[]
        {
            new(-half.X, -half.Y, -half.Z), // 0: back-bottom-left
            new( half.X, -half.Y, -half.Z), // 1: back-bottom-right
            new( half.X,  half.Y, -half.Z), // 2: back-top-right
            new(-half.X,  half.Y, -half.Z), // 3: back-top-left
            new(-half.X, -half.Y,  half.Z), // 4: front-bottom-left
            new( half.X, -half.Y,  half.Z), // 5: front-bottom-right
            new( half.X,  half.Y,  half.Z), // 6: front-top-right
            new(-half.X,  half.Y,  half.Z), // 7: front-top-left
        };

        // Face definitions: normal, corner indices, UVs
        var faces = new (Vector3 normal, int[] indices)[]
        {
            (Vector3.Forward,  new[] { 4, 5, 6, 7 }), // Front
            (Vector3.Back,     new[] { 1, 0, 3, 2 }), // Back
            (Vector3.Up,       new[] { 7, 6, 2, 3 }), // Top
            (Vector3.Down,     new[] { 4, 0, 1, 5 }), // Bottom
            (Vector3.Right,    new[] { 5, 1, 2, 6 }), // Right
            (Vector3.Left,     new[] { 0, 4, 7, 3 }), // Left
        };

        Vector2[] uvs = { new(0, 1), new(1, 1), new(1, 0), new(0, 0) };

        int vertexCount = 0;
        foreach (var (normal, indices) in faces)
        {
            for (int i = 0; i < 4; i++)
            {
                surfaceTool.SetNormal(normal);
                surfaceTool.SetUV(uvs[i]);
                surfaceTool.AddVertex(corners[indices[i]]);
            }

            // Two triangles per face
            int b = vertexCount;
            surfaceTool.AddIndex(b + 0);
            surfaceTool.AddIndex(b + 1);
            surfaceTool.AddIndex(b + 2);
            surfaceTool.AddIndex(b + 0);
            surfaceTool.AddIndex(b + 2);
            surfaceTool.AddIndex(b + 3);

            vertexCount += 4;
        }

        surfaceTool.GenerateTangents();
        return surfaceTool.Commit();
    }

    /// <summary>
    /// Generate a low-poly pot/vase shape (tapered cylinder with rim)
    /// </summary>
    public static ArrayMesh CreatePot(float bottomRadius, float topRadius, float height, int segments = 10)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float halfHeight = height / 2f;
        float angleStep = Mathf.Tau / segments;

        // Body vertices (tapered)
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            // Bottom vertex
            surfaceTool.SetNormal(new Vector3(cosA, 0, sinA).Normalized());
            surfaceTool.SetUV(new Vector2((float)i / segments, 0));
            surfaceTool.AddVertex(new Vector3(cosA * bottomRadius, -halfHeight, sinA * bottomRadius));

            // Top vertex
            surfaceTool.SetNormal(new Vector3(cosA, 0.3f, sinA).Normalized());
            surfaceTool.SetUV(new Vector2((float)i / segments, 1));
            surfaceTool.AddVertex(new Vector3(cosA * topRadius, halfHeight, sinA * topRadius));
        }

        // Body triangles
        for (int i = 0; i < segments; i++)
        {
            int baseIdx = i * 2;
            surfaceTool.AddIndex(baseIdx);
            surfaceTool.AddIndex(baseIdx + 1);
            surfaceTool.AddIndex(baseIdx + 2);
            surfaceTool.AddIndex(baseIdx + 2);
            surfaceTool.AddIndex(baseIdx + 1);
            surfaceTool.AddIndex(baseIdx + 3);
        }

        // Rim (slightly larger at top)
        float rimRadius = topRadius * 1.15f;
        float rimHeight = height * 0.1f;
        int rimStart = (segments + 1) * 2;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            // Inner rim
            surfaceTool.SetNormal(Vector3.Up);
            surfaceTool.SetUV(new Vector2((float)i / segments, 0));
            surfaceTool.AddVertex(new Vector3(cosA * topRadius, halfHeight, sinA * topRadius));

            // Outer rim
            surfaceTool.SetNormal(new Vector3(cosA, 0.5f, sinA).Normalized());
            surfaceTool.SetUV(new Vector2((float)i / segments, 1));
            surfaceTool.AddVertex(new Vector3(cosA * rimRadius, halfHeight + rimHeight, sinA * rimRadius));
        }

        // Rim triangles
        for (int i = 0; i < segments; i++)
        {
            int baseIdx = rimStart + i * 2;
            surfaceTool.AddIndex(baseIdx);
            surfaceTool.AddIndex(baseIdx + 1);
            surfaceTool.AddIndex(baseIdx + 2);
            surfaceTool.AddIndex(baseIdx + 2);
            surfaceTool.AddIndex(baseIdx + 1);
            surfaceTool.AddIndex(baseIdx + 3);
        }

        surfaceTool.GenerateTangents();
        return surfaceTool.Commit();
    }

    /// <summary>
    /// Generate a low-poly crystal shape (double pyramid)
    /// </summary>
    public static ArrayMesh CreateCrystal(float radius, float height, int sides = 6)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        float halfHeight = height / 2f;
        float angleStep = Mathf.Tau / sides;

        // Top point
        Vector3 topPoint = new(0, halfHeight, 0);
        // Bottom point
        Vector3 bottomPoint = new(0, -halfHeight, 0);

        // Generate side vertices at middle
        Vector3[] midPoints = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = i * angleStep;
            midPoints[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );
        }

        // Create triangles from top
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 v1 = midPoints[i];
            Vector3 v2 = midPoints[next];

            // Calculate face normal
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

        // Create triangles from bottom
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 v1 = midPoints[i];
            Vector3 v2 = midPoints[next];

            // Calculate face normal (reversed winding)
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

        surfaceTool.GenerateTangents();
        return surfaceTool.Commit();
    }

    /// <summary>
    /// Create a simple PBR material with color
    /// </summary>
    public static StandardMaterial3D CreateMaterial(Color albedo, float roughness = 0.8f, float metallic = 0f)
    {
        var material = new StandardMaterial3D();
        material.AlbedoColor = albedo;
        material.Roughness = roughness;
        material.Metallic = metallic;
        // Disable culling to ensure all faces render correctly (fixes "missing chunk" issues)
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return material;
    }

    /// <summary>
    /// Create a wood-like material with noise texture
    /// </summary>
    public static StandardMaterial3D CreateWoodMaterial(Color baseColor, float variation = 0.1f)
    {
        var material = new StandardMaterial3D();
        material.AlbedoColor = baseColor;
        material.Roughness = 0.85f;
        material.Metallic = 0f;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        // Add subtle detail texture
        var noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.Frequency = 0.1f;

        var noiseTexture = new NoiseTexture2D();
        noiseTexture.Noise = noise;
        noiseTexture.Width = 64;
        noiseTexture.Height = 64;

        material.DetailEnabled = true;
        material.DetailAlbedo = noiseTexture;
        material.DetailBlendMode = BaseMaterial3D.BlendModeEnum.Mul;

        return material;
    }

    /// <summary>
    /// Create a metal material (for bands, armor, etc.)
    /// </summary>
    public static StandardMaterial3D CreateMetalMaterial(Color baseColor, float roughness = 0.4f)
    {
        var material = new StandardMaterial3D();
        material.AlbedoColor = baseColor;
        material.Roughness = roughness;
        material.Metallic = 0.8f;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        material.SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon; // Low-poly aesthetic
        return material;
    }
}
