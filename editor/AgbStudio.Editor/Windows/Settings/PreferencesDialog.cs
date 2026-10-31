using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgbStudio.Build.Pipeline;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Scripts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static AgbStudio.Editor.Windows.Settings.SettingsUi;
using Fmt = AgbStudio.Editor.Theme.Shortcuts;
using IOPath = System.IO.Path;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>
/// Preferences (T-D09 Emulator tab, T-S06..T-S09; File › Preferences…, Emulator status, Launcher › Settings; 900 x 620).
/// Per-user settings in preferences.json. Save applies the theme, accent, scale, autosave, undo depth, devkitPro and
/// shortcut bindings at once; Cancel leaves everything as it was.
/// </summary>
[EditorWindow("T-D09")]
[EditorWindow("T-S06")]
[EditorWindow("T-S07")]
[EditorWindow("T-S08")]
[EditorWindow("T-S09")]
public sealed class PreferencesDialog : SettingsDialog
{
    public static readonly IReadOnlyList<SettingsTab> AllTabs =
    [
        new("T-S06", "Appearance", "image"), new("T-S07", "Editor", "code"), new("T-S08", "Paths", "folder"),
        new("T-D09", "Emulator", "pad"), new("T-S09", "Shortcuts", "type"),
    ];

    /// <summary>The rows Preferences · Shortcuts lists when the search is empty (Figma), with the command they bind.</summary>
    public static readonly IReadOnlyList<(string Label, string Command)> DefaultRows =
    [
        ("Command palette", "edit.commandPalette"), ("Save", "file.save"), ("Play", "build.play"), ("Build ROM", "build.buildRom"),
        ("Toggle grid", "scene.toggleGrid"), ("Paint tiles", "scene.tool.paint"), ("Select tool", "scene.tool.select"),
    ];

    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    readonly EditorServices _s;
    readonly Func<string, bool> _exists;

    public Preferences Draft { get; }
    public ShortcutDraft Shortcuts { get; }

