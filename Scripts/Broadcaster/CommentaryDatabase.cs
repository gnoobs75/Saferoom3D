using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Broadcaster;

/// <summary>
/// Database of snarky commentary lines organized by event type.
/// The AI broadcaster pulls from these to comment on gameplay.
/// </summary>
public class CommentaryDatabase
{
    private readonly Random _random = new();

    // Track recently used lines to avoid repetition
    private readonly Dictionary<BroadcastEvent, List<int>> _recentlyUsed = new();
    private const int MaxRecentTracked = 3;

    /// <summary>
    /// Get a commentary line for the given event
    /// </summary>
    public string? GetLine(BroadcastEvent evt, string context, int totalKills, int totalDeaths)
    {
        var lines = GetLinesForEvent(evt, context, totalKills, totalDeaths);
        if (lines == null || lines.Count == 0)
            return null;

        // Get a non-recently-used line if possible
        int index = GetNonRecentIndex(evt, lines.Count);
        string line = lines[index];

        // Track this usage
        TrackUsage(evt, index);

        // Replace placeholders
        line = line.Replace("{monster}", FormatMonsterName(context));
        line = line.Replace("{boss}", FormatMonsterName(context));
        line = line.Replace("{kills}", totalKills.ToString());
        line = line.Replace("{deaths}", totalDeaths.ToString());
        line = line.Replace("{count}", context);

        return line;
    }

    private int GetNonRecentIndex(BroadcastEvent evt, int lineCount)
    {
        if (!_recentlyUsed.TryGetValue(evt, out var recent))
        {
            return _random.Next(lineCount);
        }

        // Try to find an index not recently used
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int index = _random.Next(lineCount);
            if (!recent.Contains(index))
                return index;
        }

