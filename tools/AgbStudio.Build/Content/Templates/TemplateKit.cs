using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>Shared pieces of the templates: project setup, fonts and strings, assets, title scene. All content is
/// generated here (D-20): pixel art from code, chiptune modules, sound effects and text written for AGB Studio.</summary>
public static class TemplateKit
{
    /// <summary>A fresh project in <paramref name="dir"/> (the folder must not hold another project).</summary>
    public static GameProject NewProject(string dir, string name, string title, string romTitle, string gameCode, string startScene, Action<ProjectFile>? configure = null)
    {
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
            throw new IOException($"The folder {dir} is not empty, and a new project could overwrite its files. Pick an empty or new folder.");
        var s = new ProjectFile { Name = name };
        s.General.GameTitle = title;
        s.General.RomTitle = romTitle;
        s.General.GameCode = gameCode;
        s.General.StartScene = startScene;
        s.General.BootSplash = true;
        s.AllowRemap = true;
        configure?.Invoke(s);
        return GameProject.Create(dir, name, s);
    }

    /// <summary>Noto Sans Thai (12 px, 2bpp, drop shadow) as the default font, so every string works in Thai and English.</summary>
    public static void AddThaiFont(GameProject p)
    {
        var fonts = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts");
        File.Copy(Path.Combine(fonts, "NotoSansThai-Regular.ttf"), Path.Combine(p.FontsDir, "NotoSansThai.ttf"), true);
        File.Copy(Path.Combine(fonts, "OFL-NotoSansThai.txt"), Path.Combine(p.FontsDir, "OFL-NotoSansThai.txt"), true);
        GameProject.Save(Path.Combine(p.FontsDir, "thai_vwf.font"), new FontFile
        {
            Name = "thai_vwf", Source = "NotoSansThai.ttf", Size = 12, Variable = true, CharacterSets = { "Basic Latin", "Thai" },
            LineHeight = 14, Baseline = 10, Spacing = 1, Bpp = 2, DropShadow = true,
        });
        p.Settings.General.DefaultFont = "thai_vwf";
        p.SaveSettings();
    }

