using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AgbStudio.Editor.Graphics;

/// <summary>An icon/* component by name, tinted with the inherited Foreground (like a Figma instance colour override).</summary>
public sealed class Icon : Control
{
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<Icon, string?>(nameof(Glyph));
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<Icon, double>(nameof(Size), 16);
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

    static Icon() => AffectsRender<Icon>(GlyphProperty, SizeProperty, ForegroundProperty);

    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty) InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (string.IsNullOrEmpty(Glyph)) return;
        IconCatalog.Icon(Glyph).RenderIcon(context, Size, Foreground);
    }
}

/// <summary>An art/*, thumb/* or brand/* component drawn at the control's size.</summary>
public sealed class ArtImage : Control
{
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<ArtImage, string?>(nameof(Source));

    static ArtImage() => AffectsRender<ArtImage>(SourceProperty);

    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty) InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Source)) return default;
        var img = IconCatalog.Art(Source);
        return new Size(double.IsNaN(Width) ? img.Width : Width, double.IsNaN(Height) ? img.Height : Height);
    }

    public override void Render(DrawingContext context)
    {
        if (!string.IsNullOrEmpty(Source)) IconCatalog.Art(Source).Render(context, Bounds.Size, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
    }
}
