using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Items;
using SafeRoom3D.UI;

namespace SafeRoom3D.Enemies;

/// <summary>
/// A corpse left behind when an enemy dies. Can be looted by the player.
/// </summary>
public partial class Corpse3D : StaticBody3D
{
    public string MonsterType { get; private set; } = "Unknown";
    public bool IsBoss { get; private set; }
    public int MonsterLevel { get; private set; } = 1;
    public LootBag Loot { get; private set; } = new();
    public bool HasBeenLooted => Loot.IsEmpty();

    private MeshInstance3D? _corpseMesh;
    private CollisionShape3D? _collider;
    private OmniLight3D? _glowLight;
    private float _glowTimer;
    private bool _isHighlighted;

    // Despawn timer - corpses disappear after a while
    private float _despawnTimer = 120f; // 2 minutes
    private bool _startedDespawn;

    // Auto-loot
    private const float AutoLootRadius = 2.5f; // Distance for auto-loot
    private bool _autoLooted;

    // Signals
    [Signal] public delegate void LootedEventHandler(Corpse3D corpse);

    public override void _Ready()
    {
        // Add to corpses group for easy lookup
        AddToGroup("Corpses");

        // Set collision layers (Trigger layer for interaction)
        CollisionLayer = 64; // Trigger layer (layer 7)
        CollisionMask = 0;

        GD.Print($"[Corpse3D] Created corpse for {MonsterType}");
    }

    /// <summary>
    /// Initialize the corpse from a dead enemy
    /// </summary>
    public void Initialize(string monsterType, bool isBoss, Vector3 position, float rotation, int monsterLevel = 1)
    {
        MonsterType = monsterType;
        IsBoss = isBoss;
        MonsterLevel = monsterLevel;
        GlobalPosition = position;
        Rotation = new Vector3(0, rotation, 0);

        // Generate loot with monster level for equipment scaling
        Loot = LootBag.GenerateMonsterLoot(monsterType, isBoss, monsterLevel);

        // Create visual representation
        CreateCorpseVisuals();

        // Create interaction collider
        CreateCollider();

        // Add glow effect to show loot is available
        CreateGlowEffect();
    }

    private void CreateCorpseVisuals()
    {
        _corpseMesh = new MeshInstance3D();

        // Create a simple fallen body representation
        // Boss corpses are larger
        float scale = IsBoss ? 1.5f : 1f;

        // Body (capsule laying on its side)
        var bodyMesh = new CapsuleMesh();
        bodyMesh.Radius = 0.3f * scale;
        bodyMesh.Height = 0.8f * scale;
        _corpseMesh.Mesh = bodyMesh;

        // Material based on monster type
        var material = new StandardMaterial3D();
        material.AlbedoColor = GetCorpseColor();
        material.Roughness = 0.9f;
        // Slight desaturation for "dead" look
        material.AlbedoColor = material.AlbedoColor.Darkened(0.3f);
        _corpseMesh.MaterialOverride = material;

        // Rotate to lay on side
        _corpseMesh.RotationDegrees = new Vector3(90, 0, 0);
        _corpseMesh.Position = new Vector3(0, bodyMesh.Radius, 0);

        AddChild(_corpseMesh);
    }

    private Color GetCorpseColor()
    {
        return MonsterType.ToLower() switch
        {
            "slime" => new Color(0.3f, 0.5f, 0.3f),
            "skeleton" => new Color(0.8f, 0.75f, 0.7f),
            "wolf" => new Color(0.4f, 0.35f, 0.3f),
            "bat" => new Color(0.3f, 0.25f, 0.3f),
            "dragon" => new Color(0.6f, 0.2f, 0.15f),
            "goblin" or "torchbearer" or "goblin_torchbearer" => new Color(0.4f, 0.5f, 0.35f),
            _ => new Color(0.5f, 0.45f, 0.4f)
        };
    }

