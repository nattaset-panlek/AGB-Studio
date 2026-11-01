using System.Globalization;
using System.Text;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Fonts;

/// <summary>
/// AGB Pixel: AGB Studio's own 1bpp pixel font for Thai and Latin (an original design, drawn for this project; see
/// docs/THIRD_PARTY.md and docs/user/tutorials/thai-fonts.md). The glyphs are text art in <see cref="Art"/> (AgbPixelFont.Glyphs.cs); <see cref="Bdf"/>
/// writes them as a BDF font, the form the font importer reads (third_party/fonts/agb_pixel.bdf is this output).
/// Metrics: Latin cap height 7, x-height 5, descenders 2; Thai body 6 with ascenders (ป ฝ ฟ ฬ) 3 above it; upper vowels
/// two rows above the body after one empty row; tone marks sit at the upper-vowel height and TextLayout lifts them
/// above an upper vowel; lower vowels one row under the baseline. With a stacked tone mark that is 13 rows above the
/// baseline and 3 below, a 16 px line. Proportional: Thai letters range from 4 to 7 px wide, Latin from 1 to 5.
/// </summary>
public static partial class AgbPixelFont
{
    public const string FileName = "agb_pixel.bdf";
    public const string FamilyName = "AGB Pixel";
    public const int Ascent = 13, Descent = 3;

    /// <summary>One glyph: rows of '#' (ink) and '.' (empty), the top-left of the rows relative to the pen and the
    /// baseline (Y = -1 is the row on the baseline), and the advance.</summary>
    public sealed record Glyph(int CodePoint, int X, int Y, int Advance, IReadOnlyList<string> Rows);

    static IReadOnlyList<Glyph>? s_glyphs;

    /// <summary>Every glyph of the font, by code point.</summary>
    public static IReadOnlyList<Glyph> Glyphs => s_glyphs ??= Parse(Art);

    /// <summary>The .font settings of a project font made from this BDF (the default game font of new projects and templates).</summary>
    public static FontFile Settings(string name) => new()
    {
        Name = name, SourceKind = FontSourceKind.Bdf, Source = FileName, Size = 8, Variable = true,
        CharacterSets = { "Basic Latin", "Thai", "Symbols" }, LineHeight = Ascent + Descent, Baseline = Ascent, Spacing = 1, Bpp = 1, DropShadow = true,
    };

