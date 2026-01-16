using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.Pet;

namespace SafeRoom3D.Items;

/// <summary>
/// Handles using consumable items in the 3D game.
/// </summary>
public static class ConsumableItems3D
{
    /// <summary>
    /// Use an item by type name. Returns true if successfully used.
    /// </summary>
    public static bool UseItem(string itemType, FPSController player)
    {
        // Handle monster meat items (pattern: {monstertype}_meat)
        if (itemType.EndsWith("_meat"))
        {
            return UseMonsterMeat(itemType, player);
        }

        return itemType switch
        {
            "health_potion" => UseHealthPotion(player),
            "mana_potion" => UseManaPotion(player),
            "viewers_choice_grenade" => UseViewersChoiceGrenade(player),
            "liquid_courage" => UseLiquidCourage(player),
            "recall_beacon" => UseRecallBeacon(player),
            "monster_musk" => UseMonsterMusk(player),
            _ => false
        };
    }

    private static bool UseHealthPotion(FPSController player)
    {
        if (player.CurrentHealth >= player.MaxHealth)
        {
            GD.Print("[ConsumableItems3D] Already at full health!");
            return false;
        }

        player.Heal(100);
        SoundManager3D.Instance?.PlayHealSound(player.GlobalPosition);
        GD.Print("[ConsumableItems3D] Used Health Potion - healed 100 HP");
        return true;
    }

    private static bool UseManaPotion(FPSController player)
    {
        if (player.CurrentMana >= player.MaxMana)
        {
            GD.Print("[ConsumableItems3D] Already at full mana!");
            return false;
        }

        player.RestoreMana(50);
        SoundManager3D.Instance?.PlayMagicSound(player.GlobalPosition);
        GD.Print("[ConsumableItems3D] Used Mana Potion - restored 50 MP");
        return true;
    }

    private static bool UseViewersChoiceGrenade(FPSController player)
    {
        GD.Print("[ConsumableItems3D] Viewer's Choice Grenade thrown!");

        // Random effect
        int roll = (int)(GD.Randi() % 4);

        switch (roll)
        {
            case 0: // Massive explosion
                DoMassiveExplosion(player);
                break;
            case 1: // Heal bomb
                DoHealBomb(player);
                break;
            case 2: // Confetti
                DoConfetti(player);
                break;
            case 3: // Backfire
                DoBackfire(player);
                break;
        }

        return true;
    }

    private static void DoMassiveExplosion(FPSController player)
    {
        GD.Print("[ViewersChoiceGrenade] MASSIVE EXPLOSION!");

        float damage = 200f;
        float radius = 15f;
        Vector3 center = player.GlobalPosition + player.GetLookDirection() * 5f;

        // Damage all enemies in radius
        var enemies = player.GetTree().GetNodesInGroup("Enemies");
        int hitCount = 0;

        foreach (var node in enemies)
        {
            if (node is Node3D enemy && IsInstanceValid(enemy))
            {
                if (enemy.GlobalPosition.DistanceTo(center) <= radius)
                {
                    if (enemy.HasMethod("TakeDamage"))
                    {
                        enemy.Call("TakeDamage", damage, center, "Viewer's Choice BOOM");
                        hitCount++;
                    }
                }
            }
        }

        SoundManager3D.Instance?.PlayExplosionSound(center);
        player.RequestScreenShake(0.3f, 0.3f);
        GD.Print($"[ViewersChoiceGrenade] Hit {hitCount} enemies for {damage} damage!");
    }

    private static void DoHealBomb(FPSController player)
    {
        GD.Print("[ViewersChoiceGrenade] HEAL BOMB!");
        player.Heal(100);
        SoundManager3D.Instance?.PlayHealSound(player.GlobalPosition);
    }

    private static void DoConfetti(FPSController player)
    {
        GD.Print("[ViewersChoiceGrenade] CONFETTI! ...That's it. Just confetti.");
        // Visual effect only
    }

    private static void DoBackfire(FPSController player)
    {
        GD.Print("[ViewersChoiceGrenade] BACKFIRE! You take 30 damage!");
        player.TakeDamage(30, player.GlobalPosition);
    }

    private static bool UseLiquidCourage(FPSController player)
    {
        GD.Print("[ConsumableItems3D] Liquid Courage - Invulnerable for 3 seconds!");

        // Create buff effect
        var buff = new LiquidCourageBuff3D();
        buff.Initialize(player);
        player.GetTree().Root.AddChild(buff);

        SoundManager3D.Instance?.PlayMagicSound(player.GlobalPosition);
        player.RequestScreenShake(0.1f, 0.1f);

        return true;
    }

