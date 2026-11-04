using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>An undoable change of several project files (bytes before and after; null = the file does not exist).</summary>
public sealed class FileChanges
{
    readonly Dictionary<string, byte[]?> _before = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, byte[]?> _after = new(StringComparer.OrdinalIgnoreCase);

    static byte[]? Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

    /// <summary>Remembers a file's current content before it is changed.</summary>
    public void Track(string path)
    {
        path = Path.GetFullPath(path);
        if (!_before.ContainsKey(path)) _before[path] = Read(path);
    }

    public void Write<T>(string path, T value)
    {
        Track(path);
        GameProject.Save(path, value);
    }

    public void Delete(string path)
    {
        Track(path);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Takes the "after" state of every tracked file (call once the change is on disk).</summary>
    public void Seal()
    {
        foreach (var p in _before.Keys) _after[p] = Read(p);
    }

    public IReadOnlyCollection<string> Files => _before.Keys;

    void Apply(Dictionary<string, byte[]?> state)
    {
        foreach (var (path, bytes) in state)
        {
            if (bytes == null) { if (File.Exists(path)) File.Delete(path); }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
            }
        }
    }

    /// <summary>Records the change in Undo; <paramref name="reload"/> runs after undo and redo.</summary>
    public void Push(UndoService undo, string name, Action reload)
    {
        Seal();
        undo.Push(new DelegateAction(name, () => { Apply(_before); reload(); }, () => { Apply(_after); reload(); }));
    }
}

/// <summary>New Scene (T-D02) options.</summary>
public sealed record NewSceneOptions(string Name, string Template, int Width, int Height, int VideoMode, bool CopyBackgrounds, bool FollowPlayer);

/// <summary>Scene Settings (T-D03) values.</summary>
public sealed record SceneSettingsValues(string Name, int Width, int Height, bool StartScene, string Follow, int DeadZoneW, int DeadZoneH, bool ClampToMap,
    IReadOnlyList<double> Parallax, string Music, string OnEnter, string OnExit);

/// <summary>Creating scenes (New Scene) and applying Scene Settings: scene, map, project and linked scenes, as one undo step.</summary>
public static class SceneFiles
{
    public static readonly IReadOnlyList<string> Templates = ["Platformer level", "Top-down room", "Empty scene"];

    public static bool ValidName(string name) => name.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*$");

