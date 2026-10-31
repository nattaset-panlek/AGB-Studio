using System.Text.Json;
using System.Text.Json.Nodes;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>Background size of a tile layer (Tilemap Editor "BG size"; runtime bg::Size).</summary>
public enum BgSize { S32x32, S64x32, S32x64, S64x64 }

/// <summary>What changed in a <see cref="MapDocument"/> (views redraw only what they need).</summary>
[Flags]
public enum MapChange { None = 0, Cells = 1, Layers = 2, Tileset = 4, Map = 8, All = 15 }

/// <summary>
/// One open maps/&lt;Name&gt;.map (T-P03) with its tileset properties (&lt;tileset&gt;.png.meta.json: collision, tags,
/// animation, auto-tile sets) and the scene that uses it. Every edit goes through <see cref="Do"/> so it is undoable,
/// marks the document dirty and raises <see cref="Changed"/>. Cells hold the metatile index; bit 15 is the
/// horizontal flip painted with Brush · Flip H.
/// </summary>
public sealed class MapDocument
{
    public const int FlipH = 0x8000;
    public const int TileMask = 0x7FFF;

    JsonObject _extras = new();
    readonly UndoService _undo;

    public GameProject Project { get; }
    public string Path { get; }
    public MapFile Map { get; private set; }
    public string? TilesetPath { get; private set; }
    public TilesetMeta Meta { get; private set; } = new();
    public ColorImage? TilesetImage { get; private set; }
    public SceneFile? Scene { get; }
    public string? ScenePath { get; }
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>"BG size" (stored as "bgSize" in the .map; the runtime default is 64x32).</summary>
    public BgSize BgSize { get; private set; } = BgSize.S64x32;
    /// <summary>"Colours": 4 (16 per tile) or 8 (256 colours); stored as "bpp" in the .map.</summary>
    public int Bpp { get; private set; } = 4;

    public bool IsDirty { get; private set; }
    /// <summary>Increments with every change (render caches compare it).</summary>
    public int Version { get; private set; }
    public int TilesetVersion { get; private set; }
    public event Action<MapChange>? Changed;
    public event Action? DirtyChanged;

    MapDocument(GameProject project, string path, MapFile map, UndoService undo)
    {
        Project = project;
        Path = System.IO.Path.GetFullPath(path);
        Map = map;
        _undo = undo;
        foreach (var file in project.SceneFiles())
        {
            try
            {
                var s = GameProject.Load<SceneFile>(file);
                if (!string.Equals(s.Map, Map.Name, StringComparison.OrdinalIgnoreCase) && !string.Equals(s.Map, System.IO.Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase)) continue;
                Scene = s;
                ScenePath = file;
                break;
            }
            catch (JsonException) { }
        }
    }

    public static MapDocument Load(GameProject project, string path, UndoService undo)
    {
        var text = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<MapFile>(text, GameProject.Json) ?? new MapFile();
        var doc = new MapDocument(project, path, map, undo);
        if (JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is JsonObject o)
        {
            // keep fields this version does not know (written back on save)
            var known = JsonSerializer.SerializeToNode(map, GameProject.Json)!.AsObject();
            foreach (var (k, v) in o)
                if (!known.ContainsKey(k)) doc._extras[k] = v?.DeepClone();
        }
        doc.BgSize = map.BgSize switch { "32x32" => BgSize.S32x32, "32x64" => BgSize.S32x64, "64x64" => BgSize.S64x64, _ => BgSize.S64x32 };
        doc.Bpp = map.Bpp == 8 ? 8 : 4;
        doc.LoadTileset();
        return doc;
    }

    void LoadTileset()
    {
        TilesetPath = Project.TilesetFiles().FirstOrDefault(f => string.Equals(GameProject.AssetName(f), Map.Tileset, StringComparison.OrdinalIgnoreCase));
        Meta = TilesetPath is null ? new TilesetMeta() : Project.LoadMeta<TilesetMeta>(TilesetPath);
        TilesetImage = TilesetPath is null ? null : ColorImage.Load(TilesetPath);
        TilesetVersion++;
    }

