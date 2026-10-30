using System.Diagnostics;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Play;

/// <summary>Driving the editor window: find controls, click, press keys (as Shell/ShellE2ETests does).</summary>
public abstract class EditorDriver : IDisposable
{
    protected readonly Fixture Fx = new();
    protected readonly MainWindow Win;
    protected readonly PlayController Play;

    protected EditorDriver()
    {
        Play = PlayTestSupport.Open(Fx);
        Win = new MainWindow(Fx.Services) { Width = 1440, Height = 900 };
        Win.Show();
        Capture.Pump();
    }

    public virtual void Dispose()
    {
        Play.Dispose();
        Win.Close();
        Fx.Dispose();
    }

    protected T Find<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    protected IEnumerable<T> All<T>() where T : Control => Win.GetVisualDescendants().OfType<T>();

    protected void Click(Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Win)!.Value;
        Win.MouseMove(p);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    protected void KeyDown(PhysicalKey k, RawInputModifiers m = RawInputModifiers.None) { Win.KeyPressQwerty(k, m); Capture.Pump(); }
    protected void KeyUp(PhysicalKey k, RawInputModifiers m = RawInputModifiers.None) { Win.KeyReleaseQwerty(k, m); Capture.Pump(); }
    protected void Press(PhysicalKey k, RawInputModifiers m = RawInputModifiers.None) { KeyDown(k, m); KeyUp(k, m); }

    /// <summary>Runs the built ROM in Forest_01 and shows Play Mode.</summary>
    protected PlayModePage OpenPlayMode()
    {
        PlayTestSupport.RunForest(Play);
        Play.RunFrames(120);  // the player falls from its spawn point onto the ground
        _ = Fx.Services.Windows.Open("T-P02", source: "test");
        Capture.Pump();
        return Assert.IsType<PlayModePage>(Win.CurrentPage);
    }

    protected static ConsoleLine[] Lines(Control root) => root.GetVisualDescendants().OfType<ConsoleLine>().ToArray();

    /// <summary>A pixel of the rendered window as 0xAARRGGBB, whatever the frame's byte order.</summary>
    protected uint PixelAt(double x, double y)
    {
        using var fb = Capture.Frame(Win).Lock();
        var v = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(fb.Address + (int)y * fb.RowBytes + (int)x * 4);
        if (fb.Format == Avalonia.Platform.PixelFormat.Rgba8888) v = (v & 0xFF00FF00u) | ((v & 0xFFu) << 16) | ((v >> 16) & 0xFFu);
        return v | 0xFF000000u;
    }
}

