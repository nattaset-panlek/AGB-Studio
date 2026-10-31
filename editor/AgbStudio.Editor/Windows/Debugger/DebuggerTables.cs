using System.Globalization;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Debugger;

/// <summary>
/// Debugger › Profiler (P13.01): the frame time of the last 120 frames large, against the 16.7 ms budget, and this
/// frame's split in milliseconds and percent of the budget, with fps, VBlank time free and CPU.
/// </summary>
public sealed class ProfilerView : StackPanel
{
    readonly DebuggerModel _m;
    readonly FrameGraph _graph = new() { Height = 200, Width = 640, HorizontalAlignment = HorizontalAlignment.Left, Name = "ProfilerGraph" };
    readonly StackPanel _rows = new() { Name = "ProfilerRows" };
    readonly TextBlock _summary = UI.Text("", "Label/11", "text/muted");
    static readonly (string Header, double Width)[] Columns = [("Part", 200), ("Time", 140), ("Of the frame budget", 300)];

    public ProfilerView(DebuggerModel m)
    {
        _m = m;
        Name = "ProfilerView";
        Margin = new Thickness(16, 14, 16, 16);
        var title = UI.Row(10, UI.Text("Frame time · last 120 frames", "Title/14", "text/primary"), _summary);
        title.Margin = new Thickness(0, 0, 0, 10);
        Children.Add(title);
        Children.Add(UI.Frame(_graph, "bg/canvas", "border/default", new Thickness(1), new Thickness(8), 6).Named("ProfilerGraphBox"));
        var heading = UI.Text("This frame", "Title/14", "text/primary");
        heading.Margin = new Thickness(0, 24, 0, 10);
        Children.Add(heading);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (h, w) in Columns) header.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = h, Width = w });
        Children.Add(new StackPanel { Width = 640, HorizontalAlignment = HorizontalAlignment.Left, Children = { header, _rows } });
    }

    /// <summary>The rows shown now: part, time, percent of the budget.</summary>
    public IReadOnlyList<string> RowTexts => _rows.Children.OfType<StackPanel>().Select(r => string.Join(" | ", r.Children.OfType<TableCell>().Select(c => c.Text))).ToList();

    public void Refresh()
    {
        var play = _m.Play;
        _summary.Text = string.Create(CultureInfo.InvariantCulture, $"{play.Fps:0.0} fps · CPU {play.CpuPercent:0}% · VBlank free {play.VBlankFreeMs:0.0} ms");
        _rows.Children.Clear();
        if (play.Profile is not { } p) return;
        _graph.SetFrames(p.History.Select(c => ProfilerStats.Ms(c)).ToArray());
        foreach (var (part, cycles) in new[] { ("Logic", p.Logic), ("Render", p.Render), ("Audio", p.Audio), ("Idle", p.Idle) })
        {
            var ms = ProfilerStats.Ms(cycles);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Name = "ProfilerRow_" + part };
            row.Children.Add(new TableCell { Text = part, Width = Columns[0].Width });
            row.Children.Add(new TableCell { Kind = TableCellKind.Mono, Text = ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms", Width = Columns[1].Width });
            row.Children.Add(new TableCell { Kind = TableCellKind.Mono, Text = (ms / FrameGraph.BudgetMs * 100).ToString("0", CultureInfo.InvariantCulture) + "%", Width = Columns[2].Width });
            _rows.Children.Add(row);
        }
    }
}

/// <summary>
/// Debugger › Breakpoints (P13.06): every breakpoint with its kind and where it stops, a check box to turn it on or
/// off and a button to remove it; a field to add a C# or script line breakpoint ("PlayerController.cs:19"); and,
/// while the game is stopped, where and the call stack. Breakpoints are also toggled in the Script Editor's line
/// number gutter or with F9.
/// </summary>
public sealed class BreakpointsView : StackPanel
{
    readonly DebuggerModel _m;
    readonly StackPanel _rows = new() { Name = "BreakpointRows" };
    readonly TextBlock _paused = UI.Text("", "Label/11", "text/muted");
    readonly StackPanel _stack = new() { Spacing = 6, Name = "BreakpointsCallStack" };
    static readonly (string Header, double Width)[] Columns = [("Breakpoint", 230), ("Kind", 120), ("Stops at", 230), ("", 60)];