    /// <summary>Reads the .map again after another editor wrote it (only while there are no unsaved edits here).</summary>
    public void ReloadMap()
    {
        if (IsDirty || !File.Exists(Path)) return;
        Map = JsonSerializer.Deserialize<MapFile>(File.ReadAllText(Path), GameProject.Json) ?? Map;
        Version++;
        Changed?.Invoke(MapChange.All);
    }

    // ---- saving ------------------------------------------------------------------------------------

    public string Serialize()
    {
        // "bgSize" and "bpp" are fields of the .map (MapFile, docs/schemas/map.schema.json), left out at their defaults
        Map.BgSize = BgSize == BgSize.S64x32 ? null : SizeKey(BgSize);
        Map.Bpp = Bpp == 4 ? null : Bpp;
        var node = JsonSerializer.SerializeToNode(Map, GameProject.Json)!.AsObject();
        foreach (var (k, v) in _extras) node[k] = v?.DeepClone();
        return node.ToJsonString(GameProject.Json) + "\n";
    }

    public void Save()
    {
        var text = Serialize();
        if (!File.Exists(Path) || File.ReadAllText(Path) != text) File.WriteAllText(Path, text);
        if (TilesetPath != null) GameProject.Save(GameProject.MetaPath(TilesetPath), Meta);
        SetDirty(false);
    }

    void SetDirty(bool dirty)
    {
        if (IsDirty == dirty) return;
        IsDirty = dirty;
        DirtyChanged?.Invoke();
    }

    public static string SizeKey(BgSize s) => s switch { BgSize.S32x32 => "32x32", BgSize.S32x64 => "32x64", BgSize.S64x64 => "64x64", _ => "64x32" };

    // ---- edits -------------------------------------------------------------------------------------

    /// <summary>Applies an edit and records it for Undo / Redo.</summary>
    public void Do(string name, Action redo, Action undo, MapChange change)
    {
        _undo.Do(new DelegateAction(name, () => { undo(); Touched(change); }, () => { redo(); Touched(change); }));
    }

    /// <summary>Records an edit that is already applied (a paint stroke).</summary>
    public void Push(string name, Action redo, Action undo, MapChange change)
    {
        _undo.Push(new DelegateAction(name, () => { undo(); Touched(change); }, () => { redo(); Touched(change); }));
        Touched(change);
    }

    public void Touched(MapChange change)
    {
        Version++;
        if (change.HasFlag(MapChange.Tileset)) TilesetVersion++;
        SetDirty(true);
        Changed?.Invoke(change);
    }

    /// <summary>Raised for live feedback during a stroke (not recorded yet).</summary>
    public void Preview(MapChange change)
    {
        Version++;
        Changed?.Invoke(change);
    }

    public int Width => Map.Width;
    public int Height => Map.Height;
    public int TilesetColumns => TilesetImage is null ? 0 : TilesetImage.Width / 16;
    public int TileCount => TilesetImage is null ? 0 : TilesetImage.Width / 16 * (TilesetImage.Height / 16);
    public bool HasCells(MapLayerData l) => l.Type != LayerType.Image;

    public int Cell(int layer, int x, int y)
    {
        var l = Map.Layers[layer];
        return x < 0 || y < 0 || x >= Width || y >= Height || l.Cells.Count != Width * Height ? 0 : l.Cells[y * Width + x];
    }

    public string TileName(int tile) => Meta.Tiles.TryGetValue(tile, out var p) && p.Name.Length > 0 ? p.Name : tile == 0 ? "empty" : "tile " + tile;
    public TileProperties? Props(int tile) => Meta.Tiles.GetValueOrDefault(tile);
    public CollisionType CollisionOf(int cell) => Props(cell & TileMask)?.Collision ?? CollisionType.None;

