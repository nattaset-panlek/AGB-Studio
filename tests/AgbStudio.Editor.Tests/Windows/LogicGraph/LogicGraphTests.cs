using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.Scripts;
using AgbStudio.Editor.Windows.LogicGraph;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Scripting.Graph;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.LogicGraph;

/// <summary>Logic Graph (T-P17, T-O08, T-M21): P11.48 canvas, palette, inspector, Generated C# in sync; P11.49 Add Node and Node Context Menu.</summary>
public sealed class LogicGraphTests : IDisposable
{
    readonly EditorHarness _h = new();
    readonly string _graph;

    public LogicGraphTests() => _graph = Phase11ScriptShots.FigmaGraph(_h.Fx);

    public void Dispose() => _h.Dispose();

    LogicGraphPage Open()
    {
        _h.S.Commands.Execute("tools.logicGraph");
        Capture.Pump();
        var page = _h.Page<LogicGraphPage>();
        page.Canvas.SetView(1, default);
        Capture.Pump();
        return page;
    }

    Point Screen(LogicGraphPage page, double x, double y) => _h.At(page.Canvas, page.Canvas.ToScreen(new Point(x, y)));

    [AvaloniaFact]
    public void ToolsLogicGraph_ShowsNodesWiresPaletteStatusAndGeneratedCode()
    {
        var page = Open();
        Assert.Equal(_graph, page.Document.Path);
        Assert.Equal("Player · Logic", _h.Win.TitleBar.Document);
        Assert.Equal("10 nodes · 9 wires", _h.Find<StatusItem>("Status_Counts").Content);
        Assert.Equal("Compiled to C#", _h.Find<StatusItem>("Status_Compiled").Content);
        // nodes use the Node Header / Node Pin components, pins coloured by type
        var andView = page.Canvas.ViewOf("n4")!;
        Assert.Equal(NodeCategory.Math, andView.GetVisualDescendants().OfType<NodeHeader>().Single().Category);
        Assert.Equal([Controls.PinType.Bool, Controls.PinType.Bool, Controls.PinType.Bool], andView.GetVisualDescendants().OfType<NodePin>().Select(p => p.Type));
        // palette by category; search filters it
        Assert.Contains("Events", page.PaletteLabels);
        Assert.Contains("Set Velocity Y", page.PaletteLabels);
        page.Search.Text = "sound";
        Capture.Pump();
        Assert.Equal(["Sound", "Play Sound"], page.PaletteLabels);
        page.Search.Text = "";
        // Generated C# panel
        Assert.Contains("void Update() {", page.Code.Text);
        Assert.Contains("body.vy = -3.50;", page.Code.Text);
        Assert.Contains("Sound.Play(\"jump\", 0.8);", page.Code.Text);
        Assert.True(page.InSync);
    }

    [AvaloniaFact]
    public void ZoomButtonsWheelAndFit()
    {
        var page = Open();
        _h.Click(_h.Find<AgbButton>("ZoomIn"));
        Assert.Equal(1.25, page.Canvas.Zoom);
        _h.Click(_h.Find<AgbButton>("ZoomOut"));
        _h.Click(_h.Find<AgbButton>("ZoomOut"));
        Assert.Equal(0.75, page.Canvas.Zoom);
        Assert.Contains(_h.Find<Border>("ZoomBar").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "75%");
        _h.Click(_h.Find<AgbButton>("ZoomFit"));
        Assert.InRange(page.Canvas.Zoom, 0.25, 1);
        var p = _h.Center(page.Canvas);
        var z = page.Canvas.Zoom;
        _h.Win.MouseWheel(p, new Vector(0, 1));
        Capture.Pump();
        Assert.True(page.Canvas.Zoom > z);
    }

