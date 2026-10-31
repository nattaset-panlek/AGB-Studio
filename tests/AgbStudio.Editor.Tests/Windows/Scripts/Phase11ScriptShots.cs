using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.LogicGraph;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Editor.Windows.WorldMap;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Scripts;

/// <summary>
/// SHOT renders of the Logic Graph, World Map, Script Editor and Scripting API group (T-P04, T-D14, T-O11, T-P18, T-P17,
/// T-O08, T-M21, T-P13, T-O09, T-M22) with the Sky Hopper fixture in the state the Figma frames show.
/// </summary>
public sealed class Phase11ScriptShots : IShotSource
{
    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P04", (path, w, h) => ScriptEditor(path, w, h));
        yield return ("T-D14", (path, w, h) =>
        {
            using var fx = new Fixture();
            SkyHopperProject.Open(fx);
            var dlg = new NewScriptDialog(fx.Services, "Bat · Cave_01");
            var host = Capture.Host(dlg, w, h, Brushes.Black);
            dlg.ClassName.GetVisualDescendants().OfType<TextBox>().First().Focus();
            Capture.Save(Capture.Frame(host), path);
            host.Close();
        });
        yield return ("T-O11", (path, w, h) =>
        {
            using var fx = new Fixture();
            SkyHopperProject.Open(fx);
            var page = new ScriptEditorPage(fx.Services, SkyHopperProject.Script(fx, "PlayerController"));
            var find = new FindReplacePopover(new FindContext(page, "jumpPower", false), fx.Services);
            find.Replacement.Text = "jumpForce";
            find.SetOptions(matchCase: true, wholeWord: false, regex: false);
            find.Step(1);
            var host = Capture.Host(find, w, h, Brushes.Black);
            find.Query.Focus();
            find.Query.CaretIndex = find.Query.Text!.Length;
            Capture.Save(Capture.Frame(host), path);
            host.Close();
        });
        yield return ("T-P18", (path, w, h) => Page(path, w, h, "T-P18", "Input", null));
        yield return ("T-P17", (path, w, h) => LogicGraph(path, w, h));
        yield return ("T-O08", (path, w, h) =>
        {
            var add = new AddNodePopover(null, "veloc");
            var host = Capture.Host(add, w, h, Brushes.Black);
            add.Query.Focus();
            add.Query.CaretIndex = 5;
            Capture.Save(Capture.Frame(host), path);
            host.Close();
        });
        yield return ("T-M21", (path, w, h) =>
        {
            using var fx = new Fixture(withApp: false);
            Capture.ControlToPng(PageChrome.MenuSurfaceOf(fx.Services, LogicGraphPage.NodeContextMenu()), w, h, path, Brushes.Black);
        });
        yield return ("T-P13", (path, w, h) => WorldMap(path, w, h));
        yield return ("T-O09", (path, w, h) =>
        {
            using var fx = new Fixture();
            SkyHopperProject.Open(fx);
            var model = new WorldMapModel(fx.Services);
            Capture.ControlToPng(new LinkSettingsPopover(new LinkSettingsArgs(model, new LinkRef("Forest_01", 0))), w, h, path, Brushes.Black);
        });
        yield return ("T-M22", (path, w, h) =>
        {
            using var fx = new Fixture(withApp: false);
            Capture.ControlToPng(PageChrome.MenuSurfaceOf(fx.Services, WorldMapPage.SceneContextMenu(() => true)), w, h, path, Brushes.Black);
        });
    }

    /// <summary>Waits for a task while running the UI dispatcher (headless tests).</summary>
    public static T Wait<T>(Task<T> t)
    {
        Wait((Task)t);
        return t.Result;
    }

    public static void Wait(Task t)
    {
        var until = DateTime.UtcNow.AddSeconds(60);
        while (!t.IsCompleted && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
        if (!t.IsCompleted) throw new TimeoutException("task did not finish");
        t.GetAwaiter().GetResult();
    }

    /// <summary>Closes a SHOT window without the Unsaved Changes dialog (a dirty document would keep it open).</summary>
    static void Close(Fixture fx, MainWindow win)
    {
        foreach (var d in fx.Services.Documents.Open) d.IsDirty = false;
        win.Close();
    }

    static MainWindow Window(Fixture fx, int w, int h)
    {
        var win = new MainWindow(fx.Services, startPage: null) { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        win.Show();
        Capture.Pump();
        return win;
    }

    static void Page(string path, int w, int h, string id, object? args, Action<Fixture>? before)
    {
        using var fx = new Fixture();
        SkyHopperProject.Open(fx);
        before?.Invoke(fx);
        var win = Window(fx, w, h);
        try
        {
            Wait(fx.Services.Windows.Open(id, args, source: "SHOT"));
            Capture.UndoHistory(fx.Services);
            Capture.Save(Capture.Frame(win), path);
        }
        finally { Close(fx, win); }
    }

    static void ScriptEditor(string path, int w, int h)
    {
        using var fx = new Fixture();
        SkyHopperProject.Open(fx);
        SkyHopperProject.FigmaDocuments(fx);
        fx.Services.Toolchain.CheckNow();
        var ws = ScriptWorkspace.Of(fx.Services);
        ws.Clock = () => new DateTime(2026, 10, 31, 12, 6, 2);
        var win = Window(fx, w, h);
        try
        {
            Wait(fx.Services.Windows.Open("T-P04", SkyHopperProject.Script(fx, "PlayerController"), source: "SHOT"));
            var page = (ScriptEditorPage)win.CurrentPage!;
            // unsaved edits, as the dot on the tab and "M" in the Scripts list show
            ws.SetText(page.Buffer!, page.Buffer!.Text);
            page.Buffer.SavedText = "";
            page.Buffer.Document.IsDirty = true;
            page.AnalyzeNow();
            // the session log of the Figma state: script check, build and a hot reload
            ws.Log(ConsoleLevel.Info, "C# → C++  12 scripts translated · 0.21 s", check: true);
            ws.Log(ConsoleLevel.Info, "g++ devkitARM 16.1  -O2 -mthumb  · 0.34 s", check: true);
            ws.Clock = () => new DateTime(2026, 10, 31, 12, 6, 3);
            ws.Log(ConsoleLevel.Success, "Hot reload: PlayerController updated in the running game", check: true);
            ws.Clock = () => new DateTime(2026, 10, 31, 12, 6, 2);
            Capture.Pump();
            page.Editor.MoveCaret(8, 5);
            var issue = page.Editor.Issues.First(i => i.Id == "GAE002");
            Wait(page.ShowPopupAsync(issue));
            page.Editor.Focus();
            Capture.UndoHistory(fx.Services);
            Capture.Save(Capture.Frame(win), path);
        }
        finally { Close(fx, win); }
    }

    /// <summary>The Figma Player · Logic graph: ten nodes at the Figma positions (canvas origin 248, 80), nine wires, "Collect coin"
    /// (On Touch with Tag "Coin").</summary>
    public static string FigmaGraph(Fixture fx)
    {
        var path = Path.Combine(fx.Services.Project.Current!.ScriptsDir, "PlayerLogic.graph");
        File.WriteAllText(path, """
            {
              "format": 1,
              "class": "PlayerLogic",
              "nodes": [
                { "id": "n1", "type": "OnUpdate", "x": 40, "y": 60 },
                { "id": "n2", "type": "ButtonPressed", "x": 40, "y": 156, "props": { "button": "A" } },
                { "id": "n3", "type": "IsOnGround", "x": 40, "y": 276 },
                { "id": "n4", "type": "And", "x": 262, "y": 204 },
                { "id": "n5", "type": "Branch", "x": 420, "y": 70 },
                { "id": "n6", "type": "SetVelocityY", "x": 651, "y": 57, "props": { "value": -3.50 } },
                { "id": "n7", "type": "PlaySound", "x": 651, "y": 196, "props": { "sound": "jump.wav", "volume": 0.8 } },
                { "id": "n8", "type": "OnTouch", "x": 48, "y": 420, "props": { "tag": "Coin" } },
                { "id": "n9", "type": "AddToVariable", "x": 318, "y": 410, "props": { "variable": "score", "amount": 10 } },
                { "id": "n10", "type": "Destroy", "x": 610, "y": 436 }
              ],
              "links": [
                { "from": "n1.out", "to": "n5.in" },
                { "from": "n2.value", "to": "n4.a" },
                { "from": "n3.value", "to": "n4.b" },
                { "from": "n4.value", "to": "n5.condition" },
                { "from": "n5.true", "to": "n6.in" },
                { "from": "n6.out", "to": "n7.in" },
                { "from": "n8.out", "to": "n9.in" },
                { "from": "n9.out", "to": "n10.in" },
                { "from": "n8.other", "to": "n10.actor" }
              ],
              "comments": [
                { "title": "Collect coin", "x": 24, "y": 371, "w": 805, "h": 235 }
              ]
            }
            """);
        return path;
    }

    static void LogicGraph(string path, int w, int h)
    {
        using var fx = new Fixture();
        SkyHopperProject.Open(fx);
        var graph = FigmaGraph(fx);
        var win = Window(fx, w, h);
        try
        {
            Wait(fx.Services.Windows.Open("T-P17", graph, source: "SHOT"));
            var page = (LogicGraphPage)win.CurrentPage!;
            page.Canvas.SetView(1, default);
            page.Canvas.Select(["n6"]);
            Capture.UndoHistory(fx.Services);
            Capture.Save(Capture.Frame(win), path);
        }
        finally { Close(fx, win); }
    }

    /// <summary>The scenes at the Figma World Map positions and the links the frame draws (Forest_01: three links, Cave_01 to Summit when 3 stars).</summary>
    public static void FigmaWorld(Fixture fx)
    {
        (string Name, int X, int Y)[] pos = [("Title", 40, 70), ("Forest_01", 270, 300), ("Forest_02", 620, 60), ("Cave_01", 620, 330), ("Summit", 620, 600)];
        foreach (var (name, x, y) in pos)
        {
            var s = SkyHopperProject.Scene(fx, name);
            s.MapX = x;
            s.MapY = y;
            switch (name)
            {
                case "Forest_01":
                    var exit = s.Actors.First(a => a.Name.StartsWith("Exit", StringComparison.Ordinal));
                    exit.Name = "Exit trigger";
                    s.Actors.Add(new ActorData { Name = "Fall in pit", Kind = ActorKind.TriggerZone, X = 176, Y = 330, ZoneW = 32, ZoneH = 16 });
                    s.Links[1].From = "Exit trigger";
                    s.Links.Add(new LinkData { From = "Fall in pit", Kind = LinkKind.Respawn, To = "Forest_01", SpawnX = 40, SpawnY = 112, Transition = Transition.Cut });
                    break;
                case "Forest_02":
                    s.Links.RemoveAll(l => l.To == "Summit");
                    break;
                case "Cave_01":
                    s.Links.Insert(0, new LinkData { From = "", Kind = LinkKind.OnTouch, To = "Summit", OnlyIf = "Game.stars >= 3", Transition = Transition.Fade });
                    break;
                case "Summit":
                    s.Links.Clear();
                    break;
            }
            SkyHopperProject.SaveScene(fx, s);
        }
    }

    static void WorldMap(string path, int w, int h)
    {
        using var fx = new Fixture();
        SkyHopperProject.Open(fx);
        FigmaWorld(fx);
        var win = Window(fx, w, h);
        try
        {
            Wait(fx.Services.Windows.Open("T-P13", "Forest_01", source: "SHOT"));
            var page = (WorldMapPage)win.CurrentPage!;
            page.Canvas.SetView(1, default);
            Capture.UndoHistory(fx.Services);
            Capture.Save(Capture.Frame(win), path);
        }
        finally { Close(fx, win); }
    }
}
