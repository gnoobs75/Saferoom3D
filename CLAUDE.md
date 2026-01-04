# CLAUDE3D.md - Safe Room 3D Project Guide

## Important: Claude Code Working Guidelines

When working with Claude Code on this project, follow these guidelines to avoid context/token limit issues:

### Work in Smaller Blocks
1. **Break large files into smaller edits** - When editing large files (>500 lines), make multiple smaller edits rather than rewriting entire sections
2. **Read file sections** - Use `offset` and `limit` parameters when reading large files instead of loading the entire file
3. **Report progress frequently** - After completing each subtask, report back to the UI with a brief status update
4. **Use todo lists** - Track progress with TodoWrite to maintain visibility across context boundaries

### Token Management
1. **Avoid large code blocks** - When generating new code, create it in chunks of ~100-150 lines
2. **Use grep before reading** - Search for relevant code sections before loading entire files
3. **Summarize context** - If working across multiple sessions, summarize what was done previously
4. **Don't regenerate unchanged code** - When making edits, only include the specific lines being changed

### Progress Reporting
1. **After each file change** - Report: "Updated [filename] - [brief description]"
2. **After completing a task** - Report: "Completed [task] - [summary]"
3. **Before large operations** - Report: "Starting [operation] - will update when complete"
4. **On errors** - Report: "Error in [file]: [error message] - attempting fix"

### Example Workflow
```
1. User: "Add health bar to boss"
2. Claude: "Starting boss health bar implementation..."
3. Claude: [Reads relevant section of BossEnemy3D.cs]
4. Claude: "Added health bar fields to BossEnemy3D.cs"
5. Claude: "Added CreateBossHealthBar() method"
6. Claude: "Added UpdateBossHealthBar() method"
7. Claude: "Wired health bar updates to _PhysicsProcess and TakeDamage"
8. Claude: "Build succeeded. Boss health bar complete."
```

---

## Project Overview

**Safe Room 3D** is a first-person dungeon crawler built with Godot 4.3 (.NET) and C# (.NET 8.0). This is a 3D port of the original Safe Room 2D top-down ARPG, featuring Skyrim-inspired immersive exploration with the gritty Diablo 1 aesthetic. Set in the "Dungeon Crawler Carl" universe, it features procedurally generated dungeons, combat systems, and boss fights.

### Key Features
- First-person perspective with mouse look and WASD movement
- Procedurally generated dungeons with rooms and corridors
- Large dungeon mode (400x400 tiles, 50 rooms)
- Low-poly procedural cosmetics (40+ prop types)
- Atmospheric lighting with flickering torches
- Water effects (streams, ceiling drips with puddles)
- Dark dungeon ambiance with fog
- Enemy AI with state machines (Idle, Patrol, Aggro, Attack, Dead)
- Monster level scaling based on distance from spawn
- Boss enemies with special abilities
- Projectile combat system
- Player leveling system with XP rewards
- 3D positional audio with music and sound effects
- Minimap showing dungeon layout and enemy positions with fog of war
- Overview map with zoom (M key)
- 14 player abilities with tactical time-pause targeting
- 5x5 inventory grid with consumables and loot items
- Corpse looting system
- Combat log showing attacks, spells, damage, and XP gained

## Development Workflow (SDLC)

Follow this sequence for all code changes:

1. **Write Code** - Implement the feature or fix
2. **Build** - Compile and verify no errors
3. **Unit Test** - Run automated tests, fix any failures (when available)
4. **Present to User** - Launch game for manual testing/approval

```powershell
# Step 2: Build game project
dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"

# Step 4: Launch game for user testing
powershell -Command "Start-Process 'C:\Godot\Godot_v4.3-stable_mono_win64.exe' -ArgumentList '--path','C:\Claude\SafeRoom3D'"

# Open Godot editor
powershell -Command "Start-Process 'C:\Godot\Godot_v4.3-stable_mono_win64.exe' -ArgumentList '--editor','--path','C:\Claude\SafeRoom3D'"
```

**IMPORTANT**: The `dotnet build` command is the most reliable way to compile C# changes. Godot's `--build-solutions` can sometimes hang or fail silently. Always verify "Build succeeded" before launching.

## Environment & Dependencies

### Required Software

| Software | Version | Path / Installation |
|----------|---------|---------------------|
| Godot Engine | 4.3-stable (.NET/Mono) | `C:\Godot\Godot_v4.3-stable_mono_win64.exe` |
| .NET SDK | 8.0 | System PATH (run `dotnet --version` to verify) |
| FFmpeg | Latest | `winget install FFmpeg` (for video-to-frames conversion) |

### Godot Executables

| Executable | Purpose |
|------------|---------|
| `Godot_v4.3-stable_mono_win64.exe` | Main editor and game runner (GUI) |
| `Godot_v4.3-stable_mono_win64_console.exe` | Console version for CI/scripted builds |

### NuGet Packages

The main project (`SafeRoom3D.csproj`) uses **Godot.NET.Sdk/4.3.0** - no additional NuGet packages.

### Config Files (Runtime)

Saved to Godot's user data folder (`%APPDATA%\Godot\app_userdata\Safe Room 3D\`):

| File | Purpose |
|------|---------|
| `keybindings.cfg` | Custom key bindings |
| `audio_config.json` | Audio settings (volume, mute toggles) |

### Project Config Files

| File | Purpose |
|------|---------|
| `project.godot` | Godot project settings, input mappings, physics layers |
| `SafeRoom3D.csproj` | C# project configuration |
| `SafeRoom3D.sln` | Visual Studio solution (auto-generated) |
| `.godot/` | Godot cache (imported assets, compiled shaders) |

## Project Structure

```
SafeRoom3D/
├── Scripts/
│   ├── Core/           # Game management, dungeon generation
│   │   ├── Constants.cs           # All game constants (3D-adapted values)
│   │   ├── GameManager3D.cs       # Main game controller singleton
│   │   ├── DungeonGenerator3D.cs  # Procedural dungeon generation + enemy spawning
│   │   ├── SoundManager3D.cs      # Audio system with 3D positional sound
│   │   ├── AudioConfig.cs         # Audio settings (volume, mute toggles)
│   │   ├── KeybindingsManager.cs  # Save/load custom keybindings
│   │   ├── PerformanceMonitor.cs  # FPS overlay (F3 to toggle)
│   │   ├── LODManager.cs          # Level of detail management
│   │   ├── StatusEffect.cs        # Status effect definitions
│   │   └── StatusEffectVisuals.cs # Status effect particle systems
│   ├── Player/         # FPS player controller
│   │   └── FPSController.cs       # First-person movement, combat, camera
│   ├── Enemies/        # Enemy AI
│   │   ├── BasicEnemy3D.cs        # Basic enemy with AI state machine + Enraged buff
│   │   ├── BossEnemy3D.cs         # Boss enemy with special attacks + health bar
│   │   ├── GoblinShaman.cs        # Magical goblin that casts spells and buffs allies
│   │   ├── GoblinThrower.cs       # Ranged goblin that throws spears/axes/beer cans
│   │   ├── MonsterMeshFactory.cs  # Shared mesh creation for all monster types
│   │   └── Corpse3D.cs            # Dead enemy corpse with loot
│   ├── Abilities/      # Spell and ability system
│   │   ├── AbilityManager3D.cs    # Manages all abilities and hotbar
│   │   ├── Ability3D.cs           # Base ability class
│   │   └── Effects/               # Individual ability implementations (14 abilities)
│   │       ├── Fireball3D.cs      # Targeted AOE fireball
│   │       ├── ChainLightning3D.cs # Chain lightning
│   │       ├── SoulLeech3D.cs     # Life steal
│   │       ├── ProtectiveShell3D.cs # Invulnerability bubble
│   │       ├── GravityWell3D.cs   # Pull enemies together
│   │       ├── TimestopBubble3D.cs # Freeze time in area
│   │       ├── InfernalGround3D.cs # Ground fire DOT
│   │       ├── BansheesWail3D.cs  # Fear enemies
│   │       ├── Berserk3D.cs       # Speed/damage buff
│   │       ├── EngineOfTomorrow3D.cs # Slow all enemies
│   │       ├── DeadMansRally3D.cs # Low HP damage bonus
│   │       ├── MirrorImage3D.cs   # Decoy illusions
│   │       ├── AudienceFavorite3D.cs # Kill streak cooldown reset
│   │       ├── SponsorBlessing3D.cs # Random buff
│   │       └── TargetingIndicator3D.cs  # Ground targeting circle
│   ├── Items/          # Inventory and loot
│   │   ├── Inventory3D.cs         # Player inventory (5x5 grid)
│   │   ├── ItemDatabase.cs        # Item definitions and icons
│   │   ├── LootBag.cs             # Container for corpse/chest loot
│   │   └── ConsumableItems3D.cs   # Potion effects
│   ├── UI/             # HUD and menus
│   │   ├── HUD3D.cs               # Health/mana bars, crosshair, hotbar, minimap
│   │   ├── SpellBook3D.cs         # Ability selection UI
│   │   ├── InventoryUI3D.cs       # Inventory management UI
│   │   ├── LootUI3D.cs            # Corpse looting UI
│   │   ├── EscapeMenu3D.cs        # Pause menu with options and keybindings
│   │   ├── DebugMenu3D.cs         # Debug tools
│   │   ├── EditorScreen3D.cs      # Development editor
│   │   └── AbilityIcons.cs        # Procedural ability icon drawing
│   ├── Combat/         # Projectiles, damage
│   │   ├── Projectile3D.cs        # 3D projectile with trail effects
│   │   ├── ThrownProjectile3D.cs  # Thrown projectiles (spears, axes, beer cans)
│   │   └── MeleeSlashEffect3D.cs  # Visual melee swing effect
│   ├── Environment/    # Procedural props and effects
│   │   ├── Cosmetic3D.cs          # 3D procedural cosmetic generator (40+ types)
│   │   ├── WaterEffects3D.cs      # Water streams and ceiling drips
│   │   └── ProceduralMesh3D.cs    # Low-poly mesh utilities
│   └── Pet/            # Steve companion (to be ported)
├── Scenes/
│   └── Main3D.tscn               # Main game scene
├── Assets/
│   ├── Shaders/        # GLSL shaders
│   ├── Materials/      # PBR materials
│   └── Audio/
│       ├── Sounds/     # Sound effects (NinjaAdventure)
│       └── Events/     # Event sounds (achievement, boss kill, etc.)
└── Tests/              # Unit tests (to be ported)
```

## Key Files

