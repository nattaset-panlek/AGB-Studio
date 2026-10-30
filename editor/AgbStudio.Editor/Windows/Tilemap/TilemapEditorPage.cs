using System.Globalization;
using AgbStudio.Core.Project;

using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>
/// Tilemap Editor (T-P03, 1440 x 900; from the Forest_01.map tab, Scene › Background Layers…, Create › Tileset /
/// Tilemap). Edits the real .map file and its tileset properties with undo: tools, layer toggles, collision view,
/// tileset panel (tileset, brush tiles, tile properties), the map viewport, the layers table / auto-tile rules, the
/// Map panel (size, BG size, colours, wrap, brush stamp / random / flip, collision) and the GBA Budget.
/// </summary>
[EditorWindow("T-P03")]
public sealed class TilemapEditorPage : DockPanel
{
    readonly EditorServices _svc;
    readonly IconButton[] _tools;
    readonly LayerToggle[] _bgToggles;
    readonly LayerToggle _objToggle;
    readonly Chip _collisionChip;
    readonly List<Action> _refresh = [];

    public TilemapSession? Session { get; }
    public TilemapCanvas? Canvas { get; }
    public TilemapBottomPanel? Bottom { get; }
    public TileSwatch? Swatch { get; }

    public static readonly string[] ToolGlyphs = ["cursor", "pencil", "square", "bucket", "eraser", "pipette"];
    public static readonly string[] ToolTitles = ["Select", "Brush", "Rectangle", "Fill", "Eraser", "Pick"];
    static readonly string[] BgSizes = ["256 × 256 (32×32)", "512 × 256 (64×32)", "256 × 512 (32×64)", "512 × 512 (64×64)"];
    static readonly string[] Colours = ["4bpp · 16 per tile", "8bpp · 256 colours"];
    static readonly string[] AnimationChoices = ["none", "2 frames", "3 frames", "4 frames"];
    static readonly (CollisionType Type, string Text)[] CollisionChoices =
        [(CollisionType.Solid, "Solid"), (CollisionType.OneWay, "One-way"), (CollisionType.Ladder, "Ladder"), (CollisionType.Hazard, "Hazard"), (CollisionType.Water, "Water"), (CollisionType.None, "None")];

    public TilemapEditorPage(WindowContext ctx) : this(ctx.Services, MapLibrary.Of(ctx.Services).Current(ctx.Args)) { }

