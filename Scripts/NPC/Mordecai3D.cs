using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Enemies;
using SafeRoom3D.Items;
using SafeRoom3D.UI;
using System.Collections.Generic;

namespace SafeRoom3D.NPC;

/// <summary>
/// Mordecai the Game Guide - A Splinter-style rat humanoid sage who gives quests.
/// He knows about monsters on the current floor and gives collection quests.
/// </summary>
public partial class Mordecai3D : BaseNPC3D
{
    public override string NPCName => "Mordecai";
    public override string InteractionPrompt => "Press T to Talk";
    public override bool IsShopkeeper => false;

    // Quest giver flag for group detection
    public bool IsQuestGiver => true;

    // Idle animation state
    private float _breathTimer;
    private float _tailSwayTimer;
    private float _lookTimer;
    private Vector3 _targetLookDir = Vector3.Forward;

    public override void _Ready()
    {
        base._Ready();
        AddToGroup("QuestGivers");
        GD.Print("[Mordecai3D] Mordecai the Game Guide spawned");
    }

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;

        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "mordecai");

        GD.Print("[Mordecai3D] Created Mordecai mesh");
    }

    protected override float GetNameplateHeight() => 1.5f; // Taller character

    protected override float GetInteractionRange() => 4.0f; // Slightly larger range

    public override void Interact(Node3D player)
    {
        GD.Print("[Mordecai3D] Player interacting with Mordecai");

        // Open the quest UI
        QuestUI3D.Instance?.Open(this);
    }

    protected override void AnimateIdle(double delta)
    {
        if (_limbs == null) return;

        float dt = (float)delta;
        _breathTimer += dt;
        _tailSwayTimer += dt * 0.8f;
        _lookTimer += dt;

        // Gentle breathing (body rises/falls)
        if (_limbs.Body != null)
        {
            float breathAmount = Mathf.Sin(_breathTimer * 1.5f) * 0.005f;
            _limbs.Body.Position = new Vector3(
                _limbs.Body.Position.X,
                0.65f + breathAmount, // Base body Y + breath offset
                _limbs.Body.Position.Z
            );
        }

        // Slow tail sway (wise, contemplative)
        if (_limbs.Tail != null)
        {
            float tailSwayX = Mathf.Sin(_tailSwayTimer) * 3f;
            float tailSwayZ = Mathf.Cos(_tailSwayTimer * 0.7f) * 2f;
            _limbs.Tail.RotationDegrees = new Vector3(tailSwayZ, tailSwayX, 0);
        }

        // Occasional head turns (looking around thoughtfully)
        if (_limbs.Head != null)
        {
            // Change look direction every 4-6 seconds
            if (_lookTimer > 5f)
            {
                _lookTimer = 0;
                _targetLookDir = new Vector3(
                    (float)GD.RandRange(-0.3f, 0.3f),
                    (float)GD.RandRange(-0.1f, 0.1f),
                    1f
                ).Normalized();
            }

            // Smoothly rotate head toward target
            var currentRot = _limbs.Head.RotationDegrees;
            float targetYaw = Mathf.RadToDeg(Mathf.Atan2(_targetLookDir.X, _targetLookDir.Z));
            float targetPitch = -5f + Mathf.RadToDeg(Mathf.Asin(_targetLookDir.Y)) * 0.3f;

            _limbs.Head.RotationDegrees = new Vector3(
                Mathf.Lerp(currentRot.X, targetPitch, dt * 0.5f),
                Mathf.Lerp(currentRot.Y, targetYaw, dt * 0.5f),
                currentRot.Z
            );
        }

        // Subtle arm movement (adjusting grip on staff)
        if (_limbs.RightArm != null)
        {
            float armSway = Mathf.Sin(_breathTimer * 0.8f) * 1f;
            _limbs.RightArm.RotationDegrees = new Vector3(15 + armSway, 0, 25);
        }

        // Left arm occasionally adjusts robe
        if (_limbs.LeftArm != null)
        {
            float leftArmSway = Mathf.Sin(_breathTimer * 0.5f + 1f) * 2f;
            _limbs.LeftArm.RotationDegrees = new Vector3(25 + leftArmSway, 0, -15);
        }
    }

    /// <summary>
    /// Get available quests for the player based on current floor monsters
    /// </summary>
    public List<Quest> GetAvailableQuests()
    {
        return QuestManager.Instance?.GetAvailableQuestsForNPC(this) ?? new List<Quest>();
    }

    /// <summary>
    /// Get the player's active quests from this NPC
    /// </summary>
    public List<Quest> GetActiveQuests()
    {
        return QuestManager.Instance?.GetActiveQuestsFromNPC(this) ?? new List<Quest>();
    }

    /// <summary>
    /// Get completed quests ready to turn in
    /// </summary>
    public List<Quest> GetCompletedQuests()
    {
        return QuestManager.Instance?.GetCompletedQuestsForNPC(this) ?? new List<Quest>();
    }

    /// <summary>
    /// Accept a quest from Mordecai
    /// </summary>
    public void AcceptQuest(Quest quest)
    {
        QuestManager.Instance?.AcceptQuest(quest);
        GD.Print($"[Mordecai3D] Player accepted quest: {quest.Title}");
    }

    /// <summary>
    /// Turn in a completed quest
    /// </summary>
    public void TurnInQuest(Quest quest)
    {
        QuestManager.Instance?.CompleteQuest(quest);
        GD.Print($"[Mordecai3D] Player turned in quest: {quest.Title}");
    }
}
