using System.Globalization;
using System.Text;
using System.Text.Json;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Gallery;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Support;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using SkiaSharp;
using Xunit.Abstractions;

namespace AgbStudio.Editor.Tests.Shot;

/// <summary>
/// SHOT (checklist 2.5) for P10.01, P10.05, P10.07, P10.12, P10.13, P10.08: renders every component variant and the
/// Phase 10 windows headless at 100% DPI, compares them with the Figma exports and writes docs/evidence/app,
/// docs/evidence/diff and docs/evidence/shot-report.{json,md}.
/// </summary>
public class ShotTests(ITestOutputHelper log)
{
    sealed record ManifestItem(string id, string node, string set, string variant, double w, double h);

    static readonly SKColor ComponentBg = SKColor.Parse("#151619");
    static readonly SKColor FrameBg = SKColors.Black;

    static List<ManifestItem> Manifest() =>
        JsonSerializer.Deserialize<List<ManifestItem>>(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "manifest.json")))!;

    static string AppPng(string id) => Path.Combine(Repo.Evidence, "app", id + ".png");
    static string FigmaPng(string id) => Path.Combine(Repo.FigmaEvidence, id + ".png");
    static string DiffPng(string id) => Path.Combine(Repo.Evidence, "diff", id + ".png");

    [AvaloniaFact]
    public void Shot_AllComponentsAndWindows()
    {
        var manifest = Manifest().ToDictionary(m => m.id);
        var results = new List<(DiffResult R, string Set)>();
        var missing = new List<string>();

        // components (4.16) in every variant
        foreach (var item in GalleryCatalog.Items)
        {
            if (!manifest.TryGetValue(item.Id, out var m)) { missing.Add(item.Id); continue; }
            var w = (int)Math.Round(m.w);
            var h = (int)Math.Round(m.h);
            var host = new Panel { Children = { item.Create() } };
            Capture.ControlToPng(host, w, h, AppPng(item.Id), new SolidColorBrush(Color.FromRgb(0x15, 0x16, 0x19)));
            results.Add((ImageDiff.Compare(item.Id, FigmaPng(item.Id), AppPng(item.Id), DiffPng(item.Id), ComponentBg), item.Set));
        }

        // windows
        foreach (var (id, render) in Windows())
        {
            var m = manifest[id];
            render(AppPng(id), (int)Math.Round(m.w), (int)Math.Round(m.h));
            results.Add((ImageDiff.Compare(id, FigmaPng(id), AppPng(id), DiffPng(id), FrameBg), m.set + " " + m.variant));
        }

        var notRendered = manifest.Keys.Except(results.Select(r => r.R.Id)).ToList();
        var regions = new List<(string Id, string Region, double Diff)>();
        foreach (var id in new[] { "T-P01", "T-L01" })
            foreach (var (name, rect) in ChromeRegions)
                regions.Add((id, name, ImageDiff.RegionDiffPercent(FigmaPng(id), AppPng(id), rect, FrameBg)));
        WriteReport(results, notRendered, regions);
        foreach (var (r, _) in results.Where(x => x.R.LayoutOffset > 2)) log.WriteLine($"offset > 2 px: {r.Id} ({r.LayoutOffset})");
        Assert.Empty(missing);
        Assert.Empty(notRendered);
    }

    static IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P01", (path, w, h) => RenderShell(path, w, h, ThemeVariant.Dark));
        yield return ("T-L01", (path, w, h) => RenderShell(path, w, h, ThemeVariant.Light));
        yield return ("T-O07", (path, w, h) =>
        {
            using var fx = new Fixture();
            var root = fx.OpenSkyHopper();
            var files = new ProjectFileIndex(fx.Services.Project.Current!.Root).Files();
            var history = new PaletteHistory();
            foreach (var f in new[] { "player.png", "PlayerController.cs", "Platform_tiles.png" }.Reverse()) history.Touch(files.First(x => x.Name == f).Path);
            fx.Services.Set(history);
            var p = new CommandPalette(fx.Services, files);
            p.Query.Text = "pla";
            Capture.ControlToPng(p, w, h, path, Brushes.Black);
        });
        yield return ("T-D10", (path, w, h) =>
        {
            using var fx = new Fixture();
            Capture.ControlToPng(new KeyboardShortcutsDialog(fx.Services.Shortcuts), w, h, path, Brushes.Black);
        });
        yield return ("T-D12", (path, w, h) =>
        {
            var docs = new[] { new EditorDocument { Name = "Forest_01.scene", Glyph = "map", IsDirty = true }, new EditorDocument { Name = "PlayerController.cs", Glyph = "code", IsDirty = true } };
            Capture.ControlToPng(new UnsavedChangesDialog(docs), w, h, path, Brushes.Black);
        });
        foreach (var (id, menu) in Menus()) yield return (id, (path, w, h) => Capture.ControlToPng(menu(), w, h, path, Brushes.Black));
    }

    /// <summary>T-M01..T-M07 from the title bar model, T-M08 with the recent projects of the Figma board.</summary>
    static IEnumerable<(string, Func<Control>)> Menus()
    {
        var fx = new Fixture(withApp: false);
        var builder = new MenuBuilder(fx.Services.Commands, fx.Services.Shortcuts, id => id == "scene.setStart");
        foreach (var m in MenuModel.TitleBarMenus())
        {
            var def = m;
            yield return (def.Id, () => new MenuSurface { ItemsSource = builder.BuildItems(def.Entries) });
        }
        var noop = new Relay(() => { });
        IReadOnlyList<MenuEntry> recent = [new RecentEntry("Sky Hopper", "a", true, noop), new RecentEntry("Cave Quest RPG", "b", false, noop), new RecentEntry("Block Puzzle", "c", false, noop), SeparatorEntry.Instance, new CommandEntry("file.clearRecent")];
        yield return ("T-M08", () => new MenuSurface { ItemsSource = builder.BuildItems(recent) });
    }

    static void RenderShell(string path, int w, int h, ThemeVariant theme)
    {
        using var fx = new Fixture();
        fx.OpenSkyHopper();
        var app = Application.Current!;
        var before = app.RequestedThemeVariant;
        fx.Services.Preferences.Current.Theme = theme == ThemeVariant.Light ? ThemeMode.Light : ThemeMode.Dark;
        var win = new MainWindow(fx.Services) { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            if (win.CurrentPage is SceneWorkspace ws) ws.ConsoleCount = 2;
            // Figma shows Undo and Redo enabled: one step to undo and one to redo
            fx.Services.Undo.Push(new DelegateAction("a", () => { }, () => { }));
            fx.Services.Undo.Push(new DelegateAction("b", () => { }, () => { }));
            fx.Services.Undo.Undo();
            Capture.Save(Capture.Frame(win), path);
        }
        finally
        {
            win.Close();
            app.RequestedThemeVariant = before;
        }
    }

    /// <summary>Scene Editor chrome built in Phase 10 (the panel contents are Phase 11), in Figma frame coordinates.</summary>
    static readonly (string Name, SKRectI Rect)[] ChromeRegions =
    [
        ("Title Bar", SKRectI.Create(0, 0, 1440, 36)), ("Toolbar", SKRectI.Create(0, 36, 1440, 44)),
        ("Hierarchy header + search", SKRectI.Create(0, 80, 260, 74)), ("Doc tabs", SKRectI.Create(260, 80, 860, 34)),
        ("Inspector header", SKRectI.Create(1120, 80, 320, 34)), ("Bottom panel tabs", SKRectI.Create(260, 624, 860, 33)),
        ("Status bar", SKRectI.Create(0, 874, 1440, 26)),
    ];

    static void WriteReport(List<(DiffResult R, string Set)> results, List<string> notRendered, List<(string Id, string Region, double Diff)> regions)
    {
        var json = JsonSerializer.Serialize(new
        {
            generated = DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture),
            threshold = ImageDiff.Threshold,
            note = "diffPercent: pixels whose max channel difference > threshold at 0 offset; bestShiftDiffPercent: same at the best shift in [-3,3]; colourDiffPercent: pixels differing by > 8; layoutOffset: max(|shift|, size delta) in px",
            items = results.Select(x => new { x.R.Id, set = x.Set, figma = $"{x.R.FigmaWidth}x{x.R.FigmaHeight}", app = $"{x.R.AppWidth}x{x.R.AppHeight}", x.R.DiffPercent, x.R.BestShiftDiffPercent, shift = new[] { x.R.ShiftX, x.R.ShiftY }, x.R.LayoutOffset, x.R.MeanChannelError, x.R.ColourDiffPercent }),
            chromeRegions = regions.Select(r => new { r.Id, r.Region, diffPercent = r.Diff }),
            notRendered,
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(Repo.Evidence, "shot-report.json"), json);

        var md = new StringBuilder();
        md.AppendLine("# SHOT report (Phase 10)");
        md.AppendLine();
        md.AppendLine("Generated by `ShotTests.Shot_AllComponentsAndWindows` (tests/AgbStudio.Editor.Tests). Figma: `docs/evidence/figma/<id>.png` (read-only export, Shot/figma-export.mjs); app: `docs/evidence/app/<id>.png` (headless Skia, 100% DPI); diff: `docs/evidence/diff/<id>.png` (Figma | app | differing pixels in red).");
        md.AppendLine();
        md.AppendLine($"A pixel differs when a channel differs by more than {ImageDiff.Threshold}/255. Offset = best alignment shift or size difference (tolerance 2 px).");
        md.AppendLine();
        md.AppendLine("| Id | Set / variant | Figma | App | Diff % | Diff % at best shift | Shift | Offset px | Mean err |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (r, set) in results)
            md.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {r.Id} | {set} | {r.FigmaWidth}x{r.FigmaHeight} | {r.AppWidth}x{r.AppHeight} | {r.DiffPercent:0.00} | {r.BestShiftDiffPercent:0.00} | {r.ShiftX},{r.ShiftY} | {r.LayoutOffset} | {r.MeanChannelError:0.00} |"));
        var comps = results.Where(x => x.R.Id.StartsWith("C-", StringComparison.Ordinal)).Select(x => x.R).ToList();
        md.AppendLine();
        md.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Components: {comps.Count} variants, offset within 2 px: {comps.Count(r => r.LayoutOffset <= 2)}, median diff {Median(comps.Select(r => r.DiffPercent)):0.00} %, max diff {comps.Max(r => r.DiffPercent):0.00} %."));
        md.AppendLine();
        md.AppendLine("Scene Editor chrome regions (Phase 10 builds the chrome; panel contents are Phase 11 and stay empty, so whole-frame numbers for T-P01 / T-L01 include that content):");
        md.AppendLine();
        md.AppendLine("| Frame | Region | Diff % |");
        md.AppendLine("|---|---|---|");
        foreach (var r in regions) md.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {r.Id} | {r.Region} | {r.Diff:0.00} |"));
        File.WriteAllText(Path.Combine(Repo.Evidence, "shot-report.md"), md.ToString());
    }

    static double Median(IEnumerable<double> xs) { var a = xs.Order().ToArray(); return a.Length == 0 ? 0 : a[a.Length / 2]; }
}
