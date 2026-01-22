using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.Combat;
using SafeRoom3D.UI;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Goblin Shaman - A spellcasting goblin that buffs nearby allies with Enrage
/// and attacks the player with magical projectiles.
/// </summary>
public partial class GoblinShaman : CharacterBody3D
{
    public string MonsterId { get; set; } = "goblin_shaman";
    public string MonsterType { get; set; } = "GoblinShaman";

    // Stats
    public int Level { get; set; } = 1;
    public int MaxHealth { get; private set; } = 80;
    public int CurrentHealth { get; private set; } = 80;
    public float MoveSpeed { get; private set; } = 2.5f;
    public float Damage { get; private set; } = 15f;
    public float SpellCooldown { get; private set; } = 2.5f;
    public float BuffCooldown { get; private set; } = 8f;
    public float AggroRange { get; private set; } = 18f;
    public float AttackRange { get; private set; } = 12f; // Ranged attack
    public float BuffRange { get; private set; } = 10f;
    public int ExperienceReward { get; private set; } = 12; // XP awarded on death (higher for shaman)

    // State machine
    public enum State { Idle, Casting, Buffing, Fleeing, Dead, Stasis }
    public State CurrentState { get; private set; } = State.Idle;

    // Stasis mode (for In-Map Editor - enemy plays idle animation but doesn't react)
    private bool _isInStasis;

    // Components
    private MeshInstance3D? _meshInstance;
    private CollisionShape3D? _collider;
    private Node3D? _player;
    private Node3D? _staffNode;
    private OmniLight3D? _staffGlow;

    // Timers
    private float _spellTimer;
    private float _buffTimer;
    private float _stateTimer;
    private float _castTimer;

    // Visual
    private StandardMaterial3D? _material;
    private StandardMaterial3D? _robeMaterial;
    private float _hitFlashTimer;
    private float _staffPulseTimer;
    private bool _isCasting;

    // Hit shake effect
    private float _hitShakeTimer;
    private float _hitShakeIntensity;
    private Vector3 _hitShakeOffset;
    private const float HitShakeDuration = 0.25f;
    private const float HitShakeBaseIntensity = 0.08f;

    // Health bar
    private Node3D? _healthBarContainer;
    private MeshInstance3D? _healthBarFill;
    private BoxMesh? _healthBarFillMesh;
    private StandardMaterial3D? _healthBarFillMat;
    private Label3D? _nameLabel;
    private float _healthBarVisibleTimer;

    // AnimationPlayer system
    private AnimationPlayer? _animPlayer;
    private MonsterMeshFactory.LimbNodes? _limbNodes;
    private string _currentAnimName = "";
    private AnimationType _currentAnimType = AnimationType.Idle;

    // Signals
    [Signal] public delegate void DiedEventHandler(GoblinShaman enemy);
    [Signal] public delegate void DamagedEventHandler(int damage, int remaining);

    public static GoblinShaman Create(Vector3 position)
    {
        var shaman = new GoblinShaman();
        shaman.GlobalPosition = position;
        return shaman;
    }

    public override void _Ready()
    {
        SetupComponents();
        CurrentHealth = MaxHealth;

        CallDeferred(MethodName.FindPlayer);
        ChangeState(State.Idle);

        AddToGroup("Enemies");
        AddToGroup("Goblins");

        GD.Print($"[GoblinShaman] Spawned at {GlobalPosition}");
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("Player") as Node3D;
        if (_player == null)
            _player = FPSController.Instance;
    }

