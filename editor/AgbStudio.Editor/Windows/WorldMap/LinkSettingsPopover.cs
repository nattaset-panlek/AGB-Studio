using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.WorldMap;

/// <summary>
/// Link Settings (T-O09, 360 x 330; World Map › link chip; P11.51, P5.07): From (trigger actor), To scene, Spawn at,
/// Transition Cut / Fade / Slide, Duration, "Only if" condition. Each change is saved to the scene at once (undoable).
/// </summary>
[EditorWindow("T-O09")]
public sealed class LinkSettingsPopover : Border, IOverlayContent
{
    public const double MaxDuration = 1.25;
    readonly WorldMapModel? _model;
    readonly LinkRef? _ref;
    readonly PopoverHeader _header = new() { Name = "LinkHeader" };
    readonly Segment[] _transitions;
    readonly TextBlock _durationText = UI.Text("", "Mono/11", "text/primary");
    bool _loading;

    public event Action<object?>? Closed;
    public Button From { get; }
    public Button To { get; }
    public Button SpawnAt { get; }
    public AgbSlider Duration { get; } = new() { Name = "Duration", Maximum = 100 };
    public TextBox OnlyIf { get; } = new() { Name = "OnlyIf" };
    public Button ConditionField { get; }
    public Button ConditionOp { get; }
    public TextBox ConditionValue { get; } = new() { Name = "ConditionValue", Height = 28, Width = 56, Padding = new Thickness(8, 0) };
    public const string NoCondition = "None", CustomCondition = "Custom";

    public LinkSettingsPopover(WindowContext ctx) : this(ctx.Args as LinkSettingsArgs) { }

