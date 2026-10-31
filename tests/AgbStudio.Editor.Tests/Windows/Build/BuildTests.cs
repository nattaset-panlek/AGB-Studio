using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Build;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Build;

/// <summary>Build ROM and Build ROM · Failed bound to the build stream (P11.57).</summary>
public class BuildRomTests
{
    [AvaloniaFact]
    public void Stream_FromTheBuildThread_UpdatesStepsProgressAndLog()
    {
        using var fx = new Fixture();
        GroupFixture.SkyHopper(fx);
        var d = new BuildRomDialog(fx.Services, start: false);
        // GameBuilder calls the observer on its worker thread
        var t = Task.Run(() => GroupShots.DriveRunning(d.Stream));
        GroupFixture.PumpUntil(() => t.IsCompleted && d.Stream.Step(GameBuilder.StepCompile).Done == 9, 5000);
        var s = d.Stream;
        Assert.Equal(BuildStream.UiOrder, s.Steps.Select(x => x.Title));
        Assert.Equal((StepState.Done, "12 scripts · 0.21 s"), (s.Step(GameBuilder.StepScripts).State, s.Step(GameBuilder.StepScripts).Detail));
        Assert.Equal("8 sprites · 3 tilesets · 2 fonts · 9 sounds · 0.18 s", s.Step(GameBuilder.StepAssets).Detail);
        Assert.Equal((StepState.Running, "PlayerController.cpp · 9 / 12"), (s.Step(GameBuilder.StepCompile).State, s.Step(GameBuilder.StepCompile).Detail));
        Assert.Equal((StepState.Pending, "—"), (s.Step(GameBuilder.StepLink).State, s.Step(GameBuilder.StepLink).Detail));
        Assert.Equal(["g++ -mthumb -mcpu=arm7tdmi -O2 PlayerController.cpp", "GAE002 PlayerController.cs(8): float → use fixed", "g++ … Slime.cpp"], s.Log.Select(l => l.Text));
        Assert.Equal(ConsoleLevel.Warning, s.Log[1].Level);
        Capture.Host(d, 600, 440).Close();
        var progress = d.GetVisualDescendants().OfType<AgbProgress>().Single(p => p.Name == "StepProgress");
        Assert.Equal((9.0, 12.0), (progress.Value, progress.Maximum));
        Assert.Equal(3, d.GetVisualDescendants().OfType<ConsoleLine>().Count());
        Assert.Equal("Building skyhopper.gba…", d.Title);
        // GameBuilder hands the compile step over to Link + gbafix when ninja starts linking; the log shows the lines
        s.StepFinished(GameBuilder.StepCompile, TimeSpan.FromSeconds(0.34), true, "12 files");
        Assert.Equal((StepState.Done, "12 files · 0.34 s"), (s.Step(GameBuilder.StepCompile).State, s.Step(GameBuilder.StepCompile).Detail));
        Assert.Equal(StepState.Pending, s.Step(GameBuilder.StepLink).State);
        s.StepStarted(GameBuilder.StepLink, "");
        s.Line(@"[13/14] LINK D:\x\build\obj\skyhopper.elf");
        Assert.Equal(StepState.Running, s.Step(GameBuilder.StepLink).State);
        Assert.Equal("ld … skyhopper.elf", s.Log[^1].Text);
        s.StepFinished(GameBuilder.StepLink, TimeSpan.FromSeconds(0.05), true, "skyhopper.gba");
        Assert.Equal((StepState.Done, "skyhopper.gba · 0.05 s"), (s.Step(GameBuilder.StepLink).State, s.Step(GameBuilder.StepLink).Detail));
    }

