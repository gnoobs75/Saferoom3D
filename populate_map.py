#!/usr/bin/env python3
"""
Populate a SafeRoom3D map with aesthetically placed monsters and props.
Uses the map's tile data to intelligently place content.
"""

import json
import base64
import gzip
import random
from pathlib import Path

# Monster types by difficulty tier
TIER1_MONSTERS = ["dungeon_rat", "slime", "goblin"]
TIER2_MONSTERS = ["goblin_shaman", "goblin_thrower", "spider", "mushroom", "bat"]
TIER3_MONSTERS = ["skeleton", "wolf", "lizard", "eye", "badlama"]
TIER4_MONSTERS = ["crawler_killer", "shadow_stalker", "mimic", "flesh_golem"]
TIER5_MONSTERS = ["living_armor", "plague_bearer", "lava_elemental", "void_spawn"]

BOSSES = ["skeleton_lord", "dragon_king", "spider_queen", "the_butcher", "mordecai", "mongo"]

# Prop types by theme
DUNGEON_PROPS = ["barrel", "crate", "pot", "torch", "bone_pile", "skull_pile", "rubble_heap"]
TREASURE_PROPS = ["chest", "treasure_chest", "scattered_coins", "ancient_scroll"]
SPOOKY_PROPS = ["blood_pool", "coiled_chains", "manacles", "discarded_sword", "forgotten_shield"]
NATURE_PROPS = ["moss_patch", "glowing_mushrooms", "water_puddle", "thorny_vines"]
CAMP_PROPS = ["campfire", "abandoned_campfire", "rat_nest", "moldy_bread"]

def decode_tile_data(tile_data: str, width: int, depth: int) -> list:
    """Decode RLE+Base64 tile data to 2D array."""
    # Decode base64
    compressed = base64.b64decode(tile_data)
    # Decompress gzip
    decompressed = gzip.decompress(compressed)

    # RLE decode
    tiles = [[0] * depth for _ in range(width)]
    x, z = 0, 0
    i = 0
    while i < len(decompressed) and x < width:
        value = decompressed[i]
        i += 1
        count = 1
        if i < len(decompressed) and decompressed[i] == 0xFF:
            i += 1
            if i < len(decompressed):
                count = decompressed[i]
                i += 1

        for _ in range(count):
            if x < width and z < depth:
                tiles[x][z] = value
                z += 1
                if z >= depth:
                    z = 0
                    x += 1

    return tiles

def find_floor_positions(tiles: list, width: int, depth: int) -> list:
    """Find all floor positions (value 1)."""
    positions = []
    for x in range(width):
        for z in range(depth):
            if tiles[x][z] == 1:
                positions.append((x, z))
    return positions

def distance_from_spawn(pos, spawn_pos):
    """Calculate distance from spawn point."""
    return ((pos[0] - spawn_pos[0])**2 + (pos[1] - spawn_pos[1])**2) ** 0.5

def get_tier_for_distance(dist: float) -> int:
    """Get monster tier based on distance from spawn."""
    if dist < 30: return 1
    elif dist < 60: return 2
    elif dist < 100: return 3
    elif dist < 150: return 4
    else: return 5

def is_position_clear(pos, existing_positions, min_dist=3):
    """Check if position is clear of other objects."""
    for existing in existing_positions:
        if distance_from_spawn(pos, existing) < min_dist:
            return False
    return True

