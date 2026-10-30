using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>Tilemap Editor tools (toolbar, left to right): Select (stamp from the map), Brush, Rectangle, Fill, Eraser, Pick.</summary>
public enum TileTool { Select, Brush, Rect, Fill, Erase, Pick }

/// <summary>
/// The state of one Tilemap Editor (T-P03) around a <see cref="MapDocument"/>: the layer being edited, the tool,
/// the brush stamp (from the tileset or the map), Random and Flip H, auto-tile, collision view, layer visibility and
/// lock, the camera and zoom. Painting goes through here so the canvas, the tests and the shortcuts share one code path.
/// </summary>
public sealed class TilemapSession
{
    readonly HashSet<MapLayerData> _hidden = [];
    readonly HashSet<MapLayerData> _locked = [];
    readonly HashSet<CollisionType> _hiddenCollision = [];
    TileTool _tool = TileTool.Brush;
    int _layer;
    Random _rng = new(1);

    public MapDocument Doc { get; }
    public MapRenderer Renderer { get; }
    public EditorServices? Services { get; }

    public TilemapSession(MapDocument doc, MapRenderer renderer, EditorServices? services = null)
    {
        Doc = doc;
        Renderer = renderer;
        Services = services;
        _layer = DefaultLayer();
        var first = Enumerable.Range(1, Math.Max(0, doc.TileCount - 1)).FirstOrDefault(t => doc.Props(t) != null);
        SelectTile(doc.TileCount > 1 ? Math.Max(1, first) : 0);
        RuleSet = doc.Meta.AutoTiles.FirstOrDefault()?.Name;
        (CamX, CamY) = MapRenderer.StartCamera(doc);
        doc.Changed += c =>
        {
            if (c.HasFlag(MapChange.Layers)) _layer = Math.Clamp(_layer, 0, Math.Max(0, doc.Map.Layers.Count - 1));
            if (c.HasFlag(MapChange.Tileset) && SelectedTile >= Math.Max(1, doc.TileCount)) SelectTile(0);
            if (RuleSet != null && doc.Meta.AutoTiles.All(s => s.Name != RuleSet)) RuleSet = doc.Meta.AutoTiles.FirstOrDefault()?.Name;
            ClampCamera();
        };
    }

    int DefaultLayer()
    {
        var c = Doc.CollisionLayerIndex;
        if (c >= 0 && Doc.Map.Layers[c].Type == LayerType.Tile) return c;
        var t = Doc.Map.Layers.FindIndex(l => l.Type == LayerType.Tile);
        return Math.Max(0, t);
    }

    /// <summary>Selection, tool, visibility or camera changed (not the map data).</summary>
    public event Action? ViewChanged;
    void View() => ViewChanged?.Invoke();

    // ---- layer -------------------------------------------------------------------------------------

    public int Layer
    {
        get => _layer;
        set { if (value < 0 || value >= Doc.Map.Layers.Count || value == _layer) return; _layer = value; View(); }
    }

    public MapLayerData? CurrentLayer => _layer < Doc.Map.Layers.Count ? Doc.Map.Layers[_layer] : null;
    public bool CanPaint => CurrentLayer is { } l && l.Type != LayerType.Image && !_locked.Contains(l) && l.Cells.Count == Doc.Width * Doc.Height;

    public bool IsVisible(int layer) => layer >= 0 && layer < Doc.Map.Layers.Count && !_hidden.Contains(Doc.Map.Layers[layer]);
    public bool IsLocked(int layer) => layer >= 0 && layer < Doc.Map.Layers.Count && _locked.Contains(Doc.Map.Layers[layer]);

    public void SetVisible(int layer, bool visible)
    {
        var l = Doc.Map.Layers[layer];
        if (visible ? _hidden.Remove(l) : _hidden.Add(l)) View();
    }

    public void SetLocked(int layer, bool locked)
    {
        var l = Doc.Map.Layers[layer];
        if (locked ? _locked.Add(l) : _locked.Remove(l)) View();
    }

