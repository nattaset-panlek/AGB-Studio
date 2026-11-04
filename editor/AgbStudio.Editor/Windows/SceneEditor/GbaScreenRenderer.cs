using AgbStudio.Build.Fonts;
using AgbStudio.Build.Graphics;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>Layers the viewport draws (toolbar layer toggles BG0-BG3 / OBJ).</summary>
[Flags]
public enum ScreenLayers { None = 0, Bg0 = 1, Bg1 = 2, Bg2 = 4, Bg3 = 8, Obj = 16, All = 31 }

/// <summary>Where one actor's sprite is on the GBA screen (OAM entry), for picking and the selection box.</summary>
public sealed record SpritePlacement(int Actor, int Left, int Top, int Width, int Height, int SourceWidth, int SourceHeight, int Priority, int Frame, int PaletteBank);

/// <summary>One 240 x 160 frame in BGR555, with the layer each pixel came from (0-3 BG, 4 OBJ, 5 backdrop).</summary>
public sealed class ScreenFrame
{
    public const int Width = 240, Height = 160;
    public const byte FromObj = 4, FromBackdrop = 5;

    public ushort[] Bgr { get; } = new ushort[Width * Height];
    public byte[] Source { get; } = new byte[Width * Height];
    public List<SpritePlacement> Sprites { get; } = [];
    public int CameraX { get; init; }
    public int CameraY { get; init; }
    /// <summary>BG index used by the Text Label layer, or -1 when the scene has no labels.</summary>
    public int TextLayer { get; set; } = -1;
    public ushort[] BgPalette { get; } = new ushort[256];
    public ushort[] ObjPalette { get; } = new ushort[256];

    /// <summary>The colour as mGBA outputs it: 5-bit channels expanded with (c &lt;&lt; 3) | (c &gt;&gt; 2).</summary>
    public static uint ToArgb(ushort c)
    {
        uint r = (uint)c & 31, g = (uint)(c >> 5) & 31, b = (uint)(c >> 10) & 31;
        return 0xFF000000u | (((r << 3) | (r >> 2)) << 16) | (((g << 3) | (g >> 2)) << 8) | ((b << 3) | (b >> 2));
    }

    public uint[] ToArgb() => Bgr.Select(ToArgb).ToArray();
}

/// <summary>
/// D-18: draws a scene the way the ROM draws it, from the same converted data (SpriteConverter, TilesetConverter,
/// image layers, laid-out Text Labels) and the same runtime rules: scene load order, BG / OBJ palette bank
/// allocation (render/palette.cpp), BG configuration and scroll per map layer with parallax (render/map.cpp), the
/// shared text layer on BG0 at priority 0 with palette bank 15 (render/textdb.cpp), the start camera (follow actor,
/// dead zone, clamp to map) and OAM order by priority then spawn order (render/sprite.cpp), composed with the GBA
/// priority rules. Animated sprites show the first frame of their animation, as the game does right after loading.
/// </summary>
public static class GbaScreenRenderer
{
    // text::BoxStyle defaults: bg 0, palette bank 15, colours {0, ink, ink2, ink3, shadow, paper, frame}
    /// <summary>The number the editor shows after a Text Label a script writes to (HUD component field "previewNumber", e.g. the score).</summary>
    static string LabelPreview(ActorData a) =>
        a.Components.Select(c => c.Fields.GetValueOrDefault("previewNumber")).FirstOrDefault(v => !string.IsNullOrEmpty(v)) is { } n ? " " + n : "";

    static readonly ushort[] TextColors = [0, 0x0C63, 0x294A, 0x4E73, 0x6318, 0x7BDE, 0x0C63, 0x001F, 0x2D7F, 0x529F, 0x7C00, 0x7D6B, 0x7E94, 0x03E0, 0x2FEB, 0x53F4];  // 7..15: <c1>..<c3> and their shades
    const int TextBg = 0, TextBank = 15, MaxLabels = 6, MaxActors = 64, MaxSprites = 128;