/// <summary>Play Mode E2E (P12.01-P12.11) with the Sky Hopper fixture running in the editor's mGBA core.</summary>
public class PlayModeE2ETests : EditorDriver
{
    [AvaloniaFact]
    public void P12_01_GameTab_Core3xPixelPerfect_Audio_PauseWhenUnfocused_FastForwardTab()
    {
        var page = OpenPlayMode();
        // scale 3x, centred, nearest neighbour
        var screen = page.Game.Screen;
        Assert.Equal(new Size(720, 480), screen.ImageRect.Size);
        Assert.Equal(Math.Floor((screen.Bounds.Width - 720) / 2), screen.ImageRect.X);
        Assert.Equal(Avalonia.Media.Imaging.BitmapInterpolationMode.None, RenderOptions.GetBitmapInterpolationMode(screen));
        // the rendered picture is the core's frame, each game pixel a 3x3 block
        var origin = screen.TranslatePoint(screen.ImageRect.TopLeft, Win)!.Value;
        foreach (var (gx, gy) in new[] { (10, 10), (120, 80), (200, 150) })
        {
            if (Play.World.Actors.Any(a => Math.Abs(Math.Floor(a.Actor.X) - Play.World.CameraX - gx) < 12 && Math.Abs(Math.Floor(a.Actor.Y) - Play.World.CameraY - gy) < 20)) continue;
            var expected = Play.Core!.Frame[gy * 240 + gx] | 0xFF000000u;
            for (var dy = 0; dy < 3; dy++)
                for (var dx = 0; dx < 3; dx++)
                    Assert.Equal(expected, PixelAt(origin.X + gx * 3 + dx, origin.Y + gy * 3 + dy));
        }
        // audio: the game's samples reach the output
        var sink = Assert.IsType<CountingSink>(Play.Audio);
        Assert.True(sink.SampleRate >= 32768);
        Assert.True(sink.SamplesWritten > 0);
        // pause when unfocused (Preferences · Emulator)
        Play.Options.PauseWhenUnfocused = true;
        Play.SetFocused(false);
        Assert.True(Play.IsPaused);
        var f = Play.Frame;
        Play.Tick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(f, Play.Frame);
        Play.SetFocused(true);
        Assert.False(Play.IsPaused);
        // Tab fast-forward 4x: one 16.7 ms tick runs 4 frames instead of 1
        page.Game.Screen.Focus();
        var before = Play.Session!.Frame;
        for (var i = 0; i < 10; i++) Play.Tick(TimeSpan.FromSeconds(1 / PlaySession.Fps));
        Assert.InRange(Play.Session.Frame - before, 9, 11);
        KeyDown(PhysicalKey.Tab);
        Assert.True(Play.FastForwarding);
        Assert.Equal("4×", page.SpeedChip.Content);
        before = Play.Session.Frame;
        for (var i = 0; i < 10; i++) Play.Tick(TimeSpan.FromSeconds(1 / PlaySession.Fps));
        Assert.InRange(Play.Session.Frame - before, 39, 41);
        KeyUp(PhysicalKey.Tab);
        Assert.False(Play.FastForwarding);
        // BUD 59.7 fps: frames are paced at 59.73 per second, and the core runs well above that
        Assert.Equal(59.7275, PlaySession.Fps, 3);
        before = Play.Session.Frame;
        for (var i = 0; i < 60; i++) Play.Tick(TimeSpan.FromSeconds(1 / PlaySession.Fps));
        Assert.InRange(Play.Session.Frame - before, 59, 61);
        var sw = Stopwatch.StartNew();
        Play.RunFrames(300);
        Assert.True(300 / sw.Elapsed.TotalSeconds > 59.7, $"{300 / sw.Elapsed.TotalSeconds:0.0} fps");
    }

    [AvaloniaFact]
    public void P12_02_Play_IncrementalBuildAndRun_PlayFromHere_BuildAndRun_RunInMgba()
    {
        Fx.Services.Commands.Execute("build.play");
        Assert.Equal(("T-P02", "build.play"), Fx.Services.Windows.Requests[^1]);
        Assert.IsType<PlayModePage>(Win.CurrentPage);
        PlayTestSupport.WaitUntil(() => Play.IsRunning);
        var first = Play.LastBuild!;
        Assert.True(first.Success);
        Assert.Equal(0, first.AssetsConverted);  // incremental: the fixture was built before, every asset is cached
        Assert.Contains(Play.Console.Entries, e => e.Level == ConsoleLevel.Success && e.Text.StartsWith("Build OK in ", StringComparison.Ordinal));
        PlayTestSupport.RunUntilScene(Play, "Title", 120);  // Play starts at the start scene
        Fx.Services.Commands.Execute("build.playFromHere");
        PlayTestSupport.WaitUntil(() => Play.IsRunning && Play.LastBuild != first, describe: () => $"{Play.State} {Play.LastBuild == first} " + string.Join(" | ", Play.Console.Entries.TakeLast(5).Select(e => e.Text)));
        PlayTestSupport.RunUntilScene(Play, "Forest_01", 200);  // the Scene Editor's scene
        Assert.Equal(PlayStart.FromHere, Play.StartedAs);
        var built = Play.LastBuild;
        Fx.Services.Commands.Execute("build.buildAndRun");
        PlayTestSupport.WaitUntil(() => Play.IsRunning && Play.LastBuild != built);
        Assert.Equal(PlayStart.BuildAndRun, Play.StartedAs);
        // Build Report › Run in mGBA: runs the last ROM without building
        built = Play.LastBuild;
        var builds = 0;
        Fx.Services.Build.Started += () => builds++;
        var open = Fx.Services.Windows.Open("T-P02", PlayStart.RunBuilt, source: "RunInMgba");
        if (open.IsFaulted) throw open.Exception!;
        PlayTestSupport.WaitUntil(() => Play.IsRunning && Play.StartedAs == PlayStart.RunBuilt, describe: () => $"{Play.State} {Play.StartedAs} report {Fx.Services.Build.LoadReport() != null} " + string.Join(" | ", Play.Console.Entries.TakeLast(3).Select(e => e.Text)));
        Assert.Equal(0, builds);
        Assert.Same(built, Play.LastBuild);
        Assert.Equal(PlayTestSupport.Built.RomPath, Play.RomPath);
    }

