using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.SceneEditor;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Editor.Windows.UiEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace AgbStudio.Editor.Windows.WorldMap;

/// <summary>Arguments of Link Settings: the model and the link.</summary>
public sealed record LinkSettingsArgs(WorldMapModel Model, LinkRef Link);

/// <summary>
/// World Map (T-P13, 1440 x 900; Scene › World Map; P11.50-P11.51): the project's scenes as Scene Cards at their map
/// positions, links between them with chips (click for Link Settings), the start scene, New Scene, zoom and pan; the
/// Scenes list and the links of the selected scene; the Scene inspector (size, video mode, start scene, background
/// parallax, music, events). Right-click a card for the Scene Context Menu.
/// </summary>
[EditorWindow("T-P13")]
public sealed class WorldMapPage : DockPanel, IPageBreadcrumb
{
    readonly EditorServices _s;
    readonly StackPanel _sceneList = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 3), Name = "SceneList" };
    readonly StackPanel _linkList = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 6), Name = "LinkList" };
    readonly PanelHeader _linksHeader = new() { Title = "Links", Glyph = "link", Name = "LinksHeader" };
    readonly StackPanel _inspector = new() { Name = "SceneInspector" };
    readonly Chip _summary = new() { Glyph = "map", Name = "SummaryChip" };
    readonly StatusItem _start = new() { Glyph = "flag", Name = "Status_Start" };
    readonly StatusItem _zoom = new() { Glyph = "search", Name = "Status_Zoom" };
    readonly IconButton[] _tools;
    bool _loading;

    public WorldMapModel Model { get; }
    public WorldMapCanvas Canvas { get; }
    public string? Selected { get; private set; }

    /// <summary>The title bar shows "World Map" (the page is not a document).</summary>
    public string Breadcrumb => "World Map";

    public WorldMapPage(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public WorldMapPage(EditorServices s, string? select = null)
    {
        _s = s;
        Name = "WorldMapPage";
        Model = new WorldMapModel(s);
        Canvas = new WorldMapCanvas(this);

        _tools = [new IconButton { Glyph = "cursor", Name = "Tool_Select" }, new IconButton { Glyph = "hand", Name = "Tool_Hand" }, new IconButton { Glyph = "link", Name = "Tool_Link" }];
        for (var i = 0; i < _tools.Length; i++) { var k = i; _tools[i].Click += (_, _) => SetTool(k); }
        SetTool(0);
        var newScene = new AgbButton { Content = "New Scene", Glyph = "plus", Kind = ButtonKind.Secondary, Name = "NewScene" };
        newScene.Click += (_, _) => _ = NewSceneAsync(null);
        _summary.Click += (_, _) => Canvas.Fit();
        Children.Add(PageChrome.Toolbar([PageChrome.ToolGroup(_tools), newScene], PageChrome.PlayGroup(s), [_summary, PageChrome.UndoRedo(s)]));

        var ready = new StatusItem { Glyph = "check", IsDot = true, Content = "Ready", Name = "Status_Ready" };
        _start.Click += (_, _) => { if (Model.Find(Model.StartScene) is { } st) SelectScene(st.Name); };
        _zoom.Click += (_, _) => Canvas.SetZoom(1);
        Children.Add(PageChrome.StatusBar([ready, _start], [_zoom]));

        var scenesHeader = new PanelHeader { Title = "Scenes", Glyph = "map", ActionGlyph = "plus", Name = "ScenesHeader" };
        scenesHeader.ActionCommand = new Relay(() => _ = NewSceneAsync(null));
        var left = new DockPanel();
        PageChrome.DockTo(left, scenesHeader, Dock.Top);
        PageChrome.DockTo(left, _sceneList, Dock.Top);
        PageChrome.DockTo(left, _linksHeader, Dock.Top);
        left.Children.Add(new ScrollViewer { Content = _linkList });

        var right = new DockPanel();
        PageChrome.DockTo(right, new PanelHeader { Title = "Scene", Glyph = "sliders", Name = "SceneHeader" }, Dock.Top);
        right.Children.Add(new ScrollViewer { Content = _inspector });

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        body.Children.Add(PageChrome.SidePanel(left, true, "LeftPanel"));
        // Doc Tabs over the canvas, as on the other editor pages: back to the scene (or any open document) in one click
        var center = new DockPanel { Name = "CenterPanel" };
        PageChrome.DockTo(center, Tilemap.PageChrome.DocTabs(s, "World Map", "map"), Dock.Top);
        center.Children.Add(Canvas);
        Grid.SetColumn(center, 1);
        body.Children.Add(center);
        var rightPanel = PageChrome.SidePanel(right, false, "RightPanel");
        Grid.SetColumn(rightPanel, 2);
        body.Children.Add(rightPanel);
        Children.Add(body);

        Model.Changed += Refresh;
        Canvas.ZoomChanged += () => _zoom.Content = $"{Math.Round(Canvas.Zoom * 100)}%";
        AttachedToVisualTree += (_, _) =>
        {
            if (_s.Top is MainWindow w) w.Edit.DefaultTarget = Canvas;
        };
        DetachedFromVisualTree += (_, _) => { if (_s.Top is MainWindow w && w.Edit.DefaultTarget == Canvas) w.Edit.DefaultTarget = null; };
        Selected = select ?? Model.Scenes.FirstOrDefault(x => x.Name == s.Project.Current?.Settings.LastScene)?.Name ?? Model.Scenes.FirstOrDefault()?.Name;
        _zoom.Content = "100%";
        Refresh();
    }

    internal void SetTool(int i)
    {
        for (var k = 0; k < _tools.Length; k++) _tools[k].IsActive = k == i;
        Canvas.Tool = i;
    }

    public void SelectScene(string? name)
    {
        Selected = name;
        Refresh();
    }

    void Refresh()
    {
        if (Selected != null && Model.Find(Selected) is null) Selected = Model.Scenes.FirstOrDefault()?.Name;
        _summary.Content = Model.Summary;
        _start.Content = Model.StartScene.Length > 0 ? $"Start scene: {Model.StartScene}" : "No start scene";
        RebuildLists();
        RebuildInspector();
        Canvas.Rebuild();
    }

    void RebuildLists()
    {
        _sceneList.Children.Clear();
        foreach (var sc in Model.Scenes)
        {
            var item = new TreeItem { Label = sc.Name, Glyph = "map", Meta = WorldMapModel.Meta(sc), IsSelected = sc.Name == Selected, Name = "Scene_" + sc.Name };
            var name = sc.Name;
            item.Activated += (_, e) => { SelectScene(name); if (e.ClickCount >= 2) OpenScene(name); };
            _sceneList.Children.Add(item);
        }
        _linkList.Children.Clear();
        var sel = Selected is null ? null : Model.Find(Selected);
        _linksHeader.Title = sel is null ? "Links" : $"Links from {sel.Name}";
        if (sel is null) return;
        for (var i = 0; i < sel.Data.Links.Count; i++)
        {
            var l = sel.Data.Links[i];
            var item = new TreeItem { Label = $"{WorldMapModel.LinkLabel(l)} → {l.To}", Glyph = "link", Meta = WorldMapModel.KindText(l.Kind), Name = "Link_" + i };
            var r = new LinkRef(sel.Name, i);
            item.Activated += (_, _) => OpenLink(r);
            _linkList.Children.Add(item);
        }
    }

    // ---- inspector -------------------------------------------------------------------------------------------

    void RebuildInspector()
    {
        _loading = true;
        _inspector.Children.Clear();
        if (Selected is null || Model.Find(Selected) is not { } sc) { _loading = false; return; }
        var d = sc.Data;
        var size = PageChrome.Field("Size", FieldKind.Text, WorldMapModel.SizeText(d), "SceneSize");
        OnCommit(size, v =>
        {
            if (WorldMapModel.ParseSize(v) is { } wh && (wh.W != d.Width || wh.H != d.Height)) Model.Edit("Scene size", () => { d.Width = wh.W; d.Height = wh.H; Model.Save(sc); });
            else size.Value = WorldMapModel.SizeText(d);
        });
        var mode = PageChrome.Field("Video mode", FieldKind.Dropdown, WorldMapModel.VideoModes[Math.Clamp(d.VideoMode, 0, WorldMapModel.VideoModes.Length - 1)], "VideoMode")
            .Choices(() => WorldMapModel.VideoModes, v => { var m = Array.IndexOf(WorldMapModel.VideoModes, v); if (m >= 0 && m != d.VideoMode) Model.Edit("Video mode", () => { d.VideoMode = m; Model.Save(sc); }); });
        var start = PageChrome.Toggle(new InspectorField { Label = "Start scene", Name = "StartScene" }, Model.StartScene == sc.Name,
            on => { if (!_loading && on && Model.StartScene != sc.Name) Model.SetStart(sc.Name); });
        _inspector.Children.Add(Section(sc.Name, "map", WorldMapModel.TemplateMeta(sc), "Section_Scene", size, mode, start));

        var bgRows = new List<Control>();
        foreach (var layer in sc.Map?.Layers.Where(l => l.Type != LayerType.CollisionOnly) ?? [])
        {
            var ly = layer;
            var f = new InspectorField { Label = $"BG{layer.Bg} {layer.Name}", Kind = FieldKind.Slider, Value = WorldMapModel.Num(layer.ParallaxX), SliderValue = Math.Clamp(layer.ParallaxX, 0, 1) * 100, Name = "Parallax_BG" + layer.Bg };
            IDisposable? commit = null;
            f.PropertyChanged += (_, e) =>
            {
                if (e.Property != InspectorField.SliderValueProperty || _loading) return;
                var v = Math.Round(f.SliderValue / 100, 1);
                f.Value = WorldMapModel.Num(v);
                commit?.Dispose();
                commit = Avalonia.Threading.DispatcherTimer.RunOnce(() => SetParallax(sc, ly.Name, v), TimeSpan.FromMilliseconds(250));
            };
            bgRows.Add(f);
        }
        if (bgRows.Count > 0) _inspector.Children.Add(Section("Backgrounds", "layers", "parallax", "Section_Backgrounds", [.. bgRows]));

        var track = PageChrome.Field("Track", FieldKind.Dropdown, d.Music.Track.Length == 0 ? "None" : Model.TrackFile(d.Music.Track), "MusicTrack")
            .Choices(() => ["None", .. Model.MusicTracks()], v =>
            {
                var t = v == "None" ? "" : Path.GetFileNameWithoutExtension(v);
                if (t != d.Music.Track) Model.Edit("Music track", () => { d.Music.Track = t; Model.Save(sc); });
            });
        var volume = new InspectorField { Label = "Volume", Kind = FieldKind.Slider, Value = WorldMapModel.Num(d.Music.Volume), SliderValue = Math.Clamp(d.Music.Volume, 0, 1) * 100, Name = "MusicVolume" };
        IDisposable? volCommit = null;
        volume.PropertyChanged += (_, e) =>
        {
            if (e.Property != InspectorField.SliderValueProperty || _loading) return;
            var v = Math.Round(volume.SliderValue / 100, 1);
            volume.Value = WorldMapModel.Num(v);
            volCommit?.Dispose();
            volCommit = Avalonia.Threading.DispatcherTimer.RunOnce(() => { if (Math.Abs(v - d.Music.Volume) > 0.001) Model.Edit("Music volume", () => { d.Music.Volume = v; Model.Save(sc); }); }, TimeSpan.FromMilliseconds(250));
        };
        _inspector.Children.Add(Section("Music", "music", "", "Section_Music", track, volume));

        var onEnter = PageChrome.Field("OnEnter", FieldKind.Text, d.Events.OnEnter, "OnEnter");
        OnCommit(onEnter, v => { if (v != d.Events.OnEnter) Model.Edit("OnEnter", () => { d.Events.OnEnter = v; Model.Save(sc); }); });
        var onExit = PageChrome.Field("OnExit", FieldKind.Text, d.Events.OnExit, "OnExit");
        OnCommit(onExit, v => { if (v != d.Events.OnExit) Model.Edit("OnExit", () => { d.Events.OnExit = v; Model.Save(sc); }); });
        _inspector.Children.Add(Section("Events", "code", "", "Section_Events", onEnter, onExit));
        _inspector.Children.Add(ConnectionsSection(sc));
        _loading = false;
    }

    // Scenes joined edge to edge: one row per join with its offset, and a row that adds one. Choosing "Remove" takes a join away.
    StackPanel ConnectionsSection(MapScene sc)
    {
        const string Remove = "Remove";
        var joins = sc.Data.Connections ?? [];
        var rows = new List<Control>();
        for (var i = 0; i < joins.Count; i++)
        {
            var k = i;
            var c = joins[i];
            rows.Add(PageChrome.Field(c.Side.ToString(), FieldKind.Dropdown, c.Scene, "Connection_" + i)
                .Choices(() => [Remove], _ => { Model.RemoveConnection(sc, k); Refresh(); }));
            var offset = PageChrome.Field("Offset", FieldKind.Text, c.Offset.ToString(), "ConnectionOffset_" + i);
            OnCommit(offset, v =>
            {
                if (int.TryParse(v, out var n) && n != c.Offset) Model.SetConnectionOffset(sc, k, n);
                else offset.Value = c.Offset.ToString();
            });
            rows.Add(offset);
        }
        var others = Model.Scenes.Where(o => o != sc && joins.All(j => j.Scene != o.Name)).Select(o => o.Name).ToList();
        string[] choices = [.. others.SelectMany(n => Enum.GetNames<MapSide>().Select(side => $"{side}: {n}"))];
        rows.Add(PageChrome.Field("Connect", FieldKind.Dropdown, "none", "AddConnection").Choices(() => choices, v =>
        {
            var parts = v.Split(": ", 2);
            Model.AddConnection(sc, Enum.Parse<MapSide>(parts[0]), parts[1]);
            Refresh();
        }));
        return Section("Connections", "link", "", "Section_Connections", [.. rows]);
    }

    void SetParallax(MapScene sc, string layer, double v)
    {
        if (_s.Project.Current is not { } p || sc.Map is null) return;
        var path = Path.Combine(p.MapsDir, (sc.Data.Map.Length > 0 ? sc.Data.Map : sc.Name) + ".map");
        Model.Edit("Parallax", () =>
        {
            var map = GameProject.Load<MapFile>(path);
            foreach (var l in map.Layers.Where(l => l.Name == layer)) { l.ParallaxX = v; l.ParallaxY = v; }
            GameProject.Save(path, map);
        });
    }

    static void OnCommit(InspectorField f, Action<string> commit)
    {
        f.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextBox>("PART_Text") is not { } box) return;
            box.LostFocus += (_, _) => commit(f.Value ?? "");
            box.KeyDown += (_, ke) => { if (ke.Key == Key.Enter) { commit(f.Value ?? ""); ke.Handled = true; } };
        };
    }

    static StackPanel Section(string title, string glyph, string meta, string name, params Control[] rows) => ScriptEditorPage.Section(title, glyph, meta, name, rows);

    // ---- actions ---------------------------------------------------------------------------------------------

    /// <summary>New Scene (T-D02): after it closes, a new scene is placed at <paramref name="at"/> (else a free spot).</summary>
    public async Task NewSceneAsync(Point? at)
    {
        var before = Model.Scenes.Select(x => x.Name).ToHashSet();
        await _s.Windows.Open("T-D02", source: "WorldMapNewScene");
        Model.Reload();
        foreach (var added in Model.Scenes.Where(x => !before.Contains(x.Name) && x.Data.MapX == 0 && x.Data.MapY == 0).ToList())
        {
            var p = at ?? Canvas.FreeSpot();
            Model.Move(added, (int)p.X, (int)p.Y);
            Selected = added.Name;
        }
        Refresh();
    }

    public void OpenLink(LinkRef r) => _ = _s.Windows.Open("T-O09", new LinkSettingsArgs(Model, r), source: "LinkChip");

    /// <summary>Opens a scene in the Scene Editor (Open, double-click).</summary>
    public void OpenScene(string name)
    {
        if (Model.Find(name) is not { } sc || _s.Project.Current is not { } p) return;
        p.Settings.LastScene = name;
        _s.ProjectSettings.Save();
        _ = _s.Windows.Open("T-P01", sc.Path, source: "WorldMapOpen");
    }

    public void OpenSceneMenu(string name)
    {
        if (Canvas.CardOf(name) is not { } card) return;
        _ = _s.Windows.Open("T-M22", name, card, source: "SceneCard");
    }

    /// <summary>The map point the empty-space menu was opened at (New Scene Here puts the card's middle there).</summary>
    public Point MenuAt { get; private set; }

    /// <summary>Right-click on empty map space: the map menu (T-M22 with a point) at the mouse.</summary>
    public void OpenMapMenu(Point world)
    {
        MenuAt = world;
        _ = _s.Windows.Open("T-M22", world, Canvas, source: "WorldMapCanvas");
    }

    /// <summary>The menu on empty map space: add a scene where it was clicked, link, find the start, and the view.</summary>
    public static IReadOnlyList<MenuEntry> MapContextMenu() =>
    [
        new CommandEntry("worldmap.newHere", ShowIcon: true), new CommandEntry("worldmap.linkTool"), SeparatorEntry.Instance,
        new CommandEntry("worldmap.goStart"), new CommandEntry("worldmap.fit"), new CommandEntry("worldmap.zoom100"),
    ];

    /// <summary>
    /// The Scene Context Menu as Figma draws it; <paramref name="isStart"/> checks "Set as start scene". Enter and F2 are
    /// handled by the map canvas, so they are only shown on the items.
    /// </summary>
    public static IReadOnlyList<MenuEntry> SceneContextMenu(Func<bool>? isStart = null) =>
    [
        new CommandEntry("worldmap.open", ShowIcon: true, Keys: "Enter"), new CommandEntry("worldmap.rename", Keys: "F2"), new CommandEntry("edit.duplicate"),
        new CommandEntry("worldmap.setStart", Checkable: true, IsChecked: isStart), SeparatorEntry.Instance, new CommandEntry("edit.delete", Danger: true),
    ];

    /// <summary>T-M22 for one scene card (args: the scene name), or for empty map space (args: the map point).</summary>
    [EditorMenu("T-M22")]
    public static IReadOnlyList<MenuEntry> SceneContextMenu(WindowContext ctx) => ctx.Args is Point
        ? MapContextMenu()
        : SceneContextMenu(() => ctx.Args is string name && Current(ctx.Services)?.Model.StartScene == name);

    static WorldMapPage? Current(EditorServices s) => (s.Top as MainWindow)?.CurrentPage as WorldMapPage;

    [EditorCommand("worldmap.open", "Open", Category = "World Map", Glyph = "map", InPalette = false)]
    public static void OpenCommand(EditorServices s) { if (Current(s) is { Selected: { } n } p) p.OpenScene(n); }

    [EditorCommand("worldmap.rename", "Rename", Category = "World Map", Glyph = "pencil", InPalette = false)]
    public static void RenameCommand(EditorServices s) { if (Current(s) is { Selected: { } n } p) p.Canvas.BeginRename(n); }

    [EditorCommand("worldmap.setStart", "Set as start scene", Category = "World Map", Glyph = "flag", InPalette = false)]
    public static void SetStartCommand(EditorServices s) { if (Current(s) is { Selected: { } n } p) p.Model.SetStart(n); }

    [EditorCommand("worldmap.newHere", "New Scene Here…", Category = "World Map", Glyph = "plus", InPalette = false)]
    public static void NewHereCommand(EditorServices s)
    {
        if (Current(s) is { } p) _ = p.NewSceneAsync(new Point(p.MenuAt.X - WorldMapCanvas.CardW / 2, p.MenuAt.Y - WorldMapCanvas.CardH / 2));
    }

    [EditorCommand("worldmap.linkTool", "Link Scenes", Category = "World Map", Glyph = "link", InPalette = false)]
    public static void LinkToolCommand(EditorServices s) => Current(s)?.SetTool(2);

    [EditorCommand("worldmap.goStart", "Go to Start Scene", Category = "World Map", Glyph = "flag", InPalette = false)]
    public static void GoStartCommand(EditorServices s) { if (Current(s) is { } p && p.Model.Find(p.Model.StartScene) is { } st) p.SelectScene(st.Name); }

    [EditorCommand("worldmap.fit", "Fit All Scenes", Category = "World Map", Glyph = "search", InPalette = false)]
    public static void FitCommand(EditorServices s) => Current(s)?.Canvas.Fit();

    [EditorCommand("worldmap.zoom100", "Zoom to 100%", Category = "World Map", Glyph = "search", InPalette = false)]
    public static void Zoom100Command(EditorServices s) => Current(s)?.Canvas.SetZoom(1);
}

