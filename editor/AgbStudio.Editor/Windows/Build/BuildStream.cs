using System.Text.Json;
using System.Text.RegularExpressions;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Build;

public enum StepState { Pending, Running, Done, Failed, Skipped }

/// <summary>One row of the Build ROM dialog.</summary>
public sealed class BuildStepView(string title)
{
    public string Title { get; } = title;
    public StepState State { get; set; }
    public string Detail { get; set; } = "—";
    public string Result { get; set; } = "";
    public double Seconds { get; set; }
    public int Done { get; set; }
    public int Total { get; set; }
    public string File { get; set; } = "";
    public BuildStepView Clone() => (BuildStepView)MemberwiseClone();
}

/// <summary>A line of the build log: time, level and text; build messages keep their <see cref="Diagnostic"/>.</summary>
public sealed record BuildLogLine(DateTime Time, ConsoleLevel Level, string Text, Diagnostic? Diagnostic = null);

/// <summary>What the Build ROM dialog asks for: configuration, an export copy (Export ROM) and Run in Emulator afterwards.</summary>
public sealed record BuildRequest(BuildConfiguration? Configuration = null, string? ExportTo = null, bool PadToPowerOfTwo = false, bool RunWhenDone = false);

/// <summary>What Build ROM · Failed shows: the step rows at the moment of failure and the build messages.</summary>
public sealed record BuildFailure(string Rom, IReadOnlyList<BuildStepView> Steps, IReadOnlyList<BuildLogLine> Messages)
{
    public IEnumerable<BuildLogLine> Errors => Messages.Where(m => m.Level == ConsoleLevel.Error);
}

/// <summary>
/// The real build stream (P11.57): an <see cref="IBuildObserver"/> that <see cref="Services.BuildService"/> passes to
/// GameBuilder. Events arrive on the build thread and are applied on the UI thread to the four steps the dialog shows
/// in Figma order (C# → C++, Convert assets, Compile, Link + gbafix; the pipeline converts assets first, issue 1.4-8).
/// "Generate game" counts towards C# → C++; Link + gbafix follows the LINK / GBAFIX lines of the native build.
/// </summary>
public sealed class BuildStream : IBuildObserver
{
    public static readonly string[] UiOrder = [GameBuilder.StepScripts, GameBuilder.StepAssets, GameBuilder.StepCompile, GameBuilder.StepLink];
    /// <summary>The order the pipeline runs the steps in.</summary>
    public static readonly string[] PipelineOrder = [GameBuilder.StepAssets, GameBuilder.StepScripts, GameBuilder.StepCompile, GameBuilder.StepLink];
    const string StepGenerate = "Generate game";

    readonly Func<DateTime> _clock;
    readonly string _optimise;
    bool _firstCompile = true;

    public List<BuildStepView> Steps { get; } = [.. UiOrder.Select(t => new BuildStepView(t))];
    public List<BuildLogLine> Log { get; } = [];
    public BuildResult? Result { get; private set; }
    public event Action? Changed;

    public BuildStream(Func<DateTime>? clock = null, string optimise = "-O2")
    {
        _clock = clock ?? (() => DateTime.Now);
        _optimise = optimise;
    }

    public BuildStepView Step(string title) => Steps.First(s => s.Title == title);

    void Ui(Action a)
    {
        if (Dispatcher.UIThread.CheckAccess()) { a(); Changed?.Invoke(); }
        else Dispatcher.UIThread.Post(() => { a(); Changed?.Invoke(); });
    }

    public void StepStarted(string step, string detail) => Ui(() =>
    {
        var s = step == StepGenerate ? Step(GameBuilder.StepScripts) : Steps.FirstOrDefault(x => x.Title == step);
        if (s is null) return;
        s.State = StepState.Running;
        if (step != StepGenerate) s.Detail = detail.Length > 0 ? detail : "";
    });

    public void StepFinished(string step, TimeSpan elapsed, bool ok, string detail) => Ui(() =>
    {
        var s = step == StepGenerate ? Step(GameBuilder.StepScripts) : Steps.FirstOrDefault(x => x.Title == step);
        if (s is null) return;
        s.Seconds += elapsed.TotalSeconds;
        if (step != StepGenerate) s.Result = detail;
        if (ok)
        {
            s.State = StepState.Done;
            if (step == GameBuilder.StepCompile)
            {
                s.Detail = $"{s.Total} files · {Secs(s.Seconds)} s";
                var link = Step(GameBuilder.StepLink);
                if (link.State != StepState.Failed) link.State = StepState.Done;
            }
            else s.Detail = $"{s.Result} · {Secs(s.Seconds)} s";
            return;
        }
        Fail(s);
    });

    void Fail(BuildStepView s)
    {
        s.State = StepState.Failed;
        var file = s.File.Length > 0 ? s.File : Log.FirstOrDefault(l => l.Level == ConsoleLevel.Error && l.Diagnostic?.File.Length > 0)?.Diagnostic?.File;
        s.Detail = file is { Length: > 0 } ? "stopped at " + Path.GetFileName(file) : "stopped";
        foreach (var other in Steps.Where(x => x.State == StepState.Pending || (x.State == StepState.Running && x != s)))
        {
            other.State = StepState.Skipped;
            other.Detail = "skipped";
        }
    }

    public void FileProgress(string step, int done, int total, string file) => Ui(() =>
    {
        var s = Steps.FirstOrDefault(x => x.Title == step);
        if (s is null) return;
        s.Done = done;
        s.Total = total;
        s.File = file;
        if (s.State == StepState.Running) s.Detail = $"{Path.GetFileName(file)} · {done} / {total}";
    });