    /// <summary>The camera the game starts with: the follow actor centred (120, 80), then one dead-zone step, clamped to the map.</summary>
    public static (int X, int Y) StartCamera(SceneFile scene, MapFile? map)
    {
        int w = (map?.Width ?? scene.Width) * 16, h = (map?.Height ?? scene.Height) * 16;
        int camX = 0, camY = 0;
        var follow = scene.Actors.FindIndex(a => a.Name == scene.Camera.Follow);
        if (follow >= MaxActors) follow = -1;
        void Clamp()
        {
            if (!scene.Camera.ClampToMap) return;
            camX = w <= 240 ? 0 : Math.Clamp(camX, 0, w - 240);
            camY = h <= 160 ? 0 : Math.Clamp(camY, 0, h - 160);
        }
        if (follow >= 0)
        {
            var a = scene.Actors[follow];
            camX = a.X - 120;
            camY = a.Y - 80;
        }
        Clamp();
        if (follow >= 0)
        {
            var a = scene.Actors[follow];
            int px = a.X - camX, py = a.Y - camY;
            int dzw = (byte)scene.Camera.DeadZoneW, dzh = (byte)scene.Camera.DeadZoneH;
            int left = 120 - dzw / 2, right = 120 + dzw / 2, top = 80 - dzh / 2, bottom = 80 + dzh / 2;
            if (px < left) camX -= left - px;
            if (px > right) camX += px - right;
            if (py < top) camY -= top - py;
            if (py > bottom) camY += py - bottom;
            Clamp();
        }
        return (camX, camY);
    }

    sealed class Bg
    {
        public int Priority;
        public int ScrollX, ScrollY;
        public Func<int, int, (byte Index, int Bank)>? Sample;  // BG pixel (scrolled) -> palette index and bank
        public Func<int, int, ushort?>? Direct;                 // bitmap modes 3 and 5: the screen pixel's colour, null outside the picture
    }

    sealed class PaletteBanks
    {
        readonly (object? Owner, int Refs)[] _owner = new (object?, int)[16];
        public readonly ushort[] Colors = new ushort[256];
        readonly bool _obj;
        public PaletteBanks(bool obj) => _obj = obj;

        // render::palettes::acquire: the owner's bank again, else the first run of free banks.
        public int Acquire(object owner, IReadOnlyList<ushort> colors)
        {
            var banks = Math.Max(1, colors.Count / 16);
            for (var i = 0; i < 16; i++)
                if (ReferenceEquals(_owner[i].Owner, owner) && _owner[i].Refs > 0)
                {
                    for (var b = 0; b < banks && i + b < 16; b++) _owner[i + b].Refs++;
                    return i;
                }
            for (var i = 0; i + banks <= 16; i++)
            {
                var free = true;
                for (var b = 0; b < banks; b++) free &= _owner[i + b].Refs == 0;
                if (!free) continue;
                for (var b = 0; b < banks; b++)
                {
                    _owner[i + b] = (owner, 1);
                    SetBank(i + b, colors.Skip(b * 16).Take(16).ToArray());
                }
                return i;
            }
            return -1;
        }

        public void SetBank(int bank, IReadOnlyList<ushort> colors16)
        {
            for (var i = !_obj && bank == 0 ? 1 : 0; i < 16; i++) Colors[bank * 16 + i] = i < colors16.Count ? colors16[i] : (ushort)0;
        }
    }

