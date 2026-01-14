using SafeRoom3D.Core;

namespace SafeRoom3D.Tests;

/// <summary>
/// Unit tests for CharacterStats damage calculations and stat formulas.
/// </summary>
public class CharacterStatsTests
{
    public void TestInitialStats()
    {
        var stats = new CharacterStats();

        // Base stats should be initialized
        Assert.IsGreaterThan(stats.MaxHealth, 0);
        Assert.IsGreaterThan(stats.MaxMana, 0);
    }

    public void TestSetLevel()
    {
        var stats = new CharacterStats();
        var initialHealth = stats.MaxHealth;

        stats.SetLevel(5);

        // Health should increase with level
        Assert.IsGreaterThan(stats.MaxHealth, initialHealth);
    }

    public void TestAttributeScaling()
    {
        var stats = new CharacterStats();
        var initialHealth = stats.MaxHealth;

        // Add vitality
        stats.AddAttribute(AttributeType.Vitality, 10);
        stats.RecalculateAll();

        // Health should increase (Vitality adds +10 HP per point)
        Assert.IsGreaterThan(stats.MaxHealth, initialHealth);
    }

    public void TestDamageCalculation()
    {
        var stats = new CharacterStats();

        // Physical damage should scale with strength
        var initialDamage = stats.PhysicalDamage;

        stats.AddAttribute(AttributeType.Strength, 10);
        stats.RecalculateAll();

        Assert.IsGreaterThan(stats.PhysicalDamage, initialDamage);
    }

    public void TestDexterityAffectsCrit()
    {
        var stats = new CharacterStats();
        var initialCrit = stats.CritChance;

        // Add dexterity
        stats.AddAttribute(AttributeType.Dexterity, 20);
        stats.RecalculateAll();

        // Crit should increase (Dexterity adds +0.5% crit per point)
        Assert.IsGreaterThan(stats.CritChance, initialCrit);
    }

    public void TestEnergyAffectsMana()
    {
        var stats = new CharacterStats();
        var initialMana = stats.MaxMana;

        // Add energy
        stats.AddAttribute(AttributeType.Energy, 10);
        stats.RecalculateAll();

        // Mana should increase (Energy adds +5 mana per point)
        Assert.IsGreaterThan(stats.MaxMana, initialMana);
    }

    public void TestDamageReduction()
    {
        var stats = new CharacterStats();

        // Damage reduction should be between 0 and 1
        Assert.IsGreaterThan(stats.DamageReduction, -0.01f);
        Assert.IsLessThan(stats.DamageReduction, 1.01f);
    }

    public void TestRegeneration()
    {
        var stats = new CharacterStats();

        // Regen should be positive
        Assert.IsGreaterThan(stats.HealthRegen, -0.01f);
        Assert.IsGreaterThan(stats.ManaRegen, -0.01f);
    }

    public void TestLevelUpAttributePoints()
    {
        var stats = new CharacterStats();

        stats.SetLevel(1);
        var pointsAtLevel1 = stats.UnspentAttributePoints;

        stats.SetLevel(5);
        var pointsAtLevel5 = stats.UnspentAttributePoints;

        // Should get 5 points per level
        Assert.IsGreaterThan(pointsAtLevel5, pointsAtLevel1);
    }

    public void TestSpendAttributePoints()
    {
        var stats = new CharacterStats();
        stats.SetLevel(10); // Get some points

        var initialPoints = stats.UnspentAttributePoints;
        var initialStrength = stats.GetAttribute(AttributeType.Strength);

        var success = stats.SpendAttributePoint(AttributeType.Strength);

        Assert.IsTrue(success, "Should be able to spend attribute point");
        Assert.AreEqual(initialPoints - 1, stats.UnspentAttributePoints);
        Assert.AreEqual(initialStrength + 1, stats.GetAttribute(AttributeType.Strength));
    }

    public void TestCannotSpendWithNoPoints()
    {
        var stats = new CharacterStats();
        // Reset to level 1 with no unspent points
        stats.SetLevel(1);

        // Spend all available points
        while (stats.UnspentAttributePoints > 0)
        {
            stats.SpendAttributePoint(AttributeType.Strength);
        }

        // Should not be able to spend more
        var result = stats.SpendAttributePoint(AttributeType.Strength);
        Assert.IsFalse(result, "Should not be able to spend without points");
    }
}
