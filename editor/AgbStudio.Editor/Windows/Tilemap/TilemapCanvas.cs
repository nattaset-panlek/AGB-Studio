using AgbStudio.Core.Project;
using AgbStudio.Editor.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>
/// The Tilemap Editor viewport: the GBA screen (240 x 160, "Camera 240×160") at the zoom of the zoom chip, drawn by
/// <see cref="MapRenderer"/> from the converted data the ROM uses; the 16 px grid, the collision view, the brush
/// outline under the pointer. Left button uses the active tool, right or middle button drags the camera, the wheel
/// scrolls (Shift: sideways, Ctrl: zoom), arrow keys move the camera one tile.
/// </summary>
public sealed class TilemapCanvas : Control
{
    readonly TilemapSession _s;
    WriteableBitmap? _frame;
    bool _stale = true;
    (int X, int Y)? _dragStart, _dragEnd;
    (Point P, int CamX, int CamY)? _pan;
    (int X, int Y)? _lastPaint;

    public TilemapCanvas(TilemapSession session)
    {
        _s = session;
        Name = "TilemapCanvas";
        Focusable = true;
        ClipToBounds = true;
        void OnDoc(MapChange _) => Stale();
        PageChrome.WhileAttached(this, () => { session.Doc.Changed += OnDoc; session.ViewChanged += Stale; Stale(); },
            () => { session.Doc.Changed -= OnDoc; session.ViewChanged -= Stale; });
    }

    void Stale()
    {
        _stale = true;
        InvalidateVisual();
    }

    public TilemapSession Session => _s;

    /// <summary>The screen window in control coordinates: centred, 240 x 160 times the zoom.</summary>
    public Rect Window
    {
        get
        {
            var z = _s.Zoom;
            double w = TilemapSession.ScreenW * z, h = TilemapSession.ScreenH * z;
            return new Rect(Math.Floor((Bounds.Width - w) / 2), Math.Floor(Math.Max(0, Bounds.Height - h) / 2), w, h);
        }
    }

    /// <summary>The rendered screen as ARGB (for tests: it is what the canvas shows, before zoom).</summary>
    public uint[] RenderScreen() =>
        _s.Renderer.Render(_s.Doc, _s.CamX, _s.CamY, TilemapSession.ScreenW, TilemapSession.ScreenH, _s.IsVisible, _s.ObjVisible);

