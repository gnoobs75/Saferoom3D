using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;

namespace SafeRoom3D.Items;

/// <summary>
/// A container that holds loot items. Used by corpses and treasure chests.
/// </summary>
public class LootBag
{
    public const int DefaultSlots = 8;

    private InventoryItem?[] _items;
    public int SlotCount => _items.Length;

    // Store monster level for equipment generation
    public int MonsterLevel { get; set; } = 1;

    // Gold amount to be picked up directly (not as inventory item)
    public int GoldAmount { get; set; } = 0;

    public LootBag(int slots = DefaultSlots)
    {
        _items = new InventoryItem?[slots];
    }

    /// <summary>
    /// Get item at slot index
    /// </summary>
    public InventoryItem? GetItem(int slot)
    {
        if (slot < 0 || slot >= _items.Length) return null;
        return _items[slot];
    }

    /// <summary>
    /// Set item at slot index
    /// </summary>
    public void SetItem(int slot, InventoryItem? item)
    {
        if (slot < 0 || slot >= _items.Length) return;
        _items[slot] = item;
    }

    /// <summary>
    /// Add an item to the first available slot
    /// </summary>
    public bool AddItem(InventoryItem item)
    {
        // Try to stack first
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] != null && _items[i]!.CanStackWith(item))
            {
                int space = _items[i]!.MaxStackSize - _items[i]!.StackCount;
                int toAdd = System.Math.Min(space, item.StackCount);
                _items[i]!.StackCount += toAdd;
                item.StackCount -= toAdd;
                if (item.StackCount <= 0) return true;
            }
        }

        // Find empty slot
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] == null)
            {
                _items[i] = item.Clone();
                _items[i]!.StackCount = item.StackCount;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Remove item from slot and return it
    /// </summary>
    public InventoryItem? TakeItem(int slot)
    {
        if (slot < 0 || slot >= _items.Length) return null;
        var item = _items[slot];
        _items[slot] = null;
        return item;
    }

    /// <summary>
    /// Check if loot bag is empty
    /// </summary>
    public bool IsEmpty()
    {
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] != null) return false;
        }
        return true;
    }

    /// <summary>
    /// Get all non-null items
    /// </summary>
    public IEnumerable<(int slot, InventoryItem item)> GetAllItems()
    {
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] != null)
                yield return (i, _items[i]!);
        }
    }

    /// <summary>
    /// Generate random loot for a monster
    /// </summary>
    public static LootBag GenerateMonsterLoot(string monsterType, bool isBoss, int monsterLevel = 1)
    {
        var bag = new LootBag();
        bag.MonsterLevel = monsterLevel;

        // Boss enemies always drop better loot
        int itemCount = isBoss ? GD.RandRange(3, 6) : GD.RandRange(1, 3);
        int goldAmount = isBoss ? GD.RandRange(50, 200) : GD.RandRange(5, 30);

        // Scale gold with monster level
        goldAmount = (int)(goldAmount * (1f + monsterLevel * 0.1f));

        // Store gold amount for direct pickup (not as inventory item)
        bag.GoldAmount = goldAmount;

        // Add random items
        for (int i = 0; i < itemCount; i++)
        {
            var item = GetRandomLootItem(isBoss);
            if (item != null)
            {
                bag.AddItem(item);
            }
        }

        // Roll for equipment drop
        float magicFind = 0; // TODO: Get from player stats when available
        if (LootGenerator.ShouldDropEquipment(monsterLevel, isBoss))
        {
            // Bosses can drop up to 3 equipment pieces
            int equipCount = isBoss ? GD.RandRange(1, 3) : 1;
            for (int i = 0; i < equipCount; i++)
            {
                var equipment = LootGenerator.GenerateEquipment(monsterLevel, magicFind);
                if (equipment != null)
                {
                    var equipItem = InventoryItem.FromEquipment(equipment);
                    bag.AddItem(equipItem);
                    GD.Print($"[LootBag] Generated equipment: {equipment.Name} ({equipment.Rarity})");
                }
            }
        }

        return bag;
    }

    private static InventoryItem? GetRandomLootItem(bool isBoss)
    {
        // Weight table for loot
        int roll = GD.RandRange(0, 100);

        if (isBoss)
        {
            // Boss has better drops
            if (roll < 20) return ItemDatabase.CreateHealthPotion();
            if (roll < 35) return ItemDatabase.CreateManaPotion();
            if (roll < 45) return ItemDatabase.CreateGemstone();
            if (roll < 55) return ItemDatabase.CreateAncientRelic();
            if (roll < 65) return ItemDatabase.CreateDragonScale();
            if (roll < 75) return ItemDatabase.CreateEnchantedDust();
            if (roll < 85) return ItemDatabase.CreateMysteryMeat();
            return ItemDatabase.CreateGoblinTooth();
        }
        else
        {
            // Regular monster loot
            if (roll < 25) return ItemDatabase.CreateHealthPotion();
            if (roll < 40) return ItemDatabase.CreateManaPotion();
            if (roll < 50) return ItemDatabase.CreateMysteryMeat();
            if (roll < 60) return ItemDatabase.CreateGoblinTooth();
            if (roll < 70) return ItemDatabase.CreateRatTail();
            if (roll < 80) return ItemDatabase.CreateBrokenArrow();
            if (roll < 90) return ItemDatabase.CreateEnchantedDust();
            return null; // No additional item
        }
    }
}
