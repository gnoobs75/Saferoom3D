using Godot;
using System.Collections.Generic;
using System.Linq;
using SafeRoom3D.Core;
using SafeRoom3D.Player;
using SafeRoom3D.Enemies;
using SafeRoom3D.Pet;
using SafeRoom3D.Abilities;
using SafeRoom3D.Items;

namespace SafeRoom3D.UI;

/// <summary>
/// Inspect Mode - Pauses enemies and player movement while allowing camera look.
/// Player can mouse over enemies/items or cycle through targets with R/Shift+R.
/// Info panel is shown in bottom-right corner.
/// Activated by pressing Space.
/// </summary>
public partial class InspectMode3D : CanvasLayer
{
    public static InspectMode3D? Instance { get; private set; }

    // Targeting range - enough to see monsters in dungeon corners from spawn
    public const float TargetingRange = 60f;

    // State
    public bool IsActive { get; private set; }
    private Node3D? _currentTarget;
    private List<Node3D> _visibleTargets = new();
    private int _targetIndex = 0;
    private Camera3D? _camera;

    // Always-visible info panel tracking
    private float _autoTargetUpdateTimer = 0f;
    private const float AutoTargetUpdateInterval = 0.25f; // Update target every 0.25s when not in inspect mode

    // UI Elements
    private ColorRect? _overlay;
    private PanelContainer? _infoPanel;
    private TextureRect? _portrait;
    private Label? _nameLabel;
    private Label? _levelLabel;
    private ProgressBar? _healthBar;
    private Label? _healthLabel;
    private Label? _statsLabel;
    private Label? _descriptionLabel;
    private Label? _promptLabel;

    // Target indicator (3D marker on selected target)
    private Node3D? _targetMarker;

    // Monster descriptions
    private static readonly Dictionary<string, string> MonsterDescriptions = new()
    {
        ["goblin"] = "A small, cunning creature with a taste for shiny objects and violence. Goblins are cowardly alone but dangerous in groups.",
        ["goblin_shaman"] = "A goblin who has learned dark magic. Can buff allies with an enrage spell and hurls magical projectiles.",
        ["goblin_thrower"] = "A goblin trained in ranged combat. Throws spears, axes, and occasionally beer cans with surprising accuracy.",
        ["slime"] = "A gelatinous blob that dissolves anything it engulfs. Slow but persistent, and surprisingly bouncy.",
        ["eye"] = "A floating eyeball from another dimension. Its gaze can pierce the soul and its psychic attacks are devastating.",
        ["mushroom"] = "A sentient fungus that releases toxic spores. Don't let its cute appearance fool you.",
        ["spider"] = "An eight-legged nightmare with venomous fangs. Extremely fast and can climb any surface.",
        ["lizard"] = "A cold-blooded reptilian warrior. Strong, tough, and can regenerate lost limbs.",
        ["skeleton"] = "The reanimated bones of a fallen warrior. Tireless and immune to fear.",
        ["wolf"] = "A fierce predator that hunts in packs. Lightning fast and relentless.",
        ["bat"] = "A winged terror of the dark. Echolocation makes them nearly impossible to ambush.",
        ["dragon"] = "The apex predator of the dungeon. Ancient, powerful, and utterly terrifying.",
        ["badlama"] = "A foul-tempered llama corrupted by dungeon magic. Can breathe fire and delivers a nasty bite. Its adorable appearance belies its murderous intent.",
        ["skeleton_lord"] = "A mighty undead commander. His dark presence strengthens nearby skeletons.",
        ["dragon_king"] = "The most powerful dragon ever encountered. Legends say its fire can melt steel instantly.",
        ["spider_queen"] = "Mother of all spiders. Can summon swarms of her children to overwhelm intruders.",
        ["steve"] = "Your loyal companion chihuahua. Steve heals you when you take damage and fires magic missiles at enemies threatening you.",
    };

    // NPC descriptions
    private static readonly Dictionary<string, string> NpcDescriptions = new()
    {
        ["steve"] = "Steve the Chihuahua - Your faithful companion. Despite his small size, Steve has a big heart. He automatically heals you when you take damage and fires magic missiles at enemies who threaten you. Abilities have a 10 second cooldown.",
    };

