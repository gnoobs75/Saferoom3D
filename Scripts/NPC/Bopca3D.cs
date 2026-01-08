using Godot;
using SafeRoom3D.Enemies;
using SafeRoom3D.UI;

namespace SafeRoom3D.NPC;

/// <summary>
/// Bopca the gnome shopkeeper NPC.
/// A friendly merchant with a pointy red hat who buys and sells items.
/// </summary>
public partial class Bopca3D : BaseNPC3D
{
    public override string NPCName => "Bopca";
    public override string InteractionPrompt => "Press [T] to Trade";
    public override bool IsShopkeeper => true;

    // Idle animation state
    private float _swayPhase;
    private float _handRubPhase;
    private float _lookAroundTimer;
    private float _targetHeadYaw;
    private float _currentHeadYaw;

    // Animation constants
    private const float SwaySpeed = 1.5f;
    private const float SwayAmount = 0.02f;
    private const float HandRubSpeed = 3f;
    private const float HandRubAmount = 8f; // degrees
    private const float LookAroundInterval = 4f;
    private const float HeadTurnSpeed = 2f;

    protected override void CreateMesh()
    {
        if (_meshRoot == null) return;

        _limbs = MonsterMeshFactory.CreateMonsterMesh(_meshRoot, "bopca");
    }

    protected override float GetNameplateHeight() => 1.1f; // Bopca is shorter

    public override void Interact(Node3D player)
    {
        GD.Print($"[Bopca] Player interacted - opening shop");

        // Open shop UI
        var shopUI = ShopUI3D.Instance;
        if (shopUI != null)
        {
            shopUI.Open(this);
        }
        else
        {
            GD.PrintErr("[Bopca] ShopUI3D.Instance is null - shop UI not available");
        }
    }

    protected override void AnimateIdle(double delta)
    {
        if (_limbs == null) return;

        float dt = (float)delta;
        _swayPhase += dt * SwaySpeed;
        _handRubPhase += dt * HandRubSpeed;

        // Weight shift side-to-side (subtle body sway)
        if (_limbs.Body != null && _meshRoot != null)
        {
            float sway = Mathf.Sin(_swayPhase) * SwayAmount;
            _meshRoot.Position = new Vector3(sway, 0, 0);
        }

        // Hand rubbing animation (arms move slightly)
        if (_limbs.LeftArm != null && _limbs.RightArm != null)
        {
            float rubAngle = Mathf.Sin(_handRubPhase) * HandRubAmount;

            // Get base rotations
            var leftBaseRot = new Vector3(15, 0, -30);
            var rightBaseRot = new Vector3(15, 0, 30);

            // Add rubbing motion
            _limbs.LeftArm.RotationDegrees = leftBaseRot + new Vector3(rubAngle * 0.5f, 0, rubAngle * 0.3f);
            _limbs.RightArm.RotationDegrees = rightBaseRot + new Vector3(-rubAngle * 0.5f, 0, -rubAngle * 0.3f);
        }

        // Look around occasionally
        _lookAroundTimer -= dt;
        if (_lookAroundTimer <= 0)
        {
            _lookAroundTimer = LookAroundInterval + GD.Randf() * 2f;

            // If player is nearby, look at them; otherwise random look
            if (_playerInRange)
            {
                var player = SafeRoom3D.Player.FPSController.Instance;
                if (player != null)
                {
                    var toPlayer = player.GlobalPosition - GlobalPosition;
                    _targetHeadYaw = Mathf.Atan2(toPlayer.X, toPlayer.Z) * Mathf.RadToDeg(1f);
                    // Clamp to reasonable range
                    _targetHeadYaw = Mathf.Clamp(_targetHeadYaw - RotationDegrees.Y, -45f, 45f);
                }
            }
            else
            {
                _targetHeadYaw = (GD.Randf() - 0.5f) * 60f; // Random between -30 and 30 degrees
            }
        }

        // Smooth head turn
        _currentHeadYaw = Mathf.Lerp(_currentHeadYaw, _targetHeadYaw, dt * HeadTurnSpeed);
        if (_limbs.Head != null)
        {
            var headRot = _limbs.Head.RotationDegrees;
            _limbs.Head.RotationDegrees = new Vector3(headRot.X, _currentHeadYaw, headRot.Z);
        }

        // Slight leg shift when swaying (weight distribution)
        if (_limbs.LeftLeg != null && _limbs.RightLeg != null)
        {
            float legShift = Mathf.Sin(_swayPhase) * 2f;
            _limbs.LeftLeg.RotationDegrees = new Vector3(0, 0, legShift);
            _limbs.RightLeg.RotationDegrees = new Vector3(0, 0, -legShift);
        }
    }

    /// <summary>
    /// Get the shop inventory for this Bopca instance.
    /// </summary>
    public ShopInventory GetShopInventory()
    {
        // Return default shop inventory
        // In the future, this could be per-instance customized
        return ShopInventory.GetDefaultInventory();
    }
}

/// <summary>
/// Represents an item in a shop's inventory
/// </summary>
public class ShopItem
{
    public string ItemId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int BuyPrice { get; set; }
    public int Stock { get; set; } = -1; // -1 = unlimited

    public int SellPrice => BuyPrice / 2; // Players get 50% when selling
}

/// <summary>
/// A shop's inventory of items for sale
/// </summary>
public class ShopInventory
{
    public System.Collections.Generic.List<ShopItem> Items { get; set; } = new();

    public static ShopInventory GetDefaultInventory()
    {
        return new ShopInventory
        {
            Items = new System.Collections.Generic.List<ShopItem>
            {
                new ShopItem { ItemId = "health_potion_small", DisplayName = "Small Health Potion", BuyPrice = 25, Stock = 10 },
                new ShopItem { ItemId = "health_potion_medium", DisplayName = "Medium Health Potion", BuyPrice = 75, Stock = 5 },
                new ShopItem { ItemId = "health_potion_large", DisplayName = "Large Health Potion", BuyPrice = 150, Stock = 3 },
                new ShopItem { ItemId = "mana_potion_small", DisplayName = "Small Mana Potion", BuyPrice = 30, Stock = 10 },
                new ShopItem { ItemId = "mana_potion_medium", DisplayName = "Medium Mana Potion", BuyPrice = 90, Stock = 5 },
                new ShopItem { ItemId = "antidote", DisplayName = "Antidote", BuyPrice = 50, Stock = 5 },
                new ShopItem { ItemId = "scroll_town_portal", DisplayName = "Scroll of Town Portal", BuyPrice = 100, Stock = 3 },
            }
        };
    }
}
