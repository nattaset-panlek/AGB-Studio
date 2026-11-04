using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>Converted background graphics: a tileset (metatiles of 4 screen entries) or an image layer (screen entries per 8x8 tile).</summary>
public sealed class TileGraphics
{
    public required ConvertedTileset Converted { get; init; }
    /// <summary>Image layers: one screen entry per 8x8 tile, <see cref="WidthTiles"/> x <see cref="HeightTiles"/>.</summary>
    public ushort[]? ImageCells { get; init; }
    public int WidthTiles { get; init; }
    public int HeightTiles { get; init; }

    /// <summary>ARGB of one pixel of a screen entry (0 = transparent).</summary>
    public uint Pixel(ushort se, int x, int y)
    {
        var tile = se & 0x3FF;
        var tiles = Converted.Tiles.Tiles;
        if (tile >= tiles.Count) return 0;
        if ((se & (1 << 10)) != 0) x = 7 - x;
        if ((se & (1 << 11)) != 0) y = 7 - y;
        var index = tiles[tile][y * 8 + x];
        if (index == 0) return 0;
        var banks = Converted.Palette.Banks;
        var bank = se >> 12;
        return bank < banks.Count ? Gba.ToArgb(banks[bank][index]) : 0;
    }

    /// <summary>One pixel of a metatile (16x16, optionally flipped horizontally as a whole).</summary>
    public uint MetatilePixel(int metatile, int x, int y, bool flip)
    {
        var metas = Converted.Metatiles;
        if (metatile < 0 || metatile >= metas.Count) return 0;
        if (flip) x = 15 - x;
        var se = metas[metatile][(x >> 3) + (y >> 3) * 2];
        return Pixel(se, x & 7, y & 7);
    }
}

/// <summary>
/// The editor's software renderer for maps (D-18): draws from the same converted tiles, palettes, metatiles and screen
/// entries the build puts in the ROM (TilesetConverter), with the runtime's layer order, parallax and wrap rules, and
/// the scene's actors as sprites. Used by the Tilemap Editor canvas, the tileset panel and the Font Manager preview.
/// </summary>
public sealed class MapRenderer
{
    readonly Dictionary<string, (int Version, TileGraphics G)> _tilesets = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, TileGraphics?> _images = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (Png.Image Png, SpriteMeta Meta)?> _sprites = new(StringComparer.OrdinalIgnoreCase);

    public TileGraphics? Tileset(MapDocument doc)
    {
        if (doc.TilesetImage is null) return null;
        var key = doc.Map.Tileset;
        if (_tilesets.TryGetValue(key, out var hit) && hit.Version == doc.TilesetVersion) return hit.G;
        var converted = TilesetConverter.Convert(key, doc.TilesetImage, doc.Meta, doc.Project.Settings.Tags, 13, doc.TilesetPath ?? "");
        var g = new TileGraphics { Converted = converted };
        _tilesets[key] = (doc.TilesetVersion, g);
        return g;
    }

    public TileGraphics? Image(GameProject project, string name)
    {
        if (name.Length == 0) return null;
        if (_images.TryGetValue(name, out var hit)) return hit;
        var file = project.ImageFiles().FirstOrDefault(f => string.Equals(GameProject.AssetName(f), name, StringComparison.OrdinalIgnoreCase));
        TileGraphics? g = null;
        if (file != null)
        {
            var (t, cells, w, h) = TilesetConverter.ConvertImage(name, ColorImage.Load(file));
            g = new TileGraphics { Converted = t, ImageCells = cells, WidthTiles = w, HeightTiles = h };
        }
        _images[name] = g;
        return g;
    }

    /// <summary>The graphics each layer loads, for <see cref="VramPlan"/>.</summary>
    public IReadOnlyList<LayerGraphics> LayerGraphics(MapDocument doc) =>
        doc.Map.Layers.Select(l =>
        {
            if (l.Type == LayerType.CollisionOnly) return new LayerGraphics(null, 0, 0, true);
            var g = l.Type == LayerType.Image ? Image(doc.Project, l.Image) : Tileset(doc);
            return g is null ? new LayerGraphics(null, 0, 0, false)
                : new LayerGraphics((l.Type == LayerType.Image ? "image:" + l.Image : "tileset:" + doc.Map.Tileset), g.Converted.Tiles.Count, g.Converted.Palette.Banks.Count, false);
        }).ToList();