### Core Systems
| File | Purpose |
|------|---------|
| `Constants.cs` | All tunable 3D parameters (speeds, scales, lighting) |
| `GameManager3D.cs` | Singleton: game state, dungeon init, UI creation |
| `DungeonGenerator3D.cs` | Room/corridor generation, prop placement, lighting, enemy spawning |
| `SoundManager3D.cs` | Singleton: 2D/3D SFX, music, positional audio |
| `AudioConfig.cs` | Audio settings persistence (volume, mute toggles) |
| `KeybindingsManager.cs` | Key binding save/load to user://keybindings.cfg |
| `PerformanceMonitor.cs` | FPS/stats overlay (F3 to toggle) |
| `LODManager.cs` | Level of detail and distance culling |

### Player
| File | Purpose |
|------|---------|
| `FPSController.cs` | CharacterBody3D with mouse look, WASD, jump, combat, fall safety, leveling system |

### Enemies
| File | Purpose |
|------|---------|
| `BasicEnemy3D.cs` | AI state machine: Idle, Patrol, Tracking, Aggro, Attack, Dead, Sleep. Supports Enraged buff. |
| `BossEnemy3D.cs` | Boss with enrage, special attacks, health bar, and full animation support for Editor bosses |
| `GoblinShaman.cs` | Magical goblin that casts projectiles and buffs nearby goblins with Enrage |
| `GoblinThrower.cs` | Ranged goblin that throws spears, axes, or beer cans. Flees when player too close |
| `MonsterMeshFactory.cs` | Centralized mesh creation for all monster types (goblin, shaman, thrower, etc.) |
| `WeaponFactory.cs` | Procedural weapon mesh generation (12 weapon types) with attachment system |
| `Corpse3D.cs` | Dead enemy corpse with loot bag |

### Combat
| File | Purpose |
|------|---------|
| `Projectile3D.cs` | 3D projectile with glow, trail particles, hit detection |
| `ThrownProjectile3D.cs` | Physical thrown projectiles (spear, throwing axe, beer can) with rotation |
| `MeleeSlashEffect3D.cs` | Visual arc effect for melee swings |

### Abilities
| File | Purpose |
|------|---------|
| `AbilityManager3D.cs` | Central ability hub: registration, hotbar, cooldowns, targeting |
| `Ability3D.cs` | Base class for all abilities |
| `Effects/*.cs` | 14 individual ability implementations |

### Items
| File | Purpose |
|------|---------|
| `Inventory3D.cs` | 5x5 inventory grid with stacking |
| `ItemDatabase.cs` | Item definitions (potions, grenades, loot) |
| `LootBag.cs` | 8-slot loot container for corpses |
| `ConsumableItems3D.cs` | Potion effect implementations |

### UI
| File | Purpose |
|------|---------|
| `HUD3D.cs` | Health/mana bars, crosshair, timer, hotbar, minimap, compass |
| `SplashScreen3D.cs` | Animated splash with dungeon selection (Test Map vs Training Grounds) |
| `FloorSelector3D.cs` | In-game dungeon type selector (Enter key) |
| `SpellBook3D.cs` | Full ability browser with tooltips |
| `InventoryUI3D.cs` | Inventory grid display |
| `LootUI3D.cs` | Corpse loot window |
| `EscapeMenu3D.cs` | Pause menu, options, key bindings |
| `EditorScreen3D.cs` | Full-screen 3D monster/ability/cosmetic editor |
| `AbilityIcons.cs` | Procedural icon drawing for hotbar |

### Environment
| File | Purpose |
|------|---------|
| `Cosmetic3D.cs` | Generates 3D meshes from shape type + colors (40+ prop types) |
| `WaterEffects3D.cs` | Water streams with UV animation, ceiling drips with puddle formation |
| `ProceduralMesh3D.cs` | Low-poly cylinder, box, pot, crystal primitives |

## Controls

| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Look around |
| Space | Jump |
| Shift | Sprint |
| Left Click | Melee attack |
| Right Click | Ranged attack (uses mana) |
| E | Interact |
| T | Loot corpse |
| Tab | Spellbook |
| I | Inventory |
| M | Toggle map |
| Q/R/F/V | Special abilities (legacy, now use hotbar) |
| 1-0 | Hotbar row 1 |
| Shift+1-0 | Hotbar row 2 |
| Alt+1-0 | Hotbar row 3 |
| Escape | Menu / Cancel targeting |
| N | Toggle nameplates (health bars/names) on all enemies |
| F3 | Toggle performance monitor (FPS, stats) |
| F5 | Panic key - force close all UIs and reset input state |

### Key Rebinding
All controls can be rebound via the Escape Menu > Key Bindings panel. Changes are automatically saved to `user://keybindings.cfg`.

## Architecture Patterns

**Singletons** (access via `Instance` property):
- `GameManager3D`, `SoundManager3D`, `FPSController`, `AbilityManager3D`, `Inventory3D`, `HUD3D`

**Signals**: Use Godot signals for cross-node communication. Connect in `_Ready()` with deferred calls.

**Namespaces**: All code uses `SafeRoom3D.*` namespaces:
- `SafeRoom3D.Core` - Core systems
- `SafeRoom3D.Player` - Player controller
- `SafeRoom3D.Enemies` - Enemy AI
- `SafeRoom3D.Abilities` - Ability system
- `SafeRoom3D.Abilities.Effects` - Ability implementations
- `SafeRoom3D.Items` - Inventory and items
- `SafeRoom3D.UI` - User interface
- `SafeRoom3D.Combat` - Projectiles and damage
- `SafeRoom3D.Environment` - Props and cosmetics

## Procedural Mesh System

The `MonsterMeshFactory` class provides centralized procedural mesh generation for all monster types with proper body part connections and animation support.

### Body Geometry Pattern

All procedural meshes use the `BodyGeometry` struct to calculate attachment points:

```csharp
// BodyGeometry calculates where body parts connect
var bodyGeom = new BodyGeometry(centerY, radius, height, scaleY);
// Properties:
// - bodyGeom.Top       - World Y of mesh top surface
// - bodyGeom.Bottom    - World Y of mesh bottom surface
// - bodyGeom.ShoulderY - Y position for arm attachments (30% up from center)
// - bodyGeom.HipY      - Y position for leg attachments (30% down from center)
```

### Mesh Connection Rules

1. **Overlap Calculation**: Adjacent meshes overlap by 15-25% of the smaller dimension
   ```csharp
   float overlap = CalculateOverlap(smallerRadius); // Returns radius * 0.2
   ```

2. **Joint Spheres**: Add small sphere meshes at attachment points for seamless connections
   ```csharp
   var joint = CreateJointMesh(material, jointRadius); // 30-50% of limb radius
   joint.Position = new Vector3(0, bodyGeom.Top - overlap * 0.5f, 0);
   ```

3. **Head Positioning Example**:
   ```csharp
   float headCenterY = bodyGeom.Top + (headHeight / 2f) - neckOverlap;
   headNode.Position = new Vector3(0, headCenterY, 0);
   ```

### LimbNodes for Animation

The factory returns `LimbNodes` containing references to animatable body parts:
- `Head` - Head/face node (required for all humanoids)
- `LeftArm`, `RightArm` - Arm nodes for swinging/attacking
- `LeftLeg`, `RightLeg` - Leg nodes for walking
- `Weapon` - Attached weapon node
- `Torch` - Light-emitting torch (for Torchbearer)

### Animation System Integration

The `MonsterAnimationSystem` creates AnimationPlayer animations using captured limb positions:

1. **Position Tracks Required**: Every animation MUST include position tracks for the head to prevent position drift between animation transitions
2. **Base Position Capture**: `LimbBasePositions.FromLimbs()` captures initial limb positions before any animation
3. **Track Paths**: Uses `LimbAnimationPaths.FromLimbs()` to resolve NodePath from AnimationPlayer root to each limb

### Animation Types and Variants

Each monster has 18 animations (6 types × 3 variants):

| Type | Variants | Duration |
|------|----------|----------|
| Idle | Breathing, Alert, Aggressive | 2.0-3.0s |
| Walk | Cautious, Normal, Predatory | 0.9-1.3s |
| Run | Charge, Evade, Sprint | 0.4-0.55s |
| Attack | Swipe, Lunge, Overhead | 0.7-1.0s |
| Hit | Front Stagger, Side Twist, Knockdown | 0.4-0.6s |
| Die | Collapse, Dramatic Fall, Fade Out | 2.0-3.0s |

### Monster Personality System

Each monster type has a `MonsterPersonality` that affects animation behavior:

| Property | Range | Description |
|----------|-------|-------------|
| SquashStretchAmount | 0-1 | Elasticity (high for slime, low for skeleton) |
| MovementStyle | enum | Smooth, Twitchy, Stiff, Jiggly, Predatory, Majestic |
| IdleFrequency | 0-1 | How often secondary motions occur |
| AttackSpeed | 0.5-2.0 | Speed multiplier for attacks |
| Bounciness | 0-1 | Spring/bounce in movements |
| Stiffness | 0-1 | Rigidity of movements |

### Adding a New Monster

1. Create mesh function in `MonsterMeshFactory.cs`:
   ```csharp
   private static void CreateMyMonsterMesh(Node3D parent, LimbNodes limbs, Color? skinColor)
   {
       // 1. Calculate body geometry
       var bodyGeom = new BodyGeometry(centerY, radius, height, scaleY);

       // 2. Create body mesh
       var body = new MeshInstance3D();
       // ... set up mesh and material
       parent.AddChild(body);

       // 3. Add joint spheres at connection points
       var neckJoint = CreateJointMesh(material, neckRadius);
       neckJoint.Position = new Vector3(0, bodyGeom.Top - overlap, 0);
       parent.AddChild(neckJoint);

       // 4. Create head node for animation
       var headNode = new Node3D();
       headNode.Position = new Vector3(0, headCenterY, 0);
       parent.AddChild(headNode);
       limbs.Head = headNode;

       // 5. Add limbs with proper attachment
       // ... arms at bodyGeom.ShoulderY, legs at bodyGeom.HipY
   }
   ```

2. Register in `CreateMonsterMesh()` switch statement
3. Add personality in `MonsterAnimationSystem.GetPersonalityForMonsterType()`
4. Add to `BasicEnemy3D` monster type cases

### Procedural Face & Eye System (CRITICAL)

The `MonsterMeshFactory` includes systems for creating expressive creature faces. This section covers best practices learned from fixing visibility issues.

#### CRITICAL: SphereMesh Rendering

**ALL SphereMesh instances MUST set Height = 2 × Radius**. Without this, spheres may render as flat discs:

```csharp
// WRONG - may render as flat disc
var eyeMesh = new SphereMesh { Radius = 0.05f };

// CORRECT - always specify Height
var eyeMesh = new SphereMesh {
    Radius = 0.05f,
    Height = 0.10f,  // ALWAYS 2 × Radius
    RadialSegments = 16,
    Rings = 12
};
```

