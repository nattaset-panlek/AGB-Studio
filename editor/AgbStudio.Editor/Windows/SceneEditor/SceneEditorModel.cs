using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>What the Hierarchy, viewport and Inspector have selected.</summary>
public enum SceneItemKind { Scene, Camera, Backgrounds, Layer, Actor, Group }

public sealed record SceneItem(SceneItemKind Kind, int Index = -1, string Name = "")
{
    public static SceneItem ForActor(int index) => new(SceneItemKind.Actor, index);
}

/// <summary>The Scene Editor tools (toolbar, left to right): Select (V), Move (M), Paint tiles (B), Fill, Erase.</summary>
public enum SceneTool { Select, Move, Paint, Fill, Erase }

/// <summary>Grid &amp; Snap (T-O03): size 8 / 16 / 32, show grid, snap, tile collision, camera bounds, grid colour.</summary>
public sealed class GridSettings
{
    public int Size { get; set; } = 16;
    public bool ShowGrid { get; set; } = true;
    public bool Snap { get; set; } = true;
    public bool TileCollision { get; set; }
    public bool CameraBounds { get; set; } = true;
    /// <summary>0 = white, 1 = accent, 2 = red (the three Figma swatches).</summary>
    public int Colour { get; set; }
}

/// <summary>
/// The Scene Editor's open scene (T-P01): the project's .scene and .map files, the selection shared by Hierarchy,
/// viewport and Inspector, the viewport view state and the undoable edits. Every edit changes the real files
/// through the project model at once and is recorded in Services.Undo as a before / after snapshot.
/// </summary>
public sealed class SceneEditorModel
{
    readonly EditorServices _s;
    SceneAssets? _assets;
    SceneItem? _selection;
    SnapshotAction? _lastEdit;
    string? _lastMergeKey;
    int _undoCountAfterLast = -1;

    public static SceneEditorModel Of(EditorServices s)
    {
        if (s.GetService(typeof(SceneEditorModel)) is SceneEditorModel m) return m;
        m = new SceneEditorModel(s);
        s.Set(m);
        m.OpenDefault();
        return m;
    }

    SceneEditorModel(EditorServices s)
    {
        _s = s;
        ApplyDefaultSnap();
        s.Project.Changed += () =>
        {
            _assets = null;
            ApplyDefaultSnap();
            OpenDefault();
        };
    }

    public EditorServices Services => _s;

    /// <summary>Preferences › Editor › Default snap (8 / 16 / 32 px) is the grid a project opens with; 0 turns snapping off.</summary>
    void ApplyDefaultSnap()
    {
        var snap = _s.Preferences.Current.DefaultSnap;
        if (snap is 8 or 16 or 32) { Grid.Size = snap; Grid.Snap = true; }
        else if (snap <= 0) Grid.Snap = false;
    }

    /// <summary>Preferences › Editor › "Show collision overlay in Scene": off hides Grid &amp; Snap › Tile collision in the viewport.</summary>
    public bool CollisionOverlayAllowed => _s.Preferences.Current.ShowCollisionOverlay;
    public GameProject? Project => _s.Project.Current;
    public SceneAssets? Assets => Project is { } p ? _assets?.Project == p ? _assets : _assets = new SceneAssets(p) : null;

    public string? ScenePath { get; private set; }
    public SceneFile? Scene { get; private set; }
    public MapFile? Map { get; private set; }
    public string? MapPath { get; private set; }
    public string SceneName => Scene?.Name is { Length: > 0 } n ? n : ScenePath is null ? "" : GameProject.AssetName(ScenePath);

