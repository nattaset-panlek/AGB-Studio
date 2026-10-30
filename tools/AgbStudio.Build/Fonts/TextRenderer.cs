namespace AgbStudio.Build.Fonts;

/// <summary>Draws laid-out text exactly like the runtime TextBox (runtime/src/render/text.cpp): same glyph
/// placement, shade-to-palette mapping and drop shadow. Used for editor previews and the pixel check
/// against ROM captures (P8.09).</summary>
public static class TextRenderer
{
    public sealed record Style(byte Ink = 1, byte Shadow = 4, byte Paper = 0);

    /// <summary>Returns palette indices (width x height) for the layout, lines starting at firstLine.</summary>
    public static byte[] Render(TextLayout.Result layout, GameFont font, int width, int height, Style? style = null, int firstLine = 0)
    {
        style ??= new Style();
        var px = new byte[width * height];
        if (style.Paper != 0) Array.Fill(px, style.Paper);
        foreach (var pg in layout.Glyphs)
        {
            if (pg.Line < firstLine) continue;
            var g = font.Get(pg.CodePoint);
            if (g == null || g.Width == 0) continue;
            var y0 = (pg.Line - firstLine) * font.LineHeight + pg.Dy;
            if (y0 >= height) continue;
            DrawGlyph(px, width, height, font, g, pg.X, y0, style);
        }
        return px;
    }

    public static void DrawGlyph(byte[] px, int width, int height, GameFont font, GameFont.Glyph g, int penX, int lineTop, Style style)
    {
        void Set(int x, int y, byte v)
        {
            if (x >= 0 && y >= 0 && x < width && y < height) px[y * width + x] = v;
        }
        var shadowPass = font.Shadow && style.Shadow != 0;
        for (var pass = shadowPass ? 0 : 1; pass < 2; pass++)
            for (var y = 0; y < g.Height; y++)
                for (var x = 0; x < g.Width; x++)
                {
                    var v = g.Shades[y * g.Width + x];
                    if (v == 0) continue;
                    var dx = penX + g.Left + x;
                    var dy = lineTop + g.Top + y;
                    if (pass == 0) Set(dx + 1, dy + 1, style.Shadow);
                    else Set(dx, dy, (byte)(font.Bpp == 1 ? style.Ink : style.Ink + (3 - v)));
                }
    }

    /// <summary>Palette indices to ARGB (default text palette: paper, ink shades, shadow).</summary>
    public static uint[] ToArgb(byte[] px, uint[]? palette = null)
    {
        palette ??= [0xFFF8F4E8, 0xFF1E1A28, 0xFF5E5868, 0xFF9E98A8, 0xFFC8C0B0];
        return px.Select(i => i < palette.Length ? palette[i] : 0xFFFF00FF).ToArray();
    }

    public static uint[] Scale(uint[] src, int w, int h, int factor)
    {
        var o = new uint[w * factor * h * factor];
        for (var y = 0; y < h * factor; y++)
            for (var x = 0; x < w * factor; x++) o[y * w * factor + x] = src[(y / factor) * w + x / factor];
        return o;
    }
}