def populate_map(input_path: str, output_path: str):
    """Main function to populate a map."""
    print(f"Loading map from {input_path}")

    with open(input_path, 'r') as f:
        map_data = json.load(f)

    width = map_data.get('width', 100)
    depth = map_data.get('depth', 100)
    spawn_pos = (
        map_data.get('spawnPosition', {}).get('x', width // 2),
        map_data.get('spawnPosition', {}).get('z', depth // 2)
    )

    print(f"Map size: {width}x{depth}, Spawn: {spawn_pos}")

    # Decode tiles
    tile_data = map_data.get('tileData', '')
    if not tile_data:
        print("No tile data found!")
        return

    tiles = decode_tile_data(tile_data, width, depth)
    floor_positions = find_floor_positions(tiles, width, depth)
    print(f"Found {len(floor_positions)} floor tiles")

    # Track placed object positions
    placed_positions = list(spawn_pos)  # Start with spawn

    # Add existing enemy positions
    for enemy in map_data.get('enemies', []):
        placed_positions.append((enemy['position']['x'], enemy['position']['z']))

    new_enemies = []
    new_props = []

    # Randomly sample floor positions for monster/prop placement
    random.shuffle(floor_positions)

    # Place monsters - about 1 per 100 tiles
    target_monsters = len(floor_positions) // 100
    monsters_placed = 0

    for pos in floor_positions:
        if monsters_placed >= target_monsters:
            break

        dist = distance_from_spawn(pos, spawn_pos)

        # Skip positions too close to spawn (safe zone)
        if dist < 15:
            continue

        # Check if position is clear
        if not is_position_clear(pos, placed_positions, min_dist=5):
            continue

        tier = get_tier_for_distance(dist)

        # Select monster based on tier
        if tier == 1:
            monster_type = random.choice(TIER1_MONSTERS)
        elif tier == 2:
            monster_type = random.choice(TIER2_MONSTERS)
        elif tier == 3:
            monster_type = random.choice(TIER3_MONSTERS)
        elif tier == 4:
            monster_type = random.choice(TIER4_MONSTERS)
        else:
            monster_type = random.choice(TIER5_MONSTERS)

        # Occasionally spawn boss in far areas
        is_boss = False
        if dist > 120 and random.random() < 0.02:
            monster_type = random.choice(BOSSES)
            is_boss = True

        new_enemies.append({
            "type": monster_type,
            "roomId": -1,
            "position": {"x": pos[0], "z": pos[1]},
            "level": max(1, tier),
            "isBoss": is_boss,
            "rotationY": random.uniform(0, 6.28)
        })

        placed_positions.append(pos)
        monsters_placed += 1

    print(f"Added {monsters_placed} monsters")

    # Place props - about 1 per 50 tiles
    target_props = len(floor_positions) // 50
    props_placed = 0

    for pos in floor_positions:
        if props_placed >= target_props:
            break

        dist = distance_from_spawn(pos, spawn_pos)

        # Check if position is clear
        if not is_position_clear(pos, placed_positions, min_dist=2):
            continue

        # Select prop based on distance (more treasure/spooky far from spawn)
        if dist < 30:
            props = DUNGEON_PROPS + CAMP_PROPS
        elif dist < 80:
            props = DUNGEON_PROPS + NATURE_PROPS
        elif dist < 130:
            props = DUNGEON_PROPS + SPOOKY_PROPS + TREASURE_PROPS
        else:
            props = SPOOKY_PROPS + TREASURE_PROPS

        prop_type = random.choice(props)

        new_props.append({
            "type": prop_type,
            "x": float(pos[0]) + random.uniform(-0.3, 0.3),
            "y": 0.0,
            "z": float(pos[1]) + random.uniform(-0.3, 0.3),
            "rotationY": random.uniform(0, 6.28),
            "scale": random.uniform(0.8, 1.2)
        })

        placed_positions.append(pos)
        props_placed += 1

    print(f"Added {props_placed} props")

    # Add monster groups (clusters of related monsters)
    groups_to_add = min(20, len(floor_positions) // 500)
    groups_placed = 0

    for pos in floor_positions[::50]:  # Sample every 50th position
        if groups_placed >= groups_to_add:
            break

        dist = distance_from_spawn(pos, spawn_pos)
        if dist < 40 or not is_position_clear(pos, placed_positions, min_dist=10):
            continue

        # Create a small cluster of 3-5 monsters
        cluster_size = random.randint(3, 5)
        tier = get_tier_for_distance(dist)

        if tier <= 2:
            monster_pool = TIER1_MONSTERS + TIER2_MONSTERS
        elif tier <= 3:
            monster_pool = TIER2_MONSTERS + TIER3_MONSTERS
        else:
            monster_pool = TIER3_MONSTERS + TIER4_MONSTERS

        for i in range(cluster_size):
            offset_x = random.uniform(-4, 4)
            offset_z = random.uniform(-4, 4)
            monster_pos = (int(pos[0] + offset_x), int(pos[1] + offset_z))

            if monster_pos[0] < 0 or monster_pos[0] >= width:
                continue
            if monster_pos[1] < 0 or monster_pos[1] >= depth:
                continue
            if tiles[monster_pos[0]][monster_pos[1]] != 1:
                continue

            new_enemies.append({
                "type": random.choice(monster_pool),
                "roomId": -1,
                "position": {"x": monster_pos[0], "z": monster_pos[1]},
                "level": max(1, tier),
                "isBoss": False,
                "rotationY": random.uniform(0, 6.28)
            })

        placed_positions.append(pos)
        groups_placed += 1

    print(f"Added {groups_placed} monster groups")

    # Merge with existing data
    map_data['enemies'] = map_data.get('enemies', []) + new_enemies
    map_data['placedProps'] = map_data.get('placedProps', []) + new_props
    map_data['name'] = map_data.get('name', 'Map') + ' (Populated)'

    # Save
    with open(output_path, 'w') as f:
        json.dump(map_data, f, indent=2)

    print(f"Saved populated map to {output_path}")
    print(f"Total enemies: {len(map_data['enemies'])}")
    print(f"Total props: {len(map_data['placedProps'])}")

if __name__ == '__main__':
    input_map = Path('C:/Claude/SafeRoom3D/maps/Steves Place.json')
    output_map = Path('C:/Claude/SafeRoom3D/maps/Steves Place (Populated).json')

    populate_map(str(input_map), str(output_map))