    [AvaloniaFact]
    public void Failure_ShowsTheFailedStepItsPredecessorSkippedStepsAndErrors()
    {
        using var fx = new Fixture();
        GroupFixture.SkyHopper(fx);
        var failure = GroupShots.Failed(fx);
        var d = new BuildFailedDialog(fx.Services, failure);
        Assert.Equal("Build failed · 2 errors", d.Title);
        Assert.Equal([GameBuilder.StepScripts, GameBuilder.StepCompile, GameBuilder.StepLink], d.ShownSteps.Select(s => s.Title));
        Assert.Equal([StepState.Done, StepState.Failed, StepState.Skipped], d.ShownSteps.Select(s => s.State));
        Assert.Equal("stopped at PlayerController.cpp", d.ShownSteps[1].Detail);
        Assert.Equal("skipped", d.ShownSteps[2].Detail);
        Assert.Equal(["PlayerController.cs(22,9): ';' expected", "Slime.cs(14,13): GAE003 new inside Update — allocate in Start", "PlayerController.cs(8,5): GAE002 float → use fixed"],
            failure.Messages.Select(m => m.Text));
        // a script failure shows Convert assets (run before it) and the skipped steps
        var steps = BuildStream.UiOrder.Select(t => new BuildStepView(t)).ToList();
        steps[1].State = StepState.Done;
        steps[0].State = StepState.Failed;
        steps[2].State = steps[3].State = StepState.Skipped;
        Assert.Equal([GameBuilder.StepAssets, GameBuilder.StepScripts, GameBuilder.StepCompile, GameBuilder.StepLink], BuildFailedDialog.StepsToShow(steps).Select(s => s.Title));
    }

