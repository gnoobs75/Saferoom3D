using Godot;
using SafeRoom3D.Enemies;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC;

/// <summary>
/// Chad "The Champ" Valorius - Cocky showoff crawler with boastful advice.
/// Athletic build, bright outfit, signature headband. Arrogant but well-meaning.
/// </summary>
public partial class CrawlerChad : BaseNPC3D
{
    public override string NPCName => "Chad \"The Champ\" Valorius";
    public override string InteractionPrompt => "Press T to Talk";
    public override bool IsShopkeeper => false;

    private float _flexTimer;
    private float _breathTimer;
    private float _poseTimer;
    private int _currentPose;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("Crawlers");
        GD.Print("[CrawlerChad] Chad spawned");
    }

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;
        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "crawler_chad");
        GD.Print("[CrawlerChad] Created Chad mesh");
    }

    protected override float GetNameplateHeight() => 1.35f;
    protected override float GetInteractionRange() => 4.5f; // Wants attention

    public override void Interact(Node3D player)
    {
        GD.Print("[CrawlerChad] Player interacting with Chad");
        CrawlerDialogueUI3D.Instance?.Open(this, "crawler_chad");
    }

    protected override void AnimateIdle(double delta)
    {
        if (_limbs == null) return;

        float dt = (float)delta;
        _flexTimer += dt;
        _breathTimer += dt;
        _poseTimer += dt;

        // Confident breathing (chest puffed)
        if (_limbs.Body != null)
        {
            float breathAmount = Mathf.Sin(_breathTimer * 1.5f) * 0.008f;
            _limbs.Body.Position = new Vector3(
                _limbs.Body.Position.X,
                0.68f + breathAmount,
                _limbs.Body.Position.Z
            );
            // Slight chest puff
            _limbs.Body.Scale = new Vector3(
                1.15f + Mathf.Sin(_breathTimer * 1.5f) * 0.02f,
                1f,
                0.85f
            );
        }

        // Cycling through poses
        if (_poseTimer > 4f)
        {
            _poseTimer = 0;
            _currentPose = (_currentPose + 1) % 3;
        }

        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float flexPulse = Mathf.Sin(_flexTimer * 3f) * 3f;

            switch (_currentPose)
            {
                case 0: // Double bicep flex
                    _limbs.LeftArm.RotationDegrees = new Vector3(-60 + flexPulse, 30, 90);
                    _limbs.RightArm.RotationDegrees = new Vector3(-60 + flexPulse, -30, -90);
                    break;
                case 1: // One arm point
                    _limbs.LeftArm.RotationDegrees = new Vector3(0, 0, 25);
                    _limbs.RightArm.RotationDegrees = new Vector3(-70, 30 + flexPulse, -20);
                    break;
                case 2: // Crossed arms (confident)
                    _limbs.LeftArm.RotationDegrees = new Vector3(30, 40, 50 + flexPulse);
                    _limbs.RightArm.RotationDegrees = new Vector3(30, -40, -50 - flexPulse);
                    break;
            }
        }

        // Confident head tilt
        if (_limbs.Head != null)
        {
            float headTilt = Mathf.Sin(_flexTimer * 0.5f) * 5f;
            _limbs.Head.RotationDegrees = new Vector3(-5, headTilt, 3); // Slight smug tilt
        }
    }
}
