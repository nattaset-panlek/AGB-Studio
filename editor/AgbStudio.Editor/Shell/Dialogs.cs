using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Shell;

/// <summary>Base for Figma dialogs built in code: dialog surface, header, content and footer.</summary>
public abstract class FigmaDialog : Border, IOverlayContent
{
    public event Action<object?>? Closed;
    protected void Close(object? result) => Closed?.Invoke(result);

    protected void Build(string title, string glyph, Control content, DialogFooter footer, double width) => Build(title, glyph, content, (Control)footer, width);

    /// <summary>As above, with a footer that holds more than the Dialog Footer component (a link at its left).</summary>
    protected void Build(string title, string glyph, Control content, Control footer, double width)
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

/// <summary>
/// Keyboard Shortcuts (T-D10, 760 x 390; Help › Keyboard Shortcuts). Every command that has a shortcut, with its
/// current binding, by group (General, Scene, Script, Logic Graph, Launcher) in two scrolling columns that start
/// with the Figma rows; search filters by name or keys. "Edit in Preferences › Shortcuts" opens T-S09 to change them.
/// </summary>
[EditorWindow("T-D10")]
public sealed class KeyboardShortcutsDialog : FigmaDialog
{
    /// <summary>The rows of the Figma frame, shown first in their groups: group, label, command.</summary>
    public static readonly IReadOnlyList<(string Group, string Label, string Command)> Rows =
    [
        ("General", "Command palette", "edit.commandPalette"), ("General", "Save", "file.save"), ("General", "Play", "build.play"),
        ("General", "Build ROM", "build.buildRom"), ("General", "Undo", "edit.undo"),
        ("Scene", "Select", "scene.tool.select"), ("Scene", "Move", "scene.tool.move"), ("Scene", "Paint tiles", "scene.tool.paint"),
        ("Scene", "Toggle grid", "scene.toggleGrid"), ("Scene", "Focus selection", "scene.focusSelection"),
    ];

    readonly ShortcutService _shortcuts;
    readonly IReadOnlyList<(string Group, string Label, string Command)> _all;
    readonly Grid _columns = new() { ColumnDefinitions = ColumnDefinitions.Parse("*,24,*"), Name = "ShortcutColumns" };
    public SearchField Search { get; } = new() { Watermark = "Search shortcuts…", Name = "ShortcutSearch" };
    /// <summary>Opens Preferences › Shortcuts (T-S09), where the shortcuts are changed.</summary>
    public AgbButton EditButton { get; } = new() { Content = "Edit in Preferences › Shortcuts", Glyph = "pencil", Kind = ButtonKind.Ghost, Size = ButtonSize.SM, Name = "EditShortcuts" };

    public KeyboardShortcutsDialog(WindowContext ctx) : this(ctx.Services.Shortcuts, ctx.Services.Commands)
    {
        EditButton.Click += (_, _) => { Close(null); _ = ctx.Services.Windows.Open("T-S09", source: "KeyboardShortcuts"); };
    }

    public KeyboardShortcutsDialog(ShortcutService shortcuts, Commands.CommandRegistry? commands = null)
    {
        _shortcuts = shortcuts;
        _all = AllRows(shortcuts, commands);
        Name = "KeyboardShortcutsDialog";
        var footer = new DialogFooter { ConfirmText = "Close", CancelText = null };
        footer.Confirm += (_, _) => Close(null);
        EditButton.HorizontalAlignment = HorizontalAlignment.Left;
        EditButton.VerticalAlignment = VerticalAlignment.Center;
        EditButton.Margin = new Thickness(14, 0, 0, 0);
        EditButton.IsVisible = commands != null;
        var foot = new Panel { Children = { footer, EditButton } };
        // the Figma rows fill the first view; the rest scroll under them
        var scroll = new ScrollViewer { Content = _columns, MaxHeight = 202, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Name = "ShortcutScroll" };
        var content = UI.Column(14, Search, scroll);
        content.Margin = new Thickness(20);
        Build("Keyboard Shortcuts", "type", content, foot, 760);
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        shortcuts.Changed += Fill;
        Fill();
    }

    static readonly (Commands.ShortcutContext Context, string Group)[] Groups =
        [(Commands.ShortcutContext.Global, "General"), (Commands.ShortcutContext.Scene, "Scene"), (Commands.ShortcutContext.Script, "Script"),
         (Commands.ShortcutContext.Graph, "Logic Graph"), (Commands.ShortcutContext.Launcher, "Launcher")];

    /// <summary>The Figma rows, then every other command with a default or current shortcut, in its context's group.</summary>
    public static IReadOnlyList<(string Group, string Label, string Command)> AllRows(ShortcutService shortcuts, Commands.CommandRegistry? commands)
    {
        var rows = Rows.ToList();
        if (commands == null) return rows;
        foreach (var (context, group) in Groups)
            foreach (var c in commands.All.Where(c => c.Context == context && shortcuts.GestureOf(c.Id) != null && rows.All(r => r.Command != c.Id)))
                rows.Add((group, c.Title.TrimEnd('…'), c.Id));
        return rows.OrderBy(r => Array.FindIndex(Groups, g => g.Group == r.Group)).ToList();
    }

    /// <summary>Every row (also the ones scrolled out of view): group, label, command.</summary>
    public IReadOnlyList<(string Group, string Label, string Command)> AllShortcuts => _all;

    /// <summary>Visible row labels (for tests).</summary>
    public IEnumerable<string> VisibleLabels => _columns.GetVisualDescendants().OfType<DockPanel>().Where(d => d.Tag is string).Select(d => (string)d.Tag!);

    /// <summary>Group titles in each column, top to bottom.</summary>
    public IEnumerable<string> GroupTitles => _columns.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "GroupTitle").Select(t => t.Text ?? "");

    void Fill()
    {
        _columns.Children.Clear();
        var q = Search.Text?.Trim() ?? "";
        var columns = new[] { UI.Column(24), UI.Column(24) };
        var i = 0;
        foreach (var g in _all.GroupBy(r => r.Group))
        {
            var title = UI.Text(g.Key, "Overline/11", "text/secondary");
            title.Name = "GroupTitle";
            var stack = UI.Column(6, title);
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
            if (stack.Children.Count == 1) continue;
            // General and Script on the left, Scene and Logic Graph on the right, as Figma starts them
            columns[i++ % 2].Children.Add(stack);
        }
        for (var c = 0; c < 2; c++)
        {
            Grid.SetColumn(columns[c], c * 2);
            _columns.Children.Add(columns[c]);
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
