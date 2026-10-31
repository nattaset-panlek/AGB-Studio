using System.Globalization;
using AgbStudio.Core.Project;
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

/// <summary>One row of the Hierarchy.</summary>
public sealed record HierarchyRow(SceneItem Item, string Label, string Glyph, string? Meta, int Depth, TreeToggle Toggle, string Key);

/// <summary>
/// Hierarchy (T-P01, P11.12): the scene, its camera, background layers and actors (actors of a group with two or more
/// members under a folder), filtered by the workspace's "Search scene…" field. Click selects (in sync with the
/// viewport and Inspector), Ctrl + click adds an actor to the selection or takes it out, Shift + click selects the
/// actors from the first selected one to the clicked one, the chevron or a double click opens and closes folders, a
/// double click on an actor focuses it in the viewport, right click opens the Actor Context Menu (T-M12), F2 renames in
/// place.
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
        _m.Changed += Rebuild;
        _m.SelectionChanged += Rebuild;
        SceneEditTarget.Of(_m).RenameRequested += p => { if (p == SceneEditTarget.Panel.Scene) BeginRename(); };
        AttachedToVisualTree += (_, _) => HookSearch();
        // where in the row the pointer went down (the chevron opens and closes folders)
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is Visual v && v.FindAncestorOfType<TreeItem>(true) is { } t) _lastPointerX = e.GetPosition(t).X;
        }, RoutingStrategies.Tunnel);
        Rebuild();
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
        _ => "cube",
    };

    static string? ActorMeta(ActorData a) => a.Kind switch
    {
        ActorKind.TriggerZone when a.Sprite.Length == 0 => "trigger",
        ActorKind.CameraZone => "camera",
        ActorKind.TextLabel => "text",
        _ => null,
    };

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
        var groups = scene.Actors.Where(a => a.Group.Length > 0).GroupBy(a => a.Group).Where(g => g.Count() >= 2).ToDictionary(g => g.Key, g => g.Count());
        // Actors in scene (spawn) order; grouped actors sit in their folder and the folders are listed together where
        // the first grouped actor is (Player, Enemies, Coins, Question Block ...).
        var foldersDone = false;
        for (var i = 0; i < scene.Actors.Count; i++)
        {
            var a = scene.Actors[i];
            if (!groups.ContainsKey(a.Group))
            {
                list.Add(new(SceneItem.ForActor(i), a.Name, ActorGlyph(a), ActorMeta(a), 1, TreeToggle.None, "actor:" + i));
                continue;
            }
            if (foldersDone) continue;
            foldersDone = true;
            foreach (var g in scene.Actors.Select(x => x.Group).Where(groups.ContainsKey).Distinct())
            {
                var members = scene.Actors.Select((x, k) => (x, k)).Where(t => t.x.Group == g).ToList();
                var glyphs = members.Select(t => ActorGlyph(t.x)).Distinct().ToList();
                list.Add(new(new SceneItem(SceneItemKind.Group, -1, g), g, glyphs.Count == 1 && glyphs[0] != "cube" ? glyphs[0] : "folder",
                    groups[g].ToString(CultureInfo.InvariantCulture), 1, TreeToggle.Open, "group:" + g));
                foreach (var (x, k) in members)
                    list.Add(new(SceneItem.ForActor(k), x.Name, ActorGlyph(x), ActorMeta(x), 2, TreeToggle.None, "actor:" + k));
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
            var item = new TreeItem
            {
                Label = r.Label, Glyph = r.Glyph, Depth = r.Depth, Toggle = r.Toggle, IsSelected = selected,
                Meta = selected && r.Item.Kind == SceneItemKind.Actor ? "Actor" : r.Meta,
                Tag = r, Name = "Row_" + r.Key,
            };
            item.Activated += (_, e) => OnActivated(item, r, e.ClickCount, e.Modifiers);
            item.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(item).Properties.IsRightButtonPressed) return;
                SceneEditTarget.Of(_m).Active = SceneEditTarget.Panel.Scene;
                if (r.Item.Kind != SceneItemKind.Actor) return;
                _m.Selection = r.Item;
                _ = _s.Windows.Open("T-M12", anchor: Items.FirstOrDefault(t => t.Tag is HierarchyRow h && h.Key == r.Key) ?? (Control)this, source: "HierarchyActor");
                e.Handled = true;
            };
            _rows.Children.Add(item);
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
