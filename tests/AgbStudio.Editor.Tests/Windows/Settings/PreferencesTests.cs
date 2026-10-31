using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Fmt = AgbStudio.Editor.Theme.Shortcuts;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>P11.62: Preferences (T-D09, T-S06..T-S09) and Record Shortcut (T-O12) E2E.</summary>
public sealed class PreferencesTests : IDisposable
{
    readonly Fixture _fx = new();
    MainWindow? _win;

    public void Dispose()
    {
        _win?.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        _fx.Services.Theme?.SetAccent(ThemeService.DefaultAccent);
        _fx.Dispose();
    }

    MainWindow Shell()
    {
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
        return _win;
    }

    PreferencesDialog Open(MainWindow win)
    {
        _fx.Services.Commands.Execute("file.preferences");
        Capture.Pump();
        return Assert.IsType<PreferencesDialog>(win.Overlays.Open.Last());
    }

    /// <summary>
    /// P10.16 with fake folders (T-S08, T-P12): on first run (no devkitPro in Preferences) the DEVKITPRO variable the
    /// installer sets, in its MSYS form "/c/...", leads to a devkitPro folder whose arm-none-eabi-gcc reports 14.2.0 (a
    /// stub answers instead of the program). devkitARM is found with its version, shown in Preferences · Paths and the
    /// Launcher status line. The image editor's mark says found for a program that exists and not found otherwise.
    /// </summary>
    [AvaloniaFact]
    public void Paths_DevkitArmFoundWithItsVersion_OnFirstRun_AndTheImageEditorFoundOrNot()
    {
        var dkp = Path.GetFullPath(Path.Combine(_fx.Folder, "fake devkitPro"));
        foreach (var f in new[] { @"devkitARM\bin\arm-none-eabi-g++.exe", @"devkitARM\bin\arm-none-eabi-gcc.exe", @"libtonc\lib\libtonc.a", @"libgba\lib\libmm.a", @"tools\bin\gbafix.exe", @"tools\bin\mmutil.exe" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dkp, f))!);
            File.WriteAllText(Path.Combine(dkp, f), "");
        }
        var ran = new System.Collections.Concurrent.ConcurrentQueue<(string Exe, string Args)>();
        _fx.Services.Toolchain.RunTool = (exe, args) => { ran.Enqueue((exe, args)); return new AgbStudio.Build.ProcResult(0, "14.2.0\n"); };
        var image = Path.Combine(_fx.Folder, "tools", "Aseprite.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        File.WriteAllText(image, "");
        _fx.Services.Preferences.Update(p => { p.DevkitPro = ""; p.ImageEditorPath = image; });
        var old = Environment.GetEnvironmentVariable("DEVKITPRO");
        try
        {
            Environment.SetEnvironmentVariable("DEVKITPRO", "/" + char.ToLowerInvariant(dkp[0]) + dkp[2..].Replace('\\', '/'));
            var tools = _fx.Services.Toolchain.CheckNow();
            Assert.Equal(dkp, tools.Single(t => t.Name == "devkitPro").Path);
            var arm = tools.Single(t => t.Name == "devkitARM");
            Assert.Equal((true, "devkitARM 14.2"), (arm.Found, arm.Version));
            Assert.Equal((Path.Combine(dkp, "devkitARM", "bin", "arm-none-eabi-gcc.exe"), "-dumpversion"), Assert.Single(ran));
            Assert.All(new[] { "devkitPro", "libtonc", "maxmod", "gbafix", "mmutil" }, n => Assert.True(_fx.Services.Toolchain.Find(n)!.Found, n));
            Assert.EndsWith(" · devkitARM 14.2 found", AgbStudio.Editor.Windows.Launcher.LauncherService.Of(_fx.Services).StatusLine());

            // Preferences · Paths
            var win = Shell();
            var d = Open(win);
            win.Click(d.Find<NavItem>("Nav_T-S08"));
            var devkit = d.Find<TextField>("DevkitPro");
            Assert.Equal(dkp, devkit.Text);
            Assert.Equal("devkitARM 14.2 · libtonc · maxmod found", devkit.Help);
            Assert.Equal("check", ((Icon)d.Find<ContentControl>("DevkitPro_Status").Content!).Glyph);
            // the image editor: found, then a program that is not there, then found again
            Assert.Equal(image, d.Find<TextField>("ImageEditorPath").Text);
            Assert.Equal("check", ((Icon)d.Find<ContentControl>("ImageEditorPath_Status").Content!).Glyph);
            win.Type(d.Find<TextField>("ImageEditorPath").Box(), Path.Combine(_fx.Folder, "tools", "Photoshop.exe"));
            Assert.Equal("warn", ((Icon)d.Find<ContentControl>("ImageEditorPath_Status").Content!).Glyph);
            win.Type(d.Find<TextField>("ImageEditorPath").Box(), image);
            Assert.Equal("check", ((Icon)d.Find<ContentControl>("ImageEditorPath_Status").Content!).Glyph);
            win.Type(d.Find<TextField>("ImageEditorPath").Box(), "");
            Assert.Equal("warn", ((Icon)d.Find<ContentControl>("ImageEditorPath_Status").Content!).Glyph);
        }
        finally { Environment.SetEnvironmentVariable("DEVKITPRO", old); }
    }

    [AvaloniaFact]
    public void OpensAtEmulatorFromFileMenuShortcutAndStatusBar_TabsFromTheirIds()
    {
        _fx.OpenSkyHopper();
        var win = Shell();
        var d = Open(win);
        Assert.Equal("T-D09", d.SelectedTab);
        Assert.Equal(["Appearance", "Editor", "Paths", "Emulator", "Shortcuts"], d.NavItems.Select(n => n.Label));
        win.Key(PhysicalKey.Escape);
        win.Key(PhysicalKey.Comma, RawInputModifiers.Control);
        Assert.Equal("T-D09", Assert.IsType<PreferencesDialog>(win.Overlays.Open.Single()).SelectedTab);
        win.Key(PhysicalKey.Escape);
        win.Click(win.Find<StatusItem>("Status_Emulator"));
        Assert.Equal("T-D09", Assert.IsType<PreferencesDialog>(win.Overlays.Open.Single()).SelectedTab);
        win.Overlays.CloseTop();
        foreach (var id in new[] { "T-S06", "T-S07", "T-S08", "T-S09" })
        {
            _ = _fx.Services.Windows.Open(id, source: "test");
            Capture.Pump();
            Assert.Equal(id, Assert.IsType<PreferencesDialog>(win.Overlays.Open.Single()).SelectedTab);
            win.Overlays.CloseTop();
        }
    }

    [AvaloniaFact]
    public void EveryField_PersistsToPreferencesJson_AndAppliesOnSave()
    {
        _fx.OpenSkyHopper();
        var devkit = Directory.CreateDirectory(Path.Combine(_fx.Folder, "devkitPro")).FullName;
        var win = Shell();
        var d = Open(win);

        win.Click(d.Find<NavItem>("Nav_T-S06"));
        win.Click(d.Find<Button>("Theme_Light"));
        win.Click(d.Find<Button>("Accent_2"));
        win.Pick(d, "UiScale", "125%");
        win.Type(d.Find<InspectorField>("CodeFont").Box(), "14 px");
        win.Toggle(d, "ReduceMotion");
        win.Toggle(d, "PixelGrid");

        win.Click(d.Find<NavItem>("Nav_T-S07"));
        win.Pick(d, "Autosave", "every 5 min");
        win.Type(d.Find<InspectorField>("UndoHistory").Box(), "300 steps");
        win.Pick(d, "DefaultSnap", "8 px");
        win.Toggle(d, "OpenLastProject");
        win.Toggle(d, "CollisionOverlay");
        win.Toggle(d, "BudgetInInspector");
        win.Click(d.Find<Segment>("ScriptEditorMode_0"));
        win.Pick(d, "ExternalEditor", "JetBrains Rider");
        win.Pick(d, "OpenCsWith", "Built-in editor");
        win.Toggle(d, "GenerateCsproj");
        win.Toggle(d, "HotReloadExternal");
        win.Type(d.Find<InspectorField>("TabSize").Box(), "2");
        win.Pick(d, "CodeTheme", "Studio Light");
        win.Toggle(d, "FormatOnSave");
        win.Toggle(d, "Autocomplete");
        win.Toggle(d, "UnderlineWarnings");

        win.Click(d.Find<NavItem>("Nav_T-S08"));
        win.Type(d.Find<TextField>("DevkitPro").Box(), devkit);
        Assert.Equal("check", ((Icon)d.Find<ContentControl>("DevkitPro_Status").Content!).Glyph);
        win.Type(d.Find<TextField>("ProjectsFolder").Box(), @"D:\Games");
        win.Type(d.Find<TextField>("ScriptEditorPath").Box(), @"C:\Tools\rider64.exe");
        win.Type(d.Find<TextField>("ImageEditorPath").Box(), @"C:\Tools\Aseprite.exe");
        win.Type(d.Find<TextField>("TiledPath").Box(), @"C:\Tools\tiled.exe");
        Assert.Equal("warn", ((Icon)d.Find<ContentControl>("TiledPath_Status").Content!).Glyph);

        win.Click(d.Find<NavItem>("Nav_T-D09"));
        win.Pick(d, "EmulatorScale", "2× · 480×320");
        win.Pick(d, "EmulatorFilter", "Smooth");
        win.Click(d.Find<InspectorField>("EmulatorAudio").GetVisualDescendants().OfType<AgbSwitch>().First());
        win.Click(d.Find<AgbSwitch>("PauseWhenUnfocused"));
        win.Type(d.Find<InspectorField>("FastForward").Box(), "Space · 8×");

        Assert.Null(d.Error);
        // nothing applies before Save
        Assert.Equal(ThemeMode.Dark, _fx.Services.Preferences.Current.Theme);
        win.Click(d.SaveButton());
        Assert.False(win.Overlays.IsOpen);

        var app = Application.Current!;
        Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);
        Assert.Equal(Color.Parse("#3ECF8E"), ((ISolidColorBrush)ThemeService.Token(win, "accent/default")!).Color);
        Assert.Equal(1.25, _fx.Services.Theme!.Scale);
        Assert.Equal(TimeSpan.FromMinutes(5), _fx.Timer.Interval);
        Assert.Equal(devkit, _fx.Services.Toolchain.CheckNow().First(t => t.Name == "devkitPro").Path);

        var p = new PreferencesStore(_fx.Services.Preferences.FilePath).Current;
        Assert.Equal((ThemeMode.Light, "#3ECF8E", 125, 14, true, false), (p.Theme, p.Accent, p.UiScalePercent, p.CodeFontSize, p.ReduceMotion, p.ShowPixelGrid));
        Assert.Equal((5, 300, 8, false, false, false), (p.AutosaveMinutes, p.UndoHistory, p.DefaultSnap, p.OpenLastProjectOnStart, p.ShowCollisionOverlay, p.ShowBudgetInInspector));
        Assert.Equal((ScriptEditorMode.BuiltIn, "JetBrains Rider", false, false, false), (p.ScriptEditor, p.ExternalEditor, p.OpenCsExternally, p.GenerateCsproj, p.HotReloadOnExternalSave));
        Assert.Equal(("JetBrains Mono", 2, "Studio Light", false, false, false), (p.CodeFont, p.TabSize, p.CodeTheme, p.FormatOnSave, p.AutocompleteAsYouType, p.UnderlineAnalyzerWarnings));
        Assert.Equal((devkit, @"D:\Games", @"C:\Tools\rider64.exe", @"C:\Tools\Aseprite.exe", @"C:\Tools\tiled.exe"), (p.DevkitPro, p.ProjectsFolder, p.ScriptEditorPath, p.ImageEditorPath, p.TiledPath));
        Assert.Equal(("mGBA (built in)", 2, "Smooth", false, true, "Space", 8), (p.EmulatorCore, p.EmulatorScale, p.EmulatorFilter, p.EmulatorAudio, p.PauseWhenUnfocused, p.FastForwardKey, p.FastForwardSpeed));

        // the Editor tab names the executable set in Paths
        d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S07"));
        Assert.Equal("rider64.exe · set in Paths", d.Find<InspectorField>("Executable").Value);
    }

    [AvaloniaFact]
    public void Cancel_LeavesPreferencesAsTheyWere_AndInvalidValuesBlockSave()
    {
        _fx.OpenSkyHopper();
        var win = Shell();
        var d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S06"));
        win.Click(d.Find<Button>("Theme_Light"));
        win.Click(d.CancelButton());
        Assert.Equal(ThemeMode.Dark, _fx.Services.Preferences.Current.Theme);
        Assert.False(File.Exists(_fx.Services.Preferences.FilePath));
        d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S07"));
        win.Type(d.Find<InspectorField>("TabSize").Box(), "0");
        Assert.Equal("Tab size must be 1 to 16.", d.Error);
        Assert.False(d.SaveButton().IsEnabled);
    }

    [AvaloniaFact]
    public void Paths_ClearCacheEmptiesTheProjectsBuildCache()
    {
        _fx.OpenSkyHopper();
        var build = _fx.Services.Project.Current!.BuildDir;
        Directory.CreateDirectory(Path.Combine(build, "cache"));
        Directory.CreateDirectory(Path.Combine(build, "obj"));
        File.WriteAllBytes(Path.Combine(build, "cache", "manifest.json"), new byte[200_000]);
        File.WriteAllBytes(Path.Combine(build, "obj", "game.o"), new byte[100_000]);
        Assert.Equal(300_000, PreferencesDialog.CacheBytes(build));
        var win = Shell();
        var d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S08"));
        var card = d.Find<Border>("BuildCacheCard");
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.EndsWith("· 0.3 MB") == true);
        win.Click(d.Find<AgbButton>("ClearCache"));
        Assert.False(Directory.Exists(Path.Combine(build, "cache")));
        Assert.False(Directory.Exists(Path.Combine(build, "obj")));
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.EndsWith("· empty") == true);
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text.StartsWith("Build cache cleared"));
    }

    [AvaloniaFact]
    public void Shortcuts_SearchPressAKeyRecordWithConflictReplaceSaveAndReset()
    {
        _fx.OpenSkyHopper();
        var win = Shell();
        var d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S09"));
        Assert.Equal(PreferencesDialog.DefaultRows, d.ShortcutRows());

        var search = d.Find<SearchField>("ShortcutSearch");
        win.Type(search, "grid");
        Assert.Equal(["Toggle grid"], d.ShortcutRows().Select(r => r.Label));
        // "or press a key"
        search.Text = "";
        search.Focus();
        win.Key(PhysicalKey.K, RawInputModifiers.Control);
        Assert.Equal("Ctrl+K", search.Text);
        Assert.Equal(["Command palette"], d.ShortcutRows().Select(r => r.Label));
        search.Text = "";
        Capture.Pump();

        // pencil -> Record Shortcut -> Ctrl+G conflicts with Go to line -> Replace
        win.Click(d.Find<IconButton>("Edit_scene.toggleGrid"));
        var rec = Assert.IsType<RecordShortcutPopover>(win.Overlays.Open[^1]);
        Assert.Equal(("T-O12", "Preferences.Shortcuts"), _fx.Services.Windows.Requests[^1]);
        win.Key(PhysicalKey.G, RawInputModifiers.Control);
        Assert.Equal("Ctrl+G", Fmt.Format(rec.Recorded!));
        Assert.True(rec.ShowsConflict);
        Assert.Equal("Ctrl+G is used by “Go to line”.", rec.ConflictMessage);
        Assert.Equal(["Ctrl", "G"], rec.Find<StackPanel>("RecordedKeys").Children.OfType<Keycap>().Select(k => k.Label));
        win.Click(rec.Find<AgbButton>("Replace"));
        Assert.Same(d, win.Overlays.Open.Single());
        Assert.Equal("Ctrl+G", Fmt.Format(d.Shortcuts.GestureOf("scene.toggleGrid")!));
        Assert.Null(d.Shortcuts.GestureOf("script.goToLine"));
        Assert.Equal(["Ctrl", "G"], d.Find<Border>("Shortcut_scene.toggleGrid").GetVisualDescendants().OfType<Keycap>().Select(k => k.Label));
        // the draft is not applied yet
        Assert.Equal("G", Fmt.Format(_fx.Services.Shortcuts.GestureOf("scene.toggleGrid")!));

        // Record, then Cancel: nothing changes
        win.Click(d.Find<IconButton>("Edit_build.play"));
        rec = Assert.IsType<RecordShortcutPopover>(win.Overlays.Open[^1]);
        Assert.False(rec.Find<AgbButton>("Replace").IsEnabled);
        win.Key(PhysicalKey.F6);
        Assert.False(rec.ShowsConflict);
        win.Click(rec.Find<AgbButton>("Cancel"));
        Assert.Equal("F5", Fmt.Format(d.Shortcuts.GestureOf("build.play")!));

        var changed = 0;
        _fx.Services.Shortcuts.Changed += () => changed++;
        win.Click(d.SaveButton());
        Assert.True(changed > 0);
        Assert.Equal("Ctrl+G", Fmt.Format(_fx.Services.Shortcuts.GestureOf("scene.toggleGrid")!));
        Assert.Null(_fx.Services.Shortcuts.GestureOf("script.goToLine"));
        var stored = new PreferencesStore(_fx.Services.Preferences.FilePath).Current.Shortcuts;
        Assert.Equal("Ctrl+G", stored["scene.toggleGrid"]);
        Assert.Equal("", stored["script.goToLine"]);
        // the title bar menus and the shortcut keys use the new binding
        Assert.Equal("scene.toggleGrid", _fx.Services.Shortcuts.Resolve(Key.G, KeyModifiers.Control, ShortcutContext.Scene)!.Id);

        d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S09"));
        win.Click(d.Find<AgbButton>("ResetShortcuts"));
        win.Click(d.SaveButton());
        Assert.Equal("G", Fmt.Format(_fx.Services.Shortcuts.GestureOf("scene.toggleGrid")!));
        Assert.Equal("Ctrl+G", Fmt.Format(_fx.Services.Shortcuts.GestureOf("script.goToLine")!));
        Assert.Empty(new PreferencesStore(_fx.Services.Preferences.FilePath).Current.Shortcuts);
    }

    [AvaloniaFact]
    public void Shortcuts_ShowTheConflictChip()
    {
        _fx.Services.Preferences.Current.Shortcuts["script.goToLine"] = "G";
        var win = Shell();
        var d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S09"));
        Assert.Equal("conflicts with Go to line", d.Find<Chip>("Conflict_scene.toggleGrid").Content);
        Assert.Equal(ChipStyle.Warning, d.Find<Chip>("Conflict_scene.toggleGrid").Style);
    }

    [AvaloniaFact]
    public void Appearance_PreviewsTheThemesInTheirOwnTokens()
    {
        var win = Shell();
        var d = Open(win);
        win.Click(d.Find<NavItem>("Nav_T-S06"));
        var light = d.Find<Button>("Theme_Light").GetVisualDescendants().OfType<ThemeVariantScope>().First(s => s.RequestedThemeVariant == ThemeVariant.Light);
        var panel = light.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().Skip(1).First();
        Assert.Equal(Colors.White, ((ISolidColorBrush)panel.Fill!).Color);
        Assert.Equal("check", d.Find<Button>("Theme_Dark").GetVisualDescendants().OfType<Icon>().First().Glyph);
        win.Click(d.Find<Button>("Theme_System"));
        Assert.Equal(ThemeMode.System, d.Draft.Theme);
        Assert.Equal("check", d.Find<Button>("Theme_System").GetVisualDescendants().OfType<Icon>().First().Glyph);
        Assert.Equal(SwatchState.Selected, d.Find<Button>("Accent_0").GetVisualDescendants().OfType<Swatch>().First().State);
    }
}
