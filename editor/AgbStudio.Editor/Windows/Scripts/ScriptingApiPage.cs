using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>
/// Scripting API (T-P18, 1440 x 900; Help › Scripting API, Script Editor › Open Scripting API; P11.56): API tree with search,
/// page title and overview, member table (MEMBER / RETURNS / DESCRIPTION), example with Copy example, On this page and
/// see also. Built from the reference assembly on this machine, so it works offline ("Docs v0.1 · Offline").
/// Args: a page title, type or member ("Input", "Input.Pressed").
/// </summary>
[EditorWindow("T-P18")]
public sealed class ScriptingApiPage : DockPanel, IPageBreadcrumb
{
    readonly EditorServices _s;
    readonly string? _projectApi;
    readonly StackPanel _tree = new() { Spacing = 1, Margin = new Thickness(6, 2, 6, 6), Name = "ApiTree" };
    readonly StackPanel _content = new() { Name = "ApiContent", Margin = new Thickness(40, 20, 40, 32) };
    readonly StackPanel _onThisPage = new() { Spacing = 1, Margin = new Thickness(7, 4, 7, 6), Name = "OnThisPage" };
    readonly ScrollViewer _scroll;
    readonly Dictionary<string, Control> _anchors = [];

    /// <summary>The breadcrumb shows "Scripting API" (the page is not a document).</summary>
    public string Breadcrumb => "Scripting API";

    public SearchField Search { get; } = new() { Watermark = "Search API…", Name = "ApiSearch" };
    public ApiPage Page { get; private set; } = ScriptingApi.Pages.First(p => p.Title == "Input");
    public CodeEditor Example { get; } = new() { Name = "ApiExample", IsReadOnly = true, ShowLineNumbers = false, TextPadding = new Thickness(16, 12, 16, 12), Focusable = false };
    public IReadOnlyList<ApiRow> Rows { get; private set; } = [];
    public string StatusVersion { get; }

    public ScriptingApiPage(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public ScriptingApiPage(EditorServices s, string? target = null)
    {
        _s = s;
        Name = "ScriptingApiPage";
        if (s.Project.Current is { } p)
        {
            try { var ctx = ScriptAnalysis.ContextFor(p); _projectApi = ProjectApi.CSharp(ctx, ctx.Vars); }
            catch (Exception e) when (e is IOException or InvalidOperationException) { _projectApi = null; }
        }
        Example.Cursor = Avalonia.Input.Cursor.Default;

        var find = new IconButton { Glyph = "search", Name = "Tool_Search" };
        find.Click += (_, _) => Search.Focus();
        var docs = new IconButton { Glyph = "book", IsActive = true, Name = "Tool_Docs" };
        var chip = new Chip { Content = "GBA · Mode 0 · 240×160", Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: "VideoModeChip");
        Children.Add(PageChrome.Toolbar([PageChrome.ToolGroup(find, docs)], PageChrome.PlayGroup(s), [chip, PageChrome.UndoRedo(s)]));

        StatusVersion = "Docs " + ScriptingApi.Version();
        var version = new StatusItem { Glyph = "book", Content = StatusVersion, Name = "Status_Docs" };
        var offline = new StatusItem { Glyph = "check", Content = "Offline", Name = "Status_Offline" };
        Children.Add(PageChrome.StatusBar([version], [offline]));

        var left = new DockPanel();
        PageChrome.DockTo(left, new PanelHeader { Title = "API", Glyph = "book", Name = "ApiHeader" }, Dock.Top);
        PageChrome.DockTo(left, new Border { Padding = new Thickness(10, 8, 10, 6), Child = Search }, Dock.Top);
        left.Children.Add(new ScrollViewer { Content = _tree });

        _scroll = new ScrollViewer { Content = _content, Name = "ApiScroll" };
        // Doc Tabs over the page, as on every editor page: the open documents (back to the scene or the script that opened
        // the reference) and "Scripting API" itself. Without them the page had no way back but the title bar menus.
        var tabRow = Tilemap.PageChrome.DocTabs(s, "Scripting API", "book");
        var centerDock = new DockPanel();
        PageChrome.DockTo(centerDock, tabRow, Dock.Top);
        centerDock.Children.Add(_scroll);
        var center = UI.Frame(centerDock, "bg/base");

        var right = new DockPanel();
        PageChrome.DockTo(right, new PanelHeader { Title = "On this page", Glyph = "nodes", Name = "OnThisPageHeader" }, Dock.Top);
        right.Children.Add(_onThisPage);

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,260"), Name = "Body" };
        body.Children.Add(PageChrome.SidePanel(left, true, "LeftPanel"));
        Grid.SetColumn(center, 1);
        body.Children.Add(center);
        var rightPanel = PageChrome.SidePanel(right, false, "RightPanel");
        Grid.SetColumn(rightPanel, 2);
        body.Children.Add(rightPanel);
        Children.Add(body);

        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RebuildTree(); };
        Show(target is null ? Page : ScriptingApi.PageFor(target), target is { } t && t.Contains('.') ? t[(t.IndexOf('.') + 1)..] : null);
    }

