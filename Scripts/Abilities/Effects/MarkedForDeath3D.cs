using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Marked for Death ability - Mark a target to take increased damage.
/// Debuff/damage amplification ability.
/// </summary>
public partial class MarkedForDeath3D : Ability3D, ITargetedAbility
{
    public override string AbilityId => "marked_for_death";
    public override string AbilityName => "Marked for Death";
    public override string Description => "Mark an enemy to take 25% increased damage for 10 seconds.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 20f;
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 15;

    // Marked for Death stats
    public float Duration { get; private set; } = 10f;
    public float DamageAmplification { get; private set; } = 0.25f; // 25% more damage taken
    public float Range { get; private set; } = 30f;

    // ITargetedAbility
    public float TargetRadius => 2f;
    public Color TargetColor => new(0.8f, 0.1f, 0.1f); // Red

    private Vector3 _targetPosition;
    private Node3D? _markedEnemy;
    private MarkedForDeathEffect? _effect;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        if (Player == null) return;

        // Find enemy near target position
        _markedEnemy = FindEnemyNearPoint(_targetPosition, 3f);

        if (_markedEnemy == null)
        {
            GD.Print("[MarkedForDeath3D] No valid target");
            return;
        }

        // Apply the mark
        ApplyMark(_markedEnemy);

        // Create visual effect
        _effect = new MarkedForDeathEffect();
        _effect.Name = "MarkedForDeathEffect";
        _effect.Initialize(_markedEnemy, Duration);
        GetTree().Root.AddChild(_effect);

        SetActiveWithDuration(Duration);

        GD.Print($"[MarkedForDeath3D] Marked {_markedEnemy.Name} for {Duration}s - taking {DamageAmplification * 100}% extra damage");
    }

    private Node3D? FindEnemyNearPoint(Vector3 point, float maxRange)
    {
        Node3D? closest = null;
        float closestDist = maxRange;

        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(point);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }
        }

        return closest;
    }

    private void ApplyMark(Node3D enemy)
    {
        // Increase damage taken
        if (enemy.HasMethod("SetDamageTakenMultiplier"))
        {
            enemy.Call("SetDamageTakenMultiplier", 1f + DamageAmplification);
        }

        // Mark as... marked (for other systems to check)
        if (enemy.HasMethod("SetMarkedForDeath"))
        {
            enemy.Call("SetMarkedForDeath", true);
        }
    }

    private void RemoveMark(Node3D enemy)
    {
        if (!IsInstanceValid(enemy)) return;

        // Restore normal damage taken
        if (enemy.HasMethod("SetDamageTakenMultiplier"))
        {
            enemy.Call("SetDamageTakenMultiplier", 1f);
        }

        // Clear mark flag
        if (enemy.HasMethod("SetMarkedForDeath"))
        {
            enemy.Call("SetMarkedForDeath", false);
        }
    }

    protected override void OnActiveTick(float dt)
    {
        // Check if target died early
        if (_markedEnemy != null && !IsInstanceValid(_markedEnemy))
        {
            Deactivate();
        }
    }

    protected override void OnDeactivate()
    {
        if (_markedEnemy != null && IsInstanceValid(_markedEnemy))
        {
            RemoveMark(_markedEnemy);
        }

        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }

        _markedEnemy = null;
        _effect = null;

        GD.Print("[MarkedForDeath3D] Mark expired");
    }
}

/// <summary>
/// Visual effect for Marked for Death - skull/crosshair above enemy.
/// </summary>
public partial class MarkedForDeathEffect : Node3D
{
    private Node3D? _target;
    private float _duration;
    private float _timer;

    private MeshInstance3D? _marker;
    private GpuParticles3D? _particles;
    private OmniLight3D? _light;
    private StandardMaterial3D? _material;

    public void Initialize(Node3D target, float duration)
    {
        _target = target;
        _duration = duration;
    }

    public override void _Ready()
    {
        if (_target == null)
        {
            QueueFree();
            return;
        }

        CreateMarker();
        CreateParticles();
        CreateLight();
        CreateInitialFlash();
    }

