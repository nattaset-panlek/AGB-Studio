using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Tilemap;

/// <summary>Headless E2E of the Tilemap Editor (T-P03, T-P26, T-M20) on the real Sky Hopper project: P11.28-P11.32.</summary>
public class TilemapE2ETests : IDisposable
{
    readonly PageHarness _h = PageHarness.ForTilemap();

    public void Dispose() => _h.Dispose();

    TilemapEditorPage OpenFromTab()
    {
        _h.Click(_h.Find<DocTab>("Doc_Forest_01.map"));
        return Assert.IsType<TilemapEditorPage>(_h.Win.CurrentPage);
    }

    MapDocument Reload(TilemapEditorPage page) => MapDocument.Load(_h.S.Project.Current!, page.Session!.Doc.Path, new AgbStudio.Editor.Services.UndoService(5));

    /// <summary>The window point over a map cell of the current layer.</summary>
    Point CellPoint(TilemapEditorPage page, int x, int y)
    {
        var s = page.Session!;
        var (sx, sy) = s.ScreenOf(x, y);
        var win = page.Canvas!.Window;
        return page.Canvas.TranslatePoint(new Point(win.X + (sx + 8) * s.Zoom, win.Y + (sy + 8) * s.Zoom), _h.Win)!.Value;
    }

    [AvaloniaFact]
    public void OpensFromTheMapTabBackgroundLayersAndCreateMenu()
    {
        var page = OpenFromTab();
        Assert.Equal("T-P03", _h.S.Windows.Requests[^1].Window);
        Assert.EndsWith("Forest_01.map", page.Session!.Doc.Path);
        Assert.Equal("Forest_01.map", _h.Win.TitleBar.Document);
        Assert.Contains("Tile #1 · grass_top", _h.Texts(page));
        _h.Click(_h.Find<DocTab>("Doc_Scene"));
        Assert.IsType<SceneWorkspace>(_h.Win.CurrentPage);
        _h.S.Commands.Execute("scene.backgroundLayers");
        _h.Pump();
        Assert.IsType<TilemapEditorPage>(_h.Win.CurrentPage);
        // the same map keeps its unsaved edits when the page is opened again
        Assert.Same(page.Session, MapLibrary.Of(_h.S).Active);
        // Create › Tileset / Tilemap makes a new map and opens it
        _h.S.Commands.Execute("assets.create.tileset");
        _h.Pump();
        Assert.Equal("T-P03", _h.S.Windows.Requests[^1].Window);
        Assert.NotSame(page.Session, MapLibrary.Of(_h.S).Active);
        Assert.EndsWith("Map_01.map", MapLibrary.Of(_h.S).Active!.Doc.Path);
    }

