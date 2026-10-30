using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.Sprite;
using AgbStudio.Editor.Windows.Palette;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace AgbStudio.Editor.Tests.Windows.Palette;

/// <summary>Headless E2E of the Palette Manager (T-P10) and the Palette Effect popover (T-O10): P11.33-P11.36.</summary>
public class PaletteManagerE2ETests : ArtWindowHarness
{
    PaletteManagerPage OpenFromToolsMenu()
    {
        Fx.Services.Commands.Execute("tools.paletteManager");
        Capture.Pump();
        Assert.Contains(("T-P10", "tools.paletteManager"), Fx.Services.Windows.Requests);
        return Assert.IsType<PaletteManagerPage>(Win.CurrentPage);
    }

    [AvaloniaFact]
    public void P11_33_BanksUsedByListsAndColourEditor()
    {
        var page = OpenFromToolsMenu();
        Assert.Equal("Palettes", Win.TitleBar.Document);
        Assert.Equal("BG 3 / 16 · OBJ 3 / 16", Find<Chip>("BankCountChip").Content);
        Assert.Equal(("Background (BG)", "3 / 16"), (Find<TreeItem>("Group_Bg").Label, Find<TreeItem>("Group_Bg").Meta));
        Assert.Equal(["BG 0 · Sky & hills", "BG 1 · Ground", "BG 2 · UI & text", "OBJ 0 · Hero", "OBJ 1 · Enemies", "OBJ 2 · Items"],
            All<TreeItem>().Where(t => t.Depth == 1).Select(t => t.Label));
        Assert.True(Find<TreeItem>("Bank_OBJ_0").IsSelected);
        Assert.Equal("9 / 16", Find<TreeItem>("Bank_OBJ_0").Meta);
        Assert.Equal("OBJ 0 · Hero", Find<TextBlock>("BankTitle").Text);
        Assert.Equal("used by hero.png, heart.png", Find<TextBlock>("BankUsedBy").Text);
        Assert.Equal("OBJ 0 · 9 / 16 colours", Find<StatusItem>("Status_Bank").Content);
        Assert.Equal("Palette RAM 1 KB", Find<StatusItem>("Status_Ram").Content);
        Assert.Equal(SwatchState.Transparent, Find<Swatch>("BankSwatch_0").State);
        Assert.Equal(SwatchState.Empty, Find<Swatch>("BankSwatch_10").State);

        // used-by of another bank, collapsing a group
        Click(Find<TreeItem>("Bank_OBJ_1"));
        Assert.Equal("OBJ 1 · Enemies", Find<TextBlock>("BankTitle").Text);
        Assert.Equal("used by slime.png", Find<TextBlock>("BankUsedBy").Text);
        Click(Find<TreeItem>("Group_Bg"));
        Assert.DoesNotContain(All<TreeItem>(), t => t.Name == "Bank_BG_0");
        Click(Find<TreeItem>("Group_Bg"));
        Assert.Contains(All<TreeItem>(), t => t.Name == "Bank_BG_0");

        // colour editor with the 15-bit note
        Click(Find<TreeItem>("Bank_OBJ_0"));
        Click(Find<Swatch>("BankSwatch_4"));
        Assert.Equal("Colour 4", Find<SectionHeader>("ColourHeader").Title);
        var bank = page.Bank!;
        Assert.Equal($"GBA colours are 15-bit (5 bits per channel), so {Colour15.Hex24(bank.Colors[4])} is stored as the nearest of 32 levels.", page.Colour.Note!.Text);
        Assert.Equal(Colour15.Hex15(Colour15.Of(bank.Colors[4])), page.Colour.Hex.Value2);
        page.Colour.SetChannel(2, 31);
        Capture.Pump();
        Assert.Equal(31, Colour15.B(Colour15.Of(bank.Colors[4])));
        Assert.Equal("31", page.Colour.Blue.Value);
        Assert.True(Fx.Services.Documents.Active!.IsDirty);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.NotEqual(31, Colour15.B(Colour15.Of(bank.Colors[4])));

        // palette RAM: the selected bank is outlined; clicking a cell selects that bank and index
        Assert.Equal(0, Find<PaletteRamView>("ObjRam").SelectedBank);
        var bg = Find<PaletteRamView>("BgRam");
        Click(bg, PaletteRamView.CellRect(1, 2).Center);
        Assert.Equal(("BG 1 · Ground", 2), (page.Bank!.Label, page.Index));
        Assert.Equal(1, bg.SelectedBank);

        // pipette: picks the clicked RAM colour into the selected index
        Click(Find<IconButton>("Tool_pipette"));
        Assert.True(page.PipetteMode);
        var obj = Find<PaletteRamView>("ObjRam");
        Click(obj, PaletteRamView.CellRect(0, 3).Center);
        Assert.Equal(Colour15.Of(Lib.Find(BankKind.Obj, 0)!.Colors[3]), Colour15.Of(page.Bank!.Colors[2]));
    }

