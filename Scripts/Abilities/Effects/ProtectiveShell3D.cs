using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Protective Shell ability - Force field that blocks projectiles and pushes enemies.
/// Duration buff that creates a bubble around the player.
/// </summary>
public partial class ProtectiveShell3D : Ability3D
{
    public override string AbilityId => "protective_shell";
    public override string AbilityName => "Protective Shell";
    public override string Description => "Create a force field that blocks projectiles and pushes enemies away.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 120f; // 2 minutes
    public override int DefaultManaCost => 0;

    // Protective Shell stats - tighter to character
    public float Duration { get; private set; } = 15f;
    public float BubbleRadius { get; private set; } = 2.0f; // Tighter bubble around player
    public float PushForce { get; private set; } = 80f; // Stronger push
    public float PushDistance { get; private set; } = 4f; // Push distance

    private ProtectiveShellEffect3D? _shellEffect;

    protected override void Activate()
    {
        // Create the shell effect attached to player
        _shellEffect = new ProtectiveShellEffect3D();
        _shellEffect.Name = "ProtectiveShellEffect";
        _shellEffect.Initialize(BubbleRadius, PushForce);

        // Add as child of player so it follows
        if (Player != null)
        {
            Player.AddChild(_shellEffect);
            _shellEffect.Position = Vector3.Zero;
        }
        else
        {
            GetTree().Root.AddChild(_shellEffect);
        }

        SetActiveWithDuration(Duration);

        GD.Print($"[ProtectiveShell3D] Activated for {Duration}s");
    }

    protected override void OnActiveTick(float dt)
    {
        // Flash warning at 3s remaining
        if (DurationRemaining <= 3f && DurationRemaining > 2.9f)
        {
            _shellEffect?.StartWarningFlash();
        }
    }

    protected override void OnDeactivate()
    {
        if (_shellEffect != null && IsInstanceValid(_shellEffect))
        {
            _shellEffect.FadeOut();
        }
        _shellEffect = null;

        GD.Print("[ProtectiveShell3D] Deactivated");
    }
}

/// <summary>
/// Visual and physics effect for protective shell.
/// </summary>
public partial class ProtectiveShellEffect3D : Node3D
{
    private float _radius;
    private float _pushForce;

    private MeshInstance3D? _bubbleMesh;
    private OmniLight3D? _light;
    private Area3D? _pushArea;
    private Area3D? _projectileBlockArea;

    private StandardMaterial3D? _material;
    private float _pulseTimer;
    private float _rotationAngle;
    private bool _isWarning;
    private HashSet<Node3D> _pushedEnemies = new();

    public void Initialize(float radius, float pushForce)
    {
        _radius = radius;
        _pushForce = pushForce;
    }

    public override void _Ready()
    {
        CreateBubbleVisual();
        CreatePushArea();
        CreateProjectileBlockArea();
        CreateLight();
    }

    private void CreateBubbleVisual()
    {
        // Outer shell layer - visible from outside
        _bubbleMesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = _radius;
        sphere.Height = _radius * 2;
        sphere.RadialSegments = 24;
        sphere.Rings = 12;
        _bubbleMesh.Mesh = sphere;

        // Highly translucent cyan shield material
        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(0.3f, 0.8f, 1f, 0.15f); // More transparent
        _material.EmissionEnabled = true;
        _material.Emission = new Color(0.2f, 0.6f, 0.8f);
        _material.EmissionEnergyMultiplier = 0.8f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // See from both sides
        _material.RimEnabled = true;
        _material.Rim = 1.5f;
        _material.RimTint = 0.8f;
        _bubbleMesh.MaterialOverride = _material;

        _bubbleMesh.Position = new Vector3(0, 1f, 0); // Center on player
        AddChild(_bubbleMesh);

        // Add hexagonal energy lines on the shell
        CreateHexagonalPatterns();

        // Inner glow core
        var innerMesh = new MeshInstance3D();
        var innerSphere = new SphereMesh();
        innerSphere.Radius = _radius * 0.3f;
        innerSphere.Height = _radius * 0.6f;
        innerMesh.Mesh = innerSphere;

        var innerMat = new StandardMaterial3D();
        innerMat.AlbedoColor = new Color(0.5f, 0.9f, 1f, 0.1f);
        innerMat.EmissionEnabled = true;
        innerMat.Emission = new Color(0.4f, 0.8f, 1f);
        innerMat.EmissionEnergyMultiplier = 0.5f;
        innerMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        innerMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        innerMesh.MaterialOverride = innerMat;
        innerMesh.Position = new Vector3(0, 1f, 0);
        AddChild(innerMesh);

        // Add energy particles swirling on the surface
        CreateSurfaceParticles();
    }

