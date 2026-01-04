using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.Combat;

/// <summary>
/// Type of thrown projectile for GoblinThrower
/// </summary>
public enum ThrowableType
{
    Spear,
    ThrowingAxe,
    BeerCan
}

/// <summary>
/// 3D thrown projectile for GoblinThrower - spears, axes, beer cans.
/// Rotates as it flies through the air.
/// </summary>
public partial class ThrownProjectile3D : Area3D
{
    // Configuration
    public float Speed { get; set; } = 18f;
    public float Damage { get; set; } = 12f;
    public float Lifetime { get; set; } = 4f;
    public ThrowableType ProjectileType { get; set; } = ThrowableType.Spear;
    public Color ProjectileColor { get; set; } = new(0.5f, 0.5f, 0.5f);

    // State
    private Vector3 _direction;
    private float _lifeTimer;
    private float _rotationSpeed;
    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;
    private GpuParticles3D? _trail;

    // Signals
    [Signal] public delegate void HitEventHandler(Node3D target);
    [Signal] public delegate void ExpiredEventHandler();

    public override void _Ready()
    {
        SetupVisuals();
        SetupCollision();

        _lifeTimer = Lifetime;
        _rotationSpeed = ProjectileType switch
        {
            ThrowableType.Spear => 0f, // Spears don't spin
            ThrowableType.ThrowingAxe => 15f, // Axes spin fast
            ThrowableType.BeerCan => 8f, // Cans tumble
            _ => 5f
        };

        AddToGroup("EnemyProjectiles");
        BodyEntered += OnBodyEntered;
    }

    private void SetupVisuals()
    {
        _mesh = new MeshInstance3D();
        var material = new StandardMaterial3D();
        material.AlbedoColor = ProjectileColor;

        switch (ProjectileType)
        {
            case ThrowableType.Spear:
                CreateSpearMesh(material);
                break;
            case ThrowableType.ThrowingAxe:
                CreateAxeMesh(material);
                break;
            case ThrowableType.BeerCan:
                CreateBeerCanMesh(material);
                break;
        }

        AddChild(_mesh);

        // Add subtle light for visibility
        _light = new OmniLight3D();
        _light.LightColor = ProjectileColor.Lightened(0.3f);
        _light.LightEnergy = 0.5f;
        _light.OmniRange = 2f;
        _light.ShadowEnabled = false;
        AddChild(_light);

        // Minimal trail
        SetupTrailParticles();
    }

    private void CreateSpearMesh(StandardMaterial3D material)
    {
        // Spear shaft with wood grain texture
        var shaft = new CylinderMesh();
        shaft.TopRadius = 0.028f;
        shaft.BottomRadius = 0.032f;
        shaft.Height = 1.2f;
        _mesh!.Mesh = shaft;

        material.AlbedoColor = new Color(0.5f, 0.35f, 0.2f); // Wood color
        material.Roughness = 0.75f;
        _mesh.MaterialOverride = material;

        // Leather grip wrapping
        var wrapMat = new StandardMaterial3D();
        wrapMat.AlbedoColor = new Color(0.35f, 0.25f, 0.15f);
        wrapMat.Roughness = 0.9f;

        for (int i = 0; i < 3; i++)
        {
            var wrap = new MeshInstance3D();
            var wrapMesh = new TorusMesh { InnerRadius = 0.028f, OuterRadius = 0.035f };
            wrap.Mesh = wrapMesh;
            wrap.MaterialOverride = wrapMat;
            wrap.Position = new Vector3(0, -0.3f + i * 0.15f, 0);
            wrap.RotationDegrees = new Vector3(90, 0, 0);
            wrap.Scale = new Vector3(1f, 1f, 0.4f);
            _mesh.AddChild(wrap);
        }

        // Metal spear tip (tapered blade)
        var tipMesh = new MeshInstance3D();
        var tip = new CylinderMesh();
        tip.TopRadius = 0f;
        tip.BottomRadius = 0.045f;
        tip.Height = 0.3f;
        tipMesh.Mesh = tip;

        var tipMat = new StandardMaterial3D();
        tipMat.AlbedoColor = new Color(0.7f, 0.7f, 0.75f); // Shiny metal
        tipMat.Metallic = 0.9f;
        tipMat.Roughness = 0.3f;
        tipMesh.MaterialOverride = tipMat;
        tipMesh.Position = new Vector3(0, 0.75f, 0);
        _mesh.AddChild(tipMesh);

        // Spear socket (metal fitting where tip meets shaft)
        var socket = new MeshInstance3D();
        var socketMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.032f, Height = 0.08f };
        socket.Mesh = socketMesh;
        socket.MaterialOverride = tipMat;
        socket.Position = new Vector3(0, 0.58f, 0);
        _mesh.AddChild(socket);

