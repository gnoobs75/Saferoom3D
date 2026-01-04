# Replace CreateDragonMesh method with enhanced version
$origFile = "C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory.cs"
$enhanced = "C:\Claude\SafeRoom3D\DragonMeshEnhanced.txt"
$output = "C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory_temp.cs"

Write-Host "Reading original file..."
$lines = Get-Content $origFile

Write-Host "Reading enhanced dragon method..."
$enhancedLines = Get-Content $enhanced

Write-Host "Extracting sections..."
# Take lines before CreateDragonMesh (lines 1-2102, so index 0-2101)
$before = $lines[0..2101]
Write-Host "Before section: $($before.Count) lines"

# Take lines after CreateDragonMesh (line 2400 onwards, so index 2399+)
$after = $lines[2399..($lines.Count - 1)]
Write-Host "After section: $($after.Count) lines"
Write-Host "Enhanced method: $($enhancedLines.Count) lines"

Write-Host "Combining sections..."
$combined = @()
$combined += $before
$combined += ""  # Blank line
$combined += $enhancedLines
$combined += ""  # Blank line
$combined += $after

Write-Host "Writing output file with $($combined.Count) total lines..."
$combined | Set-Content $output -Encoding UTF8

Write-Host "Temp file created successfully!"
Write-Host "Original: $($lines.Count) lines"
Write-Host "New: $($combined.Count) lines"
