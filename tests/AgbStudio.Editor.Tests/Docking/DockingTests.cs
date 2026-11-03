using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Docking;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Docking;

public sealed class DockLayoutTests
{
    static DockLayout L() => DockLayout.Default();

    [Fact]
    public void Default_MatchesTheFigmaFrame()
    {
        var l = L();
        Assert.Equal(260, l.GroupOf(DockLayout.Hierarchy)!.Size);
        Assert.Equal(320, l.GroupOf(DockLayout.Inspector)!.Size);
        Assert.Equal(250, l.GroupOf(DockLayout.Assets)!.Size);
        Assert.Equal(0, l.GroupOf(DockLayout.Viewport)!.Size);
    }

    [Fact]
    public void Center_TabsThePanelInTheTargetGroup_AndAnEmptyGroupLeaves()
    {
        var l = L();
        Assert.True(l.Move(DockLayout.Hierarchy, DockLayout.Assets, DockZone.Center, 1));
        Assert.Equal([DockLayout.Assets, DockLayout.Hierarchy, DockLayout.Console, DockLayout.Animation, DockLayout.Database], l.GroupOf(DockLayout.Assets)!.Panels);
        Assert.Equal(DockLayout.Hierarchy, l.GroupOf(DockLayout.Assets)!.Active);
        Assert.Equal(2, l.Root.Children.Count);
    }

    [Fact]
    public void Center_InTheSameGroup_Reorders()
    {
        var l = L();
        Assert.True(l.Move(DockLayout.Database, DockLayout.Assets, DockZone.Center, 0));
        Assert.Equal(DockLayout.Database, l.GroupOf(DockLayout.Assets)!.Panels[0]);
    }

    [Fact]
    public void Edge_AlongTheSplitInsertsAndAcrossItWraps()
    {
        var l = L();
        Assert.True(l.Move(DockLayout.Console, DockLayout.Inspector, DockZone.Left));
        Assert.Equal(4, l.Root.Children.Count);
        Assert.Equal([DockLayout.Console], l.Root.Children[2].Panels);
        Assert.Equal(160, l.GroupOf(DockLayout.Console)!.Size);
        Assert.Equal(160, l.GroupOf(DockLayout.Inspector)!.Size);

        Assert.True(l.Move(DockLayout.Animation, DockLayout.Hierarchy, DockZone.Bottom));
        var wrap = l.Root.Children[0];
        Assert.Equal(DockLayout.Rows, wrap.Split);
        Assert.Equal(260, wrap.Size);
        Assert.Equal([DockLayout.Hierarchy], wrap.Children[0].Panels);
        Assert.Equal([DockLayout.Animation], wrap.Children[1].Panels);
    }

    [Fact]
    public void Viewport_CannotMoveOrTakeTabs()
    {
        var l = L();
        var before = l.ToJson();
        Assert.False(l.Move(DockLayout.Viewport, DockLayout.Assets, DockZone.Center));
        Assert.False(l.Move(DockLayout.Assets, DockLayout.Viewport, DockZone.Center));
        Assert.False(l.Float(DockLayout.Viewport, 0, 0));
        Assert.False(l.Move(DockLayout.Hierarchy, DockLayout.Hierarchy, DockZone.Left));
        Assert.Equal(before, l.ToJson());
    }

    [Fact]
    public void EveryMoveKeepsOneFillingChildPerSplit_AndNoSplitOfOne()
    {
        var l = L();
        l.Move(DockLayout.Hierarchy, DockLayout.Inspector, DockZone.Top);
        l.Move(DockLayout.Inspector, DockLayout.Viewport, DockZone.Left);
        l.Move(DockLayout.Assets, DockLayout.Viewport, DockZone.Right);
        l.Move(DockLayout.Console, DockLayout.Assets, DockZone.Bottom);
        l.Move(DockLayout.Animation, DockLayout.Console, DockZone.Center);
        l.Move(DockLayout.Database, DockLayout.Hierarchy, DockZone.Center);
        void Check(DockNode n)
        {
            if (n.IsGroup) { Assert.NotEmpty(n.Panels); return; }
            Assert.True(n.Children.Count >= 2);
            Assert.Single(n.Children, c => c.Size == 0);
            foreach (var c in n.Children) Check(c);
        }
        Check(l.Root);
        Assert.Equal(7, l.AllPanels().Distinct().Count());
    }

