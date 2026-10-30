using System.Text;

namespace AgbStudio.Emu;

/// <summary>Reads the runtime's debug channel (runtime/include/agb/debug.hpp) out of EWRAM.</summary>
public sealed class DebugChannel
{
    static readonly byte[] Magic = "AGBDBG01"u8.ToArray();

    // Field offsets in agb::debug::Channel
    public const uint OffVersion = 8, OffStatus = 12, OffFrame = 16, OffLogHead = 20, OffLogSize = 24,
        OffHostCommand = 28, OffHostArg = 32, OffActorTable = 36, OffActorCount = 40, OffActorStride = 44,
        OffProfiler = 48, OffDebugger = 52, OffSymbols = 56, OffLog = 64;

    readonly LibretroCore _core;
    uint _logRead;

    public uint Address { get; }
    public uint Version => _core.ReadU32(Address + OffVersion);
    public TestStatus Status => (TestStatus)_core.ReadU32(Address + OffStatus);
    public uint GameFrame => _core.ReadU32(Address + OffFrame);
    public uint ActorTable => _core.ReadU32(Address + OffActorTable);
    public uint ActorCount => _core.ReadU32(Address + OffActorCount);
    public uint ActorStride => _core.ReadU32(Address + OffActorStride);
    public uint Profiler => _core.ReadU32(Address + OffProfiler);
    public uint Debugger => _core.ReadU32(Address + OffDebugger);
    public uint Symbols => _core.ReadU32(Address + OffSymbols);

    DebugChannel(LibretroCore core, uint address)
    {
        _core = core;
        Address = address;
    }

    /// <summary>Finds the channel in EWRAM, or returns null when the ROM has not initialised it yet.</summary>
    public static DebugChannel? Find(LibretroCore core)
    {
        var ewram = core.Memory(GbaMemory.Ewram, GbaMemory.EwramSize);
        var at = ewram.IndexOf(Magic);
        while (at >= 0 && at % 4 != 0)
        {
            var next = ewram[(at + 1)..].IndexOf(Magic);
            at = next < 0 ? -1 : at + 1 + next;
        }
        return at < 0 ? null : new DebugChannel(core, GbaMemory.Ewram + (uint)at);
    }

    /// <summary>Returns log lines written since the last call.</summary>
    public IReadOnlyList<string> ReadNewLog()
    {
        var head = _core.ReadU32(Address + OffLogHead);
        var size = _core.ReadU32(Address + OffLogSize);
        if (size == 0) return Array.Empty<string>();
        if (head < _logRead) _logRead = 0;  // ROM restarted
        if (head - _logRead > size) _logRead = head - size;  // lost lines in an overflowing ring
        var ring = _core.Memory(Address + OffLog, (int)size);
        var sb = new StringBuilder();
        for (var i = _logRead; i < head; i++) sb.Append((char)ring[(int)(i % size)]);
        _logRead = head;
        var text = sb.ToString();
        var lines = text.Split('\n');
        return lines.Take(lines.Length - 1).ToList();
    }

    public void SendCommand(uint command, uint arg)
    {
        _core.WriteU32(Address + OffHostArg, arg);
        _core.WriteU32(Address + OffHostCommand, command);
    }
}

public enum TestStatus : uint { Running = 0, Pass = 1, Fail = 2 }