#### Eye Visibility: Protrusion is Key

For cartoon/stylized creatures, **eyes should protrude from the head surface**, not be contained inside. Eyes hidden inside the head geometry will be invisible or barely visible.

**Good eye Z position**: `eyeZ >= headRadius * 0.75f` (eyes sit ON the face surface)
**Bad eye Z position**: `eyeZ < headRadius * 0.5f` (eyes hidden inside head)

#### Eye Proportion Rules (Updated)

| Creature Type | Eye Radius | Pupil Radius | Eye Y | Eye X Spacing | Eye Z |
|---------------|------------|--------------|-------|---------------|-------|
| Humanoid (goblins, skeletons) | **19%** of head | 45% of eye | 19% up | 38% outward | 76% forward |
| Cute (slimes, cats) | **24%** of head | 50% of eye | 22% up | 32% outward | 75% forward |
| Menacing (demons, armor) | **16%** of head | 40% of eye | 18% up | 40% outward | 80% forward |
| Beast (wolves, badlamas) | **22%** of head | 45% of eye | 15% up | 42% outward | 82% forward |
| Arachnid (spiders) | **15%** of head | 50% of eye | 25% up | 45% outward | 85% forward |

**Note**: Previous values (8-14%) were too small and caused invisible eyes. Current values (15-24%) create visible, expressive eyes.

#### Recommended: Explicit Eye Positioning (Goblin Pattern)

For reliable, detailed eyes, use **explicit positioning** rather than calculated helpers. This pattern from the Goblin gives full control:

```csharp
// Create eye meshes with proper Height
float eyeRadius = 0.038f * scale;
var eyeMesh = new SphereMesh {
    Radius = eyeRadius,
    Height = eyeRadius * 2f,
    RadialSegments = 16,
    Rings = 12
};

// Create pupil meshes
float pupilRadius = 0.018f * scale;
var pupilMesh = new SphereMesh {
    Radius = pupilRadius,
    Height = pupilRadius * 2f,
    RadialSegments = 12,
    Rings = 8
};

// Materials
var eyeMat = new StandardMaterial3D { AlbedoColor = Colors.White };
var pupilMat = new StandardMaterial3D { AlbedoColor = Colors.Black };

// Position eyes to PROTRUDE from head surface
var leftEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
leftEye.Position = new Vector3(-0.075f * scale, 0.035f * scale, 0.15f * scale);
headNode.AddChild(leftEye);

// Pupils positioned slightly forward of eyes
var leftPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
leftPupil.Position = new Vector3(-0.075f * scale, 0.035f * scale, 0.18f * scale);
headNode.AddChild(leftPupil);

// Mirror for right eye
var rightEye = new MeshInstance3D { Mesh = eyeMesh, MaterialOverride = eyeMat };
rightEye.Position = new Vector3(0.075f * scale, 0.035f * scale, 0.15f * scale);
headNode.AddChild(rightEye);

var rightPupil = new MeshInstance3D { Mesh = pupilMesh, MaterialOverride = pupilMat };
rightPupil.Position = new Vector3(0.075f * scale, 0.035f * scale, 0.18f * scale);
headNode.AddChild(rightPupil);
```

#### Specialty Eye Types

**Cat Eyes (vertical slit pupils):**
```csharp
var catPupilMesh = new BoxMesh {
    Size = new Vector3(0.008f * scale, 0.04f * scale, 0.01f * scale)
};
```

**Magical Glowing Eyes:**
```csharp
var magicPupilMat = new StandardMaterial3D
{
    AlbedoColor = new Color(0.8f, 1f, 1f),
    EmissionEnabled = true,
    Emission = new Color(0.6f, 1f, 1f),
    EmissionEnergyMultiplier = 2f
};
```

**Brow Ridge (adds menace):**
```csharp
var browMesh = new BoxMesh { Size = new Vector3(0.18f * scale, 0.04f * scale, 0.08f * scale) };
var brow = new MeshInstance3D { Mesh = browMesh, MaterialOverride = darkSkinMat };
brow.Position = new Vector3(0, 0.08f * scale, 0.14f * scale);
headNode.AddChild(brow);
```

#### Neck and Head Positioning

**Segmented Necks (Dragon pattern):**
When using multiple neck segments in a chain, rotations accumulate. The head needs counter-rotation to face forward:

```csharp
int neckSegments = 3;
float segmentAngle = 12f; // Each segment rotates forward

// Build neck chain
for (int i = 0; i < neckSegments; i++)
{
    float currentAngle = 25f - i * segmentAngle; // S-curve progression
    neckNode.RotationDegrees = new Vector3(currentAngle, 0, 0);
}

// Head counter-rotation to face forward
float headCounterRotation = -60f - (neckSegments * segmentAngle);
headNode.RotationDegrees = new Vector3(headCounterRotation, 0, 0);
```

**Joint Spheres for Seamless Connections:**
Add small sphere meshes at body part connections to hide gaps:

```csharp
var jointMesh = new SphereMesh {
    Radius = limbRadius * 1.2f,
    Height = limbRadius * 2.4f
};
var joint = new MeshInstance3D { Mesh = jointMesh, MaterialOverride = mat };
joint.Position = connectionPoint;
parent.AddChild(joint);
```

#### LookAt() Alternative (CRITICAL)

**NEVER use `LookAt()` for nodes not yet in the scene tree** - it will crash. Use manual rotation:

```csharp
// WRONG - crashes if node not in scene tree
node.LookAt(targetPosition, Vector3.Up);

// CORRECT - manual rotation calculation
var direction = (targetPosition - node.Position).Normalized();
float yaw = Mathf.Atan2(direction.X, direction.Z);
float pitch = -Mathf.Asin(direction.Y);
node.Rotation = new Vector3(pitch + Mathf.Pi / 2f, yaw, 0);
```

#### Common Mistakes to AVOID

| Mistake | Problem | Correct Approach |
|---------|---------|------------------|
| `SphereMesh { Radius = 0.05f }` without Height | Renders as flat disc | Add `Height = 0.1f` (2 × Radius) |
| Eye radius < 15% of head | Eyes invisible or too small | Use 15-24% based on creature type |
| Eye Z < headRadius × 0.7 | Eyes hidden inside head | Position eyes to protrude from surface |
| Using helper functions for complex eyes | Less control, harder to debug | Use explicit Goblin-style positioning |
| Forgetting head counter-rotation on long necks | Head faces down/wrong direction | Counter-rotate by accumulated neck angle |
| `LookAt()` on nodes not in scene tree | Crashes with NullReferenceException | Use manual Atan2/Asin rotation |
| Neck segments without joint spheres | Visible gaps at connections | Add joint spheres at attachment points |

## Weapon System

The `WeaponFactory` class provides procedural weapon mesh generation with consistent attachment points for humanoid monsters.

### Weapon Types

| Type | Style | Size | Mesh Components |
|------|-------|------|-----------------|
| Dagger | One-handed | Small | Handle cylinder + guard box + blade box + tapered tip cylinder |
| Short Sword | One-handed | Medium | Handle cylinder + pommel cylinder + guard box + blade box + tip cylinder |
| Long Sword | Two-handed | Large | Handle cylinder + pommel cylinder + guard box + blade box + tip cylinder + fuller box |
| Axe | One-handed | Medium | Handle cylinder + butt cap cylinder + collar cylinder + head boxes + edge box |
| Battle Axe | Two-handed | Large | Handle cylinder + butt cap cylinder + collar cylinder + central bar box + dual head boxes |
| Spear | Two-handed | Long | Shaft cylinder + collar cylinder + cone-shaped head cylinder |
| Mace | One-handed | Medium | Handle cylinder + collar cylinder + neck cylinder + head (3 cylinders) + 4 flange boxes + spike cylinder |
| War Hammer | Two-handed | Large | Handle cylinder + collar cylinder + shaft core cylinder + hammer face boxes + spike cylinder |
| Staff | Two-handed | Long | Shaft cylinder + flared head cylinder + rotated box crystal |
| Bow | Two-handed | Medium | Grip cylinder + curved limb cylinders + bowstring cylinder |
| Club | One-handed | Medium | Tapered body cylinder + rounded cap cylinder |
| Scythe | Two-handed | Large | Shaft cylinder + collar cylinder + tang box + blade box + edge box + tip cylinder |

### WeaponFactory API

```csharp
// Get all weapon type display names
string[] names = WeaponFactory.GetWeaponTypes();

// Get weapon type from display name
WeaponType type = WeaponFactory.GetWeaponTypeFromName("Long Sword");

// Get weapon stats
WeaponData data = WeaponFactory.GetWeaponData(type);
// data.DamageBonus, data.SpeedModifier, data.Range, data.IsTwoHanded

// Create weapon mesh
Node3D weapon = WeaponFactory.CreateWeapon(type, scale: 1f);

// Attach to humanoid monster
WeaponFactory.AttachToHumanoid(weapon, limbs, scale: 1f);
```

### Weapon Attachment Coordinate System

- **Origin**: Y=0 is at the grip/handle center
- **Y-positive**: Extends toward weapon head (blade tip, mace head)
- **Y-negative**: Extends toward pommel/bottom of handle

**Standard Attachment to Right Arm:**
- Grip Position: `Vector3(0.1, -0.4, 0.08)` relative to right arm node
- Default Rotation: `X=-75°` (blade points forward-down)

### WeaponData Struct

```csharp
public struct WeaponData
{
    public string Name;
    public WeaponType Type;
    public float DamageBonus;     // Added to base damage
    public float SpeedModifier;   // 1.0 = normal, <1 = slower, >1 = faster
    public float Range;           // Attack range in meters
    public bool IsTwoHanded;
    public Vector3 GripOffset;    // Offset from hand to grip point
    public Vector3 GripRotation;  // Rotation in degrees when held
}
```

### EditorScreen3D Weapons Tab

The editor includes a "Weapons" tab for previewing all weapon types:
- Select weapon from list to preview 3D mesh
- View weapon stats (damage, speed, range, grip type)
- "Attach To" dropdown to preview weapon on different humanoid monsters:
  - Goblin, Skeleton, Goblin Warlord, Goblin Shaman, Goblin Thrower

### CRITICAL: Procedural Mesh Rendering Rules

**Problem Encountered**: `SphereMesh` and `PrismMesh` can render as flat discs/triangles instead of 3D shapes in certain contexts (especially in SubViewport or with certain camera angles).

**Solution**: Use only `CylinderMesh` and `BoxMesh` for reliable rendering.

#### Mesh Types to AVOID in Weapons

| Mesh Type | Problem | Use Instead |
|-----------|---------|-------------|
| `SphereMesh` | Renders as flat disc | `CylinderMesh` with tapered ends |
| `PrismMesh` | Renders as flat triangle | `CylinderMesh` with small TopRadius |

