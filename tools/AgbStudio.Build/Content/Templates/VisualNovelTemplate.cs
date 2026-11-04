using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Visual Novel template (Launcher · Templates, P15.06): a short story at sunset told with portraits that change
/// expression, a speaker name tag, a choice that sets a saved flag and two endings, in Thai and English.
/// </summary>
public static class VisualNovelTemplate
{
    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "AVNE", "Title", s =>
        {
            s.Save.Fields.AddRange([new SaveField { Name = "chapter", Type = "int" }, new SaveField { Name = "braveHopper", Type = "bool" }]);
            s.Tags = ["Portrait"];
        });
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("speaker.none", " ", " ", "Dialogue"),
            ("speaker.hopper", "HOPPER", "ฮอปเปอร์", "Dialogue"),
            ("speaker.owl", "OWL", "นกฮูก", "Dialogue"),
            ("vn.1", "Where did all the coins go?", "เหรียญหายไปไหนหมดนะ?", "Dialogue"),
            ("vn.2", "I saw a slime carry them toward the cave.", "ข้าเห็นเจ้าสไลม์ขนไปทางถ้ำ", "Dialogue"),
            ("vn.3", "The cave? It is so dark in there...", "ถ้ำเหรอ ข้างในมืดมากเลยนะ...", "Dialogue"),
            ("vn.choice.cave", "Go to the cave", "ไปที่ถ้ำ", "Dialogue"),
            ("vn.choice.wait", "Wait for morning", "รอให้เช้าก่อน", "Dialogue"),
            ("vn.cave.1", "I am not scared. Let's go!", "ไม่กลัวหรอก ไปกันเถอะ!", "Dialogue"),
            ("vn.cave.2", "Take this lantern with you.", "เอาตะเกียงนี่ไปด้วยสิ", "Dialogue"),
            ("vn.wait.1", "Maybe tomorrow, when the sun is up.", "พรุ่งนี้ตอนเช้าดีกว่า", "Dialogue"),
            ("vn.wait.2", "A wise choice. Sleep well.", "ฉลาดมาก นอนหลับฝันดีนะ", "Dialogue"),
            ("vn.end", "To be continued...", "โปรดติดตามตอนต่อไป...", "Dialogue"),
        ]));
        p.Settings.General.DefaultLanguage = "th";
        p.SaveSettings();
        WriteArt(p);
        TemplateKit.Music(p, "sunset_theme", XmWriter.Tune("Sunset theme", 88, 4, 21, "A"));
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 92, 2, 17, "E"));
        TemplateKit.Sfx(p, "blip", 900, 900, 0.03, 0.35);
        WriteScenes(p);
        WriteScripts(p);
        WriteReadme(p);
        TemplateKit.OpenOnMainScene(p);
        return p;
    }

    static string RomTitle(string name)
    {
        var t = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return t.Length == 0 ? "NOVEL" : t[..Math.Min(12, t.Length)];
    }

    // ---- art --------------------------------------------------------------------------------------------

    static readonly uint SkyTop = Canvas.Rgb(248, 152, 96), SkyMid = Canvas.Rgb(248, 184, 120), SkyLow = Canvas.Rgb(252, 208, 152);
    static readonly uint Sun = Canvas.Rgb(255, 236, 168), HillFar = Canvas.Rgb(168, 104, 144), HillNear = Canvas.Rgb(120, 72, 120);
    static readonly uint Yellow = Canvas.Rgb(250, 212, 64), YellowDark = Canvas.Rgb(214, 160, 40), Pink = Canvas.Rgb(240, 112, 140);
    static readonly uint Brown = Canvas.Rgb(150, 104, 72), BrownLight = Canvas.Rgb(200, 160, 112);

    /// <summary>Sunset: sky bands, the sun and two rows of hills (240 x 160).</summary>
    public static Canvas Sunset()
    {
        var c = new Canvas(240, 160);
        c.Rect(0, 0, 240, 48, SkyTop);
        c.Rect(0, 48, 240, 40, SkyMid);
        c.Rect(0, 88, 240, 72, SkyLow);
        c.Ellipse(176, 80, 20, 20, Sun);
        for (var x = 0; x < 240; x++)
        {
            var far = 92 + (int)(8 * Math.Sin(x * Math.PI / 40) + 5 * Math.Sin(x * Math.PI / 17));
            c.Rect(x, far, 1, 160 - far, HillFar);
            var near = 108 + (int)(6 * Math.Sin(x * Math.PI / 60 + 2));
            c.Rect(x, near, 1, 160 - near, HillNear);
        }
        return c;
    }

    /// <summary>Hopper: neutral, happy and worried (64 x 64 frames).</summary>
    public static Canvas Hopper()
    {
        var c = new Canvas(64 * 3, 64);
        for (var f = 0; f < 3; f++)
        {
            var x = f * 64;
            c.Ellipse(x + 32, 38, 26, 24, Yellow);
            c.Ellipse(x + 32, 48, 18, 12, YellowDark);
            c.Ellipse(x + 32, 40, 24, 20, Yellow);
            c.Rect(x + 26, 6, 4, 10, YellowDark);   // crest
            c.Rect(x + 32, 4, 4, 12, YellowDark);
            if (f == 1)
            {
                // happy: closed, curved eyes
                c.Line(x + 18, 32, x + 22, 28, PixelArt.Ink); c.Line(x + 22, 28, x + 26, 32, PixelArt.Ink);
                c.Line(x + 38, 32, x + 42, 28, PixelArt.Ink); c.Line(x + 42, 28, x + 46, 32, PixelArt.Ink);
            }
            else
            {
                c.Rect(x + 19, 26, 6, 8, PixelArt.Ink); c.Rect(x + 39, 26, 6, 8, PixelArt.Ink);
                c.Rect(x + 20, 27, 2, 2, PixelArt.White); c.Rect(x + 40, 27, 2, 2, PixelArt.White);
            }
            if (f == 2)
            {
                // worried: brows tilted up in the middle
                c.Line(x + 17, 22, x + 25, 19, PixelArt.Ink); c.Line(x + 39, 19, x + 47, 22, PixelArt.Ink);
            }
            c.Rect(x + 28, 38, 8, 5, Pink);           // beak
            c.Rect(x + 30, 43, 4, 2, f == 2 ? PixelArt.Ink : Pink);
        }
        return c;
    }

    /// <summary>The owl (64 x 64, one frame).</summary>
    public static Canvas Owl()
    {
        var c = new Canvas(64, 64);
        c.Ellipse(32, 38, 24, 25, Brown);
        c.Ellipse(32, 44, 16, 16, BrownLight);
        c.Rect(12, 12, 8, 10, Brown); c.Rect(44, 12, 8, 10, Brown);  // ear tufts
        c.Ellipse(22, 30, 8, 8, PixelArt.White); c.Ellipse(42, 30, 8, 8, PixelArt.White);
        c.Ellipse(22, 31, 4, 4, PixelArt.Ink); c.Ellipse(42, 31, 4, 4, PixelArt.Ink);
        c.Rect(30, 38, 4, 6, PixelArt.Gold);
        return c;
    }

    static void WriteArt(GameProject p)
    {
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        TemplateKit.Image(p, "sunset", Sunset());
        TemplateKit.Sprite(p, "hopper_portrait", Hopper(), new SpriteMeta
        {
            FrameWidth = 64, FrameHeight = 64, OriginX = 32, OriginY = 64, Collider = new ColliderData { X = 0, Y = 0, W = 0, H = 0 },
            Animations =
            {
                new AnimationData { Name = "neutral", Frames = { 0 }, Loop = false },
                new AnimationData { Name = "happy", Frames = { 1 }, Loop = false },
                new AnimationData { Name = "worried", Frames = { 2 }, Loop = false },
            },
        });
        TemplateKit.Sprite(p, "owl_portrait", Owl(), new SpriteMeta
        {
            FrameWidth = 64, FrameHeight = 64, OriginX = 32, OriginY = 64, Collider = new ColliderData { X = 0, Y = 0, W = 0, H = 0 },
        });
    }

    static void WriteScenes(GameProject p)
    {
        static MapFile Backdrop(string name) => new()
        {
            Name = name, Width = 15, Height = 10, CollisionLayer = -1,
            Layers = { new MapLayerData { Name = "Sunset", Type = LayerType.Image, Bg = 2, Priority = 2, Image = "sunset" } },
        };
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), Backdrop("Title"));
        GameProject.Save(Path.Combine(p.MapsDir, "Story.map"), Backdrop("Story"));
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene(p, "Title", "#F89860", "Story", "title_theme"));
        var story = new SceneFile
        {
            Hud = TemplateKit.HudPage(p, "Hud", false, ("Speaker", "speaker.none", 16, 97, 104)),
            Name = "Story", Width = 15, Height = 10, Map = "Story", Backdrop = "#F89860", MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "sunset_theme" },
            Camera = new CameraSettings { Follow = "" },
            Actors =
            {
                new ActorData { Name = "Story", Kind = ActorKind.Empty, X = 120, Y = 8, Components = { new ComponentData { Type = "Story" } } },
                new ActorData { Name = "Hopper", Kind = ActorKind.Empty, Sprite = "hopper_portrait", Animation = "neutral", X = 56, Y = 96, Tag = "Portrait", Priority = 1 },
                new ActorData { Name = "Owl", Kind = ActorKind.Empty, Sprite = "owl_portrait", X = 184, Y = 96, Tag = "Portrait", Priority = 1, Hidden = true },
            },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Story.scene"), story);
    }

    // ---- README -----------------------------------------------------------------------------------------

    static void WriteReadme(GameProject p) => TemplateKit.Readme(p, "Visual Novel",
        "A short story at sunset: Hopper and the Owl talk, you pick what Hopper does, the choice is saved as a flag and leads to one of two endings. The text is in Thai and English (Options > Language).",
        ["A: next line", "D-pad up / down: pick a choice, A: confirm", "Start: pause menu"],
        new Dictionary<string, string>
        {
            ["Story"] = "Invisible director: the Story script runs the whole scene as one cutscene (lines, the choice, the ending) when the scene starts.",
            ["Hopper"] = "Portrait sprite with the animations neutral, happy and worried; Stage.Mood picks one between lines.",
            ["Owl"] = "Second portrait, hidden until the owl speaks for the first time (Stage.Speaker shows it).",
        },
        [
            "Add lines: write new keys in Localization, then add `await Say(\"your.key\");` lines in Story.cs.",
            "Add a third choice: `await Choice(\"a\", \"b\", \"c\")` returns 0, 1 or 2.",
            "Draw a new expression in the Sprite Editor (hopper_portrait), add it as an animation and use it in Stage.Mood.",
            "Read the saved flag `Game.braveHopper` in a second chapter scene to change what the owl says.",
        ]);

    // ---- scripts ----------------------------------------------------------------------------------------

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "Story", """
using System.Threading.Tasks;

// The story of the Story scene, on the invisible Story actor: it starts when the scene starts. Stage.Speaker and
// Stage.Mood (Stage.cs) change the name tag and the portraits between lines.
public class Story : Behaviour
{
    // Start runs once, when the scene has loaded the actor.
    void Start()
    {
        Play();
    }

    // A [Cutscene] runs over many frames: each await waits for the player.
    [Cutscene]
    static async Task Play()
    {
        Game.chapter = 1;          // a save field (Project Settings > Save)
        Stage.Speaker(1);
        Stage.Mood(2);
        await Say("vn.1");         // shows a line from Localization in the dialogue box and waits for A
        Stage.Speaker(2);
        await Say("vn.2");
        Stage.Speaker(1);
        await Say("vn.3");
        // Choice shows the options and returns the number of the one picked (0 = first).
        var answer = await Choice("vn.choice.cave", "vn.choice.wait");
        if (answer == 0)
        {
            Game.braveHopper = true;   // the flag a later chapter can read
            Stage.Mood(1);
            await Say("vn.cave.1");
            Stage.Speaker(2);
            await Say("vn.cave.2");
        }
        else
        {
            Game.braveHopper = false;
            Stage.Mood(0);
            await Say("vn.wait.1");
            Stage.Speaker(2);
            await Say("vn.wait.2");
        }
        Stage.Speaker(0);
        await Say("vn.end");
        Save.Write(0);             // writes the save data to slot 0 (Continue on the title screen reads it)
        Scene.Load("Title");
    }
}
""");
        TemplateKit.Script(p, "Stage", """
// Helpers the story calls between lines: who speaks (name tag and portraits) and Hopper's expression.
// [ScriptNative] makes a static method callable from a cutscene.
public static class Stage
{
    // 0 nobody, 1 Hopper, 2 Owl. The owl appears the first time it speaks.
    [ScriptNative]
    public static void Speaker(int who)
    {
        // UI.SetText puts another string key into an element of the HUD page (ui/Hud.uipage).
        if (who == 1) UI.SetText("Speaker", "speaker.hopper");
        else if (who == 2) UI.SetText("Speaker", "speaker.owl");
        else UI.SetText("Speaker", "speaker.none");
        Actor owl = Scene.Find("Owl");
        if (owl != null && who == 2) owl.visible = true;   // the Owl starts hidden (Inspector: Hidden)
        Sound.Play("blip");
    }

    // 0 neutral, 1 happy, 2 worried: animations of the hopper_portrait sprite.
    [ScriptNative]
    public static void Mood(int mood)
    {
        Actor hopper = Scene.Find("Hopper");
        if (hopper == null) return;
        if (mood == 1) hopper.PlayAnimation("happy");
        else if (mood == 2) hopper.PlayAnimation("worried");
        else hopper.PlayAnimation("neutral");
    }
}
""");
    }
}
