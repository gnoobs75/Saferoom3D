using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.Environment;

/// <summary>
/// Procedural decal system for adding visual variety to dungeon surfaces.
/// Spawns blood splats, moss patches, cracks, water stains, and scorch marks.
/// </summary>
public partial class DungeonDecals : Node3D
{
    public enum DecalType
    {
        BloodSplat,
        MossPatch,
        Crack,
        WaterStain,
        ScorchMark,
        DirtPatch,
        Cobweb
    }

    // Decal configurations
    private static readonly Dictionary<DecalType, DecalConfig> DecalConfigs = new()
    {
        [DecalType.BloodSplat] = new DecalConfig
        {
            Color = new Color(0.4f, 0.05f, 0.02f),
            MinSize = 0.3f,
            MaxSize = 1.2f,
            NormalFade = 0.3f,
            AlphaFade = 0.8f
        },
        [DecalType.MossPatch] = new DecalConfig
        {
            Color = new Color(0.15f, 0.25f, 0.1f),
            MinSize = 0.5f,
            MaxSize = 2.0f,
            NormalFade = 0.5f,
            AlphaFade = 0.7f
        },
        [DecalType.Crack] = new DecalConfig
        {
            Color = new Color(0.1f, 0.08f, 0.06f),
            MinSize = 0.4f,
            MaxSize = 1.5f,
            NormalFade = 0.2f,
            AlphaFade = 0.9f
        },
        [DecalType.WaterStain] = new DecalConfig
        {
            Color = new Color(0.15f, 0.12f, 0.1f),
            MinSize = 0.5f,
            MaxSize = 2.5f,
            NormalFade = 0.6f,
            AlphaFade = 0.5f
        },
        [DecalType.ScorchMark] = new DecalConfig
        {
            Color = new Color(0.05f, 0.03f, 0.02f),
            MinSize = 0.4f,
            MaxSize = 1.8f,
            NormalFade = 0.4f,
            AlphaFade = 0.85f
        },
        [DecalType.DirtPatch] = new DecalConfig
        {
            Color = new Color(0.25f, 0.2f, 0.15f),
            MinSize = 0.3f,
            MaxSize = 1.0f,
            NormalFade = 0.5f,
            AlphaFade = 0.6f
        },
        [DecalType.Cobweb] = new DecalConfig
        {
            Color = new Color(0.7f, 0.7f, 0.7f, 0.4f),
            MinSize = 0.5f,
            MaxSize = 1.5f,
            NormalFade = 0.1f,
            AlphaFade = 0.5f
        }
    };

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<Decal> _activeDecals = new();
    private ImageTexture? _decalTexture;

    // Performance limits
    private const int MaxDecals = 100;
    private const float DecalCullDistance = 40f;

    public override void _Ready()
    {
        // Pre-generate decal textures
        _decalTexture = GenerateDecalTexture();
    }

    /// <summary>
    /// Spawns a random decal at the specified position.
    /// </summary>
    public Decal? SpawnDecal(DecalType type, Vector3 position, Vector3 surfaceNormal)
    {
        if (_activeDecals.Count >= MaxDecals)
        {
            // Remove oldest decal
            var oldest = _activeDecals[0];
            _activeDecals.RemoveAt(0);
            oldest.QueueFree();
        }

        var config = DecalConfigs[type];
        var decal = new Decal();

        // Size variation
        float size = _rng.RandfRange(config.MinSize, config.MaxSize);
        decal.Size = new Vector3(size, 0.5f, size);

        // Position
        decal.Position = position + surfaceNormal * 0.01f; // Slight offset to prevent z-fighting

        // Rotation to face surface normal
        if (surfaceNormal != Vector3.Up)
        {
            decal.LookAt(position + surfaceNormal, Vector3.Up);
        }
        // Random rotation around normal
        decal.RotateY(_rng.RandfRange(0, Mathf.Tau));

        // Texture and appearance
        decal.TextureAlbedo = GenerateDecalTextureForType(type);
        decal.Modulate = config.Color;
        decal.NormalFade = config.NormalFade;
        decal.LowerFade = config.AlphaFade;
        decal.UpperFade = config.AlphaFade;
        decal.CullMask = 1; // Only affect static geometry

        // Distance fade for performance
        decal.DistanceFadeEnabled = true;
        decal.DistanceFadeBegin = DecalCullDistance * 0.7f;
        decal.DistanceFadeLength = DecalCullDistance * 0.3f;

        AddChild(decal);
        _activeDecals.Add(decal);

        return decal;
    }

