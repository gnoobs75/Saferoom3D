using Godot;
using System;
using SafeRoom3D.Core;
using SafeRoom3D.Player;

namespace SafeRoom3D.Environment;

/// <summary>
/// Water effect system for dungeon ambiance.
/// Creates flowing water streams and dripping water effects.
/// </summary>
public partial class WaterStream : Node3D
{
    private MeshInstance3D? _meshInstance;
    private GpuParticles3D? _edgeDrips;
    private AudioStreamPlayer3D? _audioPlayer;
    private StandardMaterial3D? _waterMaterial;
    private float _uvOffset = 0f;

    // Performance optimization - distance culling
    private const float ParticleActiveDistance = 30f;
    private const float AudioActiveDistance = 25f;
    private float _distanceCheckTimer;
    private const float DistanceCheckInterval = 0.5f;

    public Vector3 StartPoint { get; set; }
    public Vector3 EndPoint { get; set; }
    public float Width { get; set; } = 1f;

    public override void _Ready()
    {
        GenerateStreamMesh();
        CreateEdgeDrips();
        CreateAudio();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Animate UV scrolling for flow effect
        if (_waterMaterial != null)
        {
            _uvOffset += dt * 0.5f; // Scroll speed
            if (_uvOffset > 1f) _uvOffset -= 1f;
            _waterMaterial.Uv1Offset = new Vector3(0, _uvOffset, 0);
        }

        // Distance-based culling for particles and audio
        _distanceCheckTimer -= dt;
        if (_distanceCheckTimer <= 0)
        {
            _distanceCheckTimer = DistanceCheckInterval;
            UpdateDistanceCulling();
        }
    }

    private void UpdateDistanceCulling()
    {
        var player = FPSController.Instance;
        if (player == null) return;

        float distance = GlobalPosition.DistanceTo(player.GlobalPosition);

        // Toggle particles based on distance
        if (_edgeDrips != null)
        {
            _edgeDrips.Emitting = distance < ParticleActiveDistance;
        }

        // Toggle audio based on distance
        if (_audioPlayer != null)
        {
            if (distance < AudioActiveDistance && !_audioPlayer.Playing)
            {
                _audioPlayer.Play();
            }
            else if (distance >= AudioActiveDistance && _audioPlayer.Playing)
            {
                _audioPlayer.Stop();
            }
        }
    }

    private void GenerateStreamMesh()
    {
        _meshInstance = new MeshInstance3D();
        AddChild(_meshInstance);

        // Calculate stream direction and length
        Vector3 direction = EndPoint - StartPoint;
        float length = direction.Length();
        Vector3 normalizedDir = direction.Normalized();

        // Create a slightly curved plane mesh for the water stream
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        int segments = Math.Max(4, (int)(length * 4)); // More segments for longer streams
        float halfWidth = Width / 2f;

        // Generate vertices along the stream path
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 basePos = StartPoint.Lerp(EndPoint, t);

            // Add slight curve (sagging in the middle)
            float curve = Mathf.Sin(t * Mathf.Pi) * 0.05f * length;
            basePos.Y -= curve;

            // Calculate perpendicular direction for width
            Vector3 perpendicular = new Vector3(-normalizedDir.Z, 0, normalizedDir.X).Normalized();

            // Left and right vertices
            Vector3 leftPos = basePos + perpendicular * halfWidth;
            Vector3 rightPos = basePos - perpendicular * halfWidth;

            // Add vertices
            surfaceTool.SetUV(new Vector2(0, t * length));
            surfaceTool.AddVertex(leftPos - StartPoint);

            surfaceTool.SetUV(new Vector2(1, t * length));
            surfaceTool.AddVertex(rightPos - StartPoint);
        }

        // Generate triangles
        for (int i = 0; i < segments; i++)
        {
            int baseIdx = i * 2;
            // First triangle
            surfaceTool.AddIndex(baseIdx);
            surfaceTool.AddIndex(baseIdx + 2);
            surfaceTool.AddIndex(baseIdx + 1);
            // Second triangle
            surfaceTool.AddIndex(baseIdx + 1);
            surfaceTool.AddIndex(baseIdx + 2);
            surfaceTool.AddIndex(baseIdx + 3);
        }

        surfaceTool.GenerateNormals();
        surfaceTool.GenerateTangents();
        var mesh = surfaceTool.Commit();
        _meshInstance.Mesh = mesh;

        // Create water material
        _waterMaterial = new StandardMaterial3D();
        _waterMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _waterMaterial.AlbedoColor = new Color(0.2f, 0.4f, 0.6f, 0.7f); // Transparent blue-ish
        _waterMaterial.Roughness = 0.1f; // Very smooth/reflective
        _waterMaterial.Metallic = 0.3f;
        _waterMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Render both sides
        _waterMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; // Self-illuminated look

