using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Animation type categories
/// </summary>
public enum AnimationType
{
    Idle,
    Walk,
    Run,
    Attack,
    Hit,
    Die
}

/// <summary>
/// Monster personality traits affecting animation style
/// </summary>
public struct MonsterPersonality
{
    public float SquashStretchAmount; // 0-1, how much elasticity (high for slime, low for skeleton)
    public MovementStyle MovementStyle;
    public float IdleFrequency; // How often secondary motions occur (0-1)
    public float AttackSpeed; // Speed multiplier for attacks (0.5-2.0)
    public float Bounciness; // How much bounce/spring in movements (0-1)
    public float Stiffness; // How rigid movements are (0-1, inverse of fluidity)

    public static MonsterPersonality Default => new()
    {
        SquashStretchAmount = 0.3f,
        MovementStyle = MovementStyle.Smooth,
        IdleFrequency = 0.5f,
        AttackSpeed = 1.0f,
        Bounciness = 0.3f,
        Stiffness = 0.5f
    };

    public static MonsterPersonality Goblin => new()
    {
        SquashStretchAmount = 0.2f,
        MovementStyle = MovementStyle.Twitchy,
        IdleFrequency = 0.7f,
        AttackSpeed = 1.2f,
        Bounciness = 0.4f,
        Stiffness = 0.3f
    };

    public static MonsterPersonality Slime => new()
    {
        SquashStretchAmount = 0.8f,
        MovementStyle = MovementStyle.Jiggly,
        IdleFrequency = 0.9f,
        AttackSpeed = 0.8f,
        Bounciness = 0.8f,
        Stiffness = 0.1f
    };

    public static MonsterPersonality Skeleton => new()
    {
        SquashStretchAmount = 0.05f,
        MovementStyle = MovementStyle.Stiff,
        IdleFrequency = 0.3f,
        AttackSpeed = 1.0f,
        Bounciness = 0.1f,
        Stiffness = 0.9f
    };

    public static MonsterPersonality Dragon => new()
    {
        SquashStretchAmount = 0.1f,
        MovementStyle = MovementStyle.Majestic,
        IdleFrequency = 0.4f,
        AttackSpeed = 0.7f,
        Bounciness = 0.2f,
        Stiffness = 0.7f
    };

    public static MonsterPersonality Spider => new()
    {
        SquashStretchAmount = 0.15f,
        MovementStyle = MovementStyle.Twitchy,
        IdleFrequency = 0.8f,
        AttackSpeed = 1.5f,
        Bounciness = 0.3f,
        Stiffness = 0.4f
    };

    public static MonsterPersonality Wolf => new()
    {
        SquashStretchAmount = 0.25f,
        MovementStyle = MovementStyle.Predatory,
        IdleFrequency = 0.5f,
        AttackSpeed = 1.3f,
        Bounciness = 0.4f,
        Stiffness = 0.4f
    };

    // DCC-Themed Monster Personalities

    /// <summary>Robotic/mechanical monsters - stiff, precise movements</summary>
    public static MonsterPersonality Machine => new()
    {
        SquashStretchAmount = 0.0f,
        MovementStyle = MovementStyle.Stiff,
        IdleFrequency = 0.2f,
        AttackSpeed = 1.1f,
        Bounciness = 0.0f,
        Stiffness = 1.0f
    };

    /// <summary>Flesh golem, plague bearer - heavy, lumbering movements</summary>
    public static MonsterPersonality HeavyBrute => new()
    {
        SquashStretchAmount = 0.15f,
        MovementStyle = MovementStyle.Smooth,
        IdleFrequency = 0.3f,
        AttackSpeed = 0.7f,
        Bounciness = 0.2f,
        Stiffness = 0.8f
    };

    /// <summary>Shadow stalker, void spawn - ethereal, ghostly movements</summary>
    public static MonsterPersonality Ethereal => new()
    {
        SquashStretchAmount = 0.4f,
        MovementStyle = MovementStyle.Smooth,
        IdleFrequency = 0.6f,
        AttackSpeed = 1.0f,
        Bounciness = 0.5f,
        Stiffness = 0.2f
    };

    /// <summary>Elemental monsters - fluid, flowing movements</summary>
    public static MonsterPersonality Elemental => new()
    {
        SquashStretchAmount = 0.5f,
        MovementStyle = MovementStyle.Jiggly,
        IdleFrequency = 0.8f,
        AttackSpeed = 0.9f,
        Bounciness = 0.6f,
        Stiffness = 0.3f
    };

    /// <summary>Gelatinous cube - bouncy, wobbly movements</summary>
    public static MonsterPersonality Gelatinous => new()
    {
        SquashStretchAmount = 0.9f,
        MovementStyle = MovementStyle.Jiggly,
        IdleFrequency = 0.95f,
        AttackSpeed = 0.6f,
        Bounciness = 0.9f,
        Stiffness = 0.05f
    };

    /// <summary>Crawler killer, living armor - aggressive, precise humanoid</summary>
    public static MonsterPersonality CombatRobot => new()
    {
        SquashStretchAmount = 0.05f,
        MovementStyle = MovementStyle.Twitchy,
        IdleFrequency = 0.4f,
        AttackSpeed = 1.4f,
        Bounciness = 0.1f,
        Stiffness = 0.85f
    };

    /// <summary>Mimic - deceptive, springs to action</summary>
    public static MonsterPersonality Mimic => new()
    {
        SquashStretchAmount = 0.3f,
        MovementStyle = MovementStyle.Twitchy,
        IdleFrequency = 0.1f, // Stays very still until attacking
        AttackSpeed = 1.8f,   // Very fast attack
        Bounciness = 0.3f,
        Stiffness = 0.6f
    };

    /// <summary>Dungeon rat - skittish, quick movements</summary>
    public static MonsterPersonality Rat => new()
    {
        SquashStretchAmount = 0.2f,
        MovementStyle = MovementStyle.Twitchy,
        IdleFrequency = 0.9f,
        AttackSpeed = 1.6f,
        Bounciness = 0.5f,
        Stiffness = 0.3f
    };

    /// <summary>Boss monsters - powerful, deliberate movements</summary>
    public static MonsterPersonality Boss => new()
    {
        SquashStretchAmount = 0.1f,
        MovementStyle = MovementStyle.Majestic,
        IdleFrequency = 0.3f,
        AttackSpeed = 0.8f,
        Bounciness = 0.15f,
        Stiffness = 0.75f
    };
}

/// <summary>
/// Movement style affecting animation behavior
/// </summary>
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

/// <summary>
/// Comprehensive animation system for procedural monsters.
/// Creates keyframe animations using Godot's AnimationPlayer.
/// </summary>
public static class MonsterAnimationSystem
{
    // Animation timing constants
    private const float IDLE_DURATION = 2.0f;
    private const float WALK_CYCLE_DURATION = 1.0f;
    private const float RUN_CYCLE_DURATION = 0.5f;
    private const float ATTACK_DURATION = 0.8f;
    private const float HIT_DURATION = 0.4f;
    private const float DIE_DURATION = 2.0f;

    // Attack phases (percentages of total attack time)
    private const float ATTACK_WINDUP_PERCENT = 0.3f;
    private const float ATTACK_ACTION_PERCENT = 0.4f;
    private const float ATTACK_RECOVERY_PERCENT = 0.3f;

    /// <summary>
    /// Stores base positions for limb nodes (used for animation offsets)
    /// </summary>
    private struct LimbBasePositions
    {
        public Vector3 Head;
        public Vector3 Body;
        public Vector3 LeftArm;
        public Vector3 RightArm;
        public Vector3 LeftLeg;
        public Vector3 RightLeg;
        public Vector3 Tail;

        public static LimbBasePositions FromLimbs(MonsterMeshFactory.LimbNodes limbs)
        {
            return new LimbBasePositions
            {
                Head = limbs.Head?.Position ?? Vector3.Zero,
                Body = limbs.Body?.Position ?? Vector3.Zero,
                LeftArm = limbs.LeftArm?.Position ?? Vector3.Zero,
                RightArm = limbs.RightArm?.Position ?? Vector3.Zero,
                LeftLeg = limbs.LeftLeg?.Position ?? Vector3.Zero,
                RightLeg = limbs.RightLeg?.Position ?? Vector3.Zero,
                Tail = limbs.Tail?.Position ?? Vector3.Zero
            };
        }
    }

    /// <summary>
    /// Helper to convert Euler angles (degrees) to Quaternion for rotation tracks
    /// </summary>
    private static Quaternion EulerToQuat(float xDeg, float yDeg, float zDeg)
    {
        var eulerRad = new Vector3(Mathf.DegToRad(xDeg), Mathf.DegToRad(yDeg), Mathf.DegToRad(zDeg));
        return new Quaternion(Basis.FromEuler(eulerRad));
    }

    /// <summary>
    /// Create a fully configured AnimationPlayer for a monster
    /// </summary>
    public static AnimationPlayer CreateAnimationPlayer(Node3D parent, string monsterType, MonsterMeshFactory.LimbNodes limbs)
    {
        var personality = GetPersonalityForMonsterType(monsterType);
        var player = new AnimationPlayer
        {
            Name = "AnimationPlayer"
        };
        parent.AddChild(player);

        // Store monster type for animation style decisions
        _currentMonsterType = monsterType;

        // Name the limb nodes so animation tracks can find them
        NameLimbNodes(limbs);

        // Compute animation paths from parent (AnimationPlayer root) to each limb
        // This handles complex hierarchies like dragon neck chains
        _currentPaths = LimbAnimationPaths.FromLimbs(parent, limbs);

        // Store base positions before any animations modify them
        var basePos = LimbBasePositions.FromLimbs(limbs);

        // Create all animation variants
        CreateIdleAnimations(player, limbs, basePos, personality);
        CreateWalkAnimations(player, limbs, basePos, personality);
        CreateRunAnimations(player, limbs, basePos, personality);
        CreateAttackAnimations(player, limbs, basePos, personality);
        CreateHitAnimations(player, limbs, basePos, personality);
        CreateDieAnimations(player, limbs, basePos, personality);

        // Set default animation
        if (player.HasAnimation("idle_0"))
        {
            player.CurrentAnimation = "idle_0";
            player.Play("idle_0");
        }

        GD.Print($"[MonsterAnimationSystem] Created AnimationPlayer for {monsterType} with {player.GetAnimationList().Length} animations");
        GD.Print($"  Head path: {_currentPaths.Head}, LeftArm: {_currentPaths.LeftArm}");
        return player;
    }

