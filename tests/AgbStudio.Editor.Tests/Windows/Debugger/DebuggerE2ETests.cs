using AgbStudio.Build;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Play;
using AgbStudio.Editor.Windows.Debugger;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Debugger;

/// <summary>Debugger E2E (P13.01-P13.09) on the running Sky Hopper fixture.</summary>
[Collection(EmulatorCollection.Name)]
public class DebuggerE2ETests : EditorDriver
{
    DebuggerPage OpenDebugger(string id = "T-P19")
    {
        PlayTestSupport.RunForest(Play);
        Play.RunFrames(150);  // the player lands; the camera stops following the fall
        _ = Fx.Services.Windows.Open(id, source: "test");
        Capture.Pump();
        var page = Assert.IsType<DebuggerPage>(Win.CurrentPage);
        Play.Publish();
        Capture.Pump();
        return page;
    }

    DebuggerModel Model => DebuggerModel.Of(Fx.Services);

    [AvaloniaFact]
    public void P13_01_Profiler_120FramesAgainstBudget_ThisFrameLogicRenderAudioIdle()
    {
        var page = OpenDebugger();
        Assert.Equal(DebuggerView.VramTiles, page.View);
        var graph = page.Profiler.GetVisualDescendants().OfType<FrameGraph>().Single();
        Assert.Equal(120, graph.Frames.Length);
        Assert.Equal(16.74, FrameGraph.BudgetMs, 2);
        var p = Play.Profile!;
        Assert.Equal(p.History.Select(c => ProfilerStats.Ms(c)), graph.Frames);
        string[] split = ["Logic", "Render", "Audio", "Idle"];
        var meters = split.Select(n => Find<Meter>("Meter_" + n)).ToList();
        Assert.All(meters, m => Assert.Matches(@"^\d+\.\d ms$", m.ValueText));
        var total = meters.Sum(m => double.Parse(m.ValueText![..^3], System.Globalization.CultureInfo.InvariantCulture));
        Assert.InRange(total, 16.0, 17.4);  // the four parts make one frame
        Assert.Equal(ProfilerStats.Ms(p.Logic).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms", meters[0].ValueText);
        Assert.Matches(@"^\d+ / 256 KB$", Find<Meter>("Meter_EWRAM").ValueText);
        Assert.Matches(@"^\d+ / 128$", Find<Meter>("Meter_OAM").ValueText);
    }

    [AvaloniaFact]
    public void P13_02_VramTiles_ByCharBlock_MatchEmulatorMemory()
    {
        var page = OpenDebugger();
        var vram = page.Vram;
        Assert.Equal("BG char block 0", vram.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("BG char block", StringComparison.Ordinal) == true).Text);
        Assert.Contains(vram.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("BG: block 0", StringComparison.Ordinal) == true && t.Text.EndsWith("OBJ: block 4", StringComparison.Ordinal));
        var bg = Find<PixelImage>("BgTiles");
        // tile t of char block 0 at (t % 16 * 9, t / 16 * 9): every non-transparent pixel is that VRAM nibble in a palette colour
        var core = Play.Core!;
        var tiles = core.Memory(GbaMemory.Vram, 128 * 32).ToArray();
        var pal = Inspector.PaletteRam(core);
        var checkedPixels = 0;
        for (var t = 1; t < 128; t++)
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                {
                    var b = tiles[t * 32 + y * 4 + x / 2];
                    var idx = (x & 1) == 0 ? b & 15 : b >> 4;
                    var shown = bg.Pixels[(t / 16 * 9 + y) * bg.PixelWidth + t % 16 * 9 + x];
                    if (idx == 0) { Assert.Equal(0u, shown); continue; }
                    Assert.True(Enumerable.Range(0, 16).Select(bank => Inspector.ToArgb(pal[bank * 16 + idx])).Contains(shown), $"tile {t} ({x},{y}) idx {idx} shown {shown:X8}");
                    checkedPixels++;
                }
        Assert.True(checkedPixels > 500, $"{checkedPixels} pixels");
        // OBJ graphics: the player's sprite (OAM entry 0, tile 0, palette 0) as in OBJ VRAM
        var objs = Find<WrapPanel>("ObjTiles").Children.OfType<PixelImage>().ToList();
        Assert.NotEmpty(objs);
        var first = Play.Oam[0];
        var objBytes = core.Memory(GbaMemory.Vram + 0x10000 + (uint)first.Tile * 32, 32).ToArray();
        var nib = objBytes[0] & 15;
        Assert.Equal(nib == 0 ? 0u : Inspector.ToArgb(pal[256 + first.Palette * 16 + nib]), objs[0].Pixels[0]);
    }

