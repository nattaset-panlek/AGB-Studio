using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Fonts;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>
/// G-THAI.01: every consonant with every upper vowel, lower vowel and tone mark, sara am, mai yamok, Thai digits and
/// mixed Thai / Latin lines. Each cluster is checked for marks that overlap each other, overlap the stem of an
/// ascender or the tail of a descender consonant, or leave the line; the whole corpus is compared with a reviewed
/// golden sheet; a few lines are compared with ROM captures.
/// </summary>
[Collection("emulator")]
public sealed class ThaiCorpusTests(EmulatorFixture emu)
{
    static readonly int[] Upper = [0x0E31, 0x0E34, 0x0E35, 0x0E36, 0x0E37, 0x0E47, 0x0E4D];
    static readonly int[] Lower = [0x0E38, 0x0E39, 0x0E3A];
    static readonly int[] Tones = [0x0E48, 0x0E49, 0x0E4A, 0x0E4B, 0x0E4C];

    static readonly string GoldenPath = Path.Combine(NativeBuild.RepoRoot, "tests", "AgbStudio.Tests", "Golden", "thai-corpus.png");

    static GameFont Font()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts");
        var f = new FontFile
        {
            Name = "corpus", Source = "NotoSansThai-Regular.ttf", Size = 12, Variable = true, CharacterSets = { "Basic Latin", "Thai" },
            LineHeight = 14, Baseline = 10, Spacing = 1, Bpp = 2, DropShadow = false,
        };
        return FontBuilder.Build(f, dir);
    }

    /// <summary>Consonant (ก..ฮ) x (none, an upper vowel, a lower vowel) x (no tone, each tone mark), then sara am.</summary>
    public static List<string> Clusters()
    {
        var list = new List<string>();
        for (var c = 0x0E01; c <= 0x0E2E; c++)
            foreach (var v in new int[] { 0 }.Concat(Upper).Concat(Lower))
                foreach (var t in new int[] { 0 }.Concat(Tones))
                {
                    var s = char.ConvertFromUtf32(c);
                    if (v != 0) s += char.ConvertFromUtf32(v);
                    if (t != 0) s += char.ConvertFromUtf32(t);
                    list.Add(s);
                }
        for (var c = 0x0E01; c <= 0x0E2E; c++)
            foreach (var t in new int[] { 0 }.Concat(Tones.Take(4)))
                list.Add(char.ConvertFromUtf32(c) + (t != 0 ? char.ConvertFromUtf32(t) : "") + "ำ");
        return list;
    }

    // Pixels of one placed glyph (no shadow), as a set of (x, y) in line coordinates.
    static HashSet<(int, int)> Mask(GameFont font, TextLayout.Placed p)
    {
        var set = new HashSet<(int, int)>();
        var g = font.Get(p.CodePoint);
        if (g == null) return set;
        for (var y = 0; y < g.Height; y++)
            for (var x = 0; x < g.Width; x++)
                if (g.Shades[y * g.Width + x] != 0) set.Add((p.X + g.Left + x, p.Dy + g.Top + y));
        return set;
    }

    [Fact]
    public void EveryClusterStacksItsMarksWithoutCollisions()
    {
        var font = Font();
        var problems = new List<string>();
        foreach (var text in Clusters())
        {
            var layout = TextLayout.Layout(text, font, 64);
            Assert.Empty(layout.Missing);
            Assert.Equal(1, layout.Lines);
            var placed = layout.Glyphs.Where(g => !ThaiText.IsSpace(g.CodePoint)).ToList();
            var baseCp = placed[0].CodePoint;
            var masks = placed.Select(p => (p.CodePoint, Pixels: Mask(font, p))).ToList();
            foreach (var (cp, px) in masks)
                if (px.Any(q => q.Item2 < 0 || q.Item2 >= font.LineHeight))
                    problems.Add($"{text}: U+{cp:X4} leaves the line");
            for (var i = 1; i < masks.Count; i++)
            {
                var (cp, px) = masks[i];
                if (cp == 0x0E33) continue;  // sara am is a spacing vowel
                // Marks never overlap each other.
                for (var j = 1; j < i; j++)
                    if (masks[j].CodePoint != 0x0E33 && px.Overlaps(masks[j].Pixels))
                        problems.Add($"{text}: U+{cp:X4} overlaps U+{masks[j].CodePoint:X4}");
                // Upper marks clear the stem of ป ฝ ฟ ฬ; lower vowels clear the tail of ฎ ฏ ฐ ญ.
                if ((ThaiText.IsAscender(baseCp) && !ThaiText.IsLowerVowel(cp)) || (ThaiText.IsDescender(baseCp) && ThaiText.IsLowerVowel(cp)))
                    if (px.Overlaps(masks[0].Pixels))
                        problems.Add($"{text}: U+{cp:X4} overlaps the consonant");
            }
        }
        if (problems.Count > 0) File.WriteAllLines(Path.Combine(NativeBuild.RepoRoot, "build", "thai-corpus-problems.txt"), problems);
        Assert.True(problems.Count == 0, $"{problems.Count} problems:\n" + string.Join("\n", problems.Take(40)));
    }

    [Fact]
    public void DigitsMaiYamokAndMixedLinesLayOutWithoutMissingGlyphs()
    {
        var font = Font();
        foreach (var text in new[] { "๐๑๒๓๔๕๖๗๘๙", "เด็กๆ เล่นกันทุกวันๆ", "ราคา 120 บาท (THB) ต่อชิ้น", "Level ๓: ป่าใหญ่ - Forest", "ฤดูฝน ฦ ฯลฯ" })
        {
            var layout = TextLayout.Layout(text, font, 224);
            Assert.Empty(layout.Missing);
            Assert.Equal(1, layout.Lines);
            Assert.Equal(ThaiText.CodePoints(text).Count(c => c != ' '), layout.Glyphs.Count(g => !ThaiText.IsSpace(g.CodePoint)));
        }
    }

    /// <summary>The corpus drawn as a sheet of 16 x line-height cells, 48 per row.</summary>
    static (uint[] Argb, int W, int H) Sheet(GameFont font)
    {
        var clusters = Clusters();
        const int cols = 48, cw = 16;
        var rows = (clusters.Count + cols - 1) / cols;
        int w = cols * cw, h = rows * font.LineHeight;
        var px = new byte[w * h];
        for (var i = 0; i < clusters.Count; i++)
        {
            var layout = TextLayout.Layout(clusters[i], font, 64);
            var cell = TextRenderer.Render(layout, font, cw, font.LineHeight);
            int ox = i % cols * cw, oy = i / cols * font.LineHeight;
            for (var y = 0; y < font.LineHeight; y++)
                for (var x = 0; x < cw; x++) px[(oy + y) * w + ox + x] = cell[y * cw + x];
        }
        return (TextRenderer.ToArgb(px), w, h);
    }

    [Fact]
    public void CorpusMatchesTheReviewedGoldenSheet()
    {
        var (argb, w, h) = Sheet(Font());
        if (!File.Exists(GoldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GoldenPath)!);
            AgbStudio.Core.Png.WriteArgb(GoldenPath, argb, w, h);
            Assert.Fail($"No golden sheet yet: wrote {GoldenPath}; review it and run again.");
        }
        var golden = AgbStudio.Core.Png.Read(GoldenPath);
        Assert.Equal((w, h), (golden.Width, golden.Height));
        var diff = Enumerable.Range(0, argb.Length).Count(i => argb[i] != golden.Pixels[i]);
        if (diff > 0) AgbStudio.Core.Png.WriteArgb(Path.Combine(NativeBuild.RepoRoot, "build", "thai-corpus-actual.png"), argb, w, h);
        Assert.Equal(0, diff);
    }

    public static IEnumerable<object[]> RomLines =>
    [
        ["กี่ ปั้น ฝื่น ฟ้ำ ฬี๋"],
        ["ฎุ ฏู ญุ ฐู ทำ น้ำ ค่ำ"],
        ["๑๒๓ เด็กๆ Level ๓ ok"],
    ];

    [Theory]
    [MemberData(nameof(RomLines))]
    public void CorpusLinesInTheRomMatchThePreview(string line)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "mini_thai_corpus"));
        SampleText.AddTo(p);
        var th = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.th.json"));
        th.Strings["dlg.forest.hopper_01"] = line;
        GameProject.Save(Path.Combine(p.TextDir, "strings.th.json"), th);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var core = emu.Core;
        core.LoadRom(r.RomPath);
        for (var i = 0; i < 60; i++) core.RunFrame();
        var frame = (uint[])core.Frame.Clone();
        core.UnloadRom();
        var all = string.Concat(GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json")).Strings.Values) + string.Concat(th.Strings.Values);
        var font = FontBuilder.Build(GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font")), p.FontsDir, ThaiText.CodePoints(all));
        var layout = TextLayout.Layout(line, font, 224);
        int w = 224, h = font.LineHeight * layout.Lines;
        var expected = TextRenderer.Render(layout, font, w, h, new TextRenderer.Style(1, 4, 0));
        uint? ink = null;
        var mismatches = 0;
        var inkPixels = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var screen = frame[(16 + y) * 240 + 16 + x];
                if (expected[y * w + x] == 1)
                {
                    inkPixels++;
                    ink ??= screen;
                    if (screen != ink) mismatches++;
                }
                else if (ink != null && screen == ink) mismatches++;
            }
        Assert.True(inkPixels > 60, $"only {inkPixels} ink pixels");
        Assert.Equal(0, mismatches);
    }
}
