using System.Globalization;
using System.Text;
using AgbStudio.Editor.Shell;
using Avalonia.Threading;

namespace AgbStudio.Editor.Services;

/// <summary>
/// G-ERR.06: when the editor hits an unexpected error (UI thread, a background thread or an unobserved task), it first
/// saves the open documents that have changes (as autosave does), then writes a crash log to
/// %APPDATA%\AGBStudio\crash\crash-&lt;time&gt;.log: when, the AGB Studio version, the project, which documents were saved,
/// and the error. An error on the UI thread is then shown as a toast and the editor keeps running.
/// </summary>
public sealed class CrashGuard : IDisposable
{
    readonly EditorServices _s;
    readonly Func<DateTime> _clock;
    bool _installed;

    public CrashGuard(EditorServices s, string? folder = null, Func<DateTime>? clock = null)
    {
        _s = s;
        Folder = folder ?? Path.Combine(PreferencesStore.DefaultFolder, "crash");
        _clock = clock ?? (() => DateTime.Now);
    }

    public string Folder { get; }
    /// <summary>The last crash log written (null before any).</summary>
    public string? LastLog { get; private set; }

    public void Install()
    {
        if (_installed) return;
        _installed = true;
        Dispatcher.UIThread.UnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        TaskScheduler.UnobservedTaskException += OnTaskException;
    }

    public void Dispose()
    {
        if (!_installed) return;
        _installed = false;
        Dispatcher.UIThread.UnhandledException -= OnUiException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainException;
        TaskScheduler.UnobservedTaskException -= OnTaskException;
    }

    void OnUiException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var log = Handle(e.Exception);
        e.Handled = true;
        // the full path: "crash folder" alone left users looking for the log
        _s.Toasts.Show(log.Length > 0
            ? $"AGB Studio hit an unexpected error and saved your changes. Details are in {log}."
            : $"AGB Studio hit an unexpected error and saved your changes. The crash log could not be written to {Folder}.", Controls.ToastStatus.Error, TimeSpan.FromSeconds(12));
    }

    void OnDomainException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) Handle(ex);
    }

    void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // The same error comes here again when the garbage collector finds the task it ran in: a second log would replace
        // the first, which says what was saved.
        if (_lastError == null || !e.Exception.Flatten().InnerExceptions.Contains(_lastError)) Handle(e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// Where the editor was when it failed, so a log alone tells what the user did: system, install folder (a zip's
    /// temporary folder shows here), page, previous page, the last windows opened and by what, overlays, active and open
    /// documents, Play state. Each line is read on its own: one that throws is written as unreadable, never hides the error.
    /// </summary>
    public string Context()
    {
        var sb = new StringBuilder();
        void Line(string name, Func<string?> read)
        {
            string value;
            try { value = read() ?? "none"; }
            catch (Exception e) when (e is not OutOfMemoryException) { value = "unreadable (" + e.GetType().Name + ")"; }
            sb.Append(name).Append(": ").AppendLine(value);
        }
        Line("OS", () => Environment.OSVersion.VersionString + (Environment.Is64BitProcess ? " x64" : " x86"));
        Line(".NET", () => Environment.Version.ToString());
        Line("Install folder", () => AppContext.BaseDirectory + (ZipLaunch.Current() is { } zip ? $" (running from inside {zip})" : ""));
        Line("Thread", () => Dispatcher.UIThread.CheckAccess() ? "UI" : "background " + Environment.CurrentManagedThreadId);
        Line("Page", () => _s.Windows.CurrentPage);
        Line("Previous page", () => _s.Windows.PreviousPage);
        Line("Last windows opened", () => string.Join(" > ", _s.Windows.Requests.TakeLast(12).Select(r => r.Source is null ? r.Window : $"{r.Window} ({r.Source})")));
        Line("Overlays", () => _s.Top is MainWindow w && w.Overlays.IsOpen ? string.Join(", ", w.Overlays.Open.Select(o => o.Name ?? o.GetType().Name)) : null);
        Line("Active document", () => _s.Documents.Active is { } d ? d.Path ?? d.Name : null);
        Line("Open documents", () => string.Join(", ", _s.Documents.Open.Select(d => d.Name + (d.IsDirty ? " *" : ""))));
        Line("Play", () => _s.GetService(typeof(Windows.Play.PlayController)) is Windows.Play.PlayController p
            ? $"{p.State}{(p.IsPaused ? ", paused" : "")}, frame {p.Frame}, started as {p.StartedAs}, ROM {p.RomPath ?? "none"}" : "not started");
        return sb.ToString();
    }

    Exception? _lastError;

    /// <summary>Saves the changed documents and writes the crash log; returns the log path.</summary>
    public string Handle(Exception error)
    {
        _lastError = error;
        var saved = new List<string>();
        var failed = new List<string>();
        foreach (var d in _s.Documents.Dirty.ToList())
        {
            try
            {
                // save handlers write files; give each a few seconds (a handler stuck on the UI thread must not hang the crash path)
                if (d.SaveHandler is { } save && save(d).Wait(TimeSpan.FromSeconds(5))) { d.IsDirty = false; saved.Add(d.Name); }
                else failed.Add(d.Name);
            }
            catch (Exception e) when (e is not OutOfMemoryException) { failed.Add($"{d.Name} ({e.GetBaseException().Message})"); }
        }
        var now = _clock();
        var text = new StringBuilder()
            .AppendLine("AGB Studio crash log")
            .AppendLine("Time: " + now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .AppendLine("Version: " + (typeof(CrashGuard).Assembly.GetName().Version?.ToString() ?? "?"))
            .AppendLine("Project: " + (_s.Project.Current?.ProjectPath ?? "none"))
            .AppendLine("Saved before the crash: " + (saved.Count == 0 ? "nothing to save" : string.Join(", ", saved)))
            .AppendLine(failed.Count == 0 ? "Not saved: none" : "Not saved: " + string.Join(", ", failed))
            .Append(Context())
            .AppendLine()
            .AppendLine(error.ToString())
            .ToString();
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"crash-{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.log");
            File.WriteAllText(path, text);
            LastLog = path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LastLog = null; }
        return LastLog ?? "";
    }
}
