# Safe Room 3D - Project Guide

## Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"` | Build project |
| `.\launch_game.bat` | Launch game (auto-detects GPU) |
| `.\launch_editor.bat` | Open editor (auto-detects GPU) |

**GPU-Aware Launchers:** The `.bat` files automatically use Vulkan for NVIDIA GPUs and OpenGL for Intel/AMD integrated graphics.

**Repository:** https://github.com/gnoobs75/Saferoom3D

---

## Project Overview

First-person dungeon crawler built with **Godot 4.5 (.NET)** and **C# (.NET 8.0)**. Set in the "Dungeon Crawler Carl" universe with procedurally generated dungeons, 28 monster types, 8 bosses, 14 abilities, and combat systems.

---

## Development Workflow

1. **Write Code** → 2. **Build** (`dotnet build`) → 3. **Test** → 4. **User Approval** → 5. **Commit to Git**

### Git Commit Policy
**After successful build and user approval, always ask:**
> "Build succeeded. Should I commit these changes to Git?"

Never auto-commit without explicit user approval.

---

## Project Structure

```
Scripts/
├── Core/           # GameManager3D, DungeonGenerator3D, SoundManager3D, SplashMusic, Constants
├── Player/         # FPSController (movement, combat, camera)
├── Enemies/        # BasicEnemy3D, BossEnemy3D, MonsterMeshFactory, GoblinShaman/Thrower
├── Abilities/      # AbilityManager3D, 14 ability effects in Effects/
├── Items/          # Inventory3D, ItemDatabase, LootBag, ConsumableItems3D
├── UI/             # HUD3D, SpellBook3D, InventoryUI3D, DungeonRadio, EscapeMenu3D
├── Broadcaster/    # AIBroadcaster, BroadcasterUI, CommentaryDatabase
├── Combat/         # Projectile3D, ThrownProjectile3D, MeleeSlashEffect3D
├── Environment/    # Cosmetic3D (40+ prop types), WaterEffects3D
└── Pet/            # Steve companion (supports GLB models)
```

**Singletons:** `GameManager3D`, `SoundManager3D`, `FPSController`, `AbilityManager3D`, `Inventory3D`, `HUD3D`, `DungeonRadio`, `SplashMusic`, `AIBroadcaster`

**Namespaces:** `SafeRoom3D.Core`, `.Player`, `.Enemies`, `.Abilities`, `.Items`, `.UI`, `.Combat`, `.Environment`

---

## Key Patterns

### Enemy AI State Machine
```
Idle → Patrol → Tracking → Aggro → Attack → Dead
                    ↓
                 Sleep (>40m from player)
```

### Procedural Mesh (MonsterMeshFactory)
- Use `BodyGeometry` for attachment points
- `LimbNodes` for animation references
- Always set `SphereMesh.Height = 2 × Radius`
- Eyes must protrude (Z >= headRadius × 0.75)

### SurfaceTool (Godot 4.5 Cross-Version Compatibility)
**CRITICAL:** Godot 4.5 has stricter validation - if first vertex has a normal, ALL must have normals.

**Best Practice:** Don't use `SetNormal()` at all. Let `GenerateNormals()` handle it.
```csharp
// CORRECT - No manual normals, let GenerateNormals() calculate them
var st = new SurfaceTool();
st.Begin(Mesh.PrimitiveType.Triangles);
st.AddVertex(v1);
st.AddVertex(v2);
st.AddVertex(v3);
st.GenerateNormals();  // Calculate normals from geometry
st.GenerateTangents(); // Optional, for normal maps
var mesh = st.Commit();

// WRONG - Mixing SetNormal with no-normal vertices crashes on Godot 4.5
st.SetNormal(normal);
st.AddVertex(v1);
st.AddVertex(v2);  // No SetNormal = CRASH!
```

### Performance Critical
- **Occlusion culling**: Call `AddBoxOccluder()` for walls
- **GPU billboards**: Use `mat.BillboardMode = BillboardModeEnum.Enabled` for health bars
- **Sleep system**: Enemies >40m skip AI processing
- **Cache references**: Never use `GetCamera3D()` or `FindChild()` in `_Process`

