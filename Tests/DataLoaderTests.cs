using SafeRoom3D.Core;

namespace SafeRoom3D.Tests;

/// <summary>
/// Unit tests for the DataLoader (JSON data loading).
/// </summary>
public class DataLoaderTests
{
    public void TestDataLoaderInitializes()
    {
        // Should not throw
        DataLoader.Initialize();

        // Should be able to get data after initialization
        var monsters = DataLoader.GetAllMonsters();
        Assert.IsNotNull(monsters);
    }

    public void TestGetMonster()
    {
        DataLoader.Initialize();

        var goblin = DataLoader.GetMonster("goblin");

        Assert.IsNotNull(goblin, "Goblin should exist in data");
        Assert.AreEqual("goblin", goblin!.Id);
        Assert.IsGreaterThan(goblin.MaxHealth, 0);
        Assert.IsGreaterThan(goblin.Damage, 0);
    }

    public void TestGetMonsterCaseInsensitive()
    {
        DataLoader.Initialize();

        var goblin1 = DataLoader.GetMonster("GOBLIN");
        var goblin2 = DataLoader.GetMonster("Goblin");
        var goblin3 = DataLoader.GetMonster("goblin");

        Assert.IsNotNull(goblin1);
        Assert.IsNotNull(goblin2);
        Assert.IsNotNull(goblin3);
    }

    public void TestGetNonExistentMonster()
    {
        DataLoader.Initialize();

        var monster = DataLoader.GetMonster("nonexistent_monster_xyz");

        Assert.IsNull(monster, "Non-existent monster should return null");
    }

    public void TestGetBoss()
    {
        DataLoader.Initialize();

        var skeletonLord = DataLoader.GetBoss("skeleton_lord");

        Assert.IsNotNull(skeletonLord, "Skeleton Lord boss should exist");
        Assert.IsGreaterThan(skeletonLord!.MaxHealth, 100); // Bosses have high HP
    }

    public void TestGetAllMonsters()
    {
        DataLoader.Initialize();

        var monsters = DataLoader.GetAllMonsters();

        Assert.IsNotNull(monsters);
        Assert.IsGreaterThan(monsters.Count, 0);
    }

    public void TestGetAllBosses()
    {
        DataLoader.Initialize();

        var bosses = DataLoader.GetAllBosses();

        Assert.IsNotNull(bosses);
        Assert.IsGreaterThan(bosses.Count, 0);
    }

    public void TestMonsterDefaults()
    {
        DataLoader.Initialize();

        var defaults = DataLoader.GetMonsterDefaults();

        Assert.IsNotNull(defaults);
        Assert.IsGreaterThan(defaults.AttackRange, 0);
        Assert.IsGreaterThan(defaults.AggroRange, 0);
    }

    public void TestLevelScaling()
    {
        DataLoader.Initialize();

        var scaling = DataLoader.GetLevelScaling();

        Assert.IsNotNull(scaling);
        Assert.IsGreaterThan(scaling.HealthMultiplierPerLevel, 0);
        Assert.IsGreaterThan(scaling.DamageMultiplierPerLevel, 0);
    }

    public void TestMonsterColorConversion()
    {
        DataLoader.Initialize();

        var slime = DataLoader.GetMonster("slime");
        Assert.IsNotNull(slime);

        var color = slime!.GetGodotColor();

        // Color should have valid values
        Assert.IsGreaterThan(color.R, -0.01f);
        Assert.IsLessThan(color.R, 1.01f);
        Assert.IsGreaterThan(color.G, -0.01f);
        Assert.IsLessThan(color.G, 1.01f);
    }

    public void TestGetAbility()
    {
        DataLoader.Initialize();

        var fireball = DataLoader.GetAbility("fireball");

        Assert.IsNotNull(fireball, "Fireball ability should exist");
        Assert.AreEqual("fireball", fireball!.Id);
        Assert.IsGreaterThan(fireball.Cooldown, 0);
    }

    public void TestGetAllAbilities()
    {
        DataLoader.Initialize();

        var abilities = DataLoader.GetAllAbilities();

        Assert.IsNotNull(abilities);
        Assert.IsGreaterThan(abilities.Count, 0);
    }

    public void TestGetConsumable()
    {
        DataLoader.Initialize();

        var healthPotion = DataLoader.GetConsumable("health_potion");

        Assert.IsNotNull(healthPotion, "Health potion should exist");
        Assert.IsGreaterThan(healthPotion!.HealAmount, 0);
    }

    public void TestReload()
    {
        DataLoader.Initialize();

        // Should not throw
        DataLoader.Reload();

        // Data should still be available
        var monsters = DataLoader.GetAllMonsters();
        Assert.IsGreaterThan(monsters.Count, 0);
    }
}