    /// <summary>Index of the layer on a background (BG0..BG3), or -1.</summary>
    public int LayerOnBg(int bg) => Doc.Map.Layers.FindIndex(l => l.Bg == bg && l.Type != LayerType.CollisionOnly);

    bool _objVisible = true;
    public bool ObjVisible { get => _objVisible; set { if (_objVisible == value) return; _objVisible = value; View(); } }

    // ---- tools and brush ---------------------------------------------------------------------------

    public TileTool Tool { get => _tool; set { if (_tool == value) return; _tool = value; View(); } }

    /// <summary>The selected tileset tile (Tile properties).</summary>
    public int SelectedTile { get; private set; }
    public int StampWidth { get; private set; } = 1;
    public int StampHeight { get; private set; } = 1;
    public int[] Stamp { get; private set; } = [0];
    /// <summary>Random: 0 = off, otherwise the chance in % that a painted cell takes a random tile of the stamp.</summary>
    public double Random { get; private set; }
    public bool Flip { get; private set; }
    public bool AutoTileOn { get; private set; } = true;
    public string? RuleSet { get; private set; }
    public int SelectedRule { get; private set; }

    public AutoTileSet? CurrentSet => Doc.Meta.AutoTiles.FirstOrDefault(s => s.Name == RuleSet);

    /// <summary>Columns of the tileset panel; stamps of several tiles are blocks of this layout.</summary>
    public const int SwatchColumns = 4;

    public void SelectTile(int tile, int width = 1, int height = 1)
    {
        const int cols = SwatchColumns;
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var stamp = new int[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var t = tile + y * cols + x;
                stamp[y * width + x] = t < Doc.TileCount ? t : tile;
            }
        SelectedTile = tile;
        SetStamp(stamp, width, height);
    }

    public void SetStamp(int[] cells, int width, int height)
    {
        Stamp = cells;
        StampWidth = width;
        StampHeight = height;
        View();
    }

    /// <summary>Brush · Stamp text "2 × 1 tiles": resizes the stamp from the selected tile of the tileset.</summary>
    public bool SetStampSize(string text)
    {
        var parts = text.Replace("tiles", "", StringComparison.OrdinalIgnoreCase).Split(['×', 'x', 'X', '*'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h) || w < 1 || h < 1 || w > 16 || h > 16) return false;
        SelectTile(SelectedTile, w, h);
        return true;
    }

    public void SetRandom(double percent)
    {
        percent = Math.Clamp(Math.Round(percent), 0, 100);
        if (Math.Abs(Random - percent) < 0.01) return;
        Random = percent;
        View();
    }

    public void SetFlip(bool flip) { if (Flip == flip) return; Flip = flip; View(); }
    public void SetAutoTile(bool on) { if (AutoTileOn == on) return; AutoTileOn = on; View(); }
    public void SetRuleSet(string? name) { if (RuleSet == name) return; RuleSet = name; SelectedRule = 0; View(); }
    public void SelectRule(int rule) { if (SelectedRule == rule) return; SelectedRule = rule; View(); }

    /// <summary>Seeds the Random brush (tests and reproducible strokes).</summary>
    public void Seed(int seed) => _rng = new Random(seed);

    public string StampText => $"{StampWidth} × {StampHeight} tiles";
    public string BrushText => $"Brush {StampWidth}×{StampHeight} · {Doc.TileName(SelectedTile)}";

    // ---- collision view ----------------------------------------------------------------------------

    bool _collisionView;
    public bool CollisionView { get => _collisionView; set { if (_collisionView == value) return; _collisionView = value; View(); } }
    public bool ShowsCollision(CollisionType t) => !_hiddenCollision.Contains(t);
    public void ToggleCollisionType(CollisionType t)
    {
        if (!_hiddenCollision.Remove(t)) _hiddenCollision.Add(t);
        View();
    }

    // ---- camera ------------------------------------------------------------------------------------

    public const int ScreenW = 240, ScreenH = 160;
    public static readonly int[] Zooms = [1, 2, 3, 4];
    public int CamX { get; private set; }
    public int CamY { get; private set; }
    public int Zoom { get; private set; } = 3;

