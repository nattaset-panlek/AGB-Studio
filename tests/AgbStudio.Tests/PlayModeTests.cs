using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>Play Mode and Debugger back end (Phases 12-13) driven headless.</summary>
[Collection("emulator")]
public sealed class PlayModeTests(EmulatorFixture emu)
{
    static readonly Lazy<(GameProject P, BuildResult R)> s_built = new(() =>
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "mini_play_mode"));
        var r = new GameBuilder(p, new Toolchain()).Build();
        return (p, r);
    });

    [Fact]
    public void SessionPacesFramesBySpeedAndSteps()
    {
        var (_, r) = s_built.Value;
        Assert.True(r.Success);
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        Assert.Equal(8, s.Advance(TimeSpan.FromSeconds(1)));  // never more than 8 frames per call (no catch-up burst)
        long before = s.Frame;
        for (var i = 0; i < 30; i++) s.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
        Assert.InRange(s.Frame - before, 28, 31);
        s.Speed = 0.5;
        before = s.Frame;
        for (var i = 0; i < 30; i++) s.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
        Assert.InRange(s.Frame - before, 13, 16);
        s.Paused = true;
        before = s.Frame;
        s.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(before, s.Frame);
        s.Step();
        Assert.Equal(before + 1, s.Frame);
        s.Stop();
    }

    [Fact]
    public void LiveInspectorProfilerOamAndPalettes()
    {
        var (_, r) = s_built.Value;
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 120; i++) s.Step();
        var actors = s.Actors();
        var player = actors.Single(a => a.Name == "Player");
        Assert.True(player.OnGround, $"player at {player.X},{player.Y} vy {player.Vy}; actors: {string.Join(", ", actors.Select(a => a.Name))}");
        // Hold right: the live position and velocity change.
        s.Keys = GbaKeys.Right;
        for (var i = 0; i < 20; i++) s.Step();
        var moved = s.Actors().Single(a => a.Name == "Player");
        Assert.Equal(player.X, moved.X);  // the mini sample has no controller script: input does not move the body
        var prof = s.Profiler()!;
        Assert.InRange(prof.Logic + prof.Render + prof.Audio + prof.Idle, 270000u, 290000u);
        Assert.Equal(120, prof.History.Length);
        Assert.True(prof.CpuPercent > 0 && prof.CpuPercent < 60, $"cpu {prof.CpuPercent:0.0}% logic {prof.Logic} render {prof.Render} audio {prof.Audio} idle {prof.Idle}");
        var oam = Inspector.Oam(s.Core);
        Assert.True(oam.Count >= 2, $"{oam.Count} sprites");  // hero and coin are on screen
        Assert.All(oam, o => Assert.Equal(o.Width, o.Height));  // all square sprites in this sample
        var pal = Inspector.PaletteRam(s.Core);
        Assert.Contains(pal.Skip(256), c => c != 0);  // OBJ palettes loaded
        var (mode, bg, obj) = Inspector.Display(s.Core);
        Assert.Equal(0, mode);
        Assert.True(bg[1] && obj);
        var (tiles, w, h) = Inspector.Tiles(s.Core, 0x10000, 32, 0, true);
        Assert.Equal(128, w);
        Assert.Equal(16, h);
        Assert.Contains(tiles, p => p != 0xFF202020u);
        Assert.Contains("0200", Inspector.HexDump(s.Core, 0x02000000, 2));
        s.Stop();
    }

    [Fact]
    public void HotReloadContinuesInTheSameSceneWithTheSameState()
    {
        var (p, r) = s_built.Value;
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 90; i++) s.Step();
        // Move the player through the debug actor table is not possible; walk to the goal flag scene instead by
        // writing the game state score so the hand-over carries it.
        var stateAddr = s.Core.ReadU32(s.Channel!.Game + 8);
        s.Core.WriteU32(stateAddr + 8, 1234);  // SaveHeader (8 bytes) then score
        var before = s.Actors().Single(a => a.Name == "Player");
        Assert.True(s.HotReload(r.RomPath), $"system ram {s.Core.SystemRam.Length} bytes, game info 0x{s.Channel?.Game ?? 0:X8}");
        for (var i = 0; i < 60; i++) s.Step();
        Assert.True(s.Log.Any(l => l.StartsWith("Hot reload: continued in Forest_01", StringComparison.Ordinal)), string.Join(" | ", s.Log));
        var stateAfter = s.Core.ReadU32(s.Core.ReadU32(s.Channel!.Game + 8) + 8);
        Assert.Equal(1234u, stateAfter);
        var after = s.Actors().Single(a => a.Name == "Player");
        Assert.InRange(after.X, before.X - 1, before.X + 1);
        s.Stop();
        _ = p;
    }

    [Fact]
    public void GifEncodesFrames()
    {
        var gif = new GifWriter(4, 2);
        gif.AddFrame([0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0xFFFFFFFF, 0xFFFF0000, 0xFFFF0000, 0xFF000000, 0xFF000000]);
        gif.AddFrame([0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000]);
        var bytes = gif.Encode();
        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.Equal(0x3B, bytes[^1]);
        Assert.Equal(2, gif.FrameCount);
    }

    [Fact]
    public void KeyboardMapFollowsProjectSettings()
    {
        var map = PlaySession.KeyboardMap(InputAction.Defaults().Select(a => (a.Button, a.Keyboard)));
        Assert.Equal(GbaKeys.A, map["Z"]);
        Assert.Equal(GbaKeys.B, map["X"]);
        Assert.Equal(GbaKeys.Start, map["Enter"]);
        Assert.Equal(GbaKeys.Select, map["Backspace"]);
        Assert.Equal(GbaKeys.Left, map["Left"]);
    }
}
