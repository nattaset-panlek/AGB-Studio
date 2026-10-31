using System.Runtime.InteropServices;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>A sound device that pulls interleaved 16-bit stereo frames. The editor uses winmm waveOut; tests use a capture.</summary>
public interface IAudioOutput : IDisposable
{
    int SampleRate { get; }
    bool IsRunning { get; }
    /// <summary>Starts pulling from <paramref name="fill"/> (frames written, 0 = finished) until it returns 0 or <see cref="Stop"/>.</summary>
    void Start(Func<short[], int> fill);
    void Stop();
}

/// <summary>Windows waveOut through winmm.dll (no extra packages): four 2048-frame buffers fed from a worker thread.</summary>
public sealed class WaveOutOutput(int sampleRate = AudioPreview.Rate) : IAudioOutput
{
    const int Frames = 2048, BufferCount = 4, WhdrDone = 1, CallbackEvent = 0x50000;
    const uint WaveMapper = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct WaveFormatEx
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSec, AvgBytesPerSec;
        public ushort BlockAlign, BitsPerSample, Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WaveHdr
    {
        public IntPtr Data;
        public uint BufferLength, BytesRecorded;
        public IntPtr User;
        public uint Flags, Loops;
        public IntPtr Next, Reserved;
    }

    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr hwo, uint device, ref WaveFormatEx format, IntPtr callback, IntPtr instance, int flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr hwo, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr hwo);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr hwo);

    Thread? _thread;
    volatile bool _stop;

    public int SampleRate { get; } = sampleRate;
    public bool IsRunning => _thread is { IsAlive: true };

    public void Start(Func<short[], int> fill)
    {
        Stop();
        _stop = false;
        _thread = new Thread(() => Run(fill)) { IsBackground = true, Name = "AGB Studio audio preview" };
        _thread.Start();
    }

    void Run(Func<short[], int> fill)
    {
        using var ready = new AutoResetEvent(false);
        var fmt = new WaveFormatEx { FormatTag = 1, Channels = 2, SamplesPerSec = (uint)SampleRate, AvgBytesPerSec = (uint)SampleRate * 4, BlockAlign = 4, BitsPerSample = 16 };
        if (waveOutOpen(out var hwo, WaveMapper, ref fmt, ready.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CallbackEvent) != 0) return;
        var hdrSize = Marshal.SizeOf<WaveHdr>();
        var headers = new IntPtr[BufferCount];
        var pcm = new short[Frames * 2];
        try
        {
            for (var i = 0; i < BufferCount; i++)
            {
                headers[i] = Marshal.AllocHGlobal(hdrSize);
                var h = new WaveHdr { Data = Marshal.AllocHGlobal(Frames * 4), BufferLength = Frames * 4, Flags = WhdrDone };
                Marshal.StructureToPtr(h, headers[i], false);
            }
            var finished = false;
            while (!_stop)
            {
                var busy = 0;
                for (var i = 0; i < BufferCount && !_stop; i++)
                {
                    var h = Marshal.PtrToStructure<WaveHdr>(headers[i]);
                    if ((h.Flags & WhdrDone) == 0) { busy++; continue; }
                    if (finished) continue;
                    var n = fill(pcm);
                    if (n <= 0) { finished = true; continue; }
                    Array.Clear(pcm, n * 2, pcm.Length - n * 2);
                    Marshal.Copy(pcm, 0, h.Data, Frames * 2);
                    if ((h.Flags & 2) != 0) waveOutUnprepareHeader(hwo, headers[i], hdrSize);
                    h.Flags = 0;
                    h.BufferLength = (uint)(n * 4);
                    Marshal.StructureToPtr(h, headers[i], false);
                    waveOutPrepareHeader(hwo, headers[i], hdrSize);
                    waveOutWrite(hwo, headers[i], hdrSize);
                    busy++;
                }
                if (finished && busy == 0) break;
                ready.WaitOne(50);
            }
        }
        finally
        {
            waveOutReset(hwo);
            foreach (var p in headers)
            {
                if (p == IntPtr.Zero) continue;
                waveOutUnprepareHeader(hwo, p, hdrSize);
                Marshal.FreeHGlobal(Marshal.PtrToStructure<WaveHdr>(p).Data);
                Marshal.FreeHGlobal(p);
            }
            waveOutClose(hwo);
        }
    }

    public void Stop()
    {
        _stop = true;
        if (_thread != null && _thread != Thread.CurrentThread) _thread.Join(1000);
        _thread = null;
    }

    public void Dispose() => Stop();
}

