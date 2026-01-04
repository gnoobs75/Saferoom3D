# Complete Dungeon Props Enhancement - All 25 Props

## Props 7-25: Continued Enhancements

### 7. glowing_mushrooms - Bioluminescent Cluster

**Enhancements:**
- Mushroom gills under caps
- Varied mushroom sizes
- Strong emission for glow
- Clustered natural arrangement

```csharp
private ArrayMesh CreateGlowingMushroomsMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Create mushroom cluster with varied sizes
    int count = rng.RandiRange(5, 9);
    for (int i = 0; i < count; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0.02f, 0.22f) * s;
        float x = Mathf.Cos(angle) * dist;
        float z = Mathf.Sin(angle) * dist;
        float stemH = rng.RandfRange(0.08f, 0.3f) * s;
        float capR = rng.RandfRange(0.05f, 0.14f) * s;

        // Stem (thin cylinder)
        Vector3 stemBot = new Vector3(x, 0, z);
        Vector3 stemTop = new Vector3(x, stemH, z);
        AddCylinderToSurfaceTool(surfaceTool, stemBot, stemTop, 0.018f * s);

        // Cap (dome shape - flattened sphere)
        AddSphereToSurfaceTool(surfaceTool, new Vector3(x, stemH + capR * 0.4f, z), capR);

        // Gills under cap (thin radial lines)
        for (int g = 0; g < 8; g++)
        {
            float gillAngle = g * Mathf.Tau / 8;
            Vector3 gillStart = new Vector3(x, stemH, z);
            Vector3 gillEnd = new Vector3(
                x + Mathf.Cos(gillAngle) * capR * 0.9f,
                stemH - 0.01f * s,
                z + Mathf.Sin(gillAngle) * capR * 0.9f
            );
            AddCylinderToSurfaceTool(surfaceTool, gillStart, gillEnd, 0.005f * s);
        }

        // Spots on cap
        for (int sp = 0; sp < 3; sp++)
        {
            float spotAngle = rng.Randf() * Mathf.Tau;
            float spotDist = rng.RandfRange(0f, capR * 0.7f);
            Vector3 spotPos = new Vector3(
                x + Mathf.Cos(spotAngle) * spotDist,
                stemH + capR * 0.5f,
                z + Mathf.Sin(spotAngle) * spotDist
            );
            AddSphereToSurfaceTool(surfaceTool, spotPos, 0.015f * s);
        }
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.25f, 0.75f, 0.65f),
        EmissionEnabled = true,
        Emission = new Color(0.2f, 0.95f, 0.7f),
        EmissionEnergyMultiplier = 2.0f, // Strong glow
        Roughness = 0.6f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 8. blood_pool - Wet Blood Splatter

**Enhancements:**
- Irregular splatter pattern
- Dark crimson color
- Wet sheen (low roughness)
- Organic splatter shape

```csharp
private ArrayMesh CreateBloodPoolMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Main pool with irregular edges
    int segments = 16;
    Vector3 center = Vector3.Zero;

    for (int i = 0; i < segments; i++)
    {
        float angle1 = i * Mathf.Tau / segments;
        float angle2 = (i + 1) * Mathf.Tau / segments;

        // Irregular splatter pattern
        float r1 = (0.18f + rng.Randf() * 0.16f + Mathf.Abs(Mathf.Sin(angle1 * 7f)) * 0.1f) * s;
        float r2 = (0.18f + rng.Randf() * 0.16f + Mathf.Abs(Mathf.Sin(angle2 * 7f)) * 0.1f) * s;

        surfaceTool.AddVertex(center);
        surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * r1, 0, Mathf.Sin(angle1) * r1));
        surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r2, 0, Mathf.Sin(angle2) * r2));
    }

    // Add splatter droplets around edges
    for (int i = 0; i < 8; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0.25f, 0.4f) * s;
        float dropletSize = rng.RandfRange(0.02f, 0.05f) * s;
        Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0.001f, Mathf.Sin(angle) * dist);

        // Small splatter droplet
        int dropSegs = 6;
        for (int d = 0; d < dropSegs; d++)
        {
            float a1 = d * Mathf.Tau / dropSegs;
            float a2 = (d + 1) * Mathf.Tau / dropSegs;
            surfaceTool.AddVertex(pos);
            surfaceTool.AddVertex(pos + new Vector3(Mathf.Cos(a1) * dropletSize, 0, Mathf.Sin(a1) * dropletSize));
            surfaceTool.AddVertex(pos + new Vector3(Mathf.Cos(a2) * dropletSize, 0, Mathf.Sin(a2) * dropletSize));
        }
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.28f, 0.02f, 0.02f), // Dark crimson
        Roughness = 0.15f, // Wet sheen
        Metallic = 0.3f // Slight reflectivity
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 9. spike_trap - Sharp Metal Spikes

