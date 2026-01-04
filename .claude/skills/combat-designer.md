---
name: combat-designer
description: Expert combat systems designer for Godot 4.3 C#. Creates balanced damage formulas, ability systems, status effects, enemy AI tuning, and projectile mechanics. Specializes in ARPG combat with cooldowns, mana costs, and tactical depth.
---

# Combat Designer - ARPG Combat Systems

Expert at designing balanced combat systems with abilities, status effects, damage formulas, and enemy AI for action RPGs.

---

## Ability System Architecture

### Ability Base Class
```csharp
public abstract partial class Ability3D : Node
{
    public abstract string AbilityId { get; }
    public abstract string AbilityName { get; }
    public abstract string Description { get; }
    public abstract float Cooldown { get; }
    public abstract int ManaCost { get; }
    public abstract bool RequiresTarget { get; }
    public abstract float TargetRadius { get; }

    protected float _cooldownRemaining;

    public bool IsReady => _cooldownRemaining <= 0;

    public abstract void Activate(FPSController player, Vector3? targetPosition = null);

    [Signal] public delegate void CooldownUpdatedEventHandler(float remaining, float total);
}
```

### Ability Manager (Central Hub)
```csharp
public partial class AbilityManager3D : Node
{
    private readonly Dictionary<string, Ability3D> _abilities = new();
    private readonly Ability3D?[,] _hotbars = new Ability3D?[3, 10]; // 3 rows × 10 slots

    private bool _isTargeting;
    private Ability3D? _targetingAbility;

    // Targeting freezes time for tactical decisions
    public void StartTargeting(Ability3D ability)
    {
        _isTargeting = true;
        _targetingAbility = ability;
        Engine.TimeScale = 0f;  // Pause game time
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
}
```

---

## Ability Types

### Instant (No Target Required)
```csharp
public override bool RequiresTarget => false;

public override void Activate(FPSController player, Vector3? targetPosition = null)
{
    // Immediate effect on player or nearest enemies
    var enemies = GetEnemiesInRange(player.GlobalPosition, 15f);
    foreach (var enemy in enemies.Take(5))
    {
        DealDamage(enemy, Damage);
    }
}
```

### Targeted (Ground Target)
```csharp
public override bool RequiresTarget => true;
public override float TargetRadius => 4f;  // AOE radius

public override void Activate(FPSController player, Vector3? targetPosition = null)
{
    if (targetPosition == null) return;

    // Spawn effect at target location
    var effect = CreateAOEEffect(targetPosition.Value, TargetRadius);
    player.GetParent().AddChild(effect);
}
```

### Self (Buff/Defensive)
```csharp
public override bool RequiresTarget => false;

public override void Activate(FPSController player, Vector3? targetPosition = null)
{
    // Apply buff to player
    player.ApplyBuff(BuffType.Invulnerable, Duration);
    CreateVisualEffect(player);
}
```

### Toggle (Persistent Effect)
```csharp
private bool _isActive;

public override void Activate(FPSController player, Vector3? targetPosition = null)
{
    _isActive = !_isActive;

    if (_isActive)
    {
        // Apply ongoing effect
        _effectNode = CreatePersistentEffect();
        player.AddChild(_effectNode);
    }
    else
    {
        _effectNode?.QueueFree();
    }
}
```

---

## 14 Ability Definitions

