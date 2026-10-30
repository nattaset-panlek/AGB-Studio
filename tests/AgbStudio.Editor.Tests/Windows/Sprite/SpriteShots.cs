using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Palette;
using AgbStudio.Editor.Windows.Sprite;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;

namespace AgbStudio.Editor.Tests.Windows.Sprite;

/// <summary>SHOT renders of the Sprite Editor and Palette Manager group (T-P08, T-D13, T-M19, T-P10, T-O10) with the Figma fixture state.</summary>
public sealed class SpriteShots : IShotSource
{
    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P08", (path, w, h) => RenderPage(path, w, h, "T-P08"));
        yield return ("T-P10", (path, w, h) => RenderPage(path, w, h, "T-P10"));
        yield return ("T-D13", (path, w, h) =>
        {
            using var fx = new Fixture();
            var lib = ArtFixture.Open(fx);
            var dlg = new NewSpriteDialog(lib, ArtFixture.SpritePath(lib, "slime"));
            dlg.NameField.Text = "enemy_bat";
            var win = Capture.Host(dlg, w, h, Brushes.Black);
            try
            {
                dlg.NameField.GetTemplateChildrenOf<TextBox>().First().Focus();
                Capture.Save(Capture.Frame(win), path);
            }
            finally { win.Close(); }
        });
        yield return ("T-M19", (path, w, h) =>
        {
            using var fx = new Fixture();
            var lib = ArtFixture.Open(fx);
            var builder = new MenuBuilder(fx.Services.Commands, fx.Services.Shortcuts);
            var menu = new MenuSurface { ItemsSource = builder.BuildItems(OamSizeMenu.For(new SpriteDoc(lib, ArtFixture.SpritePath(lib, "hero")))) };
            Capture.ControlToPng(menu, w, h, path, Brushes.Black);
        });
        yield return ("T-O10", (path, w, h) =>
        {
            using var fx = new Fixture();
            var lib = ArtFixture.Open(fx);
            Capture.ControlToPng(new PaletteEffectPopover(lib, EffectKind.Cycle), w, h, path, Brushes.Black);
        });
    }

    /// <summary>The main window at the Figma size with the page open in the fixture state of the frame.</summary>
    public static void RenderPage(string path, int w, int h, string id)
    {
        using var fx = new Fixture();
        var lib = ArtFixture.Open(fx);
        ArtFixture.AddSceneDocument(fx);
        var win = new MainWindow(fx.Services, startPage: null) { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            if (id == "T-P08")
            {
                _ = fx.Services.Windows.Open("T-P08", ArtFixture.SpritePath(lib, "hero"));
                var page = (SpriteEditorPage)win.CurrentPage!;
                page.Open(ArtFixture.SpritePath(lib, "slime"));
                page.Open(ArtFixture.SpritePath(lib, "hero"));
                page.SelectFrame(2);
                page.SelectColor(4);
                page.SetOnion(true);
                Capture.Pump();
                var cell = page.Canvas.CellCenter(6, 3);
                win.MouseMove(page.Canvas.TranslatePoint(cell, win)!.Value);
            }
            else
            {
                _ = fx.Services.Windows.Open("T-P10", lib.Find(BankKind.Obj, 0));
                ((PaletteManagerPage)win.CurrentPage!).SelectIndex(4);
            }
            // Figma shows Undo and Redo enabled
            fx.Services.Undo.Push(new DelegateAction("a", () => { }, () => { }));
            fx.Services.Undo.Push(new DelegateAction("b", () => { }, () => { }));
            fx.Services.Undo.Undo();
            Capture.Save(Capture.Frame(win), path);
        }
        finally { win.Close(); }
    }
}

static class TemplateExtensions
{
    public static IEnumerable<T> GetTemplateChildrenOf<T>(this Control c) where T : Control =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<T>();
}
