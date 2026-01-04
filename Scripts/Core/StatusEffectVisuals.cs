using Godot;
using System;

namespace SafeRoom3D.Core;

/// <summary>
/// Factory for creating visual effects for status effects.
/// Creates particle systems, lights, and mesh overlays.
/// </summary>
public static class StatusEffectVisuals
{
    /// <summary>
    /// Creates the visual effect node for a status effect type.
    /// </summary>
    public static Node3D CreateVisualEffect(StatusEffectType type, float entityHeight = 1.5f)
    {
        var container = new Node3D();
        container.Name = $"StatusEffect_{type}";

        switch (type)
        {
            case StatusEffectType.Burning:
                CreateBurningEffect(container, entityHeight);
                break;
            case StatusEffectType.Electrified:
                CreateElectrifiedEffect(container, entityHeight);
                break;
            case StatusEffectType.Frozen:
                CreateFrozenEffect(container, entityHeight);
                break;
            case StatusEffectType.Poisoned:
                CreatePoisonedEffect(container, entityHeight);
                break;
            case StatusEffectType.Bleeding:
                CreateBleedingEffect(container, entityHeight);
                break;
            case StatusEffectType.Petrified:
                CreatePetrifiedEffect(container, entityHeight);
                break;
            case StatusEffectType.Cursed:
                CreateCursedEffect(container, entityHeight);
                break;
            case StatusEffectType.Blessed:
                CreateBlessedEffect(container, entityHeight);
                break;
            case StatusEffectType.Confused:
                CreateConfusedEffect(container, entityHeight);
                break;
            case StatusEffectType.Enraged:
                CreateEnragedEffect(container, entityHeight);
                break;
        }

        return container;
    }

    private static void CreateBurningEffect(Node3D container, float height)
    {
        // Fire particles rising from body
        var particles = new GpuParticles3D();
        particles.Amount = 30;
        particles.Lifetime = 0.8f;
        particles.Explosiveness = 0.1f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 30f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, 2, 0);
        material.ScaleMin = 0.1f;
        material.ScaleMax = 0.3f;
        material.Color = new Color(1f, 0.5f, 0.1f);

