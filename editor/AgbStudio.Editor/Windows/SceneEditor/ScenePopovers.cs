using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// Scene Switcher (T-O01, 320 x 356; #8 breadcrumb): the project's scenes in story order (from the start scene along
/// links and menu targets) with a preview drawn like the game, their size or "menu", search, and New Scene….
/// </summary>
[EditorWindow("T-O01")]
public sealed class SceneSwitcher : ScenePopover
{
    readonly SceneEditorModel _m;
    readonly EditorServices _s;
    readonly StackPanel _rows = new() { Spacing = 4, Name = "SceneRows" };
    public SearchField Search { get; } = new() { Watermark = "Find scene…", Name = "SceneSearch" };

    public SceneSwitcher(WindowContext ctx) : this(ctx.Services) { }

    public SceneSwitcher(EditorServices s)
    {
        _s = s;
        _m = SceneEditorModel.Of(s);
        Name = "SceneSwitcher";
        var add = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Name = "NewSceneButton", Height = 30, Padding = new Thickness(20, 0), Margin = new Thickness(0, 0, 0, 7), HorizontalAlignment = HorizontalAlignment.Left };
        add.Content = UI.Row(6, new Icon { Glyph = "plus", Size = 13, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary"),
            UI.Text("New Scene…", "Body/12", "text/secondary"));
        add.Click += (_, _) => { Close(null); _ = _s.Windows.Open("T-D02", source: "SceneSwitcherNewScene"); };
        var body = new DockPanel();
        var search = new Border { Padding = new Thickness(9, 9, 9, 4), Child = Search };
        DockPanel.SetDock(search, Dock.Top);
        DockPanel.SetDock(add, Dock.Bottom);
        body.Children.AddRange([search, add, new ScrollViewer { Content = new Border { Padding = new Thickness(9, 0, 9, 0), Child = _rows } }]);
        Build("Switch scene", body, 320);
        FocusOnShow(Search);
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        Fill();
    }

    /// <summary>Scenes from the start scene along links and menu "load scene" items (breadth first), then the rest by name.</summary>
    public static List<string> StoryOrder(GameProject p)
    {
        var scenes = p.SceneFiles().ToDictionary(GameProject.AssetName, f => GameProject.Load<SceneFile>(f));
        var order = new List<string>();
        var queue = new Queue<string>();
        void Visit(string n) { if (scenes.ContainsKey(n) && !order.Contains(n)) { order.Add(n); queue.Enqueue(n); } }
        Visit(p.Settings.General.StartScene);
        while (queue.Count > 0)
        {
            var sc = scenes[queue.Dequeue()];
            foreach (var item in sc.Menu?.Items ?? []) if (item.Command == MenuCommand.LoadScene) Visit(item.Scene);
            foreach (var l in sc.Links) Visit(l.To);
        }
        foreach (var n in scenes.Keys.Order(StringComparer.Ordinal)) if (!order.Contains(n)) order.Add(n);
        return order;
    }

    public IReadOnlyList<string> Shown { get; private set; } = [];

    void Fill()
    {
        _rows.Children.Clear();
        if (_m.Project is not { } p) return;
        var q = Search.Text?.Trim() ?? "";
        Shown = StoryOrder(p).Where(n => q.Length == 0 || n.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var name in Shown)
        {
            var file = Path.Combine(p.ScenesDir, name + ".scene");
            var scene = GameProject.Load<SceneFile>(file);
            _rows.Children.Add(Row(name, scene, name == _m.SceneName));
        }
    }

