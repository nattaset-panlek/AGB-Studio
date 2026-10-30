using System.Globalization;
using System.Text.Json;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>Progress of one tutorial: the step it is on, the sub-steps done ("2.0" = step 3, first sub-step) and whether it was finished.</summary>
public sealed class TutorialState
{
    public bool Started { get; set; }
    public int Step { get; set; }
    public List<string> Done { get; set; } = [];
    public bool Completed { get; set; }

    public static string Key(int step, int sub) => step.ToString(CultureInfo.InvariantCulture) + "." + sub.ToString(CultureInfo.InvariantCulture);
    public bool IsDone(int step, int sub) => Done.Contains(Key(step, sub));
}

/// <summary>Tutorial progress per user (tutorials.json next to preferences.json): Start / Continue / Review and the percentage.</summary>
public sealed class TutorialProgressStore
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    readonly Dictionary<string, TutorialState> _states;

    public string FilePath { get; }
    public event Action? Changed;

    public TutorialProgressStore(string filePath)
    {
        FilePath = filePath;
        Dictionary<string, TutorialState>? loaded = null;
        try { if (File.Exists(filePath)) loaded = JsonSerializer.Deserialize<Dictionary<string, TutorialState>>(File.ReadAllText(filePath), Json); }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        _states = loaded ?? [];
    }

    public TutorialState Get(string id) => _states.TryGetValue(id, out var s) ? s : _states[id] = new TutorialState();

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_states, Json));
        Changed?.Invoke();
    }

    /// <summary>Share of sub-steps done (finished tutorials are 100).</summary>
    public int Percent(TutorialInfo t)
    {
        var s = Get(t.Id);
        if (s.Completed) return 100;
        var total = Math.Max(1, t.SubStepCount);
        var done = 0;
        for (var i = 0; i < t.Steps.Count; i++)
            for (var j = 0; j < t.Steps[i].SubSteps.Count; j++)
                if (s.IsDone(i, j)) done++;
        return (int)Math.Round(100.0 * done / total);
    }

    /// <summary>The Learn page row: progress text, whether it is done, and the button (Start / Continue / Review).</summary>
    public (string Text, bool Done, string Button) Row(TutorialInfo t)
    {
        var s = Get(t.Id);
        if (s.Completed) return ("Done", true, "Review");
        if (!s.Started) return (t.Duration, false, "Start");
        return ($"{Percent(t)}% · in progress", false, "Continue");
    }
}

/// <summary>
/// Editor events that complete tutorial sub-steps, observed on the Figma components every window is built from: a
/// selected Tree Item (Hierarchy) or Panel Tab, an active toolbar Icon Button, an Inspector field value, and key presses.
/// Installed once per process; sessions filter by their own main window.
/// </summary>
public static class TutorialSignals
{
    public static event Action<Control, TriggerKind, string, double>? Raised;
    public static event Action<TopLevel, Key>? KeyDown;
    static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        SelectableRow.IsSelectedProperty.Changed.AddClassHandler<SelectableRow>((row, _) =>
        {
            if (!row.IsSelected) return;
            if (row is TreeItem t && t.Label != null) Raised?.Invoke(t, TriggerKind.TreeItemSelected, t.Label, 0);
            else if (row is PanelTab p && p.Label != null) Raised?.Invoke(p, TriggerKind.PanelTab, p.Label, 0);
        });
        IconButton.IsActiveProperty.Changed.AddClassHandler<IconButton>((b, _) =>
        {
            if (b.IsActive && b.Name != null) Raised?.Invoke(b, TriggerKind.ToolActive, b.Name, 0);
        });
        InspectorField.ValueProperty.Changed.AddClassHandler<InspectorField>((f, _) =>
        {
            if (f.Label != null && double.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) Raised?.Invoke(f, TriggerKind.InspectorValue, f.Label, v);
        });
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((top, e) => KeyDown?.Invoke(top, e.Key), RoutingStrategies.Tunnel, handledEventsToo: true);
    }
}

