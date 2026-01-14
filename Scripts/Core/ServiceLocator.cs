using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Service locator pattern for dependency management.
/// Provides a cleaner alternative to direct singleton access.
///
/// Usage:
///   // Register a service
///   ServiceLocator.Register&lt;ISoundService&gt;(soundManager);
///
///   // Retrieve a service
///   var sound = ServiceLocator.Get&lt;ISoundService&gt;();
///
/// This allows:
/// - Interface-based programming (easier testing)
/// - Swapping implementations without changing consumers
/// - Clearer dependencies (explicit registration)
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly Dictionary<Type, Func<object>> _factories = new();

    /// <summary>
    /// Register a service instance.
    /// </summary>
    public static void Register<T>(T service) where T : class
    {
        var type = typeof(T);
        _services[type] = service;
        GD.Print($"[ServiceLocator] Registered: {type.Name}");
    }

    /// <summary>
    /// Register a factory for lazy instantiation.
    /// </summary>
    public static void RegisterFactory<T>(Func<T> factory) where T : class
    {
        var type = typeof(T);
        _factories[type] = () => factory();
    }

    /// <summary>
    /// Get a registered service.
    /// </summary>
    public static T? Get<T>() where T : class
    {
        var type = typeof(T);

        // Check direct registration
        if (_services.TryGetValue(type, out var service))
        {
            return service as T;
        }

        // Check factory
        if (_factories.TryGetValue(type, out var factory))
        {
            var instance = factory() as T;
            if (instance != null)
            {
                _services[type] = instance;
                return instance;
            }
        }

        return null;
    }

    /// <summary>
    /// Get a required service (throws if not found).
    /// </summary>
    public static T GetRequired<T>() where T : class
    {
        var service = Get<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
        }
        return service;
    }

    /// <summary>
    /// Check if a service is registered.
    /// </summary>
    public static bool Has<T>() where T : class
    {
        var type = typeof(T);
        return _services.ContainsKey(type) || _factories.ContainsKey(type);
    }

    /// <summary>
    /// Unregister a service.
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        var type = typeof(T);
        _services.Remove(type);
        _factories.Remove(type);
    }

    /// <summary>
    /// Clear all registered services.
    /// Call during scene transitions.
    /// </summary>
    public static void Clear()
    {
        _services.Clear();
        _factories.Clear();
        GD.Print("[ServiceLocator] All services cleared");
    }

    /// <summary>
    /// Get a debug list of registered services.
    /// </summary>
    public static string GetRegisteredServices()
    {
        var lines = new List<string>();
        foreach (var kvp in _services)
        {
            lines.Add($"  - {kvp.Key.Name}: {kvp.Value?.GetType().Name}");
        }
        foreach (var kvp in _factories)
        {
            lines.Add($"  - {kvp.Key.Name}: (factory)");
        }
        return string.Join("\n", lines);
    }
}

/// <summary>
/// Interfaces for key services (enables dependency injection and testing).
/// These can be implemented by the existing singletons.
/// </summary>
public interface ISoundService
{
    void PlaySound(string soundName);
    void PlayMusic(string musicName);
    void StopMusic();
    void SetMusicVolume(float volume);
    void SetSfxVolume(float volume);
}

public interface IGameManager
{
    bool IsPaused { get; }
    bool IsGameOver { get; }
    int CurrentFloor { get; }
    float FloorTimeRemaining { get; }
    void PauseGame();
    void ResumeGame();
    void TriggerGameOver();
    void TriggerVictory();
}

public interface IPlayerService
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    int CurrentMana { get; }
    int MaxMana { get; }
    Godot.Vector3 Position { get; }
    void TakeDamage(float damage, Godot.Vector3 fromPosition, string source);
    void Heal(int amount);
    void RestoreMana(int amount);
    void AddExperience(int amount);
}

public interface IInventoryService
{
    bool HasItem(string itemId);
    int GetItemCount(string itemId);
    bool AddItem(string itemId, int count = 1);
    bool RemoveItem(string itemId, int count = 1);
    int GetGold();
    void AddGold(int amount);
    bool SpendGold(int amount);
}

public interface IQuestService
{
    void GenerateQuestsForFloor(System.Collections.Generic.List<string> monsterTypes, int floor);
    void OnMonsterKilled(string monsterType, bool isBoss);
    void OnItemPickedUp(string itemId, int totalCount);
    System.Collections.Generic.List<SafeRoom3D.Core.Quest> GetActiveQuests();
    System.Collections.Generic.List<SafeRoom3D.Core.Quest> GetAvailableQuests();
}
