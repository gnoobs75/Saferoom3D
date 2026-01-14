using Godot;
using System.Collections.Generic;
using SafeRoom3D.Core;

namespace SafeRoom3D.Tests;

/// <summary>
/// Unit tests for the Save/Load system.
/// </summary>
public class SaveSystemTests
{
    public void TestSaveHelperVector3()
    {
        var original = new Vector3(1.5f, 2.5f, 3.5f);

        var serialized = SaveHelpers.SerializeVector3(original);
        var deserialized = SaveHelpers.DeserializeVector3(serialized);

        Assert.AreEqual(original.X, deserialized.X);
        Assert.AreEqual(original.Y, deserialized.Y);
        Assert.AreEqual(original.Z, deserialized.Z);
    }

    public void TestSaveHelperVector2()
    {
        var original = new Vector2(1.5f, 2.5f);

        var serialized = SaveHelpers.SerializeVector2(original);
        var deserialized = SaveHelpers.DeserializeVector2(serialized);

        Assert.AreEqual(original.X, deserialized.X);
        Assert.AreEqual(original.Y, deserialized.Y);
    }

    public void TestSaveHelperColor()
    {
        var original = new Color(0.5f, 0.6f, 0.7f, 0.8f);

        var serialized = SaveHelpers.SerializeColor(original);
        var deserialized = SaveHelpers.DeserializeColor(serialized);

        Assert.AreEqual(original.R, deserialized.R);
        Assert.AreEqual(original.G, deserialized.G);
        Assert.AreEqual(original.B, deserialized.B);
        Assert.AreEqual(original.A, deserialized.A);
    }

    public void TestSaveManagerSingleton()
    {
        var manager1 = SaveManager.Instance;
        var manager2 = SaveManager.Instance;

        Assert.AreEqual(manager1, manager2, "SaveManager should be singleton");
    }

    public void TestRegisterSaveable()
    {
        var manager = SaveManager.Instance;
        var testSaveable = new TestSaveable("test1");

        manager.Register(testSaveable);

        // Should not throw on double registration
        manager.Register(testSaveable);

        manager.Unregister(testSaveable);
    }

    public void TestISaveableInterface()
    {
        var saveable = new TestSaveable("test_id");

        Assert.AreEqual("test_id", saveable.SaveId);

        var data = saveable.Save();
        Assert.IsNotNull(data);
        Assert.IsTrue(data.ContainsKey("value"));
    }

    // Test implementation of ISaveable
    private class TestSaveable : ISaveable
    {
        public string SaveId { get; }
        public int Value { get; set; } = 42;

        public TestSaveable(string id) => SaveId = id;

        public Dictionary<string, object> Save()
        {
            return new Dictionary<string, object>
            {
                ["value"] = Value
            };
        }

        public void Load(Dictionary<string, object> data)
        {
            if (data.TryGetValue("value", out var value))
            {
                Value = System.Convert.ToInt32(value);
            }
        }
    }
}
