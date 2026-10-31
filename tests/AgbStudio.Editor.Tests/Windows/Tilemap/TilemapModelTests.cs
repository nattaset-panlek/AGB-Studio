using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia.Headless.XUnit;

namespace AgbStudio.Editor.Tests.Windows.Tilemap;

/// <summary>Unit tests of the Tilemap Editor model: VRAM plan, renderer, map edits with undo, tile properties, auto-tile rules (11.E).</summary>
public class TilemapModelTests : IDisposable
{
    readonly Fixture _fx = new(withApp: false);
    readonly TilemapSession _s;

    public TilemapModelTests()
    {
        SkyHopperContent.OpenForTilemap(_fx);
        _s = MapLibrary.Of(_fx.Services).Current(null)!;
    }

    public void Dispose() => _fx.Dispose();

    MapDocument Doc => _s.Doc;
    UndoService Undo => _fx.Services.Undo;
    MapDocument Reload() => MapDocument.Load(_fx.Services.Project.Current!, Doc.Path, new UndoService(10));
    int Ground => Doc.Map.Layers.FindIndex(l => l.Name == "Ground");

    // ---- P11.30 VRAM char / screen blocks (runtime bg.cpp rules) -----------------------------------

    [Fact]
    public void VramPlan_TilesFromCharBlock0Up_MapsFromScreenBlock31Down()
    {
        var plan = VramPlan.Compute([new("a", 16, 1, false), new("b", 52, 1, false), new("c", 20, 2, false)], BgSize.S64x32, false);
        Assert.Equal([(0, 0, 30), (0, 16, 28), (0, 68, 26)], plan.Layers.Select(l => (l.CharBlock, l.FirstTile, l.ScreenBlock)));
        Assert.Equal(88, plan.UniqueTiles);
        Assert.Equal(6, plan.ScreenBlocks);
        Assert.Equal(4, plan.PaletteBanks);
        Assert.Equal(88 * 32 + 6 * 2048, plan.VramBytes);
        Assert.Equal("char block 0 · screen 30", plan.Layers[0].Text);
    }

    [Fact]
    public void VramPlan_SharedTilesetLoadsOnce_CollisionOnlyTakesNoVram_32x32UsesOneBlock()
    {
        var plan = VramPlan.Compute([new("t", 10, 1, false), new(null, 0, 0, true), new("t", 10, 1, false)], BgSize.S32x32, false);
        Assert.Equal((0, 31), (plan.Layers[0].CharBlock, plan.Layers[0].ScreenBlock));
        Assert.False(plan.Layers[1].HasGraphics);
        Assert.Equal((0, 0, 30), (plan.Layers[2].CharBlock, plan.Layers[2].FirstTile, plan.Layers[2].ScreenBlock));
        Assert.Equal(10, plan.UniqueTiles);
        Assert.Equal(2, plan.ScreenBlocks);
    }

    [Fact]
    public void VramPlan_FullCharBlockMovesOn_AndTooMuchDoesNotFit()
    {
        // tile data may run on into the next char block (up to 1024 tiles from the block start), as in the runtime
        var spill = VramPlan.Compute([new("a", 512, 1, false), new("b", 8, 1, false)], BgSize.S64x32, false);
        Assert.Equal((0, 512), (spill.Layers[1].CharBlock, spill.Layers[1].FirstTile));
        // a full char block 0 (1020 tiles = 16 units): block 1 starts inside them, so the next tileset goes to block 2
        var plan = VramPlan.Compute([new("a", 1020, 1, false), new("b", 8, 1, false)], BgSize.S64x32, false);
        Assert.Equal(2, plan.Layers[1].CharBlock);
        Assert.Equal(0, plan.Layers[1].FirstTile);
        var full = VramPlan.Compute([new("a", 1000, 1, false), new("b", 1000, 1, false), new("c", 1000, 1, false)], BgSize.S64x64, true);
        Assert.Contains(full.Layers, l => !l.Fits);
        Assert.Equal("no room", full.Layers.First(l => !l.Fits).Text);
    }

