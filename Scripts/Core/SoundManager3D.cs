using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Singleton sound manager for 3D audio - sound effects and music.
/// Supports 3D positional audio for immersive dungeon atmosphere.
/// Access via SoundManager3D.Instance
/// </summary>
public partial class SoundManager3D : Node
{
    public static SoundManager3D? Instance { get; private set; }

    // Audio players
    private AudioStreamPlayer? _musicPlayer;
    private List<AudioStreamPlayer> _sfxPlayers = new();
    private List<AudioStreamPlayer3D> _sfx3DPlayers = new();
    private const int MaxSfxPlayers = 8;
    private const int Max3DSfxPlayers = 16;

    // Sound library
    private Dictionary<string, AudioStream?> _sounds = new();

    // Music tracks
    private List<AudioStream> _musicTracks = new();
    private int _currentTrackIndex = -1;

    // Volume settings
    [Export] public float MasterVolume { get; set; } = 1.0f;
    [Export] public float MusicVolume { get; set; } = 0.7f;
    [Export] public float SfxVolume { get; set; } = 1.0f;

    // Track timing
    private const float MinTrackPlayDuration = 60f;
    private double _currentTrackPlayTime;

    // Event sound queue - plays sounds sequentially with pause between
    private readonly Queue<string> _eventSoundQueue = new();
    private bool _isPlayingEventSound;
    private float _eventSoundDelay;
    private const float EventSoundPauseDuration = 1.0f;

    public override void _Ready()
    {
        Instance = this;

        // Create music player
        _musicPlayer = new AudioStreamPlayer();
        _musicPlayer.Bus = "Master";
        _musicPlayer.Finished += OnMusicTrackFinished;
        AddChild(_musicPlayer);

        // Create 2D SFX player pool (for UI sounds)
        for (int i = 0; i < MaxSfxPlayers; i++)
        {
            var player = new AudioStreamPlayer();
            player.Bus = "Master";
            AddChild(player);
            _sfxPlayers.Add(player);
        }

        // Create 3D SFX player pool (for positional audio)
        for (int i = 0; i < Max3DSfxPlayers; i++)
        {
            var player = new AudioStreamPlayer3D();
            player.Bus = "Master";
            player.MaxDistance = 50f;
            player.UnitSize = 5f;
            player.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance;
            AddChild(player);
            _sfx3DPlayers.Add(player);
        }

        LoadSounds();
        LoadMusicTracks();

        AudioConfig.ConfigChanged += OnAudioConfigChanged;

        GD.Print("[SoundManager3D] Ready");
    }

    public override void _ExitTree()
    {
        AudioConfig.ConfigChanged -= OnAudioConfigChanged;
        Instance = null;
    }

    public override void _Process(double delta)
    {
        if (_musicPlayer != null && _musicPlayer.Playing)
        {
            _currentTrackPlayTime += delta;
        }

        // Process event sound queue
        ProcessEventSoundQueue((float)delta);
    }

    private void ProcessEventSoundQueue(float delta)
    {
        // Handle delay between sounds
        if (_eventSoundDelay > 0)
        {
            _eventSoundDelay -= delta;
            return;
        }

        // If playing, check if still playing
        if (_isPlayingEventSound)
        {
            // Check if any sfx player is still playing the event sound
            bool stillPlaying = false;
            foreach (var player in _sfxPlayers)
            {
                if (player.Playing)
                {
                    stillPlaying = true;
                    break;
                }
            }

            if (!stillPlaying)
            {
                _isPlayingEventSound = false;
                // Add delay before next sound
                if (_eventSoundQueue.Count > 0)
                {
                    _eventSoundDelay = EventSoundPauseDuration;
                }
            }
            return;
        }

        // Play next queued sound
        if (_eventSoundQueue.Count > 0)
        {
            string soundKey = _eventSoundQueue.Dequeue();
            PlaySoundImmediate(soundKey);
            _isPlayingEventSound = true;
        }
    }

    private void PlaySoundImmediate(string soundKey)
    {
        if (!_sounds.TryGetValue(soundKey, out var sound) || sound == null)
        {
            GD.PrintErr($"[SoundManager3D] Event sound not found: {soundKey}");
            return;
        }

        // Find an available player
        foreach (var player in _sfxPlayers)
        {
            if (!player.Playing)
            {
                player.Stream = sound;
                player.VolumeDb = Mathf.LinearToDb(SfxVolume * MasterVolume);
                player.Play();
                GD.Print($"[SoundManager3D] Playing event sound: {soundKey}");
                return;
            }
        }
    }