#### Safe Mesh Patterns

**For spherical shapes (mace heads, pommels):**
```csharp
// DON'T: SphereMesh renders as disc
head.Mesh = new SphereMesh { Radius = 0.05f };  // BAD

// DO: Use cylinder with tapered caps
var body = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h };
var topCap = new CylinderMesh { TopRadius = r*0.4f, BottomRadius = r, Height = h*0.25f };
var bottomCap = new CylinderMesh { TopRadius = r, BottomRadius = r*0.5f, Height = h*0.2f };
```

**For pointed tips (blade tips, spearheads):**
```csharp
// DON'T: PrismMesh renders incorrectly
tip.Mesh = new PrismMesh { Size = new Vector3(w, h, d) };  // BAD

// DO: Use tapered cylinder
tip.Mesh = new CylinderMesh
{
    TopRadius = 0.002f * scale,    // Near-point
    BottomRadius = bladeWidth / 2f, // Matches blade
    Height = 0.06f * scale,
    RadialSegments = 4              // Square-ish for blade feel
};
tip.RotationDegrees = new Vector3(90, 0, 0);  // Align with Z-axis
```

**For crystal/gem shapes:**
```csharp
// DON'T: SphereMesh renders as disc
crystal.Mesh = new SphereMesh { Radius = 0.04f };  // BAD

// DO: Use rotated BoxMesh for gem shape
crystal.Mesh = new BoxMesh { Size = new Vector3(size, size, size) };
crystal.RotationDegrees = new Vector3(45, 45, 0);  // Diamond orientation
```

#### Weapon Construction Pattern

All weapons follow this Z-axis pattern:
- **+Z**: Blade/head direction (toward enemy)
- **-Z**: Handle/pommel direction (toward wielder)
- **Origin (0,0,0)**: Grip point where hand holds

```csharp
// Cylinder alignment for Z-axis weapons
cylinder.RotationDegrees = new Vector3(90, 0, 0);  // REQUIRED for Z-axis alignment
```

#### Reliable Mesh Types

| Shape Needed | Mesh Type | Notes |
|--------------|-----------|-------|
| Cylindrical shaft | `CylinderMesh` | Same top/bottom radius |
| Tapered cone | `CylinderMesh` | Different top/bottom radius |
| Rectangular | `BoxMesh` | Reliable in all contexts |
| Blade tip | `CylinderMesh` | TopRadius near 0, 4 segments |
| Round head | Multiple `CylinderMesh` | Body + tapered caps |
| Gem/crystal | `BoxMesh` | Rotated 45° on X and Y |

## Enemy System

### BasicEnemy3D State Machine
```
Idle → Patrolling → Tracking → Aggro → Attacking → Dead
           ↓
       Sleeping (distance-based performance optimization)
```

### Monster Types (Editor-Defined)
These are the only monster types spawned in dungeons, matching the Editor's Monsters tab:

**Original Monsters:**
| Type | HP | Speed | Damage | Color | Notes |
|------|---:|------:|-------:|-------|-------|
| Goblin | 75 | 3.0 | 10 | Olive green | Humanoid with ears/nose, can have random weapon |
| Goblin Shaman | 50 | 2.0 | 15 | Blue-green | Magical goblin with staff, casts spells, buffs allies with Enrage |
| Goblin Thrower | 60 | 3.5 | 12 | Tan/brown | Ranged goblin, throws spears/axes/beer cans |
| Slime | 50 | 2.0 | 8 | Green (translucent) | Blob with eyes |
| Eye | 40 | 4.0 | 15 | Red | Floating eyeball |
| Mushroom | 60 | 1.5 | 12 | Purple | Fungus creature |
| Spider | 55 | 4.5 | 14 | Dark brown | 8-legged |
| Lizard | 80 | 3.5 | 12 | Green-brown | Reptilian |
| Skeleton | 60 | 2.8 | 15 | Bone white | Undead warrior, can have random weapon |
| Wolf | 70 | 5.0 | 18 | Gray-brown | Fast quadruped |
| Badlama | 90 | 2.5 | 20 | Brown/cream | Sturdy quadruped |
| Bat | 40 | 4.0 | 8 | Dark purple | Flying |
| Dragon | 200 | 2.5 | 35 | Red | Large winged creature |

**DCC (Dungeon Crawler Carl) Monsters:**
| Type | HP | Speed | Damage | Color | Notes |
|------|---:|------:|-------:|-------|-------|
| Crawler Killer | 120 | 4.5 | 22 | Dark red | Combat robot, humanoid with weapon |
| Shadow Stalker | 70 | 5.5 | 18 | Deep shadow | Fast ethereal creature |
| Flesh Golem | 200 | 2.0 | 30 | Flesh tone | Heavy brute, massive HP |
| Plague Bearer | 90 | 2.5 | 12 | Sickly green | Diseased creature |
| Living Armor | 150 | 2.8 | 25 | Steel gray | Animated armor with weapon |
| Camera Drone | 40 | 4.0 | 10 | Chrome | Long range, alerts others |
| Shock Drone | 50 | 3.5 | 15 | Electric blue | Electric attacks |
| Advertiser Bot | 80 | 3.0 | 8 | Gold | Annoying, long range |
| Clockwork Spider | 65 | 5.0 | 14 | Brass | Mechanical arachnid |
| Lava Elemental | 110 | 2.5 | 28 | Lava orange | Fire damage, glowing |
| Ice Wraith | 75 | 4.0 | 16 | Ice blue | Cold ethereal |
| Gelatinous Cube | 180 | 1.5 | 20 | Translucent green | Slow but massive |
| Void Spawn | 85 | 4.5 | 22 | Void purple | Otherworldly |
| Mimic | 100 | 0.5 | 35 | Wood brown | Ambush predator, looks like chest |
| Dungeon Rat | 25 | 6.0 | 6 | Gray-brown | Fast, weak swarm creature |

### Boss Types (Editor-Defined)
Bosses are selected from the Editor's Bosses tab. One spawns in each room with enemies:

**Original Bosses:**
| Boss | HP | Speed | Damage | Notes |
|------|---:|------:|-------:|-------|
| Skeleton Lord | 500 | 3.2 | 40 | Undead king with full animation |
| Dragon King | 900 | 2.8 | 60 | Massive dragon, fire breath |
| Spider Queen | 450 | 4.5 | 45 | Giant spider, spawns minions |

**DCC Bosses:**
| Boss | HP | Speed | Damage | Notes |
|------|---:|------:|-------:|-------|
| The Butcher | 800 | 3.5 | 55 | Heavy brute, "Fresh meat!" |
| Mordecai the Traitor | 600 | 4.0 | 40 | Cunning dual-wielder |
| Princess Donut | 400 | 5.0 | 30 | Cat boss, fast and agile |
| Mongo the Destroyer | 1200 | 2.5 | 70 | Massive titan |
| Zev the Loot Goblin | 350 | 6.0 | 25 | Fast, greedy goblin lord |

### Boss Special Attacks
| Boss | Attack | Effect |
|------|--------|--------|
| Skeleton Lord | Ground Slam | AOE damage around boss |
| Dragon King | Fire Breath | Projectile barrage in cone |
| Spider Queen | Summon Minions | Spawns smaller spiders |
| The Butcher | Cleaver Throw | Ranged attack |
| Mordecai | Shadow Step | Teleport behind player |
| Princess Donut | Royal Command | Buffs nearby allies |
| Mongo | Earthquake | AOE stun + damage |
| Zev | Treasure Toss | Throws gold for distraction + damage |

### Boss Animation System
All Editor-defined bosses (original + DCC) use the full `MonsterAnimationSystem`:

**Animation Integration:**
- Bosses use `MonsterMeshFactory.CreateMonsterMesh()` to get `LimbNodes`
- `MonsterAnimationSystem.CreateAnimationPlayer()` creates procedural animations
- All 6 animation types supported: Idle, Walk, Run, Attack, Hit, Die

**Animation Triggers:**
| State | Animation Type |
|-------|---------------|
| Idle state | `AnimationType.Idle` |
| Chasing player | `AnimationType.Run` |
| Performing attack | `AnimationType.Attack` |
| Taking damage | `AnimationType.Hit` |
| Dying | `AnimationType.Die` |

**Code Location:** `BossEnemy3D.cs` - `SetupAnimationPlayer()`, `PlayAnimation()`, `ChangeState()`

### Room Enemy Spawning
Each room spawns exactly **4-5 enemies** as a balanced group:
- 1x **Boss** (from Editor boss types: 3 original + 5 DCC bosses)
- 3-4x **Regular Enemies** (random mix from Editor monster types)

This ensures manageable encounters while maintaining variety. All 28 monster types (13 original + 15 DCC) appear throughout the dungeon.

### Random Weapon Attachment
Humanoid monsters spawn with random weapons from the WeaponFactory:

**Eligible Monster Types:**
- `goblin` - Scale 0.7
- `skeleton` - Scale 0.9
- `crawler_killer` - Scale 1.0 (DCC)
- `living_armor` - Scale 1.0 (DCC)

**Weapon Selection:**
- Random weapon type from all 12 available types (Dagger, Short Sword, Long Sword, Axe, Battle Axe, Spear, Mace, War Hammer, Staff, Bow, Club, Scythe)
- Attached to right arm via `WeaponFactory.AttachToHumanoid()`
- Weapons are visual only (no stat modification currently)

### Death Animation System
When enemies die, the full death animation plays before the corpse spawns:

1. Enemy enters `Dead` state immediately
2. Collision disabled (no longer blocks player/projectiles)
3. Death animation plays via `MonsterAnimationSystem` (2.5 second duration)
4. After animation completes, `Corpse3D` spawns with loot
5. Enemy node is freed

This creates a more immersive experience where enemies visibly collapse before becoming lootable.

### Sleep System (Performance)
- Enemies beyond 40 meters enter sleep mode
- Sleeping enemies wake when player within 35 meters
- Sleeping enemies skip AI processing
- Taking damage immediately wakes enemies

### Monster Level Scaling
Monsters have a Level property that scales their stats:
- **HP**: BaseHP * (1 + (Level-1) * 0.15) - 15% increase per level
- **Damage**: BaseDamage * (1 + (Level-1) * 0.10) - 10% increase per level
- **XP Reward**: BaseHealth * (1 + Level * 0.5) / 10

Level is assigned based on distance from spawn room (1-10 range).

## Player Leveling System

The player gains experience from killing monsters and levels up with permanent stat bonuses.

### Leveling Stats
| Property | Per Level |
|----------|-----------|
| Max Health | +10% of base |
| Max Mana | +5% of base |
| Full heal on level up | Yes |