    /// <summary>Renders the GBA screen with the camera at (<paramref name="camX"/>, <paramref name="camY"/>).</summary>
    /// <param name="tick">Game frames since the scene loaded: animated sprites show the frame of their animation at that
    /// time (0 = the first frame, as right after loading).</param>
    public static ScreenFrame Render(SceneAssets assets, SceneFile scene, MapFile? map, int camX, int camY, ScreenLayers show = ScreenLayers.All, long tick = 0)
    {
        var frame = new ScreenFrame { CameraX = camX, CameraY = camY };
        var bgPal = new PaletteBanks(false);
        var objPal = new PaletteBanks(true);
        var bgs = new Bg?[4];

        // load step 1: backdrop
        bgPal.Colors[0] = ParseBackdrop(scene.Backdrop);

        // load steps 2-3: map layers (tileset tiles and palettes in layer order, one BG config per layer)
        var layerViews = new List<(MapLayerData Layer, int Bg)>();
        var loaded = new Dictionary<object, int>();
        // Modes 3, 4 and 5: BG2 shows the scene's picture at priority 3 (render/bitmap.cpp) and no map layer or text loads.
        var bitmap = scene.VideoMode >= 3;
        if (bitmap && assets.Picture(scene.Picture, scene.VideoMode) is { } picture)
        {
            if (picture.Indexed is { } indexed)
            {
                Array.Copy(indexed.Palette, 0, bgPal.Colors, 1, indexed.Palette.Length);
                bgs[2] = new Bg { Priority = 3, Sample = (x, y) => x is >= 0 and < 240 && y is >= 0 and < 160 ? (indexed.Indices[y * 240 + x], 0) : ((byte)0, 0) };
            }
            else
            {
                var bgr = picture.Bgr!;
                // Mode 5: 160 x 128 from the top-left corner, no scaling; the backdrop shows around it, as on the hardware.
                bgs[2] = new Bg { Priority = 3, Direct = (x, y) => x < picture.Width && y < picture.Height ? bgr[y * picture.Width + x] : null };
            }
        }
        if (map != null && !bitmap)
        {
            var tileset = assets.Tileset(map.Tileset);
            for (var i = 0; i < map.Layers.Count && i < 4; i++)
            {
                var l = map.Layers[i];
                if (l.Type == LayerType.CollisionOnly) continue;
                Func<int, int, (byte, int)>? sample = null;
                if (l.Type == LayerType.Image)
                {
                    var img = assets.Image(l.Image);
                    if (img == null) continue;
                    var bank = BankOf(img.Tiles);
                    sample = (x, y) => SampleImage(img, l.Wrap, bank, x, y);
                }
                else
                {
                    if (tileset == null || l.Cells.Count != map.Width * map.Height) continue;
                    var bank = BankOf(tileset);
                    var cells = l.Cells.ToArray();
                    sample = (x, y) => SampleTiles(tileset, cells, map.Width, map.Height, l.Wrap, bank, x, y);
                }
                var b = l.Bg & 3;
                bgs[b] = new Bg { Priority = l.Priority & 3, Sample = sample };
                layerViews.Add((l, b));
            }

            int BankOf(ConvertedTileset t)
            {
                if (loaded.TryGetValue(t, out var bank)) return bank;
                var colors = t.Palette.Flatten();
                bank = colors.Length == 0 ? 0 : Math.Max(0, bgPal.Acquire(t, colors));
                loaded[t] = bank;
                return bank;
            }
        }

        // load step 4: actors in order (sprites take OBJ palette banks), Text Labels open the text layer
        var actors = scene.Actors.Take(MaxActors).ToList();
        var sprites = new List<(int Actor, ConvertedSprite Sheet, SpriteMeta Meta, int Bank, int Frame)>();
        var labels = new List<(int TileX, int TileY, int W, int H, byte[] Pixels)>();
        for (var i = 0; i < actors.Count; i++)
        {
            var a = actors[i];
            if (a.Sprite.Length > 0 && sprites.Count < MaxSprites && assets.Sprite(a.Sprite) is { } sheet)
            {
                var meta = assets.SpriteMeta(a.Sprite);
                var bank = objPal.Acquire(sheet, sheet.Palette);
                var f = a.Animation.Length > 0 ? FrameAt(sheet, meta, a.Animation, tick) : 0;
                if (!a.Hidden) sprites.Add((i, sheet, meta, bank < 0 ? 0 : bank, f));
            }
            if (!bitmap && a.Kind == ActorKind.TextLabel && a.Text.Length > 0 && labels.Count < MaxLabels && assets.Text.HasKey(a.Text) && assets.Text.Run(a.Text, LabelPreview(a)) is { } run)
            {
                if (frame.TextLayer < 0)
                {
                    frame.TextLayer = TextLayerBg(bgs, scene.VideoMode);
                    bgPal.SetBank(TextBank, TextColors);
                    // a map layer on the same BG (no free one) is replaced: its scroll stays (set below)
                    bgs[frame.TextLayer] = new Bg { Priority = 0 };
                }
                var (wx, wy) = a.GetWorldPosition(actors);
                var lh = run.Font.LineHeight;
                int tx = wx / 8, ty = wy / 8, w = (240 - wx + 7) / 8, h = (lh * run.Lines + 7) / 8;
                if (w <= 0 || h <= 0) continue;
                var px = TextRenderer.Render(run.Layout, run.Font, w * 8, h * 8, new TextRenderer.Style(1, 4, 0));
                labels.Add((tx, ty, w, h, px));
            }
        }
        if (frame.TextLayer >= 0) bgs[frame.TextLayer]!.Sample = TextSampler(labels);

        // scroll: every map layer view sets its BG's scroll with parallax each frame (the text layer keeps 0,0
        // unless a map layer shares its BG)
        foreach (var (l, b) in layerViews)
        {
            bgs[b]!.ScrollX = (camX * Cpp.Parallax(l.ParallaxX)) >> 8;
            bgs[b]!.ScrollY = (camY * Cpp.Parallax(l.ParallaxY)) >> 8;
        }

        // OAM: by priority, then spawn order; earlier entries are in front
        var oam = sprites.Select(s => (s, Prio: actors[s.Actor].Priority & 3)).OrderBy(x => x.Prio).Take(128).ToList();
        foreach (var (s, prio) in oam)
        {
            var a = actors[s.Actor];
            var (wx, wy) = a.GetWorldPosition(actors);
            var left = wx - camX - (sbyte)s.Meta.OriginX;
            var top = wy - camY - (sbyte)s.Meta.OriginY;
            if (left >= 240 || top >= 160 || left + s.Sheet.FrameWidth <= 0 || top + s.Sheet.FrameHeight <= 0) continue;
            frame.Sprites.Add(new SpritePlacement(s.Actor, left, top, s.Sheet.FrameWidth, s.Sheet.FrameHeight, s.Sheet.SourceWidth, s.Sheet.SourceHeight, prio, s.Frame, s.Bank));
        }
        var sheets = oam.ToDictionary(x => x.s.Actor, x => x.s.Sheet);

        Array.Copy(bgPal.Colors, frame.BgPalette, 256);
        Array.Copy(objPal.Colors, frame.ObjPalette, 256);

        // compose: lowest priority number wins; OBJ beats BG on equal priority; lower BG index beats higher
        for (var y = 0; y < ScreenFrame.Height; y++)
            for (var x = 0; x < ScreenFrame.Width; x++)
            {
                var bestPrio = 4;
                byte src = ScreenFrame.FromBackdrop;
                var color = bgPal.Colors[0];
                for (var b = 0; b < 4; b++)
                {
                    var bg = bgs[b];
                    if (bg?.Direct != null)
                    {
                        if ((show & (ScreenLayers)(1 << b)) != 0 && bg.Priority < bestPrio && bg.Direct(x, y) is { } direct)
                        {
                            bestPrio = bg.Priority;
                            src = (byte)b;
                            color = direct;
                        }
                        continue;
                    }
                    if (bg?.Sample == null || ((show & (ScreenLayers)(1 << b)) == 0 && b != frame.TextLayer) || bg.Priority >= bestPrio) continue;
                    var (idx, bank) = bg.Sample(bg.ScrollX + x, bg.ScrollY + y);
                    if (idx == 0) continue;
                    bestPrio = bg.Priority;
                    src = (byte)b;
                    color = bgPal.Colors[(bank & 15) * 16 + idx];
                }
                if ((show & ScreenLayers.Obj) != 0)
                    foreach (var sp in frame.Sprites)
                    {
                        if (x < sp.Left || x >= sp.Left + sp.Width || y < sp.Top || y >= sp.Top + sp.Height) continue;
                        var idx = SpritePixel(sheets[sp.Actor], sp.Frame, x - sp.Left, y - sp.Top);
                        if (idx == 0) continue;
                        if (sp.Priority <= bestPrio)
                        {
                            src = ScreenFrame.FromObj;
                            color = objPal.Colors[sp.PaletteBank * 16 + idx];
                        }
                        break;
                    }
                frame.Bgr[y * ScreenFrame.Width + x] = color;
                frame.Source[y * ScreenFrame.Width + x] = src;
            }
        return frame;
    }

