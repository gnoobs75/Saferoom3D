using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SafeRoom3D.Tests;

/// <summary>
/// Simple test runner for unit tests in Godot.
/// Run tests by instantiating this node or calling RunAllTests().
/// </summary>
public partial class TestRunner : Node
{
    private int _passCount;
    private int _failCount;
    private readonly List<string> _failures = new();

    public override void _Ready()
    {
        GD.Print("=== SafeRoom3D Test Runner ===\n");
        RunAllTests();
    }

    public void RunAllTests()
    {
        _passCount = 0;
        _failCount = 0;
        _failures.Clear();

        // Run all test classes
        RunTestClass<StateMachineTests>();
        RunTestClass<CharacterStatsTests>();
        RunTestClass<SaveSystemTests>();
        RunTestClass<DataLoaderTests>();

        // Print summary
        GD.Print("\n=== Test Summary ===");
        GD.Print($"Passed: {_passCount}");
        GD.Print($"Failed: {_failCount}");

        if (_failures.Count > 0)
        {
            GD.Print("\nFailures:");
            foreach (var failure in _failures)
            {
                GD.Print($"  - {failure}");
            }
        }

        GD.Print($"\nTotal: {_passCount + _failCount} tests");
    }

    private void RunTestClass<T>() where T : new()
    {
        var testClass = new T();
        var type = typeof(T);

        GD.Print($"\n[{type.Name}]");

        // Find all methods with [Test] attribute or starting with "Test"
        foreach (var method in type.GetMethods())
        {
            if (method.Name.StartsWith("Test") && method.GetParameters().Length == 0)
            {
                RunTest(testClass, method);
            }
        }
    }

    private void RunTest(object testInstance, MethodInfo method)
    {
        try
        {
            method.Invoke(testInstance, null);
            _passCount++;
            GD.Print($"  ✓ {method.Name}");
        }
        catch (TargetInvocationException ex)
        {
            _failCount++;
            var message = ex.InnerException?.Message ?? ex.Message;
            _failures.Add($"{method.DeclaringType?.Name}.{method.Name}: {message}");
            GD.Print($"  ✗ {method.Name}: {message}");
        }
        catch (Exception ex)
        {
            _failCount++;
            _failures.Add($"{method.DeclaringType?.Name}.{method.Name}: {ex.Message}");
            GD.Print($"  ✗ {method.Name}: {ex.Message}");
        }
    }
}

/// <summary>
/// Assertion helper for tests.
/// </summary>
public static class Assert
{
    public static void IsTrue(bool condition, string message = "Expected true but was false")
    {
        if (!condition) throw new Exception(message);
    }

    public static void IsFalse(bool condition, string message = "Expected false but was true")
    {
        if (condition) throw new Exception(message);
    }

    public static void AreEqual<T>(T expected, T actual, string? message = null)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(message ?? $"Expected {expected} but got {actual}");
        }
    }

    public static void AreNotEqual<T>(T expected, T actual, string? message = null)
    {
        if (Equals(expected, actual))
        {
            throw new Exception(message ?? $"Expected values to be different but both were {actual}");
        }
    }

    public static void IsNull(object? obj, string message = "Expected null but was not null")
    {
        if (obj != null) throw new Exception(message);
    }

    public static void IsNotNull(object? obj, string message = "Expected not null but was null")
    {
        if (obj == null) throw new Exception(message);
    }

    public static void IsGreaterThan(float actual, float expected, string? message = null)
    {
        if (actual <= expected)
        {
            throw new Exception(message ?? $"Expected {actual} to be greater than {expected}");
        }
    }

    public static void IsLessThan(float actual, float expected, string? message = null)
    {
        if (actual >= expected)
        {
            throw new Exception(message ?? $"Expected {actual} to be less than {expected}");
        }
    }

    public static void Throws<TException>(Action action, string message = "Expected exception but none was thrown")
        where TException : Exception
    {
        try
        {
            action();
            throw new Exception(message);
        }
        catch (TException)
        {
            // Expected
        }
    }
}
