using System.Globalization;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Debugger;

/// <summary>The Debugger views (Figma: VRAM tiles, OAM sprites, Palettes, Memory, Profiler, Breakpoints).</summary>
public enum DebuggerView { VramTiles, OamSprites, Palettes, Memory, Profiler, Breakpoints }

/// <summary>
/// The Debugger (T-P19 with the VRAM tiles view and the Profiler, T-P23 Memory, T-P24 Palettes): views and breakpoints
/// on the left, the view in the middle, its panel on the right. Toolbar: breakpoints armed, step to the next line,
/// restart, "Paused at frame N"; Stop / Pause / Step; the live video mode. Opened by the Debug button (#9),
/// Tools › Debugger and Views › Memory / Palettes (P13.09).
/// </summary>
[EditorWindow("T-P19")]
[EditorWindow("T-P23")]
[EditorWindow("T-P24")]
public sealed class DebuggerPage : DockPanel
{
    readonly EditorServices _s;
    readonly DebuggerModel _m;
    readonly ListRow[] _views;
    readonly StackPanel _breakpoints = new() { Spacing = 7, Name = "BreakpointList" };
    readonly ContentControl _center = new() { Name = "DebuggerCenter" };
    readonly ContentControl _right = new() { Name = "DebuggerRight" };
    readonly PanelHeader _rightHeader = new() { Name = "RightHeader" };

    public DebuggerView View { get; private set; }
    public VramOamView Vram { get; }
    public ProfilerPanel Profiler { get; }
    public MemoryView Memory { get; }
    public WatchPanel Watch { get; }
    public PalettesView Palettes { get; }
    public ColourPanel Colour { get; }
    public IconButton ArmButton { get; } = new() { Glyph = "bug", Name = "ArmBreakpoints" };
    public IconButton StepLineButton { get; } = new() { Glyph = "step", Name = "StepLine" };
    public IconButton RestartButton { get; } = new() { Glyph = "refresh", Name = "Restart" };
    public Chip PausedChip { get; } = new() { Glyph = "pause", Style = ChipStyle.Warning, Name = "PausedChip" };
    public Chip ModeChip { get; } = new() { Glyph = "chip", Name = "VideoModeChip" };
    public StatusItem RunState { get; } = new() { IsDot = true, Name = "Status_RunState" };
    public StatusItem Location { get; } = new() { Glyph = "code", Name = "Status_Location" };
    public StatusItem Performance { get; } = new() { Glyph = "clock", Name = "Status_Performance" };
    public IReadOnlyList<ListRow> ViewRows => _views;
    public IReadOnlyList<CheckBox> BreakpointBoxes => _breakpoints.Children.OfType<CheckBox>().ToList();

    public DebuggerPage(WindowContext ctx) : this(ctx.Services, ctx.Info.Id) { }