    /// <summary>Index of the layer collision is read from (the runtime rule: CollisionLayer, else the first tile layer).</summary>
    public int CollisionLayerIndex => Map.CollisionLayer >= 0 && Map.CollisionLayer < Map.Layers.Count ? Map.CollisionLayer : Map.Layers.FindIndex(l => l.Type == LayerType.Tile);

    /// <summary>Cells of the collision layer per collision type (Collision · Solid tiles / One-way).</summary>
    public int CountCollision(CollisionType type)
    {
        var i = CollisionLayerIndex;
        if (i < 0) return 0;
        return Map.Layers[i].Cells.Count(c => CollisionOf(c) == type);
    }

    public IEnumerable<CollisionType> CollisionTypesUsed()
    {
        var i = CollisionLayerIndex;
        if (i < 0) return [];
        return Map.Layers[i].Cells.Select(CollisionOf).Where(c => c != CollisionType.None).Distinct().OrderBy(c => c);
    }

    // ---- map settings ------------------------------------------------------------------------------

    public void Resize(int w, int h)
    {
        w = Math.Clamp(w, 1, 1024);
        h = Math.Clamp(h, 1, 1024);
        if (w == Width && h == Height) return;
        var oldW = Width;
        var oldH = Height;
        var old = Map.Layers.Select(l => l.Cells.ToList()).ToList();
        void Apply(int nw, int nh, List<List<int>>? source)
        {
            for (var li = 0; li < Map.Layers.Count; li++)
            {
                var l = Map.Layers[li];
                if (!HasCells(l)) continue;
                if (source != null) { l.Cells = source[li].ToList(); continue; }
                var cells = new List<int>(new int[nw * nh]);
                for (var y = 0; y < Math.Min(nh, oldH); y++)
                    for (var x = 0; x < Math.Min(nw, oldW); x++)
                        if (y * oldW + x < old[li].Count) cells[y * nw + x] = old[li][y * oldW + x];
                l.Cells = cells;
            }
            Map.Width = nw;
            Map.Height = nh;
        }
        Do("Resize map", () => Apply(w, h, null), () => Apply(oldW, oldH, old), MapChange.Map | MapChange.Cells);
    }

    public void SetBgSize(BgSize size)
    {
        var old = BgSize;
        if (old == size) return;
        Do("BG size", () => BgSize = size, () => BgSize = old, MapChange.Map);
    }

    public void SetBpp(int bpp)
    {
        var old = Bpp;
        if (old == bpp) return;
        Do("Colours", () => Bpp = bpp, () => Bpp = old, MapChange.Map);
    }

    public void SetWrap(int layer, bool wrap)
    {
        var l = Map.Layers[layer];
        var old = l.Wrap;
        if (old == wrap) return;
        Do("Wrap", () => l.Wrap = wrap, () => l.Wrap = old, MapChange.Layers);
    }

    /// <summary>Tileset · Set: the map uses another tileset of assets/tilesets (its properties load with it).</summary>
    public bool SetTileset(string name)
    {
        var old = Map.Tileset;
        if (string.Equals(old, name, StringComparison.OrdinalIgnoreCase)) return false;
        if (!Project.TilesetFiles().Any(f => string.Equals(GameProject.AssetName(f), name, StringComparison.OrdinalIgnoreCase))) return false;
        void Switch(string to)
        {
            // properties edited for the tileset being left are kept in its meta file
            if (TilesetPath != null) GameProject.Save(GameProject.MetaPath(TilesetPath), Meta);
            Map.Tileset = to;
            LoadTileset();
        }
        Do("Tileset", () => Switch(name), () => Switch(old), MapChange.All);
        return true;
    }

    public void SetCollisionLayer(int index)
    {
        var old = Map.CollisionLayer;
        if (old == index) return;
        Do("Collision source", () => Map.CollisionLayer = index, () => Map.CollisionLayer = old, MapChange.Map | MapChange.Cells);
    }

