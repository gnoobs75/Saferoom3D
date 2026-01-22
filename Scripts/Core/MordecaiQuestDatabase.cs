using System;
using System.Collections.Generic;
using System.Linq;

namespace SafeRoom3D.Core;

/// <summary>
/// Database of quest templates, stories, and dialogue for Mordecai the Quest Giver.
/// Generates dynamic, humorous quests based on monsters present on the floor.
/// </summary>
public static class MordecaiQuestDatabase
{
    // Random instance for variety
    private static readonly Random _random = new();

    #region Quest Story Templates

    /// <summary>
    /// Kill quest templates per monster type. Each has multiple variations.
    /// {0} = monster name, {1} = count, {2} = monster part name
    /// </summary>
    private static readonly Dictionary<string, string[]> KillQuestTemplates = new()
    {
        ["goblin"] = new[]
        {
            "Those wretched goblins have been stealing my cheese wheels! Slay {1} of them and maybe they'll learn some manners.",
            "A goblin pickpocketed my favorite meditation crystal. Kill {1} goblins until you find the thief... or close enough.",
            "I've been trying to teach philosophy to the goblins. They responded by throwing rocks. Eliminate {1} of them.",
            "The goblins have formed a 'Mordecai Stinks' club. Disband it permanently by slaying {1} members.",
            "My tail got caught in a goblin trap last Tuesday. This is personal. Kill {1} goblins."
        },
        ["skeleton"] = new[]
        {
            "Those skeletons keep rattling at all hours. Destroy {1} of them so I can get some sleep.",
            "A skeleton tried to sell me my own bones. The audacity! Smash {1} of them.",
            "The skeletons formed a xylophone band using their ribs. It's terrible. End {1} of them.",
            "I asked a skeleton for directions. It pointed in four directions at once. Disassemble {1}.",
            "Skeletons have no appreciation for fine literature. Prove me right by destroying {1}."
        },
        ["slime"] = new[]
        {
            "Slimes got into my tea leaves. Now everything tastes like existential dread. Dissolve {1} slimes.",
            "A slime absorbed my favorite sandal. I want revenge on {1} of their kind.",
            "The slimes are leaving trails everywhere. It's a safety hazard. Eliminate {1}.",
            "I slipped on a slime yesterday. My dignity demands you destroy {1} of them.",
            "Slimes don't blink. It's unsettling. Make {1} of them stop... existing."
        },
        ["spider"] = new[]
        {
            "Spiders have been weaving rude messages in their webs. Squash {1} of them.",
            "A spider crawled into my ear while I was meditating. Eliminate {1} for my peace of mind.",
            "The spiders keep trying to gift-wrap me. Kill {1} before I become a present.",
            "I found a spider in my soup. Then another. Then another. End {1} of them.",
            "Eight legs is just showing off. Reduce the leg count by killing {1} spiders."
        },
        ["zombie"] = new[]
        {
            "Zombies have terrible hygiene. Put {1} of them out of everyone's misery.",
            "A zombie tried to eat my brain. Jokes on him - I'm mostly instinct. Still, kill {1}.",
            "The zombies moan too loudly. It's disrupting my meditation. Silence {1} permanently.",
            "Zombies keep leaving body parts around. It's unsanitary. Dispose of {1}.",
            "I tried reasoning with a zombie. Bad idea. Please eliminate {1} of them."
        },
        ["rat"] = new[]
        {
            "Yes, I know I'm a rat. No, that doesn't mean I like THOSE rats. Kill {1} of them.",
            "These dungeon rats give rats a bad name. Slay {1} to restore our honor.",
            "The rats have been spreading rumors about me. Silence {1} of them permanently.",
            "A rat stole my identity and opened a credit line. End {1} of these criminals.",
            "I'm not being species-ist, but those rats are jerks. Kill {1}."
        },
        ["bat"] = new[]
        {
            "Bats keep getting tangled in my whiskers. Eliminate {1} for my grooming sanity.",
            "A bat screeched directly into my ear. My hearing still rings. Kill {1}.",
            "The bats hold midnight raves above my quarters. End {1} of the party animals.",
            "Bats think they're so cool with their echolocation. Ground {1} of them permanently.",
            "I can't tell if bats are rats with wings or birds with fur. Study {1} by killing them."
        },
        ["orc"] = new[]
        {
            "Orcs challenged me to an arm wrestling match. I don't have their arms. Kill {1} instead.",
            "An orc called my philosophy 'nerd stuff.' Prove him wrong by killing {1} orcs.",
            "The orcs have been hogging the good loot. Thin their numbers by {1}.",
            "Orcs are surprisingly bad at chess. Sore losers too. Eliminate {1}.",
            "I offered an orc some herbal tea. He threw it at me. Kill {1} of them."
        },
        ["troll"] = new[]
        {
            "Trolls keep asking me riddles. I hate riddles. Slay {1} trolls.",
            "A troll sat on my meditation spot and wouldn't move. For three days. Kill {1}.",
            "Trolls regenerate, which is basically cheating at life. Kill {1} anyway.",
            "The trolls started a book club. They only read cookbooks. With us in them. End {1}.",
            "I asked a troll for the time. He tried to eat my watch. Destroy {1}."
        },
        ["demon"] = new[]
        {
            "Demons keep trying to make deals. I don't need a timeshare in Hell. Banish {1}.",
            "A demon offered me my heart's desire. It was cheese. Suspicious. Kill {1}.",
            "The demons have been practicing law down here. That's just redundant. End {1}.",
            "Demons think they're scary. Have they seen the rent prices up top? Slay {1}.",
            "I tried to convert a demon to pacifism. It didn't take. Kill {1}."
        },
        ["ghost"] = new[]
        {
            "Ghosts keep walking through my walls. It's rude. Dispel {1} of them.",
            "A ghost spoiled the ending of my book. Banish {1} to the beyond.",
            "The ghosts moan about their unfinished business. Help them finish by killing {1}.",
            "Ghosts don't pay rent. Evict {1} permanently.",
            "I tried to high-five a ghost. Very awkward. Destroy {1}."
        },
        ["mimic"] = new[]
        {
            "I sat on a mimic thinking it was a chair. TWICE. Kill {1} of these deceptive creatures.",
            "Mimics are why I have trust issues. Also the dungeon. Destroy {1}.",
            "A mimic pretended to be my diary. Now it knows my secrets. End {1}.",
            "The mimics have been impersonating doors. DOORS. Kill {1}.",
            "I tried to open a mimic treasure chest. Now I need new fingers. Slay {1}."
        },
        ["golem"] = new[]
        {
            "Golems have no sense of humor. I told a joke and one tried to squash me. Destroy {1}.",
            "A golem stepped on my tail. It didn't even apologize. Smash {1}.",
            "The golems keep rearranging my furniture. Badly. Crumble {1}.",
            "Golems are basically magic rocks with attitude problems. Break {1}.",
            "I asked a golem for directions. It pointed at itself. Unhelpful. Destroy {1}."
        },
        ["imp"] = new[]
        {
            "Imps have been drawing mustaches on my portraits. Kill {1} of the little vandals.",
            "An imp stole my reading glasses. Everything is blurry and I'm angry. End {1}.",
            "The imps keep giggling. What's so funny? Make {1} stop laughing forever.",
            "Imps set my tail on fire. It's still smoking. Extinguish {1} of them.",
            "I tried to pet an imp. Bad decision. Kill {1}."
        },
        ["wraith"] = new[]
        {
            "Wraiths are depressing to be around. Dispel {1} for morale.",
            "A wraith drained some of my life force. I need that! Destroy {1}.",
            "The wraiths keep whispering ominous prophecies. I don't want spoilers. End {1}.",
            "Wraiths float smugly like they're better than us walkers. Ground {1}.",
            "I offered a wraith a hug. Instant regret. Kill {1}."
        },
        ["elemental"] = new[]
        {
            "Fire elementals keep setting off the sprinklers. Extinguish {1}.",
            "An elemental called me 'organic.' Rude. Dissipate {1}.",
            "The elementals are causing weather inside. Kill {1} of them.",
            "Elementals have an elemental disregard for others. End {1}.",
            "I can't drink my tea near fire elementals. Too hot. Destroy {1}."
        },
        ["cultist"] = new[]
        {
            "Cultists keep trying to recruit me. I'm not joining their 'definitely not evil' club. Kill {1}.",
            "A cultist left pamphlets under my door. At 3 AM. End {1} of them.",
            "The cultists chant too loudly. It's not music. Silence {1}.",
            "Cultists claim their dark lord will rise soon. He's late. Kill {1}.",
            "I debated theology with a cultist. He tried to sacrifice me. End {1}."
        }
    };

