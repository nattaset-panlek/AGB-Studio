using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>Rect Transform, layout groups and D-pad neighbours of UI pages (ui/*.uipage).</summary>
public sealed class UiLayoutTests
{
    static UiElement El(string name, UiRect rect, params UiElement[] children) =>
        new() { Name = name, Rect = rect, Kind = UiElementKind.Panel, Children = children.ToList() };

    static UiPageFile Page(params UiElement[] children) => new() { Root = El("Canvas", UiRect.Stretch(), children) };

    static UiRect Size(int w, int h) => UiRect.TopLeft(0, 0, w, h);

    [Fact]
    public void Root_Stretches_ToTheScreen()
    {
        var p = Page();
        Assert.Equal(new UiPixelRect(0, 0, 240, 160), UiLayoutEngine.Resolve(p)[p.Root]);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 10, 20, 10, 20)]           // top left, pivot top left
    [InlineData(1, 1, 1, 1, -10, -20, 166, 124)]       // bottom right, pivot bottom right
    [InlineData(0.5, 0.5, 0.5, 0.5, 0, 0, 88, 72)]     // middle
    public void Anchor_And_Pivot_PlaceTheRect(double ax, double ay, double px, double py, int x, int y, int ex, int ey)
    {
        var e = El("A", new UiRect { AnchorMinX = ax, AnchorMaxX = ax, AnchorMinY = ay, AnchorMaxY = ay, PivotX = px, PivotY = py, X = x, Y = y, W = 64, H = 16 });
        Assert.Equal(new UiPixelRect(ex, ey, 64, 16), UiLayoutEngine.Resolve(Page(e))[e]);
    }

    [Fact]
    public void StretchedAnchors_FollowTheParent_WithInsets()
    {
        var c = El("Fill", new UiRect { AnchorMinX = 0, AnchorMaxX = 1, AnchorMinY = 0, AnchorMaxY = 1, PivotX = 0, PivotY = 0, X = 4, Y = 4, W = -8, H = -8 });
        var panel = El("Panel", UiRect.TopLeft(20, 30, 100, 50), c);
        Assert.Equal(new UiPixelRect(24, 34, 92, 42), UiLayoutEngine.Resolve(Page(panel))[c]);
    }

    [Fact]
    public void VerticalGroup_StacksWithPaddingAndSpacing_AndStretches()
    {
        UiElement a = El("A", Size(10, 12)), b = El("B", Size(10, 14));
        var list = El("List", UiRect.TopLeft(0, 0, 100, 100), a, b);
        list.Layout = new UiLayoutGroup { Kind = UiLayoutKind.Vertical, PaddingLeft = 4, PaddingTop = 6, PaddingRight = 4, Spacing = 2, StretchCross = true };
        var r = UiLayoutEngine.Resolve(Page(list));
        Assert.Equal(new UiPixelRect(4, 6, 92, 12), r[a]);
        Assert.Equal(new UiPixelRect(4, 20, 92, 14), r[b]);
    }

    [Fact]
    public void HorizontalGroup_Flexible_TakesWhatIsLeft_AndAlignCentres()
    {
        UiElement a = El("A", Size(20, 10)), b = El("B", Size(1, 10)), c = El("C", Size(30, 10));
        b.Flexible = true;
        var row = El("Row", UiRect.TopLeft(0, 0, 100, 30), a, b, c);
        row.Layout = new UiLayoutGroup { Kind = UiLayoutKind.Horizontal, Spacing = 5, AlignY = UiAlign.Center };
        var r = UiLayoutEngine.Resolve(Page(row));
        Assert.Equal(new UiPixelRect(0, 10, 20, 10), r[a]);
        Assert.Equal(new UiPixelRect(25, 10, 40, 10), r[b]);
        Assert.Equal(new UiPixelRect(70, 10, 30, 10), r[c]);
    }

    [Fact]
    public void GridGroup_WrapsByColumns()
    {
        var cells = Enumerable.Range(0, 5).Select(i => El("C" + i, Size(1, 1))).ToArray();
        var grid = El("Grid", UiRect.TopLeft(10, 10, 100, 100), cells);
        grid.Layout = new UiLayoutGroup { Kind = UiLayoutKind.Grid, Columns = 2, CellW = 20, CellH = 10, Spacing = 4, SpacingY = 2 };
        var r = UiLayoutEngine.Resolve(Page(grid));
        Assert.Equal(new UiPixelRect(10, 10, 20, 10), r[cells[0]]);
        Assert.Equal(new UiPixelRect(34, 10, 20, 10), r[cells[1]]);
        Assert.Equal(new UiPixelRect(10, 22, 20, 10), r[cells[2]]);
        Assert.Equal(new UiPixelRect(10, 34, 20, 10), r[cells[4]]);
    }

    [Fact]
    public void ContentSizeFitter_FollowsTheChildren_AndIgnoredOnesStayOut()
    {
        UiElement a = El("A", Size(40, 10)), b = El("B", Size(30, 10)), free = El("Free", UiRect.TopLeft(1, 2, 9, 9));
        free.IgnoreLayout = true;
        var fit = El("Fit", UiRect.TopLeft(0, 0, 1, 1), a, b, free);
        fit.Layout = new UiLayoutGroup { Kind = UiLayoutKind.Vertical, PaddingLeft = 2, PaddingRight = 2, PaddingTop = 3, PaddingBottom = 3, Spacing = 1, FitW = true, FitH = true };
        var r = UiLayoutEngine.Resolve(Page(fit));
        Assert.Equal(new UiPixelRect(0, 0, 44, 27), r[fit]);
        Assert.Equal(new UiPixelRect(1, 2, 9, 9), r[free]);
    }

    [Fact]
    public void AspectRatio_SetsTheHeight()
    {
        var e = El("A", UiRect.TopLeft(0, 0, 60, 1));
        e.AspectRatio = 2;
        Assert.Equal(30, UiLayoutEngine.Resolve(Page(e))[e].H);
    }

    [Fact]
    public void Neighbours_AreTheNearestThatWay_AndWrapOnlyIfAsked()
    {
        UiElement Btn(string n, int x, int y) => new() { Name = n, Kind = UiElementKind.Button, Rect = UiRect.TopLeft(x, y, 40, 16) };
        UiElement a = Btn("A", 0, 0), b = Btn("B", 0, 20), c = Btn("C", 0, 40), side = Btn("Side", 60, 20);
        var p = Page(a, b, c, side);
        var r = UiLayoutEngine.Resolve(p);
        Assert.Same(b, UiLayoutEngine.Neighbour(p, r, a, 0, 1));
        Assert.Same(side, UiLayoutEngine.Neighbour(p, r, b, 1, 0));
        Assert.Same(c, UiLayoutEngine.Neighbour(p, r, a, 0, -1));  // around to the far end
        p.WrapFocus = false;
        Assert.Null(UiLayoutEngine.Neighbour(p, r, a, 0, -1));
    }

    [Fact]
    public void PageFile_RoundTrips()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_page_json");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Bag.uipage");
        var e = El("Hp", UiRect.TopLeft(1, 2, 3, 4));
        e.Kind = UiElementKind.Slider;
        e.Amount = 40;
        e.Action = new UiAction { Kind = UiActionKind.OpenPage, Target = "Party", Data = 3 };
        e.Layout = new UiLayoutGroup { Kind = UiLayoutKind.Grid };
        GameProject.Save(path, new UiPageFile { Name = "Bag", Tag = "Menu", Root = El("Canvas", UiRect.Stretch(), e) });
        var back = GameProject.Load<UiPageFile>(path);
        Assert.Equal("Menu", back.Tag);
        var hp = back.Root.Children.Single();
        Assert.Equal(UiElementKind.Slider, hp.Kind);
        Assert.Equal(40, hp.Amount);
        Assert.Equal("Party", hp.Action.Target);
        Assert.Equal(UiLayoutKind.Grid, hp.Layout!.Kind);
        Assert.Equal(3, hp.Rect.W);
    }
}

/// <summary>UI pages in a built game: a script opens them with data, the D-pad moves, buttons open and close pages, and
/// the screen comes back when they close.</summary>
[Collection("emulator")]
public sealed class UiPageRomTests(EmulatorFixture emu)
{
    const string Fill = "#F8F0D8", Focus = "#3858C8", Ink = "#202030", White = "#F8F8F8", Bar = "#30C050";

    static UiElement Item(string name, string text, UiAction? action = null) => new()
    {
        Name = name, Kind = UiElementKind.Button, Rect = new UiRect { W = 0, H = 16 }, Text = text, TextColor = Ink,
        FocusFill = Focus, FocusTextColor = White, Action = action ?? new UiAction(), Cursor = true,
    };

    static UiElement Sized(UiElement e, int w, int h)
    {
        e.Rect.W = w;
        e.Rect.H = h;
        return e;
    }

    static GameProject CreateProject()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ui_pages"));
        SampleText.AddTo(p, "en");
        p.Settings.General.BootSplash = false;
        p.Settings.Ui.PauseMenu = false;
        p.SaveSettings();
        Directory.CreateDirectory(p.UiDir);
        GameProject.Save(Path.Combine(p.UiDir, "Main.uipage"), new UiPageFile
        {
            Name = "Main",
            Root = new UiElement
            {
                Name = "Canvas", Rect = UiRect.Stretch(),
                Children =
                {
                    new UiElement
                    {
                        Name = "Window", Rect = UiRect.TopLeft(16, 16, 96, 64), Fill = Fill, Border = Ink,
                        Layout = new UiLayoutGroup { Kind = UiLayoutKind.Vertical, PaddingLeft = 4, PaddingRight = 4, PaddingTop = 4, PaddingBottom = 4, StretchCross = true },
                        Children =
                        {
                            Item("Use", "Use"),
                            Item("Bag", "Bag", new UiAction { Kind = UiActionKind.OpenPage, Target = "Bag", Data = 9 }),
                            Item("Back", "Back", new UiAction { Kind = UiActionKind.ClosePage }),
                        },
                    },
                    new UiElement
                    {
                        Name = "Hp", Kind = UiElementKind.Slider, Interactable = false, Rect = UiRect.TopLeft(16, 88, 96, 8),
                        Fill = Ink, Accent = Bar, Amount = 50, BorderWidth = 0,
                    },
                },
            },
        });
        GameProject.Save(Path.Combine(p.UiDir, "Bag.uipage"), new UiPageFile
        {
            Name = "Bag",
            Root = new UiElement
            {
                Name = "Canvas", Rect = new UiRect { AnchorMinX = 1, AnchorMaxX = 1, AnchorMinY = 0, AnchorMaxY = 0, PivotX = 1, PivotY = 0, X = -8, Y = 8, W = 80, H = 40 }, Fill = Bar, Border = Ink,
                Children = { Sized(Item("Close", "Close", new UiAction { Kind = UiActionKind.ClosePage }), 64, 16) },
            },
        });
        File.WriteAllText(Path.Combine(p.ScriptsDir, "PageOpener.cs"), """
            using GBA;

            public class PageOpener : Behaviour
            {
                void Update()
                {
                    if (Input.Pressed(Key.Select))
                    {
                        UI.Open("Main", 7);
                        UI.SetAmount("Hp", 25);
                        if (UI.Data == 7) Debug.Log("data 7");
                    }
                    if (UI.WasChosen("Use")) Debug.Log("used");
                }
            }
            """);
        var scene = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var s = GameProject.Load<SceneFile>(scene);
        s.Actors.First(a => a.Name == "Player").Components.Add(new ComponentData { Type = "PageOpener" });
        GameProject.Save(scene, s);
        return p;
    }

    static void Press(PlaySession s, GbaKeys key)
    {
        s.Keys = key;
        s.Step();
        s.Keys = 0;
        for (var i = 0; i < 6; i++) s.Step();
    }

    static (int R, int G, int B) Pixel(LibretroCore core, int x, int y)
    {
        var c = core.Frame[y * LibretroCore.Width + x];
        return ((int)(c >> 16) & 255, (int)(c >> 8) & 255, (int)c & 255);
    }

    static void Near((int R, int G, int B) want, (int R, int G, int B) got)
    {
        Assert.True(Math.Abs(want.R - got.R) < 12 && Math.Abs(want.G - got.G) < 12 && Math.Abs(want.B - got.B) < 12, $"wanted {want}, got {got}");
    }

    [Fact]
    public void ScriptOpensAPage_DpadMoves_ButtonsOpenAndClose_AndTheSceneComesBack()
    {
        var p = CreateProject();
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var shots = Path.Combine(p.BuildDir, "ui-shots");
        Directory.CreateDirectory(shots);
        void Shot(PlaySession s, string name) => AgbStudio.Core.Png.WriteArgb(Path.Combine(shots, name + ".png"), (uint[])s.Core.Frame.Clone(), LibretroCore.Width, LibretroCore.Height);

        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 90; i++) s.Step();
        var before = Pixel(s.Core, 30, 30);

        Press(s, GbaKeys.Select);
        for (var i = 0; i < 4; i++) s.Step();
        Shot(s, "page-main");
        Assert.Contains(s.Log, l => l.Contains("UI page Main opened", StringComparison.Ordinal));
        Assert.Contains(s.Log, l => l.Contains("data 7", StringComparison.Ordinal));
        Near((248, 240, 216), Pixel(s.Core, 100, 70));  // the window's fill, below its rows
        Near((56, 88, 200), Pixel(s.Core, 100, 25));    // the first row has the focus
        Near((48, 192, 80), Pixel(s.Core, 30, 92));     // the bar: the script set 25 percent of 96 px
        Near((32, 32, 48), Pixel(s.Core, 100, 92));

        Press(s, GbaKeys.Down);
        Shot(s, "page-main-down");
        Near((248, 240, 216), Pixel(s.Core, 100, 25));
        Near((56, 88, 200), Pixel(s.Core, 100, 40));

        // A button opens a second page over the first, with the data the button holds.
        Press(s, GbaKeys.A);
        for (var i = 0; i < 4; i++) s.Step();
        Shot(s, "page-bag");
        Assert.Contains(s.Log, l => l.Contains("UI page Bag opened", StringComparison.Ordinal));
        Near((48, 192, 80), Pixel(s.Core, 225, 40));    // anchored to the top right
        Near((56, 88, 200), Pixel(s.Core, 100, 40));    // the page under it still shows
        Press(s, GbaKeys.B);
        Assert.Contains(s.Log, l => l.Contains("UI page Bag closed", StringComparison.Ordinal));
        for (var i = 0; i < 4; i++) s.Step();
        Near((56, 88, 200), Pixel(s.Core, 100, 40));

        // A closing button, then the scene runs again and the script hears nothing it should not.
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.A);
        Assert.Contains(s.Log, l => l.Contains("UI page Main closed", StringComparison.Ordinal));
        for (var i = 0; i < 6; i++) s.Step();
        Near(before, Pixel(s.Core, 30, 30));
        Assert.DoesNotContain(s.Log, l => l.Contains("used", StringComparison.Ordinal));

        // A button with no action: the page stays, and the script reads the choice when the scene runs again.
        Press(s, GbaKeys.Select);
        for (var i = 0; i < 4; i++) s.Step();
        Press(s, GbaKeys.A);
        Press(s, GbaKeys.B);
        for (var i = 0; i < 6; i++) s.Step();
        Assert.Contains(s.Log, l => l.Contains("used", StringComparison.Ordinal));
    }
}
