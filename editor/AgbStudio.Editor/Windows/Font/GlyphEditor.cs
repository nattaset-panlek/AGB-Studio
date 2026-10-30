using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Font;

/// <summary>Font Manager tools: Pencil, Eraser, Pick (shade), plus the pixel grid toggle.</summary>
public enum FontTool { Pencil, Eraser, Pick }

/// <summary>
/// The glyph detail of the Font Manager: the selected glyph's pixels (shades of the built font) on an 18 px grid with
/// the baseline in accent; Pencil / Eraser / Pick edit them and the stroke is saved as a glyph override in the .font.
/// Pixels above the source glyph's top or left of the pen cannot be stored in an override and are not editable.
/// </summary>
public sealed class GlyphEditor : Control
{
    public const double Pitch = 18, Cell = 16;
    FontEntry? _font;
    byte[] _px = [];
    bool _stroke;

    public int CodePoint { get; private set; }
    public int GridTop { get; private set; }
    public int Rows { get; private set; }
    public int Left { get; private set; }
    public int Columns { get; private set; }
    public int EditTop { get; private set; }
    public int Baseline { get; private set; }
    public FontTool Tool { get; set; } = FontTool.Pencil;
    public byte Shade { get; set; } = 3;
    bool _grid = true;
    public bool ShowGrid { get => _grid; set { _grid = value; InvalidateVisual(); } }
    public event Action? ShadePicked;

    public GlyphEditor()
    {
        Name = "GlyphEditor";
        Focusable = true;
    }