    [AvaloniaFact]
    public void SelectDragWireInspectorAndUndo()
    {
        var page = Open();
        // click Set Velocity Y: the inspector shows its description and value
        _h.ClickAt(Screen(page, 651 + 60, 57 + 15));
        Assert.Equal(["n6"], page.Canvas.Selection);
        Assert.Equal("Set Velocity Y", _h.Find<SectionHeader>("InspectorHeader").Title);
        Assert.Equal("-3.50", _h.Find<InspectorField>("Prop_value").Value);
        Assert.True(page.SetProp("n6", "value", "-4.25"));
        Assert.Contains("body.vy = -4.25;", page.Code.Text);
        Assert.False(page.SetProp("n6", "value", "fast"));
        _h.S.Undo.Undo();
        Assert.Contains("body.vy = -3.50;", page.Code.Text);
        // drag a node by its header: position saved in the document, one undo step
        _h.DragAt(Screen(page, 651 + 60, 57 + 15), Screen(page, 651 + 100, 57 + 55));
        Assert.Equal((691, 97), ((int)page.Document.Node("n6")!.X, (int)page.Document.Node("n6")!.Y));
        _h.S.Undo.Undo();
        Assert.Equal((651, 57), ((int)page.Document.Node("n6")!.X, (int)page.Document.Node("n6")!.Y));
        // drag from Play Sound's exec out to Destroy's exec in: the chain is rewired
        var from = page.Document.PinPoint("n7", "out", true)!.Value;
        var to = page.Document.PinPoint("n10", "in", false)!.Value;
        _h.DragAt(Screen(page, from.X, from.Y), Screen(page, to.X, to.Y));
        Assert.Contains(page.Document.Graph.Links, l => l.From == "n7.out" && l.To == "n10.in");
        Assert.Contains("other.Destroy();", page.Code.Text);
        Assert.True(_h.S.Documents.Active!.IsDirty);
    }

    [AvaloniaFact]
    public async Task GeneratedCodeEditsUpdateTheGraph_AndSaveWritesGraphAndCs()
    {
        var page = Open();
        page.Code.Text = page.Code.Text.Replace("body.vy = -3.50;", "body.vy = -5.00;");
        Assert.True(page.ApplyCode());
        Assert.Equal("-5.00", page.Document.Node("n6")!.Props["value"]);
        Assert.True(page.InSync);
        page.Code.Text = page.Code.Text.Replace("Sound.Play(\"jump\", 0.8);", "Explode();");
        Assert.False(page.ApplyCode());
        Assert.False(page.InSync);
        Assert.Equal("Graph ⇄ C# not in sync", _h.Find<Chip>("SyncChip").Content);
        await _h.S.Documents.SaveActiveAsync();
        var saved = GraphDocument.Load(_graph);
        Assert.Equal("-5.00", saved.Node("n6")!.Props["value"]);
        Assert.Single(saved.Comments, c => c.Title == "Collect coin");
        Assert.True(GraphCodeGen.IsGenerated(File.ReadAllText(Path.ChangeExtension(_graph, ".cs"))));
    }

    [AvaloniaFact]
    public void RightClickCanvas_AddNodeSearchEnterAndShiftEnter()
    {
        var page = Open();
        _h.ClickAt(Screen(page, 700, 640), MouseButton.Right);
        Assert.Equal("T-O08", _h.S.Windows.Requests[^1].Window);
        var add = Assert.IsType<AddNodePopover>(_h.Win.Overlays.Open.Single());
        add.Query.Focus();
        _h.Type("veloc");
        Assert.All(add.Items, d => Assert.Contains("Velocity", d.Title));
        _h.Key(Key.Down);
        Assert.Equal(1, add.HighlightIndex);
        var chosen = add.Items[1];
        _h.Key(Key.Enter);
        Assert.False(_h.Win.Overlays.IsOpen);
        var added = page.Document.Graph.Nodes.Last();
        Assert.Equal(chosen.Type, added.Type);
        Assert.Equal((700, 640), ((int)added.X, (int)added.Y));
        Assert.Equal("11 nodes · 9 wires", _h.Find<StatusItem>("Status_Counts").Content);
        // Shift+Enter adds and connects from the selected node (Play Sound's exec out)
        page.Canvas.Select(["n7"]);
        _h.ClickAt(Screen(page, 500, 700), MouseButton.Right);
        add = Assert.IsType<AddNodePopover>(_h.Win.Overlays.Open.Single());
        add.Query.Focus();
        _h.Type("checkpoint");
        _h.Key(Key.Enter, RawInputModifiers.Shift);
        var cp = page.Document.Graph.Nodes.Last();
        Assert.Equal("Checkpoint", cp.Type);
        Assert.Contains(page.Document.Graph.Links, l => l.From == "n7.out" && l.To == cp.Id + ".in");
        Assert.Contains("Save.Checkpoint();", page.Code.Text);
    }

