using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Font;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia.Controls;
using Avalonia.Media;

namespace AgbStudio.Editor.Tests.Windows.Tilemap;

/// <summary>
/// SHOT for the Tilemap Editor and Font Manager group (11.E, 11.G): T-P03, T-P26, T-M20, T-P09, T-D15, T-D16 rendered
/// at the Figma size with the real Sky Hopper project, in the state each Figma frame shows.
/// </summary>
public sealed class TilemapFontShots : IShotSource
{
    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P03", RenderTilemap);
        yield return ("T-P26", (path, w, h) =>
        {
            using var fx = new Fixture();
            SkyHopperContent.OpenForTilemap(fx);
            var session = FigmaState(fx);
            Capture.ControlToPng(new TilemapBottomPanel(session, fx.Services, 1), w, h, path, Brushes.Black);
        });
        yield return ("T-M20", (path, w, h) =>
        {
            using var fx = new Fixture();
            SkyHopperContent.OpenForTilemap(fx);
            MapLibrary.Of(fx.Services).Current(null);
            var builder = new MenuBuilder(fx.Services.Commands, fx.Services.Shortcuts);
            Capture.ControlToPng(new MenuSurface { ItemsSource = builder.BuildItems(AddLayerMenu.Entries()) }, w, h, path, Brushes.Black);
        });
        yield return ("T-P09", RenderFonts);
        yield return ("T-D15", (path, w, h) =>
        {
            using var fx = new Fixture();
            SkyHopperContent.OpenForFonts(fx);
            Capture.ControlToPng(new NewFontDialog(FontLibrary.Of(fx.Services), fx.Services.Project.Current), w, h, path, Brushes.Black);
        });
        yield return ("T-D16", (path, w, h) =>
        {
            using var fx = new Fixture();
            SkyHopperContent.OpenForFonts(fx);
            var dlg = new AddCharactersDialog(FontLibrary.Of(fx.Services).Resolve(null)!);
            dlg.Text.Text = FigmaPaste;
            var win = Capture.Host(dlg, w, h, Brushes.Black);
            try
            {
                dlg.Text.Focus();
                Capture.Save(Capture.Frame(win), path);
            }
            finally { win.Close(); }
        });
    }

    /// <summary>The text pasted in the Figma Add Characters frame.</summary>
    public const string FigmaPaste = "ฮอปเปอร์: เหรียญหายไปไหนหมดนะ?\nนกฮูก: เจ้าสไลม์ขโมยไปไว้ในถ้ำน่ะสิ! ฿100 · ½ · ©";

    static void RenderFonts(string path, int w, int h)
    {
        using var fx = new Fixture();
        SkyHopperContent.OpenForFonts(fx);
        fx.Services.Documents.Active = fx.Services.Documents.Open.First(d => d.Name == "thai_vwf.font");
        var win = new MainWindow(fx.Services, "T-P09") { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            fx.Services.Undo.Push(new AgbStudio.Editor.Services.DelegateAction("a", () => { }, () => { }));
            fx.Services.Undo.Push(new AgbStudio.Editor.Services.DelegateAction("b", () => { }, () => { }));
            fx.Services.Undo.Undo();
            Capture.Save(Capture.Frame(win), path);
        }
        finally { win.Close(); }
    }

    /// <summary>
    /// The Tilemap Editor state of the Figma frame, reached with the editor's own actions: Forest_01.map with a
    /// "Foreground" tile layer on BG3 (x1.20, hidden), the camera scrolled to the ground, collision view on,
    /// a 2 x 1 grass_top stamp, the pointer over a cell.
    /// </summary>
    internal static TilemapSession FigmaState(Fixture fx)
    {
        var session = MapLibrary.Of(fx.Services).Current(null)!;
        var bg3 = session.Doc.AddLayer(LayerType.Tile, "Foreground");
        session.Doc.Map.Layers[bg3].ParallaxX = session.Doc.Map.Layers[bg3].ParallaxY = 1.2;
        session.SetVisible(bg3, false);
        session.Layer = session.Doc.Map.Layers.FindIndex(l => l.Name == "Ground");
        session.CollisionView = true;
        session.SetCamera(0, session.Doc.Height * 16 - TilemapSession.ScreenH);
        session.SelectTile(1, 2, 1);
        session.Hover = session.CellAt(12 * 16 + 8, 4 * 16 + 8);
        fx.Services.Undo.Clear();
        session.Doc.Save();
        return session;
    }

    static void RenderTilemap(string path, int w, int h)
    {
        using var fx = new Fixture();
        SkyHopperContent.OpenForTilemap(fx);
        fx.Services.Documents.Active = fx.Services.Documents.Open.First(d => d.Name == "Forest_01.map");
        FigmaState(fx);
        var win = new MainWindow(fx.Services, "T-P03") { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            fx.Services.Undo.Push(new AgbStudio.Editor.Services.DelegateAction("a", () => { }, () => { }));
            fx.Services.Undo.Push(new AgbStudio.Editor.Services.DelegateAction("b", () => { }, () => { }));
            fx.Services.Undo.Undo();
            Capture.Save(Capture.Frame(win), path);
        }
        finally { win.Close(); }
    }
}
