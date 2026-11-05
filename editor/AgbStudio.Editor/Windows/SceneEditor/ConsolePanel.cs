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

    /// <summary>Adds a line made elsewhere with its own time (the game's log lines from Play Mode).</summary>
    public void Add(ConsoleEntry e)
    {
        _entries.Add(e);
        if (_entries.Count > MaxEntries) _entries.RemoveRange(0, _entries.Count - MaxEntries);
        Changed?.Invoke();
    }

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
        else if (r.Diagnostics.Any(d => d.Message == "Build cancelled."))
        {
            Add(ConsoleLevel.Warning, "Build cancelled · the last built ROM is unchanged");
        }
        else
        {
            var errors = r.Diagnostics.Count(d => d.Severity == Severity.Error);
            var at = r.FailedStep.Length > 0 ? " at " + r.FailedStep : "";
            Add(ConsoleLevel.Error, errors switch
            {
                0 => $"Build failed{at}. Build › Build ROM shows which step stopped and why.",
                1 => $"Build failed{at}: 1 error above. Fix it, then build again.",
                _ => $"Build failed{at}: {errors} errors above. Fix them, then build again.",
            });
        }
    }
}

public enum ConsoleFilter { All, Warnings, Errors }

/// <summary>
/// Console panel (T-P05, 860 x 250; bottom panel tab #21): the log with All / Warnings / Errors filters and their counts
/// (drawn at the right of the panel tab row as in Figma). A click selects a line; Ctrl, Shift and dragging select several;
/// Ctrl+C, right click or the "..." menu copy them, so a problem can be passed on as text. A double click on a line with a
/// file opens it in the Script Editor (T-P04) at that line.
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
    readonly IconButton _more = new() { Glyph = "dots", Name = "ConsoleMenu" };
    readonly HashSet<ConsoleEntry> _selected = new(ReferenceEqualityComparer.Instance);  // two lines can read the same
    int _anchor = -1;
    bool _dragging;
    const double LineHeight = 22;
    ConsoleFilter _filter;

    public ConsolePanel(WindowContext ctx) : this(ctx.Services) { }

    public ConsolePanel(EditorServices s)
    {
        _s = s;
        _log = ConsoleLog.Of(s);
        Name = "ConsolePanel";
        _scroll = new ScrollViewer { Content = _lines, Padding = new Thickness(12, 5, 12, 8), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Children.Add(_scroll);
        Focusable = true;
        _lines.Background = Avalonia.Media.Brushes.Transparent;  // the gaps in a line select it too
        // Tunnel: before a line's own text selection takes the press.
        _lines.AddHandler(PointerPressedEvent, LinePressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _lines.AddHandler(PointerMovedEvent, LineDragged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _lines.AddHandler(PointerReleasedEvent, (_, _) => _dragging = false, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, Keys, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ToolTip.SetTip(_more, "Copy, clear and more");
        _more.Click += (_, _) => ShowMenu(_more);
        Filters = UI.Row(4, _all, _warnings, _errors, _more);
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
        // New lines follow the end of the log unless you have scrolled up to read.
        var atEnd = _scroll.Offset.Y + _scroll.Viewport.Height >= _scroll.Extent.Height - LineHeight;
        var visible = Visible;
        _selected.IntersectWith(visible);
        _lines.Children.Clear();
        foreach (var e in visible)
        {
            var line = new ConsoleLine { Time = e.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture), Text = e.Text, Level = e.Level, Height = LineHeight, Tag = e };
            line.Classes.Set("selected", _selected.Contains(e));
            if (e.File != null) ToolTip.SetTip(line, "Double click to open the code");
            _lines.Children.Add(line);
        }
        if (atEnd) _scroll.ScrollToEnd();
    }

    // ---- selecting lines ---------------------------------------------------------------------------------------

    /// <summary>The selected lines, in the order of the log.</summary>
    public IReadOnlyList<ConsoleEntry> Selected => Visible.Where(_selected.Contains).ToList();

    void ShowSelection()
    {
        foreach (var line in _lines.Children.OfType<ConsoleLine>()) line.Classes.Set("selected", line.Tag is ConsoleEntry e && _selected.Contains(e));
    }

    int LineAt(Point p, int count) => count == 0 ? -1 : Math.Clamp((int)(p.Y / LineHeight), 0, count - 1);

    void SelectRange(IReadOnlyList<ConsoleEntry> visible, int from, int to)
    {
        _selected.Clear();
        for (var i = Math.Min(from, to); i <= Math.Max(from, to); i++) _selected.Add(visible[i]);
    }

    public void SelectAll()
    {
        _selected.UnionWith(Visible);
        ShowSelection();
    }

    void LinePressed(object? sender, PointerPressedEventArgs e)
    {
        var visible = Visible;
        var i = LineAt(e.GetPosition(_lines), visible.Count);
        if (i < 0) return;
        Focus();
        e.Handled = true;
        var point = e.GetCurrentPoint(_lines).Properties;
        if (point.IsRightButtonPressed)
        {
            if (!_selected.Contains(visible[i]))
            {
                _selected.Clear();
                _selected.Add(visible[i]);
                _anchor = i;
                ShowSelection();
            }
            ShowMenu(_lines);
            return;
        }
        if (!point.IsLeftButtonPressed) return;
        if (e.ClickCount == 2 && visible[i].File != null)
        {
            Open(visible[i]);
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _anchor >= 0 && _anchor < visible.Count) SelectRange(visible, _anchor, i);
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (!_selected.Remove(visible[i])) _selected.Add(visible[i]);
            _anchor = i;
        }
        else
        {
            _selected.Clear();
            _selected.Add(visible[i]);
            _anchor = i;
            _dragging = true;
        }
        ShowSelection();
    }

    void LineDragged(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;
        if (!e.GetCurrentPoint(_lines).Properties.IsLeftButtonPressed) { _dragging = false; return; }
        var visible = Visible;
        var i = LineAt(e.GetPosition(_lines), visible.Count);
        if (i < 0 || _anchor < 0 || _anchor >= visible.Count) return;
        SelectRange(visible, _anchor, i);
        ShowSelection();
    }

    void Keys(object? sender, KeyEventArgs e)
    {
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (control && e.Key == Key.C && _selected.Count > 0) { _ = Copy(Selected); e.Handled = true; }
        else if (control && e.Key == Key.A) { SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Escape && _selected.Count > 0) { _selected.Clear(); ShowSelection(); e.Handled = true; }
    }

    // ---- copy, clear, save ------------------------------------------------------------------------------------

    /// <summary>Lines as text to paste into a report: time, level, the message and the file it points at.</summary>
    public static string TextOf(IEnumerable<ConsoleEntry> lines) => string.Join(Environment.NewLine, lines.Select(e =>
        $"{e.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} [{e.Level}] {e.Text}{(e.File != null ? $"  ({e.File}{(e.Line > 0 ? $":{e.Line}" : "")})" : "")}"));

    async Task Copy(IReadOnlyList<ConsoleEntry> lines)
    {
        if (lines.Count == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(TextOf(lines));
        _s.Toasts.Show(lines.Count == 1 ? "Copied 1 line." : $"Copied {lines.Count} lines.", ToastStatus.Success, TimeSpan.FromSeconds(2));
    }

    /// <summary>build/console.log of the project, with the lines the filter shows; returns its path, or null without a project.</summary>
    public string? SaveToFile()
    {
        if (_s.Project.Current is not { } p) return null;
        Directory.CreateDirectory(p.BuildDir);
        var path = Path.Combine(p.BuildDir, "console.log");
        File.WriteAllText(path, TextOf(Visible) + Environment.NewLine);
        _s.Toasts.Show($"Saved the Console to {path}.", ToastStatus.Success, TimeSpan.FromSeconds(4));
        return path;
    }

    /// <summary>What the "..." button and a right click offer.</summary>
    public IReadOnlyList<(string Text, string? Keys, bool Enabled, Action Run)> MenuEntries() =>
    [
        ($"Copy Selected ({_selected.Count})", "Ctrl+C", _selected.Count > 0, () => _ = Copy(Selected)),
        ("Copy All", null, Visible.Count > 0, () => _ = Copy(Visible)),
        ("Select All", "Ctrl+A", Visible.Count > 0, SelectAll),
        ("Save to File", null, Visible.Count > 0 && _s.Project.Current != null, () => SaveToFile()),
        ("Clear", null, _log.Entries.Count > 0, _log.Clear),
    ];

    void ShowMenu(Control anchor)
    {
        var items = MenuEntries().Select(entry =>
        {
            var item = new MenuItem { Header = entry.Text, IsEnabled = entry.Enabled, InputGesture = entry.Keys != null ? KeyGesture.Parse(entry.Keys) : null };
            item.Click += (_, _) => entry.Run();
            return item;
        }).ToList();
        new ContextMenu { ItemsSource = items, Placement = anchor == _more ? PlacementMode.BottomEdgeAlignedRight : PlacementMode.Pointer }.Open(anchor);
    }

    /// <summary>Opens the line's file at its line, as Preferences › "Open .cs with" says (the Script Editor or the external editor).</summary>
    public void Open(ConsoleEntry e)
    {
        if (e.File == null) return;
        _ = Scripts.ScriptEditorPage.OpenScriptFile(_s, e.File, Math.Max(1, e.Line), source: "ConsoleLine");
    }
}