    /// <summary>
    /// Collection quest templates per monster type.
    /// {0} = item name, {1} = count, {2} = monster name
    /// </summary>
    private static readonly Dictionary<string, string[]> CollectionQuestTemplates = new()
    {
        ["goblin"] = new[]
        {
            "I'm crafting a charm against bad luck. Bring me {1} {0}s from goblins.",
            "Goblin {0}s make excellent bookmarks. Collect {1} for my library.",
            "I'm writing a thesis on goblin biology. I need {1} {0}s for... research.",
            "The alchemist needs {1} {0}s. I don't ask questions. Neither should you.",
            "Collect {1} goblin {0}s. Don't ask why. It's for science. Weird science."
        },
        ["skeleton"] = new[]
        {
            "I need {1} {0}s from skeletons. I'm building a xylophone. A BETTER one.",
            "Skeleton {0}s are excellent for divination. Bring {1} please.",
            "My furniture is falling apart. Bring {1} {0}s for... repairs.",
            "I'm teaching anatomy. Bring {1} {0}s for the classroom.",
            "The necromancer owes me money. I'll take {1} {0}s as payment."
        },
        ["slime"] = new[]
        {
            "Slime {0}s are great for cleaning. Ironic, I know. Bring {1}.",
            "I need {1} {0}s for my skincare routine. Don't judge.",
            "Slime {0}s make excellent lamp fuel. Collect {1}.",
            "I'm making glue. Industrial strength. Bring {1} {0}s.",
            "The consistency of slime {0}s is perfect for... actually, just bring {1}."
        },
        ["spider"] = new[]
        {
            "Spider {0}s make incredible thread. Bring {1} for my tailoring.",
            "I need {1} {0}s for a potion. The results will be... silky.",
            "Spider {0}s are rare components. Gather {1} for my collection.",
            "I'm studying arachnid magic. Bring {1} {0}s for experiments.",
            "The weaver's guild pays well for {1} spider {0}s. Split the profit?"
        },
        ["default"] = new[]
        {
            "I require {1} {0}s for my research. The results will benefit us all.",
            "Bring me {1} {0}s. I have a very specific and legitimate use for them.",
            "The alchemists need {1} {0}s. They're paying well.",
            "I'm cataloging dungeon creatures. {1} {0}s would help tremendously.",
            "For reasons I cannot disclose, I need {1} {0}s. Trust me."
        }
    };