    public PreferencesDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as string ?? ctx.Info.Id) { }

    /// <param name="exists">File / folder check for the Paths status marks (tests pass a fake).</param>
    public PreferencesDialog(EditorServices s, string tab = "T-S06", Func<string, bool>? exists = null) : base("Preferences", AllTabs)
    {
        _s = s;
        _exists = exists ?? (p => File.Exists(p) || Directory.Exists(p));
        Name = "PreferencesDialog";
        Draft = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(s.Preferences.Current, Json), Json)!;
        Draft.Shortcuts = new Dictionary<string, string>(Draft.Shortcuts, StringComparer.Ordinal);
        Shortcuts = new ShortcutDraft(s.Commands, Draft.Shortcuts);
        RefreshOn(a => s.Toolchain.Changed += a, a => s.Toolchain.Changed -= a, "T-S08", "T-D09");
        Select(tab);
        Changed();
    }

    void Edit(Action change)
    {
        change();
        Changed();
    }

    protected override string? Validate()
    {
        var p = Draft;
        if (p.UndoHistory is < 1 or > 10000) return "Undo history must be 1 to 10000 steps.";
        if (p.CodeFontSize is < 8 or > 32) return "Code font must be 8 to 32 px.";
        if (p.TabSize is < 1 or > 16) return "Tab size must be 1 to 16.";
        if (p.FastForwardSpeed is < 2 or > 16 || p.FastForwardKey.Length == 0) return "Fast-forward: a key and a speed, like Tab · 4×.";
        return null;
    }

    protected override void Apply()
    {
        var target = _s.Preferences.Current;
        var devkitChanged = target.DevkitPro != Draft.DevkitPro;
        foreach (var prop in typeof(Preferences).GetProperties().Where(p => p.CanRead && p.CanWrite && p.Name != nameof(Preferences.Shortcuts)))
            prop.SetValue(target, prop.GetValue(Draft));
        ShortcutDraft.Commit(new EditorServicesView(_s.Preferences, _s.Commands, _s.Shortcuts), Draft.Shortcuts);
        _s.Theme?.Apply(target);
        if (devkitChanged) _ = _s.Toolchain.CheckAsync();
    }

    protected override Control BuildTab(string id) => id switch
    {
        "T-S07" => EditorTab(),
        "T-S08" => PathsTab(),
        "T-D09" => EmulatorTab(),
        "T-S09" => ShortcutsTab(),
        _ => AppearanceTab(),
    };

    // ---- Appearance (T-S06, T-L03) -------------------------------------------------------------------

    static readonly IReadOnlyList<string> UiScales = ["75%", "90%", "100%", "110%", "125%", "150%", "200%"];
    static readonly string[] AccentKeys = ["lit/accent-blue", "lit/accent-violet", "lit/accent-green", "lit/accent-orange", "lit/accent-pink"];

    Control AppearanceTab()
    {
        var cards = UI.Row(14);
        foreach (var mode in Enum.GetValues<ThemeMode>()) cards.Children.Add(ThemeCard(mode));
        var swatches = UI.Row(8);
        for (var i = 0; i < ThemeService.AccentChoices.Count; i++)
        {
            var hex = ThemeService.AccentChoices[i];
            var color = Application.Current?.FindResource(AccentKeys[i]) is Color c ? c : default;
            var sw = new Swatch { Width = 28, Height = 28, Color = color, State = string.Equals(hex, Draft.Accent, StringComparison.OrdinalIgnoreCase) ? SwatchState.Selected : SwatchState.Default };
            var b = new Button { Theme = Bare, Content = sw, Name = "Accent_" + i };
            b.Click += (_, _) => { Edit(() => Draft.Accent = hex); Select("T-S06"); };
            swatches.Children.Add(b);
        }
        var left = Column(8,
            DropdownRow("UI scale", UiScales, Draft.UiScalePercent + "%", v => Edit(() => Draft.UiScalePercent = Number(v) ?? 100), "UiScale"),
            TextRow("Code font", $"{Draft.CodeFontSize} px", v => Edit(() => Draft.CodeFontSize = Number(v) ?? 0), "CodeFont"));
        var right = Column(10,
            Check("Reduce motion", Draft.ReduceMotion, v => Edit(() => Draft.ReduceMotion = v), "ReduceMotion"),
            Check($"Show pixel grid at {Draft.PixelGridMinZoom}% zoom and above", Draft.ShowPixelGrid, v => Edit(() => Draft.ShowPixelGrid = v), "PixelGrid"));
        return Column(12, Title("Appearance"), Overline("Theme"), cards, Overline("Accent colour"), swatches, TwoColumns(left, right, 320));
    }

    static ControlTheme? Bare => (ControlTheme?)Application.Current?.FindResource("BareButton");

    static Grid TwoColumns(Control left, Control right, double leftWidth)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions($"{leftWidth.ToString(CultureInfo.InvariantCulture)},24,*") };
        Grid.SetColumn(right, 2);
        g.Children.AddRange([left, right]);
        return g;
    }

    /// <summary>Theme card: a 160 x 100 miniature of the editor in that theme (System = half dark, half light).</summary>
    Control ThemeCard(ThemeMode mode)
    {
        var selected = Draft.Theme == mode;
        var preview = new Panel { Width = 160, Height = 100 };
        if (mode == ThemeMode.System)
        {
            var dark = Miniature(ThemeVariant.Dark);
            dark.Clip = new RectangleGeometry(new Rect(0, 0, 80, 100));
            var light = Miniature(ThemeVariant.Light);
            light.Clip = new RectangleGeometry(new Rect(80, 0, 80, 100));
            preview.Children.AddRange([dark, light]);
        }
        else preview.Children.Add(Miniature(mode == ThemeMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light));
        var frame = UI.Frame(null, stroke: selected ? "accent/default" : "border/default", strokes: new Thickness(selected ? 2 : 1), radius: 8);
        preview.Children.Add(frame);
        var clip = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = preview };

        var mark = new Icon { Glyph = selected ? "check" : "dots", Size = 12, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, selected ? "accent/default" : "text/muted");
        var label = UI.Text(mode.ToString(), selected ? "Body/12 Medium" : "Body/12", selected ? "text/primary" : "text/secondary");
        var col = UI.Column(8, clip, UI.Row(6, mark, label));
        var b = new Button { Theme = Bare, Content = col, Name = "Theme_" + mode };
        b.Click += (_, _) => { Edit(() => Draft.Theme = mode); Select("T-S06"); };
        return b;
    }

    /// <summary>The miniature drawn with the tokens of <paramref name="variant"/>; the accent pill and the viewport keep the dark accent, as in Figma.</summary>
    static Control Miniature(ThemeVariant variant)
    {
        var c = new Canvas { Width = 160, Height = 100 };
        void Rect(double x, double y, double w, double h, string token, double r = 3, bool darkAccent = false)
        {
            var rect = new Rectangle { Width = w, Height = h, RadiusX = r, RadiusY = r };
            Control item = rect;
            if (darkAccent) item = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark, Child = rect };
            rect.Bind(Shape.FillProperty, rect.GetResourceObservable(token));
            Canvas.SetLeft(item, x);
            Canvas.SetTop(item, y);
            c.Children.Add(item);
        }
        Rect(0, 0, 160, 100, "bg/base", 0);
        Rect(0, 0, 160, 12, "bg/panel", 0);
        Rect(52, 3, 22, 6, "accent/default", 3, darkAccent: true);
        Rect(6, 18, 34, 76, "bg/panel");
        Rect(46, 18, 68, 46, "accent/hover", 3, darkAccent: true);
        Rect(46, 70, 68, 24, "bg/panel");
        Rect(120, 18, 34, 76, "bg/panel");
        return new ThemeVariantScope { RequestedThemeVariant = variant, Child = c };
    }

    // ---- Editor (T-S07) ------------------------------------------------------------------------------

    static readonly IReadOnlyList<string> Autosaves = ["off", "every 1 min", "every 2 min", "every 5 min", "every 10 min"];
    static readonly IReadOnlyList<string> Snaps = ["8 px", "16 px", "32 px"];
    static readonly IReadOnlyList<string> Editors = ["Visual Studio Code", "JetBrains Rider", "Visual Studio", "Custom…"];
    static readonly IReadOnlyList<string> OpenWith = ["External editor", "Built-in editor"];
    static readonly IReadOnlyList<string> CodeFonts = ["JetBrains Mono"];
    static readonly IReadOnlyList<string> CodeThemes = ["Studio Dark", "Studio Light"];

    Control EditorTab()
    {
        var p = Draft;
        var general = Column(8,
            DropdownRow("Autosave", Autosaves, p.AutosaveMinutes <= 0 ? "off" : $"every {p.AutosaveMinutes} min", v => Edit(() => p.AutosaveMinutes = Number(v) ?? 0), "Autosave"),
            TextRow("Undo history", $"{p.UndoHistory} steps", v => Edit(() => p.UndoHistory = Number(v) ?? 0), "UndoHistory"),
            DropdownRow("Default snap", Snaps, $"{p.DefaultSnap} px", v => Edit(() => p.DefaultSnap = Number(v) ?? 16), "DefaultSnap"));
        var generalChecks = Column(8,
            Check("Open last project on start", p.OpenLastProjectOnStart, v => Edit(() => p.OpenLastProjectOnStart = v), "OpenLastProject"),
            Check("Show collision overlay in Scene", p.ShowCollisionOverlay, v => Edit(() => p.ShowCollisionOverlay = v), "CollisionOverlay"),
            Check("Show GBA budget in Inspector", p.ShowBudgetInInspector, v => Edit(() => p.ShowBudgetInInspector = v), "BudgetInInspector"));
        
        var left = Column(8, Overline("General"), general, generalChecks);

        var exe = TextRow("Executable", ExecutableText(p), _ => { }, "Executable");
        exe.TemplateApplied += (_, e) => { if (e.NameScope.Find<TextBox>("PART_Text") is { } t) t.IsReadOnly = true; };
        var scriptChecks = Column(8,
            Check("Generate .csproj for IntelliSense", p.GenerateCsproj, v => Edit(() => p.GenerateCsproj = v), "GenerateCsproj"),
            Check("Hot reload when an external save is detected", p.HotReloadOnExternalSave, v => Edit(() => p.HotReloadOnExternalSave = v), "HotReloadExternal"));
        
        var right = Column(8, Overline("Script editor"),
            Segments(["Built-in", "External"], (int)p.ScriptEditor, i => Edit(() => p.ScriptEditor = (ScriptEditorMode)i), "ScriptEditorMode"),
            DropdownRow("Editor", Editors, p.ExternalEditor, v => Edit(() => p.ExternalEditor = v), "ExternalEditor"),
            Caption("Visual Studio Code · JetBrains Rider · Visual Studio · Custom…"),
            exe,
            DropdownRow("Open .cs with", OpenWith, p.OpenCsExternally ? OpenWith[0] : OpenWith[1], v => Edit(() => p.OpenCsExternally = v == OpenWith[0]), "OpenCsWith"),
            scriptChecks);

        var builtIn = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,12,*,12,*") };
        var font = DropdownRow("Font", CodeFonts, p.CodeFont, v => Edit(() => p.CodeFont = v), "CodeFontFamily");
        var tab = TextRow("Tab size", p.TabSize.ToString(CultureInfo.InvariantCulture), v => Edit(() => p.TabSize = Number(v) ?? 0), "TabSize");
        var theme = DropdownRow("Theme", CodeThemes, p.CodeTheme, v => Edit(() => p.CodeTheme = v), "CodeTheme");
        foreach (var f in new[] { font, tab, theme }) f.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(tab, 2);
        Grid.SetColumn(theme, 4);
        builtIn.Children.AddRange([font, tab, theme]);
        var builtInChecks = UI.Row(20,
            Check("Format on save", p.FormatOnSave, v => Edit(() => p.FormatOnSave = v), "FormatOnSave"),
            Check("Autocomplete as you type", p.AutocompleteAsYouType, v => Edit(() => p.AutocompleteAsYouType = v), "Autocomplete"),
            Check("Underline analyzer warnings", p.UnderlineAnalyzerWarnings, v => Edit(() => p.UnderlineAnalyzerWarnings = v), "UnderlineWarnings"));
        var builtInTitle = Overline("Built-in code editor");
        builtInTitle.Margin = new Thickness(0, 2, 0, 0);
        return Column(12, Title("Editor"), TwoColumns(left, right, 300), builtInTitle, builtIn, builtInChecks);
    }

    // The Script Editor's settings are these preferences (ScriptEditorSettings is a view over them); the editor that is not
    // set in Paths is the one ExternalEditors.Detect finds for the Editor choice.
    ExternalEditorInfo? Detected(Preferences p) =>
        ScriptEditorSettings.KindOf(p.ExternalEditor) is var kind && kind != ExternalEditorKind.Custom ? ExternalEditorService.Of(_s).Detector.Detect(kind) : null;

    string ExecutableText(Preferences p)
    {
        if (Preferences.Expand(p.ScriptEditorPath) is { Length: > 0 } set) return IOPath.GetFileName(set) + " · set in Paths";
        return Detected(p) is { Found: true } d ? IOPath.GetFileName(d.Path!) + " · detected" : "not set · set in Paths";
    }

    /// <summary>Paths › Script editor status: the executable set there, else the chosen editor where it is installed.</summary>
    string ScriptEditorStatus(Preferences p)
    {
        if (_exists(Preferences.Expand(p.ScriptEditorPath))) return EditorName(p.ScriptEditorPath) + " found";
        return Detected(p)?.Status ?? EditorName(p.ScriptEditorPath) + " not found";
    }

    /// <summary>The editor an executable belongs to ("Code.exe" -> Visual Studio Code).</summary>
    public static string EditorName(string path) => IOPath.GetFileName(Preferences.Expand(path)).ToLowerInvariant() switch
    {
        "code.exe" => "Visual Studio Code",
        "rider64.exe" or "rider.exe" => "JetBrains Rider",
        "devenv.exe" => "Visual Studio",
        "" => "Script editor",
        var f => IOPath.GetFileNameWithoutExtension(f),
    };

    // ---- Paths (T-S08) -------------------------------------------------------------------------------

    Control PathsTab()
    {
        var p = Draft;
        var devkit = p.DevkitPro.Length > 0 ? p.DevkitPro : new AgbStudio.Build.Toolchain().DevkitPro;
        var col = Column(8, Title("Paths"),
            PathRow("devkitPro", devkit, DevkitHelp(), v => Edit(() => p.DevkitPro = v), folder: true, "DevkitPro"),
            PathRow("Projects folder", p.ProjectsFolder, null, v => Edit(() => p.ProjectsFolder = v), folder: true, "ProjectsFolder"),
            PathRow("Script editor", p.ScriptEditorPath, $"{ScriptEditorStatus(p)} · used when Script editor is External",
                v => Edit(() => p.ScriptEditorPath = v), folder: false, "ScriptEditorPath"),
            PathRow("Image editor", p.ImageEditorPath, "Opens .png / .aseprite on double-click", v => Edit(() => p.ImageEditorPath = v), folder: false, "ImageEditorPath"),
            PathRow("Tiled", p.TiledPath, "Optional · only needed to edit .tmx maps", v => Edit(() => p.TiledPath = v), folder: false, "TiledPath"),
            CacheCard());
        return col;
    }

    string DevkitHelp()
    {
        var tc = _s.Toolchain;
        if (tc.Tools.Count == 0) return "Checking the toolchain…";
        var arm = tc.Find("devkitARM") is { Found: true, Version: { } v } ? v : "devkitARM";
        string[] names = ["libtonc", "maxmod"];
        var missing = names.Where(n => tc.Find(n) is { Found: false }).ToList();
        if (tc.Find("devkitARM") is { Found: false }) missing.Insert(0, "devkitARM");
        return missing.Count == 0 ? $"{arm} · libtonc · maxmod found" : string.Join(" · ", missing) + " missing";
    }

    Control PathRow(string label, string value, string? help, Action<string> changed, bool folder, string name)
    {
        var field = SettingsUi.Text(label, value, changed, help, name);
        field.TemplateApplied += (_, e) => { if (e.NameScope.Find<TextBox>("PART_Box") is { } t) t.Watermark = "Not set"; };
        var ok = value.Trim().Length > 0 && _exists(Preferences.Expand(value));
        var mark = new ContentControl { Content = StatusIcon(ok), VerticalAlignment = VerticalAlignment.Center, Name = name + "_Status" };
        var browse = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "folder", Content = "Browse…", Name = name + "_Browse", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        browse.Click += async (_, _) =>
        {
            var picked = folder ? await _s.System.PickFolderAsync(label) : await _s.System.PickFileAsync(label, ("Programs", ["*.exe"]), ("All files", ["*"]));
            if (picked is null) return;
            field.Text = picked;
            mark.Content = StatusIcon(_exists(picked));
        };
        field.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty) mark.Content = StatusIcon((field.Text ?? "").Trim().Length > 0 && _exists(Preferences.Expand(field.Text ?? ""))); };
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,52,94") };
        Grid.SetColumn(mark, 1);
        Grid.SetColumn(browse, 2);
        g.Children.AddRange([field, mark, browse]);
        return g;
    }

    Control CacheCard()
    {
        var project = _s.Project.Current;
        var info = UI.Text(CacheText(project), "Mono/10", "text/muted");
        var text = UI.Column(2, UI.Text("Build cache", "Body/12 Semibold", "text/primary"), info);
        text.VerticalAlignment = VerticalAlignment.Center;
        var clear = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "x", Content = "Clear cache", Name = "ClearCache", VerticalAlignment = VerticalAlignment.Center, IsEnabled = project != null };
        clear.Click += (_, _) =>
        {
            if (project is null) return;
            ClearCache(project.BuildDir);
            info.Text = CacheText(project);
            _s.Toasts.Show("Build cache cleared · the next build converts every asset again");
        };
        var icon = new Icon { Glyph = "chip", Size = 16, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/primary");
        var row = new DockPanel { HorizontalSpacing = 14, Height = 54 };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(clear, Dock.Right);
        row.Children.AddRange([icon, clear, text]);
        var card = Card(row);
        card.Name = "BuildCacheCard";
        return card;
    }

    /// <summary>The asset cache and compiled objects of a project (build/cache, build/obj): what makes rebuilds fast.</summary>
    public static long CacheBytes(string buildDir) =>
        AssetCache.Size(IOPath.Combine(buildDir, "cache")) + AssetCache.Size(IOPath.Combine(buildDir, "obj"));

    public static void ClearCache(string buildDir)
    {
        AssetCache.Clear(buildDir);
        var obj = IOPath.Combine(buildDir, "obj");
        if (Directory.Exists(obj)) Directory.Delete(obj, true);
    }

    static string CacheText(AgbStudio.Core.Project.GameProject? p)
    {
        if (p is null) return "No project open";
        var mb = CacheBytes(p.BuildDir) / (1024.0 * 1024.0);
        var size = mb >= 10 ? $"{mb:0} MB" : mb >= 0.05 ? $"{mb:0.0} MB" : "empty";
        return $"{Abbreviate(IOPath.Combine(p.BuildDir, "cache"))} · {size}";
    }

    /// <summary>Writes the user's folders as %LOCALAPPDATA% / %USERPROFILE%, as Figma does.</summary>
    public static string Abbreviate(string path)
    {
        foreach (var v in new[] { "LOCALAPPDATA", "APPDATA", "USERPROFILE" })
            if (Environment.GetEnvironmentVariable(v) is { Length: > 0 } dir && path.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                return "%" + v + "%" + path[dir.Length..];
        return path;
    }

    // ---- Emulator (T-D09) ----------------------------------------------------------------------------

    static readonly IReadOnlyList<string> Cores = ["mGBA (built in)"];
    static readonly IReadOnlyList<string> ScaleOptions = ["1× · 240×160", "2× · 480×320", "3× · 720×480", "4× · 960×640"];
    static readonly IReadOnlyList<string> Filters = ["Pixel-perfect", "Smooth"];

    Control EmulatorTab()
    {
        var p = Draft;
        var audio = ToggleRow("Audio", p.EmulatorAudio, v => Edit(() => p.EmulatorAudio = v), "EmulatorAudio");
        var pause = SwitchRow("Pause when unfocused", p.PauseWhenUnfocused, v => Edit(() => p.PauseWhenUnfocused = v), "PauseWhenUnfocused");
        pause.Margin = new Thickness(0, -4);
        var fields = Column(8,
            DropdownRow("Core", Cores, p.EmulatorCore, v => Edit(() => p.EmulatorCore = v), "EmulatorCore"),
            DropdownRow("Scale", ScaleOptions, ScaleOptions[Math.Clamp(p.EmulatorScale, 1, 4) - 1], v => Edit(() => p.EmulatorScale = Number(v) ?? 3), "EmulatorScale"),
            DropdownRow("Filter", Filters, p.EmulatorFilter, v => Edit(() => p.EmulatorFilter = v), "EmulatorFilter"),
            audio, pause,
            TextRow("Fast-forward", $"{p.FastForwardKey} · {p.FastForwardSpeed}×", v => Edit(() => SetFastForward(v)), "FastForward"));
        fields.Width = 420;
        fields.HorizontalAlignment = HorizontalAlignment.Left;
        var change = new AgbButton { Kind = ButtonKind.Secondary, Content = "Change…", Name = "ChangeDevkitPro" };
        change.Click += async (_, _) =>
        {
            if (await _s.System.PickFolderAsync("devkitPro") is not { } dir) return;
            Edit(() => Draft.DevkitPro = dir);
            Select("T-S08");
        };
        return Column(12, Title("Emulator"), fields, Title("Toolchain"), ProjectSettingsDialog.ToolchainCard(_s, change, false));
    }

    /// <summary>"Tab · 4×" -> key Tab, speed 4.</summary>
    void SetFastForward(string text)
    {
        var parts = text.Split('·', 2, StringSplitOptions.TrimEntries);
        Draft.FastForwardKey = parts[0];
        Draft.FastForwardSpeed = parts.Length > 1 ? Number(parts[1]) ?? 0 : 0;
    }

    // ---- Shortcuts (T-S09) ---------------------------------------------------------------------------

    const string ShortcutColumns = "240,330,60";
    SearchField? _search;
    StackPanel? _shortcutRows;

    Control ShortcutsTab()
    {
        var reset = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "refresh", Content = "Reset to defaults", Name = "ResetShortcuts" };
        reset.Click += (_, _) => { Shortcuts.ResetToDefaults(); Changed(); FillShortcuts(); };
        var head = new DockPanel { Height = 26 };
        DockPanel.SetDock(reset, Dock.Right);
        head.Children.AddRange([reset, Title("Shortcuts")]);
        _search = new SearchField { Watermark = "Search commands or press a key…", Name = "ShortcutSearch" };
        _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) FillShortcuts(); };
        _search.AddHandler(KeyDownEvent, OnSearchKey, RoutingStrategies.Tunnel);
        _shortcutRows = new StackPanel { Width = 630, HorizontalAlignment = HorizontalAlignment.Left, Name = "ShortcutTable" };
        FillShortcuts();
        return Column(12, head, _search, _shortcutRows);
    }

    /// <summary>"or press a key": a key combination with Ctrl / Alt, or an F key, searches by that shortcut.</summary>
    void OnSearchKey(object? sender, KeyEventArgs e)
    {
        var mods = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift);
        var fkey = e.Key is >= Key.F1 and <= Key.F24;
        if ((mods & (KeyModifiers.Control | KeyModifiers.Alt)) == 0 && !fkey) return;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift) return;
        if (mods == KeyModifiers.Control && e.Key is Key.A or Key.C or Key.V or Key.X or Key.Z or Key.Y) return;
        _search!.Text = Fmt.Format(new KeyGesture(e.Key, mods));
        _search.CaretIndex = _search.Text.Length;
        e.Handled = true;
    }

    /// <summary>The rows shown now: the Figma rows, or every command whose name or shortcut matches the search.</summary>
    public IReadOnlyList<(string Label, string Command)> ShortcutRows()
    {
        var q = _search?.Text?.Trim() ?? "";
        static string Title(string t) => t.TrimEnd('…');
        if (q.Length == 0) return DefaultRows;
        return Shortcuts.Commands.All
            .Select(c => (Label: DefaultRows.FirstOrDefault(r => r.Command == c.Id).Label ?? Title(c.Title), c.Id))
            .Where(r => r.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (Shortcuts.GestureOf(r.Id) is { } g && string.Equals(Fmt.Format(g), q, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    void FillShortcuts()
    {
        if (_shortcutRows is null) return;
        _shortcutRows.Children.Clear();
        _shortcutRows.Children.Add(TableHeader(["Command", "Keys", ""], ShortcutColumns));
        foreach (var (label, id) in ShortcutRows())
        {
            var command = id;
            var keys = UI.Row(6);
            if (Shortcuts.GestureOf(id) is { } g)
                foreach (var part in KeyboardShortcutsDialog.KeyParts(Fmt.Format(g))) keys.Children.Add(new Keycap { Label = part, VerticalAlignment = VerticalAlignment.Center });
            foreach (var c in Shortcuts.ConflictsOf(id).Take(1))
                keys.Children.Add(new Chip { Style = ChipStyle.Warning, Glyph = "warn", Content = "conflicts with " + c.Command.Title.TrimEnd('…'), Margin = new Thickness(2, 0, 0, 0), IsHitTestVisible = false, Name = "Conflict_" + id });
            var edit = new IconButton { Glyph = "pencil", Name = "Edit_" + id, HorizontalAlignment = HorizontalAlignment.Center };
            edit.Click += async (_, _) => await Record(command, label, edit);
            var row = TableRow(ShortcutColumns, 30, Cell(label), keys, edit);
            edit.Margin = default;
            row.Name = "Shortcut_" + id;
            _shortcutRows.Children.Add(row);
        }
    }

    /// <summary>Pencil: Record Shortcut (T-O12); Replace binds the keys in the draft and removes them from conflicting commands.</summary>
    public async Task Record(string commandId, string label, Control? anchor = null)
    {
        var req = new RecordShortcutRequest(commandId, label, Shortcuts.GestureOf(commandId), g => Shortcuts.FindConflicts(commandId, g));
        if (await _s.Windows.Open("T-O12", req, anchor, source: "Preferences.Shortcuts") is KeyGesture g)
        {
            Shortcuts.Rebind(commandId, g, replace: true);
            Changed();
            FillShortcuts();
        }
    }
}
