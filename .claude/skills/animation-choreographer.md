---
name: animation-choreographer
description: Expert procedural animation designer for Godot 4.3 C#. Creates keyframe animations for monsters using AnimationPlayer, LimbNodes timing, personality-driven motion, and squash-stretch effects. Specializes in expressive creature animation without external assets.
---

# Animation Choreographer - Procedural Monster Animation

Expert at creating expressive procedural animations using Godot's AnimationPlayer system with personality-driven motion.

---

## Animation System Architecture

### AnimationType Enum
```csharp
public enum AnimationType
{
    Idle,    // Breathing, subtle movements
    Walk,    // Locomotion cycle
    Run,     // Fast locomotion
    Attack,  // Combat action
    Hit,     // Damage reaction
    Die      // Death sequence
}
```

### Animation Variants (3 per type)
| Type | Variant 0 | Variant 1 | Variant 2 |
|------|-----------|-----------|-----------|
| Idle | Breathing | Alert | Aggressive |
| Walk | Cautious | Normal | Predatory |
| Run | Charge | Evade | Sprint |
| Attack | Swipe | Lunge | Overhead |
| Hit | Front Stagger | Side Twist | Knockdown |
| Die | Collapse | Dramatic Fall | Fade Out |

**Total: 18 animations per monster (6 types × 3 variants)**

---

## Monster Personality System

### MonsterPersonality Struct
```csharp
public struct MonsterPersonality
{
    public float SquashStretchAmount;  // 0-1: elasticity (high=slime, low=skeleton)
    public MovementStyle MovementStyle;
    public float IdleFrequency;        // 0-1: how often secondary motions occur
    public float AttackSpeed;          // 0.5-2.0: attack animation speed
    public float Bounciness;           // 0-1: spring in movements
    public float Stiffness;            // 0-1: rigidity (inverse of fluidity)
}
```

### Movement Styles
```csharp
public enum MovementStyle
{
    Smooth,      // Fluid, gradual movements
    Twitchy,     // Quick, jerky movements
    Lumbering,   // Slow, heavy movements
    Graceful,    // Elegant, flowing movements
    Stiff,       // Rigid, mechanical movements
    Jiggly,      // Bouncy, elastic movements
    Predatory,   // Stalking, hunting movements
    Majestic     // Slow, powerful, commanding movements
}
```

### Personality Presets

| Monster Type | Squash | Style | Attack Speed | Stiffness |
|--------------|--------|-------|--------------|-----------|
| Goblin | 0.2 | Twitchy | 1.2× | 0.3 |
| Slime | 0.8 | Jiggly | 0.8× | 0.1 |
| Skeleton | 0.05 | Stiff | 1.0× | 0.9 |
| Dragon | 0.1 | Majestic | 0.7× | 0.7 |
| Spider | 0.15 | Twitchy | 1.5× | 0.4 |
| Wolf | 0.25 | Predatory | 1.3× | 0.4 |
| Machine | 0.0 | Stiff | 1.1× | 1.0 |
| Elemental | 0.5 | Jiggly | 0.9× | 0.3 |
| Gelatinous | 0.9 | Jiggly | 0.6× | 0.05 |
| Boss | 0.1 | Majestic | 0.8× | 0.75 |

---

## Animation Timing Constants

```csharp
const float IDLE_DURATION = 2.0f;
const float WALK_CYCLE_DURATION = 1.0f;
const float RUN_CYCLE_DURATION = 0.5f;
const float ATTACK_DURATION = 0.8f;
const float HIT_DURATION = 0.4f;
const float DIE_DURATION = 2.0f;

// Attack phases (percentages)
const float ATTACK_WINDUP_PERCENT = 0.3f;   // Wind up
const float ATTACK_ACTION_PERCENT = 0.4f;   // Strike
const float ATTACK_RECOVERY_PERCENT = 0.3f; // Recovery
```

---

## LimbNodes Integration

### LimbNodes Structure
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

