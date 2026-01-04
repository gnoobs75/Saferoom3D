using Godot;
using SafeRoom3D.Items;
using SafeRoom3D.Enemies;

namespace SafeRoom3D.UI;

/// <summary>
/// UI panel for looting corpses. Shows 8 slots and allows taking items.
/// </summary>
public partial class LootUI3D : Control
{
    public static LootUI3D? Instance { get; private set; }

    private Corpse3D? _currentCorpse;
    private Panel? _panel;
    private Label? _titleLabel;
    private GridContainer? _slotsGrid;
    private Button? _takeAllButton;
    private Button? _closeButton;
    private Button[] _slotButtons = new Button[LootBag.DefaultSlots];

    // Window dragging
    private const string WindowName = "LootUI";
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Control? _dragHeader;

    public override void _Ready()
    {
        Instance = this;

        // Process when paused
        ProcessMode = ProcessModeEnum.Always;

        // Start hidden
        Visible = false;

        CreateUI();

        GD.Print("[LootUI3D] Ready");
    }

    private void CreateUI()
    {
        // Center anchor
        AnchorsPreset = (int)LayoutPreset.Center;
        SetAnchorsPreset(LayoutPreset.Center);

        // Main panel
        _panel = new Panel();
        _panel.CustomMinimumSize = new Vector2(400, 350);
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.Position = new Vector2(-200, -175);

        // Style the panel
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        panelStyle.BorderColor = new Color(0.6f, 0.5f, 0.3f);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.SetCornerRadiusAll(8);
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_panel);

        // Draggable header area
        _dragHeader = new Control();
        _dragHeader.Position = new Vector2(0, 0);
        _dragHeader.Size = new Vector2(400, 45);
        _dragHeader.MouseFilter = MouseFilterEnum.Stop;
        _dragHeader.GuiInput += OnHeaderGuiInput;
        _panel.AddChild(_dragHeader);

        // Title label
        _titleLabel = new Label();
        _titleLabel.Text = "Loot (drag to move)";
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.Position = new Vector2(0, 10);
        _titleLabel.Size = new Vector2(400, 30);
        _titleLabel.AddThemeFontSizeOverride("font_size", 24);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        _panel.AddChild(_titleLabel);

        // Slots grid (4x2)
        _slotsGrid = new GridContainer();
        _slotsGrid.Columns = 4;
        _slotsGrid.Position = new Vector2(20, 50);
        _slotsGrid.Size = new Vector2(360, 180);
        _slotsGrid.AddThemeConstantOverride("h_separation", 10);
        _slotsGrid.AddThemeConstantOverride("v_separation", 10);
        _panel.AddChild(_slotsGrid);

        // Create slot buttons
        for (int i = 0; i < LootBag.DefaultSlots; i++)
        {
            var slotButton = CreateSlotButton(i);
            _slotsGrid.AddChild(slotButton);
            _slotButtons[i] = slotButton;
        }

        // Take All button
        _takeAllButton = new Button();
        _takeAllButton.Text = "Take All";
        _takeAllButton.Position = new Vector2(50, 250);
        _takeAllButton.Size = new Vector2(120, 40);
        _takeAllButton.Pressed += OnTakeAllPressed;
        StyleButton(_takeAllButton);
        _panel.AddChild(_takeAllButton);

        // Close button
        _closeButton = new Button();
        _closeButton.Text = "Close";
        _closeButton.Position = new Vector2(230, 250);
        _closeButton.Size = new Vector2(120, 40);
        _closeButton.Pressed += OnClosePressed;
        StyleButton(_closeButton);
        _panel.AddChild(_closeButton);