    // ---- layers ------------------------------------------------------------------------------------

    public static string DefaultName(LayerType t) => t switch
    {
        LayerType.Image => "Image", LayerType.Affine => "Rotate / scale", LayerType.CollisionOnly => "Collision", _ => "Tiles",
    };

    /// <summary>The background a new layer of this type would use, or null when none is free (collision-only layers use none).</summary>
    public int? FreeBg(LayerType type)
    {
        if (type == LayerType.CollisionOnly) return -1;
        var used = Map.Layers.Where(l => l.Type != LayerType.CollisionOnly).Select(l => l.Bg).ToHashSet();
        // Rotate / scale backgrounds are BG2 (Mode 1) or BG2 / BG3 (Mode 2).
        int[] order = type == LayerType.Affine ? [2, 3] : [0, 1, 2, 3];
        foreach (var bg in order) if (!used.Contains(bg)) return bg;
        return null;
    }

    /// <summary>Add Layer (T-M20): a new layer on the first free background; returns its index or -1.</summary>
    public int AddLayer(LayerType type, string? name = null)
    {
        if (FreeBg(type) is not { } bg) return -1;
        var names = Map.Layers.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = name ?? DefaultName(type);
        var unique = baseName;
        for (var n = 2; names.Contains(unique); n++) unique = $"{baseName} {n}";
        var layer = new MapLayerData
        {
            Name = unique, Bg = bg, Type = type, Priority = 0, ParallaxX = 1, ParallaxY = 1,
            Cells = type == LayerType.Image ? new List<int>() : new List<int>(new int[Width * Height]),
        };
        if (type == LayerType.Image)
        {
            var used = Map.Layers.Select(l => l.Image).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var images = Project.ImageFiles().Select(GameProject.AssetName).ToList();
            layer.Image = images.FirstOrDefault(i => !used.Contains(i)) ?? images.FirstOrDefault() ?? "";
            layer.Wrap = true;
        }
        // keep the file in BG order, collision-only layers last
        var index = type == LayerType.CollisionOnly ? Map.Layers.Count : Map.Layers.FindIndex(l => l.Type == LayerType.CollisionOnly || l.Bg > bg);
        if (index < 0) index = Map.Layers.Count;
        var oldCollision = Map.CollisionLayer;
        Do("Add layer", () =>
        {
            Map.Layers.Insert(index, layer);
            if (Map.CollisionLayer >= index) Map.CollisionLayer++;
            // a collision-only layer exists to hold collision: it becomes the collision source
            if (type == LayerType.CollisionOnly) Map.CollisionLayer = index;
        }, () =>
        {
            Map.Layers.Remove(layer);
            Map.CollisionLayer = oldCollision;
        }, MapChange.Layers | MapChange.Cells);
        return index;
    }

    public void RemoveLayer(int index)
    {
        var layer = Map.Layers[index];
        var oldCollision = Map.CollisionLayer;
        Do("Delete layer", () =>
        {
            Map.Layers.RemoveAt(index);
            Map.CollisionLayer = oldCollision == index ? -1 : oldCollision > index ? oldCollision - 1 : oldCollision;
        }, () =>
        {
            Map.Layers.Insert(index, layer);
            Map.CollisionLayer = oldCollision;
        }, MapChange.Layers | MapChange.Cells);
    }

    // ---- tile properties (tileset meta) -------------------------------------------------------------

    void EditProps(string name, int tile, Action<TileProperties> change)
    {
        var had = Meta.Tiles.TryGetValue(tile, out var before);
        var snapshot = Clone(before ?? new TileProperties());
        var after = Clone(snapshot);
        change(after);
        Do(name, () => Meta.Tiles[tile] = Clone(after), () =>
        {
            if (had) Meta.Tiles[tile] = Clone(snapshot);
            else Meta.Tiles.Remove(tile);
        }, MapChange.Tileset | MapChange.Cells);
    }