    public VramPlan Plan(MapDocument doc) => VramPlan.Compute(LayerGraphics(doc), doc.BgSize, doc.Bpp == 8);

    /// <summary>A metatile as 16x16 ARGB (tileset panel, rule cards).</summary>
    public uint[] Metatile(MapDocument doc, int metatile, bool flip = false)
    {
        var px = new uint[256];
        if (Tileset(doc) is not { } g) return px;
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++) px[y * 16 + x] = g.MetatilePixel(metatile, x, y, flip);
        return px;
    }

    public static int Scroll(int camera, double parallax) => (camera * (int)Math.Round(parallax * 256)) >> 8;

    /// <summary>Renders the screen window (camera at camX, camY in map pixels) as ARGB, like the hardware composes it.</summary>
    public uint[] Render(MapDocument doc, int camX, int camY, int width, int height, Func<int, bool>? layerVisible = null, bool sprites = true)
    {
        var px = new uint[width * height];
        Array.Fill(px, Backdrop(doc.Scene));
        var expanded = OverLayers.Expand(doc.Map, Tileset(doc)?.Converted);
        var layers = expanded.Map.Layers;
        // An over layer shows when the layer it belongs to does.
        int Source(int li) => li < doc.Map.Layers.Count ? li : Array.IndexOf(expanded.Partner, li);
        // Priority 3 first, then 2, 1, 0; within a priority BG3 under BG0; sprites of priority p above BGs of priority p.
        for (var prio = 3; prio >= 0; prio--)
        {
            foreach (var li in Enumerable.Range(0, layers.Count).Where(i => layers[i].Priority == prio && layers[i].Type != LayerType.CollisionOnly).OrderByDescending(i => layers[i].Bg))
            {
                if (layerVisible != null && !layerVisible(Source(li))) continue;
                DrawLayer(doc, layers[li], px, camX, camY, width, height);
            }
            if (sprites) DrawSprites(doc, px, camX, camY, width, height, prio);
        }
        return px;
    }

    static uint Backdrop(SceneFile? scene)
    {
        var hex = scene?.Backdrop?.TrimStart('#');
        if (hex is { Length: 6 } && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return Gba.ToArgb(Gba.ToBgr555(0xFF000000u | rgb));
        return 0xFF000000u;
    }

    void DrawLayer(MapDocument doc, MapLayerData l, uint[] px, int camX, int camY, int width, int height)
    {
        var sx = Scroll(camX, l.ParallaxX);
        var sy = Scroll(camY, l.ParallaxY);
        if (l.Type == LayerType.Image)
        {
            if (Image(doc.Project, l.Image) is not { ImageCells: { } cells } g) return;
            int iw = g.WidthTiles * 8, ih = g.HeightTiles * 8;
            for (var y = 0; y < height; y++)
            {
                var my = sy + y;
                if (l.Wrap) my = ((my % ih) + ih) % ih;
                else if (my < 0 || my >= ih) continue;
                for (var x = 0; x < width; x++)
                {
                    var mx = sx + x;
                    if (l.Wrap) mx = ((mx % iw) + iw) % iw;
                    else if (mx < 0 || mx >= iw) continue;
                    var c = g.Pixel(cells[(my >> 3) * g.WidthTiles + (mx >> 3)], mx & 7, my & 7);
                    if (c != 0) px[y * width + x] = c;
                }
            }
            return;
        }
        if (Tileset(doc) is not { } ts || l.Cells.Count != doc.Width * doc.Height) return;
        int w = doc.Width * 16, h = doc.Height * 16;
        for (var y = 0; y < height; y++)
        {
            var my = sy + y;
            if (l.Wrap) my = ((my % h) + h) % h;
            else if (my < 0 || my >= h) continue;
            for (var x = 0; x < width; x++)
            {
                var mx = sx + x;
                if (l.Wrap) mx = ((mx % w) + w) % w;
                else if (mx < 0 || mx >= w) continue;
                var cell = l.Cells[(my >> 4) * doc.Width + (mx >> 4)];
                var c = ts.MetatilePixel(cell & MapDocument.TileMask, mx & 15, my & 15, (cell & MapDocument.FlipH) != 0);
                if (c != 0) px[y * width + x] = c;
            }
        }
    }

    (Png.Image Png, SpriteMeta Meta)? Sprite(GameProject project, string name)
    {
        if (_sprites.TryGetValue(name, out var hit)) return hit;
        var file = project.SpriteFiles().FirstOrDefault(f => string.Equals(GameProject.AssetName(f), name, StringComparison.OrdinalIgnoreCase) && f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        (Png.Image, SpriteMeta)? s = file is null ? null : (Png.Read(file), project.LoadMeta<SpriteMeta>(file));
        _sprites[name] = s;
        return s;
    }

    void DrawSprites(MapDocument doc, uint[] px, int camX, int camY, int width, int height, int prio)
    {
        if (doc.Scene is null) return;
        // OAM order: the first actor is drawn on top, so draw in reverse
        foreach (var a in Enumerable.Reverse(doc.Scene.Actors))
        {
            if (a.Hidden || a.Sprite.Length == 0 || Math.Clamp(a.Priority, 0, 3) != prio) continue;
            if (Sprite(doc.Project, a.Sprite) is not { } s) continue;
            var (png, meta) = s;
            var fw = Math.Min(meta.FrameWidth, png.Width);
            var fh = Math.Min(meta.FrameHeight, png.Height);
            var frame = meta.Animations.FirstOrDefault(an => an.Name == a.Animation)?.Frames.FirstOrDefault() ?? 0;
            var cols = Math.Max(1, png.Width / Math.Max(1, fw));
            var ox = frame % cols * fw;
            var oy = frame / cols * fh;
            if (oy + fh > png.Height) { ox = 0; oy = 0; }
            var x0 = a.X - meta.OriginX - camX;
            var y0 = a.Y - meta.OriginY - camY;
            for (var y = 0; y < fh; y++)
                for (var x = 0; x < fw; x++)
                {
                    int dx = x0 + x, dy = y0 + y;
                    if (dx < 0 || dy < 0 || dx >= width || dy >= height) continue;
                    var c = png.Pixels[(oy + y) * png.Width + ox + x];
                    if (Gba.IsTransparent(c)) continue;
                    px[dy * width + dx] = Gba.ToArgb(Gba.ToBgr555(c));
                }
        }
    }

    /// <summary>The scene camera at start: centred on the followed actor and clamped to the map, as the runtime does.</summary>
    public static (int X, int Y) StartCamera(MapDocument doc)
    {
        int w = doc.Width * 16, h = doc.Height * 16;
        var follow = doc.Scene?.Actors.FirstOrDefault(a => a.Name == doc.Scene.Camera.Follow) ?? doc.Scene?.Actors.FirstOrDefault(a => a.Kind == ActorKind.Player);
        int x = follow is null ? 0 : follow.X - 120, y = follow is null ? 0 : follow.Y - 80;
        return (w <= 240 ? 0 : Math.Clamp(x, 0, w - 240), h <= 160 ? 0 : Math.Clamp(y, 0, h - 160));
    }
}

/// <summary>ARGB pixel buffers to Avalonia bitmaps (game pixels are data, not UI colours).</summary>
public static class Pixels
{
    public static WriteableBitmap ToBitmap(uint[] argb, int width, int height)
    {
        var bmp = new WriteableBitmap(new PixelSize(Math.Max(1, width), Math.Max(1, height)), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        var row = new int[width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = argb[y * width + x];
                var a = c >> 24;
                if (a == 0) { row[x] = 0; continue; }
                if (a != 255)
                {
                    uint r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
                    c = (a << 24) | (r * a / 255 << 16) | (g * a / 255 << 8) | (b * a / 255);
                }
                row[x] = unchecked((int)c);
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, width);
        }
        return bmp;
    }
}