/// <summary>
/// A running tutorial (P11.04): the current step, Back / Skip / Next step, and sub-steps that tick themselves when the
/// editor reports the action (commands, opened windows, selections, Inspector values, Play Mode input).
/// </summary>
public sealed class TutorialSession
{
    readonly LauncherService _launcher;
    bool _active, _playing;

    public TutorialInfo Tutorial { get; private set; } = TutorialCatalog.All[0];
    public TutorialState State => _launcher.Tutorials.Get(Tutorial.Id);
    public int StepIndex => Math.Clamp(State.Step, 0, Tutorial.Steps.Count - 1);
    public TutorialStep Step => Tutorial.Steps[StepIndex];
    public int Percent => _launcher.Tutorials.Percent(Tutorial);
    public bool IsActive => _active;
    public event Action? Changed;
    public event Action? Finished;

    public TutorialSession(LauncherService launcher) => _launcher = launcher;

    EditorServices S => _launcher.Services;

    /// <summary>Starts or continues a tutorial; Review (a finished one) starts again from the first step.</summary>
    public void Start(TutorialInfo t)
    {
        Stop();
        Tutorial = t;
        var st = State;
        if (st.Completed) st.Step = 0;
        st.Started = true;
        _launcher.Tutorials.Save();
        _active = true;
        _playing = (S.Windows.Host as MainWindow)?.CurrentPageId == "T-P02";
        TutorialSignals.Install();
        TutorialSignals.Raised += OnSignal;
        TutorialSignals.KeyDown += OnKey;
        S.Commands.Executed += OnCommand;
        S.Windows.Opened += OnWindow;
        _launcher.ProjectCreated += OnCreated;
        Changed?.Invoke();
    }

    public void Stop()
    {
        if (!_active) return;
        _active = false;
        TutorialSignals.Raised -= OnSignal;
        TutorialSignals.KeyDown -= OnKey;
        S.Commands.Executed -= OnCommand;
        S.Windows.Opened -= OnWindow;
        _launcher.ProjectCreated -= OnCreated;
    }

    public void Back() => Go(StepIndex - 1);

    /// <summary>Next step; after the last step the tutorial is finished.</summary>
    public void Next()
    {
        if (StepIndex >= Tutorial.Steps.Count - 1) { Complete(); return; }
        Go(StepIndex + 1);
    }

    /// <summary>Skip: counts this step as done and moves on.</summary>
    public void Skip()
    {
        for (var j = 0; j < Step.SubSteps.Count; j++) Mark(StepIndex, j, save: false);
        Next();
    }

    public void Toggle(int sub, bool done)
    {
        var key = TutorialState.Key(StepIndex, sub);
        if (done) { if (!State.Done.Contains(key)) State.Done.Add(key); }
        else State.Done.Remove(key);
        _launcher.Tutorials.Save();
        Changed?.Invoke();
    }

    void Go(int step)
    {
        if (step < 0 || step >= Tutorial.Steps.Count) return;
        State.Step = step;
        _launcher.Tutorials.Save();
        Changed?.Invoke();
    }

    void Complete()
    {
        State.Completed = true;
        State.Step = 0;
        _launcher.Tutorials.Save();
        Stop();
        Finished?.Invoke();
    }

    void Mark(int step, int sub, bool save = true)
    {
        var key = TutorialState.Key(step, sub);
        if (State.Done.Contains(key)) return;
        State.Done.Add(key);
        if (!save) return;
        _launcher.Tutorials.Save();
        Changed?.Invoke();
    }

    /// <summary>Completes the sub-steps of the current step that wait for this event.</summary>
    public void Signal(TriggerKind kind, string target, double value = 0)
    {
        if (!_active) return;
        for (var j = 0; j < Step.SubSteps.Count; j++)
            if (!State.IsDone(StepIndex, j) && Step.SubSteps[j].Triggers.Any(t => Matches(t, kind, target, value)))
                Mark(StepIndex, j);
    }

