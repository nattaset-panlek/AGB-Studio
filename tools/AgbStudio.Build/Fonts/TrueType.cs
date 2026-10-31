using System.Buffers.Binary;

namespace AgbStudio.Build.Fonts;

/// <summary>Minimal TrueType (glyf) reader: cmap, metrics and outlines. Enough to rasterize game fonts.</summary>
public sealed class TrueType
{
    readonly byte[] _d;
    readonly Dictionary<string, (int Offset, int Length)> _tables = new();
    readonly Dictionary<int, int> _cmap = new();
    int _numGlyphs, _numHMetrics, _locaFormat;

    public int UnitsPerEm { get; private set; }
    public int Ascender { get; private set; }
    public int Descender { get; private set; }
    public string Name { get; }

    public TrueType(byte[] data, string name)
    {
        _d = data;
        Name = name;
        if (U32(0) != 0x00010000 && U32(0) != 0x74727565) throw new InvalidDataException($"{name} is not a TrueType font: it is an OpenType font with CFF outlines, or not a font file, and AGB Studio reads only TrueType outlines. Use the .ttf version of the font.");
        var n = U16(4);
        for (var i = 0; i < n; i++)
        {
            var rec = 12 + i * 16;
            var tag = System.Text.Encoding.ASCII.GetString(_d, rec, 4);
            _tables[tag] = ((int)U32(rec + 8), (int)U32(rec + 12));
        }
        foreach (var t in new[] { "head", "maxp", "cmap", "hhea", "hmtx", "loca", "glyf" })
            if (!_tables.ContainsKey(t)) throw new InvalidDataException($"{name} is damaged or incomplete (its '{t}' table is missing), so it can't be read. Download the font again, or use another one.");
        var head = _tables["head"].Offset;
        UnitsPerEm = U16(head + 18);
        _locaFormat = S16(head + 50);
        _numGlyphs = U16(_tables["maxp"].Offset + 4);
        var hhea = _tables["hhea"].Offset;
        Ascender = S16(hhea + 4);
        Descender = S16(hhea + 6);
        _numHMetrics = U16(hhea + 34);
        ReadCmap();
    }

    public static TrueType Load(string path) => new(File.ReadAllBytes(path), Path.GetFileName(path));

    int U16(int o) => BinaryPrimitives.ReadUInt16BigEndian(_d.AsSpan(o));
    short S16(int o) => BinaryPrimitives.ReadInt16BigEndian(_d.AsSpan(o));
    uint U32(int o) => BinaryPrimitives.ReadUInt32BigEndian(_d.AsSpan(o));

    void ReadCmap()
    {
        var c = _tables["cmap"].Offset;
        var count = U16(c + 2);
        int best = -1, bestFormat = 0;
        for (var i = 0; i < count; i++)
        {
            var platform = U16(c + 4 + i * 8);
            var encoding = U16(c + 6 + i * 8);
            var off = c + (int)U32(c + 8 + i * 8);
            var format = U16(off);
            var unicode = platform == 0 || (platform == 3 && (encoding == 1 || encoding == 10));
            if (!unicode) continue;
            if (format == 12 || (format == 4 && bestFormat != 12)) { best = off; bestFormat = format; }
        }
        if (best < 0) throw new InvalidDataException($"{Name} has no Unicode character map, so its letters can't be found. Use a Unicode .ttf font.");
        if (bestFormat == 4)
        {
            var segX2 = U16(best + 6);
            var ends = best + 14;
            var starts = ends + segX2 + 2;
            var deltas = starts + segX2;
            var ranges = deltas + segX2;
            for (var s = 0; s < segX2 / 2; s++)
            {
                int end = U16(ends + s * 2), start = U16(starts + s * 2), delta = S16(deltas + s * 2), range = U16(ranges + s * 2);
                for (var cp = start; cp <= end && cp != 0xFFFF; cp++)
                {
                    int g;
                    if (range == 0) g = (cp + delta) & 0xFFFF;
                    else
                    {
                        var at = ranges + s * 2 + range + (cp - start) * 2;
                        g = U16(at);
                        if (g != 0) g = (g + delta) & 0xFFFF;
                    }
                    if (g != 0) _cmap[cp] = g;
                }
            }
        }
        else
        {
            var groups = (int)U32(best + 12);
            for (var i = 0; i < groups; i++)
            {
                var gOff = best + 16 + i * 12;
                uint start = U32(gOff), end = U32(gOff + 4), startGlyph = U32(gOff + 8);
                for (var cp = start; cp <= end && cp - start < 0x10000; cp++) _cmap[(int)cp] = (int)(startGlyph + cp - start);
            }
        }
    }

