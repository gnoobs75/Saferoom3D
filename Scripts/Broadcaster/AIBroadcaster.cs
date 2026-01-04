using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Broadcaster;

/// <summary>
/// Broadcast events that trigger AI commentary
/// </summary>
public enum BroadcastEvent
{
    // Combat events
    MonsterKilled,
    MultiKill,
    BossEncounter,
    BossDefeated,
    PlayerDamaged,
    NearDeath,
    PlayerDeath,

    // Ability events
    AbilityUsed,
    AbilityMissed,

    // Progress events
    FloorCleared,
    FloorEntered,
    ItemLooted,
    RareLoot,

    // Idle/Misc events
    IdleTooLong,
    FirstBlood,
    Comeback,

    // System events
    GameStarted,
    PlayerMutedAI,
    PlayerUnmutedAI
}

/// <summary>
/// Main controller for the snarky AI broadcaster system.
/// Manages commentary timing, TTS, and coordinates with the UI/Avatar.
/// </summary>
public partial class AIBroadcaster : Control
{
    // Singleton instance
    public static AIBroadcaster? Instance { get; private set; }

    // Child components
    private BroadcasterUI? _ui;
    private BroadcasterAvatar? _avatar;
    private CommentaryDatabase? _commentary;

    // Public accessor for UI (for notification forwarding)
    public BroadcasterUI? UI => _ui;

    // State
    private bool _isMuted = false;
    private bool _isMinimized = false;
    private bool _isSpeaking = false;
    private float _lastCommentaryTime = 0f;
    private float _gameTime = 0f;
    private int _killsSinceLastComment = 0;
    private int _totalKills = 0;
    private int _totalDeaths = 0;
    private string _lastMonsterKilled = "";
    private Queue<QueuedComment> _commentQueue = new();

    // Timing configuration
    [Export] public float MinTimeBetweenComments = 8f;   // Minimum seconds between comments
    [Export] public float MaxTimeBetweenComments = 30f;  // Max time before idle comment
    [Export] public float IdleCommentChance = 0.3f;      // Chance of idle comment when max time reached
    [Export] public int KillsBeforeComment = 5;          // Kills before potentially commenting
    [Export] public float MultiKillWindow = 2f;          // Seconds to count as multi-kill

    // TTS configuration
    [Export] public bool TTSEnabled = true;
    [Export] public float TTSRate = 1.0f;                // Speech rate (0.5-2.0)
    [Export] public float TTSPitch = 1.1f;               // Slightly higher pitch for snark
    [Export] public float TTSVolume = 80f;               // Volume (0-100)

    // Multi-kill tracking
    private int _recentKillCount = 0;
    private float _lastKillTime = 0f;

    // Critical events that force popup even when minimized
    private static readonly HashSet<BroadcastEvent> CriticalEvents = new()
    {
        BroadcastEvent.BossEncounter,
        BroadcastEvent.BossDefeated,
        BroadcastEvent.PlayerDeath,
        BroadcastEvent.FloorCleared,
        BroadcastEvent.RareLoot,
        BroadcastEvent.GameStarted
    };

    /// <summary>
    /// Queued comment with priority
    /// </summary>
    private struct QueuedComment
    {
        public string Text;
        public BroadcastEvent Event;
        public AvatarExpression Expression;
        public bool IsCritical;
        public float QueueTime;
    }

    public override void _Ready()
    {
        Instance = this;

        // Initialize components
        _commentary = new CommentaryDatabase();

        // Create UI
        _ui = new BroadcasterUI();
        AddChild(_ui);

        // Create avatar (will be added to UI)
        _avatar = new BroadcasterAvatar();
        _ui.SetAvatar(_avatar);

        // Connect UI signals
        _ui.MutePressed += OnMutePressed;
        _ui.MinimizePressed += OnMinimizePressed;

        // Start with a greeting
        CallDeferred(nameof(DelayedGreeting));

        GD.Print("[AIBroadcaster] Initialized - Your snarky companion awaits");
    }

    private void DelayedGreeting()
    {
        TriggerEvent(BroadcastEvent.GameStarted);
    }