    [AvaloniaFact]
    public void P11_34_ToolsMergeSortLockAndOptimize()
    {
        var page = OpenFromToolsMenu();
        var bank = page.Bank!;
        var yellow = Colour15.Of(bank.Colors[2]);
        bank.Colors.Add(Colour15.Argb(Colour15.Make(Colour15.R(yellow), Colour15.G(yellow) - 1, Colour15.B(yellow))));
        page.Refresh();
        Assert.Equal(11, bank.Colors.Count);
        Click(Find<CheckBox>("MergeNear"));
        Assert.True(bank.MergeNear);
        Assert.Equal(10, bank.Colors.Count);
        Assert.True(Find<CheckBox>("MergeNear").IsChecked);

        Click(Find<CheckBox>("SortByHue"));
        Assert.True(bank.SortByHue);
        Assert.Equal(Quantizer.SortByHue(bank.Colors.Skip(1).Select(Colour15.Of)), bank.Colors.Skip(1).Select(Colour15.Of));
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.False(bank.SortByHue);

        Assert.True(Find<CheckBox>("LockTransparent").IsChecked);
        Click(Find<CheckBox>("LockTransparent"));
        Assert.False(bank.LockTransparent);
        Assert.Equal(SwatchState.Default, Find<Swatch>("BankSwatch_0").State);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.True(bank.LockTransparent);

        // Assets › Optimize Palettes…
        Lib.Find(BankKind.Obj, 2)!.Colors.Add(0xFF0000FFu);
        Fx.Services.Commands.Execute("assets.optimizePalettes");
        Capture.Pump();
        Assert.IsType<PaletteManagerPage>(Win.CurrentPage);
        Assert.DoesNotContain(0xFF0000FFu, Lib.Find(BankKind.Obj, 2)!.Colors);
        Assert.Contains(Fx.Services.Toasts.Visible, t => t.Text.StartsWith("Palettes optimized"));
    }

