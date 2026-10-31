using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Palette;
using AgbStudio.Editor.Windows.Sprite;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Sprite;

/// <summary>Headless E2E of the Sprite Editor (T-P08), New Sprite (T-D13) and OAM Size (T-M19): P11.22-P11.27.</summary>
public class SpriteEditorE2ETests : ArtWindowHarness
{
    SpriteEditorPage OpenHero()
    {
        _ = Fx.Services.Windows.Open("T-P08", Sprite("hero"), source: "test");
        Capture.Pump();
        var page = Assert.IsType<SpriteEditorPage>(Win.CurrentPage);
        Assert.Equal("hero.png", page.Doc!.FileName);
        return page;
    }

    void ClickCell(SpriteEditorPage page, int x, int y) => Click(page.Canvas, page.Canvas.CellCenter(x, y));

    [AvaloniaFact]
    public void P11_22_Canvas_TileGridZoomToolsAndFrameIndex()
    {
        var page = OpenHero();
        var canvas = page.Canvas;
        var doc = page.Doc!;
        Assert.Equal("Sky Hopper", Win.TitleBar.Project);
        Assert.Equal("hero.png", Win.TitleBar.Document);
        Assert.Equal(26, canvas.Zoom);
        Assert.Equal("2600%", Find<Chip>("ZoomChip").Content);
        Assert.Equal(16 * 26, canvas.FrameRect.Width);
        Assert.True(canvas.ShowTileGrid);
        Assert.True(Find<IconButton>("Tool_grid").IsActive);
        // the six drawing tools of the Figma toolbar
        Assert.Equal(["pencil", "eraser", "bucket", "line", "square", "pipette"], All<IconButton>().Where(b => b.Name is { } n && n.StartsWith("Tool_") && n != "Tool_mirror" && n != "Tool_grid" && n != "Tool_onion").Select(b => b.Glyph));

        // pencil with colour 4 on a transparent pixel, then undo
        Click(Find<Swatch>("Swatch_4"));
        Assert.Equal(4, page.ColorIndex);
        var bank = doc.Bank!;
        Assert.Equal(0u, doc.Get(0, 0, 0));
        ClickCell(page, 0, 0);
        Assert.Equal(bank.Colors[4], doc.Get(0, 0, 0));
        Assert.True(Fx.Services.Documents.Active!.IsDirty);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(0u, doc.Get(0, 0, 0));

        // eraser
        var yellow = doc.Get(0, 6, 3);
        Assert.NotEqual(0u, yellow);
        Click(Find<IconButton>("Tool_eraser"));
        Assert.Equal(DrawTool.Eraser, page.Tool);
        ClickCell(page, 6, 3);
        Assert.Equal(0u, doc.Get(0, 6, 3));
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(yellow, doc.Get(0, 6, 3));

        // fill floods the connected transparent area only
        Click(Find<IconButton>("Tool_bucket"));
        Click(Find<Swatch>("Swatch_3"));
        Click(Find<IconButton>("Tool_bucket"));
        ClickCell(page, 0, 0);
        Assert.Equal(bank.Colors[3], doc.Get(0, 0, 0));
        Assert.Equal(bank.Colors[3], doc.Get(0, 15, 0));
        Assert.Equal(yellow, doc.Get(0, 6, 3));
        Key(PhysicalKey.Z, RawInputModifiers.Control);

        // line and rectangle by dragging
        Click(Find<IconButton>("Tool_line"));
        Drag(canvas, canvas.CellCenter(0, 0), canvas.CellCenter(3, 3));
        foreach (var i in Enumerable.Range(0, 4)) Assert.Equal(bank.Colors[3], doc.Get(0, i, i));
        Assert.Equal(0u, doc.Get(0, 1, 0));
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Click(Find<IconButton>("Tool_square"));
        Drag(canvas, canvas.CellCenter(0, 12), canvas.CellCenter(1, 15));
        Assert.Equal(bank.Colors[3], doc.Get(0, 0, 12));
        Assert.Equal(bank.Colors[3], doc.Get(0, 1, 15));
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(0u, doc.Get(0, 0, 12));

        // pipette picks the palette index under the cursor and returns to the pencil
        Click(Find<IconButton>("Tool_pipette"));
        ClickCell(page, 6, 3);
        Assert.Equal(ArtLibrary.IndexOf(bank, yellow), page.ColorIndex);
        Assert.Equal(DrawTool.Pencil, page.Tool);
        Assert.Equal($"index {page.ColorIndex} · {Colour15.Hex24(yellow)}", Find<Chip>("ColourChip").Content);
        Win.MouseMove(At(canvas, canvas.CellCenter(6, 3)));
        Capture.Pump();
        Assert.Equal("x 6  y 3", Find<Chip>("CursorChip").Content);

        // frame index: the third frame of "idle"
        Click(Find<FrameThumb>("Frame_2"));
        Assert.Equal(2, page.FramePosition);
        Assert.Equal(2, canvas.Frame);
        Assert.Equal("idle · frame 3 / 8", Find<Chip>("FrameChip").Content);

        // 1x / 2x / 4x previews on the scene backdrop
        Assert.Equal([1.0, 2.0, 4.0], new[] { "Preview_1x", "Preview_2x", "Preview_4x" }.Select(n => Find<PixelView>(n).Scale));
        Assert.Equal(24, Find<PixelView>("Preview_1x").Bounds.Width);
        Assert.Equal(72, Find<PixelView>("Preview_4x").Bounds.Width);

        // Ctrl+wheel zooms, the zoom chip fits again; the tile grid chip toggles the 8x8 grid
        Win.MouseWheel(At(canvas), new Vector(0, 1), RawInputModifiers.Control);
        Capture.Pump();
        Assert.True(canvas.Zoom > 26);
        Assert.Equal($"{canvas.Zoom * 100}%", Find<Chip>("ZoomChip").Content);
        Click(Find<Chip>("ZoomChip"));
        Assert.Equal(26, canvas.Zoom);
        Click(Find<Chip>("TileGridChip"));
        Assert.False(canvas.ShowTileGrid);
        Assert.False(Find<IconButton>("Tool_grid").IsActive);
    }