    #endregion

    #region Boss Quest Templates

    /// <summary>
    /// Boss kill quest templates. {0} = boss name
    /// </summary>
    private static readonly Dictionary<string, string[]> BossQuestTemplates = new()
    {
        ["slime_king"] = new[]
        {
            "The Slime King has been absorbing everything in sight. Including my spare socks. End his reign.",
            "His Royal Gelatinousness thinks he owns this floor. Show him the error of his ways.",
            "The Slime King sent me a declaration of war. In slime. Respond appropriately."
        },
        ["skeleton_lord"] = new[]
        {
            "The Skeleton Lord claims he has a bone to pick with everyone. Pick HIS bones instead.",
            "Lord Bonehead has been raising an army. Reduce it by one general.",
            "The Skeleton Lord's ego is bigger than his skull. Time for a humbling."
        },
        ["spider_queen"] = new[]
        {
            "The Spider Queen has been weaving plots. And literal webs. In my doorway. End her.",
            "Her eight-legged majesty demands tribute. I say we tribute her to oblivion.",
            "The Spider Queen invited me to dinner. I was the main course. Decline permanently."
        },
        ["dragon"] = new[]
        {
            "There's a dragon on this floor. A DRAGON. Why is there always a dragon?",
            "The dragon has been hoarding gold AND my patience. Slay it.",
            "I tried to reason with the dragon. It set my beard on fire. I don't have a beard. THAT'S how hot."
        },
        ["lich"] = new[]
        {
            "The Lich thinks undeath makes him immortal. Let's test that theory.",
            "His Undeadness has been sending me passive-aggressive curses. End him.",
            "The Lich won't stop monologuing. Silence him. Permanently."
        },
        ["minotaur_chief"] = new[]
        {
            "The Minotaur Chief challenged me to a maze race. I'm a rat in a dungeon. This is personal.",
            "Chief Hornhead has been charging at walls. And crawlers. Stop him.",
            "The Minotaur Chief ate all the hay. That was for... reasons. Eliminate him."
        },
        ["demon_lord"] = new[]
        {
            "The Demon Lord offered me eternal power. The paperwork was suspicious. Kill him.",
            "His Infernal Majesty keeps the floor too hot. I'm sweating through my fur. End this.",
            "The Demon Lord's laugh echoes everywhere. It's not even a good laugh. Silence it."
        },
        ["default"] = new[]
        {
            "A powerful boss creature lurks on this floor. They've been causing problems. Serious problems.",
            "The boss of this floor needs to be dealt with. Violently. For the good of everyone.",
            "I've heard terrible things about the boss here. Make those things stop. By killing them."
        }
    };

