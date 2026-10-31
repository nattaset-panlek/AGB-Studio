using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Menus;
using AgbStudio.Editor.Windows.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>SHOT renders of the settings group: Project Settings (6 tabs), Preferences (5 tabs + Light), About, Record Shortcut.</summary>
public sealed class SettingsShots : IShotSource
{
    static Fixture NewFixture()
    {
        var fx = new Fixture();
        fx.OpenFigmaSkyHopper();
        fx.Services.Toolchain.CheckNow();
        var p = fx.Services.Preferences.Current;
        // Figma Preferences · Paths values
        p.DevkitPro = @"D:\devkitPro";
        p.ProjectsFolder = @"D:\GBAProjects";
        p.ImageEditorPath = @"C:\Program Files\Aseprite\Aseprite.exe";
        return fx;
    }

    static void Render(Control c, int w, int h, string path, ThemeVariant? theme = null, Action? before = null)
    {
        var host = Capture.Host(c, w, h, Brushes.Black, theme);
        try
        {
            before?.Invoke();
            Capture.Pump();
            Capture.Save(Capture.Frame(host), path);
        }
        finally { host.Close(); }
    }

    static bool FigmaPaths(string path) => path.Length > 0 && !path.Contains("Tiled", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        foreach (var tab in new[] { "T-D08", "T-S01", "T-S02", "T-S03", "T-S04", "T-S05" })
        {
            var id = tab;
            yield return (id, (path, w, h) =>
            {
                using var fx = NewFixture();
                var d = new ProjectSettingsDialog(fx.Services, id);
                Render(d, w, h, path, before: () =>
                {
                    // Figma shows the Game title box focused
                    if (id == "T-S01") d.GetVisualDescendants().OfType<TextBox>().First().Focus();
                });
            });
        }
        foreach (var tab in new[] { "T-D09", "T-S06", "T-S07", "T-S08", "T-S09" })
        {
            var id = tab;
            yield return (id, (path, w, h) =>
            {
                using var fx = NewFixture();
                if (id == "T-S09") fx.Services.Preferences.Current.Shortcuts["script.goToLine"] = "G";  // the Figma conflict
                Render(new PreferencesDialog(fx.Services, id, FigmaPaths), w, h, path);
            });
        }
        yield return ("T-L03", (path, w, h) =>
        {
            using var fx = NewFixture();
            var app = Application.Current!;
            var before = app.RequestedThemeVariant;
            fx.Services.Preferences.Current.Theme = ThemeMode.Light;
            app.RequestedThemeVariant = ThemeVariant.Light;
            try
            {
                var d = new PreferencesDialog(fx.Services, "T-S06", FigmaPaths);
                // Figma: the app is light and the Dark card is picked but not saved yet
                d.Draft.Theme = ThemeMode.Dark;
                d.Select("T-S06");
                Render(d, w, h, path, ThemeVariant.Light);
            }
            finally { app.RequestedThemeVariant = before; }
        });
        yield return ("T-D11", (path, w, h) =>
        {
            using var fx = new Fixture();
            fx.Services.Set(new EditorLinks { Config = new EditorConfig { WebsiteUrl = "https://example.invalid/agb-studio" }, Open = _ => { } });
            Render(new AboutDialog(fx.Services, About.Version, new DateTime(2026, 10, 31)), w, h, path);
        });
        yield return ("T-O12", (path, w, h) =>
        {
            using var fx = new Fixture();
            var sc = fx.Services.Shortcuts;
            var req = new RecordShortcutRequest("scene.toggleGrid", "Toggle grid", sc.GestureOf("scene.toggleGrid"), g => sc.FindConflicts("scene.toggleGrid", g));
            var pop = new RecordShortcutPopover(req);
            pop.Record(Key.G, KeyModifiers.Control);
            Render(pop, w, h, path);
        });
    }
}
