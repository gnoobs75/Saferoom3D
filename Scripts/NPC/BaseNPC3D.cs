using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.NPC;

/// <summary>
/// Abstract base class for all NPCs (non-player characters) in the game.
/// NPCs are non-combatant entities that players can interact with.
/// </summary>
public abstract partial class BaseNPC3D : Node3D
{
    /// <summary>Display name shown above NPC</summary>
    public abstract string NPCName { get; }

    /// <summary>Prompt shown when player is close enough to interact</summary>
    public abstract string InteractionPrompt { get; }

    /// <summary>If true, NPC will be added to "Shopkeepers" group for T-key detection</summary>
    public virtual bool IsShopkeeper => false;

    /// <summary>Root node for mesh instances</summary>
    protected Node3D? _meshRoot;

    /// <summary>Animation limb references</summary>
    protected MonsterMeshFactory.LimbNodes? _limbs;

    /// <summary>Nameplate label above NPC</summary>
    protected Label3D? _nameplate;

    /// <summary>Interaction prompt label</summary>
    protected Label3D? _promptLabel;

    /// <summary>Time accumulator for idle animation</summary>
    protected double _idleTime;

    /// <summary>Whether player is in interaction range</summary>
    protected bool _playerInRange;

    /// <summary>GLB model instance if using custom model</summary>
    protected Node3D? _glbModelInstance;

    /// <summary>AnimationPlayer from GLB model</summary>
    protected AnimationPlayer? _glbAnimPlayer;

    public override void _Ready()
    {
        // Add to groups for detection
        AddToGroup("NPCs");
        if (IsShopkeeper)
        {
            AddToGroup("Shopkeepers");
        }

        // Create mesh root
        _meshRoot = new Node3D();
        _meshRoot.Name = "MeshRoot";
        AddChild(_meshRoot);

        // Create the NPC mesh
        CreateMesh();

        // Create collision body for raycast detection (used in editor for deletion)
        CreateCollisionBody();

        // Create nameplate
        CreateNameplate();

        // Create interaction prompt (hidden by default)
        CreatePromptLabel();
    }

    /// <summary>Create collision body for NPC (editor selection/deletion)</summary>
    protected virtual void CreateCollisionBody()
    {
        var staticBody = new StaticBody3D();
        staticBody.Name = "CollisionBody";
        staticBody.CollisionLayer = 2; // Layer 2 (Enemy layer) for detection
        staticBody.CollisionMask = 0; // NPCs don't detect collisions

        var shape = new CollisionShape3D();
        var capsule = new CapsuleShape3D();
        capsule.Radius = 0.3f;
        capsule.Height = 1.0f;
        shape.Shape = capsule;
        shape.Position = new Vector3(0, 0.5f, 0);

        staticBody.AddChild(shape);
        AddChild(staticBody);
    }

    public override void _Process(double delta)
    {
        _idleTime += delta;

        // Run idle animation
        AnimateIdle(delta);

        // Check player distance for prompt
        UpdatePromptVisibility();

        // Make nameplate face camera
        UpdateNameplateOrientation();
    }

    /// <summary>Create the procedural mesh for this NPC</summary>
    protected abstract void CreateMesh();

    /// <summary>Called when player interacts with this NPC (T key)</summary>
    public abstract void Interact(Node3D player);

    /// <summary>Override for custom idle animations</summary>
    protected virtual void AnimateIdle(double delta)
    {
        // If GLB has idle animation, use it
        if (_glbAnimPlayer != null)
        {
            PlayGlbAnimation("idle");
        }
    }

    /// <summary>
    /// Try to load GLB model for this NPC. Call from CreateMesh() in subclasses.
    /// Returns true if GLB was loaded, false to use procedural mesh.
    /// </summary>
    protected bool TryLoadGlbModel(string npcType, float scale = 1f)
    {
        if (!GlbModelConfig.TryGetNpcGlbPath(npcType, out string? glbPath) ||
            string.IsNullOrWhiteSpace(glbPath))
            return false;

        var glbModel = GlbModelConfig.LoadGlbModel(glbPath, scale);
        if (glbModel == null) return false;

        _glbModelInstance = glbModel;
        _meshRoot?.AddChild(glbModel);
        _glbAnimPlayer = GlbModelConfig.FindAnimationPlayer(glbModel);
        _limbs = null; // GLB models don't have procedural limbs

        GD.Print($"[{npcType}] Loaded GLB, AnimPlayer: {_glbAnimPlayer != null}");
        return true;
    }

    /// <summary>
    /// Play animation from GLB model's embedded AnimationPlayer.
    /// </summary>
    protected void PlayGlbAnimation(string animName)
    {
        if (_glbAnimPlayer?.HasAnimation(animName) == true)
        {
            if (_glbAnimPlayer.CurrentAnimation != animName)
            {
                _glbAnimPlayer.Play(animName);
            }
        }
    }

    /// <summary>Create floating nameplate above NPC</summary>
    protected virtual void CreateNameplate()
    {
        _nameplate = new Label3D();
        _nameplate.Text = NPCName;
        _nameplate.FontSize = 32;
        _nameplate.OutlineSize = 4;
        _nameplate.Modulate = new Color(0.2f, 0.9f, 0.3f); // Green for friendly NPCs
        _nameplate.Position = new Vector3(0, GetNameplateHeight(), 0);
        _nameplate.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _nameplate.NoDepthTest = true;
        AddChild(_nameplate);
    }

    /// <summary>Create interaction prompt label</summary>
    protected virtual void CreatePromptLabel()
    {
        _promptLabel = new Label3D();
        _promptLabel.Text = InteractionPrompt;
        _promptLabel.FontSize = 24;
        _promptLabel.OutlineSize = 3;
        _promptLabel.Modulate = new Color(1f, 1f, 0.6f); // Yellow for prompts
        _promptLabel.Position = new Vector3(0, GetNameplateHeight() - 0.25f, 0);
        _promptLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _promptLabel.NoDepthTest = true;
        _promptLabel.Visible = false;
        AddChild(_promptLabel);
    }

    /// <summary>Height for nameplate above NPC origin</summary>
    protected virtual float GetNameplateHeight() => 1.2f;

    /// <summary>Range at which interaction prompt appears</summary>
    protected virtual float GetInteractionRange() => 3.5f;

    /// <summary>Update visibility of interaction prompt based on player distance</summary>
    protected virtual void UpdatePromptVisibility()
    {
        if (_promptLabel == null) return;

        var player = SafeRoom3D.Player.FPSController.Instance;
        if (player == null)
        {
            _promptLabel.Visible = false;
            _playerInRange = false;
            return;
        }

        float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
        _playerInRange = distance <= GetInteractionRange();
        _promptLabel.Visible = _playerInRange;
    }

    /// <summary>Keep nameplate facing camera</summary>
    protected virtual void UpdateNameplateOrientation()
    {
        // Billboard mode handles this automatically
    }

    /// <summary>Get the LimbNodes for animation</summary>
    public MonsterMeshFactory.LimbNodes? GetLimbs() => _limbs;
}
