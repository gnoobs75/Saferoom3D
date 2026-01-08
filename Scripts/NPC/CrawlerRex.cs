using Godot;
using SafeRoom3D.Enemies;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC;

/// <summary>
/// Rex "Ironside" Martinez - Grizzled veteran crawler with combat tips and war stories.
/// Muscular build, battle-scarred, military vest. Cynical but helpful.
/// </summary>
public partial class CrawlerRex : BaseNPC3D
{
    public override string NPCName => "Rex \"Ironside\" Martinez";
    public override string InteractionPrompt => "Press T to Talk";
    public override bool IsShopkeeper => false;

    private float _breathTimer;
    private float _shoulderRollTimer;
    private float _lookTimer;
    private Vector3 _targetLookDir = Vector3.Forward;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("Crawlers");
        GD.Print("[CrawlerRex] Rex spawned");
    }

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;
        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "crawler_rex");
        GD.Print("[CrawlerRex] Created Rex mesh");
    }

    protected override float GetNameplateHeight() => 1.3f;
    protected override float GetInteractionRange() => 4.0f;

    public override void Interact(Node3D player)
    {
        GD.Print("[CrawlerRex] Player interacting with Rex");
        CrawlerDialogueUI3D.Instance?.Open(this, "crawler_rex");
    }

    protected override void AnimateIdle(double delta)
    {
        if (_limbs == null) return;

        float dt = (float)delta;
        _breathTimer += dt;
        _shoulderRollTimer += dt * 0.3f;
        _lookTimer += dt;

        // Steady breathing
        if (_limbs.Body != null)
        {
            float breathAmount = Mathf.Sin(_breathTimer * 1.2f) * 0.003f;
            _limbs.Body.Position = new Vector3(
                _limbs.Body.Position.X,
                0.65f + breathAmount,
                _limbs.Body.Position.Z
            );
        }

        // Occasional shoulder roll (veteran habit)
        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float shoulderRoll = Mathf.Sin(_shoulderRollTimer) * 2f;
            _limbs.LeftArm.RotationDegrees = new Vector3(0, 0, 15 + shoulderRoll);
            _limbs.RightArm.RotationDegrees = new Vector3(0, 0, -15 - shoulderRoll * 0.5f);
        }

        // Alert head movements (checking surroundings)
        if (_limbs.Head != null)
        {
            if (_lookTimer > 3f)
            {
                _lookTimer = 0;
                _targetLookDir = new Vector3(
                    (float)GD.RandRange(-0.4f, 0.4f),
                    (float)GD.RandRange(-0.1f, 0.1f),
                    1f
                ).Normalized();
            }

            var currentRot = _limbs.Head.RotationDegrees;
            float targetYaw = Mathf.RadToDeg(Mathf.Atan2(_targetLookDir.X, _targetLookDir.Z));
            float targetPitch = Mathf.RadToDeg(Mathf.Asin(_targetLookDir.Y)) * 0.3f;

            _limbs.Head.RotationDegrees = new Vector3(
                Mathf.Lerp(currentRot.X, targetPitch, dt * 0.8f),
                Mathf.Lerp(currentRot.Y, targetYaw, dt * 0.8f),
                currentRot.Z
            );
        }
    }
}
