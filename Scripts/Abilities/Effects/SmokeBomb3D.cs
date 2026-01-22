using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Smoke Bomb ability - Create a smoke cloud that blinds enemies and breaks targeting.
/// Escape/utility ability.
/// </summary>
public partial class SmokeBomb3D : Ability3D
{
    public override string AbilityId => "smoke_bomb";
    public override string AbilityName => "Smoke Bomb";
    public override string Description => "Drop a smoke bomb that blinds enemies and makes you untargetable.";
    public override AbilityType Type => AbilityType.Instant;

    public override float DefaultCooldown => 30f;
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 13;

    // Smoke Bomb stats
    public float Radius { get; private set; } = 8f;
    public float Duration { get; private set; } = 5f;
    public float BlindDuration { get; private set; } = 3f;

    protected override void Activate()
    {
        if (Player == null) return;

        Vector3 dropPosition = Player.GlobalPosition;

        // Create the smoke cloud
        var smokeCloud = new SmokeBombEffect();
        smokeCloud.Name = "SmokeBomb";
        smokeCloud.GlobalPosition = dropPosition;
        smokeCloud.Initialize(Radius, Duration, BlindDuration);
        GetTree().Root.AddChild(smokeCloud);

        // Break enemy targeting on player
        BreakEnemyTargeting();

        GD.Print($"[SmokeBomb3D] Dropped at {dropPosition}");
    }

    private void BreakEnemyTargeting()
    {
        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy && Player != null)
            {
                float dist = enemy.GlobalPosition.DistanceTo(Player.GlobalPosition);
                if (dist <= Radius * 1.5f)
                {
                    // Clear their target
                    if (enemy.HasMethod("DropAggro"))
                    {
                        enemy.Call("DropAggro");
                    }
                    else if (enemy.HasMethod("SetTarget"))
                    {
                        enemy.Call("SetTarget", new Variant());
                    }
                }
            }
        }
    }
}

/// <summary>
/// Smoke bomb cloud effect that blinds enemies inside.
/// </summary>
public partial class SmokeBombEffect : Node3D
{
    private float _radius;
    private float _duration;
    private float _blindDuration;
    private float _lifeTimer;

    private GpuParticles3D? _smokeParticles;
    private OmniLight3D? _light;
    private Area3D? _blindArea;
    private HashSet<Node3D> _blindedEnemies = new();

    public void Initialize(float radius, float duration, float blindDuration)
    {
        _radius = radius;
        _duration = duration;
        _blindDuration = blindDuration;
        _lifeTimer = duration;
    }

    public override void _Ready()
    {
        CreateSmokeParticles();
        CreateLight();
        CreateBlindArea();
        CreateInitialPoof();
    }

    private void CreateSmokeParticles()
    {
        _smokeParticles = new GpuParticles3D();
        _smokeParticles.Amount = 100;
        _smokeParticles.Lifetime = 2f;
        _smokeParticles.Preprocess = 0.5f;
        _smokeParticles.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = _radius * 0.6f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 60f;
        mat.InitialVelocityMin = 0.5f;
        mat.InitialVelocityMax = 1.5f;
        mat.Gravity = new Vector3(0, 0.3f, 0);
        mat.ScaleMin = 0.8f;
        mat.ScaleMax = 1.5f;
        mat.Color = new Color(0.3f, 0.3f, 0.35f, 0.4f);
        _smokeParticles.ProcessMaterial = mat;

        // Billboard quads for smoke puffs
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(1f, 1f);

        var smokeMat = new StandardMaterial3D();
        smokeMat.AlbedoColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
        smokeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        smokeMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        smokeMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        quadMesh.Material = smokeMat;

        _smokeParticles.DrawPass1 = quadMesh;
        _smokeParticles.Position = Vector3.Up;
        AddChild(_smokeParticles);
    }

