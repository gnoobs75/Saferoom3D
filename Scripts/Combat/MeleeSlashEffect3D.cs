using Godot;

namespace SafeRoom3D.Combat;

/// <summary>
/// 3D sword slash visual effect that appears in front of the camera when attacking.
/// Creates an animated arc/slash effect visible in first-person view.
/// Supports multiple slash directions for varied combat feel.
/// </summary>
public partial class MeleeSlashEffect3D : Node3D
{
    private MeshInstance3D? _slashMesh;
    private float _animationTime;
    private float _animationDuration = 0.3f; // Longer duration for wider swing
    private StandardMaterial3D? _material;

    // Slash direction tracking (for alternating slashes)
    private static int _slashCounter = 0;
    private static float _lastSlashTime = 0f;

    // Slash types: 0=right-to-left, 1=left-to-right, 2=downward, 3=upward diagonal
    private int _slashType;

    // Animation curve parameters (adjusted per slash type)
    private float _startAngle;
    private float _endAngle;
    private float _startScale = 0.4f;
    private float _endScale = 1.4f;
    private float _slashWidth = 150f; // Wider arc span

    // Vertical offset for different slash types
    private float _verticalOffset;
    private float _horizontalOffset;
    private float _rotationOffset;

    public override void _Ready()
    {
        // Determine slash type based on counter and timing
        float currentTime = (float)Time.GetTicksMsec() / 1000f;

        // Reset counter if it's been a while since last slash (player stopped spamming)
        if (currentTime - _lastSlashTime > 1.0f)
        {
            _slashCounter = 0;
        }

        _slashType = _slashCounter % 4;
        _slashCounter++;
        _lastSlashTime = currentTime;

        // Configure slash based on type
        ConfigureSlashType();
        CreateSlashMesh();
    }

    private void ConfigureSlashType()
    {
        switch (_slashType)
        {
            case 0: // Right-to-left horizontal (default, REVERSED from original)
                _startAngle = 80f;
                _endAngle = -80f;
                _verticalOffset = 0f;
                _horizontalOffset = 0f;
                _rotationOffset = 0f;
                break;

            case 1: // Left-to-right horizontal (opposite of default)
                _startAngle = -80f;
                _endAngle = 80f;
                _verticalOffset = 0f;
                _horizontalOffset = 0f;
                _rotationOffset = 0f;
                break;

            case 2: // Downward diagonal slash
                _startAngle = 60f;
                _endAngle = -60f;
                _verticalOffset = 0.15f;
                _horizontalOffset = 0.1f;
                _rotationOffset = 45f; // Tilt the slash
                break;

            case 3: // Upward diagonal slash
                _startAngle = -60f;
                _endAngle = 60f;
                _verticalOffset = -0.1f;
                _horizontalOffset = -0.1f;
                _rotationOffset = -45f;
                break;
        }
    }

    private void CreateSlashMesh()
    {
        _slashMesh = new MeshInstance3D();

        // Create a curved arc mesh for the slash
        var mesh = new ImmediateMesh();
        _slashMesh.Mesh = mesh;

        // Create material
        _material = new StandardMaterial3D();
        _material.AlbedoColor = new Color(1f, 1f, 1f, 0.9f);
        _material.EmissionEnabled = true;
        _material.Emission = new Color(0.8f, 0.9f, 1f);
        _material.EmissionEnergyMultiplier = 2.5f;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _material.NoDepthTest = true; // Always render on top
        _slashMesh.MaterialOverride = _material;

        // Position in front of camera with offsets based on slash type
        _slashMesh.Position = new Vector3(
            0.3f + _horizontalOffset,
            -0.1f + _verticalOffset,
            -1.0f  // Slightly further out for wider slash
        );

        // Apply rotation for diagonal slashes
        _slashMesh.RotationDegrees = new Vector3(0, 0, _rotationOffset);

        AddChild(_slashMesh);

        // Start animation
        _animationTime = 0f;
    }

    public override void _Process(double delta)
    {
        _animationTime += (float)delta;
        float progress = _animationTime / _animationDuration;

        if (progress >= 1f)
        {
            QueueFree();
            return;
        }

        // Update the slash visual
        UpdateSlashVisual(progress);
    }

    private void UpdateSlashVisual(float progress)
    {
        if (_slashMesh?.Mesh is not ImmediateMesh mesh || _material == null) return;

        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, _material);

        // Arc parameters - wider and longer
        float currentAngle = Mathf.Lerp(_startAngle, _endAngle, progress);
        float arcWidth = _slashWidth; // Wider arc span
        float radius = 0.8f; // Larger radius for longer reach
        float thickness = 0.12f + (1f - progress) * 0.15f; // Thicker slash trail

        // Ease-out for smooth motion
        float easeProgress = 1f - Mathf.Pow(1f - progress, 2f);
        float scale = Mathf.Lerp(_startScale, _endScale, easeProgress);

        // Alpha fade
        float alpha = 1f - progress * 0.8f; // Slower fade
        _material.AlbedoColor = new Color(1f, 1f, 1f, alpha * 0.9f);
        _material.EmissionEnergyMultiplier = 2.5f * alpha;

        // Draw arc segments
        int segments = 20; // More segments for smoother arc
        float startArc = currentAngle - arcWidth * (1f - progress) * 0.5f;
        float endArc = currentAngle + arcWidth * progress * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.DegToRad(Mathf.Lerp(startArc, endArc, t));

            // Calculate positions for inner and outer edge
            float innerRadius = radius * scale;
            float outerRadius = (radius + thickness) * scale;

            Vector3 innerPos = new Vector3(
                Mathf.Sin(angle) * innerRadius,
                Mathf.Cos(angle) * innerRadius * 0.35f, // Slightly taller arc
                0
            );

            Vector3 outerPos = new Vector3(
                Mathf.Sin(angle) * outerRadius,
                Mathf.Cos(angle) * outerRadius * 0.35f,
                0
            );

            // Trail effect - earlier segments are more transparent
            float segmentAlpha = t * alpha;

            mesh.SurfaceSetColor(new Color(1f, 1f, 1f, segmentAlpha * 0.5f));
            mesh.SurfaceAddVertex(innerPos);

            mesh.SurfaceSetColor(new Color(1f, 1f, 1f, segmentAlpha));
            mesh.SurfaceAddVertex(outerPos);
        }

        mesh.SurfaceEnd();
    }

    /// <summary>
    /// Create and attach a slash effect to the camera.
    /// </summary>
    public static MeleeSlashEffect3D Create(Node3D parent)
    {
        var effect = new MeleeSlashEffect3D();
        effect.Name = "MeleeSlashEffect";
        parent.AddChild(effect);
        return effect;
    }

    /// <summary>
    /// Create a strong attack slash (larger, slower, more dramatic)
    /// </summary>
    public static MeleeSlashEffect3D CreateStrongAttack(Node3D parent)
    {
        var effect = new MeleeSlashEffect3D();
        effect.Name = "StrongMeleeSlashEffect";
        effect._animationDuration = 0.4f;
        effect._startScale = 0.5f;
        effect._endScale = 1.8f;
        effect._slashWidth = 180f;
        parent.AddChild(effect);
        return effect;
    }

    /// <summary>
    /// Reset the slash counter (e.g., when combat ends or player returns to idle)
    /// </summary>
    public static void ResetSlashCounter()
    {
        _slashCounter = 0;
    }
}
