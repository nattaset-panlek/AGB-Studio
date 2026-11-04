using AgbStudio.Build.Audio;
using AgbStudio.Build.Fonts;
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

    /// <summary>Manop Thai 6x14 as the default font, so every string works in Thai and English, and the dialogue box of
    /// Project Settings with room for three lines.</summary>
    public static void AddThaiFont(GameProject p)
    {
        ManopThaiFont.CopyTo(p.FontsDir);
        GameProject.Save(Path.Combine(p.FontsDir, "thai_vwf.font"), ManopThaiFont.Settings("thai_vwf"));
        p.Settings.General.DefaultFont = "thai_vwf";
        p.Settings.Ui.Dialogue = new DialogueBoxSettings { Paper = p.Settings.Ui.Paper, Ink = p.Settings.Ui.Ink, Border = p.Settings.Ui.Ink };
        p.SaveSettings();
    }

    /// <summary>The menus of a template as UI pages (ui/*.uipage), open to change in the UI Editor: the title screen, the
    /// pause menu, the Options screen and the choices of the dialogue box.</summary>
    static void AddMenuPages(GameProject p, string firstScene)
    {
        Directory.CreateDirectory(p.UiDir);
        string[] keys = ["ui.title", "ui.menu.start", "ui.menu.continue", "ui.menu.options", "ui.pause.title", "ui.pause.resume", "ui.pause.options", "ui.pause.quit", "ui.options.title", "ui.options.music", "ui.options.effects", "ui.options.language", "ui.options.controls", "ui.options.hint"];
        foreach (var name in new[] { "Title", "Pause", "Options", "Choice" })
            GameProject.Save(Path.Combine(p.UiDir, name + ".uipage"), UiPresets.Create(name, p.Settings.Ui, firstScene, keys)!);
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

    /// <summary>A title scene: it opens the Title UI page (the title and the Start / Continue / Options menu), and the pages
    /// of the game's menus are written with it.</summary>
    public static SceneFile TitleScene(GameProject p, string map, string backdrop, string firstScene, string music = "")
    {
        AddMenuPages(p, firstScene);
        return new SceneFile
        {
            Name = "Title", Width = 15, Height = 10, Map = map, Backdrop = backdrop, MapX = 40, MapY = 60,
            Music = new MusicSettings { Track = music },
            Camera = new CameraSettings { Follow = "" },
            Page = "Title",
        };
    }

    /// <summary>
    /// README.md in the project folder: what the template is, the controls, every scene with its camera, background
    /// layers and objects (each object's components are read from the scene files, so the table always matches
    /// what the Hierarchy shows), the scripts with the comment at their top, and things to try changing.
    /// <paramref name="objects"/> describes every actor by name ("Scene/Name" for one scene only); an actor without
    /// a description is an error, so a template cannot add an object and forget to explain it.
    /// </summary>
    public static void Readme(GameProject p, string template, string intro, IEnumerable<string> controls,
        IReadOnlyDictionary<string, string> objects, IEnumerable<string> tryThis)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine($"# {p.Settings.General.GameTitle}");
        md.AppendLine();
        md.AppendLine($"Made from the AGB Studio **{template}** template. {intro}");
        md.AppendLine();
        md.AppendLine("Open a scene in the Scene Editor: the Hierarchy shows every object with small badges for its components, and the");
        md.AppendLine("Inspector lists them under **Components**. Double click a script section header to open its C# file.");
        md.AppendLine();
        md.AppendLine("## Controls");
        md.AppendLine();
        foreach (var c in controls) md.AppendLine("- " + c);
        md.AppendLine();
        md.AppendLine("## Scenes and objects");
        var scripts = p.ScriptFiles().Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        var start = p.Settings.General.StartScene;
        foreach (var file in p.SceneFiles().OrderBy(f => GameProject.AssetName(f) == start ? 0 : 1).ThenBy(f => GameProject.Load<SceneFile>(f).MapX))
        {
            var scene = GameProject.Load<SceneFile>(file);
            md.AppendLine();
            md.AppendLine($"### {scene.Name}{(scene.Name == start ? " (start scene)" : "")}");
            md.AppendLine();
            md.AppendLine($"- Camera: {(scene.Camera.Follow.Length > 0 ? $"follows {scene.Camera.Follow} (dead zone {scene.Camera.DeadZoneW} x {scene.Camera.DeadZoneH} px{(scene.Camera.ClampToMap ? ", stays inside the map" : "")})" : "fixed")}.");
            if (scene.Map.Length > 0 && File.Exists(Path.Combine(p.MapsDir, scene.Map + ".map")))
            {
                var map = GameProject.Load<MapFile>(Path.Combine(p.MapsDir, scene.Map + ".map"));
                foreach (var l in map.Layers)
                    md.AppendLine((l.Type == LayerType.CollisionOnly ? $"- {l.Name}: " : $"- BG{l.Bg} {l.Name}: ")
                        + (l.Type switch { LayerType.Image => $"image {l.Image}.png", LayerType.Affine => $"rotating tile layer ({map.Tileset})", LayerType.CollisionOnly => "collision tiles only (not drawn)", _ => $"tiles from {map.Tileset}" })
                        + (Math.Abs(l.ParallaxX - 1) > 0.001 ? $", scrolls at {l.ParallaxX.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} of the camera speed" : "")
                        + (map.Layers.IndexOf(l) == map.CollisionLayer ? ", the collision layer (solid tiles stop actors)" : "") + ".");
            }
            if (scene.Music.Track.Length > 0) md.AppendLine($"- Music: {scene.Music.Track}.");
            if (scene.Page.Length > 0) md.AppendLine($"- UI page: {scene.Page} (open it in Tools > UI Editor to change the title and menu).");
            if (scene.Menu != null) md.AppendLine($"- Menu: {string.Join(", ", scene.Menu.Items.Select(i => i.Command == MenuCommand.LoadScene ? $"{i.Text} loads {i.Scene}" : $"{i.Text} ({i.Command})"))} (Scene Settings > Menu).");
            if (scene.Events.OnEnter.Length > 0) md.AppendLine($"- On enter: `{scene.Events.OnEnter}` (Scene Settings > Events).");
            if (scene.Actors.Count == 0) continue;
            md.AppendLine();
            md.AppendLine("| Object | Components | What it does |");
            md.AppendLine("|---|---|---|");
            foreach (var a in scene.Actors)
            {
                var what = objects.TryGetValue(scene.Name + "/" + a.Name, out var w1) ? w1 : objects.TryGetValue(a.Name, out var w2) ? w2
                    : throw new InvalidOperationException($"README of {template}: the object {a.Name} of {scene.Name} has no description.");
                var parts = new List<string>();
                if (a.Sprite.Length > 0) parts.Add($"Sprite {a.Sprite}" + (a.Animation.Length > 0 ? $" ({a.Animation})" : ""));
                if (a.Kind == ActorKind.TextLabel) parts.Add($"Text label `{a.Text}`");
                if (a.Kind == ActorKind.TriggerZone) parts.Add($"Trigger zone {a.ZoneW} x {a.ZoneH}");
                foreach (var c in a.Components)
                    parts.Add(scripts.Contains(c.Type) ? $"{c.Type} (script)" : Pipeline.BuiltinBehaviours.Find(c.Type) != null ? $"{c.Type} (built-in)" : c.Type);
                if (scene.Camera.Follow == a.Name) parts.Add("Camera follows");
                foreach (var l in scene.Links.Where(l => l.From == a.Name)) parts.Add($"Link to {l.To}");
                var name = a.Name + (a.Hidden ? " (hidden, spawned by script)" : "");
                md.AppendLine($"| {name} | {string.Join(", ", parts)} | {what} |");
            }
        }
        md.AppendLine();
        md.AppendLine("## Scripts");
        md.AppendLine();
        foreach (var file in p.ScriptFiles().OrderBy(f => f, StringComparer.Ordinal))
        {
            // the comment block at the top of the file, as one line
            var head = File.ReadLines(file).Select(l => l.Trim()).SkipWhile(l => !l.StartsWith("//", StringComparison.Ordinal))
                .TakeWhile(l => l.StartsWith("//", StringComparison.Ordinal)).Select(l => l.TrimStart('/').Trim());
            md.AppendLine($"- `scripts/{Path.GetFileName(file)}`: {string.Join(" ", head)}");
        }
        md.AppendLine();
        md.AppendLine("## Try changing");
        md.AppendLine();
        foreach (var t in tryThis) md.AppendLine("- " + t);
        md.AppendLine();
        md.AppendLine("All art, music, sounds and text of this template were generated for AGB Studio.");
        File.WriteAllText(Path.Combine(p.Root, "README.md"), md.ToString().ReplaceLineEndings("\r\n"));
    }

    /// <summary>The scene a template is about: the first scene the title menu starts (the scene the editor opens).</summary>
    public static string? MainScene(GameProject p)
    {
        var start = p.Settings.General.StartScene;
        var scenes = p.SceneFiles().Select(GameProject.Load<SceneFile>).ToList();
        var first = scenes.FirstOrDefault(s => s.Name == start);
        if (first is null) return null;
        return first.Menu?.Items.FirstOrDefault(i => i.Command == MenuCommand.LoadScene)?.Scene ?? UiPageLinks.LoadSceneButtons(p, first).FirstOrDefault()?.Action.Target;
    }

    /// <summary>The editor opens the template's main scene (not the title screen, which has no objects to learn from);
    /// the game still starts on the title screen.</summary>
    public static void OpenOnMainScene(GameProject p)
    {
        if (MainScene(p) is { } main)
        {
            p.Settings.LastScene = main;
            p.SaveSettings();
        }
    }

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
