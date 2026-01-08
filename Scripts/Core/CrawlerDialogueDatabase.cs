using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Database of dialogue trees for Crawler NPCs (DCC-themed dungeon delvers).
/// Each crawler has unique personality-driven dialogues with branching paths.
/// </summary>
public static class CrawlerDialogueDatabase
{
    /// <summary>
    /// A single dialogue node with text and optional response choices.
    /// </summary>
    public class DialogueNode
    {
        public string Id { get; set; } = "";
        public string Speaker { get; set; } = "";
        public string Text { get; set; } = "";
        public List<DialogueResponse> Responses { get; set; } = new();
        public bool IsEndNode => Responses.Count == 0;
    }

    /// <summary>
    /// A player response option that leads to another dialogue node.
    /// </summary>
    public class DialogueResponse
    {
        public string Text { get; set; } = "";
        public string NextNodeId { get; set; } = "";
    }

    /// <summary>
    /// Get the starting dialogue node for a crawler type.
    /// </summary>
    public static DialogueNode? GetStartingDialogue(string crawlerType)
    {
        return crawlerType.ToLower() switch
        {
            "crawler_rex" => GetNode("rex_start"),
            "crawler_lily" => GetNode("lily_start"),
            "crawler_chad" => GetNode("chad_start"),
            "crawler_shade" => GetNode("shade_start"),
            "crawler_hank" => GetNode("hank_start"),
            _ => null
        };
    }

    /// <summary>
    /// Get a dialogue node by its ID.
    /// </summary>
    public static DialogueNode? GetNode(string nodeId)
    {
        return AllDialogues.TryGetValue(nodeId, out var node) ? node : null;
    }

