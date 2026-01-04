using System;
using System.Collections.Generic;
using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Player;

namespace SafeRoom3D.Items;

/// <summary>
/// Manages player equipment across 8 slots.
/// Handles equipping, unequipping, and stat recalculation.
/// </summary>
public class EquipmentManager
{
    // Equipment slots
    private readonly Dictionary<EquipmentSlot, EquipmentItem?> _equippedItems = new()
    {
        { EquipmentSlot.Head, null },
        { EquipmentSlot.Chest, null },
        { EquipmentSlot.Hands, null },
        { EquipmentSlot.Feet, null },
        { EquipmentSlot.MainHand, null },
        { EquipmentSlot.OffHand, null },
        { EquipmentSlot.Ring1, null },
        { EquipmentSlot.Ring2, null }
    };

    // Reference to character stats for recalculation
    private CharacterStats? _stats;

    // Events
    public event Action? OnEquipmentChanged;
    public event Action<EquipmentSlot, EquipmentItem?>? OnSlotChanged;

    /// <summary>
    /// Get all equipped items.
    /// </summary>
    public IReadOnlyDictionary<EquipmentSlot, EquipmentItem?> EquippedItems => _equippedItems;

    /// <summary>
    /// Initialize with a reference to character stats.
    /// </summary>
    public void Initialize(CharacterStats stats)
    {
        _stats = stats;
    }

    /// <summary>
    /// Get the item in a specific slot.
    /// </summary>
    public EquipmentItem? GetEquippedItem(EquipmentSlot slot)
    {
        return _equippedItems.TryGetValue(slot, out var item) ? item : null;
    }

    /// <summary>
    /// Check if a slot is occupied.
    /// </summary>
    public bool IsSlotOccupied(EquipmentSlot slot)
    {
        return _equippedItems.TryGetValue(slot, out var item) && item != null;
    }

