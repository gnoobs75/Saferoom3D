using Godot;
using System.Collections.Generic;
using SafeRoom3D.Broadcaster;

namespace SafeRoom3D.UI;

/// <summary>
/// UI Editor Mode - Press X to enter, drag any HUD element to reposition it.
/// Positions are saved automatically and persist between sessions.
/// </summary>
public partial class UIEditorMode : CanvasLayer
{
    public static UIEditorMode? Instance { get; private set; }

    public bool IsActive { get; private set; }

    // UI elements
    private ColorRect? _overlay;
    private Label? _instructionLabel;
    private readonly List<DragHandle> _dragHandles = new();

    public override void _Ready()
    {
        Instance = this;
        Layer = 150; // Above everything
        ProcessMode = ProcessModeEnum.Always;

        CreateOverlay();

        GD.Print("[UIEditorMode] Ready - Press X to edit UI layout");
    }

    private void CreateOverlay()
    {
        // Semi-transparent overlay
        _overlay = new ColorRect();
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Color = new Color(0, 0, 0, 0.5f);
        _overlay.Visible = false;
        _overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_overlay);

        // Instruction label at top
        _instructionLabel = new Label();
        _instructionLabel.Text = "UI EDITOR - Drag elements to reposition | [X] Save & Exit | [Shift+X] Reset All";
        _instructionLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _instructionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _instructionLabel.Position = new Vector2(0, 20);
        _instructionLabel.AddThemeFontSizeOverride("font_size", 22);
        _instructionLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.3f));
        _instructionLabel.Visible = false;
        AddChild(_instructionLabel);
    }

    public override void _Input(InputEvent @event)
    {
        // Don't process input when not visible (CanvasLayer visibility check)
        if (!Visible) return;

        // Only process X key when no other modal UIs are open
        if (EscapeMenu3D.Instance?.Visible == true) return;
        if (EditorScreen3D.Instance?.Visible == true) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.X || keyEvent.PhysicalKeycode == Key.X)
            {
                if (keyEvent.ShiftPressed && IsActive)
                {
                    // Reset all positions
                    ResetAllPositions();
                }
                else
                {
                    // Toggle editor mode
                    if (IsActive)
                        Deactivate();
                    else
                        Activate();
                }
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;

        // Pause game
        GetTree().Paused = true;

        // Show cursor
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Show overlay
        if (_overlay != null) _overlay.Visible = true;
        if (_instructionLabel != null) _instructionLabel.Visible = true;

        // Create drag handles for all HUD elements
        CreateDragHandles();

        GD.Print("[UIEditorMode] Activated");
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;

        // Save all positions
        SaveAllPositions();

        // Remove drag handles
        ClearDragHandles();

        // Hide overlay
        if (_overlay != null) _overlay.Visible = false;
        if (_instructionLabel != null) _instructionLabel.Visible = false;

        // Unpause game
        GetTree().Paused = false;

        // Capture cursor
        Input.MouseMode = Input.MouseModeEnum.Captured;

        GD.Print("[UIEditorMode] Deactivated - Positions saved");
    }

    private void CreateDragHandles()
    {
        ClearDragHandles();

        // Get HUD3D elements - search by Name set in creation
        var hud = HUD3D.Instance;
        if (hud != null)
        {
            // Find elements by their assigned names
            CreateHandleForElement(hud, "ActionBarPanel", "HUD_ActionBarPanel", "Action Bar");
            CreateHandleForElement(hud, "HealthManaPanel", "HUD_HealthManaPanel", "Health/Mana");
            CreateHandleForElement(hud, "TargetFrame", "HUD_TargetFrame", "Target Frame");
            CreateHandleForElement(hud, "ChatWindow", "HUD_ChatWindow", "Combat Log");
            CreateHandleForElement(hud, "MinimapOuterFrame", "HUD_MinimapOuterFrame", "Minimap");
            CreateHandleForElement(hud, "Compass", "HUD_Compass", "Compass");
            CreateHandleForElement(hud, "ShortcutIcons", "HUD_ShortcutIcons", "Shortcuts");
        }

        // Get AI Broadcaster
        var broadcaster = AIBroadcaster.Instance?.UI;
        if (broadcaster != null)
        {
            var broadcasterPanel = broadcaster.FindChild("BroadcasterPanel", true, false) as Control;
            if (broadcasterPanel != null)
            {
                var handle = new DragHandle(broadcasterPanel, "HUD_AIBroadcaster", "AI Broadcaster");
                AddChild(handle);
                _dragHandles.Add(handle);
            }

            // View Counter Panel (metrics)
            var viewCounterPanel = broadcaster.FindChild("ViewCounterPanel", true, false) as Control;
            if (viewCounterPanel != null)
            {
                var handle = new DragHandle(viewCounterPanel, "HUD_ViewCounterPanel", "View Counter");
                AddChild(handle);
                _dragHandles.Add(handle);
            }
        }

        // Get InspectMode Description Panel
        var inspectMode = InspectMode3D.Instance;
        if (inspectMode != null)
        {
            var descPanel = inspectMode.FindChild("DescriptionPanel", true, false) as Control;
            if (descPanel != null)
            {
                // Make it visible temporarily so it can be dragged
                descPanel.Visible = true;
                var handle = new DragHandle(descPanel, "HUD_DescriptionPanel", "Description");
                AddChild(handle);
                _dragHandles.Add(handle);
            }
        }

        // Get Dungeon Radio Panel
        var radio = DungeonRadio.Instance;
        if (radio != null)
        {
            var radioPanel = radio.FindChild("RadioPanel", true, false) as Control;
            if (radioPanel != null)
            {
                // Make it visible temporarily so it can be dragged
                radioPanel.Visible = true;
                var handle = new DragHandle(radioPanel, "HUD_DungeonRadio", "Dungeon Radio");
                AddChild(handle);
                _dragHandles.Add(handle);
            }
        }

        // Get Shop UI Panel
        var shopUI = ShopUI3D.Instance;
        if (shopUI != null)
        {
            var shopPanel = shopUI.GetMainPanel();
            if (shopPanel != null)
            {
                // Make it visible temporarily so it can be dragged
                shopPanel.Visible = true;
                shopUI.Visible = true;
                var handle = new DragHandle(shopPanel, "ShopUI", "Shop Window");
                AddChild(handle);
                _dragHandles.Add(handle);
            }
        }

        GD.Print($"[UIEditorMode] Created {_dragHandles.Count} drag handles");
    }

    private void CreateHandleForElement(Node parent, string nodeName, string positionKey, string displayName)
    {
        var node = parent.FindChild(nodeName, true, false) as Control;
        if (node != null)
        {
            var handle = new DragHandle(node, positionKey, displayName);
            AddChild(handle);
            _dragHandles.Add(handle);
            GD.Print($"[UIEditorMode] Created handle for {displayName}");
        }
        else
        {
            GD.PrintErr($"[UIEditorMode] Could not find element: {nodeName}");
        }
    }

    private void ClearDragHandles()
    {
        foreach (var handle in _dragHandles)
        {
            handle.QueueFree();
        }
        _dragHandles.Clear();
    }

    private void SaveAllPositions()
    {
        foreach (var handle in _dragHandles)
        {
            handle.SavePosition();
        }
        GD.Print("[UIEditorMode] All positions saved to disk");
    }

    private void ResetAllPositions()
    {
        // Clear all HUD positions
        WindowPositionManager.ClearAll();

        // Notify user
        GD.Print("[UIEditorMode] All positions reset to defaults - restart game to apply");

        // Deactivate and let user restart
        Deactivate();
    }

    public override void _ExitTree()
    {
        if (IsActive)
        {
            Deactivate();
        }
        Instance = null;
    }
}

