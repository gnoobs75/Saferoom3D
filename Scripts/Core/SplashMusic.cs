using Godot;

namespace SafeRoom3D.Core;

/// <summary>
/// Singleton that plays splash.mp3 on loop during menus and editor.
/// Fades out when the game starts.
/// </summary>
public partial class SplashMusic : Node
{
    public static SplashMusic? Instance { get; private set; }

    private AudioStreamPlayer? _player;
    private bool _isFadingOut;

    public bool IsPlaying => _player?.Playing ?? false;

    public override void _Ready()
    {
        Instance = this;

        // Make this persist across scene changes
        ProcessMode = ProcessModeEnum.Always;

        // Create audio player
        _player = new AudioStreamPlayer();
        _player.Bus = "Master";
        _player.Finished += OnTrackFinished;
        AddChild(_player);

        // Load splash music
        var stream = GD.Load<AudioStream>("res://Assets/Audio/Music/splash.mp3");
        if (stream != null)
        {
            _player.Stream = stream;
            GD.Print("[SplashMusic] Loaded splash.mp3");
        }
        else
        {
            GD.PrintErr("[SplashMusic] Could not load splash.mp3");
        }
    }

    /// <summary>
    /// Start playing splash music (respects AudioConfig.IsMusicEnabled)
    /// </summary>
    public void Play()
    {
        if (_player == null || _player.Stream == null) return;
        if (_isFadingOut) return;

        if (!AudioConfig.IsMusicEnabled)
        {
            GD.Print("[SplashMusic] Music disabled, not playing");
            return;
        }

        if (_player.Playing)
        {
            GD.Print("[SplashMusic] Already playing");
            return;
        }

        _player.VolumeDb = Mathf.LinearToDb(0.7f * AudioConfig.MusicVolume);
        _player.Play();
        GD.Print("[SplashMusic] Started playing");
    }

    /// <summary>
    /// Stop music immediately
    /// </summary>
    public void Stop()
    {
        _player?.Stop();
        _isFadingOut = false;
        GD.Print("[SplashMusic] Stopped");
    }

    /// <summary>
    /// Fade out the music over the specified duration
    /// </summary>
    public void FadeOut(float duration = 2.0f)
    {
        if (_player == null || !_player.Playing) return;
        if (_isFadingOut) return;

        _isFadingOut = true;
        GD.Print($"[SplashMusic] Fading out over {duration}s");

        var tween = GetTree().CreateTween();
        tween.TweenProperty(_player, "volume_db", -40f, duration);
        tween.TweenCallback(Callable.From(() =>
        {
            _player.Stop();
            _isFadingOut = false;
            GD.Print("[SplashMusic] Fade out complete");
        }));
    }

    /// <summary>
    /// Update volume based on AudioConfig (call when settings change)
    /// </summary>
    public void UpdateVolume()
    {
        if (_player == null || !_player.Playing || _isFadingOut) return;

        if (!AudioConfig.IsMusicEnabled)
        {
            Stop();
            return;
        }

        _player.VolumeDb = Mathf.LinearToDb(0.7f * AudioConfig.MusicVolume);
    }

    private void OnTrackFinished()
    {
        // Loop the music
        if (!_isFadingOut && AudioConfig.IsMusicEnabled)
        {
            _player?.Play();
        }
    }

    public override void _ExitTree()
    {
        if (_player != null)
        {
            _player.Finished -= OnTrackFinished;
        }
        Instance = null;
    }
}