    IBrush? Res(string key) => this.TryFindResource(key, ActualThemeVariant, out var r) ? r as IBrush : null;

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Res("bg/canvas") ?? Brushes.Black, new Rect(Bounds.Size));
        if (_stale || _frame is null)
        {
            _frame?.Dispose();
            _frame = Pixels.ToBitmap(RenderScreen(), TilemapSession.ScreenW, TilemapSession.ScreenH);
            _stale = false;
        }
        var win = Window;
        double z = _s.Zoom;
        using (ctx.PushClip(win))
        {
            using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                ctx.DrawImage(_frame, new Rect(0, 0, TilemapSession.ScreenW, TilemapSession.ScreenH), win);
            if (_s.Services?.Preferences.Current.PixelGridAt(z) == true) DrawPixelGrid(ctx, win, z);
            DrawGrid(ctx, win, z);
            if (_s.CollisionView) DrawCollision(ctx, win, z);
            DrawCursor(ctx, win, z);
        }
        // the camera (GBA screen) frame and its label
        var warn = Res("status/warning");
        // Figma T-P03: a 1 px line 1 px inside the screen edge (the map shows outside it)
        ctx.DrawRectangle(null, new Pen(warn, 1), win.Deflate(1.5));
        var style = TextStyles.Get("Label/11 Medium");
        var text = new FormattedText($"Camera {TilemapSession.ScreenW}×{TilemapSession.ScreenH}", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(style.FontFamily, FontStyle.Normal, style.FontWeight), 10, Res("bg/canvas"));
        var label = new Rect(win.X + 6, win.Y + 7, Math.Ceiling(text.Width) + 12, 16);
        ctx.DrawRectangle(warn, null, label, 3, 3);
        ctx.DrawText(text, new Point(label.X + 6, label.Y + (16 - text.Height) / 2));
    }

    /// <summary>Preferences › Appearance: the grid between game pixels at 400% and above.</summary>
    void DrawPixelGrid(DrawingContext ctx, Rect win, double z)
    {
        var pen = new Pen(Res("text/on-accent"), 1);
        using (ctx.PushOpacity(0.06))
        {
            for (var x = 1; x < TilemapSession.ScreenW; x++) ctx.DrawLine(pen, new Point(win.X + x * z + 0.5, win.Y), new Point(win.X + x * z + 0.5, win.Bottom));
            for (var y = 1; y < TilemapSession.ScreenH; y++) ctx.DrawLine(pen, new Point(win.X, win.Y + y * z + 0.5), new Point(win.Right, win.Y + y * z + 0.5));
        }
    }

    /// <summary>True when the pixel grid is drawn at the current zoom.</summary>
    public bool PixelGridShown => _s.Services?.Preferences.Current.PixelGridAt(_s.Zoom) == true;

    void DrawGrid(DrawingContext ctx, Rect win, double z)
    {
        var l = _s.CurrentLayer;
        var sx = MapRenderer.Scroll(_s.CamX, l?.ParallaxX ?? 1);
        var sy = MapRenderer.Scroll(_s.CamY, l?.ParallaxY ?? 1);
        var pen = new Pen(Res("text/on-accent"), 1);
        using (ctx.PushOpacity(0.14))
        {
            for (var x = -(sx % 16 + 16) % 16; x <= TilemapSession.ScreenW; x += 16)
                ctx.DrawLine(pen, new Point(win.X + x * z + 0.5, win.Y), new Point(win.X + x * z + 0.5, win.Bottom));
            for (var y = -(sy % 16 + 16) % 16; y <= TilemapSession.ScreenH; y += 16)
                ctx.DrawLine(pen, new Point(win.X, win.Y + y * z + 0.5), new Point(win.Right, win.Y + y * z + 0.5));
        }
    }

    public static string? CollisionToken(CollisionType t) => t switch
    {
        CollisionType.Solid => "status/danger", CollisionType.OneWay => "accent/default", CollisionType.Ladder => "status/warning",
        CollisionType.Hazard => "kind/script", CollisionType.Water => "kind/audio", _ => null,
    };

    void DrawCollision(DrawingContext ctx, Rect win, double z)
    {
        var li = _s.Doc.CollisionLayerIndex;
        if (li < 0) return;
        var layer = _s.Doc.Map.Layers[li];
        var sx = MapRenderer.Scroll(_s.CamX, layer.ParallaxX);
        var sy = MapRenderer.Scroll(_s.CamY, layer.ParallaxY);
        for (var cy = sy / 16; cy <= (sy + TilemapSession.ScreenH) / 16; cy++)
            for (var cx = sx / 16; cx <= (sx + TilemapSession.ScreenW) / 16; cx++)
            {
                var type = _s.Doc.CollisionOf(_s.Doc.Cell(li, cx, cy));
                if (CollisionToken(type) is not { } token || !_s.ShowsCollision(type)) continue;
                var brush = Res(token);
                var r = new Rect(win.X + (cx * 16 - sx) * z, win.Y + (cy * 16 - sy) * z, 16 * z, 16 * z);
                using (ctx.PushOpacity(0.28)) ctx.FillRectangle(brush!, r);
                ctx.DrawRectangle(null, new Pen(brush, 1), r.Deflate(0.5));
            }
    }

    void DrawCursor(DrawingContext ctx, Rect win, double z)
    {
        (int X, int Y, int W, int H)? box = null;
        if (_dragStart is { } a && _dragEnd is { } b && _s.Tool is TileTool.Rect or TileTool.Select)
            box = (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
        else if (_s.Hover is { } h && _s.Tool is TileTool.Brush or TileTool.Erase)
            box = (h.X, h.Y, _s.StampWidth, _s.StampHeight);
        else if (_s.Hover is { } h1)
            box = (h1.X, h1.Y, 1, 1);
        if (box is not { } c) return;
        var (px, py) = _s.ScreenOf(c.X, c.Y);
        var r = new Rect(win.X + px * z, win.Y + py * z, c.W * 16 * z, c.H * 16 * z);
        // Figma T-P03: a white 1.5 px outline over a faint (14 %) white fill
        using (ctx.PushOpacity(0.14)) ctx.FillRectangle(Res("lit/white")!, r);
        ctx.DrawRectangle(null, new Pen(Res("lit/white"), 1.5), r.Deflate(0.75));
    }

    // ---- input -------------------------------------------------------------------------------------

    (int X, int Y)? ScreenPixel(Point p)
    {
        var win = Window;
        if (!win.Contains(p)) return null;
        return ((int)Math.Floor((p.X - win.X) / _s.Zoom), (int)Math.Floor((p.Y - win.Y) / _s.Zoom));
    }

    (int X, int Y)? CellAt(Point p) => ScreenPixel(p) is { } s ? _s.CellAt(s.X, s.Y) : null;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsRightButtonPressed || pt.Properties.IsMiddleButtonPressed)
        {
            _pan = (pt.Position, _s.CamX, _s.CamY);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed || CellAt(pt.Position) is not { } cell) return;
        e.Pointer.Capture(this);
        e.Handled = true;
        switch (_s.Tool)
        {
            case TileTool.Brush:
                _s.BeginStroke();
                _s.PaintAt(cell.X, cell.Y);
                _lastPaint = cell;
                break;
            case TileTool.Erase:
                _s.BeginStroke();
                _s.EraseAt(cell.X, cell.Y);
                _lastPaint = cell;
                break;
            case TileTool.Fill:
                _s.FloodFill(cell.X, cell.Y);
                break;
            case TileTool.Pick:
                _s.PickAt(cell.X, cell.Y);
                break;
            case TileTool.Rect:
            case TileTool.Select:
                _dragStart = _dragEnd = cell;
                InvalidateVisual();
                break;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_pan is { } pan)
        {
            _s.SetCamera(pan.CamX - (int)Math.Round((p.X - pan.P.X) / _s.Zoom), pan.CamY - (int)Math.Round((p.Y - pan.P.Y) / _s.Zoom));
            return;
        }
        var cell = CellAt(p);
        _s.Hover = cell;
        if (cell is not { } c) return;
        if (_lastPaint is { } last && last != c)
        {
            if (_s.Tool == TileTool.Brush) _s.PaintAt(c.X, c.Y);
            else if (_s.Tool == TileTool.Erase) _s.EraseAt(c.X, c.Y);
            _lastPaint = c;
        }
        if (_dragStart != null && _dragEnd != c) { _dragEnd = c; InvalidateVisual(); }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        if (_pan != null) { _pan = null; return; }
        if (_lastPaint != null)
        {
            _lastPaint = null;
            _s.EndStroke(_s.Tool == TileTool.Erase ? "Erase" : "Paint");
        }
        if (_dragStart is { } a && _dragEnd is { } b)
        {
            _dragStart = _dragEnd = null;
            if (_s.Tool == TileTool.Rect) _s.FillRect(a.X, a.Y, b.X, b.Y);
            else if (_s.Tool == TileTool.Select) _s.StampFromMap(a.X, a.Y, b.X, b.Y);
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_lastPaint is null && _pan is null) _s.Hover = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var notches = Math.Sign(e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) _s.SetZoom(_s.Zoom + notches);
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Delta.X != 0) _s.SetCamera(_s.CamX - notches * 16, _s.CamY);
        else _s.SetCamera(_s.CamX, _s.CamY - notches * 16);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers != KeyModifiers.None) return;
        (int dx, int dy)? d = e.Key switch { Key.Left => (-16, 0), Key.Right => (16, 0), Key.Up => (0, -16), Key.Down => (0, 16), _ => null };
        if (d is not { } m) return;
        _s.SetCamera(_s.CamX + m.dx, _s.CamY + m.dy);
        e.Handled = true;
    }
}
