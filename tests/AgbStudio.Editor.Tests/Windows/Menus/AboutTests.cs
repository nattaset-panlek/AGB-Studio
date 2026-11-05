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
        // THIRD_PARTY.md rows may add a version or the shipped files after the name ("Avalonia 11.3 (incl. ...)")
        foreach (var c in new[] { "mGBA libretro core", "maxmod 1.0.15 (GBA sources)", "Arm GNU Toolchain 15.2.rel1", "Roslyn (Microsoft.CodeAnalysis", "Avalonia", "Inter", "Noto Sans Thai" })
            Assert.Contains(components, x => x.StartsWith(c, StringComparison.Ordinal));

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

    /// <summary>Scope log G-SCOPE.02, T-P06: the Animation panel's wheel, drag and key edits, which are not commands, are
    /// listed after every command group with their keys and a caption that they cannot be changed; search finds them.</summary>
    [AvaloniaFact]
    public void KeyboardShortcuts_ListsTheAnimationPanelGesturesAsFixed()
    {
        var win = Shell(new EditorConfig());
        win.Key(PhysicalKey.Slash, RawInputModifiers.Control);
        var dlg = Assert.IsType<KeyboardShortcutsDialog>(win.Overlays.Open.Single());
        Assert.Contains("ANIMATION PANEL", dlg.GroupTitles);
        var fixedRows = KeyboardShortcutsDialog.FixedRows;
        string? none = null;
        Assert.Equal(
            [("Animation speed", "Wheel", "on the speed line"), ("Animation frame ticks", "Wheel", "on a frame"), ("Move animation frame", "Drag", "a frame"),
             ("Animation slower (one tick more)", "+", none), ("Animation faster (one tick less)", "-", none),
             ("Move animation frame left", "Ctrl+Left", none), ("Move animation frame right", "Ctrl+Right", none)],
            fixedRows.Select(r => (r.Label, r.KeyText, r.Where)));
        Assert.Equal(fixedRows.Select(r => r.Label).Order(), dlg.VisibleLabels.Where(l => fixedRows.Any(r => r.Label == l)).Order());
        var note = dlg.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "FixedNote");
        Assert.Equal("Fixed · keys work while the panel has focus · cannot be changed", note.Text);
        // each row draws its keycaps (and where the pointer is for the mouse gestures)
        foreach (var r in fixedRows)
        {
            var row = dlg.GetVisualDescendants().OfType<DockPanel>().Single(d => d.Name == "Fixed_" + r.Id);
            Assert.Equal(r.Keys, row.GetVisualDescendants().OfType<AgbStudio.Editor.Controls.Keycap>().Select(k => k.Label));
            Assert.Equal(r.Where, row.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(t => t.Name == "GestureWhere")?.Text);
        }
        // they are not commands: no command has their labels, so Preferences › Shortcuts has no pencil for them
        Assert.All(fixedRows, r => Assert.DoesNotContain(dlg.AllShortcuts, c => c.Label == r.Label));
        dlg.Search.Text = "Ctrl+Left";
        Capture.Pump();
        Assert.Equal(["Move animation frame left"], dlg.VisibleLabels);
        dlg.Search.Text = "wheel";
        Capture.Pump();
        Assert.Equal(["Animation speed", "Animation frame ticks"], dlg.VisibleLabels);
    }
}
