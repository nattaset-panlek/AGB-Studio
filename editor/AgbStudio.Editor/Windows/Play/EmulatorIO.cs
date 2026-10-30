using System.Runtime.InteropServices;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using Avalonia.Input;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>
/// Preferences · Emulator (T-D09) as drawn in Figma: mGBA (built in), Scale 3x · 720x480, Filter Pixel-perfect, Audio on,
/// Pause when unfocused off, Fast-forward Tab · 4x. Play Mode reads <c>Services.Get&lt;EmulatorOptions&gt;()</c>, so the
/// Preferences window changes them by replacing or editing this service.
/// </summary>
public sealed class EmulatorOptions
{
    public int Scale { get; set; } = 3;
    public bool PixelPerfect { get; set; } = true;
    public bool Audio { get; set; } = true;
    public bool PauseWhenUnfocused { get; set; }
    public string FastForwardKey { get; set; } = "Tab";
    public double FastForwardSpeed { get; set; } = 4;
}

/// <summary>Where Play Mode sends the game's audio (interleaved stereo 16-bit).</summary>
public interface IAudioSink : IDisposable
{
    void Open(int sampleRate);
    void Write(short[] stereo);
    long SamplesWritten { get; }
}

/// <summary>Audio output through Windows waveOut (winmm.dll): a small ring of buffers; when the device falls behind, samples are dropped instead of building latency.</summary>
public sealed class WaveOutSink : IAudioSink
{
    const int Buffers = 8, Capacity = 32 * 1024, WhdrDone = 1, WhdrPrepared = 2;
    static readonly int HeaderSize = Marshal.SizeOf<WaveHeader>();

    IntPtr _device;
    readonly IntPtr[] _headers = new IntPtr[Buffers];
    readonly IntPtr[] _data = new IntPtr[Buffers];
    readonly bool[] _queued = new bool[Buffers];

    public long SamplesWritten { get; private set; }
    public bool IsOpen => _device != IntPtr.Zero;

    public void Open(int sampleRate)
    {
        if (IsOpen || !OperatingSystem.IsWindows()) return;
        var fmt = new WaveFormat { FormatTag = 1, Channels = 2, SamplesPerSec = (uint)sampleRate, AvgBytesPerSec = (uint)sampleRate * 4, BlockAlign = 4, BitsPerSample = 16 };
        try
        {
            if (waveOutOpen(out _device, WaveMapper, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { _device = IntPtr.Zero; return; }
        }
        catch (DllNotFoundException) { _device = IntPtr.Zero; return; }
        for (var i = 0; i < Buffers; i++)
        {
            _headers[i] = Marshal.AllocHGlobal(HeaderSize);
            _data[i] = Marshal.AllocHGlobal(Capacity);
            Marshal.StructureToPtr(new WaveHeader { Data = _data[i] }, _headers[i], false);
        }
    }

    public void Write(short[] stereo)
    {
        if (!IsOpen || stereo.Length == 0) return;
        for (var i = 0; i < Buffers; i++)
        {
            var h = Marshal.PtrToStructure<WaveHeader>(_headers[i]);
            if (_queued[i] && (h.Flags & WhdrDone) == 0) continue;
            if ((h.Flags & WhdrPrepared) != 0) waveOutUnprepareHeader(_device, _headers[i], (uint)HeaderSize);
            var bytes = Math.Min(stereo.Length * 2, Capacity);
            Marshal.Copy(stereo, 0, _data[i], bytes / 2);
            Marshal.StructureToPtr(new WaveHeader { Data = _data[i], BufferLength = (uint)bytes }, _headers[i], false);
            waveOutPrepareHeader(_device, _headers[i], (uint)HeaderSize);
            waveOutWrite(_device, _headers[i], (uint)HeaderSize);
            _queued[i] = true;
            SamplesWritten += bytes / 2;
            return;
        }
    }

    public void Dispose()
    {
        if (!IsOpen) return;
        waveOutReset(_device);
        for (var i = 0; i < Buffers; i++)
        {
            waveOutUnprepareHeader(_device, _headers[i], (uint)HeaderSize);
            Marshal.FreeHGlobal(_headers[i]);
            Marshal.FreeHGlobal(_data[i]);
        }
        waveOutClose(_device);
        _device = IntPtr.Zero;
    }

    const uint WaveMapper = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct WaveFormat
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSec, AvgBytesPerSec;
        public ushort BlockAlign, BitsPerSample, Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength, BytesRecorded;
        public IntPtr User;
        public uint Flags, Loops;
        public IntPtr Next, Reserved;
    }

    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr device, uint id, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr device, IntPtr header, uint size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr device);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr device);
}

