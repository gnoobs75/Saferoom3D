# Abilities Reference

## All 14 Abilities

| ID | Name | Type | CD | Mana | Description |
|----|------|------|---:|-----:|-------------|
| `fireball` | Fireball | Targeted | 8s | 20 | AOE fire damage |
| `chain_lightning` | Chain Lightning | Instant | 10s | 25 | Bounces 5 enemies |
| `soul_leech` | Soul Leech | Instant | 12s | 15 | Damage + heal |
| `protective_shell` | Protective Shell | Self | 120s | 30 | 15s invulnerability |
| `gravity_well` | Gravity Well | Targeted | 15s | 35 | Pull enemies |
| `timestop_bubble` | Timestop Bubble | Targeted | 20s | 40 | Freeze enemies |
| `infernal_ground` | Infernal Ground | Targeted | 12s | 30 | Fire DOT zone |
| `banshees_wail` | Banshee's Wail | Self | 25s | 35 | Fear enemies |
| `berserk` | Berserk | Self | 120s | 0 | 2x speed/damage 15s |
| `mirror_image` | Mirror Image | Self | 30s | 25 | Decoy illusions |
| `dead_mans_rally` | Dead Man's Rally | Toggle | 60s | 0 | Low HP bonus |
| `engine_of_tomorrow` | Engine of Tomorrow | Toggle | 45s | 0 | Slow enemies 50% |
| `audience_favorite` | Audience Favorite | Passive | 30s | 0 | Kill streak reset |
| `sponsor_blessing` | Sponsor's Blessing | Self | 90s | 0 | Random buff |

## Default Hotbar Layout

**Row 1 (1-0):** Fireball, Chain Lightning, Soul Leech, Protective Shell, Gravity Well, Timestop Bubble, Infernal Ground, Banshee's Wail, Berserk, Mirror Image

**Row 2 (Shift+1-0):** Dead Man's Rally, Engine of Tomorrow, Audience Favorite, Sponsor's Blessing

## Targeting System

Targeted abilities (Fireball, Gravity Well, Timestop, Infernal Ground):
1. Press hotbar key → targeting mode
2. Time freezes (`Engine.TimeScale = 0`)
3. Mouse cursor appears
4. Left-click to confirm, Right-click/Escape to cancel