    public override void _Ready()
    {
        Instance = this;
        Layer = 100; // Above most UI
        ProcessMode = ProcessModeEnum.Always; // Process even when paused

        CreateUI();
        CreateTargetMarker();

        // Get camera reference
        CallDeferred(nameof(FindCamera));

        GD.Print("[InspectMode3D] Ready");
    }

    private void FindCamera()
    {
        _camera = FPSController.Instance?.GetViewport().GetCamera3D();
    }

    private void CreateUI()
    {
        // Semi-transparent overlay at top
        _overlay = new ColorRect();
        _overlay.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _overlay.CustomMinimumSize = new Vector2(0, 50);
        _overlay.Set("color", new Color(0, 0, 0, 0.7f));
        _overlay.Visible = false;
        AddChild(_overlay);

        // Prompt in overlay
        _promptLabel = new Label();
        _promptLabel.Text = "INSPECT MODE - [R] Next Target | [Shift+R] Previous | [SPACE/ESC] Exit";
        _promptLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _promptLabel.Position = new Vector2(-300, -12);
        _promptLabel.AddThemeFontSizeOverride("font_size", 18);
        _promptLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
        _overlay.AddChild(_promptLabel);

        // Info panel (bottom-right corner, grows up and left)
        // Hidden by default, shown only in inspect mode
        _infoPanel = new PanelContainer();
        _infoPanel.Name = "DescriptionPanel";
        _infoPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        // Position: same 40px margin from bottom as action bar, 20px from right edge
        // Panel grows up/left from anchor point
        _infoPanel.Position = new Vector2(-340, -440);  // 400 height + 40px bottom margin
        _infoPanel.CustomMinimumSize = new Vector2(320, 400);
        _infoPanel.Visible = false;  // Hidden by default, shown in inspect mode

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.4f, 0.35f, 0.25f);
        panelStyle.SetCornerRadiusAll(8);
        _infoPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 15);
        margin.AddThemeConstantOverride("margin_right", 15);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        _infoPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        // Header with portrait and name
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 15);
        vbox.AddChild(header);

        // Portrait (placeholder - colored rectangle)
        _portrait = new TextureRect();
        _portrait.CustomMinimumSize = new Vector2(80, 80);
        _portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        header.AddChild(_portrait);

        // Name and level
        var nameVbox = new VBoxContainer();
        nameVbox.AddThemeConstantOverride("separation", 5);
        header.AddChild(nameVbox);

        _nameLabel = new Label();
        _nameLabel.Text = "No Target";
        _nameLabel.AddThemeFontSizeOverride("font_size", 22);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
        nameVbox.AddChild(_nameLabel);

        _levelLabel = new Label();
        _levelLabel.Text = "";
        _levelLabel.AddThemeFontSizeOverride("font_size", 14);
        _levelLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f));
        nameVbox.AddChild(_levelLabel);

        // Health bar
        var healthContainer = new VBoxContainer();
        healthContainer.AddThemeConstantOverride("separation", 3);
        vbox.AddChild(healthContainer);

        var healthHeaderRow = new HBoxContainer();
        var healthTitle = new Label { Text = "Health" };
        healthTitle.AddThemeFontSizeOverride("font_size", 14);
        healthHeaderRow.AddChild(healthTitle);
        healthHeaderRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _healthLabel = new Label { Text = "" };
        _healthLabel.AddThemeFontSizeOverride("font_size", 14);
        healthHeaderRow.AddChild(_healthLabel);
        healthContainer.AddChild(healthHeaderRow);

        _healthBar = new ProgressBar();
        _healthBar.CustomMinimumSize = new Vector2(0, 20);
        _healthBar.MaxValue = 100;
        _healthBar.Value = 100;
        _healthBar.ShowPercentage = false;

        var healthBg = new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.15f) };
        _healthBar.AddThemeStyleboxOverride("background", healthBg);
        var healthFill = new StyleBoxFlat { BgColor = new Color(0.8f, 0.2f, 0.2f) };
        _healthBar.AddThemeStyleboxOverride("fill", healthFill);
        healthContainer.AddChild(_healthBar);

        // Separator
        var sep = new HSeparator();
        vbox.AddChild(sep);

        // Stats
        _statsLabel = new Label();
        _statsLabel.Text = "Select a target to view stats";
        _statsLabel.AddThemeFontSizeOverride("font_size", 14);
        _statsLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
        vbox.AddChild(_statsLabel);

        // Another separator
        var sep2 = new HSeparator();
        vbox.AddChild(sep2);

        // Description
        _descriptionLabel = new Label();
        _descriptionLabel.Text = "Press R to cycle through visible targets.";
        _descriptionLabel.AddThemeFontSizeOverride("font_size", 13);
        _descriptionLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        _descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _descriptionLabel.CustomMinimumSize = new Vector2(280, 0);
        vbox.AddChild(_descriptionLabel);

        AddChild(_infoPanel);

        // Apply saved position if exists
        var savedPos = WindowPositionManager.GetPosition("HUD_DescriptionPanel");
        if (savedPos != WindowPositionManager.CenterMarker)
        {
            _infoPanel.Position = savedPos;
        }
    }

    private void CreateTargetMarker()
    {
        // 3D marker to show on selected target - bouncing arrow pointing down
        _targetMarker = new Node3D();
        _targetMarker.Name = "InspectTargetMarker";

        // Create arrow shape using a cone (pointing down)
        var arrowMesh = new MeshInstance3D();
        var coneMesh = new CylinderMesh();
        coneMesh.TopRadius = 0f;
        coneMesh.BottomRadius = 0.3f;
        coneMesh.Height = 0.6f;
        coneMesh.RadialSegments = 8;
        arrowMesh.Mesh = coneMesh;
        arrowMesh.RotationDegrees = new Vector3(180, 0, 0); // Point downward

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.9f, 0.3f, 0.8f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.8f, 0.2f);
        mat.EmissionEnergyMultiplier = 1.5f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        arrowMesh.MaterialOverride = mat;

        _targetMarker.AddChild(arrowMesh);
        _targetMarker.Visible = false;
    }

    // For bouncing animation
    private float _markerBounceTime = 0f;

    public override void _Process(double delta)
    {
        // Always update the info panel, even when not in inspect mode
        if (!IsActive)
        {
            // Auto-target nearest visible enemy when not in inspect mode
            _autoTargetUpdateTimer += (float)delta;
            if (_autoTargetUpdateTimer >= AutoTargetUpdateInterval)
            {
                _autoTargetUpdateTimer = 0f;
                UpdateAutoTarget();
            }

            // Update info for current auto-target
            UpdateCurrentTargetInfo();
        }
        else
        {
            // Update target marker position/rotation when in inspect mode
            UpdateTargetMarker((float)delta);

            // Check for UI hover (action bar slots)
            CheckUIHover();

            // Update info for targeted entity (it may have taken damage)
            // Only if we're not hovering over a UI element
            if (!_isHoveringUI)
            {
                UpdateCurrentTargetInfo();
            }

            // Camera look is disabled in inspect mode (cursor is visible)
        }
    }

    private bool _isHoveringUI = false;
    private string? _lastHoveredSlotKey = null;

    /// <summary>
    /// Check if mouse is hovering over UI elements like action bar slots.
    /// </summary>
    private void CheckUIHover()
    {
        var mousePos = GetViewport().GetMousePosition();

        // Check HUD3D action bar slots
        var hud = HUD3D.Instance;
        if (hud == null) return;

        // Get hovered slot info from HUD
        var slotInfo = hud.GetSlotAtPosition(mousePos);

        if (slotInfo.HasValue)
        {
            var (row, slot, abilityId, consumableId) = slotInfo.Value;
            string slotKey = $"{row}_{slot}";

            // Only update if hovering a different slot
            if (slotKey != _lastHoveredSlotKey)
            {
                _lastHoveredSlotKey = slotKey;
                _isHoveringUI = true;

                if (!string.IsNullOrEmpty(abilityId))
                {
                    GD.Print($"[InspectMode3D] Hovering ability slot: {abilityId}");
                    ShowAbilityInfo(abilityId);
                }
                else if (!string.IsNullOrEmpty(consumableId))
                {
                    GD.Print($"[InspectMode3D] Hovering consumable slot: {consumableId}");
                    ShowItemInfo(consumableId);
                }
            }
        }
        else
        {
            if (_isHoveringUI)
            {
                // Just stopped hovering UI, go back to showing target
                _isHoveringUI = false;
                _lastHoveredSlotKey = null;
            }
        }
    }

    private void ProcessCameraLook()
    {
        // Camera look is handled by FPSController when it detects inspect mode
        // We just need to ensure FPSController knows we're in inspect mode
    }

    /// <summary>
    /// Auto-target the nearest visible enemy when not in inspect mode.
    /// This keeps the info panel updated with the closest threat.
    /// </summary>
    private void UpdateAutoTarget()
    {
        if (_camera == null || FPSController.Instance == null) return;

        var playerPos = FPSController.Instance.GlobalPosition;
        var viewportSize = GetViewport().GetVisibleRect().Size;

        Node3D? nearestTarget = null;
        float nearestDist = float.MaxValue;

        // Check all enemies
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D entity && GodotObject.IsInstanceValid(entity))
            {
                float dist = entity.GlobalPosition.DistanceTo(playerPos);
                if (dist > TargetingRange) continue;

                // Check if on screen
                if (!IsPositionOnScreen(entity.GlobalPosition + Vector3.Up, viewportSize))
                    continue;

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestTarget = entity;
                }
            }
        }

        // Also check Steve
        if (Steve3D.Instance != null)
        {
            float steveDist = Steve3D.Instance.GlobalPosition.DistanceTo(playerPos);
            if (steveDist <= TargetingRange &&
                IsPositionOnScreen(Steve3D.Instance.GlobalPosition + Vector3.Up * 0.3f, viewportSize) &&
                steveDist < nearestDist)
            {
                nearestTarget = Steve3D.Instance;
            }
        }

        // Update current target
        _currentTarget = nearestTarget;
    }

    /// <summary>
    /// Update the info panel with current target's data.
    /// Called every frame to keep health values current.
    /// </summary>
    private void UpdateCurrentTargetInfo()
    {
        if (_currentTarget == null || !GodotObject.IsInstanceValid(_currentTarget))
        {
            ShowNoTargetInfo();
            return;
        }

        // Update info based on target type
        if (_currentTarget is BasicEnemy3D enemy)
        {
            ShowEnemyInfo(enemy.MonsterType, enemy.CurrentHealth, enemy.MaxHealth,
                (int)enemy.Damage, enemy.MoveSpeed, enemy.AggroRange, false);
        }
        else if (_currentTarget is BossEnemy3D boss)
        {
            ShowEnemyInfo(boss.MonsterType, boss.CurrentHealth, boss.MaxHealth,
                (int)boss.Damage, boss.MoveSpeed, boss.AggroRange, true);
        }
        else if (_currentTarget is Steve3D)
        {
            ShowSteveInfo();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("inspect_mode"))
        {
            if (IsActive)
                Deactivate();
            else
                Activate();

            GetViewport().SetInputAsHandled();
        }

        if (!IsActive) return;

        // Exit on Escape too
        if (@event.IsActionPressed("escape"))
        {
            Deactivate();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Target cycling with R key
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.R || keyEvent.PhysicalKeycode == Key.R)
            {
                if (keyEvent.ShiftPressed)
                    CyclePreviousTarget();
                else
                    CycleNextTarget();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void Activate()
    {
        if (IsActive) return;

        IsActive = true;

        // Pause the game tree (enemies, physics, etc.)
        GetTree().Paused = true;

        // Also freeze enemies explicitly for any that ignore pause
        FreezeEnemies(true);

        // Show mouse cursor - no camera movement during inspect mode
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (_overlay != null) _overlay.Visible = true;
        if (_infoPanel != null) _infoPanel.Visible = true;  // Show info panel in inspect mode

        // Gather visible targets
        UpdateVisibleTargets();

        // Show target marker
        if (_targetMarker != null)
        {
            _targetMarker.Visible = true;
            GetTree().Root.AddChild(_targetMarker);
        }

        // Select first target if any
        if (_visibleTargets.Count > 0)
        {
            _targetIndex = 0;
            SelectTarget(_visibleTargets[0]);
        }
        else
        {
            ShowNoTargetInfo();
        }

        GD.Print($"[InspectMode3D] Activated - {_visibleTargets.Count} targets in range");
    }

    public void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;

        // Unpause the game tree
        GetTree().Paused = false;

        // Unfreeze enemies
        FreezeEnemies(false);

        // Capture mouse for FPS camera control
        Input.MouseMode = Input.MouseModeEnum.Captured;

        if (_overlay != null) _overlay.Visible = false;
        if (_infoPanel != null) _infoPanel.Visible = false;  // Hide info panel outside inspect mode

        // Hide target marker
        if (_targetMarker != null)
        {
            _targetMarker.Visible = false;
            if (_targetMarker.GetParent() != null)
                _targetMarker.GetParent().RemoveChild(_targetMarker);
        }

        _currentTarget = null;
        _visibleTargets.Clear();

        GD.Print("[InspectMode3D] Deactivated");
    }

    private void FreezeEnemies(bool freeze)
    {
        // Freeze/unfreeze all enemies - including specialized types
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D enemy3D)
            {
                // Freeze physics and processing for ALL enemy types
                enemy3D.SetPhysicsProcess(!freeze);
                enemy3D.SetProcess(!freeze);
            }
        }

        // Also freeze any projectiles in flight
        var projectiles = GetTree().GetNodesInGroup("Projectiles");
        foreach (var node in projectiles)
        {
            if (node is Node3D proj)
            {
                proj.SetPhysicsProcess(!freeze);
                proj.SetProcess(!freeze);
            }
        }

        // Also freeze Steve
        if (Steve3D.Instance != null)
        {
            Steve3D.Instance.SetPhysicsProcess(!freeze);
            Steve3D.Instance.SetProcess(!freeze);
        }

        // Tell player controller we're in inspect mode
        if (FPSController.Instance != null)
        {
            FPSController.Instance.SetInspectMode(freeze);
        }
    }

    private void UpdateVisibleTargets()
    {
        _visibleTargets.Clear();

        if (_camera == null || FPSController.Instance == null) return;

        var playerPos = FPSController.Instance.GlobalPosition;
        var cameraForward = -_camera.GlobalTransform.Basis.Z;
        var viewportSize = GetViewport().GetVisibleRect().Size;

        // Get all enemies
        var enemies = GetTree().GetNodesInGroup("Enemies");
        foreach (var node in enemies)
        {
            if (node is Node3D entity && GodotObject.IsInstanceValid(entity))
            {
                // Check distance
                float dist = entity.GlobalPosition.DistanceTo(playerPos);
                if (dist > TargetingRange) continue;

                // Check if actually visible on screen using camera projection
                if (!IsPositionOnScreen(entity.GlobalPosition + Vector3.Up, viewportSize))
                    continue;

                _visibleTargets.Add(entity);
            }
        }

        // Add Steve if visible
        if (Steve3D.Instance != null)
        {
            float steveDist = Steve3D.Instance.GlobalPosition.DistanceTo(playerPos);
            if (steveDist <= TargetingRange)
            {
                if (IsPositionOnScreen(Steve3D.Instance.GlobalPosition + Vector3.Up * 0.3f, viewportSize))
                {
                    _visibleTargets.Add(Steve3D.Instance);
                }
            }
        }

        // Sort by distance
        _visibleTargets = _visibleTargets
            .OrderBy(t => t.GlobalPosition.DistanceTo(FPSController.Instance.GlobalPosition))
            .ToList();
    }

    private bool IsPositionOnScreen(Vector3 worldPos, Vector2 viewportSize)
    {
        if (_camera == null) return false;

        // Check if in front of camera first
        var toTarget = (worldPos - _camera.GlobalPosition).Normalized();
        var cameraForward = -_camera.GlobalTransform.Basis.Z;
        if (cameraForward.Dot(toTarget) < 0.1f) return false; // Behind or too far to side

        // Project to screen
        if (!_camera.IsPositionBehind(worldPos))
        {
            var screenPos = _camera.UnprojectPosition(worldPos);
            // Check if within screen bounds with some margin
            float margin = 50f;
            return screenPos.X >= -margin && screenPos.X <= viewportSize.X + margin &&
                   screenPos.Y >= -margin && screenPos.Y <= viewportSize.Y + margin;
        }
        return false;
    }

    private void CycleNextTarget()
    {
        UpdateVisibleTargets();
        if (_visibleTargets.Count == 0)
        {
            ShowNoTargetInfo();
            return;
        }

        _targetIndex = (_targetIndex + 1) % _visibleTargets.Count;
        SelectTarget(_visibleTargets[_targetIndex]);
    }

    private void CyclePreviousTarget()
    {
        UpdateVisibleTargets();
        if (_visibleTargets.Count == 0)
        {
            ShowNoTargetInfo();
            return;
        }

        _targetIndex--;
        if (_targetIndex < 0) _targetIndex = _visibleTargets.Count - 1;
        SelectTarget(_visibleTargets[_targetIndex]);
    }

    private void SelectTarget(Node3D target)
    {
        _currentTarget = target;

        // Update info panel
        if (target is BasicEnemy3D enemy)
        {
            ShowEnemyInfo(enemy.MonsterType, enemy.CurrentHealth, enemy.MaxHealth,
                (int)enemy.Damage, enemy.MoveSpeed, enemy.AggroRange, false);
        }
        else if (target is BossEnemy3D boss)
        {
            ShowEnemyInfo(boss.MonsterType, boss.CurrentHealth, boss.MaxHealth,
                (int)boss.Damage, boss.MoveSpeed, boss.AggroRange, true);
        }
        else if (target is Steve3D)
        {
            ShowSteveInfo();
        }

        // Position target marker
        if (_targetMarker != null && _currentTarget != null)
        {
            _targetMarker.GlobalPosition = _currentTarget.GlobalPosition + new Vector3(0, 1.5f, 0);
        }

        // Very subtle sound for target cycling (commented out - too annoying)
        // SoundManager3D.Instance?.PlaySound("menu_select");
    }

    private void ShowNoTargetInfo()
    {
        if (_nameLabel != null) _nameLabel.Text = "No Targets";
        if (_levelLabel != null) _levelLabel.Text = "";
        if (_healthLabel != null) _healthLabel.Text = "";
        if (_healthBar != null) _healthBar.Value = 0;
        if (_statsLabel != null) _statsLabel.Text = "No enemies in view.";
        if (_descriptionLabel != null) _descriptionLabel.Text = "Turn around to find targets, or they may all be defeated!";

        UpdatePortrait("none", false);
    }

    private void ShowEnemyInfo(string monsterType, int currentHp, int maxHp,
        int damage, float speed, float aggro, bool isBoss)
    {
        if (_infoPanel == null) return;

        // Format name
        string displayName = monsterType.Replace("_", " ");
        displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(displayName);
        if (isBoss) displayName = "[BOSS] " + displayName;

        if (_nameLabel != null) _nameLabel.Text = displayName;

        // Level based on stats
        int level = Mathf.Max(1, maxHp / 50);
        if (isBoss) level *= 2;
        if (_levelLabel != null) _levelLabel.Text = $"Level {level}";

        // Health
        if (_healthBar != null)
        {
            _healthBar.MaxValue = maxHp;
            _healthBar.Value = currentHp;
        }
        if (_healthLabel != null) _healthLabel.Text = $"{currentHp}/{maxHp}";

        // Stats
        if (_statsLabel != null)
        {
            _statsLabel.Text = $"Damage: {damage}\n" +
                              $"Speed: {speed:F1} m/s\n" +
                              $"Aggro Range: {aggro:F0}m";
        }

        // Description
        string key = monsterType.ToLower().Replace(" ", "_");
        string desc = MonsterDescriptions.GetValueOrDefault(key,
            "A dangerous creature lurking in the dungeon depths.");
        if (_descriptionLabel != null) _descriptionLabel.Text = desc;

        // Update portrait color based on monster type
        UpdatePortrait(monsterType, isBoss);
    }

    private void ShowSteveInfo()
    {
        if (_nameLabel != null) _nameLabel.Text = "Steve the Chihuahua";
        if (_levelLabel != null) _levelLabel.Text = "Companion";

        // Steve's "health" is the player's health he protects
        if (_healthBar != null)
        {
            _healthBar.MaxValue = 100;
            _healthBar.Value = 100;
        }
        if (_healthLabel != null) _healthLabel.Text = "Immortal";

        // Stats
        float healCd = Steve3D.Instance?.GetHealCooldown() ?? 0;
        float missileCd = Steve3D.Instance?.GetMissileCooldown() ?? 0;

        if (_statsLabel != null)
        {
            _statsLabel.Text = $"Heal Amount: {Steve3D.HealAmount}\n" +
                              $"Missile Damage: {Steve3D.MagicMissileDamage}\n" +
                              $"Heal CD: {healCd:F1}s | Missile CD: {missileCd:F1}s";
        }

        // Description
        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = NpcDescriptions.GetValueOrDefault("steve",
                "Your loyal chihuahua companion.");
        }

        // Chihuahua colored portrait
        UpdatePortrait("steve", false);
    }

    private void UpdatePortrait(string monsterType, bool isBoss)
    {
        // Create a colored placeholder for the portrait
        var image = Image.CreateEmpty(80, 80, false, Image.Format.Rgba8);

        Color baseColor = monsterType.ToLower() switch
        {
            "goblin" => new Color(0.45f, 0.55f, 0.35f),
            "goblin_shaman" => new Color(0.3f, 0.5f, 0.5f),
            "goblin_thrower" => new Color(0.5f, 0.45f, 0.35f),
            "slime" => new Color(0.3f, 0.8f, 0.4f),
            "eye" => new Color(0.9f, 0.2f, 0.2f),
            "mushroom" => new Color(0.6f, 0.4f, 0.6f),
            "spider" => new Color(0.2f, 0.15f, 0.2f),
            "lizard" => new Color(0.3f, 0.5f, 0.25f),
            "skeleton" => new Color(0.9f, 0.88f, 0.8f),
            "wolf" => new Color(0.4f, 0.35f, 0.3f),
            "bat" => new Color(0.25f, 0.2f, 0.25f),
            "dragon" => new Color(0.7f, 0.2f, 0.15f),
            "badlama" => new Color(0.55f, 0.4f, 0.25f), // Brown llama
            "steve" => new Color(0.8f, 0.65f, 0.45f), // Tan chihuahua
            "ability" => new Color(0.3f, 0.4f, 0.7f), // Blue for abilities
            "common" => new Color(0.6f, 0.6f, 0.6f),  // Gray for common items
            "uncommon" => new Color(0.3f, 0.7f, 0.3f), // Green for uncommon
            "rare" => new Color(0.3f, 0.5f, 0.9f),    // Blue for rare
            "epic" => new Color(0.6f, 0.3f, 0.8f),    // Purple for epic
            "legendary" => new Color(0.9f, 0.6f, 0.2f), // Orange for legendary
            "none" => new Color(0.2f, 0.2f, 0.25f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };

        if (isBoss)
        {
            baseColor = baseColor.Lightened(0.1f);
        }

        // Fill with color and add simple "face" pattern
        image.Fill(baseColor);

        // Add border
        Color borderColor = monsterType == "steve" ?
            new Color(0.4f, 0.8f, 0.5f) : // Green for friendly
            (isBoss ? new Color(1f, 0.8f, 0.2f) : new Color(0.3f, 0.3f, 0.35f));

        for (int x = 0; x < 80; x++)
        {
            for (int y = 0; y < 80; y++)
            {
                if (x < 3 || x > 76 || y < 3 || y > 76)
                {
                    image.SetPixel(x, y, borderColor);
                }
            }
        }

        // Simple eyes
        Color eyeColor = Colors.White;
        for (int ex = 25; ex < 35; ex++)
        {
            for (int ey = 30; ey < 40; ey++)
            {
                image.SetPixel(ex, ey, eyeColor);
            }
        }
        for (int ex = 45; ex < 55; ex++)
        {
            for (int ey = 30; ey < 40; ey++)
            {
                image.SetPixel(ex, ey, eyeColor);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        if (_portrait != null) _portrait.Texture = texture;
    }

    private void UpdateTargetMarker(float delta)
    {
        if (_targetMarker == null || _currentTarget == null || !GodotObject.IsInstanceValid(_currentTarget))
        {
            if (_targetMarker != null) _targetMarker.Visible = false;
            return;
        }

        _targetMarker.Visible = true;

        // Bouncing animation
        _markerBounceTime += delta * 4f;
        float bounce = Mathf.Sin(_markerBounceTime) * 0.3f;

        // Position above target with bounce
        float baseHeight = 2.5f;
        _targetMarker.GlobalPosition = _currentTarget.GlobalPosition + new Vector3(0, baseHeight + bounce, 0);

        // Gentle rotation
        _targetMarker.RotateY(delta * 1.5f);
    }

    public override void _ExitTree()
    {
        if (_targetMarker != null && _targetMarker.GetParent() != null)
        {
            _targetMarker.GetParent().RemoveChild(_targetMarker);
            _targetMarker.QueueFree();
        }
        Instance = null;
    }

    /// <summary>
    /// Hide the info panel (e.g., when map or editor is open)
    /// </summary>
    public void HideInfoPanel()
    {
        if (_infoPanel != null)
        {
            _infoPanel.Visible = false;
        }
    }

    /// <summary>
    /// Show the info panel (e.g., when returning to gameplay)
    /// </summary>
    public void ShowInfoPanel()
    {
        if (_infoPanel != null)
        {
            _infoPanel.Visible = true;
        }
    }

    /// <summary>
    /// Show ability info in the inspect panel (called when hovering over action bar slots)
    /// </summary>
    public void ShowAbilityInfo(string abilityId)
    {
        if (_infoPanel == null) return;

        var ability = AbilityManager3D.Instance?.GetAbility(abilityId);
        if (ability == null)
        {
            GD.Print($"[InspectMode3D] Ability not found: {abilityId}");
            return;
        }

        // Name
        if (_nameLabel != null) _nameLabel.Text = ability.AbilityName;

        // Type as "level"
        if (_levelLabel != null) _levelLabel.Text = $"{ability.Type} Ability";

        // Show mana cost in health bar area (repurposed)
        if (_healthBar != null)
        {
            _healthBar.MaxValue = 100;
            _healthBar.Value = 100;
            // Change color to blue for mana
            var fillStyle = _healthBar.GetThemeStylebox("fill") as StyleBoxFlat;
            if (fillStyle != null) fillStyle.BgColor = new Color(0.2f, 0.4f, 0.9f);
        }
        if (_healthLabel != null) _healthLabel.Text = $"Mana: {ability.ManaCost}";

        // Stats
        if (_statsLabel != null)
        {
            string cooldownText = ability.Cooldown > 0 ? $"{ability.Cooldown:F1}s" : "None";
            _statsLabel.Text = $"Cooldown: {cooldownText}\n" +
                              $"Mana Cost: {ability.ManaCost}";
        }

        // Description
        if (_descriptionLabel != null) _descriptionLabel.Text = ability.Description;

        // Update portrait to ability-themed color
        UpdatePortrait("ability", false);
    }

    /// <summary>
    /// Show item/consumable info in the inspect panel
    /// </summary>
    public void ShowItemInfo(string itemId)
    {
        if (_infoPanel == null) return;

        // Try to find item in inventory
        InventoryItem? item = null;
        var inventory = Inventory3D.Instance;
        if (inventory != null)
        {
            foreach (var (_, invItem) in inventory.GetAllItems())
            {
                if (invItem.Id == itemId)
                {
                    item = invItem;
                    break;
                }
            }
        }

        // Format display name from ID
        string displayName = itemId.Replace("_", " ");
        displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(displayName);

        // Name
        if (_nameLabel != null) _nameLabel.Text = item?.Name ?? displayName;

        // Type as "level"
        if (_levelLabel != null) _levelLabel.Text = item != null ? $"{item.Type} Item" : "Consumable";

        // Show health bar for consumables (mana/health restore)
        if (_healthBar != null)
        {
            _healthBar.Visible = true;
            _healthBar.MaxValue = 100;
            _healthBar.Value = 100;
            // Green for consumables
            var fillStyle = _healthBar.GetThemeStylebox("fill") as StyleBoxFlat;
            if (fillStyle != null) fillStyle.BgColor = new Color(0.3f, 0.7f, 0.3f);
        }
        if (_healthLabel != null) _healthLabel.Text = "";

        // Stats
        if (_statsLabel != null)
        {
            if (item != null)
            {
                _statsLabel.Text = $"Type: {item.Type}\n" +
                                  $"Stack: {item.StackCount}/{item.MaxStackSize}";
            }
            else
            {
                _statsLabel.Text = "Type: Consumable";
            }
        }

        // Description
        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = item?.Description ?? "A useful item from your inventory.";
        }

        // Update portrait color
        UpdatePortrait("common", false);
    }
}
