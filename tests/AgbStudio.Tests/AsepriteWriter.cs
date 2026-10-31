using System.IO.Compression;
using System.Text;

namespace AgbStudio.Tests;

/// <summary>Writes small Aseprite files for the importer tests, following Aseprite's file format document: header,
/// frames, layer / cel (raw, linked, zlib-compressed) / palette / tags chunks.</summary>
public sealed class AsepriteWriter
{
    public sealed record Layer(string Name, bool Visible = true, int Opacity = 255, int Level = 0, bool Group = false);
    public sealed record Cel(int Layer, int X, int Y, int Width, int Height, byte[] Pixels, int Opacity = 255, int LinkFrame = -1, bool Raw = false);
    public sealed record Frame(int Duration, List<Cel> Cels);
    public sealed record Tag(string Name, int From, int To, int Direction = 0, int Repeat = 0);

    public int Width { get; init; }
    public int Height { get; init; }
    public int Depth { get; init; } = 32;           // 32 RGBA, 16 grayscale, 8 indexed
    public int TransparentIndex { get; init; }
    public uint[] Palette { get; init; } = [];       // ARGB, indexed sprites
    public List<Layer> Layers { get; } = new();
    public List<Frame> Frames { get; } = new();
    public List<Tag> Tags { get; } = new();

    /// <summary>Pixels of a cel from ARGB values in the writer's colour depth.</summary>
    public byte[] Pixels(uint[] argb)
    {
        var o = new List<byte>();
        foreach (var c in argb)
        {
            switch (Depth)
            {
                case 32: o.AddRange([(byte)(c >> 16), (byte)(c >> 8), (byte)c, (byte)(c >> 24)]); break;
                case 16: o.AddRange([(byte)(c & 0xFF), (byte)(c >> 24)]); break;
                default:
                    var i = c >> 24 < 128 ? TransparentIndex : Array.IndexOf(Palette, c);
                    if (i < 0) throw new ArgumentException($"colour {c:X8} is not in the palette");
                    o.Add((byte)i);
                    break;
            }
        }
        return o.ToArray();
    }

    public byte[] Encode()
    {
        var frames = new MemoryStream();
        for (var fi = 0; fi < Frames.Count; fi++)
        {
            var chunks = new List<byte[]>();
            if (fi == 0)
            {
                if (Palette.Length > 0)
                {
                    var p = new Body();
                    p.Dword((uint)Palette.Length).Dword(0).Dword((uint)Palette.Length - 1).Zeros(8);
                    foreach (var c in Palette) p.Word(0).Byte((byte)(c >> 16)).Byte((byte)(c >> 8)).Byte((byte)c).Byte((byte)(c >> 24));
                    chunks.Add(p.Chunk(0x2019));
                }
                foreach (var l in Layers)
                {
                    var b = new Body();
                    b.Word((ushort)((l.Visible ? 1 : 0) | 2)).Word((ushort)(l.Group ? 1 : 0)).Word((ushort)l.Level).Word(0).Word(0).Word(0)
                        .Byte((byte)l.Opacity).Zeros(3).String(l.Name);
                    chunks.Add(b.Chunk(0x2004));
                }
                if (Tags.Count > 0)
                {
                    var t = new Body();
                    t.Word((ushort)Tags.Count).Zeros(8);
                    foreach (var tag in Tags)
                        t.Word((ushort)tag.From).Word((ushort)tag.To).Byte((byte)tag.Direction).Word((ushort)tag.Repeat).Zeros(6).Zeros(3).Zeros(1).String(tag.Name);
                    chunks.Add(t.Chunk(0x2018));
                }
            }
            foreach (var c in Frames[fi].Cels)
            {
                var b = new Body();
                b.Word((ushort)c.Layer).Word((ushort)(short)c.X).Word((ushort)(short)c.Y).Byte((byte)c.Opacity);
                if (c.LinkFrame >= 0)
                {
                    b.Word(1).Word(0).Zeros(5).Word((ushort)c.LinkFrame);
                }
                else if (c.Raw)
                {
                    b.Word(0).Word(0).Zeros(5).Word((ushort)c.Width).Word((ushort)c.Height).Bytes(c.Pixels);
                }
                else
                {
                    var packed = new MemoryStream();
                    using (var z = new ZLibStream(packed, CompressionLevel.Optimal, true)) z.Write(c.Pixels);
                    b.Word(2).Word(0).Zeros(5).Word((ushort)c.Width).Word((ushort)c.Height).Bytes(packed.ToArray());
                }
                chunks.Add(b.Chunk(0x2005));
            }
            var size = 16 + chunks.Sum(x => x.Length);
            var header = new Body();
            header.Dword((uint)size).Word(0xF1FA).Word((ushort)Math.Min(chunks.Count, 0xFFFF)).Word((ushort)Frames[fi].Duration).Zeros(2).Dword((uint)chunks.Count);
            frames.Write(header.ToArray());
            foreach (var c in chunks) frames.Write(c);
        }
        var file = new Body();
        file.Dword((uint)(128 + frames.Length)).Word(0xA5E0).Word((ushort)Frames.Count).Word((ushort)Width).Word((ushort)Height).Word((ushort)Depth)
            .Dword(1).Word(100).Dword(0).Dword(0).Byte((byte)TransparentIndex).Zeros(3).Word((ushort)Palette.Length).Byte(1).Byte(1)
            .Word(0).Word(0).Word(16).Word(16).Zeros(84);
        var o = file.ToArray().Concat(frames.ToArray()).ToArray();
        if (o.Length != 128 + frames.Length) throw new InvalidOperationException("header size");
        return o;
    }

    sealed class Body
    {
        readonly MemoryStream _s = new();
        public Body Byte(byte v) { _s.WriteByte(v); return this; }
        public Body Word(ushort v) { _s.Write(BitConverter.GetBytes(v)); return this; }
        public Body Dword(uint v) { _s.Write(BitConverter.GetBytes(v)); return this; }
        public Body Zeros(int n) { _s.Write(new byte[n]); return this; }
        public Body Bytes(byte[] b) { _s.Write(b); return this; }
        public Body String(string s) { var b = Encoding.UTF8.GetBytes(s); return Word((ushort)b.Length).Bytes(b); }
        public byte[] ToArray() => _s.ToArray();
        public byte[] Chunk(ushort type)
        {
            var body = ToArray();
            return BitConverter.GetBytes((uint)(body.Length + 6)).Concat(BitConverter.GetBytes(type)).Concat(body).ToArray();
        }
    }
}