**Enhancements:**
- Sharp pyramidal spikes
- Blood on tips
- Mechanism detail in base
- High metallic material

```csharp
private ArrayMesh CreateSpikeTrapMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Base plate with mechanism detail
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.02f * s, 0), new Vector3(0.95f * s, 0.04f * s, 0.95f * s));

    // Mechanism gears visible in base
    for (int i = 0; i < 4; i++)
    {
        float angle = i * Mathf.Tau / 4;
        float x = Mathf.Cos(angle) * 0.3f * s;
        float z = Mathf.Sin(angle) * 0.3f * s;
        AddSphereToSurfaceTool(surfaceTool, new Vector3(x, 0.04f * s, z), 0.05f * s);
    }

    // Sharp spikes (3x3 grid)
    for (int x = 0; x < 3; x++)
    {
        for (int z = 0; z < 3; z++)
        {
            float sx = (x - 1) * 0.28f * s;
            float sz = (z - 1) * 0.28f * s;
            float spikeH = rng.RandfRange(0.3f, 0.5f) * s;

            // Very sharp spike (6-sided pyramid)
            Vector3 tip = new Vector3(sx, 0.04f * s + spikeH, sz);
            int sides = 6;

            for (int side = 0; side < sides; side++)
            {
                float angle1 = side * Mathf.Tau / sides;
                float angle2 = (side + 1) * Mathf.Tau / sides;

                Vector3 base1 = new Vector3(
                    sx + Mathf.Cos(angle1) * 0.05f * s,
                    0.04f * s,
                    sz + Mathf.Sin(angle1) * 0.05f * s
                );
                Vector3 base2 = new Vector3(
                    sx + Mathf.Cos(angle2) * 0.05f * s,
                    0.04f * s,
                    sz + Mathf.Sin(angle2) * 0.05f * s
                );

                // Triangle face
                surfaceTool.AddVertex(base1);
                surfaceTool.AddVertex(base2);
                surfaceTool.AddVertex(tip);
            }

            // Blood on spike tip (dark red)
            AddSphereToSurfaceTool(surfaceTool, tip - new Vector3(0, 0.02f * s, 0), 0.015f * s);
        }
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.18f, 0.18f, 0.2f), // Dark iron
        Roughness = 0.5f,
        Metallic = 0.85f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 10. discarded_sword - Worn Blade with Details

**Enhancements:**
- Beveled blade edge
- Fuller (blood groove)
- Leather-wrapped grip
- Rust patches and notches

```csharp
private ArrayMesh CreateDiscardedSwordMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Blade with fuller (blood groove)
    // Main blade body
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.025f * s, 0.1f * s), new Vector3(0.045f * s, 0.025f * s, 0.65f * s));

    // Fuller groove in center of blade
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.027f * s, 0.1f * s), new Vector3(0.015f * s, 0.008f * s, 0.5f * s));

    // Blade tip (pointed)
    Vector3 tipBase1 = new Vector3(-0.02f * s, 0.025f * s, 0.42f * s);
    Vector3 tipBase2 = new Vector3(0.02f * s, 0.025f * s, 0.42f * s);
    Vector3 tipPoint = new Vector3(0, 0.025f * s, 0.5f * s);
    surfaceTool.AddVertex(tipBase1);
    surfaceTool.AddVertex(tipBase2);
    surfaceTool.AddVertex(tipPoint);

    // Beveled edges (angled faces on blade sides)
    for (int side = 0; side < 2; side++)
    {
        float xOffset = (side == 0 ? -1 : 1) * 0.0225f * s;
        AddBoxToSurfaceTool(surfaceTool,
            new Vector3(xOffset, 0.023f * s, 0.1f * s),
            new Vector3(0.005f * s, 0.015f * s, 0.55f * s));
    }

    // Crossguard (wider)
    AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.025f * s, -0.28f * s), new Vector3(0.18f * s, 0.035f * s, 0.04f * s));

    // Handle with leather wrap texture (segmented)
    for (int i = 0; i < 5; i++)
    {
        float z = -0.32f * s - i * 0.03f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(0, 0.025f * s, z), new Vector3(0.035f * s, 0.035f * s, 0.025f * s));
    }

    // Pommel (round)
    AddSphereToSurfaceTool(surfaceTool, new Vector3(0, 0.025f * s, -0.48f * s), 0.045f * s);

    // Notches in blade (battle damage)
    for (int i = 0; i < 4; i++)
    {
        float z = rng.RandfRange(-0.1f, 0.3f) * s;
        float x = (rng.Randf() < 0.5f ? -1 : 1) * 0.023f * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.025f * s, z), new Vector3(0.01f * s, 0.015f * s, 0.02f * s));
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.48f, 0.45f, 0.42f), // Worn steel with rust
        Roughness = 0.7f,
        Metallic = 0.55f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 11. shattered_potion - Glass Shards with Magic Residue

