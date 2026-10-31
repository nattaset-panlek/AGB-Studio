using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Palette;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AgbStudio.Editor.Windows.Sprite;

/// <summary>The drawing tools of the Sprite Editor toolbar, in Figma order.</summary>
public enum DrawTool { Pencil, Eraser, Fill, Line, Rectangle, Pipette }

/// <summary>Pixels of one frame drawn at an integer scale, optionally on the scene backdrop (previews, frame thumbs).</summary>
public sealed class PixelView : Control
{
    uint[] _pixels = [];
    int _w = 1, _h = 1;
    double _scale = 1, _pad;
    uint? _backdrop;
    bool _mirror;

    public void Show(uint[] pixels, int w, int h, double scale, uint? backdrop = null, double pad = 0, bool mirror = false)
    {
        _pixels = pixels;
        _w = Math.Max(1, w);
        _h = Math.Max(1, h);
        _scale = scale;
        _backdrop = backdrop;
        _pad = pad;
        _mirror = mirror;
        InvalidateMeasure();
        InvalidateVisual();
    }

    public uint[] Pixels => _pixels;
    public double Scale => _scale;

    protected override Size MeasureOverride(Size availableSize) => new(_w * _scale + 2 * _pad, _h * _scale + 2 * _pad);

    public override void Render(DrawingContext ctx)
    {
        if (_backdrop is { } b) ctx.DrawRectangle(new ImmutableSolidColorBrush(Colour15.ToColor(b)), null, new Rect(Bounds.Size), 4, 4);
        var brushes = new Dictionary<uint, IBrush>();
        for (var y = 0; y < _h; y++)
            for (var x = 0; x < _w; x++)
            {
                var i = y * _w + (_mirror ? _w - 1 - x : x);
                if (i >= _pixels.Length) continue;
                var p = _pixels[i];
                if (Colour15.IsTransparent(p)) continue;
                if (!brushes.TryGetValue(p, out var br)) brushes[p] = br = new ImmutableSolidColorBrush(Colour15.ToColor(p));
                ctx.DrawRectangle(br, null, new Rect(_pad + x * _scale, _pad + y * _scale, _scale, _scale));
            }
    }
}

/// <summary>
/// The Sprite Editor pixel canvas (P11.22): the current frame zoomed to whole pixels on a transparency checker, the
/// pixel grid, the 8x8 tile grid, the cursor cell, onion skin of the previous frame and Mirror X (OAM horizontal
/// flip preview). Draws with the toolbar tools; every stroke is one undo step.
/// </summary>
public sealed class PixelCanvas : Control
{
    public static readonly StyledProperty<IBrush?> CheckerLightProperty = AvaloniaProperty.Register<PixelCanvas, IBrush?>(nameof(CheckerLight));
    public static readonly StyledProperty<IBrush?> CheckerDarkProperty = AvaloniaProperty.Register<PixelCanvas, IBrush?>(nameof(CheckerDark));
    public static readonly StyledProperty<IBrush?> GridBrushProperty = AvaloniaProperty.Register<PixelCanvas, IBrush?>(nameof(GridBrush));
    public static readonly StyledProperty<IBrush?> TileGridBrushProperty = AvaloniaProperty.Register<PixelCanvas, IBrush?>(nameof(TileGridBrush));
    public static readonly StyledProperty<IBrush?> CursorBrushProperty = AvaloniaProperty.Register<PixelCanvas, IBrush?>(nameof(CursorBrush));