    public TilemapEditorPage(EditorServices s, TilemapSession? session)
    {
        _svc = s;
        Session = session;
        Name = "TilemapEditor";
        LastChildFill = true;

        // ---- toolbar ----
        _tools = ToolGlyphs.Select((g, i) => new IconButton { Glyph = g, Name = "Tool_" + g }).ToArray();
        for (var i = 0; i < _tools.Length; i++)
        {
            var k = i;
            ToolTip.SetTip(_tools[i], new TipContent(ToolTitles[i]));
            _tools[i].Click += (_, _) => { if (Session != null) Session.Tool = (TileTool)k; };
        }
        _bgToggles = Enumerable.Range(0, 4).Select(i => new LayerToggle { Content = "BG" + i, Name = "Layer_BG" + i }).ToArray();
        for (var i = 0; i < 4; i++)
        {
            var bg = i;
            _bgToggles[i].Click += (_, _) =>
            {
                if (Session is null) return;
                var li = Session.LayerOnBg(bg);
                if (li >= 0) Session.SetVisible(li, _bgToggles[bg].IsOn);
                else _bgToggles[bg].IsOn = false;
            };
        }
        _objToggle = new LayerToggle { Content = "OBJ", Name = "Layer_OBJ" };
        _objToggle.Click += (_, _) => { if (Session != null) Session.ObjVisible = _objToggle.IsOn; };
        _collisionChip = new Chip { Content = "Collision view", Glyph = "grid", Name = "CollisionViewChip" };
        _collisionChip.Click += (_, _) => { if (Session != null) Session.CollisionView = !Session.CollisionView; };
        Children.Add(PageChrome.Toolbar(s, [PageChrome.ToolGroup(_tools), PageChrome.ToolGroup([.. _bgToggles, _objToggle]), _collisionChip], session?.Doc.Scene?.VideoMode ?? 0));

        // ---- status bar ----
        var hover = new StatusItem { Glyph = "grid", Name = "Status_Hover" };
        var autoTile = new StatusItem { Glyph = "spark", Name = "Status_AutoTile" };
        autoTile.Click += (_, _) => Session?.SetAutoTile(!Session.AutoTileOn);
        var saved = new StatusItem { Glyph = "check", Content = "Saved", Name = "Status_Saved" };
        Children.Add(PageChrome.StatusBar([PageChrome.ReadyItem(s.Status), hover], [autoTile, saved]));
        _refresh.Add(() =>
        {
            hover.Content = Session?.HoverText ?? LastHover;
            hover.IsVisible = hover.Content != null;
            autoTile.Content = Session is null ? null : Session.AutoTileOn ? "Auto-tile on" : "Auto-tile off";
            autoTile.IsVisible = Session != null;
            saved.IsVisible = Session is { Doc.IsDirty: false };
        });

        // ---- body ----
        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        body.Children.Add(LeftPanel());
        var right = RightPanel();
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        var center = new Grid { RowDefinitions = RowDefinitions.Parse("34,*,250"), Name = "Center" };
        Grid.SetColumn(center, 1);
        center.Children.Add(PageChrome.DocTabs(s));
        if (session != null)
        {
            Canvas = new TilemapCanvas(session);
            var view = new Panel { Name = "Viewport", Children = { Canvas, ViewportChips(session) } };
            view.Token(Panel.BackgroundProperty, "bg/canvas");
            Grid.SetRow(view, 1);
            center.Children.Add(view);
            Bottom = new TilemapBottomPanel(session, s);
            Grid.SetRow(Bottom, 2);
            center.Children.Add(Bottom);
            Swatch = _swatch;
            void OnDoc(MapChange _) => Refresh();
            PageChrome.WhileAttached(this, () =>
            {
                session.Doc.Changed += OnDoc;
                session.Doc.DirtyChanged += Refresh;
                session.ViewChanged += Refresh;
                s.System.Selection = () => session.Doc.Path;
                Refresh();
            }, () =>
            {
                session.Doc.Changed -= OnDoc;
                session.Doc.DirtyChanged -= Refresh;
                session.ViewChanged -= Refresh;
            });
        }
        body.Children.Add(center);
        Children.Add(body);
        Refresh();
    }

    TileSwatch? _swatch;
    string? _lastHover;
    string? LastHover => _lastHover;

    public IReadOnlyList<IconButton> Tools => _tools;
    public IReadOnlyList<LayerToggle> BgToggles => _bgToggles;
    public Chip CollisionChip => _collisionChip;

    void Refresh()
    {
        if (Session is { } ses)
        {
            if (ses.HoverText is { } h) _lastHover = h;
            for (var i = 0; i < _tools.Length; i++) _tools[i].IsActive = (int)ses.Tool == i;
            for (var bg = 0; bg < 4; bg++)
            {
                var li = ses.LayerOnBg(bg);
                _bgToggles[bg].IsOn = li >= 0 && ses.IsVisible(li);
                _bgToggles[bg].IsActive = li >= 0 && li == ses.Layer;
            }
            _objToggle.IsOn = ses.ObjVisible;
            _collisionChip.Style = ses.CollisionView ? ChipStyle.Danger : ChipStyle.Default;
        }
        foreach (var r in _refresh) r();
    }

    // ---- left: tileset ------------------------------------------------------------------------------

