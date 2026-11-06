using AgbStudio.Core.Project;
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

    // P17.02: opens the field's dropdown and returns its rows.
    IReadOnlyList<string> OpenReference(string prop)
    {
        var button = _h.Find<InspectorField>("Prop_" + prop).GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dropdown");
        _h.Click(button);
        return [.. ((IEnumerable<Control>)AgbStudio.Editor.Windows.Database.Dropdown.Last!.ItemsSource!).OfType<MenuItem>().Select(i => (string)i.Header!)];
    }

    [AvaloniaFact]
    public void ReferenceDropdowns_ListTheProjectWriteTheNodeAndAreOneUndoStep()
    {
        var p = _h.S.Project.Current!;
        foreach (var f in Directory.GetFiles(p.TextDir, "strings.*.json")) File.Delete(f);
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), new StringTable { Strings = { ["intro.hello"] = "Hello", ["intro.bye"] = "Bye" } });
        GameProject.Save(Path.Combine(p.TextDir, "strings.th.json"), new StringTable { Language = "th", Strings = { ["intro.hello"] = "สวัสดี" } });
        var sfx = Path.Combine(p.AssetsDir, "audio", "sfx");
        var music = Path.Combine(p.AssetsDir, "audio", "music");
        Directory.CreateDirectory(sfx);
        Directory.CreateDirectory(music);
        File.WriteAllBytes(Path.Combine(sfx, "reflist_coin.wav"), []);
        File.WriteAllBytes(Path.Combine(music, "reflist_town.xm"), []);
        File.WriteAllText(Path.Combine(p.ScenesDir, "RefListCave.scene"), "{}");
        p.Settings.Tags = ["Player", "Door"];
        p.Settings.Save.Fields =
        [
            new SaveField { Name = "coins", Type = "int" }, new SaveField { Name = "speed", Type = "fixed" },
            new SaveField { Name = "metOwl", Type = "bool" }, new SaveField { Name = "bag", Type = "Items[16]" },
        ];

        var page = Open();
        string Add(string type)
        {
            var id = page.AddNode(type, new Point(900, 700), null);
            page.Canvas.Select([id]);
            Capture.Pump();
            return id;
        }
        void Pick(string id, string prop, string option, string written)
        {
            page.Canvas.Select([id]);
            Capture.Pump();
            OpenReference(prop);
            Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose(option));
            Capture.Pump();
            Assert.Equal(written, page.Document.Node(id)!.Props[prop]);
        }

        var scene = page.AddNode("LoadScene", new Point(900, 300), ("n7", "out"));
        page.Canvas.Select([scene]);
        Capture.Pump();
        Assert.Contains("RefListCave", OpenReference("scene"));
        Pick(scene, "scene", "RefListCave", "RefListCave");
        Assert.Contains("Scene.Load(\"RefListCave\");", page.Code.Text);

        var show = Add("ShowText");
        Assert.Equal(["intro.bye", "intro.hello"], OpenReference("key"));
        Pick(show, "key", "intro.hello", "intro.hello");

        var song = Add("PlayMusic");
        Assert.Contains("reflist_town", OpenReference("music"));
        Pick(song, "music", "reflist_town", "reflist_town");

        // only numbers fit the variable nodes
        var variable = Add("SetVariable");
        Assert.Equal(["coins", "speed", GraphReferences.NewField], OpenReference("variable"));
        Pick(variable, "variable", "speed", "speed");

        // "jump.wav" keeps its row and its spelling until another row is chosen; one undo step brings it back
        page.Canvas.Select(["n7"]);
        Capture.Pump();
        File.WriteAllBytes(Path.Combine(sfx, "jump.wav"), []);
        var sounds = OpenReference("sound");
        Assert.Contains("jump.wav", sounds);
        Assert.DoesNotContain("jump", sounds);
        Assert.Contains("reflist_coin", sounds);
        Pick("n7", "sound", "reflist_coin", "reflist_coin");
        Assert.Contains("Sound.Play(\"reflist_coin\", 0.8);", page.Code.Text);
        _h.S.Undo.Undo();
        Capture.Pump();
        Assert.Equal("jump.wav", page.Document.Node("n7")!.Props["sound"]);
        Assert.Equal("jump.wav", _h.Find<InspectorField>("Prop_sound").Value);

        // On Touch: "(any)" clears the tag; Compare Tag must name one
        page.Canvas.Select(["n8"]);
        Capture.Pump();
        Assert.Equal(["(any)", "Player", "Door", GraphReferences.NewTag], OpenReference("tag"));
        Pick("n8", "tag", "(any)", "");
        Assert.Equal("(any)", _h.Find<InspectorField>("Prop_tag").Value);
        Add("CompareTag");
        Assert.Equal(["Player", "Door", GraphReferences.NewTag], OpenReference("tag"));
    }

    [AvaloniaFact]
    public void ReferenceDropdown_WithNothingInTheProjectChangesNothing()
    {
        var p = _h.S.Project.Current!;
        foreach (var f in Directory.GetFiles(p.TextDir, "strings.*.json")) File.Delete(f);
        var page = Open();
        var id = page.AddNode("ShowText", new Point(900, 700), null);
        page.Canvas.Select([id]);
        Capture.Pump();
        Assert.Equal([GraphReferences.NoneFound], OpenReference("key"));
        var undo = _h.S.Undo.UndoCount;
        Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose(GraphReferences.NoneFound));
        Capture.Pump();
        Assert.Equal("", page.Document.Node(id)!.Props.GetValueOrDefault("key", ""));
        Assert.Equal(undo, _h.S.Undo.UndoCount);
    }

    // P17.04: types a name into the box the "+ New" row opened and presses Enter.
    void EnterNewName(string name)
    {
        var box = _h.Find<TextBox>("NewReference");
        box.Text = name;
        box.Focus();
        Capture.Pump();
        _h.Key(Key.Enter);
        Capture.Pump();
    }

    [AvaloniaFact]
    public void NewField_FromTheDropdown_GoesToProjectSettingsAndTheGeneratedCode()
    {
        var p = _h.S.Project.Current!;
        p.Settings.Save.Fields = [new SaveField { Name = "coins", Type = "int" }];
        var page = Open();
        var id = page.AddNode("SetVariable", new Point(900, 300), ("n7", "out"));
        page.Canvas.Select([id]);
        Capture.Pump();
        OpenReference("variable");
        Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose(GraphReferences.NewField));
        Capture.Pump();

        // refused names leave the project and the node alone, and say why
        foreach (var bad in new[] { "two words", "9lives", "class", "COINS", "" })
        {
            EnterNewName(bad);
            Assert.NotEmpty(_h.Find<TextBlock>("NewReferenceProblem").Text!);
            Assert.Equal(["coins"], p.Settings.Save.Fields.Select(f => f.Name));
        }
        Assert.Equal("score", page.Document.Node(id)!.Props.GetValueOrDefault("variable", "score"));

        EnterNewName("lives");
        Assert.Equal("int", p.Settings.Save.Fields.Single(f => f.Name == "lives").Type);
        Assert.Contains("\"lives\"", File.ReadAllText(p.ProjectPath));
        Assert.Equal("lives", page.Document.Node(id)!.Props["variable"]);
        Assert.Equal("lives", _h.Find<InspectorField>("Prop_variable").Value);
        Assert.Contains("Game.lives = ", page.Code.Text);
        Assert.DoesNotContain(_h.Win.GetVisualDescendants().OfType<TextBox>(), b => b.Name == "NewReference");
        Assert.Contains("lives", OpenReference("variable"));

        // undo takes back the node's value; the field stays in the project
        _h.S.Undo.Undo();
        Capture.Pump();
        Assert.NotEqual("lives", page.Document.Node(id)!.Props.GetValueOrDefault("variable", ""));
        Assert.Contains(p.Settings.Save.Fields, f => f.Name == "lives");
    }

    // P19.05: the nodes of Phase 19 can be found, have a description, and take whole numbers only where the API does.
    [AvaloniaTheory]
    [InlineData("camera follow", "CameraFollow")]
    [InlineData("random", "RandomRange")]
    [InlineData("load game", "LoadGame")]
    [InlineData("save exists", "SaveExists")]
    [InlineData("set tile", "SetTile")]
    public void NewNodes_AreFoundInAddNodeAndDescribed(string query, string type)
    {
        Assert.Contains(AddNodePopover.Search(query), d => d.Type == type);
        Assert.NotEmpty(GraphDocument.Description(type));
    }

    // P20.04: the waiting nodes can be found and have a description; Choice draws one row per option with its output.
    [AvaloniaTheory]
    [InlineData("wait", "Wait")]
    [InlineData("say", "Say")]
    [InlineData("choice", "Choice")]
    [InlineData("move", "Move")]
    [InlineData("fade in", "FadeIn")]
    [InlineData("fade out", "FadeOut")]
    public void WaitingNodes_AreFoundInAddNodeAndDescribed(string query, string type)
    {
        Assert.Contains(AddNodePopover.Search(query), d => d.Type == type);
        Assert.NotEmpty(GraphDocument.Description(type));
        if (type == "Choice")
            Assert.Equal(["option1", "option2", "option3", "option4"], GraphDocument.Rows(NodeCatalog.Find(type)!).Select(r => r.Right!.Name));
    }

    // P20.05: a Wait under On Update is marked on the node and explained in the inspector; undo clears it.
    [AvaloniaFact]
    public void AWaitWhereNothingCanWait_IsMarkedOnTheNode_AndExplained()
    {
        var page = Open();
        var id = page.AddNode("Wait", new Point(900, 57), ("n6", "out"));
        Capture.Pump();
        Assert.StartsWith("Wait can't wait here: On Update runs every frame.", page.Canvas.ProblemOf(id));
        Assert.StartsWith("Wait can't wait here", _h.Find<TextBlock>("NodeProblem").Text);
        page.Canvas.Select([]);
        Capture.Pump();
        var body = (Border)((Panel)page.Canvas.ViewOf(id)!).Children[0];
        Assert.True(body.TryFindResource("status/danger", body.ActualThemeVariant, out var danger));
        Assert.Equal(danger, body.BorderBrush);
        Assert.Null(page.Canvas.ProblemOf("n6"));
        _h.S.Undo.Undo();
        Capture.Pump();
        Assert.Null(page.Canvas.ProblemOf(id));
    }

    [AvaloniaFact]
    public void WholeNumberSettings_RefuseDecimals()
    {
        var page = Open();
        var id = page.AddNode("SetTile", new Point(900, 300), ("n7", "out"));
        Capture.Pump();
        Assert.True(page.SetProp(id, "tile", "12"));
        Assert.False(page.SetProp(id, "tile", "1.5"));
        Assert.False(page.SetProp(id, "x", "two"));
        Assert.True(page.SetProp(id, "x", "-1"));
        Assert.Equal("12", page.Document.Node(id)!.Props["tile"]);
        Assert.Contains("Scene.SetTile(0, -1, 0, 12);", page.Code.Text);
    }

    [AvaloniaFact]
    public void NewField_IsRefusedWhenTheSaveDataPageWouldRefuseIt()
    {
        var p = _h.S.Project.Current!;
        p.Settings.Save.Fields = [new SaveField { Name = "odd", Type = "not a type" }];
        var page = Open();
        var id = page.AddNode("SetVariable", new Point(900, 300), ("n7", "out"));
        Assert.False(page.CreateReference(id, "variable", "lives"));
        Assert.DoesNotContain(p.Settings.Save.Fields, f => f.Name == "lives");
    }

    [AvaloniaFact]
    public void NewTag_FromTheDropdown_GoesToTheProjectAndTheNode_EscapeCancels()
    {
        var p = _h.S.Project.Current!;
        p.Settings.Tags = ["Player"];
        var page = Open();
        page.Canvas.Select(["n8"]);
        Capture.Pump();
        OpenReference("tag");
        Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose(GraphReferences.NewTag));
        Capture.Pump();
        var box = _h.Find<TextBox>("NewReference");
        box.Focus();
        Capture.Pump();
        _h.Key(Key.Escape);
        Capture.Pump();
        Assert.DoesNotContain(_h.Win.GetVisualDescendants().OfType<TextBox>(), b => b.Name == "NewReference");
        Assert.Equal(["Player"], p.Settings.Tags);

        OpenReference("tag");
        Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose(GraphReferences.NewTag));
        Capture.Pump();
        EnterNewName("player");
        Assert.Contains("already exists", _h.Find<TextBlock>("NewReferenceProblem").Text);
        EnterNewName("Door");
        Assert.Equal(["Player", "Door"], p.Settings.Tags);
        Assert.Equal("Door", page.Document.Node("n8")!.Props["tag"]);
        Assert.Contains("\"Door\"", File.ReadAllText(p.ProjectPath));
    }

    [AvaloniaFact]
    public void MissingReference_IsShownKeptOnSaveAndCanBeReplaced()
    {
        var p = _h.S.Project.Current!;
        var scene = Path.Combine(p.ScenesDir, "RefGoneCave.scene");
        File.WriteAllText(scene, "{}");
        File.WriteAllText(Path.Combine(p.ScenesDir, "RefHere.scene"), "{}");
        var page = Open();
        var id = page.AddNode("LoadScene", new Point(900, 300), ("n7", "out"));
        Capture.Pump();
        Assert.True(page.SetProp(id, "scene", "RefGoneCave"));
        Capture.Pump();
        page.Canvas.Select([id]);
        Capture.Pump();
        Assert.DoesNotContain(_h.Win.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "Missing_scene");

        // the scene is deleted: the next look marks the field and the node, and the name is untouched
        File.Delete(scene);
        page.Canvas.Rebuild();
        page.Canvas.Select(["n7"]);
        Capture.Pump();
        page.Canvas.Select([id]);
        Capture.Pump();
        Assert.Equal("RefGoneCave", _h.Find<InspectorField>("Prop_scene").Value);
        Assert.StartsWith("RefGoneCave is not in this project.", _h.Find<TextBlock>("Missing_scene").Text);
        Border Box() => page.Canvas.ViewOf(id)!.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Prop_scene");
        Assert.Equal(AgbStudio.Editor.Services.ThemeService.Token(Box(), "status/danger"), Box().BorderBrush);

        // nothing is erased: saving and opening the file again keep the name, and it is still marked
        page.Document.Save();
        var saved = File.ReadAllText(page.Document.Path);
        Assert.Contains("\"scene\": \"RefGoneCave\"", saved);
        Assert.Contains("Scene.Load(\"RefGoneCave\");", page.Code.Text);
        var reopened = AgbStudio.Scripting.Graph.LogicGraph.Parse(saved);
        Assert.Equal("RefGoneCave", reopened.Node(id)!.Props["scene"]);

        // choosing another scene clears the mark, and undo brings the missing name back
        OpenReference("scene");
        Assert.True(AgbStudio.Editor.Windows.Database.Dropdown.Choose("RefHere"));
        Capture.Pump();
        Assert.Equal("RefHere", page.Document.Node(id)!.Props["scene"]);
        Assert.DoesNotContain(_h.Win.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "Missing_scene");
        Assert.NotEqual(AgbStudio.Editor.Services.ThemeService.Token(Box(), "status/danger"), Box().BorderBrush);
        _h.S.Undo.Undo();
        Capture.Pump();
        Assert.Equal("RefGoneCave", page.Document.Node(id)!.Props["scene"]);
        Assert.NotNull(_h.Find<TextBlock>("Missing_scene"));
    }

    [AvaloniaTheory]
    [InlineData(PropKind.Scene, "here", false)]
    [InlineData(PropKind.Scene, "HERE", false)]    // the build ignores case
    [InlineData(PropKind.Scene, "gone", true)]
    [InlineData(PropKind.Scene, "", false)]        // not set yet
    [InlineData(PropKind.Sound, "ding", false)]
    [InlineData(PropKind.Sound, "ding.wav", false)]
    [InlineData(PropKind.Sound, "beep", false)]    // an SFX Designer effect
    [InlineData(PropKind.Sound, "gone.wav", true)]
    [InlineData(PropKind.Music, "town.xm", false)]
    [InlineData(PropKind.Music, "gone", true)]
    [InlineData(PropKind.Variable, "coins", false)]
    [InlineData(PropKind.Variable, "COINS", true)]  // a C# member: case matters
    [InlineData(PropKind.Variable, "metOwl", false)] // exists, even though the list offers only numbers
    [InlineData(PropKind.Tag, "door", false)]
    [InlineData(PropKind.Tag, "gone", true)]
    [InlineData(PropKind.TextKey, "intro.hello", false)]
    [InlineData(PropKind.TextKey, "gone", true)]
    public void IsMissing_MatchesNamesAsTheBuildDoes(PropKind kind, string value, bool missing)
    {
        var p = _h.S.Project.Current!;
        var sfx = Path.Combine(p.AssetsDir, "audio", "sfx");
        var music = Path.Combine(p.AssetsDir, "audio", "music");
        Directory.CreateDirectory(sfx);
        Directory.CreateDirectory(music);
        Directory.CreateDirectory(p.SfxDir);
        File.WriteAllBytes(Path.Combine(sfx, "ding.wav"), []);
        File.WriteAllText(Path.Combine(p.SfxDir, "beep.sfx"), "{}");
        File.WriteAllBytes(Path.Combine(music, "town.xm"), []);
        File.WriteAllText(Path.Combine(p.ScenesDir, "here.scene"), "{}");
        foreach (var f in Directory.GetFiles(p.TextDir, "strings.*.json")) File.Delete(f);
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), new StringTable { Strings = { ["intro.hello"] = "Hello" } });
        p.Settings.Tags = ["Door"];
        p.Settings.Save.Fields = [new SaveField { Name = "coins", Type = "int" }, new SaveField { Name = "metOwl", Type = "bool" }];
        Assert.Equal(missing, new GraphReferences(p).IsMissing(new PropDef("x", kind, "d"), value));
    }

    [AvaloniaFact]
    public void IsMissing_NeverCallsATextKeyMissingWhileATableIsBroken()
    {
        var p = _h.S.Project.Current!;
        File.WriteAllText(Path.Combine(p.TextDir, "strings.xx.json"), "{ broken");
        Assert.False(new GraphReferences(p).IsMissing(new PropDef("key", PropKind.TextKey, ""), "anything"));
        Assert.False(new GraphReferences(null).IsMissing(new PropDef("scene", PropKind.Scene, ""), "anything"));
    }

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
        // the Doc Tabs row over the canvas (docs/evidence/ui-changes-since-figma.md) leaves 34 px less canvas below
        _h.ClickAt(Screen(page, 700, 600), MouseButton.Right);
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
        Assert.Equal((700, 600), ((int)added.X, (int)added.Y));
        Assert.Equal("11 nodes · 9 wires", _h.Find<StatusItem>("Status_Counts").Content);
        // Shift+Enter adds and connects from the selected node (Play Sound's exec out)
        page.Canvas.Select(["n7"]);
        _h.ClickAt(Screen(page, 500, 660), MouseButton.Right);
        add = Assert.IsType<AddNodePopover>(_h.Win.Overlays.Open.Single());
        add.Query.Focus();
        _h.Type("checkpoint");
        _h.Key(Key.Enter, RawInputModifiers.Shift);
        var cp = page.Document.Graph.Nodes.Last();
        Assert.Equal("Checkpoint", cp.Type);
        Assert.Contains(page.Document.Graph.Links, l => l.From == "n7.out" && l.To == cp.Id + ".in");
        Assert.Contains("Save.Checkpoint();", page.Code.Text);
    }

    // Nodes › Search nodes: clicking and typing stay in the box and filter the list; Enter adds the best match
    [AvaloniaFact]
    public void NodesSearch_FiltersTheListInPlace()
    {
        var page = Open();
        _h.Click(page.Search);
        Assert.False(_h.Win.Overlays.IsOpen);
        page.Search.Focus();
        _h.Type("sou");
        Assert.False(_h.Win.Overlays.IsOpen);
        Assert.Equal("sou", page.Search.Text);
        Assert.Contains("Play Sound", page.PaletteLabels);
        Assert.DoesNotContain("On Start", page.PaletteLabels);
        var nodes = page.Document.AllNodes.Count();
        _h.Key(Key.Enter);
        Assert.Equal(nodes + 1, page.Document.AllNodes.Count());
        Assert.Equal("PlaySound", page.Document.Graph.Nodes.Last().Type);
    }

    // right-click opens Add Node at the mouse, like a context menu; the tip is hidden until ? shows it
    [AvaloniaFact]
    public void AddNodeOpensAtTheMouse_AndTheTipIsHiddenUntilAsked()
    {
        var page = Open();
        var at = Screen(page, 700, 600);
        _h.ClickAt(at, MouseButton.Right);
        var add = Assert.IsType<AddNodePopover>(_h.Win.Overlays.Open.Single());
        Capture.Pump();
        var corner = add.TranslatePoint(default, _h.Win.Overlays)!.Value;
        var mouse = _h.Win.TranslatePoint(at, _h.Win.Overlays)!.Value;
        // at the mouse, or moved left / up just enough to stay inside the window
        Assert.True(corner.X <= mouse.X + 1 && corner.Y <= mouse.Y + 1, $"{corner} {mouse}");
        Assert.True(mouse.X - corner.X < add.Bounds.Width && mouse.Y - corner.Y < add.Bounds.Height, $"{corner} {mouse}");
        Assert.True(corner.X + add.Bounds.Width <= _h.Win.Overlays.Bounds.Width && corner.Y + add.Bounds.Height <= _h.Win.Overlays.Bounds.Height);
        _h.Key(Key.Escape);
        Assert.False(page.Canvas.TipVisible);
        _h.Click(_h.Find<AgbButton>("GraphTipToggle"));
        Assert.True(page.Canvas.TipVisible);
        _h.Click(_h.Find<AgbButton>("GraphTipToggle"));
        Assert.False(page.Canvas.TipVisible);
    }

    // P10.11: the Edit menu on Logic Graph nodes: Select All, Copy / Paste, Cut, Duplicate, Delete, each undoable
    [AvaloniaFact]
    public void EditMenu_OnNodes_SelectAllCopyPasteCutDuplicateDelete()
    {
        var page = Open();
        int Nodes() => page.Document.AllNodes.Count();
        var n = Nodes();
        page.Canvas.Focus();
        _h.Key(Key.A, RawInputModifiers.Control);
        Assert.Equal(n, page.Canvas.Selection.Count);
        page.Canvas.Select(["n7"]);
        _h.Key(Key.C, RawInputModifiers.Control);
        _h.Key(Key.V, RawInputModifiers.Control);
        Assert.Equal(n + 1, Nodes());
        var pasted = page.Canvas.Selection.Single();
        Assert.Equal("PlaySound", page.Document.Node(pasted)!.Type);
        _h.S.Commands.Execute("edit.duplicate");
        Assert.Equal(n + 2, Nodes());
        _h.S.Commands.Execute("edit.delete");
        Assert.Equal(n + 1, Nodes());
        // Cut removes the selected node and keeps it for Paste
        page.Canvas.Select([pasted]);
        _h.Key(Key.X, RawInputModifiers.Control);
        Assert.Equal(n, Nodes());
        Assert.Null(page.Document.Node(pasted));
        _h.Key(Key.V, RawInputModifiers.Control);
        Assert.Equal(n + 1, Nodes());
        Assert.Equal("PlaySound", page.Document.Node(page.Canvas.Selection.Single())!.Type);
        _h.S.Commands.Execute("edit.undo");
        Assert.Equal(n, Nodes());
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
        // one separator, after Collapse, as in Figma
        Assert.Equal(["Duplicate", "Disable", "Group into comment", "Collapse", "-", "Show generated C#", "Delete"],
            menu.Items.Cast<object>().Select(i => i is MenuItem m ? (string?)m.Header : "-"));
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
        // On Touch: Tag "Coin" on the first row, the exec output under it, then Other (Figma T-P17)
        page.Canvas.Select(["n8"]);
        Capture.Pump();
        var touchRows = page.Document.RowsOf(page.Document.Node("n8")!);
        Assert.Equal(new string?[] { "tag", null, null }, touchRows.Select(r => r.Prop).ToArray());
        Assert.Equal(new string?[] { "out", "other" }, touchRows.Skip(1).Select(r => r.Right?.Name).ToArray());
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
        // Figma T-P17 draws Set Velocity Y with its Value row only: no Mode row, and no Target row while it is Self
        Panel Node6() => page.Canvas.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "Node_n6");
        Assert.Null(Node6().GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "Pin_target"));
        Assert.Equal(GraphDocument.HeaderHeight + GraphDocument.BodyPad * 2 + 2 * GraphDocument.RowHeight, page.Document.Height(page.Document.Node("n6")!));
        // Is On Ground has no other input: "Target  Self" on the node
        Assert.Contains(page.Document.RowsOf(page.Document.AllNodes.First(x => x.Type == "IsOnGround")), r => r.Left?.Name == "target");
        Assert.True(page.SetTarget("n6", "target", "On Touch · Other"));
        Assert.Contains(page.Document.Graph.Links, l => l.From == "n8.other" && l.To == "n6.target");
        Assert.Contains(page.Document.RowsOf(page.Document.Node("n6")!), r => r.Left?.Name == "target");   // wired: the pin gets its row
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
