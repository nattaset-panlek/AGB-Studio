using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>One row of the Hierarchy; actor rows carry the actor's components (<see cref="ActorComponents"/>).</summary>
public sealed record HierarchyRow(SceneItem Item, string Label, string Glyph, string? Meta, int Depth, TreeToggle Toggle, string Key, IReadOnlyList<ComponentBadge>? Parts = null);

/// <summary>
/// Hierarchy (T-P01, P11.12): the scene, its camera, background layers and actors (actors of a group with two or more
/// members under a folder), filtered by the workspace's "Search scene…" field. Click selects (in sync with the
/// viewport and Inspector), Ctrl + click adds an actor to the selection or takes it out, Shift + click selects the
/// actors from the first selected one to the clicked one, the chevron or a double click opens and closes folders, a
/// double click on an actor focuses it in the viewport, right click opens the Actor Context Menu (T-M12), F2 renames in
/// place. Actor rows show small badges for the actor's components (behaviours, sprite, text, links, camera) and list
/// them all in the row tooltip.
/// </summary>
[EditorWindow("T-P01:Hierarchy", Kind = WindowKind.Panel)]
public sealed class HierarchyPanel : Panel, IEditTarget
{
    readonly EditorServices _s;
    readonly SceneEditorModel _m;
    readonly StackPanel _rows = new() { Spacing = 1, Margin = new Thickness(6, 4, 7, 6), Name = "HierarchyRows" };
    readonly HashSet<string> _closed = [];
    readonly HashSet<string> _seenGroups = [];
    SearchField? _search;
    TextBox? _rename;

    public HierarchyPanel(WindowContext ctx) : this(ctx.Services) { }

