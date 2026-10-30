using System.Text;

namespace AgbStudio.Scripting.Translation;

/// <summary>
/// Writes generated C++ with <c>#line</c> directives so g++ errors and debugger lines point at the .cs file.
/// A directive is written only when the next line would otherwise map to a different source line.
/// </summary>
internal sealed class CppWriter
{
    readonly StringBuilder _sb = new();
    readonly string _selfName;
    int _physical = 1;          // line number of the next line in this file
    string? _mappedFile;        // null = lines map to this file itself
    int _mappedLine;            // source line the next written line maps to
    public int Indent { get; set; }

    public CppWriter(string selfName) => _selfName = selfName;

    /// <summary>Writes a line mapped to a source line (or continuing the current mapping when line is 0).</summary>
    public void Line(string text, string? file = null, int line = 0)
    {
        if (file != null && line > 0 && (_mappedFile != file || _mappedLine != line))
        {
            Raw($"#line {line} \"{file.Replace('\\', '/')}\"");
            _mappedFile = file;
            _mappedLine = line;
        }
        Raw(text.Length == 0 ? "" : new string(' ', Indent * 4) + text);
        if (_mappedFile != null) _mappedLine++;
    }

    /// <summary>Lines after this map to the generated file itself again.</summary>
    public void Unmap()
    {
        if (_mappedFile == null) return;
        _mappedFile = null;
        Raw($"#line {_physical + 1} \"{_selfName}\"");
    }

    /// <summary>Writes text without indentation or mapping (preprocessor lines, blank lines).</summary>
    public void Raw(string text)
    {
        _sb.Append(text).Append('\n');
        _physical++;
    }

    public void Blank() => Line("");

    public override string ToString() => _sb.ToString();
}
