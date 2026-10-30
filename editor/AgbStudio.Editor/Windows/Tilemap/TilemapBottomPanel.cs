using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>
/// The Tilemap Editor's bottom panel (860 x 250): tabs Layers / Auto-tile rules / Assets. Layers is the layers table
/// (type, priority, parallax, the VRAM char block and screen block each layer gets, visibility, lock; right-click adds
/// a layer, T-M20). Auto-tile rules is the Auto-tile Rules panel (T-P26): the rule set, its 3x3 rules and Add rule.
/// </summary>
[EditorWindow("T-P26", Kind = WindowKind.Panel)]
public sealed class TilemapBottomPanel : DockPanel
{
    readonly TilemapSession _s;
    readonly EditorServices? _services;
    readonly PanelTab[] _tabs;
    readonly ContentControl _content = new() { Name = "BottomContent" };
    readonly InspectorField _ruleSet;
    readonly Grid _table = new() { Name = "LayersTable", ColumnDefinitions = ColumnDefinitions.Parse("200,110,90,90,260,76") };
    readonly WrapPanel _cards = new() { Name = "RuleCards", ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(14, 14, 14, 0) };
    readonly Control? _assets;

    public TilemapBottomPanel(WindowContext ctx) : this(MapLibrary.Of(ctx.Services).Current(ctx.Args) ?? throw new InvalidOperationException("No map is open."), ctx.Services, 1) { }

    public TilemapBottomPanel(TilemapSession session, EditorServices? services, int tab = 0)
    {
        _s = session;
        _services = services;
        Name = "TilemapBottomPanel";
        Focusable = true;
        this.Token(BackgroundProperty, "bg/panel");
        _tabs =
        [
            new PanelTab { Label = "Layers", Glyph = "layers", Name = "Tab_Layers", Height = 31 },
            new PanelTab { Label = "Auto-tile rules", Glyph = "spark", Name = "Tab_AutoTile", Height = 31 },
            new PanelTab { Label = "Assets", Glyph = "folder", Name = "Tab_Assets", Height = 31 },
        ];
        for (var i = 0; i < _tabs.Length; i++) { var k = i; _tabs[i].Activated += (_, _) => SelectTab(k); }
        _ruleSet = PageChrome.Field("Rule set", FieldKind.Dropdown, null, "RuleSetField");
        _ruleSet.Width = 279;
        _ruleSet.VerticalAlignment = VerticalAlignment.Center;
        PageChrome.OnDropdown(_ruleSet, OpenRuleSets);
        var tabRow = new DockPanel { Height = 32 };
        DockPanel.SetDock(_ruleSet, Dock.Right);
        _ruleSet.Margin = new Thickness(0, 0, 8, 0);
        tabRow.Children.Add(_ruleSet);
        tabRow.Children.Add(UI.Row(0, _tabs));
        var tabFrame = UI.Frame(tabRow, null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(6, 0, 0, 0)).Named("BottomTabs");
        SetDock(tabFrame, Dock.Top);
        Children.Add(tabFrame);
        Children.Add(_content);
        _table.Margin = new Thickness(8, 4, 26, 0);
        _table.HorizontalAlignment = HorizontalAlignment.Left;
        _table.VerticalAlignment = VerticalAlignment.Top;
        _assets = services?.Windows.CreatePanel("T-P01:Assets");

        void OnDoc(MapChange _) => Refresh();
        PageChrome.WhileAttached(this, () => { session.Doc.Changed += OnDoc; session.ViewChanged += Refresh; Refresh(); },
            () => { session.Doc.Changed -= OnDoc; session.ViewChanged -= Refresh; });
        SelectTab(tab);
    }

    public int SelectedTab => Array.FindIndex(_tabs, t => t.IsSelected);
    public IReadOnlyList<PanelTab> Tabs => _tabs;
    public Grid LayersTable => _table;
    public WrapPanel RuleCards => _cards;
    public InspectorField RuleSetField => _ruleSet;

    public void SelectTab(int i)
    {
        for (var k = 0; k < _tabs.Length; k++) _tabs[k].IsSelected = k == i;
        _ruleSet.IsVisible = i == 1;
        _content.Content = i switch { 0 => _table, 1 => _cards, _ => _assets ?? new Panel { Name = "AssetsSlot" } };
        if (i == 1) _services?.Windows.Requests.Add(("T-P26", "Tab_AutoTile"));
        else if (i == 2) _services?.Windows.Requests.Add(("T-P01:Assets", "Tab_Assets"));
        Refresh();
    }

    void Refresh()
    {
        if (SelectedTab == 0) BuildTable();
        else if (SelectedTab == 1) BuildCards();
    }

    // ---- layers table ------------------------------------------------------------------------------

    public static string TypeText(LayerType t) => t switch
    {
        LayerType.Image => "Image", LayerType.Affine => "Rotate / scale", LayerType.CollisionOnly => "Collision", _ => "Tilemap",
    };

