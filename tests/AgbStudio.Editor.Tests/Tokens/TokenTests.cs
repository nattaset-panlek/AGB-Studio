using System.Text.Json;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Theme;

namespace AgbStudio.Editor.Tests.Tokens;

/// <summary>T-K01, T-K02 (P10.02, P10.03): app tokens and text styles equal the read-only Figma export.</summary>
public class TokenTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void TokenFile_IsGeneratedFromFigmaExport(string theme)
    {
        var tokens = theme == "Dark" ? TokenExport.Dark : TokenExport.Light;
        var expected = TokenExport.Generate(theme, tokens);
        var path = TokenExport.XamlPath(theme);
        if (Repo.Regenerate) File.WriteAllText(path, expected);
        Assert.Equal(expected, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void TokenFile_NamesAndValuesMatchFigma(string theme)
    {
        var figma = theme == "Dark" ? TokenExport.Dark : TokenExport.Light;
        var app = TokenExport.ReadXaml(TokenExport.XamlPath(theme));
        Assert.Equal(29, figma.Count);
        Assert.Equal(figma.Select(t => t.Name).Order(StringComparer.Ordinal), app.Keys.Order(StringComparer.Ordinal));
        foreach (var t in figma) Assert.Equal(t.Argb, app[t.Name]);
    }

    [Fact]
    public void BothThemes_HaveIdenticalKeys()
    {
        var dark = TokenExport.ReadXaml(TokenExport.XamlPath("Dark")).Keys.Order(StringComparer.Ordinal);
        var light = TokenExport.ReadXaml(TokenExport.XamlPath("Light")).Keys.Order(StringComparer.Ordinal);
        Assert.Equal(dark, light);
    }

    [Fact]
    public void TextStyles_MatchFigma()
    {
        using var doc = TokenExport.Load();
        var figma = doc.RootElement.GetProperty("textStyles").EnumerateArray().ToList();
        Assert.Equal(20, figma.Count);
        Assert.Equal(figma.Select(s => s.GetProperty("name").GetString()).Order(), TextStyles.All.Select(s => s.Name).Order());
        foreach (var f in figma)
        {
            var app = TextStyles.Get(f.GetProperty("name").GetString()!);
            Assert.Equal(f.GetProperty("size").GetDouble(), app.Size);
            Assert.Equal(f.GetProperty("textCase").GetString() == "UPPER", app.Upper);
            if (f.GetProperty("family").ValueKind == JsonValueKind.Null)
            {
                // Header 1, Header 2, Body: mixed font in Figma, letter spacing in percent, auto line height
                Assert.Null(app.LineHeight);
                Assert.Equal(f.GetProperty("letterSpacingPercent").GetDouble() / 100 * app.Size, app.LetterSpacing, 3);
                continue;
            }
            Assert.Equal(f.GetProperty("family").GetString(), app.Family);
            Assert.Equal(f.GetProperty("style").GetString(), app.Weight);
            Assert.Equal(f.GetProperty("lineHeight").GetDouble(), app.LineHeight);
            Assert.Equal(f.GetProperty("letterSpacing").GetDouble(), app.LetterSpacing, 2);
        }
    }

    [Fact]
    public void EffectStyles_MatchShadowsUsedByTheApp()
    {
        using var doc = TokenExport.Load();
        var fx = doc.RootElement.GetProperty("effectStyles").EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!, e => e.GetProperty("effects")[0]);
        Assert.Equal(Shadows.Popover.ToString(), Shadows.Format(fx["Shadow/Popover"].GetProperty("y").GetDouble(), fx["Shadow/Popover"].GetProperty("radius").GetDouble(), fx["Shadow/Popover"].GetProperty("alpha").GetDouble()));
        Assert.Equal(Shadows.Dialog.ToString(), Shadows.Format(fx["Shadow/Dialog"].GetProperty("y").GetDouble(), fx["Shadow/Dialog"].GetProperty("radius").GetDouble(), fx["Shadow/Dialog"].GetProperty("alpha").GetDouble()));
    }
}
