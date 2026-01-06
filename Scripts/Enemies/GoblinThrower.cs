using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.Combat;
using SafeRoom3D.UI;

namespace SafeRoom3D.Enemies;

/// <summary>
/// Goblin Thrower - A ranged goblin that throws spears, axes, and beer cans at the player.
/// Prefers to stay at range and flees when the player gets too close.
/// </summary>
public partial class GoblinThrower : CharacterBody3D
{
    public string MonsterId { get; set; } = "goblin_thrower";
    public string MonsterType { get; set; } = "GoblinThrower";

    // Stats
    public int Level { get; set; } = 1;
    public int MaxHealth { get; private set; } = 55;
    public int CurrentHealth { get; private set; } = 55;
    public float MoveSpeed { get; private set; } = 3.5f;
    public float Damage { get; private set; } = 12f;
    public float ThrowCooldown { get; private set; } = 1.8f;
    public float AggroRange { get; private set; } = 16f;
    public float PreferredRange { get; private set; } = 10f; // Optimal throwing distance
    public float FleeRange { get; private set; } = 4f; // Start fleeing when player is this close
    public int ExperienceReward { get; private set; } = 8; // XP awarded on death

    // Projectile types - using ThrowableType from ThrownProjectile3D
    private ThrowableType _currentProjectile = ThrowableType.Spear;

    // State machine
    public enum State { Idle, Repositioning, Throwing, Fleeing, Dead, Stasis }
    public State CurrentState { get; private set; } = State.Idle;

    // Stasis mode (for In-Map Editor - enemy plays idle animation but doesn't react)
    private bool _isInStasis;

    // Components
    private MeshInstance3D? _meshInstance;
    private CollisionShape3D? _collider;
    private Node3D? _player;
    private Node3D? _throwingArmNode;
    private Node3D? _heldProjectileNode;

    // Timers
    private float _throwTimer;
    private float _stateTimer;
    private float _throwAnimTimer;

    // Visual
    private StandardMaterial3D? _material;
    private float _hitFlashTimer;
    private bool _isWindingUp;

    // Health bar
    private Node3D? _healthBarContainer;
    private MeshInstance3D? _healthBarFill;
    private BoxMesh? _healthBarFillMesh;
    private StandardMaterial3D? _healthBarFillMat;
    private Label3D? _nameLabel;
    private float _healthBarVisibleTimer;

    // Enrage state
    private bool _isEnraged;
    private float _enrageTimer;
    private Node3D? _enrageIndicator;
    private GpuParticles3D? _enrageParticles;

    // AnimationPlayer system
    private AnimationPlayer? _animPlayer;
    private MonsterMeshFactory.LimbNodes? _limbNodes;
    private string _currentAnimName = "";
    private AnimationType _currentAnimType = AnimationType.Idle;

    // Signals
    [Signal] public delegate void DiedEventHandler(GoblinThrower enemy);
    [Signal] public delegate void DamagedEventHandler(int damage, int remaining);

    public static GoblinThrower Create(Vector3 position)
    {
        var thrower = new GoblinThrower();
        thrower.GlobalPosition = position;
        return thrower;
    }

    public override void _Ready()
    {
        SetupComponents();
        CurrentHealth = MaxHealth;

        // Randomly select starting projectile type
        _currentProjectile = (ThrowableType)((int)(GD.Randf() * 3));

        CallDeferred(MethodName.FindPlayer);
        ChangeState(State.Idle);

        AddToGroup("Enemies");
        AddToGroup("Goblins");

        GD.Print($"[GoblinThrower] Spawned at {GlobalPosition} with {_currentProjectile}");
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
        Color skinColor = new Color(0.5f, 0.55f, 0.32f); // Yellowish-olive green
        _limbNodes = MonsterMeshFactory.CreateMonsterMesh(_meshInstance, "goblin_thrower", skinColor);

        // Set up AnimationPlayer
        SetupAnimationPlayer();

        // Keep reference to throwing arm for animation
        _throwingArmNode = _meshInstance.FindChild("RightArm", true, false) as Node3D;

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
        CreateEnrageIndicator();
    }

