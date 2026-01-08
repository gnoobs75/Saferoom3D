using Godot;
using SafeRoom3D.Enemies;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC;

/// <summary>
/// Hank "Noodles" Patterson - Comedic relief crawler with funny stories and random items.
/// Pudgy build, mismatched gear, goofy expression. Clumsy but lovable.
/// </summary>
public partial class CrawlerHank : BaseNPC3D
{
    public override string NPCName => "Hank \"Noodles\" Patterson";
    public override string InteractionPrompt => "Press T to Talk";
    public override bool IsShopkeeper => false;

    private float _wobbleTimer;
    private float _breathTimer;
    private float _tripTimer;
    private bool _isTripping;
    private float _tripProgress;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("Crawlers");
        GD.Print("[CrawlerHank] Hank spawned");
    }

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;
        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "crawler_hank");
        GD.Print("[CrawlerHank] Created Hank mesh");
    }

    protected override float GetNameplateHeight() => 1.2f;
    protected override float GetInteractionRange() => 4.0f;

    public override void Interact(Node3D player)
    {
        GD.Print("[CrawlerHank] Player interacting with Hank");
        CrawlerDialogueUI3D.Instance?.Open(this, "crawler_hank");
    }

    protected override void AnimateIdle(double delta)
    {
        if (_limbs == null) return;

        float dt = (float)delta;
        _wobbleTimer += dt;
        _breathTimer += dt;
        _tripTimer += dt;

        // Jolly breathing (round belly)
        if (_limbs.Body != null)
        {
            float breathAmount = Mathf.Sin(_breathTimer * 1.3f) * 0.01f;
            float wobble = Mathf.Sin(_wobbleTimer * 0.8f) * 0.005f;
            _limbs.Body.Position = new Vector3(
                wobble,
                0.55f + breathAmount,
                _limbs.Body.Position.Z
            );
            // Belly expansion
            _limbs.Body.Scale = new Vector3(
                1.1f + Mathf.Sin(_breathTimer * 1.3f) * 0.03f,
                1.2f,
                1f
            );
        }

        // Happy arm swinging
        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float leftSwing = Mathf.Sin(_wobbleTimer * 1.2f) * 10f;
            float rightSwing = Mathf.Sin(_wobbleTimer * 1.2f + 0.5f) * 10f;
            _limbs.LeftArm.RotationDegrees = new Vector3(leftSwing, 0, 20);
            _limbs.RightArm.RotationDegrees = new Vector3(rightSwing, 0, -20);
        }

        // Goofy head bobbing
        if (_limbs.Head != null)
        {
            float headBob = Mathf.Sin(_wobbleTimer * 1.5f) * 5f;
            float headTilt = Mathf.Sin(_wobbleTimer * 0.7f) * 8f;
            _limbs.Head.RotationDegrees = new Vector3(headBob, headTilt, Mathf.Sin(_wobbleTimer * 2f) * 3f);
        }

        // Occasional stumble
        if (_tripTimer > 10f + GD.Randf() * 5f && !_isTripping)
        {
            _tripTimer = 0;
            _isTripping = true;
            _tripProgress = 0;
        }

        if (_isTripping)
        {
            _tripProgress += dt * 3f;

            if (_tripProgress < 1f)
            {
                // Lean forward (almost falling)
                float leanAngle = Mathf.Sin(_tripProgress * Mathf.Pi) * 15f;
                if (_limbs.Body != null)
                {
                    _limbs.Body.RotationDegrees = new Vector3(leanAngle, 0, 0);
                }

                // Arms flail
                if (_limbs.LeftArm != null && _limbs.RightArm != null)
                {
                    float flail = Mathf.Sin(_tripProgress * Mathf.Pi * 4f) * 30f;
                    _limbs.LeftArm.RotationDegrees = new Vector3(-45 + flail, 0, 60);
                    _limbs.RightArm.RotationDegrees = new Vector3(-45 - flail, 0, -60);
                }
            }
            else
            {
                _isTripping = false;
                if (_limbs.Body != null)
                {
                    _limbs.Body.RotationDegrees = Vector3.Zero;
                }
            }
        }

        // Shuffling feet
        if (_limbs.LeftLeg != null && _limbs.RightLeg != null && !_isTripping)
        {
            float leftShuffle = Mathf.Sin(_wobbleTimer * 2f) * 0.01f;
            float rightShuffle = Mathf.Sin(_wobbleTimer * 2f + Mathf.Pi) * 0.01f;
            _limbs.LeftLeg.Position = new Vector3(-0.1f, 0.22f + leftShuffle, 0);
            _limbs.RightLeg.Position = new Vector3(0.1f, 0.22f + rightShuffle, 0);
        }
    }
}
