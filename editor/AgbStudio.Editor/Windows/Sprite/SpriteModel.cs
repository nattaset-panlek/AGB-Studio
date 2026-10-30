using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Windows.Palette;

namespace AgbStudio.Editor.Windows.Sprite;

/// <summary>
/// A sprite sheet being edited: the PNG (frames left to right, top to bottom) and its meta in the project's
/// <see cref="ArtLibrary"/>. Frame geometry, OAM size and VRAM use follow the toolchain (SpriteConverter, Gba.FitOam).
/// </summary>
public sealed class SpriteDoc(ArtLibrary lib, string path)
{
    public ArtLibrary Library { get; } = lib;
    public string Path { get; } = lib.Full(lib.Rel(path));
    public string FileName => System.IO.Path.GetFileName(Path);
    public string AssetName => GameProject.AssetName(Path);
    public EditableImage Image => Library.Image(Path);
    public SpriteMeta Meta => Library.Meta(Path);
    public PaletteBank? Bank => Library.BankOf(Path);

    public int FrameW => Math.Clamp(Meta.FrameWidth, 1, Math.Max(1, Image.Width));
    public int FrameH => Math.Clamp(Meta.FrameHeight, 1, Math.Max(1, Image.Height));
    public int Columns => Math.Max(1, Image.Width / FrameW);
    public int Rows => Math.Max(1, Image.Height / FrameH);
    public int FrameCount => Columns * Rows;

    public (int X, int Y) FrameOrigin(int frame) => (frame % Columns * FrameW, frame / Columns * FrameH);

    public uint Get(int frame, int x, int y)
    {
        if (x < 0 || y < 0 || x >= FrameW || y >= FrameH || frame < 0 || frame >= FrameCount) return Colour15.Transparent;
        var (ox, oy) = FrameOrigin(frame);
        return Image[ox + x, oy + y];
    }

    public bool Set(int frame, int x, int y, uint argb)
    {
        if (x < 0 || y < 0 || x >= FrameW || y >= FrameH || frame < 0 || frame >= FrameCount) return false;
        var (ox, oy) = FrameOrigin(frame);
        if (Image[ox + x, oy + y] == argb) return false;
        Image[ox + x, oy + y] = argb;
        return true;
    }

    public uint[] FramePixels(int frame)
    {
        var px = new uint[FrameW * FrameH];
        for (var y = 0; y < FrameH; y++)
            for (var x = 0; x < FrameW; x++) px[y * FrameW + x] = Get(frame, x, y);
        return px;
    }

    /// <summary>The hardware sprite size frames are padded to.</summary>
    public (int W, int H) Oam => OamFor(FrameW, FrameH);

    public static (int W, int H) OamFor(int w, int h) => w > 64 || h > 64 ? (64, 64) : Gba.FitOam(w, h);

    public static string ShapeOf(int w, int h) => w == h ? "Square" : w > h ? "Wide" : "Tall";
    public static string OamLabel(int w, int h) => $"{ShapeOf(w, h)} {w}×{h}";

    public int TilesPerFrame => Oam.W * Oam.H / 64;
    public int BytesPerFrame => TilesPerFrame * 32;
    public string VramText => $"{TilesPerFrame} tiles · {BytesPerFrame} B";
    public string SizeText => $"{FrameW}×{FrameH}";
    public string ListMeta => FrameCount > 1 ? $"{SizeText} · {FrameCount}f" : SizeText;

    /// <summary>Animations to show: the meta's, or one with every frame when the sheet has none.</summary>
    public IReadOnlyList<AnimationData> Animations =>
        Meta.Animations.Count > 0 ? Meta.Animations : [new AnimationData { Name = "idle", Frames = Enumerable.Range(0, FrameCount).ToList() }];

    /// <summary>Changes the meta as one undo step.</summary>
    public void EditMeta(string name, Action<SpriteMeta> change)
    {
        Library.Edit(name, [], () =>
        {
            change(Meta);
            Library.MarkMetaDirty(Path);
        }, [Path]);
    }

