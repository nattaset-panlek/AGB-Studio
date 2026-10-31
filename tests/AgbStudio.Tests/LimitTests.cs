using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>
/// G-HW.01 / G-HW.03: scenes that cannot fit the hardware fail the build with a message that names the scene and the
/// fix. G-ERR.03: no silent failure: missing strings, missing glyphs and full actor pools reach the Console (and fail
/// the build where Localization says so).
/// </summary>
[Collection("emulator")]
public sealed class LimitTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    // Noise: every 8x8 tile is different and uses 15 colours of its own.
    static Canvas Noise(int seed, int w, int h)
    {
        var c = new Canvas(w, h);
        var rng = new Random(seed);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) c.Set(x, y, Canvas.Rgb(rng.Next(256), rng.Next(256), rng.Next(256)));
        return c;
    }

    // 13 tiles of 8 x 8, each with 15 colours of its own: 13 palettes after packing, the most one image may use.
    static Canvas ThirteenPalettes(int seed)
    {
        var c = new Canvas(13 * 8, 8);
        var rng = new Random(seed);
        for (var t = 0; t < 13; t++)
        {
            var colors = Enumerable.Range(0, 15).Select(_ => Canvas.Rgb(rng.Next(256), rng.Next(256), rng.Next(256))).ToArray();
            for (var i = 0; i < 64; i++) c.Set(t * 8 + i % 8, i / 8, colors[i % 15]);
        }
        return c;
    }

    static GameProject WithImageLayers(string name, int count, int size, bool palettes = false)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        var mapPath = Path.Combine(p.MapsDir, "Forest_01.map");
        var map = GameProject.Load<MapFile>(mapPath);
        map.Layers.RemoveAll(l => l.Type == LayerType.Image);
        for (var i = 0; i < count; i++)
        {
            (palettes ? ThirteenPalettes(i + 1) : Noise(i + 1, size, size)).Save(Path.Combine(p.AssetsDir, "images", $"noise{i}.png"));
            map.Layers.Insert(0, new MapLayerData { Name = $"Noise {i}", Type = LayerType.Image, Bg = 3 - i, Priority = 3, Image = $"noise{i}", Wrap = true });
        }
        GameProject.Save(mapPath, map);
        return p;
    }

    [Fact]
    public void TooMuchBackgroundVramFailsTheBuildWithTheScene()
    {
        // Three 256 x 256 images of unique tiles: 3 x 32 KB of tiles alone.
        var p = WithImageLayers("limit_vram", 3, 256);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message.Contains("Scene Forest_01 needs", StringComparison.Ordinal) && d.Message.Contains("of background VRAM; the GBA has 64 KB"));
    }

    [Fact]
    public void TooManyBackgroundPalettesFailTheBuild()
    {
        // Two images that need 13 palettes each: 26, more than the 14 a scene has.
        var p = WithImageLayers("limit_palettes", 2, 0, palettes: true);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message.Contains("background palettes; 14 are free", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingStringsWarnOrFailAsLocalizationSays()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "limit_strings"));
        SampleText.AddTo(p, "en");
        var thPath = Path.Combine(p.TextDir, "strings.th.json");
        var th = GameProject.Load<StringTable>(thPath);
        th.Strings.Remove("hud.score");
        GameProject.Save(thPath, th);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success);
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("1 strings missing in ไทย"));
        p.Settings.Localization.FailOnMissing = true;
        p.SaveSettings();
        r = new GameBuilder(p, new Toolchain()).Build();
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message.Contains("1 strings missing in ไทย"));
    }

    [Fact]
    public void MissingGlyphsAreReported()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "limit_glyphs"));
        SampleText.AddTo(p, "en");
        var enPath = Path.Combine(p.TextDir, "strings.en.json");
        var en = GameProject.Load<StringTable>(enPath);
        en.Strings["hud.score"] = "SCORE →";  // an arrow the Thai font does not have
        GameProject.Save(enPath, en);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("U+2192"));
    }

    [Fact]
    public void AFullActorPoolIsReportedInTheConsole()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "limit_pool"));
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Flood.cs"),
            "using GBA;\n\npublic class Flood : Behaviour\n{\n    int n;\n\n    void Update()\n    {\n        if (n < 80) Scene.Spawn(\"Slime\", 40 + n, 40);\n        n += 1;\n    }\n}\n");
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Components.Add(new ComponentData { Type = "Flood" });
        GameProject.Save(scenePath, scene);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 200; i++) s.Step();
        Assert.Contains(s.Log, l => l.Contains("actor slots are in use", StringComparison.Ordinal));
        s.Stop();
    }
}
