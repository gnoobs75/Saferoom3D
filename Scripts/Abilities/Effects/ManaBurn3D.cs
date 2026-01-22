using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Mana Burn spell - Damage an enemy and restore mana based on damage dealt.
/// Sustain spell for mana management.
/// </summary>
public partial class ManaBurn3D : Spell3D, ITargetedAbility
{
    public override string AbilityId => "mana_burn";
    public override string AbilityName => "Mana Burn";
    public override string Description => "Burn an enemy's essence, dealing damage and restoring your mana.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 8f;
    public override int DefaultManaCost => 15;
    public override int RequiredLevel => 15;

    // Mana Burn stats
    public float Damage { get; private set; } = 30f;
    public float ManaRestorePercent { get; private set; } = 1.5f; // 150% of damage dealt
    public float Range { get; private set; } = 20f;

    // ITargetedAbility
    public float TargetRadius => 2f;
    public Color TargetColor => new(0.2f, 0.5f, 1f); // Blue

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Find enemy near target position
        Node3D? targetEnemy = FindEnemyNearPoint(_targetPosition, 3f);

        if (targetEnemy == null)
        {
            GD.Print("[ManaBurn3D] No valid target");
            return;
        }

        // Create visual beam effect
        CreateManaBurnEffect(GetCameraPosition(), targetEnemy.GlobalPosition);

        // Deal damage
        if (targetEnemy.HasMethod("TakeDamage"))
        {
            targetEnemy.Call("TakeDamage", Damage, GetPlayerPosition(), "Mana Burn");
        }

        // Restore mana
        int manaRestored = (int)(Damage * ManaRestorePercent);
        RestoreMana(manaRestored);

        // Mana restore visual on player
        CreateManaRestoreEffect();

        GD.Print($"[ManaBurn3D] Dealt {Damage} damage, restored {manaRestored} mana");
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

    private void CreateManaBurnEffect(Vector3 start, Vector3 end)
    {
        var effect = new Node3D();
        effect.Name = "ManaBurnEffect";
        GetTree().Root.AddChild(effect);

        // Energy beam
        var beam = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 0.08f;
        cylinder.BottomRadius = 0.08f;
        cylinder.Height = start.DistanceTo(end);
        beam.Mesh = cylinder;

        // Position at midpoint
        Vector3 midpoint = (start + end) / 2;
        beam.GlobalPosition = midpoint;

        // Rotate to face target
        beam.LookAt(end);
        beam.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.3f, 0.6f, 1f, 0.8f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.2f, 0.5f, 1f);
        mat.EmissionEnergyMultiplier = 4f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        beam.MaterialOverride = mat;
        effect.AddChild(beam);

        // Drain particles at target
        var drain = new GpuParticles3D();
        drain.GlobalPosition = end;
        drain.Amount = 20;
        drain.Lifetime = 0.5f;
        drain.Explosiveness = 0.5f;
        drain.OneShot = true;

        var drainMat = new ParticleProcessMaterial();
        drainMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        drainMat.EmissionSphereRadius = 0.5f;
        drainMat.Direction = (start - end).Normalized();
        drainMat.Spread = 30f;
        drainMat.InitialVelocityMin = 5f;
        drainMat.InitialVelocityMax = 10f;
        drainMat.ScaleMin = 0.05f;
        drainMat.ScaleMax = 0.15f;
        drainMat.Color = new Color(0.3f, 0.6f, 1f, 0.8f);
        drain.ProcessMaterial = drainMat;

        var drainMesh = new SphereMesh();
        drainMesh.Radius = 0.05f;
        drainMesh.Height = 0.1f;
        drain.DrawPass1 = drainMesh;
        effect.AddChild(drain);

        // Fade out beam
        var tween = GetTree().CreateTween();
        tween.TweenProperty(mat, "albedo_color:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(() => effect.QueueFree()));
    }

    private void CreateManaRestoreEffect()
    {
        var playerPos = GetPlayerPosition();

        var effect = new GpuParticles3D();
        effect.GlobalPosition = playerPos;
        effect.Amount = 25;
        effect.Lifetime = 0.6f;
        effect.Explosiveness = 0.3f;
        effect.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 0.3f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 45f;
        mat.InitialVelocityMin = 2f;
        mat.InitialVelocityMax = 4f;
        mat.Gravity = new Vector3(0, 3, 0);
        mat.ScaleMin = 0.08f;
        mat.ScaleMax = 0.15f;
        mat.Color = new Color(0.3f, 0.5f, 1f, 0.8f);
        effect.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.08f;
        mesh.Height = 0.16f;
        effect.DrawPass1 = mesh;
        GetTree().Root.AddChild(effect);

        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => effect.QueueFree();
    }
}