    public IBrush? CheckerLight { get => GetValue(CheckerLightProperty); set => SetValue(CheckerLightProperty, value); }
    public IBrush? CheckerDark { get => GetValue(CheckerDarkProperty); set => SetValue(CheckerDarkProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    /// <summary>When the pixel grid shows (Preferences › Appearance: at 400% and above, or never); 4x by default.</summary>
    public Func<double, bool> PixelGrid { get; set; } = z => z >= 4;
    public IBrush? TileGridBrush { get => GetValue(TileGridBrushProperty); set => SetValue(TileGridBrushProperty, value); }
    public IBrush? CursorBrush { get => GetValue(CursorBrushProperty); set => SetValue(CursorBrushProperty, value); }

    static PixelCanvas() => AffectsRender<PixelCanvas>(CheckerLightProperty, CheckerDarkProperty, GridBrushProperty, TileGridBrushProperty, CursorBrushProperty);

    SpriteDoc? _doc;
    int _frame;
    bool _drawing;
    uint[]? _before;
    int _beforeW, _beforeH;
    (int X, int Y) _start, _last;
    List<(int X, int Y)>? _shape;

    public PixelCanvas()
    {
        this.Token(CheckerLightProperty, "bg/raised");
        this.Token(CheckerDarkProperty, "bg/panel");
        this.Token(GridBrushProperty, "border/default");
        this.Token(TileGridBrushProperty, "accent/default");
        this.Token(CursorBrushProperty, "lit/white");
        ClipToBounds = true;
        Focusable = true;
    }

    public SpriteDoc? Doc { get => _doc; set { _doc = value; InvalidateVisual(); } }
    public int Frame { get => _frame; set { _frame = value; InvalidateVisual(); } }
    /// <summary>Frame drawn faintly under the current one (onion skin), or -1.</summary>
    public int OnionFrame { get; set; } = -1;
    public bool ShowTileGrid { get; set; } = true;
    public bool Mirror { get; set; }
    public DrawTool Tool { get; set; } = DrawTool.Pencil;
    /// <summary>Palette index the pencil, line, rectangle and fill draw with.</summary>
    public int ColorIndex { get; set; } = 1;
    /// <summary>True: the largest whole zoom that fits; false: <see cref="ManualZoom"/>.</summary>
    public bool Fit { get; set; } = true;
    public int ManualZoom { get; set; } = 8;
    public (int X, int Y)? Hover { get; private set; }

    public event Action? HoverChanged;
    public event Action<int>? Picked;
    public event Action? ZoomChanged;

    public int Zoom
    {
        get
        {
            if (_doc is null) return 1;
            if (!Fit) return Math.Clamp(ManualZoom, 1, 64);
            var zx = (Bounds.Width - 120) / _doc.FrameW;
            var zy = (Bounds.Height - 124) / _doc.FrameH;
            return Math.Max(1, (int)Math.Floor(Math.Min(zx, zy)));
        }
    }

    public Rect FrameRect
    {
        get
        {
            if (_doc is null) return default;
            var z = Zoom;
            double w = _doc.FrameW * z, h = _doc.FrameH * z;
            return new Rect(Math.Round((Bounds.Width - w) / 2), Math.Round((Bounds.Height - h) / 2), w, h);
        }
    }

    public void Refresh() => InvalidateVisual();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ZoomChanged?.Invoke();
    }

    uint Shown(int frame, int x, int y) => _doc!.Get(frame, Mirror ? _doc.FrameW - 1 - x : x, y);

    public override void Render(DrawingContext ctx)
    {
        if (_doc is null) return;
        var r = FrameRect;
        var z = Zoom;
        int w = _doc.FrameW, h = _doc.FrameH;
        var half = z / 2.0;
        var shape = _shape?.ToHashSet();
        var draw = DrawColor();
        var brushes = new Dictionary<uint, IBrush>();
        IBrush Brush(uint p) => brushes.TryGetValue(p, out var b) ? b : brushes[p] = new ImmutableSolidColorBrush(Colour15.ToColor(p));
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var cell = new Rect(r.X + x * z, r.Y + y * z, z, z);
                var p = shape != null && shape.Contains((x, y)) ? draw : Shown(_frame, x, y);
                if (Colour15.IsTransparent(p))
                {
                    ctx.DrawRectangle(CheckerLight, null, new Rect(cell.X, cell.Y, half, half));
                    ctx.DrawRectangle(CheckerDark, null, new Rect(cell.X + half, cell.Y, z - half, half));
                    ctx.DrawRectangle(CheckerDark, null, new Rect(cell.X, cell.Y + half, half, z - half));
                    ctx.DrawRectangle(CheckerLight, null, new Rect(cell.X + half, cell.Y + half, z - half, z - half));
                    if (OnionFrame >= 0 && OnionFrame != _frame && !Colour15.IsTransparent(Shown(OnionFrame, x, y)))
                        using (ctx.PushOpacity(0.35)) ctx.DrawRectangle(Brush(Shown(OnionFrame, x, y)), null, cell);
                    // the pixel grid shows on empty pixels only, so the art reads as solid shapes
                    if (PixelGrid(z) && GridBrush != null)
                    {
                        if (x > 0) ctx.DrawRectangle(GridBrush, null, new Rect(cell.X, cell.Y, 1, z));
                        if (y > 0) ctx.DrawRectangle(GridBrush, null, new Rect(cell.X, cell.Y, z, 1));
                    }
                }
                else ctx.DrawRectangle(Brush(p), null, cell);
            }
        if (ShowTileGrid && TileGridBrush != null)
        {
            var pen = new Pen(TileGridBrush, 1);
            for (var x = 8; x < w; x += 8) ctx.DrawLine(pen, new Point(r.X + x * z + 0.5, r.Y), new Point(r.X + x * z + 0.5, r.Bottom));
            for (var y = 8; y < h; y += 8) ctx.DrawLine(pen, new Point(r.X, r.Y + y * z + 0.5), new Point(r.Right, r.Y + y * z + 0.5));
        }
        if (Hover is { } hv && CursorBrush != null && z >= 4)
            ctx.DrawRectangle(null, new Pen(CursorBrush, 2), new Rect(r.X + hv.X * z + 1, r.Y + hv.Y * z + 1, z - 2, z - 2));
    }

