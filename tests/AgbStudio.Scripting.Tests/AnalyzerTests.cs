namespace AgbStudio.Scripting.Tests;

/// <summary>P6.02: GAE001-GAE006, one positive and one negative case per rule, severities and "Treat warnings as errors".</summary>
public sealed class AnalyzerTests
{
    static ScriptCompileResult Run(string body, ScriptCompileContext? ctx = null, string name = "PlayerController.cs") =>
        TestSupport.Compile("analyzer", ctx ?? TestSupport.Context(), TestSupport.Inline(name, body));

    static IEnumerable<ScriptDiagnostic> Gae(ScriptCompileResult r, string id) => r.Diagnostics.Where(d => d.Id == id);

    const string Head = "using GBA;\n\nclass Bullet { public int x; }\n\npublic class PlayerController : Behaviour\n{\n";

    [Fact]
    public void Gae001ClassAllocatedInUpdate()
    {
        var r = Run(Head + "    Bullet last;\n    void Update() { last = new Bullet(); }\n}\n");
        var d = Assert.Single(Gae(r, "GAE001"));
        Assert.Equal(ScriptSeverity.Error, d.Severity);
        Assert.Contains("no garbage collector", d.Message);
        var onTouch = Run(Head + "    Bullet last;\n    void OnTouch(Actor other) { last = new Bullet(); }\n}\n");
        Assert.Single(Gae(onTouch, "GAE001"));
    }

    [Fact]
    public void Gae001NotReportedInStart()
    {
        var r = Run(Head + "    Bullet last;\n    void Start() { last = new Bullet(); }\n    void Update() { var v = new fixed2(1, 2); last.x = v.x.ToInt(); }\n}\n");
        Assert.Empty(Gae(r, "GAE001"));
        Assert.True(r.Success, TestSupport.Errors(r));
    }

    [Fact]
    public void Gae002FloatFlaggedAsInFigma()
    {
        // Figma Build log: "PlayerController.cs(8,5): GAE002 float → use fixed"
        var src = "using GBA;\n\npublic class PlayerController : Behaviour\n{\n    public fixed speed = 1.5;\n    PlatformerBody body;\n\n    float coyoteTime = 0.1f;\n}\n";
        var r = Run(src);
        var d = Assert.Single(Gae(r, "GAE002"));
        Assert.Equal(ScriptSeverity.Warning, d.Severity);
        Assert.Equal((8, 5), (d.Line, d.Column));
        Assert.Equal("float is not supported on GBA", d.Message);
        Assert.Equal("PlayerController.cs(8,5): GAE002 float → use fixed", d.ToLogLine());
        Assert.Single(Gae(Run(Head + "    void Update() { double d = 2; var x = 1.5; }\n}\n"), "GAE002").Where(x => x.Message.StartsWith("double")).Take(1));
    }

    [Fact]
    public void Gae002NotReportedForFixed()
    {
        var r = Run(Head + "    fixed coyoteTime = 0.1;\n    void Update() { coyoteTime = coyoteTime * 2 + 0.5; }\n}\n");
        Assert.Empty(Gae(r, "GAE002"));
        Assert.True(r.Success, TestSupport.Errors(r));
    }

    [Fact]
    public void Gae003NewArrayInUpdate()
    {
        var d = Assert.Single(Gae(Run(Head + "    void Update() { int[] hits = new int[4]; hits[0] = 1; }\n}\n"), "GAE003"));
        Assert.Equal(ScriptSeverity.Warning, d.Severity);
        Assert.Contains("allocate it in Start", d.Message);
    }

    [Fact]
    public void Gae003NotReportedInStart()
    {
        var r = Run(Head + "    int[] hits = new int[4];\n    void Start() { int[] more = new int[2]; hits[0] = more[0]; }\n}\n");
        Assert.Empty(Gae(r, "GAE003"));
        Assert.True(r.Success, TestSupport.Errors(r));
    }

