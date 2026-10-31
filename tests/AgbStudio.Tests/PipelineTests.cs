using System.Diagnostics;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

[Collection("emulator")]
public sealed class PipelineTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    static (GameProject Project, BuildResult Result) BuildMini(string name)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return (p, r);
    }

    public sealed record ActorState(string Name, int X, int Y, bool OnGround, bool Alive);

    static List<ActorState> ReadActors(LibretroCore core, DebugChannel ch)
    {
        var list = new List<ActorState>();
        var table = ch.ActorTable;
        var stride = ch.ActorStride;
        for (uint i = 0; i < ch.ActorCount; i++)
        {
            var a = table + i * stride;
            var namePtr = core.ReadU32(a);
            var name = ReadString(core, namePtr);
            var x = (int)core.ReadU32(a + 4) >> 8;
            var y = (int)core.ReadU32(a + 8) >> 8;
            var flags = core.Memory(a + 22, 1)[0];
            list.Add(new ActorState(name, x, y, (flags & 2) != 0, (flags & 1) != 0));
        }
        return list;
    }

    static string ReadString(LibretroCore core, uint ptr)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 64; i++)
        {
            var b = core.Memory(ptr + (uint)i, 1)[0];
            if (b == 0) break;
            sb.Append((char)b);
        }
        return sb.ToString();
    }

    [Fact]
    public void MiniPlatformerBuildsAndPlays()
    {
        var (_, r) = BuildMini("mini_play");
        var core = emu.Core;
        core.LoadRom(r.RomPath);
        DebugChannel? ch = null;
        List<ActorState> actors = new();
        for (var f = 0; f < 240; f++)
        {
            core.RunFrame();
            ch ??= DebugChannel.Find(core);
        }
        Assert.NotNull(ch);
        actors = ReadActors(core, ch!);
        // Save data · Autosave: the level wrote slot 0 (copy A starts with the "AGBS" header) once it loaded.
        Assert.Equal("AGBS", System.Text.Encoding.ASCII.GetString(core.SaveRam[..4]));
        core.UnloadRom();
        Assert.Contains(actors, a => a.Name == "Player");
        var player = actors.First(a => a.Name == "Player");
        Assert.True(player.OnGround, $"player at {player.X},{player.Y}");
        Assert.Equal(144, player.Y);  // origin at the bottom of the box = top of ground row 9 (9 * 16)
        var slime = actors.First(a => a.Name == "Slime");
        Assert.True(slime.OnGround);
        Assert.InRange(slime.X, 224, 320);  // patrols between the gap and the wall
        Assert.Equal(4, actors.Count);
    }

    [Fact]
    public void SecondBuildWithoutChangesConvertsNothingAndIsFast()
    {
        var (p, first) = BuildMini("mini_incremental");
        Assert.True(first.AssetsConverted > 0);
        var sw = Stopwatch.StartNew();
        var second = new GameBuilder(p, new Toolchain()).Build();
        sw.Stop();
        Assert.True(second.Success);
        Assert.Equal(0, second.AssetsConverted);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"no-change build took {sw.Elapsed.TotalSeconds:0.00} s");
        // Changing one sprite option reconverts exactly that sprite.
        var hero = Path.Combine(p.AssetsDir, "sprites", "hero.png");
        var meta = p.LoadMeta<SpriteMeta>(hero);
        meta.OriginY = 15;
        GameProject.Save(GameProject.MetaPath(hero), meta);
        var third = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(third.Success);
        Assert.Equal(1, third.AssetsConverted);
    }

    [Fact]
    public void ReportAttributesRomToAssets()
    {
        var (p, r) = BuildMini("mini_report");
        var report = BuildReport.Load(p.BuildDir)!;
        Assert.Equal(new FileInfo(r.RomPath).Length, report.RomBytes);
        Assert.True(report.Breakdown["Graphics"] > 0);
        Assert.True(report.Breakdown["Code"] > 0);
        Assert.True(report.Breakdown["Audio"] > 0);
        Assert.Contains(report.LargestAssets, a => a.Name == "forest_tiles" && a.Type == "Tileset");
        Assert.Contains(report.LargestAssets, a => a.Name == "hero" && a.Type == "Sprite");
        Assert.True(report.IwramBytes > 0 && report.IwramBytes < 32 * 1024);
        Assert.True(report.EwramBytes > 0 && report.EwramBytes < 256 * 1024);
        Assert.True(report.Scenes.ContainsKey("Forest_01"));
        Assert.True(File.Exists(Path.Combine(p.BuildDir, "report-history.json")));
        // The library notices travel next to the ROM.
        var notices = File.ReadAllText(Path.Combine(Path.GetDirectoryName(r.RomPath)!, "THIRD_PARTY.txt"));
        Assert.Contains("libtonc", notices);
        Assert.Contains("Mukunda Johnson", notices);
        // ROM header written by gbafix from Project Settings · General.
        var rom = File.ReadAllBytes(r.RomPath);
        Assert.Equal("MINIPLAT", System.Text.Encoding.ASCII.GetString(rom, 0xA0, 8));
        Assert.Equal("AMPE", System.Text.Encoding.ASCII.GetString(rom, 0xAC, 4));
        Assert.Equal("SRAM_V113", System.Text.Encoding.ASCII.GetString(rom, rom.AsSpan().IndexOf("SRAM_V"u8), 9));
    }

    [Fact]
    public void ErrorsNameTheFileAndTheProblem()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "mini_errors"));
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Sprite = "missing_sprite";
        scene.Actors[1].Components.Add(new ComponentData { Type = "NoSuchBehaviour" });
        GameProject.Save(scenePath, scene);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message.Contains("uses sprite 'missing_sprite', which is not in assets/sprites") && d.File.EndsWith("Forest_01.scene"));
        Assert.Contains(r.Errors, d => d.Message.Contains("behaviour 'NoSuchBehaviour' was not found"));
        Assert.Equal("Generate game", r.FailedStep);
        // Failed builds are in the Build Report history too, with their first error.
        var last = BuildReport.LoadHistory(p.BuildDir)[0];
        Assert.Equal(0, last.RomBytes);
        Assert.True(last.Errors >= 2);
        Assert.Equal(r.Errors.First().ToString(), last.FirstError);
    }

    sealed class StepLog : IBuildObserver
    {
        public readonly List<string> Events = new();
        public void StepStarted(string step, string detail) => Events.Add("start " + step);
        public void StepFinished(string step, TimeSpan elapsed, bool ok, string detail) => Events.Add($"end {step} {ok}");
        public void FileProgress(string step, int done, int total, string file) { }
        public void Line(string text) { }
        public void Message(Diagnostic d) { }
    }

    [Fact]
    public void BuildRomStepsIncludeLinkAndGbafix()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "mini_steps"));
        var log = new StepLog();
        var r = new GameBuilder(p, new Toolchain(), log).Build(new BuildOptions { Clean = true });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.Equal(
        [
            "start " + GameBuilder.StepAssets, "end " + GameBuilder.StepAssets + " True",
            "start " + GameBuilder.StepScripts, "end " + GameBuilder.StepScripts + " True",
            "start Generate game", "end Generate game True",
            "start " + GameBuilder.StepCompile, "end " + GameBuilder.StepCompile + " True",
            "start " + GameBuilder.StepLink, "end " + GameBuilder.StepLink + " True",
        ], log.Events);
        Assert.True(r.StepTimes.ContainsKey(GameBuilder.StepLink));
    }

    [Fact]
    public void MissingStringsUseTheLanguageFallbackOrShowTheKeyInDebug()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "mini_l10n"));
        SampleText.AddTo(p, "en");
        var thPath = Path.Combine(p.TextDir, "strings.th.json");
        var th = GameProject.Load<StringTable>(thPath);
        th.Strings.Remove("hud.score");
        GameProject.Save(thPath, th);
        // Keys are sorted: dlg.forest.hopper_01, hud.score, ui.menu.start; hud.score is run 1 of the Thai table.
        string? Run1(BuildConfiguration config)
        {
            var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { Configuration = config });
            Assert.True(r.Success, string.Join('\n', r.Diagnostics));
            var cpp = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "text", "strings_th.cpp"));
            var runs = System.Text.RegularExpressions.Regex.Match(cpp, @"runs\[\] = \{([^}]*)\}").Groups[1].Value.Split(", ");
            if (runs[1] == "nullptr") return null;
            return System.Text.RegularExpressions.Regex.Match(cpp, @"TextRun r1 = \{&\w+, r1g, (\d+),").Groups[1].Value;
        }
        Assert.Equal("11", Run1(BuildConfiguration.Debug));  // "[hud.score]" (Show missing keys)
        Assert.Null(Run1(BuildConfiguration.Release));       // the runtime falls back to the source language
        p.Settings.Localization.Languages.Single(l => l.Code == "th").Fallback = "en";
        p.SaveSettings();
        Assert.Equal("5", Run1(BuildConfiguration.Release));  // "SCORE" from the Thai language's own fallback
    }

    sealed class LineLog : IBuildObserver
    {
        public readonly List<string> Lines = new();
        public void StepStarted(string step, string detail) { }
        public void StepFinished(string step, TimeSpan elapsed, bool ok, string detail) { }
        public void FileProgress(string step, int done, int total, string file) { }
        public void Line(string text) { lock (Lines) Lines.Add(text); }
        public void Message(Diagnostic d) { }
    }

    [Fact]
    public void BuildsWithoutNinjaAndSkipsUpToDateObjects()
    {
        // A packaged editor may run where only devkitPro is installed: the same build graph runs in-process.
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "mini_no_ninja"));
        var tc = new Toolchain { Ninja = "no-such-ninja.exe" };
        var first = new LineLog();
        var r = new GameBuilder(p, tc, first).Build(new BuildOptions { Clean = true });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.True(File.Exists(r.RomPath));
        Assert.Contains(first.Lines, l => l.Contains("] CXX ", StringComparison.Ordinal));
        Assert.Contains(first.Lines, l => l.Contains("] LINK ", StringComparison.Ordinal));
        Assert.Equal("MINIPLAT", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(r.RomPath), 0xA0, 8));  // gbafix ran
        var second = new LineLog();
        r = new GameBuilder(p, tc, second).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.DoesNotContain(second.Lines, l => l.Contains("] CXX ", StringComparison.Ordinal));
    }

    [Fact]
    public void GccErrorsBecomeClickableDiagnostics()
    {
        var d = GameBuilder.ParseGccLine(@"D:\proj\scripts\PlayerController.cs:22:9: error: expected ';' before '}' token");
        Assert.NotNull(d);
        Assert.Equal(22, d!.Line);
        Assert.Equal(9, d.Column);
        Assert.Equal("PlayerController.cs(22,9): expected ';' before '}' token", d.ToString());
    }
}

