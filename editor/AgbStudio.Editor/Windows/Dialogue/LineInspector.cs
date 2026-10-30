using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Scripting.Dialogue;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgbStudio.Editor.Windows.Dialogue;

/// <summary>
/// The LINE panel of the Dialogue Editor (T-P16): the GBA preview of the selected line (drawn like the ROM, with the
/// choices under the text), then "Line N" (speaker, portrait, text speed in characters per frame, blip sound) and
/// Conditions ("Show if", "Then set"). For a command it shows the command's fields, for a choice its target and
/// conditions.
/// </summary>
public sealed class LineInspector : DockPanel
{
    public const int MaxSpeed = 4;
    const string Default = "—";

    readonly DialogueEditorPage _page;
    readonly StackPanel _body = new() { Name = "LineBody" };
    readonly GbaScreen _screen = new() { Backdrop = "art/scene/day", Width = 296, Height = 197, Name = "GbaPreview" };
    readonly Dictionary<string, InspectorField> _fields = new(StringComparer.Ordinal);
    string? _shape;
    bool _updating;
    Border? _preview;

    public LineInspector(DialogueEditorPage page)
    {
        _page = page;
        Name = "LineInspector";
        var header = new PanelHeader { Title = "Line", Glyph = "sliders", Name = "LineHeader" };
        SetDock(header, Dock.Top);
        Children.Add(header);
        Children.Add(new ScrollViewer { Content = _body });
    }

    public GbaScreen Screen => _screen;
    public IReadOnlyDictionary<string, InspectorField> Fields => _fields;
    public string? SectionTitle { get; private set; }

    readonly Reentry _refresh = new();
    int _line = -1, _choice = -1;

    public void Refresh() => _refresh.Run(RefreshCore);

    void RefreshCore()
    {
        var c = _page.Conversation;
        var l = _page.Line;
        if (c is null || l is null || _page.Current is null)
        {
            _body.Children.Clear();
            _fields.Clear();
            _shape = null;
            return;
        }
        var shape = $"{_page.Current.Path}|{_page.ConversationIndex}|{_page.SelectedLine}|{_page.SelectedChoice}|{l.Command}";
        if (shape != _shape) Build(c, l);
        _shape = shape;
        Update(c, l);
    }