    static TileProperties Clone(TileProperties p) => new()
    {
        Name = p.Name, Collision = p.Collision, Tag = p.Tag, Animation = p.Animation?.ToList(), AnimationTicks = p.AnimationTicks,
    };

    public void SetCollision(int tile, CollisionType type)
    {
        if ((Props(tile)?.Collision ?? CollisionType.None) == type) return;
        EditProps("Collision", tile, p => p.Collision = type);
    }

    public void SetTag(int tile, string tag)
    {
        tag = tag.Trim();
        if ((Props(tile)?.Tag ?? "") == tag) return;
        EditProps("Tag", tile, p => p.Tag = tag);
    }

    /// <summary>Animation: <paramref name="frames"/> consecutive tiles of the tileset starting at the tile (0 or 1 = none).</summary>
    public void SetAnimation(int tile, int frames)
    {
        var current = Props(tile)?.Animation is { Count: > 1 } a ? a.Count : 0;
        if (frames <= 1) frames = 0;
        if (current == frames) return;
        EditProps("Animation", tile, p => p.Animation = frames == 0 ? null : Enumerable.Range(tile, frames).Where(t => t < TileCount).ToList());
    }

    public int AnimationFrames(int tile) => Props(tile)?.Animation is { Count: > 1 } a ? a.Count : 0;

    // ---- auto-tile sets ----------------------------------------------------------------------------

    public static string SetLabel(AutoTileSet s) => $"{s.Name} ({s.Rules.Count})";

    public AutoTileSet? SetOf(int tile) => Meta.AutoTiles.FirstOrDefault(s => s.Rules.Any(r => r.Tile == tile));

    void EditSets(string name, Action<List<AutoTileSet>> change)
    {
        var before = CloneSets(Meta.AutoTiles);
        var after = CloneSets(before);
        change(after);
        Do(name, () => Meta.AutoTiles = CloneSets(after), () => Meta.AutoTiles = CloneSets(before), MapChange.Tileset);
    }

    static List<AutoTileSet> CloneSets(IEnumerable<AutoTileSet> sets) =>
        sets.Select(s => new AutoTileSet { Name = s.Name, Rules = s.Rules.Select(r => new AutoTileRule { Name = r.Name, Mask = r.Mask.ToArray(), Tile = r.Tile }).ToList() }).ToList();

    /// <summary>Tile properties · Auto-tile: makes the tile a member of the set (a rule for it), or of none.</summary>
    public void SetAutoTile(int tile, string? setName)
    {
        if (SetOf(tile)?.Name == setName) return;
        EditSets("Auto-tile set", sets =>
        {
            foreach (var s in sets) s.Rules.RemoveAll(r => r.Tile == tile);
            if (setName is null) return;
            var target = sets.FirstOrDefault(s => s.Name == setName);
            if (target is null) return;
            target.Rules.Add(new AutoTileRule { Name = TileName(tile), Mask = [2, 2, 2, 2, 1, 2, 2, 2, 2], Tile = tile });
        });
    }

    /// <summary>Auto-tile Rules · Add rule: a rule for <paramref name="tile"/> that matches any neighbours until edited.</summary>
    public int AddRule(string setName, int tile)
    {
        var index = -1;
        EditSets("Add rule", sets =>
        {
            var set = sets.FirstOrDefault(s => s.Name == setName);
            if (set is null)
            {
                set = new AutoTileSet { Name = setName, Rules = [] };
                sets.Add(set);
            }
            var names = set.Rules.Select(r => r.Name).ToHashSet();
            var name = "Rule";
            for (var n = 2; names.Contains(name); n++) name = $"Rule {n}";
            set.Rules.Insert(0, new AutoTileRule { Name = name, Mask = [2, 2, 2, 2, 1, 2, 2, 2, 2], Tile = tile });
            index = 0;
        });
        return index;
    }

