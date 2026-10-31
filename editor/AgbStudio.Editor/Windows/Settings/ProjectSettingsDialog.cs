using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using static AgbStudio.Editor.Windows.Settings.SettingsUi;
using ProjectFile = AgbStudio.Core.Project.ProjectFile;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>
/// Project Settings (T-D08 Input tab, T-S01..T-S05; File › Project Settings…, 900 x 620). Every field edits the open
/// project's <see cref="ProjectFile"/> through <see cref="IProjectSettingsStore"/>; Save writes the .agbproj, and the
/// build reads it (ROM header, save type, start scene, boot splash, remap, analyzer severities ...).
/// </summary>
[EditorWindow("T-D08")]
[EditorWindow("T-S01")]
[EditorWindow("T-S02")]
[EditorWindow("T-S03")]
[EditorWindow("T-S04")]
[EditorWindow("T-S05")]
public sealed class ProjectSettingsDialog : SettingsDialog
{
    public static readonly IReadOnlyList<SettingsTab> AllTabs =
    [
        new("T-S01", "General", "sliders"), new("T-D08", "Input", "pad"), new("T-S02", "Save data", "save"),
        new("T-S03", "Build", "zap"), new("T-S04", "Scripting", "code"), new("T-S05", "Localization", "globe"),
    ];

    readonly EditorServices _s;
    readonly IProjectSettingsStore _store;
    InputAction? _listening;
    Keycap? _listeningCap;

    public ProjectSettingsModel Model { get; }

