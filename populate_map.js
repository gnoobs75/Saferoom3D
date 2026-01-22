#!/usr/bin/env node
/**
 * Populate a SafeRoom3D map with aesthetically placed monsters and props.
 * Uses the map's tile data to intelligently place content.
 */

const fs = require('fs');
const zlib = require('zlib');
const path = require('path');

// Monster types by difficulty tier
const TIER1_MONSTERS = ["dungeon_rat", "slime", "goblin"];
const TIER2_MONSTERS = ["goblin_shaman", "goblin_thrower", "spider", "mushroom", "bat"];
const TIER3_MONSTERS = ["skeleton", "wolf", "lizard", "eye", "badlama"];
const TIER4_MONSTERS = ["crawler_killer", "shadow_stalker", "mimic", "flesh_golem"];
const TIER5_MONSTERS = ["living_armor", "plague_bearer", "lava_elemental", "void_spawn"];

const BOSSES = ["skeleton_lord", "dragon_king", "spider_queen", "the_butcher", "mordecai", "mongo"];

// Prop types by theme
const DUNGEON_PROPS = ["barrel", "crate", "pot", "torch", "bone_pile", "skull_pile", "rubble_heap"];
const TREASURE_PROPS = ["chest", "treasure_chest", "scattered_coins", "ancient_scroll"];
const SPOOKY_PROPS = ["blood_pool", "coiled_chains", "manacles", "discarded_sword", "forgotten_shield"];
const NATURE_PROPS = ["moss_patch", "glowing_mushrooms", "water_puddle", "thorny_vines"];
const CAMP_PROPS = ["campfire", "abandoned_campfire", "rat_nest", "moldy_bread"];

function decodeTileData(tileDataBase64, width, depth) {
    // Decode base64
    const compressed = Buffer.from(tileDataBase64, 'base64');
    // Decompress gzip
    const decompressed = zlib.gunzipSync(compressed);

    // Initialize tiles array
    const tiles = Array.from({ length: width }, () => Array(depth).fill(0));

    if (decompressed.length < 8) return tiles;

    // Read header (stored width and depth as little-endian int32)
    const storedWidth = decompressed.readInt32LE(0);
    const storedDepth = decompressed.readInt32LE(4);
    console.log(`Stored dimensions: ${storedWidth}x${storedDepth}`);

    // RLE decode starting after 8-byte header
    let dataIndex = 8;
    let x = 0, z = 0;

    while (dataIndex < decompressed.length && z < depth) {
        const firstByte = decompressed[dataIndex++];
        let value, count;

        if ((firstByte & 0x80) !== 0) {
            // Multi-byte run: marker byte (bit 7 = 1) + 2-byte count
            value = firstByte & 0x01;
            if (dataIndex + 1 >= decompressed.length) break;
            count = decompressed[dataIndex] | (decompressed[dataIndex + 1] << 8);
            dataIndex += 2;
        } else {
            // Single byte run: bit 0 = value, bits 1-6 = count
            value = firstByte & 0x01;
            count = firstByte >> 1;
        }

        // Apply the run
        for (let i = 0; i < count && z < depth; i++) {
            if (x < width) {
                tiles[x][z] = value;
            }
            x++;
            if (x >= storedWidth) {
                x = 0;
                z++;
            }
        }
    }

    return tiles;
}

function findFloorPositions(tiles, width, depth) {
    const positions = [];
    for (let x = 0; x < width; x++) {
        for (let z = 0; z < depth; z++) {
            if (tiles[x][z] === 1) {
                positions.push([x, z]);
            }
        }
    }
    return positions;
}

function distance(pos1, pos2) {
    return Math.sqrt(Math.pow(pos1[0] - pos2[0], 2) + Math.pow(pos1[1] - pos2[1], 2));
}

function getTierForDistance(dist) {
    if (dist < 30) return 1;
    if (dist < 60) return 2;
    if (dist < 100) return 3;
    if (dist < 150) return 4;
    return 5;
}

function isPositionClear(pos, existingPositions, minDist = 3) {
    for (const existing of existingPositions) {
        if (distance(pos, existing) < minDist) {
            return false;
        }
    }
    return true;
}