    /// <summary>
    /// Check if the player can equip an item (level requirement, slot availability).
    /// </summary>
    public bool CanEquip(EquipmentItem item)
    {
        // Check level requirement
        int playerLevel = FPSController.Instance?.PlayerLevel ?? 1;
        if (playerLevel < item.RequiredLevel)
        {
            GD.Print($"[EquipmentManager] Cannot equip {item.Name}: requires level {item.RequiredLevel}, player is {playerLevel}");
            return false;
        }

        // Check if 2H weapon would be blocked by off-hand
        if (item.IsTwoHanded && item.Slot == EquipmentSlot.MainHand)
        {
            // Allow - will unequip off-hand automatically
        }

        // Check if equipping to off-hand while 2H is equipped
        if (item.Slot == EquipmentSlot.OffHand)
        {
            var mainHand = GetEquippedItem(EquipmentSlot.MainHand);
            if (mainHand != null && mainHand.IsTwoHanded)
            {
                // Would need to unequip 2H weapon first
                GD.Print($"[EquipmentManager] Cannot equip {item.Name} in off-hand: 2H weapon {mainHand.Name} is equipped");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Equip an item to its appropriate slot.
    /// Returns the previously equipped item (if any) to be returned to inventory.
    /// </summary>
    public EquipmentItem? Equip(EquipmentItem item)
    {
        if (!CanEquip(item))
        {
            return null;
        }

        var slot = item.Slot;
        var previousItem = _equippedItems[slot];

        // Handle 2H weapon: unequip off-hand
        EquipmentItem? offHandItem = null;
        if (item.IsTwoHanded && slot == EquipmentSlot.MainHand)
        {
            offHandItem = _equippedItems[EquipmentSlot.OffHand];
            if (offHandItem != null)
            {
                _equippedItems[EquipmentSlot.OffHand] = null;
                OnSlotChanged?.Invoke(EquipmentSlot.OffHand, null);
                GD.Print($"[EquipmentManager] Unequipped off-hand {offHandItem.Name} for 2H weapon");

                // Add to inventory
                Inventory3D.Instance?.AddItem(new InventoryItem(
                    offHandItem.Id, offHandItem.Name, offHandItem.Description,
                    ItemType.Equipment, 1));
            }
        }

        // Equip the new item
        _equippedItems[slot] = item;

        GD.Print($"[EquipmentManager] Equipped {item.Name} to {slot}");

        // Recalculate stats
        RecalculateStats();

        // Fire events
        OnSlotChanged?.Invoke(slot, item);
        OnEquipmentChanged?.Invoke();

        return previousItem;
    }

    /// <summary>
    /// Unequip an item from a slot.
    /// Returns the unequipped item to be returned to inventory.
    /// </summary>
    public EquipmentItem? Unequip(EquipmentSlot slot)
    {
        var item = _equippedItems[slot];
        if (item == null)
        {
            return null;
        }

        _equippedItems[slot] = null;

        GD.Print($"[EquipmentManager] Unequipped {item.Name} from {slot}");

        // Recalculate stats
        RecalculateStats();

        // Fire events
        OnSlotChanged?.Invoke(slot, null);
        OnEquipmentChanged?.Invoke();

        return item;
    }

    /// <summary>
    /// Swap items between two ring slots.
    /// </summary>
    public void SwapRings()
    {
        var ring1 = _equippedItems[EquipmentSlot.Ring1];
        var ring2 = _equippedItems[EquipmentSlot.Ring2];

        _equippedItems[EquipmentSlot.Ring1] = ring2;
        _equippedItems[EquipmentSlot.Ring2] = ring1;

        RecalculateStats();

        OnSlotChanged?.Invoke(EquipmentSlot.Ring1, ring2);
        OnSlotChanged?.Invoke(EquipmentSlot.Ring2, ring1);
        OnEquipmentChanged?.Invoke();
    }

    /// <summary>
    /// Get the total value of a stat across all equipped items.
    /// </summary>
    public float GetTotalAffixValue(AffixType type)
    {
        float total = 0;
        foreach (var kvp in _equippedItems)
        {
            if (kvp.Value != null)
            {
                total += kvp.Value.GetAffixTotal(type);
            }
        }
        return total;
    }

    /// <summary>
    /// Calculate set bonuses for equipped items.
    /// </summary>
    public Dictionary<string, int> GetEquippedSetPieces()
    {
        var setCounts = new Dictionary<string, int>();

        foreach (var kvp in _equippedItems)
        {
            if (kvp.Value?.SetId != null)
            {
                if (!setCounts.ContainsKey(kvp.Value.SetId))
                {
                    setCounts[kvp.Value.SetId] = 0;
                }
                setCounts[kvp.Value.SetId]++;
            }
        }

        return setCounts;
    }

    /// <summary>
    /// Recalculate all character stats based on equipped items.
    /// </summary>
    public void RecalculateStats()
    {
        if (_stats == null) return;

        // Clear previous equipment bonuses
        _stats.ClearEquipmentBonuses();

        // Apply all equipped items
        foreach (var kvp in _equippedItems)
        {
            if (kvp.Value != null)
            {
                _stats.ApplyEquipmentBonuses(kvp.Value);
            }
        }

        // TODO: Apply set bonuses here

        // Finalize calculations
        _stats.RecalculateAll();

        GD.Print($"[EquipmentManager] Stats recalculated:\n{_stats.GetStatsSummary()}");
    }

    /// <summary>
    /// Get display name for a slot.
    /// </summary>
    public static string GetSlotDisplayName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => "Head",
            EquipmentSlot.Chest => "Chest",
            EquipmentSlot.Hands => "Hands",
            EquipmentSlot.Feet => "Feet",
            EquipmentSlot.MainHand => "Main Hand",
            EquipmentSlot.OffHand => "Off Hand",
            EquipmentSlot.Ring1 => "Ring 1",
            EquipmentSlot.Ring2 => "Ring 2",
            _ => slot.ToString()
        };
    }

    /// <summary>
    /// Check if off-hand is blocked by a 2H weapon.
    /// </summary>
    public bool IsOffHandBlocked()
    {
        var mainHand = GetEquippedItem(EquipmentSlot.MainHand);
        return mainHand != null && mainHand.IsTwoHanded;
    }

    /// <summary>
    /// Get combined armor value from all equipped armor pieces.
    /// </summary>
    public int GetTotalArmor()
    {
        int total = 0;
        foreach (var kvp in _equippedItems)
        {
            if (kvp.Value != null)
            {
                total += kvp.Value.Armor;
            }
        }
        return total;
    }

    /// <summary>
    /// Get average item level of equipped gear.
    /// </summary>
    public float GetAverageItemLevel()
    {
        int count = 0;
        int total = 0;

        foreach (var kvp in _equippedItems)
        {
            if (kvp.Value != null)
            {
                total += kvp.Value.ItemLevel;
                count++;
            }
        }

        return count > 0 ? (float)total / count : 0;
    }

    /// <summary>
    /// Get number of equipped items.
    /// </summary>
    public int GetEquippedCount()
    {
        int count = 0;
        foreach (var kvp in _equippedItems)
        {
            if (kvp.Value != null) count++;
        }
        return count;
    }

    /// <summary>
    /// Create and equip a full set of Normal (gray) starter equipment.
    /// Used for new characters.
    /// </summary>
    public void EquipStarterGear()
    {
        GD.Print("[EquipmentManager] Equipping starter gear...");

        // Head - Worn Leather Cap
        var helm = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Worn Leather Cap",
            Description = "A simple leather cap offering minimal protection.",
            Slot = EquipmentSlot.Head,
            Rarity = ItemRarity.Normal,
            ArmorType = ArmorType.Leather,
            ItemLevel = 1,
            RequiredLevel = 1,
            Armor = 3
        };
        _equippedItems[EquipmentSlot.Head] = helm;

        // Chest - Tattered Tunic
        var chest = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Tattered Tunic",
            Description = "A worn cloth tunic with a few patches.",
            Slot = EquipmentSlot.Chest,
            Rarity = ItemRarity.Normal,
            ArmorType = ArmorType.Cloth,
            ItemLevel = 1,
            RequiredLevel = 1,
            Armor = 5
        };
        _equippedItems[EquipmentSlot.Chest] = chest;