### XP Formula
- XP to next level: `1000 * CurrentLevel`
- XP from monsters: Based on monster level and base health

### Code Location
- `FPSController.cs`: `AddExperience()`, `LevelUp()`, `PlayerLevel`, `CurrentExperience`, `ExperienceToNextLevel`
- `BasicEnemy3D.cs`: `Level`, `ExperienceReward`, awards XP in `Die()`

## Abilities

### Ability System Overview
The `AbilityManager3D` manages 14 player abilities with:
- 3x10 hotbar (Row 1: 1-0, Row 2: Shift+1-0, Row 3: Alt+1-0)
- Tactical time-pause for targeted abilities (game freezes during aiming)
- Cooldown management with UI feedback
- Mana cost system

### Ability List

| ID | Name | Type | Cooldown | Mana | Description |
|----|------|------|----------|------|-------------|
| `fireball` | Fireball | Targeted | 8s | 20 | AOE fire damage at target location |
| `chain_lightning` | Chain Lightning | Instant | 10s | 25 | Bounces between 5 enemies |
| `soul_leech` | Soul Leech | Instant | 12s | 15 | Damage enemies, heal player |
| `protective_shell` | Protective Shell | Self | 120s | 30 | 15s invulnerability bubble |
| `gravity_well` | Gravity Well | Targeted | 15s | 35 | Pull enemies to point |
| `timestop_bubble` | Timestop Bubble | Targeted | 20s | 40 | Freeze enemies in area |
| `infernal_ground` | Infernal Ground | Targeted | 12s | 30 | Fire DOT zone |
| `banshees_wail` | Banshee's Wail | Self | 25s | 35 | Fear nearby enemies |
| `berserk` | Berserk | Self | 120s | 0 | 2x speed/damage for 15s |
| `mirror_image` | Mirror Image | Self | 30s | 25 | Create decoy illusions |
| `dead_mans_rally` | Dead Man's Rally | Toggle | 60s | 0 | Damage bonus when low HP |
| `engine_of_tomorrow` | Engine of Tomorrow | Toggle | 45s | 0 | Slow all enemies 50% |
| `audience_favorite` | Audience Favorite | Passive | 30s | 0 | Kill streak resets random cooldown |
| `sponsor_blessing` | Sponsor's Blessing | Self | 90s | 0 | Random beneficial buff |

### Default Hotbar Layout
**Row 1 (1-0 keys):**
1. Fireball
2. Chain Lightning
3. Soul Leech
4. Protective Shell
5. Gravity Well
6. Timestop Bubble
7. Infernal Ground
8. Banshee's Wail
9. Berserk
0. Mirror Image

**Row 2 (Shift+1-0):**
1. Dead Man's Rally
2. Engine of Tomorrow
3. Audience Favorite
4. Sponsor's Blessing

### Targeting System
Targeted abilities (Fireball, Gravity Well, Timestop Bubble, Infernal Ground) use tactical targeting:
1. Press hotbar key to enter targeting mode
2. **Time freezes** (Engine.TimeScale = 0)
3. Mouse cursor appears for aiming
4. Targeting indicator shows on ground
5. Left-click to confirm, Right-click/Escape to cancel
6. Time resumes after cast or cancel

## Items & Inventory

### Inventory System
- 5x5 grid (25 slots)
- Items stack up to 99
- Consumables usable from inventory UI

### Item Types

**Consumables:**
| Item | Effect |
|------|--------|
| Health Potion | Restore 50 HP |
| Mana Potion | Restore 25 MP |
| Viewer's Choice Grenade | Random effect (heal, damage, buff, debuff) |
| Liquid Courage | 10s invulnerability |
| Recall Beacon | Teleport to dungeon center |
| Monster Musk | Attract nearby enemies |

**Loot Items:**
| Item | Description |
|------|-------------|
| Gold Coins | Currency (stacks) |
| Goblin Tooth | Trophy, sell for gold |
| Mystery Meat | Unknown effect when consumed |
| Rusty Key | May unlock chests |
| Ancient Scroll | Magical artifact |
| Gemstone | Valuable treasure |
| Bone Fragment | Crafting material |
| Tattered Cloth | Crafting material |
| Venom Sac | Poison crafting |

### Loot System
- Enemies drop `Corpse3D` on death
- Corpses contain `LootBag` with 8 slots
- **Auto-loot**: Walking near corpses (2.5m) automatically picks up items
- Corpses shrink/fade when looted for visual feedback
- Loot pickup sound plays on auto-loot
- Press T near corpse to manually open loot UI
- "Take All" button for quick looting

## Combat System

### Melee Attack
- Raycast-based hit detection
- Range: `Constants.AttackRange` (2.5m)
- Damage: `Constants.AttackDamage` (25)
- Screen shake on hit

### Ranged Attack
- Spawns `Projectile3D` from camera
- Mana cost: 5
- Damage: `Constants.RangedDamage` (15)
- Homing toward enemies

### Projectile3D Features
- Glowing sphere mesh with emission
- Trail particles
- Point light following projectile
- Impact effects on hit
- Auto-destroy after lifetime

### ThrownProjectile3D (Enemy)
- Used by GoblinThrower
- Types: Spear, Throwing Axe, Beer Can
- Rotation during flight
- Physical arc trajectory

## Status Effects System

10 status effects with visual feedback (particle systems, lights, overlays).

### Status Effect Types
| Effect | Description | Damage | Speed Mod | Special |
|--------|-------------|--------|-----------|---------|
| Burning | Fire damage over time | 3/0.5s | 1.0x | Orange particles, flickering light |
| Electrified | Shock damage, sparks | 5/0.8s | 0.8x | Blue sparks, electric arcs |
| Frozen | Slowed, ice crystals | 0 | 0.3x | Ice shards, frost particles |
| Poisoned | Poison damage | 2/1s | 1.0x | Green bubbles rising |
| Bleeding | Blood loss | 2/0.7s | 1.0x | Red blood drips |
| Petrified | Turned to stone | 0 | 0x | Gray overlay, cannot move/attack |
| Cursed | Dark debuff | 1/2s | 1.0x | Purple mist, 0.7x damage/defense |
| Blessed | Holy buff | -2/2s (heal) | 1.0x | Golden halo, 1.2x damage/defense |
| Confused | Random movement | 0 | 0.6x | Orbiting stars, spiral effect |
| Enraged | Berserk fury | 0 | 1.2x | Red aura, 1.5x damage, 0.5x defense |

### Files
| File | Purpose |
|------|---------|
| `StatusEffect.cs` | Status types, configs, and manager |
| `StatusEffectVisuals.cs` | Particle systems and visual effects factory |

## Dungeon Generation

The `DungeonGenerator3D` creates:
1. **Rooms** - Random sizes (5-12 tiles), random positions
2. **Corridors** - L-shaped paths connecting rooms
3. **Walls** - Box meshes on edges of floor tiles
4. **Props** - Cosmetic3D instances based on room type
5. **Lighting** - OmniLight3D torches along walls
6. **Enemies** - BasicEnemy3D and BossEnemy3D in non-spawn rooms
7. **Water Effects** - Streams and ceiling drips in designated areas

### Large Dungeon Mode
Enable via `UseLargeDungeon = true` export property:

| Parameter | Default | Description |
|-----------|---------|-------------|
| LargeDungeonWidth | 400 | Map width in tiles |
| LargeDungeonDepth | 400 | Map depth in tiles |
| LargeDungeonRooms | 50 | Number of rooms to generate |

**Features:**
- Chunked geometry building (50x50 tile chunks) for performance
- All 10 monster types spawned throughout
- Level scaling based on distance from spawn (levels 1-10)
- Goblin clusters in barracks/throne rooms
- Boss encounters in large rooms

### Room Types
| Type | Prop Distribution |
|------|-------------------|
| storage | Barrels, crates, pots |
| library | Bookshelves, tables, pots |
| armory | Chests, barrels, crates |
| cathedral | Pillars, statues, torches, cauldrons |
| crypt | Pots, chests, crystals, statues |
| barracks | Goblin clusters with warlord boss |
| throne | Goblin clusters with warlord boss |

## Lighting System

- **Torches**: OmniLight3D with flicker animation
- **Ambient**: Very dim directional light (0.1 energy)
- **Fog**: Volumetric fog for atmosphere
- **SSAO**: Screen-space ambient occlusion

### Torch Parameters (Constants.cs)
```csharp
TorchRange = 8f      // 8 meter light radius
TorchEnergy = 2f     // Light brightness
TorchFlickerSpeed = 8f
TorchFlickerAmount = 0.15f
```

## Water Effects System

The `WaterEffects3D` static factory creates atmospheric water features.

### WaterStream
Flowing water between two points with visual and audio feedback:
- Procedural mesh with UV scrolling animation (0.5 units/sec)
- Slight curve/sag in the middle for realism
- Edge drip particles along stream
- 3D positional audio (15m max distance)
- Transparent blue material with refraction

**Usage:**
```csharp
var stream = WaterEffects3D.CreateStream(startPos, endPos, width: 1f);
AddChild(stream);
```

### WaterDrip
Ceiling drips with puddle formation:
- Randomized drip interval (0.5-2 seconds)
- Droplet particles falling with gravity
- Dynamic puddle that grows with drips and shrinks over time
- 3D positional drip sound (10m max distance)

**Usage:**
```csharp
var drip = WaterEffects3D.CreateDrip(ceilingPosition, intensity: 1f);
AddChild(drip);
```

## Atmosphere (WorldEnvironment)

Created in `GameManager3D.CreateEnvironment()`:
- Dark ambient light (0.05, 0.04, 0.06)
- Fog enabled (density 0.02)
- SSAO for depth
- Glow for light bloom
- Filmic tonemapping for muted colors

## Audio System

### SoundManager3D

The `SoundManager3D` singleton handles all game audio with 3D positional support.

**Audio Players:**
- 1x `AudioStreamPlayer` for music
- 8x `AudioStreamPlayer` for 2D SFX (UI, global sounds)
- 16x `AudioStreamPlayer3D` for positional audio

**Sound Categories:**

| Category | Sounds |
|----------|--------|
| Combat | attack, whoosh, hit, hit_alt, hit_heavy |
| Magic | shoot, magic, magic_alt, heal |
| Player | player_hit, player_die, footstep |
| Enemy | enemy_hit, enemy_die, enemy_aggro |
| Environment | door_open, chest_open, explosion |
| UI | victory, stair_appear, menu_select, pickup |

**3D Audio Properties:**
```csharp
MaxDistance = 50f    // Sounds audible up to 50 meters
UnitSize = 5f        // Reference distance
AttenuationModel = InverseDistance
```

