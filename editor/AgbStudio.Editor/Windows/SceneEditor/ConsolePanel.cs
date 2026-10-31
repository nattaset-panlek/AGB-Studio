using System.Globalization;
using AgbStudio.Build.Pipeline;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>One Console line: time, level, text and the code it points at (file and line), if any.</summary>
public sealed record ConsoleEntry(DateTime Time, ConsoleLevel Level, string Text, string? File = null, int Line = 0);

/// <summary>
/// The Console's messages (T-P05): build results and diagnostics from BuildService, the Scene Editor's own messages
/// and game logs (Play Mode and the Debugger add theirs with <see cref="Add"/>). Shared through
/// <c>ConsoleLog.Of(services)</c>.
/// </summary>
public sealed class ConsoleLog
{
    readonly List<ConsoleEntry> _entries = [];
    public const int MaxEntries = 1000;

    public static ConsoleLog Of(EditorServices s)
    {
        if (s.GetService(typeof(ConsoleLog)) is ConsoleLog c) return c;
        c = new ConsoleLog();
        s.Set(c);
        s.Build.Finished += r => Avalonia.Threading.Dispatcher.UIThread.Post(() => c.AddBuild(r));
        return c;
    }

    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;
    public IReadOnlyList<ConsoleEntry> Entries => _entries;
    public int Warnings => _entries.Count(e => e.Level == ConsoleLevel.Warning);
    public int Errors => _entries.Count(e => e.Level == ConsoleLevel.Error);
    public event Action? Changed;

    public void Add(ConsoleLevel level, string text, string? file = null, int line = 0)
    {
        _entries.Add(new ConsoleEntry(Clock(), level, text, file, line));
        if (_entries.Count > MaxEntries) _entries.RemoveRange(0, _entries.Count - MaxEntries);
        Changed?.Invoke();
    }

    public void Info(string text) => Add(ConsoleLevel.Info, text);

    public void Clear()
    {
        _entries.Clear();
        Changed?.Invoke();
    }

    /// <summary>A build result: its diagnostics (file and line kept), then "Build OK in 0.80 s · sky_hopper.gba 412 KB" or the failure.</summary>
    public void AddBuild(BuildResult r)
    {
        foreach (var d in r.Diagnostics.Where(d => d.Severity != Severity.Info))
        {
            var where = d.File.Length > 0 && d.Line > 0 ? $"{Path.GetFileName(d.File)}({d.Line}): " : d.File.Length > 0 ? Path.GetFileName(d.File) + ": " : "";
            Add(d.Severity == Severity.Error ? ConsoleLevel.Error : ConsoleLevel.Warning, where + (d.Code.Length > 0 ? d.Code + " " : "") + d.Message,
                d.File.Length > 0 ? d.File : null, d.Line);
        }
        if (r.Success)
        {
            var size = File.Exists(r.RomPath) ? new FileInfo(r.RomPath).Length : 0;
            Add(ConsoleLevel.Success, string.Create(CultureInfo.InvariantCulture, $"Build OK in {r.Elapsed.TotalSeconds:0.00} s · {Path.GetFileName(r.RomPath)} {(size + 1023) / 1024} KB"));
        }
        else
        {
            Add(ConsoleLevel.Error, "Build failed" + (r.FailedStep.Length > 0 ? " at " + r.FailedStep : ""));
        }
    }
}

public enum ConsoleFilter { All, Warnings, Errors }

/// <summary>
/// Console panel (T-P05, 860 x 250; bottom panel tab #21): the log with All / Warnings / Errors filters and their counts
/// (drawn at the right of the panel tab row as in Figma); clicking a line with a file opens it in the Script Editor
/// (T-P04) at that line.
/// </summary>
[EditorWindow("T-P05", Kind = WindowKind.Panel)]
public sealed class ConsolePanel : Panel
{
    readonly EditorServices _s;
    readonly ConsoleLog _log;
    readonly StackPanel _lines = new() { Spacing = 0, Name = "ConsoleLines" };
    readonly ScrollViewer _scroll;
    readonly Chip _all = new() { Content = "All", Name = "Filter_All" };
    readonly Chip _warnings = new() { Name = "Filter_Warnings" };
    readonly Chip _errors = new() { Name = "Filter_Errors" };
    ConsoleFilter _filter;

    public ConsolePanel(WindowContext ctx) : this(ctx.Services) { }