    // P11.28 Tileset panel with tileset dropdown, brush, stamp, random
    [AvaloniaFact]
    public void TilesetPanel_DropdownBrushStampRandom_PaintWithTheMouseSaveUndo()
    {
        var page = OpenFromTab();
        var s = page.Session!;
        Assert.Equal("forest_tiles.png", _h.Find<InspectorField>("TilesetField").Value);
        _h.Click(_h.Find<InspectorField>("TilesetField").GetVisualDescendants().OfType<Button>().First(b => b.IsVisible));
        Assert.Equal(("T-O04", "TilesetDropdown"), _h.S.Windows.Requests[^1]);
        // the Asset Picker opens on its Tilesets tab (T-O04) and lists the tilesets
        var picker = Assert.IsType<AgbStudio.Editor.Windows.SceneEditor.AssetPicker>(_h.Win.Overlays.Open[^1]);
        Assert.Equal("Tilesets", picker.Tab);
        Assert.Contains(picker.Items, i => Path.GetFileName(i) == "forest_tiles.png");
        _h.Win.Overlays.CloseTop();
        _h.Pump();
        // brush tile from the tileset panel: click selects one tile, dragging selects a 2 x 1 stamp
        var swatch = page.Swatch!;
        Point TileCenter(int t) => swatch.TranslatePoint(swatch.CellRect(t).Center, _h.Win)!.Value;
        _h.Click(_h.Find<IconButton>("Tool_pencil"));
        _h.ClickAt(TileCenter(7));
        Assert.Equal((7, 1, 1), (s.SelectedTile, s.StampWidth, s.StampHeight));
        Assert.Contains("Tile #7 · stone", _h.Texts(page));
        _h.Drag(TileCenter(4), TileCenter(5));
        Assert.Equal([4, 5], s.Stamp);
        Assert.Equal("2 × 1 tiles", _h.Find<InspectorField>("StampField").Value);
        Assert.Equal("Brush 2×1 · ladder", _h.Find<Chip>("BrushChip").Content);
        // Stamp field
        _h.Type(_h.Find<InspectorField>("StampField"), "1 × 1");
        Assert.Equal((1, 1), (s.StampWidth, s.StampHeight));
        // paint with the mouse on the canvas (auto-tile off for a plain brush)
        _h.Click(_h.Find<StatusItem>("Status_AutoTile"));
        Assert.Equal("Auto-tile off", _h.Find<StatusItem>("Status_AutoTile").Content);
        s.SetCamera(0, 0);
        _h.Pump();
        var ground = s.Layer;
        _h.ClickAt(CellPoint(page, 2, 1));
        Assert.Equal(4, s.Doc.Cell(ground, 2, 1));
        Assert.Contains("tile 2, 1", (string?)_h.Find<Chip>("HoverChip").Content);
        Assert.Contains("BG2 Ground · tile 2, 1 · id 4 ladder", (string?)_h.Find<StatusItem>("Status_Hover").Content);
        Assert.True(_h.S.Documents.Active!.IsDirty);
        Assert.False(_h.Find<StatusItem>("Status_Saved").IsVisible);
        // Random: drag the slider fully right, paint a row
        var slider = _h.Find<InspectorField>("RandomField").GetVisualDescendants().OfType<AgbSlider>().First();
        _h.Drag(_h.Center(slider), _h.Center(slider) + new Point(slider.Bounds.Width, 0));
        Assert.Equal(100, s.Random);
        Assert.Equal("100%", _h.Find<InspectorField>("RandomField").Value);
        s.SelectTile(4, 2, 1);
        s.Seed(3);
        _h.Drag(CellPoint(page, 0, 3), CellPoint(page, 9, 3), 9);
        var row = Enumerable.Range(0, 10).Select(x => s.Doc.Cell(ground, x, 3)).ToList();
        Assert.All(row, c => Assert.Contains(c, new[] { 4, 5 }));
        // Ctrl+S saves the .map; Ctrl+Z undoes the last stroke
        _h.Key(PhysicalKey.S, RawInputModifiers.Control);
        Assert.False(_h.S.Documents.Active!.IsDirty);
        Assert.Equal(row, Enumerable.Range(0, 10).Select(x => Reload(page).Cell(ground, x, 3)));
        _h.Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.All(Enumerable.Range(0, 10), x => Assert.Equal(0, s.Doc.Cell(ground, x, 3)));
        Assert.Equal(4, s.Doc.Cell(ground, 2, 1));
    }