**Usage:**
```csharp
// Play global sound
SoundManager3D.Instance?.PlaySound("attack");

// Play at 3D position
SoundManager3D.Instance?.PlaySoundAt("explosion", position);

// Helper methods
SoundManager3D.Instance?.PlayAttackSound();
SoundManager3D.Instance?.PlayHitSound(enemyPosition);
SoundManager3D.Instance?.PlayEnemyDeathSound(position);
```

### Event Sounds

Event sounds are queued to play sequentially (with 1 second pause between) so they don't overlap.

| Event | Sound File | Trigger |
|-------|------------|---------|
| Welcome | `ai_welcome_crawler.mp3` | 3 seconds after dungeon loads |
| Dungeon Start | `cascadia_now_get_out_there_and_kill_kill_kill.mp3` | 5 seconds after welcome sound |
| Achievement | `ai_new_achievement.mp3` | Every 10 kills of same monster type |
| Boss Kill | `ai_holy_crap_dude.mp3` | Killing a boss enemy |
| Player Death | `ai_you_are_so_dead.mp3` | Player health reaches 0 |
| Multi-Kill | `ai_you_monster.mp3` | 5+ kills within 2 seconds |

**Event Sound Location:** `res://Assets/Audio/Sounds/Events/`

**Usage:**
```csharp
SoundManager3D.Instance?.PlayWelcomeSound();
SoundManager3D.Instance?.PlayDungeonStartSound();
SoundManager3D.Instance?.PlayAchievementSound();
SoundManager3D.Instance?.PlayBossKillSound();
SoundManager3D.Instance?.PlayPlayerDeathEventSound();
SoundManager3D.Instance?.PlayMultiKillSound();
```

### AudioConfig

Session-only audio settings (not persisted between game sessions):
- `IsMusicEnabled` / `ToggleMusic()`
- `IsSoundEnabled` / `ToggleSound()`
- `MusicVolume` (0-1, default 0.7)
- `SoundVolume` (0-1, default 1.0)

## UI Systems

### Splash Screen
The game starts with an animated splash screen (`SplashScreen3D.cs`):

**Buttons:**
| Button | Action |
|--------|--------|
| Enter Dungeon | Starts Training Grounds (smaller 80x80 map) |
| Test Map | Starts Test Map (larger 160x160 dungeon with all monsters) |
| Editor | Opens the 3D monster/ability/cosmetic editor |
| Music ON/OFF | Toggles background music |
| Sound ON/OFF | Toggles sound effects |

The selected dungeon type is stored in `SplashScreen3D.UseTestMap` and used by `GameManager3D` when generating the dungeon.

### HUD3D Components
- **Health/Mana bars** - Bottom-left, horizontal bars
- **Crosshair** - Center screen
- **Timer** - Top center, floor time remaining
- **Hotbar** - Bottom center, 10 slots per row
- **Minimap** - Top right, shows dungeon layout
- **Compass** - Top center, cardinal directions
- **Loot Notifications** - Top-left, shows items picked up (auto-loot)
- **Achievement Notifications** - Top-left (below loot), shows kill milestones
- **Combat Log** - Bottom-left, shows combat events and XP gains

### Notification Systems

**Loot Notifications:**
- Appear in top-left corner when auto-looting corpses
- Stack if same item looted multiple times
- Fade out after 3 seconds
- Maximum 5 visible at once

**Achievement Notifications:**
- Appear below loot notifications
- Trigger every 10 kills of the same monster type
- Gold/yellow styled panel
- Play achievement sound
- Fade out after 4 seconds

**Kill Tracking:**
- Tracks kills per monster type (goblin, slime, etc.)
- Multi-kill detection (5+ kills within 2 seconds)
- Boss kills tracked separately

### Minimap

The HUD includes a top-right minimap that shows:
- Dungeon layout (corridors and rooms)
- Enemy positions (red dots)
- Player position and facing (green triangle)

**Settings (Constants):**
```csharp
MinimapSize = 180       // Pixel size
MinimapScale = 0.15f    // World-to-pixel ratio
MinimapUpdateThreshold = 2f  // Movement threshold before redraw
```

**Tile Colors:**
| Type | Color |
|------|-------|
| Wall/Void | Dark gray (0.15) |
| Corridor | Medium gray (0.35) |
| Room | Lighter gray (0.4) |
| Enemy | Red |
| Player | Green |

### SpellBook3D
- Full-screen ability browser
- Shows all 14 abilities with icons
- Displays cooldowns, mana costs, descriptions
- Assign abilities to hotbar slots

### InventoryUI3D
- 5x5 grid display
- Drag and drop support
- Right-click to use consumables
- Item tooltips on hover

### LootUI3D
- 8-slot loot bag display
- Click to take individual items
- "Take All" button
- Auto-closes when empty or player moves away

### EscapeMenu3D
- Pause/Resume game
- Audio options (music, SFX toggles)
- Key bindings panel
- Return to menu / Quit

## Monster & Asset Editor (EditorScreen3D)

A full-screen development editor for previewing and configuring game assets.

### Features
- **3D Preview Viewport**: Isolated SubViewport with orbiting camera
- **Mouse Controls**: Drag to rotate, scroll to zoom, right-click to reset
- **Auto-Rotate Toggle**: Preview models in motion
- **Animation Cycling**: Toggle to auto-cycle through animations or select specific ones
- **Status Effect Preview**: Apply any of 10 status effects to preview models
- **Tab-based Navigation**: Monsters, Abilities, Props

### Monster Editor
- Preview all 11 monster types with full 3D procedural models
- Editable stats: Health, Damage, Speed, Aggro Range
- Skin color picker
- 5 animation types: Idle, Walk, Attack, Hit, Die
- Real-time animation with limb movement

### Ability Editor
- Preview ability effect spheres with element-appropriate colors
- Editable stats: Cooldown, Damage, Radius, Duration, Mana Cost

### Cosmetics Editor
- Preview all 40+ prop types (including 25 new dungeon props)
- Scale adjustment
- Light toggle and color picker for light-emitting props

### Technical Notes
- Uses `SubViewport.OwnWorld3D = true` to isolate from main game
- Camera uses spherical coordinates for smooth orbiting
- DirectionalLight3D with RotationDegrees (not LookAt) for stable lighting
- WorldEnvironment provides ambient and background

## Map Editor System

The Map Editor provides a complete workflow for creating, editing, and playtesting custom dungeon maps. It consists of two integrated components:

### MapEditorTab (2D Tile Editor)
Located in EditorScreen3D's "Maps" tab, provides traditional top-down map creation:

**Features:**
- **Tile Painting**: Click/drag to paint floor tiles, Ctrl+click for void
- **Room Definitions**: Define rectangular rooms with types (storage, library, armory, etc.)
- **Corridor Connections**: Connect rooms with auto-generated corridors
- **Spawn Point**: Set player spawn location (green marker)
- **Monster Placement**: Place individual monsters or groups
- **Prop Placement**: Add decorative props with rotation
- **Map Properties**: Name, author, description, dimensions

**File Format:** Maps are saved as JSON files in `%APPDATA%\Godot\app_userdata\Safe Room 3D\maps\`

### InMapEditor (First-Person Editor)
Activated via "Edit in First Person" button, provides immersive in-world editing:

**Controls:**
| Key | Action |
|-----|--------|
| Z | Open/close object selector panel |
| R | Rotate placement preview (45° increments) |
| Left Click | Place object at crosshair |
| Right Click | Cancel current placement |
| Ctrl+Click | Delete object at crosshair |
| Delete | Delete selected object |
| Escape | Exit edit mode (saves automatically) |

**Features:**
- Real-time 3D preview of objects before placement
- Surface snapping (floor, wall, ceiling)
- Rotation saved for both props and monsters
- Monsters spawn in stasis mode (frozen until gameplay)
- Hotbar for quick prop access (1-0 keys)
- Search box for filtering objects

### Map Definition Structure (MapDefinition.cs)

```csharp
public class MapDefinition
{
    public string Name { get; set; }
    public string Author { get; set; }
    public int Width { get; set; }      // Map width in tiles
    public int Depth { get; set; }      // Map depth in tiles
    public string Mode { get; set; }    // "tile" or "room"
    public string? TileData { get; set; }  // RLE+GZip+Base64 encoded tiles
    public bool UseProceduralProps { get; set; }

    public List<RoomDefinition> Rooms { get; set; }
    public List<CorridorDefinition> Corridors { get; set; }
    public List<EnemyPlacement> Enemies { get; set; }
    public List<PropPlacement> PlacedProps { get; set; }
    public List<MonsterGroupPlacement> MonsterGroups { get; set; }
    public PositionData? SpawnPosition { get; set; }
}
```

### TileData Encoding (TileDataEncoder.cs)
Tile maps use efficient compression for storage:
1. **RLE (Run-Length Encoding)**: Consecutive identical tiles compressed
2. **GZip Compression**: Further reduces size
3. **Base64 Encoding**: Safe for JSON storage

```csharp
// Encode tiles for saving
string encoded = TileDataEncoder.Encode(tiles);  // int[,] -> string

// Decode tiles for loading
int[,] tiles = TileDataEncoder.Decode(encoded, width, depth);
```

### Placement Data Classes

**PropPlacement:**
```csharp
public class PropPlacement
{
    public string Type { get; set; }     // e.g., "barrel", "torch"
    public float X { get; set; }         // World position
    public float Y { get; set; }
    public float Z { get; set; }
    public float RotationY { get; set; } // Rotation in RADIANS
    public float Scale { get; set; }
}
```

**EnemyPlacement:**
```csharp
public class EnemyPlacement
{
    public string Type { get; set; }     // e.g., "goblin", "skeleton"
    public PositionData Position { get; set; }  // Tile coordinates
    public int Level { get; set; }
    public bool IsBoss { get; set; }
    public float RotationY { get; set; } // Rotation in RADIANS
    public int RoomId { get; set; }
}
```

### Editor Navigation Flow

```
SplashScreen3D
    ├── "Editor" button → EditorScreen3D (Maps tab)
    │   ├── Load/Save maps
    │   ├── 2D tile editing
    │   ├── "Play This Map" → GameManager3D (with map)
    │   └── "Edit in First Person" → GameManager3D + InMapEditor
    │
    └── InMapEditor (during gameplay)
        ├── Escape → Back to EditorScreen3D (via SplashScreen redirect)
        └── Objects placed are saved to MapDefinition
