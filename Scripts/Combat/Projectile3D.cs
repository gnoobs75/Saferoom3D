using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.Combat;

/// <summary>
/// 3D projectile for ranged attacks. Can be player or enemy projectile.
/// Ported from 2D Projectile with 3D physics and visuals.
/// </summary>
public partial class Projectile3D : Area3D
{
    // Configuration
    public float Speed { get; set; } = 15f;
    public float Damage { get; set; } = 10f;
    public float Lifetime { get; set; } = 5f;
    public bool IsPlayerProjectile { get; set; } = true;
    public string Source { get; set; } = "Unknown";

    // Visual
    public Color ProjectileColor { get; set; } = new(0.4f, 0.7f, 1f); // Default blue

    // State
    private Vector3 _direction;
    private float _lifeTimer;
    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;
    private GpuParticles3D? _trail;
    private Vector3? _pendingPosition; // Position to set once in scene tree

    // Signals
    [Signal] public delegate void HitEventHandler(Node3D target);
    [Signal] public delegate void ExpiredEventHandler();

    public override void _Ready()
    {
        // Apply pending position now that we're in the scene tree
        if (_pendingPosition.HasValue)
        {
            GlobalPosition = _pendingPosition.Value;
            _pendingPosition = null;
        }

        SetupVisuals();
        SetupCollision();

        _lifeTimer = Lifetime;

        // Add to appropriate group
        if (IsPlayerProjectile)
        {
            AddToGroup("PlayerProjectiles");
        }
        else
        {
            AddToGroup("EnemyProjectiles");
        }

        // Connect collision signal
        BodyEntered += OnBodyEntered;
    }

    private void SetupVisuals()
    {
        // Create glowing sphere mesh
        _mesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.15f;
        sphere.Height = 0.3f;
        _mesh.Mesh = sphere;

        // Glowing material
        var material = new StandardMaterial3D();
        material.AlbedoColor = ProjectileColor;
        material.EmissionEnabled = true;
        material.Emission = ProjectileColor;
        material.EmissionEnergyMultiplier = 2f;
        _mesh.MaterialOverride = material;

        AddChild(_mesh);

        // Add point light
        _light = new OmniLight3D();
        _light.LightColor = ProjectileColor;
        _light.LightEnergy = 1.5f;
        _light.OmniRange = 3f;
        _light.ShadowEnabled = false;
        AddChild(_light);

        // Add trail particles
        SetupTrailParticles();
    }

    private void SetupTrailParticles()
    {
        _trail = new GpuParticles3D();
        _trail.Amount = 20;
        _trail.Lifetime = 0.3f;
        _trail.Preprocess = 0;
        _trail.SpeedScale = 1f;
        _trail.Explosiveness = 0f;
        _trail.Randomness = 0.2f;

        // Create particle process material
        var processMat = new ParticleProcessMaterial();
        processMat.Direction = new Vector3(0, 0, 0);
        processMat.Spread = 10f;
        processMat.InitialVelocityMin = 0.5f;
        processMat.InitialVelocityMax = 1f;
        processMat.Gravity = Vector3.Zero;
        processMat.ScaleMin = 0.5f;
        processMat.ScaleMax = 1f;
        _trail.ProcessMaterial = processMat;

        // Create mesh for particles
        var particleMesh = new SphereMesh();
        particleMesh.Radius = 0.05f;
        particleMesh.Height = 0.1f;
        _trail.DrawPass1 = particleMesh;

        // Particle material
        var drawMat = new StandardMaterial3D();
        drawMat.AlbedoColor = ProjectileColor.Lightened(0.3f);
        drawMat.EmissionEnabled = true;
        drawMat.Emission = ProjectileColor;
        drawMat.EmissionEnergyMultiplier = 1f;
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        particleMesh.Material = drawMat;

        AddChild(_trail);
    }