    public GridSettings Grid { get; } = new();
    /// <summary>Viewport zoom in percent (Zoom menu T-M15: 100 / 200 / 300 / 400 or Fit to Screen).</summary>
    public int Zoom { get; private set; } = 300;
    public bool FitToScreen { get; private set; }
    public bool PixelPerfect { get; private set; } = true;
    /// <summary>Viewport pan: offset of the shown GBA screen from the game's start camera, in scene pixels.</summary>
    public (int X, int Y) Pan { get; private set; }
    public ScreenLayers Layers { get; private set; } = ScreenLayers.All;
    /// <summary>Scene pixel under the pointer, or null when the pointer is outside the screen.</summary>
    public (int X, int Y)? Cursor { get; private set; }

    /// <summary>The scene or map changed (load, edit, undo).</summary>
    public event Action? Changed;
    public event Action? SelectionChanged;
    /// <summary>Zoom, pan, layers, grid or cursor changed.</summary>
    public event Action? ViewChanged;

    // ---- scenes -------------------------------------------------------------------------------------

    public IReadOnlyList<string> SceneFiles() => Project?.SceneFiles().ToList() ?? [];

    void OpenDefault()
    {
        var p = Project;
        if (p == null) { Load(null); return; }
        var files = p.SceneFiles().ToList();
        var pick = files.FirstOrDefault(f => GameProject.AssetName(f) == p.Settings.LastScene)
                   ?? files.FirstOrDefault(f => GameProject.AssetName(f) == p.Settings.General.StartScene)
                   ?? files.FirstOrDefault();
        Load(pick);
    }

    /// <summary>Opens a scene of the project by name (Scene Switcher, World Map) and remembers it as the last scene.</summary>
    public bool OpenScene(string name)
    {
        var file = SceneFiles().FirstOrDefault(f => GameProject.AssetName(f) == name);
        if (file == null) return false;
        Load(file);
        if (Project is { } p && p.Settings.LastScene != name)
        {
            p.Settings.LastScene = name;
            p.SaveSettings();
        }
        return true;
    }

    void Load(string? file)
    {
        ScenePath = file;
        Scene = file == null ? null : GameProject.Load<SceneFile>(file);
        LoadMap();
        _selection = Scene?.Actors.Count > 0 ? SceneItem.ForActor(DefaultActor()) : Scene == null ? null : new SceneItem(SceneItemKind.Scene);
        Pan = (0, 0);
        _lastEdit = null;
        UpdateDocument();
        Changed?.Invoke();
        SelectionChanged?.Invoke();
        ViewChanged?.Invoke();
        if (Scene != null) ConsoleLog.Of(_s).Info($"Scene {SceneName} loaded (BG {BgLayerCount} · OBJ {Scene.Actors.Count(a => a.Sprite.Length > 0)})");
    }

    // The player is selected when a scene opens (else the first actor).
    int DefaultActor() => Math.Max(0, Scene!.Actors.FindIndex(a => a.Kind == ActorKind.Player));

    void LoadMap()
    {
        Map = null;
        MapPath = null;
        if (Scene is not { Map.Length: > 0 } || Project is not { } p) return;
        var path = Path.Combine(p.MapsDir, Path.GetFileNameWithoutExtension(Scene.Map) + ".map");
        if (!File.Exists(path)) return;
        MapPath = path;
        Map = GameProject.Load<MapFile>(path);
    }

    /// <summary>After New Scene / Scene Settings (and their undo): project settings and the given scene from disk.</summary>
    public void ReloadProjectScenes(string scenePath)
    {
        if (Project is { } p && File.Exists(p.ProjectPath)) p.Settings = GameProject.Load<AgbStudio.Core.Project.ProjectFile>(p.ProjectPath);
        _assets = null;
        Load(File.Exists(scenePath) ? scenePath : SceneFiles().FirstOrDefault(f => GameProject.AssetName(f) == Project?.Settings.LastScene) ?? SceneFiles().FirstOrDefault());
    }

