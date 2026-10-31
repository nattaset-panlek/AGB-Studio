using System.Text;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Graphics;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>
/// P7.02: the image converter from PNG and Aseprite to 4bpp tiles (split frames, trim empty frames, reduce to 16
/// colours, share a palette, animations from Aseprite tags), checked against reviewed golden outputs in
/// tests/AgbStudio.Tests/Golden. P7.03: the "too many colours" fixes and why 48 colours need 4 palettes.
/// </summary>
public sealed class ImageConverterTests
{
    static readonly string GoldenDir = Path.Combine(NativeBuild.RepoRoot, "tests", "AgbStudio.Tests", "Golden");

    /// <summary>A text dump of a converted sprite: everything the build writes into the ROM for it.</summary>
    static string Dump(ConvertedSprite s, IEnumerable<AnimationData>? anims = null)
    {
        var sb = new StringBuilder();
        sb.Append($"frames {s.FrameCount} of {s.SourceWidth}x{s.SourceHeight}, OAM {s.FrameWidth}x{s.FrameHeight} (shape {s.Shape}, size {s.SizeCode}), {s.TilesPerFrame} tiles per frame\n");
        sb.Append($"source frames {string.Join(",", s.SourceFrames)}\n");
        sb.Append($"palette {string.Join(" ", s.Palette.Select(Gba.Hex))}\n");
        foreach (var a in anims ?? [])
            sb.Append($"animation {a.Name}: frames {string.Join(",", a.Frames)} ticks {(a.FrameTicks != null ? string.Join(",", a.FrameTicks) : a.Ticks)}{(a.Loop ? " loop" : "")}{(a.PingPong ? " ping-pong" : "")}\n");
        for (var t = 0; t < s.Tiles.Length / 32; t++)
            sb.Append($"tile {t,3} {Convert.ToHexString(s.Tiles, t * 32, 32)}\n");
        return sb.ToString();
    }