        var colorRamp = new GradientTexture1D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.8f, 0.2f, 1f));
        gradient.SetColor(1, new Color(1f, 0.2f, 0.1f, 0f));
        colorRamp.Gradient = gradient;
        material.ColorRamp = colorRamp;

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.5f, 0);

        var drawMesh = new SphereMesh { Radius = 0.08f, Height = 0.16f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Flickering orange light
        var light = new OmniLight3D();
        light.LightColor = new Color(1f, 0.5f, 0.1f);
        light.LightEnergy = 1.5f;
        light.OmniRange = 3f;
        light.Position = new Vector3(0, height * 0.5f, 0);
        container.AddChild(light);

        // Add flicker script
        var flickerNode = new StatusEffectFlicker();
        flickerNode.TargetLight = light;
        flickerNode.FlickerSpeed = 15f;
        flickerNode.FlickerAmount = 0.4f;
        container.AddChild(flickerNode);
    }

    private static void CreateElectrifiedEffect(Node3D container, float height)
    {
        // Electric sparks
        var particles = new GpuParticles3D();
        particles.Amount = 20;
        particles.Lifetime = 0.3f;
        particles.Explosiveness = 0.8f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 0, 0);
        material.Spread = 180f;
        material.InitialVelocityMin = 2f;
        material.InitialVelocityMax = 5f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.02f;
        material.ScaleMax = 0.08f;
        material.Color = new Color(0.5f, 0.8f, 1f);

        var colorRamp = new GradientTexture1D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 1f, 1f, 1f));
        gradient.SetColor(1, new Color(0.4f, 0.7f, 1f, 0f));
        colorRamp.Gradient = gradient;
        material.ColorRamp = colorRamp;

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.5f, 0);

        var drawMesh = new SphereMesh { Radius = 0.03f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Electric arcs (using simple lines)
        for (int i = 0; i < 3; i++)
        {
            var arc = CreateElectricArc(height);
            arc.Position = new Vector3(0, height * 0.3f + i * 0.2f, 0);
            container.AddChild(arc);
        }

        // Blue flickering light
        var light = new OmniLight3D();
        light.LightColor = new Color(0.5f, 0.8f, 1f);
        light.LightEnergy = 1f;
        light.OmniRange = 2.5f;
        light.Position = new Vector3(0, height * 0.5f, 0);
        container.AddChild(light);

        var flickerNode = new StatusEffectFlicker();
        flickerNode.TargetLight = light;
        flickerNode.FlickerSpeed = 30f;
        flickerNode.FlickerAmount = 0.8f;
        container.AddChild(flickerNode);
    }

    private static Node3D CreateElectricArc(float height)
    {
        var arc = new MeshInstance3D();
        var mesh = new CylinderMesh();
        mesh.TopRadius = 0.01f;
        mesh.BottomRadius = 0.01f;
        mesh.Height = 0.15f;
        arc.Mesh = mesh;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.7f, 0.9f, 1f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.5f, 0.8f, 1f);
        mat.EmissionEnergyMultiplier = 2f;
        arc.MaterialOverride = mat;

        arc.RotationDegrees = new Vector3(
            (float)GD.RandRange(-60, 60),
            (float)GD.RandRange(0, 360),
            0
        );

        return arc;
    }

    private static void CreateFrozenEffect(Node3D container, float height)
    {
        // Ice crystals
        for (int i = 0; i < 5; i++)
        {
            var crystal = new MeshInstance3D();
            var mesh = new PrismMesh();
            mesh.Size = new Vector3(0.08f, 0.2f, 0.08f);
            crystal.Mesh = mesh;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.7f, 0.9f, 1f, 0.7f);
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.Roughness = 0.1f;
            mat.Metallic = 0.3f;
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.5f, 0.8f, 1f);
            mat.EmissionEnergyMultiplier = 0.3f;
            crystal.MaterialOverride = mat;

            float angle = i * Mathf.Pi * 2f / 5f;
            float radius = 0.25f;
            crystal.Position = new Vector3(
                Mathf.Cos(angle) * radius,
                height * 0.2f + (float)GD.RandRange(-0.1f, 0.2f),
                Mathf.Sin(angle) * radius
            );
            crystal.RotationDegrees = new Vector3(
                (float)GD.RandRange(-20, 20),
                angle * 180f / Mathf.Pi,
                (float)GD.RandRange(-10, 10)
            );

            container.AddChild(crystal);
        }

        // Frost particles
        var particles = new GpuParticles3D();
        particles.Amount = 15;
        particles.Lifetime = 1.5f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, -0.2f, 0);
        material.Spread = 45f;
        material.InitialVelocityMin = 0.2f;
        material.InitialVelocityMax = 0.5f;
        material.Gravity = new Vector3(0, -0.3f, 0);
        material.ScaleMin = 0.02f;
        material.ScaleMax = 0.05f;
        material.Color = new Color(0.8f, 0.95f, 1f, 0.8f);

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.7f, 0);

        var drawMesh = new SphereMesh { Radius = 0.02f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Cold blue light
        var light = new OmniLight3D();
        light.LightColor = new Color(0.6f, 0.85f, 1f);
        light.LightEnergy = 0.5f;
        light.OmniRange = 2f;
        light.Position = new Vector3(0, height * 0.5f, 0);
        container.AddChild(light);
    }

    private static void CreatePoisonedEffect(Node3D container, float height)
    {
        // Poison bubbles rising
        var particles = new GpuParticles3D();
        particles.Amount = 20;
        particles.Lifetime = 1.2f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 30f;
        material.InitialVelocityMin = 0.5f;
        material.InitialVelocityMax = 1f;
        material.Gravity = new Vector3(0, 0.5f, 0);
        material.ScaleMin = 0.04f;
        material.ScaleMax = 0.1f;
        material.Color = new Color(0.3f, 0.9f, 0.2f, 0.8f);

        var colorRamp = new GradientTexture1D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.4f, 1f, 0.3f, 1f));
        gradient.SetColor(1, new Color(0.2f, 0.7f, 0.1f, 0f));
        colorRamp.Gradient = gradient;
        material.ColorRamp = colorRamp;

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.3f, 0);

        var drawMesh = new SphereMesh { Radius = 0.05f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Green glow
        var light = new OmniLight3D();
        light.LightColor = new Color(0.3f, 0.9f, 0.2f);
        light.LightEnergy = 0.7f;
        light.OmniRange = 2f;
        light.Position = new Vector3(0, height * 0.5f, 0);
        container.AddChild(light);
    }

    private static void CreateBleedingEffect(Node3D container, float height)
    {
        // Blood drips
        var particles = new GpuParticles3D();
        particles.Amount = 15;
        particles.Lifetime = 0.8f;
        particles.Explosiveness = 0.3f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, -1, 0);
        material.Spread = 20f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, -8f, 0);
        material.ScaleMin = 0.03f;
        material.ScaleMax = 0.06f;
        material.Color = new Color(0.7f, 0.1f, 0.1f);

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.6f, 0);

        var drawMesh = new SphereMesh { Radius = 0.03f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);
    }

    private static void CreatePetrifiedEffect(Node3D container, float height)
    {
        // Stone overlay - gray mesh shell
        var overlay = new MeshInstance3D();
        var mesh = new CapsuleMesh { Radius = 0.4f, Height = height * 0.9f };
        overlay.Mesh = mesh;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.Roughness = 1f;
        mat.Metallic = 0f;
        overlay.MaterialOverride = mat;
        overlay.Position = new Vector3(0, height * 0.5f, 0);

        container.AddChild(overlay);

        // Stone dust particles (minimal)
        var particles = new GpuParticles3D();
        particles.Amount = 5;
        particles.Lifetime = 2f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, -1, 0);
        material.Spread = 60f;
        material.InitialVelocityMin = 0.1f;
        material.InitialVelocityMax = 0.3f;
        material.Gravity = new Vector3(0, -0.5f, 0);
        material.ScaleMin = 0.02f;
        material.ScaleMax = 0.04f;
        material.Color = new Color(0.6f, 0.58f, 0.55f, 0.5f);

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.8f, 0);

        var drawMesh = new SphereMesh { Radius = 0.02f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);
    }

    private static void CreateCursedEffect(Node3D container, float height)
    {
        // Dark purple mist
        var particles = new GpuParticles3D();
        particles.Amount = 25;
        particles.Lifetime = 1.5f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 0.3f, 0);
        material.Spread = 60f;
        material.InitialVelocityMin = 0.3f;
        material.InitialVelocityMax = 0.6f;
        material.Gravity = new Vector3(0, 0.2f, 0);
        material.ScaleMin = 0.1f;
        material.ScaleMax = 0.25f;
        material.Color = new Color(0.4f, 0.1f, 0.5f, 0.6f);

        var colorRamp = new GradientTexture1D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.5f, 0.15f, 0.6f, 0.8f));
        gradient.SetColor(1, new Color(0.2f, 0.05f, 0.3f, 0f));
        colorRamp.Gradient = gradient;
        material.ColorRamp = colorRamp;

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.3f, 0);

        var drawMesh = new SphereMesh { Radius = 0.08f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Dark purple light
        var light = new OmniLight3D();
        light.LightColor = new Color(0.4f, 0.1f, 0.5f);
        light.LightEnergy = 0.6f;
        light.OmniRange = 2f;
        light.Position = new Vector3(0, height * 0.5f, 0);
        container.AddChild(light);
    }

    private static void CreateBlessedEffect(Node3D container, float height)
    {
        // Golden light rays
        var particles = new GpuParticles3D();
        particles.Amount = 20;
        particles.Lifetime = 1.2f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 30f;
        material.InitialVelocityMin = 0.5f;
        material.InitialVelocityMax = 1f;
        material.Gravity = new Vector3(0, 0.5f, 0);
        material.ScaleMin = 0.03f;
        material.ScaleMax = 0.08f;
        material.Color = new Color(1f, 0.95f, 0.6f);

        var colorRamp = new GradientTexture1D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 1f, 0.8f, 1f));
        gradient.SetColor(1, new Color(1f, 0.9f, 0.5f, 0f));
        colorRamp.Gradient = gradient;
        material.ColorRamp = colorRamp;

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.2f, 0);

        var drawMesh = new SphereMesh { Radius = 0.04f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Halo ring
        var halo = new MeshInstance3D();
        var haloMesh = new TorusMesh();
        haloMesh.InnerRadius = 0.25f;
        haloMesh.OuterRadius = 0.35f;
        halo.Mesh = haloMesh;

        var haloMat = new StandardMaterial3D();
        haloMat.AlbedoColor = new Color(1f, 0.95f, 0.6f, 0.5f);
        haloMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        haloMat.EmissionEnabled = true;
        haloMat.Emission = new Color(1f, 0.95f, 0.6f);
        haloMat.EmissionEnergyMultiplier = 1f;
        halo.MaterialOverride = haloMat;
        halo.Position = new Vector3(0, height + 0.2f, 0);
        halo.RotationDegrees = new Vector3(90, 0, 0);

        container.AddChild(halo);

        // Golden light
        var light = new OmniLight3D();
        light.LightColor = new Color(1f, 0.95f, 0.7f);
        light.LightEnergy = 1f;
        light.OmniRange = 3f;
        light.Position = new Vector3(0, height * 0.5f, 0);
        container.AddChild(light);
    }

    private static void CreateConfusedEffect(Node3D container, float height)
    {
        // Spiral particles (stars/spirals around head)
        var particles = new GpuParticles3D();
        particles.Amount = 8;
        particles.Lifetime = 2f;

        var material = new ParticleProcessMaterial();
        // Orbit around head using angular velocity
        material.Direction = new Vector3(1, 0, 0);
        material.Spread = 0f;
        material.InitialVelocityMin = 0.8f;
        material.InitialVelocityMax = 1f;
        material.OrbitVelocityMin = 0.5f;
        material.OrbitVelocityMax = 0.8f;
        material.Gravity = new Vector3(0, 0, 0);
        material.ScaleMin = 0.06f;
        material.ScaleMax = 0.1f;
        material.Color = new Color(1f, 1f, 0.5f);

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height + 0.15f, 0);

        var drawMesh = new SphereMesh { Radius = 0.05f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Spiral/question mark indicator
        var indicator = new MeshInstance3D();
        var indicatorMesh = new SphereMesh { Radius = 0.1f };
        indicator.Mesh = indicatorMesh;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.9f, 0.7f, 1f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.8f, 0.6f, 1f);
        mat.EmissionEnergyMultiplier = 0.5f;
        indicator.MaterialOverride = mat;
        indicator.Position = new Vector3(0, height + 0.3f, 0);

        container.AddChild(indicator);
    }

    private static void CreateEnragedEffect(Node3D container, float height)
    {
        // Red aura/veins pulsing
        var particles = new GpuParticles3D();
        particles.Amount = 25;
        particles.Lifetime = 0.6f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 60f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, 2f, 0);
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.12f;
        material.Color = new Color(1f, 0.2f, 0.2f, 0.8f);

        var colorRamp = new GradientTexture1D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.3f, 0.2f, 1f));
        gradient.SetColor(1, new Color(0.8f, 0.1f, 0.1f, 0f));
        colorRamp.Gradient = gradient;
        material.ColorRamp = colorRamp;

        particles.ProcessMaterial = material;
        particles.Position = new Vector3(0, height * 0.3f, 0);

        var drawMesh = new SphereMesh { Radius = 0.06f };
        particles.DrawPass1 = drawMesh;

        container.AddChild(particles);

        // Red pulsing light
        var light = new OmniLight3D();
        light.LightColor = new Color(1f, 0.2f, 0.15f);
        light.LightEnergy = 1.2f;
        light.OmniRange = 2.5f;
        light.Position = new Vector3(0, height * 0.5f, 0);
        container.AddChild(light);

        // Add pulse effect
        var pulseNode = new StatusEffectPulse();
        pulseNode.TargetLight = light;
        pulseNode.PulseSpeed = 4f;
        pulseNode.MinEnergy = 0.8f;
        pulseNode.MaxEnergy = 1.5f;
        container.AddChild(pulseNode);
    }
}

