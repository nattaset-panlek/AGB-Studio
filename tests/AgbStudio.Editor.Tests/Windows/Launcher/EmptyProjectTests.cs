using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Launcher;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Launcher;

/// <summary>P11.09: the empty project path opens Scene Editor · Empty project (T-P22) with all the empty states of 4.9,
/// and the Scene Editor takes over when the project gets content.</summary>
public class EmptyProjectTests
{
    static async Task<NewProjectDialog> CreateEmpty(LauncherHarness h, string name = "My Game")
    {
        _ = h.S.Windows.Open("T-D01", "puzzle", source: "test");
        Capture.Pump();
        var dlg = (NewProjectDialog)h.Win.Overlays.Open.Single();
        dlg.NameField.Text = name;
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "Made"));
        dlg.SampleLevel.IsChecked = dlg.ThaiFont.IsChecked = dlg.LogicGraph.IsChecked = dlg.Git.IsChecked = false;
        h.Click(dlg.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Confirm"));
        await LauncherHarness.Until(() => h.S.Project.Current != null && !h.Win.Overlays.IsOpen);
        return dlg;
    }

    [AvaloniaFact]
    public async Task P11_09_EmptyProject_OpensWithEveryEmptyState()
    {
        using var h = new LauncherHarness();
        await CreateEmpty(h);
        Assert.Equal("T-P22", h.Win.CurrentPageId);
        var page = Assert.IsType<EmptyProjectPage>(h.Win.CurrentPage);
        Assert.True(ProjectContents.Of(h.S.Project.Current!).IsEmpty);
        Assert.True(h.Win.TitleBar.IsVisible);
        Assert.Equal("My Game", h.Win.TitleBar.Project);
        Assert.Equal("Scene_01.scene", h.Win.TitleBar.Document);
        var texts = h.Texts(page).ToList();
        // Hierarchy
        Assert.Equal("Scene_01", h.Find<TreeItem>("Tree_Scene").Label);
        Assert.Equal(("Main Camera", "fixed"), (h.Find<TreeItem>("Tree_MainCamera").Label, h.Find<TreeItem>("Tree_MainCamera").Meta));
        Assert.Contains("Nothing else here yet. Press + or drag a sprite from Assets into the scene.", texts);
        // Inspector
        Assert.Contains("Nothing selected", texts);
        Assert.Contains("Select an actor or a layer to edit it here.", texts);
        Assert.Contains("USEFUL KEYS", texts);
        Assert.Equal(["Ctrl", "I", "B", "F5", "Ctrl", "K"], h.Find<StackPanel>("UsefulKeys").GetVisualDescendants().OfType<Keycap>().Select(k => k.Label));
        Assert.Equal(["0 / 64 KB", "0 / 32 KB", "0 / 128"], new[] { "Meter_VramBg", "Meter_VramObj", "Meter_Oam" }.Select(n => h.Find<Meter>(n).ValueText));
        // viewport
        Assert.Contains("This scene is empty", texts);
        Assert.Contains("The dashed box is the GBA screen, 240 × 160. Start with one of these:", texts);
        Assert.Contains("or drop PNG / Aseprite files anywhere", texts);
        Assert.Equal((720, 480), (h.Find<ScreenFrame>("ScreenFrame").Bounds.Width, h.Find<ScreenFrame>("ScreenFrame").Bounds.Height));
        Assert.Equal(["Add player", "Paint tiles", "Import images"], new[] { "AddPlayer", "PaintTiles", "ImportImages" }.Select(n => (string)h.Find<AgbButton>(n).Content!));
        // Assets
        Assert.Contains("No assets yet", texts);
        Assert.Contains("Import sprites, tilesets, fonts and sounds — or drag files here.", texts);
        Assert.Equal("Assets", page.PanelTabs.Single(t => t.IsSelected).Label);
        // status bar
        Assert.Equal("New project · 0 assets", h.Find<StatusItem>("Status_Project").Content);
        Assert.False(h.Find<StatusItem>("Status_Rom").IsVisible);   // not built yet
        Assert.Equal(["Scene", "Game"], h.Find<StackPanel>("DocTabs").Children.OfType<DocTab>().Select(t => t.Label));

        // Import images and Import… open Import Asset (T-D04)
        h.Click(h.Find<AgbButton>("ImportImages"));
        Assert.Equal(("T-D04", "EmptySceneImport"), h.S.Windows.Requests[^1]);
        h.Win.Overlays.CloseTop();
        h.Click(h.Find<AgbButton>("ImportAssets"));
        Assert.Equal(("T-D04", "AssetsEmptyImport"), h.S.Windows.Requests[^1]);
        h.Win.Overlays.CloseTop();
        Capture.Pump();

        // a file imported into the project ends the empty state: the Scene Editor takes over
        var p = h.S.Project.Current!;
        AgbStudio.Build.Content.PixelArt.Hero8().Save(Path.Combine(p.AssetsDir, "sprites", "hero.png"));
        await LauncherHarness.Until(() => h.Win.CurrentPageId == "T-P01", 15000);
        Assert.Equal(("T-P01", "ProjectHasContent"), h.S.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public async Task P11_09_AddPlayer_PutsThePlayerInTheSceneAndOpensTheSceneEditor()
    {
        using var h = new LauncherHarness();
        await CreateEmpty(h, "Empty One");
        var page = Assert.IsType<EmptyProjectPage>(h.Win.CurrentPage);
        h.Click(h.Find<AgbButton>("AddPlayer"));
        Assert.Equal("T-P01", h.Win.CurrentPageId);
        if (!h.S.Commands.Get("scene.addActor.player").IsImplemented)
        {
            // without the Scene Editor's Add Actor command the player is written into the scene file
            var scene = GameProject.Load<SceneFile>(Path.Combine(h.S.Project.Current!.ScenesDir, "Scene_01.scene"));
            Assert.Equal(ActorKind.Player, Assert.Single(scene.Actors).Kind);
            Assert.False(ProjectContents.Of(h.S.Project.Current!).IsEmpty);
        }

        // opening an empty project again shows the empty state; Paint tiles takes the Paint tool into the Scene Editor
        var empty = Path.Combine(h.Fx.Folder, "Made", "Again");
        var created = ProjectCreator.Create(new NewProjectOptions { Template = TemplateCatalog.Find("rpg"), Name = "Again", Folder = empty, RomTitle = "AGAIN", GameCode = "AGNE", SampleLevel = false, ThaiFont = false, InitGit = false });
        Assert.True(await h.Launcher.OpenAsync(created.ProjectPath, "test"));
        Assert.Equal("T-P22", h.Win.CurrentPageId);
        page = (EmptyProjectPage)h.Win.CurrentPage!;
        h.Click(h.Find<AgbButton>("PaintTiles"));
        Assert.Equal(2, page.ActiveTool);
        Assert.Equal(("T-P01", "PaintTiles"), h.S.Windows.Requests[^1]);
    }

    [Fact]
    public void Contents_EmptyOnlyWithoutAssetsScriptsActorsAndMap()
    {
        var dir = Directory.CreateTempSubdirectory("agb-empty-").FullName;
        try
        {
            var p = GameProject.Create(Path.Combine(dir, "P"), "P");
            ProjectCreator.AddStartScene(p);
            Assert.Equal(new ProjectContents(1, 0, 0, 0, false), ProjectContents.Of(p));
            Assert.True(ProjectContents.Of(p).IsEmpty);
            File.WriteAllText(Path.Combine(p.ScriptsDir, "A.cs"), "");
            Assert.False(ProjectContents.Of(p).IsEmpty);
            File.Delete(Path.Combine(p.ScriptsDir, "A.cs"));
            ProjectCreator.AddSampleLevel(p);
            var c = ProjectContents.Of(p);
            Assert.Equal((2, 4), (c.Scenes, c.Actors));
            Assert.True(c.HasMap);
            Assert.False(c.IsEmpty);
        }
        finally { Directory.Delete(dir, true); }
    }
}