    static bool Matches(Trigger t, TriggerKind kind, string target, double value) =>
        t.Kind == kind && (t.Target.Length == 0 || string.Equals(t.Target, target, StringComparison.OrdinalIgnoreCase))
        && (kind != TriggerKind.InspectorValue || Math.Abs(t.Value - value) < 0.05);

    void OnSignal(Control source, TriggerKind kind, string target, double value)
    {
        if (TopLevel.GetTopLevel(source) is { } top && top == S.Top) Signal(kind, target, value);
    }

    void OnCommand(EditorCommand c)
    {
        if (c.Id is "build.play" or "build.playFromHere" or "build.buildAndRun") _playing = true;
        Signal(TriggerKind.Command, c.Id);
    }

    void OnWindow(string id)
    {
        if (id == "T-P02") _playing = true;
        else if (WindowCatalog.Find(id)?.Kind == WindowKind.Page) _playing = false;
        Signal(TriggerKind.Window, id);
    }

    void OnCreated(string path) => Signal(TriggerKind.ProjectCreated, "");

    /// <summary>In Play Mode a key that the project maps to a GBA button (Project Settings · Input) presses that button.</summary>
    void OnKey(TopLevel top, Key key)
    {
        if (!_playing || top != S.Top) return;
        var name = AgbStudio.Editor.Theme.Shortcuts.KeyName(key);
        var actions = S.Project.Current?.Settings.Input ?? AgbStudio.Core.Project.InputAction.Defaults();
        foreach (var a in actions.Where(a => string.Equals(a.Keyboard, name, StringComparison.OrdinalIgnoreCase)))
            Signal(TriggerKind.PlayButton, a.Button);
    }
}

/// <summary>
/// Shows Tutorial Steps (T-O13) next to the editor without blocking it: the popover sits in the main window's
/// overlay layer under the dialogs, so the user can select, edit and play while the sub-steps tick. It stays when the
/// page changes (opening the project, Play Mode) and closes with its X or when the tutorial is finished.
/// </summary>
public sealed class TutorialHost
{
    readonly LauncherService _launcher;
    TutorialStepsPopover? _view;

    public TutorialSession Session { get; }
    public TutorialStepsPopover? View => _view;
    public bool IsOpen => _view?.Parent != null;

    public TutorialHost(LauncherService launcher)
    {
        _launcher = launcher;
        Session = new TutorialSession(launcher);
        Session.Finished += Hide;
    }

    public void Show(TutorialInfo t, string source)
    {
        Session.Start(t);
        _launcher.Services.Windows.Requests.Add(("T-O13", source));
        if (IsOpen) return;
        _view = new TutorialStepsPopover(Session) { Margin = new Thickness(0, 0, 336, 42), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        _view.Closed += _ => { Session.Stop(); Hide(); };
        if (_launcher.Services.Windows.Host is MainWindow w && w.Overlays.Parent is Panel layer)
            layer.Children.Insert(layer.Children.IndexOf(w.Overlays), _view);
        else
            _ = _launcher.Services.Windows.Host?.Overlays.Show(_view, OverlayPlacement.TopCenter);
    }

    public void Hide()
    {
        if (_view?.Parent is Panel p && p is not OverlayHost) p.Children.Remove(_view);
        else if (_view != null) _launcher.Services.Windows.Host?.Overlays.Close(_view);
        _view = null;
    }
}

/// <summary>
/// Tutorial Steps (T-O13, 380 x 466; Launcher › Learn › Start / Continue): "Your first platformer · 3 of 8", the
/// tutorial progress, the step text and picture, the sub-steps and Back / Skip / Next step.
/// </summary>
[EditorWindow("T-O13")]
public sealed class TutorialStepsPopover : Border, IOverlayContent
{
    readonly TutorialSession _session;
    readonly PopoverHeader _header = new() { Name = "TutorialHeader" };
    readonly AgbProgress _progress = new() { Status = ProgressStatus.Accent, Name = "TutorialProgress" };
    readonly TextBlock _title = UI.Text(null, "Title/14", "text/primary");
    readonly TextBlock _text = UI.Text(null, "Body/12", "text/secondary");
    readonly Border _picture = new() { Width = 338, Height = 140, CornerRadius = new CornerRadius(8), ClipToBounds = true, Margin = new Thickness(0, 14, 0, 0), Name = "TutorialPicture" };
    readonly StackPanel _subSteps = new() { Spacing = 15, Margin = new Thickness(0, 14, 0, 0), Name = "SubSteps" };
    readonly AgbButton _back = new() { Content = "Back", Glyph = "undo", Kind = ButtonKind.Secondary, Name = "TutorialBack" };
    readonly AgbButton _skip = new() { Content = "Skip", Kind = ButtonKind.Ghost, Name = "TutorialSkip" };
    readonly AgbButton _next = new() { Content = "Next step", Glyph = "right", Kind = ButtonKind.Primary, Name = "TutorialNext" };
    bool _building;

