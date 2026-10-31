using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace AgbStudio.Build.Fonts;

/// <summary>
/// Outlines of an OpenType font with PostScript outlines: the 'CFF ' table (Adobe Technical Note 5176) with Type 2
/// charstrings (Technical Note 5177). Reads the INDEX and DICT structures, name-keyed and CID-keyed fonts (FDArray /
/// FDSelect, one Private DICT and local subroutines per font DICT), and runs the charstrings: path operators, local
/// and global subroutines, the four flex operators and the arithmetic and storage operators. Hints are skipped (the
/// rasterizer does not hint) and the deprecated <c>seac</c> accent form of endchar draws only the base path. Cubic
/// curves are flattened into polylines in font units, y up, like <see cref="TrueType.Outline"/>.
/// </summary>
sealed class CffOutlines
{
    const int CurveSteps = 8;
    const int MaxStack = 48;
    const int MaxSubrDepth = 10;

    readonly byte[] _d;
    readonly int _end;
    readonly string _name;
    readonly List<(int Start, int End)> _charStrings;
    readonly List<(int Start, int End)> _globalSubrs;
    readonly List<List<(int Start, int End)>> _localSubrs = new();  // per font DICT (one for a name-keyed font)
    readonly byte[]? _fdSelect;                                     // font DICT of each glyph (CID-keyed fonts)
    readonly double _scale = 1;                                     // FontMatrix relative to the font's unitsPerEm

    public int GlyphCount => _charStrings.Count;

    public CffOutlines(byte[] data, int offset, int length, int unitsPerEm, string name)
    {
        _d = data;
        _name = name;
        _end = offset + length;
        if (length < 4 || _end > data.Length) throw Damaged("its CFF table is cut short");
        if (_d[offset] != 1) throw new InvalidDataException($"{name} has CFF outlines of version {_d[offset]}, which AGB Studio does not read. Use a .ttf version of the font, or an .otf with CFF version 1 outlines.");
        var p = offset + _d[offset + 2];  // hdrSize
        ReadIndex(ref p);                 // Name INDEX (one font in an OpenType CFF table)
        var topDicts = ReadIndex(ref p);
        ReadIndex(ref p);                 // String INDEX (glyph names; not needed for drawing)
        _globalSubrs = ReadIndex(ref p);
        if (topDicts.Count == 0) throw Damaged("its CFF table has no font");
        var top = ReadDict(topDicts[0].Start, topDicts[0].End);
        if (top.TryGetValue(1206, out var type) && type.Count > 0 && (int)type[0] != 2)
            throw new InvalidDataException($"{name} uses Type 1 charstrings inside CFF, which AGB Studio does not read. Use a .ttf version of the font.");
        if (!top.TryGetValue(17, out var cs) || cs.Count == 0) throw Damaged("its CFF table has no CharStrings");
        var csAt = offset + (int)cs[0];
        _charStrings = ReadIndex(ref csAt);
        if (top.TryGetValue(1207, out var matrix) && matrix.Count >= 1 && matrix[0] != 0)
        {
            var scale = matrix[0] * unitsPerEm;
            if (Math.Abs(scale - 1) > 1e-9) _scale = scale;
        }
        if (top.ContainsKey(1230))
        {
            // CID-keyed: each glyph's font DICT holds the Private DICT with its local subroutines.
            if (!top.TryGetValue(1236, out var fdArray) || !top.TryGetValue(1237, out var fdSelect)) throw Damaged("its CID font has no FDArray or FDSelect");
            var fa = offset + (int)fdArray[0];
            foreach (var fd in ReadIndex(ref fa)) _localSubrs.Add(LocalSubrs(offset, ReadDict(fd.Start, fd.End)));
            _fdSelect = ReadFdSelect(offset + (int)fdSelect[0], _charStrings.Count);
        }
        else _localSubrs.Add(LocalSubrs(offset, top));
    }

    InvalidDataException Damaged(string what) =>
        new($"{_name} is damaged or incomplete ({what}), so it can't be read. Download the font again, or use another one.");

    int U16(int o)
    {
        if (o + 2 > _end) throw Damaged("its CFF table is cut short");
        return BinaryPrimitives.ReadUInt16BigEndian(_d.AsSpan(o));
    }

    byte U8(int o) => o < _end ? _d[o] : throw Damaged("its CFF table is cut short");

