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

    /// <summary>A PNG, or an Aseprite file with its frames side by side (AsepriteFile.ToSheet).</summary>
    public static ColorImage Load(string path) => AsepriteFile.IsAseprite(path) ? AsepriteFile.Read(path).ToSheet() : FromPng(Png.Read(path));

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

    /// <summary>A copy that goes on from these tiles (P5.01 secondary tileset): the same tiles at the same indices,
    /// and a tile added later that equals one of them (or a flip of it) is found instead of stored again. Tiles added
    /// with <see cref="AddUnique"/> (animated) are never matched, as in the original.</summary>
    public TileSet4 Extend()
    {
        var t = new TileSet4(false) { Dedupe = Dedupe };
        t._tiles.AddRange(_tiles);
        foreach (var kv in _lookup) t._lookup[kv.Key] = kv.Value;
        return t;
    }

    /// <summary>Adds a tile without deduplication (animated tiles must own their VRAM slot).</summary>
    public int AddUnique(byte[] pixels)
    {
        _tiles.Add(pixels);
        return _tiles.Count - 1;
    }

    public static byte[] Pack(byte[] pixels)
    {
        var o = new byte[32];
        for (var i = 0; i < 32; i++) o[i] = (byte)(pixels[i * 2] | (pixels[i * 2 + 1] << 4));
        return o;
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
    public static (PaletteBanks Banks, int[] Assign)? Pack(IReadOnlyList<HashSet<ushort>> sets, int maxBanks, ushort backdrop = 0) =>
        Pack(sets, maxBanks, null, backdrop);

    /// <summary>As <see cref="Pack(IReadOnlyList{HashSet{ushort}}, int, ushort)"/>, going on from the banks of a primary
    /// tileset (P5.01): those come first, unchanged, and a set whose colours are all in one of them uses it; the other sets
    /// are packed into new banks after them. <paramref name="maxBanks"/> counts the fixed banks too.</summary>
    public static (PaletteBanks Banks, int[] Assign)? Pack(IReadOnlyList<HashSet<ushort>> sets, int maxBanks, PaletteBanks? fixedBanks, ushort backdrop = 0)
    {
        var order = Enumerable.Range(0, sets.Count).OrderByDescending(i => sets[i].Count).ToList();
        var bankSets = new List<HashSet<ushort>>();
        var fixedCount = fixedBanks?.Banks.Count ?? 0;
        if (fixedBanks != null)
            foreach (var b in fixedBanks.Banks) bankSets.Add(b.Skip(1).ToHashSet());
        var assign = new int[sets.Count];
        foreach (var i in order)
        {
            if (sets[i].Count > 15) return null;
            var placed = -1;
            // A fixed bank (the primary's) takes no new colours: it must already hold every colour of the set.
            for (var b = 0; b < fixedCount && placed < 0; b++)
                if (sets[i].IsSubsetOf(bankSets[b])) placed = b;
            // Else the new bank that needs the fewest new colours.
            var bestNew = int.MaxValue;
            for (var b = fixedCount; b < bankSets.Count && (placed < 0 || placed >= fixedCount); b++)
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
            if (placed >= fixedCount) bankSets[placed].UnionWith(sets[i]);
            assign[i] = placed;
        }
        var pb = new PaletteBanks();
        if (fixedBanks != null) pb.Banks.AddRange(fixedBanks.Banks.Select(b => (ushort[])b.Clone()));
        foreach (var s in bankSets.Skip(fixedCount))
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
