using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SafeRoom3D.Core;

/// <summary>
/// LOD (Level of Detail) interface for objects that support distance-based optimization.
/// Enemies, props, and other game objects can implement this interface to be managed by LODManager.
/// </summary>
public interface ILODObject
{
    /// <summary>
    /// Get the world position of this object for distance calculations
    /// </summary>
    Vector3 GetPosition();

    /// <summary>
    /// Set the current LOD level for this object
    /// </summary>
    /// <param name="level">0=Full detail, 1=Medium, 2=Low, 3=Minimal/Culled</param>
    void SetLODLevel(int level);

    /// <summary>
    /// Get the current LOD level
    /// </summary>
    int GetCurrentLOD();

    /// <summary>
    /// Check if this object is still valid (not freed/disposed)
    /// </summary>
    bool IsValid();
}

/// <summary>
/// LOD Manager singleton that handles Level of Detail optimization for the entire game.
/// Uses spatial partitioning for efficient distance checks and batched updates.
///
/// LOD Levels:
/// - LOD0 (Full detail): 0-15 meters - Full mesh detail, all effects, animations
/// - LOD1 (Medium detail): 15-30 meters - Reduced mesh detail, simplified effects
/// - LOD2 (Low detail): 30-50 meters - Minimal mesh, basic rendering
/// - LOD3 (Minimal/Culled): 50+ meters - Culled or extremely simplified
///
/// Performance optimizations:
/// - Spatial grid partitioning for O(1) neighbor queries
/// - Updates only every 0.5 seconds
/// - Only updates when player moves significantly
/// - Batched processing to avoid frame spikes
/// </summary>
public partial class LODManager : Node
{
    // Singleton pattern
    private static LODManager? _instance;
    public static LODManager? Instance => _instance;

    // Player tracking
    private Node3D? _player;
    private Vector3 _lastPlayerPosition;
    private float _updateTimer;

    // Registered LOD objects
    private readonly List<ILODObject> _lodObjects = new();

    // Spatial partitioning grid
    private readonly Dictionary<Vector2I, List<ILODObject>> _spatialGrid = new();

