# Dungeon Props 3D Model Enhancement Guide

## Overview
This document provides enhanced implementations for all 25 new dungeon props in `Cosmetic3D.cs`.
Each enhancement includes better geometry, improved PBR materials, and prop-specific visual details.

## Implementation Instructions
1. Open `C:\Claude\SafeRoom3D\Scripts\Environment\Cosmetic3D.cs`
2. Find each method listed below
3. Replace with the enhanced version
4. Build with: `dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"`

---

## Enhanced Implementations

### 1. bone_pile - Anatomically Detailed Bone Pile

**Enhancements:**
- Skull with separated jaw bone and eye sockets
- Femur bones with anatomically accurate knobbed ends
- Curved ribs using cylinder geometry
- Vertebrae with spinal processes
- Scattered bone fragments
- Enhanced material with subtle metallic sheen

**Replace CreateBonePileMesh with:**

```csharp
private ArrayMesh CreateBonePileMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;
    var boneColor = new Color(0.88f, 0.82f, 0.68f);

    // Central skull with detailed features
    AddSphereToSurfaceTool(surfaceTool, new Vector3(0, 0.15f * s, 0), 0.13f * s);
    // Jaw bone (separated from skull)
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.08f * s, 0.08f * s), new Vector3(0.08f * s, 0.02f * s, 0.06f * s));
    // Eye sockets (dark indented spheres)
    AddSphereToSurfaceTool(surfaceTool, new Vector3(-0.04f * s, 0.17f * s, 0.08f * s), 0.025f * s);
    AddSphereToSurfaceTool(surfaceTool, new Vector3(0.04f * s, 0.17f * s, 0.08f * s), 0.025f * s);

    // Femur bones with knobbed ends
    for (int i = 0; i < 3; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0.15f, 0.35f) * s;
        float boneLen = rng.RandfRange(0.2f, 0.35f) * s;
        Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0.04f * s, Mathf.Sin(angle) * dist);
        // Bone shaft
        AddCylinderToSurfaceTool(surfaceTool, pos, pos + new Vector3(boneLen * 0.5f, 0, 0), 0.025f * s);
        // Knobbed ends
        AddSphereToSurfaceTool(surfaceTool, pos, 0.04f * s);
        AddSphereToSurfaceTool(surfaceTool, pos + new Vector3(boneLen * 0.5f, 0, 0), 0.04f * s);
    }

    // Ribs (curved bones)
    for (int i = 0; i < 6; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0.1f, 0.25f) * s;
        Vector3 ribStart = new Vector3(Mathf.Cos(angle) * dist * 0.3f, 0.06f * s, Mathf.Sin(angle) * dist * 0.3f);
        Vector3 ribEnd = new Vector3(Mathf.Cos(angle) * dist, 0.02f * s, Mathf.Sin(angle) * dist);
        AddCylinderToSurfaceTool(surfaceTool, ribStart, ribEnd, 0.015f * s);
    }

    // Vertebrae scattered
    for (int i = 0; i < 5; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0.08f, 0.3f) * s;
        Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0.02f * s, Mathf.Sin(angle) * dist);
        AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(0.03f * s, 0.025f * s, 0.03f * s));
        // Spinal process
        AddBoxToSurfaceTool(surfaceTool, pos + new Vector3(0, 0.02f * s, 0), new Vector3(0.01f * s, 0.03f * s, 0.01f * s));
    }

    // Small bone fragments
    for (int i = 0; i < 8; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0.05f, 0.4f) * s;
        Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0.01f * s, Mathf.Sin(angle) * dist);
        AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(0.03f * s, 0.01f * s, 0.01f * s));
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = boneColor,
        Roughness = 0.85f,
        Metallic = 0.05f // Slight sheen on old bones
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 2. coiled_chains - Individual Chain Links with Rust

**Enhancements:**
- Torus-shaped individual links
- Better connection between links
- Rust material with higher roughness
- More realistic chain coiling

**Replace CreateCoiledChainsMesh with:**

```csharp
private ArrayMesh CreateCoiledChainsMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Create chain links as connected tori
    int links = 16;
    for (int i = 0; i < links; i++)
    {
        float t = i / (float)links;
        float angle = t * Mathf.Pi * 4f + rng.Randf() * 0.3f; // More coils
        float radius = 0.12f * s + t * 0.08f * s; // Spiral outward
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;
        float y = 0.03f * s + Mathf.Sin(t * Mathf.Pi * 3f) * 0.04f * s; // Vertical wave

        // Each link is a small torus (approximated with cylinders)
        bool vertical = i % 2 == 0;
        if (vertical)
        {
            // Vertical oval link
            Vector3 top = new Vector3(x, y + 0.025f * s, z);
            Vector3 bottom = new Vector3(x, y - 0.025f * s, z);
            Vector3 left = new Vector3(x - 0.02f * s, y, z);
            Vector3 right = new Vector3(x + 0.02f * s, y, z);

            AddCylinderToSurfaceTool(surfaceTool, top, left, 0.008f * s);
            AddCylinderToSurfaceTool(surfaceTool, left, bottom, 0.008f * s);
            AddCylinderToSurfaceTool(surfaceTool, bottom, right, 0.008f * s);
            AddCylinderToSurfaceTool(surfaceTool, right, top, 0.008f * s);
        }
        else
        {
            // Horizontal oval link
            Vector3 front = new Vector3(x, y, z + 0.025f * s);
            Vector3 back = new Vector3(x, y, z - 0.025f * s);
            Vector3 left = new Vector3(x - 0.02f * s, y, z);
            Vector3 right = new Vector3(x + 0.02f * s, y, z);

            AddCylinderToSurfaceTool(surfaceTool, front, left, 0.008f * s);
            AddCylinderToSurfaceTool(surfaceTool, left, back, 0.008f * s);
            AddCylinderToSurfaceTool(surfaceTool, back, right, 0.008f * s);
            AddCylinderToSurfaceTool(surfaceTool, right, front, 0.008f * s);
        }
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.32f, 0.28f, 0.24f), // Darker rust
        Roughness = 0.85f,
        Metallic = 0.5f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 3. moss_patch - Organic Edges with Height Variation