    [AvaloniaFact]
    public void P11_35_ImportPalGplAndExport()
    {
        var page = OpenFromToolsMenu();
        var gpl = Path.Combine(Fx.Folder, "warm.gpl");
        File.WriteAllText(gpl, "GIMP Palette\nName: warm\nColumns: 16\n#\n255 0 255\tIndex\n255 0 0\tIndex\n255 128 0\tIndex\n255 255 0\tIndex\n");
        page.PickImportFile = () => Task.FromResult<string?>(gpl);
        Click(Find<AgbButton>("ImportPal"));
        var bank = page.Bank!;
        Assert.Equal(4, bank.Colors.Count);
        Assert.Equal(Colour15.Transparent, bank.Colors[0]);
        Assert.Equal(Colour15.Make(31, 0, 0), bank.Value(1));
        Assert.Equal("3 / 16", Find<TreeItem>("Bank_OBJ_0").Meta);
        // the hero art now uses the imported colours only
        var hero = Lib.Image(Sprite("hero"));
        Assert.All(hero.Pixels.Where(p => p != 0), p => Assert.Contains(Colour15.Of(p), bank.Colors.Skip(1).Select(Colour15.Of)));
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(10, bank.Colors.Count);

        var pal = Path.Combine(Fx.Folder, "hero.pal");
        page.PickExportFile = _ => Task.FromResult<string?>(pal);
        Click(Find<AgbButton>("ExportPal"));
        Assert.StartsWith("JASC-PAL", File.ReadAllText(pal));
        Assert.Equal(Enumerable.Range(0, 10).Select(bank.Value), PaletteFiles.Read(pal).Take(10));

        // a broken file is reported, the bank stays
        var bad = Path.Combine(Fx.Folder, "bad.pal");
        File.WriteAllText(bad, "not a palette");
        page.PickImportFile = () => Task.FromResult<string?>(bad);
        Click(Find<AgbButton>("ImportPal"));
        Assert.Equal(10, bank.Colors.Count);
        Assert.Contains(Fx.Services.Toasts.Visible, t => t.Status == ToastStatus.Error);
    }

