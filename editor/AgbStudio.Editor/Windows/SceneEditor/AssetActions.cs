using System.Text.RegularExpressions;
using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>The Assets panel categories (T-P01 bottom panel).</summary>
public enum AssetCategory { Sprites, Tilesets, Maps, Fonts, Audio, Scripts, Data }

/// <summary>One asset file: its category, the editor window that opens it and the card text.</summary>
public sealed record AssetItem(string Path, AssetCategory Category, string Window, string Meta, string? Thumb)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Stem => GameProject.AssetName(Path);
}

/// <summary>
/// The project's assets for the Assets panel, Asset Picker and Asset Context Menu (T-M13): listing per category with
/// counts, selection, open in the right editor, rename / duplicate / delete (undoable, references in scenes and maps
/// follow a rename), find usages, reimport and create animation.
/// </summary>
public sealed class AssetActions
{
    readonly SceneEditorModel _m;
    string? _selected;

    AssetActions(SceneEditorModel m)
    {
        _m = m;
        m.Services.Project.Changed += () => { _selected = null; Changed?.Invoke(); };
    }

    public static AssetActions Of(SceneEditorModel m)
    {
        if (m.Services.GetService(typeof(AssetActions)) is AssetActions a) return a;
        a = new AssetActions(m);
        m.Services.Set(a);
        return a;
    }

    EditorServices S => _m.Services;
    GameProject? P => _m.Project;
    public event Action? Changed;

    public static readonly IReadOnlyList<(AssetCategory Category, string Label, string Glyph)> Categories =
    [
        (AssetCategory.Sprites, "Sprites", "image"), (AssetCategory.Tilesets, "Tilesets", "tiles"), (AssetCategory.Maps, "Maps", "map"),
        (AssetCategory.Fonts, "Fonts", "type"), (AssetCategory.Audio, "Audio", "music"), (AssetCategory.Scripts, "Scripts", "code"),
        (AssetCategory.Data, "Data", "db"),
    ];