    Control Row(string name, SceneFile scene, bool current)
    {
        var map = _m.Assets?.Map(scene);
        var thumb = new Border { Width = 48, Height = 32, CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = PixelArt.View(Preview(scene, map), 48, 32) };
        var meta = UI.Text(scene.Menu != null ? "menu" : WorldMap.WorldMapModel.IsBossScene(scene) ? "boss" : $"{map?.Width ?? scene.Width}×{map?.Height ?? scene.Height}",
            "Mono/10", current ? "text/on-selected" : "text/muted");
        meta.Name = "SceneMeta_" + name;
        // Figma: the current scene's name stays text/primary; only its size turns text/on-selected
        var label = UI.Text(name, "Body/12", "text/primary");
        var dock = new DockPanel { HorizontalSpacing = 11 };
        DockPanel.SetDock(thumb, Dock.Left);
        DockPanel.SetDock(meta, Dock.Right);
        dock.Children.AddRange([thumb, meta, label]);
        var b = new Button
        {
            Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Height = 44, Padding = new Thickness(7, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center,
            Content = dock, Name = "Scene_" + name, CornerRadius = new CornerRadius(7),
        };
        if (current) b.Token(TemplatedControl.BackgroundProperty, "accent/selected");
        b.Template = VideoModePopover.BorderedTemplate();
        b.Click += (_, _) => { _m.OpenScene(name); Close(name); };
        return b;
    }

    Avalonia.Media.Imaging.WriteableBitmap? Preview(SceneFile scene, MapFile? map)
    {
        if (_m.Assets is not { } a) return null;
        var (cx, cy) = GbaScreenRenderer.StartCamera(scene, map);
        var f = GbaScreenRenderer.Render(a, scene, map, cx, cy);
        // 240 x 160 down to 48 x 32: every fifth pixel
        var px = new uint[48 * 32];
        for (var y = 0; y < 32; y++)
            for (var x = 0; x < 48; x++) px[y * 48 + x] = ScreenFrame.ToArgb(f.Bgr[(y * 5 + 2) * 240 + x * 5 + 2]);
        return PixelArt.Bitmap(px, 48, 32);
    }
}

/// <summary>Video Mode (T-O02, 380 x 400; #12 video mode chip): Mode 0-3 of the open scene, alpha blending and mosaic.</summary>
[EditorWindow("T-O02")]
public sealed class VideoModePopover : ScenePopover
{
    public static readonly IReadOnlyList<(string Title, string Description)> Modes =
    [
        ("Mode 0", "4 tile layers · scroll · best for platformers & RPGs"), ("Mode 1", "2 tile + 1 rotate/scale layer"),
        ("Mode 2", "2 rotate/scale layers · Mode-7 racing"), ("Mode 3", "1 bitmap 240×160 · 16-bit colour"),
    ];

    readonly SceneEditorModel _m;
    readonly StackPanel _cards = new() { Spacing = 14, Name = "ModeCards" };
    public CheckBox Alpha { get; } = new() { Content = "Alpha blending (fog, shadows)", Name = "AlphaBlending" };
    public CheckBox Mosaic { get; } = new() { Content = "Mosaic effect", Name = "Mosaic" };

    public VideoModePopover(WindowContext ctx) : this(ctx.Services) { }

    public VideoModePopover(EditorServices s)
    {
        _m = SceneEditorModel.Of(s);
        Name = "VideoModePopover";
        var content = UI.Column(14, _cards, UI.Column(14, Alpha, Mosaic));
        content.Margin = new Thickness(20, 20, 20, 14);
        Build("Video Mode", content, 380);
        Alpha.IsCheckedChanged += (_, _) => { if (_m.Scene != null && Alpha.IsChecked != _m.Scene.AlphaBlending) _m.EditScene("Alpha Blending", (sc, _) => sc.AlphaBlending = Alpha.IsChecked == true); };
        Mosaic.IsCheckedChanged += (_, _) => { if (_m.Scene != null && Mosaic.IsChecked != _m.Scene.Mosaic) _m.EditScene("Mosaic", (sc, _) => sc.Mosaic = Mosaic.IsChecked == true); };
        Fill();
    }

    public int Selected => _m.Scene?.VideoMode ?? 0;

    public void Choose(int mode)
    {
        if (_m.Scene == null || mode == _m.Scene.VideoMode) return;
        _m.EditScene("Change Video Mode", (sc, _) => sc.VideoMode = mode);
        Fill();
    }

