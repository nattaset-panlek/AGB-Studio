using System.Diagnostics;
using System.Text.Json;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Fonts;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;

namespace AgbStudio.Tests;

/// <summary>The Sky Hopper fixture built once from clean, then built again without changes.</summary>
public sealed class FixtureBuild
{
    public static readonly string Dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "fixture_build");
    public GameProject Project { get; }
    public ScriptBackend Scripts { get; } = new();
    public BuildResult Clean { get; }
    public TimeSpan CleanTime { get; }
    public BuildResult Second { get; } = new();
    public List<string> SecondLines { get; } = new();
    public List<string> RewrittenBySecond { get; } = new();  // generated files and the ROM the second build wrote again

    sealed class Lines(List<string> lines) : IBuildObserver
    {
        public void StepStarted(string step, string detail) { }
        public void StepFinished(string step, TimeSpan elapsed, bool ok, string detail) { }
        public void FileProgress(string step, int done, int total, string file) { }
        public void Line(string text) { lock (lines) lines.Add(text); }
        public void Message(Diagnostic d) { }
    }

    public FixtureBuild()
    {
        Project = SkyHopper.Create(Dir);
        var sw = Stopwatch.StartNew();
        Clean = new GameBuilder(Project, new Toolchain()) { Scripts = Scripts }.Build(new BuildOptions { Clean = true });
        CleanTime = sw.Elapsed;
        if (!Clean.Success) return;
        var times = Directory.EnumerateFiles(Path.Combine(Project.BuildDir, "gen"), "*.*", SearchOption.AllDirectories).Append(Clean.RomPath)
            .ToDictionary(f => f, File.GetLastWriteTimeUtc);
        Second = new GameBuilder(Project, new Toolchain(), new Lines(SecondLines)) { Scripts = Scripts }.Build();
        RewrittenBySecond.AddRange(times.Where(kv => File.GetLastWriteTimeUtc(kv.Key) != kv.Value).Select(kv => Path.GetRelativePath(Project.BuildDir, kv.Key)));
    }
}

/// <summary>
/// P7.X1: the fixture builds clean to a .gba, a second build with no changes does no work, the report matches the map.
/// G-PERF.03: the clean build, project open and Play start targets of D-26. G-HW.06: the save slot size the build checks is
/// the size the compiler gives the game state. P9.X1: the fixture in English and Thai, and Japanese showing its fallback.
/// </summary>
[Collection("emulator")]
public sealed class FixtureBuildTests(EmulatorFixture emu, FixtureBuild fixture) : IClassFixture<FixtureBuild>
{
    // D-26 targets (docs/DECISIONS.md).
    public static readonly TimeSpan CleanBuildTarget = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ProjectOpenTarget = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan PlayStartTarget = TimeSpan.FromSeconds(3);

    static void Record(string name, TimeSpan t) =>
        File.AppendAllText(Path.Combine(NativeBuild.RepoRoot, "build", "perf-targets.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm} {name} {t.TotalSeconds:0.00} s\n");

    [Fact]
    public void FixtureBuildsCleanWithinTheTarget()
    {
        Assert.True(fixture.Clean.Success, string.Join('\n', fixture.Clean.Diagnostics));
        Assert.True(File.Exists(fixture.Clean.RomPath));
        Assert.EndsWith("skyhopper.gba", fixture.Clean.RomPath);
        Assert.True(fixture.Clean.AssetsConverted > 0);
        Assert.Equal("SKYHOPPER", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(fixture.Clean.RomPath), 0xA0, 9));
        Record("clean build", fixture.CleanTime);
        Assert.True(fixture.CleanTime < CleanBuildTarget, $"clean build took {fixture.CleanTime.TotalSeconds:0.0} s (target {CleanBuildTarget.TotalSeconds} s)");
    }

