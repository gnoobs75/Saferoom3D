using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.UI;

/// <summary>
/// Manages window positions for draggable UI panels.
/// Stores positions in memory so windows remember their last position during a session.
/// </summary>
public static class WindowPositionManager
{
    // Store window positions by window name
    private static readonly Dictionary<string, Vector2> _positions = new();

    // Default center position marker
    public static readonly Vector2 CenterMarker = new Vector2(-99999, -99999);

    /// <summary>
    /// Get the stored position for a window, or CenterMarker if not set.
    /// </summary>
    public static Vector2 GetPosition(string windowName)
    {
        return _positions.TryGetValue(windowName, out var pos) ? pos : CenterMarker;
    }

    /// <summary>
    /// Store a window's position.
    /// </summary>
    public static void SetPosition(string windowName, Vector2 position)
    {
        _positions[windowName] = position;
    }

    /// <summary>
    /// Check if a window has a stored position.
    /// </summary>
    public static bool HasPosition(string windowName)
    {
        return _positions.ContainsKey(windowName);
    }

    /// <summary>
    /// Clear the stored position for a window (reset to center).
    /// </summary>
    public static void ClearPosition(string windowName)
    {
        _positions.Remove(windowName);
    }

    /// <summary>
    /// Clear all stored positions.
    /// </summary>
    public static void ClearAll()
    {
        _positions.Clear();
    }

    /// <summary>
    /// Calculate centered position for a panel of given size.
    /// </summary>
    public static Vector2 GetCenteredPosition(Vector2 viewportSize, Vector2 panelSize)
    {
        return new Vector2(
            (viewportSize.X - panelSize.X) / 2,
            (viewportSize.Y - panelSize.Y) / 2
        );
    }

    /// <summary>
    /// Clamp a position to keep the window within viewport bounds.
    /// </summary>
    public static Vector2 ClampToViewport(Vector2 position, Vector2 viewportSize, Vector2 panelSize)
    {
        // Keep at least 50 pixels of the window visible
        const float minVisible = 50f;

        float minX = minVisible - panelSize.X;
        float maxX = viewportSize.X - minVisible;
        float minY = 0; // Don't let title bar go above screen
        float maxY = viewportSize.Y - minVisible;

        return new Vector2(
            Mathf.Clamp(position.X, minX, maxX),
            Mathf.Clamp(position.Y, minY, maxY)
        );
    }
}

/// <summary>
/// Helper class to add drag functionality to a panel.
/// Attach to the main panel's header area.
/// </summary>
public partial class DraggableWindowHeader : Control
{
    private PanelContainer _targetPanel;
    private string _windowName;
    private bool _isDragging;
    private Vector2 _dragOffset;

    public DraggableWindowHeader(PanelContainer targetPanel, string windowName)
    {
        _targetPanel = targetPanel;
        _windowName = windowName;

        // Make this control cover the header area
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(0, 40); // Header height
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    // Start dragging
                    _isDragging = true;
                    _dragOffset = GetGlobalMousePosition() - _targetPanel.GlobalPosition;
                    AcceptEvent();
                }
                else
                {
                    // Stop dragging
                    if (_isDragging)
                    {
                        _isDragging = false;
                        // Save final position
                        WindowPositionManager.SetPosition(_windowName, _targetPanel.Position);
                    }
                }
            }
        }
        else if (@event is InputEventMouseMotion && _isDragging)
        {
            // Update panel position
            var newPos = GetGlobalMousePosition() - _dragOffset;

            // Clamp to viewport
            var viewportSize = GetViewportRect().Size;
            var panelSize = _targetPanel.Size;
            newPos = WindowPositionManager.ClampToViewport(newPos, viewportSize, panelSize);

            _targetPanel.Position = newPos;
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Cancel drag on escape or if we lose focus
        if (_isDragging && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
        {
            _isDragging = false;
            WindowPositionManager.SetPosition(_windowName, _targetPanel.Position);
        }
    }
}
