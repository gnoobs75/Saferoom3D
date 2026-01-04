using Godot;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// 3D targeting indicator that shows where abilities will be cast.
/// Displays as a ring on the ground at the cursor position.
/// </summary>
public partial class TargetingIndicator3D : Node3D
{
    private MeshInstance3D? _ringMesh;
    private MeshInstance3D? _fillMesh;
    private StandardMaterial3D? _ringMaterial;
    private StandardMaterial3D? _fillMaterial;

    private float _radius = 5f;
    private Color _color = Colors.Orange;
    private float _pulseTimer;
    private const float PulseSpeed = 3f;

    public override void _Ready()
    {
        // Allow processing during time-pause targeting
        ProcessMode = ProcessModeEnum.Always;

        CreateRingMesh();
        CreateFillMesh();
    }

    public override void _Process(double delta)
    {
        // Pulse animation
        _pulseTimer += (float)delta * PulseSpeed;
        float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;

        // Update ring scale with pulse
        if (_ringMesh != null)
        {
            float scale = 1f + pulse * 0.05f;
            _ringMesh.Scale = new Vector3(scale, 1f, scale);
        }

        // Update fill alpha with pulse
        if (_fillMaterial != null)
        {
            _fillMaterial.AlbedoColor = new Color(_color.R, _color.G, _color.B, 0.1f + pulse * 0.1f);
        }
    }

    private void CreateRingMesh()
    {
        _ringMesh = new MeshInstance3D();

        // Create ring using torus mesh
        var torusMesh = new TorusMesh();
        torusMesh.InnerRadius = _radius - 0.1f;
        torusMesh.OuterRadius = _radius;
        torusMesh.Rings = 32;
        torusMesh.RingSegments = 4;

        _ringMesh.Mesh = torusMesh;

        // Create material
        _ringMaterial = new StandardMaterial3D();
        _ringMaterial.AlbedoColor = _color;
        _ringMaterial.EmissionEnabled = true;
        _ringMaterial.Emission = _color;
        _ringMaterial.EmissionEnergyMultiplier = 2f;
        _ringMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _ringMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        _ringMesh.MaterialOverride = _ringMaterial;

        // Rotate to lie flat on ground
        _ringMesh.RotationDegrees = new Vector3(90, 0, 0);
        _ringMesh.Position = new Vector3(0, 0.05f, 0); // Slightly above ground

        AddChild(_ringMesh);
    }

    private void CreateFillMesh()
    {
        _fillMesh = new MeshInstance3D();

        // Create filled circle using cylinder
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = _radius;
        cylinder.BottomRadius = _radius;
        cylinder.Height = 0.02f;
        cylinder.RadialSegments = 32;

        _fillMesh.Mesh = cylinder;

        // Create semi-transparent fill material
        _fillMaterial = new StandardMaterial3D();
        _fillMaterial.AlbedoColor = new Color(_color.R, _color.G, _color.B, 0.15f);
        _fillMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _fillMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _fillMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        _fillMesh.MaterialOverride = _fillMaterial;

        _fillMesh.Position = new Vector3(0, 0.01f, 0); // On ground

        AddChild(_fillMesh);
    }

    /// <summary>
    /// Set the radius of the targeting indicator.
    /// </summary>
    public void SetRadius(float radius)
    {
        _radius = radius;

        // Update ring mesh
        if (_ringMesh?.Mesh is TorusMesh torusMesh)
        {
            torusMesh.InnerRadius = radius - 0.1f;
            torusMesh.OuterRadius = radius;
        }

        // Update fill mesh
        if (_fillMesh?.Mesh is CylinderMesh cylinder)
        {
            cylinder.TopRadius = radius;
            cylinder.BottomRadius = radius;
        }
    }

    /// <summary>
    /// Set the color of the targeting indicator.
    /// </summary>
    public void SetColor(Color color)
    {
        _color = color;

        if (_ringMaterial != null)
        {
            _ringMaterial.AlbedoColor = color;
            _ringMaterial.Emission = color;
        }

        if (_fillMaterial != null)
        {
            _fillMaterial.AlbedoColor = new Color(color.R, color.G, color.B, 0.15f);
        }
    }
}
