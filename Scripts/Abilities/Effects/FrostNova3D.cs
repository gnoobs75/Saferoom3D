using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Frost Nova spell - Instant AOE freeze around the player.
/// Freezes all enemies in radius for a duration.
/// </summary>
public partial class FrostNova3D : Spell3D
{
    public override string AbilityId => "frost_nova";
    public override string AbilityName => "Frost Nova";
    public override string Description => "Release a burst of frost, freezing all nearby enemies.";
    public override AbilityType Type => AbilityType.Instant;

    public override float DefaultCooldown => 10f;
    public override int DefaultManaCost => 30;
    public override int RequiredLevel => 9;

    // Frost Nova stats
    public float Radius { get; private set; } = 10f;
    public float FreezeDuration { get; private set; } = 3f;
    public float Damage { get; private set; } = 25f;

    private HashSet<Node3D> _frozenEnemies = new();

    protected override void Activate()
    {
        var playerPos = GetPlayerPosition();

        // Create visual effect
        CreateFrostNovaEffect(playerPos);

        // Freeze and damage all enemies in radius
        var enemies = GetEnemiesInRadius(playerPos, Radius);
        foreach (var enemy in enemies)
        {
            // Deal frost damage
            DealDamage(enemy, Damage, playerPos);

            // Freeze enemy
            if (enemy.HasMethod("SetFrozen"))
            {
                enemy.Call("SetFrozen", true);
                _frozenEnemies.Add(enemy);
            }

            // Apply frost visual
            ApplyFrostVisual(enemy);
        }

        // Set up timed unfreeze
        var timer = GetTree().CreateTimer(FreezeDuration);
        timer.Timeout += OnFreezeEnd;

        // Screen shake
        RequestScreenShake(0.15f, 0.3f);

        GD.Print($"[FrostNova3D] Froze {enemies.Count} enemies for {FreezeDuration}s");
    }

    private void CreateFrostNovaEffect(Vector3 position)
    {
        var effect = new Node3D();
        effect.Name = "FrostNovaEffect";
        effect.GlobalPosition = position;
        GetTree().Root.AddChild(effect);

        // Expanding frost ring
        var ring = new MeshInstance3D();
        var torusMesh = new TorusMesh();
        torusMesh.InnerRadius = 0.1f;
        torusMesh.OuterRadius = 0.5f;
        ring.Mesh = torusMesh;
        ring.RotationDegrees = new Vector3(90, 0, 0);

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.5f, 0.8f, 1f, 0.8f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.3f, 0.6f, 1f);
        mat.EmissionEnergyMultiplier = 2f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        ring.MaterialOverride = mat;
        effect.AddChild(ring);

        // Create frost particles
        var particles = new GpuParticles3D();
        particles.Amount = 50;
        particles.Lifetime = 0.5f;
        particles.Explosiveness = 1f;
        particles.OneShot = true;

        var pmat = new ParticleProcessMaterial();
        pmat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        pmat.EmissionSphereRadius = Radius * 0.5f;
        pmat.Direction = new Vector3(0, 1, 0);
        pmat.Spread = 180f;
        pmat.InitialVelocityMin = 5f;
        pmat.InitialVelocityMax = 10f;
        pmat.Gravity = new Vector3(0, -5, 0);
        pmat.ScaleMin = 0.1f;
        pmat.ScaleMax = 0.3f;
        pmat.Color = new Color(0.7f, 0.9f, 1f, 0.8f);
        particles.ProcessMaterial = pmat;

        var particleMesh = new SphereMesh();
        particleMesh.Radius = 0.1f;
        particleMesh.Height = 0.2f;
        particles.DrawPass1 = particleMesh;
        effect.AddChild(particles);

        // Animate ring expansion
        var tween = GetTree().CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(ring, "scale", Vector3.One * Radius * 2, 0.3f)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(mat, "albedo_color:a", 0f, 0.5f);
        tween.Chain().TweenCallback(Callable.From(() => effect.QueueFree()));
    }

    private void ApplyFrostVisual(Node3D enemy)
    {
        // Add ice tint to enemy
        if (enemy.FindChild("FrostTint", false) != null) return;

        var tint = new OmniLight3D();
        tint.Name = "FrostTint";
        tint.LightColor = new Color(0.5f, 0.7f, 1f);
        tint.LightEnergy = 0.5f;
        tint.OmniRange = 2f;
        enemy.AddChild(tint);
    }

    private void OnFreezeEnd()
    {
        foreach (var enemy in _frozenEnemies)
        {
            if (GodotObject.IsInstanceValid(enemy))
            {
                // Unfreeze
                if (enemy.HasMethod("SetFrozen"))
                {
                    enemy.Call("SetFrozen", false);
                }

                // Remove frost visual
                var tint = enemy.FindChild("FrostTint", false);
                if (tint != null)
                {
                    tint.QueueFree();
                }
            }
        }

        _frozenEnemies.Clear();
        GD.Print("[FrostNova3D] Freeze ended");
    }
}
