using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>P11.61: Project Settings (T-D08, T-S01..T-S05) E2E: opening, every field persisting to the .agbproj, validation, and the build reading it.</summary>
public sealed class ProjectSettingsTests : IDisposable
{
    readonly Fixture _fx = new();
    MainWindow? _win;

    public void Dispose()
    {
        _win?.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        _fx.Dispose();
    }

    MainWindow Shell()
    {
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
        return _win;
    }

    ProjectSettingsDialog OpenDialog(MainWindow win)
    {
        _fx.Services.Commands.Execute("file.projectSettings");
        Capture.Pump();
        return Assert.IsType<ProjectSettingsDialog>(win.Overlays.Open.Last());
    }

    [AvaloniaFact]
    public void OpensFromFileMenuAtInput_TabsSwitch_AndEachTabOpensFromItsWindowId()
    {
        _fx.OpenFigmaSkyHopper();
        var win = Shell();
        var d = OpenDialog(win);
        Assert.Equal((900, 620), ((int)d.Bounds.Width, (int)d.Bounds.Height));
        Assert.Equal("T-D08", d.SelectedTab);
        Assert.Equal(["General", "Input", "Save data", "Build", "Scripting", "Localization"], d.NavItems.Select(n => n.Label));
        Assert.True(d.NavItems[1].IsSelected);
        win.Click(d.NavItems[3]);
        Assert.Equal("T-S03", d.SelectedTab);
        Assert.True(d.NavItems[3].IsSelected);
        win.Key(PhysicalKey.Escape);
        Assert.False(win.Overlays.IsOpen);
        foreach (var id in new[] { "T-S01", "T-S02", "T-S03", "T-S04", "T-S05", "T-D08" })
        {
            _ = _fx.Services.Windows.Open(id, source: "test");
            Capture.Pump();
            Assert.Equal(id, Assert.IsType<ProjectSettingsDialog>(win.Overlays.Open.Single()).SelectedTab);
            win.Overlays.CloseTop();
        }
    }

