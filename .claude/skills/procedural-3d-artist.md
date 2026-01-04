---
name: procedural-3d-artist
description: Expert procedural 3D mesh artist for Godot 4.3 C#. Creates monsters, creatures, dungeon props, weapons, and environmental assets using CylinderMesh, BoxMesh, and SphereMesh primitives. Specializes in fantasy and sci-fi RPG aesthetics with proper body geometry, limb animation support, and performance optimization.
---

# Procedural 3D Artist - Fantasy & Sci-Fi RPG Assets

Expert at creating low-poly procedural meshes for monsters, creatures, props, and dungeon environments using Godot 4.3 primitives.

---

## Core Primitives (ONLY use these)

| Mesh Type | Use For | Critical Settings |
|-----------|---------|-------------------|
| `CylinderMesh` | Bodies, limbs, necks, tails, handles | `TopRadius`, `BottomRadius`, `Height`, `RadialSegments` |
| `BoxMesh` | Armor, platforms, crystals, crates | `Size` as Vector3 |
| `SphereMesh` | Heads, eyes, joints, orbs | **ALWAYS set `Height = 2 × Radius`** |

**NEVER use:** `PrismMesh`, `TorusMesh`, `CapsuleMesh` - they render incorrectly in SubViewport contexts.

---

## Body Geometry System

### BodyGeometry Struct
```csharp
private struct BodyGeometry
{
    public float CenterY;   // Y position of mesh center
    public float Radius;    // Mesh radius
    public float Height;    // Mesh height
    public float ScaleY;    // Y scale factor

    public float Top => CenterY + (Height * ScaleY / 2f);
    public float Bottom => CenterY - (Height * ScaleY / 2f);
    public float ShoulderY => CenterY + (Height * ScaleY * 0.3f);
    public float HipY => CenterY - (Height * ScaleY * 0.3f);
}
```

### Attachment Rules
- **Adjacent meshes overlap 15-25%** of smaller dimension
- **Joint spheres** at connections (radius = 30-50% of limb radius)
- **Head position**: `bodyGeom.Top + headHeight/2 - neckOverlap`
- **Arm attachment**: `bodyGeom.ShoulderY`
- **Leg attachment**: `bodyGeom.HipY`

---

## LimbNodes for Animation

```csharp
public class LimbNodes
{
    public Node3D? Head;
    public Node3D? LeftArm, RightArm;
    public Node3D? LeftLeg, RightLeg;
    public Node3D? Weapon;
    public Node3D? Torch;
    public Node3D? Tail;
}
```

**Every humanoid mesh MUST populate:** `Head`, `LeftArm`, `RightArm`, `LeftLeg`, `RightLeg`

---

## Eye System (CRITICAL)

### SphereMesh Rendering Rule
```csharp
// WRONG - renders as flat disc
var eye = new SphereMesh { Radius = 0.05f };

// CORRECT - always set Height
var eye = new SphereMesh {
    Radius = 0.05f,
    Height = 0.10f,  // ALWAYS 2 × Radius
    RadialSegments = 16,
    Rings = 12
};
```

### Eye Proportions by Creature Type

| Type | Eye Radius | Pupil | Eye Y | Eye X | Eye Z |
|------|------------|-------|-------|-------|-------|
| Humanoid | 19% of head | 45% of eye | 19% up | 38% out | 76% forward |
| Cute | 24% of head | 50% of eye | 22% up | 32% out | 72% forward |
| Menacing | 16% of head | 40% of eye | 18% up | 40% out | 78% forward |
| Beast | 22% of head | 45% of eye | 15% up | 42% out | 75% forward |
| Arachnid | 15% of head | 50% of eye | 25% up | 45% out | 80% forward |

### Eye Visibility Rule
**Eyes MUST protrude from head surface** (Z >= headRadius × 0.7). Eyes hidden inside head geometry are invisible.

---

## Monster Creation Template

