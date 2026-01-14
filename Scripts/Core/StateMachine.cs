using Godot;
using System;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Generic state machine that can be used with any enum type for states.
/// Provides clean state transitions with enter/exit callbacks.
/// </summary>
/// <typeparam name="TState">Enum type representing the possible states.</typeparam>
public class StateMachine<TState> where TState : Enum
{
    private TState _currentState;
    private TState _previousState;
    private float _stateTimer;

    // Callbacks for state transitions
    private readonly Dictionary<TState, Action> _onEnterCallbacks = new();
    private readonly Dictionary<TState, Action> _onExitCallbacks = new();
    private readonly Dictionary<TState, Action<float>> _onUpdateCallbacks = new();

    // Transition guards - return false to prevent transition
    private readonly Dictionary<(TState from, TState to), Func<bool>> _transitionGuards = new();

    /// <summary>
    /// Current state of the machine.
    /// </summary>
    public TState CurrentState => _currentState;

    /// <summary>
    /// Previous state before the last transition.
    /// </summary>
    public TState PreviousState => _previousState;

    /// <summary>
    /// Time spent in the current state (in seconds).
    /// </summary>
    public float StateTime => _stateTimer;

    /// <summary>
    /// Event fired when state changes.
    /// Parameters: previousState, newState
    /// </summary>
    public event Action<TState, TState>? StateChanged;

    /// <summary>
    /// Create a new state machine with an initial state.
    /// </summary>
    public StateMachine(TState initialState)
    {
        _currentState = initialState;
        _previousState = initialState;
        _stateTimer = 0;
    }

    /// <summary>
    /// Register a callback for when entering a specific state.
    /// </summary>
    public StateMachine<TState> OnEnter(TState state, Action callback)
    {
        _onEnterCallbacks[state] = callback;
        return this;
    }

    /// <summary>
    /// Register a callback for when exiting a specific state.
    /// </summary>
    public StateMachine<TState> OnExit(TState state, Action callback)
    {
        _onExitCallbacks[state] = callback;
        return this;
    }

    /// <summary>
    /// Register a callback for updating while in a specific state.
    /// The callback receives the delta time.
    /// </summary>
    public StateMachine<TState> OnUpdate(TState state, Action<float> callback)
    {
        _onUpdateCallbacks[state] = callback;
        return this;
    }

    /// <summary>
    /// Add a guard condition for a specific transition.
    /// The transition will only occur if the guard returns true.
    /// </summary>
    public StateMachine<TState> AddGuard(TState from, TState to, Func<bool> guard)
    {
        _transitionGuards[(from, to)] = guard;
        return this;
    }

    /// <summary>
    /// Attempt to transition to a new state.
    /// Returns true if the transition occurred.
    /// </summary>
    public bool TransitionTo(TState newState)
    {
        // Don't transition to same state
        if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
        {
            return false;
        }

        // Check transition guard
        if (_transitionGuards.TryGetValue((_currentState, newState), out var guard))
        {
            if (!guard())
            {
                return false;
            }
        }

        // Exit current state
        if (_onExitCallbacks.TryGetValue(_currentState, out var exitCallback))
        {
            exitCallback();
        }

        // Update state
        _previousState = _currentState;
        _currentState = newState;
        _stateTimer = 0;

        // Enter new state
        if (_onEnterCallbacks.TryGetValue(_currentState, out var enterCallback))
        {
            enterCallback();
        }

        // Fire event
        StateChanged?.Invoke(_previousState, _currentState);

        return true;
    }

    /// <summary>
    /// Force transition without checking guards.
    /// </summary>
    public void ForceTransitionTo(TState newState)
    {
        if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
        {
            return;
        }

        // Exit current state
        if (_onExitCallbacks.TryGetValue(_currentState, out var exitCallback))
        {
            exitCallback();
        }

        // Update state
        _previousState = _currentState;
        _currentState = newState;
        _stateTimer = 0;

        // Enter new state
        if (_onEnterCallbacks.TryGetValue(_currentState, out var enterCallback))
        {
            enterCallback();
        }

        // Fire event
        StateChanged?.Invoke(_previousState, _currentState);
    }

    /// <summary>
    /// Update the state machine. Call this in _Process or _PhysicsProcess.
    /// </summary>
    public void Update(float delta)
    {
        _stateTimer += delta;

        if (_onUpdateCallbacks.TryGetValue(_currentState, out var updateCallback))
        {
            updateCallback(delta);
        }
    }

    /// <summary>
    /// Check if the machine is in a specific state.
    /// </summary>
    public bool IsInState(TState state)
    {
        return EqualityComparer<TState>.Default.Equals(_currentState, state);
    }

    /// <summary>
    /// Check if the machine is in any of the specified states.
    /// </summary>
    public bool IsInAnyState(params TState[] states)
    {
        foreach (var state in states)
        {
            if (EqualityComparer<TState>.Default.Equals(_currentState, state))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Check if the machine was previously in a specific state.
    /// </summary>
    public bool WasInState(TState state)
    {
        return EqualityComparer<TState>.Default.Equals(_previousState, state);
    }

    /// <summary>
    /// Reset the state machine to its initial state.
    /// </summary>
    public void Reset(TState initialState)
    {
        _previousState = _currentState;
        _currentState = initialState;
        _stateTimer = 0;
    }

    /// <summary>
    /// Get a string representation of the current state.
    /// </summary>
    public override string ToString()
    {
        return $"StateMachine<{typeof(TState).Name}>: {_currentState} (was {_previousState}, {_stateTimer:F2}s)";
    }
}

/// <summary>
/// Extension methods for StateMachine fluent configuration.
/// </summary>
public static class StateMachineExtensions
{
    /// <summary>
    /// Configure multiple enter callbacks at once using a dictionary.
    /// </summary>
    public static StateMachine<TState> ConfigureEnterCallbacks<TState>(
        this StateMachine<TState> machine,
        Dictionary<TState, Action> callbacks) where TState : Enum
    {
        foreach (var (state, callback) in callbacks)
        {
            machine.OnEnter(state, callback);
        }
        return machine;
    }

    /// <summary>
    /// Configure multiple exit callbacks at once using a dictionary.
    /// </summary>
    public static StateMachine<TState> ConfigureExitCallbacks<TState>(
        this StateMachine<TState> machine,
        Dictionary<TState, Action> callbacks) where TState : Enum
    {
        foreach (var (state, callback) in callbacks)
        {
            machine.OnExit(state, callback);
        }
        return machine;
    }
}
