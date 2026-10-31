using System.Globalization;
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
/// Build Report (T-P11; #34 Build status, Build › Build Report, Budget Details › Open Build Report). Reads the real
/// build/report.json and report-history.json: ROM size, build time split, warnings, free VRAM, ROM breakdown, largest
/// assets and history; the cartridge header fields edit Project Settings; Export opens the build folder, runs the ROM
/// in mGBA (Play Mode) or flashes it; Build again rebuilds (P11.58).
/// </summary>
[EditorWindow("T-P11")]
public sealed class BuildReportPage : DockPanel
{
    readonly EditorServices _s;
    readonly ContentControl _content = new() { Name = "ReportContent" };
    readonly StackPanel _header = new() { Spacing = 6, Name = "CartridgeFields" };
    readonly StatusItem _statusBuild, _statusTime, _statusRom;
    readonly EditorDocument _doc = new() { Name = "Build Report", Glyph = "chip" };

    public BuildReport? Report { get; private set; }
    public IReadOnlyList<BuildReport.HistoryEntry> History { get; private set; } = [];

    public BuildReportPage(WindowContext ctx) : this(ctx.Services) { }

    public BuildReportPage(EditorServices s)
    {
        _s = s;
        Name = "BuildReportPage";
        var again = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "refresh", Content = "Build again", Name = "BuildAgain" };
        again.Click += (_, _) => BuildAgain();
        var toolbar = PageFrame.Toolbar(s, again);
        _statusBuild = PageFrame.Status("Status_Build", "check", null, dot: true);
        _statusTime = PageFrame.Status("Status_Time", "clock", null);
        _statusRom = PageFrame.Status("Status_RomFile", "chip", null);
        var status = PageFrame.StatusBar([_statusBuild, _statusTime], [_statusRom]);

        var right = new DockPanel();
        var ph = new PanelHeader { Title = "Cartridge header", Glyph = "chip", Name = "CartridgeHeader" };
        DockPanel.SetDock(ph, Dock.Top);
        var open = ExportButton("Open build folder", "folder", ButtonKind.Secondary, OpenBuildFolder, "OpenBuildFolder");
        var run = ExportButton("Run in mGBA", "play", ButtonKind.Secondary, RunInMgba, "RunInMgba");
        var flash = ExportButton("Flash to cartridge", "chip", ButtonKind.Primary, () => _ = _s.Windows.Open("T-D07", source: "BuildReportFlash"), "FlashToCartridge");
        var export = PageFrame.Section(new SectionHeader { Title = "Export", Glyph = "upload" }, UI.Column(6, open, run, flash));
        right.Children.AddRange([ph, UI.Column(0, PageFrame.Section(new SectionHeader { Title = "Header", Glyph = "chip" }, _header), export)]);

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,320"), Name = "Body" };
        var center = UI.Frame(new ScrollViewer { Content = _content }, "bg/base").Named("CenterPanel");
        var r = UI.Frame(right, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("RightPanel");
        Grid.SetColumn(r, 1);
        body.Children.AddRange([center, r]);
        Children.Add(PageFrame.Page(toolbar, status, body));

        s.Build.Finished += _ => Avalonia.Threading.Dispatcher.UIThread.Post(Reload);
        // the breadcrumb shows "Sky Hopper / Build Report" while the page is open
        AttachedToVisualTree += (_, _) =>
        {
            if (!_s.Documents.Open.Contains(_doc)) _s.Documents.Add(_doc);
            _s.Documents.Active = _doc;
            Reload();
        };
        DetachedFromVisualTree += (_, _) => _s.Documents.Close(_doc);
        Reload();
    }

    static AgbButton ExportButton(string text, string glyph, ButtonKind kind, Action click, string name)
    {
        var b = new AgbButton { Kind = kind, Glyph = glyph, Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Name = name };
        b.Click += (_, _) => click();
        return b;
    }

    public void BuildAgain() => _ = _s.Windows.Open("T-D06", source: "BuildReportBuildAgain");

    public void OpenBuildFolder()
    {
        if (_s.Project.Current is { } p) _s.System.ShowInExplorer(Directory.Exists(p.BuildDir) ? p.BuildDir : p.Root);
    }

    /// <summary>Run in mGBA: Play Mode (T-P02) with the built ROM.</summary>
    public void RunInMgba()
    {
        var rom = RomPath;
        if (rom is null || !File.Exists(rom)) { _s.Toasts.Show("Build the ROM first.", ToastStatus.Error); return; }
        _ = _s.Windows.Open("T-P02", rom, source: "BuildReportRunInMgba");
    }