    #endregion

    #region Contextual Quest Modifiers

    /// <summary>
    /// Additional story elements when multiple monster types are present.
    /// </summary>
    private static readonly Dictionary<(string, string), string[]> MonsterInteractionStories = new()
    {
        [("goblin", "orc")] = new[]
        {
            " The goblins and orcs have formed an alliance. Break it up.",
            " Goblins are doing the orcs' paperwork now. Corporate structure in a dungeon!",
            " The goblins ride orcs into battle. It's as ridiculous as it sounds."
        },
        [("skeleton", "zombie")] = new[]
        {
            " The skeletons look down on zombies for having flesh. Undead racism.",
            " Skeletons and zombies are fighting over who's more dead. Help them find out.",
            " There's an undead union forming. Management (you) disagrees."
        },
        [("spider", "bat")] = new[]
        {
            " Spiders have been catching bats. Or bats eating spiders. Nature is metal.",
            " The bats and spiders share the ceiling. Awkward roommates.",
            " Spiders web the bats. Bats eat the spiders. Circle of dungeon life."
        },
        [("slime", "rat")] = new[]
        {
            " Rats keep getting stuck in slimes. It's tragic. Also kind of funny.",
            " The slimes absorbed a rat colony. They squeak now. Disturbing.",
            " Rats ride slimes like surfboards. I wish I was kidding."
        },
        [("demon", "cultist")] = new[]
        {
            " Cultists worship the demons here. Bad judgment all around.",
            " The demons don't even like their cultists. Still use them though.",
            " Cultist-demon relations are at an all-time low. Still bad for us."
        },
        [("ghost", "wraith")] = new[]
        {
            " Ghosts and wraiths argue about who's spookier. Kill them to settle it.",
            " The incorporeal beings float smugly together. Ground them all.",
            " Ghosts haunt the wraiths who haunt us. It's haunts all the way down."
        }
    };

    #endregion

    #region Mordecai Dialogue

    /// <summary>
    /// Mordecai's greetings when opening quest UI.
    /// </summary>
    public static readonly string[] Greetings = new[]
    {
        "Ah, a fellow seeker of purpose! Or gold. Usually gold.",
        "Welcome, crawler. I have tasks that need... permanent solutions.",
        "You look capable of violence. I have just the opportunities.",
        "Back again? The dungeon provides endless problems. And I provide quests.",
        "Greetings! I was just contemplating existence. Care to end some instead?",
        "Another brave soul! Or desperate one. Works either way.",
        "Perfect timing! I have problems and you have weapons. Synergy!",
        "Welcome to my humble corner of despair. Let's make it someone else's."
    };

    /// <summary>
    /// Mordecai's comments when player accepts a quest.
    /// </summary>
    public static readonly string[] QuestAcceptLines = new[]
    {
        "Excellent choice! Try not to die. Paperwork is a nightmare.",
        "May fortune favor you. And if not fortune, at least aim.",
        "Go forth! Return victorious! Or at all. Return at all would be nice.",
        "Your bravery is noted. Your survival is pending.",
        "The dungeon awaits! It's not excited to see you, but that's its problem.",
        "Adventure calls! It's more of a threatening whisper, really."
    };