    [AvaloniaFact]
    public void P13_03_OamTable_ActorXYSizeTilePalPrioFlags_InUse()
    {
        var page = OpenDebugger();
        var oam = Inspector.Oam(Play.Core!);
        Assert.Contains($"OAM · {oam.Count} / 128 in use", page.Vram.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.Equal(["#", "ACTOR", "X", "Y", "SIZE", "TILE", "PAL", "PRIO", "FLAGS"],
            page.Vram.GetVisualDescendants().OfType<TableCell>().Where(c => c.Kind == TableCellKind.Header).Select(c => c.Text!.ToUpperInvariant()));
        var rows = page.Vram.Rows;
        Assert.Equal(oam.Count, rows.Count);
        var player = rows.First(r => r.Actor == "Player").Entry;
        var cells = Find<StackPanel>("OamRow_0").Children.OfType<TableCell>().Select(c => c.Text).ToList();
        Assert.Equal([player.Index.ToString(), "Player", player.X.ToString(), player.Y.ToString(), $"{player.Width}×{player.Height}", player.Tile.ToString(), player.Palette.ToString(), player.Priority.ToString(), "—"], cells);
        Assert.True(rows.Any(r => r.Actor.StartsWith("Coin", StringComparison.Ordinal) && r.Entry.Width == 8),
            string.Join(" | ", rows.Select(r => $"{r.Entry.Index} {r.Actor} {r.Entry.X},{r.Entry.Y}")) + " cam " + Play.World.CameraX + "," + Play.World.CameraY);
        Assert.Contains(rows, r => r.Actor.StartsWith("Slime", StringComparison.Ordinal));
        Assert.All(Find<StackPanel>("OamRow_0").Children.OfType<TableCell>(), c => Assert.Equal(TableCellKind.Selected, c.Kind));
        Click(Find<StackPanel>("OamRow_2"));
        Assert.Equal(2, page.Vram.SelectedRow);
    }

    [AvaloniaFact]
    public void P13_04_Palettes_LiveRam_ChangedOutline_AddressBgr555Rgb_UsedBy_OpenAndBreak()
    {
        var page = OpenDebugger("T-P24");
        Assert.Equal(DebuggerView.Palettes, page.View);
        var core = Play.Core!;
        var pal = Inspector.PaletteRam(core);
        var used = Enumerable.Range(16, 16).First(i => i % 16 != 0 && pal[i] != 0);  // a colour of BG bank 1
        Click(page.Palettes.Cell(used));
        Assert.Equal(used, Model.SelectedColour);
        Assert.Equal(Color.FromUInt32(Inspector.ToArgb(pal[used])), ((ISolidColorBrush)page.Palettes.Cell(used).Background!).Color);
        Assert.Equal($"BG 1 · index {used % 16}", page.Colour.Title);
        Assert.Equal($"0x0500 {used * 2:X4}", page.Colour.AddressText);
        Assert.Equal($"0x{pal[used]:X4} (BGR555)", page.Colour.ValueText);
        Assert.Equal($"{pal[used] & 31}, {(pal[used] >> 5) & 31}, {(pal[used] >> 10) & 31}", page.Colour.RgbText);
        Assert.Contains(page.Colour.UsedByLines, l => l.StartsWith("BG1 Hills · tiles ", StringComparison.Ordinal));
        // T-P24: the palette effect on the colour (the project's water cycle runs on BG 1, entries 6-9)
        Click(page.Palettes.Cell(16 + 7));
        Play.Publish();
        Capture.Pump();
        Assert.Equal("BG 1 · index 7", page.Colour.Title);
        Assert.Contains("Palette effect · water cycle (6–9)", page.Colour.UsedByLines);
        Assert.True(page.Colour.UsedByLines.ToList().IndexOf("Palette effect · water cycle (6–9)") < page.Colour.UsedByLines.Count - 1);   // before "Changed …"
        Click(page.Palettes.Cell(16 + 3));
        Play.Publish();
        Capture.Pump();
        Assert.DoesNotContain(page.Colour.UsedByLines, l => l.StartsWith("Palette effect", StringComparison.Ordinal));
        Click(page.Palettes.Cell(used));
        Play.Publish();
        Capture.Pump();
        // a colour changed this frame gets the white outline
        BitConverter.TryWriteBytes(core.Memory(GbaMemory.Palette + (uint)used * 2, 2), (ushort)(pal[used] ^ 0x7FFF));
        Play.Publish();
        Capture.Pump();
        Assert.True(Model.ChangedThisFrame[used]);
        Assert.Equal(Colors.White, ((ISolidColorBrush)page.Palettes.Cell(used).BorderBrush!).Color);
        Assert.Contains("Changed 0 frames ago", page.Colour.UsedByLines);
        // Break when this colour changes: the next change pauses the game
        Click(page.Colour.BreakOnChange);
        Assert.Equal(used, Model.BreakOnColour);
        Play.RunFrames(1);
        Assert.False(Play.IsPaused);
        var now = Inspector.PaletteRam(core)[used];
        BitConverter.TryWriteBytes(core.Memory(GbaMemory.Palette + (uint)used * 2, 2), (ushort)(now ^ 0x001F));
        Play.Publish();
        Capture.Pump();
        Assert.True(Play.IsPaused);
        Assert.True(page.PausedChip.IsVisible);
        // Open in Palette Manager
        Click(page.Colour.OpenPaletteManager);
        Assert.Equal(("T-P10", "DebuggerPalettes"), Fx.Services.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public void P13_05_Memory_MapByCategory_WatchList_HexView_AddWatch()
    {
        var page = OpenDebugger("T-P23");
        Assert.Equal(DebuggerView.Memory, page.View);
        var (ew, iw) = Model.MemoryMap();
        Assert.True(ew.Single(s => s.Category == "Actor pool").Bytes > 0);
        Assert.True(ew.Single(s => s.Category == "Audio mixer").Bytes > 0);
        Assert.True(iw.Single(s => s.Category == "Fast code (ARM)").Bytes > 0);
        Assert.True(iw.Single(s => s.Category == "Stack").Bytes > 0);
        Assert.True(iw.Single(s => s.Category == "Globals").Bytes > 0);
        page.Memory.Refresh(force: true);
        Assert.Contains(page.Memory.LegendTexts, t => t.StartsWith("Actor pool · ", StringComparison.Ordinal));
        Assert.Contains(page.Memory.LegendTexts, t => t.StartsWith("Free · ", StringComparison.Ordinal));
        // watches: player.position follows the game
        var player = Play.World.Find("Player")!;
        Assert.Equal(FormattableString.Invariant($"player.position=({player.Actor.X:0.00}, {player.Actor.Y:0.00})"), page.Watch.Values[0]);
        Assert.Equal("player.lives=3", page.Watch.Values[2]);
        Assert.Equal("Save.buffer=63 bytes", page.Watch.Values[4]);
        // hex view: 5 rows of 16 bytes at the selected watch, as in emulator memory
        var addr = Model.Watches[0].Address & ~15u;
        var bytes = Play.Core!.Memory(addr, 16).ToArray();
        Assert.StartsWith($"{addr >> 16:X4} {addr & 0xFFFF:X4} | {string.Join(" ", bytes.Select(b => b.ToString("X2")))}", page.Memory.HexText);
        Assert.Equal(5, page.Memory.HexText.Split('\n').Length);
        // Add watch by name, and by address
        Click(page.Watch.AddButton);
        var box = Find<TextBox>("AddWatchBox");
        Assert.True(box.IsVisible);
        box.Text = "Game.lives";
        box.Focus();
        Press(PhysicalKey.Enter);
        Assert.Contains("Game.lives", Model.WatchNames);
        Assert.False(box.IsVisible);
        Assert.True(Model.AddWatch("0x03000000"));
        Assert.False(Model.AddWatch("nobody.position"));
        Play.RunFrames(1);
        Capture.Pump();
        Assert.Equal(7, page.Watch.Values.Count);
    }

    [AvaloniaFact]
    public void P13_06_Breakpoints_StopAtPlayerController19_PausedAtFrame_CallStackInCSharpNames()
    {
        var page = OpenDebugger();
        Model.AddBreakpoint("PlayerController.cs", 19);
        Model.AddBreakpoint("Slime.cs", 42, enabled: false);
        Model.AddSceneLoad(enabled: false);
        Capture.Pump();
        Assert.Equal(["PlayerController.cs:19", "Slime.cs:42", "On scene load"], page.BreakpointBoxes.Select(b => b.Content));
        Assert.Equal((7, 20), Model.BindCSharp(Model.Breakpoints.First(b => b.Label == "PlayerController.cs:19")));  // line 19 is the "{" of the jump: it stops at its first statement
        Play.RunFrames(2);
        page.Focus();
        Play.KeyDown(Key.Z);  // jump: enters the block at line 19
        for (var i = 0; i < 10 && !Model.AtBreakpoint; i++) Play.RunFrames(1);
        Play.KeyUp(Key.Z);
        Capture.Pump();
        Assert.True(Model.AtBreakpoint);
        Assert.Equal("PlayerController.cs:19", Model.PausedAt);
        Assert.Equal($"Paused at frame {Model.PausedFrame}", page.PausedChip.Content);
        Assert.Equal("Paused", page.RunState.Content);
        Assert.Equal("PlayerController.cs:19", page.Location.Content);
        Assert.Equal(["PlayerController.Update() :19", "Actor.Tick()", "Scene.Update()", "Game.Frame()"], page.Profiler.StackLines);
        // the game stays inside the statement
        var frame = Play.Frame;
        Play.RunFrames(5);
        Assert.Equal(frame, Play.Frame);
        // step to the next line, then continue
        Click(page.StepLineButton);
        Play.RunFrames(2);
        Assert.Equal("PlayerController.cs:21", Model.PausedAt);
        page.BreakpointBoxes[0].IsChecked = false;
        Click(page.PausedChip);
        Play.RunFrames(5);
        Assert.False(Model.AtBreakpoint);
        Assert.True(Play.Frame > frame);
        // VM script lines go to the Event Script VM's table
        var gen = Directory.CreateTempSubdirectory("agb-debugmap-").FullName;
        File.WriteAllText(Path.Combine(gen, "debug-map.json"), "{\"files\":[\"Intro.cs\"],\"paths\":[\"scripts/Intro.cs\"],\"methods\":[]}");
        File.WriteAllText(Path.Combine(gen, "scripts.json"), "{\"scripts\":[{\"id\":3,\"name\":\"Intro.Play\",\"file\":\"scripts/Intro.cs\",\"line\":5,\"lines\":[[0,6],[4,7]]}]}");
        Model.UseMap(DebugMap.Load(gen)!);
        var vmBp = Model.AddBreakpoint("Intro.cs", 7);
        Assert.Equal(BreakpointKind.ScriptLine, vmBp.Kind);
        Play.RunFrames(1);
        var vm = new ScriptDebugger(Play.Core!, Play.Session!.Channel!);
        var state = Play.Session.Channel!.Debugger;
        Assert.Equal(1, Play.Core!.Memory(state + 64, 1)[0]);
        Assert.Equal((3, 7), (Play.Core.ReadU16(state), Play.Core.ReadU16(state + 2)));
        Assert.True(vm.Available);
        // On scene load: restarting loads Forest_01 and the game pauses there
        Model.Breakpoints.Remove(vmBp);
        Model.Breakpoints.Single(b => b.Kind == BreakpointKind.SceneLoad).Enabled = true;
        Play.Run(PlayTestSupport.Built.RomPath, "Forest_01", PlayStart.FromHere);
        for (var i = 0; i < 30 && !Play.IsPaused; i++) Play.RunFrames(10);
        Assert.True(Play.IsPaused);
        Assert.StartsWith("On scene load · ", Model.PauseReason);
        Directory.Delete(gen, true);
    }

    [AvaloniaFact]
    public void P13_09_DebugButtonAndToolsDebugger_OpenTheDebugger_ViewsMemoryAndPalettes()
    {
        PlayTestSupport.RunForest(Play);
        // #9 Debug in the title bar
        var debug = Win.TitleBar.GetVisualDescendants().OfType<Button>().First(b => b.Command == Fx.Services.Commands.Get("tools.debugger"));
        Click(debug);
        var page = Assert.IsType<DebuggerPage>(Win.CurrentPage);
        Assert.Equal(("T-P19", "tools.debugger"), Fx.Services.Windows.Requests[^1]);
        Assert.Equal("Debugger", Win.TitleBar.Document);
        // Tools › Debugger (Ctrl+Shift+D) from the Scene Editor
        _ = Fx.Services.Windows.Open("T-P01");
        Capture.Pump();
        Press(PhysicalKey.D, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.IsType<DebuggerPage>(Win.CurrentPage);
        // Views › Memory (T-C41) and Views › Palettes (T-C42)
        page = (DebuggerPage)Win.CurrentPage!;
        Click(page.ViewRows.Single(r => r.Label == "Memory"));
        Assert.Equal(DebuggerView.Memory, page.View);
        Assert.Contains(page.GetVisualDescendants().OfType<MemoryView>(), v => v.IsVisible);
        Click(page.ViewRows.Single(r => r.Label == "Palettes"));
        Assert.Equal(DebuggerView.Palettes, page.View);
        Assert.Contains(page.GetVisualDescendants().OfType<PalettesView>(), v => v.IsVisible);
        // the Game tab goes back to Play Mode
        Click(Find<DocTab>("Doc_Game"));
        Assert.IsType<PlayModePage>(Win.CurrentPage);
    }
}

/// <summary>Budget Details numbers and suggestions (P13.07) and Build Report history (P13.08).</summary>
[Collection(EmulatorCollection.Name)]
public class BudgetAndHistoryTests
{
    [Fact]
    public void P13_07_PaletteSharingSuggestion_ForTheFixture()
    {
        var p = GameProject.Open(PlayTestSupport.ProjectPath);
        var suggestions = BudgetService.PaletteSuggestions(p, "Forest_01");
        // OBJ palettes go to the scene's sprites in spawn order: hero 0, slime 1, block 2, flag 3, coin 4
        Assert.Equal(["hero.png and slime.png could share OBJ palette 0 — saves one palette slot.", "block.png and flag.png could share OBJ palette 2 — saves one palette slot."], suggestions);
        // each pair really fits one palette: 15 colours + transparent
        foreach (var pair in new[] { new[] { "hero", "slime" }, ["block", "flag"] })
        {
            var colours = pair.SelectMany(n => AgbStudio.Build.Graphics.ColorImage.Load(Path.Combine(p.AssetsDir, "sprites", n + ".png")).Histogram().Keys).Distinct().Count();
            Assert.InRange(colours, 1, 15);
        }
        // a sprite is part of one suggestion at most
        var names = suggestions.SelectMany(s => s.Split(' ').Where(w => w.EndsWith(".png", StringComparison.Ordinal))).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [AvaloniaFact]
    public void P13_07_LiveBudget_FromTheRunningGame()
    {
        using var fx = new Fixture();
        var play = PlayTestSupport.Open(fx);
        PlayTestSupport.RunForest(play);
        var b = BudgetService.Of(fx.Services).Current();
        Assert.True(b.Live);
        Assert.Equal("Forest_01", b.Scene);
        Assert.Equal(["VRAM BG", "VRAM OBJ", "OAM", "BG palettes", "OBJ palettes"], b.Lines.Take(5).Select(l => l.Label));
        Assert.Equal($"{Inspector.Oam(play.Core!).Count} / 128", b.Line("OAM")!.Text);
        Assert.Matches(@"^Palette OBJ \d+$", b.Lines[5].Label);
        Assert.Matches(@"^\d+ / 16 colours$", b.Lines[5].Text);
        Assert.Matches(@"^\d+ KB / 32 MB$", b.Line("ROM")!.Text);
        Assert.Contains(b.Suggestions, s => s.Contains("could share OBJ palette"));
        Assert.True(b.AllGood);
        Assert.Same(BudgetService.Of(fx.Services), fx.Services.Get<BudgetService>());
        play.Dispose();
    }

    [AvaloniaFact]
    public void P13_08_BuildReportHistory_AcrossBuilds_IncludingFailures()
    {
        using var fx = new Fixture();
        PlayTestSupport.Open(fx);
        var history = BuildHistory.Of(fx.Services);
        var before = history.Entries().Count;
        var ok = PlayTestSupport.Wait(fx.Services.Build.BuildAsync(new BuildOptions { Configuration = BuildConfiguration.Debug }));
        Assert.True(ok.Success);
        Assert.Equal(Math.Min(20, before + 1), history.Entries().Count);
        Assert.Equal(ok.Report!.RomBytes, history.Entries()[0].RomBytes);
        // a failing build is recorded with its first error
        var script = Path.Combine(Path.GetDirectoryName(PlayTestSupport.ProjectPath)!, "scripts", "Owl.cs");
        var original = File.ReadAllText(script);
        try
        {
            File.WriteAllText(script, original.Replace("public int talks = 0;", "public int talks = ;"));
            var failed = PlayTestSupport.Wait(fx.Services.Build.BuildAsync(new BuildOptions { Configuration = BuildConfiguration.Debug }));
            Assert.False(failed.Success);
            var top = history.Entries()[0];
            Assert.True(top.Errors > 0);
            Assert.Contains("Owl.cs", top.FirstError);
            Assert.Equal(ok.Report!.RomBytes, history.Entries()[1].RomBytes);
        }
        finally
        {
            File.WriteAllText(script, original);
        }
    }
}