    /// <summary>
    /// Ensure limb nodes have proper names for animation paths
    /// </summary>
    private static void NameLimbNodes(MonsterMeshFactory.LimbNodes limbs)
    {
        if (limbs.Head != null) limbs.Head.Name = "Head";
        if (limbs.Body != null) limbs.Body.Name = "Body";
        if (limbs.LeftArm != null) limbs.LeftArm.Name = "LeftArm";
        if (limbs.RightArm != null) limbs.RightArm.Name = "RightArm";
        if (limbs.LeftLeg != null) limbs.LeftLeg.Name = "LeftLeg";
        if (limbs.RightLeg != null) limbs.RightLeg.Name = "RightLeg";
        if (limbs.Tail != null) limbs.Tail.Name = "Tail";
        if (limbs.Weapon != null) limbs.Weapon.Name = "Weapon";
        if (limbs.Torch != null) limbs.Torch.Name = "Torch";
    }

    /// <summary>
    /// Stores the animation paths from the AnimationPlayer's root to each limb node.
    /// This is needed because limbs may be nested in hierarchies (e.g., dragon head in neck chain).
    /// </summary>
    private struct LimbAnimationPaths
    {
        public NodePath Head;
        public NodePath Body;
        public NodePath LeftArm;
        public NodePath RightArm;
        public NodePath LeftLeg;
        public NodePath RightLeg;
        public NodePath Tail;
        public NodePath Weapon;
        public NodePath Torch;

        public static LimbAnimationPaths FromLimbs(Node3D root, MonsterMeshFactory.LimbNodes limbs)
        {
            return new LimbAnimationPaths
            {
                Head = GetRelativePath(root, limbs.Head),
                Body = GetRelativePath(root, limbs.Body),
                LeftArm = GetRelativePath(root, limbs.LeftArm),
                RightArm = GetRelativePath(root, limbs.RightArm),
                LeftLeg = GetRelativePath(root, limbs.LeftLeg),
                RightLeg = GetRelativePath(root, limbs.RightLeg),
                Tail = GetRelativePath(root, limbs.Tail),
                Weapon = GetRelativePath(root, limbs.Weapon),
                Torch = GetRelativePath(root, limbs.Torch)
            };
        }

        /// <summary>
        /// Get the NodePath from root to target, or empty path if target is null or not in tree.
        /// </summary>
        private static NodePath GetRelativePath(Node3D root, Node3D? target)
        {
            if (target == null || root == null) return new NodePath("");

            // Build path from target up to root
            var pathParts = new System.Collections.Generic.List<string>();
            Node? current = target;

            while (current != null && current != root)
            {
                pathParts.Add(current.Name);
                current = current.GetParent();
            }

            // If we didn't reach root, target is not a descendant
            if (current != root)
            {
                GD.PrintErr($"[MonsterAnimationSystem] Node {target.Name} is not a descendant of {root.Name}");
                return new NodePath(target.Name);  // Fallback to simple name
            }

            // Reverse to get path from root to target
            pathParts.Reverse();
            return new NodePath(string.Join("/", pathParts));
        }
    }

    // Store animation paths for use in animation creation methods
    private static LimbAnimationPaths _currentPaths;

    // Store current monster type for animation style decisions
    private static string _currentMonsterType = "";

    /// <summary>
    /// Get animation name from type and variant index
    /// </summary>
    public static string GetAnimationName(AnimationType type, int variant)
    {
        return $"{type.ToString().ToLower()}_{variant}";
    }

    /// <summary>
    /// Get personality parameters for a specific monster type
    /// </summary>
    private static MonsterPersonality GetPersonalityForMonsterType(string monsterType)
    {
        return monsterType.ToLower() switch
        {
            // Original monsters
            "goblin" or "goblin_shaman" or "goblin_thrower" or "goblin_warlord" => MonsterPersonality.Goblin,
            "slime" or "slime_king" => MonsterPersonality.Slime,
            "skeleton" or "skeleton_lord" => MonsterPersonality.Skeleton,
            "dragon" or "dragon_king" => MonsterPersonality.Dragon,
            "spider" or "spider_queen" or "clockwork_spider" => MonsterPersonality.Spider,
            "wolf" => MonsterPersonality.Wolf,

            // DCC-Themed Humanoid monsters
            "crawler_killer" or "living_armor" => MonsterPersonality.CombatRobot,
            "shadow_stalker" or "void_spawn" => MonsterPersonality.Ethereal,
            "flesh_golem" or "plague_bearer" => MonsterPersonality.HeavyBrute,

            // DCC-Themed Machine monsters
            "camera_drone" or "shock_drone" or "advertiser_bot" => MonsterPersonality.Machine,

            // DCC-Themed Elemental monsters
            "lava_elemental" or "ice_wraith" => MonsterPersonality.Elemental,

            // DCC-Themed Aberration monsters
            "gelatinous_cube" => MonsterPersonality.Gelatinous,
            "mimic" => MonsterPersonality.Mimic,

            // DCC-Themed Beast monsters
            "dungeon_rat" => MonsterPersonality.Rat,
            "badlama" => MonsterPersonality.Wolf, // Similar quadruped movement
            "bat" => MonsterPersonality.Rat, // Quick, darting movements like rat
            "eye" or "mushroom" or "lizard" => MonsterPersonality.Default,

            // DCC-Themed Boss monsters
            "the_butcher" or "boss_butcher" => MonsterPersonality.Boss,
            "syndicate_enforcer" or "boss_enforcer" => MonsterPersonality.CombatRobot,
            "hive_mother" or "boss_hive" => MonsterPersonality.Boss,
            "architects_favorite" or "boss_architect" => MonsterPersonality.Machine,
            "mordecais_shadow" or "boss_mordecai" => MonsterPersonality.Ethereal,

            _ => MonsterPersonality.Default
        };
    }

    #region Idle Animations

