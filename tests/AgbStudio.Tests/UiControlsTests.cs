using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Fonts;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>Scroll Rect, Scrollbar, Dropdown, Input Field, numbers in text, the cursor API, the waiting cutscene (UI.Choose),
/// the Dialogue box settings and text tokens, in a built game.</summary>
[Collection("emulator")]
public sealed class UiControlsTests(EmulatorFixture emu)
{
    const string Paper = "#F8F0D8", Focus = "#3858C8", Ink = "#202030", Thumb = "#C03030", Frame = "#108010";

    static UiElement Row(int i) => new()
    {
        Name = "Row" + i, Kind = UiElementKind.Button, Rect = new UiRect { W = 0, H = 16 }, Text = "Row", TextColor = Ink,
        FocusFill = Focus, Action = new UiAction { Data = i }, NavRight = "Pick",
    };

    static GameProject CreateProject()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_controls"));
        SampleText.AddTo(p, "en");
        p.Settings.General.BootSplash = false;
        p.Settings.Ui.PauseMenu = false;
        p.Settings.Ui.Paper = Paper;
        p.Settings.Ui.Dialogue = new DialogueBoxSettings { Border = Frame, Paper = Paper };
        p.SaveSettings();
        var table = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json"));
        table.Strings["line.value"] = "You have {0} and <c1>{1}</c>.";
        table.Strings["line.fmt"] = "HP {0}/{1}";
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), table);
        Directory.CreateDirectory(p.UiDir);
        GameProject.Save(Path.Combine(p.UiDir, "Main.uipage"), new UiPageFile
        {
            Name = "Main",
            Root = new UiElement
            {
                Name = "Canvas", Rect = UiRect.Stretch(),
                Children =
                {
                    new UiElement
                    {
                        Name = "List", Kind = UiElementKind.ScrollRect, Rect = UiRect.TopLeft(16, 16, 96, 48), Fill = Paper,
                        Layout = new UiLayoutGroup { Kind = UiLayoutKind.Vertical, StretchCross = true },
                        Children = { Row(0), Row(1), Row(2), Row(3), Row(4), Row(5) },
                    },
                    new UiElement { Name = "Bar", Kind = UiElementKind.Scrollbar, ScrollTarget = "List", Rect = UiRect.TopLeft(112, 16, 8, 48), Fill = Ink, Accent = Thumb },
                    new UiElement { Name = "Pick", Kind = UiElementKind.Dropdown, Rect = UiRect.TopLeft(128, 16, 96, 16), Fill = Paper, TextColor = Ink, FocusFill = Focus, Options = { "One", "Two", "Three" }, Action = new UiAction { Data = 50 }, NavLeft = "Row0" },
                    new UiElement { Name = "Name", Kind = UiElementKind.InputField, Rect = UiRect.TopLeft(128, 34, 96, 16), Fill = Paper, TextColor = Ink, FocusFill = Focus, MaxLength = 4, Action = new UiAction { Data = 60 } },
                    new UiElement { Name = "Hp", Kind = UiElementKind.Text, Rect = UiRect.TopLeft(16, 66, 96, 16), Text = "line.fmt", TextColor = Ink },
                    new UiElement { Name = "Off", Kind = UiElementKind.Button, Rect = UiRect.TopLeft(128, 52, 96, 2), Fill = Ink, Interactable = false },
                },
            },
        });
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Controls.cs"), """
            using GBA;
            using System.Threading.Tasks;

            public class Controls : Behaviour
            {
                void Update()
                {
                    if (Input.Pressed(Key.Select)) Run();
                }

                [Cutscene]
                static async Task Run()
                {
                    Helper.Open();
                    int r = 0;
                    while (r != -1)
                    {
                        r = await UI.Choose(1);
                        Helper.Report(r);
                        if (r == 3) await Say("line.value");
                    }
                    Helper.Close();
                }
            }

            public static class Helper
            {
                [ScriptNative]
                public static void Open()
                {
                    UI.Open("Main");
                    UI.SetFraction("Hp", 18, 22);
                    Text.SetVar(0, 7, 2);
                    Text.SetVarText(1, "hud.score");
                }

                [ScriptNative]
                public static void Report(int r)
                {
                    Debug.Log($"choose {r} focus {UI.FocusData} pick {UI.GetAmount("Pick")} typed {UI.InputLength} {UI.InputChar(0)}");
                }

                [ScriptNative] public static void Close() => UI.Close();
            }
            """);
        var scene = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var s = GameProject.Load<SceneFile>(scene);
        s.Actors.First(a => a.Name == "Player").Components.Add(new ComponentData { Type = "Controls" });
        GameProject.Save(scene, s);
        return p;
    }

    static void Press(PlaySession s, GbaKeys key)
    {
        s.Keys = key;
        s.Step();
        s.Step();
        s.Keys = 0;
        for (var i = 0; i < 10; i++) s.Step();
    }

    static (int R, int G, int B) Pixel(LibretroCore core, int x, int y)
    {
        var c = core.Frame[y * LibretroCore.Width + x];
        return ((int)(c >> 16) & 255, (int)(c >> 8) & 255, (int)c & 255);
    }

    static void Near((int R, int G, int B) want, (int R, int G, int B) got)
    {
        Assert.True(Math.Abs(want.R - got.R) < 12 && Math.Abs(want.G - got.G) < 12 && Math.Abs(want.B - got.B) < 12, $"wanted {want}, got {got}");
    }

    static void Heard(PlaySession s, string text) => Assert.Contains(s.Log, l => l.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void ScrollDropdownInputField_AndAWaitingCutscene_Work()
    {
        var p = CreateProject();
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 90; i++) s.Step();
        Press(s, GbaKeys.Select);
        for (var i = 0; i < 10; i++) s.Step();
        Heard(s, "UI page Main opened");
        Near((56, 88, 200), Pixel(s.Core, 100, 20));   // row 0 has the focus
        Near((192, 48, 48), Pixel(s.Core, 116, 20));   // the thumb starts at the top of the bar
        Near((32, 32, 48), Pixel(s.Core, 116, 60));    // the track shows under it: half the list fits

        // Going down the list: the cursor stays in view and the list moves under the mask.
        for (var i = 0; i < 5; i++) Press(s, GbaKeys.Down);
        Heard(s, "choose -2 focus 5");
        Near((56, 88, 200), Pixel(s.Core, 100, 60));   // row 5 is the bottom row of the view
        Near((248, 240, 216), Pixel(s.Core, 100, 20)); // row 3 at the top, unfocused
        Near((192, 48, 48), Pixel(s.Core, 116, 60));   // the thumb followed

        // A waiting cutscene hears A with the row's Data, and the page stays open; Say works over the page, and it comes back.
        Press(s, GbaKeys.Up);
        Press(s, GbaKeys.Up);
        Press(s, GbaKeys.A);
        Heard(s, "choose 3 focus 3");
        for (var i = 0; i < 60; i++) s.Step();
        Near((16, 128, 16), Pixel(s.Core, 8, 130));     // the Dialogue box's border, from Project Settings
        Near((248, 240, 216), Pixel(s.Core, 12, 116));  // inside it: the padding has no text
        Press(s, GbaKeys.A);
        for (var i = 0; i < 6; i++) s.Step();

        // Dropdown: A opens, Down moves, A picks.
        Press(s, GbaKeys.Right);
        Press(s, GbaKeys.A);
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.A);
        Heard(s, "choose 50 focus 50 pick 1");

        // Input Field: A opens the keyboard, A types the first key, Start ends.
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.A);
        Press(s, GbaKeys.A);
        Press(s, GbaKeys.Start);
        Heard(s, "choose 60 focus 60 pick 1 typed 1 65");
        // Start on the field itself takes the text without opening the keyboard.
        Press(s, GbaKeys.Start);
        Assert.Equal(2, s.Log.Count(l => l.Contains("choose 60", StringComparison.Ordinal)));

        // B: the cutscene hears -1 and closes the page.
        Press(s, GbaKeys.B);
        Heard(s, "choose -1");
        for (var i = 0; i < 4; i++) s.Step();
        Heard(s, "UI page Main closed");
    }

    [Fact]
    public void Tokens_ReserveSpace_AndAreNotGlyphs()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_tokens"));
        SampleText.AddTo(p, "en");
        p.Settings.General.BootSplash = false;
        p.SaveSettings();
        var table = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json"));
        table.Strings["line.value"] = "Go {0}! <c2>Now</c> {1}";
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), table);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var cpp = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "text", "strings_en.cpp"));
        Assert.Contains("TextMarkKind::Var, 0", cpp);
        Assert.Contains("TextMarkKind::Color, 2", cpp);
        Assert.Contains("TextMarkKind::Color, 0", cpp);
        Assert.Contains("TextMarkKind::Var, 1", cpp);
        Assert.Equal(56, TextLayout.VarReserve);
    }

    [Fact]
    public void TextThatDoesNotFit_IsAWarningAtBuildTime()
    {
        var p = CreateProject();
        var file = Path.Combine(p.UiDir, "Tiny.uipage");
        GameProject.Save(file, new UiPageFile
        {
            Name = "Tiny",
            Root = new UiElement
            {
                Name = "Canvas", Rect = UiRect.Stretch(),
                Children = { new UiElement { Name = "T", Kind = UiElementKind.Text, Rect = UiRect.TopLeft(0, 0, 8, 16), Text = "dlg.forest.hopper_01", TextColor = Ink } },
            },
        });
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.Contains(r.Diagnostics, d => d.Message.Contains("so it is cut", StringComparison.Ordinal) && d.Message.Contains("T (dlg.forest.hopper_01", StringComparison.Ordinal));
    }

    const string Bdf1 = "STARTFONT 2.1\nFONT x-tis620-0\nSIZE 8 75 75\nFONTBOUNDINGBOX 6 8 0 0\nSTARTPROPERTIES 1\nCHARSET_REGISTRY \"tis620\"\nENDPROPERTIES\nCHARS 3\n";

    static string BdfGlyph(int code, int advance, string row) =>
        $"STARTCHAR c{code}\nENCODING {code}\nSWIDTH 0 0\nDWIDTH {advance} 0\nBBX 6 2 0 0\nBITMAP\n{row}\n{row}\nENDCHAR\n";

    [Fact]
    public void Tis620Bdf_MapsToUnicode_AndBoldUsesTheEmphasisFont()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_bold"));
        SampleText.AddTo(p, "en");
        p.Settings.General.BootSplash = false;
        p.Settings.General.DefaultFont = "plain";
        p.Settings.Localization.Languages = [new LanguageInfo { Code = "en", Name = "English", Font = "plain" }];
        p.SaveSettings();
        File.Delete(Path.Combine(p.TextDir, "strings.th.json"));
        foreach (var (file, advance, row) in new[] { ("plain", 6, "F8"), ("heavy", 9, "FC") })
        {
            File.WriteAllText(Path.Combine(p.FontsDir, file + ".bdf"), Bdf1 + BdfGlyph(32, advance, "00") + BdfGlyph(0xA1, advance, row) + BdfGlyph(0xA2, advance, row) + "ENDFONT\n");
            GameProject.Save(Path.Combine(p.FontsDir, file + ".font"), new FontFile
            {
                Name = file, SourceKind = FontSourceKind.Bdf, Source = file + ".bdf", Size = 8, LineHeight = 16, Baseline = 12, Bpp = 1,
                CharacterSets = { "Thai" }, EmphasisFont = file == "plain" ? "heavy" : "",
            });
        }
        var table = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json"));
        table.Strings = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["a"] = "\u0E01<b>\u0E01\u0E02</b>\u0E02", ["hud.score"] = "\u0E01", ["dlg.forest.hopper_01"] = "\u0E01", ["ui.menu.start"] = "\u0E01" };
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), table);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var strings = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "text", "strings_en.cpp"));
        // Key "a": plain advance 6, then two heavy glyphs of advance 9 from the glyphs after the plain ones.
        Assert.Matches(@"\{\d+, 0, 0, 0, 0\},\{\d+, 6, 0, 0, \d+\},\{\d+, 15, 0, 0, \d+\},\{\d+, 24, 0, 0, \d+\}", strings);
        var fontCpp = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "text", "font_plain.cpp"));
        Assert.Contains("<b> U+0E01", fontCpp);
        Assert.Contains("U+0E02", fontCpp);
    }

    [Fact]
    public void ReadyMadePage_OpensByName_WithNoFileInTheProject()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_preset"));
        SampleText.AddTo(p, "en");
        p.Settings.General.BootSplash = false;
        p.Settings.Ui.PauseMenu = false;
        p.SaveSettings();
        Assert.Empty(p.UiPageFiles());
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Asker.cs"), """
            using GBA;
            using System.Threading.Tasks;

            public class Asker : Behaviour
            {
                void Update()
                {
                    if (Input.Pressed(Key.Select)) Run();
                }

                [Cutscene]
                static async Task Run()
                {
                    Pages.Open();
                    int r = await UI.Choose(0);
                    Pages.Report(r);
                }
            }

            public static class Pages
            {
                [ScriptNative] public static void Open() => UI.Open("TextInput");
                [ScriptNative]
                public static void Report(int r)
                {
                    Debug.Log($"typed {r} {UI.InputLength} {UI.InputChar(0)}");
                    UI.Close();
                }
            }
            """);
        var scene = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var s0 = GameProject.Load<SceneFile>(scene);
        s0.Actors.First(a => a.Name == "Player").Components.Add(new ComponentData { Type = "Asker" });
        GameProject.Save(scene, s0);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 90; i++) s.Step();
        Press(s, GbaKeys.Select);
        for (var i = 0; i < 6; i++) s.Step();
        Heard(s, "UI page TextInput opened");
        Press(s, GbaKeys.A);   // opens the keyboard
        Press(s, GbaKeys.A);   // types A
        Press(s, GbaKeys.Start);
        Heard(s, "typed 1 1 65");
    }

    [Fact]
    public void EveryPreset_BuildsAsAPage_AndIsSelfConsistent()
    {
        foreach (var name in UiPresets.Names)
        {
            var page = UiPresets.Create(name, new UiSettings())!;
            Assert.Equal(name, page.Name);
            var rects = UiLayoutEngine.Resolve(page);
            foreach (var e in page.Root.Walk())
            {
                var r = rects[e];
                Assert.True(r.X >= 0 && r.Y >= 0 && r.Right <= 240 && r.Bottom <= 160, $"{name}.{e.Name} leaves the screen: {r}");
            }
            Assert.Equal(page.Root.Walk().Count(), page.Root.Walk().Select(e => e.Name).Distinct().Count());
        }
    }

    [Fact]
    public void NewControls_RoundTrip_ThroughTheFile()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_page_json");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Controls.uipage");
        GameProject.Save(path, new UiPageFile
        {
            Name = "Controls", ScenePlays = true, Dim = 7,
            Root = new UiElement
            {
                Name = "Canvas",
                Children =
                {
                    new UiElement { Name = "D", Kind = UiElementKind.Dropdown, Options = { "a", "b" }, PaddingLeft = 3, DisabledTextColor = "#111111", ImageVariants = { "x" } },
                    new UiElement { Name = "I", Kind = UiElementKind.InputField, MaxLength = 5 },
                    new UiElement { Name = "B", Kind = UiElementKind.Scrollbar, ScrollTarget = "L" },
                    new UiElement { Name = "L", Kind = UiElementKind.ScrollRect },
                },
            },
        });
        var back = GameProject.Load<UiPageFile>(path);
        Assert.True(back.ScenePlays);
        Assert.Equal(7, back.Dim);
        Assert.Equal(["a", "b"], back.Root.Children[0].Options);
        Assert.Equal(3, back.Root.Children[0].PaddingLeft);
        Assert.Equal(["x"], back.Root.Children[0].ImageVariants);
        Assert.Equal(5, back.Root.Children[1].MaxLength);
        Assert.Equal("L", back.Root.Children[2].ScrollTarget);
        Assert.True(back.Root.Children[0].IsSelectable);
        Assert.True(back.Root.Children[1].IsSelectable);
        Assert.False(back.Root.Children[3].IsSelectable);
    }
}