    /// <summary>Titles shown in the tree (pages, or search results while searching).</summary>
    public IReadOnlyList<string> TreeLabels => _tree.Children.OfType<TreeItem>().Select(t => t.Label ?? "").ToList();

    void RebuildTree()
    {
        _tree.Children.Clear();
        var q = Search.Text?.Trim() ?? "";
        if (q.Length > 0)
        {
            foreach (var (page, m) in ScriptingApi.Search(q, _projectApi))
            {
                var label = m.Name == m.Type ? m.Type : $"{m.Type}.{m.Name}";
                var item = new TreeItem { Label = label, Glyph = page.Glyph, Meta = m.Kind == "method" ? "()" : null, Name = "Result_" + label };
                var (pg, member) = (page, m.Name);
                item.Activated += (_, _) => Show(pg, member);
                _tree.Children.Add(item);
            }
            if (_tree.Children.Count == 0) _tree.Children.Add(new Border { Padding = new Thickness(8), Child = UI.Text($"No API member matches “{q}”.", "Label/11", "text/muted") });
            return;
        }
        foreach (var page in ScriptingApi.Pages)
        {
            if (page.Types.Any(t => !ScriptingApi.Members(_projectApi).Any(m => m.Type == t)) && page.Types.All(t => !ScriptingApi.Members(_projectApi).Any(m => m.Type == t))) continue;
            var item = new TreeItem { Label = page.Title, Glyph = page.Glyph, Depth = page.Parent is null ? 0 : 1, IsSelected = page == Page, Name = "Page_" + page.Title.Split(' ')[0] };
            if (page.Parent != null) item.Padding = new Thickness(18, 0, 8, 0);
            var pg = page;
            item.Activated += (_, _) => Show(pg);
            _tree.Children.Add(item);
        }
    }

