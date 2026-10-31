using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Editor.Windows.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Scripts;

/// <summary>
/// Settings that act on the Script Editor (T-P04): Project Settings › Scripting "Show inline hints" (T-S04), Preferences ›
/// Editor code font and theme (T-S07), Appearance code font size (T-S06); the Analyzer chip opens Scripting.
/// </summary>
public sealed class ScriptEditorSettingsTests : IDisposable
{
    readonly EditorHarness _h = new();

    public void Dispose() => _h.Dispose();

    ScriptEditorPage OpenPlayer()
    {
        _h.Open("T-P04", SkyHopperProject.Script(_h.Fx, "PlayerController"));
        var page = _h.Page<ScriptEditorPage>();
        page.AnalyzeNow();
        Capture.Pump();
        return page;
    }

    [AvaloniaFact]
    public void InlineHints_OffKeepsTheHintClosed_OnOpensIt()
    {
        var page = OpenPlayer();
        var ed = page.Editor;
        var issue = Assert.Single(ed.Issues, i => i.Id == "GAE002");
        var p = ed.PointOf(issue.Start);
        var at = _h.At(ed, new Avalonia.Point(p.X + 8, p.Y + 10));
        // off: the underline stays, the hint does not open (the Problems list still has it)
        _h.S.Project.Current!.Settings.Scripting.InlineHints = false;
        Assert.False(page.InlineHints);
        _h.ClickAt(at);
        Assert.Null(page.Popup);
        // on (the Figma default): the click opens the hint next to the code
        _h.S.Project.Current.Settings.Scripting.InlineHints = true;
        _h.ClickAt(new Avalonia.Point(at.X, at.Y + 60));
        _h.ClickAt(at);
        Assert.NotNull(page.Popup);
    }

    [AvaloniaFact]
    public void CodeFontSizeFontAndTheme_ApplyToTheCodeEditor()
    {
        var page = OpenPlayer();
        var ed = page.Editor;
        var scope = _h.Find<ThemeVariantScope>("CodeThemeScope");
        Assert.Equal(ThemeVariant.Dark, scope.RequestedThemeVariant);
        Assert.Equal(13, ed.FontSizeOverride);
        var lineHeight = ed.LineHeight;
        var darkBg = ((ISolidColorBrush)AgbStudio.Editor.Services.ThemeService.Token(ed, "bg/base")!).Color;
        // Preferences saved: font size 16, theme Studio Light
        _h.S.Preferences.Update(x => { x.CodeFontSize = 16; x.CodeTheme = "Studio Light"; });
        Capture.Pump();
        Assert.Equal(16, ed.FontSizeOverride);
        Assert.True(ed.LineHeight > lineHeight);
        Assert.Equal(ThemeVariant.Light, scope.RequestedThemeVariant);
        Assert.Equal(ThemeVariant.Light, ed.ActualThemeVariant);
        Assert.NotEqual(darkBg, ((ISolidColorBrush)AgbStudio.Editor.Services.ThemeService.Token(ed, "bg/base")!).Color);
        // the rest of the editor keeps the app theme
        Assert.Equal(ThemeVariant.Dark, page.ActualThemeVariant);
        // the code font: the bundled JetBrains Mono, another name is used when installed
        Assert.Same(AgbStudio.Editor.Theme.Fonts.Mono, ed.FontFamilyOverride);
        _h.S.Preferences.Update(x => x.CodeFont = "Consolas");
        Assert.Equal("Consolas", ed.FontFamilyOverride!.FamilyNames.PrimaryFamilyName);
    }

    [AvaloniaFact]
    public void AnalyzerChip_OpensProjectSettingsOnScripting()
    {
        OpenPlayer();
        _h.Click(_h.Find<Chip>("AnalyzerChip"));
        Assert.Equal(("T-S04", "AnalyzerChip"), _h.S.Windows.Requests[^1]);
        var d = Assert.IsType<ProjectSettingsDialog>(_h.Win.Overlays.Open.Single());
        Assert.Contains(d.GetVisualDescendants().OfType<NavItem>(), n => n.Name == "Nav_T-S04" && n.IsSelected);
    }
}
