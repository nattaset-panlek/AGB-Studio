using AgbStudio.Build.Fonts;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content;

/// <summary>The boot splash (Figma "In-game · Boot splash"): the Plus Pad mark, "AGB Studio" and "made with
/// AGB Studio" on the dark backdrop, drawn at GBA size. The backdrop itself is the backdrop colour, so the
/// image is transparent around the art.</summary>
public static class SplashArt
{
    public const uint Backdrop = 0xFF1E1A28;
    static readonly uint Ink = Canvas.Rgb(230, 232, 238);
    static readonly uint Muted = Canvas.Rgb(154, 160, 174);
    static readonly uint Accent = Canvas.Rgb(59, 79, 216);

    public static Canvas Draw()
    {
        var c = new Canvas(240, 160);
        // Plus Pad: five 10 px squares in a cross with 1 px gaps, ink arms and an indigo centre.
        const int u = 10, cx = 120, cy = 66;
        c.Rect(cx - u / 2, cy - u * 3 / 2 - 1, u, u, Ink);
        c.Rect(cx - u * 3 / 2 - 1, cy - u / 2, u, u, Ink);
        c.Rect(cx + u / 2 + 1, cy - u / 2, u, u, Ink);
        c.Rect(cx - u / 2, cy + u / 2 + 1, u, u, Ink);
        c.Rect(cx - u / 2, cy - u / 2, u, u, Accent);
        var fonts = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts");
        DrawCentred(c, Font(fonts, 12, 14, 11), "AGB Studio", 92, Ink);
        DrawCentred(c, Font(fonts, 10, 12, 9), "made with AGB Studio", 136, Muted);
        return c;
    }

    public static ColorImage ToImage(Canvas c)
    {
        var img = new ColorImage(c.Width, c.Height);
        for (var i = 0; i < c.Pixels.Length; i++)
        {
            img.Transparent[i] = Gba.IsTransparent(c.Pixels[i]);
            img.Colors[i] = img.Transparent[i] ? (ushort)0 : Gba.ToBgr555(c.Pixels[i]);
        }
        return img;
    }

    static GameFont Font(string dir, int size, int lineHeight, int baseline) => FontBuilder.Build(new FontFile
    {
        Name = $"splash{size}", Source = "fallback-latin.ttf", Size = size, Variable = true, CharacterSets = { "Basic Latin" },
        LineHeight = lineHeight, Baseline = baseline, Spacing = 1, Bpp = 1,
    }, dir);

    static void DrawCentred(Canvas c, GameFont font, string text, int top, uint color)
    {
        var layout = TextLayout.Layout(text, font, 240);
        var px = TextRenderer.Render(layout, font, 240, font.LineHeight, new TextRenderer.Style(1, 0, 0));
        int min = 240, max = -1;
        for (var i = 0; i < px.Length; i++)
            if (px[i] != 0)
            {
                min = Math.Min(min, i % 240);
                max = Math.Max(max, i % 240);
            }
        if (max < 0) return;
        var dx = (240 - (max - min + 1)) / 2 - min;
        for (var i = 0; i < px.Length; i++)
            if (px[i] != 0) c.Set(i % 240 + dx, top + i / 240, color);
    }
}
