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
- Low-poly procedural cosmetics (barrels, crates, torches, etc.)
- Atmospheric lighting with flickering torches
- Dark dungeon ambiance with fog and SSAO
- Enemy AI with state machines (Idle, Patrol, Aggro, Attack, Dead)
- Boss enemies with special abilities
- Projectile combat system
- 3D positional audio with music and sound effects
- Minimap showing dungeon layout and enemy positions
- 14 player abilities with tactical time-pause targeting
- 5x5 inventory grid with consumables and loot items
- Corpse looting system

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
│   ├── Environment/    # Procedural props
│   │   ├── Cosmetic3D.cs          # 3D procedural cosmetic generator
│   │   └── ProceduralMesh3D.cs    # Low-poly mesh utilities
│   └── Pet/            # Steve companion (to be ported)
├── Scenes/
│   └── Main3D.tscn               # Main game scene
├── Assets/
│   ├── Shaders/        # GLSL shaders
│   ├── Materials/      # PBR materials
│   └── Audio/          # Sound effects
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

### Player
| File | Purpose |
|------|---------|
| `FPSController.cs` | CharacterBody3D with mouse look, WASD, jump, combat, fall safety |

### Enemies
| File | Purpose |
|------|---------|
| `BasicEnemy3D.cs` | AI state machine: Idle, Patrol, Tracking, Aggro, Attack, Dead, Sleep. Supports Enraged buff. |
| `BossEnemy3D.cs` | Boss with enrage, special attacks, always-visible health bar with name label |
| `GoblinShaman.cs` | Magical goblin that casts projectiles and buffs nearby goblins with Enrage |
| `GoblinThrower.cs` | Ranged goblin that throws spears, axes, or beer cans. Flees when player too close |
| `MonsterMeshFactory.cs` | Centralized mesh creation for all monster types (goblin, shaman, thrower, etc.) |
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
| `SpellBook3D.cs` | Full ability browser with tooltips |
| `InventoryUI3D.cs` | Inventory grid display |
| `LootUI3D.cs` | Corpse loot window |
| `EscapeMenu3D.cs` | Pause menu, options, key bindings |
| `EditorScreen3D.cs` | Full-screen 3D monster/ability/cosmetic editor |
| `AbilityIcons.cs` | Procedural icon drawing for hotbar |

### Environment
| File | Purpose |
|------|---------|
| `Cosmetic3D.cs` | Generates 3D meshes from shape type + colors |
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

## Enemy System

### BasicEnemy3D State Machine
```
Idle → Patrolling → Tracking → Aggro → Attacking → Dead
           ↓
       Sleeping (distance-based performance optimization)
```

### Monster Types
| Type | HP | Speed | Damage | Color | Notes |
|------|---:|------:|-------:|-------|-------|
| Goblin | 75 | 3.0 | 10 | Olive green | Humanoid with ears/nose |
| Goblin Shaman | 50 | 2.0 | 15 | Blue-green | Magical goblin with staff, casts spells, buffs allies with Enrage |
| Goblin Thrower | 60 | 3.5 | 12 | Tan/brown | Ranged goblin, throws spears/axes/beer cans |
| Goblin Warlord | 350 | 3.5 | 30 | Dark green | Boss goblin with armor, helmet spike, big sword |
| Slime | 50 | 2.0 | 8 | Green (translucent) | Blob with eyes |
| Eye | 40 | 4.0 | 15 | Red | Floating eyeball |
| Mushroom | 60 | 1.5 | 12 | Purple | Fungus creature |
| Spider | 55 | 4.5 | 14 | Dark brown | 8-legged |
| Lizard | 80 | 3.5 | 12 | Green-brown | Reptilian |
| Skeleton | 60 | 2.8 | 15 | Bone white | Undead warrior |
| Wolf | 70 | 5.0 | 18 | Gray-brown | Fast quadruped |
| Bat | 40 | 4.0 | 8 | Dark purple | Flying |
| Dragon | 200 | 2.5 | 35 | Red | Boss-class |
| Torchbearer | 65 | 2.8 | 8 | Olive green | Goblin with torch light |

