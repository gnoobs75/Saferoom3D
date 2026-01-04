$file = 'Scripts/Enemies/MonsterMeshFactory.cs'
$enhanced = Get-Content 'enhanced_slime_methods.cs' -Raw
$content = Get-Content $file -Raw

# Find the start and end positions
$startMarker = '    private static void CreateSlimeMesh(Node3D parent, LimbNodes limbs, Color? skinColorOverride)'
$endMarker = '    private static void CreateSkeletonMesh(Node3D parent, LimbNodes limbs)'

$startIndex = $content.IndexOf($startMarker)
$endIndex = $content.IndexOf($endMarker)

if ($startIndex -eq -1 -or $endIndex -eq -1) {
    Write-Host 'Could not find method boundaries'
    Write-Host "Start index: $startIndex"
    Write-Host "End index: $endIndex"
    exit 1
}

Write-Host "Start index: $startIndex"
Write-Host "End index: $endIndex"

# Extract parts
$before = $content.Substring(0, $startIndex)
$after = $content.Substring($endIndex)

# Combine
$newContent = $before + $enhanced + "`r`n`r`n" + $after

# Write back
Set-Content -Path $file -Value $newContent -NoNewline
Write-Host 'File updated successfully'