    private void SetupAnimationPlayer()
    {
        if (_limbNodes == null || _meshInstance == null) return;

        try
        {
            _animPlayer = MonsterAnimationSystem.CreateAnimationPlayer(
                _meshInstance,
                "goblin_thrower",
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
            GD.PrintErr($"[GoblinThrower] Failed to create AnimationPlayer: {e.Message}");
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

    private void CreateThrowerMesh()
    {
        // Thrower colors - slightly different shade, more aggressive look
        Color skinColor = new Color(0.5f, 0.55f, 0.32f); // Yellowish-olive green
        Color clothColor = new Color(0.5f, 0.35f, 0.25f); // Reddish-brown leather
        Color bandColor = new Color(0.6f, 0.5f, 0.35f); // Tan wraps/bands

        _material = new StandardMaterial3D();
        _material.AlbedoColor = skinColor;
        _material.Roughness = 0.8f;

        var clothMaterial = new StandardMaterial3D();
        clothMaterial.AlbedoColor = clothColor;
        clothMaterial.Roughness = 0.9f;

        var bandMaterial = new StandardMaterial3D();
        bandMaterial.AlbedoColor = bandColor;
        bandMaterial.Roughness = 0.85f;

        // === BODY (lighter armor/vest) ===
        var body = new MeshInstance3D();
        var bodyMesh = new SphereMesh { Radius = 0.22f, Height = 0.44f };
        body.Mesh = bodyMesh;
        body.MaterialOverride = clothMaterial;
        body.Position = new Vector3(0, 0.5f, 0);
        body.Scale = new Vector3(1f, 1.1f, 0.85f);
        _meshInstance!.AddChild(body);

        // Shoulder armor pads
        CreateShoulderPad(bandMaterial, true);
        CreateShoulderPad(bandMaterial, false);

        // === BELLY ===
        var belly = new MeshInstance3D();
        var bellyMesh = new SphereMesh { Radius = 0.18f, Height = 0.36f };
        belly.Mesh = bellyMesh;
        belly.MaterialOverride = _material;
        belly.Position = new Vector3(0, 0.42f, 0.06f);
        belly.Scale = new Vector3(0.85f, 0.75f, 0.7f);
        _meshInstance.AddChild(belly);

        // === HEAD ===
        var head = new MeshInstance3D();
        var headMesh = new SphereMesh { Radius = 0.2f, Height = 0.4f };
        head.Mesh = headMesh;
        head.MaterialOverride = _material;
        head.Position = new Vector3(0, 0.85f, 0);
        head.Scale = new Vector3(1f, 0.9f, 0.95f);
        _meshInstance.AddChild(head);

        // Headband/bandana
        var headband = new MeshInstance3D();
        var headbandMesh = new TorusMesh { InnerRadius = 0.15f, OuterRadius = 0.18f };
        headband.Mesh = headbandMesh;
        headband.MaterialOverride = bandMaterial;
        headband.Position = new Vector3(0, 0.9f, 0);
        headband.RotationDegrees = new Vector3(90, 0, 0);
        headband.Scale = new Vector3(1f, 1f, 0.4f);
        _meshInstance.AddChild(headband);

        // === EARS ===
        CreateThrowerEar(true);
        CreateThrowerEar(false);

        // === EYES (mean-looking) ===
        var eyeMaterial = new StandardMaterial3D();
        eyeMaterial.AlbedoColor = new Color(0.9f, 0.7f, 0.2f);
        eyeMaterial.EmissionEnabled = true;
        eyeMaterial.Emission = new Color(0.8f, 0.6f, 0.1f);
        eyeMaterial.EmissionEnergyMultiplier = 0.5f;

        var leftEye = new MeshInstance3D();
        var eyeMesh = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        leftEye.Mesh = eyeMesh;
        leftEye.MaterialOverride = eyeMaterial;
        leftEye.Position = new Vector3(-0.07f, 0.88f, 0.15f);
        _meshInstance.AddChild(leftEye);

        var rightEye = new MeshInstance3D();
        rightEye.Mesh = eyeMesh;
        rightEye.MaterialOverride = eyeMaterial;
        rightEye.Position = new Vector3(0.07f, 0.88f, 0.15f);
        _meshInstance.AddChild(rightEye);

        // Angry eyebrows (tilted boxes)
        var browMaterial = new StandardMaterial3D();
        browMaterial.AlbedoColor = skinColor.Darkened(0.3f);

        var leftBrow = new MeshInstance3D();
        var browMesh = new BoxMesh { Size = new Vector3(0.06f, 0.015f, 0.02f) };
        leftBrow.Mesh = browMesh;
        leftBrow.MaterialOverride = browMaterial;
        leftBrow.Position = new Vector3(-0.07f, 0.94f, 0.16f);
        leftBrow.RotationDegrees = new Vector3(0, 0, -15); // Angry tilt
        _meshInstance.AddChild(leftBrow);

        var rightBrow = new MeshInstance3D();
        rightBrow.Mesh = browMesh;
        rightBrow.MaterialOverride = browMaterial;
        rightBrow.Position = new Vector3(0.07f, 0.94f, 0.16f);
        rightBrow.RotationDegrees = new Vector3(0, 0, 15);
        _meshInstance.AddChild(rightBrow);

        // === NOSE ===
        var nose = new MeshInstance3D();
        var noseMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.04f, Height = 0.1f };
        nose.Mesh = noseMesh;
        nose.MaterialOverride = _material;
        nose.Position = new Vector3(0, 0.82f, 0.17f);
        nose.RotationDegrees = new Vector3(-70, 0, 0);
        _meshInstance.AddChild(nose);

        // === ARMS ===
        CreateThrowerArm(true);
        _throwingArmNode = CreateThrowerArm(false); // Right arm for throwing

        // === LEGS ===
        CreateThrowerLeg(true);
        CreateThrowerLeg(false);

        // === BELT with pouches and weapon holsters ===
        var belt = new MeshInstance3D();
        var beltMesh = new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.24f };
        belt.Mesh = beltMesh;
        belt.MaterialOverride = clothMaterial;
        belt.Position = new Vector3(0, 0.35f, 0);
        belt.RotationDegrees = new Vector3(90, 0, 0);
        belt.Scale = new Vector3(1f, 1f, 0.3f);
        _meshInstance.AddChild(belt);

        // Pouches on belt
        for (int i = 0; i < 3; i++)
        {
            var pouch = new MeshInstance3D();
            var pouchMesh = new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.04f) };
            pouch.Mesh = pouchMesh;
            pouch.MaterialOverride = bandMaterial;
            float angle = -0.8f + i * 0.8f;
            pouch.Position = new Vector3(Mathf.Sin(angle) * 0.22f, 0.32f, Mathf.Cos(angle) * 0.22f);
            _meshInstance.AddChild(pouch);
        }