    // T-O08: Nodes › Add node… opens the Add Node popover for the middle of the view; typing goes to its search
    [AvaloniaFact]
    public void NodesAddNode_OpensTheAddNodePopover()
    {
        var page = Open();
        _h.Click(page.Search);
        Assert.Equal(("T-O08", "NodesAddNode"), _h.S.Windows.Requests[^1]);
        var add = Assert.IsType<AddNodePopover>(_h.Win.Overlays.Open.Single());
        add.Query.Focus();
        _h.Type("veloc");
        Assert.All(add.Items, d => Assert.Contains("Velocity", d.Title));
        _h.Key(Key.Enter);
        Assert.False(_h.Win.Overlays.IsOpen);
        var added = page.Document.Graph.Nodes.Last();
        Assert.Contains("Velocity", NodeCatalog.Find(added.Type)!.Title);
        // typing into the box opens the popover with the text in its search
        page.Search.Focus();
        _h.Type("sou");
        add = Assert.IsType<AddNodePopover>(_h.Win.Overlays.Open.Single());
        Assert.Equal("sou", add.Query.Text);
        Assert.Contains(add.Items, d => d.Title == "Play Sound");
        _h.Key(Key.Escape);
        Assert.False(_h.Win.Overlays.IsOpen);
    }

    // T-P17 toolbar: the Link tool wires a node to another by dragging between their bodies
    [AvaloniaFact]
    public void LinkTool_DragsAWireFromNodeToNode()
    {
        var page = Open();
        // below and right of the Figma graph, at half zoom so both fit in the view
        page.Canvas.SetView(0.5, default);
        var start = page.AddNode("OnStart", new Point(1150, 1150), null);
        var sound = page.AddNode("PlaySound", new Point(1450, 1150), null);
        Capture.Pump();
        _h.Click(_h.Find<IconButton>("Tool_Link"));
        Assert.Equal(GraphTool.Link, page.Canvas.Tool);
        Point Mid(string id) { var b = page.Document.Bounds(page.Document.Node(id)!); return Screen(page, b.Center.X, b.Center.Y); }
        var wires = page.Document.AllLinks.Count();
        _h.DragAt(Mid(start), Mid(sound));
        Assert.Equal(wires + 1, page.Document.AllLinks.Count());
        Assert.Contains(page.Document.Graph.Links, l => l.FromNode == start && l.ToNode == sound && l.ToPin == "in");
        Assert.Contains("void Start()", page.Code.Text);
        _h.S.Commands.Execute("edit.undo");
        Assert.Equal(wires, page.Document.AllLinks.Count());
        // dropping on empty space opens Add Node to connect from that node
        _h.DragAt(Mid(start), Screen(page, 1200, 1450));
        Assert.Equal("T-O08", _h.S.Windows.Requests[^1].Window);
        _h.Key(Key.Escape);
    }

    // T-P17 Preview: the project's scene, its sprite animations playing (still with Reduce motion)
    [AvaloniaFact]
    public void Preview_PlaysTheSceneAnimations()
    {
        var page = Open();
        var preview = page.Preview;
        Assert.Equal("Forest_01", preview.SceneName);
        Assert.True(preview.Playing);
        var first = preview.Frame!;
        Assert.Contains(first.Sprites, s => s.Frame >= 0);
        // a second later every animated sprite has moved on in its animation
        preview.SetTick(30);
        var later = preview.Frame!;
        Assert.NotEqual(first.Sprites.Select(s => s.Frame), later.Sprites.Select(s => s.Frame));
        Assert.NotEqual(first.Bgr, later.Bgr);
        // Reduce motion stops it on the first frame
        _h.S.Preferences.Update(p => p.ReduceMotion = true);
        Assert.False(preview.Playing);
        Assert.Equal(0, preview.Tick);
        _h.S.Preferences.Update(p => p.ReduceMotion = false);
        Assert.True(preview.Playing);
    }

