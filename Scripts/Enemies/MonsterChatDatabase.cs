using System;
using System.Collections.Generic;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Database of comedic dialogue lines for monster social interactions.
/// Monsters will occasionally stop to chat with each other when nearby.
/// </summary>
public static class MonsterChatDatabase
{
    // Global chat cooldown (30-60 seconds between any chat in the dungeon)
    private static float _globalChatCooldown = 0f;
    private static readonly Random _random = new();

    public const float MinChatInterval = 30f;
    public const float MaxChatInterval = 60f;
    public const float ChatRange = 6f;
    public const float ChatDuration = 4f;

    /// <summary>
    /// Check if a new chat can be initiated (global cooldown).
    /// </summary>
    public static bool CanInitiateChat()
    {
        return _globalChatCooldown <= 0;
    }

    /// <summary>
    /// Start a new chat cooldown.
    /// </summary>
    public static void StartChatCooldown()
    {
        _globalChatCooldown = MinChatInterval + (float)_random.NextDouble() * (MaxChatInterval - MinChatInterval);
    }

    /// <summary>
    /// Update the global chat cooldown.
    /// </summary>
    public static void UpdateCooldown(float delta)
    {
        if (_globalChatCooldown > 0)
            _globalChatCooldown -= delta;
    }

    /// <summary>
    /// Get a random chat line for two monster types interacting.
    /// </summary>
    public static (string speaker1Line, string? speaker2Line) GetChatLines(string type1, string type2)
    {
        // Normalize types for lookup
        string key = GetPairKey(type1, type2);

        if (PairDialogues.TryGetValue(key, out var dialogues))
        {
            var dialogue = dialogues[_random.Next(dialogues.Length)];
            return dialogue;
        }

        // Fallback to same-type dialogues
        string sameTypeKey = GetPairKey(type1, type1);
        if (type1 == type2 && SameTypeDialogues.TryGetValue(NormalizeType(type1), out var sameDialogues))
        {
            var dialogue = sameDialogues[_random.Next(sameDialogues.Length)];
            return dialogue;
        }

        // Generic fallback
        var generic = GenericDialogues[_random.Next(GenericDialogues.Length)];
        return generic;
    }

