using Godot;
using SafeRoom3D.Core;

namespace SafeRoom3D.UI;

/// <summary>
/// Floor selector UI that allows the player to choose between dungeon types.
/// Activated by pressing Enter key.
/// </summary>
public partial class FloorSelector3D : Control
{
    public static FloorSelector3D? Instance { get; private set; }

    private Panel? _panel;
    private VBoxContainer? _container;
    private Label? _titleLabel;
    private Button? _largeDungeonButton;
    private Button? _testDungeonButton;
    private Button? _cancelButton;
    private bool _isVisible = false;

    // Colors
    private static readonly Color PanelColor = new(0.1f, 0.1f, 0.15f, 0.95f);
    private static readonly Color ButtonColor = new(0.2f, 0.2f, 0.3f, 1f);
    private static readonly Color ButtonHoverColor = new(0.3f, 0.3f, 0.5f, 1f);
    private static readonly Color LargeFloorColor = new(0.6f, 0.3f, 0.1f, 1f);  // Orange/bronze
    private static readonly Color TestFloorColor = new(0.2f, 0.5f, 0.3f, 1f);   // Green

    public override void _Ready()
    {
        Instance = this;
        Name = "FloorSelector";

        // Full screen overlay
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        CreateUI();
        Hide();
        _isVisible = false;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    private void CreateUI()
    {
        // Semi-transparent background
        var background = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.7f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        // Center panel
        _panel = new Panel();
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.CustomMinimumSize = new Vector2(400, 350);
        _panel.Position = new Vector2(-200, -175);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = PanelColor,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color(0.5f, 0.4f, 0.3f, 1f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_panel);

        // Container for content
        _container = new VBoxContainer
        {
            Position = new Vector2(20, 20),
            Size = new Vector2(360, 310)
        };
        _container.AddThemeConstantOverride("separation", 15);
        _panel.AddChild(_container);

        // Title
        _titleLabel = new Label
        {
            Text = "SELECT FLOOR",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 28);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.7f));
        _container.AddChild(_titleLabel);

        // Spacer
        _container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        // Large Dungeon button
        _largeDungeonButton = CreateFloorButton(
            "THE DEPTHS",
            "160x160 tiles • 20 rooms • All monsters",
            LargeFloorColor
        );
        _largeDungeonButton.Pressed += OnLargeDungeonPressed;
        _container.AddChild(_largeDungeonButton);

        // Test Dungeon button
        _testDungeonButton = CreateFloorButton(
            "TRAINING GROUNDS",
            "80x80 tiles • 5-8 rooms • Basic enemies",
            TestFloorColor
        );
        _testDungeonButton.Pressed += OnTestDungeonPressed;
        _container.AddChild(_testDungeonButton);

        // Spacer
        _container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 5) });

        // Cancel button
        _cancelButton = new Button
        {
            Text = "Cancel (ESC)",
            CustomMinimumSize = new Vector2(360, 40)
        };
        StyleButton(_cancelButton, ButtonColor);
        _cancelButton.Pressed += OnCancelPressed;
        _container.AddChild(_cancelButton);
    }

    private Button CreateFloorButton(string title, string description, Color accentColor)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(360, 70),
            ClipText = false
        };

        // Create rich text for button content
        var vbox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 2);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        titleLabel.AddThemeColorOverride("font_color", accentColor);

        var descLabel = new Label
        {
            Text = description,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        descLabel.AddThemeFontSizeOverride("font_size", 14);
        descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        vbox.AddChild(titleLabel);
        vbox.AddChild(descLabel);

        button.AddChild(vbox);
        StyleButton(button, ButtonColor);

        return button;
    }

    private void StyleButton(Button button, Color bgColor)
    {
        var normalStyle = new StyleBoxFlat
        {
            BgColor = bgColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };

        var hoverStyle = new StyleBoxFlat
        {
            BgColor = ButtonHoverColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color(0.6f, 0.5f, 0.4f)
        };

        var pressedStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.25f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isVisible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                OnCancelPressed();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void ShowSelector()
    {
        if (_isVisible) return;

        _isVisible = true;
        Show();

        // Pause game and show mouse
        Engine.TimeScale = 0;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Focus first button
        _largeDungeonButton?.GrabFocus();

        GD.Print("[FloorSelector] Opened floor selector");
    }

    public void HideSelector()
    {
        if (!_isVisible) return;

        _isVisible = false;
        Hide();

        // Resume game at configured speed and capture mouse
        GameConfig.ResumeToNormalSpeed();
        Input.MouseMode = Input.MouseModeEnum.Captured;

        GD.Print("[FloorSelector] Closed floor selector");
    }

    public bool IsOpen => _isVisible;

    private void OnLargeDungeonPressed()
    {
        GD.Print("[FloorSelector] Selected: Large Dungeon (The Depths)");
        HideSelector();
        RegenerateDungeon(useLarge: true);
    }

    private void OnTestDungeonPressed()
    {
        GD.Print("[FloorSelector] Selected: Test Dungeon (Training Grounds)");
        HideSelector();
        RegenerateDungeon(useLarge: false);
    }

    private void OnCancelPressed()
    {
        HideSelector();
    }

    private void RegenerateDungeon(bool useLarge)
    {
        // Find the dungeon generator
        var dungeon = GetTree().Root.FindChild("DungeonGenerator", true, false) as DungeonGenerator3D;
        if (dungeon == null)
        {
            // Try alternate search
            var gameManager = GameManager3D.Instance;
            if (gameManager != null)
            {
                dungeon = gameManager.FindChild("DungeonGenerator", true, false) as DungeonGenerator3D;
            }
        }

        if (dungeon == null)
        {
            GD.PrintErr("[FloorSelector] Could not find DungeonGenerator3D!");
            return;
        }

        // Set the dungeon type and regenerate
        dungeon.UseLargeDungeon = useLarge;

        // Reset player position will happen after generation
        var player = Player.FPSController.Instance;

        // Generate new dungeon with new seed
        dungeon.Generate(0); // 0 = random seed

        // Teleport player to spawn
        var spawnPos = dungeon.GetSpawnPosition();
        if (player != null && spawnPos != Vector3.Zero)
        {
            player.GlobalPosition = spawnPos + Vector3.Up * 2;
            player.Velocity = Vector3.Zero;
            GD.Print($"[FloorSelector] Teleported player to {player.GlobalPosition}");
        }

        GD.Print($"[FloorSelector] Regenerated dungeon - Large: {useLarge}");
    }
}
