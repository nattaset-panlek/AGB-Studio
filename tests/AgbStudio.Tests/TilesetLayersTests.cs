using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Graphics;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;

namespace AgbStudio.Tests;

/// <summary>A secondary tileset and two-layer metatiles, from project files to the screen of the booted ROM.</summary>
[Collection("emulator")]
public sealed class TilesetLayersTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");
    static readonly uint Red = Canvas.Rgb(248, 0, 0), Green = Canvas.Rgb(0, 248, 0), Blue = Canvas.Rgb(0, 0, 248);

    // Metatiles side by side, each painted by its function (x, y in 0..15); 0 stays see-through.
    static void AddTileset(GameProject p, string name, TilesetMeta meta, params Func<int, int, uint>[] tiles)
    {
        var c = new Canvas(16 * tiles.Length, 16);
        for (var m = 0; m < tiles.Length; m++)
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++) c.Set(m * 16 + x, y, tiles[m](x, y));
        var file = Path.Combine(p.AssetsDir, "tilesets", name + ".png");
        c.Save(file);
        GameProject.Save(GameProject.MetaPath(file), meta);
    }

    // One scene, one tile layer filled by cell(x, y), and one still actor at (32, 32) unless the caller changes it.
    static GameProject Project(string name, string tileset, Func<int, int, int> cell)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        const int w = 30, h = 20;
        var map = new MapFile { Name = "Forest_01", Tileset = tileset, Width = w, Height = h, CollisionLayer = -1 };
        var cells = new List<int>();
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) cells.Add(cell(x, y));
        map.Layers.Add(new MapLayerData { Name = "Ground", Bg = 1, Type = LayerType.Tile, Priority = 1, Cells = cells });
        GameProject.Save(Path.Combine(p.MapsDir, "Forest_01.map"), map);
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Width = w;
        scene.Height = h;
        scene.Music = new MusicSettings();
        scene.Links.Clear();
        var actor = scene.Actors.First(a => a.Name == "Coin");
        actor.X = actor.Y = 32;
        actor.Priority = 1;
        actor.Group = "";
        actor.Components.Clear();
        scene.Actors.Clear();
        scene.Actors.Add(actor);
        GameProject.Save(scenePath, scene);
        return p;
    }

    uint[] Booted(string rom)
    {
        var core = emu.Core;
        core.Keys = GbaKeys.None;
        core.LoadRom(rom);
        for (var f = 0; f < 360; f++) core.RunFrame();
        var frame = (uint[])core.Frame.Clone();
        core.UnloadRom();
        return frame;
    }

    static uint Screen(uint color) => Gba.ToArgb(Gba.ToBgr555(color));

    static bool Close(uint a, uint b)
    {
        bool Ch(int shift) => Math.Abs((int)((a >> shift) & 255) - (int)((b >> shift) & 255)) <= 8;
        return Ch(16) && Ch(8) && Ch(0);
    }

    static void AssertPixel(uint[] frame, int x, int y, uint color, string what) =>
        Assert.True(Close(frame[y * 240 + x], Screen(color)), $"{what}: ({x},{y}) is {frame[y * 240 + x]:X8}, expected {Screen(color):X8}");

    [Fact]
    public void SecondaryTilesetShowsItsOwnTilesAndThePrimarysAtTheRightPlaces()
    {
        var p = Project("tileset_secondary", "house", (x, _) => x < 8 ? 0 : 1);
        AddTileset(p, "town", new TilesetMeta(), (_, _) => Red, (_, _) => Green);
        AddTileset(p, "house", new TilesetMeta { Primary = "town" }, (_, _) => Red, (_, _) => Blue);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.Contains("&agbasset_tileset_town", File.ReadAllText(Path.Combine(p.BuildDir, "gen", "assets", "tileset_house.cpp")));
        var frame = Booted(r.RomPath);
        AssertPixel(frame, 4, 4, Red, "a tile the primary holds");
        AssertPixel(frame, 200, 100, Blue, "a tile of the secondary");
    }

    [Fact]
    public void APrimaryThatHasAPrimaryIsRefusedInWords()
    {
        var p = Project("tileset_chain", "a", (_, _) => 0);
        AddTileset(p, "a", new TilesetMeta { Primary = "b" }, (_, _) => Red);
        AddTileset(p, "b", new TilesetMeta { Primary = "a" }, (_, _) => Green);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("extends another tileset"));
        AssertPixel(Booted(r.RomPath), 100, 100, Red, "tileset a built on its own");
    }

    // Tile 1 is green and draws tile 2 over the actors; tile 2 is blue on its right half only.
    static GameProject OverProject(string name)
    {
        var p = Project(name, "layers", (_, _) => 1);
        var meta = new TilesetMeta();
        meta.Tiles[1] = new TileProperties { Over = 2 };
        AddTileset(p, "layers", meta, (_, _) => Red, (_, _) => Green, (x, _) => x >= 8 ? Blue : 0);
        return p;
    }

    [Fact]
    public void OverTilesDrawInFrontOfActorsAndTheGroundAndActorsShowElsewhere()
    {
        var p = OverProject("tileset_over");
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var frame = Booted(r.RomPath);
        var actorPixels = 0;
        for (var y = 0; y < 160; y++)
            for (var x = 0; x < 240; x++)
            {
                if (x % 16 >= 8) AssertPixel(frame, x, y, Blue, "the over half, also on the actor");
                else if (!Close(frame[y * 240 + x], Screen(Green)))
                {
                    Assert.InRange(x, 16, 48);  // around the actor, whose X and Y are its pivot
                    Assert.InRange(y, 16, 48);
                    actorPixels++;
                }
            }
        Assert.True(actorPixels > 0, "the actor should show through the see-through half of the over tile");
    }

    [Fact]
    public void SetTileFromAScriptMovesTheOverTileToo()
    {
        var p = OverProject("tileset_over_settile");
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Digger.cs"), """
using GBA;

public class Digger : Behaviour
{
    void Start()
    {
        Scene.SetTile(0, 10, 5, 0);
    }
}
""");
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Components.Add(new ComponentData { Type = "Digger" });
        GameProject.Save(scenePath, scene);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var frame = Booted(r.RomPath);
        // Tile 0 has no over tile: the cell is plain red, where its neighbour still has the blue half.
        AssertPixel(frame, 10 * 16 + 12, 5 * 16 + 8, Red, "the changed cell");
        AssertPixel(frame, 11 * 16 + 12, 5 * 16 + 8, Blue, "the next cell");
    }

    [Fact]
    public void OverLayerGoesOnTheHighestFreeBgAndLeavesLayerNumbersAlone()
    {
        var ts = TilesetConverter.Convert("t", Image(3), new TilesetMeta { Tiles = { [1] = new TileProperties { Over = 2 } } }, []);
        var map = new MapFile { Name = "m", Width = 2, Height = 1 };
        map.Layers.Add(new MapLayerData { Bg = 3, Type = LayerType.Tile, Priority = 2, Cells = [0, 0] });
        map.Layers.Add(new MapLayerData { Bg = 1, Type = LayerType.Tile, Priority = 1, ParallaxX = 0.5, Cells = [1, 0x8001] });
        var e = OverLayers.Expand(map, ts);
        Assert.Null(e.Problem);
        Assert.Equal([-1, 2], e.Partner);
        var over = e.Map.Layers[2];
        Assert.Equal(2, over.Bg);
        Assert.Equal(0, over.Priority);
        Assert.Equal(0.5, over.ParallaxX);
        Assert.Equal([2, 0x8002], over.Cells);
        Assert.Equal(2, map.Layers.Count);
    }

    [Fact]
    public void WithoutOverTilesTheMapIsTheSameObject()
    {
        var ts = TilesetConverter.Convert("t", Image(2), new TilesetMeta(), []);
        var map = new MapFile { Name = "m", Width = 1, Height = 1, Layers = { new MapLayerData { Type = LayerType.Tile, Cells = [0] } } };
        Assert.Same(map, OverLayers.Expand(map, ts).Map);
    }

    [Fact]
    public void NoFreeBgIsSaidInWords()
    {
        var ts = TilesetConverter.Convert("t", Image(3), new TilesetMeta { Tiles = { [1] = new TileProperties { Over = 2 } } }, []);
        var map = new MapFile { Name = "m", Width = 1, Height = 1 };
        for (var b = 0; b < 4; b++) map.Layers.Add(new MapLayerData { Bg = b, Type = LayerType.Tile, Cells = [1] });
        var e = OverLayers.Expand(map, ts);
        Assert.Same(map, e.Map);
        Assert.Contains("no background layer is free", e.Problem);
    }

    [Fact]
    public void AnOverTileOutsideTheTilesetIsReportedAndIgnored()
    {
        var ts = TilesetConverter.Convert("t", Image(2), new TilesetMeta { Tiles = { [0] = new TileProperties { Over = 9 }, [1] = new TileProperties { Over = 1 } } }, []);
        Assert.Equal([-1, -1], ts.Over);
        Assert.Equal(2, ts.Problems.Count(x => x.Message.Contains("over the actors")));
    }

    static ColorImage Image(int metatiles)
    {
        var img = new ColorImage(16 * metatiles, 16);
        for (var i = 0; i < img.Colors.Length; i++)
        {
            img.Colors[i] = (ushort)((i % 16) / 8 + 1);
            img.Transparent[i] = false;
        }
        return img;
    }
}