    [AvaloniaFact]
    public void P12_03_PlayControls_PauseStepStop_StopReturnsToSceneEditor()
    {
        var page = OpenPlayMode();
        Click(page.PauseButton);
        Assert.True(Play.IsPaused);
        Assert.Equal("Paused", page.RunState.Content);
        var f = Play.Session!.Frame;
        Play.Tick(TimeSpan.FromMilliseconds(200));
        Assert.Equal(f, Play.Session.Frame);
        Click(page.StepButton);
        Assert.Equal(f + 1, Play.Session.Frame);
        Click(page.PauseButton);
        Assert.False(Play.IsPaused);
        Assert.Equal("Running", page.RunState.Content);
        Click(page.StopButton);
        Assert.Null(Play.Session);
        Assert.Equal("T-P01", Win.CurrentPageId);
        Assert.Equal(("T-P01", "PlayStop"), Fx.Services.Windows.Requests[^1]);
        // the Scene Editor's Pause / Step are the same commands, disabled while nothing runs
        var ws = Assert.IsType<SceneWorkspace>(Win.CurrentPage);
        Assert.Same(Fx.Services.Commands.Get("play.pause"), Find<PlayControl>("PauseButton").Command);
        Assert.Same(Fx.Services.Commands.Get("play.frameStep"), Find<PlayControl>("StepButton").Command);
        Assert.False(Fx.Services.Commands.Get("play.pause").CanExecute());
        _ = ws;
    }

    [AvaloniaFact]
    public void P12_04_PlaySpeed_QuarterToFourTimes_AndFrameStep()
    {
        var page = OpenPlayMode();
        Assert.Equal("1×", page.SpeedChip.Content);
        Click(page.SpeedChip);
        Assert.Equal(("T-M18", "SpeedChip"), Fx.Services.Windows.Requests[^1]);
        Press(PhysicalKey.Escape);
        var entries = PlayCommands.PlaySpeedMenu(new WindowContext { Services = Fx.Services, Info = Fx.Services.Windows.Info("T-M18") });
        Assert.Equal(["¼×", "½×", "1×", "2×", "4×"], entries.OfType<RecentEntry>().Select(e => e.Name));
        Assert.True(entries.OfType<RecentEntry>().Single(e => e.Name == "1×").IsCurrent);
        Assert.Equal("play.frameStep", Assert.IsType<CommandEntry>(entries[^1]).CommandId);
        Assert.Equal("F10", AgbStudio.Editor.Theme.Shortcuts.Format(Fx.Services.Shortcuts.GestureOf("play.frameStep")!));
        foreach (var (label, speed) in new[] { ("¼×", 0.25), ("½×", 0.5), ("2×", 2.0), ("4×", 4.0), ("1×", 1.0) })
        {
            entries.OfType<RecentEntry>().Single(e => e.Name == label).Open.Execute(null);
            Capture.Pump();
            Assert.Equal(speed, Play.Speed);
            Assert.Equal(label, page.SpeedChip.Content);
            var before = Play.Session!.Frame;
            for (var i = 0; i < 60; i++) Play.Tick(TimeSpan.FromSeconds(1 / PlaySession.Fps));
            Assert.InRange(Play.Session.Frame - before, 60 * speed - 1, 60 * speed + 1);
        }
        // Frame step F10: pauses and advances exactly one frame
        var f = Play.Session!.Frame;
        Press(PhysicalKey.F10);
        Assert.True(Play.IsPaused);
        Assert.Equal(f + 1, Play.Session.Frame);
        Press(PhysicalKey.F10);
        Assert.Equal(f + 2, Play.Session.Frame);
    }

