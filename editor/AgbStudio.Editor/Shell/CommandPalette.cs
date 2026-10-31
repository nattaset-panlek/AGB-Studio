using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace AgbStudio.Editor.Shell;

/// <summary>Files opened from the palette, most recent first (ranks ties in the Files group).</summary>
public sealed class PaletteHistory
{
    public List<string> Files { get; } = [];
    public void Touch(string path) { Files.Remove(path); Files.Insert(0, path); }
    public int Rank(ProjectFile f) { var i = Files.FindIndex(p => string.Equals(p, f.Path, StringComparison.OrdinalIgnoreCase)); return i < 0 ? int.MaxValue : i; }
}

/// <summary>
/// Command Palette (T-O07, 640 x 325; P10.12): Ctrl+K, Edit › Command Palette…, Edit › Find in Project…. Commands and
/// project files, "&gt;" for commands only, Up / Down / Enter / Esc.
/// </summary>
[EditorWindow("T-O07")]
public sealed class CommandPalette : Border, IOverlayContent
{
    readonly EditorServices _s;
    readonly IReadOnlyList<ProjectFile> _files;
    readonly PaletteHistory _history;
    readonly StackPanel _results = new() { Spacing = 2, Name = "Results" };
    readonly List<(MenuRow Row, PaletteResult Item)> _rows = [];
    int _index;

    public TextBox Query { get; } = new() { Name = "PaletteQuery" };
    public event Action<object?>? Closed;
    public IReadOnlyList<PaletteResult> Items => _rows.Select(r => r.Item).ToList();
    public int HighlightIndex => _index;

    public CommandPalette(WindowContext ctx) : this(ctx.Services) { }

    public CommandPalette(EditorServices s, IReadOnlyList<ProjectFile>? files = null)
    {
        _s = s;
        Name = "CommandPalette";
        if (s.GetService(typeof(PaletteHistory)) is not PaletteHistory h) s.Set(h = new PaletteHistory());
        _history = h;
        _files = files ?? new ProjectFileIndex(s.Project.Current?.Root).Files();
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = 640;

        Query.Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareTextBox");
        Theme_Style(Query, "Body/12 Medium");
        var queryRow = new DockPanel { HorizontalSpacing = 10 };
        var icon = new Icon { Glyph = "search", Size = 16, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
        DockPanel.SetDock(icon, Dock.Left);
        var esc = new Keycap { Label = "Esc", VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(esc, Dock.Right);
        queryRow.Children.AddRange([icon, esc, Query]);

        var footer = UI.Row(14, Hint("↑↓", "navigate"), Hint("Enter", "open"), Hint(">", "commands only"));
        var content = UI.Column(2,
            new Border { Height = 44, Padding = new Thickness(12, 0), Child = queryRow },
            new Border { Height = 1 }.Token(Border.BackgroundProperty, "border/default"),
            _results,
            new Border { Height = 34, Padding = new Thickness(10, 8, 10, 4), Child = footer });
        content.Margin = new Thickness(8);
        Child = content;

        Query.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };
        Query.AddHandler(KeyDownEvent, OnQueryKey, RoutingStrategies.Tunnel);
        Refresh();
    }

    static void Theme_Style(TextBox t, string style) => AgbStudio.Editor.Theme.Fig.SetStyle(t, style);

    static StackPanel Hint(string key, string text) => UI.Row(6, new Keycap { Label = key, VerticalAlignment = VerticalAlignment.Center }, UI.Text(text, "Label/11", "text/muted"));

    void Refresh()
    {
        var (cmds, files) = PaletteSearch.Search(Query.Text ?? "", _s.Commands.All, _files,
            c => _s.Shortcuts.GestureOf(c.Id) is { } g ? AgbStudio.Editor.Theme.Shortcuts.Format(g) : null, _history.Rank);
        _results.Children.Clear();
        _rows.Clear();
        void Group(string title, IReadOnlyList<PaletteResult> items)
        {
            if (items.Count == 0) return;
            _results.Children.Add(new Border { Height = 26, Padding = new Thickness(10, 8, 10, 4), Child = UI.Text(title, "Overline/11", "text/muted") });
            foreach (var it in items)
            {
                var row = new MenuRow { Label = it.Label, Glyph = it.Glyph, RightText = it.Right };
                var item = it;
                row.Activated += (_, _) => Run(item);
                _rows.Add((row, it));
                _results.Children.Add(row);
            }
        }
        Group("Commands", cmds);
        Group("Files", files);
        Highlight(0);
    }

    void Highlight(int i)
    {
        _index = _rows.Count == 0 ? -1 : Math.Clamp(i, 0, _rows.Count - 1);
        for (var k = 0; k < _rows.Count; k++) _rows[k].Row.IsSelected = k == _index;
    }

    void OnQueryKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Highlight(_index + 1); e.Handled = true; break;
            case Key.Up: Highlight(_index - 1); e.Handled = true; break;
            case Key.Enter: if (_index >= 0) Run(_rows[_index].Item); e.Handled = true; break;
            case Key.Escape: Closed?.Invoke(null); e.Handled = true; break;
        }
    }

    void Run(PaletteResult item)
    {
        Closed?.Invoke(item);
        if (item.Command is { } c) _s.Commands.Execute(c.Id);
        else if (item.File is { } f)
        {
            _history.Touch(f.Path);
            if (_s.OpenWithTool?.Invoke(f.Path) == true) return;
            _ = _s.Windows.Open(EditorFor(f), f.Path, source: "CommandPalette");
        }
    }

    /// <summary>The Figma window that edits a file (Sprite Editor, Tilemap Editor, Script Editor, ...).</summary>
    public static string EditorFor(ProjectFile f) => f.Glyph switch
    {
        "tiles" => "T-P03",
        "image" => "T-P08",
        "code" => "T-P04",
        "type" => "T-P09",
        "table" => "T-P07",
        "book" => "T-P16",
        "nodes" => "T-P17",
        "music" or "sound" => "T-P15",
        _ => "T-P01",
    };
}
