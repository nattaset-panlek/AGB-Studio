using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Emu;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.SceneEditor;
using AgbStudio.Scripting;
using Xunit.Abstractions;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>
/// P11.10 / D-18: the viewport renderer draws a scene exactly as the ROM does. The Sky Hopper project is built with
/// GameBuilder (scripts through the scripting compiler) into this worktree's build folder and run in the mGBA core; the
/// 240 x 160 frame is compared pixel for pixel (BGR555) with <see cref="GbaScreenRenderer"/> at the game's camera.
/// Actors that move (physics, patrol) or animate (frames change every few ticks) are masked at their editor position
/// and at their live position read from the debug channel; every other pixel (the backgrounds with parallax, the text
/// layer and the still actors) must be equal. Palette RAM and the OAM entries of still sprites are compared as well.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class ViewportRomTests(ITestOutputHelper log)
{
    static readonly Lazy<GameProject> Project = new(() =>
    {
        var p = SkyHopper.Create(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "scene_viewport"));
        p.Settings.General.StartScene = "Forest_01";
        p.SaveSettings();
        return p;
    });

    static BuildResult Build(GameProject p)
    {
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build(new BuildOptions { Configuration = BuildConfiguration.Debug });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics.Where(d => d.Severity == Severity.Error)));
        return r;
    }

    sealed record Capture(uint[] Frame, List<LiveActor> Actors, ushort[] Palette, List<OamEntry> Oam);

    static Capture Run(string rom, int frames)
    {
        // the process core, shared with the other editor tests (D-06)
        using var host = new EmulatorHost();
        var core = host.Acquire();
        var s = new PlaySession(core);
        s.Start(rom);
        for (var i = 0; i < frames; i++) s.Step();
        var c = new Capture((uint[])core.Frame.Clone(), s.Actors(), Inspector.PaletteRam(core), Inspector.Oam(core));
        s.Stop();
        return c;
    }

    /// <summary>Compares the emulator frame with the renderer; returns (compared pixels, mismatches).</summary>
    (int Compared, int Mismatch) Compare(SceneAssets assets, SceneFile scene, MapFile? map, Capture rom, string name, (int X, int Y, int W, int H)? notDrawn = null)
    {
        // the camera the game has now: the follow actor at its live position (the camera has settled on it)
        var live = SceneFiles.Copy(scene);
        foreach (var a in live.Actors)
            if (rom.Actors.FirstOrDefault(x => x.Name == a.Name) is { } la) (a.X, a.Y) = ((int)Math.Floor(la.X), (int)Math.Floor(la.Y));
        var (camX, camY) = GbaScreenRenderer.StartCamera(live, map);
        var frame = GbaScreenRenderer.Render(assets, scene, map, camX, camY);
        log.WriteLine($"{name}: camera {camX},{camY}; text layer BG{frame.TextLayer}; sprites {frame.Sprites.Count}");

        // moving or animated actors: masked where the editor draws them and where the game has them now
        var mask = new bool[240 * 160];
        void Mask(int left, int top, int w, int h)
        {
            for (var y = Math.Max(0, top); y < Math.Min(160, top + h); y++)
                for (var x = Math.Max(0, left); x < Math.Min(240, left + w); x++) mask[y * 240 + x] = true;
        }
        if (notDrawn is { } nd) Mask(nd.X, nd.Y, nd.W, nd.H);
        var masked = new HashSet<int>();
        for (var i = 0; i < scene.Actors.Count; i++)
        {
            var a = scene.Actors[i];
            if (a.Sprite.Length == 0 || assets.Sprite(a.Sprite) is not { } sheet) continue;
            var meta = assets.SpriteMeta(a.Sprite);
            var anim = meta.Animations.FirstOrDefault(x => x.Name == a.Animation);
            var animated = anim != null && anim.Frames.Where(f => f < sheet.FrameCount).Distinct().Count() > 1;
            var moved = live.Actors[i].X != a.X || live.Actors[i].Y != a.Y || !rom.Actors.Any(x => x.Name == a.Name);
            if (!animated && !moved) continue;
            masked.Add(i);
            Mask(a.X - camX - (sbyte)meta.OriginX, a.Y - camY - (sbyte)meta.OriginY, sheet.FrameWidth, sheet.FrameHeight);
            // the frame on screen shows the actor one game frame before the position read back, so it can be a pixel off
            Mask(live.Actors[i].X - camX - (sbyte)meta.OriginX - 1, live.Actors[i].Y - camY - (sbyte)meta.OriginY - 1, sheet.FrameWidth + 2, sheet.FrameHeight + 2);
        }
        // Text Labels a script writes to (HUD.cs adds the score after "SCORE"): the editor draws the label's own text
        foreach (var a in scene.Actors.Where(a => a.Kind == ActorKind.TextLabel && a.Components.Count > 0)) Mask(a.X, a.Y, 240 - a.X, 16);
        int compared = 0, mismatch = 0;
        var diff = new uint[240 * 160 * 3];
        for (var y = 0; y < 160; y++)
            for (var x = 0; x < 240; x++)
            {
                var i = y * 240 + x;
                var e = rom.Frame[i];
                var eb = (ushort)(((e >> 19) & 31) | (((e >> 11) & 31) << 5) | (((e >> 3) & 31) << 10));
                diff[y * 720 + x] = e | 0xFF000000u;
                diff[y * 720 + 240 + x] = ScreenFrame.ToArgb(frame.Bgr[i]);
                if (mask[i]) { diff[y * 720 + 480 + x] = 0xFF303030u; continue; }
                compared++;
                var same = eb == frame.Bgr[i];
                if (!same) mismatch++;
                diff[y * 720 + 480 + x] = same ? 0xFF000000u : 0xFFFF0000u;
            }
        AgbStudio.Core.Png.WriteArgb(Path.Combine(Project.Value.BuildDir, $"viewport-vs-rom-{name}.png"), diff, 720, 160);
        log.WriteLine($"{name}: {compared} pixels compared, {mismatch} differ, actors masked: {string.Join(", ", masked.Select(i => scene.Actors[i].Name))}");

        // palette RAM: BG banks of the layers and the text layer, OBJ banks of the sprites
        // (banks the scene does not load stay out: the title menu panel of agb::ui sets its own bank when it opens)
        var bgBanks = Enumerable.Range(0, 16).Where(b => scene.Menu == null || frame.BgPalette.Skip(b * 16).Take(16).Any(c => c != 0)).ToHashSet();
        var bgDiffs = Enumerable.Range(1, 255).Where(i => bgBanks.Contains(i / 16) && rom.Palette[i] != frame.BgPalette[i]).ToList();
        var bgDiff = bgDiffs.Count;
        if (bgDiff > 0) log.WriteLine($"{name}: BG palette differs at {string.Join(", ", bgDiffs)}");
        var objDiff = Enumerable.Range(0, 256).Count(i => rom.Palette[256 + i] != frame.ObjPalette[i]);
        log.WriteLine($"{name}: palette entries that differ: BG {bgDiff}, OBJ {objDiff}");
        Assert.Equal(0, bgDiff);
        Assert.Equal(0, objDiff);

        // OAM: every still sprite on screen is where the renderer puts it, with the same palette bank and priority
        foreach (var sp in frame.Sprites.Where(sp => !masked.Contains(sp.Actor)))
            Assert.Contains(rom.Oam, o => o.X == sp.Left && o.Y == sp.Top && o.Palette == sp.PaletteBank && o.Priority == sp.Priority);
        return (compared, mismatch);
    }

    [Fact]
    public void Viewport_DrawsTheSceneLikeTheRom()
    {
        var p = Project.Value;
        var assets = new SceneAssets(p);
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        var map = assets.Map(scene);

        // A: Forest_01 as made; after 4 s the player has landed and the camera follows it at the bottom of the map
        var a = Run(Build(p).RomPath, 240);
        Assert.True(a.Actors.Single(x => x.Name == "Player").OnGround);
        var (comparedA, mismatchA) = Compare(assets, scene, map, a, "forest01");
        Assert.True(comparedA > 240 * 160 * 3 / 4, $"only {comparedA} pixels compared");
        Assert.Equal(0, mismatchA);

        // B: the camera follows the goal flag (still), so the map scrolls in x and y: BG parallax 0.2 / 0.5 / 1, and
        // the text layer, which takes the BG the map leaves free and so does not scroll
        scene.Camera.Follow = "Goal Flag";
        GameProject.Save(scenePath, scene);
        var b = Run(Build(p).RomPath, 120);
        var (comparedB, mismatchB) = Compare(assets, scene, map, b, "forest01-flag");
        Assert.True(comparedB > 240 * 160 * 3 / 4, $"only {comparedB} pixels compared");
        Assert.Equal(0, mismatchB);

        // C: the Title scene: its Text Label "ui.title" in the start language (Thai, thai_vwf) on the text layer, which
        // has BG0 to itself here. The title menu panel (agb::ui, opened when the scene has entered) is not part of the
        // viewport and is left out of the comparison.
        p.Settings.General.StartScene = "Title";
        p.SaveSettings();
        var title = GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, "Title.scene"));
        var c = Run(Build(p).RomPath, 120);
        var menu = title.Menu!;
        var (comparedC, mismatchC) = Compare(assets, title, assets.Map(title), c, "title", (menu.X / 8 * 8 - 8, menu.Y / 8 * 8 - 8, menu.Width + 24, 160));
        var label = GbaScreenRenderer.Render(assets, title, assets.Map(title), 0, 0);
        Assert.Equal(0, label.TextLayer);
        Assert.Contains(label.Source.Take(240 * 72), s => s == 0);   // label pixels from the text layer are compared
        Assert.True(comparedC > 240 * 160 / 2, $"only {comparedC} pixels compared");
        Assert.Equal(0, mismatchC);
    }
}
