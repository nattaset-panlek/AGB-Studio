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

/// <summary>SHOT renders of Play Mode (T-P02), Play Speed (T-M18) and the Debugger (T-P19, T-P23, T-P24) with the Sky Hopper fixture running.</summary>
public sealed class PlayShots : IShotSource
{
    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P02", (path, w, h) => RenderPage(path, w, h, "T-P02", null));
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

    /// <summary>The Figma breakpoints; the player jumps so the game stops at PlayerController.cs:19.</summary>
    static void PauseAtBreakpoint(DebuggerModel m)
    {
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
            Capture.Save(Capture.Frame(win), path);
        }
        finally
        {
            win.Close();
            play.Dispose();
        }
    }
}
