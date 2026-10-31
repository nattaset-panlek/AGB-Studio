using System.Text.Json;
using System.Text.RegularExpressions;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Gallery;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using Capture = AgbStudio.Editor.Tests.Shot.Capture;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Design;

/// <summary>T-K02..T-K04, 4.16, T-L04, G-EMOJI: fonts, icons, components and source scans.</summary>
public class DesignSystemTests
{
    static readonly string[] EditorSources = Directory.EnumerateFiles(Repo.Editor, "*.*", SearchOption.AllDirectories)
        .Where(f => (f.EndsWith(".cs") || f.EndsWith(".axaml")) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)).ToArray();

    [AvaloniaFact]
    public void Icons_All70ExportedFromFigma()
    {
        var names = Directory.EnumerateFiles(Path.Combine(Repo.Editor, "Assets", "Icons"), "*.svg").Select(Path.GetFileNameWithoutExtension).ToList();
        Assert.Equal(70, names.Count);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Repo.Editor, "Assets", "Icons"), "*.svg"))
        {
            var img = SvgImage.Parse(File.ReadAllText(f));
            Assert.Equal(16, img.Width);
        }
    }

    [AvaloniaFact]
    public void Icons_EveryNameUsedInTheUiExists()
    {
        Assert.Equal(70, IconCatalog.Names.Count);
        var used = new HashSet<string>();
        var rx = new Regex(@"Glyph(?:\s*=\s*|="")""?([a-z]+)""", RegexOptions.Compiled);
        foreach (var f in EditorSources)
            foreach (Match m in rx.Matches(File.ReadAllText(f))) used.Add(m.Groups[1].Value);
        foreach (var c in Catalog.Definitions()) if (c.Glyph != null) used.Add(c.Glyph);
        // icons on screen in the shell and the gallery
        using var fx = new Fixture();
        fx.OpenSkyHopper();
        var win = new MainWindow(fx.Services);
        win.Show();
        var gallery = new ComponentGallery();
        gallery.Show();
        Capture.Pump();
        foreach (var top in new Window[] { win, gallery })
            foreach (var icon in top.GetVisualDescendants().OfType<Icon>())
                if (icon.Glyph != null) used.Add(icon.Glyph);
        win.Close();
        gallery.Close();
        Assert.True(used.Count > 40, "too few icon references found: " + used.Count);
        var missing = used.Where(u => !IconCatalog.Exists(u)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Fonts_BundledWithOflLicences()
    {
        var dir = Path.Combine(Repo.Editor, "Assets", "Fonts");
        foreach (var family in new[] { "Inter", "JetBrainsMono", "IBMPlexSansThai", "IBMPlexSansThaiLooped", "PressStart2P", "Nunito" })
        {
            var licence = Path.Combine(dir, family, "OFL.txt");
            Assert.True(File.Exists(licence), licence);
            Assert.Contains("SIL Open Font License", File.ReadAllText(licence));
            Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(dir, family), "*.ttf"));
        }
        // the four weights the text styles use
        foreach (var f in new[] { "Inter/Inter-Regular.ttf", "Inter/Inter-Medium.ttf", "Inter/Inter-SemiBold.ttf", "Inter/Inter-Bold.ttf", "JetBrainsMono/JetBrainsMono-Bold.ttf", "IBMPlexSansThai/IBMPlexSansThai-Medium.ttf", "Nunito/Nunito-ExtraBold.ttf" })
            Assert.True(File.Exists(Path.Combine(dir, f)), f);
    }

    [AvaloniaFact]
    public void Fonts_EveryTextStyleResolvesToItsBundledFace()
    {
        foreach (var s in TextStyles.All.Where(s => s.LineHeight != null))
        {
            var tf = new Typeface(s.FontFamily, FontStyle.Normal, s.FontWeight);
            Assert.True(FontManager.Current.TryGetGlyphTypeface(tf, out var g), s.Name);
            Assert.StartsWith(s.Family, g!.FamilyName);
            Assert.Equal(s.FontWeight, g.Weight);
        }
    }

    [AvaloniaFact]
    public void Thai_RendersWithPlexThaiInUiText()
    {
        const string thai = "ยินดีต้อนรับสู่ Sky Hopper กด A เพื่อเริ่ม";
        var tb = new TextBlock { Text = thai, Foreground = Brushes.White };
        Fig.SetStyle(tb, "Body/12");
        var thaiFace = new Typeface(Fonts.ThaiFamily);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(thaiFace, out var g));
        foreach (var ch in thai.Where(c => c is >= '฀' and <= '๿')) Assert.NotEqual(0, g!.GetGlyph(ch));
        var path = Path.Combine(Repo.Evidence, "app", "P10-thai-specimen.png");
        Capture.ControlToPng(new Border { Background = Brushes.Black, Padding = new Avalonia.Thickness(8), Child = tb }, 320, 40, path, Brushes.Black);
        // text with Thai is wider than the Latin part alone, so the fallback face drew the Thai glyphs
        Assert.True(tb.Bounds.Width > 200);
    }

    [AvaloniaFact]
    public void TypeSpecimen_RendersAllTwentyStyles()
    {
        var col = new StackPanel { Spacing = 6, Margin = new Avalonia.Thickness(16) };
        foreach (var s in TextStyles.All)
        {
            var sample = s.Family.StartsWith("IBM") ? "ยินดีต้อนรับสู่ Sky Hopper — กด A เพื่อเริ่ม" : s.Family == "JetBrains Mono" ? "body.vy = -jumpPower; // 0x35FF" : s.Family == "Press Start 2P" ? "SCORE 0120" : "The quick GBA game engine 0123";
            var name = UI.Text(s.Name, "Mono/11", "text/muted", 170);
            var text = UI.Text(sample, s.Name, "text/primary");
            col.Children.Add(UI.Row(24, name, text));
        }
        Capture.ControlToPng(col, 900, 560, Path.Combine(Repo.Evidence, "app", "P10-type-specimen.png"), new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x23)));
        Assert.Equal(20, col.Children.Count);
    }

    [Fact]
    public void Components_All47BuildableComponentsAreInTheGallery()
    {
        string[] expected =
        [
            "Menu Item", "Menu Bar Item", "Button", "Icon Button", "Play Control", "Layer Toggle", "Chip", "Switch", "Checkbox", "Segment", "Progress", "Slider",
            "Tree Item", "Doc Tab", "Panel Tab", "List Item", "Nav Item", "Section Header", "Field", "Swatch", "Status Item", "Console Line", "Toast",
            "Asset Card", "Frame Thumb", "Scene Card", "Template Card", "Glyph Cell", "Table Cell", "Text Input", "Select", "Tab", "Node Header", "Node Pin",
            "brand/app-icon", "Menu Separator", "Badge", "Keycap", "Panel Header", "Search Field", "Meter", "Tooltip", "Title Bar", "Dialog Header",
            "Dialog Footer", "Popover Header", "brand/mark",
        ];
        Assert.Equal(47, expected.Length);
        Assert.Equal(expected.Order(), GalleryCatalog.Items.Select(i => i.Set).Distinct().Order());
        // every Figma variant has a gallery entry with the same id
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "manifest.json"))).RootElement.EnumerateArray()
            .Select(e => e.GetProperty("id").GetString()!).Where(id => id.StartsWith("C-")).Order();
        Assert.Equal(manifest, GalleryCatalog.Items.Select(i => i.Id).Order());
    }

    [Fact]
    public void Sources_NoColourLiteralsOutsideTheTheme()
    {
        // T-L04: colours come from the token files; Theme/Literals.axaml holds the raw hex Figma itself uses and
        // ThemeService derives the non-default accent tokens (Figma only defines the default accent); SvgImage reads colours from the SVG assets.
        // Besides hex and parsed colours the scan finds named colours (Brushes.White, Colors.Black, Foreground="White";
        // Transparent is a hit-test background, not a colour) and colours built from a literal (Color.FromUInt32(0x...)).
        var rx = new Regex(@"#[0-9A-Fa-f]{6}\b|#[0-9A-Fa-f]{8}\b|Color\.Parse|FromRgb|FromArgb|FromUInt32\(\s*0x|new Color\(\s*(?:0x|\d)|SolidColorBrush\(\s*0x|"
            + $@"(?<![\w.])(?:Avalonia\.Media\.)?(?:Brushes|Colors)\.(?:{NamedColours})\b|(?:Color|Background|Foreground|Fill|Stroke|BorderBrush)=""(?:{NamedColours})""");
        var offenders = EditorSources
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "Theme" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "Gallery" + Path.DirectorySeparatorChar))
            .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, i, l)))
            .Where(x => rx.IsMatch(WithoutResourceFallback(x.l)) && !x.l.TrimStart().StartsWith("//") && !x.f.EndsWith("ThemeService.cs") && !x.f.EndsWith("SvgImage.cs"))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}").ToList();
        Assert.Empty(offenders);
        // the scan finds the colours it is meant to find
        foreach (var hit in new[] { "ctx.DrawEllipse(Brushes.White, null, r);", "var c = Avalonia.Media.Colors.Black;", "Color.FromUInt32(0xFF1E1F23)", "<Border Background=\"White\" />" })
            Assert.Matches(rx, WithoutResourceFallback(hit));
        foreach (var allowed in new[] { "Background = Brushes.Transparent;", "bank.Colors.Count", "Colors.Count - 1", "Token(\"lit/black\") ?? Brushes.Black", "Color.FromUInt32(ToArgb(c))" })
            Assert.DoesNotMatch(rx, WithoutResourceFallback(allowed));
    }

    /// <summary>Avalonia's named colours (White, Black, Red ...) other than Transparent.</summary>
    static readonly string NamedColours = string.Join("|", typeof(Colors).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Select(p => p.Name).Where(n => n != nameof(Colors.Transparent)));

    /// <summary>
    /// A line without the named colour that only stands in when a theme resource is not found ("Token("lit/black") ??
    /// Brushes.Black", "TryFindResource(...) ? ... : Colors.White"): the colour on screen is the token's.
    /// </summary>
    static string WithoutResourceFallback(string line) =>
        Regex.IsMatch(line, @"\b(?:Token|Res|FindResource|TryFindResource)\(")
            ? Regex.Replace(line, $@"(?:\?\?|:)\s*(?:Avalonia\.Media\.)?(?:Brushes|Colors)\.(?:{NamedColours})\b", "")
            : line;

    // Dialog Footer (component): the buttons are laid out first and keep their size and place; a long hint wraps to a
    // second line in the width they leave, then ends with an ellipsis. A short hint stays one line at the left.
    [AvaloniaFact]
    public void DialogFooter_ALongHintNeverPushesTheButtonsOut()
    {
        (Avalonia.Rect Cancel, Avalonia.Rect Confirm, Avalonia.Rect Hint, TextBlock HintText, Window Host) Show(string? hint)
        {
            var footer = new AgbStudio.Editor.Controls.DialogFooter { CancelText = "Don't Save", ConfirmText = "Save All", Hint = hint };
            var host = Capture.Host(footer, 460, 60);
            Avalonia.Rect In(Control c) => new(c.TranslatePoint(default, footer)!.Value, c.Bounds.Size);
            Control Part(string name) => footer.GetVisualDescendants().OfType<Control>().Single(c => c.Name == name);
            var text = (TextBlock)Part("PART_Hint");
            return (In(Part("PART_Cancel")), In(Part("PART_Confirm")), In(text), text, host);
        }
        var plain = Show(null);
        var longHint = string.Join(" ", Enumerable.Repeat("Flashing cancelled at 128 / 412 KB. Flash again before playing the cartridge.", 4));
        var crowded = Show(longHint);
        var hinted = Show("Keep the cartridge connected until it finishes.");
        try
        {
            // the buttons: same size and place with or without a hint, right-aligned inside the 20 px padding
            foreach (var f in new[] { crowded, hinted })
            {
                Assert.Equal(plain.Cancel, f.Cancel);
                Assert.Equal(plain.Confirm, f.Confirm);
            }
            Assert.Equal(460 - 20, plain.Confirm.Right, 1);
            Assert.Equal(plain.Confirm.X - 8, plain.Cancel.Right, 1);
            Assert.True(plain.Cancel.Width > 60 && plain.Confirm.Width > 60);
            // the long hint stays left of Cancel (8 px apart) and inside the footer: two lines, the rest trimmed
            Assert.Equal(20, crowded.Hint.X, 1);
            Assert.True(crowded.Hint.Right <= crowded.Cancel.X - 8 + 0.5, $"hint ends at {crowded.Hint.Right}, Cancel starts at {crowded.Cancel.X}");
            Assert.True(crowded.Hint.Y >= 0 && crowded.Hint.Bottom <= 60, $"hint at {crowded.Hint}");
            var lines = crowded.HintText.TextLayout.TextLines;
            Assert.Equal(2, lines.Count);
            Assert.True(lines[^1].HasCollapsed, "the second line does not end with an ellipsis");
            // a hint that fits is one line at the left, centred in the footer
            Assert.Single(hinted.HintText.TextLayout.TextLines);
            Assert.Equal(20, hinted.Hint.X, 1);
            Assert.Equal(30, hinted.Hint.Y + hinted.Hint.Height / 2, 1);
        }
        finally
        {
            foreach (var f in new[] { plain, crowded, hinted }) f.Host.Close();
        }
    }

    // T-L04: the Slider knob is the lit/white token, as the Switch knob (Figma "Slider" / "Switch")
    [AvaloniaFact]
    public void Slider_KnobIsTheLitWhiteToken_AsTheSwitchKnob()
    {
        var slider = new AgbStudio.Editor.Controls.AgbSlider { Width = 160, Value = 40 };
        var toggle = new AgbStudio.Editor.Controls.AgbSwitch();
        var host = Capture.Host(new StackPanel { Children = { slider, toggle } }, 180, 40);
        var lit = Avalonia.Application.Current!.FindResource("lit/white");
        Assert.NotNull(lit);
        Assert.Same(lit, slider.Knob);
        Assert.Same(lit, toggle.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(e => e.Name == "PART_Knob").Fill);
        host.Close();
    }

    [Fact]
    public void Sources_NoEmoji()
    {
        var files = EditorSources.Concat(Directory.EnumerateFiles(Path.Combine(Repo.Root, "tests", "AgbStudio.Editor.Tests"), "*.cs", SearchOption.AllDirectories));
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            for (var i = 0; i < text.Length; i++)
            {
                var cp = char.ConvertToUtf32(text, i);
                if (char.IsHighSurrogate(text[i])) i++;
                Assert.False(cp is >= 0x1F000 and <= 0x1FAFF or >= 0x2600 and <= 0x27BF, $"{Path.GetFileName(f)} contains U+{cp:X}");
            }
        }
    }
}
