using Godot;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SafeRoom3D.Core;

/// <summary>
/// Performance monitoring overlay that displays FPS, frame times, and system stats.
/// Press P to toggle visibility.
/// </summary>
public partial class PerformanceMonitor : CanvasLayer
{
    public static PerformanceMonitor? Instance { get; private set; }

    private Label? _statsLabel;
    private bool _visible = false; // Start hidden

    // Frame timing
    private readonly Queue<double> _frameTimes = new();
    private readonly Queue<double> _physicsTimes = new();
    private const int SampleCount = 60;

    // Per-system timing
    private static readonly Dictionary<string, double> _systemTimes = new();
    private static readonly Dictionary<string, int> _systemCounts = new();
    private static Stopwatch _stopwatch = new();

    // Update interval
    private float _updateTimer;
    private const float UpdateInterval = 0.5f; // Update display every 0.5s

    public override void _Ready()
    {
        Instance = this;
        Layer = 100; // On top of everything

        _statsLabel = new Label();
        _statsLabel.Position = new Vector2(10, 10);
        _statsLabel.AddThemeColorOverride("font_color", Colors.Lime);
        _statsLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _statsLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_statsLabel);

        // Add background panel for readability
        var panel = new Panel();
        panel.Position = new Vector2(5, 5);
        panel.Size = new Vector2(350, 400);
        panel.Modulate = new Color(0, 0, 0, 0.7f);
        panel.ZIndex = -1;
        AddChild(panel);
        panel.MoveToFront();
        _statsLabel.MoveToFront();

        // Start hidden
        Visible = false;
        GD.Print("[PerformanceMonitor] Initialized - Press P to toggle");
    }

    public override void _Input(InputEvent @event)
    {
        // Toggle visibility with P key (using toggle_debug action)
        if (@event.IsActionPressed("toggle_debug"))
        {
            _visible = !_visible;
            Visible = _visible;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {

        // Track frame time
        _frameTimes.Enqueue(delta * 1000.0); // Convert to ms
        while (_frameTimes.Count > SampleCount)
            _frameTimes.Dequeue();

        // Update display periodically
        _updateTimer -= (float)delta;
        if (_updateTimer <= 0 && _visible)
        {
            _updateTimer = UpdateInterval;
            UpdateDisplay();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _physicsTimes.Enqueue(delta * 1000.0);
        while (_physicsTimes.Count > SampleCount)
            _physicsTimes.Dequeue();
    }

    private void UpdateDisplay()
    {
        if (_statsLabel == null) return;

        var sb = new System.Text.StringBuilder();

        // FPS and frame times
        double avgFrameTime = _frameTimes.Count > 0 ? _frameTimes.Average() : 0;
        double maxFrameTime = _frameTimes.Count > 0 ? _frameTimes.Max() : 0;
        double minFrameTime = _frameTimes.Count > 0 ? _frameTimes.Min() : 0;
        double fps = avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0;

        sb.AppendLine($"=== PERFORMANCE MONITOR (P to hide) ===");
        sb.AppendLine($"FPS: {fps:F1} ({1000.0/maxFrameTime:F0} - {1000.0/minFrameTime:F0})");
        sb.AppendLine($"Frame: {avgFrameTime:F2}ms (max: {maxFrameTime:F2}ms)");
        sb.AppendLine();

        // Godot built-in stats
        sb.AppendLine("=== RENDERING ===");
        sb.AppendLine($"Draw Calls: {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}");
        sb.AppendLine($"Objects: {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)}");
        sb.AppendLine($"Primitives: {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)}");
        sb.AppendLine($"Video Mem: {Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1024 / 1024:F1} MB");
        sb.AppendLine();

        // Physics - using available Godot 4.3 monitors
        sb.AppendLine("=== PHYSICS ===");
        sb.AppendLine($"3D Active: {Performance.GetMonitor(Performance.Monitor.Physics3DActiveObjects)}");
        sb.AppendLine($"3D Pairs: {Performance.GetMonitor(Performance.Monitor.Physics3DCollisionPairs)}");
        sb.AppendLine();

        // Memory
        sb.AppendLine("=== MEMORY ===");
        sb.AppendLine($"Static Mem: {Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1024 / 1024:F1} MB");
        sb.AppendLine($"Objects: {Performance.GetMonitor(Performance.Monitor.ObjectCount)}");
        sb.AppendLine($"Resources: {Performance.GetMonitor(Performance.Monitor.ObjectResourceCount)}");
        sb.AppendLine($"Nodes: {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
        sb.AppendLine($"Orphan Nodes: {Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)}");
        sb.AppendLine();

        // Game-specific stats
        sb.AppendLine("=== GAME ===");
        var tree = GetTree();
        if (tree != null)
        {
            var enemies = tree.GetNodesInGroup("Enemies");
            int activeEnemies = 0;
            int sleepingEnemies = 0;
            foreach (var node in enemies)
            {
                if (node is SafeRoom3D.Enemies.BasicEnemy3D enemy)
                {
                    if (enemy.CurrentState == SafeRoom3D.Enemies.BasicEnemy3D.State.Sleeping)
                        sleepingEnemies++;
                    else
                        activeEnemies++;
                }
            }
            sb.AppendLine($"Enemies: {activeEnemies} active, {sleepingEnemies} sleeping");
        }
        sb.AppendLine();

        // Custom system timings
        if (_systemTimes.Count > 0)
        {
            sb.AppendLine("=== SYSTEM TIMES ===");
            foreach (var kvp in _systemTimes.OrderByDescending(x => x.Value))
            {
                int count = _systemCounts.GetValueOrDefault(kvp.Key, 1);
                sb.AppendLine($"{kvp.Key}: {kvp.Value:F2}ms (x{count})");
            }
        }

        _statsLabel.Text = sb.ToString();

        // Reset system timings for next interval
        _systemTimes.Clear();
        _systemCounts.Clear();
    }

    /// <summary>
    /// Start timing a system. Call EndTiming with the same name when done.
    /// </summary>
    public static void StartTiming(string systemName)
    {
        _stopwatch.Restart();
    }

    /// <summary>
    /// End timing and record the duration.
    /// </summary>
    public static void EndTiming(string systemName)
    {
        _stopwatch.Stop();
        double elapsed = _stopwatch.Elapsed.TotalMilliseconds;

        if (_systemTimes.ContainsKey(systemName))
        {
            _systemTimes[systemName] += elapsed;
            _systemCounts[systemName]++;
        }
        else
        {
            _systemTimes[systemName] = elapsed;
            _systemCounts[systemName] = 1;
        }
    }

    /// <summary>
    /// Record a timing directly without using stopwatch.
    /// </summary>
    public static void RecordTiming(string systemName, double milliseconds, int count = 1)
    {
        if (_systemTimes.ContainsKey(systemName))
        {
            _systemTimes[systemName] += milliseconds;
            _systemCounts[systemName] += count;
        }
        else
        {
            _systemTimes[systemName] = milliseconds;
            _systemCounts[systemName] = count;
        }
    }
}
