@echo off
echo Backing up original file...
copy "Scripts\Enemies\MonsterMeshFactory.cs" "Scripts\Enemies\MonsterMeshFactory.cs.backup"

echo Creating replacement file...
powershell -Command "$content = Get-Content 'Scripts\Enemies\MonsterMeshFactory.cs.backup' -Raw; $newMethods = Get-Content 'C:\Claude\SafeRoom3D\mushroom_methods_temp.cs' -Raw; $pattern = '(?s)    private static void CreateMushroomMesh\(Node3D parent, LimbNodes limbs, Color\? colorOverride\).*?^    \}'; $content = $content -replace $pattern, $newMethods.TrimEnd(); $content | Set-Content 'Scripts\Enemies\MonsterMeshFactory.cs'"

echo Done!