```

### Critical Implementation Details

**State Management:**
The editor uses static flags on `SplashScreen3D` to coordinate between scenes:
- `ReturnToEditorAfterPlay`: Return to MapEditorTab after InMapEditor exits
- `EnterEditModeAfterLoad`: Enter InMapEditor after map loads
- `CustomMapPath`: Path to the map being edited
- `EditModeMap` / `EditModeMapPath`: Map data for edit mode

**Scene Transitions:**
- Always unpause tree before scene changes: `GetTree().Paused = false`
- Always set visible mouse: `Input.MouseMode = Input.MouseModeEnum.Visible`
- Use `CallDeferred` for scene changes during input handling
- Clear all static flags when returning to splash to prevent stale state

**Rotation Units:**
- Placement preview uses `Rotation.Y` (radians)
- JSON stores `RotationY` in radians
- When loading, use `prop.Rotation = new Vector3(0, rotationY, 0)` NOT `RotationDegrees`

### Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Black screen after Escape | Tree still paused | Unpause before scene change |
| Splash buttons don't work | Tree paused or mouse captured | SplashScreen._Ready() forces unpause and visible mouse |
| Props/monsters not rotated when playing | Using RotationDegrees instead of Rotation | Store and load as radians |
| Objects not loading in gameplay | SpawnPropsFromDefinition not called | Ensure called in both tile and room mode generation |

## Physics Layers

| Layer | Name | Usage |
|-------|------|-------|
| 1 | Player | Player collision |
| 2 | Enemy | Enemy collision |
| 3 | PlayerAttack | Player projectiles |
| 4 | EnemyAttack | Enemy projectiles |
| 5 | Obstacle | Props, furniture |
| 6 | Pickup | Items, meat |
| 7 | Trigger | Area triggers |
| 8 | Wall | Dungeon walls/floor |

## Safety Systems

### Fall Through World Prevention
- Player saves safe position every 0.5s when on floor
- If player falls below Y = -20, respawns at last safe position
- Floor collision boxes are 0.5m thick

### F5 Panic Key
- Force closes all open UIs
- Recaptures mouse
- Resumes time scale
- Resets input state

### Input Health Checks
- Automatic detection of stuck input states
- Mouse recapture after UI close
- Time scale reset after targeting cancel

## 2D → 3D Conversion Guide

### Scale Conversion
- 16 2D pixels = 1 3D unit (1 meter)
- `Constants.PixelToUnit = 0.0625f`
- Wall height: 3 meters
- Player eye height: 1.7 meters

### Key Differences

| 2D System | 3D Equivalent |
|-----------|---------------|
| Node2D | Node3D |
| Sprite2D | MeshInstance3D |
| CharacterBody2D | CharacterBody3D |
| TileMap | MeshInstance3D (procedural) |
| PointLight2D | OmniLight3D |
| Camera2D | Camera3D |
| GPUParticles2D | GPUParticles3D |
| CanvasModulate | WorldEnvironment |
| _Draw() | SurfaceTool mesh generation |
| Vector2 position | Vector3 (Y is up, X/Z horizontal) |

### Procedural Cosmetics Mapping

| 2D Method | 3D Equivalent |
|-----------|---------------|
| DrawCircle | ProceduralMesh3D.CreateCylinder |
| DrawRect | ProceduralMesh3D.CreateBox |
| DrawPolygon | SurfaceTool triangulation |

### Cosmetic Types Supported (40+ Props)

**Original Props:**
- `barrel` - Cylinder with wood material
- `crate` - Box with wood material
- `pot` - Tapered cylinder (vase shape)
- `pillar` - Tall cylinder
- `crystal` - Double pyramid with emissive glow
- `torch` - Small cylinder with OmniLight3D
- `chest` - Box with wood/metal
- `table` - Flat box
- `bookshelf` - Tall box
- `cauldron` - Pot with green light
- `statue` - Abstract cylinder form
- `chair` - Simple wooden chair
- `skull_pile` - Pile of skulls
- `treasure_chest` - Lootable gold chest
- `campfire` - Fire with flickering light

**Dungeon Ambiance Props (25 new):**
- `bone_pile` - Scattered bones
- `coiled_chains` - Metal chains
- `moss_patch` - Green floor moss
- `water_puddle` - Reflective water pool
- `broken_barrel` - Damaged barrel
- `altar_stone` - Dark ritual altar
- `glowing_mushrooms` - Bioluminescent fungi with light
- `blood_pool` - Dark red pool
- `spike_trap` - Floor spikes
- `discarded_sword` - Rusty weapon
- `shattered_potion` - Broken vial with liquid
- `ancient_scroll` - Rolled parchment
- `brazier_fire` - Standing brazier with fire particles
- `manacles` - Wall-mounted chains
- `cracked_tiles` - Damaged floor
- `rubble_heap` - Stone debris
- `thorny_vines` - Dangerous plant growth
- `engraved_rune` - Glowing floor rune
- `abandoned_campfire` - Cold campfire remains
- `rat_nest` - Small nest pile
- `broken_pottery` - Ceramic shards
- `scattered_coins` - Gold/silver coins
- `forgotten_shield` - Rusty shield
- `moldy_bread` - Spoiled food

## Unit Testing

### Test Project Structure
```
Tests/
└── SafeRoom3D.Tests/
    ├── SafeRoom3D.Tests.csproj    # xUnit test project
    ├── TileDataEncoderTests.cs    # TileData RLE/compression tests
    └── MapDefinitionTests.cs      # JSON serialization tests
```

### Running Tests
```powershell
# Build and run all tests
cd "C:\Claude\SafeRoom3D\Tests\SafeRoom3D.Tests"
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~MapDefinitionTests"