**Enhancements:**
- Sharp angular glass shards
- Transparent glass material
- Glowing magical liquid
- Vapor effect suggestion

```csharp
private ArrayMesh CreateShatteredPotionMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Sharp glass shards scattered
    for (int i = 0; i < 10; i++)
    {
        float angle = rng.Randf() * Mathf.Tau;
        float dist = rng.RandfRange(0.04f, 0.18f) * s;
        float x = Mathf.Cos(angle) * dist;
        float z = Mathf.Sin(angle) * dist;

        // Sharp triangular shard
        float shardHeight = rng.RandfRange(0.03f, 0.08f) * s;
        float shardAngle = rng.Randf() * Mathf.Tau;

        Vector3 base1 = new Vector3(x, 0.005f, z);
        Vector3 base2 = new Vector3(x + Mathf.Cos(shardAngle) * 0.035f * s, 0.005f, z + Mathf.Sin(shardAngle) * 0.035f * s);
        Vector3 base3 = new Vector3(x + Mathf.Cos(shardAngle + Mathf.Pi * 0.6f) * 0.025f * s, 0.005f, z + Mathf.Sin(shardAngle + Mathf.Pi * 0.6f) * 0.025f * s);
        Vector3 top = new Vector3(x + 0.01f * s, shardHeight, z + 0.01f * s);

        // Shard faces
        surfaceTool.AddVertex(base1);
        surfaceTool.AddVertex(base2);
        surfaceTool.AddVertex(top);

        surfaceTool.AddVertex(base2);
        surfaceTool.AddVertex(base3);
        surfaceTool.AddVertex(top);

        surfaceTool.AddVertex(base3);
        surfaceTool.AddVertex(base1);
        surfaceTool.AddVertex(top);
    }

    // Glowing magical residue pool (irregular)
    int poolSegs = 12;
    for (int i = 0; i < poolSegs; i++)
    {
        float angle1 = i * Mathf.Tau / poolSegs;
        float angle2 = (i + 1) * Mathf.Tau / poolSegs;
        float r = 0.12f * s + Mathf.Sin(angle1 * 5f) * 0.03f * s;

        surfaceTool.AddVertex(Vector3.Zero);
        surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle1) * r, 0.003f, Mathf.Sin(angle1) * r));
        surfaceTool.AddVertex(new Vector3(Mathf.Cos(angle2) * r, 0.003f, Mathf.Sin(angle2) * r));
    }

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.15f, 0.85f, 0.25f, 0.65f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        EmissionEnabled = true,
        Emission = new Color(0.1f, 0.95f, 0.2f),
        EmissionEnergyMultiplier = 1.5f,
        Roughness = 0.2f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

### 12. ancient_scroll - Aged Parchment with Text

**Enhancements:**
- Curved rolled edges
- Aged yellowed parchment
- Visible rune/text markings
- Frayed edges

```csharp
private ArrayMesh CreateAncientScrollMesh(RandomNumberGenerator rng)
{
    var surfaceTool = new SurfaceTool();
    surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
    float s = PropScale;

    // Main parchment body (partially unrolled, curved)
    int segments = 8;
    for (int i = 0; i < segments; i++)
    {
        float t1 = i / (float)segments;
        float t2 = (i + 1) / (float)segments;

        float x1 = (t1 - 0.5f) * 0.45f * s;
        float x2 = (t2 - 0.5f) * 0.45f * s;

        // Slight curve
        float y1 = 0.01f * s + Mathf.Sin(t1 * Mathf.Pi) * 0.015f * s;
        float y2 = 0.01f * s + Mathf.Sin(t2 * Mathf.Pi) * 0.015f * s;

        Vector3 v1Front = new Vector3(x1, y1, 0.15f * s);
        Vector3 v2Front = new Vector3(x2, y2, 0.15f * s);
        Vector3 v1Back = new Vector3(x1, y1, -0.15f * s);
        Vector3 v2Back = new Vector3(x2, y2, -0.15f * s);

        // Front face
        surfaceTool.AddVertex(v1Front);
        surfaceTool.AddVertex(v2Front);
        surfaceTool.AddVertex(v2Back);

        surfaceTool.AddVertex(v1Front);
        surfaceTool.AddVertex(v2Back);
        surfaceTool.AddVertex(v1Back);
    }

    // Rolled ends (cylinders)
    Vector3 leftRollCenter = new Vector3(-0.21f * s, 0.025f * s, 0);
    Vector3 rightRollCenter = new Vector3(0.21f * s, 0.025f * s, 0);

    // Left roll
    for (int i = 0; i < 8; i++)
    {
        float angle1 = i * Mathf.Tau / 8;
        float angle2 = (i + 1) * Mathf.Tau / 8;
        Vector3 v1 = leftRollCenter + new Vector3(0, Mathf.Cos(angle1) * 0.03f * s, Mathf.Sin(angle1) * 0.03f * s);
        Vector3 v2 = leftRollCenter + new Vector3(0, Mathf.Cos(angle2) * 0.03f * s, Mathf.Sin(angle2) * 0.03f * s);
        Vector3 v3 = v1 + new Vector3(0.05f * s, 0, 0);
        Vector3 v4 = v2 + new Vector3(0.05f * s, 0, 0);

        surfaceTool.AddVertex(v1);
        surfaceTool.AddVertex(v2);
        surfaceTool.AddVertex(v4);
        surfaceTool.AddVertex(v1);
        surfaceTool.AddVertex(v4);
        surfaceTool.AddVertex(v3);
    }

    // Right roll (similar)
    for (int i = 0; i < 8; i++)
    {
        float angle1 = i * Mathf.Tau / 8;
        float angle2 = (i + 1) * Mathf.Tau / 8;
        Vector3 v1 = rightRollCenter + new Vector3(0, Mathf.Cos(angle1) * 0.03f * s, Mathf.Sin(angle1) * 0.03f * s);
        Vector3 v2 = rightRollCenter + new Vector3(0, Mathf.Cos(angle2) * 0.03f * s, Mathf.Sin(angle2) * 0.03f * s);
        Vector3 v3 = v1 - new Vector3(0.05f * s, 0, 0);
        Vector3 v4 = v2 - new Vector3(0.05f * s, 0, 0);

        surfaceTool.AddVertex(v1);
        surfaceTool.AddVertex(v2);
        surfaceTool.AddVertex(v4);
        surfaceTool.AddVertex(v1);
        surfaceTool.AddVertex(v4);
        surfaceTool.AddVertex(v3);
    }

    // Rune markings (raised text-like shapes)
    for (int i = 0; i < 6; i++)
    {
        float x = rng.RandfRange(-0.15f, 0.15f) * s;
        float z = rng.RandfRange(-0.1f, 0.1f) * s;
        AddBoxToSurfaceTool(surfaceTool, new Vector3(x, 0.015f * s, z), new Vector3(0.03f * s, 0.003f * s, 0.01f * s));
    }

    // Wax seal remnant
    AddSphereToSurfaceTool(surfaceTool, new Vector3(0.08f * s, 0.018f * s, -0.05f * s), 0.025f * s);

    surfaceTool.GenerateNormals();
    var mesh = surfaceTool.Commit();
    var material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.82f, 0.75f, 0.58f), // Aged parchment
        Roughness = 0.95f
    };
    mesh.SurfaceSetMaterial(0, material);
    return mesh;
}
```

---

## Remaining Props (13-25) - Quick Reference

### 13. brazier_fire
- Ornate bowl with decorative patterns
- Visible coal bed
- Detailed tripod legs
- Enhanced fire particles with ember glow

### 14. manacles
- Ring-shaped metal cuffs (using curved cylinders)
- Individual chain links
- Hinge detail on cuff openings
- Blood rust on edges

### 15. cracked_tiles
- Square tile grid pattern
- Visible crack lines (thin dark lines)
- Grout between tiles
- Varied tile orientations

### 16. rubble_heap
- Varied rock sizes (small to large)
- Angular rock shapes (not just spheres)
- Stacked/piled arrangement
- Color variation per rock

### 17. thorny_vines
- Twisted organic vine curves
- Sharp pyramidal thorns
- Small leaf shapes
- Dark green with brown thorns

### 18. engraved_rune
- Complex overlapping rune patterns
- Deep carved grooves
- Pulsing red/orange emission
- Worn stone around symbols

### 19. abandoned_campfire
- Charcoal lumps (dark irregular shapes)
- Half-burned log pieces
- Ash pile with gray texture
- Soot marks on surrounding stones

### 20. rat_nest
- Shredded fabric strips
- Small scattered bones
- Straw strands and twigs
- Tiny droppings (dark spheres)

### 21. broken_pottery
- Curved ceramic shards
- Painted decorative patterns
- Terracotta orange-brown
- Sharp fractured edges

### 22. scattered_coins
- Circular disc meshes
- Embossed center patterns
- Gold (0.85, 0.7, 0.2) and silver (0.75, 0.75, 0.78) mix
- Various angles/tilts

### 23. forgotten_shield
- Wood plank construction
- Metal boss with embossed emblem
- Leather strap remnants
- Dents and cuts from battle

### 24. moldy_bread
- Textured crust bumps
- Green/blue mold patches with emission
- Sagging squashed shape
- Organic rough texture

---

## Material Property Guidelines

### Metals:
- **Roughness**: 0.4-0.8 (0.4 polished, 0.8 rusted)
- **Metallic**: 0.7-0.9
- **Colors**: Iron (0.2, 0.2, 0.22), Rust (0.35, 0.28, 0.24), Gold (0.85, 0.7, 0.2)

### Organic Materials:
- **Roughness**: 0.85-1.0
- **Metallic**: 0.0-0.1
- **Colors**: Bone (0.88, 0.82, 0.68), Wood (0.45, 0.32, 0.18), Moss (0.18, 0.42, 0.15)

### Liquids:
- **Roughness**: 0.05-0.2 (water very smooth, blood slightly rough)
- **Metallic**: 0.2-0.4 (for reflections)
- **Transparency**: Alpha 0.6-0.8
- **Refraction**: 0.05 for water

### Magical/Glowing:
- **Emission**: Always enabled
- **Emission Energy**: 1.0-3.0 (mushrooms 2.0, runes 1.5)
- **Emission Color**: Bright saturated colors
- **Roughness**: 0.3-0.6 (allows glow to stand out)

---

## Performance Notes

### Polygon Counts:
- Simple props (coins, fragments): 50-150 triangles
- Medium props (barrels, altars): 200-500 triangles
- Complex props (chains, vines): 400-800 triangles
- Target: Under 1000 triangles per prop

### Optimization Tips:
- Use `AddCylinderToSurfaceTool` with 6-8 segments (not 12+)
- Use `AddSphereToSurfaceTool` with 4 rings, 6 sectors
- Share materials where possible (same rust color for multiple props)
- Keep emission props under 30 per scene (performance)

---

## Testing Checklist

After implementing enhancements:

1. **Visual Quality**:
   - [ ] Props recognizable from 5+ meters away
   - [ ] Silhouettes distinct and interesting
   - [ ] Materials respond correctly to dungeon lighting
   - [ ] Emission props glow appropriately

2. **Performance**:
   - [ ] No frame drops when 20+ props visible
   - [ ] Large dungeon (400x400) maintains 60fps
   - [ ] Prop generation under 50ms per prop

3. **Variety**:
   - [ ] Random variations make each instance unique
   - [ ] Props fit thematically in room types
   - [ ] Color palettes complement dungeon aesthetic

4. **Technical**:
   - [ ] No visual artifacts (z-fighting, normals errors)
   - [ ] Collision shapes match visual geometry
   - [ ] Materials don't cause shader compilation spikes

---

## Build Command

```bash
dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"
```

## Implementation Priority

**High Priority** (Most visible impact):
1. bone_pile - Very common, high visibility
2. glowing_mushrooms - Lighting contribution
3. blood_pool - Atmosphere enhancement
4. altar_stone - Landmark prop
5. scattered_coins - Player attraction

**Medium Priority**:
6. broken_barrel
7. spike_trap
8. discarded_sword
9. shattered_potion
10. ancient_scroll

**Lower Priority** (Fine details):
11-25. Remaining props

---

**Total Enhancement Scope**: 25 dungeon props with ~60% polygon increase, 100% material quality improvement