    public event Action<object?>? Closed;

    public TutorialStepsPopover(WindowContext ctx) : this(Session(ctx)) { }

    static TutorialSession Session(WindowContext ctx)
    {
        var host = LauncherService.Of(ctx.Services).TutorialHost;
        if (ctx.Args is string id && TutorialCatalog.Find(id) is { } t && (!host.Session.IsActive || host.Session.Tutorial != t)) host.Session.Start(t);
        else if (!host.Session.IsActive) host.Session.Start(host.Session.Tutorial);
        return host.Session;
    }

    public TutorialStepsPopover(TutorialSession session)
    {
        _session = session;
        Name = "TutorialSteps";
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = 380;
        _text.TextWrapping = TextWrapping.Wrap;
        _title.Margin = new Thickness(0, 14, 0, 0);
        _text.Margin = new Thickness(0, 14, 0, 0);
        _header.CloseRequested += (_, _) => Closed?.Invoke(null);
        _back.Click += (_, _) => _session.Back();
        _skip.Click += (_, _) => _session.Skip();
        _next.Click += (_, _) => _session.Next();
        var footer = new DockPanel { Margin = new Thickness(0, 15, 0, 0), HorizontalSpacing = 8 };
        DockPanel.SetDock(_back, Dock.Left);
        DockPanel.SetDock(_next, Dock.Right);
        DockPanel.SetDock(_skip, Dock.Right);
        footer.Children.AddRange([_back, _next, _skip, new Panel()]);
        var body = new StackPanel { Margin = new Thickness(20, 20, 20, 21), Children = { _progress, _title, _text, _picture, _subSteps, footer } };
        var dock = new DockPanel();
        DockPanel.SetDock(_header, Dock.Top);
        dock.Children.AddRange([_header, body]);
        Child = dock;
        _session.Changed += Fill;
        Fill();
    }

    /// <summary>The sub-step check boxes (tests).</summary>
    public IReadOnlyList<CheckBox> SubStepBoxes => _subSteps.Children.OfType<CheckBox>().ToList();
    public string Title => _header.Title ?? "";

    void Fill()
    {
        var t = _session.Tutorial;
        var i = _session.StepIndex;
        var step = _session.Step;
        _header.Title = $"{t.Title} · {i + 1} of {t.Steps.Count}";
        _progress.Value = _session.Percent;
        _title.Text = step.Title;
        _text.Text = step.Text;
        _picture.IsVisible = step.Image != null;
        // the art is scaled to 340 px and cropped to the playfield, as in Figma (60 px of sky cut at the top)
        _picture.Child = step.Image is null ? null : new ArtImage
        {
            Source = step.Image, Width = 340, Height = 340 * 2 / 3.0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -60, -2, 0),
        };
        _building = true;
        _subSteps.Children.Clear();
        for (var j = 0; j < step.SubSteps.Count; j++)
        {
            var k = j;
            var box = new CheckBox { Content = step.SubSteps[j].Text, IsChecked = _session.State.IsDone(i, j), Name = "SubStep" + j };
            box.IsCheckedChanged += (_, _) => { if (!_building) _session.Toggle(k, box.IsChecked == true); };
            _subSteps.Children.Add(box);
        }
        _building = false;
        _back.IsEnabled = i > 0;
    }
}
