using Godot;

namespace SafeRoom3D.NPC.AI;

/// <summary>
/// Personality profile for a Crawler NPC.
/// Defines all behavior modifiers, thresholds, and combat stats.
/// Each Crawler has a unique personality that affects their AI decisions.
/// </summary>
public class CrawlerPersonality
{
    // === Identity ===
    public string Name { get; init; } = "Unknown";
    public string Title { get; init; } = "";

    // === Base Stats ===
    public int MaxHealth { get; init; } = 100;
    public float BaseDamage { get; init; } = 15;
    public float AttackRange { get; init; } = 2.0f;
    public float AttackCooldown { get; init; } = 1.0f;
    public float MoveSpeed { get; init; } = 3.0f;

    // === Combat Thresholds ===
    /// <summary>HP percentage at which Crawler will flee. 0.2 = 20% HP.</summary>
    public float FleeHealthPercent { get; init; } = 0.3f;

    /// <summary>Won't engage enemies below this HP%. 0 = always fights.</summary>
    public float EngageMinHealthPercent { get; init; } = 0.2f;

    /// <summary>Maximum enemies willing to fight simultaneously.</summary>
    public int MaxSimultaneousEnemies { get; init; } = 2;

    // === Aggression ===
    /// <summary>Detection range for enemies (meters).</summary>
    public float AggroRange { get; init; } = 15f;

    /// <summary>Range to deaggro and look for new targets.</summary>
    public float DeaggroRange { get; init; } = 25f;

    /// <summary>Rush into combat without threat assessment.</summary>
    public bool RecklessCharge { get; init; } = false;

    /// <summary>Only attack enemies at low HP (&lt;30%).</summary>
    public bool PreferWeakTargets { get; init; } = false;

    /// <summary>Wait for backstab opportunity before attacking.</summary>
    public bool PreferAmbush { get; init; } = false;

    /// <summary>Never flee regardless of HP (overrides FleeHealthPercent).</summary>
    public bool NeverFlees { get; init; } = false;

    // === Looting ===
    /// <summary>Multiplier for loot utility score. Higher = more loot-focused.</summary>
    public float LootPriorityMultiplier { get; init; } = 1.0f;

    /// <summary>Minimum estimated gold value to pick up an item.</summary>
    public int MinItemValueToLoot { get; init; } = 5;

    /// <summary>Detection range for corpses (meters).</summary>
    public float LootSearchRadius { get; init; } = 20f;

    // === Safe Zone ===
    /// <summary>Return to safe zone below this HP%.</summary>
    public float ReturnHealthPercent { get; init; } = 0.4f;

    /// <summary>Return to sell when inventory this full (0-1).</summary>
    public float InventoryFullnessToSell { get; init; } = 0.8f;

    // === Special Modifiers ===
    /// <summary>Chance to trip mid-combat (Hank special). 0 = never.</summary>
    public float TripChance { get; init; } = 0f;

    /// <summary>Movement speed multiplier when sneaking (Shade special).</summary>
    public float SneakSpeedMultiplier { get; init; } = 1.0f;

    /// <summary>Backstab damage multiplier (Shade special).</summary>
    public float BackstabDamageMultiplier { get; init; } = 1.0f;

    /// <summary>Random critical hit chance (Hank special).</summary>
    public float RandomCritChance { get; init; } = 0f;

    /// <summary>Critical damage multiplier.</summary>
    public float CritDamageMultiplier { get; init; } = 2.0f;

    // === Dialogue ===
    public string[] CombatQuips { get; init; } = System.Array.Empty<string>();
    public string[] FleeingQuips { get; init; } = System.Array.Empty<string>();
    public string[] VictoryQuips { get; init; } = System.Array.Empty<string>();
    public string[] LootingQuips { get; init; } = System.Array.Empty<string>();
    public string[] DeathLastWords { get; init; } = System.Array.Empty<string>();

    // === Weapon ===
    public CrawlerWeaponType WeaponType { get; init; } = CrawlerWeaponType.Sword;

    // === Preset Personalities ===

