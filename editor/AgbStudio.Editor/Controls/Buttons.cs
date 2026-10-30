using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace AgbStudio.Editor.Controls;

public enum ButtonKind { Primary, Secondary, Ghost, Danger }
public enum ButtonSize { SM, LG }

/// <summary>Figma "Button": Type Primary / Secondary / Ghost / Danger, Size SM / LG, State Default / Hover / Pressed / Disabled, optional icon.</summary>
public class AgbButton : Button
{
    public static readonly StyledProperty<ButtonKind> KindProperty = AvaloniaProperty.Register<AgbButton, ButtonKind>(nameof(Kind), ButtonKind.Secondary);
    public static readonly StyledProperty<ButtonSize> SizeProperty = AvaloniaProperty.Register<AgbButton, ButtonSize>(nameof(Size));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<AgbButton, string?>(nameof(Glyph));

    public ButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public ButtonSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>Figma "Icon Button": 28 x 26, State Default / Active / Disabled / Hover / Pressed.</summary>
public class IconButton : Button
{
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<IconButton, string?>(nameof(Glyph));
    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<IconButton, bool>(nameof(IsActive));

    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty) PseudoClasses.Set(":active", IsActive);
    }
}

public enum PlayKind { Play, Pause, Step, Stop }

/// <summary>Figma "Play Control": Play / Pause / Step / Stop.</summary>
public class PlayControl : Button
{
    public static readonly StyledProperty<PlayKind> KindProperty = AvaloniaProperty.Register<PlayControl, PlayKind>(nameof(Kind));
    public PlayKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
}

/// <summary>Figma "Layer Toggle": On / Off (visibility) and Active (the layer being edited).</summary>
public class LayerToggle : Button
{
    public static readonly StyledProperty<bool> IsOnProperty = AvaloniaProperty.Register<LayerToggle, bool>(nameof(IsOn), true);
    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<LayerToggle, bool>(nameof(IsActive));

    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public LayerToggle() => UpdateState();

    protected override void OnClick() { IsOn = !IsOn; base.OnClick(); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty || change.Property == IsActiveProperty) UpdateState();
    }

    void UpdateState()
    {
        PseudoClasses.Set(":active", IsActive);
        PseudoClasses.Set(":off", !IsActive && !IsOn);
    }
}

public enum ChipStyle { Default, Accent, Success, Warning, Danger, Script }

/// <summary>Figma "Chip": Default / Accent / Success / Warning / Danger / Script with optional icon. Clickable (e.g. Video mode chip).</summary>
public class Chip : Button
{
    public static readonly StyledProperty<ChipStyle> StyleProperty = AvaloniaProperty.Register<Chip, ChipStyle>(nameof(Style));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<Chip, string?>(nameof(Glyph));

    public ChipStyle Style { get => GetValue(StyleProperty); set => SetValue(StyleProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>Figma "Switch": On / Off, 32 x 18.</summary>
public class AgbSwitch : ToggleButton
{
    public AgbSwitch() => IsChecked = true;
}

/// <summary>Figma "Segment": Default / Active / Hover; one of a group (RadioButton).</summary>
public class Segment : RadioButton;

/// <summary>Figma "Tab": Default / Active (2 px accent underline).</summary>
public class Tab : RadioButton;

/// <summary>Base for list-like rows that can be selected and show hover.</summary>
public abstract class SelectableRow : TemplatedControl
{
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<SelectableRow, bool>(nameof(IsSelected));
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    public event EventHandler<RoutedEventArgsWrapper>? Activated;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty) PseudoClasses.Set(":selected", IsSelected);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Activated?.Invoke(this, new RoutedEventArgsWrapper(e.ClickCount));
        e.Handled = true;
    }
}

public sealed record RoutedEventArgsWrapper(int ClickCount);