### Base Positions (CRITICAL)
```csharp
// Capture BEFORE any animation
struct LimbBasePositions
{
    public Vector3 Head;
    public Vector3 LeftArm, RightArm;
    public Vector3 LeftLeg, RightLeg;

    public static LimbBasePositions FromLimbs(LimbNodes limbs)
    {
        return new LimbBasePositions
        {
            Head = limbs.Head?.Position ?? Vector3.Zero,
            LeftArm = limbs.LeftArm?.Position ?? Vector3.Zero,
            // ... etc
        };
    }
}
```

### Animation Paths
```csharp
// Calculate path from AnimationPlayer root to each limb
struct LimbAnimationPaths
{
    public NodePath Head;      // e.g., "Head" or "Neck/Head"
    public NodePath LeftArm;
    public NodePath RightArm;
    public NodePath LeftLeg;
    public NodePath RightLeg;
}

// Handles nested hierarchies (e.g., dragon neck chains)
NodePath GetRelativePath(Node3D root, Node3D? target)
{
    var pathParts = new List<string>();
    Node? current = target;

    while (current != null && current != root)
    {
        pathParts.Add(current.Name);
        current = current.GetParent();
    }

    pathParts.Reverse();
    return new NodePath(string.Join("/", pathParts));
}
```

---

## AnimationPlayer Setup

### Creating Animation Player
```csharp
public static AnimationPlayer CreateAnimationPlayer(
    Node3D parent,
    string monsterType,
    LimbNodes limbs)
{
    var personality = GetPersonalityForMonsterType(monsterType);
    var player = new AnimationPlayer { Name = "AnimationPlayer" };
    parent.AddChild(player);

    // Name limb nodes for animation tracks
    NameLimbNodes(limbs);

    // Calculate animation paths
    _currentPaths = LimbAnimationPaths.FromLimbs(parent, limbs);

    // Store base positions
    var basePos = LimbBasePositions.FromLimbs(limbs);

    // Create all 18 animations
    CreateIdleAnimations(player, limbs, basePos, personality);
    CreateWalkAnimations(player, limbs, basePos, personality);
    CreateRunAnimations(player, limbs, basePos, personality);
    CreateAttackAnimations(player, limbs, basePos, personality);
    CreateHitAnimations(player, limbs, basePos, personality);
    CreateDieAnimations(player, limbs, basePos, personality);

    // Start with idle
    player.CurrentAnimation = "idle_0";
    player.Play("idle_0");

    return player;
}
```

### Naming Limb Nodes
```csharp
void NameLimbNodes(LimbNodes limbs)
{
    if (limbs.Head != null) limbs.Head.Name = "Head";
    if (limbs.LeftArm != null) limbs.LeftArm.Name = "LeftArm";
    if (limbs.RightArm != null) limbs.RightArm.Name = "RightArm";
    if (limbs.LeftLeg != null) limbs.LeftLeg.Name = "LeftLeg";
    if (limbs.RightLeg != null) limbs.RightLeg.Name = "RightLeg";
}
```

---

## Animation Track Types

### Rotation Tracks (Primary)
```csharp
// Euler to Quaternion conversion
Quaternion EulerToQuat(float xDeg, float yDeg, float zDeg)
{
    var eulerRad = new Vector3(
        Mathf.DegToRad(xDeg),
        Mathf.DegToRad(yDeg),
        Mathf.DegToRad(zDeg)
    );
    return new Quaternion(Basis.FromEuler(eulerRad));
}

// Add rotation track
int trackIdx = animation.AddTrack(Animation.TrackType.Rotation3D);
animation.TrackSetPath(trackIdx, $"{path}:rotation");
animation.TrackInsertKey(trackIdx, 0f, EulerToQuat(0, 0, 0));    // Start
animation.TrackInsertKey(trackIdx, 0.5f, EulerToQuat(15, 0, 0)); // Mid
animation.TrackInsertKey(trackIdx, 1f, EulerToQuat(0, 0, 0));    // End
```