    /// <summary>An INDEX: count, offset size, count + 1 offsets (1-based, from the byte before the data), the data.
    /// Returns where each item's bytes are and moves <paramref name="p"/> past the INDEX.</summary>
    List<(int Start, int End)> ReadIndex(ref int p)
    {
        var count = U16(p);
        var items = new List<(int, int)>(count);
        if (count == 0)
        {
            p += 2;
            return items;
        }
        var offSize = U8(p + 2);
        if (offSize is < 1 or > 4) throw Damaged("a CFF INDEX has a wrong offset size");
        var offsets = p + 3;
        var dataBase = offsets + (count + 1) * offSize - 1;
        int Offset(int i)
        {
            var v = 0;
            for (var k = 0; k < offSize; k++) v = v << 8 | U8(offsets + i * offSize + k);
            return dataBase + v;
        }
        var prev = Offset(0);
        for (var i = 1; i <= count; i++)
        {
            var next = Offset(i);
            if (next < prev || next > _end) throw Damaged("a CFF INDEX points outside the table");
            items.Add((prev, next));
            prev = next;
        }
        p = prev;
        return items;
    }

    /// <summary>A DICT: operands (integers and reals) before each operator; two-byte operators are 1200 + the second byte.</summary>
    Dictionary<int, List<double>> ReadDict(int start, int end)
    {
        var dict = new Dictionary<int, List<double>>();
        var operands = new List<double>();
        var p = start;
        while (p < end)
        {
            int b0 = U8(p);
            if (b0 <= 21)
            {
                var op = b0;
                p++;
                if (b0 == 12) op = 1200 + U8(p++);
                dict[op] = operands;
                operands = new List<double>();
            }
            else if (b0 == 28) { operands.Add((short)(U8(p + 1) << 8 | U8(p + 2))); p += 3; }
            else if (b0 == 29) { operands.Add(U8(p + 1) << 24 | U8(p + 2) << 16 | U8(p + 3) << 8 | U8(p + 4)); p += 5; }
            else if (b0 == 30) operands.Add(ReadReal(ref p));
            else if (b0 is >= 32 and <= 246) { operands.Add(b0 - 139); p++; }
            else if (b0 is >= 247 and <= 250) { operands.Add((b0 - 247) * 256 + U8(p + 1) + 108); p += 2; }
            else if (b0 is >= 251 and <= 254) { operands.Add(-(b0 - 251) * 256 - U8(p + 1) - 108); p += 2; }
            else throw Damaged("a CFF DICT holds a reserved byte");
        }
        return dict;
    }

    // A real number: nibbles 0-9, '.', 'E', 'E-', '-', end (0xF), after the 30 prefix.
    double ReadReal(ref int p)
    {
        var sb = new StringBuilder();
        p++;
        while (true)
        {
            var b = U8(p++);
            foreach (var n in new[] { b >> 4, b & 15 })
            {
                switch (n)
                {
                    case < 10: sb.Append((char)('0' + n)); break;
                    case 0xA: sb.Append('.'); break;
                    case 0xB: sb.Append('E'); break;
                    case 0xC: sb.Append("E-"); break;
                    case 0xE: sb.Append('-'); break;
                    case 0xF:
                        return double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
                }
            }
        }
    }

    List<(int Start, int End)> LocalSubrs(int cff, Dictionary<int, List<double>> fontDict)
    {
        if (!fontDict.TryGetValue(18, out var priv) || priv.Count < 2) return [];
        int size = (int)priv[0], at = cff + (int)priv[1];
        var privateDict = ReadDict(at, at + size);
        if (!privateDict.TryGetValue(19, out var subrs) || subrs.Count == 0) return [];
        var p = at + (int)subrs[0];
        return ReadIndex(ref p);
    }

    byte[] ReadFdSelect(int p, int glyphs)
    {
        var fds = new byte[glyphs];
        switch (U8(p))
        {
            case 0:
                for (var g = 0; g < glyphs; g++) fds[g] = U8(p + 1 + g);
                break;
            case 3:
                var ranges = U16(p + 1);
                for (var r = 0; r < ranges; r++)
                {
                    var at = p + 3 + r * 3;
                    int first = U16(at), fd = U8(at + 2), next = U16(at + 3);
                    for (var g = first; g < next && g < glyphs; g++) fds[g] = (byte)fd;
                }
                break;
            default:
                throw Damaged("its FDSelect has an unknown format");
        }
        return fds;
    }

    static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