    public TextField AddField { get; } = new() { Text = "", Help = "A script line, for example PlayerController.cs:19", Name = "AddBreakpointField", Width = 230 };
    public AgbButton AddButton { get; } = new() { Content = "Add breakpoint", Glyph = "plus", Kind = ButtonKind.Secondary, Size = ButtonSize.SM, Name = "AddBreakpointButton" };
    public string? Error { get; private set; }

    public BreakpointsView(DebuggerModel m)
    {
        _m = m;
        Name = "BreakpointsView";
        Margin = new Thickness(16, 14, 16, 16);
        var title = UI.Text("Breakpoints", "Title/14", "text/primary");
        title.Margin = new Thickness(0, 0, 0, 10);
        Children.Add(title);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (h, w) in Columns) header.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = h, Width = w });
        Children.Add(new StackPanel { Width = 640, HorizontalAlignment = HorizontalAlignment.Left, Children = { header, _rows } });
        var add = UI.Row(8, AddField, AddButton);
        add.Margin = new Thickness(0, 12, 0, 0);
        Children.Add(add);
        AddButton.Click += (_, _) => Add();
        AddField.AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Enter) { Add(); e.Handled = true; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var stackTitle = UI.Row(10, UI.Text("Call stack", "Title/14", "text/primary"), _paused);
        stackTitle.Margin = new Thickness(0, 24, 0, 10);
        Children.Add(stackTitle);
        Children.Add(_stack);
        m.Breakpoints.CollectionChanged += (_, _) => Refresh();
    }

    /// <summary>Adds the breakpoint typed in the field ("File.cs:line"); a wrong entry says how to write it.</summary>
    public bool Add()
    {
        var text = (AddField.Text ?? "").Trim();
        var at = text.LastIndexOf(':');
        if (at <= 0 || !int.TryParse(text[(at + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var line) || line < 1 || !text[..at].EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            Error = $"\"{text}\" is not a script line. Write the script file and the line, for example PlayerController.cs:19.";
            AddField.Help = Error;
            return false;
        }
        Error = null;
        AddField.Help = "A script line, for example PlayerController.cs:19";
        _m.AddBreakpoint(text[..at], line);
        AddField.Text = "";
        Refresh();
        return true;
    }

    public IReadOnlyList<string> RowTexts => _rows.Children.OfType<StackPanel>().Select(r => string.Join(" | ", r.Children.OfType<TableCell>().Select(c => c.Text))).ToList();

    public void Refresh()
    {
        _rows.Children.Clear();
        foreach (var b in DebuggerModel.InListOrder(_m.Breakpoints))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Name = "BreakpointRow_" + b.Label };
            var box = new CheckBox { Content = b.Label, IsChecked = b.Enabled, Width = Columns[0].Width - 12, Margin = new Thickness(12, 0, 0, 0), Name = "BreakpointRowCheck_" + b.Label };
            var bp = b;
            box.IsCheckedChanged += (_, _) => bp.Enabled = box.IsChecked == true;
            row.Children.Add(box);
            row.Children.Add(new TableCell { Text = b.Kind switch { BreakpointKind.CSharpLine => "C# line", BreakpointKind.ScriptLine => "Script line", _ => "Scene load" }, Width = Columns[1].Width });
            row.Children.Add(new TableCell { Kind = TableCellKind.Mono, Text = _m.StopsAt(b), Width = Columns[2].Width });
            var remove = new IconButton { Glyph = "x", Name = "RemoveBreakpoint_" + b.Label };
            ToolTip.SetTip(remove, new TipContent("Remove breakpoint"));
            remove.Click += (_, _) => _m.RemoveBreakpoint(bp);
            row.Children.Add(remove);
            _rows.Children.Add(row);
        }
        _paused.Text = _m.PauseReason ?? (_m.IsPaused ? "Paused" : "Running · the game stops at an enabled breakpoint");
        _stack.Children.Clear();
        foreach (var r in _m.CallStack) _stack.Children.Add(UI.Text(r.Text, "Mono/11", "text/secondary"));
    }
}