    void Fill()
    {
        _cards.Children.Clear();
        Alpha.IsChecked = _m.Scene?.AlphaBlending ?? false;
        Mosaic.IsChecked = _m.Scene?.Mosaic ?? false;
        for (var i = 0; i < Modes.Count; i++)
        {
            var sel = i == Selected;
            var icon = new Icon { Glyph = sel ? "check" : "dots", Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, sel ? "accent/default" : "text/muted");
            // Figma: the mode descriptions in text/muted
            var text = UI.Column(2, UI.Text(Modes[i].Title, "Body/12 Semibold", "text/primary"), UI.Text(Modes[i].Description, "Label/11", "text/muted"));
            text.VerticalAlignment = VerticalAlignment.Center;
            var row = UI.Row(10, icon, text);
            var card = new Button
            {
                Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Height = 54, Padding = new Thickness(13, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center,
                Content = row, Name = "Mode_" + i, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            };
            card.Token(TemplatedControl.BackgroundProperty, sel ? "accent/selected" : "bg/base").Token(TemplatedControl.BorderBrushProperty, sel ? "accent/default" : "border/default");
            card.Template = BorderedTemplate();
            var mode = i;
            card.Click += (_, _) => Choose(mode);
            _cards.Children.Add(card);
        }
    }

    /// <summary>A bare button that draws its background, border and corner radius (cards).</summary>
    internal static Avalonia.Controls.Templates.FuncControlTemplate BorderedTemplate() => new((b, _) =>
    {
        var btn = (Button)b;
        var border = new Border();
        border.Bind(Border.BackgroundProperty, btn.GetObservable(TemplatedControl.BackgroundProperty));
        border.Bind(Border.BorderBrushProperty, btn.GetObservable(TemplatedControl.BorderBrushProperty));
        border.Bind(Border.BorderThicknessProperty, btn.GetObservable(TemplatedControl.BorderThicknessProperty));
        border.Bind(Border.CornerRadiusProperty, btn.GetObservable(TemplatedControl.CornerRadiusProperty));
        border.Bind(Border.PaddingProperty, btn.GetObservable(TemplatedControl.PaddingProperty));
        var cp = new ContentPresenter { Name = "PART_ContentPresenter" };
        cp.Bind(ContentPresenter.ContentProperty, btn.GetObservable(ContentControl.ContentProperty));
        cp.Bind(ContentPresenter.HorizontalContentAlignmentProperty, btn.GetObservable(ContentControl.HorizontalContentAlignmentProperty));
        cp.Bind(ContentPresenter.VerticalContentAlignmentProperty, btn.GetObservable(ContentControl.VerticalContentAlignmentProperty));
        border.Child = cp;
        return border;
    });
}

/// <summary>Grid &amp; Snap (T-O03, 300 x 328; #19 grid chip): size 8 / 16 / 32, show grid, snap, tile collision, camera bounds, grid colour.</summary>
[EditorWindow("T-O03")]
public sealed class GridSnapPopover : ScenePopover
{
    readonly SceneEditorModel _m;
    readonly Segment[] _sizes;
    readonly Border[] _colours = new Border[3];
    public AgbSwitch ShowGrid { get; } = new() { Name = "ShowGrid" };
    public AgbSwitch Snap { get; } = new() { Name = "SnapToGrid" };
    public AgbSwitch TileCollision { get; } = new() { Name = "TileCollision" };
    public AgbSwitch CameraBounds { get; } = new() { Name = "CameraBounds" };

    public GridSnapPopover(WindowContext ctx) : this(ctx.Services) { }

