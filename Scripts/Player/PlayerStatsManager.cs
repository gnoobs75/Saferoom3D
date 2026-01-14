using Godot;
using SafeRoom3D.Core;

namespace SafeRoom3D.Player;

/// <summary>
/// Manages player stats: health, mana, experience, regeneration, and leveling.
/// Extracted from FPSController for better separation of concerns.
/// </summary>
public class PlayerStatsManager
{
    // Stats system
    public CharacterStats Stats { get; }
    public int CurrentHealth { get; private set; }
    public int CurrentMana { get; private set; }
    public int CurrentExperience { get; private set; }
    public int ExperienceToLevel { get; private set; }
    public int PlayerLevel { get; private set; } = 1;

    public int MaxHealth => Stats.MaxHealth;
    public int MaxMana => Stats.MaxMana;

    // Regeneration
    private float _healthRegenTimer;
    private float _manaRegenTimer;
    private const float RegenTickInterval = 0.5f;

    // Events
    public event System.Action<int, int>? HealthChanged;
    public event System.Action<int, int>? ManaChanged;
    public event System.Action<int, int, int>? ExperienceChanged;
    public event System.Action<int>? LeveledUp;
    public event System.Action? Died;

    public PlayerStatsManager()
    {
        Stats = new CharacterStats();
    }

    /// <summary>
    /// Initialize stats and set starting values.
    /// </summary>
    public void Initialize(int startingLevel = 1)
    {
        Stats.SetLevel(startingLevel);
        PlayerLevel = startingLevel;

        CurrentHealth = Stats.MaxHealth;
        CurrentMana = Stats.MaxMana;
        CurrentExperience = Constants.StartingExperience;
        ExperienceToLevel = CalculateExperienceToLevel(PlayerLevel);

        EmitHealthChanged();
        EmitManaChanged();
        EmitExperienceChanged();
    }

    /// <summary>
    /// Process regeneration over time.
    /// </summary>
    public void ProcessRegeneration(float delta)
    {
        // Health regeneration
        _healthRegenTimer += delta;
        if (_healthRegenTimer >= RegenTickInterval)
        {
            _healthRegenTimer = 0;

            if (CurrentHealth < Stats.MaxHealth && CurrentHealth > 0)
            {
                var regenAmount = Mathf.RoundToInt(Stats.HealthRegen);
                if (regenAmount > 0)
                {
                    Heal(regenAmount, silent: true);
                }
            }
        }

        // Mana regeneration
        _manaRegenTimer += delta;
        if (_manaRegenTimer >= RegenTickInterval)
        {
            _manaRegenTimer = 0;

            if (CurrentMana < Stats.MaxMana)
            {
                var regenAmount = Mathf.RoundToInt(Stats.ManaRegen);
                if (regenAmount > 0)
                {
                    RestoreMana(regenAmount, silent: true);
                }
            }
        }
    }

    /// <summary>
    /// Apply damage to the player.
    /// </summary>
    /// <returns>True if the player died from this damage.</returns>
    public bool TakeDamage(float damage, out float actualDamage)
    {
        // Apply damage reduction from stats
        var reduction = Stats.DamageReduction;
        actualDamage = damage * (1f - reduction);

        // Critical hit avoidance (dodge)
        var random = new System.Random();
        if (random.NextDouble() < Stats.DodgeChance)
        {
            actualDamage = 0;
            return false;
        }

        CurrentHealth -= Mathf.RoundToInt(actualDamage);
        CurrentHealth = Mathf.Max(0, CurrentHealth);

        EmitHealthChanged();

        if (CurrentHealth <= 0)
        {
            Died?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Heal the player.
    /// </summary>
    public void Heal(int amount, bool silent = false)
    {
        if (CurrentHealth <= 0) return; // Can't heal if dead

        CurrentHealth = Mathf.Min(CurrentHealth + amount, Stats.MaxHealth);

        if (!silent)
        {
            EmitHealthChanged();
        }
    }

    /// <summary>
    /// Restore mana.
    /// </summary>
    public void RestoreMana(int amount, bool silent = false)
    {
        CurrentMana = Mathf.Min(CurrentMana + amount, Stats.MaxMana);

        if (!silent)
        {
            EmitManaChanged();
        }
    }

    /// <summary>
    /// Consume mana for ability use.
    /// </summary>
    /// <returns>True if mana was consumed, false if not enough mana.</returns>
    public bool ConsumeMana(int amount)
    {
        if (CurrentMana < amount) return false;

        CurrentMana -= amount;
        EmitManaChanged();
        return true;
    }

    /// <summary>
    /// Add experience and potentially level up.
    /// </summary>
    public void AddExperience(int amount)
    {
        CurrentExperience += amount;

        // Check for level up
        while (CurrentExperience >= ExperienceToLevel)
        {
            CurrentExperience -= ExperienceToLevel;
            LevelUp();
        }

        EmitExperienceChanged();
    }

    private void LevelUp()
    {
        PlayerLevel++;
        Stats.SetLevel(PlayerLevel);

        // Restore health and mana on level up
        CurrentHealth = Stats.MaxHealth;
        CurrentMana = Stats.MaxMana;

        ExperienceToLevel = CalculateExperienceToLevel(PlayerLevel);

        LeveledUp?.Invoke(PlayerLevel);
        GD.Print($"[PlayerStats] Level up! Now level {PlayerLevel}");
    }

    private int CalculateExperienceToLevel(int level)
    {
        // Exponential scaling: 100 * level^1.5
        return Mathf.RoundToInt(100 * Mathf.Pow(level, 1.5f));
    }

    /// <summary>
    /// Reset health and mana to full.
    /// </summary>
    public void ResetHealth()
    {
        CurrentHealth = Stats.MaxHealth;
        CurrentMana = Stats.MaxMana;
        EmitHealthChanged();
        EmitManaChanged();
    }

    /// <summary>
    /// Force recalculate stats (after equipment change, etc.)
    /// </summary>
    public void RecalculateStats()
    {
        Stats.RecalculateAll();

        // Clamp current values to new max
        if (CurrentHealth > Stats.MaxHealth)
            CurrentHealth = Stats.MaxHealth;
        if (CurrentMana > Stats.MaxMana)
            CurrentMana = Stats.MaxMana;

        EmitHealthChanged();
        EmitManaChanged();
    }

    private void EmitHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, Stats.MaxHealth);
    }

    private void EmitManaChanged()
    {
        ManaChanged?.Invoke(CurrentMana, Stats.MaxMana);
    }

    private void EmitExperienceChanged()
    {
        ExperienceChanged?.Invoke(CurrentExperience, ExperienceToLevel, PlayerLevel);
    }
}