    private void CreateMarker()
    {
        // Create a crosshair/target marker
        _marker = new MeshInstance3D();

        // Use a torus as crosshair ring
        var torus = new TorusMesh();
        torus.InnerRadius = 0.4f;
        torus.OuterRadius = 0.5f;
        _marker.Mesh = torus;

        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(1f, 0.1f, 0.1f, 0.8f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(1f, 0f, 0f);
        _material.EmissionEnergyMultiplier = 2f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _marker.MaterialOverride = _material;

        _marker.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(_marker);

        // Add crosshair lines
        CreateCrosshairLines();
    }

    private void CreateCrosshairLines()
    {
        var lineMat = new StandardMaterial3D();
        lineMat.AlbedoColor = new Color(1f, 0.1f, 0.1f, 0.9f);
        lineMat.EmissionEnabled = true;
        lineMat.Emission = new Color(1f, 0f, 0f);
        lineMat.EmissionEnergyMultiplier = 2f;
        lineMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

        // Four lines pointing inward
        for (int i = 0; i < 4; i++)
        {
            var line = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(0.08f, 0.3f, 0.08f);
            line.Mesh = box;
            line.MaterialOverride = lineMat;

            float angle = i * Mathf.Pi / 2f;
            line.Position = new Vector3(Mathf.Cos(angle) * 0.6f, 0, Mathf.Sin(angle) * 0.6f);
            line.LookAt(Vector3.Zero);
            line.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);

            AddChild(line);
        }
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 12;
        _particles.Lifetime = 1f;
        _particles.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        mat.EmissionRingRadius = 0.6f;
        mat.EmissionRingInnerRadius = 0.4f;
        mat.EmissionRingHeight = 0.1f;
        mat.Direction = new Vector3(0, -1, 0);
        mat.Spread = 10f;
        mat.InitialVelocityMin = 1f;
        mat.InitialVelocityMax = 2f;
        mat.Gravity = new Vector3(0, -2, 0);
        mat.ScaleMin = 0.05f;
        mat.ScaleMax = 0.1f;
        mat.Color = new Color(1f, 0.2f, 0.1f, 0.8f);
        _particles.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        _particles.DrawPass1 = mesh;

        AddChild(_particles);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(1f, 0.1f, 0.1f);
        _light.LightEnergy = 1f;
        _light.OmniRange = 3f;
        AddChild(_light);
    }

    private void CreateInitialFlash()
    {
        // Bright flash when mark is applied
        var flash = new OmniLight3D();
        flash.GlobalPosition = _target?.GlobalPosition ?? GlobalPosition;
        flash.GlobalPosition += Vector3.Up * 1.5f;
        flash.LightColor = new Color(1f, 0.2f, 0.1f);
        flash.LightEnergy = 5f;
        flash.OmniRange = 8f;
        GetTree().Root.AddChild(flash);

        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "light_energy", 0f, 0.3f);
        tween.TweenCallback(Callable.From(() => flash.QueueFree()));

        // Descending ring effect
        var ring = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 1.5f;
        torus.OuterRadius = 1.8f;
        ring.Mesh = torus;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.2f, 0.1f, 0.8f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0f, 0f);
        mat.EmissionEnergyMultiplier = 3f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        ring.MaterialOverride = mat;

        ring.GlobalPosition = _target?.GlobalPosition ?? GlobalPosition;
        ring.GlobalPosition += Vector3.Up * 5f;
        ring.RotationDegrees = new Vector3(90, 0, 0);
        GetTree().Root.AddChild(ring);

        var ringTween = ring.CreateTween();
        ringTween.SetParallel(true);
        ringTween.TweenProperty(ring, "global_position:y", (_target?.GlobalPosition.Y ?? 0) + 2.5f, 0.5f);
        ringTween.TweenProperty(ring, "scale", Vector3.One * 0.3f, 0.5f);
        ringTween.TweenProperty(mat, "albedo_color:a", 0f, 0.5f);
        ringTween.SetParallel(false);
        ringTween.TweenCallback(Callable.From(() => ring.QueueFree()));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _timer += dt;

        // Follow target
        if (_target != null && IsInstanceValid(_target))
        {
            GlobalPosition = _target.GlobalPosition + Vector3.Up * 2.5f;
        }
        else
        {
            QueueFree();
            return;
        }

        // Rotate marker
        if (_marker != null)
        {
            var rot = _marker.RotationDegrees;
            rot.Y += dt * 90f;
            _marker.RotationDegrees = new Vector3(90, rot.Y, 0);
        }

        // Pulse effect
        float pulse = (Mathf.Sin(_timer * 4f) + 1f) * 0.5f;

        if (_material != null)
        {
            _material.AlbedoColor = new Color(1f, 0.1f, 0.1f, 0.6f + pulse * 0.3f);
            _material.EmissionEnergyMultiplier = 1.5f + pulse * 1f;
        }

        if (_light != null)
        {
            _light.LightEnergy = 0.8f + pulse * 0.6f;
        }
    }

    public void FadeOut()
    {
        if (_particles != null)
        {
            _particles.Emitting = false;
        }

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