    [AvaloniaFact]
    public void RightClickNode_ContextMenuDuplicateDisableGroupCollapseShowCodeDelete()
    {
        var page = Open();
        _h.ClickAt(Screen(page, 651 + 60, 57 + 15), MouseButton.Right);
        Assert.Equal("T-M21", _h.S.Windows.Requests[^1].Window);
        var menu = _h.OpenContextMenu()!;
        Assert.Equal(["Duplicate", "Disable", "Group into comment", "Collapse", "Show generated C#", "Delete"],
            menu.Items.OfType<MenuItem>().Select(i => (string?)i.Header));
        Assert.Equal("Ctrl+G", AgbStudio.Editor.Theme.Shortcuts.Format(menu.Items.OfType<MenuItem>().Single(i => (string?)i.Header == "Group into comment").InputGesture!));
        _h.RunMenuItem("Duplicate");
        Assert.Equal(11, page.Document.AllNodes.Count());
        var copy = page.Canvas.Selection.Single();
        Assert.Equal("SetVelocityY", page.Document.Node(copy)!.Type);
        _h.ClickAt(Screen(page, 651 + 60, 57 + 15), MouseButton.Right);
        _h.RunMenuItem("Disable");
        Assert.True(page.Document.IsDisabled("n6"));
        Assert.DoesNotContain("body.vy", page.Document.GenerateCSharp());
        Assert.Equal(0.45, page.Canvas.ViewOf("n6")!.Opacity);
        page.ToggleDisabled();
        Assert.False(page.Document.IsDisabled("n6"));
        Assert.Contains("body.vy = -3.50;", page.Document.GenerateCSharp());
        _h.ClickAt(Screen(page, 651 + 60, 57 + 15), MouseButton.Right);
        _h.RunMenuItem("Collapse");
        Assert.Equal(GraphDocument.HeaderHeight, page.Document.Height(page.Document.Node("n6")!));
        _h.ClickAt(Screen(page, 651 + 60, 57 + 15), MouseButton.Right);
        _h.RunMenuItem("Group into comment");
        Assert.Equal(2, page.Document.Comments.Count);
        // Ctrl+G on the canvas groups too; a comment is renamed in place
        page.Canvas.Focus();
        _h.Key(Key.G, RawInputModifiers.Control);
        Assert.Equal(3, page.Document.Comments.Count);
        var box = page.Canvas.BeginRenameComment(page.Document.Comments[^1]);
        box.Text = "Jump";
        _h.Key(Key.Enter);
        Assert.Equal("Jump", page.Document.Comments[^1].Title);
        _h.ClickAt(Screen(page, 651 + 60, 57 + 15), MouseButton.Right);
        _h.RunMenuItem("Show generated C#");
        var script = _h.Page<ScriptEditorPage>();
        Assert.Equal("PlayerLogic.cs", script.Buffer!.Name);
        Assert.True(script.Editor.IsReadOnly);
        Assert.Contains("void Update()", script.Editor.LineText(script.Editor.CaretLine - 1));
        // back to the graph: Delete removes the node and its wires
        _h.S.Commands.Execute("tools.logicGraph");
        Capture.Pump();
        page = _h.Page<LogicGraphPage>();
        page.Canvas.Select(["n7"]);
        page.Canvas.Focus();
        _h.Key(Key.Delete);
        Assert.Null(page.Document.Node("n7"));
        Assert.DoesNotContain(page.Document.Graph.Links, l => l.FromNode == "n7" || l.ToNode == "n7");
        _h.S.Undo.Undo();
        Assert.NotNull(page.Document.Node("n7"));
    }