    // Performance tracking
    private int _objectsUpdatedLastFrame;
    private float _lastUpdateTime;

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            GD.PrintErr("LODManager: Multiple instances detected! Freeing duplicate.");
            QueueFree();
            return;
        }
        _instance = this;
    }

    public override void _ExitTree()
    {
        if (_instance == this)
            _instance = null;
    }

    public override void _Ready()
    {
        // Find player
        _player = GetTree().Root.GetNode<Node3D>("Main3D/FPSController");
        if (_player == null)
        {
            GD.PrintErr("LODManager: Could not find player FPSController!");
            return;
        }

        _lastPlayerPosition = _player.GlobalPosition;
        _updateTimer = 0f;

        GD.Print("LODManager: Initialized successfully");
    }

    public override void _Process(double delta)
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            return;
        }

        _updateTimer += (float)delta;

        // Only update LOD levels periodically
        if (_updateTimer >= Constants.LODUpdateInterval)
        {
            _updateTimer = 0f;

            Vector3 currentPlayerPos = _player.GlobalPosition;
            float playerMovement = _lastPlayerPosition.DistanceTo(currentPlayerPos);

            // Only update if player has moved significantly
            if (playerMovement >= Constants.LODUpdateThreshold)
            {
                _lastPlayerPosition = currentPlayerPos;
                UpdateAllLODLevels();
            }
        }
    }

    /// <summary>
    /// Register an object for LOD management
    /// </summary>
    public void RegisterObject(ILODObject obj)
    {
        if (obj == null || !obj.IsValid())
            return;

        if (!_lodObjects.Contains(obj))
        {
            _lodObjects.Add(obj);
            AddToSpatialGrid(obj);
        }
    }

    /// <summary>
    /// Unregister an object from LOD management
    /// </summary>
    public void UnregisterObject(ILODObject obj)
    {
        if (obj == null)
            return;

        _lodObjects.Remove(obj);
        RemoveFromSpatialGrid(obj);
    }

    /// <summary>
    /// Update LOD levels for all registered objects based on distance from player
    /// </summary>
    private void UpdateAllLODLevels()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        float startTime = Time.GetTicksMsec() / 1000f;
        _objectsUpdatedLastFrame = 0;

        Vector3 playerPos = _player.GlobalPosition;

        // Clean up invalid objects
        _lodObjects.RemoveAll(obj => !obj.IsValid());

        // Update LOD levels based on distance
        foreach (var obj in _lodObjects)
        {
            if (!obj.IsValid())
                continue;

            Vector3 objPos = obj.GetPosition();
            float distance = playerPos.DistanceTo(objPos);

            int newLOD = CalculateLODLevel(distance);
            int currentLOD = obj.GetCurrentLOD();

            // Only update if LOD level changed
            if (newLOD != currentLOD)
            {
                obj.SetLODLevel(newLOD);
                _objectsUpdatedLastFrame++;
            }
        }

        _lastUpdateTime = (Time.GetTicksMsec() / 1000f) - startTime;

        // Debug output every few updates
        if (_updateTimer < 0.1f)
        {
            GD.Print($"LODManager: Updated {_objectsUpdatedLastFrame}/{_lodObjects.Count} objects in {_lastUpdateTime * 1000f:F2}ms");
        }
    }

    /// <summary>
    /// Calculate appropriate LOD level based on distance from player
    /// </summary>
    private static int CalculateLODLevel(float distance)
    {
        if (distance < Constants.LOD0Distance)
            return 0; // Full detail
        else if (distance < Constants.LOD1Distance)
            return 1; // Medium detail
        else if (distance < Constants.LOD2Distance)
            return 2; // Low detail
        else if (distance < Constants.MaxRenderDistance)
            return 3; // Minimal detail
        else
            return 4; // Culled (invisible)
    }

    /// <summary>
    /// Add object to spatial partitioning grid
    /// </summary>
    private void AddToSpatialGrid(ILODObject obj)
    {
        if (!obj.IsValid())
            return;

        Vector2I gridCell = GetGridCell(obj.GetPosition());

        if (!_spatialGrid.ContainsKey(gridCell))
        {
            _spatialGrid[gridCell] = new List<ILODObject>();
        }

        if (!_spatialGrid[gridCell].Contains(obj))
        {
            _spatialGrid[gridCell].Add(obj);
        }
    }

    /// <summary>
    /// Remove object from spatial partitioning grid
    /// </summary>
    private void RemoveFromSpatialGrid(ILODObject obj)
    {
        if (!obj.IsValid())
            return;

        Vector2I gridCell = GetGridCell(obj.GetPosition());

        if (_spatialGrid.ContainsKey(gridCell))
        {
            _spatialGrid[gridCell].Remove(obj);

            // Clean up empty cells
            if (_spatialGrid[gridCell].Count == 0)
            {
                _spatialGrid.Remove(gridCell);
            }
        }
    }

    /// <summary>
    /// Convert world position to grid cell coordinates
    /// </summary>
    private static Vector2I GetGridCell(Vector3 worldPos)
    {
        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / Constants.LODGridCellSize),
            Mathf.FloorToInt(worldPos.Z / Constants.LODGridCellSize)
        );
    }

    /// <summary>
    /// Get all objects in a grid cell and neighboring cells
    /// </summary>
    public List<ILODObject> GetObjectsNear(Vector3 position, int cellRadius = 1)
    {
        var result = new List<ILODObject>();
        Vector2I centerCell = GetGridCell(position);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                Vector2I cell = new Vector2I(centerCell.X + x, centerCell.Y + z);
                if (_spatialGrid.ContainsKey(cell))
                {
                    result.AddRange(_spatialGrid[cell]);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get performance statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new Dictionary<string, object>
        {
            { "total_objects", _lodObjects.Count },
            { "objects_updated_last_frame", _objectsUpdatedLastFrame },
            { "last_update_time_ms", _lastUpdateTime * 1000f },
            { "grid_cells", _spatialGrid.Count }
        };
    }

    /// <summary>
    /// Force immediate LOD update for all objects
    /// </summary>
    public void ForceUpdate()
    {
        _updateTimer = Constants.LODUpdateInterval; // Trigger update on next frame
        _lastPlayerPosition = _player?.GlobalPosition ?? Vector3.Zero;
    }
}

