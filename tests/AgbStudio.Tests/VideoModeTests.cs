using AgbStudio.Build;
using AgbStudio.Core;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>P3.01, P3.02, P3.03: the pictures of the render_modes ROM (Mode 0 with four tile layers, alpha blending and
/// mosaic; Mode 1; Mode 2 with two blended affine layers; a rotate / scale sweep; the Mode 3 bitmap with mosaic and a
/// semi-transparent sprite) are captured from the emulator and compared pixel for pixel with reference images computed
/// here from the same content and the GBA's display rules. Captures and references go to build/runtime-tests/modes.</summary>
[Collection("emulator")]
public sealed class VideoModeTests(EmulatorFixture emu)
{
    static readonly string[] ShotNames = ["mode0", "mode1", "mode2", "sweep0", "sweep1", "sweep2", "sweep3", "sweep4", "sweep5", "mode3"];

    [Fact]
    public void VideoModesMatchReferenceImages()
    {
        var build = RuntimeTests.BuildAll(["render_modes"]);
        Assert.True(build.Success, build.Log);
        var (status, log, shots) = Run(RuntimeTests.RomPath("render_modes"));
        Assert.True(status == TestStatus.Pass, string.Join('\n', log));
        Assert.Equal(ShotNames, shots.Keys);

        var affine = log.Where(l => l.StartsWith("affine ", StringComparison.Ordinal)).Select(l => l.Split(' '))
            .ToDictionary(p => p[1], p => new Matrix(int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]), int.Parse(p[5]), long.Parse(p[6]), long.Parse(p[7])));
        var dir = Path.Combine(RuntimeTests.BuildDir, "modes");
        Directory.CreateDirectory(dir);
        var report = new List<string>();
        foreach (var (name, frame) in shots)
        {
            var reference = Reference(name, affine);
            Png.WriteArgb(Path.Combine(dir, name + ".png"), frame, LibretroCore.Width, LibretroCore.Height);
            Png.WriteArgb(Path.Combine(dir, name + "-reference.png"), reference.Select(ToArgb).ToArray(), LibretroCore.Width, LibretroCore.Height);
            var wrong = 0;
            string? first = null;
            for (var i = 0; i < reference.Length; i++)
            {
                if (Close(frame[i], reference[i])) continue;
                if (wrong++ == 0) first = $"({i % 240},{i / 240}) captured {frame[i] & 0xFFFFFF:X6}, reference {ToArgb(reference[i]) & 0xFFFFFF:X6}";
            }
            if (wrong > 0) report.Add($"{name}: {wrong} pixels differ, first at {first}");
        }
        Assert.True(report.Count == 0, string.Join('\n', report));

        // The sweep matrices are the requested rotation and scale (8.8 rounding).
        foreach (var line in log.Where(l => l.StartsWith("sweep ", StringComparison.Ordinal)))
        {
            var p = line.Split(' ');
            var (angle, scale) = (int.Parse(p[3]), int.Parse(p[5]) / 256.0);
            var m = affine[p[1]];
            var t = angle * Math.PI / 128;
            Assert.InRange(m.Pa, Math.Cos(t) / scale * 256 - 3, Math.Cos(t) / scale * 256 + 3);
            Assert.InRange(m.Pc, Math.Sin(t) / scale * 256 - 3, Math.Sin(t) / scale * 256 + 3);
            Assert.Equal(m.Pa, m.Pd);
            Assert.Equal(m.Pb, -m.Pc);
        }
    }

    // Modes 3, 4 and 5 of the render_bitmap ROM: pictures with queued rectangles and pixels, page flips, the hardware's
    // Mode 5 size with the backdrop around it, and a white 16 x 16 sprite at (100, 60) over each.
    static readonly string[] BitmapShots = ["bitmap3", "bitmap4", "bitmap4_hidden", "bitmap4_flipped", "bitmap5", "bitmap5_flipped"];

    [Fact]
    public void BitmapModesMatchReferenceImages()
    {
        var build = RuntimeTests.BuildAll(["render_bitmap"]);
        Assert.True(build.Success, build.Log);
        var (status, log, shots) = Run(RuntimeTests.RomPath("render_bitmap"));
        Assert.True(status == TestStatus.Pass, string.Join('\n', log));
        Assert.Equal(BitmapShots, shots.Keys);
        var dir = Path.Combine(RuntimeTests.BuildDir, "bitmap");
        Directory.CreateDirectory(dir);
        var report = new List<string>();
        foreach (var (name, frame) in shots)
        {
            var reference = new ushort[240 * 160];
            for (var y = 0; y < 160; y++)
                for (var x = 0; x < 240; x++) reference[y * 240 + x] = BitmapReference(name, x, y);
            Png.WriteArgb(Path.Combine(dir, name + ".png"), frame, LibretroCore.Width, LibretroCore.Height);
            Png.WriteArgb(Path.Combine(dir, name + "-reference.png"), reference.Select(ToArgb).ToArray(), LibretroCore.Width, LibretroCore.Height);
            var wrong = 0;
            string? first = null;
            for (var i = 0; i < reference.Length; i++)
            {
                if (Close(frame[i], reference[i])) continue;
                if (wrong++ == 0) first = $"({i % 240},{i / 240}) captured {frame[i] & 0xFFFFFF:X6}, reference {ToArgb(reference[i]) & 0xFFFFFF:X6}";
            }
            if (wrong > 0) report.Add($"{name}: {wrong} pixels differ, first at {first}");
        }
        Assert.True(report.Count == 0, string.Join('\n', report));
    }

    // ---- the content of runtime/tests/render_bitmap/main.cpp ---------------------------------------------------------
    const ushort BitmapBackdrop = 0x4010;
    static ushort BgrPixel(int x, int y) => (ushort)((x >> 3) | ((y >> 3) << 5) | (((x ^ y) >> 4 & 31) << 10));
    static int IndexPixel(int x, int y) => x is >= 8 and < 24 && y is >= 8 and < 24 ? 0 : 1 + ((x >> 2) & 7) + ((y >> 2) & 7) * 8;
    static ushort PaletteColor(int index) => (ushort)(((index & 7) * 4) | ((((index >> 3) & 7) * 4) << 5) | (20 << 10));

    static ushort BitmapReference(string shot, int x, int y)
    {
        if (x is >= 100 and < 116 && y is >= 60 and < 76) return 0x7FFF;  // the sprite, in front in every picture
        switch (shot)
        {
            case "bitmap3":
                if (x is >= 10 and < 40 && y is >= 20 and < 25) return 0x001F;
                if (y == 50 && x == 50) return 0x03E0;
                if (y == 50 && x == 51) return 0x7C00;
                return BgrPixel(x, y);
            case "bitmap4_flipped":
                if (x is >= 60 and < 91 && y is >= 40 and < 61) return 0x03FF;
                if (x == 120 && y == 100) return 0x03FF;
                goto case "bitmap4";
            case "bitmap4":
            case "bitmap4_hidden":
                var index = IndexPixel(x, y);
                return index == 0 ? BitmapBackdrop : PaletteColor(index);
            case "bitmap5_flipped":
                if (x is >= 150 and < 160 && y is >= 120 and < 128) return 0x001F;
                goto case "bitmap5";
            case "bitmap5":
                return x < 160 && y < 128 ? BgrPixel(x, y) : BitmapBackdrop;
            default:
                throw new ArgumentException(shot);
        }
    }

    // Runs the ROM and keeps the frame after each "shot <name>" line (the ROM holds the picture for four frames).
    (TestStatus, List<string>, Dictionary<string, uint[]>) Run(string rom)
    {
        var core = emu.Core;
        var log = new List<string>();
        var shots = new Dictionary<string, uint[]>();
        core.Keys = GbaKeys.None;
        core.LoadRom(rom);
        DebugChannel? channel = null;
        string? pending = null;
        var status = TestStatus.Running;
        for (var f = 0; f < 1200 && status == TestStatus.Running; f++)
        {
            core.RunFrame();
            if (pending != null)
            {
                shots[pending] = (uint[])core.Frame.Clone();
                pending = null;
            }
            channel ??= DebugChannel.Find(core);
            if (channel == null) continue;
            foreach (var l in channel.ReadNewLog())
            {
                log.Add(l);
                if (l.StartsWith("shot ", StringComparison.Ordinal)) pending = l[5..];
            }
            status = channel.Status;
        }
        core.UnloadRom();
        return (status, log, shots);
    }

    sealed record Matrix(int Pa, int Pb, int Pc, int Pd, long X, long Y);

    // ---- the ROM's content (runtime/tests/render_modes/main.cpp) ------------------------------------------------------
    static readonly ushort[][] LayerColors = [[0, 0x001F, 0x03FF], [0, 0x03E0, 0x7FE0], [0, 0x7C00, 0x7C1F], [0, 0x4210, 0x1084]];
    static readonly ushort[][] AffColors = [[0, 0x03E0, 0x001F, 0x7C00], [0, 0x7FFF, 0x2529, 0x03FF]];

    static int LayerCell(int layer, int mx, int my) => layer switch
    {
        0 => mx <= 4 && my <= 4 ? 1 : mx >= 12 && my >= 8 ? 2 : 0,
        1 => mx is >= 3 and <= 9 && my is >= 2 and <= 6 ? 1 : 0,
        2 => mx >= 7 && my is >= 4 and <= 8 ? 1 : 0,
        _ => ((mx + my) & 1) != 0 ? 1 : 2,
    };

    static int AffCell(int texture, int tx, int ty) =>
        texture == 0 ? (tx < 8 && ty < 4 ? 2 : ((tx / 2) + (ty / 2)) & 1) : ((tx / 4 + ty / 4) & 1) != 0 ? 3 : (tx / 4) % 3;

    static ushort PicturePixel(int x, int y) => (ushort)((x >> 3) | ((y >> 3) << 5) | ((((x ^ y) >> 4) & 31) << 10));

    // ---- GBA display rules ------------------------------------------------------------------------------------------
    // A layer pixel: a BGR555 colour, or null where the layer is clear (colour index 0).
    static ushort? TileLayer(int layer, int x, int y, int mosaic = 1)
    {
        x -= x % mosaic;
        y -= y % mosaic;
        var cell = LayerCell(layer, x / 16, y / 16);
        return cell == 0 ? null : LayerColors[layer][cell];
    }

    // Affine layer with wrap: texture = reference + (pa * x + pb * y, pc * x + pd * y) in 8.8, 32 x 32 tiles of 8 px.
    static ushort? AffineLayer(int texture, Matrix m, int x, int y)
    {
        var tx = (int)((m.X + (long)m.Pa * x + (long)m.Pb * y) >> 8) & 255;
        var ty = (int)((m.Y + (long)m.Pc * x + (long)m.Pd * y) >> 8) & 255;
        var tile = AffCell(texture, tx / 8, ty / 8);
        return tile == 3 ? null : AffColors[texture][tile + 1];
    }

    static ushort Blend(ushort a, ushort b, int eva, int evb)
    {
        int Ch(int shift) => Math.Min(31, (((a >> shift) & 31) * eva + ((b >> shift) & 31) * evb) >> 4);
        return (ushort)(Ch(0) | (Ch(5) << 5) | (Ch(10) << 10));
    }

    static ushort[] Reference(string name, Dictionary<string, Matrix> affine)
    {
        var px = new ushort[240 * 160];
        for (var y = 0; y < 160; y++)
            for (var x = 0; x < 240; x++)
                px[y * 240 + x] = name switch
                {
                    "mode0" => Mode0(x, y),
                    "mode1" => TileLayer(0, x, y) ?? TileLayer(1, x, y) ?? AffineLayer(0, affine["mode1"], x, y) ?? 0,
                    "mode2" => AffineLayer(1, affine["mode2a"], x, y) is { } front
                        ? Blend(front, AffineLayer(0, affine["mode2b"], x, y) ?? 0, 8, 8)
                        : AffineLayer(0, affine["mode2b"], x, y) ?? 0,
                    "mode3" => Mode3(x, y),
                    _ => AffineLayer(0, affine[name], x, y) ?? 0,
                };
        return px;
    }

    // BG0..BG3 at priorities 0..3; BG3 with a 6 px mosaic; BG1 blended 10/16 over the next layer (BG2, BG3, backdrop).
    static ushort Mode0(int x, int y)
    {
        var layers = new[] { TileLayer(0, x, y), TileLayer(1, x, y), TileLayer(2, x, y), TileLayer(3, x, y, 6) };
        var top = Array.FindIndex(layers, l => l != null);
        if (top < 0) return 0;
        if (top != 1) return layers[top]!.Value;
        var below = Array.FindIndex(layers, top + 1, l => l != null);
        return Blend(layers[1]!.Value, below < 0 ? (ushort)0 : layers[below]!.Value, 10, 6);
    }

    // The bitmap with a 4 px mosaic; a white 16 x 16 semi-transparent sprite at (100, 60) blended 8/16 + 8/16.
    static ushort Mode3(int x, int y)
    {
        var bg = PicturePixel(x - x % 4, y - y % 4);
        return x is >= 100 and < 116 && y is >= 60 and < 76 ? Blend(0x7FFF, bg, 8, 8) : bg;
    }

    static uint ToArgb(ushort c)
    {
        uint Expand(int v) => (uint)((v << 3) | (v >> 2));
        return 0xFF000000u | (Expand(c & 31) << 16) | (Expand((c >> 5) & 31) << 8) | Expand((c >> 10) & 31);
    }

    // Same colour within one step of 31 per channel (the core may hand frames over as RGB565 or XRGB8888).
    static bool Close(uint argb, ushort bgr555)
    {
        int To5(uint v) => (int)((v * 31 + 127) / 255);
        return Math.Abs(To5((argb >> 16) & 255) - (bgr555 & 31)) <= 1 && Math.Abs(To5((argb >> 8) & 255) - ((bgr555 >> 5) & 31)) <= 1 &&
               Math.Abs(To5(argb & 255) - ((bgr555 >> 10) & 31)) <= 1;
    }
}