    public void Load(FontEntry font, int cp)
    {
        _font = font;
        CodePoint = cp;
        var gf = font.Font;
        var g = gf.Get(cp);
        var emTop = Math.Max(0, gf.Baseline - font.File.Baseline);
        int top = emTop, bottom = emTop + Math.Max(1, font.File.Size);
        var has = g is { Width: > 0, Height: > 0 };
        if (has) { top = Math.Min(top, g!.Top); bottom = Math.Max(bottom, g.Top + g.Height); }
        bottom = Math.Max(bottom, gf.Baseline + 1);
        top = Math.Max(0, top);
        var x0 = has ? Math.Min(0, g!.Left) : 0;
        var x1 = Math.Max((g?.Advance ?? font.File.Size / 2) + 1, has ? g!.Left + g.Width : 0);
        x1 = Math.Max(x1, x0 + 4);
        GridTop = top;
        Rows = bottom - top;
        Left = x0;
        Columns = x1 - x0;
        EditTop = g?.Top ?? 0;
        Baseline = gf.Baseline;
        _px = new byte[Rows * Columns];
        if (has)
            for (var y = 0; y < g!.Height; y++)
                for (var x = 0; x < g.Width; x++)
                {
                    var gy = g.Top + y - top;
                    var gx = g.Left + x - x0;
                    if (gy >= 0 && gy < Rows && gx >= 0 && gx < Columns) _px[gy * Columns + gx] = g.Shades[y * g.Width + x];
                }
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shade of a pixel in font coordinates (x from the pen, y from the line top).</summary>
    public byte PixelAt(int x, int y)
    {
        int gx = x - Left, gy = y - GridTop;
        return gx < 0 || gy < 0 || gx >= Columns || gy >= Rows ? (byte)0 : _px[gy * Columns + gx];
    }

    protected override Size MeasureOverride(Size availableSize) => new(Columns * Pitch, Rows * Pitch);

    IBrush? Res(string key) => this.TryFindResource(key, ActualThemeVariant, out var r) ? r as IBrush : null;

    public override void Render(DrawingContext ctx)
    {
        var size = new Rect(0, 0, Columns * Pitch, Rows * Pitch);
        ctx.FillRectangle(Res(_grid ? "bg/raised" : "bg/sunken")!, size);
        var empty = Res("bg/sunken")!;
        var bpp1 = _font?.Font.Bpp == 1;
        IBrush? Ink(byte s) => s == 0 ? null : bpp1 || s >= 3 ? Res("text/primary") : s == 2 ? Res("text/secondary") : Res("text/muted");
        for (var y = 0; y < Rows; y++)
            for (var x = 0; x < Columns; x++)
            {
                var r = _grid ? new Rect(x * Pitch, y * Pitch, Cell, Cell) : new Rect(x * Pitch, y * Pitch, Pitch, Pitch);
                ctx.FillRectangle(Ink(_px[y * Columns + x]) ?? empty, r);
            }
        var baseY = (Baseline - GridTop) * Pitch - 2;
        if (baseY > 0) ctx.FillRectangle(Res("accent/default")!, new Rect(0, baseY, Columns * Pitch, 2));
    }

    (int X, int Y)? CellAt(Point p)
    {
        var x = (int)Math.Floor(p.X / Pitch);
        var y = (int)Math.Floor(p.Y / Pitch);
        return x < 0 || y < 0 || x >= Columns || y >= Rows ? null : (x, y);
    }

    bool Apply((int X, int Y) c)
    {
        if (c.X < 0 || c.Y < 0 || c.X >= Columns || c.Y >= Rows) return false;
        var i = c.Y * Columns + c.X;
        if (Tool == FontTool.Pick)
        {
            if (_px[i] != 0) { Shade = _px[i]; ShadePicked?.Invoke(); }
            return false;
        }
        // an override starts at the pen and at the source glyph's top
        if (c.X + Left < 0 || c.Y + GridTop < EditTop) return false;
        var v = Tool == FontTool.Eraser ? (byte)0 : _font?.Font.Bpp == 1 ? (byte)1 : Shade;
        if (_px[i] == v) return false;
        _px[i] = v;
        _changed = true;
        InvalidateVisual();
        return true;
    }

    bool _changed;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_font is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || CellAt(e.GetPosition(this)) is not { } c) return;
        _stroke = Tool != FontTool.Pick;
        _changed = false;
        Focus();
        e.Pointer.Capture(this);
        Apply(c);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_stroke && CellAt(e.GetPosition(this)) is { } c) Apply(c);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        if (!_stroke || _font is null) return;
        _stroke = false;
        if (_changed) Commit();
    }

    /// <summary>Writes the pixels from the editable top and the pen as the glyph override.</summary>
    public void Commit()
    {
        if (_font is null) return;
        var top = Math.Max(EditTop, GridTop);
        int width = Columns + Left, rows = GridTop + Rows - top;
        if (width <= 0 || rows <= 0) return;
        var shades = new byte[width * rows];
        for (var y = 0; y < rows; y++)
            for (var x = 0; x < width; x++) shades[y * width + x] = PixelAt(x, top + y);
        // pixels between the source top and the grid top (none when the grid covers the glyph) stay empty
        if (EditTop < GridTop)
        {
            var pad = GridTop - EditTop;
            var padded = new byte[width * (rows + pad)];
            Array.Copy(shades, 0, padded, pad * width, shades.Length);
            shades = padded;
        }
        _font.SetGlyphPixels(CodePoint, width, shades);
    }

    /// <summary>Sets one pixel and commits (tests, keyboard-free editing).</summary>
    public bool Paint(int x, int y)
    {
        if (!Apply((x - Left, y - GridTop))) return false;
        Commit();
        return true;
    }
}

/// <summary>Game-text previews drawn with the ROM's layout and rasteriser (TextLayout + TextRenderer, P8.09).</summary>
public static class TextPreview
{
    public const string ThaiSpeaker = "ฮอปเปอร์";
    public const string ThaiLine = "เหรียญหายไปไหนหมดนะ? ต้องรีบตามหาก่อนพระอาทิตย์ตก!";
    public const string LatinSpeaker = "Hopper";
    public const string LatinLine = "Where did all the coins go? I have to find them before sunset!";
    public const uint NameInk = 0xFFFFD84A;