    [Fact]
    public void FloatAndDock_GoOutAndBackBesideTheirGroup()
    {
        var l = L();
        Assert.True(l.Float(DockLayout.Console, 10, 20));
        Assert.DoesNotContain(DockLayout.Console, l.GroupOf(DockLayout.Assets)!.Panels);
        Assert.Equal(DockLayout.Assets, l.Floats.Single().ReturnTo);
        l.Dock(DockLayout.Console);
        Assert.Empty(l.Floats);
        Assert.Contains(DockLayout.Console, l.GroupOf(DockLayout.Assets)!.Panels);
    }

    [Fact]
    public void Json_RoundTrips_DamagedGivesDefault_AndPanelsAreFitted()
    {
        var l = L();
        l.Move(DockLayout.Hierarchy, DockLayout.Assets, DockZone.Center);
        Assert.Equal(l.ToJson(), DockLayout.FromJson(l.ToJson()).ToJson());
        Assert.Equal(L().ToJson(), DockLayout.FromJson("{ not json").ToJson());
        Assert.Equal(L().ToJson(), DockLayout.FromJson(null).ToJson());

        string[] known = [DockLayout.Viewport, DockLayout.Assets, DockLayout.Hierarchy, "New"];
        var fitted = DockLayout.FromJson(L().ToJson(), known);
        Assert.Equal(known.Order(), fitted.AllPanels().Order());
    }
}

public sealed class DockHostTests : IDisposable
{
    readonly Fixture _fx = new();
    readonly MainWindow _win;

    public DockHostTests()
    {
        _fx.OpenSkyHopper();
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        _win.Close();
        _fx.Dispose();
    }

