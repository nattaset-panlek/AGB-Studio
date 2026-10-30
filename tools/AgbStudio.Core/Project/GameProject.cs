using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgbStudio.Core.Project;

/// <summary>Reads and writes the files of a project folder (SkyHopper.agbproj, scenes/, maps/, assets/ ...).</summary>
public sealed class GameProject
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string Root { get; }
    public string ProjectPath { get; }
    public ProjectFile Settings { get; set; }

    public string ScenesDir => Path.Combine(Root, "scenes");
    public string MapsDir => Path.Combine(Root, "maps");
    public string AssetsDir => Path.Combine(Root, "assets");
    public string ScriptsDir => Path.Combine(Root, "scripts");
    public string DataDir => Path.Combine(Root, "data");
    public string TextDir => Path.Combine(Root, "text");
    public string FontsDir => Path.Combine(Root, "fonts");
    public string DialogueDir => Path.Combine(Root, "dialogue");
    public string SfxDir => Path.Combine(Root, "audio", "sfx");
    public string BuildDir => Path.Combine(Root, "build");

    GameProject(string projectPath, ProjectFile settings)
    {
        ProjectPath = Path.GetFullPath(projectPath);
        Root = Path.GetDirectoryName(ProjectPath)!;
        Settings = settings;
    }

    public static GameProject Open(string path)
    {
        if (Directory.Exists(path))
            path = Directory.EnumerateFiles(path, "*.agbproj").FirstOrDefault()
                   ?? throw new FileNotFoundException($"No .agbproj file in {path}.");
        var settings = Load<ProjectFile>(path);
        if (settings.Format > 1) throw new InvalidDataException($"{Path.GetFileName(path)} was made by a newer AGB Studio (format {settings.Format}).");
        return new GameProject(path, settings);
    }

    public static GameProject Create(string folder, string name, ProjectFile? settings = null)
    {
        Directory.CreateDirectory(folder);
        foreach (var d in new[] { "scenes", "maps", "assets/sprites", "assets/tilesets", "assets/images", "assets/audio/music", "assets/audio/sfx",
                     "audio/sfx", "fonts", "scripts", "data", "text", "dialogue" })
            Directory.CreateDirectory(Path.Combine(folder, d));
        settings ??= new ProjectFile();
        settings.Name = name;
        var path = Path.Combine(folder, FileName(name) + ".agbproj");
        Save(path, settings);
        File.WriteAllText(Path.Combine(folder, ".gitignore"), "build/\n");
        return new GameProject(path, settings);
    }

    public static string FileName(string name) => new(name.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());

    public void SaveSettings() => Save(ProjectPath, Settings);

    public static T Load<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? throw new InvalidDataException($"{path} is empty.");

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var text = JsonSerializer.Serialize(value, Json) + "\n";
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        File.WriteAllText(path, text);
    }

    public IEnumerable<string> SceneFiles() => Files(ScenesDir, "*.scene");
    public IEnumerable<string> MapFiles() => Files(MapsDir, "*.map");
    public IEnumerable<string> TableFiles() => Files(DataDir, "*.db");
    public IEnumerable<string> FontFiles() => Files(FontsDir, "*.font");
    public IEnumerable<string> ScriptFiles() => Files(ScriptsDir, "*.cs", true);
    public IEnumerable<string> GraphFiles() => Files(ScriptsDir, "*.graph", true);
    public IEnumerable<string> DialogueFiles() => Files(DialogueDir, "*.dlg");
    public IEnumerable<string> SfxFiles() => Files(SfxDir, "*.sfx");
    public IEnumerable<string> SpriteFiles() => Images(Path.Combine(AssetsDir, "sprites"));
    public IEnumerable<string> TilesetFiles() => Images(Path.Combine(AssetsDir, "tilesets"));
    public IEnumerable<string> ImageFiles() => Images(Path.Combine(AssetsDir, "images"));
    public IEnumerable<string> MusicFiles() => Files(Path.Combine(AssetsDir, "audio", "music"), "*.*").Where(f => f.EndsWith(".mod") || f.EndsWith(".xm"));
    public IEnumerable<string> SoundFiles() => Files(Path.Combine(AssetsDir, "audio", "sfx"), "*.wav");
    public IEnumerable<string> StringTables() => Files(TextDir, "strings.*.json").Where(f => !f.EndsWith("strings.meta.json"));

    public static string AssetName(string path)
    {
        var name = Path.GetFileName(path);
        var dot = name.IndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    public static string MetaPath(string assetPath) => assetPath + ".meta.json";

    public T LoadMeta<T>(string assetPath) where T : new() =>
        File.Exists(MetaPath(assetPath)) ? Load<T>(MetaPath(assetPath)) : new T();

    static IEnumerable<string> Files(string dir, string pattern, bool recursive = false) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

    static IEnumerable<string> Images(string dir) =>
        Files(dir, "*.*", true).Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase));
}
