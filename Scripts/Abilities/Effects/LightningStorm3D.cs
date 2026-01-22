using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Lightning Storm spell - Random lightning bolts strike an area over time.
/// Chaotic AOE damage spell.
/// </summary>
public partial class LightningStorm3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "lightning_storm";
    public override string AbilityName => "Lightning Storm";
    public override string Description => "Summon a storm that strikes random enemies with lightning.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 30f;
    public override int DefaultManaCost => 45;
    public override int RequiredLevel => 13;

    // Lightning Storm stats
    public float DamagePerBolt { get; private set; } = 40f;
    public float Duration { get; private set; } = 5f;
    public float Radius { get; private set; } = 12f;
    public float BoltInterval { get; private set; } = 0.3f;

    // ITargetedAbility
    public float TargetRadius => Radius;
    public Color TargetColor => new(0.4f, 0.6f, 1f); // Blue

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        var storm = new LightningStormEffect();
        storm.Name = "LightningStorm";
        storm.GlobalPosition = _targetPosition;
        storm.Initialize(Radius, Duration, DamagePerBolt, BoltInterval);
        GetTree().Root.AddChild(storm);

        GD.Print($"[LightningStorm3D] Storm summoned at {_targetPosition}");
    }
}

/// <summary>
/// The lightning storm effect that periodically strikes enemies.
/// </summary>
public partial class LightningStormEffect : Node3D
{
    private float _radius;
    private float _duration;
    private float _damage;
    private float _boltInterval;
    private float _boltTimer;
    private float _lifeTimer;

    private GpuParticles3D? _cloudParticles;

    public void Initialize(float radius, float duration, float damage, float interval)
    {
        _radius = radius;
        _duration = duration;
        _damage = damage;
        _boltInterval = interval;
        _lifeTimer = duration;
    }

    public override void _Ready()
    {
        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // Storm cloud particles
        _cloudParticles = new GpuParticles3D();
        _cloudParticles.Position = Vector3.Up * 8f;
        _cloudParticles.Amount = 40;
        _cloudParticles.Lifetime = 2f;
        _cloudParticles.Preprocess = 1f;

        var pmat = new ParticleProcessMaterial();
        pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        pmat.EmissionBoxExtents = new Vector3(_radius * 0.8f, 0.5f, _radius * 0.8f);
        pmat.Direction = new Vector3(0, 0, 0);
        pmat.Spread = 0f;
        pmat.InitialVelocityMin = 0f;
        pmat.InitialVelocityMax = 0.5f;
        pmat.ScaleMin = 1f;
        pmat.ScaleMax = 2f;
        pmat.Color = new Color(0.3f, 0.3f, 0.4f, 0.6f);
        _cloudParticles.ProcessMaterial = pmat;

        var cloudMesh = new SphereMesh();
        cloudMesh.Radius = 1f;
        cloudMesh.Height = 0.5f;
        _cloudParticles.DrawPass1 = cloudMesh;
        AddChild(_cloudParticles);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        _lifeTimer -= dt;
        _boltTimer += dt;

        // Strike bolt
        if (_boltTimer >= _boltInterval)
        {
            _boltTimer = 0f;
            StrikeLightning();
        }

        // Cleanup
        if (_lifeTimer <= 0f)
        {
            // Fade out particles
            if (_cloudParticles != null)
                _cloudParticles.Emitting = false;

            var timer = GetTree().CreateTimer(2f);
            timer.Timeout += () => QueueFree();
            SetProcess(false);
        }
    }

