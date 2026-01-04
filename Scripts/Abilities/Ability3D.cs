using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.Player;

namespace SafeRoom3D.Abilities;

/// <summary>
/// Base class for all player abilities in the 3D game.
/// Provides common functionality: cooldowns, mana costs, activation.
/// Ported from 2D ability system architecture.
/// </summary>
public abstract partial class Ability3D : Node
{
    // Ability identity
    public abstract string AbilityId { get; }
    public abstract string AbilityName { get; }
    public abstract string Description { get; }

    // Ability type
    public abstract AbilityType Type { get; }

    // Default stats (can be overridden by config)
    public abstract float DefaultCooldown { get; }
    public abstract int DefaultManaCost { get; }

    // Current stats (loaded from config or defaults)
    public float Cooldown { get; protected set; }
    public int ManaCost { get; protected set; }

    // Cooldown state
    public bool IsOnCooldown => CooldownRemaining > 0;
    public float CooldownRemaining { get; protected set; }
    public float CooldownProgress => Cooldown > 0 ? 1f - (CooldownRemaining / Cooldown) : 1f;

    // Active state (for toggle/duration abilities)
    public bool IsActive { get; protected set; }
    public float DurationRemaining { get; protected set; }

    // References
    protected FPSController? Player => FPSController.Instance;
    protected GameManager3D? GameManager => GameManager3D.Instance;

    // Signals
    [Signal] public delegate void CooldownStartedEventHandler(float duration);
    [Signal] public delegate void CooldownUpdatedEventHandler(float remaining, float total);
    [Signal] public delegate void CooldownFinishedEventHandler();
    [Signal] public delegate void ActivatedEventHandler();
    [Signal] public delegate void DeactivatedEventHandler();
    [Signal] public delegate void EffectAppliedEventHandler(Node3D target);

    public override void _Ready()
    {
        // Load config values or use defaults
        LoadConfig();

        // Add to abilities group for easy finding
        AddToGroup("Abilities");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Process cooldown
        if (IsOnCooldown)
        {
            CooldownRemaining -= dt;
            EmitSignal(SignalName.CooldownUpdated, CooldownRemaining, Cooldown);

            if (CooldownRemaining <= 0)
            {
                CooldownRemaining = 0;
                EmitSignal(SignalName.CooldownFinished);
            }
        }

        // Process active duration (for buff/effect abilities)
        if (IsActive && DurationRemaining > 0)
        {
            DurationRemaining -= dt;
            OnActiveTick(dt);

            if (DurationRemaining <= 0)
            {
                Deactivate();
            }
        }
    }

    /// <summary>
    /// Load ability configuration from config system.
    /// Override to load additional stats.
    /// </summary>
    protected virtual void LoadConfig()
    {
        // TODO: Load from AbilityConfig3D when implemented
        Cooldown = DefaultCooldown;
        ManaCost = DefaultManaCost;
    }

    /// <summary>
    /// Attempt to activate the ability.
    /// Returns true if activation was successful.
    /// </summary>
    public virtual bool TryActivate()
    {
        // Check if on cooldown
        if (IsOnCooldown)
        {
            GD.Print($"[{AbilityName}] On cooldown: {CooldownRemaining:F1}s remaining");
            return false;
        }

        // Check player reference
        if (Player == null)
        {
            GD.Print($"[{AbilityName}] No player reference");
            return false;
        }

        // NOTE: Mana cost check disabled for testing
        // if (Player.CurrentMana < ManaCost)
        // {
        //     GD.Print($"[{AbilityName}] Not enough mana: {Player.CurrentMana}/{ManaCost}");
        //     return false;
        // }

        // Toggle abilities can be deactivated
        if (Type == AbilityType.Toggle && IsActive)
        {
            Deactivate();
            return true;
        }

        // Check additional conditions
        if (!CanActivate())
        {
            return false;
        }

        // NOTE: Mana consumption disabled for testing
        // if (ManaCost > 0)
        // {
        //     Player.UseMana(ManaCost);
        // }

        // Start cooldown
        StartCooldown();

        // Activate the ability
        Activate();

        return true;
    }

    /// <summary>
    /// Override to add additional activation conditions.
    /// </summary>
    protected virtual bool CanActivate()
    {
        return true;
    }

    /// <summary>
    /// Core activation logic. Override in subclasses.
    /// </summary>
    protected abstract void Activate();

    /// <summary>
    /// Deactivate the ability (for toggle/duration abilities).
    /// </summary>
    public virtual void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        DurationRemaining = 0;
        OnDeactivate();
        EmitSignal(SignalName.Deactivated);

