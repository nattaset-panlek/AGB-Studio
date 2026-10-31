using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Play;
using AgbStudio.Editor.Windows.Debugger;
using AgbStudio.Editor.Windows.Scripts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Debugger;

/// <summary>
/// T-P19 / P13.06: breakpoints are added by the user (Script Editor gutter, F9, Debugger › Breakpoints) and stop the
/// game; the OAM sprites, Profiler and Breakpoints views show their own data (P13.01, P13.03).
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class BreakpointUiTests : EditorDriver
{
    DebuggerModel Model => DebuggerModel.Of(Fx.Services);

    ScriptEditorPage OpenScript(string name)
    {
        var path = Directory.EnumerateFiles(Fx.Services.Project.Current!.ScriptsDir, name, SearchOption.AllDirectories).First();
        _ = Fx.Services.Windows.Open("T-P04", path, source: "test");
        Capture.Pump();
        return Assert.IsType<ScriptEditorPage>(Win.CurrentPage);
    }

    /// <summary>Clicks the line number of a line in the code editor's gutter.</summary>
    void ClickGutter(CodeEditor ed, int line)
    {
        var local = new Point(20, ed.PointOf(ed.Text.Split('\n').Take(line - 1).Sum(l => l.Length + 1)).Y + ed.LineHeight / 2);
        var p = ed.TranslatePoint(local, Win)!.Value;
        Win.MouseMove(p);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    [AvaloniaFact]
    public void Breakpoints_FromTheScriptEditorGutterF9AndTheBreakpointsView_StopTheGame()
    {
        // Script Editor: a click on line number 19 sets a breakpoint, a second click removes it, F9 sets one at the caret
        var script = OpenScript("PlayerController.cs");
        var ed = script.Editor;
        ClickGutter(ed, 19);
        Assert.Equal(["PlayerController.cs:19"], Model.Breakpoints.Select(b => b.Label));
        Assert.True(ed.HasBreakpoint!(19));
        ClickGutter(ed, 19);
        Assert.DoesNotContain(Model.Breakpoints, b => b.Kind != BreakpointKind.SceneLoad);
        ed.MoveCaret(19, 1);
        ed.Focus();
        Press(PhysicalKey.F9);
        Assert.Equal(["PlayerController.cs:19"], Model.Breakpoints.Select(b => b.Label));

        // the Debugger lists it, with "On scene load" (off) as in Figma
        PlayTestSupport.RunForest(Play);
        Play.RunFrames(150);
        _ = Fx.Services.Windows.Open("T-P19", source: "test");
        Capture.Pump();
        var page = Assert.IsType<DebuggerPage>(Win.CurrentPage);
        Assert.Equal(["PlayerController.cs:19", "On scene load"], page.BreakpointBoxes.Select(b => b.Content));
        Assert.Equal([true, false], page.BreakpointBoxes.Select(b => b.IsChecked == true));

        // Debugger › Breakpoints: the table, adding by "File.cs:line", a wrong entry says how to write it
        Click(Find<ListRow>("View_Breakpoints"));
        Assert.Equal(DebuggerView.Breakpoints, page.View);
        var view = page.BreakpointsCenter;
        Assert.Same(view, Find<ContentControl>("DebuggerCenter").Content);
        Assert.Equal(["C# line | line 20", "Scene load | each scene load"], view.RowTexts);
        var box = view.AddField.GetVisualDescendants().OfType<TextBox>().First();
        box.Focus();
        Win.KeyTextInput("Slime.cs");
        Capture.Pump();
        Click(view.AddButton);
        Assert.Contains("PlayerController.cs:19", view.Error);
        view.AddField.Text = "";
        box.Focus();
        Win.KeyTextInput("Slime.cs:42");
        Press(PhysicalKey.Enter);
        Assert.Null(view.Error);
        Assert.Equal(["PlayerController.cs:19", "Slime.cs:42", "On scene load"], page.BreakpointBoxes.Select(b => b.Content));
        Click(Find<IconButton>("RemoveBreakpoint_Slime.cs:42"));
        Assert.Equal(["PlayerController.cs:19", "On scene load"], page.BreakpointBoxes.Select(b => b.Content));

        // the breakpoint set in the gutter stops the game at the jump
        Play.RunFrames(2);
        Play.KeyDown(Key.Z);
        for (var i = 0; i < 10 && !Model.AtBreakpoint; i++) Play.RunFrames(1);
        Play.KeyUp(Key.Z);
        Capture.Pump();
        Assert.True(Model.AtBreakpoint);
        Assert.Equal("PlayerController.cs:19", Model.PausedAt);
        view.Refresh();
        Assert.Contains("PlayerController.Update() :19", view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        page.BreakpointBoxes[0].IsChecked = false;
        Model.Resume();
        Play.RunFrames(3);
        Assert.False(Model.AtBreakpoint);
    }

    [AvaloniaFact]
    public void OamSpritesAndProfilerViews_ShowTheirOwnData()
    {
        PlayTestSupport.RunForest(Play);
        Play.RunFrames(150);
        _ = Fx.Services.Windows.Open("T-P19", source: "test");
        Capture.Pump();
        var page = Assert.IsType<DebuggerPage>(Win.CurrentPage);
        Play.Publish();
        Capture.Pump();
        // VRAM tiles (Figma T-P19): character blocks above the OAM table
        Assert.True(page.Vram.ShowTiles);
        // OAM sprites: the OAM table alone
        Click(Find<ListRow>("View_OamSprites"));
        Assert.Equal(DebuggerView.OamSprites, page.View);
        Assert.False(page.Vram.ShowTiles);
        Assert.False(Find<Control>("BgTiles").IsEffectivelyVisible);
        Assert.True(Find<StackPanel>("OamRows").IsEffectivelyVisible);
        Assert.NotEmpty(page.Vram.Rows);
        // Profiler: the frame graph large and this frame's parts in ms and percent of the budget
        Click(Find<ListRow>("View_Profiler"));
        Assert.Equal(DebuggerView.Profiler, page.View);
        Assert.Same(page.ProfilerCenter, Find<ContentControl>("DebuggerCenter").Content);
        Play.RunFrames(2);
        page.Refresh();
        Assert.Equal(120, Find<FrameGraph>("ProfilerGraph").Frames.Length);
        Assert.Equal(["Logic", "Render", "Audio", "Idle"], page.ProfilerCenter.RowTexts.Select(r => r.Split(" | ")[0]));
        Assert.All(page.ProfilerCenter.RowTexts, r => Assert.Matches(@"^\w+ \| \d+\.\d ms \| \d+%$", r));
        // back to VRAM tiles shows the blocks again
        Click(Find<ListRow>("View_VramTiles"));
        Assert.True(page.Vram.ShowTiles);
    }
}
