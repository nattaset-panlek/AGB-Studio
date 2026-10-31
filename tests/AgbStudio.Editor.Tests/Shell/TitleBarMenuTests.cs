using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.Launcher;
using AgbStudio.Editor.Tests.Windows.Scripts;
using AgbStudio.Editor.Windows.Launcher;
using AgbStudio.Editor.Windows.Scripts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Shell;

/// <summary>
/// The title bar menus with real clicks (T-B01..T-B07, T-M01..T-M08): every title opens its menu, File › Open Recent
/// runs a project through Opening Project (T-D22) and Clear Recent empties it, File › Save All saves the edited files,
/// File › Exit and closing the window ask about unsaved files (T-D12).
/// </summary>
public sealed class TitleBarMenuTests
{
    static MenuItem Title(MainWindow win, string title) => win.MenuBar.Items.OfType<MenuItem>().Single(m => (string?)m.Header == title);

    static MenuItem Item(MenuItem menu, string header) => menu.Items.OfType<MenuItem>().Single(m => (string?)m.Header == header);

    static IEnumerable<string?> Headers(MenuItem menu) => menu.Items.OfType<MenuItem>().Select(m => (string?)m.Header);

    /// <summary>One left click on a control, through the window or the menu popup it is shown in.</summary>
    static void Click(Control c)
    {
        var top = TopLevel.GetTopLevel(c) ?? throw new Xunit.Sdk.XunitException($"{c.Name ?? c.GetType().Name} is not shown");
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), top)!.Value;
        top.MouseMove(p);
        top.MouseDown(p, MouseButton.Left);
        top.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    static void Key(MainWindow win, PhysicalKey k)
    {
        win.KeyPressQwerty(k, RawInputModifiers.None);
        win.KeyReleaseQwerty(k, RawInputModifiers.None);
        Capture.Pump();
    }

    /// <summary>A menu item shown in its open popup (a popup window, or the window's overlay layer), laid out and visible.</summary>
    static void AssertShown(MainWindow win, MenuItem item)
    {
        Assert.True(TopLevel.GetTopLevel(item) is { } top && (top == win || top is Avalonia.Controls.Primitives.IPopupHost), $"{item.Header} is not shown");
        Assert.True(item.GetVisualAncestors().Any(a => a is Avalonia.Controls.Primitives.IPopupHost) || TopLevel.GetTopLevel(item) is Avalonia.Controls.Primitives.IPopupHost,
            $"{item.Header} is not in a menu popup");
        Assert.True(item.IsEffectivelyVisible && item.Bounds.Height > 0, $"{item.Header} is not shown");
    }

    // T-B01..T-B07 open T-M01..T-M07 ("from" of each menu): click the title, the menu opens with its first items; Esc closes it
    [AvaloniaFact]
    public void TitleBar_ClickEachMenuTitle_OpensItsMenuWithItsFirstItems()
    {
        using var h = new EditorHarness();
        var menus = new (string Title, string Id, string[] First)[]
        {
            ("File", "T-M01", ["New Project…", "Open Project…", "Open Recent"]),
            ("Edit", "T-M02", ["Undo", "Redo", "Cut"]),
            ("Assets", "T-M03", ["Create", "Import…", "Reimport"]),
            ("Scene", "T-M04", ["New Scene…", "Scene Settings…", "Set as Start Scene"]),
            ("Build", "T-M05", ["Play", "Play from Here", "Build ROM"]),
            ("Tools", "T-M06", ["Palette Manager", "Font Manager", "Database"]),
            ("Help", "T-M07", ["Getting Started", "Scripting API", "Keyboard Shortcuts"]),
        };
        Assert.Equal(menus.Select(m => m.Title), h.Win.MenuBar.Items.OfType<MenuItem>().Select(m => (string?)m.Header));
        var page = h.Win.CurrentPageId;
        foreach (var (title, id, first) in menus)
        {
            var menu = Title(h.Win, title);
            Assert.Same(h.Win, TopLevel.GetTopLevel(menu));
            Click(menu);
            Assert.True(menu.IsSubMenuOpen, $"the {title} menu did not open");
            Assert.Equal(id, menu.Tag);
            Assert.All(h.Win.MenuBar.Items.OfType<MenuItem>().Where(m => m != menu), m => Assert.False(m.IsSubMenuOpen, $"{m.Header} is open with {title}"));
            var items = menu.Items.OfType<MenuItem>().Take(first.Length).ToList();
            Assert.Equal(first, items.Select(m => (string?)m.Header));
            foreach (var item in items) AssertShown(h.Win, item);
            // the items are listed top to bottom
            Assert.True(items.Zip(items.Skip(1)).All(p => p.First.TranslatePoint(default, p.Second)!.Value.Y < 0), $"{title} items are not in order");
            Key(h.Win, PhysicalKey.Escape);
            Assert.False(menu.IsSubMenuOpen, $"Esc did not close the {title} menu");
            // a title only opens its menu: the page stays
            Assert.Equal(page, h.Win.CurrentPageId);
        }
    }

    // T-M08, T-D22 and the Open Recent part of T-M01: a recent project opens through Opening Project; Clear Recent empties the list
    [AvaloniaFact]
    public async Task FileOpenRecent_OpensTheProjectThroughOpeningProject_AndClearRecentEmptiesTheList()
    {
        using var h = new LauncherHarness(startPage: null, recent: true);
        var gbaProjects = h.S.Recent.Items.ToDictionary(r => r.Name, r => r.Path);
        h.Fx.OpenSkyHopper();
        // the fixture's Sky Hopper is the open project; its empty namesake of the Launcher fixture goes
        h.S.Recent.Remove(gbaProjects["Sky Hopper"]);
        var cave = gbaProjects["Cave Quest RPG"];
        var p = GameProject.Open(cave);
        ProjectCreator.AddSampleLevel(p);
        p.Settings.LastScene = ProjectCreator.SampleScene;
        p.SaveSettings();
        h.Show("T-P01");
        var hold = new TaskCompletionSource();
        var reached = new List<string>();
        h.Launcher.BeforeOpenStep = async (step, ct) =>
        {
            reached.Add(step);
            if (step == "Load assets") await hold.Task.WaitAsync(ct);
        };

        // File › Open Recent lists the recent projects, the open one checked, then Clear Recent
        var file = Title(h.Win, "File");
        Click(file);
        var recent = Item(file, "Open Recent");
        AssertShown(h.Win, recent);
        Click(recent);
        Assert.True(recent.IsSubMenuOpen, "Open Recent did not open");
        Assert.Equal(["Sky Hopper", "Cave Quest RPG", "Snow Run", "Dusk Novel", "Clear Recent"], Headers(recent));
        Assert.Equal(["Sky Hopper"], recent.Items.OfType<MenuItem>().Where(m => m.IsChecked).Select(m => (string?)m.Header));
        Assert.Single(recent.Items.OfType<Separator>());
        var entry = Item(recent, "Cave Quest RPG");
        AssertShown(h.Win, entry);
        Click(entry);

        // Opening Project (T-D22) runs its steps for that project
        await LauncherHarness.Until(() => reached.Contains("Load assets"));
        Assert.Equal(("T-D22", "OpenRecent"), h.S.Windows.Requests[^1]);
        Assert.False(file.IsSubMenuOpen);
        var dlg = Assert.IsType<OpeningProjectDialog>(h.Win.Overlays.Open.Single());
        Assert.Equal("Opening Cave Quest RPG…", dlg.GetVisualDescendants().OfType<DialogHeader>().Single().Title);
        Assert.Equal([OpenStepState.Done, OpenStepState.Running, OpenStepState.Pending, OpenStepState.Pending], dlg.States);
        Assert.Equal("Sky Hopper", h.S.Project.Name);
        hold.SetResult();
        await LauncherHarness.Until(() => !h.Win.Overlays.IsOpen);
        Assert.Equal(["Read project", "Load assets", "Compile scripts", "Open last scene"], reached);
        Assert.Equal("Cave Quest RPG", h.S.Project.Name);
        Assert.Equal("T-P01", h.Win.CurrentPageId);
        Assert.Equal("Forest_01.scene", h.Win.TitleBar.Document);

        // the menu now has it first and checked
        Click(file);
        Click(recent);
        Assert.Equal(["Cave Quest RPG", "Sky Hopper", "Snow Run", "Dusk Novel", "Clear Recent"], Headers(recent));
        Assert.Equal(["Cave Quest RPG"], recent.Items.OfType<MenuItem>().Where(m => m.IsChecked).Select(m => (string?)m.Header));

        // Clear Recent empties the list; the menu then holds Clear Recent only
        Click(Item(recent, "Clear Recent"));
        Assert.Empty(h.S.Recent.Items);
        Assert.False(file.IsSubMenuOpen);
        Click(file);
        Click(recent);
        Assert.Equal(["Clear Recent"], Headers(recent));
        Assert.Empty(recent.Items.OfType<Separator>());
        Key(h.Win, PhysicalKey.Escape);
        Key(h.Win, PhysicalKey.Escape);
        // the open project stays open
        Assert.Equal("Cave Quest RPG", h.S.Project.Name);
    }

    // ---- File › Save All, File › Exit, closing the window (T-M01, T-D12) ----------------------------------------------

    /// <summary>Opens a script in the Script Editor and adds a comment at the end of its first line (unsaved).</summary>
    static ScriptEditorPage EditScript(EditorHarness h, string name, string comment)
    {
        h.Open("T-P04", SkyHopperProject.Script(h.Fx, name));
        var page = h.Page<ScriptEditorPage>();
        page.Editor.Focus();
        page.Editor.MoveCaret(1, page.Editor.LineText(0).Length + 1);
        h.Type(" " + comment);
        Assert.True(page.Buffer!.Document.IsDirty);
        return page;
    }

    static string Disk(EditorHarness h, string name) => File.ReadAllText(SkyHopperProject.Script(h.Fx, name));

    [AvaloniaFact]
    public async Task FileSaveAll_SavesEveryEditedFile()
    {
        using var h = new EditorHarness();
        ScriptEditorSettings.Of(h.S).FormatOnSave = false;
        var player = EditScript(h, "PlayerController", "// player edit");
        var coin = EditScript(h, "Coin", "// coin edit");
        Assert.Equal(2, h.S.Documents.Dirty.Count);
        Assert.DoesNotContain("// player edit", Disk(h, "PlayerController"));

        var file = Title(h.Win, "File");
        Click(file);
        var saveAll = Item(file, "Save All");
        Assert.True(saveAll.IsEffectivelyEnabled);
        Click(saveAll);
        await LauncherHarness.Until(() => !h.S.Documents.HasUnsaved);
        Assert.Contains("// player edit", Disk(h, "PlayerController"));
        Assert.Contains("// coin edit", Disk(h, "Coin"));
        Assert.False(player.Buffer!.Document.IsDirty);
        Assert.False(coin.Buffer!.Document.IsDirty);
        Assert.All(h.Win.GetVisualDescendants().OfType<DocTab>(), t => Assert.False(t.IsDirty, t.Name + " still shows the dirty dot"));
        // with nothing to save, Save All is greyed out
        Click(file);
        Assert.False(Item(file, "Save All").IsEffectivelyEnabled);
        Key(h.Win, PhysicalKey.Escape);
    }

    /// <summary>
    /// File › Exit with an unsaved script asks first (T-D12) and the window stays; the dialog's X keeps the app open.
    /// Closing the window asks again (MainWindow.OnClosing), a second close while it asks opens no second dialog, and
    /// Save All saves the file and closes the window.
    /// </summary>
    [AvaloniaFact]
    public async Task FileExitAndClosingTheWindow_AskFirst_CancelStays_SaveAllSavesAndCloses()
    {
        using var h = new EditorHarness();
        ScriptEditorSettings.Of(h.S).FormatOnSave = false;
        var closed = false;
        h.Win.Closed += (_, _) => closed = true;
        EditScript(h, "PlayerController", "// unsaved");

        var file = Title(h.Win, "File");
        Click(file);
        Click(Item(file, "Exit"));
        var dlg = Assert.IsType<UnsavedChangesDialog>(h.Win.Overlays.Open.Single());
        Assert.Equal("Save changes before exiting?", dlg.GetVisualDescendants().OfType<DialogHeader>().Single().Title);
        var texts = dlg.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("1 file has changes that will be lost if you don't save it.", texts);
        Assert.Contains("PlayerController.cs", texts);
        Assert.False(closed);
        Assert.True(h.Win.IsVisible);
        // Cancel: the dialog's X; the app stays open with the file unsaved
        Click(dlg.GetVisualDescendants().OfType<DialogHeader>().Single().GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Close"));
        Assert.False(h.Win.Overlays.IsOpen);
        Assert.False(closed);
        Assert.True(h.S.Documents.HasUnsaved);
        Assert.DoesNotContain("// unsaved", Disk(h, "PlayerController"));

        // closing the window (the OS close button) asks the same; Esc is Cancel
        h.Win.Close();
        Capture.Pump();
        Assert.False(closed);
        Assert.IsType<UnsavedChangesDialog>(h.Win.Overlays.Open.Single());
        Key(h.Win, PhysicalKey.Escape);
        Assert.False(h.Win.Overlays.IsOpen);
        Assert.False(closed);

        // close again, and once more while it asks: one dialog; Save All saves and closes
        h.Win.Close();
        Capture.Pump();
        h.Win.Close();
        Capture.Pump();
        dlg = Assert.IsType<UnsavedChangesDialog>(h.Win.Overlays.Open.Single());
        Assert.False(closed);
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().Single(b => (string?)b.Content == "Save All"));
        await LauncherHarness.Until(() => closed);
        Assert.Contains("// unsaved", Disk(h, "PlayerController"));
        Assert.False(h.S.Documents.HasUnsaved);
    }

    // T-D12: Don't Save closes the window and leaves the file on disk as it was
    [AvaloniaFact]
    public async Task ClosingTheWindow_DontSave_ClosesWithoutWriting()
    {
        using var h = new EditorHarness();
        var closed = false;
        h.Win.Closed += (_, _) => closed = true;
        var before = Disk(h, "PlayerController");
        EditScript(h, "PlayerController", "// thrown away");
        h.Win.Close();
        Capture.Pump();
        var dlg = Assert.IsType<UnsavedChangesDialog>(h.Win.Overlays.Open.Single());
        Assert.False(closed);
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().Single(b => (string?)b.Content == "Don't Save"));
        await LauncherHarness.Until(() => closed);
        Assert.Equal(before, Disk(h, "PlayerController"));
    }
}
