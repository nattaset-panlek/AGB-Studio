using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Launcher;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Launcher;

/// <summary>Opening Project (P11.06): the four steps with real counts, Cancel leaves no half-open project, a full open.</summary>
public class OpeningProjectTests
{
    /// <summary>A small project with a scene, a sprite, a tileset, a map and one script.</summary>
    static string MakeProject(string folder, string name = "Cave Quest")
    {
        var p = GameProject.Create(folder, name);
        ProjectCreator.AddSampleLevel(p);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Hello.cs"), "using GBA;\n\npublic class Hello : Behaviour\n{\n    public int count;\n\n    public override void Update() { count = count + 1; }\n}\n");
        p.Settings.LastScene = ProjectCreator.SampleScene;
        p.SaveSettings();
        return p.ProjectPath;
    }

    [AvaloniaFact]
    public async Task P11_06_CancelDuringLoadAssets_LeavesTheOpenProjectAsItWas()
    {
        using var h = new LauncherHarness(startPage: null, recent: false);
        var sky = h.Fx.OpenSkyHopper();
        h.S.Preferences.Update(p => p.LastProject = h.S.Project.Current!.ProjectPath);
        h.Show("T-P01");
        var other = MakeProject(Path.Combine(h.Fx.Folder, "CaveQuest"));
        var before = (Project: h.S.Project.Current, Docs: h.S.Documents.Open.ToList(), Recent: h.S.Recent.Items.ToList(), Last: h.S.Preferences.Current.LastProject, Page: h.Win.CurrentPage);

        var hold = new TaskCompletionSource();
        var reached = new List<string>();
        h.Launcher.BeforeOpenStep = async (step, ct) =>
        {
            reached.Add(step);
            if (step == "Load assets") await hold.Task.WaitAsync(ct);
        };
        // File › Open Recent routes here too (MainWindow opens T-D22 with the path)
        var open = h.Launcher.OpenAsync(other, "OpenRecent");
        await LauncherHarness.Until(() => reached.Contains("Load assets"));
        var dlg = Assert.IsType<OpeningProjectDialog>(h.Win.Overlays.Open.Single());
        Assert.Equal(480, dlg.Bounds.Width);
        Assert.Equal("Opening Cave Quest…", dlg.GetVisualDescendants().OfType<DialogHeader>().Single().Title);
        Assert.Equal([OpenStepState.Done, OpenStepState.Running, OpenStepState.Pending, OpenStepState.Pending], dlg.States);
        Assert.Equal("CaveQuest.agbproj · 1 scene", dlg.Detail(0));
        var scan = ProjectScan.Read(other);
        Assert.Equal($"0 / {scan.Assets.Count}", dlg.Detail(1));
        Assert.Equal("1 C# script", dlg.Detail(2));
        Assert.Equal("Forest_01", dlg.Detail(3));
        Assert.True(dlg.GetVisualDescendants().OfType<AgbProgress>().Single(b => b.Name == "AssetProgress").IsVisible);

        h.Click(dlg.GetVisualDescendants().OfType<AgbButton>().Single(b => b.Name == "OpenCancel"));
        Assert.False(h.Win.Overlays.IsOpen);
        Assert.False(await open);
        hold.SetResult();
        await Task.Delay(200);
        Capture.Pump();

        // nothing of the other project reached the editor
        Assert.Same(before.Project, h.S.Project.Current);
        Assert.Equal(before.Docs, h.S.Documents.Open);
        Assert.Equal(before.Recent, h.S.Recent.Items);
        Assert.Equal(before.Last, h.S.Preferences.Current.LastProject);
        Assert.Same(before.Page, h.Win.CurrentPage);
        Assert.Equal(["Read project", "Load assets"], reached);
        Assert.Equal(sky, h.S.Project.Current!.Root);

        // Esc while it compiles is a cancel as well
        hold = new TaskCompletionSource();
        reached.Clear();
        h.Launcher.BeforeOpenStep = async (step, ct) =>
        {
            reached.Add(step);
            if (step == "Compile scripts") await hold.Task.WaitAsync(ct);
        };
        open = h.Launcher.OpenAsync(other, "test");
        await LauncherHarness.Until(() => reached.Contains("Compile scripts"));
        dlg = (OpeningProjectDialog)h.Win.Overlays.Open.Single();
        Assert.Equal([OpenStepState.Done, OpenStepState.Done, OpenStepState.Running, OpenStepState.Pending], dlg.States);
        Assert.Equal($"{scan.Assets.Count} / {scan.Assets.Count}", dlg.Detail(1));
        h.Key(PhysicalKey.Escape);
        Assert.False(await open);
        Assert.Same(before.Project, h.S.Project.Current);
        Assert.Equal(before.Recent, h.S.Recent.Items);
    }

