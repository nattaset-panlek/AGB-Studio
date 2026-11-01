using AgbStudio.Build;
using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>
/// AGB Pixel, AGB Studio's own 1bpp Thai and Latin pixel font (Fonts/AgbPixelFont.cs), the default game font of new
/// projects and templates: the committed BDF is the generator's output, every character of Basic Latin, Thai and
/// Symbols is drawn, the metrics hold, Thai marks land where TextLayout should put them (stacked tone marks, marks
/// left of the tall stems of ป ฝ ฟ ฬ, lower vowels under ฎ ฏ ญ ฐ, sara am), and a sample sheet matches its golden.
/// </summary>
public sealed class AgbPixelFontTests
{
    static readonly string Fonts = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts");
    static readonly string GoldenPath = Path.Combine(NativeBuild.RepoRoot, "tests", "AgbStudio.Tests", "Golden", "agb-pixel-sample.png");

    public const string Sample = "ที่นี่ น้ำ ปู่ ฎีกา ฐาน ญี่ปุ่น กำลัง\nThe quick brown fox jumps over the lazy dog.\nLevel ๓: ป่าใหญ่ - Forest (HP 120/150)\n๐๑๒๓๔๕๖๗๘๙ 0123456789 ฿99.50";

    static GameFont Font() => FontBuilder.Build(AgbPixelFont.Settings("agb_pixel"), Fonts);

    [Fact]
    public void CommittedBdfIsTheGeneratorOutput()
    {
        // Edit Fonts/AgbPixelFont.Glyphs.cs, then run `agb pixel-font` to rewrite the BDF.
        Assert.Equal(AgbPixelFont.Bdf(), File.ReadAllText(Path.Combine(Fonts, AgbPixelFont.FileName)).Replace("\r\n", "\n"));
    }

    [Fact]
    public void EveryCharacterOfLatinThaiAndSymbolsIsDrawn()
    {
        var font = Font();
        Assert.Empty(font.Missing);
        var wanted = new[] { "Basic Latin", "Thai", "Symbols" }.SelectMany(s => FontBuilder.CharacterSets[s]).ToList();
        foreach (var cp in wanted.Where(c => c != ' '))
            Assert.True(font.Get(cp) is { Width: > 0, Height: > 0 }, $"U+{cp:X4} has no pixels");
        // the tailless ญ and ฐ come along with the Thai set
        Assert.NotNull(font.Get(ThaiText.YoYingNoTail));
        Assert.NotNull(font.Get(ThaiText.ThoThanNoTail));
        Assert.Equal(wanted.Count + 2, font.Glyphs.Count);
        // 1bpp only, marks take no room, everything else does
        Assert.All(font.Glyphs.Values, g => Assert.All(g.Shades, s => Assert.InRange(s, 0, 1)));
        Assert.All(font.Glyphs.Values, g => Assert.Equal(ThaiText.IsMark(g.CodePoint), g.Advance == 0));
        // the text art parses back to the glyphs the BDF holds
        Assert.Equal(AgbPixelFont.Glyphs.Count, Bdf.Read(Path.Combine(Fonts, AgbPixelFont.FileName), 13).Count);
    }

    [Fact]
    public void MetricsLatinCapsThaiBodyAndA16PixelLine()
    {
        var font = Font();
        Assert.Equal((16, 13), (font.LineHeight, font.Baseline));
        int Top(char c) => font.Baseline - font.Get(c)!.Top;
        int Bottom(char c) => font.Get(c)!.Top + font.Get(c)!.Height - font.Baseline;
        Assert.Equal((7, 7, 5, 2), (Top('H'), Top('d'), Top('x'), Bottom('g')));
        // Thai body 6 rows on the baseline; ป ฝ ฟ ฬ rise 3 rows above it; upper vowels after one empty row
        foreach (var c in new[] { 'ก', 'ข', 'ด', 'น', 'ม', 'อ' }) Assert.Equal((6, 0), (Top(c), Bottom(c)));
        foreach (var c in new[] { 'ป', 'ฝ', 'ฟ', 'ฬ' }) Assert.Equal(9, Top(c));
        Assert.Equal((9, 2), (font.Baseline - font.UpperVowelTop, font.Get(0x0E34)!.Height));
        // lower vowels one row under the baseline
        Assert.Equal(1, font.Get(0x0E38)!.Top - font.Baseline);
    }

    static TextLayout.Placed At(TextLayout.Result r, int cp) => r.Glyphs.Single(g => g.CodePoint == cp);

    // Rows a placed glyph covers, in line coordinates.
    static (int Top, int Bottom) Rows(GameFont f, TextLayout.Placed p) => (f.Get(p.CodePoint)!.Top + p.Dy, f.Get(p.CodePoint)!.Top + f.Get(p.CodePoint)!.Height - 1 + p.Dy);

