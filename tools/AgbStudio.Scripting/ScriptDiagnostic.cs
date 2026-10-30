using System.Text.RegularExpressions;
using AgbStudio.Analyzers;
using Microsoft.CodeAnalysis;

namespace AgbStudio.Scripting;

public enum ScriptSeverity { Info, Warning, Error }

/// <summary>A compile message with its source position (1-based line and column).</summary>
public sealed record ScriptDiagnostic(
    string Id,
    ScriptSeverity Severity,
    string Message,
    string File,
    int Line,
    int Column,
    int EndLine = 0,
    int EndColumn = 0,
    string? ShortMessage = null)
{
    public bool IsError => Severity == ScriptSeverity.Error;

    /// <summary>
    /// Build log line as in the Figma Build log: <c>PlayerController.cs(8,5): GAE002 float → use fixed</c> for analyzer
    /// rules, <c>PlayerController.cs(22,9): ';' expected</c> for compiler errors.
    /// </summary>
    public string ToLogLine()
    {
        var where = File.Length == 0 ? "" : $"{Path.GetFileName(File)}({Line},{Column}): ";
        return Id.StartsWith("GAE", StringComparison.Ordinal)
            ? $"{where}{Id} {ShortMessage ?? Message}"
            : $"{where}{Message}";
    }

    public override string ToString() => $"{File}({Line},{Column}): {Severity.ToString().ToLowerInvariant()} {Id}: {Message}";

    internal static ScriptDiagnostic From(Diagnostic d, Func<string, string> displayPath)
    {
        var span = d.Location.GetMappedLineSpan();
        var file = span.IsValid ? displayPath(span.Path) : "";
        var sev = d.Severity switch
        {
            DiagnosticSeverity.Error => ScriptSeverity.Error,
            DiagnosticSeverity.Warning => ScriptSeverity.Warning,
            _ => ScriptSeverity.Info,
        };
        string? shortMessage = null;
        if (d.Id.StartsWith("GAE", StringComparison.Ordinal))
        {
            var args = d.Properties.TryGetValue(GaeRules.ArgsProperty, out var a) && a != null ? a.Split('|') : [];
            shortMessage = GaeRules.ShortMessage(d.Id, args);
        }
        var message = d.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        // Build log style of the Figma design: "';' expected" (Roslyn writes "; expected").
        if (d.Id.StartsWith("CS", StringComparison.Ordinal) && Regex.IsMatch(message, @"^\S{1,2} expected$")) message = $"'{message[..message.IndexOf(' ')]}' expected";
        return new ScriptDiagnostic(d.Id, sev, message, file,
            span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1, shortMessage);
    }
}

/// <summary>Maps g++ messages from generated C++ back to the .cs lines through the <c>#line</c> directives.</summary>
public static partial class GccLogMapper
{
    [GeneratedRegex(@"^(?<file>.+?\.(?:cs|dlg|graph)):(?<line>\d+):(?:(?<col>\d+):)?\s*(?<sev>fatal error|error|warning|note):\s*(?<msg>.*)$")]
    private static partial Regex LineRegex();

    /// <summary>Diagnostics for every g++ error or warning that points into a script file.</summary>
    public static IReadOnlyList<ScriptDiagnostic> Map(string gccOutput)
    {
        var list = new List<ScriptDiagnostic>();
        foreach (var raw in gccOutput.Split('\n'))
        {
            var m = LineRegex().Match(raw.TrimEnd('\r'));
            if (!m.Success) continue;
            var sev = m.Groups["sev"].Value switch
            {
                "warning" => ScriptSeverity.Warning,
                "note" => ScriptSeverity.Info,
                _ => ScriptSeverity.Error,
            };
            var col = m.Groups["col"].Success ? int.Parse(m.Groups["col"].Value) : 1;
            list.Add(new ScriptDiagnostic("CXX", sev, m.Groups["msg"].Value, m.Groups["file"].Value.Trim(),
                int.Parse(m.Groups["line"].Value), col));
        }
        return list;
    }
}