    public LinkSettingsPopover(LinkSettingsArgs? args)
    {
        _model = args?.Model;
        _ref = args?.Link;
        Name = "LinkSettings";
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = 360;
        _header.CloseRequested += (_, _) => Closed?.Invoke(null);
        From = Dropdown("LinkFrom");
        To = Dropdown("LinkTo");
        SpawnAt = Dropdown("SpawnAt");
        _transitions = [.. new[] { "Cut", "Fade", "Slide" }.Select(t => new Segment { Content = t, GroupName = "Transition", Name = "Transition_" + t })];
        foreach (var t in _transitions)
            t.IsCheckedChanged += (_, _) => { if (t.IsChecked == true && !_loading) Change("Transition", l => l.Transition = Enum.Parse<Transition>((string)t.Content!)); };
        var transitions = UI.Frame(UI.Row(2, _transitions), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);
        transitions.HorizontalAlignment = HorizontalAlignment.Left;
        Duration.Width = 174;
        IDisposable? commit = null;
        Duration.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty || _loading) return;
            var seconds = Math.Round(Duration.Value / 100 * MaxDuration / 0.05) * 0.05;
            _durationText.Text = Seconds(seconds);
            commit?.Dispose();
            commit = Avalonia.Threading.DispatcherTimer.RunOnce(() => Change("Duration", l => l.Duration = seconds), TimeSpan.FromMilliseconds(250));
        };
        OnlyIf.Height = 28;
        OnlyIf.Padding = new Thickness(10, 0);
        AgbStudio.Editor.Theme.Fig.SetStyle(OnlyIf, "Mono/11");
        OnlyIf.LostFocus += (_, _) => CommitCondition();
        OnlyIf.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitCondition(); e.Handled = true; } };
        ConditionField = Dropdown("ConditionField");
        ConditionOp = Dropdown("ConditionOp");
        ConditionField.Width = 106;
        ConditionOp.Width = 52;
        ConditionField.Click += (_, _) => Database.Dropdown.Show(ConditionField, [NoCondition, .. LogicGraph.GraphReferences.NumberFields(_model?.Project)], TextOf(ConditionField), PickField);
        ConditionOp.Click += (_, _) => Database.Dropdown.Show(ConditionOp, LinkCondition.Ops, TextOf(ConditionOp), op =>
        {
            if (LinkCondition.Parse(OnlyIf.Text ?? "") is { } c) SetCondition(c with { Op = op });
        });
        AgbStudio.Editor.Theme.Fig.SetStyle(ConditionValue, "Mono/11");
        ConditionValue.LostFocus += (_, _) => CommitValue();
        ConditionValue.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitValue(); e.Handled = true; } };
        var builder = UI.Row(6, ConditionField, ConditionOp, ConditionValue);
        var help = UI.Text("Leave “Only if” empty to always allow the link.", "Label/11", "text/muted");
        help.Margin = new Thickness(0, 3, 0, 0);
        var duration = UI.Row(16, Duration, _durationText);
        duration.VerticalAlignment = VerticalAlignment.Center;
        var rows = UI.Column(10,
            Row("From", From, 28), Row("To scene", To, 28), Row("Spawn at", SpawnAt, 28), Row("Transition", transitions, 34),
            Row("Duration", duration, 28), Row("Only if", OnlyIf, 28), Row("Choose", builder, 28), help);
        rows.Margin = new Thickness(20, 18, 20, 16);
        Child = UI.Column(0, _header, rows);
        Load();
    }

    static string Seconds(double s) => s.ToString("0.0#", CultureInfo.InvariantCulture) + " s";

    static Button Dropdown(string name)
    {
        var text = UI.Text("", "Mono/11", "text/primary");
        var chevron = PageChrome.Glyph("down", 13, "text/muted");
        var dock = new DockPanel { HorizontalSpacing = 8 };
        DockPanel.SetDock(chevron, Dock.Right);
        dock.Children.AddRange([chevron, text]);
        var frame = UI.Frame(dock, "bg/base", "border/default", new Thickness(1), new Thickness(10, 0), 6);
        frame.Height = 28;
        return new Button
        {
            Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = frame, Name = name, Tag = text,
        };
    }

    static void SetText(Button b, string text) => ((TextBlock)b.Tag!).Text = text;
    public static string TextOf(Button b) => ((TextBlock)b.Tag!).Text ?? "";

    static DockPanel Row(string label, Control control, double height)
    {
        var l = UI.Text(label, "Body/12", "text/secondary", 94);
        DockPanel.SetDock(l, Dock.Left);
        control.VerticalAlignment = VerticalAlignment.Center;
        return new DockPanel { Height = height, Children = { l, control } };
    }

    LinkData? Link => _model is null || _ref is null ? null : _model.Link(_ref);
    MapScene? Scene => _ref is null ? null : _model?.Find(_ref.Scene);

    void Load()
    {
        if (Link is not { } l || Scene is not { } scene || _model is null) return;
        _loading = true;
        _header.Title = $"Link · {WorldMapModel.LinkLabel(l)}";
        SetText(From, l.From.Length > 0 ? $"{l.From} · {scene.Name}" : "Condition only");
        SetText(To, l.To);
        SetText(SpawnAt, l.SpawnAt.Length > 0 ? l.SpawnAt : "Start (left edge)");
        foreach (var t in _transitions) t.IsChecked = (string)t.Content! == l.Transition.ToString();
        Duration.Value = Math.Clamp(l.Duration / MaxDuration * 100, 0, 100);
        _durationText.Text = Seconds(l.Duration);
        OnlyIf.Text = WorldMapModel.ShowCondition(l.OnlyIf);
        ShowBuilder(l.OnlyIf);
        _loading = false;
        From.Click -= OnFrom;
        From.Click += OnFrom;
        To.Click -= OnTo;
        To.Click += OnTo;
        SpawnAt.Click -= OnSpawn;
        SpawnAt.Click += OnSpawn;
    }

    void OnFrom(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Scene is not { } scene || _model is null) return;
        PageChrome.ShowChoices(From, [.. _model.Triggers(scene), "Condition only"], TextOf(From), v =>
            Change("Link trigger", l => l.From = v == "Condition only" ? "" : v[..v.LastIndexOf(" · ", StringComparison.Ordinal)]));
    }

    void OnTo(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_model is null) return;
        PageChrome.ShowChoices(To, _model.Scenes.Select(s => s.Name), TextOf(To), v => Change("Link target", l => { l.To = v; _model.ApplySpawn(l, l.SpawnAt); }));
    }

    void OnSpawn(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Link is not { } link || _model is null) return;
        PageChrome.ShowChoices(SpawnAt, _model.SpawnPoints(link.To), TextOf(SpawnAt), v => Change("Spawn point", l => _model.ApplySpawn(l, v)));
    }

    // The builder shows a condition it can read; anything else stays as typed text, with the builder on "Custom".
    void ShowBuilder(string stored)
    {
        var parsed = LinkCondition.Parse(stored);
        SetText(ConditionField, parsed?.Field ?? (stored.Trim().Length == 0 ? NoCondition : CustomCondition));
        SetText(ConditionOp, parsed?.Op ?? "");
        ConditionValue.Text = parsed?.Value ?? "";
        ConditionOp.IsEnabled = ConditionValue.IsEnabled = parsed != null;
    }

    void PickField(string field)
    {
        if (field == NoCondition) { if (Link is { OnlyIf.Length: > 0 }) Change("Only if", x => x.OnlyIf = ""); return; }
        var current = LinkCondition.Parse(OnlyIf.Text ?? "");
        SetCondition(new LinkCondition(field, current?.Op ?? ">=", current?.Value ?? "1"));
    }

    void SetCondition(LinkCondition c)
    {
        if (Link is { } l && l.OnlyIf != c.Text) Change("Only if", x => x.OnlyIf = c.Text);
    }

    void CommitValue()
    {
        if (_loading || LinkCondition.Parse(OnlyIf.Text ?? "") is not { } c) return;
        var value = (ConditionValue.Text ?? "").Trim();
        if (LinkCondition.IsValue(value)) SetCondition(c with { Value = value });
        else ConditionValue.Text = c.Value;
    }

    void CommitCondition()
    {
        var stored = WorldMapModel.StoreCondition(OnlyIf.Text ?? "");
        if (Link is { } l && l.OnlyIf != stored) Change("Only if", x => x.OnlyIf = stored);
        else ShowBuilder(stored);
    }

    /// <summary>Applies one change to the link and saves its scene (one undo step).</summary>
    public void Change(string name, Action<LinkData> change)
    {
        if (_model is null || _ref is null) return;
        _model.EditLink(_ref, name, change);
        Load();
    }

    /// <summary>Selects a transition (tests, keyboard).</summary>
    public void SetTransition(Transition t) => _transitions[(int)t].IsChecked = true;
}
