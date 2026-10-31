using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Scripting.Graph;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.LogicGraph;

/// <summary>
/// Add Node (T-O08, 380 x 269; Logic Graph › right-click canvas, Nodes › Add node…; P11.49): search the node catalog,
/// results grouped by category, Up / Down, Enter adds the node where the canvas was clicked, Shift+Enter adds it and
/// connects it to the pin the wire was dragged from (or the selected node).
/// </summary>
[EditorWindow("T-O08")]
public sealed class AddNodePopover : Border, IOverlayContent
{
    // Figma: at most six lines under the search (category headers count), so the footer stays in the 269 px popover,
    // and at most three nodes of a category, so the next category shows too ("veloc": PHYSICS, then MATH)
    const int MaxLines = 6, PerCategory = 3;
    readonly AddNodeRequest? _request;
    readonly StackPanel _results = new() { Spacing = 0, Name = "Results" };
    readonly List<(MenuRow Row, NodeDef Def)> _rows = [];
    int _index;

    public event Action<object?>? Closed;
    public TextBox Query { get; } = new() { Name = "NodeQuery" };
    public IReadOnlyList<NodeDef> Items => _rows.Select(r => r.Def).ToList();
    public int HighlightIndex => _index;
    /// <summary>The id of the node the popover added (tests).</summary>
    public string? Added { get; private set; }

    public AddNodePopover(WindowContext ctx) : this(ctx.Args as AddNodeRequest, (ctx.Args as AddNodeRequest)?.Query) { }

    public AddNodePopover(AddNodeRequest? request, string? query = null)
    {
        _request = request;
        Name = "AddNode";
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = 380;
        Query.Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareTextBox");
        AgbStudio.Editor.Theme.Fig.SetStyle(Query, "Body/12");
        var queryRow = new DockPanel { HorizontalSpacing = 10 };
        var icon = new Icon { Glyph = "search", Size = 16, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
        DockPanel.SetDock(icon, Dock.Left);
        var esc = new Keycap { Label = "Esc", VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(esc, Dock.Right);
        queryRow.Children.AddRange([icon, esc, Query]);
        var footer = UI.Row(14, Hint("Enter", "add here"), Hint("Shift+Enter", "add and connect"));
        // Figma: a 50 px search row with the icon and Esc 3 px under its middle, Esc 18 px from the edge; the footer 9 px
        // under the last result
        Child = UI.Column(0,
            new Border { Height = 50, Padding = new Thickness(18, 6, 18, 0), Child = queryRow },
            PageChrome.Divider(),
            new Border { Padding = new Thickness(8, 1, 8, 0), Child = _results },
            new Border { Padding = new Thickness(18, 9, 18, 12), Child = footer });
        Query.Text = query ?? "";
        Query.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };
        Query.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Refresh();
    }

    static StackPanel Hint(string key, string text) => UI.Row(8, new Keycap { Label = key, VerticalAlignment = VerticalAlignment.Center }, UI.Text(text, "Label/11", "text/muted"));

    /// <summary>Catalog nodes matching the query, best first, grouped by category in that order.</summary>
    public static IReadOnlyList<NodeDef> Search(string query)
    {
        query = query.Trim();
        // a word of the title that starts with the query ranks like a prefix ("veloc" finds Set Velocity X as well as
        // Velocity X); then nodes that do something come before nodes that read a value
        var scored = NodeCatalog.All.Select((n, i) => (n, i, s: query.Length == 0 ? 0 : Math.Max(Shell.PaletteSearch.Score(n.Title, query), Shell.PaletteSearch.Score(n.Type, query))))
            .Where(x => x.s >= 0).OrderByDescending(x => Math.Min(x.s, 800)).ThenBy(x => x.n.Role == NodeRole.Value ? 1 : 0).ThenByDescending(x => x.s).ThenBy(x => x.i)
            .Select(x => x.n).ToList();
        // categories in the order their best match appears
        var shown = new List<NodeDef>();
        var lines = 0;
        foreach (var g in scored.GroupBy(n => n.Category))
        {
            if (lines + 2 > MaxLines) break;
            var take = g.Take(Math.Min(PerCategory, MaxLines - lines - 1)).ToList();
            shown.AddRange(take);
            lines += take.Count + 1;
        }
        return shown;
    }

    void Refresh()
    {
        _results.Children.Clear();
        _rows.Clear();
        string? cat = null;
        foreach (var def in Search(Query.Text ?? ""))
        {
            if (def.Category != cat)
            {
                cat = def.Category;
                var square = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
                if (Application.Current?.FindResource(GraphDocument.CategoryColor(cat)) is Color c) square.Background = new SolidColorBrush(c);
                _results.Children.Add(new Border { Height = 26, Padding = new Thickness(10, 8, 10, 2), Child = UI.Row(8, square, UI.Text(cat, "Overline/11", "text/muted")) });
            }
            var row = new MenuRow { Label = def.Title, Height = 30, Name = "Node_" + def.Type };
            // the hint in the Figma row is Label/11, not the mono shortcut style of the Menu Item
            var hint = UI.Text(GraphDocument.Hint(def.Type), "Label/11", "text/muted");
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            hint.Margin = new Thickness(0, 0, 10, 0);
            hint.IsHitTestVisible = false;
            row.PropertyChanged += (_, e) => { if (e.Property == SelectableRow.IsSelectedProperty) hint.Token(TextBlock.ForegroundProperty, row.IsSelected ? "text/on-selected" : "text/muted"); };
            var d = def;
            row.Activated += (_, _) => Add(d, connect: false);
            _rows.Add((row, def));
            _results.Children.Add(new Panel { Children = { row, hint } });
        }
        if (_rows.Count == 0) _results.Children.Add(new Border { Height = 30, Padding = new Thickness(10, 8), Child = UI.Text("No node matches.", "Label/11", "text/muted") });
        Highlight(0);
    }

    void Highlight(int i)
    {
        _index = _rows.Count == 0 ? -1 : Math.Clamp(i, 0, _rows.Count - 1);
        for (var k = 0; k < _rows.Count; k++) _rows[k].Row.IsSelected = k == _index;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Highlight(_index + 1); e.Handled = true; break;
            case Key.Up: Highlight(_index - 1); e.Handled = true; break;
            case Key.Enter:
                if (_index >= 0) Add(_rows[_index].Def, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                e.Handled = true;
                break;
            case Key.Escape: Closed?.Invoke(null); e.Handled = true; break;
        }
    }

    /// <summary>Adds the node (and connects it with Shift+Enter) and closes.</summary>
    public string? Add(NodeDef def, bool connect)
    {
        if (_request is { } r)
        {
            var from = connect ? r.From ?? r.Page.DefaultConnectFrom() : null;
            Added = r.Page.AddNode(def.Type, r.Position, from);
        }
        Closed?.Invoke(Added);
        return Added;
    }
}
