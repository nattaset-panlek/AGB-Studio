using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Scripting.Graph;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.LogicGraph;

public enum GraphTool { Select, Hand, Link }

/// <summary>
/// The Logic Graph canvas (T-P17, P11.48): dot grid, comment frames, nodes (Node Header and Node Pin components with
/// property rows), wires coloured by pin type, selection and dragging, wiring by dragging from a pin, zoom with the wheel
/// or the − / 100% / + / Fit bar, pan with the hand tool or the middle button, and the minimap. Edits go through the
/// page (undoable). Right-click opens Add Node (canvas) or the Node Context Menu (node).
/// </summary>
public sealed class GraphCanvas : Panel, IEditTarget
{
    readonly LogicGraphPage _page;
    readonly Canvas _world = new() { Name = "GraphWorld", IsHitTestVisible = false };
    readonly BackLayer _back;
    readonly WireLayer _wires;
    readonly DotGrid _grid;
    readonly Dictionary<string, Panel> _nodeViews = [];
    readonly TextBlock _zoomText = UI.Text("100%", "Label/11", "text/primary");
    readonly MiniMap _mini;
    Vector _pan = new(0, 0);
    double _zoom = 1;
    // drag state
    enum Drag { None, Nodes, Pan, Wire, Comment }
    Drag _drag;
    Point _dragStart;
    Vector _panStart;
    Dictionary<string, Point> _moveStart = [];
    string? _beforeJson;
    (string Node, string Pin, bool Output, PinKind Kind)? _wireFrom;
    Point _wireEnd;
    GraphComment? _dragComment;
    List<GraphNode> _clipboard = [];

    public HashSet<string> Selection { get; } = new(StringComparer.Ordinal);
    public GraphTool Tool { get; set; } = GraphTool.Select;
    public double Zoom => _zoom;
    public Vector Pan => _pan;
    public event Action? SelectionChanged;
    GraphDocument Doc => _page.Document;

