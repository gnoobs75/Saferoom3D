using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Abilities.Effects;

/// <summary>
/// Chain Lightning ability - Lightning that bounces between enemies.
/// Targeted spell that chains to 3 enemies with diminishing damage.
/// </summary>
public partial class ChainLightning3D : Ability3D, ITargetedAbility
{
    public override string AbilityId => "chain_lightning";
    public override string AbilityName => "Chain Lightning";
    public override string Description => "Lightning that bounces between up to 3 enemies.";
    public override AbilityType Type => AbilityType.Targeted;

    public override float DefaultCooldown => 8f;
    public override int DefaultManaCost => 35;

    // Chain lightning stats
    public float BaseDamage { get; private set; } = 50f;
    public int MaxChains { get; private set; } = 3;
    public float ChainRadius { get; private set; } = 15f; // 240px / 16
    public float DamageDecay { get; private set; } = 0.5f; // 50% less each bounce

    // ITargetedAbility
    public float TargetRadius => ChainRadius;
    public Color TargetColor => new(0.4f, 0.6f, 1f); // Blue

    private Vector3 _targetPosition;

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }

    protected override void Activate()
    {
        // Find initial targets near target position
        var enemies = GetEnemiesInRadius(_targetPosition, ChainRadius);

        if (enemies.Count == 0)
        {
            GD.Print("[ChainLightning3D] No enemies in target area");
            return;
        }

        // Sort by distance to target position
        var sortedEnemies = new List<Node3D>();
        foreach (var enemy in enemies)
        {
            sortedEnemies.Add(enemy);
        }
        sortedEnemies.Sort((a, b) =>
            a.GlobalPosition.DistanceTo(_targetPosition)
            .CompareTo(b.GlobalPosition.DistanceTo(_targetPosition)));

        // Create the chain effect
        var chainEffect = new ChainLightningEffect3D();
        chainEffect.Name = "ChainLightningEffect";
        chainEffect.GlobalPosition = GetCameraPosition();
        chainEffect.Initialize(sortedEnemies, BaseDamage, MaxChains, DamageDecay, ChainRadius);

        GetTree().Root.AddChild(chainEffect);

        GD.Print($"[ChainLightning3D] Cast at {_targetPosition}, {sortedEnemies.Count} potential targets");
    }
}

/// <summary>
/// Visual and damage effect for chain lightning.
/// </summary>
public partial class ChainLightningEffect3D : Node3D
{
    private List<Node3D> _targets = new();
    private float _baseDamage;
    private int _maxChains;
    private float _damageDecay;
    private float _chainRadius;

    private int _currentChain;
    private float _chainDelay = 0.15f;
    private float _chainTimer;
    private Vector3 _lastPosition;
    private HashSet<Node3D> _hitTargets = new();

    private List<LightningBolt3D> _bolts = new();

    public void Initialize(List<Node3D> targets, float damage, int maxChains, float decay, float chainRadius)
    {
        _targets = targets;
        _baseDamage = damage;
        _maxChains = maxChains;
        _damageDecay = decay;
        _chainRadius = chainRadius;
        _lastPosition = GlobalPosition;
    }

    public override void _Ready()
    {
        // Start chaining immediately
        ChainToNext();
    }

    public override void _Process(double delta)
    {
        if (_currentChain >= _maxChains || _currentChain >= _targets.Count)
        {
            // All chains complete, fade out
            CleanupBolts();
            return;
        }

        _chainTimer += (float)delta;
        if (_chainTimer >= _chainDelay)
        {
            _chainTimer = 0;
            ChainToNext();
        }

        // Update bolt visuals
        foreach (var bolt in _bolts)
        {
            bolt.UpdateVisual();
        }
    }