    public GridSnapPopover(EditorServices s)
    {
        _m = SceneEditorModel.Of(s);
        Name = "GridSnapPopover";
        var g = _m.Grid;
        var group = "grid-size-" + Guid.NewGuid().ToString("N");
        _sizes = new[] { 8, 16, 32 }.Select(v => new Segment { Content = v.ToString(CultureInfo.InvariantCulture), GroupName = group, IsChecked = g.Size == v, Name = "Size_" + v, Tag = v }).ToArray();
        foreach (var seg in _sizes)
            seg.IsCheckedChanged += (_, _) => { if (seg.IsChecked == true) { g.Size = (int)seg.Tag!; _m.GridChanged(); } };
        var sizes = UI.Frame(UI.Row(2, _sizes), "bg/base", "border/default", null, new Thickness(2), 7);
        sizes.HorizontalAlignment = HorizontalAlignment.Left;
        ShowGrid.IsChecked = g.ShowGrid;
        Snap.IsChecked = g.Snap;
        TileCollision.IsChecked = g.TileCollision;
        // Preferences › Editor › "Show collision overlay in Scene" off: the overlay is not available here
        TileCollision.IsEnabled = _m.CollisionOverlayAllowed;
        if (!TileCollision.IsEnabled) ToolTip.SetTip(TileCollision, new TipContent("Turned off in Preferences › Editor"));
        CameraBounds.IsChecked = g.CameraBounds;
        ShowGrid.IsCheckedChanged += (_, _) => { g.ShowGrid = ShowGrid.IsChecked == true; _m.GridChanged(); };
        Snap.IsCheckedChanged += (_, _) => { g.Snap = Snap.IsChecked == true; _m.GridChanged(); };
        TileCollision.IsCheckedChanged += (_, _) => { g.TileCollision = TileCollision.IsChecked == true; _m.GridChanged(); };
        CameraBounds.IsCheckedChanged += (_, _) => { g.CameraBounds = CameraBounds.IsChecked == true; _m.GridChanged(); };
        var swatches = UI.Row(8);
        string[] tokens = ["lit/white", "accent/default", "status/danger"];
        for (var i = 0; i < 3; i++)
        {
            var k = i;
            // Figma: 22 px swatches with a 1 px ring, black when not chosen and white when chosen
            var fill = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4) }.Token(Border.BackgroundProperty, tokens[i]);
            var ring = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Child = fill, Name = "GridColour_" + i };
            _colours[i] = ring;
            var b = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = ring };
            b.Click += (_, _) => { g.Colour = k; _m.GridChanged(); Rings(); };
            swatches.Children.Add(b);
        }
        Rings();
        var rows = UI.Column(14,
            UI.Text("Grid size", "Overline/11", "text/secondary"), sizes,
            Line("Show grid", ShowGrid), Line("Snap to grid", Snap), Line("Tile collision", TileCollision), Line("Camera\nbounds", CameraBounds),
            Line("Grid colour", swatches));
        rows.Margin = new Thickness(20, 20, 20, 16);
        Build("Grid & Snap", rows, 300);
    }

    void Rings()
    {
        for (var i = 0; i < 3; i++) _colours[i].Token(Border.BorderBrushProperty, i == _m.Grid.Colour ? "lit/white" : "lit/black");
    }

    public void SelectSize(int size)
    {
        foreach (var s in _sizes) s.IsChecked = (int)s.Tag! == size;
    }

    static Control Line(string label, Control value)
    {
        var dock = new DockPanel { HorizontalSpacing = 8, Height = 24 };
        var t = UI.Text(label, "Body/12", "text/secondary", 86);
        // a two-line label ("Camera bounds") overflows the 24 px row evenly, as in Figma, instead of losing its second line
        if (label.Contains('\n'))
        {
            t.Height = (label.Count(c => c == '\n') + 1) * 16;
            t.Margin = new Thickness(0, -(t.Height - 24) / 2, 0, -(t.Height - 24) / 2);
        }
        DockPanel.SetDock(t, Dock.Left);
        value.HorizontalAlignment = HorizontalAlignment.Left;
        value.VerticalAlignment = VerticalAlignment.Center;
        dock.Children.AddRange([t, value]);
        return dock;
    }
}

/// <summary>
/// Asset Picker (T-O04, 440 x 368; #28 sheet dropdown, Add to Scene › Prefab…, Tilemap Editor › tileset dropdown):
/// search and type tabs Sprites / Tilesets / Fonts; closes with the chosen file's path. Args: the starting tab
/// ("Sprites", "Tilesets", "Fonts") or <see cref="PrefabMode"/>, which lists prefabs and adds the chosen one to the scene.
/// </summary>
[EditorWindow("T-O04")]
public sealed class AssetPicker : ScenePopover
{
    public const string PrefabMode = "Prefabs";
    static readonly (string Tab, AssetCategory Category, string Title)[] Tabs =
        [("Sprites", AssetCategory.Sprites, "Choose sprite sheet"), ("Tilesets", AssetCategory.Tilesets, "Choose tileset"), ("Fonts", AssetCategory.Fonts, "Choose font")];