    /// <summary>Writes the BDF next to a project's fonts (fonts/agb_pixel.bdf) and returns its path.</summary>
    public static string WriteTo(string fontsDir)
    {
        Directory.CreateDirectory(fontsDir);
        var path = Path.Combine(fontsDir, FileName);
        File.WriteAllText(path, Bdf(), new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// Text art format: a glyph starts with a header line "U+0E01" with optional "x=", "y=" and "w=" (pen offset,
    /// top row, advance); "= U+0E4D U+0E32" instead of rows composes the glyph from others. Rows follow. A row of
    /// dashes is the baseline: rows above it end on the baseline, rows below it hang under it. Without a dash row, "y="
    /// gives the top row. The advance is the row width plus one, or 0 for Thai marks. Text after "//" is a comment.
    /// </summary>
    public static IReadOnlyList<Glyph> Parse(string art)
    {
        var list = new List<Glyph>();
        var byCp = new Dictionary<int, Glyph>();
        string[]? header = null;
        var rows = new List<string>();
        var composed = new List<(int Cp, List<int> Parts)>();
        void Flush()
        {
            if (header == null) return;
            var cp = int.Parse(header[0][2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int? x = null, y = null, w = null;
            List<int>? compose = null;
            foreach (var t in header.Skip(1))
            {
                if (t.StartsWith("x=", StringComparison.Ordinal)) x = int.Parse(t[2..], CultureInfo.InvariantCulture);
                else if (t.StartsWith("y=", StringComparison.Ordinal)) y = int.Parse(t[2..], CultureInfo.InvariantCulture);
                else if (t.StartsWith("w=", StringComparison.Ordinal)) w = int.Parse(t[2..], CultureInfo.InvariantCulture);
                else if (t == "=") compose = [];
                else if (compose != null && t.StartsWith("U+", StringComparison.Ordinal)) compose.Add(int.Parse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                else throw new FormatException($"U+{cp:X4}: unknown header item '{t}'");
            }
            Glyph g;
            if (compose != null)
            {
                composed.Add((cp, compose));
                g = new Glyph(cp, 0, 0, 0, []);  // placeholder until every part is read
            }
            else
            {
                var dash = rows.FindIndex(r => r.Length > 0 && r.All(ch => ch == '-'));
                var art2 = rows.Where((_, i) => i != dash).ToList();
                if (art2.Any(r => r.Any(ch => ch is not ('#' or '.')))) throw new FormatException($"U+{cp:X4}: rows may only hold '#' and '.'");
                var top = dash >= 0 ? -dash : art2.Count == 0 ? 0 : y ?? throw new FormatException($"U+{cp:X4}: needs a baseline row of dashes or y=");
                var width = art2.Count == 0 ? 0 : art2.Max(r => r.Length);
                var advance = w ?? (ThaiText.IsMark(cp) ? 0 : width + 1);
                g = new Glyph(cp, x ?? 0, top, advance, art2.Select(r => r.PadRight(width, '.')).ToList());
            }
            if (!byCp.TryAdd(cp, g)) throw new FormatException($"U+{cp:X4} is drawn twice");
            list.Add(g);
            header = null;
            rows.Clear();
        }
        foreach (var raw in art.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) line = line[..comment];
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("U+", StringComparison.Ordinal))
            {
                Flush();
                header = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
            else if (header == null) throw new FormatException($"rows before the first glyph header: '{line}'");
            else rows.Add(line);
        }
        Flush();
        foreach (var (cp, parts) in composed)
            list[list.FindIndex(g => g.CodePoint == cp)] = Compose(cp,
                parts.Select(c => byCp.TryGetValue(c, out var part) && !composed.Any(k => k.Cp == c) ? part : throw new FormatException($"U+{cp:X4}: U+{c:X4} is not drawn")).ToList());
        return list;
    }

    // A glyph made of others, each drawn at its own offsets; the advance is that of the last spacing part.
    static Glyph Compose(int cp, IReadOnlyList<Glyph> parts)
    {
        var px = parts.SelectMany(p => Pixels(p)).ToHashSet();
        if (px.Count == 0) return new Glyph(cp, 0, 0, parts.LastOrDefault(p => p.Advance > 0)?.Advance ?? 0, []);
        int x0 = px.Min(p => p.X), x1 = px.Max(p => p.X), y0 = px.Min(p => p.Y), y1 = px.Max(p => p.Y);
        var rows = Enumerable.Range(y0, y1 - y0 + 1)
            .Select(y => new string(Enumerable.Range(x0, x1 - x0 + 1).Select(x => px.Contains((x, y)) ? '#' : '.').ToArray())).ToList();
        return new Glyph(cp, x0, y0, parts.LastOrDefault(p => p.Advance > 0)?.Advance ?? 0, rows);
    }

    /// <summary>Ink pixels of a glyph as (x from the pen, y from the baseline; -1 = the row on the baseline).</summary>
    public static IEnumerable<(int X, int Y)> Pixels(Glyph g)
    {
        for (var r = 0; r < g.Rows.Count; r++)
            for (var c = 0; c < g.Rows[r].Length; c++)
                if (g.Rows[r][c] == '#') yield return (g.X + c, g.Y + r);
    }

    /// <summary>The whole font as BDF 2.1 text (LF line ends; each glyph's bitmap trimmed to its ink).</summary>
    public static string Bdf()
    {
        var sb = new StringBuilder();
        void L(string s) => sb.Append(s).Append('\n');
        var all = Glyphs.OrderBy(g => g.CodePoint).ToList();
        var ink = all.SelectMany(g => Pixels(g)).ToList();
        int bx0 = ink.Min(p => p.X), bx1 = ink.Max(p => p.X), by0 = ink.Min(p => p.Y), by1 = ink.Max(p => p.Y);
        L("STARTFONT 2.1");
        L("COMMENT AGB Pixel: the AGB Studio project's own pixel font for Thai and Latin. Original work, drawn for AGB Studio;");
        L("COMMENT covered by the AGB Studio licence (LICENSE). Generated by tools/AgbStudio.Build/Fonts/AgbPixelFont.cs, do not edit.");
        L($"FONT -agbstudio-AGB Pixel-medium-r-normal--{Ascent + Descent}-160-75-75-p-50-iso10646-1");
        L($"SIZE {Ascent + Descent} 75 75");
        L($"FONTBOUNDINGBOX {bx1 - bx0 + 1} {by1 - by0 + 1} {bx0} {-by1 - 1}");
        L("STARTPROPERTIES 8");
        L($"FAMILY_NAME \"{FamilyName}\"");
        L("WEIGHT_NAME \"Medium\"");
        L("SLANT \"R\"");
        L($"PIXEL_SIZE {Ascent + Descent}");
        L($"FONT_ASCENT {Ascent}");
        L($"FONT_DESCENT {Descent}");
        L("COPYRIGHT \"AGB Studio project, original work\"");
        L("DEFAULT_CHAR 32");
        L("ENDPROPERTIES");
        L($"CHARS {all.Count}");
        foreach (var g in all)
        {
            var px = Pixels(g).ToList();
            L($"STARTCHAR uni{g.CodePoint:X4}");
            L($"ENCODING {g.CodePoint}");
            L($"SWIDTH {g.Advance * 1000 / (Ascent + Descent)} 0");
            L($"DWIDTH {g.Advance} 0");
            if (px.Count == 0)
            {
                L("BBX 0 0 0 0");
                L("BITMAP");
            }
            else
            {
                int x0 = px.Min(p => p.X), x1 = px.Max(p => p.X), y0 = px.Min(p => p.Y), y1 = px.Max(p => p.Y);
                int w = x1 - x0 + 1, bytes = (w + 7) / 8;
                L($"BBX {w} {y1 - y0 + 1} {x0} {-y1 - 1}");
                L("BITMAP");
                var set = px.ToHashSet();
                for (var y = y0; y <= y1; y++)
                {
                    var bits = 0UL;
                    for (var x = x0; x <= x1; x++)
                        if (set.Contains((x, y))) bits |= 1UL << (bytes * 8 - 1 - (x - x0));
                    L(bits.ToString("X" + bytes * 2, CultureInfo.InvariantCulture));
                }
            }
            L("ENDCHAR");
        }
        L("ENDFONT");
        return sb.ToString();
    }
}
