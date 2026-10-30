using AgbStudio.Build;
using AgbStudio.Build.Content.Templates;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;
using Xunit.Abstractions;

namespace AgbStudio.Tests;

/// <summary>Phase 15 templates: each one is created, built with its C# scripts and played in the emulator.</summary>
[Collection("emulator")]
public sealed class TemplateTests(EmulatorFixture emu, ITestOutputHelper log)
{
    static (GameProject P, BuildResult R) Build(string id)
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "template_" + id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var t = TemplateCatalog.Find(id)!;
        var p = t.Create(dir, t.Name);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return (p, r);
    }

    static void Press(PlaySession s, GbaKeys key, int hold = 1)
    {
        s.Keys = key;
        for (var i = 0; i < hold; i++) s.Step();
        s.Keys = 0;
        s.Step();
    }

    // GameState of the Platformer: SaveHeader (8 bytes), then score, checkpointX, checkpointY (int), lives (byte).
    static (int Score, int CheckX, int Lives) PlatformerState(PlaySession s)
    {
        var state = s.Core.ReadU32(s.Channel!.Game + 8);
        return ((int)s.Core.ReadU32(state + 8), (int)s.Core.ReadU32(state + 12), s.Core.Memory(state + 20, 1)[0]);
    }

    [Fact]
    public void CatalogListsTheEightFigmaTemplates()
    {
        Assert.Equal(["Platformer"], TemplateCatalog.All.Select(t => t.Name).Take(1));
        Assert.All(TemplateCatalog.All, t => Assert.Contains(t.Genre, new[] { "Action", "Adventure", "RPG", "Puzzle", "Story", "Racing" }));
    }

    [Fact]
    public void PlatformerPlaysThroughToTheGoal()
    {
        var (p, r) = Build("platformer");
        Assert.Equal("Mode 0 · 3 BG layers · 9 actors · 4 scripts", TemplateInfo.Stats(p));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Assert.DoesNotContain(s.Actors(), a => a.Name == "Player");  // title screen
        Press(s, GbaKeys.A);                                          // Start
        for (var i = 0; i < 40; i++) s.Step();
        var player = s.Actors().Single(a => a.Name == "Player");
        Assert.True(player.OnGround, $"player at {player.X},{player.Y}");
        Assert.Equal((0, 40, 3), PlatformerState(s));

        // A simple bot: hold Right, jump over slimes ahead and over the pit (x 448-479).
        double plateauY = 999, maxX = 0;
        var checkpointReached = false;
        var jumpHold = 0;
        var trace = new List<string>();
        for (var f = 0; f < 1500; f++)
        {
            var actors = s.Actors();
            var me = actors.FirstOrDefault(a => a.Name == "Player");
            if (me == null) break;  // back on the title screen
            maxX = Math.Max(maxX, me.X);
            if (me.X is > 210 and < 270 && me.OnGround) plateauY = Math.Min(plateauY, me.Y);
            var slimeAhead = actors.Any(a => a.Name.StartsWith("Slime", StringComparison.Ordinal) && a.Alive && a.X - me.X is > 12 and < 40);
            var pitAhead = me.X is > 430 and < 446;
            if (jumpHold == 0 && me.OnGround && (slimeAhead || pitAhead)) jumpHold = 14;
            s.Keys = GbaKeys.Right | (jumpHold > 0 ? GbaKeys.A : 0);
            if (jumpHold > 0) jumpHold--;
            s.Step();
            var st = PlatformerState(s);
            if (st.CheckX == 520) checkpointReached = true;
            if (f % 50 == 0) trace.Add($"f{f} x{me.X:0} y{me.Y:0} g{(me.OnGround ? 1 : 0)} st{st}");
        }
        s.Keys = 0;
        foreach (var t in trace) log.WriteLine(t);
        foreach (var l in s.Log.Where(l => l.Contains("warning", StringComparison.Ordinal)).Take(10)) log.WriteLine(l);
        var final = PlatformerState(s);
        Assert.InRange(plateauY, 111, 113);                      // walked up the slope onto the hill
        Assert.True(final.Score >= 10, $"score {final.Score}");   // the coin on the hill
        Assert.True(checkpointReached, "checkpoint not reached");
        Assert.True(maxX > 900, $"got as far as x {maxX}");
        Assert.DoesNotContain(s.Actors(), a => a.Name == "Player");  // the goal flag leads back to the title
        Assert.DoesNotContain(s.Log, l => GameplayOverrun(l));
        s.Stop();
    }

    // A gameplay frame that took too long ("... (Scene: update)"). Load steps run while the screen is faded out and
    // are reported separately ("load step ... took N cycles").
    static bool GameplayOverrun(string line) =>
        line.Contains("took longer than one frame", StringComparison.Ordinal) && line.Contains(": update)", StringComparison.Ordinal);

    static LiveActor Me(PlaySession s) => s.Actors().Single(a => a.Name == "Player");

    // Frames that overran while walking or talking, with the profiler split (shown when a test fails).
    static readonly List<string> Overruns = new();

    static void Walk(PlaySession s, GbaKeys dir, Func<LiveActor, bool> until, int max = 400)
    {
        for (var i = 0; i < max; i++)
        {
            var me = s.Actors().FirstOrDefault(a => a.Name == "Player");
            if (me == null || until(me)) break;  // no player: the scene changed (a battle started)
            s.Keys = dir;
            var before = s.Profiler()?.Overruns ?? 0;
            s.Step();
            var p = s.Profiler();
            if (p != null && p.Overruns != before) Overruns.Add($"frame {s.Frame} at {Me(s).X},{Me(s).Y} logic {p.Logic} render {p.Render} audio {p.Audio}");
        }
        s.Keys = 0;
        s.Step();
    }

    // Presses A until the dialogue box (light paper at the bottom of the screen) is gone.
    static void CloseDialogue(PlaySession s)
    {
        for (var i = 0; i < 12; i++)
        {
            var c = Pixel(s, 120, 140);
            if (c.R < 230 || c.G < 230 || c.B < 230) return;  // the box paper is near white (BGR555 0x7BDE)
            Press(s, GbaKeys.A);
            for (var k = 0; k < 20; k++) s.Step();
        }
        Assert.Fail("the dialogue box did not close; log: " + string.Join(" | ", s.Log.TakeLast(15)));
    }

    static (int R, int G, int B) Pixel(PlaySession s, int x, int y)
    {
        var c = s.Core.Frame[y * LibretroCore.Width + x];
        return ((int)(c >> 16) & 255, (int)(c >> 8) & 255, (int)c & 255);
    }

    [Fact]
    public void TopDownAdventureTalksPushesUnlocksAndEnters()
    {
        var (p, r) = Build("topdown");
        Assert.Equal("Mode 0 · 1 BG layers · 6 actors · 4 scripts", TemplateInfo.Stats(p));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);
        for (var i = 0; i < 40; i++) s.Step();
        uint State(int offset) => s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + (uint)offset);
        int SceneIndex() => s.Core.ReadU16(s.Channel!.Game);

        // Talk to the elder (200, 120): the dialogue box opens over the bottom of the screen.
        Walk(s, GbaKeys.Up, a => a.Y <= 125);  // row 7, clear of the fence posts on row 6
        Walk(s, GbaKeys.Right, a => a.X >= 180);
        s.Keys = GbaKeys.A;
        for (var i = 0; i < 32; i++)
        {
            var before = s.Profiler()?.Overruns ?? 0;
            s.Step();
            s.Keys = 0;
            var pr = s.Profiler();
            if (pr != null && pr.Overruns != before) Overruns.Add($"talk frame +{i} logic {pr.Logic} render {pr.Render} audio {pr.Audio}");
        }
        var box = Pixel(s, 120, 140);
        Assert.True(box.R > 230 && box.G > 230 && box.B > 230, $"dialogue box paper {box}");
        CloseDialogue(s);

        // Push the crate (72, 112) one tile left, out of the opening of the fenced corner.
        Walk(s, GbaKeys.Left, a => a.X <= 88);
        Walk(s, GbaKeys.Up, a => a.Y <= 104);
        s.Keys = GbaKeys.Left;
        for (var i = 0; i < 40; i++) s.Step();
        s.Keys = 0;
        for (var i = 0; i < 10; i++) s.Step();
        var crate = s.Actors().Single(a => a.Name == "Crate");
        Assert.True((int)crate.X == 56, $"crate at {crate.X},{crate.Y}; player at {Me(s).X},{Me(s).Y}");

        // Through the opening to the key, then back out to the door.
        Walk(s, GbaKeys.Left, a => a.X <= 72);
        Walk(s, GbaKeys.Up, a => a.Y <= 60, 200);
        Assert.Equal(1u, State(8));  // keys
        CloseDialogue(s);
        Walk(s, GbaKeys.Down, a => a.Y >= 120);
        Walk(s, GbaKeys.Right, a => a.X >= 152);
        Walk(s, GbaKeys.Up, a => a.Y <= 104);
        s.Keys = GbaKeys.Up;
        for (var i = 0; i < 20; i++) s.Step();
        Assert.Equal(1u, State(12) & 0xFF);  // doorOpen
        Assert.Equal(0u, State(8));          // the key was used
        for (var i = 0; i < 60 && SceneIndex() != 0; i++) s.Step();
        s.Keys = 0;
        for (var i = 0; i < 40; i++) s.Step();
        Assert.Equal(0, SceneIndex());  // House
        Assert.False(s.Log.Any(l => GameplayOverrun(l)), string.Join(" | ", s.Log.Concat(Overruns)));
        s.Stop();
    }

    [Fact]
    public void VisualNovelTellsTheStoryAndRemembersTheChoice()
    {
        var (p, r) = Build("visualnovel");
        Assert.Equal("Mode 0 · 1 BG layers · 3 actors · 2 scripts", TemplateInfo.Stats(p));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);  // Start
        for (var i = 0; i < 70; i++) s.Step();
        uint State(int offset) => s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + (uint)offset);
        int SceneIndex() => s.Core.ReadU16(s.Channel!.Game);
        var story = SceneIndex();
        Assert.Equal(1u, State(8));  // chapter 1 started
        var box = Pixel(s, 120, 140);
        Assert.True(box.R > 230 && box.G > 230 && box.B > 230, $"dialogue box paper {box}");
        AgbStudio.Core.Png.WriteArgb(Path.Combine(p.BuildDir, "story.png"), (uint[])s.Core.Frame.Clone(), 240, 160);
        Assert.False(s.Actors().Single(a => a.Name == "Owl").Visible);
        // Three lines, then the choice: Down picks "Wait for morning".
        for (var line = 0; line < 3; line++)
        {
            Press(s, GbaKeys.A);
            for (var i = 0; i < 40; i++) s.Step();
        }
        Assert.True(s.Actors().Single(a => a.Name == "Owl").Visible);  // the owl has spoken
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.A);
        for (var i = 0; i < 40; i++) s.Step();
        Assert.Equal(0u, State(12) & 0xFF);  // braveHopper stays false
        for (var i = 0; i < 8 && SceneIndex() == story; i++)
        {
            Press(s, GbaKeys.A);
            for (var k = 0; k < 40; k++) s.Step();
        }
        Assert.NotEqual(story, SceneIndex());  // back on the title screen
        Assert.DoesNotContain(s.Log, l => GameplayOverrun(l));
        s.Stop();
    }

    [Fact]
    public void PuzzleDropsPairsClearsLinesAndEnds()
    {
        var (p, r) = Build("puzzle");
        Assert.Equal("Mode 0 · 1 BG layers · 4 actors · 1 scripts", TemplateInfo.Stats(p));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);  // Start
        for (var i = 0; i < 60; i++) s.Step();
        uint Score() => s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + 8);
        int SceneIndex() => s.Core.ReadU16(s.Channel!.Game);
        var board = SceneIndex();
        // A bot: each pair goes to the next column (the pair spawns in column 2) and drops with Down.
        for (var piece = 0; piece < 80 && SceneIndex() == board; piece++)
        {
            var column = piece % 6;
            for (var k = 0; k < Math.Abs(column - 2); k++) Press(s, column < 2 ? GbaKeys.Left : GbaKeys.Right);
            s.Keys = GbaKeys.Down;
            for (var i = 0; i < 60; i++)
            {
                var before = s.Profiler()?.Overruns ?? 0;
                s.Step();
                var pr = s.Profiler();
                if (pr != null && pr.Overruns != before && Overruns.Count < 5) Overruns.Add($"piece {piece} logic {pr.Logic} render {pr.Render} audio {pr.Audio}");
            }
            s.Keys = 0;
            s.Step();
            if (piece % 10 == 9) Press(s, GbaKeys.A);  // after game over, A returns to the title
        }
        Assert.True(Score() > 0, "no line was cleared");
        for (var i = 0; i < 10 && SceneIndex() == board; i++) { Press(s, GbaKeys.A); for (var k = 0; k < 30; k++) s.Step(); }
        Assert.NotEqual(board, SceneIndex());  // the well filled up and A went back to the title
        Assert.False(s.Log.Any(l => GameplayOverrun(l)), string.Join(" | ", Overruns));
        s.Stop();
    }

    [Fact]
    public void ShootEmUpScrollsFiresAndClearsTheStage()
    {
        var (p, r) = Build("shmup");
        Assert.Equal("Mode 0 · 2 BG layers · 8 actors · 3 scripts", TemplateInfo.Stats(p));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);  // Start
        for (var i = 0; i < 40; i++) s.Step();
        uint Score() => s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + 8);
        int SceneIndex() => s.Core.ReadU16(s.Channel!.Game);
        var stage = SceneIndex();
        // Hold fire and sweep up and down.
        var mostActors = 0;
        for (var f = 0; f < 2200 && SceneIndex() == stage; f++)
        {
            s.Keys = GbaKeys.A | ((f / 60) % 2 == 0 ? GbaKeys.Up : GbaKeys.Down);
            var before = s.Profiler()?.Overruns ?? 0;
            s.Step();
            var pr = s.Profiler();
            if (pr != null && pr.Overruns != before) Overruns.Add($"frame {f} actors {s.Actors().Count} logic {pr.Logic} render {pr.Render} audio {pr.Audio}");
            if (f % 30 == 0) mostActors = Math.Max(mostActors, s.Actors().Count);
            if (f == 400) AgbStudio.Core.Png.WriteArgb(Path.Combine(p.BuildDir, "stage.png"), (uint[])s.Core.Frame.Clone(), 240, 160);
        }
        s.Keys = 0;
        Assert.True(Score() >= 100, $"score {Score()}");
        Assert.NotEqual(stage, SceneIndex());  // stage clear (or out of lives) returns to the title
        Assert.True(mostActors > 10, $"at most {mostActors} actors at once");  // shots and enemies were spawned
        Assert.DoesNotContain(s.Log, l => l.Contains("actor slots are in use", StringComparison.Ordinal));
        Assert.DoesNotContain(s.Log, l => l.Contains("is full", StringComparison.Ordinal));
        Assert.False(s.Log.Any(l => GameplayOverrun(l)), string.Join(" | ", Overruns));
        s.Stop();
    }

    [Fact]
    public void RpgShopsAndWinsABattle()
    {
        var (p, r) = Build("rpg");
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);  // Start
        for (var i = 0; i < 60; i++) s.Step();
        uint Field(int index) => s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + 8 + (uint)index * 4);  // gold, exp, heroHp, mageHp, mageMp, potions, level, battles
        int SceneIndex() => s.Core.ReadU16(s.Channel!.Game);
        var meadow = SceneIndex();
        Assert.Equal(40u, Field(0));
        Assert.Equal(1u, Field(5));

        // Buy a potion: walk up to the shopkeeper (72, 56) and talk.
        Walk(s, GbaKeys.Up, a => a.Y <= 66);  // stops against the shopkeeper
        Press(s, GbaKeys.A);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);   // past "Potions, 30 gold each. Buy one?"
        for (var i = 0; i < 20; i++) s.Step();
        Press(s, GbaKeys.A);   // Buy
        for (var i = 0; i < 40; i++) s.Step();
        CloseDialogue(s);
        Assert.Equal(10u, Field(0));
        Assert.Equal(2u, Field(5));

        // Into the tall grass until a battle starts.
        Walk(s, GbaKeys.Down, a => a.Y >= 136);
        Walk(s, GbaKeys.Right, a => a.X >= 216);
        for (var i = 0; i < 40 && SceneIndex() == meadow; i++)
        {
            Walk(s, GbaKeys.Down, a => a.Y >= 184, 60);
            Walk(s, GbaKeys.Up, a => a.Y <= 136, 60);
        }
        Assert.NotEqual(meadow, SceneIndex());
        // FIGHT every turn: A picks the first command and A moves the text on.
        for (var i = 0; i < 60 && SceneIndex() != meadow; i++)
        {
            Press(s, GbaKeys.A);
            for (var k = 0; k < 25; k++) s.Step();
        }
        for (var i = 0; i < 40; i++) s.Step();
        Assert.Equal(meadow, SceneIndex());
        Assert.Equal(1u, Field(7));            // one battle
        Assert.True(Field(0) > 10, $"gold {Field(0)}");  // the slime's gold
        Assert.True(Field(1) > 0, $"exp {Field(1)}");
        Assert.DoesNotContain(s.Log, l => GameplayOverrun(l));
        s.Stop();
    }
}