| ID | Name | Type | CD | Mana | Damage/Effect |
|----|------|------|---:|-----:|---------------|
| `fireball` | Fireball | Targeted | 8s | 20 | 50 AOE fire |
| `chain_lightning` | Chain Lightning | Instant | 10s | 25 | 30 × 5 bounces |
| `soul_leech` | Soul Leech | Instant | 12s | 15 | 25 damage, heal 50% |
| `protective_shell` | Protective Shell | Self | 120s | 30 | 15s invulnerability |
| `gravity_well` | Gravity Well | Targeted | 15s | 35 | Pull enemies to point |
| `timestop_bubble` | Timestop Bubble | Targeted | 20s | 40 | 5s freeze in radius |
| `infernal_ground` | Infernal Ground | Targeted | 12s | 30 | 10 DOT/sec for 8s |
| `banshees_wail` | Banshee's Wail | Self | 25s | 35 | Fear all in 10m |
| `berserk` | Berserk | Self | 120s | 0 | 2× speed/damage 15s |
| `mirror_image` | Mirror Image | Self | 30s | 25 | 3 decoy illusions |
| `dead_mans_rally` | Dead Man's Rally | Toggle | 60s | 0 | +50% damage at <30% HP |
| `engine_of_tomorrow` | Engine of Tomorrow | Toggle | 45s | 0 | Slow enemies 50% |
| `audience_favorite` | Audience Favorite | Passive | 30s | 0 | Kill streak resets CD |
| `sponsor_blessing` | Sponsor's Blessing | Self | 90s | 0 | Random buff |

---

## Damage Formulas

### Base Damage Calculation
```csharp
float CalculateDamage(float baseDamage, int attackerLevel, int defenderLevel)
{
    // Level scaling
    float levelBonus = 1f + (attackerLevel - 1) * 0.1f;

    // Defense reduction (10% per level difference)
    float levelDiff = attackerLevel - defenderLevel;
    float defenseMultiplier = 1f - Mathf.Clamp(levelDiff * -0.1f, -0.5f, 0.5f);

    return baseDamage * levelBonus * defenseMultiplier;
}
```

### Critical Hits
```csharp
float critChance = 0.1f;  // 10% base
float critMultiplier = 2.0f;

if (_rng.Randf() < critChance)
{
    damage *= critMultiplier;
    ShowCriticalEffect();
}
```

### Damage Over Time (DOT)
```csharp
public override void _Process(double delta)
{
    _tickTimer += (float)delta;
    if (_tickTimer >= TickInterval)
    {
        _tickTimer = 0f;
        DealDamage(_target, DamagePerTick);
        _remainingDuration -= TickInterval;

        if (_remainingDuration <= 0)
            QueueFree();
    }
}
```

---

## Status Effects System

### 10 Status Effect Types

| Effect | Damage | Speed | Special | Visual |
|--------|--------|-------|---------|--------|
| Burning | 3/0.5s | 1.0× | - | Orange particles, light |
| Electrified | 5/0.8s | 0.8× | - | Blue sparks |
| Frozen | 0 | 0.3× | - | Ice crystals |
| Poisoned | 2/1s | 1.0× | - | Green bubbles |
| Bleeding | 2/0.7s | 1.0× | - | Red drips |
| Petrified | 0 | 0× | Can't act | Gray overlay |
| Cursed | 1/2s | 1.0× | 0.7× damage | Purple mist |
| Blessed | -2/2s | 1.0× | 1.2× damage | Golden halo |
| Confused | 0 | 0.6× | Random movement | Orbiting stars |
| Enraged | 0 | 1.2× | 1.5× damage, 0.5× defense | Red aura |

### Status Effect Application
```csharp
public void ApplyStatusEffect(StatusEffectType type, float duration, Node3D source)
{
    // Check immunity
    if (_immunities.Contains(type)) return;

    // Stack or refresh
    if (_activeEffects.ContainsKey(type))
    {
        _activeEffects[type].Refresh(duration);
    }
    else
    {
        var effect = StatusEffectFactory.Create(type, duration, this);
        _activeEffects[type] = effect;
        AddChild(effect);
    }
}
```

### Status Effect Processing
```csharp
public override void _Process(double delta)
{
    _tickTimer += (float)delta;

    // Apply DOT
    if (DamagePerTick > 0 && _tickTimer >= TickInterval)
    {
        _tickTimer = 0f;
        _target.TakeDamage((int)DamagePerTick, "status_effect");
    }

    // Apply speed modifier
    _target.SpeedMultiplier = SpeedModifier;

    // Check expiration
    _remainingDuration -= (float)delta;
    if (_remainingDuration <= 0)
        Remove();
}
```