    private void QueueEventSound(string soundKey)
    {
        // Add to queue - will be played sequentially
        _eventSoundQueue.Enqueue(soundKey);
        GD.Print($"[SoundManager3D] Queued event sound: {soundKey} (queue size: {_eventSoundQueue.Count})");
    }

    private void LoadSounds()
    {
        // Use NinjaAdventure sounds from SafeRoom3D project
        string basePath = "res://Assets/NinjaAdventure/Audio/Sounds/";

        GD.Print($"[SoundManager3D] Loading sounds from: {basePath}");

        // Debug: Check if base directory exists
        var dir = DirAccess.Open(basePath);
        if (dir == null)
        {
            GD.PrintErr($"[SoundManager3D] ERROR: Cannot open directory: {basePath}");
            GD.PrintErr($"[SoundManager3D] Error code: {DirAccess.GetOpenError()}");
            return;
        }

        dir.ListDirEnd();

        // Attack/Combat sounds
        TryLoadSound("attack", basePath + "Whoosh & Slash/Slash.wav");
        TryLoadSound("attack_alt", basePath + "Whoosh & Slash/Sword2.wav");
        TryLoadSound("whoosh", basePath + "Whoosh & Slash/Whoosh.wav");

        // Hit sounds
        TryLoadSound("hit", basePath + "Hit & Impact/Hit1.wav");
        TryLoadSound("hit_alt", basePath + "Hit & Impact/Impact.wav");
        TryLoadSound("hit_heavy", basePath + "Hit & Impact/Hit3.wav");

        // Ranged/Magic sounds
        TryLoadSound("shoot", basePath + "Whoosh & Slash/Whoosh.wav");
        TryLoadSound("magic", basePath + "Magic & Skill/Magic1.wav");
        TryLoadSound("magic_alt", basePath + "Magic & Skill/Magic5.wav");
        TryLoadSound("heal", basePath + "Magic & Skill/Heal.wav");

        // Player sounds
        TryLoadSound("player_hit", basePath + "Voice/Voice1.wav");
        TryLoadSound("player_die", basePath + "Voice/Voice5.wav");
        TryLoadSound("footstep", basePath + "Hit & Impact/Wood1.wav");

        // Enemy sounds (generic fallbacks)
        TryLoadSound("enemy_hit", basePath + "Hit & Impact/Hit3.wav");
        TryLoadSound("enemy_die", basePath + "Elemental/Explosion3.wav");
        TryLoadSound("enemy_aggro", basePath + "Voice/Voice2.wav");

        // Environment sounds
        TryLoadSound("door_open", basePath + "Hit & Impact/Wood2.wav");
        TryLoadSound("chest_open", basePath + "Menu/Accept2.wav");
        TryLoadSound("pickup", basePath + "Bonus/Coin.wav");
        TryLoadSound("powerup", basePath + "Bonus/PowerUp1.wav");

        // UI sounds
        TryLoadSound("victory", basePath + "Bonus/Bonus.wav");
        TryLoadSound("stair_appear", basePath + "Magic & Skill/Magic5.wav");
        TryLoadSound("menu_select", basePath + "Menu/Accept2.wav");
        TryLoadSound("menu_back", basePath + "Menu/Decline2.wav");

        // Explosion/Impact
        TryLoadSound("explosion", basePath + "Elemental/Explosion1.wav");
        TryLoadSound("explosion_big", basePath + "Elemental/Explosion2.wav");

        // Load monster-specific sounds
        LoadMonsterSounds(basePath);

        // Load game event sounds (custom sounds folder)
        LoadEventSounds();

        int loaded = 0;
        foreach (var sound in _sounds.Values)
        {
            if (sound != null) loaded++;
        }
        GD.Print($"[SoundManager3D] Loaded {loaded} sounds");
    }

