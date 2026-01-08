using Godot;
using SafeRoom3D.Enemies;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC;

/// <summary>
/// Lily Chen - Nervous rookie crawler with dungeon hints and anxious observations.
/// Slim build, oversized gear, constantly fidgeting. Scared but resourceful.
/// </summary>
public partial class CrawlerLily : BaseNPC3D
{
    public override string NPCName => "Lily Chen";
    public override string InteractionPrompt => "Press T to Talk";
    public override bool IsShopkeeper => false;

    private float _fidgetTimer;
    private float _breathTimer;
    private float _jumpTimer;
    private bool _isJumping;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("Crawlers");
        GD.Print("[CrawlerLily] Lily spawned");
    }

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;
        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "crawler_lily");
        GD.Print("[CrawlerLily] Created Lily mesh");
    }

    protected override float GetNameplateHeight() => 1.15f;
    protected override float GetInteractionRange() => 4.0f;

    public override void Interact(Node3D player)
    {
        GD.Print("[CrawlerLily] Player interacting with Lily");
        CrawlerDialogueUI3D.Instance?.Open(this, "crawler_lily");
    }

    protected override void AnimateIdle(double delta)
    {
        if (_limbs == null) return;

        float dt = (float)delta;
        _fidgetTimer += dt;
        _breathTimer += dt * 1.8f; // Faster, nervous breathing
        _jumpTimer += dt;

        // Nervous quick breathing
        if (_limbs.Body != null)
        {
            float breathAmount = Mathf.Sin(_breathTimer) * 0.006f;
            float baseY = 0.54f; // Smaller scale
            _limbs.Body.Position = new Vector3(
                _limbs.Body.Position.X,
                baseY + breathAmount,
                _limbs.Body.Position.Z
            );
        }

        // Constant fidgeting with arms
        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float leftFidget = Mathf.Sin(_fidgetTimer * 2.5f) * 5f;
            float rightFidget = Mathf.Sin(_fidgetTimer * 3f + 1f) * 4f;
            _limbs.LeftArm.RotationDegrees = new Vector3(10 + leftFidget, 0, 20);
            _limbs.RightArm.RotationDegrees = new Vector3(10 + rightFidget, 0, -20);
        }

        // Occasional nervous glance
        if (_limbs.Head != null)
        {
            float headTwitch = Mathf.Sin(_fidgetTimer * 4f) * 3f;
            float headNod = Mathf.Sin(_fidgetTimer * 2f) * 2f;
            _limbs.Head.RotationDegrees = new Vector3(headNod - 5, headTwitch, 0);
        }

        // Occasional startled jump
        if (_jumpTimer > 8f + GD.Randf() * 4f)
        {
            _jumpTimer = 0;
            _isJumping = true;
        }

        if (_isJumping && _limbs.LeftLeg != null && _limbs.RightLeg != null)
        {
            // Quick startle animation
            float jumpProgress = _jumpTimer / 0.3f;
            if (jumpProgress < 1f)
            {
                float jumpHeight = Mathf.Sin(jumpProgress * Mathf.Pi) * 0.05f;
                _limbs.Body!.Position = new Vector3(
                    _limbs.Body.Position.X,
                    0.54f + jumpHeight,
                    _limbs.Body.Position.Z
                );
            }
            else
            {
                _isJumping = false;
            }
        }
    }
}