    public HierarchyPanel(EditorServices s)
    {
        _s = s;
        _m = SceneEditorModel.Of(s);
        Name = "HierarchyPanel";
        Children.Add(new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        _dropLine.Token(Border.BackgroundProperty, "accent/default");
        _dropDot.Token(Shape.FillProperty, "accent/default");
        _dropBox.Token(Border.BackgroundProperty, "accent/subtle").Token(Border.BorderBrushProperty, "accent/default");
        Children.Add(new Canvas { IsHitTestVisible = false, Children = { _dropBox, _dropLine, _dropDot } });
        _m.Changed += Rebuild;
        _m.SelectionChanged += UpdateSelectionOnly;
        SceneEditTarget.Of(_m).RenameRequested += p => { if (p == SceneEditTarget.Panel.Scene) BeginRename(); };
        AttachedToVisualTree += (_, _) => HookSearch();
        // where in the row the pointer went down (the chevron opens and closes folders)
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is Visual v && v.FindAncestorOfType<TreeItem>(true) is { } t)
            {
                _lastPointerX = e.GetPosition(t).X;
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && t.Tag is HierarchyRow r && r.Item.Kind == SceneItemKind.Actor)
                {
                    _dragActorIndex = r.Item.Index;
                    _dragStartPoint = e.GetPosition(this);
                    _isDragging = false;
                }
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_dragActorIndex >= 0 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var cur = e.GetPosition(this);
                if (!_isDragging && (Math.Abs(cur.X - _dragStartPoint.X) > 4 || Math.Abs(cur.Y - _dragStartPoint.Y) > 4))
                {
                    _isDragging = true;
                    e.Pointer.Capture(this);
                    if (_m.Scene?.Actors.ElementAtOrDefault(_dragActorIndex) is { } dragged) DragGhost.Show(this, e, ActorGlyph(dragged), dragged.Name);
                }
                if (_isDragging)
                {
                    DragGhost.Move(this, e);
                    UpdateDropFeedback(cur);
                }
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_isDragging && _dragActorIndex >= 0)
            {
                // the drop first: releasing the pointer ends the drag
                ApplyDrop(e.GetPosition(this));
                e.Pointer.Capture(null);
                e.Handled = true;
            }
            _dragActorIndex = -1;
            _isDragging = false;
            DragGhost.Hide();
            ClearDropFeedback();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        AddHandler(PointerCaptureLostEvent, (_, _) =>
        {
            if (!_isDragging) return;
            _dragActorIndex = -1;
            _isDragging = false;
            DragGhost.Hide();
            ClearDropFeedback();
        });
        Rebuild();
    }

    int _dragActorIndex = -1;
    Point _dragStartPoint;
    bool _isDragging;
    readonly Border _dropLine = new() { Height = 2, IsVisible = false, Name = "HierarchyDropLine" };
    readonly Ellipse _dropDot = new() { Width = 6, Height = 6, IsVisible = false, Name = "HierarchyDropDot" };
    readonly Border _dropBox = new() { IsVisible = false, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(4), Name = "HierarchyDropBox" };
    DropPosition _currentDropPos = DropPosition.None;

    public enum DropPosition { None, Above, Inside, Below, Root }

    public (TreeItem? Item, DropPosition Pos) HitTestRow(Point pos)
    {
        var items = Items;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var origin = item.TranslatePoint(new Point(0, 0), this);
            if (origin == null) continue;
            var rel = pos - origin.Value;
            var h = Math.Max(20, item.Bounds.Height);
            if (rel.Y >= 0 && rel.Y <= h)
            {
                if (item.Tag is HierarchyRow r && r.Item.Kind == SceneItemKind.Actor)
                {
                    if (rel.Y < h * 0.25) return (item, DropPosition.Above);
                    if (rel.Y > h * 0.75) return (item, DropPosition.Below);
                    return (item, DropPosition.Inside);
                }
                return (item, DropPosition.Root);
            }
        }
        return (null, DropPosition.Root);
    }

    /// <summary>The actor a drop on this row would act on, or -1 when the row cannot take the dragged actor (itself or
    /// one of its own children).</summary>
    int DropTarget(TreeItem? item)
    {
        if (item?.Tag is not HierarchyRow { Item.Kind: SceneItemKind.Actor } r) return -1;
        return r.Item.Index == _dragActorIndex || _m.IsDescendantOf(r.Item.Index, _dragActorIndex) ? -1 : r.Item.Index;
    }

    void UpdateDropFeedback(Point pos)
    {
        var (item, dropPos) = HitTestRow(pos);
        _currentDropPos = dropPos;
        _dropLine.IsVisible = _dropDot.IsVisible = _dropBox.IsVisible = false;
        var name = (item?.Tag as HierarchyRow)?.Label;
        if (dropPos != DropPosition.Root && DropTarget(item) < 0)
        {
            DragGhost.SetHint("Can't drop on itself or its children", refused: true);
            return;
        }
        switch (dropPos)
        {
            case DropPosition.Inside:
                PlaceBox(item!);
                DragGhost.SetHint("Child of " + name);
                break;
            case DropPosition.Above:
                PlaceLine(item!.TranslatePoint(default, this)!.Value.Y, item!.Depth);
                DragGhost.SetHint("Above " + name);
                break;
            case DropPosition.Below:
                // a sibling goes after the whole subtree of the row, so the line is drawn there
                var last = LastOfSubtree(item!);
                PlaceLine(last.TranslatePoint(new Point(0, last.Bounds.Height), this)!.Value.Y, item!.Depth);
                DragGhost.SetHint("Below " + name);
                break;
            default:
                if (Items.LastOrDefault() is { } end) PlaceLine(end.TranslatePoint(new Point(0, end.Bounds.Height), this)!.Value.Y, 0);
                DragGhost.SetHint("Scene root");
                break;
        }
    }

    TreeItem LastOfSubtree(TreeItem row)
    {
        var items = Items;
        var last = row;
        for (var i = items.ToList().IndexOf(row) + 1; i < items.Count && items[i].Depth > row.Depth; i++) last = items[i];
        return last;
    }

    void PlaceLine(double y, int depth)
    {
        var x = _rows.Margin.Left + TreeItem.IndentOf(depth);
        var width = Math.Max(16, Bounds.Width - x - _rows.Margin.Right);
        Canvas.SetLeft(_dropLine, x);
        Canvas.SetTop(_dropLine, y - 1);
        _dropLine.Width = width;
        Canvas.SetLeft(_dropDot, x - 3);
        Canvas.SetTop(_dropDot, y - 3);
        _dropLine.IsVisible = _dropDot.IsVisible = true;
    }

    void PlaceBox(TreeItem row)
    {
        var at = row.TranslatePoint(default, this)!.Value;
        Canvas.SetLeft(_dropBox, at.X);
        Canvas.SetTop(_dropBox, at.Y);
        _dropBox.Width = row.Bounds.Width;
        _dropBox.Height = row.Bounds.Height;
        _dropBox.IsVisible = true;
    }

    void ClearDropFeedback()
    {
        _dropLine.IsVisible = _dropDot.IsVisible = _dropBox.IsVisible = false;
        _currentDropPos = DropPosition.None;
    }

    public void ApplyDrop(Point pos)
    {
        var (item, dropPos) = HitTestRow(pos);
        if (_dragActorIndex < 0 || _m.Scene == null || _dragActorIndex >= _m.Scene.Actors.Count) return;

        if (item?.Tag is HierarchyRow r && r.Item.Kind == SceneItemKind.Actor)
        {
            var targetIdx = DropTarget(item);
            if (targetIdx < 0) return;

            switch (dropPos)
            {
                case DropPosition.Inside:
                    ReparentActor(_dragActorIndex, targetIdx, worldPositionStays: true);
                    break;
                case DropPosition.Above:
                    _m.ReorderAndReparent(_dragActorIndex, targetIdx, insertBefore: true, worldPositionStays: true);
                    break;
                case DropPosition.Below:
                    _m.ReorderAndReparent(_dragActorIndex, targetIdx, insertBefore: false, worldPositionStays: true);
                    break;
            }
        }
        else
        {
            _m.UnparentActor(_dragActorIndex, worldPositionStays: true);
        }
    }

    public void ReorderActor(int fromIndex, int toIndex) => _m.ReorderActor(fromIndex, toIndex);

    public bool ReparentActor(int actorIndex, int? newParentIndex, bool worldPositionStays = true)
    {
        if (_m.Scene == null || actorIndex < 0 || actorIndex >= _m.Scene.Actors.Count) return false;
        if (newParentIndex == null)
        {
            _m.UnparentActor(actorIndex, worldPositionStays);
            return true;
        }
        var targetIdx = newParentIndex.Value;
        if (targetIdx < 0 || targetIdx >= _m.Scene.Actors.Count || targetIdx == actorIndex) return false;
        if (_m.IsDescendantOf(targetIdx, actorIndex)) return false;

        var targetActor = _m.Scene.Actors[targetIdx];
        _m.SetActorParent(actorIndex, targetActor.Name, worldPositionStays);
        return true;
    }

    public string Query => _search?.Text?.Trim() ?? "";
    public IReadOnlyList<TreeItem> Items => _rows.Children.Select(c => c as TreeItem ?? (c as Panel)?.Children.OfType<TreeItem>().FirstOrDefault()).OfType<TreeItem>().ToList();

    void HookSearch()
    {
        if (_search != null) return;
        _search = this.GetVisualParent()?.GetVisualDescendants().OfType<SearchField>().FirstOrDefault();
        if (_search != null) _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Rebuild(); };
    }

    public static string ActorGlyph(ActorData a) => a.Kind switch
    {
        ActorKind.Player => "user",
        ActorKind.Pickup => "coin",
        ActorKind.TriggerZone => a.Sprite.Length > 0 ? "flag" : "link",
        ActorKind.CameraZone => "camera",
        ActorKind.TextLabel => "type",
        ActorKind.Panel => "square",
        ActorKind.Canvas => "layers",
        _ => "cube",
    };

    static string? ActorMeta(ActorData a)
    {
        if (a.Hidden) return "inactive";
        return a.Kind switch
        {
            ActorKind.TriggerZone when a.Sprite.Length == 0 => "trigger",
            ActorKind.CameraZone => "camera",
            ActorKind.TextLabel => "text",
            ActorKind.Panel => "panel",
            ActorKind.Canvas => "canvas",
            _ => null,
        };
    }

    public static string Parallax(double p) => "×" + p.ToString("0.0#", CultureInfo.InvariantCulture);

    /// <summary>All rows with folders open (search ignores closed folders).</summary>
    public List<HierarchyRow> AllRows()
    {
        var list = new List<HierarchyRow>();
        if (_m.Scene is not { } scene) return list;
        list.Add(new(new SceneItem(SceneItemKind.Scene), _m.SceneName, "map", null, 0, TreeToggle.Open, "scene"));
        list.Add(new(new SceneItem(SceneItemKind.Camera), "Main Camera", "camera", scene.Camera.Follow.Length > 0 ? "follow" : "fixed", 1, TreeToggle.None, "camera"));
        if (_m.Map is { Layers.Count: > 0 } map)
        {
            list.Add(new(new SceneItem(SceneItemKind.Backgrounds), "Backgrounds", "layers", null, 1, TreeToggle.Open, "bg"));
            for (var i = 0; i < map.Layers.Count; i++)
            {
                var l = map.Layers[i];
                var meta = l.Type switch
                {
                    LayerType.CollisionOnly => "collision",
                    LayerType.Tile or LayerType.Affine when Math.Abs(l.ParallaxX - 1) < 0.001 => "tilemap",
                    _ => Parallax(l.ParallaxX),
                };
                list.Add(new(new SceneItem(SceneItemKind.Layer, i), $"BG{l.Bg} · {l.Name}", l.Type == LayerType.Image ? "image" : "tiles", meta, 2, TreeToggle.None, "layer:" + i));
            }
        }
        var childrenOf = new Dictionary<string, List<(ActorData Actor, int Index)>>(StringComparer.Ordinal);
        var actorByName = scene.Actors.ToDictionary(a => a.Name, StringComparer.Ordinal);
        for (var i = 0; i < scene.Actors.Count; i++)
        {
            var a = scene.Actors[i];
            if (!string.IsNullOrEmpty(a.Parent) && actorByName.ContainsKey(a.Parent))
            {
                if (!childrenOf.TryGetValue(a.Parent, out var kids))
                {
                    kids = [];
                    childrenOf[a.Parent] = kids;
                }
                kids.Add((a, i));
            }
        }

        var emitted = new HashSet<int>();

        void EmitActor(ActorData a, int index, int depth)
        {
            if (!emitted.Add(index)) return;
            var hasKids = childrenOf.TryGetValue(a.Name, out var kids) && kids.Count > 0;
            var key = "actor:" + index;
            list.Add(new(SceneItem.ForActor(index), a.Name, ActorGlyph(a), ActorMeta(a), depth, hasKids ? TreeToggle.Open : TreeToggle.None, key, ActorComponents.Of(a, scene)));
            if (hasKids && kids != null)
            {
                foreach (var (child, childIdx) in kids)
                    EmitActor(child, childIdx, depth + 1);
            }
        }

        var groups = scene.Actors.Where(a => a.Group.Length > 0 && (string.IsNullOrEmpty(a.Parent) || !actorByName.ContainsKey(a.Parent)))
            .GroupBy(a => a.Group).Where(g => g.Count() >= 2).ToDictionary(g => g.Key, g => g.Count());
        var foldersDone = false;
        for (var i = 0; i < scene.Actors.Count; i++)
        {
            var a = scene.Actors[i];
            if (emitted.Contains(i)) continue;
            if (!string.IsNullOrEmpty(a.Parent) && actorByName.ContainsKey(a.Parent)) continue;

            if (!groups.ContainsKey(a.Group))
            {
                EmitActor(a, i, 1);
                continue;
            }
            if (foldersDone) continue;
            foldersDone = true;
            foreach (var g in scene.Actors.Select(x => x.Group).Where(groups.ContainsKey).Distinct())
            {
                var members = scene.Actors.Select((x, k) => (x, k)).Where(t => t.x.Group == g && (string.IsNullOrEmpty(t.x.Parent) || !actorByName.ContainsKey(t.x.Parent))).ToList();
                var glyphs = members.Select(t => ActorGlyph(t.x)).Distinct().ToList();
                list.Add(new(new SceneItem(SceneItemKind.Group, -1, g), g, glyphs.Count == 1 && glyphs[0] != "cube" ? glyphs[0] : "folder",
                    groups[g].ToString(CultureInfo.InvariantCulture), 1, TreeToggle.Open, "group:" + g));
                foreach (var (x, k) in members)
                    EmitActor(x, k, 2);
            }
        }
        // groups with more than three members start closed (Coins), smaller ones open (Enemies)
        foreach (var (g, count) in groups)
            if (_seenGroups.Add(g) && count > 3) _closed.Add("group:" + g);
        return list;
    }

    /// <summary>The rows shown: folders closed, search applied.</summary>
    public List<HierarchyRow> VisibleRows()
    {
        var all = AllRows();
        var q = Query;
        if (q.Length > 0)
        {
            var keep = new List<HierarchyRow>();
            for (var i = 0; i < all.Count; i++)
            {
                if (!all[i].Label.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                // ancestors first
                var depth = all[i].Depth;
                var chain = new List<HierarchyRow>();
                for (var k = i - 1; k >= 0 && depth > 0; k--)
                    if (all[k].Depth < depth) { chain.Insert(0, all[k]); depth = all[k].Depth; }
                foreach (var c in chain.Append(all[i])) if (!keep.Contains(c)) keep.Add(c);
            }
            return keep;
        }
        var rows = new List<HierarchyRow>();
        var hideBelow = int.MaxValue;
        foreach (var r in all)
        {
            if (r.Depth > hideBelow) continue;
            hideBelow = int.MaxValue;
            var closed = r.Toggle != TreeToggle.None && _closed.Contains(r.Key);
            rows.Add(closed ? r with { Toggle = TreeToggle.Closed } : r);
            if (closed) hideBelow = r.Depth;
        }
        return rows;
    }

    public void Toggle(string key)
    {
        if (!_closed.Remove(key)) _closed.Add(key);
        Rebuild();
    }

    void Rebuild()
    {
        _rows.Children.Clear();
        _rename = null;
        var sel = _m.Selection;
        foreach (var r in VisibleRows())
        {
            var selected = r.Item.Kind == SceneItemKind.Actor ? _m.IsActorSelected(r.Item.Index)
                : sel == r.Item || (sel is { Kind: SceneItemKind.Group } g && r.Item.Kind == SceneItemKind.Group && g.Name == r.Item.Name);
            var isActorHidden = r.Item.Kind == SceneItemKind.Actor && _m.Scene?.Actors.ElementAtOrDefault(r.Item.Index)?.Hidden == true;
            var item = new TreeItem
            {
                Label = r.Label, Glyph = isActorHidden ? "eye-off" : r.Glyph, Depth = r.Depth, Toggle = r.Toggle, IsSelected = selected,
                Meta = selected && r.Item.Kind == SceneItemKind.Actor ? (isActorHidden ? "Actor (Inactive)" : "Actor") : r.Meta,
                Tag = r, Name = "Row_" + r.Key,
                Opacity = isActorHidden ? 0.55 : 1.0,
                // what the actor is made of: small icons after the name, the full list as the tooltip
                Badges = r.Parts is { } parts ? ActorComponents.Glyphs(parts) : null,
            };
            if (r.Parts is { } all) ToolTip.SetTip(item, new TipContent(ActorComponents.Tooltip(all)));
            item.Activated += (_, e) => OnActivated(item, r, e.ClickCount, e.Modifiers);
            item.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(item).Properties.IsRightButtonPressed)
                {
                    SceneEditTarget.Of(_m).Active = SceneEditTarget.Panel.Scene;
                    if (r.Item.Kind != SceneItemKind.Actor) return;
                    _m.Selection = r.Item;
                    _ = _s.Windows.Open("T-M12", anchor: Items.FirstOrDefault(t => t.Tag is HierarchyRow h && h.Key == r.Key) ?? (Control)this, source: "HierarchyActor");
                    e.Handled = true;
                }
            };
            _rows.Children.Add(item);
        }
    }

    void UpdateSelectionOnly()
    {
        var sel = _m.Selection;
        foreach (var item in _rows.Children.OfType<TreeItem>())
        {
            if (item.Tag is HierarchyRow r)
            {
                var selected = r.Item.Kind == SceneItemKind.Actor ? _m.IsActorSelected(r.Item.Index)
                    : sel == r.Item || (sel is { Kind: SceneItemKind.Group } g && r.Item.Kind == SceneItemKind.Group && g.Name == r.Item.Name);
                item.IsSelected = selected;
                item.Meta = selected && r.Item.Kind == SceneItemKind.Actor ? "Actor" : r.Meta;
            }
        }
    }

    void OnActivated(TreeItem item, HierarchyRow r, int clicks, KeyModifiers mods = KeyModifiers.None)
    {
        SceneEditTarget.Of(_m).Active = SceneEditTarget.Panel.Scene;
        if (r.Item.Kind == SceneItemKind.Actor && clicks == 1 && (mods & KeyModifiers.Control) != 0)
        {
            _m.ToggleActor(r.Item.Index);
            return;
        }
        if (r.Item.Kind == SceneItemKind.Actor && clicks == 1 && (mods & KeyModifiers.Shift) != 0)
        {
            SelectRange(r);
            return;
        }
        var pointerX = _lastPointerX;
        var inToggle = r.Toggle != TreeToggle.None && pointerX >= 0 && pointerX < TreeItem.IndentOf(r.Depth) + 16;
        if (inToggle || (clicks == 2 && r.Toggle != TreeToggle.None))
        {
            Toggle(r.Key);
            return;
        }
        if (clicks == 2 && r.Item.Kind == SceneItemKind.Actor)
        {
            _m.Selection = r.Item;
            SceneCommands.FocusSelection(_m);
            return;
        }
        if (clicks == 2 && r.Item.Kind is SceneItemKind.Layer or SceneItemKind.Backgrounds && _m.MapPath != null)
        {
            _ = _s.Windows.Open("T-P03", _m.MapPath, source: "HierarchyLayer");
            return;
        }
        _m.Selection = r.Item;
    }

    double _lastPointerX = -1;

    /// <summary>Shift + click: the actors of the rows shown from the first selected actor to the clicked one.</summary>
    void SelectRange(HierarchyRow to)
    {
        var rows = VisibleRows();
        var anchor = _m.SelectedActorIndex;
        var from = rows.FindIndex(x => x.Item.Kind == SceneItemKind.Actor && x.Item.Index == anchor);
        var end = rows.FindIndex(x => x.Key == to.Key);
        if (from < 0 || end < 0) { _m.SelectActor(to.Item.Index); return; }
        var (a, b) = from <= end ? (from, end) : (end, from);
        _m.SelectActors(rows.Skip(a).Take(b - a + 1).Where(x => x.Item.Kind == SceneItemKind.Actor).Select(x => x.Item.Index), anchor);
    }

    // ---- inline rename (F2, Actor Context Menu › Rename) ----------------------------------------------

    public TextBox? RenameBox => _rename;

    public void BeginRename()
    {
        var index = _m.SelectedActorIndex;
        if (index < 0) return;
        var key = "actor:" + index;
        var at = _rows.Children.ToList().FindIndex(c => c is TreeItem { Tag: HierarchyRow r } && r.Key == key);
        if (at < 0) return;
        var row = (TreeItem)_rows.Children[at];
        var box = new TextBox
        {
            Text = _m.Scene!.Actors[index].Name, Name = "RenameBox", Height = 24,
            Margin = new Thickness(TreeItem.IndentOf(row.Depth) + 20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("FieldBox"),
        };
        AgbStudio.Editor.Theme.Fig.SetStyle(box, "Body/12");
        _rows.Children.RemoveAt(at);
        var wrap = new Panel { Children = { row, box } };
        _rows.Children.Insert(at, wrap);
        _rename = box;
        var done = false;
        void Commit(bool keep)
        {
            if (done) return;
            done = true;
            if (keep) _m.RenameActor(index, box.Text ?? "");
            else Rebuild();
        }
        box.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) { Commit(true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Commit(false); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        box.LostFocus += (_, _) => Commit(true);
        // the box is in the tree already: take the keyboard once it has its template
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { box.Focus(); box.SelectAll(); }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    // ---- IEditTarget (Edit menu while the Hierarchy has focus) ------------------------------------------

    SceneEditTarget Target
    {
        get
        {
            var t = SceneEditTarget.Of(_m);
            t.Active = SceneEditTarget.Panel.Scene;
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
