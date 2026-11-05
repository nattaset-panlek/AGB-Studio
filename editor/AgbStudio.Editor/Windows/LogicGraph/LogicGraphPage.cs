using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Scripting.Graph;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.LogicGraph;

/// <summary>Where Add Node puts the new node (graph coordinates) and the output pin to connect from (Shift+Enter).</summary>
public sealed record AddNodeRequest(LogicGraphPage Page, Point Position, (string Node, string Pin)? From, string? Query = null);

/// <summary>Open graphs of an editor instance (pages are views; a graph keeps its undo and dirty state across them).</summary>
public sealed class GraphSession
{
    public Dictionary<string, (GraphDocument Doc, EditorDocument Tab)> Open { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static GraphSession Of(EditorServices s)
    {
        if (s.GetService(typeof(GraphSession)) is GraphSession g) return g;
        var created = new GraphSession();
        s.Set(created);
        return created;
    }
}

/// <summary>
/// Logic Graph (T-P17, 1440 x 900; Tools › Logic Graph, Create › Logic Graph; P11.48-P11.49): node palette by category with
/// search, the graph canvas, preview, node inspector, Generated C# panel kept in sync both ways (D-11), status
/// (nodes, wires, compiled). Edits .graph files (and writes the generated .cs on save). Args: a .graph path or its tab.
/// </summary>
[EditorWindow("T-P17")]
public sealed class LogicGraphPage : DockPanel
{
    readonly EditorServices _s;
    readonly StackPanel _palette = new() { Spacing = 1, Margin = new Thickness(6, 2, 6, 6), Name = "Palette" };
    readonly HashSet<string> _openCategories = new(StringComparer.Ordinal) { "Events", "Physics" };
    readonly StackPanel _inspector = new() { Name = "NodeInspector" };
    readonly SectionHeader _codeHeader = new() { Title = "Generated C#", Glyph = "code", Meta = "in sync", Height = 38, Name = "CodeHeader" };
    readonly Chip _syncChip = new() { Content = "Graph ⇄ C# in sync", Glyph = "check", Style = ChipStyle.Success, Name = "SyncChip" };
    readonly StatusItem _counts = new() { Glyph = "nodes", Name = "Status_Counts" };
    readonly StatusItem _compiled = new() { Glyph = "check", Content = "Compiled to C#", Name = "Status_Compiled" };
    readonly IconButton[] _tools;
    EditorDocument _tab = null!;
    string? _paletteSelection;
    bool _codeFromGraph;
    IDisposable? _codeTimer;

    public GraphDocument Document { get; private set; } = new();
    public GraphCanvas Canvas { get; }
    public CodeEditor Code { get; } = new() { Name = "GeneratedCode", TextStyleName = "Mono/11", ShowLineNumbers = false, TextPadding = new Thickness(10, 10, 10, 10) };
    public SearchField Search { get; } = new() { Watermark = "Add node…", Name = "NodeSearch" };
    /// <summary>Preview: the project's scene as the ROM draws it, its sprite animations playing.</summary>
    public ScenePreview Preview { get; }
    public bool InSync { get; private set; } = true;
    public string? SyncError { get; private set; }

