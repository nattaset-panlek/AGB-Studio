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

    // A sprite sheet of 64 x 64 frames in 15 colours, every frame a different pattern.
    static void BigSprite(GameProject p, string name, int frames, int seed)
    {
        var path = Path.Combine(p.AssetsDir, "sprites", name + ".png");
        var c = new Canvas(64 * frames, 64);
        var rng = new Random(seed);
        var colours = Enumerable.Range(0, 15).Select(_ => Canvas.Rgb(rng.Next(256), rng.Next(256), rng.Next(256))).ToArray();
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64 * frames; x++) c.Set(x, y, colours[(x * 7 + y * 13 + x / 64 * 5) % 15]);
        c.Save(path);
        GameProject.Save(GameProject.MetaPath(path), new SpriteMeta { FrameWidth = 64, FrameHeight = 64, OriginX = 32, OriginY = 64 });
    }

    static void AddActors(GameProject p, IEnumerable<string> sprites)
    {
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        var i = 0;
        foreach (var sprite in sprites)
            scene.Actors.Add(new ActorData { Name = $"Big {i}", Kind = ActorKind.Enemy, Sprite = sprite, X = 20 + i++ * 8, Y = 100 });
        GameProject.Save(scenePath, scene);
    }

    [Fact]
    public void TooManySpriteFramesForObjVramFailTheBuild()
    {
        // G-HW.01: 18 actors of one 64 x 64 sprite with 18 frames can show 18 different frames of 2 KB at once: 36 KB
        // plus the scene's own sprites, more than the 32 KB of OBJ VRAM.
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "limit_obj_vram"));
        BigSprite(p, "boss", 18, 1);
        AddActors(p, Enumerable.Repeat("boss", 18));
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.False(r.Success);
        var e = Assert.Single(r.Errors);
        Assert.StartsWith("Scene Forest_01 needs up to 37 KB of OBJ VRAM for the sprite frames its actors show at once; the GBA has 32 KB.", e.Message);
        Assert.EndsWith("Forest_01.scene", e.File);
        // 12 of them fit: 24 KB plus hero 16x16, slime 16x16 (from 16x12), coin 8x8 and flag 16x32.
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors.RemoveAll(a => a.Sprite == "boss" && int.Parse(a.Name[4..]) >= 12);
        GameProject.Save(scenePath, scene);
        var builder = new GameBuilder(p, new Toolchain());
        Assert.True(builder.Build(new BuildOptions { GenerateOnly = true }).Success);
        Assert.Equal(12 * 2048 + (4 + 4 + 1 + 8) * 32, builder.Assets.Scenes["Forest_01"].ObjBytes);
        // Bitmap modes keep only the upper 16 KB of OBJ VRAM.
        scene.VideoMode = 3;
        GameProject.Save(scenePath, scene);
        r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.Contains(r.Errors, d => d.Message.Contains("the GBA has 16 KB in bitmap modes", StringComparison.Ordinal));
    }

    [Fact]
    public void TooManySpritePalettesFailTheBuildUnlessSpritesShareThem()
    {
        // G-HW.01 / G-HW.03: 13 sprites with palettes of their own plus the scene's 4 need 17 of the 16 OBJ palettes.
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "limit_obj_palettes"));
        var names = Enumerable.Range(0, 13).Select(i => $"gem{i}").ToList();
        for (var i = 0; i < names.Count; i++)
        {
            var path = Path.Combine(p.AssetsDir, "sprites", names[i] + ".png");
            var c = new Canvas(8, 8);
            c.Rect(0, 0, 8, 8, Canvas.Rgb(20 * i, 255 - 15 * i, 100));
            c.Save(path);
            GameProject.Save(GameProject.MetaPath(path), new SpriteMeta { FrameWidth = 8, FrameHeight = 8 });
        }
        AddActors(p, names);
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message.StartsWith("Scene Forest_01 needs 17 sprite palettes; the GBA has 16.", StringComparison.Ordinal));
        // One gem shares another's palette (Sprite Editor · Palette): 16 banks, and the build points both at one palette.
        var shared = Path.Combine(p.AssetsDir, "sprites", "gem12.png");
        GameProject.Save(GameProject.MetaPath(shared), new SpriteMeta { FrameWidth = 8, FrameHeight = 8, Palette = "gem0" });
        var b = new GameBuilder(p, new Toolchain());
        r = b.Build(new BuildOptions { GenerateOnly = true });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.Equal(16, b.Assets.Scenes["Forest_01"].ObjBanks);
        var cpp = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "assets", "sprite_gem12.cpp"));
        Assert.Contains("extern const agb::data::Palette agbasset_sprite_gem0_pal;", cpp);
        Assert.Contains("&agbasset_sprite_gem0_pal,", cpp);
        Assert.DoesNotContain("agbasset_sprite_gem12_colors", cpp);
    }

    [Fact]
    public void SaveDataThatDoesNotFitTheSaveTypeFailsTheBuild()
    {
        // G-HW.06: the slot layout of runtime save.cpp (two copies per slot, a 16-byte header each, Flash in 4 KB
        // sectors, EEPROM in 8-byte blocks, at most 4 KB a slot).
        Assert.Equal(8 + 4 + 1 + 3, SaveLayout.SlotBytes([new SaveField { Name = "score", Type = "int" }, new SaveField { Name = "lives", Type = "byte" }], false));
        // The Figma Save data tab fields with the header, padding and the saved script store.
        SaveField[] figma =
        [
            new() { Name = "score", Type = "int" }, new() { Name = "lives", Type = "byte" }, new() { Name = "coins", Type = "short" },
            new() { Name = "flags", Type = "bitset 128" }, new() { Name = "inventory", Type = "Items[16]" }, new() { Name = "position", Type = "fixed2" },
        ];
        Assert.Equal(63, figma.Sum(f => f.SizeBytes()));
        Assert.Equal(8 + 4 + 8 + 2 + 32 + 1 + 16 + 1 + 160, SaveLayout.SlotBytes(figma, true));
        Assert.Equal(4096, SaveLayout.SlotCapacity(SaveType.Sram32K, 3));   // 5460 B a copy, capped at 4 KB a slot
        Assert.Equal(1360 - 16, SaveLayout.SlotCapacity(SaveType.Eeprom8K, 3));
        Assert.Equal(4096, SaveLayout.SlotCapacity(SaveType.Flash128K, 4));  // 16 KB a copy, capped
        Assert.Equal(0, SaveLayout.SlotCapacity(SaveType.Flash64K, 9));   // 3640 B a copy: less than a 4 KB sector
        Assert.Equal(0, SaveLayout.SlotCapacity(SaveType.None, 3));
        // the options block is kept outside the slots: 8 B at the end of SRAM and EEPROM, the last sector on Flash
        Assert.Equal([0, 8, 4096, 4096, 8], new[] { SaveType.None, SaveType.Sram32K, SaveType.Flash64K, SaveType.Flash128K, SaveType.Eeprom8K }.Select(SaveLayout.OptionsReserve));
        Assert.Equal(8, SaveLayout.OptionsBytes);

        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "limit_save"));
        p.Settings.Save.Type = SaveType.Eeprom8K;
        p.Settings.Save.Slots = 3;
        p.Settings.Save.Fields.Add(new SaveField { Name = "map", Type = "Items[700]" });
        p.SaveSettings();
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message == "Save data needs 1416 B per slot, but EEPROM 8 KB with 3 slots has room for 1344 B per slot " +
            "(each slot keeps two copies). Use fewer slots, a bigger save type or fewer save fields (Project Settings · Save data)." && d.File == p.ProjectPath);
        Assert.Equal("Generate game", r.FailedStep);
        // One slot fits.
        p.Settings.Save.Slots = 1;
        p.SaveSettings();
        var b = new GameBuilder(p, new Toolchain());
        Assert.True(b.Build(new BuildOptions { GenerateOnly = true }).Success);
        Assert.Equal((1416L, 4072L), (b.Assets.SaveBytes, b.Assets.SaveCapacity));
        // Flash 64 KB cannot hold 9 slots of whole sectors at all.
        p.Settings.Save.Type = SaveType.Flash64K;
        p.Settings.Save.Slots = 9;
        p.SaveSettings();
        Assert.Contains(new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true }).Errors,
            d => d.Message.StartsWith("Save data needs 1416 B per slot, but Flash 64 KB with 9 slots has room for 0 B per slot", StringComparison.Ordinal));
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
