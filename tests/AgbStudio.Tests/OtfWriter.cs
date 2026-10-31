using System.Buffers.Binary;
using System.Text;
using AgbStudio.Build.Fonts;

namespace AgbStudio.Tests;

/// <summary>
/// P8.01: writes OpenType fonts with PostScript outlines (.otf, sfnt version 'OTTO') for the font tests, from the
/// OpenType and CFF (Adobe TN 5176 / 5177) specifications: the tables head, hhea, maxp 0.5, OS/2 4, hmtx, cmap
/// (format 4), name, post 3.0 and 'CFF ' with Type 2 charstrings, local and global subroutines, and either a
/// name-keyed font or a CID-keyed one (ROS, FDArray with a Private DICT per font DICT, FDSelect format 3).
/// </summary>
public sealed class OtfWriter
{
    /// <summary>A glyph: the code point it maps to (or -1 for none), its advance in font units, its charstring.</summary>
    public sealed record Glyph(int CodePoint, int Advance, byte[] Charstring);

    public int UnitsPerEm { get; init; } = 1000;
    public int Ascender { get; init; } = 800;
    public int Descender { get; init; } = -200;
    public string Family { get; init; } = "AGB Test";
    /// <summary>Glyphs 1 onward; glyph 0 (.notdef) is an empty glyph.</summary>
    public List<Glyph> Glyphs { get; } = new();
    public List<byte[]> LocalSubrs { get; } = new();
    public List<byte[]> GlobalSubrs { get; } = new();
    /// <summary>A CID-keyed font: glyphs in the first half use font DICT 0, the rest font DICT 1 (each with its own copy
    /// of the local subroutines).</summary>
    public bool CidKeyed { get; init; }