    public override void _Process(double delta)
    {
        _gameTime += (float)delta;

        // Check for idle commentary
        float timeSinceComment = _gameTime - _lastCommentaryTime;
        if (timeSinceComment > MaxTimeBetweenComments && !_isSpeaking)
        {
            if (GD.Randf() < IdleCommentChance)
            {
                TriggerEvent(BroadcastEvent.IdleTooLong);
            }
        }

        // Process comment queue
        ProcessCommentQueue();

        // Update multi-kill window
        if (_recentKillCount > 0 && _gameTime - _lastKillTime > MultiKillWindow)
        {
            if (_recentKillCount >= 3)
            {
                TriggerEventWithContext(BroadcastEvent.MultiKill, _recentKillCount.ToString());
            }
            _recentKillCount = 0;
        }
    }

    /// <summary>
    /// Trigger a broadcast event with optional context
    /// </summary>
    public void TriggerEvent(BroadcastEvent evt, string context = "")
    {
        TriggerEventWithContext(evt, context);
    }

    /// <summary>
    /// Trigger event with context (monster name, item name, etc.)
    /// </summary>
    public void TriggerEventWithContext(BroadcastEvent evt, string context)
    {
        // Check if we should comment
        if (!ShouldComment(evt))
            return;

        // Get commentary line
        string? line = _commentary?.GetLine(evt, context, _totalKills, _totalDeaths);
        if (string.IsNullOrEmpty(line))
            return;

        // If muted, convert to ALL CAPS
        if (_isMuted)
        {
            line = line.ToUpper();
        }

        // Determine expression
        AvatarExpression expression = GetExpressionForEvent(evt);

        // Queue the comment
        bool isCritical = CriticalEvents.Contains(evt);
        _commentQueue.Enqueue(new QueuedComment
        {
            Text = line,
            Event = evt,
            Expression = expression,
            IsCritical = isCritical,
            QueueTime = _gameTime
        });

        // Critical events force popup
        if (isCritical && _isMinimized)
        {
            _isMinimized = false;
            _ui?.SetMinimized(false);
        }
    }

    /// <summary>
    /// Called when a monster is killed
    /// </summary>
    public void OnMonsterKilled(string monsterType)
    {
        _totalKills++;
        _killsSinceLastComment++;
        _lastMonsterKilled = monsterType;
        _recentKillCount++;
        _lastKillTime = _gameTime;

        // First kill of the session
        if (_totalKills == 1)
        {
            TriggerEventWithContext(BroadcastEvent.FirstBlood, monsterType);
            return;
        }

        // Check if we should comment on this kill
        if (_killsSinceLastComment >= KillsBeforeComment)
        {
            TriggerEventWithContext(BroadcastEvent.MonsterKilled, monsterType);
            _killsSinceLastComment = 0;
        }
    }

    /// <summary>
    /// Called when player takes damage
    /// </summary>
    public void OnPlayerDamaged(float currentHealthPercent)
    {
        if (currentHealthPercent < 0.2f)
        {
            TriggerEvent(BroadcastEvent.NearDeath);
        }
        else if (GD.Randf() < 0.1f) // 10% chance to comment on regular damage
        {
            TriggerEvent(BroadcastEvent.PlayerDamaged);
        }
    }

    /// <summary>
    /// Called when player dies
    /// </summary>
    public void OnPlayerDeath()
    {
        _totalDeaths++;
        TriggerEvent(BroadcastEvent.PlayerDeath);
    }

    /// <summary>
    /// Called when boss is encountered
    /// </summary>
    public void OnBossEncounter(string bossName)
    {
        TriggerEventWithContext(BroadcastEvent.BossEncounter, bossName);
    }

    /// <summary>
    /// Called when boss is defeated
    /// </summary>
    public void OnBossDefeated(string bossName)
    {
        TriggerEventWithContext(BroadcastEvent.BossDefeated, bossName);
    }

    private bool ShouldComment(BroadcastEvent evt)
    {
        // Critical events always get through
        if (CriticalEvents.Contains(evt))
            return true;

        // Check timing
        float timeSinceComment = _gameTime - _lastCommentaryTime;
        if (timeSinceComment < MinTimeBetweenComments)
            return false;

        // Don't queue too many
        if (_commentQueue.Count >= 3)
            return false;

        return true;
    }

    private void ProcessCommentQueue()
    {
        if (_isSpeaking || _commentQueue.Count == 0)
            return;

        // Expire old comments (more than 10 seconds old)
        while (_commentQueue.Count > 0 && _gameTime - _commentQueue.Peek().QueueTime > 10f)
        {
            _commentQueue.Dequeue();
        }

        if (_commentQueue.Count == 0)
            return;

        var comment = _commentQueue.Dequeue();
        PlayComment(comment);
    }

