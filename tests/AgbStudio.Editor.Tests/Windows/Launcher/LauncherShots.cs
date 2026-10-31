using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Gallery;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Launcher;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Launcher;

/// <summary>The Launcher group's Figma fixture: four recent projects, tutorial progress and the toolchain line of the frames.</summary>
public static class LauncherFixture
{
    public static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0);

    /// <summary>Sky Hopper (2 h ago), Cave Quest RPG (yesterday), Snow Run (3 days ago) and Dusk Novel (last week) as real projects.</summary>
    public static string AddRecentProjects(Fixture fx)
    {
        var dir = Path.Combine(fx.Folder, "GBAProjects");
        foreach (var (name, when) in new[] { ("Dusk Novel", Now.AddDays(-9)), ("Snow Run", Now.AddDays(-3)), ("Cave Quest RPG", Now.AddDays(-1)), ("Sky Hopper", Now.AddHours(-2)) })
        {
            var p = GameProject.Create(Path.Combine(dir, GameProject.FileName(name)), name);
            fx.Now = when;
            fx.Services.Recent.Touch(name, p.ProjectPath);
        }
        fx.Now = Now;
        return dir;
    }

    /// <summary>"Your first platformer" at step 3 with two sub-steps ticked (40 %), "Palettes &amp; VRAM" done.</summary>
    public static void TutorialProgress(LauncherService l)
    {
        var s = l.Tutorials.Get("first-platformer");
        s.Started = true;
        s.Step = 2;
        s.Done = ["0.0", "0.1", "0.2", "1.0", "1.1", "1.2", "2.0", "2.1"];
        var p = l.Tutorials.Get("palettes");
        p.Started = p.Completed = true;
        l.Tutorials.Save();
    }

    public static LauncherService Launcher(Fixture fx)
    {
        var l = LauncherService.Of(fx.Services);
        // the running editor's version (Scope log: show 1.0.0, not Figma's 0.1) and the toolchain of the frames
        l.StatusLine = () => $"{EditorVersion.Short} · devkitARM 16.1 found";
        l.DevkitPro = @"D:\devkitPro";
        l.DeleteFolder = folder => { Directory.Delete(folder, true); return true; };
        l.OpenDocument = _ => { };
        l.Git = (_, _) => new AgbStudio.Build.ProcResult(0, "");
        return l;
    }
}

/// <summary>SHOT renders of the Launcher group (T-P12, T-P20, T-P21, T-O13, T-M17, T-D22, T-D01, T-L02, T-P22).</summary>
public sealed class LauncherShots : IShotSource
{
    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        foreach (var id in new[] { "T-P12", "T-P20", "T-P21" })
        {
            var page = id;
            yield return (page, (path, w, h) => RenderLauncher(page, path, w, h));
        }
        yield return ("T-O13", (path, w, h) =>
        {
            using var fx = new Fixture();
            var l = LauncherFixture.Launcher(fx);
            LauncherFixture.TutorialProgress(l);
            l.TutorialHost.Session.Start(TutorialCatalog.All[0]);
            var view = new TutorialStepsPopover(l.TutorialHost.Session);
            Capture.ControlToPng(view, w, h, path, Brushes.Black);
            l.TutorialHost.Session.Stop();
        });
        yield return ("T-M17", (path, w, h) =>
        {
            using var fx = new Fixture(withApp: false);
            var builder = new MenuBuilder(fx.Services.Commands, fx.Services.Shortcuts);
            Capture.ControlToPng(new MenuSurface { ItemsSource = builder.BuildItems(ProjectContextMenu.Entries()) }, w, h, path, Brushes.Black);
        });
        yield return ("T-D22", (path, w, h) =>
        {
            using var fx = new Fixture();
            LauncherFixture.Launcher(fx);
            var p = AgbStudio.Build.Content.SkyHopper.Create(Path.Combine(fx.Folder, "SkyHopper"));
            var scan = ProjectScan.Read(p.ProjectPath);
            var dlg = new OpeningProjectDialog(fx.Services, p.ProjectPath, autoStart: false);
            var at = Math.Min(29, scan.Assets.Count);
            dlg.SetStep(0, OpenStepState.Done, $"{Path.GetFileName(p.ProjectPath)} · {scan.Scenes} scenes");
            dlg.SetStep(1, OpenStepState.Running, $"{at} / {scan.Assets.Count} · {Path.GetFileName(scan.Assets[at - 1])}", 100.0 * at / scan.Assets.Count);
            dlg.SetStep(2, OpenStepState.Pending, $"{scan.Scripts} C# scripts");
            dlg.SetStep(3, OpenStepState.Pending, scan.LastScene);
            Capture.ControlToPng(dlg, w, h, path, Brushes.Black);
        });
        yield return ("T-D01", (path, w, h) => RenderNewProject(path, w, h, ThemeVariant.Dark));
        yield return ("T-L02", (path, w, h) => RenderNewProject(path, w, h, ThemeVariant.Light));
        yield return ("T-P22", RenderEmptyProject);
    }

    static void RenderLauncher(string page, string path, int w, int h)
    {
        using var fx = new Fixture();
        var l = LauncherFixture.Launcher(fx);
        LauncherFixture.AddRecentProjects(fx);
        LauncherFixture.TutorialProgress(l);
        var win = new MainWindow(fx.Services, page) { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            Capture.Pump();
            Capture.Save(Capture.Frame(win), path);
        }
        finally { win.Close(); }
    }

    public static NewProjectDialog NewProjectFixture(Fixture fx)
    {
        LauncherFixture.Launcher(fx);
        var dlg = new NewProjectDialog(fx.Services);
        dlg.NameField.Text = "Sky Hopper";
        dlg.SetLocationBase(@"D:\GBAProjects");
        return dlg;
    }

    static void RenderNewProject(string path, int w, int h, ThemeVariant theme)
    {
        using var fx = new Fixture();
        var dlg = NewProjectFixture(fx);
        GalleryCatalog.Force(dlg.NameField, ":focus", "PART_Box");
        var app = Avalonia.Application.Current!;
        var before = app.RequestedThemeVariant;
        try
        {
            app.RequestedThemeVariant = theme;
            Capture.ControlToPng(dlg, w, h, path, Brushes.Black, theme);
        }
        finally { app.RequestedThemeVariant = before; }
    }

    static void RenderEmptyProject(string path, int w, int h)
    {
        using var fx = new Fixture();
        var l = LauncherFixture.Launcher(fx);
        var created = ProjectCreator.Create(new NewProjectOptions
        {
            Template = TemplateCatalog.Find("puzzle"), Name = "My Game", Folder = Path.Combine(fx.Folder, "MyGame"), RomTitle = "MYGAME", GameCode = "MYGE",
            SampleLevel = false, ThaiFont = false, LogicGraphExamples = false, InitGit = false,
        });
        var p = GameProject.Open(created.ProjectPath);
        // the frame shows the ROM line of one earlier build of the empty project
        Directory.CreateDirectory(p.BuildDir);
        new AgbStudio.Build.Pipeline.BuildReport { RomBytes = 18 * 1024 }.Save(p.BuildDir);
        var win = new MainWindow(fx.Services, null) { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            l.CompleteOpen(created.ProjectPath, ProjectCreator.StartScene);
            Capture.UndoHistory(fx.Services);
            Capture.Save(Capture.Frame(win), path);
        }
        finally { win.Close(); }
    }
}