    /// <summary>English and Thai string tables (key -> English, Thai) with groups for Localization.</summary>
    public static void AddStrings(GameProject p, IEnumerable<(string Key, string En, string Th, string Group)> strings)
    {
        var en = new StringTable { Language = "en" };
        var th = new StringTable { Language = "th" };
        var meta = new StringMeta();
        foreach (var (key, e, t, group) in strings)
        {
            en.Strings[key] = e;
            th.Strings[key] = t;
            meta.Keys[key] = new StringInfo { Group = group, BoxWidth = 224, MaxLines = group == "Dialogue" ? 3 : 1 };
        }
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), en);
        GameProject.Save(Path.Combine(p.TextDir, "strings.th.json"), th);
        GameProject.Save(Path.Combine(p.TextDir, "strings.meta.json"), meta);
        p.Settings.Localization.Source = "en";
        p.Settings.Localization.Languages = [new LanguageInfo { Code = "en", Name = "English", Font = "thai_vwf" }, new LanguageInfo { Code = "th", Name = "ไทย", Font = "thai_vwf", Fallback = "en" }];
        p.Settings.General.DefaultLanguage = "en";
        p.SaveSettings();
    }

    /// <summary>The title screen strings every template uses (Figma "In-game · Title screen").</summary>
    public static IEnumerable<(string, string, string, string)> MenuStrings(string titleEn, string titleTh) =>
    [
        ("ui.title", titleEn, titleTh, "UI & menus"),
        ("ui.menu.start", "Start", "เริ่มเกม", "UI & menus"),
        ("ui.menu.continue", "Continue", "เล่นต่อ", "UI & menus"),
        ("ui.menu.options", "Options", "ตั้งค่า", "UI & menus"),
        ("ui.pause.title", "Paused", "หยุดเกม", "UI & menus"),
        ("ui.pause.resume", "Resume", "เล่นต่อ", "UI & menus"),
        ("ui.pause.options", "Options", "ตั้งค่า", "UI & menus"),
        ("ui.pause.quit", "Quit to title", "กลับหน้าแรก", "UI & menus"),
        ("ui.options.title", "Options", "ตั้งค่า", "UI & menus"),
        ("ui.options.music", "Music", "เพลง", "UI & menus"),
        ("ui.options.effects", "Sound effects", "เสียงเอฟเฟกต์", "UI & menus"),
        ("ui.options.language", "Language", "ภาษา", "UI & menus"),
        ("ui.options.controls", "Controls", "ปุ่มควบคุม", "UI & menus"),
        ("ui.options.hint", "A: change   B: back", "A: เปลี่ยน   B: กลับ", "UI & menus"),
        ("ui.options.press", "press a button", "กดปุ่มที่ต้องการ", "UI & menus"),
    ];

    /// <summary>A title scene: the title label and the Start / Continue / Options menu (Figma "In-game · Title screen").</summary>
    public static SceneFile TitleScene(string map, string backdrop, string firstScene, string music = "") => new()
    {
        Name = "Title", Width = 15, Height = 10, Map = map, Backdrop = backdrop, MapX = 40, MapY = 60,
        Music = new MusicSettings { Track = music },
        Camera = new CameraSettings { Follow = "" },
        Actors = { new ActorData { Name = "Title", Kind = ActorKind.TextLabel, X = 88, Y = 40, Text = "ui.title" } },
        Menu = new SceneMenu
        {
            X = 88, Y = 72, Width = 96, Height = 48, Spacing = 14,
            Items =
            {
                new SceneMenuItem { Text = "ui.menu.start", Command = MenuCommand.LoadScene, Scene = firstScene },
                new SceneMenuItem { Text = "ui.menu.continue", Command = MenuCommand.Continue },
                new SceneMenuItem { Text = "ui.menu.options", Command = MenuCommand.Options },
            },
        },
    };

    public static void Sprite(GameProject p, string name, Canvas art, SpriteMeta meta)
    {
        var path = Path.Combine(p.AssetsDir, "sprites", name + ".png");
        art.Save(path);
        GameProject.Save(GameProject.MetaPath(path), meta);
    }

    public static void Image(GameProject p, string name, Canvas art) => art.Save(Path.Combine(p.AssetsDir, "images", name + ".png"));

    public static void Tileset(GameProject p, string name, Canvas art, TilesetMeta meta)
    {
        var path = Path.Combine(p.AssetsDir, "tilesets", name + ".png");
        art.Save(path);
        GameProject.Save(GameProject.MetaPath(path), meta);
    }

    public static void Music(GameProject p, string name, byte[] module) => File.WriteAllBytes(Path.Combine(p.AssetsDir, "audio", "music", name + ".xm"), module);

    /// <summary>A short square-wave sound that slides from one pitch to another.</summary>
    public static void Sfx(GameProject p, string name, double fromHz, double toHz, double seconds, double volume = 0.7)
    {
        const int rate = 11025;
        var n = (int)(seconds * rate);
        var data = new sbyte[n];
        double phase = 0;
        for (var i = 0; i < n; i++)
        {
            var t = (double)i / n;
            phase += (fromHz + (toHz - fromHz) * t) / rate;
            data[i] = (sbyte)((phase % 1 < 0.5 ? 1 : -1) * 100 * volume * (1 - t));
        }
        File.WriteAllBytes(Path.Combine(p.AssetsDir, "audio", "sfx", name + ".wav"), WavWriter.Encode(data, rate));
    }

    public static void Script(GameProject p, string name, string body) =>
        File.WriteAllText(Path.Combine(p.ScriptsDir, name + ".cs"), "using GBA;\n\n" + body.Trim() + "\n");

    public static void Save<T>(GameProject p, string folder, string name, T value) => GameProject.Save(Path.Combine(p.Root, folder, name), value);
}
