using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Defines custom sounds for each monster type. Each monster has sounds for:
/// - Idle: ambient sounds when not in combat
/// - Aggro: when spotting the player
/// - Attack: during attack animations
/// - Hit: when taking damage
/// - Die: death sound
///
/// Sound mappings use existing NinjaAdventure audio assets with pitch/volume
/// variations to create unique monster personalities.
/// </summary>
public static class MonsterSounds
{
    /// <summary>
    /// Sound configuration for a specific action
    /// </summary>
    public class SoundConfig
    {
        public string SoundKey { get; set; } = "";
        public float PitchBase { get; set; } = 1.0f;
        public float PitchVariation { get; set; } = 0.1f;
        public float VolumeDb { get; set; } = 0f;

        public SoundConfig(string key, float pitch = 1.0f, float pitchVar = 0.1f, float volume = 0f)
        {
            SoundKey = key;
            PitchBase = pitch;
            PitchVariation = pitchVar;
            VolumeDb = volume;
        }
    }

    /// <summary>
    /// Complete sound set for a monster type
    /// </summary>
    public class MonsterSoundSet
    {
        public string MonsterType { get; set; } = "";
        public SoundConfig Idle { get; set; } = new("");
        public SoundConfig[]? IdleVariants { get; set; } // Optional additional idle variants
        public SoundConfig Aggro { get; set; } = new("");
        public SoundConfig Attack { get; set; } = new("");
        public SoundConfig Hit { get; set; } = new("");
        public SoundConfig Die { get; set; } = new("");

        /// <summary>
        /// Get a random idle sound config (includes main idle and any variants)
        /// </summary>
        public SoundConfig GetRandomIdleSound()
        {
            if (IdleVariants == null || IdleVariants.Length == 0)
                return Idle;

            // Create array with main idle + variants
            int totalVariants = 1 + IdleVariants.Length;
            int choice = (int)(GD.Randi() % totalVariants);

            if (choice == 0)
                return Idle;
            return IdleVariants[choice - 1];
        }
    }

    // All monster sound configurations
    private static readonly Dictionary<string, MonsterSoundSet> _soundSets = new();

    // Sound action types for UI
    public static readonly string[] SoundActions = { "Idle", "Aggro", "Attack", "Hit", "Die" };

    static MonsterSounds()
    {
        InitializeSoundSets();
    }

