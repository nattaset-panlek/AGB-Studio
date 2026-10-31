using System.IO.Compression;
using System.Text;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Graphics;

/// <summary>
/// Reads Aseprite files (.aseprite / .ase, as documented by Aseprite in docs/ase-file-specs.md): RGBA, grayscale and
/// indexed sprites, layers (hidden layers and hidden groups are left out, layer and cel opacity applied, every blend
/// mode drawn as Normal), raw, compressed and linked cels, the palette and the frame tags. Each frame is flattened to
/// ARGB pixels; <see cref="ToSheet"/> puts the frames side by side, the layout the sprite converter splits.
/// </summary>
public sealed class AsepriteFile
{
    public enum Direction { Forward, Reverse, PingPong, PingPongReverse }

    public sealed record Tag(string Name, int From, int To, Direction Direction, int Repeat);

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int ColorDepth { get; private set; }   // 32 RGBA, 16 grayscale, 8 indexed
    public List<uint[]> Frames { get; } = new();   // ARGB, Width * Height each
    public List<int> Durations { get; } = new();   // milliseconds per frame
    public List<Tag> Tags { get; } = new();
    public uint[] Palette { get; private set; } = new uint[256];

    sealed class Layer
    {
        public int Flags, Type, Level, Opacity;
        public string Name = "";
        public int Parent = -1;
        public bool Visible;  // with every parent group
    }

    sealed class Cel
    {
        public int Layer, X, Y, Opacity, Z, Width, Height;
        public byte[] Pixels = [];  // raw pixels in the sprite's colour depth
    }

    public static bool IsAseprite(string path) =>
        path.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ase", StringComparison.OrdinalIgnoreCase);

    public static AsepriteFile Read(string path) => Parse(File.ReadAllBytes(path), Path.GetFileName(path));

