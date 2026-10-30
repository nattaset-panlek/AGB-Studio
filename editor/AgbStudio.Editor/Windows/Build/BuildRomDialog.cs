using AgbStudio.Build;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Build;

/// <summary>
/// Build ROM (T-D06, 600 x 440; #10 Build, Build › Build ROM, Build Report › Build again). Starts a build with
/// <see cref="BuildService"/> and shows its real stream: the steps with time and detail, per-file progress of the
/// running step and the log (P11.57). When a step fails it closes and opens Build ROM · Failed (T-D23).
/// </summary>
[EditorWindow("T-D06")]
public sealed class BuildRomDialog : FigmaDialog
{
    readonly EditorServices _s;
    readonly BuildRequest _request;
    readonly StackPanel _steps = new() { Spacing = 14, Name = "BuildSteps" };
    readonly StackPanel _log = new() { Spacing = 2, Name = "BuildLog" };
    readonly ScrollViewer _logScroll;
    readonly DialogHeader _header;
    readonly DialogFooter _footer;
    readonly string _rom;
    bool _runWhenDone;

    public BuildStream Stream { get; }
    public Task<BuildResult?> Completion { get; private set; } = Task.FromResult<BuildResult?>(null);
    public string Title => _header.Title ?? "";
    public event Action<BuildResult>? Completed;