    public bool Has(int codepoint) => _cmap.ContainsKey(codepoint);
    public int GlyphIndex(int codepoint) => _cmap.GetValueOrDefault(codepoint);
    public IEnumerable<int> CodePoints => _cmap.Keys;

    public (int Advance, int Lsb) HMetrics(int glyph)
    {
        var h = _tables["hmtx"].Offset;
        if (glyph < _numHMetrics) return (U16(h + glyph * 4), S16(h + glyph * 4 + 2));
        return (U16(h + (_numHMetrics - 1) * 4), S16(h + _numHMetrics * 4 + (glyph - _numHMetrics) * 2));
    }

    int GlyphOffset(int glyph, out int length)
    {
        var loca = _tables["loca"].Offset;
        int a, b;
        if (_locaFormat == 0) { a = U16(loca + glyph * 2) * 2; b = U16(loca + glyph * 2 + 2) * 2; }
        else { a = (int)U32(loca + glyph * 4); b = (int)U32(loca + glyph * 4 + 4); }
        length = b - a;
        return _tables["glyf"].Offset + a;
    }

    /// <summary>Contours as closed polylines in font units (quadratics flattened), y up.</summary>
    public List<List<(double X, double Y)>> Outline(int glyph, int depth = 0)
    {
        var result = new List<List<(double, double)>>();
        if (glyph < 0 || glyph >= _numGlyphs || depth > 8) return result;
        var o = GlyphOffset(glyph, out var len);
        if (len == 0) return result;
        var contours = S16(o);
        if (contours >= 0) return Simple(o, contours);
        // Composite
        var p = o + 10;
        while (true)
        {
            var flags = U16(p);
            var index = U16(p + 2);
            p += 4;
            double dx, dy;
            if ((flags & 1) != 0) { dx = S16(p); dy = S16(p + 2); p += 4; }
            else { dx = (sbyte)_d[p]; dy = (sbyte)_d[p + 1]; p += 2; }
            double a = 1, b = 0, c = 0, d = 1;
            if ((flags & 8) != 0) { a = d = S16(p) / 16384.0; p += 2; }
            else if ((flags & 0x40) != 0) { a = S16(p) / 16384.0; d = S16(p + 2) / 16384.0; p += 4; }
            else if ((flags & 0x80) != 0) { a = S16(p) / 16384.0; b = S16(p + 2) / 16384.0; c = S16(p + 4) / 16384.0; d = S16(p + 6) / 16384.0; p += 8; }
            foreach (var contour in Outline(index, depth + 1))
                result.Add(contour.Select(pt => (pt.Item1 * a + pt.Item2 * c + dx, pt.Item1 * b + pt.Item2 * d + dy)).ToList());
            if ((flags & 0x20) == 0) break;
        }
        return result;
    }

    List<List<(double, double)>> Simple(int o, int contours)
    {
        var ends = new int[contours];
        for (var i = 0; i < contours; i++) ends[i] = U16(o + 10 + i * 2);
        var points = contours == 0 ? 0 : ends[^1] + 1;
        var p = o + 10 + contours * 2;
        var instr = U16(p);
        p += 2 + instr;
        var flags = new byte[points];
        for (var i = 0; i < points; i++)
        {
            var f = _d[p++];
            flags[i] = f;
            if ((f & 8) != 0)
            {
                var rep = _d[p++];
                for (var k = 0; k < rep && i + 1 < points; k++) flags[++i] = f;
            }
        }
        var xs = new int[points];
        var ys = new int[points];
        var v = 0;
        for (var i = 0; i < points; i++)
        {
            var f = flags[i];
            if ((f & 2) != 0) { v += (f & 16) != 0 ? _d[p] : -_d[p]; p++; }
            else if ((f & 16) == 0) { v += S16(p); p += 2; }
            xs[i] = v;
        }
        v = 0;
        for (var i = 0; i < points; i++)
        {
            var f = flags[i];
            if ((f & 4) != 0) { v += (f & 32) != 0 ? _d[p] : -_d[p]; p++; }
            else if ((f & 32) == 0) { v += S16(p); p += 2; }
            ys[i] = v;
        }
        var result = new List<List<(double, double)>>();
        var start = 0;
        foreach (var end in ends)
        {
            var pts = new List<(double X, double Y, bool On)>();
            for (var i = start; i <= end; i++) pts.Add((xs[i], ys[i], (flags[i] & 1) != 0));
            start = end + 1;
            if (pts.Count == 0) continue;
            // Insert implied on-curve points between consecutive off-curve points.
            var full = new List<(double X, double Y, bool On)>();
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                full.Add(a);
                if (!a.On && !b.On) full.Add(((a.X + b.X) / 2, (a.Y + b.Y) / 2, true));
            }
            var first = full.FindIndex(q => q.On);
            if (first < 0) continue;
            var poly = new List<(double, double)>();
            var n = full.Count;
            var cur = full[first];
            poly.Add((cur.X, cur.Y));
            for (var k = 1; k <= n; k++)
            {
                var q = full[(first + k) % n];
                if (q.On)
                {
                    poly.Add((q.X, q.Y));
                    cur = q;
                    continue;
                }
                var next = full[(first + k + 1) % n];
                for (var t = 1; t <= 6; t++)
                {
                    var tt = t / 6.0;
                    var x = (1 - tt) * (1 - tt) * cur.X + 2 * (1 - tt) * tt * q.X + tt * tt * next.X;
                    var y = (1 - tt) * (1 - tt) * cur.Y + 2 * (1 - tt) * tt * q.Y + tt * tt * next.Y;
                    poly.Add((x, y));
                }
                cur = next;
                k++;
            }
            result.Add(poly);
        }
        return result;
    }
}

