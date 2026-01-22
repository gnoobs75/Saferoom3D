using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Player;

namespace SafeRoom3D.Abilities;

/// <summary>
/// Base class for mana-based spells.
/// Spells cost mana AND have cooldowns.
/// Extends Ability3D with mana enforcement enabled.
/// </summary>
public abstract partial class Spell3D : Ability3D
{
    /// <summary>
    /// Spells use mana (unlike abilities which are cooldown-only).
    /// </summary>
    public override bool UsesMana => true;

    /// <summary>
    /// Category for UI organization.
    /// </summary>
    public override string Category => "Spell";

    // Additional spell-specific properties that can be overridden

    /// <summary>
    /// Base damage for damage-dealing spells. Override in subclasses.
    /// </summary>
    public virtual float BaseDamage => 0f;

    /// <summary>
    /// Area of effect radius for AOE spells. Override in subclasses.
    /// </summary>
    public virtual float AoeRadius => 0f;

    /// <summary>
    /// Effect duration for duration-based spells. Override in subclasses.
    /// </summary>
    public virtual float EffectDuration => 0f;

    /// <summary>
    /// Projectile speed for projectile spells. Override in subclasses.
    /// </summary>
    public virtual float ProjectileSpeed => 20f;

    /// <summary>
    /// Number of projectiles/chains for multi-hit spells. Override in subclasses.
    /// </summary>
    public virtual int HitCount => 1;

    /// <summary>
    /// Whether this spell is a projectile type.
    /// </summary>
    public virtual bool IsProjectile => false;

    /// <summary>
    /// Whether this spell is an AOE type.
    /// </summary>
    public virtual bool IsAoe => AoeRadius > 0;

    /// <summary>
    /// Color used for spell effects and UI.
    /// </summary>
    public virtual Color SpellColor => new Color(0.8f, 0.5f, 1f); // Purple default

    /// <summary>
    /// Helper to spawn a visual effect at a position.
    /// </summary>
    protected Node3D? SpawnEffect(PackedScene? effectScene, Vector3 position)
    {
        if (effectScene == null) return null;

        var effect = effectScene.Instantiate<Node3D>();
        effect.GlobalPosition = position;
        GetTree().Root.AddChild(effect);
        return effect;
    }

    /// <summary>
    /// Create a basic glow material for spell effects.
    /// </summary>
    protected StandardMaterial3D CreateGlowMaterial(Color color, float emission = 2f)
    {
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = color;
        mat.EmissionEnabled = true;
        mat.Emission = color;
        mat.EmissionEnergyMultiplier = emission;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        return mat;
    }

    /// <summary>
    /// Create a particle trail effect.
    /// </summary>
    protected GpuParticles3D CreateParticleTrail(Color color, int amount = 20)
    {
        var particles = new GpuParticles3D();
        particles.Amount = amount;
        particles.Lifetime = 0.5f;
        particles.Explosiveness = 0f;
        particles.Preprocess = 0.2f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 15f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, -2, 0);
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.1f;
        material.Color = color;
        particles.ProcessMaterial = material;

        var mesh = new SphereMesh();
        mesh.Radius = 0.05f;
        mesh.Height = 0.1f;
        particles.DrawPass1 = mesh;

        return particles;
    }

    /// <summary>
    /// Deal AOE damage to all enemies in radius.
    /// </summary>
    protected int DealAoeDamage(Vector3 center, float radius, float damage)
    {
        var enemies = GetEnemiesInRadius(center, radius);
        int hitCount = 0;

        foreach (var enemy in enemies)
        {
            DealDamage(enemy, damage, center);
            hitCount++;
        }

        return hitCount;
    }

    /// <summary>
    /// Find the closest enemy to a position.
    /// </summary>
    protected Node3D? FindClosestEnemy(Vector3 position, float maxRange = 50f)
    {
        Node3D? closest = null;
        float closestDist = maxRange;

        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// Create explosion visual effect at position.
    /// </summary>
    protected void CreateExplosionEffect(Vector3 position, float radius, Color color)
    {
        // Create expanding sphere
        var explosion = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.1f;
        sphere.Height = 0.2f;
        explosion.Mesh = sphere;
        explosion.MaterialOverride = CreateGlowMaterial(color, 3f);
        explosion.GlobalPosition = position;
        GetTree().Root.AddChild(explosion);

        // Animate expansion and fade
        var tween = GetTree().CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(explosion, "scale", Vector3.One * radius * 2, 0.3f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(explosion.MaterialOverride, "albedo_color:a", 0f, 0.3f);
        tween.Chain().TweenCallback(Callable.From(() => explosion.QueueFree()));

        // Screen shake
        RequestScreenShake(0.2f, radius * 0.1f);
    }
}