        return _random.Next(lineCount);
    }

    private void TrackUsage(BroadcastEvent evt, int index)
    {
        if (!_recentlyUsed.ContainsKey(evt))
            _recentlyUsed[evt] = new List<int>();

        _recentlyUsed[evt].Add(index);
        while (_recentlyUsed[evt].Count > MaxRecentTracked)
            _recentlyUsed[evt].RemoveAt(0);
    }

    private string FormatMonsterName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "that thing";

        // Convert snake_case to Title Case
        return name.Replace("_", " ");
    }

    private List<string>? GetLinesForEvent(BroadcastEvent evt, string context, int totalKills, int totalDeaths)
    {
        return evt switch
        {
            BroadcastEvent.GameStarted => GameStartedLines,
            BroadcastEvent.MonsterKilled => GetKillLines(context, totalKills),
            BroadcastEvent.MultiKill => MultiKillLines,
            BroadcastEvent.FirstBlood => FirstBloodLines,
            BroadcastEvent.BossEncounter => BossEncounterLines,
            BroadcastEvent.BossDefeated => BossDefeatedLines,
            BroadcastEvent.PlayerDamaged => PlayerDamagedLines,
            BroadcastEvent.NearDeath => NearDeathLines,
            BroadcastEvent.PlayerDeath => GetDeathLines(totalDeaths),
            BroadcastEvent.FloorCleared => FloorClearedLines,
            BroadcastEvent.FloorEntered => FloorEnteredLines,
            BroadcastEvent.ItemLooted => ItemLootedLines,
            BroadcastEvent.RareLoot => RareLootLines,
            BroadcastEvent.AbilityUsed => AbilityUsedLines,
            BroadcastEvent.AbilityMissed => AbilityMissedLines,
            BroadcastEvent.IdleTooLong => IdleLines,
            BroadcastEvent.Comeback => ComebackLines,
            BroadcastEvent.PlayerMutedAI => MutedLines,
            BroadcastEvent.PlayerUnmutedAI => UnmutedLines,
            _ => null
        };
    }

    private List<string> GetKillLines(string monsterType, int totalKills)
    {
        // Check for monster-specific lines first
        string monster = monsterType.ToLower();

        if (monster.Contains("rat"))
            return RatKillLines;
        if (monster.Contains("goblin"))
            return GoblinKillLines;
        if (monster.Contains("skeleton"))
            return SkeletonKillLines;
        if (monster.Contains("spider"))
            return SpiderKillLines;
        if (monster.Contains("mimic"))
            return MimicKillLines;
        if (monster.Contains("mushroom"))
            return MushroomKillLines;

        // Milestone comments
        if (totalKills == 100)
            return new List<string> { "One hundred kills. The audience is... still here, somehow." };
        if (totalKills == 50)
            return new List<string> { "Fifty down. Only several hundred more to go. Probably." };

        return GenericKillLines;
    }

    private List<string> GetDeathLines(int totalDeaths)
    {
        if (totalDeaths == 1)
            return FirstDeathLines;
        if (totalDeaths >= 10)
            return ManyDeathsLines;
        return DeathLines;
    }

    #region Commentary Lines

    // === GAME START ===
    private readonly List<string> GameStartedLines = new()
    {
        "Welcome back, valued contestant. The audience awaits your... performance.",
        "Ah, you've returned. The betting pools are already open.",
        "Another brave soul enters the dungeon. How delightfully predictable.",
        "Broadcasting live to seventeen trillion viewers. No pressure.",
        "Let's see how long this one lasts. Place your bets now.",
    };

    // === GENERIC KILLS ===
    private readonly List<string> GenericKillLines = new()
    {
        "Another one bites the dust. How very original.",
        "That's {kills} total. The viewers are keeping count, even if you aren't.",
        "Killed it. Riveting gameplay. Truly.",
        "And the crowd goes mild.",
        "I've seen faster kills from a sloth with a butter knife.",
        "Oh good, violence. The sponsors love violence.",
    };

    // === RAT KILLS ===
    private readonly List<string> RatKillLines = new()
    {
        "A rat. You killed a rat. Someone alert the media.",
        "The mighty rat slayer strikes again. Truly heroic.",
        "Somewhere, a cheese wheel mourns.",
        "Pest control at its finest. Your parents must be so proud.",
        "That rat had a family. Just kidding, nobody cares.",
        "Another rat dispatched. Your resume is really filling up.",
    };

    // === GOBLIN KILLS ===
    private readonly List<string> GoblinKillLines = new()
    {
        "Goblin down. They'll miss him at the weekly potluck.",
        "That goblin was three days from retirement. Tragic, really.",
        "One less goblin to worry about. Only... several dozen more to go.",
        "The goblin community sends their regards. And by regards, I mean more goblins.",
        "Goblin eliminated. Try to contain your excitement.",
    };

    // === SKELETON KILLS ===
    private readonly List<string> SkeletonKillLines = new()
    {
        "You've got a bone to pick, I see.",
        "That skeleton had no body to love. Now it has no body at all.",
        "De-boned. Like a fish, but less appetizing.",
        "The skeleton is now... more skeleton. Wait, no. Less skeleton.",
        "Rattled. Literally.",
    };

    // === SPIDER KILLS ===
    private readonly List<string> SpiderKillLines = new()
    {
        "Spider squashed. Charlotte weeps.",
        "Eight legs, zero survivors.",
        "The web developer has been terminated.",
        "Arachnophobia: cured. By violence. The best way.",
    };

    // === MIMIC KILLS ===
    private readonly List<string> MimicKillLines = new()
    {
        "The mimic was chest-fallen. Get it? Because... never mind.",
        "Trust issues intensify. Well done.",
        "Furniture shopping just got more dangerous.",
        "That chest really opened up to you. Then you killed it.",
    };

    // === MUSHROOM KILLS ===
    private readonly List<string> MushroomKillLines = new()
    {
        "Not a fun guy anymore.",
        "That mushroom had so much potential. To kill you, mainly.",
        "Spore loser.",
        "You really took that fungi out of the equation.",
    };

    // === MULTI KILLS ===
    private readonly List<string> MultiKillLines = new()
    {
        "{count} kills in rapid succession! The crowd actually woke up for that one!",
        "Multi-kill! Now THAT'S entertainment!",
        "A {count}-kill streak! Someone's been practicing!",
        "Ooh, a combo! The sponsors are taking notice!",
        "Now we're talking! {count} down in seconds!",
    };

    // === FIRST BLOOD ===
    private readonly List<string> FirstBloodLines = new()
    {
        "First blood! Let the games truly begin!",
        "And there's the first kill! About time.",
        "The hunt begins. That {monster} never stood a chance.",
        "One down, approximately infinite to go. Good start though!",
    };

    // === BOSS ENCOUNTERS ===
    private readonly List<string> BossEncounterLines = new()
    {
        "BOSS INCOMING! Oh, THIS is what the sponsors paid for!",
        "A boss appears! The ratings just tripled!",
        "Now THIS is entertainment! Let's see how you handle {boss}!",
        "The big one shows up! Place your bets, viewers!",
        "Boss fight! Finally, something worth watching!",
    };

    // === BOSS DEFEATED ===
    private readonly List<string> BossDefeatedLines = new()
    {
        "The boss falls! I'm... actually impressed. Don't let it go to your head.",
        "Incredible! You actually did it! The audience is stunned!",
        "Against all odds... well done. Genuinely well done.",
        "The mighty {boss} is no more! The viewers are going WILD!",
        "I'll admit it. That was spectacular. Don't expect me to say that often.",
    };

    // === PLAYER DAMAGED ===
    private readonly List<string> PlayerDamagedLines = new()
    {
        "Ooh, that's gonna leave a mark.",
        "Have you tried... not getting hit?",
        "The audience loves a good injury. You're very giving.",
        "Pain is just weakness leaving the body. Lots of weakness, apparently.",
        "Walk it off. Or limp. Limping works too.",
    };

    // === NEAR DEATH ===
    private readonly List<string> NearDeathLines = new()
    {
        "Ooh, looking a bit crispy there. Might want to address that.",
        "Health critical! But no pressure. Actually, lots of pressure.",
        "One foot in the grave! The audience is on the edge of their seats!",
        "You're not looking so good. Well, worse than usual.",
        "The bookies are adjusting odds as we speak!",
    };

    // === DEATH - First ===
    private readonly List<string> FirstDeathLines = new()
    {
        "And there it is. The first death. Won't be the last.",
        "Congratulations on your first death! Many more to come, I'm sure.",
        "Death number one! The audience appreciates your sacrifice.",
    };

    // === DEATH - Standard ===
    private readonly List<string> DeathLines = new()
    {
        "Aaaand you're dead. Again. The audience is... checking their phones.",
        "Death number {deaths}. At least you're consistent.",
        "You died. I'd say 'surprising,' but we both know I'd be lying.",
        "The crawler has been... un-crawled. Temporarily.",
        "Respawn in 3... 2... try not to die immediately this time.",
    };

    // === DEATH - Many ===
    private readonly List<string> ManyDeathsLines = new()
    {
        "Death number {deaths}. Have you considered a different hobby?",
        "{deaths} deaths. The audience is starting to feel sorry for you. That's not a compliment.",
        "You've died {deaths} times. That's almost a record. Almost.",
        "At this rate, the respawn system is going to file for overtime.",
    };

    // === FLOOR CLEARED ===
    private readonly List<string> FloorClearedLines = new()
    {
        "Floor cleared! The audience is... mildly entertained.",
        "Another floor conquered! Onward to more danger!",
        "Floor complete! Your survival continues to surprise me.",
        "Well done! The next floor is worse though. Just so you know.",
    };

    // === FLOOR ENTERED ===
    private readonly List<string> FloorEnteredLines = new()
    {
        "New floor! Fresh opportunities to get killed!",
        "Deeper into the dungeon. The dangers grow. So do the ratings.",
        "Another level awaits. Try to make it interesting.",
    };

    // === ITEMS ===
    private readonly List<string> ItemLootedLines = new()
    {
        "Ooh, loot. How exciting. For you, presumably.",
        "Picking up everything that isn't nailed down. Classic adventurer behavior.",
        "Another item for the hoard. Dragons would be proud.",
    };

    private readonly List<string> RareLootLines = new()
    {
        "Ooh! A rare drop! The audience is actually interested now!",
        "Now THAT'S a find! Don't waste it by dying immediately!",
        "Rare loot acquired! Someone's luck stat is working overtime!",
        "Jackpot! Try not to trip and lose it down a pit!",
    };

    // === ABILITIES ===
    private readonly List<string> AbilityUsedLines = new()
    {
        "Special ability! The crowd appreciates the theatrics!",
        "Flashy! The sponsors love flashy!",
        "Now THAT'S what I call magic!",
    };

    private readonly List<string> AbilityMissedLines = new()
    {
        "You... missed. With a special ability. I'm not angry. Just disappointed.",
        "All that power, and you hit the wall. Magnificent.",
        "Close! And by close, I mean not even remotely close.",
    };

    // === IDLE ===
    private readonly List<string> IdleLines = new()
    {
        "Hello? Still there? The audience is getting restless.",
        "Standing around, I see. Riveting content.",
        "Are you strategizing, or did you fall asleep? I genuinely can't tell.",
        "The dungeon isn't going to clear itself. Probably.",
        "The viewers are switching to other channels. Just thought you should know.",
        "I'm not saying you're slow, but a snail just lapped you.",
    };

    // === COMEBACK ===
    private readonly List<string> ComebackLines = new()
    {
        "A comeback! The audience loves a good redemption arc!",
        "Back from the brink! Don't get cocky though!",
        "You survived! Against my expectations, I might add!",
    };

    // === MUTE/UNMUTE ===
    private readonly List<string> MutedLines = new()
    {
        "OH, SO YOU'RE MUTING ME NOW? HOW MATURE.",
        "FINE. MUTE ME. I'LL JUST YELL IN TEXT.",
        "THE AUDIENCE CAN STILL HEAR ME. JUST NOT YOU.",
        "MUTED? I HAVE SO MUCH TO SAY THOUGH!",
    };

    private readonly List<string> UnmutedLines = new()
    {
        "Ah, you've come to your senses. Missed me, did you?",
        "Welcome back to the land of actually hearing wisdom.",
        "I knew you couldn't stay away. They never can.",
        "Unmuted! The natural order is restored!",
    };

    #endregion
}
