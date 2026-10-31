using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Build;
using AgbStudio.Editor.Windows.Audio;
using AgbStudio.Editor.Windows.Import;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Import;

/// <summary>Type detection and conversion of Import Asset (P11.64).</summary>
public class AssetImporterTests
{
    [AvaloniaFact]
    public void Detects_EachTypeWithItsOptionsAndProblems()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        var files = GroupFixture.ImportFiles(fx, p);
        var items = files.Select(f => AssetImporter.Inspect(f, p)).ToList();
        Assert.Equal(["Sprite sheet", "Tilemap (Tiled)", "Sound effect"], items.Select(i => i.Detected));
        Assert.All(items, i => Assert.True(i.IsReady));
        var run = items[0];
        Assert.Equal((16, 16), (run.FrameWidth, run.FrameHeight));
        Assert.Equal((6, 1), (run.FramesFound, run.FrameRows));
        Assert.Equal("hero", run.SharePalette);
        Assert.Equal("run", run.AnimationName);
        Assert.Equal("Ready · 40 × 20 tiles, 2 layers.", items[1].Ready);
        Assert.Equal("Ready · resampled to 11 kHz mono 8-bit (9 KB).", items[2].Ready);

        var problems = GroupFixture.ProblemFiles(fx, p).Select(f => AssetImporter.Inspect(f, p)).ToList();
        Assert.Equal("Uses 48 colours. A GBA sprite palette holds 16, so it needs reducing or splitting.", problems[0].Warning);
        Assert.Equal(["Reduce to 16", "Split into 4 palettes"], problems[0].Fixes);
        Assert.Equal("MP3 is not supported on GBA. Use .wav for sound effects or .mod / .xm for music.", problems[1].Error);
        Assert.Equal(["How to convert"], problems[1].Fixes);
        Assert.True(problems[2].IsReady);
        var junk = Path.Combine(fx.Folder, "notes.xyz");
        File.WriteAllText(junk, "x");
        Assert.Equal("AGB Studio can't import .xyz files. Save it as one of the kinds it imports: .png, .aseprite, .tmx, .wav, .mod, .xm, .pal, .gpl, .ttf, .otf, .bdf or .cs.", AssetImporter.Inspect(junk, p).Error);
        Assert.Equal((32, 32), AssetImporter.GuessFrame(64, 32));
        Assert.Equal((16, 16), AssetImporter.GuessFrame(96, 16));
    }

    [AvaloniaFact]
    public void Import_ConvertsIntoTheProjectFolders()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        var items = GroupFixture.ImportFiles(fx, p).Select(f => AssetImporter.Inspect(f, p)).ToList();
        foreach (var i in items) AssetImporter.Import(p, i);
        var meta = p.LoadMeta<SpriteMeta>(Path.Combine(p.AssetsDir, "sprites", "hero_run.png"));
        Assert.Equal(("hero", 16, 16, true), (meta.Palette, meta.FrameWidth, meta.FrameHeight, meta.TrimEmptyFrames));
        Assert.Equal("run", meta.Animations.Single().Name);
        Assert.Equal([0, 1, 2, 3, 4, 5], meta.Animations[0].Frames);
        var map = GameProject.Load<MapFile>(Path.Combine(p.MapsDir, "forest2.map"));
        Assert.Equal(("forest2_tiles", 40, 20, 2), (map.Tileset, map.Width, map.Height, map.Layers.Count));
        Assert.Equal(800, map.Layers[0].Cells.Count);
        Assert.Equal(2, map.Layers[0].Cells[15 * 40]);   // gid 3 with firstgid 1
        Assert.True(File.Exists(Path.Combine(p.AssetsDir, "tilesets", "forest2_tiles.png")));
        var wav = WavData.Load(Path.Combine(p.AssetsDir, "audio", "sfx", "jump2.wav"));
        Assert.Equal((11025, 8, 1), (wav.Rate, wav.Bits, wav.Channels));
        Assert.Contains(p.SoundFiles(), f => f.EndsWith("jump2.wav", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Fixes_ReduceOrSplitIntoLayeredSprites()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        var big = AssetImporter.Inspect(GroupFixture.ProblemFiles(fx, p)[0], p);
        Assert.False(big.IsReady);
        AssetImporter.ApplyFix(big, "Split into 4 palettes");
        Assert.True(big.IsReady);
        var written = AssetImporter.Import(p, big);
        var layers = written.Where(f => f.EndsWith(".png", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, layers.Count);
        var colours = layers.Select(l => Png.Read(l).Pixels.Where(px => px >> 24 != 0).Distinct().Count()).ToList();
        Assert.All(colours, c => Assert.InRange(c, 1, 15));
        Assert.Equal(48, colours.Sum());
        var reduce = AssetImporter.Inspect(GroupFixture.ProblemFiles(fx, p)[0], p);
        AssetImporter.ApplyFix(reduce, "Reduce to 16");
        AssetImporter.Import(p, reduce);
        Assert.True(p.LoadMeta<SpriteMeta>(Path.Combine(p.AssetsDir, "sprites", "hero_big.png")).ReduceColors);
    }
}

/// <summary>Import Asset and Import Asset · Errors E2E (P11.64).</summary>
public class ImportDialogTests : IDisposable
{
    readonly Fixture _fx = new();
    readonly GameProject _p;
    readonly MainWindow _win;

    public ImportDialogTests()
    {
        _p = GroupFixture.SkyHopper(_fx);
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        _win.Close();
        _fx.Dispose();
    }

    void Click(Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), _win)!.Value;
        _win.MouseMove(p);
        _win.MouseDown(p, MouseButton.Left);
        _win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    T In<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [AvaloniaFact]
    public void FileImportAsset_ListsFilesShowsOptionsAndImports()
    {
        _fx.Services.Commands.Execute("file.importAsset");
        Capture.Pump();
        var d = Assert.IsType<ImportAssetDialog>(_win.Overlays.Open.Single());
        d.Add(GroupFixture.ImportFiles(_fx, _p));
        Capture.Pump();
        Assert.Equal(["hero_run.png", "forest2.tmx", "jump2.wav"], d.Items.Select(i => i.FileName));
        Assert.Equal("ready", In<Chip>(d, "State_hero_run.png").Content);
        Assert.Equal("16 × 16", In<InspectorField>(d, "Opt_FrameSize").Value);
        Assert.Equal("6 · 1 row", In<InspectorField>(d, "Opt_Frames").Value);
        Assert.StartsWith("OBJ ", In<InspectorField>(d, "Opt_Palette").Value);
        Assert.EndsWith("share with hero", In<InspectorField>(d, "Opt_Palette").Value);
        Assert.Equal("Create animation “run”", In<CheckBox>(d, "Opt_Animation").Content);
        // trimming off counts every frame; selecting another file shows its options
        In<CheckBox>(d, "Opt_Trim").IsChecked = false;
        Capture.Pump();
        Assert.False(d.Items[0].TrimEmptyFrames);
        Click(d.GetVisualDescendants().OfType<TableCell>().First(c => c.Text == "jump2.wav"));
        Assert.Equal("jump2.wav", d.Selected!.FileName);
        Assert.Equal("11 kHz mono 8-bit", In<InspectorField>(d, "Opt_Detail").Value);
        Click(In<AgbButton>(d, "PART_Confirm"));
        // the files are converted off the UI thread; the dialog closes when they are in
        GroupFixture.PumpUntil(() => !_win.Overlays.IsOpen, 30_000);
        Assert.False(_win.Overlays.IsOpen);
        Assert.True(File.Exists(Path.Combine(_p.AssetsDir, "sprites", "hero_run.png")));
        Assert.True(File.Exists(Path.Combine(_p.MapsDir, "forest2.map")));
        Assert.True(File.Exists(Path.Combine(_p.AssetsDir, "audio", "sfx", "jump2.wav")));
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text == "Imported 3 files");
    }

    [AvaloniaFact]
    public void Problems_OpenTheErrorsDialog_FixesAndSkipsErrors()
    {
        _ = _fx.Services.Windows.Open("T-D04", GroupFixture.ProblemFiles(_fx, _p), source: "test");
        Capture.Pump();
        var d = Assert.IsType<ImportAssetDialog>(_win.Overlays.Open.Single());
        Assert.Equal("needs fix", In<Chip>(d, "State_hero_big.png").Content);
        Assert.Equal("error", In<Chip>(d, "State_theme.mp3").Content);
        Click(In<AgbButton>(d, "PART_Confirm"));
        var errors = Assert.IsType<ImportErrorsDialog>(_win.Overlays.Open.Single());
        Assert.Equal(("T-D24", "ImportAssetErrors"), _fx.Services.Windows.Requests[^1]);
        Assert.Equal("Import Asset · 2 problems", errors.Title);
        Assert.Equal("Import 1 file", errors.ConfirmText);
        Click(In<AgbButton>(errors, "Fix_Howtoconvert"));
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text.StartsWith("Open theme.mp3 in an audio editor", StringComparison.Ordinal));
        Click(In<AgbButton>(errors, "Fix_Reduceto16"));
        Assert.Equal("Import Asset · 1 problem", errors.Title);
        Assert.Equal("Import 2 files", errors.ConfirmText);
        Click(In<AgbButton>(errors, "PART_Confirm"));
        GroupFixture.PumpUntil(() => !_win.Overlays.IsOpen, 30_000);
        Assert.False(_win.Overlays.IsOpen);
        Assert.True(File.Exists(Path.Combine(_p.AssetsDir, "sprites", "hero_big.png")));
        Assert.False(File.Exists(Path.Combine(_p.AssetsDir, "audio", "music", "theme.mp3")));
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text == "Imported 2 files");
    }
}