### Position Tracks (CRITICAL for Head)
```csharp
// Head MUST have position track to prevent drift
int posTrackIdx = animation.AddTrack(Animation.TrackType.Position3D);
animation.TrackSetPath(posTrackIdx, $"{headPath}:position");
animation.TrackInsertKey(posTrackIdx, 0f, basePos.Head);
animation.TrackInsertKey(posTrackIdx, duration, basePos.Head);
```

### Scale Tracks (Squash/Stretch)
```csharp
float squash = personality.SquashStretchAmount;

int scaleTrack = animation.AddTrack(Animation.TrackType.Scale3D);
animation.TrackSetPath(scaleTrack, $"{bodyPath}:scale");

// Squash (compress Y, expand X/Z)
animation.TrackInsertKey(scaleTrack, 0f, Vector3.One);
animation.TrackInsertKey(scaleTrack, 0.25f, new Vector3(1 + squash*0.2f, 1 - squash*0.3f, 1 + squash*0.2f));
animation.TrackInsertKey(scaleTrack, 0.5f, Vector3.One);
```

---

## Idle Animation Patterns

### Breathing Cycle
```csharp
void CreateIdleBreathing(Animation anim, LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality p)
{
    float duration = IDLE_DURATION * (1f + p.Stiffness * 0.5f); // Stiff = slower

    // Head subtle bob
    AddHeadBob(anim, limbs.Head, basePos.Head, p.IdleFrequency * 0.1f, duration);

    // Arm sway (if not stiff)
    if (p.Stiffness < 0.8f)
    {
        AddArmSway(anim, limbs.LeftArm, 5f * (1f - p.Stiffness), duration);
        AddArmSway(anim, limbs.RightArm, -5f * (1f - p.Stiffness), duration);
    }

    // Body squash (for jiggly types)
    if (p.SquashStretchAmount > 0.3f)
    {
        AddBodySquash(anim, p.SquashStretchAmount * 0.1f, duration);
    }
}
```

### Alert Idle
```csharp
void CreateIdleAlert(Animation anim, LimbNodes limbs, MonsterPersonality p)
{
    // Head looks around
    float headTurn = 20f * (1f - p.Stiffness);

    AddHeadRotation(anim, limbs.Head,
        new Vector3[] {
            Vector3.Zero,
            new Vector3(0, -headTurn, 0),
            Vector3.Zero,
            new Vector3(0, headTurn, 0),
            Vector3.Zero
        },
        IDLE_DURATION);
}
```

---

## Walk/Run Animation Patterns

### Bipedal Walk Cycle
```csharp
void CreateBipedalWalk(Animation anim, LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality p)
{
    float duration = WALK_CYCLE_DURATION / p.AttackSpeed;
    float legSwing = 30f * (1f - p.Stiffness * 0.5f);
    float armSwing = 20f * (1f - p.Stiffness * 0.5f);

    // Left leg forward, right leg back at t=0
    // Swap at t=0.5

    // Leg rotation (X axis = forward/back swing)
    AddLimbCycle(anim, _currentPaths.LeftLeg, "rotation",
        new float[] { 0, 0.5f, 1.0f },
        new Quaternion[] {
            EulerToQuat(legSwing, 0, 0),
            EulerToQuat(-legSwing, 0, 0),
            EulerToQuat(legSwing, 0, 0)
        }, duration);

    // Opposite for right leg
    AddLimbCycle(anim, _currentPaths.RightLeg, "rotation",
        new float[] { 0, 0.5f, 1.0f },
        new Quaternion[] {
            EulerToQuat(-legSwing, 0, 0),
            EulerToQuat(legSwing, 0, 0),
            EulerToQuat(-legSwing, 0, 0)
        }, duration);

    // Arms opposite to legs
    AddLimbCycle(anim, _currentPaths.LeftArm, "rotation",
        new float[] { 0, 0.5f, 1.0f },
        new Quaternion[] {
            EulerToQuat(-armSwing, 0, 0),
            EulerToQuat(armSwing, 0, 0),
            EulerToQuat(-armSwing, 0, 0)
        }, duration);
}
```