    private void LoadMonsterSounds(string basePath)
    {
        // ============================================
        // GOBLIN sounds - using custom .m4a sounds from Assets/Audio/Sounds/Goblin
        // ============================================
        const string goblinSoundPath = "res://Assets/Audio/Sounds/Goblin/";
        TryLoadSound("monster_goblin_idle", goblinSoundPath + "Goblin_Idle.m4a");
        TryLoadSound("monster_goblin_idle2", goblinSoundPath + "Goblin_Idle2.m4a");
        TryLoadSound("monster_goblin_idle3", goblinSoundPath + "Goblin_Idle3.m4a");
        TryLoadSound("monster_goblin_aggro", goblinSoundPath + "Goblin_Aggro.m4a");
        TryLoadSound("monster_goblin_attack", goblinSoundPath + "Goblin_Attack.m4a");
        TryLoadSound("monster_goblin_hit", goblinSoundPath + "Goblin_Hit.m4a");
        TryLoadSound("monster_goblin_die", goblinSoundPath + "Goblin_Death.m4a");

        // ============================================
        // GOBLIN SHAMAN sounds - mystical
        // ============================================
        TryLoadSound("monster_shaman_idle", basePath + "Magic & Skill/Strange.wav");
        TryLoadSound("monster_shaman_aggro", basePath + "Magic & Skill/Spirit.wav");
        TryLoadSound("monster_shaman_attack", basePath + "Magic & Skill/Magic3.wav");
        TryLoadSound("monster_shaman_die", basePath + "Magic & Skill/Magic4.wav");

        // ============================================
        // GOBLIN THROWER sounds
        // ============================================
        TryLoadSound("monster_thrower_attack", basePath + "Whoosh & Slash/Launch.wav");

        // ============================================
        // SLIME sounds - wet, squishy
        // ============================================
        TryLoadSound("monster_slime_idle", basePath + "Elemental/Bubble.wav");
        TryLoadSound("monster_slime_aggro", basePath + "Elemental/Bubble2.wav");
        TryLoadSound("monster_slime_attack", basePath + "Elemental/Water3.wav");
        TryLoadSound("monster_slime_hit", basePath + "Elemental/Water1.wav");
        TryLoadSound("monster_slime_die", basePath + "Elemental/Bubble.wav");

        // ============================================
        // EYE sounds - eerie, psychic
        // ============================================
        TryLoadSound("monster_eye_idle", basePath + "Magic & Skill/Fx.wav");
        TryLoadSound("monster_eye_aggro", basePath + "Magic & Skill/Magic2.wav");
        TryLoadSound("monster_eye_attack", basePath + "Magic & Skill/Magic1.wav");
        TryLoadSound("monster_eye_hit", basePath + "Voice/Voice4.wav");
        TryLoadSound("monster_eye_die", basePath + "Elemental/Explosion4.wav");

        // ============================================
        // MUSHROOM sounds - spore-y, organic
        // ============================================
        TryLoadSound("monster_mushroom_idle", basePath + "Elemental/Grass.wav");
        TryLoadSound("monster_mushroom_aggro", basePath + "Elemental/Grass2.wav");
        TryLoadSound("monster_mushroom_attack", basePath + "Elemental/Explosion5.wav");
        TryLoadSound("monster_mushroom_hit", basePath + "Hit & Impact/Impact2.wav");
        TryLoadSound("monster_mushroom_die", basePath + "Elemental/Grass2.wav");

        // ============================================
        // SPIDER sounds - chittering
        // ============================================
        TryLoadSound("monster_spider_idle", basePath + "Creature/Wings.wav");
        TryLoadSound("monster_spider_aggro", basePath + "Voice/Voice6.wav");
        TryLoadSound("monster_spider_attack", basePath + "Hit & Impact/Hit2.wav");
        TryLoadSound("monster_spider_hit", basePath + "Hit & Impact/Impact3.wav");
        TryLoadSound("monster_spider_die", basePath + "Hit & Impact/Impact4.wav");

        // ============================================
        // LIZARD sounds - hissing, reptilian
        // ============================================
        TryLoadSound("monster_lizard_idle", basePath + "Voice/Voice7.wav");
        TryLoadSound("monster_lizard_aggro", basePath + "Voice/Voice8.wav");
        TryLoadSound("monster_lizard_attack", basePath + "Whoosh & Slash/Slash3.wav");
        TryLoadSound("monster_lizard_hit", basePath + "Voice/Voice9.wav");
        TryLoadSound("monster_lizard_die", basePath + "Voice/Voice10.wav");

        // ============================================
        // SKELETON sounds - bony, rattling
        // ============================================
        TryLoadSound("monster_skeleton_idle", basePath + "Hit & Impact/Wood1.wav");
        TryLoadSound("monster_skeleton_aggro", basePath + "Alert/Alert3.wav");
        TryLoadSound("monster_skeleton_attack", basePath + "Whoosh & Slash/Sword2.wav");
        TryLoadSound("monster_skeleton_hit", basePath + "Hit & Impact/Hit5.wav");
        TryLoadSound("monster_skeleton_die", basePath + "Hit & Impact/Impact5.wav");

        // ============================================
        // WOLF sounds - growling, snarling
        // ============================================
        TryLoadSound("monster_wolf_idle", basePath + "Creature/Dog.wav");
        TryLoadSound("monster_wolf_aggro", basePath + "Voice/Voice2.wav");
        TryLoadSound("monster_wolf_attack", basePath + "Whoosh & Slash/Slash4.wav");
        TryLoadSound("monster_wolf_hit", basePath + "Voice/Voice1.wav");
        TryLoadSound("monster_wolf_die", basePath + "Voice/Voice5.wav");

        // ============================================
        // BAT sounds - squeaky, fluttery
        // ============================================
        TryLoadSound("monster_bat_idle", basePath + "Creature/Wings2.wav");
        TryLoadSound("monster_bat_aggro", basePath + "Creature/Wings.wav");
        TryLoadSound("monster_bat_attack", basePath + "Whoosh & Slash/Whoosh2.wav");
        TryLoadSound("monster_bat_hit", basePath + "Voice/Voice4.wav");
        TryLoadSound("monster_bat_die", basePath + "Jump & Bounce/Bounce3.wav");

        // ============================================
        // DRAGON sounds - powerful, booming
        // ============================================
        TryLoadSound("monster_dragon_idle", basePath + "Elemental/Fire.wav");
        TryLoadSound("monster_dragon_aggro", basePath + "Elemental/Explosion2.wav");
        TryLoadSound("monster_dragon_attack", basePath + "Elemental/Fireball.wav");
        TryLoadSound("monster_dragon_hit", basePath + "Hit & Impact/Hit6.wav");
        TryLoadSound("monster_dragon_die", basePath + "Elemental/Explosion6.wav");

        GD.Print("[SoundManager3D] Monster sounds loaded");
    }