    private void CreateLight()
    {
        // Dim light to show obscured area
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.3f, 0.3f, 0.35f);
        _light.LightEnergy = 0.5f;
        _light.OmniRange = _radius;
        _light.Position = Vector3.Up * 1.5f;
        AddChild(_light);
    }

    private void CreateBlindArea()
    {
        _blindArea = new Area3D();
        _blindArea.Name = "BlindArea";

        var shape = new CollisionShape3D();
        var sphereShape = new SphereShape3D();
        sphereShape.Radius = _radius;
        shape.Shape = sphereShape;
        shape.Position = Vector3.Up;

        _blindArea.AddChild(shape);
        _blindArea.CollisionLayer = 0;
        _blindArea.CollisionMask = 2; // Enemy layer

        _blindArea.BodyEntered += OnBodyEntered;

        AddChild(_blindArea);
    }

    private void CreateInitialPoof()
    {
        // Quick burst of smoke on creation
        var burst = new GpuParticles3D();
        burst.Amount = 30;
        burst.Lifetime = 0.5f;
        burst.Explosiveness = 1f;
        burst.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 180f;
        mat.InitialVelocityMin = 5f;
        mat.InitialVelocityMax = 10f;
        mat.Gravity = new Vector3(0, -2, 0);
        mat.ScaleMin = 0.5f;
        mat.ScaleMax = 1f;
        mat.Color = new Color(0.4f, 0.4f, 0.45f, 0.6f);
        burst.ProcessMaterial = mat;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.8f, 0.8f);
        var smokeMat = new StandardMaterial3D();
        smokeMat.AlbedoColor = new Color(0.35f, 0.35f, 0.4f, 0.6f);
        smokeMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        smokeMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        smokeMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        quadMesh.Material = smokeMat;
        burst.DrawPass1 = quadMesh;

        burst.GlobalPosition = GlobalPosition + Vector3.Up;
        GetTree().Root.AddChild(burst);

        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => burst.QueueFree();
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("Enemies") && !_blindedEnemies.Contains(body))
        {
            BlindEnemy(body);
        }
    }

    private void BlindEnemy(Node3D enemy)
    {
        _blindedEnemies.Add(enemy);

        // Apply blind effect
        if (enemy.HasMethod("SetBlinded"))
        {
            enemy.Call("SetBlinded", true);
        }

        // Clear their target
        if (enemy.HasMethod("DropAggro"))
        {
            enemy.Call("DropAggro");
        }
        else if (enemy.HasMethod("SetTarget"))
        {
            enemy.Call("SetTarget", new Variant());
        }

        // Visual indicator on enemy
        CreateBlindIndicator(enemy);

        // Remove blind after duration
        var timer = GetTree().CreateTimer(_blindDuration);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(enemy))
            {
                if (enemy.HasMethod("SetBlinded"))
                {
                    enemy.Call("SetBlinded", false);
                }
                _blindedEnemies.Remove(enemy);

                // Remove blind indicator
                var indicator = enemy.GetNodeOrNull("BlindIndicator");
                indicator?.QueueFree();
            }
        };

        GD.Print($"[SmokeBomb] Blinded {enemy.Name} for {_blindDuration}s");
    }

    private void CreateBlindIndicator(Node3D enemy)
    {
        // Swirling stars above head
        var indicator = new GpuParticles3D();
        indicator.Name = "BlindIndicator";
        indicator.Amount = 8;
        indicator.Lifetime = 0.8f;
        indicator.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        mat.EmissionRingRadius = 0.5f;
        mat.EmissionRingInnerRadius = 0.4f;
        mat.EmissionRingHeight = 0.1f;
        mat.Direction = new Vector3(0, 0, 0);
        mat.Spread = 0f;
        mat.InitialVelocityMin = 0f;
        mat.InitialVelocityMax = 0f;
        mat.AngularVelocityMin = 180f;
        mat.AngularVelocityMax = 360f;
        mat.ScaleMin = 0.1f;
        mat.ScaleMax = 0.15f;
        mat.Color = new Color(1f, 1f, 0.3f, 0.8f);
        indicator.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.08f;
        mesh.Height = 0.16f;
        indicator.DrawPass1 = mesh;

        indicator.Position = Vector3.Up * 2.5f;
        enemy.AddChild(indicator);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _lifeTimer -= dt;

        // Fade out near end
        if (_lifeTimer <= 1f)
        {
            float alpha = _lifeTimer;
            if (_light != null)
            {
                _light.LightEnergy = 0.5f * alpha;
            }
        }

        // Stop emitting before duration ends
        if (_lifeTimer <= 1.5f && _smokeParticles != null)
        {
            _smokeParticles.Emitting = false;
        }

        // Cleanup
        if (_lifeTimer <= 0)
        {
            QueueFree();
        }
    }
}
