using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Settings;
using AgbStudio.Editor.Windows.Menus;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Menus;

/// <summary>P11.63: About (T-D11) and Keyboard Shortcuts (T-D10) from the Help menu.</summary>
public sealed class AboutTests : IDisposable
{
    readonly Fixture _fx = new();
    readonly List<string> _opened = [];
    MainWindow? _win;

    public void Dispose()
    {
        _win?.Close();
        _fx.Dispose();
    }

    MainWindow Shell(EditorConfig config)
    {
        _fx.Services.Set(new EditorLinks { Config = config, Open = _opened.Add, Fetch = (_, _) => throw new InvalidOperationException("no network in tests") });
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
        return _win;
    }

    [AvaloniaFact]
    public void HelpAbout_ShowsVersionDateCreditsLicensesAndWebsite()
    {
        var win = Shell(new EditorConfig { WebsiteUrl = "https://agb.example/studio" });
        _fx.Services.Commands.Execute("help.about");
        Capture.Pump();
        var about = Assert.IsType<AboutDialog>(win.Overlays.Open.Single());
        Assert.Equal(("T-D11", "help.about"), _fx.Services.Windows.Requests[^1]);
        Assert.Equal((460, 304), ((int)about.Bounds.Width, (int)about.Bounds.Height));
        Assert.Equal("1.0.0", About.Version);   // Directory.Build.props
        Assert.Equal($"Version {About.Version} · {About.BuildDate:yyyy-MM-dd}", about.VersionLine);
        var texts = about.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("AGB Studio", texts);
        Assert.Contains(AboutDialog.Credits, texts);
        Assert.Contains("About AGB Studio", about.GetVisualDescendants().OfType<DialogHeader>().Select(h => h.Title));

        win.Click(about.LicensesButton);
        var notices = Assert.Single(_opened);
        Assert.EndsWith("THIRD_PARTY.md", notices);
        var components = About.LicensedComponents(notices);
        foreach (var c in new[] { "mGBA libretro core", "libtonc (Tonc library)", "maxmod (libmm)", "Roslyn (Microsoft.CodeAnalysis)", "Avalonia", "Inter", "Noto Sans Thai" })
            Assert.Contains(c, components);

        Assert.True(about.WebsiteButton.IsEnabled);
        win.Click(about.WebsiteButton);
        Assert.Equal("https://agb.example/studio", _opened[^1]);
        win.Click(about.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm"));
        Assert.False(win.Overlays.IsOpen);
    }

    [AvaloniaFact]
    public void Website_IsDisabledWhenNoUrlIsConfigured()
    {
        var win = Shell(new EditorConfig());
        _fx.Services.Commands.Execute("help.about");
        Capture.Pump();
        var about = Assert.IsType<AboutDialog>(win.Overlays.Open.Single());
        Assert.False(about.WebsiteButton.IsEnabled);
    }

    [AvaloniaFact]
    public void EditorJson_IsReadWithItsD13Keys()
    {
        var path = Path.Combine(_fx.Folder, "editor.json");
        File.WriteAllText(path, """{ "bugUrl": "https://bugs.example/new", "updateFeed": "https://feed.example/latest.json", "websiteUrl": "https://agb.example" }""");
        var c = EditorConfig.Load(path);
        Assert.Equal(("https://bugs.example/new", "https://feed.example/latest.json", "https://agb.example"), (c.BugUrl, c.UpdateFeed, c.WebsiteUrl));
        Assert.Equal("", EditorConfig.Load(Path.Combine(_fx.Folder, "missing.json")).BugUrl);
    }

    [AvaloniaFact]
    public void KeyboardShortcuts_GroupsAndSearch()
    {
        var win = Shell(new EditorConfig());
        win.Key(PhysicalKey.Slash, RawInputModifiers.Control);
        var dlg = Assert.IsType<KeyboardShortcutsDialog>(win.Overlays.Open.Single());
        // P11.63 / T-D10: every command with a shortcut, by group; the Figma rows come first
        var groups = dlg.GroupTitles.ToList();
        Assert.Equal("GENERAL", groups[0]);
        Assert.Contains("SCENE", groups);
        Assert.Contains("SCRIPT", groups);
        var s = win.Services;
        var bound = s.Commands.All.Where(c => c.Context != AgbStudio.Editor.Commands.ShortcutContext.Launcher && s.Shortcuts.GestureOf(c.Id) != null).Select(c => c.Id).ToList();
        Assert.True(bound.Count > 20);
        Assert.All(bound, id => Assert.Contains(dlg.AllShortcuts, r => r.Command == id));
        Assert.Equal(KeyboardShortcutsDialog.Rows.Take(5).Select(r => r.Label), dlg.VisibleLabels.Take(5));
        Assert.Contains("Toggle breakpoint", dlg.VisibleLabels);
        Assert.Contains("Find", dlg.VisibleLabels);
        dlg.Search.Text = "Ctrl+B";
        Capture.Pump();
        Assert.Equal(["Build ROM"], dlg.VisibleLabels);
        dlg.Search.Text = "F9";
        Capture.Pump();
        Assert.Equal(["Toggle breakpoint"], dlg.VisibleLabels);
        // the link opens Preferences › Shortcuts
        win.Click(dlg.EditButton);
        var prefs = Assert.IsType<AgbStudio.Editor.Windows.Settings.PreferencesDialog>(win.Overlays.Open.Single());
        Assert.Equal(("T-S09", "KeyboardShortcuts"), s.Windows.Requests[^1]);
        Assert.Contains(prefs.GetVisualDescendants().OfType<AgbStudio.Editor.Controls.NavItem>(), n => n.Name == "Nav_T-S09" && n.IsSelected);
    }
}