    private static string GetPairKey(string type1, string type2)
    {
        string t1 = NormalizeType(type1);
        string t2 = NormalizeType(type2);
        // Alphabetical order for consistent lookup
        return string.Compare(t1, t2, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{t1}_{t2}"
            : $"{t2}_{t1}";
    }

    private static string NormalizeType(string type)
    {
        return type.ToLower().Replace(" ", "_");
    }

    // Same-type dialogues (monster talking to same species)
    private static readonly Dictionary<string, (string, string?)[]> SameTypeDialogues = new()
    {
        ["goblin"] = new[]
        {
            ("Got any shinies?", "No shinies! Only stabbies!"),
            ("Boss is angry again...", "Boss always angry. Is boss thing."),
            ("You smell funny.", "YOU smell funny!"),
            ("My cousin got squished yesterday.", "Was he the dumb one?"),
            ("I found a boot!", "Only one boot? Useless."),
            ("That human scary.", "All humans scary. Too tall."),
            ("Want trade lunch?", "What you got?"),
            ("I'm bored.", "Me too. Wanna poke something?"),
        },
        ["skeleton"] = new[]
        {
            ("*rattle rattle*", "*rattle*"),
            ("My arm fell off again.", "Just put it back. Drama queen."),
            ("I feel empty inside.", "We ALL feel empty inside."),
            ("Remember when we had skin?", "Vaguely. Was itchy."),
            ("These adventurers have no respect.", "*nods skull*"),
            ("Got any spare ribs?", "That's not funny anymore."),
        },
        ["slime"] = new[]
        {
            ("*gloop*", "*bloop*"),
            ("*wobble*", "*jiggle*"),
            ("*splorch*", "*gurgle*"),
            ("*bounce bounce*", "*squish*"),
        },
        ["spider"] = new[]
        {
            ("*click click*", "*hiss*"),
            ("Web sales are down.", "Economy is rough."),
            ("Eight legs, still trip.", "Mood."),
            ("Caught anything good?", "Just dust. Always dust."),
        },
        ["wolf"] = new[]
        {
            ("*growl*", "*growl back*"),
            ("Smell that?", "Adventurer. Fresh."),
            ("Pack hunt later?", "Always pack hunt."),
            ("Moon not out yet.", "Patience."),
        },
        ["bat"] = new[]
        {
            ("*screech*", "*screech screech*"),
            ("Can't see anything.", "Use your ears, dummy."),
            ("Ceiling comfy?", "Best spot."),
        },
        ["mushroom"] = new[]
        {
            ("*spore puff*", "*spore puff*"),
            ("Feeling sporadic today.", "Me too, friend."),
            ("Sun bad.", "Sun VERY bad."),
            ("Nice damp here.", "Very nice damp."),
        },
        ["dragon"] = new[]
        {
            ("Hoard looking good?", "Could be shinier."),
            ("Burnt any villages lately?", "Last Tuesday. Classic."),
            ("Gold or gems?", "Why not both?"),
        },
        ["eye"] = new[]
        {
            ("*blink*", "*blink blink*"),
            ("See anything?", "I see EVERYTHING."),
            ("Dry in here.", "Need eye drops."),
        },
        ["lizard"] = new[]
        {
            ("*hiss*", "*hiss hiss*"),
            ("Sun rock taken.", "Find own sun rock!"),
            ("Cold today.", "Very cold. Need warmth."),
        },
    };

    // Cross-type dialogues (different species interacting)
    private static readonly Dictionary<string, (string, string?)[]> PairDialogues = new()
    {
        ["goblin_skeleton"] = new[]
        {
            ("You need meat on those bones!", "At least I have STYLE."),
            ("How do you even hold things?", "Very carefully."),
            ("Stop rattling, it's creepy!", "*rattles louder*"),
            ("Want some food?", "Can't eat. No stomach."),
        },
        ["goblin_slime"] = new[]
        {
            ("Ew, you're sticky.", "*gloop* (offended)"),
            ("Stay away from my stuff!", "*inch closer*"),
            ("Can you even talk?", "*aggressive wobbling*"),
        },
        ["goblin_spider"] = new[]
        {
            ("Too many legs!", "Too FEW legs."),
            ("Your webs keep getting on my stuff!", "Feature, not bug."),
            ("Ugh, eyes everywhere.", "Better vision than YOU."),
        },
        ["skeleton_slime"] = new[]
        {
            ("You're dissolving my joints!", "*apologetic bubble*"),
            ("*bones rattle nervously*", "*curious wobble*"),
            ("Stay back, goo creature!", "*sad squish*"),
        },
        ["skeleton_spider"] = new[]
        {
            ("Stop webbing my ribs!", "Good anchor points."),
            ("*annoyed rattle*", "*innocent chittering*"),
        },
        ["slime_mushroom"] = new[]
        {
            ("*absorb attempt*", "*toxic spore defense*"),
            ("*curious wobble*", "*nervous spore puff*"),
        },
        ["wolf_goblin"] = new[]
        {
            ("*threatening growl*", "Nice doggy... nice doggy..."),
            ("Smell fear.", "N-not scared!"),
            ("*sniff sniff*", "Personal space!"),
        },
        ["dragon_goblin"] = new[]
        {
            ("KNEEL.", "Yes boss! Kneeling boss!"),
            ("Bring tribute.", "Right away, great one!"),
            ("You again?", "*nervous sweating*"),
        },
        ["bat_spider"] = new[]
        {
            ("Your webs ruined my flight path!", "Watch where you're going."),
            ("*echolocation*", "*web vibration*"),
        },
        ["eye_goblin"] = new[]
        {
            ("I see your secrets.", "W-what secrets?!"),
            ("*intense staring*", "Stop that!"),
            ("Fascinating specimen.", "I'm not a specimen!"),
        },
    };

    // Generic fallback dialogues
    private static readonly (string, string?)[] GenericDialogues = new[]
    {
        ("...", "..."),
        ("*awkward silence*", "*nod*"),
        ("Weather's nice.", "If you're into dungeon weather."),
        ("Adventurers, am I right?", "Tell me about it."),
        ("Seen any loot?", "Not sharing."),
        ("This dungeon needs better decor.", "Budget cuts."),
        ("Guard duty is boring.", "Could be worse."),
        ("*yawn*", "*stretch*"),
        ("Break time?", "Always break time."),
        ("What level you think they are?", "Too high for comfort."),
    };
}
