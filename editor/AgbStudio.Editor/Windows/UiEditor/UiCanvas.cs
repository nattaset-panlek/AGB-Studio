using AgbStudio.Core.Project;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AgbStudio.Editor.Windows.UiEditor;

/// <summary>The 240 x 160 screen of the UI Editor: shows the page as the game draws it, selects elements with a click,
/// moves them by dragging and resizes them by their handles. Elements a layout group places have no move or size handles;
/// the status bar says why.</summary>
public sealed class UiCanvas : Control
{
    enum Grip { None, Move, L, R, T, B, TL, TR, BL, BR }

    const int HandleSize = 7;

    readonly UiPageLibrary _lib;
    WriteableBitmap? _bitmap;
    Point _start;
    Grip _grip;
    UiPixelRect _startRect;
    int _startX, _startY, _startW, _startH;
    bool _moved;

    public UiPageRenderer? Renderer { get; set; }
    public int Scale { get; private set; } = 3;
    /// <summary>The element a drag was refused for, because a layout group places it.</summary>
    public event Action? PlacedByLayout;

    public UiCanvas(UiPageLibrary lib)
    {
        _lib = lib;
        Focusable = true;
        ClipToBounds = true;
        Name = "UiCanvas";
    }

    Point Origin => new((Bounds.Width - UiPageRenderer.W * Scale) / 2, (Bounds.Height - UiPageRenderer.H * Scale) / 2);