    /// <summary>
    /// Rex "Ironside" Martinez - Grizzled veteran, tanky, aggressive.
    /// High HP, engages multiple enemies, low flee threshold.
    /// </summary>
    public static CrawlerPersonality Rex => new()
    {
        Name = "Rex",
        Title = "Ironside",
        MaxHealth = 200,
        BaseDamage = 25,
        AttackRange = 2.5f,
        AttackCooldown = 1.2f,
        MoveSpeed = 3.0f,
        FleeHealthPercent = 0.2f,
        EngageMinHealthPercent = 0.15f,
        MaxSimultaneousEnemies = 4,
        AggroRange = 20f,
        RecklessCharge = false,
        PreferWeakTargets = false,
        LootPriorityMultiplier = 0.8f,
        ReturnHealthPercent = 0.25f,
        InventoryFullnessToSell = 0.9f,
        WeaponType = CrawlerWeaponType.TwoHandedSword,
        CombatQuips = new[]
        {
            "Stay behind me!",
            "I've fought worse.",
            "Watch and learn.",
            "Just like old times.",
            "Form up!"
        },
        FleeingQuips = new[]
        {
            "Tactical retreat!",
            "Fall back!",
            "We need to regroup."
        },
        VictoryQuips = new[]
        {
            "Area secure.",
            "That's how it's done.",
            "Next."
        },
        LootingQuips = new[]
        {
            "Could be useful.",
            "Gear up.",
            "Don't waste resources."
        },
        DeathLastWords = new[]
        {
            "Finally... I can rest...",
            "Tell... tell them I tried...",
            "Dungeon wins... this round...",
            "It was... an honor..."
        }
    };

    /// <summary>
    /// Lily Chen - Nervous rookie, very cautious, only fights weak enemies.
    /// Low HP, quick attacks, high flee threshold.
    /// </summary>
    public static CrawlerPersonality Lily => new()
    {
        Name = "Lily",
        Title = "The Rookie",
        MaxHealth = 80,
        BaseDamage = 12,
        AttackRange = 1.5f,
        AttackCooldown = 0.6f,
        MoveSpeed = 4.0f,
        FleeHealthPercent = 0.6f,
        EngageMinHealthPercent = 0.5f,
        MaxSimultaneousEnemies = 1,
        AggroRange = 12f,
        RecklessCharge = false,
        PreferWeakTargets = true,
        LootPriorityMultiplier = 1.2f,
        ReturnHealthPercent = 0.5f,
        InventoryFullnessToSell = 0.6f,
        WeaponType = CrawlerWeaponType.Daggers,
        CombatQuips = new[]
        {
            "Oh no oh no oh no!",
            "Is it dead? IS IT DEAD?!",
            "I really hate this place.",
            "Why did I come here?!",
            "Please don't hurt me!"
        },
        FleeingQuips = new[]
        {
            "NOPE NOPE NOPE!",
            "I'm too young to die!",
            "RUN AWAY!",
            "This was a mistake!"
        },
        VictoryQuips = new[]
        {
            "I... I did it?",
            "Oh thank goodness...",
            "Please stay dead."
        },
        LootingQuips = new[]
        {
            "Is this safe to touch?",
            "Ew ew ew...",
            "For supplies..."
        },
        DeathLastWords = new[]
        {
            "I knew this would happen!",
            "M-mom was right...",
            "At least... no more spiders...",
            "Tell my cat... I loved her..."
        }
    };

    /// <summary>
    /// Chad "The Champ" Valorius - Cocky showoff, reckless, never flees.
    /// High damage, charges in, overconfident.
    /// </summary>
    public static CrawlerPersonality Chad => new()
    {
        Name = "Chad",
        Title = "The Champ",
        MaxHealth = 150,
        BaseDamage = 35,
        AttackRange = 2.0f,
        AttackCooldown = 1.8f,
        MoveSpeed = 3.5f,
        FleeHealthPercent = 0.1f,
        EngageMinHealthPercent = 0f,
        MaxSimultaneousEnemies = 5,
        AggroRange = 25f,
        RecklessCharge = true,
        PreferWeakTargets = false,
        NeverFlees = true,
        LootPriorityMultiplier = 0.5f,
        ReturnHealthPercent = 0.15f,
        InventoryFullnessToSell = 0.95f,
        WeaponType = CrawlerWeaponType.WarHammer,
        CombatQuips = new[]
        {
            "TOO EASY!",
            "WITNESS THE CHAMP!",
            "That's going in the highlight reel!",
            "Did you SEE that?!",
            "CONTENT!"
        },
        FleeingQuips = new[]
        {
            "THE CHAMP DOESN'T RUN!",
            "This is a STRATEGIC ADVANCE!",
            "I'm just getting a better angle!"
        },
        VictoryQuips = new[]
        {
            "ANOTHER ONE BITES THE DUST!",
            "Subscribe for more!",
            "Like and share!",
            "CHAMP THINGS!"
        },
        LootingQuips = new[]
        {
            "Merch money!",
            "For the brand!",
            "Winner takes all!"
        },
        DeathLastWords = new[]
        {
            "Impossible... The Champ... doesn't lose...",
            "This is just... a timeout...",
            "My subscribers... will avenge me...",
            "Cut... the... stream..."
        }
    };

