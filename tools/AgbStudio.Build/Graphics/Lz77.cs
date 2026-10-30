namespace AgbStudio.Build.Graphics;

/// <summary>GBA BIOS LZ77 (type 0x10). Output is VRAM-safe: no match references the byte right before it,
/// because LZ77UnCompVram writes 16 bits at a time.</summary>
public static class Lz77
{
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        var o = new List<byte>(data.Length / 2 + 16)
        {
            0x10, (byte)data.Length, (byte)(data.Length >> 8), (byte)(data.Length >> 16),
        };
        var pos = 0;
        // Hash chains of 3-byte prefixes for speed.
        var head = new Dictionary<int, List<int>>();
        while (pos < data.Length)
        {
            var flagIndex = o.Count;
            o.Add(0);
            byte flags = 0;
            for (var bit = 0; bit < 8 && pos < data.Length; bit++)
            {
                int bestLen = 0, bestDist = 0;
                if (pos + 2 < data.Length)
                {
                    var key = data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16);
                    if (head.TryGetValue(key, out var list))
                    {
                        for (var k = list.Count - 1; k >= 0; k--)
                        {
                            var cand = list[k];
                            var dist = pos - cand;
                            if (dist > 4096) break;
                            if (dist < 2) continue;  // VRAM safety
                            var len = 0;
                            while (len < 18 && pos + len < data.Length && data[cand + len] == data[pos + len]) len++;
                            if (len > bestLen)
                            {
                                bestLen = len;
                                bestDist = dist;
                                if (len == 18) break;
                            }
                        }
                    }
                }
                var advance = 1;
                if (bestLen >= 3)
                {
                    flags |= (byte)(0x80 >> bit);
                    var d = bestDist - 1;
                    o.Add((byte)(((bestLen - 3) << 4) | (d >> 8)));
                    o.Add((byte)d);
                    advance = bestLen;
                }
                else
                {
                    o.Add(data[pos]);
                }
                for (var i = 0; i < advance; i++, pos++)
                {
                    if (pos + 2 >= data.Length) continue;
                    var key = data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16);
                    if (!head.TryGetValue(key, out var l)) head[key] = l = new List<int>();
                    l.Add(pos);
                }
            }
            o[flagIndex] = flags;
        }
        while (o.Count % 4 != 0) o.Add(0);
        return o.ToArray();
    }

    public static byte[] Decompress(ReadOnlySpan<byte> src)
    {
        if (src[0] != 0x10) throw new InvalidDataException("Not LZ77 type 0x10 data.");
        var size = src[1] | (src[2] << 8) | (src[3] << 16);
        var o = new byte[size];
        int s = 4, d = 0;
        while (d < size)
        {
            var flags = src[s++];
            for (var bit = 0; bit < 8 && d < size; bit++)
            {
                if ((flags & (0x80 >> bit)) == 0)
                {
                    o[d++] = src[s++];
                    continue;
                }
                var len = (src[s] >> 4) + 3;
                var dist = (((src[s] & 0xF) << 8) | src[s + 1]) + 1;
                s += 2;
                for (var i = 0; i < len && d < size; i++, d++) o[d] = o[d - dist];
            }
        }
        return o;
    }
}
