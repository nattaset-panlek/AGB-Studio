using System.Globalization;
using AgbStudio.Editor.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>Draws text with a Figma text style and a colour token (custom controls that render themselves).</summary>
static class Draw
{
    public static FormattedText Text(Control owner, string text, string style, string token)
    {
        var s = TextStyles.Get(style);
        var brush = Brush(owner, token);
        return new FormattedText(s.Upper ? text.ToUpperInvariant() : text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(s.FontFamily, FontStyle.Normal, s.FontWeight), s.Size, brush);
    }

    /// <summary>A colour token for the control's theme (the token dictionaries are theme dictionaries).</summary>
    public static IBrush Brush(Control owner, string token) =>
        owner.TryFindResource(token, owner.ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Transparent;

    /// <summary>Draws <paramref name="t"/> vertically centred in a line box of <paramref name="lineHeight"/> starting at y.</summary>
    public static void At(DrawingContext ctx, FormattedText t, double x, double y, double lineHeight) =>
        ctx.DrawText(t, new Point(x, y + Math.Round((lineHeight - t.Height) / 2)));
}

/// <summary>The song overview of the Audio page: 160 level bars (status/success) and the playhead; click to seek.</summary>
public sealed class WaveformOverview : Control
{
    public static readonly StyledProperty<double> PlayheadProperty = AvaloniaProperty.Register<WaveformOverview, double>(nameof(Playhead));
    float[] _peaks = [];

    static WaveformOverview() => AffectsRender<WaveformOverview>(PlayheadProperty);

    public double Playhead { get => GetValue(PlayheadProperty); set => SetValue(PlayheadProperty, value); }
    public float[] Peaks { get => _peaks; set { _peaks = value; InvalidateVisual(); } }
    public event Action<double>? Seek;

    public WaveformOverview() { Height = 64; Cursor = new Cursor(StandardCursorType.Hand); ActualThemeVariantChanged += (_, _) => InvalidateVisual(); }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        ctx.DrawRectangle(Draw.Brush(this, "bg/panel"), new Pen(Draw.Brush(this, "border/default")), new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);
        if (_peaks.Length > 0)
        {
            var bar = Draw.Brush(this, "status/success");
            var pitch = (w - 2) / _peaks.Length;
            for (var i = 0; i < _peaks.Length; i++)
            {
                var bh = Math.Max(2, Math.Round(_peaks[i] * (h - 8)));
                ctx.FillRectangle(bar, new Rect(1 + i * pitch, h - 1 - bh, pitch * 0.66, bh));
            }
        }
        var x = Math.Round(1 + Math.Clamp(Playhead, 0, 1) * (w - 4));
        ctx.FillRectangle(Draw.Brush(this, "lit/white"), new Rect(x, 1, 2, h - 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Seek?.Invoke(Math.Clamp(e.GetPosition(this).X / Math.Max(1, Bounds.Width), 0, 1));
        e.Handled = true;
    }
}

/// <summary>
/// The tracker pattern view of the Audio page: a ROW column and one 124 px column per channel with note, instrument
/// and volume ("C-4 01 v40"); the current row is highlighted. Click a cell to act with the current tool.
/// </summary>
public sealed class TrackerGrid : Control
{
    public const double RowHeight = 15.5, HeaderHeight = 16, LabelWidth = 41, ColumnWidth = 124, ColumnPitch = 125;
    public const int VisibleRows = 14, CurrentSlot = 4;

    TrackerModule? _module;
    int _pattern, _row;
    (int Row, int Channel)? _selected;

    public TrackerModule? Module { get => _module; set { _module = value; InvalidateVisual(); } }
    public int PatternIndex { get => _pattern; set { _pattern = value; InvalidateVisual(); } }
    public int CurrentRow { get => _row; set { _row = value; InvalidateVisual(); } }
    public (int Row, int Channel)? Selected { get => _selected; set { _selected = value; InvalidateVisual(); } }
    public event Action<int, int>? CellPressed;
    public event Action<int>? Scrolled;

    public TrackerGrid() { Height = HeaderHeight + VisibleRows * RowHeight; ClipToBounds = true; ActualThemeVariantChanged += (_, _) => InvalidateVisual(); }

    TrackerPattern? Pattern => _module != null && _pattern >= 0 && _pattern < _module.Patterns.Count ? _module.Patterns[_pattern] : null;