    [AvaloniaFact]
    public void P11_22_SaveWritesThePng()
    {
        var page = OpenHero();
        Click(Find<Swatch>("Swatch_5"));
        ClickCell(page, 0, 0);
        Key(PhysicalKey.S, RawInputModifiers.Control);
        var png = Png.Read(Sprite("hero"));
        Assert.Equal(Colour15.Of(page.Doc!.Bank!.Colors[5]), Colour15.Of(png.Pixels[0]));
        Assert.False(Fx.Services.Documents.Active!.IsDirty);
        Assert.Equal("Saved", Find<StatusItem>("Status_Saved").Content);
    }

    [AvaloniaFact]
    public void P11_23_PalettePanel_RgbHex15BitSlotsAndSharing()
    {
        var page = OpenHero();
        var doc = page.Doc!;
        var bank = doc.Bank!;
        Assert.Equal("OBJ 0 · Hero", Find<SectionHeader>("BankHeader").Title);
        Assert.Equal("16 colours", Find<SectionHeader>("BankHeader").Meta);
        Assert.Equal(SwatchState.Transparent, Find<Swatch>("Swatch_0").State);
        Assert.Equal(SwatchState.Empty, Find<Swatch>("Swatch_15").State);
        Assert.Equal(16, All<Swatch>().Count(s => s.Name!.StartsWith("Swatch_")));

        Click(Find<Swatch>("Swatch_4"));
        Assert.Equal(SwatchState.Selected, Find<Swatch>("Swatch_4").State);
        Assert.Equal("Colour 4", Find<SectionHeader>("ColourHeader").Title);
        var c = Colour15.Of(bank.Colors[4]);
        Assert.Equal(Colour15.R(c).ToString(), page.Colour.Red.Value);
        Assert.Equal(Colour15.Hex24(bank.Colors[4]), page.Colour.Hex.Value);
        Assert.Equal(Colour15.Hex15(c), page.Colour.Hex.Value2);

        // G slider to 0 (0-31 levels): every pixel of the bank's art that used colour 4 follows; one undo step
        var heroPixels = Enumerable.Range(0, 16 * 16).Count(i => Colour15.Of(doc.Get(0, i % 16, i / 16)) == c && !Colour15.IsTransparent(doc.Get(0, i % 16, i / 16)));
        Assert.True(heroPixels > 0);
        page.Colour.Green.SliderValue = 50;
        page.Colour.Green.SliderValue = 0;
        Capture.Pump();
        var edited = Colour15.Of(bank.Colors[4]);
        Assert.Equal(0, Colour15.G(edited));
        Assert.Equal("0", page.Colour.Green.Value);
        Assert.Equal(heroPixels, Enumerable.Range(0, 256).Count(i => Colour15.Of(doc.Get(0, i % 16, i / 16)) == edited && !Colour15.IsTransparent(doc.Get(0, i % 16, i / 16))));
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(c, Colour15.Of(bank.Colors[4]));

        // hex and 15-bit value typed
        page.Colour.Hex.Value = "#102030";
        Capture.Pump();
        Assert.Equal(0xFF102030u, bank.Colors[4]);
        page.Colour.Hex.Value2 = "0x7FFF";
        Capture.Pump();
        Assert.Equal((ushort)0x7FFF, Colour15.Of(bank.Colors[4]));

        // palette slots used and opening a slot in the Palette Manager
        Assert.Equal("3 / 16 used", Find<SectionHeader>("SlotsHeader").Meta);
        Assert.Equal(3, All<DockPanel>().Count(d => d.Name?.StartsWith("Slot_OBJ_") == true));
        Click(Find<DockPanel>("Slot_OBJ_1"));
        var pm = Assert.IsType<PaletteManagerPage>(Win.CurrentPage);
        Assert.Equal("OBJ 1 · Enemies", pm.Bank!.Label);
        Assert.Contains(("T-P10", "PaletteSlots"), Fx.Services.Windows.Requests);

        // share palette: hero joins OBJ 1 and its meta names the owner sprite (read by the build)
        page.SharePalette(Lib.Find(BankKind.Obj, 1)!);
        Assert.Equal("OBJ 1", doc.Bank!.Id);
        Assert.Equal("slime", doc.Meta.Palette);
        Assert.Equal("", Lib.Meta(Sprite("heart")).Palette);
        Lib.SaveAll();
        Assert.Equal("slime", GameProject.Load<SpriteMeta>(GameProject.MetaPath(Sprite("hero"))).Palette);
    }

