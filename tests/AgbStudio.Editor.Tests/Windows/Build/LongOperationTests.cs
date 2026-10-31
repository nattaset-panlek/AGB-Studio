using System.Diagnostics;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Play;
using AgbStudio.Editor.Windows.Build;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Editor.Windows.SceneEditor;
using AgbStudio.Emu;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Build;

/// <summary>
/// G-ERR.04: a build can be cancelled (closing Build ROM while it builds) and leaves the project consistent: the next
/// build succeeds. G-PERF.04: the UI thread stays responsive while a real build runs and while the game plays.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class LongOperationTests
{
    /// <summary>Runs the UI thread until <paramref name="done"/>, moving the pointer like a user; returns the slowest turn.</summary>
    static TimeSpan PumpResponsive(Avalonia.Controls.Window win, Func<bool> done, int timeoutMs)
    {
        var worst = TimeSpan.Zero;
        var total = Stopwatch.StartNew();
        var x = 0;
        while (!done() && total.ElapsedMilliseconds < timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            win.MouseMove(new Point(400 + (x++ % 200), 300));
            Dispatcher.UIThread.RunJobs();
            if (sw.Elapsed > worst) worst = sw.Elapsed;
            Thread.Sleep(5);
        }
        return worst;
    }

    [AvaloniaFact]
    public void Build_CancelledFromBuildRom_LeavesTheProjectConsistent_UiStaysResponsive()
    {
        if (!new Toolchain().IsComplete) { Assert.Fail("devkitPro toolchain not found"); return; }
        using var fx = new Fixture();
        var dir = Path.Combine(Repo.Root, "build", "editor-tests", "cancel");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        SampleProjects.CreatePlatformerMini(dir);
        fx.Services.Project.Open(dir);
        fx.Services.ProjectSettings.Settings.Build.RunAfterBuild = false;
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        try
        {
            fx.Services.Commands.Execute("build.buildRom");
            Capture.Pump();
            var d = Assert.IsType<BuildRomDialog>(win.Overlays.Open.Single());
            // G-PERF.04: while the build converts and compiles on its own thread, every UI turn stays short
            var worst = PumpResponsive(win, () => d.Stream.Step(GameBuilder.StepCompile).State == StepState.Running || d.Completion.IsCompleted, 600_000);
            Assert.False(d.Completion.IsCompleted, "the build ended before the compile step could be cancelled");
            Assert.True(worst < TimeSpan.FromMilliseconds(250), $"UI thread blocked for {worst.TotalMilliseconds:0} ms during the build");
            // G-ERR.04: Close while it builds stops the build
            var close = d.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Close");
            var p = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), win)!.Value;
            win.MouseDown(p, MouseButton.Left);
            win.MouseUp(p, MouseButton.Left);
            GroupFixture.PumpUntil(() => d.Completion.IsCompleted, 120_000);
            var r = d.Completion.Result!;
            Assert.False(r.Success);
            Assert.Contains(r.Diagnostics, x => x.Message == "Build cancelled.");
            Assert.False(fx.Services.Build.IsBuilding);
            Assert.Equal("Build cancelled", fx.Services.Status.Build);
            Assert.DoesNotContain(fx.Services.Windows.Requests, q => q.Window == "T-D23");
            Assert.Contains(ConsoleLog.Of(fx.Services).Entries, e => e.Level == ConsoleLevel.Warning && e.Text.StartsWith("Build cancelled", StringComparison.Ordinal));
            Assert.False(win.Overlays.IsOpen);
            // the project is consistent: the next build finishes and writes the ROM
            fx.Services.Commands.Execute("build.buildRom");
            Capture.Pump();
            d = Assert.IsType<BuildRomDialog>(win.Overlays.Open.Single());
            GroupFixture.PumpUntil(() => d.Completion.IsCompleted, 600_000);
            Assert.True(d.Completion.Result!.Success, string.Join('\n', d.Completion.Result.Diagnostics));
            Assert.True(File.Exists(d.Completion.Result.RomPath));
        }
        finally { win.Close(); }
    }

    [AvaloniaFact]
    public void Play_TheGameTicksLeaveTheUiThreadFree()
    {
        using var fx = new Fixture();
        var play = PlayTestSupport.Open(fx);
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        try
        {
            PlayTestSupport.RunForest(play);
            _ = fx.Services.Windows.Open("T-P02", source: "test");
            Capture.Pump();
            // G-PERF.04: a second of play at 59.73 fps, with pointer input between the frames
            var ticks = new List<double>();
            var worst = TimeSpan.Zero;
            for (var i = 0; i < 60; i++)
            {
                var sw = Stopwatch.StartNew();
                play.Tick(TimeSpan.FromSeconds(1 / PlaySession.Fps));
                ticks.Add(sw.Elapsed.TotalMilliseconds);
                var input = Stopwatch.StartNew();
                win.MouseMove(new Point(300 + i, 400));
                Dispatcher.UIThread.RunJobs();
                if (input.Elapsed > worst) worst = input.Elapsed;
            }
            Assert.True(ticks.Average() < 16.7, $"a game frame takes {ticks.Average():0.0} ms of the UI thread on average");
            Assert.True(ticks.Max() < 100, $"the slowest game frame held the UI thread {ticks.Max():0} ms");
            Assert.True(worst < TimeSpan.FromMilliseconds(100), $"input waited {worst.TotalMilliseconds:0} ms");
        }
        finally
        {
            play.Dispose();
            win.Close();
        }
    }
}