    public BuildRomDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as BuildRequest) { }

    /// <param name="start">False shows the dialog without starting a build (tests drive <see cref="Stream"/> directly).</param>
    public BuildRomDialog(EditorServices s, BuildRequest? request = null, bool start = true, Func<DateTime>? clock = null)
    {
        _s = s;
        _request = request ?? new BuildRequest();
        _runWhenDone = _request.RunWhenDone;
        Name = "BuildRomDialog";
        var p = s.Project.Current;
        _rom = p is null ? "game.gba" : Path.GetFileName(new GameBuilder(p, new Toolchain()).RomPath);
        Stream = new BuildStream(clock, p?.Settings.Build.Optimise ?? "-O2");
        Stream.Changed += Render;

        _logScroll = new ScrollViewer { Content = _log, Height = 66, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden };
        var logBox = UI.Frame(_logScroll, "bg/base", pad: new Thickness(10, 9, 10, 9), radius: 8).Named("LogBox");
        var content = UI.Column(0, _steps, logBox);
        logBox.Margin = new Thickness(0, 15, 0, 0);
        content.Margin = new Thickness(20, 20, 20, 21);

        _footer = new DialogFooter { CancelText = "Close", ConfirmText = "Run in Emulator", Hint = BuildStream.TypicalTimeHint(p), Name = "BuildFooter" };
        _footer.Cancel += (_, _) => Close(Stream.Result);
        _footer.Confirm += (_, _) => RunInEmulator();
        Build($"Building {_rom}…", "zap", content, _footer, 600);
        _header = this.GetLogicalDescendantsOfType<DialogHeader>().First();
        Render();
        if (start) Completion = RunAsync();
    }

    async Task<BuildResult?> RunAsync()
    {
        var p = _s.Project.Current;
        if (p is null || _s.Build.IsBuilding)
        {
            Stream.Log.Add(new BuildLogLine(DateTime.Now, ConsoleLevel.Error, p is null ? "No project is open." : "A build is already running."));
            Render();
            return null;
        }
        BuildResult result;
        try
        {
            result = await _s.Build.BuildAsync(new BuildOptions { Configuration = _request.Configuration }, Stream);
            // the stream's last events were posted from the build thread: apply them before the result
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            result = new BuildResult();
            result.Diagnostics.Add(new Diagnostic(Severity.Error, "", 0, 0, "", e.Message));
        }
        Finish(result);
        return result;
    }

    /// <summary>Applies the end of a build: status bar, history, export copy, Run in Emulator, or Build ROM · Failed.</summary>
    public void Finish(BuildResult result)
    {
        Stream.Complete(result);
        var p = _s.Project.Current;
        var kb = File.Exists(result.RomPath) ? new FileInfo(result.RomPath).Length / 1024 : 0;
        _s.Status.Build = result.Success ? $"Build OK · {result.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s" : "Build failed";
        if (result.Success) _s.Status.Rom = $"ROM {kb} KB / 32 MB";
        Completed?.Invoke(result);
        if (!result.Success)
        {
            if (p != null) BuildStream.RecordFailure(p, result, DateTime.Now);
            Close(result);
            _ = _s.Windows.Open("T-D23", Stream.Failure(_rom), source: "BuildRomFailed");
            return;
        }
        _header.Title = $"Built {_rom}";
        if (_request.ExportTo is { } dest) Export(result.RomPath, dest, _request.PadToPowerOfTwo);
        if (_runWhenDone || (_request.Configuration is null && _request.ExportTo is null && p?.Settings.Build.RunAfterBuild == true)) RunInEmulator();
    }

    void Export(string rom, string dest, bool pad)
    {
        try
        {
            var bytes = File.ReadAllBytes(rom);
            if (pad) bytes = ExportRomDialog.PadRom(bytes);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dest))!);
            File.WriteAllBytes(dest, bytes);
            _s.Toasts.Show($"Exported {Path.GetFileName(dest)}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _s.Toasts.Show($"Could not write {Path.GetFileName(dest)}: {e.Message}", ToastStatus.Error);
        }
    }

    /// <summary>Opens Play Mode with the ROM; while the build is still running it does so when the build succeeds.</summary>
    public void RunInEmulator()
    {
        if (Stream.Result is not { Success: true } r) { _runWhenDone = true; return; }
        Close(r);
        _ = _s.Windows.Open("T-P02", r.RomPath, source: "BuildRomRunInEmulator");
    }

    void Render()
    {
        _steps.Children.Clear();
        foreach (var step in Stream.Steps) _steps.Children.Add(StepRow(step));
        _log.Children.Clear();
        foreach (var l in Stream.Log) _log.Children.Add(LogLine(l));
        _logScroll.ScrollToEnd();
    }

    /// <summary>A step row as Figma draws it: status icon, title (Body/12 Semibold) and detail, progress for the running step.</summary>
    public static Control StepRow(BuildStepView s)
    {
        var (glyph, iconToken, titleToken) = s.State switch
        {
            StepState.Done => ("check", "status/success", "text/primary"),
            StepState.Running => ("refresh", "accent/default", "text/primary"),
            StepState.Failed => ("x", "status/danger", "status/danger"),
            _ => ("clock", "text/muted", "text/muted"),
        };
        var icon = new Icon { Glyph = glyph, Size = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 0, 0) }.Token(Icon.ForegroundProperty, iconToken);
        var title = UI.Text(s.Title, "Body/12 Semibold", titleToken);
        var detail = UI.Text(s.Detail, "Label/11", "text/muted");
        detail.Margin = new Thickness(0, 4, 0, 0);
        var col = UI.Column(0, title, detail);
        if (s.State == StepState.Running && s.Total > 0)
        {
            var bar = new AgbProgress { Status = ProgressStatus.Accent, Maximum = s.Total, Value = s.Done, Margin = new Thickness(0, 3, 0, 0), Name = "StepProgress" };
            col.Children.Add(bar);
        }
        var row = new DockPanel { HorizontalSpacing = 12, Name = "Step_" + s.Title };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.AddRange([icon, col]);
        return row;
    }

    public static ConsoleLine LogLine(BuildLogLine l) =>
        new() { Time = l.Time.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), Level = l.Level, Text = l.Text };
}

/// <summary>
/// Build ROM · Failed (T-D23, 600 x 382; Build ROM when a step fails). Shows the failed step, the step that ran before
/// it and the skipped steps, and the build's errors and warnings; clicking a message or "Go to first error" opens the
/// file at that line.
/// </summary>
[EditorWindow("T-D23")]
public sealed class BuildFailedDialog : FigmaDialog
{
    readonly EditorServices _s;

