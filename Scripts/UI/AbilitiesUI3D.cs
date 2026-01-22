using Godot;
using System.Collections.Generic;
using SafeRoom3D.Abilities;

namespace SafeRoom3D.UI;

/// <summary>
/// Abilities menu UI that displays all abilities (non-mana cooldown-based skills).
/// Supports drag-and-drop to action bars.
/// Separate from SpellBook which shows mana-based spells.
/// </summary>
public partial class AbilitiesUI3D : Control
{
    // Singleton
    public static AbilitiesUI3D? Instance { get; private set; }

    // UI Elements
    private PanelContainer? _mainPanel;
    private VBoxContainer? _contentContainer;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _abilityListContainer;
    private Label? _titleLabel;
    private Button? _closeButton;

    // Ability entries
    private readonly List<AbilityEntry> _entries = new();

    // Drag state
    private static string? _draggedAbilityId;
    private static Control? _dragPreview;

    // Panel size
    private const float PanelWidth = 420;
    private const float PanelHeight = 500;

    // Window dragging
    private const string WindowName = "AbilitiesMenu";
    private bool _isWindowDragging;
    private Vector2 _windowDragOffset;

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        // Cover full screen to block clicks
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        CreateUI();
        PopulateAbilityList();

        GD.Print("[AbilitiesUI3D] Ready");
    }

    private void CreateUI()
    {
        // Semi-transparent background
        var background = new ColorRect();
        background.Name = "Background";
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.Color = new Color(0, 0, 0, 0.5f);
        background.MouseFilter = MouseFilterEnum.Stop;
        AddChild(background);

        // Main panel
        _mainPanel = new PanelContainer();
        _mainPanel.Name = "AbilitiesPanel";
        _mainPanel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.15f, 0.12f, 0.98f);  // Greenish tint for abilities
        panelStyle.BorderColor = new Color(0.3f, 0.6f, 0.4f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetContentMarginAll(15);
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_mainPanel);

        // Content container
        _contentContainer = new VBoxContainer();
        _contentContainer.AddThemeConstantOverride("separation", 10);
        _mainPanel.AddChild(_contentContainer);

        // Header with title and close button
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        header.CustomMinimumSize = new Vector2(0, 40);
        header.MouseFilter = MouseFilterEnum.Stop;
        header.GuiInput += OnHeaderGuiInput;

        _titleLabel = new Label();
        _titleLabel.Text = "Abilities (drag to move)";
        _titleLabel.AddThemeFontSizeOverride("font_size", 24);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 1f, 0.9f));
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        header.AddChild(_titleLabel);

        _closeButton = new Button();
        _closeButton.Text = "X";
        _closeButton.CustomMinimumSize = new Vector2(32, 32);
        _closeButton.Pressed += () => Hide();
        header.AddChild(_closeButton);

        _contentContainer.AddChild(header);

        // Separator
        var separator = new HSeparator();
        _contentContainer.AddChild(separator);

        // Instruction label
        var instructions = new Label();
        instructions.Text = "Drag unlocked abilities to your action bar";
        instructions.AddThemeFontSizeOverride("font_size", 12);
        instructions.AddThemeColorOverride("font_color", new Color(0.65f, 0.8f, 0.7f));
        instructions.HorizontalAlignment = HorizontalAlignment.Center;
        _contentContainer.AddChild(instructions);

        // Scroll container
        _scrollContainer = new ScrollContainer();
        _scrollContainer.CustomMinimumSize = new Vector2(0, PanelHeight - 120);
        _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        _contentContainer.AddChild(_scrollContainer);

        _abilityListContainer = new VBoxContainer();
        _abilityListContainer.AddThemeConstantOverride("separation", 8);
        _abilityListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scrollContainer.AddChild(_abilityListContainer);
    }

    private void PopulateAbilityList()
    {
        if (_abilityListContainer == null) return;

        foreach (var child in _abilityListContainer.GetChildren())
        {
            child.QueueFree();
        }
        _entries.Clear();

        CallDeferred(nameof(PopulateAbilityListDeferred));
    }

    private void PopulateAbilityListDeferred()
    {
        var am = AbilityManager3D.Instance;
        if (am == null)
        {
            GD.Print("[AbilitiesUI3D] AbilityManager not ready, retrying...");
            GetTree().CreateTimer(0.5f).Timeout += PopulateAbilityListDeferred;
            return;
        }

        // Only show abilities (UsesMana == false), not spells
        foreach (var ability in am.GetAllAbilities())
        {
            if (!ability.UsesMana)  // Filter to abilities only
            {
                var entry = CreateAbilityEntry(ability);
                _entries.Add(entry);
                _abilityListContainer?.AddChild(entry.Container);
            }
        }

        GD.Print($"[AbilitiesUI3D] Populated with {_entries.Count} abilities");
    }

    private AbilityEntry CreateAbilityEntry(Ability3D ability)
    {
        var entry = new AbilityEntry();
        entry.AbilityId = ability.AbilityId;
        entry.Ability = ability;

        bool isLocked = !ability.IsUnlocked;

        // Main container
        entry.Container = new PanelContainer();
        entry.Container.CustomMinimumSize = new Vector2(0, 65);

        var containerStyle = new StyleBoxFlat();
        if (isLocked)
        {
            containerStyle.BgColor = new Color(0.08f, 0.1f, 0.08f, 0.9f);
            containerStyle.BorderColor = new Color(0.2f, 0.25f, 0.2f);
        }
        else
        {
            containerStyle.BgColor = new Color(0.15f, 0.2f, 0.18f, 0.9f);
            containerStyle.BorderColor = new Color(0.35f, 0.5f, 0.4f);
        }
        containerStyle.SetBorderWidthAll(1);
        containerStyle.SetCornerRadiusAll(6);
        containerStyle.SetContentMarginAll(8);
        entry.Container.AddThemeStyleboxOverride("panel", containerStyle);

        // Make draggable
        entry.DragArea = new Control();
        entry.DragArea.SetAnchorsPreset(LayoutPreset.FullRect);
        entry.DragArea.MouseFilter = MouseFilterEnum.Stop;
        entry.DragArea.GuiInput += (InputEvent e) => OnAbilityEntryInput(e, entry);
        entry.Container.AddChild(entry.DragArea);

        // Content HBox
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);
        hbox.MouseFilter = MouseFilterEnum.Ignore;
        entry.Container.AddChild(hbox);

        // Icon
        var iconContainer = new PanelContainer();
        iconContainer.CustomMinimumSize = new Vector2(50, 50);
        iconContainer.MouseFilter = MouseFilterEnum.Ignore;

        var iconStyle = new StyleBoxFlat();
        iconStyle.BgColor = new Color(0.08f, 0.12f, 0.1f);
        iconStyle.BorderColor = isLocked ? new Color(0.25f, 0.3f, 0.25f) : new Color(0.4f, 0.7f, 0.5f);
        iconStyle.SetBorderWidthAll(2);
        iconStyle.SetCornerRadiusAll(4);
        iconContainer.AddThemeStyleboxOverride("panel", iconStyle);

        var icon = new TextureRect();
        icon.Texture = AbilityIcons.GetIcon(ability.AbilityId);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.CustomMinimumSize = new Vector2(44, 44);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        if (isLocked)
        {
            icon.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        }
        iconContainer.AddChild(icon);
        hbox.AddChild(iconContainer);

        // Info VBox
        var infoVBox = new VBoxContainer();
        infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        infoVBox.AddThemeConstantOverride("separation", 2);
        infoVBox.MouseFilter = MouseFilterEnum.Ignore;

        // Name row
        var nameRow = new HBoxContainer();
        nameRow.MouseFilter = MouseFilterEnum.Ignore;

        var nameLabel = new Label();
        nameLabel.Text = ability.AbilityName;
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        nameLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.5f, 0.55f, 0.5f) : new Color(0.95f, 1f, 0.95f));
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameLabel.MouseFilter = MouseFilterEnum.Ignore;
        nameRow.AddChild(nameLabel);

        if (isLocked)
        {
            var lockLabel = new Label();
            lockLabel.Text = $"[Lvl {ability.RequiredLevel}]";
            lockLabel.AddThemeFontSizeOverride("font_size", 11);
            lockLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.4f, 0.4f));
            lockLabel.MouseFilter = MouseFilterEnum.Ignore;
            nameRow.AddChild(lockLabel);
        }

        infoVBox.AddChild(nameRow);

        // Description
        var descLabel = new Label();
        descLabel.Text = ability.Description;
        descLabel.AddThemeFontSizeOverride("font_size", 11);
        descLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.35f, 0.4f, 0.35f) : new Color(0.7f, 0.8f, 0.75f));
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.CustomMinimumSize = new Vector2(260, 0);
        descLabel.MouseFilter = MouseFilterEnum.Ignore;
        infoVBox.AddChild(descLabel);

        // Cooldown
        var cdLabel = new Label();
        cdLabel.Text = ability.Cooldown > 0 ? $"Cooldown: {ability.Cooldown}s" : "No Cooldown";
        cdLabel.AddThemeFontSizeOverride("font_size", 11);
        cdLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.4f, 0.45f, 0.4f) : new Color(0.8f, 0.9f, 0.6f));
        cdLabel.MouseFilter = MouseFilterEnum.Ignore;
        infoVBox.AddChild(cdLabel);

        hbox.AddChild(infoVBox);

        return entry;
    }

    private void OnHeaderGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _isWindowDragging = true;
                _windowDragOffset = GetGlobalMousePosition() - _mainPanel!.GlobalPosition;
            }
            else
            {
                if (_isWindowDragging)
                {
                    _isWindowDragging = false;
                    WindowPositionManager.SetPosition(WindowName, _mainPanel!.Position);
                }
            }
        }
        else if (@event is InputEventMouseMotion && _isWindowDragging && _mainPanel != null)
        {
            var newPos = GetGlobalMousePosition() - _windowDragOffset;
            var viewportSize = GetViewportRect().Size;
            var panelSize = _mainPanel.Size;
            _mainPanel.Position = WindowPositionManager.ClampToViewport(newPos, viewportSize, panelSize);
        }
    }

    private void OnAbilityEntryInput(InputEvent e, AbilityEntry entry)
    {
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            // Only allow dragging unlocked abilities
            if (entry.Ability?.IsUnlocked != true)
            {
                GD.Print($"[AbilitiesUI3D] Cannot drag locked ability: {entry.AbilityId}");
                return;
            }

            StartDrag(entry);
        }
    }

    private void StartDrag(AbilityEntry entry)
    {
        _draggedAbilityId = entry.AbilityId;

        _dragPreview = CreateDragPreview(entry.AbilityId);
        AddChild(_dragPreview);

        GD.Print($"[AbilitiesUI3D] Started dragging {entry.AbilityId}");
    }

    private Control CreateDragPreview(string abilityId)
    {
        var preview = new PanelContainer();
        preview.CustomMinimumSize = new Vector2(52, 52);
        preview.ZIndex = 100;
        preview.MouseFilter = MouseFilterEnum.Ignore;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.15f, 0.25f, 0.2f, 0.95f);
        style.BorderColor = new Color(0.5f, 0.8f, 0.6f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(6);
        preview.AddThemeStyleboxOverride("panel", style);

        var icon = new TextureRect();
        icon.Texture = AbilityIcons.GetIcon(abilityId);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.CustomMinimumSize = new Vector2(44, 44);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        preview.AddChild(icon);

        return preview;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        // Stop window drag
        if (_isWindowDragging && @event is InputEventMouseButton windowMb && windowMb.ButtonIndex == MouseButton.Left && !windowMb.Pressed)
        {
            _isWindowDragging = false;
            if (_mainPanel != null)
            {
                WindowPositionManager.SetPosition(WindowName, _mainPanel.Position);
            }
        }

        // Handle ability drag
        if (_draggedAbilityId != null && _dragPreview != null)
        {
            if (@event is InputEventMouseMotion motion)
            {
                _dragPreview.GlobalPosition = motion.GlobalPosition - new Vector2(26, 26);
            }
            else if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
            {
                EndDrag(mb.GlobalPosition);
            }
        }

        // ESC to close
        if (@event.IsActionPressed("escape"))
        {
            Hide();
            GetViewport().SetInputAsHandled();
        }
    }

    private void EndDrag(Vector2 dropPosition)
    {
        if (_draggedAbilityId == null) return;

        GD.Print($"[AbilitiesUI3D] Ended drag at {dropPosition}");

        var hud = HUD3D.Instance;
        if (hud != null)
        {
            var (row, slot) = hud.FindActionBarSlotAt(dropPosition);
            if (row >= 0 && slot >= 0)
            {
                GD.Print($"[AbilitiesUI3D] Dropped on slot [{row},{slot}]");
                hud.UpdateActionSlotConsumable(row, slot, null);
                AbilityManager3D.Instance?.AssignToHotbar(row, slot, _draggedAbilityId);
            }
        }

        _dragPreview?.QueueFree();
        _dragPreview = null;
        _draggedAbilityId = null;
    }

    public new void Show()
    {
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (_mainPanel != null)
        {
            var viewportSize = GetViewportRect().Size;
            var storedPos = WindowPositionManager.GetPosition(WindowName);
            if (storedPos == WindowPositionManager.CenterMarker)
            {
                _mainPanel.Position = WindowPositionManager.GetCenteredPosition(viewportSize, new Vector2(PanelWidth, PanelHeight));
            }
            else
            {
                _mainPanel.Position = WindowPositionManager.ClampToViewport(storedPos, viewportSize, new Vector2(PanelWidth, PanelHeight));
            }
        }

        GD.Print("[AbilitiesUI3D] Opened");
    }

    public new void Hide()
    {
        Visible = false;

        bool otherUIOpen = (SpellBook3D.Instance?.Visible == true) ||
                           (InventoryUI3D.Instance?.Visible == true) ||
                           (CharacterSheetUI.Instance?.Visible == true) ||
                           (LootUI3D.Instance?.Visible == true);
        if (!otherUIOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        GD.Print("[AbilitiesUI3D] Closed");
    }

    public void Toggle()
    {
        if (Visible)
            Hide();
        else
            Show();
    }

    /// <summary>
    /// Refresh the ability list to reflect unlock status changes.
    /// </summary>
    public void RefreshAbilityList()
    {
        if (_abilityListContainer == null) return;

        foreach (var child in _abilityListContainer.GetChildren())
        {
            child.QueueFree();
        }
        _entries.Clear();

        CallDeferred(nameof(PopulateAbilityListDeferred));
        GD.Print("[AbilitiesUI3D] Refreshed ability list");
    }

    public static bool IsDragging => _draggedAbilityId != null;
    public static string? DraggedAbilityId => _draggedAbilityId;

    public override void _ExitTree()
    {
        Instance = null;
    }
}

/// <summary>
/// Data for an ability entry.
/// </summary>
public class AbilityEntry
{
    public string AbilityId { get; set; } = "";
    public Ability3D? Ability { get; set; }
    public PanelContainer? Container { get; set; }
    public Control? DragArea { get; set; }
}
