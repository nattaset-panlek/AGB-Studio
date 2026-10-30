using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace AgbStudio.Editor.Controls;

/// <summary>Figma "Status Item": icon or coloured dot plus a label (status bar).</summary>
public class StatusItem : Button
{
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<StatusItem, string?>(nameof(Glyph), "check");
    public static readonly StyledProperty<bool> IsDotProperty = AvaloniaProperty.Register<StatusItem, bool>(nameof(IsDot));

    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public bool IsDot { get => GetValue(IsDotProperty); set => SetValue(IsDotProperty, value); }
}

public enum ConsoleLevel { Info, Success, Warning, Error }

/// <summary>Figma "Console Line": time, level icon and text; Info / Success / Warning / Error.</summary>
public class ConsoleLine : TemplatedControl
{
    public static readonly StyledProperty<string?> TimeProperty = AvaloniaProperty.Register<ConsoleLine, string?>(nameof(Time));
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<ConsoleLine, string?>(nameof(Text));
    public static readonly StyledProperty<ConsoleLevel> LevelProperty = AvaloniaProperty.Register<ConsoleLine, ConsoleLevel>(nameof(Level));

    public string? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ConsoleLevel Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
}

public enum ToastStatus { Success, Error }

/// <summary>Figma "Toast": Success / Error, 340 px wide, closable.</summary>
public class Toast : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Toast, string?>(nameof(Text));
    public static readonly StyledProperty<ToastStatus> StatusProperty = AvaloniaProperty.Register<Toast, ToastStatus>(nameof(Status));
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent = RoutedEvent.Register<Toast, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ToastStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public event EventHandler<RoutedEventArgs>? CloseRequested { add => AddHandler(CloseRequestedEvent, value); remove => RemoveHandler(CloseRequestedEvent, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_Close") is { } close)
            close.Click += (_, _) => RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
    }
}

/// <summary>Content of the Figma "Tooltip": text plus an optional shortcut.</summary>
public sealed record TipContent(string Text, string? Shortcut = null);

/// <summary>Renders a <see cref="TipContent"/> the way the Figma "Tooltip" shows it.</summary>
public class TooltipView : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<TooltipView, string?>(nameof(Text));
    public static readonly StyledProperty<string?> ShortcutProperty = AvaloniaProperty.Register<TooltipView, string?>(nameof(Shortcut));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
}

/// <summary>Figma "Badge": small count pill.</summary>
public class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Badge, string?>(nameof(Label));
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}

/// <summary>Figma "Keycap": one key of a shortcut.</summary>
public class Keycap : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Keycap, string?>(nameof(Label));
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}
