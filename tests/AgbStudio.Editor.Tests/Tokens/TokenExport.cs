using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AgbStudio.Editor.Tests.Support;

namespace AgbStudio.Editor.Tests.Tokens;

public sealed record FigmaToken(string Name, string Hex, double Alpha)
{
    /// <summary>#AARRGGBB as Avalonia writes it (alpha rounded like Figma's 0..1 value * 255).</summary>
    public string Argb => "#" + ((int)Math.Round(Alpha * 255)).ToString("X2", CultureInfo.InvariantCulture) + Hex.TrimStart('#').ToUpperInvariant();
}

/// <summary>Reads docs/evidence/figma/tokens.json (written by Shot/figma-export.mjs) and generates the app token dictionaries.</summary>
public static class TokenExport
{
    public static readonly string JsonPath = Repo.Path_("docs", "evidence", "figma", "tokens.json");
    public static string XamlPath(string theme) => Path.Combine(Repo.Editor, "Theme", $"Tokens.{theme}.axaml");

    public static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(JsonPath));

    public static IReadOnlyList<FigmaToken> Collection(string name)
    {
        using var doc = Load();
        var col = doc.RootElement.GetProperty("collections").EnumerateArray().First(c => c.GetProperty("name").GetString() == name);
        return col.GetProperty("variables").EnumerateArray()
            .Select(v => new FigmaToken(v.GetProperty("name").GetString()!, v.GetProperty("hex").GetString()!, v.GetProperty("alpha").GetDouble()))
            .OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<FigmaToken> Dark => Collection("AGB Studio");
    public static IReadOnlyList<FigmaToken> Light => Collection("AGB Studio · Light");

    public static string Generate(string theme, IReadOnlyList<FigmaToken> tokens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- Generated from docs/evidence/figma/tokens.json by AgbStudio.Editor.Tests (AGB_REGEN=1). Do not edit. -->");
        sb.AppendLine($"<!-- Figma variable collection: {(theme == "Dark" ? "AGB Studio" : "AGB Studio · Light")} (T-K01) -->");
        sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        foreach (var t in tokens)
            sb.AppendLine($"  <SolidColorBrush x:Key=\"{t.Name}\" Color=\"{t.Argb}\" />");
        sb.AppendLine("</ResourceDictionary>");
        return sb.ToString();
    }

    /// <summary>Token name -> #AARRGGBB from an app token file.</summary>
    public static Dictionary<string, string> ReadXaml(string path)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(path).Root!.Elements()
            .ToDictionary(e => (string)e.Attribute(x + "Key")!, e => ((string)e.Attribute("Color")!).ToUpperInvariant(), StringComparer.Ordinal);
    }
}
