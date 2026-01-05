using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;
using SafeRoom3D.Broadcaster;

namespace SafeRoom3D.UI;

/// <summary>
/// Dungeon Radio - A cheesy dungeon-themed music player.
/// Press Y to toggle the radio interface.
/// Plays MP3 files from Assets/Audio/Music
/// </summary>
public partial class DungeonRadio : Node
{
    public static DungeonRadio? Instance { get; private set; }

    // Audio
    private AudioStreamPlayer? _player;
    private readonly List<TrackInfo> _playlist = new();
    private int _currentTrackIndex = -1;

    // Volume and ducking
    private float _baseVolume = 0.7f;
    private float _currentVolume = 0.7f;
    private const float DuckedVolume = 0.25f;  // Volume when AI is talking
    private const float DuckingSpeed = 5f;     // How fast to duck/unduck

    // State
    public bool IsPlaying => _player?.Playing ?? false;
    public bool IsPaused { get; private set; }
    public bool IsVisible => _ui?.IsVisible ?? false;
    public float PlaybackPosition => _player?.GetPlaybackPosition() ?? 0f;
    public float TrackLength => (float)(_player?.Stream?.GetLength() ?? 0);

    // UI
    private DungeonRadioUI? _ui;

    // Events
    public event System.Action? TrackChanged;
    public event System.Action? PlayStateChanged;

    public class TrackInfo
    {
        public string FileName { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public AudioStream? Stream { get; set; }
    }

    public override void _Ready()
    {
        Instance = this;

        // Create audio player
        _player = new AudioStreamPlayer();
        _player.Bus = "Master";
        _player.Finished += OnTrackFinished;
        AddChild(_player);

        // Load MP3 playlist
        LoadPlaylist();

        // Create UI
        _ui = new DungeonRadioUI();
        AddChild(_ui);

        GD.Print($"[DungeonRadio] Ready with {_playlist.Count} tracks");
    }

    public override void _Process(double delta)
    {
        if (_player == null || !_player.Playing) return;

        // Check if AI broadcaster is speaking - duck the music
        bool aiSpeaking = AIBroadcaster.Instance?.IsSpeaking ?? false;
        float targetVolume = aiSpeaking ? DuckedVolume : _baseVolume;

        // Smoothly interpolate volume
        _currentVolume = Mathf.MoveToward(_currentVolume, targetVolume, DuckingSpeed * (float)delta);

        // Apply volume
        _player.VolumeDb = Mathf.LinearToDb(_currentVolume * AudioConfig.MusicVolume);
    }

    private void LoadPlaylist()
    {
        string basePath = "res://Assets/Audio/Music/";
        var dir = DirAccess.Open(basePath);

        if (dir == null)
        {
            GD.PrintErr($"[DungeonRadio] Cannot open music directory: {basePath}");
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();

        while (!string.IsNullOrEmpty(fileName))
        {
            // Only load MP3 files (not .import files)
            if (fileName.EndsWith(".mp3") && !fileName.EndsWith(".mp3.import"))
            {
                string fullPath = basePath + fileName;
                var stream = GD.Load<AudioStream>(fullPath);

                if (stream != null)
                {
                    var track = ParseTrackInfo(fileName);
                    track.Stream = stream;
                    _playlist.Add(track);
                    GD.Print($"[DungeonRadio] Loaded: {track.DisplayName}");
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        // Shuffle playlist on load
        ShufflePlaylist();
    }

    private TrackInfo ParseTrackInfo(string fileName)
    {
        // Remove .mp3 extension
        string name = fileName.Replace(".mp3", "");

        // Try to parse "Artist - Title" format
        string artist = "Unknown Artist";
        string title = name;

        int dashIndex = name.IndexOf(" - ");
        if (dashIndex > 0)
        {
            artist = name.Substring(0, dashIndex).Trim();
            title = name.Substring(dashIndex + 3).Trim();
        }

        return new TrackInfo
        {
            FileName = fileName,
            Artist = artist,
            Title = title,
            DisplayName = name
        };
    }

    private void ShufflePlaylist()
    {
        // Fisher-Yates shuffle
        for (int i = _playlist.Count - 1; i > 0; i--)
        {
            int j = (int)(GD.Randi() % (i + 1));
            (_playlist[i], _playlist[j]) = (_playlist[j], _playlist[i]);
        }
    }

    public void ToggleUI()
    {
        _ui?.Toggle();
    }

    public void Play()
    {
        if (_playlist.Count == 0) return;

        if (IsPaused && _player != null)
        {
            // Resume from pause
            _player.StreamPaused = false;
            IsPaused = false;
            PlayStateChanged?.Invoke();
            return;
        }

        if (_currentTrackIndex < 0)
        {
            _currentTrackIndex = 0;
        }

        PlayCurrentTrack();
    }

    public void Pause()
    {
        if (_player == null || !_player.Playing) return;

        _player.StreamPaused = true;
        IsPaused = true;
        PlayStateChanged?.Invoke();
    }

    public void TogglePlayPause()
    {
        if (IsPaused || !IsPlaying)
        {
            Play();
        }
        else
        {
            Pause();
        }
    }

    public void Stop()
    {
        _player?.Stop();
        IsPaused = false;
        PlayStateChanged?.Invoke();
    }

    public void NextTrack()
    {
        if (_playlist.Count == 0) return;

        _currentTrackIndex = (_currentTrackIndex + 1) % _playlist.Count;
        PlayCurrentTrack();
    }

    public void PreviousTrack()
    {
        if (_playlist.Count == 0) return;

        // If more than 3 seconds in, restart current track
        if (PlaybackPosition > 3f)
        {
            RestartTrack();
            return;
        }

        _currentTrackIndex--;
        if (_currentTrackIndex < 0)
            _currentTrackIndex = _playlist.Count - 1;

        PlayCurrentTrack();
    }

    public void RestartTrack()
    {
        if (_player == null) return;

        _player.Seek(0);
        if (!_player.Playing)
        {
            _player.Play();
            IsPaused = false;
        }
        PlayStateChanged?.Invoke();
    }

    public void Seek(float position)
    {
        _player?.Seek(position);
    }

    private void PlayCurrentTrack()
    {
        if (_player == null || _currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
            return;

        var track = _playlist[_currentTrackIndex];
        if (track.Stream == null) return;

        _player.Stream = track.Stream;
        _player.VolumeDb = Mathf.LinearToDb(_currentVolume * AudioConfig.MusicVolume);
        _player.Play();
        IsPaused = false;

        TrackChanged?.Invoke();
        PlayStateChanged?.Invoke();

        GD.Print($"[DungeonRadio] Now playing: {track.DisplayName}");
    }

    private void OnTrackFinished()
    {
        // Auto-advance to next track
        NextTrack();
    }

    public TrackInfo? GetCurrentTrack()
    {
        if (_currentTrackIndex < 0 || _currentTrackIndex >= _playlist.Count)
            return null;
        return _playlist[_currentTrackIndex];
    }

    public int GetCurrentTrackIndex() => _currentTrackIndex;
    public int GetPlaylistCount() => _playlist.Count;

    public List<TrackInfo> GetPlaylist() => _playlist;

    public void SetVolume(float volume)
    {
        _baseVolume = volume;
        // Current volume will be adjusted in _Process based on ducking state
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