        GD.Print($"[{AbilityName}] Deactivated");
    }

    /// <summary>
    /// Called when ability is deactivated. Override for cleanup.
    /// </summary>
    protected virtual void OnDeactivate() { }

    /// <summary>
    /// Called every frame while ability is active. Override for ongoing effects.
    /// </summary>
    protected virtual void OnActiveTick(float dt) { }

    /// <summary>
    /// Start the cooldown timer.
    /// </summary>
    protected virtual void StartCooldown()
    {
        CooldownRemaining = Cooldown;
        EmitSignal(SignalName.CooldownStarted, Cooldown);
    }

    /// <summary>
    /// Reset cooldown (used by Audience Favorite).
    /// </summary>
    public virtual void ResetCooldown()
    {
        CooldownRemaining = 0;
        EmitSignal(SignalName.CooldownFinished);
        GD.Print($"[{AbilityName}] Cooldown reset!");
    }

    /// <summary>
    /// Set the ability as active with a duration.
    /// </summary>
    protected void SetActiveWithDuration(float duration)
    {
        IsActive = true;
        DurationRemaining = duration;
        EmitSignal(SignalName.Activated);
        GD.Print($"[{AbilityName}] Activated for {duration}s");
    }

    /// <summary>
    /// Set the ability as active (toggle mode, no duration).
    /// </summary>
    protected void SetActiveToggle()
    {
        IsActive = true;
        DurationRemaining = -1; // Infinite until toggled off
        EmitSignal(SignalName.Activated);
        GD.Print($"[{AbilityName}] Toggled ON");
    }

    /// <summary>
    /// Get camera look direction for targeting.
    /// </summary>
    protected Vector3 GetLookDirection()
    {
        return Player?.GetLookDirection() ?? Vector3.Forward;
    }

    /// <summary>
    /// Get camera position for spawning effects.
    /// </summary>
    protected Vector3 GetCameraPosition()
    {
        return Player?.GetCameraPosition() ?? Vector3.Zero;
    }

    /// <summary>
    /// Get player position.
    /// </summary>
    protected Vector3 GetPlayerPosition()
    {
        return Player?.GlobalPosition ?? Vector3.Zero;
    }

    /// <summary>
    /// Perform 3D raycast from camera to find ground/target position.
    /// </summary>
    protected Vector3? GetTargetPosition(float maxDistance = 50f)
    {
        if (Player == null) return null;

        var spaceState = Player.GetWorld3D().DirectSpaceState;
        var origin = GetCameraPosition();
        var direction = GetLookDirection();
        var end = origin + direction * maxDistance;

        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollideWithBodies = true;
        query.CollisionMask = 1 | 2 | 8 | 16; // Player, Enemy, Wall, Obstacle

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            return (Vector3)result["position"];
        }

        // No hit, return max distance point
        return end;
    }

    /// <summary>
    /// Find all enemies within radius of a position.
    /// </summary>
    protected Godot.Collections.Array<Node3D> GetEnemiesInRadius(Vector3 center, float radius)
    {
        var enemies = new Godot.Collections.Array<Node3D>();

        foreach (var node in GetTree().GetNodesInGroup("Enemies"))
        {
            if (node is Node3D enemy)
            {
                float dist = enemy.GlobalPosition.DistanceTo(center);
                if (dist <= radius)
                {
                    enemies.Add(enemy);
                }
            }
        }

        return enemies;
    }

    /// <summary>
    /// Deal damage to a target enemy.
    /// </summary>
    protected void DealDamage(Node3D target, float damage, Vector3 fromPosition)
    {
        if (target.HasMethod("TakeDamage"))
        {
            target.Call("TakeDamage", damage, fromPosition, AbilityName);
            EmitSignal(SignalName.EffectApplied, target);
        }
    }

    /// <summary>
    /// Heal the player.
    /// </summary>
    protected void HealPlayer(int amount)
    {
        Player?.Heal(amount);
    }

    /// <summary>
    /// Restore player mana.
    /// </summary>
    protected void RestoreMana(int amount)
    {
        Player?.RestoreMana(amount);
    }

    /// <summary>
    /// Request screen shake.
    /// </summary>
    protected void RequestScreenShake(float duration, float intensity)
    {
        Player?.RequestScreenShake(duration, intensity);
    }
}

/// <summary>
/// Types of abilities for UI/behavior categorization.
/// </summary>
public enum AbilityType
{
    /// <summary>Instant effect, then cooldown</summary>
    Instant,

    /// <summary>Requires targeting before activation</summary>
    Targeted,

    /// <summary>Active for duration, provides buff/effect</summary>
    Duration,

    /// <summary>Toggle on/off, no duration</summary>
    Toggle,

    /// <summary>Passive effect while equipped</summary>
    Passive
}
