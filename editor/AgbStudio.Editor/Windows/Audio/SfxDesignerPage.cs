using System.Globalization;
using System.Text.Json;
using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Build;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>
/// SFX Designer (T-P25; Audio › Sound effects +). Edits an audio/sfx/*.sfx effect for the DMG channels: output
/// Hardware (DMG) or Bake to .wav, channel, duty, start note, length, envelope and sweep, the presets, the pitch and
/// volume graph, the exact SOUNDxCNT register values and the C# line that plays it (P11.45). The preview is synthesised
/// with the hardware rules (<see cref="DmgSound"/>).
/// </summary>
[EditorWindow("T-P25")]
public sealed class SfxDesignerPage : DockPanel
{
    /// <summary>Args value that creates a new effect.</summary>
    public const string NewEffect = "new";

    readonly EditorServices _s;
    readonly StackPanel _list = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "SfxList" };
    readonly WrapPanel _presets = new() { ItemSpacing = 6, LineSpacing = 6, Margin = new Thickness(12, 12, 12, 12), Name = "Presets" };
    readonly Segment[] _channels, _duties, _outputs;
    readonly WaveformBox _wave = new() { Name = "WaveformBox" };
    readonly PitchVolumeGraph _graph = new() { Name = "PitchGraph" };
    readonly TextBlock _waveTitle = UI.Text("", "Overline/11", "text/secondary");
    readonly TextBlock _pitchLegend = UI.Text("", "Label/11", "text/secondary"), _volumeLegend = UI.Text("", "Label/11", "text/secondary");
    readonly AgbSlider _note = new() { Minimum = 0, Maximum = 48, Name = "NoteSlider" }, _length = new() { Minimum = 0, Maximum = 0.7, Name = "LengthSlider" };
    readonly AgbSlider _volume = new() { Minimum = 0, Maximum = 15, Name = "VolumeSlider" }, _fade = new() { Minimum = 0, Maximum = 7, Name = "FadeSlider" };
    readonly AgbSlider _sweepTime = new() { Minimum = 0, Maximum = 7, Name = "SweepTimeSlider" }, _sweep = new() { Minimum = 0, Maximum = 7, Name = "SweepSlider" };
    readonly TextBlock _noteText = Value(), _lengthText = Value(), _volumeText = Value(), _fadeText = Value(), _sweepTimeText = Value(), _sweepText = Value();
    readonly Button _fadeLabel, _sweepLabel;
    readonly TextBlock _fadeLabelText = UI.Text("Fade down", "Body/12", "text/secondary"), _sweepLabelText = UI.Text("Sweep up", "Body/12", "text/secondary");
    readonly Border _dutyGroup;
    readonly Control _sweepRows;
    readonly TextBlock _outputNote = UI.Text("", "Label/11", "text/muted");
    readonly SelectableTextBlock _snippet = new() { Name = "Snippet" };
    readonly StackPanel _registers = new() { Spacing = 0, Margin = new Thickness(0, -3, 0, 0), Name = "Registers" };
    readonly AgbButton _loop = new() { Kind = ButtonKind.Secondary, Size = ButtonSize.LG, Glyph = "refresh", Content = "Loop", Name = "LoopButton" };
    readonly IconButton[] _tools;
    readonly Chip _channelChip = new() { Glyph = "sound", Name = "ChannelChip" };
    readonly StatusItem _statusFile = PageFrame.Status("Status_Sfx", "sound", null), _statusSaved = PageFrame.Status("Status_Saved", "check", "Saved");
    readonly DocTab _tab;
    bool _updating, _looping;
    string? _preset;
    (string Name, DateTime At, int Count, string Before)? _coalesce;
    EditorDocument? _doc;

    public IReadOnlyList<string> Files { get; private set; } = [];
    public string? FilePath { get; private set; }
    public SfxFile Sfx { get; private set; } = new();
    public string? ActivePreset => _preset;
    public bool IsLooping => _looping;
    public bool PencilTool => _tools[1].IsActive;
    public EditorDocument? Document => _doc;

    public SfxDesignerPage(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public SfxDesignerPage(EditorServices s, string? open = null)
    {
        _s = s;
        Name = "SfxDesignerPage";
        _tools = [new IconButton { Glyph = "cursor", Name = "Tool_cursor", IsActive = true }, new IconButton { Glyph = "pencil", Name = "Tool_pencil" }];
        _tools[0].Click += (_, _) => SetPencil(false);
        _tools[1].Click += (_, _) => SetPencil(true);
        var toolbar = PageFrame.Toolbar(s, UI.Row(8, PageFrame.ToolGroup(_tools), _channelChip));
        var status = PageFrame.StatusBar([PageFrame.Status("Status_Ready", "check", "Ready", dot: true), _statusFile], [_statusSaved]);

        // left: effects and presets
        var header = new PanelHeader { Title = "Sound effects", Glyph = "sound", ActionGlyph = "plus", Name = "SfxHeader" };
        header.ActionCommand = new Relay(() => CreateEffect());
        foreach (var name in SfxPresets.Names)
        {
            var preset = name;
            var chip = new Chip { Content = name, Name = "Preset_" + name };
            chip.Click += (_, _) => ApplyPreset(preset);
            _presets.Children.Add(chip);
        }
        var left = UI.Column(0, header, _list, new PanelHeader { Title = "Presets", Glyph = "spark", Name = "PresetsHeader" }, _presets);

        // centre
        _channels = [Seg("Square 1 (sweep)", "Channel"), Seg("Square 2", "Channel"), Seg("Wave", "Channel"), Seg("Noise", "Channel")];
        for (var i = 0; i < 4; i++) { var k = (DmgChannelKind)i; _channels[i].IsCheckedChanged += (_, _) => { if (_channels[(int)k].IsChecked == true) Change("Channel", f => f.Channel = k); }; }
        _duties = [Seg("12.5%", "Duty"), Seg("25%", "Duty"), Seg("50%", "Duty"), Seg("75%", "Duty")];
        for (var i = 0; i < 4; i++) { var d = DmgSound.Duties[i]; var k = i; _duties[i].IsCheckedChanged += (_, _) => { if (_duties[k].IsChecked == true) Change("Duty", f => f.Duty = d); }; }
        _outputs = [Seg("Hardware (DMG)", "Output"), Seg("Bake to .wav", "Output")];
        _outputs[0].IsCheckedChanged += (_, _) => { if (_outputs[0].IsChecked == true) Change("Output", f => f.Bake = false); };
        _outputs[1].IsCheckedChanged += (_, _) => { if (_outputs[1].IsChecked == true) Change("Output", f => f.Bake = true); };
        var play = new PlayControl { Kind = PlayKind.Play, Name = "SfxPlay" };
        play.Click += (_, _) => Preview();
        _loop.Click += (_, _) => ToggleLoop();
        var top = new DockPanel { Height = 30 };
        var right = UI.Row(12, play, _loop);
        DockPanel.SetDock(right, Dock.Right);
        var channelLabel = UI.Text("Channel", "Body/12", "text/secondary");
        channelLabel.Margin = new Thickness(0, 0, 12, 0);
        top.Children.AddRange([right, UI.Row(0, channelLabel, Group(_channels))]);

        _wave.WaveDrawn += (i, v) => Change("Draw wave", f => { if (f.Wave.Length != 32) f.Wave = new int[32]; if (f.Wave.All(x => x == 0)) f.Wave = DmgSound.WaveSamples(f); f.Wave[i] = v; });
        var legend = UI.Row(16, Legend("status/success", _pitchLegend), Legend("status/warning", _volumeLegend));
        legend.Margin = new Thickness(0, 6, 0, 0);
        var graphs = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("420,16,*"), RowDefinitions = RowDefinitions.Parse("Auto,Auto,Auto"), Margin = new Thickness(0, 14, 0, 0) };
        var pitchTitle = UI.Text("Pitch and volume over time", "Overline/11", "text/secondary");
        foreach (var (c, row, col) in new (Control, int, int)[] { (_waveTitle, 0, 0), (pitchTitle, 0, 2), (_wave, 1, 0), (_graph, 1, 2), (legend, 2, 2) })
        {
            Grid.SetRow(c, row);
            Grid.SetColumn(c, col);
            graphs.Children.Add(c);
        }
        _waveTitle.Margin = pitchTitle.Margin = new Thickness(0, 0, 0, 6);

        _dutyGroup = Group(_duties);
        _dutyGroup.HorizontalAlignment = HorizontalAlignment.Left;
        _fadeLabel = LabelButton(_fadeLabelText, () => Change("Envelope direction", f => f.EnvelopeUp = !f.EnvelopeUp));
        _sweepLabel = LabelButton(_sweepLabelText, () => Change("Sweep direction", f => f.SweepUp = !f.SweepUp));
        var tone = UI.Column(0, Overline("Tone"),
            Row(UI.Text("Duty", "Body/12", "text/secondary"), _dutyGroup, null, 38),
            Row(UI.Text("Start note", "Body/12", "text/secondary"), _note, _noteText, 32),
            Row(UI.Text("Length", "Body/12", "text/secondary"), _length, _lengthText, 32));
        var sweepTimeRow = Row(UI.Text("Sweep time", "Body/12", "text/secondary"), _sweepTime, _sweepTimeText, 32);
        var sweepRow = Row(_sweepLabel, _sweep, _sweepText, 32);
        _sweepRows = UI.Column(0, sweepTimeRow, sweepRow);
        var envelope = UI.Column(0, Overline("Envelope and sweep"),
            Row(UI.Text("Volume", "Body/12", "text/secondary"), _volume, _volumeText, 32),
            Row(_fadeLabel, _fade, _fadeText, 32), _sweepRows);
        var fields = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("400,24,*"), Margin = new Thickness(0, 13, 0, 0) };
        Grid.SetColumn(envelope, 2);
        fields.Children.AddRange([tone, envelope]);
        _note.PropertyChanged += (_, e) => { if (e.Property == AgbSlider.ValueProperty) Change("Start note", f => f.StartNote = DmgSound.NoteName(48 + (int)Math.Round(_note.Value))); };
        _length.PropertyChanged += (_, e) => { if (e.Property == AgbSlider.ValueProperty) Change("Length", f => f.Length = Math.Max(0.02, Math.Round(_length.Value, 2))); };
        _volume.PropertyChanged += (_, e) => { if (e.Property == AgbSlider.ValueProperty) Change("Volume", f => f.Volume = (int)Math.Round(_volume.Value)); };
        _fade.PropertyChanged += (_, e) => { if (e.Property == AgbSlider.ValueProperty) Change("Fade", f => f.EnvelopeStep = (int)Math.Round(_fade.Value)); };
        _sweepTime.PropertyChanged += (_, e) => { if (e.Property == AgbSlider.ValueProperty) Change("Sweep time", f => f.SweepTime = (int)Math.Round(_sweepTime.Value)); };
        _sweep.PropertyChanged += (_, e) => { if (e.Property == AgbSlider.ValueProperty) Change("Sweep", f => f.SweepShift = SweepShift((int)Math.Round(_sweep.Value))); };

        var centerStack = UI.Column(0, top, graphs, fields);
        centerStack.Margin = new Thickness(20, 14, 20, 14);
        _tab = PageFrame.Tab("", "sound", true);
        var tabs = PageFrame.DocTabs(PageFrame.Tab("Audio", "music", false, () => _ = s.Windows.Open("T-P15", source: "SfxAudioTab")), _tab);
        var center = new DockPanel();
        DockPanel.SetDock(tabs, Dock.Top);
        center.Children.AddRange([tabs, new ScrollViewer { Content = centerStack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }]);

        // right: output
        var outHeader = new PanelHeader { Title = "Output", Glyph = "upload", Name = "OutputHeader" };
        _outputNote.TextWrapping = TextWrapping.Wrap;
        _outputNote.Margin = new Thickness(0, 4, 0, 0);
        var outputGroup = Group(_outputs);
        outputGroup.HorizontalAlignment = HorizontalAlignment.Left;
        var how = PageFrame.Section(new SectionHeader { Title = "How it plays", Glyph = "sound" }, UI.Column(6, outputGroup, _outputNote));
        ApplyStyle(_snippet, "Mono/11");
        var snippetBox = UI.Frame(_snippet, "bg/base", pad: new Thickness(10, 7), radius: 6);
        var scriptHeader = new SectionHeader { Title = "Use in a script", Glyph = "code" };
        scriptHeader.TemplateApplied += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            foreach (var i in scriptHeader.GetVisualDescendantsOfType<Icon>().Where(i => i.Glyph == "code")) i.Token(Icon.ForegroundProperty, "kind/script");
        });
        var script = PageFrame.Section(scriptHeader, snippetBox);
        var regs = PageFrame.Section(new SectionHeader { Title = "Registers", Glyph = "chip" }, _registers);
        var rightPanel = new DockPanel();
        DockPanel.SetDock(outHeader, Dock.Top);
        rightPanel.Children.AddRange([outHeader, new ScrollViewer { Content = UI.Column(0, how, script, regs) }]);

        Children.Add(PageFrame.Page(toolbar, status, PageFrame.Body(left, center, rightPanel)));
        Load(open);
        AttachedToVisualTree += (_, _) => { if (_doc != null && !_s.Documents.Open.Contains(_doc)) _s.Documents.Add(_doc); if (_doc != null) _s.Documents.Active = _doc; };
        DetachedFromVisualTree += (_, _) => { StopPreview(); if (_doc is { IsDirty: false }) _s.Documents.Close(_doc); };
    }

    static TextBlock Value()
    {
        var t = UI.Text("", "Mono/11", "text/primary");
        t.HorizontalAlignment = HorizontalAlignment.Right;
        return t;
    }

    static void ApplyStyle(SelectableTextBlock t, string style) => AgbStudio.Editor.Theme.Fig.SetStyle(t, style);

    static Segment Seg(string text, string group) => new() { Content = text, GroupName = group + Guid.NewGuid().ToString("N"), Name = "Seg_" + text };

    static Border Group(Segment[] segs)
    {
        var name = segs[0].GroupName;
        foreach (var s in segs) s.GroupName = name;
        return UI.Frame(UI.Row(2, segs), "bg/base", "border/default", new Thickness(1), new Thickness(3), 7);
    }

    static TextBlock Overline(string text)
    {
        var t = UI.Text(text, "Overline/11", "text/secondary");
        t.Margin = new Thickness(0, 0, 0, 4);
        return t;
    }

    static StackPanel Legend(string token, TextBlock text)
    {
        var line = new Border { Width = 10, Height = 3, CornerRadius = new CornerRadius(1.5), VerticalAlignment = VerticalAlignment.Center }.Token(Border.BackgroundProperty, token);
        return UI.Row(6, line, text);
    }

    static Button LabelButton(TextBlock text, Action click)
    {
        var b = new Button { Content = text, Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        b.Click += (_, _) => click();
        return b;
    }

    static Grid Row(Control label, Control editor, Control? value, double height)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("94,*,54"), Height = height };
        label.VerticalAlignment = VerticalAlignment.Center;
        editor.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(editor, 1);
        g.Children.AddRange([label, editor]);
        if (value != null)
        {
            Grid.SetColumn(value, 2);
            g.Children.Add(value);
        }
        else Grid.SetColumnSpan(editor, 2);
        return g;
    }

    /// <summary>"Sweep up 2" is the sweep amount: 0 = off, 1..7 = shift 7..1 (larger = faster pitch change).</summary>
    public static int SweepAmount(int shift) => shift == 0 ? 0 : 8 - shift;
    public static int SweepShift(int amount) => amount == 0 ? 0 : 8 - amount;

    // ---- files ----------------------------------------------------------------------------------------

    void Load(string? open)
    {
        var p = _s.Project.Current;
        if (open == NewEffect) { CreateEffect(); return; }
        Files = p?.SfxFiles().ToList() ?? [];
        var pick = open != null ? Files.FirstOrDefault(f => string.Equals(f, open, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f) == open || GameProject.AssetName(f) == open) : null;
        BuildList();
        Select(pick ?? Files.FirstOrDefault());
    }

    void BuildList()
    {
        _list.Children.Clear();
        foreach (var f in Files)
        {
            var file = f;
            SfxFile sfx;
            try { sfx = GameProject.Load<SfxFile>(f); }
            catch (Exception e) when (e is IOException or JsonException) { continue; }
            var row = new TreeItem { Label = Path.GetFileName(f), Glyph = "sound", Meta = ChannelName(sfx.Channel), Name = "Sfx_" + Path.GetFileName(f), IsSelected = f == FilePath };
            row.Activated += (_, _) => Select(file);
            _list.Children.Add(row);
        }
    }

    public static string ChannelName(DmgChannelKind k) => k switch
    {
        DmgChannelKind.Square1 => "Square 1",
        DmgChannelKind.Square2 => "Square 2",
        DmgChannelKind.Wave => "Wave",
        _ => "Noise",
    };

    /// <summary>Opens an effect file (saving the current one when it has changes).</summary>
    public void Select(string? file)
    {
        if (_doc is { IsDirty: true } && file != FilePath) _ = _s.Documents.SaveAsync(_doc);
        StopPreview();
        if (_doc != null && file != FilePath && !_doc.IsDirty) _s.Documents.Close(_doc);
        FilePath = file;
        try { Sfx = file is null ? new SfxFile() : GameProject.Load<SfxFile>(file); }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Sfx = new SfxFile();
            _s.Toasts.Show($"{Path.GetFileName(file)} could not be read: {e.Message.TrimEnd('.')}. An empty sound is shown instead; fix the file before you edit here, or the empty sound is saved over it.", ToastStatus.Error);
        }
        if (Sfx.Name.Length == 0 && file != null) Sfx.Name = GameProject.AssetName(file);
        _preset = SfxPresets.Matching(Sfx) ?? SfxPresets.Names.FirstOrDefault(n => string.Equals(n, Sfx.Name, StringComparison.OrdinalIgnoreCase));
        _coalesce = null;
        if (file != null && _doc?.Path != file)
        {
            _doc = new EditorDocument { Name = Path.GetFileName(file), Glyph = "sound", Path = file, SaveHandler = _ => { Save(); return Task.CompletedTask; } };
            _s.Documents.Add(_doc, activate: true);
        }
        else if (file is null) _doc = null;
        if (_doc != null) _doc.PropertyChanged += (_, _) => UpdateSaved();
        foreach (var r in _list.Children.OfType<TreeItem>()) r.IsSelected = file != null && r.Label == Path.GetFileName(file);
        _tab.Label = file is null ? "" : Path.GetFileName(file);
        _tab.IsVisible = file != null;
        Refresh();
    }

    /// <summary>"+": a new effect file in audio/sfx (sfx_1.sfx, sfx_2.sfx ...).</summary>
    public string? CreateEffect()
    {
        var p = _s.Project.Current;
        if (p is null) return null;
        Directory.CreateDirectory(p.SfxDir);
        var n = 1;
        while (File.Exists(Path.Combine(p.SfxDir, $"sfx_{n}.sfx"))) n++;
        var path = Path.Combine(p.SfxDir, $"sfx_{n}.sfx");
        var sfx = new SfxFile { Name = $"sfx_{n}" };
        SfxPresets.Apply("Select", sfx);
        GameProject.Save(path, sfx);
        Files = p.SfxFiles().ToList();
        BuildList();
        Select(path);
        return path;
    }

    /// <summary>Writes the .sfx; with "Bake to .wav" also assets/audio/sfx/&lt;name&gt;.wav (11 kHz mono 8-bit) for Maxmod.</summary>
    public void Save()
    {
        if (FilePath is null) return;
        GameProject.Save(FilePath, Sfx);
        if (Sfx.Bake && _s.Project.Current is { } p)
        {
            var wav = Path.Combine(p.AssetsDir, "audio", "sfx", (Sfx.Name.Length > 0 ? Sfx.Name : GameProject.AssetName(FilePath)) + ".wav");
            Directory.CreateDirectory(Path.GetDirectoryName(wav)!);
            File.WriteAllBytes(wav, WavWriter.Encode(DmgSound.Bake(Sfx), 11025));
        }
        if (_doc != null) _doc.IsDirty = false;
        BuildList();
        UpdateSaved();
    }

    // ---- edits ----------------------------------------------------------------------------------------

    /// <summary>Applies an edit as one undo step (slider drags of the same field merge into one step).</summary>
    public void Change(string name, Action<SfxFile> edit)
    {
        if (_updating) return;
        var before = JsonSerializer.Serialize(Sfx, GameProject.Json);
        var copy = JsonSerializer.Deserialize<SfxFile>(before, GameProject.Json)!;
        edit(copy);
        var after = JsonSerializer.Serialize(copy, GameProject.Json);
        if (after == before) { Refresh(); return; }
        var now = DateTime.UtcNow;
        if (_coalesce is { } c && c.Name == name && (now - c.At).TotalSeconds < 1.5 && _s.Undo.UndoCount == c.Count && _s.Undo.NextUndo == name)
        {
            // continue the last step: undo returns to the state before the drag started
            _s.Undo.Undo();
            before = c.Before;
            _s.Undo.Do(new DelegateAction(name, () => Restore(c.Before), () => Restore(after)));
        }
        else _s.Undo.Do(new DelegateAction(name, () => Restore(before), () => Restore(after)));
        _coalesce = (name, now, _s.Undo.UndoCount, before);
        if (name != "Draw wave") _preset = SfxPresets.Matching(Sfx);
        Refresh();
    }

    void Restore(string json)
    {
        Sfx = JsonSerializer.Deserialize<SfxFile>(json, GameProject.Json)!;
        if (_doc != null) _doc.IsDirty = true;
        Refresh();
    }

    public void ApplyPreset(string preset)
    {
        Change("Preset " + preset, f => SfxPresets.Apply(preset, f));
        _preset = preset;
        _coalesce = null;
        Refresh();
    }

    void SetPencil(bool on)
    {
        _tools[0].IsActive = !on;
        _tools[1].IsActive = on;
        _wave.PencilEnabled = on;
    }

    // ---- view -----------------------------------------------------------------------------------------

    void Refresh()
    {
        _updating = true;
        try
        {
            var f = Sfx;
            var square = f.Channel is DmgChannelKind.Square1 or DmgChannelKind.Square2;
            for (var i = 0; i < 4; i++) _channels[i].IsChecked = (int)f.Channel == i;
            var duty = Array.IndexOf(DmgSound.Duties, DmgSound.Duties.MinBy(d => Math.Abs(d - f.Duty)));
            for (var i = 0; i < 4; i++) _duties[i].IsChecked = square && i == duty;
            _dutyGroup.IsEnabled = square;
            _dutyGroup.Opacity = square ? 1 : 0.4;
            _outputs[0].IsChecked = !f.Bake;
            _outputs[1].IsChecked = f.Bake;
            var midi = DmgSound.Midi(f.StartNote);
            _note.Value = Math.Clamp(midi - 48, 0, 48);
            _noteText.Text = f.StartNote;
            _length.Value = f.Length;
            _lengthText.Text = f.Length.ToString("0.00", CultureInfo.InvariantCulture) + " s";
            _volume.Value = f.Volume;
            _volumeText.Text = f.Volume.ToString(CultureInfo.InvariantCulture);
            _fade.Value = f.EnvelopeStep;
            _fadeText.Text = f.EnvelopeStep.ToString(CultureInfo.InvariantCulture);
            _fadeLabelText.Text = f.EnvelopeUp ? "Fade up" : "Fade down";
            var hasEnvelope = f.Channel != DmgChannelKind.Wave;
            _fade.IsEnabled = _fadeLabel.IsEnabled = hasEnvelope;
            _fade.Opacity = hasEnvelope ? 1 : 0.4;
            _sweepTime.Value = f.SweepTime;
            _sweepTimeText.Text = f.SweepTime.ToString(CultureInfo.InvariantCulture);
            _sweep.Value = SweepAmount(f.SweepShift);
            _sweepText.Text = SweepAmount(f.SweepShift).ToString(CultureInfo.InvariantCulture);
            _sweepLabelText.Text = f.SweepUp ? "Sweep up" : "Sweep down";
            _sweepRows.IsEnabled = f.Channel == DmgChannelKind.Square1;
            _sweepRows.Opacity = f.Channel == DmgChannelKind.Square1 ? 1 : 0.4;
            _waveTitle.Text = f.Channel switch
            {
                DmgChannelKind.Wave => "Waveform · wave RAM",
                DmgChannelKind.Noise => "Waveform · noise",
                _ => $"Waveform · duty {DutyText(f.Duty)}",
            };
            _wave.Sfx = f;
            var trace = DmgSound.Simulate(f);
            _graph.Trace = trace;
            var sweeping = f.Channel == DmgChannelKind.Square1 && f.SweepTime > 0 && f.SweepShift > 0;
            _pitchLegend.Text = sweeping ? $"pitch (sweep {(f.SweepUp ? "up" : "down")})" : "pitch";
            _volumeLegend.Text = hasEnvelope && f.EnvelopeStep > 0 ? $"volume (envelope {(f.EnvelopeUp ? "up" : "down")})" : "volume";
            _channelChip.Content = "GBA DMG sound · " + ChannelName(f.Channel);
            var bakeKb = Math.Max(1, (int)Math.Round(Math.Ceiling(f.Length * 11025) / 1024.0));
            _outputNote.Text = $"Hardware: 0 bytes of ROM, uses one DMG channel while playing. Bake: {bakeKb} KB sample, mixes with music in Maxmod.";
            var name = f.Name.Length > 0 ? f.Name : FilePath is null ? "sfx" : GameProject.AssetName(FilePath);
            _snippet.Inlines = Snippet(name);
            _registers.Children.Clear();
            foreach (var r in DmgSound.Shown(f))
            {
                var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("128,*"), Height = 28, Name = "Reg_" + r.Name };
                var v = UI.Text(r.Hex, "Mono/11 Bold", "text/primary");
                Grid.SetColumn(v, 1);
                row.Children.AddRange([UI.Text(r.Name, "Mono/11", "text/secondary"), v]);
                _registers.Children.Add(row);
            }
            foreach (var chip in _presets.Children.OfType<Chip>()) chip.Style = (string?)chip.Content == _preset ? ChipStyle.Accent : ChipStyle.Default;
            var rom = DmgSound.RomBytes(f);
            _statusFile.Content = FilePath is null ? null : $"{Path.GetFileName(FilePath)} · {f.Length.ToString("0.00", CultureInfo.InvariantCulture)} s · {(rom == 0 ? "0 bytes" : PageFrame.Kb(rom))} of ROM";
            _statusFile.IsVisible = FilePath != null;
            UpdateSaved();
        }
        finally { _updating = false; }
    }

    Avalonia.Controls.Documents.InlineCollection Snippet(string name)
    {
        Avalonia.Controls.Documents.Run R(string text, string token) => new Avalonia.Controls.Documents.Run(text).Token(token);
        return [R("Sound", "accent/hover"), R(".", "text/primary"), R("Play", "status/success"), R("(", "text/primary"), R($"\"{name}\"", "kind/data"), R(");", "text/primary")];
    }

    void UpdateSaved() => _statusSaved.IsVisible = _doc is null || !_doc.IsDirty;

    static string DutyText(double d) => DmgSound.Duties.MinBy(x => Math.Abs(x - d)) switch { 0.125 => "12.5%", 0.25 => "25%", 0.5 => "50%", _ => "75%" };

    // ---- preview --------------------------------------------------------------------------------------

    /// <summary>The PCM the preview plays (44.1 kHz mono).</summary>
    public float[] PreviewSamples() => DmgSound.Render(Sfx, AudioPreview.Rate);

    public void Preview()
    {
        var p = AudioPreview.Of(_s);
        p.StopSounds();
        p.PlaySound(PreviewSamples(), _looping);
    }

    void ToggleLoop()
    {
        _looping = !_looping;
        _loop.Kind = _looping ? ButtonKind.Primary : ButtonKind.Secondary;
        if (!_looping) StopPreview();
        else Preview();
    }

    void StopPreview()
    {
        if (_s.GetService(typeof(AudioPreview)) is AudioPreview p) p.StopSounds();
    }
}

static class RunExtensions
{
    public static Avalonia.Controls.Documents.Run Token(this Avalonia.Controls.Documents.Run r, string token)
    {
        r.Bind(Avalonia.Controls.Documents.TextElement.ForegroundProperty, Application.Current!.GetResourceObservable(token));
        return r;
    }
}
