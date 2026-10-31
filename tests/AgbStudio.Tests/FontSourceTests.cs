using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>P8.01: game fonts from BDF bitmap fonts, image sheets and OpenType fonts with CFF outlines (.otf, made in the
/// test by <see cref="OtfWriter"/>; TrueType is covered by every fixture font).
/// P8.08: what the Figma Add Characters sample text really misses in the fixture font.</summary>
public sealed class FontSourceTests
{
    static string TempDir(string name)
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-fonts", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    const string Bdf = """
        STARTFONT 2.1
        FONT -agb-test-medium-r-normal--8-80-75-75-c-60-iso10646-1
        SIZE 8 75 75
        FONTBOUNDINGBOX 6 9 0 -2
        CHARS 3
        STARTCHAR space
        ENCODING 32
        DWIDTH 4 0
        BBX 0 0 0 0
        BITMAP
        ENDCHAR
        STARTCHAR A
        ENCODING 65
        DWIDTH 6 0
        BBX 5 7 0 0
        BITMAP
        20
        50
        88
        F8
        88
        88
        88
        ENDCHAR
        STARTCHAR g
        ENCODING 103
        DWIDTH 6 0
        BBX 5 6 0 -2
        BITMAP
        78
        88
        88
        78
        08
        70
        ENDCHAR
        ENDFONT
        """;

    static string Rows(GameFont.Glyph g) =>
        string.Join("|", Enumerable.Range(0, g.Height).Select(y => string.Concat(Enumerable.Range(0, g.Width).Select(x => g.Shades[y * g.Width + x].ToString()))));

    [Fact]
    public void BdfFontBuildsGlyphsMetricsAndMissingCharacters()
    {
        var dir = TempDir("bdf");
        File.WriteAllText(Path.Combine(dir, "tiny.bdf"), Bdf);
        var f = new FontFile { Name = "tiny", SourceKind = FontSourceKind.Bdf, Source = "tiny.bdf", CharacterSets = { "Basic Latin" }, LineHeight = 9, Baseline = 7, Bpp = 1 };
        var font = FontBuilder.Build(f, dir);
        var a = font.Get('A')!;
        Assert.Equal((5, 7, 0, 0, 6), (a.Width, a.Height, a.Left, a.Top, a.Advance));
        Assert.Equal("00100|01010|10001|11111|10001|10001|10001", Rows(a));
        // The descender of g reaches 2 rows below the baseline (BBX y offset -2).
        var g = font.Get('g')!;
        Assert.Equal((3, 6), (g.Top, g.Height));
        Assert.Equal("01111|10001|10001|01111|00001|01110", Rows(g));
        Assert.Equal(4, font.Get(' ')!.Advance);
        Assert.Equal(95 - 3, font.Missing.Count);
        Assert.Equal(9, font.LineHeight);
        // Laid out like any other font: pen advances by DWIDTH.
        var layout = TextLayout.Layout("Ag A", font, 100);
        Assert.Equal([0, 6, 12, 16], layout.Glyphs.Select(p => p.X));
        Assert.Contains('B', TextLayout.Layout("B", font, 100).Missing);
        // Packed 1bpp rows, MSB first, as the runtime draws them.
        Assert.Equal(new byte[] { 0x20, 0x50, 0x88, 0xF8, 0x88, 0x88, 0x88 }, FontBuilder.Pack(a, 1));
    }

    [Fact]
    public void ImageSheetFontReadsCellsInCharacterSetOrder()
    {
        var dir = TempDir("sheet");
        // 16 cells per row, 8 x 8 each, 5 rows: 80 of the 95 Basic Latin characters (the last 15 are missing).
        var sheet = new Canvas(128, 40);
        void Cell(char ch, Action<int, int> draw) => draw((ch - 0x20) % 16 * 8, (ch - 0x20) / 16 * 8);
        Cell('A', (x, y) => { sheet.Rect(x, y + 1, 5, 6, Canvas.Rgb(0, 0, 0)); sheet.Rect(x + 1, y + 2, 3, 2, Canvas.Rgb(128, 128, 128)); });
        Cell('i', (x, y) => sheet.Rect(x + 1, y, 1, 7, Canvas.Rgb(255, 255, 255)));
        sheet.Save(Path.Combine(dir, "sheet.png"));
        var f = new FontFile { Name = "sheet", SourceKind = FontSourceKind.Image, Source = "sheet.png", FixedWidth = 8, CellHeight = 8, CharacterSets = { "Basic Latin" },
            Variable = true, Spacing = 1, LineHeight = 8, Baseline = 7, Bpp = 2 };
        var font = FontBuilder.Build(f, dir);
        var a = font.Get('A')!;
        Assert.Equal((8, 8, 6), (a.Width, a.Height, a.Advance));  // 5 used columns + spacing 1
        Assert.Equal(3, a.Shades[1 * 8 + 0]);                      // black = darkest shade
        Assert.Equal(2, a.Shades[2 * 8 + 1]);                      // grey = middle shade
        Assert.Equal(0, a.Shades[0]);                              // transparent
        Assert.Equal(1, font.Get('i')!.Shades[0 * 8 + 1]);         // white = lightest shade
        Assert.Equal(3, font.Get('i')!.Advance);
        Assert.Equal(15, font.Missing.Count);
        Assert.Contains(0x7E, font.Missing);  // '~' is cell 94, past the sheet
        // Fixed width: every advance is the cell width.
        f.Variable = false;
        Assert.Equal(8, FontBuilder.Build(f, dir).Get('i')!.Advance);
    }

    [Theory]
    [InlineData("NotoSansThai-Regular.ttf", "Thai", 12)]
    [InlineData("fallback-latin.ttf", "Basic Latin", 10)]
    public void OpenTypeFontWithCffOutlinesBuildsTheSameGameFontAsTheTrueTypeFont(string ttfFile, string set, int size)
    {
        // An .otf made in the test from the outlines of a bundled TrueType font (each point at 16.16 precision): the
        // build reads its CFF charstrings and draws the same game font, pixel for pixel, as from the .ttf.
        var dir = TempDir("otf_" + Path.GetFileNameWithoutExtension(ttfFile));
        var ttfPath = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts", ttfFile);
        File.Copy(ttfPath, Path.Combine(dir, "source.ttf"));
        var tt = TrueType.Load(ttfPath);
        // Basic Latin and the set (the Thai font has some Latin), as the build asks for them.
        FontFile Font(string source) => new()
        {
            Name = "f", SourceKind = FontSourceKind.Ttf, Source = source, Size = size, CharacterSets = ["Basic Latin", set], LineHeight = size + 4, Baseline = size,
        };
        var cps = FontBuilder.CodePointsOf(Font("source.ttf")).Where(tt.Has).ToList();
        Assert.True(cps.Count > 80, $"{cps.Count} code points");
        File.WriteAllBytes(Path.Combine(dir, "source_cff.otf"), OtfWriter.FromTrueType(tt, cps, "AGB CFF " + set));

        var otf = TrueType.Load(Path.Combine(dir, "source_cff.otf"));
        Assert.True(otf.IsCff);
        Assert.False(tt.IsCff);
        Assert.Equal((tt.UnitsPerEm, tt.Ascender, tt.Descender), (otf.UnitsPerEm, otf.Ascender, otf.Descender));
        var curves = 0;
        foreach (var cp in cps)
        {
            Assert.True(otf.Has(cp), $"U+{cp:X4} is not in the .otf cmap");
            Assert.Equal(tt.HMetrics(tt.GlyphIndex(cp)).Advance, otf.HMetrics(otf.GlyphIndex(cp)).Advance);
            var a = tt.Outline(tt.GlyphIndex(cp));
            var b = otf.Outline(otf.GlyphIndex(cp));
            Assert.Equal(a.Select(c => c.Count), b.Select(c => c.Count));
            var worst = a.Zip(b).SelectMany(p => p.First.Zip(p.Second, (u, v) => Math.Max(Math.Abs(u.X - v.X), Math.Abs(u.Y - v.Y)))).DefaultIfEmpty(0).Max();
            Assert.True(worst <= 1.0 / 65536, $"U+{cp:X4}: points differ by {worst} units");
            curves += a.Sum(c => c.Count);
        }
        Assert.True(curves > 2000, $"{curves} points");

        var noFallback = Path.Combine(dir, "no-fallback.ttf");
        var fromTtf = FontBuilder.Build(Font("source.ttf"), dir, fallbackTtf: noFallback);
        var fromOtf = FontBuilder.Build(Font("source_cff.otf"), dir, fallbackTtf: noFallback);
        Assert.Equal(fromTtf.Glyphs.Keys.Order(), fromOtf.Glyphs.Keys.Order());
        Assert.Equal(fromTtf.Missing, fromOtf.Missing);
        Assert.Equal((fromTtf.LineHeight, fromTtf.Baseline, fromTtf.UpperVowelTop, fromTtf.ToneTop), (fromOtf.LineHeight, fromOtf.Baseline, fromOtf.UpperVowelTop, fromOtf.ToneTop));
        foreach (var (cp, g) in fromTtf.Glyphs)
        {
            var o = fromOtf.Glyphs[cp];
            Assert.True((g.Width, g.Height, g.Left, g.Top, g.Advance, Rows(g)) == (o.Width, o.Height, o.Left, o.Top, o.Advance, Rows(o)), $"U+{cp:X4} differs");
            Assert.Equal(FontBuilder.Pack(g, 2), FontBuilder.Pack(o, 2));  // as the ROM stores it
        }
        Assert.True(fromOtf.Glyphs.Values.Count(g => g.Width > 0) > 70);
    }

    // A 500 x 700 rectangle at (100, 0) in a 1000-unit em: the outline a test glyph should come out as.
    static readonly (double X, double Y)[] Box = [(100, 0), (600, 0), (600, 700), (100, 700)];

    static OtfWriter OperatorFont(bool cidKeyed)
    {
        static OtfWriter.Charstring C() => new();
        var w = new OtfWriter { Family = cidKeyed ? "AGB Ops CID" : "AGB Ops", CidKeyed = cidKeyed };
        // Subroutines: a global one draws two sides and calls a local one for the third.
        w.LocalSubrs.Add(C().N(-500, 0).Op(5).Op(11).Bytes);
        w.GlobalSubrs.Add(C().N(500, 0, 0, 700).Op(5).N(OtfWriter.SubrNumber(0, 1)).Op(10).Op(11).Bytes);
        void Add(int cp, OtfWriter.Charstring cs) => w.Glyphs.Add(new OtfWriter.Glyph(cp, 700, cs.Op(14).Bytes));
        // 'A': a width before the hints, hstemhm, hintmask with implicit vstems and its mask byte, rmoveto, hlineto.
        Add('A', C().N(640, 0, 700).Op(18).N(100, 500).Op(19).Raw(0xC0).N(100, 0).Op(21).N(500, 700, -500).Op(6));
        // 'B': the same box through callgsubr and callsubr.
        Add('B', C().N(100, 0).Op(21).N(OtfWriter.SubrNumber(0, 1)).Op(29));
        // 'C': the box from hmoveto, hlineto and vlineto after vstem hints and a cntrmask with its mask byte.
        Add('C', C().N(100, 500).Op(3).Op(20).Raw(0x80).N(100).Op(22).N(500).Op(6).N(700).Op(7).N(-500).Op(6));
        // 'D' to 'G' and 'O': one ellipse (4 cubic curves) as rrcurveto; as vhcurveto (alternating) after hmoveto and
        // vmoveto (a lone moveto leaves no contour); as single vhcurveto / hvcurveto curves; and as vhcurveto with the
        // optional last number, against rrcurveto ('O').
        double[] e1 = [0, 193, 112, 157, 138, 0], e2 = [138, 0, 112, -157, 0, -193], e3 = [0, -193, -112, -157, -138, 0], e4 = [-138, 0, -112, 157, 0, 193];
        Add('D', C().N(100, 350).Op(21).N([.. e1, .. e2, .. e3, .. e4]).Op(8));
        Add('E', C().N(100).Op(22).N(350).Op(4).N(193, 112, 157, 138, 138, 112, -157, -193, -193, -112, -157, -138, -138, -112, 157, 193).Op(30));
        Add('F', C().N(100, 350).Op(21).N(193, 112, 157, 138).Op(30).N(138, 112, -157, -193).Op(31).N(-193, -112, -157, -138).Op(30).N(-138, -112, 157, 193).Op(31));
        Add('G', C().N(100, 350).Op(21).N(193, 112, 157, 138, 138, 112, -157, -193, -193, -112, -157, -138, -138, -112, 157, 193, 25).Op(30));
        Add('O', C().N(100, 350).Op(21).N([.. e1, .. e2, .. e3, -138, 0, -112, 157, 25, 193]).Op(8));
        // 'H' / 'I': hhcurveto with dy1, vvcurveto with dx1, rlinecurve and rcurveline, against rrcurveto / rlineto.
        Add('H', C().N(100, 300).Op(21).N(100, 150, 200, 0, 150).Op(27).N(50, -100, 0, -50, -100).Op(26)
            .N(-100, 0, -100, 0, -100, 50, -50, 100).Op(25).N(-50, 0, -100, 0, -100, 0, 0, -50).Op(24));
        Add('I', C().N(100, 300).Op(21).N(150, 100, 200, 0, 150, 0, 50, -100, 0, -50, 0, -100).Op(8).N(-100, 0).Op(5)
            .N(-100, 0, -100, 50, -50, 100, -50, 0, -100, 0, -100, 0).Op(8).N(0, -50).Op(5));
        // 'J' to 'M': flex, hflex, hflex1 and flex1, each against the two rrcurvetos it stands for.
        Add('J', C().N(100, 100).Op(21).N(100, 0, 100, 50, 100, 50, 100, -50, 100, -50, 100, 0, 50).Op12(35).N(-600).Op(6));
        Add('K', C().N(100, 100).Op(21).N(100, 0, 100, 50, 100, 50, 100, -50, 100, -50, 100, 0).Op(8).N(-600).Op(6));
        Add('L', C().N(100, 100).Op(21).N(100, 100, 50, 100, 100, 100, 100).Op12(34).N(100, 20, 100, 30, 100, 100, 100, -30, 100).Op12(36)
            .N(100, 20, 100, 30, 100, 0, 100, 0, 100, -30, 100).Op12(37).N(-1200).Op(6));
        Add('M', C().N(100, 100).Op(21).N(100, 0, 100, 50, 100, 0, 100, 0, 100, -50, 100, 0).Op(8).N(100, 20, 100, 30, 100, 0, 100, 0, 100, -30, 100, -20).Op(8)
            .N(100, 20, 100, 30, 100, 0, 100, 0, 100, -30, 100, -20).Op(8).N(-1200).Op(6));
        // 'N': the box with its numbers worked out by arithmetic, storage and stack operators.
        Add('N', C().N(123, 3).Op12(20).N(3).Op12(21).Op12(18)               // put 123, get it back, drop it
            .N(1, 2, 5, 3).Op12(22).Op12(18)                                    // ifelse (5 > 3: the second), drop
            .N(4).Op12(27).Op12(15).Op12(5).Op12(18)                            // dup, eq, not, drop
            .N(1, 0).Op12(4).N(1).Op12(3).N(0).Op12(29).Op12(10).Op12(18)       // or, and, index, add, drop
            .N(50, 2).Op12(24).N(7, 7).Op12(11).Op(21)                          // mul, sub: rmoveto 100 0
            .N(1000, 2).Op12(12).N(-700).Op12(14).Op12(28)                      // div, neg, exch: 700 500
            .N(2, 1).Op12(30)                                                   // roll the two: 500 700
            .N(-500).Op12(9).Op12(14).Op(6));                                   // abs, neg: hlineto 500 700 -500
        return w;
    }

    [Fact]
    public void CffCharstringOperatorsDrawTheOutlinesTheyDescribe()
    {
        var dir = TempDir("otf_ops");
        foreach (var cid in new[] { false, true })
        {
            var file = cid ? "ops_cid.otf" : "ops.otf";
            File.WriteAllBytes(Path.Combine(dir, file), OperatorFont(cid).Write());
            var f = TrueType.Load(Path.Combine(dir, file));
            List<List<(double X, double Y)>> O(char c) => f.Outline(f.GlyphIndex(c));
            Assert.True(f.IsCff);
            Assert.Equal(1000, f.UnitsPerEm);
            Assert.Equal(700, f.HMetrics(f.GlyphIndex('A')).Advance);
            foreach (var c in "ABCN") Assert.Equal([Box], O(c).Select(x => x.ToArray()));
            // Curves are flattened into 8 segments each: 4 curves and the start point.
            Assert.Equal(1 + 4 * 8, O('D').Single().Count);
            Assert.Equal((350.0, 700.0), O('D').Single()[8]);
            Assert.Equal(O('D'), O('E'));
            Assert.Equal(O('D'), O('F'));
            Assert.Equal(O('O'), O('G'));
            Assert.NotEqual(O('D'), O('G'));
            Assert.Equal(O('I'), O('H'));
            Assert.Equal(O('K'), O('J'));
            Assert.Equal(O('M'), O('L'));
        }
        // Drawn by the build: the box at 10 px is a solid 5 x 7 block standing on the baseline, 7 px wide with spacing.
        var font = FontBuilder.Build(new FontFile { Name = "ops", Source = "ops_cid.otf", Size = 10, Bpp = 2, LineHeight = 12, Baseline = 9, CharacterSets = ["Basic Latin"] },
            dir, fallbackTtf: Path.Combine(dir, "no-fallback.ttf"));
        var a = font.Get('A')!;
        Assert.Equal((5, 7, 1, 2, 7), (a.Width, a.Height, a.Left, a.Top, a.Advance));
        Assert.Equal(string.Join("|", Enumerable.Repeat("33333", 7)), Rows(a));
        Assert.Equal(95 - 15 - 1, font.Missing.Count);  // A to O are drawn; a space is never missing
    }

    [Fact]
    public void FontFilesAGameFontCannotUseAreRefusedWithTheReason()
    {
        var dir = TempDir("otf_refused");
        string Refusal(string file, byte[] bytes)
        {
            File.WriteAllBytes(Path.Combine(dir, file), bytes);
            return Assert.Throws<InvalidDataException>(() => TrueType.Load(Path.Combine(dir, file))).Message;
        }
        Assert.Contains("font collection (.ttc)", Refusal("a.ttc", [.. "ttcf"u8, 0, 1, 0, 0, 0, 0, 0, 0]));
        Assert.Contains("web font (WOFF)", Refusal("a.woff", [.. "wOFF"u8, 0, 1, 0, 0, 0, 0, 0, 0]));
        Assert.Contains("not a TrueType or OpenType font", Refusal("a.otf", "hello, font"u8.ToArray()));
        // An .otf whose CFF table was renamed CFF2 (a variable font) and one that lost its CFF table.
        var otf = OperatorFont(false).Write();
        var at = otf.AsSpan().IndexOf("CFF "u8);
        var cff2 = (byte[])otf.Clone();
        cff2[at + 3] = (byte)'2';
        Assert.Contains("CFF2 outlines (an OpenType variable font)", Refusal("b.otf", cff2));
        var none = (byte[])otf.Clone();
        none[at] = (byte)'X';
        Assert.Contains("its 'CFF' table is missing", Refusal("c.otf", none));
    }

    public const string FigmaPaste ="ฮอปเปอร์: เหรียญหายไปไหนหมดนะ?\nนกฮูก: เจ้าสไลม์ขโมยไปไว้ในถ้ำน่ะสิ! ฿100 · ½ · ©";

    [Fact]
    public void FigmaAddCharactersSampleMissesTwoCharactersInTheFixtureFont()
    {
        // Figma T-D16 shows "7 characters are not in thai_vwf yet": ฮ ฬ ฦ ฿ ½ © ๆ. The fixture's thai_vwf is the font the
        // Font Manager frame (T-P09) shows: Basic Latin 95, Thai 87, Symbols 12. Against it the pasted text misses 2.
        var p = SkyHopper.Create(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "add_characters"));
        var font = FontBuilder.Build(GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font")), p.FontsDir);
        var text = ThaiText.CodePoints(FigmaPaste).ToHashSet();
        var missing = text.Where(c => c >= 0x20 && font.Get(c) == null).Order().ToList();
        Assert.Equal([0x00B7, 0x00BD], missing);  // · and ½
        int[] figma = [0x0E2E, 0x0E2C, 0x0E26, 0x0E3F, 0x00BD, 0x00A9, 0x0E46];
        // Three of Figma's seven (ฬ ฦ ๆ) are not in the pasted text at all, so no scan of that text can list them ...
        Assert.Equal([0x0E26, 0x0E2C, 0x0E46], figma.Where(c => !text.Contains(c)).Order());
        // ... and ฮ ฿ © are in the font's own character sets (Thai 87, Symbols 12), so they are not missing.
        Assert.All(new[] { 0x0E2E, 0x0E3F, 0x00A9 }, c => Assert.NotNull(font.Get(c)));
        Assert.Equal(87, FontBuilder.CharacterSets["Thai"].Count(c => font.Get(c) != null));
        // The middle dot is in the text and not in the font, but Figma does not list it.
        Assert.DoesNotContain(0x00B7, figma);
    }
}
