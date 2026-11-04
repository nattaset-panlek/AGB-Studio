using AgbStudio.Core.Project;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.Tilemap;
using AgbStudio.Editor.Windows.UiEditor;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace AgbStudio.Editor.Tests.Windows.UiEditor;

/// <summary>Smart guides of the UI Editor: edges and centres of the screen and of other elements.</summary>
public sealed class UiSnapTests : IDisposable
{
    static readonly UiSnapEdges Move = new(true, true, true, true);
    static readonly UiSnapTarget Screen = new(0, 0, 240, 160);
    readonly PageHarness _h = PageHarness.ForFonts();

    public void Dispose() => _h.Dispose();

    [AvaloniaFact]
    public void Move_SnapsItsCentreToTheScreenCentre()
    {
        var (dx, dy, guides) = UiSnap.Solve(new UiPixelRect(10, 10, 40, 20), 88, 49, Move, [Screen], 3);
        Assert.Equal((90, 50), (dx, dy));
        Assert.Contains(new UiGuide(true, 120, 0, 160), guides);
        Assert.Contains(new UiGuide(false, 80, 0, 240), guides);
    }

    [AvaloniaFact]
    public void Move_SnapsToTheEdgesAndCentreOfAnotherElement()
    {
        var other = new UiSnapTarget(100, 20, 140, 40);
        Assert.Equal(90, UiSnap.Solve(new UiPixelRect(10, 100, 20, 10), 88, 0, Move, [other], 3).Dx);   // left edges meet
        Assert.Equal(100, UiSnap.Solve(new UiPixelRect(10, 100, 20, 10), 101, 0, Move, [other], 3).Dx); // centres meet
        Assert.Equal(110, UiSnap.Solve(new UiPixelRect(10, 100, 20, 10), 109, 0, Move, [other], 3).Dx); // right edges meet
    }

    [AvaloniaFact]
    public void FarFromEveryLine_StaysWhereThePointerIs_WithNoGuides()
    {
        var (dx, dy, guides) = UiSnap.Solve(new UiPixelRect(10, 10, 40, 20), 20, 15, Move, [Screen], 3);
        Assert.Equal((20, 15), (dx, dy));
        Assert.Empty(guides);
    }

    [AvaloniaFact]
    public void OddSizedCentre_DoesNotSnapToALineItCannotReachInWholePixels()
    {
        var (dx, _, guides) = UiSnap.Solve(new UiPixelRect(0, 0, 41, 20), 100, 7, Move, [new UiSnapTarget(120, 200, 120, 200)], 1);
        Assert.Equal(100, dx);
        Assert.DoesNotContain(guides, g => g.Vertical);
    }

    [AvaloniaFact]
    public void Resize_SnapsOnlyTheEdgeBeingDragged()
    {
        var right = new UiSnapEdges(false, true, false, false);
        var (dx, _, guides) = UiSnap.Solve(new UiPixelRect(10, 10, 40, 20), 188, 0, right, [Screen], 3);
        Assert.Equal(190, dx);
        Assert.Equal(240, Assert.Single(guides).At);
        Assert.Equal(20, UiSnap.Solve(new UiPixelRect(10, 10, 100, 20), 20, 0, right, [Screen], 3).Dx); // its centre does not count
    }

    [AvaloniaFact]
    public void DraggingOnTheCanvas_ShowsGuidesAndLandsOnTheScreenCentre_AltDragsFreely()
    {
        _h.S.Commands.Execute("tools.uiEditor");
        _h.Pump();
        var page = Assert.IsType<UiEditorPage>(_h.Win.CurrentPage);
        page.Library.NewPage();
        page.Library.AddElement(UiElementKind.Panel);   // at 8,8, 96 x 48: its centre is 56,32
        _h.Pump();
        var c = page.Canvas;
        var s = c.Scale;
        var origin = c.TranslatePoint(new Point((c.Bounds.Width - 240 * s) / 2, (c.Bounds.Height - 160 * s) / 2), _h.Win)!.Value;
        Point At(int x, int y) => new(origin.X + x * s, origin.Y + y * s);

        _h.ClickAt(At(40, 30));
        _h.Win.MouseDown(At(40, 30), MouseButton.Left);
        _h.Win.MouseMove(At(40 + 63, 30 + 47));         // one pixel short of the centre on both axes
        _h.Pump();
        Assert.Contains(c.Guides, g => g.Vertical && g.At == 120);
        Assert.Contains(c.Guides, g => !g.Vertical && g.At == 80);
        _h.Win.MouseUp(At(40 + 63, 30 + 47), MouseButton.Left);
        _h.Pump();
        Assert.Empty(c.Guides);
        var r = UiLayoutEngine.Resolve(page.Library.Page!)[page.Library.Selected!];
        Assert.Equal((120, 80), (r.X + r.W / 2, r.Y + r.H / 2));

        _h.Win.MouseDown(At(120, 80), MouseButton.Left);
        _h.Win.MouseMove(At(121, 81), RawInputModifiers.Alt);
        _h.Pump();
        Assert.Empty(c.Guides);
        _h.Win.MouseUp(At(121, 81), MouseButton.Left);
        _h.Pump();
        r = UiLayoutEngine.Resolve(page.Library.Page!)[page.Library.Selected!];
        Assert.Equal((121, 81), (r.X + r.W / 2, r.Y + r.H / 2));
    }
}
