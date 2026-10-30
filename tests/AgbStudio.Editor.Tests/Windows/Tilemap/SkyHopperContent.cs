using AgbStudio.Build.Content;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Support;

namespace AgbStudio.Editor.Tests.Windows.Tilemap;

/// <summary>
/// The real "Sky Hopper" fixture project (AgbStudio.Build.Content.SkyHopper: maps, tilesets, scenes, fonts, strings),
/// generated once per test run and copied into each <see cref="Fixture"/>, then opened with the documents of a Figma frame.
/// </summary>
public static class SkyHopperContent
{
    static readonly Lazy<string> Template = new(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "agb-skyhopper-" + Environment.ProcessId);
        SkyHopper.Create(dir);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
        return dir;
    });

    /// <summary>Copies and opens the project; <paramref name="docs"/> are (name, glyph, path relative to the root or null).</summary>
    public static string Open(Fixture fx, params (string Name, string Glyph, string? Path)[] docs)
    {
        var root = System.IO.Path.Combine(fx.Folder, "SkyHopper");
        Copy(Template.Value, root);
        fx.Services.Project.Open(root);
        foreach (var (name, glyph, path) in docs)
            fx.Services.Documents.Add(new EditorDocument { Name = name, Glyph = glyph, Path = path is null ? null : System.IO.Path.Combine(root, path) }, activate: fx.Services.Documents.Open.Count == 0);
        var s = fx.Services.Status;
        s.Build = "Build OK · 0.8 s";
        s.Rom = "ROM 412 KB / 32 MB";
        s.ToolchainOk = true;
        return root;
    }

    /// <summary>The documents of the Tilemap Editor frame (T-P03): Scene, Forest_01.map, hero.png.</summary>
    public static string OpenForTilemap(Fixture fx) =>
        Open(fx, ("Scene", "map", @"scenes\Forest_01.scene"), ("Forest_01.map", "tiles", @"maps\Forest_01.map"), ("hero.png", "image", @"assets\sprites\hero.png"));

    /// <summary>The documents of the Font Manager frame (T-P09): Scene, thai_vwf.font.</summary>
    public static string OpenForFonts(Fixture fx) =>
        Open(fx, ("Scene", "map", @"scenes\Forest_01.scene"), ("thai_vwf.font", "type", @"fonts\thai_vwf.font"));

    static void Copy(string from, string to)
    {
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(System.IO.Path.Combine(to, System.IO.Path.GetRelativePath(from, dir)));
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, System.IO.Path.Combine(to, System.IO.Path.GetRelativePath(from, file)), true);
    }
}
