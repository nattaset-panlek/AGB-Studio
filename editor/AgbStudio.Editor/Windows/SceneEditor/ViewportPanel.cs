using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// Scene viewport (T-P01 #18-#20, P11.10 / P11.11): the GBA screen 240 x 160 drawn as the ROM draws it (D-18,
/// <see cref="GbaScreenRenderer"/>) at the zoom, with the grid, the selection box and the chips: scene size (Scene
/// Settings), cursor position, grid (Grid &amp; Snap) and zoom (Zoom menu). Click selects, drag moves (snapped), the
/// middle button or Space + drag pans, Ctrl + wheel zooms. It also follows the toolbar layer toggles, video mode
/// chip and grid button of the workspace.
/// </summary>
[EditorWindow("T-P01:Viewport", Kind = WindowKind.Panel)]
public sealed class ViewportPanel : Panel
{
    readonly EditorServices _s;
    readonly SceneEditorModel _m;

    public GbaScreenView Screen { get; }
    public Chip SceneChip { get; } = new() { Glyph = "map", Name = "SceneSizeChip" };
    public Chip CursorChip { get; } = new() { Name = "CursorChip" };
    public Chip GridChip { get; } = new() { Glyph = "grid", Name = "GridChip" };
    public Chip ZoomChip { get; } = new() { Name = "ZoomChip" };

    public ViewportPanel(WindowContext ctx) : this(ctx.Services)
    {
        // opened for a scene file (Command Palette, World Map › Open): show that scene
        if (ctx.Args is string path && path.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) && GameProject.AssetName(path) != _m.SceneName)
            _m.OpenScene(GameProject.AssetName(path));
    }

    public ViewportPanel(EditorServices s)
    {
        _s = s;
        _m = SceneEditorModel.Of(s);
        Name = "ViewportPanel";
        ClipToBounds = true;
        Screen = new GbaScreenView(_m) { Name = "GbaScreen" };
        Children.Add(Screen);
        var left = UI.Row(8, SceneChip, CursorChip);
        var right = UI.Row(8, GridChip, ZoomChip);
        foreach (var row in new[] { left, right })
        {
            row.VerticalAlignment = VerticalAlignment.Bottom;
            row.Margin = new Thickness(12, 0, 12, 12);
            Children.Add(row);
        }
        left.HorizontalAlignment = HorizontalAlignment.Left;
        right.HorizontalAlignment = HorizontalAlignment.Right;
        SceneChip.Click += (_, _) => _ = _s.Windows.Open("T-D03", anchor: SceneChip, source: "SceneSizeChip");
        GridChip.Click += (_, _) => _ = _s.Windows.Open("T-O03", anchor: GridChip, source: "GridChip");
        ZoomChip.Click += (_, _) => _ = _s.Windows.Open("T-M15", anchor: ZoomChip, source: "ZoomChip");
        _m.Changed += UpdateChips;
        _m.ViewChanged += UpdateChips;
        _m.SelectionChanged += UpdateChips;
        UpdateChips();
        AttachedToVisualTree += (_, _) => HookWorkspace();
    }

    void UpdateChips()
    {
        var sc = _m.Scene;
        SceneChip.IsVisible = sc != null;
        CursorChip.IsVisible = sc != null;
        if (sc == null) return;
        int w = _m.Map?.Width ?? sc.Width, h = _m.Map?.Height ?? sc.Height;
        SceneChip.Content = $"{_m.SceneName} · {w}×{h} tiles";
        var p = _m.Cursor ?? (_m.SelectedActor is { } a ? (a.X, a.Y) : null);
        CursorChip.Content = p is { } c ? $"x {c.X}  y {c.Y}" : "x –  y –";
        GridChip.Content = $"Grid {_m.Grid.Size}";
        ZoomChip.Content = _m.FitToScreen ? "Fit" : $"{_m.Zoom}%";
        UpdateVideoChip();
    }

    // ---- the workspace toolbar (layer toggles, video mode chip, grid button) and shared hooks ----------

    Chip? _videoChip;
    bool _hooked;

    void HookWorkspace()
    {
        if (this.FindAncestorOfType<SceneWorkspace>() is not { } ws) return;
        if (TopLevel.GetTopLevel(this) is MainWindow win) win.Edit.DefaultTarget = SceneEditTarget.Of(_m);
        if (_hooked) return;
        _hooked = true;
        var console = ConsoleLog.Of(_s);
        void Count() => ws.ConsoleCount = console.Entries.Count == 0 ? null : console.Entries.Count;
        console.Changed += Count;
        Count();
        foreach (var t in ws.GetVisualDescendants().OfType<LayerToggle>())
        {
            var layer = t.Name switch
            {
                "Layer_BG0" => ScreenLayers.Bg0, "Layer_BG1" => ScreenLayers.Bg1, "Layer_BG2" => ScreenLayers.Bg2,
                "Layer_BG3" => ScreenLayers.Bg3, "Layer_OBJ" => ScreenLayers.Obj, _ => ScreenLayers.None,
            };
            if (layer == ScreenLayers.None) continue;
            var toggle = t;
            _m.SetLayer(layer, t.IsOn);
            t.PropertyChanged += (_, e) => { if (e.Property == LayerToggle.IsOnProperty) _m.SetLayer(layer, toggle.IsOn); };
        }
        if (ws.GetVisualDescendants().OfType<IconButton>().FirstOrDefault(b => b.Name == "Tool_grid") is { } grid)
            grid.PropertyChanged += (_, e) =>
            {
                if (e.Property != IconButton.IsActiveProperty) return;
                _m.Grid.ShowGrid = !_m.Grid.ShowGrid;
                _m.GridChanged();
            };
        // Select / Move / Paint tiles / Fill / Erase of the toolbar
        _m.Tool = (SceneTool)Math.Max(0, ws.ActiveTool);
        ws.ToolChanged += i => _m.Tool = (SceneTool)i;
        _videoChip = ws.GetVisualDescendants().OfType<Chip>().FirstOrDefault(c => c.Name == "VideoModeChip");
        UpdateVideoChip();
    }

    void UpdateVideoChip()
    {
        if (_videoChip != null && _m.Scene != null) _videoChip.Content = VideoModeText(_m.Scene.VideoMode);
    }

    public static string VideoModeText(int mode) => $"GBA · Mode {mode} · 240×160";
}

