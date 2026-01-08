using Godot;
using SafeRoom3D.Core;
using SafeRoom3D.NPC;
using System.Collections.Generic;

namespace SafeRoom3D.UI;

/// <summary>
/// UI panel for interacting with quest givers like Mordecai.
/// Shows available quests, active quests, and completed quests ready for turn-in.
/// </summary>
public partial class QuestUI3D : Control
{
	public static QuestUI3D? Instance { get; private set; }

	private Mordecai3D? _currentNpc;
	private Panel? _panel;
	private Label? _titleLabel;
	private TabContainer? _tabContainer;

	// Tab panels
	private VBoxContainer? _availableTab;
	private VBoxContainer? _activeTab;
	private VBoxContainer? _completedTab;

	// Quest detail panel
	private Panel? _detailPanel;
	private Label? _questTitleLabel;
	private RichTextLabel? _questDescLabel;
	private VBoxContainer? _objectivesContainer;
	private Label? _rewardsLabel;
	private Button? _acceptButton;
	private Button? _turnInButton;

	private Quest? _selectedQuest;

	// Window dragging
	private const string WindowName = "QuestUI";
	private bool _isDragging;
	private Vector2 _dragOffset;
	private Control? _dragHeader;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		CreateUI();
		GD.Print("[QuestUI3D] Ready");
	}

	private void CreateUI()
	{
		// Main panel (900x600)
		_panel = new Panel();
		_panel.CustomMinimumSize = new Vector2(900, 600);
		_panel.SetAnchorsPreset(LayoutPreset.Center);
		_panel.Position = new Vector2(-450, -300);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.08f, 0.08f, 0.1f, 0.97f);
		panelStyle.BorderColor = new Color(0.5f, 0.4f, 0.6f); // Purple-ish for quest giver
		panelStyle.SetBorderWidthAll(2);
		panelStyle.SetCornerRadiusAll(8);
		_panel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(_panel);

		// Draggable header
		_dragHeader = new Control();
		_dragHeader.Position = Vector2.Zero;
		_dragHeader.Size = new Vector2(900, 50);
		_dragHeader.MouseFilter = MouseFilterEnum.Stop;
		_dragHeader.GuiInput += OnHeaderGuiInput;
		_panel.AddChild(_dragHeader);

		// Title
		_titleLabel = new Label();
		_titleLabel.Text = "Mordecai the Game Guide";
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.Position = new Vector2(0, 10);
		_titleLabel.Size = new Vector2(900, 35);
		_titleLabel.AddThemeFontSizeOverride("font_size", 28);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.9f));
		_titleLabel.MouseFilter = MouseFilterEnum.Ignore;
		_panel.AddChild(_titleLabel);

		// Close button (X)
		var closeBtn = new Button();
		closeBtn.Text = "X";
		closeBtn.Position = new Vector2(855, 10);
		closeBtn.Size = new Vector2(35, 35);
		closeBtn.Pressed += Close;
		StyleCloseButton(closeBtn);
		_panel.AddChild(closeBtn);

		// Left side: Tab container for quest lists
		_tabContainer = new TabContainer();
		_tabContainer.Position = new Vector2(15, 55);
		_tabContainer.Size = new Vector2(400, 530);
		_tabContainer.TabChanged += OnTabChanged;
		_panel.AddChild(_tabContainer);

		// Available quests tab
		_availableTab = CreateQuestListTab("Available");
		_tabContainer.AddChild(_availableTab);

		// Active quests tab
		_activeTab = CreateQuestListTab("Active");
		_tabContainer.AddChild(_activeTab);

		// Completed quests tab
		_completedTab = CreateQuestListTab("Complete");
		_tabContainer.AddChild(_completedTab);

		// Right side: Quest detail panel
		_detailPanel = new Panel();
		_detailPanel.Position = new Vector2(430, 55);
		_detailPanel.Size = new Vector2(455, 530);

		var detailStyle = new StyleBoxFlat();
		detailStyle.BgColor = new Color(0.12f, 0.1f, 0.14f);
		detailStyle.BorderColor = new Color(0.4f, 0.35f, 0.5f);
		detailStyle.SetBorderWidthAll(1);
		detailStyle.SetCornerRadiusAll(4);
		_detailPanel.AddThemeStyleboxOverride("panel", detailStyle);
		_panel.AddChild(_detailPanel);

		// Quest title in detail panel
		_questTitleLabel = new Label();
		_questTitleLabel.Text = "Select a Quest";
		_questTitleLabel.Position = new Vector2(15, 15);
		_questTitleLabel.Size = new Vector2(425, 30);
		_questTitleLabel.AddThemeFontSizeOverride("font_size", 22);
		_questTitleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
		_detailPanel.AddChild(_questTitleLabel);

		// Quest description
		_questDescLabel = new RichTextLabel();
		_questDescLabel.Position = new Vector2(15, 55);
		_questDescLabel.Size = new Vector2(425, 150);
		_questDescLabel.BbcodeEnabled = true;
		_questDescLabel.AddThemeFontSizeOverride("normal_font_size", 16);
		_questDescLabel.AddThemeColorOverride("default_color", new Color(0.8f, 0.8f, 0.8f));
		_detailPanel.AddChild(_questDescLabel);

		// Objectives header
		var objHeader = new Label();
		objHeader.Text = "Objectives:";
		objHeader.Position = new Vector2(15, 215);
		objHeader.Size = new Vector2(425, 25);
		objHeader.AddThemeFontSizeOverride("font_size", 18);
		objHeader.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.9f));
		_detailPanel.AddChild(objHeader);

		// Objectives container
		_objectivesContainer = new VBoxContainer();
		_objectivesContainer.Position = new Vector2(15, 245);
		_objectivesContainer.Size = new Vector2(425, 150);
		_detailPanel.AddChild(_objectivesContainer);

		// Rewards label
		_rewardsLabel = new Label();
		_rewardsLabel.Text = "Rewards: --";
		_rewardsLabel.Position = new Vector2(15, 410);
		_rewardsLabel.Size = new Vector2(425, 50);
		_rewardsLabel.AddThemeFontSizeOverride("font_size", 16);
		_rewardsLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
		_rewardsLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_detailPanel.AddChild(_rewardsLabel);

		// Accept button (for available quests)
		_acceptButton = new Button();
		_acceptButton.Text = "Accept Quest";
		_acceptButton.Position = new Vector2(100, 475);
		_acceptButton.Size = new Vector2(250, 40);
		_acceptButton.Pressed += OnAcceptPressed;
		_acceptButton.Visible = false;
		StyleActionButton(_acceptButton, new Color(0.2f, 0.5f, 0.3f));
		_detailPanel.AddChild(_acceptButton);

		// Turn in button (for completed quests)
		_turnInButton = new Button();
		_turnInButton.Text = "Turn In Quest";
		_turnInButton.Position = new Vector2(100, 475);
		_turnInButton.Size = new Vector2(250, 40);
		_turnInButton.Pressed += OnTurnInPressed;
		_turnInButton.Visible = false;
		StyleActionButton(_turnInButton, new Color(0.5f, 0.4f, 0.2f));
		_detailPanel.AddChild(_turnInButton);
	}

	private VBoxContainer CreateQuestListTab(string name)
	{
		var scrollContainer = new ScrollContainer();
		scrollContainer.Name = name;
		scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		var container = new VBoxContainer();
		container.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		container.AddThemeConstantOverride("separation", 5);
		scrollContainer.AddChild(container);

		// Return a wrapper that holds the scroll container
		var wrapper = new VBoxContainer();
		wrapper.Name = name;
		wrapper.AddChild(scrollContainer);

		// Store reference to the actual list container
		scrollContainer.SetMeta("list_container", container);

		return wrapper;
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

	private void StyleActionButton(Button btn, Color baseColor)
	{
		var style = new StyleBoxFlat();
		style.BgColor = baseColor;
		style.BorderColor = baseColor.Lightened(0.3f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		btn.AddThemeStyleboxOverride("normal", style);

		var hoverStyle = style.Duplicate() as StyleBoxFlat;
		hoverStyle!.BgColor = baseColor.Lightened(0.2f);
		btn.AddThemeStyleboxOverride("hover", hoverStyle);

		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.9f));
	}

	private Button CreateQuestButton(Quest quest, bool showProgress = false)
	{
		var btn = new Button();
		btn.CustomMinimumSize = new Vector2(370, 50);
		btn.ClipText = true;

		string statusIcon = quest.Status switch
		{
			QuestStatus.Available => "[!]",
			QuestStatus.Active => "[>]",
			QuestStatus.ReadyToTurnIn => "[*]",
			_ => "[ ]"
		};

		string progressText = "";
		if (showProgress && quest.Objectives.Count > 0)
		{
			var obj = quest.Objectives[0];
			progressText = $" ({obj.CurrentCount}/{obj.RequiredCount})";
		}

		btn.Text = $"{statusIcon} {quest.Title}{progressText}";
		btn.SetMeta("quest_id", quest.Id);
		btn.Pressed += () => SelectQuest(quest);

		// Style based on status
		var style = new StyleBoxFlat();
		style.BgColor = quest.Status switch
		{
			QuestStatus.ReadyToTurnIn => new Color(0.3f, 0.35f, 0.2f),
			QuestStatus.Active => new Color(0.2f, 0.25f, 0.3f),
			_ => new Color(0.15f, 0.15f, 0.18f)
		};
		style.BorderColor = quest.Status == QuestStatus.ReadyToTurnIn
			? new Color(0.7f, 0.7f, 0.3f)
			: new Color(0.35f, 0.35f, 0.4f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		btn.AddThemeStyleboxOverride("normal", style);

		var hoverStyle = style.Duplicate() as StyleBoxFlat;
		hoverStyle!.BgColor = style.BgColor.Lightened(0.15f);
		btn.AddThemeStyleboxOverride("hover", hoverStyle);

		return btn;
	}

	private void SelectQuest(Quest quest)
	{
		_selectedQuest = quest;
		UpdateDetailPanel();
	}

	private void OnTabChanged(long tabIndex)
	{
		// Clear selection when switching tabs
		_selectedQuest = null;
		UpdateDetailPanel();
	}

	private void UpdateDetailPanel()
	{
		// Guard against being called before UI is initialized
		if (_questTitleLabel == null || _questDescLabel == null || _rewardsLabel == null ||
			_acceptButton == null || _turnInButton == null)
			return;

		if (_selectedQuest == null)
		{
			_questTitleLabel.Text = "Select a Quest";
			_questDescLabel.Text = "Choose a quest from the list on the left to view its details.";
			ClearObjectives();
			_rewardsLabel.Text = "Rewards: --";
			_acceptButton.Visible = false;
			_turnInButton.Visible = false;
			return;
		}

		_questTitleLabel.Text = _selectedQuest.Title;
		_questDescLabel.Text = _selectedQuest.Description;

		// Update objectives
		ClearObjectives();
		foreach (var obj in _selectedQuest.Objectives)
		{
			var objLabel = new Label();
			string checkmark = obj.IsComplete ? "[color=green][X][/color]" : "[ ]";
			objLabel.Text = $"{(obj.IsComplete ? "[X]" : "[ ]")} {obj.GetProgressText()}";
			objLabel.AddThemeFontSizeOverride("font_size", 15);
			objLabel.AddThemeColorOverride("font_color", obj.IsComplete
				? new Color(0.5f, 0.9f, 0.5f)
				: new Color(0.8f, 0.8f, 0.8f));
			_objectivesContainer?.AddChild(objLabel);
		}

		// Update rewards
		_rewardsLabel.Text = $"Rewards: {_selectedQuest.Reward.Gold} Gold, {_selectedQuest.Reward.Experience} XP";

		// Show appropriate button
		_acceptButton.Visible = _selectedQuest.Status == QuestStatus.Available;
		_turnInButton.Visible = _selectedQuest.Status == QuestStatus.ReadyToTurnIn;
	}

	private void ClearObjectives()
	{
		if (_objectivesContainer == null) return;
		foreach (var child in _objectivesContainer.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void OnAcceptPressed()
	{
		if (_selectedQuest == null || _currentNpc == null) return;
		if (_selectedQuest.Status != QuestStatus.Available) return;

		_currentNpc.AcceptQuest(_selectedQuest);
		RefreshQuestLists();

		// Switch to active tab
		_tabContainer!.CurrentTab = 1;
		UpdateDetailPanel();
	}

	private void OnTurnInPressed()
	{
		if (_selectedQuest == null || _currentNpc == null) return;
		if (_selectedQuest.Status != QuestStatus.ReadyToTurnIn) return;

		_currentNpc.TurnInQuest(_selectedQuest);
		_selectedQuest = null;
		RefreshQuestLists();
		UpdateDetailPanel();
	}

	private void RefreshQuestLists()
	{
		if (_currentNpc == null) return;

		// Clear all lists
		ClearQuestList(_availableTab!);
		ClearQuestList(_activeTab!);
		ClearQuestList(_completedTab!);

		// Populate available quests
		var available = _currentNpc.GetAvailableQuests();
		foreach (var quest in available)
		{
			AddQuestToList(_availableTab!, quest, false);
		}

		// Populate active quests
		var active = _currentNpc.GetActiveQuests();
		foreach (var quest in active)
		{
			AddQuestToList(_activeTab!, quest, true);
		}

		// Populate completed quests
		var completed = _currentNpc.GetCompletedQuests();
		foreach (var quest in completed)
		{
			AddQuestToList(_completedTab!, quest, true);
		}

		// Add empty state labels if needed
		if (available.Count == 0) AddEmptyLabel(_availableTab!, "No quests available right now.");
		if (active.Count == 0) AddEmptyLabel(_activeTab!, "No active quests from this NPC.");
		if (completed.Count == 0) AddEmptyLabel(_completedTab!, "No quests ready to turn in.");
	}

	private void ClearQuestList(VBoxContainer tab)
	{
		var scroll = tab.GetChild(0) as ScrollContainer;
		var container = scroll?.GetMeta("list_container").As<VBoxContainer>();
		if (container == null) return;

		foreach (var child in container.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void AddQuestToList(VBoxContainer tab, Quest quest, bool showProgress)
	{
		var scroll = tab.GetChild(0) as ScrollContainer;
		var container = scroll?.GetMeta("list_container").As<VBoxContainer>();
		if (container == null) return;

		var btn = CreateQuestButton(quest, showProgress);
		container.AddChild(btn);
	}

	private void AddEmptyLabel(VBoxContainer tab, string text)
	{
		var scroll = tab.GetChild(0) as ScrollContainer;
		var container = scroll?.GetMeta("list_container").As<VBoxContainer>();
		if (container == null) return;

		var label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		label.HorizontalAlignment = HorizontalAlignment.Center;
		container.AddChild(label);
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

		// Close on ESC or T
		if (@event.IsActionPressed("escape") || @event.IsActionPressed("interact"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>
	/// Open the quest UI for a specific NPC.
	/// </summary>
	public void Open(Mordecai3D npc)
	{
		_currentNpc = npc;
		_selectedQuest = null;

		// Update title
		_titleLabel!.Text = $"{npc.NPCName} the Game Guide";

		// Refresh quest lists
		RefreshQuestLists();
		UpdateDetailPanel();

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

		GD.Print($"[QuestUI3D] Opened for {npc.NPCName}");
	}

	/// <summary>
	/// Close the quest UI.
	/// </summary>
	public void Close()
	{
		_currentNpc = null;
		_selectedQuest = null;
		Visible = false;

		Input.MouseMode = Input.MouseModeEnum.Captured;

		if (Player.FPSController.Instance != null)
		{
			Player.FPSController.Instance.MouseControlLocked = false;
		}

		GD.Print("[QuestUI3D] Closed");
	}

	public bool IsOpen => Visible;

	public override void _ExitTree()
	{
		Instance = null;
	}
}