    readonly SceneEditorModel _m;
    readonly WrapPanel _cards = new() { ItemSpacing = 10, LineSpacing = 10, Name = "PickerCards" };
    readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 6, Name = "PickerTabs" };
    readonly bool _prefabs;
    string _tab;
    public SearchField Search { get; } = new() { Watermark = "Search assets…", Name = "AssetSearch" };

    public AssetPicker(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public AssetPicker(EditorServices s, string? mode = null)
    {
        _m = SceneEditorModel.Of(s);
        Name = "AssetPicker";
        _prefabs = mode == PrefabMode;
        _tab = Tabs.Any(t => t.Tab == mode) ? mode! : "Sprites";
        var top = UI.Column(14, Search, _tabs);
        top.IsVisible = true;
        _tabs.IsVisible = !_prefabs;
        // more cards than fit scroll with the wheel; Figma draws no scroll bar over the cards
        var body = UI.Column(14, top, new ScrollViewer { Content = _cards, MaxHeight = 228, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden });
        body.Margin = new Thickness(20, 20, 20, 12);
        Build(_prefabs ? "Choose prefab" : Tabs.First(t => t.Tab == _tab).Title, body, 440);
        FocusOnShow(Search);
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        Fill();
    }

    public string Tab
    {
        get => _tab;
        set
        {
            _tab = value;
            if (Child is DockPanel d && d.Children.OfType<PopoverHeader>().FirstOrDefault() is { } h) h.Title = Tabs.First(t => t.Tab == value).Title;
            Fill();
        }
    }

    /// <summary>The paths listed now (search and tab applied).</summary>
    public IReadOnlyList<string> Items { get; private set; } = [];

    void Fill()
    {
        _tabs.Children.Clear();
        foreach (var (tab, _, _) in Tabs)
        {
            var chip = new Chip { Content = tab, Style = tab == _tab ? ChipStyle.Accent : ChipStyle.Default, Name = "Tab_" + tab };
            var t = tab;
            chip.Click += (_, _) => Tab = t;
            _tabs.Children.Add(chip);
        }
        _cards.Children.Clear();
        var q = Search.Text?.Trim() ?? "";
        if (_prefabs)
        {
            var dir = _m.Project is { } p ? SceneCommands.PrefabsDir(p) : null;
            Items = dir != null && Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*.prefab").Order(StringComparer.Ordinal).Where(f => Path.GetFileName(f).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList() : [];
            foreach (var f in Items)
            {
                var actor = SceneCommands.LoadPrefab(f);
                AddCard(f, Path.GetFileName(f), actor?.Sprite is { Length: > 0 } sp ? $"{actor.Kind} · {sp}.png" : actor?.Kind.ToString() ?? "", actor?.Sprite is { Length: > 0 } s2 ? PixelArt.SpriteBitmap(_m.Assets, s2) : null, "thumb/folder");
            }
            return;
        }
        var cat = Tabs.First(t => t.Tab == _tab).Category;
        var items = AssetActions.Of(_m).List(cat).Where(a => cat != AssetCategory.Tilesets || _m.Project?.TilesetFiles().Contains(a.Path) == true)
            .Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Items = items.Select(a => a.Path).ToList();
        var current = _m.SelectedActor?.Sprite;
        foreach (var a in items)
        {
            var bmp = a.Thumb == null ? cat == AssetCategory.Sprites ? PixelArt.SpriteBitmap(_m.Assets, a.Stem) : PixelArt.Png(a.Path) : null;
            AddCard(a.Path, a.Name, a.Meta.Replace(" frames", "f"), bmp, a.Thumb, cat == AssetCategory.Sprites && a.Stem == current, cat == AssetCategory.Sprites);
        }
    }

    void AddCard(string path, string name, string meta, Avalonia.Media.Imaging.Bitmap? bmp, string? thumb, bool selected = false, bool sprite = true)
    {
        var card = new AssetCard { NameText = name, Meta = meta, Preview = bmp == null ? thumb : null, IsSelected = selected, Name = "Pick_" + name };
        var holder = new Panel { Width = 102, Children = { card } };
        if (bmp != null)
        {
            var img = PixelArt.View(bmp, sprite ? 32 : 64, sprite ? 32 : 48);
            img.VerticalAlignment = VerticalAlignment.Top;
            img.Margin = new Thickness(0, Math.Max(0, (62 - img.Height) / 2), 0, 0);
            img.IsHitTestVisible = false;
            holder.Children.Add(img);
        }
        card.Activated += (_, _) => Pick(path);
        _cards.Children.Add(holder);
    }

    /// <summary>Chooses an item: closes with its path (prefab mode also adds the prefab to the scene).</summary>
    public void Pick(string path)
    {
        if (_prefabs && SceneCommands.LoadPrefab(path) is { } actor && _m.Scene != null)
        {
            var (x, y) = _m.SpawnPoint();
            actor.X = x;
            actor.Y = y;
            actor.Prefab = Path.GetFileNameWithoutExtension(path);
            _m.AddActor(actor, "Add Prefab");
        }
        Close(path);
    }
}