    [AvaloniaFact]
    public void P12_05_Overlays_HitboxesAndTileCollision_DrawnOverTheGame()
    {
        var page = OpenPlayMode();
        Assert.Equal(ChipStyle.Accent, page.HitboxesChip.Style);
        Assert.Equal(ChipStyle.Default, page.CollisionChip.Style);
        var screen = page.Game.Screen;
        var player = Play.World.Find("Player")!;
        var origin = screen.TranslatePoint(screen.ImageRect.TopLeft, Win)!.Value;
        var left = origin.X + (Math.Floor(player.Actor.X) + player.ColliderX - Play.World.CameraX) * 3;
        var top = origin.Y + (Math.Floor(player.Actor.Y) + player.ColliderY - Play.World.CameraY) * 3;
        uint At(double x, double y) => PixelAt(x, y);
        var success = ((ISolidColorBrush)AgbStudio.Editor.Services.ThemeService.Token(Win, "status/success")!).Color.ToUInt32();
        // the player's collider outline, in status/success
        Assert.Equal(success, At(left + 0.2, top + player.ColliderH * 1.5));
        Click(page.HitboxesChip);
        Assert.False(Play.Hitboxes);
        Assert.NotEqual(success, At(left + 0.2, top + player.ColliderH * 1.5));
        // tile collision: the solid ground under the player gets the accent tint
        var groundY = top + player.ColliderH * 3 + 24;
        var plain = At(left + 10, groundY);
        Click(page.CollisionChip);
        Assert.True(Play.TileCollision);
        Assert.Equal(ChipStyle.Accent, page.CollisionChip.Style);
        Assert.NotEqual(plain, At(left + 10, groundY));
    }

    [AvaloniaFact]
    public void P12_06_LiveInspector_StatePositionVelocityOnGroundScriptFields_ChangeWhileRunning()
    {
        var page = OpenPlayMode();
        var pos = Find<InspectorField>("Live_Position");
        var vel = Find<InspectorField>("Live_Velocity");
        var ground = Find<InspectorField>("Live_OnGround");
        Assert.True(ground.IsOn);
        Assert.Equal("1.5", Find<InspectorField>("Live_PlayerController_speed").Value);
        Assert.Equal("3.5", Find<InspectorField>("Live_PlayerController_jumpPower").Value);
        Assert.Equal("3", Find<InspectorField>("Live_PlayerController_lives").Value);
        Assert.Equal("0.25", Find<InspectorField>("Live_gravity").Value);
        var x0 = pos.Value;
        // hold Right (Project Settings · Input: D-pad = Arrows): position and velocity change while running
        page.Game.Screen.Focus();
        KeyDown(PhysicalKey.ArrowRight);
        Play.RunFrames(20);
        Capture.Pump();
        Assert.NotEqual(x0, pos.Value);
        Assert.Equal("1.50", vel.Value);
        Assert.Equal("Player · state Run", page.Game.ActorChip.Content);
        KeyUp(PhysicalKey.ArrowRight);
        // A (keyboard Z) jumps: off the ground, state Jump, velocity up
        KeyDown(PhysicalKey.Z);
        Play.RunFrames(3);
        Capture.Pump();
        KeyUp(PhysicalKey.Z);
        Assert.False(ground.IsOn);
        Assert.StartsWith("-", vel.Value2);
        Assert.Equal("Player · state Jump", page.Game.ActorChip.Content);
        // a live edit goes into the game without a rebuild (D-15)
        var lives = Find<InspectorField>("Live_PlayerController_lives");
        lives.Value = "2";
        var box = lives.GetVisualDescendants().OfType<TextBox>().First(t => t.IsVisible);
        box.Focus();
        Press(PhysicalKey.Enter);
        Play.RunFrames(1);
        var field = Play.World.Find("Player")!.Behaviours.Single(b => b.Type == "PlayerController").Field("lives")!;
        Assert.Equal(2, field.Value);
        // Select: clicking another actor in the game view inspects it
        Play.RunFrames(60);
        var block = Play.World.Find("Question Block")!;
        var screen = page.Game.Screen;
        var r = screen.ImageRect;
        var at = new Point(r.X + (Math.Floor(block.Actor.X) + block.ColliderX + block.ColliderW / 2.0 - Play.World.CameraX) * 3,
            r.Y + (Math.Floor(block.Actor.Y) + block.ColliderY + block.ColliderH / 2.0 - Play.World.CameraY) * 3);
        Assert.Equal(block.Name, screen.ActorAt(at)?.Name);
        var p = screen.TranslatePoint(at, Win)!.Value;
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
        Assert.Equal("Question Block", Play.SelectedActor);
        Assert.Equal("Question Block", Inspector().GetVisualDescendants().OfType<TextBlock>().First().Text);
        Assert.Equal("3", Find<InspectorField>("Live_QuestionBlock_coins").Value);
        LiveInspector Inspector() => page.Inspector;
    }