/// <summary>
/// Helper class for mesh simplification and LOD mesh generation
/// </summary>
public static class LODMeshHelper
{
    /// <summary>
    /// Create a simplified version of a mesh by reducing polygon count
    /// </summary>
    /// <param name="original">Original mesh to simplify</param>
    /// <param name="reduction">Reduction factor (0-1). 0.5 = 50% fewer polygons</param>
    /// <returns>Simplified mesh</returns>
    public static ArrayMesh? CreateLODMesh(Mesh original, float reduction)
    {
        if (original == null || reduction <= 0f || reduction >= 1f)
            return null;

        // For procedural meshes, we'll reduce segment count
        // This is a simplified approach - production code would use proper mesh decimation

        // Get original mesh data
        if (original is not ArrayMesh arrayMesh)
            return null;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        // For each surface in the mesh
        for (int i = 0; i < arrayMesh.GetSurfaceCount(); i++)
        {
            var arrays = arrayMesh.SurfaceGetArrays(i);
            var vertices = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
            var normals = (Vector3[])arrays[(int)Mesh.ArrayType.Normal];
            var indices = (int[])arrays[(int)Mesh.ArrayType.Index];

            if (vertices == null || indices == null)
                continue;

            // Simple decimation: skip every Nth triangle based on reduction factor
            int skipFactor = Mathf.Max(1, Mathf.RoundToInt(1f / (1f - reduction)));

            for (int j = 0; j < indices.Length; j += 3 * skipFactor)
            {
                // Add triangle vertices
                for (int k = 0; k < 3; k++)
                {
                    if (j + k < indices.Length)
                    {
                        int idx = indices[j + k];
                        if (idx < vertices.Length)
                        {
                            surfaceTool.AddVertex(vertices[idx]);
                        }
                    }
                }
            }
        }

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    /// <summary>
    /// Create a simplified material for distant objects
    /// </summary>
    public static StandardMaterial3D CreateSimplifiedMaterial(Material original)
    {
        var simplified = new StandardMaterial3D();

        if (original is StandardMaterial3D stdMat)
        {
            // Copy basic properties
            simplified.AlbedoColor = stdMat.AlbedoColor;
            simplified.Metallic = stdMat.Metallic;
            simplified.Roughness = stdMat.Roughness;

            // Disable expensive features
            simplified.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; // Unshaded for performance
            simplified.DisableReceiveShadows = true;
            simplified.CullMode = BaseMaterial3D.CullModeEnum.Back;
        }
        else
        {
            // Fallback basic material
            simplified.AlbedoColor = new Color(0.5f, 0.5f, 0.5f);
            simplified.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        }

        return simplified;
    }

    /// <summary>
    /// Create a simplified cylinder mesh with fewer segments
    /// </summary>
    public static ArrayMesh CreateSimplifiedCylinder(float height, float radius, int segments)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        // Top cap
        Vector3 topCenter = new Vector3(0, height / 2f, 0);
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = ((i + 1) % segments) * Mathf.Tau / segments;

            Vector3 p1 = topCenter + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
            Vector3 p2 = topCenter + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

            surfaceTool.AddVertex(topCenter);
            surfaceTool.AddVertex(p2);
            surfaceTool.AddVertex(p1);
        }

        // Bottom cap
        Vector3 bottomCenter = new Vector3(0, -height / 2f, 0);
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = ((i + 1) % segments) * Mathf.Tau / segments;

            Vector3 p1 = bottomCenter + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
            Vector3 p2 = bottomCenter + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

            surfaceTool.AddVertex(bottomCenter);
            surfaceTool.AddVertex(p1);
            surfaceTool.AddVertex(p2);
        }

        // Side walls
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.Tau / segments;
            float angle2 = ((i + 1) % segments) * Mathf.Tau / segments;

            Vector3 top1 = topCenter + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
            Vector3 top2 = topCenter + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);
            Vector3 bottom1 = bottomCenter + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
            Vector3 bottom2 = bottomCenter + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

            // First triangle
            surfaceTool.AddVertex(bottom1);
            surfaceTool.AddVertex(top1);
            surfaceTool.AddVertex(top2);

            // Second triangle
            surfaceTool.AddVertex(bottom1);
            surfaceTool.AddVertex(top2);
            surfaceTool.AddVertex(bottom2);
        }

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    /// <summary>
    /// Create a simplified box mesh
    /// </summary>
    public static ArrayMesh CreateSimplifiedBox(Vector3 size)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        Vector3 halfSize = size / 2f;

        // Define box vertices
        Vector3[] vertices = new Vector3[]
        {
            // Front face
            new Vector3(-halfSize.X, -halfSize.Y, halfSize.Z),
            new Vector3(halfSize.X, -halfSize.Y, halfSize.Z),
            new Vector3(halfSize.X, halfSize.Y, halfSize.Z),
            new Vector3(-halfSize.X, halfSize.Y, halfSize.Z),
            // Back face
            new Vector3(halfSize.X, -halfSize.Y, -halfSize.Z),
            new Vector3(-halfSize.X, -halfSize.Y, -halfSize.Z),
            new Vector3(-halfSize.X, halfSize.Y, -halfSize.Z),
            new Vector3(halfSize.X, halfSize.Y, -halfSize.Z),
        };

        // Define faces (each face is 2 triangles = 6 vertices)
        int[][] faces = new int[][]
        {
            new int[] { 0, 1, 2, 0, 2, 3 }, // Front
            new int[] { 4, 5, 6, 4, 6, 7 }, // Back
            new int[] { 5, 0, 3, 5, 3, 6 }, // Left
            new int[] { 1, 4, 7, 1, 7, 2 }, // Right
            new int[] { 3, 2, 7, 3, 7, 6 }, // Top
            new int[] { 5, 4, 1, 5, 1, 0 }, // Bottom
        };

        Vector3[] normals = new Vector3[]
        {
            Vector3.Forward,  // Front
            Vector3.Back,     // Back
            Vector3.Left,     // Left
            Vector3.Right,    // Right
            Vector3.Up,       // Top
            Vector3.Down,     // Bottom
        };

        for (int f = 0; f < faces.Length; f++)
        {
            for (int i = 0; i < faces[f].Length; i++)
            {
                surfaceTool.AddVertex(vertices[faces[f][i]]);
            }
        }

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }
}
