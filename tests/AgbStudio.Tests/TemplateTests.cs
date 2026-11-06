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
        Assert.Equal(["Platformer", "Top-down Adventure", "Visual Novel", "Puzzle", "Shoot 'em up", "Turn-based RPG", "Racing (Mode 7)", "Beat 'em up", "Blank"], TemplateCatalog.All.Select(t => t.Name));
        Assert.All(TemplateCatalog.All, t => Assert.Contains(t.Genre, new[] { "Action", "Adventure", "RPG", "Puzzle", "Story", "Racing", "Blank" }));
    }

    public static IEnumerable<object[]> TemplateIds => TemplateCatalog.All.Where(t => t.Id != BlankTemplate.Id).Select(t => new object[] { t.Id });

    /// <summary>
    /// T-D12: every template is a readable example. Its objects carry the components they use (built-in behaviours of
    /// the runtime or the project's own scripts, nothing unknown), the player is called Player, the HUD labels sit in
    /// a HUD folder, every script is commented and used, README.md explains every object and what to try, and the
    /// editor opens on the main scene instead of the title screen.
    /// </summary>
    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void TemplateIsAReadableExample(string id)
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "template_readable_" + id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var t = TemplateCatalog.Find(id)!;
        var p = t.Create(dir, t.Name);
        var scenes = p.SceneFiles().Select(GameProject.Load<SceneFile>).ToList();
        var scripts = p.ScriptFiles().ToDictionary(f => Path.GetFileNameWithoutExtension(f), File.ReadAllText);
        var main = TemplateKit.MainScene(p);
        Assert.NotNull(main);
        Assert.NotEqual(p.Settings.General.StartScene, main);
        Assert.Equal(main, p.Settings.LastScene);

        var used = new HashSet<string>();
        foreach (var scene in scenes)
            foreach (var a in scene.Actors)
            {
                foreach (var c in a.Components)
                {
                    Assert.True(BuiltinBehaviours.Find(c.Type) != null || scripts.ContainsKey(c.Type), $"{id}: {scene.Name}/{a.Name} uses {c.Type}, which is neither built in nor a script");
                    used.Add(c.Type);
                }
                if (a.Kind == ActorKind.Player) Assert.Equal("Player", a.Name);
                if (a.Kind == ActorKind.TextLabel && scene.Name != p.Settings.General.StartScene) Assert.True(a.Group == "HUD" || a.Components.Count > 0, $"{id}: label {a.Name} is not in the HUD folder");
                if (a.Kind is not (ActorKind.TextLabel or ActorKind.TriggerZone or ActorKind.CameraZone))
                    Assert.True(a.Sprite.Length > 0 || a.Components.Count > 0, $"{id}: {scene.Name}/{a.Name} has neither a sprite nor a component");
            }
        var mainScene = scenes.Single(s => s.Name == main);
        Assert.True(mainScene.Actors.Any(a => a.Components.Count > 0), $"{id}: no object of the main scene has a behaviour");

        foreach (var (name, text) in scripts)
        {
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("using ", StringComparison.Ordinal)).ToList();
            Assert.StartsWith("//", lines[0]);
            var comments = lines.Count(l => l.StartsWith("//", StringComparison.Ordinal) || l.Contains(" // ", StringComparison.Ordinal));
            Assert.True(comments * 10 >= lines.Count, $"{id}: {name}.cs has {comments} comment lines for {lines.Count} lines");
            // a behaviour is on some actor; other classes (natives, scene scripts) are called by them
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"class\s+" + name + @"\s*:\s*Behaviour")) Assert.Contains(name, used);
        }

        var readme = File.ReadAllText(Path.Combine(p.Root, "README.md"));
        foreach (var scene in scenes)
        {
            Assert.Contains("### " + scene.Name, readme);
            foreach (var a in scene.Actors) Assert.Contains("| " + a.Name, readme);
        }
        foreach (var name in scripts.Keys) Assert.Contains($"scripts/{name}.cs", readme);
        var tries = readme[readme.IndexOf("## Try changing", StringComparison.Ordinal)..].Split('\n').Count(l => l.StartsWith("- ", StringComparison.Ordinal));
        Assert.True(tries >= 3, $"{id}: {tries} things to try");
        Assert.DoesNotContain('�', readme);
    }

    [Fact]
    public void PlatformerPlaysThroughToTheGoal()
    {
        var (p, r) = Build("platformer");
        Assert.Equal("Mode 0 · 3 BG layers · 7 actors · 4 scripts", TemplateInfo.Stats(p));
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
        AssertFrameBudget(s);
        s.Stop();
    }

    // A gameplay frame that took too long ("... (Scene: update)"). Load steps run while the screen is faded out and
    // are reported separately ("load step ... took N cycles").
    static bool GameplayOverrun(string line) =>
        line.Contains("took longer than one frame", StringComparison.Ordinal) && line.Contains(": update)", StringComparison.Ordinal);

    // G-HW.07: every frame of the play-through fitted its 280,896 cycles (the runtime logs each gameplay or staged-load
    // frame that missed its VBlank), every commit finished inside VBlank, and the longest step of every scene load (the
    // worst scene included) fitted one frame. The load records and a profiler reading go to the test output.
    void AssertFrameBudget(PlaySession s)
    {
        var loads = s.Log.Where(l => l.Contains(" loaded: longest load step ", StringComparison.Ordinal)).ToList();
        foreach (var l in loads) log.WriteLine(l);
        if (s.Profiler() is { } p)
            log.WriteLine($"profiler: busiest of the last 120 frames {p.History.Max()} cycles; logic {p.Logic}, render {p.Render}, audio {p.Audio}, VBlank left {p.VBlankFree}");
        Assert.NotEmpty(loads);
        Assert.DoesNotContain(s.Log, l => l.Contains("took longer than one frame", StringComparison.Ordinal));
        Assert.DoesNotContain(s.Log, l => l.Contains("VBlank commit did not finish", StringComparison.Ordinal));
        foreach (var l in loads)
            Assert.True(long.Parse(System.Text.RegularExpressions.Regex.Match(l, @"' (\d+) cycles").Groups[1].Value) < 280896, l);
    }

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

    // Every template's menus are UI pages, open to change in the UI Editor: nothing about them is drawn in code.
    [Theory]
    [InlineData("platformer")]
    [InlineData("topdown")]
    [InlineData("visualnovel")]
    [InlineData("puzzle")]
    [InlineData("shmup")]
    [InlineData("rpg")]
    [InlineData("racing")]
    [InlineData("brawler")]
    public void TemplateMenusAreUiPages(string id)
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "template_pages_" + id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var t = TemplateCatalog.Find(id)!;
        var p = t.Create(dir, t.Name);
        foreach (var page in new[] { "Title", "Pause", "Options", "Choice" })
            Assert.True(File.Exists(Path.Combine(p.UiDir, page + ".uipage")), page);
        var title = GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, "Title.scene"));
        Assert.Equal("Title", title.Page);
        Assert.Null(title.Menu);
        Assert.DoesNotContain(title.Actors, a => a.IsUi);
        var start = Assert.Single(UiPageLinks.LoadSceneButtons(p, title));
        Assert.Equal(TemplateKit.MainScene(p), start.Action.Target);
        Assert.True(File.Exists(Path.Combine(p.ScenesDir, start.Action.Target + ".scene")), start.Action.Target);
        UiPageLinks.RenameScene(p, start.Action.Target, "Renamed");
        Assert.Equal("Renamed", UiPageLinks.LoadSceneButtons(p, title).Single().Action.Target);
    }

    // A HUD is a UI page the game goes on under: the scene keeps running, Start opens the pause menu over it, and in Mode 7 it
    // gives its memory to the Options page and comes back.
    [Fact]
    public void RacingHud_StaysWhileTheGamePlays_AndUnderThePauseAndOptionsPages()
    {
        var (p, r) = Build("racing");
        var hud = GameProject.Load<UiPageFile>(Path.Combine(p.UiDir, "Hud.uipage"));
        Assert.True(hud.ScenePlays);
        Assert.False(hud.Cancel);
        Assert.Contains(p.SceneFiles().Select(GameProject.Load<SceneFile>), s => s.Hud == "Hud");
        Assert.DoesNotContain(p.SceneFiles().Select(GameProject.Load<SceneFile>).SelectMany(s => s.Actors), a => a.Kind == ActorKind.TextLabel);
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 120; i++) s.Step();
        Press(s, GbaKeys.A);  // Start
        for (var i = 0; i < 180; i++) s.Step();
        Assert.Contains("UI page Hud opened", s.Log);
        Assert.DoesNotContain("UI page Hud closed", s.Log);
        Press(s, GbaKeys.Start);
        for (var i = 0; i < 40; i++) s.Step();
        Assert.Contains("UI page Pause opened", s.Log);
        Press(s, GbaKeys.Down);
        for (var i = 0; i < 8; i++) s.Step();
        Press(s, GbaKeys.A);
        for (var i = 0; i < 60; i++) s.Step();
        Assert.Contains("UI page Options opened", s.Log);
        Assert.DoesNotContain(s.Log, l => l.Contains("did not open", StringComparison.Ordinal));
        Press(s, GbaKeys.B);
        for (var i = 0; i < 40; i++) s.Step();
        Assert.Equal(2, s.Log.Count(l => l == "UI page Pause opened"));
        Press(s, GbaKeys.B);
        for (var i = 0; i < 40; i++) s.Step();
        Assert.DoesNotContain("UI page Hud closed", s.Log);
        Assert.DoesNotContain(s.Log, l => l.Contains("took longer than one frame", StringComparison.Ordinal));
    }

    // Mode 7 leaves the least background memory: the Options page takes the place of the page that opened it, which comes
    // back with its cursor when Options closes.
    [Fact]
    public void RacingOpensTheOptionsPage_FromTheTitle_AndComesBack()
    {
        var (_, r) = Build("racing");
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 120; i++) s.Step();
        Press(s, GbaKeys.Down);  // Continue is off without a save: the cursor goes to Options
        for (var i = 0; i < 8; i++) s.Step();
        Press(s, GbaKeys.A);
        for (var i = 0; i < 60; i++) s.Step();
        Assert.Contains("UI page Options opened", s.Log);
        Assert.DoesNotContain(s.Log, l => l.Contains("did not open", StringComparison.Ordinal));
        Press(s, GbaKeys.B);
        for (var i = 0; i < 60; i++) s.Step();
        Assert.Equal(2, s.Log.Count(l => l == "UI page Title opened"));
        Press(s, GbaKeys.A);  // the cursor is still on Options
        for (var i = 0; i < 60; i++) s.Step();
        Assert.Equal(2, s.Log.Count(l => l == "UI page Options opened"));
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
        Assert.True(box.R >= 230 && box.G >= 230 && box.B >= 230, $"dialogue box paper {box}");
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
        AssertFrameBudget(s);
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
        Assert.True(box.R >= 230 && box.G >= 230 && box.B >= 230, $"dialogue box paper {box}");
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
        AssertFrameBudget(s);
        s.Stop();
    }

    [Fact]
    public void PuzzleDropsPairsClearsLinesAndEnds()
    {
        var (p, r) = Build("puzzle");
        Assert.Equal("Mode 0 · 1 BG layers · 2 actors · 2 scripts", TemplateInfo.Stats(p));
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
        AssertFrameBudget(s);
        s.Stop();
    }

    [Fact]
    public void ShootEmUpScrollsFiresAndClearsTheStage()
    {
        var (p, r) = Build("shmup");
        Assert.Equal("Mode 0 · 2 BG layers · 6 actors · 4 scripts", TemplateInfo.Stats(p));
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
        AssertFrameBudget(s);
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
        AssertFrameBudget(s);
        s.Stop();
    }

    [Fact]
    public void RacingDrivesTheMode7CircuitAndCountsLaps()
    {
        var (p, r) = Build("racing");
        Assert.Equal("Mode 1 · 2 BG layers · 3 actors · 2 scripts", TemplateInfo.Stats(p));
        // The Mode 7 line table (161 lines x PA, PC, X, Y): the floor point under the kart sprite (120, 136) and the
        // heading come from line 136, so the test can steer like a player looking at the screen.
        var table = (uint)BuildReport.Symbols(new Toolchain(), r.ElfPath).Single(x => x.Name == "g_table" && x.Size == 161 * 16).Address;
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);  // Start
        for (var i = 0; i < 40; i++) s.Step();
        int SceneIndex() => s.Core.ReadU16(s.Channel!.Game);
        uint Best() => s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + 8);
        var circuit = SceneIndex();
        var (mode, bg, _) = Inspector.Display(s.Core);
        Assert.Equal(1, mode);
        Assert.True(bg[2], "the floor (BG2) shows");
        (double X, double Y, double Heading) Kart()
        {
            var e = table + 136 * 16;
            var pa = (short)(s.Core.ReadU32(e) & 0xFFFF);
            var pc = (short)(s.Core.ReadU32(e + 4) & 0xFFFF);
            var x = (int)s.Core.ReadU32(e + 8) + pa * 120;
            var y = (int)s.Core.ReadU32(e + 12) + pc * 120;
            return (x / 256.0, y / 256.0, Math.Atan2(-pa, pc) * 128 / Math.PI);
        }
        // Centre line of the circuit (RacingTemplate.Circuit): a rounded rectangle round (512, 512).
        var path = new List<(double X, double Y)>();
        for (var i = 0; i < 128; i++)
        {
            var t = i / 128.0 * 4;
            var side = (int)t;
            var f = t - side;
            double u = -352 + 704 * f;
            var (x, y) = side switch { 0 => (-352.0, -u), 1 => (u, -352.0), 2 => (352.0, u), _ => (-u, 352.0) };
            // Pull the corners onto the radius-160 arcs.
            double cx = Math.Clamp(x, -192, 192), cy = Math.Clamp(y, -192, 192), dx = x - cx, dy = y - cy, d = Math.Sqrt(dx * dx + dy * dy);
            if (d > 0) (x, y) = (cx + dx / d * 160, cy + dy / d * 160);
            path.Add((512 + x, 512 + y));
        }
        var shot = false;
        for (var f = 0; f < 5000 && Best() == 0; f++)
        {
            var k = Kart();
            var near = Enumerable.Range(0, path.Count).MinBy(i => Math.Pow(path[i].X - k.X, 2) + Math.Pow(path[i].Y - k.Y, 2));
            var target = path[(near + 6) % path.Count];
            var want = Math.Atan2(target.Y - k.Y, target.X - k.X) * 128 / Math.PI;
            var diff = (want - k.Heading + 384) % 256 - 128;
            s.Keys = GbaKeys.A | (diff > 3 ? GbaKeys.Right : diff < -3 ? GbaKeys.Left : 0);
            var before = s.Profiler()?.Overruns ?? 0;
            s.Step();
            var pr = s.Profiler();
            if (pr != null && pr.Overruns != before) Overruns.Add($"frame {f} at {k.X:0},{k.Y:0} logic {pr.Logic} render {pr.Render} audio {pr.Audio}");
            if (!shot && k.Y < 200)
            {
                // The first corner: the floor turns and a rival is on screen.
                AgbStudio.Core.Png.WriteArgb(Path.Combine(p.BuildDir, "race.png"), (uint[])s.Core.Frame.Clone(), 240, 160);
                shot = true;
            }
        }
        s.Keys = 0;
        var best = Best();
        Assert.True(best > 0, "three laps were not finished");
        Assert.InRange(best / 60.0, 30, 120);  // seconds for three laps of about 2540 px
        for (var i = 0; i < 300 && SceneIndex() == circuit; i++) s.Step();
        Assert.NotEqual(circuit, SceneIndex());  // back to the title after the finish
        Assert.False(s.Log.Any(l => GameplayOverrun(l)), string.Join(" | ", Overruns));
        AssertFrameBudget(s);
        s.Stop();
    }

    [Fact]
    public void BeatEmUpFightsThroughTheWavesWithCombos()
    {
        var (p, r) = Build("brawler");
        Assert.Equal("Mode 0 · 3 BG layers · 3 actors · 3 scripts", TemplateInfo.Stats(p));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 40; i++) s.Step();
        Press(s, GbaKeys.A);  // Start
        for (var i = 0; i < 200 && !s.Actors().Any(a => a.Name == "Player"); i++) s.Step();
        int SceneIndex() => s.Core.ReadU16(s.Channel!.Game);
        uint Score() => s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + 8);
        var pier = SceneIndex();
        var most = 0;
        var shot = false;
        // Walk right; when thugs are on screen, line up beside the nearest one, face it and press A in rhythm
        // (jab, jab, kick).
        for (var f = 0; f < 9000 && SceneIndex() == pier; f++)
        {
            var actors = s.Actors();
            var me = actors.SingleOrDefault(a => a.Name == "Player");
            if (me == null)
            {
                s.Step();  // the scene is changing
                continue;
            }
            var thugs = actors.Where(a => a.Name == "Thug" && a.Alive && a.Visible && a.Y > 0).ToList();
            most = Math.Max(most, thugs.Count);
            GbaKeys keys = 0;
            if (thugs.Count == 0) keys = GbaKeys.Right;
            else
            {
                var t = thugs.MinBy(a => Math.Abs(a.X - me.X) + Math.Abs(a.Y - me.Y))!;
                var standX = t.X + (me.X < t.X ? -18 : 18);
                if (t.Y > me.Y + 3) keys |= GbaKeys.Down;
                else if (t.Y < me.Y - 3) keys |= GbaKeys.Up;
                if (standX > me.X + 3) keys |= GbaKeys.Right;
                else if (standX < me.X - 3) keys |= GbaKeys.Left;
                else if (t.X < me.X != me.FacingLeft) keys |= t.X < me.X ? GbaKeys.Left : GbaKeys.Right;
                else if (Math.Abs(t.Y - me.Y) <= 6 && f % 16 == 0) keys = GbaKeys.A;
                if (!shot && thugs.Count >= 2 && Math.Abs(t.X - me.X) < 30)
                {
                    AgbStudio.Core.Png.WriteArgb(Path.Combine(p.BuildDir, "brawl.png"), (uint[])s.Core.Frame.Clone(), 240, 160);
                    shot = true;
                }
            }
            s.Keys = keys;
            var before = s.Profiler()?.Overruns ?? 0;
            s.Step();
            var pr = s.Profiler();
            if (pr != null && pr.Overruns != before) Overruns.Add($"frame {f} thugs {thugs.Count} logic {pr.Logic} render {pr.Render} audio {pr.Audio}");
        }
        s.Keys = 0;
        var lives = s.Core.Memory(s.Core.ReadU32(s.Channel!.Game + 8) + 12, 1)[0];
        Assert.True(Score() >= 9 * 100, $"score {Score()}, lives {lives}");  // all nine thugs of the three waves
        Assert.True(lives > 0, "the fighter lost every life");
        Assert.True(most >= 4, $"at most {most} thugs at once");
        Assert.NotEqual(pier, SceneIndex());  // stage clear returns to the title
        Assert.False(s.Log.Any(l => GameplayOverrun(l)), string.Join(" | ", Overruns));
        AssertFrameBudget(s);
        s.Stop();
    }

    [Fact]
    public void BlankIsOneEmptySceneThatBuildsAndBoots()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "template_blank");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var p = TemplateCatalog.Find(BlankTemplate.Id)!.Create(dir, "Blank");
        var scene = Assert.Single(p.SceneFiles());
        Assert.Empty(GameProject.Load<SceneFile>(scene).Actors);
        Assert.Empty(p.SpriteFiles());
        Assert.Empty(p.ScriptFiles());
        Assert.Empty(p.MusicFiles());
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 200; i++) s.Step();
        Assert.NotNull(s.Channel);
        Assert.Equal(0, s.Core.ReadU16(s.Channel!.Game));
        s.Stop();
    }

    public static IEnumerable<object[]> SampleIds => [["Sky Hopper demo"], ["Cave Quest"], ["Snow Run"], ["Dusk Story"]];

    [Theory]
    [MemberData(nameof(SampleIds))]
    public void SampleProjectsBuildAndBootToTheTitle(string name)
    {
        Func<string, GameProject> create = name switch
        {
            "Sky Hopper demo" => AgbStudio.Build.Content.SkyHopper.Create,
            "Cave Quest" => AgbStudio.Build.Content.Samples.CaveQuest,
            "Snow Run" => AgbStudio.Build.Content.Samples.SnowRun,
            _ => AgbStudio.Build.Content.Samples.DuskStory,
        };
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "sample_" + name.Replace(' ', '_'));
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var p = create(dir);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 200; i++) s.Step();
        Assert.NotNull(s.Channel);
        var start = p.SceneFiles().Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal).ToList().IndexOf(p.Settings.General.StartScene);
        Assert.Equal(start, s.Core.ReadU16(s.Channel!.Game));
        AgbStudio.Core.Png.WriteArgb(Path.Combine(p.BuildDir, "title.png"), (uint[])s.Core.Frame.Clone(), 240, 160);
        Assert.DoesNotContain(s.Log, l => l.Contains("has no text", StringComparison.Ordinal) || l.Contains("could not be loaded", StringComparison.Ordinal));
        s.Stop();
    }
}