    [AvaloniaFact]
    public void EveryField_PersistsToTheAgbproj()
    {
        var p = _fx.OpenFigmaSkyHopper();
        var win = Shell();
        var d = OpenDialog(win);

        // General
        win.Click(d.Find<NavItem>("Nav_T-S01"));
        win.Type(d.Find<TextField>("GameTitle").Box(), "Sky Hopper DX");
        win.Type(d.Find<TextField>("Version").Box(), "1.2.4");
        win.Type(d.Find<TextField>("RomTitle").Box(), "skyhopperdx");
        win.Type(d.Find<TextField>("GameCode").Box(), "SKYD");
        win.Type(d.Find<TextField>("Maker").Box(), "AG");
        Assert.Equal("SKYHOPPERDX", d.Find<TextField>("RomTitle").Text);
        win.Pick(d, "StartScene", "Forest_01");
        win.Pick(d, "DefaultFont", "pixel8");
        win.Pick(d, "DefaultLanguage", "English");
        win.Pick(d, "BootSplash", "AGB Studio logo · 3 s");
        win.Toggle(d, "SkipSplash");

        // Input
        win.Click(d.Find<NavItem>("Nav_T-D08"));
        win.Type(d.Find<TextBox>("Action_A"), "Hop");
        win.Click(d.Find<Button>("Key_B"));
        Assert.Equal("…", d.Find<Button>("Key_B").GetVisualDescendants().OfType<Keycap>().First().Label);
        win.Key(PhysicalKey.C);
        Assert.Equal("C", d.Find<Button>("Key_B").GetVisualDescendants().OfType<Keycap>().First().Label);
        win.Pick(d, "Pad_A", "East");
        win.Toggle(d, "AllowRemap");

        // Save data
        win.Click(d.Find<NavItem>("Nav_T-S02"));
        win.Click(d.Find<Segment>("SaveType_2"));
        win.Click(d.Find<AgbButton>("AddField"));
        win.Type(d.Find<TextBox>("SaveField_6"), "gems");
        win.Type(d.Find<InspectorField>("Slots").Box(), "4");
        win.Click(d.Find<InspectorField>("Autosave").GetVisualDescendants().OfType<AgbSwitch>().First());
        win.Toggle(d, "Checksum");

        // Build
        win.Click(d.Find<NavItem>("Nav_T-S03"));
        win.Click(d.Find<Segment>("Configuration_0"));
        win.Pick(d, "Optimise", "-Os · size");
        win.Type(d.Find<InspectorField>("Output").Box(), "out/");
        win.Type(d.Find<InspectorField>("WarnRom").Box(), "8 MB");
        win.Toggle(d, "Compress");
        win.Toggle(d, "StripLogs");
        win.Toggle(d, "KeepAsserts");
        win.Toggle(d, "RunAfterBuild");

        // Scripting
        win.Click(d.Find<NavItem>("Nav_T-S04"));
        win.Type(d.Find<InspectorField>("Generated").Box(), "build/cs");
        win.Type(d.Find<InspectorField>("Namespace").Box(), "Sky.Hopper");
        win.Toggle(d, "HotReload");
        win.Toggle(d, "WarningsAsErrors");
        win.Toggle(d, "InlineHints");
        win.Pick(d, "Severity_GAE002", "Error");
        win.Pick(d, "Severity_GAE005", "None");

        // Localization
        win.Click(d.Find<NavItem>("Nav_T-S05"));
        win.Pick(d, "Font_th", "title_big");
        win.Pick(d, "Source", "ไทย");
        win.Pick(d, "Fallback", "ไทย");
        win.Pick(d, "ThaiWrap", "Any character");
        win.Toggle(d, "ShowMissingKeys");
        win.Toggle(d, "FailOnMissing");

        Assert.Null(d.Error);
        win.Click(d.SaveButton());
        Assert.False(win.Overlays.IsOpen);

        var s = GameProject.Open(p.ProjectPath).Settings;
        var g = s.General;
        Assert.Equal(("Sky Hopper DX", "1.2.4", "SKYHOPPERDX", "SKYD", "AG"), (g.GameTitle, g.Version, g.RomTitle, g.GameCode, g.Maker));
        Assert.Equal(("Forest_01", "pixel8", "en", true, 3, false), (g.StartScene, g.DefaultFont, g.DefaultLanguage, g.BootSplash, g.SplashSeconds, g.SkipSplashInDebug));
        Assert.Equal("Hop", s.Input.First(a => a.Button == "A").Action);
        Assert.Equal("C", s.Input.First(a => a.Button == "B").Keyboard);
        Assert.Equal("East", s.Input.First(a => a.Button == "A").Gamepad);
        Assert.False(s.AllowRemap);
        Assert.Equal(SaveType.Flash64K, s.Save.Type);
        Assert.Equal(("gems", "int"), (s.Save.Fields[^1].Name, s.Save.Fields[^1].Type));
        Assert.Equal((4, false, false), (s.Save.Slots, s.Save.Autosave, s.Save.Checksum));
        Assert.Equal((BuildConfiguration.Debug, "-Os", "out/", 8), (s.Build.Configuration, s.Build.Optimise, s.Build.Output, s.Build.WarnRomMegabytes));
        Assert.Equal((false, false, true, false), (s.Build.CompressGraphics, s.Build.StripDebugLog, s.Build.KeepAsserts, s.Build.RunAfterBuild));
        Assert.Equal(("build/cs", "Sky.Hopper", false, true, false), (s.Scripting.Generated, s.Scripting.Namespace, s.Scripting.HotReload, s.Scripting.TreatWarningsAsErrors, s.Scripting.InlineHints));
        Assert.Equal(("Error", "None"), (s.Scripting.AnalyzerRules["GAE002"], s.Scripting.AnalyzerRules["GAE005"]));
        var loc = s.Localization;
        Assert.Equal("title_big", loc.Languages.First(l => l.Code == "th").Font);
        Assert.Equal("th", loc.Source);
        Assert.Equal("th", loc.Languages.First(l => l.Code == "en").Fallback);
        Assert.Equal((false, false, true), (loc.ThaiWrap, loc.ShowMissingKeys, loc.FailOnMissing));
        // the Build tab's Save type is the same setting
        d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S03"));
        Assert.Equal("Flash · 64 KB", Dropdown.Of(d.Find<InspectorField>("BuildSaveType"))!.Value);
    }

