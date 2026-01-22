using Godot;
using SafeRoom3D.Player;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Heal spell - starter spell that heals the player.
/// Costs 3 mana, heals 20 HP.
/// Available from level 0.
/// </summary>
public partial class Heal3D : Spell3D
{
    public override string AbilityId => "heal";
    public override string AbilityName => "Heal";
    public override string Description => "Restore 20 HP to yourself.";

    public override AbilityType Type => AbilityType.Instant;

    public override float DefaultCooldown => 5f;
    public override int DefaultManaCost => 3;
    public override int RequiredLevel => 0; // Available from start

    public override Color SpellColor => new Color(0.3f, 1f, 0.3f); // Green for healing

    private const int HealAmount = 20;

    protected override void Activate()
    {
        if (Player == null) return;

        // Heal the player
        HealPlayer(HealAmount);

        // Visual feedback - green glow on player
        CreateHealEffect();

        GD.Print($"[Heal] Healed player for {HealAmount} HP");
    }

    private void CreateHealEffect()
    {
        if (Player == null) return;

        // Create green particles rising around player
        var particles = new GpuParticles3D();
        particles.Amount = 30;
        particles.Lifetime = 1f;
        particles.Explosiveness = 0.8f;
        particles.OneShot = true;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 45f;
        material.InitialVelocityMin = 2f;
        material.InitialVelocityMax = 4f;
        material.Gravity = new Vector3(0, 1, 0);
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.15f;
        material.Color = SpellColor;
        particles.ProcessMaterial = material;

        var mesh = new SphereMesh();
        mesh.Radius = 0.08f;
        mesh.Height = 0.16f;
        var mat = CreateGlowMaterial(SpellColor, 3f);
        mesh.Material = mat;
        particles.DrawPass1 = mesh;

        particles.GlobalPosition = Player.GlobalPosition;
        GetTree().Root.AddChild(particles);
        particles.Emitting = true;

        // Auto-cleanup
        var timer = GetTree().CreateTimer(2f);
        timer.Timeout += () => particles.QueueFree();

        // Create a brief green glow ring
        var ring = new MeshInstance3D();
        var torusMesh = new TorusMesh();
        torusMesh.InnerRadius = 0.8f;
        torusMesh.OuterRadius = 1.2f;
        ring.Mesh = torusMesh;
        ring.MaterialOverride = CreateGlowMaterial(SpellColor, 2f);
        ring.GlobalPosition = Player.GlobalPosition + new Vector3(0, 0.1f, 0);
        ring.RotationDegrees = new Vector3(90, 0, 0);
        GetTree().Root.AddChild(ring);

        // Animate ring expansion and fade
        var tween = GetTree().CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(ring, "scale", Vector3.One * 2f, 0.5f)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(ring.MaterialOverride, "albedo_color:a", 0f, 0.5f);
        tween.Chain().TweenCallback(Callable.From(() => ring.QueueFree()));
    }
}
