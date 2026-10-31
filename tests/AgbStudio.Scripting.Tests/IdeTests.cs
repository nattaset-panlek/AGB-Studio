using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace AgbStudio.Scripting.Tests;

/// <summary>P14.02 (generated .csproj), P14.06 (API reference assembly, autocomplete), G-DOCS.01 (generated API docs).</summary>
public sealed partial class IdeTests
{
    // External IDE spelling: Fixed (alias of the GBA fixed type), because `fixed` is reserved in standard C#.
    const string IdeScript = """
        using GBA;

        class Spark { }

        public class PlayerController : Behaviour
        {
            [Range(0.5, 3)] public Fixed speed = 1.5;
            float coyoteTime = 0.1f;
            Spark spark;

            void Update()
            {
                spark = new Spark();
                if (Input.Pressed(Key.A)) Sound.Play("jump", 0.8);
            }
        }

        """;

    [GeneratedRegex(@"(?<file>[^\s(]+\.cs)\((?<line>\d+),(?<col>\d+)\): (?<sev>warning|error) (?<id>GAE\d{3}):")]
    private static partial Regex BuildLine();

    [Fact]
    public void GeneratedProjectBuildsWithTheSameGaeDiagnostics()
    {
        var root = TestSupport.Scratch("ide-project");
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        var script = Path.Combine(root, "scripts", "PlayerController.cs");
        File.WriteAllText(script, IdeScript.Replace("\r\n", "\n"));
        var ctx = TestSupport.Context();
        ctx.ProjectRoot = root;
        ctx.EmitProjectHeaders = false;
        var inProcess = new ScriptCompiler().Compile([ScriptSource.FromFile(script)], ctx, Path.Combine(root, "build", "gen", "scripts"));
        var expected = inProcess.Diagnostics.Where(d => d.Id.StartsWith("GAE")).Select(d => $"{d.Id} {d.Line},{d.Column} {d.Severity}").Order().ToList();
        Assert.Equal(["GAE001 13,17 Error", "GAE002 8,5 Warning"], expected);

        var written = CsprojGenerator.Write(root, "SkyHopper", ctx, inProcess);
        Assert.Contains(written, f => f.EndsWith("SkyHopper.csproj"));
        Assert.True(File.Exists(Path.Combine(root, ".vscode", "settings.json")));
        Assert.Contains("dotnet_diagnostic.GAE001.severity = error", File.ReadAllText(Path.Combine(root, ".editorconfig")));
        Assert.Empty(CsprojGenerator.Write(root, "SkyHopper", ctx, inProcess));  // unchanged: nothing rewritten

        var (exit, output) = Run("dotnet", "build SkyHopper.csproj -nologo -clp:NoSummary", root);
        var fromBuild = BuildLine().Matches(output).Select(m => $"{m.Groups["id"].Value} {m.Groups["line"].Value},{m.Groups["col"].Value} {(m.Groups["sev"].Value == "error" ? "Error" : "Warning")}")
            .Distinct().Order().ToList();
        Assert.True(fromBuild.SequenceEqual(expected), output);
        Assert.NotEqual(0, exit);  // GAE001 is an error by default
        Assert.DoesNotContain("CS8032", output);  // the analyzer assemblies load in the command-line compiler
        Assert.DoesNotContain("error CS", output);

        // With GAE001 set to warning in Project Settings · Scripting, the IDE build succeeds.
        ctx.Analyzer.Rules["GAE001"] = RuleSeverity.Warning;
        CsprojGenerator.Write(root, "SkyHopper", ctx, inProcess);
        var (exit2, output2) = Run("dotnet", "build SkyHopper.csproj -nologo -clp:NoSummary", root);
        Assert.True(exit2 == 0, output2);
    }

