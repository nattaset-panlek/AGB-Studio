using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// The Scene Editor frame (T-P01) without its Phase 11 content: toolbar, docking layout (Hierarchy 260 px, document
/// tabs + viewport, bottom panel 250 px with Assets / Console / Animation / Database, Inspector 320 px) and status bar.
/// The panels are slots filled by registered windows: "T-P01:Hierarchy", "T-P01:Viewport", "T-P01:Inspector",
/// "T-P01:Assets", "T-P05" (Console) and "T-P06" (Animation). Splitters resize the docks.
/// </summary>
[EditorWindow("T-P01")]
public sealed class SceneWorkspace : DockPanel
{
    readonly EditorServices _s;
    readonly IconButton[] _tools;
    readonly StackPanel _docTabs = new() { Orientation = Orientation.Horizontal };
    readonly ContentControl _bottomContent = new();
    readonly PanelTab[] _panelTabs;

    public SceneWorkspace(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    /// <param name="args">What the page was opened for: a .scene path (Command Palette, World Map › Open) switches the
    /// viewport to that scene.</param>
    public SceneWorkspace(EditorServices s, object? args = null)
    {
        _s = s;
        Name = "SceneWorkspace";
        LastChildFill = true;

        // ---- toolbar (44) ----
        _tools = new[] { "cursor", "move", "pencil", "bucket", "eraser", "grid" }.Select(g => new IconButton { Glyph = g, Name = "Tool_" + g }).ToArray();
        for (var i = 0; i < 5; i++) { var k = i; _tools[i].Click += (_, _) => SetTool(k); }
        _tools[5].Click += (_, _) => s.Commands.Execute("scene.toggleGrid");
        SetTool(0);
        s.Commands.Get("scene.tool.select").Handle(() => SetTool(0));
        s.Commands.Get("scene.tool.move").Handle(() => SetTool(1));
        s.Commands.Get("scene.tool.paint").Handle(() => SetTool(2));
        s.Commands.Get("scene.toggleGrid").Handle(() => _tools[5].IsActive = !_tools[5].IsActive);

        var layers = new[] { "BG0", "BG1", "BG2", "BG3", "OBJ" }.Select(l => new LayerToggle { Content = l, IsOn = l != "BG3", Name = "Layer_" + l }).ToArray();
        var play = UI.Row(4,
            new PlayControl { Kind = PlayKind.Play, Command = s.Commands.Get("build.play"), Name = "PlayButton" },
            PlayButtons.Pause(s), PlayButtons.Step(s));
        // P10.18 tooltips: command title and its current shortcut
        Tip(play.Children[0], "build.play");
        string[] toolCommands = ["scene.tool.select", "scene.tool.move", "scene.tool.paint", "", "", "scene.toggleGrid"];
        for (var i = 0; i < _tools.Length; i++) if (toolCommands[i].Length > 0) Tip(_tools[i], toolCommands[i]);
        ToolTip.SetTip(_tools[3], new TipContent("Fill"));
        ToolTip.SetTip(_tools[4], new TipContent("Erase"));
        var chip = new Chip { Content = "GBA · Mode 0 · 240×160", Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: "VideoModeChip");
        var undo = new IconButton { Glyph = "undo", Command = s.Commands.Get("edit.undo"), Name = "UndoButton" };
        var redo = new IconButton { Glyph = "redo", Command = s.Commands.Get("edit.redo"), Name = "RedoButton" };
        Tip(undo, "edit.undo");
        Tip(redo, "edit.redo");

        var bar = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,8,Auto,8,*,8,Auto,8,*,8,Auto,8,Auto"), VerticalAlignment = VerticalAlignment.Center };
        void At(Control c, int col) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        At(ToolGroup(_tools), 0);
        At(ToolGroup(layers), 2);
        At(play, 6);
        At(chip, 10);
        At(ToolGroup([undo, redo]), 12);
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        SetDock(toolbar, Avalonia.Controls.Dock.Top);
        Children.Add(toolbar);

        // ---- status bar (26) ----
        var status = StatusBar(s.Status);
        SetDock(status, Avalonia.Controls.Dock.Bottom);
        Children.Add(status);

        // ---- body ----
        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        var hierarchyHeader = new PanelHeader { Title = "Hierarchy", Glyph = "layers", ActionGlyph = "plus", Name = "HierarchyHeader" };
        hierarchyHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-M11", anchor: hierarchyHeader, source: "HierarchyAdd"));
        var search = new SearchField { Watermark = "Search scene…", Name = "HierarchySearch" };
        var hierarchy = new DockPanel();
        DockAt(hierarchy, hierarchyHeader, Avalonia.Controls.Dock.Top);
        DockAt(hierarchy, new Border { Padding = new Thickness(10, 8, 10, 4), Child = search }, Avalonia.Controls.Dock.Top);
        hierarchy.Children.Add(Slot("T-P01:Hierarchy"));
        var left = UI.Frame(hierarchy, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("HierarchyPanel");
        left.ClipToBounds = true;
        body.Children.Add(left);

        var inspectorHeader = new PanelHeader { Title = "Inspector", Glyph = "sliders", ActionGlyph = "dots", Name = "InspectorHeader" };
        inspectorHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-M14", anchor: inspectorHeader, source: "InspectorMenu"));
        var inspector = new DockPanel();
        DockAt(inspector, inspectorHeader, Avalonia.Controls.Dock.Top);
        inspector.Children.Add(Slot("T-P01:Inspector"));
        var right = UI.Frame(inspector, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("InspectorPanel");
        right.ClipToBounds = true;
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        var center = new Grid { RowDefinitions = RowDefinitions.Parse("34,*,250"), Name = "Center" };
        Grid.SetColumn(center, 1);
        var tabs = UI.Frame(_docTabs, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs");
        _docTabs.VerticalAlignment = VerticalAlignment.Stretch;
        center.Children.Add(tabs);
        var viewport = UI.Frame(Slot("T-P01:Viewport", args), "bg/canvas").Named("Viewport");
        Grid.SetRow(viewport, 1);
        center.Children.Add(viewport);

        _panelTabs =
        [
            new PanelTab { Label = "Assets", Glyph = "folder", Name = "Tab_Assets", Tag = "T-P01:Assets" },
            new PanelTab { Label = "Console", Glyph = "code", Name = "Tab_Console", Tag = "T-P05" },
            new PanelTab { Label = "Animation", Glyph = "film", Name = "Tab_Animation", Tag = "T-P06" },
            new PanelTab { Label = "Database", Glyph = "db", Name = "Tab_Database", Tag = "T-P07" },
        ];
        foreach (var t in _panelTabs) t.Activated += (_, _) => SelectPanel(t);
        var panelTabRow = new Border { Height = 33, Padding = new Thickness(6, 0), Child = UI.Row(2, _panelTabs) }.Token(Border.BorderBrushProperty, "border/default");
        panelTabRow.BorderThickness = new Thickness(0, 0, 0, 1);
        var bottom = new DockPanel();
        DockAt(bottom, panelTabRow, Avalonia.Controls.Dock.Top);
        bottom.Children.Add(_bottomContent);
        var bottomPanel = UI.Frame(bottom, "bg/panel", "border/default", new Thickness(0, 1, 0, 0)).Named("BottomPanel");
        Grid.SetRow(bottomPanel, 2);
        center.Children.Add(bottomPanel);
        center.Children.Add(Splitter(row: 2));
        body.Children.Add(center);
        body.Children.Add(Splitter(col: 1, align: HorizontalAlignment.Left));
        body.Children.Add(Splitter(col: 1, align: HorizontalAlignment.Right));
        Children.Add(body);

        SelectPanel(_panelTabs[0]);
        s.Documents.Changed += RebuildTabs;
        RebuildTabs();
    }

    void Tip(Control c, string commandId)
    {
        var cmd = _s.Commands.Get(commandId);
        ToolTip.SetTip(c, new TipContent(cmd.Title, _s.Shortcuts.GestureOf(commandId) is { } g ? AgbStudio.Editor.Theme.Shortcuts.Format(g) : null));
    }

    public int ActiveTool => Array.FindIndex(_tools[..5], t => t.IsActive);

    /// <summary>The tool changed (index: Select, Move, Paint tiles, Fill, Erase); the viewport paints with the last three.</summary>
    public event Action<int>? ToolChanged;
    public IReadOnlyList<PanelTab> PanelTabs => _panelTabs;
    public PanelTab? SelectedPanel => _panelTabs.FirstOrDefault(t => t.IsSelected);

    /// <summary>The console badge (Figma shows 2 when there are build messages).</summary>
    public int? ConsoleCount { get => _panelTabs[1].Count; set => _panelTabs[1].Count = value; }

    void SetTool(int i)
    {
        for (var k = 0; k < 5; k++) _tools[k].IsActive = k == i;
        ToolChanged?.Invoke(i);
    }

    void SelectPanel(PanelTab tab)
    {
        var id = (string)tab.Tag!;
        if (id == "T-P07") { _ = _s.Windows.Open(id, source: "DatabaseTab"); return; }
        foreach (var t in _panelTabs) t.IsSelected = t == tab;
        _bottomContent.Content = _s.Windows.CreatePanel(id);
        if (id != "T-P01:Assets") _s.Windows.Requests.Add((id, tab.Name));
    }

    Control Slot(string id, object? args = null) => (Control?)_s.Windows.CreatePanel(id, args) ?? new Panel { Name = "Slot_" + id.Replace(':', '_').Replace('-', '_') };

    static Border ToolGroup(IEnumerable<Control> items)
    {
        var b = UI.Frame(UI.Row(2, items.ToArray()), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);
        return b;
    }

    static void DockAt(DockPanel p, Control c, Dock d) { DockPanel.SetDock(c, d); p.Children.Add(c); }

    static GridSplitter Splitter(int col = 0, int row = 0, HorizontalAlignment align = HorizontalAlignment.Stretch)
    {
        var g = new GridSplitter { Background = Avalonia.Media.Brushes.Transparent };
        if (row > 0) { Grid.SetRow(g, row); g.Height = 4; g.VerticalAlignment = VerticalAlignment.Top; g.ResizeDirection = GridResizeDirection.Rows; }
        else { Grid.SetColumn(g, col); g.Width = 4; g.HorizontalAlignment = align; g.ResizeDirection = GridResizeDirection.Columns; }
        return g;
    }

    void RebuildTabs()
    {
        _docTabs.Children.Clear();
        foreach (var d in _s.Documents.Open)
        {
            var tab = new DocTab { Label = d.Name, Glyph = d.Glyph, IsDirty = d.IsDirty, IsSelected = d == _s.Documents.Active, Height = 33, Name = "Doc_" + d.Name };
            var doc = d;
            tab.Activated += (_, _) => Activate(doc);
            _docTabs.Children.Add(tab);
        }
    }

    void Activate(EditorDocument d)
    {
        _s.Documents.Active = d;
        // #15-#17 of the Editor Map: Game -> Play Mode, .map -> Tilemap Editor, .cs -> Script Editor
        var target = d.Name == "Game" ? "T-P02" : d.Name.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? "T-P03"
            : d.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "T-P04" : null;
        if (target != null) _ = _s.Windows.Open(target, d, source: "Doc_" + d.Name);
    }

    Border StatusBar(StatusModel m)
    {
        StatusItem Item(string name, string glyph, Func<string?> text, string? target = null)
        {
            var i = new StatusItem { Glyph = glyph, Name = name };
            void Update() { i.Content = text(); i.IsVisible = !string.IsNullOrEmpty(text()); }
            Update();
            m.PropertyChanged += (_, _) => Update();
            if (target != null) i.Click += (_, _) => _ = _s.Windows.Open(target, source: name);
            return i;
        }
        var ready = Item("Status_Ready", "check", () => m.Ready);
        ready.IsDot = true;
        void Dot() => ready.Classes.Set("warn", !m.ToolchainOk);
        Dot();
        m.PropertyChanged += (_, _) => Dot();
        var left = UI.Row(16, ready, Item("Status_Build", "check", () => m.Build, "T-P11"), Item("Status_Rom", "chip", () => m.Rom));
        var right = UI.Row(16, Item("Status_Performance", "clock", () => m.Performance), Item("Status_Emulator", "pad", () => m.Emulator, "T-D09"));
        var dock = new DockPanel();
        DockPanel.SetDock(right, Avalonia.Controls.Dock.Right);
        dock.Children.Add(right);
        dock.Children.Add(left);
        var bar = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        bar.Height = 26;
        return bar;
    }
}

/// <summary>A tiny ICommand for buttons inside the shell.</summary>
public sealed class Relay(Action run, Func<bool>? can = null) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => can?.Invoke() ?? true;
    public void Execute(object? parameter) => run();
    public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