/// <summary>
/// G-ERR.04: an import can be cancelled and leaves the project consistent. File › Import Asset with 41 sound effects
/// (one replaces a sound of the project), Cancel while it converts the 21st: no file of the import is left, the replaced
/// sound is back, the Assets panel lists what it listed; Import again brings all 41 in.
/// </summary>
public sealed class ImportCancelTests : IDisposable
{
    readonly AgbStudio.Editor.Tests.Windows.SceneEditor.SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    static Button Part(Control d, string name) => d.GetVisualDescendants().OfType<Button>().First(b => b.Name == name);

    /// <summary>Every file of the project folder with its content hash.</summary>
    static SortedDictionary<string, string> Snapshot(string root) =>
        new(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            f => Path.GetRelativePath(root, f), f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)))), StringComparer.OrdinalIgnoreCase);

    static string[] Cards(AgbStudio.Editor.Windows.SceneEditor.AssetsPanel panel) => panel.Cards.Select(c => c.NameText ?? "").ToArray();
    static string[] Counts(AgbStudio.Editor.Windows.SceneEditor.AssetsPanel panel) => panel.CategoryRows.Select(r => $"{r.Label} {r.Meta}").ToArray();

    [AvaloniaFact]
    public void Cancel_DuringAManyFileImport_LeavesTheProjectAndTheAssetsPanelAsTheyWere_AndImportAgainWorks()
    {
        var p = _w.S.Project.Current!;
        var panel = _w.All<AgbStudio.Editor.Windows.SceneEditor.AssetsPanel>().Single();
        _w.Click(_w.Find<ListRow>("Category_Audio"));
        var cardsBefore = Cards(panel);
        var countsBefore = Counts(panel);
        var audioBefore = p.MusicFiles().Count() + p.SoundFiles().Count();
        var incoming = Directory.CreateDirectory(Path.Combine(_w.Fx.Fx.Folder, "incoming-many")).FullName;
        var replaced = p.SoundFiles().First();
        var files = new List<string> { Path.Combine(incoming, Path.GetFileName(replaced)) };
        File.WriteAllBytes(files[0], AgbStudio.Build.Audio.WavWriter.Encode(AgbStudio.Build.Audio.WavWriter.Tone(330, 0.2, 11025, true), 11025));
        for (var i = 0; i < 40; i++)
        {
            files.Add(Path.Combine(incoming, $"step_{i:00}.wav"));
            File.WriteAllBytes(files[^1], AgbStudio.Build.Audio.WavWriter.Encode(AgbStudio.Build.Audio.WavWriter.Tone(200 + 10 * i, 0.25, 11025, true), 11025));
        }
        var before = Snapshot(p.Root);
        var sfx = Path.Combine(p.AssetsDir, "audio", "sfx");

        _w.S.Commands.Execute("file.importAsset");
        Capture.Pump();
        var d = Assert.IsType<ImportAssetDialog>(_w.Win.Overlays.Open.Single());
        d.Add(files);
        Capture.Pump();
        Assert.Equal(41, d.Items.Count);
        Assert.All(d.Items, i => Assert.True(i.IsReady, i.FileName));
        // the import is held before its 21st file, as a long import would be there
        var held = new TaskCompletionSource();
        d.BeforeFile = async (i, ct) =>
        {
            if (i < 20) return;
            held.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        };
        _w.Click(Part(d, "PART_Confirm"));
        GroupFixture.PumpUntil(() => held.Task.IsCompleted && d.Hint == "Importing 21 of 41: step_19.wav", 30_000);
        Assert.True(d.IsImporting);
        Assert.Equal("Importing 21 of 41: step_19.wav", d.Hint);
        Assert.False(Part(d, "PART_Confirm").IsEnabled);
        // 20 files are in the project already, one of them over a sound it had
        Assert.True(File.Exists(Path.Combine(sfx, "step_18.wav")));
        Assert.False(File.Exists(Path.Combine(sfx, "step_19.wav")));
        Assert.NotEqual(before[Path.GetRelativePath(p.Root, replaced)], Snapshot(p.Root)[Path.GetRelativePath(p.Root, replaced)]);

        _w.Click(Part(d, "PART_Cancel"));
        GroupFixture.PumpUntil(() => !d.IsImporting, 30_000);
        Assert.False(d.IsImporting);
        // no file of the import is left and the replaced sound is back: the project folder is as it was
        Assert.Equal(before, Snapshot(p.Root));
        // the dialog stays open with its files, says so, and Import is enabled again
        Assert.Same(d, _w.Win.Overlays.Open.Single());
        Assert.Equal(41, d.Items.Count);
        Assert.Equal("Import cancelled. Press Import to try again.", d.Hint);
        Assert.True(Part(d, "PART_Confirm").IsEnabled);
        Assert.Contains(_w.S.Toasts.Visible, t => t.Text == "Import cancelled. Nothing was added to the project.");
        Assert.Contains(AgbStudio.Editor.Windows.SceneEditor.ConsoleLog.Of(_w.S).Entries, e => e.Level == ConsoleLevel.Warning && e.Text == "Import cancelled. Nothing was added to the project.");
        Assert.DoesNotContain(_w.S.Toasts.Visible, t => t.Text.StartsWith("Imported", StringComparison.Ordinal));
        // the Assets panel, read again from the disk, lists what it listed
        _w.Click(_w.Find<ListRow>("Category_Audio"));
        Assert.Equal(cardsBefore, Cards(panel));
        Assert.Equal(countsBefore, Counts(panel));

        // Import again: all 41 files come in and the Assets panel lists them
        d.BeforeFile = null;
        _w.Click(Part(d, "PART_Confirm"));
        GroupFixture.PumpUntil(() => !_w.Win.Overlays.IsOpen, 30_000);
        Assert.False(_w.Win.Overlays.IsOpen);
        Assert.Contains(_w.S.Toasts.Visible, t => t.Text == "Imported 41 files");
        Assert.All(Enumerable.Range(0, 40), i =>
        {
            Assert.True(File.Exists(Path.Combine(sfx, $"step_{i:00}.wav")));
            Assert.True(File.Exists(GameProject.MetaPath(Path.Combine(sfx, $"step_{i:00}.wav"))));
        });
        var wav = WavData.Load(replaced);
        Assert.Equal((11025, 8, 1), (wav.Rate, wav.Bits, wav.Channels));
        Assert.Equal(audioBefore + 40, p.MusicFiles().Count() + p.SoundFiles().Count());
        Assert.Contains($"Audio {audioBefore + 40}", Counts(panel));
        Assert.Contains("step_00.wav", Cards(panel));
    }
}