    string PostScriptName => new(Family.Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Subroutine number to push before callsubr / callgsubr for subroutine <paramref name="index"/> of a list of <paramref name="count"/>.</summary>
    public static int SubrNumber(int index, int count) => index - (count < 1240 ? 107 : count < 33900 ? 1131 : 32768);

    /// <summary>An OTF whose glyphs are the outlines of a TrueType font (curves as the reader flattens them, so as line
    /// segments), each point at 16.16 precision, with the same metrics and advances.</summary>
    public static byte[] FromTrueType(TrueType tt, IEnumerable<int> codePoints, string family)
    {
        var w = new OtfWriter { UnitsPerEm = tt.UnitsPerEm, Ascender = tt.Ascender, Descender = tt.Descender, Family = family };
        foreach (var cp in codePoints.Where(tt.Has).Distinct().Order())
        {
            var g = tt.GlyphIndex(cp);
            var cs = new Charstring();
            long x = 0, y = 0;
            foreach (var contour in tt.Outline(g))
            {
                var pts = contour.Select(p => ((long)Math.Round(p.X * 65536), (long)Math.Round(p.Y * 65536))).ToList();
                cs.Fixed(pts[0].Item1 - x).Fixed(pts[0].Item2 - y).Op(21);  // rmoveto
                (x, y) = pts[0];
                // rlineto takes up to 24 pairs (48 numbers on the stack).
                for (var i = 1; i < pts.Count; i += 24)
                {
                    foreach (var (px, py) in pts.Skip(i).Take(24))
                    {
                        cs.Fixed(px - x).Fixed(py - y);
                        (x, y) = (px, py);
                    }
                    cs.Op(5);
                }
            }
            w.Glyphs.Add(new Glyph(cp, tt.HMetrics(g).Advance, cs.Op(14).Bytes));
        }
        return w.Write();
    }

    /// <summary>Builds a Type 2 charstring: numbers in the shortest encoding, operators, escape operators.</summary>
    public sealed class Charstring
    {
        readonly List<byte> _b = new();
        public byte[] Bytes => _b.ToArray();

        public Charstring N(params double[] values)
        {
            foreach (var v in values)
            {
                if (v != Math.Floor(v) || v < -32768 || v > 32767)
                {
                    Fixed((long)Math.Round(v * 65536));
                    continue;
                }
                var i = (int)v;
                if (i is >= -107 and <= 107) _b.Add((byte)(i + 139));
                else if (i is >= 108 and <= 1131) { _b.Add((byte)(247 + ((i - 108) >> 8))); _b.Add((byte)((i - 108) & 0xFF)); }
                else if (i is >= -1131 and <= -108) { _b.Add((byte)(251 + ((-i - 108) >> 8))); _b.Add((byte)((-i - 108) & 0xFF)); }
                else { _b.Add(28); _b.Add((byte)(i >> 8)); _b.Add((byte)i); }
            }
            return this;
        }

        /// <summary>A 16.16 fixed number (255 prefix), or an integer when it has no fraction.</summary>
        public Charstring Fixed(long raw)
        {
            if (raw % 65536 == 0) return N(raw / 65536);
            _b.Add(255);
            var v = checked((int)raw);
            _b.Add((byte)(v >> 24)); _b.Add((byte)(v >> 16)); _b.Add((byte)(v >> 8)); _b.Add((byte)v);
            return this;
        }

        public Charstring Op(int op) { _b.Add((byte)op); return this; }
        public Charstring Op12(int op) { _b.Add(12); _b.Add((byte)op); return this; }
        public Charstring Raw(params byte[] bytes) { _b.AddRange(bytes); return this; }
    }

    // ---- CFF --------------------------------------------------------------------------------------------

    static byte[] Index(IReadOnlyList<byte[]> items)
    {
        if (items.Count == 0) return [0, 0];
        var total = items.Sum(i => i.Length);
        var offSize = total + 1 <= 0xFF ? 1 : total + 1 <= 0xFFFF ? 2 : total + 1 <= 0xFFFFFF ? 3 : 4;
        var o = new List<byte> { (byte)(items.Count >> 8), (byte)items.Count, (byte)offSize };
        var at = 1;
        for (var i = 0; i <= items.Count; i++)
        {
            for (var k = offSize - 1; k >= 0; k--) o.Add((byte)(at >> (8 * k)));
            if (i < items.Count) at += items[i].Length;
        }
        foreach (var item in items) o.AddRange(item);
        return o.ToArray();
    }

    // DICT operands: offsets as 5-byte integers so a DICT's size is known before the offsets are.
    static IEnumerable<byte> Int32(int v) => [29, (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
    static IEnumerable<byte> Small(int v) => v is >= -107 and <= 107 ? [(byte)(v + 139)] : Int32(v);

    // nominalWidthX 0, and the local subroutines right after the DICT.
    static byte[] PrivateDict(int size, bool subrs) => subrs ? [.. Small(0), 21, .. Int32(size), 19] : [.. Small(0), 21];

    byte[] Cff()
    {
        var glyphCount = Glyphs.Count + 1;
        var strings = new List<byte[]>();
        for (var g = 1; g < glyphCount; g++) strings.Add(Encoding.ASCII.GetBytes($"g{g}"));
        var registry = 391 + strings.Count;
        if (CidKeyed)
        {
            strings.Add("Adobe"u8.ToArray());
            strings.Add("Identity"u8.ToArray());
        }
        var nameIndex = Index([Encoding.ASCII.GetBytes(PostScriptName)]);
        var stringIndex = Index(strings);
        var gsubrIndex = Index(GlobalSubrs);
        // charset format 2: glyphs 1.. are SIDs 391.. (their names "g1", "g2", ...), or CIDs 1.. in a CID font.
        byte[] charset = glyphCount > 1 ? [2, 1, 135, (byte)((glyphCount - 2) >> 8), (byte)(glyphCount - 2)] : [0];
        if (CidKeyed) charset = glyphCount > 1 ? [2, 0, 1, (byte)((glyphCount - 2) >> 8), (byte)(glyphCount - 2)] : [0];
        var charStrings = Index([new Charstring().Op(14).Bytes, .. Glyphs.Select(g => g.Charstring)]);
        var localIndex = LocalSubrs.Count > 0 ? Index(LocalSubrs) : [];
        var privateSize = PrivateDict(0, LocalSubrs.Count > 0).Length;
        var privateBlock = privateSize + localIndex.Length;  // a Private DICT and its local subroutines

        // Top DICT size with placeholder offsets (every offset is a 5-byte integer).
        byte[] TopDict(int charsetAt, int charStringsAt, int privateAt, int fdArrayAt, int fdSelectAt) => CidKeyed
            ? [.. Small(registry), .. Small(registry + 1), .. Small(0), 12, 30,  // ROS Adobe-Identity-0
               .. Small(glyphCount), 12, 34,                                     // CIDCount
               .. Int32(charsetAt), 15, .. Int32(charStringsAt), 17, .. Int32(fdArrayAt), 12, 36, .. Int32(fdSelectAt), 12, 37]
            : [.. Int32(charsetAt), 15, .. Int32(charStringsAt), 17, .. Int32(privateSize), .. Int32(privateAt), 18];
        var topIndexSize = Index([TopDict(0, 0, 0, 0, 0)]).Length;

        var at = 4 + nameIndex.Length + topIndexSize + stringIndex.Length + gsubrIndex.Length;
        var charsetAt = at;
        at += charset.Length;
        var charStringsAt = at;
        at += charStrings.Length;
        var o = new List<byte>();
        if (!CidKeyed)
        {
            var privateAt = at;
            var top = Index([TopDict(charsetAt, charStringsAt, privateAt, 0, 0)]);
            o.AddRange([1, 0, 4, 4]);
            o.AddRange(nameIndex); o.AddRange(top); o.AddRange(stringIndex); o.AddRange(gsubrIndex);
            o.AddRange(charset); o.AddRange(charStrings);
            o.AddRange(PrivateDict(privateSize, LocalSubrs.Count > 0)); o.AddRange(localIndex);
            return o.ToArray();
        }
        // CID-keyed: two font DICTs, each with its own Private DICT and local subroutines; FDSelect format 3.
        var split = Math.Max(1, glyphCount / 2);
        byte[] FontDict(int privateAt) => [.. Int32(privateSize), .. Int32(privateAt), 18];
        var fdArraySize = Index([FontDict(0), FontDict(0)]).Length;
        var fdArrayAt = at;
        at += fdArraySize;
        byte[] fdSelect = [3, 0, 2, 0, 0, 0, (byte)(split >> 8), (byte)split, 1, (byte)(glyphCount >> 8), (byte)glyphCount];
        var fdSelectAt = at;
        at += fdSelect.Length;
        var private0 = at;
        var private1 = at + privateBlock;
        var topDict = Index([TopDict(charsetAt, charStringsAt, 0, fdArrayAt, fdSelectAt)]);
        o.AddRange([1, 0, 4, 4]);
        o.AddRange(nameIndex); o.AddRange(topDict); o.AddRange(stringIndex); o.AddRange(gsubrIndex);
        o.AddRange(charset); o.AddRange(charStrings);
        o.AddRange(Index([FontDict(private0), FontDict(private1)]));
        o.AddRange(fdSelect);
        for (var fd = 0; fd < 2; fd++)
        {
            o.AddRange(PrivateDict(privateSize, LocalSubrs.Count > 0));
            o.AddRange(localIndex);
        }
        return o.ToArray();
    }

    // ---- sfnt -------------------------------------------------------------------------------------------

    sealed class Table(int capacity)
    {
        readonly List<byte> _b = new(capacity);
        public byte[] Bytes => _b.ToArray();
        public Table U16(int v) { _b.Add((byte)(v >> 8)); _b.Add((byte)v); return this; }
        public Table U32(long v) { U16((int)(v >> 16)); return U16((int)v); }
        public Table Zero(int n) { _b.AddRange(new byte[n]); return this; }
        public Table Add(IEnumerable<byte> b) { _b.AddRange(b); return this; }
    }

    byte[] Cmap()
    {
        var map = Glyphs.Select((g, i) => (Cp: g.CodePoint, Gid: i + 1)).Where(x => x.Cp is >= 0 and < 0xFFFF).OrderBy(x => x.Cp).ToList();
        // Segments of consecutive code points with consecutive glyphs (one idDelta each), then the 0xFFFF end segment.
        var segs = new List<(int Start, int End, int Delta)>();
        foreach (var (cp, gid) in map)
        {
            if (segs.Count > 0 && segs[^1].End == cp - 1 && (segs[^1].Delta + cp & 0xFFFF) == gid) segs[^1] = (segs[^1].Start, cp, segs[^1].Delta);
            else segs.Add((cp, cp, (gid - cp) & 0xFFFF));
        }
        segs.Add((0xFFFF, 0xFFFF, 1));
        var n = segs.Count;
        var pow = 1;
        var log = 0;
        while (pow * 2 <= n) { pow *= 2; log++; }
        var sub = new Table(16 + n * 8).U16(4).U16(16 + n * 8).U16(0).U16(n * 2).U16(pow * 2).U16(log).U16(n * 2 - pow * 2);
        foreach (var s in segs) sub.U16(s.End);
        sub.U16(0);
        foreach (var s in segs) sub.U16(s.Start);
        foreach (var s in segs) sub.U16(s.Delta);
        foreach (var _ in segs) sub.U16(0);
        return new Table(12).U16(0).U16(1).U16(3).U16(1).U32(12).Add(sub.Bytes).Bytes;
    }

    byte[] Name()
    {
        (int Id, string Text)[] names = [(1, Family), (2, "Regular"), (4, Family + " Regular"), (6, PostScriptName)];
        var strings = names.Select(n => Encoding.BigEndianUnicode.GetBytes(n.Text)).ToList();
        var t = new Table(64).U16(0).U16(names.Length).U16(6 + names.Length * 12);
        var at = 0;
        for (var i = 0; i < names.Length; i++)
        {
            t.U16(3).U16(1).U16(0x409).U16(names[i].Id).U16(strings[i].Length).U16(at);
            at += strings[i].Length;
        }
        foreach (var s in strings) t.Add(s);
        return t.Bytes;
    }

    public byte[] Write()
    {
        var glyphCount = Glyphs.Count + 1;
        var maxAdvance = Glyphs.Count > 0 ? Glyphs.Max(g => g.Advance) : UnitsPerEm / 2;
        var head = new Table(54).U32(0x00010000).U32(0x00010000).U32(0).U32(0x5F0F3CF5).U16(3).U16(UnitsPerEm)
            .Zero(16)                                                    // created, modified
            .U16(0).U16(Descender & 0xFFFF).U16(maxAdvance).U16(Ascender)  // xMin, yMin, xMax, yMax
            .U16(0).U16(8).U16(2).U16(0).U16(0).Bytes;                   // macStyle, lowestRecPPEM, direction, loca, glyf format
        var hhea = new Table(36).U32(0x00010000).U16(Ascender).U16(Descender & 0xFFFF).U16(0).U16(maxAdvance)
            .U16(0).U16(0).U16(maxAdvance).U16(1).U16(0).U16(0).Zero(8).U16(0).U16(glyphCount).Bytes;
        var maxp = new Table(6).U32(0x00005000).U16(glyphCount).Bytes;
        var hmtx = new Table(glyphCount * 4).U16(UnitsPerEm / 2).U16(0);
        foreach (var g in Glyphs) hmtx.U16(g.Advance).U16(0);
        var cps = Glyphs.Where(g => g.CodePoint >= 0).Select(g => g.CodePoint).DefaultIfEmpty(32).ToList();
        var os2 = new Table(96).U16(4).U16(maxAdvance / 2).U16(400).U16(5).U16(0)
            .Zero(16)                                                  // sub / superscript sizes and offsets
            .U16(50).U16(UnitsPerEm / 4).U16(0)                        // strikeout size and position, family class
            .Zero(10).Zero(16).Add("AGBS"u8.ToArray()).U16(0x40)       // panose, Unicode ranges, vendor, fsSelection REGULAR
            .U16(Math.Min(0xFFFF, cps.Min())).U16(Math.Min(0xFFFF, cps.Max()))
            .U16(Ascender).U16(Descender & 0xFFFF).U16(0).U16(Ascender).U16(-Descender)
            .U32(1).U32(0)                                             // code page Latin 1
            .U16(UnitsPerEm / 2).U16(UnitsPerEm * 7 / 10).U16(0).U16(32).U16(1).Bytes;
        var post = new Table(32).U32(0x00030000).U32(0).U16(-UnitsPerEm / 10 & 0xFFFF).U16(UnitsPerEm / 20).Zero(20).Bytes;
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["CFF "] = Cff(), ["OS/2"] = os2, ["cmap"] = Cmap(), ["head"] = head, ["hhea"] = hhea, ["hmtx"] = hmtx.Bytes,
            ["maxp"] = maxp, ["name"] = Name(), ["post"] = post,
        };
        var pow = 1;
        var log = 0;
        while (pow * 2 <= tables.Count) { pow *= 2; log++; }
        var dir = new Table(12 + tables.Count * 16).U32(0x4F54544F).U16(tables.Count).U16(pow * 16).U16(log).U16(tables.Count * 16 - pow * 16);
        var offset = 12 + tables.Count * 16;
        var body = new List<byte>();
        var headAt = 0;
        foreach (var (tag, data) in tables)
        {
            if (tag == "head") headAt = offset;
            dir.Add(Encoding.ASCII.GetBytes(tag)).U32(Checksum(data)).U32(offset).U32(data.Length);
            body.AddRange(data);
            var pad = (4 - data.Length % 4) % 4;
            body.AddRange(new byte[pad]);
            offset += data.Length + pad;
        }
        var font = dir.Bytes.Concat(body).ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headAt + 8), unchecked(0xB1B0AFBAu - Checksum(font)));
        return font;
    }

    static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (var k = 0; k < 4; k++) word = word << 8 | (i + k < data.Length ? data[i + k] : 0u);
            sum = unchecked(sum + word);
        }
        return sum;
    }
}
