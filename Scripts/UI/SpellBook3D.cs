using Godot;
using System.Collections.Generic;
using SafeRoom3D.Abilities;

namespace SafeRoom3D.UI;

/// <summary>
/// Spell book UI that displays all abilities with descriptions, costs, cooldowns.
/// Supports drag-and-drop to action bars.
/// </summary>
public partial class SpellBook3D : Control
{
    // Singleton
    public static SpellBook3D? Instance { get; private set; }

    // UI Elements
    private PanelContainer? _mainPanel;
    private VBoxContainer? _contentContainer;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _spellListContainer;
    private Label? _titleLabel;
    private Button? _closeButton;

    // Spell entries for all abilities
    private readonly List<SpellBookEntry> _entries = new();

    // Drag state
    private static string? _draggedAbilityId;
    private static Control? _dragPreview;

    // Panel size
    private const float PanelWidth = 450;
    private const float PanelHeight = 600;

    // Window dragging
    private const string WindowName = "SpellBook";
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
        PopulateSpellList();

        GD.Print("[SpellBook3D] Ready");
    }

    private void CreateUI()
    {
        // Semi-transparent background to darken game
        var background = new ColorRect();
        background.Name = "Background";
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        background.Color = new Color(0, 0, 0, 0.5f);
        background.MouseFilter = MouseFilterEnum.Stop;
        AddChild(background);

        // Main panel centered
        _mainPanel = new PanelContainer();
        _mainPanel.Name = "SpellBookPanel";
        _mainPanel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.12f, 0.1f, 0.18f, 0.98f);
        panelStyle.BorderColor = new Color(0.5f, 0.4f, 0.6f);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.SetCornerRadiusAll(12);
        panelStyle.SetContentMarginAll(15);
        _mainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_mainPanel);

        // Content container
        _contentContainer = new VBoxContainer();
        _contentContainer.AddThemeConstantOverride("separation", 10);
        _mainPanel.AddChild(_contentContainer);

        // Header with title and close button (draggable area)
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        header.CustomMinimumSize = new Vector2(0, 40);
        header.MouseFilter = MouseFilterEnum.Stop;
        header.GuiInput += OnHeaderGuiInput;

        _titleLabel = new Label();
        _titleLabel.Text = "Spell Book (drag to move)";
        _titleLabel.AddThemeFontSizeOverride("font_size", 24);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 1f));
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
        instructions.Text = "Drag unlocked spells to your action bar (locked ones shown greyed)";
        instructions.AddThemeFontSizeOverride("font_size", 12);
        instructions.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.8f));
        instructions.HorizontalAlignment = HorizontalAlignment.Center;
        _contentContainer.AddChild(instructions);

        // Scroll container for spell list
        _scrollContainer = new ScrollContainer();
        _scrollContainer.CustomMinimumSize = new Vector2(0, PanelHeight - 120);
        _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        _contentContainer.AddChild(_scrollContainer);

        _spellListContainer = new VBoxContainer();
        _spellListContainer.AddThemeConstantOverride("separation", 8);
        _spellListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scrollContainer.AddChild(_spellListContainer);
    }

    private void PopulateSpellList()
    {
        if (_spellListContainer == null) return;

        // Clear existing entries
        foreach (var child in _spellListContainer.GetChildren())
        {
            child.QueueFree();
        }
        _entries.Clear();

        // Wait for AbilityManager to be ready
        CallDeferred(nameof(PopulateSpellListDeferred));
    }

    private void PopulateSpellListDeferred()
    {
        var am = AbilityManager3D.Instance;
        if (am == null)
        {
            GD.Print("[SpellBook3D] AbilityManager not ready, retrying...");
            GetTree().CreateTimer(0.5f).Timeout += PopulateSpellListDeferred;
            return;
        }

        foreach (var ability in am.GetAllAbilities())
        {
            var entry = CreateSpellEntry(ability);
            _entries.Add(entry);
            _spellListContainer?.AddChild(entry.Container);
        }

        GD.Print($"[SpellBook3D] Populated with {_entries.Count} spells");
    }

    private SpellBookEntry CreateSpellEntry(Ability3D ability)
    {
        var entry = new SpellBookEntry();
        entry.AbilityId = ability.AbilityId;
        entry.Ability = ability;

        bool isLocked = !ability.IsUnlocked;

        // Main container - draggable only if unlocked
        entry.Container = new PanelContainer();
        entry.Container.CustomMinimumSize = new Vector2(0, 70);

        var containerStyle = new StyleBoxFlat();
        // Greyed out if locked
        if (isLocked)
        {
            containerStyle.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            containerStyle.BorderColor = new Color(0.25f, 0.25f, 0.3f);
        }
        else
        {
            containerStyle.BgColor = new Color(0.18f, 0.15f, 0.25f, 0.9f);
            containerStyle.BorderColor = new Color(0.4f, 0.35f, 0.5f);
        }
        containerStyle.SetBorderWidthAll(1);
        containerStyle.SetCornerRadiusAll(6);
        containerStyle.SetContentMarginAll(8);
        entry.Container.AddThemeStyleboxOverride("panel", containerStyle);

        // Make draggable
        entry.DragArea = new Control();
        entry.DragArea.SetAnchorsPreset(LayoutPreset.FullRect);
        entry.DragArea.MouseFilter = MouseFilterEnum.Stop;
        entry.DragArea.GuiInput += (InputEvent e) => OnSpellEntryInput(e, entry);
        entry.Container.AddChild(entry.DragArea);

        // Content HBox
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);
        hbox.MouseFilter = MouseFilterEnum.Ignore;
        entry.Container.AddChild(hbox);

        // Icon
        var iconContainer = new PanelContainer();
        iconContainer.CustomMinimumSize = new Vector2(54, 54);
        iconContainer.MouseFilter = MouseFilterEnum.Ignore;

        var iconStyle = new StyleBoxFlat();
        iconStyle.BgColor = new Color(0.1f, 0.08f, 0.15f);
        iconStyle.BorderColor = isLocked ? new Color(0.3f, 0.3f, 0.3f) : GetAbilityTypeColor(ability.Type);
        iconStyle.SetBorderWidthAll(2);
        iconStyle.SetCornerRadiusAll(4);
        iconContainer.AddThemeStyleboxOverride("panel", iconStyle);

        var icon = new TextureRect();
        icon.Texture = AbilityIcons.GetIcon(ability.AbilityId);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.CustomMinimumSize = new Vector2(48, 48);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        // Desaturate locked icons
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

        // Name row with type and lock status
        var nameRow = new HBoxContainer();
        nameRow.MouseFilter = MouseFilterEnum.Ignore;

        var nameLabel = new Label();
        nameLabel.Text = ability.AbilityName;
        nameLabel.AddThemeFontSizeOverride("font_size", 16);
        nameLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.5f, 0.5f, 0.5f) : new Color(1f, 0.95f, 0.85f));
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameLabel.MouseFilter = MouseFilterEnum.Ignore;
        nameRow.AddChild(nameLabel);

        // Lock indicator for locked abilities
        if (isLocked)
        {
            var lockLabel = new Label();
            lockLabel.Text = $"[Lvl {ability.RequiredLevel}]";
            lockLabel.AddThemeFontSizeOverride("font_size", 11);
            lockLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.4f, 0.4f));
            lockLabel.MouseFilter = MouseFilterEnum.Ignore;
            nameRow.AddChild(lockLabel);
        }

        var typeLabel = new Label();
        typeLabel.Text = $"[{ability.Type}]";
        typeLabel.AddThemeFontSizeOverride("font_size", 11);
        typeLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.4f, 0.4f, 0.4f) : GetAbilityTypeColor(ability.Type));
        typeLabel.MouseFilter = MouseFilterEnum.Ignore;
        nameRow.AddChild(typeLabel);

        infoVBox.AddChild(nameRow);

        // Description
        var descLabel = new Label();
        descLabel.Text = ability.Description;
        descLabel.AddThemeFontSizeOverride("font_size", 11);
        descLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.4f, 0.4f, 0.45f) : new Color(0.75f, 0.7f, 0.85f));
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.CustomMinimumSize = new Vector2(280, 0);
        descLabel.MouseFilter = MouseFilterEnum.Ignore;
        infoVBox.AddChild(descLabel);

        // Stats row
        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 15);
        statsRow.MouseFilter = MouseFilterEnum.Ignore;

        // Mana cost
        var manaLabel = new Label();
        manaLabel.Text = ability.ManaCost > 0 ? $"Mana: {ability.ManaCost}" : "Mana: Free";
        manaLabel.AddThemeFontSizeOverride("font_size", 11);
        manaLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.3f, 0.35f, 0.5f) : new Color(0.4f, 0.6f, 1f));
        manaLabel.MouseFilter = MouseFilterEnum.Ignore;
        statsRow.AddChild(manaLabel);

        // Cooldown
        var cdLabel = new Label();
        cdLabel.Text = ability.Cooldown > 0 ? $"CD: {ability.Cooldown}s" : "CD: None";
        cdLabel.AddThemeFontSizeOverride("font_size", 11);
        cdLabel.AddThemeColorOverride("font_color", isLocked ? new Color(0.5f, 0.4f, 0.3f) : new Color(1f, 0.8f, 0.4f));
        cdLabel.MouseFilter = MouseFilterEnum.Ignore;
        statsRow.AddChild(cdLabel);

        infoVBox.AddChild(statsRow);

        hbox.AddChild(infoVBox);

        return entry;
    }

    private Color GetAbilityTypeColor(AbilityType type)
    {
        return type switch
        {
            AbilityType.Instant => new Color(0.4f, 0.8f, 0.4f),
            AbilityType.Targeted => new Color(1f, 0.6f, 0.3f),
            AbilityType.Duration => new Color(0.5f, 0.7f, 1f),
            AbilityType.Toggle => new Color(0.9f, 0.5f, 0.9f),
            AbilityType.Passive => new Color(0.7f, 0.7f, 0.7f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };
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

    private void OnSpellEntryInput(InputEvent e, SpellBookEntry entry)
    {
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            // Only allow dragging unlocked abilities
            if (entry.Ability?.IsUnlocked != true)
            {
                GD.Print($"[SpellBook3D] Cannot drag locked ability: {entry.AbilityId} (requires level {entry.Ability?.RequiredLevel ?? 0})");
                return;
            }

            // Start drag
            StartDrag(entry);
        }
    }

    private void StartDrag(SpellBookEntry entry)
    {
        _draggedAbilityId = entry.AbilityId;

        // Create drag preview
        _dragPreview = CreateDragPreview(entry.AbilityId);
        AddChild(_dragPreview);

        GD.Print($"[SpellBook3D] Started dragging {entry.AbilityId}");
    }

    private Control CreateDragPreview(string abilityId)
    {
        var preview = new PanelContainer();
        preview.CustomMinimumSize = new Vector2(56, 56);
        preview.ZIndex = 100;
        preview.MouseFilter = MouseFilterEnum.Ignore;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.2f, 0.15f, 0.3f, 0.95f);
        style.BorderColor = new Color(0.8f, 0.7f, 0.9f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(6);
        preview.AddThemeStyleboxOverride("panel", style);

        var icon = new TextureRect();
        icon.Texture = AbilityIcons.GetIcon(abilityId);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.CustomMinimumSize = new Vector2(48, 48);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        preview.AddChild(icon);

        return preview;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        // Stop window drag if mouse released anywhere
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
                // Update preview position
                _dragPreview.GlobalPosition = motion.GlobalPosition - new Vector2(28, 28);
            }
            else if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
            {
                // End drag - check if dropped on action bar
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

        GD.Print($"[SpellBook3D] Ended drag at {dropPosition}");

        // Check if dropped on action bar slot using HUD3D's slot detection
        var hud = HUD3D.Instance;
        if (hud != null)
        {
            var (row, slot) = hud.FindActionBarSlotAt(dropPosition);
            if (row >= 0 && slot >= 0)
            {
                GD.Print($"[SpellBook3D] Dropped on slot [{row},{slot}]");
                // Clear any consumable in that slot and assign ability
                hud.UpdateActionSlotConsumable(row, slot, null);  // Clear consumable
                AbilityManager3D.Instance?.AssignToHotbar(row, slot, _draggedAbilityId);
            }
        }

        // Clean up
        _dragPreview?.QueueFree();
        _dragPreview = null;
        _draggedAbilityId = null;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        // Position is set in Show() and during drag, no per-frame update needed
    }

    public new void Show()
    {
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Restore or center position
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

        GD.Print("[SpellBook3D] Opened");
    }

    public new void Hide()
    {
        Visible = false;

        // Only capture mouse if no other UI windows are open
        bool otherUIOpen = (InventoryUI3D.Instance?.Visible == true) ||
                           (CharacterSheetUI.Instance?.Visible == true) ||
                           (LootUI3D.Instance?.Visible == true);
        if (!otherUIOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        GD.Print("[SpellBook3D] Closed");
    }

    public void Toggle()
    {
        if (Visible)
            Hide();
        else
            Show();
    }

    /// <summary>
    /// Refresh the spell list to reflect unlock status changes.
    /// Called when abilities are unlocked (e.g., on level up).
    /// </summary>
    public void RefreshSpellList()
    {
        if (_spellListContainer == null) return;

        // Clear and repopulate
        foreach (var child in _spellListContainer.GetChildren())
        {
            child.QueueFree();
        }
        _entries.Clear();

        CallDeferred(nameof(PopulateSpellListDeferred));
        GD.Print("[SpellBook3D] Refreshed spell list (unlock status may have changed)");
    }

    /// <summary>
    /// Check if currently dragging an ability.
    /// </summary>
    public static bool IsDragging => _draggedAbilityId != null;

    /// <summary>
    /// Get the currently dragged ability ID.
    /// </summary>
    public static string? DraggedAbilityId => _draggedAbilityId;

    public override void _ExitTree()
    {
        Instance = null;
    }
}

/// <summary>
/// Data for a spell book entry.
/// </summary>
public class SpellBookEntry
{
    public string AbilityId { get; set; } = "";
    public Ability3D? Ability { get; set; }
    public PanelContainer? Container { get; set; }
    public Control? DragArea { get; set; }
}
