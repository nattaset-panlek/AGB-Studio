using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Support;

namespace AgbStudio.Editor.Tests.Windows.Scripts;

/// <summary>
/// The full Sky Hopper fixture (AgbStudio.Build.Content.SkyHopper: scenes with map positions and links, 12 scripts,
/// maps, music) opened in a test's editor services. Generated once per test run and copied for each test.
/// </summary>
public static class SkyHopperProject
{
    static readonly Lazy<string> Template = new(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "agb-skyhopper-template-" + Environment.ProcessId);
        if (!Directory.Exists(dir)) AgbStudio.Build.Content.SkyHopper.Create(dir);
        return dir;
    });

    /// <summary>Copies the fixture into the test folder, opens it and returns its root.</summary>
    public static string Open(Fixture fx)
    {
        var root = Path.Combine(fx.Folder, "SkyHopper");
        Copy(Template.Value, root);
        fx.Services.Project.Open(root);
        return root;
    }

    static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var d in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, d)));
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, f));
            File.Copy(f, target, true);
            // keep the creation order of the scripts (Scripts list order)
            File.SetCreationTimeUtc(target, File.GetCreationTimeUtc(f));
        }
    }

    public static string Script(Fixture fx, string name) => Path.Combine(fx.Services.Project.Current!.ScriptsDir, name + ".cs");

    public static SceneFile Scene(Fixture fx, string name) => GameProject.Load<SceneFile>(Path.Combine(fx.Services.Project.Current!.ScenesDir, name + ".scene"));

    public static void SaveScene(Fixture fx, SceneFile s) => GameProject.Save(Path.Combine(fx.Services.Project.Current!.ScenesDir, s.Name + ".scene"), s);

    /// <summary>The open documents of the Figma Script Editor: Scene, PlayerController.cs, Slime.cs, items.db.</summary>
    public static void FigmaDocuments(Fixture fx)
    {
        var p = fx.Services.Project.Current!;
        var d = fx.Services.Documents;
        d.Add(new EditorDocument { Name = "Scene", Glyph = "map", Path = Path.Combine(p.ScenesDir, "Forest_01.scene") });
        d.Add(new EditorDocument { Name = "PlayerController.cs", Glyph = "code", Path = Script(fx, "PlayerController") });
        d.Add(new EditorDocument { Name = "Slime.cs", Glyph = "code", Path = Script(fx, "Slime") });
        d.Add(new EditorDocument { Name = "items.db", Glyph = "db", Path = Path.Combine(p.DataDir, "items.db") });
    }
}
