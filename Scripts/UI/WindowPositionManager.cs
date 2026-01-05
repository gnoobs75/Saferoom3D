using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace SafeRoom3D.UI;

/// <summary>
/// Manages window positions for draggable UI panels.
/// Persists positions to disk so they're remembered between sessions.
/// </summary>
public static class WindowPositionManager
{
    // Store window positions by window name
    private static readonly Dictionary<string, Vector2> _positions = new();

    // Default center position marker
    public static readonly Vector2 CenterMarker = new Vector2(-99999, -99999);

    // File path for saving positions
    private const string PositionsFilePath = "user://ui_positions.json";

    // Track if we've loaded from disk
    private static bool _loaded = false;

    /// <summary>
    /// Get the stored position for a window, or CenterMarker if not set.
    /// Automatically loads from disk on first access.
    /// </summary>
    public static Vector2 GetPosition(string windowName)
    {
        if (!_loaded) LoadFromDisk();
        return _positions.TryGetValue(windowName, out var pos) ? pos : CenterMarker;
    }

    /// <summary>
    /// Store a window's position and auto-save to disk.
    /// </summary>
    public static void SetPosition(string windowName, Vector2 position)
    {
        if (!_loaded) LoadFromDisk();
        _positions[windowName] = position;
        SaveToDisk();
    }

    /// <summary>
    /// Check if a window has a stored position.
    /// </summary>
    public static bool HasPosition(string windowName)
    {
        if (!_loaded) LoadFromDisk();
        return _positions.ContainsKey(windowName);
    }

    /// <summary>
    /// Clear the stored position for a window (reset to default).
    /// </summary>
    public static void ClearPosition(string windowName)
    {
        _positions.Remove(windowName);
        SaveToDisk();
    }

    /// <summary>
    /// Clear all stored positions.
    /// </summary>
    public static void ClearAll()
    {
        _positions.Clear();
        SaveToDisk();
    }

    /// <summary>
    /// Save all positions to disk.
    /// </summary>
    public static void SaveToDisk()
    {
        try
        {
            var data = new Dictionary<string, float[]>();
            foreach (var kvp in _positions)
            {
                data[kvp.Key] = new float[] { kvp.Value.X, kvp.Value.Y };
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            using var file = FileAccess.Open(PositionsFilePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
                GD.Print($"[WindowPositionManager] Saved {_positions.Count} UI positions to disk");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[WindowPositionManager] Failed to save positions: {ex.Message}");
        }
    }

    /// <summary>
    /// Load positions from disk.
    /// </summary>
    public static void LoadFromDisk()
    {
        _loaded = true;

        if (!FileAccess.FileExists(PositionsFilePath))
        {
            GD.Print("[WindowPositionManager] No saved UI positions found");
            return;
        }

        try
        {
            using var file = FileAccess.Open(PositionsFilePath, FileAccess.ModeFlags.Read);
            if (file == null) return;

            string json = file.GetAsText();
            var data = JsonSerializer.Deserialize<Dictionary<string, float[]>>(json);

            if (data != null)
            {
                _positions.Clear();
                foreach (var kvp in data)
                {
                    if (kvp.Value.Length >= 2)
                    {
                        _positions[kvp.Key] = new Vector2(kvp.Value[0], kvp.Value[1]);
                    }
                }
                GD.Print($"[WindowPositionManager] Loaded {_positions.Count} UI positions from disk");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[WindowPositionManager] Failed to load positions: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all stored positions (for debugging/editing).
    /// </summary>
    public static IReadOnlyDictionary<string, Vector2> GetAllPositions()
    {
        if (!_loaded) LoadFromDisk();
        return _positions;
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
        ProcessMode = ProcessModeEnum.Always; // Work even when paused
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

/// <summary>
/// Overlay that enables dragging for HUD elements during UI edit mode.
/// Shows a visible border when active, becomes invisible when not editing.
/// </summary>
public partial class DraggableHUDOverlay : ColorRect
{
    private Control _targetElement;
    private string _elementName;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Label? _nameLabel;

    public bool IsEditMode { get; set; }

    public DraggableHUDOverlay(Control targetElement, string elementName)
    {
        _targetElement = targetElement;
        _elementName = elementName;

        // Cover the entire target element
        Position = Vector2.Zero;
        Size = targetElement.Size;

        // Transparent by default, colored border when editing
        Color = new Color(0, 0, 0, 0);

        MouseFilter = MouseFilterEnum.Ignore; // Ignore input when not editing
        ProcessMode = ProcessModeEnum.Always; // Work when paused

        // Name label (shown during edit mode)
        _nameLabel = new Label();
        _nameLabel.Text = elementName;
        _nameLabel.AddThemeFontSizeOverride("font_size", 11);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.5f));
        _nameLabel.Position = new Vector2(4, 2);
        _nameLabel.Visible = false;
        AddChild(_nameLabel);
    }

    public void SetEditMode(bool editing)
    {
        IsEditMode = editing;

        if (editing)
        {
            // Show colored border overlay
            Color = new Color(1f, 0.8f, 0.2f, 0.15f);
            MouseFilter = MouseFilterEnum.Stop;
            if (_nameLabel != null) _nameLabel.Visible = true;
        }
        else
        {
            // Hide overlay
            Color = new Color(0, 0, 0, 0);
            MouseFilter = MouseFilterEnum.Ignore;
            if (_nameLabel != null) _nameLabel.Visible = false;
            _isDragging = false;
        }
    }

    public override void _Process(double delta)
    {
        // Keep size synced with target
        if (_targetElement != null && Size != _targetElement.Size)
        {
            Size = _targetElement.Size;
        }
    }

    public override void _Draw()
    {
        if (IsEditMode)
        {
            // Draw border
            var rect = new Rect2(Vector2.Zero, Size);
            DrawRect(rect, new Color(1f, 0.8f, 0.2f, 0.8f), false, 2f);

            // Draw corner handles
            float handleSize = 8f;
            var handleColor = new Color(1f, 0.9f, 0.3f);

            // Top-left
            DrawRect(new Rect2(0, 0, handleSize, handleSize), handleColor);
            // Top-right
            DrawRect(new Rect2(Size.X - handleSize, 0, handleSize, handleSize), handleColor);
            // Bottom-left
            DrawRect(new Rect2(0, Size.Y - handleSize, handleSize, handleSize), handleColor);
            // Bottom-right
            DrawRect(new Rect2(Size.X - handleSize, Size.Y - handleSize, handleSize, handleSize), handleColor);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!IsEditMode) return;

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _isDragging = true;
                _dragOffset = GetGlobalMousePosition() - _targetElement.GlobalPosition;
                AcceptEvent();
            }
            else if (_isDragging)
            {
                _isDragging = false;
                WindowPositionManager.SetPosition(_elementName, _targetElement.Position);
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion && _isDragging)
        {
            var newPos = GetGlobalMousePosition() - _dragOffset;
            var viewportSize = GetViewportRect().Size;
            newPos = WindowPositionManager.ClampToViewport(newPos, viewportSize, _targetElement.Size);
            _targetElement.Position = newPos;
            AcceptEvent();
        }
    }
}
