using AgbStudio.Core.Project;

namespace AgbStudio.Build.Graphics;

public sealed class ConvertedSprite
{
    public required string Name { get; init; }
    public byte[] Tiles { get; init; } = [];        // 4bpp, frames one after another, 1D mapping
    public ushort[] Palette { get; init; } = new ushort[16];
    public int FrameWidth { get; init; }             // padded OAM size
    public int FrameHeight { get; init; }
    public int SourceWidth { get; init; }            // authored frame size
    public int SourceHeight { get; init; }
    public int Shape { get; init; }
    public int SizeCode { get; init; }
    public int TilesPerFrame { get; init; }
    public int FrameCount { get; init; }
    public int ColorsUsed { get; init; }
    public List<ImportProblem> Problems { get; } = new();
}

/// <summary>Sprite sheets: split into frames, reduce to 16 colours, pad to an OAM size, 4bpp 1D tiles.</summary>
public static class SpriteConverter
{
    public static ConvertedSprite Convert(string name, ColorImage img, SpriteMeta meta, ushort[]? sharedPalette = null, string file = "")
    {
        var fw = Math.Min(meta.FrameWidth, img.Width);
        var fh = Math.Min(meta.FrameHeight, img.Height);
        var cols = Math.Max(1, img.Width / fw);
        var rows = Math.Max(1, img.Height / fh);
        var frames = new List<(int X, int Y)>();
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (meta.TrimEmptyFrames && img.IsEmpty(c * fw, r * fh, fw, fh)) continue;
                frames.Add((c * fw, r * fh));
            }
        var (ow, oh) = Gba.FitOam(fw, fh);
        var oam = Gba.OamSize(ow, oh)!.Value;

        // Palette: shared one, or this sheet's colours reduced to 15 (+ transparent).
        var hist = img.Histogram();
        var problems = new List<ImportProblem>();
        ushort[] palette;
        if (sharedPalette != null)
        {
            palette = sharedPalette;
        }
        else
        {
            if (hist.Count > 15 && !meta.ReduceColors)
                problems.Add(new ImportProblem(file,
                    $"Uses {hist.Count} colours. A GBA sprite palette holds 16, so it needs reducing or splitting.",
                    ["Reduce to 16", $"Split into {(hist.Count + 14) / 15} palettes"]));
            var colors = Quantizer.Reduce(hist, 15);
            palette = new ushort[16];
            for (var i = 0; i < colors.Count; i++) palette[i + 1] = colors[i];
        }

        var tilesW = ow / 8;
        var tilesH = oh / 8;
        var data = new byte[frames.Count * tilesW * tilesH * 32];
        var o = 0;
        foreach (var (fx, fy) in frames)
            for (var ty = 0; ty < tilesH; ty++)
                for (var tx = 0; tx < tilesW; tx++)
                    for (var py = 0; py < 8; py++)
                        for (var px = 0; px < 8; px += 2)
                        {
                            byte Index(int x, int y)
                            {
                                if (x >= fw || y >= fh) return 0;
                                var i = (fy + y) * img.Width + fx + x;
                                return img.Transparent[i] ? (byte)0 : (byte)Quantizer.Nearest(palette, img.Colors[i], 1);
                            }
                            var lx = tx * 8 + px;
                            var ly = ty * 8 + py;
                            data[o++] = (byte)(Index(lx, ly) | (Index(lx + 1, ly) << 4));
                        }
        var result = new ConvertedSprite
        {
            Name = name,
            Tiles = data,
            Palette = palette,
            FrameWidth = ow,
            FrameHeight = oh,
            SourceWidth = fw,
            SourceHeight = fh,
            Shape = oam.Shape,
            SizeCode = oam.Size,
            TilesPerFrame = tilesW * tilesH,
            FrameCount = frames.Count,
            ColorsUsed = Math.Min(hist.Count, 15),
        };
        result.Problems.AddRange(problems);
        return result;
    }
}