    // P11.29 Tile properties: collision type, flip H, tag, animation, auto-tile set; collision view
    [AvaloniaFact]
    public void TileProperties_CollisionFlipTagAnimationAutoTile_CollisionView()
    {
        var page = OpenFromTab();
        var s = page.Session!;
        var doc = s.Doc;
        s.SelectTile(4);
        _h.Pump();
        Assert.Equal(ChipStyle.Accent, _h.Find<Chip>("Collision_Ladder").Style);
        _h.Click(_h.Find<Chip>("Collision_Hazard"));
        Assert.Equal(CollisionType.Hazard, doc.Props(4)!.Collision);
        Assert.Equal(ChipStyle.Danger, _h.Find<Chip>("Collision_Hazard").Style);
        _h.Type(_h.Find<InspectorField>("TagField"), "climb");
        Assert.Equal("climb", doc.Props(4)!.Tag);
        _h.Choose(_h.Find<InspectorField>("AnimationField"), "2 frames");
        Assert.Equal([4, 5], doc.Props(4)!.Animation);
        Assert.Equal("2 frames", _h.Find<InspectorField>("AnimationField").Value);
        _h.Choose(_h.Find<InspectorField>("AutoTileField"), "Grass edges (4)");
        Assert.Equal("Grass edges", doc.SetOf(4)?.Name);
        Assert.Equal("Grass edges (5)", _h.Find<InspectorField>("AutoTileField").Value);
        _h.Key(PhysicalKey.S, RawInputModifiers.Control);
        var meta = GameProject.Load<TilesetMeta>(GameProject.MetaPath(doc.TilesetPath!));
        Assert.Equal((CollisionType.Hazard, "climb"), (meta.Tiles[4].Collision, meta.Tiles[4].Tag));
        // Flip H: the switch flips what the brush paints
        var flip = _h.Find<InspectorField>("FlipField");
        _h.Click(flip.GetVisualDescendants().OfType<AgbSwitch>().First());
        Assert.True(s.Flip);
        s.SetAutoTile(false);
        s.SetCamera(0, 0);
        s.SelectTile(7);
        _h.Pump();
        _h.ClickAt(CellPoint(page, 1, 1));
        Assert.Equal(7 | MapDocument.FlipH, doc.Cell(s.Layer, 1, 1));
        // collision view: the chip turns the overlay on and the canvas shows it
        s.SetCamera(0, doc.Height * 16 - 160);
        _h.Pump();
        var chip = _h.Find<Chip>("CollisionViewChip");
        Assert.False(s.CollisionView);
        var before = page.Canvas!.Bounds;
        _h.Click(chip);
        Assert.True(s.CollisionView);
        Assert.Equal(ChipStyle.Danger, chip.Style);
        Assert.Contains(_h.All<Chip>(), c => c.Name == "Legend_Solid");
        _h.Click(_h.Find<Chip>("Legend_Solid"));
        Assert.False(s.ShowsCollision(CollisionType.Solid));
        Assert.Equal(before, page.Canvas.Bounds);
    }

