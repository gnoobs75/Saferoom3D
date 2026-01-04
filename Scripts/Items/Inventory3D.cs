using Godot;
using System;
using System.Collections.Generic;
using SafeRoom3D.Core;

namespace SafeRoom3D.Items;

/// <summary>
/// Represents an item that can be stored in the inventory.
/// </summary>
public class InventoryItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int StackCount { get; set; }
    public int MaxStackSize { get; set; }
    public ItemType Type { get; set; }

    // For potions
    public int HealAmount { get; set; }
    public int ManaAmount { get; set; }

    // For equipment items - null if not equipment
    public EquipmentItem? Equipment { get; set; }

    public InventoryItem(string id, string name, string description, ItemType type, int maxStack = 99)
    {
        Id = id;
        Name = name;
        Description = description;
        Type = type;
        MaxStackSize = maxStack;
        StackCount = 1;
    }

    /// <summary>
    /// Create an InventoryItem wrapper for an EquipmentItem.
    /// Equipment items don't stack (maxStack = 1).
    /// </summary>
    public static InventoryItem FromEquipment(EquipmentItem equipment)
    {
        var item = new InventoryItem(
            equipment.Id,
            equipment.Name,
            equipment.Description,
            ItemType.Equipment,
            1  // Equipment doesn't stack
        )
        {
            Equipment = equipment
        };
        return item;
    }

    public InventoryItem Clone()
    {
        var clone = new InventoryItem(Id, Name, Description, Type, MaxStackSize)
        {
            StackCount = StackCount,
            HealAmount = HealAmount,
            ManaAmount = ManaAmount,
            Equipment = Equipment // Equipment reference is shared (items are unique)
        };
        return clone;
    }

    public bool CanStackWith(InventoryItem other)
    {
        // Equipment items never stack
        if (Type == ItemType.Equipment || other?.Type == ItemType.Equipment)
            return false;

        return other != null && Id == other.Id && StackCount < MaxStackSize;
    }

    /// <summary>
    /// Get the display color for this item based on rarity.
    /// </summary>
    public Color GetRarityColor()
    {
        if (Equipment == null)
            return new Color(0.8f, 0.8f, 0.8f); // White/gray for non-equipment

        return Equipment.Rarity switch
        {
            ItemRarity.Normal => new Color(0.8f, 0.8f, 0.8f),    // White
            ItemRarity.Magic => new Color(0.4f, 0.6f, 1.0f),     // Blue
            ItemRarity.Rare => new Color(1.0f, 1.0f, 0.4f),      // Yellow
            ItemRarity.Unique => new Color(0.8f, 0.5f, 0.2f),    // Orange
            ItemRarity.Set => new Color(0.3f, 1.0f, 0.3f),       // Green
            _ => new Color(0.8f, 0.8f, 0.8f)
        };
    }
}

public enum ItemType
{
    Consumable,
    Equipment,
    Material,
    Quest
}

/// <summary>
/// Manages the player's inventory with a 5x5 grid.
/// </summary>
public partial class Inventory3D : Node
{
    public static Inventory3D? Instance { get; private set; }

    public const int GridWidth = 5;
    public const int GridHeight = 5;
    public const int TotalSlots = GridWidth * GridHeight;

    // The inventory grid (null = empty slot)
    private InventoryItem?[] _items = new InventoryItem?[TotalSlots];

    // Signals
    [Signal] public delegate void InventoryChangedEventHandler();
    [Signal] public delegate void ItemUsedEventHandler(string itemId, int slotIndex);

    public override void _Ready()
    {
        Instance = this;
        AddStartingItems();
        GD.Print("[Inventory3D] Ready - 5x5 grid initialized");
    }

