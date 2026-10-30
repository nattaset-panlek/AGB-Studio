using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Audio;
using AgbStudio.Editor.Windows.Build;
using AgbStudio.Editor.Windows.Import;
using Avalonia.Media;

namespace AgbStudio.Editor.Tests.Windows.Build;

/// <summary>
/// SHOT renders of the Audio, SFX Designer, Import and Build windows (T-P15, T-P25, T-D04, T-D24, T-D06, T-D23, T-P11,
/// T-D05, T-D07) at the Figma size, in the state the Figma frames show, with the Sky Hopper fixture content.
/// </summary>
public sealed class GroupShots : IShotSource
{
    static readonly DateTime At = new(2026, 10, 31, 12, 6, 2);

    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P15", (path, w, h) => Page(path, "T-P15", fx =>
        {
            var page = (AudioPage)fx.CurrentPage;
            page.Seek(7, 20);
        }));
        yield return ("T-P25", (path, w, h) => Page(path, "T-P25", _ => { }, args: "jump.sfx"));
        yield return ("T-P11", (path, w, h) => Page(path, "T-P11", _ => { }, report: true));
        yield return ("T-D06", (path, w, h) => Dialog(path, w, h, fx =>
        {
            var d = new BuildRomDialog(fx.Services, start: false, clock: Clock());
            DriveRunning(d.Stream);
            return d;
        }, history: true));
        yield return ("T-D23", (path, w, h) => Dialog(path, w, h, fx => new BuildFailedDialog(fx.Services, Failed(fx))));
        yield return ("T-D05", (path, w, h) => Dialog(path, w, h, fx =>
        {
            var d = new ExportRomDialog(fx.Services);
            d.Folder.Selected = @"D:\GBAProjects\SkyHopper\build";
            return d;
        }, report: true, romKb: 398));
        yield return ("T-D07", (path, w, h) =>
        {
            // the Figma moment: writing, 128 of 412 KB
            using var fx = new Fixture();
            GroupFixture.WriteReport(GroupFixture.SkyHopper(fx));
            var dev = new FakeCartridge { PauseAt = 128 * 1024 };
            var d = new FlashDialog(fx.Services, new FakeDevices(dev));
            var host = Capture.Host(d, w, h, Brushes.Black);
            try
            {
                _ = d.FlashAsync();
                GroupFixture.PumpUntil(() => d.ProgressText.StartsWith("128 /", StringComparison.Ordinal), 5000);
                Capture.Save(Capture.Frame(host), path);
            }
            finally
            {
                dev.Gate.TrySetCanceled();
                host.Close();
            }
        });
        yield return ("T-D04", (path, w, h) => Dialog(path, w, h, fx => new ImportAssetDialog(fx.Services, GroupFixture.ImportFiles(fx, fx.Services.Project.Current!))));
        yield return ("T-D24", (path, w, h) => Dialog(path, w, h, fx =>
        {
            var items = GroupFixture.ProblemFiles(fx, fx.Services.Project.Current!).Select(f => AssetImporter.Inspect(f, fx.Services.Project.Current)).ToList();
            return new ImportErrorsDialog(fx.Services, items);
        }));
    }

    static Func<DateTime> Clock()
    {
        var times = new Queue<DateTime>([At, At, At.AddSeconds(1)]);
        return () => times.Count > 1 ? times.Dequeue() : times.Peek();
    }

    /// <summary>The Figma moment of T-D06: scripts and assets done, compiling file 9 of 12, Link waiting.</summary>
    public static void DriveRunning(BuildStream s)
    {
        s.StepStarted(GameBuilder.StepAssets, "");
        s.StepFinished(GameBuilder.StepAssets, TimeSpan.FromSeconds(0.18), true, "8 sprites · 3 tilesets · 2 fonts · 9 sounds");
        s.StepStarted(GameBuilder.StepScripts, "");
        s.StepFinished(GameBuilder.StepScripts, TimeSpan.FromSeconds(0.21), true, "12 scripts");
        s.StepStarted(GameBuilder.StepCompile, "");
        s.Line(@"[8/14] CXX D:\GBAProjects\SkyHopper\build\gen\scripts\PlayerController.cpp");
        s.Message(new Diagnostic(Severity.Warning, "PlayerController.cs", 8, 5, "GAE002", "float → use fixed"));
        s.Line(@"[9/14] CXX D:\GBAProjects\SkyHopper\build\gen\scripts\Slime.cpp");
        s.FileProgress(GameBuilder.StepCompile, 9, 12, "PlayerController.cpp");
    }

    /// <summary>The Figma moment of T-D23: Compile failed at PlayerController.cpp with two errors and a warning.</summary>
    public static BuildFailure Failed(Fixture fx)
    {
        var t = new DateTime(2026, 10, 31, 12, 31, 8);
        var s = new BuildStream(() => t);
        s.StepStarted(GameBuilder.StepAssets, "");
        s.StepFinished(GameBuilder.StepAssets, TimeSpan.FromSeconds(0.18), true, "8 sprites · 3 tilesets · 2 fonts · 9 sounds");
        s.StepStarted(GameBuilder.StepScripts, "");
        s.StepFinished(GameBuilder.StepScripts, TimeSpan.FromSeconds(0.21), true, "12 scripts");
        s.StepStarted(GameBuilder.StepCompile, "");
        s.FileProgress(GameBuilder.StepCompile, 3, 12, "PlayerController.cpp");
        var errors = new[]
        {
            new Diagnostic(Severity.Error, "PlayerController.cs", 22, 9, "", "';' expected"),
            new Diagnostic(Severity.Error, "Slime.cs", 14, 13, "GAE003", "new inside Update — allocate in Start"),
            new Diagnostic(Severity.Warning, "PlayerController.cs", 8, 5, "GAE002", "float → use fixed"),
        };
        foreach (var e in errors) s.Message(e);
        s.StepFinished(GameBuilder.StepCompile, TimeSpan.FromSeconds(0.3), false, "failed");
        var r = new BuildResult();
        r.Diagnostics.AddRange(errors);
        s.Complete(r);
        _ = fx;
        return s.Failure("sky_hopper.gba");
    }

    static void Page(string path, string id, Action<(Fixture Fx, object CurrentPage)> setup, bool report = false, object? args = null)
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        if (report) GroupFixture.WriteReport(p);
        var win = GroupFixture.Show(fx, id, args);
        try
        {
            GroupFixture.UndoRedoEnabled(fx.Services);
            setup((fx, win.CurrentPage!));
            Capture.Save(Capture.Frame(win), path);
        }
        finally { win.Close(); }
    }

    static void Dialog(string path, int w, int h, Func<Fixture, Avalonia.Controls.Control> create, bool report = false, long romKb = 412, bool history = false)
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        if (report || history) GroupFixture.WriteReport(p, romKb);
        Capture.ControlToPng(create(fx), w, h, path, Brushes.Black);
    }
}