    public string? RomPath => _s.Project.Current is { } p && Report is { Rom.Length: > 0 } r ? Path.Combine(p.BuildDir, r.Rom) : null;

    /// <summary>Reads report.json and the history again.</summary>
    public void Reload()
    {
        var p = _s.Project.Current;
        Report = _s.Build.LoadReport();
        History = p is null ? [] : BuildStream.LoadHistory(p);
        _content.Content = Report is null ? Empty() : ReportView(Report, p!);
        BuildHeaderFields();
        var last = History.FirstOrDefault();
        var ok = last is null ? Report != null : last.Errors == 0;
        // a failed build names its error count (the history line below shows the first error; Build again is in the toolbar)
        _statusBuild.Content = Report is null && last is null ? "No build yet" : ok ? "Build OK"
            : last!.Errors == 1 ? "Build failed · 1 error" : $"Build failed · {last!.Errors} errors";
        _statusBuild.Classes.Set("warn", !ok);
        _statusTime.Content = Report is null ? null : $"{Secs(Report.BuildSeconds)} s · {Report.BuiltAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
        _statusTime.IsVisible = Report != null;
        _statusRom.Content = Report?.Rom;
        _statusRom.IsVisible = Report != null;
    }

    static Control Empty()
    {
        var t = UI.Text("No build yet. Build the ROM to see its report.", "Body/12", "text/muted");
        t.Margin = new Thickness(20);
        return t;
    }

    static string Secs(double s) => s.ToString("0.00", CultureInfo.InvariantCulture);

