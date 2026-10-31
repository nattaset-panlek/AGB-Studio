using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>P11.10 (viewport in the editor: GBA screen 240 x 160, zoom, pan, cursor coordinates) and P11.11 (layer
/// toggles BG0-BG3 / OBJ, video mode chip, scene size chip, grid chip, zoom chip), driven in the headless main window.</summary>
public sealed class ViewportTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    GbaScreenView Screen => _w.Find<GbaScreenView>("GbaScreen");

    [AvaloniaFact]
    public void Viewport_ShowsTheGbaScreenAtThreeHundredPercentWithTheStartCamera()
    {
        Assert.Equal("Forest_01", _w.M.SceneName);
        var screen = Screen;
        Assert.Equal(860, screen.Bounds.Width);
        Assert.Equal(3, screen.Scale);
        // 720 x 480 centred in the 860 x 510 viewport, as in Figma (330, 129 in the page)
        var tl = screen.TranslatePoint(screen.Origin, _w.Win)!.Value;
        Assert.Equal(new Point(330, 129), tl);
        // the ROM's start camera: Player (48, 288) at the bottom-left of the 480 x 320 map, clamped (the Figma picture)
        Assert.Equal((0, 160), _w.M.Camera);
        var f = screen.Frame!;
        Assert.Equal(240 * 160, f.Bgr.Length);
        Assert.Contains(f.Sprites, s => _w.M.Scene!.Actors[s.Actor].Name == "Player");
        Assert.Equal("Forest_01 · 30×20 tiles", _w.Find<Chip>("SceneSizeChip").Content);
        Assert.Equal("300%", _w.Find<Chip>("ZoomChip").Content);
        Assert.Equal("Grid 16", _w.Find<Chip>("GridChip").Content);
        // with the pointer outside, the cursor chip shows the selected actor's position
        Assert.Equal("x 48  y 288", _w.Find<Chip>("CursorChip").Content);
    }

    [AvaloniaFact]
    public void Cursor_ZoomAndPan()
    {
        var screen = Screen;
        var o = screen.TranslatePoint(screen.Origin, _w.Win)!.Value;
        // pointer over screen pixel (100, 50): scene (100, 210) with the camera at (0, 160)
        _w.Win.MouseMove(new Point(o.X + 100 * 3 + 1, o.Y + 50 * 3 + 1));
        Assert.Equal((100, 210), _w.M.Cursor);
        Assert.Equal("x 100  y 210", _w.Find<Chip>("CursorChip").Content);
        // Ctrl + wheel zooms in steps of 100 %
        _w.Win.MouseWheel(new Point(o.X + 10, o.Y + 10), new Vector(0, 1), RawInputModifiers.Control);
        Assert.Equal(400, _w.M.Zoom);
        Assert.Equal("400%", _w.Find<Chip>("ZoomChip").Content);
        _w.Win.MouseWheel(new Point(o.X + 10, o.Y + 10), new Vector(0, -1), RawInputModifiers.Control);
        _w.Win.MouseWheel(new Point(o.X + 10, o.Y + 10), new Vector(0, -1), RawInputModifiers.Control);
        Assert.Equal(200, _w.M.Zoom);
        // middle-button drag pans the camera (scene pixels = screen pixels / zoom)
        var c = _w.Center(screen);
        _w.Drag(c, new Point(c.X - 40, c.Y - 20), MouseButton.Middle);
        Assert.Equal((20, 10), _w.M.Pan);
        Assert.Equal((20, 170), _w.M.Camera);
        // Fit to Screen: the largest whole zoom that fits 860 x 510 (pixel-perfect)
        _w.S.Commands.Execute("scene.zoom.fit");
        Capture.Pump();
        Assert.Equal(3, Screen.Scale);
        Assert.Equal("Fit", _w.Find<Chip>("ZoomChip").Content);
    }

    [AvaloniaFact]
    public void ClickSelectsAndDragMovesWithSnap()
    {
        var screen = Screen;
        var m = _w.M;
        var block = m.Scene!.Actors.FindIndex(a => a.Name == "Question Block");
        Assert.Equal((0, 160), m.Camera);   // the block (64, 240) is on screen
        Capture.Pump();
        var (x, y, _, _) = screen.ActorRect(block)!.Value;
        var p = screen.TranslatePoint(screen.ToView(x + 8, y + 8), _w.Win)!.Value;
        _w.ClickAt(p);
        Assert.Equal(block, m.SelectedActorIndex);
        Assert.True(_w.Find<TreeItem>("Row_actor:" + block).IsSelected);
        // drag by 20 scene pixels right: snapped to the 16 px grid
        _w.Drag(p, new Point(p.X + 20 * 3, p.Y));
        Assert.Equal(88, m.Scene.Actors[block].X);
        Assert.Equal(88, GameProject.Load<SceneFile>(m.ScenePath!).Actors[block].X);
        // one undo step for the whole drag
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal(64, m.Scene.Actors[block].X);
        // arrow keys move by the grid size
        screen.Focus();
        _w.Key(PhysicalKey.ArrowDown);
        Assert.Equal(256, m.Scene.Actors[block].Y);
        // empty space selects the scene
        _w.ClickAt(screen.TranslatePoint(screen.ToView(m.Camera.X + 230, m.Camera.Y + 5), _w.Win)!.Value);
        Assert.Equal(SceneItemKind.Scene, m.Selection!.Kind);
    }

    // T-P01: Paint tiles (B), Fill and Erase edit the scene's map in the viewport, one undo step per stroke
    [AvaloniaFact]
    public void PaintFillAndErase_EditTheMapWithUndo()
    {
        var m = _w.M;
        var screen = Screen;
        Point View(int sceneX, int sceneY) => screen.TranslatePoint(screen.ToView(sceneX, sceneY), _w.Win)!.Value;
        int[] Disk() => GameProject.Load<MapFile>(m.MapPath!).Layers[m.PaintLayer].Cells.ToArray();
        _w.Click(_w.Find<IconButton>("Tool_pencil"));
        Assert.Equal(SceneTool.Paint, m.Tool);
        Assert.Equal(2, _w.Workspace.ActiveTool);
        // the ground layer (the collision source) is painted with the first named tile of the tileset
        var layer = m.PaintLayer;
        Assert.Equal("Ground", m.Map!.Layers[layer].Name);
        var brush = Assert.Single(m.Brush.Cells);
        Assert.True(brush > 0);
        // an empty sky cell on the start screen (camera (0, 160)): scene (100, 210) is cell (6, 13)
        Assert.Equal((6, 13), m.CellAt(layer, 100, 210));
        int Cell(int x, int y) => m.Map!.Layers[layer].Cells[y * m.Map.Width + x];
        Assert.Equal(0, Cell(6, 13));
        var undoBefore = _w.S.Undo.UndoCount;
        _w.ClickAt(View(100, 210));
        Assert.Equal(brush, Cell(6, 13));
        Assert.Equal(brush, Disk()[13 * m.Map!.Width + 6]);
        Assert.Equal(SceneItemKind.Actor, m.Selection!.Kind);   // painting does not change the selection
        // a drag paints every cell it crosses as one undo step
        _w.Drag(View(100, 178), View(148, 178));
        Assert.Equal([brush, brush, brush, brush], new[] { Cell(6, 11), Cell(7, 11), Cell(8, 11), Cell(9, 11) });
        Assert.Equal(undoBefore + 2, _w.S.Undo.UndoCount);
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal([0, 0, 0, 0], new[] { Cell(6, 11), Cell(7, 11), Cell(8, 11), Cell(9, 11) });
        Assert.Equal(brush, Cell(6, 13));
        // Erase empties the cell
        _w.Click(_w.Find<IconButton>("Tool_eraser"));
        Assert.Equal(SceneTool.Erase, m.Tool);
        _w.ClickAt(View(100, 210));
        Assert.Equal(0, Cell(6, 13));
        Assert.Equal(0, Disk()[13 * m.Map.Width + 6]);
        // Fill replaces the connected empty cells around the clicked one (the sky) in one step, and undoes in one
        _w.Click(_w.Find<IconButton>("Tool_bucket"));
        var empty = m.Map.Layers[layer].Cells.Count(c => c == 0);
        _w.ClickAt(View(100, 210));
        Assert.True(m.Map.Layers[layer].Cells.Count(c => c == 0) < empty - 50);
        Assert.Equal(brush, Cell(0, 0));
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal(empty, m.Map.Layers[layer].Cells.Count(c => c == 0));
        // the brush follows the Tilemap Editor's stamp for this map
        var session = AgbStudio.Editor.Windows.Tilemap.MapLibrary.Of(_w.S).Open(m.MapPath!);
        session.SelectTile(7);
        Assert.Equal([7], m.Brush.Cells);
        _w.Click(_w.Find<IconButton>("Tool_pencil"));
        _w.ClickAt(View(100, 210));
        Assert.Equal(7, Cell(6, 13));
        // the open Tilemap Editor map (no unsaved edits) shows the painted cell
        Assert.Equal(7, session.Doc.Cell(layer, 6, 13));
        // a Layer selected in the Hierarchy is the one painted
        var hills = m.Map.Layers.FindIndex(l => l.Type == LayerType.Tile && l.Name != "Ground");
        if (hills >= 0)
        {
            m.Selection = new SceneItem(SceneItemKind.Layer, hills);
            Assert.Equal(hills, m.PaintLayer);
        }
        // Select clicks pick actors again
        _w.Click(_w.Find<IconButton>("Tool_cursor"));
        Assert.Equal(SceneTool.Select, m.Tool);
    }

    [AvaloniaFact]
    public void LayerTogglesHideBackgroundsAndSprites()
    {
        var screen = Screen;
        var before = screen.Frame!;
        Assert.Contains(before.Source, s => s == 2);   // BG2 Ground (platforms)
        _w.Click(_w.Find<LayerToggle>("Layer_BG2"));
        Assert.False(_w.Find<LayerToggle>("Layer_BG2").IsOn);
        Assert.DoesNotContain(Screen.Frame!.Source, s => s == 2);
        _w.Click(_w.Find<LayerToggle>("Layer_OBJ"));
        Assert.DoesNotContain(Screen.Frame!.Source, s => s == ScreenFrame.FromObj);
        _w.Click(_w.Find<LayerToggle>("Layer_OBJ"));
        _w.Click(_w.Find<LayerToggle>("Layer_BG2"));
        Assert.Equal(ScreenLayers.All & ~ScreenLayers.Bg3, _w.M.Layers);
        Assert.Contains(Screen.Frame!.Source, s => s == ScreenFrame.FromObj);
    }

    [AvaloniaFact]
    public void ChipsOpenTheirWindows()
    {
        var req = _w.S.Windows.Requests;
        _w.Click(_w.Find<Chip>("VideoModeChip"));
        Assert.Equal(("T-O02", "VideoModeChip"), req[^1]);
        var vm = Assert.IsType<VideoModePopover>(_w.Win.Overlays.Open.Single());
        _w.Click(vm.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == "Mode_1"));
        Assert.Equal(1, _w.M.Scene!.VideoMode);
        Assert.Equal("GBA · Mode 1 · 240×160", _w.Find<Chip>("VideoModeChip").Content);
        _w.Key(PhysicalKey.Escape);
        _w.Click(_w.Find<Chip>("SceneSizeChip"));
        Assert.Equal("T-D03", req[^1].Window);
        Assert.IsType<SceneSettingsDialog>(_w.Win.Overlays.Open.Single());
        _w.Key(PhysicalKey.Escape);
        _w.Click(_w.Find<Chip>("GridChip"));
        Assert.Equal("T-O03", req[^1].Window);
        Assert.IsType<GridSnapPopover>(_w.Win.Overlays.Open.Single());
        _w.Key(PhysicalKey.Escape);
        _w.Click(_w.Find<Chip>("ZoomChip"));
        Assert.Equal(("T-M15", "ZoomChip"), req[^1]);
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal(0, _w.M.Scene.VideoMode);
        Assert.Equal("GBA · Mode 0 · 240×160", _w.Find<Chip>("VideoModeChip").Content);
    }

    [AvaloniaFact]
    public void GridButtonAndGridSnapDrawTheOverlays()
    {
        Assert.True(_w.M.Grid.ShowGrid);
        _w.Click(_w.Find<IconButton>("Tool_grid"));
        Assert.False(_w.M.Grid.ShowGrid);
        _w.Key(PhysicalKey.G);
        Assert.True(_w.M.Grid.ShowGrid);
        // tile collision reads the tileset's collision types under the collision layer
        var screen = Screen;
        Assert.Equal(CollisionType.Solid, screen.CollisionAt(10, 19 * 16 + 4));
        Assert.Equal(CollisionType.None, screen.CollisionAt(10, 10));
        // camera bounds show once the view is panned past the map edge
        Assert.False(screen.CameraBoundsVisible);
        _w.M.SetPan(-40, 0);
        Assert.True(screen.CameraBoundsVisible);
    }
}