    [AvaloniaFact]
    public async Task P11_06_FullOpen_ReadsLoadsCompilesAndOpensTheLastScene()
    {
        using var h = new LauncherHarness(startPage: "T-P12", recent: false);
        var path = MakeProject(Path.Combine(h.Fx.Folder, "CaveQuest"));
        var steps = new List<string>();
        h.Launcher.BeforeOpenStep = (step, _) => { steps.Add(step); return Task.CompletedTask; };
        Assert.True(await h.Launcher.OpenAsync(path, "test"));
        Assert.Equal(["Read project", "Load assets", "Compile scripts", "Open last scene"], steps);
        Assert.Equal("Cave Quest", h.S.Project.Name);
        Assert.Equal(path, h.S.Recent.Items[0].Path);
        Assert.Equal(path, h.S.Preferences.Current.LastProject);
        Assert.Equal(["Scene", "Game"], h.S.Documents.Open.Select(d => d.Name));
        Assert.Equal("Forest_01.scene", Path.GetFileName(h.S.Documents.Active!.Path));
        Assert.Equal("T-P01", h.Win.CurrentPageId);
        Assert.True(h.Win.TitleBar.IsVisible);
        Assert.False(h.Win.Overlays.IsOpen);
        Assert.False(Directory.Exists(Path.Combine(h.Fx.Folder, "CaveQuest", "build")));   // compiling at open writes nothing into the project
    }

    [AvaloniaFact]
    public async Task P11_06_UnsavedChangesAreAskedFirstAndBrokenProjectsFailCleanly()
    {
        using var h = new LauncherHarness(startPage: null, recent: false);
        h.Fx.OpenSkyHopper();
        h.Show("T-P01");
        var other = MakeProject(Path.Combine(h.Fx.Folder, "CaveQuest"));
        h.S.Documents.Open[0].IsDirty = true;
        var open = h.Launcher.OpenAsync(other, "test");
        Capture.Pump();
        // P10.08: the title names the action (opening another project, not exiting)
        var first = Assert.IsType<UnsavedChangesDialog>(h.Win.Overlays.Open[^1]);
        Assert.Equal("Save changes before opening another project?", first.GetVisualDescendants().OfType<DialogHeader>().Single().Title);
        h.Key(PhysicalKey.Escape);   // stay
        Assert.False(await open);
        Assert.Equal("Sky Hopper", h.S.Project.Name);

        // Don't Save: the other project opens
        open = h.Launcher.OpenAsync(other, "test");
        Capture.Pump();
        var ask = (UnsavedChangesDialog)h.Win.Overlays.Open[^1];
        h.Click(ask.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Don't Save"));
        Assert.True(await open);
        Assert.Equal("Cave Quest", h.S.Project.Name);

        // a broken scene stops at Read project with a message; the open project stays
        var broken = MakeProject(Path.Combine(h.Fx.Folder, "Broken"), "Broken");
        File.WriteAllText(Path.Combine(h.Fx.Folder, "Broken", "scenes", "Forest_01.scene"), "{ not json");
        Assert.False(await h.Launcher.OpenAsync(broken, "test"));
        Assert.Equal("Cave Quest", h.S.Project.Name);
        Assert.Contains(h.S.Toasts.Visible, t => t.Text.StartsWith("Could not open Broken", StringComparison.Ordinal) && t.Status == ToastStatus.Error);
    }
}