    /// <summary>
    /// OAM size menu (T-M19): makes the frames <paramref name="w"/> x <paramref name="h"/>. Each frame keeps its pixels
    /// from the top-left corner (cropped or padded with transparent), so the sheet is re-laid out in one row.
    /// </summary>
    public void SetFrameSize(int w, int h)
    {
        if (w == FrameW && h == FrameH && Image.Width == FrameW * FrameCount) return;
        Library.Edit($"OAM size {w}×{h}", [], () =>
        {
            var n = FrameCount;
            var px = new uint[w * n * h];
            for (var f = 0; f < n; f++)
                for (var y = 0; y < Math.Min(h, FrameH); y++)
                    for (var x = 0; x < Math.Min(w, FrameW); x++)
                        px[y * w * n + f * w + x] = Get(f, x, y);
            Image.Replace(w * n, h, px);
            var m = Meta;
            m.FrameWidth = w;
            m.FrameHeight = h;
            m.OamSize = OamLabel(w, h).Replace('×', 'x');
            m.OriginX = Math.Min(m.OriginX, w);
            m.OriginY = Math.Min(m.OriginY, h);
            m.Collider.X = Math.Min(m.Collider.X, w - 1);
            m.Collider.Y = Math.Min(m.Collider.Y, h - 1);
            m.Collider.W = Math.Min(m.Collider.W, w - m.Collider.X);
            m.Collider.H = Math.Min(m.Collider.H, h - m.Collider.Y);
            Library.MarkMetaDirty(Path);
        }, [Path], [Path]);
    }

    /// <summary>Flips one frame horizontally (one undo step).</summary>
    public void FlipFrame(int frame)
    {
        var before = (uint[])Image.Pixels.Clone();
        int w = Image.Width, h = Image.Height;
        var px = FramePixels(frame);
        for (var y = 0; y < FrameH; y++)
            for (var x = 0; x < FrameW; x++) Set(frame, x, y, px[y * FrameW + FrameW - 1 - x]);
        Image.Touch();
        Library.RecordImageEdit("Flip frame", Path, w, h, before);
    }
}

/// <summary>Import options of the Sprite Editor drop zone (P11.24) and New Sprite › Import PNG….</summary>
public sealed record SpriteImportOptions(bool Reduce = true, bool Split = true, int FrameW = 16, int FrameH = 16, PaletteBank? ShareWith = null);

/// <summary>Imports PNG sheets as sprites: auto-reduce to 16 colours (15 + transparent), split into frames, share a bank.</summary>
public static class SpriteImport
{
    public static readonly string[] Extensions = [".png"];

    public static bool CanImport(string path) => Extensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>A free file name in assets/sprites for <paramref name="name"/> ("bat.png", then "bat_2.png" ...).</summary>
    public static string FreePath(ArtLibrary lib, string name)
    {
        var dir = System.IO.Path.Combine(lib.Project.AssetsDir, "sprites");
        var stem = GameProject.FileName(name.Replace(' ', '_'));
        if (stem.Length == 0) stem = "sprite";
        var path = System.IO.Path.Combine(dir, stem + ".png");
        for (var i = 2; File.Exists(path); i++) path = System.IO.Path.Combine(dir, $"{stem}_{i}.png");
        return path;
    }

    /// <summary>Reduces the colours of an image to <paramref name="max"/> (median cut on 15-bit colours) in place. Returns the colours it had.</summary>
    public static int Reduce(uint[] pixels, int max = 15)
    {
        var hist = new Dictionary<ushort, int>();
        foreach (var p in pixels)
            if (!Colour15.IsTransparent(p)) hist[Colour15.Of(p)] = hist.GetValueOrDefault(Colour15.Of(p)) + 1;
        if (hist.Count <= max) return hist.Count;
        var palette = Quantizer.Reduce(hist, max);
        var cache = new Dictionary<ushort, uint>();
        for (var i = 0; i < pixels.Length; i++)
        {
            if (Colour15.IsTransparent(pixels[i])) continue;
            var c = Colour15.Of(pixels[i]);
            if (!cache.TryGetValue(c, out var to)) cache[c] = to = Colour15.Argb(palette[Quantizer.Nearest(palette, c)]);
            pixels[i] = to;
        }
        return hist.Count;
    }