        // Add refraction for glass-like effect
        _waterMaterial.RefractionEnabled = true;
        _waterMaterial.RefractionScale = 0.05f;

        _meshInstance.MaterialOverride = _waterMaterial;

        // Position the mesh
        Position = StartPoint;
    }

    private void CreateEdgeDrips()
    {
        // Optional particle drips along stream edges
        _edgeDrips = new GpuParticles3D();
        AddChild(_edgeDrips);

        _edgeDrips.Amount = 15; // Reduced from 20 for performance
        _edgeDrips.Lifetime = 0.8f;
        _edgeDrips.Emitting = false; // Start disabled, enable when player is nearby
        _edgeDrips.OneShot = false;

        // Create particle material
        var particleMaterial = new ParticleProcessMaterial();
        particleMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        particleMaterial.EmissionBoxExtents = new Vector3(Width / 2f, 0.05f, (EndPoint - StartPoint).Length() / 2f);
        particleMaterial.Direction = Vector3.Down;
        particleMaterial.Spread = 5f;
        particleMaterial.InitialVelocityMin = 0.5f;
        particleMaterial.InitialVelocityMax = 1f;
        particleMaterial.Gravity = new Vector3(0, -3f, 0);
        particleMaterial.ScaleMin = 0.03f;
        particleMaterial.ScaleMax = 0.05f;
        particleMaterial.Color = new Color(0.3f, 0.5f, 0.7f, 0.6f);

        _edgeDrips.ProcessMaterial = particleMaterial;

        // Simple quad mesh for particles
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.05f, 0.05f);
        _edgeDrips.DrawPass1 = quadMesh;

        // Position at stream center
        _edgeDrips.Position = (EndPoint - StartPoint) / 2f;
    }

    private void CreateAudio()
    {
        // Use existing water/ambient sound if available
        _audioPlayer = new AudioStreamPlayer3D();
        AddChild(_audioPlayer);

        _audioPlayer.Position = (EndPoint - StartPoint) / 2f;
        _audioPlayer.MaxDistance = 15f;
        _audioPlayer.UnitSize = 5f;
        _audioPlayer.Autoplay = true;

        // Note: Actual audio stream would need to be loaded from assets
        // For now, we'll just set up the player structure
        // In practice: _audioPlayer.Stream = GD.Load<AudioStream>("res://Assets/Audio/water_flow.ogg");
    }
}

/// <summary>
/// Dripping water from ceiling with puddle formation.
/// </summary>
public partial class WaterDrip : Node3D
{
    private GpuParticles3D? _droplets;
    private MeshInstance3D? _puddle;
    private AudioStreamPlayer3D? _audioPlayer;
    private StandardMaterial3D? _puddleMaterial;

    // Performance optimization - distance culling
    private const float ActiveDistance = 25f;
    private float _distanceCheckTimer;
    private const float DistanceCheckInterval = 0.5f;
    private bool _isActive = true;

    public Vector3 CeilingPosition { get; set; }
    public float Intensity { get; set; } = 1f;

    private float _puddleSize = 0f;
    private float _targetPuddleSize = 0f;
    private float _dripTimer = 0f;
    private float _dripInterval = 0f;

    public override void _Ready()
    {
        CreateDropletParticles();
        CreatePuddle();
        CreateAudio();

        // Randomize drip timing
        var rng = new RandomNumberGenerator();
        _dripInterval = rng.RandfRange(0.5f, 2f) / Intensity;
        _dripTimer = rng.Randf() * _dripInterval;
        _targetPuddleSize = 0.2f + rng.Randf() * 0.3f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Distance culling check
        _distanceCheckTimer -= dt;
        if (_distanceCheckTimer <= 0)
        {
            _distanceCheckTimer = DistanceCheckInterval;
            var player = FPSController.Instance;
            if (player != null)
            {
                _isActive = GlobalPosition.DistanceTo(player.GlobalPosition) < ActiveDistance;
            }
        }

        // Skip processing if too far from player
        if (!_isActive) return;

        // Drip timing
        _dripTimer += dt;
        if (_dripTimer >= _dripInterval)
        {
            _dripTimer = 0f;
            EmitDrip();

            // Grow puddle gradually
            _puddleSize = Mathf.Min(_puddleSize + 0.05f, _targetPuddleSize);
        }

        // Shrink puddle slowly when no drips
        if (_dripTimer > _dripInterval * 0.8f)
        {
            _puddleSize = Mathf.Max(_puddleSize - dt * 0.02f, 0f);
        }

        // Update puddle size
        if (_puddle != null)
        {
            _puddle.Scale = new Vector3(_puddleSize, 1f, _puddleSize);
        }
    }