    /// <summary>The frame an animation shows <paramref name="tick"/> game frames after it started (ticks per frame,
    /// per-frame ticks, loop / ping-pong; a non-looping animation stays on its last frame).</summary>
    public static int FrameAt(ConvertedSprite sheet, SpriteMeta meta, string animation, long tick)
    {
        if (tick <= 0) return FirstFrame(sheet, meta, animation);
        var a = meta.Animations.FirstOrDefault(x => x.Name == animation);
        var frames = a?.Frames.Where(f => f < sheet.FrameCount).ToList() ?? [];
        if (a is null || frames.Count <= 1) return FirstFrame(sheet, meta, animation);
        var order = Enumerable.Range(0, frames.Count).ToList();
        if (a.PingPong) order.AddRange(Enumerable.Range(1, frames.Count - 2).Reverse());
        int Ticks(int k) => Math.Max(1, a.FrameTicks != null && k < a.FrameTicks.Count ? a.FrameTicks[k] : a.Ticks);
        var cycle = order.Sum(Ticks);
        if (!a.Loop && tick >= cycle) return frames[^1];
        var t = tick % cycle;
        foreach (var k in order)
        {
            if (t < Ticks(k)) return frames[k];
            t -= Ticks(k);
        }
        return frames[order[^1]];
    }

    /// <summary>The frame an animation starts on (GameBuilder keeps frames inside the sheet; none = frame 0).</summary>
    public static int FirstFrame(ConvertedSprite sheet, SpriteMeta meta, string animation)
    {
        var anims = meta.Animations.Count > 0 ? meta.Animations : [new AnimationData { Name = "idle", Frames = Enumerable.Range(0, sheet.FrameCount).ToList() }];
        var a = anims.FirstOrDefault(x => x.Name == animation);
        if (a == null) return 0;
        var frames = a.Frames.Where(f => f < sheet.FrameCount).ToList();
        return frames.Count == 0 ? 0 : frames[0];
    }

