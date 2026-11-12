using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperSqij;

/// <summary>Loads every embedded PNG and exposes the shared sprite rectangles.</summary>
public sealed class Art : IDisposable
{
    public Texture2D Tiles = null!;
    public Texture2D Sqij = null!;
    public Texture2D Enemies = null!;
    public Texture2D Items = null!;
    public Texture2D Monsta = null!;
    public Texture2D Altar = null!;
    public Texture2D Door = null!;
    public Texture2D Torch = null!;
    public Texture2D BgFar = null!;
    public Texture2D BgNear = null!;
    public Texture2D BgWall = null!;
    public Texture2D Glow = null!;
    public Texture2D Vignette = null!;
    public Texture2D Scanlines = null!;
    public Texture2D Logo = null!;
    public Texture2D Splash = null!;
    public Texture2D Font = null!;
    public Texture2D White = null!;

    private readonly GraphicsDevice _device;

    // Tiles atlas layout: 8 columns x 4 rows of 16x16.
    public static readonly Rectangle[] RockTiles =
    {
        new(0, 0, 16, 16), new(16, 0, 16, 16), new(32, 0, 16, 16), new(48, 0, 16, 16),
    };
    public static readonly Rectangle[] MossTiles = { new(0, 16, 16, 16), new(16, 16, 16, 16) };
    public static readonly Rectangle[] LedgeTiles = { new(0, 32, 16, 16), new(16, 32, 16, 16) };
    public static readonly Rectangle[] LavaTiles =
    {
        new(0, 48, 16, 16), new(16, 48, 16, 16), new(32, 48, 16, 16), new(48, 48, 16, 16),
    };
    public static readonly Rectangle[] SpikeTiles = { new(64, 48, 16, 16), new(80, 48, 16, 16) };
    public static readonly Rectangle[] CrystalTiles = { new(96, 48, 16, 16), new(112, 48, 16, 16) };

    // Sqij sheet: 6 columns x 2 rows of 24x24.
    public const int SqijFrame = 24;
    public const int MonsterFrame = 32;

    public Art(GraphicsDevice device)
    {
        _device = device;
        Tiles = Load("tiles.png");
        Sqij = Load("sqij.png");
        Enemies = Load("enemies.png");
        Items = Load("items.png");
        Monsta = Load("monsta.png");
        Altar = Load("altar.png");
        Door = Load("door.png");
        Torch = Load("torch.png");
        BgFar = Load("bg_far.png");
        BgNear = Load("bg_near.png");
        BgWall = Load("bg_wall.png");
        Glow = Load("glow.png");
        Vignette = Load("vignette.png");
        Scanlines = Load("scanlines.png");
        Logo = Load("logo.png");
        Splash = Load("splash.png");
        Font = Load("font.png");
        White = new Texture2D(device, 1, 1);
        White.SetData(new[] { Color.White });
    }

    private Texture2D Load(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SuperSqij.Assets." + name)
            ?? throw new InvalidOperationException("Missing embedded asset: " + name);
        return Texture2D.FromStream(_device, stream);
    }

    public void Dispose()
    {
        Tiles.Dispose(); Sqij.Dispose(); Enemies.Dispose(); Items.Dispose(); Monsta.Dispose();
        Altar.Dispose(); Door.Dispose(); Torch.Dispose(); BgFar.Dispose(); BgNear.Dispose();
        Glow.Dispose(); Vignette.Dispose(); Scanlines.Dispose(); Logo.Dispose(); Splash.Dispose();
        Font.Dispose(); White.Dispose(); BgWall.Dispose();
    }
}

/// <summary>Simple 8x10 bitmap font atlas renderer (ASCII 32..127).</summary>
public static class PixelFont
{
    public const int CellW = 8;
    public const int CellH = 10;
    public const int Advance = 7;
    public const int LineHeight = 11;

    public static float Measure(string text, float scale = 1f)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int longest = 0, current = 0;
        foreach (char c in text)
        {
            if (c == '\n') { longest = Math.Max(longest, current); current = 0; }
            else current++;
        }
        longest = Math.Max(longest, current);
        return Math.Max(0, longest * Advance * scale - scale);
    }

    public static void Draw(SpriteBatch batch, Texture2D font, string text, Vector2 position, Color color,
        float scale = 1f, float alpha = 1f, bool shadow = false)
    {
        if (shadow)
            DrawInternal(batch, font, text, position + new Vector2(scale, scale), Color.Black * (0.55f * alpha), scale, alpha);
        DrawInternal(batch, font, text, position, color, scale, alpha);
    }

    private static void DrawInternal(SpriteBatch batch, Texture2D font, string text, Vector2 position, Color color,
        float scale, float alpha)
    {
        float x = position.X;
        float y = position.Y;
        var tint = color * alpha;
        foreach (char raw in text)
        {
            char c = raw;
            if (c == '\n') { x = position.X; y += LineHeight * scale; continue; }
            if (c < 32 || c > 127) c = '?';
            int index = c - 32;
            var src = new Rectangle((index % 16) * CellW, (index / 16) * CellH, CellW, CellH);
            batch.Draw(font, new Vector2(MathF.Round(x), MathF.Round(y)), src, tint, 0f, Vector2.Zero,
                scale, SpriteEffects.None, 0f);
            x += Advance * scale;
        }
    }

    public static void DrawCentered(SpriteBatch batch, Texture2D font, string text, float centerX, float y,
        Color color, float scale = 1f, float alpha = 1f, bool shadow = false)
    {
        float w = Measure(text, scale);
        Draw(batch, font, text, new Vector2(MathF.Round(centerX - w / 2), y), color, scale, alpha, shadow);
    }
}
