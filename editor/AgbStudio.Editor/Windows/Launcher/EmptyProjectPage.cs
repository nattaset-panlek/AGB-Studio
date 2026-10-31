using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>
/// Scene Editor · Empty project (T-P22, 1440 x 900; checklist 4.9 "whole project empty"): the Scene Editor frame
/// with the empty states of every panel. It opens when the project that is opened or created has no assets, scripts,
/// actors or map, and hands over to the Scene Editor (T-P01) as soon as the project gets content: an actor is added,
/// files are imported or dropped, or the user starts painting.
/// </summary>
[EditorWindow("T-P22")]
public sealed class EmptyProjectPage : DockPanel
{
    readonly EditorServices _s;
    readonly IconButton[] _tools;
    readonly StackPanel _docTabs = new() { Orientation = Orientation.Horizontal, Name = "DocTabs" };
    readonly TreeItem _sceneItem = new() { Glyph = "map", Toggle = TreeToggle.Open, Depth = 0, Name = "Tree_Scene" };
    readonly TreeItem _cameraItem = new() { Label = "Main Camera", Glyph = "camera", Depth = 1, Name = "Tree_MainCamera" };
    readonly StatusItem _projectStatus = new() { Glyph = "folder", Name = "Status_Project" };
    readonly StatusItem _romStatus = new() { Glyph = "chip", Name = "Status_Rom" };
    readonly StatusItem _ready = new() { IsDot = true, Content = "Ready", Name = "Status_Ready" };
    readonly Meter _vramBg = new() { Label = "VRAM BG", Name = "Meter_VramBg" };
    readonly Meter _vramObj = new() { Label = "VRAM OBJ", Name = "Meter_VramObj" };
    readonly Meter _oam = new() { Label = "OAM", Name = "Meter_Oam" };
    readonly PanelTab[] _panelTabs;
    readonly ContentControl _bottomContent = new();
    readonly Control _assetsEmpty;
    FileSystemWatcher? _watcher;
    bool _checkQueued;

    public EmptyProjectPage(WindowContext ctx) : this(ctx.Services) { }