    [Fact]
    public void SecondBuildWithoutChangesDoesNoWork()
    {
        var r = fixture.Second;
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.Equal(0, r.AssetsConverted);
        Assert.True(r.AssetsCached > 0);
        // Nothing compiled, linked or fixed; Ninja says so.
        Assert.DoesNotContain(fixture.SecondLines, l => l.Contains("] CXX ", StringComparison.Ordinal) || l.Contains("] LINK ", StringComparison.Ordinal) || l.Contains("] GBAFIX ", StringComparison.Ordinal));
        if (File.Exists(new Toolchain().Ninja) || Toolchain.FindOnPath(new Toolchain().Ninja) != null)
            Assert.Contains(fixture.SecondLines, l => l.Contains("no work to do", StringComparison.Ordinal));
        // No generated file and not the ROM was written again.
        Assert.Empty(fixture.RewrittenBySecond);
        Assert.Equal(fixture.Clean.Report!.RomBytes, r.Report!.RomBytes);
    }

    // P3.11: the game definition hands the MusicMeta table of the soundbank (MusicMetaTests) to the runtime.
    [Fact]
    public void GameDefinitionHandsTheMusicMetaToTheRuntime()
    {
        Assert.True(fixture.Clean.Success, string.Join('\n', fixture.Clean.Diagnostics));
        var gen = Path.Combine(fixture.Project.BuildDir, "gen");
        var game = File.ReadAllText(Path.Combine(gen, "game.cpp"));
        var modules = fixture.Project.MusicFiles().Count();
        Assert.Contains("extern const agb::audio::MusicInfo agb_soundbank_music[];", game);
        Assert.Contains($"agb_soundbank_music, {modules}}};", game);
        Assert.Contains("{205, 4, true},  // bgm_forest.xm", File.ReadAllText(Path.Combine(gen, "audio", "soundbank.cpp")));
    }

    [Fact]
    public void FixtureReportMatchesTheLinkerMap()
    {
        var report = BuildReport.Load(fixture.Project.BuildDir)!;
        var map = BuildReportTests.ReadMap(fixture.Clean.ElfPath + ".map");
        Assert.Equal(map.RomEnd - 0x08000000, report.RomBytes);
        Assert.Equal(new FileInfo(fixture.Clean.RomPath).Length, report.RomBytes);
        Assert.Equal(report.RomBytes, report.Breakdown.Values.Sum());
        Assert.Equal(map.IheapStart - 0x03000000, report.IwramBytes);
        Assert.Equal(map.EheapStart - 0x02000000, report.EwramBytes);
        // Every sprite, tileset, font and table: its bytes are its object file's bytes in the map (plus padding).
        var checkedAssets = 0;
        foreach (var a in report.Assets.Where(a => a.Type is "Sprite" or "Tileset" or "Font" or "Data"))
        {
            var prefix = a.Type switch { "Sprite" => "sprite_", "Font" => "font_", "Data" => "db_", _ => "tileset_" };
            var inMap = map.RomByObject.Where(kv => kv.Key.EndsWith(prefix + Cpp.Ident(a.Name) + ".o", StringComparison.OrdinalIgnoreCase)
                                                    || kv.Key.EndsWith("image_" + Cpp.Ident(a.Name) + ".o", StringComparison.OrdinalIgnoreCase)).Sum(kv => kv.Value);
            Assert.True(a.Bytes >= inMap && a.Bytes < inMap + 64, $"{a.Name}: report {a.Bytes} B, map {inMap} B");
            checkedAssets++;
        }
        Assert.True(checkedAssets >= 12, $"{checkedAssets} assets checked");  // what the scenes use; the linker drops the rest
        // The scene budgets come from the same limit checks that fail a build.
        Assert.Equal(5, report.Scenes.Count);
        Assert.All(report.Scenes.Values, s => Assert.InRange(s.VramBgBytes, s.VramTextBytes + 2048, GameBuilder.BgVramBytes));
        Assert.All(report.Scenes.Values, s => Assert.InRange(s.VramObjBytes, 0, GameBuilder.ObjVramBytes));
        // P9.07: a table's size is its bytes in the ROM (within the alignment of the table). The fixture's scripts read
        // no table, so the linker drops them all; BuildReportTests.ReportNumbersMatchTheLinkerMap checks one in the ROM.
        foreach (var f in fixture.Project.TableFiles())
        {
            var t = GameProject.Load<TableFile>(f);
            var size = AgbStudio.Build.Data.DatabaseCodegen.TableBytes(t);
            if (report.Assets.SingleOrDefault(a => a.Name == t.Name && a.Type == "Data") is not { } built) continue;
            Assert.True(built.Bytes >= size && built.Bytes <= size + 3, $"{t.Name}: table {size} B, ROM {built.Bytes} B");
        }
        // P8.11: each font's estimate is within 10% of its bytes in the ROM.
        foreach (var f in fixture.Project.FontFiles())
        {
            var ff = GameProject.Load<FontFile>(f);
            var built = report.Assets.SingleOrDefault(a => a.Name == ff.Name && a.Type == "Font")?.Bytes ?? 0;
            if (built == 0) continue;  // not linked: no language or menu uses it
            var estimate = FontBuilder.Build(ff, fixture.Project.FontsDir).RomBytes;
            Assert.InRange(estimate, built * 0.9, built * 1.1);
        }
    }