        // Keybind hint
        var hintLabel = new Label();
        hintLabel.Text = "[T] to close";
        hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hintLabel.Position = new Vector2(0, 310);
        hintLabel.Size = new Vector2(400, 20);
        hintLabel.AddThemeFontSizeOverride("font_size", 12);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        _panel.AddChild(hintLabel);
    }

    private Button CreateSlotButton(int index)
    {
        var button = new Button();
        button.CustomMinimumSize = new Vector2(80, 80);
        button.ClipText = true;

        // Store index in metadata
        button.SetMeta("slot_index", index);
        button.Pressed += () => OnSlotPressed(index);

        // Style
        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.15f, 0.15f, 0.18f);
        normalStyle.BorderColor = new Color(0.4f, 0.4f, 0.45f);
        normalStyle.SetBorderWidthAll(1);
        normalStyle.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = normalStyle.Duplicate() as StyleBoxFlat;
        hoverStyle!.BgColor = new Color(0.2f, 0.2f, 0.25f);
        hoverStyle.BorderColor = new Color(0.7f, 0.6f, 0.4f);
        button.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = normalStyle.Duplicate() as StyleBoxFlat;
        pressedStyle!.BgColor = new Color(0.25f, 0.22f, 0.18f);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);

        return button;
    }

    private void StyleButton(Button button)
    {
        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.2f, 0.18f, 0.15f);
        normalStyle.BorderColor = new Color(0.6f, 0.5f, 0.3f);
        normalStyle.SetBorderWidthAll(1);
        normalStyle.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = normalStyle.Duplicate() as StyleBoxFlat;
        hoverStyle!.BgColor = new Color(0.3f, 0.25f, 0.18f);
        button.AddThemeStyleboxOverride("hover", hoverStyle);

        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
    }

    private void OnHeaderGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _isDragging = true;
                _dragOffset = GetGlobalMousePosition() - _panel!.GlobalPosition;
            }
            else
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    WindowPositionManager.SetPosition(WindowName, _panel!.Position);
                }
            }
        }
        else if (@event is InputEventMouseMotion && _isDragging && _panel != null)
        {
            var newPos = GetGlobalMousePosition() - _dragOffset;
            var viewportSize = GetViewportRect().Size;
            var panelSize = _panel.Size;
            _panel.Position = WindowPositionManager.ClampToViewport(newPos, viewportSize, panelSize);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        // Stop drag if mouse released anywhere
        if (_isDragging && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
        {
            _isDragging = false;
            if (_panel != null)
            {
                WindowPositionManager.SetPosition(WindowName, _panel.Position);
            }
        }

        // Close on T or ESC
        if (@event.IsActionPressed("interact") || @event.IsActionPressed("escape"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// Open the loot window for a corpse
    /// </summary>
    public void Open(Corpse3D corpse)
    {
        _currentCorpse = corpse;

        // Update title
        if (_titleLabel != null)
        {
            _titleLabel.Text = $"{corpse.MonsterType} Corpse (drag to move)";
        }

        // Update slots
        RefreshSlots();

        // Show and pause
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Restore or center position
        if (_panel != null)
        {
            var viewportSize = GetViewportRect().Size;
            var panelSize = _panel.Size;
            var storedPos = WindowPositionManager.GetPosition(WindowName);
            if (storedPos == WindowPositionManager.CenterMarker)
            {
                _panel.Position = new Vector2(
                    (viewportSize.X - panelSize.X) / 2,
                    (viewportSize.Y - panelSize.Y) / 2
                );
            }
            else
            {
                _panel.Position = WindowPositionManager.ClampToViewport(storedPos, viewportSize, panelSize);
            }
        }

        // Lock player mouse control
        if (Player.FPSController.Instance != null)
        {
            Player.FPSController.Instance.MouseControlLocked = true;
        }

        GD.Print($"[LootUI3D] Opened loot window for {corpse.MonsterType}");
    }

    /// <summary>
    /// Close the loot window
    /// </summary>
    public void Close()
    {
        if (_currentCorpse != null)
        {
            _currentCorpse.OnLootingComplete();
        }

        _currentCorpse = null;
        Visible = false;

        // Restore mouse capture
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // Unlock player mouse control
        if (Player.FPSController.Instance != null)
        {
            Player.FPSController.Instance.MouseControlLocked = false;
        }

        GD.Print("[LootUI3D] Closed loot window");
    }

    private void RefreshSlots()
    {
        if (_currentCorpse == null) return;

        for (int i = 0; i < LootBag.DefaultSlots; i++)
        {
            var item = _currentCorpse.Loot.GetItem(i);
            UpdateSlotButton(i, item);
        }
    }

    private void UpdateSlotButton(int index, InventoryItem? item)
    {
        var button = _slotButtons[index];
        if (button == null) return;

        // Clear existing children
        foreach (var child in button.GetChildren())
        {
            child.QueueFree();
        }

        if (item == null)
        {
            button.Text = "";
            button.Disabled = true;
            return;
        }

        button.Text = "";
        button.Disabled = false;

        // Create item display
        var container = new VBoxContainer();
        container.SetAnchorsPreset(LayoutPreset.FullRect);
        container.AddThemeConstantOverride("separation", 2);
        button.AddChild(container);

        // Icon (colored box for now)
        var iconRect = new ColorRect();
        iconRect.CustomMinimumSize = new Vector2(40, 40);
        iconRect.Color = ItemDatabase.GetItemColor(item.Id);
        iconRect.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        container.AddChild(iconRect);

        // Stack count
        if (item.StackCount > 1)
        {
            var countLabel = new Label();
            countLabel.Text = item.StackCount.ToString();
            countLabel.HorizontalAlignment = HorizontalAlignment.Center;
            countLabel.AddThemeFontSizeOverride("font_size", 12);
            container.AddChild(countLabel);
        }

        // Tooltip
        button.TooltipText = $"{item.Name}\n{item.Description}";
    }

    private void OnSlotPressed(int index)
    {
        if (_currentCorpse == null) return;

        var item = _currentCorpse.Loot.TakeItem(index);
        if (item == null) return;

        // Add to player inventory
        if (Inventory3D.Instance != null)
        {
            bool added = Inventory3D.Instance.AddItem(item);
            if (added)
            {
                GD.Print($"[LootUI3D] Picked up {item.Name} x{item.StackCount}");
                Core.SoundManager3D.Instance?.PlayPickupSound(Player.FPSController.Instance?.GlobalPosition ?? Vector3.Zero);
            }
            else
            {
                // Put it back if inventory is full
                _currentCorpse.Loot.SetItem(index, item);
                GD.Print("[LootUI3D] Inventory full!");
            }
        }

        RefreshSlots();

        // Close if empty
        if (_currentCorpse.Loot.IsEmpty())
        {
            Close();
        }
    }

    private void OnTakeAllPressed()
    {
        if (_currentCorpse == null) return;

        bool tookSomething = false;
        for (int i = 0; i < LootBag.DefaultSlots; i++)
        {
            var item = _currentCorpse.Loot.GetItem(i);
            if (item == null) continue;

            if (Inventory3D.Instance?.AddItem(item) == true)
            {
                _currentCorpse.Loot.SetItem(i, null);
                tookSomething = true;
                GD.Print($"[LootUI3D] Picked up {item.Name} x{item.StackCount}");
            }
        }

        if (tookSomething)
        {
            Core.SoundManager3D.Instance?.PlayPickupSound(Player.FPSController.Instance?.GlobalPosition ?? Vector3.Zero);
        }

        RefreshSlots();

        // Close if empty
        if (_currentCorpse.Loot.IsEmpty())
        {
            Close();
        }
    }

    private void OnClosePressed()
    {
        Close();
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}