/// <summary>
/// The preview mixer shared by the Audio page and the SFX Designer: one music player and any number of one-shot
/// sounds mixed into one output (so "Preview SFX" plays over the music, as the game mixes them).
/// </summary>
public sealed class AudioPreview : IDisposable
{
    public const int Rate = 44100;

    sealed class Sound(float[] data, bool loop)
    {
        public float[] Data = data;
        public int Pos;
        public bool Loop = loop;
    }

    readonly IAudioOutput _out;
    readonly object _lock = new();
    readonly List<Sound> _sounds = [];
    ModulePlayer? _music;

    public AudioPreview(IAudioOutput? output = null) => _out = output ?? new WaveOutOutput();

    /// <summary>The preview of the editor instance (created on first use with waveOut).</summary>
    public static AudioPreview Of(EditorServices s)
    {
        if (s.GetService(typeof(AudioPreview)) is AudioPreview p) return p;
        p = new AudioPreview();
        s.Set(p);
        return p;
    }

    public ModulePlayer? Music { get { lock (_lock) return _music; } }
    public bool IsPlayingMusic { get { lock (_lock) return _music is { Ended: false }; } }
    public bool IsPlayingSound { get { lock (_lock) return _sounds.Count > 0; } }
    public long FramesMixed { get; private set; }
    public event Action? Stopped;

    public void PlayMusic(ModulePlayer player)
    {
        lock (_lock) _music = player;
        EnsureRunning();
    }

    public void StopMusic()
    {
        lock (_lock) _music = null;
    }

    /// <summary>Plays mono samples at <see cref="Rate"/>; a looping sound repeats until <see cref="StopSounds"/>.</summary>
    public void PlaySound(float[] mono, bool loop = false)
    {
        if (mono.Length == 0) return;
        lock (_lock) _sounds.Add(new Sound(mono, loop));
        EnsureRunning();
    }

    public void StopSounds()
    {
        lock (_lock) _sounds.Clear();
    }

    public void StopAll()
    {
        lock (_lock) { _music = null; _sounds.Clear(); }
    }

    void EnsureRunning()
    {
        if (!_out.IsRunning) _out.Start(Fill);
    }

    /// <summary>Mixes the next frames (called by the output on its thread).</summary>
    public int Fill(short[] buffer)
    {
        lock (_lock)
        {
            var frames = buffer.Length / 2;
            Array.Clear(buffer);
            var any = false;
            if (_music != null)
            {
                var n = _music.Render(buffer);
                any |= n > 0;
                if (_music.Ended) _music = null;
            }
            for (var k = _sounds.Count - 1; k >= 0; k--)
            {
                var s = _sounds[k];
                for (var i = 0; i < frames; i++)
                {
                    if (s.Pos >= s.Data.Length)
                    {
                        if (!s.Loop) break;
                        s.Pos = 0;
                    }
                    var v = (int)(s.Data[s.Pos++] * 26000);
                    buffer[i * 2] = (short)Math.Clamp(buffer[i * 2] + v, -32768, 32767);
                    buffer[i * 2 + 1] = (short)Math.Clamp(buffer[i * 2 + 1] + v, -32768, 32767);
                }
                any = true;
                if (s.Pos >= s.Data.Length && !s.Loop) _sounds.RemoveAt(k);
            }
            if (!any) { Avalonia.Threading.Dispatcher.UIThread.Post(() => Stopped?.Invoke()); return 0; }
            FramesMixed += frames;
            return frames;
        }
    }

    /// <summary>Resamples mono samples from <paramref name="rate"/> to the preview rate.</summary>
    public static float[] ToRate(float[] samples, int rate) => new WavData(samples, rate, 1, 16).Resample(Rate);

    public void Dispose()
    {
        StopAll();
        _out.Dispose();
    }
}