        // Rotate to point forward (Z axis)
        _mesh.RotationDegrees = new Vector3(90, 0, 0);
    }

    private void CreateAxeMesh(StandardMaterial3D material)
    {
        // Axe handle (tapered)
        var handle = new CylinderMesh();
        handle.TopRadius = 0.02f;
        handle.BottomRadius = 0.028f;
        handle.Height = 0.5f;
        _mesh!.Mesh = handle;

        material.AlbedoColor = new Color(0.45f, 0.3f, 0.15f); // Dark wood
        material.Roughness = 0.8f;
        _mesh.MaterialOverride = material;

        // Handle wrap at grip
        var wrapMat = new StandardMaterial3D();
        wrapMat.AlbedoColor = new Color(0.35f, 0.2f, 0.1f);
        wrapMat.Roughness = 0.9f;

        var grip = new MeshInstance3D();
        var gripMesh = new CylinderMesh { TopRadius = 0.032f, BottomRadius = 0.032f, Height = 0.15f };
        grip.Mesh = gripMesh;
        grip.MaterialOverride = wrapMat;
        grip.Position = new Vector3(0, -0.1f, 0);
        _mesh.AddChild(grip);

        // Curved axe blade (main cutting edge)
        var bladeMesh = new MeshInstance3D();
        var blade = new BoxMesh();
        blade.Size = new Vector3(0.28f, 0.16f, 0.025f);
        bladeMesh.Mesh = blade;

        var bladeMat = new StandardMaterial3D();
        bladeMat.AlbedoColor = new Color(0.65f, 0.65f, 0.7f); // Polished metal
        bladeMat.Metallic = 0.95f;
        bladeMat.Roughness = 0.25f;
        bladeMesh.MaterialOverride = bladeMat;
        bladeMesh.Position = new Vector3(0.13f, 0.22f, 0);
        bladeMesh.RotationDegrees = new Vector3(0, 0, -5); // Slight curve
        _mesh.AddChild(bladeMesh);

        // Axe spine (back edge for weight)
        var spine = new MeshInstance3D();
        var spineMesh = new BoxMesh { Size = new Vector3(0.15f, 0.08f, 0.03f) };
        spine.Mesh = spineMesh;
        spine.MaterialOverride = bladeMat;
        spine.Position = new Vector3(-0.06f, 0.22f, 0);
        _mesh.AddChild(spine);

        // Axe socket (where blade meets handle)
        var socket = new MeshInstance3D();
        var socketMesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.025f, Height = 0.12f };
        socket.Mesh = socketMesh;
        socket.MaterialOverride = bladeMat;
        socket.Position = new Vector3(0, 0.18f, 0);
        socket.RotationDegrees = new Vector3(90, 0, 0);
        _mesh.AddChild(socket);

        _mesh.RotationDegrees = new Vector3(90, 0, 0);
    }

    private void CreateBeerCanMesh(StandardMaterial3D material)
    {
        // Beer can/mug cylinder with slight taper
        var can = new CylinderMesh();
        can.TopRadius = 0.055f;
        can.BottomRadius = 0.06f;
        can.Height = 0.18f;
        _mesh!.Mesh = can;

        // Tan/golden color for beer mug
        material.AlbedoColor = new Color(0.75f, 0.6f, 0.35f);
        material.Metallic = 0.2f;
        material.Roughness = 0.5f;
        _mesh.MaterialOverride = material;

        // Mug handle (loop)
        var handleMat = new StandardMaterial3D();
        handleMat.AlbedoColor = new Color(0.7f, 0.55f, 0.3f);
        handleMat.Roughness = 0.6f;

        var handle = new MeshInstance3D();
        var handleMesh = new TorusMesh { InnerRadius = 0.025f, OuterRadius = 0.04f };
        handle.Mesh = handleMesh;
        handle.MaterialOverride = handleMat;
        handle.Position = new Vector3(0.075f, 0, 0);
        handle.RotationDegrees = new Vector3(0, 90, 0);
        handle.Scale = new Vector3(1.2f, 1f, 0.7f);
        _mesh.AddChild(handle);

        // Top rim
        var rim = new MeshInstance3D();
        var rimMesh = new TorusMesh { InnerRadius = 0.055f, OuterRadius = 0.062f };
        rim.Mesh = rimMesh;
        rim.MaterialOverride = handleMat;
        rim.Position = new Vector3(0, 0.09f, 0);
        rim.RotationDegrees = new Vector3(90, 0, 0);
        rim.Scale = new Vector3(1f, 1f, 0.3f);
        _mesh.AddChild(rim);

        // Bottom base
        var base_ = new MeshInstance3D();
        var baseMesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.065f, Height = 0.02f };
        base_.Mesh = baseMesh;
        base_.MaterialOverride = handleMat;
        base_.Position = new Vector3(0, -0.1f, 0);
        _mesh.AddChild(base_);

        // Foam spilling over top
        var foamMat = new StandardMaterial3D();
        foamMat.AlbedoColor = new Color(0.95f, 0.92f, 0.85f);
        foamMat.EmissionEnabled = true;
        foamMat.Emission = new Color(0.9f, 0.88f, 0.8f);
        foamMat.EmissionEnergyMultiplier = 0.3f;
        foamMat.Roughness = 0.9f;

        var foam = new MeshInstance3D();
        var foamMesh = new SphereMesh { Radius = 0.045f, Height = 0.09f };
        foam.Mesh = foamMesh;
        foam.MaterialOverride = foamMat;
        foam.Position = new Vector3(0, 0.12f, 0);
        foam.Scale = new Vector3(1.2f, 0.6f, 1.2f);
        _mesh.AddChild(foam);

        // Label band around can
        var labelMat = new StandardMaterial3D();
        labelMat.AlbedoColor = new Color(0.85f, 0.2f, 0.15f); // Red label
        labelMat.Roughness = 0.7f;

        var label = new MeshInstance3D();
        var labelMesh = new CylinderMesh { TopRadius = 0.062f, BottomRadius = 0.062f, Height = 0.08f };
        label.Mesh = labelMesh;
        label.MaterialOverride = labelMat;
        label.Position = new Vector3(0, 0, 0);
        _mesh.AddChild(label);

        _mesh.RotationDegrees = new Vector3(90, 0, 0);
    }

    private void SetupTrailParticles()
    {
        _trail = new GpuParticles3D();
        _trail.Amount = 10;
        _trail.Lifetime = 0.2f;
        _trail.Emitting = true;

        var processMat = new ParticleProcessMaterial();
        processMat.Direction = new Vector3(0, 0, 1); // Trail behind
        processMat.Spread = 5f;
        processMat.InitialVelocityMin = 0.2f;
        processMat.InitialVelocityMax = 0.5f;
        processMat.Gravity = Vector3.Zero;
        processMat.ScaleMin = 0.3f;
        processMat.ScaleMax = 0.6f;
        _trail.ProcessMaterial = processMat;

        var particleMesh = new SphereMesh();
        particleMesh.Radius = 0.03f;
        particleMesh.Height = 0.06f;

        var drawMat = new StandardMaterial3D();
        drawMat.AlbedoColor = ProjectileColor.Lightened(0.2f);
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        particleMesh.Material = drawMat;
        _trail.DrawPass1 = particleMesh;

        AddChild(_trail);
    }

    private void SetupCollision()
    {
        var collider = new CollisionShape3D();
        var capsule = new CapsuleShape3D();
        capsule.Radius = 0.15f;
        capsule.Height = 0.5f;
        collider.Shape = capsule;
        collider.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(collider);

        // Enemy projectile - hits player
        CollisionLayer = 4;  // EnemyAttack layer
        CollisionMask = 1;   // Player layer
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Move in direction
        GlobalPosition += _direction * Speed * dt;

        // Rotate the projectile (spears stay stable, axes/cans spin)
        if (_rotationSpeed > 0 && _mesh != null)
        {
            if (ProjectileType == ThrowableType.ThrowingAxe)
            {
                // Axes spin around horizontal axis
                _mesh.RotateX(dt * _rotationSpeed);
            }
            else
            {
                // Cans tumble
                _mesh.RotateZ(dt * _rotationSpeed);
            }
        }

        // Lifetime
        _lifeTimer -= dt;
        if (_lifeTimer <= 0)
        {
            EmitSignal(SignalName.Expired);
            Destroy();
        }

        // Fade out near end
        if (_lifeTimer < 0.5f && _light != null)
        {
            _light.LightEnergy = Mathf.Lerp(0, 0.5f, _lifeTimer / 0.5f);
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        // Hit player
        if (body.HasMethod("TakeDamage"))
        {
            string sourceName = ProjectileType switch
            {
                ThrowableType.Spear => "Goblin Thrower (Spear)",
                ThrowableType.ThrowingAxe => "Goblin Thrower (Axe)",
                ThrowableType.BeerCan => "Goblin Thrower (Beer Can)",
                _ => "Goblin Thrower"
            };
            body.Call("TakeDamage", Damage, GlobalPosition, sourceName);
            EmitSignal(SignalName.Hit, body);
            SpawnImpactEffect();
            Destroy();
            return;
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
        SoundManager3D.Instance?.PlayHitSound(GlobalPosition);

        var flash = new OmniLight3D();
        flash.LightColor = ProjectileColor;
        flash.LightEnergy = 2f;
        flash.OmniRange = 1.5f;
        flash.GlobalPosition = GlobalPosition;

        if (GetParent() != null)
        {
            GetParent().AddChild(flash);
            var tween = flash.CreateTween();
            tween.TweenProperty(flash, "light_energy", 0f, 0.15f);
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

        if (_direction != Vector3.Zero)
        {
            LookAt(GlobalPosition + _direction);
        }
    }

    /// <summary>
    /// Static factory to create a thrown projectile
    /// </summary>
    public static ThrownProjectile3D Create(
        Vector3 position,
        Vector3 direction,
        float damage,
        ThrowableType type,
        Color? color = null)
    {
        var projectile = new ThrownProjectile3D();
        projectile.GlobalPosition = position;
        projectile.Damage = damage;
        projectile.ProjectileType = type;

        if (color.HasValue)
        {
            projectile.ProjectileColor = color.Value;
        }
        else
        {
            projectile.ProjectileColor = type switch
            {
                ThrowableType.Spear => new Color(0.5f, 0.35f, 0.2f),
                ThrowableType.ThrowingAxe => new Color(0.5f, 0.5f, 0.55f),
                ThrowableType.BeerCan => new Color(0.8f, 0.65f, 0.3f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
        }

        return projectile;
    }
}
