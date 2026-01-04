using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Mirror Image ability - Spawn illusory clones that distract enemies.
/// Defensive ability that creates decoys around the player.
/// </summary>
public partial class MirrorImage3D : Ability3D
{
    public override string AbilityId => "mirror_image";
    public override string AbilityName => "Mirror Image";
    public override string Description => "Spawn illusory clones that distract enemies.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 60f;
    public override int DefaultManaCost => 0;

    // Mirror Image stats
    public float Duration { get; private set; } = 12f;
    public int CloneCount { get; private set; } = 3;
    public float SpawnRadius { get; private set; } = 5f; // 80px / 16

    private List<MirrorClone3D> _clones = new();

    protected override void Activate()
    {
        SpawnClones();
        SetActiveWithDuration(Duration);

        GD.Print($"[MirrorImage3D] Spawned {CloneCount} clones");
    }

    private void SpawnClones()
    {
        Vector3 playerPos = GetPlayerPosition();

        for (int i = 0; i < CloneCount; i++)
        {
            // Position clones in a circle around player
            float angle = (Mathf.Tau / CloneCount) * i;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * SpawnRadius,
                0,
                Mathf.Sin(angle) * SpawnRadius
            );

            var clone = new MirrorClone3D();
            clone.Name = $"MirrorClone_{i}";
            clone.GlobalPosition = playerPos + offset;
            clone.Initialize(this);

            GetTree().Root.AddChild(clone);
            _clones.Add(clone);
        }

        Core.SoundManager3D.Instance?.PlayMagicSound(playerPos);
    }

    protected override void OnActiveTick(float dt)
    {
        // Check if all clones are destroyed
        _clones.RemoveAll(c => !IsInstanceValid(c) || c.IsQueuedForDeletion());

        if (_clones.Count == 0)
        {
            Deactivate();
        }
    }

    protected override void OnDeactivate()
    {
        // Destroy remaining clones
        foreach (var clone in _clones)
        {
            if (IsInstanceValid(clone))
            {
                clone.FadeOut();
            }
        }
        _clones.Clear();

        GD.Print("[MirrorImage3D] All clones dismissed");
    }

    /// <summary>
    /// Called when a clone is destroyed.
    /// </summary>
    public void OnCloneDestroyed(MirrorClone3D clone)
    {
        _clones.Remove(clone);
        GD.Print($"[MirrorImage3D] Clone destroyed, {_clones.Count} remaining");
    }
}

/// <summary>
/// Individual mirror clone that moves randomly and can be destroyed.
/// </summary>
public partial class MirrorClone3D : CharacterBody3D
{
    private MirrorImage3D? _owner;
    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;

    private float _moveTimer;
    private Vector3 _moveDirection;
    private float _moveSpeed = 2f;
    private float _wanderRadius = 8f;
    private Vector3 _spawnPosition;

    private StandardMaterial3D? _material;
    private float _shimmerTimer;

    public void Initialize(MirrorImage3D owner)
    {
        _owner = owner;
        _spawnPosition = GlobalPosition;
    }

    public override void _Ready()
    {
        CreateVisuals();
        SetupCollision();
        PickNewDirection();

        AddToGroup("MirrorClones");
    }

    private void CreateVisuals()
    {
        // Create ghost-like player mesh
        _mesh = new MeshInstance3D();
        var capsule = new CapsuleMesh();
        capsule.Radius = 0.4f;
        capsule.Height = 1.8f;
        _mesh.Mesh = capsule;

        // Semi-transparent blue/white material
        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(0.7f, 0.8f, 1f, 0.5f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(0.5f, 0.6f, 0.9f);
        _material.EmissionEnergyMultiplier = 0.5f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _mesh.MaterialOverride = _material;

        _mesh.Position = new Vector3(0, 0.9f, 0);
        AddChild(_mesh);

        // Subtle glow
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.6f, 0.7f, 1f);
        _light.LightEnergy = 0.5f;
        _light.OmniRange = 2f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);
    }

    private void SetupCollision()
    {
        var collider = new CollisionShape3D();
        var capsule = new CapsuleShape3D();
        capsule.Radius = 0.4f;
        capsule.Height = 1.8f;
        collider.Shape = capsule;
        collider.Position = new Vector3(0, 0.9f, 0);
        AddChild(collider);

        // Can be targeted by enemies
        CollisionLayer = 1; // Same as player
        CollisionMask = 8 | 16; // Wall, Obstacle
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Wander movement
        _moveTimer -= dt;
        if (_moveTimer <= 0)
        {
            PickNewDirection();
        }

        // Move
        Vector3 movement = _moveDirection * _moveSpeed;
        Velocity = new Vector3(movement.X, Velocity.Y, movement.Z);

        // Apply gravity
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - 20f * dt, Velocity.Z);
        }

        MoveAndSlide();

        // Stay within wander radius
        float distFromSpawn = GlobalPosition.DistanceTo(_spawnPosition);
        if (distFromSpawn > _wanderRadius)
        {
            _moveDirection = (_spawnPosition - GlobalPosition).Normalized();
            _moveDirection.Y = 0;
        }
    }

    public override void _Process(double delta)
    {
        // Shimmer effect
        _shimmerTimer += (float)delta;
        float shimmer = (Mathf.Sin(_shimmerTimer * 4f) + 1f) * 0.5f;

        if (_material != null)
        {
            _material.AlbedoColor = new Color(0.7f, 0.8f, 1f, 0.4f + shimmer * 0.2f);
            _material.EmissionEnergyMultiplier = 0.3f + shimmer * 0.4f;
        }

        if (_light != null)
        {
            _light.LightEnergy = 0.3f + shimmer * 0.4f;
        }
    }

    private void PickNewDirection()
    {
        _moveTimer = GD.Randf() * 1.5f + 0.5f;
        float angle = GD.Randf() * Mathf.Tau;
        _moveDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
    }

    /// <summary>
    /// Called when clone takes any damage - immediately destroys it.
    /// </summary>
    public void TakeDamage(float damage, Vector3 fromPosition, string source = "Unknown")
    {
        GD.Print($"[MirrorClone3D] Destroyed by {source}");

        // Notify owner
        _owner?.OnCloneDestroyed(this);

        // Shatter effect
        CreateShatterEffect();

        Core.SoundManager3D.Instance?.PlayMagicSound(GlobalPosition);

        QueueFree();
    }

    private void CreateShatterEffect()
    {
        // Create burst of particles
        var particles = new GpuParticles3D();
        particles.Amount = 30;
        particles.Lifetime = 0.5f;
        particles.Explosiveness = 1f;
        particles.OneShot = true;
        particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.5f;
        material.Direction = new Vector3(0, 0, 0);
        material.Spread = 180f;
        material.InitialVelocityMin = 3f;
        material.InitialVelocityMax = 6f;
        material.Gravity = new Vector3(0, -5, 0);
        material.ScaleMin = 0.1f;
        material.ScaleMax = 0.3f;
        material.Color = new Color(0.7f, 0.8f, 1f, 0.8f);

        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.2f, 0.2f);
        particles.DrawPass1 = quadMesh;

        particles.GlobalPosition = GlobalPosition + new Vector3(0, 1f, 0);
        GetTree().Root.AddChild(particles);

        // Auto-cleanup
        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => particles.QueueFree();
    }

    public void FadeOut()
    {
        var tween = CreateTween();
        tween.SetParallel(true);

        if (_material != null)
        {
            tween.TweenProperty(_material, "albedo_color:a", 0f, 0.3f);
        }
        if (_light != null)
        {
            tween.TweenProperty(_light, "light_energy", 0f, 0.3f);
        }

        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
