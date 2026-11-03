using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AgbStudio.Editor.Controls;

public enum TreeToggle { None, Open, Closed }

/// <summary>Figma "Tree Item": State Default / Selected / Hover, Toggle None / Open / Closed, optional meta. Depth sets the indent (6 / 18 / 36 / 54).</summary>
public class TreeItem : SelectableRow
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<TreeItem, string?>(nameof(Label));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<TreeItem, string?>(nameof(Glyph), "cube");
    public static readonly StyledProperty<string?> MetaProperty = AvaloniaProperty.Register<TreeItem, string?>(nameof(Meta));
    public static readonly StyledProperty<TreeToggle> ToggleProperty = AvaloniaProperty.Register<TreeItem, TreeToggle>(nameof(Toggle));
    public static readonly StyledProperty<int> DepthProperty = AvaloniaProperty.Register<TreeItem, int>(nameof(Depth));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public TreeToggle Toggle { get => GetValue(ToggleProperty); set => SetValue(ToggleProperty, value); }
    public int Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }

    /// <summary>Small component icons after the label (Hierarchy: what an actor uses; not in Figma, see
    /// docs/evidence/ui-changes-since-figma.md "Templates").</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> BadgesProperty = AvaloniaProperty.Register<TreeItem, IReadOnlyList<string>?>(nameof(Badges));

    public IReadOnlyList<string>? Badges { get => GetValue(BadgesProperty); set => SetValue(BadgesProperty, value); }

    StackPanel? _badges;

    /// <summary>Left padding per depth, from the Scene Editor hierarchy in Figma.</summary>
    public static double IndentOf(int depth) => depth switch { <= 0 => 6, 1 => 18, 2 => 36, _ => 54 };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _badges = e.NameScope.Find<StackPanel>("PART_Badges");
        FillBadges();
    }

    void FillBadges()
    {
        if (_badges == null) return;
        _badges.Children.Clear();
        foreach (var g in Badges ?? [])
            _badges.Children.Add(new AgbStudio.Editor.Graphics.Icon { Glyph = g, Size = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Opacity = 0.85 });
        _badges.IsVisible = _badges.Children.Count > 0;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DepthProperty) Padding = new Thickness(IndentOf(Depth), 0, 8, 0);
        else if (change.Property == BadgesProperty) FillBadges();
    }
}

/// <summary>Figma "Doc Tab": Default / Active / Hover with a dirty dot.</summary>
public class DocTab : SelectableRow
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<DocTab, string?>(nameof(Label));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<DocTab, string?>(nameof(Glyph), "map");
    public static readonly StyledProperty<bool> IsDirtyProperty = AvaloniaProperty.Register<DocTab, bool>(nameof(IsDirty));
    public static readonly StyledProperty<bool> IsClosableProperty = AvaloniaProperty.Register<DocTab, bool>(nameof(IsClosable));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public bool IsDirty { get => GetValue(IsDirtyProperty); set => SetValue(IsDirtyProperty, value); }
    public bool IsClosable { get => GetValue(IsClosableProperty); set => SetValue(IsClosableProperty, value); }

    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent = RoutedEvent.Register<DocTab, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);
    public event EventHandler<RoutedEventArgs>? CloseRequested { add => AddHandler(CloseRequestedEvent, value); remove => RemoveHandler(CloseRequestedEvent, value); }

    public void RequestClose() => RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_Close") is { } b)
        {
            b.Click += (_, ev) =>
            {
                ev.Handled = true;
                RequestClose();
            };
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed && IsClosable)
        {
            RequestClose();
            e.Handled = true;
            return;
        }
        if (DeferActivation && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressedClicks = e.ClickCount;
            _pressedKeys = e.KeyModifiers;
            e.Handled = true;
            return;
        }
        base.OnPointerPressed(e);
    }

    /// <summary>Activates on release instead of on press, so that the tab can be dragged without leaving the page.</summary>
    public bool DeferActivation { get; set; }
    int _pressedClicks;
    KeyModifiers _pressedKeys;

    /// <summary>A drag started: the release that ends it does not activate the tab.</summary>
    public void CancelActivation() => _pressedClicks = 0;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressedClicks == 0) return;
        var clicks = _pressedClicks;
        _pressedClicks = 0;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Activate(clicks, _pressedKeys);
    }
}

/// <summary>Figma "Panel Tab": Default / Active / Hover with an optional count badge.</summary>
public class PanelTab : SelectableRow
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<PanelTab, string?>(nameof(Label));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<PanelTab, string?>(nameof(Glyph), "folder");
    public static readonly StyledProperty<int?> CountProperty = AvaloniaProperty.Register<PanelTab, int?>(nameof(Count));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public int? Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
}

/// <summary>Figma "List Item": Default / Selected / Hover with icon and meta.</summary>
public class ListRow : SelectableRow
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<ListRow, string?>(nameof(Label));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<ListRow, string?>(nameof(Glyph), "image");
    public static readonly StyledProperty<string?> MetaProperty = AvaloniaProperty.Register<ListRow, string?>(nameof(Meta));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
}

/// <summary>Figma "Nav Item": Default / Active / Hover (settings dialogs, Launcher).</summary>
public class NavItem : SelectableRow
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<NavItem, string?>(nameof(Label));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<NavItem, string?>(nameof(Glyph), "sliders");

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>Figma "Section Header": Open / Closed (Inspector component sections). Click toggles.</summary>
public class SectionHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<SectionHeader, string?>(nameof(Title));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<SectionHeader, string?>(nameof(Glyph), "move");
    public static readonly StyledProperty<string?> MetaProperty = AvaloniaProperty.Register<SectionHeader, string?>(nameof(Meta));
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<SectionHeader, bool>(nameof(IsOpen), true);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        IsOpen = !IsOpen;
        e.Handled = true;
    }
}

/// <summary>Figma "Panel Header": icon, upper-case title and an optional action icon.</summary>
public class PanelHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<PanelHeader, string?>(nameof(Title));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<PanelHeader, string?>(nameof(Glyph), "layers");
    public static readonly StyledProperty<string?> ActionGlyphProperty = AvaloniaProperty.Register<PanelHeader, string?>(nameof(ActionGlyph));
    public static readonly StyledProperty<ICommand?> ActionCommandProperty = AvaloniaProperty.Register<PanelHeader, ICommand?>(nameof(ActionCommand));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string? ActionGlyph { get => GetValue(ActionGlyphProperty); set => SetValue(ActionGlyphProperty, value); }
    public ICommand? ActionCommand { get => GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
}

/// <summary>Figma "Search Field": 28 px text box with a search icon and placeholder.</summary>
public class SearchField : TextBox
{
    protected override Type StyleKeyOverride => typeof(SearchField);
}