    [AvaloniaFact]
    public void CancelEscAndClose_DropTheDraft()
    {
        var p = _fx.OpenFigmaSkyHopper();
        p.SaveSettings();
        var before = File.ReadAllText(p.ProjectPath);
        var win = Shell();
        var d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S01"));
        win.Type(d.Find<TextField>("GameTitle").Box(), "Changed");
        win.Click(d.CancelButton());
        Assert.False(win.Overlays.IsOpen);
        d = OpenDialog(win);
        win.Toggle(d, "AllowRemap");
        win.Key(PhysicalKey.Escape);
        Assert.False(win.Overlays.IsOpen);
        Assert.Equal(before, File.ReadAllText(p.ProjectPath));
        Assert.Equal("Sky Hopper", _fx.Services.ProjectSettings.Settings.General.GameTitle);
    }

    [AvaloniaFact]
    public void InvalidValues_DisableSaveAndSayWhy()
    {
        _fx.OpenFigmaSkyHopper();
        var win = Shell();
        var d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S01"));
        Assert.True(d.SaveButton().IsEnabled);
        win.Type(d.Find<TextField>("Version").Box(), "1.0");
        Assert.Equal("Version must be major.minor.patch, like 1.0.3.", d.Error);
        Assert.Equal(d.Error, d.Footer.Hint);
        Assert.False(d.SaveButton().IsEnabled);
        Assert.False(d.Save());
        win.Type(d.Find<TextField>("Version").Box(), "1.0.4");
        Assert.True(d.SaveButton().IsEnabled);
        // header fields keep to the cartridge header: upper case, 12 / 4 / 2 characters
        var rom = d.Find<TextField>("RomTitle").Box();
        win.Type(rom, "averylongromtitle");
        Assert.Equal(12, rom.Text!.Length);
        Assert.Equal(rom.Text, rom.Text.ToUpperInvariant());
        win.Type(d.Find<TextField>("GameCode").Box(), "AB");
        Assert.Equal("Game code must be 4 capital letters or digits.", d.Error);
        win.Type(d.Find<TextField>("GameCode").Box(), "ABCD");
        win.Click(d.Find<NavItem>("Nav_T-S02"));
        win.Type(d.Find<TextBox>("SaveField_0"), "two words");
        Assert.Contains("must be one word", d.Error);
        win.Type(d.Find<TextBox>("SaveField_0"), "score");
        win.Type(d.Find<InspectorField>("Slots").Box(), "900");
        Assert.Equal("Slots must be 1 to 16.", d.Error);
    }

