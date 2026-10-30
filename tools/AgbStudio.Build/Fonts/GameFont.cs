using System.Globalization;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Fonts;

/// <summary>A game font after conversion: glyph bitmaps in shades 0..3 (or 0..1), metrics, lookup by code point.</summary>
public sealed class GameFont
{
    public sealed class Glyph
    {
        public int CodePoint;
        public int Width, Height, Left, Top;  // Top: from the line top
        public int Advance;
        public byte[] Shades = [];             // Width * Height, 0 = empty, Bpp 2: 1..3 (3 darkest)
    }

    public required string Name { get; init; }
    public int Bpp { get; init; } = 2;
    public int LineHeight { get; init; } = 14;
    public int Baseline { get; init; } = 10;
    public bool Shadow { get; init; }
    public int Spacing { get; init; } = 1;
    public bool Variable { get; init; } = true;
    public FontFile Settings { get; init; } = new();
    public Dictionary<int, Glyph> Glyphs { get; } = new();
    public HashSet<int> Missing { get; } = new();
    // Vertical position of upper vowels and tone marks as drawn by the source font (line-top based).
    public int UpperVowelTop { get; set; }
    public int ToneTop { get; set; }

    public Glyph? Get(int cp) => Glyphs.GetValueOrDefault(cp);

    public int RomBytes => Glyphs.Values.Sum(g => (g.Width * Bpp + 7) / 8 * g.Height + 8);
}