    public static bool Exists(GameProject p, string name) => p.SceneFiles().Any(f => string.Equals(GameProject.AssetName(f), name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The next free name in a numbered series: Forest_01 -> Forest_02 (or _03 when _02 exists).</summary>
    public static string SuggestName(GameProject p, string? current)
    {
        var m = System.Text.RegularExpressions.Regex.Match(current ?? "", @"^(.*?)(\d+)$");
        var stem = m.Success ? m.Groups[1].Value : current is { Length: > 0 } c ? c + "_" : "Scene_";
        var digits = m.Success ? m.Groups[2].Value.Length : 2;
        var start = m.Success ? int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) + 1 : 1;
        for (var i = start; ; i++)
        {
            var name = stem + i.ToString("D" + digits, System.Globalization.CultureInfo.InvariantCulture);
            if (!Exists(p, name)) return name;
        }
    }

    // A tile of the tileset with a property: the ground (tag "ground", else a solid tile), a one-way platform, a wall.
    static int TileWith(GameProject p, string tileset, Func<TileProperties, bool> pick, int fallback)
    {
        var path = p.TilesetFiles().FirstOrDefault(f => GameProject.AssetName(f) == tileset);
        if (path == null) return fallback;
        var meta = p.LoadMeta<TilesetMeta>(path);
        return meta.Tiles.Where(kv => pick(kv.Value)).Select(kv => kv.Key).OrderBy(k => k).FirstOrDefault(fallback);
    }

    /// <summary>Writes scenes/&lt;Name&gt;.scene and maps/&lt;Name&gt;.map from the options (template ground, copied
    /// backgrounds, a following camera with the current scene's player) and records it for Undo.</summary>
    public static string Create(SceneEditorModel m, NewSceneOptions o)
    {
        var p = m.Project ?? throw new InvalidOperationException("No project is open.");
        var from = m.Scene;
        var fromMap = m.Map;
        var tileset = fromMap?.Tileset is { Length: > 0 } t ? t : p.TilesetFiles().Select(GameProject.AssetName).FirstOrDefault() ?? "";
        var map = new MapFile { Name = o.Name, Tileset = tileset, Width = o.Width, Height = o.Height };
        var used = new HashSet<int>();
        if (o.CopyBackgrounds && fromMap != null)
            foreach (var l in fromMap.Layers.Where(l => l.Type == LayerType.Image))
            {
                map.Layers.Add(new MapLayerData { Name = l.Name, Bg = l.Bg, Type = LayerType.Image, Image = l.Image, Priority = l.Priority, ParallaxX = l.ParallaxX, ParallaxY = l.ParallaxY, Wrap = l.Wrap });
                used.Add(l.Bg);
            }
        var cells = new int[o.Width * o.Height];
        var ground = TileWith(p, tileset, t => t.Tag == "ground", TileWith(p, tileset, t => t.Collision == CollisionType.Solid, 1));
        var wall = TileWith(p, tileset, t => t.Collision == CollisionType.Solid && t.Tag != "ground", ground);
        var platform = TileWith(p, tileset, t => t.Collision == CollisionType.OneWay, ground);
        switch (o.Template)
        {
            case "Platformer level":
                for (var x = 0; x < o.Width; x++) cells[(o.Height - 1) * o.Width + x] = ground;
                if (o.Height > 4)
                    for (var x = 5; x <= 8 && x < o.Width; x++) cells[(o.Height - 4) * o.Width + x] = platform;
                break;
            case "Top-down room":
                for (var x = 0; x < o.Width; x++) { cells[x] = wall; cells[(o.Height - 1) * o.Width + x] = wall; }
                for (var y = 0; y < o.Height; y++) { cells[y * o.Width] = wall; cells[y * o.Width + o.Width - 1] = wall; }
                break;
        }
        // the ground goes on the first BG after the copied backgrounds (BG2 after Sky and Hills)
        var bg = Enumerable.Range(0, 4).FirstOrDefault(b => !used.Contains(b) && (used.Count == 0 || b > used.Max()), Enumerable.Range(0, 4).First(b => !used.Contains(b)));
        map.Layers.Add(new MapLayerData { Name = "Ground", Bg = bg, Type = LayerType.Tile, Priority = 1, Cells = cells.ToList() });
        map.CollisionLayer = map.Layers.Count - 1;

        var scene = new SceneFile
        {
            Name = o.Name, Width = o.Width, Height = o.Height, VideoMode = o.VideoMode, Map = o.Name, Template = o.Template,
            // the copied backgrounds keep their backdrop colour; otherwise the scene file's default
            Backdrop = o.CopyBackgrounds && from != null ? from.Backdrop : new SceneFile().Backdrop,
            Camera = new CameraSettings { Follow = "" },
        };
        if (from != null && p.SceneFiles().Select(f => GameProject.Load<SceneFile>(f)).ToList() is { } all)
        {
            scene.MapX = all.Max(s => s.MapX) + 220;
            scene.MapY = from.MapY;
            scene.Music = new MusicSettings { Track = from.Music.Track, Volume = from.Music.Volume, FadeOnSceneChange = from.Music.FadeOnSceneChange };
        }
        if (o.FollowPlayer)
        {
            var player = from?.Actors.FirstOrDefault(a => a.Kind == ActorKind.Player) is { } fp ? SceneEditorModel.Clone(fp) : new ActorData { Name = "Player", Kind = ActorKind.Player, Tag = "Player" };
            player.X = 40;
            player.Y = o.Template == "Platformer level" ? (o.Height - 1) * 16 : o.Height * 8;
            player.Group = "";
            scene.Actors.Add(player);
            scene.Camera.Follow = player.Name;
        }
        var changes = new FileChanges();
        var scenePath = Path.Combine(p.ScenesDir, o.Name + ".scene");
        changes.Write(Path.Combine(p.MapsDir, o.Name + ".map"), map);
        changes.Write(scenePath, scene);
        changes.Push(m.Services.Undo, "New Scene", () => m.ReloadProjectScenes(scenePath));
        m.OpenScene(o.Name);
        return scenePath;
    }

    /// <summary>Applies Scene Settings to the open scene: rename (links, start / last / title scene follow), size (map
    /// cells kept from the top-left), start scene, camera, parallax, music and events.</summary>
    public static void Apply(SceneEditorModel m, SceneSettingsValues v)
    {
        if (m.Project is not { } p || m.Scene is not { } scene || m.ScenePath is not { } oldPath) return;
        var changes = new FileChanges();
        var oldName = m.SceneName;
        var rename = v.Name != oldName && ValidName(v.Name) && !Exists(p, v.Name);
        var name = rename ? v.Name : oldName;
        var newPath = rename ? Path.Combine(p.ScenesDir, name + ".scene") : oldPath;

        scene.Name = name;
        scene.Camera.Follow = v.Follow;
        scene.Camera.DeadZoneW = Math.Clamp(v.DeadZoneW, 0, 240);
        scene.Camera.DeadZoneH = Math.Clamp(v.DeadZoneH, 0, 160);
        scene.Camera.ClampToMap = v.ClampToMap;
        scene.Music.Track = v.Music;
        scene.Events.OnEnter = v.OnEnter;
        scene.Events.OnExit = v.OnExit;
        if (m.Map is { } map && m.MapPath is { } mapPath)
        {
            var layers = map.Layers.Where(l => l.Type != LayerType.CollisionOnly).ToList();
            for (var i = 0; i < layers.Count && i < v.Parallax.Count; i++) layers[i].ParallaxX = layers[i].ParallaxY = Math.Round(v.Parallax[i], 2);
            if (v.Width != map.Width || v.Height != map.Height) Resize(map, Math.Clamp(v.Width, 1, 256), Math.Clamp(v.Height, 1, 256));
            changes.Write(mapPath, map);
        }
        scene.Width = Math.Clamp(v.Width, 1, 256);
        scene.Height = Math.Clamp(v.Height, 1, 256);
        if (rename) changes.Delete(oldPath);
        changes.Write(newPath, scene);

        var settings = p.Settings;
        var projectChanged = false;
        if (rename)
        {
            foreach (var f in p.SceneFiles().Where(f => !string.Equals(Path.GetFullPath(f), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase)))
            {
                var other = GameProject.Load<SceneFile>(f);
                var changed = false;
                foreach (var l in other.Links.Where(l => l.To == oldName)) { l.To = name; changed = true; }
                foreach (var it in other.Menu?.Items ?? []) if (it.Scene == oldName) { it.Scene = name; changed = true; }
                if (changed) changes.Write(f, other);
            }
            UiPageLinks.RenameScene(p, oldName, name);
            if (settings.LastScene == oldName) { settings.LastScene = name; projectChanged = true; }
            if (settings.Ui.TitleScene == oldName) { settings.Ui.TitleScene = name; projectChanged = true; }
        }
        var isStart = settings.General.StartScene == oldName;
        if (v.StartScene && (!isStart || rename)) { settings.General.StartScene = name; projectChanged = true; }
        else if (!v.StartScene && isStart) { settings.General.StartScene = ""; projectChanged = true; }
        if (projectChanged) changes.Write(p.ProjectPath, settings);
        changes.Push(m.Services.Undo, "Scene Settings", () => m.ReloadProjectScenes(File.Exists(newPath) ? newPath : oldPath));
        m.ReloadProjectScenes(newPath);
    }

    static void Resize(MapFile map, int w, int h)
    {
        foreach (var l in map.Layers.Where(l => l.Type != LayerType.Image && l.Cells.Count == map.Width * map.Height))
        {
            var cells = new int[w * h];
            for (var y = 0; y < Math.Min(h, map.Height); y++)
                for (var x = 0; x < Math.Min(w, map.Width); x++) cells[y * w + x] = l.Cells[y * map.Width + x];
            l.Cells = cells.ToList();
        }
        map.Width = w;
        map.Height = h;
    }

    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, GameProject.Json), GameProject.Json)!;
}
