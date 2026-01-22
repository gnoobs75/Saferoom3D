using Godot;
using System.Collections.Generic;

namespace SafeRoom3D.UI;

/// <summary>
/// Generates procedural ability icons using Godot's drawing primitives.
/// Each icon is a small texture that represents the ability visually.
/// </summary>
public static class AbilityIcons
{
    private static readonly Dictionary<string, ImageTexture> _iconCache = new();
    public const int IconSize = 48;

    /// <summary>
    /// Get or create an icon for an ability.
    /// </summary>
    public static ImageTexture GetIcon(string abilityId)
    {
        if (_iconCache.TryGetValue(abilityId, out var cached))
            return cached;

        var icon = CreateIcon(abilityId);
        _iconCache[abilityId] = icon;
        return icon;
    }

    private static ImageTexture CreateIcon(string abilityId)
    {
        var image = Image.CreateEmpty(IconSize, IconSize, false, Image.Format.Rgba8);

        // Get colors for this ability
        var (primary, secondary, symbol) = GetAbilityColors(abilityId);

        // Fill background with gradient
        for (int y = 0; y < IconSize; y++)
        {
            for (int x = 0; x < IconSize; x++)
            {
                float t = (float)y / IconSize;
                var bgColor = primary.Lerp(secondary, t * 0.5f);
                // Darken edges
                float edgeDist = Mathf.Min(Mathf.Min(x, y), Mathf.Min(IconSize - x - 1, IconSize - y - 1));
                float edgeFade = Mathf.Clamp(edgeDist / 6f, 0, 1);
                bgColor = bgColor.Darkened(1f - edgeFade * 0.3f);
                image.SetPixel(x, y, bgColor);
            }
        }

        // Draw symbol based on ability type
        DrawAbilitySymbol(image, abilityId, symbol);

        // Add border
        DrawBorder(image, new Color(0.3f, 0.3f, 0.35f));

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    private static (Color primary, Color secondary, Color symbol) GetAbilityColors(string abilityId)
    {
        return abilityId switch
        {
            "fireball" => (new Color(0.8f, 0.3f, 0.1f), new Color(0.9f, 0.5f, 0.2f), new Color(1f, 0.9f, 0.3f)),
            "chain_lightning" => (new Color(0.2f, 0.4f, 0.8f), new Color(0.4f, 0.6f, 1f), new Color(0.8f, 0.9f, 1f)),
            "soul_leech" => (new Color(0.2f, 0.5f, 0.2f), new Color(0.3f, 0.6f, 0.3f), new Color(0.6f, 1f, 0.6f)),
            "protective_shell" => (new Color(0.3f, 0.5f, 0.7f), new Color(0.5f, 0.7f, 0.9f), new Color(0.9f, 0.95f, 1f)),
            "gravity_well" => (new Color(0.3f, 0.1f, 0.4f), new Color(0.5f, 0.2f, 0.6f), new Color(0.8f, 0.5f, 1f)),
            "timestop_bubble" => (new Color(0.2f, 0.6f, 0.7f), new Color(0.4f, 0.8f, 0.9f), new Color(0.9f, 1f, 1f)),
            "infernal_ground" => (new Color(0.7f, 0.2f, 0.05f), new Color(0.9f, 0.4f, 0.1f), new Color(1f, 0.7f, 0.2f)),
            "banshees_wail" => (new Color(0.4f, 0.3f, 0.5f), new Color(0.6f, 0.4f, 0.7f), new Color(0.9f, 0.8f, 1f)),
            "berserk" => (new Color(0.7f, 0.1f, 0.1f), new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.5f, 0.5f)),
            "engine_of_tomorrow" => (new Color(0.4f, 0.3f, 0.6f), new Color(0.5f, 0.4f, 0.8f), new Color(0.7f, 0.6f, 1f)),
            "dead_mans_rally" => (new Color(0.5f, 0.1f, 0.1f), new Color(0.3f, 0.05f, 0.05f), new Color(1f, 0.3f, 0.3f)),
            "mirror_image" => (new Color(0.5f, 0.5f, 0.6f), new Color(0.7f, 0.7f, 0.8f), new Color(0.9f, 0.9f, 1f)),
            "audience_favorite" => (new Color(0.6f, 0.5f, 0.2f), new Color(0.8f, 0.7f, 0.3f), new Color(1f, 0.9f, 0.5f)),
            "sponsor_blessing" => (new Color(0.2f, 0.6f, 0.3f), new Color(0.3f, 0.8f, 0.4f), new Color(0.7f, 1f, 0.7f)),
            _ => (new Color(0.3f, 0.3f, 0.35f), new Color(0.4f, 0.4f, 0.45f), new Color(0.8f, 0.8f, 0.8f))
        };
    }