    public BuildFailure Failure { get; }
    public IReadOnlyList<BuildStepView> ShownSteps { get; }
    public string Title { get; }
    public string? Opened { get; private set; }

    public BuildFailedDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as BuildFailure ?? new BuildFailure("game.gba", [], [])) { }

    public BuildFailedDialog(EditorServices s, BuildFailure failure)
    {
        _s = s;
        Failure = failure;
        Name = "BuildFailedDialog";
        ShownSteps = StepsToShow(failure.Steps);
        var steps = new StackPanel { Spacing = 14, Name = "FailedSteps" };
        foreach (var step in ShownSteps) steps.Children.Add(BuildRomDialog.StepRow(step));
        var lines = new StackPanel { Spacing = 2, Name = "ErrorLines" };
        foreach (var m in failure.Messages)
        {
            var msg = m;
            var b = new Button { Content = BuildRomDialog.LogLine(m), Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            b.Click += (_, _) => Open(msg.Diagnostic);
            lines.Children.Add(b);
        }
        var box = UI.Frame(new ScrollViewer { Content = lines, MaxHeight = 66, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, "bg/base", pad: new Thickness(10, 9, 10, 9), radius: 8).Named("ErrorBox");
        box.Margin = new Thickness(0, 15, 0, 0);
        var content = UI.Column(0, steps, box);
        content.Margin = new Thickness(20, 20, 20, 21);
        var errors = failure.Errors.Count();
        Title = errors == 1 ? "Build failed · 1 error" : $"Build failed · {errors} errors";
        var footer = new DialogFooter { CancelText = "Close", ConfirmText = "Go to first error", Hint = "Fix them, then build again." };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => Open(failure.Errors.FirstOrDefault(e => e.Diagnostic?.File.Length > 0)?.Diagnostic ?? failure.Errors.FirstOrDefault()?.Diagnostic);
        Build(Title, "x", content, footer, 600);
    }

    /// <summary>The failed step, the step the pipeline ran just before it, and the steps that were skipped (pipeline order).</summary>
    public static IReadOnlyList<BuildStepView> StepsToShow(IReadOnlyList<BuildStepView> steps)
    {
        var ordered = BuildStream.PipelineOrder.Select(t => steps.FirstOrDefault(s => s.Title == t)).Where(s => s != null).Select(s => s!).ToList();
        var failed = ordered.FindIndex(s => s.State == StepState.Failed);
        if (failed < 0) return ordered;
        var list = new List<BuildStepView>();
        if (failed > 0) list.Add(ordered[failed - 1]);
        list.AddRange(ordered.Skip(failed));
        return list;
    }

    /// <summary>Opens the message's file: C# scripts in the Script Editor (T-P04) at the line, other files in Explorer.</summary>
    public void Open(Diagnostic? d)
    {
        if (d is null || d.File.Length == 0) return;
        var p = _s.Project.Current;
        var file = d.File;
        if (!Path.IsPathRooted(file) && p != null)
            file = new[] { Path.Combine(p.Root, file), Path.Combine(p.ScriptsDir, file) }.FirstOrDefault(File.Exists)
                   ?? p.ScriptFiles().FirstOrDefault(f => Path.GetFileName(f) == Path.GetFileName(file)) ?? Path.Combine(p.Root, file);
        Opened = file;
        Close(d);
        if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var doc = _s.Documents.Open.FirstOrDefault(x => x.Path != null && string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
                      ?? _s.Documents.Add(new EditorDocument { Name = Path.GetFileName(file), Glyph = "code", Path = file });
            _s.Documents.Active = doc;
            _s.Set(new SourceLocation(file, d.Line, d.Column));
            _ = _s.Windows.Open("T-P04", doc, source: "BuildFailedGoToError");
        }
        else _s.System.ShowInExplorer(file);
    }
}

/// <summary>The file and line a "go to error" asked for (the Script Editor reads it with Services.Get&lt;SourceLocation&gt;()).</summary>
public sealed record SourceLocation(string File, int Line, int Column);