[Collection("emulator")]
public sealed class TextPipelineTests(EmulatorFixture emu)
{
    [Fact]
    public void ThaiLabelInRomMatchesThePreviewRendererPixelForPixel()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "mini_text"));
        SampleText.AddTo(p);
        var builder = new GameBuilder(p, new Toolchain());
        var r = builder.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var core = emu.Core;
        core.LoadRom(r.RomPath);
        for (var i = 0; i < 60; i++) core.RunFrame();
        var frame = (uint[])core.Frame.Clone();
        core.UnloadRom();
        AgbStudio.Core.Png.WriteArgb(Path.Combine(p.BuildDir, "label.png"), frame, 240, 160);

        // Same font and layout through the C# renderer.
        var font = AgbStudio.Build.Fonts.FontBuilder.Build(GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font")), p.FontsDir,
            AgbStudio.Build.Fonts.ThaiText.CodePoints("เหรียญหายไปไหนหมดนะ?Where did all the coins go?SCOREคะแนนStartเริ่มเกม"));
        var layout = AgbStudio.Build.Fonts.TextLayout.Layout("เหรียญหายไปไหนหมดนะ?", font, 224);
        var w = 240 - 16;
        var h = font.LineHeight * layout.Lines;
        var expected = AgbStudio.Build.Fonts.TextRenderer.Render(layout, font, w, h, new AgbStudio.Build.Fonts.TextRenderer.Style(1, 4, 0));
        // The label box starts at tile (2, 2) = pixel (16, 16). Ink index 1 must be exactly the darkest colour on screen.
        uint? ink = null;
        var mismatches = 0;
        var inkPixels = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var e = expected[y * w + x];
                var screen = frame[(16 + y) * 240 + 16 + x];
                if (e == 1)
                {
                    inkPixels++;
                    ink ??= screen;
                    if (screen != ink) mismatches++;
                }
                else if (ink != null && screen == ink)
                {
                    mismatches++;
                }
            }
        Assert.True(inkPixels > 100, $"only {inkPixels} ink pixels in the expected render");
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void DictionaryWrapNeverSplitsWords()
    {
        var f = new FontFile { Name = "t", Source = "NotoSansThai-Regular.ttf", Size = 12, CharacterSets = { "Basic Latin", "Thai" } };
        var font = AgbStudio.Build.Fonts.FontBuilder.Build(f, Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts"));
        const string text = "ต้องรีบตามหาก่อนพระอาทิตย์ตก เหรียญหายไปไหนหมดนะ";
        foreach (var width in new[] { 60, 90, 120, 150 })
        {
            var layout = AgbStudio.Build.Fonts.TextLayout.Layout(text, font, width);
            Assert.All(layout.LineWidths, lw => Assert.True(lw <= width + 12, $"line {lw} px in a {width} px box"));
            // Every line break falls on a dictionary word boundary.
            var words = AgbStudio.Build.Fonts.ThaiText.WordStarts("ต้องรีบตามหาก่อนพระอาทิตย์ตก");
            Assert.Contains(0, words);
            Assert.True(AgbStudio.Build.Fonts.ThaiText.InDictionary("อาทิตย์"));
        }
        var lines = AgbStudio.Build.Fonts.TextLayout.Layout("ต้องรีบตามหาก่อนพระอาทิตย์ตก", font, 120);
        var firstLine = string.Concat(lines.Glyphs.Where(g => g.Line == 0).Select(g => char.ConvertFromUtf32(g.CodePoint)));
        Assert.True(firstLine is "ต้องรีบตามหา" or "ต้องรีบตามหาก่อน" or "ต้องรีบ", $"first line '{firstLine}'");
    }
}