```csharp
private static void CreateMyMonsterMesh(Node3D parent, LimbNodes limbs, Color? colorOverride, LODLevel lod = LODLevel.High)
{
    float scale = 1.0f;
    Color skinColor = colorOverride ?? new Color(0.6f, 0.5f, 0.4f);
    var mat = new StandardMaterial3D { AlbedoColor = skinColor };

    // 1. BODY - Ground level reference
    float bodyHeight = 0.5f * scale;
    float bodyRadius = 0.2f * scale;
    float bodyCenterY = bodyHeight / 2f; // Bottom at Y=0
    var bodyGeom = new BodyGeometry(bodyCenterY, bodyRadius, bodyHeight, 1f);

    var body = new MeshInstance3D();
    body.Mesh = new CylinderMesh {
        TopRadius = bodyRadius * 0.9f,
        BottomRadius = bodyRadius,
        Height = bodyHeight,
        RadialSegments = lod == LODLevel.High ? 12 : 8
    };
    body.Position = new Vector3(0, bodyCenterY, 0);
    body.MaterialOverride = mat;
    parent.AddChild(body);

    // 2. HEAD NODE (for animation)
    float headRadius = 0.15f * scale;
    float neckOverlap = CalculateOverlap(headRadius);
    float headCenterY = bodyGeom.Top + headRadius - neckOverlap;

    var headNode = new Node3D();
    headNode.Position = new Vector3(0, headCenterY, 0);
    parent.AddChild(headNode);
    limbs.Head = headNode;

    // Head mesh
    var head = new MeshInstance3D();
    head.Mesh = new SphereMesh {
        Radius = headRadius,
        Height = headRadius * 2f, // CRITICAL
        RadialSegments = 16,
        Rings = 12
    };
    head.MaterialOverride = mat;
    headNode.AddChild(head);

    // 3. EYES (protruding from head)
    AddEyesToHead(headNode, headRadius, scale, EyeProportions.Humanoid);

    // 4. NECK JOINT
    var neckJoint = CreateJointMesh(mat, bodyRadius * 0.4f);
    neckJoint.Position = new Vector3(0, bodyGeom.Top - neckOverlap * 0.5f, 0);
    parent.AddChild(neckJoint);

    // 5. ARMS (at shoulder height)
    CreateArm(parent, limbs, mat, scale, bodyGeom, isLeft: true);
    CreateArm(parent, limbs, mat, scale, bodyGeom, isLeft: false);

    // 6. LEGS (at hip height)
    CreateLeg(parent, limbs, mat, scale, bodyGeom, isLeft: true);
    CreateLeg(parent, limbs, mat, scale, bodyGeom, isLeft: false);
}
```

---

## Creature Archetypes

### Humanoid (Goblins, Skeletons, Knights)
- Upright torso (CylinderMesh, tapered top)
- Head on top, arms at shoulders, legs at hips
- Weapon attachment point on right arm
- Scale: 0.7-1.2 for regular, 1.5-2.5 for bosses

### Quadruped (Wolves, Badlamas, Horses)
- Horizontal body cylinder
- 4 legs, head on front neck segment
- Optional tail on back
- Lower body Y, legs extend to ground

### Arachnid (Spiders, Clockwork Spiders)
- Spherical/oval body
- 8 legs arranged radially (±30°, ±60°, ±90°, ±120°)
- Multiple eyes in cluster
- Optional mandibles

### Serpentine (Dragons, Worms)
- Segmented body chain (parent-child hierarchy)
- Each segment 15% overlap with previous
- Head counter-rotation to face forward
- Wings as flat BoxMesh with rotation

### Amorphous (Slimes, Gelatinous Cube)
- Translucent material (`Transparency = Alpha`)
- Deformed sphere or cube
- Eyes floating inside or protruding
- No limb nodes needed

