using AgbStudio.Build.Fonts;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// Text as the ROM shows it (P8.09, P9.10): the language's game font built like the build does
/// (<see cref="FontBuilder.Build"/> with every character the string tables use), laid out with <see cref="TextLayout"/>
/// and drawn with <see cref="TextRenderer"/> into the runtime dialogue box (runtime/include/agb/text.hpp
/// <c>BoxStyle</c>: tiles 1,14 to 28 x 5, paper / ink / shadow colours) with the continue marker of
/// <c>TextBox::showContinueMarker</c>. The fit check is the build's: lines against the key's max lines, width
/// against its box width.
/// </summary>
public sealed class GbaText(ProjectData data)
{
    public const int ScreenWidth = 240, ScreenHeight = 160;
    public const int BoxX = 8, BoxY = 112, BoxWidth = 224, BoxHeight = 40;

    // agb::text::BoxStyle defaults (BGR555): 1 ink, 2 ink2, 3 ink3, 4 shadow, 5 paper; the box uses Style{1, 4, 5}.
    static readonly ushort[] BoxPalette = [0, 0x0C63, 0x294A, 0x4E73, 0x6318, 0x7BDE, 0x0C63];
    const byte Ink = 1, Shadow = 4, Paper = 5;

    static readonly Dictionary<string, (DateTime Time, string Sig, GameFont? Font)> Fonts = new(StringComparer.OrdinalIgnoreCase);
    HashSet<int>? _used;

    /// <summary>The font name a language uses: its Localization font, else the project's default font (as the build picks it).</summary>
    public string FontName(string lang)
    {
        var info = data.Loc.Languages.FirstOrDefault(l => l.Code == lang);
        return info?.Font is { Length: > 0 } f ? f : data.Project.Settings.General.DefaultFont;
    }

    public IReadOnlyList<string> FontNames() => data.Project.FontFiles().Select(GameProject.AssetName).Order(StringComparer.Ordinal).ToList();

    /// <summary>Code points the build passes to the font builder: every string of every language, plus language names.</summary>
    HashSet<int> Used(string extraText)
    {
        if (_used == null)
        {
            var langs = data.Loc.Languages;
            var all = string.Concat(data.Strings.Values.SelectMany(t => t.Strings.Values)) + (langs.Count > 1 ? string.Concat(langs.Select(l => l.Name)) : "");
            _used = ThaiText.CodePoints(all).Where(c => c >= 0x20).ToHashSet();
        }
        foreach (var c in ThaiText.CodePoints(extraText)) if (c >= 0x20) _used.Add(c);
        return _used;
    }

    /// <summary>The built game font, or null when the project has no such font or its source cannot be read.</summary>
    public GameFont? Font(string name, string text = "")
    {
        var path = data.Project.FontFiles().FirstOrDefault(f => GameProject.AssetName(f) == name);
        if (path is null) return null;
        var used = Used(text);
        var sig = used.Count + ":" + used.Sum(c => (long)c);
        var time = File.GetLastWriteTimeUtc(path);
        if (Fonts.TryGetValue(path, out var c) && c.Time == time && c.Sig == sig) return c.Font;
        GameFont? font;
        try
        {
            var ff = GameProject.Load<FontFile>(path);
            if (ff.Name.Length == 0) ff.Name = name;
            font = FontBuilder.Build(ff, data.Project.FontsDir, used.Order().ToList());
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or IndexOutOfRangeException) { font = null; }
        Fonts[path] = (time, sig, font);
        return font;
    }

    public GameFont? FontFor(string lang, string text = "") => Font(FontName(lang), text);

    public TextLayout.Result? Layout(string lang, string text, int width = BoxWidth) =>
        FontFor(lang, text) is { } f ? TextLayout.Layout(text, f, width, data.Loc.ThaiWrap) : null;

    /// <summary>Result of the fit check of one string.</summary>
    public sealed record FitResult(bool Fits, int Lines, int Width, int BoxWidth, int MaxLines)
    {
        /// <summary>The Localization preview caption: Figma's line when it fits, the build's warning when it does not.</summary>
        public string Caption(bool thaiWrap) => Fits
            ? $"พอดี {Lines} บรรทัดในกล่องข้อความ (สูงสุด {MaxLines})" + (thaiWrap ? " · ตัดคำไทยตามพจนานุกรม" : "")
            : $"needs {Lines} lines; the box shows {MaxLines}";
    }

    /// <summary>The build's fit check for a key's text in a language (box width and max lines from strings.meta.json).</summary>
    public FitResult? Fit(string lang, string key, string text)
    {
        var info = data.Info(key);
        var layout = Layout(lang, text, info.BoxWidth);
        if (layout is null) return null;
        var lines = Math.Max(1, layout.Lines);
        return new FitResult(lines <= info.MaxLines && layout.MaxWidth <= info.BoxWidth, lines, layout.MaxWidth, info.BoxWidth, info.MaxLines);
    }

    /// <summary>
    /// The dialogue box (224 x 40) with the text fully typed, as ARGB pixels: with the continue marker, or with the
    /// choice options under the text and the cursor on the first one (agb::text drawChoices).
    /// </summary>
    public PixelData? RenderBox(string lang, string text, IReadOnlyList<string>? choices = null)
    {
        if (FontFor(lang, text + string.Concat(choices ?? [])) is not { } font) return null;
        var layout = TextLayout.Layout(text, font, BoxWidth, data.Loc.ThaiWrap);
        var style = new TextRenderer.Style(Ink, Shadow, Paper);
        var px = TextRenderer.Render(layout, font, BoxWidth, BoxHeight, style);
        void Fill(int x, int y, int w, int h, byte v)
        {
            for (var yy = Math.Max(0, y); yy < Math.Min(BoxHeight, y + h); yy++)
                for (var xx = Math.Max(0, x); xx < Math.Min(BoxWidth, x + w); xx++) px[yy * BoxWidth + xx] = v;
        }
        if (choices is { Count: > 0 })
        {
            var lh = font.LineHeight;
            for (var i = 0; i < choices.Count; i++)
            {
                var y = (Math.Max(1, layout.Lines) + i) * lh;
                Fill(0, y, BoxWidth, lh, 0);
                if (i == 0) for (var k = 0; k < 3; k++) Fill(2 + k, y + lh / 2 - 3 + k, 1, 7 - 2 * k, Ink);
                var option = TextLayout.Layout(choices[i], font, BoxWidth, data.Loc.ThaiWrap);
                foreach (var g in option.Glyphs.Where(g => g.Line == 0))
                    if (font.Get(g.CodePoint) is { Width: > 0 } glyph) TextRenderer.DrawGlyph(px, BoxWidth, BoxHeight, font, glyph, g.X + 10, y + g.Dy, style);
            }
        }
        else
        {
            // TextBox::showContinueMarker: a 5 px down-pointing triangle in the bottom-right corner
            int x0 = BoxWidth - 9, y0 = BoxHeight - 6;
            for (var r = 0; r < 3; r++)
                for (var c = r; c < 5 - r; c++) px[(y0 + r) * BoxWidth + x0 + c] = Ink;
        }
        var palette = BoxPalette.Select(Gba.ToArgb).ToArray();
        palette[0] = 0;
        return new PixelData(BoxWidth, BoxHeight, TextRenderer.ToArgb(px, palette));
    }
}