[Collection("emulator")]
public sealed class LayerFeatureTests(EmulatorFixture emu)
{
    [Fact]
    public void ImageLayerAndAnimatedTilesShowInTheRom()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "mini_layers"));
        // Sky image layer behind the ground.
        var sky = new Canvas(256, 256);
        sky.Rect(0, 0, 256, 256, Canvas.Rgb(120, 190, 250));
        sky.Rect(0, 0, 256, 8, Canvas.Rgb(250, 250, 245));
        sky.Save(Path.Combine(p.AssetsDir, "images", "sky.png"));
        var mapPath = Path.Combine(p.MapsDir, "Forest_01.map");
        var map = GameProject.Load<MapFile>(mapPath);
        map.Layers.Insert(0, new MapLayerData { Name = "Sky", Bg = 0, Type = LayerType.Image, Image = "sky", Priority = 3, ParallaxX = 0.2, ParallaxY = 0.2, Wrap = true });
        map.CollisionLayer = 1;
        // Water tiles that alternate with dirt every 8 frames.
        for (var x = 2; x < 6; x++) map.Layers[1].Cells[8 * map.Width + x] = PixelArt.TWater;
        GameProject.Save(mapPath, map);
        var tsPath = Path.Combine(p.AssetsDir, "tilesets", "forest_tiles.png");
        var meta = p.LoadMeta<TilesetMeta>(tsPath);
        meta.Tiles[PixelArt.TWater].Animation = [PixelArt.TWater, PixelArt.TDirt];
        meta.Tiles[PixelArt.TWater].AnimationTicks = 8;
        GameProject.Save(GameProject.MetaPath(tsPath), meta);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));

        var core = emu.Core;
        core.LoadRom(r.RomPath);
        for (var i = 0; i < 40; i++) core.RunFrame();
        var a = (uint[])core.Frame.Clone();
        var changed = false;
        for (var i = 0; i < 9 && !changed; i++)
        {
            core.RunFrame();
            // Water cell at metatile (3, 8) -> screen pixel (3 * 16 + 8, 8 * 16 + 8).
            changed = core.Frame[(8 * 16 + 8) * 240 + 3 * 16 + 8] != a[(8 * 16 + 8) * 240 + 3 * 16 + 8];
        }
        AgbStudio.Core.Png.WriteArgb(Path.Combine(p.BuildDir, "layers.png"), core.Frame, 240, 160);
        core.UnloadRom();
        Assert.True(changed, "the animated water tile did not change on screen");
        // Sky colour at the top of the screen (white stripe at y < 8 scrolls with parallax 0.2 from camera y 0).
        var top = a[2 * 240 + 100];
        Assert.Equal(0xFFu, top >> 24);
        Assert.True(((top >> 16) & 0xFF) > 200 && (top & 0xFF) > 200, $"top pixel {top:X8}");
        var skyPixel = a[40 * 240 + 100];
        Assert.True((skyPixel & 0xFF) > 200 && ((skyPixel >> 16) & 0xFF) < 160, $"sky pixel {skyPixel:X8}");
    }
}
