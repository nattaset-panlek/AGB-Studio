using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;
using Xunit.Abstractions;

namespace AgbStudio.Tests;

/// <summary>Reads and moves things in a running game through the debug channel (play tests).</summary>
public static class Play
{
    /// <summary>Address of the game state (GameState) of the running game.</summary>
    public static uint StateAddress(PlaySession s) => s.Core.ReadU32(s.Channel!.Game + 8);

    /// <summary>The scene the running game shows (GameDef.scenes index).</summary>
    public static int Scene(PlaySession s) => s.Channel is { Game: not 0 } ch ? s.Core.ReadU16(ch.Game) : -1;

    /// <summary>
    /// Byte offset of a save field in the generated GameState: the 8-byte SaveHeader, then the fields largest first
    /// (GameBuilder writes them that way, so none needs padding).
    /// </summary>
    public static uint Offset(SaveSettings save, string field)
    {
        static int Align(string t) => t.Trim() is "int" or "fixed" or "fixed2" ? 4 : t.Trim() == "short" || t.Contains('[') ? 2 : 1;
        var offset = 8;
        foreach (var f in save.Fields.OrderByDescending(f => Align(f.Type)))
        {
            if (f.Name == field) return (uint)offset;
            offset += f.SizeBytes();
        }
        throw new ArgumentException($"no save field {field}");
    }

    public static int ReadInt(PlaySession s, SaveSettings save, string field) => (int)s.Core.ReadU32(StateAddress(s) + Offset(save, field));

    /// <summary>Moves an actor of the running game (between frames) by writing its position.</summary>
    public static void Teleport(PlaySession s, string name, int x, int y)
    {
        var ch = s.Channel!;
        for (uint i = 0; i < ch.ActorCount; i++)
        {
            var row = ch.ActorTable + i * ch.ActorStride;
            if (Inspector.ReadString(s.Core, s.Core.ReadU32(row)) != name) continue;
            uint rawX = s.Core.ReadU32(row + 4), rawY = s.Core.ReadU32(row + 8), actor = s.Core.ReadU32(row + 24);
            // The actor's position: the first two words that hold the published x and y.
            for (uint off = 0; off < 64; off += 4)
                if (s.Core.ReadU32(actor + off) == rawX && s.Core.ReadU32(actor + off + 4) == rawY)
                {
                    s.Core.WriteU32(actor + off, (uint)(x * 256));
                    s.Core.WriteU32(actor + off + 4, (uint)(y * 256));
                    return;
                }
            throw new InvalidOperationException($"position of {name} not found");
        }
        throw new InvalidOperationException($"no actor {name}");
    }

    public static LiveActor? Actor(PlaySession s, string name) => s.Actors().FirstOrDefault(a => a.Name == name);

    /// <summary>Steps until the scene `index` runs with the player at `spawnX`; returns the frames it took.</summary>
    public static int WaitForScene(PlaySession s, int index, int spawnX, int max = 180)
    {
        for (var i = 1; i <= max; i++)
        {
            s.Step();
            if (Scene(s) == index && Actor(s, "Player") is { } p && (int)p.X == spawnX) return i;
        }
        throw new Xunit.Sdk.XunitException($"scene {index} with the player at x {spawnX} did not load (scene {Scene(s)}, player {Actor(s, "Player")})");
    }

    public static void Hold(PlaySession s, GbaKeys keys, int frames)
    {
        s.Keys = keys;
        for (var i = 0; i < frames; i++) s.Step();
        s.Keys = 0;
    }
}

/// <summary>
/// The game framework in a built ROM: a WaitUntil cutscene, a database script cell, Scene.Pause from C#, prefabs,
/// a link "Only if" condition, map scripts OnLoad / OnEnter / OnExit, a map shared by two scenes and stable flag ids.
/// </summary>
[Collection("emulator")]
public sealed class FrameworkPlayTests(EmulatorFixture emu, ITestOutputHelper log)
{
    static readonly Lazy<(GameProject P, BuildResult R)> s_game = new(BuildGame);

    static void Script(GameProject p, string name, string body) =>
        File.WriteAllText(Path.Combine(p.ScriptsDir, name + ".cs"), "using GBA;\n\n" + body.Trim() + "\n");

