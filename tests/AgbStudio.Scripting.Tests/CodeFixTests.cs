namespace AgbStudio.Scripting.Tests;

/// <summary>P6.03: "Change to fixed" and "Ignore" from the Script Editor diagnostic popup.</summary>
public sealed class CodeFixTests
{
    const string Source = "using GBA;\n\npublic class PlayerController : Behaviour\n{\n    public fixed speed = 1.5;\n    PlatformerBody body;\n\n    float coyoteTime = 0.1f;\n}\n";

    [Fact]
    public async Task PopupOffersBothFixes()
    {
        var fixes = await ScriptCodeFixes.FixesAsync(Source, "GAE002", 8, 5);
        Assert.Equal(["Change to fixed", "Ignore"], fixes);
    }

    [Fact]
    public async Task ChangeToFixedRewritesTypeAndLiteral()
    {
        var fixedText = await ScriptCodeFixes.ApplyAsync(Source, "GAE002", 8, 5, "Change to fixed");
        Assert.NotNull(fixedText);
        Assert.Contains("    fixed coyoteTime = 0.1;\n", fixedText);
        Assert.Contains("    public fixed speed = 1.5;\n", fixedText);  // untouched lines keep the Figma spelling
        var r = TestSupport.Compile("codefix", TestSupport.Context(), TestSupport.Inline("PlayerController.cs", fixedText!));
        Assert.DoesNotContain(r.Diagnostics, d => d.Id == "GAE002");
        Assert.True(r.Success, TestSupport.Errors(r));
    }

    [Fact]
    public async Task ChangeToFixedOnLocalsAndReturnTypes()
    {
        const string src = "using GBA;\n\npublic class Mover : Behaviour\n{\n    double Half(int v)\n    {\n        var x = 0.5;\n        return x * v;\n    }\n}\n";
        var step1 = await ScriptCodeFixes.ApplyAsync(src, "GAE002", 5, 5, "Change to fixed");
        Assert.Contains("    fixed Half(int v)", step1);
        var step2 = await ScriptCodeFixes.ApplyAsync(step1!, "GAE002", 7, 9, "Change to fixed");
        Assert.Contains("        fixed x = 0.5;", step2);
        var r = TestSupport.Compile("codefix2", TestSupport.Context(), TestSupport.Inline("Mover.cs", step2!));
        Assert.True(r.Success, TestSupport.Errors(r));
    }

    [Fact]
    public async Task IgnoreAddsPragmaAroundTheLine()
    {
        var text = await ScriptCodeFixes.ApplyAsync(Source, "GAE002", 8, 5, "Ignore");
        Assert.NotNull(text);
        Assert.Contains("    #pragma warning disable GAE002\n    float coyoteTime = 0.1f;\n    #pragma warning restore GAE002\n", text);
        var r = TestSupport.Compile("codefix3", TestSupport.Context(), TestSupport.Inline("PlayerController.cs", text!));
        Assert.DoesNotContain(r.Diagnostics, d => d.Id == "GAE002");
    }

    [Fact]
    public async Task IgnoreWorksForErrorRules()
    {
        const string src = "using GBA;\n\nclass Shot { }\n\npublic class Gun : Behaviour\n{\n    Shot s;\n    void Update()\n    {\n        s = new Shot();\n    }\n}\n";
        var fixes = await ScriptCodeFixes.FixesAsync(src, "GAE001", 10, 13);
        Assert.Equal(["Ignore"], fixes);
        var text = await ScriptCodeFixes.ApplyAsync(src, "GAE001", 10, 13, "Ignore");
        Assert.Contains("#pragma warning disable GAE001", text);
    }

    [Fact]
    public void FixedKeywordRewriteKeepsPositions()
    {
        const string text = "fixed a = 1; // fixed in a comment\nstring s = \"fixed\"; fixed2 v;";
        var r = FixedKeyword.Rewrite(text);
        Assert.Equal(text.Length, r.Length);
        Assert.Equal("Fixed a = 1; // fixed in a comment\nstring s = \"fixed\"; fixed2 v;", r);
        Assert.Equal(text, FixedKeyword.Restore(r));
    }
}