    Control LeftPanel()
    {
        var header = new PanelHeader { Title = "Tileset", Glyph = "tiles", ActionGlyph = "dots", Name = "TilesetHeader" };
        header.ActionCommand = new Relay(() => _ = _svc.Windows.Open("T-M13", Session?.Doc.TilesetPath, anchor: header, source: "TilesetMenu"));
        var stack = new StackPanel { Name = "TilesetBody" };
        if (Session is { } ses)
        {
            var set = PageChrome.Field("Set", FieldKind.Dropdown, ses.Doc.TilesetPath is { } tp ? Path.GetFileName(tp) : ses.Doc.Map.Tileset, "TilesetField");
            set.Margin = new Thickness(10, 8, 10, 0);
            PageChrome.OnDropdown(set, () => _ = PickTileset(set));
            stack.Children.Add(set);

            _swatch = new TileSwatch(ses);
            var scroll = new ScrollViewer { Content = _swatch, Height = 118, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, Name = "SwatchScroll" };
            var box = UI.Frame(scroll, "bg/base", "border/default", new Thickness(1), radius: 4).Named("SwatchBox");
            box.Margin = new Thickness(10, 8, 10, 0);
            stack.Children.Add(box);

            var tileHeader = new SectionHeader { Glyph = "square", Name = "TileHeader" };
            var chips = new WrapPanel { ItemSpacing = 5, LineSpacing = 5, Name = "CollisionChips" };
            var chipControls = CollisionChoices.Select(c =>
            {
                var chip = new Chip { Content = c.Text, Name = "Collision_" + c.Type };
                chip.Click += (_, _) => ses.Doc.SetCollision(ses.SelectedTile, c.Type);
                chips.Children.Add(chip);
                return (c.Type, chip);
            }).ToList();
            var tag = PageChrome.Field("Tag", FieldKind.Text, null, "TagField");
            PageChrome.OnCommit(tag, () => ses.Doc.SetTag(ses.SelectedTile, tag.Value ?? ""));
            var anim = PageChrome.Field("Animation", FieldKind.Dropdown, null, "AnimationField");
            PageChrome.OnDropdown(anim, () => PageChrome.Choose(anim, AnimationChoices, anim.Value, i => ses.Doc.SetAnimation(ses.SelectedTile, i == 0 ? 0 : i + 1)));
            var auto = PageChrome.Field("Auto-tile", FieldKind.Dropdown, null, "AutoTileField");
            PageChrome.OnDropdown(auto, () =>
            {
                var sets = ses.Doc.Meta.AutoTiles;
                PageChrome.Choose(auto, ["none", .. sets.Select(MapDocument.SetLabel)], auto.Value, i => ses.Doc.SetAutoTile(ses.SelectedTile, i == 0 ? null : sets[i - 1].Name));
            });
            chips.Margin = new Thickness(0, 0, 0, 0);
            var section = PageChrome.Section(tileHeader, [chips, tag, anim, auto], 12, "TileSection");
            section.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(section);
            _refresh.Add(() =>
            {
                var t = ses.SelectedTile;
                tileHeader.Title = $"Tile #{t} · {ses.Doc.TileName(t)}";
                var col = ses.Doc.Props(t)?.Collision ?? CollisionType.None;
                foreach (var (type, chip) in chipControls)
                {
                    var on = type == col;
                    chip.Style = !on ? ChipStyle.Default : type is CollisionType.Solid or CollisionType.Hazard ? ChipStyle.Danger : ChipStyle.Accent;
                    chip.Glyph = on ? "square" : null;
                }
                if (!tag.IsKeyboardFocusWithin) tag.Value = ses.Doc.Props(t)?.Tag ?? "";
                var frames = ses.Doc.AnimationFrames(t);
                anim.Value = frames == 0 ? "none" : $"{frames} frames";
                auto.Value = ses.Doc.SetOf(t) is { } st ? MapDocument.SetLabel(st) : "none";
                set.Value = ses.Doc.TilesetPath is { } p ? Path.GetFileName(p) : ses.Doc.Map.Tileset;
            });
        }
        return PageChrome.SidePanel(header, stack, true, "TilesetPanel");
    }

    async Task PickTileset(InspectorField field)
    {
        if (Session is not { } ses) return;
        var result = await _svc.Windows.Open("T-O04", "Tileset", field, source: "TilesetDropdown");
        var name = result switch { string s => GameProject.AssetName(s), _ => null };
        if (name is null || string.Equals(name, ses.Doc.Map.Tileset, StringComparison.OrdinalIgnoreCase)) return;
        ses.Doc.SetTileset(name);
    }

    // ---- centre: viewport chips -------------------------------------------------------------------

