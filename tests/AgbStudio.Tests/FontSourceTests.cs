using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>P8.01: game fonts from BDF bitmap fonts and image sheets (TrueType is covered by every fixture font).
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

    public const string FigmaPaste = "ฮอปเปอร์: เหรียญหายไปไหนหมดนะ?\nนกฮูก: เจ้าสไลม์ขโมยไปไว้ในถ้ำน่ะสิ! ฿100 · ½ · ©";

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
