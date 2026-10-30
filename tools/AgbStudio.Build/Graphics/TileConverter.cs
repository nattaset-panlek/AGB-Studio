using AgbStudio.Core;

namespace AgbStudio.Build.Graphics;

/// <summary>A problem found while importing, phrased for the Import Asset · Errors dialog.</summary>
public sealed record ImportProblem(string File, string Message, IReadOnlyList<string> Fixes);

/// <summary>An image as BGR555 colours with a transparency mask.</summary>
public sealed class ColorImage
{
    public int Width { get; }
    public int Height { get; }
    public ushort[] Colors { get; }
    public bool[] Transparent { get; }

    public ColorImage(int w, int h)
    {
        Width = w;
        Height = h;
        Colors = new ushort[w * h];
        Transparent = new bool[w * h];
    }

    public static ColorImage FromPng(Png.Image png)
    {
        var img = new ColorImage(png.Width, png.Height);
        for (var i = 0; i < png.Pixels.Length; i++)
        {
            img.Transparent[i] = Gba.IsTransparent(png.Pixels[i]);
            img.Colors[i] = Gba.ToBgr555(png.Pixels[i]);
        }
        return img;
    }

    public static ColorImage Load(string path) => FromPng(Png.Read(path));

    public Dictionary<ushort, int> Histogram(int x0 = 0, int y0 = 0, int w = -1, int h = -1)
    {
        if (w < 0) w = Width;
        if (h < 0) h = Height;
        var hist = new Dictionary<ushort, int>();
        for (var y = y0; y < y0 + h && y < Height; y++)
            for (var x = x0; x < x0 + w && x < Width; x++)
            {
                var i = y * Width + x;
                if (Transparent[i]) continue;
                hist[Colors[i]] = hist.GetValueOrDefault(Colors[i]) + 1;
            }
        return hist;
    }

    public bool IsEmpty(int x0, int y0, int w, int h)
    {
        for (var y = y0; y < y0 + h && y < Height; y++)
            for (var x = x0; x < x0 + w && x < Width; x++)
                if (!Transparent[y * Width + x]) return false;
        return true;
    }
}

/// <summary>8x8 4bpp tiles with deduplication including flipped copies (Tilemap "Unique tiles").</summary>
public sealed class TileSet4
{
    readonly List<byte[]> _tiles = new();
    readonly Dictionary<string, (int Index, bool H, bool V)> _lookup = new();

    public IReadOnlyList<byte[]> Tiles => _tiles;  // 64 indices (0..15) per tile
    public int Count => _tiles.Count;
    public bool Dedupe { get; init; } = true;

    public TileSet4(bool reserveEmpty = true)
    {
        if (reserveEmpty) Add(new byte[64]);
    }

    /// <summary>Adds a tile (64 palette indices) or finds an existing one. Returns index and flips.</summary>
    public (int Index, bool H, bool V) Add(byte[] pixels)
    {
        if (Dedupe)
        {
            if (_lookup.TryGetValue(Key(pixels), out var hit)) return hit;
        }
        var index = _tiles.Count;
        _tiles.Add(pixels);
        if (Dedupe)
        {
            _lookup.TryAdd(Key(pixels), (index, false, false));
            _lookup.TryAdd(Key(Flip(pixels, true, false)), (index, true, false));
            _lookup.TryAdd(Key(Flip(pixels, false, true)), (index, false, true));
            _lookup.TryAdd(Key(Flip(pixels, true, true)), (index, true, true));
        }
        return (index, false, false);
    }

    static string Key(byte[] p) => Convert.ToHexString(p);

    public static byte[] Flip(byte[] p, bool h, bool v)
    {
        var o = new byte[64];
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
                o[y * 8 + x] = p[(v ? 7 - y : y) * 8 + (h ? 7 - x : x)];
        return o;
    }

    /// <summary>Packed 4bpp data, 32 bytes per tile, low nibble = left pixel.</summary>
    public byte[] ToBytes()
    {
        var o = new byte[_tiles.Count * 32];
        for (var t = 0; t < _tiles.Count; t++)
            for (var i = 0; i < 32; i++)
                o[t * 32 + i] = (byte)(_tiles[t][i * 2] | (_tiles[t][i * 2 + 1] << 4));
        return o;
    }
}

/// <summary>Palettes of 16 colours (index 0 transparent / backdrop) assigned to groups of tiles.</summary>
public sealed class PaletteBanks
{
    public List<ushort[]> Banks { get; } = new();

    /// <summary>Packs colour sets (one per tile) into as few 15-colour banks as possible (first fit, biggest first).
    /// Returns the bank of each set, or null when more than <paramref name="maxBanks"/> banks are needed.</summary>
    public static (PaletteBanks Banks, int[] Assign)? Pack(IReadOnlyList<HashSet<ushort>> sets, int maxBanks, ushort backdrop = 0)
    {
        var order = Enumerable.Range(0, sets.Count).OrderByDescending(i => sets[i].Count).ToList();
        var bankSets = new List<HashSet<ushort>>();
        var assign = new int[sets.Count];
        foreach (var i in order)
        {
            if (sets[i].Count > 15) return null;
            var placed = -1;
            // Prefer the bank that needs the fewest new colours.
            var bestNew = int.MaxValue;
            for (var b = 0; b < bankSets.Count; b++)
            {
                var newColors = sets[i].Count(c => !bankSets[b].Contains(c));
                if (bankSets[b].Count + newColors <= 15 && newColors < bestNew)
                {
                    bestNew = newColors;
                    placed = b;
                }
            }
            if (placed < 0)
            {
                if (bankSets.Count >= maxBanks) return null;
                bankSets.Add(new HashSet<ushort>());
                placed = bankSets.Count - 1;
            }
            bankSets[placed].UnionWith(sets[i]);
            assign[i] = placed;
        }
        var pb = new PaletteBanks();
        foreach (var s in bankSets)
        {
            var bank = new ushort[16];
            bank[0] = backdrop;
            var k = 1;
            foreach (var c in s.OrderBy(c => c)) bank[k++] = c;
            pb.Banks.Add(bank);
        }
        return (pb, assign);
    }

    public byte IndexOf(int bank, ushort color)
    {
        var b = Banks[bank];
        for (var i = 1; i < 16; i++)
            if (b[i] == color) return (byte)i;
        return (byte)Quantizer.Nearest(b, color, 1);
    }

    public ushort[] Flatten() => Banks.SelectMany(b => b).ToArray();
}