    public int FirstRow
    {
        get
        {
            var rows = Pattern?.Rows ?? 64;
            return Math.Clamp(_row - CurrentSlot, 0, Math.Max(0, rows - VisibleRows));
        }
    }

    /// <summary>Text of a cell as the view shows it.</summary>
    public static string CellText(TrackerModule m, TrackerCell c)
    {
        var note = c.Note == 0 ? "..." : m.NoteName(c.Note);
        var ins = c.Instrument == 0 ? ".." : c.Instrument.ToString("00", CultureInfo.InvariantCulture);
        var vol = "...";
        if (m.IsXm && c.Volume is >= 0x10 and <= 0x50) vol = "v" + (c.Volume - 0x10).ToString("00", CultureInfo.InvariantCulture);
        else if (c.Effect == 0xC) vol = "v" + Math.Min(64, (int)c.Param).ToString("00", CultureInfo.InvariantCulture);
        else if (c.Note is > 0 and < TrackerCell.KeyOff && c.Instrument > 0 && c.Instrument <= m.Instruments.Count)
            vol = "v" + (m.Instruments[c.Instrument - 1].SampleFor(c.Note)?.Volume ?? 64).ToString("00", CultureInfo.InvariantCulture);
        return $"{note} {ins} {vol}";
    }

    public override void Render(DrawingContext ctx)
    {
        var m = _module;
        var pat = Pattern;
        if (m is null || pat is null) return;
        var first = FirstRow;
        var panel = Draw.Brush(this, "bg/panel");
        var cols = m.Channels;
        for (var c = 0; c < cols; c++)
            ctx.DrawRectangle(panel, null, new Rect(LabelWidth + c * ColumnPitch, 0, ColumnWidth, Bounds.Height), 6, 6);
        var cur = _row - first;
        if (cur is >= 0 and < VisibleRows)
            ctx.FillRectangle(Draw.Brush(this, "accent/selected"), new Rect(LabelWidth, HeaderHeight + cur * RowHeight, cols * ColumnPitch - 1, RowHeight));
        if (_selected is { } sel && sel.Row - first is >= 0 and < VisibleRows && sel.Row != _row)
            ctx.DrawRectangle(null, new Pen(Draw.Brush(this, "accent/default")), new Rect(LabelWidth + sel.Channel * ColumnPitch + 0.5, HeaderHeight + (sel.Row - first) * RowHeight + 0.5, ColumnWidth - 1, RowHeight - 1));
        Draw.At(ctx, Draw.Text(this, "ROW", "Label/11", "text/muted"), 0, HeaderHeight - 14 + 2, 14);
        for (var c = 0; c < cols; c++)
            Draw.At(ctx, Draw.Text(this, m.ChannelName(c), "Label/11", "text/secondary"), LabelWidth + c * ColumnPitch + 8, 1, 14);
        for (var i = 0; i < VisibleRows; i++)
        {
            var r = first + i;
            if (r >= pat.Rows) break;
            var y = HeaderHeight + i * RowHeight;
            var current = r == _row;
            Draw.At(ctx, Draw.Text(this, r.ToString("00", CultureInfo.InvariantCulture), current ? "Mono/11 Bold" : "Mono/11", current ? "text/primary" : "text/muted"), 0, y, RowHeight);
            for (var c = 0; c < cols; c++)
            {
                var cell = pat.Cells[r, c];
                var empty = cell.Note == 0;
                Draw.At(ctx, Draw.Text(this, CellText(m, cell), "Mono/11", empty ? "text/muted" : "text/primary"), LabelWidth + c * ColumnPitch + 8, y, RowHeight);
            }
        }
    }

    public (int Row, int Channel)? CellAt(Point p)
    {
        if (_module is null || p.Y < HeaderHeight || p.X < LabelWidth) return null;
        var c = (int)((p.X - LabelWidth) / ColumnPitch);
        var r = FirstRow + (int)((p.Y - HeaderHeight) / RowHeight);
        if (c >= _module.Channels || r >= (Pattern?.Rows ?? 0)) return null;
        return (r, c);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (CellAt(e.GetPosition(this)) is { } cell) { CellPressed?.Invoke(cell.Row, cell.Channel); e.Handled = true; }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Scrolled?.Invoke(e.Delta.Y > 0 ? -1 : 1);
        e.Handled = true;
    }
}
