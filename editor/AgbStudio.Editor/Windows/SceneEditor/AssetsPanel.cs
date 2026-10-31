using System.Globalization;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// Assets panel (T-P01 bottom panel, P11.14): categories with counts (the category list filters the cards), cards with
/// the real asset pixels, double click opens the right editor (#24, #26), right click the Asset Context Menu (T-M13,
/// #25), and a sprite card dragged into the viewport becomes an actor at the drop point.
/// </summary>
[EditorWindow("T-P01:Assets", Kind = WindowKind.Panel)]
public sealed class AssetsPanel : DockPanel, IEditTarget
{
    readonly EditorServices _s;
    readonly SceneEditorModel _m;
    readonly AssetActions _assets;
    readonly StackPanel _categories = new() { Spacing = 1, Name = "AssetCategories" };
    readonly WrapPanel _cards = new() { Name = "AssetCards", ItemSpacing = 10, LineSpacing = 12 };
    AssetCategory _category = AssetCategory.Sprites;
    // drag a sprite card into the viewport
    AssetItem? _drag;
    Point _dragFrom;
    bool _dragging;

    public AssetsPanel(WindowContext ctx) : this(ctx.Services) { }

    public AssetsPanel(EditorServices s)
    {
        _s = s;
        _m = SceneEditorModel.Of(s);
        _assets = AssetActions.Of(_m);
        Name = "AssetsPanel";
        var left = UI.Frame(_categories, null, "border/default", new Thickness(0, 0, 1, 0), new Thickness(6, 8, 6, 6));
        left.Width = 170;
        SetDock(left, Avalonia.Controls.Dock.Left);
        Children.Add(left);
        Children.Add(new ScrollViewer { Content = new Border { Padding = new Thickness(12, 10, 12, 10), Child = _cards }, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        // a click in the panel gives it the keyboard (as the Animation panel and the scene view do): Ctrl+C / Ctrl+V /
        // Del then act on the selected asset, not on a text box that had the focus before
        Focusable = true;
        AddHandler(PointerPressedEvent, (_, e) => { _dragFrom = e.GetPosition(this); Focus(); }, RoutingStrategies.Tunnel, handledEventsToo: true);
        _assets.Changed += Rebuild;
        _m.Changed += RebuildCounts;
        _m.SelectionChanged += Rebuild;
        DetachedFromVisualTree += (_, _) => { _assets.Changed -= Rebuild; _m.Changed -= RebuildCounts; _m.SelectionChanged -= Rebuild; };
        AttachedToVisualTree += (_, _) =>
        {
            _assets.Changed -= Rebuild; _m.Changed -= RebuildCounts; _m.SelectionChanged -= Rebuild;
            _assets.Changed += Rebuild; _m.Changed += RebuildCounts; _m.SelectionChanged += Rebuild;
            Rebuild();
        };
        Rebuild();
    }

    public AssetCategory Category
    {
        get => _category;
        set
        {
            _category = value;
            Rebuild();
        }
    }

    public IReadOnlyList<AssetCard> Cards => _cards.Children.OfType<Panel>().Select(p => p.Children.OfType<AssetCard>().First()).ToList();
    public IReadOnlyList<ListRow> CategoryRows => _categories.Children.OfType<ListRow>().ToList();

    void RebuildCounts()
    {
        foreach (var row in CategoryRows)
            if (row.Tag is AssetCategory c) row.Meta = _assets.Count(c).ToString(CultureInfo.InvariantCulture);
    }

    void Rebuild()
    {
        _categories.Children.Clear();
        foreach (var (c, label, glyph) in AssetActions.Categories)
        {
            var row = new ListRow { Label = label, Glyph = glyph, Meta = _assets.Count(c).ToString(CultureInfo.InvariantCulture), IsSelected = c == _category, Tag = c, Name = "Category_" + label };
            var cat = c;
            row.Activated += (_, _) => Category = cat;
            _categories.Children.Add(row);
        }
        _cards.Children.Clear();
        // the selected asset, else the sheet of the selected actor (hero.png when the Player is selected)
        var selected = _assets.Selected?.Path ?? (_m.SelectedActor?.Sprite is { Length: > 0 } sp ? _m.Assets?.SpritePath(sp) : null);
        foreach (var a in _assets.List(_category)) _cards.Children.Add(Card(a, a.Path == selected));
    }

    Control Card(AssetItem a, bool selected)
    {
        var card = new AssetCard { NameText = a.Name, Meta = a.Meta, Preview = a.Thumb, IsSelected = selected, Tag = a, Name = "Card_" + a.Name };
        var holder = new Panel { Width = 102, Children = { card } };
        if (a.Thumb == null)
        {
            var bmp = a.Category == AssetCategory.Sprites ? PixelArt.SpriteBitmap(_m.Assets, a.Stem) : PixelArt.Png(a.Path);
            var img = a.Category == AssetCategory.Sprites ? PixelArt.View(bmp, 32, 32) : PixelArt.View(bmp, 64, 48);
            img.VerticalAlignment = VerticalAlignment.Top;
            img.Margin = new Thickness(0, Math.Max(0, (62 - (double.IsNaN(img.Height) ? 0 : img.Height)) / 2), 0, 0);
            img.IsHitTestVisible = false;
            holder.Children.Add(img);
        }
        card.Activated += (_, e) =>
        {
            _assets.Select(a.Path);
            if (e.ClickCount == 2) _ = _assets.Open(a);
            else if (a.Category == AssetCategory.Sprites) { _drag = a; _dragging = false; }
        };
        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsRightButtonPressed) return;
            _assets.Select(a.Path);
            _ = _s.Windows.Open("T-M13", a.Path, card, source: "AssetCard");
            e.Handled = true;
        };
        return holder;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        if (!_dragging && (Math.Abs(p.X - _dragFrom.X) > 4 || Math.Abs(p.Y - _dragFrom.Y) > 4))
        {
            _dragging = true;
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.DragCopy);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var drag = _drag;
        var dragging = _dragging;
        _drag = null;
        _dragging = false;
        Cursor = null;
        e.Pointer.Capture(null);
        if (drag == null || !dragging || TopLevel.GetTopLevel(this) is not { } top) return;
        var pt = e.GetPosition(top);
        if (top.InputHitTest(pt) is Visual v && (v as GbaScreenView ?? v.FindAncestorOfType<GbaScreenView>()) is { } screen)
            screen.DropSprite(drag.Stem, e.GetPosition(screen));
    }

    // ---- IEditTarget ----------------------------------------------------------------------------------

    SceneEditTarget Target
    {
        get
        {
            var t = SceneEditTarget.Of(_m);
            t.Active = SceneEditTarget.Panel.Assets;
            return t;
        }
    }

    public bool CanCopy => Target.CanCopy;
    public bool CanPaste => Target.CanPaste;
    public void Cut() => Target.Cut();
    public void Copy() => Target.Copy();
    public void Paste() => Target.Paste();
    public void Duplicate() => Target.Duplicate();
    public void Delete() => Target.Delete();
    public void SelectAll() => Target.SelectAll();
}