    /// <summary>The contours of a glyph as polylines in font units, y up (curves flattened).</summary>
    public List<List<(double X, double Y)>> Outline(int glyph)
    {
        var st = new Charstring(this, glyph);
        if (glyph < 0 || glyph >= _charStrings.Count) return st.Contours;
        var fd = _fdSelect != null ? _fdSelect[glyph] : 0;
        st.Local = fd < _localSubrs.Count ? _localSubrs[fd] : [];
        st.Run(_charStrings[glyph], 0);
        st.Close();
        if (_scale != 1)
            for (var c = 0; c < st.Contours.Count; c++)
                st.Contours[c] = st.Contours[c].Select(pt => (pt.X * _scale, pt.Y * _scale)).ToList();
        return st.Contours;
    }

    /// <summary>The Type 2 charstring interpreter state of one glyph.</summary>
    sealed class Charstring(CffOutlines font, int glyph)
    {
        public List<List<(double X, double Y)>> Contours { get; } = new();
        public List<(int Start, int End)> Local = [];
        readonly double[] _stack = new double[MaxStack];
        readonly double[] _transient = new double[32];
        int _count;
        int _stems;
        bool _widthSeen;
        bool _done;
        double _x, _y;
        List<(double X, double Y)>? _contour;

        InvalidDataException Bad(string what) => font.Damaged($"glyph {glyph}: {what}");

        void Push(double v)
        {
            if (_count >= MaxStack) throw Bad("its charstring overflows the stack");
            _stack[_count++] = v;
        }

        double Pop() => _count > 0 ? _stack[--_count] : throw Bad("its charstring uses more numbers than it pushed");

        // The first stack-clearing operator may carry the advance width as an extra first number.
        int Args(int expected, bool even = false)
        {
            var first = 0;
            if (!_widthSeen)
            {
                _widthSeen = true;
                if (even ? _count % 2 == 1 : _count > expected) first = 1;
            }
            return first;
        }

        public void Close()
        {
            if (_contour is { Count: >= 2 }) Contours.Add(_contour);
            _contour = null;
        }

        void MoveTo(double dx, double dy)
        {
            Close();
            _x += dx;
            _y += dy;
            _contour = [(_x, _y)];
        }

        void LineTo(double dx, double dy)
        {
            _x += dx;
            _y += dy;
            (_contour ??= [(_x - dx, _y - dy)]).Add((_x, _y));
        }