    // All dialogue nodes indexed by ID
    private static readonly Dictionary<string, DialogueNode> AllDialogues = new()
    {
        // =====================================================
        // REX "IRONSIDE" MARTINEZ - Grizzled Veteran
        // =====================================================
        ["rex_start"] = new DialogueNode
        {
            Id = "rex_start",
            Speaker = "Rex",
            Text = "*adjusts worn military vest* Another crawler, huh? Name's Rex. Been down here longer than I care to remember. Lost count after floor fifty.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Got any tips for surviving?", NextNodeId = "rex_tips" },
                new() { Text = "What happened to your team?", NextNodeId = "rex_team" },
                new() { Text = "Seen anything interesting lately?", NextNodeId = "rex_interesting" },
                new() { Text = "Just passing through.", NextNodeId = "rex_bye" }
            }
        },
        ["rex_tips"] = new DialogueNode
        {
            Id = "rex_tips",
            Speaker = "Rex",
            Text = "*cracks knuckles* First rule: never trust a chest that smiles at you. Second: slimes dissolve leather - learned that one the hard way. Third: if a goblin offers you a 'deal', run.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "What about combat advice?", NextNodeId = "rex_combat" },
                new() { Text = "Any other survival tips?", NextNodeId = "rex_survival" },
                new() { Text = "Thanks, that helps.", NextNodeId = "rex_bye" }
            }
        },
        ["rex_combat"] = new DialogueNode
        {
            Id = "rex_combat",
            Speaker = "Rex",
            Text = "Hit hard, hit first. Skeletons? Blunt force. Slimes? Fire or cold. Spiders? Never let 'em get behind you. And dragons... *pauses* ...run. Just run.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "You've fought a dragon?", NextNodeId = "rex_dragon" },
                new() { Text = "Good to know. Thanks.", NextNodeId = "rex_bye" }
            }
        },
        ["rex_dragon"] = new DialogueNode
        {
            Id = "rex_dragon",
            Speaker = "Rex",
            Text = "*touches a burn scar on his neck* Once. Lost three good friends that day. Barely escaped with my skin... well, most of it. Some battles you don't win - you survive.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "I'm sorry for your loss.", NextNodeId = "rex_loss" },
                new() { Text = "I should go.", NextNodeId = "rex_bye" }
            }
        },
        ["rex_loss"] = new DialogueNode
        {
            Id = "rex_loss",
            Speaker = "Rex",
            Text = "*nods slowly* It's the way of the dungeon. You either get out, or you become part of the floor. Honor their memory by not making the same mistakes. Stay sharp, crawler.",
            Responses = new List<DialogueResponse>()
        },
        ["rex_survival"] = new DialogueNode
        {
            Id = "rex_survival",
            Speaker = "Rex",
            Text = "Always carry antivenom. Keep your back to a wall when resting. Trust your gut - if a room feels wrong, it probably is. And never, EVER skip breakfast.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Why breakfast specifically?", NextNodeId = "rex_breakfast" },
                new() { Text = "Thanks for the advice.", NextNodeId = "rex_bye" }
            }
        },
        ["rex_breakfast"] = new DialogueNode
        {
            Id = "rex_breakfast",
            Speaker = "Rex",
            Text = "*chuckles darkly* Fought a mimic on an empty stomach once. Passed out mid-fight. Woke up inside the thing. Trust me - eat first, fight second.",
            Responses = new List<DialogueResponse>()
        },
        ["rex_team"] = new DialogueNode
        {
            Id = "rex_team",
            Speaker = "Rex",
            Text = "*expression hardens* Started with six. Good people. Now it's just me. Mimics got Jenkins. Spider Queen took Maria. The rest... *shakes head* ...this place takes everyone eventually.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Why do you keep going?", NextNodeId = "rex_why" },
                new() { Text = "I won't end up like them.", NextNodeId = "rex_confident" },
                new() { Text = "I'm sorry.", NextNodeId = "rex_bye" }
            }
        },
        ["rex_why"] = new DialogueNode
        {
            Id = "rex_why",
            Speaker = "Rex",
            Text = "Because stopping means dying. Because someone's gotta warn the new crawlers. Because... I made a promise to get everyone home. Even if I'm the only one left to keep it.",
            Responses = new List<DialogueResponse>()
        },
        ["rex_confident"] = new DialogueNode
        {
            Id = "rex_confident",
            Speaker = "Rex",
            Text = "*gives a bitter laugh* Kid, we all said that. Keep that fire - you'll need it. Just remember: confidence kills. Caution survives.",
            Responses = new List<DialogueResponse>()
        },
        ["rex_interesting"] = new DialogueNode
        {
            Id = "rex_interesting",
            Speaker = "Rex",
            Text = "Saw some goblins setting up what looked like a trading post. Either they're getting smarter or someone's organizing them. Neither option is good.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Organized goblins? That's new.", NextNodeId = "rex_goblins" },
                new() { Text = "I'll keep an eye out.", NextNodeId = "rex_bye" }
            }
        },
        ["rex_goblins"] = new DialogueNode
        {
            Id = "rex_goblins",
            Speaker = "Rex",
            Text = "Yeah. Usually they just stab and grab. But these ones had actual inventory. Price tags. A RETURN POLICY. The dungeon's changing, crawler. Stay on your toes.",
            Responses = new List<DialogueResponse>()
        },
        ["rex_bye"] = new DialogueNode
        {
            Id = "rex_bye",
            Speaker = "Rex",
            Text = "*nods* Watch your back out there. And if you see a chest with teeth... you didn't see it from me.",
            Responses = new List<DialogueResponse>()
        },

        // =====================================================
        // LILY CHEN - Nervous Rookie
        // =====================================================
        ["lily_start"] = new DialogueNode
        {
            Id = "lily_start",
            Speaker = "Lily",
            Text = "*jumps slightly* Oh! Hi! You scared me. I mean, not that YOU'RE scary. Just, you know, everything else is. I'm Lily. Is it okay if I stand near you for a minute?",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Sure, are you okay?", NextNodeId = "lily_okay" },
                new() { Text = "How long have you been down here?", NextNodeId = "lily_time" },
                new() { Text = "Have you seen anything dangerous?", NextNodeId = "lily_danger" },
                new() { Text = "I need to keep moving.", NextNodeId = "lily_bye" }
            }
        },
        ["lily_okay"] = new DialogueNode
        {
            Id = "lily_okay",
            Speaker = "Lily",
            Text = "*nervous laugh* Define okay? I've been chased by skeletons, almost eaten by a slime, and I'm pretty sure something has been following me for the last hour. So... surviving?",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Following you? Like what?", NextNodeId = "lily_following" },
                new() { Text = "How did you get down here?", NextNodeId = "lily_story" },
                new() { Text = "Stay strong. You'll make it.", NextNodeId = "lily_encourage" }
            }
        },
        ["lily_following"] = new DialogueNode
        {
            Id = "lily_following",
            Speaker = "Lily",
            Text = "*whispers* I keep hearing footsteps when I stop walking. And sometimes... breathing? But when I turn around, nothing's there. Please tell me I'm imagining things.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Could be an invisible enemy.", NextNodeId = "lily_invisible" },
                new() { Text = "Probably just echoes.", NextNodeId = "lily_echoes" }
            }
        },
        ["lily_invisible"] = new DialogueNode
        {
            Id = "lily_invisible",
            Speaker = "Lily",
            Text = "*goes pale* INVISIBLE?! There are INVISIBLE things down here?! Oh no. Oh no no no. I need to... I need to find somewhere to hide. With walls. Lots of walls.",
            Responses = new List<DialogueResponse>()
        },
        ["lily_echoes"] = new DialogueNode
        {
            Id = "lily_echoes",
            Speaker = "Lily",
            Text = "*takes a deep breath* Right. Yes. Echoes. These stone walls play tricks. That's... that's probably it. Thanks, I feel slightly less terrified now.",
            Responses = new List<DialogueResponse>()
        },
        ["lily_time"] = new DialogueNode
        {
            Id = "lily_time",
            Speaker = "Lily",
            Text = "Three days? Maybe four? There's no sun down here so I've kind of lost track. It feels like weeks though. Every room looks the same after a while.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "How have you survived?", NextNodeId = "lily_survive" },
                new() { Text = "Hang in there.", NextNodeId = "lily_bye" }
            }
        },
        ["lily_survive"] = new DialogueNode
        {
            Id = "lily_survive",
            Speaker = "Lily",
            Text = "Hiding, mostly. I found this oversized armor that makes me look bigger than I am. And I've gotten really good at finding small spaces to squeeze into. Spiders can't fit in air vents, right? RIGHT?",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Small spiders can...", NextNodeId = "lily_small_spiders" },
                new() { Text = "You're doing great.", NextNodeId = "lily_bye" }
            }
        },
        ["lily_small_spiders"] = new DialogueNode
        {
            Id = "lily_small_spiders",
            Speaker = "Lily",
            Text = "*stares* ...I'm never hiding in a vent again. Thank you for ruining my only safe spot. I mean that sincerely. Better to know, right? *laughs nervously*",
            Responses = new List<DialogueResponse>()
        },
        ["lily_story"] = new DialogueNode
        {
            Id = "lily_story",
            Speaker = "Lily",
            Text = "I was exploring this old building for my architecture thesis. Found some weird stairs. Thought 'how deep could it go?' Turns out: VERY. Now I'm thesis-less and possibly doomed!",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Architecture? Spot any weaknesses?", NextNodeId = "lily_architecture" },
                new() { Text = "We'll find a way out.", NextNodeId = "lily_bye" }
            }
        },
        ["lily_architecture"] = new DialogueNode
        {
            Id = "lily_architecture",
            Speaker = "Lily",
            Text = "*perks up* Actually yes! The dungeon uses inconsistent load-bearing structures. And the room proportions follow patterns - larger rooms usually have multiple exits. Also the decorations are tacky.",
            Responses = new List<DialogueResponse>()
        },
        ["lily_danger"] = new DialogueNode
        {
            Id = "lily_danger",
            Speaker = "Lily",
            Text = "Define dangerous? There's a room two halls back with glowing mushrooms that made me hallucinate my grandmother yelling at me. Also actual danger: at least three goblins camping near the fountain.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Mushrooms made you hallucinate?", NextNodeId = "lily_mushroom" },
                new() { Text = "Thanks for the warning.", NextNodeId = "lily_bye" }
            }
        },
        ["lily_mushroom"] = new DialogueNode
        {
            Id = "lily_mushroom",
            Speaker = "Lily",
            Text = "Don't breathe the spores! Or touch them. Or look at them too long, apparently. My grandma kept telling me to stand up straight and stop exploring murder dungeons. Good advice, honestly.",
            Responses = new List<DialogueResponse>()
        },
        ["lily_encourage"] = new DialogueNode
        {
            Id = "lily_encourage",
            Speaker = "Lily",
            Text = "*small smile* Thanks. I needed that. You know, for a death dungeon, there are some surprisingly nice people down here. Stay safe, okay? We rookies gotta stick together. Sort of. From a distance.",
            Responses = new List<DialogueResponse>()
        },
        ["lily_bye"] = new DialogueNode
        {
            Id = "lily_bye",
            Speaker = "Lily",
            Text = "*waves anxiously* Okay, bye! Be careful! Watch out for mimics! And slimes! And everything else! *takes a deep breath* I'm fine. This is fine. Everything is fine.",
            Responses = new List<DialogueResponse>()
        },

        // =====================================================
        // CHAD "THE CHAMP" VALORIUS - Cocky Showoff
        // =====================================================
        ["chad_start"] = new DialogueNode
        {
            Id = "chad_start",
            Speaker = "Chad",
            Text = "*flexes* Well well, another fan! The name's Chad Valorius - you've probably heard of me. Slayer of beasts, crusher of dungeons, wearer of this SICK headband. What can The Champ do for you?",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Never heard of you.", NextNodeId = "chad_never" },
                new() { Text = "Got any advice, 'Champ'?", NextNodeId = "chad_advice" },
                new() { Text = "What's your biggest victory?", NextNodeId = "chad_victory" },
                new() { Text = "I'll pass.", NextNodeId = "chad_bye" }
            }
        },
        ["chad_never"] = new DialogueNode
        {
            Id = "chad_never",
            Speaker = "Chad",
            Text = "*blinks* Never... heard... okay wow, the dungeon really does block cell service. I have like, 50k followers topside! I'm kind of a big deal! There's merchandise!",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Merchandise? Really?", NextNodeId = "chad_merch" },
                new() { Text = "How's fame working out down here?", NextNodeId = "chad_fame" }
            }
        },
        ["chad_merch"] = new DialogueNode
        {
            Id = "chad_merch",
            Speaker = "Chad",
            Text = "*pulls out a crumpled photo* T-shirts, mugs, this sick poster of me fighting a dragon! ...Okay it was a large lizard but the lighting was dramatic. Point is - BRAND RECOGNITION.",
            Responses = new List<DialogueResponse>()
        },
        ["chad_fame"] = new DialogueNode
        {
            Id = "chad_fame",
            Speaker = "Chad",
            Text = "*deflates slightly* Not... great. Slimes don't care about subscriber counts. Skeletons don't take selfies. And my ring light ran out of batteries on floor two. It's been a humbling experience.",
            Responses = new List<DialogueResponse>()
        },
        ["chad_advice"] = new DialogueNode
        {
            Id = "chad_advice",
            Speaker = "Chad",
            Text = "Advice? THE CHAMP has TONS. Rule one: always look cool. Even if you're running away, do it stylishly. Rule two: victory poses are mandatory. Rule three: core strength. Always core strength.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Any... practical advice?", NextNodeId = "chad_practical" },
                new() { Text = "Victory poses against monsters?", NextNodeId = "chad_poses" }
            }
        },
        ["chad_practical"] = new DialogueNode
        {
            Id = "chad_practical",
            Speaker = "Chad",
            Text = "*thinks hard* Oh! Goblin armor has weak points at the armpits. Skeletons reassemble if you don't scatter the bones. And slimes... *shudders* ...don't flex at slimes. They don't care and you just look dumb.",
            Responses = new List<DialogueResponse>()
        },
        ["chad_poses"] = new DialogueNode
        {
            Id = "chad_poses",
            Speaker = "Chad",
            Text = "Listen, documentation is IMPORTANT. How else will people know I won if there's no dramatic photo? ...The goblins stole three of my cameras. They're surprisingly photography-literate.",
            Responses = new List<DialogueResponse>()
        },
        ["chad_victory"] = new DialogueNode
        {
            Id = "chad_victory",
            Speaker = "Chad",
            Text = "*puffs up* Once held off SEVEN goblins with nothing but my bare hands and raw charisma! They were so impressed they just... left. Totally not because backup arrived. Pure Champ energy.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "So backup saved you?", NextNodeId = "chad_backup" },
                new() { Text = "Impressive, I guess.", NextNodeId = "chad_bye" }
            }
        },
        ["chad_backup"] = new DialogueNode
        {
            Id = "chad_backup",
            Speaker = "Chad",
            Text = "*defensive* They ASSISTED. There's a difference. The goblins were clearly intimidated before the others showed up. You could see it in their eyes. All six of their eyes. They have a lot of eyes.",
            Responses = new List<DialogueResponse>()
        },
        ["chad_bye"] = new DialogueNode
        {
            Id = "chad_bye",
            Speaker = "Chad",
            Text = "*points finger guns* Stay golden, fellow crawler! And if you find any of my cameras, The Champ would appreciate it. They have important... research footage. Definitely not blooper reels.",
            Responses = new List<DialogueResponse>()
        },

        // =====================================================
        // THE SILENT ONE (SHADE) - Mysterious Cryptic
        // =====================================================
        ["shade_start"] = new DialogueNode
        {
            Id = "shade_start",
            Speaker = "???",
            Text = "*a hooded figure turns slowly, eyes glowing faintly beneath the cowl* ...You walk where shadows gather. Most do not pause when they see me. Curious.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Who are you?", NextNodeId = "shade_who" },
                new() { Text = "What do you know about this place?", NextNodeId = "shade_place" },
                new() { Text = "You seem... different.", NextNodeId = "shade_different" },
                new() { Text = "*slowly back away*", NextNodeId = "shade_bye" }
            }
        },
        ["shade_who"] = new DialogueNode
        {
            Id = "shade_who",
            Speaker = "Shade",
            Text = "*pause* I have had many names. The crawlers call me Shade. The monsters... have other names. Older names. Names that mean 'do not approach'. Wise, perhaps.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Are you human?", NextNodeId = "shade_human" },
                new() { Text = "How long have you been here?", NextNodeId = "shade_long" }
            }
        },
        ["shade_human"] = new DialogueNode
        {
            Id = "shade_human",
            Speaker = "Shade",
            Text = "*tilts head* I... was. Once. The dungeon changes those who linger. My humanity is a distant memory, like warmth or the taste of bread. I am what remains.",
            Responses = new List<DialogueResponse>()
        },
        ["shade_long"] = new DialogueNode
        {
            Id = "shade_long",
            Speaker = "Shade",
            Text = "*glowing eyes flicker* Time moves strangely in the deep places. I have watched civilizations rise above while roots grew through my dwelling. Decades? Centuries? The count lost meaning long ago.",
            Responses = new List<DialogueResponse>()
        },
        ["shade_place"] = new DialogueNode
        {
            Id = "shade_place",
            Speaker = "Shade",
            Text = "The dungeon is not a place. It is an appetite. It draws the curious, the desperate, the ambitious. It feeds on dreams and leaves bones. But it also... rewards. Sometimes.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Rewards how?", NextNodeId = "shade_rewards" },
                new() { Text = "How do I get out?", NextNodeId = "shade_out" }
            }
        },
        ["shade_rewards"] = new DialogueNode
        {
            Id = "shade_rewards",
            Speaker = "Shade",
            Text = "Power. Knowledge. The treasures of forgotten ages. The dungeon gives to those who take. But every gift has weight. Every power has price. Do not accept lightly what darkness offers.",
            Responses = new List<DialogueResponse>()
        },
        ["shade_out"] = new DialogueNode
        {
            Id = "shade_out",
            Speaker = "Shade",
            Text = "*strange, hollow laugh* Out? One does not simply leave. The dungeon decides when you are worthy of release. Some descend for years seeking an exit that reveals itself only when they stop searching.",
            Responses = new List<DialogueResponse>()
        },
        ["shade_different"] = new DialogueNode
        {
            Id = "shade_different",
            Speaker = "Shade",
            Text = "Different. Yes. The monsters sense it. They do not attack me, nor I them. We have... an understanding. The dungeon's children recognize its adopted. We coexist in darkness.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Can you teach me that?", NextNodeId = "shade_teach" },
                new() { Text = "That sounds lonely.", NextNodeId = "shade_lonely" }
            }
        },
        ["shade_teach"] = new DialogueNode
        {
            Id = "shade_teach",
            Speaker = "Shade",
            Text = "*eyes dim* It is not taught. It is... given up. Trade your warmth for cold sight. Your voice for whispers. Your reflection for shadows. I do not recommend this path. The price is... everything you were.",
            Responses = new List<DialogueResponse>()
        },
        ["shade_lonely"] = new DialogueNode
        {
            Id = "shade_lonely",
            Speaker = "Shade",
            Text = "*long pause* ...Lonely. Yes. That is a word I had forgotten. Thank you for... reminding me what I was. *voice softens almost imperceptibly* Go. Live warmly while you can.",
            Responses = new List<DialogueResponse>()
        },
        ["shade_bye"] = new DialogueNode
        {
            Id = "shade_bye",
            Speaker = "Shade",
            Text = "*begins fading into shadow* Walk carefully, living one. The dungeon watches. It always watches. We will meet again... perhaps.",
            Responses = new List<DialogueResponse>()
        },

        // =====================================================
        // HANK "NOODLES" PATTERSON - Comedic Relief
        // =====================================================
        ["hank_start"] = new DialogueNode
        {
            Id = "hank_start",
            Speaker = "Hank",
            Text = "*trips over own feet, catches self* Whoa! Hey there, friend! Name's Hank. Some call me Noodles. Don't ask why - okay fine it's because of the spaghetti incident. Long story. How ya doing?",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "What spaghetti incident?", NextNodeId = "hank_spaghetti" },
                new() { Text = "You look like you've seen some things.", NextNodeId = "hank_seen" },
                new() { Text = "Got anything useful?", NextNodeId = "hank_useful" },
                new() { Text = "Stay safe, Hank.", NextNodeId = "hank_bye" }
            }
        },
        ["hank_spaghetti"] = new DialogueNode
        {
            Id = "hank_spaghetti",
            Speaker = "Hank",
            Text = "*sighs* So I found what I THOUGHT was a food crate, right? Turns out it was a mimic. Mid-way through eating it, IT started eating ME. We were both covered in marinara. Zero stars, do not recommend.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Wait, you ate a mimic?", NextNodeId = "hank_ate_mimic" },
                new() { Text = "How did you survive?", NextNodeId = "hank_survive" }
            }
        },
        ["hank_ate_mimic"] = new DialogueNode
        {
            Id = "hank_ate_mimic",
            Speaker = "Hank",
            Text = "ACCIDENTALLY. It tasted like drawer wood and regret. Now I always poke food with a stick first. Three times. While saying 'please don't eat me'. Works 60% of the time!",
            Responses = new List<DialogueResponse>()
        },
        ["hank_survive"] = new DialogueNode
        {
            Id = "hank_survive",
            Speaker = "Hank",
            Text = "Turns out mimics can't digest pure panic. Also I fell through a rotten floor mid-fight. Landed in a goblin's laundry pile. Now I smell like regret AND goblin socks. Win-win!",
            Responses = new List<DialogueResponse>()
        },
        ["hank_seen"] = new DialogueNode
        {
            Id = "hank_seen",
            Speaker = "Hank",
            Text = "Oh buddy, you have NO idea. I've seen a skeleton try to moonwalk. Watched two slimes merge and argue about who gets the eyes. Got invited to a spider tea party. The dungeon is WEIRD.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "Spider tea party?", NextNodeId = "hank_tea" },
                new() { Text = "Slimes argue?", NextNodeId = "hank_slimes" }
            }
        },
        ["hank_tea"] = new DialogueNode
        {
            Id = "hank_tea",
            Speaker = "Hank",
            Text = "Yeah! Little cups, tiny cakes, the whole thing. Very polite until I realized the 'cake' was webbed-up bugs. Had to excuse myself. They wrote me a strongly-worded letter about etiquette.",
            Responses = new List<DialogueResponse>()
        },
        ["hank_slimes"] = new DialogueNode
        {
            Id = "hank_slimes",
            Speaker = "Hank",
            Text = "More like... blob aggressively at each other? One wanted to go left, one wanted to go right. They split up, realized they were literally half the slime they used to be, then got back together. Relationship goals?",
            Responses = new List<DialogueResponse>()
        },
        ["hank_useful"] = new DialogueNode
        {
            Id = "hank_useful",
            Speaker = "Hank",
            Text = "*rummages through mismatched gear* Let's see... broken compass, half a map that's just a drawing of a sandwich, some lint... OH! I've got this shiny rock! It glows when monsters are near. Or when it's hungry. Not sure which.",
            Responses = new List<DialogueResponse>
            {
                new() { Text = "A hungry rock?", NextNodeId = "hank_rock" },
                new() { Text = "That's... something.", NextNodeId = "hank_bye" }
            }
        },
        ["hank_rock"] = new DialogueNode
        {
            Id = "hank_rock",
            Speaker = "Hank",
            Text = "Yeah it vibrates near food and also near giant spiders. Very confusing in spider territory. 'Ooh lunch!' No wait that's death. 'Death!' No wait that's lunch. Lost three lunches that way.",
            Responses = new List<DialogueResponse>()
        },
        ["hank_bye"] = new DialogueNode
        {
            Id = "hank_bye",
            Speaker = "Hank",
            Text = "*waves enthusiastically, trips* Whoop! I'm okay! Stay awesome, friend! And remember - if it jiggles, don't eat it. Or hug it. Or make eye contact. Actually just... run. Bye!",
            Responses = new List<DialogueResponse>()
        }
    };
}