    static (GameProject, BuildResult) BuildGame()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "framework_game");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var settings = new ProjectFile();
        settings.General.GameTitle = "Framework";
        settings.General.RomTitle = "FRAMEWORK";
        settings.General.StartScene = "Arena";
        foreach (var f in new[] { "score", "coins", "gate", "ticks", "loads", "enters", "exits" })
            settings.Save.Fields.Add(new SaveField { Name = f, Type = "int" });
        var p = GameProject.Create(dir, "Framework", settings);
        SampleProjects.WriteCommonArt(p);
        var tiles = Path.Combine(p.AssetsDir, "tilesets", "forest_tiles.png");
        PixelArt.ForestTiles().Save(tiles);
        GameProject.Save(GameProject.MetaPath(tiles), SampleProjects.ForestTilesetMeta());
        GameProject.Save(Path.Combine(p.MapsDir, "Arena.map"), SampleProjects.GroundMap("Arena", 30, 10, -1, -1, -1));

        Script(p, "PlayerMove", """
public class PlayerMove : Behaviour
{
    public fixed speed = 1.5;
    PlatformerBody body;

    void Start() => body = Get<PlatformerBody>();

    void Update()
    {
        body.vx = Input.AxisX * speed;
        if (Input.Pressed(Key.Start)) Scene.Pause();
    }
}
""");
        Script(p, "Ticker", """
public class Ticker : Behaviour
{
    int t;

    void Update()
    {
        Game.ticks += 1;
        t += 1;
        if (t >= 30) { t = 0; Game.coins += 1; }
    }
}
""");
        Script(p, "GemPickup", """
public class GemPickup : Behaviour
{
    void OnTouch(Actor other)
    {
        if (other.tag == "Player") { Items.Get(ItemId.Gem).effect(); actor.Destroy(); }
    }
}
""");
        Script(p, "Spawner", "public class Spawner : Behaviour\n{\n    void Start() { Scene.Spawn(\"Spark\", 300, 100); }\n}");
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Arena.cs"), """
using System.Threading.Tasks;
using GBA;

public class Arena : SceneScript
{
    void OnEnter() { WatchGate(); }

