using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.UiEditor;

/// <summary>
/// UI Editor (T-P27, 1440 x 900; from Tools › UI Editor or a .uipage file): the game's UI pages (ui/*.uipage). Left: the
/// pages, ready-made presets and the element tree. Middle: the 240 x 160 screen, drawn as the game draws it. Right: the
/// selected element's Rect Transform, layout, colours, text and control settings. Every change is undoable and saved to
/// the file at once; the next build has it.
/// </summary>
[EditorWindow("T-P27")]
public sealed class UiEditorPage : DockPanel
{
    public const string DocumentName = "UI Editor";
    const int TreeRowHeight = 26;

    readonly EditorServices _s;
    readonly UiPageLibrary _lib;
    readonly UiCanvas _canvas;
    readonly StackPanel _pages = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "PageList" };
    readonly StackPanel _tree = new() { Name = "ElementTree" };
    readonly StackPanel _inspector = new() { Name = "UiInspector" };
    readonly List<Action> _values = [];
    readonly StatusItem _problem = new() { Glyph = "warn", Name = "Status_TextFit", IsVisible = false };
    readonly StatusItem _colours = new() { Glyph = "sliders", Name = "Status_Colours" };
    readonly StatusItem _rect = new() { Glyph = "cursor", Name = "Status_Rect" };
    readonly Chip _language = new() { Glyph = "globe", Name = "UiLanguageChip" };
    readonly TextBlock _empty;
    readonly Dictionary<string, bool> _sectionOpen = [];
    UiPageRenderer? _renderer;
    IReadOnlyList<int>? _dragging;
    Point _dragStart;
    bool _dragMoved;

    public UiPageLibrary Library => _lib;
    public UiCanvas Canvas => _canvas;

    public UiEditorPage(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    public UiEditorPage(EditorServices s, object? args = null)
    {
        _s = s;
        _lib = UiPageLibrary.Of(s);
        _canvas = new UiCanvas(_lib);
        Name = "UiEditor";
        LastChildFill = true;

        var duplicate = Tool("cube", "Duplicate", "DuplicateElement", _lib.DuplicateSelected);
        var delete = Tool("x", "Delete", "DeleteElement", _lib.DeleteSelected);
        var up = Tool("down", "Move up", "MoveElementUp", () => _lib.MoveSelected(-1));
        var down = Tool("down", "Move down", "MoveElementDown", () => _lib.MoveSelected(1));
        up.RenderTransformOrigin = RelativePoint.Center;
        up.RenderTransform = new RotateTransform(180);
        _language.Click += (_, _) =>
        {
            if (_renderer is not { } r) return;
            PageChrome.Choose(_language, r.Languages, r.Language, i =>
            {
                r.SetLanguage(r.Languages[i]);
                Refresh();
            });
        };
        Children.Add(PageChrome.Toolbar(s, [PageChrome.ToolGroup([duplicate, delete, up, down]), _language], 0));
        _canvas.PlacedByLayout += () => _rect.Content = "A layout group places this element. Change its size in the Inspector, or turn on Ignore Layout.";

        Children.Add(PageChrome.StatusBar([PageChrome.ReadyItem(s.Status), _problem], [_rect, _colours]));
        _problem.Click += (_, _) =>
        {
            if (_renderer?.Problems.FirstOrDefault() is { } p) _lib.Select(p.Element);
        };

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,340"), Name = "Body" };
        body.Children.Add(LeftPanel());
        var center = new Grid { RowDefinitions = RowDefinitions.Parse("34,Auto,*"), Name = "Center" };
        Grid.SetColumn(center, 1);
        center.Children.Add(PageChrome.DocTabs(s, DocumentName, "layers"));
        _empty = UI.Text("This project has no UI pages yet. Add one with + or start from a preset.", "Body/12", "text/muted");
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        var toolbox = Toolbox();
        Grid.SetRow(toolbox, 1);
        center.Children.Add(toolbox);
        Grid.SetRow(_canvas, 2);
        Grid.SetRow(_empty, 2);
        center.Children.Add(_canvas);
        center.Children.Add(_empty);
        body.Children.Add(center);
        var header = new PanelHeader { Title = "Inspector", Glyph = "sliders", Name = "UiInspectorHeader" };
        var right = PageChrome.SidePanel(header, new ScrollViewer { Content = _inspector }, false, "UiInspectorPanel");
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        Children.Add(body);

        PageChrome.WhileAttached(this, () =>
        {
            _lib.Changed += OnChanged;
            _lib.Structure += OnStructure;
            _renderer = _lib.Project is { } p ? new UiPageRenderer(p, _renderer?.Language) : null;
            _canvas.Renderer = _renderer;
            _lib.OpenFirstIfNeeded();
            OnStructure();
        }, () =>
        {
            _lib.Changed -= OnChanged;
            _lib.Structure -= OnStructure;
        });
        if (args is string path && path.EndsWith(".uipage", StringComparison.OrdinalIgnoreCase)) _lib.Open(path);
        else if (args is EditorDocument { Path: { } docPath } && docPath.EndsWith(".uipage", StringComparison.OrdinalIgnoreCase)) _lib.Open(docPath);
    }

    // One click adds an element of that kind inside the selection.
    Control Toolbox()
    {
        var row = new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Name = "UiToolbox" };
        foreach (var kind in Enum.GetValues<UiElementKind>())
        {
            var chip = new Chip { Content = Title(kind), Glyph = GlyphOf(kind), Name = "Add_" + kind };
            chip.Click += (_, _) => _lib.AddElement(kind);
            row.Children.Add(chip);
        }
        return UI.Frame(row, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(8, 6));
    }

    static string Title(UiElementKind k) => k switch
    {
        UiElementKind.ScrollRect => "Scroll List",
        UiElementKind.InputField => "Input Field",
        _ => k.ToString(),
    };

    IconButton Tool(string glyph, string title, string name, Action run)
    {
        var b = new IconButton { Glyph = glyph, Name = name };
        ToolTip.SetTip(b, new TipContent(title));
        b.Click += (_, _) => run();
        return b;
    }

    // ---- left panel ---------------------------------------------------------------------------------

    Control LeftPanel()
    {
        var newPage = Tool("plus", "New page", "NewPage", _lib.NewPage);
        var preset = new Chip { Content = "Preset", Glyph = "star", Name = "AddPreset" };
        preset.Click += (_, _) => PageChrome.Choose(preset, UiPresets.Names, null, i => _lib.AddPreset(UiPresets.Names[i]));
        var deletePage = Tool("x", "Delete page", "DeletePage", _lib.DeletePage);
        var row = UI.Row(4, newPage, preset, deletePage);
        row.Margin = new Thickness(8, 6);
        var pagesHeader = new PanelHeader { Title = "Pages", Glyph = "layers", Name = "PagesHeader" };
        var elementsHeader = new PanelHeader { Title = "Elements", Glyph = "cube", Name = "ElementsHeader" };
        var top = new DockPanel();
        DockPanel.SetDock(row, Dock.Top);
        top.Children.Add(row);
        top.Children.Add(new ScrollViewer { Content = _pages, MaxHeight = 132 });
        var dock = new DockPanel();
        var upper = UI.Column(0, pagesHeader, top, elementsHeader);
        DockPanel.SetDock(upper, Dock.Top);
        dock.Children.Add(upper);
        var scroll = new ScrollViewer { Content = _tree };
        dock.Children.Add(scroll);
        _tree.AddHandler(PointerPressedEvent, TreePressed, RoutingStrategies.Tunnel);
        _tree.AddHandler(PointerMovedEvent, TreeMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        _tree.AddHandler(PointerReleasedEvent, TreeReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        var frame = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("UiLeftPanel");
        frame.ClipToBounds = true;
        return frame;
    }

    static string GlyphOf(UiElementKind k) => k switch
    {
        UiElementKind.Panel => "square",
        UiElementKind.Image => "image",
        UiElementKind.Text => "type",
        UiElementKind.Button => "cursor",
        UiElementKind.Toggle => "check",
        UiElementKind.Slider => "sliders",
        UiElementKind.ScrollRect => "layers",
        UiElementKind.Scrollbar => "line",
        UiElementKind.Dropdown => "down",
        UiElementKind.InputField => "pencil",
        _ => "cube",
    };

    void FillPages()
    {
        _pages.Children.Clear();
        foreach (var file in _lib.Files)
        {
            var name = GameProject.AssetName(file);
            var row = new ListRow { Label = name, Glyph = "file", IsSelected = name == _lib.PageName, Name = "Page_" + name };
            row.Activated += (_, _) => _lib.Open(file);
            _pages.Children.Add(row);
        }
    }

    void FillTree()
    {
        _tree.Children.Clear();
        if (_lib.Page is not { } page) return;
        var selected = _lib.Selected;
        void Add(UiElement e, int depth, List<int> path)
        {
            var at = path.ToList();
            var row = new TreeItem
            {
                Label = e.Name, Glyph = GlyphOf(e.Kind), Meta = e.Hidden ? "hidden" : e.Kind.ToString(), Depth = depth,
                IsSelected = ReferenceEquals(e, selected), Height = TreeRowHeight, Tag = at, Name = "Element_" + e.Name,
            };
            row.Activated += (_, _) => _lib.Select(at);
            _tree.Children.Add(row);
            for (var i = 0; i < e.Children.Count; i++) Add(e.Children[i], depth + 1, [.. path, i]);
        }
        Add(page.Root, 0, []);
    }

    void TreePressed(object? sender, PointerPressedEventArgs e)
    {
        if (RowAt(e.GetPosition(_tree)) is not { Tag: List<int> path }) return;
        _dragging = path.Count == 0 ? null : path;
        _dragStart = e.GetPosition(_tree);
        _dragMoved = false;
    }

    void TreeMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging != null && Math.Abs(e.GetPosition(_tree).Y - _dragStart.Y) > 6) _dragMoved = true;
    }

    void TreeReleased(object? sender, PointerReleasedEventArgs e)
    {
        var from = _dragging;
        _dragging = null;
        if (from is null || !_dragMoved) return;
        DropOn(from, e.GetPosition(_tree).Y);
    }

    Control? RowAt(Point p) => _tree.Children.FirstOrDefault(c => c.Bounds.Contains(p));

    /// <summary>A drag in the tree ends over a row: its upper part puts the element before that row, the lower part after
    /// it, and the middle of a Panel or Scroll Rect inside it.</summary>
    public void DropOn(IReadOnlyList<int> from, double y)
    {
        if (RowAt(new Point(4, Math.Clamp(y, 0, Math.Max(0, _tree.Bounds.Height - 1)))) is not { Tag: List<int> target } row) return;
        var part = (y - row.Bounds.Y) / Math.Max(1, row.Bounds.Height);
        if (target.Count == 0 || (part is > 0.3 and < 0.7 && ElementAt(target)?.Kind is UiElementKind.Panel or UiElementKind.ScrollRect))
        {
            _lib.Reparent(from, target, ElementAt(target)?.Children.Count ?? 0);
            return;
        }
        var parent = target.Take(target.Count - 1).ToList();
        _lib.Reparent(from, parent, target[^1] + (part >= 0.5 ? 1 : 0));
    }

    UiElement? ElementAt(IReadOnlyList<int> path)
    {
        var e = _lib.Page?.Root;
        foreach (var i in path)
        {
            if (e is null || i >= e.Children.Count) return null;
            e = e.Children[i];
        }
        return e;
    }

    // ---- refresh --------------------------------------------------------------------------------------

    void OnStructure()
    {
        FillPages();
        FillTree();
        BuildInspector();
        Refresh();
    }

    void OnChanged()
    {
        foreach (var v in _values) v();
        Refresh();
    }

    void Refresh()
    {
        var hasPage = _lib.Page != null;
        _empty.IsVisible = !hasPage;
        _canvas.IsVisible = hasPage;
        _canvas.Redraw();
        _language.Content = _renderer?.Language ?? "";
        _language.IsVisible = _renderer is { Languages.Count: > 1 };
        if (_renderer is { } r)
        {
            var used = r.ColoursInUse();
            _colours.Content = $"Colours {used} / 15";
            _colours.Classes.Set("warn", used > 15);
            var problems = r.Problems;
            _problem.IsVisible = problems.Count > 0;
            if (problems.FirstOrDefault() is { } p)
                _problem.Content = $"{p.Element.Name}: text {p.TextWidth} x {p.TextHeight} px in {p.Width} x {p.Height} px, so it is cut";
        }
        if (_lib.Page is { } page && _lib.Selected is { } e && UiLayoutEngine.Resolve(page).TryGetValue(e, out var rect))
            _rect.Content = $"{e.Name} · X {rect.X}  Y {rect.Y}  W {rect.W}  H {rect.H}";
        else _rect.Content = "";
    }

    // ---- inspector ------------------------------------------------------------------------------------

    // Sections that are rarely needed start closed, so what is used most stays near the top.
    static readonly HashSet<string> ClosedAtFirst = ["Page", "Anchors", "Layout Element", "Image"];

    void Section(string title, string glyph, params Control?[] rows)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph };
        _inspector.Children.Add(PageChrome.Section(header, rows.OfType<Control>(), name: "Section_" + title.Replace(" ", "")));
        header.IsOpen = _sectionOpen.GetValueOrDefault(title, !ClosedAtFirst.Contains(title));
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) _sectionOpen[title] = header.IsOpen; };
    }

    InspectorField TextRow(string label, Func<string> get, Action<string> set)
    {
        var f = PageChrome.Field(label, FieldKind.Text, get());
        PageChrome.OnCommit(f, () =>
        {
            var v = f.Value ?? "";
            if (v != get()) set(v);
            else f.Value = get();
        });
        _values.Add(() => { if (!f.IsKeyboardFocusWithin) f.Value = get(); });
        return f;
    }

    InspectorField IntRow(string label, Func<int> get, Action<int> set, int min = int.MinValue, int max = int.MaxValue) =>
        NumberCommit(TextRow(label, () => get().ToString(), _ => { }), get, set, min, max);

    InspectorField NumberCommit(InspectorField f, Func<int> get, Action<int> set, int min, int max)
    {
        PageChrome.OnCommit(f, () =>
        {
            if (int.TryParse((f.Value ?? "").Trim(), out var n))
            {
                n = Math.Clamp(n, min, max);
                if (n != get()) set(n);
            }
            f.Value = get().ToString();
        });
        return f;
    }

    InspectorField DoubleRow(string label, Func<double> get, Action<double> set)
    {
        var f = TextRow(label, () => get().ToString("0.##"), _ => { });
        PageChrome.OnCommit(f, () =>
        {
            if (double.TryParse((f.Value ?? "").Trim(), out var n) && Math.Abs(n - get()) > 0.0001) set(Math.Clamp(n, 0, 1));
            f.Value = get().ToString("0.##");
        });
        return f;
    }

    InspectorField BoolRow(string label, Func<bool> get, Action<bool> set)
    {
        var f = PageChrome.Field(label, FieldKind.Toggle);
        f.IsOn = get();
        f.PropertyChanged += (_, e) =>
        {
            if (e.Property == InspectorField.IsOnProperty && f.IsOn != get()) set(f.IsOn);
        };
        _values.Add(() => { if (f.IsOn != get()) f.IsOn = get(); });
        return f;
    }

    InspectorField ChoiceRow(string label, Func<string> get, Func<IReadOnlyList<string>> options, Action<string> set)
    {
        var f = PageChrome.Field(label, FieldKind.Dropdown, get());
        PageChrome.OnDropdown(f, () =>
        {
            var list = options();
            PageChrome.Choose(f, list, get(), i => set(list[i]));
        });
        _values.Add(() => f.Value = get());
        return f;
    }

    InspectorField EnumRow<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum =>
        ChoiceRow(label, () => get().ToString(), () => Enum.GetNames<T>(), v => set(Enum.Parse<T>(v)));

    // A colour: its hex (empty = none) and a list of the colours the project's pages already use.
    Control ColorRow(string label, Func<string> get, Action<string> set)
    {
        var f = TextRow(label, get, v =>
        {
            v = v.Trim();
            if (v.Length == 0 || UiPageRenderer.Colour(v) != null) set(v.Length == 0 || v.StartsWith('#') ? v.ToUpperInvariant() : "#" + v.ToUpperInvariant());
        });
        var swatch = new Button { Width = 22, Height = 22, Name = "Pick_" + label.Replace(" ", ""), Theme = PageChrome.Bare, VerticalAlignment = VerticalAlignment.Center };
        void Paint()
        {
            swatch.Background = UiPageRenderer.Colour(get()) is { } c ? new SolidColorBrush(c) : Brushes.Transparent;
        }
        Paint();
        _values.Add(Paint);
        swatch.Click += (_, _) =>
        {
            var list = new List<string> { "None" };
            list.AddRange(ProjectColours());
            PageChrome.Choose(swatch, list, get(), i => set(i == 0 ? "" : list[i]));
        };
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,6,22") };
        grid.Children.Add(f);
        Grid.SetColumn(swatch, 2);
        grid.Children.Add(swatch);
        return grid;
    }

    List<string> ProjectColours()
    {
        var used = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in _lib.Files)
            foreach (var e in GameProject.Load<UiPageFile>(file).Root.Walk())
                foreach (var c in new[] { e.Fill, e.Border, e.Accent, e.FocusFill, e.FocusBorder, e.TextColor, e.FocusTextColor, e.DisabledTextColor })
                    if (c.Length > 0) used.Add(c);
        if (_lib.Project is { } p)
            foreach (var c in new[] { p.Settings.Ui.Ink, p.Settings.Ui.Paper, p.Settings.Ui.Muted, p.Settings.Ui.BarFill, p.Settings.Ui.BarTrack }) used.Add(c);
        return used.ToList();
    }

    // Applies a named change to the selected element.
    Action<T> Set<T>(string name, Action<UiElement, T> apply, bool structure = false) => v => _lib.EditSelected(name, e => apply(e, v), structure);

    void BuildInspector()
    {
        _inspector.Children.Clear();
        _values.Clear();
        if (_lib.Page is null || _lib.Selected is not { } e) return;
        var isRoot = ReferenceEquals(e, _lib.Page.Root);
        var P = () => _lib.Page!;
        var E = () => _lib.Selected ?? e;

        Section("Element", GlyphOf(e.Kind),
            TextRow("Name", () => E().Name, Set<string>("Rename element", (x, v) => x.Name = v.Trim(), true)),
            EnumRow("Kind", () => E().Kind, Set<UiElementKind>("Element kind", (x, v) => x.Kind = v, true)),
            BoolRow("Hidden", () => E().Hidden, Set<bool>("Hidden", (x, v) => x.Hidden = v)),
            e.IsSelectable || e.Kind is UiElementKind.Panel or UiElementKind.Slider ? BoolRow("Interactable", () => E().Interactable, Set<bool>("Interactable", (x, v) => x.Interactable = v)) : null);

        var anchors = new[] { "Top Left", "Top", "Top Right", "Left", "Middle", "Right", "Bottom Left", "Bottom", "Bottom Right", "Stretch" };
        Section("Position and size", "move",
            IntRow("X", () => E().Rect.X, Set<int>("Rect", (x, v) => x.Rect.X = v)),
            IntRow("Y", () => E().Rect.Y, Set<int>("Rect", (x, v) => x.Rect.Y = v)),
            IntRow("W", () => E().Rect.W, Set<int>("Rect", (x, v) => x.Rect.W = v)),
            IntRow("H", () => E().Rect.H, Set<int>("Rect", (x, v) => x.Rect.H = v)),
            isRoot ? null : ChoiceRow("Anchor", () => AnchorName(E().Rect), () => anchors, ApplyAnchor));
        if (!isRoot)
            Section("Anchors", "link",
                DoubleRow("Anchor min X", () => E().Rect.AnchorMinX, Set<double>("Anchor", (x, v) => x.Rect.AnchorMinX = v)),
                DoubleRow("Anchor min Y", () => E().Rect.AnchorMinY, Set<double>("Anchor", (x, v) => x.Rect.AnchorMinY = v)),
                DoubleRow("Anchor max X", () => E().Rect.AnchorMaxX, Set<double>("Anchor", (x, v) => x.Rect.AnchorMaxX = v)),
                DoubleRow("Anchor max Y", () => E().Rect.AnchorMaxY, Set<double>("Anchor", (x, v) => x.Rect.AnchorMaxY = v)),
                DoubleRow("Pivot X", () => E().Rect.PivotX, Set<double>("Pivot", (x, v) => x.Rect.PivotX = v)),
                DoubleRow("Pivot Y", () => E().Rect.PivotY, Set<double>("Pivot", (x, v) => x.Rect.PivotY = v)));

        var groups = e.Kind is UiElementKind.Panel or UiElementKind.ScrollRect or UiElementKind.Button;
        UiLayoutGroup L(UiElement x) => x.Layout ??= new UiLayoutGroup();
        var layoutKind = e.Layout?.Kind ?? UiLayoutKind.None;
        if (groups)
        {
            var on = layoutKind != UiLayoutKind.None;
            var grid = layoutKind == UiLayoutKind.Grid;
            Section("Layout Group", "grid",
                EnumRow("Kind", () => E().Layout?.Kind ?? UiLayoutKind.None, Set<UiLayoutKind>("Layout group", (x, v) =>
                {
                    if (v == UiLayoutKind.None) x.Layout = null;
                    else L(x).Kind = v;
                }, true)),
                on ? IntRow("Padding left", () => L(E()).PaddingLeft, Set<int>("Padding", (x, v) => L(x).PaddingLeft = v), 0, 255) : null,
                on ? IntRow("Padding top", () => L(E()).PaddingTop, Set<int>("Padding", (x, v) => L(x).PaddingTop = v), 0, 255) : null,
                on ? IntRow("Padding right", () => L(E()).PaddingRight, Set<int>("Padding", (x, v) => L(x).PaddingRight = v), 0, 255) : null,
                on ? IntRow("Padding bottom", () => L(E()).PaddingBottom, Set<int>("Padding", (x, v) => L(x).PaddingBottom = v), 0, 255) : null,
                on ? IntRow("Spacing", () => L(E()).Spacing, Set<int>("Spacing", (x, v) => L(x).Spacing = v), 0, 255) : null,
                grid ? IntRow("Spacing Y", () => L(E()).SpacingY, Set<int>("Spacing", (x, v) => L(x).SpacingY = v), 0, 255) : null,
                grid ? IntRow("Cell W", () => L(E()).CellW, Set<int>("Cell", (x, v) => L(x).CellW = v), 1, 240) : null,
                grid ? IntRow("Cell H", () => L(E()).CellH, Set<int>("Cell", (x, v) => L(x).CellH = v), 1, 160) : null,
                grid ? IntRow("Columns", () => L(E()).Columns, Set<int>("Columns", (x, v) => L(x).Columns = v), 0, 30) : null,
                on ? EnumRow("Align X", () => L(E()).AlignX, Set<UiAlign>("Align", (x, v) => L(x).AlignX = v)) : null,
                on ? EnumRow("Align Y", () => L(E()).AlignY, Set<UiAlign>("Align", (x, v) => L(x).AlignY = v)) : null,
                on && !grid ? BoolRow("Stretch across", () => L(E()).StretchCross, Set<bool>("Stretch", (x, v) => L(x).StretchCross = v)) : null,
                on ? BoolRow("Fit width", () => L(E()).FitW, Set<bool>("Fit", (x, v) => L(x).FitW = v)) : null,
                on ? BoolRow("Fit height", () => L(E()).FitH, Set<bool>("Fit", (x, v) => L(x).FitH = v)) : null);
        }
        if (!isRoot)
            Section("Layout Element", "line",
                BoolRow("Ignore layout", () => E().IgnoreLayout, Set<bool>("Ignore layout", (x, v) => x.IgnoreLayout = v)),
                BoolRow("Flexible", () => E().Flexible, Set<bool>("Flexible", (x, v) => x.Flexible = v)),
                DoubleAspect());

        Section("Colours", "sliders",
            ColorRow("Fill", () => E().Fill, Set<string>("Fill", (x, v) => x.Fill = v)),
            ColorRow("Border", () => E().Border, Set<string>("Border", (x, v) => x.Border = v)),
            IntRow("Border width", () => E().BorderWidth, Set<int>("Border width", (x, v) => x.BorderWidth = v), 0, 8),
            e.Kind is UiElementKind.Slider or UiElementKind.Toggle or UiElementKind.Button or UiElementKind.Scrollbar ? ColorRow("Accent", () => E().Accent, Set<string>("Accent", (x, v) => x.Accent = v)) : null,
            e.IsSelectable ? ColorRow("Focus fill", () => E().FocusFill, Set<string>("Focus fill", (x, v) => x.FocusFill = v)) : null,
            e.IsSelectable ? ColorRow("Focus border", () => E().FocusBorder, Set<string>("Focus border", (x, v) => x.FocusBorder = v)) : null);

        if (e.Kind is UiElementKind.Text or UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Dropdown or UiElementKind.InputField or UiElementKind.Panel)
        {
            var key = TextRow("Text key", () => E().Text, Set<string>("Text", (x, v) => x.Text = v));
            var pick = new AgbButton { Content = "Pick string", Name = "PickString" };
            pick.Click += (_, _) =>
            {
                var keys = _renderer?.StringKeys ?? [];
                PageChrome.Choose(pick, keys, E().Text, i => Set<string>("Text", (x, v) => x.Text = v)(keys[i]));
            };
            var shown = UI.Text(null, "Label/11", "text/muted");
            shown.TextWrapping = TextWrapping.Wrap;
            shown.Name = "ResolvedText";
            void Show() => shown.Text = E().Text.Length > 0 ? _renderer?.TextOf(E().Text) : "";
            Show();
            _values.Add(Show);
            Section("Text", "type", key, pick, shown,
                ColorRow("Colour", () => E().TextColor, Set<string>("Text colour", (x, v) => x.TextColor = v)),
                e.IsSelectable ? ColorRow("Focus colour", () => E().FocusTextColor, Set<string>("Focus colour", (x, v) => x.FocusTextColor = v)) : null,
                e.IsSelectable ? ColorRow("Disabled colour", () => E().DisabledTextColor, Set<string>("Disabled colour", (x, v) => x.DisabledTextColor = v)) : null,
                EnumRow("Align X", () => E().TextAlignX, Set<UiAlign>("Text align", (x, v) => x.TextAlignX = v)),
                EnumRow("Align Y", () => E().TextAlignY, Set<UiAlign>("Text align", (x, v) => x.TextAlignY = v)),
                IntRow("Padding left", () => E().PaddingLeft, Set<int>("Text padding", (x, v) => x.PaddingLeft = v), 0, 255),
                IntRow("Padding top", () => E().PaddingTop, Set<int>("Text padding", (x, v) => x.PaddingTop = v), 0, 255),
                IntRow("Padding right", () => E().PaddingRight, Set<int>("Text padding", (x, v) => x.PaddingRight = v), 0, 255),
                IntRow("Padding bottom", () => E().PaddingBottom, Set<int>("Text padding", (x, v) => x.PaddingBottom = v), 0, 255));
        }

        if (e.Kind is UiElementKind.Image or UiElementKind.Panel or UiElementKind.Button)
            Section("Image", "image",
                ChoiceRow("Image", () => E().Image, () => ["", .. _lib.Project?.ImageFiles().Select(GameProject.AssetName) ?? []], Set<string>("Image", (x, v) => x.Image = v)),
                EnumRow("Mode", () => E().ImageMode, Set<UiImageMode>("Image mode", (x, v) => x.ImageMode = v)),
                IntRow("Slice left", () => E().SliceLeft, Set<int>("Slice", (x, v) => x.SliceLeft = v), 0, 255),
                IntRow("Slice top", () => E().SliceTop, Set<int>("Slice", (x, v) => x.SliceTop = v), 0, 255),
                IntRow("Slice right", () => E().SliceRight, Set<int>("Slice", (x, v) => x.SliceRight = v), 0, 255),
                IntRow("Slice bottom", () => E().SliceBottom, Set<int>("Slice", (x, v) => x.SliceBottom = v), 0, 255),
                TextRow("Variants", () => string.Join(", ", E().ImageVariants), Set<string>("Image variants", (x, v) => x.ImageVariants = SplitList(v))));

        if (e.IsSelectable || e.Kind is UiElementKind.Slider or UiElementKind.Scrollbar)
        {
            string[] targets() => E().Action.Kind == UiActionKind.LoadScene
                ? _lib.Project?.SceneFiles().Select(GameProject.AssetName).ToArray() ?? []
                : _lib.Files.Select(GameProject.AssetName).ToArray();
            string[] names() => ["", .. P().Root.Walk().Where(x => x.IsSelectable).Select(x => x.Name)];
            Section("Control", "cursor",
                e.Kind == UiElementKind.Button ? BoolRow("Cursor", () => E().Cursor, Set<bool>("Cursor", (x, v) => x.Cursor = v)) : null,
                e.Kind == UiElementKind.Toggle ? BoolRow("On", () => E().On, Set<bool>("On", (x, v) => x.On = v)) : null,
                e.Kind == UiElementKind.Toggle ? TextRow("Toggle Group", () => E().Group, Set<string>("Toggle group", (x, v) => x.Group = v.Trim())) : null,
                e.Kind == UiElementKind.Slider ? IntRow("Amount", () => E().Amount, Set<int>("Amount", (x, v) => x.Amount = v), 0, 100) : null,
                e.Kind == UiElementKind.Dropdown ? TextRow("Options", () => string.Join(", ", E().Options), Set<string>("Options", (x, v) => x.Options = SplitList(v))) : null,
                e.Kind == UiElementKind.InputField ? IntRow("Max length", () => E().MaxLength, Set<int>("Max length", (x, v) => x.MaxLength = v), 1, 12) : null,
                e.Kind == UiElementKind.Scrollbar ? ChoiceRow("Scroll target", () => E().ScrollTarget, () => P().Root.Walk().Where(x => x.Kind == UiElementKind.ScrollRect).Select(x => x.Name).ToList(), Set<string>("Scroll target", (x, v) => x.ScrollTarget = v)) : null,
                e.IsSelectable ? EnumRow("Action", () => E().Action.Kind, Set<UiActionKind>("Action", (x, v) => x.Action.Kind = v, true)) : null,
                e.IsSelectable && e.Action.Kind is UiActionKind.OpenPage or UiActionKind.LoadScene ? ChoiceRow("Target", () => E().Action.Target, targets, Set<string>("Action target", (x, v) => x.Action.Target = v)) : null,
                e.IsSelectable ? IntRow("Data", () => E().Action.Data, Set<int>("Data", (x, v) => x.Action.Data = v), -32768, 32767) : null,
                e.IsSelectable ? ChoiceRow("Nav up", () => E().NavUp, names, Set<string>("Navigation", (x, v) => x.NavUp = v)) : null,
                e.IsSelectable ? ChoiceRow("Nav down", () => E().NavDown, names, Set<string>("Navigation", (x, v) => x.NavDown = v)) : null,
                e.IsSelectable ? ChoiceRow("Nav left", () => E().NavLeft, names, Set<string>("Navigation", (x, v) => x.NavLeft = v)) : null,
                e.IsSelectable ? ChoiceRow("Nav right", () => E().NavRight, names, Set<string>("Navigation", (x, v) => x.NavRight = v)) : null);
        }

        if (e.Kind == UiElementKind.InputField)
        {
            UiKeyboard K(UiElement x) => x.Keyboard ??= new UiKeyboard();
            Section("Keyboard", "pad",
                IntRow("X", () => K(E()).X, Set<int>("Keyboard", (x, v) => K(x).X = v), 0, 239),
                IntRow("Y", () => K(E()).Y, Set<int>("Keyboard", (x, v) => K(x).Y = v), 0, 159),
                IntRow("W", () => K(E()).W, Set<int>("Keyboard", (x, v) => K(x).W = v), 112, 240),
                IntRow("H", () => K(E()).H, Set<int>("Keyboard", (x, v) => K(x).H = v), 54, 160),
                ColorRow("Fill", () => K(E()).Fill, Set<string>("Keyboard fill", (x, v) => K(x).Fill = v)),
                ColorRow("Border", () => K(E()).Border, Set<string>("Keyboard border", (x, v) => K(x).Border = v)),
                ColorRow("Text", () => K(E()).TextColor, Set<string>("Keyboard text", (x, v) => K(x).TextColor = v)),
                ColorRow("Focus fill", () => K(E()).FocusFill, Set<string>("Keyboard focus", (x, v) => K(x).FocusFill = v)),
                ColorRow("Focus text", () => K(E()).FocusTextColor, Set<string>("Keyboard focus", (x, v) => K(x).FocusTextColor = v)));
        }

        Section("Page", "file",
            TextRow("Name", () => _lib.PageName, v => _lib.RenamePage(v)),
            BoolRow("B closes", () => P().Cancel, v => _lib.Edit("B closes", p => p.Cancel = v)),
            BoolRow("Wrap cursor", () => P().WrapFocus, v => _lib.Edit("Wrap cursor", p => p.WrapFocus = v)),
            ChoiceRow("First focus", () => P().FirstFocus, () => ["", .. P().Root.Walk().Where(x => x.IsSelectable).Select(x => x.Name)], v => _lib.Edit("First focus", p => p.FirstFocus = v)),
            BoolRow("Scene plays", () => P().ScenePlays, v => _lib.Edit("Scene plays", p => p.ScenePlays = v)),
            IntRow("Dim scene", () => P().Dim, v => _lib.Edit("Dim scene", p => p.Dim = v), 0, 16),
            ChoiceRow("Opens in scene", SceneOpening, () => ["", .. _lib.Project?.SceneFiles().Select(GameProject.AssetName) ?? []], SetSceneOpening));
    }

    // The scene that opens this page when it starts (a title screen).
    string SceneOpening()
    {
        if (_lib.Project is not { } p) return "";
        return p.SceneFiles().FirstOrDefault(f => GameProject.Load<SceneFile>(f).Page == _lib.PageName) is { } hit ? GameProject.AssetName(hit) : "";
    }

    void SetSceneOpening(string scene)
    {
        if (_lib.Project is not { } p) return;
        foreach (var f in p.SceneFiles())
        {
            var s = GameProject.Load<SceneFile>(f);
            var wanted = GameProject.AssetName(f) == scene;
            if (wanted && s.Page != _lib.PageName) s.Page = _lib.PageName;
            else if (!wanted && s.Page == _lib.PageName) s.Page = "";
            else continue;
            GameProject.Save(f, s);
        }
        OnChanged();
    }

    InspectorField DoubleAspect()
    {
        var f = TextRow("Aspect ratio", () => (_lib.Selected?.AspectRatio ?? 0).ToString("0.##"), _ => { });
        PageChrome.OnCommit(f, () =>
        {
            if (double.TryParse((f.Value ?? "").Trim(), out var n) && n >= 0 && Math.Abs(n - (_lib.Selected?.AspectRatio ?? 0)) > 0.0001)
                _lib.EditSelected("Aspect ratio", x => x.AspectRatio = n);
        });
        return f;
    }

    static List<string> SplitList(string text) => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    static string AnchorName(UiRect r)
    {
        if (r is { AnchorMinX: 0, AnchorMinY: 0, AnchorMaxX: 1, AnchorMaxY: 1 }) return "Stretch";
        if (r.AnchorMinX != r.AnchorMaxX || r.AnchorMinY != r.AnchorMaxY) return "Custom";
        var y = r.AnchorMinY switch { 0 => "Top", 1 => "Bottom", 0.5 => "", _ => "?" };
        var x = r.AnchorMinX switch { 0 => "Left", 1 => "Right", 0.5 => "", _ => "?" };
        if (x == "?" || y == "?") return "Custom";
        var name = (y + " " + x).Trim();
        return name.Length == 0 ? "Middle" : name;
    }

    // Changes the anchors and pivot and keeps the element where it is on the screen.
    void ApplyAnchor(string name)
    {
        if (_lib.Page is not { } page || _lib.Selected is not { } selected || _lib.ParentOf(selected) is not { } parent) return;
        var rects = UiLayoutEngine.Resolve(page);
        if (!rects.TryGetValue(selected, out var at) || !rects.TryGetValue(parent, out var within)) return;
        var stretch = name == "Stretch";
        var ax = name.Contains("Left") ? 0 : name.Contains("Right") ? 1 : 0.5;
        var ay = name.Contains("Top") ? 0 : name.Contains("Bottom") ? 1 : 0.5;
        _lib.EditSelected("Anchor", e =>
        {
            var r = e.Rect;
            r.AnchorMinX = stretch ? 0 : ax;
            r.AnchorMaxX = stretch ? 1 : ax;
            r.AnchorMinY = stretch ? 0 : ay;
            r.AnchorMaxY = stretch ? 1 : ay;
            r.PivotX = stretch ? 0 : ax;
            r.PivotY = stretch ? 0 : ay;
            var spanX = (int)Math.Round((r.AnchorMaxX - r.AnchorMinX) * within.W);
            var spanY = (int)Math.Round((r.AnchorMaxY - r.AnchorMinY) * within.H);
            r.W = at.W - spanX;
            r.H = at.H - spanY;
            var left = within.X + r.AnchorMinX * within.W;
            var top = within.Y + r.AnchorMinY * within.H;
            r.X = (int)Math.Round(at.X - left - spanX * r.PivotX + r.PivotX * at.W);
            r.Y = (int)Math.Round(at.Y - top - spanY * r.PivotY + r.PivotY * at.H);
        });
    }
}