    private void CreateDropletParticles()
    {
        _droplets = new GpuParticles3D();
        AddChild(_droplets);

        _droplets.Position = CeilingPosition;
        _droplets.Amount = 5;
        _droplets.Lifetime = 1.5f;
        _droplets.Emitting = false; // We'll trigger manually
        _droplets.OneShot = true;
        _droplets.Explosiveness = 0.8f;

        var particleMaterial = new ParticleProcessMaterial();
        particleMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point;
        particleMaterial.Direction = Vector3.Down;
        particleMaterial.Spread = 2f;
        particleMaterial.InitialVelocityMin = 2f;
        particleMaterial.InitialVelocityMax = 3f;
        particleMaterial.Gravity = new Vector3(0, -9.8f, 0);
        particleMaterial.ScaleMin = 0.04f;
        particleMaterial.ScaleMax = 0.06f;
        particleMaterial.Color = new Color(0.4f, 0.6f, 0.8f, 0.8f);

        // Fade out near ground
        var gradient = new Gradient();
        gradient.AddPoint(0f, Colors.White);
        gradient.AddPoint(0.8f, Colors.White);
        gradient.AddPoint(1f, new Color(1f, 1f, 1f, 0f));
        var gradientTexture = new GradientTexture1D();
        gradientTexture.Gradient = gradient;
        particleMaterial.ColorRamp = gradientTexture;

        _droplets.ProcessMaterial = particleMaterial;

        // Droplet mesh
        var sphereMesh = new SphereMesh();
        sphereMesh.RadialSegments = 6;
        sphereMesh.Rings = 4;
        sphereMesh.Radius = 0.03f;
        sphereMesh.Height = 0.06f;
        _droplets.DrawPass1 = sphereMesh;
    }

    private void CreatePuddle()
    {
        _puddle = new MeshInstance3D();
        AddChild(_puddle);

        // Flat disc for puddle
        var cylinderMesh = new CylinderMesh();
        cylinderMesh.TopRadius = 0.5f;
        cylinderMesh.BottomRadius = 0.5f;
        cylinderMesh.Height = 0.01f;
        cylinderMesh.RadialSegments = 12;

        _puddle.Mesh = cylinderMesh;

        // Puddle material - translucent blue
        _puddleMaterial = new StandardMaterial3D();
        _puddleMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _puddleMaterial.AlbedoColor = new Color(0.3f, 0.5f, 0.7f, 0.5f);
        _puddleMaterial.Roughness = 0.1f;
        _puddleMaterial.Metallic = 0.2f;
        _puddleMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _puddleMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

        _puddle.MaterialOverride = _puddleMaterial;

        // Position on ground (assuming floor at y=0)
        _puddle.Position = new Vector3(CeilingPosition.X, 0.01f, CeilingPosition.Z);
        _puddle.Scale = Vector3.Zero; // Start invisible
    }

    private void CreateAudio()
    {
        _audioPlayer = new AudioStreamPlayer3D();
        AddChild(_audioPlayer);

        _audioPlayer.Position = CeilingPosition;
        _audioPlayer.MaxDistance = 10f;
        _audioPlayer.UnitSize = 3f;

        // Note: Would load drip sound from assets
        // _audioPlayer.Stream = GD.Load<AudioStream>("res://Assets/Audio/water_drip.ogg");
    }

    private void EmitDrip()
    {
        if (_droplets != null)
        {
            _droplets.Restart();
        }

        // Play drip sound (if we had audio loaded)
        if (_audioPlayer != null && _audioPlayer.Stream != null)
        {
            _audioPlayer.Play();
        }
    }
}

/// <summary>
/// Factory class for creating water effects.
/// </summary>
public static class WaterEffects3D
{
    /// <summary>
    /// Create a flowing water stream between two points.
    /// </summary>
    /// <param name="start">Starting position of the stream</param>
    /// <param name="end">Ending position of the stream</param>
    /// <param name="width">Width of the stream in meters</param>
    /// <returns>WaterStream node ready to add to scene tree</returns>
    public static WaterStream CreateStream(Vector3 start, Vector3 end, float width = 1f)
    {
        var stream = new WaterStream
        {
            StartPoint = start,
            EndPoint = end,
            Width = width
        };
        return stream;
    }

    /// <summary>
    /// Create a dripping water effect from ceiling.
    /// </summary>
    /// <param name="ceilingPos">Position on ceiling where water drips from</param>
    /// <param name="intensity">Drip frequency multiplier (higher = more frequent)</param>
    /// <returns>WaterDrip node ready to add to scene tree</returns>
    public static WaterDrip CreateDrip(Vector3 ceilingPos, float intensity = 1f)
    {
        var drip = new WaterDrip
        {
            CeilingPosition = ceilingPos,
            Intensity = intensity
        };
        return drip;
    }
}