    public void SetCamera(int x, int y)
    {
        var (ox, oy) = (CamX, CamY);
        CamX = x;
        CamY = y;
        ClampCamera();
        if (CamX != ox || CamY != oy) View();
    }

    void ClampCamera()
    {
        int w = Doc.Width * 16, h = Doc.Height * 16;
        CamX = w <= ScreenW ? 0 : Math.Clamp(CamX, 0, w - ScreenW);
        CamY = h <= ScreenH ? 0 : Math.Clamp(CamY, 0, h - ScreenH);
    }

    public void SetZoom(int zoom) { zoom = Math.Clamp(zoom, 1, 4); if (Zoom == zoom) return; Zoom = zoom; View(); }
    public void NextZoom() => SetZoom(Zoom >= 4 ? 1 : Zoom + 1);

    /// <summary>The map cell of the current layer under a screen pixel (parallax applied).</summary>
    public (int X, int Y) CellAt(int screenX, int screenY)
    {
        var l = CurrentLayer;
        var sx = MapRenderer.Scroll(CamX, l?.ParallaxX ?? 1);
        var sy = MapRenderer.Scroll(CamY, l?.ParallaxY ?? 1);
        return ((int)Math.Floor((sx + screenX) / 16.0), (int)Math.Floor((sy + screenY) / 16.0));
    }

    /// <summary>The screen pixel of a cell's top-left on the current layer.</summary>
    public (int X, int Y) ScreenOf(int cellX, int cellY)
    {
        var l = CurrentLayer;
        return (cellX * 16 - MapRenderer.Scroll(CamX, l?.ParallaxX ?? 1), cellY * 16 - MapRenderer.Scroll(CamY, l?.ParallaxY ?? 1));
    }

    (int X, int Y)? _hover;
    public (int X, int Y)? Hover { get => _hover; set { if (_hover == value) return; _hover = value; View(); } }

    /// <summary>Status bar: "BG2 Ground · tile 12, 4 · id 1 grass_top".</summary>
    public string? HoverText
    {
        get
        {
            if (Hover is not { } h || CurrentLayer is not { } l) return null;
            var cell = l.Type == LayerType.Image ? 0 : Doc.Cell(_layer, h.X, h.Y);
            var id = cell & MapDocument.TileMask;
            return $"{LayerLabel(l, " ")} · tile {h.X}, {h.Y} · id {id} {Doc.TileName(id)}";
        }
    }

    public static string LayerLabel(MapLayerData l, string sep = " · ") => l.Type == LayerType.CollisionOnly || l.Bg < 0 ? l.Name : $"BG{l.Bg}{sep}{l.Name}";

    // ---- painting ----------------------------------------------------------------------------------

    int TileFor(int sx, int sy, bool flip)
    {
        var x = flip ? StampWidth - 1 - sx : sx;
        var t = Stamp[sy * StampWidth + x];
        if (Random > 0 && _rng.NextDouble() * 100 < Random) t = Stamp[_rng.Next(Stamp.Length)];
        return t;
    }

    public void BeginStroke() { if (CanPaint) Doc.BeginStroke(_layer); }

    public void EndStroke(string name = "Paint") => Doc.EndStroke(name);

    /// <summary>Brush: paints the stamp with its top-left at (x, y).</summary>
    public void PaintAt(int x, int y)
    {
        if (!CanPaint) return;
        var own = !Doc.InStroke;
        if (own) Doc.BeginStroke(_layer);
        var set = AutoTileOn ? CurrentSet : null;
        for (var sy = 0; sy < StampHeight; sy++)
            for (var sx = 0; sx < StampWidth; sx++)
            {
                var t = TileFor(sx, sy, Flip);
                if (set != null && !Flip && set.Rules.Any(r => r.Tile == (t & MapDocument.TileMask))) Doc.PaintAuto(_layer, x + sx, y + sy, set, false);
                else Doc.SetCell(_layer, x + sx, y + sy, (t & MapDocument.TileMask) | (Flip ? MapDocument.FlipH : 0));
            }
        if (own) Doc.EndStroke("Paint");
        else Doc.Preview(MapChange.Cells);
    }