    // P11.30 Layers table (type, priority, parallax, VRAM char / screen block) and Add Layer menu
    [AvaloniaFact]
    public void LayersTable_RowsVisibilityLockSelection_AddLayerMenu()
    {
        var page = OpenFromTab();
        var s = page.Session!;
        var table = page.Bottom!;
        Assert.Equal(["Layers", "Auto-tile rules", "Assets"], table.Tabs.Select(t => t.Label));
        Assert.Equal(["BG0 · Sky", "Image", "3", "× 0.20", "char block 0 · screen 30"], table.RowTexts(0));
        Assert.Equal(["BG1 · Hills", "Image", "2", "× 0.50", "char block 0 · screen 28"], table.RowTexts(1));
        Assert.Equal(["BG2 · Ground", "Tilemap", "1", "× 1.00", "char block 0 · screen 26"], table.RowTexts(2));
        Assert.Equal(["OBJ · Actors", "Sprites", "–", "–", "OAM 10 / 128"], table.RowTexts(3));
        Assert.True(page.BgToggles[2].IsActive);
        // click a row: that layer is edited
        _h.Click(_h.Find<TableCell>("Layer2_0"));
        Assert.Equal(1, s.Layer);
        Assert.True(page.BgToggles[1].IsActive);
        _h.Click(_h.Find<TableCell>("Layer3_0"));
        // eye hides the layer (and the BG toggle shows it off); lock stops painting
        _h.Click(_h.Find<Button>("Eye_2"));
        Assert.False(s.IsVisible(2));
        Assert.False(page.BgToggles[2].IsOn);
        _h.Click(page.BgToggles[2]);
        Assert.True(s.IsVisible(2));
        _h.Click(_h.Find<Button>("Lock_2"));
        Assert.True(s.IsLocked(2));
        Assert.False(s.CanPaint);
        _h.Click(_h.Find<Button>("Eye_OBJ"));
        Assert.False(s.ObjVisible);
        // right-click the table: Add Layer (T-M20)
        _h.Click(_h.Find<TableCell>("Layer1_1"), MouseButton.Right);
        Assert.Equal(("T-M20", "LayersTable"), _h.S.Windows.Requests[^1]);
        _h.Key(PhysicalKey.Escape);
        Assert.True(_h.S.Commands.Get("tilemap.addLayer.tile").CanExecute());
        _h.S.Commands.Execute("tilemap.addLayer.tile");
        _h.Pump();
        Assert.Equal(["BG3 · Tiles", "Tilemap", "0", "× 1.00", "char block 0 · screen 24"], table.RowTexts(3));
        Assert.Equal(3, s.Layer);
        Assert.False(_h.S.Commands.Get("tilemap.addLayer.image").CanExecute());
        Assert.True(_h.S.Commands.Get("tilemap.addLayer.collision").CanExecute());
        _h.S.Commands.Execute("tilemap.addLayer.collision");
        _h.Pump();
        Assert.Equal(["Collision", "Collision", "–", "–", "empty"], table.RowTexts(4));
        Assert.Equal("Collision layer", _h.Find<InspectorField>("SourceField").Value);
        _h.Key(PhysicalKey.S, RawInputModifiers.Control);
        Assert.Equal(5, Reload(page).Map.Layers.Count);
        // the Map header's menu opens Add Layer too
        _h.Click(_h.Find<PanelHeader>("MapHeader").GetVisualDescendants().OfType<Button>().First());
        Assert.Equal(("T-M20", "MapMenu"), _h.S.Windows.Requests[^1]);
        Assert.Equal(["Tile layer", "Image layer", "Rotate / scale layer (Mode 1–2)", "Collision-only layer"],
            AddLayerMenu.Entries().OfType<AgbStudio.Editor.Commands.CommandEntry>().Select(e => _h.S.Commands.Get(e.CommandId).Title));
    }

    // P11.31 Map size, BG size, colours, wrap; stats unique tiles, screen blocks, BG palettes, VRAM BG
    [AvaloniaFact]
    public void MapPanel_SizeBgSizeColoursWrap_AndBudget()
    {
        var page = OpenFromTab();
        var doc = page.Session!.Doc;
        Assert.Equal(("30 × 20", "480×320 px"), (_h.Find<InspectorField>("SizeField").Value, _h.Find<InspectorField>("SizeField").Value2));
        Assert.Equal("512 × 256 (64×32)", _h.Find<InspectorField>("BgSizeField").Value);
        Assert.Equal("4bpp · 16 per tile", _h.Find<InspectorField>("ColoursField").Value);
        Assert.Equal("Off", _h.Find<InspectorField>("WrapField").Value);
        Assert.Equal("tile properties", _h.Find<InspectorField>("SourceField").Value);
        Assert.Equal(doc.CountCollision(CollisionType.Solid).ToString(), _h.Find<InspectorField>("SolidField").Value);
        Assert.Equal(doc.CountCollision(CollisionType.OneWay).ToString(), _h.Find<InspectorField>("OneWayField").Value);
        var plan = page.Session.Plan();
        Assert.Equal($"{plan.UniqueTiles} / 1024", _h.Find<Meter>("Meter_Uniquetiles").ValueText);
        Assert.Equal($"{(int)Math.Round(plan.VramBytes / 1024.0)} / 64 KB", _h.Find<Meter>("Meter_VRAMBG").ValueText);
        Assert.Equal("6 / 32", _h.Find<Meter>("Meter_Screenblocks").ValueText);
        Assert.Equal("3 / 16", _h.Find<Meter>("Meter_BGpalettes").ValueText);
        Assert.True(_h.Find<StackPanel>("BudgetAllGood").IsVisible);

        _h.Type(_h.Find<InspectorField>("SizeField"), "40 × 24");
        Assert.Equal((40, 24), (doc.Width, doc.Height));
        Assert.Equal("640×384 px", _h.Find<InspectorField>("SizeField").Value2);
        _h.Choose(_h.Find<InspectorField>("BgSizeField"), "512 × 512 (64×64)");
        Assert.Equal(BgSize.S64x64, doc.BgSize);
        Assert.Equal("12 / 32", _h.Find<Meter>("Meter_Screenblocks").ValueText);
        Assert.Equal(["char block 0 · screen 28", "char block 0 · screen 24", "char block 0 · screen 20"], page.Session.Plan().Layers.Select(l => l.Text));
        _h.Choose(_h.Find<InspectorField>("ColoursField"), "8bpp · 256 colours");
        Assert.Equal(8, doc.Bpp);
        _h.Choose(_h.Find<InspectorField>("WrapField"), "On");
        Assert.True(doc.Map.Layers[page.Session.Layer].Wrap);
        _h.Click(_h.Find<Chip>("ZoomChip"));
        Assert.Equal("400%", _h.Find<Chip>("ZoomChip").Content);
        _h.Key(PhysicalKey.S, RawInputModifiers.Control);
        var back = Reload(page);
        Assert.Equal((40, 24, BgSize.S64x64, 8, true), (back.Width, back.Height, back.BgSize, back.Bpp, back.Map.Layers[page.Session.Layer].Wrap));
    }

