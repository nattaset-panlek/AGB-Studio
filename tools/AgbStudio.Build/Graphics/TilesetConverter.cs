using AgbStudio.Core.Project;

namespace AgbStudio.Build.Graphics;

public sealed class ConvertedTileset
{
    public required string Name { get; init; }
    public TileSet4 Tiles { get; init; } = new();
    public PaletteBanks Palette { get; init; } = new();
    public List<ushort[]> Metatiles { get; } = new();   // 4 screen entries each, relative to the tileset
    public List<CollisionType> Collision { get; } = new();
    public List<byte> Tags { get; } = new();
    public List<ImportProblem> Problems { get; } = new();
    public int MetatileColumns { get; init; }
    public List<(int BaseTile, byte[] FrameTiles, int Frames, int Ticks)> Anims { get; } = new();

    /// <summary>P5.01: the primary tileset this one extends, or null. <see cref="Tiles"/> and <see cref="Palette"/> then
    /// hold the primary's tiles and banks first, at the same indices, and this tileset's own after them; the metatiles'
    /// screen entries count from the primary's first tile and bank, as the runtime places them (bg::loadTileset).</summary>
    public ConvertedTileset? Primary { get; init; }
    /// <summary>The first tile and bank that belong to this tileset (0 without a primary): the ROM stores only these.</summary>
    public int OwnTileStart => Primary?.Tiles.Count ?? 0;
    public int OwnBankStart => Primary?.Palette.Banks.Count ?? 0;
    public int OwnTileCount => Tiles.Count - OwnTileStart;
    public int OwnBankCount => Palette.Banks.Count - OwnBankStart;

    /// <summary>Packed 4bpp data of this tileset's own tiles (all of them without a primary).</summary>
    public byte[] OwnTileBytes() => Tiles.ToBytes()[(OwnTileStart * 32)..];

    /// <summary>The colours of this tileset's own banks, 16 per bank.</summary>
    public ushort[] OwnColors() => Palette.Banks.Skip(OwnBankStart).SelectMany(b => b).ToArray();
}