    [Fact]
    public void Gae004TryAndThrow()
    {
        var r = Run(Head + "    void Update()\n    {\n        try { lives(); } catch { }\n        throw null;\n    }\n    void lives() { }\n}\n");
        var ds = Gae(r, "GAE004").ToList();
        Assert.Equal(2, ds.Count);
        Assert.All(ds, d => Assert.Equal(ScriptSeverity.Error, d.Severity));
        Assert.Equal(["'try' is not supported: exceptions are not available on GBA", "'throw' is not supported: exceptions are not available on GBA"], ds.Select(d => d.Message));
    }

    [Fact]
    public void Gae004NotReportedWithoutExceptions()
    {
        Assert.Empty(Gae(Run(Head + "    bool Hit(int hp) { if (hp <= 0) return false; return true; }\n}\n"), "GAE004"));
    }

    [Fact]
    public void Gae005StringConcatenationInLoop()
    {
        var r = Run(Head + "    void Start()\n    {\n        string s = \"\";\n        for (int i = 0; i < 3; i++) s += \"x\";\n        while (s == \"\") { s = s + \"y\"; }\n    }\n}\n");
        Assert.Equal(2, Gae(r, "GAE005").Count());
    }

    [Fact]
    public void Gae005NotReportedOutsideLoops()
    {
        Assert.Empty(Gae(Run(Head + "    void Start() { string s = \"a\"; s = s + \"b\"; }\n}\n"), "GAE005"));
    }

    [Fact]
    public void Gae006CapturingLambda()
    {
        var r = Run(Head + "    void Start()\n    {\n        int bonus = 3;\n        System.Func<int, int> add = x => x + bonus;\n    }\n}\n");
        var d = Assert.Single(Gae(r, "GAE006"));
        Assert.Contains("bonus", d.Message);
    }

    [Fact]
    public void Gae006NotReportedForStaticOrPureLambdas()
    {
        var r = Run(Head + "    void Start()\n    {\n        System.Func<int, int> twice = x => x * 2;\n        System.Func<int, int> neg = static x => -x;\n    }\n}\n");
        Assert.Empty(Gae(r, "GAE006"));
    }

    [Fact]
    public void SeverityPerRuleIsConfigurable()
    {
        const string src = Head + "    float t;\n}\n";
        var ctx = TestSupport.Context();
        ctx.Analyzer.Rules["GAE002"] = RuleSeverity.Error;
        Assert.Equal(ScriptSeverity.Error, Assert.Single(Gae(Run(src, ctx), "GAE002")).Severity);
        ctx.Analyzer.Rules["GAE002"] = RuleSeverity.None;
        Assert.Empty(Gae(Run(src, ctx), "GAE002"));
        ctx.Analyzer.Rules.Clear();
        ctx.Analyzer.TreatWarningsAsErrors = true;
        var r = Run(src, ctx);
        Assert.Equal(ScriptSeverity.Error, Assert.Single(Gae(r, "GAE002")).Severity);
        Assert.False(r.Success);
    }

    [Fact]
    public void ProjectFileSeveritiesAreRead()
    {
        var s = AnalyzerSettings.From(new Dictionary<string, string> { ["GAE001"] = "Error", ["GAE003"] = "None", ["GAE005"] = "Info" }, true);
        Assert.Equal(RuleSeverity.Error, s.Rules["GAE001"]);
        Assert.Equal(RuleSeverity.None, s.Rules["GAE003"]);
        Assert.Equal(RuleSeverity.Info, s.Rules["GAE005"]);
        Assert.True(s.TreatWarningsAsErrors);
    }

    [Fact]
    public void FigmaRuleTitlesAndDescription()
    {
        var titles = AgbStudio.Analyzers.GaeRules.All.Select(d => d.Id + " " + d.Title).ToList();
        Assert.Equal(
        [
            "GAE001 class allocated in Update / OnTouch (no garbage collector)",
            "GAE002 float or double — the GBA has no FPU, use fixed",
            "GAE003 new inside Update — allocate in Start or use a pool",
            "GAE004 try / throw — exceptions are not supported",
            "GAE005 string concatenation in a loop",
            "GAE006 lambda that captures variables",
        ], titles);
        Assert.Equal("The GBA has no FPU, so floats are emulated and very slow. Use the 24.8 fixed-point type instead — same syntax, runs at full speed.",
            AgbStudio.Analyzers.GaeRules.FloatType.Description.ToString());
    }
}