        // Hands - Frayed Gloves
        var hands = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Frayed Gloves",
            Description = "Simple cloth gloves, worn thin at the fingers.",
            Slot = EquipmentSlot.Hands,
            Rarity = ItemRarity.Normal,
            ArmorType = ArmorType.Cloth,
            ItemLevel = 1,
            RequiredLevel = 1,
            Armor = 2
        };
        _equippedItems[EquipmentSlot.Hands] = hands;

        // Feet - Scuffed Boots
        var feet = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Scuffed Boots",
            Description = "Well-worn leather boots with thin soles.",
            Slot = EquipmentSlot.Feet,
            Rarity = ItemRarity.Normal,
            ArmorType = ArmorType.Leather,
            ItemLevel = 1,
            RequiredLevel = 1,
            Armor = 3
        };
        _equippedItems[EquipmentSlot.Feet] = feet;

        // Main Hand - Rusty Short Sword
        var weapon = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Rusty Short Sword",
            Description = "A dull blade with spots of rust. Better than nothing.",
            Slot = EquipmentSlot.MainHand,
            Rarity = ItemRarity.Normal,
            WeaponType = WeaponType.ShortSword,
            ItemLevel = 1,
            RequiredLevel = 1,
            MinDamage = 3,
            MaxDamage = 7,
            AttackSpeed = 1.0f,
            WeaponRange = 2.5f,
            IsTwoHanded = false
        };
        _equippedItems[EquipmentSlot.MainHand] = weapon;

        // Off Hand - Battered Wooden Shield
        var shield = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Battered Wooden Shield",
            Description = "A wooden shield with several dents and scratches.",
            Slot = EquipmentSlot.OffHand,
            Rarity = ItemRarity.Normal,
            ArmorType = ArmorType.None,
            ItemLevel = 1,
            RequiredLevel = 1,
            Armor = 4,
            BlockChance = 10,
            BlockAmount = 5
        };
        _equippedItems[EquipmentSlot.OffHand] = shield;

        // Ring 1 - Dull Copper Ring
        var ring1 = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Dull Copper Ring",
            Description = "A simple copper ring, tarnished with age.",
            Slot = EquipmentSlot.Ring1,
            Rarity = ItemRarity.Normal,
            ItemLevel = 1,
            RequiredLevel = 1
        };
        _equippedItems[EquipmentSlot.Ring1] = ring1;

        // Ring 2 - Worn Silver Band
        var ring2 = new EquipmentItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Worn Silver Band",
            Description = "A thin silver band, scratched and worn.",
            Slot = EquipmentSlot.Ring2,
            Rarity = ItemRarity.Normal,
            ItemLevel = 1,
            RequiredLevel = 1
        };
        _equippedItems[EquipmentSlot.Ring2] = ring2;

        // Fire slot changed events for all slots
        foreach (var slot in _equippedItems.Keys)
        {
            OnSlotChanged?.Invoke(slot, _equippedItems[slot]);
        }

        // Recalculate stats with new gear
        RecalculateStats();

        OnEquipmentChanged?.Invoke();

        GD.Print("[EquipmentManager] Starter gear equipped: 8 items");
    }
}