    public static string ParallaxText(double p) => "× " + p.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Rows in table order: layers by BG (collision-only last), then OBJ (-1).</summary>
    public IReadOnlyList<int> RowOrder() =>
        [.. Enumerable.Range(0, _s.Doc.Map.Layers.Count).OrderBy(i => _s.Doc.Map.Layers[i].Type == LayerType.CollisionOnly).ThenBy(i => _s.Doc.Map.Layers[i].Bg), -1];

    /// <summary>The texts of one table row (tests).</summary>
    public IReadOnlyList<string> RowTexts(int row) =>
        _table.Children.OfType<TableCell>().Where(c => Grid.GetRow(c) == row + 1).OrderBy(Grid.GetColumn).Select(c => c.Text ?? "").ToList();

    void BuildTable()
    {
        _table.Children.Clear();
        _table.RowDefinitions.Clear();
        _table.RowDefinitions.Add(new RowDefinition(28, GridUnitType.Pixel));
        string[] heads = ["Layer", "Type", "Priority", "Parallax", "VRAM", ""];
        for (var c = 0; c < heads.Length; c++) Put(new TableCell { Kind = TableCellKind.Header, Text = heads[c] }, 0, c);
        var plan = _s.Plan();
        var row = 1;
        foreach (var li in RowOrder())
        {
            _table.RowDefinitions.Add(new RowDefinition(30, GridUnitType.Pixel));
            var selected = li >= 0 && li == _s.Layer;
            string[] texts;
            if (li < 0)
                texts = ["OBJ · Actors", "Sprites", "–", "–", $"OAM {VramPlan.OamEntries(_s.Doc.Scene)} / 128"];
            else
            {
                var l = _s.Doc.Map.Layers[li];
                texts = [TilemapSession.LayerLabel(l), TypeText(l.Type), l.Type == LayerType.CollisionOnly ? "–" : l.Priority.ToString(CultureInfo.InvariantCulture),
                    l.Type == LayerType.CollisionOnly ? "–" : ParallaxText(l.ParallaxX), li < plan.Layers.Count ? plan.Layers[li].Text : ""];
            }
            for (var c = 0; c < texts.Length; c++)
            {
                var kind = selected ? TableCellKind.Selected : c >= 2 ? TableCellKind.Mono : TableCellKind.Default;
                var cell = new TableCell { Kind = kind, Text = texts[c], Height = 30, Name = $"Layer{row}_{c}" };
                var layerIndex = li;
                cell.PointerPressed += (_, e) => RowPressed(layerIndex, cell, e);
                Put(cell, row, c);
            }
            Put(Icons(li, selected), row, 5);
            row++;
        }
    }