    [AvaloniaFact]
    public void FileKeepsCommentsCollapsedAndDisabledOutsideTheCompiledGraph()
    {
        var doc = GraphDocument.New(Path.Combine(Path.GetTempPath(), "x.graph"), "XLogic");
        doc.Graph.Nodes.Add(new GraphNode { Id = "n1", Type = "OnUpdate" });
        doc.Graph.Nodes.Add(new GraphNode { Id = "n2", Type = "Jump" });
        doc.Graph.Links.Add(new GraphLink("n1.out", "n2.in"));
        doc.Comments.Add(new GraphComment { Title = "Hop", X = 1, Y = 2, W = 3, H = 4 });
        doc.Collapsed.Add("n1");
        doc.SetDisabled("n2", true);
        var json = doc.ToJson();
        // the compiler reads only enabled nodes
        var compiled = AgbStudio.Scripting.Graph.LogicGraph.Parse(json);
        Assert.Equal(["n1"], compiled.Nodes.Select(n => n.Id));
        Assert.DoesNotContain("body.Jump", GraphCodeGen.Generate(compiled, "XLogic"));
        var back = new GraphDocument();
        back.FromJson(json);
        Assert.True(back.IsDisabled("n2"));
        Assert.Contains("n1", back.Collapsed);
        Assert.Equal("Hop", back.Comments.Single().Title);
        back.SetDisabled("n2", false);
        Assert.Contains(back.Graph.Links, l => l.From == "n1.out" && l.To == "n2.in");
        Assert.Equal("XLogic", back.Graph.Class);
    }

    /// <summary>The node fields Figma shows: On Touch Tag, Set Velocity Y Mode (Set | Add | Clamp) and Target (Self or a wired actor); Vector pins.</summary>
    [AvaloniaFact]
    public void Inspector_TagModeTarget_AndVectorPins()
    {
        var page = Open();
        // On Touch: Tag "Coin"
        page.Canvas.Select(["n8"]);
        Capture.Pump();
        Assert.Equal("Coin", _h.Find<InspectorField>("Prop_tag").Value);
        Assert.Equal("Coin", ((TextBlock)((Border)page.Canvas.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "Node_n8")
            .GetVisualDescendants().OfType<Border>().First(b => b.Name == "Prop_tag")).Child!).Text);

        // Set Velocity Y: Mode is a segmented control; Add changes the generated code
        page.Canvas.Select(["n6"]);
        Capture.Pump();
        var segments = _h.Find<Border>("PropSegments_mode").GetVisualDescendants().OfType<Segment>().ToList();
        Assert.Equal(["Set", "Add", "Clamp"], segments.Select(s => (string?)s.Content));
        Assert.True(segments[0].IsChecked);
        var set = page.Code.Text;
        _h.Click(segments[1]);
        Assert.Equal("Add", page.Document.Node("n6")!.Props["mode"]);
        Assert.NotEqual(set, page.Code.Text);
        _h.S.Undo.Undo();
        Assert.Equal(set, page.Code.Text);

        // Target: Self until an actor is wired; choosing On Touch · Other wires it, Self removes the wire
        page.Canvas.Select(["n6"]);
        Capture.Pump();
        Assert.Equal("Self", _h.Find<InspectorField>("Pin_target").Value);
        Assert.NotNull(page.Canvas.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "Pin_target"));   // "Target  Self" on the node
        Assert.True(page.SetTarget("n6", "target", "On Touch · Other"));
        Assert.Contains(page.Document.Graph.Links, l => l.From == "n8.other" && l.To == "n6.target");
        page.Canvas.Select(["n6"]);
        Capture.Pump();
        Assert.Equal("On Touch · Other", _h.Find<InspectorField>("Pin_target").Value);
        Assert.True(page.SetTarget("n6", "target", "Self"));
        Assert.DoesNotContain(page.Document.Graph.Links, l => l.To == "n6.target");

        // Vector pins (Get Velocity) have their own colour
        page.AddNode("GetVelocity", new Point(900, 300), null);
        Capture.Pump();
        var vector = page.Canvas.GetVisualDescendants().OfType<NodePin>().First(p => p.Type == PinType.Vector);
        Assert.Equal(Avalonia.Media.Color.Parse("#B392FF"), ((Avalonia.Media.ISolidColorBrush)vector.Foreground!).Color);
    }
}

/// <summary>The Logic Graph without an open project: an empty graph that can still be edited.</summary>
public sealed class LogicGraphNoProjectTests
{
    [AvaloniaFact]
    public void OpensAndEditsWithoutAProject()
    {
        using var fx = new AgbStudio.Editor.Tests.Support.Fixture();
        var page = new LogicGraphPage(fx.Services);
        Assert.Empty(page.Document.AllNodes);
        var id = page.AddNode("OnUpdate", new Point(10, 10), null);
        Assert.Equal("OnUpdate", page.Document.Node(id)!.Type);
        Assert.Contains("void Update()", page.Code.Text);
    }
}