    /// <summary>
    /// Mordecai's comments when player completes a quest.
    /// </summary>
    public static readonly string[] QuestCompleteLines = new[]
    {
        "You actually did it! I mean, of course you did. Never doubted.",
        "Splendid! The dungeon is slightly less terrible thanks to you.",
        "Success! Here's your reward. Spend it wisely. Or don't. I'm not your parent.",
        "Well done! I'll update my ledger. Under 'miracles.'",
        "Incredible! You survived AND succeeded. Today is a good day.",
        "The deed is done! The dungeon mourns. I celebrate. Here's gold."
    };

    /// <summary>
    /// Mordecai's comments for different quest difficulties.
    /// </summary>
    public static readonly Dictionary<string, string[]> DifficultyComments = new()
    {
        ["easy"] = new[]
        {
            "A simple task. Even I could do it. If I left this corner. Which I won't.",
            "Should be quick work for someone of your... enthusiasm.",
            "An easy one to warm up. The dungeon has training wheels sometimes."
        },
        ["medium"] = new[]
        {
            "A fair challenge. Bring bandages. Maybe two.",
            "Moderately dangerous. My favorite kind.",
            "This one requires actual effort. Novel concept, I know."
        },
        ["hard"] = new[]
        {
            "This task is... ambitious. I've prepared a eulogy. Just in case.",
            "A challenging endeavor. The rewards match the risk. Mostly.",
            "Danger level: significant. Reward level: also significant. Worth it."
        },
        ["boss"] = new[]
        {
            "A boss hunt. You're either brave or foolish. Effective either way.",
            "The big one. Legends are made of such quests. Also corpses.",
            "Face the champion of this floor. Return a hero. Or not at all."
        }
    };

    #endregion

    #region Public Methods

    /// <summary>
    /// Generate a quest title for a kill quest.
    /// </summary>
    public static string GenerateKillQuestTitle(string monsterType, int count)
    {
        string displayName = FormatMonsterName(monsterType);
        string[] titles = new[]
        {
            $"Cull the {displayName} Population",
            $"Thin the {displayName} Ranks",
            $"{displayName} Problem",
            $"The {displayName} Menace",
            $"Exterminate {displayName}s",
            $"A {displayName} Situation"
        };
        return titles[_random.Next(titles.Length)];
    }

    /// <summary>
    /// Generate a quest description for a kill quest.
    /// </summary>
    public static string GenerateKillQuestDescription(string monsterType, int count, List<string>? otherMonsters = null)
    {
        string key = monsterType.ToLower();
        string displayName = FormatMonsterName(monsterType);

        // Get base template
        string[] templates;
        if (KillQuestTemplates.TryGetValue(key, out var specificTemplates))
        {
            templates = specificTemplates;
        }
        else
        {
            templates = new[]
            {
                $"The {displayName}s have become a nuisance. Remove {{1}} of them.",
                $"Too many {displayName}s. Kill {{1}} to restore balance.",
                $"I've had it with these {displayName}s. Slay {{1}}.",
                $"The {displayName} population needs... management. Kill {{1}}.",
                $"For reasons, I need {{1}} {displayName}s dead. Don't ask."
            };
        }

        string description = templates[_random.Next(templates.Length)]
            .Replace("{0}", displayName)
            .Replace("{1}", count.ToString());

        // Add contextual story if other monsters present
        if (otherMonsters != null && otherMonsters.Count > 0)
        {
            foreach (var other in otherMonsters)
            {
                var interaction = GetMonsterInteraction(monsterType, other);
                if (interaction != null)
                {
                    description += interaction;
                    break; // Only add one interaction
                }
            }
        }

        return description;
    }

    /// <summary>
    /// Generate a quest title for a collection quest.
    /// </summary>
    public static string GenerateCollectionQuestTitle(string monsterType, string itemName, int count)
    {
        string displayItem = FormatItemName(itemName);
        string[] titles = new[]
        {
            $"Collect {displayItem}s",
            $"Gather the {displayItem}s",
            $"The {displayItem} Collection",
            $"Acquire {displayItem}s",
            $"Hunt for {displayItem}s"
        };
        return titles[_random.Next(titles.Length)];
    }