        // Quiver on back with visible throwing axes
        var quiver = new MeshInstance3D();
        var quiverMesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.06f, Height = 0.3f };
        quiver.Mesh = quiverMesh;
        quiver.MaterialOverride = clothMaterial;
        quiver.Position = new Vector3(-0.15f, 0.55f, -0.12f);
        quiver.RotationDegrees = new Vector3(15, -20, 10);
        _meshInstance.AddChild(quiver);

        var metalMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.5f, 0.45f), Metallic = 0.5f, Roughness = 0.5f };
        var woodMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.35f, 0.2f), Roughness = 0.8f };

        // Mini axes sticking out of quiver
        for (int i = 0; i < 2; i++)
        {
            // Axe handle
            var handle = new MeshInstance3D();
            var handleMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.012f, Height = 0.25f };
            handle.Mesh = handleMesh;
            handle.MaterialOverride = woodMat;
            handle.Position = new Vector3(-0.14f + i * 0.04f, 0.72f, -0.14f);
            handle.RotationDegrees = new Vector3(5, -20 + i * 10, 8);
            _meshInstance.AddChild(handle);

            // Axe blade
            var blade = new MeshInstance3D();
            var bladeMesh = new BoxMesh { Size = new Vector3(0.08f, 0.05f, 0.015f) };
            blade.Mesh = bladeMesh;
            blade.MaterialOverride = metalMat;
            blade.Position = new Vector3(-0.13f + i * 0.04f, 0.84f, -0.15f);
            blade.RotationDegrees = new Vector3(5, -20 + i * 10, 8);
            _meshInstance.AddChild(blade);
        }

        // Spear holster on other side of back
        var spearHolder = new MeshInstance3D();
        var holderMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.025f, Height = 0.8f };
        spearHolder.Mesh = holderMesh;
        spearHolder.MaterialOverride = woodMat;
        spearHolder.Position = new Vector3(0.1f, 0.5f, -0.15f);
        spearHolder.RotationDegrees = new Vector3(-10, 15, -20);
        _meshInstance.AddChild(spearHolder);

        // Spear tip
        var spearTip = new MeshInstance3D();
        var tipMesh = new CylinderMesh { TopRadius = 0.001f, BottomRadius = 0.025f, Height = 0.15f };
        spearTip.Mesh = tipMesh;
        spearTip.MaterialOverride = metalMat;
        spearTip.Position = new Vector3(0.15f, 0.92f, -0.2f);
        spearTip.RotationDegrees = new Vector3(-10, 15, -20);
        _meshInstance.AddChild(spearTip);

        // === HELD PROJECTILE ===
        CreateHeldProjectile();
    }

    private void CreateShoulderPad(StandardMaterial3D material, bool isLeft)
    {
        float side = isLeft ? -1f : 1f;
        var pad = new MeshInstance3D();
        var padMesh = new SphereMesh { Radius = 0.08f, Height = 0.16f };
        pad.Mesh = padMesh;
        pad.MaterialOverride = material;
        pad.Position = new Vector3(side * 0.25f, 0.6f, 0);
        pad.Scale = new Vector3(1f, 0.6f, 0.8f);
        _meshInstance!.AddChild(pad);
    }

    private void CreateThrowerEar(bool isLeft)
    {
        float side = isLeft ? -1f : 1f;
        var ear = new MeshInstance3D();
        var earMesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.05f, Height = 0.15f };
        ear.Mesh = earMesh;
        ear.MaterialOverride = _material;
        ear.Position = new Vector3(side * 0.2f, 0.88f, 0);
        ear.RotationDegrees = new Vector3(0, 0, side * 55);
        _meshInstance!.AddChild(ear);

        // Ear notch (battle scar)
        var notch = new MeshInstance3D();
        var notchMesh = new BoxMesh { Size = new Vector3(0.025f, 0.015f, 0.012f) };
        notch.Mesh = notchMesh;
        notch.MaterialOverride = new StandardMaterial3D { AlbedoColor = _material.AlbedoColor.Darkened(0.3f) };
        notch.Position = new Vector3(side * 0.21f, 0.96f, 0.01f);
        notch.RotationDegrees = new Vector3(0, 0, side * 55);
        _meshInstance.AddChild(notch);

        // Earring (small metal ring)
        var earring = new MeshInstance3D();
        var ringMesh = new TorusMesh { InnerRadius = 0.012f, OuterRadius = 0.018f };
        earring.Mesh = ringMesh;
        var metalMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.55f, 0.45f), Metallic = 0.7f, Roughness = 0.4f };
        earring.MaterialOverride = metalMat;
        earring.Position = new Vector3(side * 0.19f, 0.84f, 0.02f);
        earring.RotationDegrees = new Vector3(0, 90, side * 55);
        earring.Scale = new Vector3(1f, 1f, 0.3f);
        _meshInstance.AddChild(earring);
    }

    private Node3D CreateThrowerArm(bool isLeft)
    {
        float side = isLeft ? -1f : 1f;
        var arm = new Node3D();
        arm.Name = isLeft ? "LeftArm" : "RightArm";
        arm.Position = new Vector3(side * 0.28f, 0.5f, 0);
        _meshInstance!.AddChild(arm);

        // More muscular upper arm
        var upperArm = new MeshInstance3D();
        var upperMesh = new CapsuleMesh { Radius = 0.055f, Height = 0.22f };
        upperArm.Mesh = upperMesh;
        upperArm.MaterialOverride = _material;
        upperArm.RotationDegrees = new Vector3(0, 0, side * 20);
        arm.AddChild(upperArm);

        // Bicep bulge
        var bicep = new MeshInstance3D();
        var bicepMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        bicep.Mesh = bicepMesh;
        bicep.MaterialOverride = _material;
        bicep.Position = new Vector3(side * 0.01f, -0.05f, 0.02f);
        bicep.Scale = new Vector3(1.2f, 0.9f, 1f);
        arm.AddChild(bicep);

        // Defined forearm
        var forearm = new MeshInstance3D();
        var foreMesh = new CapsuleMesh { Radius = 0.045f, Height = 0.2f };
        forearm.Mesh = foreMesh;
        forearm.MaterialOverride = _material;
        forearm.Position = new Vector3(side * 0.05f, -0.18f, 0.05f);
        forearm.RotationDegrees = new Vector3(-25, 0, side * 10);
        arm.AddChild(forearm);

        // Wrist wraps
        var wrapMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.5f, 0.35f), Roughness = 0.85f };
        var wrist = new MeshInstance3D();
        var wristMesh = new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.048f };
        wrist.Mesh = wristMesh;
        wrist.MaterialOverride = wrapMat;
        wrist.Position = new Vector3(side * 0.07f, -0.28f, 0.08f);
        wrist.RotationDegrees = new Vector3(65, 0, side * 10);
        wrist.Scale = new Vector3(1f, 1f, 0.6f);
        arm.AddChild(wrist);

        // Hand with clawed fingers
        var hand = new MeshInstance3D();
        var handMesh = new SphereMesh { Radius = 0.04f, Height = 0.08f };
        hand.Mesh = handMesh;
        hand.MaterialOverride = _material;
        hand.Position = new Vector3(side * 0.08f, -0.35f, 0.1f);
        hand.Scale = new Vector3(1.1f, 0.8f, 1.2f);
        hand.Name = "Hand";
        arm.AddChild(hand);

        // Sharp claws for gripping weapons
        var clawMat = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.28f, 0.22f), Roughness = 0.5f };
        for (int f = 0; f < 4; f++)
        {
            var claw = new MeshInstance3D();
            var clawMesh = new CylinderMesh { TopRadius = 0.001f, BottomRadius = 0.01f, Height = 0.04f };
            claw.Mesh = clawMesh;
            claw.MaterialOverride = clawMat;
            float fingerSpread = -0.03f + f * 0.02f;
            claw.Position = new Vector3(side * 0.08f + fingerSpread * side, -0.38f, 0.13f);
            claw.RotationDegrees = new Vector3(-70, 0, fingerSpread * 25);
            arm.AddChild(claw);
        }

        return arm;
    }

    private void CreateThrowerLeg(bool isLeft)
    {
        float side = isLeft ? -1f : 1f;
        var leg = new Node3D();
        leg.Position = new Vector3(side * 0.1f, 0.25f, 0);
        _meshInstance!.AddChild(leg);

        var thigh = new MeshInstance3D();
        var thighMesh = new CapsuleMesh { Radius = 0.06f, Height = 0.18f };
        thigh.Mesh = thighMesh;
        thigh.MaterialOverride = _material;
        leg.AddChild(thigh);

        var shin = new MeshInstance3D();
        var shinMesh = new CapsuleMesh { Radius = 0.045f, Height = 0.16f };
        shin.Mesh = shinMesh;
        shin.MaterialOverride = _material;
        shin.Position = new Vector3(0, -0.16f, 0);
        leg.AddChild(shin);

        var foot = new MeshInstance3D();
        var footMesh = new SphereMesh { Radius = 0.05f, Height = 0.1f };
        foot.Mesh = footMesh;
        foot.MaterialOverride = _material;
        foot.Position = new Vector3(0, -0.28f, 0.03f);
        foot.Scale = new Vector3(0.8f, 0.6f, 1.1f);
        leg.AddChild(foot);
    }

    private void CreateHeldProjectile()
    {
        _heldProjectileNode = new Node3D();
        _heldProjectileNode.Name = "HeldProjectile";
        // Position in right hand
        _heldProjectileNode.Position = new Vector3(0.36f, 0.15f, 0.1f);
        _meshInstance!.AddChild(_heldProjectileNode);

        UpdateHeldProjectileVisual();
    }

    private void UpdateHeldProjectileVisual()
    {
        if (_heldProjectileNode == null) return;

        // Clear existing
        foreach (var child in _heldProjectileNode.GetChildren())
        {
            child.QueueFree();
        }

        var metalMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.5f, 0.45f), Metallic = 0.5f, Roughness = 0.5f };
        var woodMat = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.35f, 0.2f), Roughness = 0.8f };
        var canMat = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.5f, 0.3f), Metallic = 0.3f, Roughness = 0.6f };

        switch (_currentProjectile)
        {
            case ThrowableType.Spear:
                // Spear shaft
                var shaft = new MeshInstance3D();
                var shaftMesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.018f, Height = 0.8f };
                shaft.Mesh = shaftMesh;
                shaft.MaterialOverride = woodMat;
                shaft.Position = new Vector3(0, 0.4f, 0);
                _heldProjectileNode.AddChild(shaft);

                // Spear head
                var spearHead = new MeshInstance3D();
                var headMesh = new PrismMesh { Size = new Vector3(0.04f, 0.15f, 0.02f) };
                spearHead.Mesh = headMesh;
                spearHead.MaterialOverride = metalMat;
                spearHead.Position = new Vector3(0, 0.85f, 0);
                _heldProjectileNode.AddChild(spearHead);
                break;

            case ThrowableType.ThrowingAxe:
                // Axe handle
                var handle = new MeshInstance3D();
                var handleMesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.022f, Height = 0.4f };
                handle.Mesh = handleMesh;
                handle.MaterialOverride = woodMat;
                handle.Position = new Vector3(0, 0.2f, 0);
                _heldProjectileNode.AddChild(handle);

                // Axe head
                var axeHead = new MeshInstance3D();
                var axeMesh = new BoxMesh { Size = new Vector3(0.12f, 0.08f, 0.02f) };
                axeHead.Mesh = axeMesh;
                axeHead.MaterialOverride = metalMat;
                axeHead.Position = new Vector3(0.04f, 0.42f, 0);
                _heldProjectileNode.AddChild(axeHead);
                break;

            case ThrowableType.BeerCan:
                // Beer can/mug
                var can = new MeshInstance3D();
                var canMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.035f, Height = 0.12f };
                can.Mesh = canMesh;
                can.MaterialOverride = canMat;
                can.Position = new Vector3(0, 0.06f, 0);
                _heldProjectileNode.AddChild(can);

                // Handle
                var mugHandle = new MeshInstance3D();
                var mugMesh = new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.04f };
                mugHandle.Mesh = mugMesh;
                mugHandle.MaterialOverride = canMat;
                mugHandle.Position = new Vector3(0.06f, 0.06f, 0);
                mugHandle.RotationDegrees = new Vector3(0, 90, 0);
                mugHandle.Scale = new Vector3(1, 1, 0.5f);
                _heldProjectileNode.AddChild(mugHandle);
                break;
        }

        // Rotate to look like being held for throwing
        _heldProjectileNode.RotationDegrees = new Vector3(-45, 0, -15);
    }

    private void CreateHealthBar()
    {
        _healthBarContainer = new Node3D();
        _healthBarContainer.Name = "HealthBar";
        _healthBarContainer.Position = new Vector3(0, 1.8f, 0);
        AddChild(_healthBarContainer);

        // Monster name label above health bar
        _nameLabel = new Label3D();
        _nameLabel.Text = "Goblin Thrower";
        _nameLabel.FontSize = 32;
        _nameLabel.OutlineSize = 4;
        _nameLabel.Modulate = new Color(0.9f, 0.8f, 0.6f); // Tan tint for thrower
        _nameLabel.Position = new Vector3(0, 0.12f, 0);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.VerticalAlignment = VerticalAlignment.Bottom;
        _nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _nameLabel.NoDepthTest = false; // Don't render through walls
        _healthBarContainer.AddChild(_nameLabel);

        // Background - uses material billboard mode for GPU billboarding
        var bg = new MeshInstance3D();
        var bgMesh = new BoxMesh { Size = new Vector3(0.9f, 0.1f, 0.02f) };
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
        _healthBarFillMesh = new BoxMesh { Size = new Vector3(0.8f, 0.08f, 0.025f) };
        _healthBarFill.Mesh = _healthBarFillMesh;
        _healthBarFillMat = new StandardMaterial3D();
        _healthBarFillMat.AlbedoColor = new Color(0.2f, 0.8f, 0.3f);
        _healthBarFillMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _healthBarFillMat.EmissionEnabled = true;
        _healthBarFillMat.Emission = new Color(0.1f, 0.4f, 0.15f);
        _healthBarFillMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled; // PERF: GPU billboarding
        _healthBarFillMat.NoDepthTest = false;
        _healthBarFill.MaterialOverride = _healthBarFillMat;
        _healthBarFill.Position = new Vector3(0, 0, 0.01f);
        _healthBarFill.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthBarContainer.AddChild(_healthBarFill);

        // Show based on nameplate toggle setting
        _healthBarContainer.Visible = GameManager3D.NameplatesEnabled;
    }

    private void CreateEnrageIndicator()
    {
        // Floating enrage icon above head
        _enrageIndicator = new Node3D();
        _enrageIndicator.Name = "EnrageIndicator";
        _enrageIndicator.Position = new Vector3(0, 2.2f, 0);
        _enrageIndicator.Visible = false;
        AddChild(_enrageIndicator);

        // Rage symbol (angry face or fire icon using meshes)
        var symbol = new MeshInstance3D();
        var symbolMesh = new SphereMesh { Radius = 0.12f, Height = 0.24f };
        symbol.Mesh = symbolMesh;
        var symbolMat = new StandardMaterial3D();
        symbolMat.AlbedoColor = new Color(1f, 0.3f, 0.1f);
        symbolMat.EmissionEnabled = true;
        symbolMat.Emission = new Color(1f, 0.4f, 0.1f);
        symbolMat.EmissionEnergyMultiplier = 3f;
        symbolMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        symbol.MaterialOverride = symbolMat;
        _enrageIndicator.AddChild(symbol);

        // Create rage particles
        _enrageParticles = new GpuParticles3D();
        _enrageParticles.Amount = 15;
        _enrageParticles.Lifetime = 0.8f;
        _enrageParticles.Emitting = false;
        _enrageParticles.Position = new Vector3(0, 1f, 0);

        var material = new ParticleProcessMaterial();
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = 0.3f;
        material.Direction = new Vector3(0, 1, 0);
        material.Spread = 30f;
        material.InitialVelocityMin = 1f;
        material.InitialVelocityMax = 2f;
        material.Gravity = new Vector3(0, 2, 0);
        material.ScaleMin = 0.03f;
        material.ScaleMax = 0.06f;
        material.Color = new Color(1f, 0.4f, 0.1f);

        _enrageParticles.ProcessMaterial = material;

        var quadMesh = new QuadMesh { Size = new Vector2(0.08f, 0.08f) };
        var quadMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.5f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.3f, 0.1f),
            EmissionEnergyMultiplier = 2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        quadMesh.Material = quadMat;
        _enrageParticles.DrawPass1 = quadMesh;

        AddChild(_enrageParticles);
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
        UpdateEnrage(dt);
        UpdateVisuals(dt);
        UpdateHealthBar();
    }

    private void UpdateTimers(float dt)
    {
        _throwTimer += dt;
        _stateTimer += dt;
        _hitFlashTimer -= dt;
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
                    if (distToPlayer < FleeRange)
                    {
                        ChangeState(State.Fleeing);
                    }
                    else if (distToPlayer > PreferredRange + 2f)
                    {
                        ChangeState(State.Repositioning);
                    }
                    else if (_throwTimer >= GetThrowCooldown())
                    {
                        ChangeState(State.Throwing);
                    }
                }
                // Face player using Atan2 for correct facing (models face +Z by default)
                if (hasLOS) FaceTarget(_player.GlobalPosition);
                break;

            case State.Repositioning:
                // Move to preferred range
                if (distToPlayer < FleeRange)
                {
                    ChangeState(State.Fleeing);
                }
                else if (distToPlayer >= PreferredRange - 1f && distToPlayer <= PreferredRange + 3f)
                {
                    ChangeState(State.Idle);
                }
                else
                {
                    // Move toward optimal range
                    Vector3 toPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
                    toPlayer.Y = 0;

                    if (distToPlayer > PreferredRange)
                    {
                        // Move closer
                        Velocity = toPlayer * MoveSpeed;
                    }
                    else
                    {
                        // Move away (shouldn't hit this, but just in case)
                        Velocity = -toPlayer * MoveSpeed;
                    }
                    MoveAndSlide();
                    FaceTarget(_player.GlobalPosition);
                }
                break;

            case State.Throwing:
                _throwAnimTimer += dt;

                // Cancel throw if we lose line of sight
                if (!hasLOS)
                {
                    _throwAnimTimer = 0;
                    _isWindingUp = false;
                    if (_throwingArmNode != null)
                        _throwingArmNode.RotationDegrees = Vector3.Zero;
                    ChangeState(State.Idle);
                    break;
                }

                // Wind up animation
                if (_throwAnimTimer < 0.3f)
                {
                    _isWindingUp = true;
                    if (_throwingArmNode != null)
                    {
                        float windUp = _throwAnimTimer / 0.3f;
                        _throwingArmNode.RotationDegrees = new Vector3(-60 * windUp, 0, 0);
                    }
                }
                // Release throw
                else if (_throwAnimTimer >= 0.3f && _isWindingUp)
                {
                    _isWindingUp = false;
                    ThrowProjectile();
                }
                // Follow through
                else if (_throwAnimTimer < 0.6f)
                {
                    if (_throwingArmNode != null)
                    {
                        float followThrough = (_throwAnimTimer - 0.3f) / 0.3f;
                        _throwingArmNode.RotationDegrees = new Vector3(-60 + 90 * followThrough, 0, 0);
                    }
                }
                else
                {
                    // Reset arm
                    if (_throwingArmNode != null)
                        _throwingArmNode.RotationDegrees = Vector3.Zero;

                    _throwTimer = 0;
                    _throwAnimTimer = 0;

                    // Pick next projectile type randomly
                    _currentProjectile = (ThrowableType)((int)(GD.Randf() * 3));
                    UpdateHeldProjectileVisual();

                    ChangeState(State.Idle);
                }

                FaceTarget(_player.GlobalPosition);
                break;

            case State.Fleeing:
                Vector3 fleeDir = (GlobalPosition - _player.GlobalPosition).Normalized();
                fleeDir.Y = 0;
                Velocity = fleeDir * MoveSpeed * 1.3f;
                MoveAndSlide();

                // Face away from player while fleeing (face movement direction)
                FaceDirection(fleeDir);

                if (distToPlayer > PreferredRange || _stateTimer > 2.5f)
                {
                    ChangeState(State.Idle);
                }
                break;
        }
    }

    private float GetThrowCooldown()
    {
        return _isEnraged ? ThrowCooldown * 0.6f : ThrowCooldown;
    }

    private void ThrowProjectile()
    {
        if (_player == null) return;

        // Don't throw if we don't have line of sight (safety check)
        if (!HasLineOfSightToPlayer()) return;

        Vector3 spawnPos = GlobalPosition + new Vector3(0, 1.2f, 0);
        Vector3 targetPos = _player.GlobalPosition + new Vector3(0, 1f, 0);
        Vector3 direction = (targetPos - spawnPos).Normalized();

        float damage = _isEnraged ? Damage * 1.5f : Damage;
        Color projectileColor;
        float projectileSpeed;

        switch (_currentProjectile)
        {
            case ThrowableType.Spear:
                projectileColor = new Color(0.6f, 0.5f, 0.4f);
                projectileSpeed = 20f;
                break;
            case ThrowableType.ThrowingAxe:
                projectileColor = new Color(0.5f, 0.45f, 0.4f);
                projectileSpeed = 16f;
                damage *= 1.2f; // Axes do more damage
                break;
            case ThrowableType.BeerCan:
                projectileColor = new Color(0.7f, 0.6f, 0.3f);
                projectileSpeed = 12f;
                damage *= 0.7f; // Beer cans do less damage
                break;
            default:
                projectileColor = new Color(0.5f, 0.5f, 0.5f);
                projectileSpeed = 18f;
                break;
        }

        var projectile = ThrownProjectile3D.Create(
            spawnPos,
            direction,
            damage,
            _currentProjectile,
            projectileColor
        );
        projectile.Speed = projectileSpeed;

        GetTree().Root.AddChild(projectile);
        projectile.Fire(direction);

        // Hide held projectile briefly
        if (_heldProjectileNode != null)
            _heldProjectileNode.Visible = false;

        // Show it again after throw animation
        var timer = GetTree().CreateTimer(0.5f);
        timer.Timeout += () =>
        {
            if (_heldProjectileNode != null)
                _heldProjectileNode.Visible = true;
        };

        SoundManager3D.Instance?.PlayAttackSound();
        GD.Print($"[GoblinThrower] Threw {_currentProjectile}!");
    }

    public void ApplyEnrage(float duration)
    {
        _isEnraged = true;
        _enrageTimer = duration;

        if (_enrageIndicator != null)
            _enrageIndicator.Visible = true;

        if (_enrageParticles != null)
            _enrageParticles.Emitting = true;

        // Speed boost
        MoveSpeed = 4.5f;

        GD.Print($"[GoblinThrower] ENRAGED for {duration}s!");
    }

    private void UpdateEnrage(float dt)
    {
        if (!_isEnraged) return;

        _enrageTimer -= dt;

        // Pulse the rage indicator
        if (_enrageIndicator != null)
        {
            float pulse = 1f + Mathf.Sin(_stateTimer * 8f) * 0.2f;
            _enrageIndicator.Scale = Vector3.One * pulse;
        }

        if (_enrageTimer <= 0)
        {
            _isEnraged = false;
            MoveSpeed = 3.5f; // Reset speed

            if (_enrageIndicator != null)
                _enrageIndicator.Visible = false;

            if (_enrageParticles != null)
                _enrageParticles.Emitting = false;

            GD.Print("[GoblinThrower] Enrage ended");
        }
    }

    private void UpdateVisuals(float dt)
    {
        // Hit flash
        if (_hitFlashTimer > 0 && _material != null)
        {
            float flash = _hitFlashTimer / 0.2f;
            _material.EmissionEnabled = true;
            _material.Emission = new Color(1f, 0.3f, 0.3f) * flash;
        }
        else if (_material != null && !_isEnraged)
        {
            _material.EmissionEnabled = false;
        }

        // Enrage glow
        if (_isEnraged && _material != null)
        {
            float pulse = 0.3f + Mathf.Sin(_stateTimer * 6f) * 0.1f;
            _material.EmissionEnabled = true;
            _material.Emission = new Color(1f, 0.4f, 0.1f) * pulse;
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
        if (_healthBarFillMesh == null || _healthBarFillMat == null) return;

        float healthPercent = (float)CurrentHealth / MaxHealth;
        healthPercent = Mathf.Clamp(healthPercent, 0f, 1f);

        float fullWidth = 0.8f;
        float currentWidth = fullWidth * healthPercent;
        _healthBarFillMesh.Size = new Vector3(currentWidth, 0.08f, 0.025f);

        if (_healthBarFill != null)
        {
            _healthBarFill.Position = new Vector3((currentWidth - fullWidth) / 2, 0, 0.01f);
        }

        // Color based on health - use cached material reference
        Color healthColor = healthPercent > 0.6f ? new Color(0.2f, 0.8f, 0.3f) :
                           healthPercent > 0.3f ? new Color(0.9f, 0.8f, 0.2f) :
                           new Color(0.9f, 0.2f, 0.2f);

        _healthBarFillMat.AlbedoColor = healthColor;
        _healthBarFillMat.Emission = healthColor * 0.3f;
    }

    private void ChangeState(State newState)
    {
        CurrentState = newState;
        _stateTimer = 0;

        // Play appropriate animation for new state
        switch (newState)
        {
            case State.Idle:
                PlayAnimation(AnimationType.Idle);
                break;
            case State.Repositioning:
                PlayAnimation(AnimationType.Walk);
                break;
            case State.Throwing:
                PlayAnimation(AnimationType.Attack); // Throwing uses attack animation
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
    /// Check if thrower is in stasis (for In-Map Editor).
    /// </summary>
    public bool IsInStasis() => _isInStasis;

    /// <summary>
    /// Enter stasis mode - thrower plays idle animation but doesn't react to player.
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

        GD.Print($"[GoblinThrower] Entered stasis");
    }

    /// <summary>
    /// Exit stasis mode - thrower resumes normal AI behavior.
    /// </summary>
    public void ExitStasis()
    {
        if (!_isInStasis) return;

        _isInStasis = false;
        ChangeState(State.Idle);

        GD.Print($"[GoblinThrower] Exited stasis");
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source)
    {
        TakeDamage(damage, fromPosition, source, false);
    }

    public void TakeDamage(float damage, Vector3 fromPosition, string source, bool isCrit)
    {
        if (CurrentState == State.Dead) return;

        int intDamage = (int)damage;
        CurrentHealth -= intDamage;

        _hitFlashTimer = 0.2f;

        // Play hit animation
        if (CurrentState != State.Dead)
        {
            PlayAnimation(AnimationType.Hit);
        }

        EmitSignal(SignalName.Damaged, intDamage, CurrentHealth);
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

        GD.Print($"[GoblinThrower] Took {intDamage} damage from {source}. Health: {CurrentHealth}/{MaxHealth}");
    }

    private void SpawnFloatingDamageText(int damage, bool isCrit = false)
    {
        var textContainer = new Node3D();
        textContainer.GlobalPosition = GlobalPosition + new Vector3(0, 2f, 0);
        GetTree().Root.AddChild(textContainer);

        var label = new Label3D();
        label.Text = isCrit ? $"!{damage}!" : damage.ToString();
        label.FontSize = isCrit ? 96 : 64;
        label.OutlineSize = isCrit ? 12 : 8;
        label.Modulate = isCrit ? new Color(1f, 0.85f, 0.2f) : (damage >= 20 ? new Color(1f, 0.3f, 0.1f) : new Color(1f, 0.9f, 0.2f));
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
            HUD3D.Instance?.LogEnemyDeath("Goblin Thrower", ExperienceReward);

            // Register kill for achievements
            HUD3D.Instance?.RegisterKill("goblin_thrower", false);
        }

        GD.Print("[GoblinThrower] Died!");

        // Wait for death animation to complete before spawning corpse
        float deathAnimDuration = 2.5f;

        var tween = CreateTween();
        tween.TweenInterval(deathAnimDuration);
        tween.TweenCallback(Callable.From(() =>
        {
            var corpse = Corpse3D.Create("goblin_thrower", false, GlobalPosition, Rotation.Y, Level);
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
