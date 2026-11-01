using System.Globalization;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Debugger;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>
/// Play Mode (T-P02): the Scene Editor frame with the running game in the Game tab. Toolbar: tools, Hitboxes and Tile
/// collision overlays, Stop / Pause / Step and "Simulating · mGBA", frame number and the Play Speed chip (T-M18). The
/// Inspector shows the selected actor live; the Console shows build and game logs; the status bar shows Running /
/// Paused, Hot reload, fps and CPU. Opened by Build › Play / Play from Here / Build &amp; Run (args: none), the Game
/// tab (args: the document) and Build Report › Run in mGBA (args: <see cref="PlayStart.RunBuilt"/>, or the built ROM's path).
/// </summary>
[EditorWindow("T-P02")]
public sealed class PlayModePage : DockPanel
{
    readonly EditorServices _s;
    readonly PlayController _play;
    readonly StackPanel _docTabs = new() { Orientation = Orientation.Horizontal };
    readonly PanelTab[] _panelTabs;
    readonly ContentControl _bottomContent = new();
    readonly ItemsControl _consoleList = new() { Name = "PlayConsoleList" };
    readonly StackPanel _budgetMeters = new() { Spacing = 7, Name = "BudgetMeters" };
    readonly TextBlock _budgetStatus = UI.Text("all good", "Label/11", "status/success");
    readonly Avalonia.Controls.Shapes.Ellipse _budgetDot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
    int _budgetCountdown;

    public GameView Game { get; }
    public LiveInspector Inspector { get; }
    public Chip HitboxesChip { get; } = new() { Content = "Hitboxes", Glyph = "square", Name = "HitboxesChip" };
    public Chip CollisionChip { get; } = new() { Content = "Tile collision", Glyph = "grid", Name = "TileCollisionChip" };
    public Chip SimulatingChip { get; } = new() { Content = "Simulating · mGBA", Glyph = "rec", Style = ChipStyle.Danger, Name = "SimulatingChip" };
    public Chip FrameChip { get; } = new() { Glyph = "clock", Name = "FrameChip" };
    public Chip SpeedChip { get; } = new() { Glyph = "clock", Name = "SpeedChip" };
    public PlayControl StopButton { get; } = new() { Kind = PlayKind.Stop, Name = "StopButton" };
    public PlayControl PauseButton { get; } = new() { Kind = PlayKind.Pause, Name = "PauseButton" };
    public PlayControl StepButton { get; } = new() { Kind = PlayKind.Step, Name = "StepButton" };
    public StatusItem RunState { get; } = new() { IsDot = true, Name = "Status_RunState" };
    public StatusItem HotReload { get; } = new() { Glyph = "refresh", Content = "Hot reload on", Name = "Status_HotReload" };
    public StatusItem Performance { get; } = new() { Glyph = "clock", Name = "Status_Performance" };
    public StatusItem EmulatorItem { get; } = new() { Glyph = "pad", Content = "mGBA core", Name = "Status_Emulator" };
    public IReadOnlyList<PanelTab> PanelTabs => _panelTabs;

