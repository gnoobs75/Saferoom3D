using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Gravity Well spell - Create a singularity that pulls enemies toward center.
/// Targeted control spell for grouping enemies.
/// </summary>
public partial class GravityWell3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "gravity_well";
    public override string AbilityName => "Gravity Well";
    public override string Description => "Create a singularity that pulls enemies toward its center.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 45f;
    public override int DefaultManaCost => 20;
    public override int RequiredLevel => 5;

    // Gravity Well stats
    public float Duration { get; private set; } = 5f;
    public float Radius { get; private set; } = 15.625f; // 250px / 16
    public float PullForce { get; private set; } = 25f; // 400px / 16

    // ITargetedAbility
    public float TargetRadius => Radius;
    public Color TargetColor => new(0.6f, 0.3f, 0.8f); // Purple

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Create the gravity well effect
        var effect = new GravityWellEffect3D();
        effect.Name = "GravityWellEffect";
        effect.GlobalPosition = _targetPosition;
        effect.Initialize(Duration, Radius, PullForce);

        GetTree().Root.AddChild(effect);

        GD.Print($"[GravityWell3D] Created at {_targetPosition}");
    }
}

/// <summary>
/// Visual and physics effect for gravity well.
/// </summary>
public partial class GravityWellEffect3D : Node3D
{
    private float _duration;
    private float _radius;
    private float _pullForce;
    private float _timeRemaining;

    private MeshInstance3D? _coreMesh;
    private MeshInstance3D? _ringMesh;
    private OmniLight3D? _light;
    private GpuParticles3D? _particles;
    private Area3D? _pullArea;

    private StandardMaterial3D? _coreMaterial;
    private float _rotationAngle;
    private List<Node3D> _affectedEnemies = new();

    public void Initialize(float duration, float radius, float pullForce)
    {
        _duration = duration;
        _radius = radius;
        _pullForce = pullForce;
        _timeRemaining = duration;
    }