    public static AsepriteFile Parse(byte[] data, string name = "sprite")
    {
        var f = new AsepriteFile();
        var r = new Reader(data);
        r.Dword();  // file size
        if (r.Word() != 0xA5E0) throw new InvalidDataException($"{name} is not an Aseprite file.");
        var frames = r.Word();
        f.Width = r.Word();
        f.Height = r.Word();
        f.ColorDepth = r.Word();
        if (f.ColorDepth is not (32 or 16 or 8)) throw new InvalidDataException($"{name}: colour depth {f.ColorDepth} is not RGBA, grayscale or indexed.");
        var flags = r.Dword();
        r.Skip(2 + 4 + 4);  // speed (deprecated), two zero DWORDs
        var transparentIndex = r.Byte();
        r.Skip(3);
        r.Word();  // number of colours
        r.Skip(128 - r.Position);
        var layers = new List<Layer>();
        var celsByFrame = new List<List<Cel>>();
        for (var fi = 0; fi < frames; fi++)
        {
            var frameStart = r.Position;
            var frameBytes = (int)r.Dword();
            if (r.Word() != 0xF1FA) throw new InvalidDataException($"{name}: frame {fi} is damaged.");
            var oldChunks = r.Word();
            f.Durations.Add(r.Word());
            r.Skip(2);
            var newChunks = (int)r.Dword();
            var chunks = newChunks != 0 ? newChunks : oldChunks;
            var cels = new List<Cel>();
            celsByFrame.Add(cels);
            for (var ci = 0; ci < chunks; ci++)
            {
                var chunkStart = r.Position;
                var size = (int)r.Dword();
                var type = r.Word();
                switch (type)
                {
                    case 0x0004:  // old palette (0..255 values)
                    case 0x0011:  // old palette (0..63 values)
                    {
                        if (type == 0x0011 || f.HasNewPalette) break;
                        var packets = r.Word();
                        var index = 0;
                        for (var p = 0; p < packets; p++)
                        {
                            index += r.Byte();
                            int count = r.Byte();
                            if (count == 0) count = 256;
                            for (var k = 0; k < count && index < 256; k++, index++)
                                f.Palette[index] = 0xFF000000u | (uint)(r.Byte() << 16) | (uint)(r.Byte() << 8) | r.Byte();
                        }
                        break;
                    }
                    case 0x2004:  // layer
                    {
                        var l = new Layer { Flags = r.Word(), Type = r.Word(), Level = r.Word() };
                        r.Skip(4);  // default width / height
                        r.Word();   // blend mode: drawn as Normal (GBA sprites have no blend modes)
                        l.Opacity = r.Byte();
                        if ((flags & 1) == 0) l.Opacity = 255;
                        r.Skip(3);
                        l.Name = r.String();
                        // The parent is the nearest earlier layer one level up.
                        for (var p = layers.Count - 1; p >= 0 && l.Level > 0; p--)
                            if (layers[p].Level == l.Level - 1) { l.Parent = p; break; }
                        var hidden = (l.Flags & 1) == 0 || (l.Flags & 64) != 0;  // hidden or a reference layer
                        l.Visible = !hidden && (l.Parent < 0 || layers[l.Parent].Visible);
                        layers.Add(l);
                        break;
                    }
                    case 0x2005:  // cel
                    {
                        var c = new Cel { Layer = r.Word(), X = r.Short(), Y = r.Short(), Opacity = r.Byte() };
                        var celType = r.Word();
                        c.Z = r.Short();
                        r.Skip(5);
                        switch (celType)
                        {
                            case 0:
                                c.Width = r.Word();
                                c.Height = r.Word();
                                c.Pixels = r.Bytes(c.Width * c.Height * f.ColorDepth / 8);
                                cels.Add(c);
                                break;
                            case 1:
                            {
                                var link = r.Word();
                                var src = link < celsByFrame.Count ? celsByFrame[link].FirstOrDefault(x => x.Layer == c.Layer) : null;
                                if (src != null) cels.Add(new Cel { Layer = c.Layer, X = c.X, Y = c.Y, Opacity = c.Opacity, Z = c.Z, Width = src.Width, Height = src.Height, Pixels = src.Pixels });
                                break;
                            }
                            case 2:
                            {
                                c.Width = r.Word();
                                c.Height = r.Word();
                                var packed = r.Bytes(chunkStart + size - r.Position);
                                using var z = new ZLibStream(new MemoryStream(packed), CompressionMode.Decompress);
                                using var outMs = new MemoryStream();
                                z.CopyTo(outMs);
                                c.Pixels = outMs.ToArray();
                                if (c.Pixels.Length < c.Width * c.Height * f.ColorDepth / 8) throw new InvalidDataException($"{name}: a cel in frame {fi} is damaged.");
                                cels.Add(c);
                                break;
                            }
                            // 3: compressed tilemap (tilemap layers) - not a sprite image; left out.
                        }
                        break;
                    }
                    case 0x2018:  // tags
                    {
                        var count = r.Word();
                        r.Skip(8);
                        for (var t = 0; t < count; t++)
                        {
                            int from = r.Word(), to = r.Word();
                            var dir = (Direction)Math.Min((int)r.Byte(), 3);
                            var repeat = r.Word();
                            r.Skip(6 + 3 + 1);
                            f.Tags.Add(new Tag(r.String(), from, to, dir, repeat));
                        }
                        break;
                    }
                    case 0x2019:  // palette
                    {
                        f.HasNewPalette = true;
                        r.Dword();  // new palette size
                        var first = (int)r.Dword();
                        var last = (int)r.Dword();
                        r.Skip(8);
                        for (var i = first; i <= last; i++)
                        {
                            var entryFlags = r.Word();
                            byte cr = r.Byte(), cg = r.Byte(), cb = r.Byte(), ca = r.Byte();
                            if (i < 256) f.Palette[i] = (uint)(ca << 24 | cr << 16 | cg << 8 | cb);
                            if ((entryFlags & 1) != 0) r.String();
                        }
                        break;
                    }
                }
                r.Position = chunkStart + size;
            }
            r.Position = frameStart + frameBytes;
        }
        foreach (var cels in celsByFrame) f.Frames.Add(f.Flatten(cels, layers, transparentIndex));
        return f;
    }

    bool HasNewPalette { get; set; }

    // Composites the visible cels of one frame (layer order plus z-index, Normal blending).
    uint[] Flatten(List<Cel> cels, List<Layer> layers, int transparentIndex)
    {
        var px = new uint[Width * Height];
        foreach (var c in cels.Where(c => c.Layer < layers.Count && layers[c.Layer].Visible && layers[c.Layer].Type == 0)
                     .OrderBy(c => c.Layer + c.Z).ThenBy(c => c.Z))
        {
            var opacity = c.Opacity * layers[c.Layer].Opacity / 255;
            for (var y = 0; y < c.Height; y++)
            {
                var ty = c.Y + y;
                if (ty < 0 || ty >= Height) continue;
                for (var x = 0; x < c.Width; x++)
                {
                    var tx = c.X + x;
                    if (tx < 0 || tx >= Width) continue;
                    var src = Pixel(c.Pixels, y * c.Width + x, transparentIndex);
                    var a = (int)(src >> 24) * opacity / 255;
                    if (a == 0) continue;
                    var i = ty * Width + tx;
                    px[i] = Blend(px[i], src & 0xFFFFFF, a);
                }
            }
        }
        return px;
    }