    private void PlayComment(QueuedComment comment)
    {
        _isSpeaking = true;
        _lastCommentaryTime = _gameTime;

        // Set avatar expression
        _avatar?.SetExpression(comment.Expression);

        // Show text in UI
        _ui?.ShowComment(comment.Text);

        // Start TTS if enabled and not muted
        if (TTSEnabled && !_isMuted)
        {
            SpeakText(comment.Text);
        }
        else
        {
            // No TTS, just show text for a few seconds
            float displayTime = Mathf.Max(2f, comment.Text.Length * 0.05f);
            GetTree().CreateTimer(displayTime).Timeout += OnCommentFinished;
        }

        // Start talking animation
        _avatar?.StartTalking();
    }

    private void SpeakText(string text)
    {
        // Use Godot's built-in TTS
        var voices = DisplayServer.TtsGetVoices();
        if (voices.Count == 0)
        {
            GD.PrintErr("[AIBroadcaster] No TTS voices available");
            float displayTime = Mathf.Max(2f, text.Length * 0.05f);
            GetTree().CreateTimer(displayTime).Timeout += OnCommentFinished;
            return;
        }

        // Pick a voice (preferring English)
        string voiceId = "";
        foreach (var voice in voices)
        {
            string lang = voice["language"].AsString();
            if (lang.StartsWith("en"))
            {
                voiceId = voice["id"].AsString();
                break;
            }
        }
        if (string.IsNullOrEmpty(voiceId) && voices.Count > 0)
        {
            voiceId = voices[0]["id"].AsString();
        }

        // Speak
        DisplayServer.TtsSpeak(text, voiceId, (int)TTSVolume, TTSPitch, TTSRate);

        // Estimate speech duration and set timer
        float wordsPerSecond = 2.5f * TTSRate;
        int wordCount = text.Split(' ').Length;
        float duration = wordCount / wordsPerSecond + 0.5f;

        GetTree().CreateTimer(duration).Timeout += OnCommentFinished;
    }

    private void OnCommentFinished()
    {
        _isSpeaking = false;
        _avatar?.StopTalking();
        _avatar?.SetExpression(AvatarExpression.Idle);
    }

    private AvatarExpression GetExpressionForEvent(BroadcastEvent evt)
    {
        // Sometimes add eye roll for extra snark
        if (GD.Randf() < 0.15f && evt is BroadcastEvent.MonsterKilled or BroadcastEvent.IdleTooLong or BroadcastEvent.PlayerDamaged)
        {
            return AvatarExpression.EyeRoll;
        }

        return evt switch
        {
            BroadcastEvent.MonsterKilled => GD.Randf() < 0.3f ? AvatarExpression.Skeptical : AvatarExpression.Bored,
            BroadcastEvent.MultiKill => AvatarExpression.FakeExcited,
            BroadcastEvent.BossEncounter => AvatarExpression.Excited,
            BroadcastEvent.BossDefeated => AvatarExpression.Impressed,
            BroadcastEvent.PlayerDamaged => GD.Randf() < 0.4f ? AvatarExpression.Skeptical : AvatarExpression.Smug,
            BroadcastEvent.NearDeath => AvatarExpression.FakeWorried,
            BroadcastEvent.PlayerDeath => _totalDeaths > 3 ? AvatarExpression.EyeRoll : AvatarExpression.Disappointed,
            BroadcastEvent.FloorCleared => AvatarExpression.SlightlyImpressed,
            BroadcastEvent.RareLoot => AvatarExpression.Surprised,
            BroadcastEvent.IdleTooLong => GD.Randf() < 0.5f ? AvatarExpression.Thinking : AvatarExpression.Bored,
            BroadcastEvent.PlayerMutedAI => AvatarExpression.Angry,
            BroadcastEvent.AbilityUsed => AvatarExpression.Thinking,
            BroadcastEvent.FirstBlood => AvatarExpression.SlightlyImpressed,
            _ => AvatarExpression.Idle
        };
    }

    private void OnMutePressed()
    {
        _isMuted = !_isMuted;

        if (_isMuted)
        {
            DisplayServer.TtsStop();
            TriggerEvent(BroadcastEvent.PlayerMutedAI);
        }
        else
        {
            TriggerEvent(BroadcastEvent.PlayerUnmutedAI);
        }

        _ui?.SetMuted(_isMuted);
    }

    private void OnMinimizePressed()
    {
        _isMinimized = !_isMinimized;
        _ui?.SetMinimized(_isMinimized);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;

        DisplayServer.TtsStop();
    }
}