function randomChoice(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

function shuffle(array) {
    const arr = [...array];
    for (let i = arr.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [arr[i], arr[j]] = [arr[j], arr[i]];
    }
    return arr;
}

function populateMap(inputPath, outputPath) {
    console.log(`Loading map from ${inputPath}`);

    const mapData = JSON.parse(fs.readFileSync(inputPath, 'utf8'));

    const width = mapData.width || 100;
    const depth = mapData.depth || 100;
    const spawnPos = [
        mapData.spawnPosition?.x || Math.floor(width / 2),
        mapData.spawnPosition?.z || Math.floor(depth / 2)
    ];

    console.log(`Map size: ${width}x${depth}, Spawn: (${spawnPos[0]}, ${spawnPos[1]})`);

    // Decode tiles
    const tileData = mapData.tileData || '';
    if (!tileData) {
        console.log("No tile data found!");
        return;
    }

    const tiles = decodeTileData(tileData, width, depth);
    let floorPositions = findFloorPositions(tiles, width, depth);
    console.log(`Found ${floorPositions.length} floor tiles`);

    // Track placed object positions
    const placedPositions = [[...spawnPos]];

    // Add existing enemy positions
    for (const enemy of (mapData.enemies || [])) {
        placedPositions.push([enemy.position.x, enemy.position.z]);
    }

    const newEnemies = [];
    const newProps = [];

    // Shuffle floor positions
    floorPositions = shuffle(floorPositions);

    // Place monsters - about 1 per 100 tiles
    const targetMonsters = Math.floor(floorPositions.length / 100);
    let monstersPlaced = 0;

    for (const pos of floorPositions) {
        if (monstersPlaced >= targetMonsters) break;

        const dist = distance(pos, spawnPos);

        // Skip positions too close to spawn (safe zone)
        if (dist < 15) continue;

        // Check if position is clear
        if (!isPositionClear(pos, placedPositions, 5)) continue;

        const tier = getTierForDistance(dist);

        // Select monster based on tier
        let monsterType;
        if (tier === 1) monsterType = randomChoice(TIER1_MONSTERS);
        else if (tier === 2) monsterType = randomChoice(TIER2_MONSTERS);
        else if (tier === 3) monsterType = randomChoice(TIER3_MONSTERS);
        else if (tier === 4) monsterType = randomChoice(TIER4_MONSTERS);
        else monsterType = randomChoice(TIER5_MONSTERS);

        // Occasionally spawn boss in far areas
        let isBoss = false;
        if (dist > 120 && Math.random() < 0.02) {
            monsterType = randomChoice(BOSSES);
            isBoss = true;
        }

        newEnemies.push({
            type: monsterType,
            roomId: -1,
            position: { x: pos[0], z: pos[1] },
            level: Math.max(1, tier),
            isBoss: isBoss,
            rotationY: Math.random() * 6.28
        });

        placedPositions.push(pos);
        monstersPlaced++;
    }

    console.log(`Added ${monstersPlaced} monsters`);

    // Place props - about 1 per 50 tiles
    const targetProps = Math.floor(floorPositions.length / 50);
    let propsPlaced = 0;

    for (const pos of floorPositions) {
        if (propsPlaced >= targetProps) break;

        const dist = distance(pos, spawnPos);

        // Check if position is clear
        if (!isPositionClear(pos, placedPositions, 2)) continue;

        // Select prop based on distance
        let props;
        if (dist < 30) props = [...DUNGEON_PROPS, ...CAMP_PROPS];
        else if (dist < 80) props = [...DUNGEON_PROPS, ...NATURE_PROPS];
        else if (dist < 130) props = [...DUNGEON_PROPS, ...SPOOKY_PROPS, ...TREASURE_PROPS];
        else props = [...SPOOKY_PROPS, ...TREASURE_PROPS];

        const propType = randomChoice(props);

        newProps.push({
            type: propType,
            x: pos[0] + (Math.random() - 0.5) * 0.6,
            y: 0.0,
            z: pos[1] + (Math.random() - 0.5) * 0.6,
            rotationY: Math.random() * 6.28,
            scale: 0.8 + Math.random() * 0.4
        });

        placedPositions.push(pos);
        propsPlaced++;
    }

    console.log(`Added ${propsPlaced} props`);

    // Add monster groups (clusters of related monsters)
    const groupsToAdd = Math.min(20, Math.floor(floorPositions.length / 500));
    let groupsPlaced = 0;

    for (let i = 0; i < floorPositions.length && groupsPlaced < groupsToAdd; i += 50) {
        const pos = floorPositions[i];
        const dist = distance(pos, spawnPos);

        if (dist < 40 || !isPositionClear(pos, placedPositions, 10)) continue;

        // Create a small cluster of 3-5 monsters
        const clusterSize = 3 + Math.floor(Math.random() * 3);
        const tier = getTierForDistance(dist);

        let monsterPool;
        if (tier <= 2) monsterPool = [...TIER1_MONSTERS, ...TIER2_MONSTERS];
        else if (tier <= 3) monsterPool = [...TIER2_MONSTERS, ...TIER3_MONSTERS];
        else monsterPool = [...TIER3_MONSTERS, ...TIER4_MONSTERS];

        for (let c = 0; c < clusterSize; c++) {
            const offsetX = (Math.random() - 0.5) * 8;
            const offsetZ = (Math.random() - 0.5) * 8;
            const monsterX = Math.floor(pos[0] + offsetX);
            const monsterZ = Math.floor(pos[1] + offsetZ);

            if (monsterX < 0 || monsterX >= width) continue;
            if (monsterZ < 0 || monsterZ >= depth) continue;
            if (tiles[monsterX][monsterZ] !== 1) continue;

            newEnemies.push({
                type: randomChoice(monsterPool),
                roomId: -1,
                position: { x: monsterX, z: monsterZ },
                level: Math.max(1, tier),
                isBoss: false,
                rotationY: Math.random() * 6.28
            });
        }

        placedPositions.push(pos);
        groupsPlaced++;
    }

    console.log(`Added ${groupsPlaced} monster groups`);

    // Merge with existing data
    mapData.enemies = [...(mapData.enemies || []), ...newEnemies];
    mapData.placedProps = [...(mapData.placedProps || []), ...newProps];
    mapData.name = (mapData.name || 'Map') + ' (Populated)';

    // Save
    fs.writeFileSync(outputPath, JSON.stringify(mapData, null, 2));

    console.log(`Saved populated map to ${outputPath}`);
    console.log(`Total enemies: ${mapData.enemies.length}`);
    console.log(`Total props: ${mapData.placedProps.length}`);
}

// Run
const inputMap = path.join('C:/Claude/SafeRoom3D/maps', 'Steves Place.json');
const outputMap = path.join('C:/Claude/SafeRoom3D/maps', 'Steves Place (Populated).json');

populateMap(inputMap, outputMap);
