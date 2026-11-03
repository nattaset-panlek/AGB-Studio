using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Shell;

public sealed class OverlayHostTests : IDisposable
{
    readonly Fixture _fx = new();
    public void Dispose() => _fx.Dispose();

    [AvaloniaFact]
    public void OverlayHost_DialogHasTranslateTransformAndCanBeDragged()
    {
        _fx.OpenSkyHopper();
        var win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        Capture.Pump();

        var dialog = new KeyboardShortcutsDialog(_fx.Services.Shortcuts, _fx.Services.Commands);
        _ = win.Overlays.Show(dialog);
        Capture.Pump();

        Assert.True(win.Overlays.IsOpen);
        var tt = dialog.RenderTransform as TranslateTransform;
        Assert.NotNull(tt);
        Assert.Equal(0, tt.X);
        Assert.Equal(0, tt.Y);

        var header = dialog.GetVisualDescendants().OfType<DialogHeader>().Single();
        var p1 = header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), win)!.Value;
        var p2 = new Point(p1.X + 60, p1.Y + 40);

        win.MouseMove(p1);
        win.MouseDown(p1, MouseButton.Left);
        win.MouseMove(p2);
        win.MouseUp(p2, MouseButton.Left);
        Capture.Pump();

        Assert.True(tt.X > 0, $"Expected tt.X > 0, got {tt.X}");
        Assert.True(tt.Y > 0, $"Expected tt.Y > 0, got {tt.Y}");

        var curP = header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), win)!.Value;
        win.MouseMove(curP);
        win.MouseDown(curP, MouseButton.Left);
        win.MouseUp(curP, MouseButton.Left);
        win.MouseDown(curP, MouseButton.Left);
        win.MouseUp(curP, MouseButton.Left);
        Capture.Pump();

        Assert.Equal(0, tt.X);
        Assert.Equal(0, tt.Y);

        win.Overlays.Close(dialog);
        Capture.Pump();
        Assert.False(win.Overlays.IsOpen);
    }

    [AvaloniaFact]
    public void OverlayHost_CloseButton_ClosesWithoutDragging()
    {
        _fx.OpenSkyHopper();
        var win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        Capture.Pump();

        var dialog = new KeyboardShortcutsDialog(_fx.Services.Shortcuts, _fx.Services.Commands);
        _ = win.Overlays.Show(dialog);
        Capture.Pump();

        Assert.True(win.Overlays.IsOpen);
        var closeBtn = dialog.GetVisualDescendants().OfType<IconButton>().Single(b => b.Name == "PART_Close");
        var p = closeBtn.TranslatePoint(new Point(closeBtn.Bounds.Width / 2, closeBtn.Bounds.Height / 2), win)!.Value;

        win.MouseMove(p);
        win.MouseDown(p, MouseButton.Left);
        win.MouseUp(p, MouseButton.Left);
        Capture.Pump();

        Assert.False(win.Overlays.IsOpen);
    }
}
