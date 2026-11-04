using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Docking;

/// <summary>One panel the host can show. With <see cref="Header"/> it shows that header when alone in its group, and a
/// tab next to other panels. <see cref="ActivateOnly"/> is a tab that runs an action instead of showing content.</summary>
public sealed class DockPanelDef
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Glyph { get; init; }
    public string? TabName { get; init; }
    public string FrameName { get; init; } = "BottomPanel";
    public Control? Header { get; init; }
    /// <summary>Makes the content. Called once when <see cref="Keep"/>, else each time the panel is selected.</summary>
    public Func<Control?>? Content { get; init; }
    public bool Keep { get; init; }
    public Action? ActivateOnly { get; init; }
}

/// <summary>
/// Shows a <see cref="DockLayout"/> and edits it by drag and drop: drag a tab or a panel header onto a group's middle to
/// tab it, onto an edge to split, outside the workspace to float it in its own window. Splitters resize; Esc cancels.
/// </summary>
public sealed class DockHost : Panel
{
    const double DragStart = 6, EdgeShare = 0.25;

    readonly Dictionary<string, DockPanelDef> _defs;
    readonly Dictionary<string, PanelTab> _tabs = [];
    readonly Dictionary<string, Control> _kept = [];
    readonly Dictionary<string, Window> _floats = [];
    readonly HashSet<string> _hidden = [];
    readonly Dictionary<DockNode, (Control Frame, Control? TabRow, ContentControl? Content)> _groups = [];
    readonly Func<Control> _documentView;
    readonly Canvas _overlay = new() { IsHitTestVisible = false, Name = "DockOverlay" };
    readonly Border _dropMark = new() { IsVisible = false, Name = "DockDropMark", CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1) };
    Control? _root;
    string? _pressed, _dragging;
    Point _pressedAt;
    Control? _captured;
    TopLevel? _escapeFrom;

    public DockLayout Layout { get; private set; }

    /// <summary>The layout changed by a move, a float or a splitter (save it).</summary>
    public event Action? LayoutChanged;
    /// <summary>A panel became the selected tab of its group by a click.</summary>
    public event Action<string, PanelTab>? PanelSelected;

    public DockHost(DockLayout layout, IEnumerable<DockPanelDef> panels, Func<Control> documentView)
    {
        Layout = layout;
        _defs = panels.ToDictionary(p => p.Id);
        _documentView = documentView;
        Name = "Body";
        _dropMark.Token(Border.BackgroundProperty, "accent/subtle").Token(Border.BorderBrushProperty, "accent/default");
        _overlay.Children.Add(_dropMark);
        WatchDrag(this);
        Rebuild();
        Windows.Tilemap.PageChrome.WhileAttached(this, ShowFloats, HideFloats);
    }

    public PanelTab Tab(string id)
    {
        if (_tabs.TryGetValue(id, out var t)) return t;
        var d = _defs[id];
        t = new PanelTab { Label = d.Title, Glyph = d.Glyph, Name = d.TabName ?? "Tab_" + d.Title, Tag = id };
        t.Activated += (_, _) => Select(id);
        return _tabs[id] = t;
    }

    public void Select(string id)
    {
        var d = _defs[id];
        if (d.ActivateOnly != null) { d.ActivateOnly(); return; }
        var g = Layout.GroupOf(id);
        if (g == null) return;
        g.Active = id;
        ShowActive(g);
        PanelSelected?.Invoke(id, Tab(id));
    }

    /// <summary>Replaces the layout (Default, a saved one) and shows it.</summary>
    public void Apply(DockLayout layout)
    {
        foreach (var w in _floats.Values.ToList()) CloseFloat(w);
        _floats.Clear();
        Layout = layout;
        Rebuild();
        ShowFloats();
        LayoutChanged?.Invoke();
    }

    public bool Move(string panel, string target, DockZone zone, int index = -1)
    {
        if (!Layout.Move(panel, target, zone, index)) return false;
        Changed();
        return true;
    }

    public bool Float(string panel, double x, double y)
    {
        if (!Layout.Float(panel, x, y)) return false;
        Changed();
        return true;
    }

    public void Dock(string panel)
    {
        Layout.Dock(panel);
        Changed();
    }

    public Window? FloatWindow(string panel) => _floats.GetValueOrDefault(panel);

    /// <summary>Leaves these panels out of the view without changing the layout; the rest keep their place and size.</summary>
    public void Hide(params string[] panels)
    {
        if (_hidden.SetEquals(panels)) return;
        _hidden.Clear();
        _hidden.UnionWith(panels);
        Rebuild();
    }

    IEnumerable<string> Shown(DockNode g) => g.Panels.Where(p => !_hidden.Contains(p));

    void Changed()
    {
        Rebuild();
        ShowFloats();
        LayoutChanged?.Invoke();
    }

    // ---- building --------------------------------------------------------------------------------------

    void Rebuild()
    {
        // what stays alive between builds leaves its old parent first
        foreach (var w in _floats.Where(kv => Layout.Floats.All(f => f.Panel != kv.Key)).ToList())
        {
            CloseFloat(w.Value);
            _floats.Remove(w.Key);
        }
        foreach (var d in _defs.Values)
            if (d.Header != null) Detach(d.Header);
        foreach (var c in _kept.Values) Detach(c);
        foreach (var t in _tabs.Values) Detach(t);
        _groups.Clear();
        Children.Clear();
        _root = Build(Layout.Root);
        Children.Add(_root);
        Children.Add(_overlay);
    }

    static void Detach(Control c)
    {
        switch (c.Parent)
        {
            case Panel p: p.Children.Remove(c); break;
            case ContentControl cc: cc.Content = null; break;
            case Decorator d: d.Child = null; break;
        }
    }

    Control Build(DockNode n, DockNode? parent = null, int index = 0)
    {
        if (n.IsGroup) return BuildGroup(n, parent, index);
        var grid = new Grid { Name = parent == null ? "DockRoot" : n.Holds(DockLayout.Viewport) ? "Center" : null };
        var shown = n.Children.Where(c => c.Groups().Any(g => Shown(g).Any())).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var c = shown[i];
            var size = c.Size > 0 ? new GridLength(c.Size) : GridLength.Star;
            if (n.IsColumns) grid.ColumnDefinitions.Add(new ColumnDefinition(size) { MinWidth = 80 });
            else grid.RowDefinitions.Add(new RowDefinition(size) { MinHeight = 60 });
            var child = Build(c, n, n.Children.IndexOf(c));
            if (n.IsColumns) Grid.SetColumn(child, i); else Grid.SetRow(child, i);
            grid.Children.Add(child);
        }
        for (var i = 1; i < shown.Count; i++) grid.Children.Add(Splitter(n, shown, grid, i));
        return grid;
    }

    GridSplitter Splitter(DockNode n, List<DockNode> shown, Grid grid, int i)
    {
        var g = new GridSplitter { Background = Brushes.Transparent };
        if (n.IsColumns) { Grid.SetColumn(g, i); g.Width = 4; g.HorizontalAlignment = HorizontalAlignment.Left; g.ResizeDirection = GridResizeDirection.Columns; }
        else { Grid.SetRow(g, i); g.Height = 4; g.VerticalAlignment = VerticalAlignment.Top; g.ResizeDirection = GridResizeDirection.Rows; }
        g.DragCompleted += (_, _) =>
        {
            for (var k = 0; k < shown.Count; k++)
                if (shown[k].Size > 0)
                    shown[k].Size = n.IsColumns ? grid.ColumnDefinitions[k].ActualWidth : grid.RowDefinitions[k].ActualHeight;
            LayoutChanged?.Invoke();
        };
        return g;
    }

    Control BuildGroup(DockNode g, DockNode? parent, int index)
    {
        if (g.Panels.Contains(DockLayout.Viewport))
        {
            var doc = _documentView();
            Detach(doc);
            _groups[g] = (doc, null, null);
            return doc;
        }
        var dock = new DockPanel();
        Control? tabRow = null;
        var content = new ContentControl();
        var panels = Shown(g).ToList();
        var single = panels.Count == 1 ? _defs[panels[0]] : null;
        if (single?.Header != null)
        {
            single.Header.Tag = single.Id;
            DockPanel.SetDock(single.Header, Avalonia.Controls.Dock.Top);
            dock.Children.Add(single.Header);
        }
        else
        {
            var row = new Border { Height = 33, Padding = new Thickness(6, 0), Child = UI.Row(2, panels.Select(Tab).ToArray<Control>()) }.Token(Border.BorderBrushProperty, "border/default");
            row.BorderThickness = new Thickness(0, 0, 0, 1);
            DockPanel.SetDock(row, Avalonia.Controls.Dock.Top);
            dock.Children.Add(row);
            tabRow = row;
        }
        dock.Children.Add(content);
        var frame = UI.Frame(dock, "bg/panel", "border/default", Edge(g, parent, index)).Named(_defs[panels[0]].FrameName);
        frame.ClipToBounds = true;
        _groups[g] = (frame, tabRow, content);
        ShowActive(g);
        return frame;
    }

    /// <summary>One line between two neighbours: drawn by the group, on the side of the neighbour that holds the
    /// document or else on its far side, as in the Figma frame.</summary>
    static Thickness Edge(DockNode g, DockNode? parent, int index)
    {
        if (parent == null) return default;
        var prev = index > 0;
        var next = index < parent.Children.Count - 1;
        var before = prev ? 1 : 0;
        var after = next && !parent.Children[index + 1].Holds(DockLayout.Viewport) ? 0 : next ? 1 : 0;
        return parent.IsColumns ? new Thickness(before, 0, after, 0) : new Thickness(0, before, 0, after);
    }

    void ShowActive(DockNode g)
    {
        if (!_groups.TryGetValue(g, out var view) || view.Content == null) return;
        var panels = Shown(g).ToList();
        var id = g.Active != null && panels.Contains(g.Active) && _defs[g.Active].ActivateOnly == null
            ? g.Active
            : panels.FirstOrDefault(p => _defs[p].ActivateOnly == null);
        foreach (var p in panels) Tab(p).IsSelected = p == id;
        view.Content.Content = id == null ? null : ContentOf(id);
    }

    Control? ContentOf(string id)
    {
        var d = _defs[id];
        if (!d.Keep) return d.Content?.Invoke();
        if (!_kept.TryGetValue(id, out var c))
        {
            c = d.Content?.Invoke() ?? new Panel();
            _kept[id] = c;
        }
        Detach(c);
        return c;
    }

    // ---- floating windows --------------------------------------------------------------------------------

    void ShowFloats()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        foreach (var f in Layout.Floats)
        {
            if (_floats.TryGetValue(f.Panel, out var open))
            {
                if (!open.IsVisible) open.Show(owner);
                continue;
            }
            var w = MakeFloat(f);
            _floats[f.Panel] = w;
            w.Show(owner);
        }
    }

    void HideFloats()
    {
        foreach (var w in _floats.Values) w.Hide();
    }

    Window MakeFloat(DockFloat f)
    {
        var d = _defs[f.Panel];
        var dock = new DockPanel();
        var tab = Tab(f.Panel);
        Detach(tab);
        var row = new Border { Height = 33, Padding = new Thickness(6, 0), Child = UI.Row(2, tab) }.Token(Border.BorderBrushProperty, "border/default");
        row.BorderThickness = new Thickness(0, 0, 0, 1);
        tab.IsSelected = true;
        DockPanel.SetDock(row, Avalonia.Controls.Dock.Top);
        dock.Children.Add(row);
        dock.Children.Add(new ContentControl { Content = ContentOf(f.Panel) });
        var w = new Window
        {
            Title = d.Title,
            Width = f.Width,
            Height = f.Height,
            Position = new PixelPoint((int)f.X, (int)f.Y),
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            Content = UI.Frame(dock, "bg/panel"),
            Name = "Float_" + d.Title,
        }.Token(TemplatedControl_Background, "bg/panel");
        w.Tag = f.Panel;
        WatchDrag(w);
        w.PositionChanged += (_, e) => { f.X = e.Point.X; f.Y = e.Point.Y; };
        w.Resized += (_, _) => { f.Width = w.ClientSize.Width; f.Height = w.ClientSize.Height; };
        w.Closing += (_, e) =>
        {
            // the user closed the window: the panel goes back; closing the app keeps it floating for next time
            if (e.CloseReason != WindowCloseReason.WindowClosing || w.Tag == null || !_floats.ContainsKey(f.Panel)) return;
            e.Cancel = true;
            Dispatcher_Post(() => Dock(f.Panel));
        };
        return w;
    }

    static readonly AvaloniaProperty TemplatedControl_Background = Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty;

    static void Dispatcher_Post(Action a) => Avalonia.Threading.Dispatcher.UIThread.Post(a);

    static void CloseFloat(Window w)
    {
        w.Tag = null;
        if (w.Content is Control c) Release(c);
        w.Close();
    }

    /// <summary>Kept panels and tabs must leave the window before it goes.</summary>
    static void Release(Control c)
    {
        if (c.Parent is Window w) w.Content = null;
    }

    // ---- drag and drop -----------------------------------------------------------------------------------

    void WatchDrag(Control root)
    {
        root.AddHandler(PointerPressedEvent, OnPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        root.AddHandler(PointerMovedEvent, OnMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        root.AddHandler(PointerReleasedEvent, OnReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        root.AddHandler(PointerCaptureLostEvent, (_, _) => { if (_dragging != null) EndDrag(); });
    }

    /// <summary>The panel a press on this control would drag: a tab or a panel header, not the header's button.</summary>
    string? HandleOf(object? source)
    {
        for (var v = source as Visual; v != null; v = v.GetVisualParent())
        {
            if (v is Button) return null;
            if (v is PanelTab { Tag: string t } && _defs.TryGetValue(t, out var d) && d.ActivateOnly == null) return t;
            if (v is PanelHeader { Tag: string h } && _defs.ContainsKey(h)) return h;
        }
        return null;
    }

    Point ToHost(PointerEventArgs e)
    {
        if (e.Source is Visual v && TopLevel.GetTopLevel(v) is { } top && top != TopLevel.GetTopLevel(this) && TopLevel.GetTopLevel(this) is { } mine)
            {
            var client = mine.PointToClient(top.PointToScreen(e.GetPosition(top)));
            return mine.TranslatePoint(client, this) ?? client;
        }
        return e.GetPosition(this);
    }

    void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressed = HandleOf(e.Source);
        _pressedAt = ToHost(e);
    }

    void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed == null) return;
        var at = ToHost(e);
        if (_dragging == null)
        {
            if (Math.Abs(at.X - _pressedAt.X) < DragStart && Math.Abs(at.Y - _pressedAt.Y) < DragStart) return;
            _dragging = _pressed;
            _captured = e.Source as Control;
            e.Pointer.Capture(_captured);
            DragGhost.Show(this, e, _defs[_dragging].Glyph, _defs[_dragging].Title);
            // the keyboard goes to the window, wherever the focus is
            _escapeFrom = TopLevel.GetTopLevel(this);
            _escapeFrom?.AddHandler(KeyDownEvent, OnDragKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            SetDimmed(_dragging, true);
        }
        if (e.Source is Visual from) DragGhost.Move(from, e);
        Mark(TargetAt(at));
        e.Handled = true;
    }

    void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var panel = _dragging;
        _pressed = null;
        if (panel == null) return;
        var at = ToHost(e);
        var drop = TargetAt(at);
        e.Pointer.Capture(null);
        EndDrag();
        e.Handled = true;
        if (drop is { } d) Move(panel, d.Target, d.Zone, d.Index);
        else if (!new Rect(Bounds.Size).Contains(at) && Layout.Floats.All(f => f.Panel != panel) && TopLevel.GetTopLevel(this) is { } top)
        {
            var p = top.PointToScreen(e.GetPosition(top));
            Float(panel, p.X, p.Y);
        }
    }

    /// <summary>The tab or header being dragged stays in place, faded, until the drop.</summary>
    void SetDimmed(string panel, bool dimmed)
    {
        var opacity = dimmed ? 0.45 : 1;
        if (_tabs.TryGetValue(panel, out var t)) t.Opacity = opacity;
        if (_defs[panel].Header is { } h) h.Opacity = opacity;
    }

    void OnDragKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        EndDrag();
        e.Handled = true;
    }

    void EndDrag()
    {
        _escapeFrom?.RemoveHandler(KeyDownEvent, OnDragKey);
        _escapeFrom = null;
        if (_dragging != null) SetDimmed(_dragging, false);
        DragGhost.Hide();
        _dragging = null;
        _pressed = null;
        _captured = null;
        _dropMark.IsVisible = false;
    }

    /// <summary>What a drop at this point of the host would do, or null outside the workspace.</summary>
    public (string Target, DockZone Zone, int Index, Rect Mark)? TargetAt(Point at)
    {
        foreach (var (g, view) in _groups)
        {
            var origin = view.Frame.TranslatePoint(default, this);
            if (origin == null) continue;
            var r = new Rect(origin.Value, view.Frame.Bounds.Size);
            if (!r.Contains(at)) continue;
            var target = g.Panels.First();
            var document = g.Panels.Contains(DockLayout.Viewport);
            if (view.TabRow is { } row && at.Y < r.Y + row.Bounds.Height) return (target, DockZone.Center, TabIndexAt(g, at), r);
            var dx = (at.X - r.X) / r.Width;
            var dy = (at.Y - r.Y) / r.Height;
            var edge = Math.Min(Math.Min(dx, 1 - dx), Math.Min(dy, 1 - dy));
            if (edge >= EdgeShare)
            {
                if (document) continue;
                return (target, DockZone.Center, -1, r);
            }
            if (edge == dx) return (target, DockZone.Left, -1, new Rect(r.X, r.Y, r.Width / 2, r.Height));
            if (edge == 1 - dx) return (target, DockZone.Right, -1, new Rect(r.Center.X, r.Y, r.Width / 2, r.Height));
            if (edge == dy) return (target, DockZone.Top, -1, new Rect(r.X, r.Y, r.Width, r.Height / 2));
            return (target, DockZone.Bottom, -1, new Rect(r.X, r.Center.Y, r.Width, r.Height / 2));
        }
        return null;
    }

    int TabIndexAt(DockNode g, Point at)
    {
        for (var i = 0; i < g.Panels.Count; i++)
        {
            if (!_tabs.TryGetValue(g.Panels[i], out var t) || t.TranslatePoint(new Point(t.Bounds.Width / 2, 0), this) is not { } mid) continue;
            if (at.X < mid.X) return i;
        }
        return g.Panels.Count;
    }

    void Mark((string Target, DockZone Zone, int Index, Rect Mark)? drop)
    {
        _dropMark.IsVisible = drop != null;
        if (drop is not { } d) return;
        Canvas.SetLeft(_dropMark, d.Mark.X);
        Canvas.SetTop(_dropMark, d.Mark.Y);
        _dropMark.Width = d.Mark.Width;
        _dropMark.Height = d.Mark.Height;
    }
}