    /// <summary>
    /// Create 3 idle animation variants:
    /// 0 = Breathing (subtle up/down)
    /// 1 = Alert (head turning, looking around)
    /// 2 = Aggressive (tensed, ready to pounce)
    /// </summary>
    private static void CreateIdleAnimations(AnimationPlayer player, MonsterMeshFactory.LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality personality)
    {
        // Use passed-in base positions for animation offsets
        Vector3 headBasePos = basePos.Head;

        // Variant 0: Breathing
        var breathingAnim = new Animation();
        breathingAnim.Length = IDLE_DURATION;
        breathingAnim.LoopMode = Animation.LoopModeEnum.Linear;

        if (limbs.Head != null && !_currentPaths.Head.IsEmpty)
        {
            int headTrack = breathingAnim.AddTrack(Animation.TrackType.Position3D);
            breathingAnim.TrackSetPath(headTrack, _currentPaths.Head);

            // Subtle breathing motion - use base position + offset
            float breatheAmount = 0.05f + 0.03f * personality.SquashStretchAmount;
            breathingAnim.PositionTrackInsertKey(headTrack, 0.0f, headBasePos);
            breathingAnim.PositionTrackInsertKey(headTrack, IDLE_DURATION * 0.5f, headBasePos + new Vector3(0, breatheAmount, 0));
            breathingAnim.PositionTrackInsertKey(headTrack, IDLE_DURATION, headBasePos);

            breathingAnim.TrackSetInterpolationType(headTrack, Animation.InterpolationType.Cubic);
        }

        // Tail gentle sway during breathing (if present)
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = breathingAnim.AddTrack(Animation.TrackType.Rotation3D);
            breathingAnim.TrackSetPath(tailTrack, _currentPaths.Tail);

            float tailSwayAngle = Mathf.DegToRad(8f * personality.IdleFrequency);
            breathingAnim.RotationTrackInsertKey(tailTrack, 0.0f, Quaternion.Identity);
            breathingAnim.RotationTrackInsertKey(tailTrack, IDLE_DURATION * 0.25f, new Quaternion(Vector3.Up, tailSwayAngle));
            breathingAnim.RotationTrackInsertKey(tailTrack, IDLE_DURATION * 0.5f, Quaternion.Identity);
            breathingAnim.RotationTrackInsertKey(tailTrack, IDLE_DURATION * 0.75f, new Quaternion(Vector3.Up, -tailSwayAngle));
            breathingAnim.RotationTrackInsertKey(tailTrack, IDLE_DURATION, Quaternion.Identity);

            breathingAnim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Cubic);
        }

        // Body subtle sway (if present)
        if (limbs.Body != null && !_currentPaths.Body.IsEmpty)
        {
            int bodyTrack = breathingAnim.AddTrack(Animation.TrackType.Rotation3D);
            breathingAnim.TrackSetPath(bodyTrack, _currentPaths.Body);

            float bodySway = Mathf.DegToRad(2f * (1f - personality.Stiffness));
            breathingAnim.RotationTrackInsertKey(bodyTrack, 0.0f, Quaternion.Identity);
            breathingAnim.RotationTrackInsertKey(bodyTrack, IDLE_DURATION * 0.5f, EulerToQuat(bodySway, 0, bodySway * 0.5f));
            breathingAnim.RotationTrackInsertKey(bodyTrack, IDLE_DURATION, Quaternion.Identity);

            breathingAnim.TrackSetInterpolationType(bodyTrack, Animation.InterpolationType.Cubic);
        }

        player.AddAnimationLibrary("", new AnimationLibrary());
        player.GetAnimationLibrary("").AddAnimation("idle_0", breathingAnim);

        // Variant 1: Alert (head turning)
        var alertAnim = new Animation();
        alertAnim.Length = IDLE_DURATION * 1.5f;
        alertAnim.LoopMode = Animation.LoopModeEnum.Linear;

        if (limbs.Head != null && !_currentPaths.Head.IsEmpty)
        {
            // Position track to maintain head at correct height during rotation
            int headPosTrack = alertAnim.AddTrack(Animation.TrackType.Position3D);
            alertAnim.TrackSetPath(headPosTrack, _currentPaths.Head);
            alertAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos);
            alertAnim.PositionTrackInsertKey(headPosTrack, alertAnim.Length, headBasePos);
            alertAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Linear);

            int headRotTrack = alertAnim.AddTrack(Animation.TrackType.Rotation3D);
            alertAnim.TrackSetPath(headRotTrack, _currentPaths.Head);

            // Look left, center, right, center
            float lookAngle = Mathf.DegToRad(25);
            alertAnim.RotationTrackInsertKey(headRotTrack, 0.0f, Quaternion.Identity);
            alertAnim.RotationTrackInsertKey(headRotTrack, 0.4f, new Quaternion(Vector3.Up, -lookAngle));
            alertAnim.RotationTrackInsertKey(headRotTrack, 0.8f, Quaternion.Identity);
            alertAnim.RotationTrackInsertKey(headRotTrack, 1.2f, new Quaternion(Vector3.Up, lookAngle));
            alertAnim.RotationTrackInsertKey(headRotTrack, 1.6f, Quaternion.Identity);

            alertAnim.TrackSetInterpolationType(headRotTrack, Animation.InterpolationType.Cubic);
        }

        // Tail alert twitching - faster, more nervous motion
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = alertAnim.AddTrack(Animation.TrackType.Rotation3D);
            alertAnim.TrackSetPath(tailTrack, _currentPaths.Tail);

            float tailTwitchAngle = Mathf.DegToRad(12f * personality.IdleFrequency);
            // Faster, irregular tail motion when alert
            alertAnim.RotationTrackInsertKey(tailTrack, 0.0f, Quaternion.Identity);
            alertAnim.RotationTrackInsertKey(tailTrack, 0.2f, new Quaternion(Vector3.Up, tailTwitchAngle));
            alertAnim.RotationTrackInsertKey(tailTrack, 0.35f, Quaternion.Identity);
            alertAnim.RotationTrackInsertKey(tailTrack, 0.5f, new Quaternion(Vector3.Up, -tailTwitchAngle * 0.7f));
            alertAnim.RotationTrackInsertKey(tailTrack, 0.8f, Quaternion.Identity);
            alertAnim.RotationTrackInsertKey(tailTrack, 1.0f, new Quaternion(Vector3.Up, tailTwitchAngle * 0.5f));
            alertAnim.RotationTrackInsertKey(tailTrack, alertAnim.Length, Quaternion.Identity);

            alertAnim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("idle_1", alertAnim);

        // Variant 2: Aggressive (tensed stance)
        var aggressiveAnim = new Animation();
        aggressiveAnim.Length = IDLE_DURATION * 0.8f;
        aggressiveAnim.LoopMode = Animation.LoopModeEnum.Linear;

        // Crouched, ready to attack - use base position + offset
        if (limbs.Head != null)
        {
            int headPosTrack = aggressiveAnim.AddTrack(Animation.TrackType.Position3D);
            aggressiveAnim.TrackSetPath(headPosTrack, _currentPaths.Head);

            float crouchAmount = -0.1f;
            aggressiveAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos + new Vector3(0, crouchAmount, 0.05f));
            aggressiveAnim.PositionTrackInsertKey(headPosTrack, aggressiveAnim.Length * 0.5f, headBasePos + new Vector3(0, crouchAmount - 0.03f, 0.05f));
            aggressiveAnim.PositionTrackInsertKey(headPosTrack, aggressiveAnim.Length, headBasePos + new Vector3(0, crouchAmount, 0.05f));

            aggressiveAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Cubic);
        }

        // Arms ready
        if (limbs.LeftArm != null)
        {
            int leftArmTrack = aggressiveAnim.AddTrack(Animation.TrackType.Rotation3D);
            aggressiveAnim.TrackSetPath(leftArmTrack, _currentPaths.LeftArm);
            aggressiveAnim.RotationTrackInsertKey(leftArmTrack, 0.0f, EulerToQuat(-45, 0, 0));
            aggressiveAnim.TrackSetInterpolationType(leftArmTrack, Animation.InterpolationType.Cubic);
        }

        if (limbs.RightArm != null)
        {
            int rightArmTrack = aggressiveAnim.AddTrack(Animation.TrackType.Rotation3D);
            aggressiveAnim.TrackSetPath(rightArmTrack, _currentPaths.RightArm);
            aggressiveAnim.RotationTrackInsertKey(rightArmTrack, 0.0f, EulerToQuat(-45, 0, 0));
            aggressiveAnim.TrackSetInterpolationType(rightArmTrack, Animation.InterpolationType.Cubic);
        }

        // Tail rigid/raised when aggressive - held tense with small twitches
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = aggressiveAnim.AddTrack(Animation.TrackType.Rotation3D);
            aggressiveAnim.TrackSetPath(tailTrack, _currentPaths.Tail);

            float tailRaiseAngle = Mathf.DegToRad(-15f); // Raised up
            float tailTwitch = Mathf.DegToRad(3f);       // Small twitches
            aggressiveAnim.RotationTrackInsertKey(tailTrack, 0.0f, EulerToQuat(tailRaiseAngle, 0, 0));
            aggressiveAnim.RotationTrackInsertKey(tailTrack, aggressiveAnim.Length * 0.3f, EulerToQuat(tailRaiseAngle, tailTwitch, 0));
            aggressiveAnim.RotationTrackInsertKey(tailTrack, aggressiveAnim.Length * 0.6f, EulerToQuat(tailRaiseAngle, -tailTwitch, 0));
            aggressiveAnim.RotationTrackInsertKey(tailTrack, aggressiveAnim.Length, EulerToQuat(tailRaiseAngle, 0, 0));

            aggressiveAnim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("idle_2", aggressiveAnim);
    }

    #endregion

    #region Walk Animations

    /// <summary>
    /// Create 3 walk animation variants:
    /// 0 = Cautious (slow, careful steps)
    /// 1 = Normal (standard walking gait)
    /// 2 = Predatory (stalking, hunting)
    /// </summary>
    private static void CreateWalkAnimations(AnimationPlayer player, MonsterMeshFactory.LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality personality)
    {
        // Use passed-in base positions for animation offsets
        Vector3 headBasePos = basePos.Head;
        bool isQuadruped = IsQuadrupedMonster(_currentMonsterType);

        // Variant 0: Cautious walk
        Animation cautiousWalk;
        if (isQuadruped)
            cautiousWalk = CreateQuadrupedWalkCycle(limbs, headBasePos, WALK_CYCLE_DURATION * 1.3f, personality, 0.5f);
        else
            cautiousWalk = CreateBasicWalkCycle(limbs, headBasePos, WALK_CYCLE_DURATION * 1.3f, personality, 0.5f);
        cautiousWalk.LoopMode = Animation.LoopModeEnum.Linear;
        player.GetAnimationLibrary("").AddAnimation("walk_0", cautiousWalk);

        // Variant 1: Normal walk
        Animation normalWalk;
        if (isQuadruped)
            normalWalk = CreateQuadrupedWalkCycle(limbs, headBasePos, WALK_CYCLE_DURATION, personality, 1.0f);
        else
            normalWalk = CreateBasicWalkCycle(limbs, headBasePos, WALK_CYCLE_DURATION, personality, 1.0f);
        normalWalk.LoopMode = Animation.LoopModeEnum.Linear;
        player.GetAnimationLibrary("").AddAnimation("walk_1", normalWalk);

        // Variant 2: Predatory walk (low, stalking) - create custom cycle with lowered head
        Vector3 crouchedHeadPos = headBasePos + new Vector3(0, -0.15f, 0.1f);
        Animation predatoryWalk;
        if (isQuadruped)
            predatoryWalk = CreateQuadrupedWalkCycle(limbs, crouchedHeadPos, WALK_CYCLE_DURATION * 0.9f, personality, 1.2f);
        else
            predatoryWalk = CreateBasicWalkCycle(limbs, crouchedHeadPos, WALK_CYCLE_DURATION * 0.9f, personality, 1.2f);
        predatoryWalk.LoopMode = Animation.LoopModeEnum.Linear;

        player.GetAnimationLibrary("").AddAnimation("walk_2", predatoryWalk);
    }

    private static Animation CreateBasicWalkCycle(MonsterMeshFactory.LimbNodes limbs, Vector3 headBasePos, float duration, MonsterPersonality personality, float intensity)
    {
        var anim = new Animation();
        anim.Length = duration;

        float legSwing = Mathf.DegToRad(30) * intensity;
        float armSwing = Mathf.DegToRad(20) * intensity;
        float bobAmount = 0.08f * intensity * personality.Bounciness;

        // Left leg
        if (limbs.LeftLeg != null)
        {
            int leftLegTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(leftLegTrack, _currentPaths.LeftLeg);
            anim.RotationTrackInsertKey(leftLegTrack, 0.0f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(leftLegTrack, duration * 0.5f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(leftLegTrack, duration, new Quaternion(Vector3.Right, legSwing));
            anim.TrackSetInterpolationType(leftLegTrack, Animation.InterpolationType.Cubic);
        }

        // Right leg (opposite phase)
        if (limbs.RightLeg != null)
        {
            int rightLegTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(rightLegTrack, _currentPaths.RightLeg);
            anim.RotationTrackInsertKey(rightLegTrack, 0.0f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(rightLegTrack, duration * 0.5f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(rightLegTrack, duration, new Quaternion(Vector3.Right, -legSwing));
            anim.TrackSetInterpolationType(rightLegTrack, Animation.InterpolationType.Cubic);
        }

        // Left arm (opposite to right leg)
        if (limbs.LeftArm != null)
        {
            int leftArmTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(leftArmTrack, _currentPaths.LeftArm);
            anim.RotationTrackInsertKey(leftArmTrack, 0.0f, new Quaternion(Vector3.Right, -armSwing));
            anim.RotationTrackInsertKey(leftArmTrack, duration * 0.5f, new Quaternion(Vector3.Right, armSwing));
            anim.RotationTrackInsertKey(leftArmTrack, duration, new Quaternion(Vector3.Right, -armSwing));
            anim.TrackSetInterpolationType(leftArmTrack, Animation.InterpolationType.Cubic);
        }

        // Right arm (opposite to left leg)
        if (limbs.RightArm != null)
        {
            int rightArmTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(rightArmTrack, _currentPaths.RightArm);
            anim.RotationTrackInsertKey(rightArmTrack, 0.0f, new Quaternion(Vector3.Right, armSwing));
            anim.RotationTrackInsertKey(rightArmTrack, duration * 0.5f, new Quaternion(Vector3.Right, -armSwing));
            anim.RotationTrackInsertKey(rightArmTrack, duration, new Quaternion(Vector3.Right, armSwing));
            anim.TrackSetInterpolationType(rightArmTrack, Animation.InterpolationType.Cubic);
        }

        // Head bob - use base position + offset
        if (limbs.Head != null)
        {
            int headBobTrack = anim.AddTrack(Animation.TrackType.Position3D);
            anim.TrackSetPath(headBobTrack, _currentPaths.Head);
            anim.PositionTrackInsertKey(headBobTrack, 0.0f, headBasePos);
            anim.PositionTrackInsertKey(headBobTrack, duration * 0.25f, headBasePos + new Vector3(0, bobAmount, 0));
            anim.PositionTrackInsertKey(headBobTrack, duration * 0.5f, headBasePos);
            anim.PositionTrackInsertKey(headBobTrack, duration * 0.75f, headBasePos + new Vector3(0, bobAmount, 0));
            anim.PositionTrackInsertKey(headBobTrack, duration, headBasePos);
            anim.TrackSetInterpolationType(headBobTrack, Animation.InterpolationType.Cubic);
        }

        // Tail sway during walking - counterbalance motion
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(tailTrack, _currentPaths.Tail);

            // Tail swings opposite to leg motion for balance
            float tailSwayAngle = Mathf.DegToRad(15f * intensity * (1f - personality.Stiffness));
            anim.RotationTrackInsertKey(tailTrack, 0.0f, new Quaternion(Vector3.Up, tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.25f, Quaternion.Identity);
            anim.RotationTrackInsertKey(tailTrack, duration * 0.5f, new Quaternion(Vector3.Up, -tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.75f, Quaternion.Identity);
            anim.RotationTrackInsertKey(tailTrack, duration, new Quaternion(Vector3.Up, tailSwayAngle));

            anim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Cubic);
        }

        // Body sway during walking - weight shift
        if (limbs.Body != null && !_currentPaths.Body.IsEmpty)
        {
            int bodyTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(bodyTrack, _currentPaths.Body);

            float bodySway = Mathf.DegToRad(4f * intensity * (1f - personality.Stiffness));
            // Shift weight side to side with steps
            anim.RotationTrackInsertKey(bodyTrack, 0.0f, EulerToQuat(0, 0, bodySway));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.25f, Quaternion.Identity);
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.5f, EulerToQuat(0, 0, -bodySway));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.75f, Quaternion.Identity);
            anim.RotationTrackInsertKey(bodyTrack, duration, EulerToQuat(0, 0, bodySway));

            anim.TrackSetInterpolationType(bodyTrack, Animation.InterpolationType.Cubic);
        }

        return anim;
    }

    #endregion

    #region Run Animations

    /// <summary>
    /// Create 3 run animation variants:
    /// 0 = Charge (aggressive forward rush)
    /// 1 = Evade (lateral dodging motion)
    /// 2 = Sprint (maximum speed)
    /// </summary>
    private static void CreateRunAnimations(AnimationPlayer player, MonsterMeshFactory.LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality personality)
    {
        // Use passed-in base positions for animation offsets
        Vector3 headBasePos = basePos.Head;
        bool isQuadruped = IsQuadrupedMonster(_currentMonsterType);

        // Variant 0: Charge - head leaned forward
        Vector3 chargeHeadPos = headBasePos + new Vector3(0, -0.05f, 0.15f);
        Animation chargeAnim;
        if (isQuadruped)
            chargeAnim = CreateQuadrupedRunCycle(limbs, chargeHeadPos, RUN_CYCLE_DURATION, personality, 1.5f);
        else
            chargeAnim = CreateBasicRunCycle(limbs, chargeHeadPos, RUN_CYCLE_DURATION, personality, 1.5f);
        chargeAnim.LoopMode = Animation.LoopModeEnum.Linear;
        player.GetAnimationLibrary("").AddAnimation("run_0", chargeAnim);

        // Variant 1: Evade - uses side-to-side motion via custom run cycle
        // (Evade works for both bipeds and quadrupeds)
        var evadeAnim = CreateEvadeRunCycle(limbs, headBasePos, RUN_CYCLE_DURATION * 1.1f, personality, 1.2f);
        evadeAnim.LoopMode = Animation.LoopModeEnum.Linear;
        player.GetAnimationLibrary("").AddAnimation("run_1", evadeAnim);

        // Variant 2: Sprint
        Animation sprintAnim;
        if (isQuadruped)
            sprintAnim = CreateQuadrupedRunCycle(limbs, headBasePos, RUN_CYCLE_DURATION * 0.8f, personality, 2.0f);
        else
            sprintAnim = CreateBasicRunCycle(limbs, headBasePos, RUN_CYCLE_DURATION * 0.8f, personality, 2.0f);
        sprintAnim.LoopMode = Animation.LoopModeEnum.Linear;
        player.GetAnimationLibrary("").AddAnimation("run_2", sprintAnim);
    }

    private static Animation CreateBasicRunCycle(MonsterMeshFactory.LimbNodes limbs, Vector3 headBasePos, float duration, MonsterPersonality personality, float intensity)
    {
        var anim = new Animation();
        anim.Length = duration;

        float legSwing = Mathf.DegToRad(50) * intensity;
        float armSwing = Mathf.DegToRad(40) * intensity;
        float bobAmount = 0.12f * intensity * personality.Bounciness;

        // Legs - faster, more extreme motion than walk
        if (limbs.LeftLeg != null)
        {
            int leftLegTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(leftLegTrack, _currentPaths.LeftLeg);
            anim.RotationTrackInsertKey(leftLegTrack, 0.0f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(leftLegTrack, duration * 0.5f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(leftLegTrack, duration, new Quaternion(Vector3.Right, legSwing));
            anim.TrackSetInterpolationType(leftLegTrack, Animation.InterpolationType.Linear);
        }

        if (limbs.RightLeg != null)
        {
            int rightLegTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(rightLegTrack, _currentPaths.RightLeg);
            anim.RotationTrackInsertKey(rightLegTrack, 0.0f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(rightLegTrack, duration * 0.5f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(rightLegTrack, duration, new Quaternion(Vector3.Right, -legSwing));
            anim.TrackSetInterpolationType(rightLegTrack, Animation.InterpolationType.Linear);
        }

        // Arms pumping
        if (limbs.LeftArm != null)
        {
            int leftArmTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(leftArmTrack, _currentPaths.LeftArm);
            anim.RotationTrackInsertKey(leftArmTrack, 0.0f, new Quaternion(Vector3.Right, -armSwing));
            anim.RotationTrackInsertKey(leftArmTrack, duration * 0.5f, new Quaternion(Vector3.Right, armSwing));
            anim.RotationTrackInsertKey(leftArmTrack, duration, new Quaternion(Vector3.Right, -armSwing));
            anim.TrackSetInterpolationType(leftArmTrack, Animation.InterpolationType.Linear);
        }

        if (limbs.RightArm != null)
        {
            int rightArmTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(rightArmTrack, _currentPaths.RightArm);
            anim.RotationTrackInsertKey(rightArmTrack, 0.0f, new Quaternion(Vector3.Right, armSwing));
            anim.RotationTrackInsertKey(rightArmTrack, duration * 0.5f, new Quaternion(Vector3.Right, -armSwing));
            anim.RotationTrackInsertKey(rightArmTrack, duration, new Quaternion(Vector3.Right, armSwing));
            anim.TrackSetInterpolationType(rightArmTrack, Animation.InterpolationType.Linear);
        }

        // Pronounced head bob - use base position + offset
        if (limbs.Head != null)
        {
            int headBobTrack = anim.AddTrack(Animation.TrackType.Position3D);
            anim.TrackSetPath(headBobTrack, _currentPaths.Head);
            anim.PositionTrackInsertKey(headBobTrack, 0.0f, headBasePos);
            anim.PositionTrackInsertKey(headBobTrack, duration * 0.25f, headBasePos + new Vector3(0, bobAmount, 0));
            anim.PositionTrackInsertKey(headBobTrack, duration * 0.5f, headBasePos);
            anim.PositionTrackInsertKey(headBobTrack, duration * 0.75f, headBasePos + new Vector3(0, bobAmount, 0));
            anim.PositionTrackInsertKey(headBobTrack, duration, headBasePos);
            anim.TrackSetInterpolationType(headBobTrack, Animation.InterpolationType.Linear);
        }

        // Tail whips during running - more extreme than walking
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(tailTrack, _currentPaths.Tail);

            // Faster, more dramatic tail motion when running
            float tailSwayAngle = Mathf.DegToRad(25f * intensity * (1f - personality.Stiffness * 0.5f));
            anim.RotationTrackInsertKey(tailTrack, 0.0f, new Quaternion(Vector3.Up, tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.25f, Quaternion.Identity);
            anim.RotationTrackInsertKey(tailTrack, duration * 0.5f, new Quaternion(Vector3.Up, -tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.75f, Quaternion.Identity);
            anim.RotationTrackInsertKey(tailTrack, duration, new Quaternion(Vector3.Up, tailSwayAngle));

            anim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Linear);
        }

        // Body leans forward and sways during running
        if (limbs.Body != null && !_currentPaths.Body.IsEmpty)
        {
            int bodyTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(bodyTrack, _currentPaths.Body);

            float bodyLean = Mathf.DegToRad(8f * intensity);  // Forward lean
            float bodySway = Mathf.DegToRad(6f * intensity * (1f - personality.Stiffness));
            // Lean forward with side sway
            anim.RotationTrackInsertKey(bodyTrack, 0.0f, EulerToQuat(bodyLean, 0, bodySway));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.25f, EulerToQuat(bodyLean, 0, 0));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.5f, EulerToQuat(bodyLean, 0, -bodySway));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.75f, EulerToQuat(bodyLean, 0, 0));
            anim.RotationTrackInsertKey(bodyTrack, duration, EulerToQuat(bodyLean, 0, bodySway));

            anim.TrackSetInterpolationType(bodyTrack, Animation.InterpolationType.Linear);
        }

        return anim;
    }

    /// <summary>
    /// Create an evade-style run cycle with side-to-side head motion
    /// </summary>
    private static Animation CreateEvadeRunCycle(MonsterMeshFactory.LimbNodes limbs, Vector3 headBasePos, float duration, MonsterPersonality personality, float intensity)
    {
        var anim = new Animation();
        anim.Length = duration;

        float legSwing = Mathf.DegToRad(50) * intensity;
        float armSwing = Mathf.DegToRad(40) * intensity;
        float sideSway = 0.08f; // Side-to-side head motion

        // Legs - faster, more extreme motion than walk
        if (limbs.LeftLeg != null)
        {
            int leftLegTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(leftLegTrack, _currentPaths.LeftLeg);
            anim.RotationTrackInsertKey(leftLegTrack, 0.0f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(leftLegTrack, duration * 0.5f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(leftLegTrack, duration, new Quaternion(Vector3.Right, legSwing));
            anim.TrackSetInterpolationType(leftLegTrack, Animation.InterpolationType.Linear);
        }

        if (limbs.RightLeg != null)
        {
            int rightLegTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(rightLegTrack, _currentPaths.RightLeg);
            anim.RotationTrackInsertKey(rightLegTrack, 0.0f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(rightLegTrack, duration * 0.5f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(rightLegTrack, duration, new Quaternion(Vector3.Right, -legSwing));
            anim.TrackSetInterpolationType(rightLegTrack, Animation.InterpolationType.Linear);
        }

        // Arms - opposite phase running motion
        if (limbs.LeftArm != null)
        {
            int leftArmTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(leftArmTrack, _currentPaths.LeftArm);
            anim.RotationTrackInsertKey(leftArmTrack, 0.0f, new Quaternion(Vector3.Right, -armSwing));
            anim.RotationTrackInsertKey(leftArmTrack, duration * 0.5f, new Quaternion(Vector3.Right, armSwing));
            anim.RotationTrackInsertKey(leftArmTrack, duration, new Quaternion(Vector3.Right, -armSwing));
            anim.TrackSetInterpolationType(leftArmTrack, Animation.InterpolationType.Linear);
        }

        if (limbs.RightArm != null)
        {
            int rightArmTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(rightArmTrack, _currentPaths.RightArm);
            anim.RotationTrackInsertKey(rightArmTrack, 0.0f, new Quaternion(Vector3.Right, armSwing));
            anim.RotationTrackInsertKey(rightArmTrack, duration * 0.5f, new Quaternion(Vector3.Right, -armSwing));
            anim.RotationTrackInsertKey(rightArmTrack, duration, new Quaternion(Vector3.Right, armSwing));
            anim.TrackSetInterpolationType(rightArmTrack, Animation.InterpolationType.Linear);
        }

        // Side-to-side head motion for evasive running
        if (limbs.Head != null)
        {
            int headTrack = anim.AddTrack(Animation.TrackType.Position3D);
            anim.TrackSetPath(headTrack, _currentPaths.Head);
            anim.PositionTrackInsertKey(headTrack, 0.0f, headBasePos + new Vector3(sideSway, 0, 0));
            anim.PositionTrackInsertKey(headTrack, duration * 0.5f, headBasePos + new Vector3(-sideSway, 0, 0));
            anim.PositionTrackInsertKey(headTrack, duration, headBasePos + new Vector3(sideSway, 0, 0));
            anim.TrackSetInterpolationType(headTrack, Animation.InterpolationType.Cubic);
        }

        // Tail whips during evasion - erratic motion
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(tailTrack, _currentPaths.Tail);

            float tailSwayAngle = Mathf.DegToRad(30f * intensity);
            anim.RotationTrackInsertKey(tailTrack, 0.0f, new Quaternion(Vector3.Up, tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.5f, new Quaternion(Vector3.Up, -tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration, new Quaternion(Vector3.Up, tailSwayAngle));

            anim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Linear);
        }

        return anim;
    }

    /// <summary>
    /// Create a quadruped walk cycle where diagonal pairs move together
    /// (front-left + back-right, then front-right + back-left)
    /// </summary>
    private static Animation CreateQuadrupedWalkCycle(MonsterMeshFactory.LimbNodes limbs, Vector3 headBasePos, float duration, MonsterPersonality personality, float intensity)
    {
        var anim = new Animation();
        anim.Length = duration;

        float legSwing = Mathf.DegToRad(25f) * intensity;
        float bobAmount = 0.04f * intensity * personality.Bounciness;

        // Quadruped diagonal pair movement:
        // Phase 0.0: Front-Left + Back-Right forward, Front-Right + Back-Left back
        // Phase 0.5: Swap

        // Front left leg (LeftArm in limbs)
        if (limbs.LeftArm != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.LeftArm);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Cubic);
        }

        // Back right leg (RightLeg in limbs) - same phase as front left
        if (limbs.RightLeg != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.RightLeg);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Cubic);
        }

        // Front right leg (RightArm in limbs) - opposite phase
        if (limbs.RightArm != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.RightArm);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, -legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Cubic);
        }

        // Back left leg (LeftLeg in limbs) - same phase as front right
        if (limbs.LeftLeg != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.LeftLeg);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, -legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Cubic);
        }

        // Head bob - more frequent for quadrupeds (twice per cycle)
        if (limbs.Head != null)
        {
            int headTrack = anim.AddTrack(Animation.TrackType.Position3D);
            anim.TrackSetPath(headTrack, _currentPaths.Head);
            anim.PositionTrackInsertKey(headTrack, 0.0f, headBasePos);
            anim.PositionTrackInsertKey(headTrack, duration * 0.25f, headBasePos + new Vector3(0, bobAmount, 0));
            anim.PositionTrackInsertKey(headTrack, duration * 0.5f, headBasePos);
            anim.PositionTrackInsertKey(headTrack, duration * 0.75f, headBasePos + new Vector3(0, bobAmount, 0));
            anim.PositionTrackInsertKey(headTrack, duration, headBasePos);
            anim.TrackSetInterpolationType(headTrack, Animation.InterpolationType.Cubic);
        }

        // Tail sway - counterbalance motion
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(tailTrack, _currentPaths.Tail);

            float tailSwayAngle = Mathf.DegToRad(12f * intensity * (1f - personality.Stiffness));
            anim.RotationTrackInsertKey(tailTrack, 0.0f, new Quaternion(Vector3.Up, tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.25f, Quaternion.Identity);
            anim.RotationTrackInsertKey(tailTrack, duration * 0.5f, new Quaternion(Vector3.Up, -tailSwayAngle));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.75f, Quaternion.Identity);
            anim.RotationTrackInsertKey(tailTrack, duration, new Quaternion(Vector3.Up, tailSwayAngle));

            anim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Cubic);
        }

        // Body sway during quadruped walk
        if (limbs.Body != null && !_currentPaths.Body.IsEmpty)
        {
            int bodyTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(bodyTrack, _currentPaths.Body);

            float bodySway = Mathf.DegToRad(3f * intensity * (1f - personality.Stiffness));
            anim.RotationTrackInsertKey(bodyTrack, 0.0f, EulerToQuat(0, 0, bodySway));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.25f, Quaternion.Identity);
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.5f, EulerToQuat(0, 0, -bodySway));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.75f, Quaternion.Identity);
            anim.RotationTrackInsertKey(bodyTrack, duration, EulerToQuat(0, 0, bodySway));

            anim.TrackSetInterpolationType(bodyTrack, Animation.InterpolationType.Cubic);
        }

        return anim;
    }

    /// <summary>
    /// Create a quadruped run/gallop cycle - faster, more extreme diagonal motion
    /// </summary>
    private static Animation CreateQuadrupedRunCycle(MonsterMeshFactory.LimbNodes limbs, Vector3 headBasePos, float duration, MonsterPersonality personality, float intensity)
    {
        var anim = new Animation();
        anim.Length = duration;

        float legSwing = Mathf.DegToRad(40f) * intensity;
        float bobAmount = 0.08f * intensity * personality.Bounciness;

        // Front left + Back right (diagonal pair 1)
        if (limbs.LeftArm != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.LeftArm);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Linear);
        }

        if (limbs.RightLeg != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.RightLeg);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Linear);
        }

        // Front right + Back left (diagonal pair 2)
        if (limbs.RightArm != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.RightArm);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, -legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Linear);
        }

        if (limbs.LeftLeg != null)
        {
            int track = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(track, _currentPaths.LeftLeg);
            anim.RotationTrackInsertKey(track, 0.0f, new Quaternion(Vector3.Right, -legSwing));
            anim.RotationTrackInsertKey(track, duration * 0.5f, new Quaternion(Vector3.Right, legSwing));
            anim.RotationTrackInsertKey(track, duration, new Quaternion(Vector3.Right, -legSwing));
            anim.TrackSetInterpolationType(track, Animation.InterpolationType.Linear);
        }

        // Head bob - pronounced gallop bob
        if (limbs.Head != null)
        {
            int headTrack = anim.AddTrack(Animation.TrackType.Position3D);
            anim.TrackSetPath(headTrack, _currentPaths.Head);
            anim.PositionTrackInsertKey(headTrack, 0.0f, headBasePos);
            anim.PositionTrackInsertKey(headTrack, duration * 0.25f, headBasePos + new Vector3(0, bobAmount, 0.02f));
            anim.PositionTrackInsertKey(headTrack, duration * 0.5f, headBasePos);
            anim.PositionTrackInsertKey(headTrack, duration * 0.75f, headBasePos + new Vector3(0, bobAmount, 0.02f));
            anim.PositionTrackInsertKey(headTrack, duration, headBasePos);
            anim.TrackSetInterpolationType(headTrack, Animation.InterpolationType.Linear);
        }

        // Tail streaming behind during run
        if (limbs.Tail != null && !_currentPaths.Tail.IsEmpty)
        {
            int tailTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(tailTrack, _currentPaths.Tail);

            float tailStreamAngle = Mathf.DegToRad(20f * intensity);
            float tailSway = Mathf.DegToRad(15f * intensity);
            // Tail streams back and sways
            anim.RotationTrackInsertKey(tailTrack, 0.0f, EulerToQuat(tailStreamAngle, tailSway, 0));
            anim.RotationTrackInsertKey(tailTrack, duration * 0.5f, EulerToQuat(tailStreamAngle, -tailSway, 0));
            anim.RotationTrackInsertKey(tailTrack, duration, EulerToQuat(tailStreamAngle, tailSway, 0));

            anim.TrackSetInterpolationType(tailTrack, Animation.InterpolationType.Linear);
        }

        // Body undulates during gallop
        if (limbs.Body != null && !_currentPaths.Body.IsEmpty)
        {
            int bodyTrack = anim.AddTrack(Animation.TrackType.Rotation3D);
            anim.TrackSetPath(bodyTrack, _currentPaths.Body);

            float bodyFlex = Mathf.DegToRad(5f * intensity);
            anim.RotationTrackInsertKey(bodyTrack, 0.0f, EulerToQuat(bodyFlex, 0, 0));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.25f, EulerToQuat(0, 0, 0));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.5f, EulerToQuat(-bodyFlex * 0.5f, 0, 0));
            anim.RotationTrackInsertKey(bodyTrack, duration * 0.75f, EulerToQuat(0, 0, 0));
            anim.RotationTrackInsertKey(bodyTrack, duration, EulerToQuat(bodyFlex, 0, 0));

            anim.TrackSetInterpolationType(bodyTrack, Animation.InterpolationType.Linear);
        }

        return anim;
    }

    /// <summary>
    /// Check if monster type uses quadruped animations
    /// </summary>
    private static bool IsQuadrupedMonster(string monsterType)
    {
        return monsterType.ToLower() switch
        {
            "dungeon_rat" or "rat" => true,
            "badlama" or "llama" => true,
            "wolf" => true,
            "spider" or "spider_queen" or "clockwork_spider" => true,
            "crawler_killer" => true,
            _ => false
        };
    }

    #endregion

    #region Attack Animations

    /// <summary>
    /// Create 3 attack animation variants:
    /// 0 = Swipe (horizontal slash)
    /// 1 = Lunge (forward thrust)
    /// 2 = Overhead (vertical smash)
    /// </summary>
    private static void CreateAttackAnimations(AnimationPlayer player, MonsterMeshFactory.LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality personality)
    {
        // Use passed-in base positions for animation offsets
        Vector3 headBasePos = basePos.Head;

        float attackDuration = ATTACK_DURATION / personality.AttackSpeed;
        float windupTime = attackDuration * ATTACK_WINDUP_PERCENT;
        float actionTime = windupTime + (attackDuration * ATTACK_ACTION_PERCENT);
        float recoveryTime = attackDuration;

        // Variant 0: Swipe
        var swipeAnim = new Animation();
        swipeAnim.Length = attackDuration;
        swipeAnim.LoopMode = Animation.LoopModeEnum.None;

        // Maintain head position during swipe attack
        if (limbs.Head != null)
        {
            int headPosTrack = swipeAnim.AddTrack(Animation.TrackType.Position3D);
            swipeAnim.TrackSetPath(headPosTrack, _currentPaths.Head);
            swipeAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos);
            swipeAnim.PositionTrackInsertKey(headPosTrack, attackDuration, headBasePos);
            swipeAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Linear);
        }

        if (limbs.RightArm != null || limbs.Weapon != null)
        {
            var attackingLimb = limbs.Weapon ?? limbs.RightArm;
            if (attackingLimb != null)
            {
                int armTrack = swipeAnim.AddTrack(Animation.TrackType.Rotation3D);
                swipeAnim.TrackSetPath(armTrack, attackingLimb == limbs.Weapon ? _currentPaths.Weapon : _currentPaths.RightArm);

                // Windup - pull back
                swipeAnim.RotationTrackInsertKey(armTrack, 0.0f, Quaternion.Identity);
                swipeAnim.RotationTrackInsertKey(armTrack, windupTime, EulerToQuat(0, -90, -45));

                // Action - fast swipe
                swipeAnim.RotationTrackInsertKey(armTrack, actionTime, EulerToQuat(0, 90, 15));

                // Recovery
                swipeAnim.RotationTrackInsertKey(armTrack, recoveryTime, Quaternion.Identity);

                swipeAnim.TrackSetInterpolationType(armTrack, Animation.InterpolationType.Cubic);
            }
        }

        player.GetAnimationLibrary("").AddAnimation("attack_0", swipeAnim);

        // Variant 1: Lunge
        var lungeAnim = new Animation();
        lungeAnim.Length = attackDuration;
        lungeAnim.LoopMode = Animation.LoopModeEnum.None;

        // Body forward motion - use base position + offset
        if (limbs.Head != null)
        {
            int bodyTrack = lungeAnim.AddTrack(Animation.TrackType.Position3D);
            lungeAnim.TrackSetPath(bodyTrack, _currentPaths.Head);

            lungeAnim.PositionTrackInsertKey(bodyTrack, 0.0f, headBasePos);
            lungeAnim.PositionTrackInsertKey(bodyTrack, windupTime, headBasePos + new Vector3(0, -0.1f, -0.1f)); // Crouch back
            lungeAnim.PositionTrackInsertKey(bodyTrack, actionTime, headBasePos + new Vector3(0, 0.05f, 0.3f)); // Lunge forward
            lungeAnim.PositionTrackInsertKey(bodyTrack, recoveryTime, headBasePos);

            lungeAnim.TrackSetInterpolationType(bodyTrack, Animation.InterpolationType.Cubic);
        }

        // Weapon thrust
        if (limbs.RightArm != null || limbs.Weapon != null)
        {
            var attackingLimb = limbs.Weapon ?? limbs.RightArm;
            if (attackingLimb != null)
            {
                int armTrack = lungeAnim.AddTrack(Animation.TrackType.Rotation3D);
                lungeAnim.TrackSetPath(armTrack, attackingLimb == limbs.Weapon ? _currentPaths.Weapon : _currentPaths.RightArm);

                lungeAnim.RotationTrackInsertKey(armTrack, 0.0f, Quaternion.Identity);
                lungeAnim.RotationTrackInsertKey(armTrack, windupTime, EulerToQuat(-45, 0, 0));
                lungeAnim.RotationTrackInsertKey(armTrack, actionTime, EulerToQuat(45, 0, 0));
                lungeAnim.RotationTrackInsertKey(armTrack, recoveryTime, Quaternion.Identity);

                lungeAnim.TrackSetInterpolationType(armTrack, Animation.InterpolationType.Cubic);
            }
        }

        player.GetAnimationLibrary("").AddAnimation("attack_1", lungeAnim);

        // Variant 2: Overhead
        var overheadAnim = new Animation();
        overheadAnim.Length = attackDuration * 1.2f; // Slightly longer
        overheadAnim.LoopMode = Animation.LoopModeEnum.None;

        float overheadWindup = overheadAnim.Length * ATTACK_WINDUP_PERCENT;
        float overheadAction = overheadWindup + (overheadAnim.Length * ATTACK_ACTION_PERCENT);
        float overheadRecovery = overheadAnim.Length;

        if (limbs.RightArm != null || limbs.Weapon != null)
        {
            var attackingLimb = limbs.Weapon ?? limbs.RightArm;
            if (attackingLimb != null)
            {
                int armTrack = overheadAnim.AddTrack(Animation.TrackType.Rotation3D);
                overheadAnim.TrackSetPath(armTrack, attackingLimb == limbs.Weapon ? _currentPaths.Weapon : _currentPaths.RightArm);

                // Windup - raise high
                overheadAnim.RotationTrackInsertKey(armTrack, 0.0f, Quaternion.Identity);
                overheadAnim.RotationTrackInsertKey(armTrack, overheadWindup, EulerToQuat(-135, 0, 15));

                // Action - slam down
                overheadAnim.RotationTrackInsertKey(armTrack, overheadAction, EulerToQuat(45, 0, 0));

                // Recovery
                overheadAnim.RotationTrackInsertKey(armTrack, overheadRecovery, Quaternion.Identity);

                overheadAnim.TrackSetInterpolationType(armTrack, Animation.InterpolationType.Cubic);
            }
        }

        // Position track to maintain head position, plus squash on impact
        if (limbs.Head != null)
        {
            // Position track - maintain head position throughout attack
            int headPosTrack = overheadAnim.AddTrack(Animation.TrackType.Position3D);
            overheadAnim.TrackSetPath(headPosTrack, _currentPaths.Head);
            overheadAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos);
            overheadAnim.PositionTrackInsertKey(headPosTrack, overheadRecovery, headBasePos);
            overheadAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Linear);

            // Scale track - squash on impact
            int scaleTrack = overheadAnim.AddTrack(Animation.TrackType.Scale3D);
            overheadAnim.TrackSetPath(scaleTrack, _currentPaths.Head);

            float squashAmount = personality.SquashStretchAmount;
            overheadAnim.ScaleTrackInsertKey(scaleTrack, 0.0f, Vector3.One);
            overheadAnim.ScaleTrackInsertKey(scaleTrack, overheadAction, new Vector3(1.0f + squashAmount, 1.0f - squashAmount, 1.0f + squashAmount));
            overheadAnim.ScaleTrackInsertKey(scaleTrack, overheadAction + 0.1f, Vector3.One);

            overheadAnim.TrackSetInterpolationType(scaleTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("attack_2", overheadAnim);
    }

    #endregion

    #region Hit Animations

    /// <summary>
    /// Create 3 hit reaction animation variants:
    /// 0 = Front stagger (hit from front)
    /// 1 = Side twist (hit from side)
    /// 2 = Knockdown (heavy hit)
    /// </summary>
    private static void CreateHitAnimations(AnimationPlayer player, MonsterMeshFactory.LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality personality)
    {
        // Use passed-in base positions for animation offsets
        Vector3 headBasePos = basePos.Head;

        // Variant 0: Front stagger
        var frontStaggerAnim = new Animation();
        frontStaggerAnim.Length = HIT_DURATION;
        frontStaggerAnim.LoopMode = Animation.LoopModeEnum.None;

        if (limbs.Head != null)
        {
            int headTrack = frontStaggerAnim.AddTrack(Animation.TrackType.Position3D);
            frontStaggerAnim.TrackSetPath(headTrack, _currentPaths.Head);

            // Recoil backward - use base position + offset
            frontStaggerAnim.PositionTrackInsertKey(headTrack, 0.0f, headBasePos);
            frontStaggerAnim.PositionTrackInsertKey(headTrack, HIT_DURATION * 0.3f, headBasePos + new Vector3(0, 0, -0.15f));
            frontStaggerAnim.PositionTrackInsertKey(headTrack, HIT_DURATION, headBasePos);

            frontStaggerAnim.TrackSetInterpolationType(headTrack, Animation.InterpolationType.Cubic);
        }

        // Arms fly back
        if (limbs.LeftArm != null)
        {
            int leftArmTrack = frontStaggerAnim.AddTrack(Animation.TrackType.Rotation3D);
            frontStaggerAnim.TrackSetPath(leftArmTrack, _currentPaths.LeftArm);
            frontStaggerAnim.RotationTrackInsertKey(leftArmTrack, 0.0f, Quaternion.Identity);
            frontStaggerAnim.RotationTrackInsertKey(leftArmTrack, HIT_DURATION * 0.3f, EulerToQuat(-60, 0, -30));
            frontStaggerAnim.RotationTrackInsertKey(leftArmTrack, HIT_DURATION, Quaternion.Identity);
            frontStaggerAnim.TrackSetInterpolationType(leftArmTrack, Animation.InterpolationType.Cubic);
        }

        if (limbs.RightArm != null)
        {
            int rightArmTrack = frontStaggerAnim.AddTrack(Animation.TrackType.Rotation3D);
            frontStaggerAnim.TrackSetPath(rightArmTrack, _currentPaths.RightArm);
            frontStaggerAnim.RotationTrackInsertKey(rightArmTrack, 0.0f, Quaternion.Identity);
            frontStaggerAnim.RotationTrackInsertKey(rightArmTrack, HIT_DURATION * 0.3f, EulerToQuat(-60, 0, 30));
            frontStaggerAnim.RotationTrackInsertKey(rightArmTrack, HIT_DURATION, Quaternion.Identity);
            frontStaggerAnim.TrackSetInterpolationType(rightArmTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("hit_0", frontStaggerAnim);

        // Variant 1: Side twist
        var sideTwistAnim = new Animation();
        sideTwistAnim.Length = HIT_DURATION;
        sideTwistAnim.LoopMode = Animation.LoopModeEnum.None;

        if (limbs.Head != null)
        {
            // Position track to maintain head position during side twist
            int headPosTrack = sideTwistAnim.AddTrack(Animation.TrackType.Position3D);
            sideTwistAnim.TrackSetPath(headPosTrack, _currentPaths.Head);
            sideTwistAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos);
            sideTwistAnim.PositionTrackInsertKey(headPosTrack, HIT_DURATION, headBasePos);
            sideTwistAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Linear);

            int headRotTrack = sideTwistAnim.AddTrack(Animation.TrackType.Rotation3D);
            sideTwistAnim.TrackSetPath(headRotTrack, _currentPaths.Head);

            // Twist to side
            sideTwistAnim.RotationTrackInsertKey(headRotTrack, 0.0f, Quaternion.Identity);
            sideTwistAnim.RotationTrackInsertKey(headRotTrack, HIT_DURATION * 0.3f, EulerToQuat(0, 45, 15));
            sideTwistAnim.RotationTrackInsertKey(headRotTrack, HIT_DURATION, Quaternion.Identity);

            sideTwistAnim.TrackSetInterpolationType(headRotTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("hit_1", sideTwistAnim);

        // Variant 2: Knockdown
        var knockdownAnim = new Animation();
        knockdownAnim.Length = HIT_DURATION * 1.5f;
        knockdownAnim.LoopMode = Animation.LoopModeEnum.None;

        if (limbs.Head != null)
        {
            // Fall backward - use base position + offset
            int headPosTrack = knockdownAnim.AddTrack(Animation.TrackType.Position3D);
            knockdownAnim.TrackSetPath(headPosTrack, _currentPaths.Head);
            knockdownAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos);
            knockdownAnim.PositionTrackInsertKey(headPosTrack, knockdownAnim.Length * 0.5f, headBasePos + new Vector3(0, -0.3f, -0.2f));
            knockdownAnim.PositionTrackInsertKey(headPosTrack, knockdownAnim.Length, headBasePos + new Vector3(0, -0.15f, -0.1f));
            knockdownAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Cubic);

            // Rotate back
            int headRotTrack = knockdownAnim.AddTrack(Animation.TrackType.Rotation3D);
            knockdownAnim.TrackSetPath(headRotTrack, _currentPaths.Head);
            knockdownAnim.RotationTrackInsertKey(headRotTrack, 0.0f, Quaternion.Identity);
            knockdownAnim.RotationTrackInsertKey(headRotTrack, knockdownAnim.Length * 0.5f, EulerToQuat(-45, 0, 0));
            knockdownAnim.RotationTrackInsertKey(headRotTrack, knockdownAnim.Length, EulerToQuat(-30, 0, 0));
            knockdownAnim.TrackSetInterpolationType(headRotTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("hit_2", knockdownAnim);
    }

    #endregion

    #region Die Animations

    /// <summary>
    /// Create 3 death animation variants:
    /// 0 = Collapse (fall to knees, then forward)
    /// 1 = Dramatic fall (spin and fall backward)
    /// 2 = Fade out (dissolve/disintegrate)
    /// </summary>
    private static void CreateDieAnimations(AnimationPlayer player, MonsterMeshFactory.LimbNodes limbs, LimbBasePositions basePos, MonsterPersonality personality)
    {
        // Use passed-in base positions for animation offsets
        Vector3 headBasePos = basePos.Head;

        // Variant 0: Collapse
        var collapseAnim = new Animation();
        collapseAnim.Length = DIE_DURATION;
        collapseAnim.LoopMode = Animation.LoopModeEnum.None;

        if (limbs.Head != null)
        {
            // Fall to ground - use base position + offset
            int headPosTrack = collapseAnim.AddTrack(Animation.TrackType.Position3D);
            collapseAnim.TrackSetPath(headPosTrack, _currentPaths.Head);
            collapseAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos);
            collapseAnim.PositionTrackInsertKey(headPosTrack, DIE_DURATION * 0.3f, headBasePos + new Vector3(0, -0.3f, 0)); // Knees
            collapseAnim.PositionTrackInsertKey(headPosTrack, DIE_DURATION, headBasePos + new Vector3(0, -0.8f, 0.3f)); // Face down
            collapseAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Cubic);

            // Rotate forward
            int headRotTrack = collapseAnim.AddTrack(Animation.TrackType.Rotation3D);
            collapseAnim.TrackSetPath(headRotTrack, _currentPaths.Head);
            collapseAnim.RotationTrackInsertKey(headRotTrack, 0.0f, Quaternion.Identity);
            collapseAnim.RotationTrackInsertKey(headRotTrack, DIE_DURATION * 0.3f, EulerToQuat(-30, 0, 0));
            collapseAnim.RotationTrackInsertKey(headRotTrack, DIE_DURATION, EulerToQuat(90, 0, 0));
            collapseAnim.TrackSetInterpolationType(headRotTrack, Animation.InterpolationType.Cubic);
        }

        // Arms fall limp
        if (limbs.LeftArm != null)
        {
            int leftArmTrack = collapseAnim.AddTrack(Animation.TrackType.Rotation3D);
            collapseAnim.TrackSetPath(leftArmTrack, _currentPaths.LeftArm);
            collapseAnim.RotationTrackInsertKey(leftArmTrack, 0.0f, Quaternion.Identity);
            collapseAnim.RotationTrackInsertKey(leftArmTrack, DIE_DURATION, EulerToQuat(90, 0, -45));
            collapseAnim.TrackSetInterpolationType(leftArmTrack, Animation.InterpolationType.Cubic);
        }

        if (limbs.RightArm != null)
        {
            int rightArmTrack = collapseAnim.AddTrack(Animation.TrackType.Rotation3D);
            collapseAnim.TrackSetPath(rightArmTrack, _currentPaths.RightArm);
            collapseAnim.RotationTrackInsertKey(rightArmTrack, 0.0f, Quaternion.Identity);
            collapseAnim.RotationTrackInsertKey(rightArmTrack, DIE_DURATION, EulerToQuat(90, 0, 45));
            collapseAnim.TrackSetInterpolationType(rightArmTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("die_0", collapseAnim);

        // Variant 1: Dramatic fall
        var dramaticFallAnim = new Animation();
        dramaticFallAnim.Length = DIE_DURATION * 1.2f;
        dramaticFallAnim.LoopMode = Animation.LoopModeEnum.None;

        if (limbs.Head != null)
        {
            // Spin and fall
            int headRotTrack = dramaticFallAnim.AddTrack(Animation.TrackType.Rotation3D);
            dramaticFallAnim.TrackSetPath(headRotTrack, _currentPaths.Head);
            dramaticFallAnim.RotationTrackInsertKey(headRotTrack, 0.0f, Quaternion.Identity);
            dramaticFallAnim.RotationTrackInsertKey(headRotTrack, dramaticFallAnim.Length * 0.5f, EulerToQuat(0, 180, -45));
            dramaticFallAnim.RotationTrackInsertKey(headRotTrack, dramaticFallAnim.Length, EulerToQuat(-90, 270, -90));
            dramaticFallAnim.TrackSetInterpolationType(headRotTrack, Animation.InterpolationType.Cubic);

            // Fall backward - use base position + offset
            int headPosTrack = dramaticFallAnim.AddTrack(Animation.TrackType.Position3D);
            dramaticFallAnim.TrackSetPath(headPosTrack, _currentPaths.Head);
            dramaticFallAnim.PositionTrackInsertKey(headPosTrack, 0.0f, headBasePos);
            dramaticFallAnim.PositionTrackInsertKey(headPosTrack, dramaticFallAnim.Length * 0.3f, headBasePos + new Vector3(0, 0.1f, 0)); // Brief lift
            dramaticFallAnim.PositionTrackInsertKey(headPosTrack, dramaticFallAnim.Length, headBasePos + new Vector3(0, -0.8f, -0.3f)); // Splat
            dramaticFallAnim.TrackSetInterpolationType(headPosTrack, Animation.InterpolationType.Cubic);
        }

        // Arms flail
        if (limbs.LeftArm != null)
        {
            int leftArmTrack = dramaticFallAnim.AddTrack(Animation.TrackType.Rotation3D);
            dramaticFallAnim.TrackSetPath(leftArmTrack, _currentPaths.LeftArm);
            dramaticFallAnim.RotationTrackInsertKey(leftArmTrack, 0.0f, Quaternion.Identity);
            dramaticFallAnim.RotationTrackInsertKey(leftArmTrack, dramaticFallAnim.Length * 0.5f, EulerToQuat(-120, 0, -90));
            dramaticFallAnim.RotationTrackInsertKey(leftArmTrack, dramaticFallAnim.Length, EulerToQuat(45, 0, -45));
            dramaticFallAnim.TrackSetInterpolationType(leftArmTrack, Animation.InterpolationType.Cubic);
        }

        if (limbs.RightArm != null)
        {
            int rightArmTrack = dramaticFallAnim.AddTrack(Animation.TrackType.Rotation3D);
            dramaticFallAnim.TrackSetPath(rightArmTrack, _currentPaths.RightArm);
            dramaticFallAnim.RotationTrackInsertKey(rightArmTrack, 0.0f, Quaternion.Identity);
            dramaticFallAnim.RotationTrackInsertKey(rightArmTrack, dramaticFallAnim.Length * 0.5f, EulerToQuat(-120, 0, 90));
            dramaticFallAnim.RotationTrackInsertKey(rightArmTrack, dramaticFallAnim.Length, EulerToQuat(45, 0, 45));
            dramaticFallAnim.TrackSetInterpolationType(rightArmTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("die_1", dramaticFallAnim);

        // Variant 2: Fade out (scale down + transparency if possible)
        var fadeOutAnim = new Animation();
        fadeOutAnim.Length = DIE_DURATION * 1.5f;
        fadeOutAnim.LoopMode = Animation.LoopModeEnum.None;

        if (limbs.Head != null)
        {
            // Shrink
            int scaleTrack = fadeOutAnim.AddTrack(Animation.TrackType.Scale3D);
            fadeOutAnim.TrackSetPath(scaleTrack, _currentPaths.Head);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, 0.0f, Vector3.One);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, fadeOutAnim.Length * 0.3f, Vector3.One * 1.1f); // Brief expand
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, fadeOutAnim.Length, Vector3.One * 0.01f); // Shrink to nothing
            fadeOutAnim.TrackSetInterpolationType(scaleTrack, Animation.InterpolationType.Cubic);

            // Sink into ground - use base position + offset
            int posTrack = fadeOutAnim.AddTrack(Animation.TrackType.Position3D);
            fadeOutAnim.TrackSetPath(posTrack, _currentPaths.Head);
            fadeOutAnim.PositionTrackInsertKey(posTrack, 0.0f, headBasePos);
            fadeOutAnim.PositionTrackInsertKey(posTrack, fadeOutAnim.Length, headBasePos + new Vector3(0, -0.5f, 0));
            fadeOutAnim.TrackSetInterpolationType(posTrack, Animation.InterpolationType.Cubic);
        }

        // Scale all limbs
        if (limbs.LeftArm != null)
        {
            int scaleTrack = fadeOutAnim.AddTrack(Animation.TrackType.Scale3D);
            fadeOutAnim.TrackSetPath(scaleTrack, _currentPaths.LeftArm);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, 0.0f, Vector3.One);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, fadeOutAnim.Length, Vector3.One * 0.01f);
            fadeOutAnim.TrackSetInterpolationType(scaleTrack, Animation.InterpolationType.Cubic);
        }

        if (limbs.RightArm != null)
        {
            int scaleTrack = fadeOutAnim.AddTrack(Animation.TrackType.Scale3D);
            fadeOutAnim.TrackSetPath(scaleTrack, _currentPaths.RightArm);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, 0.0f, Vector3.One);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, fadeOutAnim.Length, Vector3.One * 0.01f);
            fadeOutAnim.TrackSetInterpolationType(scaleTrack, Animation.InterpolationType.Cubic);
        }

        if (limbs.LeftLeg != null)
        {
            int scaleTrack = fadeOutAnim.AddTrack(Animation.TrackType.Scale3D);
            fadeOutAnim.TrackSetPath(scaleTrack, _currentPaths.LeftLeg);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, 0.0f, Vector3.One);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, fadeOutAnim.Length, Vector3.One * 0.01f);
            fadeOutAnim.TrackSetInterpolationType(scaleTrack, Animation.InterpolationType.Cubic);
        }

        if (limbs.RightLeg != null)
        {
            int scaleTrack = fadeOutAnim.AddTrack(Animation.TrackType.Scale3D);
            fadeOutAnim.TrackSetPath(scaleTrack, _currentPaths.RightLeg);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, 0.0f, Vector3.One);
            fadeOutAnim.ScaleTrackInsertKey(scaleTrack, fadeOutAnim.Length, Vector3.One * 0.01f);
            fadeOutAnim.TrackSetInterpolationType(scaleTrack, Animation.InterpolationType.Cubic);
        }

        player.GetAnimationLibrary("").AddAnimation("die_2", fadeOutAnim);
    }

    #endregion

    /// <summary>
    /// Helper to play a random variant of an animation type
    /// </summary>
    public static void PlayRandomVariant(AnimationPlayer player, AnimationType type)
    {
        if (player == null) return;

        int variant = GD.RandRange(0, 2);
        string animName = GetAnimationName(type, variant);

        if (player.HasAnimation(animName))
        {
            player.Play(animName);
        }
    }

    /// <summary>
    /// Helper to play a specific animation variant
    /// </summary>
    public static void PlayAnimation(AnimationPlayer player, AnimationType type, int variant = 0)
    {
        if (player == null) return;

        string animName = GetAnimationName(type, variant);

        if (player.HasAnimation(animName))
        {
            player.Play(animName);
        }
    }

    /// <summary>
    /// Blend between two animations smoothly
    /// </summary>
    public static void BlendToAnimation(AnimationPlayer player, AnimationType targetType, int variant = 0, float blendTime = 0.2f)
    {
        if (player == null) return;

        string targetAnim = GetAnimationName(targetType, variant);

        if (player.HasAnimation(targetAnim))
        {
            // Queue the animation with custom blend time
            player.Play(targetAnim, blendTime);
        }
    }
}