/// <summary>The GBA screen of the viewport: game pixels scaled without smoothing, grid, selection and picking.</summary>
public sealed class GbaScreenView : Control
{
    readonly SceneEditorModel _m;
    ScreenFrame? _frame;
    WriteableBitmap? _bitmap;
    bool _dirty = true;
    // drag state
    int _dragActor = -1;
    Point _dragStart;
    (int X, int Y) _actorStart;
    (int X, int Y) _grab;   // pivot minus the top-left of the actor's box: the box snaps to the grid, not the pivot
    bool _panning;
    (int X, int Y) _panStart;
    bool _spaceDown;
    bool _painting;

    public GbaScreenView(SceneEditorModel m)
    {
        _m = m;
        Focusable = true;
        ClipToBounds = true;
        m.Changed += Invalidate;
        m.ViewChanged += Invalidate;
        m.SelectionChanged += InvalidateVisual;
    }

    void Invalidate()
    {
        _dirty = true;
        InvalidateVisual();
    }

    /// <summary>The frame now shown (renders it if the scene or view changed).</summary>
    public ScreenFrame? Frame
    {
        get
        {
            if (_dirty)
            {
                _dirty = false;
                _frame = _m.Render();
                _bitmap = _frame == null ? null : PixelArt.Bitmap(_frame.ToArgb(), ScreenFrame.Width, ScreenFrame.Height);
            }
            return _frame;
        }
    }

    public double Scale => _m.EffectiveZoom(Bounds.Width, Bounds.Height);

    /// <summary>Top-left of the GBA screen in this control.</summary>
    public Point Origin
    {
        get
        {
            var k = Scale;
            return new Point(Math.Round((Bounds.Width - 240 * k) / 2), Math.Round((Bounds.Height - 160 * k) / 2));
        }
    }

    /// <summary>Scene pixel under a point of this control.</summary>
    public (int X, int Y) ToScene(Point p)
    {
        var k = Scale;
        var o = Origin;
        var cam = _m.Camera;
        return ((int)Math.Floor((p.X - o.X) / k) + cam.X, (int)Math.Floor((p.Y - o.Y) / k) + cam.Y);
    }

    public Point ToView(int sceneX, int sceneY)
    {
        var k = Scale;
        var o = Origin;
        var cam = _m.Camera;
        return new Point(o.X + (sceneX - cam.X) * k, o.Y + (sceneY - cam.Y) * k);
    }

