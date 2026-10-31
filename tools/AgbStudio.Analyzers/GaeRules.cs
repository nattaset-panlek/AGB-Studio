using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AgbStudio.Analyzers;

/// <summary>The GBA subset rules (Project Settings · Scripting). Titles are the Figma texts.</summary>
public static class GaeRules
{
    public const string Category = "GBA";
    public const string HelpBase = "https://agbstudio.local/docs/scripting/rules#";

    public static readonly DiagnosticDescriptor ClassInUpdate = new(
        "GAE001",
        "class allocated in Update / OnTouch (no garbage collector)",
        "'new {0}' allocates a class in {1}; the GBA has no garbage collector, so this memory is never given back",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "Classes live until the scene ends. Create the object once in Start (it goes into the scene's memory arena) and reuse it.",
        helpLinkUri: HelpBase + "GAE001");

    public static readonly DiagnosticDescriptor FloatType = new(
        "GAE002",
        "float or double — the GBA has no FPU, use fixed",
        "{0} is very slow on GBA (no floating-point hardware)",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        description: "The GBA has no FPU, so floats are emulated and very slow. Use the 24.8 fixed-point type instead — same syntax, runs at full speed.",
        helpLinkUri: HelpBase + "GAE002");

    public static readonly DiagnosticDescriptor NewInUpdate = new(
        "GAE003",
        "new inside Update — allocate in Start or use a pool",
        "'new {0}' inside {1} allocates every frame; allocate it in Start or use a pool",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "Arrays and other allocations made every frame fill the scene's memory arena. Allocate once in Start and reuse the array.",
        helpLinkUri: HelpBase + "GAE003");

    public static readonly DiagnosticDescriptor Exceptions = new(
        "GAE004",
        "try / throw — exceptions are not supported",
        "'{0}' is not supported: exceptions are not available on GBA",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "The GBA runtime is built without exceptions. Return a bool or an error value instead of throwing.",
        helpLinkUri: HelpBase + "GAE004");

    public static readonly DiagnosticDescriptor StringConcatInLoop = new(
        "GAE005",
        "string concatenation in a loop (a new string every pass)",
        "String concatenation in a loop builds a new string on every pass",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        description: "Strings are constant data on GBA. Build text with string keys from Localization, or log values with Debug.Log($\"...\") outside the loop.",
        helpLinkUri: HelpBase + "GAE005");

    public static readonly DiagnosticDescriptor CapturingLambda = new(
        "GAE006",
        "lambda that captures variables (hidden allocation)",
        "This lambda captures '{0}', which needs a hidden allocation",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        description: "A lambda that uses local variables or fields needs a hidden object. Make it static, or pass the values as parameters.",
        helpLinkUri: HelpBase + "GAE006");

    public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        ClassInUpdate, FloatType, NewInUpdate, Exceptions, StringConcatInLoop, CapturingLambda);

    // Short build log texts (Figma Build log: "PlayerController.cs(8,5): GAE002 float → use fixed").
    static readonly Dictionary<string, string> s_short = new()
    {
        ["GAE001"] = "class allocated in {1} is never freed → allocate in Start",
        ["GAE002"] = "{0} is slow on GBA → use fixed",
        ["GAE003"] = "new inside {1} fills memory every frame → allocate in Start or use a pool",
        ["GAE004"] = "{0} (no exceptions on GBA) → return a bool or an error value",
        ["GAE005"] = "string concatenation in a loop makes a new string every pass → use string keys, or log outside the loop",
        ["GAE006"] = "lambda captures {0} (hidden allocation) → pass it as a parameter and make the lambda static",
    };

    /// <summary>Short message for the build log; arguments are the diagnostic's message arguments.</summary>
    public static string ShortMessage(string id, params object[] args)
    {
        if (!s_short.TryGetValue(id, out var format)) return id;
        var text = format;
        for (var i = 0; i < 2; i++) text = text.Replace("{" + i + "}", i < args.Length ? args[i]?.ToString() ?? "" : "");
        return text;
    }

    /// <summary>Key in Diagnostic.Properties holding the message arguments joined with '|'.</summary>
    public const string ArgsProperty = "args";
}