    private void SetupCollision()
    {
        var collider = new CollisionShape3D();
        var sphere = new SphereShape3D();
        sphere.Radius = 0.2f;
        collider.Shape = sphere;
        AddChild(collider);

        // Set collision layers based on source
        if (IsPlayerProjectile)
        {
            CollisionLayer = 4;  // PlayerAttack layer
            CollisionMask = 2;   // Enemy layer
        }
        else
        {
            CollisionLayer = 4;  // EnemyAttack layer - using same for simplicity
            CollisionMask = 1;   // Player layer
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Move in direction
        GlobalPosition += _direction * Speed * dt;

        // Lifetime
        _lifeTimer -= dt;
        if (_lifeTimer <= 0)
        {
            EmitSignal(SignalName.Expired);
            Destroy();
        }

        // Fade out near end of life
        if (_lifeTimer < 0.5f && _light != null)
        {
            _light.LightEnergy = Mathf.Lerp(0, 1.5f, _lifeTimer / 0.5f);
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (IsPlayerProjectile)
        {
            // Hit enemy
            if (body is BasicEnemy3D enemy)
            {
                enemy.TakeDamage(Damage, GlobalPosition, Source);
                EmitSignal(SignalName.Hit, body);
                SpawnImpactEffect();
                Destroy();
            }
            else if (body is BossEnemy3D boss)
            {
                boss.TakeDamage(Damage, GlobalPosition, Source);
                EmitSignal(SignalName.Hit, body);
                SpawnImpactEffect();
                Destroy();
            }
        }
        else
        {
            // Hit player
            if (body.HasMethod("TakeDamage"))
            {
                body.Call("TakeDamage", Damage, GlobalPosition, Source);
                EmitSignal(SignalName.Hit, body);
                SpawnImpactEffect();
                Destroy();
            }
        }

        // Hit wall/obstacle
        if (body.IsInGroup("Walls") || body.IsInGroup("Obstacles"))
        {
            SpawnImpactEffect();
            Destroy();
        }
    }

    private void SpawnImpactEffect()
    {
        // Cache position before we might be removed from tree
        var impactPosition = IsInsideTree() ? GlobalPosition : Position;

        // Play impact sound
        SoundManager3D.Instance?.PlayHitSound(impactPosition);

        // Create impact flash
        var flash = new OmniLight3D();
        flash.LightColor = ProjectileColor;
        flash.LightEnergy = 3f;
        flash.OmniRange = 2f;

        var parent = GetParent();
        if (parent != null)
        {
            parent.AddChild(flash);
            // Set position after adding to tree
            flash.GlobalPosition = impactPosition;

            // Fade and remove
            var tween = flash.CreateTween();
            tween.TweenProperty(flash, "light_energy", 0f, 0.2f);
            tween.TweenCallback(Callable.From(() => flash.QueueFree()));
        }
    }

    private void Destroy()
    {
        QueueFree();
    }

    /// <summary>
    /// Initialize and fire the projectile
    /// </summary>
    public void Fire(Vector3 direction)
    {
        _direction = direction.Normalized();

        // Face the direction of travel
        if (_direction != Vector3.Zero)
        {
            LookAt(GlobalPosition + _direction);
        }
    }

    /// <summary>
    /// Static factory to create a projectile
    /// </summary>
    public static Projectile3D Create(
        Vector3 position,
        Vector3 direction,
        float damage,
        bool isPlayerProjectile,
        Color? color = null,
        string source = "Projectile")
    {
        var projectile = new Projectile3D();
        // Store position for later - can't set GlobalPosition before adding to scene tree
        projectile._pendingPosition = position;
        projectile.Damage = damage;
        projectile.IsPlayerProjectile = isPlayerProjectile;
        projectile.Source = source;

        if (color.HasValue)
        {
            projectile.ProjectileColor = color.Value;
        }
        else
        {
            // Default colors based on source
            projectile.ProjectileColor = isPlayerProjectile
                ? new Color(0.4f, 0.7f, 1f)   // Blue for player
                : new Color(1f, 0.4f, 0.3f);  // Red for enemy
        }

        return projectile;
    }
}