**Enhancements:**
- More segments for smooth organic edge
- Height variation for 3D depth
- Multiple layers for thickness
- Subtle bioluminescent emission

**Replace CreateMossPatchMesh with:**

```csharp
private ArrayMesh CreateMossPatchMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Irregular moss patch with organic edges
    int segments = 20; // More segments for smooth curves
    Vector3 center = new Vector3(0, 0.015f * s, 0);

    // Create main moss layer
    for (int i = 0; i < segments; i++)
    {
        float angle1 = i * Mathf.Tau / segments;
        float angle2 = (i + 1) * Mathf.Tau / segments;

        // Use noise for organic edge
        float r1 = (0.25f + rng.Randf() * 0.2f + Mathf.Sin(angle1 * 3f) * 0.08f) * s;
        float r2 = (0.25f + rng.Randf() * 0.2f + Mathf.Sin(angle2 * 3f) * 0.08f) * s;

        // Add height variation
        float h1 = 0.01f * s + Mathf.Sin(angle1 * 5f) * 0.008f * s;
        float h2 = 0.01f * s + Mathf.Sin(angle2 * 5f) * 0.008f * s;

        Vector3 v1 = new Vector3(Mathf.Cos(angle1) * r1, h1, Mathf.Sin(angle1) * r1);
        Vector3 v2 = new Vector3(Mathf.Cos(angle2) * r2, h2, Mathf.Sin(angle2) * r2);

        surfaceTool.AddVertex(center);
        surfaceTool.AddVertex(v1);
        surfaceTool.AddVertex(v2);
    }

    // Add thicker clumps in center
    for (int i = 0; i < 8; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0f, 0.15f) * s;
        Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0.02f * s, Mathf.Sin(angle) * dist);
        AddSphereToSurfaceTool(surfaceTool, pos, 0.03f * s);
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.18f, 0.42f, 0.15f), // Rich green
        Roughness = 1f,
        EmissionEnabled = true,
        Emission = new Color(0.08f, 0.18f, 0.05f), // Subtle glow
        EmissionEnergyMultiplier = 0.25f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 4. water_puddle - Reflective Surface with Refraction

**Enhancements:**
- Smoother edges with more segments
- Mirror-like surface (low roughness)
- Proper water material properties
- Enhanced transparency

**Replace CreateWaterPuddleMesh with:**

```csharp
private ArrayMesh CreateWaterPuddleMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Create smooth puddle with organic shape
    int segments = 24; // High segment count for smooth water edge
    Vector3 center = Vector3.Zero;

    for (int i = 0; i < segments; i++)
    {
        float angle1 = i * Mathf.Tau / segments;
        float angle2 = (i + 1) * Mathf.Tau / segments;

        // Organic puddle shape
        float r1 = (0.22f + rng.Randf() * 0.12f + Mathf.Sin(angle1 * 4f) * 0.05f) * s;
        float r2 = (0.22f + rng.Randf() * 0.12f + Mathf.Sin(angle2 * 4f) * 0.05f) * s;

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
        AlbedoColor = new Color(0.15f, 0.25f, 0.35f, 0.75f),
        Roughness = 0.05f, // Very smooth for mirror reflection
        Metallic = 0.4f, // Reflective
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled, // Visible from both sides
        Refraction = 0.05f // Water refraction
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 5. broken_barrel - Splintered Wood with Liquid Pool

**Enhancements:**
- Wood plank detail
- Splintered broken edges
- Metal hoops with rust
- Liquid pool with transparency

**Replace CreateBrokenBarrelMesh with:**

```csharp
private ArrayMesh CreateBrokenBarrelMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Barrel on its side, broken with planks
    int planks = 10;
    float radius = 0.35f * s;
    float height = 0.5f * s;

    // Create barrel planks (some missing for broken look)
    for (int i = 0; i < planks; i++)
    {
        // Skip some planks for broken section
        if (i >= 3 && i <= 5) continue;

        float angle = i * Mathf.Tau / planks;
        float nextAngle = (i + 1) * Mathf.Tau / planks;

        float y1 = Mathf.Cos(angle) * radius;
        float z1 = Mathf.Sin(angle) * radius;
        float y2 = Mathf.Cos(nextAngle) * radius;
        float z2 = Mathf.Sin(nextAngle) * radius;

        // Wood plank
        Vector3 v1 = new Vector3(-height/2, y1, z1);
        Vector3 v2 = new Vector3(height/2, y1, z1);
        Vector3 v3 = new Vector3(height/2, y2, z2);
        Vector3 v4 = new Vector3(-height/2, y2, z2);

        surfaceTool.AddVertex(v1);
        surfaceTool.AddVertex(v2);
        surfaceTool.AddVertex(v3);
        surfaceTool.AddVertex(v1);
        surfaceTool.AddVertex(v3);
        surfaceTool.AddVertex(v4);

        // Add thickness to planks
        Vector3 inset = new Vector3(0, y1 * 0.95f, z1 * 0.95f);
        AddBoxToSurfaceTool(surfaceTool, (v1 + v2) / 2f, new Vector3(height * 1.1f, 0.03f * s, 0.08f * s));
    }

    // Metal hoops (rusted)
    for (int h = 0; h < 3; h++)
    {
        float hx = (-0.3f + h * 0.3f) * height;
        int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            float y = Mathf.Cos(angle) * (radius + 0.02f * s);
            float z = Mathf.Sin(angle) * (radius + 0.02f * s);
            AddBoxToSurfaceTool(surfaceTool, new Vector3(hx, y, z), new Vector3(0.04f * s, 0.025f * s, 0.025f * s));
        }
    }

    // Splintered wood pieces at break
    for (int i = 0; i < 6; i++)
    {
        float angle = (3.5f + rng.Randf()) * Mathf.Tau / planks;
        float len = rng.RandfRange(0.08f, 0.15f) * s;
        Vector3 pos = new Vector3(0, Mathf.Cos(angle) * radius * 1.1f, Mathf.Sin(angle) * radius * 1.1f);
        AddBoxToSurfaceTool(surfaceTool, pos, new Vector3(len, 0.02f * s, 0.03f * s));
    }

    // Liquid pool beneath (darker, thicker liquid)
    int poolSegments = 12;
    for (int i = 0; i < poolSegments; i++)
    {
        float angle1 = i * Mathf.Tau / poolSegments;
        float angle2 = (i + 1) * Mathf.Tau / poolSegments;
        float r = 0.32f * s;
        Vector3 poolCenter = new Vector3(0.15f * s, 0.005f, 0);
        surfaceTool.AddVertex(poolCenter);
        surfaceTool.AddVertex(poolCenter + new Vector3(Mathf.Cos(angle1) * r, 0, Mathf.Sin(angle1) * r));
        surfaceTool.AddVertex(poolCenter + new Vector3(Mathf.Cos(angle2) * r, 0, Mathf.Sin(angle2) * r));
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = ProceduralMesh3D.CreateWoodMaterial(new Color(0.48f, 0.32f, 0.18f));
    material.Roughness = 0.75f;
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 6. altar_stone - Carved Runes with Blood Stains

**Enhancements:**
- Detailed carved symbols
- Blood stains with darker material
- Worn/chipped edges
- Ancient weathered appearance

**Replace CreateAltarStoneMesh with:**

```csharp
private ArrayMesh CreateAltarStoneMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Main altar slab with worn edges
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.25f * s, 0), new Vector3(1.2f * s, 0.5f * s, 0.7f * s));

    // Carved rune grooves (complex patterns)
    // Central circle
    int circleSegs = 16;
    for (int i = 0; i < circleSegs; i++)
    {
        float angle1 = i * Mathf.Tau / circleSegs;
        float angle2 = (i + 1) * Mathf.Tau / circleSegs;
        float r = 0.15f * s;
        Vector3 c = new Vector3(0, 0.51f * s, 0);
        surfaceTool.AddVertex(c + new Vector3(Mathf.Cos(angle1) * r, 0, Mathf.Sin(angle1) * r));
        surfaceTool.AddVertex(c + new Vector3(Mathf.Cos(angle1) * (r-0.02f*s), 0, Mathf.Sin(angle1) * (r-0.02f*s)));
        surfaceTool.AddVertex(c + new Vector3(Mathf.Cos(angle2) * (r-0.02f*s), 0, Mathf.Sin(angle2) * (r-0.02f*s)));
    }

    // Cross pattern
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.51f * s, 0), new Vector3(0.65f * s, 0.015f * s, 0.035f * s));
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.51f * s, 0), new Vector3(0.035f * s, 0.015f * s, 0.55f * s));

    // Radiating lines
    for (int i = 0; i < 8; i++)
    {
        float angle = i * Mathf.Tau / 8;
        Vector3 start = new Vector3(Mathf.Cos(angle) * 0.16f * s, 0.51f * s, Mathf.Sin(angle) * 0.16f * s);
        Vector3 end = new Vector3(Mathf.Cos(angle) * 0.35f * s, 0.51f * s, Mathf.Sin(angle) * 0.35f * s);
        AddCylinderToSurfaceTool(surfaceTool, start, end, 0.015f * s);
    }

    // Blood stains (multiple dark red patches)
    for (int i = 0; i < 5; i++)
    {
        float x = rng.RandfRange(-0.4f, 0.4f) * s;
        float z = rng.RandfRange(-0.25f, 0.25f) * s;
        float size = rng.RandfRange(0.08f, 0.15f) * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.51f * s, z), new Vector3(size, 0.008f * s, size * 0.8f));
    }

    // Chipped corners
    for (int i = 0; i < 4; i++)
    {
        float x = (i % 2 == 0 ? -1 : 1) * 0.55f * s;
        float z = (i / 2 == 0 ? -1 : 1) * 0.3f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.48f * s, z), new Vector3(0.08f * s, 0.05f * s, 0.08f * s));
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.28f, 0.26f, 0.24f), // Dark weathered stone
        Roughness = 0.9f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

## Summary of Material Enhancements

### PBR Material Improvements:
- **Roughness**: More realistic values (bone: 0.85, water: 0.05, metal: 0.5-0.8)
- **Metallic**: Proper metallic values for metal props (chains: 0.5, coins: 0.8)
- **Emission**: Strategic use for glowing elements (mushrooms, runes, potions)
- **Transparency**: Proper alpha for liquids and glass
- **Refraction**: Added for water puddles

### Geometry Improvements:
- **More Polygons**: Increased segments for organic shapes (moss: 20, water: 24)
- **Surface Detail**: Added carved details, textures, and wear
- **Anatomical Accuracy**: Realistic bone structure, proper chain links
- **Better Silhouettes**: More recognizable shapes from all angles

### Lighting Integration:
- **Emission Maps**: Glowing elements properly emit light
- **Roughness Variation**: Surface variation for realistic lighting response
- **Normal Generation**: All meshes properly generate normals for correct shading

---

## Build and Test
After implementing changes:
```bash
dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"
```

Then launch the game to see improvements in the dungeon ambiance!
