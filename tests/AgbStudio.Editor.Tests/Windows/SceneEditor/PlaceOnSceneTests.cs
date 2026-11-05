using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>Phase 18: place actors by clicking the scene, drag out zones, the start marker and the collision brush (T-P01).</summary>
public sealed class PlaceOnSceneTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    GbaScreenView Screen => _w.Find<GbaScreenView>("GbaScreen");
    SceneEditorModel M => _w.M;

    Point OnScreen(int sceneX, int sceneY)
    {
        var (cx, cy) = M.Camera;
        return Screen.TranslatePoint(Screen.ToView(cx + sceneX, cy + sceneY), _w.Win)!.Value;
    }

    void Snap16()
    {
        M.Grid.Snap = true;
        M.Grid.Size = 16;
    }

    // ---- P18.01 ---------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void PlaceMode_EachClickPutsOneActorOnTheGrid_OneUndoStepEach_EscapeStops()
    {
        Snap16();
        var (cx, cy) = M.Camera;
        var before = M.Scene!.Actors.Count;
        var undo = _w.S.Undo.UndoCount;
        _w.S.Commands.Execute("scene.place.actor");
        Capture.Pump();
        Assert.Equal(ActorKind.Empty, M.PlaceKind);

        _w.ClickAt(OnScreen(53, 70));
        var first = M.Scene.Actors[^1];
        Assert.Equal(before + 1, M.Scene.Actors.Count);
        Assert.Equal(M.SnapValue(cx + 53), first.X);
        Assert.Equal(M.SnapValue(cy + 70), first.Y);
        Assert.Equal(M.Scene.Actors.Count - 1, M.SelectedActorIndex);
        Assert.Equal(undo + 1, _w.S.Undo.UndoCount);

        // the mode stays for the next click, and each actor is its own undo step
        _w.ClickAt(OnScreen(120, 70));
        Assert.Equal(before + 2, M.Scene.Actors.Count);
        Assert.Equal(undo + 2, _w.S.Undo.UndoCount);
        Assert.Equal(ActorKind.Empty, M.PlaceKind);
        _w.S.Undo.Undo();
        Capture.Pump();
        Assert.Equal(before + 1, M.Scene.Actors.Count);

        _w.Key(PhysicalKey.Escape);
        Capture.Pump();
        Assert.Null(M.PlaceKind);
        _w.ClickAt(OnScreen(180, 70));
        Assert.Equal(before + 1, M.Scene.Actors.Count);
    }

    [AvaloniaFact]
    public void PlaceMode_KeysStartIt_ARightClickOrAnotherToolStopsIt_TheMenuStaysAsItWas()
    {
        Screen.Focus();
        Capture.Pump();
        _w.Key(PhysicalKey.A);
        Capture.Pump();
        Assert.Equal(ActorKind.Empty, M.PlaceKind);
        _w.Key(PhysicalKey.T);
        Capture.Pump();
        Assert.Equal(ActorKind.TriggerZone, M.PlaceKind);

        var count = M.Scene!.Actors.Count;
        _w.ClickAt(OnScreen(60, 60), MouseButton.Right);
        Assert.Null(M.PlaceKind);
        Assert.Equal(count, M.Scene.Actors.Count);

        _w.S.Commands.Execute("scene.place.enemy");
        Assert.Equal(ActorKind.Enemy, M.PlaceKind);
        _w.S.Commands.Execute("scene.tool.paint");
        Capture.Pump();
        Assert.Null(M.PlaceKind);
        _w.S.Commands.Execute("scene.tool.select");
        Capture.Pump();

        // Add Actor in the menus still uses the middle of the screen and starts no mode
        var (sx, sy) = M.SpawnPoint();
        _w.S.Commands.Execute("scene.addActor.pickup");
        Capture.Pump();
        Assert.Null(M.PlaceKind);
        Assert.Equal((sx, sy), (M.Scene.Actors[^1].X, M.Scene.Actors[^1].Y));
    }

    [AvaloniaFact]
    public void PlaceMode_GivesEachKindItsDefaults()
    {
        Snap16();
        foreach (var (command, kind, name) in new[]
                 {
                     ("scene.place.player", ActorKind.Player, "Player"), ("scene.place.enemy", ActorKind.Enemy, "Enemy"),
                     ("scene.place.pickup", ActorKind.Pickup, "Pickup"), ("scene.place.camera", ActorKind.CameraZone, "Camera Zone"),
                 })
        {
            _w.S.Commands.Execute(command);
            _w.ClickAt(OnScreen(90, 60));
            var a = M.Scene!.Actors[^1];
            Assert.Equal(kind, a.Kind);
            Assert.StartsWith(name, a.Name);
            M.PlaceKind = null;
        }
    }

    // ---- P18.02 ---------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void TriggerZone_DragGivesItsSizeAndPosition_AClickGivesTheDefault()
    {
        Snap16();
        var (cx, cy) = M.Camera;
        var undo = _w.S.Undo.UndoCount;
        _w.S.Commands.Execute("scene.place.trigger");
        Capture.Pump();
        _w.Drag(OnScreen(48, 32), OnScreen(112, 96));
        Capture.Pump();
        var zone = M.Scene!.Actors[^1];
        Assert.Equal(ActorKind.TriggerZone, zone.Kind);
        int left = M.SnapValue(cx + 48), top = M.SnapValue(cy + 32), right = M.SnapValue(cx + 112), bottom = M.SnapValue(cy + 96);
        Assert.Equal(right - left, zone.ZoneW);
        Assert.Equal(bottom - top, zone.ZoneH);
        Assert.Equal(left, zone.X);
        // a zone's position is its bottom-left: the viewport box, which is also the build's collider, covers the drag
        Assert.Equal(bottom, zone.Y);
        Assert.Equal((left, top, right - left, bottom - top), Screen.ActorRect(M.Scene.Actors.Count - 1));
        Assert.Equal(undo + 1, _w.S.Undo.UndoCount);

        // dragging backwards gives the same box
        _w.Drag(OnScreen(112, 96), OnScreen(48, 32));
        Capture.Pump();
        var back = M.Scene.Actors[^1];
        Assert.Equal((zone.X, zone.Y, zone.ZoneW, zone.ZoneH), (back.X, back.Y, back.ZoneW, back.ZoneH));

        // a click: the kind's default size, with its top on the clicked cell
        _w.ClickAt(OnScreen(160, 64));
        Capture.Pump();
        var small = M.Scene.Actors[^1];
        Assert.Equal((16, 32), (small.ZoneW, small.ZoneH));
        Assert.Equal(M.SnapValue(cy + 64) + 32, small.Y);
    }

    [AvaloniaFact]
    public void TriggerZone_ThatIsSavedKeepsItsDraggedSize()
    {
        Snap16();
        _w.S.Commands.Execute("scene.place.trigger");
        Capture.Pump();
        _w.Drag(OnScreen(48, 32), OnScreen(112, 64));
        Capture.Pump();
        var zone = M.Scene!.Actors[^1];
        _w.S.Commands.Execute("file.save");
        Capture.Pump();
        var saved = GameProject.Load<SceneFile>(M.ScenePath!).Actors.Single(a => a.Name == zone.Name);
        Assert.Equal((zone.ZoneW, zone.ZoneH, zone.X, zone.Y), (saved.ZoneW, saved.ZoneH, saved.X, saved.Y));
    }

    // ---- P18.03 ---------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void StartMarker_ShowsOnlyInTheStartScene_AndDraggingItMovesThePlayer()
    {
        var settings = M.Project!.Settings;
        settings.General.StartScene = "SomewhereElse";
        Screen.InvalidateVisual();
        Assert.Null(Screen.StartMarker);

        settings.General.StartScene = M.SceneName;
        var index = M.Scene!.Actors.FindIndex(a => a.Kind == ActorKind.Player);
        Assert.True(index >= 0);
        Assert.Equal(index, M.StartActor);
        var tag = Assert.NotNull(Screen.StartMarker);
        var player = M.Scene.Actors[index];
        var (x0, y0) = (player.X, player.Y);

        var from = Screen.TranslatePoint(tag.Center, _w.Win)!.Value;
        _w.Drag(from, from + new Point(64, -32));
        Capture.Pump();
        Assert.NotEqual(x0, player.X);
        Assert.NotEqual(y0, player.Y);
        Assert.Equal(index, M.SelectedActorIndex);

        // the drag is what gets saved
        _w.S.Commands.Execute("file.save");
        Capture.Pump();
        var saved = GameProject.Load<SceneFile>(M.ScenePath!).Actors[index];
        Assert.Equal((player.X, player.Y), (saved.X, saved.Y));

        _w.S.Undo.Undo();
        Capture.Pump();
        Assert.Equal((x0, y0), (M.Scene.Actors[index].X, M.Scene.Actors[index].Y));
    }

    [AvaloniaFact]
    public void StartMarker_NeedsAPlayer()
    {
        M.Project!.Settings.General.StartScene = M.SceneName;
        M.Scene!.Actors.RemoveAll(a => a.Kind == ActorKind.Player);
        Assert.Equal(-1, M.StartActor);
        Assert.Null(Screen.StartMarker);
    }

    // ---- P18.05 ---------------------------------------------------------------------------------------

    int AddCollisionLayer()
    {
        var map = M.Map!;
        map.Layers.Add(new MapLayerData { Name = "Walls", Type = LayerType.CollisionOnly, Bg = 0, Cells = new int[map.Width * map.Height].ToList() });
        map.CollisionLayer = map.Layers.Count - 1;
        M.SelectActor(0);
        M.Selection = new SceneItem(SceneItemKind.Scene);
        Capture.Pump();
        return map.CollisionLayer;
    }

    void PickBrush(string option)
    {
        var button = _w.Find<InspectorField>("Field_CollisionBrush").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dropdown");
        _w.Click(button);
        Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose(option));
        Capture.Pump();
    }

    [AvaloniaFact]
    public void CollisionBrush_PaintsTheChosenTypeIntoItsOwnLayer_AndLeavesTheArtAlone()
    {
        var layer = AddCollisionLayer();
        var art = M.Map!.Layers.Take(layer).Select(l => l.Cells.ToList()).ToList();

        _w.Click(_w.Find<InspectorField>("Field_CollisionBrush").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dropdown"));
        var rows = ((IEnumerable<Control>)AgbStudio.Editor.Windows.Database.Dropdown.Last!.ItemsSource!).OfType<MenuItem>().Select(i => (string)i.Header!).ToList();
        Assert.Equal("Off", rows[0]);
        Assert.Contains("Solid", rows);
        Assert.Equal("None", rows[^1]);
        Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose("Solid"));
        Capture.Pump();
        Assert.Equal(CollisionType.Solid, M.CollisionBrush);
        Assert.Equal(SceneTool.Paint, M.Tool);
        Assert.True(M.Grid.TileCollision);
        Assert.Equal(layer, M.PaintLayer);
        Assert.Equal("Solid", _w.Find<InspectorField>("Field_CollisionBrush").Value);

        var (cx, cy) = M.Camera;
        var (sx, sy) = (cx + 100, cy + 40);
        Assert.Equal(CollisionType.None, Screen.CollisionAt(sx, sy));
        var undo = _w.S.Undo.UndoCount;
        _w.ClickAt(OnScreen(100, 40));
        Capture.Pump();
        Assert.Equal(CollisionType.Solid, Screen.CollisionAt(sx, sy));
        Assert.Equal(undo + 1, _w.S.Undo.UndoCount);
        for (var i = 0; i < art.Count; i++) Assert.Equal(art[i], M.Map!.Layers[i].Cells);

        // None empties the cell; undo brings the wall back
        PickBrush("None");
        _w.ClickAt(OnScreen(100, 40));
        Capture.Pump();
        Assert.Equal(CollisionType.None, Screen.CollisionAt(sx, sy));
        _w.S.Undo.Undo();
        Capture.Pump();
        Assert.Equal(CollisionType.Solid, Screen.CollisionAt(sx, sy));

        // Off gives back the normal brush
        PickBrush("Off");
        Assert.Null(M.CollisionBrush);
        Assert.NotEqual(layer, M.PaintLayer);
    }

    [AvaloniaFact]
    public void CollisionBrush_IsAlsoOfferedOnTheCollisionLayerItself()
    {
        var layer = AddCollisionLayer();
        M.Selection = new SceneItem(SceneItemKind.Layer, layer);
        Capture.Pump();
        Assert.NotNull(_w.Find<InspectorField>("Field_CollisionBrush"));
    }

    [AvaloniaFact]
    public void CollisionFromTheTiles_SaysWhereItComesFrom_AndOffersNoBrush()
    {
        var map = M.Map!;
        Assert.False(M.HasCollisionOnlyLayer);
        var art = map.Layers.Select(l => l.Cells.ToList()).ToList();
        M.SelectActor(0);
        M.Selection = new SceneItem(SceneItemKind.Scene);
        Capture.Pump();
        var text = _w.Find<TextBlock>("CollisionSource").Text!;
        Assert.Contains("would change the art", text);
        Assert.Contains("Tilemap Editor", text);
        Assert.DoesNotContain(_w.All<InspectorField>(), f => f.Name == "Field_CollisionBrush");

        // a brush chosen earlier does nothing on such a map
        M.SetCollisionBrush(CollisionType.Solid);
        Assert.Null(M.CollisionBrush);
        for (var i = 0; i < art.Count; i++) Assert.Equal(art[i], map.Layers[i].Cells);
    }
}
