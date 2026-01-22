using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.UI;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Fireball spell - Launch a fireball that explodes on impact.
/// Targeted AOE damage spell. Starter spell for all players.
/// </summary>
public partial class Fireball3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "fireball";
    public override string AbilityName => "Fireball";
    public override string Description => "Launch a fireball that explodes on impact, dealing AOE damage.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 0f; // No cooldown, mana-limited
    public override int DefaultManaCost => 25;
    public override int RequiredLevel => 1; // Starter spell

    // Fireball stats
    public float Damage { get; private set; } = 100f;
    public float ExplosionRadius { get; private set; } = 7.5f; // 120px in 2D / 16
    public float ProjectileSpeed { get; private set; } = 22f; // 350px/s in 2D / 16

    // ITargetedAbility
    public float TargetRadius => ExplosionRadius;
    public Color TargetColor => new(1f, 0.5f, 0.2f); // Orange

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Create fireball projectile
        var fireball = new FireballProjectile3D();
        fireball.Name = "FireballProjectile";
        fireball.GlobalPosition = GetCameraPosition();
        fireball.Initialize(_targetPosition, Damage, ExplosionRadius, ProjectileSpeed);

        // Add to scene
        GetTree().Root.AddChild(fireball);

        GD.Print($"[Fireball3D] Launched toward {_targetPosition}");
    }
}

/// <summary>
/// The fireball projectile that travels and explodes.
/// </summary>
public partial class FireballProjectile3D : Node3D
{
    private Vector3 _targetPosition;
    private Vector3 _direction;
    private float _speed;
    private float _damage;
    private float _explosionRadius;
    private bool _initialized;

    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;
    private GpuParticles3D? _trail;

    private float _lifetime = 5f;
    private bool _exploded;

    public void Initialize(Vector3 target, float damage, float radius, float speed)
    {
        _targetPosition = target;
        _damage = damage;
        _explosionRadius = radius;
        _speed = speed;
        // Direction will be calculated in _Ready when GlobalPosition is valid
    }

    public override void _Ready()
    {
        // Calculate direction now that we're in the scene tree with valid position
        _direction = (_targetPosition - GlobalPosition).Normalized();
        _initialized = true;

        GD.Print($"[Fireball] Initialized at {GlobalPosition}, target {_targetPosition}, direction {_direction}");

        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // Create fireball with multiple layers for depth
        var fireballContainer = new Node3D();
        fireballContainer.Name = "FireballVisual";
        AddChild(fireballContainer);

        // Outer glow layer (translucent orange)
        var outerGlow = new MeshInstance3D();
        var outerMesh = new SphereMesh { Radius = 0.4f, Height = 0.8f };
        outerGlow.Mesh = outerMesh;
        var outerMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.4f, 0.1f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0f),
            EmissionEnergyMultiplier = 2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        outerGlow.MaterialOverride = outerMat;
        fireballContainer.AddChild(outerGlow);

