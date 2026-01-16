using Godot;
using SafeRoom3D.Player;
using SafeRoom3D.Broadcaster;

namespace SafeRoom3D.Core;

/// <summary>
/// Cinematic kill cam that triggers randomly on monster kills.
/// Shows slow-motion camera pan with AI commentary.
/// </summary>
public partial class KillCam3D : Node3D
{
    public static KillCam3D? Instance { get; private set; }

    // Configuration
    private const float TriggerChance = 0.05f;  // 5%
    private const float SlowMotionScale = 0.2f; // 20% speed
    private const float Duration = 4.5f;

    // State
    private bool _isActive;
    private float _timer;
    private Camera3D? _killCam;
    private Camera3D? _originalCamera;
    private Vector3 _victimPosition;
    private string _victimName = "";

    /// <summary>
    /// Whether kill cam is currently active
    /// </summary>
    public bool IsActive => _isActive;

    public override void _Ready()
    {
        Instance = this;
        Name = "KillCam3D";

        // Subscribe to monster kills
        GameEvents.MonsterKilled += OnMonsterKilled;

        GD.Print("[KillCam3D] Initialized - 5% chance on monster kills");
    }

    public override void _ExitTree()
    {
        GameEvents.MonsterKilled -= OnMonsterKilled;

        if (Instance == this)
            Instance = null;
    }

    private void OnMonsterKilled(string monsterType, int xp, Vector3 position)
    {
        // Skip if already showing kill cam
        if (_isActive) return;

        // Skip if player is dead or doesn't exist
        if (FPSController.Instance == null) return;
        if (FPSController.Instance.CurrentHealth <= 0) return;

        // Skip if in edit mode
        if (GameManager3D.IsEditMode) return;

        // Skip if game is paused
        if (GameManager3D.Instance?.IsPaused == true) return;

        // 5% chance to trigger
        if (GD.Randf() > TriggerChance) return;

        StartKillCam(monsterType, position);
    }

    private void StartKillCam(string monsterType, Vector3 victimPos)
    {
        _isActive = true;
        _timer = Duration;
        _victimPosition = victimPos;
        _victimName = monsterType;

        // Store original camera
        _originalCamera = GetViewport().GetCamera3D();
        var playerPos = FPSController.Instance?.GlobalPosition ?? Vector3.Zero;

        // Create cinematic camera
        _killCam = new Camera3D();
        _killCam.Name = "KillCamCamera";
        _killCam.Fov = 50f; // Narrower FOV for dramatic effect
        AddChild(_killCam);

        // Position camera behind and above player, looking at victim
        Vector3 toVictim = (victimPos - playerPos).Normalized();
        Vector3 cameraOffset = Vector3.Up * 2f - toVictim * 3f;
        _killCam.GlobalPosition = playerPos + cameraOffset;
        _killCam.LookAt(victimPos + Vector3.Up * 0.5f);
        _killCam.Current = true;

        // Apply slow motion
        Engine.TimeScale = SlowMotionScale;

        // Lock player input
        if (FPSController.Instance != null)
            FPSController.Instance.MouseControlLocked = true;

        // Trigger AI commentary
        TriggerKillCamCommentary();

        // Request screen shake for dramatic effect
        FPSController.Instance?.RequestScreenShake(0.2f, 0.15f);

        GD.Print($"[KillCam3D] Started for {monsterType} kill at {victimPos}");
    }

    public override void _Process(double delta)
    {
        if (!_isActive) return;

        // Use unscaled delta for timer (Engine.TimeScale affects normal delta)
        float realDelta = (float)(delta / Engine.TimeScale);
        _timer -= realDelta;

        // Animate camera - slow pan toward victim
        if (_killCam != null && IsInstanceValid(_killCam))
        {
            Vector3 targetPos = _victimPosition + Vector3.Up * 1f;
            Vector3 targetCameraPos = _victimPosition + Vector3.Up * 1.5f + Vector3.Back * 2f;

            _killCam.GlobalPosition = _killCam.GlobalPosition.Lerp(
                targetCameraPos,
                realDelta * 0.5f
            );
            _killCam.LookAt(targetPos);
        }

        // End kill cam when timer expires
        if (_timer <= 0)
        {
            EndKillCam();
        }
    }

    private void EndKillCam()
    {
        if (!_isActive) return;
        _isActive = false;

        // Restore original camera
        if (_originalCamera != null && IsInstanceValid(_originalCamera))
        {
            _originalCamera.Current = true;
        }

        // Clean up kill cam
        if (_killCam != null && IsInstanceValid(_killCam))
        {
            _killCam.QueueFree();
        }
        _killCam = null;
        _originalCamera = null;

        // Restore time to normal game speed
        GameConfig.ResumeToNormalSpeed();

        // Unlock player input
        if (FPSController.Instance != null)
            FPSController.Instance.MouseControlLocked = false;

        GD.Print("[KillCam3D] Ended, control returned to player");
    }

    private void TriggerKillCamCommentary()
    {
        // Trigger kill cam specific commentary
        if (AIBroadcaster.Instance != null)
        {
            // Use TriggerEventWithContext for kill cam moment
            AIBroadcaster.Instance.TriggerEventWithContext(
                BroadcastEvent.KillCam,
                _victimName
            );
        }

        // Spike the ratings for drama
        AIBroadcaster.Instance?.UI?.SpikeRatings(0.7f);
    }

    /// <summary>
    /// Force end kill cam (used for panic key, death, etc.)
    /// </summary>
    public void ForceEnd()
    {
        if (_isActive)
        {
            EndKillCam();
            GD.Print("[KillCam3D] Force ended");
        }
    }

    /// <summary>
    /// Format monster type for display
    /// </summary>
    private static string FormatMonsterName(string monsterType)
    {
        var words = monsterType.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
        }
        return string.Join(" ", words);
    }
}
