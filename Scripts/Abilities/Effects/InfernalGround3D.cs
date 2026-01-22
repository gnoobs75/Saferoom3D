using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Infernal Ground spell - Create a fire zone that damages enemies over time.
/// Targeted DOT (damage over time) zone spell.
/// </summary>
public partial class InfernalGround3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "infernal_ground";
    public override string AbilityName => "Infernal Ground";
    public override string Description => "Create a fire zone that burns enemies standing in it.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 15f;
    public override int DefaultManaCost => 35;
    public override int RequiredLevel => 4;

    // Infernal Ground stats
    public float DamagePerTick { get; private set; } = 15f;
    public float TickInterval { get; private set; } = 0.5f; // 30 DPS
    public float Duration { get; private set; } = 8f;
    public float Radius { get; private set; } = 7.5f; // 120px / 16

    // ITargetedAbility
    public float TargetRadius => Radius;
    public Color TargetColor => new(1f, 0.4f, 0.1f); // Orange/fire

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Create the fire zone
        var effect = new InfernalGroundEffect3D();
        effect.Name = "InfernalGroundEffect";
        effect.GlobalPosition = _targetPosition;
        effect.Initialize(DamagePerTick, TickInterval, Duration, Radius);

        GetTree().Root.AddChild(effect);

        GD.Print($"[InfernalGround3D] Created at {_targetPosition}");
    }
}

/// <summary>
/// Visual and damage effect for infernal ground.
/// </summary>
public partial class InfernalGroundEffect3D : Node3D
{
    private float _damagePerTick;
    private float _tickInterval;
    private float _duration;
    private float _radius;
    private float _timeRemaining;
    private float _tickTimer;

    private MeshInstance3D? _groundMesh;
    private OmniLight3D? _light;
    private GpuParticles3D? _fireParticles;
    private Area3D? _damageArea;

    private StandardMaterial3D? _material;
    private float _animTimer;
    private HashSet<Node3D> _enemiesInZone = new();

    public void Initialize(float damagePerTick, float tickInterval, float duration, float radius)
    {
        _damagePerTick = damagePerTick;
        _tickInterval = tickInterval;
        _duration = duration;
        _radius = radius;
        _timeRemaining = duration;
        _tickTimer = tickInterval; // Start with a tick
    }