/// <summary>
/// The World Map canvas: dot grid, link curves (dashed for title-menu starts) with chips, Scene Cards; right-click on
/// empty space for the map menu; select, drag to move (saved as MapX / MapY), link tool to connect scenes, wheel zoom, hand / middle-button pan.
/// </summary>
public sealed class WorldMapCanvas : Panel, IEditTarget
{
    public const double CardW = 166, CardH = 146;
    readonly WorldMapPage _page;
    readonly Canvas _world = new() { Name = "MapWorld", IsHitTestVisible = false, RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute) };
    readonly Lines _lines;
    readonly Grid _grid;
    readonly Dictionary<string, SceneCard> _cards = [];
    readonly Dictionary<string, ((long, long) Stamp, IImage? Image)> _thumbs = [];
    (GameProject Project, SceneAssets Assets)? _assets;
    readonly List<(Rect Rect, LinkRef? Link, string To)> _chips = [];
    double _zoom = 1;
    Vector _pan;
    enum Drag { None, Card, Pan, Link }
    Drag _drag;
    Point _start, _linkEnd;
    Vector _panStart;
    MapScene? _dragScene;
    (int X, int Y) _cardStart;
    TextBox? _rename;

    public int Tool { get; set; }
    public double Zoom => _zoom;
    public event Action? ZoomChanged;
    WorldMapModel Model => _page.Model;

    public WorldMapCanvas(WorldMapPage page)
    {
        _page = page;
        Name = "MapCanvas";
        Focusable = true;
        ClipToBounds = true;
        this.Token(BackgroundProperty, "bg/canvas");
        _grid = new Grid(this) { IsHitTestVisible = false };
        _lines = new Lines(this);
        Children.Add(_grid);
        Children.Add(_world);
        Apply();
    }

    void Apply()
    {
        _world.RenderTransform = new MatrixTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_pan));
        _grid.InvalidateVisual();
        ZoomChanged?.Invoke();
    }

    public Point ToWorld(Point p) => new((p.X - _pan.X) / _zoom, (p.Y - _pan.Y) / _zoom);

    public void SetZoom(double z, Point? around = null)
    {
        z = Math.Clamp(z, 0.25, 2);
        var c = around ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var w = ToWorld(c);
        _zoom = z;
        _pan = new Vector(c.X - w.X * z, c.Y - w.Y * z);
        Apply();
    }

    public void SetView(double zoom, Vector pan) { _zoom = zoom; _pan = pan; Apply(); }

    public void Fit()
    {
        if (Model.Scenes.Count == 0 || Bounds.Width <= 0) return;
        var r = Model.Scenes.Select(Rect).Aggregate((a, b) => a.Union(b));
        _zoom = Math.Clamp(Math.Min((Bounds.Width - 80) / r.Width, (Bounds.Height - 80) / r.Height), 0.25, 1.5);
        _pan = new Vector(40 - r.X * _zoom, 40 - r.Y * _zoom);
        Apply();
    }

    static Rect Rect(MapScene s) => new(s.Data.MapX, s.Data.MapY, CardW, CardH);

    /// <summary>Where a new scene goes when no point was picked: under the lowest card, at the left edge of the map.</summary>
    public Point FreeSpot() => Model.Scenes.Count == 0 ? new Point(40, 40) : new Point(Model.Scenes.Min(s => s.Data.MapX), Model.Scenes.Max(s => s.Data.MapY) + CardH + 48);

    public SceneCard? CardOf(string name) => _cards.GetValueOrDefault(name);

    /// <summary>Link chips in map coordinates (clicks open Link Settings).</summary>
    public IReadOnlyList<(Rect Rect, LinkRef? Link, string To)> Chips => _chips;

    /// <summary>The picture of a card: the scene at its start camera, with its menu on top (its UI page, else its Menu items).</summary>
    public static uint[] ThumbnailPixels(GameProject p, SceneAssets assets, MapScene s)
    {
        var (x, y) = GbaScreenRenderer.StartCamera(s.Data, s.Map);
        var scene = GbaScreenRenderer.Render(assets, s.Data, s.Map, x, y).ToArgb();
        var page = MenuPage(p, s.Data);
        if (page == null) return scene;
        if (page.Dim > 0)
            for (var i = 0; i < scene.Length; i++)
            {
                var c = scene[i];
                uint Dark(int shift) => ((c >> shift) & 0xFF) * (uint)(16 - page.Dim) / 16 << shift;
                scene[i] = 0xFF000000u | Dark(16) | Dark(8) | Dark(0);
            }
        var selectable = page.Root.Walk().Where(e => e.IsSelectable).ToList();
        var focus = selectable.FirstOrDefault(e => e.Name == page.FirstFocus) ?? selectable.FirstOrDefault();
        return new UiPageRenderer(p).Render(page, focus, scene);
    }

    static UiPageFile? MenuPage(GameProject p, SceneFile scene)
    {
        var path = System.IO.Path.Combine(p.UiDir, scene.Page + ".uipage");
        if (scene.Page.Length > 0 && File.Exists(path)) return GameProject.Load<UiPageFile>(path);
        if (scene.Menu is not { } menu) return null;
        var page = new UiPageFile();
        for (var i = 0; i < menu.Items.Count; i++)
            page.Root.Children.Add(new UiElement { Kind = UiElementKind.Text, Name = "Item" + i, Text = menu.Items[i].Text, TextColor = UiPresets.MenuInk, Rect = UiRect.TopLeft(menu.X, menu.Y + i * menu.Spacing, menu.Width, menu.Spacing) });
        return page;
    }

    /// <summary>The scene as the ROM draws it at its start camera; redrawn only when its scene or map file changed. Null when it cannot be drawn.</summary>
    IImage? Thumbnail(MapScene s)
    {
        if (Model.Project is not { } p) return null;
        var mapPath = System.IO.Path.Combine(p.MapsDir, (s.Data.Map.Length > 0 ? s.Data.Map : s.Name) + ".map");
        var pagePath = System.IO.Path.Combine(p.UiDir, s.Data.Page + ".uipage");
        var stamp = (File.GetLastWriteTimeUtc(s.Path).Ticks, File.GetLastWriteTimeUtc(mapPath).Ticks ^ File.GetLastWriteTimeUtc(pagePath).Ticks);
        if (_thumbs.TryGetValue(s.Path, out var hit) && hit.Stamp == stamp) return hit.Image;
        IImage? image = null;
        try
        {
            var assets = _assets is { Project: var owner } && owner == p ? _assets.Value.Assets : new SceneAssets(p);
            _assets = (p, assets);
            image = PixelArt.Bitmap(ThumbnailPixels(p, assets, s), ScreenFrame.Width, ScreenFrame.Height);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { }
        _thumbs[s.Path] = (stamp, image);
        return image;
    }

    public void Rebuild()
    {
        _world.Children.Clear();
        _cards.Clear();
        _world.Children.Add(_lines);
        foreach (var s in Model.Scenes)
        {
            var card = new SceneCard { NameText = s.Name, Meta = WorldMapModel.Meta(s), Preview = null, PreviewImage = Thumbnail(s), IsSelected = s.Name == _page.Selected, Name = "Card_" + s.Name };
            Avalonia.Controls.Canvas.SetLeft(card, s.Data.MapX);
            Avalonia.Controls.Canvas.SetTop(card, s.Data.MapY);
            _world.Children.Add(card);
            _cards[s.Name] = card;
        }
        // chips over the lines and cards
        _chips.Clear();
        foreach (var (from, to, label, glyph, dashed, link) in Model.Connections())
        {
            var (a, b, c1, c2) = Curve(from, to);
            var chip = new Chip { Content = label, Glyph = glyph, Style = ChipStyle.Accent, Name = "Chip_" + label.Replace(' ', '_'), Tag = link };
            var (cw, ch) = ChipSize(label);
            // on its own line, a little way from the scene it leaves, so each chip clearly belongs to one link
            var at = Bezier(a, c1, c2, b, dashed ? 0.3 : 0.4);
            var (x, y) = (at.X - cw / 2, at.Y - ch / 2);
            Avalonia.Controls.Canvas.SetLeft(chip, x);
            Avalonia.Controls.Canvas.SetTop(chip, y);
            _world.Children.Add(chip);
            _chips.Add((new Rect(x, y, cw, ch), link, to.Name));
        }
        _lines.InvalidateVisual();
    }

    enum Side { Left, Right, Top, Bottom }

    /// <summary>The card sides a link leaves from and arrives at: sideways when the cards are mostly side by side, top /
    /// bottom when mostly above each other, else from the side to the top or bottom.</summary>
    static (Side A, Side B) Sides(MapScene from, MapScene to)
    {
        var dx = Rect(to).Center.X - Rect(from).Center.X;
        var dy = Rect(to).Center.Y - Rect(from).Center.Y;
        if (Math.Abs(dx) >= Math.Abs(dy) * 1.2) return dx > 0 ? (Side.Right, Side.Left) : (Side.Left, Side.Right);
        if (Math.Abs(dy) >= Math.Abs(dx) * 1.2) return dy > 0 ? (Side.Bottom, Side.Top) : (Side.Top, Side.Bottom);
        return (dx > 0 ? Side.Right : Side.Left, dy > 0 ? Side.Top : Side.Bottom);
    }

    /// <summary>Where a link meets a card: every link end on one side of a card gets its own point, spread evenly
    /// along that side and ordered by where the other card is, so the lines start on the card and do not cross.</summary>
    Point Port(MapScene scene, MapScene other)
    {
        var ends = new List<(MapScene Other, Side Side)>();
        foreach (var (f, t, _, _, _, _) in Model.Connections())
        {
            var (sa, sb) = Sides(f, t);
            if (f == scene) ends.Add((t, sa));
            if (t == scene) ends.Add((f, sb));
        }
        var side = ends.First(e => e.Other == other).Side;
        var vertical = side is Side.Left or Side.Right;
        var same = ends.Where(e => e.Side == side).OrderBy(e => vertical ? Rect(e.Other).Center.Y : Rect(e.Other).Center.X)
            .ThenBy(e => e.Other.Name, StringComparer.Ordinal).Select(e => e.Other).ToList();
        var r = Rect(scene);
        var i = same.IndexOf(other);
        // keep clear of the rounded corners: 24 px from each end
        const double inset = 24;
        double Along(double length) => inset + (length - 2 * inset) * (i + 1) / (same.Count + 1);
        return side switch
        {
            Side.Left => new Point(r.Left, r.Top + Along(r.Height)),
            Side.Right => new Point(r.Right, r.Top + Along(r.Height)),
            Side.Top => new Point(r.Left + Along(r.Width), r.Top),
            _ => new Point(r.Left + Along(r.Width), r.Bottom),
        };
    }

    /// <summary>Anchor points and control points of the curve between two cards; each end leaves its card straight out
    /// of the side it is on.</summary>
    internal (Point A, Point B, Point C1, Point C2) Curve(MapScene from, MapScene to)
    {
        var (sa, sb) = Sides(from, to);
        var a = Port(from, to);
        var b = Port(to, from);
        var k = Math.Max(40, Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) * 0.5);
        static Vector Out(Side s) => s switch { Side.Left => new(-1, 0), Side.Right => new(1, 0), Side.Top => new(0, -1), _ => new(0, 1) };
        return (a, b, a + Out(sa) * k, b + Out(sb) * k);
    }

    static Point Bezier(Point a, Point c1, Point c2, Point b, double t)
    {
        var u = 1 - t;
        return new Point(u * u * u * a.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * b.X, u * u * u * a.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * b.Y);
    }

    /// <summary>Size of an Accent Chip with an icon (padding 9, icon 12, gap 6, Label/11 text, 24 high), known before it is laid out.</summary>
    static (double W, double H) ChipSize(string label)
    {
        var style = AgbStudio.Editor.Theme.TextStyles.Get("Label/11");
        var text = new TextLayout(label, new Typeface(style.FontFamily, FontStyle.Normal, style.FontWeight), style.Size, null);
        return (Math.Ceiling(9 + 12 + 6 + text.Width + 9), 24);
    }

    /// <summary>The point of the curve at a given x (the curves here are monotonic in x).</summary>
    static Point AtX(Point a, Point c1, Point c2, Point b, double x)
    {
        double lo = 0, hi = 1;
        var increasing = b.X > a.X;
        for (var i = 0; i < 30; i++)
        {
            var mid = (lo + hi) / 2;
            var px = Bezier(a, c1, c2, b, mid).X;
            if (px < x == increasing) lo = mid; else hi = mid;
        }
        return Bezier(a, c1, c2, b, (lo + hi) / 2);
    }

    MapScene? SceneAt(Point w) => Model.Scenes.LastOrDefault(s => Rect(s).Contains(w));

    // ---- pointer ---------------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Source is Visual v && v != this) return;
        Focus();
        CommitRename();
        var p = e.GetPosition(this);
        var w = ToWorld(p);
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed)
        {
            if (SceneAt(w) is { } hit) { _page.SelectScene(hit.Name); _page.OpenSceneMenu(hit.Name); }
            else _page.OpenMapMenu(w);
            e.Handled = true;
            return;
        }
        if (props.IsMiddleButtonPressed || Tool == 1 && props.IsLeftButtonPressed) { Begin(Drag.Pan, p, e); return; }
        if (!props.IsLeftButtonPressed) return;
        var chip = _chips.LastOrDefault(c => c.Rect.Contains(w));
        if (chip.Link is { } link) { _page.SelectScene(link.Scene); _page.OpenLink(link); e.Handled = true; return; }
        if (chip.To != null && chip.Link is null) { _page.SelectScene(chip.To); e.Handled = true; return; }
        if (SceneAt(w) is { } scene)
        {
            _page.SelectScene(scene.Name);
            if (e.ClickCount >= 2) { _page.OpenScene(scene.Name); e.Handled = true; return; }
            _dragScene = scene;
            _cardStart = (scene.Data.MapX, scene.Data.MapY);
            if (Tool == 2) { _linkEnd = w; Begin(Drag.Link, p, e); }
            else Begin(Drag.Card, p, e);
            return;
        }
        Begin(Drag.Pan, p, e);
    }

    void Begin(Drag d, Point p, PointerPressedEventArgs e)
    {
        _drag = d;
        _start = p;
        _panStart = _pan;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        switch (_drag)
        {
            case Drag.Pan:
                _pan = _panStart + (p - _start);
                Apply();
                break;
            case Drag.Card when _dragScene != null:
                var d = (p - _start) / _zoom;
                _dragScene.Data.MapX = (int)Math.Round((_cardStart.X + d.X) / 10) * 10;
                _dragScene.Data.MapY = (int)Math.Round((_cardStart.Y + d.Y) / 10) * 10;
                if (_cards.TryGetValue(_dragScene.Name, out var card))
                {
                    Avalonia.Controls.Canvas.SetLeft(card, _dragScene.Data.MapX);
                    Avalonia.Controls.Canvas.SetTop(card, _dragScene.Data.MapY);
                }
                _lines.InvalidateVisual();
                break;
            case Drag.Link:
                _linkEnd = ToWorld(p);
                _lines.InvalidateVisual();
                break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var drag = _drag;
        _drag = Drag.None;
        e.Pointer.Capture(null);
        if (drag == Drag.Card && _dragScene is { } s && (s.Data.MapX, s.Data.MapY) != _cardStart)
        {
            var (x, y) = (s.Data.MapX, s.Data.MapY);
            (s.Data.MapX, s.Data.MapY) = _cardStart;
            Model.Move(s, x, y);
        }
        else if (drag == Drag.Link && _dragScene is { } from && SceneAt(_linkEnd) is { } to && to != from)
        {
            var r = Model.AddLink(from, to);
            _page.OpenLink(r);
        }
        _dragScene = null;
        _lines.InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        SetZoom(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _page.Selected is not { } sel) return;
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None) { _page.OpenScene(sel); e.Handled = true; }
        else if (e.Key == Key.F2) { BeginRename(sel); e.Handled = true; }
    }

    /// <summary>Rename: a text box over the card name; Enter renames, Esc cancels.</summary>
    public TextBox? BeginRename(string scene)
    {
        if (Model.Find(scene) is not { } s) return null;
        CommitRename();
        var box = _rename = new TextBox { Text = s.Name, Width = CardW - 16, Height = 26, Name = "RenameBox", Tag = s.Name };
        AgbStudio.Editor.Theme.Fig.SetStyle(box, "Body/12 Semibold");
        box.Padding = new Thickness(6, 0);
        var at = new Point(s.Data.MapX * _zoom + _pan.X + 8, (s.Data.MapY + 110) * _zoom + _pan.Y);
        box.Margin = new Thickness(at.X, at.Y, 0, 0);
        box.HorizontalAlignment = HorizontalAlignment.Left;
        box.VerticalAlignment = VerticalAlignment.Top;
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CommitRename(); e.Handled = true; }
            else if (e.Key == Key.Escape) { CancelRename(); e.Handled = true; }
        };
        Children.Add(box);
        box.Focus();
        box.SelectAll();
        return box;
    }

    public void CommitRename()
    {
        if (_rename is not { } box) return;
        _rename = null;
        Children.Remove(box);
        if (Model.Find((string)box.Tag!) is { } s && Model.Rename(s, box.Text ?? "")) _page.SelectScene(box.Text!.Trim());
    }

    void CancelRename()
    {
        if (_rename is { } box) Children.Remove(box);
        _rename = null;
    }

    // ---- Edit menu ---------------------------------------------------------------------------------------------

    public bool CanCopy => _page.Selected != null;
    public bool CanPaste => false;
    public void Cut() { }
    public void Copy() { }
    public void Paste() { }
    public void Duplicate() { if (_page.Selected is { } n && Model.Find(n) is { } s) _page.SelectScene(Model.Duplicate(s)); }
    public void Delete() { if (_page.Selected is { } n && Model.Find(n) is { } s) Model.Delete(s); }
    public void SelectAll() { }

    // ---- layers ------------------------------------------------------------------------------------------------

    sealed class Grid(WorldMapCanvas c) : Control
    {
        public override void Render(DrawingContext ctx)
        {
            // Figma: a 2 px dot in the middle of every 24 px cell
            var brush = ThemeService.Token(this, "border/default");
            var step = 24 * c._zoom;
            if (brush is null || step < 6) return;
            var ox = ((c._pan.X + step / 2) % step + step) % step;
            var oy = ((c._pan.Y + step / 2) % step + step) % step;
            for (var y = oy; y < Bounds.Height; y += step)
                for (var x = ox; x < Bounds.Width; x += step)
                    ctx.FillRectangle(brush, new Rect(Math.Round(x), Math.Round(y), 2, 2));
        }
    }

    sealed class Lines(WorldMapCanvas c) : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var brush = ThemeService.Token(this, "accent/default");
            foreach (var (from, to, _, _, dashed, _) in c.Model.Connections())
            {
                var (a, b, c1, c2) = c.Curve(from, to);
                var g = new StreamGeometry();
                using (var s = g.Open())
                {
                    s.BeginFigure(a, false);
                    s.CubicBezierTo(c1, c2, b);
                    s.EndFigure(false);
                }
                ctx.DrawGeometry(null, new Pen(brush, 2, dashed ? new DashStyle([2.5, 2.5], 0) : null), g);
            }
            if (c._drag == Drag.Link && c._dragScene is { } d)
                ctx.DrawLine(new Pen(brush, 2, new DashStyle([2, 2], 0)), new Rect(d.Data.MapX, d.Data.MapY, CardW, CardH).Center, c._linkEnd);
        }
    }
}