        void CurveTo(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
        {
            double x0 = _x, y0 = _y;
            double x1 = x0 + dx1, y1 = y0 + dy1;
            double x2 = x1 + dx2, y2 = y1 + dy2;
            double x3 = x2 + dx3, y3 = y2 + dy3;
            _contour ??= [(x0, y0)];
            for (var i = 1; i <= CurveSteps; i++)
            {
                var t = (double)i / CurveSteps;
                var u = 1 - t;
                if (i == CurveSteps) _contour.Add((x3, y3));
                else _contour.Add((u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3,
                                   u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3));
            }
            _x = x3;
            _y = y3;
        }

        double S(int i) => _stack[i];

        public void Run((int Start, int End) code, int depth)
        {
            if (depth > MaxSubrDepth) throw Bad("its subroutines nest too deep");
            var d = font._d;
            var p = code.Start;
            while (p < code.End && !_done)
            {
                int b0 = d[p];
                // Numbers.
                if (b0 == 28)
                {
                    if (p + 3 > code.End) throw Bad("its charstring is cut short");
                    Push((short)(d[p + 1] << 8 | d[p + 2]));
                    p += 3;
                    continue;
                }
                if (b0 >= 32)
                {
                    if (b0 <= 246) { Push(b0 - 139); p++; }
                    else if (b0 == 255)
                    {
                        if (p + 5 > code.End) throw Bad("its charstring is cut short");
                        Push(BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(p + 1)) / 65536.0);
                        p += 5;
                    }
                    else
                    {
                        if (p + 2 > code.End) throw Bad("its charstring is cut short");
                        Push(b0 <= 250 ? (b0 - 247) * 256 + d[p + 1] + 108 : -(b0 - 251) * 256 - d[p + 1] - 108);
                        p += 2;
                    }
                    continue;
                }
                p++;
                switch (b0)
                {
                    case 1 or 3 or 18 or 23:  // hstem, vstem, hstemhm, vstemhm
                    {
                        var a = Args(0, even: true);
                        _stems += (_count - a) / 2;
                        _count = 0;
                        break;
                    }
                    case 19 or 20:  // hintmask, cntrmask: numbers before it are vstems; then the mask bytes
                    {
                        var a = Args(0, even: true);
                        _stems += (_count - a) / 2;
                        _count = 0;
                        p += (_stems + 7) / 8;
                        break;
                    }
                    case 21:  // rmoveto
                    {
                        var a = Args(2);
                        if (_count - a < 2) throw Bad("rmoveto needs two numbers");
                        MoveTo(S(a), S(a + 1));
                        _count = 0;
                        break;
                    }
                    case 22:  // hmoveto
                    {
                        var a = Args(1);
                        if (_count - a < 1) throw Bad("hmoveto needs a number");
                        MoveTo(S(a), 0);
                        _count = 0;
                        break;
                    }
                    case 4:  // vmoveto
                    {
                        var a = Args(1);
                        if (_count - a < 1) throw Bad("vmoveto needs a number");
                        MoveTo(0, S(a));
                        _count = 0;
                        break;
                    }
                    case 5:  // rlineto
                        for (var i = 0; i + 1 < _count; i += 2) LineTo(S(i), S(i + 1));
                        _count = 0;
                        break;
                    case 6 or 7:  // hlineto, vlineto: alternate horizontal and vertical lines
                    {
                        var horizontal = b0 == 6;
                        for (var i = 0; i < _count; i++, horizontal = !horizontal)
                            if (horizontal) LineTo(S(i), 0);
                            else LineTo(0, S(i));
                        _count = 0;
                        break;
                    }
                    case 8:  // rrcurveto
                        for (var i = 0; i + 5 < _count; i += 6) CurveTo(S(i), S(i + 1), S(i + 2), S(i + 3), S(i + 4), S(i + 5));
                        _count = 0;
                        break;
                    case 24:  // rcurveline: curves, then a line
                    {
                        var i = 0;
                        for (; i + 6 <= _count - 2; i += 6) CurveTo(S(i), S(i + 1), S(i + 2), S(i + 3), S(i + 4), S(i + 5));
                        if (i + 1 < _count) LineTo(S(i), S(i + 1));
                        _count = 0;
                        break;
                    }
                    case 25:  // rlinecurve: lines, then a curve
                    {
                        var i = 0;
                        for (; i + 2 <= _count - 6; i += 2) LineTo(S(i), S(i + 1));
                        if (i + 5 < _count) CurveTo(S(i), S(i + 1), S(i + 2), S(i + 3), S(i + 4), S(i + 5));
                        _count = 0;
                        break;
                    }
                    case 26:  // vvcurveto: dx1? {dya dxb dyb dyc}+
                    {
                        var i = 0;
                        double dx1 = 0;
                        if (_count % 4 == 1) dx1 = S(i++);
                        for (; i + 3 < _count; i += 4, dx1 = 0) CurveTo(dx1, S(i), S(i + 1), S(i + 2), 0, S(i + 3));
                        _count = 0;
                        break;
                    }
                    case 27:  // hhcurveto: dy1? {dxa dxb dyb dxc}+
                    {
                        var i = 0;
                        double dy1 = 0;
                        if (_count % 4 == 1) dy1 = S(i++);
                        for (; i + 3 < _count; i += 4, dy1 = 0) CurveTo(S(i), dy1, S(i + 1), S(i + 2), S(i + 3), 0);
                        _count = 0;
                        break;
                    }
                    case 30 or 31:  // vhcurveto, hvcurveto: curves that start vertical / horizontal, alternating
                    {
                        var horizontal = b0 == 31;
                        for (var i = 0; i + 3 < _count; horizontal = !horizontal)
                        {
                            var last = _count - i == 5 ? S(i + 4) : 0;
                            if (horizontal) CurveTo(S(i), 0, S(i + 1), S(i + 2), last, S(i + 3));
                            else CurveTo(0, S(i), S(i + 1), S(i + 2), S(i + 3), last);
                            i += _count - i == 5 ? 5 : 4;
                        }
                        _count = 0;
                        break;
                    }
                    case 10 or 29:  // callsubr, callgsubr
                    {
                        var subrs = b0 == 10 ? Local : font._globalSubrs;
                        var index = (int)Pop() + Bias(subrs.Count);
                        if (index < 0 || index >= subrs.Count) throw Bad($"it calls subroutine {index}, which does not exist");
                        Run(subrs[index], depth + 1);
                        break;
                    }
                    case 11:  // return
                        return;
                    case 14:  // endchar (the seac accent form draws only the base path here)
                        Args(0);
                        Close();
                        _count = 0;
                        _done = true;
                        return;
                    case 12:
                        if (p >= code.End) throw Bad("its charstring is cut short");
                        Escape(d[p++]);
                        break;
                    default:  // reserved operators
                        _count = 0;
                        break;
                }
            }
        }