    private void LoadMusicTracks()
    {
        // Music is now handled by:
        // - SplashMusic: plays splash.mp3 during menus/editor
        // - DungeonRadio: plays MP3 playlist during gameplay
        // The old .ogg loop system has been removed.
        GD.Print("[SoundManager3D] Music handled by SplashMusic and DungeonRadio");
    }

    private void LoadEventSounds()
    {
        // Load custom event sounds from Assets/Audio/Sounds/Events
        const string eventPath = "res://Assets/Audio/Sounds/Events/";

        // Achievement/milestone sounds
        TryLoadSound("event_achievement", eventPath + "ai_new_achievement.mp3");
        TryLoadSound("event_boss_kill", eventPath + "ai_holy_crap_dude.mp3");
        TryLoadSound("event_player_death", eventPath + "ai_you_are_so_dead.mp3");
        TryLoadSound("event_multi_kill", eventPath + "ai_you_monster.mp3");
        TryLoadSound("event_welcome", eventPath + "ai_welcome_crawler.mp3");
        TryLoadSound("event_dungeon_start", eventPath + "cascadia_now_get_out_there_and_kill_kill_kill.mp3");

        // Loot pickup sound (using existing pickup or a simple sound)
        // We already have "pickup" loaded above

        GD.Print("[SoundManager3D] Event sounds loaded");
    }

    private void TryLoadSound(string name, string path)
    {
        AudioStream? stream = null;

        // Try various audio formats - OGG, WAV, M4A, MP3
        string[] extensions = { ".ogg", ".wav", ".m4a", ".mp3" };
        string basePath = System.IO.Path.ChangeExtension(path, null);

        foreach (var ext in extensions)
        {
            string tryPath = basePath + ext;
            // Check if file exists first to avoid error spam in console
            if (!ResourceLoader.Exists(tryPath))
                continue;

            try
            {
                stream = GD.Load<AudioStream>(tryPath);
                if (stream != null)
                {
                    GD.Print($"[SoundManager3D] Loaded {ext.ToUpper()}: {name}");
                    _sounds[name] = stream;
                    return;
                }
            }
            catch { /* Format not found, try next */ }
        }

        // Also try original path as-is (might have correct extension)
        try
        {
            stream = GD.Load<AudioStream>(path);
            if (stream != null)
            {
                GD.Print($"[SoundManager3D] Loaded: {name}");
                _sounds[name] = stream;
                return;
            }
        }
        catch { /* Path not found */ }

        GD.Print($"[SoundManager3D] FAILED to load: {name} ({basePath})");
        _sounds[name] = null;
    }