    static readonly Regex Ninja = new(@"^\[\d+/\d+\]\s+(CXX|LINK|GBAFIX)\s+(.*)$", RegexOptions.Compiled);

    public void Line(string text) => Ui(() =>
    {
        var m = Ninja.Match(text.Trim());
        if (!m.Success) return;
        var file = Path.GetFileName(m.Groups[2].Value.Trim());
        switch (m.Groups[1].Value)
        {
            case "CXX":
                Log.Add(new BuildLogLine(_clock(), ConsoleLevel.Info, _firstCompile ? $"g++ -mthumb -mcpu=arm7tdmi {_optimise} {file}" : $"g++ … {file}"));
                _firstCompile = false;
                break;
            case "LINK":
                var link = Step(GameBuilder.StepLink);
                link.State = StepState.Running;
                link.Detail = file;
                Log.Add(new BuildLogLine(_clock(), ConsoleLevel.Info, $"ld … {file}"));
                break;
            default:
                Log.Add(new BuildLogLine(_clock(), ConsoleLevel.Info, $"gbafix {file}"));
                break;
        }
    });

    public void Message(Diagnostic d) => Ui(() =>
    {
        if (d.Severity == Severity.Info) return;
        Log.Add(new BuildLogLine(_clock(), d.Severity == Severity.Error ? ConsoleLevel.Error : ConsoleLevel.Warning, LogText(d), d));
    });

    /// <summary>The Build ROM log form of a message: "GAE002 PlayerController.cs(8): float → use fixed".</summary>
    public static string LogText(Diagnostic d)
    {
        var where = d.File.Length == 0 ? "" : d.Line > 0 ? $"{Path.GetFileName(d.File)}({d.Line}): " : $"{Path.GetFileName(d.File)}: ";
        return (d.Code.Length > 0 ? d.Code + " " : "") + where + d.Message;
    }

    /// <summary>Applies the final result (called on the UI thread when the build task completes).</summary>
    public void Complete(BuildResult r)
    {
        Result = r;
        foreach (var d in r.Diagnostics.Where(d => d.Severity == Severity.Error && !Log.Any(l => l.Diagnostic == d)))
            Log.Add(new BuildLogLine(_clock(), ConsoleLevel.Error, LogText(d), d));
        if (r.Success)
        {
            var link = Step(GameBuilder.StepLink);
            link.State = StepState.Done;
            link.Detail = File.Exists(r.RomPath) ? $"{Path.GetFileName(r.RomPath)} · {PageFrame.Kb(new FileInfo(r.RomPath).Length)}" : Path.GetFileName(r.RomPath);
        }
        else if (!Steps.Any(s => s.State == StepState.Failed))
        {
            var running = Steps.FirstOrDefault(s => s.State == StepState.Running) ?? Steps.FirstOrDefault(s => s.State == StepState.Pending) ?? Steps[^1];
            Fail(running);
        }
        Changed?.Invoke();
    }

    public BuildFailure Failure(string rom) =>
        new(rom, [.. Steps.Select(s => s.Clone())], [.. Log.Where(l => l.Diagnostic != null && l.Level != ConsoleLevel.Info).Select(l => l with { Text = l.Diagnostic!.ToString() })]);

    static string Secs(double s) => s.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    // ---- history of failed builds ------------------------------------------------------------------------

    static readonly JsonSerializerOptions HistoryJson = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static List<BuildReport.HistoryEntry> LoadHistory(GameProject p)
    {
        var path = Path.Combine(p.BuildDir, "report-history.json");
        try { return File.Exists(path) ? JsonSerializer.Deserialize<List<BuildReport.HistoryEntry>>(File.ReadAllText(path), HistoryJson) ?? [] : []; }
        catch (Exception e) when (e is IOException or JsonException) { return []; }
    }

    /// <summary>GameBuilder writes history only for builds that produce a report; failed builds are added here so Build Report lists them.</summary>
    public static void RecordFailure(GameProject p, BuildResult r, DateTime at)
    {
        var first = r.Diagnostics.FirstOrDefault(d => d.Severity == Severity.Error);
        var text = first is null ? "Build failed" : first.Line > 0 && first.File.Length > 0 ? $"{Path.GetFileName(first.File)}({first.Line}): {first.Message}" : first.ToString();
        var history = LoadHistory(p);
        history.Insert(0, new BuildReport.HistoryEntry(at, 0, Math.Round(r.Elapsed.TotalSeconds, 2), r.Diagnostics.Count(d => d.Severity == Severity.Warning), r.Errors.Count(), text));
        Directory.CreateDirectory(p.BuildDir);
        File.WriteAllText(Path.Combine(p.BuildDir, "report-history.json"), JsonSerializer.Serialize(history.Take(20).ToList(), HistoryJson));
    }

    /// <summary>"Usually under a second." from the history of successful builds; null without history.</summary>
    public static string? TypicalTimeHint(GameProject? p)
    {
        if (p is null) return null;
        var times = LoadHistory(p).Where(h => h.Errors == 0 && h.Seconds > 0).Select(h => h.Seconds).Order().ToList();
        if (times.Count == 0) return null;
        var median = times[times.Count / 2];
        return median < 1 ? "Usually under a second." : $"Usually about {Math.Round(median).ToString(System.Globalization.CultureInfo.InvariantCulture)} s.";
    }
}
