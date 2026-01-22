namespace SafeRoom3D.Abilities;

/// <summary>
/// Interface for level-gated content (spells, abilities).
/// Items implementing this can be locked until player reaches required level.
/// </summary>
public interface IUnlockable
{
    /// <summary>
    /// Unique identifier for this unlockable item.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Description text for tooltips.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Minimum player level required to unlock this item.
    /// </summary>
    int RequiredLevel { get; }

    /// <summary>
    /// Whether this item has been unlocked by the player.
    /// </summary>
    bool IsUnlocked { get; set; }

    /// <summary>
    /// Category for organization (e.g., "Spell", "Ability").
    /// </summary>
    string Category { get; }
}
