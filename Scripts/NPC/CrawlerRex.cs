using Godot;
using SafeRoom3D.Enemies;
using SafeRoom3D.NPC.AI;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC;

/// <summary>
/// Rex "Ironside" Martinez - Grizzled veteran crawler with combat tips and war stories.
/// Muscular build, battle-scarred, military vest. Cynical but helpful.
///
/// AI Personality: Tanky veteran who engages multiple enemies, low flee threshold.
/// </summary>
public partial class CrawlerRex : CrawlerAIBase
{
    // === Identity ===
    public override string CrawlerName => "Rex";
    public override CrawlerPersonality Personality => CrawlerPersonality.Rex;

    // === Animation Timers ===
    private float _breathTimer;
    private float _shoulderRollTimer;
    private float _lookTimer;
    private Vector3 _targetLookDir = Vector3.Forward;

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;
        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "crawler_rex");
        GD.Print("[CrawlerRex] Created Rex mesh");
    }

    protected override float GetNameplateHeight() => 2.1f;
    protected override float GetInteractionRange() => 4.0f;
    protected override string GetDialogueKey() => "crawler_rex";

    public override void Interact(Node3D player)
    {
        GD.Print("[CrawlerRex] Player interacting with Rex");
        CrawlerDialogueUI3D.Instance?.Open(this, "crawler_rex");
    }

    protected override void AnimateIdle(float dt)
    {
        if (_limbs == null) return;

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

    protected override void AnimateByState(float dt)
    {
        // Different animations based on current AI state
        switch (CurrentState)
        {
            case AIState.Combat:
                AnimateCombat(dt);
                break;
            case AIState.Fleeing:
            case AIState.ReturningToSafeZone:
                AnimateMoving(dt);
                break;
            default:
                AnimateIdle(dt);
                break;
        }
    }

    private void AnimateCombat(float dt)
    {
        if (_limbs == null) return;

        _breathTimer += dt * 1.5f;  // Faster breathing in combat

        // Combat stance
        if (_limbs.Body != null)
        {
            float breathAmount = Mathf.Sin(_breathTimer * 2f) * 0.005f;
            _limbs.Body.Position = new Vector3(
                _limbs.Body.Position.X,
                0.6f + breathAmount,  // Slightly lower stance
                _limbs.Body.Position.Z
            );
        }

        // Arms ready for attack
        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float combatSway = Mathf.Sin(_breathTimer * 3f) * 5f;
            _limbs.LeftArm.RotationDegrees = new Vector3(-30, 0, 10 + combatSway);
            _limbs.RightArm.RotationDegrees = new Vector3(-30, 0, -10 - combatSway);
        }
    }

    private void AnimateMoving(float dt)
    {
        if (_limbs == null) return;

        _breathTimer += dt * 2f;  // Even faster when running

        // Bob while moving
        if (_limbs.Body != null)
        {
            float bobAmount = Mathf.Sin(_breathTimer * 6f) * 0.02f;
            _limbs.Body.Position = new Vector3(
                _limbs.Body.Position.X,
                0.65f + bobAmount,
                _limbs.Body.Position.Z
            );
        }

        // Arm swing while moving
        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float swing = Mathf.Sin(_breathTimer * 6f) * 15f;
            _limbs.LeftArm.RotationDegrees = new Vector3(swing, 0, 15);
            _limbs.RightArm.RotationDegrees = new Vector3(-swing, 0, -15);
        }

        // Leg movement
        if (_limbs.LeftLeg != null && _limbs.RightLeg != null)
        {
            float legSwing = Mathf.Sin(_breathTimer * 6f) * 20f;
            _limbs.LeftLeg.RotationDegrees = new Vector3(legSwing, 0, 0);
            _limbs.RightLeg.RotationDegrees = new Vector3(-legSwing, 0, 0);
        }
    }
}
