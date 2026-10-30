namespace AgbStudio.Emu;

/// <summary>Animated GIF encoder for Tools › Screenshot / GIF (game frames are 15-bit colour, so a frame rarely has
/// more than 256 colours; extra colours map to the nearest palette entry).</summary>
public sealed class GifWriter
{
    readonly int _w, _h;
    readonly List<(byte[] Indices, uint[] Palette, int DelayCs)> _frames = new();

    public GifWriter(int width, int height)
    {
        _w = width;
        _h = height;
    }

    public int FrameCount => _frames.Count;

    public void AddFrame(uint[] argb, int delayCentiseconds = 2)
    {
        var palette = new List<uint>();
        var lookup = new Dictionary<uint, byte>();
        var idx = new byte[argb.Length];
        for (var i = 0; i < argb.Length; i++)
        {
            var c = argb[i] & 0xFFFFFF;
            if (!lookup.TryGetValue(c, out var k))
            {
                if (palette.Count < 256)
                {
                    k = (byte)palette.Count;
                    palette.Add(c);
                }
                else
                {
                    k = (byte)Nearest(palette, c);
                }
                lookup[c] = k;
            }
            idx[i] = k;
        }
        while (palette.Count < 2) palette.Add(0);
        _frames.Add((idx, palette.ToArray(), delayCentiseconds));
    }

    static int Nearest(List<uint> pal, uint c)
    {
        var best = 0;
        var bestD = int.MaxValue;
        for (var i = 0; i < pal.Count; i++)
        {
            int dr = (int)((pal[i] >> 16) & 0xFF) - (int)((c >> 16) & 0xFF), dg = (int)((pal[i] >> 8) & 0xFF) - (int)((c >> 8) & 0xFF), db = (int)(pal[i] & 0xFF) - (int)(c & 0xFF);
            var d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    public byte[] Encode()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("GIF89a"u8);
        w.Write((ushort)_w);
        w.Write((ushort)_h);
        w.Write((byte)0x70);  // no global colour table, 8-bit colour resolution
        w.Write((byte)0);
        w.Write((byte)0);
        // Loop forever (NETSCAPE2.0)
        w.Write([0x21, 0xFF, 0x0B]);
        w.Write("NETSCAPE2.0"u8);
        w.Write([0x03, 0x01, 0x00, 0x00, 0x00]);
        foreach (var (indices, palette, delay) in _frames)
        {
            w.Write([0x21, 0xF9, 0x04, 0x00]);
            w.Write((ushort)delay);
            w.Write([0x00, 0x00]);
            var bits = 1;
            while ((1 << bits) < palette.Length) bits++;
            w.Write((byte)0x2C);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)_w);
            w.Write((ushort)_h);
            w.Write((byte)(0x80 | (bits - 1)));
            for (var i = 0; i < 1 << bits; i++)
            {
                var c = i < palette.Length ? palette[i] : 0;
                w.Write((byte)(c >> 16));
                w.Write((byte)(c >> 8));
                w.Write((byte)c);
            }
            var minCode = Math.Max(2, bits);
            w.Write((byte)minCode);
            var data = Lzw(indices, minCode);
            for (var i = 0; i < data.Length; i += 255)
            {
                var n = Math.Min(255, data.Length - i);
                w.Write((byte)n);
                w.Write(data, i, n);
            }
            w.Write((byte)0);
        }
        w.Write((byte)0x3B);
        return ms.ToArray();
    }

    static byte[] Lzw(byte[] pixels, int minCode)
    {
        var output = new List<byte>();
        int clear = 1 << minCode, end = clear + 1, codeSize = minCode + 1, next = end + 1;
        var dict = new Dictionary<(int, byte), int>();
        int bitBuf = 0, bitCount = 0;
        void Emit(int code)
        {
            bitBuf |= code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                output.Add((byte)bitBuf);
                bitBuf >>= 8;
                bitCount -= 8;
            }
        }
        Emit(clear);
        var prefix = -1;
        foreach (var p in pixels)
        {
            if (prefix < 0) { prefix = p; continue; }
            if (dict.TryGetValue((prefix, p), out var code)) { prefix = code; continue; }
            Emit(prefix);
            if (next < 4096)
            {
                dict[(prefix, p)] = next++;
                if (next > (1 << codeSize) && codeSize < 12) codeSize++;
            }
            else
            {
                Emit(clear);
                dict.Clear();
                codeSize = minCode + 1;
                next = end + 1;
            }
            prefix = p;
        }
        if (prefix >= 0) Emit(prefix);
        Emit(end);
        if (bitCount > 0) output.Add((byte)bitBuf);
        return output.ToArray();
    }
}
