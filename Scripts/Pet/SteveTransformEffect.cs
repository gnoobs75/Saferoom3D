using Godot;

namespace SafeRoom3D.Pet;

/// <summary>
/// Buff effect that manages Steve's transformation into a monster form.
/// Lasts 5 minutes then reverts Steve to normal.
/// </summary>
public partial class SteveTransformEffect : Node3D
{
    /// <summary>
    /// Currently active transformation effect (only one can be active at a time)
    /// </summary>
    public static SteveTransformEffect? Active { get; private set; }

    private string _monsterType = "";
    private float _duration = 300f; // 5 minutes
    private float _lifetime;
    private bool _hasExpired;

    /// <summary>
    /// The monster type Steve is transformed into
    /// </summary>
    public string MonsterType => _monsterType;

    /// <summary>
    /// Time remaining on transformation in seconds
    /// </summary>
    public float TimeRemaining => Mathf.Max(0, _duration - _lifetime);

    /// <summary>
    /// Initialize the transformation effect
    /// </summary>
    public void Initialize(string monsterType)
    {
        _monsterType = monsterType;
        _lifetime = 0;
        _hasExpired = false;

        // Cancel any existing transformation
        if (Active != null && Active != this)
        {
            Active.Expire();
        }
        Active = this;

        // Apply transformation to Steve
        if (Steve3D.Instance != null)
        {
            Steve3D.Instance.TransformInto(monsterType);
            GD.Print($"[SteveTransformEffect] Steve transformed into {monsterType} for {_duration} seconds");
        }
        else
        {
            GD.PrintErr("[SteveTransformEffect] Steve not found, cannot transform");
            QueueFree();
        }
    }

    public override void _Process(double delta)
    {
        if (_hasExpired) return;

        _lifetime += (float)delta;

        // Check if transformation has expired
        if (_lifetime >= _duration)
        {
            Expire();
        }

        // Log remaining time every 60 seconds
        if ((int)_lifetime % 60 == 0 && _lifetime > 0 && _lifetime < _duration)
        {
            int minutesRemaining = (int)((_duration - _lifetime) / 60);
            if (minutesRemaining > 0 && (int)(_lifetime - (float)delta) % 60 != 0)
            {
                GD.Print($"[SteveTransformEffect] {minutesRemaining} minute(s) remaining on {_monsterType} form");
            }
        }
    }

    /// <summary>
    /// End the transformation and revert Steve to normal
    /// </summary>
    public void Expire()
    {
        if (_hasExpired) return;
        _hasExpired = true;

        // Revert Steve to normal form
        if (Steve3D.Instance != null)
        {
            Steve3D.Instance.RevertToNormal();
            GD.Print($"[SteveTransformEffect] Steve reverted from {_monsterType} to normal form");

            // Notify player
            UI.HUD3D.Instance?.AddCombatLogMessage(
                $"[color=#aaaaff]Steve's {FormatMonsterName(_monsterType)} form has worn off.[/color]",
                new Color(0.67f, 0.67f, 1f));
        }

        // Clear active reference
        if (Active == this)
        {
            Active = null;
        }

        QueueFree();
    }

    public override void _ExitTree()
    {
        // Ensure we clean up if removed unexpectedly
        if (Active == this)
        {
            Active = null;
        }
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

    /// <summary>
    /// Check if Steve is currently transformed
    /// </summary>
    public static bool IsTransformed => Active != null;

    /// <summary>
    /// Get the current transformation type (or null if not transformed)
    /// </summary>
    public static string? CurrentForm => Active?.MonsterType;
}