    public override void _Ready()
    {
        CreateCoreVisual();
        CreateRingVisual();
        CreatePullArea();
        CreateLight();
        CreateParticles();

        Core.SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);
    }

    private void CreateCoreVisual()
    {
        // Dark core singularity
        _coreMesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.5f;
        sphere.Height = 1f;
        _coreMesh.Mesh = sphere;

        _coreMaterial = new StandardMaterial3D();
        _coreMaterial.AlbedoColor = new Color(0.1f, 0.05f, 0.15f);
        _coreMaterial.EmissionEnabled = true;
        _coreMaterial.Emission = new Color(0.4f, 0.2f, 0.6f);
        _coreMaterial.EmissionEnergyMultiplier = 2f;
        _coreMesh.MaterialOverride = _coreMaterial;

        _coreMesh.Position = new Vector3(0, 1f, 0);
        AddChild(_coreMesh);
    }

    private void CreateRingVisual()
    {
        // Accretion disk
        _ringMesh = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = _radius * 0.8f;
        torus.OuterRadius = _radius;
        torus.Rings = 32;
        torus.RingSegments = 8;
        _ringMesh.Mesh = torus;

        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.5f, 0.3f, 0.7f, 0.4f);
        material.EmissionEnabled = true;
        material.Emission = new Color(0.4f, 0.2f, 0.6f);
        material.EmissionEnergyMultiplier = 1f;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _ringMesh.MaterialOverride = material;

        _ringMesh.Position = new Vector3(0, 0.5f, 0);
        _ringMesh.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(_ringMesh);
    }

    private void CreatePullArea()
    {
        _pullArea = new Area3D();
        _pullArea.Name = "PullArea";

        var shape = new CollisionShape3D();
        var sphereShape = new SphereShape3D();
        sphereShape.Radius = _radius;
        shape.Shape = sphereShape;
        shape.Position = new Vector3(0, 1f, 0);

        _pullArea.AddChild(shape);
        _pullArea.CollisionLayer = 0;
        _pullArea.CollisionMask = 2; // Enemy layer

        _pullArea.BodyEntered += OnBodyEntered;
        _pullArea.BodyExited += OnBodyExited;

        AddChild(_pullArea);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.5f, 0.3f, 0.8f);
        _light.LightEnergy = 2f;
        _light.OmniRange = _radius + 2f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 100;
        _particles.Lifetime = 1f;
        _particles.Explosiveness = 0f;
        _particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = _radius;
        material.Direction = new Vector3(0, 0, 0);
        material.Spread = 180f;
        material.InitialVelocityMin = 0f;
        material.InitialVelocityMax = 0f;
        material.RadialAccelMin = -10f; // Pull toward center
        material.RadialAccelMax = -15f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.15f;
        material.Color = new Color(0.6f, 0.4f, 0.9f);

        _particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.2f, 0.2f);
        _particles.DrawPass1 = quadMesh;

        _particles.Position = new Vector3(0, 1f, 0);
        AddChild(_particles);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("Enemies") && !_affectedEnemies.Contains(body))
        {
            _affectedEnemies.Add(body);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        _affectedEnemies.Remove(body);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        _timeRemaining -= dt;

        // Pull all affected enemies toward center
        Vector3 center = GlobalPosition + new Vector3(0, 1f, 0);

        for (int i = _affectedEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = _affectedEnemies[i];
            if (!IsInstanceValid(enemy))
            {
                _affectedEnemies.RemoveAt(i);
                continue;
            }

            Vector3 pullDir = (center - enemy.GlobalPosition).Normalized();
            float dist = enemy.GlobalPosition.DistanceTo(center);

            // Pull force increases as enemy gets closer (inverse square would be too strong)
            float pullStrength = _pullForce * (1f - dist / _radius);
            pullStrength = Mathf.Max(pullStrength, _pullForce * 0.3f);

            // Apply pull via velocity or knockback
            if (enemy is CharacterBody3D charBody)
            {
                // Direct velocity modification for CharacterBody3D
                Vector3 currentVel = charBody.Velocity;
                Vector3 pullVel = pullDir * pullStrength * dt * 10f;
                charBody.Velocity = currentVel + pullVel;
            }
            else if (enemy.HasMethod("ApplyKnockback"))
            {
                enemy.Call("ApplyKnockback", pullDir, pullStrength * dt);
            }
        }

        // Check if expired
        if (_timeRemaining <= 0)
        {
            Collapse();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Rotate ring and core
        _rotationAngle += dt * 2f;
        if (_ringMesh != null)
        {
            _ringMesh.Rotation = new Vector3(Mathf.Pi / 2, _rotationAngle, 0);
        }
        if (_coreMesh != null)
        {
            _coreMesh.Rotation = new Vector3(0, _rotationAngle * 3f, 0);

            // Pulse core
            float pulse = (Mathf.Sin(_rotationAngle * 4f) + 1f) * 0.5f;
            _coreMesh.Scale = Vector3.One * (1f + pulse * 0.2f);
        }

        // Pulse light
        if (_light != null)
        {
            float pulse = (Mathf.Sin(_rotationAngle * 2f) + 1f) * 0.5f;
            _light.LightEnergy = 2f + pulse;
        }
    }

    private void Collapse()
    {
        // Stop affecting enemies
        _affectedEnemies.Clear();

        // Disable area
        if (_pullArea != null) _pullArea.CollisionMask = 0;

        // Stop particles
        if (_particles != null) _particles.Emitting = false;

        // Implosion animation
        var tween = CreateTween();
        tween.SetParallel(true);

        if (_coreMesh != null)
        {
            tween.TweenProperty(_coreMesh, "scale", Vector3.Zero, 0.3f).SetEase(Tween.EaseType.In);
        }
        if (_ringMesh != null)
        {
            tween.TweenProperty(_ringMesh, "scale", Vector3.Zero, 0.3f).SetEase(Tween.EaseType.In);
        }
        if (_light != null)
        {
            tween.TweenProperty(_light, "light_energy", 0f, 0.3f);
        }
        if (_coreMaterial != null)
        {
            tween.TweenProperty(_coreMaterial, "emission_energy_multiplier", 5f, 0.1f);
        }

        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
