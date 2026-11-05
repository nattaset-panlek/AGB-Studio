using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;
using Xunit.Abstractions;

namespace AgbStudio.Tests;

/// <summary>Phase 18: what the Scene Editor's start marker and collision brush save is what the booted ROM does.</summary>
[Collection("emulator")]
public sealed class SceneEditorRomTests(EmulatorFixture emu, ITestOutputHelper log)
{
    static GameProject Project(string name) =>
        SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", name));

    static string Build(GameProject p)
    {
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return r.RomPath;
    }

    static string ScenePath(GameProject p) => Path.Combine(p.ScenesDir, "Forest_01.scene");

    static void MovePlayer(GameProject p, int x, int y)
    {
        var scene = GameProject.Load<SceneFile>(ScenePath(p));
        var player = scene.Actors.Single(a => a.Kind == ActorKind.Player);
        player.X = x;
        player.Y = y;
        GameProject.Save(ScenePath(p), scene);
    }

    static LiveActor WaitForPlayer(PlaySession s)
    {
        for (var i = 0; i < 120 && Play.Actor(s, "Player") == null; i++) s.Step();
        return Assert.IsType<LiveActor>(Play.Actor(s, "Player"));
    }

    [Fact]
    public void TheRomStartsWhereTheStartMarkerWasDragged()
    {
        var p = Project("start_marker");
        MovePlayer(p, 72, 100);
        using var s = new PlaySession(emu.Core);
        s.Start(Build(p));
        var player = WaitForPlayer(s);
        Assert.Equal(0, Play.Scene(s));
        Assert.Equal(72, (int)player.X);
    }

    [Fact]
    public void ActorsStopOnCellsOfACollisionOnlyLayer_AndTheArtLayerNoLongerStopsThem()
    {
        var p = Project("collision_brush");
        var mapPath = Path.Combine(p.MapsDir, "Forest_01.map");
        var map = GameProject.Load<MapFile>(mapPath);
        // what the brush saves: a Solid tile in row 4 under the player, in a layer the art does not use
        var solid = SampleProjects.ForestTilesetMeta().Tiles.First(t => t.Value.Collision == CollisionType.Solid).Key;
        var cells = new int[map.Width * map.Height];
        for (var x = 0; x < map.Width; x++) cells[4 * map.Width + x] = solid;
        map.Layers.Add(new MapLayerData { Name = "Walls", Type = LayerType.CollisionOnly, Bg = 0, Cells = [.. cells] });
        map.CollisionLayer = map.Layers.Count - 1;
        GameProject.Save(mapPath, map);
        MovePlayer(p, 40, 40);

        using var s = new PlaySession(emu.Core);
        s.Start(Build(p));
        WaitForPlayer(s);
        Play.Hold(s, 0, 120);
        var player = Assert.IsType<LiveActor>(Play.Actor(s, "Player"));
        log.WriteLine($"player stopped at y {player.Y}");
        // the top of row 4 is y 64; the sample map's own ground is far below it, at row 8 and down
        Assert.InRange(player.Y, 64 - 8, 64 + 8);
        // standing still: Vy itself goes up and down under a pixel a frame (PlatformerBody adds gravity, then hits the ground)
        Assert.True(player.OnGround, "the player should stand on the painted cells");
        // and its position by under a pixel with it, so the pixel it is drawn at stays the same
        var y = Math.Floor(player.Y);
        for (var f = 0; f < 30; f++)
        {
            Play.Hold(s, 0, 1);
            Assert.Equal(y, Math.Floor(Assert.IsType<LiveActor>(Play.Actor(s, "Player")).Y));
        }
    }
}