/// <summary>
/// Helper node for flickering light effects.
/// </summary>
public partial class StatusEffectFlicker : Node
{
    public OmniLight3D? TargetLight { get; set; }
    public float FlickerSpeed { get; set; } = 10f;
    public float FlickerAmount { get; set; } = 0.3f;

    private float _baseEnergy;
    private float _timer;

    public override void _Ready()
    {
        if (TargetLight != null)
        {
            _baseEnergy = TargetLight.LightEnergy;
        }
    }

    public override void _Process(double delta)
    {
        if (TargetLight == null) return;

        _timer += (float)delta * FlickerSpeed;
        float noise = Mathf.Sin(_timer) * Mathf.Sin(_timer * 2.3f) * Mathf.Sin(_timer * 0.7f);
        TargetLight.LightEnergy = _baseEnergy + noise * FlickerAmount * _baseEnergy;
    }
}

/// <summary>
/// Helper node for pulsing light effects.
/// </summary>
public partial class StatusEffectPulse : Node
{
    public OmniLight3D? TargetLight { get; set; }
    public float PulseSpeed { get; set; } = 2f;
    public float MinEnergy { get; set; } = 0.5f;
    public float MaxEnergy { get; set; } = 1.5f;

    private float _timer;

    public override void _Process(double delta)
    {
        if (TargetLight == null) return;

        _timer += (float)delta * PulseSpeed;
        float t = (Mathf.Sin(_timer) + 1f) * 0.5f;
        TargetLight.LightEnergy = Mathf.Lerp(MinEnergy, MaxEnergy, t);
    }
}
