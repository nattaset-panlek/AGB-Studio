using System.Text.Json;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Theme;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Tests.Shot;

/// <summary>
/// A SHOT comparison that is not a whole frame of the manifest: a part of a frame or a sample exported on its own
/// (the type specimen of P10.03, the Play Mode game view of P12.05). Found by reflection like <see cref="IShotSource"/>.
/// <c>Render(path)</c> writes the app's PNG at the size of the Figma PNG <c>FigmaPng</c>.
/// </summary>
public interface IShotSpecimen
{
    IEnumerable<(string Id, string Set, string FigmaPng, Action<string> Render)> Specimens();
}

/// <summary>
/// P10.03 / T-K02 type specimen: the sample text Figma uses for each text style (docs/evidence/figma/type/type.json,
/// one text node per style, exported at 1x) rendered with the app's text style (<see cref="TextStyles"/>) in the colour
/// token that has the node's colour, left-aligned in a box of the node's size.
/// </summary>
public sealed class TypeSpecimenShots : IShotSpecimen
{
    public sealed record Sample(string style, string text, double w, double h, double[]? fill, string file);

    public static IReadOnlyList<Sample> Samples() =>
        JsonSerializer.Deserialize<List<Sample>>(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "type", "type.json")))!;

    /// <summary>The styles with no sample text in the Figma file (no text node uses them).</summary>
    public static IReadOnlyList<string> WithoutSample() => TextStyles.All.Select(s => s.Name).Except(Samples().Select(s => s.style)).ToList();

    public IEnumerable<(string Id, string Set, string FigmaPng, Action<string> Render)> Specimens()
    {
        foreach (var s in Samples())
        {
            var sample = s;
            yield return ("type/" + Path.GetFileNameWithoutExtension(s.file), "Type specimen " + s.style,
                Path.Combine(Repo.FigmaEvidence, "type", s.file), path => Render(sample, path));
        }
    }

    /// <summary>The Dark colour token whose value is the sample's fill (the specimen uses tokens only).</summary>
    public static string TokenOf(double[] fill)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "tokens.json")));
        var dark = doc.RootElement.GetProperty("collections").EnumerateArray().First(c => c.GetProperty("name").GetString() == "AGB Studio");
        var hex = "#" + string.Concat(fill.Select(c => ((int)Math.Round(c * 255)).ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        return dark.GetProperty("variables").EnumerateArray().Where(v => v.GetProperty("hex").GetString() == hex)
            .Select(v => v.GetProperty("name").GetString()!).OrderBy(n => n.StartsWith("text/", StringComparison.Ordinal) ? 0 : 1).First();
    }

    static void Render(Sample s, string path)
    {
        var text = UI.Text(s.text, s.style, TokenOf(s.fill!));
        text.TextWrapping = TextWrapping.NoWrap;
        text.VerticalAlignment = VerticalAlignment.Top;
        text.HorizontalAlignment = HorizontalAlignment.Left;
        using var png = SkiaSharp.SKBitmap.Decode(Path.Combine(Repo.FigmaEvidence, "type", s.file));
        // the headless frame has no alpha: draw on bg/base, the colour the comparison puts under the Figma export
        var host = new Panel { Children = { text } }.Token(Panel.BackgroundProperty, "bg/base");
        Capture.ControlToPng(host, png.Width, png.Height, path);
    }
}
