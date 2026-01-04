import re

# Read the original file
with open(r'C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Read the new methods
with open(r'C:\Claude\SafeRoom3D\mushroom_methods_temp.cs', 'r', encoding='utf-8') as f:
    new_methods = f.read()

# Find and replace the old CreateMushroomMesh method
# Pattern: from method start to the closing brace before the next /// comment or next private static method
pattern = r'    private static void CreateMushroomMesh\(Node3D parent, LimbNodes limbs, Color\? colorOverride\)[\s\S]*?^    \}'

# Find the method
match = re.search(pattern, content, re.MULTILINE)
if match:
    print(f"Found CreateMushroomMesh method at position {match.start()}-{match.end()}")
    print(f"Method starts with: {match.group()[:100]}...")

    # Replace with new methods
    new_content = content[:match.start()] + new_methods.rstrip() + content[match.end():]

    # Write back
    with open(r'C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory.cs', 'w', encoding='utf-8') as f:
        f.write(new_content)

    print("Successfully replaced CreateMushroomMesh and added CreateSporelingElderMesh")
else:
    print("ERROR: Could not find CreateMushroomMesh method")