    public EmptyProjectPage(EditorServices s)
    {
        _s = s;
        Name = "EmptyProjectPage";
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // ---- toolbar (44): as the Scene Editor ----
        string[] toolCommands = ["scene.tool.select", "scene.tool.move", "scene.tool.paint", "", "", "scene.toggleGrid"];
        _tools = new[] { "cursor", "move", "pencil", "bucket", "eraser", "grid" }.Select(g => new IconButton { Glyph = g, Name = "Tool_" + g }).ToArray();
        for (var i = 0; i < _tools.Length; i++)
        {
            var k = i;
            _tools[i].Click += (_, _) =>
            {
                if (k < 5) SetTool(k);
                else _tools[5].IsActive = !_tools[5].IsActive;
                if (toolCommands[k].Length > 0 && s.Commands.Get(toolCommands[k]).IsImplemented) s.Commands.Execute(toolCommands[k]);
            };
            if (toolCommands[i].Length > 0) Tip(_tools[i], toolCommands[i]);
        }
        SetTool(0);
        var layers = new[] { "BG0", "BG1", "BG2", "BG3", "OBJ" }.Select(l => (Control)new LayerToggle { Content = l, IsOn = l != "BG3", Name = "Layer_" + l }).ToArray();
        var play = new PlayControl { Kind = PlayKind.Play, Command = s.Commands.Get("build.play"), Name = "PlayButton" };
        Tip(play, "build.play");
        var playRow = UI.Row(4, play, PlayButtons.Pause(s), PlayButtons.Step(s));
        var chip = new Chip { Content = "GBA · Mode 0 · 240×160", Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: "VideoModeChip");
        var undo = new IconButton { Glyph = "undo", Command = s.Commands.Get("edit.undo"), Name = "UndoButton" };
        var redo = new IconButton { Glyph = "redo", Command = s.Commands.Get("edit.redo"), Name = "RedoButton" };
        var bar = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,8,Auto,8,*,8,Auto,8,*,8,Auto,8,Auto"), VerticalAlignment = VerticalAlignment.Center };
        void At(Control c, int col) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        At(ToolGroup(_tools), 0);
        At(ToolGroup(layers), 2);
        At(playRow, 6);
        At(chip, 10);
        At(ToolGroup([undo, redo]), 12);
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        SetDock(toolbar, Dock.Top);
        Children.Add(toolbar);

        // ---- status bar (26) ----
        var status = StatusBar();
        SetDock(status, Dock.Bottom);
        Children.Add(status);

        // ---- body ----
        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        body.Children.Add(Hierarchy());
        var inspector = Inspector();
        Grid.SetColumn(inspector, 2);
        body.Children.Add(inspector);

        var center = new Grid { RowDefinitions = RowDefinitions.Parse("34,*,250"), Name = "Center" };
        Grid.SetColumn(center, 1);
        center.Children.Add(UI.Frame(_docTabs, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabsBar"));
        var viewport = UI.Frame(new Panel { Children = { new ScreenFrame { Name = "ScreenFrame" }, EmptyScene() } }, "bg/canvas").Named("Viewport");
        Grid.SetRow(viewport, 1);
        center.Children.Add(viewport);

        _assetsEmpty = AssetsEmpty();
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
        SetDock(panelTabRow, Dock.Top);
        bottom.Children.AddRange([panelTabRow, _bottomContent]);
        var bottomPanel = UI.Frame(bottom, "bg/panel", "border/default", new Thickness(0, 1, 0, 0)).Named("BottomPanel");
        Grid.SetRow(bottomPanel, 2);
        center.Children.Add(bottomPanel);
        body.Children.Add(center);
        Children.Add(body);
        SelectPanel(_panelTabs[0]);

        s.Documents.Changed += () => Dispatcher.UIThread.Post(Refresh);
        s.Status.PropertyChanged += (_, _) => Dispatcher.UIThread.Post(Refresh);
        s.Commands.Executed += OnCommand;
        Refresh();
    }

    static Border ToolGroup(IEnumerable<Control> items) => UI.Frame(UI.Row(2, items.ToArray()), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    void Tip(Control c, string commandId)
    {
        var cmd = _s.Commands.Get(commandId);
        ToolTip.SetTip(c, new TipContent(cmd.Title, _s.Shortcuts.GestureOf(commandId) is { } g ? AgbStudio.Editor.Theme.Shortcuts.Format(g) : null));
    }

    void SetTool(int i)
    {
        for (var k = 0; k < 5; k++) _tools[k].IsActive = k == i;
    }

    public int ActiveTool => Array.FindIndex(_tools[..5], t => t.IsActive);

    // ---- panels -------------------------------------------------------------------------------------

    Border Hierarchy()
    {
        var header = new PanelHeader { Title = "Hierarchy", Glyph = "layers", ActionGlyph = "plus", Name = "HierarchyHeader" };
        header.ActionCommand = new Relay(() => _ = _s.Windows.Open("T-M11", anchor: header, source: "HierarchyAdd"));
        var tree = UI.Column(1, _sceneItem, _cameraItem);
        tree.Margin = new Thickness(6, 3, 6, 0);
        _sceneItem.Activated += (_, _) => { _sceneItem.IsSelected = true; _cameraItem.IsSelected = false; };
        _cameraItem.Activated += (_, _) => { _cameraItem.IsSelected = true; _sceneItem.IsSelected = false; };
        var hint = UI.Text("Nothing else here yet. Press + or drag a sprite from Assets into the scene.", "Label/11", "text/muted").Named("HierarchyHint");
        hint.TextWrapping = TextWrapping.Wrap;
        hint.Margin = new Thickness(16, 21, 16, 0);
        var dock = new DockPanel();
        SetDock(header, Dock.Top);
        dock.Children.AddRange([header, new StackPanel { Children = { tree, hint } }]);
        var left = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("HierarchyPanel");
        left.ClipToBounds = true;
        return left;
    }

    Border Inspector()
    {
        var header = new PanelHeader { Title = "Inspector", Glyph = "sliders", Name = "InspectorHeader" };
        var keys = UI.Column(10);
        keys.Name = "UsefulKeys";
        foreach (var (cmd, label) in new[] { ("file.importAsset", "Import assets"), ("scene.tool.paint", "Paint tiles"), ("build.play", "Play"), ("edit.commandPalette", "Command palette") })
        {
            var gesture = _s.Shortcuts.GestureOf(cmd) is { } g ? AgbStudio.Editor.Theme.Shortcuts.Format(g) : "";
            var row = UI.Row(6, KeyboardShortcutsDialog.KeyParts(gesture).Select(p => (Control)new Keycap { Label = p, VerticalAlignment = VerticalAlignment.Center }).ToArray());
            row.Children.Add(UI.Text(label, "Body/12", "text/secondary"));
            row.Height = 22;
            keys.Children.Add(row);
        }
        var info = new StackPanel { Margin = new Thickness(20, 20, 20, 0), Name = "InspectorEmpty" };
        info.Children.Add(UI.Text("Nothing selected", "Body/12 Semibold", "text/primary"));
        info.Children.Add(UI.Text("Select an actor or a layer to edit it here.", "Label/11", "text/muted").Named("InspectorHint"));
        info.Children[1].Margin = new Thickness(0, 10, 0, 0);
        var useful = UI.Text("Useful keys", "Overline/11", "text/secondary");
        useful.Margin = new Thickness(0, 10, 0, 9);
        info.Children.Add(useful);
        info.Children.Add(keys);
        var budget = UI.Frame(UI.Column(7, _vramBg, _vramObj, _oam), "bg/sunken", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 10, 12, 11)).Named("Budget");
        var dock = new DockPanel();
        SetDock(header, Dock.Top);
        SetDock(budget, Dock.Bottom);
        dock.Children.AddRange([header, budget, info]);
        var right = UI.Frame(dock, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("InspectorPanel");
        right.ClipToBounds = true;
        return right;
    }

    Control EmptyScene()
    {
        var title = UI.Text("This scene is empty", "Title/14", "text/primary");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 15, 0, 0);
        var text = UI.Text("The dashed box is the GBA screen, 240 × 160. Start with one of these:", "Body/12", "text/secondary");
        text.TextWrapping = TextWrapping.Wrap;
        text.TextAlignment = TextAlignment.Center;
        text.Margin = new Thickness(0, 11, 0, 0);
        var add = new AgbButton { Content = "Add player", Glyph = "user", Kind = ButtonKind.Primary, Name = "AddPlayer" };
        add.Click += (_, _) => AddPlayer();
        var paint = new AgbButton { Content = "Paint tiles", Glyph = "pencil", Kind = ButtonKind.Secondary, Name = "PaintTiles" };
        paint.Click += (_, _) => PaintTiles();
        var import = new AgbButton { Content = "Import images", Glyph = "upload", Kind = ButtonKind.Secondary, Name = "ImportImages" };
        import.Click += (_, _) => _ = _s.Windows.Open("T-D04", source: "EmptySceneImport");
        var buttons = UI.Row(8, add, paint, import);
        buttons.HorizontalAlignment = HorizontalAlignment.Center;
        buttons.Margin = new Thickness(0, 12, 0, 0);
        var hint = UI.Text("or drop PNG / Aseprite files anywhere", "Label/11", "text/muted");
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.Margin = new Thickness(0, 12, 0, 0);
        var icon = new Icon { Glyph = "map", Size = 24, HorizontalAlignment = HorizontalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
        var col = new StackPanel { Margin = new Thickness(20, 26, 20, 24), Children = { icon, title, text, buttons, hint } };
        var card = UI.Frame(col, "bg/panel", "border/default", pad: default, radius: 12).Named("EmptySceneCard");
        card.Width = 420;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        card.Margin = new Thickness(0, 6, 0, 0);   // Figma puts the 216 px card 3 px under the centre of the screen box
        return card;
    }

    Control AssetsEmpty()
    {
        // Figma T-P22: an 18 x 14 folder (a 20 px icon) whose middle is 2 px lower than an 18 px icon's; the text stays put
        var icon = new Icon { Glyph = "folder", Size = 22.5, Margin = new Thickness(1, 0, 0, -4.5), HorizontalAlignment = HorizontalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted");
        var title = UI.Text("No assets yet", "Body/12 Semibold", "text/primary");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 10, 0, 0);
        var text = UI.Text("Import sprites, tilesets, fonts and sounds — or drag files here.", "Label/11", "text/muted");
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.Margin = new Thickness(0, 8, 0, 0);
        var import = new AgbButton { Content = "Import…", Glyph = "upload", Kind = ButtonKind.Secondary, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 9, 0, 0), Name = "ImportAssets" };
        import.Click += (_, _) => _ = _s.Windows.Open("T-D04", source: "AssetsEmptyImport");
        return new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 3), Children = { icon, title, text, import }, Name = "AssetsEmpty" };
    }