# Run with verbose output
dotnet test -v n
```

### Test Categories

**TileDataEncoderTests** - Tests for tile map encoding/decoding:
- `RoundTrip_EmptyMap_PreservesData` - Empty maps survive encode/decode
- `RoundTrip_CorridorPattern_PreservesData` - Complex patterns preserved
- `Compression_LargeEmptyMap_CompressesEfficiently` - RLE+GZip compression works
- `Decode_SmallerExpectedSize_CropsCorrectly` - Size mismatches handled

**MapDefinitionTests** - Tests for map JSON serialization:
- `RoundTrip_WithRooms_PreservesData` - Room definitions serialize correctly
- `RoundTrip_WithEnemies_PreservesRotation` - Enemy rotation (radians) preserved
- `RoundTrip_WithProps_PreservesRotationAndScale` - Prop placement data intact
- `RoundTrip_CompleteMap_AllDataPreserved` - Full map round-trip works

### Adding New Tests

Tests must be standalone (no Godot runtime). Use test data classes that mirror game code:

```csharp
// Create standalone versions of game classes for testing
public class PropPlacement
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "barrel";

    [JsonPropertyName("rotationY")]
    public float RotationY { get; set; }  // Radians!
}
```

### Known Test Issues
- Two TileDataEncoder tests are skipped due to decode position mismatch
- Tests don't validate actual game integration (use manual playtesting)

## Porting Status

### Completed
- [x] Project structure and build system
- [x] Constants adapted for 3D
- [x] FPS player controller with fall safety
- [x] Procedural mesh generation
- [x] Cosmetic3D prop system
- [x] Dungeon generator (rooms, corridors, walls)
- [x] Basic HUD (health, mana, crosshair, timer)
- [x] Atmosphere (fog, SSAO, glow)
- [x] BasicEnemy3D with AI state machine
- [x] BossEnemy3D with special attacks and full animation support
- [x] Combat system (projectiles, melee)
- [x] Floor collision improvements
- [x] Sound manager with 3D positional audio
- [x] Minimap with dungeon/enemy display and fog of war
- [x] Compass bar showing cardinal directions
- [x] **Ability system** with time-pause tactical targeting
- [x] **14 abilities** (Fireball, Chain Lightning, Soul Leech, etc.)
- [x] **SpellBook UI** for viewing/assigning abilities
- [x] **3x10 action bar** with Shift/Alt modifiers
- [x] **Inventory system** (5x5 grid)
- [x] **Inventory UI** with item tooltips
- [x] **Corpse/loot system** - enemies leave lootable corpses
- [x] **Loot UI** - 8-slot loot bags with take all button
- [x] **9 loot items** (gold coins, goblin tooth, mystery meat, etc.)
- [x] **Key bindings menu** in Escape Menu
- [x] **Keybinding persistence** saved to user://keybindings.cfg
- [x] **Shortcut icons** showing keybinds near action bar
- [x] **Goblin Shaman** - magical goblin with spell casting and ally buffs
- [x] **Goblin Thrower** - ranged goblin with thrown projectiles
- [x] **ThrownProjectile3D** - physical thrown weapons (spear, axe, beer can)
- [x] **Enraged buff system** - enemies get damage/speed boost with visual indicator
- [x] **Boss health bar** - health bar with name label (shows after first hit like regular enemies)
- [x] **Balanced enemy groups** - 4-5 enemies per room (1 boss + 3-4 regular)
- [x] **F5 panic key** - force close all UIs and reset input state
- [x] **Input health checks** - auto-fix stuck input states
- [x] **Status effects** - 10 status types with visual feedback
- [x] **EditorScreen3D** - Monster/ability/cosmetic preview
- [x] **25 new dungeon props** - bone piles, chains, mushrooms, altars, etc.
- [x] **Large dungeon mode** - 400x400 tiles, 50 rooms, chunked generation
- [x] **Player leveling system** - XP, level ups, stat bonuses
- [x] **Monster level scaling** - HP/damage scaling, XP rewards
- [x] **Water effects** - streams with UV animation, ceiling drips with puddles
- [x] **Editor-only monsters** - all 13 monster types from Editor's Monsters tab
- [x] **Editor bosses** - Skeleton Lord, Dragon King, Spider Queen with full animations
- [x] **Random weapon attachment** - Goblins and Skeletons spawn with random weapons
- [x] **Death animation delay** - 2.5s death animation plays before corpse spawns
- [x] **DCC monsters** - 15 new Dungeon Crawler Carl themed monsters with LOD support
- [x] **DCC bosses** - 5 new bosses (The Butcher, Mordecai, Princess Donut, Mongo, Zev)
- [x] **Monster personalities** - 9 new personality types for animation variety
- [x] **Monster sounds** - Sound sets for all 28 monster types and 8 bosses
- [x] **LOD selector** - Graphics settings menu option for High/Medium/Low detail
- [x] **GraphicsConfig** - Session-based graphics settings system

### To Be Ported

#### Phase 2 - Config Systems (Shared with 2D)
- [ ] MonsterConfig - Can reuse JSON format, adapt asset paths
- [ ] AbilityConfig - Same data model, port UI
- [ ] CosmeticsConfig - Adapt for 3D procedural meshes
- [ ] DialogConfig - Quips and dialog system

#### Phase 3 - Advanced Features
- [ ] Steve pet companion - 3D model/mesh
- [ ] Safe Room (rest area)
- [ ] Save/load system
- [ ] Victory/death screens
- [ ] Dialog/Commentator system
- [ ] Map Editor

#### Phase 4 - Polish
- [ ] Damage numbers (floating text)
- [ ] More sound effects
- [ ] Music tracks
- [ ] Tutorial/onboarding

## Performance Notes

### CRITICAL: Occlusion Culling (Enabled)
Godot's occlusion culling prevents rendering objects behind walls. This is **essential** for dungeon performance.

**How it works:**
- Walls are added to an `ArrayOccluder3D` during dungeon generation
- An `OccluderInstance3D` is created with all wall geometry
- Before rendering each frame, Godot checks what's visible
- Objects completely hidden behind walls are **not rendered at all**
- This affects: meshes, lights, particles, enemies, props - everything!

**Project Setting:** `project.godot` → `rendering/occlusion_culling/use_occlusion_culling=true`

**Implementation:** `DungeonGenerator3D.cs`
- `AddBoxOccluder()` - Adds wall geometry to occluder mesh
- `BuildOccluder()` - Creates OccluderInstance3D after geometry is built
- Called from `BuildGeometry()` after walls are created

**Debug:** In Godot editor: **Perspective menu → Display Advanced… → Occlusion Culling Buffer**

**IMPORTANT for new geometry:** When adding new wall/blocking geometry, call `AddBoxOccluder(position, size)` to include it in occlusion culling. Objects that can block line of sight should be occluders.

### GPU Billboard Mode (Health Bars)
Health bars use `BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled` on materials instead of manual `LookAt()` calls.

**Why this matters:**
- Manual billboarding: `GetCamera3D()` + `LookAt()` per enemy per frame = expensive
- GPU billboarding: Zero CPU cost, GPU handles rotation automatically

**Applied to:**
- `BasicEnemy3D` - All regular enemies
- `GoblinShaman` - Spell-casting goblins
- `GoblinThrower` - Ranged goblins
- `BossEnemy3D` - Already used GPU billboarding

**Pattern for new health bars:**
```csharp
var mat = new StandardMaterial3D();
mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
mat.NoDepthTest = false; // Don't render through walls
```

### Performance Monitor (F3)
Press **F3** in-game to toggle the performance monitor overlay showing:
- FPS and frame times (avg/min/max)
- Draw calls, objects, primitives
- Physics bodies and collision pairs
- Memory usage and node counts
- Active vs sleeping enemy counts

### Mesh Generation
- Use `Constants.LowPolySegments = 12` for cylinders
- Procedural meshes are generated once at spawn
- Materials are shared via static factory methods

### Lighting Optimization (Critical for FPS)
- **DungeonGenerator3D**: Max 4 shadow-casting room lights (`MaxShadowLights = 4`)
- **Cosmetic3D**: Max 6 shadow-casting prop lights (torches, crystals, cauldrons)
- Torchbearer enemy torches have shadows disabled (moving shadows are very expensive)
- Player torch has shadows disabled
- Corridor lights never cast shadows
- Use OmniAttenuation for faster falloff
- **Occlusion culling hides lights behind walls** - no rendering cost for hidden lights

### Post-Processing (Disabled for Performance)
- **SSAO**: Disabled - very expensive screen-space effect
- **Glow/Bloom**: Disabled - adds GPU overhead
- **Fog**: Enabled - cheap atmospheric effect
- **Tonemapping**: Filmic - minimal cost

### Enemy Optimization
- Sleep system for distant enemies (>40m from player)
- Sleeping enemies skip all AI processing (only check wake condition every 0.5s)
- Dead/frozen enemies exit `_PhysicsProcess` early
- Navigation avoidance disabled by default (very expensive)
- Avoidance only enabled when actively chasing player
- **GPU billboard mode for health bars** - no per-frame camera lookups

### Cosmetic3D Prop Optimization
- Torch flicker only updates every 100ms (not every frame)
- Flicker effect distance-culled beyond 30 meters
- Processing staggered with random offset to avoid frame spikes

### Optimization TODO
- MultiMeshInstance3D for repeated props
- LOD meshes for distant objects
- Batch floor/wall meshes into fewer draw calls

## Relation to SafeRoom (2D)

This project is a 3D port of `C:\Claude\SafeRoom`. Key systems are designed to share:
- Config file formats (JSON in user data folder)
- Game logic patterns (state machines, signals)
- Cosmetic data structures

The 2D version remains functional and is backed up separately.

### Key Differences When Porting

| 2D Concept | 3D Equivalent | Notes |
|------------|---------------|-------|
| `Vector2` position | `Vector3` (Y is up) | X/Z for horizontal, Y for height |
| `Node2D` | `Node3D` | Base class change |
| `Sprite2D` | `MeshInstance3D` | Use procedural or loaded meshes |
| `CharacterBody2D` | `CharacterBody3D` | Different physics methods |
| `_Draw()` calls | `SurfaceTool` meshes | Build meshes procedurally |
| `AtlasTexture` regions | Material textures | Different texture handling |
| `PointLight2D` | `OmniLight3D` | Similar API, different scale |
| Screen-space UI | Same (CanvasLayer) | UI remains 2D |

### Shared Config Files
Config systems can share the same JSON format:
- `%APPDATA%\Godot\app_userdata\Safe Room\*.json` - 2D configs
- `%APPDATA%\Godot\app_userdata\Safe Room 3D\*.json` - 3D configs

Consider: Load 2D configs as fallback, or provide import functionality.

### Asset Sharing
Both projects use NinjaAdventure assets:
- Sounds: Can be shared directly (WAV/OGG)
- Sprites: Used for UI, minimaps (2D elements in 3D)
- 3D meshes: Generated procedurally from 2D sprite data or custom 3D

## UI Positioning Pattern

The HUD uses fixed positioning (CustomMinimumSize + Size + Position) rather than Godot anchors, which provides more reliable control over UI element placement.

### Why Fixed Positioning Works Better

Godot 4.3 anchor-based positioning with CanvasLayer can be unreliable. Fixed positioning approach:
1. Set `CustomMinimumSize` to desired dimensions
2. Set `Size` to match `CustomMinimumSize`
3. Calculate `Position` dynamically in `_Process` based on viewport size
4. Use `ZIndex` for layering control

### HUD Element Positions

```csharp
// Health/Mana Panel - Bottom left
_healthManaPanel.CustomMinimumSize = new Vector2(280, 130);
_healthManaPanel.Position = new Vector2(20, viewportSize.Y - 150);

// Chat/Combat Log - Above health panel
_chatWindow.CustomMinimumSize = new Vector2(300, 220);
_chatWindow.Position = new Vector2(20, viewportSize.Y - 380);

// Action Bar - Bottom center
actionBarPanel.CustomMinimumSize = new Vector2(720, 200);
actionBarPanel.Position = new Vector2((viewportSize.X - 720) / 2, viewportSize.Y - 200 - 40);

// Shortcut Icons - Right of action bar
_shortcutIcons.Position = new Vector2(actionBarRight + 15, viewportSize.Y - 90);
```

### Position Updates

Call position update methods in `_Process`:
```csharp
UpdateLeftPanelsPosition();   // Health/mana panel, chat window
UpdateActionBarPosition();    // Action bar centering
UpdateShortcutIconsPosition(); // Icons relative to action bar
```

## Enemy Facing Direction

Use `Atan2` instead of `LookAt()` for correct facing. Godot 3D models face +Z by default.

### Correct Pattern
```csharp
private void FaceTarget(Vector3 targetPosition)
{
    Vector3 direction = targetPosition - GlobalPosition;
    direction.Y = 0;
    if (direction.LengthSquared() > 0.01f)
    {
        float targetAngle = Mathf.Atan2(direction.X, direction.Z);
        Rotation = new Vector3(0, targetAngle, 0);
    }
}
```

### Why LookAt() Fails
`LookAt()` makes the -Z axis face the target, but procedural meshes face +Z. This causes models to appear backwards.

## Combat Log System

The HUD includes a combat log window with helper methods for logging game events.

### Log Methods (HUD3D)
| Method | Usage |
|--------|-------|
| `LogPlayerDamage(enemy, damage, source)` | Player hits enemy |
| `LogPlayerTakeDamage(source, damage)` | Enemy/projectile hits player |
| `LogSpellCast(spellName)` | Player casts ability |
| `LogSpellDamage(spell, target, damage)` | Spell hits enemy |
| `LogEnemyDeath(enemyName, xpGained)` | Enemy killed |
| `LogLoot(itemName, quantity)` | Item looted |
| `LogStatusEffect(target, effect, isPlayer)` | Status applied |

### Where Combat Messages Originate
- Player attacks: `FPSController.PerformMeleeAttack()`, `PerformStrongAttack()`
- Spell casts: `AbilityManager3D.TryActivateAbilityInternal()`
- Player takes damage: `FPSController.TakeDamage()` - receives source string
- Enemy deaths: `BasicEnemy3D.Die()`, `GoblinThrower.Die()`, `GoblinShaman.Die()`, `BossEnemy3D.Die()`

## Known Issues / Technical Debt

1. **Debug logging** - Extensive `GD.Print` statements throughout (appropriate for development)
2. **Missing save/load** - No persistence between sessions yet
3. **No victory/death screens** - Game continues after player death

## Performance Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
| `GetViewport().GetCamera3D()` in `_Process` | Expensive viewport/camera lookup every frame | Cache camera reference, refresh every 1s |
| `LookAt()` for billboards | CPU cost per object per frame | Use `BillboardMode` on material (GPU handles it) |
| Health bar without GPU billboard | Thousands of LookAt calls with many enemies | Set `mat.BillboardMode = BillboardModeEnum.Enabled` |
| Walls without occluders | Everything behind walls still renders | Call `AddBoxOccluder()` for blocking geometry |
| `QueueRedraw()` every frame | Redraws even when nothing changed | Only redraw on frame/state change |
| `foreach` on Godot arrays | Can throw InvalidCastException | Use index-based for loop with IsInstanceValid |
| `GetTree().GetNodesInGroup()` in `_Process` | Allocates array every frame | Cache reference, refresh periodically |
| Accessing disposed singletons | Throws ObjectDisposedException | Check `GodotObject.IsInstanceValid()` first |
| Signal connect in `_Process` loop | Connects multiple times | Connect once in `_Ready()`, cache state |
| Direct `Size = ...` assignment | Can cause layout warnings | Use `CallDeferred(SetSize, ...)` |
| `FindChild()` in `_Process` | O(n) tree traversal every frame | Cache reference, refresh when null/invalid |

## Signal Lifecycle Management

**Problem**: Connecting signals without disconnecting causes memory leaks and orphaned handlers.

**Solution**: Cache signal references and disconnect in `_ExitTree()`:

```csharp
// Cache references when connecting
private SomeNode? _connectedNode;

// Disconnect in cleanup
public override void _ExitTree()
{
    if (_connectedNode != null)
        _connectedNode.SomeSignal -= OnSomeSignal;
}
```