    public void RemoveRule(string setName, int rule) =>
        EditSets("Delete rule", sets =>
        {
            var set = sets.FirstOrDefault(s => s.Name == setName);
            if (set != null && rule >= 0 && rule < set.Rules.Count) set.Rules.RemoveAt(rule);
        });

    /// <summary>Cycles one cell of a rule's 3x3 mask: same set (1) -> not the set (0) -> any (2).</summary>
    public void CycleMask(string setName, int rule, int cell)
    {
        if (cell == 4) return;
        EditSets("Edit rule", sets =>
        {
            var r = sets.FirstOrDefault(s => s.Name == setName)?.Rules.ElementAtOrDefault(rule);
            if (r is null) return;
            if (r.Mask.Length < 9) r.Mask = [.. r.Mask, .. new int[9 - r.Mask.Length]];
            r.Mask[cell] = r.Mask[cell] switch { 1 => 0, 0 => 2, _ => 1 };
        });
    }

    public void SetRuleTile(string setName, int rule, int tile) =>
        EditSets("Rule tile", sets =>
        {
            var r = sets.FirstOrDefault(s => s.Name == setName)?.Rules.ElementAtOrDefault(rule);
            if (r != null) r.Tile = tile;
        });

    // ---- painting ----------------------------------------------------------------------------------

    sealed class Stroke(int layer)
    {
        public int Layer { get; } = layer;
        public Dictionary<int, (int Old, int New)> Cells { get; } = [];
    }

    Stroke? _stroke;
    public bool InStroke => _stroke != null;

    public void BeginStroke(int layer) => _stroke = new Stroke(layer);

    /// <summary>Sets one cell inside the current stroke (or as its own step when no stroke is open).</summary>
    public void SetCell(int layer, int x, int y, int value)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        var own = _stroke is null;
        if (own) BeginStroke(layer);
        Write(layer, y * Width + x, value);
        if (own) EndStroke("Paint");
    }

    void Write(int layer, int index, int value)
    {
        var cells = Map.Layers[layer].Cells;
        if (cells.Count != Width * Height) return;
        var old = cells[index];
        if (old == value) return;
        cells[index] = value;
        if (_stroke!.Cells.TryGetValue(index, out var e)) _stroke.Cells[index] = (e.Old, value);
        else _stroke.Cells[index] = (old, value);
    }

    /// <summary>Paints a cell with an auto-tile set: the cell and its member neighbours take the tiles of the matching rules.</summary>
    public void PaintAuto(int layer, int x, int y, AutoTileSet set, bool erase)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        var own = _stroke is null;
        if (own) BeginStroke(layer);
        var cells = Map.Layers[layer].Cells;
        var before = cells.ToArray();
        if (erase) AutoTile.Erase(cells, Width, Height, x, y, set);
        else AutoTile.Paint(cells, Width, Height, x, y, set);
        for (var i = 0; i < cells.Count; i++)
            if (cells[i] != before[i])
            {
                var v = cells[i];
                cells[i] = before[i];
                Write(layer, i, v);
            }
        if (own) EndStroke(erase ? "Erase" : "Paint");
    }

    public void EndStroke(string name)
    {
        var s = _stroke;
        _stroke = null;
        if (s is null || s.Cells.Count == 0) return;
        var layer = Map.Layers[s.Layer];
        var changes = s.Cells.ToList();
        Push(name, () => { foreach (var (i, (_, n)) in changes) layer.Cells[i] = n; },
            () => { foreach (var (i, (o, _)) in changes) layer.Cells[i] = o; }, MapChange.Cells);
    }

    public void CancelStroke()
    {
        if (_stroke is null) return;
        var layer = Map.Layers[_stroke.Layer];
        foreach (var (i, (o, _)) in _stroke.Cells) layer.Cells[i] = o;
        _stroke = null;
        Preview(MapChange.Cells);
    }
}