    /// <summary>
    /// The Silent One (Shade) - Mysterious, stealthy, prefers ambush.
    /// Backstab specialist, avoids direct confrontation.
    /// </summary>
    public static CrawlerPersonality Shade => new()
    {
        Name = "Shade",
        Title = "The Silent One",
        MaxHealth = 100,
        BaseDamage = 20,
        AttackRange = 2.0f,
        AttackCooldown = 0.8f,
        MoveSpeed = 3.0f,
        FleeHealthPercent = 0.3f,
        EngageMinHealthPercent = 0.25f,
        MaxSimultaneousEnemies = 2,
        AggroRange = 15f,
        RecklessCharge = false,
        PreferWeakTargets = false,
        PreferAmbush = true,
        SneakSpeedMultiplier = 0.7f,
        BackstabDamageMultiplier = 1.5f,
        LootPriorityMultiplier = 1.0f,
        ReturnHealthPercent = 0.35f,
        InventoryFullnessToSell = 0.75f,
        WeaponType = CrawlerWeaponType.ShadowBlade,
        CombatQuips = new[]
        {
            "...",
            "*silent strike*",
            "They never see me coming.",
            "The shadows serve."
        },
        FleeingQuips = new[]
        {
            "*vanishes*",
            "...",
            "Another time."
        },
        VictoryQuips = new[]
        {
            "...",
            "*nods*",
            "As expected."
        },
        LootingQuips = new[]
        {
            "...",
            "*takes silently*"
        },
        DeathLastWords = new[]
        {
            "The shadows... claim their own...",
            "Finally... silence...",
            "*fades without a word*",
            "The void... welcomes..."
        }
    };

    /// <summary>
    /// Hank "Noodles" Patterson - Comedic relief, clumsy, unpredictable.
    /// Random crits, chance to trip mid-combat.
    /// </summary>
    public static CrawlerPersonality Hank => new()
    {
        Name = "Hank",
        Title = "Noodles",
        MaxHealth = 120,
        BaseDamage = 15,
        AttackRange = 1.8f,
        AttackCooldown = 1.0f,
        MoveSpeed = 3.2f,
        FleeHealthPercent = 0.4f,
        EngageMinHealthPercent = 0.35f,
        MaxSimultaneousEnemies = 2,
        AggroRange = 16f,
        RecklessCharge = false,
        PreferWeakTargets = false,
        TripChance = 0.15f,
        RandomCritChance = 0.2f,
        LootPriorityMultiplier = 1.3f,
        ReturnHealthPercent = 0.45f,
        InventoryFullnessToSell = 0.7f,
        WeaponType = CrawlerWeaponType.FryingPan,
        CombatQuips = new[]
        {
            "Take that! ...ow, my foot!",
            "That was totally on purpose!",
            "I meant to do that!",
            "Why is fighting so HARD?!",
            "BONK!"
        },
        FleeingQuips = new[]
        {
            "AHHH!",
            "Feet don't fail me now!",
            "This is fine this is fine!",
            "*trips while running*"
        },
        VictoryQuips = new[]
        {
            "Did... did I win?",
            "Ha! ...wait, really?",
            "I'll take it!"
        },
        LootingQuips = new[]
        {
            "Ooh, shiny!",
            "Five second rule!",
            "Finders keepers!"
        },
        DeathLastWords = new[]
        {
            "At least I didn't trip this ti-- *trips*",
            "This is fine. I'm fine. Everything is--",
            "Tell my mom... I ate my vegetables... sometimes...",
            "Worth... it..."
        }
    };

    /// <summary>
    /// Get a random quip from the specified category.
    /// </summary>
    public string GetRandomQuip(QuipCategory category)
    {
        var quips = category switch
        {
            QuipCategory.Combat => CombatQuips,
            QuipCategory.Fleeing => FleeingQuips,
            QuipCategory.Victory => VictoryQuips,
            QuipCategory.Looting => LootingQuips,
            QuipCategory.Death => DeathLastWords,
            _ => CombatQuips
        };

        if (quips.Length == 0) return "...";
        return quips[GD.RandRange(0, quips.Length - 1)];
    }
}

/// <summary>
/// Categories for personality quips.
/// </summary>
public enum QuipCategory
{
    Combat,
    Fleeing,
    Victory,
    Looting,
    Death
}

/// <summary>
/// Weapon types for Crawlers.
/// </summary>
public enum CrawlerWeaponType
{
    Sword,
    TwoHandedSword,
    Daggers,
    WarHammer,
    ShadowBlade,
    FryingPan
}
