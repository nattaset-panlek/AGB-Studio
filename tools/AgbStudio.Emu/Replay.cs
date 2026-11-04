using System.IO.Compression;
using AgbStudio.Core;
using AgbStudio.Core.Project;

namespace AgbStudio.Emu;

/// <summary>replays/&lt;Name&gt;.replay: a play session from the first frame, as the buttons held on each frame, what was on
/// the save chip when it began and a fingerprint of the picture every so often. The emulator gives the same frames for the
/// same ROM and buttons, so playing it again shows whether the game still does what it did.</summary>
public sealed class ReplayFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    /// <summary>The save chip at the start, deflated and in base64; null when it was empty.</summary>
    public string? Save { get; set; }
    /// <summary>Runs of [frames, buttons (GbaKeys)].</summary>
    public List<int[]> Keys { get; set; } = [];
    public List<ReplayCheck> Checks { get; set; } = [];

    public int Frames => Keys.Sum(k => k[0]);
}

public sealed class ReplayCheck
{
    public int Frame { get; set; }
    public string Picture { get; set; } = "";
}

/// <summary>Writes down a session frame by frame (PlaySession.Recorder). Start it right after the ROM was loaded.</summary>
public sealed class ReplayRecorder(byte[] saveAtStart)
{
    public const int CheckEvery = 120;
    readonly ReplayFile _replay = new() { Save = ReplayRunner.PackSave(saveAtStart) };
    int _frame;

    public int Frames => _frame;

    public void Before(GbaKeys keys)
    {
        if (_replay.Keys.Count > 0 && _replay.Keys[^1][1] == (int)keys) _replay.Keys[^1][0]++;
        else _replay.Keys.Add([1, (int)keys]);
    }

    public void After(uint[] frame)
    {
        if (++_frame % CheckEvery == 0) _replay.Checks.Add(new ReplayCheck { Frame = _frame, Picture = ReplayRunner.Fingerprint(frame) });
    }

    /// <summary>The finished replay, whose last check is the last frame.</summary>
    public ReplayFile Finish(string name, uint[] lastFrame)
    {
        _replay.Name = name;
        if (_frame > 0 && (_replay.Checks.Count == 0 || _replay.Checks[^1].Frame != _frame))
            _replay.Checks.Add(new ReplayCheck { Frame = _frame, Picture = ReplayRunner.Fingerprint(lastFrame) });
        return _replay;
    }
}

public sealed record ReplayResult(string Name, bool Passed, int Frames, string Message, string? PicturePath);

public static class ReplayRunner
{
    /// <summary>Plays a replay on the core. A difference or a failed assert in the game ends the run; the picture at that frame
    /// goes into <paramref name="pictureDir"/> when given.</summary>
    public static ReplayResult Run(LibretroCore core, string romPath, ReplayFile replay, string? pictureDir = null)
    {
        if (replay.Format != 1 || replay.Frames == 0)
            return new ReplayResult(replay.Name, false, 0, "This replay file is empty or from a newer version of AGB Studio. Record it again.", null);
        var checks = replay.Checks.ToDictionary(c => c.Frame, c => c.Picture);
        core.Keys = GbaKeys.None;
        core.LoadRom(romPath);
        try
        {
            RestoreSave(core, replay.Save);
            DebugChannel? channel = null;
            var frame = 0;
            foreach (var run in replay.Keys)
            {
                core.Keys = (GbaKeys)run[1];
                for (var i = 0; i < run[0]; i++)
                {
                    core.RunFrame();
                    frame++;
                    channel ??= DebugChannel.Find(core);
                    if (channel?.Status == TestStatus.Fail)
                        return Failed(replay, core, frame, $"The game stopped on a failed assert at frame {frame}: {string.Join(' ', channel.ReadNewLog().TakeLast(1))}", pictureDir);
                    if (checks.TryGetValue(frame, out var expected) && Fingerprint(core.Frame) != expected)
                        return Failed(replay, core, frame, $"The screen at frame {frame} ({frame / 60} s) differs from the recording.", pictureDir);
                }
            }
            return new ReplayResult(replay.Name, true, frame, $"{frame} frames, {checks.Count} screens match.", null);
        }
        finally
        {
            core.Keys = GbaKeys.None;
            core.UnloadRom();
        }
    }

    /// <summary>Plays every replay of a built project; pictures of failures go to build/replays.</summary>
    public static List<ReplayResult> RunAll(LibretroCore core, GameProject project, string romPath)
    {
        var results = new List<ReplayResult>();
        foreach (var file in project.ReplayFiles())
        {
            var name = GameProject.AssetName(file);
            ReplayFile replay;
            try { replay = GameProject.Load<ReplayFile>(file); }
            catch (Exception e) when (e is System.Text.Json.JsonException or IOException)
            {
                results.Add(new ReplayResult(name, false, 0, $"The replay file cannot be read: {e.Message}. Record it again.", null));
                continue;
            }
            replay.Name = name;
            results.Add(Run(core, romPath, replay, Path.Combine(project.BuildDir, "replays")));
        }
        return results;
    }

    static ReplayResult Failed(ReplayFile replay, LibretroCore core, int frame, string message, string? pictureDir)
    {
        string? path = null;
        if (pictureDir != null)
        {
            Directory.CreateDirectory(pictureDir);
            path = Path.Combine(pictureDir, $"{replay.Name}-frame{frame}.png");
            Png.WriteArgb(path, core.Frame, LibretroCore.Width, LibretroCore.Height);
            message += $" The screen is in {path}.";
        }
        return new ReplayResult(replay.Name, false, frame, message, path);
    }

    /// <summary>FNV-1a of the picture: 64 bits, written as hex.</summary>
    public static string Fingerprint(uint[] frame)
    {
        var h = 14695981039346656037ul;
        foreach (var px in frame)
        {
            h ^= px & 0xFFFFFF;
            h *= 1099511628211ul;
        }
        return h.ToString("X16");
    }

    public static string? PackSave(byte[] save)
    {
        if (Array.TrueForAll(save, b => b == 0)) return null;
        using var memory = new MemoryStream();
        using (var deflate = new DeflateStream(memory, CompressionLevel.SmallestSize)) deflate.Write(save);
        return Convert.ToBase64String(memory.ToArray());
    }

    public static void RestoreSave(LibretroCore core, string? packed)
    {
        var chip = core.SaveRam;
        chip.Clear();
        if (packed == null) return;
        using var deflate = new DeflateStream(new MemoryStream(Convert.FromBase64String(packed)), CompressionMode.Decompress);
        using var bytes = new MemoryStream();
        deflate.CopyTo(bytes);
        var data = bytes.GetBuffer().AsSpan(0, (int)bytes.Length);
        data[..Math.Min(data.Length, chip.Length)].CopyTo(chip);
    }
}
