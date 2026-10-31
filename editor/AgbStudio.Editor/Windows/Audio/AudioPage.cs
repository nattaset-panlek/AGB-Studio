using System.Globalization;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Build;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>
/// Audio (T-P15; Tools › Audio, Create › Sound Effect, Create › Music). Lists the project's music (MOD / XM) and sound
/// effects (WAV) with their real lengths, reads track info from the module (BPM, speed, patterns, length, loop), shows
/// the tracker pattern view and plays a preview through <see cref="AudioPreview"/>. The Track inspector edits the
/// music's .meta.json (volume, loop start, fade on scene change); the tracker tools edit pattern cells (P11.43, P11.44).
/// </summary>
[EditorWindow("T-P15")]
public sealed class AudioPage : DockPanel
{
    public enum Tool { Select, Pencil, Eraser }

    readonly EditorServices _s;
    readonly StackPanel _musicList = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "MusicList" };
    readonly StackPanel _soundList = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "SoundList" };
    readonly ContentControl _center = new() { Name = "AudioCenter" };
    readonly StackPanel _inspector = new() { Name = "TrackInspector" };
    readonly IconButton[] _tools;
    readonly StatusItem _statusTrack, _statusRom, _statusCpu;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly DocTab _trackTab;
    WaveformOverview? _overview;
    TrackerGrid? _grid;
    readonly List<(string Name, AgbProgress Bar, TextBlock Value)> _mixer = [];
    EditorDocument? _doc;
    TrackerCell? _picked;
    bool _moduleEdited;

    public IReadOnlyList<string> MusicFiles { get; private set; } = [];
    public IReadOnlyList<string> SoundFiles { get; private set; } = [];
    public string? Track { get; private set; }
    public TrackerModule? Module { get; private set; }
    public ModuleAnalysis? Analysis { get; private set; }
    public MusicMeta Meta { get; private set; } = new();
    public int Order { get; private set; }
    public int Row { get; private set; }
    public Tool CurrentTool { get; private set; }
    public ModulePlayer? Player { get; private set; }
    public EditorDocument? Document => _doc;
    public IReadOnlyList<(string Name, double Percent, string Value)> Channels { get; private set; } = [];
    public IReadOnlyList<(string Name, double Db)> MixerLevels { get; private set; } = [];

    public AudioPage(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public AudioPage(EditorServices s, string? select = null)
    {
        _s = s;
        Name = "AudioPage";
        _tools = [new IconButton { Glyph = "cursor", Name = "Tool_cursor" }, new IconButton { Glyph = "pencil", Name = "Tool_pencil" }, new IconButton { Glyph = "eraser", Name = "Tool_eraser" }];
        for (var i = 0; i < _tools.Length; i++) { var k = (Tool)i; _tools[i].Click += (_, _) => SetTool(k); }
        SetTool(Tool.Select);
        var engine = new Chip { Content = EngineText(s), Glyph = "music", Name = "EngineChip" };
        var toolbar = PageFrame.Toolbar(s, UI.Row(8, PageFrame.ToolGroup(_tools), engine));

        _statusTrack = PageFrame.Status("Status_Track", "music", null);
        _statusRom = PageFrame.Status("Status_AudioRom", "chip", null);
        _statusCpu = PageFrame.Status("Status_Cpu", "clock", null);
        var status = PageFrame.StatusBar([PageFrame.Status("Status_Ready", "check", "Ready", dot: true), _statusTrack], [_statusRom, _statusCpu]);

        var musicHeader = new PanelHeader { Title = "Music", Glyph = "music", ActionGlyph = "plus", Name = "MusicHeader" };
        musicHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-D04", source: "AudioMusicAdd"));
        var soundHeader = new PanelHeader { Title = "Sound effects", Glyph = "sound", ActionGlyph = "plus", Name = "SoundHeader" };
        soundHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-P25", SfxDesignerPage.NewEffect, source: "AudioSoundAdd"));
        var left = UI.Column(0, musicHeader, _musicList, soundHeader, _soundList);

        var trackHeader = new PanelHeader { Title = "Track", Glyph = "sliders", Name = "TrackHeader" };
        var right = new DockPanel();
        DockPanel.SetDock(trackHeader, Dock.Top);
        right.Children.Add(trackHeader);
        var footer = BudgetFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        right.Children.Add(footer);
        right.Children.Add(new ScrollViewer { Content = _inspector, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });

        _trackTab = PageFrame.Tab("", "music", true);
        var tabs = PageFrame.DocTabs(PageFrame.Tab("Scene", "map", false, () => _ = s.Windows.Open("T-P01", source: "AudioSceneTab")), _trackTab);
        var center = new DockPanel();
        DockPanel.SetDock(tabs, Dock.Top);
        center.Children.AddRange([tabs, _center]);

        Children.Add(PageFrame.Page(toolbar, status, PageFrame.Body(left, center, right)));

        _timer.Tick += (_, _) => Tick();
        Reload(select);
        AttachedToVisualTree += (_, _) => { if (_doc != null && !_s.Documents.Open.Contains(_doc)) _s.Documents.Add(_doc); if (_doc != null) _s.Documents.Active = _doc; };
        DetachedFromVisualTree += (_, _) => { StopPreview(); if (_doc is { IsDirty: false }) _s.Documents.Close(_doc); };
    }

    static string EngineText(EditorServices s)
    {
        var a = s.Project.Current?.Settings.Audio ?? new AudioSettings();
        return $"Maxmod · {a.Channels} ch · {a.MixKHz} kHz";
    }

    // ---- data -----------------------------------------------------------------------------------------

    /// <summary>Re-reads the audio files of the project and selects <paramref name="select"/> (a path or file name).</summary>
    public void Reload(string? select = null)
    {
        var p = _s.Project.Current;
        MusicFiles = p?.MusicFiles().ToList() ?? [];
        SoundFiles = p?.SoundFiles().ToList() ?? [];
        var pick = select is null ? null : MusicFiles.FirstOrDefault(f => string.Equals(f, select, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f) == select);
        pick ??= Track != null && MusicFiles.Contains(Track) ? Track : SceneTrack(p) ?? MusicFiles.FirstOrDefault();
        BuildLists();
        UpdateChannels();
        SelectTrack(pick);
        UpdateBudget();
    }

    static string? SceneTrack(GameProject? p)
    {
        if (p is null || p.Settings.LastScene.Length == 0) return null;
        var file = Path.Combine(p.ScenesDir, p.Settings.LastScene + ".scene");
        try
        {
            if (!File.Exists(file)) return null;
            var track = GameProject.Load<SceneFile>(file).Music.Track;
            return track.Length == 0 ? null : p.MusicFiles().FirstOrDefault(f => GameProject.AssetName(f) == track);
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { return null; }
    }

    static readonly Dictionary<string, (DateTime Stamp, double Seconds)> LengthCache = [];

    /// <summary>Length of a music file in seconds (cached by file time).</summary>
    public static double MusicSeconds(string file)
    {
        var stamp = File.GetLastWriteTimeUtc(file);
        lock (LengthCache)
            if (LengthCache.TryGetValue(file, out var c) && c.Stamp == stamp) return c.Seconds;
        double seconds;
        try { seconds = ModuleAnalysis.Of(TrackerModule.Load(file), 1).Seconds; }
        catch (Exception e) when (e is IOException or InvalidDataException or IndexOutOfRangeException or ArgumentException) { seconds = 0; }
        lock (LengthCache) LengthCache[file] = (stamp, seconds);
        return seconds;
    }

    public static double SoundSeconds(string file)
    {
        try { return WavData.Load(file).Seconds; }
        catch (Exception e) when (e is IOException or InvalidDataException) { return 0; }
    }

    void BuildLists()
    {
        _musicList.Children.Clear();
        foreach (var f in MusicFiles)
        {
            var file = f;
            var row = new TreeItem { Label = Path.GetFileName(f), Glyph = "music", Meta = ModuleAnalysis.FormatTime(MusicSeconds(f)), Name = "Music_" + Path.GetFileName(f) };
            row.Activated += (_, _) => SelectTrack(file);
            _musicList.Children.Add(row);
        }
        _soundList.Children.Clear();
        foreach (var f in SoundFiles)
        {
            var file = f;
            var row = new TreeItem { Label = Path.GetFileName(f), Glyph = "sound", Meta = SoundSeconds(f).ToString("0.00", CultureInfo.InvariantCulture) + " s", Name = "Sound_" + Path.GetFileName(f) };
            row.Activated += (_, e) =>
            {
                if (e.ClickCount >= 2 && SfxFor(file) is { } sfx) _ = _s.Windows.Open("T-P25", sfx, source: "AudioSoundOpen");
                else PreviewSound(file);
            };
            _soundList.Children.Add(row);
        }
    }

    string? SfxFor(string wav)
    {
        var p = _s.Project.Current;
        var sfx = p is null ? null : Path.Combine(p.SfxDir, GameProject.AssetName(wav) + ".sfx");
        return sfx != null && File.Exists(sfx) ? sfx : null;
    }

    /// <summary>Selects a music file: loads the module, its analysis and meta, and opens it as the active document.</summary>
    public void SelectTrack(string? file)
    {
        if (_doc is { IsDirty: true } && file != Track) _ = _s.Documents.SaveAsync(_doc);
        StopPreview();
        if (_doc != null && file != Track && !_doc.IsDirty) _s.Documents.Close(_doc);
        Track = file;
        Module = null;
        Analysis = null;
        _moduleEdited = false;
        _picked = null;
        foreach (var r in _musicList.Children.OfType<TreeItem>()) r.IsSelected = file != null && r.Label == Path.GetFileName(file);
        if (file != null)
        {
            try
            {
                Module = TrackerModule.Load(file);
                Analysis = ModuleAnalysis.Of(Module);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or IndexOutOfRangeException or ArgumentException)
            {
                _s.Toasts.Show($"{Path.GetFileName(file)} could not be read: {e.Message}", ToastStatus.Error);
            }
            Meta = _s.Project.Current?.LoadMeta<MusicMeta>(file) ?? new MusicMeta();
            if (_doc?.Path != file)
            {
                _doc = new EditorDocument { Name = Path.GetFileName(file), Glyph = "music", Path = file, SaveHandler = _ => { Save(); return Task.CompletedTask; } };
                _s.Documents.Add(_doc, activate: true);
            }
        }
        else _doc = null;
        Order = 0;
        Row = 0;
        _trackTab.Label = file is null ? "" : Path.GetFileName(file);
        _trackTab.IsVisible = file != null;
        BuildCenter();
        BuildInspector();
        UpdateStatus();
    }

    /// <summary>Writes the .meta.json and, after tracker edits, the module.</summary>
    public void Save()
    {
        if (Track is null) return;
        GameProject.Save(GameProject.MetaPath(Track), Meta);
        if (_moduleEdited && Module != null)
        {
            File.WriteAllBytes(Track, Module.Encode());
            _moduleEdited = false;
            lock (LengthCache) LengthCache.Remove(Track);
        }
        if (_doc != null) _doc.IsDirty = false;
    }

    void Dirty() { if (_doc != null) _doc.IsDirty = true; }

    // ---- centre ---------------------------------------------------------------------------------------

    void BuildCenter()
    {
        _mixer.Clear();
        _overview = null;
        _grid = null;
        if (Module is null || Analysis is null)
        {
            var empty = UI.Text(MusicFiles.Count == 0 ? "No music in this project yet. Use + to import a .mod or .xm file." : "Select a track.", "Body/12", "text/muted");
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            _center.Content = empty;
            return;
        }
        var m = Module;
        var a = Analysis;
        var play = new PlayControl { Kind = PlayKind.Play, Name = "TrackPlay" };
        play.Click += (_, _) => Play();
        var stop = new PlayControl { Kind = PlayKind.Stop, Name = "TrackStop" };
        stop.Click += (_, _) => Stop();
        var loop = new Chip { Content = LoopText(), Glyph = "refresh", Name = "LoopChip" };
        loop.Click += (_, _) => Seek(Math.Min(Meta.LoopStart, m.Order.Count - 1), 0);
        var buttons = UI.Row(10, play, stop, loop);
        var stats = UI.Row(8, Stat("BPM", a.Bpm.ToString(CultureInfo.InvariantCulture)), Stat("Speed", a.Speed.ToString(CultureInfo.InvariantCulture)),
            Stat("Patterns", a.PatternCount.ToString(CultureInfo.InvariantCulture)), Stat("Length", ModuleAnalysis.FormatTime(a.Seconds)));
        stats.HorizontalAlignment = HorizontalAlignment.Right;
        var top = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*"), Height = 30 };
        Grid.SetColumn(stats, 1);
        top.Children.AddRange([buttons, stats]);

        _overview = new WaveformOverview { Peaks = a.Overview, Margin = new Thickness(0, 12, 8, 0), Name = "Overview" };
        _overview.Seek += k => { var (o, r) = a.RowAt(k * a.Seconds); Seek(o, r); };
        _grid = new TrackerGrid { Module = m, Margin = new Thickness(0, 10, 0, 0), Name = "Tracker", HorizontalAlignment = HorizontalAlignment.Left, Width = TrackerGrid.LabelWidth + m.Channels * TrackerGrid.ColumnPitch };
        _grid.CellPressed += CellPressed;
        _grid.Scrolled += d => Step(d);

        var mixer = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("53,82,*,86"), Margin = new Thickness(0, 9, 0, 0), Name = "Mixer" };
        var title = UI.Text("Mixer", "Overline/11", "text/secondary");
        title.VerticalAlignment = VerticalAlignment.Top;
        title.Margin = new Thickness(0, 5, 0, 0);
        mixer.Children.Add(title);
        var levels = new List<(string, double)>();
        var rowIndex = 0;
        for (var c = 0; c < m.Channels; c++)
        {
            if (!m.ChannelHasNotes(c)) continue;
            mixer.RowDefinitions.Add(new RowDefinition(24, GridUnitType.Pixel));
            var name = m.ChannelName(c);
            var db = c < a.ChannelDb.Length ? a.ChannelDb[c] : double.NegativeInfinity;
            levels.Add((name, db));
            var label = UI.Text(name, "Body/12", "text/secondary");
            var bar = new AgbProgress { Status = ProgressStatus.Accent, Value = DbPercent(db), VerticalAlignment = VerticalAlignment.Center };
            var value = UI.Text(DbText(db), "Mono/10", "text/secondary");
            value.HorizontalAlignment = HorizontalAlignment.Right;
            foreach (var (ctl, col) in new (Control, int)[] { (label, 1), (bar, 2), (value, 3) }) { Grid.SetRow(ctl, rowIndex); Grid.SetColumn(ctl, col); mixer.Children.Add(ctl); }
            _mixer.Add((name, bar, value));
            rowIndex++;
        }
        if (rowIndex > 0) Grid.SetRowSpan(title, rowIndex);
        MixerLevels = levels;

        var stack = UI.Column(0, top, _overview, _grid, mixer);
        stack.Margin = new Thickness(16, 12, 16, 12);
        _center.Content = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        RefreshPosition();
    }

    static StackPanel Stat(string label, string value)
    {
        var col = UI.Column(0, UI.Text(label, "Label/11", "text/muted"), UI.Text(value, "Mono/11 Bold", "text/primary"));
        col.Name = "Stat_" + label;
        return col;
    }

    string LoopText()
    {
        var last = Math.Max(0, (Module?.Order.Count ?? 1) - 1);
        return $"Loop · pattern {Math.Min(Meta.LoopStart, last)}–{last}";
    }

    /// <summary>Mixer bar: -20 dB .. 0 dB over the bar.</summary>
    public static double DbPercent(double db) => double.IsNegativeInfinity(db) ? 0 : Math.Clamp(100 + 5 * db, 0, 100);
    public static string DbText(double db) => double.IsNegativeInfinity(db) ? "-inf dB" : $"{Math.Round(db).ToString("0", CultureInfo.InvariantCulture)} dB";

    // ---- position, playback ---------------------------------------------------------------------------

    /// <summary>Moves the view (and a running preview) to an order position and row.</summary>
    public void Seek(int order, int row)
    {
        if (Module is null) return;
        Order = Math.Clamp(order, 0, Math.Max(0, Module.Order.Count - 1));
        var rows = PatternRows(Order);
        Row = Math.Clamp(row, 0, Math.Max(0, rows - 1));
        if (Player != null && AudioPreview.Of(_s).Music == Player) Player.Seek(Order, Row);
        RefreshPosition();
    }

    int PatternRows(int order) => Module is { } m && order < m.Order.Count && m.Order[order] < m.Patterns.Count ? m.Patterns[m.Order[order]].Rows : 64;

    void Step(int rows)
    {
        if (Module is null) return;
        var o = Order;
        var r = Row + rows;
        if (r < 0 && o > 0) { o--; r += PatternRows(o); }
        else if (r >= PatternRows(o) && o < Module.Order.Count - 1) { r -= PatternRows(o); o++; }
        Seek(o, r);
    }

    void RefreshPosition()
    {
        if (Module is null || Analysis is null) return;
        if (_grid != null)
        {
            _grid.PatternIndex = Order < Module.Order.Count ? Module.Order[Order] : 0;
            _grid.CurrentRow = Row;
        }
        if (_overview != null) _overview.Playhead = Analysis.Seconds > 0 ? Analysis.TimeOf(Order, Row) / Analysis.Seconds : 0;
    }

    public bool IsPlaying => Player != null && AudioPreview.Of(_s).Music == Player;

    public void Play()
    {
        if (Module is null) return;
        var preview = AudioPreview.Of(_s);
        Player = new ModulePlayer(Module, AudioPreview.Rate) { Volume = Meta.Volume, LoopOrder = Math.Min(Meta.LoopStart, Module.Order.Count - 1) };
        Player.Seek(Order, Row);
        preview.PlayMusic(Player);
        _timer.Start();
    }

    public void Stop()
    {
        StopPreview();
        Seek(0, 0);
    }

    void StopPreview()
    {
        _timer.Stop();
        if (Player != null && _s.GetService(typeof(AudioPreview)) is AudioPreview p && p.Music == Player) p.StopMusic();
        Player = null;
    }

    /// <summary>Follows the playing preview (timer tick; tests call it directly).</summary>
    public void Tick()
    {
        if (Player is null) { _timer.Stop(); return; }
        Order = Player.Order;
        Row = Player.Row;
        RefreshPosition();
        if (Player.Ended) StopPreview();
    }

    public void PreviewSound(string wav)
    {
        try
        {
            var w = WavData.Load(wav);
            AudioPreview.Of(_s).PlaySound(w.Resample(AudioPreview.Rate));
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            _s.Toasts.Show($"{Path.GetFileName(wav)} could not be played: {e.Message}", ToastStatus.Error);
        }
    }

    // ---- tracker tools --------------------------------------------------------------------------------

    public void SetTool(Tool t)
    {
        CurrentTool = t;
        for (var i = 0; i < _tools.Length; i++) _tools[i].IsActive = i == (int)t;
    }

    void CellPressed(int row, int channel) => ApplyTool(row, channel);

    /// <summary>Select picks the cell and moves to its row; Pencil writes the picked cell; Eraser clears the cell.</summary>
    public void ApplyTool(int row, int channel)
    {
        if (Module is null || _grid is null) return;
        var pattern = Module.Patterns[_grid.PatternIndex];
        switch (CurrentTool)
        {
            case Tool.Select:
                _picked = pattern.Cells[row, channel];
                _grid.Selected = (row, channel);
                Seek(Order, row);
                break;
            case Tool.Pencil when _picked is { } cell:
                SetCell(pattern, row, channel, cell, "Write note");
                break;
            case Tool.Eraser:
                SetCell(pattern, row, channel, default, "Clear note");
                break;
        }
    }

    void SetCell(TrackerPattern pattern, int row, int channel, TrackerCell value, string name)
    {
        var old = pattern.Cells[row, channel];
        if (old == value) return;
        void Apply(TrackerCell c) { pattern.Cells[row, channel] = c; _moduleEdited = true; Dirty(); _grid?.InvalidateVisual(); }
        _s.Undo.Do(new DelegateAction(name, () => Apply(old), () => Apply(value)));
    }

    // ---- inspector ------------------------------------------------------------------------------------

    void BuildInspector()
    {
        _inspector.Children.Clear();
        if (Track != null && Module != null)
        {
            var header = new SectionHeader { Title = Path.GetFileName(Track), Glyph = "music", Name = "TrackSection" };
            header.TemplateApplied += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                foreach (var i in header.GetVisualDescendantsOfType<Icon>().Where(i => i.Glyph == "music")) i.Token(Icon.ForegroundProperty, "kind/audio");
            });
            var format = new InspectorField { Kind = FieldKind.Dropdown, Label = "Format", Value = $"{Module.Format} → Maxmod bank", Name = "FormatField" };
            var volume = new InspectorField { Kind = FieldKind.Slider, Label = "Volume", SliderValue = Meta.Volume * 100, Value = VolumeText(Meta.Volume), Name = "VolumeField" };
            volume.PropertyChanged += (_, e) =>
            {
                if (e.Property != InspectorField.SliderValueProperty) return;
                var v = Math.Round(volume.SliderValue / 100, 2);
                volume.Value = VolumeText(v);
                if (Math.Abs(v - Meta.Volume) < 1e-9) return;
                var old = Meta.Volume;
                _s.Undo.Do(new DelegateAction("Volume", () => SetVolume(old, volume), () => SetVolume(v, volume)));
            };
            var loop = new InspectorField { Kind = FieldKind.Text, Label = "Loop start", Value = $"pattern {Meta.LoopStart}", Name = "LoopStartField" };
            loop.PropertyChanged += (_, e) =>
            {
                if (e.Property != InspectorField.ValueProperty) return;
                var digits = new string((loop.Value ?? "").Where(char.IsDigit).ToArray());
                if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return;
                n = Math.Clamp(n, 0, Math.Max(0, Module.Order.Count - 1));
                if (n == Meta.LoopStart) return;
                var old = Meta.LoopStart;
                _s.Undo.Do(new DelegateAction("Loop start", () => SetLoop(old), () => SetLoop(n)));
            };
            var fade = new AgbSwitch { IsChecked = Meta.FadeOnSceneChange, Name = "FadeSwitch", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            fade.IsCheckedChanged += (_, _) =>
            {
                var v = fade.IsChecked == true;
                if (v == Meta.FadeOnSceneChange) return;
                _s.Undo.Do(new DelegateAction("Fade on scene change", () => SetFade(!v, fade), () => SetFade(v, fade)));
            };
            var fields = UI.Column(6, format, volume, loop, PageFrame.FieldRow("Fade on scene change", fade, 32));
            _inspector.Children.Add(PageFrame.Section(header, fields));
        }

        var channels = UI.Column(6);
        channels.Name = "ChannelMeters";
        foreach (var (name, pct, value) in Channels)
            channels.Children.Add(new Meter { Label = name, Percent = pct, ValueText = value, Status = name.StartsWith("DSound", StringComparison.Ordinal) ? ProgressStatus.Accent : ProgressStatus.OK });
        _inspector.Children.Add(PageFrame.Section(new SectionHeader { Title = "GBA channels", Glyph = "chip" }, channels));

        var buttons = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Name = "PreviewSfx" };
        foreach (var f in SoundFiles)
        {
            var file = f;
            var b = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "play", Content = GameProject.AssetName(f), Name = "PreviewSfx_" + GameProject.AssetName(f) };
            b.Click += (_, _) => PreviewSound(file);
            buttons.Children.Add(b);
        }
        _inspector.Children.Add(PageFrame.Section(new SectionHeader { Title = "Preview SFX", Glyph = "sound" }, buttons));
    }

    static string VolumeText(double v) => v.ToString("0.0#", CultureInfo.InvariantCulture);

    void SetVolume(double v, InspectorField f)
    {
        Meta.Volume = v;
        if (Math.Abs(f.SliderValue - v * 100) > 0.5) f.SliderValue = v * 100;
        f.Value = VolumeText(v);
        if (Player != null) Player.Volume = v;
        Dirty();
    }

    void SetLoop(int n)
    {
        Meta.LoopStart = n;
        if (Player != null) Player.LoopOrder = n;
        if (_inspector.GetVisualDescendantsOfType<InspectorField>().FirstOrDefault(x => x.Name == "LoopStartField") is { } f && f.Value != $"pattern {n}") f.Value = $"pattern {n}";
        if (_center.GetVisualDescendantsOfType<Chip>().FirstOrDefault(c => c.Name == "LoopChip") is { } chip) chip.Content = LoopText();
        Dirty();
    }

    void SetFade(bool v, AgbSwitch sw)
    {
        Meta.FadeOnSceneChange = v;
        if (sw.IsChecked != v) sw.IsChecked = v;
        Dirty();
    }

    /// <summary>GBA channels: Direct Sound A / B for Maxmod (music and samples), the four DMG channels for the .sfx effects.</summary>
    void UpdateChannels()
    {
        var p = _s.Project.Current;
        var list = new List<(string, double, string)>();
        var maxmod = MusicFiles.Count + SoundFiles.Count > 0;
        list.Add(("DSound A", maxmod ? 100 : 0, maxmod ? "music L" : "free"));
        list.Add(("DSound B", maxmod ? 100 : 0, maxmod ? "music R" : "free"));
        var effects = new List<SfxFile>();
        foreach (var f in p?.SfxFiles() ?? [])
        {
            try { effects.Add(GameProject.Load<SfxFile>(f)); }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { }
        }
        foreach (var (label, kind) in new[] { ("Square 1", DmgChannelKind.Square1), ("Square 2", DmgChannelKind.Square2), ("Wave", DmgChannelKind.Wave), ("Noise", DmgChannelKind.Noise) })
        {
            var on = effects.Where(e => e.Channel == kind && !e.Bake).ToList();
            var busy = Math.Min(1, on.Sum(e => e.Length));
            var names = on.Select(e => e.Name.Length > 0 ? e.Name : "sfx").Order(StringComparer.Ordinal).ToList();
            list.Add((label, busy * 100, names.Count == 0 ? "free" : "SFX " + names[0] + (names.Count > 1 ? $" +{names.Count - 1}" : "")));
        }
        Channels = list;
    }

    readonly Meter _romMeter = new() { Label = "Audio ROM", Name = "AudioRomMeter" };
    readonly Meter _cpuMeter = new() { Label = "Mixer CPU", Name = "MixerCpuMeter" };

    Border BudgetFooter()
    {
        var b = UI.Frame(UI.Column(7, _romMeter, _cpuMeter), "bg/sunken", "border/default", new Thickness(0, 0, 0, 0), new Thickness(12, 10, 12, 10));
        b.Name = "AudioBudget";
        return b;
    }

    /// <summary>Audio ROM: the soundbank of the last build report, else the converted size of the audio files.</summary>
    public long AudioRomBytes { get; private set; }
    public int MixerCpuPercent { get; private set; }

    void UpdateBudget()
    {
        var p = _s.Project.Current;
        var report = p is null ? null : BuildReport.Load(p.BuildDir);
        AudioRomBytes = report?.Breakdown.GetValueOrDefault("Audio") is > 0 and var built
            ? built
            : MusicFiles.Sum(f => new FileInfo(f).Length) + SoundFiles.Sum(f => (long)Math.Ceiling(SoundSeconds(f) * 11025));
        var capacity = report is { RomBytes: > 0 } ? report.RomBytes : 32L * 1024 * 1024;
        _romMeter.Percent = Math.Clamp(100.0 * AudioRomBytes / capacity, 0, 100);
        _romMeter.ValueText = $"{Math.Round(AudioRomBytes / 1024.0).ToString(CultureInfo.InvariantCulture)} KB";
        var a = p?.Settings.Audio ?? new AudioSettings();
        MixerCpuPercent = MixerCpu(a.Channels, a.MixKHz);
        _cpuMeter.Percent = MixerCpuPercent;
        _cpuMeter.ValueText = $"{MixerCpuPercent} %";
        _statusRom.Content = $"Audio ROM {Math.Round(AudioRomBytes / 1024.0).ToString(CultureInfo.InvariantCulture)} KB";
        _statusCpu.Content = $"CPU {MixerCpuPercent}%";
    }

    /// <summary>Maxmod software mixing cost: about 6 % of the CPU for 8 channels at 21 kHz, linear in both.</summary>
    public static int MixerCpu(int channels, int khz) => (int)Math.Round(channels * khz / 28.0);

    void UpdateStatus()
    {
        _statusTrack.Content = Track is null || Analysis is null ? null : $"{Path.GetFileName(Track)} · {ModuleAnalysis.FormatTime(Analysis.Seconds)} · {Analysis.Bpm} BPM";
        _statusTrack.IsVisible = _statusTrack.Content != null;
    }
}

static class VisualHelpers
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual v) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>();
}
