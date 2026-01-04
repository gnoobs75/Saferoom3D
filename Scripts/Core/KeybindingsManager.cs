using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Core;

/// <summary>
/// Manages saving and loading of custom keybindings.
/// </summary>
public static class KeybindingsManager
{
    private const string SettingsPath = "user://keybindings.cfg";

    // List of actions that can be rebound
    private static readonly string[] RebindableActions = new[]
    {
        "move_forward", "move_back", "move_left", "move_right",
        "jump", "sprint", "attack", "attack_alt",
        "interact", "loot", "toggle_spellbook", "toggle_inventory",
        "toggle_map", "escape", "ability_q", "ability_r", "ability_f", "ability_v"
    };

    /// <summary>
    /// Save current keybindings to file
    /// </summary>
    public static void SaveKeybindings()
    {
        var config = new ConfigFile();

        foreach (var action in RebindableActions)
        {
            var events = InputMap.ActionGetEvents(action);
            if (events.Count > 0)
            {
                var ev = events[0];
                if (ev is InputEventKey key)
                {
                    config.SetValue("keybindings", action + "_type", "key");
                    config.SetValue("keybindings", action + "_physical", (long)key.PhysicalKeycode);
                    config.SetValue("keybindings", action + "_keycode", (long)key.Keycode);
                }
                else if (ev is InputEventMouseButton mouse)
                {
                    config.SetValue("keybindings", action + "_type", "mouse");
                    config.SetValue("keybindings", action + "_button", (int)mouse.ButtonIndex);
                }
            }
        }

        var error = config.Save(SettingsPath);
        if (error == Error.Ok)
        {
            GD.Print("[KeybindingsManager] Keybindings saved to " + SettingsPath);
        }
        else
        {
            GD.PrintErr($"[KeybindingsManager] Failed to save keybindings: {error}");
        }
    }

    /// <summary>
    /// Load keybindings from file
    /// </summary>
    public static void LoadKeybindings()
    {
        var config = new ConfigFile();
        var error = config.Load(SettingsPath);

        if (error != Error.Ok)
        {
            GD.Print("[KeybindingsManager] No keybindings file found, using defaults");
            return;
        }

        foreach (var action in RebindableActions)
        {
            if (!config.HasSectionKey("keybindings", action + "_type"))
                continue;

            var type = (string)config.GetValue("keybindings", action + "_type");

            // Clear existing events
            InputMap.ActionEraseEvents(action);

            if (type == "key")
            {
                var physical = (Key)(long)config.GetValue("keybindings", action + "_physical", (long)Key.None);
                var keycode = (Key)(long)config.GetValue("keybindings", action + "_keycode", (long)Key.None);

                var keyEvent = new InputEventKey();
                keyEvent.PhysicalKeycode = physical;
                keyEvent.Keycode = keycode;
                InputMap.ActionAddEvent(action, keyEvent);

                GD.Print($"[KeybindingsManager] Loaded {action} = {OS.GetKeycodeString(physical)}");
            }
            else if (type == "mouse")
            {
                var button = (MouseButton)(int)config.GetValue("keybindings", action + "_button", (int)MouseButton.Left);

                var mouseEvent = new InputEventMouseButton();
                mouseEvent.ButtonIndex = button;
                InputMap.ActionAddEvent(action, mouseEvent);

                GD.Print($"[KeybindingsManager] Loaded {action} = Mouse{(int)button}");
            }
        }

        GD.Print("[KeybindingsManager] Keybindings loaded from " + SettingsPath);
    }

    /// <summary>
    /// Reset all keybindings to defaults
    /// </summary>
    public static void ResetToDefaults()
    {
        // This would reload from project.godot
        // For now, we just delete the settings file
        var dir = DirAccess.Open("user://");
        if (dir != null && dir.FileExists("keybindings.cfg"))
        {
            dir.Remove("keybindings.cfg");
            GD.Print("[KeybindingsManager] Keybindings reset to defaults");
        }

        // Reload from project (would need to restart game for full effect)
        InputMap.LoadFromProjectSettings();
    }
}