    [Fact]
    public void VramPlan_SkyHopperForest01()
    {
        var plan = _s.Plan();
        Assert.Equal(["char block 0 · screen 30", "char block 0 · screen 28", "char block 0 · screen 26"], plan.Layers.Select(l => l.Text));
        var graphics = _s.Renderer.LayerGraphics(Doc);
        Assert.Equal(graphics.Sum(g => g.Tiles), plan.UniqueTiles);
        Assert.Equal(6, plan.ScreenBlocks);
        Assert.Equal(3, plan.PaletteBanks);
        Assert.Equal(13, VramPlan.OamEntries(Doc.Scene));   // with the three HUD hearts
    }

    // ---- renderer (D-18: the converted data the ROM uses) -------------------------------------------

    [Fact]
    public void Renderer_DrawsTheConvertedTilesAndPalettes()
    {
        var converted = TilesetConverter.Convert(Doc.Map.Tileset, Doc.TilesetImage!, Doc.Meta, Doc.Project.Settings.Tags);
        var px = _s.Renderer.Metatile(Doc, 1);
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var se = converted.Metatiles[1][(x >> 3) + (y >> 3) * 2];
                var tile = converted.Tiles.Tiles[se & 0x3FF];
                int tx = (se & 0x400) != 0 ? 7 - (x & 7) : x & 7, ty = (se & 0x800) != 0 ? 7 - (y & 7) : y & 7;
                var index = tile[ty * 8 + tx];
                var expected = index == 0 ? 0u : Gba.ToArgb(converted.Palette.Banks[se >> 12][index]);
                Assert.Equal(expected, px[y * 16 + x]);
            }
        var flipped = _s.Renderer.Metatile(Doc, 1, flip: true);
        Assert.Equal(px[3 * 16 + 2], flipped[3 * 16 + 13]);
    }

    [Fact]
    public void Renderer_ComposesLayersWithParallaxPriorityAndSprites()
    {
        // ground row 19 at the bottom of the map: at camera (0, 160) the last screen row is the ground layer
        var screen = _s.Renderer.Render(Doc, 0, 160, 240, 160, null, false);
        var cell = Doc.Cell(Ground, 0, 19);
        Assert.Equal(_s.Renderer.Metatile(Doc, cell & MapDocument.TileMask)[15 * 16 + 4], screen[159 * 240 + 4]);
        // hiding the ground shows the hills / sky behind it
        var hidden = _s.Renderer.Render(Doc, 0, 160, 240, 160, i => i != Ground, false);
        Assert.NotEqual(screen[159 * 240 + 4], hidden[159 * 240 + 4]);
        // parallax 0.5: hills scroll half as far as the camera
        Assert.Equal(40, MapRenderer.Scroll(80, 0.5));
        Assert.Equal(15, MapRenderer.Scroll(80, 0.2));  // 0.2 is 51/256 in the ROM
        // a sprite (OBJ priority 1, Player) is drawn over BG2 (priority 1); the Player stands on the start screen
        var player = Doc.Scene!.Actors.First(a => a.Name == "Player");
        var withSprites = _s.Renderer.Render(Doc, 0, 160, 240, 160, null, true);
        var without = _s.Renderer.Render(Doc, 0, 160, 240, 160, null, false);
        var diff = Enumerable.Range(0, 240 * 160).Count(i => withSprites[i] != without[i]);
        Assert.InRange(diff, 20, 240 * 160);
        Assert.NotEqual(withSprites[(player.Y - 8 - 160) * 240 + player.X], without[(player.Y - 8 - 160) * 240 + player.X]);
    }

    // ---- P11.28 brush, stamp, random; editing with undo; the .map round trip ----------------------------

    [Fact]
    public void Brush_StampRandomFlip_UndoRedo_SaveRoundTrip()
    {
        _s.Layer = Ground;
        _s.SetAutoTile(false);
        _s.SelectTile(1, 2, 1);
        Assert.Equal([1, 2], _s.Stamp);
        _s.PaintAt(3, 5);
        Assert.Equal((1, 2), (Doc.Cell(Ground, 3, 5), Doc.Cell(Ground, 4, 5)));
        Assert.True(Doc.IsDirty);
        // Flip H mirrors the stamp and sets bit 15
        _s.SetFlip(true);
        _s.PaintAt(3, 6);
        Assert.Equal((2 | MapDocument.FlipH, 1 | MapDocument.FlipH), (Doc.Cell(Ground, 3, 6), Doc.Cell(Ground, 4, 6)));
        _s.SetFlip(false);
        Undo.Undo();
        Assert.Equal(0, Doc.Cell(Ground, 3, 6));
        Undo.Redo();
        Assert.Equal(2 | MapDocument.FlipH, Doc.Cell(Ground, 3, 6));
        // Random 100 %: every cell takes a random tile of the stamp
        _s.SelectTile(4, 2, 1);
        _s.SetRandom(100);
        _s.Seed(7);
        _s.BeginStroke();
        for (var x = 0; x < 20; x++) _s.PaintAt(x, 2);
        _s.EndStroke();
        var row = Enumerable.Range(0, 20).Select(x => Doc.Cell(Ground, x, 2)).ToList();
        Assert.All(row, c => Assert.Contains(c, new[] { 4, 5 }));
        Assert.Contains(4, row);
        Assert.Contains(5, row);
        // one stroke = one undo step
        Undo.Undo();
        Assert.All(Enumerable.Range(0, 20), x => Assert.Equal(0, Doc.Cell(Ground, x, 2)));
        Undo.Redo();
        Doc.Save();
        Assert.False(Doc.IsDirty);
        var back = Reload();
        Assert.Equal(Doc.Map.Layers[Ground].Cells, back.Map.Layers[Ground].Cells);
        Assert.Equal(2 | MapDocument.FlipH, back.Cell(Ground, 3, 6));
    }

    [Fact]
    public void Tools_EraseRectFillPickAndStampFromMap()
    {
        _s.Layer = Ground;
        _s.SetAutoTile(false);
        _s.SelectTile(7);
        _s.FillRect(2, 2, 5, 3);
        Assert.All(from y in Enumerable.Range(2, 2) from x in Enumerable.Range(2, 4) select Doc.Cell(Ground, x, y), c => Assert.Equal(7, c));
        _s.SelectTile(3);
        _s.FloodFill(2, 2);
        Assert.Equal(3, Doc.Cell(Ground, 5, 3));
        Assert.Equal(0, Doc.Cell(Ground, 6, 3));
        _s.EraseAt(5, 3);
        Assert.Equal(0, Doc.Cell(Ground, 5, 3));
        _s.PickAt(2, 2);
        Assert.Equal(3, _s.SelectedTile);
        _s.StampFromMap(2, 2, 4, 3);
        Assert.Equal((3, 2), (_s.StampWidth, _s.StampHeight));
        Assert.True(_s.SetStampSize("2 × 1 tiles"));
        Assert.Equal("2 × 1 tiles", _s.StampText);
        Assert.False(_s.SetStampSize("big"));
        // a locked layer cannot be painted
        _s.SetLocked(Ground, true);
        _s.PaintAt(0, 0);
        Assert.Equal(0, Doc.Cell(Ground, 0, 0));
    }

    [Fact]
    public void AutoTile_PaintingAMemberResolvesTheGrassEdgesRules()
    {
        var set = Doc.Meta.AutoTiles.Single();
        Assert.Equal("Grass edges (5)", MapDocument.SetLabel(set));
        _s.Layer = Ground;
        _s.SetAutoTile(true);
        _s.SelectTile(set.Rules[0].Tile);
        // a 3 x 3 block of the set in an empty area: the Figma rules (T-P26) give the top row, the left and right
        // edges, the top-left corner and the inside their tiles
        _s.BeginStroke();
        for (var y = 3; y <= 5; y++)
            for (var x = 4; x <= 6; x++) _s.PaintAt(x, y);
        _s.EndStroke();
        var cells = Doc.Map.Layers[Ground].Cells;
        for (var y = 3; y <= 5; y++)
            for (var x = 4; x <= 6; x++) Assert.Equal(AutoTile.Resolve(cells, Doc.Width, Doc.Height, x, y, set), cells[y * Doc.Width + x]);
        Assert.Equal(PixelArtTiles.Grass, Doc.Cell(Ground, 5, 3));     // Top edge
        Assert.Equal(PixelArtTiles.Bush, Doc.Cell(Ground, 4, 3));      // Corner
        Assert.Equal(PixelArtTiles.Dirt, Doc.Cell(Ground, 4, 4));      // Left edge
        Assert.Equal(PixelArtTiles.Bricks, Doc.Cell(Ground, 6, 4));    // Right edge
        Assert.Equal(PixelArtTiles.Leaves, Doc.Cell(Ground, 5, 4));    // Inner
        // with auto-tile off the tile is painted as is
        _s.SetAutoTile(false);
        _s.SelectTile(PixelArtTiles.Bricks);
        _s.PaintAt(10, 3);
        Assert.Equal(PixelArtTiles.Bricks, Doc.Cell(Ground, 10, 3));
    }

    // ---- P11.29 tile properties ---------------------------------------------------------------------

    [Fact]
    public void TileProperties_CollisionTagAnimationAutoTile_SavedAndUndoable()
    {
        Doc.SetCollision(4, CollisionType.Hazard);
        Doc.SetTag(4, "lava");
        Doc.SetAnimation(4, 2);
        Doc.SetAutoTile(7, "Grass edges");
        Doc.Save();
        var meta = GameProject.Load<TilesetMeta>(GameProject.MetaPath(Doc.TilesetPath!));
        Assert.Equal(CollisionType.Hazard, meta.Tiles[4].Collision);
        Assert.Equal("lava", meta.Tiles[4].Tag);
        Assert.Equal([4, 5], meta.Tiles[4].Animation);
        Assert.Contains(meta.AutoTiles[0].Rules, r => r.Tile == 7);
        Assert.Equal("Grass edges", Doc.SetOf(7)!.Name);
        Undo.Undo();
        Assert.Null(Doc.SetOf(7));
        Undo.Undo();
        Assert.Equal(0, Doc.AnimationFrames(4));
        Undo.Undo();
        Undo.Undo();
        Assert.Equal(CollisionType.None, Doc.Props(4)!.Collision);   // the bush tile
        // counts of the collision section follow the cells
        var solid = Doc.CountCollision(CollisionType.Solid);
        Doc.SetCollision(PixelArtTiles.Grass, CollisionType.OneWay);
        Assert.True(Doc.CountCollision(CollisionType.Solid) < solid);
    }

    // ---- P11.32 auto-tile rules -------------------------------------------------------------------

    [Fact]
    public void AutoTileRules_AddEditDelete()
    {
        var set = "Grass edges";
        var count = Doc.Meta.AutoTiles[0].Rules.Count;
        Doc.AddRule(set, 7);
        Assert.Equal(count + 1, Doc.Meta.AutoTiles[0].Rules.Count);
        Assert.Equal(7, Doc.Meta.AutoTiles[0].Rules[0].Tile);
        Doc.CycleMask(set, 0, 1);
        Assert.Equal(1, Doc.Meta.AutoTiles[0].Rules[0].Mask[1]);
        Doc.CycleMask(set, 0, 1);
        Assert.Equal(0, Doc.Meta.AutoTiles[0].Rules[0].Mask[1]);
        Doc.CycleMask(set, 0, 4);
        Assert.Equal(1, Doc.Meta.AutoTiles[0].Rules[0].Mask[4]);
        Doc.SetRuleTile(set, 0, 6);
        Assert.Equal(6, Doc.Meta.AutoTiles[0].Rules[0].Tile);
        Doc.RemoveRule(set, 0);
        Assert.Equal(count, Doc.Meta.AutoTiles[0].Rules.Count);
        Undo.Undo();
        Assert.Equal(count + 1, Doc.Meta.AutoTiles[0].Rules.Count);
    }

    // ---- P11.30 / P11.31 layers and map settings ---------------------------------------------------

    [Fact]
    public void Layers_AddTypesOnFreeBackgrounds_CollisionOnlyBecomesTheSource()
    {
        Assert.Equal(3, Doc.FreeBg(LayerType.Tile));
        var fg = Doc.AddLayer(LayerType.Tile, "Foreground");
        Assert.Equal(3, Doc.Map.Layers[fg].Bg);
        Assert.Equal(Doc.Width * Doc.Height, Doc.Map.Layers[fg].Cells.Count);
        Assert.Null(Doc.FreeBg(LayerType.Image));
        Assert.Null(Doc.FreeBg(LayerType.Affine));
        Assert.Equal(-1, Doc.AddLayer(LayerType.Image));
        var col = Doc.AddLayer(LayerType.CollisionOnly);
        Assert.Equal(LayerType.CollisionOnly, Doc.Map.Layers[col].Type);
        Assert.Equal(col, Doc.CollisionLayerIndex);
        Doc.Save();
        var back = Reload();
        Assert.Equal(["Sky", "Hills", "Ground", "Foreground", "Collision"], back.Map.Layers.Select(l => l.Name));
        Assert.Equal(4, back.Map.CollisionLayer);
        Undo.Undo();
        Undo.Undo();
        Assert.Equal(3, Doc.Map.Layers.Count);
        Assert.Equal(2, Doc.Map.CollisionLayer);
        // image and rotate / scale layers
        Doc.RemoveLayer(0);
        var img = Doc.AddLayer(LayerType.Image);
        Assert.Equal("sky_bg", Doc.Map.Layers[img].Image);
        Assert.Equal(0, Doc.Map.Layers[img].Bg);
        var affine = Doc.AddLayer(LayerType.Affine);
        Assert.Equal(3, Doc.Map.Layers[affine].Bg);
    }

    [Fact]
    public void MapSettings_ResizeBgSizeColoursWrap_PersistAndChangeThePlan()
    {
        Doc.SetCell(Ground, 29, 19, 7);
        Doc.Resize(40, 22);
        Assert.Equal((40, 22), (Doc.Width, Doc.Height));
        Assert.Equal(7, Doc.Cell(Ground, 29, 19));
        Assert.All(Doc.Map.Layers.Where(Doc.HasCells), l => Assert.Equal(40 * 22, l.Cells.Count));
        var before = _s.Plan();
        Doc.SetBgSize(BgSize.S64x64);
        Doc.SetBpp(8);
        Doc.SetWrap(Ground, true);
        var after = _s.Plan();
        Assert.Equal(before.ScreenBlocks * 2, after.ScreenBlocks);
        Assert.Equal(before.TileBytes * 2, after.TileBytes);
        Doc.Save();
        var text = File.ReadAllText(Doc.Path);
        Assert.Contains("\"bgSize\": \"64x64\"", text);
        Assert.Contains("\"bpp\": 8", text);
        var back = Reload();
        Assert.Equal((BgSize.S64x64, 8, true, 40), (back.BgSize, back.Bpp, back.Map.Layers[Ground].Wrap, back.Width));
        Undo.Undo();
        Undo.Undo();
        Undo.Undo();
        Undo.Undo();
        Assert.Equal((30, 20, BgSize.S64x32, 4), (Doc.Width, Doc.Height, Doc.BgSize, Doc.Bpp));
        Assert.Equal(7, Doc.Cell(Ground, 29, 19));
    }

    [Fact]
    public void UnknownMapFieldsSurviveASave()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Doc.Path))!.AsObject();
        node["futureField"] = 42;
        File.WriteAllText(Doc.Path, node.ToJsonString());
        var d = Reload();
        d.Save();
        Assert.Contains("futureField", File.ReadAllText(Doc.Path));
    }
}

/// <summary>Forest tileset indices of the Sky Hopper fixture art (AgbStudio.Build.Content.SkyHopperArt).</summary>
static class PixelArtTiles
{
    public const int Grass = AgbStudio.Build.Content.SkyHopperArt.TGrass, Dirt = AgbStudio.Build.Content.SkyHopperArt.TDirt,
        Bricks = AgbStudio.Build.Content.SkyHopperArt.TBricks, Bush = AgbStudio.Build.Content.SkyHopperArt.TBush,
        Leaves = AgbStudio.Build.Content.SkyHopperArt.TLeaves;
}
