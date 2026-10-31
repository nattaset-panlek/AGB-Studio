using AgbStudio.Build.Audio;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Audio;
using AgbStudio.Editor.Windows.Build;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AgbStudio.Editor.Tests.Windows.Build;

/// <summary>Test data for the Audio, Import and Build windows: the full Sky Hopper content, build reports, import files, fakes.</summary>
public static class GroupFixture
{
    /// <summary>Creates the full Sky Hopper fixture project (AgbStudio.Build.Content, with its music, sounds and .sfx files) and opens it.</summary>
    public static GameProject SkyHopper(Fixture fx)
    {
        var dir = Path.Combine(fx.Folder, "SkyHopper");
        AgbStudio.Build.Content.SkyHopper.Create(dir);
        var p = fx.Services.Project.Open(dir);
        fx.Services.Status.Build = "Build OK · 0.8 s";
        return p;
    }

    /// <summary>A report.json and history like the last builds of the Figma Build Report.</summary>
    public static BuildReport WriteReport(GameProject p, long romKb = 412)
    {
        Directory.CreateDirectory(p.BuildDir);
        var history = new List<BuildReport.HistoryEntry>
        {
            new(new DateTime(2026, 10, 31, 11, 40, 5), 401 * 1024, 0.91, 0, 0, ""),
            new(new DateTime(2026, 10, 31, 11, 52, 12), 0, 0.4, 0, 1, "PlayerController.cs(22): ';' expected"),
            new(new DateTime(2026, 10, 31, 11, 58, 40), 409 * 1024, 0.77, 0, 0, ""),
        };
        history.Reverse();
        File.WriteAllText(Path.Combine(p.BuildDir, "report-history.json"), System.Text.Json.JsonSerializer.Serialize(history,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        var r = new BuildReport
        {
            Rom = "sky_hopper.gba", Title = "SKYHOPPER", GameCode = "SKYH", Maker = "GE", Version = "1.0.3", SaveType = "Sram32K", Configuration = "Debug",
            BuiltAt = new DateTime(2026, 10, 31, 12, 6, 3), RomBytes = romKb * 1024, BuildSeconds = 0.80,
            StepSeconds = new() { [GameBuilder.StepScripts] = 0.21, [GameBuilder.StepAssets] = 0.18, [GameBuilder.StepCompile] = 0.34 },
            Breakdown = new() { ["Graphics"] = 182 * 1024, ["Code"] = 128 * 1024, ["Audio"] = 96 * 1024, ["Data"] = 6 * 1024 },
            LargestAssets =
            [
                new("bgm_forest", "Music", 41 * 1024, 10), new("forest_tiles", "Tileset", 32 * 1024, 7.8), new("thai_vwf", "Font", 6349, 1.5),
                new("hero", "Sprite", 4 * 1024, 1), new("items", "Data", 1126, 0.3),
            ],
            Warnings = ["PlayerController.cs(8,5): GAE002 float → use fixed"],
            Scenes = new() { ["Forest_01"] = new BuildReport.SceneBudget { VramBgBytes = 26 * 1024, VramObjBytes = 12 * 1024, Actors = 9 } },
        };
        r.Save(p.BuildDir);
        File.WriteAllBytes(Path.Combine(p.BuildDir, r.Rom), new byte[r.RomBytes]);
        return r;
    }

    /// <summary>hero_run.png (6 frames of the hero), forest2.tmx (a Tiled map) and jump2.wav, as in the Figma Import Asset.</summary>
    public static string[] ImportFiles(Fixture fx, GameProject p)
    {
        var dir = Directory.CreateDirectory(Path.Combine(fx.Folder, "incoming")).FullName;
        var hero = Png.Read(Path.Combine(p.AssetsDir, "sprites", "hero.png"));
        var run = new uint[96 * 16];
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 96; x++) run[y * 96 + x] = hero.Pixels[y * hero.Width + 32 + x];
        var heroRun = Path.Combine(dir, "hero_run.png");
        Png.WriteArgb(heroRun, run, 96, 16);
        var tiles = Path.Combine(dir, "forest2_tiles.png");
        File.Copy(Path.Combine(p.AssetsDir, "tilesets", "forest_tiles.png"), tiles);
        var csv = string.Join(",\n", Enumerable.Range(0, 20).Select(r => string.Join(",", Enumerable.Range(0, 40).Select(c => r > 14 ? 3 : r == 14 ? 2 : 0))));
        var tmx = Path.Combine(dir, "forest2.tmx");
        File.WriteAllText(tmx, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <map version="1.10" orientation="orthogonal" width="40" height="20" tilewidth="16" tileheight="16">
             <tileset firstgid="1" name="forest2" tilewidth="16" tileheight="16"><image source="forest2_tiles.png" width="128" height="64"/></tileset>
             <layer id="1" name="Ground" width="40" height="20"><data encoding="csv">
            {csv}
            </data></layer>
             <layer id="2" name="Decor" width="40" height="20"><data encoding="csv">
            {csv}
            </data></layer>
            </map>
            """);
        var wav = Path.Combine(dir, "jump2.wav");
        File.WriteAllBytes(wav, WavWriter.Encode(WavWriter.Tone(660, 9170 / 11025.0, 11025, true), 11025));
        return [heroRun, tmx, wav];
    }

    /// <summary>hero_big.png (48 colours), theme.mp3 and jump2.wav, as in the Figma Import Asset · Errors.</summary>
    public static string[] ProblemFiles(Fixture fx, GameProject p)
    {
        var dir = Directory.CreateDirectory(Path.Combine(fx.Folder, "incoming2")).FullName;
        var big = new uint[32 * 32];
        for (var i = 0; i < big.Length; i++)
        {
            var k = i % 48;   // 48 colours that stay distinct in 15-bit
            big[i] = 0xFF000000u | (uint)(k % 8 * 32 << 16) | (uint)(k / 8 * 40 << 8) | 128u;
        }
        var heroBig = Path.Combine(dir, "hero_big.png");
        Png.WriteArgb(heroBig, big, 32, 32);
        var mp3 = Path.Combine(dir, "theme.mp3");
        File.WriteAllBytes(mp3, [0x49, 0x44, 0x33, 3, 0, 0, 0, 0, 0, 0]);
        var wav = Path.Combine(dir, "jump2.wav");
        File.WriteAllBytes(wav, WavWriter.Encode(WavWriter.Tone(660, 9170 / 11025.0, 11025, true), 11025));
        _ = p;
        return [heroBig, mp3, wav];
    }

    /// <summary>The main window at the Figma size with a page open.</summary>
    public static MainWindow Show(Fixture fx, string page, object? args = null)
    {
        var win = new MainWindow(fx.Services, startPage: null) { Width = 1440, Height = 900, SystemDecorations = SystemDecorations.None };
        win.Show();
        _ = fx.Services.Windows.Open(page, args, source: "test");
        Capture.Pump();
        return win;
    }

    /// <summary>Undo and Redo enabled, as Figma draws them.</summary>
    public static void UndoRedoEnabled(EditorServices s)
    {
        s.Undo.Push(new DelegateAction("a", () => { }, () => { }));
        s.Undo.Push(new DelegateAction("b", () => { }, () => { }));
        s.Undo.Undo();
    }

    /// <summary>Runs the dispatcher until <paramref name="done"/> or the timeout.</summary>
    public static void PumpUntil(Func<bool> done, int timeoutMs = 120_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && sw.ElapsedMilliseconds < timeoutMs)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Capture.Pump();
    }
}

/// <summary>An audio output that records what it pulls instead of playing it.</summary>
public sealed class CaptureAudioOutput : IAudioOutput
{
    Func<short[], int>? _fill;
    public int SampleRate => AudioPreview.Rate;
    public bool IsRunning => _fill != null;
    public List<short> Pcm { get; } = [];
    public void Start(Func<short[], int> fill) => _fill = fill;
    public void Stop() => _fill = null;
    public void Dispose() => Stop();

    /// <summary>Pulls <paramref name="buffers"/> buffers of 2048 frames, as the device would.</summary>
    public int Pull(int buffers = 4)
    {
        var total = 0;
        var buf = new short[2048 * 2];
        for (var i = 0; i < buffers && _fill != null; i++)
        {
            var n = _fill(buf);
            if (n <= 0) { _fill = null; break; }
            Pcm.AddRange(buf.Take(n * 2));
            total += n;
        }
        return total;
    }
}

/// <summary>A cartridge device that keeps everything in memory (never a real device in tests).</summary>
public sealed class FakeCartridge(string name = "GBxCart RW · USB (COM4)") : ICartridgeDevice
{
    public string Name { get; } = name;
    public CartridgeStatus Status { get; set; } = new(true, "Cartridge detected · SKYH", "Current game: SKYHOPPER rev 2", "SKYHOPPER", FlashMegabytes: 32);
    public byte[]? Save { get; set; } = [1, 2, 3, 4];
    /// <summary>What the cartridge holds (a game with a header: the device reports its title, as GBxCart RW does).</summary>
    public byte[] Flash { get; set; } = [];
    public bool CorruptOnWrite { get; set; }
    /// <summary>Writes stopped by Cancel; the flash keeps what it held.</summary>
    public int CancelledWrites { get; private set; }
    /// <summary>When set, writing stops after this many bytes until <see cref="Gate"/> completes.</summary>
    public long PauseAt { get; set; } = -1;
    public TaskCompletionSource Gate { get; } = new();

    public Task<CartridgeStatus> DetectAsync(CancellationToken ct) => Task.FromResult(
        Flash.Length >= 0xC0 && Flash[0xB2] == 0x96 && System.Text.Encoding.ASCII.GetString(Flash, 0xA0, 12).TrimEnd('\0', ' ') is var title
            ? Status with { Detail = $"Current game: {title} rev {Flash[0xBC]}", CurrentTitle = title }
            : Status);
    public Task<byte[]?> ReadSaveAsync(string romName, IProgress<long> progress, CancellationToken ct) => Task.FromResult(Save);

    public async Task WriteRomAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct)
    {
        var data = new byte[rom.Length];
        try
        {
            for (var o = 0; o < rom.Length; o += 16 * 1024)
            {
                ct.ThrowIfCancellationRequested();
                var n = Math.Min(16 * 1024, rom.Length - o);
                Array.Copy(rom, o, data, o, n);
                progress.Report(o + n);
                if (PauseAt >= 0 && o + n >= PauseAt && !Gate.Task.IsCompleted) await Gate.Task.WaitAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            CancelledWrites++;
            throw;
        }
        if (CorruptOnWrite && data.Length > 0) data[0] ^= 0xFF;
        Flash = data;
    }

    public Task<bool> VerifyAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct) => Task.FromResult(Flash.AsSpan().SequenceEqual(rom));
}

public sealed class FakeDevices(params ICartridgeDevice[] devices) : ICartridgeDeviceProvider
{
    public IReadOnlyList<ICartridgeDevice> Find() => devices;
}