    public override void _Ready()
    {
        CreateGroundVisual();
        CreateDamageArea();
        CreateLight();
        CreateFireParticles();

        Core.SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);
    }

    private void CreateGroundVisual()
    {
        _groundMesh = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = _radius;
        cylinder.BottomRadius = _radius;
        cylinder.Height = 0.1f;
        _groundMesh.Mesh = cylinder;

        // Fire/lava material
        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(1f, 0.3f, 0.1f, 0.8f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(1f, 0.4f, 0f);
        _material.EmissionEnergyMultiplier = 2f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _groundMesh.MaterialOverride = _material;

        _groundMesh.Position = new Vector3(0, 0.05f, 0);
        AddChild(_groundMesh);

        // Add inner glow circle
        var inner = new MeshInstance3D();
        var innerCyl = new CylinderMesh();
        innerCyl.TopRadius = _radius * 0.7f;
        innerCyl.BottomRadius = _radius * 0.7f;
        innerCyl.Height = 0.12f;
        inner.Mesh = innerCyl;

        var innerMat = new StandardMaterial3D();
        innerMat.AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.6f);
        innerMat.EmissionEnabled = true;
        innerMat.Emission = new Color(1f, 0.5f, 0.1f);
        innerMat.EmissionEnergyMultiplier = 3f;
        innerMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        innerMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        inner.MaterialOverride = innerMat;

        inner.Position = new Vector3(0, 0.06f, 0);
        AddChild(inner);
    }

    private void CreateDamageArea()
    {
        _damageArea = new Area3D();
        _damageArea.Name = "DamageArea";

        var shape = new CollisionShape3D();
        var cylinder = new CylinderShape3D();
        cylinder.Radius = _radius;
        cylinder.Height = 3f; // Tall enough to catch enemies
        shape.Shape = cylinder;
        shape.Position = new Vector3(0, 1.5f, 0);

        _damageArea.AddChild(shape);
        _damageArea.CollisionLayer = 0;
        _damageArea.CollisionMask = 2; // Enemy layer

        _damageArea.BodyEntered += OnBodyEntered;
        _damageArea.BodyExited += OnBodyExited;

        AddChild(_damageArea);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(1f, 0.5f, 0.2f);
        _light.LightEnergy = 3f;
        _light.OmniRange = _radius + 3f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);
    }

    private void CreateFireParticles()
    {
        _fireParticles = new GpuParticles3D();
        _fireParticles.Amount = 100;
        _fireParticles.Lifetime = 0.8f;
        _fireParticles.Explosiveness = 0f;
        _fireParticles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = _radius * 0.9f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 20f;
        material.InitialVelocityMin = 2f;
        material.InitialVelocityMax = 4f;
        material.Gravity = new Vector3(0, 0, 0); // Fire rises regardless
        material.ScaleMin = 0.2f;
        material.ScaleMax = 0.5f;

        // Fire color gradient: yellow → orange → red → dark
        var colorRamp = new Gradient();
        colorRamp.Colors = new Color[]
        {
            new Color(1f, 0.9f, 0.3f),
            new Color(1f, 0.5f, 0.1f),
            new Color(0.8f, 0.2f, 0.05f),
            new Color(0.3f, 0.1f, 0.05f, 0f)
        };
        colorRamp.Offsets = new float[] { 0f, 0.3f, 0.6f, 1f };

        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = colorRamp;
        material.ColorRamp = gradientTex;

        _fireParticles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.4f, 0.4f);
        _fireParticles.DrawPass1 = quadMesh;

        _fireParticles.Position = new Vector3(0, 0.2f, 0);
        AddChild(_fireParticles);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("Enemies"))
        {
            _enemiesInZone.Add(body);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        _enemiesInZone.Remove(body);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        _timeRemaining -= dt;
        _tickTimer -= dt;

        // Damage tick
        if (_tickTimer <= 0)
        {
            _tickTimer = _tickInterval;
            DamageEnemiesInZone();
        }

        // Check expiration
        if (_timeRemaining <= 0)
        {
            Expire();
        }
    }

    private void DamageEnemiesInZone()
    {
        foreach (var enemy in _enemiesInZone.ToArray())
        {
            if (!IsInstanceValid(enemy))
            {
                _enemiesInZone.Remove(enemy);
                continue;
            }

            if (enemy.HasMethod("TakeDamage"))
            {
                enemy.Call("TakeDamage", _damagePerTick, GlobalPosition, "Infernal Ground");
            }
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _animTimer += dt;

        // Animate fire intensity
        float flicker = Mathf.Sin(_animTimer * 8f) * 0.3f + Mathf.Sin(_animTimer * 13f) * 0.2f;

        if (_material != null)
        {
            _material.EmissionEnergyMultiplier = 2f + flicker;
        }

        if (_light != null)
        {
            _light.LightEnergy = 3f + flicker * 2f;
        }

        // Fade when about to expire
        if (_timeRemaining < 1f)
        {
            float fade = _timeRemaining;
            if (_material != null)
            {
                _material.AlbedoColor = new Color(1f, 0.3f, 0.1f, 0.8f * fade);
            }
        }
    }

    private void Expire()
    {
        _enemiesInZone.Clear();

        // Disable damage area
        if (_damageArea != null) _damageArea.CollisionMask = 0;

        // Stop particles
        if (_fireParticles != null) _fireParticles.Emitting = false;

        // Fade out
        var tween = CreateTween();
        tween.SetParallel(true);

        if (_material != null)
        {
            tween.TweenProperty(_material, "albedo_color:a", 0f, 0.5f);
        }
        if (_light != null)
        {
            tween.TweenProperty(_light, "light_energy", 0f, 0.5f);
        }

        tween.SetParallel(false);
        tween.TweenInterval(0.5f); // Wait for particles to die
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