    /// <summary>
    /// Play a 2D sound effect (for UI, global sounds).
    /// </summary>
    public void PlaySound(string name, float pitchVariation = 0.1f)
    {
        if (!AudioConfig.IsSoundEnabled)
        {
            GD.Print($"[SoundManager3D] PlaySound({name}) - sound disabled");
            return;
        }

        if (!_sounds.TryGetValue(name, out var stream) || stream == null)
        {
            GD.Print($"[SoundManager3D] PlaySound({name}) - sound not found");
            return;
        }

        var player = GetAvailableSfxPlayer();
        if (player == null)
        {
            GD.Print($"[SoundManager3D] PlaySound({name}) - no available player");
            return;
        }

        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(SfxVolume * MasterVolume * AudioConfig.SoundVolume);
        player.PitchScale = 1.0f + (float)GD.RandRange(-pitchVariation, pitchVariation);
        player.Play();
        GD.Print($"[SoundManager3D] PlaySound({name}) - playing at {player.VolumeDb}dB");
    }

    /// <summary>
    /// Play a 3D positional sound effect at a world position.
    /// </summary>
    public void PlaySoundAt(string name, Vector3 position, float pitchVariation = 0.1f)
    {
        if (!AudioConfig.IsSoundEnabled)
        {
            GD.Print($"[SoundManager3D] PlaySoundAt({name}) - sound disabled");
            return;
        }

        if (!_sounds.TryGetValue(name, out var stream) || stream == null)
        {
            GD.Print($"[SoundManager3D] PlaySoundAt({name}) - sound not found");
            return;
        }

        var player = GetAvailable3DSfxPlayer();
        if (player == null)
        {
            GD.Print($"[SoundManager3D] PlaySoundAt({name}) - no available player");
            return;
        }

        player.GlobalPosition = position;
        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(SfxVolume * MasterVolume * AudioConfig.SoundVolume);
        player.PitchScale = 1.0f + (float)GD.RandRange(-pitchVariation, pitchVariation);
        player.Play();
        GD.Print($"[SoundManager3D] PlaySoundAt({name}, {position}) - playing at {player.VolumeDb}dB");
    }

    /// <summary>
    /// Start playing random background music.
    /// </summary>
    public void StartRandomMusic()
    {
        if (_musicPlayer == null || _musicTracks.Count == 0) return;
        if (!AudioConfig.IsMusicEnabled) return;

        if (_musicPlayer.Playing)
        {
            GD.Print("[SoundManager3D] Music already playing");
            return;
        }

        PlayRandomTrack();
    }

    private void PlayRandomTrack()
    {
        if (_musicPlayer == null || _musicTracks.Count == 0) return;

        if (_musicPlayer.Playing) return;

        int newIndex;
        if (_musicTracks.Count > 1)
        {
            do
            {
                newIndex = (int)(GD.Randi() % _musicTracks.Count);
            } while (newIndex == _currentTrackIndex);
        }
        else
        {
            newIndex = 0;
        }

        _currentTrackIndex = newIndex;
        _currentTrackPlayTime = 0;
        _musicPlayer.Stream = _musicTracks[newIndex];
        _musicPlayer.VolumeDb = Mathf.LinearToDb(MusicVolume * MasterVolume * AudioConfig.MusicVolume);
        _musicPlayer.Play();

        GD.Print($"[SoundManager3D] Playing track {newIndex + 1}/{_musicTracks.Count}");
    }

    private void OnMusicTrackFinished()
    {
        if (!AudioConfig.IsMusicEnabled) return;

        if (_currentTrackPlayTime < MinTrackPlayDuration && _currentTrackIndex >= 0)
        {
            _musicPlayer?.Play();
        }
        else
        {
            _currentTrackPlayTime = 0;
            PlayRandomTrack();
        }
    }

    public void StopMusic(bool fadeOut = true)
    {
        if (_musicPlayer == null) return;

        if (fadeOut && _musicPlayer.Playing)
        {
            var tween = GetTree().CreateTween();
            tween.TweenProperty(_musicPlayer, "volume_db", -40f, 1.0f);
            tween.TweenCallback(Callable.From(() => _musicPlayer.Stop()));
        }
        else
        {
            _musicPlayer.Stop();
        }

        _currentTrackIndex = -1;
        _currentTrackPlayTime = 0;
    }