    private void CreateCollider()
    {
        _collider = new CollisionShape3D();
        var shape = new BoxShape3D();
        float scale = IsBoss ? 1.5f : 1f;
        shape.Size = new Vector3(1f * scale, 0.5f * scale, 0.8f * scale);
        _collider.Shape = shape;
        _collider.Position = new Vector3(0, 0.25f * scale, 0);
        AddChild(_collider);
    }

    private void CreateGlowEffect()
    {
        if (Loot.IsEmpty()) return;

        _glowLight = new OmniLight3D();
        _glowLight.LightColor = new Color(1f, 0.9f, 0.5f); // Gold glow
        _glowLight.LightEnergy = 0.5f;
        _glowLight.OmniRange = 2f;
        _glowLight.OmniAttenuation = 1.5f;
        _glowLight.Position = new Vector3(0, 0.5f, 0);
        AddChild(_glowLight);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Pulsing glow effect when loot is available
        if (_glowLight != null && !Loot.IsEmpty())
        {
            _glowTimer += dt * 2f;
            float pulse = (Mathf.Sin(_glowTimer) + 1f) * 0.5f;
            _glowLight.LightEnergy = 0.3f + pulse * 0.4f;
        }

        // Auto-loot when player walks nearby
        if (!_autoLooted && !Loot.IsEmpty())
        {
            CheckAutoLoot();
        }

        // Highlight effect when player is nearby (still useful for visual feedback)
        UpdateHighlight();

        // Despawn timer
        _despawnTimer -= dt;
        if (_despawnTimer <= 0 && !_startedDespawn)
        {
            StartDespawn();
        }
    }

    private void CheckAutoLoot()
    {
        var player = Player.FPSController.Instance;
        if (player == null) return;

        float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
        if (distance <= AutoLootRadius)
        {
            AutoLootAll();
        }
    }

    private void AutoLootAll()
    {
        _autoLooted = true;

        var inventory = Inventory3D.Instance;
        if (inventory == null) return;

        bool anyLooted = false;

        // Transfer all loot to player's inventory with notifications
        for (int slot = 0; slot < Loot.SlotCount; slot++)
        {
            var item = Loot.GetItem(slot);
            if (item == null) continue;

            string itemName = item.Name;
            int quantity = item.StackCount;
            Color itemColor = GetItemColor(item);

            // Try to add to inventory (clone the item)
            bool added = inventory.AddItem(item.Clone());
            if (added)
            {
                // Show notification with rarity color for equipment
                HUD3D.Instance?.ShowLootNotification(itemName, quantity, itemColor);
                Loot.TakeItem(slot); // Remove from loot bag
                anyLooted = true;
                GD.Print($"[Corpse3D] Auto-looted {quantity}x {itemName}");
            }
            else
            {
                // Inventory full - show message
                HUD3D.Instance?.AddCombatLogMessage($"[color=#ff6666]Inventory full![/color] Could not pick up {itemName}.", new Color(1f, 0.4f, 0.4f));
                GD.Print($"[Corpse3D] Inventory full, could not auto-loot {itemName}");
                _autoLooted = false; // Allow retry when space available
                break;
            }
        }

        // Play loot pickup sound if any items were looted
        if (anyLooted)
        {
            SoundManager3D.Instance?.PlayLootPickupSound();
            // Start visual fade/shrink to indicate looting
            StartLootedFade();
        }

        // Clean up if all loot collected
        if (Loot.IsEmpty())
        {
            OnLootingComplete();
        }
    }

    private bool _lootedFadeStarted;