    static HashSet<(int X, int Y)> Mask(GameFont f, TextLayout.Placed p)
    {
        var g = f.Get(p.CodePoint)!;
        var set = new HashSet<(int, int)>();
        for (var y = 0; y < g.Height; y++)
            for (var x = 0; x < g.Width; x++)
                if (g.Shades[y * g.Width + x] != 0) set.Add((p.X + g.Left + x, p.Dy + g.Top + y));
        return set;
    }

    // True when two pixel sets overlap or touch (8 neighbours).
    static bool Meet(HashSet<(int X, int Y)> a, HashSet<(int X, int Y)> b) =>
        a.Any(p => Enumerable.Range(-1, 3).Any(dx => Enumerable.Range(-1, 3).Any(dy => b.Contains((p.X + dx, p.Y + dy)))));

    [Fact]
    public void ToneMarksSitAtTheUpperVowelHeightAndStackAboveAnUpperVowel()
    {
        var f = Font();
        var plain = TextLayout.Layout("ก่", f, 64);
        var vowelRows = Rows(f, At(TextLayout.Layout("กี", f, 64), 0x0E35));
        Assert.Equal(vowelRows.Bottom, Rows(f, At(plain, 0x0E48)).Bottom);
        foreach (var tone in new[] { 0x0E48, 0x0E49, 0x0E4A, 0x0E4B, 0x0E4C })
        {
            var r = TextLayout.Layout("กี" + char.ConvertFromUtf32(tone), f, 64);
            // one empty row between the vowel and the tone mark above it, and the stack stays inside the line
            Assert.Equal(vowelRows.Top - 2, Rows(f, At(r, tone)).Bottom);
            Assert.True(Rows(f, At(r, tone)).Top >= 0);
        }
        // sara am: a tone mark stacks over its nikhahit (น้ำ)
        var am = TextLayout.Layout("น้ำ", f, 64);
        Assert.Equal(Rows(f, At(am, ThaiText.SaraAm)).Top - 2, Rows(f, At(am, 0x0E49)).Bottom);
    }

    [Fact]
    public void UpperMarksMoveLeftOfTheTallStemsWithAGap()
    {
        var f = Font();
        foreach (var c in "ปฝฟฬ")
            foreach (var marks in new[] { "ิ", "ี่", "ั้", "่", "๊", "ื๋", "็", "์" })
            {
                var r = TextLayout.Layout(c + marks, f, 64);
                var stem = Mask(f, r.Glyphs[0]);
                foreach (var m in r.Glyphs.Skip(1))
                    Assert.False(Meet(stem, Mask(f, m)), $"{c}{marks}: U+{m.CodePoint:X4} touches the stem");
            }
        // sara am after a tall stem: sara aa plus a nikhahit moved left of the stem
        var am = TextLayout.Layout("ป่ำ", f, 64);
        Assert.Equal([0x0E1B, 0x0E48, ThaiText.SaraAa, ThaiText.Nikhahit], am.Glyphs.Select(g => g.CodePoint));
        Assert.False(Meet(Mask(f, am.Glyphs[0]), Mask(f, At(am, ThaiText.Nikhahit))));
        Assert.False(Meet(Mask(f, At(am, 0x0E48)), Mask(f, At(am, ThaiText.Nikhahit))));
        // other consonants keep the precomposed sara am
        Assert.Contains(TextLayout.Layout("กำ", f, 64).Glyphs, g => g.CodePoint == ThaiText.SaraAm);
    }

    [Fact]
    public void LowerVowelsUnderDescendersAndTaillessYoYingAndThoThan()
    {
        var f = Font();
        foreach (var v in new[] { 0x0E38, 0x0E39, 0x0E3A })
        {
            var s = char.ConvertFromUtf32(v);
            // ญ and ฐ drop their tails, so the vowel stays at its own height
            var yy = TextLayout.Layout("ญ" + s, f, 64);
            Assert.Equal([ThaiText.YoYingNoTail, v], yy.Glyphs.Select(g => g.CodePoint));
            Assert.Equal(0, At(yy, v).Dy);
            Assert.Equal(ThaiText.ThoThanNoTail, TextLayout.Layout("ฐ" + s, f, 64).Glyphs[0].CodePoint);
            // ฎ ฏ keep their short left-hand tails, clear of the vowel at the right
            foreach (var c in "ฎฏ")
            {
                var r = TextLayout.Layout(c + s, f, 64);
                Assert.Equal(0, At(r, v).Dy);
                Assert.False(Meet(Mask(f, r.Glyphs[0]), Mask(f, At(r, v))), $"{c}: U+{v:X4} touches the tail");
            }
        }
        // without a lower vowel the tails stay
        Assert.Equal(0x0E0D, TextLayout.Layout("ญี่", f, 64).Glyphs[0].CodePoint);
    }