/// <summary>Keyboard and gamepad mapping from Project Settings · Input (T-D08): "Keyboard (emulator)" and "Gamepad" columns.</summary>
public sealed class InputMap
{
    readonly Dictionary<string, GbaKeys> _keyboard;
    readonly List<(string Pad, GbaKeys Keys)> _pad = [];

    public InputMap(IEnumerable<InputAction> actions)
    {
        var list = actions.ToList();
        _keyboard = PlaySession.KeyboardMap(list.Select(a => (a.Button, a.Keyboard)));
        foreach (var a in list)
        {
            if (a.Gamepad.Length == 0) continue;
            if (a.Button == "D-pad") { _pad.Add((a.Gamepad, GbaKeys.Up | GbaKeys.Down | GbaKeys.Left | GbaKeys.Right)); continue; }
            if (Enum.TryParse<GbaKeys>(a.Button, out var k)) _pad.Add((a.Gamepad, k));
        }
    }

    /// <summary>The GBA button for a keyboard key, by its Project Settings name ("Z", "Enter", "Backspace", "Up").</summary>
    public GbaKeys Key(Key key) => _keyboard.TryGetValue(KeyName(key), out var k) ? k : GbaKeys.None;

    public static string KeyName(Key key) => key switch
    {
        Avalonia.Input.Key.Enter => "Enter",
        Avalonia.Input.Key.Back => "Backspace",
        Avalonia.Input.Key.Space => "Space",
        Avalonia.Input.Key.Tab => "Tab",
        Avalonia.Input.Key.Escape => "Esc",
        >= Avalonia.Input.Key.D0 and <= Avalonia.Input.Key.D9 => ((char)('0' + (key - Avalonia.Input.Key.D0))).ToString(),
        _ => key.ToString(),
    };

    /// <summary>GBA buttons held on an XInput pad (names as Project Settings · Input writes them: South, West, LB, RB, Menu, View, Stick / D-pad).</summary>
    public GbaKeys Pad(XInputPad pad)
    {
        var keys = GbaKeys.None;
        foreach (var (name, k) in _pad)
        {
            if (k == (GbaKeys.Up | GbaKeys.Down | GbaKeys.Left | GbaKeys.Right))
            {
                if (pad.Up) keys |= GbaKeys.Up;
                if (pad.Down) keys |= GbaKeys.Down;
                if (pad.Left) keys |= GbaKeys.Left;
                if (pad.Right) keys |= GbaKeys.Right;
                continue;
            }
            if (pad.Pressed(name)) keys |= k;
        }
        return keys;
    }
}

/// <summary>One XInput controller state (buttons and left stick).</summary>
public readonly record struct XInputPad(ushort Buttons, short StickX, short StickY)
{
    const short Dead = 12000;
    public bool Up => (Buttons & 0x0001) != 0 || StickY > Dead;
    public bool Down => (Buttons & 0x0002) != 0 || StickY < -Dead;
    public bool Left => (Buttons & 0x0004) != 0 || StickX < -Dead;
    public bool Right => (Buttons & 0x0008) != 0 || StickX > Dead;

    public bool Pressed(string name) => name switch
    {
        "South" => (Buttons & 0x1000) != 0,
        "East" => (Buttons & 0x2000) != 0,
        "West" => (Buttons & 0x4000) != 0,
        "North" => (Buttons & 0x8000) != 0,
        "LB" => (Buttons & 0x0100) != 0,
        "RB" => (Buttons & 0x0200) != 0,
        "Menu" => (Buttons & 0x0010) != 0,
        "View" => (Buttons & 0x0020) != 0,
        _ => false,
    };

    /// <summary>Polls controller 0 through xinput1_4.dll; null when none is connected.</summary>
    public static XInputPad? Poll()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            if (XInputGetState(0, out var s) != 0) return null;
            return new XInputPad(s.Buttons, s.ThumbLX, s.ThumbLY);
        }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct State
    {
        public uint Packet;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
    }

    [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint index, out State state);
}