    private void StrikeLightning()
    {
        // Find random enemy in radius, or random point
        Vector3 strikePos = GlobalPosition;
        Node3D? targetEnemy = null;

        // 70% chance to target an enemy if any are present
        var enemies = new Godot.Collections.Array<Node3D>();
        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(GlobalPosition);
                if (dist <= _radius)
                {
                    enemies.Add(enemy);
                }
            }
        }

        if (enemies.Count > 0 && GD.Randf() < 0.7f)
        {
            int idx = GD.RandRange(0, enemies.Count - 1);
            targetEnemy = enemies[idx];
            strikePos = targetEnemy.GlobalPosition;
        }
        else
        {
            // Random point in radius
            float angle = GD.Randf() * Mathf.Pi * 2;
            float dist = GD.Randf() * _radius;
            strikePos = GlobalPosition + new Vector3(
                Mathf.Cos(angle) * dist,
                0,
                Mathf.Sin(angle) * dist
            );
        }

        // Create lightning bolt visual
        CreateLightningBolt(strikePos);

        // Deal damage if we hit an enemy
        if (targetEnemy != null && targetEnemy.HasMethod("TakeDamage"))
        {
            targetEnemy.Call("TakeDamage", _damage, strikePos, "Lightning Storm");
        }
    }

    private void CreateLightningBolt(Vector3 targetPos)
    {
        var bolt = new Node3D();
        bolt.Name = "LightningBolt";
        GetTree().Root.AddChild(bolt);

        // Create bolt segments from cloud to ground
        Vector3 start = GlobalPosition + Vector3.Up * 8f;
        Vector3 end = targetPos;

        // Create jagged lightning path
        int segments = 6;
        Vector3[] points = new Vector3[segments + 1];
        points[0] = start;
        points[segments] = end;

        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / segments;
            Vector3 midpoint = start.Lerp(end, t);
            // Add random offset
            midpoint += new Vector3(
                (GD.Randf() - 0.5f) * 2f,
                0,
                (GD.Randf() - 0.5f) * 2f
            );
            points[i] = midpoint;
        }

        // Create mesh for each segment
        for (int i = 0; i < segments; i++)
        {
            var segmentMesh = new MeshInstance3D();
            var cylinder = new CylinderMesh();
            cylinder.TopRadius = 0.05f;
            cylinder.BottomRadius = 0.1f;
            cylinder.Height = points[i].DistanceTo(points[i + 1]);
            segmentMesh.Mesh = cylinder;

            // Position and rotate to connect points
            Vector3 midpoint = (points[i] + points[i + 1]) / 2;
            segmentMesh.GlobalPosition = midpoint;
            segmentMesh.LookAt(points[i + 1]);
            segmentMesh.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.8f, 0.9f, 1f);
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.5f, 0.7f, 1f);
            mat.EmissionEnergyMultiplier = 5f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            segmentMesh.MaterialOverride = mat;
            bolt.AddChild(segmentMesh);
        }

        // Flash light at impact
        var flash = new OmniLight3D();
        flash.GlobalPosition = targetPos;
        flash.LightColor = new Color(0.7f, 0.8f, 1f);
        flash.LightEnergy = 5f;
        flash.OmniRange = 8f;
        bolt.AddChild(flash);

        // Impact particles
        var impact = new GpuParticles3D();
        impact.GlobalPosition = targetPos;
        impact.Amount = 20;
        impact.Lifetime = 0.3f;
        impact.Explosiveness = 1f;
        impact.OneShot = true;

        var impactMat = new ParticleProcessMaterial();
        impactMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        impactMat.EmissionSphereRadius = 0.5f;
        impactMat.Direction = new Vector3(0, 1, 0);
        impactMat.Spread = 90f;
        impactMat.InitialVelocityMin = 3f;
        impactMat.InitialVelocityMax = 8f;
        impactMat.ScaleMin = 0.05f;
        impactMat.ScaleMax = 0.15f;
        impactMat.Color = new Color(0.7f, 0.8f, 1f);
        impact.ProcessMaterial = impactMat;

        var impactMesh = new SphereMesh();
        impactMesh.Radius = 0.05f;
        impactMesh.Height = 0.1f;
        impact.DrawPass1 = impactMesh;
        bolt.AddChild(impact);

        // Quick fade out
        var tween = GetTree().CreateTween();
        tween.TweenProperty(flash, "light_energy", 0f, 0.15f);
        tween.TweenCallback(Callable.From(() => bolt.QueueFree()));
    }
}