    public ConsolePanel(EditorServices s)
    {
        _s = s;
        _log = ConsoleLog.Of(s);
        Name = "ConsolePanel";
        _scroll = new ScrollViewer { Content = _lines, Padding = new Thickness(12, 5, 12, 8), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Children.Add(_scroll);
        Filters = UI.Row(4, _all, _warnings, _errors);
        Filters.HorizontalAlignment = HorizontalAlignment.Right;
        Filters.VerticalAlignment = VerticalAlignment.Center;
        Filters.Margin = new Thickness(0, 0, 8, 0);
        Filters.Name = "ConsoleFilters";
        _all.Click += (_, _) => Filter = ConsoleFilter.All;
        _warnings.Click += (_, _) => Filter = ConsoleFilter.Warnings;
        _errors.Click += (_, _) => Filter = ConsoleFilter.Errors;
        _log.Changed += Refresh;
        DetachedFromVisualTree += (_, _) =>
        {
            _log.Changed -= Refresh;
            if (_tabRow != null) AdornerLayer.SetAdorner(_tabRow, null);
            _tabRow = null;
        };
        AttachedToVisualTree += (_, _) =>
        {
            _log.Changed -= Refresh;
            _log.Changed += Refresh;
            PlaceFilters();
            Refresh();
        };
        Refresh();
    }

    /// <summary>All / Warnings / Errors. In the Scene Editor they sit at the right of the panel tab row (Figma), as an
    /// adorner of that row; a Console shown on its own has them at its top right.</summary>
    public StackPanel Filters { get; }
    Control? _tabRow;

    void PlaceFilters()
    {
        if (Filters.Parent is Panel p) p.Children.Remove(Filters);
        var row = this.GetVisualAncestors().OfType<SceneWorkspace>().FirstOrDefault()?.GetVisualDescendants().OfType<PanelTab>()
            .FirstOrDefault(t => t.Name == "Tab_Console")?.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Height == 33);
        if (row != null && AdornerLayer.GetAdornerLayer(row) != null)
        {
            _tabRow = row;
            AdornerLayer.SetAdorner(row, Filters);
            // the layer places the adorner from the row's position, known once the panel has been laid out
            Avalonia.Threading.Dispatcher.UIThread.Post(() => AdornerLayer.GetAdornerLayer(row)?.InvalidateArrange(), Avalonia.Threading.DispatcherPriority.Loaded);
            return;
        }
        Filters.VerticalAlignment = VerticalAlignment.Top;
        Filters.Margin = new Thickness(0, 4, 8, 0);
        Children.Add(Filters);
    }

    public ConsoleFilter Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            Refresh();
        }
    }

    public IReadOnlyList<ConsoleEntry> Visible => _log.Entries.Where(Shows).ToList();

    bool Shows(ConsoleEntry e) => _filter switch
    {
        ConsoleFilter.Warnings => e.Level == ConsoleLevel.Warning,
        ConsoleFilter.Errors => e.Level == ConsoleLevel.Error,
        _ => true,
    };

    void Refresh()
    {
        _warnings.Content = $"Warnings · {_log.Warnings}";
        _errors.Content = $"Errors · {_log.Errors}";
        _all.Style = _filter == ConsoleFilter.All ? ChipStyle.Accent : ChipStyle.Default;
        _warnings.Style = _filter == ConsoleFilter.Warnings ? ChipStyle.Accent : _log.Warnings > 0 ? ChipStyle.Warning : ChipStyle.Default;
        _errors.Style = _filter == ConsoleFilter.Errors ? ChipStyle.Accent : _log.Errors > 0 ? ChipStyle.Danger : ChipStyle.Default;
        _lines.Children.Clear();
        foreach (var e in Visible)
        {
            var line = new ConsoleLine { Time = e.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture), Text = e.Text, Level = e.Level, Height = 22, Tag = e };
            if (e.File != null)
            {
                line.Cursor = new Cursor(StandardCursorType.Hand);
                var entry = e;
                line.AddHandler(PointerPressedEvent, (_, args) =>
                {
                    if (args.GetCurrentPoint(line).Properties.IsLeftButtonPressed) Open(entry);
                }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
            _lines.Children.Add(line);
        }
        _scroll.ScrollToEnd();
    }

    /// <summary>Opens the line's file at its line, as Preferences › "Open .cs with" says (the Script Editor or the external editor).</summary>
    public void Open(ConsoleEntry e)
    {
        if (e.File == null) return;
        _ = Scripts.ScriptEditorPage.OpenScriptFile(_s, e.File, Math.Max(1, e.Line), source: "ConsoleLine");
    }
}