    public ProjectSettingsDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as string ?? ctx.Info.Id) { }

    public ProjectSettingsDialog(EditorServices s, string tab = "T-S01") : base("Project Settings", AllTabs)
    {
        _s = s;
        _store = s.ProjectSettings;
        Name = "ProjectSettingsDialog";
        Model = new ProjectSettingsModel(_store.Settings, s.Project.Current);
        AddHandler(KeyDownEvent, OnKeyCapture, RoutingStrategies.Tunnel);
        RefreshOn(a => s.Toolchain.Changed += a, a => s.Toolchain.Changed -= a, "T-S03");
        Select(tab);
        Changed();
    }

    ProjectFile D => Model.Draft;

    protected override string? Validate() => Model.Validate();

    protected override void Apply()
    {
        Model.ApplyTo(_store.Settings);
        _store.Save();
    }

    void Edit(Action change)
    {
        change();
        Changed();
    }

    protected override Control BuildTab(string id) => id switch
    {
        "T-D08" => InputTab(),
        "T-S02" => SaveTab(),
        "T-S03" => BuildSettingsTab(),
        "T-S04" => ScriptingTab(),
        "T-S05" => LocalizationTab(),
        _ => GeneralTab(),
    };

    // ---- General (T-S01) -----------------------------------------------------------------------------

    static Grid Cols(int n, params Control[] cells)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse(string.Join(",12,", Enumerable.Repeat("*", n))) };
        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i * 2);
            cells[i].VerticalAlignment = VerticalAlignment.Top;
            g.Children.Add(cells[i]);
        }
        return g;
    }

    static TextField Limit(TextField f, int max, bool upper)
    {
        f.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextBox>("PART_Box") is not { } b) return;
            b.MaxLength = max;
            if (upper)
                b.TextChanged += (_, _) =>
                {
                    if (b.Text is not { } t || t == t.ToUpperInvariant()) return;
                    var caret = b.CaretIndex;
                    b.Text = t.ToUpperInvariant();
                    b.CaretIndex = caret;
                };
        };
        return f;
    }

    Control GeneralTab()
    {
        var g = D.General;
        var title = SettingsUi.Text("Game title", g.GameTitle, v => Edit(() => g.GameTitle = v), name: "GameTitle");
        var version = SettingsUi.Text("Version", g.Version, v => Edit(() => g.Version = v.Trim()), "major.minor.patch", "Version");
        var rom = Limit(SettingsUi.Text("ROM title", g.RomTitle, v => Edit(() => g.RomTitle = v.ToUpperInvariant()), "12 characters max", "RomTitle"), 12, true);
        var code = Limit(SettingsUi.Text("Game code", g.GameCode, v => Edit(() => g.GameCode = v.ToUpperInvariant()), "4 characters", "GameCode"), 4, true);
        var maker = Limit(SettingsUi.Text("Maker", g.Maker, v => Edit(() => g.Maker = v.ToUpperInvariant()), "2 characters", "Maker"), 2, true);
        var start = SettingsUi.Select("Start scene", Model.Scenes, g.StartScene, v => Edit(() => g.StartScene = v), "StartScene");
        var font = SettingsUi.Select("Default font", Model.Fonts, g.DefaultFont, v => Edit(() => g.DefaultFont = v), "DefaultFont");
        var lang = SettingsUi.Select("Default language", Model.LanguageNames, Model.LanguageName(g.DefaultLanguage), v => Edit(() => g.DefaultLanguage = Model.LanguageCode(v)), "DefaultLanguage");
        var splash = SettingsUi.Select("Boot splash", () => ProjectSettingsModel.Splashes, Model.SplashLabel, v => Edit(() => Model.SetSplash(v)), "BootSplash");
        var skip = Check("Skip splash in Debug builds", g.SkipSplashInDebug, v => Edit(() => g.SkipSplashInDebug = v), "SkipSplash");
        return Column(12,
            TitleWithCaption("General", "Shown on the cartridge header and the title screen"),
            Cols(2, title, version), Cols(3, rom, code, maker), Cols(2, start, font), Cols(2, lang, splash), skip);
    }

    // ---- Input (T-D08) -------------------------------------------------------------------------------

    // Figma: ACTION NAME at x 372, KEYBOARD 572, GAMEPAD 752 (table at 232)
    const string InputColumns = "140,200,180,*";

    Control InputTab()
    {
        var table = new StackPanel { Width = 660, HorizontalAlignment = HorizontalAlignment.Left, Name = "InputTable" };
        table.Children.Add(TableHeader(["GBA button", "Action name", "Keyboard (emulator)", "Gamepad"], InputColumns));
        foreach (var a in D.Input)
        {
            var action = a;
            var button = new Keycap { Label = a.Button, HorizontalAlignment = HorizontalAlignment.Left };
            var name = CellBox(a.Action, "Mono/11", v => Edit(() => action.Action = v), "Action_" + a.Button);
            var cap = new Keycap { Label = a.Keyboard, HorizontalAlignment = HorizontalAlignment.Left };
            var key = new Button { Theme = Bare, Content = cap, Name = "Key_" + a.Button, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = a.Button != "D-pad" };
            key.Click += (_, _) => Listen(action, cap);
            var padText = UI.Text(a.Gamepad, "Body/12", "text/primary");
            var pad = new Button { Theme = Bare, Content = padText, Name = "Pad_" + a.Button, HorizontalAlignment = HorizontalAlignment.Left };
            Dropdown.Attach(pad, () => ProjectSettingsModel.GamepadButtons, a.Gamepad, v => padText.Text = v, v => Edit(() => action.Gamepad = v));
            table.Children.Add(TableRow(InputColumns, 34, button, name, key, pad));
        }
        var remap = Check("Let players remap buttons in the options menu", D.AllowRemap, v => Edit(() => D.AllowRemap = v), "AllowRemap");
        return Column(12, Title("Input mapping"),
            Caption("Name each GBA button by what it does. Scripts use the names; the emulator uses the keyboard keys."), table, remap);
    }

    static Avalonia.Styling.ControlTheme? Bare => (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton");

    /// <summary>Keyboard (emulator): the next key pressed becomes the key of this button (Esc cancels).</summary>
    public void Listen(InputAction action, Keycap cap)
    {
        _listening = action;
        _listeningCap = cap;
        cap.Label = "…";
    }

    void OnKeyCapture(object? sender, KeyEventArgs e)
    {
        if (_listening is not { } a || _listeningCap is not { } cap) return;
        e.Handled = true;
        _listening = null;
        _listeningCap = null;
        if (e.Key == Key.Escape) { cap.Label = a.Keyboard; return; }
        var name = KeyName(e.Key);
        Edit(() => a.Keyboard = name);
        cap.Label = name;
    }

    /// <summary>Key names as Project Settings · Input writes them ("Z", "Enter", "Backspace").</summary>
    public static string KeyName(Key k) => k switch
    {
        Key.Back => "Backspace",
        Key.Return => "Enter",
        Key.Space => "Space",
        Key.LeftShift or Key.RightShift => "Shift",
        Key.LeftCtrl or Key.RightCtrl => "Ctrl",
        _ => AgbStudio.Editor.Theme.Shortcuts.KeyName(k),
    };

    // ---- Save data (T-S02) ---------------------------------------------------------------------------

    const string SaveColumns = "150,130,70";
    Meter? _slotsMeter;
    TextBlock? _perSlot;

    Control SaveTab()
    {
        var save = D.Save;
        var type = Segments(ProjectSettingsModel.SaveTypeSegments, ProjectSettingsModel.SaveTypes.ToList().IndexOf(save.Type), i => Edit(() => { save.Type = ProjectSettingsModel.SaveTypes[i]; UpdateSlots(); }), "SaveType");

        var table = new StackPanel { Width = 350, HorizontalAlignment = HorizontalAlignment.Left, Name = "SaveFields" };
        table.Children.Add(TableHeader(["Field", "Type", "Size"], SaveColumns));
        void AddRow(SaveField f)
        {
            var field = f;
            // the size of each field is the bytes its type takes in the save (byte 1 B, short 2 B, int 4 B, bitset N, Table[N] ...)
            var size = Cell(Size(f.Type), true);
            size.Name = "SaveFieldSize_" + save.Fields.IndexOf(f);
            var name = CellBox(f.Name, "Mono/11", v => Edit(() => { field.Name = v; UpdateSlots(); }), "SaveField_" + save.Fields.IndexOf(f));
            var type = CellBox(f.Type, "Mono/11", v => Edit(() => { field.Type = v; size.Text = Size(v); UpdateSlots(); }), "SaveFieldType_" + save.Fields.IndexOf(f));
            table.Children.Insert(table.Children.Count - 1, TableRow(SaveColumns, 30, name, type, size));
        }
        var add = new AgbButton { Kind = ButtonKind.Ghost, Glyph = "plus", Content = "Add field", Name = "AddField", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        // Figma draws "+ Add field" in text/muted, quieter than a plain Ghost button
        add.Token(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, "text/muted");
        table.Children.Add(add);
        foreach (var f in save.Fields) AddRow(f);
        add.Click += (_, _) =>
        {
            var n = 1;
            while (save.Fields.Any(x => x.Name == "field" + n)) n++;
            var f = new SaveField { Name = "field" + n, Type = "int" };
            Edit(() => { save.Fields.Add(f); UpdateSlots(); });
            AddRow(f);
        };

        var slots = TextRow("Slots", save.Slots.ToString(CultureInfo.InvariantCulture), v => Edit(() => { save.Slots = Number(v) ?? 0; UpdateSlots(); }), "Slots");
        var autosave = ToggleRow("Autosave", save.Autosave, v => Edit(() => save.Autosave = v), "Autosave");
        _slotsMeter = new Meter { Name = "SlotsMeter" };
        _perSlot = Caption("");
        UpdateSlots();
        var right = Column(10, slots, autosave, Caption("Autosave runs on every scene change."), _slotsMeter, _perSlot,
            Check("Checksum each slot", save.Checksum, v => Edit(() => save.Checksum = v), "Checksum"), CodeSample());

        var cols = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("350,20,*") };
        Grid.SetColumn(right, 2);
        cols.Children.AddRange([table, right]);
        return Column(12, TitleWithCaption("Save data", "What the cartridge remembers between play sessions"), Overline("Save type"), type, cols);
    }

    static string Size(string type) => ProjectSettingsModel.SafeSize(type) is { } n ? n.ToString(CultureInfo.InvariantCulture) + " B" : "?";

    void UpdateSlots()
    {
        if (_slotsMeter is null || _perSlot is null) return;
        var s = D.Save;
        var per = Model.BytesPerSlot;
        var total = Model.TotalSaveBytes;  // the slots and the 8 B options block (outside the slots)
        var cap = ProjectSettingsModel.SaveCapacity(s.Type);
        _slotsMeter.Label = $"{s.Slots} slots";
        _slotsMeter.ValueText = cap == 0 ? "no save chip" : $"{ProjectSettingsModel.Kb(total)} / {ProjectSettingsModel.Kb(cap)}";
        var pct = cap == 0 ? 0 : 100.0 * total / cap;
        // a used chip always shows a little fill, as in Figma
        _slotsMeter.Percent = total > 0 && cap > 0 ? Math.Max(pct, 10) : pct;
        _slotsMeter.Status = pct > 100 ? ProgressStatus.Danger : pct > 90 ? ProgressStatus.Warn : ProgressStatus.OK;
        _perSlot.Text = $"{per} bytes per slot";
    }

    /// <summary>The save API sample with code colours (type, method, keyword).</summary>
    Control CodeSample()
    {
        var tb = new TextBlock { Name = "SaveSample" };
        AgbStudio.Editor.Theme.Fig.SetStyle(tb, "Mono/11");
        tb.Token(TextBlock.ForegroundProperty, "text/primary");
        void R(string text, string? token = null, bool keyword = false)
        {
            var run = new Run(text);
            if (keyword && Application.Current?.FindResource("lit/accent-pink") is Color pink) run.Foreground = new SolidColorBrush(pink);
            else if (token != null) run.Bind(TextElement.ForegroundProperty, tb.GetResourceObservable(token));
            tb.Inlines!.Add(run);
        }
        R("Save", "accent/hover"); R("."); R("Write", "status/success"); R("(slot);\n");
        R("if", keyword: true); R(" ("); R("Save", "accent/hover"); R("."); R("Read", "status/success"); R("(slot))\n");
        R("    "); R("Scene", "accent/hover"); R("."); R("Load", "status/success"); R("("); R("Save", "accent/hover"); R(".scene);");
        return UI.Frame(tb, "bg/base", pad: new Thickness(10, 7), radius: 6);
    }

    // ---- Build (T-S03) -------------------------------------------------------------------------------

    Control BuildSettingsTab()
    {
        var b = D.Build;
        // Figma: the segment at x 308, 2 px after the 86 px label (not lined up with the fields under it)
        var config = new DockPanel { HorizontalSpacing = 2 };
        var configLabel = UI.Text("Configuration", "Body/12", "text/secondary", 86);
        DockPanel.SetDock(configLabel, Dock.Left);
        config.Children.AddRange([configLabel, Segments(["Debug", "Release"], (int)b.Configuration, i => Edit(() => b.Configuration = (BuildConfiguration)i), "Configuration")]);

        var left = Column(8,
            DropdownRow("Optimise", ProjectSettingsModel.Optimisations.Select(o => o.Label).ToList(), ProjectSettingsModel.OptimiseLabel(b.Optimise), v => Edit(() => b.Optimise = ProjectSettingsModel.OptimiseFlag(v)), "Optimise"),
            TextRow("Output", b.Output, v => Edit(() => b.Output = v), "Output"),
            TextRow("Warn ROM >", $"{b.WarnRomMegabytes} MB", v => Edit(() => b.WarnRomMegabytes = Number(v) ?? 0), "WarnRom"),
            DropdownRow("Save type", ProjectSettingsModel.SaveTypeOptions, ProjectSettingsModel.SaveTypeOptions[ProjectSettingsModel.SaveTypes.ToList().IndexOf(D.Save.Type)],
                v => Edit(() => D.Save.Type = ProjectSettingsModel.SaveTypes[ProjectSettingsModel.SaveTypeOptions.ToList().IndexOf(v)]), "BuildSaveType"));
        var right = Column(10, Overline("Release options"),
            Check("Compress graphics with LZ77", b.CompressGraphics, v => Edit(() => b.CompressGraphics = v), "Compress"),
            Check("Strip Debug.Log calls", b.StripDebugLog, v => Edit(() => b.StripDebugLog = v), "StripLogs"),
            Check("Keep asserts", b.KeepAsserts, v => Edit(() => b.KeepAsserts = v), "KeepAsserts"),
            Check("Run in mGBA after build", b.RunAfterBuild, v => Edit(() => b.RunAfterBuild = v), "RunAfterBuild"));
        var cols = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("320,24,*") };
        Grid.SetColumn(right, 2);
        cols.Children.AddRange([left, right]);

        var change = new AgbButton { Kind = ButtonKind.Secondary, Content = "Change in Preferences…", Name = "ChangeInPreferences" };
        change.Click += (_, _) => _ = _s.Windows.Open("T-S08", source: "ProjectSettings.Build");
        return Column(12, Title("Build"), config, cols, Overline("Toolchain"), ToolchainCard(_s, change, true));
    }

    /// <summary>The Toolchain row (Build tab and Preferences · Emulator): check mark, devkitPro, tools found, button.</summary>
    public static Control ToolchainCard(EditorServices s, Button button, bool versionFirst)
    {
        var tools = s.Toolchain.Tools;
        var ok = s.Toolchain.IsComplete;
        var root = s.Toolchain.Find("devkitPro")?.Path ?? s.Preferences.Current.DevkitProOrNull() ?? new AgbStudio.Build.Toolchain().DevkitPro;
        var arm = s.Toolchain.Find("devkitARM")?.Version ?? "devkitARM";
        var title = UI.Text(versionFirst ? $"{arm} · {root}" : root, "Mono/11 Bold", "text/primary");
        string[] shown = versionFirst ? ["arm-none-eabi-g++", "libtonc", "maxmod", "gbafix"] : [arm, "libtonc", "maxmod", "gbafix"];
        string[] keys = ["devkitARM", "libtonc", "maxmod", "gbafix"];
        var missing = keys.Where(k => tools.Count > 0 && s.Toolchain.Find(k) is { Found: false }).ToList();
        var detail = tools.Count == 0 ? "Checking the toolchain…"
            : missing.Count == 0 ? string.Join(" · ", shown) + " found"
            : string.Join(" · ", keys.Where(k => !missing.Contains(k))) + (keys.Length > missing.Count ? " found · " : "") + string.Join(" · ", missing)
              + " not found · set the devkitPro folder, or install devkitPro";
        var text = UI.Column(2, title, Caption(detail));
        text.VerticalAlignment = VerticalAlignment.Center;
        button.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel { HorizontalSpacing = 12, Height = 54 };
        var icon = StatusIcon(ok || tools.Count == 0);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(button, Dock.Right);
        row.Children.AddRange([icon, button, text]);
        var card = Card(row);
        card.Name = "ToolchainCard";
        if (tools.Count == 0) _ = s.Toolchain.CheckAsync();
        return card;
    }

    // ---- Scripting (T-S04) ---------------------------------------------------------------------------

    const string RuleColumns = "80,400,*";

    Control ScriptingTab()
    {
        var sc = D.Scripting;
        var left = Column(8,
            DropdownRow("Language", [ProjectSettingsModel.ScriptLanguage], sc.Language.Length > 0 ? sc.Language : ProjectSettingsModel.ScriptLanguage, v => Edit(() => sc.Language = v), "ScriptLanguage"),
            TextRow("Generated", sc.Generated, v => Edit(() => sc.Generated = v), "Generated"),
            TextRow("Namespace", sc.Namespace, v => Edit(() => sc.Namespace = v.Trim()), "Namespace"));
        var right = Column(10,
            Check("Hot reload in Play mode", sc.HotReload, v => Edit(() => sc.HotReload = v), "HotReload"),
            Check("Treat warnings as errors", sc.TreatWarningsAsErrors, v => Edit(() => sc.TreatWarningsAsErrors = v), "WarningsAsErrors"),
            Check("Show inline hints in the editor", sc.InlineHints, v => Edit(() => sc.InlineHints = v), "InlineHints"));
        var cols = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("320,24,*") };
        Grid.SetColumn(right, 2);
        cols.Children.AddRange([left, right]);

        var table = new StackPanel { Width = 620, HorizontalAlignment = HorizontalAlignment.Left, Name = "RulesTable" };
        table.Children.Add(TableHeader(["Rule", "Catches", "Severity"], RuleColumns));
        foreach (var (id, catches) in ProjectSettingsModel.AnalyzerRules)
        {
            var rule = id;
            var chip = new Chip { Content = Model.Severity(rule), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
            StyleChip(chip);
            var chevron = new Icon { Glyph = "down", Size = 12, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted");
            // Figma: the chevron 6 px after the chip
            var pick = new Button { Theme = Bare, Content = UI.Row(6, chip, chevron), Name = "Severity_" + rule, HorizontalAlignment = HorizontalAlignment.Left };
            Dropdown.Attach(pick, () => ProjectSettingsModel.Severities, Model.Severity(rule), v => { chip.Content = v; StyleChip(chip); }, v => Edit(() => sc.AnalyzerRules[rule] = v));
            table.Children.Add(TableRow(RuleColumns, 30, Cell(id, true), Cell(catches), pick));
        }
        return Column(12, Title("Scripting"), cols, Overline("Analyzer rules"), table);
    }

    static void StyleChip(Chip c) => c.Style = (string?)c.Content switch
    {
        "Error" => ChipStyle.Danger,
        "Warning" => ChipStyle.Warning,
        "Info" => ChipStyle.Accent,
        _ => ChipStyle.Default,
    };

    // ---- Localization (T-S05) ------------------------------------------------------------------------

    Control LocalizationTab()
    {
        var loc = D.Localization;
        var meters = Column(10);
        foreach (var l in loc.Languages)
        {
            var (done, total) = Model.Progress(l.Code);
            meters.Children.Add(new Meter
            {
                Label = l.Name, ValueText = $"{done} / {total}", Percent = total == 0 ? 0 : 100.0 * done / total,
                Status = total > 0 && done == total ? ProgressStatus.OK : ProgressStatus.Warn, Name = "Lang_" + l.Code,
            });
        }
        var add = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "plus", Content = "Add language", Name = "AddLanguage", HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += async (_, _) =>
        {
            var result = await _s.Windows.Open("T-D20", source: "ProjectSettings.Localization");
            MergeLanguages(result);
            Select("T-S05");
        };
        var fonts = Column(10);
        foreach (var l in loc.Languages)
        {
            var lang = l;
            fonts.Children.Add(DropdownRow(l.Name, () => [ProjectSettingsModel.NoFont, .. Model.Fonts()], l.Font.Length > 0 ? l.Font : ProjectSettingsModel.NoFont,
                v => Edit(() => lang.Font = v == ProjectSettingsModel.NoFont ? "" : v), "Font_" + l.Code, 320));
        }
        var left = Column(10, Overline("Languages"), meters, add, Overline("Font per language"), fonts);
        left.Width = 320;

        var rules = Column(8,
            DropdownRow("Source", Model.LanguageNames, Model.LanguageName(loc.Source), v => Edit(() => loc.Source = Model.LanguageCode(v)), "Source"),
            DropdownRow("Fallback", Model.LanguageNames, Model.LanguageName(Model.Fallback), v => Edit(() => Model.Fallback = Model.LanguageCode(v)), "Fallback"),
            DropdownRow("Thai wrap", ProjectSettingsModel.ThaiWrapOptions, ProjectSettingsModel.ThaiWrapOptions[loc.ThaiWrap ? 0 : 1], v => Edit(() => loc.ThaiWrap = v == ProjectSettingsModel.ThaiWrapOptions[0]), "ThaiWrap"));
        var right = Column(8, Overline("Rules"), rules, Caption("Thai has no spaces between words, so lines break with a word list.", true),
            Check("Show missing keys as [key] in Debug", loc.ShowMissingKeys, v => Edit(() => loc.ShowMissingKeys = v), "ShowMissingKeys"),
            Check("Fail the build on missing strings", loc.FailOnMissing, v => Edit(() => loc.FailOnMissing = v), "FailOnMissing"));
        var cols = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("320,24,*") };
        Grid.SetColumn(right, 2);
        cols.Children.AddRange([left, right]);
        return Column(12, TitleWithCaption("Localization", "Strings live in the Localization page; this sets the rules"), cols);
    }

    /// <summary>After Add Language (T-D20): takes a returned language, and languages the dialog saved into the project.</summary>
    void MergeLanguages(object? result)
    {
        var langs = D.Localization.Languages;
        if (result is LanguageInfo li && langs.All(l => l.Code != li.Code)) langs.Add(li);
        foreach (var l in _store.Settings.Localization.Languages)
            if (langs.All(x => x.Code != l.Code)) langs.Add(new LanguageInfo { Code = l.Code, Name = l.Name, Font = l.Font, Fallback = l.Fallback });
        Changed();
    }
}