    private static void InitializeSoundSets()
    {
        // ============================================
        // GOBLINS - Custom .m4a sounds from Assets/Audio/Sounds/Goblin
        // ============================================
        _soundSets["goblin"] = new MonsterSoundSet
        {
            MonsterType = "goblin",
            Idle = new("monster_goblin_idle", 1.0f, 0.1f, -3f),       // Custom goblin idle 1
            IdleVariants = new SoundConfig[]
            {
                new("monster_goblin_idle2", 1.0f, 0.1f, -3f),         // Custom goblin idle 2
                new("monster_goblin_idle3", 1.0f, 0.1f, -3f),         // Custom goblin idle 3
            },
            Aggro = new("monster_goblin_aggro", 1.0f, 0.1f, 0f),      // Custom aggro sound
            Attack = new("monster_goblin_attack", 1.0f, 0.1f, 0f),    // Custom attack sound
            Hit = new("monster_goblin_hit", 1.0f, 0.1f, 0f),          // Custom hit sound
            Die = new("monster_goblin_die", 1.0f, 0.1f, 0f),          // Custom death sound
        };

        _soundSets["goblin_shaman"] = new MonsterSoundSet
        {
            MonsterType = "goblin_shaman",
            Idle = new("monster_shaman_idle", 0.9f, 0.1f, -5f),       // Low mystical humming
            Aggro = new("monster_shaman_aggro", 0.85f, 0.1f, 0f),     // Magical incantation
            Attack = new("monster_shaman_attack", 0.8f, 0.1f, 2f),    // Spell casting sound
            Hit = new("monster_goblin_hit", 1.2f, 0.15f, 0f),         // Higher pitched pain
            Die = new("monster_shaman_die", 0.7f, 0.1f, 0f),          // Ethereal fade
        };

        _soundSets["goblin_thrower"] = new MonsterSoundSet
        {
            MonsterType = "goblin_thrower",
            Idle = new("monster_goblin_idle", 1.0f, 0.12f, -4f),      // Normal goblin idle
            Aggro = new("monster_goblin_aggro", 1.15f, 0.1f, 0f),     // Battle cry
            Attack = new("monster_thrower_attack", 1.2f, 0.1f, 0f),   // Throwing grunt
            Hit = new("monster_goblin_hit", 1.3f, 0.15f, 0f),         // Pain squeal
            Die = new("monster_goblin_die", 1.0f, 0.1f, 0f),          // Standard death
        };

        // ============================================
        // SLIME - Wet, squishy sounds
        // ============================================
        _soundSets["slime"] = new MonsterSoundSet
        {
            MonsterType = "slime",
            Idle = new("monster_slime_idle", 1.0f, 0.2f, -6f),        // Bubbling quietly
            Aggro = new("monster_slime_aggro", 0.9f, 0.15f, 0f),      // Angry bubbling
            Attack = new("monster_slime_attack", 1.1f, 0.2f, 0f),     // Wet splat
            Hit = new("monster_slime_hit", 1.2f, 0.25f, 0f),          // Squelch
            Die = new("monster_slime_die", 0.7f, 0.1f, 0f),           // Deflating pop
        };

        // ============================================
        // EYE - Eerie, psychic sounds
        // ============================================
        _soundSets["eye"] = new MonsterSoundSet
        {
            MonsterType = "eye",
            Idle = new("monster_eye_idle", 0.8f, 0.1f, -8f),          // Faint humming
            Aggro = new("monster_eye_aggro", 0.7f, 0.1f, 2f),         // Psychic screech
            Attack = new("monster_eye_attack", 0.75f, 0.15f, 0f),     // Energy beam
            Hit = new("monster_eye_hit", 1.5f, 0.2f, 0f),             // High squeal
            Die = new("monster_eye_die", 0.5f, 0.1f, 2f),             // Ethereal pop
        };

        // ============================================
        // MUSHROOM - Spore-y, puffing sounds
        // ============================================
        _soundSets["mushroom"] = new MonsterSoundSet
        {
            MonsterType = "mushroom",
            Idle = new("monster_mushroom_idle", 0.9f, 0.15f, -5f),    // Soft breathing
            Aggro = new("monster_mushroom_aggro", 0.8f, 0.1f, 0f),    // Spore puff
            Attack = new("monster_mushroom_attack", 0.85f, 0.15f, 0f), // Poison spray
            Hit = new("monster_mushroom_hit", 1.1f, 0.2f, 0f),        // Squishy thud
            Die = new("monster_mushroom_die", 0.6f, 0.1f, 0f),        // Decomposing
        };

        // ============================================
        // SPIDER - Chittering, skittering sounds
        // ============================================
        _soundSets["spider"] = new MonsterSoundSet
        {
            MonsterType = "spider",
            Idle = new("monster_spider_idle", 1.3f, 0.2f, -6f),       // Leg clicks
            Aggro = new("monster_spider_aggro", 1.2f, 0.15f, 0f),     // Angry hissing
            Attack = new("monster_spider_attack", 1.4f, 0.15f, 0f),   // Fangs snapping
            Hit = new("monster_spider_hit", 1.5f, 0.2f, 0f),          // Chitinous crack
            Die = new("monster_spider_die", 1.0f, 0.1f, 0f),          // Legs curling
        };

        // ============================================
        // LIZARD - Hissing, reptilian sounds
        // ============================================
        _soundSets["lizard"] = new MonsterSoundSet
        {
            MonsterType = "lizard",
            Idle = new("monster_lizard_idle", 0.95f, 0.1f, -4f),      // Tongue flicking
            Aggro = new("monster_lizard_aggro", 0.9f, 0.1f, 0f),      // Threatening hiss
            Attack = new("monster_lizard_attack", 1.0f, 0.15f, 0f),   // Snapping jaws
            Hit = new("monster_lizard_hit", 1.1f, 0.15f, 0f),         // Pained screech
            Die = new("monster_lizard_die", 0.8f, 0.1f, 0f),          // Death rattle
        };

        // ============================================
        // SKELETON - Bony, rattling sounds
        // ============================================
        _soundSets["skeleton"] = new MonsterSoundSet
        {
            MonsterType = "skeleton",
            Idle = new("monster_skeleton_idle", 1.0f, 0.15f, -5f),    // Bone creaking
            Aggro = new("monster_skeleton_aggro", 0.85f, 0.1f, 0f),   // Hollow scream
            Attack = new("monster_skeleton_attack", 1.1f, 0.1f, 0f),  // Sword swing
            Hit = new("monster_skeleton_hit", 1.3f, 0.2f, 0f),        // Bone crack
            Die = new("monster_skeleton_die", 1.2f, 0.15f, 0f),       // Bones falling
        };

        // ============================================
        // WOLF - Growling, snarling sounds
        // ============================================
        _soundSets["wolf"] = new MonsterSoundSet
        {
            MonsterType = "wolf",
            Idle = new("monster_wolf_idle", 0.9f, 0.1f, -5f),         // Soft panting
            Aggro = new("monster_wolf_aggro", 0.85f, 0.1f, 2f),       // Threatening growl
            Attack = new("monster_wolf_attack", 0.9f, 0.15f, 0f),     // Snap and bite
            Hit = new("monster_wolf_hit", 1.0f, 0.15f, 0f),           // Yelp
            Die = new("monster_wolf_die", 0.7f, 0.1f, 0f),            // Whimper
        };

        // ============================================
        // BAT - Squeaky, fluttery sounds
        // ============================================
        _soundSets["bat"] = new MonsterSoundSet
        {
            MonsterType = "bat",
            Idle = new("monster_bat_idle", 1.8f, 0.3f, -8f),          // Ultrasonic chirps
            Aggro = new("monster_bat_aggro", 1.6f, 0.2f, 0f),         // Angry screech
            Attack = new("monster_bat_attack", 1.7f, 0.2f, 0f),       // Dive attack
            Hit = new("monster_bat_hit", 2.0f, 0.3f, 0f),             // High squeak
            Die = new("monster_bat_die", 1.4f, 0.2f, 0f),             // Falling squeak
        };

        // ============================================
        // DRAGON - Deep, powerful sounds
        // ============================================
        _soundSets["dragon"] = new MonsterSoundSet
        {
            MonsterType = "dragon",
            Idle = new("monster_dragon_idle", 0.6f, 0.1f, -3f),       // Deep breathing
            Aggro = new("monster_dragon_aggro", 0.5f, 0.1f, 4f),      // Mighty roar
            Attack = new("monster_dragon_attack", 0.55f, 0.1f, 3f),   // Fire breath
            Hit = new("monster_dragon_hit", 0.7f, 0.15f, 0f),         // Angry snarl
            Die = new("monster_dragon_die", 0.4f, 0.1f, 2f),          // Epic death roar
        };

        // ============================================
        // BOSSES - More powerful versions
        // ============================================
        _soundSets["skeleton_lord"] = new MonsterSoundSet
        {
            MonsterType = "skeleton_lord",
            Idle = new("monster_skeleton_idle", 0.7f, 0.1f, 0f),      // Deep bone rattling
            Aggro = new("monster_skeleton_aggro", 0.6f, 0.1f, 4f),    // Commanding scream
            Attack = new("monster_skeleton_attack", 0.75f, 0.1f, 3f), // Heavy sword swing
            Hit = new("monster_skeleton_hit", 0.9f, 0.15f, 0f),       // Armored crack
            Die = new("monster_skeleton_die", 0.5f, 0.1f, 4f),        // Shattering collapse
        };

        _soundSets["dragon_king"] = new MonsterSoundSet
        {
            MonsterType = "dragon_king",
            Idle = new("monster_dragon_idle", 0.45f, 0.08f, 0f),      // Thunderous breathing
            Aggro = new("monster_dragon_aggro", 0.4f, 0.05f, 6f),     // Earth-shaking roar
            Attack = new("monster_dragon_attack", 0.42f, 0.08f, 5f),  // Inferno blast
            Hit = new("monster_dragon_hit", 0.55f, 0.1f, 2f),         // Enraged roar
            Die = new("monster_dragon_die", 0.35f, 0.05f, 6f),        // Cataclysmic death
        };

        _soundSets["spider_queen"] = new MonsterSoundSet
        {
            MonsterType = "spider_queen",
            Idle = new("monster_spider_idle", 0.9f, 0.1f, 0f),        // Many legs clicking
            Aggro = new("monster_spider_aggro", 0.85f, 0.1f, 4f),     // Terrifying shriek
            Attack = new("monster_spider_attack", 1.0f, 0.1f, 3f),    // Venom spray
            Hit = new("monster_spider_hit", 1.1f, 0.15f, 0f),         // Carapace crack
            Die = new("monster_spider_die", 0.7f, 0.08f, 4f),         // Dramatic collapse
        };

        // ============================================
        // DCC MONSTERS - Dungeon Crawler Carl themed
        // ============================================

        // Crawler Killer - Combat robot, metallic sounds
        _soundSets["crawler_killer"] = new MonsterSoundSet
        {
            MonsterType = "crawler_killer",
            Idle = new("monster_skeleton_idle", 0.7f, 0.1f, -4f),     // Servo whine
            Aggro = new("monster_skeleton_aggro", 0.6f, 0.1f, 2f),    // Target acquired
            Attack = new("monster_skeleton_attack", 0.8f, 0.1f, 2f),  // Blade swing
            Hit = new("monster_skeleton_hit", 1.0f, 0.15f, 2f),       // Metal clang
            Die = new("monster_skeleton_die", 0.5f, 0.1f, 2f),        // Systems failure
        };

        // Shadow Stalker - Ethereal, whispery sounds
        _soundSets["shadow_stalker"] = new MonsterSoundSet
        {
            MonsterType = "shadow_stalker",
            Idle = new("monster_eye_idle", 0.5f, 0.15f, -8f),         // Shadow whispers
            Aggro = new("monster_eye_aggro", 0.4f, 0.1f, 0f),         // Chilling shriek
            Attack = new("monster_eye_attack", 0.45f, 0.1f, 0f),      // Shadow strike
            Hit = new("monster_eye_hit", 0.7f, 0.2f, 0f),             // Ethereal wail
            Die = new("monster_eye_die", 0.3f, 0.1f, 2f),             // Fading into darkness
        };

        // Flesh Golem - Heavy, meaty sounds
        _soundSets["flesh_golem"] = new MonsterSoundSet
        {
            MonsterType = "flesh_golem",
            Idle = new("monster_slime_idle", 0.5f, 0.15f, -3f),       // Wet breathing
            Aggro = new("monster_dragon_aggro", 0.7f, 0.1f, 2f),      // Monstrous roar
            Attack = new("monster_slime_attack", 0.6f, 0.1f, 3f),     // Heavy flesh slam
            Hit = new("monster_slime_hit", 0.8f, 0.2f, 0f),           // Meaty thud
            Die = new("monster_slime_die", 0.5f, 0.1f, 2f),           // Collapsing mass
        };

        // Plague Bearer - Sickly, bubbling sounds
        _soundSets["plague_bearer"] = new MonsterSoundSet
        {
            MonsterType = "plague_bearer",
            Idle = new("monster_mushroom_idle", 0.7f, 0.2f, -5f),     // Diseased breathing
            Aggro = new("monster_mushroom_aggro", 0.6f, 0.1f, 0f),    // Phlegmy growl
            Attack = new("monster_mushroom_attack", 0.65f, 0.15f, 0f), // Plague spray
            Hit = new("monster_slime_hit", 0.9f, 0.2f, 0f),           // Infected splatter
            Die = new("monster_mushroom_die", 0.5f, 0.1f, 2f),        // Disease release
        };

        // Living Armor - Metallic, hollow sounds
        _soundSets["living_armor"] = new MonsterSoundSet
        {
            MonsterType = "living_armor",
            Idle = new("monster_skeleton_idle", 0.6f, 0.1f, -3f),     // Armor creaking
            Aggro = new("monster_skeleton_aggro", 0.55f, 0.1f, 3f),   // Hollow battle cry
            Attack = new("monster_skeleton_attack", 0.7f, 0.1f, 2f),  // Heavy sword swing
            Hit = new("monster_skeleton_hit", 0.8f, 0.15f, 3f),       // Armor denting
            Die = new("monster_skeleton_die", 0.4f, 0.1f, 4f),        // Armor collapsing
        };

        // Camera Drone - Electronic, buzzy sounds
        _soundSets["camera_drone"] = new MonsterSoundSet
        {
            MonsterType = "camera_drone",
            Idle = new("monster_bat_idle", 1.5f, 0.2f, -6f),          // Propeller buzz
            Aggro = new("monster_eye_aggro", 1.3f, 0.15f, 0f),        // Alert beeping
            Attack = new("monster_eye_attack", 1.4f, 0.1f, 0f),       // Camera flash
            Hit = new("monster_skeleton_hit", 1.6f, 0.2f, 0f),        // Electronics sparking
            Die = new("monster_bat_die", 1.8f, 0.2f, 2f),             // System crash
        };

        // Shock Drone - Electric, crackling sounds
        _soundSets["shock_drone"] = new MonsterSoundSet
        {
            MonsterType = "shock_drone",
            Idle = new("monster_eye_idle", 1.2f, 0.15f, -5f),         // Electric hum
            Aggro = new("monster_eye_aggro", 1.1f, 0.1f, 2f),         // Charging up
            Attack = new("monster_eye_attack", 1.0f, 0.1f, 3f),       // Electric discharge
            Hit = new("monster_skeleton_hit", 1.4f, 0.2f, 0f),        // Sparks flying
            Die = new("monster_eye_die", 0.8f, 0.15f, 3f),            // Power failure
        };

        // Advertiser Bot - Annoying, jingly sounds
        _soundSets["advertiser_bot"] = new MonsterSoundSet
        {
            MonsterType = "advertiser_bot",
            Idle = new("monster_goblin_idle", 1.3f, 0.2f, -3f),       // Jingle playing
            Aggro = new("monster_goblin_aggro", 1.4f, 0.1f, 2f),      // BUY NOW announcement
            Attack = new("monster_eye_attack", 1.2f, 0.15f, 0f),      // Ad bombardment
            Hit = new("monster_skeleton_hit", 1.5f, 0.2f, 0f),        // Screen crack
            Die = new("monster_goblin_die", 1.1f, 0.15f, 2f),         // Error message
        };

        // Clockwork Spider - Mechanical clicking
        _soundSets["clockwork_spider"] = new MonsterSoundSet
        {
            MonsterType = "clockwork_spider",
            Idle = new("monster_spider_idle", 1.5f, 0.15f, -4f),      // Gears ticking
            Aggro = new("monster_spider_aggro", 1.4f, 0.1f, 0f),      // Gears accelerating
            Attack = new("monster_spider_attack", 1.6f, 0.1f, 0f),    // Metal fangs
            Hit = new("monster_skeleton_hit", 1.3f, 0.2f, 0f),        // Cog cracking
            Die = new("monster_spider_die", 1.2f, 0.1f, 2f),          // Spring release
        };

        // Lava Elemental - Fiery, rumbling sounds
        _soundSets["lava_elemental"] = new MonsterSoundSet
        {
            MonsterType = "lava_elemental",
            Idle = new("monster_dragon_idle", 0.7f, 0.1f, -3f),       // Molten bubbling
            Aggro = new("monster_dragon_aggro", 0.65f, 0.1f, 3f),     // Volcanic roar
            Attack = new("monster_dragon_attack", 0.7f, 0.1f, 3f),    // Lava eruption
            Hit = new("monster_slime_hit", 0.6f, 0.15f, 2f),          // Cooling crack
            Die = new("monster_dragon_die", 0.5f, 0.1f, 4f),          // Cooling solidify
        };

        // Ice Wraith - Cold, ethereal sounds
        _soundSets["ice_wraith"] = new MonsterSoundSet
        {
            MonsterType = "ice_wraith",
            Idle = new("monster_eye_idle", 0.6f, 0.1f, -6f),          // Freezing wind
            Aggro = new("monster_eye_aggro", 0.55f, 0.1f, 2f),        // Chilling scream
            Attack = new("monster_eye_attack", 0.6f, 0.1f, 0f),       // Ice blast
            Hit = new("monster_skeleton_hit", 1.8f, 0.2f, 2f),        // Ice cracking
            Die = new("monster_eye_die", 0.4f, 0.1f, 3f),             // Shattering ice
        };

        // Gelatinous Cube - Wet, sloshing sounds
        _soundSets["gelatinous_cube"] = new MonsterSoundSet
        {
            MonsterType = "gelatinous_cube",
            Idle = new("monster_slime_idle", 0.6f, 0.2f, -4f),        // Digesting
            Aggro = new("monster_slime_aggro", 0.5f, 0.15f, 0f),      // Hungry slosh
            Attack = new("monster_slime_attack", 0.55f, 0.2f, 2f),    // Engulfing
            Hit = new("monster_slime_hit", 0.7f, 0.25f, 0f),          // Acidic splash
            Die = new("monster_slime_die", 0.4f, 0.1f, 2f),           // Dissolving
        };

        // Void Spawn - Otherworldly, distorted sounds
        _soundSets["void_spawn"] = new MonsterSoundSet
        {
            MonsterType = "void_spawn",
            Idle = new("monster_eye_idle", 0.4f, 0.2f, -6f),          // Reality warping
            Aggro = new("monster_eye_aggro", 0.35f, 0.15f, 2f),       // Void screech
            Attack = new("monster_eye_attack", 0.4f, 0.1f, 2f),       // Reality tear
            Hit = new("monster_eye_hit", 0.5f, 0.2f, 0f),             // Dimensional shift
            Die = new("monster_eye_die", 0.25f, 0.1f, 4f),            // Void collapse
        };

        // Mimic - Deceptive, surprising sounds
        _soundSets["mimic"] = new MonsterSoundSet
        {
            MonsterType = "mimic",
            Idle = new("monster_mushroom_idle", 0.1f, 0.05f, -20f),   // Almost silent
            Aggro = new("monster_dragon_aggro", 1.0f, 0.1f, 4f),      // Shocking reveal
            Attack = new("monster_wolf_attack", 0.8f, 0.15f, 2f),     // Vicious bite
            Hit = new("monster_slime_hit", 1.0f, 0.2f, 0f),           // Wooden thud
            Die = new("monster_mushroom_die", 0.8f, 0.1f, 0f),        // Chest collapsing
        };

        // Dungeon Rat - Squeaky, skittering sounds
        _soundSets["dungeon_rat"] = new MonsterSoundSet
        {
            MonsterType = "dungeon_rat",
            Idle = new("monster_bat_idle", 1.4f, 0.25f, -8f),         // Soft squeaking
            Aggro = new("monster_bat_aggro", 1.5f, 0.2f, -2f),        // Aggressive squeal
            Attack = new("monster_bat_attack", 1.6f, 0.2f, 0f),       // Biting
            Hit = new("monster_bat_hit", 1.8f, 0.25f, 0f),            // Pain squeak
            Die = new("monster_bat_die", 1.3f, 0.15f, 0f),            // Death squeak
        };

        // ============================================
        // DCC BOSSES
        // ============================================

        // The Butcher - Heavy, brutal sounds
        _soundSets["the_butcher"] = new MonsterSoundSet
        {
            MonsterType = "the_butcher",
            Idle = new("monster_dragon_idle", 0.55f, 0.1f, -2f),      // Heavy breathing
            Aggro = new("monster_dragon_aggro", 0.5f, 0.08f, 5f),     // FRESH MEAT!
            Attack = new("monster_dragon_attack", 0.6f, 0.1f, 4f),    // Cleaver swing
            Hit = new("monster_slime_hit", 0.65f, 0.15f, 2f),         // Meaty impact
            Die = new("monster_dragon_die", 0.45f, 0.08f, 5f),        // Butcher falls
        };

        // Mordecai the Traitor - Cunning, metallic sounds
        _soundSets["mordecai_the_traitor"] = new MonsterSoundSet
        {
            MonsterType = "mordecai_the_traitor",
            Idle = new("monster_skeleton_idle", 0.65f, 0.1f, -3f),    // Armor shifting
            Aggro = new("monster_goblin_aggro", 0.7f, 0.1f, 3f),      // Traitorous laugh
            Attack = new("monster_skeleton_attack", 0.75f, 0.1f, 3f), // Dual blade strike
            Hit = new("monster_skeleton_hit", 0.85f, 0.15f, 0f),      // Armor clang
            Die = new("monster_goblin_die", 0.6f, 0.1f, 4f),          // Dying gasp
        };

        // Princess Donut - Cat sounds (high pitched)
        _soundSets["princess_donut"] = new MonsterSoundSet
        {
            MonsterType = "princess_donut",
            Idle = new("monster_bat_idle", 1.3f, 0.2f, -4f),          // Purring
            Aggro = new("monster_wolf_aggro", 1.5f, 0.15f, 2f),       // Angry meow
            Attack = new("monster_wolf_attack", 1.4f, 0.15f, 0f),     // Claw swipe
            Hit = new("monster_bat_hit", 1.6f, 0.2f, 0f),             // Hurt yowl
            Die = new("monster_wolf_die", 1.2f, 0.1f, 3f),            // Dramatic fall (she has 9 lives)
        };

        // Mongo the Destroyer - Massive, thunderous sounds
        _soundSets["mongo_the_destroyer"] = new MonsterSoundSet
        {
            MonsterType = "mongo_the_destroyer",
            Idle = new("monster_dragon_idle", 0.35f, 0.08f, 0f),      // Massive breathing
            Aggro = new("monster_dragon_aggro", 0.3f, 0.05f, 7f),     // Earth-shaking roar
            Attack = new("monster_dragon_attack", 0.35f, 0.08f, 6f),  // Crushing stomp
            Hit = new("monster_dragon_hit", 0.4f, 0.1f, 3f),          // Enraged bellow
            Die = new("monster_dragon_die", 0.25f, 0.05f, 8f),        // Titan falls
        };

        // Zev the Loot Goblin - Quick, greedy sounds
        _soundSets["zev_the_loot_goblin"] = new MonsterSoundSet
        {
            MonsterType = "zev_the_loot_goblin",
            Idle = new("monster_goblin_idle", 1.1f, 0.15f, -3f),      // Counting coins
            Aggro = new("monster_goblin_aggro", 1.2f, 0.1f, 2f),      // Mine! All mine!
            Attack = new("monster_goblin_attack", 1.3f, 0.1f, 0f),    // Quick stab
            Hit = new("monster_goblin_hit", 1.4f, 0.2f, 0f),          // Greedy squeal
            Die = new("monster_goblin_die", 1.0f, 0.1f, 3f),          // My precious...
        };
    }

    /// <summary>
    /// Get the sound set for a monster type
    /// </summary>
    public static MonsterSoundSet? GetSoundSet(string monsterType)
    {
        var key = monsterType.ToLower().Replace(" ", "_");
        return _soundSets.TryGetValue(key, out var set) ? set : null;
    }

    /// <summary>
    /// Get all available monster types that have sound definitions
    /// </summary>
    public static IEnumerable<string> GetAllMonsterTypes()
    {
        return _soundSets.Keys;
    }

    /// <summary>
    /// Get a specific sound config for a monster and action
    /// </summary>
    public static SoundConfig? GetSoundConfig(string monsterType, string action)
    {
        var set = GetSoundSet(monsterType);
        if (set == null) return null;

        return action.ToLower() switch
        {
            "idle" => set.Idle,
            "aggro" => set.Aggro,
            "attack" => set.Attack,
            "hit" => set.Hit,
            "die" => set.Die,
            _ => null
        };
    }
}