    // Graphics in the order the game uses them (scenes in story order, their actors and map layers), then unused ones by name.
    IEnumerable<string> ByUse(IEnumerable<string> files)
    {
        var used = new List<string>();
        if (P is { } p)
            foreach (var name in SceneSwitcher.StoryOrder(p))
            {
                var scene = GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, name + ".scene"));
                used.AddRange(scene.Actors.Select(a => a.Sprite));
                if (_m.Assets?.Map(scene) is { } map) used.AddRange(map.Layers.Select(l => l.Image).Prepend(map.Tileset));
            }
        int Rank(string f) => used.IndexOf(GameProject.AssetName(f)) is var i and >= 0 ? i : int.MaxValue;
        return files.OrderBy(Rank).ThenBy(f => f, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> Files(AssetCategory c)
    {
        if (P is not { } p) return [];
        return c switch
        {
            AssetCategory.Sprites => ByUse(p.SpriteFiles()).ToList(),
            AssetCategory.Tilesets => ByUse(p.TilesetFiles()).Concat(ByUse(p.ImageFiles())).ToList(),
            AssetCategory.Maps => p.MapFiles().ToList(),
            AssetCategory.Fonts => p.FontFiles().ToList(),
            AssetCategory.Audio => p.MusicFiles().Concat(p.SoundFiles()).ToList(),
            AssetCategory.Scripts => p.ScriptFiles().Concat(p.GraphFiles()).Concat(p.DialogueFiles()).ToList(),
            AssetCategory.Data => p.TableFiles().ToList(),
            _ => [],
        };
    }

    public int Count(AssetCategory c) => Files(c).Count;

    public IReadOnlyList<AssetItem> List(AssetCategory c) => Files(c).Select(f => Item(f, c)).ToList();

    public AssetItem? Find(string path)
    {
        foreach (var (c, _, _) in Categories)
            if (Files(c).FirstOrDefault(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)) is { } hit) return Item(hit, c);
        return null;
    }

    /// <summary>The window that edits a file (Editor Map #24 / #26 and the Command Palette rules).</summary>
    public static string EditorFor(string path, AssetCategory c)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".map" => "T-P03",
            ".font" or ".ttf" => "T-P09",
            ".db" => "T-P07",
            ".dlg" => "T-P16",
            ".graph" => "T-P17",
            ".cs" => "T-P04",
            ".wav" or ".mod" or ".xm" or ".sfx" => "T-P15",
            _ => c == AssetCategory.Tilesets ? "T-P03" : "T-P08",
        };
    }

    AssetItem Item(string path, AssetCategory c)
    {
        string meta = "";
        string? thumb = null;
        var assets = _m.Assets;
        try
        {
            switch (c)
            {
                case AssetCategory.Sprites:
                    if (assets?.Sprite(GameProject.AssetName(path)) is { } s)
                        meta = s.FrameCount > 1 ? $"{s.SourceWidth}×{s.SourceHeight} · {s.FrameCount} frames" : $"{s.SourceWidth}×{s.SourceHeight}";
                    break;
                case AssetCategory.Tilesets:
                    if (assets?.Tileset(GameProject.AssetName(path)) is { } t && P!.TilesetFiles().Contains(path)) meta = $"{t.Metatiles.Count} tiles · 4bpp";
                    else if (assets?.Image(GameProject.AssetName(path)) is { } img) meta = $"{img.WidthTiles * 8}×{img.HeightTiles * 8} · image";
                    break;
                case AssetCategory.Maps:
                    var map = GameProject.Load<MapFile>(path);
                    meta = $"{map.Width}×{map.Height} · {map.Layers.Count} layers";
                    thumb = "thumb/map";
                    break;
                case AssetCategory.Fonts:
                    var f = GameProject.Load<FontFile>(path);
                    var glyphs = FontBuilder.CodePointsOf(f).Count();
                    meta = f.Variable ? $"{f.Size} px · {glyphs} glyphs" : $"{f.FixedWidth}×{f.LineHeight} · {glyphs} glyphs";
                    thumb = f.CharacterSets.Contains("Thai") ? "thumb/font-thai" : "thumb/font";
                    break;
                case AssetCategory.Audio:
                    var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                    meta = ext == "SFX" ? "DMG sound" : $"{ext} · {(new FileInfo(path).Length + 1023) / 1024} KB";
                    thumb = ext is "WAV" or "SFX" ? "thumb/sound" : "thumb/music";
                    break;
                case AssetCategory.Scripts:
                    var lines = File.ReadLines(path).Count();
                    meta = Path.GetExtension(path).ToLowerInvariant() switch { ".graph" => "Logic Graph", ".dlg" => "Dialogue", _ => $"C# · {lines} lines" };
                    thumb = Path.GetExtension(path).Equals(".graph", StringComparison.OrdinalIgnoreCase) ? "thumb/graph" : "thumb/script";
                    break;
                case AssetCategory.Data:
                    var table = GameProject.Load<TableFile>(path);
                    meta = $"{table.Rows.Count} rows · {table.Fields.Count} fields";
                    thumb = "thumb/table";
                    break;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            meta = "cannot read";
        }
        return new AssetItem(path, c, EditorFor(path, c), meta, thumb);
    }

    // ---- selection and the context menu --------------------------------------------------------------

    public AssetItem? Selected => _selected != null && File.Exists(_selected) ? Find(_selected) : null;

    public void Select(string? path)
    {
        _selected = path;
        S.System.Selection = () => _selected;
        SceneEditTarget.Of(_m).Active = SceneEditTarget.Panel.Assets;
        Changed?.Invoke();
    }

    public Task<object?> Open(AssetItem a) => S.Windows.Open(a.Window, a.Path, source: "AssetCard");

    // ---- undoable file operations -----------------------------------------------------------------------

    sealed class FileSet
    {
        readonly List<(string Path, byte[]? Before, byte[]? After)> _files = [];
        public void Record(string path, byte[]? before, byte[]? after) => _files.Add((path, before, after));
        public void Apply(bool forward)
        {
            var list = forward ? _files : Enumerable.Reverse(_files).ToList();
            foreach (var (path, before, after) in list)
            {
                var content = forward ? after : before;
                if (content == null) { if (File.Exists(path)) File.Delete(path); }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, content);
                }
            }
        }
    }

    void Do(string name, FileSet set)
    {
        S.Undo.Do(new DelegateAction(name, () => { set.Apply(false); AfterFiles(); }, () => { set.Apply(true); AfterFiles(); }));
    }

    void AfterFiles()
    {
        _m.ReloadFromDisk();
        Changed?.Invoke();
    }

    static byte[]? Read(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

    /// <summary>The asset file and its .meta.json move together.</summary>
    static IEnumerable<string> WithMeta(string path) => File.Exists(GameProject.MetaPath(path)) ? [path, GameProject.MetaPath(path)] : [path];

    public static bool ValidName(string stem) => stem.Length > 0 && Regex.IsMatch(stem, @"^[A-Za-z0-9_\-]+$");

    /// <summary>Renames an asset (file name without extension); scenes and maps that use it by name follow.</summary>
    public bool Rename(AssetItem a, string newStem)
    {
        newStem = newStem.Trim();
        if (P is not { } p || !ValidName(newStem) || newStem == a.Stem) return false;
        var ext = a.Name[a.Stem.Length..];
        var target = Path.Combine(Path.GetDirectoryName(a.Path)!, newStem + ext);
        if (File.Exists(target)) return false;
        var set = new FileSet();
        foreach (var f in WithMeta(a.Path))
        {
            var to = Path.Combine(Path.GetDirectoryName(f)!, newStem + Path.GetFileName(f)[a.Stem.Length..]);
            set.Record(f, Read(f), null);
            set.Record(to, null, Read(f));
        }
        foreach (var (file, text) in References(p, a, newStem)) set.Record(file, Read(file), System.Text.Encoding.UTF8.GetBytes(text));
        Do("Rename", set);
        _selected = target;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Scene and map files rewritten for a renamed sprite, tileset, image or music file.</summary>
    static IEnumerable<(string File, string Text)> References(GameProject p, AssetItem a, string newStem)
    {
        var old = a.Stem;
        foreach (var f in p.SceneFiles())
        {
            var scene = GameProject.Load<SceneFile>(f);
            var changed = false;
            foreach (var actor in scene.Actors.Where(x => x.Sprite == old && a.Category == AssetCategory.Sprites)) { actor.Sprite = newStem; changed = true; }
            if (a.Category == AssetCategory.Audio && scene.Music.Track == old) { scene.Music.Track = newStem; changed = true; }
            if (a.Category == AssetCategory.Maps && Path.GetFileNameWithoutExtension(scene.Map) == old) { scene.Map = newStem; changed = true; }
            if (changed) yield return (f, System.Text.Json.JsonSerializer.Serialize(scene, GameProject.Json) + "\n");
        }
        if (a.Category != AssetCategory.Tilesets) yield break;
        foreach (var f in p.MapFiles())
        {
            var map = GameProject.Load<MapFile>(f);
            var changed = false;
            if (map.Tileset == old) { map.Tileset = newStem; changed = true; }
            foreach (var l in map.Layers.Where(l => l.Image == old)) { l.Image = newStem; changed = true; }
            if (changed) yield return (f, System.Text.Json.JsonSerializer.Serialize(map, GameProject.Json) + "\n");
        }
    }

    public AssetItem? Duplicate(AssetItem? a = null)
    {
        a ??= Selected;
        if (a == null) return null;
        var dir = Path.GetDirectoryName(a.Path)!;
        var ext = a.Name[a.Stem.Length..];
        var stem = a.Stem + "_copy";
        for (var i = 2; File.Exists(Path.Combine(dir, stem + ext)); i++) stem = $"{a.Stem}_copy{i}";
        var set = new FileSet();
        foreach (var f in WithMeta(a.Path)) set.Record(Path.Combine(dir, stem + Path.GetFileName(f)[a.Stem.Length..]), null, Read(f));
        Do("Duplicate", set);
        _selected = Path.Combine(dir, stem + ext);
        Changed?.Invoke();
        return Find(_selected);
    }

    public void Delete(AssetItem? a = null)
    {
        a ??= Selected;
        if (a == null) return;
        var set = new FileSet();
        foreach (var f in WithMeta(a.Path)) set.Record(f, Read(f), null);
        Do("Delete", set);
        _selected = null;
        Changed?.Invoke();
    }

    public AssetItem? Clipboard { get; private set; }
    public void Copy() => Clipboard = Selected;
    public AssetItem? Paste() => Clipboard != null && File.Exists(Clipboard.Path) ? Duplicate(Clipboard) : null;

    /// <summary>Where an asset is used: scene actors (sprites, behaviour scripts), maps (tilesets, images), scene music, fonts.</summary>
    public List<string> Usages(AssetItem a)
    {
        var list = new List<string>();
        if (P is not { } p) return list;
        foreach (var f in p.SceneFiles())
        {
            var scene = GameProject.Load<SceneFile>(f);
            var name = GameProject.AssetName(f);
            foreach (var actor in scene.Actors)
            {
                if (a.Category == AssetCategory.Sprites && actor.Sprite == a.Stem) list.Add($"{name} › {actor.Name}");
                if (a.Category == AssetCategory.Scripts && actor.Components.Any(c => c.Type == a.Stem)) list.Add($"{name} › {actor.Name}");
            }
            if (a.Category == AssetCategory.Audio && scene.Music.Track == a.Stem) list.Add($"{name} › music");
            if (a.Category == AssetCategory.Maps && Path.GetFileNameWithoutExtension(scene.Map) == a.Stem) list.Add($"{name} › map");
        }
        if (a.Category == AssetCategory.Tilesets)
            foreach (var f in p.MapFiles())
            {
                var map = GameProject.Load<MapFile>(f);
                if (map.Tileset == a.Stem) list.Add($"{map.Name} › tileset");
                foreach (var l in map.Layers.Where(l => l.Image == a.Stem)) list.Add($"{map.Name} › {l.Name}");
            }
        if (a.Category == AssetCategory.Sprites)
            foreach (var other in p.SpriteFiles().Where(f => f != a.Path))
                if (p.LoadMeta<SpriteMeta>(other).Palette == a.Stem) list.Add($"{Path.GetFileName(other)} › palette");
        if (a.Category == AssetCategory.Fonts)
        {
            var font = GameProject.Load<FontFile>(a.Path);
            var fname = font.Name.Length > 0 ? font.Name : a.Stem;
            if (p.Settings.General.DefaultFont == fname) list.Add("Project Settings › default font");
            foreach (var l in p.Settings.Localization.Languages.Where(l => l.Font == fname)) list.Add($"Localization › {l.Name}");
        }
        return list;
    }

    /// <summary>Find Usages: lists them in the Console and says how many in a toast.</summary>
    public List<string> FindUsages(AssetItem? a = null)
    {
        a ??= Selected;
        if (a == null) return [];
        var u = Usages(a);
        var log = ConsoleLog.Of(S);
        log.Info(u.Count == 0 ? $"{a.Name} is not used" : $"{a.Name} is used {u.Count} {(u.Count == 1 ? "time" : "times")}");
        foreach (var x in u) log.Info("  " + x);
        S.Toasts.Show(u.Count == 0 ? $"{a.Name} is not used" : $"{a.Name}: {u.Count} {(u.Count == 1 ? "usage" : "usages")} in the Console", ToastStatus.Success);
        return u;
    }

    /// <summary>Reimport: converts the asset again as the build does and reports its import problems in the Console.</summary>
    public int Reimport(AssetItem? a = null)
    {
        a ??= Selected;
        if (a == null || _m.Assets is not { } assets) return 0;
        var log = ConsoleLog.Of(S);
        var problems = a.Category switch
        {
            AssetCategory.Sprites => assets.Sprite(a.Stem)?.Problems.Select(p => p.Message).ToList(),
            AssetCategory.Tilesets => (assets.Tileset(a.Stem)?.Problems.Select(p => p.Message) ?? []).ToList(),
            _ => null,
        } ?? [];
        foreach (var p in problems) log.Add(ConsoleLevel.Warning, $"{a.Name}: {p}", a.Path);
        log.Add(problems.Count == 0 ? ConsoleLevel.Success : ConsoleLevel.Warning, $"Reimported {a.Name}" + (problems.Count == 0 ? "" : $" · {problems.Count} warnings"));
        _m.ReloadFromDisk();
        Changed?.Invoke();
        return problems.Count;
    }

    /// <summary>Create Animation: adds an animation to a sprite's .meta.json (all frames, 6 ticks) and opens the Sprite Editor.</summary>
    public string? CreateAnimation(AssetItem? a = null)
    {
        a ??= Selected;
        if (a is not { Category: AssetCategory.Sprites } || P is not { } p) return null;
        var metaPath = GameProject.MetaPath(a.Path);
        var meta = p.LoadMeta<SpriteMeta>(a.Path);
        var name = "anim";
        for (var i = 2; meta.Animations.Any(x => x.Name == name); i++) name = "anim" + i;
        var frames = _m.Assets?.Sprite(a.Stem)?.FrameCount ?? 1;
        meta.Animations.Add(new AnimationData { Name = name, Frames = Enumerable.Range(0, Math.Max(1, frames)).ToList(), Ticks = 6 });
        var set = new FileSet();
        set.Record(metaPath, Read(metaPath), System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(meta, GameProject.Json) + "\n"));
        Do("Create Animation", set);
        _ = S.Windows.Open("T-P08", a.Path, source: "CreateAnimation");
        return name;
    }
}
