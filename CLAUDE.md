# Safe Room 3D - Project Guide

## Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet build "C:\Claude\SafeRoom3D\SafeRoom3D.csproj"` | Build project |
| `Start-Process 'C:\Godot\Godot_v4.3-stable_mono_win64.exe' -ArgumentList '--path','C:\Claude\SafeRoom3D'` | Launch game |
| `Start-Process 'C:\Godot\Godot_v4.3-stable_mono_win64.exe' -ArgumentList '--editor','--path','C:\Claude\SafeRoom3D'` | Open editor |

**Repository:** https://github.com/gnoobs75/Saferoom3D

---

## Project Overview

First-person dungeon crawler built with **Godot 4.3 (.NET)** and **C# (.NET 8.0)**. Set in the "Dungeon Crawler Carl" universe with procedurally generated dungeons, 28 monster types, 8 bosses, 14 abilities, and combat systems.

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
├── Core/           # GameManager3D, DungeonGenerator3D, SoundManager3D, Constants
├── Player/         # FPSController (movement, combat, camera)
├── Enemies/        # BasicEnemy3D, BossEnemy3D, MonsterMeshFactory, GoblinShaman/Thrower
├── Abilities/      # AbilityManager3D, 14 ability effects in Effects/
├── Items/          # Inventory3D, ItemDatabase, LootBag, ConsumableItems3D
├── UI/             # HUD3D, SpellBook3D, InventoryUI3D, EscapeMenu3D, EditorScreen3D
├── Combat/         # Projectile3D, ThrownProjectile3D, MeleeSlashEffect3D
├── Environment/    # Cosmetic3D (40+ prop types), WaterEffects3D
└── Pet/            # Steve companion (WIP)
```

**Singletons:** `GameManager3D`, `SoundManager3D`, `FPSController`, `AbilityManager3D`, `Inventory3D`, `HUD3D`

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
| T | Loot | F5 | Panic (reset UI) |
| N | Toggle nameplates | | |

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

---

## Known Issues

1. No save/load system yet
2. No victory/death screens
3. Debug logging throughout (appropriate for dev)