    void Build(Conversation c, DialogueLine l)
    {
        // a focused box commits to the line it was built for while it is removed
        _body.Children.Clear();
        _fields.Clear();
        _line = _page.SelectedLine;
        _choice = _page.SelectedChoice;
        _preview ??= new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = _screen, Width = 296, Height = 197, Margin = new Thickness(12, 12, 12, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        _body.Children.Add(_preview);
        var index = _line;
        var choice = _choice;
        var list = UI.Column(6);
        if (choice >= 0)
        {
            SectionTitle = $"Choice {choice + 1}";
            list.Children.Add(Dropdown_("Target", () => _page.Targets(c), v => _page.SetTarget(index, choice, v)));
            _body.Children.Add(Section(SectionTitle, "nodes", list, new Thickness(12, 20, 12, 10)));
            _body.Children.Add(Ui.Rule());
            _body.Children.Add(Section("Conditions", "link", UI.Column(6,
                Text_("Show if", v => Edit(x => Choice(x).ShowIf = v.Length == 0 ? null : v)),
                Text_("Then set", v => Edit(x => Choice(x).ThenSet = v.Length == 0 ? null : DialogueEditorPage.Statements(v)))), new Thickness(12, 8, 12, 9)));
            _body.Children.Add(Ui.Rule());
            return;
        }
        if (l.Command != null)
        {
            var (title, glyph) = LineCards.CommandInfo.GetValueOrDefault(l.Command, (l.Command, "code"));
            SectionTitle = title;
            var data = _page.Data!;
            switch (l.Command)
            {
                case "goToScene":
                    list.Children.Add(Dropdown_("Scene", () => data.Project.SceneFiles().Select(GameProject.AssetName), v => Edit(x => x.Scene = v)));
                    break;
                case "setFlag":
                    list.Children.Add(Text_("Flag", v => Edit(x => x.Flag = v)));
                    list.Children.Add(Toggle_("Value", v => Edit(x => x.Value = v)));
                    break;
                case "giveItem":
                    list.Children.Add(Dropdown_("Table", () => data.Tables.Select(t => t.Name), v => Edit(x => { x.Table = v; x.Item = data.Table(v)?.Table.Rows.FirstOrDefault()?.GetValueOrDefault(data.Table(v)!.Table.KeyField); })));
                    list.Children.Add(Dropdown_("Item", () => data.Table(_page.Line?.Table ?? "") is { } t ? t.Table.Rows.Select(r => r.GetValueOrDefault(t.Table.KeyField) ?? "") : [], v => Edit(x => x.Item = v)));
                    list.Children.Add(Text_("Count", v => { if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) Edit(x => x.Count = n); }));
                    break;
                case "playSound":
                    list.Children.Add(Dropdown_("Sound", () => _page.Sounds(), v => Edit(x => x.Sound = DialogueEditorPage.SoundName(v))));
                    break;
                case "wait":
                    list.Children.Add(Text_("Frames", v => { if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) Edit(x => { x.Frames = n; x.Seconds = null; }); }));
                    break;
                case "shakeCamera":
                    list.Children.Add(Text_("Amplitude", v => { if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) Edit(x => x.Amplitude = n); }));
                    list.Children.Add(Text_("Frames", v => { if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) Edit(x => x.Frames = n); }));
                    break;
            }
            _body.Children.Add(Section(title, glyph, list, new Thickness(12, 20, 12, 10)));
            _body.Children.Add(Ui.Rule());
            _body.Children.Add(Section("Conditions", "link", UI.Column(6, Text_("Show if", v => Edit(x => x.ShowIf = v.Length == 0 ? null : v))), new Thickness(12, 8, 12, 9)));
            _body.Children.Add(Ui.Rule());
            return;
        }
        SectionTitle = $"Line {index + 1}";
        list.Children.Add(Dropdown_("Speaker", () => _page.Current!.File.Speakers.Select(s => s.Name), v => Edit(x => x.Speaker = v)));
        list.Children.Add(Dropdown_("Portrait", () => Portraits(), v => Edit(x => x.Portrait = v == Default ? null : v)));
        var speed = new InspectorField { Kind = FieldKind.Slider, Label = "Text speed", Name = "Field_TextSpeed" };
        _fields["Text speed"] = speed;
        int? dragFrom = null;
        speed.PropertyChanged += (_, e) =>
        {
            if (e.Property != InspectorField.SliderValueProperty || _updating) return;
            dragFrom ??= Speed(_page.Line!);
            speed.Value = SpeedText(SliderSpeed(speed.SliderValue));
        };
        void CommitSpeed()
        {
            if (dragFrom is null) return;
            dragFrom = null;
            var v = SliderSpeed(speed.SliderValue);
            if (v != Speed(_page.Line!)) Edit(x => x.Speed = v);
            else Update(c, _page.Line!);
        }
        speed.AddHandler(PointerReleasedEvent, (_, _) => CommitSpeed(), RoutingStrategies.Bubble, handledEventsToo: true);
        speed.AddHandler(KeyUpEvent, (_, _) => CommitSpeed(), RoutingStrategies.Bubble, handledEventsToo: true);
        list.Children.Add(speed);
        list.Children.Add(Dropdown_("Blip sound", () => _page.Sounds().Prepend(Default), v => Edit(x => x.Blip = v == Default ? null : DialogueEditorPage.SoundName(v))));
        _body.Children.Add(Section(SectionTitle, "book", list, new Thickness(12, 20, 12, 10)));
        _body.Children.Add(Ui.Rule());
        _body.Children.Add(Section("Conditions", "link", UI.Column(6,
            Text_("Show if", v => Edit(x => x.ShowIf = v.Length == 0 ? null : v)),
            Text_("Then set", v => Edit(x => x.ThenSet = v.Length == 0 ? null : DialogueEditorPage.Statements(v)))), new Thickness(12, 8, 12, 9)));
        _body.Children.Add(Ui.Rule());
    }

    DialogueChoice Choice(DialogueLine l) => l.Choices![_choice];

    IEnumerable<string> Portraits()
    {
        var names = SpriteLibrary.Names(_page.Data?.Project).ToList();
        if (_page.Line?.Portrait is { } p && !names.Contains(p)) names.Add(p);
        return names.Prepend(Default);
    }

    /// <summary>Text speed of a line: its own, else the speaker's, else the file's (2 ch/frame when none is set).</summary>
    int Speed(DialogueLine l)
    {
        var sp = _page.Current!.File.Speakers.FirstOrDefault(s => s.Name == l.Speaker);
        return l.Speed ?? (sp is { TextSpeed: >= 0 } ? sp.TextSpeed : _page.Current.File.TextSpeed >= 0 ? _page.Current.File.TextSpeed : 2);
    }

    static int SliderSpeed(double slider) => (int)Math.Round(slider / 100 * MaxSpeed);
    public static string SpeedText(int speed) => speed == 0 ? "instant" : $"{speed} ch/frame";

    void Edit(Action<DialogueLine> change)
    {
        var index = _line;
        var c = _page.Conversation;
        if (c is null || index < 0) return;
        _page.EditDialogue("Edit line", () => change(c.Lines[index]));
    }

    static StackPanel Section(string title, string glyph, Control content, Thickness margin)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph, Name = "Section_" + title.Replace(" ", "") };
        var s = UI.Column(6, header, content);
        s.Margin = margin;
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        return s;
    }

    InspectorField Dropdown_(string label, Func<IEnumerable<string>> options, Action<string> pick)
    {
        var f = new InspectorField { Kind = FieldKind.Dropdown, Label = label, Name = "Field_" + label.Replace(" ", "") };
        Dropdown.Attach(f, options, pick);
        _fields[label] = f;
        return f;
    }

    InspectorField Text_(string label, Action<string> commit)
    {
        var f = new InspectorField { Kind = FieldKind.Text, Label = label, Name = "Field_" + label.Replace(" ", "") };
        f.CommitOnLeave(() => Current(label), v => commit(v.Trim()));
        _fields[label] = f;
        return f;
    }

    InspectorField Toggle_(string label, Action<bool> commit)
    {
        var f = new InspectorField { Kind = FieldKind.Toggle, Label = label, Name = "Field_" + label.Replace(" ", "") };
        f.PropertyChanged += (_, e) => { if (e.Property == InspectorField.IsOnProperty && !_updating) commit(f.IsOn); };
        _fields[label] = f;
        return f;
    }

    /// <summary>The value a field shows for the current selection.</summary>
    public string Current(string label)
    {
        var l = _page.Conversation is { } conv && _line >= 0 && _line < conv.Lines.Count ? conv.Lines[_line] : null;
        if (l is null) return "";
        var ch = _choice >= 0 && l.Choices is { } list && _choice < list.Count ? list[_choice] : null;
        var sp = _page.Current?.File.Speakers.FirstOrDefault(s => s.Name == l.Speaker);
        return label switch
        {
            "Target" when ch != null && _page.Conversation is { } c => DialogueEditorPage.TargetText(c, ch),
            "Show if" => (ch != null ? ch.ShowIf : l.ShowIf) ?? "",
            "Then set" => DialogueEditorPage.StatementsText(ch != null ? ch.ThenSet : l.ThenSet),
            "Speaker" => l.Speaker ?? Default,
            "Portrait" => l.Portrait ?? sp?.Portrait ?? Default,
            "Text speed" => SpeedText(Speed(l)),
            "Blip sound" => _page.SoundFile(l.Blip ?? sp?.Blip ?? _page.Current?.File.Blip) ?? Default,
            "Scene" => l.Scene ?? "",
            "Flag" => l.Flag ?? "",
            "Table" => l.Table ?? "",
            "Item" => l.Item ?? "",
            "Count" => (l.Count ?? 1).ToString(CultureInfo.InvariantCulture),
            "Sound" => _page.SoundFile(l.Sound) ?? "",
            "Frames" => (l.Frames ?? 0).ToString(CultureInfo.InvariantCulture),
            "Amplitude" => (l.Amplitude ?? 2).ToString(CultureInfo.InvariantCulture),
            _ => "",
        };
    }

    void Update(Conversation c, DialogueLine l)
    {
        _updating = true;
        try
        {
            foreach (var (label, f) in _fields)
            {
                if (f.Kind == FieldKind.Slider) { f.Value = Current(label); f.SliderValue = Math.Clamp(Speed(l) * 100.0 / MaxSpeed, 0, 100); }
                else if (f.Kind == FieldKind.Toggle) f.IsOn = l.Value ?? true;
                else if (!f.GetVisualDescendantsOfType<TextBox>().Any(b => b.IsFocused)) f.Value = Current(label);
            }
            var gba = _page.Gba;
            if (gba is null || l.Command != null) { _screen.Box = null; return; }
            var key = _page.KeyOf(l, _page.SelectedLine);
            var text = _page.TextOf(key, l.Text, _page.Primary) ?? "";
            var choices = l.Choices?.Select((ch, i) => _page.TextOf(ch.Key ?? $"{key}.choice{i}", ch.Text, _page.Primary) ?? "").ToList();
            _screen.Box = gba.RenderBox(_page.Primary, text, choices);
        }
        finally { _updating = false; }
    }

    /// <summary>Focuses the first field (after Add to Dialogue › Command › …).</summary>
    public void FocusFirst() => FocusBox(_fields.Values.FirstOrDefault(f => f.Kind == FieldKind.Text));

    /// <summary>Focuses "Show if" (Add to Dialogue › Condition).</summary>
    public void FocusCondition() => FocusBox(_fields.GetValueOrDefault("Show if"));

    static void FocusBox(InspectorField? f)
    {
        if (f is null) return;
        f.ApplyTemplate();
        f.GetVisualDescendantsOfType<TextBox>().FirstOrDefault()?.Focus();
    }
}