    /// <summary>
    /// Generate a quest description for a collection quest.
    /// </summary>
    public static string GenerateCollectionQuestDescription(string monsterType, string itemName, int count, List<string>? otherMonsters = null)
    {
        string key = monsterType.ToLower();
        string displayItem = FormatItemName(itemName);
        string displayMonster = FormatMonsterName(monsterType);

        string[] templates;
        if (CollectionQuestTemplates.TryGetValue(key, out var specificTemplates))
        {
            templates = specificTemplates;
        }
        else
        {
            templates = CollectionQuestTemplates["default"];
        }

        string description = templates[_random.Next(templates.Length)]
            .Replace("{0}", displayItem)
            .Replace("{1}", count.ToString())
            .Replace("{2}", displayMonster);

        // Add contextual story if other monsters present
        if (otherMonsters != null && otherMonsters.Count > 0)
        {
            foreach (var other in otherMonsters)
            {
                var interaction = GetMonsterInteraction(monsterType, other);
                if (interaction != null)
                {
                    description += interaction;
                    break;
                }
            }
        }

        return description;
    }

    /// <summary>
    /// Generate a quest title for a boss kill quest.
    /// </summary>
    public static string GenerateBossQuestTitle(string bossType)
    {
        string displayName = FormatMonsterName(bossType);
        string[] titles = new[]
        {
            $"Slay {displayName}",
            $"Defeat {displayName}",
            $"The {displayName} Must Fall",
            $"Boss Hunt: {displayName}",
            $"End the {displayName}'s Reign"
        };
        return titles[_random.Next(titles.Length)];
    }

    /// <summary>
    /// Generate a quest description for a boss kill quest.
    /// </summary>
    public static string GenerateBossQuestDescription(string bossType)
    {
        string key = bossType.ToLower().Replace(" ", "_");
        string displayName = FormatMonsterName(bossType);

        string[] templates;
        if (BossQuestTemplates.TryGetValue(key, out var specificTemplates))
        {
            templates = specificTemplates;
        }
        else
        {
            templates = BossQuestTemplates["default"];
        }

        return templates[_random.Next(templates.Length)].Replace("{0}", displayName);
    }

    /// <summary>
    /// Get a random greeting from Mordecai.
    /// </summary>
    public static string GetGreeting()
    {
        return Greetings[_random.Next(Greetings.Length)];
    }

    /// <summary>
    /// Get a random quest accept line from Mordecai.
    /// </summary>
    public static string GetQuestAcceptLine()
    {
        return QuestAcceptLines[_random.Next(QuestAcceptLines.Length)];
    }

    /// <summary>
    /// Get a random quest complete line from Mordecai.
    /// </summary>
    public static string GetQuestCompleteLine()
    {
        return QuestCompleteLines[_random.Next(QuestCompleteLines.Length)];
    }

    /// <summary>
    /// Get a difficulty comment for a quest.
    /// </summary>
    public static string GetDifficultyComment(string difficulty)
    {
        if (DifficultyComments.TryGetValue(difficulty.ToLower(), out var comments))
        {
            return comments[_random.Next(comments.Length)];
        }
        return DifficultyComments["medium"][0];
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get interaction story between two monster types if one exists.
    /// </summary>
    private static string? GetMonsterInteraction(string monster1, string monster2)
    {
        var key1 = (monster1.ToLower(), monster2.ToLower());
        var key2 = (monster2.ToLower(), monster1.ToLower());

        if (MonsterInteractionStories.TryGetValue(key1, out var stories1))
        {
            return stories1[_random.Next(stories1.Length)];
        }
        if (MonsterInteractionStories.TryGetValue(key2, out var stories2))
        {
            return stories2[_random.Next(stories2.Length)];
        }
        return null;
    }

    /// <summary>
    /// Format a monster type for display (e.g., "goblin_shaman" -> "Goblin Shaman").
    /// </summary>
    private static string FormatMonsterName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";

        // Replace underscores with spaces
        name = name.Replace("_", " ");

        // Capitalize each word
        var words = name.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }

    /// <summary>
    /// Format an item name for display (e.g., "goblin_ear" -> "Goblin Ear").
    /// </summary>
    private static string FormatItemName(string name)
    {
        return FormatMonsterName(name); // Same logic
    }

    #endregion
}
