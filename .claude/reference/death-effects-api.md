# Death Effects API Integration Guide

This document explains how to integrate spells and abilities with the death effect system to trigger specific death animations (e.g., Fire spells cause Burn Away, Ice spells cause Freeze & Shatter).

## Current Architecture

The death effect system is in `Scripts/Enemies/BasicEnemy3D.cs`:

```csharp
// Private enum (line 171)
private enum DeathEffectType {
    None,
    Melt,
    Explode,
    InflatePop,
    SpinIntoGround,
    FreezeShatter,  // Ice death
    BurnAway,       // Fire death
    Disintegrate    // Arcane/void death
}
```

Currently, `StartDeathEffect()` randomly selects a death type when the enemy dies.

---

## Integration Options

### Option 1: Add Public Method to Set Pending Death Effect (Recommended)

**Changes needed in `BasicEnemy3D.cs`:**

```csharp
// Make enum public (move outside class or make nested public)
public enum DeathEffectType {
    None, Melt, Explode, InflatePop, SpinIntoGround,
    FreezeShatter, BurnAway, Disintegrate
}

// Add field to track pending death effect from external source
private DeathEffectType _pendingDeathEffect = DeathEffectType.None;

// Add public method to set death effect
public void SetPendingDeathEffect(DeathEffectType effectType)
{
    _pendingDeathEffect = effectType;
}

// Modify StartDeathEffect() to check for pending effect first
private void StartDeathEffect()
{
    // Use pending effect if set, otherwise random
    if (_pendingDeathEffect != DeathEffectType.None)
    {
        _deathEffectType = _pendingDeathEffect;
        _pendingDeathEffect = DeathEffectType.None;
    }
    else
    {
        // Existing random selection...
    }
    // ... rest of method
}
```

**Usage from spells:**

```csharp
// In Fireball3D.cs or any fire spell
private void OnHitEnemy(BasicEnemy3D enemy, float damage)
{
    // Set pending death effect BEFORE dealing damage
    enemy.SetPendingDeathEffect(DeathEffectType.BurnAway);
    enemy.TakeDamage(damage, GlobalPosition, "Fireball", false);
}

// In FrostNova3D.cs or any ice spell
private void OnHitEnemy(BasicEnemy3D enemy, float damage)
{
    enemy.SetPendingDeathEffect(DeathEffectType.FreezeShatter);
    enemy.TakeDamage(damage, GlobalPosition, "Frost Nova", false);
}
```

---

### Option 2: Add TakeDamage Overload with Death Effect Parameter

**Changes needed in `BasicEnemy3D.cs`:**

```csharp
// New overload
public void TakeDamage(float damage, Vector3 fromPosition, string source,
                       bool isCrit, float pushbackMultiplier, DeathEffectType deathEffect)
{
    _pendingDeathEffect = deathEffect;
    TakeDamage(damage, fromPosition, source, isCrit, pushbackMultiplier);
}
```

**Usage from spells:**

```csharp
// Fire spell
enemy.TakeDamage(damage, pos, "Fireball", false, 1f, DeathEffectType.BurnAway);

// Ice spell
enemy.TakeDamage(damage, pos, "Ice Bolt", false, 0.5f, DeathEffectType.FreezeShatter);
```

---

### Option 3: Use Damage Source String Mapping

**No public enum needed - map source names to effects:**

```csharp
// In StartDeathEffect(), add before random selection:
private void StartDeathEffect()
{
    // Check if last damage source suggests a specific death effect
    _deathEffectType = _lastDamageSource?.ToLower() switch
    {
        var s when s.Contains("fire") || s.Contains("burn") || s.Contains("flame")
            => DeathEffectType.BurnAway,
        var s when s.Contains("ice") || s.Contains("frost") || s.Contains("freeze")
            => DeathEffectType.FreezeShatter,
        var s when s.Contains("void") || s.Contains("arcane") || s.Contains("disintegrate")
            => DeathEffectType.Disintegrate,
        var s when s.Contains("lightning") || s.Contains("shock") || s.Contains("electric")
            => DeathEffectType.Explode,
        _ => SelectRandomDeathEffect()  // Fall back to random
    };
    // ... rest of method
}

// Need to track last damage source
private string? _lastDamageSource;

// In TakeDamage, store source before Die() is called:
_lastDamageSource = source;
```

**Usage from spells:**

```csharp
// Just use descriptive source names - no API changes needed!
enemy.TakeDamage(damage, pos, "Fireball", false);      // → BurnAway
enemy.TakeDamage(damage, pos, "Frost Nova", false);    // → FreezeShatter
enemy.TakeDamage(damage, pos, "Void Bolt", false);     // → Disintegrate
enemy.TakeDamage(damage, pos, "Lightning", false);     // → Explode
enemy.TakeDamage(damage, pos, "Sword", false);         // → Random
```

---

## Death Effect to Spell Type Mapping

| Death Effect | Suggested Spell Types | Visual Description |
|--------------|----------------------|---------------------|
| `BurnAway` | Fire, Flame, Burn, Inferno | Fire consumes from feet up, leaves ash |
| `FreezeShatter` | Ice, Frost, Freeze, Cold | Turn blue, crack, explode into ice shards |
| `Disintegrate` | Void, Arcane, Shadow, Chaos | Thanos-snap dissolve with glowing edges |
| `Explode` | Lightning, Thunder, Shock | Instant particle explosion |
| `Melt` | Acid, Poison, Corrosion | Sink into floor, spread out |
| `InflatePop` | Chaos, Wild Magic | Balloon up then burst |
| `SpinIntoGround` | Gravity, Earth | Corkscrew rotation while sinking |

---

## Example Spell Mappings for SafeRoom3D

Based on existing abilities in `Scripts/Abilities/Effects/`:

| Spell File | Suggested Death Effect |
|------------|----------------------|
| `Fireball3D.cs` | `BurnAway` |
| `InfernalGround3D.cs` | `BurnAway` |
| `FrostNova3D.cs` | `FreezeShatter` |
| `ChainLightning3D.cs` | `Explode` |
| `GravityWell3D.cs` | `SpinIntoGround` |
| `TimestopBubble3D.cs` | `FreezeShatter` |
| `SoulLeech3D.cs` | `Disintegrate` |
| `BansheesWail3D.cs` | `Disintegrate` |

---

## Quick Implementation Checklist

To implement **Option 1** (recommended):

1. [ ] Make `DeathEffectType` enum public in `BasicEnemy3D.cs`
2. [ ] Add `_pendingDeathEffect` field
3. [ ] Add `SetPendingDeathEffect()` public method
4. [ ] Modify `StartDeathEffect()` to check pending effect first
5. [ ] Update spell effects to call `SetPendingDeathEffect()` before damage

**Estimated changes:** ~20 lines in `BasicEnemy3D.cs`, ~2 lines per spell

---

## File Locations

- **Death Effects System:** `Scripts/Enemies/BasicEnemy3D.cs` (lines 171-3230)
- **Dissolve Shader:** `Assets/Shaders/dissolve.gdshader`
- **Ability Effects:** `Scripts/Abilities/Effects/*.cs`
- **Ability Base:** `Scripts/Abilities/Ability3D.cs`

---

## Notes

- Death effects only trigger when `CurrentHealth <= 0`
- Multiple damage sources in quick succession: last one wins
- Boss enemies (`BossEnemy3D.cs`) may need similar changes
- Death effects work for both GLB models and procedural meshes