    /// <summary>Reads the open scene and map again (after asset renames, deletes and their undo).</summary>
    public void ReloadFromDisk()
    {
        if (ScenePath == null || !File.Exists(ScenePath)) { OpenDefault(); return; }
        Scene = GameProject.Load<SceneFile>(ScenePath);
        LoadMap();
        if (_selection is { Kind: SceneItemKind.Actor } s && s.Index >= Scene.Actors.Count) _selection = null;
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    public int BgLayerCount => Map?.Layers.Count(l => l.Type != LayerType.CollisionOnly) ?? 0;

    /// <summary>The "Scene" document tab shows the open scene file (title bar breadcrumb).</summary>
    void UpdateDocument()
    {
        var docs = _s.Documents;
        var old = docs.Open.FirstOrDefault(d => d.Name == "Scene");
        if (ScenePath == null)
        {
            return;
        }
        if (old != null && old.Path is { } p && string.Equals(Path.GetFullPath(p), Path.GetFullPath(ScenePath), StringComparison.OrdinalIgnoreCase))
        {
            old.SaveHandler ??= _ => { Save(); return Task.CompletedTask; };
            if (docs.Active == null) docs.Active = old;
            return;
        }
        var doc = new EditorDocument { Name = "Scene", Glyph = "map", Path = ScenePath, SaveHandler = _ => { Save(); return Task.CompletedTask; } };
        var index = old == null ? 0 : docs.Open.IndexOf(old);
        var wasActive = old == null || docs.Active == old || docs.Active == null;
        if (old != null) docs.Close(old);
        docs.Add(doc, activate: wasActive);
        var at = docs.Open.IndexOf(doc);
        if (at != index && index >= 0 && index < docs.Open.Count)
        {
            docs.Open.Move(at, index);
            // DocumentService raises Changed on Add before the move; nudge the dirty flag so the tabs re-read the order
            doc.IsDirty = true;
            doc.IsDirty = false;
        }
    }

    /// <summary>Writes the scene and its map (Save on the Scene tab; edits already write through).</summary>
    public void Save()
    {
        if (ScenePath != null && Scene != null) GameProject.Save(ScenePath, Scene);
        if (MapPath != null && Map != null) GameProject.Save(MapPath, Map);
    }

    // ---- selection ------------------------------------------------------------------------------------

    public SceneItem? Selection
    {
        get => _selection;
        set
        {
            if (_selection == value) return;
            _selection = value;
            SelectedComponent = 0;
            SelectionChanged?.Invoke();
        }
    }

    /// <summary>The component of the selected actor the Component Menu acts on (last clicked in the Inspector).</summary>
    public int SelectedComponent { get; set; }

    public int SelectedActorIndex => _selection is { Kind: SceneItemKind.Actor } s && Scene != null && s.Index < Scene.Actors.Count ? s.Index : -1;
    public ActorData? SelectedActor => SelectedActorIndex is var i and >= 0 ? Scene!.Actors[i] : null;

    public void SelectActor(int index) => Selection = index >= 0 && Scene != null && index < Scene.Actors.Count ? SceneItem.ForActor(index) : null;

    // ---- view -----------------------------------------------------------------------------------------

    public void SetZoom(int percent)
    {
        Zoom = Math.Clamp(percent, 100, 800);
        FitToScreen = false;
        ViewChanged?.Invoke();
    }

    public void SetFitToScreen()
    {
        FitToScreen = true;
        ViewChanged?.Invoke();
    }

    /// <summary>Fit to Screen picks the largest zoom (whole multiples when pixel-perfect) that shows the 240 x 160 screen.</summary>
    public double EffectiveZoom(double width, double height)
    {
        if (!FitToScreen) return Zoom / 100.0;
        var k = Math.Min((width - 16) / 240, (height - 16) / 160);
        return Math.Max(1, PixelPerfect ? Math.Floor(k) : k);
    }

    public void SetPixelPerfect(bool on)
    {
        PixelPerfect = on;
        ViewChanged?.Invoke();
    }

    public void SetPan(int x, int y)
    {
        Pan = (x, y);
        ViewChanged?.Invoke();
    }

    public void SetLayer(ScreenLayers layer, bool on)
    {
        Layers = on ? Layers | layer : Layers & ~layer;
        ViewChanged?.Invoke();
    }

    public void SetCursor((int X, int Y)? p)
    {
        if (Cursor == p) return;
        Cursor = p;
        ViewChanged?.Invoke();
    }

    public void GridChanged() => ViewChanged?.Invoke();

    /// <summary>The camera of the shown screen: the game's start camera plus the pan.</summary>
    public (int X, int Y) Camera
    {
        get
        {
            if (Scene == null) return (0, 0);
            var (x, y) = GbaScreenRenderer.StartCamera(Scene, Map);
            return (x + Pan.X, y + Pan.Y);
        }
    }

    /// <summary>The GBA screen as the ROM draws it at <see cref="Camera"/>.</summary>
    public ScreenFrame? Render() => Scene != null && Assets is { } a ? GbaScreenRenderer.Render(a, Scene, Map, Camera.X, Camera.Y, Layers) : null;

    // ---- edits ----------------------------------------------------------------------------------------

    string SceneJson() => Scene == null ? "" : JsonSerializer.Serialize(Scene, GameProject.Json);
    string MapJson() => Map == null ? "" : JsonSerializer.Serialize(Map, GameProject.Json);

    /// <summary>
    /// Applies an edit to the open scene / map, writes the files and records it for Undo. Edits with the same
    /// <paramref name="mergeKey"/> in a row (typing into a field, dragging a slider) become one undo step.
    /// </summary>
    public void Edit(string name, Action change, string? mergeKey = null)
    {
        if (Scene == null || ScenePath == null) return;
        var beforeScene = SceneJson();
        var beforeMap = MapJson();
        var selection = _selection;
        change();
        var afterScene = SceneJson();
        var afterMap = MapJson();
        if (afterScene == beforeScene && afterMap == beforeMap) { Changed?.Invoke(); return; }
        Write();
        var after = _selection;
        if (mergeKey != null && mergeKey == _lastMergeKey && _lastEdit != null && _s.Undo.UndoCount == _undoCountAfterLast && _s.Undo.NextUndo == _lastEdit.Name)
        {
            _lastEdit.AfterScene = afterScene;
            _lastEdit.AfterMap = afterMap;
            _lastEdit.AfterSelection = after;
        }
        else
        {
            _lastEdit = new SnapshotAction(this, name, ScenePath, beforeScene, beforeMap, afterScene, afterMap, selection, after);
            _s.Undo.Push(_lastEdit);
            _lastMergeKey = mergeKey;
            _undoCountAfterLast = _s.Undo.UndoCount;
        }
        Changed?.Invoke();
    }

    void Write()
    {
        if (ScenePath != null && Scene != null) GameProject.Save(ScenePath, Scene);
        if (MapPath != null && Map != null)
        {
            GameProject.Save(MapPath, Map);
            // the Tilemap Editor reads the map again when it has no unsaved edits of its own
            (_s.GetService(typeof(Tilemap.MapLibrary)) as Tilemap.MapLibrary)?.FileChanged(MapPath);
        }
    }

    void Restore(string scenePath, string scene, string map, SceneItem? selection)
    {
        if (ScenePath == null || !string.Equals(Path.GetFullPath(ScenePath), Path.GetFullPath(scenePath), StringComparison.OrdinalIgnoreCase))
        {
            Load(scenePath);
        }
        Scene = JsonSerializer.Deserialize<SceneFile>(scene, GameProject.Json);
        if (map.Length > 0) Map = JsonSerializer.Deserialize<MapFile>(map, GameProject.Json);
        Write();
        _lastMergeKey = null;
        _selection = selection is { Kind: SceneItemKind.Actor } s && (Scene == null || s.Index >= Scene.Actors.Count) ? null : selection;
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    sealed class SnapshotAction(SceneEditorModel m, string name, string path, string beforeScene, string beforeMap, string afterScene, string afterMap,
        SceneItem? beforeSel, SceneItem? afterSel) : IUndoableAction
    {
        public string Name { get; } = name;
        public string AfterScene { get; set; } = afterScene;
        public string AfterMap { get; set; } = afterMap;
        public SceneItem? AfterSelection { get; set; } = afterSel;
        public void Undo() => m.Restore(path, beforeScene, beforeMap, beforeSel);
        public void Redo() => m.Restore(path, AfterScene, AfterMap, AfterSelection);
    }

    // ---- actor operations (Hierarchy, Add Actor / Add to Scene, Actor Context Menu, Edit menu) ----------

    /// <summary>A name not used by another actor: "Slime" -> "Slime (1)", "Slime (2)" ...</summary>
    public string UniqueName(string name)
    {
        if (Scene == null || Scene.Actors.All(a => a.Name != name)) return name;
        var stem = System.Text.RegularExpressions.Regex.Replace(name, @" \(\d+\)$", "");
        for (var i = 1; ; i++)
            if (Scene.Actors.All(a => a.Name != $"{stem} ({i})")) return $"{stem} ({i})";
    }

    /// <summary>Where new actors go: the centre of the shown screen, snapped to the grid.</summary>
    public (int X, int Y) SpawnPoint()
    {
        var (cx, cy) = Camera;
        return (SnapValue(cx + 120), SnapValue(cy + 80));
    }

    public int SnapValue(int v) => Grid.Snap && Grid.Size > 0 ? (int)Math.Round(v / (double)Grid.Size) * Grid.Size : v;

    public int AddActor(ActorData actor, string undoName)
    {
        if (Scene == null) return -1;
        var index = -1;
        Edit(undoName, () =>
        {
            actor.Name = UniqueName(actor.Name);
            Scene.Actors.Add(actor);
            index = Scene.Actors.Count - 1;
            _selection = SceneItem.ForActor(index);
        });
        SelectionChanged?.Invoke();
        return index;
    }

    /// <summary>A new actor of a kind (Add Actor T-M10 / Add to Scene T-M11), with the defaults of that kind.</summary>
    public ActorData NewActor(ActorKind kind, string? sprite = null)
    {
        var (x, y) = SpawnPoint();
        var a = new ActorData { Kind = kind, X = x, Y = y };
        switch (kind)
        {
            case ActorKind.Empty: a.Name = "Actor"; break;
            case ActorKind.Player:
                a.Name = "Player"; a.Tag = "Player"; a.Sprite = sprite ?? "";
                a.Components.Add(new ComponentData { Type = "PlatformerBody" });
                break;
            case ActorKind.Enemy:
                a.Name = sprite is { Length: > 0 } s ? char.ToUpperInvariant(s[0]) + s[1..].Replace('_', ' ') : "Enemy";
                a.Tag = "Enemy"; a.Sprite = sprite ?? ""; a.Priority = 2;
                a.Components.Add(new ComponentData { Type = "PlatformerBody" });
                a.Components.Add(new ComponentData { Type = "Patrol" });
                a.Components.Add(new ComponentData { Type = "DamageOnTouch" });
                break;
            case ActorKind.Pickup: a.Name = "Pickup"; a.Priority = 2; a.Sprite = sprite ?? ""; break;
            case ActorKind.TriggerZone: a.Name = "Trigger Zone"; a.ZoneW = 16; a.ZoneH = 32; break;
            case ActorKind.CameraZone: a.Name = "Camera Zone"; a.ZoneW = 240; a.ZoneH = 160; break;
            case ActorKind.TextLabel: a.Name = "Text Label"; break;
        }
        if (a.Tag.Length > 0 && Project is { } p && !p.Settings.Tags.Contains(a.Tag)) a.Tag = "";
        if (a.Sprite.Length > 0 && Assets?.Sprite(a.Sprite) is { } sheet && Assets.SpriteMeta(a.Sprite).Animations.FirstOrDefault() is { } anim)
            a.Animation = sheet.FrameCount > 0 ? anim.Name : "";
        return a;
    }

    public void RenameActor(int index, string name)
    {
        if (Scene == null || index < 0 || index >= Scene.Actors.Count) return;
        name = name.Trim();
        var old = Scene.Actors[index].Name;
        if (name.Length == 0 || name == old) return;
        Edit("Rename", () =>
        {
            Scene.Actors[index].Name = UniqueName(name);
            // Links and the camera follow the actor by name.
            foreach (var l in Scene.Links.Where(l => l.From == old)) l.From = Scene.Actors[index].Name;
            if (Scene.Camera.Follow == old) Scene.Camera.Follow = Scene.Actors[index].Name;
        });
    }

    public void DeleteActor(int index)
    {
        if (Scene == null || index < 0 || index >= Scene.Actors.Count) return;
        Edit("Delete", () =>
        {
            var name = Scene.Actors[index].Name;
            Scene.Actors.RemoveAt(index);
            Scene.Links.RemoveAll(l => l.From == name);
            _selection = Scene.Actors.Count == 0 ? new SceneItem(SceneItemKind.Scene) : SceneItem.ForActor(Math.Min(index, Scene.Actors.Count - 1));
        });
        SelectionChanged?.Invoke();
    }

    public int DuplicateActor(int index)
    {
        if (Scene == null || index < 0 || index >= Scene.Actors.Count) return -1;
        var copy = Clone(Scene.Actors[index]);
        var at = -1;
        Edit("Duplicate", () =>
        {
            copy.Name = UniqueName(copy.Name);
            Scene.Actors.Insert(index + 1, copy);
            at = index + 1;
            _selection = SceneItem.ForActor(at);
        });
        SelectionChanged?.Invoke();
        return at;
    }

    public static ActorData Clone(ActorData a) => JsonSerializer.Deserialize<ActorData>(JsonSerializer.Serialize(a, GameProject.Json), GameProject.Json)!;

    public void MoveActor(int index, int x, int y, bool merge = false)
    {
        if (Scene == null || index < 0 || index >= Scene.Actors.Count) return;
        var a = Scene.Actors[index];
        if (a.X == x && a.Y == y) return;
        Edit("Move", () => { a.X = x; a.Y = y; }, merge ? "move:" + index : null);
    }

    /// <summary>Changes a field of the selected actor's component (Inspector).</summary>
    public void SetComponentField(int actor, int component, string field, string value)
    {
        if (Scene == null || actor < 0 || actor >= Scene.Actors.Count) return;
        var c = Scene.Actors[actor].Components.ElementAtOrDefault(component);
        if (c == null) return;
        Edit($"Change {field}", () => c.Fields[field] = value, $"field:{actor}:{component}:{field}");
    }

    public void AddComponent(int actor, string type)
    {
        if (Scene == null || actor < 0 || actor >= Scene.Actors.Count) return;
        var a = Scene.Actors[actor];
        if (a.Components.Any(c => c.Type == type)) return;
        Edit("Add Component", () => a.Components.Add(new ComponentData { Type = type }));
    }

    public void RemoveComponent(int actor, int component)
    {
        if (Scene == null || actor < 0 || actor >= Scene.Actors.Count || component < 0 || component >= Scene.Actors[actor].Components.Count) return;
        Edit("Remove Component", () => Scene.Actors[actor].Components.RemoveAt(component));
    }

    public void ResetComponent(int actor, int component)
    {
        if (Scene == null || actor < 0 || actor >= Scene.Actors.Count || component < 0 || component >= Scene.Actors[actor].Components.Count) return;
        Edit("Reset", () => Scene.Actors[actor].Components[component].Fields.Clear());
    }

    /// <summary>Copy Component / Paste Values (Component Menu T-M14).</summary>
    public ComponentData? ComponentClipboard { get; private set; }

    public void CopyComponent(int actor, int component)
    {
        var c = Scene?.Actors.ElementAtOrDefault(actor)?.Components.ElementAtOrDefault(component);
        if (c != null) ComponentClipboard = new ComponentData { Type = c.Type, Fields = new Dictionary<string, string>(c.Fields) };
    }

    public bool PasteComponentValues(int actor, int component)
    {
        var c = Scene?.Actors.ElementAtOrDefault(actor)?.Components.ElementAtOrDefault(component);
        if (c == null || ComponentClipboard is not { } src || src.Type != c.Type) return false;
        Edit("Paste Values", () => c.Fields = new Dictionary<string, string>(src.Fields));
        return true;
    }

    /// <summary>Actor clipboard for Cut / Copy / Paste in the Hierarchy and viewport.</summary>
    public ActorData? ActorClipboard { get; private set; }

    public void CopyActor(int index)
    {
        if (Scene != null && index >= 0 && index < Scene.Actors.Count) ActorClipboard = Clone(Scene.Actors[index]);
    }

    public int PasteActor()
    {
        if (ActorClipboard == null) return -1;
        var a = Clone(ActorClipboard);
        a.X += Grid.Size;
        a.Y += Grid.Size;
        return AddActor(a, "Paste");
    }

    // ---- tile painting (Paint tiles B, Fill, Erase) ------------------------------------------------------

    SceneTool _tool;
    int _stroke;

    /// <summary>The toolbar tool: Select and Move pick and drag actors; Paint tiles, Fill and Erase edit the map.</summary>
    public SceneTool Tool
    {
        get => _tool;
        set { if (_tool == value) return; _tool = value; ViewChanged?.Invoke(); }
    }

    public bool IsTileTool => _tool is SceneTool.Paint or SceneTool.Fill or SceneTool.Erase;

    /// <summary>
    /// The map layer the tile tools paint: the layer selected in the Hierarchy when it has cells, else the collision
    /// layer (the ground), else the first tile layer; -1 when the scene has no paintable layer.
    /// </summary>
    public int PaintLayer
    {
        get
        {
            if (Map is not { } map) return -1;
            bool Paintable(int i) => i >= 0 && i < map.Layers.Count && map.Layers[i].Type is LayerType.Tile or LayerType.CollisionOnly
                                     && map.Layers[i].Cells.Count == map.Width * map.Height;
            if (_selection is { Kind: SceneItemKind.Layer } l && Paintable(l.Index)) return l.Index;
            if (Paintable(map.CollisionLayer) && map.Layers[map.CollisionLayer].Type == LayerType.Tile) return map.CollisionLayer;
            for (var i = 0; i < map.Layers.Count; i++) if (map.Layers[i].Type == LayerType.Tile && Paintable(i)) return i;
            return -1;
        }
    }

    /// <summary>
    /// The brush: the Tilemap Editor's stamp when that editor has this map open (tiles chosen in its tileset panel),
    /// else the first named tile of the tileset (grass_top in Sky Hopper). Cells are metatile ids, row by row.
    /// </summary>
    public (int[] Cells, int Width, int Height) Brush
    {
        get
        {
            if (_s.GetService(typeof(Tilemap.MapLibrary)) is Tilemap.MapLibrary lib && lib.Active is { } ses && MapPath != null
                && string.Equals(ses.Doc.Path, Path.GetFullPath(MapPath), StringComparison.OrdinalIgnoreCase))
                return (ses.Stamp, ses.StampWidth, ses.StampHeight);
            var path = Map is { } m ? Assets?.TilesetPath(m.Tileset) : null;
            var meta = path != null ? Project!.LoadMeta<TilesetMeta>(path) : null;
            var first = meta?.Tiles.Where(t => t.Key > 0 && t.Value.Name.Length > 0).Select(t => t.Key).DefaultIfEmpty(1).Min() ?? 1;
            return ([first], 1, 1);
        }
    }

    /// <summary>The cell of a layer under a scene pixel, with the layer's parallax as the ROM scrolls it.</summary>
    public (int X, int Y) CellAt(int layer, int sceneX, int sceneY)
    {
        var l = Map!.Layers[layer];
        var (camX, camY) = Camera;
        int lx = ((camX * AgbStudio.Build.Pipeline.Cpp.Parallax(l.ParallaxX)) >> 8) + sceneX - camX;
        int ly = ((camY * AgbStudio.Build.Pipeline.Cpp.Parallax(l.ParallaxY)) >> 8) + sceneY - camY;
        return ((int)Math.Floor(lx / 16.0), (int)Math.Floor(ly / 16.0));
    }

    /// <summary>Starts a stroke: the cells painted until the next one are one undo step.</summary>
    public void BeginStroke() => _stroke++;

    int CellIndex(int x, int y) => x < 0 || y < 0 || x >= Map!.Width || y >= Map.Height ? -1 : y * Map.Width + x;

    /// <summary>Paint tiles: the brush with its top-left at the cell under the scene pixel. False when nothing changed.</summary>
    public bool PaintAt(int sceneX, int sceneY) => StrokeAt(sceneX, sceneY, erase: false);

    /// <summary>Erase: empties the cells under the brush.</summary>
    public bool EraseAt(int sceneX, int sceneY) => StrokeAt(sceneX, sceneY, erase: true);

    bool StrokeAt(int sceneX, int sceneY, bool erase)
    {
        var layer = PaintLayer;
        if (layer < 0) return false;
        var (cx, cy) = CellAt(layer, sceneX, sceneY);
        var (cells, w, h) = Brush;
        var cellsOf = Map!.Layers[layer].Cells;
        var changes = new List<(int Index, int Tile)>();
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (CellIndex(cx + x, cy + y) is var i and >= 0 && cellsOf[i] != (erase ? 0 : cells[y * w + x]))
                    changes.Add((i, erase ? 0 : cells[y * w + x]));
        if (changes.Count == 0) return false;
        Edit(erase ? "Erase" : "Paint tiles", () => { foreach (var (i, t) in changes) Map.Layers[layer].Cells[i] = t; }, "stroke:" + _stroke);
        return true;
    }

    /// <summary>Fill: the connected cells equal to the clicked one take the brush pattern (one undo step).</summary>
    public bool FillAt(int sceneX, int sceneY)
    {
        var layer = PaintLayer;
        if (layer < 0) return false;
        var (sx, sy) = CellAt(layer, sceneX, sceneY);
        if (CellIndex(sx, sy) < 0) return false;
        var map = Map!;
        var cells = map.Layers[layer].Cells;
        var (brush, bw, bh) = Brush;
        static int Mod(int a, int m) => ((a % m) + m) % m;
        var target = cells[CellIndex(sx, sy)];
        var seen = new bool[map.Width * map.Height];
        var queue = new Queue<(int X, int Y)>();
        var changes = new List<(int Index, int Tile)>();
        queue.Enqueue((sx, sy));
        seen[CellIndex(sx, sy)] = true;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            changes.Add((CellIndex(x, y), brush[Mod(y - sy, bh) * bw + Mod(x - sx, bw)]));
            foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
            {
                var n = CellIndex(nx, ny);
                if (n < 0 || seen[n]) continue;
                seen[n] = true;
                if (cells[n] == target) queue.Enqueue((nx, ny));
            }
        }
        if (changes.All(c => cells[c.Index] == c.Tile)) return false;
        Edit("Fill", () => { foreach (var (i, t) in changes) map.Layers[layer].Cells[i] = t; });
        return true;
    }

    /// <summary>Changes the open scene or its map (Scene Settings, Video Mode, Inspector header).</summary>
    public void EditScene(string name, Action<SceneFile, MapFile?> change, string? mergeKey = null)
    {
        if (Scene == null) return;
        Edit(name, () => change(Scene, Map), mergeKey);
    }
}