    /// <summary>
    /// Check if there's a clear line of sight to the player (no walls blocking).
    /// </summary>
    private bool HasLineOfSightToPlayer()
    {
        if (_player == null) return false;

        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return false;

        Vector3 fromPos = GlobalPosition + new Vector3(0, 1.2f, 0);
        Vector3 toPos = _player.GlobalPosition + new Vector3(0, 1.0f, 0);

        var query = PhysicsRayQueryParameters3D.Create(fromPos, toPos);
        query.CollisionMask = 128 | 16; // Wall + Obstacle layers
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);
        return result.Count == 0;
    }

    private void SetupComponents()
    {
        _meshInstance = new MeshInstance3D();
        _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        AddChild(_meshInstance);

        // Use factory for mesh creation with LOD and animation support
        Color skinColor = new Color(0.35f, 0.45f, 0.28f); // Darker olive green
        _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance, "goblin_shaman", skinColor);

        // Set up AnimationPlayer
        SetupAnimationPlayer();

        // Keep reference to staff for glowing effects
        _staffNode = _meshInstance.FindChild("Staff", true, false) as Node3D;

        // Collision
        _collider = new CollisionShape3D();
        var shape = new CapsuleShape3D();
        shape.Radius = 0.35f;
        shape.Height = 1.0f;
        _collider.Shape = shape;
        _collider.Position = new Vector3(0, 0.5f, 0);
        AddChild(_collider);

        CollisionLayer = 2; // Enemy layer
        CollisionMask = 1 | 16 | 128; // Player, Obstacle, Wall (layer 8 = bit 7 = 128)

        CreateHealthBar();
    }

    private void SetupAnimationPlayer()
    {
        if (_limbNodes == null || _meshInstance == null) return;

        try
        {
            _animPlayer = MonsterAnimationSystem.CreateAnimationPlayer(
                _meshInstance,
                "goblin_shaman",
                _limbNodes
            );

            _currentAnimType = AnimationType.Idle;
            _currentAnimName = MonsterAnimationSystem.GetAnimationName(AnimationType.Idle, 0);

            if (_animPlayer.HasAnimation(_currentAnimName))
            {
                _animPlayer.Play(_currentAnimName);
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[GoblinShaman] Failed to create AnimationPlayer: {e.Message}");
            _animPlayer = null;
        }
    }

    private void PlayAnimation(AnimationType animType, int variant = -1)
    {
        if (_animPlayer == null) return;

        if (variant < 0) variant = GD.RandRange(0, 2);

        _currentAnimType = animType;
        _currentAnimName = MonsterAnimationSystem.GetAnimationName(animType, variant);

        if (_animPlayer.HasAnimation(_currentAnimName))
        {
            if (animType == AnimationType.Attack || animType == AnimationType.Hit || animType == AnimationType.Die)
            {
                _animPlayer.Stop();
                _animPlayer.Play(_currentAnimName);
            }
            else if (_animPlayer.CurrentAnimation != _currentAnimName)
            {
                _animPlayer.Play(_currentAnimName);
            }
        }
    }

    private void CreateShamanMesh()
    {
        // Shaman colors - darker skin, purple/mystical robes
        Color skinColor = new Color(0.35f, 0.45f, 0.28f); // Darker olive green
        Color wartColor = skinColor.Darkened(0.3f); // Warty bumps
        Color robeColor = new Color(0.4f, 0.25f, 0.5f); // Purple robes
        Color accentColor = new Color(0.6f, 0.4f, 0.8f); // Light purple accents
        Color staffColor = new Color(0.5f, 0.4f, 0.3f); // Wood
        Color orbColor = new Color(0.5f, 0.8f, 1f); // Magical blue orb
        Color boneColor = new Color(0.85f, 0.82f, 0.7f); // Bone accessories

        _material = new StandardMaterial3D();
        _material.AlbedoColor = skinColor;
        _material.Roughness = 0.8f;

        var wartMaterial = new StandardMaterial3D();
        wartMaterial.AlbedoColor = wartColor;
        wartMaterial.Roughness = 0.9f;

        _robeMaterial = new StandardMaterial3D();
        _robeMaterial.AlbedoColor = robeColor;
        _robeMaterial.Roughness = 0.9f;

        var staffMaterial = new StandardMaterial3D();
        staffMaterial.AlbedoColor = staffColor;
        staffMaterial.Roughness = 0.7f;

        var orbMaterial = new StandardMaterial3D();
        orbMaterial.AlbedoColor = orbColor;
        orbMaterial.EmissionEnabled = true;
        orbMaterial.Emission = orbColor;
        orbMaterial.EmissionEnergyMultiplier = 2f;
        orbMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

        var boneMaterial = new StandardMaterial3D();
        boneMaterial.AlbedoColor = boneColor;
        boneMaterial.Roughness = 0.6f;

        // === BODY (robed torso) ===
        var body = new MeshInstance3D();
        var bodyMesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.28f, Height = 0.5f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = _robeMaterial;
        body.Position = new Vector3(0, 0.5f, 0);
        _meshInstance!.AddChild(body);

        // Robe bottom (flowing)
        var robeBottom = new MeshInstance3D();
        var robeMesh = new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.35f, Height = 0.3f };
        robeBottom.Mesh = robeMesh;
        robeBottom.MaterialOverride = _robeMaterial;
        robeBottom.Position = new Vector3(0, 0.15f, 0);
        _meshInstance.AddChild(robeBottom);

        // === HEAD ===
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.2f, Height = 0.4f };
        head.Mesh = headMesh;
        head.MaterialOverride = _material;
        head.Position = new Vector3(0, 0.9f, 0);
        _meshInstance.AddChild(head);

        // Hood (cone over head)
        var hood = new MeshInstance3D();
        var hoodMesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.25f, Height = 0.35f };
        hood.Mesh = hoodMesh;
        hood.MaterialOverride = _robeMaterial;
        hood.Position = new Vector3(0, 1.0f, -0.05f);
        hood.RotationDegrees = new Vector3(15, 0, 0);
        _meshInstance.AddChild(hood);

        // === EARS (large pointed with notches) ===
        CreateShamanEar(skinColor, wartColor, true);
        CreateShamanEar(skinColor, wartColor, false);

        // Warts on face and head for aged appearance
        float[] wartPositions = { -0.12f, 0.08f, 0.15f, -0.05f };
        foreach (float wartX in wartPositions)
        {
            var wart = new MeshInstance3D();
            var wartMesh = new SphereMesh { Radius = 0.015f + (float)(GD.Randf() * 0.01f), Height = 0.03f };
            wart.Mesh = wartMesh;
            wart.MaterialOverride = wartMaterial;
            float wartY = 0.85f + (float)(GD.Randf() * 0.15f);
            float wartZ = 0.12f + (float)(GD.Randf() * 0.05f);
            wart.Position = new Vector3(wartX, wartY, wartZ);
            _meshInstance.AddChild(wart);
        }

        // === EYES (glowing) ===
        var eyeMaterial = new StandardMaterial3D();
        eyeMaterial.AlbedoColor = new Color(0.7f, 0.9f, 1f);
        eyeMaterial.EmissionEnabled = true;
        eyeMaterial.Emission = new Color(0.5f, 0.7f, 1f);
        eyeMaterial.EmissionEnergyMultiplier = 1.5f;

        var leftEye = new MeshInstance3D();
        var eyeMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        leftEye.Mesh = eyeMesh;
        leftEye.MaterialOverride = eyeMaterial;
        leftEye.Position = new Vector3(-0.07f, 0.92f, 0.15f);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D();
        rightEye.Mesh = eyeMesh;
        rightEye.MaterialOverride = eyeMaterial;
        rightEye.Position = new Vector3(0.07f, 0.92f, 0.15f);
        _meshInstance.AddChild(rightEye);

        // === NOSE ===
        var nose = new MeshInstance3D();
        var noseMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.04f, Height = 0.1f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = _material;
        nose.Position = new Vector3(0, 0.88f, 0.18f);
        nose.RotationDegrees = new Vector3(-70, 0, 0);
        _meshInstance.AddChild(nose);

        // === STAFF (detailed with crystal orb) ===
        _staffNode = new Node3D();
        _staffNode.Name = "Staff";
        _staffNode.Position = new Vector3(0.35f, 0.3f, 0.1f);
        _staffNode.RotationDegrees = new Vector3(-20, 0, 15);
        _meshInstance.AddChild(_staffNode);

        // Staff shaft with wood grain details
        var shaft = new MeshInstance3D();
        var shaftMesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 1.2f };
        shaft.Mesh = shaftMesh;
        shaft.MaterialOverride = staffMaterial;
        shaft.Position = new Vector3(0, 0.6f, 0);
        _staffNode.AddChild(shaft);

        // Binding wraps on shaft
        for (int i = 0; i < 3; i++)
        {
            var wrap = new MeshInstance3D();
            var wrapMesh = new TorusMesh { InnerRadius = 0.024f, OuterRadius = 0.032f };
            wrap.Mesh = wrapMesh;
            wrap.MaterialOverride = _robeMaterial;
            wrap.Position = new Vector3(0, 0.2f + i * 0.3f, 0);
            wrap.RotationDegrees = new Vector3(90, 0, 0);
            wrap.Scale = new Vector3(1f, 1f, 0.5f);
            _staffNode.AddChild(wrap);
        }

        // Gnarled wood branches at top
        for (int i = 0; i < 4; i++)
        {
            var branch = new MeshInstance3D();
            var branchMesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.015f, Height = 0.25f };
            branch.Mesh = branchMesh;
            branch.MaterialOverride = staffMaterial;
            float angle = i * Mathf.Pi / 2;
            branch.Position = new Vector3(Mathf.Cos(angle) * 0.06f, 1.15f, Mathf.Sin(angle) * 0.06f);
            branch.RotationDegrees = new Vector3(30, angle * 57.3f, 0);
            _staffNode.AddChild(branch);
        }

        // Magic crystal orb (multi-faceted)
        var orb = new MeshInstance3D();
        var orbMesh = new SphereMesh { Radius = 0.1f, Height = 0.2f };
        orb.Mesh = orbMesh;
        orb.MaterialOverride = orbMaterial;
        orb.Position = new Vector3(0, 1.25f, 0);
        orb.Name = "MagicOrb";
        _staffNode.AddChild(orb);

        // Inner crystal core (brighter)
        var core = new MeshInstance3D();
        var coreMesh = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        core.Mesh = coreMesh;
        var coreMat = new StandardMaterial3D();
        coreMat.AlbedoColor = Colors.White;
        coreMat.EmissionEnabled = true;
        coreMat.Emission = Colors.White;
        coreMat.EmissionEnergyMultiplier = 4f;
        coreMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        core.MaterialOverride = coreMat;
        core.Position = new Vector3(0, 1.25f, 0);
        _staffNode.AddChild(core);

        // Staff glow light
        _staffGlow = new OmniLight3D();
        _staffGlow.LightColor = orbColor;
        _staffGlow.LightEnergy = 1.5f;
        _staffGlow.OmniRange = 4f;
        _staffGlow.Position = new Vector3(0, 1.25f, 0);
        _staffNode.AddChild(_staffGlow);

        // === ARMS (simple, mostly hidden by robe) ===
        CreateShamanArm(_material, true);
        CreateShamanArm(_material, false);

        // Bone amulet necklace
        var amuletChain = new MeshInstance3D();
        var chainMesh = new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.18f };
        amuletChain.Mesh = chainMesh;
        amuletChain.MaterialOverride = boneMaterial;
        amuletChain.Position = new Vector3(0, 0.75f, 0.02f);
        amuletChain.RotationDegrees = new Vector3(90, 0, 0);
        amuletChain.Scale = new Vector3(1f, 1f, 0.15f);
        _meshInstance.AddChild(amuletChain);

        // Skull pendant on necklace
        var skull = new MeshInstance3D();
        var skullMesh = new SphereMesh { Radius = 0.04f, Height = 0.06f };
        skull.Mesh = skullMesh;
        skull.MaterialOverride = boneMaterial;
        skull.Position = new Vector3(0, 0.62f, 0.15f);
        skull.Scale = new Vector3(0.8f, 1f, 0.7f);
        _meshInstance.AddChild(skull);

        // Eye sockets on skull
        var socketMat = new StandardMaterial3D { AlbedoColor = Colors.Black };
        for (int i = 0; i < 2; i++)
        {
            var socket = new MeshInstance3D();
            var socketMesh = new SphereMesh { Radius = 0.008f, Height = 0.016f };
            socket.Mesh = socketMesh;
            socket.MaterialOverride = socketMat;
            socket.Position = new Vector3((i == 0 ? -0.015f : 0.015f), 0.63f, 0.17f);
            _meshInstance.AddChild(socket);
        }

        // Robe trim/decorations with glowing runes
        var trimMaterial = new StandardMaterial3D();
        trimMaterial.AlbedoColor = accentColor;
        trimMaterial.EmissionEnabled = true;
        trimMaterial.Emission = accentColor * 0.3f;

        // Mystical rune symbols on robe (glowing)
        float[] runeAngles = { 0, 1.5f, 3f, 4.5f };
        foreach (float angle in runeAngles)
        {
            var rune = new MeshInstance3D();
            var runeMesh = new TorusMesh { InnerRadius = 0.015f, OuterRadius = 0.025f };
            rune.Mesh = runeMesh;
            rune.MaterialOverride = trimMaterial;
            rune.Position = new Vector3(Mathf.Cos(angle) * 0.25f, 0.35f, Mathf.Sin(angle) * 0.25f);
            rune.RotationDegrees = new Vector3(90, angle * 57.3f, 0);
            rune.Scale = new Vector3(1f, 1f, 0.3f);
            _meshInstance.AddChild(rune);
        }

        // Tribal face paint markings (dark lines)
        var paintMat = new StandardMaterial3D();
        paintMat.AlbedoColor = new Color(0.2f, 0.1f, 0.25f);
        paintMat.EmissionEnabled = true;
        paintMat.Emission = accentColor * 0.5f;

        // Vertical lines on forehead
        for (int i = 0; i < 3; i++)
        {
            var mark = new MeshInstance3D();
            var markMesh = new BoxMesh { Size = new Vector3(0.01f, 0.08f, 0.01f) };
            mark.Mesh = markMesh;
            mark.MaterialOverride = paintMat;
            mark.Position = new Vector3(-0.03f + i * 0.03f, 0.95f, 0.18f);
            _meshInstance.AddChild(mark);
        }
    }

    private void CreateShamanEar(Color skinColor, Color wartColor, bool isLeft)
    {
        var earMat = new StandardMaterial3D { AlbedoColor = skinColor, Roughness = 0.8f };
        var piercingMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.5f, 0.3f), Metallic = 0.4f, Roughness = 0.5f };
        float side = isLeft ? -1f : 1f;

        var ear = new MeshInstance3D();
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.05f, Height = 0.15f };
        ear.Mesh = earMesh;
        ear.MaterialOverride = earMat;
        ear.Position = new Vector3(side * 0.2f, 0.92f, 0);
        ear.RotationDegrees = new Vector3(0, 0, side * 55);
        _meshInstance!.AddChild(ear);

        // Ear notch (battle damage)
        var notch = new MeshInstance3D();
        var notchMesh = new BoxMesh { Size = new Vector3(0.03f, 0.02f, 0.015f) };
        notch.Mesh = notchMesh;
        notch.MaterialOverride = new StandardMaterial3D { AlbedoColor = wartColor };
        notch.Position = new Vector3(side * 0.22f, 1.0f, 0.01f);
        notch.RotationDegrees = new Vector3(0, 0, side * 55);
        _meshInstance.AddChild(notch);

        // Bone piercing
        var piercing = new MeshInstance3D();
        var piercingMesh = new TorusMesh { InnerRadius = 0.015f, OuterRadius = 0.02f };
        piercing.Mesh = piercingMesh;
        piercing.MaterialOverride = piercingMat;
        piercing.Position = new Vector3(side * 0.18f, 0.88f, 0.02f);
        piercing.RotationDegrees = new Vector3(0, 90, side * 55);
        piercing.Scale = new Vector3(1f, 1f, 0.4f);
        _meshInstance.AddChild(piercing);
    }

    private void CreateShamanArm(StandardMaterial3D material, bool isLeft)
    {
        float side = isLeft ? -1f : 1f;
        var arm = new MeshInstance3D();
        var armMesh = new CapsuleMesh { Radius = 0.04f, Height = 0.3f };
        arm.Mesh = armMesh;
        arm.MaterialOverride = _robeMaterial; // Arm covered by sleeve
        arm.Position = new Vector3(side * 0.25f, 0.55f, 0.05f);
        arm.RotationDegrees = new Vector3(isLeft ? -30 : 20, 0, side * 25);
        _meshInstance!.AddChild(arm);

        // Hand with clawed fingers
        var hand = new MeshInstance3D();
        var handMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        hand.Mesh = handMesh;
        hand.MaterialOverride = material;
        hand.Position = new Vector3(side * 0.32f, 0.38f, 0.1f);
        hand.Scale = new Vector3(1f, 0.8f, 1.2f);
        _meshInstance.AddChild(hand);

        // Sharp claws
        var clawMat = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.3f, 0.25f), Roughness = 0.5f };
        for (int f = 0; f < 4; f++)
        {
            var claw = new MeshInstance3D();
            var clawMesh = new CylinderMesh { TopRadius = 0.001f, BottomRadius = 0.008f, Height = 0.035f };
            claw.Mesh = clawMesh;
            claw.MaterialOverride = clawMat;
            float fingerSpread = -0.025f + f * 0.015f;
            claw.Position = new Vector3(side * 0.32f + fingerSpread, 0.36f, 0.14f);
            claw.RotationDegrees = new Vector3(-65, 0, fingerSpread * 30);
            _meshInstance.AddChild(claw);
        }
    }

    private void CreateHealthBar()
    {
        _healthBarContainer = new Node3D();
        _healthBarContainer.Name = "HealthBar";
        _healthBarContainer.Position = new Vector3(0, 2.0f, 0);
        AddChild(_healthBarContainer);

        // Monster name label above health bar
        _nameLabel = new Label3D();
        _nameLabel.Text = "Goblin Shaman";
        _nameLabel.FontSize = 32;
        _nameLabel.OutlineSize = 4;
        _nameLabel.Modulate = new Color(0.8f, 0.6f, 1f); // Purple tint for shaman
        _nameLabel.Position = new Vector3(0, 0.15f, 0);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.VerticalAlignment = VerticalAlignment.Bottom;
        _nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _nameLabel.NoDepthTest = false; // Don't render through walls
        _healthBarContainer.AddChild(_nameLabel);

        // Background - uses material billboard mode for GPU billboarding
        var bg = new MeshInstance3D();
        var bgMesh = new BoxMesh { Size = new Vector3(1.0f, 0.12f, 0.02f) };
        bg.Mesh = bgMesh;
        var bgMat = new StandardMaterial3D();
        bgMat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        bgMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        bgMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled; // PERF: GPU billboarding
        bgMat.NoDepthTest = false;
        bg.MaterialOverride = bgMat;
        bg.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(bg);

        // Fill - uses material billboard mode for GPU billboarding
        _healthBarFill = new MeshInstance3D();
        _healthBarFillMesh = new BoxMesh { Size = new Vector3(0.9f, 0.1f, 0.025f) };
        _healthBarFill.Mesh = _healthBarFillMesh;
        _healthBarFillMat = new StandardMaterial3D();
        _healthBarFillMat.AlbedoColor = new Color(0.6f, 0.3f, 0.9f); // Purple for shaman
        _healthBarFillMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _healthBarFillMat.EmissionEnabled = true;
        _healthBarFillMat.Emission = new Color(0.4f, 0.2f, 0.6f);
        _healthBarFillMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled; // PERF: GPU billboarding
        _healthBarFillMat.NoDepthTest = false;
        _healthBarFill.MaterialOverride = _healthBarFillMat;
        _healthBarFill.Position = new Vector3(0, 0, 0.01f);
        _healthBarFill.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(_healthBarFill);

        // Show based on nameplate toggle setting
        _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (CurrentState == State.Dead) return;

        // Stasis mode - only update visuals, no AI processing
        if (_isInStasis)
        {
            UpdateVisuals(dt);
            if (_healthBarContainer != null)
            {
                _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
            }
            UpdateHealthBar();
            return;
        }

        // Update health bar visibility (respects nameplate toggle)
        if (_healthBarContainer != null)
        {
            _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
        }

        UpdateTimers(dt);
        UpdateBehavior(dt);
        UpdateVisuals(dt);
        UpdateHealthBar();
    }

    private void UpdateTimers(float dt)
    {
        _spellTimer += dt;
        _buffTimer += dt;
        _stateTimer += dt;
        _hitFlashTimer -= dt;
        _staffPulseTimer += dt;

        // Hit shake effect update
        if (_hitShakeTimer > 0 && _meshInstance != null)
        {
            _hitShakeTimer -= dt;
            float shakeProgress = _hitShakeTimer / HitShakeDuration;
            float currentIntensity = _hitShakeIntensity * shakeProgress;

            float shakeFreq = 45f;
            _hitShakeOffset = new Vector3(
                Mathf.Sin(_staffPulseTimer * shakeFreq) * currentIntensity,
                Mathf.Sin(_staffPulseTimer * shakeFreq * 1.3f) * currentIntensity * 0.3f,
                Mathf.Cos(_staffPulseTimer * shakeFreq * 0.9f) * currentIntensity
            );
            _meshInstance.Position = _hitShakeOffset;
        }
        else if (_meshInstance != null && _hitShakeOffset != Vector3.Zero)
        {
            _hitShakeOffset = Vector3.Zero;
            _meshInstance.Position = Vector3.Zero;
        }
    }

    private void UpdateBehavior(float dt)
    {
        if (_player == null) return;

        float distToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
        bool hasLOS = HasLineOfSightToPlayer();

        switch (CurrentState)
        {
            case State.Idle:
                // Only aggro if we have line of sight
                if (distToPlayer < AggroRange && hasLOS)
                {
                    // Check if we should buff nearby goblins
                    if (_buffTimer >= BuffCooldown && HasNearbyGoblins())
                    {
                        ChangeState(State.Buffing);
                    }
                    else if (_spellTimer >= SpellCooldown && distToPlayer <= AttackRange)
                    {
                        ChangeState(State.Casting);
                    }
                    else if (distToPlayer < 5f)
                    {
                        // Too close, flee!
                        ChangeState(State.Fleeing);
                    }
                    // Face the player when in aggro range
                    FaceTarget(_player.GlobalPosition);
                }
                break;

            case State.Casting:
                _castTimer += dt;
                // Face player during casting
                FaceTarget(_player.GlobalPosition);
                // Cancel cast if we lose line of sight
                if (!hasLOS)
                {
                    _castTimer = 0;
                    ChangeState(State.Idle);
                    break;
                }
                if (_castTimer >= 0.8f) // Cast time
                {
                    CastSpell();
                    _spellTimer = 0;
                    _castTimer = 0;
                    ChangeState(State.Idle);
                }
                break;

            case State.Buffing:
                _castTimer += dt;
                // Face player during buffing
                FaceTarget(_player.GlobalPosition);
                if (_castTimer >= 1.2f) // Buff cast time
                {
                    BuffNearbyGoblins();
                    _buffTimer = 0;
                    _castTimer = 0;
                    ChangeState(State.Idle);
                }
                break;

            case State.Fleeing:
                // Run away from player
                Vector3 fleeDir = (GlobalPosition - _player.GlobalPosition).Normalized();
                fleeDir.Y = 0;
                Velocity = fleeDir * MoveSpeed * 1.5f;
                MoveAndSlide();

                // Face movement direction while fleeing
                FaceDirection(fleeDir);

                if (distToPlayer > 8f || _stateTimer > 3f)
                {
                    ChangeState(State.Idle);
                }
                break;
        }
    }

    private bool HasNearbyGoblins()
    {
        var goblins = GetTree().GetNodesInGroup("Goblins");
        foreach (var node in goblins)
        {
            if (node == this) continue;
            if (node is Node3D goblin)
            {
                if (GlobalPosition.DistanceTo(goblin.GlobalPosition) <= BuffRange)
                    return true;
            }
        }
        return false;
    }

    private void CastSpell()
    {
        if (_player == null) return;

        // Don't cast if we don't have line of sight (safety check)
        if (!HasLineOfSightToPlayer()) return;

        // Create magical projectile
        Vector3 spawnPos = GlobalPosition + new Vector3(0, 1.5f, 0);
        Vector3 direction = (_player.GlobalPosition + new Vector3(0, 1f, 0) - spawnPos).Normalized();

        var projectile = Projectile3D.Create(
            spawnPos,
            direction,
            Damage,
            isPlayerProjectile: false,
            color: new Color(0.5f, 0.3f, 0.9f), // Purple magic
            source: "GoblinShaman"
        );
        projectile.Speed = 15f;

        GetTree().Root.AddChild(projectile);
        projectile.Fire(direction);

        // Visual effect on staff
        CreateCastEffect();

        SoundManager3D.Instance?.PlayMagicSound(spawnPos);
        GD.Print("[GoblinShaman] Cast spell!");
    }

    private void BuffNearbyGoblins()
    {
        var goblins = GetTree().GetNodesInGroup("Goblins");
        int buffedCount = 0;

        foreach (var node in goblins)
        {
            if (node == this) continue;
            if (node is Node3D goblin)
            {
                float dist = GlobalPosition.DistanceTo(goblin.GlobalPosition);
                if (dist <= BuffRange)
                {
                    // Apply Enrage buff via method call
                    if (goblin.HasMethod("ApplyEnrage"))
                    {
                        goblin.Call("ApplyEnrage", 10f); // 10 second enrage
                        CreateBuffEffect(goblin.GlobalPosition);
                        buffedCount++;
                    }
                }
            }
        }

        // Self buff visual
        CreateBuffEffect(GlobalPosition);

        GD.Print($"[GoblinShaman] Buffed {buffedCount} goblins with Enrage!");
    }

    private void CreateCastEffect()
    {
        if (_staffGlow != null)
        {
            _staffGlow.LightEnergy = 5f;

            var tween = CreateTween();
            tween.TweenProperty(_staffGlow, "light_energy", 1.5f, 0.5f);
        }

        // Create particles at staff
        var particles = new GpuParticles3D();
        particles.Amount = 20;
        particles.Lifetime = 0.5f;
        particles.OneShot = true;
        particles.Explosiveness = 0.8f;
        particles.GlobalPosition = _staffNode?.GlobalPosition ?? GlobalPosition + new Vector3(0.35f, 1.5f, 0.1f);

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.1f;
        material.Direction = new Vector3(0, 0, -1);
        material.Spread = 30f;
        material.InitialVelocityMin = 5f;
        material.InitialVelocityMax = 10f;
        material.Gravity = Vector3.Zero;
        material.ScaleMin = 0.05f;
        material.ScaleMax = 0.1f;
        material.Color = new Color(0.5f, 0.3f, 0.9f);
        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh { Size = new Vector2(0.1f, 0.1f) };
        var quadMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.4f, 0.9f),
            EmissionEnabled = true,
            Emission = new Color(0.5f, 0.3f, 0.8f),
            EmissionEnergyMultiplier = 2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        quadMesh.Material = quadMat;
        particles.DrawPass1 = quadMesh;

        GetTree().Root.AddChild(particles);
        particles.Emitting = true;

        var timer = GetTree().CreateTimer(1f);
        timer.Timeout += () => particles.QueueFree();
    }

    private void CreateBuffEffect(Vector3 position)
    {
        // Create rising buff particles
        var particles = new GpuParticles3D();
        particles.Amount = 30;
        particles.Lifetime = 1f;
        particles.OneShot = true;
        particles.Explosiveness = 0.5f;
        particles.GlobalPosition = position + new Vector3(0, 0.5f, 0);

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.5f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 20f;
        material.InitialVelocityMin = 2f;
        material.InitialVelocityMax = 4f;
        material.Gravity = new Vector3(0, 1, 0); // Rise upward
        material.ScaleMin = 0.03f;
        material.ScaleMax = 0.08f;
        material.Color = new Color(1f, 0.4f, 0.2f); // Orange/red for rage

        particles.ProcessMaterial = material;

        var quadMesh = new QuadMesh { Size = new Vector2(0.1f, 0.1f) };
        var quadMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.5f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f),
            EmissionEnergyMultiplier = 3f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        quadMesh.Material = quadMat;
        particles.DrawPass1 = quadMesh;

        GetTree().Root.AddChild(particles);
        particles.Emitting = true;

        var timer = GetTree().CreateTimer(1.5f);
        timer.Timeout += () => particles.QueueFree();
    }

    private void UpdateVisuals(float dt)
    {
        // Staff glow pulse
        if (_staffGlow != null && CurrentState != State.Casting)
        {
            float pulse = 1.5f + Mathf.Sin(_staffPulseTimer * 2f) * 0.5f;
            _staffGlow.LightEnergy = pulse;
        }

        // Hit flash
        if (_hitFlashTimer > 0 && _material != null)
        {
            float flash = _hitFlashTimer / 0.2f;
            _material.EmissionEnabled = true;
            _material.Emission = new Color(1f, 0.3f, 0.3f) * flash;
        }
        else if (_material != null)
        {
            _material.EmissionEnabled = false;
        }

        // Casting animation - bob staff
        if (CurrentState == State.Casting || CurrentState == State.Buffing)
        {
            if (_staffNode != null)
            {
                float bob = Mathf.Sin(_castTimer * 10f) * 0.1f;
                _staffNode.Position = new Vector3(0.35f, 0.3f + bob, 0.1f);
            }
        }
    }

    // PERFORMANCE: Health bar uses GPU billboard mode - no camera lookups needed

    private void UpdateHealthBar()
    {
        // PERFORMANCE: Billboard mode is now set on materials directly (GPU-based billboarding)
        // No LookAt() calls needed - this method is kept for compatibility but does nothing
    }

    private void UpdateHealthBarVisuals()
    {
        if (_healthBarFillMesh == null) return;

        float healthPercent = (float)CurrentHealth / MaxHealth;
        healthPercent = Mathf.Clamp(healthPercent, 0f, 1f);

        float fullWidth = 0.9f;
        float currentWidth = fullWidth * healthPercent;
        _healthBarFillMesh.Size = new Vector3(currentWidth, 0.1f, 0.025f);

        if (_healthBarFill != null)
        {
            _healthBarFill.Position = new Vector3((currentWidth - fullWidth) / 2, 0, 0.01f);
        }
    }

    private void ChangeState(State newState)
    {
        CurrentState = newState;
        _stateTimer = 0;
        _castTimer = 0;

        // Play appropriate animation for new state
        switch (newState)
        {
            case State.Idle:
                PlayAnimation(AnimationType.Idle);
                break;
            case State.Casting:
            case State.Buffing:
                PlayAnimation(AnimationType.Attack); // Use attack animation for casting
                break;
            case State.Fleeing:
                PlayAnimation(AnimationType.Run);
                break;
            case State.Dead:
                PlayAnimation(AnimationType.Die);
                break;
        }
    }

    /// <summary>
    /// Face toward a target position using Atan2 (models face +Z by default)
    /// </summary>
    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(direction.X, direction.Z);
            Rotation = new Vector3(0, targetAngle, 0);
        }
    }

    /// <summary>
    /// Face in a movement direction using Atan2
    /// </summary>
    private void FaceDirection(Vector3 direction)
    {
        direction.Y = 0;
        if (direction.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(direction.X, direction.Z);
            Rotation = new Vector3(0, targetAngle, 0);
        }
    }

    /// <summary>
    /// Check if shaman is in stasis (for In-Map Editor).
    /// </summary>
    public bool IsInStasis() => _isInStasis;

    /// <summary>
    /// Enter stasis mode - shaman plays idle animation but doesn't react to player.
    /// Used by In-Map Editor.
    /// </summary>
    public void EnterStasis()
    {
        if (_isInStasis) return;

        _isInStasis = true;
        CurrentState = State.Stasis;
        Velocity = Vector3.Zero;

        // Switch to idle animation
        if (_animPlayer != null)
        {
            PlayAnimation(AnimationType.Idle);
        }

        GD.Print($"[GoblinShaman] Entered stasis");
    }

    /// <summary>
    /// Exit stasis mode - shaman resumes normal AI behavior.
    /// </summary>
    public void ExitStasis()
    {
        if (!_isInStasis) return;

        _isInStasis = false;
        ChangeState(State.Idle);

        GD.Print($"[GoblinShaman] Exited stasis");
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source)
    {
        TakeDamage(damage, fromPosition, source, false, 1f);
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source, bool isCrit)
    {
        TakeDamage(damage, fromPosition, source, isCrit, 1f);
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source, bool isCrit, float pushbackMultiplier)
    {
        if (CurrentState == State.Dead) return;

        int intDamage = (int)damage;
        CurrentHealth -= intDamage;

        _hitFlashTimer = 0.2f;

        // Hit shake effect
        float damageRatio = Mathf.Min(1f, damage / (MaxHealth * 0.25f));
        _hitShakeTimer = HitShakeDuration;
        _hitShakeIntensity = HitShakeBaseIntensity * (0.5f + damageRatio * 0.5f);

        // Play hit animation
        if (CurrentState != State.Dead)
        {
            PlayAnimation(AnimationType.Hit);
        }

        EmitSignal(SignalName.Damaged, intDamage, CurrentHealth);

        // Update health bar visuals
        UpdateHealthBarVisuals();

        SpawnFloatingDamageText(intDamage, isCrit);

        // Flee when hit!
        if (CurrentState != State.Dead && CurrentHealth > 0)
        {
            ChangeState(State.Fleeing);
        }

        if (CurrentHealth <= 0)
        {
            Die();
        }

        GD.Print($"[GoblinShaman] Took {intDamage} damage from {source}. Health: {CurrentHealth}/{MaxHealth}");
    }

    private void SpawnFloatingDamageText(int damage, bool isCrit = false)
    {
        var textContainer = new Node3D();
        textContainer.GlobalPosition = GlobalPosition + new Vector3(0, 2.2f, 0);
        GetTree().Root.AddChild(textContainer);

        var label = new Label3D();
        label.Text = isCrit ? $"!{damage}!" : damage.ToString();
        label.FontSize = isCrit ? 96 : 64;
        label.OutlineSize = isCrit ? 12 : 8;
        label.Modulate = isCrit ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.4f, 1f); // Gold for crit, purple otherwise
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.NoDepthTest = true;
        textContainer.AddChild(label);

        var tween = textContainer.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(textContainer, "position:y", textContainer.Position.Y + 1.5f, 0.8f);
        tween.TweenProperty(label, "modulate:a", 0f, 0.8f).SetDelay(0.3f);
        tween.Chain().TweenCallback(Callable.From(() => textContainer.QueueFree()));
    }

    private void Die()
    {
        ChangeState(State.Dead);
        CollisionLayer = 0;
        CollisionMask = 0;

        EmitSignal(SignalName.Died, this);

        // Play death animation
        PlayAnimation(AnimationType.Die);

        SoundManager3D.Instance?.PlayEnemyDeathSound(GlobalPosition);

        // Award XP to player
        if (FPSController.Instance != null && ExperienceReward > 0)
        {
            FPSController.Instance.AddExperience(ExperienceReward);
            HUD3D.Instance?.LogEnemyDeath("Goblin Shaman", ExperienceReward);

            // Register kill for achievements
            HUD3D.Instance?.RegisterKill("goblin_shaman", false);
        }

        GD.Print("[GoblinShaman] Died!");

        // Wait for death animation to complete before spawning corpse
        float deathAnimDuration = 2.5f;

        var tween = CreateTween();
        tween.TweenInterval(deathAnimDuration);
        tween.TweenCallback(Callable.From(() =>
        {
            // Create corpse (monsterType, isBoss, position, rotation, level)
            var corpse = Corpse3D.Create("goblin_shaman", false, GlobalPosition, Rotation.Y, Level);
            GetTree().Root.AddChild(corpse);
            QueueFree();
        }));
    }

    public override void _ExitTree()
    {
        RemoveFromGroup("Enemies");
        RemoveFromGroup("Goblins");
    }
}