/// <summary>Builds a GameFont from a .font file: TrueType, BDF or an image sheet; applies glyph edits.</summary>
public static class FontBuilder
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> CharacterSets = new Dictionary<string, IReadOnlyList<int>>
    {
        ["Basic Latin"] = Enumerable.Range(0x20, 95).ToList(),
        ["Thai"] = Enumerable.Range(0x0E01, 0x3A).Concat(Enumerable.Range(0x0E3F, 0x1D)).ToList(),
        ["Symbols"] = new[] { 0x2026, 0x2022, 0x00A9, 0x00AE, 0x00B0, 0x00B1, 0x00D7, 0x00F7, 0x20AC, 0x00A3, 0x00A5, 0x2122 },
        ["Japanese kana"] = Enumerable.Range(0x3041, 0x56).Concat(Enumerable.Range(0x30A1, 0x5A)).Concat([0x30FC, 0x3001, 0x3002]).ToList(),
    };

    public static IEnumerable<int> CodePointsOf(FontFile f) =>
        f.CharacterSets.SelectMany(s => CharacterSets.GetValueOrDefault(s) ?? Array.Empty<int>())
            .Concat(ThaiText.CodePoints(f.ExtraCharacters))
            .Distinct();

    public static GameFont Build(FontFile f, string fontsDir, IEnumerable<int>? extra = null, string? fallbackTtf = null)
    {
        fallbackTtf ??= Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts", "fallback-latin.ttf");
        var raw = BuildGlyphs(f, fontsDir, extra, fallbackTtf, f.Baseline, f.LineHeight);
        // Stacked Thai marks rise above the ascender: lower the baseline so nothing is drawn above the
        // line top, and grow the line so descenders still fit.
        var above = raw.Glyphs.Values.Where(g => g.Height > 0).Select(g => g.Top).DefaultIfEmpty(0).Min();
        var stack = raw.Glyphs.ContainsKey(0x0E34) ? Math.Max(0, raw.UpperVowelTop - raw.ToneTop) : 0;
        var baseline = f.Baseline + Math.Max(0, -above) + (f.StackUpperMarks ? stack : 0);
        var below = raw.Glyphs.Values.Where(g => g.Height > 0).Select(g => g.Top + g.Height).DefaultIfEmpty(f.LineHeight).Max() - f.Baseline;
        var line = Math.Max(f.LineHeight, baseline + below);
        return baseline == f.Baseline && line == f.LineHeight ? raw : BuildGlyphs(f, fontsDir, extra, fallbackTtf, baseline, line);
    }

    static GameFont BuildGlyphs(FontFile f, string fontsDir, IEnumerable<int>? extra, string? fallbackTtf, int baseline, int lineHeight)
    {
        var font = new GameFont
        {
            Name = f.Name, Bpp = f.Bpp, LineHeight = lineHeight, Baseline = baseline, Shadow = f.DropShadow,
            Spacing = f.Spacing, Variable = f.Variable, Settings = f,
        };
        var cps = CodePointsOf(f).Concat(extra ?? []).Distinct().OrderBy(c => c).ToList();
        var source = Path.Combine(fontsDir, f.Source);
        switch (f.SourceKind)
        {
            case FontSourceKind.Ttf:
            {
                var tt = TrueType.Load(source);
                var fallback = fallbackTtf != null && File.Exists(fallbackTtf) ? TrueType.Load(fallbackTtf) : null;
                foreach (var cp in cps)
                {
                    var src = tt.Has(cp) ? tt : fallback != null && fallback.Has(cp) ? fallback : null;
                    if (src == null)
                    {
                        if (cp != ' ') font.Missing.Add(cp);
                        if (cp == ' ') font.Glyphs[cp] = new GameFont.Glyph { CodePoint = cp, Advance = Math.Max(2, f.Size / 3) };
                        continue;
                    }
                    font.Glyphs[cp] = FromRaster(Rasterizer.Render(src, cp, f.Size), f, baseline);
                }
                break;
            }
            case FontSourceKind.Bdf:
                foreach (var g in Bdf.Read(source, baseline))
                    if (cps.Contains(g.CodePoint)) font.Glyphs[g.CodePoint] = g;
                foreach (var cp in cps.Where(c => !font.Glyphs.ContainsKey(c) && c != ' ')) font.Missing.Add(cp);
                break;
            case FontSourceKind.Image:
                ImageFont.Read(source, f, cps, font);
                break;
        }
        if (!f.Variable)
            foreach (var g in font.Glyphs.Values)
                if (!ThaiText.IsMark(g.CodePoint)) g.Advance = f.FixedWidth;
        ApplyOverrides(f, font);
        // Reference heights for Thai mark stacking.
        var upper = font.Get(0x0E34);
        var tone = font.Get(0x0E48);
        font.UpperVowelTop = upper?.Top ?? 0;
        font.ToneTop = tone?.Top ?? 0;
        return font;
    }

    static GameFont.Glyph FromRaster(RasterGlyph r, FontFile f, int baseline)
    {
        var shades = new byte[r.Width * r.Height];
        for (var i = 0; i < shades.Length; i++)
        {
            var c = r.Coverage[i];
            shades[i] = f.Bpp == 1 ? (byte)(c >= 128 ? 1 : 0) : c >= 176 ? (byte)3 : c >= 96 ? (byte)2 : c >= 40 ? (byte)1 : (byte)0;
        }
        var g = new GameFont.Glyph
        {
            CodePoint = r.CodePoint, Width = r.Width, Height = r.Height, Left = r.Left, Top = baseline + r.Top, Shades = shades,
            Advance = ThaiText.IsMark(r.CodePoint) ? 0 : r.Advance + (r.Width > 0 ? f.Spacing - 1 : 0),
        };
        Trim(g);
        return g;
    }

    // Drops empty rows / columns at the edges so bitmaps stay small.
    static void Trim(GameFont.Glyph g)
    {
        if (g.Width == 0 || g.Shades.All(s => s == 0))
        {
            g.Width = g.Height = 0;
            g.Shades = [];
            return;
        }
        int x0 = g.Width, x1 = -1, y0 = g.Height, y1 = -1;
        for (var y = 0; y < g.Height; y++)
            for (var x = 0; x < g.Width; x++)
                if (g.Shades[y * g.Width + x] != 0) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        var w = x1 - x0 + 1;
        var h = y1 - y0 + 1;
        var s = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) s[y * w + x] = g.Shades[(y + y0) * g.Width + x + x0];
        g.Left += x0;
        g.Top += y0;
        g.Width = w;
        g.Height = h;
        g.Shades = s;
    }

    static void ApplyOverrides(FontFile f, GameFont font)
    {
        foreach (var (key, o) in f.Glyphs)
        {
            var cp = key.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ? int.Parse(key[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : char.ConvertToUtf32(key, 0);
            var g = font.Get(cp) ?? new GameFont.Glyph { CodePoint = cp, Top = 0 };
            if (o.Rows.Count > 0)
            {
                g.Height = o.Rows.Count;
                g.Width = o.Rows.Max(r => r.Length);
                g.Shades = new byte[g.Width * g.Height];
                for (var y = 0; y < g.Height; y++)
                    for (var x = 0; x < o.Rows[y].Length; x++) g.Shades[y * g.Width + x] = (byte)(o.Rows[y][x] - '0');
                g.Left = 0;
            }
            if (o.Advance >= 0) g.Advance = o.Advance;
            font.Glyphs[cp] = g;
            font.Missing.Remove(cp);
        }
    }

    /// <summary>Packs a glyph's shades into rows of `bpp` bits, MSB first (runtime TextBox::drawGlyph format).</summary>
    public static byte[] Pack(GameFont.Glyph g, int bpp)
    {
        var rowBytes = (g.Width * bpp + 7) / 8;
        var o = new byte[rowBytes * g.Height];
        for (var y = 0; y < g.Height; y++)
            for (var x = 0; x < g.Width; x++)
            {
                var v = g.Shades[y * g.Width + x];
                var bit = x * bpp;
                o[y * rowBytes + bit / 8] |= (byte)(v << (8 - bpp - bit % 8));
            }
        return o;
    }
}

/// <summary>BDF bitmap fonts (one bit per pixel).</summary>
public static class Bdf
{
    public static List<GameFont.Glyph> Read(string path, int baseline)
    {
        var glyphs = new List<GameFont.Glyph>();
        GameFont.Glyph? cur = null;
        var rows = new List<string>();
        var inBitmap = false;
        int bbw = 0, bbh = 0, bbx = 0, bby = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            switch (parts[0])
            {
                case "STARTCHAR": cur = new GameFont.Glyph(); rows.Clear(); break;
                case "ENCODING" when cur != null: cur.CodePoint = int.Parse(parts[1], CultureInfo.InvariantCulture); break;
                case "DWIDTH" when cur != null: cur.Advance = int.Parse(parts[1], CultureInfo.InvariantCulture); break;
                case "BBX" when cur != null:
                    bbw = int.Parse(parts[1], CultureInfo.InvariantCulture); bbh = int.Parse(parts[2], CultureInfo.InvariantCulture);
                    bbx = int.Parse(parts[3], CultureInfo.InvariantCulture); bby = int.Parse(parts[4], CultureInfo.InvariantCulture);
                    break;
                case "BITMAP": inBitmap = true; break;
                case "ENDCHAR" when cur != null:
                    inBitmap = false;
                    cur.Width = bbw;
                    cur.Height = bbh;
                    cur.Left = bbx;
                    cur.Top = baseline - (bby + bbh);
                    cur.Shades = new byte[bbw * bbh];
                    for (var y = 0; y < bbh && y < rows.Count; y++)
                    {
                        var bits = Convert.ToUInt64(rows[y].PadRight(16, '0')[..16], 16);
                        for (var x = 0; x < bbw && x < 64; x++)
                            if ((bits & (1UL << (63 - x))) != 0) cur.Shades[y * bbw + x] = 1;
                    }
                    glyphs.Add(cur);
                    cur = null;
                    break;
                default:
                    if (inBitmap) rows.Add(parts[0]);
                    break;
            }
        }
        return glyphs;
    }
}

