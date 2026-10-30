using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AgbStudio.Editor.Theme;

/// <summary>One Figma text style (T-K02). LineHeight null = auto; Upper = text case UPPER.</summary>
public sealed record TextStyle(string Name, string Family, string Weight, double Size, double? LineHeight, double LetterSpacing, bool Upper)
{
    public FontFamily FontFamily => Fonts.ForFigmaFamily(Family);
    public FontWeight FontWeight => Fonts.ForFigmaStyle(Weight);
}

/// <summary>The 20 text styles of the Figma file, applied with the attached property <c>Fig.Style</c>.</summary>
public static class TextStyles
{
    // Header 1, Header 2 and Body have a mixed font in Figma and no consumers; they use Inter Regular here.
    public static readonly IReadOnlyList<TextStyle> All =
    [
        new("Header 1", "Inter", "Regular", 34, null, -0.02 * 34, false),
        new("Header 2", "Inter", "Regular", 20, null, -0.02 * 20, false),
        new("Body", "Inter", "Regular", 13, null, -0.02 * 13, false),
        new("Brand/13", "Inter", "Bold", 13, 16, -0.2, false),
        new("Title/18", "Inter", "Bold", 18, 24, -0.2, false),
        new("Title/14", "Inter", "Semi Bold", 14, 18, 0, false),
        new("Body/12", "Inter", "Regular", 12, 16, 0, false),
        new("Body/12 Medium", "Inter", "Medium", 12, 16, 0, false),
        new("Body/12 Semibold", "Inter", "Semi Bold", 12, 16, 0, false),
        new("Label/11", "Inter", "Regular", 11, 14, 0, false),
        new("Label/11 Medium", "Inter", "Medium", 11, 14, 0, false),
        new("Caption/10", "Inter", "Regular", 10, 12, 0, false),
        new("Overline/11", "Inter", "Semi Bold", 11, 14, 0.4, true),
        new("Mono/10", "JetBrains Mono", "Regular", 10, 12, 0, false),
        new("Mono/11", "JetBrains Mono", "Regular", 11, 14, 0, false),
        new("Mono/11 Bold", "JetBrains Mono", "Bold", 11, 14, 0, false),
        new("Mono/13", "JetBrains Mono", "Regular", 13, 20, 0, false),
        new("Thai/13", "IBM Plex Sans Thai", "Regular", 13, 20, 0, false),
        new("Thai/13 Medium", "IBM Plex Sans Thai", "Medium", 13, 20, 0, false),
        new("Pixel/10", "Press Start 2P", "Regular", 10, 12, 0, false),
    ];

    static readonly Dictionary<string, TextStyle> ByName = All.ToDictionary(s => s.Name);

    public static TextStyle Get(string name) => ByName.TryGetValue(name, out var s) ? s : throw new ArgumentException("Unknown text style: " + name);
    public static bool Exists(string name) => ByName.ContainsKey(name);

    /// <summary>Applies the font, size, weight, line height and letter spacing of a style to a text element.</summary>
    public static void Apply(AvaloniaObject target, TextStyle s)
    {
        target.SetValue(TextElement.FontFamilyProperty, s.FontFamily);
        target.SetValue(TextElement.FontSizeProperty, s.Size);
        target.SetValue(TextElement.FontWeightProperty, s.FontWeight);
        switch (target)
        {
            case TextBlock tb:
                tb.LineHeight = s.LineHeight ?? double.NaN;
                tb.LetterSpacing = s.LetterSpacing;
                break;
            case TextBox tx:
                tx.LineHeight = s.LineHeight ?? double.NaN;
                tx.LetterSpacing = s.LetterSpacing;
                break;
        }
    }
}

/// <summary>Attached <c>Fig.Style="Body/12"</c>: sets a Figma text style on a TextBlock or TextBox.</summary>
public sealed class Fig : AvaloniaObject
{
    public static readonly AttachedProperty<string?> StyleProperty = AvaloniaProperty.RegisterAttached<Fig, AvaloniaObject, string?>("Style");

    static Fig()
    {
        StyleProperty.Changed.AddClassHandler<AvaloniaObject>((o, e) =>
        {
            if (e.NewValue is not string name) return;
            var s = TextStyles.Get(name);
            TextStyles.Apply(o, s);
            if (s.Upper && o is TextBlock tb) Uppercase(tb);
        });
    }

    public static string? GetStyle(AvaloniaObject o) => o.GetValue(StyleProperty);
    public static void SetStyle(AvaloniaObject o, string? v) => o.SetValue(StyleProperty, v);

    // Figma text case UPPER: keep the displayed text upper-case whenever it changes
    static void Uppercase(TextBlock tb)
    {
        void Fix() { var t = tb.Text; if (t != null && t != t.ToUpperInvariant()) tb.SetCurrentValue(TextBlock.TextProperty, t.ToUpperInvariant()); }
        Fix();
        tb.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) Fix(); };
    }
}
