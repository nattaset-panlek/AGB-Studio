using System.Globalization;
using System.Text.RegularExpressions;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.WorldMap;

/// <summary>A link of a scene by position in its Links list.</summary>
public sealed record LinkRef(string Scene, int Index);

/// <summary>A scene of the World Map: its file, data and map (for the background parallax).</summary>
public sealed class MapScene
{
    public required string Path { get; init; }
    public required SceneFile Data { get; set; }
    public MapFile? Map { get; set; }
    public string Name => Data.Name.Length > 0 ? Data.Name : GameProject.AssetName(Path);
}

/// <summary>
/// The World Map model (T-P13, P11.50-P11.51): the project's scenes at their MapX / MapY, their links (LinkData) and the
/// start scene. Every change writes the scene / map / project files at once and is one undo step (the files are
/// snapshotted before and after).
/// </summary>
public sealed partial class WorldMapModel
{
    readonly EditorServices _s;

    public WorldMapModel(EditorServices s)
    {
        _s = s;
        Reload();
    }

    public List<MapScene> Scenes { get; } = [];
    public event Action? Changed;
    GameProject? Project => _s.Project.Current;

    public void Reload()
    {
        Scenes.Clear();
        if (Project is not { } p) { Changed?.Invoke(); return; }
        foreach (var f in p.SceneFiles())
        {
            try
            {
                var data = GameProject.Load<SceneFile>(f);
                var scene = new MapScene { Path = f, Data = data };
                var mapPath = System.IO.Path.Combine(p.MapsDir, (data.Map.Length > 0 ? data.Map : scene.Name) + ".map");
                if (File.Exists(mapPath)) scene.Map = GameProject.Load<MapFile>(mapPath);
                Scenes.Add(scene);
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { }
        }
        // the order of the Figma Scenes list: the start scene first, then by position on the map
        var start = StartScene;
        Scenes.Sort((a, b) => a.Name == start ? -1 : b.Name == start ? 1 : (a.Data.MapX, a.Data.MapY).CompareTo((b.Data.MapX, b.Data.MapY)));
        Changed?.Invoke();
    }

    public MapScene? Find(string name) => Scenes.FirstOrDefault(s => s.Name == name);

    public string StartScene => Project?.Settings.General.StartScene ?? "";
    public int LinkCount => Scenes.Sum(s => s.Data.Links.Count);

    /// <summary>"5 scenes · 6 links" (toolbar chip).</summary>
    public string Summary => $"{Scenes.Count} scene{(Scenes.Count == 1 ? "" : "s")} · {LinkCount} link{(LinkCount == 1 ? "" : "s")}";

    // ---- presentation ------------------------------------------------------------------------------------------

    /// <summary>Scene Card art by the scene's role: title menus at dusk, caves, boss scenes in the snow, levels by day.</summary>
    public static string Art(MapScene s)
    {
        if (s.Data.Menu != null) return "art/scene/dusk";
        if ((s.Map?.Tileset ?? "").Contains("cave", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Cave", StringComparison.OrdinalIgnoreCase)) return "art/scene/cave";
        if (IsBoss(s)) return "art/scene/snow";
        return "art/scene/day";
    }

    static bool IsBoss(MapScene s) => s.Data.Actors.Any(a => a.Kind == ActorKind.Enemy && a.Components.Any(c => c.Type == "Health"));

    /// <summary>Card / list meta: "menu", "boss" or the size in tiles ("30×20").</summary>
    public static string Meta(MapScene s) => s.Data.Menu != null ? "menu" : IsBoss(s) ? "boss" : $"{s.Data.Width}×{s.Data.Height}";

    /// <summary>Inspector meta: the template the scene started from ("Platformer level" -> "Platformer").</summary>
    public static string TemplateMeta(MapScene s) => s.Data.Template.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    public static string KindText(LinkKind k) => k switch { LinkKind.OnTouch => "on touch", LinkKind.Door => "door", _ => "respawn" };

    [GeneratedRegex(@"^\s*Game\.(?<f>\w+)\s*(>=|≥|==|>)\s*(?<n>\d+)\s*$")]
    private static partial Regex CountCondition();

    /// <summary>Name of a link on its chip and in lists: the trigger actor, or the condition in words ("3 stars").</summary>
    public static string LinkLabel(LinkData l)
    {
        if (l.From.Trim().Length > 0) return l.From;
        var m = CountCondition().Match(l.OnlyIf);
        if (m.Success) return $"{m.Groups["n"].Value} {m.Groups["f"].Value}";
        return l.OnlyIf.Trim().Length > 0 ? l.OnlyIf.Trim() : "Link";
    }

    /// <summary>Chip icon: trigger flag (on touch), door, respawn, or a star for condition-only links.</summary>
    public static string LinkGlyph(LinkData l) => l.From.Trim().Length == 0 ? "star" : l.Kind switch { LinkKind.OnTouch => "flag", LinkKind.Door => "link", _ => "refresh" };

    /// <summary>"Only if" as shown (≥ ≤ ≠) and as stored (C#).</summary>
    public static string ShowCondition(string c) => c.Replace(">=", "≥").Replace("<=", "≤").Replace("!=", "≠");
    public static string StoreCondition(string c) => c.Replace("≥", ">=").Replace("≤", "<=").Replace("≠", "!=").Trim();

    /// <summary>Lines between scenes: one per pair of scenes, labelled with the first link; menu items that load a scene are dashed.</summary>
    public IReadOnlyList<(MapScene From, MapScene To, string Label, string Glyph, bool Dashed, LinkRef? Link)> Connections()
    {
        var list = new List<(MapScene, MapScene, string, string, bool, LinkRef?)>();
        var pairs = new HashSet<(string, string)>();
        foreach (var s in Scenes)
        {
            foreach (var item in s.Data.Menu?.Items ?? [])
                if (item.Command == MenuCommand.LoadScene && Find(item.Scene) is { } target && target != s && pairs.Add(Key(s.Name, target.Name)))
                    list.Add((s, target, MenuLabel(item.Text), "play", true, null));
            for (var i = 0; i < s.Data.Links.Count; i++)
            {
                var l = s.Data.Links[i];
                if (Find(l.To) is not { } to || to == s || !pairs.Add(Key(s.Name, to.Name))) continue;
                list.Add((s, to, LinkLabel(l), LinkGlyph(l), false, new LinkRef(s.Name, i)));
            }
        }
        return list;
    }

    static (string, string) Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);

    /// <summary>"ui.menu.start" -> "Start".</summary>
    static string MenuLabel(string key)
    {
        var last = key.Split('.').Last();
        return last.Length == 0 ? key : char.ToUpperInvariant(last[0]) + last[1..];
    }

    // ---- editing -----------------------------------------------------------------------------------------------

    Dictionary<string, string?> Snapshot()
    {
        var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (Project is not { } p) return d;
        foreach (var f in Directory.EnumerateFiles(p.ScenesDir, "*.scene").Concat(Directory.EnumerateFiles(p.MapsDir, "*.map")).Append(p.ProjectPath))
            d[f] = File.ReadAllText(f);
        return d;
    }

    void Write(Dictionary<string, string?> files, Dictionary<string, string?> other)
    {
        foreach (var f in other.Keys.Where(k => !files.ContainsKey(k))) if (File.Exists(f)) File.Delete(f);
        foreach (var (f, text) in files) if (text != null && (!File.Exists(f) || File.ReadAllText(f) != text)) File.WriteAllText(f, text);
        if (Project is { } p) p.Settings = GameProject.Load<AgbStudio.Core.Project.ProjectFile>(p.ProjectPath);
        Reload();
    }

    /// <summary>Runs a change on the files as one undo step (the change saves what it edits).</summary>
    public void Edit(string name, Action change)
    {
        if (Project is null) return;
        var before = Snapshot();
        change();
        var after = Snapshot();
        if (before.Count == after.Count && before.All(kv => after.TryGetValue(kv.Key, out var v) && v == kv.Value)) { Reload(); return; }
        _s.Undo.Push(new DelegateAction(name, () => Write(before, after), () => Write(after, before)));
        Reload();
    }

    public void Save(MapScene s) => GameProject.Save(s.Path, s.Data);

    public void Move(MapScene s, int x, int y) => Edit("Move scene", () => { s.Data.MapX = x; s.Data.MapY = y; Save(s); });

    public void SetStart(string scene) => Edit("Set as start scene", () =>
    {
        _s.Project.Current!.Settings.General.StartScene = scene;
        _s.ProjectSettings.Save();
    });

    /// <summary>Renames a scene: the file, links that point to it, title menus and the start scene.</summary>
    public bool Rename(MapScene s, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || newName == s.Name || Find(newName) != null || newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return false;
        var old = s.Name;
        Edit("Rename scene", () =>
        {
            foreach (var other in Scenes)
            {
                var changed = false;
                foreach (var l in other.Data.Links.Where(l => l.To == old)) { l.To = newName; changed = true; }
                foreach (var m in other.Data.Menu?.Items ?? []) if (m.Scene == old) { m.Scene = newName; changed = true; }
                if (changed && other != s) Save(other);
            }
            s.Data.Name = newName;
            var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.Path)!, newName + ".scene");
            GameProject.Save(path, s.Data);
            File.Delete(s.Path);
            var settings = _s.Project.Current!.Settings;
            if (settings.General.StartScene == old) { settings.General.StartScene = newName; _s.ProjectSettings.Save(); }
        });
        return true;
    }