### Signal Lifecycle
```csharp
// Connect in _Ready(), disconnect in _ExitTree()
public override void _ExitTree()
{
    if (_connectedNode != null)
        _connectedNode.SomeSignal -= OnSomeSignal;
}
```

### Enemy Facing (use Atan2, not LookAt)
```csharp
float targetAngle = Mathf.Atan2(direction.X, direction.Z);
Rotation = new Vector3(0, targetAngle, 0);
```

### First-Person Weapon System
- Weapons dynamically switch based on equipped MainHand item
- Uses `WeaponFactory.CreateWeapon()` for procedural meshes
- Each weapon type has unique positioning, rotation, and scale for FPS view
- `OnEquipmentChanged` event triggers weapon rebuild automatically

```csharp
// Map equipment type to factory type
var factoryType = MapToFactoryWeaponType(equipmentWeaponType);
var weaponMesh = WeaponFactory.CreateWeapon(factoryType, scale);
```

### Input Handling for CanvasLayer UIs
CanvasLayer-based UIs (EscapeMenu, UIEditorMode) require manual visibility checks:

```csharp
public override void _Input(InputEvent @event)
{
    // CRITICAL: CanvasLayer doesn't auto-block input when hidden
    if (!Visible) return;

    // Also check if modal UIs should block this handler
    if (EscapeMenu3D.Instance?.Visible == true) return;

    // Process input...
}
```

**Layer priorities:** EscapeMenu (200) > UIEditorMode (150) > Broadcaster (50)

---

## Controls

| Key | Action | Key | Action |
|-----|--------|-----|--------|
| WASD | Move | 1-0 | Hotbar row 1 |
| Mouse | Look | Shift+1-0 | Hotbar row 2 |
| Space | Jump | Tab | Spellbook |
| Shift | Sprint | I | Inventory |
| LMB | Melee | M | Map |
| RMB | Ranged | Esc | Menu |
| E | Interact | F3 | Performance |
| T | Loot | F5 | **Panic key** (force reset all UI/input) |
| N | Toggle nameplates | C | Character Sheet |
| Y | Dungeon Radio | | |

### UI Editor Mode

Customize HUD layout by repositioning elements:

| Key | Action |
|-----|--------|
| X | Enter/exit UI editor mode (saves on exit) |
| Shift+X | Reset all positions to defaults |

**In Editor Mode:**
- Drag any HUD element to reposition it
- Positions persist between sessions (saved to `user://ui_positions.json`)

**Draggable Elements (11):** Action Bar, Health/Mana, Target Frame, Combat Log, Minimap, Compass, Shortcuts, AI Broadcaster, View Counter, Description Panel, Dungeon Radio

### Character Sheet Tooltips

Hover over stats in the Character Sheet (C) to see formulas:

**Attributes:**
- Strength: +2% Physical Damage per point
- Dexterity: +1% Attack Speed, +0.5% Crit, +0.5% Dodge per point
- Vitality: +10 Health, +0.5 HP Regen per point
- Energy: +5 Mana, +0.2 MP Regen per point

**Combat Stats** show full formula breakdowns on hover.

### Inventory Weapon Comparison

Hover over weapons in Inventory (I) to see comparison vs equipped:
- **▲ +X** (green) = better stat
- **▼ -X** (red) = worse stat
- **=** (gray) = equal

### Dungeon Radio (Y key)

Winamp-style music player with cheesy dungeon theme:
- **Play/Pause/Stop** - Control playback
- **Prev/Next** - Navigate playlist
- **Volume slider** - Adjust music volume
- **Progress bar** - Seek within track
- **LCD display** - Scrolling track info
- **Visualizer** - Animated spectrum bars

**Features:**
- Loads all MP3 files from `Assets/Audio/Music/`
- Auto-shuffles playlist on startup
- Auto-advances to next track
- **Audio ducking** - Volume reduces when AI Broadcaster speaks
- Draggable with X key (UI Editor Mode)

---

## Audio Systems

### Music Flow

```
Splash Screen → SplashMusic (splash.mp3 loop)
                     ↓ (fade out on game start)
In-Game → DungeonRadio (MP3 playlist, starts after welcome sounds)
```

### SplashMusic (`Scripts/Core/SplashMusic.cs`)
- Singleton persists across scene changes (added to Root)
- Plays `splash.mp3` on loop during menus/editor
- Fades out (3 sec) when game starts
- Respects `AudioConfig.IsMusicEnabled`