    // P11.32 Auto-tile Rules panel: rule sets, 3x3 rules, Add rule
    [AvaloniaFact]
    public void AutoTileRules_TabRulesMaskAddRuleRuleSet()
    {
        var page = OpenFromTab();
        var s = page.Session!;
        _h.Click(_h.Find<PanelTab>("Tab_AutoTile"));
        Assert.Equal(("T-P26", "Tab_AutoTile"), _h.S.Windows.Requests[^1]);
        var panel = page.Bottom!;
        Assert.True(panel.RuleSetField.IsVisible);
        Assert.Equal("Grass edges (4)", panel.RuleSetField.Value);
        var labels = panel.RuleCards.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["Top edge", "Left edge", "Right edge", "Inner", "Add rule"], labels);
        // mask cell 1 (above) of "Top edge" is "not the set": a click makes it "any"
        Assert.Equal(0, s.Doc.Meta.AutoTiles[0].Rules[0].Mask[1]);
        _h.Click(_h.Find<Border>("Mask0_1"));
        Assert.Equal(2, s.Doc.Meta.AutoTiles[0].Rules[0].Mask[1]);
        _h.Click(_h.Find<Border>("RuleCard2"));
        Assert.Equal(2, s.SelectedRule);
        // Add rule: a rule for the selected tileset tile
        s.SelectTile(7);
        _h.Pump();
        _h.Click(_h.Find<Border>("AddRule"));
        Assert.Equal(5, s.Doc.Meta.AutoTiles[0].Rules.Count);
        Assert.Equal(7, s.Doc.Meta.AutoTiles[0].Rules[0].Tile);
        Assert.Equal("Grass edges (5)", panel.RuleSetField.Value);
        // Delete removes the selected rule
        panel.Focus();
        _h.Key(PhysicalKey.Delete);
        Assert.Equal(4, s.Doc.Meta.AutoTiles[0].Rules.Count);
        _h.Choose(panel.RuleSetField, "Grass edges (4)");
        Assert.Equal("Grass edges", s.RuleSet);
        _h.Key(PhysicalKey.S, RawInputModifiers.Control);
        var meta = GameProject.Load<TilesetMeta>(GameProject.MetaPath(s.Doc.TilesetPath!));
        Assert.Equal(2, meta.AutoTiles[0].Rules[0].Mask[1]);
        // the Auto-tile Rules panel opens on its own for the active map too (T-P26 registration)
        var standalone = Assert.IsType<TilemapBottomPanel>(_h.S.Windows.CreatePanel("T-P26"));
        Assert.Equal(1, standalone.SelectedTab);
    }
}