    [AvaloniaFact]
    public void P12_07_Console_EngineLogsAndDebugLog_WithTimestamps()
    {
        var page = OpenPlayMode();
        var lines = Lines(page);
        Assert.True(lines.Any(l => System.Text.RegularExpressions.Regex.IsMatch(l.Text!, @"^Scene Forest_01 loaded \(BG 3 · OBJ \d+\)$") && l.Level == ConsoleLevel.Info),
            string.Join(" | ", lines.Select(l => l.Text)));
        Assert.Contains(lines, l => l.Level == ConsoleLevel.Warning && l.Text!.Contains("took longer than one frame"));  // engine warning, "warning: " prefix removed
        Assert.All(lines, l => Assert.Matches(@"^\d\d:\d\d:\d\d$", l.Time));
        Assert.Equal(Play.Console.Entries.Count, page.PanelTabs[1].Count);
        // a Debug.Log line from the game appears with the time it arrived
        Play.Console.Clock = () => new DateTime(2026, 10, 31, 12, 4, 53);
        Play.Console.AddGameLine("Debug.Log: jumpPower = 3.50");
        Capture.Pump();
        var last = Lines(page).Last();
        Assert.Equal(("12:04:53", "Debug.Log: jumpPower = 3.50", ConsoleLevel.Info), (last.Time, last.Text, last.Level));
        Assert.Same(Play.Console, Fx.Services.Get<PlayConsole>());
    }

    [AvaloniaFact]
    public void P12_08_StatusBar_FrameFpsVBlankCpu_RunningPaused_HotReload()
    {
        var page = OpenPlayMode();
        var frame = Play.Frame;
        Assert.Equal($"Frame {frame}", page.FrameChip.Content);
        Play.RunFrames(10);
        Capture.Pump();
        Assert.Equal($"Frame {frame + 10}", page.FrameChip.Content);
        Assert.Matches(@"^\d+\.\d fps$", page.Game.FpsChip.Content!.ToString());
        Assert.Matches(@"^vblank \d+\.\d ms free$", page.Game.VBlankChip.Content!.ToString());
        Assert.True(Play.VBlankFreeMs > 0);
        Assert.Matches(@"^\d+\.\d fps · CPU \d+%$", page.Performance.Content!.ToString());
        Assert.InRange(Play.CpuPercent, 1, 99);
        Assert.Equal("Running", page.RunState.Content);
        Assert.Equal("Hot reload on", page.HotReload.Content);
        Assert.True(page.HotReload.IsVisible);
        Play.SetPaused(true);
        Assert.Equal("Paused", page.RunState.Content);
        Fx.Services.Project.Current!.Settings.Scripting.HotReload = false;
        Play.SetPaused(false);
        Assert.False(page.HotReload.IsVisible);
        Fx.Services.Project.Current!.Settings.Scripting.HotReload = true;
    }

    [AvaloniaFact]
    public void P12_09_KeyboardAndGamepad_FromProjectSettingsInput()
    {
        var page = OpenPlayMode();
        page.Game.Screen.Focus();
        // default map: Z = A, X = B, Enter = Start, arrows = D-pad
        KeyDown(PhysicalKey.X);
        Assert.Equal(GbaKeys.B, Play.Keys);
        KeyUp(PhysicalKey.X);
        Assert.Equal(GbaKeys.None, Play.Keys);
        // remapped in Project Settings · Input: A on K
        Fx.Services.ProjectSettings.Settings.Input.Single(a => a.Button == "A").Keyboard = "K";
        KeyDown(PhysicalKey.K);
        Assert.Equal(GbaKeys.A, Play.Keys);
        KeyUp(PhysicalKey.K);
        KeyDown(PhysicalKey.Z);
        Assert.Equal(GbaKeys.None, Play.Keys);
        KeyUp(PhysicalKey.Z);
        // gamepad: South = A, LB = L, stick left = Left (Gamepad column)
        Play.Gamepad = () => new XInputPad(0x1000 | 0x0100, -20000, 0);
        Play.Tick(TimeSpan.FromMilliseconds(16));
        Assert.Equal(GbaKeys.A | GbaKeys.L | GbaKeys.Left, Play.Keys);
        Play.Gamepad = () => new XInputPad(0, 0, 0);
        Play.Tick(TimeSpan.FromMilliseconds(16));
        Assert.Equal(GbaKeys.None, Play.Keys);
    }

