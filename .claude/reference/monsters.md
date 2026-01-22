# Monster Reference

## Original Monsters (14 types)

| Type | HP | Speed | Damage | Color | Notes |
|------|---:|------:|-------:|-------|-------|
| Goblin | 75 | 3.0 | 10 | Olive green | Humanoid, can have random weapon |
| Goblin Shaman | 50 | 2.0 | 15 | Blue-green | Casts spells, buffs allies with Enrage |
| Goblin Thrower | 60 | 3.5 | 12 | Tan/brown | Throws spears/axes/beer cans |
| Slime | 50 | 2.0 | 8 | Green (translucent) | Blob with eyes |
| Eye | 40 | 4.0 | 15 | Red | Floating eyeball |
| Mushroom | 60 | 1.5 | 12 | Purple | Fungus creature |
| Spider | 55 | 4.5 | 14 | Dark brown | 8-legged |
| Lizard | 80 | 3.5 | 12 | Green-brown | Reptilian |
| Skeleton | 60 | 2.8 | 15 | Bone white | Undead, can have random weapon |
| Wolf | 70 | 5.0 | 18 | Gray-brown | Fast quadruped |
| Badlama | 90 | 2.5 | 20 | Brown/cream | Sturdy quadruped |
| Bat | 40 | 4.0 | 8 | Dark purple | Flying |
| Dragon | 200 | 2.5 | 35 | Red | Large winged creature |
| Rabbidd | 75 | 4.5 | 16 | Dark purple-crimson | Demonic rabbit with horns, glowing eyes |

## DCC Monsters (15 types)

| Type | HP | Speed | Damage | Notes |
|------|---:|------:|-------:|-------|
| Crawler Killer | 120 | 4.5 | 22 | Combat robot with weapon |
| Shadow Stalker | 70 | 5.5 | 18 | Fast ethereal |
| Flesh Golem | 200 | 2.0 | 30 | Heavy brute |
| Plague Bearer | 90 | 2.5 | 12 | Diseased creature |
| Living Armor | 150 | 2.8 | 25 | Animated armor with weapon |
| Camera Drone | 40 | 4.0 | 10 | Long range, alerts others |
| Shock Drone | 50 | 3.5 | 15 | Electric attacks |
| Advertiser Bot | 80 | 3.0 | 8 | Annoying, long range |
| Clockwork Spider | 65 | 5.0 | 14 | Mechanical arachnid |
| Lava Elemental | 110 | 2.5 | 28 | Fire damage, glowing |
| Ice Wraith | 75 | 4.0 | 16 | Cold ethereal |
| Gelatinous Cube | 180 | 1.5 | 20 | Slow but massive |
| Void Spawn | 85 | 4.5 | 22 | Otherworldly |
| Mimic | 100 | 0.5 | 35 | Ambush predator |
| Dungeon Rat | 25 | 6.0 | 6 | Fast swarm creature |

## Bosses

### Original Bosses
| Boss | HP | Speed | Damage | Special |
|------|---:|------:|-------:|---------|
| Skeleton Lord | 500 | 3.2 | 40 | Ground Slam AOE |
| Dragon King | 900 | 2.8 | 60 | Fire Breath barrage |
| Spider Queen | 450 | 4.5 | 45 | Summon Minions |

### DCC Bosses
| Boss | HP | Speed | Damage | Special |
|------|---:|------:|-------:|---------|
| The Butcher | 800 | 3.5 | 55 | Cleaver Throw |
| Mordecai | 600 | 4.0 | 40 | Shadow Step |
| Princess Donut | 400 | 5.0 | 30 | Royal Command (buff allies) |
| Mongo | 1200 | 2.5 | 70 | Earthquake stun |
| Zev | 350 | 6.0 | 25 | Treasure Toss |

## Monster Level Scaling

- **HP**: BaseHP × (1 + (Level-1) × 0.15)
- **Damage**: BaseDamage × (1 + (Level-1) × 0.10)
- **XP Reward**: BaseHealth × (1 + Level × 0.5) / 10

## Random Weapon Attachment

Eligible types: `goblin`, `skeleton`, `crawler_killer`, `living_armor`

Weapons selected from all 12 types, attached via `WeaponFactory.AttachToHumanoid()`.
