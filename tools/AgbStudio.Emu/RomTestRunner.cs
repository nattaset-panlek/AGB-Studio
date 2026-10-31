namespace AgbStudio.Emu;

/// <summary>Headless ROM test: runs a ROM until its debug channel reports pass or fail.</summary>
public sealed record RomTestResult(TestStatus Status, long Frames, IReadOnlyList<string> Log, uint[] LastFrame, string? Error)
{
    public bool Passed => Status == TestStatus.Pass;
}

public sealed class RomTestOptions
{
    public int MaxFrames { get; init; } = 600;
    /// <summary>Optional input script: frame number to keys held from that frame on.</summary>
    public IReadOnlyDictionary<int, GbaKeys>? Input { get; init; }
    public Action<LibretroCore, long>? OnFrame { get; init; }
    /// <summary>Keep running this many frames after pass, e.g. to capture a settled screen.</summary>
    public int ExtraFramesAfterPass { get; init; }
    public Action<LibretroCore>? BeforeUnload { get; init; }
}

public static class RomTestRunner
{
    public static RomTestResult Run(LibretroCore core, string romPath, RomTestOptions? options = null)
    {
        options ??= new RomTestOptions();
        var log = new List<string>();
        core.LoadRom(romPath);
        DebugChannel? channel = null;
        var status = TestStatus.Running;
        long frame = 0;
        var extra = -1;
        for (; frame < options.MaxFrames; frame++)
        {
            if (options.Input != null && options.Input.TryGetValue((int)frame, out var keys)) core.Keys = keys;
            core.RunFrame();
            options.OnFrame?.Invoke(core, frame);
            channel ??= DebugChannel.Find(core);
            if (channel == null) continue;
            log.AddRange(channel.ReadNewLog());
            if (extra > 0) { extra--; continue; }
            if (extra == 0) break;
            status = channel.Status;
            if (status != TestStatus.Running)
            {
                if (options.ExtraFramesAfterPass <= 0) { frame++; break; }
                extra = options.ExtraFramesAfterPass;
            }
        }
        var error = channel == null ? "The ROM never started the AGB Studio debug channel, so no test result can be read. Check that it is a Debug build made by AGB Studio."
            : status == TestStatus.Running ? $"Timed out after {options.MaxFrames} frames without a pass or fail. Give it more frames (agb romtest --frames N), or check that the test ends." : null;
        options.BeforeUnload?.Invoke(core);
        var result = new RomTestResult(status, frame, log, (uint[])core.Frame.Clone(), error);
        core.UnloadRom();
        return result;
    }
}