    [AvaloniaFact]
    public void P12_10_SaveFile_KeptPerProject_SaveStopPlayLoad()
    {
        OpenPlayMode();
        if (File.Exists(Play.SaveFilePath)) File.Delete(Play.SaveFilePath);
        // the game writes its save into the cartridge SRAM
        var pattern = "AGB SAVE TEST 0123"u8.ToArray();
        pattern.CopyTo(Play.Core!.SaveRam);
        Click(Find<PlayControl>("StopButton"));
        Assert.True(File.Exists(Play.SaveFilePath));
        Assert.StartsWith(Path.Combine(Fx.Services.Project.Current!.BuildDir, "play"), Play.SaveFilePath);
        Assert.Equal(pattern, File.ReadAllBytes(Play.SaveFilePath).Take(pattern.Length));
        // play again: the save is in SRAM before the first frame, so the game reads it from 0x0E000000
        Assert.True(Play.Run(PlayTestSupport.Built.RomPath));
        Assert.Equal(pattern, Play.Core!.SaveRam[..pattern.Length].ToArray());
        Play.RunFrames(60);
        Assert.Equal(pattern, Play.Core.SaveRam[..pattern.Length].ToArray());
        if (Play.Core.Regions.Any(r => r.Start == 0x0E000000)) Assert.Equal(pattern, Play.Core.Memory(0x0E000000, pattern.Length).ToArray());
        Play.Stop();
        File.Delete(Play.SaveFilePath);
    }

    [AvaloniaFact]
    public void P12_11_HotReload_SaveAScript_GameContinuesInTheSameSceneWithTheNewCode()
    {
        var page = OpenPlayMode();
        page.Game.Screen.Focus();
        KeyDown(PhysicalKey.ArrowRight);
        Play.RunFrames(30);
        KeyUp(PhysicalKey.ArrowRight);
        Play.RunFrames(20);
        var before = Play.World.Find("Player")!.Actor;
        var script = Path.Combine(Path.GetDirectoryName(PlayTestSupport.ProjectPath)!, "scripts", "PlayerController.cs");
        var original = File.ReadAllText(script);
        try
        {
            // edit the script (same line count) and save it in the editor
            File.WriteAllText(script, original.Replace("void Start() => body = Get<PlatformerBody>();", "void Start() { body = Get<PlatformerBody>(); Debug.Log(\"PlayerController ready\"); }"));
            var doc = Fx.Services.Documents.Open.Single(d => d.Name == "PlayerController.cs");
            doc.IsDirty = true;
            PlayTestSupport.Wait(Fx.Services.Documents.SaveAsync(doc));
            Assert.NotNull(Play.PendingReload);
            PlayTestSupport.Wait(Play.PendingReload!);
            Play.RunFrames(60);
            Assert.Contains(Play.Console.Entries, e => e.Text == "Hot reload: PlayerController updated in the running game" && e.Level == ConsoleLevel.Success);
            Assert.Contains(Play.Console.Entries, e => e.Text == "Hot reload: continued in Forest_01");
            Assert.Contains(Play.Console.Entries, e => e.Text == "PlayerController ready");  // the new code runs
            Assert.Equal("Forest_01", Play.World.SceneName);
            var after = Play.World.Find("Player")!.Actor;
            Assert.InRange(after.X, before.X - 2, before.X + 2);
            Assert.True(Play.IsRunning);
        }
        finally
        {
            File.WriteAllText(script, original);
        }
    }
}
