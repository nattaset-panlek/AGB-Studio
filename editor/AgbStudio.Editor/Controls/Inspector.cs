using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace AgbStudio.Editor.Controls;

public enum ProgressStatus { OK, Warn, Danger, Accent }

/// <summary>Figma "Progress": OK / Warn / Danger / Accent, value 0-100, 6 px bar.</summary>
public class AgbProgress : RangeBase
{
    public static readonly StyledProperty<ProgressStatus> StatusProperty = AvaloniaProperty.Register<AgbProgress, ProgressStatus>(nameof(Status));
    public ProgressStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    static AgbProgress()
    {
        MaximumProperty.OverrideDefaultValue<AgbProgress>(100);
        AffectsRender<AgbProgress>(StatusProperty, ValueProperty, MinimumProperty, MaximumProperty, BackgroundProperty, ForegroundProperty);
    }

    protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 160 : 0, 6);

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        ctx.DrawRectangle(Background, null, new Rect(0, 0, w, 6), 3, 3);
        var k = Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0;
        if (k <= 0) return;
        using (ctx.PushClip(new RoundedRect(new Rect(0, 0, w, 6), 3)))
            ctx.DrawRectangle(Foreground, null, new Rect(0, 0, w * Math.Min(1, k), 6), 3, 3);
    }
}

/// <summary>
/// Figma "Slider": 4 px track, accent fill, 12 px white knob with a border/strong ring. The knob sits at
/// width * value / 100 - 6, clamped to the track, exactly as the 160 px Figma variants place it; a wider slider scales
/// that clamp the way the Figma frames stretch the component (T-D03: at 1.0 the knob centre is 16 px from the end).
/// </summary>
public class AgbSlider : RangeBase
{
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<AgbSlider, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> KnobRingProperty = AvaloniaProperty.Register<AgbSlider, IBrush?>(nameof(KnobRing));
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? KnobRing { get => GetValue(KnobRingProperty); set => SetValue(KnobRingProperty, value); }

    bool _drag;

    static AgbSlider()
    {
        MaximumProperty.OverrideDefaultValue<AgbSlider>(100);
        FocusableProperty.OverrideDefaultValue<AgbSlider>(true);
        AffectsRender<AgbSlider>(ValueProperty, MinimumProperty, MaximumProperty, BackgroundProperty, FillProperty, KnobRingProperty);
    }

    double Ratio => Maximum > Minimum ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1) : 0;

    /// <summary>Width of the Figma Slider component; frames stretch it, scaling the knob position with it.</summary>
    const double DesignWidth = 160;

    protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 160 : 0, 12);

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        ctx.DrawRectangle(Background, null, new Rect(0, 4, w, 4), 2, 2);
        if (Ratio > 0) ctx.DrawRectangle(Fill, null, new Rect(0, 4, w * Ratio, 4), 2, 2);
        var x = Math.Clamp((Math.Clamp(DesignWidth * Ratio - 6, 0, DesignWidth - 12) + 6) * (w / DesignWidth) - 6, 0, Math.Max(0, w - 12));
        ctx.DrawEllipse(Brushes.White, KnobRing is null ? null : new Pen(KnobRing, 1), new Rect(x + 0.5, 0.5, 11, 11));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _drag = true;
        e.Pointer.Capture(this);
        SetFromX(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag) SetFromX(e.GetPosition(this).X);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drag = false;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var step = (Maximum - Minimum) / 100;
        if (e.Key is Key.Left or Key.Down) { Value = Math.Max(Minimum, Value - step); e.Handled = true; }
        else if (e.Key is Key.Right or Key.Up) { Value = Math.Min(Maximum, Value + step); e.Handled = true; }
        base.OnKeyDown(e);
    }

    void SetFromX(double x) => Value = Minimum + Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1) * (Maximum - Minimum);
}

public enum FieldKind { Text, Dropdown, XY, Slider, Toggle }

/// <summary>Figma "Field": an Inspector row with an 86 px label and a Text / Dropdown / XY / Slider / Toggle editor.</summary>
public class InspectorField : TemplatedControl
{
    public static readonly StyledProperty<FieldKind> KindProperty = AvaloniaProperty.Register<InspectorField, FieldKind>(nameof(Kind));
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<InspectorField, string?>(nameof(Label));
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<InspectorField, string?>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<string?> Value2Property = AvaloniaProperty.Register<InspectorField, string?>(nameof(Value2), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<double> SliderValueProperty = AvaloniaProperty.Register<InspectorField, double>(nameof(SliderValue), 50, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> IsOnProperty = AvaloniaProperty.Register<InspectorField, bool>(nameof(IsOn), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public FieldKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? Value2 { get => GetValue(Value2Property); set => SetValue(Value2Property, value); }
    public double SliderValue { get => GetValue(SliderValueProperty); set => SetValue(SliderValueProperty, value); }
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }
}

/// <summary>Figma "Meter": label (74 px), progress and value (78 px, right aligned), as in the GBA Budget block.</summary>
public class Meter : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Meter, string?>(nameof(Label));
    public static readonly StyledProperty<string?> ValueTextProperty = AvaloniaProperty.Register<Meter, string?>(nameof(ValueText));
    public static readonly StyledProperty<double> PercentProperty = AvaloniaProperty.Register<Meter, double>(nameof(Percent));
    public static readonly StyledProperty<ProgressStatus> StatusProperty = AvaloniaProperty.Register<Meter, ProgressStatus>(nameof(Status));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public double Percent { get => GetValue(PercentProperty); set => SetValue(PercentProperty, value); }
    public ProgressStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
}

public enum SwatchState { Default, Selected, Empty, Transparent }

/// <summary>Figma "Swatch": 30 x 30 palette colour with index; Default / Selected / Empty / Transparent.</summary>
public class Swatch : TemplatedControl
{
    public static readonly StyledProperty<SwatchState> StateProperty = AvaloniaProperty.Register<Swatch, SwatchState>(nameof(State));
    public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<Swatch, Color>(nameof(Color), Colors.Transparent);
    public static readonly StyledProperty<string?> IndexProperty = AvaloniaProperty.Register<Swatch, string?>(nameof(Index));

    public static readonly StyledProperty<IBrush?> ColorBrushProperty = AvaloniaProperty.Register<Swatch, IBrush?>(nameof(ColorBrush));

    public SwatchState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public string? Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
    /// <summary>The palette colour as a brush (template fill).</summary>
    public IBrush? ColorBrush { get => GetValue(ColorBrushProperty); private set => SetValue(ColorBrushProperty, value); }

    public Swatch() => ColorBrush = new SolidColorBrush(Color);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty) ColorBrush = new SolidColorBrush(Color);
    }
}
