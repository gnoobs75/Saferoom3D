# Safe Room 3D Launcher
# Automatically selects OpenGL for Intel/AMD integrated GPUs, Vulkan for NVIDIA

$gpu = Get-WmiObject Win32_VideoController | Select-Object -First 1 -ExpandProperty Name

Write-Host "Detected GPU: $gpu"

$godotPath = "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe"
$projectPath = "C:\Claude\SafeRoom3D"

if ($gpu -match "NVIDIA") {
    Write-Host "Using Vulkan renderer (NVIDIA detected)"
    Start-Process $godotPath -ArgumentList "--path", $projectPath
} else {
    Write-Host "Using OpenGL renderer (Intel/AMD integrated detected)"
    Start-Process $godotPath -ArgumentList "--path", $projectPath, "--rendering-driver", "opengl3"
}
