using SafeRoom3D.Core;

namespace SafeRoom3D.Tests;

/// <summary>
/// Unit tests for the generic StateMachine class.
/// </summary>
public class StateMachineTests
{
    private enum TestState { Idle, Running, Jumping, Falling }

    public void TestInitialState()
    {
        var sm = new StateMachine<TestState>(TestState.Idle);

        Assert.AreEqual(TestState.Idle, sm.CurrentState);
        Assert.AreEqual(0f, sm.StateTime);
    }

    public void TestBasicTransition()
    {
        var sm = new StateMachine<TestState>(TestState.Idle);

        var result = sm.TransitionTo(TestState.Running);

        Assert.IsTrue(result, "Transition should succeed");
        Assert.AreEqual(TestState.Running, sm.CurrentState);
        Assert.AreEqual(TestState.Idle, sm.PreviousState);
    }

    public void TestTransitionToSameState()
    {
        var sm = new StateMachine<TestState>(TestState.Idle);

        var result = sm.TransitionTo(TestState.Idle);

        Assert.IsFalse(result, "Transition to same state should fail");
        Assert.AreEqual(TestState.Idle, sm.CurrentState);
    }

    public void TestOnEnterCallback()
    {
        var enterCalled = false;
        var sm = new StateMachine<TestState>(TestState.Idle)
            .OnEnter(TestState.Running, () => enterCalled = true);

        sm.TransitionTo(TestState.Running);

        Assert.IsTrue(enterCalled, "OnEnter callback should be called");
    }

    public void TestOnExitCallback()
    {
        var exitCalled = false;
        var sm = new StateMachine<TestState>(TestState.Idle)
            .OnExit(TestState.Idle, () => exitCalled = true);

        sm.TransitionTo(TestState.Running);

        Assert.IsTrue(exitCalled, "OnExit callback should be called");
    }

    public void TestStateChangedEvent()
    {
        var eventFired = false;
        TestState previousInEvent = TestState.Idle;
        TestState newInEvent = TestState.Idle;

        var sm = new StateMachine<TestState>(TestState.Idle);
        sm.StateChanged += (prev, next) =>
        {
            eventFired = true;
            previousInEvent = prev;
            newInEvent = next;
        };

        sm.TransitionTo(TestState.Running);

        Assert.IsTrue(eventFired, "StateChanged event should fire");
        Assert.AreEqual(TestState.Idle, previousInEvent);
        Assert.AreEqual(TestState.Running, newInEvent);
    }

    public void TestTransitionGuard()
    {
        var allowTransition = false;
        var sm = new StateMachine<TestState>(TestState.Idle)
            .AddGuard(TestState.Idle, TestState.Jumping, () => allowTransition);

        // Guard returns false, transition should fail
        var result1 = sm.TransitionTo(TestState.Jumping);
        Assert.IsFalse(result1, "Transition should fail when guard returns false");
        Assert.AreEqual(TestState.Idle, sm.CurrentState);

        // Guard returns true, transition should succeed
        allowTransition = true;
        var result2 = sm.TransitionTo(TestState.Jumping);
        Assert.IsTrue(result2, "Transition should succeed when guard returns true");
        Assert.AreEqual(TestState.Jumping, sm.CurrentState);
    }

    public void TestForceTransition()
    {
        var sm = new StateMachine<TestState>(TestState.Idle)
            .AddGuard(TestState.Idle, TestState.Jumping, () => false); // Always block

        sm.ForceTransitionTo(TestState.Jumping);

        Assert.AreEqual(TestState.Jumping, sm.CurrentState, "Force transition should ignore guards");
    }

    public void TestStateTimer()
    {
        var sm = new StateMachine<TestState>(TestState.Idle);

        Assert.AreEqual(0f, sm.StateTime);

        sm.Update(0.5f);
        Assert.AreEqual(0.5f, sm.StateTime);

        sm.Update(0.5f);
        Assert.AreEqual(1.0f, sm.StateTime);

        // Timer should reset on transition
        sm.TransitionTo(TestState.Running);
        Assert.AreEqual(0f, sm.StateTime);
    }

    public void TestIsInState()
    {
        var sm = new StateMachine<TestState>(TestState.Idle);

        Assert.IsTrue(sm.IsInState(TestState.Idle));
        Assert.IsFalse(sm.IsInState(TestState.Running));
    }

    public void TestIsInAnyState()
    {
        var sm = new StateMachine<TestState>(TestState.Running);

        Assert.IsTrue(sm.IsInAnyState(TestState.Idle, TestState.Running, TestState.Jumping));
        Assert.IsFalse(sm.IsInAnyState(TestState.Jumping, TestState.Falling));
    }

    public void TestOnUpdateCallback()
    {
        float deltaReceived = 0;
        var sm = new StateMachine<TestState>(TestState.Idle)
            .OnUpdate(TestState.Idle, (delta) => deltaReceived = delta);

        sm.Update(0.016f);

        Assert.AreEqual(0.016f, deltaReceived);
    }

    public void TestChainedConfiguration()
    {
        var enterCount = 0;
        var exitCount = 0;

        var sm = new StateMachine<TestState>(TestState.Idle)
            .OnEnter(TestState.Running, () => enterCount++)
            .OnExit(TestState.Idle, () => exitCount++)
            .OnEnter(TestState.Jumping, () => enterCount++);

        sm.TransitionTo(TestState.Running);

        Assert.AreEqual(1, enterCount);
        Assert.AreEqual(1, exitCount);
    }
}