    void Put(Control c, int row, int col)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        _table.Children.Add(c);
    }

    Control Icons(int li, bool selected)
    {
        var token = selected ? "text/primary" : "text/muted";
        var visible = li < 0 ? _s.ObjVisible : _s.IsVisible(li);
        var locked = li >= 0 && _s.IsLocked(li);
        var eye = new Button { Theme = PageChrome.Bare, Content = PageChrome.Glyph("eye", 14, token), Name = $"Eye_{(li < 0 ? "OBJ" : li.ToString(CultureInfo.InvariantCulture))}", Opacity = visible ? 1 : 0.35 };
        eye.Click += (_, _) => { if (li < 0) _s.ObjVisible = !_s.ObjVisible; else _s.SetVisible(li, !_s.IsVisible(li)); };
        var lockIcon = PageChrome.Glyph("lock", 14, locked ? "status/warning" : token);
        var lockButton = new Button { Theme = PageChrome.Bare, Content = lockIcon, Name = $"Lock_{(li < 0 ? "OBJ" : li.ToString(CultureInfo.InvariantCulture))}", IsHitTestVisible = li >= 0 };
        lockButton.Click += (_, _) => _s.SetLocked(li, !_s.IsLocked(li));
        var row = UI.Row(10, eye, lockButton);
        row.Margin = new Thickness(10, 0, 0, 0);
        row.VerticalAlignment = VerticalAlignment.Center;
        var b = new Border { Child = row, Background = Brushes.Transparent };
        if (selected) b.Token(Border.BackgroundProperty, "accent/selected");
        return b;
    }

    void RowPressed(int li, Control cell, PointerPressedEventArgs e)
    {
        var p = e.GetCurrentPoint(cell).Properties;
        if (p.IsRightButtonPressed)
        {
            _ = _services?.Windows.Open("T-M20", _s, anchor: cell, source: "LayersTable");
            e.Handled = true;
            return;
        }
        if (li >= 0) _s.Layer = li;
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (SelectedTab == 0 && e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            _ = _services?.Windows.Open("T-M20", _s, anchor: _table, source: "LayersTable");
            e.Handled = true;
        }
    }

    // ---- auto-tile rules ---------------------------------------------------------------------------

    void OpenRuleSets()
    {
        var sets = _s.Doc.Meta.AutoTiles;
        PageChrome.Choose(_ruleSet, sets.Select(MapDocument.SetLabel).ToList(), _ruleSet.Value, i => _s.SetRuleSet(sets[i].Name));
    }

    void BuildCards()
    {
        var set = _s.CurrentSet;
        _ruleSet.Value = set is null ? "none" : MapDocument.SetLabel(set);
        _cards.Children.Clear();
        if (set != null)
            for (var i = 0; i < set.Rules.Count; i++) _cards.Children.Add(RuleCard(set, i));
        _cards.Children.Add(AddRuleCard());
    }

    Control RuleCard(AutoTileSet set, int index)
    {
        var rule = set.Rules[index];
        var canvas = new Canvas { Width = 130, Height = 82 };
        for (var i = 0; i < 9; i++)
        {
            var v = i == 4 ? 1 : i < rule.Mask.Length ? rule.Mask[i] : 2;
            var cell = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(2), BorderThickness = new Thickness(v == 1 ? 0 : 1), Name = $"Mask{index}_{i}" };
            cell.Token(Border.BackgroundProperty, v == 1 ? "status/success" : "bg/raised");
            if (v != 1) cell.Token(Border.BorderBrushProperty, v == 0 ? "status/danger" : "border/strong");
            Canvas.SetLeft(cell, 10 + i % 3 * 14);
            Canvas.SetTop(cell, 9 + i / 3 * 14);
            var k = i;
            cell.PointerPressed += (_, e) => { _s.SelectRule(index); _s.Doc.CycleMask(set.Name, index, k); e.Handled = true; };
            canvas.Children.Add(cell);
        }
        var arrow = PageChrome.Glyph("right", 12, "text/muted");
        Canvas.SetLeft(arrow, 59);
        Canvas.SetTop(arrow, 24);
        canvas.Children.Add(arrow);
        var tile = new Image { Width = 38, Height = 38, Source = Pixels.ToBitmap(_s.Renderer.Metatile(_s.Doc, rule.Tile), 16, 16), Name = $"RuleTile{index}" };
        RenderOptions.SetBitmapInterpolationMode(tile, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        var tileBox = UI.Frame(tile, null, "border/default", new Thickness(1));
        tileBox.Width = tileBox.Height = 40;
        Canvas.SetLeft(tileBox, 80);
        Canvas.SetTop(tileBox, 9);
        ToolTip.SetTip(tileBox, new TipContent(_s.Doc.TileName(rule.Tile)));
        tileBox.PointerPressed += (_, e) =>
        {
            _s.SelectRule(index);
            if (e.ClickCount >= 2) _s.Doc.SetRuleTile(set.Name, index, _s.SelectedTile);
            e.Handled = true;
        };
        canvas.Children.Add(tileBox);
        var label = UI.Text(rule.Name, "Label/11", "text/secondary");
        label.TextAlignment = TextAlignment.Center;
        label.Width = 130;
        Canvas.SetTop(label, 58);
        canvas.Children.Add(label);
        var card = UI.Frame(canvas, "bg/base", index == _s.SelectedRule ? "accent/default" : "border/default", new Thickness(1), radius: 8).Named($"RuleCard{index}");
        card.Width = 132;
        card.Height = 84;
        card.VerticalAlignment = VerticalAlignment.Top;
        card.PointerPressed += (_, e) => { _s.SelectRule(index); Focus(); e.Handled = true; };
        return card;
    }

    Control AddRuleCard()
    {
        var dash = new Rectangle { RadiusX = 8, RadiusY = 8, StrokeThickness = 1, StrokeDashArray = [3, 3] };
        dash.Token(Shape.StrokeProperty, "border/strong");
        var content = UI.Column(6, PageChrome.Glyph("plus", 14, "text/muted"), UI.Text("Add rule", "Body/12", "text/muted"));
        ((Control)content.Children[0]).HorizontalAlignment = HorizontalAlignment.Center;
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        var b = new Border { Width = 90, Height = 112, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, Child = new Panel { Children = { dash, content } }, Name = "AddRule", Cursor = new Cursor(StandardCursorType.Hand) };
        b.PointerPressed += (_, e) =>
        {
            var name = _s.RuleSet ?? _s.Doc.TileName(_s.SelectedTile) + " rules";
            _s.Doc.AddRule(name, _s.SelectedTile);
            _s.SetRuleSet(name);
            _s.SelectRule(0);
            e.Handled = true;
        };
        return b;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (SelectedTab != 1 || e.Key is not (Key.Delete or Key.Back) || _s.CurrentSet is not { } set) return;
        _s.Doc.RemoveRule(set.Name, _s.SelectedRule);
        _s.SelectRule(Math.Max(0, _s.SelectedRule - 1));
        e.Handled = true;
    }
}
