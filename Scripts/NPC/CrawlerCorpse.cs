using Godot;

namespace SafeRoom3D.NPC;

/// <summary>
/// Permanent memorial corpse for fallen Crawler NPCs.
/// Unlike monster corpses, Crawler corpses CANNOT be looted.
/// They remain as permanent memorials in the dungeon.
/// </summary>
public partial class CrawlerCorpse : StaticBody3D
{
    public string CrawlerName { get; private set; } = "";
    public string CrawlerTitle { get; private set; } = "";

    private Label3D? _memorialLabel;
    private MeshInstance3D? _corpseMesh;

    /// <summary>
    /// Initialize the corpse at the death location.
    /// </summary>
    public void Initialize(string name, string title, Vector3 position, float rotationY)
    {
        CrawlerName = name;
        CrawlerTitle = title;
        GlobalPosition = position;
        Rotation = new Vector3(0, rotationY, 0);

        CreateCorpseVisuals();
        CreateMemorialLabel();

        // Add to group (but NOT Corpses group - not lootable)
        AddToGroup("CrawlerCorpses");

        GD.Print($"[CrawlerCorpse] Memorial created for {name} \"{title}\"");
    }

    private void CreateCorpseVisuals()
    {
        _corpseMesh = new MeshInstance3D();

        // Simple fallen body mesh - a horizontal capsule
        var capsule = new CapsuleMesh();
        capsule.Radius = 0.3f;
        capsule.Height = 1.5f;
        _corpseMesh.Mesh = capsule;

        // Rotate to lie on ground
        _corpseMesh.RotationDegrees = new Vector3(0, 0, 90);
        _corpseMesh.Position = new Vector3(0, 0.3f, 0);

        // Gray/desaturated material
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.4f, 0.4f, 0.45f);  // Grayish
        mat.Roughness = 0.9f;
        _corpseMesh.MaterialOverride = mat;

        AddChild(_corpseMesh);

        // Add collision shape
        var collision = new CollisionShape3D();
        var shape = new CapsuleShape3D();
        shape.Radius = 0.3f;
        shape.Height = 1.5f;
        collision.Shape = shape;
        collision.RotationDegrees = new Vector3(0, 0, 90);
        collision.Position = new Vector3(0, 0.3f, 0);
        AddChild(collision);
    }

    private void CreateMemorialLabel()
    {
        _memorialLabel = new Label3D();
        _memorialLabel.Text = $"R.I.P.\n{CrawlerName}\n\"{CrawlerTitle}\"";
        _memorialLabel.FontSize = 24;
        _memorialLabel.Modulate = new Color(0.7f, 0.7f, 0.8f, 0.9f);  // Ghostly white/blue
        _memorialLabel.Position = new Vector3(0, 0.8f, 0);
        _memorialLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _memorialLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _memorialLabel.NoDepthTest = true;
        AddChild(_memorialLabel);
    }

    public override void _Ready()
    {
        // Corpse just sits there - no processing needed
        SetProcess(false);
        SetPhysicsProcess(false);
    }
}
