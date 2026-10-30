using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AgbStudio.Emu;

/// <summary>GBA buttons in libretro joypad id order.</summary>
[Flags]
public enum GbaKeys : ushort
{
    None = 0,
    B = 1 << 0,
    Select = 1 << 2,
    Start = 1 << 3,
    Up = 1 << 4,
    Down = 1 << 5,
    Left = 1 << 6,
    Right = 1 << 7,
    A = 1 << 8,
    L = 1 << 10,
    R = 1 << 11,
}

/// <summary>
/// Hosts the mGBA libretro core in-process. Libretro cores keep global state,
/// so only one instance can exist per process at a time.
/// </summary>
public sealed unsafe class LibretroCore : IDisposable
{
    public const int Width = 240;
    public const int Height = 160;

    static LibretroCore? s_current;
    static readonly object s_lock = new();

    readonly IntPtr _lib;
    readonly delegate* unmanaged[Cdecl]<void> _init, _deinit, _run, _unloadGame;
    readonly delegate* unmanaged[Cdecl]<IntPtr, void> _setEnvironment, _setVideo, _setAudio, _setAudioBatch, _setInputPoll, _setInputState;
    readonly delegate* unmanaged[Cdecl]<GameInfo*, byte> _loadGame;
    readonly delegate* unmanaged[Cdecl]<uint, IntPtr> _getMemoryData;
    readonly delegate* unmanaged[Cdecl]<uint, nuint> _getMemorySize;
    readonly delegate* unmanaged[Cdecl]<nuint> _serializeSize;
    readonly delegate* unmanaged[Cdecl]<IntPtr, nuint, byte> _serialize, _unserialize;
    readonly delegate* unmanaged[Cdecl]<SystemAvInfo*, void> _getAvInfo;

    readonly IntPtr _systemDir;
    IntPtr _romData, _romPath;
    readonly List<MemoryRegion> _regions = new();
    int _pixelFormat = 2;  // RGB565 until the core asks otherwise
    readonly List<short> _audio = new();
    bool _loaded;

    /// <summary>Last frame as 0xAARRGGBB, 240 x 160.</summary>
    public uint[] Frame { get; } = new uint[Width * Height];
    public GbaKeys Keys { get; set; }
    public long FrameCount { get; private set; }
    public double Fps { get; private set; }
    public double SampleRate { get; private set; }
    public IReadOnlyList<MemoryRegion> Regions => _regions;

    public static string DefaultCorePath => Path.Combine(AppContext.BaseDirectory, "mgba_libretro.dll");

    public LibretroCore(string? corePath = null)
    {
        lock (s_lock)
        {
            if (s_current != null) throw new InvalidOperationException("Only one emulator core can run per process.");
            s_current = this;
        }
        _lib = NativeLibrary.Load(corePath ?? DefaultCorePath);
        _init = (delegate* unmanaged[Cdecl]<void>)Sym("retro_init");
        _deinit = (delegate* unmanaged[Cdecl]<void>)Sym("retro_deinit");
        _run = (delegate* unmanaged[Cdecl]<void>)Sym("retro_run");
        _unloadGame = (delegate* unmanaged[Cdecl]<void>)Sym("retro_unload_game");
        _setEnvironment = (delegate* unmanaged[Cdecl]<IntPtr, void>)Sym("retro_set_environment");
        _setVideo = (delegate* unmanaged[Cdecl]<IntPtr, void>)Sym("retro_set_video_refresh");
        _setAudio = (delegate* unmanaged[Cdecl]<IntPtr, void>)Sym("retro_set_audio_sample");
        _setAudioBatch = (delegate* unmanaged[Cdecl]<IntPtr, void>)Sym("retro_set_audio_sample_batch");
        _setInputPoll = (delegate* unmanaged[Cdecl]<IntPtr, void>)Sym("retro_set_input_poll");
        _setInputState = (delegate* unmanaged[Cdecl]<IntPtr, void>)Sym("retro_set_input_state");
        _loadGame = (delegate* unmanaged[Cdecl]<GameInfo*, byte>)Sym("retro_load_game");
        _getMemoryData = (delegate* unmanaged[Cdecl]<uint, IntPtr>)Sym("retro_get_memory_data");
        _getMemorySize = (delegate* unmanaged[Cdecl]<uint, nuint>)Sym("retro_get_memory_size");
        _serializeSize = (delegate* unmanaged[Cdecl]<nuint>)Sym("retro_serialize_size");
        _serialize = (delegate* unmanaged[Cdecl]<IntPtr, nuint, byte>)Sym("retro_serialize");
        _unserialize = (delegate* unmanaged[Cdecl]<IntPtr, nuint, byte>)Sym("retro_unserialize");
        _getAvInfo = (delegate* unmanaged[Cdecl]<SystemAvInfo*, void>)Sym("retro_get_system_av_info");

        var dir = Path.Combine(Path.GetTempPath(), "agb-studio-emu");
        Directory.CreateDirectory(dir);
        _systemDir = Marshal.StringToHGlobalAnsi(dir);

        _setEnvironment((IntPtr)(delegate* unmanaged[Cdecl]<uint, void*, byte>)&OnEnvironment);
        _init();
        _setVideo((IntPtr)(delegate* unmanaged[Cdecl]<void*, uint, uint, nuint, void>)&OnVideo);
        _setAudio((IntPtr)(delegate* unmanaged[Cdecl]<short, short, void>)&OnAudioSample);
        _setAudioBatch((IntPtr)(delegate* unmanaged[Cdecl]<short*, nuint, nuint>)&OnAudioBatch);
        _setInputPoll((IntPtr)(delegate* unmanaged[Cdecl]<void>)&OnInputPoll);
        _setInputState((IntPtr)(delegate* unmanaged[Cdecl]<uint, uint, uint, uint, short>)&OnInputState);
    }