    /// <summary>
    /// Spawns multiple random decals in an area.
    /// </summary>
    public void PopulateArea(Vector3 center, float radius, int count, DecalType[] allowedTypes)
    {
        for (int i = 0; i < count; i++)
        {
            // Random position within radius
            float angle = _rng.RandfRange(0, Mathf.Tau);
            float dist = _rng.RandfRange(0, radius);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
            Vector3 position = center + offset;

            // Random type from allowed
            var type = allowedTypes[_rng.RandiRange(0, allowedTypes.Length - 1)];

            // Default to floor normal (up)
            SpawnDecal(type, position, Vector3.Up);
        }
    }

    /// <summary>
    /// Spawns wall decals along a wall surface.
    /// </summary>
    public void PopulateWall(Vector3 start, Vector3 end, Vector3 wallNormal, int count, DecalType[] allowedTypes)
    {
        for (int i = 0; i < count; i++)
        {
            // Random position along wall
            float t = _rng.Randf();
            Vector3 position = start.Lerp(end, t);

            // Random height
            position.Y = _rng.RandfRange(0.5f, 3f);

            var type = allowedTypes[_rng.RandiRange(0, allowedTypes.Length - 1)];
            SpawnDecal(type, position, wallNormal);
        }
    }

    /// <summary>
    /// Generates a procedural decal texture based on type.
    /// </summary>
    private ImageTexture GenerateDecalTextureForType(DecalType type)
    {
        int size = 128;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        switch (type)
        {
            case DecalType.BloodSplat:
                GenerateBloodSplatTexture(image, size);
                break;
            case DecalType.MossPatch:
                GenerateMossPatchTexture(image, size);
                break;
            case DecalType.Crack:
                GenerateCrackTexture(image, size);
                break;
            case DecalType.WaterStain:
                GenerateStainTexture(image, size);
                break;
            case DecalType.ScorchMark:
                GenerateScorchTexture(image, size);
                break;
            case DecalType.DirtPatch:
                GenerateDirtTexture(image, size);
                break;
            case DecalType.Cobweb:
                GenerateCobwebTexture(image, size);
                break;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private void GenerateBloodSplatTexture(Image image, int size)
    {
        float centerX = size / 2f;
        float centerY = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / (size / 2f);

                // Organic splatter shape
                float angle = Mathf.Atan2(dy, dx);
                float noise = Mathf.Sin(angle * 7) * 0.2f + Mathf.Sin(angle * 13) * 0.1f;
                float threshold = 0.6f + noise;

                float alpha = dist < threshold ? Mathf.Clamp(1f - dist / threshold, 0, 1) : 0;
                alpha *= 0.8f + (float)GD.RandRange(-0.1, 0.1);

                image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(alpha, 0, 1)));
            }
        }
    }

    private void GenerateMossPatchTexture(Image image, int size)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                // Multiple noise scales for organic look
                float noise1 = Mathf.Sin(u * 15) * Mathf.Cos(v * 12);
                float noise2 = Mathf.Sin(u * 31 + v * 7) * 0.5f;
                float combined = (noise1 + noise2) * 0.5f + 0.5f;

                // Circular falloff
                float dx = u - 0.5f;
                float dy = v - 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) * 2;
                float falloff = 1f - Mathf.Clamp(dist, 0, 1);

                float alpha = combined * falloff;
                alpha = alpha > 0.3f ? alpha : 0;

                image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(alpha, 0, 1)));
            }
        }
    }

    private void GenerateCrackTexture(Image image, int size)
    {
        // Generate crack lines
        int numCracks = _rng.RandiRange(3, 7);
        float centerX = size / 2f;
        float centerY = size / 2f;

        // Clear to transparent
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                image.SetPixel(x, y, new Color(1, 1, 1, 0));

        for (int c = 0; c < numCracks; c++)
        {
            float angle = _rng.RandfRange(0, Mathf.Tau);
            float length = _rng.RandfRange(size * 0.3f, size * 0.5f);

            float px = centerX;
            float py = centerY;

            for (int i = 0; i < (int)length; i++)
            {
                // Draw thick crack line with falloff
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int ix = (int)px + dx;
                        int iy = (int)py + dy;
                        if (ix >= 0 && ix < size && iy >= 0 && iy < size)
                        {
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            float alpha = 1f - dist / 3f;
                            var existing = image.GetPixel(ix, iy);
                            image.SetPixel(ix, iy, new Color(1, 1, 1, Mathf.Max(existing.A, alpha)));
                        }
                    }
                }

                // Move along crack with slight randomization
                angle += _rng.RandfRange(-0.3f, 0.3f);
                px += Mathf.Cos(angle);
                py += Mathf.Sin(angle);
            }
        }
    }

    private void GenerateStainTexture(Image image, int size)
    {
        // Soft circular gradient with noise
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - size / 2f) / (size / 2f);
                float dy = (y - size / 2f) / (size / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float noise = (float)GD.RandRange(-0.1, 0.1);
                float alpha = 1f - Mathf.Clamp(dist + noise, 0, 1);
                alpha = Mathf.Pow(alpha, 1.5f); // Softer edges

                image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(alpha, 0, 1)));
            }
        }
    }

    private void GenerateScorchTexture(Image image, int size)
    {
        // Radial scorch with irregular edges
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - size / 2f) / (size / 2f);
                float dy = (y - size / 2f) / (size / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Irregular edge
                float edgeNoise = Mathf.Sin(angle * 5) * 0.15f + Mathf.Sin(angle * 11) * 0.1f;
                float threshold = 0.7f + edgeNoise;

                float alpha = dist < threshold ? 1f - Mathf.Pow(dist / threshold, 2) : 0;

                image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(alpha, 0, 1)));
            }
        }
    }

    private void GenerateDirtTexture(Image image, int size)
    {
        // Noisy patches
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = (float)GD.RandRange(0, 1);
                float dx = (x - size / 2f) / (size / 2f);
                float dy = (y - size / 2f) / (size / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = noise * (1f - dist);
                alpha = alpha > 0.4f ? alpha : 0;

                image.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(alpha, 0, 1)));
            }
        }
    }

    private void GenerateCobwebTexture(Image image, int size)
    {
        // Clear
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                image.SetPixel(x, y, new Color(1, 1, 1, 0));

        // Draw radial lines from corner
        int numLines = _rng.RandiRange(8, 15);
        float startX = 0;
        float startY = 0;

        for (int i = 0; i < numLines; i++)
        {
            float angle = Mathf.Pi * 0.5f * (i / (float)numLines);
            float length = size * _rng.RandfRange(0.7f, 1.0f);

            for (int j = 0; j < (int)length; j++)
            {
                int px = (int)(startX + Mathf.Cos(angle) * j);
                int py = (int)(startY + Mathf.Sin(angle) * j);

                if (px >= 0 && px < size && py >= 0 && py < size)
                {
                    float alpha = 0.5f + (float)GD.RandRange(-0.2, 0.2);
                    image.SetPixel(px, py, new Color(1, 1, 1, alpha));
                }
            }
        }

        // Add connecting arcs
        for (int ring = 0; ring < 5; ring++)
        {
            float radius = size * (ring + 1) / 6f;
            for (float a = 0; a < Mathf.Pi * 0.5f; a += 0.05f)
            {
                int px = (int)(Mathf.Cos(a) * radius);
                int py = (int)(Mathf.Sin(a) * radius);
                if (px >= 0 && px < size && py >= 0 && py < size)
                {
                    if (GD.Randf() > 0.3f)
                    {
                        image.SetPixel(px, py, new Color(1, 1, 1, 0.4f));
                    }
                }
            }
        }
    }

    private ImageTexture GenerateDecalTexture()
    {
        // Default texture
        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = new Vector2(x - 32, y - 32).Length() / 32f;
                float alpha = 1f - Mathf.Clamp(dist, 0, 1);
                image.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private class DecalConfig
    {
        public Color Color;
        public float MinSize;
        public float MaxSize;
        public float NormalFade;
        public float AlphaFade;
    }
}