    [AvaloniaFact]
    public void Tabs_ShowTheProjectsData()
    {
        _fx.OpenFigmaSkyHopper();
        _fx.Services.Toolchain.CheckNow();
        var win = Shell();
        var d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S05"));
        Assert.Equal(["340 / 340", "326 / 340", "0 / 340"], new[] { "en", "th", "ja" }.Select(c => d.Find<Meter>("Lang_" + c).ValueText));
        Assert.Equal(ProgressStatus.Warn, d.Find<Meter>("Lang_th").Status);
        win.Click(d.Find<NavItem>("Nav_T-S02"));
        Assert.Equal(("3 slots", "189 B / 32 KB"), (d.Find<Meter>("SlotsMeter").Label, d.Find<Meter>("SlotsMeter").ValueText));
        Assert.Contains(d.Page!.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "63 bytes per slot");
        // T-S02: the size of each field follows its type, as Figma lists them (score 4 B, lives 1 B, coins 2 B ...)
        Assert.Equal(["4 B", "1 B", "2 B", "16 B", "32 B", "8 B"], Enumerable.Range(0, 6).Select(i => d.Find<TableCell>("SaveFieldSize_" + i).Text));
        win.Type(d.Find<TextBox>("SaveFieldType_1"), "int");
        Assert.Equal("4 B", d.Find<TableCell>("SaveFieldSize_1").Text);
        Assert.Contains(d.Page!.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "66 bytes per slot");
        win.Type(d.Find<TextBox>("SaveFieldType_1"), "short");
        Assert.Equal("2 B", d.Find<TableCell>("SaveFieldSize_1").Text);
        win.Type(d.Find<TextBox>("SaveFieldType_1"), "byte");
        Assert.Equal("1 B", d.Find<TableCell>("SaveFieldSize_1").Text);
        Assert.Contains(d.Page!.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "63 bytes per slot");
        win.Click(d.Find<Segment>("SaveType_4"));
        Assert.Equal("189 B / 8 KB", d.Find<Meter>("SlotsMeter").ValueText);
        win.Click(d.Find<NavItem>("Nav_T-S01"));
        Assert.Equal(SkyHopper.SceneNames.Order(), Dropdown.Of(d.Find<SelectField>("StartScene"))!.Options.Order());
        Assert.Equal(["pixel8", "thai_vwf", "title_big"], Dropdown.Of(d.Find<SelectField>("DefaultFont"))!.Options);
        Assert.Equal("ไทย", d.Find<SelectField>("DefaultLanguage").Selected);
        win.Click(d.Find<NavItem>("Nav_T-S03"));
        var card = d.Find<Border>("ToolchainCard").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        var devkit = _fx.Services.Toolchain.Find("devkitPro")!;
        Assert.Contains(card, t => t != null && t.EndsWith(devkit.Path));
        Assert.Contains(card, t => t != null && (t.EndsWith("found") || t.EndsWith("missing")));
    }

    [AvaloniaFact]
    public void AddLanguageOpensAddLanguage_AndChangeInPreferencesOpensPaths()
    {
        _fx.OpenFigmaSkyHopper();
        var win = Shell();
        var d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S05"));
        win.Click(d.Find<AgbButton>("AddLanguage"));
        Assert.Equal(("T-D20", "ProjectSettings.Localization"), _fx.Services.Windows.Requests[^1]);
        // Add Language (another group's dialog) saves the language into the project; Project Settings shows it when it closes
        _fx.Services.ProjectSettings.Settings.Localization.Languages.Add(new LanguageInfo { Code = "fr", Name = "Français" });
        win.Overlays.CloseTop();
        Capture.Pump();
        Assert.Same(d, win.Overlays.Open.Single());
        Assert.Equal("0 / 340", d.Find<Meter>("Lang_fr").ValueText);
        win.Click(d.Find<NavItem>("Nav_T-S03"));
        win.Click(d.Find<AgbButton>("ChangeInPreferences"));
        Assert.Equal("T-S08", Assert.IsType<PreferencesDialog>(win.Overlays.Open[^1]).SelectedTab);
    }

