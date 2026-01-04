#!/usr/bin/env python3

# Read the files
with open('Scripts/Enemies/MonsterMeshFactory.cs', 'r', encoding='utf-8') as f:
    content = f.read()

with open('enhanced_slime_methods.cs', 'r', encoding='utf-8') as f:
    enhanced = f.read()

# Find the start and end indices
start_marker = "    private static void CreateSlimeMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride)"
end_marker = "    private static void CreateSkeletonMesh(Node3D parent, LimbNodes limbs)"

start_idx = content.find(start_marker)
end_idx = content.find(end_marker)

if start_idx == -1 or end_idx == -1:
    print(f"Error: Could not find method boundaries")
    print(f"Start index: {start_idx}")
    print(f"End index: {end_idx}")
    exit(1)

# Replace the section
before = content[:start_idx]
after = content[end_idx:]

new_content = before + enhanced + "\n\n" + after

# Write back
with open('Scripts/Enemies/MonsterMeshFactory.cs', 'w', encoding='utf-8') as f:
    f.write(new_content)

print("File updated successfully!")
print(f"Replaced {end_idx - start_idx} characters")