    [AvaloniaFact]
    public void GoToFirstError_OpensTheScriptAtItsLine()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        try
        {
            _ = fx.Services.Windows.Open("T-D23", GroupShots.Failed(fx));
            Capture.Pump();
            var d = Assert.IsType<BuildFailedDialog>(win.Overlays.Open.Single());
            var go = d.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm");
            var pt = go.TranslatePoint(new Point(go.Bounds.Width / 2, go.Bounds.Height / 2), win)!.Value;
            win.MouseDown(pt, MouseButton.Left);
            win.MouseUp(pt, MouseButton.Left);
            Capture.Pump();
            Assert.Equal(Path.Combine(p.ScriptsDir, "PlayerController.cs"), d.Opened);
            Assert.Equal(("T-P04", (string?)"BuildFailedGoToError"), fx.Services.Windows.Requests[^1]);
            // the Script Editor shows the file with the caret on the error
            var script = Assert.IsType<AgbStudio.Editor.Windows.Scripts.ScriptEditorPage>(win.CurrentPage);
            Assert.Equal((d.Opened, 22, 9), (script.Buffer!.Path, script.Editor.CaretLine, script.Editor.CaretColumn));
            Assert.Equal("PlayerController.cs", fx.Services.Documents.Active!.Name);
            Assert.DoesNotContain(d, win.Overlays.Open);
        }
        finally { win.Close(); }
    }

    /// <summary>A real build of a small project through BuildService (devkitARM, ninja, gbafix) with the dialog as observer.</summary>
    [AvaloniaFact]
    public void BuildRom_RealBuild_StreamsStepsAndUpdatesStatusAndReport()
    {
        if (!new Toolchain().IsComplete) { Assert.Fail("devkitPro toolchain not found"); return; }
        using var fx = new Fixture();
        var dir = Path.Combine(Repo.Root, "build", "editor-tests", "buildrom");
        var p = SampleProjects.CreatePlatformerMini(dir);
        fx.Services.Project.Open(dir);
        fx.Services.ProjectSettings.Settings.Build.RunAfterBuild = false;
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        try
        {
            fx.Services.Commands.Execute("build.buildRom");
            Capture.Pump();
            var d = Assert.IsType<BuildRomDialog>(win.Overlays.Open.Single());
            GroupFixture.PumpUntil(() => d.Completion.IsCompleted, 600_000);
            var r = d.Completion.Result!;
            Assert.True(r.Success, string.Join('\n', r.Diagnostics));
            Assert.All(d.Stream.Steps, s => Assert.Equal(StepState.Done, s.State));
            Assert.Contains(d.Stream.Log, l => l.Text.StartsWith("g++ ", StringComparison.Ordinal) || l.Text.StartsWith("ld ", StringComparison.Ordinal) || l.Text.StartsWith("gbafix", StringComparison.Ordinal) || l.Level == ConsoleLevel.Warning);
            Assert.EndsWith(".gba · " + PageFrame.Kb(new FileInfo(r.RomPath).Length), d.Stream.Step(GameBuilder.StepLink).Detail);
            Assert.StartsWith("Built ", d.Title);
            Assert.StartsWith("Build OK", fx.Services.Status.Build);
            Assert.Equal($"ROM {new FileInfo(r.RomPath).Length / 1024} KB / 32 MB", fx.Services.Status.Rom);
            // the Build Report reads the report this build wrote
            fx.Services.Commands.Execute("build.report");
            Capture.Pump();
            var report = Assert.IsType<BuildReportPage>(win.CurrentPage);
            Assert.Equal(new FileInfo(r.RomPath).Length, report.Report!.RomBytes);
            Assert.Equal(0, report.History[0].Errors);
        }
        finally { win.Close(); }
    }

    [AvaloniaFact]
    public void BuildRom_ScriptError_OpensBuildFailedAndRecordsHistory()
    {
        if (!new Toolchain().IsComplete) { Assert.Fail("devkitPro toolchain not found"); return; }
        using var fx = new Fixture();
        var dir = Path.Combine(fx.Folder, "broken");
        var p = SampleProjects.CreatePlatformerMini(dir);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Broken.cs"), "public class Broken : Behaviour\n{\n    void Update()\n    {\n        int x = 1\n    }\n}\n");
        fx.Services.Project.Open(dir);
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        try
        {
            fx.Services.Commands.Execute("build.buildRom");
            Assert.Equal(("T-D06", (string?)"build.buildRom"), fx.Services.Windows.Requests[^1]);
            // the script error stops the build in its first step, possibly before the Build ROM dialog was even drawn
            GroupFixture.PumpUntil(() => win.Overlays.Open.OfType<BuildFailedDialog>().Any(), 600_000);
            Capture.Pump();
            Assert.False(fx.Services.Build.Last!.Success);
            var failed = Assert.IsType<BuildFailedDialog>(win.Overlays.Open.Single());
            Assert.Contains(failed.ShownSteps, s => s.State == StepState.Failed);
            Assert.Contains(failed.Failure.Errors, e => e.Text.Contains("Broken.cs", StringComparison.Ordinal));
            // the status bar names the error count, as the Build ROM · Failed title does
            Assert.Matches(@"^Build failed · \d+ errors?$", fx.Services.Status.Build);
            var history = BuildStream.LoadHistory(p);
            Assert.True(history[0].Errors > 0);
            Assert.Contains("Broken.cs", history[0].FirstError, StringComparison.Ordinal);
        }
        finally { win.Close(); }
    }

    [AvaloniaFact]
    public void RunInEmulator_AfterSuccess_OpensPlayModeWithTheRom_AndExportCopiesPadded()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        GroupFixture.WriteReport(p, 300);
        var rom = Path.Combine(p.BuildDir, "sky_hopper.gba");
        File.WriteAllText(Path.Combine(p.BuildDir, "THIRD_PARTY.txt"), "libtonc, maxmod");   // every build writes the notices beside the ROM
        var dest = Path.Combine(fx.Folder, "out", "export.gba");
        var d = new BuildRomDialog(fx.Services, new BuildRequest(BuildConfiguration.Release, dest, true), start: false);
        var ok = new BuildResult { Success = true, RomPath = rom };
        d.RunInEmulator();   // asked while building: runs when the build succeeds
        d.Finish(ok);
        Assert.Equal(("T-P02", "BuildRomRunInEmulator"), fx.Services.Windows.Requests[^1]);
        Assert.Equal(512 * 1024, new FileInfo(dest).Length);
        Assert.Equal(0xFF, File.ReadAllBytes(dest)[^1]);
        Assert.Equal("libtonc, maxmod", File.ReadAllText(Path.Combine(fx.Folder, "out", "THIRD_PARTY.txt")));
        Assert.Contains(fx.Services.Toasts.Visible, t => t.Text == "Exported export.gba");
    }
}

