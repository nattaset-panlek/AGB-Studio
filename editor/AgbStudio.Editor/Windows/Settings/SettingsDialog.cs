using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>One tab of a settings dialog: key (the Figma window id), Nav Item label and icon.</summary>
public sealed record SettingsTab(string Id, string Label, string Glyph);

/// <summary>
/// The frame shared by Project Settings and Preferences (900 x 620): Dialog Header, a 200 px side bar of Nav Items,
/// the tab page and a Dialog Footer with Cancel / Save. Edits go to a draft; Save applies it, Cancel / Esc / X drop it.
/// While the draft is invalid the footer says why and Save is disabled.
/// </summary>
public abstract class SettingsDialog : Border, IOverlayContent
{
    readonly StackPanel _nav = new() { Spacing = 2, Margin = new Thickness(10, 12, 10, 12), Name = "SettingsNav" };
    readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly Border _page = new() { Padding = new Thickness(20), Name = "SettingsPage" };
    AgbButton? _confirm;

    public event Action<object?>? Closed;
    public DialogFooter Footer { get; } = new() { CancelText = "Cancel", ConfirmText = "Save", Name = "SettingsFooter" };
    public DialogHeader Header { get; }
    public IReadOnlyList<SettingsTab> Tabs { get; }
    public string SelectedTab { get; private set; } = "";
    public IReadOnlyList<NavItem> NavItems => _nav.Children.OfType<NavItem>().ToList();
    /// <summary>Why Save is disabled, or null.</summary>
    public string? Error { get; private set; }
    public bool Saved { get; private set; }

    protected SettingsDialog(string title, IReadOnlyList<SettingsTab> tabs)
    {
        Tabs = tabs;
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("DialogSurface");
        Width = 900;
        Height = 620;
        Header = new DialogHeader { Title = title, Glyph = "sliders" };
        Header.CloseRequested += (_, _) => Close(null);
        Footer.Cancel += (_, _) => Close(null);
        Footer.Confirm += (_, _) => Save();
        Footer.TemplateApplied += (_, e) =>
        {
            _confirm = e.NameScope.Find<AgbButton>("PART_Confirm");
            if (_confirm != null) _confirm.IsEnabled = Error is null;
        };

        foreach (var t in tabs)
        {
            var item = new NavItem { Label = t.Label, Glyph = t.Glyph, Name = "Nav_" + t.Id, Tag = t.Id };
            item.Activated += (_, _) => Select(t.Id);
            _nav.Children.Add(item);
        }
        var side = UI.Frame(_nav, "bg/base");
        side.Width = 200;
        _scroll.Content = _page;

        var body = new DockPanel();
        DockPanel.SetDock(side, Dock.Left);
        body.Children.AddRange([side, _scroll]);
        var root = new DockPanel();
        DockPanel.SetDock(Header, Dock.Top);
        DockPanel.SetDock(Footer, Dock.Bottom);
        root.Children.AddRange([Header, Footer, body]);
        Child = root;
    }

    /// <summary>Shows a tab (its Figma id, e.g. "T-S03"); the page is rebuilt from the draft.</summary>
    public void Select(string id)
    {
        if (Tabs.All(t => t.Id != id)) id = Tabs[0].Id;
        SelectedTab = id;
        foreach (var n in NavItems) n.IsSelected = (string?)n.Tag == id;
        _page.Child = BuildTab(id);
        _scroll.Offset = default;
    }

    /// <summary>Rebuilds the shown tab when <paramref name="source"/> fires while the dialog is open and the tab is one of <paramref name="tabs"/> (e.g. the toolchain check finished).</summary>
    protected void RefreshOn(Action<Action> subscribe, Action<Action> unsubscribe, params string[] tabs)
    {
        void Refresh() => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (tabs.Contains(SelectedTab)) Select(SelectedTab); });
        AttachedToVisualTree += (_, _) => subscribe(Refresh);
        DetachedFromVisualTree += (_, _) => unsubscribe(Refresh);
    }

    /// <summary>The current page (for tests).</summary>
    public Control? Page => _page.Child;

    protected abstract Control BuildTab(string id);

    /// <summary>Validates the draft; returns the first problem in plain language, or null.</summary>
    protected abstract string? Validate();

    /// <summary>Applies the draft to the stores.</summary>
    protected abstract void Apply();

    /// <summary>Call after every edit: updates the footer hint and the Save button.</summary>
    protected void Changed()
    {
        Error = Validate();
        Footer.Hint = Error;
        if (_confirm != null) _confirm.IsEnabled = Error is null;
    }

    /// <summary>Save: applies a valid draft and closes with "saved".</summary>
    public bool Save()
    {
        Changed();
        if (Error != null) return false;
        Apply();
        Saved = true;
        Close("saved");
        return true;
    }

    protected void Close(object? result) => Closed?.Invoke(result);

    /// <summary>A page column: vertical stack with the Figma gap.</summary>
    protected static StackPanel Column(double gap, params Control[] children)
    {
        var s = UI.Column(gap, children);
        s.HorizontalAlignment = HorizontalAlignment.Stretch;
        return s;
    }
}
