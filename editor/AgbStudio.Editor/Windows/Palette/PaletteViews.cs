using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AgbStudio.Editor.Windows.Palette;

/// <summary>
/// Background or sprite palette RAM: 16 banks x 16 colours, 15 px cells with 2 px gaps. Empty entries use the raised
/// fill; the selected bank is outlined in the accent colour. Clicking a cell selects that bank and index.
/// </summary>
public sealed class PaletteRamView : Control
{
    public const int Cell = 15, Gap = 2;

    public static readonly StyledProperty<IBrush?> EmptyBrushProperty = AvaloniaProperty.Register<PaletteRamView, IBrush?>(nameof(EmptyBrush));
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty = AvaloniaProperty.Register<PaletteRamView, IBrush?>(nameof(HighlightBrush));

    public IBrush? EmptyBrush { get => GetValue(EmptyBrushProperty); set => SetValue(EmptyBrushProperty, value); }
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    /// <summary>Colours per bank slot (null = bank not in use); index 0 of a locked bank is transparent.</summary>
    public IReadOnlyList<uint>?[] Banks { get; } = new IReadOnlyList<uint>?[16];
    public int SelectedBank { get; set; } = -1;
    public event Action<int, int>? CellClicked;

    static PaletteRamView() => AffectsRender<PaletteRamView>(EmptyBrushProperty, HighlightBrushProperty);

    public PaletteRamView()
    {
        this.Token(EmptyBrushProperty, "bg/raised");
        this.Token(HighlightBrushProperty, "accent/default");
        Width = 16 * (Cell + Gap) - Gap;
        Height = 16 * (Cell + Gap) - Gap;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public static Rect CellRect(int bank, int index) => new(index * (Cell + Gap), bank * (Cell + Gap), Cell, Cell);

    public override void Render(DrawingContext ctx)
    {
        for (var b = 0; b < 16; b++)
            for (var i = 0; i < 16; i++)
            {
                var r = CellRect(b, i);
                var colors = Banks[b];
                IBrush? fill = EmptyBrush;
                if (colors != null && i < colors.Count && !Colour15.IsTransparent(colors[i])) fill = new ImmutableSolidColorBrush(Colour15.ToColor(colors[i]));
                ctx.DrawRectangle(fill, null, r, 2, 2);
                if (b == SelectedBank && HighlightBrush != null)
                    ctx.DrawRectangle(null, new Pen(HighlightBrush, 1), r.Deflate(0.5), 2, 2);
            }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);
        var b = (int)(p.Y / (Cell + Gap));
        var i = (int)(p.X / (Cell + Gap));
        if (b is < 0 or > 15 || i is < 0 or > 15) return;
        CellClicked?.Invoke(b, i);
        e.Handled = true;
    }

    public void Refresh() => InvalidateVisual();
}

/// <summary>A row of palette colours drawn edge to edge (Sprite Editor › Palette slots).</summary>
public sealed class ColourStrip : Control
{
    public double CellWidth { get; set; } = 10;
    public double CellHeight { get; set; } = 14;
    IReadOnlyList<uint> _colors = [];

    public IReadOnlyList<uint> Colors
    {
        get => _colors;
        set { _colors = value; InvalidateMeasure(); InvalidateVisual(); }
    }

    protected override Size MeasureOverride(Size availableSize) => new(CellWidth * _colors.Count, CellHeight);

    public override void Render(DrawingContext ctx)
    {
        for (var i = 0; i < _colors.Count; i++)
            ctx.DrawRectangle(new ImmutableSolidColorBrush(Colour15.ToColor(_colors[i])), null, new Rect(i * CellWidth, 0, CellWidth, CellHeight));
    }
}