    public DebuggerPage(EditorServices s, string id = "T-P19")
    {
        _s = s;
        _m = DebuggerModel.Of(s);
        Name = "DebuggerPage";
        LastChildFill = true;
        Vram = new VramOamView(_m);
        Profiler = new ProfilerPanel(_m);
        Memory = new MemoryView(_m);
        Watch = new WatchPanel(_m);
        Palettes = new PalettesView(_m);
        Colour = new ColourPanel(_m, s);
        _m.AddDefaultWatches();

        // ---- toolbar ----
        ArmButton.Click += (_, _) => _m.Armed = !_m.Armed;
        StepLineButton.Click += (_, _) => _m.StepLine();
        RestartButton.Click += (_, _) => _m.Restart();
        ToolTip.SetTip(ArmButton, new TipContent("Breakpoints"));
        ToolTip.SetTip(StepLineButton, new TipContent("Step to the next line"));
        ToolTip.SetTip(RestartButton, new TipContent("Restart"));
        PausedChip.Click += (_, _) => _m.Resume();
        var stop = new PlayControl { Kind = PlayKind.Stop, Command = s.Commands.Get("play.stop"), Name = "StopButton" };
        var pause = new PlayControl { Kind = PlayKind.Pause, Name = "PauseButton" };
        pause.Click += (_, _) => { if (_m.IsPaused) _m.Resume(); else _m.Play.SetPaused(true); };
        var step = new PlayControl { Kind = PlayKind.Step, Command = s.Commands.Get("play.frameStep"), Name = "StepButton" };
        var simulating = new Chip { Content = "Simulating · mGBA", Glyph = "rec", Style = ChipStyle.Danger, Name = "SimulatingChip" };
        ModeChip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: ModeChip, source: "DebuggerVideoMode");
        var undo = new IconButton { Glyph = "undo", Command = s.Commands.Get("edit.undo"), Name = "UndoButton" };
        var redo = new IconButton { Glyph = "redo", Command = s.Commands.Get("edit.redo"), Name = "RedoButton" };
        var bar = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center };
        void At(Control c, int col) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        At(UI.Row(8, ToolGroup(ArmButton, StepLineButton, RestartButton), PausedChip), 0);
        At(UI.Row(4, stop, pause, step, simulating), 2);
        At(UI.Row(8, ModeChip, ToolGroup(undo, redo)), 4);
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        SetDock(toolbar, Avalonia.Controls.Dock.Top);
        Children.Add(toolbar);

        // ---- status bar ----
        var left = UI.Row(16, RunState, Location);
        var dock = new DockPanel();
        DockPanel.SetDock(Performance, Avalonia.Controls.Dock.Right);
        dock.Children.Add(Performance);
        dock.Children.Add(left);
        var status = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        status.Height = 26;
        SetDock(status, Avalonia.Controls.Dock.Bottom);
        Children.Add(status);

        // ---- body: views | center | right ----
        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("220,*,340"), Name = "Body" };
        (string Label, string Glyph, DebuggerView View)[] views =
        [
            ("VRAM tiles", "tiles", DebuggerView.VramTiles), ("OAM sprites", "user", DebuggerView.OamSprites), ("Palettes", "sliders", DebuggerView.Palettes),
            ("Memory", "chip", DebuggerView.Memory), ("Profiler", "clock", DebuggerView.Profiler), ("Breakpoints", "bug", DebuggerView.Breakpoints),
        ];
        _views = views.Select(v => new ListRow { Label = v.Label, Glyph = v.Glyph, Tag = v.View, Name = "View_" + v.View }).ToArray();
        foreach (var v in _views) v.Activated += (_, _) => Show((DebuggerView)v.Tag!);
        var side = new StackPanel();
        side.Children.Add(new PanelHeader { Title = "Views", Glyph = "bug", Name = "ViewsHeader" });
        side.Children.Add(new StackPanel { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Children = { _views[0], _views[1], _views[2], _views[3], _views[4], _views[5] } });
        side.Children.Add(new PanelHeader { Title = "Breakpoints", Glyph = "bug", Name = "BreakpointsHeader" });
        _breakpoints.Margin = new Thickness(12, 8, 12, 8);
        side.Children.Add(_breakpoints);
        var leftPanel = UI.Frame(side, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("ViewsPanel");
        body.Children.Add(leftPanel);

        var center = new DockPanel();
        var tabs = UI.Row(0,
            DocTabOf("Game", "pad", false, () => _ = s.Windows.Open("T-P02", source: "DebuggerGameTab")),
            DocTabOf("Debugger", "bug", true, null));
        var tabRow = UI.Frame(tabs, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs");
        tabRow.Height = 34;
        DockPanel.SetDock(tabRow, Avalonia.Controls.Dock.Top);
        center.Children.Add(tabRow);
        center.Children.Add(new ScrollViewer { Content = _center, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        var centerFrame = UI.Frame(center, "bg/base").Named("CenterPanel");
        Grid.SetColumn(centerFrame, 1);
        body.Children.Add(centerFrame);

        var right = new DockPanel();
        DockPanel.SetDock(_rightHeader, Avalonia.Controls.Dock.Top);
        right.Children.Add(_rightHeader);
        right.Children.Add(new ScrollViewer { Content = _right, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var rightFrame = UI.Frame(right, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("RightPanel");
        Grid.SetColumn(rightFrame, 2);
        body.Children.Add(rightFrame);
        Children.Add(body);

        Show(id switch { "T-P23" => DebuggerView.Memory, "T-P24" => DebuggerView.Palettes, _ => DebuggerView.VramTiles });
        RebuildBreakpoints();
    }

    static Border ToolGroup(params Control[] items) => UI.Frame(UI.Row(2, items), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    static DocTab DocTabOf(string label, string glyph, bool selected, Action? open)
    {
        var t = new DocTab { Label = label, Glyph = glyph, IsSelected = selected, Height = 33, Name = "Doc_" + label };
        if (open != null) t.Activated += (_, _) => open();
        return t;
    }

    /// <summary>Selects a view (Views list; T-C41 Memory, T-C42 Palettes).</summary>
    public void Show(DebuggerView view)
    {
        View = view;
        foreach (var v in _views) v.IsSelected = (DebuggerView)v.Tag! == view;
        switch (view)
        {
            case DebuggerView.Memory:
                _center.Content = Memory;
                _right.Content = Watch;
                (_rightHeader.Title, _rightHeader.Glyph) = ("Watch memory", "eye");
                break;
            case DebuggerView.Palettes:
                _center.Content = Palettes;
                _right.Content = Colour;
                (_rightHeader.Title, _rightHeader.Glyph) = ("Colour", "pipette");
                break;
            default:
                _center.Content = Vram;
                _right.Content = Profiler;
                (_rightHeader.Title, _rightHeader.Glyph) = ("Profiler", "clock");
                break;
        }
        Refresh();
    }

    void RebuildBreakpoints()
    {
        _breakpoints.Children.Clear();
        foreach (var b in _m.Breakpoints)
        {
            var box = new CheckBox { Content = b.Label, IsChecked = b.Enabled, Name = "Breakpoint_" + b.Label };
            var bp = b;
            box.IsCheckedChanged += (_, _) => bp.Enabled = box.IsChecked == true;
            _breakpoints.Children.Add(box);
        }
    }

    /// <summary>Reads the model into the visible view, toolbar and status bar.</summary>
    public void Refresh()
    {
        var play = _m.Play;
        ArmButton.IsActive = _m.Armed;
        PausedChip.IsVisible = _m.IsPaused;
        PausedChip.Content = "Paused at frame " + (_m.AtBreakpoint ? _m.PausedFrame : play.Frame).ToString(CultureInfo.InvariantCulture);
        if (play.Core is { } core)
        {
            try
            {
                var (mode, _, _) = Inspector.Display(core);
                ModeChip.Content = $"GBA · Mode {mode} · 240×160";
            }
            catch (ArgumentOutOfRangeException) { }
        }
        else ModeChip.Content = "GBA · Mode 0 · 240×160";
        RunState.Content = play.Session == null ? "Stopped" : _m.IsPaused ? "Paused" : "Running";
        Location.Content = _m.PausedAt;
        Location.IsVisible = _m.PausedAt != null;
        Performance.Content = string.Create(CultureInfo.InvariantCulture, $"{play.Fps:0.0} fps · CPU {play.CpuPercent:0}%");
        if (_breakpoints.Children.Count != _m.Breakpoints.Count) RebuildBreakpoints();
        switch (View)
        {
            case DebuggerView.Memory: Memory.Refresh(); Watch.Refresh(); break;
            case DebuggerView.Palettes: Palettes.Refresh(); Colour.Refresh(); break;
            default: Vram.Refresh(); Profiler.Refresh(); break;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _m.Updated += Refresh;
        _s.Documents.Changed += ShowTitle;
        ShowTitle();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _m.Updated -= Refresh;
        _s.Documents.Changed -= ShowTitle;
        // the breadcrumb goes back to the active document, as the shell shows it
        if (e.Root is MainWindow w) w.TitleBar.Document = _s.Documents.Active is { } d ? (d.Path is { } p ? System.IO.Path.GetFileName(p) : d.Name) : null;
    }

    /// <summary>Breadcrumb "Sky Hopper / Debugger" while the Debugger is shown.</summary>
    void ShowTitle()
    {
        if (TopLevel.GetTopLevel(this) is MainWindow w) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (IsAttachedToVisualTree()) w.TitleBar.Document = "Debugger"; });
    }

    bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) != null;

    // ---- commands -----------------------------------------------------------------------------------

    /// <summary>Debug (#9) and Tools › Debugger: opens the Debugger and starts the game from the Scene Editor's scene when nothing runs.</summary>
    [CommandHandler("tools.debugger")]
    public static void OpenDebugger(EditorServices s)
    {
        var play = PlayController.Of(s);
        _ = s.Windows.Open("T-P19", source: "tools.debugger");
        if (play.Session == null && play.State != PlayState.Building && s.Project.Current != null) _ = play.StartAsync(PlayStart.FromHere);
    }
}