---

## Enemy AI Combat States

### State Machine
```csharp
public enum State { Idle, Patrolling, Tracking, Aggro, Attacking, Dead, Sleeping }

void UpdateState()
{
    float distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);

    switch (CurrentState)
    {
        case State.Idle:
            if (distanceToPlayer < AggroRange)
                ChangeState(State.Aggro);
            else if (_patrolTimer <= 0)
                ChangeState(State.Patrolling);
            break;

        case State.Aggro:
            if (distanceToPlayer > DeaggroRange)
                ChangeState(State.Idle);
            else if (distanceToPlayer <= AttackRange)
                ChangeState(State.Attacking);
            break;

        case State.Attacking:
            if (_attackTimer <= 0)
            {
                PerformAttack();
                _attackTimer = AttackCooldown;
            }
            if (distanceToPlayer > AttackRange * 1.5f)
                ChangeState(State.Aggro);
            break;
    }
}
```

### Aggro System
```csharp
float AggroRange = 15f;     // Start chasing
float DeaggroRange = 25f;   // Stop chasing
float AttackRange = 2f;     // Melee range

// Line of sight check
bool CanSeePlayer()
{
    var spaceState = GetWorld3D().DirectSpaceState;
    var query = PhysicsRayQueryParameters3D.Create(
        GlobalPosition + Vector3.Up,
        _player.GlobalPosition + Vector3.Up,
        collisionMask: 0b100  // Wall layer
    );
    var result = spaceState.IntersectRay(query);
    return result.Count == 0;  // No walls blocking
}
```

---

## Projectile System

### Player Projectile
```csharp
public partial class Projectile3D : Area3D
{
    public float Speed = 20f;
    public int Damage = 15;
    public float Lifetime = 3f;
    public bool IsHoming = true;

    private Node3D? _target;

    public override void _PhysicsProcess(double delta)
    {
        if (IsHoming && _target != null)
        {
            // Smooth homing
            Vector3 toTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
            _velocity = _velocity.Lerp(toTarget * Speed, 0.1f);
        }

        GlobalPosition += _velocity * (float)delta;

        _lifetime -= (float)delta;
        if (_lifetime <= 0) QueueFree();
    }

    void OnBodyEntered(Node3D body)
    {
        if (body is BasicEnemy3D enemy)
        {
            enemy.TakeDamage(Damage, "projectile");
            CreateImpactEffect();
            QueueFree();
        }
    }
}
```

### Enemy Thrown Projectile
```csharp
public partial class ThrownProjectile3D : RigidBody3D
{
    public enum ProjectileType { Spear, ThrowingAxe, BeerCan }

    public float Damage = 12f;
    public float RotationSpeed = 720f;  // degrees/sec

    public override void _PhysicsProcess(double delta)
    {
        // Rotate during flight
        RotationDegrees += new Vector3(RotationSpeed * (float)delta, 0, 0);
    }

    void OnBodyEntered(Node3D body)
    {
        if (body is FPSController player)
        {
            player.TakeDamage((int)Damage, _projectileType.ToString());
        }
        QueueFree();
    }
}
```

---

## Targeting System

### Tactical Time Pause
```csharp
void StartTargeting(Ability3D ability)
{
    _isTargeting = true;
    _targetingAbility = ability;

    // Freeze game time
    Engine.TimeScale = 0f;

    // Show cursor for aiming
    Input.MouseMode = Input.MouseModeEnum.Visible;

    // Show targeting indicator
    _targetingIndicator.Visible = true;
    _targetingIndicator.SetRadius(ability.TargetRadius);

    EmitSignal(SignalName.TargetingStarted, ability.AbilityId, ability.TargetRadius);
}

void ConfirmTargeting()
{
    // Cast ray to find ground position
    Vector3 targetPos = GetMouseGroundPosition();

    // Activate ability
    _targetingAbility.Activate(_player, targetPos);

    // Resume time
    Engine.TimeScale = 1f;
    Input.MouseMode = Input.MouseModeEnum.Captured;
    _isTargeting = false;
}
```