    IntPtr Sym(string name) => NativeLibrary.GetExport(_lib, name);

    public void LoadRom(string path) => LoadRom(File.ReadAllBytes(path), path);

    public void LoadRom(byte[] rom, string path = "game.gba")
    {
        if (_loaded) UnloadRom();
        _regions.Clear();
        _romData = Marshal.AllocHGlobal(rom.Length);
        Marshal.Copy(rom, 0, _romData, rom.Length);
        _romPath = Marshal.StringToHGlobalAnsi(path);
        var info = new GameInfo { Path = _romPath, Data = _romData, Size = (nuint)rom.Length, Meta = IntPtr.Zero };
        if (_loadGame(&info) == 0) throw new InvalidOperationException("The emulator core could not load the ROM.");
        SystemAvInfo av;
        _getAvInfo(&av);
        Fps = av.Fps;
        SampleRate = av.SampleRate;
        FrameCount = 0;
        _loaded = true;
    }

    public void UnloadRom()
    {
        if (!_loaded) return;
        _unloadGame();
        _loaded = false;
        Marshal.FreeHGlobal(_romData);
        Marshal.FreeHGlobal(_romPath);
        _romData = _romPath = IntPtr.Zero;
    }

    public void RunFrame()
    {
        if (!_loaded) throw new InvalidOperationException("No ROM loaded.");
        _run();
        FrameCount++;
    }

    /// <summary>Takes the audio produced since the last call as interleaved stereo samples.</summary>
    public short[] DrainAudio()
    {
        var samples = _audio.ToArray();
        _audio.Clear();
        return samples;
    }

    public Span<byte> SaveRam => MemorySpan(0);

    Span<byte> MemorySpan(uint id)
    {
        var ptr = _getMemoryData(id);
        var size = (int)_getMemorySize(id);
        return ptr == IntPtr.Zero ? Span<byte>.Empty : new Span<byte>((void*)ptr, size);
    }

    /// <summary>Returns the bytes of the GBA memory region that contains <paramref name="address"/>, starting at that address.</summary>
    public Span<byte> Memory(uint address, int length)
    {
        foreach (var r in _regions)
        {
            if (address >= r.Start && address + (ulong)length <= r.Start + (ulong)r.Length)
                return new Span<byte>((byte*)r.Pointer + (address - r.Start), length);
        }
        throw new ArgumentOutOfRangeException(nameof(address), $"0x{address:X8} is not mapped by the core.");
    }

    public uint ReadU32(uint address) => BitConverter.ToUInt32(Memory(address, 4));
    public ushort ReadU16(uint address) => BitConverter.ToUInt16(Memory(address, 2));
    public void WriteU32(uint address, uint value) => BitConverter.TryWriteBytes(Memory(address, 4), value);

    public byte[] SaveState()
    {
        var size = _serializeSize();
        var buf = new byte[(int)size];
        fixed (byte* p = buf)
            if (_serialize((IntPtr)p, size) == 0) throw new InvalidOperationException("Save state failed.");
        return buf;
    }

    public void LoadState(byte[] state)
    {
        fixed (byte* p = state)
            if (_unserialize((IntPtr)p, (nuint)state.Length) == 0) throw new InvalidOperationException("Load state failed.");
    }

    public void Dispose()
    {
        UnloadRom();
        _deinit();
        Marshal.FreeHGlobal(_systemDir);
        NativeLibrary.Free(_lib);
        lock (s_lock) s_current = null;
    }