    private void AddStartingItems()
    {
        // Add 5 health potions
        var healthPotion = ItemDatabase.CreateHealthPotion();
        healthPotion.StackCount = 5;
        AddItem(healthPotion);

        // Add 5 mana potions
        var manaPotion = ItemDatabase.CreateManaPotion();
        manaPotion.StackCount = 5;
        AddItem(manaPotion);

        // Add 3 of each special consumable
        var grenade = ItemDatabase.CreateViewersChoiceGrenade();
        grenade.StackCount = 3;
        AddItem(grenade);

        var courage = ItemDatabase.CreateLiquidCourage();
        courage.StackCount = 3;
        AddItem(courage);

        var beacon = ItemDatabase.CreateRecallBeacon();
        beacon.StackCount = 3;
        AddItem(beacon);

        var musk = ItemDatabase.CreateMonsterMusk();
        musk.StackCount = 3;
        AddItem(musk);

        GD.Print("[Inventory3D] Starting items added");
    }

    /// <summary>
    /// Get item at specific slot index (0-24)
    /// </summary>
    public InventoryItem? GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return null;
        return _items[slotIndex];
    }

    /// <summary>
    /// Get item at grid position (x, y)
    /// </summary>
    public InventoryItem? GetItemAt(int x, int y)
    {
        if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return null;
        return _items[y * GridWidth + x];
    }

    /// <summary>
    /// Set item at specific slot index
    /// </summary>
    public void SetItem(int slotIndex, InventoryItem? item)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return;
        _items[slotIndex] = item;
        EmitSignal(SignalName.InventoryChanged);
    }

    /// <summary>
    /// Add an item to the first available slot (or stack with existing)
    /// </summary>
    public bool AddItem(InventoryItem item)
    {
        // First, try to stack with existing items of same type
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_items[i] != null && _items[i]!.CanStackWith(item))
            {
                int spaceAvailable = _items[i]!.MaxStackSize - _items[i]!.StackCount;
                int toAdd = Math.Min(spaceAvailable, item.StackCount);
                _items[i]!.StackCount += toAdd;
                item.StackCount -= toAdd;

                if (item.StackCount <= 0)
                {
                    EmitSignal(SignalName.InventoryChanged);
                    return true;
                }
            }
        }

        // Then, find an empty slot for remaining items
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_items[i] == null)
            {
                _items[i] = item.Clone();
                _items[i]!.StackCount = item.StackCount;
                EmitSignal(SignalName.InventoryChanged);
                return true;
            }
        }

        // Inventory full
        return false;
    }

    /// <summary>
    /// Use one item from a stack at the specified slot
    /// </summary>
    public bool UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return false;

        var item = _items[slotIndex];
        if (item == null) return false;

        // Emit signal before removing
        EmitSignal(SignalName.ItemUsed, item.Id, slotIndex);

        // Decrease stack count
        item.StackCount--;
        if (item.StackCount <= 0)
        {
            _items[slotIndex] = null;
        }

        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    /// <summary>
    /// Find the first slot containing an item with the given ID
    /// </summary>
    public int FindItemSlot(string itemId)
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_items[i]?.Id == itemId)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Get total count of an item type across all stacks
    /// </summary>
    public int GetItemCount(string itemId)
    {
        int count = 0;
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_items[i]?.Id == itemId)
                count += _items[i]!.StackCount;
        }
        return count;
    }

    /// <summary>
    /// Alias for GetItemCount - used by hotbar consumable display.
    /// </summary>
    public int CountItem(string itemId) => GetItemCount(itemId);

    /// <summary>
    /// Use one item with the given ID from anywhere in the inventory.
    /// Returns true if an item was used.
    /// </summary>
    public bool UseItemById(string itemId)
    {
        int slot = FindItemSlot(itemId);
        if (slot < 0) return false;
        return UseItem(slot);
    }

    /// <summary>
    /// Check if inventory has at least one of the specified item
    /// </summary>
    public bool HasItem(string itemId)
    {
        return FindItemSlot(itemId) >= 0;
    }

    /// <summary>
    /// Swap items between two slots
    /// </summary>
    public void SwapItems(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= TotalSlots || slotB < 0 || slotB >= TotalSlots)
            return;

        (_items[slotA], _items[slotB]) = (_items[slotB], _items[slotA]);
        EmitSignal(SignalName.InventoryChanged);
    }

    /// <summary>
    /// Get all non-null items
    /// </summary>
    public IEnumerable<(int slot, InventoryItem item)> GetAllItems()
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_items[i] != null)
                yield return (i, _items[i]!);
        }
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}
