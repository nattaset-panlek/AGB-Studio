using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>
/// The maps open in the Tilemap Editor, one <see cref="TilemapSession"/> per .map file, kept while the project is
/// open so switching documents never drops unsaved edits. Each map has an <see cref="EditorDocument"/> (Doc Tab,
/// dirty dot, Save / Save All / autosave write the .map and the tileset properties).
/// </summary>
public sealed class MapLibrary
{
    readonly EditorServices _s;
    readonly Dictionary<string, TilemapSession> _open = new(StringComparer.OrdinalIgnoreCase);

    public MapRenderer Renderer { get; private set; } = new();
    public TilemapSession? Active { get; private set; }

    MapLibrary(EditorServices s)
    {
        _s = s;
        s.Project.Changed += () =>
        {
            _open.Clear();
            _peek.Clear();
            Active = null;
            Renderer = new MapRenderer();
        };
    }

    public static MapLibrary Of(EditorServices s)
    {
        if (s.GetService(typeof(MapLibrary)) is MapLibrary m) return m;
        m = new MapLibrary(s);
        s.Set(m);
        AddLayerMenu.Wire(s, m);
        return m;
    }

    /// <summary>The .map a window was opened for: a session, a document, a path, else the active or first map of the project.</summary>
    public string? Resolve(object? args)
    {
        var p = _s.Project.Current;
        if (p is null) return null;
        string? Exists(string? path) => path != null && path.EndsWith(".map", StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? Path.GetFullPath(path) : null;
        switch (args)
        {
            case TilemapSession ts: return ts.Doc.Path;
            case EditorDocument d when Exists(d.Path) is { } dp: return dp;
            case string s when Exists(Path.IsPathRooted(s) ? s : Path.Combine(p.MapsDir, s.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? s : s + ".map")) is { } sp: return sp;
        }
        if (Active != null) return Active.Doc.Path;
        if (Exists(_s.Documents.Active?.Path) is { } active) return active;
        foreach (var d in _s.Documents.Open) if (Exists(d.Path) is { } open) return open;
        var last = p.Settings.LastScene;
        var scene = p.SceneFiles().FirstOrDefault(f => GameProject.AssetName(f) == last);
        if (scene != null)
        {
            try
            {
                var map = GameProject.Load<SceneFile>(scene).Map;
                if (Exists(Path.Combine(p.MapsDir, map + ".map")) is { } sm) return sm;
            }
            catch (System.Text.Json.JsonException) { }
        }
        return p.MapFiles().Select(Path.GetFullPath).FirstOrDefault();
    }

    public TilemapSession? Current(object? args) => Resolve(args) is { } path ? Open(path) : null;

    readonly Dictionary<string, MapDocument> _peek = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A map for display only (previews): the open one when it is being edited, else read from disk without a Doc Tab.</summary>
    public MapDocument? Peek(string? path)
    {
        if (path is null || !File.Exists(path) || _s.Project.Current is not { } p) return null;
        path = Path.GetFullPath(path);
        if (_open.TryGetValue(path, out var s)) return s.Doc;
        if (!_peek.TryGetValue(path, out var d)) _peek[path] = d = MapDocument.Load(p, path, new UndoService(1));
        return d;
    }

    /// <summary>The map of the scene the project was last on (Settings.LastScene, else the start scene, else the first scene).</summary>
    public MapDocument? SceneMap()
    {
        if (_s.Project.Current is not { } p) return null;
        var names = new[] { p.Settings.LastScene, p.Settings.General.StartScene };
        var scene = names.Select(n => p.SceneFiles().FirstOrDefault(f => GameProject.AssetName(f) == n)).FirstOrDefault(f => f != null) ?? p.SceneFiles().FirstOrDefault();
        if (scene is null) return null;
        try { return Peek(Path.Combine(p.MapsDir, GameProject.Load<SceneFile>(scene).Map + ".map")); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    public TilemapSession Open(string path)
    {
        path = Path.GetFullPath(path);
        if (!_open.TryGetValue(path, out var session))
        {
            var doc = MapDocument.Load(_s.Project.Current!, path, _s.Undo);
            session = new TilemapSession(doc, Renderer, _s);
            _open[path] = session;
        }
        Active = session;
        Attach(session.Doc);
        return session;
    }

    /// <summary>The map's Doc Tab: found by path (or name) or added, made active; Save writes the map.</summary>
    EditorDocument Attach(MapDocument doc)
    {
        var docs = _s.Documents;
        var ed = docs.Open.FirstOrDefault(d => d.Path != null && string.Equals(Path.GetFullPath(d.Path), doc.Path, StringComparison.OrdinalIgnoreCase))
                 ?? docs.Open.FirstOrDefault(d => d.Path is null && d.Name == doc.FileName)
                 ?? docs.Add(new EditorDocument { Name = doc.FileName, Glyph = "tiles", Path = doc.Path });
        if (ed.SaveHandler is null)
        {
            ed.SaveHandler = _ => { doc.Save(); return Task.CompletedTask; };
            doc.DirtyChanged += () => ed.IsDirty = doc.IsDirty;
        }
        docs.Active = ed;
        return ed;
    }
}

/// <summary>Add Layer (T-M20, Tilemap Editor · right-click the layers table or Map ··· ): Tile, Image, Rotate / scale (Mode 1-2), Collision-only.</summary>
public static class AddLayerMenu
{
    [EditorMenu("T-M20")]
    public static IReadOnlyList<MenuEntry> Entries() =>
    [
        new CommandEntry("tilemap.addLayer.tile", true), new CommandEntry("tilemap.addLayer.image", true),
        new CommandEntry("tilemap.addLayer.affine", true), SeparatorEntry.Instance, new CommandEntry("tilemap.addLayer.collision", true),
    ];

    [EditorCommand("tilemap.addLayer.tile", "Tile layer", Category = "Tilemap", Glyph = "tiles", InPalette = false)]
    public static void Tile(EditorServices s) => Add(s, LayerType.Tile);

    [EditorCommand("tilemap.addLayer.image", "Image layer", Category = "Tilemap", Glyph = "image", InPalette = false)]
    public static void Image(EditorServices s) => Add(s, LayerType.Image);

    [EditorCommand("tilemap.addLayer.affine", "Rotate / scale layer (Mode 1–2)", Category = "Tilemap", Glyph = "refresh", InPalette = false)]
    public static void Affine(EditorServices s) => Add(s, LayerType.Affine);

    [EditorCommand("tilemap.addLayer.collision", "Collision-only layer", Category = "Tilemap", Glyph = "square", InPalette = false)]
    public static void Collision(EditorServices s) => Add(s, LayerType.CollisionOnly);

    static readonly (string Id, LayerType Type)[] Commands =
        [("tilemap.addLayer.tile", LayerType.Tile), ("tilemap.addLayer.image", LayerType.Image), ("tilemap.addLayer.affine", LayerType.Affine), ("tilemap.addLayer.collision", LayerType.CollisionOnly)];

    /// <summary>Adds a layer of the type to the active map and selects it; returns its index or -1.</summary>
    public static int Add(EditorServices s, LayerType type)
    {
        var session = MapLibrary.Of(s).Active;
        if (session is null) return -1;
        var index = session.Doc.AddLayer(type);
        if (index >= 0) session.Layer = index;
        return index;
    }

    /// <summary>Disables an entry when no background is free (four BG layers) or no map is open.</summary>
    internal static void Wire(EditorServices s, MapLibrary lib)
    {
        foreach (var (id, type) in Commands)
        {
            var t = type;
            s.Commands.Get(id).Handle(() => Add(s, t), () => lib.Active?.Doc.FreeBg(t) != null);
        }
    }
}
