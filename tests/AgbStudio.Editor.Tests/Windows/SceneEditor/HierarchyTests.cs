using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>P11.12: Hierarchy tree, search, add (+), context menu, selection in sync with the viewport and Inspector, rename.</summary>
public sealed class HierarchyTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    HierarchyPanel Panel => _w.All<HierarchyPanel>().Single();
    TreeItem Row(string key) => _w.Find<TreeItem>("Row_" + key);

    [AvaloniaFact]
    public void Tree_ShowsTheSceneAsInFigma()
    {
        var rows = Panel.Items.Select(i => (i.Label, i.Meta, i.Depth, i.Glyph)).ToList();
        Assert.Equal(
        [
            ("Forest_01", null, 0, "map"), ("Main Camera", "follow", 1, "camera"), ("Backgrounds", null, 1, "layers"),
            ("BG0 · Sky", "×0.2", 2, "image"), ("BG1 · Hills", "×0.5", 2, "image"), ("BG2 · Ground", "tilemap", 2, "tiles"),
            ("Player", "Actor", 1, "user"), ("Enemies", "2", 1, "folder"), ("Slime", null, 2, "cube"), ("Slime (1)", null, 2, "cube"),
            // the HUD folder holds HUD · Score and its three hearts (the HUD of the Figma picture)
            ("HUD", "4", 1, "folder"), ("Coins", "5", 1, "coin"), ("Question Block", null, 1, "cube"), ("Goal Flag", null, 1, "flag"),
            ("Exit → Cave_01", "trigger", 1, "link"),
        ], rows);
        Assert.Equal(TreeToggle.Closed, Row("group:HUD").Toggle);
        Assert.Equal(TreeToggle.Closed, Row("group:Coins").Toggle);
        Assert.True(Row("actor:0").IsSelected);
        // opening the Coins folder shows its five actors
        _w.DoubleClick(Row("group:Coins"));
        Assert.Equal(20, Panel.Items.Count);
        Assert.Equal(TreeToggle.Open, Row("group:Coins").Toggle);
    }

    /// <summary>Actor rows show what the actor is made of: behaviour, sprite, zone, link and camera badges, and every
    /// component in the tooltip (Templates, docs/evidence/ui-changes-since-figma.md).</summary>
    [AvaloniaFact]
    public void ActorRows_ShowTheirComponentsAsBadges()
    {
        // Player: Platformer Body (physics), PlayerController (script), the sprite, and the camera follows it
        Assert.Equal(["rocket", "code", "image", "camera"], Row("actor:0").Badges);
        var tip = Assert.IsType<TipContent>(ToolTip.GetTip(Row("actor:0")));
        Assert.Equal("Components: Sprite, Animation idle, Platformer Body, PlayerController (script), Collider, Camera follows", tip.Text);
        // Slime: Platformer Body, Patrol (AI), Damage on Touch (combat), sprite
        Assert.Equal(["rocket", "bug", "sword", "image"], Row("actor:1").Badges);
        // the exit is a trigger zone with a link to Cave_01
        var exit = Panel.AllRows().Single(r => r.Label.StartsWith("Exit", StringComparison.Ordinal));
        Assert.Equal(["flag", "link"], Row(exit.Key).Badges);
        Assert.Contains(exit.Parts!, b => b is { Kind: "link", Title: "Link to Cave_01" });
        // the badges are drawn in the row
        var icons = Row("actor:0").GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "PART_Badges");
        Assert.True(icons.IsVisible);
        Assert.Equal(4, icons.Children.Count);
        // rows that are not actors have none
        Assert.Null(Row("camera").Badges);
        Assert.False(Row("camera").GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "PART_Badges").IsVisible);
        // adding a component updates the badges
        _w.M.Edit("Add Component", () => _w.M.Scene!.Actors[0].Components.Add(new ComponentData { Type = "SoundOnEvent" }));
        Capture.Pump();
        Assert.Contains("sound", Row("actor:0").Badges!);
    }

    [AvaloniaFact]
    public void Search_FiltersRowsAndKeepsTheirFolders()
    {
        var search = _w.Find<SearchField>("HierarchySearch");
        search.Focus();
        _w.Type("slime");
        Assert.Equal(["Forest_01", "Enemies", "Slime", "Slime (1)"], Panel.Items.Select(i => i.Label));
        search.Text = "coin (3";
        Capture.Pump();
        Assert.Equal(["Forest_01", "Coins", "Coin (3)"], Panel.Items.Select(i => i.Label));
        search.Text = "";
        Capture.Pump();
        Assert.Equal(15, Panel.Items.Count);
    }

    [AvaloniaFact]
    public void Selection_IsSharedWithViewportAndInspector()
    {
        _w.Click(Row("actor:1"));
        Assert.Equal(1, _w.M.SelectedActorIndex);
        Assert.Equal("Slime", _w.Find<TextBlock>("ActorName").Text);
        Assert.Equal("Actor", Row("actor:1").Meta);
        Assert.Null(Row("actor:0").Meta);
        // the viewport draws the selection box of the selected actor
        var screen = _w.Find<GbaScreenView>("GbaScreen");
        Assert.NotNull(screen.ActorRect(_w.M.SelectedActorIndex));
        // selecting in the model (viewport click, menus) moves the Hierarchy selection
        _w.M.SelectActor(3);
        Capture.Pump();
        Assert.True(Row("actor:3").IsSelected);
        Assert.Equal("Question Block", _w.Find<TextBlock>("ActorName").Text);
        // layers and the camera are selectable too
        _w.Click(Row("layer:1"));
        Assert.Equal(new SceneItem(SceneItemKind.Layer, 1), _w.M.Selection);
        _w.Click(Row("camera"));
        Assert.Equal(SceneItemKind.Camera, _w.M.Selection!.Kind);
    }

    [AvaloniaFact]
    public void AddButtonAndContextMenu()
    {
        var header = _w.Find<PanelHeader>("HierarchyHeader");
        _w.Click(header.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action"));
        Assert.Equal(("T-M11", "HierarchyAdd"), _w.S.Windows.Requests[^1]);
        _w.Key(PhysicalKey.Escape);
        _w.Click(Row("actor:4"), MouseButton.Right);
        Assert.Equal(("T-M12", "HierarchyActor"), _w.S.Windows.Requests[^1]);
        Assert.Equal(4, _w.M.SelectedActorIndex);
    }

    [AvaloniaFact]
    public void F2_RenamesInPlace_AndUndo()
    {
        _w.Click(Row("actor:3"));
        _w.Key(PhysicalKey.F2);
        var box = Panel.RenameBox;
        Assert.NotNull(box);
        Assert.Equal("Question Block", box!.Text);
        box.SelectAll();
        _w.Type("Lucky Block");
        _w.Key(PhysicalKey.Enter);
        Assert.Equal("Lucky Block", _w.M.Scene!.Actors[3].Name);
        Assert.Equal("Lucky Block", GameProject.Load<SceneFile>(_w.M.ScenePath!).Actors[3].Name);
        Assert.Contains(Panel.Items, i => i.Label == "Lucky Block");
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal("Question Block", _w.M.Scene.Actors[3].Name);
    }

    [AvaloniaFact]
    public void EditMenu_DeleteDuplicateCopyPasteOnTheSelectedActor()
    {
        var m = _w.M;
        var count = m.Scene!.Actors.Count;
        _w.Click(Row("actor:3"));
        _w.S.Commands.Execute("edit.duplicate");
        Assert.Equal(count + 1, m.Scene.Actors.Count);
        Assert.Equal("Question Block (1)", m.Scene.Actors[4].Name);
        Assert.Equal(4, m.SelectedActorIndex);
        _w.S.Commands.Execute("edit.delete");
        Assert.Equal(count, m.Scene.Actors.Count);
        _w.S.Commands.Execute("edit.copy");
        _w.S.Commands.Execute("edit.paste");
        Assert.Equal(count + 1, m.Scene.Actors.Count);
        Assert.Equal(count + 1, GameProject.Load<SceneFile>(m.ScenePath!).Actors.Count);
        _w.S.Commands.Execute("edit.undo");
        _w.S.Commands.Execute("edit.undo");
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal(count, m.Scene.Actors.Count);
        Assert.Equal("Question Block", m.Scene.Actors[3].Name);
    }

    /// <summary>Each actor as name and position, in scene order (what Cut / Paste must bring back).</summary>
    List<string> Actors() => _w.M.Scene!.Actors.Select(a => $"{a.Name}@{a.X},{a.Y}").ToList();

    List<int> SelectedRowActors() => Panel.Items.Where(i => i.IsSelected && i.Tag is HierarchyRow { Item.Kind: SceneItemKind.Actor })
        .Select(i => ((HierarchyRow)i.Tag!).Item.Index).Order().ToList();

    // P10.11 on actors: Ctrl+X cuts the selected actor (saved without it), Ctrl+V puts it back where it was under its
    // name; Ctrl+A selects every actor (rows, viewport outlines, Inspector), and Cut / Paste then moves them all
    [AvaloniaFact]
    public void EditMenu_CutPasteAndSelectAllOnActors()
    {
        var m = _w.M;
        var original = Actors();
        var count = original.Count;
        var links = m.Scene!.Links.Count;
        _w.Click(Row("actor:3"));
        Assert.Equal(3, m.SelectedActorIndex);
        _w.Key(PhysicalKey.X, RawInputModifiers.Control);
        Assert.Equal(count - 1, m.Scene.Actors.Count);
        Assert.DoesNotContain(m.Scene.Actors, a => a.Name == "Question Block");
        Assert.Equal(count - 1, GameProject.Load<SceneFile>(m.ScenePath!).Actors.Count);
        Assert.DoesNotContain(Panel.Items, i => i.Label == "Question Block");
        _w.Key(PhysicalKey.V, RawInputModifiers.Control);
        Assert.Equal(count, m.Scene.Actors.Count);
        Assert.Equal(original[3], Actors()[^1]);
        Assert.Equal(count - 1, m.SelectedActorIndex);
        Assert.Equal(count, GameProject.Load<SceneFile>(m.ScenePath!).Actors.Count);
        m.Services.Undo.Undo();
        m.Services.Undo.Undo();
        Capture.Pump();
        Assert.Equal(original, Actors());

        // Ctrl+A: every actor is selected
        _w.Click(Row("actor:0"));
        var view = _w.All<GbaScreenView>().Single();
        // the start camera shows the Player; 100 px down the coins, Question Block and the slimes are on screen
        m.SetPan(0, 100);
        Capture.Pump();
        var accent = ((Avalonia.Media.ISolidColorBrush)AgbStudio.Editor.Services.ThemeService.Token(_w.Win, "accent/default")!).Color.ToUInt32();
        var samples = OutlineSamples(view, 0);
        Assert.True(samples.Count > 0, $"no actor outline on screen: screen {view.Origin} x{view.Scale}, boxes {string.Join("; ", Enumerable.Range(0, m.Scene!.Actors.Count).Select(i => view.SelectionBox(i)?.ToString() ?? "-"))}");
        Assert.Contains(DistanceTo(accent, samples), d => d > 40);   // not outlined while only the Player is selected
        _w.Key(PhysicalKey.A, RawInputModifiers.Control);
        Assert.Equal(Enumerable.Range(0, count), m.SelectedActorIndices);
        Assert.Equal(0, m.SelectedActorIndex);
        Assert.Equal(Panel.Items.Where(i => i.Tag is HierarchyRow { Item.Kind: SceneItemKind.Actor }).Select(i => ((HierarchyRow)i.Tag!).Item.Index).Order(), SelectedRowActors());
        Assert.False(Row("scene").IsSelected);
        // the viewport outlines each of them
        Assert.All(DistanceTo(accent, samples), d => Assert.InRange(d, 0, 12));
        // the Inspector shows the first one read-only under the count
        Assert.Equal($"{count} actors selected", _w.Find<TextBlock>("MultiSelectionText").Text);
        Assert.Equal("Player", _w.Find<TextBlock>("ActorName").Text);
        Assert.False(_w.Find<StackPanel>("MultiSelectionFields").IsEnabled);

        // Cut: every actor leaves (one undo step); Paste brings them all back, in order, where they were, selected
        _w.Key(PhysicalKey.X, RawInputModifiers.Control);
        Assert.Empty(m.Scene.Actors);
        Assert.Empty(GameProject.Load<SceneFile>(m.ScenePath!).Actors);
        Assert.DoesNotContain(Panel.Items, i => i.Tag is HierarchyRow { Item.Kind: SceneItemKind.Actor });
        Assert.Equal(SceneItemKind.Scene, m.Selection?.Kind);
        _w.Key(PhysicalKey.V, RawInputModifiers.Control);
        Assert.Equal(original, Actors());
        Assert.Equal(links, m.Scene.Links.Count);
        Assert.Equal(count, GameProject.Load<SceneFile>(m.ScenePath!).Actors.Count);
        Assert.Equal(Enumerable.Range(0, count), m.SelectedActorIndices);
        m.Services.Undo.Undo();
        Capture.Pump();
        Assert.Empty(m.Scene.Actors);
        m.Services.Undo.Undo();
        Capture.Pump();
        Assert.Equal(original, Actors());
        Assert.Equal(Enumerable.Range(0, count), m.SelectedActorIndices);

        // a plain click selects one actor again
        _w.Click(Row("actor:3"));
        Assert.Equal(new[] { 3 }, m.SelectedActorIndices);
        Assert.Equal(new[] { 3 }, SelectedRowActors());
        Assert.DoesNotContain(_w.All<TextBlock>(), t => t.Name == "MultiSelectionText");
    }

    // Ctrl + click adds an actor to the selection or takes it out, Shift + click selects the rows between; Duplicate and
    // Delete then act on every selected actor in one undo step each
    [AvaloniaFact]
    public void CtrlAndShiftClick_SelectSeveralActors_DuplicateAndDeleteActOnAll()
    {
        var m = _w.M;
        var original = Actors();
        _w.Click(Row("actor:0"));
        _w.Click(Row("actor:3"), mods: RawInputModifiers.Control);
        Assert.Equal(new[] { 0, 3 }, m.SelectedActorIndices);
        Assert.Equal(new[] { 0, 3 }, SelectedRowActors());
        Assert.Equal(0, m.SelectedActorIndex);
        Assert.Equal("2 actors selected", _w.Find<TextBlock>("MultiSelectionText").Text);
        // Ctrl + click again takes it out
        _w.Click(Row("actor:3"), mods: RawInputModifiers.Control);
        Assert.Equal(new[] { 0 }, m.SelectedActorIndices);
        Assert.DoesNotContain(_w.All<TextBlock>(), t => t.Name == "MultiSelectionText");
        // Shift + click: from the Player to Question Block, the actor rows shown between them
        _w.Click(Row("actor:3"), mods: RawInputModifiers.Shift);
        var rows = Panel.Items.Select(i => (HierarchyRow)i.Tag!).ToList();
        var from = rows.FindIndex(r => r.Key == "actor:0");
        var to = rows.FindIndex(r => r.Key == "actor:3");
        var between = rows.Skip(from).Take(to - from + 1).Where(r => r.Item.Kind == SceneItemKind.Actor).Select(r => r.Item.Index).Order().ToList();
        Assert.True(between.Count > 2, "the rows between the Player and Question Block hold more actors");
        Assert.Equal(between, m.SelectedActorIndices);
        Assert.Equal(between, SelectedRowActors());

        // Duplicate: a copy of each right after it, the copies selected; one undo step
        _w.Key(PhysicalKey.D, RawInputModifiers.Control);
        Assert.Equal(original.Count + between.Count, m.Scene!.Actors.Count);
        Assert.Equal(between.Select((i, k) => i + k + 1), m.SelectedActorIndices);
        Assert.All(m.SelectedActorIndices, i => Assert.StartsWith(m.Scene.Actors[i - 1].Name.Split(" (")[0], m.Scene.Actors[i].Name));
        m.Services.Undo.Undo();
        Capture.Pump();
        Assert.Equal(original, Actors());
        // Delete: all of them; one undo step brings them back
        _w.Click(Row("actor:0"));
        _w.Click(Row("actor:3"), mods: RawInputModifiers.Shift);
        _w.Key(PhysicalKey.Delete);
        Assert.Equal(original.Count - between.Count, m.Scene.Actors.Count);
        Assert.Equal(original.Where((_, i) => !between.Contains(i)), Actors());
        m.Services.Undo.Undo();
        Capture.Pump();
        Assert.Equal(original, Actors());
    }

    /// <summary>Points on the top edge of the outline the viewport would draw around each actor other than <paramref name="primary"/>, in window pixels (on screen, clear of the first actor's name tag and handles).</summary>
    List<PixelPoint> OutlineSamples(GbaScreenView view, int primary)
    {
        var screen = new Rect(view.Origin, new Size(240 * view.Scale, 160 * view.Scale));
        var keep = view.SelectionBox(primary) is { } p ? new Rect(p.X - 8, p.Y - 30, 160, p.Height + 38) : default;
        var list = new List<PixelPoint>();
        for (var i = 0; i < _w.M.Scene!.Actors.Count; i++)
        {
            if (i == primary || view.SelectionBox(i) is not { } b) continue;
            var mid = new Point(Math.Floor(b.X + b.Width / 2), Math.Floor(b.Y));
            if (!screen.Contains(mid) || keep.Contains(mid)) continue;
            var at = view.TranslatePoint(mid, _w.Win)!.Value;
            list.Add(new PixelPoint((int)at.X, (int)at.Y));
        }
        return list;
    }

    /// <summary>
    /// For each point, how far the pixel closest to <paramref name="color"/> within 2 px is from it (sum of the RGB
    /// differences) in one rendered frame of the window: 0 where the outline is drawn.
    /// </summary>
    List<int> DistanceTo(uint color, IEnumerable<PixelPoint> points)
    {
        using var fb = Capture.Frame(_w.Win).Lock();
        uint At(int x, int y)
        {
            var v = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(fb.Address + y * fb.RowBytes + x * 4);
            return fb.Format == Avalonia.Platform.PixelFormat.Rgba8888 ? (v & 0xFF00FF00u) | ((v & 0xFFu) << 16) | ((v >> 16) & 0xFFu) : v;
        }
        int Diff(uint a, uint b) => Math.Abs((int)((a >> 16) & 0xFF) - (int)((b >> 16) & 0xFF)) + Math.Abs((int)((a >> 8) & 0xFF) - (int)((b >> 8) & 0xFF)) + Math.Abs((int)(a & 0xFF) - (int)(b & 0xFF));
        return points.Select(p => Enumerable.Range(-2, 5).SelectMany(dy => Enumerable.Range(-2, 5).Select(dx => Diff(At(p.X + dx, p.Y + dy), color))).Min()).ToList();
    }
}