        void Escape(int op)
        {
            switch (op)
            {
                case 35:  // flex: two curves, then the flex depth
                    Need(13);
                    CurveTo(S(0), S(1), S(2), S(3), S(4), S(5));
                    CurveTo(S(6), S(7), S(8), S(9), S(10), S(11));
                    _count = 0;
                    return;
                case 34:  // hflex: dx1 dx2 dy2 dx3 dx4 dx5 dx6, back to the starting height
                {
                    Need(7);
                    CurveTo(S(0), 0, S(1), S(2), S(3), 0);
                    CurveTo(S(4), 0, S(5), -S(2), S(6), 0);
                    _count = 0;
                    return;
                }
                case 36:  // hflex1: dx1 dy1 dx2 dy2 dx3 dx4 dx5 dy5 dx6
                {
                    Need(9);
                    var y0 = _y;
                    CurveTo(S(0), S(1), S(2), S(3), S(4), 0);
                    CurveTo(S(5), 0, S(6), S(7), S(8), y0 - (_y + S(7)));
                    _count = 0;
                    return;
                }
                case 37:  // flex1: five points, the sixth on the longer axis, back to the start on the other
                {
                    Need(11);
                    double x0 = _x, y0 = _y;
                    var dx = S(0) + S(2) + S(4) + S(6) + S(8);
                    var dy = S(1) + S(3) + S(5) + S(7) + S(9);
                    CurveTo(S(0), S(1), S(2), S(3), S(4), S(5));
                    double ex, ey;
                    if (Math.Abs(dx) > Math.Abs(dy)) { ex = S(10); ey = y0 - (_y + S(7) + S(9)); }
                    else { ex = x0 - (_x + S(6) + S(8)); ey = S(10); }
                    CurveTo(S(6), S(7), S(8), S(9), ex, ey);
                    _count = 0;
                    return;
                }
                // Arithmetic and storage (rare in fonts, but part of Type 2).
                case 3: { var b = Pop(); var a = Pop(); Push(a != 0 && b != 0 ? 1 : 0); return; }  // and
                case 4: { var b = Pop(); var a = Pop(); Push(a != 0 || b != 0 ? 1 : 0); return; }  // or
                case 5: Push(Pop() == 0 ? 1 : 0); return;                                          // not
                case 9: Push(Math.Abs(Pop())); return;                                             // abs
                case 10: { var b = Pop(); var a = Pop(); Push(a + b); return; }                    // add
                case 11: { var b = Pop(); var a = Pop(); Push(a - b); return; }                    // sub
                case 12: { var b = Pop(); var a = Pop(); Push(b == 0 ? 0 : a / b); return; }       // div
                case 14: Push(-Pop()); return;                                                     // neg
                case 15: { var b = Pop(); var a = Pop(); Push(a == b ? 1 : 0); return; }           // eq
                case 18: Pop(); return;                                                            // drop
                case 20: { var i = (int)Pop(); var v = Pop(); if (i is >= 0 and < 32) _transient[i] = v; return; }  // put
                case 21: { var i = (int)Pop(); Push(i is >= 0 and < 32 ? _transient[i] : 0); return; }             // get
                case 22: { var v2 = Pop(); var v1 = Pop(); var s2 = Pop(); var s1 = Pop(); Push(v1 <= v2 ? s1 : s2); return; }  // ifelse
                case 23: Push(0.5); return;                                                        // random: any value in (0, 1]
                case 24: { var b = Pop(); var a = Pop(); Push(a * b); return; }                    // mul
                case 26: Push(Math.Sqrt(Math.Max(0, Pop()))); return;                              // sqrt
                case 27: { var a = Pop(); Push(a); Push(a); return; }                              // dup
                case 28: { var b = Pop(); var a = Pop(); Push(b); Push(a); return; }               // exch
                case 29:                                                                            // index
                {
                    var i = (int)Pop();
                    if (_count == 0) throw Bad("index on an empty stack");
                    Push(_stack[_count - 1 - Math.Clamp(i < 0 ? 0 : i, 0, _count - 1)]);
                    return;
                }
                case 30:                                                                            // roll
                {
                    var j = (int)Pop();
                    var n = (int)Pop();
                    if (n <= 0 || n > _count) return;
                    var top = _stack.AsSpan(_count - n, n).ToArray();
                    for (var k = 0; k < n; k++) _stack[_count - n + ((k + j) % n + n) % n] = top[k];
                    return;
                }
                default:  // dotsection and reserved: clear the stack
                    _count = 0;
                    return;
            }
        }

        void Need(int n)
        {
            if (_count < n) throw Bad("a flex operator has too few numbers");
        }
    }
}
