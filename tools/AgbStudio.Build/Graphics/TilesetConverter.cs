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
}

/// <summary>Converts a tileset image (16x16 editor tiles) or an image layer (8x8 tiles) to tiles, palettes and
/// screen entries. Tiles that use more than 15 colours are reduced; banks are packed automatically.</summary>
public static class TilesetConverter
{
    public static ConvertedTileset Convert(string name, ColorImage img, TilesetMeta meta, IReadOnlyList<string> tagNames, int maxBanks = 13, string file = "")
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
            problems.Add(new ImportProblem(file, $"{reduced.Count} tiles use more than 15 colours; they were reduced.", ["Reduce to 16"]));
        var packed = PaletteBanks.Pack(tileSets, maxBanks);
        if (packed == null)
        {
            // Too many distinct colour sets: reduce the whole image to one bank.
            problems.Add(new ImportProblem(file, $"The tiles need more than {maxBanks} palettes; colours were reduced to fit one palette.", ["Reduce to 16"]));
            var all = Quantizer.Reduce(img.Histogram(), 15).ToHashSet();
            packed = PaletteBanks.Pack(tileSets.Select(_ => all).ToList(), maxBanks)!;
        }
        var (banks, assign) = packed.Value;
        var tiles = new TileSet4();
        var result = new ConvertedTileset { Name = name, Tiles = tiles, Palette = banks, MetatileColumns = mcols };
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