    static uint Parse(string? hex, uint fallback)
    {
        var h = hex?.TrimStart('#');
        return h is { Length: 6 } && uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var rgb) ? 0xFF000000u | rgb : fallback;
    }

    static uint Mix(uint a, uint b, double t)
    {
        uint C(int sh) => (uint)Math.Round(((a >> sh) & 0xFF) * (1 - t) + ((b >> sh) & 0xFF) * t);
        return 0xFF000000u | (C(16) << 16) | (C(8) << 8) | C(0);
    }

    /// <summary>Text palette for TextRenderer indices: 0 none, 1..3 ink shades (darkest first), 4 shadow.</summary>
    public static uint[] Palette(uint ink, uint paper) => [0, ink, Mix(ink, paper, 0.35), Mix(ink, paper, 0.65), Mix(paper, 0xFF000000u, 0.6)];

    /// <summary>Draws laid-out text into an ARGB buffer (index 0 keeps what is there).</summary>
    public static void Draw(uint[] dst, int dstW, int dstH, int x0, int y0, int w, int h, TextLayout.Result layout, GameFont font, uint[] palette, int firstLine = 0)
    {
        var idx = TextRenderer.Render(layout, font, w, h, new TextRenderer.Style(1, 4, 0), firstLine);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = idx[y * w + x];
                if (i == 0 || i >= palette.Length) continue;
                int dx = x0 + x, dy = y0 + y;
                if (dx >= 0 && dy >= 0 && dx < dstW && dy < dstH) dst[dy * dstW + dx] = palette[i];
            }
    }

    /// <summary>A line of text on a transparent background (New Font preview).</summary>
    public static (uint[] Pixels, int Width, int Height) Line(string text, GameFont font, uint ink, uint paper)
    {
        var layout = TextLayout.Layout(text, font, 4096, false);
        var w = Math.Max(1, layout.Glyphs.Select(g => g.X + (font.Get(g.CodePoint) is { } gl ? gl.Left + gl.Width + 1 : 0)).DefaultIfEmpty(1).Max() + 1);
        var h = font.LineHeight + 1;
        var px = new uint[w * h];
        Draw(px, w, h, 0, 0, w, h, layout, font, Palette(ink, paper));
        return (px, w, h);
    }

    /// <summary>
    /// Font Manager "Preview · Dialogue box on GBA": the scene as the ROM draws it (240 x 160) with a dialogue box in
    /// the project's UI colours, the speaker and two lines laid out and drawn exactly as the runtime does, and the
    /// runtime's continue marker.
    /// </summary>
    public static uint[] Dialogue(FontEntry font, MapDocument? scene, MapRenderer renderer, UiSettings ui)
    {
        const int W = 240, H = 160;
        uint[] px;
        if (scene != null)
        {
            var (cx, cy) = MapRenderer.StartCamera(scene);
            px = renderer.Render(scene, cx, cy, W, H);
        }
        else px = Enumerable.Repeat(0xFF000000u, W * H).ToArray();
        var gf = font.Font;
        var thai = font.File.CharacterSets.Contains("Thai");
        var paper = Parse(ui.Screen, 0xFF1E1A28);
        var ink = Parse(ui.ScreenInk, 0xFFE6E8EE);
        int lh = gf.LineHeight, bw = 228, bh = Math.Min(H - 8, 3 * lh + 6), bx = 6, by = H - bh - 6;
        for (var y = 0; y < bh; y++)
            for (var x = 0; x < bw; x++)
                px[(by + y) * W + bx + x] = x == 0 || y == 0 || x == bw - 1 || y == bh - 1 ? ink : paper;
        var name = TextLayout.Layout(thai ? ThaiSpeaker : LatinSpeaker, gf, bw - 16, false);
        Draw(px, W, H, bx + 8, by + 3, bw - 16, lh, name, gf, Palette(NameInk, paper));
        var text = TextLayout.Layout(thai ? ThaiLine : LatinLine, gf, bw - 16, font.File.ThaiWordWrap);
        Draw(px, W, H, bx + 8, by + 3 + lh, bw - 16, Math.Min(2, Math.Max(1, text.Lines)) * lh, text, gf, Palette(ink, paper));
        // continue marker: a 5 px down-pointing triangle in the bottom-right corner (runtime TextBox::showContinueMarker)
        int mx = bx + bw - 10, my = by + bh - 7;
        for (var r = 0; r < 3; r++)
            for (var c = r; c < 5 - r; c++) px[(my + r) * W + mx + c] = ink;
        return px;
    }
}