/// <summary>A rasterized glyph: coverage 0..255 per pixel, placed relative to the pen and the baseline.</summary>
public sealed record RasterGlyph(int CodePoint, int Width, int Height, int Left, int Top, int Advance, byte[] Coverage);

public static class Rasterizer
{
    /// <summary>Renders a glyph at <paramref name="sizePx"/> (em size) with 4x4 supersampling and non-zero winding.</summary>
    public static RasterGlyph Render(TrueType font, int codepoint, double sizePx)
    {
        var g = font.GlyphIndex(codepoint);
        var scale = sizePx / font.UnitsPerEm;
        var (adv, _) = font.HMetrics(g);
        var advance = (int)Math.Round(adv * scale);
        var contours = font.Outline(g);
        if (contours.Count == 0) return new RasterGlyph(codepoint, 0, 0, 0, 0, advance, []);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var c in contours)
            foreach (var (x, y) in c)
            {
                minX = Math.Min(minX, x * scale); maxX = Math.Max(maxX, x * scale);
                minY = Math.Min(minY, y * scale); maxY = Math.Max(maxY, y * scale);
            }
        var left = (int)Math.Floor(minX);
        var top = (int)Math.Floor(-maxY);  // y down from the baseline
        var w = (int)Math.Ceiling(maxX) - left;
        var h = (int)Math.Ceiling(-minY) - top;
        if (w <= 0 || h <= 0) return new RasterGlyph(codepoint, 0, 0, 0, 0, advance, []);
        const int ss = 4;
        var cov = new int[w * h];
        var edges = new List<(double X0, double Y0, double X1, double Y1, int Dir)>();
        foreach (var c in contours)
            for (var i = 0; i < c.Count; i++)
            {
                var a = c[i];
                var b = c[(i + 1) % c.Count];
                double x0 = a.X * scale - left, y0 = -a.Y * scale - top, x1 = b.X * scale - left, y1 = -b.Y * scale - top;
                if (Math.Abs(y0 - y1) < 1e-9) continue;
                edges.Add(y0 < y1 ? (x0, y0, x1, y1, 1) : (x1, y1, x0, y0, -1));
            }
        var crossings = new List<(double X, int Dir)>();
        for (var sy = 0; sy < h * ss; sy++)
        {
            var y = (sy + 0.5) / ss;
            crossings.Clear();
            foreach (var e in edges)
                if (y >= e.Y0 && y < e.Y1) crossings.Add((e.X0 + (y - e.Y0) * (e.X1 - e.X0) / (e.Y1 - e.Y0), e.Dir));
            crossings.Sort((p, q) => p.X.CompareTo(q.X));
            var winding = 0;
            for (var i = 0; i < crossings.Count - 1; i++)
            {
                winding += crossings[i].Dir;
                if (winding == 0) continue;
                // Fill [x_i, x_i+1) in subsample units.
                var xa = crossings[i].X * ss;
                var xb = crossings[i + 1].X * ss;
                for (var sx = (int)Math.Ceiling(xa - 0.5); sx + 0.5 < xb; sx++)
                {
                    var px = sx / ss;
                    if (px >= 0 && px < w) cov[(sy / ss) * w + px]++;
                }
            }
        }
        var bytes = cov.Select(v => (byte)Math.Min(255, v * 255 / (ss * ss))).ToArray();
        return new RasterGlyph(codepoint, w, h, left, top, advance, bytes);
    }
}
