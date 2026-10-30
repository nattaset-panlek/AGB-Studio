using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

public sealed class GraphicsTests
{
    [Fact]
    public void ColorsUseNearestOf32Levels()
    {
        Assert.Equal((ushort)0x7FFF, Gba.ToBgr555(0xFFFFFFFF));
        Assert.Equal((ushort)0, Gba.ToBgr555(0xFF000000));
        // #FF4F6D -> R 31, G 10 (79 * 31 / 255 = 9.6), B 13
        var c = Gba.ToBgr555(0xFFFF4F6D);
        Assert.Equal(31, c & 31);
        Assert.Equal(10, (c >> 5) & 31);
        Assert.Equal(13, (c >> 10) & 31);
    }

    [Theory]
    [InlineData(16, 12, 16, 16)]
    [InlineData(8, 8, 8, 8)]
    [InlineData(24, 24, 32, 32)]
    [InlineData(32, 10, 32, 16)]
    [InlineData(7, 30, 8, 32)]
    public void FramesPadToTheSmallestOamSize(int w, int h, int ow, int oh) => Assert.Equal((ow, oh), Gba.FitOam(w, h));

    [Fact]
    public void AllTwelveOamSizesAreKnown()
    {
        Assert.Equal(12, Gba.OamSizes.Length);
        foreach (var (w, h) in Gba.OamSizes) Assert.NotNull(Gba.OamSize(w, h));
        Assert.Null(Gba.OamSize(16, 12));
    }

    [Fact]
    public void Lz77RoundTripsAndIsVramSafe()
    {
        var rng = new Random(5);
        foreach (var len in new[] { 0, 1, 7, 64, 1000, 20000 })
        {
            var data = new byte[len];
            for (var i = 0; i < len; i++) data[i] = (byte)(i % 37 < 20 ? i % 7 : rng.Next(256));
            var packed = Lz77.Compress(data);
            Assert.Equal(0, packed.Length % 4);
            Assert.Equal(data, Lz77.Decompress(packed));
            // No back reference of distance 1 (VRAM writes 16 bits at a time).
            var s = 4;
            while (s < packed.Length)
            {
                var flags = packed[s++];
                for (var bit = 0; bit < 8 && s < packed.Length; bit++)
                {
                    if ((flags & (0x80 >> bit)) == 0) { s++; continue; }
                    var dist = (((packed[s] & 0xF) << 8) | packed[s + 1]) + 1;
                    Assert.True(dist >= 2);
                    s += 2;
                }
            }
        }
        var zeros = new byte[4096];
        Assert.True(Lz77.Compress(zeros).Length < 600);
    }

    [Fact]
    public void QuantizerReducesTo15()
    {
        var hist = new Dictionary<ushort, int>();
        for (var i = 0; i < 48; i++) hist[(ushort)(i * 683 & 0x7FFF)] = 1 + i;
        var reduced = Quantizer.Reduce(hist, 15);
        Assert.True(reduced.Count <= 15);
        Assert.True(reduced.Count >= 10);
    }

    [Fact]
    public void TilesDedupeWithFlips()
    {
        var set = new TileSet4();
        var a = new byte[64];
        for (var i = 0; i < 64; i++) a[i] = (byte)(1 + i * 7 % 15);  // no symmetry
        var first = set.Add(a);
        Assert.Equal((1, false, false), first);
        Assert.Equal((1, true, false), set.Add(TileSet4.Flip(a, true, false)));
        Assert.Equal((1, true, true), set.Add(TileSet4.Flip(a, true, true)));
        Assert.Equal(2, set.Count);  // empty tile + one unique
        var bytes = set.ToBytes();
        Assert.Equal(64, bytes.Length);
        Assert.Equal((byte)(a[0] | (a[1] << 4)), bytes[32]);  // low nibble = left pixel
    }

    [Fact]
    public void PalettesPackIntoBanks()
    {
        var sets = new List<HashSet<ushort>>
        {
            new() { 1, 2, 3 }, new() { 2, 3, 4 }, new() { 100, 101 },
            Enumerable.Range(200, 14).Select(i => (ushort)i).ToHashSet(),
        };
        var r = PaletteBanks.Pack(sets, 16)!.Value;
        Assert.Equal(r.Assign[0], r.Assign[1]);  // share a bank
        Assert.True(r.Banks.Banks.Count <= 2);
        Assert.Null(PaletteBanks.Pack([Enumerable.Range(0, 16).Select(i => (ushort)i).ToHashSet()], 16));
    }

    static ColorImage Checker(int w, int h, int colors)
    {
        var img = new ColorImage(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                img.Colors[i] = (ushort)((i % colors) * 2 + 1);
                img.Transparent[i] = x == 0 && y == 0;
            }
        return img;
    }

    [Fact]
    public void SpriteSheetSplitsPadsAndReportsTooManyColors()
    {
        var img = Checker(64, 12, 5);  // four 16x12 frames
        var s = SpriteConverter.Convert("slime", img, new SpriteMeta { FrameWidth = 16, FrameHeight = 12 });
        Assert.Equal(4, s.FrameCount);
        Assert.Equal((16, 16), (s.FrameWidth, s.FrameHeight));
        Assert.Equal(4, s.TilesPerFrame);
        Assert.Equal(4 * 4 * 32, s.Tiles.Length);
        Assert.Empty(s.Problems);
        var big = Checker(16, 16, 48);
        var bad = SpriteConverter.Convert("hero_big", big, new SpriteMeta { ReduceColors = false }, null, "hero_big.png");
        var p = Assert.Single(bad.Problems);
        Assert.Equal("Uses 48 colours. A GBA sprite palette holds 16, so it needs reducing or splitting.", p.Message);
        Assert.Contains("Reduce to 16", p.Fixes);
        Assert.Contains("Split into 4 palettes", p.Fixes);
    }

    [Fact]
    public void TilesetBuildsMetatilesWithCollision()
    {
        var img = Checker(32, 16, 4);  // two 16x16 tiles
        var meta = new TilesetMeta { Tiles = { [1] = new TileProperties { Collision = CollisionType.Solid, Tag = "ground" } } };
        var t = TilesetConverter.Convert("forest", img, meta, ["ground"]);
        Assert.Equal(2, t.Metatiles.Count);
        Assert.Equal(CollisionType.Solid, t.Collision[1]);
        Assert.Equal(1, t.Tags[1]);
        Assert.True(t.Tiles.Count <= 1 + 8);
    }

    [Fact]
    public void PaletteFilesRoundTrip()
    {
        var dir = Directory.CreateTempSubdirectory("agbpal").FullName;
        var colors = new List<ushort> { 0, 0x7FFF, 0x001F, 0x03E0, 0x7C00 };
        foreach (var ext in new[] { ".pal", ".gpl" })
        {
            var path = Path.Combine(dir, "p" + ext);
            PaletteFiles.Write(path, colors);
            Assert.Equal(colors, PaletteFiles.Read(path));
        }
    }

    [Fact]
    public void PngRoundTrips()
    {
        var px = new uint[] { 0xFF102030, 0x00000000, 0xFFFFFFFF, 0x80FF0000 };
        var png = Png.Decode(Png.EncodeArgb(px, 2, 2));
        Assert.Equal(px, png.Pixels);
    }
}
