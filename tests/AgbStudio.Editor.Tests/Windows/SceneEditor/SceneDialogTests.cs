using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>P11.15 Scene Switcher, P11.18 New Scene and Scene Settings, P11.21 Set as Start Scene and Background Layers….</summary>
public sealed class SceneDialogTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    GameProject P => _w.S.Project.Current!;

    [AvaloniaFact]
    public void SceneSwitcher_StoryOrderSearchSwitchAndNewScene()
    {
        _w.Click(_w.Win.TitleBar.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Breadcrumb"));
        Assert.Equal(("T-O01", "Breadcrumb"), _w.S.Windows.Requests[^1]);
        var sw = Assert.IsType<SceneSwitcher>(_w.Win.Overlays.Open.Single());
        Assert.Equal(["Title", "Forest_01", "Forest_02", "Cave_01", "Summit"], sw.Shown);
        // the row tags of the Figma frame: "menu", the size in tiles, and "boss" for Summit (Slime King has Health)
        string Meta(string scene) => sw.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "SceneMeta_" + scene).Text!;
        Assert.Equal(["menu", "30×20", "32×20", "24×40", "boss"], sw.Shown.Select(Meta).ToArray());
        _w.Type("for");
        Assert.Equal(["Forest_01", "Forest_02"], sw.Shown);
        _w.Click(sw.GetVisualDescendants().OfType<Button>().First(b => b.Name == "Scene_Forest_02"));
        Assert.False(_w.Win.Overlays.IsOpen);
        Assert.Equal("Forest_02", _w.M.SceneName);
        Assert.Equal("Forest_02", P.Settings.LastScene);
        Assert.Equal("Forest_02.scene", _w.Win.TitleBar.Document);
        Assert.Equal("Forest_02 · 32×20 tiles", _w.Find<Chip>("SceneSizeChip").Content);
        Assert.Equal("Scene", _w.S.Documents.Open[0].Name);
        // New Scene… from the switcher opens the New Scene dialog
        _w.Click(_w.Win.TitleBar.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Breadcrumb"));
        sw = (SceneSwitcher)_w.Win.Overlays.Open.Single();
        _w.Click(sw.GetVisualDescendants().OfType<Button>().First(b => b.Name == "NewSceneButton"));
        Assert.Equal(("T-D02", "SceneSwitcherNewScene"), _w.S.Windows.Requests[^1]);
        Assert.IsType<NewSceneDialog>(_w.Win.Overlays.Open.Single());
    }

    [AvaloniaFact]
    public void NewScene_CreatesTheSceneFromTheOptions_AndUndo()
    {
        _w.S.Commands.Execute("scene.new");
        Capture.Pump();
        var d = Assert.IsType<NewSceneDialog>(_w.Win.Overlays.Open.Single());
        Assert.Equal("Forest_03", d.SceneName.Text);                       // next free name after Forest_01, _02
        Assert.Equal(("30", "20"), (d.WidthTiles.Text, d.HeightTiles.Text)); // the current scene's size
        Assert.Equal("Copy backgrounds from Forest_01", d.CopyBackgrounds.Content);
        Assert.Equal(SceneFiles.Templates, d.StartFrom.Options);
        // validation
        d.SceneName.Text = "Forest_02";
        Assert.Equal("A scene named Forest_02 already exists.", d.Problem);
        Assert.Null(d.Create());
        d.SceneName.Text = "Meadow";
        d.WidthTiles.Text = "40";
        d.HeightTiles.Text = "12";
        d.GetVisualDescendants().OfType<Segment>().First(s => s.Name == "Mode_1").IsChecked = true;
        Assert.Null(d.Problem);
        _w.Click(d.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Create Scene"));
        Assert.False(_w.Win.Overlays.IsOpen);
        var scene = GameProject.Load<SceneFile>(Path.Combine(P.ScenesDir, "Meadow.scene"));
        var map = GameProject.Load<MapFile>(Path.Combine(P.MapsDir, "Meadow.map"));
        Assert.Equal((40, 12, 1, "Platformer level", "Meadow"), (scene.Width, scene.Height, scene.VideoMode, scene.Template, scene.Map));
        Assert.Equal("Player", scene.Camera.Follow);
        Assert.Equal("hero", scene.Actors.Single().Sprite);
        Assert.Equal(["Sky", "Hills", "Ground"], map.Layers.Select(l => l.Name));       // backgrounds copied
        Assert.Equal(2, map.Layers[2].Bg);
        Assert.Equal(40, map.Layers[2].Cells.Skip(11 * 40).Count(c => c != 0));        // ground along the bottom
        Assert.Equal("Meadow", _w.M.SceneName);                                        // opened
        _w.S.Commands.Execute("edit.undo");
        Assert.False(File.Exists(Path.Combine(P.ScenesDir, "Meadow.scene")));
        Assert.NotEqual("Meadow", _w.M.SceneName);
    }

    [AvaloniaFact]
    public void SceneSettings_AppliesEverySection_RenamesWithLinks_AndUndo()
    {
        _w.S.Commands.Execute("scene.settings");
        Capture.Pump();
        var d = Assert.IsType<SceneSettingsDialog>(_w.Win.Overlays.Open.Single());
        Assert.Equal(("Forest_01", "30", "20"), (d.SceneNameField.Value, d.Size.Value, d.Size.Value2));
        Assert.False(d.StartScene.IsOn);
        Assert.Equal(("Player", "32", "24", true), (d.Follow.Value, d.DeadZone.Value, d.DeadZone.Value2, d.Clamp.IsOn));
        Assert.Equal(["BG0 Sky", "BG1 Hills", "BG2 Ground"], d.ParallaxFields.Select(f => f.Label));
        Assert.Equal(["0.2", "0.5", "1.0"], d.ParallaxFields.Select(f => f.Value));
        Assert.Equal(("bgm_forest.xm", "FadeIn(0.5s)", "Save.Checkpoint()"), (d.Music.Value, d.OnEnter.Value, d.OnExit.Value));
        d.SceneNameField.Value = "Woods_01";
        d.Size.Value = "32";
        d.StartScene.IsOn = true;
        d.Follow.Value = "Goal Flag";
        d.DeadZone.Value = "40";
        d.Clamp.IsOn = false;
        d.ParallaxFields[1].SliderValue = 25;
        d.Music.Value = "bgm_cave.mod";
        d.OnEnter.Value = "FadeIn(1s)";
        _w.Click(d.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Save"));
        Assert.False(_w.Win.Overlays.IsOpen);
        var path = Path.Combine(P.ScenesDir, "Woods_01.scene");
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(P.ScenesDir, "Forest_01.scene")));
        var s = GameProject.Load<SceneFile>(path);
        Assert.Equal(("Woods_01", 32, "Goal Flag", 40, false, "bgm_cave", "FadeIn(1s)"), (s.Name, s.Width, s.Camera.Follow, s.Camera.DeadZoneW, s.Camera.ClampToMap, s.Music.Track, s.Events.OnEnter));
        var map = GameProject.Load<MapFile>(Path.Combine(P.MapsDir, "Forest_01.map"));
        Assert.Equal((32, 0.25), (map.Width, map.Layers[1].ParallaxX));
        Assert.Equal(32 * 20, map.Layers[2].Cells.Count);
        var settings = GameProject.Load<ProjectFile>(P.ProjectPath);
        Assert.Equal(("Woods_01", "Woods_01"), (settings.General.StartScene, settings.LastScene));
        // links from other scenes follow the rename
        Assert.Contains(GameProject.Load<SceneFile>(Path.Combine(P.ScenesDir, "Cave_01.scene")).Links, l => l.To == "Woods_01");
        Assert.Equal("Woods_01", _w.M.SceneName);
        Assert.Equal("Woods_01.scene", _w.Win.TitleBar.Document);
        _w.S.Commands.Execute("edit.undo");
        Assert.True(File.Exists(Path.Combine(P.ScenesDir, "Forest_01.scene")));
        Assert.False(File.Exists(path));
        Assert.Contains(GameProject.Load<SceneFile>(Path.Combine(P.ScenesDir, "Cave_01.scene")).Links, l => l.To == "Forest_01");
        Assert.Equal("Title", P.Settings.General.StartScene);
        Assert.Equal("Forest_01", _w.M.SceneName);
    }

    [AvaloniaFact]
    public void SetAsStartScene_AndBackgroundLayers()
    {
        Assert.Equal("Title", P.Settings.General.StartScene);
        _w.S.Commands.Execute("scene.setStart");
        Assert.Equal("Forest_01", GameProject.Load<ProjectFile>(P.ProjectPath).General.StartScene);
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal("Title", GameProject.Load<ProjectFile>(P.ProjectPath).General.StartScene);
        _w.S.Commands.Execute("scene.backgroundLayers");
        Assert.Equal(("T-P03", "scene.backgroundLayers"), _w.S.Windows.Requests[^1]);
        // the Scene menu reaches both (T-M04)
        var scene = _w.Win.MenuBar.Items.OfType<MenuItem>().First(m => (string)m.Header! == "Scene");
        Assert.Contains(scene.Items.OfType<MenuItem>(), i => (string?)i.Header == "Set as Start Scene");
        Assert.Contains(scene.Items.OfType<MenuItem>(), i => (string?)i.Header == "Background Layers…");
    }
}