    public GraphCanvas(LogicGraphPage page)
    {
        _page = page;
        Name = "GraphCanvas";
        Focusable = true;
        ClipToBounds = true;
        this.Token(BackgroundProperty, "bg/canvas");
        _grid = new DotGrid(this) { IsHitTestVisible = false };
        _back = new BackLayer(this);
        _wires = new WireLayer(this);
        _world.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        Children.Add(_grid);
        Children.Add(_world);
        _mini = new MiniMap(this) { Width = 160, Height = 106, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 16, 20), Name = "MiniMap" };
        Children.Add(Tip());
        Children.Add(ZoomBar());
        Children.Add(_mini);
        ApplyTransform();
    }

    // ---- view transform ------------------------------------------------------------------------------------

    void ApplyTransform()
    {
        _world.RenderTransform = new MatrixTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_pan));
        _zoomText.Text = $"{Math.Round(_zoom * 100)}%";
        _grid.InvalidateVisual();
        _mini.InvalidateVisual();
    }

    public Point ToWorld(Point screen) => new((screen.X - _pan.X) / _zoom, (screen.Y - _pan.Y) / _zoom);
    public Point ToScreen(Point world) => new(world.X * _zoom + _pan.X, world.Y * _zoom + _pan.Y);

    public void SetZoom(double zoom, Point? around = null)
    {
        zoom = Math.Clamp(zoom, 0.25, 2);
        var c = around ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var w = ToWorld(c);
        _zoom = zoom;
        _pan = new Vector(c.X - w.X * zoom, c.Y - w.Y * zoom);
        ApplyTransform();
    }

    public void ZoomStep(int dir)
    {
        double[] steps = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2];
        var next = dir > 0 ? steps.FirstOrDefault(s => s > _zoom + 0.001, 2) : steps.LastOrDefault(s => s < _zoom - 0.001, 0.25);
        SetZoom(next);
    }

    /// <summary>Fit: every node in view with a margin.</summary>
    public void Fit()
    {
        var nodes = Doc.AllNodes.ToList();
        if (nodes.Count == 0 || Bounds.Width <= 0) { _zoom = 1; _pan = default; ApplyTransform(); return; }
        var r = nodes.Select(Doc.Bounds).Aggregate((a, b) => a.Union(b));
        foreach (var c in Doc.Comments) r = r.Union(new Rect(c.X, c.Y, c.W, c.H));
        var zoom = Math.Clamp(Math.Min((Bounds.Width - 80) / r.Width, (Bounds.Height - 160) / r.Height), 0.25, 1);
        _zoom = zoom;
        _pan = new Vector(40 - r.X * zoom + (Bounds.Width - 80 - r.Width * zoom) / 2, 40 - r.Y * zoom);
        ApplyTransform();
    }

    public void SetView(double zoom, Vector pan)
    {
        _zoom = zoom;
        _pan = pan;
        ApplyTransform();
    }

    Control ZoomBar()
    {
        var minus = new AgbButton { Content = "−", Kind = ButtonKind.Ghost, Name = "ZoomOut", Padding = new Thickness(10, 0) };
        minus.Click += (_, _) => ZoomStep(-1);
        var plus = new AgbButton { Content = "+", Kind = ButtonKind.Ghost, Name = "ZoomIn", Padding = new Thickness(10, 0) };
        plus.Click += (_, _) => ZoomStep(1);
        var fit = new AgbButton { Content = "Fit", Kind = ButtonKind.Ghost, Name = "ZoomFit", Padding = new Thickness(10, 0) };
        fit.Click += (_, _) => Fit();
        var level = UI.Frame(_zoomText, "bg/hover", pad: new Thickness(10, 0), radius: 5);
        level.Height = 24;
        level.Cursor = new Cursor(StandardCursorType.Hand);
        level.PointerPressed += (_, e) => { SetZoom(1); e.Handled = true; };
        var bar = UI.Frame(UI.Row(2, minus, level, plus, fit), "bg/panel", "border/default", new Thickness(1), new Thickness(4), 8).Named("ZoomBar");
        bar.HorizontalAlignment = HorizontalAlignment.Left;
        bar.VerticalAlignment = VerticalAlignment.Bottom;
        bar.Margin = new Thickness(16, 0, 0, 16);
        return bar;
    }

    static Control Tip()
    {
        var text = UI.Text("Tip: every node compiles to plain C#, then to C++ for the GBA. Switch to the Code view any time — the graph and the script stay in sync.", "Label/11", "kind/data");
        text.TextWrapping = TextWrapping.Wrap;
        var tip = UI.Frame(text, "lit/chip-warning", "lit/chip-warning", new Thickness(1), new Thickness(12, 9, 8, 9), 6).Named("GraphTip");
        tip.Width = 380;
        tip.HorizontalAlignment = HorizontalAlignment.Left;
        tip.VerticalAlignment = VerticalAlignment.Bottom;
        tip.Margin = new Thickness(24, 0, 0, 102);
        tip.IsHitTestVisible = false;
        return tip;
    }

    // ---- building visuals ------------------------------------------------------------------------------------

    /// <summary>Rebuilds node views and wires from the document.</summary>
    public void Rebuild()
    {
        _world.Children.Clear();
        _nodeViews.Clear();
        _world.Children.Add(_back);
        foreach (var c in Doc.Comments) _world.Children.Add(CommentTitle(c));
        _world.Children.Add(_wires);
        foreach (var n in Doc.AllNodes) _world.Children.Add(_nodeViews[n.Id] = NodeView(n));
        Selection.RemoveWhere(id => Doc.Node(id) is null);
        RefreshSelection();
        _wires.InvalidateVisual();
        _back.InvalidateVisual();
        _mini.InvalidateVisual();
    }

    Control CommentTitle(GraphComment c)
    {
        var row = UI.Row(6, PageChrome.Glyph("clock", 12, "text/muted"), UI.Text(c.Title, "Label/11 Medium", "text/secondary"));
        Canvas.SetLeft(row, c.X + 12);
        Canvas.SetTop(row, c.Y + 8);
        row.IsHitTestVisible = false;
        return row;
    }

    Panel NodeView(GraphNode n)
    {
        var def = NodeCatalog.Find(n.Type);
        var h = Doc.Height(n);
        var w = GraphDocument.WidthOf(n.Type);
        var body = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Name = "Body" };
        body.Token(Border.BackgroundProperty, "lit/pin-hollow");
        body.Token(Border.BorderBrushProperty, "border/default");
        var inner = new Canvas { Width = w - 2, Height = h - 2 };
        var header = new NodeHeader { Category = GraphDocument.CategoryOf(def?.Category ?? "Other"), Title = def?.Title ?? n.Type, Width = w - 2, Height = GraphDocument.HeaderHeight - 1 };
        inner.Children.Add(header);
        if (def != null && !Doc.Collapsed.Contains(n.Id))
        {
            var rows = GraphDocument.Rows(def);
            for (var i = 0; i < rows.Count; i++)
            {
                var y = GraphDocument.HeaderHeight + GraphDocument.BodyPad + i * GraphDocument.RowHeight - 1;
                var row = rows[i];
                if (row.Label != null)
                {
                    var label = UI.Text(row.Label, "Label/11", "text/secondary");
                    label.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                    Canvas.SetLeft(label, 14);
                    Canvas.SetTop(label, y + (GraphDocument.RowHeight - 14) / 2);
                    inner.Children.Add(label);
                }
                var connected = row.Left is { } lp && Doc.AllLinks.Any(l => l.ToNode == n.Id && l.ToPin == lp.Name);
                // an actor input that is not wired acts on the graph's own actor (Figma "Target  Self")
                var self = row.Prop is null && row.Left is { Kind: PinKind.Actor } && !connected;
                if ((row.Prop != null || self) && !connected)
                {
                    var box = UI.Frame(UI.Text(self ? "Self" : GraphDocument.PropText(n, row.Prop!), "Mono/10", "text/primary"), "bg/base", "border/default", new Thickness(1), new Thickness(6, 0), 4);
                    box.Height = 20;
                    box.Name = self ? "Pin_" + row.Left!.Name : "Prop_" + row.Prop;
                    box.Measure(Size.Infinity);
                    Canvas.SetLeft(box, w - 2 - 15 - box.DesiredSize.Width);
                    Canvas.SetTop(box, y + 3);
                    inner.Children.Add(box);
                }
            }
        }
        body.Child = inner;
        var pins = new Canvas();
        foreach (var p in Doc.Pins(n))
        {
            var pin = new NodePin { Type = (Controls.PinType)(int)p.Kind, Dir = p.Output ? PinDir.Out : PinDir.In };
            var size = p.Kind == PinKind.Exec ? 12 : 10;
            // exec arrows sit just inside / outside the edge; value pins are centred on it
            var x = p.Kind == PinKind.Exec ? (p.Output ? p.Offset.X - 4 : p.Offset.X - 6) : p.Offset.X - size / 2.0;
            Canvas.SetLeft(pin, x);
            Canvas.SetTop(pin, p.Offset.Y - size / 2.0);
            pins.Children.Add(pin);
        }
        var view = new Panel { Width = w, Height = h, Name = "Node_" + n.Id, Tag = n.Id, IsHitTestVisible = false, Children = { body, pins } };
        if (Doc.IsDisabled(n.Id)) view.Opacity = 0.45;
        Canvas.SetLeft(view, n.X);
        Canvas.SetTop(view, n.Y);
        return view;
    }

    void RefreshSelection()
    {
        foreach (var (id, view) in _nodeViews)
        {
            var body = (Border)view.Children[0];
            var sel = Selection.Contains(id);
            body.Token(Border.BorderBrushProperty, sel ? "lit/white" : "border/default");
            body.BorderThickness = new Thickness(sel ? 2 : 1);
        }
        _mini.InvalidateVisual();
    }

    public Control? ViewOf(string id) => _nodeViews.GetValueOrDefault(id);

    public void Select(IEnumerable<string> ids, bool add = false)
    {
        if (!add) Selection.Clear();
        foreach (var id in ids) Selection.Add(id);
        RefreshSelection();
        SelectionChanged?.Invoke();
    }

    void MoveViews()
    {
        foreach (var n in Doc.AllNodes)
            if (_nodeViews.TryGetValue(n.Id, out var v)) { Canvas.SetLeft(v, n.X); Canvas.SetTop(v, n.Y); }
        _wires.InvalidateVisual();
        _mini.InvalidateVisual();
    }

    // ---- hit testing -----------------------------------------------------------------------------------------

    public PinGeometry? PinAt(Point world, double radius = 9)
    {
        foreach (var n in Doc.AllNodes.Reverse())
            foreach (var p in Doc.Pins(n))
                if (Math.Abs(n.X + p.Offset.X - world.X) <= radius && Math.Abs(n.Y + p.Offset.Y - world.Y) <= radius) return p;
        return null;
    }

    public GraphNode? NodeAt(Point world) => Doc.AllNodes.Reverse().FirstOrDefault(n => Doc.Bounds(n).Contains(world));

    /// <summary>The pin a Link tool wire leaves a node from: its exec out, else its first output.</summary>
    public static PinDef? LinkOutput(GraphNode n) =>
        NodeCatalog.Find(n.Type) is { } def ? def.Pins.FirstOrDefault(p => p.Output && p.Kind == PinKind.Exec) ?? def.Pins.FirstOrDefault(p => p.Output) : null;

    /// <summary>The input a Link tool wire of this kind lands on: the first one not wired yet, else the first one.</summary>
    public PinDef? LinkInput(GraphNode n, PinKind kind)
    {
        if (NodeCatalog.Find(n.Type) is not { } def) return null;
        var inputs = def.Pins.Where(p => !p.Output && p.Kind == kind).ToList();
        return inputs.FirstOrDefault(p => kind == PinKind.Exec || !Doc.AllLinks.Any(l => l.ToNode == n.Id && l.ToPin == p.Name)) ?? inputs.FirstOrDefault();
    }

    GraphComment? CommentTitleAt(Point world) => Doc.Comments.LastOrDefault(c => new Rect(c.X, c.Y, c.W, 28).Contains(world));

    // ---- pointer ---------------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Source is Visual v && v != this) return;
        Focus();
        var screen = e.GetPosition(this);
        var world = ToWorld(screen);
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed)
        {
            if (NodeAt(world) is { } node)
            {
                if (!Selection.Contains(node.Id)) Select([node.Id]);
                _page.OpenNodeMenu(node.Id);
            }
            else _page.OpenAddNode(world, null);
            e.Handled = true;
            return;
        }
        if (props.IsMiddleButtonPressed || Tool == GraphTool.Hand && props.IsLeftButtonPressed)
        {
            StartDrag(Drag.Pan, screen, e);
            return;
        }
        if (!props.IsLeftButtonPressed) return;
        if (e.ClickCount == 2 && NodeAt(world) is null && CommentTitleAt(world) is { } titled) { BeginRenameComment(titled); e.Handled = true; return; }
        if (e.ClickCount == 2 && NodeAt(world) is null) { _page.OpenAddNode(world, null); e.Handled = true; return; }
        if (PinAt(world) is { } pin)
        {
            _wireFrom = (pin.Node, pin.Pin, pin.Output, pin.Kind);
            _wireEnd = world;
            StartDrag(Drag.Wire, screen, e);
            return;
        }
        // Link tool: drag from anywhere on a node to another node; the wire leaves its exec out (else its first
        // output) and lands on the first free input of the same kind
        if (Tool == GraphTool.Link && NodeAt(world) is { } source && LinkOutput(source) is { } output)
        {
            _wireFrom = (source.Id, output.Name, true, output.Kind);
            _wireEnd = world;
            StartDrag(Drag.Wire, screen, e);
            return;
        }
        if (NodeAt(world) is { } hit)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (!Selection.Remove(hit.Id)) Selection.Add(hit.Id);
                RefreshSelection();
                SelectionChanged?.Invoke();
            }
            else if (!Selection.Contains(hit.Id)) Select([hit.Id]);
            _moveStart = Doc.AllNodes.Where(n => Selection.Contains(n.Id)).ToDictionary(n => n.Id, n => new Point(n.X, n.Y));
            _beforeJson = Doc.ToJson();
            StartDrag(Drag.Nodes, screen, e);
            return;
        }
        if (CommentTitleAt(world) is { } comment)
        {
            _dragComment = comment;
            _moveStart = Doc.AllNodes.Where(n => new Rect(comment.X, comment.Y, comment.W, comment.H).Contains(Doc.Bounds(n).Center)).ToDictionary(n => n.Id, n => new Point(n.X, n.Y));
            _moveStart["__comment"] = new Point(comment.X, comment.Y);
            _beforeJson = Doc.ToJson();
            StartDrag(Drag.Comment, screen, e);
            return;
        }
        if (Selection.Count > 0) Select([]);
        StartDrag(Drag.Pan, screen, e);
    }

    void StartDrag(Drag d, Point screen, PointerPressedEventArgs e)
    {
        _drag = d;
        _dragStart = screen;
        _panStart = _pan;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == Drag.None) return;
        var screen = e.GetPosition(this);
        var delta = (screen - _dragStart) / _zoom;
        switch (_drag)
        {
            case Drag.Pan:
                _pan = _panStart + (screen - _dragStart);
                ApplyTransform();
                break;
            case Drag.Nodes:
            case Drag.Comment:
                foreach (var (id, start) in _moveStart)
                {
                    if (id == "__comment") { _dragComment!.X = Math.Round(start.X + delta.X); _dragComment.Y = Math.Round(start.Y + delta.Y); continue; }
                    if (Doc.Node(id) is { } n) { n.X = Math.Round(start.X + delta.X); n.Y = Math.Round(start.Y + delta.Y); }
                }
                if (_drag == Drag.Comment) Rebuild();
                else MoveViews();
                break;
            case Drag.Wire:
                _wireEnd = ToWorld(screen);
                _wires.InvalidateVisual();
                break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var drag = _drag;
        _drag = Drag.None;
        e.Pointer.Capture(null);
        switch (drag)
        {
            case Drag.Nodes or Drag.Comment when _beforeJson != null:
                var moved = _moveStart.Any(kv => kv.Key == "__comment" ? _dragComment is { } c && (c.X != kv.Value.X || c.Y != kv.Value.Y) : Doc.Node(kv.Key) is { } n && (n.X != kv.Value.X || n.Y != kv.Value.Y));
                if (moved) _page.Record(drag == Drag.Comment ? "Move comment" : "Move nodes", _beforeJson);
                _beforeJson = null;
                _dragComment = null;
                break;
            case Drag.Wire when _wireFrom is { } from:
                _wireFrom = null;
                var target = PinAt(_wireEnd, 12);
                if (target is null && Tool == GraphTool.Link && from.Output && NodeAt(_wireEnd) is { } onto && onto.Id != from.Node && LinkInput(onto, from.Kind) is { } input)
                    target = new PinGeometry(onto.Id, input.Name, input.Kind, false, default);
                if (target != null && target.Output != from.Output)
                {
                    var (a, b) = from.Output ? ((from.Node, from.Pin), (target.Node, target.Pin)) : ((target.Node, target.Pin), (from.Node, from.Pin));
                    _page.Edit("Connect", () => Doc.Connect(a.Item1, a.Item2, b.Item1, b.Item2));
                }
                else if (target is null && from.Output) _page.OpenAddNode(_wireEnd, (from.Node, from.Pin));
                _wires.InvalidateVisual();
                break;
        }
    }

    /// <summary>Renames a comment frame: a text box over its title; Enter keeps the name, Esc cancels.</summary>
    public TextBox BeginRenameComment(GraphComment c)
    {
        var p = ToScreen(new Point(c.X + 30, c.Y + 4));
        var box = new TextBox { Text = c.Title, Width = 220, Height = 24, Name = "CommentTitleBox", Padding = new Thickness(6, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(p.X, p.Y, 0, 0) };
        AgbStudio.Editor.Theme.Fig.SetStyle(box, "Label/11 Medium");
        void Finish(bool keep)
        {
            if (!Children.Contains(box)) return;
            Children.Remove(box);
            var title = box.Text?.Trim() ?? "";
            if (keep && title.Length > 0 && title != c.Title) _page.Edit("Rename comment", () => { c.Title = title; return true; });
        }
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Finish(true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
        };
        box.LostFocus += (_, _) => Finish(true);
        Children.Add(box);
        box.Focus();
        box.SelectAll();
        return box;
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
        if (e.Handled) return;
        if (e.Key == Key.G && e.KeyModifiers == KeyModifiers.Control) { _page.GroupIntoComment(); e.Handled = true; }
        else if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None) { Delete(); e.Handled = true; }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _grid.InvalidateVisual();
        _mini.InvalidateVisual();
    }

    // ---- Edit menu (IEditTarget) ------------------------------------------------------------------------------

    public bool CanCopy => Selection.Count > 0;
    public bool CanPaste => _clipboard.Count > 0;

    public void Copy() => _clipboard = Doc.AllNodes.Where(n => Selection.Contains(n.Id)).Select(Clone).ToList();

    public void Cut()
    {
        Copy();
        Delete();
    }

    public void Paste() => PasteNodes(_clipboard, 30);

    public void Duplicate() => PasteNodes(Doc.AllNodes.Where(n => Selection.Contains(n.Id)).Select(Clone).ToList(), 30, "Duplicate");

    void PasteNodes(List<GraphNode> nodes, double offset, string name = "Paste")
    {
        if (nodes.Count == 0) return;
        var ids = new List<string>();
        _page.Edit(name, () =>
        {
            foreach (var n in nodes)
            {
                var copy = Clone(n);
                copy.Id = Doc.NextId();
                copy.X += offset;
                copy.Y += offset;
                Doc.Graph.Nodes.Add(copy);
                ids.Add(copy.Id);
            }
            return true;
        });
        Select(ids);
    }

    static GraphNode Clone(GraphNode n) => new() { Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, Props = new Dictionary<string, string>(n.Props, StringComparer.Ordinal) };

    public void Delete()
    {
        if (Selection.Count == 0) return;
        var ids = Selection.ToList();
        _page.Edit(ids.Count == 1 ? "Delete node" : "Delete nodes", () => { foreach (var id in ids) Doc.RemoveNode(id); return true; });
        Select([]);
    }

    public void SelectAll() => Select(Doc.AllNodes.Select(n => n.Id));

    // ---- layers ----------------------------------------------------------------------------------------------

    /// <summary>The dot grid in screen space (follows pan and zoom).</summary>
    sealed class DotGrid(GraphCanvas c) : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var brush = ThemeService.Token(this, "border/default");
            if (brush is null) return;
            var step = 20 * c._zoom;
            if (step < 6) return;
            var ox = ((c._pan.X % step) + step) % step;
            var oy = ((c._pan.Y % step) + step) % step;
            for (var y = oy; y < Bounds.Height; y += step)
                for (var x = ox; x < Bounds.Width; x += step)
                    ctx.FillRectangle(brush, new Rect(Math.Round(x), Math.Round(y), 1.5, 1.5));
        }
    }

    /// <summary>Comment frames (dashed, behind nodes).</summary>
    sealed class BackLayer(GraphCanvas c) : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var pen = new Pen(ThemeService.Token(this, "border/strong"), 1, new DashStyle([5, 4], 0));
            foreach (var cm in c.Doc.Comments)
                ctx.DrawRectangle(null, pen, new Rect(cm.X, cm.Y, cm.W, cm.H), 10, 10);
        }
    }

    /// <summary>Wires: cubic curves from output to input pins, coloured by pin type; the wire being dragged.</summary>
    sealed class WireLayer(GraphCanvas c) : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var doc = c.Doc;
            foreach (var l in doc.AllLinks)
            {
                var a = doc.PinPoint(l.FromNode, l.FromPin, true);
                var b = doc.PinPoint(l.ToNode, l.ToPin, false);
                if (a is null || b is null) continue;
                var kind = NodeCatalog.Find(doc.Node(l.FromNode)!.Type)?.Pin(l.FromPin)?.Kind ?? PinKind.Exec;
                var disabled = doc.IsDisabled(l.FromNode) || doc.IsDisabled(l.ToNode);
                Curve(ctx, a.Value, b.Value, Brush(kind), disabled ? 0.4 : 1, kind == PinKind.Exec);
            }
            if (c._drag == Drag.Wire && c._wireFrom is { } from && doc.PinPoint(from.Node, from.Pin, from.Output) is { } p)
            {
                var (s, e) = from.Output ? (p, c._wireEnd) : (c._wireEnd, p);
                Curve(ctx, s, e, Brush(from.Kind), 0.8, from.Kind == PinKind.Exec);
            }
        }

        IBrush? Brush(PinKind k) => ThemeService.Token(this, k switch
        {
            PinKind.Bool => "lit/pin-bool", PinKind.Fixed => "lit/pin-fixed", PinKind.Actor => "lit/pin-actor", PinKind.Text => "lit/pin-text",
            PinKind.Vector => "lit/pin-vector", _ => "lit/pin-exec",
        });

        static void Curve(DrawingContext ctx, Point a, Point b, IBrush? brush, double opacity, bool exec)
        {
            if (brush is null) return;
            // exec wires start after the arrow and end at its tip
            if (exec) { a = new Point(a.X + 6, a.Y); b = new Point(b.X + 6, b.Y); }
            var dx = Math.Max(40, Math.Abs(b.X - a.X) * 0.5);
            var g = new StreamGeometry();
            using (var s = g.Open())
            {
                s.BeginFigure(a, false);
                s.CubicBezierTo(new Point(a.X + dx, a.Y), new Point(b.X - dx, b.Y), b);
                s.EndFigure(false);
            }
            using (ctx.PushOpacity(opacity))
                ctx.DrawGeometry(null, new Pen(brush, 2, lineCap: PenLineCap.Round), g);
        }
    }

    /// <summary>The minimap: nodes as category colours, the visible area as a white frame; click to move the view.</summary>
    sealed class MiniMap : Control
    {
        readonly GraphCanvas _c;

        public MiniMap(GraphCanvas c)
        {
            _c = c;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        (Rect World, double Scale, Point Origin) Map()
        {
            var nodes = _c.Doc.AllNodes.ToList();
            var r = nodes.Count == 0 ? new Rect(0, 0, 400, 300) : nodes.Select(_c.Doc.Bounds).Aggregate((a, b) => a.Union(b));
            var view = new Rect(_c.ToWorld(default), _c.ToWorld(new Point(_c.Bounds.Width, _c.Bounds.Height)));
            r = r.Union(view);
            var inner = new Rect(10, 10, Bounds.Width - 20, Bounds.Height - 20);
            var scale = Math.Min(inner.Width / Math.Max(1, r.Width), inner.Height / Math.Max(1, r.Height));
            return (r, scale, new Point(inner.X + (inner.Width - r.Width * scale) / 2, inner.Y + (inner.Height - r.Height * scale) / 2));
        }

        public override void Render(DrawingContext ctx)
        {
            ctx.DrawRectangle(ThemeService.Token(this, "bg/panel"), new Pen(ThemeService.Token(this, "border/default"), 1), new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1), 8, 8);
            var (world, scale, o) = Map();
            Point M(double x, double y) => new(o.X + (x - world.X) * scale, o.Y + (y - world.Y) * scale);
            foreach (var n in _c.Doc.AllNodes)
            {
                var cat = NodeCatalog.Find(n.Type)?.Category ?? "Other";
                if (this.FindResource(GraphDocument.CategoryColor(cat)) is not Color col) continue;
                var p = M(n.X, n.Y);
                ctx.FillRectangle(new SolidColorBrush(col), new Rect(p.X, p.Y, 30, 13), 2);
            }
            var a = M(_c.ToWorld(default).X, _c.ToWorld(default).Y);
            var end = _c.ToWorld(new Point(_c.Bounds.Width, _c.Bounds.Height));
            var b = M(end.X, end.Y);
            ctx.DrawRectangle(null, new Pen(ThemeService.Token(this, "lit/white"), 1), new Rect(a, b));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var (world, scale, o) = Map();
            var p = e.GetPosition(this);
            var target = new Point(world.X + (p.X - o.X) / scale, world.Y + (p.Y - o.Y) / scale);
            _c._pan = new Vector(_c.Bounds.Width / 2 - target.X * _c._zoom, _c.Bounds.Height / 2 - target.Y * _c._zoom);
            _c.ApplyTransform();
            e.Handled = true;
        }
    }
}
