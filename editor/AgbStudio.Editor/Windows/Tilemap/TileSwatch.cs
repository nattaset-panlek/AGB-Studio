using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>
/// The tileset panel's tiles (4 per row, 56 px each, converted pixels as in the ROM). Click picks the brush tile,
/// dragging picks a block of tiles as the stamp; the stamp is outlined.
/// </summary>
public sealed class TileSwatch : Control
{
    public const double Cell = 56, Gap = 3, Pad = 1;
    readonly TilemapSession _s;
    readonly Dictionary<int, WriteableBitmap> _bitmaps = [];
    int _version = -1;
    int? _anchor;

    public TileSwatch(TilemapSession session)
    {
        _s = session;
        Name = "TileSwatch";
        void OnDoc(MapChange c) { if (c.HasFlag(MapChange.Tileset)) { InvalidateMeasure(); InvalidateVisual(); } }
        PageChrome.WhileAttached(this, () => { session.ViewChanged += InvalidateVisual; session.Doc.Changed += OnDoc; InvalidateVisual(); },
            () => { session.ViewChanged -= InvalidateVisual; session.Doc.Changed -= OnDoc; });
    }

    int Rows => (_s.Doc.TileCount + TilemapSession.SwatchColumns - 1) / TilemapSession.SwatchColumns;

    protected override Size MeasureOverride(Size availableSize) =>
        new(Pad * 2 + TilemapSession.SwatchColumns * Cell + (TilemapSession.SwatchColumns - 1) * Gap, Pad * 2 + Math.Max(2, Rows) * (Cell + Gap) - Gap);

    public Rect CellRect(int tile) =>
        new(Pad + tile % TilemapSession.SwatchColumns * (Cell + Gap), Pad + tile / TilemapSession.SwatchColumns * (Cell + Gap), Cell, Cell);

    public int? TileAt(Point p)
    {
        var col = (int)Math.Floor((p.X - Pad) / (Cell + Gap));
        var row = (int)Math.Floor((p.Y - Pad) / (Cell + Gap));
        if (col < 0 || row < 0 || col >= TilemapSession.SwatchColumns) return null;
        var t = row * TilemapSession.SwatchColumns + col;
        return t < _s.Doc.TileCount ? t : null;
    }

    IBrush? Res(string key) => this.TryFindResource(key, ActualThemeVariant, out var r) ? r as IBrush : null;

    public override void Render(DrawingContext ctx)
    {
        if (_version != _s.Doc.TilesetVersion)
        {
            foreach (var b in _bitmaps.Values) b.Dispose();
            _bitmaps.Clear();
            _version = _s.Doc.TilesetVersion;
        }
        using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            for (var t = 0; t < _s.Doc.TileCount; t++)
            {
                if (!_bitmaps.TryGetValue(t, out var bmp)) _bitmaps[t] = bmp = Pixels.ToBitmap(_s.Renderer.Metatile(_s.Doc, t), 16, 16);
                ctx.DrawImage(bmp, new Rect(0, 0, 16, 16), CellRect(t));
            }
        // stamp outline
        var first = _s.SelectedTile;
        var r = CellRect(first);
        var w = Math.Min(_s.StampWidth, TilemapSession.SwatchColumns - first % TilemapSession.SwatchColumns);
        var sel = new Rect(r.X, r.Y, w * (Cell + Gap) - Gap, _s.StampHeight * (Cell + Gap) - Gap);
        ctx.DrawRectangle(null, new Pen(Res("lit/white"), 2), sel.Inflate(-1));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || TileAt(e.GetPosition(this)) is not { } t) return;
        _anchor = t;
        e.Pointer.Capture(this);
        _s.SelectTile(t);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_anchor is not { } a || TileAt(e.GetPosition(this)) is not { } t) return;
        const int cols = TilemapSession.SwatchColumns;
        int x0 = Math.Min(a % cols, t % cols), y0 = Math.Min(a / cols, t / cols);
        int x1 = Math.Max(a % cols, t % cols), y1 = Math.Max(a / cols, t / cols);
        var first = y0 * cols + x0;
        if (first != _s.SelectedTile || x1 - x0 + 1 != _s.StampWidth || y1 - y0 + 1 != _s.StampHeight) _s.SelectTile(first, x1 - x0 + 1, y1 - y0 + 1);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _anchor = null;
        e.Pointer.Capture(null);
    }
}