    /// <summary>Duplicates a scene next to it ("Forest_01 (2)") with its own copy of the map.</summary>
    public string Duplicate(MapScene s)
    {
        var name = s.Name + "_copy";
        for (var i = 2; Find(name) != null; i++) name = $"{s.Name}_copy{i}";
        Edit("Duplicate scene", () =>
        {
            var copy = GameProject.Load<SceneFile>(s.Path);
            copy.Name = name;
            copy.MapX = s.Data.MapX + 40;
            copy.MapY = s.Data.MapY + 40;
            if (Project is { } p && s.Map is { } map)
            {
                var mapCopy = GameProject.Load<MapFile>(System.IO.Path.Combine(p.MapsDir, (s.Data.Map.Length > 0 ? s.Data.Map : s.Name) + ".map"));
                mapCopy.Name = name;
                GameProject.Save(System.IO.Path.Combine(p.MapsDir, name + ".map"), mapCopy);
                copy.Map = name;
                _ = map;
            }
            GameProject.Save(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.Path)!, name + ".scene"), copy);
        });
        return name;
    }

    /// <summary>Deletes a scene file (its map stays; links to it are removed). Undo brings everything back.</summary>
    public void Delete(MapScene s) => Edit("Delete scene", () =>
    {
        foreach (var other in Scenes.Where(o => o != s))
            if (other.Data.Links.RemoveAll(l => l.To == s.Name) > 0) Save(other);
        File.Delete(s.Path);
    });

    public LinkData? Link(LinkRef r) => Find(r.Scene) is { } s && r.Index >= 0 && r.Index < s.Data.Links.Count ? s.Data.Links[r.Index] : null;

    /// <summary>Changes a link (Link Settings) and saves its scene.</summary>
    public void EditLink(LinkRef r, string name, Action<LinkData> change)
    {
        if (Find(r.Scene) is not { } s || Link(r) is not { } l) return;
        Edit(name, () => { change(l); Save(s); });
    }

    /// <summary>Adds a link from one scene to another (link tool) and returns it.</summary>
    public LinkRef AddLink(MapScene from, MapScene to)
    {
        var index = from.Data.Links.Count;
        Edit("Add link", () =>
        {
            var trigger = from.Data.Actors.FirstOrDefault(a => a.Kind == ActorKind.TriggerZone && from.Data.Links.All(l => l.From != a.Name));
            from.Data.Links.Add(new LinkData { From = trigger?.Name ?? "", To = to.Name, Kind = LinkKind.OnTouch });
            Save(from);
        });
        return new LinkRef(from.Name, index);
    }

    // ---- inspector fields ------------------------------------------------------------------------------------

    public static string SizeText(SceneFile s) => $"{s.Width} × {s.Height} tiles";

    public static (int W, int H)? ParseSize(string text)
    {
        var m = Regex.Match(text, @"^\s*(\d+)\s*[x×*]\s*(\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out var w) && int.TryParse(m.Groups[2].Value, out var h) && w is >= 15 and <= 256 && h is >= 10 and <= 256 ? (w, h) : null;
    }

    public static readonly string[] VideoModes = ["Mode 0 · 4 BG", "Mode 1 · 3 BG", "Mode 2 · 2 BG", "Mode 3 · bitmap"];

    public static string Num(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Music files of the project with their extension ("bgm_forest.xm").</summary>
    public IReadOnlyList<string> MusicTracks() => Project?.MusicFiles().Select(System.IO.Path.GetFileName).OfType<string>().ToList() ?? [];

    public string TrackFile(string track) => MusicTracks().FirstOrDefault(t => System.IO.Path.GetFileNameWithoutExtension(t) == track) ?? track;

    /// <summary>Actors of a scene for Link Settings › From ("Goal Flag · Forest_01").</summary>
    public IReadOnlyList<string> Triggers(MapScene s) => s.Data.Actors.Select(a => $"{a.Name} · {s.Name}").ToList();

    /// <summary>Spawn points of a target scene: its edges and its actors.</summary>
    public IReadOnlyList<string> SpawnPoints(string scene) =>
        ["Start (left edge)", "End (right edge)", .. Find(scene)?.Data.Actors.Where(a => a.Kind != ActorKind.Player).Select(a => a.Name) ?? []];

    public void ApplySpawn(LinkData l, string spawn)
    {
        l.SpawnAt = spawn;
        if (Find(l.To) is not { } target) return;
        if (spawn.StartsWith("Start", StringComparison.Ordinal)) l.SpawnX = 24;
        else if (spawn.StartsWith("End", StringComparison.Ordinal)) l.SpawnX = target.Data.Width * 16 - 24;
        else if (target.Data.Actors.FirstOrDefault(a => a.Name == spawn) is { } a) { l.SpawnX = a.X; l.SpawnY = a.Y; }
    }
}