    [AvaloniaFact]
    public void P11_36_PaletteEffectsPopoverAndPreview()
    {
        var page = OpenFromToolsMenu();
        Assert.Equal("to black · 30 frames", Find<InspectorField>("FadeField").Value);
        Assert.Equal("BG 1 · 6-9 · water", Find<InspectorField>("CycleField").Value);
        Assert.True(Find<InspectorField>("FlashField").IsOn);
        Assert.Equal(["100%", "66%", "33%"], new[] { 0, 1, 2 }.Select(i => Find<TextBlock>("FadePercent_" + i).Text));

        // Cycle opens the popover on the cycle
        ClickBox(Find<InspectorField>("CycleField"));
        Assert.Equal(("T-O10", "CycleField"), Fx.Services.Windows.Requests[^1]);
        var fx = Assert.IsType<PaletteEffectPopover>(Win.Overlays.Open.Single());
        Assert.Equal(380, fx.Bounds.Width);
        Assert.Equal(EffectKind.Cycle, fx.Kind);
        Assert.Equal("Colour cycle · water", fx.Title);
        Assert.Equal("BG 1 · Ground", fx.BankField.Value);
        Assert.Equal("RANGE · INDEX 6 TO 9", Find<TextBlock>("RangeLabel").Text);
        Assert.Equal([6, 7, 8, 9], Enumerable.Range(0, 12).Where(i => Find<Swatch>("Range_" + i).State == SwatchState.Selected));
        Assert.Equal("8\nframes", fx.Speed.Value);
        Assert.True(fx.DirectionSegments[1].IsChecked);

        // live preview: the range rotates right every 8 frames, as the runtime cycles it
        var ground = Lib.Find(BankKind.Bg, 1)!;
        var values = Enumerable.Range(0, ground.Colors.Count).Select(ground.Value).ToArray();
        Assert.Equal(PaletteEffects.Cycle(values, 6, 9, 8, CycleDirection.Right, fx.PreviewFrame)[6..10], fx.PreviewColours());
        var before = fx.PreviewColours().ToArray();
        fx.Advance(8);
        Assert.Equal(PaletteEffects.Cycle(values, 6, 9, 8, CycleDirection.Right, fx.PreviewFrame)[6..10], fx.PreviewColours());
        Assert.Equal(before[..3], fx.PreviewColours().Skip(1));
        Assert.Equal(4, All<Border>().Count(b => b.Name?.StartsWith("PreviewBlock_") == true));
        // the strip scrubs like a slider: halfway through the 4 x 8 frame cycle
        Assert.Equal(32, fx.PreviewPeriod);
        var strip = Find<Grid>("EffectPreview");
        var half = At(strip, new Point(strip.Bounds.Width / 2, 10));
        Win.MouseMove(half);
        Win.MouseDown(half, MouseButton.Left);
        Assert.Equal(16, fx.PreviewFrame);
        Assert.Equal(PaletteEffects.Cycle(values, 6, 9, 8, CycleDirection.Right, 16)[6..10], fx.PreviewColours());
        Win.MouseMove(At(strip, new Point(1, 10)));
        Assert.Equal(0, fx.PreviewFrame);
        Win.MouseUp(At(strip, new Point(1, 10)), MouseButton.Left);

        // range: click the first and the last index; direction; speed slider in frames
        Click(Find<Swatch>("Range_2"));
        Click(Find<Swatch>("Range_4"));
        Assert.Equal((2, 4), (Lib.Effects.Cycle.First, Lib.Effects.Cycle.Last));
        Click(fx.DirectionSegments[2]);
        Assert.Equal(CycleDirection.PingPong, Lib.Effects.Cycle.Direction);
        fx.Speed.SliderValue = 0;
        Capture.Pump();
        Assert.Equal(1, Lib.Effects.Cycle.Frames);
        Assert.Equal("1\nframe", fx.Speed.Value);
        Key(PhysicalKey.Escape);
        Assert.False(Win.Overlays.IsOpen);
        Assert.Equal("BG 1 · 2-4 · water", Find<InspectorField>("CycleField").Value);

        // Fade: speed in frames updates the fade preview; Flash in the same popover
        ClickBox(Find<InspectorField>("FadeField"));
        fx = Assert.IsType<PaletteEffectPopover>(Win.Overlays.Open.Single());
        Assert.Equal(EffectKind.Fade, fx.Kind);
        Assert.Equal("Fade · to black", fx.Title);
        Assert.False(fx.DirectionSegments[0].IsEnabled);
        fx.Speed.SliderValue = 100;
        Capture.Pump();
        Assert.Equal(60, Lib.Effects.Fade.Frames);
        Click(fx.EffectSegments[2]);
        Assert.Equal(EffectKind.Flash, fx.Kind);
        Assert.Equal("Flash on hit", fx.Title);
        fx.Advance(16 - fx.PreviewFrame % 16);
        Assert.All(fx.PreviewColours(), c => Assert.Equal((ushort)0x7FFF, c));
        fx.Advance(8);
        Assert.Equal(Enumerable.Range(1, 4).Select(Lib.Find(BankKind.Obj, 0)!.Value), fx.PreviewColours());
        Key(PhysicalKey.Escape);
        Assert.Equal("to black · 60 frames", Find<InspectorField>("FadeField").Value);

        // Flash on hit toggle, undo, and the effects are saved with the palettes
        Click(Find<InspectorField>("FlashField").GetVisualDescendant<AgbSwitch>("PART_Toggle"));
        Assert.False(Lib.Effects.Flash.Enabled);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.True(Lib.Effects.Flash.Enabled);
        Key(PhysicalKey.S, RawInputModifiers.Control);
        var file = GameProject.Load<PaletteLibraryFile>(Lib.FilePath);
        Assert.Equal(60, file.Effects.Fade.Frames);
        Assert.Equal((2, 4), (file.Effects.Cycle.First, file.Effects.Cycle.Last));
        Assert.False(Fx.Services.Documents.Active!.IsDirty);
    }

    [AvaloniaFact]
    public void CreatePalette_AndBanksPlus()
    {
        var page = OpenFromToolsMenu();
        Click(Find<PanelHeader>("BanksHeader").GetVisualDescendant<Button>("PART_Action"));
        Assert.Equal("OBJ 3 · New palette", page.Bank!.Label);
        Assert.Equal("BG 3 / 16 · OBJ 4 / 16", Find<Chip>("BankCountChip").Content);
        Fx.Services.Commands.Execute("assets.create.palette");
        Capture.Pump();
        var p2 = Assert.IsType<PaletteManagerPage>(Win.CurrentPage);
        Assert.Equal("OBJ 4 · New palette", p2.Bank!.Label);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(3, Lib.Of(BankKind.Obj).Count());
    }
}
