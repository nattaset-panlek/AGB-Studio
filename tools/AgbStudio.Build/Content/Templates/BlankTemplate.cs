using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>Blank template: one empty scene and nothing else, for starting a game from nothing.</summary>
public static class BlankTemplate
{
    public const string Id = "blank", StartScene = "Scene_01";

    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "BLNK", StartScene);
        // One GBA screen with a fixed camera, as the project that New Project makes without a sample level.
        TemplateKit.Save(p, "scenes", StartScene + ".scene", new SceneFile
        {
            Name = StartScene, Width = 15, Height = 10, Camera = new CameraSettings { Follow = "" },
        });
        TemplateKit.Readme(p, "Blank", "It has one empty scene, so everything in it is yours to add.", [],
            new Dictionary<string, string>(),
            ["Import a tileset and a sprite with Assets › Import", "Add a Player actor to the scene and press Play"]);
        p.Settings.LastScene = StartScene;
        p.SaveSettings();
        return p;
    }

    static string RomTitle(string name)
    {
        var t = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return t.Length == 0 ? "BLANK" : t[..Math.Min(12, t.Length)];
    }
}