    // ---- callbacks -------------------------------------------------------------------------------

    const uint EnvGetCanDupe = 3, EnvGetSystemDirectory = 9, EnvSetPixelFormat = 10, EnvGetVariableUpdate = 17,
        EnvGetSaveDirectory = 31, EnvSetMemoryMaps = 36 | 0x10000, EnvGetAudioVideoEnable = 47 | 0x10000;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static byte OnEnvironment(uint cmd, void* data)
    {
        var self = s_current;
        if (self == null) return 0;
        switch (cmd)
        {
            case EnvGetCanDupe:
                *(byte*)data = 1;
                return 1;
            case EnvGetSystemDirectory:
            case EnvGetSaveDirectory:
                *(IntPtr*)data = self._systemDir;
                return 1;
            case EnvSetPixelFormat:
                var fmt = *(int*)data;
                if (fmt != 1 && fmt != 2) return 0;
                self._pixelFormat = fmt;
                return 1;
            case EnvGetVariableUpdate:
                *(byte*)data = 0;
                return 1;
            case EnvGetAudioVideoEnable:
                *(int*)data = 3;
                return 1;
            case EnvSetMemoryMaps:
                var map = (MemoryMap*)data;
                self._regions.Clear();
                for (var i = 0; i < map->NumDescriptors; i++)
                {
                    var d = map->Descriptors[i];
                    if (d.Ptr == IntPtr.Zero || d.Len == 0) continue;
                    var name = d.AddrSpace == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(d.AddrSpace) ?? "";
                    self._regions.Add(new MemoryRegion((uint)d.Start, (int)d.Len, d.Ptr + (nint)d.Offset, name));
                }
                return 1;
            default:
                return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static void OnVideo(void* data, uint width, uint height, nuint pitch)
    {
        var self = s_current;
        if (self == null || data == null) return;
        var w = (int)Math.Min(width, Width);
        var h = (int)Math.Min(height, Height);
        for (var y = 0; y < h; y++)
        {
            var row = (byte*)data + (long)y * (long)pitch;
            for (var x = 0; x < w; x++)
            {
                uint argb;
                if (self._pixelFormat == 1)
                {
                    argb = ((uint*)row)[x] | 0xFF000000u;
                }
                else
                {
                    var p = ((ushort*)row)[x];
                    uint r = (uint)(p >> 11) & 31, g = (uint)(p >> 5) & 63, b = (uint)p & 31;
                    argb = 0xFF000000u | ((r * 255 / 31) << 16) | ((g * 255 / 63) << 8) | (b * 255 / 31);
                }
                self.Frame[y * Width + x] = argb;
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static void OnAudioSample(short left, short right)
    {
        var self = s_current;
        if (self == null) return;
        self._audio.Add(left);
        self._audio.Add(right);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static nuint OnAudioBatch(short* data, nuint frames)
    {
        var self = s_current;
        if (self == null) return frames;
        var n = (int)frames * 2;
        for (var i = 0; i < n; i++) self._audio.Add(data[i]);
        if (self._audio.Count > 48000 * 2) self._audio.RemoveRange(0, self._audio.Count - 48000 * 2);
        return frames;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static void OnInputPoll() { }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    static short OnInputState(uint port, uint device, uint index, uint id)
    {
        var self = s_current;
        if (self == null || port != 0 || device != 1 || id > 15) return 0;
        return (short)(((ushort)self.Keys >> (int)id) & 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct GameInfo { public IntPtr Path; public IntPtr Data; public nuint Size; public IntPtr Meta; }

    [StructLayout(LayoutKind.Sequential)]
    struct SystemAvInfo
    {
        public uint BaseWidth, BaseHeight, MaxWidth, MaxHeight;
        public float AspectRatio;
        public double Fps, SampleRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryDescriptor
    {
        public ulong Flags;
        public IntPtr Ptr;
        public nuint Offset, Start, Select, Disconnect, Len;
        public IntPtr AddrSpace;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryMap { public MemoryDescriptor* Descriptors; public uint NumDescriptors; }
}

public readonly record struct MemoryRegion(uint Start, int Length, IntPtr Pointer, string Name);

public static class GbaMemory
{
    public const uint Ewram = 0x02000000, Iwram = 0x03000000, Io = 0x04000000, Palette = 0x05000000,
        Vram = 0x06000000, Oam = 0x07000000, Rom = 0x08000000;
    public const int EwramSize = 256 * 1024, IwramSize = 32 * 1024, PaletteSize = 1024, VramSize = 96 * 1024, OamSize = 1024;
}

public static class Ascii
{
    public static string Read(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);
}
