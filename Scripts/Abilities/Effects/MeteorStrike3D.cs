using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Meteor Strike spell - Call down a devastating meteor after a delay.
/// High damage spell with wind-up time.
/// </summary>
public partial class MeteorStrike3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "meteor_strike";
    public override string AbilityName => "Meteor Strike";
    public override string Description => "Call down a meteor that deals massive damage after a short delay.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 45f;
    public override int DefaultManaCost => 60;
    public override int RequiredLevel => 12;

    // Meteor stats
    public float Damage { get; private set; } = 200f;
    public float ImpactRadius { get; private set; } = 8f;
    public float Delay { get; private set; } = 2f;
    public float KnockbackForce { get; private set; } = 15f;

    // ITargetedAbility
    public float TargetRadius => ImpactRadius;
    public Color TargetColor => new(1f, 0.3f, 0.1f); // Red-orange

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Create warning indicator
        CreateWarningIndicator(_targetPosition);

        // Schedule meteor impact
        var timer = GetTree().CreateTimer(Delay);
        timer.Timeout += () => SpawnMeteor(_targetPosition);

        GD.Print($"[MeteorStrike3D] Meteor incoming in {Delay}s at {_targetPosition}");
    }

    private void CreateWarningIndicator(Vector3 position)
    {
        var warning = new Node3D();
        warning.Name = "MeteorWarning";
        warning.GlobalPosition = position + Vector3.Up * 0.1f;
        GetTree().Root.AddChild(warning);

        // Red glowing circle
        var circle = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = ImpactRadius;
        cylinder.BottomRadius = ImpactRadius;
        cylinder.Height = 0.1f;
        circle.Mesh = cylinder;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.2f, 0.1f, 0.5f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.3f, 0.1f);
        mat.EmissionEnergyMultiplier = 1f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        circle.MaterialOverride = mat;
        warning.AddChild(circle);

        // Pulsing animation
        var tween = GetTree().CreateTween();
        tween.SetLoops((int)(Delay / 0.5f));
        tween.TweenProperty(mat, "emission_energy_multiplier", 3f, 0.25f);
        tween.TweenProperty(mat, "emission_energy_multiplier", 1f, 0.25f);
        tween.Chain().TweenCallback(Callable.From(() => warning.QueueFree()));
    }

    private void SpawnMeteor(Vector3 targetPos)
    {
        var meteor = new MeteorProjectile();
        meteor.Name = "Meteor";
        meteor.GlobalPosition = targetPos + Vector3.Up * 30f; // Start high
        meteor.Initialize(targetPos, Damage, ImpactRadius, KnockbackForce);
        GetTree().Root.AddChild(meteor);
    }
}

/// <summary>
/// The meteor projectile that falls and explodes.
/// </summary>
public partial class MeteorProjectile : Node3D
{
    private Vector3 _targetPosition;
    private float _damage;
    private float _radius;
    private float _knockback;
    private float _fallSpeed = 40f;
    private bool _impacted;

    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;

    public void Initialize(Vector3 target, float damage, float radius, float knockback)
    {
        _targetPosition = target;
        _damage = damage;
        _radius = radius;
        _knockback = knockback;
    }

    public override void _Ready()
    {
        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // Fiery meteor mesh
        _mesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 1.5f;
        sphere.Height = 3f;
        _mesh.Mesh = sphere;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.3f, 0.1f, 0f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.5f, 0.1f);
        mat.EmissionEnergyMultiplier = 5f;
        _mesh.MaterialOverride = mat;
        AddChild(_mesh);

        // Bright light
        _light = new OmniLight3D();
        _light.LightColor = new Color(1f, 0.6f, 0.2f);
        _light.LightEnergy = 3f;
        _light.OmniRange = 15f;
        AddChild(_light);

        // Fire trail
        var trail = new GpuParticles3D();
        trail.Amount = 30;
        trail.Lifetime = 0.5f;

        var pmat = new ParticleProcessMaterial();
        pmat.Direction = new Vector3(0, 1, 0);
        pmat.Spread = 30f;
        pmat.InitialVelocityMin = 5f;
        pmat.InitialVelocityMax = 10f;
        pmat.ScaleMin = 0.2f;
        pmat.ScaleMax = 0.5f;
        pmat.Color = new Color(1f, 0.5f, 0.1f, 0.8f);
        trail.ProcessMaterial = pmat;