### DungeonRadio (`Scripts/UI/DungeonRadio.cs`)
- Manages playlist of all MP3s in music folder
- Audio ducking when AI speaks (reduces to 25% volume)
- DungeonRadioUI provides Winamp-style interface

### AudioConfig
Static class managing audio state (not persisted between sessions):
- `IsMusicEnabled` / `IsSoundEnabled` - Toggle flags
- `MusicVolume` / `SoundVolume` - Volume levels (0-1)
- `ConfigChanged` event - Notify listeners of changes

---

## Anti-Patterns to Avoid

| Don't | Do Instead |
|-------|------------|
| `GetCamera3D()` in `_Process` | Cache reference, refresh every 1s |
| `LookAt()` for billboards | Use `BillboardMode` on material |
| `foreach` on Godot arrays | Index-based loop + `IsInstanceValid` |
| `FindChild()` in `_Process` | Cache reference in `_Ready` |
| Signal connect in loops | Connect once in `_Ready` |
| Walls without occluders | Call `AddBoxOccluder()` |

---

## Physics Layers

| Layer | Name | Layer | Name |
|-------|------|-------|------|
| 1 | Player | 5 | Obstacle |
| 2 | Enemy | 6 | Pickup |
| 3 | PlayerAttack | 7 | Trigger |
| 4 | EnemyAttack | 8 | Wall |

---

## Files Reference

For detailed information on monsters, abilities, weapons, cosmetics, and porting status, see:
- `.claude/reference/monsters.md` - All 28 monster types + 8 bosses
- `.claude/reference/abilities.md` - 14 abilities with stats
- `.claude/reference/weapons.md` - 12 weapon types
- `.claude/reference/cosmetics.md` - 40+ prop types
- `.claude/reference/porting-status.md` - What's done / to-do

## Skills

Custom skills for specialized capabilities (auto-loaded in Claude Code sessions):
- `.claude/skills/godot-3d-dev.md` - Godot 4.5 C# development workflow
- `.claude/skills/procedural-3d-artist.md` - Procedural 3D mesh creation for monsters & props
- `.claude/skills/dungeon-architect.md` - Procedural dungeon generation, room layouts, spawn systems
- `.claude/skills/combat-designer.md` - Ability systems, damage formulas, status effects, enemy AI
- `.claude/skills/animation-choreographer.md` - Procedural animation, LimbNodes, personality-driven motion

---

## Steve the Chihuahua (Pet System)

Steve is the player's companion pet with healing and attack abilities.

### GLB Model Support

Steve supports switching between procedural mesh and custom .glb models at runtime:

1. **Place GLB file at:** `Assets/Models/steve.glb`
2. **In-game toggle:**
   - Open Editor Screen → NPCs tab → Select "Steve the Chihuahua"
   - Click **"Model Type"** toggle to switch between "Procedural" and "GLB Model"
   - Changes apply immediately to both preview and in-game Steve

```csharp
// Programmatic access
Steve3D.UseGlbModel = true;  // Enable GLB mode
Steve3D.Instance?.ReloadModel();  // Apply changes to live instance
```

### Steve's Abilities
- **Heal** (10s cooldown): Heals player for 50 HP when they take damage
- **Magic Missile** (10s cooldown): Attacks enemies threatening the player

---

## Known Issues

1. No save/load system yet
2. No victory/death screens
3. Debug logging throughout (appropriate for dev)

## Recent Fixes (v4.5 Upgrade)

1. **SurfaceTool Compatibility** - Removed all `SetNormal()` calls, use `GenerateNormals()` instead (6 files fixed: Cosmetic3D, ProceduralMesh3D, MonsterLODSystem, LODManager, WaterEffects3D, MonsterMeshFactory, DungeonGenerator3D)
2. **RLE Encoding Bug** - Fixed `TileDataEncoder.cs` map corruption when tile runs >= 64 consecutive tiles
3. **GPU Compatibility** - Added GPU-aware launchers that use OpenGL for Intel/AMD integrated graphics
4. **Steve GLB Support** - Added runtime toggle to switch Steve between procedural mesh and .glb model