    static (int Exit, string Output) Run(string exe, string args, string dir)
    {
        var psi = new ProcessStartInfo(exe, args) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        // The test host runs inside MSBuild / dotnet test: do not leak its MSBuild settings into the child build.
        foreach (var key in psi.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || k.StartsWith("MSBUILD", StringComparison.Ordinal) || k == "DOTNET_HOST_PATH").ToList())
            psi.Environment.Remove(key);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(TimeSpan.FromMinutes(5))) p.Kill(true);
        return (p.ExitCode, stdout.Result + stderr.Result);
    }

    // ---- P14.06: autocomplete lists the members of P6.09 ---------------------------------------------------

    public static TheoryData<string, string[]> Completions() => new()
    {
        { "Input.", ["Pressed", "Held", "Released", "AxisX", "AxisY", "AnyKey"] },
        { "Key.", ["A", "B", "Select", "Start", "Right", "Left", "Up", "Down", "R", "L"] },
        { "Sound.", ["Play"] },
        { "Music.", ["Play", "Stop"] },
        { "Camera.", ["X", "Y", "Follow", "Shake", "SetPosition"] },
        { "Scene.", ["Load", "Pause", "Resume", "Find"] },
        { "Save.", ["Write", "Read", "Checkpoint", "scene"] },
        { "Text.", ["Show"] },
        { "Debug.", ["Log"] },
        { "Items.", ["Get", "Count"] },
        { "Game.", ["score", "coins"] },
        { "Fixed.", ["Abs", "Min", "Max", "Clamp", "Sin", "Cos", "Sqrt", "FromRaw"] },
        { "other.", ["Is", "Get", "Destroy", "tag", "pos", "vel"] },
        { "body.", ["vx", "vy", "OnGround", "Jump", "gravity", "jumpForce", "maxSpeed"] },
        { "this.", ["Get", "actor", "enabled"] },
    };

    [Theory]
    [MemberData(nameof(Completions))]
    public async Task AutocompleteListsTheApiMembers(string prefix, string[] members)
    {
        var ctx = TestSupport.Context();
        const string marker = "/*here*/";
        var code = "using GBA;\n\npublic class T : Behaviour\n{\n    PlatformerBody body;\n    void OnTouch(Actor other)\n    {\n        " + prefix + marker + "\n    }\n}\n";
        var ws = new AdhocWorkspace();
        var project = ws.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Scripts", "Scripts", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: ApiReference.All));
        project = project.AddDocument(ProjectApi.CSharpFileName, SourceText.From(ProjectApi.CSharp(ctx, ctx.Vars))).Project;
        var doc = project.AddDocument("T.cs", SourceText.From(code));
        var position = code.IndexOf(marker, StringComparison.Ordinal);
#pragma warning disable CS0618
        var symbols = await Microsoft.CodeAnalysis.Recommendations.Recommender.GetRecommendedSymbolsAtPositionAsync(doc, position);
#pragma warning restore CS0618
        var names = symbols.Select(s => s.Name).ToHashSet();
        foreach (var m in members)
        {
            Assert.Contains(m, names);
            var s = symbols.First(x => x.Name == m);
            // IntelliSense shows the XML documentation of the reference assembly (or the generated project API).
            Assert.False(string.IsNullOrWhiteSpace(s.GetDocumentationCommentXml()), $"{prefix}{m} has no documentation");
        }
        Assert.DoesNotContain("CppAttribute", names);
    }

    // ---- G-DOCS.01 ------------------------------------------------------------------------------------------

    [Fact]
    public void ApiDocsCoverEveryPublicMember()
    {
        var members = ApiDocs.Members();
        Assert.True(members.Count > 150, members.Count.ToString());
        var missing = members.Where(m => m.Summary.Length == 0).Select(m => m.Type + "." + m.Name).ToList();
        Assert.True(missing.Count == 0, string.Join(", ", missing));
        foreach (var t in new[] { "Input", "Key", "Behaviour", "Actor", "Sound", "Music", "Camera", "Save", "Text", "fixed", "fixed2", "Scene", "Debug", "Random" })
            Assert.Contains(members, m => m.Type == t);
        Assert.DoesNotContain(members, m => m.Type is "CppAttribute" or "AssetAttribute" or "ScriptOpAttribute" or "AssetKind");
        var dir = TestSupport.Scratch("api-docs");
        var files = ApiDocs.Write(dir);
        Assert.Contains(files, f => f.EndsWith("index.html"));
        Assert.Contains("GAE002", File.ReadAllText(Path.Combine(dir, "subset.html")));
        var input = File.ReadAllText(Path.Combine(dir, "Input.html"));
        Assert.Contains("Pressed(Key key)", input);
        Assert.Contains("True on the frame the key goes down.", input);
    }

    [Fact]
    public void ReferenceAssemblyShipsWithXmlDocs()
    {
        Assert.True(File.Exists(ApiReference.ApiAssemblyPath));
        Assert.True(File.Exists(ApiReference.ApiDocumentationPath));
        Assert.Contains("M:GBA.Input.Pressed(GBA.Key)", File.ReadAllText(ApiReference.ApiDocumentationPath));
    }
}
