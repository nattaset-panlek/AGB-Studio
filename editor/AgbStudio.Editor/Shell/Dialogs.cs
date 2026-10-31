using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Shell;

/// <summary>Base for Figma dialogs built in code: dialog surface, header, content and footer.</summary>
public abstract class FigmaDialog : Border, IOverlayContent
{
    public event Action<object?>? Closed;
    protected void Close(object? result) => Closed?.Invoke(result);

    protected void Build(string title, string glyph, Control content, DialogFooter footer, double width)
    {
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("DialogSurface");
        Width = width;
        var header = new DialogHeader { Title = title, Glyph = glyph };
        header.CloseRequested += (_, _) => Close(null);
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.AddRange([header, footer, content]);
        Child = dock;
    }
}

/// <summary>Keyboard Shortcuts (T-D10, 760 x 390; Help › Keyboard Shortcuts). Shows the current bindings, filtered by search.</summary>
[EditorWindow("T-D10")]
public sealed class KeyboardShortcutsDialog : FigmaDialog
{
    /// <summary>Rows as in Figma: group, label, command.</summary>
    public static readonly IReadOnlyList<(string Group, string Label, string Command)> Rows =
    [
        ("General", "Command palette", "edit.commandPalette"), ("General", "Save", "file.save"), ("General", "Play", "build.play"),
        ("General", "Build ROM", "build.buildRom"), ("General", "Undo", "edit.undo"),
        ("Scene", "Select", "scene.tool.select"), ("Scene", "Move", "scene.tool.move"), ("Scene", "Paint tiles", "scene.tool.paint"),
        ("Scene", "Toggle grid", "scene.toggleGrid"), ("Scene", "Focus selection", "scene.focusSelection"),
    ];

    readonly ShortcutService _shortcuts;
    readonly Grid _columns = new() { ColumnDefinitions = ColumnDefinitions.Parse("*,24,*"), Name = "ShortcutColumns" };
    public SearchField Search { get; } = new() { Watermark = "Search shortcuts…", Name = "ShortcutSearch" };

    public KeyboardShortcutsDialog(WindowContext ctx) : this(ctx.Services.Shortcuts) { }

    public KeyboardShortcutsDialog(ShortcutService shortcuts)
    {
        _shortcuts = shortcuts;
        Name = "KeyboardShortcutsDialog";
        var footer = new DialogFooter { ConfirmText = "Close", CancelText = null };
        footer.Confirm += (_, _) => Close(null);
        var content = UI.Column(14, Search, _columns);
        content.Margin = new Thickness(20);
        Build("Keyboard Shortcuts", "type", content, footer, 760);
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        shortcuts.Changed += Fill;
        Fill();
    }

    /// <summary>Visible row labels (for tests).</summary>
    public IEnumerable<string> VisibleLabels => _columns.Children.OfType<StackPanel>().SelectMany(c => c.Children.OfType<DockPanel>()).Select(d => (string)d.Tag!);

    void Fill()
    {
        _columns.Children.Clear();
        var q = Search.Text?.Trim() ?? "";
        var col = 0;
        foreach (var g in Rows.GroupBy(r => r.Group))
        {
            var stack = UI.Column(6, UI.Text(g.Key, "Overline/11", "text/secondary"));
            foreach (var (_, label, cmd) in g)
            {
                var keys = _shortcuts.GestureOf(cmd) is { } k ? AgbStudio.Editor.Theme.Shortcuts.Format(k) : "";
                if (q.Length > 0 && !label.Contains(q, StringComparison.OrdinalIgnoreCase) && !keys.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                var row = new DockPanel { Height = 30, HorizontalSpacing = 6, Tag = label };
                var caps = UI.Row(6, KeyParts(keys).Select(p => (Control)new Keycap { Label = p, VerticalAlignment = VerticalAlignment.Center }).ToArray());
                caps.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(caps, Dock.Right);
                row.Children.AddRange([caps, UI.Text(label, "Body/12", "text/primary")]);
                stack.Children.Add(row);
            }
            Grid.SetColumn(stack, col);
            _columns.Children.Add(stack);
            col += 2;
        }
    }

    /// <summary>"Ctrl+Shift+S" -> Ctrl, Shift, S; "Ctrl+," -> Ctrl, ",".</summary>
    public static IEnumerable<string> KeyParts(string keys) => keys.Length == 0 ? [] : keys.EndsWith("++") ? [.. keys[..^2].Split('+'), "+"] : keys.Split('+');
}

/// <summary>Why Unsaved Changes asks: the editor exits, another project opens, or the open project closes (a new one replaces it).</summary>
public enum UnsavedReason { Exit, OpenProject, CloseProject }

/// <summary>What Unsaved Changes is opened with: the dirty documents and the action that would lose them.</summary>
public sealed record UnsavedChangesArgs(IReadOnlyList<EditorDocument> Documents, UnsavedReason Reason);

/// <summary>
/// Unsaved Changes (T-D12, 460 x 262): File › Exit or closing the app, opening another project or creating a new one
/// with dirty documents; the title names that action. Result "save", "discard" or null (stay).
/// </summary>
[EditorWindow("T-D12")]
public sealed class UnsavedChangesDialog : FigmaDialog
{
    public UnsavedChangesDialog(WindowContext ctx) : this(
        ctx.Args switch { UnsavedChangesArgs a => a.Documents, IReadOnlyList<EditorDocument> l => l, _ => ctx.Services.Documents.Dirty },
        ctx.Args is UnsavedChangesArgs r ? r.Reason : UnsavedReason.Exit) { }

    /// <summary>The dialog title for the action that would lose the changes.</summary>
    public static string TitleFor(UnsavedReason reason) => reason switch
    {
        UnsavedReason.OpenProject => "Save changes before opening another project?",
        UnsavedReason.CloseProject => "Save changes before closing the project?",
        _ => "Save changes before exiting?",
    };

    public UnsavedChangesDialog(IReadOnlyList<EditorDocument> dirty, UnsavedReason reason = UnsavedReason.Exit)
    {
        Name = "UnsavedChangesDialog";
        var n = dirty.Count;
        var message = UI.Text(n == 1 ? "1 file has changes that will be lost if you don't save it." : $"{n} files have changes that will be lost if you don't save them.", "Body/12", "text/secondary");
        message.TextWrapping = TextWrapping.Wrap;
        var content = UI.Column(14, message);
        foreach (var d in dirty)
        {
            var row = new DockPanel { HorizontalSpacing = 8 };
            var icon = new Icon { Glyph = d.Glyph, Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
            var state = UI.Text("modified", "Label/11", "status/warning");
            DockPanel.SetDock(icon, Dock.Left);
            DockPanel.SetDock(state, Dock.Right);
            row.Children.AddRange([icon, state, UI.Text(d.Name, "Body/12 Medium", "text/primary")]);
            content.Children.Add(UI.Frame(row, "bg/base", pad: new Thickness(10, 8), radius: 6));
        }
        content.Margin = new Thickness(20);
        var footer = new DialogFooter { CancelText = "Don't Save", ConfirmText = "Save All" };
        footer.Cancel += (_, _) => Close("discard");
        footer.Confirm += (_, _) => Close("save");
        Build(TitleFor(reason), "warn", content, footer, 460);
    }
}