    private static void DrawAbilitySymbol(Image image, string abilityId, Color symbolColor)
    {
        int cx = IconSize / 2;
        int cy = IconSize / 2;

        switch (abilityId)
        {
            case "heal":
                DrawHealHeart(image, cx, cy, symbolColor);
                break;
            case "fireball":
                DrawFlame(image, cx, cy, symbolColor);
                break;
            case "chain_lightning":
                DrawLightningBolt(image, cx, cy, symbolColor);
                break;
            case "soul_leech":
                DrawSpiral(image, cx, cy, symbolColor);
                break;
            case "protective_shell":
                DrawShield(image, cx, cy, symbolColor);
                break;
            case "gravity_well":
                DrawVortex(image, cx, cy, symbolColor);
                break;
            case "timestop_bubble":
                DrawClock(image, cx, cy, symbolColor);
                break;
            case "infernal_ground":
                DrawFlames(image, cx, cy + 6, symbolColor);
                break;
            case "banshees_wail":
                DrawSoundWaves(image, cx, cy, symbolColor);
                break;
            case "berserk":
                DrawCrossedSwords(image, cx, cy, symbolColor);
                break;
            case "engine_of_tomorrow":
                DrawGear(image, cx, cy, symbolColor);
                break;
            case "dead_mans_rally":
                DrawSkull(image, cx, cy, symbolColor);
                break;
            case "mirror_image":
                DrawMirror(image, cx, cy, symbolColor);
                break;
            case "audience_favorite":
                DrawStar(image, cx, cy, symbolColor);
                break;
            case "sponsor_blessing":
                DrawCross(image, cx, cy, symbolColor);
                break;
            default:
                DrawQuestion(image, cx, cy, symbolColor);
                break;
        }
    }

    private static void DrawFlame(Image image, int cx, int cy, Color color)
    {
        // Simple flame shape
        for (int y = -10; y <= 10; y++)
        {
            int width = (int)(6 * (1f - Mathf.Abs(y) / 12f));
            if (y < 0) width = (int)(width * 0.7f);
            for (int x = -width; x <= width; x++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                {
                    float intensity = 1f - Mathf.Abs(x) / (float)(width + 1);
                    image.SetPixel(px, py, color * intensity);
                }
            }
        }
    }

    private static void DrawLightningBolt(Image image, int cx, int cy, Color color)
    {
        // Zigzag lightning
        int[] xOffsets = { 0, 4, -2, 3, -1, 2 };
        int yStart = cy - 12;
        for (int i = 0; i < xOffsets.Length - 1; i++)
        {
            int y1 = yStart + i * 4;
            int y2 = yStart + (i + 1) * 4;
            int x1 = cx + xOffsets[i];
            int x2 = cx + xOffsets[i + 1];
            DrawLine(image, x1, y1, x2, y2, color, 2);
        }
    }

    private static void DrawSpiral(Image image, int cx, int cy, Color color)
    {
        for (float t = 0; t < 4f * Mathf.Pi; t += 0.2f)
        {
            float r = 2f + t * 1.5f;
            int x = cx + (int)(Mathf.Cos(t) * r);
            int y = cy + (int)(Mathf.Sin(t) * r);
            if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
            {
                image.SetPixel(x, y, color);
                if (x + 1 < IconSize) image.SetPixel(x + 1, y, color);
            }
        }
    }

    private static void DrawShield(Image image, int cx, int cy, Color color)
    {
        for (int y = -10; y <= 10; y++)
        {
            int width = y < 0 ? 8 : 8 - (int)(y * 0.6f);
            for (int x = -width; x <= width; x++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                {
                    // Edge only
                    if (Mathf.Abs(x) >= width - 1 || y == -10 || (y == 10 && Mathf.Abs(x) < 2))
                    {
                        image.SetPixel(px, py, color);
                    }
                }
            }
        }
    }

    private static void DrawVortex(Image image, int cx, int cy, Color color)
    {
        for (int r = 3; r <= 12; r += 3)
        {
            for (float a = 0; a < Mathf.Tau; a += 0.15f)
            {
                int x = cx + (int)(Mathf.Cos(a + r * 0.3f) * r);
                int y = cy + (int)(Mathf.Sin(a + r * 0.3f) * r);
                if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
                    image.SetPixel(x, y, color);
            }
        }
    }

