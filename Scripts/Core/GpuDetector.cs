using Godot;
using System.IO;

namespace SafeRoom3D.Core;

/// <summary>
/// Detects Intel integrated graphics and relaunches with OpenGL3 for stability.
/// Intel Iris Xe Vulkan drivers crash during UI rendering.
/// </summary>
public static class GpuDetector
{
    private static bool _checked = false;
    private static string MarkerPath => Path.Combine(
        OS.GetUserDataDir(), "opengl_relaunch_marker.txt");

    /// <summary>
    /// Call at game start. Returns true if relaunch is happening (caller should return early).
    /// </summary>
    public static bool CheckAndRelaunchIfNeeded(Node caller)
    {
        if (_checked) return false;
        _checked = true;

        // Check if marker file exists (we already relaunched)
        if (File.Exists(MarkerPath))
        {
            GD.Print("[GpuDetector] Marker found - already relaunched with OpenGL");
            // Clean up marker for next cold start
            try { File.Delete(MarkerPath); } catch { }
            return false;
        }

        string adapterName = RenderingServer.GetVideoAdapterName();
        GD.Print($"[GpuDetector] GPU: {adapterName}");

        // Check for Intel integrated graphics
        string nameLower = adapterName.ToLowerInvariant();
        bool isIntelIntegrated = nameLower.Contains("intel") &&
            (nameLower.Contains("iris") || nameLower.Contains("uhd") || nameLower.Contains("hd graphics"));

        if (!isIntelIntegrated)
        {
            GD.Print("[GpuDetector] Not Intel integrated, using Vulkan");
            return false;
        }

        GD.Print("[GpuDetector] Intel integrated detected - switching to OpenGL3...");

        // Get paths
        string exePath = OS.GetExecutablePath();
        string projectPath = ProjectSettings.GlobalizePath("res://").TrimEnd('/');

        // Build args
        string[] args = new string[] { "--path", projectPath, "--rendering-driver", "opengl3" };

        try
        {
            // Create marker file so the relaunched process knows not to relaunch again
            File.WriteAllText(MarkerPath, "OpenGL relaunch marker");
            GD.Print($"[GpuDetector] Created marker at: {MarkerPath}");

            // Use cmd /c start to properly detach the process (same as batch file)
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" \"{exePath}\" --path \"{projectPath}\" --rendering-driver opengl3",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            GD.Print($"[GpuDetector] Launching: {startInfo.Arguments}");

            System.Diagnostics.Process.Start(startInfo);

            // Quit immediately - the new process is now detached
            caller.GetTree().Quit(0);

            return true;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[GpuDetector] Failed: {ex.Message}");
            return false;
        }
    }
}
