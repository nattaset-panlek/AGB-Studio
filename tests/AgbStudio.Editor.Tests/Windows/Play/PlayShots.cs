using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Debugger;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;
using Avalonia.Controls;
using Avalonia.Media;

namespace AgbStudio.Editor.Tests.Windows.Play;

/// <summary>
/// SHOT renders of Play Mode (T-P02), Play Speed (T-M18) and the Debugger (T-P19, T-P23, T-P24) with the Sky Hopper
/// fixture running, and P12.05: the Play Mode game view with the Hitboxes overlay on (Tile collision off), cropped to
/// the Game View rectangle of the Figma frame (docs/evidence/figma/T-P02-game-view.json).
/// </summary>
public sealed class PlayShots : IShotSource, IShotSpecimen
{
    public IEnumerable<(string Id, string Set, string FigmaPng, Action<string> Render)> Specimens()
    {
        yield return ("T-P02-game-view", "Play Mode game view · Hitboxes on, Tile collision off", Path.Combine(Repo.FigmaEvidence, "T-P02-game-view.png"), path =>
        {
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "T-P02-game-view.json")));
            int Get(string k) => (int)Math.Round(json.RootElement.GetProperty(k).GetDouble());
            var full = Path.Combine(Path.GetDirectoryName(path)!, "T-P02-overlays-full.png");
            // the overlays as the Figma Game View draws them: hitboxes (with the velocity arrows) on, tile collision off
            RenderPage(full, 1440, 900, "T-P02", m => { m.Play.Hitboxes = true; m.Play.TileCollision = false; Jump(m.Play); });
            using (var src = SkiaSharp.SKBitmap.Decode(full))
            using (var crop = new SkiaSharp.SKBitmap(Get("w"), Get("h")))
            {
                using (var canvas = new SkiaSharp.SKCanvas(crop))
                    canvas.DrawBitmap(src, SkiaSharp.SKRect.Create(Get("x"), Get("y"), Get("w"), Get("h")), SkiaSharp.SKRect.Create(0, 0, Get("w"), Get("h")));
                using var data = crop.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(path, data.ToArray());
            }
            File.Delete(full);
        });
    }

    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P02", (path, w, h) => RenderPage(path, w, h, "T-P02", m => Jump(m.Play)));
        yield return ("T-M18", (path, w, h) =>
        {
            // the speed chip's menu while the game runs (Frame step is enabled then)
            using var fx = new Fixture();
            var play = PlayTestSupport.Open(fx);
            Assert.True(play.Run(PlayTestSupport.Built.RomPath));
            play.RunFrames(2);
            var entries = PlayCommands.PlaySpeedMenu(new WindowContext { Services = fx.Services, Info = fx.Services.Windows.Info("T-M18") });
            var builder = new MenuBuilder(fx.Services.Commands, fx.Services.Shortcuts);
            Capture.ControlToPng(new MenuSurface { ItemsSource = builder.BuildItems(entries) }, w, h, path, Brushes.Black);
            play.Dispose();
        });
        yield return ("T-P19", (path, w, h) => RenderPage(path, w, h, "T-P19", PauseAtBreakpoint));
        yield return ("T-P23", (path, w, h) => RenderPage(path, w, h, "T-P23", PauseAtBreakpoint));
        yield return ("T-P24", (path, w, h) => RenderPage(path, w, h, "T-P24", m => { PauseAtBreakpoint(m); m.SelectedColour = 16 + 7; }));
    }

    /// <summary>
    /// The game frame of the Figma frames (T-P02 "Frame 2841", T-P19 / T-P23 / T-P24 "Paused at frame 2841"). The toolbar
    /// centres the play controls between its chips, so a shorter frame number would move them.
    /// </summary>
    const long FigmaFrame = 2841;

    static void RunToFrame(PlayController play, long frame)
    {
        for (var left = frame - play.Frame; left > 0; left = frame - play.Frame) play.RunFrames((int)left);
    }

    /// <summary>The player a few frames into a jump to the right at the Figma frame ("Player · state Jump", velocity arrow).</summary>
    static void Jump(PlayController play)
    {
        RunToFrame(play, FigmaFrame - 6);
        play.KeyDown(Avalonia.Input.Key.Right);
        play.KeyDown(Avalonia.Input.Key.Z);
        RunToFrame(play, FigmaFrame);
    }

    /// <summary>The Figma breakpoints; the player jumps so the game stops at PlayerController.cs:19 at the Figma frame.</summary>
    static void PauseAtBreakpoint(DebuggerModel m)
    {
        RunToFrame(m.Play, FigmaFrame - 3);
        m.AddBreakpoint("PlayerController.cs", 19);
        m.AddBreakpoint("Slime.cs", 42, enabled: false);
        m.AddSceneLoad(enabled: false);
        var play = m.Play;
        play.RunFrames(2);
        play.KeyDown(Avalonia.Input.Key.Z);
        for (var i = 0; i < 20 && !m.AtBreakpoint; i++) play.RunFrames(1);
        play.KeyUp(Avalonia.Input.Key.Z);
        play.RunFrames(3);
    }

    static void RenderPage(string path, int w, int h, string id, Action<DebuggerModel>? setup)
    {
        using var fx = new Fixture();
        var play = PlayTestSupport.Open(fx);
        fx.GameLanguage("en");   // the Figma game view shows the English HUD ("SCORE"); Play builds with it
        var win = new MainWindow(fx.Services) { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            Assert.True(PlayTestSupport.Wait(play.StartAsync(PlayStart.FromHere)));
            PlayTestSupport.RunUntilScene(play, "Forest_01", 300);
            play.RunFrames(90);
            var model = DebuggerModel.Of(fx.Services);
            setup?.Invoke(model);
            _ = fx.Services.Windows.Open(id, source: "shot");
            play.Publish();
            Capture.UndoHistory(fx.Services);
            Capture.Save(Capture.Frame(win), path);
        }
        finally
        {
            win.Close();
            play.Dispose();
        }
    }
}
