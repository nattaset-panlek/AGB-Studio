using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>Menu / UI kit in a built game (Figma "In-game" screens): boot splash, title scene menu, options
/// screen with language and remap, pause overlay. Frames are written to the build folder for the SHOT check.</summary>
[Collection("emulator")]
public sealed class UiKitTests(EmulatorFixture emu)
{
    static GameProject CreateProject()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_kit"));
        SampleText.AddTo(p, "en");
        p.Settings.General.StartScene = "Title";
        p.Settings.General.BootSplash = true;
        p.Settings.General.SplashSeconds = 1;
        p.Settings.General.SkipSplashInDebug = false;
        p.Settings.AllowRemap = true;
        p.SaveSettings();
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), new SceneFile
        {
            Name = "Title", Width = 15, Height = 10, Backdrop = "#78BEFA",
            Menu = new SceneMenu
            {
                X = 88, Y = 72, Width = 96, Height = 48, Spacing = 14,
                Items =
                {
                    new SceneMenuItem { Text = "ui.menu.start", Command = MenuCommand.LoadScene, Scene = "Forest_01" },
                    new SceneMenuItem { Text = "Continue", Command = MenuCommand.Continue },
                    new SceneMenuItem { Text = "Options", Command = MenuCommand.Options },
                },
            },
        });
        return p;
    }

    static void Press(PlaySession s, GbaKeys key)
    {
        s.Keys = key;
        s.Step();
        s.Keys = 0;
        for (var i = 0; i < 3; i++) s.Step();
    }

    static (int R, int G, int B) Pixel(LibretroCore core, int x, int y)
    {
        var c = core.Frame[y * LibretroCore.Width + x];
        return ((int)(c >> 16) & 255, (int)(c >> 8) & 255, (int)c & 255);
    }

    [Fact]
    public void SplashTitleOptionsAndPauseInABuiltGame()
    {
        var p = CreateProject();
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var shots = Path.Combine(p.BuildDir, "ui-shots");
        Directory.CreateDirectory(shots);
        void Shot(PlaySession s, string name) => AgbStudio.Core.Png.WriteArgb(Path.Combine(shots, name + ".png"), (uint[])s.Core.Frame.Clone(), 240, 160);

        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 35; i++) s.Step();
        Shot(s, "boot-splash");
        var centre = Pixel(s.Core, 120, 66);
        Assert.True(centre.B > 150 && centre.R < 100, $"splash centre {centre}");  // the indigo square of the Plus Pad mark
        var arm = Pixel(s.Core, 120, 50);
        Assert.True(arm.R > 180 && arm.G > 180, $"splash arm {arm}");

        // Splash 60 frames, then the title scene loads and fades in.
        for (var i = 0; i < 70; i++) s.Step();
        Shot(s, "title-screen");
        Near((120, 190, 250), Pixel(s.Core, 4, 4));  // the title backdrop, not the splash
        Assert.DoesNotContain(s.Actors(), a => a.Name == "Player");

        // Options from the title menu: Continue is disabled, so Down goes straight to Options.
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.A);
        for (var i = 0; i < 4; i++) s.Step();
        Assert.Contains(s.Log, l => l.Contains("Options opened", StringComparison.Ordinal));
        Shot(s, "options");
        Near((30, 26, 40), Pixel(s.Core, 2, 2));  // options screen colour #1E1A28
        // Language row: switch th <-> en; then remap Jump (row 3 + heading) to R.
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.Right);
        Press(s, GbaKeys.B);
        Assert.Contains(s.Log, l => l.Contains("Options closed", StringComparison.Ordinal));

        // Start the game.
        Press(s, GbaKeys.Up);
        Press(s, GbaKeys.A);
        for (var i = 0; i < 60; i++) s.Step();
        Assert.Contains(s.Actors(), a => a.Name == "Player");

        // Pause overlay over the running scene.
        Press(s, GbaKeys.Start);
        for (var i = 0; i < 3; i++) s.Step();
        Assert.Contains(s.Log, l => l.Contains("Pause menu opened", StringComparison.Ordinal));
        Shot(s, "pause-menu");
        var paper = Pixel(s.Core, 120, 110);
        Assert.True(paper.R > 230 && paper.G > 230, $"pause box paper {paper}");
        var dimmed = Pixel(s.Core, 4, 4);
        Assert.True(dimmed.B < 150, $"scene behind the pause box is darkened: {dimmed}");
        Press(s, GbaKeys.Start);
        Assert.Contains(s.Log, l => l.Contains("Pause menu closed", StringComparison.Ordinal));
        for (var i = 0; i < 4; i++) s.Step();
        Near((120, 190, 250), Pixel(s.Core, 4, 4));
        s.Stop();
    }

    // GBA colours are 5 bits per channel: a design colour comes back within about 12 of each channel.
    static void Near((int R, int G, int B) want, (int R, int G, int B) got) =>
        Assert.True(Math.Abs(want.R - got.R) <= 12 && Math.Abs(want.G - got.G) <= 12 && Math.Abs(want.B - got.B) <= 12, $"expected about {want}, got {got}");
}
