using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Settings;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Editor.Windows.SceneEditor;
using AgbStudio.Editor.Windows.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Play;

/// <summary>
/// The running game seen from the rest of the editor: Pause / Step on every page toolbar, the game's log lines in the
/// Console panel (T-P05, P12.07) and Preferences · Emulator taking effect in Play Mode (T-D09, P12.01).
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class PlayAcrossEditorTests : EditorDriver
{
    // Pause and Step of the 15 pages that only showed them (T-P03 ... T-P25) act on the running game
    [AvaloniaTheory]
    [InlineData("T-P03")]
    [InlineData("T-P04")]
    [InlineData("T-P07")]
    [InlineData("T-P08")]
    [InlineData("T-P09")]
    [InlineData("T-P10")]
    [InlineData("T-P11")]
    [InlineData("T-P13")]
    [InlineData("T-P14")]
    [InlineData("T-P15")]
    [InlineData("T-P16")]
    [InlineData("T-P17")]
    [InlineData("T-P18")]
    [InlineData("T-P22")]
    [InlineData("T-P25")]
    public void PauseAndStep_OnEveryPage_ActOnTheRunningGame(string page)
    {
        // nothing runs yet: the buttons are disabled and their tooltip still names the command
        _ = Fx.Services.Windows.Open(page, source: "test");
        Capture.Pump();
        Assert.Equal(page, Win.CurrentPageId);
        var pause = Find<PlayControl>("PauseButton");
        var step = Find<PlayControl>("StepButton");
        Assert.False(pause.IsEffectivelyEnabled);
        Assert.False(step.IsEffectivelyEnabled);
        Assert.Equal("Pause", Assert.IsType<TipContent>(ToolTip.GetTip(pause)).Text);
        Assert.Equal(new TipContent("Frame step", "F10"), ToolTip.GetTip(step));
        Assert.True(ToolTip.GetShowOnDisabled(pause));

        PlayTestSupport.RunForest(Play);
        _ = Fx.Services.Windows.Open(page, source: "test");
        Capture.Pump();
        pause = Find<PlayControl>("PauseButton");
        step = Find<PlayControl>("StepButton");
        Assert.True(pause.IsEffectivelyEnabled);
        Click(pause);
        Assert.True(Play.IsPaused);
        var f = Play.Session!.Frame;
        Click(step);
        Assert.Equal(f + 1, Play.Session.Frame);
        Click(pause);
        Assert.False(Play.IsPaused);
    }

    // T-P05 / P12.07: the game's own lines (engine warnings, Debug.Log) reach the Console panel of the Scene Editor
    [AvaloniaFact]
    public void GameLogLines_ReachTheConsolePanel()
    {
        var log = ConsoleLog.Of(Fx.Services);
        var before = log.Entries.Count;
        PlayTestSupport.RunForest(Play);
        // the debug channel's lines while the scene loads (real game output, not added by the editor)
        var gameLines = Play.Console.Entries.Where(e => !e.Text.StartsWith("Scene ", StringComparison.Ordinal)).ToList();
        Play.Console.Clock = () => new DateTime(2026, 10, 31, 12, 4, 53);
        // a Debug.Log line as the game prints it on its debug channel
        Play.ReceiveGameLine("Debug.Log: jumpPower = 3.50");
        _ = Fx.Services.Windows.Open("T-P01", source: "test");
        Capture.Pump();
        var ws = Assert.IsType<SceneWorkspace>(Win.CurrentPage);
        Click(ws.PanelTabs[1]);
        var panel = Find<ConsolePanel>("ConsolePanel");
        var shown = Lines(panel);
        foreach (var g in gameLines) Assert.Contains(shown, l => l.Text == g.Text);
        var last = shown.Last();
        Assert.Equal(("12:04:53", "Debug.Log: jumpPower = 3.50", ConsoleLevel.Info), (last.Time, last.Text, last.Level));
        Assert.Equal(log.Entries.Count, ws.ConsoleCount);
        Assert.True(log.Entries.Count > before);
        // a game warning counts in the Warnings filter
        Play.ReceiveGameLine("warning: Slime.cs(42): pool full");
        Capture.Pump();
        panel.Filter = ConsoleFilter.Warnings;
        Assert.Contains(panel.Visible, e => e.Text == "Slime.cs(42): pool full");
    }

    // P14.04 / P12.07: a script saved in an external editor while the game runs is hot-reloaded into the running game,
    // and the new code's Debug.Log line reaches the Console panel
    [AvaloniaFact]
    public void ExternalSave_WhileTheGameRuns_HotReloads_AndItsDebugLogReachesTheConsolePanel()
    {
        OpenPlayMode();
        var ws = AgbStudio.Editor.Windows.Scripts.ScriptWorkspace.Of(Fx.Services);
        var script = Path.Combine(Path.GetDirectoryName(PlayTestSupport.ProjectPath)!, "scripts", "PlayerController.cs");
        var original = File.ReadAllText(script);
        try
        {
            File.WriteAllText(script, original.Replace("void Start() => body = Get<PlatformerBody>();",
                "void Start() { body = Get<PlatformerBody>(); Debug.Log(\"saved in an external editor\"); }"));
            // what the scripts folder watcher reports when VS Code saves the file
            ws.OnExternalSave(script);
            PlayTestSupport.WaitUntil(() => Play.PendingReload != null || Play.Console.Entries.Any(e => e.Text.StartsWith("Hot reload: PlayerController updated", StringComparison.Ordinal)), 300);
            if (Play.PendingReload is { } reload) PlayTestSupport.Wait(reload);
            PlayTestSupport.WaitUntil(() => Play.Console.Entries.Any(e => e.Text == "Hot reload: PlayerController updated in the running game"), 300);
            Play.RunFrames(60);
            Capture.Pump();
            Assert.True(Play.IsRunning);
            Assert.Equal("Forest_01", Play.World.SceneName);
            Assert.Contains(Play.Console.Entries, e => e.Text == "saved in an external editor");
            Assert.Contains(ConsoleLog.Of(Fx.Services).Entries, e => e.Text == "saved in an external editor" && e.Level == ConsoleLevel.Info);
        }
        finally
        {
            File.WriteAllText(script, original);
        }
    }

    // T-D09 / P12.01: Preferences · Emulator saved in the dialog change the running game
    [AvaloniaFact]
    public void EmulatorPreferences_TakeEffectInPlayMode()
    {
        var page = OpenPlayMode();
        Assert.Equal(3, page.Game.Screen.Scale);
        Assert.True(Play.Options.PixelPerfect);
        Assert.NotNull(Play.Audio);
        _ = Fx.Services.Windows.Open("T-D09", source: "test");
        Capture.Pump();
        var d = Assert.IsType<PreferencesDialog>(Win.Overlays.Open[^1]);
        Win.Pick(d, "EmulatorScale", "2× · 480×320");
        Win.Pick(d, "EmulatorFilter", "Smooth");
        Win.Click(d.Find<InspectorField>("EmulatorAudio").GetVisualDescendants().OfType<AgbSwitch>().First());
        Win.Click(d.Find<AgbSwitch>("PauseWhenUnfocused"));
        Win.Type(d.Find<InspectorField>("FastForward").Box(), "Space · 2×");
        Win.Click(d.SaveButton());
        Assert.False(Win.Overlays.IsOpen);
        // the game view is drawn at 2x with smoothing, the audio stops, Space fast-forwards at 2x
        Assert.Equal(2, page.Game.Screen.Scale);
        Assert.Equal(480, page.Game.Screen.ImageRect.Width);
        Assert.False(Play.Options.PixelPerfect);
        Assert.Null(Play.Audio);
        Assert.Equal(2, Play.Session!.FastForward);
        page.Game.Screen.Focus();
        KeyDown(PhysicalKey.Space);
        Assert.True(Play.FastForwarding);
        var before = Play.Session.Frame;
        for (var i = 0; i < 10; i++) Play.Tick(TimeSpan.FromSeconds(1 / AgbStudio.Emu.PlaySession.Fps));
        Assert.InRange(Play.Session.Frame - before, 19, 21);
        KeyUp(PhysicalKey.Space);
        // pause when unfocused is on now
        Play.SetFocused(false);
        Assert.True(Play.IsPaused);
        Play.SetFocused(true);
        // a new Play keeps them (they come from Preferences, not from the defaults)
        PlayTestSupport.RunForest(Play);
        Assert.Null(Play.Audio);
        Assert.Equal(2, Play.Session.FastForward);
        // audio back on: the running game gets its output again
        Fx.Services.Preferences.Update(p => p.EmulatorAudio = true);
        Assert.NotNull(Play.Audio);
    }
}