    public void ForceRestartMusic()
    {
        _musicPlayer?.Stop();
        _currentTrackIndex = -1;
        _currentTrackPlayTime = 0;

        if (AudioConfig.IsMusicEnabled)
        {
            PlayRandomTrack();
        }
    }

    private void OnAudioConfigChanged()
    {
        if (!AudioConfig.IsMusicEnabled)
        {
            _musicPlayer?.Stop();
        }
        else if (_musicPlayer != null && !_musicPlayer.Playing)
        {
            PlayRandomTrack();
        }

        if (_musicPlayer != null && _musicPlayer.Playing)
        {
            _musicPlayer.VolumeDb = Mathf.LinearToDb(MusicVolume * MasterVolume * AudioConfig.MusicVolume);
        }
    }

    public bool IsMusicEnabled => AudioConfig.IsMusicEnabled;
    public bool IsSoundEnabled => AudioConfig.IsSoundEnabled;

    public void ToggleMusic() => AudioConfig.ToggleMusic();
    public void ToggleSound() => AudioConfig.ToggleSound();

    private AudioStreamPlayer? GetAvailableSfxPlayer()
    {
        foreach (var player in _sfxPlayers)
        {
            if (!player.Playing)
                return player;
        }
        return _sfxPlayers.Count > 0 ? _sfxPlayers[0] : null;
    }

    private AudioStreamPlayer3D? GetAvailable3DSfxPlayer()
    {
        foreach (var player in _sfx3DPlayers)
        {
            if (!player.Playing)
                return player;
        }
        return _sfx3DPlayers.Count > 0 ? _sfx3DPlayers[0] : null;
    }

    /// <summary>
    /// Ambient sound helpers for dungeon atmosphere
    /// </summary>
    public void PlayAttackSound() => PlaySound("attack");
    public void PlayHitSound(Vector3 position) => PlaySoundAt("hit", position);
    public void PlayEnemyDeathSound(Vector3 position) => PlaySoundAt("enemy_die", position);
    public void PlayPlayerHitSound() => PlaySound("player_hit");
    public void PlayPlayerDeathSound() => PlaySound("player_die");
    public void PlayMagicSound(Vector3 position) => PlaySoundAt("magic", position);
    public void PlayExplosionSound(Vector3 position) => PlaySoundAt("explosion", position);
    public void PlayVictorySound() => PlaySound("victory");
    public void PlayHealSound(Vector3 position) => PlaySoundAt("heal", position);
    public void PlayPickupSound(Vector3 position) => PlaySoundAt("pickup", position);

    // ============================================
    // MONSTER SOUND METHODS
    // ============================================

    /// <summary>
    /// Play a monster's sound with its configured pitch and volume settings.
    /// Used by the editor preview and in-game enemies.
    /// </summary>
    public void PlayMonsterSound(string monsterType, string action, float pitchOverride = -1f)
    {
        MonsterSounds.SoundConfig? config;

        // For idle sounds, use random variant if available
        if (action.Equals("idle", System.StringComparison.OrdinalIgnoreCase))
        {
            var soundSet = MonsterSounds.GetSoundSet(monsterType);
            config = soundSet?.GetRandomIdleSound();
        }
        else
        {
            config = MonsterSounds.GetSoundConfig(monsterType, action);
        }

        if (config == null || string.IsNullOrEmpty(config.SoundKey))
        {
            // Fallback to generic sounds
            PlayFallbackMonsterSound(action);
            return;
        }

        PlaySoundWithConfig(config, pitchOverride);
    }

    /// <summary>
    /// Play a monster's sound at a 3D position with its configured pitch and volume settings.
    /// </summary>
    public void PlayMonsterSoundAt(string monsterType, string action, Vector3 position, float pitchOverride = -1f)
    {
        MonsterSounds.SoundConfig? config;

        // For idle sounds, use random variant if available
        if (action.Equals("idle", System.StringComparison.OrdinalIgnoreCase))
        {
            var soundSet = MonsterSounds.GetSoundSet(monsterType);
            config = soundSet?.GetRandomIdleSound();
        }
        else
        {
            config = MonsterSounds.GetSoundConfig(monsterType, action);
        }

        if (config == null || string.IsNullOrEmpty(config.SoundKey))
        {
            // Fallback to generic sounds
            PlayFallbackMonsterSoundAt(action, position);
            return;
        }

        PlaySoundWithConfigAt(config, position, pitchOverride);
    }