### Quadruped Walk Cycle
```csharp
void CreateQuadrupedWalk(Animation anim, LimbNodes limbs, MonsterPersonality p)
{
    float duration = WALK_CYCLE_DURATION;
    float legSwing = 25f;

    // Diagonal pairs move together:
    // Phase 0: Front-Left + Back-Right forward
    // Phase 0.5: Front-Right + Back-Left forward

    AddDiagonalPairCycle(anim, limbs.LeftArm, limbs.RightLeg, 0f, legSwing, duration);
    AddDiagonalPairCycle(anim, limbs.RightArm, limbs.LeftLeg, 0.5f, legSwing, duration);

    // Head bob
    AddHeadBob(anim, limbs.Head, 0.05f, duration / 2f);
}
```

### Spider Walk (8-Legged)
```csharp
// Legs alternate in wave pattern
// Phase offset: 0, 0.125, 0.25, 0.375 for left side
// Opposite phase for right side
```

---

## Attack Animation Patterns

### Melee Swipe
```csharp
void CreateMeleeSwipe(Animation anim, LimbNodes limbs, MonsterPersonality p)
{
    float duration = ATTACK_DURATION / p.AttackSpeed;
    float windupTime = duration * ATTACK_WINDUP_PERCENT;
    float actionTime = duration * ATTACK_ACTION_PERCENT;
    float recoveryTime = duration * ATTACK_RECOVERY_PERCENT;

    // Right arm swipe
    int armTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
    anim.TrackSetPath(armTrack, $"{_currentPaths.RightArm}:rotation");

    // Windup: arm goes back
    anim.TrackInsertKey(armTrack, 0f, EulerToQuat(0, 0, 0));
    anim.TrackInsertKey(armTrack, windupTime, EulerToQuat(-60, -30, 0));

    // Strike: fast forward swing
    anim.TrackInsertKey(armTrack, windupTime + actionTime, EulerToQuat(45, 30, 0));

    // Recovery: return to rest
    anim.TrackInsertKey(armTrack, duration, EulerToQuat(0, 0, 0));

    // Body lean into attack
    AddBodyLean(anim, 10f, windupTime, actionTime);
}
```

### Attack with Weapon
```csharp
void CreateWeaponAttack(Animation anim, LimbNodes limbs, MonsterPersonality p)
{
    // Weapon follows right arm
    // Add extra rotation for weapon swing arc

    int weaponTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
    anim.TrackSetPath(weaponTrack, $"{_currentPaths.Weapon}:rotation");

    // Overhead swing pattern
    anim.TrackInsertKey(weaponTrack, 0f, EulerToQuat(-75, 0, 0));        // Rest
    anim.TrackInsertKey(weaponTrack, windupTime, EulerToQuat(-120, 0, 0)); // Raised
    anim.TrackInsertKey(weaponTrack, windupTime + actionTime, EulerToQuat(-30, 0, 0)); // Swung
    anim.TrackInsertKey(weaponTrack, duration, EulerToQuat(-75, 0, 0));  // Return
}
```

---

## Hit Reaction Patterns

### Front Stagger
```csharp
void CreateFrontStagger(Animation anim, LimbNodes limbs, MonsterPersonality p)
{
    float duration = HIT_DURATION;
    float staggerAngle = 15f * (1f - p.Stiffness);

    // Body rocks back
    int bodyTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
    anim.TrackInsertKey(bodyTrack, 0f, EulerToQuat(0, 0, 0));
    anim.TrackInsertKey(bodyTrack, 0.1f, EulerToQuat(-staggerAngle, 0, 0)); // Quick back
    anim.TrackInsertKey(bodyTrack, duration, EulerToQuat(0, 0, 0));         // Recover

    // Head whips back
    AddHeadWhip(anim, limbs.Head, -staggerAngle * 1.5f, duration);

    // Arms flail
    if (p.Stiffness < 0.7f)
        AddArmFlail(anim, limbs, staggerAngle, duration);
}
```

