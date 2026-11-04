using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Graphics;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;

namespace AgbStudio.Tests;

/// <summary>Bitmap video modes end to end: a project's scene in Mode 3, 4 or 5 with a picture builds and boots showing it,
/// with its actors over the picture and its map's collision working; wrong pictures are plain-language diagnostics; C#
/// scripts and cutscenes draw through Bitmap. Pictures go through the same converter as the Scene Editor's preview.</summary>
[Collection("emulator")]
public sealed class BitmapSceneTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");
    const string Backdrop = "#400080";

    // 150 colours in 16 px blocks, with a see-through square at (8..23, 8..23) in the variants that have one.
    static uint Block(int x, int y) => Canvas.Rgb(Math.Min(255, x / 16 * 16), Math.Min(255, y / 16 * 24), 128);

    static string AddImage(GameProject p, string name, int w, int h, Func<int, int, uint> color)
    {
        var c = new Canvas(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++) c.Set(x, y, color(x, y));
        var dir = Path.Combine(p.AssetsDir, "images");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".png");
        c.Save(path);
        return path;
    }

    static GameProject Project(string name, int mode, string picture)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.VideoMode = mode;
        scene.Picture = picture;
        scene.Backdrop = Backdrop;
        scene.Links.Clear();
        GameProject.Save(scenePath, scene);
        return p;
    }

    static BuildResult Build(GameProject p, bool scripts = false) =>
        new GameBuilder(p, new Toolchain()) { Scripts = scripts ? new ScriptBackend() : null }.Build();

    static uint Argb(ushort c)
    {
        uint Expand(int v) => (uint)((v << 3) | (v >> 2));
        return 0xFF000000u | (Expand(c & 31) << 16) | (Expand((c >> 5) & 31) << 8) | Expand((c >> 10) & 31);
    }

    // Within one step of 31 per channel, as VideoModeTests.
    static bool Close(uint a, uint b)
    {
        bool Ch(int shift) => Math.Abs((int)((a >> shift) & 255) - (int)((b >> shift) & 255)) <= 8;
        return Ch(16) && Ch(8) && Ch(0);
    }

    uint[] RunTo(string rom, Func<uint[], bool> ready, int frames = 900)
    {
        var core = emu.Core;
        core.Keys = GbaKeys.None;
        core.LoadRom(rom);
        uint[] frame = [];
        for (var f = 0; f < frames; f++)
        {
            core.RunFrame();
            frame = (uint[])core.Frame.Clone();
            if (ready(frame)) break;
        }
        return frame;
    }

    uint[] Booted(string rom)
    {
        var frame = RunTo(rom, _ => false, 360);
        emu.Core.UnloadRom();
        return frame;
    }

    // The row area with no actors in it is exact; lower down, the actors are the only differences.
    static void AssertShows(uint[] frame, Func<int, int, uint> expected, string what)
    {
        for (var y = 0; y < 160; y++)
            for (var x = 0; x < 240; x++)
                if (y < 60) Assert.True(Close(frame[y * 240 + x], expected(x, y)), $"{what}: ({x},{y}) is {frame[y * 240 + x]:X8}, expected {expected(x, y):X8}");
        var differs = Enumerable.Range(60 * 240, 100 * 240).Count(i => !Close(frame[i], expected(i % 240, i / 240)));
        Assert.InRange(differs, 1, 1500);  // a sprite shows over the picture, and nothing else moved
    }

    [Fact]
    public void Mode3SceneBootsShowingItsPictureWithActorsOverIt()
    {
        var p = Project("bitmap_mode3", 3, "poster");
        AddImage(p, "poster", 240, 160, Block);
        var r = Build(p);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        // Its map stays in ROM for collision; the build says the layers do not show.
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("uses Mode 3") && d.Message.Contains("map layers"));
        var gen = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "pictures", "picture_poster.cpp"));
        Assert.Contains("agbasset_picture_poster = {kBgr, nullptr, nullptr, 0, 240, 160}", gen);
        AssertShows(Booted(r.RomPath), (x, y) => Argb(Gba.ToBgr555(Block(x, y))), "Mode 3");
    }

    [Fact]
    public void Mode4SceneBootsWithPaletteIndicesAndSeeThroughBackdrop()
    {
        var p = Project("bitmap_mode4", 4, "poster");
        AddImage(p, "poster", 240, 160, (x, y) => x is >= 8 and < 24 && y is >= 8 and < 24 ? 0u : Block(x, y));
        var r = Build(p);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.DoesNotContain(r.Diagnostics, d => d.Message.Contains("colours and Mode 4"));
        var gen = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "pictures", "picture_poster.cpp"));
        Assert.Contains("nullptr, reinterpret_cast<const unsigned char*>(kIndices), kPalette, 150, 240, 160}", gen);
        var backdrop = Argb(Gba.ToBgr555(0xFF400080u));
        AssertShows(Booted(r.RomPath), (x, y) => x is >= 8 and < 24 && y is >= 8 and < 24 ? backdrop : Argb(Gba.ToBgr555(Block(x, y))), "Mode 4");
    }

    [Fact]
    public void Mode4ReducesAnImageWithTooManyColoursAndSaysSo()
    {
        var p = Project("bitmap_mode4_many", 4, "gradient");
        var path = AddImage(p, "gradient", 240, 160, (x, y) => Canvas.Rgb(x * 255 / 239, y * 255 / 159, (x + y) * 255 / 398));
        var r = Build(p);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var warning = Assert.Single(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("Mode 4 shows 255"));
        Assert.Contains("so the build reduced it to", warning.Message);
        Assert.Contains("To keep every colour, use Mode 3", warning.Message);
        // The ROM shows the converter's result, which is what the Scene Editor draws.
        var indexed = BitmapConverter.ToIndexed(ColorImage.Load(path));
        Assert.True(indexed.Reduced);
        Assert.InRange(indexed.Palette.Length, 2, 255);
        Assert.DoesNotContain((byte)0, indexed.Indices);
        AssertShows(Booted(r.RomPath), (x, y) => Argb(indexed.Palette[indexed.Indices[y * 240 + x] - 1]), "Mode 4 reduced");
    }

    [Fact]
    public void Mode5SceneBootsAtTheHardwareSizeWithTheBackdropAroundIt()
    {
        var p = Project("bitmap_mode5", 5, "poster");
        AddImage(p, "poster", 160, 128, Block);
        var r = Build(p);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var backdrop = Argb(Gba.ToBgr555(0xFF400080u));
        AssertShows(Booted(r.RomPath), (x, y) => x < 160 && y < 128 ? Argb(Gba.ToBgr555(Block(x, y))) : backdrop, "Mode 5");
    }

    [Fact]
    public void ActorsStandOnTheMapInABitmapScene()
    {
        var p = Project("bitmap_collision", 4, "poster");
        AddImage(p, "poster", 240, 160, Block);
        var r = Build(p);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var core = emu.Core;
        core.Keys = GbaKeys.None;
        core.LoadRom(r.RomPath);
        DebugChannel? ch = null;
        for (var f = 0; f < 240; f++)
        {
            core.RunFrame();
            ch ??= DebugChannel.Find(core);
        }
        Assert.NotNull(ch);
        var player = -1;
        for (uint i = 0; i < ch!.ActorCount; i++)
            if (Inspector.ReadString(core, core.ReadU32(ch.ActorTable + i * ch.ActorStride)) == "Player") player = (int)i;
        Assert.True(player >= 0);
        // The ground row of GroundMap is at 16 px tiles 8 and 9: the player lands on it (no layer was loaded for it).
        var y = (int)core.ReadU32(ch.ActorTable + (uint)player * ch.ActorStride + 8) / 256;
        core.UnloadRom();
        Assert.InRange(y, 90, 150);
    }

    [Fact]
    public void ScenesChangeBetweenBitmapAndTileModes()
    {
        // Mode 4 scene that flips to page 1, then a Mode 0 scene, then the Mode 4 scene again.
        var p = Project("bitmap_switch", 4, "poster");
        AddImage(p, "poster", 240, 160, Block);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Hopper.cs"), """
using GBA;

public class Hopper : Behaviour
{
    public int target = 0;
    public int flips = 0;
    int frame;

    void Update()
    {
        frame = frame + 1;
        if (frame == 20 && flips > 0)
        {
            Bitmap.Flip();
            Bitmap.Clear(1);
        }
        if (frame == 90) Scene.LoadAt(target, 40, 100);
    }
}
""");
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Components.Add(new ComponentData { Type = "Hopper", Fields = { ["target"] = "1", ["flips"] = "1" } });
        GameProject.Save(scenePath, scene);
        var other = Path.Combine(p.ScenesDir, "Forest_02.scene");
        var second = GameProject.Load<SceneFile>(other);
        second.Actors[0].Components.Add(new ComponentData { Type = "Hopper", Fields = { ["target"] = "0" } });
        second.Backdrop = "#008040";
        GameProject.Save(other, second);
        var r = Build(p, scripts: true);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));

        var core = emu.Core;
        core.Keys = GbaKeys.None;
        core.LoadRom(r.RomPath);
        bool Wait(Func<bool> ready, int frames = 600)
        {
            for (var f = 0; f < frames; f++)
            {
                core.RunFrame();
                if (ready()) return true;
            }
            return false;
        }
        const uint DispCnt = 0x04000000;
        Assert.True(Wait(() => (core.ReadU16(DispCnt) & 0x17) == 0x14), "the Mode 4 scene never showed page 1");
        // Tile mode next: the page bit and BG2's frame buffer are gone, and queued drawing does not land on its tiles.
        Assert.True(Wait(() => (core.ReadU16(DispCnt) & 7) == 0), "the Mode 0 scene never came");
        Assert.Equal(0, core.ReadU16(DispCnt) & 0x10);
        Assert.True(Wait(() => Close(core.Frame[0], Argb(Gba.ToBgr555(0xFF008040u)))), "the Mode 0 scene never showed its backdrop");
        // Back again: page 0, the picture whole, on a palette loaded again.
        Assert.True(Wait(() => (core.ReadU16(DispCnt) & 7) == 4), "the Mode 4 scene never came back");
        Assert.True(Wait(() => Close(core.Frame[10 * 240 + 100], Argb(Gba.ToBgr555(Block(100, 10))))), "the picture never came back");
        var frame = (uint[])core.Frame.Clone();
        core.UnloadRom();
        for (var i = 0; i < 40 * 240; i++) Assert.True(Close(frame[i], Argb(Gba.ToBgr555(Block(i % 240, i / 240)))), $"({i % 240},{i / 240})");
    }

    [Fact]
    public void WrongPicturesAreDiagnosticsNotCrashes()
    {
        // Missing, with what is wrong and then how to fix it.
        var p = Project("bitmap_missing", 3, "nowhere");
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.False(r.Success);
        var e = Assert.Single(r.Errors);
        Assert.Equal("Scene Forest_01 shows the picture 'nowhere', which is not in assets/images. Import the image, or pick another picture in the Video Mode popover.", e.Message);
        Assert.EndsWith("Forest_01.scene", e.File);

        // Wrong size for each mode.
        foreach (var (mode, w, h, want) in new[] { (3, 160, 128, "240×160"), (4, 64, 64, "240×160"), (5, 240, 160, "160×128") })
        {
            p = Project($"bitmap_size{mode}", mode, "poster");
            AddImage(p, "poster", w, h, Block);
            r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
            Assert.False(r.Success);
            e = Assert.Single(r.Errors);
            Assert.Equal($"Scene Forest_01 shows the picture 'poster', which is {w}×{h}; Mode {mode} shows {want} pictures. Resize the image to {want} in your paint program, or pick another picture in the Video Mode popover.", e.Message);
        }

        // A file that is not an image is an error too, not a crash of the picture step.
        p = Project("bitmap_broken", 3, "poster");
        // (an image layer conversion reads assets/images first, so the message is the build's own for that file)
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        File.WriteAllText(Path.Combine(p.AssetsDir, "images", "poster.png"), "not a png");
        r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);

        // A picture in a tile mode is left out, with a warning; the video mode range is checked.
        p = Project("bitmap_tilemode", 0, "poster");
        AddImage(p, "poster", 240, 160, Block);
        r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("Mode 0 draws tile layers, so the picture is not shown"));
        Assert.False(File.Exists(Path.Combine(p.BuildDir, "gen", "pictures", "picture_poster.cpp")));
        p = Project("bitmap_badmode", 7, "");
        r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        Assert.Contains(r.Errors, d => d.Message.StartsWith("Scene Forest_01 has video mode 7; the GBA has modes 0 to 5.", StringComparison.Ordinal));
    }

    [Fact]
    public void ScenesWithoutThePictureFieldStillLoadAndBudgetsCountTheBitmapModes()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "bitmap_old_scene"));
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var json = File.ReadAllText(scenePath);
        Assert.Contains("\"picture\"", json);
        File.WriteAllText(scenePath, System.Text.RegularExpressions.Regex.Replace(json, "\\s*\"picture\": \"\",", ""));
        Assert.DoesNotContain("\"picture\"", File.ReadAllText(scenePath));
        var scene = GameProject.Load<SceneFile>(scenePath);
        Assert.Equal("", scene.Picture);
        // No picture and a tile mode: the generated scene is as it was (no extra initialisers).
        var builder = new GameBuilder(p, new Toolchain());
        Assert.True(builder.Build(new BuildOptions { GenerateOnly = true }).Success);
        Assert.DoesNotContain("agbasset_picture", File.ReadAllText(Path.Combine(p.BuildDir, "gen", "scenes", "scene_Forest_01.cpp")));
        var tileBytes = builder.Assets.Scenes["Forest_01"].BgBytes;
        Assert.True(tileBytes > 0);

        // In each bitmap mode: no background layers or text count, the frame buffers do not hit the 64 KB check, and the
        // sprites have 16 KB.
        foreach (var mode in new[] { 3, 4, 5 })
        {
            scene.VideoMode = mode;
            GameProject.Save(scenePath, scene);
            builder = new GameBuilder(p, new Toolchain());
            var r = builder.Build(new BuildOptions { GenerateOnly = true });
            Assert.True(r.Success, string.Join('\n', r.Diagnostics));
            Assert.Equal(0, builder.Assets.Scenes["Forest_01"].BgBytes);
            Assert.Equal(GameBuilder.ObjVramBitmapBytes, 16 * 1024);
        }
    }

    const string Painter = """
using GBA;

public class Painter : Behaviour
{
    int frame;

    void Update()
    {
        frame = frame + 1;
        if (frame == 10)
        {
            Bitmap.SetPaletteColor(10, Bitmap.Rgb(0, 0, 16));
            Bitmap.SetPaletteColor(11, Bitmap.Rgb(31, 0, 0));
            Bitmap.SetPaletteColor(12, Bitmap.Rgb(0, 31, 0));
            Bitmap.SetPaletteColor(13, Bitmap.Rgb(31, 31, 0));
            Bitmap.Clear(COLOR_A);
            Bitmap.FillRect(11, 20, 31, 10, COLOR_B);
            Bitmap.SetPixel(100, 50, COLOR_C);
            Bitmap.SetPixel(101, 50, COLOR_D);
            Bitmap.Flip();
        }
        if (frame == 120)
        {
            Bitmap.Show("poster");
            Bitmap.Flip();
        }
    }
}
""";

    static string PainterFor(int mode) => Painter
        .Replace("COLOR_A", mode == 4 ? "10" : "Bitmap.Rgb(0, 0, 16)").Replace("COLOR_B", mode == 4 ? "11" : "Bitmap.Rgb(31, 0, 0)")
        .Replace("COLOR_C", mode == 4 ? "12" : "Bitmap.Rgb(0, 31, 0)").Replace("COLOR_D", mode == 4 ? "13" : "Bitmap.Rgb(31, 31, 0)");

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ScriptsDrawThroughTheBitmapApi(int mode)
    {
        // The scene starts with no picture: the script draws, then shows one no scene names.
        var p = Project($"bitmap_script{mode}", mode, "");
        var (w, h) = BitmapConverter.PictureSize(mode);
        AddImage(p, "poster", w, h, Block);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Painter.cs"), PainterFor(mode));
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Components.Add(new ComponentData { Type = "Painter" });
        GameProject.Save(scenePath, scene);
        var r = Build(p, scripts: true);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));

        uint Rgb(int red, int green, int blue) => Argb((ushort)(red | (green << 5) | (blue << 10)));
        var blue = Rgb(0, 0, 16);
        var frame = RunTo(r.RomPath, f => Close(f[50 * 240 + 100], Rgb(0, 31, 0)) && Close(f[50 * 240 + 101], Rgb(31, 31, 0)) && Close(f[0], blue));
        Assert.True(Close(frame[50 * 240 + 100], Rgb(0, 31, 0)), "SetPixel did not arrive");
        Assert.True(Close(frame[50 * 240 + 101], Rgb(31, 31, 0)), "the neighbour SetPixel did not arrive");
        Assert.True(Close(frame[50 * 240 + 102], blue));
        Assert.True(Close(frame[0], blue), "Clear did not arrive");
        Assert.True(Close(frame[20 * 240 + 11], Rgb(31, 0, 0)), "FillRect's first pixel");
        Assert.True(Close(frame[29 * 240 + 41], Rgb(31, 0, 0)), "FillRect's last pixel");
        Assert.True(Close(frame[20 * 240 + 10], blue), "left of FillRect");
        Assert.True(Close(frame[20 * 240 + 42], blue), "right of FillRect");
        Assert.True(Close(frame[30 * 240 + 11], blue), "under FillRect");
        if (mode == 5) Assert.True(Close(frame[10 * 240 + 200], Argb(Gba.ToBgr555(0xFF400080u))), "Mode 5 stays 160 px wide");

        // Then Bitmap.Show: the picture replaces the drawing.
        // The flip waits for the whole picture, so no frame shows part of it: the first frame with it has every row right.
        var core = emu.Core;
        var shown = false;
        for (var f = 0; f < 600 && !shown; f++)
        {
            core.RunFrame();
            shown = Close(core.Frame[10 * 240 + 100], Argb(Gba.ToBgr555(Block(100, 10))));
        }
        var finalFrame = (uint[])core.Frame.Clone();
        core.UnloadRom();
        Assert.True(shown, "Bitmap.Show never arrived");
        Assert.True(Close(finalFrame[0], Argb(Gba.ToBgr555(Block(0, 0)))));
        Assert.True(Close(finalFrame[50 * 240 + 100], Argb(Gba.ToBgr555(Block(100, 50)))));
        var symbol = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "game.cpp"));
        Assert.Contains("kPictures", symbol);
    }

    [Fact]
    public void CutscenesDrawThroughTheNatives()
    {
        var p = Project("bitmap_cutscene", 4, "");
        AddImage(p, "poster", 240, 160, Block);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Forest_01.cs"), """
using System.Threading.Tasks;
using GBA;

public class Forest_01 : SceneScript
{
    void OnEnter() { Paint(); }

    [Cutscene]
    static async Task Paint()
    {
        Bitmap.SetPaletteColor(20, Bitmap.Rgb(0, 31, 31));
        Bitmap.SetPaletteColor(21, Bitmap.Rgb(31, 0, 31));
        Bitmap.Clear(20);
        Bitmap.FillRect(31, 5, 3, 3, 21);
        Bitmap.SetPixel(60, 6, 21);
        Bitmap.Flip();
        await Wait(30);
        Bitmap.Show("poster");
        Bitmap.Flip();
    }
}
""");
        var r = Build(p, scripts: true);
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        uint Rgb(int red, int green, int blue) => Argb((ushort)(red | (green << 5) | (blue << 10)));
        var frame = RunTo(r.RomPath, f => Close(f[6 * 240 + 60], Rgb(31, 0, 31)) && Close(f[0], Rgb(0, 31, 31)));
        Assert.True(Close(frame[6 * 240 + 60], Rgb(31, 0, 31)));
        Assert.True(Close(frame[6 * 240 + 31], Rgb(31, 0, 31)));
        Assert.True(Close(frame[6 * 240 + 33], Rgb(31, 0, 31)));
        Assert.True(Close(frame[6 * 240 + 30], Rgb(0, 31, 31)));
        Assert.True(Close(frame[6 * 240 + 34], Rgb(0, 31, 31)));
        var shown = false;
        for (var f = 0; f < 300 && !shown; f++)
        {
            emu.Core.RunFrame();
            shown = Close(emu.Core.Frame[6 * 240 + 60], Argb(Gba.ToBgr555(Block(60, 6))));
        }
        emu.Core.UnloadRom();
        Assert.True(shown, "Bitmap.Show in the cutscene never arrived");
    }

    [Fact]
    public void AScriptPictureWithNoSceneToShowItIsADiagnostic()
    {
        var script = "using GBA;\n\npublic class Painter : Behaviour\n{\n    void Start() { Bitmap.Show(\"poster\"); }\n}\n";
        // 64 x 64 fits no mode: an error.
        var p = Project("bitmap_script_size", 3, "");
        AddImage(p, "poster", 64, 64, Block);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Painter.cs"), script);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build(new BuildOptions { GenerateOnly = true });
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message.StartsWith("A script shows the picture 'poster', which is 64×64; Modes 3 and 4 show 240×160 pictures and Mode 5 shows 160×128.", StringComparison.Ordinal));
        // 160 x 128 with only a Mode 3 scene: left out, with a warning.
        p = Project("bitmap_script_mode", 3, "");
        AddImage(p, "poster", 160, 128, Block);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Painter.cs"), script);
        r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build(new BuildOptions { GenerateOnly = true });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("no scene uses Mode 5"));
    }
}