    /// <summary>Eraser: empties the cells under the stamp (auto-tile neighbours re-resolve).</summary>
    public void EraseAt(int x, int y)
    {
        if (!CanPaint) return;
        var own = !Doc.InStroke;
        if (own) Doc.BeginStroke(_layer);
        var set = AutoTileOn ? CurrentSet : null;
        for (var sy = 0; sy < StampHeight; sy++)
            for (var sx = 0; sx < StampWidth; sx++)
            {
                var c = Doc.Cell(_layer, x + sx, y + sy);
                if (set != null && set.Rules.Any(r => r.Tile == c)) Doc.PaintAuto(_layer, x + sx, y + sy, set, true);
                else Doc.SetCell(_layer, x + sx, y + sy, 0);
            }
        if (own) Doc.EndStroke("Erase");
        else Doc.Preview(MapChange.Cells);
    }

    /// <summary>Rectangle: fills the rectangle with the stamp pattern.</summary>
    public void FillRect(int x0, int y0, int x1, int y1)
    {
        if (!CanPaint) return;
        if (x1 < x0) (x0, x1) = (x1, x0);
        if (y1 < y0) (y0, y1) = (y1, y0);
        Doc.BeginStroke(_layer);
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var t = TileFor(Mod(x - x0, StampWidth), Mod(y - y0, StampHeight), Flip);
                Doc.SetCell(_layer, x, y, t | (Flip ? MapDocument.FlipH : 0));
            }
        Doc.EndStroke("Fill rectangle");
    }

    /// <summary>Fill: replaces the connected cells equal to the clicked one with the stamp pattern.</summary>
    public void FloodFill(int x, int y)
    {
        if (!CanPaint || x < 0 || y < 0 || x >= Doc.Width || y >= Doc.Height) return;
        var target = Doc.Cell(_layer, x, y);
        var seen = new bool[Doc.Width * Doc.Height];
        var queue = new Queue<(int, int)>();
        queue.Enqueue((x, y));
        seen[y * Doc.Width + x] = true;
        Doc.BeginStroke(_layer);
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            var t = TileFor(Mod(cx - x, StampWidth), Mod(cy - y, StampHeight), Flip);
            Doc.SetCell(_layer, cx, cy, t | (Flip ? MapDocument.FlipH : 0));
            foreach (var (nx, ny) in new[] { (cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1) })
            {
                if (nx < 0 || ny < 0 || nx >= Doc.Width || ny >= Doc.Height || seen[ny * Doc.Width + nx]) continue;
                seen[ny * Doc.Width + nx] = true;
                if (Doc.Map.Layers[_layer].Cells[ny * Doc.Width + nx] == target) queue.Enqueue((nx, ny));
            }
        }
        Doc.EndStroke("Fill");
    }

    /// <summary>Pick: the clicked cell becomes the brush (with its flip).</summary>
    public void PickAt(int x, int y)
    {
        if (CurrentLayer is not { } l || l.Type == LayerType.Image) return;
        var c = Doc.Cell(_layer, x, y);
        SelectedTile = c & MapDocument.TileMask;
        Flip = (c & MapDocument.FlipH) != 0;
        SetStamp([SelectedTile], 1, 1);
    }

    /// <summary>Select: the cells of a rectangle of the map become the stamp.</summary>
    public void StampFromMap(int x0, int y0, int x1, int y1)
    {
        if (CurrentLayer is not { } l || l.Type == LayerType.Image) return;
        if (x1 < x0) (x0, x1) = (x1, x0);
        if (y1 < y0) (y0, y1) = (y1, y0);
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
        x1 = Math.Min(Doc.Width - 1, x1); y1 = Math.Min(Doc.Height - 1, y1);
        if (x1 < x0 || y1 < y0) return;
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        var cells = new int[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) cells[y * w + x] = Doc.Cell(_layer, x0 + x, y0 + y) & MapDocument.TileMask;
        SelectedTile = cells[0];
        SetStamp(cells, w, h);
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    // ---- stats (GBA Budget) ------------------------------------------------------------------------

    public VramPlan Plan() => Renderer.Plan(Doc);
}