/// <summary>Build Report (P11.58): the real report.json and history, header fields, Export actions, Build again.</summary>
public class BuildReportTests
{
    [AvaloniaFact]
    public void Report_ShowsSizeTimesWarningsVramBreakdownAssetsAndHistory()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        GroupFixture.WriteReport(p);
        var win = GroupFixture.Show(fx, "T-P11");
        try
        {
            var page = Assert.IsType<BuildReportPage>(win.CurrentPage);
            string Card(string n) => string.Join(" | ", win.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Card_" + n).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Equal("ROM SIZE | 412 KB | of 32 MB cartridge · 1.3 %", Card("RomSize"));
            Assert.Equal("BUILD TIME | 0.80 s | C# 0.21 · assets 0.18 · g++ 0.34", Card("BuildTime"));
            Assert.Equal("WARNINGS | 1 | GAE002 float in PlayerController", Card("Warnings"));
            Assert.Equal("FREE VRAM | 58 KB | of 96 KB", Card("FreeVram"));
            var legend = win.GetVisualDescendants().OfType<StackPanel>().First(s => s.Name == "BreakdownLegend").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);
            Assert.Equal(["Graphics · 182 KB", "Code · 128 KB", "Audio · 96 KB", "Data · 6 KB"], legend);
            var cells = win.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "LargestAssets").Children.OfType<TableCell>().Select(c => c.Text).Where(t => t != null).ToList();
            Assert.Equal(["Asset", "Type", "Size", "Share", "bgm_forest.xm", "Music", "41 KB", "forest_tiles.png", "Tileset", "32 KB", "thai_vwf.font", "Font", "6.2 KB", "hero.png", "Sprite", "4 KB", "Items.db", "Data", "1.1 KB"], cells);
            var history = win.GetVisualDescendants().OfType<StackPanel>().First(s => s.Name == "History").Children.OfType<StackPanel>()
                .Select(r => string.Join(" ", r.Children.OfType<TextBlock>().Select(t => t.Text))).ToList();
            Assert.Equal(["12:06:03 412 KB · 0.80 s · 1 warning", "11:58:40 409 KB · 0.77 s", "11:52:12 PlayerController.cs(22): ';' expected", "11:40:05 401 KB · 0.91 s"], history);
            Assert.Equal("Build Report", win.TitleBar.Document);
            Assert.Equal("Build OK", win.GetVisualDescendants().OfType<StatusItem>().First(s => s.Name == "Status_Build").Content);
            Assert.Equal("0.80 s · 12:06:03", win.GetVisualDescendants().OfType<StatusItem>().First(s => s.Name == "Status_Time").Content);
            _ = page;
        }
        finally { win.Close(); }
    }

    [AvaloniaFact]
    public void HeaderFields_EditProjectSettings_AndExportActionsOpenTheirWindows()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        GroupFixture.WriteReport(p);
        var launched = new List<string>();
        fx.Services.System = new SystemShell(() => null, (_, a) => launched.Add(a));
        var page = new BuildReportPage(fx.Services);
        Capture.Host(page, 1440, 860).Close();
        var title = page.GetVisualDescendants().OfType<InspectorField>().First(f => f.Name == "Field_Title");
        Assert.Equal("SKYHOPPER", title.Value);
        title.Value = "skyhop2";
        Assert.Equal("SKYHOP2", fx.Services.ProjectSettings.Settings.General.RomTitle);
        Assert.Equal("SKYHOP2", GameProject.Open(p.Root).Settings.General.RomTitle);
        page.GetVisualDescendants().OfType<InspectorField>().First(f => f.Name == "Field_Version").Value = "not a version";
        Assert.Equal("1.0.3", fx.Services.ProjectSettings.Settings.General.Version);
        fx.Services.Undo.Undo();
        Assert.Equal("SKYHOPPER", fx.Services.ProjectSettings.Settings.General.RomTitle);
        Assert.Equal("SRAM · 32 KB", page.GetVisualDescendants().OfType<InspectorField>().First(f => f.Name == "Field_SaveType").Value);
        page.OpenBuildFolder();
        Assert.Contains(p.BuildDir, launched.Single());
        page.RunInMgba();
        Assert.Equal(("T-P02", "BuildReportRunInMgba"), fx.Services.Windows.Requests[^1]);
        page.BuildAgain();
        Assert.Equal(("T-D06", "BuildReportBuildAgain"), fx.Services.Windows.Requests[^1]);
        page.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "FlashToCartridge").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(("T-D07", "BuildReportFlash"), fx.Services.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public void MenuCommands_OpenTheBuildWindows()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        GroupFixture.WriteReport(p);
        fx.Services.Set<ICartridgeDeviceProvider>(new FakeDevices(new FakeCartridge()));
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        win.Show();
        try
        {
            fx.Services.Commands.Execute("build.report");
            Capture.Pump();
            Assert.IsType<BuildReportPage>(win.CurrentPage);
            fx.Services.Commands.Execute("build.flash");
            Capture.Pump();
            Assert.IsType<FlashDialog>(win.Overlays.Open.Single());
            win.Overlays.CloseTop();
            fx.Services.Commands.Execute("file.exportRom");
            Capture.Pump();
            Assert.IsType<ExportRomDialog>(win.Overlays.Open.Single());
            win.Overlays.CloseTop();
        }
        finally { win.Close(); }
    }
}

