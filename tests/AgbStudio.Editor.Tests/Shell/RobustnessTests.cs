using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Import;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace AgbStudio.Editor.Tests.Shell;

/// <summary>G-ERR.06 crash handler, G-ERR.03 failed imports in the Console, P10.15 the start-up choice.</summary>
public sealed class RobustnessTests : IDisposable
{
    readonly Fixture _fx = new();
    public void Dispose() => _fx.Dispose();

    // G-ERR.06: a forced crash saves the changed documents and writes a crash log; the editor keeps running
    [AvaloniaFact]
    public void ForcedCrash_SavesTheWorkAndWritesACrashLog()
    {
        _fx.OpenSkyHopper();
        var win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        var saved = new List<string>();
        var scene = _fx.Services.Documents.Open[0];
        scene.SaveHandler = d => { saved.Add(d.Name); return Task.CompletedTask; };
        scene.IsDirty = true;
        var folder = Path.Combine(_fx.Folder, "crash");
        using var guard = new CrashGuard(_fx.Services, folder, () => new DateTime(2026, 10, 31, 12, 30, 0));
        guard.Install();
        try
        {
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("forced crash for the test"));
            Capture.Pump();
            Assert.Equal(["Scene"], saved);
            Assert.False(scene.IsDirty);
            var log = Path.Combine(folder, "crash-20261031-123000.log");
            Assert.Equal(log, guard.LastLog);
            var text = File.ReadAllText(log);
            Assert.Contains("forced crash for the test", text);
            Assert.Contains("Saved before the crash: Scene", text);
            Assert.Contains(_fx.Services.Project.Current!.ProjectPath, text);
            Assert.Contains(_fx.Services.Toasts.Visible, t => t.Status == ToastStatus.Error && t.Text.Contains("saved your changes"));
            // the editor is still usable
            Assert.True(win.IsVisible);
            _fx.Services.Commands.Execute("edit.commandPalette");
            Capture.Pump();
            Assert.IsType<CommandPalette>(win.Overlays.Open.Single());
            // a background-thread crash writes its log too
            guard.Handle(new IOException("disk error on a worker thread"));
            Assert.Contains("disk error on a worker thread", File.ReadAllText(guard.LastLog!));
        }
        finally
        {
            guard.Dispose();
            win.Close();
        }
    }

    // G-ERR.03: an import that fails reaches the Console with the file and the reason
    [AvaloniaFact]
    public void FailedImport_ReachesTheConsole()
    {
        _fx.OpenSkyHopper();
        var broken = Path.Combine(_fx.Folder, "broken.png");
        File.WriteAllBytes(broken, [1, 2, 3, 4]);
        var good = Path.Combine(_fx.Folder, "beep.wav");
        File.Copy(Directory.EnumerateFiles(_fx.Services.Project.Current!.Root, "*.wav", SearchOption.AllDirectories).First(), good);
        var items = new[] { AssetImporter.Inspect(broken, _fx.Services.Project.Current), AssetImporter.Inspect(good, _fx.Services.Project.Current) };
        Assert.False(items[0].IsReady);
        var log = ConsoleLog.Of(_fx.Services);
        var import = ImportErrorsDialog.ImportReadyAsync(_fx.Services, items);
        AgbStudio.Editor.Tests.Windows.Build.GroupFixture.PumpUntil(() => import.IsCompleted, 30_000);
        Assert.True(import.IsCompletedSuccessfully);
        var line = Assert.Single(log.Entries, e => e.Text.StartsWith("Import: broken.png was not imported: ", StringComparison.Ordinal));
        Assert.Equal(ConsoleLevel.Warning, line.Level);
        Assert.DoesNotContain(log.Entries, e => e.Text.Contains("beep.wav"));
    }

    // P10.15: the start-up choice (Preferences › Editor "Open last project on start")
    [AvaloniaFact]
    public void Startup_OpensTheLastProjectOrTheLauncher()
    {
        var root = Path.Combine(_fx.Folder, "SkyHopper");
        AgbStudio.Build.Content.SkyHopper.Create(root);
        var project = Directory.EnumerateFiles(root, "*.agbproj").Single();
        var s = _fx.Services;
        // no last project: the Launcher
        Assert.Equal("T-P12", Startup.Choose(s));
        Assert.Null(s.Project.Current);
        // last project, preference on: it opens in the Scene Editor
        s.Preferences.Update(p => { p.LastProject = project; p.OpenLastProjectOnStart = true; });
        Assert.Equal("T-P01", Startup.Choose(s));
        Assert.Equal(Path.GetFullPath(project), Path.GetFullPath(s.Project.Current!.ProjectPath));
        s.Project.Close();
        // preference off: the Launcher, nothing opened
        s.Preferences.Update(p => p.OpenLastProjectOnStart = false);
        Assert.Equal("T-P12", Startup.Choose(s));
        Assert.Null(s.Project.Current);
        // the project is gone: the Launcher
        s.Preferences.Update(p => { p.OpenLastProjectOnStart = true; p.LastProject = Path.Combine(_fx.Folder, "Gone", "Gone.agbproj"); });
        Assert.Equal("T-P12", Startup.Choose(s));
        Assert.Null(s.Project.Current);
    }
}