    [Cutscene]
    static async Task WatchGate()
    {
        await WaitUntil(() => Game.coins >= 2);
        Game.gate = 1;
    }
}
""");
        // Items.db: the Effect column is a script cell.
        GameProject.Save(Path.Combine(p.DataDir, "Items.db"), new TableFile
        {
            Name = "Items",
            Fields = { new FieldDef { Name = "id", Type = "enum" }, new FieldDef { Name = "price", Type = "int" }, new FieldDef { Name = "effect", Type = "script" } },
            Rows = { new() { ["id"] = "Gem", ["price"] = "25", ["effect"] = "score += price" }, new() { ["id"] = "Rock", ["price"] = "1", ["effect"] = "" } },
        });
        // A dialogue declares a saved flag (its id is stored in the project).
        File.WriteAllText(Path.Combine(p.DialogueDir, "intro.dlg"),
            "{\n  \"format\": 1,\n  \"name\": \"intro\",\n  \"conversations\": [\n    { \"id\": \"talk\", \"lines\": [ { \"command\": \"setFlag\", \"flag\": \"MetGem\", \"value\": true } ] }\n  ]\n}\n");
        // Make Prefab output: prefabs/Spark.prefab.
        var spark = new ActorData { Name = "Spark", Kind = ActorKind.Empty, Sprite = "coin", Animation = "spin", X = 77, Y = 55, Priority = 2 };
        GameProject.Save(Path.Combine(p.PrefabsDir, "Spark.prefab"), spark);

        ActorData Player(int x) => new()
        {
            Name = "Player", Kind = ActorKind.Player, Sprite = "hero", Animation = "idle", X = x, Y = 100, Tag = "Player",
            Components = { new ComponentData { Type = "PlatformerBody" }, new ComponentData { Type = "PlayerMove" } },
        };
        var arena = new SceneFile
        {
            Name = "Arena", Width = 30, Height = 10, Map = "Arena",
            Events = new SceneEvents { OnLoad = "Game.loads += 1", OnEnter = "Game.enters += 1", OnExit = "Game.exits += 1" },
            Actors =
            {
                Player(40),
                new ActorData { Name = "Ticker", Kind = ActorKind.Empty, X = 8, Y = 8, Components = { new ComponentData { Type = "Ticker" } } },
                new ActorData { Name = "Gem", Kind = ActorKind.Pickup, Sprite = "coin", X = 120, Y = 144, Tag = "Coin", Components = { new ComponentData { Type = "GemPickup" } } },
                new ActorData { Name = "Exit", Kind = ActorKind.TriggerZone, X = 200, Y = 144, ZoneW = 16, ZoneH = 32 },
                new ActorData { Name = "Spawner", Kind = ActorKind.Empty, X = 8, Y = 8, Components = { new ComponentData { Type = "Spawner" } } },
            },
            Links = { new LinkData { From = "Exit", To = "Bonus", SpawnX = 60, SpawnY = 100, Transition = Transition.Fade, Duration = 0.25, OnlyIf = "Game.coins >= 10" } },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Arena.scene"), arena);
        // The bonus room shows the same map (one copy in ROM) and holds a placed copy of the prefab.
        var placed = GameProject.Load<ActorData>(Path.Combine(p.PrefabsDir, "Spark.prefab"));
        placed.Name = "Spark (placed)";
        placed.Prefab = "Spark";
        placed.X = 150;
        placed.Y = 120;
        GameProject.Save(Path.Combine(p.ScenesDir, "Bonus.scene"), new SceneFile { Name = "Bonus", Width = 30, Height = 10, Map = "Arena", Actors = { Player(60), placed } });

        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return (p, r);
    }

    [Fact]
    public void BuildMakesPrefabTemplatesSharesMapsAndKeepsFlagIds()
    {
        var (p, r) = s_game.Value;
        var gen = Path.Combine(p.BuildDir, "gen");
        // P4.07: each prefab is a const ActorDef in ROM, listed by name for Scene.Spawn.
        var prefabs = File.ReadAllText(Path.Combine(gen, "prefabs.cpp"));
        Assert.Contains("const agb::ActorDef agbasset_prefab_Spark = {\"Spark\", agb::ActorKind::Empty, &agbasset_sprite_coin, 0, 0, 0, 2, \"spin\",", prefabs);
        Assert.Contains("extern const agb::PrefabDef agb_prefabs[] = {\n    {\"Spark\", &agbasset_prefab_Spark},\n};", prefabs.Replace("\r\n", "\n"));
        var game = File.ReadAllText(Path.Combine(gen, "game.cpp"));
        Assert.Contains("agb_prefabs, 1, ", game);  // then the MusicMeta table (P3.11)
        // P5.03: one map file, one copy of its layers, used by both scenes.
        Assert.Equal(["map_Arena.cpp"], Directory.GetFiles(Path.Combine(gen, "maps")).Select(Path.GetFileName));
        foreach (var scene in new[] { "Arena", "Bonus" })
        {
            var cpp = File.ReadAllText(Path.Combine(gen, "scenes", $"scene_{scene}.cpp"));
            Assert.Contains("extern const agb::data::MapLayer agbasset_map_Arena_layers[];", cpp);
            Assert.DoesNotContain("const unsigned short agbasset_map_", cpp);
        }
        // P9.06: the Items table points at the compiled Effect cell.
        Assert.Contains("&agb_dbscript_Items_Gem_effect", File.ReadAllText(Path.Combine(gen, "data", "db_Items.cpp")));
        // P4.08: the dialogue's saved flag got an id, kept in the project file for the next builds.
        Assert.Equal(new Dictionary<string, int> { ["MetGem"] = 0 }, p.Settings.Save.FlagIds);
        Assert.Equal(0, GameProject.Open(p.ProjectPath).Settings.Save.FlagIds["MetGem"]);
        Assert.True(File.Exists(r.RomPath));
    }

    [Fact]
    public void CutsceneScriptCellPauseOnlyIfLinkAndMapScriptsRunInTheRom()
    {
        var (p, r) = s_game.Value;
        var save = p.Settings.Save;
        int Get(PlaySession s, string f) => Play.ReadInt(s, save, f);
        using var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 120 && Play.Actor(s, "Player") == null; i++) s.Step();
        Assert.NotNull(Play.Actor(s, "Player"));
        // P5.06: OnLoad and OnEnter ran once (Scene Settings events), OnExit not yet.
        Assert.Equal((1, 1, 0), (Get(s, "loads"), Get(s, "enters"), Get(s, "exits")));

        // P6.12: the cutscene started in OnEnter waits until Game.coins >= 2, checked every frame.
        var frames = 0;
        while (Get(s, "gate") == 0 && frames < 200)
        {
            Assert.True(Get(s, "coins") < 2, $"coins {Get(s, "coins")} but the gate is still closed");
            s.Step();
            frames++;
        }
        Assert.Equal(1, Get(s, "gate"));
        Assert.Equal(2, Get(s, "coins"));
        log.WriteLine($"WaitUntil finished after {frames} frames");

        // P4.07: Spawner's Start copied the prefab template.
        var spark = Play.Actor(s, "Spark");
        Assert.NotNull(spark);
        Assert.Equal((300.0, 100.0, true), (spark!.X, spark.Y, spark.Visible));

        // P9.06: touching the gem runs Items.Gem's Effect cell "score += price".
        for (var i = 0; i < 200 && Play.Actor(s, "Gem") != null; i++)
        {
            s.Keys = GbaKeys.Right;
            s.Step();
        }
        s.Keys = 0;
        Assert.Null(Play.Actor(s, "Gem"));
        Assert.Equal(25, Get(s, "score"));

        // P4.11: Start calls Scene.Pause (no pause menu in this game): scripts, physics and timers stop until Start.
        Play.Hold(s, GbaKeys.Start, 1);
        var ticks = Get(s, "ticks");
        var x = Play.Actor(s, "Player")!.X;
        Play.Hold(s, GbaKeys.Right, 20);
        Assert.Equal(ticks, Get(s, "ticks"));
        Assert.Equal(x, Play.Actor(s, "Player")!.X);
        Play.Hold(s, GbaKeys.Start, 1);
        Assert.Equal(ticks, Get(s, "ticks"));  // the resuming frame runs no scripts
        Play.Hold(s, 0, 5);
        Assert.Equal(ticks + 5, Get(s, "ticks"));

        // P5.05: the Exit link has "Only if Game.coins >= 10": standing in it does nothing until then.
        for (var i = 0; i < 200 && Play.Actor(s, "Player")!.X < 206; i++)
        {
            s.Keys = GbaKeys.Right;
            s.Step();
        }
        s.Keys = 0;
        Assert.True(Get(s, "coins") < 10, $"coins {Get(s, "coins")}");
        while (Get(s, "coins") < 10 && frames < 2000)
        {
            Assert.Equal(0, Play.Scene(s));
            s.Step();
            frames++;
        }
        Play.WaitForScene(s, 1, 60);
        // P5.06: OnExit ran when the Arena closed.
        Assert.Equal((1, 1, 1), (Get(s, "loads"), Get(s, "enters"), Get(s, "exits")));
        // The bonus room (same map) holds the placed prefab copy.
        Assert.Equal((150.0, 120.0), (Play.Actor(s, "Spark (placed)")!.X, Play.Actor(s, "Spark (placed)")!.Y));
        Assert.DoesNotContain(s.Log, l => l.Contains("warning: Spawn", StringComparison.Ordinal));
    }
}

/// <summary>
/// P4.X1, P5.07, P5.X1: the Sky Hopper fixture built and played: the Forest_01 slice at 60 fps, and every scene
/// reached through its World Map links (on touch with "Only if", door, respawn) with their transitions.
/// </summary>
[Collection("emulator")]
public sealed class SkyHopperPlayTests(EmulatorFixture emu, ITestOutputHelper log)
{
    static readonly Lazy<(GameProject P, BuildResult R)> s_game = new(() =>
    {
        var p = SkyHopper.Create(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "skyhopper_play"));
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return (p, r);
    });

    // GameDef.scenes is sorted by file name.
    const int Cave = 0, Forest1 = 1, Forest2 = 2, Summit = 3, Title = 4;

    static PlaySession StartInForest(EmulatorFixture emu, string rom)
    {
        var s = new PlaySession(emu.Core);
        s.Start(rom);
        for (var i = 0; i < 120 && Play.Scene(s) != Title; i++) s.Step();
        Assert.Equal(Title, Play.Scene(s));
        Play.Hold(s, 0, 20);
        // Title menu: Start (the first item) loads Forest_01 with a fade.
        Play.Hold(s, GbaKeys.A, 1);
        Play.WaitForScene(s, Forest1, 40);
        return s;
    }

    static bool GameplayOverrun(string line) =>
        line.Contains("took longer than one frame", StringComparison.Ordinal) && line.Contains(": update)", StringComparison.Ordinal);

    [Fact]
    public void ForestSliceRunsAt60FpsWithPlayerSlimesCoinsAndHud()
    {
        var (_, r) = s_game.Value;
        using var s = StartInForest(emu, r.RomPath);
        var names = s.Actors().Select(a => a.Name).ToList();
        Assert.Contains("Player", names);
        Assert.Contains("Slime", names);
        Assert.Contains("Slime (1)", names);
        Assert.Equal(5, names.Count(n => n.StartsWith("Coin", StringComparison.Ordinal)));
        Assert.Contains("HUD · Score", names);
        // 10 seconds of play: walk, jump, turn. No gameplay frame may miss its VBlank.
        var before = s.Profiler()!.Overruns;
        uint worst = 0;
        for (var f = 0; f < 600; f++)
        {
            s.Keys = (f / 120 % 2 == 0 ? GbaKeys.Right : GbaKeys.Left) | (f % 40 < 14 ? GbaKeys.A : 0);
            s.Step();
            var prof = s.Profiler()!;
            worst = Math.Max(worst, prof.Logic + prof.Render + prof.Audio);
        }
        s.Keys = 0;
        var after = s.Profiler()!;
        log.WriteLine($"Forest_01: worst frame {worst} cycles ({100.0 * worst / ProfilerStats.CyclesPerFrame:0.0}% of a frame), overruns {after.Overruns - before}");
        Assert.Equal(before, after.Overruns);
        Assert.True(worst < ProfilerStats.CyclesPerFrame, $"worst frame {worst} cycles");
        Assert.DoesNotContain(s.Log, GameplayOverrun);
        Assert.Equal(Forest1, Play.Scene(s));
    }

    [Fact]
    public void AllFiveScenesLoadThroughTheirLinks()
    {
        var (p, r) = s_game.Value;
        using var s = StartInForest(emu, r.RomPath);
        var visited = new List<string> { "Title", "Forest_01" };
        // The test moves the player straight to each trigger, which moves the camera across the map in one frame (like
        // Camera.SetPosition or a respawn): the layers redraw their whole windows, at most two a frame, without an overrun.
        void Teleport(int x, int y)
        {
            Play.Teleport(s, "Player", x, y);
            Play.Hold(s, 0, 4);
        }
        int Go(int scene, int spawnX, string name, GbaKeys keys = 0, int minFrames = 0)
        {
            if (keys != 0) Play.Hold(s, keys, 1);
            var n = Play.WaitForScene(s, scene, spawnX);
            log.WriteLine($"-> {name} in {n} frames");
            Assert.True(n >= minFrames, $"{name}: the transition took {n} frames");
            visited.Add(name);
            Play.Hold(s, 0, 10);
            return n;
        }

        // Forest_01 Goal Flag -> Forest_02 (on touch, Only if Game.coins >= 10, fade 0.5 s).
        Teleport(458, 304);
        Play.Hold(s, 0, 26);
        Assert.Equal(Forest1, Play.Scene(s));  // no coins yet: the link does not fire
        var coins = Play.StateAddress(s) + Play.Offset(p.Settings.Save, "coins");
        BitConverter.TryWriteBytes(s.Core.Memory(coins, 2), (short)10);
        Teleport(458, 304);
        Go(Forest2, 24, "Forest_02", minFrames: 26);

        // Forest_02 Summit Door -> Summit (door: needs Up, slide 0.5 s).
        Teleport(498, 304);
        Play.Hold(s, 0, 16);
        Assert.Equal(Forest2, Play.Scene(s));
        Teleport(498, 304);
        Go(Summit, 24, "Summit", GbaKeys.Up, minFrames: 30);

        // Summit Back Door -> Forest_02 (door, fade).
        Teleport(12, 144);
        Go(Forest2, 470, "Forest_02 (back)", GbaKeys.Up);

        // Forest_02 Fall in pit -> Forest_01 (respawn, cut).
        Teleport(192, 330);
        var cut = Go(Forest1, 40, "Forest_01 (respawn)");

        // Forest_01 Exit -> Cave_01 (door, fade 0.5 s).
        Teleport(308, 304);
        Go(Cave, 32, "Cave_01", GbaKeys.Up, minFrames: 30);

        // Cave_01 Cave Exit -> Forest_01 (door, fade).
        Teleport(368, 624);
        Go(Forest1, 300, "Forest_01 (from the cave)", GbaKeys.Up);

        Assert.Equal(SkyHopper.SceneNames.Order(), visited.Select(v => v.Split(' ')[0]).Distinct().Order());
        Assert.True(cut < 30, $"the cut took {cut} frames");
        Assert.DoesNotContain(s.Log, GameplayOverrun);
    }
}