    private void StartLootedFade()
    {
        if (_lootedFadeStarted) return;
        _lootedFadeStarted = true;

        // Shrink and fade the corpse mesh to show it's been looted
        var tween = CreateTween();
        tween.SetParallel(true);

        // Shrink the mesh
        if (_corpseMesh != null)
        {
            tween.TweenProperty(_corpseMesh, "scale", Vector3.One * 0.3f, 0.5f);
        }

        // Fade out the glow light
        if (_glowLight != null)
        {
            tween.TweenProperty(_glowLight, "light_energy", 0f, 0.5f);
        }

        // After shrink, queue free if loot is empty
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (Loot.IsEmpty())
            {
                QueueFree();
            }
        }));
    }

    private static Color GetItemColor(InventoryItem item)
    {
        // For equipment, use rarity color
        if (item.Type == ItemType.Equipment && item.Equipment != null)
        {
            return item.GetRarityColor();
        }

        // Return color based on item type for notification icon
        return item.Id switch
        {
            "gold_coins" or "scattered_coins" => new Color(1f, 0.85f, 0.3f), // Gold
            "health_potion" => new Color(0.9f, 0.2f, 0.2f), // Red
            "mana_potion" => new Color(0.2f, 0.4f, 0.9f), // Blue
            "mystery_meat" => new Color(0.7f, 0.4f, 0.3f), // Brown
            "goblin_tooth" => new Color(0.8f, 0.8f, 0.7f), // Off-white
            "rusty_key" => new Color(0.6f, 0.5f, 0.3f), // Rusty
            "ancient_scroll" => new Color(0.9f, 0.85f, 0.7f), // Parchment
            "gemstone" => new Color(0.3f, 0.8f, 0.4f), // Green gem
            "bone_fragment" => new Color(0.9f, 0.88f, 0.82f), // Bone
            "tattered_cloth" => new Color(0.5f, 0.45f, 0.4f), // Gray
            "venom_sac" => new Color(0.4f, 0.8f, 0.3f), // Toxic green
            _ => new Color(0.7f, 0.7f, 0.75f) // Default gray
        };
    }

    private void UpdateHighlight()
    {
        // Check if player is looking at us and close enough
        var player = Player.FPSController.Instance;
        if (player == null) return;

        float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
        bool shouldHighlight = distance < 3f && !Loot.IsEmpty();

        if (shouldHighlight != _isHighlighted)
        {
            _isHighlighted = shouldHighlight;
            if (_corpseMesh?.MaterialOverride is StandardMaterial3D mat)
            {
                if (_isHighlighted)
                {
                    mat.EmissionEnabled = true;
                    mat.Emission = new Color(1f, 0.9f, 0.5f);
                    mat.EmissionEnergyMultiplier = 0.3f;
                }
                else
                {
                    mat.EmissionEnabled = false;
                }
            }
        }
    }

    private void StartDespawn()
    {
        _startedDespawn = true;

        // Shrink out and remove (3D nodes don't have modulate)
        var tween = CreateTween();
        tween.SetParallel(true);
        if (_corpseMesh != null)
        {
            tween.TweenProperty(_corpseMesh, "scale", Vector3.Zero, 2f);
        }
        if (_glowLight != null)
        {
            tween.TweenProperty(_glowLight, "light_energy", 0f, 2f);
        }
        tween.Chain().TweenCallback(Callable.From(() => QueueFree()));
    }

    /// <summary>
    /// Called when the player interacts with the corpse
    /// </summary>
    public void Interact()
    {
        if (Loot.IsEmpty())
        {
            GD.Print($"[Corpse3D] {MonsterType} corpse has already been looted");
            return;
        }

        GD.Print($"[Corpse3D] Player looting {MonsterType} corpse");

        // Open loot UI
        UI.LootUI3D.Instance?.Open(this);
    }

    /// <summary>
    /// Called when looting is complete
    /// </summary>
    public void OnLootingComplete()
    {
        EmitSignal(SignalName.Looted, this);

        // Remove glow if empty
        if (Loot.IsEmpty() && _glowLight != null)
        {
            _glowLight.QueueFree();
            _glowLight = null;
        }

        // Speed up despawn for looted corpses
        if (Loot.IsEmpty())
        {
            _despawnTimer = Mathf.Min(_despawnTimer, 30f);
        }
    }

    /// <summary>
    /// Create a corpse from a dead enemy
    /// </summary>
    public static Corpse3D Create(string monsterType, bool isBoss, Vector3 position, float rotation, int monsterLevel = 1)
    {
        var corpse = new Corpse3D();
        corpse.Initialize(monsterType, isBoss, position, rotation, monsterLevel);
        return corpse;
    }
}
