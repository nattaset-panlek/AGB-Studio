using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.Scripts;
using AgbStudio.Editor.Windows.WorldMap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.WorldMap;

/// <summary>World Map (T-P13, T-O09, T-M22): P11.50 scene cards, links, start scene, inspector, New Scene, zoom; P11.51 Link Settings and Scene Context Menu.</summary>
public sealed class WorldMapTests : IDisposable
{
    readonly EditorHarness _h = new(Phase11ScriptShots.FigmaWorld);

    public void Dispose() => _h.Dispose();

    WorldMapPage Open()
    {
        _h.Key(Key.M, RawInputModifiers.Control);
        var page = _h.Page<WorldMapPage>();
        page.Canvas.SetView(1, default);
        Capture.Pump();
        return page;
    }

    SceneFile Scene(string name) => GameProject.Load<SceneFile>(Path.Combine(_h.S.Project.Current!.ScenesDir, name + ".scene"));

    Point Card(WorldMapPage page, string name) => _h.Center(page.Canvas.CardOf(name)!);

    [AvaloniaFact]
    public void CtrlM_ShowsScenesLinksStartSceneAndInspector()
    {
        var page = Open();
        Assert.Equal("T-P13", _h.S.Windows.Requests[^1].Window);
        Assert.Equal("World Map", _h.Win.TitleBar.Document);
        Assert.Equal("5 scenes · 6 links", _h.Find<Chip>("SummaryChip").Content);
        Assert.Equal("Start scene: Title", _h.Find<StatusItem>("Status_Start").Content);
        // the Scenes list: start scene first, meta "menu" / size / "boss"
        var list = _h.Find<StackPanel>("SceneList").Children.OfType<TreeItem>().Select(t => (t.Label, t.Meta)).ToList();
        Assert.Equal([("Title", "menu"), ("Forest_01", "30×20"), ("Forest_02", "32×20"), ("Cave_01", "24×40"), ("Summit", "boss")], list);
        // cards at their map positions, showing the real scene and no sample art
        var card = page.Canvas.CardOf("Cave_01")!;
        Assert.Null(card.Preview);
        Assert.NotNull(card.PreviewImage);
        Assert.NotSame(card.PreviewImage, page.Canvas.CardOf("Summit")!.PreviewImage);
        // a menu scene's card shows its menu over the scene; a level's shows the scene alone
        var project = _h.S.Project.Current!;
        var assets = new AgbStudio.Editor.Windows.SceneEditor.SceneAssets(project);
        uint[] Plain(MapScene s)
        {
            var (x, y) = AgbStudio.Editor.Windows.SceneEditor.GbaScreenRenderer.StartCamera(s.Data, s.Map);
            return AgbStudio.Editor.Windows.SceneEditor.GbaScreenRenderer.Render(assets, s.Data, s.Map, x, y).ToArgb();
        }
        var title = page.Model.Find("Title")!;
        var forest = page.Model.Find("Forest_01")!;
        Assert.NotEqual(Plain(title), WorldMapCanvas.ThumbnailPixels(project, assets, title));
        Assert.Equal(Plain(forest), WorldMapCanvas.ThumbnailPixels(project, assets, forest));
        Assert.Equal((620.0, 330.0), (Avalonia.Controls.Canvas.GetLeft(card), Avalonia.Controls.Canvas.GetTop(card)));
        // link chips: the title menu start, trigger links and a condition-only link
        foreach (var chip in new[] { "Chip_Start", "Chip_Goal_Flag", "Chip_Exit_trigger", "Chip_3_stars" }) Assert.NotNull(_h.TryFind<Chip>(chip));
        Assert.Equal("star", _h.Find<Chip>("Chip_3_stars").Glyph);
        // select Forest_01: links from it and the inspector
        _h.ClickAt(Card(page, "Forest_01"));
        Assert.Equal("Forest_01", page.Selected);
        Assert.True(page.Canvas.CardOf("Forest_01")!.IsSelected);
        Assert.Equal("Links from Forest_01", _h.Find<PanelHeader>("LinksHeader").Title);
        Assert.Equal(["Goal Flag → Forest_02", "Exit trigger → Cave_01", "Fall in pit → Forest_01"],
            _h.Find<StackPanel>("LinkList").Children.OfType<TreeItem>().Select(t => t.Label));
        Assert.Equal("30 × 20 tiles", _h.Find<InspectorField>("SceneSize").Value);
        Assert.Equal("Mode 0 · 4 BG", _h.Find<InspectorField>("VideoMode").Value);
        Assert.Equal(["Mode 0 · 4 BG", "Mode 1 · 3 BG", "Mode 2 · 2 BG", "Mode 3 · bitmap", "Mode 4 · bitmap 255", "Mode 5 · bitmap small"], WorldMapModel.VideoModes);
        Assert.False(_h.Find<InspectorField>("StartScene").IsOn);
        Assert.Equal(["0.2", "0.5", "1.0"], _h.Find<StackPanel>("Section_Backgrounds").GetVisualDescendants().OfType<InspectorField>().Select(f => f.Value));
        Assert.Equal("bgm_forest.xm", _h.Find<InspectorField>("MusicTrack").Value);
        Assert.Equal("0.8", _h.Find<InspectorField>("MusicVolume").Value);
        Assert.Equal("FadeIn(0.5s)", _h.Find<InspectorField>("OnEnter").Value);
        Assert.Equal("Save.Checkpoint()", _h.Find<InspectorField>("OnExit").Value);
    }