    public PlayModePage(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    public PlayModePage(EditorServices s, object? args = null)
    {
        _s = s;
        _play = PlayController.Of(s);
        Name = "PlayModePage";
        LastChildFill = true;
        Focusable = true;
        Game = new GameView(_play);
        Inspector = new LiveInspector(_play);

        // ---- toolbar (44) ----
        // while the game runs only Select works (click an actor in the game view to inspect it); the editing tools are off
        var tools = new[] { "cursor", "move", "pencil", "bucket", "eraser", "grid" }.Select((g, i) => new IconButton { Glyph = g, Name = "Tool_" + g, IsEnabled = i == 0 }).ToArray();
        ToolTip.SetTip(tools[0], new TipContent(s.Commands.Get("scene.tool.select").Title));
        HitboxesChip.Click += (_, _) => { _play.Hitboxes = !_play.Hitboxes; UpdateChrome(); Game.Screen.InvalidateVisual(); };
        CollisionChip.Click += (_, _) => { _play.TileCollision = !_play.TileCollision; UpdateChrome(); Game.Screen.InvalidateVisual(); };
        StopButton.Command = s.Commands.Get("play.stop");
        PauseButton.Command = s.Commands.Get("play.pause");
        StepButton.Command = s.Commands.Get("play.frameStep");
        ToolTip.SetTip(StopButton, new TipContent("Stop"));
        ToolTip.SetTip(PauseButton, new TipContent("Pause"));
        ToolTip.SetTip(StepButton, new TipContent("Frame step", "F10"));
        SpeedChip.Click += (_, _) => _ = s.Windows.Open("T-M18", anchor: SpeedChip, source: "SpeedChip");
        // the frame number changes every frame: a fixed width with tabular digits keeps the toolbar still
        FrameCounter.Fix(FrameChip, 128);
        var left = UI.Row(8, ToolGroup(tools), HitboxesChip, CollisionChip);
        var center = UI.Row(4, StopButton, PauseButton, StepButton, SimulatingChip);
        var right = UI.Row(8, FrameChip, SpeedChip);
        var bar = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center };
        void At(Control c, int col) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        At(left, 0);
        At(center, 2);
        At(right, 4);
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        SetDock(toolbar, Avalonia.Controls.Dock.Top);
        Children.Add(toolbar);

        // ---- status bar (26) ----
        var statusLeft = UI.Row(16, RunState, HotReload);
        var statusRight = UI.Row(16, Performance, EmulatorItem);
        EmulatorItem.Click += (_, _) => _ = s.Windows.Open("T-D09", source: "Status_Emulator");
        var statusDock = new DockPanel();
        DockPanel.SetDock(statusRight, Avalonia.Controls.Dock.Right);
        statusDock.Children.Add(statusRight);
        statusDock.Children.Add(statusLeft);
        var status = UI.Frame(statusDock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        status.Height = 26;
        SetDock(status, Avalonia.Controls.Dock.Bottom);
        Children.Add(status);

        // ---- body ----
        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        var hierarchyHeader = new PanelHeader { Title = "Hierarchy", Glyph = "layers", ActionGlyph = "plus", Name = "HierarchyHeader" };
        hierarchyHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-M11", anchor: hierarchyHeader, source: "HierarchyAdd"));
        var hierarchy = new DockPanel();
        DockAt(hierarchy, hierarchyHeader, Avalonia.Controls.Dock.Top);
        DockAt(hierarchy, new Border { Padding = new Thickness(10, 8, 10, 4), Child = new SearchField { Watermark = "Search scene…", Name = "HierarchySearch" } }, Avalonia.Controls.Dock.Top);
        hierarchy.Children.Add(s.Windows.CreatePanel("T-P01:Hierarchy") ?? new Panel());
        var leftPanel = UI.Frame(hierarchy, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("HierarchyPanel");
        leftPanel.ClipToBounds = true;
        body.Children.Add(leftPanel);

        var inspectorHeader = new PanelHeader { Title = "Inspector · Live", Glyph = "sliders", ActionGlyph = "dots", Name = "InspectorHeader" };
        inspectorHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-M14", anchor: inspectorHeader, source: "InspectorMenu"));
        var inspector = new DockPanel();
        DockAt(inspector, inspectorHeader, Avalonia.Controls.Dock.Top);
        DockAt(inspector, BudgetBlock(), Avalonia.Controls.Dock.Bottom);
        inspector.Children.Add(Inspector);
        var rightPanel = UI.Frame(inspector, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("InspectorPanel");
        rightPanel.ClipToBounds = true;
        Grid.SetColumn(rightPanel, 2);
        body.Children.Add(rightPanel);

        var centerGrid = new Grid { RowDefinitions = RowDefinitions.Parse("34,*,250"), Name = "Center" };
        Grid.SetColumn(centerGrid, 1);
        _docTabs.VerticalAlignment = VerticalAlignment.Stretch;
        centerGrid.Children.Add(UI.Frame(_docTabs, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs"));
        var viewport = UI.Frame(Game, "lit/black").Named("Viewport");
        Grid.SetRow(viewport, 1);
        centerGrid.Children.Add(viewport);
        _panelTabs =
        [
            new PanelTab { Label = "Assets", Glyph = "folder", Name = "Tab_Assets", Tag = "T-P01:Assets" },
            new PanelTab { Label = "Console", Glyph = "code", Name = "Tab_Console", Tag = "Console" },
            new PanelTab { Label = "Animation", Glyph = "film", Name = "Tab_Animation", Tag = "T-P06" },
            new PanelTab { Label = "Database", Glyph = "db", Name = "Tab_Database", Tag = "T-P07" },
        ];
        foreach (var t in _panelTabs) t.Activated += (_, _) => SelectPanel(t);
        var panelTabRow = new Border { Height = 33, Padding = new Thickness(6, 0), Child = UI.Row(2, _panelTabs), BorderThickness = new Thickness(0, 0, 0, 1) }.Token(Border.BorderBrushProperty, "border/default");
        var bottom = new DockPanel();
        DockAt(bottom, panelTabRow, Avalonia.Controls.Dock.Top);
        bottom.Children.Add(_bottomContent);
        var bottomPanel = UI.Frame(bottom, "bg/panel", "border/default", new Thickness(0, 1, 0, 0)).Named("BottomPanel");
        Grid.SetRow(bottomPanel, 2);
        centerGrid.Children.Add(bottomPanel);
        body.Children.Add(centerGrid);
        Children.Add(body);

        _consoleList.ItemsSource = _play.Console.Entries;
        _consoleList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ConsoleEntry>((e, _) => new ConsoleLine { Time = e?.TimeText, Text = e?.Text, Level = e?.Level ?? ConsoleLevel.Info });
        _consoleList.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new StackPanel { Spacing = 2 });
        SelectPanel(_panelTabs[1]);

        _panelTabs[1].Count = _play.Console.Entries.Count;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        RebuildTabs();
        UpdateChrome();
        RefreshBudget();

        // Game tab, Build Report › Run in mGBA: start when nothing runs yet
        if (args is PlayStart how && _play.State != PlayState.Building) _ = _play.StartAsync(how);
        else if (args is string rom && File.Exists(rom) && _play.State != PlayState.Building) _ = _play.StartAsync(PlayStart.RunBuilt, rom);
        else if (args is EditorDocument && !_play.IsRunning && _play.State != PlayState.Building) _ = _play.StartAsync(PlayStart.Play);
    }

    static Border ToolGroup(IEnumerable<Control> items) => UI.Frame(UI.Row(2, items.ToArray()), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    static void DockAt(DockPanel p, Control c, Avalonia.Controls.Dock d) { DockPanel.SetDock(c, d); p.Children.Add(c); }

    Border BudgetBlock()
    {
        var title = UI.Text("GBA Budget", "Overline/11", "text/secondary");
        var icon = new Graphics.Icon { Glyph = "chip", Size = 13, VerticalAlignment = VerticalAlignment.Center }.Token(Graphics.Icon.ForegroundProperty, "text/secondary");
        _budgetDot.Token(Avalonia.Controls.Shapes.Shape.FillProperty, "status/success");
        var statusRow = UI.Row(5, _budgetDot, _budgetStatus);
        var header = new DockPanel { Height = 14 };
        DockPanel.SetDock(statusRow, Avalonia.Controls.Dock.Right);
        header.Children.Add(statusRow);
        header.Children.Add(UI.Row(8, icon, title));
        var open = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = header, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Name = "BudgetButton" };
        open.Click += (_, _) => _ = _s.Windows.Open("T-O06", anchor: open, source: "GbaBudget");
        var col = UI.Column(7, open, _budgetMeters);
        var b = UI.Frame(col, "bg/sunken", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 10, 12, 12)).Named("BudgetBlock");
        return b;
    }

    void RefreshBudget()
    {
        var snap = BudgetService.Of(_s).Current();
        string[] labels = ["VRAM BG", "VRAM OBJ", "OAM"];
        var rows = labels.Select(l => snap.Line(l)).Where(l => l != null).Select(l => (l!.Label, l.Text, l.Percent, l.Status)).ToList();
        var bgPal = snap.Line("BG palettes");
        var objPal = snap.Line("OBJ palettes");
        if (bgPal != null && objPal != null)
        {
            var fullest = snap.Lines.FirstOrDefault(l => l.Label.StartsWith("Palette OBJ", StringComparison.Ordinal));
            var pct = fullest?.Percent ?? Math.Max(bgPal.Percent, objPal.Percent);
            rows.Add(("Palettes", $"BG {bgPal.Used} · OBJ {objPal.Used}", pct, fullest?.Status ?? ProgressStatus.OK));
        }
        if (snap.Line("EWRAM") is { } ew) rows.Add((ew.Label, ew.Text, ew.Percent, ew.Status));
        while (_budgetMeters.Children.Count > rows.Count) _budgetMeters.Children.RemoveAt(_budgetMeters.Children.Count - 1);
        for (var i = 0; i < rows.Count; i++)
        {
            if (i >= _budgetMeters.Children.Count) _budgetMeters.Children.Add(new Meter { Name = "Budget_" + rows[i].Label.Replace(" ", "") });
            var m = (Meter)_budgetMeters.Children[i];
            (m.Label, m.ValueText, m.Percent, m.Status) = rows[i];
        }
        _budgetStatus.Text = snap.StatusText;
        var token = snap.AllGood ? "status/success" : "status/danger";
        _budgetStatus.Token(TextBlock.ForegroundProperty, token);
        _budgetDot.Token(Avalonia.Controls.Shapes.Shape.FillProperty, token);
    }

    void SelectPanel(PanelTab tab)
    {
        var id = (string)tab.Tag!;
        if (id == "T-P07") { _ = _s.Windows.Open(id, source: "DatabaseTab"); return; }
        foreach (var t in _panelTabs) t.IsSelected = t == tab;
        _bottomContent.Content = id == "Console"
            ? new ScrollViewer { Content = new Border { Padding = new Thickness(12, 6, 12, 6), Child = _consoleList }, Name = "PlayConsole" }
            : _s.Windows.CreatePanel(id);
    }

    void RebuildTabs()
    {
        _docTabs.Children.Clear();
        foreach (var d in _s.Documents.Open)
        {
            var isGame = d.Name == "Game";
            var tab = new DocTab { Label = d.Name, Glyph = d.Glyph, IsDirty = d.IsDirty, IsSelected = isGame, Height = 33, Name = "Doc_" + d.Name };
            var doc = d;
            tab.Activated += (_, _) =>
            {
                if (doc.Name == "Game") return;
                _s.Documents.Active = doc;
                var target = doc.Name.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? "T-P03" : doc.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "T-P04" : "T-P01";
                _ = _s.Windows.Open(target, target == "T-P01" ? null : doc, source: "Doc_" + doc.Name);
            };
            _docTabs.Children.Add(tab);
        }
    }

    void OnChanged()
    {
        UpdateChrome();
        Game.Refresh();
        Inspector.Refresh();
    }

    void OnFrame()
    {
        Game.Refresh();
        Inspector.Refresh();
        UpdateChrome();
        if (--_budgetCountdown <= 0) { _budgetCountdown = 30; RefreshBudget(); }
    }

    /// <summary>Toolbar chips and status bar from the controller (P12.08).</summary>
    public void UpdateChrome()
    {
        HitboxesChip.Style = _play.Hitboxes ? ChipStyle.Accent : ChipStyle.Default;
        CollisionChip.Style = _play.TileCollision ? ChipStyle.Accent : ChipStyle.Default;
        FrameChip.Content = "Frame " + _play.Frame.ToString(CultureInfo.InvariantCulture);
        SpeedChip.Content = PlayCommands.SpeedText(_play.FastForwarding ? _play.Options.FastForwardSpeed : _play.Speed);
        SimulatingChip.IsVisible = _play.Session != null;
        RunState.Content = _play.State switch
        {
            PlayState.Building => "Building",
            PlayState.BuildFailed => "Build failed",
            PlayState.Running when _play.IsPaused => "Paused",
            PlayState.Running => "Running",
            _ => "Stopped",
        };
        RunState.Classes.Set("warn", _play.State != PlayState.Running || _play.IsPaused);
        HotReload.IsVisible = _play.HotReloadEnabled;
        Performance.Content = string.Create(CultureInfo.InvariantCulture, $"{_play.Fps:0.0} fps · CPU {_play.CpuPercent:0}%");
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !_play.IsRunning || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.KeyModifiers != KeyModifiers.None && e.Key != Key.Tab) return;
        if (_play.KeyDown(e.Key)) e.Handled = true;
    }

    void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_play.IsRunning) return;
        if (_play.KeyUp(e.Key)) e.Handled = true;
    }

    void OnConsole(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => _panelTabs[1].Count = _play.Console.Entries.Count;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _s.Documents.Changed += RebuildTabs;
        _play.Changed += OnChanged;
        _play.FrameUpdated += OnFrame;
        _play.Console.Entries.CollectionChanged += OnConsole;
        RebuildTabs();
        Game.Screen.Focus();
        OnChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _s.Documents.Changed -= RebuildTabs;
        _play.Changed -= OnChanged;
        _play.FrameUpdated -= OnFrame;
        _play.Console.Entries.CollectionChanged -= OnConsole;
    }
}
