using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Death Coil spell - Damage an enemy OR heal yourself.
/// Dual-purpose spell based on target.
/// </summary>
public partial class DeathCoil3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "death_coil";
    public override string AbilityName => "Death Coil";
    public override string Description => "Launch a coil of death energy. Damages enemies or heals yourself.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 15f;
    public override int DefaultManaCost => 35;
    public override int RequiredLevel => 14;

    // Death Coil stats
    public float Damage { get; private set; } = 60f;
    public float HealAmount { get; private set; } = 60f;
    public new float ProjectileSpeed { get; private set; } = 30f;

    // ITargetedAbility
    public float TargetRadius => 2f;
    public Color TargetColor => new(0.5f, 0.2f, 0.5f); // Dark purple

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Check if targeting near player (self-heal mode)
        var playerPos = GetPlayerPosition();
        float distToPlayer = _targetPosition.DistanceTo(playerPos);

        if (distToPlayer < 3f)
        {
            // Self-heal mode
            SelfHeal();
        }
        else
        {
            // Damage mode - launch projectile
            LaunchCoil();
        }
    }

    private void SelfHeal()
    {
        HealPlayer((int)HealAmount);

        // Healing effect on player
        CreateHealEffect(GetPlayerPosition());

        GD.Print($"[DeathCoil3D] Self-heal for {HealAmount} HP");
    }

    private void LaunchCoil()
    {
        var coil = new DeathCoilProjectile();
        coil.Name = "DeathCoil";
        coil.GlobalPosition = GetCameraPosition();
        coil.Initialize(_targetPosition, Damage, ProjectileSpeed);
        GetTree().Root.AddChild(coil);

        GD.Print($"[DeathCoil3D] Launched toward {_targetPosition}");
    }

    private void CreateHealEffect(Vector3 position)
    {
        var effect = new GpuParticles3D();
        effect.GlobalPosition = position;
        effect.Amount = 30;
        effect.Lifetime = 0.8f;
        effect.Explosiveness = 0.3f;
        effect.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.5f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 30f;
        mat.InitialVelocityMin = 2f;
        mat.InitialVelocityMax = 4f;
        mat.Gravity = new Vector3(0, 2, 0);
        mat.ScaleMin = 0.1f;
        mat.ScaleMax = 0.2f;
        mat.Color = new Color(0.3f, 0.8f, 0.3f, 0.8f);
        effect.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.1f;
        mesh.Height = 0.2f;
        effect.DrawPass1 = mesh;
        GetTree().Root.AddChild(effect);

        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => effect.QueueFree();
    }
}

/// <summary>
/// Death Coil projectile that damages on impact.
/// </summary>
public partial class DeathCoilProjectile : Node3D
{
    private Vector3 _targetPosition;
    private Vector3 _direction;
    private float _damage;
    private float _speed;
    private float _lifetime = 5f;
    private bool _hit;

    public void Initialize(Vector3 target, float damage, float speed)
    {
        _targetPosition = target;
        _damage = damage;
        _speed = speed;
    }

    public override void _Ready()
    {
        _direction = (_targetPosition - GlobalPosition).Normalized();
        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // Swirling dark energy
        var mesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.3f;
        sphere.Height = 0.6f;
        mesh.Mesh = sphere;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.3f, 0.1f, 0.3f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.5f, 0.2f, 0.5f);
        mat.EmissionEnergyMultiplier = 3f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mesh.MaterialOverride = mat;
        AddChild(mesh);

        // Dark light
        var light = new OmniLight3D();
        light.LightColor = new Color(0.5f, 0.2f, 0.5f);
        light.LightEnergy = 1.5f;
        light.OmniRange = 4f;
        AddChild(light);

        // Swirl particles
        var particles = new GpuParticles3D();
        particles.Amount = 15;
        particles.Lifetime = 0.4f;

        var pmat = new ParticleProcessMaterial();
        pmat.Direction = new Vector3(0, 0, 0);
        pmat.InitialVelocityMin = 0f;
        pmat.InitialVelocityMax = 1f;
        pmat.ScaleMin = 0.05f;
        pmat.ScaleMax = 0.15f;
        pmat.Color = new Color(0.4f, 0.1f, 0.4f, 0.7f);
        particles.ProcessMaterial = pmat;

        var particleMesh = new SphereMesh();
        particleMesh.Radius = 0.05f;
        particleMesh.Height = 0.1f;
        particles.DrawPass1 = particleMesh;
        AddChild(particles);
    }

    public override void _Process(double delta)
    {
        if (_hit) return;

        float dt = (float)delta;
        _lifetime -= dt;

        if (_lifetime <= 0)
        {
            QueueFree();
            return;
        }

        // Move toward target
        GlobalPosition += _direction * _speed * dt;

        // Check for enemy hits
        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(GlobalPosition);
                if (dist < 1.5f)
                {
                    OnHit(enemy);
                    return;
                }
            }
        }

        // Check if passed target
        if (GlobalPosition.DistanceTo(_targetPosition) < 0.5f)
        {
            QueueFree();
        }
    }

    private void OnHit(Node3D enemy)
    {
        _hit = true;

        if (enemy.HasMethod("TakeDamage"))
        {
            enemy.Call("TakeDamage", _damage, GlobalPosition, "Death Coil");
        }

        // Impact effect
        var impact = new GpuParticles3D();
        impact.GlobalPosition = GlobalPosition;
        impact.Amount = 20;
        impact.Lifetime = 0.4f;
        impact.Explosiveness = 1f;
        impact.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.3f;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 180f;
        mat.InitialVelocityMin = 3f;
        mat.InitialVelocityMax = 6f;
        mat.ScaleMin = 0.05f;
        mat.ScaleMax = 0.15f;
        mat.Color = new Color(0.5f, 0.2f, 0.5f);
        impact.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        impact.DrawPass1 = mesh;
        GetTree().Root.AddChild(impact);

        var timer = GetTree().CreateTimer(0.5f);
        timer.Timeout += () => impact.QueueFree();

        QueueFree();
    }
}