    private static void DrawClock(Image image, int cx, int cy, Color color)
    {
        // Circle
        for (float a = 0; a < Mathf.Tau; a += 0.1f)
        {
            int x = cx + (int)(Mathf.Cos(a) * 10);
            int y = cy + (int)(Mathf.Sin(a) * 10);
            if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
                image.SetPixel(x, y, color);
        }
        // Hands
        DrawLine(image, cx, cy, cx, cy - 7, color, 1);
        DrawLine(image, cx, cy, cx + 5, cy, color, 1);
    }

    private static void DrawFlames(Image image, int cx, int cy, Color color)
    {
        // Multiple small flames at bottom
        int[] offsets = { -8, -4, 0, 4, 8 };
        int[] heights = { 6, 10, 8, 10, 6 };
        for (int i = 0; i < offsets.Length; i++)
        {
            for (int y = 0; y < heights[i]; y++)
            {
                int w = (int)(2 * (1f - y / (float)heights[i]));
                for (int x = -w; x <= w; x++)
                {
                    int px = cx + offsets[i] + x;
                    int py = cy - y;
                    if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                        image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static void DrawSoundWaves(Image image, int cx, int cy, Color color)
    {
        for (int r = 4; r <= 12; r += 4)
        {
            for (float a = -0.5f; a <= 0.5f; a += 0.1f)
            {
                int x = cx + (int)(Mathf.Cos(a) * r);
                int y = cy + (int)(Mathf.Sin(a) * r);
                if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
                    image.SetPixel(x, y, color);
            }
        }
        // Speaker
        DrawLine(image, cx - 2, cy - 4, cx - 2, cy + 4, color, 1);
        DrawLine(image, cx - 6, cy - 2, cx - 2, cy - 4, color, 1);
        DrawLine(image, cx - 6, cy + 2, cx - 2, cy + 4, color, 1);
    }

    private static void DrawCrossedSwords(Image image, int cx, int cy, Color color)
    {
        DrawLine(image, cx - 10, cy - 10, cx + 10, cy + 10, color, 2);
        DrawLine(image, cx + 10, cy - 10, cx - 10, cy + 10, color, 2);
        // Hilts
        DrawLine(image, cx - 6, cy - 4, cx - 4, cy - 6, color, 1);
        DrawLine(image, cx + 6, cy - 4, cx + 4, cy - 6, color, 1);
    }

    private static void DrawGear(Image image, int cx, int cy, Color color)
    {
        // Outer ring with teeth
        for (float a = 0; a < Mathf.Tau; a += 0.05f)
        {
            float r = 9 + Mathf.Sin(a * 8) * 2;
            int x = cx + (int)(Mathf.Cos(a) * r);
            int y = cy + (int)(Mathf.Sin(a) * r);
            if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
                image.SetPixel(x, y, color);
        }
        // Inner circle
        for (float a = 0; a < Mathf.Tau; a += 0.1f)
        {
            int x = cx + (int)(Mathf.Cos(a) * 4);
            int y = cy + (int)(Mathf.Sin(a) * 4);
            if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
                image.SetPixel(x, y, color);
        }
    }

    private static void DrawSkull(Image image, int cx, int cy, Color color)
    {
        // Head
        for (float a = 0; a < Mathf.Pi; a += 0.1f)
        {
            int x = cx + (int)(Mathf.Cos(a) * 8);
            int y = cy - 3 + (int)(Mathf.Sin(a) * -7);
            if (x >= 0 && x < IconSize && y >= 0 && y < IconSize)
                image.SetPixel(x, y, color);
        }
        // Jaw
        DrawLine(image, cx - 6, cy + 2, cx - 4, cy + 8, color, 1);
        DrawLine(image, cx + 6, cy + 2, cx + 4, cy + 8, color, 1);
        DrawLine(image, cx - 4, cy + 8, cx + 4, cy + 8, color, 1);
        // Eyes
        FillCircle(image, cx - 3, cy - 2, 2, color);
        FillCircle(image, cx + 3, cy - 2, 2, color);
    }

    private static void DrawMirror(Image image, int cx, int cy, Color color)
    {
        // Three overlapping figures
        for (int offset = -5; offset <= 5; offset += 5)
        {
            float alpha = offset == 0 ? 1f : 0.5f;
            for (int y = -8; y <= 8; y++)
            {
                int x = cx + offset;
                if (x >= 0 && x < IconSize && cy + y >= 0 && cy + y < IconSize)
                    image.SetPixel(x, cy + y, color * alpha);
            }
            // Head
            FillCircle(image, cx + offset, cy - 6, 3, color * alpha);
        }
    }

    private static void DrawStar(Image image, int cx, int cy, Color color)
    {
        // 5-point star
        for (int i = 0; i < 5; i++)
        {
            float a1 = i * Mathf.Tau / 5 - Mathf.Pi / 2;
            float a2 = (i + 2) * Mathf.Tau / 5 - Mathf.Pi / 2;
            int x1 = cx + (int)(Mathf.Cos(a1) * 10);
            int y1 = cy + (int)(Mathf.Sin(a1) * 10);
            int x2 = cx + (int)(Mathf.Cos(a2) * 10);
            int y2 = cy + (int)(Mathf.Sin(a2) * 10);
            DrawLine(image, x1, y1, x2, y2, color, 1);
        }
    }

    private static void DrawCross(Image image, int cx, int cy, Color color)
    {
        // Health cross
        for (int y = -10; y <= 10; y++)
        {
            for (int x = -3; x <= 3; x++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                    image.SetPixel(px, py, color);
            }
        }
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -3; y <= 3; y++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                    image.SetPixel(px, py, color);
            }
        }
    }

    private static void DrawQuestion(Image image, int cx, int cy, Color color)
    {
        DrawLine(image, cx - 4, cy - 8, cx + 4, cy - 8, color, 1);
        DrawLine(image, cx + 4, cy - 8, cx + 4, cy - 2, color, 1);
        DrawLine(image, cx + 4, cy - 2, cx, cy - 2, color, 1);
        DrawLine(image, cx, cy - 2, cx, cy + 2, color, 1);
        FillCircle(image, cx, cy + 6, 2, color);
    }

    private static void DrawLine(Image image, int x1, int y1, int x2, int y2, Color color, int thickness)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = Mathf.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
            {
                for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                {
                    int px = x1 + tx;
                    int py = y1 + ty;
                    if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                        image.SetPixel(px, py, color);
                }
            }

            if (x1 == x2 && y1 == y2) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x1 += sx; }
            if (e2 < dx) { err += dx; y1 += sy; }
        }
    }

    private static void FillCircle(Image image, int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
                        image.SetPixel(px, py, color);
                }
            }
        }
    }

    private static void DrawHealHeart(Image image, int cx, int cy, Color color)
    {
        // Draw a heart shape with a plus sign for healing
        // Heart outline
        for (float t = 0; t < Mathf.Tau; t += 0.05f)
        {
            // Heart parametric equation
            float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
            float y = -(13 * Mathf.Cos(t) - 5 * Mathf.Cos(2*t) - 2 * Mathf.Cos(3*t) - Mathf.Cos(4*t));
            int px = cx + (int)(x * 0.6f);
            int py = cy + (int)(y * 0.5f) - 1;
            if (px >= 0 && px < IconSize && py >= 0 && py < IconSize)
            {
                image.SetPixel(px, py, color);
                // Thicken the line
                if (px + 1 < IconSize) image.SetPixel(px + 1, py, color);
            }
        }

        // Plus sign in center (medical cross)
        Color brightColor = color * 1.2f;
        brightColor.A = 1f;
        // Vertical bar
        for (int y = -4; y <= 4; y++)
        {
            int py = cy + y;
            if (py >= 0 && py < IconSize)
            {
                image.SetPixel(cx, py, brightColor);
                if (cx + 1 < IconSize) image.SetPixel(cx + 1, py, brightColor);
            }
        }
        // Horizontal bar
        for (int x = -4; x <= 4; x++)
        {
            int px = cx + x;
            if (px >= 0 && px < IconSize)
            {
                image.SetPixel(px, cy, brightColor);
                if (cy + 1 < IconSize) image.SetPixel(px, cy + 1, brightColor);
            }
        }
    }

    private static void DrawBorder(Image image, Color color)
    {
        for (int i = 0; i < IconSize; i++)
        {
            image.SetPixel(i, 0, color);
            image.SetPixel(i, IconSize - 1, color);
            image.SetPixel(0, i, color);
            image.SetPixel(IconSize - 1, i, color);
        }
    }
}