    uint Pixel(byte[] p, int i, int transparentIndex) => ColorDepth switch
    {
        32 => (uint)(p[i * 4 + 3] << 24 | p[i * 4] << 16 | p[i * 4 + 1] << 8 | p[i * 4 + 2]),
        16 => (uint)(p[i * 2 + 1] << 24 | p[i * 2] << 16 | p[i * 2] << 8 | p[i * 2]),
        _ => p[i] == transparentIndex ? 0u : Palette[p[i]],
    };

    static uint Blend(uint dst, uint rgb, int a)
    {
        var da = (int)(dst >> 24);
        if (a == 255 || da == 0) return (uint)(a << 24) | rgb;
        var oa = a + da * (255 - a) / 255;
        int Ch(int shift)
        {
            var s = (int)(rgb >> shift) & 255;
            var d = (int)(dst >> shift) & 255;
            return (s * a + d * da * (255 - a) / 255) / Math.Max(1, oa);
        }
        return (uint)(oa << 24 | Ch(16) << 16 | Ch(8) << 8 | Ch(0));
    }

    /// <summary>ARGB pixels of the frames side by side (Width * frames x Height).</summary>
    public uint[] SheetPixels()
    {
        var w = Width * Math.Max(1, Frames.Count);
        var argb = new uint[w * Height];
        for (var fi = 0; fi < Frames.Count; fi++)
            for (var y = 0; y < Height; y++)
                Array.Copy(Frames[fi], y * Width, argb, y * w + fi * Width, Width);
        return argb;
    }

    /// <summary>The frames side by side, as the sprite converter reads a sheet.</summary>
    public ColorImage ToSheet() => ColorImage.FromPng(new Core.Png.Image(Width * Math.Max(1, Frames.Count), Height, SheetPixels(), null, null));

    /// <summary>Sprite animations from the frame tags (all frames as "idle" when there are none). <paramref name="kept"/>
    /// is the source frame of each converted frame (frames trimmed as empty are left out); durations become ticks at
    /// 60 frames a second.</summary>
    public List<AnimationData> Animations(IReadOnlyList<int> kept)
    {
        var tags = Tags.Count > 0 ? Tags : [new Tag("idle", 0, Math.Max(0, Frames.Count - 1), Direction.Forward, 0)];
        var list = new List<AnimationData>();
        foreach (var t in tags)
        {
            var source = Enumerable.Range(t.From, Math.Max(0, t.To - t.From + 1)).ToList();
            if (t.Direction is Direction.Reverse or Direction.PingPongReverse) source.Reverse();
            var frames = source.Select(s => (Source: s, Index: IndexOf(kept, s))).Where(x => x.Index >= 0).ToList();
            if (frames.Count == 0) continue;
            var ticks = frames.Select(x => Math.Max(1, (int)Math.Round(Durations[x.Source] * 60 / 1000.0))).ToList();
            list.Add(new AnimationData
            {
                Name = t.Name,
                Frames = frames.Select(x => x.Index).ToList(),
                Ticks = ticks[0],
                FrameTicks = ticks.All(x => x == ticks[0]) ? null : ticks,
                Loop = t.Repeat == 0,
                PingPong = t.Direction is Direction.PingPong or Direction.PingPongReverse,
            });
        }
        return list;
    }

    static int IndexOf(IReadOnlyList<int> list, int value)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == value) return i;
        return -1;
    }

    sealed class Reader(byte[] data)
    {
        public int Position;

        void Need(int n)
        {
            if (Position + n > data.Length) throw new InvalidDataException("The Aseprite file ends too early.");
        }

        public byte Byte() { Need(1); return data[Position++]; }
        public int Word() { Need(2); var v = data[Position] | data[Position + 1] << 8; Position += 2; return v; }
        public short Short() => (short)Word();
        public uint Dword() { Need(4); var v = BitConverter.ToUInt32(data, Position); Position += 4; return v; }
        public void Skip(int n) { Need(n); Position += n; }
        public byte[] Bytes(int n) { Need(n); var b = data.AsSpan(Position, n).ToArray(); Position += n; return b; }
        public string String() { var n = Word(); return Encoding.UTF8.GetString(Bytes(n)); }
    }
}
