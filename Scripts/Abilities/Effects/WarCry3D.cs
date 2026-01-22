using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// War Cry ability - Boost damage for player and nearby allies.
/// Party buff ability.
/// </summary>
public partial class WarCry3D : Ability3D
{
    public override string AbilityId => "war_cry";
    public override string AbilityName => "War Cry";
    public override string Description => "Let out a battle cry that increases damage for you and nearby allies.";
    public override AbilityType Type => AbilityType.Duration;

    public override float DefaultCooldown => 60f;
    public override int DefaultManaCost => 0;
    public override int RequiredLevel => 8;

    // War Cry stats
    public float Duration { get; private set; } = 10f;
    public float DamageBonus { get; private set; } = 0.30f; // 30% damage increase
    public float Radius { get; private set; } = 15f;

    private WarCryEffect? _effect;

    protected override void Activate()
    {
        if (Player == null) return;

        // Create visual effect
        _effect = new WarCryEffect();
        _effect.Name = "WarCryEffect";
        _effect.Initialize(Player, Radius, Duration, DamageBonus);
        GetTree().Root.AddChild(_effect);

        // Apply buff to player
        ApplyDamageBuff(Player, true);

        // Apply to nearby allies (pets, etc)
        ApplyToNearbyAllies();

        SetActiveWithDuration(Duration);

        GD.Print($"[WarCry3D] Activated - {DamageBonus * 100}% damage boost for {Duration}s");
    }

    private void ApplyToNearbyAllies()
    {
        foreach (var node in GetTree().GetNodesInGroup("Allies"))
        {
            if (node is Node3D ally && Player != null)
            {
                float dist = ally.GlobalPosition.DistanceTo(Player.GlobalPosition);
                if (dist <= Radius)
                {
                    ApplyDamageBuff(ally, true);
                }
            }
        }
    }

    private void ApplyDamageBuff(Node3D target, bool apply)
    {
        if (target.HasMethod("SetDamageMultiplier"))
        {
            float multiplier = apply ? (1f + DamageBonus) : 1f;
            target.Call("SetDamageMultiplier", multiplier);
        }
    }

    protected override void OnDeactivate()
    {
        // Remove buff from player
        if (Player != null)
        {
            ApplyDamageBuff(Player, false);
        }

        // Remove from allies
        foreach (var node in GetTree().GetNodesInGroup("Allies"))
        {
            if (node is Node3D ally)
            {
                ApplyDamageBuff(ally, false);
            }
        }

        if (_effect != null && IsInstanceValid(_effect))
        {
            _effect.FadeOut();
        }
        _effect = null;

        GD.Print("[WarCry3D] Deactivated");
    }
}

/// <summary>
/// Visual effect for War Cry - pulsing aura around player.
/// </summary>
public partial class WarCryEffect : Node3D
{
    private Node3D? _player;
    private float _radius;
    private float _duration;
    private float _damageBonus;

    private MeshInstance3D? _auraMesh;
    private GpuParticles3D? _particles;
    private OmniLight3D? _light;
    private StandardMaterial3D? _material;
    private float _timer;

    public void Initialize(Node3D player, float radius, float duration, float damageBonus)
    {
        _player = player;
        _radius = radius;
        _duration = duration;
        _damageBonus = damageBonus;
    }

    public override void _Ready()
    {
        CreateInitialBurst();
        CreateAura();
        CreateParticles();
        CreateLight();
    }

    private void CreateInitialBurst()
    {
        // Expanding ring effect for the "cry"
        var ring = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 0.3f;
        torus.OuterRadius = 0.5f;
        ring.Mesh = torus;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.8f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.5f, 0.1f);
        mat.EmissionEnergyMultiplier = 3f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        ring.MaterialOverride = mat;

        ring.GlobalPosition = _player?.GlobalPosition ?? GlobalPosition;
        ring.GlobalPosition += Vector3.Up * 0.5f;
        ring.RotationDegrees = new Vector3(90, 0, 0);
        GetTree().Root.AddChild(ring);

        var tween = ring.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(ring, "scale", Vector3.One * (_radius * 2), 0.5f);
        tween.TweenProperty(mat, "albedo_color:a", 0f, 0.5f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => ring.QueueFree()));
    }

    private void CreateAura()
    {
        _auraMesh = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 1.5f;
        cylinder.BottomRadius = 1.5f;
        cylinder.Height = 2.5f;
        _auraMesh.Mesh = cylinder;

        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(1f, 0.5f, 0.1f, 0.15f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(1f, 0.4f, 0f);
        _material.EmissionEnergyMultiplier = 1f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _auraMesh.MaterialOverride = _material;

        AddChild(_auraMesh);
    }

    private void CreateParticles()
    {
        _particles = new GpuParticles3D();
        _particles.Amount = 25;
        _particles.Lifetime = 1f;
        _particles.Emitting = true;

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        mat.EmissionSphereRadius = 1f;
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 30f;
        mat.InitialVelocityMin = 2f;
        mat.InitialVelocityMax = 4f;
        mat.Gravity = new Vector3(0, -1, 0);
        mat.ScaleMin = 0.08f;
        mat.ScaleMax = 0.15f;
        mat.Color = new Color(1f, 0.6f, 0.2f, 0.8f);
        _particles.ProcessMaterial = mat;

        var mesh = new SphereMesh();
        mesh.Radius = 0.08f;
        mesh.Height = 0.16f;
        _particles.DrawPass1 = mesh;

        _particles.Position = Vector3.Up * 1f;
        AddChild(_particles);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(1f, 0.5f, 0.1f);
        _light.LightEnergy = 1.5f;
        _light.OmniRange = 5f;
        _light.Position = Vector3.Up * 1.5f;
        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _timer += dt;

        // Follow player
        if (_player != null && IsInstanceValid(_player))
        {
            GlobalPosition = _player.GlobalPosition;
        }

        // Pulse effect
        float pulse = (Mathf.Sin(_timer * 4f) + 1f) * 0.5f;
        if (_material != null)
        {
            _material.AlbedoColor = new Color(1f, 0.5f, 0.1f, 0.1f + pulse * 0.1f);
            _material.EmissionEnergyMultiplier = 0.8f + pulse * 0.4f;
        }

        if (_light != null)
        {
            _light.LightEnergy = 1f + pulse * 0.5f;
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
