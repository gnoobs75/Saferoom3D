using Godot;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Core;
using SafeRoom3D.Abilities.Effects;

namespace SafeRoom3D.Abilities;

/// <summary>
/// Manages all mana-based spells: registration, unlocks, and spell-specific functionality.
/// Works alongside AbilityManager3D which handles abilities (no mana cost).
/// </summary>
public partial class SpellManager3D : Node
{
    // Singleton
    public static SpellManager3D? Instance { get; private set; }

    // All registered spells
    private readonly Dictionary<string, Spell3D> _spells = new();

    // Unlock tracking
    private readonly HashSet<string> _unlockedSpellIds = new();

    // Signals
    [Signal] public delegate void SpellRegisteredEventHandler(string spellId, string spellName);
    [Signal] public delegate void SpellUnlockedEventHandler(string spellId);

    public override void _Ready()
    {
        Instance = this;
        CallDeferred(nameof(Initialize));
    }

    private void Initialize()
    {
        CreateSpells();
        SetupStarterSpells();
        GD.Print($"[SpellManager3D] Initialized with {_spells.Count} spells");
    }

    private void CreateSpells()
    {
        // Register existing spells (mana-based)
        RegisterSpell(new Fireball3D());
        RegisterSpell(new ChainLightning3D());
        RegisterSpell(new SoulLeech3D());
        RegisterSpell(new GravityWell3D());
        RegisterSpell(new TimestopBubble3D());
        RegisterSpell(new InfernalGround3D());
        RegisterSpell(new BansheesWail3D());
        RegisterSpell(new Berserk3D());

        // Register new spells (will be created in Phase 3)
        // RegisterSpell(new FrostNova3D());
        // RegisterSpell(new ArcaneMissiles3D());
        // RegisterSpell(new MeteorStrike3D());
        // RegisterSpell(new PoisonCloud3D());
        // RegisterSpell(new LightningStorm3D());
        // RegisterSpell(new DeathCoil3D());
        // RegisterSpell(new ManaBurn3D());
    }

    private void RegisterSpell(Ability3D spell)
    {
        // Validate it's actually a spell (uses mana)
        if (!spell.UsesMana)
        {
            GD.PrintErr($"[SpellManager3D] {spell.AbilityId} doesn't use mana - should be registered with AbilityManager3D");
            return;
        }

        if (_spells.ContainsKey(spell.AbilityId))
        {
            GD.PrintErr($"[SpellManager3D] Duplicate spell ID: {spell.AbilityId}");
            return;
        }

        // Add as child so it receives _Process calls
        AddChild(spell);

        _spells[spell.AbilityId] = (Spell3D)spell;
        EmitSignal(SignalName.SpellRegistered, spell.AbilityId, spell.AbilityName);
    }

    private void SetupStarterSpells()
    {
        // Fireball is unlocked by default
        UnlockSpell("fireball");
    }

    /// <summary>
    /// Unlock a spell for the player.
    /// </summary>
    public bool UnlockSpell(string spellId)
    {
        if (!_spells.TryGetValue(spellId, out var spell))
        {
            GD.PrintErr($"[SpellManager3D] Unknown spell: {spellId}");
            return false;
        }

        if (_unlockedSpellIds.Contains(spellId))
        {
            GD.Print($"[SpellManager3D] {spellId} already unlocked");
            return false;
        }

        _unlockedSpellIds.Add(spellId);
        spell.IsUnlocked = true;

        EmitSignal(SignalName.SpellUnlocked, spellId);
        GD.Print($"[SpellManager3D] Unlocked spell: {spell.AbilityName}");

        return true;
    }

    /// <summary>
    /// Check if a spell is unlocked.
    /// </summary>
    public bool IsSpellUnlocked(string spellId)
    {
        return _unlockedSpellIds.Contains(spellId);
    }

    /// <summary>
    /// Get all spells available for unlocking at a given level.
    /// </summary>
    public IEnumerable<Spell3D> GetSpellsAvailableAtLevel(int level)
    {
        return _spells.Values
            .Where(s => !s.IsUnlocked && s.RequiredLevel <= level)
            .OrderBy(s => s.RequiredLevel);
    }

    /// <summary>
    /// Get all unlocked spells.
    /// </summary>
    public IEnumerable<Spell3D> GetUnlockedSpells()
    {
        return _spells.Values.Where(s => s.IsUnlocked);
    }

    /// <summary>
    /// Get all spells (unlocked or not).
    /// </summary>
    public IEnumerable<Spell3D> GetAllSpells() => _spells.Values;

    /// <summary>
    /// Get spell by ID.
    /// </summary>
    public Spell3D? GetSpell(string spellId)
    {
        return _spells.TryGetValue(spellId, out var spell) ? spell : null;
    }

    /// <summary>
    /// Get spell count.
    /// </summary>
    public int SpellCount => _spells.Count;

    /// <summary>
    /// Get unlocked spell count.
    /// </summary>
    public int UnlockedSpellCount => _unlockedSpellIds.Count;

    /// <summary>
    /// Get locked spell count.
    /// </summary>
    public int LockedSpellCount => _spells.Count - _unlockedSpellIds.Count;

    /// <summary>
    /// Save unlocked spells to a list (for persistence).
    /// </summary>
    public List<string> GetUnlockedSpellIds()
    {
        return _unlockedSpellIds.ToList();
    }

    /// <summary>
    /// Load unlocked spells from a list (for persistence).
    /// </summary>
    public void LoadUnlockedSpells(IEnumerable<string> spellIds)
    {
        _unlockedSpellIds.Clear();

        foreach (var id in spellIds)
        {
            if (_spells.TryGetValue(id, out var spell))
            {
                _unlockedSpellIds.Add(id);
                spell.IsUnlocked = true;
            }
        }

        GD.Print($"[SpellManager3D] Loaded {_unlockedSpellIds.Count} unlocked spells");
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}
