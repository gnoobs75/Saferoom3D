using Godot;
using SafeRoom3D.Enemies;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC;

/// <summary>
/// The Silent One (Shade) - Mysterious hooded crawler with cryptic warnings.
/// Thin robed figure, glowing eyes under cowl. Ancient and otherworldly.
/// </summary>
public partial class CrawlerShade : BaseNPC3D
{
    public override string NPCName => "The Silent One";
    public override string InteractionPrompt => "Press T to Approach";
    public override bool IsShopkeeper => false;

    private float _floatTimer;
    private float _pulseTimer;
    private float _fadeTimer;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("Crawlers");
        GD.Print("[CrawlerShade] Shade spawned");
    }

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;
        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "crawler_shade");
        GD.Print("[CrawlerShade] Created Shade mesh");
    }

    protected override float GetNameplateHeight() => 1.25f;
    protected override float GetInteractionRange() => 3.5f; // Keeps distance

    public override void Interact(Node3D player)
    {
        GD.Print("[CrawlerShade] Player interacting with Shade");
        CrawlerDialogueUI3D.Instance?.Open(this, "crawler_shade");
    }

    protected override void AnimateIdle(double delta)
    {
        if (_limbs == null) return;

        float dt = (float)delta;
        _floatTimer += dt;
        _pulseTimer += dt * 2f;
        _fadeTimer += dt * 0.5f;

        // Subtle floating/hovering motion
        if (_limbs.Body != null)
        {
            float floatAmount = Mathf.Sin(_floatTimer * 0.8f) * 0.02f;
            float swayAmount = Mathf.Sin(_floatTimer * 0.5f) * 0.01f;
            _limbs.Body.Position = new Vector3(
                swayAmount,
                0.55f + floatAmount,
                _limbs.Body.Position.Z
            );
        }

        // Arms held together mysteriously (meditation pose)
        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float armSway = Mathf.Sin(_floatTimer * 0.6f) * 2f;
            _limbs.LeftArm.RotationDegrees = new Vector3(20 + armSway, 0, 30);
            _limbs.RightArm.RotationDegrees = new Vector3(20 + armSway, 0, -30);
        }

        // Very slow, deliberate head movement
        if (_limbs.Head != null)
        {
            float headTurn = Mathf.Sin(_fadeTimer) * 10f;
            float headTilt = Mathf.Sin(_fadeTimer * 0.7f) * 3f;
            _limbs.Head.RotationDegrees = new Vector3(headTilt - 5, headTurn, 0);
        }

        // Robe sway (using leg nodes as robe proxy)
        if (_limbs.LeftLeg != null && _limbs.RightLeg != null)
        {
            float robeSway = Mathf.Sin(_floatTimer * 0.4f) * 0.02f;
            _limbs.LeftLeg.Position = new Vector3(-0.08f + robeSway, 0.1f, 0);
            _limbs.RightLeg.Position = new Vector3(0.08f - robeSway, 0.1f, 0);
        }
    }
}
