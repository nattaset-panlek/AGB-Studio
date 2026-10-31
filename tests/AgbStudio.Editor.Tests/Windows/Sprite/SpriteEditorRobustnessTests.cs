using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Palette;
using AgbStudio.Editor.Windows.Sprite;
using Avalonia.Headless.XUnit;

namespace AgbStudio.Editor.Tests.Windows.Sprite;

/// <summary>The pages with no project, and with a project whose sprite files cannot be read.</summary>
public class SpriteEditorRobustnessTests
{
    [AvaloniaFact]
    public void Pages_OpenWithoutAProjectAndWithUnreadableArt()
    {
        using var fx = new Fixture();
        var win = new MainWindow(fx.Services);
        try
        {
            win.Show();
            // no project: the pages show their frame, the commands do not throw
            _ = fx.Services.Windows.Open("T-P08");
            Assert.IsType<SpriteEditorPage>(win.CurrentPage);
            _ = fx.Services.Windows.Open("T-P10");
            Assert.IsType<PaletteManagerPage>(win.CurrentPage);
            fx.Services.Commands.Execute("assets.optimizePalettes");
            fx.Services.Commands.Execute("assets.create.palette");
            Capture.Pump();

            // the fixture adds an empty player.png next to the Sky Hopper art: it is listed but reported when opened
            var root = fx.OpenSkyHopper();
            var page = new SpriteEditorPage(fx.Services, Path.Combine(root, "assets", "sprites", "player.png"));
            Assert.Null(page.Doc);
            Assert.Contains(fx.Services.Toasts.Visible, t => t.Status == ToastStatus.Error && t.Text.Contains("player.png"));
            Assert.NotNull(new PaletteManagerPage(fx.Services).Library);
        }
        finally { win.Close(); }
    }
}