### Ground Target Raycasting
```csharp
Vector3 GetMouseGroundPosition()
{
    var camera = GetViewport().GetCamera3D();
    var mousePos = GetViewport().GetMousePosition();

    var from = camera.ProjectRayOrigin(mousePos);
    var to = from + camera.ProjectRayNormal(mousePos) * 100f;

    var spaceState = GetWorld3D().DirectSpaceState;
    var query = PhysicsRayQueryParameters3D.Create(from, to, 0b10000000); // Floor layer
    var result = spaceState.IntersectRay(query);

    if (result.Count > 0)
        return (Vector3)result["position"];

    return Vector3.Zero;
}
```

---

## Hotbar System

### 3-Row Layout
```csharp
// Row 0: Keys 1-0 (slots 0-9)
// Row 1: Shift + 1-0
// Row 2: Alt + 1-0

void HandleHotbarInput(InputEventKey keyEvent)
{
    int slot = GetSlotFromKeycode(keyEvent.Keycode);  // 1→0, 2→1, ... 0→9

    int row = 0;
    if (keyEvent.ShiftPressed) row = 1;
    else if (keyEvent.AltPressed) row = 2;

    TryActivateSlot(row, slot);
}
```

### Default Layout
```csharp
// Row 0: Combat abilities
AssignToHotbar(0, 0, "fireball");
AssignToHotbar(0, 1, "chain_lightning");
// ... etc

// Row 1: Utility/toggles
AssignToHotbar(1, 0, "dead_mans_rally");
AssignToHotbar(1, 1, "engine_of_tomorrow");
```

---

## Balance Guidelines

### Damage Scaling
| Level | HP Multiplier | Damage Multiplier |
|-------|---------------|-------------------|
| 1 | 1.0× | 1.0× |
| 5 | 1.6× | 1.4× |
| 10 | 2.35× | 1.9× |

### Cooldown Categories
| Type | Cooldown Range | Examples |
|------|---------------|----------|
| Spam | 5-10s | Fireball, Chain Lightning |
| Tactical | 15-30s | Gravity Well, Mirror Image |
| Ultimate | 60-120s | Protective Shell, Berserk |

### Mana Economy
```csharp
int BaseMaxMana = 100;
float ManaRegen = 5f;  // per second
float ManaPerLevel = 5f;  // +5% max mana per level

// Typical fight duration: 30-60 seconds
// Should be able to cast 5-10 abilities per fight
```

---

## Combat Log Integration

```csharp
// Log damage dealt
HUD3D.Instance?.LogPlayerDamage(enemy.MonsterType, damage, "melee");

// Log damage taken
HUD3D.Instance?.LogPlayerTakeDamage(source, damage);

// Log spell cast
HUD3D.Instance?.LogSpellCast(AbilityName);

// Log enemy death
HUD3D.Instance?.LogEnemyDeath(enemy.MonsterType, xpReward);
```

---

## Common Mistakes to AVOID

| Mistake | Problem | Fix |
|---------|---------|-----|
| Abilities work during pause | Cheating | Check `Engine.TimeScale` |
| No mana check before cast | Infinite casts | Validate mana in `TryActivate()` |
| DOT stacks infinitely | Broken balance | Refresh duration, don't stack |
| Homing ignores walls | Unrealistic | Add raycast LOS check |
| No cooldown visual | Confusing | Emit `CooldownUpdated` signal |
| Status effects on dead enemies | Wasted processing | Check `CurrentState != Dead` |