    [Fact]
    public void SaveSlotSizeIsTheCompiledGameState()
    {
        var report = BuildReport.Load(fixture.Project.BuildDir)!;
        Assert.Equal(SaveLayout.SlotBytes(fixture.Project.Settings.Save.Fields, true), report.SaveBytes);
        Assert.Equal(4096, report.SaveCapacity);
        var s = new PlaySession(emu.Core);
        s.Start(fixture.Clean.RomPath);
        for (var i = 0; i < 30; i++) s.Step();
        var stateSize = s.Core.ReadU16(s.Channel!.Game + 6);  // GameInfo.stateSize = sizeof(GameState)
        s.Stop();
        Assert.Equal(report.SaveBytes, stateSize);
    }

    [Fact]
    public void PlayStartsWithinTheTarget()
    {
        // Play: build (nothing changed), load the ROM into the embedded core, run to the first game frame.
        var sw = Stopwatch.StartNew();
        var r = new GameBuilder(fixture.Project, new Toolchain()) { Scripts = fixture.Scripts }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        var frames = 0;
        while ((s.Channel == null || s.Channel.GameFrame < 1) && frames++ < 600) s.Step();
        sw.Stop();
        var started = s.Channel?.GameFrame >= 1;
        s.Stop();
        Assert.True(started, "the game did not start");
        Record("Play start", sw.Elapsed);
        Assert.True(sw.Elapsed < PlayStartTarget, $"Play start took {sw.Elapsed.TotalSeconds:0.00} s (target {PlayStartTarget.TotalSeconds} s)");
    }