/// <summary>Export ROM (P11.59).</summary>
public class ExportRomTests
{
    [AvaloniaFact]
    public void Fields_ComeFromTheProject_ValidateAndExportARelease()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        GroupFixture.WriteReport(p, 398);
        var d = new ExportRomDialog(fx.Services);
        Assert.Equal("skyhopper.gba", d.FileName.Text);
        Assert.Equal(Path.GetFullPath(p.BuildDir), Path.GetFullPath(d.Folder.Selected!));
        Assert.Equal(("SKYHOPPER", "SKYH", "1.0.3"), (d.RomTitle.Text, d.GameCode.Text, d.Version.Text));
        Assert.True(d.Compress.IsChecked);
        Assert.True(d.Strip.IsChecked);
        Assert.Equal("398 KB / 32 MB", d.EstimateText);
        d.Pad.IsChecked = true;
        Assert.Equal("512 KB / 32 MB", d.EstimateText);
        d.GameCode.Text = "TOOLONG";
        d.Version.Text = "1.0";
        Assert.False(d.Validate());
        Assert.NotNull(d.GameCode.Help);
        Assert.NotNull(d.Version.Help);
        d.GameCode.Text = "skh2";
        d.Version.Text = "1.1.0";
        d.Compress.IsChecked = false;
        d.Export();
        var s = fx.Services.ProjectSettings.Settings;
        Assert.Equal(("SKH2", "1.1.0", false), (s.General.GameCode, s.General.Version, s.Build.CompressGraphics));
        Assert.Equal(new BuildRequest(BuildConfiguration.Release, Path.Combine(d.Folder.Selected!, "skyhopper.gba"), true), d.Request);
        Assert.Equal(("T-D06", "ExportRom"), fx.Services.Windows.Requests[^1]);
        Assert.Equal(1024, ExportRomDialog.PadRom(new byte[1000]).Length);
    }
}

