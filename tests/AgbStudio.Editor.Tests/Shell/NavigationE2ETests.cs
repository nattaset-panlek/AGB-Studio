using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Play;
using AgbStudio.Editor.Windows.Debugger;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Editor.Windows.SceneEditor;
using AgbStudio.Editor.Windows.Scripts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Shell;

/// <summary>
/// Navigation across the whole editor (owner report 2026-11-01: switching tabs or tools made panels show or disappear,
/// Play hijacked other pages, the Scripting API page had no way back). Visits every top-level page, there and back and
/// in several orders, with and without a running game, and checks after every step that the shown page, its Doc Tabs,
/// the breadcrumb, the toolbar's play controls, the Scene Editor's tool / panel / selection and the Play state agree.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class NavigationE2ETests : EditorDriver
{
    /// <summary>Every top-level page and the command a user reaches it with (null: a Doc Tab or a link inside a page).</summary>
    static readonly (string Id, string? Command)[] Pages =
    [
        ("T-P03", null), ("T-P04", null), ("T-P07", "tools.database"), ("T-P08", null), ("T-P09", "tools.fontManager"),
        ("T-P10", "tools.paletteManager"), ("T-P11", "build.report"), ("T-P13", "scene.worldMap"), ("T-P14", "tools.localization"),
        ("T-P15", "tools.audio"), ("T-P16", "tools.dialogueEditor"), ("T-P17", "tools.logicGraph"), ("T-P18", "help.scriptingApi"),
        ("T-P19", "tools.debugger"), ("T-P23", null), ("T-P24", null), ("T-P25", null), ("T-P12", "help.gettingStarted"),
    ];

    static bool IsLauncher(string id) => id is "T-P12" or "T-P20" or "T-P21";

    SceneWorkspace? _scene;
    SceneItem? _selection;

    void Go(string id, string? command)
    {
        // Tools › Debugger starts the game when nothing runs (a build): without a game the page is opened directly
        if (command != null && !(command == "tools.debugger" && !Play.IsRunning)) Fx.Services.Commands.Execute(command);
        else _ = Fx.Services.Windows.Open(id, source: "nav");
        Capture.Pump();
    }

    IEnumerable<T> Visible<T>() where T : Control => Win.CurrentPage!.GetVisualDescendants().OfType<T>().Where(c => c.IsEffectivelyVisible);

    /// <summary>The checks after every step.</summary>
    void Check(string page, bool running, string step)
    {
        Capture.Pump();
        string Where() => $"{step}: page {Win.CurrentPageId}";
        Assert.True(page == Win.CurrentPageId, $"{Where()}, expected {page}");
        Assert.Equal(page, Fx.Services.Windows.CurrentPage);
        Assert.True(Win.CurrentPage!.GetVisualRoot() == Win, $"{Where()}: the page {Win.CurrentPage!.GetType().Name} is not in the window (parent {Win.CurrentPage!.Parent?.GetType().Name}, visual parent {Win.CurrentPage.GetVisualParent()?.GetType().Name}, requests {string.Join(" > ", Fx.Services.Windows.Requests.TakeLast(4))})");
        Assert.False(Win.Overlays.IsOpen, Where() + ": overlays left open: " + string.Join(", ", Win.Overlays.Open.Select(o => o.Name)));

        // documents: never a document that is not open; Game only in Play Mode; the Scene Editor edits its scene
        var docs = Fx.Services.Documents;
        Assert.True(docs.Active is null || docs.Open.Contains(docs.Active), $"{Where()}: active document {docs.Active?.Name} is not open");
        if (page != "T-P02") Assert.False(docs.Active?.IsGame == true, Where() + ": the Game document is active outside Play Mode");
        if (page == "T-P01") Assert.True(EditorDocument.IsSceneDocument(docs.Active), $"{Where()}: Scene Editor with {docs.Active?.Name} active");

        // Doc Tabs: one selected at most, Game selected only in Play Mode, and a tab back unless the page is the Launcher
        var tabs = Visible<DocTab>().ToList();
        Assert.True(tabs.Count(t => t.IsSelected) <= 1, $"{Where()}: selected tabs {string.Join(", ", tabs.Where(t => t.IsSelected).Select(t => t.Name))}");
        Assert.True((page == "T-P02") == tabs.Any(t => t.IsSelected && t.Name == "Doc_Game"), Where() + ": Game tab selection");
        if (IsLauncher(page))
        {
            Assert.False(Win.TitleBar.IsVisible, Where());
            Assert.True(Win.CurrentPage!.GetVisualDescendants().OfType<AgbButton>().Single(b => b.Name == "LauncherBack").IsEffectivelyVisible, Where() + ": no way back to the project");
        }
        else
        {
            Assert.True(Win.TitleBar.IsVisible, Where() + ": title bar hidden");
            Assert.False(string.IsNullOrEmpty(Win.TitleBar.Document), Where() + ": empty breadcrumb");
            Assert.True(tabs.Any(t => !t.IsSelected), Where() + ": no Doc Tab leads anywhere else (dead end)");
            // one toolbar and one status bar: a page never shows another page's frame
            Assert.Single(Visible<Border>(), b => b.Name == "Toolbar");
            Assert.Single(Visible<Border>(), b => b.Name == "StatusBar");
        }

        // the toolbar's Pause / Step act on a running game only, on every page
        foreach (var c in Visible<PlayControl>().Where(c => c.Name is "PauseButton" or "StepButton"))
            Assert.True(running == c.IsEffectivelyEnabled, $"{Where()}: {c.Name} enabled {c.IsEffectivelyEnabled}, game running {running}");
        // navigating never starts, stops or restarts the game
        Assert.True(running == Play.IsRunning, $"{Where()}: game running {Play.IsRunning}, expected {running}");

        // the Scene Editor keeps its tool, bottom panel tab and selection
        if (page == "T-P01")
        {
            var ws = Assert.IsType<SceneWorkspace>(Win.CurrentPage);
            if (_scene != null)
            {
                Assert.Same(_scene, ws);
                Assert.Equal(1, ws.ActiveTool);
                Assert.Equal("Tab_Console", ws.SelectedPanel?.Name);
                Assert.Equal(_selection, SceneEditorModel.Of(Fx.Services).Selection);
            }
        }
    }

    void ClickTab(string name)
    {
        var tab = Visible<DocTab>().FirstOrDefault(t => t.Name == name) ?? throw new Xunit.Sdk.XunitException($"{Win.CurrentPageId} has no {name} tab");
        Click(tab);
    }

    /// <summary>Back to the Scene Editor the way a user would: the Scene tab, else the Launcher's Back button or the
    /// page's other tab (SFX Designer > Audio), each step checked.</summary>
    void BackToScene(bool running, string step, int depth = 0)
    {
        Assert.True(depth < 3, $"{step}: no way back to the Scene Editor from {Win.CurrentPageId}");
        if (IsLauncher(Win.CurrentPageId!))
            Click(Win.CurrentPage!.GetVisualDescendants().OfType<AgbButton>().Single(b => b.Name == "LauncherBack"));
        else if (Visible<DocTab>().Any(t => t.Name == "Doc_Scene"))
            ClickTab("Doc_Scene");
        else
            Click(Visible<DocTab>().First(t => !t.IsSelected && t.Name != "Doc_Game"));
        Check(Win.CurrentPageId!, running, step + " > " + Win.CurrentPageId);
        if (Win.CurrentPageId != "T-P01") BackToScene(running, step, depth + 1);
    }

    void PrepareSceneEditor()
    {
        _ = Fx.Services.Windows.Open("T-P01", source: "nav");
        Capture.Pump();
        var ws = Assert.IsType<SceneWorkspace>(Win.CurrentPage);
        Click(Find<IconButton>("Tool_move"));
        Click(ws.PanelTabs[1]);
        SceneEditorModel.Of(Fx.Services).SelectActor(0);
        _selection = SceneEditorModel.Of(Fx.Services).Selection;
        Assert.NotNull(_selection);
        _scene = ws;
        Check("T-P01", false, "start");
    }

    [AvaloniaFact]
    public void EveryPage_ThereAndBack_WithoutAndWithARunningGame_InSeveralOrders()
    {
        PrepareSceneEditor();

        // 1. every page from the Scene Editor and back, nothing running
        foreach (var (id, command) in Pages)
        {
            Go(id, command);
            Check(id, false, "open " + id);
            BackToScene(false, "back from " + id);
        }

        // 2. the game runs: Play Mode through the Game tab, every page from Play Mode and back to Play Mode
        PlayTestSupport.RunForest(Play);
        Check("T-P01", true, "game started");
        ClickTab("Doc_Game");
        Check("T-P02", true, "Game tab");
        foreach (var (id, command) in Pages)
        {
            Go(id, command);
            Check(id, true, "open " + id + " while playing");
            BackToScene(true, "back from " + id + " while playing");
            ClickTab("Doc_Game");
            Check("T-P02", true, "Game tab after " + id);
        }

        // 3. page to page in three orders (forward, backward, shuffled), with Play Mode and the Scene Editor between
        var orders = new List<(string Id, string? Command)[]>
        {
            Pages, Pages.Reverse().ToArray(), Pages.OrderBy(p => int.Parse(p.Id[3..], System.Globalization.CultureInfo.InvariantCulture) * 7 % 19).ToArray(),
        };
        foreach (var order in orders)
        {
            foreach (var (id, command) in order)
            {
                if (IsLauncher(id)) continue;   // the Launcher hides the title bar: covered in 1 and 2
                Go(id, command);
                Check(id, true, "page to page: " + id);
            }
            BackToScene(true, "after an order");
            Go("T-P02", null);
            Check("T-P02", true, "Play Mode between orders");
            BackToScene(true, "Scene Editor between orders");
        }

        // 4. Stop from a page that is not Play Mode stays there; Stop in Play Mode returns to the Scene Editor
        Go("T-P10", "tools.paletteManager");
        Fx.Services.Commands.Execute("play.stop");
        Check("T-P10", false, "Stop from the Palette Manager");
        PlayTestSupport.RunForest(Play);
        BackToScene(true, "after restart");
        ClickTab("Doc_Game");
        Check("T-P02", true, "Game tab after restart");
        Click(Assert.IsType<PlayModePage>(Win.CurrentPage).StopButton);
        Check("T-P01", false, "Stop in Play Mode");
    }

    [AvaloniaFact]
    public void ScriptingApi_HasDocTabsBack_ToTheScriptAndTheScene_AndNoGhostDocument()
    {
        _ = Fx.Services.Windows.Open("T-P04", source: "nav");
        Capture.Pump();
        Assert.IsType<ScriptEditorPage>(Win.CurrentPage);
        var docsBefore = Fx.Services.Documents.Open.ToList();
        Click(Find<AgbButton>("OpenScriptingApi"));
        Check("T-P18", false, "Open Scripting API");
        Assert.Equal("Scripting API", Win.TitleBar.Document);
        Assert.Equal(docsBefore, Fx.Services.Documents.Open);
        var selected = Assert.Single(Visible<DocTab>(), t => t.IsSelected);
        Assert.Equal("Scripting API", selected.Label);
        // back to the script, then again to the scene
        ClickTab("Doc_PlayerController.cs");
        Check("T-P04", false, "back to the script");
        Fx.Services.Commands.Execute("help.scriptingApi");
        Check("T-P18", false, "F1 again");
        ClickTab("Doc_Scene");
        Check("T-P01", false, "back to the scene");
    }

    [AvaloniaFact]
    public void OpeningAnotherProject_FromTheLauncher_StopsTheOldProjectsGame()
    {
        PlayTestSupport.RunForest(Play);
        Fx.Services.Commands.Execute("help.gettingStarted");
        Check("T-P12", true, "Getting Started while playing");
        AgbStudio.Editor.Windows.Launcher.LauncherService.Of(Fx.Services).CompleteOpen(Path.Combine(Fx.Folder, "SkyHopper"), "Forest_01");
        Check("T-P01", false, "another project opened");
    }

    [AvaloniaFact]
    public void ProjectWithoutAScene_DoesNotMakeTheGameTabActive()
    {
        // a project opened without a last scene (a new empty project) only had the Game document: it became the active
        // document, and every page showed Game selected, as if stuck in Play
        var docs = Fx.Services.Documents;
        foreach (var d in docs.Open.ToList()) docs.Close(d);
        docs.Add(new EditorDocument { Name = EditorDocument.GameName, Glyph = "pad" });
        Assert.Null(docs.Active);
        _ = Fx.Services.Windows.Open("T-P10", source: "nav");
        Capture.Pump();
        Assert.DoesNotContain(Visible<DocTab>(), t => t.IsSelected && t.Name == "Doc_Game");
    }

    [AvaloniaFact]
    public void FrameCounters_HaveAFixedWidthAndTabularDigits_SoThePlayControlsStayStill()
    {
        var page = OpenPlayMode();
        Capture.Pump();
        double StopX() => page.StopButton.TranslatePoint(default, Win)!.Value.X;
        var x = StopX();
        var width = page.FrameChip.Bounds.Width;
        Assert.Equal(128, width);
        var label = page.FrameChip.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Contains(label.FontFeatures!, f => f.Tag == "tnum");
        foreach (var frames in new[] { 1, 7, 93, 911 })
        {
            Play.RunFrames(frames);
            page.UpdateChrome();
            Capture.Pump();
            Assert.Equal(x, StopX());
            Assert.Equal(width, page.FrameChip.Bounds.Width);
        }
        // the Debugger's "Paused at frame N" chip keeps its slot when it shows and hides
        _ = Fx.Services.Windows.Open("T-P19", source: "nav");
        Capture.Pump();
        var dbg = Assert.IsType<DebuggerPage>(Win.CurrentPage);
        var stop = dbg.GetVisualDescendants().OfType<PlayControl>().First(c => c.Name == "StopButton");
        var dx = stop.TranslatePoint(default, Win)!.Value.X;
        Click(dbg.PauseButton);
        Assert.True(Play.IsPaused);
        Assert.True(dbg.PausedChip.IsVisible);
        Assert.Equal(dx, stop.TranslatePoint(default, Win)!.Value.X);
        Play.StepFrame();
        dbg.Refresh();
        Capture.Pump();
        Assert.Equal(dx, stop.TranslatePoint(default, Win)!.Value.X);
    }
}