    /// <summary>The actor whose sprite (or zone) is drawn at a point, front-most first.</summary>
    public int ActorAt(Point p)
    {
        if (Frame is not { } f || _m.Scene is not { } scene) return -1;
        var k = Scale;
        var o = Origin;
        int sx = (int)Math.Floor((p.X - o.X) / k), sy = (int)Math.Floor((p.Y - o.Y) / k);
        foreach (var sp in f.Sprites)
            if (sx >= sp.Left && sx < sp.Left + sp.SourceWidth && sy >= sp.Top && sy < sp.Top + sp.SourceHeight) return sp.Actor;
        var (x, y) = ToScene(p);
        for (var i = scene.Actors.Count - 1; i >= 0; i--)
        {
            var a = scene.Actors[i];
            if (a.Sprite.Length > 0) continue;
            var (rx, ry, rw, rh) = ZoneRect(a);
            if (x >= rx && x < rx + rw && y >= ry && y < ry + rh) return i;
        }
        return -1;
    }

    // Trigger / camera zones and labels: their box from the actor position (the build's collider for zones).
    static (int X, int Y, int W, int H) ZoneRect(ActorData a) => a.Kind switch
    {
        ActorKind.TriggerZone or ActorKind.CameraZone => (a.X, a.Y - a.ZoneH, a.ZoneW, a.ZoneH),
        ActorKind.TextLabel => (a.X / 8 * 8, a.Y / 8 * 8, 64, 16),
        _ => (a.X - 4, a.Y - 4, 8, 8),
    };

    /// <summary>Selection rectangle of an actor in scene pixels.</summary>
    public (int X, int Y, int W, int H)? ActorRect(int index)
    {
        if (_m.Scene is not { } scene || index < 0 || index >= scene.Actors.Count) return null;
        var a = scene.Actors[index];
        if (a.Sprite.Length > 0 && _m.Assets?.Sprite(a.Sprite) is { } sheet)
        {
            var meta = _m.Assets.SpriteMeta(a.Sprite);
            return (a.X - (sbyte)meta.OriginX, a.Y - (sbyte)meta.OriginY, sheet.SourceWidth, sheet.SourceHeight);
        }
        return ZoneRect(a);
    }

    public override void Render(DrawingContext ctx)
    {
        var f = Frame;
        if (f == null || _bitmap == null) return;
        var k = Scale;
        var o = Origin;
        var screen = new Rect(o.X, o.Y, 240 * k, 160 * k);
        using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            ctx.DrawImage(_bitmap, new Rect(0, 0, 240, 160), screen);
        using (ctx.PushClip(screen))
        {
            if (_m.Grid.TileCollision && _m.CollisionOverlayAllowed) DrawCollision(ctx, k);
            if (PixelGridShown) DrawPixelGrid(ctx, screen, k);
            if (_m.Grid.ShowGrid && _m.Grid.Size > 0) DrawGrid(ctx, screen, k);
            if (_m.Grid.CameraBounds) DrawCameraBounds(ctx, k);
            if (_m.IsTileTool) DrawBrush(ctx, k);
            else DrawSelection(ctx);
        }
    }

    /// <summary>Paint tiles / Fill / Erase: the brush outline on the cell under the pointer.</summary>
    void DrawBrush(DrawingContext ctx, double k)
    {
        var layer = _m.PaintLayer;
        if (_m.Cursor is not { } c || layer < 0) return;
        var (cx, cy) = _m.CellAt(layer, c.X, c.Y);
        var l = _m.Map!.Layers[layer];
        var cam = _m.Camera;
        // the cell's top-left back in scene pixels (the layer scrolls with its parallax)
        int sx = cx * 16 - ((cam.X * AgbStudio.Build.Pipeline.Cpp.Parallax(l.ParallaxX)) >> 8) + cam.X;
        int sy = cy * 16 - ((cam.Y * AgbStudio.Build.Pipeline.Cpp.Parallax(l.ParallaxY)) >> 8) + cam.Y;
        var (_, w, h) = _m.Tool == SceneTool.Fill ? ([0], 1, 1) : _m.Brush;
        var tl = ToView(sx, sy);
        var pen = new Pen(_m.Tool == SceneTool.Erase ? Token("status/danger") : Token("accent/default"), 2);
        ctx.DrawRectangle(null, pen, new Rect(Math.Round(tl.X) + 1, Math.Round(tl.Y) + 1, w * 16 * k - 2, h * 16 * k - 2));
    }