    [AvaloniaFact]
    public void DragCardEditInspectorStartSceneZoomAndUndo()
    {
        var page = Open();
        var from = Card(page, "Summit");
        _h.DragAt(from, new Point(from.X + 100, from.Y + 20));
        Assert.Equal((720, 620), (Scene("Summit").MapX, Scene("Summit").MapY));
        _h.S.Undo.Undo();
        Assert.Equal((620, 600), (Scene("Summit").MapX, Scene("Summit").MapY));
        // inspector edits write the scene and map files
        page.SelectScene("Forest_01");
        var size = _h.Find<InspectorField>("SceneSize");
        size.Value = "40 x 20";
        var box = size.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "PART_Text");
        box.Focus();
        _h.Key(Key.Enter);
        Assert.Equal((40, 20), (Scene("Forest_01").Width, Scene("Forest_01").Height));
        var start = _h.Find<InspectorField>("StartScene");
        _h.Click(start.GetVisualDescendants().OfType<AgbSwitch>().Single());
        Assert.Equal("Forest_01", _h.S.Project.Current!.Settings.General.StartScene);
        Assert.Equal("Start scene: Forest_01", _h.Find<StatusItem>("Status_Start").Content);
        _h.S.Undo.Undo();
        Assert.Equal("Title", _h.S.Project.Current!.Settings.General.StartScene);
        // zoom with the wheel shows in the status bar
        _h.Win.MouseWheel(_h.Center(page.Canvas), new Vector(0, 1));
        Capture.Pump();
        Assert.Equal("110%", _h.Find<StatusItem>("Status_Zoom").Content);
        // New Scene opens the New Scene dialog (T-D02)
        _h.Click(_h.Find<AgbButton>("NewScene"));
        Assert.Equal("T-D02", _h.S.Windows.Requests[^1].Window);
    }

    [AvaloniaFact]
    public void LinkChipOpensLinkSettings_ChangesAreSaved()
    {
        var page = Open();
        _h.Click(_h.Find<Chip>("Chip_Goal_Flag"));
        Assert.Equal("T-O09", _h.S.Windows.Requests[^1].Window);
        Assert.Contains(page.Canvas.Chips, x => x.Link == new LinkRef("Forest_01", 0) && x.Rect.Width > 40);
        var link = Assert.IsType<LinkSettingsPopover>(_h.Win.Overlays.Open.Single());
        Assert.Equal("Goal Flag · Forest_01", LinkSettingsPopover.TextOf(link.From));
        Assert.Equal("Forest_02", LinkSettingsPopover.TextOf(link.To));
        Assert.Equal("Start (left edge)", LinkSettingsPopover.TextOf(link.SpawnAt));
        Assert.Equal("Game.coins >= 10", link.OnlyIf.Text);
        link.SetTransition(Transition.Slide);
        Assert.Equal(Transition.Slide, Scene("Forest_01").Links[0].Transition);
        link.OnlyIf.Text = "Game.coins ≥ 20";
        link.OnlyIf.Focus();
        _h.Key(Key.Enter);
        Assert.Equal("Game.coins >= 20", Scene("Forest_01").Links[0].OnlyIf);
        link.Change("Spawn point", l => page.Model.ApplySpawn(l, "End (right edge)"));
        Assert.Equal(32 * 16 - 24, Scene("Forest_01").Links[0].SpawnX);
        _h.S.Undo.Undo();
        Assert.Equal("Start (left edge)", Scene("Forest_01").Links[0].SpawnAt);
        _h.Key(Key.Escape);
        Assert.False(_h.Win.Overlays.IsOpen);
        // the link tool draws a new link from a card to another and opens its settings
        _h.Click(_h.Find<IconButton>("Tool_Link"));
        _h.DragAt(Card(page, "Summit"), Card(page, "Title"));
        Assert.Contains(Scene("Summit").Links, l => l.To == "Title");
        Assert.IsType<LinkSettingsPopover>(_h.Win.Overlays.Open.Single());
    }

    [AvaloniaFact]
    public void SceneContextMenu_OpenRenameDuplicateSetStartDelete()
    {
        var page = Open();
        _h.ClickAt(Card(page, "Title"), MouseButton.Right);
        Assert.Equal("T-M22", _h.S.Windows.Requests[^1].Window);
        var menu = _h.OpenContextMenu()!;
        var items = menu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(["Open", "Rename", "Duplicate", "Set as start scene", "Delete"], items.Select(i => (string?)i.Header));
        Assert.True(items[3].IsChecked);
        Assert.Equal(["Enter", "F2", "Ctrl+D", null, "Del"], items.Select(i => i.InputGesture is { } g ? AgbStudio.Editor.Theme.Shortcuts.Format(g) : null));
        menu.Close();
        // Set as start scene on Cave_01
        _h.ClickAt(Card(page, "Cave_01"), MouseButton.Right);
        Assert.False(_h.OpenContextMenu()!.Items.OfType<MenuItem>().ElementAt(3).IsChecked);
        _h.RunMenuItem("Set as start scene");
        Assert.Equal("Cave_01", _h.S.Project.Current!.Settings.General.StartScene);
        // Rename with F2: the file and the links to it follow
        page.SelectScene("Forest_02");
        page.Canvas.Focus();
        _h.Key(Key.F2);
        var box = _h.Find<TextBox>("RenameBox");
        box.Text = "Forest_02b";
        _h.Key(Key.Enter);
        Assert.True(File.Exists(Path.Combine(_h.S.Project.Current!.ScenesDir, "Forest_02b.scene")));
        Assert.Equal("Forest_02b", Scene("Forest_01").Links[0].To);
        Assert.Equal("Forest_02b", page.Selected);
        // Duplicate (Ctrl+D through the Edit menu) and Delete, both undoable
        _h.ClickAt(Card(page, "Summit"), MouseButton.Right);
        _h.RunMenuItem("Duplicate");
        Assert.NotNull(page.Model.Find("Summit_copy"));
        Assert.Equal("Summit_copy", page.Selected);
        _h.ClickAt(Card(page, "Summit_copy"), MouseButton.Right);
        _h.RunMenuItem("Delete");
        Assert.Null(page.Model.Find("Summit_copy"));
        _h.S.Undo.Undo();
        Assert.NotNull(page.Model.Find("Summit_copy"));
        // Open (Enter on the selected card) goes to the Scene Editor with the scene
        page.SelectScene("Cave_01");
        page.Canvas.Focus();
        _h.Key(Key.Enter);
        Assert.Equal(("T-P01", "WorldMapOpen"), _h.S.Windows.Requests[^1]);
        Assert.Equal("Cave_01", _h.S.Project.Current!.Settings.LastScene);
    }
}