    void SelectPanel(PanelTab tab)
    {
        var id = (string)tab.Tag!;
        if (id == "T-P07") { _ = _s.Windows.Open(id, source: "DatabaseTab"); return; }
        foreach (var t in _panelTabs) t.IsSelected = t == tab;
        _bottomContent.Content = id == "T-P01:Assets" ? _assetsEmpty : _s.Windows.CreatePanel(id);
        if (id != "T-P01:Assets") _s.Windows.Requests.Add((id, tab.Name));
    }

    public IReadOnlyList<PanelTab> PanelTabs => _panelTabs;

    Border StatusBar()
    {
        var left = UI.Row(16, _ready, _projectStatus);
        var right = UI.Row(16, _romStatus);
        var dock = new DockPanel();
        SetDock(right, Dock.Right);
        dock.Children.AddRange([right, left]);
        var bar = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        bar.Height = 26;
        return bar;
    }

    // ---- data ---------------------------------------------------------------------------------------

    string? SceneName()
    {
        var p = _s.Project.Current;
        if (_s.Documents.Open.FirstOrDefault(d => d.Path?.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) == true) is { Path: { } path }) return Path.GetFileNameWithoutExtension(path);
        if (p is null) return null;
        var names = p.SceneFiles().Select(GameProject.AssetName).ToList();
        return names.Contains(p.Settings.LastScene) ? p.Settings.LastScene : names.FirstOrDefault();
    }

    /// <summary>Hierarchy, tabs, status bar and budget from the open project.</summary>
    public void Refresh()
    {
        var p = _s.Project.Current;
        var scene = SceneName() ?? ProjectCreator.StartScene;
        _sceneItem.Label = scene;
        SceneFile? file = null;
        if (p != null)
        {
            try { file = GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, scene + ".scene")); }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException) { }
        }
        _cameraItem.Meta = string.IsNullOrEmpty(file?.Camera.Follow) ? "fixed" : "follow";
        var contents = p is null ? null : ProjectContents.Of(p);
        var assets = contents?.Assets ?? 0;
        _projectStatus.Content = $"New project · {assets} {(assets == 1 ? "asset" : "assets")}";
        _ready.Classes.Set("warn", !_s.Status.ToolchainOk);
        var rom = _s.Status.Rom ?? (p is null ? null : LauncherService.BuildReportLine(p));
        _romStatus.Content = rom;
        _romStatus.IsVisible = !string.IsNullOrEmpty(rom);
        var report = p is null ? null : AgbStudio.Build.Pipeline.BuildReport.Load(p.BuildDir);
        var budget = report != null && report.Scenes.TryGetValue(scene, out var b) ? b : null;
        SetMeter(_vramBg, (budget?.VramBgBytes ?? 0) / 1024, 64, " KB");
        SetMeter(_vramObj, (budget?.VramObjBytes ?? 0) / 1024, 32, " KB");
        SetMeter(_oam, file?.Actors.Count ?? 0, 128, "");
        RebuildTabs();
    }

    static void SetMeter(Meter m, long used, long max, string unit)
    {
        m.ValueText = $"{used} / {max}{unit}";
        m.Percent = 100.0 * used / max;
        m.Status = used > max ? ProgressStatus.Danger : used * 10 > max * 8 ? ProgressStatus.Warn : ProgressStatus.OK;
    }

    void RebuildTabs()
    {
        _docTabs.Children.Clear();
        foreach (var d in _s.Documents.Open)
        {
            var tab = new DocTab { Label = d.Name, Glyph = d.Glyph, IsDirty = d.IsDirty, IsSelected = d == _s.Documents.Active, Height = 33, Name = "Doc_" + d.Name };
            var doc = d;
            tab.Activated += (_, _) =>
            {
                _s.Documents.Active = doc;
                if (doc.Name == "Game") _ = _s.Windows.Open("T-P02", doc, source: "Doc_Game");
            };
            _docTabs.Children.Add(tab);
        }
    }

    // ---- leaving the empty state ----------------------------------------------------------------------

    /// <summary>Add player: through the Scene Editor's Add Actor command when it is there, otherwise a Player actor is
    /// written into the scene; the scene is then no longer empty and the Scene Editor takes over.</summary>
    public void AddPlayer()
    {
        var cmd = _s.Commands.Get("scene.addActor.player");
        if (cmd.IsImplemented) { _s.Commands.Execute(cmd.Id); return; }
        if (_s.Project.Current is { } p && SceneName() is { } scene)
        {
            var path = Path.Combine(p.ScenesDir, scene + ".scene");
            var file = File.Exists(path) ? GameProject.Load<SceneFile>(path) : new SceneFile { Name = scene, Width = 15, Height = 10 };
            file.Actors.Add(new ActorData
            {
                Name = "Player", Kind = ActorKind.Player, X = 120, Y = 80, Tag = "Player",
                Components = { new ComponentData { Type = "PlatformerBody" } },
            });
            GameProject.Save(path, file);
        }
        _ = _s.Windows.Open("T-P01", source: "AddPlayer");
    }

    /// <summary>Paint tiles: the Paint tiles tool in the Scene Editor.</summary>
    public void PaintTiles()
    {
        SetTool(2);
        if (_s.Commands.Get("scene.tool.paint").IsImplemented) _s.Commands.Execute("scene.tool.paint");
        _ = _s.Windows.Open("T-P01", source: "PaintTiles");
    }

    void OnCommand(EditorCommand c)
    {
        if (!c.Id.StartsWith("scene.addActor.", StringComparison.Ordinal) || _s.Windows.Host?.CurrentPage != this) return;
        Dispatcher.UIThread.Post(() => _ = _s.Windows.Open("T-P01", source: c.Id));
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).Where(f => f != null
            && (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase))).Cast<string>().ToList();
        if (files is not { Count: > 0 }) return;
        _ = _s.Windows.Open("T-D04", files, source: "DropFiles");
        e.Handled = true;
    }

    /// <summary>Hands over to the Scene Editor when files appear in the project and it is no longer empty.</summary>
    public void CheckContents()
    {
        _checkQueued = false;
        if (_s.Windows.Host?.CurrentPage != this) return;
        if (_s.Project.Current is { } p && !ProjectContents.Of(p).IsEmpty) _ = _s.Windows.Open("T-P01", source: "ProjectHasContent");
        else Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
        _watcher?.Dispose();
        _watcher = null;
        if (_s.Project.Current is not { } p || !Directory.Exists(p.Root)) return;
        _watcher = new FileSystemWatcher(p.Root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName };
        FileSystemEventHandler changed = (_, _) =>
        {
            if (_checkQueued) return;
            _checkQueued = true;
            Dispatcher.UIThread.Post(CheckContents);
        };
        _watcher.Created += changed;
        _watcher.Changed += changed;
        _watcher.Renamed += (_, _) => changed(null!, null!);
        _watcher.EnableRaisingEvents = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watcher?.Dispose();
        _watcher = null;
    }
}