    public void Redraw()
    {
        if (Renderer is { } r && _lib.Page is { } page)
        {
            var focus = _lib.Selected;
            _bitmap = Pixels.ToBitmap(r.Render(page, focus), UiPageRenderer.W, UiPageRenderer.H);
        }
        else
        {
            _bitmap = null;
        }
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Scale = Math.Max(1, (int)Math.Min(Bounds.Width / UiPageRenderer.W, Bounds.Height / UiPageRenderer.H));
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        IBrush Token(string name) => this.TryFindResource(name, ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Transparent;
        ctx.FillRectangle(Token("bg/sunken"), new Rect(Bounds.Size));
        var o = Origin;
        var screen = new Rect(o.X, o.Y, UiPageRenderer.W * Scale, UiPageRenderer.H * Scale);
        if (_bitmap != null)
            using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                ctx.DrawImage(_bitmap, new Rect(0, 0, UiPageRenderer.W, UiPageRenderer.H), screen);
        ctx.DrawRectangle(new Pen(Token("border/default")), screen);
        if (_lib.Page is not { } page || _lib.Selected is not { } e) return;
        var rects = UiLayoutEngine.Resolve(page);
        if (!rects.TryGetValue(e, out var r)) return;
        var accent = Token("accent/primary");
        var rect = new Rect(o.X + r.X * Scale, o.Y + r.Y * Scale, r.W * Scale, r.H * Scale);
        ctx.DrawRectangle(new Pen(accent), rect);
        if (!CanPlace(e)) return;
        foreach (var (_, h) in Handles(rect))
        {
            ctx.FillRectangle(Token("bg/panel"), h);
            ctx.DrawRectangle(new Pen(accent), h);
        }
    }

    bool CanPlace(UiElement e) =>
        !ReferenceEquals(e, _lib.Page?.Root) && (e.IgnoreLayout || _lib.ParentOf(e)?.Layout is null or { Kind: UiLayoutKind.None });

    static IEnumerable<(Grip Grip, Rect Box)> Handles(Rect r)
    {
        var s = HandleSize;
        Rect At(double x, double y) => new(x - s / 2.0, y - s / 2.0, s, s);
        yield return (Grip.TL, At(r.X, r.Y));
        yield return (Grip.T, At(r.Center.X, r.Y));
        yield return (Grip.TR, At(r.Right, r.Y));
        yield return (Grip.L, At(r.X, r.Center.Y));
        yield return (Grip.R, At(r.Right, r.Center.Y));
        yield return (Grip.BL, At(r.X, r.Bottom));
        yield return (Grip.B, At(r.Center.X, r.Bottom));
        yield return (Grip.BR, At(r.Right, r.Bottom));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_lib.Page is not { } page) return;
        var p = e.GetPosition(this);
        var o = Origin;
        var rects = UiLayoutEngine.Resolve(page);
        _grip = Grip.None;
        if (_lib.Selected is { } current && rects.TryGetValue(current, out var cr) && CanPlace(current))
            foreach (var (g, box) in Handles(new Rect(o.X + cr.X * Scale, o.Y + cr.Y * Scale, cr.W * Scale, cr.H * Scale)))
                if (box.Inflate(2).Contains(p)) _grip = g;
        if (_grip == Grip.None)
        {
            var gx = (p.X - o.X) / Scale;
            var gy = (p.Y - o.Y) / Scale;
            var hit = page.Root.Walk().Where(x => !ReferenceEquals(x, page.Root) && !x.Hidden && rects.TryGetValue(x, out var a) && gx >= a.X && gx < a.Right && gy >= a.Y && gy < a.Bottom).LastOrDefault();
            if (!ReferenceEquals(hit, _lib.Selected)) _lib.Select(hit ?? page.Root);
            if (hit is null) return;
            _grip = Grip.Move;
        }
        if (_lib.Selected is not { } sel || !rects.TryGetValue(sel, out var sr)) return;
        if (!CanPlace(sel))
        {
            _grip = Grip.None;
            PlacedByLayout?.Invoke();
            return;
        }
        _start = p;
        _startRect = sr;
        (_startX, _startY, _startW, _startH) = (sel.Rect.X, sel.Rect.Y, sel.Rect.W, sel.Rect.H);
        _moved = false;
        _lib.BeginLive();
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_grip == Grip.None || !Equals(e.Pointer.Captured, this)) return;
        var d = e.GetPosition(this) - _start;
        var dx = (int)Math.Round(d.X / Scale);
        var dy = (int)Math.Round(d.Y / Scale);
        if (dx == 0 && dy == 0 && !_moved) return;
        _moved = true;
        var g = _grip;
        // How far each edge moved. The Rect Transform's X and W then follow from the pivot, for any anchors.
        int left = g is Grip.L or Grip.TL or Grip.BL or Grip.Move ? dx : 0;
        int right = g is Grip.R or Grip.TR or Grip.BR or Grip.Move ? dx : 0;
        int top = g is Grip.T or Grip.TL or Grip.TR or Grip.Move ? dy : 0;
        int bottom = g is Grip.B or Grip.BL or Grip.BR or Grip.Move ? dy : 0;
        if (_startRect.W + right - left < 1) { if (left != 0) left = _startRect.W - 1; else right = 1 - _startRect.W; }
        if (_startRect.H + bottom - top < 1) { if (top != 0) top = _startRect.H - 1; else bottom = 1 - _startRect.H; }
        _lib.Live(el =>
        {
            var dw = right - left;
            var dh = bottom - top;
            el.Rect.W = _startW + dw;
            el.Rect.H = _startH + dh;
            el.Rect.X = _startX + left + (int)Math.Round(el.Rect.PivotX * dw);
            el.Rect.Y = _startY + top + (int)Math.Round(el.Rect.PivotY * dh);
        });
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_grip == Grip.None) return;
        var grip = _grip;
        _grip = Grip.None;
        e.Pointer.Capture(null);
        if (_moved) _lib.EndLive(grip == Grip.Move ? "Move element" : "Resize element");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_lib.Selected is not { } sel) return;
        switch (e.Key)
        {
            case Key.Delete:
                _lib.DeleteSelected();
                e.Handled = true;
                return;
            case Key.Left or Key.Right or Key.Up or Key.Down when CanPlace(sel):
                var (dx, dy) = e.Key switch { Key.Left => (-1, 0), Key.Right => (1, 0), Key.Up => (0, -1), _ => (0, 1) };
                _lib.EditSelected("Move element", el =>
                {
                    el.Rect.X += dx;
                    el.Rect.Y += dy;
                });
                e.Handled = true;
                return;
        }
    }
}