    private void ChainToNext()
    {
        if (_currentChain >= _maxChains) return;

        // Find next valid target
        Node3D? nextTarget = null;

        foreach (var target in _targets)
        {
            if (!IsInstanceValid(target)) continue;
            if (_hitTargets.Contains(target)) continue;

            // For chains after the first, check distance from last hit
            if (_currentChain > 0)
            {
                float dist = target.GlobalPosition.DistanceTo(_lastPosition);
                if (dist > _chainRadius) continue;
            }

            nextTarget = target;
            break;
        }

        if (nextTarget == null)
        {
            GD.Print($"[ChainLightning] No more valid targets after {_currentChain} chains");
            _currentChain = _maxChains; // End chaining
            return;
        }

        // Calculate damage for this chain
        float damage = _baseDamage;
        for (int i = 0; i < _currentChain; i++)
        {
            damage *= _damageDecay;
        }

        // Deal damage
        if (nextTarget.HasMethod("TakeDamage"))
        {
            nextTarget.Call("TakeDamage", damage, _lastPosition, "Chain Lightning");
            GD.Print($"[ChainLightning] Chain {_currentChain + 1}: Hit {nextTarget.Name} for {damage:F0} damage");
        }

        // Create lightning bolt visual
        var bolt = new LightningBolt3D();
        bolt.Name = $"Bolt_{_currentChain}";
        bolt.Initialize(_lastPosition, nextTarget.GlobalPosition);
        AddChild(bolt);
        _bolts.Add(bolt);

        // Play sound
        Core.SoundManager3D.Instance?.PlayMagicSound(nextTarget.GlobalPosition);

        // Update state
        _hitTargets.Add(nextTarget);
        _lastPosition = nextTarget.GlobalPosition;
        _currentChain++;
    }

    private void CleanupBolts()
    {
        // Fade out bolts
        foreach (var bolt in _bolts)
        {
            if (IsInstanceValid(bolt))
            {
                bolt.FadeOut();
            }
        }

        // Queue free after fade
        var tween = CreateTween();
        tween.TweenInterval(0.5f);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}

/// <summary>
/// Visual lightning bolt between two points.
/// </summary>
public partial class LightningBolt3D : Node3D
{
    private Vector3 _start;
    private Vector3 _end;
    private MeshInstance3D? _mesh;
    private OmniLight3D? _light;
    private float _noiseTime;

    public void Initialize(Vector3 start, Vector3 end)
    {
        _start = start;
        _end = end;
        GlobalPosition = (_start + _end) / 2f;

        CreateBoltMesh();
        CreateLight();
    }

    private void CreateBoltMesh()
    {
        _mesh = new MeshInstance3D();

        // Create a simple cylinder for the bolt
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = 0.08f;
        cylinder.BottomRadius = 0.08f;
        cylinder.Height = _start.DistanceTo(_end);
        _mesh.Mesh = cylinder;

        // Electric blue material
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.5f, 0.7f, 1f);
        material.EmissionEnabled = true;
        material.Emission = new Color(0.3f, 0.5f, 1f);
        material.EmissionEnergyMultiplier = 4f;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _mesh.MaterialOverride = material;

        // Orient to face from start to end
        _mesh.LookAtFromPosition(GlobalPosition, _end, Vector3.Up);
        _mesh.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);

        AddChild(_mesh);
    }

    private void CreateLight()
    {
        _light = new OmniLight3D();
        _light.LightColor = new Color(0.5f, 0.7f, 1f);
        _light.LightEnergy = 2f;
        _light.OmniRange = 4f;
        AddChild(_light);
    }

    public void UpdateVisual()
    {
        // Add some jitter/noise to make it look electric
        _noiseTime += (float)GetProcessDeltaTime() * 20f;

        if (_mesh != null)
        {
            float jitter = Mathf.Sin(_noiseTime) * 0.02f;
            _mesh.Position = new Vector3(jitter, 0, Mathf.Cos(_noiseTime * 1.3f) * 0.02f);

            // Flicker intensity
            if (_mesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.EmissionEnergyMultiplier = 3f + Mathf.Sin(_noiseTime * 2f);
            }
        }
    }

    public void FadeOut()
    {
        if (_mesh?.MaterialOverride is StandardMaterial3D mat)
        {
            var tween = CreateTween();
            tween.TweenProperty(mat, "albedo_color:a", 0f, 0.3f);
        }

        if (_light != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_light, "light_energy", 0f, 0.3f);
        }
    }
}