    /// <summary>The tile tool acts at a point of this control (scene pixel under it).</summary>
    bool PaintAt(Point p)
    {
        var (x, y) = ToScene(p);
        return _m.Tool switch
        {
            SceneTool.Paint => _m.PaintAt(x, y),
            SceneTool.Erase => _m.EraseAt(x, y),
            SceneTool.Fill => _m.FillAt(x, y),
            _ => false,
        };
    }

    /// <summary>The collision type of each tile of the collision layer (Grid &amp; Snap › Tile collision).</summary>
    public CollisionType CollisionAt(int sceneX, int sceneY)
    {
        if (_m.Map is not { } map || _m.Assets is not { } assets || sceneX < 0 || sceneY < 0) return CollisionType.None;
        var index = map.CollisionLayer >= 0 ? map.CollisionLayer : map.Layers.FindIndex(l => l.Type == LayerType.Tile);
        if (index < 0 || index >= map.Layers.Count) return CollisionType.None;
        int tx = sceneX / 16, ty = sceneY / 16;
        var layer = map.Layers[index];
        if (tx >= map.Width || ty >= map.Height || layer.Cells.Count != map.Width * map.Height) return CollisionType.None;
        var path = assets.TilesetPath(map.Tileset);
        if (path == null) return CollisionType.None;
        var meta = _m.Project!.LoadMeta<TilesetMeta>(path);
        return meta.Tiles.GetValueOrDefault(layer.Cells[ty * map.Width + tx])?.Collision ?? CollisionType.None;
    }

    void DrawCollision(DrawingContext ctx, double k)
    {
        var cam = _m.Camera;
        var brushes = new Dictionary<CollisionType, IBrush?>
        {
            [CollisionType.Solid] = Tint("status/danger"), [CollisionType.OneWay] = Tint("status/warning"), [CollisionType.Ladder] = Tint("status/success"),
            [CollisionType.Hazard] = Tint("status/danger"), [CollisionType.Water] = Tint("accent/default"),
        };
        for (var y = cam.Y / 16 * 16; y < cam.Y + 160; y += 16)
            for (var x = cam.X / 16 * 16; x < cam.X + 240; x += 16)
            {
                var c = CollisionAt(x, y);
                if (c == CollisionType.None || brushes[c] is not { } b) continue;
                var tl = ToView(x, y);
                ctx.DrawRectangle(b, null, new Rect(tl.X, tl.Y, 16 * k, 16 * k));
            }
    }

    IBrush? Tint(string token) => Token(token) is ISolidColorBrush b ? new SolidColorBrush(b.Color, 0.35) : null;