        var trailMesh = new SphereMesh();
        trailMesh.Radius = 0.15f;
        trailMesh.Height = 0.3f;
        trail.DrawPass1 = trailMesh;
        AddChild(trail);
    }

    public override void _Process(double delta)
    {
        if (_impacted) return;

        float dt = (float)delta;

        // Fall toward target
        GlobalPosition += Vector3.Down * _fallSpeed * dt;

        // Check for impact
        if (GlobalPosition.Y <= _targetPosition.Y + 0.5f)
        {
            Impact();
        }
    }

    private void Impact()
    {
        _impacted = true;
        GlobalPosition = _targetPosition;

        // Deal damage to all enemies in radius
        var enemies = new Godot.Collections.Array<Node3D>();
        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(_targetPosition);
                if (dist <= _radius)
                {
                    enemies.Add(enemy);
                }
            }
        }

        foreach (var enemy in enemies)
        {
            // Deal damage
            if (enemy.HasMethod("TakeDamage"))
            {
                enemy.Call("TakeDamage", _damage, _targetPosition, "Meteor Strike");
            }

            // Apply knockback
            Vector3 knockDir = (enemy.GlobalPosition - _targetPosition).Normalized();
            knockDir.Y = 0.3f; // Slight upward component
            knockDir = knockDir.Normalized();

            if (enemy.HasMethod("ApplyKnockback"))
            {
                enemy.Call("ApplyKnockback", knockDir * _knockback);
            }
        }

        // Create explosion effect
        CreateExplosion();

        // Screen shake
        var player = SafeRoom3D.Player.FPSController.Instance;
        player?.RequestScreenShake(0.4f, 0.8f);

        // Log damage
        SafeRoom3D.UI.HUD3D.Instance?.LogSpellDamage("Meteor Strike", $"{enemies.Count} enemies", (int)_damage);

        GD.Print($"[MeteorStrike] Impact! Hit {enemies.Count} enemies for {_damage} damage");

        QueueFree();
    }

    private void CreateExplosion()
    {
        // Explosion sphere
        var explosion = new MeshInstance3D();
        explosion.GlobalPosition = _targetPosition;
        var sphere = new SphereMesh();
        sphere.Radius = 0.5f;
        sphere.Height = 1f;
        explosion.Mesh = sphere;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.9f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.4f, 0.1f);
        mat.EmissionEnergyMultiplier = 10f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        explosion.MaterialOverride = mat;
        GetTree().Root.AddChild(explosion);

        // Expand and fade
        var tween = GetTree().CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(explosion, "scale", Vector3.One * _radius * 2, 0.4f)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(mat, "albedo_color:a", 0f, 0.4f);
        tween.Chain().TweenCallback(Callable.From(() => explosion.QueueFree()));

        // Debris particles
        var debris = new GpuParticles3D();
        debris.GlobalPosition = _targetPosition;
        debris.Amount = 50;
        debris.Lifetime = 1f;
        debris.Explosiveness = 1f;
        debris.OneShot = true;

        var pmat = new ParticleProcessMaterial();
        pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        pmat.EmissionSphereRadius = 1f;
        pmat.Direction = new Vector3(0, 1, 0);
        pmat.Spread = 90f;
        pmat.InitialVelocityMin = 10f;
        pmat.InitialVelocityMax = 20f;
        pmat.Gravity = new Vector3(0, -15, 0);
        pmat.ScaleMin = 0.1f;
        pmat.ScaleMax = 0.4f;
        pmat.Color = new Color(0.4f, 0.2f, 0.1f);
        debris.ProcessMaterial = pmat;

        var debrisMesh = new BoxMesh();
        debrisMesh.Size = new Vector3(0.2f, 0.2f, 0.2f);
        debris.DrawPass1 = debrisMesh;
        GetTree().Root.AddChild(debris);

        var cleanup = GetTree().CreateTimer(1.5f);
        cleanup.Timeout += () => debris.QueueFree();
    }
}