/// <summary>
/// Component Picker (T-O05, 400 x 464; #32 Add Component, Actor Context Menu › Add Behaviour…): built-in behaviours
/// by category and the project's C# behaviours, search, Up / Down / Enter, and New C# script… (T-D14). Adds the chosen
/// behaviour to the selected actor and closes with its name.
/// </summary>
[EditorWindow("T-O05")]
public sealed class ComponentPicker : ScenePopover
{
    readonly EditorServices _s;
    readonly SceneEditorModel _m;
    readonly StackPanel _list = new() { Spacing = 0, Name = "BehaviourList" };
    readonly List<(MenuRow Row, BehaviourDef Def)> _rows = [];
    int _highlight;
    public SearchField Search { get; } = new() { Watermark = "Search behaviours…", Name = "BehaviourSearch" };

    public ComponentPicker(WindowContext ctx) : this(ctx.Services) { }

    public ComponentPicker(EditorServices s)
    {
        _s = s;
        _m = SceneEditorModel.Of(s);
        Name = "ComponentPicker";
        var newScript = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Height = 30, Padding = new Thickness(19, 0), Name = "NewScriptButton" };
        newScript.Content = UI.Row(8, new Icon { Glyph = "code", Size = 13, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "kind/script"),
            UI.Text("New C# script…", "Body/12", "kind/script"));
        newScript.Click += (_, _) => { Close(null); _ = _s.Windows.Open("T-D14", source: "ComponentPickerNewScript"); };
        var dock = new DockPanel();
        var search = new Border { Padding = new Thickness(9, 9, 9, 0), Child = Search };
        newScript.Margin = new Thickness(0, 0, 0, 11);
        newScript.HorizontalAlignment = HorizontalAlignment.Left;
        DockPanel.SetDock(search, Dock.Top);
        DockPanel.SetDock(newScript, Dock.Bottom);
        dock.Children.AddRange([search, newScript, new ScrollViewer { Content = new Border { Padding = new Thickness(9, 0), Child = _list }, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden }]);
        Build("Add behaviour", dock, 400);
        FocusOnShow(Search);
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        Search.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        Fill();
    }

    public IReadOnlyList<BehaviourDef> Items => _rows.Select(r => r.Def).ToList();

    public int Highlight
    {
        get => _highlight;
        set
        {
            _highlight = _rows.Count == 0 ? -1 : Math.Clamp(value, 0, _rows.Count - 1);
            for (var i = 0; i < _rows.Count; i++) _rows[i].Row.IsSelected = i == _highlight;
        }
    }

    void Fill()
    {
        _list.Children.Clear();
        _rows.Clear();
        var q = Search.Text?.Trim() ?? "";
        var all = Behaviours.Builtins.Concat(Behaviours.Scripts(_m.Project, _s.Build.Scripts.Last))
            .Where(b => q.Length == 0 || b.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || b.Description.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var g in all.GroupBy(b => b.Category))
        {
            var header = UI.Row(6, new Icon { Glyph = Behaviours.CategoryGlyph(g.Key), Size = 12, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted"),
                UI.Text(g.Key, "Overline/11", "text/muted"));
            header.VerticalAlignment = VerticalAlignment.Top;
            // Figma: the category icon at x 18 and its label at 36, 1 px higher than the rows' 10 px inset
            _list.Children.Add(new Border { Height = 26, Padding = new Thickness(7, 9, 10, 0), Child = header });
            foreach (var b in g)
            {
                // the description in the Figma row is Label/11, not the shortcut style of the Menu Item
                var row = new MenuRow { Label = b.Title, Height = 30, Name = "Behaviour_" + b.Name };
                var desc = UI.Text(b.Description, "Label/11", "text/muted");
                desc.HorizontalAlignment = HorizontalAlignment.Right;
                desc.Margin = new Thickness(0, 0, 10, 0);
                desc.IsHitTestVisible = false;
                var def = b;
                row.Activated += (_, _) => Pick(def);
                row.PropertyChanged += (_, e) => { if (e.Property == SelectableRow.IsSelectedProperty) desc.Token(TextBlock.ForegroundProperty, row.IsSelected ? "text/on-selected" : "text/muted"); };
                _rows.Add((row, b));
                _list.Children.Add(new Panel { Children = { row, desc } });
            }
        }
        Highlight = 0;
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Highlight = _highlight + 1; e.Handled = true; break;
            case Key.Up: Highlight = _highlight - 1; e.Handled = true; break;
            case Key.Enter: if (_highlight >= 0) Pick(_rows[_highlight].Def); e.Handled = true; break;
        }
    }

    public void Pick(BehaviourDef b)
    {
        var i = _m.SelectedActorIndex;
        if (i >= 0) _m.AddComponent(i, b.Name);
        Close(b.Name);
    }
}