/// <summary>Converts a tileset image (16x16 editor tiles) or an image layer (8x8 tiles) to tiles, palettes and
/// screen entries. Tiles that use more than 15 colours are reduced; banks are packed automatically.</summary>
public static class TilesetConverter
{
    /// <summary>Converts a tileset. With a <paramref name="primary"/> (P5.01, TilesetMeta.Primary) the tiles go on from
    /// the primary's: a tile the primary has (or a flip of it) in a bank of the primary is not stored again, and new
    /// colours go into banks after the primary's (<paramref name="maxBanks"/> counts both).</summary>
    public static ConvertedTileset Convert(string name, ColorImage img, TilesetMeta meta, IReadOnlyList<string> tagNames, int maxBanks = 13, string file = "",
        ConvertedTileset? primary = null)
    {
        var mcols = img.Width / 16;
        var mrows = img.Height / 16;
        var tileSets = new List<HashSet<ushort>>();
        var problems = new List<ImportProblem>();
        var reduced = new Dictionary<int, List<ushort>>();
        // Colour set of every 8x8 tile in reading order of metatiles (TL, TR, BL, BR).
        var tileOrigins = new List<(int X, int Y)>();
        for (var my = 0; my < mrows; my++)
            for (var mx = 0; mx < mcols; mx++)
                for (var q = 0; q < 4; q++)
                {
                    var x = mx * 16 + (q & 1) * 8;
                    var y = my * 16 + (q >> 1) * 8;
                    tileOrigins.Add((x, y));
                    var hist = img.Histogram(x, y, 8, 8);
                    if (hist.Count > 15)
                    {
                        var r = Quantizer.Reduce(hist, 15);
                        reduced[tileOrigins.Count - 1] = r;
                        tileSets.Add(r.ToHashSet());
                    }
                    else
                    {
                        tileSets.Add(hist.Keys.ToHashSet());
                    }
                }
        if (reduced.Count > 0)
            problems.Add(new ImportProblem(file, $"{reduced.Count} tiles use more than 15 colours (a GBA tile has 15 plus transparent), so their colours were reduced. Check them, or redraw them with fewer colours.", ["Reduce to 16"]));
        var packed = PaletteBanks.Pack(tileSets, maxBanks, primary?.Palette);
        if (packed == null)
        {
            // Too many distinct colour sets: reduce the whole image to one bank.
            problems.Add(new ImportProblem(file, $"The tiles need more than the {maxBanks} free palettes, so all colours were reduced to one palette of 16. Use fewer different colour sets, or split the tileset.", ["Reduce to 16"]));
            var all = Quantizer.Reduce(img.Histogram(), 15).ToHashSet();
            packed = PaletteBanks.Pack(tileSets.Select(_ => all).ToList(), Math.Max(maxBanks, (primary?.Palette.Banks.Count ?? 0) + 1), primary?.Palette)!;
        }
        var (banks, assign) = packed.Value;
        var tiles = primary?.Tiles.Extend() ?? new TileSet4();
        var result = new ConvertedTileset { Name = name, Tiles = tiles, Palette = banks, MetatileColumns = mcols, Primary = primary };
        result.Problems.AddRange(problems);
        for (var m = 0; m < mcols * mrows; m++)
        {
            var se = new ushort[4];
            for (var q = 0; q < 4; q++)
            {
                var ti = m * 4 + q;
                var (ox, oy) = tileOrigins[ti];
                var bank = assign[ti];
                var px = new byte[64];
                for (var y = 0; y < 8; y++)
                    for (var x = 0; x < 8; x++)
                    {
                        var i = (oy + y) * img.Width + ox + x;
                        px[y * 8 + x] = img.Transparent[i] ? (byte)0 : banks.IndexOf(bank, img.Colors[i]);
                    }
                var (idx, h, v) = tiles.Add(px);
                se[q] = (ushort)(idx | (h ? 1 << 10 : 0) | (v ? 1 << 11 : 0) | (bank << 12));
            }
            result.Metatiles.Add(se);
            var props = meta.Tiles.GetValueOrDefault(m);
            if (props?.Animation is { Count: > 1 } frames)
            {
                // Four unique tiles for the base, then each frame's tiles converted with the base tile's bank.
                byte[] TilePixels(int metatile, int q, int bank)
                {
                    var (ox, oy) = tileOrigins[metatile * 4 + q];
                    var px = new byte[64];
                    for (var y = 0; y < 8; y++)
                        for (var x = 0; x < 8; x++)
                        {
                            var i = (oy + y) * img.Width + ox + x;
                            px[y * 8 + x] = img.Transparent[i] ? (byte)0 : banks.IndexOf(bank, img.Colors[i]);
                        }
                    return px;
                }
                var baseTile = -1;
                for (var q = 0; q < 4; q++)
                {
                    var bank = se[q] >> 12;
                    var t = tiles.AddUnique(TilePixels(m, q, bank));
                    if (q == 0) baseTile = t;
                    se[q] = (ushort)(t | (bank << 12));
                }
                var data = new List<byte>();
                foreach (var f in frames.Where(f => f >= 0 && f < mcols * mrows))
                    for (var q = 0; q < 4; q++) data.AddRange(TileSet4.Pack(TilePixels(f, q, se[q] >> 12)));
                result.Anims.Add((baseTile, data.ToArray(), data.Count / 128, Math.Max(1, props.AnimationTicks)));
            }
            result.Collision.Add(props?.Collision ?? CollisionType.None);
            var tag = props == null || props.Tag == "" ? 0 : tagNames.ToList().IndexOf(props.Tag) + 1;
            result.Tags.Add((byte)Math.Max(0, tag));
        }
        return result;
    }

    /// <summary>Image layer (sky, title art): 8x8 tiles straight to screen entries.</summary>
    public static (ConvertedTileset Tiles, ushort[] Cells, int WidthTiles, int HeightTiles) ConvertImage(string name, ColorImage img, int maxBanks = 13)
    {
        var tw = img.Width / 8;
        var th = img.Height / 8;
        var sets = new List<HashSet<ushort>>();
        for (var y = 0; y < th; y++)
            for (var x = 0; x < tw; x++)
            {
                var h = img.Histogram(x * 8, y * 8, 8, 8);
                sets.Add((h.Count > 15 ? Quantizer.Reduce(h, 15) : h.Keys.ToList()).ToHashSet());
            }
        var packed = PaletteBanks.Pack(sets, maxBanks) ?? PaletteBanks.Pack(sets.Select(_ => Quantizer.Reduce(img.Histogram(), 15).ToHashSet()).ToList(), maxBanks)!.Value;
        var (banks, assign) = packed;
        var tiles = new TileSet4();
        var cells = new ushort[tw * th];
        for (var y = 0; y < th; y++)
            for (var x = 0; x < tw; x++)
            {
                var ti = y * tw + x;
                var px = new byte[64];
                for (var py = 0; py < 8; py++)
                    for (var pxx = 0; pxx < 8; pxx++)
                    {
                        var i = (y * 8 + py) * img.Width + x * 8 + pxx;
                        px[py * 8 + pxx] = img.Transparent[i] ? (byte)0 : banks.IndexOf(assign[ti], img.Colors[i]);
                    }
                var (idx, h, v) = tiles.Add(px);
                cells[ti] = (ushort)(idx | (h ? 1 << 10 : 0) | (v ? 1 << 11 : 0) | (assign[ti] << 12));
            }
        return (new ConvertedTileset { Name = name, Tiles = tiles, Palette = banks }, cells, tw, th);
    }
}