---

## Death Animation Patterns

### Collapse Death
```csharp
void CreateCollapseDeath(Animation anim, LimbNodes limbs, MonsterPersonality p)
{
    float duration = DIE_DURATION;

    // Body falls forward
    int bodyTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
    anim.TrackInsertKey(bodyTrack, 0f, EulerToQuat(0, 0, 0));
    anim.TrackInsertKey(bodyTrack, duration * 0.3f, EulerToQuat(10, 0, 5));   // Stagger
    anim.TrackInsertKey(bodyTrack, duration * 0.6f, EulerToQuat(45, 0, 10));  // Falling
    anim.TrackInsertKey(bodyTrack, duration, EulerToQuat(90, 0, 15));         // On ground

    // Head drops
    AddHeadDrop(anim, limbs.Head, duration);

    // Arms go limp
    AddLimbsGoLimp(anim, limbs, duration);

    // Squash on impact (for elastic types)
    if (p.SquashStretchAmount > 0.2f)
        AddImpactSquash(anim, duration * 0.6f, p.SquashStretchAmount);
}
```

---

## Playing Animations

### Animation Selection
```csharp
public void PlayAnimation(AnimationType type, int variant = -1)
{
    if (variant < 0)
        variant = _rng.RandiRange(0, 2);  // Random variant

    string animName = $"{type.ToString().ToLower()}_{variant}";

    if (_animPlayer.HasAnimation(animName))
    {
        _animPlayer.Play(animName);
        _currentAnimType = type;
        _currentAnimVariant = variant;
    }
}
```

### State-Based Animation
```csharp
void ChangeState(State newState)
{
    CurrentState = newState;

    switch (newState)
    {
        case State.Idle:
            PlayAnimation(AnimationType.Idle);
            break;
        case State.Patrolling:
            PlayAnimation(AnimationType.Walk);
            break;
        case State.Aggro:
            PlayAnimation(AnimationType.Run);
            break;
        case State.Attacking:
            PlayAnimation(AnimationType.Attack);
            break;
        case State.Dead:
            PlayAnimation(AnimationType.Die);
            break;
    }
}
```

---

## Common Mistakes to AVOID

| Mistake | Problem | Fix |
|---------|---------|-----|
| No position track on head | Head drifts between anims | Always add head position track |
| Wrong animation path | Tracks don't affect limbs | Use `LimbAnimationPaths.FromLimbs()` |
| Forgot to name limb nodes | Empty paths | Call `NameLimbNodes()` first |
| Using degrees instead of Quaternion | Gimbal lock | Use `EulerToQuat()` helper |
| Same duration for all personalities | Unnatural | Scale by `AttackSpeed`, `Stiffness` |
| No looping on walk/idle | Animation stops | Set `animation.LoopMode = LoopMode.Linear` |
| Attack too fast | Can't see it | Min 0.4s even with high AttackSpeed |

---

## Performance Tips

1. **Reuse animations** - Create once per monster type, share instances
2. **LOD animations** - Fewer keyframes for distant monsters
3. **Skip sleeping enemies** - Don't process animation for enemies >40m away
4. **Batch updates** - Process animation changes in `_PhysicsProcess`, not `_Process`

---

## Workflow

1. **Define Personality** - Set squash, style, speed, stiffness
2. **Create LimbNodes** - Build monster mesh with animation points
3. **Capture Base Positions** - Store limb positions before animation
4. **Calculate Paths** - Get NodePath from root to each limb
5. **Create AnimationPlayer** - Add as child of monster root
6. **Build All 18 Animations** - 6 types × 3 variants each
7. **Test State Transitions** - Verify smooth animation changes
8. **Tune Timing** - Adjust durations per personality
