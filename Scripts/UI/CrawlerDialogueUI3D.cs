using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.NPC;

namespace SafeRoom3D.UI;

/// <summary>
/// UI panel for interacting with Crawler NPCs.
/// Shows branching dialogue with response choices.
/// </summary>
public partial class CrawlerDialogueUI3D : Control
{
    public static CrawlerDialogueUI3D? Instance { get; private set; }

    private BaseNPC3D? _currentNpc;
    private string? _crawlerType;
    private CrawlerDialogueDatabase.DialogueNode? _currentNode;

    private Panel? _panel;
    private Label? _npcNameLabel;
    private RichTextLabel? _dialogueText;
    private VBoxContainer? _responseContainer;
    private TextureRect? _npcPortrait;

    // Window dragging
    private const string WindowName = "CrawlerDialogue";
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Control? _dragHeader;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        CreateUI();
        GD.Print("[CrawlerDialogueUI3D] Ready");
    }

    private void CreateUI()
    {
        // Main panel (650x450)
        _panel = new Panel();
        _panel.CustomMinimumSize = new Vector2(650, 450);
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.Position = new Vector2(-325, -225);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.06f, 0.06f, 0.08f, 0.97f);
        panelStyle.BorderColor = new Color(0.4f, 0.5f, 0.6f);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.SetCornerRadiusAll(8);
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_panel);

        // Draggable header
        _dragHeader = new Control();
        _dragHeader.Position = Vector2.Zero;
        _dragHeader.Size = new Vector2(650, 50);
        _dragHeader.MouseFilter = MouseFilterEnum.Stop;
        _dragHeader.GuiInput += OnHeaderGuiInput;
        _panel.AddChild(_dragHeader);

        // NPC Name label (speaker)
        _npcNameLabel = new Label();
        _npcNameLabel.Text = "Crawler";
        _npcNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _npcNameLabel.Position = new Vector2(0, 12);
        _npcNameLabel.Size = new Vector2(650, 35);
        _npcNameLabel.AddThemeFontSizeOverride("font_size", 26);
        _npcNameLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.8f, 0.6f));
        _npcNameLabel.MouseFilter = MouseFilterEnum.Ignore;
        _panel.AddChild(_npcNameLabel);

        // Close button (X)
        var closeBtn = new Button();
        closeBtn.Text = "X";
        closeBtn.Position = new Vector2(605, 10);
        closeBtn.Size = new Vector2(35, 35);
        closeBtn.Pressed += Close;
        StyleCloseButton(closeBtn);
        _panel.AddChild(closeBtn);

        // Left side: NPC portrait area (placeholder colored box)
        var portraitPanel = new Panel();
        portraitPanel.Position = new Vector2(20, 60);
        portraitPanel.Size = new Vector2(140, 180);

        var portraitStyle = new StyleBoxFlat();
        portraitStyle.BgColor = new Color(0.12f, 0.12f, 0.15f);
        portraitStyle.BorderColor = new Color(0.3f, 0.35f, 0.4f);
        portraitStyle.SetBorderWidthAll(2);
        portraitStyle.SetCornerRadiusAll(4);
        portraitPanel.AddThemeStyleboxOverride("panel", portraitStyle);
        _panel.AddChild(portraitPanel);

        // Portrait placeholder (colored rectangle representing the crawler)
        _npcPortrait = new TextureRect();
        _npcPortrait.Position = new Vector2(10, 10);
        _npcPortrait.Size = new Vector2(120, 160);
        _npcPortrait.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
        portraitPanel.AddChild(_npcPortrait);

        // Portrait silhouette (we'll draw a simple shape)
        var silhouetteLabel = new Label();
        silhouetteLabel.Text = "[?]";
        silhouetteLabel.HorizontalAlignment = HorizontalAlignment.Center;
        silhouetteLabel.VerticalAlignment = VerticalAlignment.Center;
        silhouetteLabel.Position = new Vector2(0, 0);
        silhouetteLabel.Size = new Vector2(120, 160);
        silhouetteLabel.AddThemeFontSizeOverride("font_size", 48);
        silhouetteLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.35f, 0.4f));
        _npcPortrait.AddChild(silhouetteLabel);

        // Right side: Dialogue text area
        var dialoguePanel = new Panel();
        dialoguePanel.Position = new Vector2(175, 60);
        dialoguePanel.Size = new Vector2(455, 180);

        var dialogueStyle = new StyleBoxFlat();
        dialogueStyle.BgColor = new Color(0.08f, 0.08f, 0.1f);
        dialogueStyle.BorderColor = new Color(0.3f, 0.3f, 0.35f);
        dialogueStyle.SetBorderWidthAll(1);
        dialogueStyle.SetCornerRadiusAll(4);
        dialoguePanel.AddThemeStyleboxOverride("panel", dialogueStyle);
        _panel.AddChild(dialoguePanel);

        // Dialogue text
        _dialogueText = new RichTextLabel();
        _dialogueText.Position = new Vector2(15, 15);
        _dialogueText.Size = new Vector2(425, 150);
        _dialogueText.BbcodeEnabled = true;
        _dialogueText.AddThemeFontSizeOverride("normal_font_size", 16);
        _dialogueText.AddThemeColorOverride("default_color", new Color(0.9f, 0.9f, 0.85f));
        dialoguePanel.AddChild(_dialogueText);

        // Response buttons area
        var responsePanel = new Panel();
        responsePanel.Position = new Vector2(20, 255);
        responsePanel.Size = new Vector2(610, 175);

        var responseStyle = new StyleBoxFlat();
        responseStyle.BgColor = new Color(0.05f, 0.05f, 0.07f);
        responseStyle.BorderColor = new Color(0.25f, 0.28f, 0.3f);
        responseStyle.SetBorderWidthAll(1);
        responseStyle.SetCornerRadiusAll(4);
        responsePanel.AddThemeStyleboxOverride("panel", responseStyle);
        _panel.AddChild(responsePanel);

        // Response container
        _responseContainer = new VBoxContainer();
        _responseContainer.Position = new Vector2(15, 15);
        _responseContainer.Size = new Vector2(580, 145);
        _responseContainer.AddThemeConstantOverride("separation", 8);
        responsePanel.AddChild(_responseContainer);
    }

    private void StyleCloseButton(Button btn)
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.5f, 0.2f, 0.2f);
        style.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = style.Duplicate() as StyleBoxFlat;
        hoverStyle!.BgColor = new Color(0.7f, 0.3f, 0.3f);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeColorOverride("font_color", Colors.White);
    }

    private Button CreateResponseButton(string text, int index)
    {
        var btn = new Button();
        btn.Text = $"{index + 1}. {text}";
        btn.CustomMinimumSize = new Vector2(560, 32);
        btn.ClipText = true;
        btn.Alignment = HorizontalAlignment.Left;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.15f, 0.18f, 0.22f);
        style.BorderColor = new Color(0.35f, 0.4f, 0.45f);
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.ContentMarginLeft = 10;
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = style.Duplicate() as StyleBoxFlat;
        hoverStyle!.BgColor = new Color(0.25f, 0.3f, 0.35f);
        hoverStyle.BorderColor = new Color(0.5f, 0.55f, 0.6f);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.95f));

        return btn;
    }

    private Button CreateEndButton()
    {
        var btn = new Button();
        btn.Text = "[Continue...]";
        btn.CustomMinimumSize = new Vector2(560, 35);
        btn.Alignment = HorizontalAlignment.Center;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.2f, 0.22f, 0.18f);
        style.BorderColor = new Color(0.5f, 0.55f, 0.4f);
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = style.Duplicate() as StyleBoxFlat;
        hoverStyle!.BgColor = new Color(0.3f, 0.32f, 0.28f);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        btn.AddThemeFontSizeOverride("font_size", 15);
        btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 0.85f));

        btn.Pressed += Close;
        return btn;
    }

    private void ShowDialogueNode(CrawlerDialogueDatabase.DialogueNode node)
    {
        _currentNode = node;

        // Update speaker name
        _npcNameLabel!.Text = node.Speaker;

        // Update dialogue text
        _dialogueText!.Text = node.Text;

        // Clear and populate responses
        foreach (var child in _responseContainer!.GetChildren())
        {
            child.QueueFree();
        }

        if (node.IsEndNode)
        {
            // Show continue button to close dialogue
            var endBtn = CreateEndButton();
            _responseContainer.AddChild(endBtn);
        }
        else
        {
            // Show response options
            for (int i = 0; i < node.Responses.Count; i++)
            {
                var response = node.Responses[i];
                var btn = CreateResponseButton(response.Text, i);
                string nextNodeId = response.NextNodeId;
                btn.Pressed += () => OnResponseSelected(nextNodeId);
                _responseContainer.AddChild(btn);
            }
        }

        UpdatePortraitColor();
    }

    private void OnResponseSelected(string nextNodeId)
    {
        var nextNode = CrawlerDialogueDatabase.GetNode(nextNodeId);
        if (nextNode != null)
        {
            ShowDialogueNode(nextNode);
        }
        else
        {
            GD.PrintErr($"[CrawlerDialogueUI3D] Node not found: {nextNodeId}");
            Close();
        }
    }

    private void UpdatePortraitColor()
    {
        // Set portrait background color based on crawler type
        Color portraitColor = _crawlerType?.ToLower() switch
        {
            "crawler_rex" => new Color(0.4f, 0.25f, 0.2f),    // Brown/tan (veteran)
            "crawler_lily" => new Color(0.3f, 0.4f, 0.35f),   // Pale green (nervous)
            "crawler_chad" => new Color(0.45f, 0.35f, 0.15f), // Gold (flashy)
            "crawler_shade" => new Color(0.15f, 0.1f, 0.2f),  // Dark purple (mysterious)
            "crawler_hank" => new Color(0.35f, 0.3f, 0.25f),  // Warm brown (friendly)
            _ => new Color(0.2f, 0.2f, 0.25f)
        };

        // Update the portrait silhouette
        if (_npcPortrait?.GetChildCount() > 0)
        {
            var silhouette = _npcPortrait.GetChild(0) as Label;
            if (silhouette != null)
            {
                silhouette.Text = _crawlerType?.ToLower() switch
                {
                    "crawler_rex" => "[VET]",
                    "crawler_lily" => "[?!?]",
                    "crawler_chad" => "[MVP]",
                    "crawler_shade" => "[...]",
                    "crawler_hank" => ":D",
                    _ => "[?]"
                };
                silhouette.AddThemeColorOverride("font_color", portraitColor.Lightened(0.3f));
            }
        }
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

        // Stop drag if mouse released
        if (_isDragging && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
        {
            _isDragging = false;
            if (_panel != null)
            {
                WindowPositionManager.SetPosition(WindowName, _panel.Position);
            }
        }

        // Close on ESC
        if (@event.IsActionPressed("escape"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }

        // Number keys for response selection
        if (_currentNode != null && !_currentNode.IsEndNode)
        {
            for (int i = 0; i < _currentNode.Responses.Count && i < 9; i++)
            {
                if (@event.IsActionPressed($"hotbar_{i + 1}"))
                {
                    OnResponseSelected(_currentNode.Responses[i].NextNodeId);
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
        }

        // Enter/Space to continue on end nodes
        if (_currentNode?.IsEndNode == true)
        {
            if (@event is InputEventKey key && key.Pressed)
            {
                if (key.Keycode == Key.Enter || key.Keycode == Key.Space)
                {
                    Close();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    /// <summary>
    /// Open the dialogue UI for a specific crawler NPC.
    /// </summary>
    public void Open(BaseNPC3D npc, string crawlerType)
    {
        _currentNpc = npc;
        _crawlerType = crawlerType;

        // Get starting dialogue
        var startNode = CrawlerDialogueDatabase.GetStartingDialogue(crawlerType);
        if (startNode == null)
        {
            GD.PrintErr($"[CrawlerDialogueUI3D] No dialogue found for type: {crawlerType}");
            return;
        }

        ShowDialogueNode(startNode);

        // Show UI
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Position panel
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

        // Lock player controls
        if (Player.FPSController.Instance != null)
        {
            Player.FPSController.Instance.MouseControlLocked = true;
        }

        GD.Print($"[CrawlerDialogueUI3D] Opened for {crawlerType}");
    }

    /// <summary>
    /// Close the dialogue UI.
    /// </summary>
    public void Close()
    {
        _currentNpc = null;
        _crawlerType = null;
        _currentNode = null;
        Visible = false;

        Input.MouseMode = Input.MouseModeEnum.Captured;

        if (Player.FPSController.Instance != null)
        {
            Player.FPSController.Instance.MouseControlLocked = false;
        }

        GD.Print("[CrawlerDialogueUI3D] Closed");
    }

    public bool IsOpen => Visible;

    public override void _ExitTree()
    {
        Instance = null;
    }
}