    [Fact]
    public void EveryClusterFitsTheLineWithoutCollisions()
    {
        var f = Font();
        int[] upper = [0, 0x0E31, 0x0E34, 0x0E35, 0x0E36, 0x0E37, 0x0E47, 0x0E4D, 0x0E38, 0x0E39, 0x0E3A];
        int[] tones = [0, 0x0E48, 0x0E49, 0x0E4A, 0x0E4B, 0x0E4C];
        var problems = new List<string>();
        for (var c = 0x0E01; c <= 0x0E2E; c++)
            foreach (var v in upper)
                foreach (var t in tones)
                    foreach (var am in new[] { false, true })
                    {
                        if (am && v != 0) continue;
                        if (c is 0x0E24 or 0x0E26 && ThaiText.IsLowerVowel(v)) continue;  // ฤ ฦ are vowels: no lower vowel goes under them
                        var text = char.ConvertFromUtf32(c) + (v != 0 ? char.ConvertFromUtf32(v) : "") + (t != 0 ? char.ConvertFromUtf32(t) : "") + (am ? "ำ" : "");
                        var r = TextLayout.Layout(text, f, 64);
                        if (r.Missing.Count > 0) problems.Add($"{text}: missing");
                        var masks = r.Glyphs.Select(g => (g.CodePoint, Px: Mask(f, g))).ToList();
                        foreach (var (cp, px) in masks)
                            if (px.Any(p => p.Y < 0 || p.Y >= f.LineHeight)) problems.Add($"{text}: U+{cp:X4} leaves the line");
                        for (var i = 1; i < masks.Count; i++)
                            for (var j = 1; j < i; j++)
                                if (masks[i].CodePoint != ThaiText.SaraAm && masks[j].CodePoint != ThaiText.SaraAm && masks[i].Px.Overlaps(masks[j].Px))
                                    problems.Add($"{text}: U+{masks[i].CodePoint:X4} overlaps U+{masks[j].CodePoint:X4}");
                        // marks never touch the consonant they sit on
                        foreach (var (cp, px) in masks.Skip(1).Where(m => ThaiText.IsMark(m.CodePoint) || m.CodePoint == ThaiText.Nikhahit))
                            if (px.Overlaps(masks[0].Px)) problems.Add($"{text}: U+{cp:X4} overlaps the consonant");
                    }
        Assert.True(problems.Count == 0, $"{problems.Count} problems:\n" + string.Join("\n", problems.Take(40)));
    }

    /// <summary>The sample sentences drawn as `agb text-preview` draws them (1x, text palette), against a reviewed golden.</summary>
    public static (uint[] Argb, int W, int H) SampleImage()
    {
        var font = Font();
        var layout = TextLayout.Layout(Sample, font, 232);
        int w = 240, h = layout.Lines * font.LineHeight + 4;
        var px = TextRenderer.Render(layout, font, w, h, new TextRenderer.Style(1, 4, 0));
        return (TextRenderer.ToArgb(px), w, h);
    }

    [Fact]
    public void SampleSentencesMatchTheReviewedGolden()
    {
        var (argb, w, h) = SampleImage();
        var layout = TextLayout.Layout(Sample, Font(), 232);
        Assert.Empty(layout.Missing);
        Assert.Equal(4, layout.Lines);
        if (!File.Exists(GoldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GoldenPath)!);
            AgbStudio.Core.Png.WriteArgb(GoldenPath, argb, w, h);
            Assert.Fail($"No golden yet: wrote {GoldenPath}; review it and run again.");
        }
        var golden = AgbStudio.Core.Png.Read(GoldenPath);
        Assert.Equal((w, h), (golden.Width, golden.Height));
        var diff = Enumerable.Range(0, argb.Length).Count(i => argb[i] != golden.Pixels[i]);
        if (diff > 0) AgbStudio.Core.Png.WriteArgb(Path.Combine(NativeBuild.RepoRoot, "build", "agb-pixel-sample-actual.png"), argb, w, h);
        Assert.Equal(0, diff);
    }

    [Fact]
    public void TemplatesAndNewProjectsUseItAsTheDefaultFont()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "agb_pixel_template");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var p = AgbStudio.Build.Content.Templates.TemplateCatalog.Find("rpg")!.Create(dir, "Pixel");
        Assert.Equal("thai_vwf", p.Settings.General.DefaultFont);
        var file = GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font"));
        Assert.Equal((FontSourceKind.Bdf, AgbPixelFont.FileName, 1), (file.SourceKind, file.Source, file.Bpp));
        Assert.Equal(AgbPixelFont.Bdf(), File.ReadAllText(Path.Combine(p.FontsDir, AgbPixelFont.FileName)));
        Assert.Empty(FontBuilder.Build(file, p.FontsDir).Missing);
    }
}