    uint DrawColor()
    {
        if (Tool == DrawTool.Eraser || _doc?.Bank is not { } bank || ColorIndex >= bank.Colors.Count || bank.IsTransparentIndex(ColorIndex)) return Colour15.Transparent;
        return bank.Colors[ColorIndex] | 0xFF000000u;
    }

    /// <summary>The frame pixel under a point of the control (Mirror X applied), or null outside the frame.</summary>
    public (int X, int Y)? PixelAt(Point p)
    {
        if (_doc is null) return null;
        var r = FrameRect;
        var z = Zoom;
        var x = (int)Math.Floor((p.X - r.X) / z);
        var y = (int)Math.Floor((p.Y - r.Y) / z);
        if (x < 0 || y < 0 || x >= _doc.FrameW || y >= _doc.FrameH) return null;
        return (x, y);
    }

    /// <summary>Screen position of the centre of a frame pixel (tests and E2E clicks).</summary>
    public Point CellCenter(int x, int y)
    {
        var r = FrameRect;
        var z = Zoom;
        return new Point(r.X + x * z + z / 2.0, r.Y + y * z + z / 2.0);
    }

    int FrameX(int shownX) => Mirror && _doc != null ? _doc.FrameW - 1 - shownX : shownX;

    void SetHover((int X, int Y)? h)
    {
        if (Hover == h) return;
        Hover = h;
        HoverChanged?.Invoke();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_doc is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        if (PixelAt(e.GetPosition(this)) is not { } c) return;
        e.Handled = true;
        switch (Tool)
        {
            case DrawTool.Pipette:
                Picked?.Invoke(_doc.Bank is { } bank ? ArtLibrary.IndexOf(bank, _doc.Get(_frame, FrameX(c.X), c.Y)) : 0);
                return;
            case DrawTool.Fill:
                Begin();
                FloodFill(FrameX(c.X), c.Y, DrawColor());
                Commit("Fill");
                return;
        }
        Begin();
        _drawing = true;
        _start = _last = c;
        e.Pointer.Capture(this);
        if (Tool is DrawTool.Pencil or DrawTool.Eraser) Plot(c.X, c.Y);
        else { _shape = Shape(_start, c); InvalidateVisual(); }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var c = PixelAt(e.GetPosition(this));
        SetHover(c);
        if (!_drawing || _doc is null) return;
        var p = c ?? Clamp(e.GetPosition(this));
        if (Tool is DrawTool.Pencil or DrawTool.Eraser)
        {
            foreach (var (x, y) in Line(_last, p)) Plot(x, y);
            _last = p;
        }
        else
        {
            _shape = Shape(_start, p);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_drawing) return;
        _drawing = false;
        e.Pointer.Capture(null);
        if (_shape != null && _doc != null)
        {
            var color = DrawColor();
            foreach (var (x, y) in _shape) _doc.Set(_frame, FrameX(x), y, color);
            _shape = null;
        }
        Commit(Tool switch { DrawTool.Eraser => "Erase", DrawTool.Line => "Line", DrawTool.Rectangle => "Rectangle", _ => "Draw" });
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_drawing) SetHover(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || _doc is null) return;
        var z = Zoom;
        ManualZoom = Math.Clamp(e.Delta.Y > 0 ? z + Math.Max(1, z / 8) : z - Math.Max(1, z / 8), 1, 64);
        Fit = false;
        ZoomChanged?.Invoke();
        InvalidateVisual();
        e.Handled = true;
    }

    (int X, int Y) Clamp(Point p)
    {
        var r = FrameRect;
        var z = Zoom;
        return (Math.Clamp((int)Math.Floor((p.X - r.X) / z), 0, _doc!.FrameW - 1), Math.Clamp((int)Math.Floor((p.Y - r.Y) / z), 0, _doc.FrameH - 1));
    }

    void Begin()
    {
        var img = _doc!.Image;
        _before = (uint[])img.Pixels.Clone();
        _beforeW = img.Width;
        _beforeH = img.Height;
    }

    void Commit(string name)
    {
        if (_doc is null || _before is null) return;
        var img = _doc.Image;
        if (img.Width == _beforeW && img.Height == _beforeH && img.Pixels.AsSpan().SequenceEqual(_before)) { _before = null; InvalidateVisual(); return; }
        img.Touch();
        _doc.Library.RecordImageEdit(name, _doc.Path, _beforeW, _beforeH, _before);
        _before = null;
    }

    void Plot(int shownX, int y)
    {
        if (_doc!.Set(_frame, FrameX(shownX), y, DrawColor())) { _doc.Image.Touch(); InvalidateVisual(); }
    }

    void FloodFill(int x, int y, uint color)
    {
        var doc = _doc!;
        var target = doc.Get(_frame, x, y);
        if (target == color) return;
        var stack = new Stack<(int, int)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (px, py) = stack.Pop();
            if (px < 0 || py < 0 || px >= doc.FrameW || py >= doc.FrameH || doc.Get(_frame, px, py) != target) continue;
            doc.Set(_frame, px, py, color);
            stack.Push((px + 1, py));
            stack.Push((px - 1, py));
            stack.Push((px, py + 1));
            stack.Push((px, py - 1));
        }
        doc.Image.Touch();
    }

    List<(int X, int Y)> Shape((int X, int Y) a, (int X, int Y) b)
    {
        if (Tool == DrawTool.Line) return Line(a, b);
        int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
        var list = new List<(int, int)>();
        for (var x = x0; x <= x1; x++) { list.Add((x, y0)); list.Add((x, y1)); }
        for (var y = y0 + 1; y < y1; y++) { list.Add((x0, y)); list.Add((x1, y)); }
        return list;
    }

    /// <summary>Bresenham line between two pixels (both ends included).</summary>
    public static List<(int X, int Y)> Line((int X, int Y) a, (int X, int Y) b)
    {
        var list = new List<(int, int)>();
        int x = a.X, y = a.Y, dx = Math.Abs(b.X - a.X), dy = -Math.Abs(b.Y - a.Y), sx = a.X < b.X ? 1 : -1, sy = a.Y < b.Y ? 1 : -1, err = dx + dy;
        while (true)
        {
            list.Add((x, y));
            if (x == b.X && y == b.Y) break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
        return list;
    }
}
