using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Timestop Bubble ability - Create a zone that freezes enemies.
/// Targeted control spell that completely stops enemies inside.
/// </summary>
public partial class TimestopBubble3D : Ability3D, ITargetedAbility
{
    public override string AbilityId => "timestop_bubble";
    public override string AbilityName => "Timestop Bubble";
    public override string Description => "Create a zone that freezes enemies in time.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 30f;
    public override int DefaultManaCost => 50;

    // Timestop stats
    public float Duration { get; private set; } = 6f;
    public float Radius { get; private set; } = 9.375f; // 150px / 16

    // ITargetedAbility
    public float TargetRadius => Radius;
    public Color TargetColor => new(0.3f, 0.8f, 0.9f); // Cyan

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Create the timestop zone
        var effect = new TimestopBubbleEffect3D();
        effect.Name = "TimestopBubbleEffect";
        effect.GlobalPosition = _targetPosition;
        effect.Initialize(Duration, Radius);

        GetTree().Root.AddChild(effect);

        GD.Print($"[TimestopBubble3D] Created at {_targetPosition}");
    }
}

/// <summary>
/// Visual and freeze effect for timestop bubble.
/// </summary>
public partial class TimestopBubbleEffect3D : Node3D
{
    // Singleton for checking if enemies are frozen
    public static TimestopBubbleEffect3D? Instance { get; private set; }

    private float _duration;
    private float _radius;
    private float _timeRemaining;

    private MeshInstance3D? _bubbleMesh;
    private OmniLight3D? _light;
    private Area3D? _freezeArea;

    private StandardMaterial3D? _material;
    private float _pulseTimer;
    private HashSet<Node3D> _frozenEnemies = new();

    public void Initialize(float duration, float radius)
    {
        _duration = duration;
        _radius = radius;
        _timeRemaining = duration;
    }

    public override void _Ready()
    {
        Instance = this;

        CreateBubbleVisual();
        CreateFreezeArea();
        CreateLight();

        Core.SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);
    }

    private void CreateBubbleVisual()
    {
        _bubbleMesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = _radius;
        sphere.Height = _radius * 2;
        sphere.RadialSegments = 32;
        sphere.Rings = 16;
        _bubbleMesh.Mesh = sphere;

        // Translucent cyan/white bubble
        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(0.6f, 0.9f, 1f, 0.25f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(0.5f, 0.8f, 1f);
        _material.EmissionEnergyMultiplier = 0.5f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _bubbleMesh.MaterialOverride = _material;

        _bubbleMesh.Position = new Vector3(0, _radius, 0);
        AddChild(_bubbleMesh);

        // Add frozen clock pattern inside
        CreateClockPattern();
    }

    private void CreateClockPattern()
    {
        // Create a simple torus ring inside the bubble
        var ring = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = _radius * 0.5f;
        torus.OuterRadius = _radius * 0.55f;
        ring.Mesh = torus;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.7f, 0.95f, 1f, 0.5f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.6f, 0.9f, 1f);
        mat.EmissionEnergyMultiplier = 1f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        ring.MaterialOverride = mat;

        ring.Position = new Vector3(0, _radius, 0);
        ring.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(ring);
    }

    private void CreateFreezeArea()
    {
        _freezeArea = new Area3D();
        _freezeArea.Name = "FreezeArea";

        var shape = new CollisionShape3D();
        var sphereShape = new SphereShape3D();
        sphereShape.Radius = _radius;
        shape.Shape = sphereShape;
        shape.Position = new Vector3(0, _radius, 0);

        _freezeArea.AddChild(shape);
        _freezeArea.CollisionLayer = 0;
        _freezeArea.CollisionMask = 2; // Enemy layer

        _freezeArea.BodyEntered += OnBodyEntered;
        _freezeArea.BodyExited += OnBodyExited;

        AddChild(_freezeArea);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.6f, 0.9f, 1f);
        _light.LightEnergy = 1.5f;
        _light.OmniRange = _radius + 2f;
        _light.Position = new Vector3(0, _radius, 0);
        AddChild(_light);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("Enemies") && !_frozenEnemies.Contains(body))
        {
            FreezeEnemy(body);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (_frozenEnemies.Contains(body))
        {
            UnfreezeEnemy(body);
        }
    }

    private void FreezeEnemy(Node3D enemy)
    {
        _frozenEnemies.Add(enemy);

        // Notify enemy it's frozen
        if (enemy.HasMethod("SetFrozen"))
        {
            enemy.Call("SetFrozen", true);
        }

        // Visual: add ice tint
        if (enemy is Node3D node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
                {
                    // Store original and apply ice tint
                    mat.AlbedoColor = mat.AlbedoColor.Lerp(new Color(0.7f, 0.9f, 1f), 0.5f);
                }
            }
        }

        GD.Print($"[TimestopBubble] Froze {enemy.Name}");
    }

    private void UnfreezeEnemy(Node3D enemy)
    {
        _frozenEnemies.Remove(enemy);

        if (!IsInstanceValid(enemy)) return;

        // Notify enemy it's unfrozen
        if (enemy.HasMethod("SetFrozen"))
        {
            enemy.Call("SetFrozen", false);
        }

        GD.Print($"[TimestopBubble] Unfroze {enemy.Name}");
    }

    /// <summary>
    /// Check if an enemy is currently frozen by this timestop bubble.
    /// </summary>
    public bool IsEnemyFrozen(Node3D enemy)
    {
        return _frozenEnemies.Contains(enemy);
    }

    /// <summary>
    /// Static check if any timestop bubble is freezing an enemy.
    /// </summary>
    public static bool IsAnyBubbleFreezingEnemy(Node3D enemy)
    {
        if (Instance != null && GodotObject.IsInstanceValid(Instance))
        {
            return Instance.IsEnemyFrozen(enemy);
        }
        return false;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        _timeRemaining -= dt;
        _pulseTimer += dt;

        // Slow pulse
        float pulse = (Mathf.Sin(_pulseTimer * 2f) + 1f) * 0.5f;

        if (_material != null)
        {
            _material.AlbedoColor = new Color(0.6f, 0.9f, 1f, 0.2f + pulse * 0.1f);
        }

        if (_light != null)
        {
            _light.LightEnergy = 1f + pulse * 0.5f;
        }

        // Check expiration
        if (_timeRemaining <= 0)
        {
            Expire();
        }
    }

    private void Expire()
    {
        // Unfreeze all enemies
        foreach (var enemy in _frozenEnemies.ToArray())
        {
            if (IsInstanceValid(enemy))
            {
                UnfreezeEnemy(enemy);
            }
        }
        _frozenEnemies.Clear();

        // Disable area
        if (_freezeArea != null) _freezeArea.CollisionMask = 0;

        // Shatter animation
        var tween = CreateTween();
        tween.SetParallel(true);

        if (_bubbleMesh != null)
        {
            tween.TweenProperty(_bubbleMesh, "scale", Vector3.One * 1.2f, 0.2f);
        }
        if (_material != null)
        {
            tween.TweenProperty(_material, "albedo_color:a", 0f, 0.2f);
        }
        if (_light != null)
        {
            // Flash then fade
            _light.LightEnergy = 5f;
            tween.TweenProperty(_light, "light_energy", 0f, 0.2f);
        }

        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() =>
        {
            if (Instance == this) Instance = null;
            QueueFree();
        }));
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;

        // Make sure all enemies are unfrozen
        foreach (var enemy in _frozenEnemies)
        {
            if (IsInstanceValid(enemy) && enemy.HasMethod("SetFrozen"))
            {
                enemy.Call("SetFrozen", false);
            }
        }
    }
}