/// <summary>
/// Budget Details (T-O06, 420 x 398; #33 GBA Budget): the open scene's live budget (VRAM BG / OBJ, OAM, palettes, the
/// fullest OBJ palette, EWRAM, IWRAM, ROM), the palette-sharing hint and Open Build Report (T-P11).
/// </summary>
[EditorWindow("T-O06")]
public sealed class BudgetDetails : ScenePopover
{
    public SceneBudget Budget { get; }

    public BudgetDetails(WindowContext ctx) : this(ctx.Services) { }

    public BudgetDetails(EditorServices s)
    {
        var m = SceneEditorModel.Of(s);
        Name = "BudgetDetails";
        Budget = SceneBudget.For(m);
        var rows = UI.Column(8);
        rows.Name = "BudgetRows";
        foreach (var b in Budget.Meters)
        {
            // a fill thinner than the 6 px bar would only draw a dot: Figma shows ROM 412 KB of 32 MB as an empty track
            var meter = new Meter { Label = b.Label, ValueText = b.Text, Percent = b.Percent < 2 ? 0 : b.Percent, Status = b.Status, Name = "Meter_" + b.Label.Replace(' ', '_') };
            meter.TemplateApplied += (_, _) => WrapValue(meter);
            rows.Children.Add(meter);
        }
        // Figma: rows, tip and button 8 px apart; the tip in Label/11 (two lines make a 48 px box) beside a 16 px bulb
        var col = UI.Column(8, rows);
        if (Budget.Hint is { } hint)
        {
            var text = UI.Text(hint, "Label/11", "text/secondary");
            text.TextWrapping = TextWrapping.Wrap;
            var bulb = new Icon { Glyph = "bulb", Size = 16, VerticalAlignment = VerticalAlignment.Top }.Token(Icon.ForegroundProperty, "status/warning");
            var dock = new DockPanel { HorizontalSpacing = 10 };
            DockPanel.SetDock(bulb, Dock.Left);
            dock.Children.AddRange([bulb, text]);
            col.Children.Add(UI.Frame(dock, "bg/base", null, null, new Thickness(11, 10, 12, 10), 8).Named("PaletteHint"));
        }
        var open = new AgbButton { Content = "Open Build Report", Glyph = "chip", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Name = "OpenBuildReport" };
        open.Click += (_, _) => { Close(null); _ = s.Windows.Open("T-P11", source: "BudgetDetails"); };
        col.Children.Add(open);
        col.Margin = new Thickness(20, 20, 20, 20);
        Build($"GBA budget · {m.SceneName}", col, 420);
    }

    /// <summary>
    /// Figma wraps a long value ("15 / 16 colours", "412 KB / 32 MB") onto two lines that overflow the 18 px meter row
    /// evenly, instead of cutting it at the 78 px value column.
    /// </summary>
    static void WrapValue(Meter meter)
    {
        if (meter.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => DockPanel.GetDock(t) == Dock.Right) is not { } value) return;
        var text = meter.ValueText ?? "";
        var layout = new Avalonia.Media.TextFormatting.TextLayout(text, new Typeface(value.FontFamily, value.FontStyle, value.FontWeight), value.FontSize, null,
            TextAlignment.Right, TextWrapping.Wrap, maxWidth: value.Width, lineHeight: value.LineHeight, letterSpacing: value.LetterSpacing);
        if (layout.TextLines.Count < 2) return;
        // each line without the space it broke at, so both end at the right edge; the text may draw outside the row
        value.Text = string.Join("\n", layout.TextLines.Select(l => text.Substring(l.FirstTextSourceIndex, Math.Min(l.Length, text.Length - l.FirstTextSourceIndex)).Trim()));
        value.Height = layout.Height;
        value.Margin = new Thickness(0, -(layout.Height - meter.Height) / 2, 0, -(layout.Height - meter.Height) / 2);
        value.ClipToBounds = meter.ClipToBounds = false;
    }
}
