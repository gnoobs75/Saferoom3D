#!/usr/bin/env python3
"""
Script to replace skeleton mesh methods in MonsterMeshFactory.cs
"""

def replace_method(lines, start_marker, end_marker, new_content):
    """Replace a method between start and end markers"""
    start_idx = -1
    end_idx = -1

    for i, line in enumerate(lines):
        if start_marker in line:
            start_idx = i
        if start_idx != -1 and end_marker in line and i > start_idx:
            end_idx = i + 1
            break

    if start_idx == -1 or end_idx == -1:
        print(f"Could not find method markers: {start_marker}")
        return lines

    print(f"Replacing lines {start_idx+1} to {end_idx}")
    return lines[:start_idx] + new_content + lines[end_idx:]

def main():
    # Read the original file
    with open(r'C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory.cs', 'r', encoding='utf-8') as f:
        lines = f.readlines()

    print(f"Original file has {len(lines)} lines")

    # Read the replacement content
    with open(r'C:\Claude\SafeRoom3D\skeleton_enhanced.txt', 'r', encoding='utf-8') as f:
        skeleton_content = f.readlines()

    with open(r'C:\Claude\SafeRoom3D\skeleton_lord_enhanced.txt', 'r', encoding='utf-8') as f:
        skeleton_lord_content = f.readlines()

    # Replace CreateSkeletonMesh and helpers
    # Find CreateSkeletonArm first (it comes after CreateSkeletonMesh)
    lines = replace_method(
        lines,
        "    private static void CreateSkeletonMesh(Node3D parent, LimbNodes limbs)",
        "    private static void CreateSkeletonArm(Node3D parent, LimbNodes limbs",
        skeleton_content
    )

    # Replace CreateSkeletonLordMesh and helper
    lines = replace_method(
        lines,
        "    private static void CreateSkeletonLordMesh(Node3D parent, LimbNodes limbs)",
        "    private static void CreateDragonKingMesh(Node3D parent, LimbNodes limbs)",
        skeleton_lord_content
    )

    # Write the modified file
    with open(r'C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory.cs', 'w', encoding='utf-8') as f:
        f.writelines(lines)

    print(f"Modified file has {len(lines)} lines")
    print("Replacement complete!")

if __name__ == "__main__":
    main()
