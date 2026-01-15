using Godot;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Blood splatter effect spawned when enemies die.
/// Creates both particle effects and persistent blood pool decals.
/// </summary>
public partial class BloodSplatter3D : Node3D
{
    private GpuParticles3D? _bloodParticles;
    private MeshInstance3D? _bloodPool;
    private float _poolExpandTime;
    private float _poolMaxRadius;
    private bool _expanding = true;

    // Despawn timer - blood pools fade after a while
    private float _despawnTimer = 90f; // 1.5 minutes
    private bool _fading;

    /// <summary>
    /// Creates a blood splatter effect at the given position.
    /// </summary>
    public static BloodSplatter3D Create(Vector3 position, string monsterType, bool isBoss = false)
    {
        var splatter = new BloodSplatter3D();
        splatter.Position = position;
        splatter.Initialize(monsterType, isBoss);
        return splatter;
    }

    private void Initialize(string monsterType, bool isBoss)
    {
        // Determine blood color based on monster type
        var bloodColor = GetBloodColor(monsterType);
        _poolMaxRadius = isBoss ? 1.8f : 0.8f + GD.Randf() * 0.4f;

        // Create burst of blood particles
        CreateBloodParticles(bloodColor, isBoss);

        // Create expanding blood pool on ground
        CreateBloodPool(bloodColor);
    }

    private Color GetBloodColor(string monsterType)
    {
        return monsterType.ToLower() switch
        {
            // Green blood for goblins and orcs
            "goblin" or "goblin_shaman" or "goblin_thrower" or "torchbearer"
                or "goblin_torchbearer" or "orc" => new Color(0.2f, 0.4f, 0.15f),

            // Glowing ichor for slimes
            "slime" or "toxic_slime" or "ice_slime" => new Color(0.3f, 0.5f, 0.2f, 0.8f),

            // White bone dust for skeletons
            "skeleton" or "skeleton_lord" or "skeleton_warrior"
                or "skeleton_archer" or "skeleton_mage" => new Color(0.85f, 0.82f, 0.75f),

            // Purple ichor for spiders
            "spider" or "spider_queen" or "giant_spider" => new Color(0.4f, 0.2f, 0.5f),

            // Orange fire blood for dragons and fire creatures
            "dragon" or "dragon_king" or "fire_elemental" => new Color(0.8f, 0.3f, 0.1f),

            // Blue blood for ice creatures
            "ice_golem" or "frost_giant" => new Color(0.2f, 0.4f, 0.7f),

            // Dark crimson for demons
            "demon" or "the_butcher" or "badlama" => new Color(0.5f, 0.1f, 0.1f),

            // Standard red blood for most creatures
            _ => new Color(0.5f, 0.08f, 0.08f)
        };
    }