    [Fact]
    public void ProjectOpensWithinTheTarget()
    {
        // The Opening Project steps (T-D22): read the project and its scenes, load every asset as the editor does, compile
        // the scripts; opening the scene view itself is UI work outside this test.
        var sw = Stopwatch.StartNew();
        var p = GameProject.Open(FixtureBuild.Dir);
        foreach (var f in p.SceneFiles()) GameProject.Load<SceneFile>(f);
        var assets = p.SpriteFiles().Concat(p.TilesetFiles()).Concat(p.ImageFiles()).Concat(p.MapFiles()).Concat(p.FontFiles()).Concat(p.MusicFiles())
            .Concat(p.SoundFiles()).Concat(p.SfxFiles()).Concat(p.TableFiles()).Concat(p.DialogueFiles()).Concat(p.StringTables()).ToList();
        foreach (var file in assets)
        {
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) Png.Read(file);
            else if (Path.GetExtension(file) is ".map" or ".font" or ".db" or ".dlg" or ".sfx" or ".json") { using var _ = JsonDocument.Parse(File.ReadAllText(file)); }
            else File.ReadAllBytes(file);
            if (File.Exists(GameProject.MetaPath(file))) { using var _ = JsonDocument.Parse(File.ReadAllText(GameProject.MetaPath(file))); }
        }
        var output = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "fixture_open_scripts");
        var input = new ScriptInput
        {
            OutputDir = output, Debug = true, GameState = p.Settings.Save.Fields,
            Scenes = p.SceneFiles().Select(GameProject.AssetName).Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i),
            Tags = p.Settings.Tags.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i),
        };
        var compiled = new ScriptBackend().Compile(p, input, NullObserver.Instance);
        sw.Stop();
        Assert.Equal(12, compiled.ScriptCount);
        Assert.True(assets.Count >= 40, $"{assets.Count} assets");
        Record("project open", sw.Elapsed);
        Assert.True(sw.Elapsed < ProjectOpenTarget, $"project open took {sw.Elapsed.TotalSeconds:0.00} s (target {ProjectOpenTarget.TotalSeconds} s)");
    }

    // Ink pixels of a rendered label match one colour on screen, and that colour is nowhere else in the label box.
    static int Mismatches(uint[] frame, byte[] expected, int x0, int y0, int w, int h)
    {
        uint? ink = null;
        var bad = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var e = expected[y * w + x];
                var screen = frame[(y0 + y) * 240 + x0 + x];
                if (e == 1)
                {
                    ink ??= screen;
                    if (screen != ink) bad++;
                }
                else if (ink != null && screen == ink) bad++;
            }
        return bad;
    }

    [Fact]
    public void FixtureBuildsInEnglishAndThaiAndJapaneseShowsTheFallback()
    {
        var p = fixture.Project;
        var meta = GameProject.Load<StringMeta>(Path.Combine(p.TextDir, "strings.meta.json"));
        var en = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json"));
        var th = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.th.json"));
        var all = string.Concat(en.Strings.Values) + string.Concat(th.Strings.Values) + string.Concat(p.Settings.Localization.Languages.Select(l => l.Name));
        var used = ThaiText.CodePoints(all).Where(c => c >= 0x20).Distinct().ToList();
        GameFont FontOf(string name) => FontBuilder.Build(GameProject.Load<FontFile>(Path.Combine(p.FontsDir, name + ".font")), p.FontsDir, used);
        var shots = Path.Combine(p.BuildDir, "language-shots");
        Directory.CreateDirectory(shots);
        try
        {
            // The title's "ui.title" label at (88, 40): 152 px wide, laid out at its 224 px box.
            foreach (var (lang, text, font) in new[]
                     {
                         ("en", en.Strings["ui.title"], "thai_vwf"),
                         ("th", th.Strings["ui.title"], "thai_vwf"),
                         // 日本語 has no strings and its font jp_kana is not in fonts/: English through its fallback, in the
                         // first font of the project.
                         ("ja", en.Strings["ui.title"], "pixel8"),
                     })
            {
                p.Settings.General.DefaultLanguage = lang;
                p.SaveSettings();
                var r = new GameBuilder(p, new Toolchain()) { Scripts = fixture.Scripts }.Build();
                Assert.True(r.Success, string.Join('\n', r.Diagnostics));
                if (lang == "ja")
                {
                    Assert.Contains(r.Diagnostics, d => d.Message == $"{en.Strings.Count} strings missing in 日本語. Translate them in Localization (\"Missing only\"), or set a fallback language in Localization · Languages.");
                    Assert.Contains(r.Diagnostics, d => d.Message == "Language 'ja' uses font 'jp_kana', which is not in fonts/, so pixel8 is used. Add the font in Font Manager, or pick another one in Localization · Languages.");
                }
                var s = new PlaySession(emu.Core);
                s.Start(r.RomPath);
                for (var i = 0; i < 90; i++) s.Step();
                var frame = (uint[])s.Core.Frame.Clone();
                s.Stop();
                Png.WriteArgb(Path.Combine(shots, $"title-{lang}.png"), frame, 240, 160);
                var f = FontOf(font);
                var layout = TextLayout.Layout(text, f, meta.Keys["ui.title"].BoxWidth);
                int w = 152, h = f.LineHeight * layout.Lines;
                var expected = TextRenderer.Render(layout, f, w, h, new TextRenderer.Style(1, 4, 0));
                Assert.True(expected.Count(x => x == 1) > 20, $"{lang}: nothing to compare");
                Assert.True(Mismatches(frame, expected, 88, 40, w, h) == 0, $"{lang}: the title does not show '{text}' (see {shots})");
            }
        }
        finally
        {
            p.Settings.General.DefaultLanguage = "th";
            p.SaveSettings();
            new GameBuilder(p, new Toolchain()) { Scripts = fixture.Scripts }.Build();
        }
    }
}