    Control ReportView(BuildReport r, GameProject p)
    {
        var root = UI.Column(0);
        root.Margin = new Thickness(20, 20, 20, 20);

        // cards
        var warnings = r.Warnings.Count;
        var (freeVram, vramNote) = FreeVram(r);
        var stepTimes = $"C# {StepSecs(r, GameBuilder.StepScripts)} · assets {StepSecs(r, GameBuilder.StepAssets)} · g++ {StepSecs(r, GameBuilder.StepCompile)}";
        var cards = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*,*"), ColumnSpacing = 13, Name = "Cards" };
        var list = new[]
        {
            Card("ROM size", PageFrame.Kb(r.RomBytes), "text/primary", $"of {r.RomCapacity / 1024 / 1024} MB cartridge · {(100.0 * r.RomBytes / Math.Max(1, r.RomCapacity)).ToString("0.0", CultureInfo.InvariantCulture)} %", "Card_RomSize"),
            Card("Build time", Secs(r.BuildSeconds) + " s", "text/primary", stepTimes, "Card_BuildTime"),
            Card("Warnings", warnings.ToString(CultureInfo.InvariantCulture), warnings > 0 ? "status/warning" : "status/success", warnings > 0 ? WarningSummary(r.Warnings[0]) : "No warnings", "Card_Warnings"),
            Card("Free VRAM", PageFrame.Kb(freeVram), freeVram >= 16 * 1024 ? "status/success" : "status/warning", vramNote, "Card_FreeVram"),
        };
        for (var i = 0; i < list.Length; i++) { Grid.SetColumn(list[i], i); cards.Children.Add(list[i]); }
        root.Children.Add(cards);

        // ROM breakdown
        root.Children.Add(Overline("ROM breakdown", 16));
        var parts = new[] { ("Graphics", "accent/default"), ("Code", "kind/script"), ("Audio", "kind/audio"), ("Data", "kind/data") };
        var total = Math.Max(1, parts.Sum(x => r.Breakdown.GetValueOrDefault(x.Item1)));
        var bar = new Grid { Height = 22, Width = 960, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 0), ClipToBounds = true, Name = "Breakdown" };
        var barBorder = new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = bar, HorizontalAlignment = HorizontalAlignment.Left };
        var legend = UI.Row(18);
        legend.Margin = new Thickness(0, 16, 0, 0);
        legend.Name = "BreakdownLegend";
        foreach (var (name, token) in parts)
        {
            var bytes = r.Breakdown.GetValueOrDefault(name);
            bar.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(0, bytes), GridUnitType.Star));
            var seg = new Border().Token(Border.BackgroundProperty, token);
            Grid.SetColumn(seg, bar.ColumnDefinitions.Count - 1);
            bar.Children.Add(seg);
            var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center }.Token(Border.BackgroundProperty, token);
            legend.Children.Add(UI.Row(6, dot, UI.Text($"{name} · {PageFrame.Kb(bytes)}", "Label/11", "text/secondary")));
        }
        _ = total;
        root.Children.Add(barBorder);
        root.Children.Add(legend);

        // largest assets
        root.Children.Add(Overline("Largest assets", 16));
        var table = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("300,140,120,260"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 0), Name = "LargestAssets" };
        table.RowDefinitions.Add(new RowDefinition(28, GridUnitType.Pixel));
        string[] heads = ["Asset", "Type", "Size", "Share"];
        for (var c = 0; c < 4; c++) Put(table, new TableCell { Kind = TableCellKind.Header, Text = heads[c] }, 0, c);
        var largest = r.LargestAssets.Take(5).ToList();
        var max = Math.Max(1, largest.Count == 0 ? 1 : largest.Max(a => a.Bytes));
        for (var i = 0; i < largest.Count; i++)
        {
            var a = largest[i];
            table.RowDefinitions.Add(new RowDefinition(30, GridUnitType.Pixel));
            Put(table, new TableCell { Text = AssetFileName(p, a), Height = 30 }, i + 1, 0);
            Put(table, new TableCell { Text = a.Type, Height = 30 }, i + 1, 1);
            Put(table, new TableCell { Text = PageFrame.Kb(a.Bytes), Kind = TableCellKind.Mono, Height = 30 }, i + 1, 2);
            var share = new AgbProgress { Status = ProgressStatus.Accent, Value = 100.0 * a.Bytes / max, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
            Put(table, new TableCell { Height = 30 }, i + 1, 3);
            Put(table, share, i + 1, 3);
        }
        root.Children.Add(table);

        // history
        root.Children.Add(Overline("History", 16));
        var history = UI.Column(6);
        history.Margin = new Thickness(0, 4, 0, 0);
        history.Name = "History";
        foreach (var h in History.Take(12))
        {
            var ok = h.Errors == 0;
            var text = ok ? $"{PageFrame.Kb(h.RomBytes)} · {Secs(h.Seconds)} s" + (h.Warnings > 0 ? $" · {h.Warnings} warning{(h.Warnings == 1 ? "" : "s")}" : "") : h.FirstError;
            var row = UI.Row(10, UI.Text(h.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture), "Mono/10", "text/muted"),
                PageFrame.Glyph(ok ? "check" : "x", 12, ok ? "status/success" : "status/danger"), UI.Text(text, "Mono/11", ok ? "status/success" : "status/danger"));
            row.Height = 20;
            history.Children.Add(row);
        }
        root.Children.Add(history);
        return root;
    }

    static void Put(Grid g, Control c, int row, int col)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        g.Children.Add(c);
    }

    static TextBlock Overline(string text, double top)
    {
        var t = UI.Text(text, "Overline/11", "text/secondary");
        t.Margin = new Thickness(0, top, 0, 0);
        return t;
    }

    static Border Card(string title, string value, string valueToken, string sub, string name)
    {
        var v = UI.Text(value, "Title/18", valueToken);
        v.FontSize = 22;   // Figma sets 22 px on the Title/18 numbers of these cards
        v.LineHeight = 26;
        v.Margin = new Thickness(0, 2, 0, 0);
        var s = UI.Text(sub, "Label/11", "text/muted");
        s.Margin = new Thickness(0, 3, 0, 0);
        s.TextTrimming = TextTrimming.CharacterEllipsis;
        var b = UI.Frame(UI.Column(0, UI.Text(title, "Overline/11", "text/secondary"), v, s), "bg/panel", "border/default", new Thickness(1), new Thickness(15, 15, 15, 14), 10);
        b.Name = name;
        b.Height = 90;
        return b;
    }

    static string StepSecs(BuildReport r, string step) => r.StepSeconds.TryGetValue(step, out var s) ? Secs(s) : "0.00";

    /// <summary>"GAE002 float in PlayerController" from "PlayerController.cs(8,5): GAE002 float → use fixed".</summary>
    public static string WarningSummary(string warning)
    {
        var m = System.Text.RegularExpressions.Regex.Match(warning, @"^(?<file>[^(:]+?)(\(\d+(,\d+)?\))?: (?<code>[A-Z]+\d+) (?<msg>\S+)");
        return m.Success ? $"{m.Groups["code"].Value} {m.Groups["msg"].Value} in {Path.GetFileNameWithoutExtension(m.Groups["file"].Value)}" : warning;
    }

    /// <summary>Free VRAM of the scene that uses the most (96 KB minus its BG and OBJ tiles).</summary>
    public static (long Free, string Note) FreeVram(BuildReport r)
    {
        const long vram = 96 * 1024;
        var worst = r.Scenes.Count == 0 ? 0 : r.Scenes.Values.Max(s => s.VramBgBytes + s.VramObjBytes);
        return (Math.Max(0, vram - worst), "of 96 KB");
    }

    /// <summary>The file name of a report asset ("bgm_forest" -> "bgm_forest.xm").</summary>
    public static string AssetFileName(GameProject p, BuildReport.ReportAsset a)
    {
        IEnumerable<string> files = a.Type switch
        {
            "Music" => p.MusicFiles(),
            "Sound" => p.SoundFiles(),
            "Sprite" => p.SpriteFiles(),
            "Tileset" => p.TilesetFiles().Concat(p.ImageFiles()),
            "Font" => p.FontFiles(),
            "Data" => p.TableFiles(),
            "Map" => p.MapFiles(),
            _ => [],
        };
        var match = files.FirstOrDefault(f => string.Equals(GameProject.AssetName(f), a.Name, StringComparison.OrdinalIgnoreCase));
        return match is null ? a.Name : Path.GetFileName(match);
    }

    // ---- cartridge header -----------------------------------------------------------------------------

    public static readonly IReadOnlyList<(SaveType Type, string Text)> SaveTypes =
        [(SaveType.None, "None"), (SaveType.Sram32K, "SRAM · 32 KB"), (SaveType.Flash64K, "Flash · 64 KB"), (SaveType.Flash128K, "Flash · 128 KB"), (SaveType.Eeprom8K, "EEPROM · 8 KB")];

    void BuildHeaderFields()
    {
        _header.Children.Clear();
        var g = _s.ProjectSettings.Settings.General;
        var save = _s.ProjectSettings.Settings.Save;
        _header.Children.Add(TextField("Title", g.RomTitle, v => g.RomTitle = v, 12, upper: true));
        _header.Children.Add(TextField("Game code", g.GameCode, v => g.GameCode = v, 4, upper: true));
        _header.Children.Add(TextField("Maker", g.Maker, v => g.Maker = v, 2, upper: true));
        var type = new InspectorField { Kind = FieldKind.Dropdown, Label = "Save type", Value = SaveTypes.FirstOrDefault(t => t.Type == save.Type).Text, Name = "Field_SaveType" };
        Dropdown(type, SaveTypes.Select(t => t.Text).ToList(), text =>
        {
            var old = save.Type;
            var v = SaveTypes.First(t => t.Text == text).Type;
            if (v == old) return;
            _s.Undo.Do(new DelegateAction("Save type", () => { save.Type = old; Commit(); }, () => { save.Type = v; Commit(); }));
        });
        _header.Children.Add(type);
        _header.Children.Add(TextField("Version", g.Version, v => g.Version = v, 11, upper: false, version: true));
    }

    InspectorField TextField(string label, string value, Action<string> set, int max, bool upper, bool version = false)
    {
        var f = new InspectorField { Kind = FieldKind.Text, Label = label, Value = value, Name = "Field_" + label.Replace(" ", "") };
        var current = value;
        f.PropertyChanged += (_, e) =>
        {
            if (e.Property != InspectorField.ValueProperty) return;
            var v = (f.Value ?? "").Trim();
            if (upper) v = v.ToUpperInvariant();
            if (v.Length > max || v.Length == 0 || (version && !System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+\.\d+\.\d+$"))) return;
            if (v == current) return;
            var old = current;
            current = v;
            _s.Undo.Do(new DelegateAction(label, () => { set(old); current = old; f.Value = old; Commit(); }, () => { set(v); current = v; Commit(); }));
        };
        return f;
    }

    void Commit() => _s.ProjectSettings.Save();

    /// <summary>Opens a menu of options under an Inspector "Dropdown" field.</summary>
    public static void Dropdown(InspectorField field, IReadOnlyList<string> options, Action<string> pick)
    {
        field.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Button>("PART_Dropdown") is not { } b) return;
            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
            foreach (var o in options)
            {
                var text = o;
                var item = new MenuItem { Header = o };
                item.Click += (_, _) => { field.Value = text; pick(text); };
                flyout.Items.Add(item);
            }
            b.Flyout = flyout;
        };
    }
}