    public static ushort ParseBackdrop(string hex)
    {
        try { return Gba.ToBgr555(0xFF000000u | uint.Parse(hex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber)); }
        catch (FormatException) { return 0; }
        catch (OverflowException) { return 0; }
    }

    /// <summary>Palette index of a sprite frame pixel (4bpp tiles, 1D mapping, low nibble = left pixel).</summary>
    public static byte SpritePixel(ConvertedSprite s, int frame, int x, int y)
    {
        var tilesW = s.FrameWidth / 8;
        var tile = frame * s.TilesPerFrame + (y >> 3) * tilesW + (x >> 3);
        var i = tile * 32 + (y & 7) * 4 + ((x & 7) >> 1);
        if (i < 0 || i >= s.Tiles.Length) return 0;
        var b = s.Tiles[i];
        return (byte)((x & 1) == 0 ? b & 15 : b >> 4);
    }

    static (byte, int) TilePixel(TileSet4 tiles, ushort se, int bank, int x, int y)
    {
        var t = se & 0x3FF;
        if (t >= tiles.Count) return (0, 0);
        if ((se & (1 << 10)) != 0) x = 7 - x;
        if ((se & (1 << 11)) != 0) y = 7 - y;
        return (tiles.Tiles[t][y * 8 + x], ((se >> 12) + bank) & 15);
    }

    // MapLayerView::screenEntry for tile layers: metatile cell -> one of its 4 screen entries
    static (byte, int) SampleTiles(ConvertedTileset ts, int[] cells, int width, int height, bool wrap, int bank, int px, int py)
    {
        int tx = px >> 3, ty = py >> 3, w = width * 2, h = height * 2;
        if (wrap)
        {
            tx = ((tx % w) + w) % w;
            ty = ((ty % h) + h) % h;
        }
        else if (tx < 0 || ty < 0 || tx >= w || ty >= h) return (0, 0);
        var mt = cells[(ty >> 1) * width + (tx >> 1)];
        if (mt < 0 || mt >= ts.Metatiles.Count) return (0, 0);
        var se = ts.Metatiles[mt][(tx & 1) + (ty & 1) * 2];
        return TilePixel(ts.Tiles, se, bank, px & 7, py & 7);
    }

    static (byte, int) SampleImage(SceneAssets.ImageLayer img, bool wrap, int bank, int px, int py)
    {
        int tx = px >> 3, ty = py >> 3, w = img.WidthTiles, h = img.HeightTiles;
        if (wrap)
        {
            tx = ((tx % w) + w) % w;
            ty = ((ty % h) + h) % h;
        }
        else if (tx < 0 || ty < 0 || tx >= w || ty >= h) return (0, 0);
        return TilePixel(img.Tiles.Tiles, img.Cells[ty * w + tx], bank, px & 7, py & 7);
    }

    // text::pickBg (runtime/src/render/textdb.cpp): BoxStyle::bg (BG0) when the scene leaves it free, else the highest
    // regular BG of the video mode the scene does not show, else BG0 shared with the scene layer (taking its scroll).
    static int TextLayerBg(Bg?[] bgs, int videoMode)
    {
        var last = videoMode == 0 ? 3 : videoMode == 1 ? 1 : -1;
        if (TextBg <= last && bgs[TextBg] == null) return TextBg;
        for (var b = last; b >= 0; b--)
            if (bgs[b] == null) return b;
        return TextBg;
    }

    // The text layer: a 32x32 map (256 x 256 px, wrapping) whose entries point at each label box's tiles, written
    // row by row from the box's top-left tile like TextBox::remap; everything else is the blank tile.
    static Func<int, int, (byte, int)> TextSampler(List<(int TileX, int TileY, int W, int H, byte[] Pixels)> labels)
    {
        var map = new (int Box, int Tile)[1024];
        Array.Fill(map, (-1, 0));
        for (var b = 0; b < labels.Count; b++)
        {
            var (tx, ty, w, h, _) = labels[b];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var i = (ty + y) * 32 + tx + x;
                    if (i >= 0 && i < 1024) map[i] = (b, y * w + x);
                }
        }
        return (px, py) =>
        {
            px &= 255;
            py &= 255;
            var (box, tile) = map[(py >> 3) * 32 + (px >> 3)];
            if (box < 0) return (0, 0);
            var l = labels[box];
            int lx = (tile % l.W) * 8 + (px & 7), ly = (tile / l.W) * 8 + (py & 7);
            return (l.Pixels[ly * l.W * 8 + lx], TextBank);
        };
    }
}