/// <summary>The GBA screen in the empty viewport: 240 x 160 at 300 % (720 x 480), dashed edge and the 16 px grid.</summary>
public sealed class ScreenFrame : Control
{
    public const double Scale = 3;

    public ScreenFrame()
    {
        Width = 240 * Scale;
        Height = 160 * Scale;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
    }

    public override void Render(DrawingContext ctx)
    {
        var white = this.TryFindResource("lit/white", ActualThemeVariant, out var lw) && lw is ISolidColorBrush wb ? wb.Color : Colors.White;
        var edge = this.TryFindResource("border/strong", ActualThemeVariant, out var e) ? e as IBrush : null;
        var w = Bounds.Width;
        var h = Bounds.Height;
        ctx.DrawRectangle(new SolidColorBrush(white, 0.017), null, new Rect(0, 0, w, h));
        var line = new SolidColorBrush(white, 0.025);
        var step = 16 * Scale;
        for (var x = step - 1; x < w - 1; x += step) ctx.DrawRectangle(line, null, new Rect(x, 1, 2, h - 2));
        for (var y = step - 1; y < h - 1; y += step) ctx.DrawRectangle(line, null, new Rect(1, y, w - 2, 2));
        if (edge != null) ctx.DrawRectangle(null, new Pen(edge, 1, new DashStyle([6, 5], 0)), new Rect(0.5, 0.5, w - 1, h - 1));
    }
}
