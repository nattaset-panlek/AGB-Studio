using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Docking;
using AgbStudio.Editor.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// The Scene Editor frame (T-P01) without its Phase 11 content: toolbar, docking layout (Hierarchy 260 px, document
/// tabs + viewport, bottom panel 250 px with Assets / Console / Animation / Database, Inspector 320 px) and status bar.
/// The panels are slots filled by registered windows: "T-P01:Hierarchy", "T-P01:Viewport", "T-P01:Inspector",
/// "T-P01:Assets", "T-P05" (Console) and "T-P06" (Animation). They live in a <see cref="DockHost"/>: splitters resize,
/// tabs and headers drag to tab, split or float, and the layout is kept in the preferences.
/// </summary>
[EditorWindow("T-P01")]
public sealed class SceneWorkspace : DockPanel
{
    readonly EditorServices _s;
    readonly IconButton[] _tools;
    readonly DocTabStrip _docTabs;
    readonly PanelTab[] _panelTabs;

    public SceneWorkspace(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    /// <param name="args">What the page was opened for: a .scene path (Command Palette, World Map › Open) switches the
    /// viewport to that scene.</param>
    public SceneWorkspace(EditorServices s, object? args = null)
    {
        _s = s;
        _docTabs = new DocTabStrip(s);
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
        var hierarchyHeader = new PanelHeader { Title = "Hierarchy", Glyph = "layers", ActionGlyph = "plus", Name = "HierarchyHeader" };
        hierarchyHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-M11", anchor: hierarchyHeader, source: "HierarchyAdd"));
        var search = new SearchField { Watermark = "Search scene…", Name = "HierarchySearch" };
        var hierarchy = new DockPanel();
        DockAt(hierarchy, new Border { Padding = new Thickness(10, 8, 10, 4), Child = search }, Avalonia.Controls.Dock.Top);
        hierarchy.Children.Add(Slot("T-P01:Hierarchy"));

        var inspectorHeader = new PanelHeader { Title = "Inspector", Glyph = "sliders", ActionGlyph = "dots", Name = "InspectorHeader" };
        inspectorHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-M14", anchor: inspectorHeader, source: "InspectorMenu"));

        var tabs = UI.Frame(_docTabs, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs");
        _docTabs.VerticalAlignment = VerticalAlignment.Stretch;
        var viewport = UI.Frame(Slot("T-P01:Viewport", args), "bg/canvas").Named("Viewport");
        var document = new Grid { RowDefinitions = RowDefinitions.Parse("34,*"), Name = "DocumentGroup" };
        Grid.SetRow(viewport, 1);
        document.Children.AddRange([tabs, viewport]);

        DockPanelDef[] panels =
        [
            new() { Id = DockLayout.Hierarchy, Title = "Hierarchy", Glyph = "layers", FrameName = "HierarchyPanel", Header = hierarchyHeader, Keep = true, Content = () => hierarchy },
            new() { Id = DockLayout.Inspector, Title = "Inspector", Glyph = "sliders", FrameName = "InspectorPanel", Header = inspectorHeader, Keep = true, Content = () => Slot("T-P01:Inspector") },
            new() { Id = DockLayout.Viewport, Title = "Viewport", Glyph = "map" },
            new() { Id = DockLayout.Assets, Title = "Assets", Glyph = "folder", Content = () => s.Windows.CreatePanel(DockLayout.Assets) },
            new() { Id = DockLayout.Console, Title = "Console", Glyph = "code", Content = () => s.Windows.CreatePanel(DockLayout.Console) },
            new() { Id = DockLayout.Animation, Title = "Animation", Glyph = "film", Content = () => s.Windows.CreatePanel(DockLayout.Animation) },
            new() { Id = DockLayout.Database, Title = "Database", Glyph = "db", ActivateOnly = () => _ = s.Windows.Open(DockLayout.Database, source: "DatabaseTab") },
        ];
        DockArea = new DockHost(DockLayout.FromJson(s.Preferences.Current.SceneLayout, panels.Select(p => p.Id).ToList()), panels, () => document);
        _panelTabs = [DockArea.Tab(DockLayout.Assets), DockArea.Tab(DockLayout.Console), DockArea.Tab(DockLayout.Animation), DockArea.Tab(DockLayout.Database)];
        DockArea.LayoutChanged += () => s.Preferences.Update(x => x.SceneLayout = DockArea.Layout.ToJson());
        DockArea.PanelSelected += (id, tab) => { if (id != DockLayout.Assets) s.Windows.Requests.Add((id, tab.Name)); };
        Children.Add(DockArea);
        WorkspaceLayouts.Attach(s, DockArea);
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
    /// <summary>The panels of the workspace: drag tabs and headers to rearrange them.</summary>
    public DockHost DockArea { get; }

    /// <summary>The console badge (Figma shows 2 when there are build messages).</summary>
    public int? ConsoleCount { get => _panelTabs[1].Count; set => _panelTabs[1].Count = value; }

    void SetTool(int i)
    {
        for (var k = 0; k < 5; k++) _tools[k].IsActive = k == i;
        ToolChanged?.Invoke(i);
    }

    Control Slot(string id, object? args = null) => (Control?)_s.Windows.CreatePanel(id, args) ?? new Panel { Name = "Slot_" + id.Replace(':', '_').Replace('-', '_') };

    static Border ToolGroup(IEnumerable<Control> items)
    {
        var b = UI.Frame(UI.Row(2, items.ToArray()), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);
        return b;
    }

    static void DockAt(DockPanel p, Control c, Dock d) { DockPanel.SetDock(c, d); p.Children.Add(c); }

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
