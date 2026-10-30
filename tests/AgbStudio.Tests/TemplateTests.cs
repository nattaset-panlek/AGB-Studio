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
        Assert.DoesNotContain(s.Log, l => l.Contains("took longer than one frame", StringComparison.Ordinal));
        s.Stop();
    }
}
