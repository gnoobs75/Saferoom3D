using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Core;
using SafeRoom3D.Items;

namespace SafeRoom3D.NPC.AI;

/// <summary>
/// Inventory system for Crawler NPCs.
/// Simplified version of player inventory - same 5x5 grid but no starting items.
/// Crawlers collect loot and sell it at Bopca's shop.
/// </summary>
public class CrawlerInventory
{
    public const int GridWidth = 5;
    public const int GridHeight = 5;
    public const int TotalSlots = GridWidth * GridHeight;

    private readonly InventoryItem?[] _items = new InventoryItem?[TotalSlots];
    private int _gold = 0;
    private readonly string _ownerName;

    /// <summary>
    /// Total gold collected by this Crawler.
    /// </summary>
    public int Gold => _gold;

    /// <summary>
    /// Total value of all loot (for decision-making).
    /// </summary>
    public int TotalLootValue => _items
        .Where(item => item != null)
        .Sum(item => EstimateItemValue(item!));

    public CrawlerInventory(string ownerName)
    {
        _ownerName = ownerName;
    }

    /// <summary>
    /// Get the percentage of inventory slots that are occupied (0-1).
    /// </summary>
    public float GetFullnessPercent()
    {
        int occupied = _items.Count(item => item != null);
        return (float)occupied / TotalSlots;
    }

    /// <summary>
    /// Check if inventory has space for at least one more item.
    /// </summary>
    public bool HasSpace()
    {
        return _items.Any(item => item == null);
    }

    /// <summary>
    /// Check if inventory has any items to sell.
    /// </summary>
    public bool HasItemsToSell()
    {
        return _items.Any(item => item != null);
    }

    /// <summary>
    /// Add an item to the inventory. Returns true if successful.
    /// </summary>
    public bool AddItem(InventoryItem item)
    {
        if (item == null) return false;

        // First, try to stack with existing items
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_items[i] != null && _items[i]!.CanStackWith(item))
            {
                int spaceAvailable = _items[i]!.MaxStackSize - _items[i]!.StackCount;
                int toAdd = Math.Min(spaceAvailable, item.StackCount);
                _items[i]!.StackCount += toAdd;
                item.StackCount -= toAdd;

                if (item.StackCount <= 0)
                    return true;
            }
        }

        // If we still have items to add, find empty slot
        if (item.StackCount > 0)
        {
            for (int i = 0; i < TotalSlots; i++)
            {
                if (_items[i] == null)
                {
                    _items[i] = item.Clone();
                    return true;
                }
            }

            // No space - inventory full
            return false;
        }

        return true;
    }

    /// <summary>
    /// Remove an item from a specific slot.
    /// </summary>
    public bool RemoveItem(int slotIndex, int count = 1)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return false;
        var item = _items[slotIndex];
        if (item == null) return false;

        if (item.StackCount <= count)
        {
            _items[slotIndex] = null;
        }
        else
        {
            item.StackCount -= count;
        }

        return true;
    }

    /// <summary>
    /// Get item at a specific slot.
    /// </summary>
    public InventoryItem? GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return null;
        return _items[slotIndex];
    }

    /// <summary>
    /// Get all items with their slot indices.
    /// </summary>
    public IEnumerable<(int slot, InventoryItem item)> GetAllItems()
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_items[i] != null)
                yield return (i, _items[i]!);
        }
    }

    /// <summary>
    /// Add gold to the inventory.
    /// </summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        _gold += amount;
        GD.Print($"[{_ownerName}] +{amount}g (total: {_gold}g)");
    }

    /// <summary>
    /// Spend gold from the inventory.
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (_gold < amount) return false;

        _gold -= amount;
        return true;
    }

    /// <summary>
    /// Sell all items and add gold. Returns total gold earned.
    /// </summary>
    public int SellAllItems()
    {
        int totalEarned = 0;

        for (int i = 0; i < TotalSlots; i++)
        {
            var item = _items[i];
            if (item == null) continue;

            int sellPrice = CalculateSellPrice(item);
            totalEarned += sellPrice * item.StackCount;
            _items[i] = null;
        }

        _gold += totalEarned;
        GD.Print($"[{_ownerName}] Sold items for {totalEarned}g (total: {_gold}g)");
        return totalEarned;
    }

    /// <summary>
    /// Calculate sell price for an item (50% of estimated value).
    /// </summary>
    public static int CalculateSellPrice(InventoryItem item)
    {
        if (item.Equipment != null)
        {
            // Equipment: based on rarity and level
            int baseValue = item.Equipment.ItemLevel * 10;
            float rarityMult = item.Equipment.Rarity switch
            {
                ItemRarity.Normal => 0.3f,
                ItemRarity.Magic => 0.5f,
                ItemRarity.Rare => 0.7f,
                ItemRarity.Unique => 1.0f,
                ItemRarity.Set => 1.0f,
                _ => 0.3f
            };
            return (int)(baseValue * rarityMult);
        }

        // Consumables and materials
        return item.Id switch
        {
            "health_potion" => 10,
            "mana_potion" => 12,
            "viewers_choice_grenade" => 50,
            "liquid_courage" => 75,
            "recall_beacon" => 40,
            "monster_musk" => 30,
            "gemstone" => 100,
            "ancient_relic" => 150,
            "dragon_scale" => 200,
            "enchanted_dust" => 25,
            _ => item.Id.Contains("_part") ? 15 : 5  // Monster parts worth more
        };
    }

    /// <summary>
    /// Estimate value of an item for loot priority decisions.
    /// </summary>
    public static int EstimateItemValue(InventoryItem item)
    {
        if (item.Equipment != null)
        {
            return item.Equipment.Rarity switch
            {
                ItemRarity.Normal => 10,
                ItemRarity.Magic => 50,
                ItemRarity.Rare => 150,
                ItemRarity.Unique => 500,
                ItemRarity.Set => 500,
                _ => 10
            };
        }

        return item.Id switch
        {
            "health_potion" => 20,
            "mana_potion" => 25,
            "gemstone" => 200,
            "ancient_relic" => 300,
            "dragon_scale" => 400,
            "enchanted_dust" => 50,
            _ => item.Id.Contains("_part") ? 30 : 10
        };
    }

    /// <summary>
    /// Get number of occupied slots.
    /// </summary>
    public int GetOccupiedSlotCount()
    {
        return _items.Count(item => item != null);
    }

    /// <summary>
    /// Get number of empty slots.
    /// </summary>
    public int GetEmptySlotCount()
    {
        return TotalSlots - GetOccupiedSlotCount();
    }

    /// <summary>
    /// Clear all items (used on death - items are NOT transferred).
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < TotalSlots; i++)
            _items[i] = null;
        _gold = 0;
    }
}
