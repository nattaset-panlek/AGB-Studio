using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// Option lists of the Figma "Select" and Inspector "Field · Dropdown" boxes: a menu (Figma Menu Item rows) under the
/// box with the current value checked. The last menu opened is kept for tests.
/// </summary>
public static class Dropdown
{
    public static ContextMenu? Last { get; private set; }

    public static ContextMenu Show(Control anchor, IEnumerable<string> options, string? current, Action<string> pick)
    {
        var items = new List<Control>();
        foreach (var o in options)
        {
            var value = o;
            var mi = new MenuItem { Header = o, Tag = o };
            if (o == current) { mi.ToggleType = MenuItemToggleType.CheckBox; mi.IsChecked = true; }
            mi.Click += (_, _) => pick(value);
            items.Add(mi);
        }
        var cm = new ContextMenu { ItemsSource = items, Placement = PlacementMode.BottomEdgeAlignedLeft, MinWidth = Math.Max(0, anchor.Bounds.Width) };
        Last = cm;
        cm.Open(anchor);
        return cm;
    }

    /// <summary>Picks an option of the last opened menu (tests and keyboard-free automation).</summary>
    public static bool Choose(string option)
    {
        if (Last?.ItemsSource is not IEnumerable<Control> items || items.OfType<MenuItem>().FirstOrDefault(m => (string?)m.Tag == option) is not { } mi) return false;
        mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Last.Close();
        return true;
    }

    /// <summary>Opens the options when the Select box is clicked; <paramref name="pick"/> receives the new value.</summary>
    public static SelectField Attach(SelectField f, Func<IEnumerable<string>> options, Action<string> pick)
    {
        f.AddHandler(Button.ClickEvent, (_, e) =>
        {
            e.Handled = true;
            Show(f, options(), f.Selected, v => { f.Selected = v; pick(v); });
        });
        return f;
    }

    /// <summary>Opens the options when an Inspector dropdown field is clicked.</summary>
    public static InspectorField Attach(InspectorField f, Func<IEnumerable<string>> options, Action<string> pick)
    {
        f.AddHandler(Button.ClickEvent, (_, e) =>
        {
            e.Handled = true;
            Show(f, options(), f.Value, v => { f.Value = v; pick(v); });
        });
        return f;
    }
}

/// <summary>
/// File pickers of the data windows (Import CSV, Export .csv, New Table · CSV file…, New Speaker portrait): the OS
/// dialogs of <see cref="Services.SystemShell"/>, replaceable by tests.
/// </summary>
public sealed class FileChooser(EditorServices s)
{
    public Func<string, string, Task<string?>>? OpenOverride { get; set; }
    public Func<string, string, Task<string?>>? SaveOverride { get; set; }

    public static FileChooser For(EditorServices s)
    {
        if (s.GetService(typeof(FileChooser)) is FileChooser f) return f;
        f = new FileChooser(s);
        s.Set(f);
        return f;
    }

    /// <summary>Open dialog for one file type, e.g. ("Import CSV", "csv").</summary>
    public Task<string?> Open(string title, string extension) =>
        OpenOverride?.Invoke(title, extension) ?? s.System.PickFileAsync(title, (extension.ToUpperInvariant() + " files", ["*." + extension]));

    public Task<string?> Save(string title, string suggestedName) =>
        SaveOverride?.Invoke(title, suggestedName) ?? s.System.PickSaveFileAsync(title, suggestedName, Path.GetExtension(suggestedName).TrimStart('.'));
}

/// <summary>
/// Runs a refresh without re-entering it: a refresh asked for while one runs (an edit committed when a rebuild removes a
/// focused text box) runs again right after it.
/// </summary>
public sealed class Reentry
{
    bool _busy, _again;

    public void Run(Action refresh)
    {
        if (_busy) { _again = true; return; }
        _busy = true;
        try
        {
            do { _again = false; refresh(); } while (_again);
        }
        finally { _busy = false; }
    }
}

/// <summary>Small layout helpers for the data windows.</summary>
public static class Ui
{
    /// <summary>A horizontal 1 px border/default rule.</summary>
    public static Border Rule() => new Border { Height = 1 }.Token(Border.BackgroundProperty, "border/default");

    /// <summary>A Figma "Overline/11" section label (START FROM, ENUM VALUES, USE IT IN A SCRIPT ...).</summary>
    public static TextBlock Overline(string text) => UI.Text(text, "Overline/11", "text/secondary");

    public static T At<T>(this T c, int row, int col = 0) where T : Control
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        return c;
    }

    public static T Margin<T>(this T c, double l, double t, double r, double b) where T : Control
    {
        c.Margin = new Avalonia.Thickness(l, t, r, b);
        return c;
    }

    public static T Left<T>(this T c) where T : Control
    {
        c.HorizontalAlignment = HorizontalAlignment.Left;
        return c;
    }

    /// <summary>Commits a text box when it loses focus or on Enter (one undo step); Esc restores the current value.</summary>
    public static void CommitOnLeave(TextBox box, Func<string> current, Action<string> commit)
    {
        box.LostFocus += (_, _) => { if ((box.Text ?? "") != current()) commit(box.Text ?? ""); };
        box.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && !box.AcceptsReturn) { if ((box.Text ?? "") != current()) commit(box.Text ?? ""); e.Handled = true; }
            else if (e.Key == Avalonia.Input.Key.Escape) { box.Text = current(); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// An in-place editor: focused (all text selected) once the click that opened it is done, and <paramref name="left"/>
    /// runs when focus leaves it after that.
    /// </summary>
    public static void EditorFocus(TextBox box, Action left)
    {
        var focused = false;
        box.GotFocus += (_, _) => focused = true;
        box.LostFocus += (_, _) => { if (focused) left(); };
        void Focus() => Avalonia.Threading.Dispatcher.UIThread.Post(() => { box.Focus(); box.SelectAll(); });
        if (box.GetVisualRoot() != null) Focus();
        else box.AttachedToVisualTree += (_, _) => Focus();
    }

    /// <summary>Focuses the text box of a Text Input when its dialog opens (Figma shows the first field focused).</summary>
    public static void FocusOnOpen(Control dialog, TextField field) =>
        dialog.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(field).OfType<TextBox>().FirstOrDefault()?.Focus());

    /// <summary>Commits the text box inside an Inspector "Field · Text" (see <see cref="CommitOnLeave"/>).</summary>
    public static InspectorField CommitOnLeave(this InspectorField field, Func<string> current, Action<string> commit)
    {
        field.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextBox>("PART_Text") is { } box) CommitOnLeave(box, current, commit);
        };
        return field;
    }
}
