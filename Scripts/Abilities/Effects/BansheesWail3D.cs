using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Banshee's Wail ability - Scream that causes enemies to flee.
/// Instant fear AOE around the player.
/// </summary>
public partial class BansheesWail3D : Ability3D
{
    // Singleton for checking fear state
    public static BansheesWail3D? Instance { get; private set; }

    public override string AbilityId => "banshees_wail";
    public override string AbilityName => "Banshee's Wail";
    public override string Description => "Let out a terrifying scream that causes enemies to flee.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 25f;
    public override int DefaultManaCost => 30;

    // Banshee's Wail stats
    public float Duration { get; private set; } = 4f;
    public float Radius { get; private set; } = 15.625f; // 250px / 16

    private HashSet<Node3D> _fearedEnemies = new();
    private BansheesWailEffect3D? _effect;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
    }

    protected override void Activate()
    {
        // Create visual effect centered on player
        _effect = new BansheesWailEffect3D();
        _effect.Name = "BansheesWailEffect";
        _effect.Initialize(Radius);

        if (Player != null)
        {
            Player.AddChild(_effect);
            _effect.Position = Vector3.Zero;
        }

        // Apply fear to all enemies in radius
        ApplyFear();

        SetActiveWithDuration(Duration);

        GD.Print($"[BansheesWail3D] Activated, feared {_fearedEnemies.Count} enemies");
    }

    private void ApplyFear()
    {
        Vector3 playerPos = GetPlayerPosition();

        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(playerPos);
                if (dist <= Radius)
                {
                    _fearedEnemies.Add(enemy);

                    // Notify enemy it's feared
                    if (enemy.HasMethod("SetFeared"))
                    {
                        enemy.Call("SetFeared", true);
                    }
                }
            }
        }
    }

    protected override void OnDeactivate()
    {
        // Remove fear from all enemies
        foreach (var enemy in _fearedEnemies)
        {
            if (IsInstanceValid(enemy) && enemy.HasMethod("SetFeared"))
            {
                enemy.Call("SetFeared", false);
            }
        }
        _fearedEnemies.Clear();

        // Remove effect
        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print("[BansheesWail3D] Deactivated, enemies recovered");
    }

    /// <summary>
    /// Check if an enemy is currently feared.
    /// </summary>
    public bool IsEnemyFeared(Node3D enemy)
    {
        return _fearedEnemies.Contains(enemy);
    }

    /// <summary>
    /// Static check for enemy fear state.
    /// </summary>
    public static bool IsAnyWailFearingEnemy(Node3D enemy)
    {
        if (Instance != null && IsInstanceValid(Instance))
        {
            return Instance.IsEnemyFeared(enemy);
        }
        return false;
    }

    /// <summary>
    /// Get the player position for flee direction calculation.
    /// </summary>
    public Vector3 GetFleeFromPosition()
    {
        return GetPlayerPosition();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (Instance == this) Instance = null;
    }
}

/// <summary>
/// Visual effect for banshee's wail.
/// </summary>
public partial class BansheesWailEffect3D : Node3D
{
    private float _radius;
    private MeshInstance3D? _waveMesh;
    private OmniLight3D? _light;
    private float _waveProgress;

    public void Initialize(float radius)
    {
        _radius = radius;
    }

    public override void _Ready()
    {
        CreateWaveEffect();
        CreateLight();

        // Play scream sound
        Core.SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);
    }

    private void CreateWaveEffect()
    {
        // Expanding ring wave
        _waveMesh = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 0.5f;
        torus.OuterRadius = 1f;
        torus.Rings = 32;
        torus.RingSegments = 6;
        _waveMesh.Mesh = torus;

        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.8f, 0.6f, 0.9f, 0.8f);
        material.EmissionEnabled = true;
        material.Emission = new Color(0.7f, 0.5f, 0.9f);
        material.EmissionEnergyMultiplier = 2f;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _waveMesh.MaterialOverride = material;

        _waveMesh.Position = new Vector3(0, 1f, 0);
        _waveMesh.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(_waveMesh);

        // Animate expansion
        AnimateWave();
    }

    private void AnimateWave()
    {
        if (_waveMesh == null) return;

        var tween = CreateTween();
        tween.TweenProperty(_waveMesh, "scale", Vector3.One * _radius, 0.5f)
            .SetEase(Tween.EaseType.Out);

        if (_waveMesh.MaterialOverride is StandardMaterial3D mat)
        {
            tween.Parallel().TweenProperty(mat, "albedo_color:a", 0.2f, 0.5f);
        }
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.8f, 0.6f, 0.9f);
        _light.LightEnergy = 3f;
        _light.OmniRange = 5f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);

        // Flash and fade
        var tween = CreateTween();
        tween.TweenProperty(_light, "light_energy", 1f, 0.3f);
    }

    public override void _Process(double delta)
    {
        // Pulse effect during active duration
        _waveProgress += (float)delta;
        float pulse = Mathf.Sin(_waveProgress * 5f) * 0.3f;

        if (_waveMesh != null)
        {
            float baseScale = _radius * 0.8f;
            _waveMesh.Scale = Vector3.One * (baseScale + pulse);
        }

        if (_light != null)
        {
            _light.LightEnergy = 1f + Mathf.Abs(pulse);
        }
    }

    public void FadeOut()
    {
        var tween = CreateTween();
        tween.SetParallel(true);

        if (_waveMesh?.MaterialOverride is StandardMaterial3D mat)
        {
            tween.TweenProperty(mat, "albedo_color:a", 0f, 0.3f);
        }
        if (_light != null)
        {
            tween.TweenProperty(_light, "light_energy", 0f, 0.3f);
        }

        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