/// <summary>
/// Export ROM "Pad ROM to a power of two" in the emulator (P7.15, T-D05): a real Release ROM of a small game, padded by
/// <see cref="ExportRomDialog.PadRom"/> as the export writes it, boots in the mGBA core and shows the same frames as the
/// ROM the build made.
/// </summary>
[Collection(EmulatorCollection.Name)]
public class ExportRomBootTests
{
    [Fact]
    public void PaddedReleaseRom_BootsToTheSameFramesAsTheBuiltRom()
    {
        if (!new Toolchain().IsComplete) { Assert.Fail("devkitPro toolchain not found"); return; }
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "export_padded");
        var p = SampleProjects.CreatePlatformerMini(dir);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build(new BuildOptions { Configuration = BuildConfiguration.Release });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics.Where(d => d.Severity == Severity.Error)));
        var rom = File.ReadAllBytes(r.RomPath);
        var padded = ExportRomDialog.PadRom(rom);
        Assert.True(padded.Length > rom.Length, $"the built ROM ({rom.Length} bytes) is already a power of two, so nothing was padded");
        Assert.Equal(0, padded.Length & (padded.Length - 1));
        Assert.True(padded.AsSpan(0, rom.Length).SequenceEqual(rom));
        Assert.True(padded.AsSpan(rom.Length).IndexOfAnyExcept((byte)0xFF) < 0);
        var exported = Path.Combine(dir, "export", "padded.gba");
        Directory.CreateDirectory(Path.GetDirectoryName(exported)!);
        File.WriteAllBytes(exported, padded);

        // the process core, shared with the other editor tests (D-06)
        using var host = new EmulatorHost();
        var core = host.Acquire();
        List<uint[]> Frames(string path)
        {
            core.LoadRom(path);
            var shots = new List<uint[]>();
            for (var f = 1; f <= 360; f++)
            {
                core.RunFrame();
                if (f % 120 == 0) shots.Add((uint[])core.Frame.Clone());
            }
            core.UnloadRom();
            return shots;
        }
        var built = Frames(r.RomPath);
        var fromExport = Frames(exported);
        for (var i = 0; i < built.Count; i++)
            Assert.True(built[i].AsSpan().SequenceEqual(fromExport[i]), $"frame {(i + 1) * 120}: the padded ROM shows another picture than the built ROM");
        // the game is running at frame 360 (not a blank screen of a ROM that did not start)
        Assert.True(built[^1].Distinct().Count() > 4, "the built ROM shows a blank screen at frame 360");
    }
}

/// <summary>Flash to Cartridge per D-12 with a fake device (P11.60; hardware n/a until hardware).</summary>
public class FlashTests
{
    // T-D07: the flash size comes from the cartridge's address mirrors (a 4 / 8 / 16 MB chip repeats itself)
    [Fact]
    public void FlashSize_FromAddressMirrors()
    {
        byte[] Rom(int mb, int offset) => Enumerable.Range(0, 64).Select(i => (byte)((offset % (mb * 1024 * 1024)) / (1024 * 1024) * 7 + i)).ToArray();
        foreach (var mb in new[] { 4, 8, 16, 32 })
            Assert.Equal(mb, CartridgeStatus.FlashSizeFromMirrors(o => Rom(mb, o)));
        // the save part of the line follows Project Settings › Save data
        var s = new CartridgeStatus(true, "Cartridge detected · SKYH", "", FlashMegabytes: 16);
        Assert.Equal("Cartridge detected · 16 MB flash · Flash 128 KB", s.Line(AgbStudio.Core.Project.SaveType.Flash128K));
        Assert.Equal("Cartridge detected · 16 MB flash", s.Line(AgbStudio.Core.Project.SaveType.None));
        Assert.Equal("Cartridge detected · SKYH", (s with { FlashMegabytes = null }).Line(AgbStudio.Core.Project.SaveType.Sram32K));
    }