    /// <summary>The settings reach the ROM: header (title, code, maker, version), save chip id, start scene, boot splash and button remap.</summary>
    [AvaloniaFact]
    public async Task Build_ReadsTheSettings_RomHeaderSaveTypeStartSceneSplashRemap()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(_fx.Folder, "mini"));
        SampleText.AddTo(p);
        _fx.Services.Project.Open(p.ProjectPath);
        var win = Shell();
        var d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S01"));
        win.Type(d.Find<TextField>("RomTitle").Box(), "SETTINGSTEST");
        win.Type(d.Find<TextField>("GameCode").Box(), "ASTX");
        win.Type(d.Find<TextField>("Maker").Box(), "ZZ");
        win.Type(d.Find<TextField>("Version").Box(), "1.0.7");
        win.Pick(d, "StartScene", "Forest_02");
        win.Pick(d, "BootSplash", "None");
        win.Click(d.Find<NavItem>("Nav_T-D08"));
        win.Toggle(d, "AllowRemap");
        win.Click(d.Find<NavItem>("Nav_T-S02"));
        win.Click(d.Find<Segment>("SaveType_2"));
        win.Click(d.SaveButton());

        var r = await _fx.Services.Build.BuildAsync();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var rom = File.ReadAllBytes(r.RomPath);
        string Ascii(int at, int n) => System.Text.Encoding.ASCII.GetString(rom, at, n);
        Assert.Equal(("SETTINGSTEST", "ASTX", "ZZ"), (Ascii(0xA0, 12), Ascii(0xAC, 4), Ascii(0xB0, 2)));
        Assert.Equal(7, rom[0xBC]);
        Assert.True(rom.AsSpan().IndexOf("FLASH_V126"u8) > 0);
        Assert.True(rom.AsSpan().IndexOf("SRAM_V113"u8) < 0);
        var gen = Path.Combine(p.BuildDir, "gen");
        var game = File.ReadAllText(Path.Combine(gen, "game.cpp"));
        Assert.Contains("kScenes, 2, 1,", game);   // Forest_02 is scene 1
        Assert.DoesNotContain("&agb_splash", game);
        Assert.False(File.Exists(Path.Combine(gen, "assets", "splash.cpp")));
        Assert.EndsWith("true};", File.ReadAllText(Path.Combine(gen, "ui.cpp")).TrimEnd());

        // and back: splash on, remap off, SRAM
        d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S01"));
        win.Pick(d, "BootSplash", "AGB Studio logo · 2 s");
        win.Click(d.Find<NavItem>("Nav_T-D08"));
        win.Toggle(d, "AllowRemap");
        win.Click(d.Find<NavItem>("Nav_T-S02"));
        win.Click(d.Find<Segment>("SaveType_1"));
        win.Click(d.SaveButton());
        r = await _fx.Services.Build.BuildAsync();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        rom = File.ReadAllBytes(r.RomPath);
        Assert.True(rom.AsSpan().IndexOf("SRAM_V113"u8) > 0);
        Assert.Contains("&agb_splash", File.ReadAllText(Path.Combine(gen, "game.cpp")));
        Assert.EndsWith("false};", File.ReadAllText(Path.Combine(gen, "ui.cpp")).TrimEnd());
    }

    /// <summary>"Treat warnings as errors" turns the GAE002 float warning into an error: the C# step of the build fails.</summary>
    [AvaloniaFact]
    public async Task TreatWarningsAsErrors_FailsTheScriptStepOnAWarning()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(_fx.Folder, "mini_gae"));
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Floaty.cs"), "using GBA;\n\npublic class Floaty : Behaviour\n{\n    float drift = 0.5f;\n}\n");
        _fx.Services.Project.Open(p.ProjectPath);
        var project = _fx.Services.Project.Current!;
        ScriptInput Input() => new() { OutputDir = Path.Combine(project.BuildDir, "gen", "scripts") };
        var off = _fx.Services.Build.Scripts.Compile(project, Input(), new NullObserver());
        Assert.Contains(off.Diagnostics, x => x.Code == "GAE002" && x.Severity == Severity.Warning);
        Assert.DoesNotContain(off.Diagnostics, x => x.Severity == Severity.Error);

        var win = Shell();
        var d = OpenDialog(win);
        win.Click(d.Find<NavItem>("Nav_T-S04"));
        win.Toggle(d, "WarningsAsErrors");
        win.Click(d.SaveButton());
        var on = _fx.Services.Build.Scripts.Compile(project, Input(), new NullObserver());
        Assert.Contains(on.Diagnostics, x => x.Code == "GAE002" && x.Severity == Severity.Error);
        var r = await _fx.Services.Build.BuildAsync();
        Assert.False(r.Success);
        Assert.Equal(GameBuilder.StepScripts, r.FailedStep);
    }
}
