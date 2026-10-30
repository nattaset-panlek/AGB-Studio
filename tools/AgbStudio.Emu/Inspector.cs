using System.Text;

namespace AgbStudio.Emu;

/// <summary>A live actor from the runtime's debug actor table (Play Mode Inspector "live").</summary>
public sealed record LiveActor(string Name, double X, double Y, double Vx, double Vy, int Tag, bool Alive, bool OnGround, bool Visible, bool FacingLeft, int State);

/// <summary>One OAM entry decoded (Debugger "OAM sprites": #, X, Y, Size, Tile, Pal, Prio, Flags).</summary>
public sealed record OamEntry(int Index, int X, int Y, int Width, int Height, int Tile, int Palette, int Priority, bool Hidden, bool Affine,
    bool DoubleSize, bool HFlip, bool VFlip, bool Mosaic, bool Blend, int AffineIndex)
{
    public string Flags => string.Join(' ', new[] { HFlip ? "flip H" : "", VFlip ? "flip V" : "", Affine ? "affine" : "", Mosaic ? "mosaic" : "", Blend ? "blend" : "" }.Where(s => s.Length > 0));
}

/// <summary>Profiler stats (runtime agb::profile::Stats).</summary>
public sealed record ProfilerStats(uint Logic, uint Render, uint Audio, uint Idle, uint[] History, uint Overruns, uint VBlankFree)
{
    public const uint CyclesPerFrame = 280896;
    public static double Ms(uint cycles) => cycles / 16777.216;
    public double BusyMs => Ms(Logic + Render + Audio);
    public double CpuPercent => 100.0 * (Logic + Render + Audio) / CyclesPerFrame;
}