    void DrawCameraBounds(DrawingContext ctx, double k)
    {
        if (_m.Scene is not { } sc) return;
        int w = (_m.Map?.Width ?? sc.Width) * 16, h = (_m.Map?.Height ?? sc.Height) * 16;
        // the map edges where the camera stops; an edge on the screen border (the clamped camera) is not drawn
        var cam = _m.Camera;
        var a = ToView(0, 0);
        var b = ToView(w, h);
        var pen = new Pen(Token("accent/default"), 2) { DashStyle = new DashStyle([3, 2], 0) };
        if (cam.X < 0) ctx.DrawLine(pen, new Point(a.X, a.Y), new Point(a.X, b.Y));
        if (cam.Y < 0) ctx.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, a.Y));
        if (cam.X + 240 > w) ctx.DrawLine(pen, new Point(b.X, a.Y), new Point(b.X, b.Y));
        if (cam.Y + 160 > h) ctx.DrawLine(pen, new Point(a.X, b.Y), new Point(b.X, b.Y));
    }

    /// <summary>True when the camera bounds overlay shows an edge (the view is panned past the map).</summary>
    public bool CameraBoundsVisible
    {
        get
        {
            if (_m.Scene is not { } sc || !_m.Grid.CameraBounds) return false;
            int w = (_m.Map?.Width ?? sc.Width) * 16, h = (_m.Map?.Height ?? sc.Height) * 16;
            var cam = _m.Camera;
            return cam.X < 0 || cam.Y < 0 || cam.X + 240 > w || cam.Y + 160 > h;
        }
    }

    // the theme's brush for this control's theme variant (FindResource without one missed the themed tokens: no accent outline)
    IBrush? Token(string key) => ThemeService.Token(this, key);

    /// <summary>Preferences › Appearance: the grid between game pixels at 400% and above.</summary>
    public bool PixelGridShown => _m.Services.Preferences.Current.PixelGridAt(Scale);

    void DrawPixelGrid(DrawingContext ctx, Rect screen, double k)
    {
        if (Token("lit/white") is not ISolidColorBrush b) return;
        var pen = new Pen(new SolidColorBrush(b.Color, 0.06), 1);
        for (var x = 1; x < 240; x++) { var vx = Math.Round(screen.X + x * k) + 0.5; ctx.DrawLine(pen, new Point(vx, screen.Top), new Point(vx, screen.Bottom)); }
        for (var y = 1; y < 160; y++) { var vy = Math.Round(screen.Y + y * k) + 0.5; ctx.DrawLine(pen, new Point(screen.Left, vy), new Point(screen.Right, vy)); }
    }

    void DrawGrid(DrawingContext ctx, Rect screen, double k)
    {
        var colour = _m.Grid.Colour switch { 1 => Token("accent/default"), 2 => Token("status/danger"), _ => Token("lit/white") };
        if (colour is not ISolidColorBrush b) return;
        var pen = new Pen(new SolidColorBrush(b.Color, 0.22), 1);
        var cam = _m.Camera;
        var g = _m.Grid.Size;
        var firstX = (int)Math.Ceiling(cam.X / (double)g) * g;
        for (var x = firstX; x < cam.X + 240; x += g)
        {
            var vx = Math.Round(screen.X + (x - cam.X) * k) + 0.5;
            ctx.DrawLine(pen, new Point(vx, screen.Top), new Point(vx, screen.Bottom));
        }
        var firstY = (int)Math.Ceiling(cam.Y / (double)g) * g;
        for (var y = firstY; y < cam.Y + 160; y += g)
        {
            var vy = Math.Round(screen.Y + (y - cam.Y) * k) + 0.5;
            ctx.DrawLine(pen, new Point(screen.Left, vy), new Point(screen.Right, vy));
        }
    }

    /// <summary>The outline the viewport draws around a selected actor, in this control.</summary>
    public Rect? SelectionBox(int index)
    {
        if (ActorRect(index) is not { } r) return null;
        var k = Scale;
        var tl = ToView(r.X, r.Y);
        return new Rect(Math.Round(tl.X - 4) + 0.5, Math.Round(tl.Y - 4) + 0.5, Math.Round(r.W * k + 8), Math.Round(r.H * k + 8));
    }

    /// <summary>Every selected actor gets its outline; the first one selected also gets the corner handles and its name.</summary>
    void DrawSelection(DrawingContext ctx)
    {
        var i = _m.SelectedActorIndex;
        if (i < 0 || SelectionBox(i) is not { } box) return;
        var accent = Token("accent/default");
        var white = Token("lit/white");
        foreach (var other in _m.SelectedActorIndices)
            if (other != i && SelectionBox(other) is { } b) ctx.DrawRectangle(null, new Pen(accent, 1), b);
        ctx.DrawRectangle(null, new Pen(accent, 1), box);
        foreach (var c in new[] { box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight })
            ctx.DrawRectangle(white, new Pen(accent, 1), new Rect(c.X - 3, c.Y - 3, 6, 6));
        // name tag above the box
        var name = _m.Scene!.Actors[i].Name;
        var style = AgbStudio.Editor.Theme.TextStyles.Get("Label/11");
        var text = new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(style.FontFamily, FontStyle.Normal, style.FontWeight), style.Size, Token("text/on-accent"));
        var tag = new Rect(box.X - 0.5, box.Y - 0.5 - 25, Math.Round(text.Width) + 14, 18);
        ctx.DrawRectangle(accent, null, tag, 4, 4);
        ctx.DrawText(text, new Point(tag.X + 7, tag.Y + (18 - text.Height) / 2));
    }

    // ---- input ----------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pt = e.GetCurrentPoint(this);
        var p = pt.Position;
        SceneEditTarget.Of(_m).Active = SceneEditTarget.Panel.Scene;
        if (pt.Properties.IsMiddleButtonPressed || (pt.Properties.IsLeftButtonPressed && _spaceDown))
        {
            _panning = true;
            _dragStart = p;
            _panStart = _m.Pan;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (pt.Properties.IsLeftButtonPressed && _m.IsTileTool)
        {
            // Paint tiles and Erase paint while the button is held (one undo step per stroke); Fill fills once
            _m.BeginStroke();
            PaintAt(p);
            _painting = _m.Tool != SceneTool.Fill;
            if (_painting) e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        var actor = ActorAt(p);
        if (pt.Properties.IsRightButtonPressed)
        {
            if (actor >= 0)
            {
                _m.SelectActor(actor);
                _ = _m.Services.Windows.Open("T-M12", anchor: this, source: "ViewportActor");
            }
            e.Handled = true;
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed) return;
        if (actor < 0)
        {
            _m.Selection = _m.Scene == null ? null : new SceneItem(SceneItemKind.Scene);
            return;
        }
        _m.SelectActor(actor);
        var a = _m.Scene!.Actors[actor];
        _dragActor = actor;
        _dragStart = p;
        var r = ActorRect(actor) ?? (a.X, a.Y, 0, 0);
        _actorStart = (r.X, r.Y);
        _grab = (a.X - r.X, a.Y - r.Y);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        var k = Scale;
        var o = Origin;
        var inside = p.X >= o.X && p.Y >= o.Y && p.X < o.X + 240 * k && p.Y < o.Y + 160 * k;
        _m.SetCursor(inside ? ToScene(p) : null);
        if (_panning)
        {
            var dx = (int)Math.Round((_dragStart.X - p.X) / k);
            var dy = (int)Math.Round((_dragStart.Y - p.Y) / k);
            _m.SetPan(_panStart.X + dx, _panStart.Y + dy);
            return;
        }
        if (_painting)
        {
            if (inside) PaintAt(p);
            return;
        }
        if (_dragActor < 0) return;
        var mx = (int)Math.Round((p.X - _dragStart.X) / k);
        var my = (int)Math.Round((p.Y - _dragStart.Y) / k);
        if (mx == 0 && my == 0) return;
        var nx = _m.SnapValue(_actorStart.X + mx) + _grab.X;
        var ny = _m.SnapValue(_actorStart.Y + my) + _grab.Y;
        _m.MoveActor(_dragActor, nx, ny, merge: true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragActor = -1;
        _panning = false;
        _painting = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _m.SetCursor(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            var z = (int)Math.Round(Scale * 100);
            _m.SetZoom(Math.Clamp(e.Delta.Y > 0 ? (z / 100 + 1) * 100 : ((z + 99) / 100 - 1) * 100, 100, 800));
        }
        else
        {
            var step = 16;
            var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            _m.SetPan(_m.Pan.X - (int)(shift ? e.Delta.Y : e.Delta.X) * step, _m.Pan.Y - (int)(shift ? 0 : e.Delta.Y) * step);
        }
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space) { _spaceDown = true; e.Handled = true; }
        var i = _m.SelectedActorIndex;
        if (i < 0 || e.Handled || e.KeyModifiers != KeyModifiers.None) return;
        var step = _m.Grid.Snap ? _m.Grid.Size : 1;
        (int dx, int dy) = e.Key switch { Key.Left => (-step, 0), Key.Right => (step, 0), Key.Up => (0, -step), Key.Down => (0, step), _ => (0, 0) };
        if (dx == 0 && dy == 0) return;
        // the arrow keys move every selected actor (one undo step)
        _m.MoveActorsBy(_m.SelectedActorIndices, dx, dy);
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space) _spaceDown = false;
    }

    /// <summary>Drops a sprite asset from the Assets panel: a new actor with that sprite at the point (snapped).</summary>
    public int DropSprite(string sprite, Point p)
    {
        if (_m.Scene == null) return -1;
        var (x, y) = ToScene(p);
        var meta = _m.Assets?.SpriteMeta(sprite);
        var kind = _m.Project is { } proj ? MostCommonKind(proj, sprite) : ActorKind.Empty;
        var a = _m.NewActor(kind, sprite);
        a.Sprite = sprite;
        a.Name = char.ToUpperInvariant(sprite[0]) + sprite[1..].Replace('_', ' ');
        if (meta?.Animations.FirstOrDefault() is { } anim) a.Animation = anim.Name;
        // the dropped sprite is centred on the pointer; its box snaps to the grid
        a.X = _m.SnapValue(x - (meta?.FrameWidth ?? 0) / 2) + (meta == null ? 0 : (sbyte)meta.OriginX);
        a.Y = _m.SnapValue(y - (meta?.FrameHeight ?? 0) / 2) + (meta == null ? 0 : (sbyte)meta.OriginY);
        return _m.AddActor(a, "Add Actor");
    }

    // An asset dropped in the scene becomes the kind of actor it is used for elsewhere in the project.
    static ActorKind MostCommonKind(GameProject p, string sprite) =>
        p.SceneFiles().SelectMany(f => GameProject.Load<SceneFile>(f).Actors).Where(a => a.Sprite == sprite)
            .GroupBy(a => a.Kind).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault(ActorKind.Empty);
}