/// <summary>Image sheet fonts: a grid of cells (FixedWidth x CellHeight) in the order of the character sets.</summary>
public static class ImageFont
{
    public static void Read(string path, FontFile f, IReadOnlyList<int> cps, GameFont font)
    {
        var img = Core.Png.Read(path);
        var cols = Math.Max(1, img.Width / f.FixedWidth);
        var order = FontBuilder.CodePointsOf(f).ToList();
        for (var i = 0; i < order.Count; i++)
        {
            var cx = i % cols * f.FixedWidth;
            var cy = i / cols * f.CellHeight;
            if (cy + f.CellHeight > img.Height) { font.Missing.Add(order[i]); continue; }
            var g = new GameFont.Glyph { CodePoint = order[i], Width = f.FixedWidth, Height = f.CellHeight, Top = 0, Advance = f.FixedWidth };
            g.Shades = new byte[g.Width * g.Height];
            var used = 0;
            for (var y = 0; y < g.Height; y++)
                for (var x = 0; x < g.Width; x++)
                {
                    var p = img.Pixels[(cy + y) * img.Width + cx + x];
                    if ((p >> 24) < 128) continue;
                    var lum = ((p >> 16) & 0xFF) * 3 + ((p >> 8) & 0xFF) * 4 + (p & 0xFF);
                    g.Shades[y * g.Width + x] = (byte)(f.Bpp == 1 ? 1 : lum < 600 ? 3 : lum < 1300 ? 2 : 1);
                    used = Math.Max(used, x + 1);
                }
            if (f.Variable && used > 0) g.Advance = used + f.Spacing;
            if (cps.Contains(order[i])) font.Glyphs[order[i]] = g;
        }
    }
}