    private void PlaySoundWithConfig(MonsterSounds.SoundConfig config, float pitchOverride = -1f)
    {
        if (!AudioConfig.IsSoundEnabled) return;

        if (!_sounds.TryGetValue(config.SoundKey, out var stream) || stream == null)
        {
            GD.Print($"[SoundManager3D] Monster sound not found: {config.SoundKey}");
            return;
        }

        var player = GetAvailableSfxPlayer();
        if (player == null) return;

        float pitch = pitchOverride > 0 ? pitchOverride : config.PitchBase;
        float variation = config.PitchVariation;

        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(SfxVolume * MasterVolume * AudioConfig.SoundVolume) + config.VolumeDb;
        player.PitchScale = pitch + (float)GD.RandRange(-variation, variation);
        player.Play();
    }

    private void PlaySoundWithConfigAt(MonsterSounds.SoundConfig config, Vector3 position, float pitchOverride = -1f)
    {
        if (!AudioConfig.IsSoundEnabled) return;

        if (!_sounds.TryGetValue(config.SoundKey, out var stream) || stream == null)
        {
            GD.Print($"[SoundManager3D] Monster sound not found: {config.SoundKey}");
            return;
        }

        var player = GetAvailable3DSfxPlayer();
        if (player == null) return;

        float pitch = pitchOverride > 0 ? pitchOverride : config.PitchBase;
        float variation = config.PitchVariation;

        player.GlobalPosition = position;
        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(SfxVolume * MasterVolume * AudioConfig.SoundVolume) + config.VolumeDb;
        player.PitchScale = pitch + (float)GD.RandRange(-variation, variation);
        player.Play();
    }

    private void PlayFallbackMonsterSound(string action)
    {
        string fallback = action.ToLower() switch
        {
            "idle" => "",  // No generic idle sound
            "aggro" => "enemy_aggro",
            "attack" => "attack",
            "hit" => "enemy_hit",
            "die" => "enemy_die",
            _ => ""
        };

        if (!string.IsNullOrEmpty(fallback))
        {
            PlaySound(fallback);
        }
    }

    private void PlayFallbackMonsterSoundAt(string action, Vector3 position)
    {
        string fallback = action.ToLower() switch
        {
            "idle" => "",  // No generic idle sound
            "aggro" => "enemy_aggro",
            "attack" => "attack",
            "hit" => "enemy_hit",
            "die" => "enemy_die",
            _ => ""
        };

        if (!string.IsNullOrEmpty(fallback))
        {
            PlaySoundAt(fallback, position);
        }
    }

    /// <summary>
    /// Check if a specific sound key exists in the library
    /// </summary>
    public bool HasSound(string soundKey)
    {
        return _sounds.TryGetValue(soundKey, out var stream) && stream != null;
    }

    // ============================================
    // EVENT SOUND METHODS
    // ============================================

    /// <summary>
    /// Play achievement sound (every 10 kills of a monster type) - queued
    /// </summary>
    public void PlayAchievementSound() => QueueEventSound("event_achievement");

    /// <summary>
    /// Play boss kill sound - queued
    /// </summary>
    public void PlayBossKillSound() => QueueEventSound("event_boss_kill");

    /// <summary>
    /// Play player death sound - queued (high priority, plays immediately if possible)
    /// </summary>
    public void PlayPlayerDeathEventSound() => QueueEventSound("event_player_death");

    /// <summary>
    /// Play multi-kill sound (5+ kills at once) - queued
    /// </summary>
    public void PlayMultiKillSound() => QueueEventSound("event_multi_kill");

    /// <summary>
    /// Play welcome crawler sound - queued
    /// </summary>
    public void PlayWelcomeSound() => QueueEventSound("event_welcome");

    /// <summary>
    /// Play dungeon start sound (kill kill kill) - queued
    /// </summary>
    public void PlayDungeonStartSound() => QueueEventSound("event_dungeon_start");

    /// <summary>
    /// Play loot pickup sound (not queued - immediate)
    /// </summary>
    public void PlayLootPickupSound() => PlaySound("pickup", 0.1f);
}
