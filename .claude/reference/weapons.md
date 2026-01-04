# Weapons Reference

## 12 Weapon Types

| Type | Style | Size | Components |
|------|-------|------|------------|
| Dagger | One-handed | Small | Handle + guard + blade + tip |
| Short Sword | One-handed | Medium | Handle + pommel + guard + blade |
| Long Sword | Two-handed | Large | Handle + pommel + guard + blade + fuller |
| Axe | One-handed | Medium | Handle + butt cap + head + edge |
| Battle Axe | Two-handed | Large | Handle + dual heads |
| Spear | Two-handed | Long | Shaft + collar + cone head |
| Mace | One-handed | Medium | Handle + head + flanges + spike |
| War Hammer | Two-handed | Large | Handle + hammer faces + spike |
| Staff | Two-handed | Long | Shaft + flared head + crystal |
| Bow | Two-handed | Medium | Grip + curved limbs + bowstring |
| Club | One-handed | Medium | Tapered body + rounded cap |
| Scythe | Two-handed | Large | Shaft + collar + blade + edge |

## WeaponFactory API

```csharp
// Get all weapon types
string[] names = WeaponFactory.GetWeaponTypes();

// Create weapon mesh
Node3D weapon = WeaponFactory.CreateWeapon(type, scale: 1f);

// Attach to humanoid
WeaponFactory.AttachToHumanoid(weapon, limbs, scale: 1f);
```

## Attachment Coordinate System

- **Origin**: Y=0 at grip center
- **Y-positive**: Toward weapon head
- **Y-negative**: Toward pommel
- **Grip Position**: `Vector3(0.1, -0.4, 0.08)` relative to right arm
- **Default Rotation**: `X=-75°` (blade points forward-down)