    public LogicGraphPage(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    public LogicGraphPage(EditorServices s, object? args = null)
    {
        _s = s;
        Name = "LogicGraphPage";
        Canvas = new GraphCanvas(this);
        Code.Undo = s.Undo;

        // ---- toolbar ----
        _tools = [new IconButton { Glyph = "cursor", Name = "Tool_Select" }, new IconButton { Glyph = "hand", Name = "Tool_Hand" }, new IconButton { Glyph = "link", Name = "Tool_Link" }];
        for (var i = 0; i < _tools.Length; i++) { var k = (GraphTool)i; _tools[i].Click += (_, _) => SetTool(k); }
        SetTool(GraphTool.Select);
        _syncChip.Click += (_, _) => ShowGeneratedCode(null);
        var chip = new Chip { Content = "GBA · Mode 0 · 240×160", Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: "VideoModeChip");
        Children.Add(PageChrome.Toolbar([PageChrome.ToolGroup(_tools), _syncChip], PageChrome.PlayGroup(s), [chip, PageChrome.UndoRedo(s)]));

        var ready = new StatusItem { Glyph = "check", IsDot = true, Content = "Ready", Name = "Status_Ready" };
        Children.Add(PageChrome.StatusBar([ready, _counts], [_compiled]));

        // ---- left: node palette ----
        var left = new DockPanel();
        PageChrome.DockTo(left, new PanelHeader { Title = "Nodes", Glyph = "nodes", Name = "NodesHeader" }, Dock.Top);
        PageChrome.DockTo(left, new Border { Padding = new Thickness(10, 8, 10, 6), Child = Search }, Dock.Top);
        left.Children.Add(new ScrollViewer { Content = _palette });
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RebuildPalette(); };
        // Nodes › Add node… opens Add Node (T-O08) for the middle of the view; what is typed goes to its search
        Search.AddHandler(PointerPressedEvent, (_, e) => { OpenAddNodeFromSearch(null); e.Handled = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Search.AddHandler(TextInputEvent, (_, e) => { OpenAddNodeFromSearch(e.Text); e.Handled = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Search.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Down)) return;
            OpenAddNodeFromSearch(null);
            e.Handled = true;
        };

        // ---- right: preview, inspector, generated C# ----
        Preview = new ScenePreview(s) { Width = 294, Height = 194 };
        var preview = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Height = 194, Margin = new Thickness(13, 13, 13, 12), Name = "Preview",
            Child = Preview };
        var codeBox = UI.Frame(Code, "bg/base", radius: 6);
        codeBox.ClipToBounds = true;
        codeBox.Height = 198;
        codeBox.Margin = new Thickness(13, 0, 13, 10);
        var codeSection = UI.Column(0, new Border { Padding = new Thickness(13, 0), Child = _codeHeader }, codeBox, PageChrome.Divider());
        _codeHeader.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) codeBox.IsVisible = _codeHeader.IsOpen; };
        var right = new DockPanel();
        PageChrome.DockTo(right, new PanelHeader { Title = "Preview", Glyph = "pad", Name = "PreviewHeader" }, Dock.Top);
        PageChrome.DockTo(right, preview, Dock.Top);
        PageChrome.DockTo(right, PageChrome.Divider(), Dock.Top);
        right.Children.Add(new ScrollViewer { Content = UI.Column(0, _inspector, codeSection) });

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("248,*,320"), Name = "Body" };
        body.Children.Add(PageChrome.SidePanel(left, true, "LeftPanel"));
        // Doc Tabs over the canvas (the graph is the active document): back to the scene in one click
        var center = new DockPanel { Name = "CenterPanel" };
        PageChrome.DockTo(center, Tilemap.PageChrome.DocTabs(s), Dock.Top);
        center.Children.Add(Canvas);
        Grid.SetColumn(center, 1);
        body.Children.Add(center);
        var rightPanel = PageChrome.SidePanel(right, false, "RightPanel");
        Grid.SetColumn(rightPanel, 2);
        body.Children.Add(rightPanel);
        Children.Add(body);

        Code.Token(BackgroundProperty, "bg/base");
        Code.TextChanged += CodeEdited;
        Canvas.SelectionChanged += RebuildInspector;
        AttachedToVisualTree += (_, _) => { if (_s.Top is MainWindow w) w.Edit.DefaultTarget = Canvas; };
        DetachedFromVisualTree += (_, _) => { if (_s.Top is MainWindow w && w.Edit.DefaultTarget == Canvas) w.Edit.DefaultTarget = null; };

        Open(args);
        RebuildPalette();
    }

    // ---- documents -----------------------------------------------------------------------------------------

    void Open(object? args)
    {
        var p = _s.Project.Current;
        string? path = args switch
        {
            string str when str.EndsWith(".graph", StringComparison.OrdinalIgnoreCase) => str,
            EditorDocument d => GraphSession.Of(_s).Open.FirstOrDefault(x => x.Value.Tab == d).Key ?? d.Path,
            _ => null,
        };
        path ??= GraphSession.Of(_s).Open.Keys.FirstOrDefault() ?? p?.GraphFiles().FirstOrDefault();
        if (path is null && p != null) path = NewGraphPath(p.ScriptsDir, "PlayerLogic");
        if (path is null) { Document = GraphDocument.New("", "Logic"); _tab = new EditorDocument { Name = "Logic", Glyph = "nodes" }; Canvas.Rebuild(); Refresh(); return; }
        path = Path.GetFullPath(path);
        var session = GraphSession.Of(_s);
        if (!session.Open.TryGetValue(path, out var entry))
        {
            var doc = File.Exists(path) ? GraphDocument.Load(path) : GraphDocument.New(path, Path.GetFileNameWithoutExtension(path));
            var tab = new EditorDocument { Name = doc.DisplayName, Glyph = "nodes" };
            tab.SaveHandler = _ => { doc.Save(); return Task.CompletedTask; };
            if (!File.Exists(path)) doc.Save();
            _s.Documents.Add(tab);
            session.Open[path] = entry = (doc, tab);
        }
        Document = entry.Doc;
        _tab = entry.Tab;
        _s.Documents.Active = _tab;
        Canvas.Rebuild();
        Dispatcher.UIThread.Post(() => { if (Canvas.Bounds.Width > 0 && Document.AllNodes.Any() && !IsInView()) Canvas.Fit(); });
        Refresh();
    }

    bool IsInView() => Document.AllNodes.All(n => Canvas.ToScreen(new Point(n.X, n.Y)).X < Canvas.Bounds.Width && Canvas.ToScreen(new Point(n.X, n.Y)).Y < Canvas.Bounds.Height);

    /// <summary>A free path scripts/&lt;Name&gt;.graph (Name, Name2, …).</summary>
    public static string NewGraphPath(string dir, string name)
    {
        var path = Path.Combine(dir, name + ".graph");
        for (var i = 2; File.Exists(path) || File.Exists(Path.ChangeExtension(path, ".cs")); i++) path = Path.Combine(dir, $"{name}{i}.graph");
        return path;
    }

    // ---- editing (undoable) ----------------------------------------------------------------------------------

    /// <summary>Applies a change to the graph as one undo step; <paramref name="change"/> returns false when nothing changed.</summary>
    public bool Edit(string name, Func<bool> change)
    {
        var before = Document.ToJson();
        if (!change()) return false;
        Record(name, before);
        return true;
    }

    /// <summary>Records a change that was already applied (dragging nodes).</summary>
    public void Record(string name, string beforeJson)
    {
        var doc = Document;
        var after = doc.ToJson();
        if (after == beforeJson) return;
        _s.Undo.Push(new DelegateAction(name, () => Restore(doc, beforeJson), () => Restore(doc, after)));
        Changed();
    }

    void Restore(GraphDocument doc, string json)
    {
        doc.FromJson(json);
        if (doc == Document) { Canvas.Rebuild(); Changed(); }
    }

    void Changed()
    {
        _tab.IsDirty = true;
        Canvas.Rebuild();
        Refresh();
    }

    void Refresh()
    {
        _counts.Content = $"{Document.AllNodes.Count()} nodes · {Document.AllLinks.Count()} wires";
        if (!_codeFromGraph) UpdateCode();
        RebuildInspector();
    }

    void UpdateCode()
    {
        string code;
        try
        {
            code = GraphDocument.CompactView(Document.GenerateCSharp());
            _compiled.Content = "Compiled to C#";
            _compiled.Glyph = "check";
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or KeyNotFoundException)
        {
            // the code pane says why and what to do; the status bar points to it
            code = "// No C# could be made from this graph:\n// " + e.Message.Replace("\n", "\n// ", StringComparison.Ordinal)
                   + "\n// Fix that node setting or wire in the graph; this code updates as you edit.";
            _compiled.Content = "C# generation failed · see Generated C#";
            _compiled.Glyph = "warn";
        }
        _codeFromGraph = true;
        Code.Text = code;
        Code.Spans = ScriptAnalysis.Colors(code);
        _codeFromGraph = false;
        SetSync(true, null);
    }

    void SetSync(bool ok, string? error)
    {
        InSync = ok;
        SyncError = error;
        _codeHeader.Meta = ok ? "in sync" : "not in sync";
        _syncChip.Content = ok ? "Graph ⇄ C# in sync" : "Graph ⇄ C# not in sync";
        _syncChip.Style = ok ? ChipStyle.Success : ChipStyle.Warning;
        _syncChip.Glyph = ok ? "check" : "warn";
        if (error != null) ToolTip.SetTip(_syncChip, new TipContent(error));
    }

    void CodeEdited()
    {
        if (_codeFromGraph) return;
        _codeTimer?.Dispose();
        _codeTimer = DispatcherTimer.RunOnce(() => ApplyCode(), TimeSpan.FromMilliseconds(500));
    }

    /// <summary>C# -> Graph (D-11): rebuilds the graph from the edited Generated C# panel, keeping ids and positions.</summary>
    public bool ApplyCode()
    {
        _codeTimer?.Dispose();
        var text = Code.Text;
        Scripting.Graph.LogicGraph parsed;
        try { parsed = GraphFromCode.Parse(Document.FromCompactView(text), Document.Graph); }
        catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException or NullReferenceException)
        {
            SetSync(false, e.Message);
            return false;
        }
        parsed.Class = Document.Graph.Class;
        var doc = Document;
        var before = doc.ToJson();
        var oldJson = doc.Graph.ToJson();
        if (parsed.ToJson() == oldJson) { SetSync(true, null); return true; }
        _codeFromGraph = true;
        doc.FromJson(MergeExtras(parsed, doc));
        Record("Edit generated C#", before);
        _codeFromGraph = false;
        Code.Spans = ScriptAnalysis.Colors(text);
        SetSync(true, null);
        return true;
    }

    static string MergeExtras(Scripting.Graph.LogicGraph g, GraphDocument doc)
    {
        var copy = new GraphDocument { Path = doc.Path };
        copy.FromJson(g.ToJson());
        copy.Comments.AddRange(doc.Comments);
        foreach (var c in doc.Collapsed.Where(id => g.Node(id) != null)) copy.Collapsed.Add(c);
        return copy.ToJson();
    }

    // ---- palette ---------------------------------------------------------------------------------------------

    IEnumerable<NodeDef> PaletteMatches()
    {
        var q = Search.Text?.Trim() ?? "";
        return q.Length == 0 ? NodeCatalog.All : NodeCatalog.All.Select(n => (n, s: PaletteSearch.Score(n.Title, q))).Where(x => x.s >= 0).OrderByDescending(x => x.s).Select(x => x.n);
    }

    void RebuildPalette()
    {
        _palette.Children.Clear();
        var searching = (Search.Text?.Trim() ?? "").Length > 0;
        var matches = PaletteMatches().ToList();
        foreach (var cat in GraphDocument.PaletteOrder)
        {
            var nodes = matches.Where(n => n.Category == cat).ToList();
            if (nodes.Count == 0) continue;
            var open = searching || _openCategories.Contains(cat);
            var header = new TreeItem { Label = cat, Glyph = GraphDocument.CategoryGlyph(cat), Toggle = open ? TreeToggle.Open : TreeToggle.Closed, Name = "Category_" + cat.Split(' ')[0] };
            // category colours are Figma literals (the same in both themes)
            if (Application.Current?.FindResource(GraphDocument.CategoryColor(cat)) is Color col) header.Foreground = new SolidColorBrush(col);
            var c = cat;
            header.Activated += (_, _) => { if (!_openCategories.Remove(c)) _openCategories.Add(c); RebuildPalette(); };
            _palette.Children.Add(header);
            if (!open) continue;
            foreach (var n in nodes)
            {
                var item = new TreeItem { Label = n.Title, Glyph = "dots", Depth = 1, IsSelected = _paletteSelection == n.Type, Name = "NodeType_" + n.Type };
                item.Padding = new Thickness(30, 0, 8, 0);
                var type = n.Type;
                item.Activated += (_, e) =>
                {
                    _paletteSelection = type;
                    if (e.ClickCount >= 2) AddNode(type, CenterOfView(), null);
                    RebuildPalette();
                };
                _palette.Children.Add(item);
            }
        }
    }

    /// <summary>Highlights a node type in the palette (the last one picked there).</summary>
    public void HighlightPalette(string type)
    {
        _paletteSelection = type;
        RebuildPalette();
    }

    /// <summary>Labels in the node palette (tests).</summary>
    public IReadOnlyList<string> PaletteLabels => _palette.Children.OfType<TreeItem>().Select(t => t.Label ?? "").ToList();

    Point CenterOfView() => Canvas.ToWorld(new Point(Math.Max(200, Canvas.Bounds.Width / 2 - 90), Math.Max(150, Canvas.Bounds.Height / 2 - 40)));

    /// <summary>Adds a node at a position; with <paramref name="from"/> also connects that output to the new node.</summary>
    public string AddNode(string type, Point at, (string Node, string Pin)? from)
    {
        var def = NodeCatalog.Find(type) ?? throw new ArgumentException("Unknown node " + type);
        var id = "";
        Edit("Add " + def.Title, () =>
        {
            id = Document.NextId();
            Document.Graph.Nodes.Add(new GraphNode { Id = id, Type = type, X = Math.Round(at.X), Y = Math.Round(at.Y) });
            if (from is { } f && Document.Node(f.Node) is { } src && NodeCatalog.Find(src.Type)?.Pin(f.Pin) is { } outPin)
            {
                var input = def.Pins.FirstOrDefault(p => !p.Output && p.Kind == outPin.Kind);
                if (input != null) Document.Connect(f.Node, f.Pin, id, input.Name);
            }
            return true;
        });
        Canvas.Select([id]);
        return id;
    }

    /// <summary>The output to connect from with Shift+Enter when Add Node was not opened from a pin: the selected node's exec out or first output.</summary>
    public (string Node, string Pin)? DefaultConnectFrom()
    {
        if (Canvas.Selection.Count != 1 || Document.Node(Canvas.Selection.First()) is not { } n || NodeCatalog.Find(n.Type) is not { } def) return null;
        var pin = def.Pins.FirstOrDefault(p => p.Output && p.Kind == PinKind.Exec) ?? def.Pins.FirstOrDefault(p => p.Output);
        return pin is null ? null : (n.Id, pin.Name);
    }

    public void OpenAddNode(Point world, (string Node, string Pin)? from) =>
        _ = _s.Windows.Open("T-O08", new AddNodeRequest(this, world, from), source: "GraphCanvas");

    /// <summary>Nodes › Add node…: Add Node for the middle of the view, its search starting with <paramref name="text"/>.</summary>
    public void OpenAddNodeFromSearch(string? text)
    {
        if (_s.Windows.Host?.Overlays.IsOpen == true) return;
        _ = _s.Windows.Open("T-O08", new AddNodeRequest(this, CenterOfView(), null, text), Search, source: "NodesAddNode");
    }

    // ---- inspector -------------------------------------------------------------------------------------------

    void RebuildInspector()
    {
        _inspector.Children.Clear();
        if (Canvas.Selection.Count != 1 || Document.Node(Canvas.Selection.First()) is not { } n || NodeCatalog.Find(n.Type) is not { } def) return;
        var header = new SectionHeader { Title = def.Title, Glyph = GraphDocument.CategoryGlyph(def.Category), Meta = def.Category, Height = 38, Name = "InspectorHeader" };
        var desc = UI.Text(GraphDocument.Description(def.Type), "Label/11", "text/secondary");
        desc.TextWrapping = TextWrapping.Wrap;
        var rows = new List<Control> { desc };
        foreach (var prop in def.Props)
        {
            var connected = Document.AllLinks.Any(l => l.ToNode == n.Id && l.ToPin == prop.Name);
            var value = n.Props.TryGetValue(prop.Name, out var v) ? v : prop.Default;
            var name = prop.Name;
            if (prop.Kind == PropKind.Choice && prop.Options is { Count: > 0 } options)
            {
                // a segmented control, as Figma draws Set Velocity Y "Mode  Set | Add | Clamp"
                var label = UI.Text(GraphDocument.Label(name), "Body/12", "text/secondary");
                label.Width = 86;
                label.VerticalAlignment = VerticalAlignment.Center;
                var segments = Settings.SettingsUi.Segments(options, Math.Max(0, options.ToList().IndexOf(value)), i => SetProp(n.Id, name, options[i]), "PropSegments_" + name);
                var choice = new DockPanel { HorizontalSpacing = 8, Name = "PropRow_" + name };
                DockPanel.SetDock(label, Dock.Left);
                choice.Children.AddRange([label, segments]);
                rows.Add(choice);
                continue;
            }
            var field = new InspectorField { Label = GraphDocument.Label(prop.Name), Value = connected ? "(wired)" : GraphReferences.Display(prop, value), Name = "Prop_" + prop.Name, IsEnabled = !connected };
            switch (prop.Kind)
            {
                case var kind when GraphReferences.IsReference(kind):
                    field.Kind = FieldKind.Dropdown;
                    field.TemplateApplied += (_, e) =>
                    {
                        if (e.NameScope.Find<Button>("PART_Dropdown") is not { } button) return;
                        button.Click += (_, _) => Database.Dropdown.Show(button, References().Options(prop, value), field.Value, option =>
                        {
                            if (GraphReferences.IsNewRow(option)) { _creating = (n.Id, name); _createProblem = null; RebuildInspector(); }
                            else if (GraphReferences.Value(option) is { } chosen) SetProp(n.Id, name, chosen);
                        });
                    };
                    if (_creating == (n.Id, name))
                    {
                        rows.Add(field);
                        rows.AddRange(NewReferenceRows(n.Id, name));
                        continue;
                    }
                    if (!connected && References().IsMissing(prop, value))
                    {
                        rows.Add(field);
                        var missing = UI.Text(GraphReferences.MissingText(prop, value), "Label/11", "status/danger");
                        missing.TextWrapping = TextWrapping.Wrap;
                        missing.Name = "Missing_" + name;
                        rows.Add(missing);
                        continue;
                    }
                    break;
                case PropKind.Key:
                    field.Kind = FieldKind.Dropdown;
                    field.Choices(() => ["A", "B", "Select", "Start", "Right", "Left", "Up", "Down", "R", "L"], x => SetProp(n.Id, name, x));
                    break;
                case PropKind.Compare:
                    field.Kind = FieldKind.Dropdown;
                    field.Choices(() => [">", ">=", "<", "<=", "==", "!="], x => SetProp(n.Id, name, x));
                    break;
                case PropKind.Bool:
                    field.Kind = FieldKind.Toggle;
                    field.IsOn = value == "true";
                    field.PropertyChanged += (_, e) => { if (e.Property == InspectorField.IsOnProperty) SetProp(n.Id, name, field.IsOn ? "true" : "false"); };
                    break;
                default:
                    field.Kind = FieldKind.Text;
                    field.TemplateApplied += (_, e) =>
                    {
                        if (e.NameScope.Find<TextBox>("PART_Text") is not { } box) return;
                        box.LostFocus += (_, _) => SetProp(n.Id, name, field.Value ?? "");
                        box.KeyDown += (_, ke) => { if (ke.Key == Key.Enter) { SetProp(n.Id, name, field.Value ?? ""); ke.Handled = true; } };
                    };
                    break;
            }
            rows.Add(field);
        }
        // actor inputs (Target): Self, or the actor output wired to it; choosing one rewires the input. Figma T-P17 lists
        // Target first, then the values (Value, then the Mode segments)
        var targetAt = 1;
        foreach (var pin in def.Pins.Where(p => !p.Output && p.Kind == PinKind.Actor && def.Props.All(x => x.Name != p.Name)))
        {
            var input = pin.Name;
            var sources = ActorSources(n.Id);
            var wired = Document.AllLinks.FirstOrDefault(l => l.ToNode == n.Id && l.ToPin == input);
            var current = wired is null ? "Self" : sources.FirstOrDefault(s => s.Node == wired.FromNode && s.Pin == wired.FromPin).Text ?? "(wired)";
            var field = new InspectorField { Label = GraphDocument.Label(input), Kind = FieldKind.Dropdown, Value = current, Name = "Pin_" + input };
            field.Choices(() => ["Self", .. ActorSources(n.Id).Select(s => s.Text)], x => SetTarget(n.Id, input, x));
            rows.Insert(targetAt++, field);
        }
        var content = UI.Column(8, [.. rows]);
        content.Margin = new Thickness(0, 0, 0, 10);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        var section = UI.Column(0, header, content);
        section.Margin = new Thickness(13, 0);
        _inspector.Children.Add(section);
        _inspector.Children.Add(PageChrome.Divider());
    }

    // ---- a new Save data field or tag, named under the dropdown (P17.04) -------------------------------

    (string Node, string Prop)? _creating;
    string? _createProblem;

    IEnumerable<Control> NewReferenceRows(string node, string prop)
    {
        var box = new TextBox { Name = "NewReference", Height = 28, Padding = new Thickness(10, 0), Watermark = "Name, then Enter" };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { _creating = null; _createProblem = null; RebuildInspector(); e.Handled = true; }
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            CreateReference(node, prop, box.Text ?? "");
        };
        box.AttachedToVisualTree += (_, _) => box.Focus();
        yield return box;
        if (_createProblem is null) yield break;
        var problem = UI.Text(_createProblem, "Label/11", "status/danger");
        problem.TextWrapping = TextWrapping.Wrap;
        problem.Name = "NewReferenceProblem";
        yield return problem;
    }

    /// <summary>
    /// Adds a Save data field (int) or project tag and sets the node to it. False when the name is refused; the problem
    /// shows under the box. The project file is saved at once; undo takes back the node's value, not the new name.
    /// </summary>
    public bool CreateReference(string node, string prop, string name)
    {
        if (_s.Project.Current is not { } project || Document.Node(node) is not { } n || NodeCatalog.Find(n.Type)?.Props.FirstOrDefault(p => p.Name == prop) is not { } def) return false;
        name = name.Trim();
        var isTag = def.Kind == PropKind.Tag;
        var problem = GraphReferences.NameProblem(isTag ? "tag" : "field", name);
        var taken = isTag ? project.Settings.Tags : project.Settings.Save.Fields.Select(f => f.Name.Trim());
        if (problem is null && taken.Contains(name, StringComparer.OrdinalIgnoreCase)) problem = $"{name} already exists. Choose it from the list.";
        var field = new AgbStudio.Core.Project.SaveField { Name = name, Type = "int" };
        if (problem is null && !isTag)
        {
            // the Save data page's own check: for example, whether the save chip still holds every slot
            var model = new Settings.ProjectSettingsModel(project.Settings, project);
            model.Draft.Save.Fields.Add(field);
            problem = model.Validate();
        }
        _createProblem = problem;
        if (problem is null)
        {
            if (isTag) project.Settings.Tags.Add(name);
            else project.Settings.Save.Fields.Add(field);
            _s.ProjectSettings.Save();
            _creating = null;
            SetProp(node, prop, name);
        }
        RebuildInspector();
        return problem is null;
    }

    /// <summary>A fresh look at what the project holds, for the dropdowns and for marking names it no longer has.</summary>
    public GraphReferences References() => new(_s.Project.Current);

    /// <summary>Changes a node property (inspector), checking numbers.</summary>
    public bool SetProp(string id, string prop, string value)
    {
        if (Document.Node(id) is not { } n || NodeCatalog.Find(n.Type)?.Props.FirstOrDefault(p => p.Name == prop) is not { } def) return false;
        value = value.Trim();
        if (def.Kind == PropKind.Number)
        {
            value = value.TrimStart('+');
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) { RebuildInspector(); return false; }
        }
        var current = n.Props.TryGetValue(prop, out var c) ? c : def.Default;
        if (current == value) return false;
        return Edit("Change " + GraphDocument.Label(prop), () => { n.Props[prop] = value; return true; });
    }

    /// <summary>The actor outputs of the other nodes that can feed an actor input ("On Touch · Other").</summary>
    public IReadOnlyList<(string Node, string Pin, string Text)> ActorSources(string exceptNode) =>
        Document.Graph.Nodes.Where(x => x.Id != exceptNode)
            .SelectMany(x => NodeCatalog.Find(x.Type) is { } d ? d.Pins.Where(p => p.Output && p.Kind == PinKind.Actor).Select(p => (x.Id, p.Name, $"{d.Title} · {GraphDocument.Label(p.Name)}")) : [])
            .ToList();

    /// <summary>Target of an actor input (inspector): "Self" removes its wire, an actor output ("On Touch · Other") wires it.</summary>
    public bool SetTarget(string id, string input, string choice)
    {
        var wired = Document.Graph.Links.FirstOrDefault(l => l.ToNode == id && l.ToPin == input);
        if (choice == "Self")
            return wired != null && Edit("Change " + GraphDocument.Label(input), () => Document.Graph.Links.Remove(wired));
        if (ActorSources(id).FirstOrDefault(s => s.Text == choice) is not { Node.Length: > 0 } src) return false;
        if (wired is { } w && w.FromNode == src.Node && w.FromPin == src.Pin) return false;
        return Edit("Change " + GraphDocument.Label(input), () => Document.Connect(src.Node, src.Pin, id, input));
    }

    // ---- tools and menu actions --------------------------------------------------------------------------------

    public void SetTool(GraphTool tool)
    {
        Canvas.Tool = tool;
        for (var i = 0; i < _tools.Length; i++) _tools[i].IsActive = i == (int)tool;
        Canvas.Cursor = tool == GraphTool.Hand ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    public void OpenNodeMenu(string nodeId)
    {
        if (Canvas.ViewOf(nodeId) is not { } anchor) return;
        _ = _s.Windows.Open("T-M21", nodeId, anchor, source: "GraphNode");
    }

    /// <summary>The Node Context Menu; Ctrl+G is handled by the canvas, so it is only shown on its item.</summary>
    [EditorMenu("T-M21")]
    public static IReadOnlyList<MenuEntry> NodeContextMenu() =>
    [
        new CommandEntry("edit.duplicate"), new CommandEntry("graph.disable"), new CommandEntry("graph.groupIntoComment", Keys: "Ctrl+G"), new CommandEntry("graph.collapse"),
        SeparatorEntry.Instance, new CommandEntry("graph.showCode", ShowIcon: true), new CommandEntry("edit.delete", Danger: true),
    ];

    /// <summary>Disable / enable the selected nodes (disabled nodes are kept in the file but not compiled).</summary>
    public void ToggleDisabled()
    {
        var ids = Canvas.Selection.ToList();
        if (ids.Count == 0) return;
        var disable = ids.Any(id => !Document.IsDisabled(id));
        Edit(disable ? "Disable node" : "Enable node", () => { foreach (var id in ids) Document.SetDisabled(id, disable); return true; });
        Canvas.Select(ids);
    }

    public void ToggleCollapsed()
    {
        var ids = Canvas.Selection.ToList();
        if (ids.Count == 0) return;
        var collapse = ids.Any(id => !Document.Collapsed.Contains(id));
        Edit(collapse ? "Collapse node" : "Expand node", () => { foreach (var id in ids) { if (collapse) Document.Collapsed.Add(id); else Document.Collapsed.Remove(id); } return true; });
        Canvas.Select(ids);
    }

    /// <summary>Frames the selected nodes in a comment.</summary>
    public GraphComment? GroupIntoComment()
    {
        var nodes = Document.AllNodes.Where(n => Canvas.Selection.Contains(n.Id)).ToList();
        if (nodes.Count == 0) return null;
        var r = nodes.Select(Document.Bounds).Aggregate((a, b) => a.Union(b));
        var c = new GraphComment { Title = "Comment", X = r.X - 24, Y = r.Y - 40, W = r.Width + 48, H = r.Height + 64 };
        Edit("Group into comment", () => { Document.Comments.Add(c); return true; });
        return c;
    }

    /// <summary>Show generated C#: writes the .cs and opens it in the Script Editor at the event that runs the node.</summary>
    public void ShowGeneratedCode(string? nodeId)
    {
        if (Document.Path.Length == 0) return;
        Document.Save();
        _tab.IsDirty = false;
        var cs = Path.ChangeExtension(Document.Path, ".cs");
        var code = File.ReadAllText(cs).Replace("\r\n", "\n");
        var line = 1;
        if (nodeId != null && EventOf(nodeId) is { } ev)
        {
            var sig = ev switch { "OnStart" => "void Start()", "OnUpdate" => "void Update()", "OnLateUpdate" => "void LateUpdate()", "OnTouch" => "void OnTouch(", _ => "void OnDestroy()" };
            var at = code.IndexOf(sig, StringComparison.Ordinal);
            if (at >= 0) line = code.AsSpan(0, at).Count('\n') + 1;
        }
        _ = _s.Windows.Open("T-P04", new ScriptLocation(cs, line), source: "ShowGeneratedCode");
    }

    /// <summary>The event node whose chain reaches a node (walks wires back).</summary>
    public string? EventOf(string nodeId)
    {
        var seen = new HashSet<string>();
        var queue = new Queue<string>([nodeId]);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!seen.Add(id) || Document.Node(id) is not { } n) continue;
            if (NodeCatalog.Find(n.Type)?.Role == NodeRole.Event) return n.Type;
            foreach (var l in Document.AllLinks.Where(l => l.ToNode == id)) queue.Enqueue(l.FromNode);
            // value nodes feed nodes downstream
            foreach (var l in Document.AllLinks.Where(l => l.FromNode == id && NodeCatalog.Find(n.Type)?.Role == NodeRole.Value)) queue.Enqueue(l.ToNode);
        }
        return null;
    }

    static LogicGraphPage? Current(EditorServices s) => (s.Top as MainWindow)?.CurrentPage as LogicGraphPage;

    [EditorCommand("graph.disable", "Disable", Category = "Logic Graph", InPalette = false)]
    public static void DisableCommand(EditorServices s) => Current(s)?.ToggleDisabled();

    [EditorCommand("graph.groupIntoComment", "Group into comment", Category = "Logic Graph", InPalette = false)]
    public static void GroupCommand(EditorServices s) => Current(s)?.GroupIntoComment();

    [EditorCommand("graph.collapse", "Collapse", Category = "Logic Graph", InPalette = false)]
    public static void CollapseCommand(EditorServices s) => Current(s)?.ToggleCollapsed();

    [EditorCommand("graph.showCode", "Show generated C#", Category = "Logic Graph", Glyph = "code", InPalette = false)]
    public static void ShowCodeCommand(EditorServices s)
    {
        if (Current(s) is { } p) p.ShowGeneratedCode(p.Canvas.Selection.FirstOrDefault());
    }

    /// <summary>Create › Logic Graph: a new graph file in scripts/, opened in the Logic Graph.</summary>
    [CommandHandler("assets.create.logicGraph")]
    public static void CreateGraph(EditorServices s)
    {
        if (s.Project.Current is not { } p) { _ = s.Windows.Open("T-P17", source: "assets.create.logicGraph"); return; }
        var path = NewGraphPath(p.ScriptsDir, "NewLogic");
        GraphDocument.New(path, Path.GetFileNameWithoutExtension(path)).Save();
        _ = s.Windows.Open("T-P17", path, source: "assets.create.logicGraph");
    }
}