    private static bool UseRecallBeacon(FPSController player)
    {
        GD.Print("[ConsumableItems3D] Recall Beacon - Teleporting to center!");

        // Teleport to center of arena
        var dungeon = GameManager3D.Instance?.GetDungeon();
        Vector3 targetPos = dungeon?.GetSpawnPosition() ?? Vector3.Zero;

        player.TeleportTo(targetPos + new Vector3(0, 0.5f, 0));
        SoundManager3D.Instance?.PlayMagicSound(targetPos);
        player.RequestScreenShake(0.2f, 0.2f);

        return true;
    }

    private static bool UseMonsterMusk(FPSController player)
    {
        GD.Print("[ConsumableItems3D] Monster Musk - Attracting enemies for 10 seconds!");

        // Create musk effect
        var musk = new MonsterMuskEffect3D();
        musk.Initialize(player);
        player.GetTree().Root.AddChild(musk);

        SoundManager3D.Instance?.PlayMagicSound(player.GlobalPosition);

        return true;
    }

    /// <summary>
    /// Feed monster meat to Steve to transform him into that monster type.
    /// </summary>
    private static bool UseMonsterMeat(string meatId, FPSController player)
    {
        // Extract monster type from meat ID (e.g., "goblin_meat" -> "goblin")
        string monsterType = meatId[..^5]; // Remove "_meat" suffix

        // Check if Steve exists
        if (Steve3D.Instance == null)
        {
            GD.Print("[ConsumableItems3D] Steve not found - cannot use monster meat");
            UI.HUD3D.Instance?.AddCombatLogMessage(
                "[color=#ff6666]Steve is not with you![/color]",
                new Color(1f, 0.4f, 0.4f));
            return false;
        }

        // Cancel existing transformation if any
        SteveTransformEffect.Active?.Expire();

        // Create new transformation effect
        var effect = new SteveTransformEffect();
        player.GetTree().Root.AddChild(effect);
        effect.Initialize(monsterType);

        // Play sound and notify player
        SoundManager3D.Instance?.PlayMagicSound(player.GlobalPosition);
        player.RequestScreenShake(0.15f, 0.15f);

        string displayName = FormatMonsterName(monsterType);
        UI.HUD3D.Instance?.AddCombatLogMessage(
            $"[color=#aaffaa]Steve transforms into a {displayName}![/color]",
            new Color(0.67f, 1f, 0.67f));

        GD.Print($"[ConsumableItems3D] Used {meatId} - Steve transformed into {monsterType}");
        return true;
    }

    /// <summary>
    /// Format monster type for display
    /// </summary>
    private static string FormatMonsterName(string monsterType)
    {
        var words = monsterType.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
        }
        return string.Join(" ", words);
    }

    private static bool IsInstanceValid(GodotObject obj)
    {
        return GodotObject.IsInstanceValid(obj);
    }
}

/// <summary>
/// Liquid Courage buff - invulnerability for 3 seconds.
/// </summary>
public partial class LiquidCourageBuff3D : Node3D
{
    public static LiquidCourageBuff3D? ActiveBuff { get; private set; }

    private FPSController? _player;
    private float _duration = 3f;
    private float _lifetime;

    public void Initialize(FPSController player)
    {
        _player = player;
        ActiveBuff = this;
    }

    public override void _Process(double delta)
    {
        _lifetime += (float)delta;

        if (_lifetime >= _duration)
        {
            Expire();
        }
    }

    private void Expire()
    {
        ActiveBuff = null;
        GD.Print("[LiquidCourageBuff3D] Invulnerability wears off.");
        QueueFree();
    }

    public static bool IsPlayerInvulnerable()
    {
        return ActiveBuff != null;
    }
}

/// <summary>
/// Monster Musk effect - attracts enemies for 10 seconds.
/// </summary>
public partial class MonsterMuskEffect3D : Node3D
{
    public static MonsterMuskEffect3D? ActiveMusk { get; private set; }

    private FPSController? _player;
    private float _duration = 10f;
    private float _lifetime;

    public void Initialize(FPSController player)
    {
        _player = player;
        ActiveMusk = this;
    }

    public override void _Process(double delta)
    {
        _lifetime += (float)delta;

        if (_lifetime >= _duration)
        {
            Expire();
        }
    }

    private void Expire()
    {
        ActiveMusk = null;
        GD.Print("[MonsterMuskEffect3D] Enemies lose interest.");
        QueueFree();
    }

    public static bool IsPlayerAttracting()
    {
        return ActiveMusk != null;
    }
}
