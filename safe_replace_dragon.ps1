# Safely replace CreateDragonMesh method without affecting other methods
$origFile = "C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory.cs"
$enhanced = "C:\Claude\SafeRoom3D\DragonMeshEnhanced.txt"
$output = "C:\Claude\SafeRoom3D\Scripts\Enemies\MonsterMeshFactory_new.cs"

Write-Host "Reading files..."
$content = Get-Content $origFile -Raw
$enhancedContent = Get-Content $enhanced -Raw

# Find CreateDragonMesh method start and end
# Pattern: "private static void CreateDragonMesh(Node3D parent, LimbNodes limbs)"  to the closing "}"
# We need to find the method and replace it

# Use regex to find and replace the method
# Match from "private static void CreateDragonMesh" to the method's closing brace
# This is complex because we need to count braces

$lines = Get-Content $origFile
$enhancedLines = Get-Content $enhanced

# Find start line of CreateDragonMesh
$startLine = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*private static void CreateDragonMesh\(Node3D parent, LimbNodes limbs\)') {
        $startLine = $i
        Write-Host "Found CreateDragonMesh at line $($i + 1)"
        break
    }
}

if ($startLine -eq -1) {
    Write-Host "ERROR: Could not find CreateDragonMesh method!"
    exit 1
}

# Find the end of the method by counting braces
$braceCount = 0
$inMethod = $false
$endLine = -1

for ($i = $startLine; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]

    # Count opening braces
    $openBraces = ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closeBraces = ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count

    if ($openBraces -gt 0) {
        $inMethod = $true
    }

    $braceCount += $openBraces
    $braceCount -= $closeBraces

    if ($inMethod -and $braceCount -eq 0) {
        $endLine = $i
        Write-Host "Method ends at line $($i + 1)"
        break
    }
}

if ($endLine -eq -1) {
    Write-Host "ERROR: Could not find end of CreateDragonMesh method!"
    exit 1
}

# Build the new file
$newLines = @()

# Add everything before the method
$newLines += $lines[0..($startLine - 1)]

# Add enhanced method
$newLines += $enhancedLines

# Add everything after the method
if ($endLine + 1 -lt $lines.Count) {
    $newLines += $lines[($endLine + 1)..($lines.Count - 1)]
}

Write-Host "Writing new file..."
$newLines | Set-Content $output -Encoding UTF8

Write-Host "Done! Lines: Original=$($lines.Count), New=$($newLines.Count), Replaced=$($endLine - $startLine + 1), Enhanced=$($enhancedLines.Count)"
