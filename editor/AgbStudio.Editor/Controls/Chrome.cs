using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace AgbStudio.Editor.Controls;

/// <summary>
/// Figma "Title Bar" (36 px): the 7 menus, the breadcrumb (project / document) and the Debug / Build buttons.
/// No brand text: the OS window title already shows the AGB Studio icon and name.
/// </summary>
public class TitleBar : TemplatedControl
{
    public static readonly StyledProperty<string?> ProjectProperty = AvaloniaProperty.Register<TitleBar, string?>(nameof(Project));
    public static readonly StyledProperty<string?> DocumentProperty = AvaloniaProperty.Register<TitleBar, string?>(nameof(Document));
    public static readonly StyledProperty<object?> MenusProperty = AvaloniaProperty.Register<TitleBar, object?>(nameof(Menus));
    public static readonly StyledProperty<ICommand?> DebugCommandProperty = AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(DebugCommand));
    public static readonly StyledProperty<ICommand?> BuildCommandProperty = AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(BuildCommand));
    public static readonly StyledProperty<ICommand?> BreadcrumbCommandProperty = AvaloniaProperty.Register<TitleBar, ICommand?>(nameof(BreadcrumbCommand));

    public string? Project { get => GetValue(ProjectProperty); set => SetValue(ProjectProperty, value); }
    public string? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    /// <summary>The menu bar (an Avalonia Menu) shown at the left edge.</summary>
    public object? Menus { get => GetValue(MenusProperty); set => SetValue(MenusProperty, value); }
    public ICommand? DebugCommand { get => GetValue(DebugCommandProperty); set => SetValue(DebugCommandProperty, value); }
    public ICommand? BuildCommand { get => GetValue(BuildCommandProperty); set => SetValue(BuildCommandProperty, value); }
    public ICommand? BreadcrumbCommand { get => GetValue(BreadcrumbCommandProperty); set => SetValue(BreadcrumbCommandProperty, value); }
}

/// <summary>Figma "Dialog Header" (52 px): icon, title and close button.</summary>
public class DialogHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DialogHeader, string?>(nameof(Title));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<DialogHeader, string?>(nameof(Glyph), "folderplus");
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent = RoutedEvent.Register<DialogHeader, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public event EventHandler<RoutedEventArgs>? CloseRequested { add => AddHandler(CloseRequestedEvent, value); remove => RemoveHandler(CloseRequestedEvent, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_Close") is { } b) b.Click += (_, _) => RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
    }
}

/// <summary>Figma "Dialog Footer" (60 px): hint that hugs its text, Cancel and Confirm buttons.</summary>
public class DialogFooter : TemplatedControl
{
    public static readonly StyledProperty<string?> HintProperty = AvaloniaProperty.Register<DialogFooter, string?>(nameof(Hint));
    public static readonly StyledProperty<string?> CancelTextProperty = AvaloniaProperty.Register<DialogFooter, string?>(nameof(CancelText), "Cancel");
    public static readonly StyledProperty<string?> ConfirmTextProperty = AvaloniaProperty.Register<DialogFooter, string?>(nameof(ConfirmText), "OK");
    public static readonly StyledProperty<ButtonKind> ConfirmKindProperty = AvaloniaProperty.Register<DialogFooter, ButtonKind>(nameof(ConfirmKind), ButtonKind.Primary);
    public static readonly RoutedEvent<RoutedEventArgs> CancelEvent = RoutedEvent.Register<DialogFooter, RoutedEventArgs>(nameof(Cancel), RoutingStrategies.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> ConfirmEvent = RoutedEvent.Register<DialogFooter, RoutedEventArgs>(nameof(Confirm), RoutingStrategies.Bubble);

    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }
    public string? ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }
    public ButtonKind ConfirmKind { get => GetValue(ConfirmKindProperty); set => SetValue(ConfirmKindProperty, value); }
    public event EventHandler<RoutedEventArgs>? Cancel { add => AddHandler(CancelEvent, value); remove => RemoveHandler(CancelEvent, value); }
    public event EventHandler<RoutedEventArgs>? Confirm { add => AddHandler(ConfirmEvent, value); remove => RemoveHandler(ConfirmEvent, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_Cancel") is { } c) c.Click += (_, _) => RaiseEvent(new RoutedEventArgs(CancelEvent));
        if (e.NameScope.Find<Button>("PART_Confirm") is { } o) o.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ConfirmEvent));
    }
}

/// <summary>Figma "Popover Header" (40 px): title and close button.</summary>
public class PopoverHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<PopoverHeader, string?>(nameof(Title));
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent = RoutedEvent.Register<PopoverHeader, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public event EventHandler<RoutedEventArgs>? CloseRequested { add => AddHandler(CloseRequestedEvent, value); remove => RemoveHandler(CloseRequestedEvent, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_Close") is { } b) b.Click += (_, _) => RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
    }
}

/// <summary>Figma "brand/mark": the Plus Pad logo (96 px), scales with Width/Height.</summary>
public class BrandMark : TemplatedControl;

/// <summary>Figma menu frame: bg/raised, border/strong, radius 8, padding 4, popover shadow; holds Menu Item and Menu Separator.</summary>
public class MenuSurface : ItemsControl;