/// <summary>Reads hardware and runtime state out of the emulator for Play Mode and the Debugger.</summary>
public static class Inspector
{
    public static string ReadString(LibretroCore core, uint ptr, int max = 64)
    {
        if (ptr == 0) return "";
        var bytes = new List<byte>();
        for (var i = 0; i < max; i++)
        {
            byte b;
            try { b = core.Memory(ptr + (uint)i, 1)[0]; }
            catch (ArgumentOutOfRangeException) { break; }
            if (b == 0) break;
            bytes.Add(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public static List<LiveActor> Actors(LibretroCore core, DebugChannel ch)
    {
        var list = new List<LiveActor>();
        var table = ch.ActorTable;
        var stride = ch.ActorStride;
        if (table == 0 || stride == 0) return list;
        for (uint i = 0; i < ch.ActorCount && i < 64; i++)
        {
            var a = table + i * stride;
            var flags = core.Memory(a + 22, 1)[0];
            list.Add(new LiveActor(ReadString(core, core.ReadU32(a)),
                (int)core.ReadU32(a + 4) / 256.0, (int)core.ReadU32(a + 8) / 256.0, (int)core.ReadU32(a + 12) / 256.0, (int)core.ReadU32(a + 16) / 256.0,
                core.ReadU16(a + 20), (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, (sbyte)core.Memory(a + 23, 1)[0]));
        }
        return list;
    }

    public static ProfilerStats? Profiler(LibretroCore core, DebugChannel ch)
    {
        var p = ch.Profiler;
        if (p == 0) return null;
        var history = new uint[120];
        var head = core.ReadU32(p + 16 + 480);
        for (var i = 0; i < 120; i++) history[i] = core.ReadU32(p + 16 + (uint)(((head + i) % 120) * 4));  // oldest first
        return new ProfilerStats(core.ReadU32(p), core.ReadU32(p + 4), core.ReadU32(p + 8), core.ReadU32(p + 12), history,
            core.ReadU32(p + 16 + 488), core.ReadU32(p + 16 + 484));
    }

    public static ushort[] PaletteRam(LibretroCore core)
    {
        var bytes = core.Memory(GbaMemory.Palette, GbaMemory.PaletteSize);
        var o = new ushort[512];
        for (var i = 0; i < 512; i++) o[i] = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
        return o;
    }

    public static uint ToArgb(ushort bgr)
    {
        int r = bgr & 31, g = (bgr >> 5) & 31, b = (bgr >> 10) & 31;
        return 0xFF000000u | ((uint)(r * 255 / 31) << 16) | ((uint)(g * 255 / 31) << 8) | (uint)(b * 255 / 31);
    }

    static readonly (int W, int H)[,] OamSizes =
    {
        { (8, 8), (16, 16), (32, 32), (64, 64) }, { (16, 8), (32, 8), (32, 16), (64, 32) }, { (8, 16), (8, 32), (16, 32), (32, 64) },
    };

    public static List<OamEntry> Oam(LibretroCore core, bool includeHidden = false)
    {
        var bytes = core.Memory(GbaMemory.Oam, GbaMemory.OamSize);
        var list = new List<OamEntry>();
        for (var i = 0; i < 128; i++)
        {
            int a0 = bytes[i * 8] | (bytes[i * 8 + 1] << 8), a1 = bytes[i * 8 + 2] | (bytes[i * 8 + 3] << 8), a2 = bytes[i * 8 + 4] | (bytes[i * 8 + 5] << 8);
            var affine = (a0 & 0x100) != 0;
            var hidden = !affine && (a0 & 0x200) != 0;
            if (hidden && !includeHidden) continue;
            var shape = (a0 >> 14) & 3;
            if (shape == 3) continue;
            var (w, h) = OamSizes[shape, (a1 >> 14) & 3];
            int y = a0 & 0xFF, x = a1 & 0x1FF;
            if (y >= 160) y -= 256;
            if (x >= 240) x -= 512;
            list.Add(new OamEntry(i, x, y, w, h, a2 & 0x3FF, (a2 >> 12) & 15, (a2 >> 10) & 3, hidden, affine, affine && (a0 & 0x200) != 0,
                !affine && (a1 & 0x1000) != 0, !affine && (a1 & 0x2000) != 0, (a0 & 0x1000) != 0, ((a0 >> 10) & 3) == 1, affine ? (a1 >> 9) & 31 : -1));
        }
        return list;
    }

    /// <summary>Renders tiles of VRAM (Debugger "VRAM tiles") as ARGB, 16 tiles per row, using one 16-colour bank.</summary>
    public static (uint[] Pixels, int Width, int Height) Tiles(LibretroCore core, uint vramOffset, int tileCount, int paletteBank, bool obj)
    {
        var pal = PaletteRam(core);
        var bytes = core.Memory(GbaMemory.Vram + vramOffset, tileCount * 32);
        const int perRow = 16;
        var rows = (tileCount + perRow - 1) / perRow;
        var w = perRow * 8;
        var h = rows * 8;
        var px = new uint[w * h];
        for (var t = 0; t < tileCount; t++)
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                {
                    var b = bytes[t * 32 + y * 4 + x / 2];
                    var idx = (x & 1) == 0 ? b & 15 : b >> 4;
                    var c = idx == 0 ? 0xFF202020u : ToArgb(pal[(obj ? 256 : 0) + paletteBank * 16 + idx]);
                    px[(t / perRow * 8 + y) * w + t % perRow * 8 + x] = c;
                }
        return (px, w, h);
    }

    /// <summary>Display control: video mode and visible layers.</summary>
    public static (int Mode, bool[] Bg, bool Obj) Display(LibretroCore core)
    {
        var d = core.ReadU16(GbaMemory.Io);
        return (d & 7, [(d & 0x100) != 0, (d & 0x200) != 0, (d & 0x400) != 0, (d & 0x800) != 0], (d & 0x1000) != 0);
    }

    public static string HexDump(LibretroCore core, uint address, int lines)
    {
        var sb = new StringBuilder();
        for (var l = 0; l < lines; l++)
        {
            var a = address + (uint)(l * 16);
            Span<byte> row;
            try { row = core.Memory(a, 16); }
            catch (ArgumentOutOfRangeException) { break; }
            sb.Append($"{a >> 16:X4} {a & 0xFFFF:X4}  ");
            for (var i = 0; i < 16; i++) sb.Append($"{row[i]:X2} ");
            sb.Append(' ');
            foreach (var b in row) sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

/// <summary>The Event Script VM debugger state (runtime agb::script::DebugState) in the debug channel.</summary>
public sealed class ScriptDebugger(LibretroCore core, DebugChannel channel)
{
    uint State => channel.Debugger;
    public bool Available => State != 0;

    public void SetBreakpoints(IReadOnlyList<(int Script, int Line)> points)
    {
        var s = State;
        var n = Math.Min(16, points.Count);
        for (var i = 0; i < n; i++)
        {
            BitConverter.TryWriteBytes(core.Memory(s + (uint)(i * 4), 2), (ushort)points[i].Script);
            BitConverter.TryWriteBytes(core.Memory(s + (uint)(i * 4) + 2, 2), (ushort)points[i].Line);
        }
        core.Memory(s + 64, 1)[0] = (byte)n;
    }

    public (int Context, int Script, int Line)? Paused
    {
        get
        {
            var s = State;
            var ctx = core.Memory(s + 65, 1)[0];
            return ctx == 0xFF ? null : (ctx, core.ReadU16(s + 68), core.ReadU16(s + 70));
        }
    }

    public void Resume() => core.Memory(State + 66, 1)[0] = 1;
    public void Step() => core.Memory(State + 66, 1)[0] = 2;
}