    [AvaloniaFact]
    public void Detects_BacksUp_Writes_Verifies_WithProgress()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        GroupFixture.WriteReport(p);
        var dev = new FakeCartridge();
        var d = new FlashDialog(fx.Services, new FakeDevices(dev, new FakeCartridge("EverDrive GBA · SD card (E:)")));
        Assert.Equal(["GBxCart RW · USB (COM4)", "EverDrive GBA · SD card (E:)"], d.Device.Options);
        // T-D07: the cartridge line shows the measured flash size and the save chip of the game (Save data: SRAM)
        Assert.Equal(AgbStudio.Core.Project.SaveType.Sram32K, fx.Services.ProjectSettings.Settings.Save.Type);
        Assert.Equal("Cartridge detected · 32 MB flash · SRAM 32 KB", d.InfoTitle);
        Assert.True(d.CanFlash);
        Assert.Equal("0 / 412 KB", d.ProgressText);
        var task = d.FlashAsync();
        GroupFixture.PumpUntil(() => task.IsCompleted, 10_000);
        Assert.True(task.Result);
        Assert.Equal(File.ReadAllBytes(Path.Combine(p.BuildDir, "sky_hopper.gba")), dev.Flash);
        Assert.Equal(dev.Save, File.ReadAllBytes(d.BackupPath!));
        Assert.Equal("Verifying", d.StageText);
        Assert.Contains(fx.Services.Toasts.Visible, t => t.Text.StartsWith("Flashed sky_hopper.gba", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void VerifyFailure_NoCartridge_AndGbxCartWriteAreReported()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        GroupFixture.WriteReport(p);
        var bad = new FakeCartridge { CorruptOnWrite = true };
        var d = new FlashDialog(fx.Services, new FakeDevices(bad));
        var t = d.FlashAsync();
        GroupFixture.PumpUntil(() => t.IsCompleted, 10_000);
        Assert.False(t.Result);
        Assert.Contains(fx.Services.Toasts.Visible, x => x.Text.StartsWith("Verify failed", StringComparison.Ordinal));
        var empty = new FakeCartridge { Status = new CartridgeStatus(false, "No cartridge", "Insert a cartridge") };
        var d2 = new FlashDialog(fx.Services, new FakeDevices(empty));
        Assert.False(d2.CanFlash);
        Assert.Equal("No cartridge", d2.InfoTitle);
        var gbx = new GbxCartRw("COM99");
        Assert.Equal("GBxCart RW · USB (COM99)", gbx.Name);
        var write = gbx.WriteRomAsync("a.gba", [], new Progress<long>(), CancellationToken.None);
        Assert.IsType<NotSupportedException>(write.Exception?.InnerException);
    }

    [AvaloniaFact]
    public void SdCardFlashCart_CopiesVerifiesAndFindsTheSave()
    {
        using var fx = new Fixture();
        var root = Directory.CreateDirectory(Path.Combine(fx.Folder, "sd")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "SAVER"));
        File.WriteAllBytes(Path.Combine(root, "SAVER", "game.sav"), [9, 8, 7]);
        var sd = new SdCardFlashCart(root);
        Assert.StartsWith("EZ-Flash · SD card", sd.Name);
        T Await<T>(Task<T> t) { GroupFixture.PumpUntil(() => t.IsCompleted, 10_000); return t.Result; }
        Assert.True(Await(sd.DetectAsync(CancellationToken.None)).Detected);
        var rom = Enumerable.Range(0, 100_000).Select(i => (byte)i).ToArray();
        long reported = 0;
        var write = sd.WriteRomAsync("game.gba", rom, new SyncProgress(n => reported = n), CancellationToken.None);
        GroupFixture.PumpUntil(() => write.IsCompleted, 10_000);
        Assert.True(write.IsCompletedSuccessfully);
        Assert.Equal(rom.Length, reported);
        Assert.Equal(rom, File.ReadAllBytes(Path.Combine(root, "game.gba")));
        Assert.True(Await(sd.VerifyAsync("game.gba", rom, new SyncProgress(_ => { }), CancellationToken.None)));
        Assert.Equal([9, 8, 7], Await(sd.ReadSaveAsync("game.gba", new SyncProgress(_ => { }), CancellationToken.None)));
    }

    sealed class SyncProgress(Action<long> a) : IProgress<long>
    {
        public void Report(long value) => a(value);
    }
}