    /// <summary>Maps colours the bank cannot hold (it is full) to the bank's nearest colour, in place.</summary>
    public static void FitToBank(uint[] pixels, PaletteBank bank)
    {
        var have = bank.Colors.Where((_, i) => !bank.IsTransparentIndex(i)).Select(Colour15.Of).ToHashSet();
        var room = 16 - bank.Colors.Count;
        var extra = new HashSet<ushort>();
        for (var i = 0; i < pixels.Length; i++)
        {
            if (Colour15.IsTransparent(pixels[i])) continue;
            var c = Colour15.Of(pixels[i]);
            if (have.Contains(c) || extra.Contains(c)) continue;
            if (extra.Count < room) { extra.Add(c); continue; }
            pixels[i] = bank.Colors[ArtLibrary.IndexOf(bank, pixels[i])];
        }
    }

    /// <summary>Imports one PNG into assets/sprites and returns the new sprite's path. Writes the PNG and its meta.</summary>
    public static string Import(ArtLibrary lib, string source, SpriteImportOptions o)
    {
        if (!CanImport(source)) throw new InvalidDataException($"{System.IO.Path.GetFileName(source)}: only PNG sheets can be imported.");
        var png = Png.Read(source);
        var px = png.Pixels.Select(p => Colour15.IsTransparent(p) ? Colour15.Transparent : p | 0xFF000000u).ToArray();
        if (o.Reduce) Reduce(px);
        if (o.ShareWith is { } bank) FitToBank(px, bank);
        int fw = png.Width, fh = png.Height;
        if (o.Split || fw > 64 || fh > 64)
        {
            fw = Math.Min(o.FrameW, png.Width);
            fh = Math.Min(o.FrameH, png.Height);
        }
        var path = FreePath(lib, System.IO.Path.GetFileNameWithoutExtension(source));
        return Create(lib, path, png.Width, png.Height, px, fw, fh, o.ShareWith, createIdle: true, collider: null);
    }

    /// <summary>Writes a new sprite (PNG + meta), puts it in a bank (shared or a new one) and returns its path.</summary>
    public static string Create(ArtLibrary lib, string path, int w, int h, uint[] pixels, int fw, int fh, PaletteBank? share, bool createIdle, (int W, int H)? collider)
    {
        var img = lib.AddImage(path, w, h, pixels);
        var frames = Math.Max(1, w / fw) * Math.Max(1, h / fh);
        var meta = new SpriteMeta
        {
            FrameWidth = fw, FrameHeight = fh, OriginX = fw / 2, OriginY = fh,
            Collider = collider is { } c ? new ColliderData { X = (fw - c.W) / 2, Y = fh - c.H, W = c.W, H = c.H } : new ColliderData { X = 0, Y = 0, W = fw, H = fh },
        };
        if (createIdle) meta.Animations.Add(new AnimationData { Name = "idle", Frames = Enumerable.Range(0, frames).ToList(), Ticks = 6 });
        img.Save();
        lib.SetMeta(path, meta);
        var bank = share ?? lib.NewBankFor(BankKind.Obj, GameProject.AssetName(path)) ?? lib.Of(BankKind.Obj).Last();
        lib.Assign(path, bank, undoable: false);
        lib.SaveMeta(path);
        foreach (var a in bank.Assets)
            if (lib.IsMetaDirty(a)) lib.SaveMeta(a);
        return path;
    }
}