    static void Golden(string name, string actual)
    {
        var path = Path.Combine(GoldenDir, name);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(GoldenDir);
            File.WriteAllText(path, actual);
            Assert.Fail($"No golden output yet: wrote {path}; review it and run again.");
        }
        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected != actual) File.WriteAllText(Path.Combine(NativeBuild.RepoRoot, "build", name), actual);
        Assert.Equal(expected, actual);
    }

    // The slime's four frames, an empty frame and frame 0 again: a sheet of six 16 x 12 frames.
    static Canvas SlimeSheet()
    {
        var slime = PixelArt.Slime4();
        var sheet = new Canvas(16 * 6, 12);
        sheet.Blit(slime, 0, 0);
        for (var y = 0; y < 12; y++)
            for (var x = 0; x < 16; x++) sheet.Set(80 + x, y, slime.Get(x, y));
        return sheet;
    }

    static uint[] Frame(Canvas sheet, int f, int y0 = 0, int y1 = 12)
    {
        var px = new uint[16 * 12];
        for (var y = y0; y < y1; y++)
            for (var x = 0; x < 16; x++) px[y * 16 + x] = sheet.Get(f * 16 + x, y);
        return px;
    }

    /// <summary>The same six frames as an Aseprite file: the top half of the slime on one layer and the bottom half on
    /// another, a hidden layer of junk, frame 4 empty, frame 5 a linked cel of frame 0, and two tags.</summary>
    static byte[] SlimeAseprite(int depth)
    {
        var sheet = SlimeSheet();
        var colours = sheet.Pixels.Where(c => c >> 24 >= 128).Distinct().OrderBy(c => c).ToList();
        var w = new AsepriteWriter
        {
            Width = 16, Height = 12, Depth = depth, TransparentIndex = 0,
            Palette = depth == 8 ? [0x00000000u, .. colours] : [],
        };
        w.Layers.Add(new("Bottom"));
        w.Layers.Add(new("Top"));
        w.Layers.Add(new("Sketch", Visible: false));
        int[] durations = [100, 100, 100, 100, 100, 200];
        for (var f = 0; f < 6; f++)
        {
            var cels = new List<AsepriteWriter.Cel>();
            if (f is < 4)
            {
                cels.Add(new(0, 0, 0, 16, 12, w.Pixels(Frame(sheet, f, 6, 12)), Raw: f == 1));
                cels.Add(new(1, 0, 0, 16, 6, w.Pixels(Frame(sheet, f, 0, 6)[..(16 * 6)])));
                var junk = Enumerable.Repeat(depth == 8 ? colours[0] : 0xFFFF0000u, 4 * 4).ToArray();
                cels.Add(new(2, 3, 3, 4, 4, w.Pixels(junk)));
            }
            else if (f == 5)
            {
                cels.Add(new(0, 0, 0, 0, 0, [], LinkFrame: 0));
                cels.Add(new(1, 0, 0, 0, 0, [], LinkFrame: 0));
            }
            w.Frames.Add(new(durations[f], cels));
        }
        w.Tags.Add(new("idle", 0, 3, Direction: 2));
        w.Tags.Add(new("blink", 4, 5, Repeat: 1));
        return w.Encode();
    }

    [Fact]
    public void PngSheetSplitsTrimsEmptyFramesAndMatchesTheGolden()
    {
        var img = ColorImage.FromPng(new Core.Png.Image(96, 12, SlimeSheet().Pixels, null, null));
        var all = SpriteConverter.Convert("slime", img, new SpriteMeta { FrameWidth = 16, FrameHeight = 12 });
        Assert.Equal(6, all.FrameCount);
        var s = SpriteConverter.Convert("slime", img, new SpriteMeta { FrameWidth = 16, FrameHeight = 12, TrimEmptyFrames = true });
        Assert.Equal(5, s.FrameCount);
        Assert.Equal([0, 1, 2, 3, 5], s.SourceFrames);
        Assert.Equal(s.Tiles.AsSpan(0, 4 * 32).ToArray(), s.Tiles.AsSpan(4 * 4 * 32, 4 * 32).ToArray());  // frame 5 = frame 0
        Golden("sprite-slime.golden.txt", Dump(s));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(8)]
    public void AsepriteFlattensLayersAndGivesTheSameTilesAsThePng(int depth)
    {
        var ase = AsepriteFile.Parse(SlimeAseprite(depth));
        Assert.Equal((16, 12, 6), (ase.Width, ase.Height, ase.Frames.Count));
        Assert.Equal([100, 100, 100, 100, 100, 200], ase.Durations);
        var sheet = SlimeSheet();
        for (var f = 0; f < 6; f++)
            for (var i = 0; i < 16 * 12; i++)
            {
                var want = sheet.Get(f * 16 + i % 16, i / 16);
                Assert.True((want >> 24 < 128 && ase.Frames[f][i] >> 24 < 128) || want == ase.Frames[f][i], $"frame {f} pixel {i}: {ase.Frames[f][i]:X8}, not {want:X8}");
            }
        var s = SpriteConverter.Convert("slime", ase.ToSheet(), new SpriteMeta { FrameWidth = ase.Width, FrameHeight = ase.Height, TrimEmptyFrames = true });
        // Same converted tiles and palette as the PNG sheet (the hidden layer is left out).
        Golden("sprite-slime.golden.txt", Dump(s));
        // Tags become animations; frame 4 was trimmed, so "blink" keeps only the linked frame.
        var anims = ase.Animations(s.SourceFrames);
        Assert.Equal("animation idle: frames 0,1,2,3 ticks 6 loop ping-pong\nanimation blink: frames 4 ticks 12\n",
            string.Concat(anims.Select(a => $"animation {a.Name}: frames {string.Join(",", a.Frames)} ticks {(a.FrameTicks != null ? string.Join(",", a.FrameTicks) : a.Ticks)}{(a.Loop ? " loop" : "")}{(a.PingPong ? " ping-pong" : "")}\n")));
    }

    [Fact]
    public void CommittedSampleAsepriteFileIsTheSlime()
    {
        // Golden/slime.aseprite is the file the editor's Import Asset test reads (AsepriteImportTests).
        var path = Path.Combine(GoldenDir, "slime.aseprite");
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, SlimeAseprite(32));
            Assert.Fail($"No sample Aseprite file yet: wrote {path}; run again.");
        }
        var committed = AsepriteFile.Read(path);
        var fresh = AsepriteFile.Parse(SlimeAseprite(32));
        Assert.Equal(fresh.Frames.Count, committed.Frames.Count);
        for (var f = 0; f < fresh.Frames.Count; f++) Assert.Equal(fresh.Frames[f], committed.Frames[f]);
        Assert.Equal(fresh.Tags, committed.Tags);
        Assert.Equal(fresh.Durations, committed.Durations);
    }

    [Fact]
    public void ADamagedAsepriteFileIsRefusedWithItsName()
    {
        var data = SlimeAseprite(32);
        var e = Assert.Throws<InvalidDataException>(() => AsepriteFile.Parse(data[..200], "slime.aseprite"));
        Assert.Contains("ends too early", e.Message);
        data[4] = 0;
        Assert.Contains("slime.aseprite is not an Aseprite file", Assert.Throws<InvalidDataException>(() => AsepriteFile.Parse(data, "slime.aseprite")).Message);
    }

    [Fact]
    public void ReducedAndSharedPalettesMatchTheGolden()
    {
        // 48 colours in a 16 x 16 frame reduced to 15 + transparent.
        var big = new ColorImage(16, 16);
        for (var i = 0; i < 256; i++)
        {
            var k = i % 48;
            big.Colors[i] = (ushort)((k * 5 % 32) | (k * 11 % 32) << 5 | (k * 17 % 32) << 10);
            big.Transparent[i] = i % 16 == 0;
        }
        var reduced = SpriteConverter.Convert("big", big, new SpriteMeta());
        Assert.Equal(15, reduced.Palette.Skip(1).Count(c => c != 0) + (reduced.Palette.Skip(1).Contains((ushort)0) ? 1 : 0));
        // A sprite that shares that palette: its pixels map to the nearest colours of the owner's palette.
        var coin = ColorImage.FromPng(new Core.Png.Image(48, 8, PixelArt.Coin6().Pixels, null, null));
        var shared = SpriteConverter.Convert("coin", coin, new SpriteMeta { FrameWidth = 8, FrameHeight = 8, Palette = "big" }, reduced.Palette);
        Assert.Same(reduced.Palette, shared.Palette);
        Golden("sprite-reduced-shared.golden.txt", Dump(reduced) + Dump(shared));
    }

    [Fact]
    public void AsepriteSpritesBuildWithTheirTagsAsAnimations()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "mini_aseprite"));
        var sprites = Path.Combine(p.AssetsDir, "sprites");
        File.Delete(Path.Combine(sprites, "slime.png"));
        File.WriteAllBytes(Path.Combine(sprites, "slime.aseprite"), SlimeAseprite(32));
        GameProject.Save(GameProject.MetaPath(Path.Combine(sprites, "slime.aseprite")), new SpriteMeta { OriginX = 8, OriginY = 12, TrimEmptyFrames = true });
        File.Delete(GameProject.MetaPath(Path.Combine(sprites, "slime.png")));
        var b = new GameBuilder(p, new Toolchain());
        var r = b.Build(new BuildOptions { GenerateOnly = true });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var cpp = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "assets", "sprite_slime.cpp"));
        Assert.Contains("{\"idle\", agbasset_sprite_slime_a0f, agbasset_sprite_slime_a0t, 4, true, true}", cpp);
        Assert.Contains("{\"blink\", agbasset_sprite_slime_a1f, agbasset_sprite_slime_a1t, 1, false, false}", cpp);
        Assert.Equal(5, b.Assets.Sprites["slime"].Count);
    }

    [Fact]
    public void FortyEightColoursNeedFourPalettesNotThree()
    {
        // P7.03: Figma's Import Asset · Errors card offers "Split into 3 palettes" for a 48-colour image. A 4bpp GBA
        // palette has 16 entries and entry 0 is always transparent for sprites (and for background tiles), so each
        // palette holds 15 colours: 3 palettes hold at most 45, and 48 colours need ceil(48 / 15) = 4. No packing
        // can do better, because every colour must sit in at least one palette.
        var img = new ColorImage(16, 16);
        for (var i = 0; i < 256; i++) img.Colors[i] = (ushort)(1 + i % 48 * 3);
        var p = Assert.Single(SpriteConverter.Convert("hero_big", img, new SpriteMeta { ReduceColors = false }, null, "hero_big.png").Problems);
        Assert.Equal(["Reduce to 16", "Split into 4 palettes"], p.Fixes);
        // The packer agrees: 48 one-colour groups fit 4 banks of 15 and never 3.
        var sets = Enumerable.Range(0, 48).Select(i => new HashSet<ushort> { (ushort)(1 + i * 3) }).ToList();
        Assert.Null(PaletteBanks.Pack(sets, 3));
        Assert.Equal(4, PaletteBanks.Pack(sets, 4)!.Value.Banks.Banks.Count);
        // 3 palettes is the right answer for 31 to 45 colours.
        foreach (var (colours, palettes) in new[] { (16, 2), (30, 2), (31, 3), (45, 3), (46, 4), (48, 4) })
        {
            var im = new ColorImage(16, 16);
            for (var i = 0; i < 256; i++) im.Colors[i] = (ushort)(1 + i % colours * 3);
            Assert.Contains($"Split into {palettes} palettes", SpriteConverter.Convert("x", im, new SpriteMeta { ReduceColors = false }).Problems.Single().Fixes);
        }
    }
}