    private void CreateBloodParticles(Color bloodColor, bool isBoss)
    {
        _bloodParticles = new GpuParticles3D();
        _bloodParticles.Amount = isBoss ? 40 : 20;
        _bloodParticles.Lifetime = 0.8f;
        _bloodParticles.OneShot = true;
        _bloodParticles.Explosiveness = 0.9f;
        _bloodParticles.Randomness = 0.3f;

        // Create particle material
        var processMaterial = new ParticleProcessMaterial();
        processMaterial.Direction = new Vector3(0, 1, 0);
        processMaterial.Spread = 60f;
        processMaterial.InitialVelocityMin = 2f;
        processMaterial.InitialVelocityMax = isBoss ? 6f : 4f;
        processMaterial.Gravity = new Vector3(0, -12f, 0);
        processMaterial.DampingMin = 2f;
        processMaterial.DampingMax = 4f;

        // Create scale curve for particle size over lifetime
        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, isBoss ? 1.5f : 1f));
        scaleCurve.AddPoint(new Vector2(1, isBoss ? 0.8f : 0.5f));
        var scaleCurveTexture = new CurveTexture();
        scaleCurveTexture.Curve = scaleCurve;
        processMaterial.ScaleCurve = scaleCurveTexture;

        // Color gradient: starts bright, fades darker
        var colorGradient = new Gradient();
        colorGradient.SetColor(0, bloodColor);
        colorGradient.SetColor(1, bloodColor.Darkened(0.5f));
        var gradientTexture = new GradientTexture1D();
        gradientTexture.Gradient = colorGradient;
        processMaterial.ColorRamp = gradientTexture;

        _bloodParticles.ProcessMaterial = processMaterial;

        // Simple sphere mesh for particles
        var sphereMesh = new SphereMesh();
        sphereMesh.Radius = 0.05f;
        sphereMesh.Height = 0.1f;
        sphereMesh.RadialSegments = 6;
        sphereMesh.Rings = 3;
        _bloodParticles.DrawPass1 = sphereMesh;

        // Blood particle material
        var drawMaterial = new StandardMaterial3D();
        drawMaterial.AlbedoColor = bloodColor;
        drawMaterial.Roughness = 0.3f;
        drawMaterial.Metallic = 0.1f;
        drawMaterial.EmissionEnabled = true;
        drawMaterial.Emission = bloodColor.Lightened(0.2f);
        drawMaterial.EmissionEnergyMultiplier = 0.3f;
        sphereMesh.Material = drawMaterial;

        _bloodParticles.Position = new Vector3(0, 0.3f, 0); // Slightly above ground
        AddChild(_bloodParticles);

        // Emit particles immediately
        _bloodParticles.Emitting = true;
    }

    private void CreateBloodPool(Color bloodColor)
    {
        _bloodPool = new MeshInstance3D();

        // Use a disc mesh for the blood pool
        var discMesh = new PlaneMesh();
        discMesh.Size = new Vector2(0.1f, 0.1f); // Start small
        _bloodPool.Mesh = discMesh;

        // Blood pool material - glossy and wet looking
        var poolMaterial = new StandardMaterial3D();
        poolMaterial.AlbedoColor = bloodColor.Darkened(0.2f);
        poolMaterial.Roughness = 0.2f; // Wet, shiny look
        poolMaterial.Metallic = 0.1f;
        poolMaterial.EmissionEnabled = true;
        poolMaterial.Emission = bloodColor.Darkened(0.3f);
        poolMaterial.EmissionEnergyMultiplier = 0.1f;
        poolMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        // Slight transparency for pooled blood effect
        poolMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        poolMaterial.AlbedoColor = new Color(poolMaterial.AlbedoColor.R,
            poolMaterial.AlbedoColor.G, poolMaterial.AlbedoColor.B, 0.85f);
        _bloodPool.MaterialOverride = poolMaterial;

        // Position just above floor to prevent z-fighting
        _bloodPool.Position = new Vector3(0, 0.01f, 0);
        // Add slight random rotation for variety
        _bloodPool.RotationDegrees = new Vector3(0, GD.Randf() * 360f, 0);

        AddChild(_bloodPool);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Expand blood pool over time
        if (_expanding && _bloodPool != null)
        {
            _poolExpandTime += dt * 2f;
            float progress = Mathf.Min(1f, _poolExpandTime);
            float currentRadius = Mathf.Lerp(0.1f, _poolMaxRadius, EaseOutQuad(progress));

            if (_bloodPool.Mesh is PlaneMesh planeMesh)
            {
                planeMesh.Size = new Vector2(currentRadius * 2f, currentRadius * 2f);
            }

            if (progress >= 1f)
            {
                _expanding = false;
            }
        }

        // Despawn timer
        _despawnTimer -= dt;
        if (_despawnTimer <= 0 && !_fading)
        {
            StartFadeOut();
        }
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private void StartFadeOut()
    {
        _fading = true;

        var tween = CreateTween();
        tween.SetParallel(true);

        // Fade out pool
        if (_bloodPool?.MaterialOverride is StandardMaterial3D poolMat)
        {
            var startColor = poolMat.AlbedoColor;
            var endColor = new Color(startColor.R, startColor.G, startColor.B, 0f);
            tween.TweenProperty(poolMat, "albedo_color", endColor, 3f);
        }

        // Scale down
        if (_bloodPool != null)
        {
            tween.TweenProperty(_bloodPool, "scale", Vector3.Zero, 3f);
        }

        tween.Chain().TweenCallback(Callable.From(() => QueueFree()));
    }
}
