namespace AgbStudio.Build.Pipeline;

public enum Severity { Info, Warning, Error }

/// <summary>A build message that the Build ROM dialog and Console can link to (file(line,col): code message).</summary>
public sealed record Diagnostic(Severity Severity, string File, int Line, int Column, string Code, string Message)
{
    public override string ToString()
    {
        var where = File.Length == 0 ? "" : Line > 0 ? $"{Path.GetFileName(File)}({Line},{Column}): " : $"{Path.GetFileName(File)}: ";
        var code = Code.Length > 0 ? Code + " " : "";
        return $"{where}{code}{Message}";
    }
}

/// <summary>Progress events for the Build ROM dialog: steps, per-file progress and messages.</summary>
public interface IBuildObserver
{
    void StepStarted(string step, string detail);
    void StepFinished(string step, TimeSpan elapsed, bool ok, string detail);
    void FileProgress(string step, int done, int total, string file);
    void Line(string text);
    void Message(Diagnostic d);
}

public sealed class NullObserver : IBuildObserver
{
    public static readonly NullObserver Instance = new();
    public void StepStarted(string step, string detail) { }
    public void StepFinished(string step, TimeSpan elapsed, bool ok, string detail) { }
    public void FileProgress(string step, int done, int total, string file) { }
    public void Line(string text) { }
    public void Message(Diagnostic d) { }
}
