using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Graphics;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>Connected scenes in the booted ROM: the next scene shows past the edge, the player walks across with no load or
/// fade, its collision counts, and the way back works as well; scenes that cannot be joined are refused in words.</summary>
[Collection("emulator")]
public sealed class ConnectedScenesTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");
    static readonly uint Red = Canvas.Rgb(248, 0, 0), Blue = Canvas.Rgb(0, 0, 248), Green = Canvas.Rgb(0, 248, 0);
    const int MapW = 30, MapH = 20;

    // Forest_01 is red, Forest_02 is blue with a wall of green tiles at column `wallX`; a top-down player starts near the east edge.
    static GameProject Project(string name, MapSide side = MapSide.East, int wallX = 12, Action<SceneFile, SceneFile>? change = null)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        var c = new Canvas(48, 16);
        c.Rect(0, 0, 16, 16, Red);
        c.Rect(16, 0, 16, 16, Blue);
        c.Rect(32, 0, 16, 16, Green);
        var file = Path.Combine(p.AssetsDir, "tilesets", "joined.png");
        c.Save(file);
        GameProject.Save(GameProject.MetaPath(file), new TilesetMeta { Tiles = { [2] = new TileProperties { Collision = CollisionType.Solid } } });
        void Map(string map, Func<int, int> cell)
        {
            var cells = new List<int>();
            for (var y = 0; y < MapH; y++)
                for (var x = 0; x < MapW; x++) cells.Add(cell(x));
            GameProject.Save(Path.Combine(p.MapsDir, map + ".map"), new MapFile
            {
                Name = map, Tileset = "joined", Width = MapW, Height = MapH, CollisionLayer = 0,
                Layers = { new MapLayerData { Name = "Ground", Bg = 1, Type = LayerType.Tile, Priority = 1, Cells = cells } },
            });
        }
        Map("Forest_01", _ => 0);
        Map("Forest_02", x => x == wallX ? 2 : 1);
        SceneFile Scene(string scene, int x)
        {
            var s = GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, scene + ".scene"));
            s.Width = MapW;
            s.Height = MapH;
            s.Music = new MusicSettings();
            s.Links.Clear();
            s.Backdrop = "#000000";
            var player = s.Actors.First(a => a.Name == "Player");
            player.X = x;
            player.Y = 120;
            player.Components.Clear();
            player.Components.Add(new ComponentData { Type = "TopDownMover", Fields = { ["speed"] = "2" } });
            s.Actors.Clear();
            s.Actors.Add(player);
            return s;
        }
        var a = Scene("Forest_01", MapW * 16 - 150);
        var b = Scene("Forest_02", 100);
        a.Connections = [new SceneConnection { Side = side, Scene = "Forest_02" }];
        change?.Invoke(a, b);
        GameProject.Save(Path.Combine(p.ScenesDir, "Forest_01.scene"), a);
        GameProject.Save(Path.Combine(p.ScenesDir, "Forest_02.scene"), b);
        return p;
    }

    static bool Close(uint a, uint b)
    {
        bool Ch(int shift) => Math.Abs((int)((a >> shift) & 255) - (int)((b >> shift) & 255)) <= 8;
        return Ch(16) && Ch(8) && Ch(0);
    }

    static uint Screen(uint color) => Gba.ToArgb(Gba.ToBgr555(color));

    // Where the top row of the screen turns from red to the next scene (blue, with its green wall), 240 if it is all red, -1 if none is red; fails on anything else.
    static int SeamOnScreen(uint[] frame, int f)
    {
        var seam = -1;
        for (var x = 0; x < 240; x++)
        {
            var px = frame[x];
            var red = Close(px, Screen(Red));
            Assert.True(red || Close(px, Screen(Blue)) || Close(px, Screen(Green)), $"frame {f}: pixel {x} of the top row is {px:X8}");
            if (!red && seam < 0) seam = x;
            if (red) Assert.True(seam < 0, $"frame {f}: red again at {x} after blue from {seam}");
        }
        return seam < 0 ? 240 : seam == 0 ? -1 : seam;
    }

    static int PlayerX(LibretroCore core, DebugChannel ch) => (int)core.ReadU32(ch.ActorTable + 4) / 256;

    [Fact]
    public void ThePlayerWalksIntoTheNextSceneWithNoLoadAndTheScreenNeverJumps()
    {
        var r = new GameBuilder(Project("connected_walk"), new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var core = emu.Core;
        core.Keys = GbaKeys.None;
        core.LoadRom(r.RomPath);
        for (var f = 0; f < 360; f++) core.RunFrame();
        var ch = DebugChannel.Find(core);
        Assert.NotNull(ch);
        var startX = PlayerX(core, ch!);
        Assert.InRange(startX, MapW * 16 - 160, MapW * 16 - 140);

        core.Keys = GbaKeys.Right;
        var last = SeamOnScreen(core.Frame, 0);
        var crossed = false;
        for (var f = 1; f < 600 && !crossed; f++)
        {
            core.RunFrame();
            var seam = SeamOnScreen(core.Frame, f);
            if (seam >= 0 && last >= 0) Assert.InRange(last - seam, 0, 3);  // at most the player's speed, whatever the frame
            last = seam;
            crossed = PlayerX(core, ch!) < 100;
        }
        Assert.True(crossed, $"the player never crossed: x {PlayerX(core, ch!)}");
        Assert.InRange(last, 0, 240);  // the old scene still shows behind, drawn as it was
        Assert.True(last < 240, "the blue scene should have been on the screen before the player got there");

        // The new scene's wall stops the player: the wall's collision is the new scene's own.
        for (var f = 0; f < 200; f++) core.RunFrame();
        Assert.InRange(PlayerX(core, ch!), 170, 195);

        // And back again: the old scene shows on the left, then the player is in it.
        core.Keys = GbaKeys.Left;
        var back = false;
        for (var f = 0; f < 300 && !back; f++)
        {
            core.RunFrame();
            _ = SeamOnScreen(core.Frame, f);
            back = PlayerX(core, ch!) > MapW * 16 - 16;
        }
        Assert.True(back, $"the player did not go back: x {PlayerX(core, ch!)}");
        // Walking on in the old scene shows it as it was, far from the join.
        for (var f = 0; f < 120; f++) core.RunFrame();
        Assert.Equal(240, SeamOnScreen(core.Frame, 0));
        core.UnloadRom();
    }

    [Fact]
    public void AJoinNamedInOneSceneCountsForBothAndTheBuildWritesIt()
    {
        var p = Project("connected_gen", MapSide.East);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var one = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "scenes", "scene_Forest_01.cpp"));
        var two = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "scenes", "scene_Forest_02.cpp"));
        Assert.Contains($", {MapW}, 0}},", one);
        Assert.Contains($", {-MapW}, 0}},", two);
    }

    [Theory]
    [InlineData("tileset", "different tilesets")]
    [InlineData("hud", "different Hud")]
    [InlineData("label", "Text Label")]
    [InlineData("missing", "no scene has that name")]
    public void ScenesThatCannotBeJoinedAreRefusedInWords(string what, string message)
    {
        var p = Project("connected_refused_" + what, change: (a, b) =>
        {
            if (what == "hud") a.Hud = "Status";
            if (what == "label") b.Actors.Add(new ActorData { Name = "Sign", Kind = ActorKind.TextLabel, Text = "x" });
            if (what == "missing") a.Connections![0].Scene = "Nowhere";
        });
        if (what == "tileset")
        {
            var c = new Canvas(16, 16);
            c.Rect(0, 0, 16, 16, Red);
            var file = Path.Combine(p.AssetsDir, "tilesets", "other.png");
            c.Save(file);
            GameProject.Save(GameProject.MetaPath(file), new TilesetMeta());
            var path = Path.Combine(p.MapsDir, "Forest_02.map");
            var map = GameProject.Load<MapFile>(path);
            map.Tileset = "other";
            for (var i = 0; i < map.Layers[0].Cells.Count; i++) map.Layers[0].Cells[i] = 0;
            GameProject.Save(path, map);
        }
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.False(r.Success);
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains(message));
    }
}