### Mechanical (Drones, Robots)
- Sharp edges (BoxMesh primary)
- Metallic materials with emission
- Antenna/sensor protrusions
- Glowing eyes/lights

---

## Prop Creation (Cosmetic3D)

### Dungeon Props Pattern
```csharp
case "barrel":
    mesh = CreateBarrelMesh(scale, PrimaryColor, SecondaryColor);
    collisionSize = new Vector3(0.4f, 0.6f, 0.4f) * scale;
    break;

case "torch":
    mesh = CreateTorchMesh(scale, PrimaryColor);
    HasLighting = true;
    LightColor = new Color(1f, 0.7f, 0.3f);
    break;
```

### Prop Categories

| Category | Props | Materials |
|----------|-------|-----------|
| Storage | barrel, crate, chest, pot | Wood, metal bands |
| Furniture | table, chair, bookshelf | Wood, cloth |
| Lighting | torch, brazier, crystal | Emissive, OmniLight3D |
| Debris | bone_pile, rubble, broken_barrel | Stone, bone white |
| Nature | moss_patch, mushrooms, vines | Organic greens |
| Magic | altar, rune, cauldron | Dark stone, glowing accents |
| Hazard | spike_trap, blood_pool | Metal, red translucent |

---

## Materials

### Standard Material Setup
```csharp
var mat = new StandardMaterial3D {
    AlbedoColor = color,
    Metallic = 0f,           // 0 for organic, 0.8+ for metal
    Roughness = 0.7f,        // Higher = less shiny
    CullMode = BaseMaterial3D.CullModeEnum.Back
};
```

### Translucent (Slimes, Ghosts)
```csharp
mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
mat.AlbedoColor = new Color(0.2f, 0.8f, 0.3f, 0.6f); // RGBA with alpha
```

### Emissive (Glowing eyes, Magic)
```csharp
mat.EmissionEnabled = true;
mat.Emission = glowColor;
mat.EmissionEnergyMultiplier = 2f;
```

### Billboard (Health bars)
```csharp
mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
mat.NoDepthTest = false;
```

---

## Performance Rules

1. **LOD Levels**: High=12 segments, Medium=8, Low=6
2. **Share materials** via static factory methods
3. **No shadows** on small details or moving lights
4. **Occlusion**: Add to occluder for wall-like props
5. **Sleep system**: Skip processing for distant objects

---

## Common Mistakes to AVOID

| Mistake | Problem | Fix |
|---------|---------|-----|
| SphereMesh without Height | Flat disc | `Height = 2 × Radius` |
| Eyes inside head | Invisible | Z >= headRadius × 0.7 |
| `LookAt()` before AddChild | Crash | Use manual Atan2 rotation |
| PrismMesh for tips | Wrong render | Tapered CylinderMesh |
| Flat hierarchy for chains | Position drift | Parent-child segments |
| Forgetting joint spheres | Visible gaps | Add at all connections |

---

## Rotation & Facing

### Face Target (Enemy AI)
```csharp
// DON'T use LookAt() - faces wrong direction
// DO use Atan2:
Vector3 dir = (target - GlobalPosition).Normalized();
dir.Y = 0;
float angle = Mathf.Atan2(dir.X, dir.Z);
Rotation = new Vector3(0, angle, 0);
```

### Cylinder Alignment
```csharp
// Cylinders are Y-axis aligned by default
// To align with Z-axis (forward):
cylinder.RotationDegrees = new Vector3(90, 0, 0);
```

---

## Workflow

1. **Sketch anatomy** - Identify body parts and connections
2. **Define BodyGeometry** - Calculate attachment points
3. **Build bottom-up** - Legs → body → arms → head
4. **Add joint spheres** - At all connections
5. **Create LimbNodes** - For animation system
6. **Add eyes** - Using proportions for creature type
7. **Test in EditorScreen3D** - Preview before gameplay
8. **Verify animation** - All 6 types work (idle, walk, run, attack, hit, die)
