using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>
/// SHOT sources of 11.C (Scene Editor group): the Console and Animation panels cut from the Scene Editor page as the
/// Figma frames show them (bottom panel with its tab row), the New Scene and Scene Settings dialogs, the six popovers and
/// the menus T-M09..T-M16, all with the Sky Hopper fixture project. T-P01 / T-L01 are rendered by ShotTests itself.
/// </summary>
public sealed class SceneEditorShots : IShotSource
{
    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P05", (path, w, h) => BottomPanel(path, w, h, "Tab_Console", fx =>
        {
            var log = ConsoleLog.Of(fx.Services);
            log.Clear();
            var t = new DateTime(2026, 10, 31, 12, 4, 31);
            void Add(int s, ConsoleLevel l, string text, string? file = null, int line = 0) { log.Clock = () => t.AddSeconds(s); log.Add(l, text, file, line); }
            // a play session of the Figma frame: build, scene load, game logs
            Add(0, ConsoleLevel.Success, "Build OK in 0.80 s · sky_hopper.gba 412 KB");
            Add(0, ConsoleLevel.Info, "Scene Forest_01 loaded (BG 3 · OBJ 9)");
            Add(5, ConsoleLevel.Info, "Player.OnTouch(Coin) → score 110");
            Add(8, ConsoleLevel.Warning, "Palette OBJ 1 uses 15/16 colours");
            Add(21, ConsoleLevel.Error, "Slime.cs(42): NullReference — target was destroyed", Path.Combine(fx.Root, "scripts", "Slime.cs"), 42);
            Add(22, ConsoleLevel.Info, "Debug.Log: jumpPower = 3.50");
        }));
        yield return ("T-P06", (path, w, h) => BottomPanel(path, w, h, "Tab_Animation", _ => { }, win =>
        {
            // the Figma frame shows onion skin on
            win.GetVisualDescendants().OfType<AnimationPanel>().Single().OnionSkin = true;
        }));
        yield return ("T-D02", (path, w, h) => WithScene(fx =>
        {
            var d = new NewSceneDialog(fx.Services);
            var host = Capture.Host(d, w, h, Brushes.Black);
            d.SceneName.GetVisualDescendants().OfType<TextBox>().First().Focus();
            Capture.Save(Capture.Frame(host), path);
            host.Close();
        }));
        yield return ("T-D03", (path, w, h) => WithScene(fx => Capture.ControlToPng(new SceneSettingsDialog(fx.Services), w, h, path, Brushes.Black)));
        yield return ("T-O01", (path, w, h) => WithScene(fx => Capture.ControlToPng(new SceneSwitcher(fx.Services), w, h, path, Brushes.Black)));
        yield return ("T-O02", (path, w, h) => WithScene(fx =>
        {
            // the Figma frame shows alpha blending on
            var m = SceneEditorModel.Of(fx.Services);
            m.EditScene("Alpha Blending", (s, _) => s.AlphaBlending = true);
            Capture.ControlToPng(new VideoModePopover(fx.Services), w, h, path, Brushes.Black);
        }));
        yield return ("T-O03", (path, w, h) => WithScene(fx => Capture.ControlToPng(new GridSnapPopover(fx.Services), w, h, path, Brushes.Black)));
        yield return ("T-O04", (path, w, h) => WithScene(fx => Capture.ControlToPng(new AssetPicker(fx.Services, "Sprites"), w, h, path, Brushes.Black)));
        yield return ("T-O05", (path, w, h) => WithScene(fx =>
        {
            var p = new ComponentPicker(fx.Services);
            p.Highlight = p.Items.ToList().FindIndex(b => b.Name == "Patrol");
            Capture.ControlToPng(p, w, h, path, Brushes.Black);
        }));
        yield return ("T-O06", (path, w, h) => WithScene(fx => Capture.ControlToPng(new BudgetDetails(fx.Services), w, h, path, Brushes.Black)));
        foreach (var id in new[] { "T-M09", "T-M10", "T-M11", "T-M12", "T-M13", "T-M14", "T-M15", "T-M16" })
        {
            var menu = id;
            yield return (menu, (path, w, h) => WithScene(fx =>
            {
                var builder = new MenuBuilder(fx.Services.Commands, fx.Services.Shortcuts);
                var ctx = new WindowContext { Services = fx.Services, Info = fx.Services.Windows.Info(menu) };
                var m = SceneEditorModel.Of(fx.Services);
                IReadOnlyList<MenuEntry> entries = menu switch
                {
                    "T-M09" => MenuModel.Create(),
                    "T-M10" => MenuModel.AddActor(),
                    "T-M11" => SceneCommands.AddToScene(ctx),
                    "T-M12" => SceneCommands.ActorContextMenu(ctx),
                    "T-M13" => SceneCommands.AssetContextMenu(new WindowContext { Services = fx.Services, Info = ctx.Info, Args = AssetActions.Of(m).Files(AssetCategory.Sprites)[0] }),
                    "T-M14" => SceneCommands.ComponentMenu(),
                    "T-M15" => SceneCommands.ZoomMenu(ctx),
                    _ => SceneCommands.AnimationList(ctx),
                };
                Capture.ControlToPng(new MenuSurface { ItemsSource = builder.BuildItems(entries) }, w, h, path, Brushes.Black);
            }));
        }
    }

    static void WithScene(Action<SceneFixture> render)
    {
        using var fx = new SceneFixture();
        render(fx);
    }

    /// <summary>The bottom panel (260, 624, 860 x 250) of the Scene Editor page with a tab selected.</summary>
    static void BottomPanel(string path, int w, int h, string tab, Action<SceneFixture> setup, Action<MainWindow>? after = null)
    {
        using var fx = new SceneFixture();
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            Capture.Pump();
            setup(fx);
            var t = win.GetVisualDescendants().OfType<PanelTab>().First(p => p.Name == tab);
            var c = t.TranslatePoint(new Point(t.Bounds.Width / 2, t.Bounds.Height / 2), win)!.Value;
            win.MouseDown(c, MouseButton.Left);
            win.MouseUp(c, MouseButton.Left);
            win.MouseMove(new Point(2, 890));
            Capture.Pump();
            after?.Invoke(win);
            Capture.Pump();
            var frame = Capture.Frame(win);
            using var crop = new RenderTargetBitmap(new PixelSize(w, h));
            using (var dc = crop.CreateDrawingContext())
                dc.DrawImage(frame, new Rect(260, 624, w, h), new Rect(0, 0, w, h));
            Capture.Save(crop, path);
        }
        finally
        {
            win.Close();
        }
    }
}