    /// <summary>Shows a page (and scrolls to a member row).</summary>
    public void Show(ApiPage page, string? member = null)
    {
        Page = page;
        _anchors.Clear();
        _content.Children.Clear();
        var title = UI.Text(page.Title, "Header 1", "text/primary");
        // the Figma page title is Inter Bold 28 (no text style of that size exists)
        title.FontSize = 28;
        title.FontWeight = FontWeight.Bold;
        title.LetterSpacing = -0.4;
        title.Margin = new Thickness(0, 0, 0, 8);
        _anchors["Overview"] = title;
        var overview = UI.Text(page.Overview, "Body/12", "text/secondary");
        overview.TextWrapping = TextWrapping.Wrap;
        overview.MaxWidth = 840;
        _content.Children.Add(title);
        _content.Children.Add(overview);

        Rows = ScriptingApi.Rows(page, _projectApi);
        if (Rows.Count > 0)
        {
            var members = Overline("Members");
            _anchors["Members"] = members;
            _content.Children.Add(members);
            _content.Children.Add(Table(Rows, member));
        }
        if (page.Example.Length > 0)
        {
            var ex = Overline("Example");
            _anchors["Example"] = ex;
            _content.Children.Add(ex);
            Example.Text = page.Example;
            Example.Spans = ScriptAnalysis.Colors(page.Example);
            Example.Height = Example.LineHeight * Example.LineCount + 24;
            if (Example.Parent is Border old) old.Child = null;
            var box = UI.Frame(Example, "bg/panel", radius: 8);
            box.ClipToBounds = true;
            box.MaxWidth = 840;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.Width = 840;
            Example.Token(BackgroundProperty, "bg/panel");
            _content.Children.Add(box);
        }
        if (page.SeeAlso.Count > 0)
        {
            var see = Overline("See also");
            _anchors["See also"] = see;
            _content.Children.Add(see);
            var links = UI.Row(6);
            foreach (var other in page.SeeAlso)
            {
                var b = new AgbButton { Content = other, Kind = ButtonKind.Ghost, Glyph = ScriptingApi.Find(other)?.Glyph, Name = "SeeAlso_" + other.Split(' ')[0] };
                var target = other;
                b.Click += (_, _) => Show(ScriptingApi.PageFor(target));
                links.Children.Add(b);
            }
            links.Margin = new Thickness(-10, 0, 0, 0);
            _content.Children.Add(links);
        }
        RebuildTree();
        RebuildOnThisPage();
        _scroll.Offset = default;
        if (member != null && _content.Children.OfType<StackPanel>().FirstOrDefault(c => c.Name == "MemberTable") is { } table &&
            table.Children.OfType<Control>().FirstOrDefault(r => r.Name == "Row_" + member) is { } row)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => row.BringIntoView());
    }

    static TextBlock Overline(string text)
    {
        var t = UI.Text(text, "Overline/11", "text/secondary");
        t.Margin = new Thickness(0, 14, 0, 14);
        return t;
    }

    static StackPanel Table(IReadOnlyList<ApiRow> rows, string? highlight)
    {
        var table = new StackPanel { Name = "MemberTable", Width = 760, HorizontalAlignment = HorizontalAlignment.Left };
        Grid Row(string a, string b, string c, TableCellKind first, TableCellKind second, TableCellKind third, double height, string name)
        {
            var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,100,400"), Height = height, Name = name };
            var cells = new[] { new TableCell { Text = a, Kind = first, Height = height }, new TableCell { Text = b, Kind = second, Height = height }, new TableCell { Text = c, Kind = third, Height = height } };
            for (var i = 0; i < 3; i++) { Grid.SetColumn(cells[i], i); g.Children.Add(cells[i]); }
            return g;
        }
        table.Children.Add(Row("Member", "Returns", "Description", TableCellKind.Header, TableCellKind.Header, TableCellKind.Header, 28, "Header"));
        foreach (var r in rows)
        {
            var sel = highlight != null && r.Name == highlight;
            table.Children.Add(Row(r.Member, r.Returns, r.Description, sel ? TableCellKind.Selected : TableCellKind.Mono, sel ? TableCellKind.Selected : TableCellKind.Mono,
                sel ? TableCellKind.Selected : TableCellKind.Default, 30, "Row_" + r.Name));
        }
        return table;
    }

    void RebuildOnThisPage()
    {
        _onThisPage.Children.Clear();
        var items = new List<(string Label, Action Go)> { ("Overview", () => ScrollTo("Overview")) };
        if (Rows.Count > 0) items.Add(("Members", () => ScrollTo("Members")));
        if (Page.Enum is { } en) items.Add(($"{en} enum", () => Show(ScriptingApi.PageFor(en))));
        if (Page.Example.Length > 0) items.Add(("Example", () => ScrollTo("Example")));
        if (Page.SeeAlso.Count > 0) items.Add(("See also: " + string.Join(", ", Page.SeeAlso), () => ScrollTo("See also")));
        for (var i = 0; i < items.Count; i++)
        {
            var (label, go) = items[i];
            var row = new ListRow { Label = label, Glyph = "dots", IsSelected = i == 0, Name = "OnPage_" + label.Split(' ', ':')[0] };
            row.Activated += (_, _) => { foreach (var r in _onThisPage.Children.OfType<ListRow>()) r.IsSelected = r == row; go(); };
            _onThisPage.Children.Add(row);
        }
        if (Page.Example.Length > 0)
        {
            var copy = new AgbButton { Content = "Copy example", Glyph = "code", Kind = ButtonKind.Secondary, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(6, 14, 6, 0), Name = "CopyExample" };
            copy.Click += (_, _) => _ = CopyExampleAsync();
            _onThisPage.Children.Add(copy);
        }
    }

    void ScrollTo(string anchor)
    {
        if (!_anchors.TryGetValue(anchor, out var c)) return;
        var y = c.TranslatePoint(default, _content)?.Y ?? 0;
        _scroll.Offset = new Vector(0, Math.Max(0, y - 8));
    }

    /// <summary>The "On this page" entries.</summary>
    public IReadOnlyList<string> OnThisPage => _onThisPage.Children.OfType<ListRow>().Select(r => r.Label ?? "").ToList();

    /// <summary>Copies the example to the clipboard (Copy example).</summary>
    public async Task<string> CopyExampleAsync()
    {
        var text = Page.Example;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb) await cb.SetTextAsync(text);
        _s.Toasts.Show("Example copied");
        return text;
    }
}