/// <summary>
/// Drag handle that appears over a UI element during editor mode.
/// </summary>
public partial class DragHandle : Control
{
    private Control _target;
    private string _positionKey;
    private string _displayName;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Label? _nameLabel;

    public DragHandle(Control target, string positionKey, string displayName)
    {
        _target = target;
        _positionKey = positionKey;
        _displayName = displayName;

        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;

        // Position over target
        GlobalPosition = target.GlobalPosition;
        Size = target.Size;
    }

    public override void _Ready()
    {
        // Name label
        _nameLabel = new Label();
        _nameLabel.Text = _displayName;
        _nameLabel.AddThemeFontSizeOverride("font_size", 14);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.5f));
        _nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _nameLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _nameLabel.Position = new Vector2(8, 4);
        AddChild(_nameLabel);
    }

    public override void _Process(double delta)
    {
        // Keep synced with target position and size
        if (_target != null && GodotObject.IsInstanceValid(_target))
        {
            GlobalPosition = _target.GlobalPosition;
            Size = _target.Size;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        // Draw border
        var borderColor = _isDragging ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.8f, 0.2f);
        DrawRect(new Rect2(Vector2.Zero, Size), borderColor, false, 3f);

        // Draw fill
        var fillColor = _isDragging ? new Color(0.3f, 1f, 0.3f, 0.2f) : new Color(1f, 0.8f, 0.2f, 0.15f);
        DrawRect(new Rect2(Vector2.Zero, Size), fillColor, true);

        // Draw corner handles
        float handleSize = 12f;
        var handleColor = _isDragging ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.9f, 0.3f);

        DrawRect(new Rect2(0, 0, handleSize, handleSize), handleColor, true);
        DrawRect(new Rect2(Size.X - handleSize, 0, handleSize, handleSize), handleColor, true);
        DrawRect(new Rect2(0, Size.Y - handleSize, handleSize, handleSize), handleColor, true);
        DrawRect(new Rect2(Size.X - handleSize, Size.Y - handleSize, handleSize, handleSize), handleColor, true);

        // Draw move icon in center
        var center = Size / 2;
        float iconSize = 20f;
        DrawLine(new Vector2(center.X - iconSize, center.Y), new Vector2(center.X + iconSize, center.Y), handleColor, 2f);
        DrawLine(new Vector2(center.X, center.Y - iconSize), new Vector2(center.X, center.Y + iconSize), handleColor, 2f);
        // Arrow heads
        DrawLine(new Vector2(center.X - iconSize, center.Y), new Vector2(center.X - iconSize + 6, center.Y - 6), handleColor, 2f);
        DrawLine(new Vector2(center.X - iconSize, center.Y), new Vector2(center.X - iconSize + 6, center.Y + 6), handleColor, 2f);
        DrawLine(new Vector2(center.X + iconSize, center.Y), new Vector2(center.X + iconSize - 6, center.Y - 6), handleColor, 2f);
        DrawLine(new Vector2(center.X + iconSize, center.Y), new Vector2(center.X + iconSize - 6, center.Y + 6), handleColor, 2f);
        DrawLine(new Vector2(center.X, center.Y - iconSize), new Vector2(center.X - 6, center.Y - iconSize + 6), handleColor, 2f);
        DrawLine(new Vector2(center.X, center.Y - iconSize), new Vector2(center.X + 6, center.Y - iconSize + 6), handleColor, 2f);
        DrawLine(new Vector2(center.X, center.Y + iconSize), new Vector2(center.X - 6, center.Y + iconSize - 6), handleColor, 2f);
        DrawLine(new Vector2(center.X, center.Y + iconSize), new Vector2(center.X + 6, center.Y + iconSize - 6), handleColor, 2f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _isDragging = true;
                _dragOffset = GetGlobalMousePosition() - _target.GlobalPosition;
                AcceptEvent();
            }
            else if (_isDragging)
            {
                _isDragging = false;
                SavePosition();
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion && _isDragging)
        {
            var newPos = GetGlobalMousePosition() - _dragOffset;

            // Clamp to viewport
            var viewportSize = GetViewportRect().Size;
            newPos = WindowPositionManager.ClampToViewport(newPos, viewportSize, _target.Size);

            _target.GlobalPosition = newPos;
            AcceptEvent();
        }
    }

    public void SavePosition()
    {
        if (_target != null)
        {
            WindowPositionManager.SetPosition(_positionKey, _target.Position);
            GD.Print($"[DragHandle] Saved {_displayName} position: {_target.Position}");
        }
    }
}
