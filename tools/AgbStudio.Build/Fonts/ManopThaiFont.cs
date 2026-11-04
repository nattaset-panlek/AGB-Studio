using AgbStudio.Core.Project;

namespace AgbStudio.Build.Fonts;

/// <summary>Manop Thai 6x14, the default game font of new projects and templates: a public domain 1bpp Thai and Latin BDF
/// font (third_party/fonts/thai6x14.bdf). Every letter is 6 px wide, so a 224 px box holds 37 characters, and its marks
/// are drawn in place by the font, so the layout does not move them. Three lines fit in a 56 px box.</summary>
public static class ManopThaiFont
{
    public const string FileName = "thai6x14.bdf";
    public const string FamilyName = "Manop Thai";

    public static string BundledPath => Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts", FileName);

    /// <summary>The .font settings of a project font made from this BDF.</summary>
    public static FontFile Settings(string name) => new()
    {
        Name = name, SourceKind = FontSourceKind.Bdf, Source = FileName, Size = 14, Variable = true, FixedWidth = 6,
        CharacterSets = { "Basic Latin", "Thai" }, LineHeight = 16, Baseline = 12, Spacing = 0, Bpp = 1,
        StackUpperMarks = false, PlaceLowerVowels = false,
        // The font draws its full stop as a small cross, which reads as "+" on the GBA screen: a square dot instead.
        Glyphs = { ["U+002E"] = new GlyphOverride { Rows = ["000000", "000000", "000000", "000000", "000000", "000000", "000000", "001100", "001100", "000000"] } },
    };

    /// <summary>Copies the BDF next to a project's fonts and returns its path.</summary>
    public static string CopyTo(string fontsDir)
    {
        Directory.CreateDirectory(fontsDir);
        var path = Path.Combine(fontsDir, FileName);
        File.Copy(BundledPath, path, true);
        return path;
    }
}