    static string Sheet(string folder, string name, int w, int h, int colours)
    {
        var px = new uint[w * h];
        for (var i = 0; i < px.Length; i++)
        {
            var k = (uint)(i % colours);
            px[i] = i % 7 == 0 ? 0 : 0xFF000000u | ((k * 37 + 20) % 256 << 16) | ((k * 91 + 40) % 256 << 8) | ((k * 53 + 60) % 256);
        }
        var path = Path.Combine(folder, name);
        Png.WriteArgb(path, px, w, h);
        return path;
    }

    [AvaloniaFact]
    public void P11_24_Import_DropZoneAutoReducesAndSplitsSheet()
    {
        var page = OpenHero();
        Assert.Equal("Split sheet into 16×16 frames", Find<CheckBox>("SplitSheet").Content);
        Assert.Equal("Share palette with OBJ 0", Find<CheckBox>("SharePalette").Content);
        var src = Sheet(Fx.Folder, "walker.png", 32, 16, 24);
        Assert.Equal(24, Png.Read(src).Pixels.Where(p => p >> 24 != 0).Distinct().Count());

        // drop the file on the drop zone
        var zone = Find<Panel>("DropZone");
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(src));
        zone.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, zone, new Point(10, 10), KeyModifiers.None));
        Capture.Pump();
        // T-C13: the drop opens Import Asset with this page's options set on the file (reduce, 16 x 16 frames, hero's palette)
        Assert.Equal(("T-D04", "SpriteDropZone"), Fx.Services.Windows.Requests[^1]);
        var dlg = Assert.IsType<AgbStudio.Editor.Windows.Import.ImportAssetDialog>(Win.Overlays.Open[^1]);
        var item = Assert.Single(dlg.Items);
        Assert.True(item.IsReady, item.Error ?? item.Warning);
        Assert.Equal((16, 16, "hero"), (item.FrameWidth, item.FrameHeight, item.SharePalette));
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm"));
        Capture.Pump();
        Assert.False(Win.Overlays.IsOpen);

        var doc = page.Doc!;
        Assert.Equal("walker.png", doc.FileName);
        Assert.True(File.Exists(doc.Path));
        Assert.Equal(2, doc.FrameCount);
        Assert.Equal((16, 16), (doc.FrameW, doc.FrameH));
        // auto-reduce: Import Asset keeps the source and marks it for the build to reduce to the 15 colours of the palette
        var walkerMeta = GameProject.Load<SpriteMeta>(GameProject.MetaPath(doc.Path));
        Assert.True(walkerMeta.ReduceColors);
        Assert.Equal("OBJ 0", doc.Bank!.Id);
        Assert.Equal("hero", walkerMeta.Palette);
        Assert.Equal([0, 1], doc.Meta.Animations.Single().Frames);
        Assert.Contains(All<TreeItem>(), t => t.Label == "walker.png" && t.IsSelected);

        // without split and sharing: one frame, its own palette
        Click(Find<CheckBox>("SplitSheet"));
        Click(Find<CheckBox>("SharePalette"));
        page.ImportFiles([Sheet(Fx.Folder, "banner.png", 32, 16, 6)]);
        Assert.Equal(1, page.Doc!.FrameCount);
        Assert.Equal((32, 16), (page.Doc.FrameW, page.Doc.FrameH));
        Assert.NotEqual("OBJ 0", page.Doc.Bank!.Id);

        // an Aseprite file dropped here goes through Import Asset too and becomes a sprite of the project
        var ase = Path.Combine(Fx.Folder, "jelly.aseprite");
        File.Copy(Path.Combine(AgbStudio.Build.NativeBuild.RepoRoot, "tests", "AgbStudio.Tests", "Golden", "slime.aseprite"), ase, true);
        var drop = new DataTransfer();
        drop.Add(DataTransferItem.CreateText(ase));
        zone.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, drop, zone, new Point(10, 10), KeyModifiers.None));
        Capture.Pump();
        dlg = Assert.IsType<AgbStudio.Editor.Windows.Import.ImportAssetDialog>(Win.Overlays.Open[^1]);
        Assert.Equal("Sprite sheet", Assert.Single(dlg.Items).Detected);
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm"));
        Capture.Pump();
        Assert.Contains(Fx.Services.Project.Current!.SpriteFiles(), s => Path.GetFileName(s) == "jelly.aseprite");

        // a file that is not an image is reported in Import Asset (the page's own import refuses it with a toast)
        var bad = Path.Combine(Fx.Folder, "sheet.aseprite");
        File.WriteAllText(bad, "");
        page.ImportFiles([bad]);
        Assert.Contains(Fx.Services.Toasts.Visible, t => t.Status == ToastStatus.Error && t.Text.Contains("sheet.aseprite"));
    }

    [AvaloniaFact]
    public void P11_25_SpriteSettings_OamSizeOriginColliderMirrorVram()
    {
        var page = OpenHero();
        var doc = page.Doc!;
        Assert.Equal("Square 16×16", Find<InspectorField>("OamField").Value);
        Assert.Equal("4 tiles · 128 B", Find<InspectorField>("VramField").Value);
        Assert.Equal(("8", "16"), (Find<InspectorField>("OriginField").Value, Find<InspectorField>("OriginField").Value2));
        Assert.Equal(("12 × 14", "2, 2"), (Find<InspectorField>("ColliderField").Value, Find<InspectorField>("ColliderField").Value2));

        // the OAM size field opens T-M19; its entries are the Figma ones with the current size checked
        ClickBox(Find<InspectorField>("OamField"));
        Assert.Equal(("T-M19", "OamField"), Fx.Services.Windows.Requests[^1]);
        var entries = OamSizeMenu.For(doc);
        Assert.Equal(["Square 8×8", "Square 16×16", "Square 32×32", "Square 64×64", "Wide 16×8 · 32×8 · 32×16 · 64×32", "Tall 8×16 · 8×32 · 16×32 · 32×64"],
            entries.OfType<RecentEntry>().Select(e => e.Name));
        Assert.IsType<SeparatorEntry>(entries[4]);
        Assert.Equal(["Square 16×16"], entries.OfType<RecentEntry>().Where(e => e.IsCurrent).Select(e => e.Name));

        entries.OfType<RecentEntry>().Single(e => e.Name == "Square 32×32").Open.Execute(null);
        Capture.Pump();
        Assert.Equal((32, 32, 8), (doc.FrameW, doc.FrameH, doc.FrameCount));
        Assert.Equal(256, doc.Image.Width);
        Assert.Equal("Square 32×32", Find<InspectorField>("OamField").Value);
        Assert.Equal("16 tiles · 512 B", Find<InspectorField>("VramField").Value);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal((16, 16, 128), (doc.FrameW, doc.FrameH, doc.Image.Width));

        // Wide / Tall reach every hardware size of the shape
        OamSizeMenu.For(doc).OfType<RecentEntry>().Single(e => e.Name.StartsWith("Wide")).Open.Execute(null);
        Assert.Equal((32, 16), (doc.FrameW, doc.FrameH));
        Assert.True(OamSizeMenu.For(doc).OfType<RecentEntry>().Single(e => e.Name.StartsWith("Wide")).IsCurrent);
        OamSizeMenu.For(doc).OfType<RecentEntry>().Single(e => e.Name.StartsWith("Wide")).Open.Execute(null);
        Assert.Equal((64, 32), (doc.FrameW, doc.FrameH));
        Assert.Equal((16, 32), OamSizeMenu.Next(OamSizeMenu.Tall, 16, 16));
        Assert.All(OamSizeMenu.Square.Concat(OamSizeMenu.Wide).Concat(OamSizeMenu.Tall), s => Assert.True(OamSizeMenu.IsHardware(s.W, s.H)));
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Key(PhysicalKey.Z, RawInputModifiers.Control);
        Capture.Pump();

        // origin and collider are edited in place and saved into the meta
        Find<InspectorField>("OriginField").Value = "4";
        Find<InspectorField>("ColliderField").Value = "10 × 12";
        Find<InspectorField>("ColliderField").Value2 = "3, 4";
        Capture.Pump();
        Assert.Equal((4, 16), (doc.Meta.OriginX, doc.Meta.OriginY));
        Assert.Equal((3, 4, 10, 12), (doc.Meta.Collider.X, doc.Meta.Collider.Y, doc.Meta.Collider.W, doc.Meta.Collider.H));
        Key(PhysicalKey.S, RawInputModifiers.Control);
        var saved = GameProject.Load<SpriteMeta>(GameProject.MetaPath(doc.Path));
        Assert.Equal((4, 10, 12), (saved.OriginX, saved.Collider.W, saved.Collider.H));

        // Mirror X: the canvas shows the OAM horizontal flip and drawing lands on the mirrored pixel
        Click(Find<Chip>("MirrorChip"));
        Assert.True(page.Canvas.Mirror);
        Assert.Equal(ChipStyle.Accent, Find<Chip>("MirrorChip").Style);
        Assert.True(Find<IconButton>("Tool_mirror").IsActive);
        Click(Find<Swatch>("Swatch_3"));
        ClickCell(page, 0, 0);
        Assert.Equal(doc.Bank!.Colors[3], doc.Get(0, 15, 0));
        Assert.Equal(0u, doc.Get(0, 0, 0));
    }

    [AvaloniaFact]
    public void P11_26_Timeline_AnimationsTicksLoopPingPongOnionSkin()
    {
        var page = OpenHero();
        var doc = page.Doc!;
        Assert.Equal(["8f · loop", "6f · loop", "3f", "2f"], new[] { "idle", "run", "jump", "hurt" }.Select(n => Find<ListRow>("Anim_" + n).Meta));
        Assert.Equal("6 ticks ≈ 10 fps", Find<TextBox>("TicksField").Text);
        Assert.Equal("6 ticks ≈ 10 fps", SpriteEditorPage.TicksText(6));
        Assert.Equal("8 ticks ≈ 7.5 fps", SpriteEditorPage.TicksText(8));
        Assert.Equal(8, All<FrameThumb>().Count());
        Assert.Equal("1 · 6t", Find<FrameThumb>("Frame_0").Label);

        Click(Find<ListRow>("Anim_run"));
        Assert.Equal(1, page.AnimationIndex);
        Assert.Equal(6, All<FrameThumb>().Count());
        Assert.Equal("run · frame 1 / 6", Find<Chip>("FrameChip").Content);

        // ticks per frame typed in the speed field
        var ticks = Find<TextBox>("TicksField");
        ticks.Focus();
        ticks.Text = "8";
        Key(PhysicalKey.Enter);
        Assert.Equal(8, doc.Meta.Animations[1].Ticks);
        Assert.Equal("8 ticks ≈ 7.5 fps", ticks.Text);
        Assert.Equal("1 · 8t", Find<FrameThumb>("Frame_0").Label);

        // play / pause, then 8 ticks per frame at 60 frames per second; step
        Click(Find<IconButton>("AnimPlay"));
        Assert.True(page.IsPlaying);
        Assert.Equal("pause", Find<IconButton>("AnimPlay").Glyph);
        Click(Find<IconButton>("AnimPlay"));
        Assert.False(page.IsPlaying);
        page.SelectFrame(0);
        page.Tick(7);
        Assert.Equal(0, page.FramePosition);
        page.Tick(1);
        Assert.Equal(1, page.FramePosition);
        Click(Find<IconButton>("AnimStep"));
        Assert.Equal(2, page.FramePosition);

        // loop: double-click the animation; a non-looping animation stops on its last frame
        Click(Find<ListRow>("Anim_jump"));
        Assert.False(doc.Meta.Animations[2].Loop);
        page.Tick(6 * 5);
        Assert.Equal(2, page.FramePosition);
        Thread.Sleep(600);
        Click(Find<ListRow>("Anim_jump"), clicks: 2);
        Assert.True(doc.Meta.Animations[2].Loop);
        Assert.Equal("3f · loop", Find<ListRow>("Anim_jump").Meta);

        // ping-pong
        Click(Find<ListRow>("Anim_idle"));
        Click(Find<Chip>("PingPongChip"));
        Assert.True(doc.Meta.Animations[0].PingPong);
        Assert.Equal(ChipStyle.Accent, Find<Chip>("PingPongChip").Style);
        var seen = new List<int>();
        for (var i = 0; i < 9; i++) { page.Tick(6); seen.Add(page.FramePosition); }
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 6, 5], seen);

        // onion skin draws the previous frame of the animation under the current one
        Click(Find<Chip>("OnionChip"));
        Assert.True(page.OnionSkin);
        Assert.Equal(ChipStyle.Accent, Find<Chip>("OnionChip").Style);
        Assert.True(Find<IconButton>("Tool_onion").IsActive);
        Assert.Equal(doc.Meta.Animations[0].Frames[page.FramePosition - 1], page.Canvas.OnionFrame);
        Key(PhysicalKey.S, RawInputModifiers.Control);
        var saved = GameProject.Load<SpriteMeta>(GameProject.MetaPath(doc.Path));
        Assert.True(saved.Animations[0].PingPong);
        Assert.Equal(8, saved.Animations[1].Ticks);
    }

    [AvaloniaFact]
    public async Task P11_27_NewSprite_FromSpritesPlus()
    {
        var page = OpenHero();
        Click(Find<PanelHeader>("SpritesHeader").GetVisualDescendant<Button>("PART_Action"));
        var dlg = Assert.IsType<NewSpriteDialog>(Win.Overlays.Open.Single());
        Assert.Equal(520, dlg.Bounds.Width);
        Assert.Equal("new_sprite", dlg.NameField.Text);
        Assert.Equal("16 × 16", dlg.SizeField.Selected);
        Assert.Equal("GBA sizes: 8 to 64 px", dlg.SizeField.Help);
        Assert.Equal("8", dlg.FramesField.Text);
        Assert.Equal("OBJ 0 · Hero", dlg.PaletteField.Selected);
        Assert.Equal("Shares 16 colours with hero.png", dlg.PaletteField.Help);
        Assert.Equal(["Blank", "Copy of hero.png", "Import PNG…"], dlg.StartSegments.Select(s => (string)s.Content!));
        Assert.True(dlg.CreateIdle.IsChecked);
        Assert.Equal("Add a 12 × 12 collider", dlg.AddCollider.Content);
        Assert.Equal(12, dlg.SizeField.Options!.Count);
        Assert.All(NewSpriteDialog.Sizes, s => Assert.True(NewSpriteDialog.IsGbaSize(s.W, s.H)));
        Assert.Equal([.. Lib.Of(BankKind.Obj).Select(b => b.Label), "New palette"], dlg.PaletteField.Options);

        dlg.NameField.Text = "enemy_bat";
        dlg.SizeField.Selected = "32 × 32";
        dlg.FramesField.Text = "4";
        dlg.PaletteField.Selected = "OBJ 1 · Enemies";
        Assert.Equal("Shares 16 colours with slime.png", dlg.PaletteField.Help);
        Assert.Equal("Add a 28 × 28 collider", dlg.AddCollider.Content);
        Click(dlg.AddCollider);
        Click(dlg.StartSegments[1]);
        Assert.Equal(NewSpriteDialog.StartFrom.Copy, dlg.Start);
        Click(dlg.Footer.GetVisualDescendant<Button>("PART_Confirm"));
        Assert.False(Win.Overlays.IsOpen);

        var path = Path.Combine(Lib.Project.AssetsDir, "sprites", "enemy_bat.png");
        Assert.True(File.Exists(path));
        var png = Png.Read(path);
        Assert.Equal((128, 32), (png.Width, png.Height));
        var meta = GameProject.Load<SpriteMeta>(GameProject.MetaPath(path));
        Assert.Equal((32, 32), (meta.FrameWidth, meta.FrameHeight));
        Assert.Equal("idle", meta.Animations.Single().Name);
        Assert.Equal([0, 1, 2, 3], meta.Animations.Single().Frames);
        Assert.Equal((28, 28), (meta.Collider.W, meta.Collider.H));
        Assert.Equal("slime", meta.Palette);
        // the copy of hero.png: the hero art in the top-left 16x16 of each frame
        var hero = new SpriteDoc(Lib, Sprite("hero"));
        Assert.Equal(hero.Get(0, 6, 3), png.Pixels[3 * 128 + 6]);
        await Task.Yield();
        Assert.Equal("enemy_bat.png", page.Doc!.FileName);
        Assert.Contains(Fx.Services.Documents.Open, d => d.Name == "enemy_bat.png");

        // blank, own palette, no animation, imported PNG
        var src = Sheet(Fx.Folder, "imp.png", 32, 8, 5);
        var dlg2 = new NewSpriteDialog(Lib, Sprite("hero")) { PickImport = () => Task.FromResult<string?>(src) };
        dlg2.NameField.Text = "imported";
        dlg2.SizeField.Selected = "16 × 8";
        dlg2.FramesField.Text = "2";
        dlg2.PaletteField.Selected = NewSpriteDialog.NewPalette;
        dlg2.CreateIdle.IsChecked = false;
        dlg2.SetStart(NewSpriteDialog.StartFrom.Import);
        await Task.Delay(1);
        Assert.Equal(src, dlg2.ImportPath);
        var created = dlg2.Create()!;
        var png2 = Png.Read(created);
        Assert.Equal((32, 8), (png2.Width, png2.Height));
        Assert.Equal(Png.Read(src).Pixels[1], png2.Pixels[1]);
        Assert.Empty(GameProject.Load<SpriteMeta>(GameProject.MetaPath(created)).Animations);
        Assert.NotNull(Lib.BankOf(created));
        Assert.DoesNotContain(Lib.BankOf(created)!.Assets, a => a.EndsWith("hero.png"));
    }

    [AvaloniaFact]
    public async Task CreateSprite_CommandOpensEditorAndDialog()
    {
        Fx.Services.Commands.Execute("assets.create.sprite");
        await Task.Yield();
        Capture.Pump();
        Assert.IsType<SpriteEditorPage>(Win.CurrentPage);
        Assert.IsType<NewSpriteDialog>(Win.Overlays.Open.Single());
        Assert.Contains(("T-D13", "assets.create.sprite"), Fx.Services.Windows.Requests);
        Key(PhysicalKey.Escape);
        Assert.False(Win.Overlays.IsOpen);
        // a sprite document tab opens the Sprite Editor
        Click(Find<DocTab>("Doc_Scene"));
        Assert.IsType<SceneWorkspace>(Win.CurrentPage);
    }
}

static class VisualFind
{
    public static T GetVisualDescendant<T>(this Visual v, string name) where T : Control =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>().First(c => c.Name == name);
}