        // Main fire sphere (hot orange)
        _mesh = new MeshInstance3D();
        var sphere = new SphereMesh { Radius = 0.28f, Height = 0.56f };
        _mesh.Mesh = sphere;
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.5f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.4f, 0f),
            EmissionEnergyMultiplier = 4f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _mesh.MaterialOverride = material;
        fireballContainer.AddChild(_mesh);

        // Inner core (bright yellow/white)
        var core = new MeshInstance3D();
        var coreMesh = new SphereMesh { Radius = 0.15f, Height = 0.3f };
        core.Mesh = coreMesh;
        var coreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.95f, 0.7f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.9f, 0.5f),
            EmissionEnergyMultiplier = 6f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        core.MaterialOverride = coreMat;
        fireballContainer.AddChild(core);

        // Add flame tendrils around the fireball
        CreateFlameTendrils(fireballContainer);

        // Create light
        _light = new OmniLight3D();
        _light.LightColor = new Color(1f, 0.5f, 0.2f);
        _light.LightEnergy = 4f;
        _light.OmniRange = 8f;
        _light.OmniAttenuation = 1.3f;
        AddChild(_light);

        // Create particle trail
        _trail = CreateFireTrail();
        AddChild(_trail);

        // Create swirling flame particles around the fireball
        var swirl = CreateSwirlParticles();
        AddChild(swirl);
    }

    private void CreateFlameTendrils(Node3D parent)
    {
        // Create flame-like protrusions using stretched spheres
        var tendrilMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.7f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.4f, 0.1f),
            EmissionEnergyMultiplier = 3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // Create 6 flame tendrils radiating outward
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Pi * 2f / 6f;
            var tendril = new MeshInstance3D();
            var tendrilMesh = new SphereMesh { Radius = 0.1f, Height = 0.2f };
            tendril.Mesh = tendrilMesh;
            tendril.MaterialOverride = tendrilMat;
            // Position outward from center
            tendril.Position = new Vector3(Mathf.Cos(angle) * 0.25f, Mathf.Sin(angle * 0.7f) * 0.1f, Mathf.Sin(angle) * 0.25f);
            // Stretch to look like flames licking outward
            tendril.Scale = new Vector3(0.6f, 1.5f, 0.6f);
            tendril.RotationDegrees = new Vector3(0, angle * 57.3f, 25);
            parent.AddChild(tendril);
        }
    }

    private GpuParticles3D CreateSwirlParticles()
    {
        var particles = new GpuParticles3D();
        particles.Amount = 20;
        particles.Lifetime = 0.3f;
        particles.Explosiveness = 0f;
        particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.25f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 180f;
        material.InitialVelocityMin = 0.5f;
        material.InitialVelocityMax = 1.5f;
        material.Gravity = Vector3.Zero;
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.15f;

        // Color gradient from yellow to orange to transparent
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.9f, 0.4f, 1f));
        gradient.SetColor(1, new Color(1f, 0.3f, 0.1f, 0f));
        var gradientTex = new GradientTexture1D { Gradient = gradient };
        material.ColorRamp = gradientTex;

        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh { Size = new Vector2(0.1f, 0.1f) };
        var quadMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.7f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.5f, 0.1f),
            EmissionEnergyMultiplier = 2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        quadMesh.Material = quadMat;
        particles.DrawPass1 = quadMesh;

        return particles;
    }

    private GpuParticles3D CreateFireTrail()
    {
        var particles = new GpuParticles3D();
        particles.Amount = 30;
        particles.Lifetime = 0.4f;
        particles.Explosiveness = 0f;
        particles.Emitting = true;

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.1f;
        material.Direction = new Vector3(0, 0, 1);
        material.Spread = 30f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, 2, 0);
        material.ScaleMin = 0.1f;
        material.ScaleMax = 0.3f;
        material.Color = new Color(1f, 0.5f, 0.1f);

        particles.ProcessMaterial = material;

        // Simple quad mesh for particles
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.2f, 0.2f);
        particles.DrawPass1 = quadMesh;

        return particles;
    }

    public override void _Process(double delta)
    {
        if (_exploded || !_initialized) return;

        float dt = (float)delta;

        // Move toward target
        GlobalPosition += _direction * _speed * dt;

        // Check if reached target
        float distToTarget = GlobalPosition.DistanceTo(_targetPosition);
        if (distToTarget < 0.5f)
        {
            Explode();
            return;
        }

        // Lifetime check
        _lifetime -= dt;
        if (_lifetime <= 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        GD.Print($"[Fireball] Exploded at {GlobalPosition}");

        // Deal damage to all enemies in radius
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(GlobalPosition);
                if (dist <= _explosionRadius)
                {
                    if (enemy.HasMethod("TakeDamage"))
                    {
                        enemy.Call("TakeDamage", _damage, GlobalPosition, "Fireball");

                        // Log damage to combat log
                        string enemyName = enemy.Name;
                        if (enemy is BasicEnemy3D basic)
                            enemyName = basic.MonsterType;
                        else if (enemy is BossEnemy3D boss)
                            enemyName = boss.BossName;
                        HUD3D.Instance?.LogSpellDamage("Fireball", enemyName, (int)_damage);

                        GD.Print($"[Fireball] Hit {enemy.Name} for {_damage} damage");
                    }
                }
            }
        }

        // Create explosion effect
        CreateExplosionEffect();

        // Play sound
        SoundManager3D.Instance?.PlayExplosionSound(GlobalPosition);

        // Screen shake
        SafeRoom3D.Player.FPSController.Instance?.RequestScreenShake(0.2f, 0.1f);

        // Remove after short delay for particles to finish
        var tween = CreateTween();
        tween.TweenInterval(0.5f);
        tween.TweenCallback(Callable.From(() => QueueFree()));

        // Hide projectile mesh
        if (_mesh != null) _mesh.Visible = false;
        if (_trail != null) _trail.Emitting = false;
    }

    private void CreateExplosionEffect()
    {
        // Create expanding explosion ring
        var explosion = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.5f;
        sphere.Height = 1f;
        explosion.Mesh = sphere;

        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.8f);
        material.EmissionEnabled = true;
        material.Emission = new Color(1f, 0.3f, 0f);
        material.EmissionEnergyMultiplier = 5f;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        explosion.MaterialOverride = material;

        explosion.GlobalPosition = GlobalPosition;
        GetTree().Root.AddChild(explosion);

        // Animate expansion and fade
        var tween = explosion.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(explosion, "scale", Vector3.One * _explosionRadius * 0.5f, 0.3f)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(material, "albedo_color:a", 0f, 0.3f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => explosion.QueueFree()));

        // Flash light
        if (_light != null)
        {
            _light.LightEnergy = 8f;
            _light.OmniRange = _explosionRadius + 2f;
            var lightTween = CreateTween();
            lightTween.TweenProperty(_light, "light_energy", 0f, 0.3f);
        }
    }
}
