using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>P11.12: Hierarchy tree, search, add (+), context menu, selection in sync with the viewport and Inspector, rename.</summary>
public sealed class HierarchyTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    HierarchyPanel Panel => _w.All<HierarchyPanel>().Single();
    TreeItem Row(string key) => _w.Find<TreeItem>("Row_" + key);

    [AvaloniaFact]
    public void Tree_ShowsTheSceneAsInFigma()
    {
        var rows = Panel.Items.Select(i => (i.Label, i.Meta, i.Depth, i.Glyph)).ToList();
        Assert.Equal(
        [
            ("Forest_01", null, 0, "map"), ("Main Camera", "follow", 1, "camera"), ("Backgrounds", null, 1, "layers"),
            ("BG0 · Sky", "×0.2", 2, "image"), ("BG1 · Hills", "×0.5", 2, "image"), ("BG2 · Ground", "tilemap", 2, "tiles"),
            ("Player", "Actor", 1, "user"), ("Enemies", "2", 1, "folder"), ("Slime", null, 2, "cube"), ("Slime (1)", null, 2, "cube"),
            ("Coins", "5", 1, "coin"), ("Question Block", null, 1, "cube"), ("Goal Flag", null, 1, "flag"),
            ("Exit → Cave_01", "trigger", 1, "link"), ("HUD · Score", "text", 1, "type"),
        ], rows);
        Assert.Equal(TreeToggle.Closed, Row("group:Coins").Toggle);
        Assert.True(Row("actor:0").IsSelected);
        // opening the Coins folder shows its five actors
        _w.DoubleClick(Row("group:Coins"));
        Assert.Equal(20, Panel.Items.Count);
        Assert.Equal(TreeToggle.Open, Row("group:Coins").Toggle);
    }

    [AvaloniaFact]
    public void Search_FiltersRowsAndKeepsTheirFolders()
    {
        var search = _w.Find<SearchField>("HierarchySearch");
        search.Focus();
        _w.Type("slime");
        Assert.Equal(["Forest_01", "Enemies", "Slime", "Slime (1)"], Panel.Items.Select(i => i.Label));
        search.Text = "coin (3";
        Capture.Pump();
        Assert.Equal(["Forest_01", "Coins", "Coin (3)"], Panel.Items.Select(i => i.Label));
        search.Text = "";
        Capture.Pump();
        Assert.Equal(15, Panel.Items.Count);
    }

    [AvaloniaFact]
    public void Selection_IsSharedWithViewportAndInspector()
    {
        _w.Click(Row("actor:1"));
        Assert.Equal(1, _w.M.SelectedActorIndex);
        Assert.Equal("Slime", _w.Find<TextBlock>("ActorName").Text);
        Assert.Equal("Actor", Row("actor:1").Meta);
        Assert.Null(Row("actor:0").Meta);
        // the viewport draws the selection box of the selected actor
        var screen = _w.Find<GbaScreenView>("GbaScreen");
        Assert.NotNull(screen.ActorRect(_w.M.SelectedActorIndex));
        // selecting in the model (viewport click, menus) moves the Hierarchy selection
        _w.M.SelectActor(3);
        Capture.Pump();
        Assert.True(Row("actor:3").IsSelected);
        Assert.Equal("Question Block", _w.Find<TextBlock>("ActorName").Text);
        // layers and the camera are selectable too
        _w.Click(Row("layer:1"));
        Assert.Equal(new SceneItem(SceneItemKind.Layer, 1), _w.M.Selection);
        _w.Click(Row("camera"));
        Assert.Equal(SceneItemKind.Camera, _w.M.Selection!.Kind);
    }

    [AvaloniaFact]
    public void AddButtonAndContextMenu()
    {
        var header = _w.Find<PanelHeader>("HierarchyHeader");
        _w.Click(header.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action"));
        Assert.Equal(("T-M11", "HierarchyAdd"), _w.S.Windows.Requests[^1]);
        _w.Key(PhysicalKey.Escape);
        _w.Click(Row("actor:4"), MouseButton.Right);
        Assert.Equal(("T-M12", "HierarchyActor"), _w.S.Windows.Requests[^1]);
        Assert.Equal(4, _w.M.SelectedActorIndex);
    }

    [AvaloniaFact]
    public void F2_RenamesInPlace_AndUndo()
    {
        _w.Click(Row("actor:3"));
        _w.Key(PhysicalKey.F2);
        var box = Panel.RenameBox;
        Assert.NotNull(box);
        Assert.Equal("Question Block", box!.Text);
        box.SelectAll();
        _w.Type("Lucky Block");
        _w.Key(PhysicalKey.Enter);
        Assert.Equal("Lucky Block", _w.M.Scene!.Actors[3].Name);
        Assert.Equal("Lucky Block", GameProject.Load<SceneFile>(_w.M.ScenePath!).Actors[3].Name);
        Assert.Contains(Panel.Items, i => i.Label == "Lucky Block");
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal("Question Block", _w.M.Scene.Actors[3].Name);
    }

    [AvaloniaFact]
    public void EditMenu_DeleteDuplicateCopyPasteOnTheSelectedActor()
    {
        var m = _w.M;
        var count = m.Scene!.Actors.Count;
        _w.Click(Row("actor:3"));
        _w.S.Commands.Execute("edit.duplicate");
        Assert.Equal(count + 1, m.Scene.Actors.Count);
        Assert.Equal("Question Block (1)", m.Scene.Actors[4].Name);
        Assert.Equal(4, m.SelectedActorIndex);
        _w.S.Commands.Execute("edit.delete");
        Assert.Equal(count, m.Scene.Actors.Count);
        _w.S.Commands.Execute("edit.copy");
        _w.S.Commands.Execute("edit.paste");
        Assert.Equal(count + 1, m.Scene.Actors.Count);
        Assert.Equal(count + 1, GameProject.Load<SceneFile>(m.ScenePath!).Actors.Count);
        _w.S.Commands.Execute("edit.undo");
        _w.S.Commands.Execute("edit.undo");
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal(count, m.Scene.Actors.Count);
        Assert.Equal("Question Block", m.Scene.Actors[3].Name);
    }

    // P10.11 on actors: Ctrl+X cuts the selected actor (saved without it), Ctrl+V puts it back; Ctrl+A selects the
    // scene row, which holds every actor; with no actor selected, Ctrl+X cuts nothing
    [AvaloniaFact]
    public void EditMenu_CutPasteAndSelectAllOnActors()
    {
        var m = _w.M;
        var count = m.Scene!.Actors.Count;
        _w.Click(Row("actor:3"));
        Assert.Equal(3, m.SelectedActorIndex);
        _w.Key(PhysicalKey.X, RawInputModifiers.Control);
        Assert.Equal(count - 1, m.Scene.Actors.Count);
        Assert.DoesNotContain(m.Scene.Actors, a => a.Name == "Question Block");
        Assert.Equal(count - 1, GameProject.Load<SceneFile>(m.ScenePath!).Actors.Count);
        Assert.DoesNotContain(Panel.Items, i => i.Label == "Question Block");
        _w.Key(PhysicalKey.V, RawInputModifiers.Control);
        Assert.Equal(count, m.Scene.Actors.Count);
        Assert.Contains(m.Scene.Actors, a => a.Name.StartsWith("Question Block", StringComparison.Ordinal));
        Assert.Equal(count, GameProject.Load<SceneFile>(m.ScenePath!).Actors.Count);

        _w.Key(PhysicalKey.A, RawInputModifiers.Control);
        Assert.Equal(SceneItemKind.Scene, m.Selection?.Kind);
        Assert.True(Row("scene").IsSelected);
        Assert.False(Row("actor:0").IsSelected);
        Assert.Equal(-1, m.SelectedActorIndex);
        _w.Key(PhysicalKey.X, RawInputModifiers.Control);
        Assert.Equal(count, m.Scene.Actors.Count);
    }
}