    private void CreateHexagonalPatterns()
    {
        // Create ring bands around the shell for energy effect
        var bandMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.9f, 1f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.7f, 0.9f),
            EmissionEnergyMultiplier = 1.5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        // Horizontal bands
        float[] bandHeights = { 0.5f, 1f, 1.5f };
        foreach (float h in bandHeights)
        {
            var band = new MeshInstance3D();
            float bandRadius = Mathf.Sqrt(_radius * _radius - (h - 1f) * (h - 1f)); // Sphere radius at height
            if (bandRadius > 0.2f)
            {
                var torus = new TorusMesh { InnerRadius = bandRadius - 0.02f, OuterRadius = bandRadius + 0.02f };
                band.Mesh = torus;
                band.MaterialOverride = bandMat;
                band.Position = new Vector3(0, h, 0);
                band.Scale = new Vector3(1, 0.3f, 1);
                AddChild(band);
            }
        }
    }

    private void CreateSurfaceParticles()
    {
        var particles = new GpuParticles3D();
        particles.Name = "ShellParticles";
        particles.Amount = 30;
        particles.Lifetime = 2f;
        particles.Explosiveness = 0f;
        particles.Emitting = true;
        particles.Position = new Vector3(0, 1f, 0);

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = _radius * 0.95f;
        material.Direction = new Vector3(0, 0, 0);
        material.Spread = 180f;
        material.InitialVelocityMin = 0.1f;
        material.InitialVelocityMax = 0.3f;
        material.Gravity = Vector3.Zero;
        material.ScaleMin = 0.03f;
        material.ScaleMax = 0.08f;

        // Color gradient
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.5f, 0.9f, 1f, 0.8f));
        gradient.SetColor(1, new Color(0.3f, 0.7f, 1f, 0f));
        var gradientTex = new GradientTexture1D { Gradient = gradient };
        material.ColorRamp = gradientTex;

        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh { Size = new Vector2(0.1f, 0.1f) };
        var quadMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.9f, 1f),
            EmissionEnabled = true,
            Emission = new Color(0.4f, 0.8f, 1f),
            EmissionEnergyMultiplier = 2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        quadMesh.Material = quadMat;
        particles.DrawPass1 = quadMesh;

        AddChild(particles);
    }

    private void CreatePushArea()
    {
        _pushArea = new Area3D();
        _pushArea.Name = "PushArea";

        var shape = new CollisionShape3D();
        var sphereShape = new SphereShape3D();
        sphereShape.Radius = _radius;
        shape.Shape = sphereShape;
        shape.Position = new Vector3(0, 1f, 0);

        _pushArea.AddChild(shape);
        _pushArea.CollisionLayer = 0;
        _pushArea.CollisionMask = 2; // Enemy layer

        _pushArea.BodyEntered += OnBodyEntered;

        AddChild(_pushArea);
    }

    private void CreateProjectileBlockArea()
    {
        _projectileBlockArea = new Area3D();
        _projectileBlockArea.Name = "ProjectileBlockArea";

        var shape = new CollisionShape3D();
        var sphereShape = new SphereShape3D();
        sphereShape.Radius = _radius;
        shape.Shape = sphereShape;
        shape.Position = new Vector3(0, 1f, 0);

        _projectileBlockArea.AddChild(shape);
        _projectileBlockArea.CollisionLayer = 0;
        _projectileBlockArea.CollisionMask = 4; // EnemyAttack layer

        _projectileBlockArea.AreaEntered += OnProjectileEntered;

        AddChild(_projectileBlockArea);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.4f, 0.8f, 1f);
        _light.LightEnergy = 1f;
        _light.OmniRange = _radius + 2f;
        _light.Position = new Vector3(0, 1f, 0);
        AddChild(_light);
    }

    private void OnBodyEntered(Node3D body)
    {
        // Push enemy out of the shell
        if (body.IsInGroup("Enemies") && !_pushedEnemies.Contains(body))
        {
            PushEnemy(body);
            _pushedEnemies.Add(body);

            // Remove from set after a delay to allow re-pushing
            var timer = GetTree().CreateTimer(0.5f);
            timer.Timeout += () => _pushedEnemies.Remove(body);
        }
    }

    private void PushEnemy(Node3D enemy)
    {
        Vector3 playerPos = GlobalPosition;
        Vector3 pushDir = (enemy.GlobalPosition - playerPos).Normalized();
        pushDir.Y = 0;

        // Teleport enemy just outside the shell
        Vector3 newPos = playerPos + pushDir * (_radius + 1f);
        newPos.Y = enemy.GlobalPosition.Y;

        // Apply knockback if enemy supports it
        if (enemy.HasMethod("ApplyKnockback"))
        {
            enemy.Call("ApplyKnockback", pushDir, _pushForce);
        }

        // Create push effect
        CreatePushEffect(enemy.GlobalPosition);

        GD.Print($"[ProtectiveShell] Pushed {enemy.Name} out of shell");
    }

    private void CreatePushEffect(Vector3 position)
    {
        // Simple expanding ring effect
        var ring = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 0.3f;
        torus.OuterRadius = 0.5f;
        ring.Mesh = torus;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.5f, 0.9f, 1f, 0.8f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.4f, 0.8f, 1f);
        mat.EmissionEnergyMultiplier = 2f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        ring.MaterialOverride = mat;

        ring.GlobalPosition = position;
        ring.RotationDegrees = new Vector3(90, 0, 0);
        GetTree().Root.AddChild(ring);

        var tween = ring.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(ring, "scale", Vector3.One * 3f, 0.3f);
        tween.TweenProperty(mat, "albedo_color:a", 0f, 0.3f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => ring.QueueFree()));
    }

    private void OnProjectileEntered(Area3D area)
    {
        // Destroy enemy projectiles
        if (area.IsInGroup("EnemyProjectiles"))
        {
            GD.Print($"[ProtectiveShell] Blocked projectile: {area.Name}");

            // Create block effect
            CreateBlockEffect(area.GlobalPosition);

            // Destroy projectile
            area.QueueFree();
        }
    }

    private void CreateBlockEffect(Vector3 position)
    {
        // Flash at impact point
        var flash = new OmniLight3D();
        flash.LightColor = new Color(0.5f, 0.9f, 1f);
        flash.LightEnergy = 5f;
        flash.OmniRange = 3f;
        flash.GlobalPosition = position;
        GetTree().Root.AddChild(flash);

        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "light_energy", 0f, 0.2f);
        tween.TweenCallback(Callable.From(() => flash.QueueFree()));

        Core.SoundManager3D.Instance?.PlayHitSound(position);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Pulse animation
        _pulseTimer += dt * 2f;
        float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;

        if (_material != null)
        {
            if (_isWarning)
            {
                // Flash red/cyan when about to expire
                float flashSpeed = 5f;
                float flash = (Mathf.Sin(_pulseTimer * flashSpeed) + 1f) * 0.5f;
                _material.AlbedoColor = new Color(
                    0.3f + flash * 0.5f,
                    0.8f - flash * 0.6f,
                    1f - flash * 0.8f,
                    0.3f + pulse * 0.1f
                );
            }
            else
            {
                _material.AlbedoColor = new Color(0.3f, 0.8f, 1f, 0.3f + pulse * 0.1f);
            }
            _material.EmissionEnergyMultiplier = 1f + pulse * 0.5f;
        }

        // Rotate bubble slowly
        _rotationAngle += dt * 0.5f;
        if (_bubbleMesh != null)
        {
            _bubbleMesh.Rotation = new Vector3(0, _rotationAngle, 0);
        }
    }

    public void StartWarningFlash()
    {
        _isWarning = true;
    }

    public void FadeOut()
    {
        // Disable collision
        if (_pushArea != null) _pushArea.CollisionMask = 0;
        if (_projectileBlockArea != null) _projectileBlockArea.CollisionMask = 0;

        // Fade out visuals
        var tween = CreateTween();
        if (_material != null)
        {
            tween.TweenProperty(_material, "albedo_color:a", 0f, 0.5f);
        }
        if (_light != null)
        {
            tween.Parallel().TweenProperty(_light, "light_energy", 0f, 0.5f);
        }
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
