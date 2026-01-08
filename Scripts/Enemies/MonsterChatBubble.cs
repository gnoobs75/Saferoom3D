using Godot;

namespace SafeRoom3D.Enemies;

/// <summary>
/// A floating speech bubble that appears above monsters when they chat.
/// Auto-fades and destroys itself after a set duration.
/// </summary>
public partial class MonsterChatBubble : Node3D
{
    private Label3D? _textLabel;
    private MeshInstance3D? _bubbleBackground;
    private float _lifetime;
    private float _timer = 0f;
    private float _fadeStartTime;
    private Color _originalColor;

    private const float FadeDuration = 0.8f;
    private const float BobSpeed = 2f;
    private const float BobAmount = 0.05f;

    private Vector3 _basePosition;

    /// <summary>
    /// Creates a chat bubble at the given position with the specified text.
    /// </summary>
    public static MonsterChatBubble Create(string text, Vector3 worldPosition, float duration = 4f)
    {
        var bubble = new MonsterChatBubble();
        bubble._lifetime = duration;
        bubble._fadeStartTime = duration - FadeDuration;
        bubble.GlobalPosition = worldPosition;
        bubble._basePosition = worldPosition;

        // Create text label
        bubble._textLabel = new Label3D();
        bubble._textLabel.Text = text;
        bubble._textLabel.FontSize = 48;
        bubble._textLabel.OutlineSize = 8;
        bubble._textLabel.Modulate = new Color(1f, 1f, 0.9f); // Warm white
        bubble._textLabel.OutlineModulate = new Color(0.1f, 0.1f, 0.15f); // Dark outline
        bubble._textLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        bubble._textLabel.NoDepthTest = true; // Always visible
        bubble._textLabel.PixelSize = 0.005f;
        bubble._originalColor = bubble._textLabel.Modulate;

        // Offset text slightly up from bubble center
        bubble._textLabel.Position = new Vector3(0, 0.1f, 0);
        bubble.AddChild(bubble._textLabel);

        // Create speech bubble background
        bubble.CreateBubbleBackground(text.Length);

        return bubble;
    }

    private void CreateBubbleBackground(int textLength)
    {
        // Size based on text length
        float width = Mathf.Max(0.8f, textLength * 0.04f);
        float height = 0.4f;

        // Create a rounded box mesh for the bubble
        var mesh = new BoxMesh();
        mesh.Size = new Vector3(width, height, 0.02f);

        _bubbleBackground = new MeshInstance3D();
        _bubbleBackground.Mesh = mesh;

        // White/cream material with slight transparency
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.95f, 0.93f, 0.88f, 0.9f);
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        material.NoDepthTest = true;
        _bubbleBackground.MaterialOverride = material;

        // Position behind text
        _bubbleBackground.Position = new Vector3(0, 0.05f, 0.01f);
        AddChild(_bubbleBackground);

        // Add a small triangle pointer at bottom
        CreatePointer();
    }

    private void CreatePointer()
    {
        // Create a small downward-pointing triangle
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // Triangle vertices (pointing down)
        st.AddVertex(new Vector3(-0.08f, -0.15f, 0));
        st.AddVertex(new Vector3(0.08f, -0.15f, 0));
        st.AddVertex(new Vector3(0, -0.3f, 0));

        st.GenerateNormals();
        var pointerMesh = st.Commit();

        var pointer = new MeshInstance3D();
        pointer.Mesh = pointerMesh;

        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.95f, 0.93f, 0.88f, 0.9f);
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        material.NoDepthTest = true;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        pointer.MaterialOverride = material;

        AddChild(pointer);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _timer += dt;

        // Gentle bobbing animation
        float bob = Mathf.Sin(_timer * BobSpeed) * BobAmount;
        GlobalPosition = _basePosition + new Vector3(0, bob, 0);

        // Fade out at end of lifetime
        if (_timer >= _fadeStartTime && _textLabel != null)
        {
            float fadeProgress = (_timer - _fadeStartTime) / FadeDuration;
            float alpha = 1f - fadeProgress;

            _textLabel.Modulate = new Color(
                _originalColor.R,
                _originalColor.G,
                _originalColor.B,
                alpha
            );

            // Also fade background
            if (_bubbleBackground?.MaterialOverride is StandardMaterial3D bgMat)
            {
                bgMat.AlbedoColor = new Color(0.95f, 0.93f, 0.88f, 0.9f * alpha);
            }
        }

        // Destroy when lifetime expires
        if (_timer >= _lifetime)
        {
            QueueFree();
        }
    }

    /// <summary>
    /// Update the bubble's base position (follows monster).
    /// </summary>
    public void SetBasePosition(Vector3 position)
    {
        _basePosition = position;
    }
}