### Boss Special Attacks
| Boss | Attack | Effect |
|------|--------|--------|
| Slime King | Ground Slam | AOE damage around boss |
| All-Seeing Eye | Projectile Barrage | Multiple projectiles |
| Sporeling Elder | Poison Cloud | Poison area effect |
| Broodmother | Summon Minions | Spawns smaller enemies |
| Lizard Chieftain | Charge | Fast dash attack |
| Goblin Warlord | Ground Slam | AOE damage, leads goblin clusters |

### Goblin Cluster Spawning
Goblin clusters spawn as mixed groups with specialized roles:
- 1x **Goblin Warlord** (boss) - Armored leader with big sword
- 1x **Goblin Shaman** - Casts spells, buffs allies with Enrage
- 2x **Goblin Thrower** - Ranged attackers with throwing weapons
- 2-3x **Goblin Melee** - One may be a Torchbearer

### Sleep System (Performance)
- Enemies beyond 40 meters enter sleep mode
- Sleeping enemies wake when player within 35 meters
- Sleeping enemies skip AI processing
- Taking damage immediately wakes enemies

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
- Press T near corpse to open loot UI
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

### Room Types
| Type | Prop Distribution |
|------|-------------------|
| storage | Barrels, crates, pots |
| library | Bookshelves, tables, pots |
| armory | Chests, barrels, crates |
| cathedral | Pillars, statues, torches, cauldrons |
| crypt | Pots, chests, crystals, statues |

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

### AudioConfig

Session-only audio settings (not persisted between game sessions):
- `IsMusicEnabled` / `ToggleMusic()`
- `IsSoundEnabled` / `ToggleSound()`
- `MusicVolume` (0-1, default 0.7)
- `SoundVolume` (0-1, default 1.0)

## UI Systems

### HUD3D Components
- **Health/Mana bars** - Left side, horizontal bars
- **Crosshair** - Center screen
- **Timer** - Top center, floor time remaining
- **Hotbar** - Bottom center, 10 slots per row
- **Minimap** - Top right, shows dungeon layout
- **Compass** - Top center, cardinal directions

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
- Preview all 13 prop types
- Scale adjustment
- Light toggle and color picker for light-emitting props

### Technical Notes
- Uses `SubViewport.OwnWorld3D = true` to isolate from main game
- Camera uses spherical coordinates for smooth orbiting
- DirectionalLight3D with RotationDegrees (not LookAt) for stable lighting
- WorldEnvironment provides ambient and background

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

### Cosmetic Types Supported
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
- [x] BossEnemy3D with special attacks
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
- [x] **Boss health bar** - always-visible health bar with name label
- [x] **Goblin cluster spawning** - mixed goblin groups with roles
- [x] **F5 panic key** - force close all UIs and reset input state
- [x] **Input health checks** - auto-fix stuck input states
- [x] **Status effects** - 10 status types with visual feedback
- [x] **EditorScreen3D** - Monster/ability/cosmetic preview

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

### Mesh Generation
- Use `Constants.LowPolySegments = 12` for cylinders
- Procedural meshes are generated once at spawn
- Materials are shared via static factory methods

### Lighting
- Limit torches to ~30 per dungeon with shadows
- Use OmniAttenuation for faster falloff
- Disable shadows for distant lights

### Enemy Optimization
- Sleep system for distant enemies (>40m)
- State machine avoids pathfinding when not needed

### Optimization TODO
- MultiMeshInstance3D for repeated props
- LOD for distant objects
- Occlusion culling
- Batch floor/wall meshes

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

## Known Issues / Technical Debt

1. **Hardcoded monster names** - Some combat log messages have hardcoded names
2. **Debug logging** - Extensive `GD.Print` statements throughout (appropriate for development)
3. **Missing save/load** - No persistence between sessions yet
4. **No victory/death screens** - Game continues after player death

## Performance Anti-Patterns to Avoid

| Anti-Pattern | Why It's Bad | Better Approach |
|--------------|--------------|-----------------|
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