    SceneWorkspace Ws => Assert.IsType<SceneWorkspace>(_win.CurrentPage);
    T Find<T>(string name) where T : Control => _win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    Point Mid(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), _win)!.Value;

    void Drag(Point from, Point to)
    {
        _win.MouseMove(from);
        _win.MouseDown(from, MouseButton.Left);
        _win.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        _win.MouseMove(to);
        _win.MouseUp(to, MouseButton.Left);
        Capture.Pump();
    }

    void HoldDrag(Point from, Point to)
    {
        _win.MouseMove(from);
        _win.MouseDown(from, MouseButton.Left);
        _win.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Capture.Pump();
    }

    [AvaloniaFact]
    public void Ghost_NamesThePanelOrDocumentWhileDragging_AndGoesOnDrop()
    {
        var from = Mid(Find<PanelTab>("Tab_Console"));
        var to = from + new Point(0, -80);
        HoldDrag(from, to);
        Assert.True(DragGhost.IsVisible);
        Assert.Equal("Console", DragGhost.Title);
        Assert.Equal(0.45, Find<PanelTab>("Tab_Console").Opacity);
        _win.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Capture.Pump();
        Assert.False(DragGhost.IsVisible);
        Assert.Equal(1, Find<PanelTab>("Tab_Console").Opacity);
        _win.MouseUp(to, MouseButton.Left);
        Capture.Pump();

        var tab = Mid(Find<DocTab>("Doc_Scene"));
        HoldDrag(tab, tab + new Point(40, 0));
        Assert.Equal("Scene", DragGhost.Title);
        _win.MouseUp(tab + new Point(40, 0), MouseButton.Left);
        Capture.Pump();
        Assert.False(DragGhost.IsVisible);
    }

    [AvaloniaFact]
    public void Ghost_NamesTheActorDraggedInTheHierarchy()
    {
        var row = _win.GetVisualDescendants().OfType<TreeItem>().First(t => t.Tag is AgbStudio.Editor.Windows.SceneEditor.HierarchyRow r && r.Item.Kind == AgbStudio.Editor.Windows.SceneEditor.SceneItemKind.Actor);
        var name = ((AgbStudio.Editor.Windows.SceneEditor.HierarchyRow)row.Tag!).Label;
        var from = Mid(row);
        var to = from + new Point(0, 40);
        HoldDrag(from, to);
        Assert.Equal(name, DragGhost.Title);
        _win.MouseUp(to, MouseButton.Left);
        Capture.Pump();
        Assert.False(DragGhost.IsVisible);
    }

    TreeItem ActorRow(string label) => _win.GetVisualDescendants().OfType<TreeItem>().First(t => t.Tag is AgbStudio.Editor.Windows.SceneEditor.HierarchyRow r && r.Label == label && r.Item.Kind == AgbStudio.Editor.Windows.SceneEditor.SceneItemKind.Actor);

    IReadOnlyList<string> ActorLabels() => _win.GetVisualDescendants().OfType<TreeItem>()
        .Where(t => t.Tag is AgbStudio.Editor.Windows.SceneEditor.HierarchyRow { Item.Kind: AgbStudio.Editor.Windows.SceneEditor.SceneItemKind.Actor })
        .Select(t => ((AgbStudio.Editor.Windows.SceneEditor.HierarchyRow)t.Tag!).Label).ToList();

    Point RowPoint(TreeItem row, double share) => row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height * share), _win)!.Value;

    [AvaloniaFact]
    public void Hierarchy_ShowsALineAboveOrBelow_AndABoxForChild_AndTheChipSaysWhich()
    {
        var names = ActorLabels();
        var from = Mid(ActorRow(names[0]));
        var other = names[2];
        var line = Find<Border>("HierarchyDropLine");
        var box = Find<Border>("HierarchyDropBox");
        _win.MouseMove(from);
        _win.MouseDown(from, MouseButton.Left);

        void Over(double share) { _win.MouseMove(RowPoint(ActorRow(other), share), RawInputModifiers.LeftMouseButton); Capture.Pump(); }

        Over(0.1);
        Assert.True(line.IsVisible);
        Assert.False(box.IsVisible);
        Assert.Equal("Above " + other, DragGhost.Hint);
        var rowTop = ActorRow(other).TranslatePoint(default, _win)!.Value.Y;
        Assert.Equal(rowTop, line.TranslatePoint(new Point(0, 1), _win)!.Value.Y, 1);

        Over(0.9);
        Assert.True(line.IsVisible);
        Assert.Equal("Below " + other, DragGhost.Hint);

        Over(0.5);
        Assert.False(line.IsVisible);
        Assert.True(box.IsVisible);
        Assert.Equal("Child of " + other, DragGhost.Hint);
        Assert.Equal(ActorRow(other).Bounds.Height, box.Height);

        _win.MouseUp(RowPoint(ActorRow(other), 0.5), MouseButton.Left);
        Capture.Pump();
        Assert.False(line.IsVisible);
        Assert.False(box.IsVisible);
        Assert.False(DragGhost.IsVisible);
        var moved = AgbStudio.Editor.Windows.SceneEditor.SceneEditorModel.Of(_fx.Services).Scene!.Actors.First(a => a.Name == names[0]);
        Assert.Equal(other, moved.Parent);
    }

    [AvaloniaFact]
    public void Hierarchy_RefusesItselfAndItsChildren_WithoutAParentLoop()
    {
        var m = AgbStudio.Editor.Windows.SceneEditor.SceneEditorModel.Of(_fx.Services);
        var names = ActorLabels();
        m.SetActorParent(m.Scene!.Actors.FindIndex(a => a.Name == names[1]), names[0]);
        Capture.Pump();
        var parent = ActorRow(names[0]);
        var child = ActorRow(names[1]);
        _win.MouseMove(Mid(parent));
        _win.MouseDown(Mid(parent), MouseButton.Left);
        _win.MouseMove(RowPoint(child, 0.1), RawInputModifiers.LeftMouseButton);
        Capture.Pump();
        Assert.Equal("Can't drop on itself or its children", DragGhost.Hint);
        Assert.False(Find<Border>("HierarchyDropLine").IsVisible);
        _win.MouseUp(RowPoint(child, 0.1), MouseButton.Left);
        Capture.Pump();
        Assert.Equal("", m.Scene!.Actors.First(a => a.Name == names[0]).Parent);
        Assert.Equal(names[0], m.Scene!.Actors.First(a => a.Name == names[1]).Parent);
    }

    [AvaloniaFact]
    public void DefaultLayout_KeepsTheFigmaSizes()
    {
        Assert.Equal(260, Find<Border>("HierarchyPanel").Bounds.Width);
        Assert.Equal(320, Find<Border>("InspectorPanel").Bounds.Width);
        Assert.Equal(250, Find<Border>("BottomPanel").Bounds.Height);
        Assert.Equal(34, Find<Border>("DocTabs").Bounds.Height);
    }

    [AvaloniaFact]
    public void DragHeader_OntoTheBottomTabs_MakesATabAndKeepsThePanelContent()
    {
        var row = Find<PanelTab>("Tab_Animation");
        Drag(Mid(Find<PanelHeader>("InspectorHeader")), Mid(row));
        var group = Ws.DockArea.Layout.GroupOf(DockLayout.Assets)!;
        Assert.Contains(DockLayout.Inspector, group.Panels);
        Assert.Equal(DockLayout.Inspector, group.Active);
        Assert.True(Find<PanelTab>("Tab_Inspector").IsSelected);
        // the same field controls show in the new place
        Assert.NotEmpty(Find<Border>("BottomPanel").GetVisualDescendants().OfType<TextBlock>());
    }

    [AvaloniaFact]
    public void DragTab_ToTheViewportEdge_SplitsBesideTheDocument()
    {
        var doc = Find<Grid>("DocumentGroup");
        var p = doc.TranslatePoint(new Point(4, doc.Bounds.Height / 2), _win)!.Value;
        Drag(Mid(Find<PanelTab>("Tab_Console")), p);
        var g = Ws.DockArea.Layout.GroupOf(DockLayout.Console)!;
        Assert.Equal([DockLayout.Console], g.Panels);
        Assert.Contains(DockLayout.Console, Ws.DockArea.Layout.AllPanels());
        Assert.DoesNotContain(DockLayout.Console, Ws.DockArea.Layout.GroupOf(DockLayout.Assets)!.Panels);
    }

    [AvaloniaFact]
    public void Click_WithoutMoving_OnlySelectsTheTab()
    {
        var p = Mid(Find<PanelTab>("Tab_Console"));
        _win.MouseMove(p);
        _win.MouseDown(p, MouseButton.Left);
        _win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
        Assert.Equal("Console", Ws.SelectedPanel!.Label);
        Assert.Equal(DockLayout.Console, Ws.DockArea.Layout.GroupOf(DockLayout.Console)!.Active);
    }

    [AvaloniaFact]
    public void Layout_IsSavedInThePreferencesAndComesBack()
    {
        Ws.DockArea.Move(DockLayout.Hierarchy, DockLayout.Inspector, DockZone.Center);
        Capture.Pump();
        var saved = _fx.Services.Preferences.Current.SceneLayout;
        Assert.Contains(DockLayout.Hierarchy, DockLayout.FromJson(saved).GroupOf(DockLayout.Inspector)!.Panels);
        var again = new SceneWorkspace(_fx.Services);
        Assert.Contains(DockLayout.Hierarchy, again.DockArea.Layout.GroupOf(DockLayout.Inspector)!.Panels);
    }

    [AvaloniaFact]
    public void DefaultLayoutCommand_And_SavedLayouts()
    {
        Ws.DockArea.Move(DockLayout.Console, DockLayout.Inspector, DockZone.Center);
        WorkspaceLayouts.Save(_fx.Services, Ws.DockArea, "Wide");
        _fx.Services.Commands.Execute("tools.layout.reset");
        Capture.Pump();
        Assert.Equal(DockLayout.Default().ToJson(), Ws.DockArea.Layout.ToJson());
        Assert.Equal(320, Find<Border>("InspectorPanel").Bounds.Width);
        WorkspaceLayouts.Load(Ws.DockArea, _fx.Services.Preferences.Current.SavedLayouts["Wide"]);
        Capture.Pump();
        Assert.Contains(DockLayout.Console, Ws.DockArea.Layout.GroupOf(DockLayout.Inspector)!.Panels);
    }

    [AvaloniaFact]
    public void Float_OpensAWindow_ClosingItDocksThePanelBack()
    {
        Assert.True(Ws.DockArea.Float(DockLayout.Inspector, 50, 50));
        Capture.Pump();
        var w = Ws.DockArea.FloatWindow(DockLayout.Inspector);
        Assert.NotNull(w);
        Assert.True(w!.IsVisible);
        Assert.Null(Ws.DockArea.Layout.GroupOf(DockLayout.Inspector));
        w.Close();
        Capture.Pump();
        Assert.Empty(Ws.DockArea.Layout.Floats);
        Assert.NotNull(Ws.DockArea.Layout.GroupOf(DockLayout.Inspector));
        Assert.Null(Ws.DockArea.FloatWindow(DockLayout.Inspector));
    }

    [AvaloniaFact]
    public void DocumentTab_DragReorders_WithoutLeavingThePage_AndAClickStillOpens()
    {
        var docs = _fx.Services.Documents;
        var scene = Find<DocTab>("Doc_Scene");
        var game = Find<DocTab>("Doc_Game");
        Assert.Equal("Scene", docs.Open[0].Name);
        Drag(Mid(scene), new Point(Mid(game).X + game.Bounds.Width / 2 + 2, Mid(game).Y));
        Assert.True(docs.Open.Select(d => d.Name).ToList().IndexOf("Scene") >= 1);
        Assert.IsType<SceneWorkspace>(_win.CurrentPage);
        var p = Mid(Find<DocTab>("Doc_Game"));
        _win.MouseMove(p);
        _win.MouseDown(p, MouseButton.Left);
        Assert.IsType<SceneWorkspace>(_win.CurrentPage);
        _win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
        Assert.Equal("T-P02", _win.CurrentPageId);
    }
}