    Control ViewportChips(TilemapSession ses)
    {
        var tile = new Chip { Glyph = "grid", Name = "HoverChip" };
        var brush = new Chip { Name = "BrushChip" };
        brush.Click += (_, _) => ses.Tool = TileTool.Brush;
        var zoom = new Chip { Name = "ZoomChip" };
        zoom.Click += (_, _) => ses.NextZoom();
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Name = "CollisionLegend" };
        var left = UI.Row(6, tile, brush);
        left.HorizontalAlignment = HorizontalAlignment.Left;
        var right = UI.Row(6, legend, zoom);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        var grid = new Panel { Children = { left, right }, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 0, 13, 12) };
        (int X, int Y) last = (0, 0);
        _refresh.Add(() =>
        {
            if (ses.Hover is { } h) last = h;
            tile.Content = $"tile {last.X}, {last.Y}";
            brush.Content = ses.BrushText;
            zoom.Content = $"{ses.Zoom * 100}%";
            legend.Children.Clear();
            var selected = ses.Doc.Props(ses.SelectedTile)?.Collision ?? CollisionType.None;
            foreach (var type in ses.Doc.CollisionTypesUsed())
            {
                var text = CollisionChoices.First(c => c.Type == type).Text;
                var chip = new Chip { Content = text, Glyph = type == selected ? "square" : null, Name = "Legend_" + type, Opacity = ses.ShowsCollision(type) ? 1 : 0.5 };
                var t = type;
                chip.Click += (_, _) => ses.ToggleCollisionType(t);
                legend.Children.Add(chip);
            }
        });
        return grid;
    }

    // ---- right: map ---------------------------------------------------------------------------------

    Control RightPanel()
    {
        var header = new PanelHeader { Title = "Map", Glyph = "sliders", ActionGlyph = "dots", Name = "MapHeader" };
        header.ActionCommand = new Relay(() => _ = _svc.Windows.Open("T-M20", Session, anchor: header, source: "MapMenu"));
        var dock = new DockPanel();
        if (Session is { } ses)
        {
            var doc = ses.Doc;
            dock.Children.Add(Budget(ses));
            var stack = new StackPanel { Name = "MapBody" };

            var size = PageChrome.Field("Size", FieldKind.XY, null, "SizeField");
            PageChrome.OnCommit(size, () =>
            {
                var parts = (size.Value ?? "").Split(['×', 'x', 'X', '*'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h)) doc.Resize(w, h);
                else Refresh();
            });
            var bgSize = PageChrome.Field("BG size", FieldKind.Dropdown, null, "BgSizeField");
            PageChrome.OnDropdown(bgSize, () => PageChrome.Choose(bgSize, BgSizes, bgSize.Value, i => doc.SetBgSize((BgSize)i)));
            var colours = PageChrome.Field("Colours", FieldKind.Dropdown, null, "ColoursField");
            PageChrome.OnDropdown(colours, () => PageChrome.Choose(colours, Colours, colours.Value, i => doc.SetBpp(i == 0 ? 4 : 8)));
            var wrap = PageChrome.Field("Wrap", FieldKind.Dropdown, null, "WrapField");
            PageChrome.OnDropdown(wrap, () => PageChrome.Choose(wrap, ["Off", "On"], wrap.Value, i => doc.SetWrap(ses.Layer, i == 1)));
            var mapHeader = new SectionHeader { Glyph = "map", Name = "MapSectionHeader" };
            stack.Children.Add(PageChrome.Section(mapHeader, [size, bgSize, colours, wrap], 12, "MapSection"));

            var stamp = PageChrome.Field("Stamp", FieldKind.Text, null, "StampField");
            PageChrome.OnCommit(stamp, () => { if (!ses.SetStampSize(stamp.Value ?? "")) Refresh(); });
            var random = PageChrome.Field("Random", FieldKind.Slider, "off", "RandomField");
            random.SliderValue = 0;
            random.PropertyChanged += (_, e) => { if (e.Property == InspectorField.SliderValueProperty) ses.SetRandom(random.SliderValue); };
            var flip = PageChrome.Field("Flip H", FieldKind.Toggle, null, "FlipField");
            flip.IsOn = false;
            flip.PropertyChanged += (_, e) => { if (e.Property == InspectorField.IsOnProperty) ses.SetFlip(flip.IsOn); };
            stack.Children.Add(PageChrome.Section(new SectionHeader { Title = "Brush", Glyph = "pencil", Name = "BrushSectionHeader" }, [stamp, random, flip], 12, "BrushSection"));

            var source = PageChrome.Field("Source", FieldKind.Text, null, "SourceField");
            var solid = PageChrome.Field("Solid tiles", FieldKind.Text, null, "SolidField");
            var oneWay = PageChrome.Field("One-way", FieldKind.Text, null, "OneWayField");
            foreach (var f in new[] { source, solid, oneWay }) PageChrome.OnCommit(f, Refresh);  // computed: typing is reverted
            var note = UI.Text("Actors with a Platformer Body collide with these automatically — no code needed.", "Label/11", "text/muted");
            note.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            note.Margin = new Thickness(0, 0, 0, 0);
            stack.Children.Add(PageChrome.Section(new SectionHeader { Title = "Collision", Glyph = "square", Name = "CollisionSectionHeader" }, [source, solid, oneWay, note], 12, "CollisionSection"));
            dock.Children.Add(stack);

            _refresh.Add(() =>
            {
                mapHeader.Title = doc.FileName;
                if (!size.IsKeyboardFocusWithin) size.Value = $"{doc.Width} × {doc.Height}";
                size.Value2 = $"{doc.Width * 16}×{doc.Height * 16} px";
                bgSize.Value = BgSizes[(int)doc.BgSize];
                colours.Value = doc.Bpp == 8 ? Colours[1] : Colours[0];
                wrap.Value = ses.CurrentLayer?.Wrap == true ? "On" : "Off";
                if (!stamp.IsKeyboardFocusWithin) stamp.Value = ses.StampText;
                random.Value = ses.Random <= 0 ? "off" : ses.Random.ToString("0", CultureInfo.InvariantCulture) + "%";
                random.SliderValue = ses.Random;
                flip.IsOn = ses.Flip;
                var ci = doc.CollisionLayerIndex;
                source.Value = ci >= 0 && doc.Map.Layers[ci].Type == LayerType.CollisionOnly ? doc.Map.Layers[ci].Name + " layer" : "tile properties";
                solid.Value = doc.CountCollision(CollisionType.Solid).ToString(CultureInfo.InvariantCulture);
                oneWay.Value = doc.CountCollision(CollisionType.OneWay).ToString(CultureInfo.InvariantCulture);
            });
        }
        return PageChrome.SidePanel(header, dock, false, "MapPanel");
    }

    Control Budget(TilemapSession ses)
    {
        var title = UI.Row(8, PageChrome.Glyph("chip", 13, "text/secondary"), UI.Text("GBA Budget", "Overline/11", "text/secondary"));
        var dot = new Avalonia.Controls.Shapes.Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
        dot.Token(Avalonia.Controls.Shapes.Shape.FillProperty, "status/success");
        var good = UI.Row(5, dot, UI.Text("all good", "Label/11", "status/success"));
        good.Name = "BudgetAllGood";
        var head = new DockPanel { Height = 18 };
        DockPanel.SetDock(good, Dock.Right);
        head.Children.Add(good);
        head.Children.Add(title);
        Meter M(string label) => new() { Label = label, Name = "Meter_" + label.Replace(" ", "") };
        var tiles = M("Unique tiles");
        var vram = M("VRAM BG");
        var blocks = M("Screen blocks");
        var palettes = M("BG palettes");
        var stack = UI.Column(7, head, tiles, vram, blocks, palettes);
        var frame = UI.Frame(stack, "bg/sunken", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 6, 12, 11)).Named("Budget");
        DockPanel.SetDock(frame, Dock.Bottom);
        _refresh.Add(() =>
        {
            var plan = ses.Plan();
            var ok = true;
            void Set(Meter m, int used, int limit, string unit)
            {
                var pct = limit == 0 ? 0 : 100.0 * used / limit;
                m.Percent = Math.Min(100, pct);
                m.Status = pct >= 100 ? ProgressStatus.Danger : pct >= 80 ? ProgressStatus.Warn : ProgressStatus.OK;
                m.ValueText = $"{used} / {limit}{unit}";
                ok &= pct < 80;
            }
            Set(tiles, plan.UniqueTiles, 1024, "");
            Set(vram, (int)Math.Round(plan.VramBytes / 1024.0), 64, " KB");
            Set(blocks, plan.ScreenBlocks, 32, "");
            Set(palettes, plan.PaletteBanks, 16, "");
            ok &= plan.Layers.All(l => l.Fits);
            good.IsVisible = ok;
        });
        return frame;
    }
}
