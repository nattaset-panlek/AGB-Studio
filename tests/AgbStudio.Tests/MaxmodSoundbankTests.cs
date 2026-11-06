using System.Text;
using System.Text.RegularExpressions;
using AgbStudio.Build;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Audio.Maxmod;
using AgbStudio.Build.Content;
using static AgbStudio.Tests.TrackerFixtures;

namespace AgbStudio.Tests;

/// <summary>
/// The C# port of mmutil 1.10.1's soundbank writer (Audio/Maxmod) against the installed mmutil.exe: for each case both
/// run on the same inputs, and soundbank.bin must be byte for byte the same, the header the same text (so the same
/// #define names and values) and the port's log the same lines mmutil prints.
/// </summary>
public sealed class MaxmodSoundbankTests
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "maxmod");

    static string MmutilExe()
    {
        // devkitPro's mmutil.exe is the reference only: builds no longer run it (P24.05)
        var exe = Path.Combine(Toolchain.DetectDevkitPro() ?? @"D:\devkitPro", "tools", "bin", "mmutil.exe");
        Assert.True(File.Exists(exe), $"mmutil.exe (devkitPro, version 1.10.1) is not installed at {exe}; the maxmod port is checked against it, so this test cannot run without it.");
        var usage = Proc.Run(exe, "", null, TimeSpan.FromSeconds(30)).Output;
        Assert.True(usage.Contains("Maxmod Utility 1.10.1", StringComparison.Ordinal), $"{exe} is not mmutil 1.10.1:\n{usage}");
        return exe;
    }

    static string Dir(string name)
    {
        var d = Path.Combine(Root, name);
        if (Directory.Exists(d)) Directory.Delete(d, true);
        Directory.CreateDirectory(d);
        return d;
    }

    static string Put(string dir, string file, byte[] data)
    {
        var p = Path.Combine(dir, file);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllBytes(p, data);
        return p;
    }

    sealed record Run(bool Ok, byte[]? Bin, byte[]? Header, string Log, MaxmodResult? Port);

    static Run RunMmutil(IReadOnlyList<string> inputs, string dir)
    {
        var exe = MmutilExe();
        Directory.CreateDirectory(dir);
        var bin = Path.Combine(dir, "soundbank.bin");
        var h = Path.Combine(dir, "soundbank.h");
        var args = $"-o\"{bin}\" -h\"{h}\" " + string.Join(' ', inputs.Select(i => $"\"{i}\""));
        var r = Proc.Run(exe, args, dir, TimeSpan.FromMinutes(2));
        var ok = r.ExitCode == 0 && File.Exists(bin);
        return new Run(ok, ok ? File.ReadAllBytes(bin) : null, File.Exists(h) ? File.ReadAllBytes(h) : null, r.Output, null);
    }

    static Run RunPort(IReadOnlyList<string> inputs, string dir)
    {
        Directory.CreateDirectory(dir);
        var bin = Path.Combine(dir, "soundbank.bin");
        var h = Path.Combine(dir, "soundbank.h");
        var r = MaxmodSoundbank.Write(inputs, bin, h);
        return new Run(r.Success, File.Exists(bin) ? File.ReadAllBytes(bin) : null, File.Exists(h) ? File.ReadAllBytes(h) : null, r.Log, r);
    }

    static List<(string Name, int Value)> Defines(byte[] header) =>
        Regex.Matches(Encoding.Latin1.GetString(header), @"#define\s+(\S+)\s+(\d+)").Select(m => (m.Groups[1].Value, int.Parse(m.Groups[2].Value))).ToList();

    static string FirstDifference(byte[] expected, byte[] actual)
    {
        var n = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < n; i++)
            if (expected[i] != actual[i])
                return $"first difference at byte 0x{i:X} ({i}): mmutil {expected[i]:X2}, port {actual[i]:X2}; lengths {expected.Length} and {actual.Length}";
        return $"lengths differ: mmutil {expected.Length}, port {actual.Length}";
    }

    /// <summary>Runs both on the inputs; the bank, header and printed lines must be the same. Returns the port's result.</summary>
    static MaxmodResult Compare(string name, IReadOnlyList<string> inputs, bool portSucceeds = true)
    {
        var dir = Dir(name);
        var mm = RunMmutil(inputs, Path.Combine(dir, "mmutil"));
        var port = RunPort(inputs, Path.Combine(dir, "port"));
        Assert.True(mm.Ok, $"mmutil failed on {name}:\n{mm.Log}");
        Assert.True(port.Port!.Success == portSucceeds, $"port result on {name} (expected success {portSucceeds}):\n{port.Log}");
        Assert.NotNull(port.Bin);
        Assert.True(mm.Bin!.AsSpan().SequenceEqual(port.Bin), $"{name}: soundbank.bin differs, {FirstDifference(mm.Bin!, port.Bin!)}");
        Assert.NotNull(port.Header);
        Assert.Equal(Defines(mm.Header!), Defines(port.Header!));
        Assert.Equal(Encoding.Latin1.GetString(mm.Header!), Encoding.Latin1.GetString(port.Header!));
        var mmLines = mm.Log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        var portLines = port.Log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith("error: ", StringComparison.Ordinal) && !l.StartsWith("warning: ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(mmLines, portLines);
        return port.Port;
    }

    // ------------------------------------------------------------------ 1. runtime test audio

    static List<string> RuntimeAudioPaths() =>
        Directory.EnumerateFiles(Path.Combine(NativeBuild.RepoRoot, "runtime", "tests"), "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "audio" && (f.EndsWith(".mod", StringComparison.Ordinal) || f.EndsWith(".wav", StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(NativeBuild.RepoRoot, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();

    public static TheoryData<string> RuntimeAudioFiles() => new(RuntimeAudioPaths());

    [Theory]
    [MemberData(nameof(RuntimeAudioFiles))]
    public void RuntimeTestAudioFileIsIdentical(string file) =>
        Compare("rt_" + Regex.Replace(file, "[^A-Za-z0-9]", "_"), [Path.Combine(NativeBuild.RepoRoot, file)]);

    [Fact]
    public void AllRuntimeTestAudioInOneBankIsIdentical()
    {
        var files = RuntimeAudioPaths().Select(f => Path.Combine(NativeBuild.RepoRoot, f)).ToList();
        Assert.True(files.Count >= 6, $"found {files.Count} runtime test audio files");
        Compare("rt_all", files);
    }

    // ------------------------------------------------------------------ 2. XM from XmWriter and Sky Hopper

    [Fact]
    public void XmWriterTunesAreIdentical()
    {
        var dir = Dir("xmwriter_in");
        var inputs = new List<string>
        {
            Put(dir, "tune_c.xm", XmWriter.Tune("tune c", 128, 4, 1)),
            Put(dir, "tune_a.xm", XmWriter.Tune("tune a", 90, 6, 7, "A")),
            Put(dir, "tune_g.xm", XmWriter.Tune("tune g", 150, 3, 12, "G")),
            Put(dir, "tune_long.xm", XmWriter.Tune("tune long", 120, 16, 99, "D")),
        };
        foreach (var f in inputs) Compare("xmwriter_" + Path.GetFileNameWithoutExtension(f), [f]);
        Compare("xmwriter_all", inputs);
    }

    [Fact]
    public void SkyHopperMusicAndSoundsAreIdentical()
    {
        var p = SkyHopper.Create(Path.Combine(Root, "skyhopper_project"));
        var music = p.MusicFiles().ToList();
        var sounds = p.SoundFiles().ToList();
        Assert.Contains(music, f => f.EndsWith("bgm_forest.xm", StringComparison.Ordinal));
        Assert.Contains(music, f => f.EndsWith("jingle_win.mod", StringComparison.Ordinal));
        Assert.True(sounds.Count >= 5);
        // the order SoundbankBuilder passes them: music, then sound effects
        Compare("skyhopper", music.Concat(sounds).ToList());
        foreach (var f in music) Compare("skyhopper_" + Path.GetFileName(f).Replace('.', '_'), [f]);
    }

    // ------------------------------------------------------------------ 3. S3M and IT

    static S3MCell[,] S3MPattern(int seed, int channels, bool sparse)
    {
        var rng = new Random(seed);
        var p = new S3MCell[64, channels];
        for (var r = 0; r < 64; r++)
            for (var c = 0; c < channels; c++)
            {
                if (sparse && (r + c) % 4 != 0) continue;
                var kind = rng.Next(10);
                p[r, c] = kind switch
                {
                    0 => new S3MCell(Note: 254),                                        // note cut
                    1 => new S3MCell(Vol: rng.Next(65)),                                // volume only
                    2 => new S3MCell(Fx: 'C' - 64, Param: 0x12),                        // pattern break to row 12 (BCD)
                    3 => new S3MCell(Note: 0x40 + rng.Next(12), Inst: 1 + rng.Next(3), Fx: 'X' - 64, Param: 0x50),
                    4 => new S3MCell(Note: 0x30 + rng.Next(12), Inst: 2, Vol: 40, Fx: 'V' - 64, Param: 0x30),
                    5 => new S3MCell(Fx: 'S' - 64, Param: 0xB0),                        // pattern loop start
                    6 => new S3MCell(Fx: 'D' - 64, Param: 0x0F),
                    7 => new S3MCell(Note: 0x50 + rng.Next(12), Inst: 3, Fx: 255, Param: 9),  // fx 255 is cleared
                    _ => new S3MCell(Note: 0x20 + rng.Next(40) % 12, Inst: 1 + rng.Next(4), Vol: rng.Next(64)),
                };
            }
        return p;
    }

    static List<S3MSample> S3MSamples() =>
    [
        new("lead 8-bit loop", "lead.smp", Wave(3000, 1), Loop: true, LoopStart: 1000, LoopEnd: 2900, C2Spd: 16726),
        new("bass 16-bit", "bass.smp", Wave(4001, 2, 16), Bit16: true, Volume: 50, C2Spd: 22050),
        new("pad 16-bit loop", "pad.smp", Wave(5000, 3, 16), Bit16: true, Loop: true, LoopStart: 100, LoopEnd: 260),
        new("short loop", "short.smp", Wave(97, 4), Loop: true, LoopStart: 10, LoopEnd: 97, Volume: 30),
        new("empty", "empty.smp", []),
        new("adlib slot", "", [], Type: 2),
        new("loop past end", "past.smp", Wave(301, 5), Loop: true, LoopStart: 100, LoopEnd: 5000),
    ];

    static byte[] S3MFile(int ffi, bool stereo, bool defaultPan, int seed)
    {
        byte[]? pan = defaultPan ? Enumerable.Range(0, 32).Select(i => (byte)(i % 3 == 0 ? 0x20 | (i % 16) : i)).ToArray() : null;
        var patterns = new List<S3MCell[,]?> { S3MPattern(seed, 8, false), S3MPattern(seed + 1, 4, true), new S3MCell[64, 4], S3MPattern(seed + 2, 16, true) };
        return S3M(S3MSamples(), patterns, [0, 1, 254, 2, 3, 1, 255, 2], ffi, stereo, null, pan, globalVolume: 48, speed: 5, tempo: 140);
    }

    [Fact]
    public void S3MFilesAreIdentical()
    {
        var dir = Dir("s3m_in");
        var unsigned = Put(dir, "unsigned.s3m", S3MFile(2, true, false, 1));
        var signed = Put(dir, "signed.s3m", S3MFile(1, true, true, 2));
        var mono = Put(dir, "mono.s3m", S3MFile(2, false, false, 3));
        var chans = Enumerable.Range(0, 32).Select(i => (byte)(i < 16 ? i : i < 20 ? 16 + i : 255)).ToArray();
        var odd = Put(dir, "channels.s3m", S3M(S3MSamples().Take(3).ToList(), [S3MPattern(9, 20, true), null], [0, 1, 0], 2, true, chans));
        foreach (var f in new[] { unsigned, signed, mono, odd }) Compare("s3m_" + Path.GetFileNameWithoutExtension(f), [f]);
    }

    static ItCell[,] ItPattern(int seed, int rows, int channels)
    {
        var rng = new Random(seed);
        var p = new ItCell[rows, channels];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < channels; c++)
            {
                if ((r * 7 + c * 3 + seed) % 5 > 1) continue;  // empty cells and empty rows
                var k = rng.Next(12);
                p[r, c] = k switch
                {
                    0 => new ItCell(Note: 255),                                    // note off
                    1 => new ItCell(Note: 254),                                    // note cut
                    2 => new ItCell(Note: 253),                                    // note fade
                    3 => new ItCell(Note: 60, Inst: 1, Vol: 64, Fx: 3, Param: 0x08),  // C08 pattern break
                    4 => new ItCell(Fx: 19, Param: 0xB0),                         // SB0 loop start
                    5 => new ItCell(Fx: 19, Param: 0xB2),
                    6 => new ItCell(Vol: 70 + rng.Next(60)),                       // volume column effects
                    7 => new ItCell(Note: 48, Inst: 2, Vol: 128 + rng.Next(64)),  // panning in the volume column
                    8 => new ItCell(Note: 60, Inst: 1, Vol: 64, Fx: 3, Param: 0x08),
                    _ => new ItCell(Note: 36 + rng.Next(48), Inst: 1 + rng.Next(4), Vol: rng.Next(65), Fx: 1 + rng.Next(26), Param: rng.Next(256)),
                };
            }
        return p;
    }

    static ushort[] NoteMap(Func<int, (int Note, int Sample)> f) => Enumerable.Range(0, 120).Select(n => { var (note, s) = f(n); return (ushort)(note | s << 8); }).ToArray();

    static List<ItInstrument> ItInstruments() =>
    [
        new("plain", NoteMap(n => (n, 1))),  // one sample, notes unchanged: a single notemap entry
        new("split", NoteMap(n => (n, n < 60 ? 2 : 3)), Nna: 3, Dct: 1, Dca: 2, Fadeout: 300, GlobalVolume: 100, DefaultPan: 48, RandomVolume: 10,
            Volume: new(1 | 2 | 4, [(64, 0), (40, 10), (48, 30), (0, 1000)], LoopStart: 1, LoopEnd: 2, SusStart: 1, SusEnd: 1),
            Pan: new(1, [(-32, 0), (32, 40), (0, 80)]),
            Pitch: new(1 | 128, [(0, 0), (20, 5), (-20, 900), (5, 901), (6, 901)])),
        new("transposed", NoteMap(n => (Math.Min(119, n + 12), 4)), Volume: new(0, [(64, 0), (0, 64)]), Pan: new(0, [])),
        new("one node env", NoteMap(n => (n, 1)), Volume: new(1, [(32, 0)]), Pan: new(1, [(10, 0), (12, 2)]), Pitch: new(0, [(1, 1), (2, 2)])),
        new("empty map", NoteMap(_ => (0, 0))),
    ];

    static List<ItSample> ItSamples(bool compressed) =>
    [
        new("lead", "lead.wav", Wave(2000, 11), Loop: true, LoopStart: 500, LoopEnd: 1999, C5: 22050, Compressed: compressed, VibSpeed: 3, VibDepth: 4, VibRate: 5, VibType: 1),
        new("bass16", "bass.wav", Wave(3000, 12, 16), Bit16: true, Compressed: compressed, DefaultPan: 32 | 128, Volume: 48),
        new("pingpong", "pp.wav", Wave(1200, 13), Loop: true, PingPong: true, LoopStart: 300, LoopEnd: 700, Compressed: compressed, DefaultPan: 64),
        new("unsigned16 pp", "u16.wav", Wave(999, 14, 16), Bit16: true, Signed: false, Loop: true, PingPong: true, LoopStart: 10, LoopEnd: 90, DefaultPan: 0xA0),
        new("unsigned8", "u8.wav", Wave(777, 15), Signed: false, Loop: true, LoopStart: 0, LoopEnd: 777, C5: 11025),
        new("empty", "empty.wav", [], Loop: true, LoopStart: 3, LoopEnd: 9),
    ];

    static byte[] ItFile(bool instruments, bool compressed, int cmwt, int seed, int flags = 1 | 8)
    {
        var pan = Enumerable.Range(0, 64).Select(i => (byte)(i % 4 == 0 ? 0 : i % 4 == 1 ? 64 : i % 4 == 2 ? 100 : 32 | 128)).ToArray();
        var vol = Enumerable.Range(0, 64).Select(i => (byte)(64 - i % 40)).ToArray();
        List<(int, ItCell[,])?> patterns = [(64, ItPattern(seed, 64, 8)), (32, ItPattern(seed + 1, 32, 12)), null, (200, ItPattern(seed + 2, 200, 4)), (64, new ItCell[64, 4])];
        return It(instruments ? ItInstruments() : null, ItSamples(compressed), patterns, [0, 1, 2, 254, 3, 4, 0, 255, 1], cmwt, flags, pan, vol, 100, 4, 160);
    }

    [Fact]
    public void ItFilesAreIdentical()
    {
        var dir = Dir("it_in");
        var files = new[]
        {
            Put(dir, "instruments.it", ItFile(true, false, 0x214, 1)),
            Put(dir, "samples_only.it", ItFile(false, false, 0x214, 2, flags: 1 | 16 | 32)),
            Put(dir, "compressed214.it", ItFile(true, true, 0x214, 3)),
            Put(dir, "compressed215.it", ItFile(true, true, 0x215, 4, flags: 0)),
        };
        foreach (var f in files) Compare("it_" + Path.GetFileNameWithoutExtension(f), [f]);
        // a channel above 32: mmutil stops reading that pattern (the port warns)
        var forty = Put(dir, "forty.it", It(null, ItSamples(false), [(64, ItPattern(5, 64, 40)), (64, ItPattern(6, 64, 8))], [0, 1]));
        Assert.Single(Compare("it_forty", [forty]).Warnings);
    }

    [Fact]
    public void ItCompressedLongSamplesSpanSeveralBlocksIdentically()
    {
        var dir = Dir("it_long_in");
        List<ItSample> samples =
        [
            new("long8", "l8.wav", Wave(70000, 21), Compressed: true, Loop: true, LoopStart: 100, LoopEnd: 69999),
            new("long16", "l16.wav", Wave(40000, 22, 16), Bit16: true, Compressed: true),
            new("noise16", "n16.wav", Wave(17000, 23, 16, 1.0).Select((v, i) => i % 2 == 0 ? v : -v).ToArray(), Bit16: true, Compressed: true),
            new("noise8", "n8.wav", Wave(33000, 24, 8, 1.0).Select((v, i) => i % 3 == 0 ? v : -v / 2).ToArray(), Compressed: true),
        ];
        var p = new ItCell[64, 2];
        p[0, 0] = new ItCell(60, 1, 64);
        p[0, 1] = new ItCell(60, 2, 64);
        foreach (var (name, cmwt) in new[] { ("long214.it", 0x214), ("long215.it", 0x215) })
            Compare("it_" + Path.GetFileNameWithoutExtension(name), [Put(dir, name, It(null, samples, [(64, p)], [0], cmwt))]);
    }

    // ------------------------------------------------------------------ XM and MOD beyond the generated ones

    static XmCell[,] XmPattern(int seed, int rows, int channels, int instruments)
    {
        var rng = new Random(seed);
        var p = new XmCell[rows, channels];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < channels; c++)
            {
                if ((r + c * 5 + seed) % 3 == 0) continue;
                var k = rng.Next(14);
                p[r, c] = k switch
                {
                    0 => new XmCell(Note: 97),                                  // key off
                    1 => new XmCell(Fx: 0xD, Param: 0x16),                      // pattern break, row 16
                    2 => new XmCell(Fx: 0xE, Param: 0x60),                      // E60 loop start
                    3 => new XmCell(Fx: 0xE, Param: rng.Next(256)),
                    4 => new XmCell(Fx: 33, Param: 0x10 + rng.Next(48)),        // X1y / X2y
                    5 => new XmCell(Vol: 0x10 + rng.Next(0xF0)),                // volume column
                    6 => new XmCell(Note: 1 + rng.Next(96), Inst: 1 + rng.Next(instruments), Fx: rng.Next(36), Param: rng.Next(256)),
                    7 => new XmCell(Fx: 0xF, Param: rng.Next(64)),
                    8 => new XmCell(Fx: 1, Param: 0xF0),
                    _ => new XmCell(Note: 1 + rng.Next(96), Inst: 1 + rng.Next(instruments), Vol: 0x10 + rng.Next(65)),
                };
            }
        return p;
    }

    static List<XmInstrument> XmInstruments() =>
    [
        new("lead", [new("lead8", Wave(1500, 31), LoopType: 1, LoopStart: 200, LoopLength: 1000, Finetune: -16, RelNote: 12, Panning: 200)],
            Volume: new([0, 64, 10, 40, 30, 50, 200, 0], Sustain: 1, LoopStart: 1, LoopEnd: 2), VibType: 1, VibSweep: 10, VibDepth: 4, VibRate: 8, Fadeout: 1024),
        new("multi", [new("low16", Wave(2000, 32, 16), Bit16: true, RelNote: -12, Finetune: 100),
                      new("high16pp", Wave(800, 33, 16), Bit16: true, LoopType: 2, LoopStart: 100, LoopLength: 300, Volume: 40),
                      new("odd8", Wave(33, 34), LoopType: 1, LoopStart: 1, LoopLength: 31)],
            KeyMap: Enumerable.Range(0, 96).Select(n => (byte)(n < 30 ? 0 : n < 60 ? 1 : 2)).ToArray(),
            Volume: new([0, 64, 4, 64, 8, 0], On: true), Pan: new([0, 0, 20, 64, 40, 32], Sustain: 2), Fadeout: 33),
        new("no samples", []),
        new("empty16", [new("empty", [], Bit16: true), new("bidi8", Wave(600, 35), LoopType: 2, LoopStart: 0, LoopLength: 600, RelNote: 7)], VibSweep: 255),
        new("loop3", [new("looptype3", Wave(900, 36), LoopType: 3, LoopStart: 100, LoopLength: 200)]),
    ];

    [Fact]
    public void XmFixturesWith16BitPingPongAndEnvelopesAreIdentical()
    {
        var dir = Dir("xm_in");
        var ins = XmInstruments();
        var a = Put(dir, "envelopes.xm", Xm(8, ins, [XmPattern(1, 64, 8, ins.Count), XmPattern(2, 32, 8, ins.Count), new XmCell[16, 8], XmPattern(3, 256, 8, ins.Count)], [0, 1, 2, 3, 1, 0], restart: 2));
        var b = Put(dir, "amiga.xm", Xm(4, ins.Take(2).ToList(), [XmPattern(4, 64, 4, 2)], [0, 0], linear: false, speed: 3, bpm: 200));
        var c = Put(dir, "wide.xm", Xm(32, ins.Skip(1).ToList(), [XmPattern(5, 48, 32, 4), XmPattern(6, 1, 32, 4)], [1, 0, 1]));
        // more than 32 channels: mmutil writes channel 32+ into the next row's cells (rows are 32 cells apart)
        var d = Put(dir, "forty.xm", Xm(40, ins.Take(2).ToList(), [XmPattern(7, 64, 40, 2)], [0]));
        foreach (var f in new[] { a, b, c, d }) Compare("xm_" + Path.GetFileNameWithoutExtension(f), [f]);
    }

    static (int, int, int, int)[,] ModPattern(int seed, int channels)
    {
        var rng = new Random(seed);
        int[] periods = [856, 808, 762, 720, 678, 640, 604, 570, 538, 508, 480, 453, 428, 404, 381, 360, 339, 113, 57, 1712, 4000, 1, 0];
        var p = new (int, int, int, int)[64, channels];
        for (var r = 0; r < 64; r++)
            for (var c = 0; c < channels; c++)
            {
                if ((r + c + seed) % 3 == 0) continue;
                var fx = rng.Next(16);
                var param = rng.Next(256);
                if (r == 40 && c == 0) (fx, param) = (0xD, 0x21);  // pattern break to row 21
                p[r, c] = (periods[rng.Next(periods.Length)], rng.Next(6), fx, param);
            }
        return p;
    }

    static List<ModSample> ModSamples() =>
    [
        new("square", Enumerable.Range(0, 64).Select(i => i < 32 ? 100 : -100).ToArray(), Finetune: 3, LoopStart: 0, LoopLength: 64),
        new("noise", Wave(5000, 41), Finetune: -8, Volume: 40),
        new("loop", Wave(4000, 42), Finetune: 7, LoopStart: 1000, LoopLength: 2000),
        new("tiny loop", Wave(200, 43), LoopStart: 10, LoopLength: 2),
        new("loop past end", Wave(300, 44), LoopStart: 200, LoopLength: 400),
    ];

    [Fact]
    public void ModVariantsAreIdentical()
    {
        var dir = Dir("mod_in");
        var files = new[]
        {
            Put(dir, "mk.mod", Mod("M.K.", 4, ModSamples(), [ModPattern(1, 4), ModPattern(2, 4)], [0, 1, 1, 0], restart: 1)),
            Put(dir, "six.mod", Mod("6CHN", 6, ModSamples(), [ModPattern(3, 6)], [0], restart: 127)),
            Put(dir, "twelve.mod", Mod("12CH", 12, ModSamples().Take(3).ToList(), [ModPattern(4, 12), ModPattern(5, 12), ModPattern(6, 12)], [2, 0, 1])),
            Put(dir, "one.mod", Mod("1CHN", 1, ModSamples().Take(2).ToList(), [ModPattern(7, 1)], [0])),
        };
        foreach (var f in files) Compare("mod_" + Path.GetFileNameWithoutExtension(f), [f]);
    }

    // ------------------------------------------------------------------ 4. several inputs in one run

    [Fact]
    public void MixedInputsShareSamplesAndNamesIdentically()
    {
        var dir = Dir("mixed_in");
        var shared = Wave(1000, 51);
        // the same sample data in an S3M, an IT, an XM and a WAV: module samples reuse the first one in the bank
        var s3m = Put(dir, "shared.s3m", S3M([new("shared", "a.smp", shared, Loop: true, LoopStart: 0, LoopEnd: 1000), new("other", "b.smp", Wave(500, 52))],
            [S3MPattern(1, 4, false)], [0]));
        var it = Put(dir, "shared.it", It(null, [new("shared", "a.wav", shared, Loop: true, LoopStart: 0, LoopEnd: 1000), new("x", "x.wav", Wave(64, 53))],
            [(64, ItPattern(2, 64, 4))], [0]));
        var xm = Put(dir, "shared.xm", Xm(4, [new("shared", [new("shared", shared, LoopType: 1, LoopStart: 0, LoopLength: 1000)])], [XmPattern(3, 64, 4, 1)], [0]));
        var wavShared = Put(dir, "shared_loop.wav", Wav([shared], 8, 8000, (0, 0, 1000)));
        var wav = Put(dir, "sfx/beep.wav", Wav([Wave(1001, 54)], 8, 11025));
        var tuneA = Put(dir, "a/tune.mod", Mod("M.K.", 4, ModSamples(), [ModPattern(8, 4)], [0]));
        var tuneB = Put(dir, "b/tune.mod", Mod("M.K.", 4, ModSamples(), [ModPattern(9, 4)], [0]));
        var dash = Put(dir, "my-song.xm", XmWriter.Tune("my song", 100, 2, 5));
        var space = Put(dir, "my song.xm", XmWriter.Tune("my song", 100, 2, 5));
        var dots = Put(dir, "a.b.wav", Wav([Wave(300, 55)], 8, 11025));
        Compare("mixed", [wavShared, s3m, it, wav, xm, tuneA, tuneB, wav, dash, space, dots, s3m, Path.Combine(NativeBuild.RepoRoot, "runtime", "tests", "audio_basic", "audio", "beep.wav")]);
        Compare("mixed_reversed", [space, dash, tuneB, tuneA, xm, wav, it, s3m, wavShared]);
    }

    // ------------------------------------------------------------------ 5. edge cases

    [Fact]
    public void WavVariantsAreIdentical()
    {
        var dir = Dir("wav_in");
        var l = Wave(5001, 61, 16);
        var r = Wave(5001, 62, 16);
        var files = new List<string>
        {
            Put(dir, "mono8_odd.wav", Wav([Wave(1001, 63)], 8, 11025)),
            Put(dir, "mono16.wav", Wav([l], 16, 22050)),
            Put(dir, "stereo16.wav", Wav([l, r], 16, 32000)),                             // negative odd sums: unsigned division
            Put(dir, "stereo8.wav", Wav([Wave(999, 64), Wave(999, 65).Select(v => -v).ToArray()], 8, 8000)),
            Put(dir, "three16.wav", Wav([l, r, Wave(5001, 66, 16)], 16, 44100)),
            Put(dir, "loop_fwd.wav", Wav([Wave(3000, 67)], 8, 11025, (0, 1000, 1100))),  // short loop: unrolled to 512+
            Put(dir, "loop_bidi16.wav", Wav([Wave(3000, 68, 16)], 16, 11025, (1, 500, 2600))),
            Put(dir, "loop_tiny.wav", Wav([Wave(3000, 69)], 8, 11025, (0, 100, 110))),   // under 16: no loop
            Put(dir, "loop_past_end.wav", Wav([Wave(300, 70)], 8, 11025, (0, 100, 9000))),
            Put(dir, "loop_start_past_end.wav", Wav([Wave(300, 71)], 8, 11025, (0, 400, 500))),
            Put(dir, "loop_type_other.wav", Wav([Wave(300, 72)], 8, 11025, (5, 10, 200))),
            Put(dir, "chunks.wav", Wav([Wave(2001, 73)], 8, 11025, null, extraChunks: true, fmtSize: 18)),
            Put(dir, "empty.wav", Wav([Array.Empty<int>()], 8, 11025)),
            Put(dir, "long.wav", Wav([Wave(300000, 74)], 8, 16000)),
            Put(dir, "rate_odd.wav", Wav([Wave(10, 75)], 8, 1)),
        };
        foreach (var f in files) Compare("wav_" + Path.GetFileNameWithoutExtension(f), [f]);
        Compare("wav_all", files);
    }

    [Fact]
    public void ModuleSamplesNamedWithHashGetSfxDefinesIdentically()
    {
        var dir = Dir("hash_in");
        var s3m = Put(dir, "hash.s3m", S3M([new("kick", "#kick.raw", Wave(800, 81)), new("snare", "#Snare-2", Wave(700, 82)), new("x", "#", Wave(10, 83)), new("y", "#abcdefghijk", Wave(11, 84))],
            [S3MPattern(1, 4, false)], [0]));
        var it = Put(dir, "hash.it", It(null, [new("hat", "#hat.wav", Wave(300, 85)), new("kick again", "#kick.raw", Wave(800, 81))], [(64, ItPattern(1, 64, 4))], [0]));
        var mod = Put(dir, "hash.mod", Mod("M.K.", 4, [new("#mod sample.raw", Wave(400, 86)), new("#abcdefghijklmnop", Wave(402, 87))], [ModPattern(1, 4)], [0]));
        var xm = Put(dir, "hash.xm", Xm(2, [new("i", [new("#xm/dir\\name", Wave(100, 88))])], [XmPattern(1, 16, 2, 1)], [0]));
        Compare("hash", [s3m, it, mod, xm]);
    }

    [Fact]
    public void ZeroLengthOddLengthAndRepeatedModulesAreIdentical()
    {
        var dir = Dir("zero_in");
        // only empty samples (8-bit and 16-bit never fixed by mmutil), modules with no patterns or instruments
        var xm = Put(dir, "empty.xm", Xm(2, [new("e16", [new("e16", [], Bit16: true, LoopType: 1, LoopStart: 4, LoopLength: 8)]), new("e8", [new("e8", [])])], [new XmCell[64, 2]], [0]));
        var xmBare = Put(dir, "bare.xm", Xm(1, [], [], []));
        var s3m = Put(dir, "empty.s3m", S3M([new("e", "e", [], Bit16: true, Loop: true, LoopStart: 2, LoopEnd: 6)], [new S3MCell[64, 1]], [0]));
        var it = Put(dir, "empty.it", It(null, [new("e", "e", [], Bit16: true)], [], []));
        var odd = Put(dir, "odd.it", It(null, [new("odd", "o", Wave(3, 91)), new("odd2", "p", Wave(5, 92)), new("odd3", "q", Wave(7, 93), Loop: true, LoopStart: 1, LoopEnd: 6)], [(64, new ItCell[64, 1])], [0, 0, 0]));
        var mod = Put(dir, "empty.mod", Mod("M.K.", 4, [], [new (int, int, int, int)[64, 4]], [0]));
        Compare("zero", [xm, xmBare, s3m, it, odd, mod, xm, odd, mod]);
        Compare("zero_xm16_first", [xm, Put(dir, "silence.wav", Wav([Array.Empty<int>()], 16, 8000)), s3m]);
    }

    [Fact]
    public void InputsMmutilPassesOverGiveTheSameBankAndAnError()
    {
        var dir = Dir("odd_in");
        var good = Put(dir, "good.mod", Mod("M.K.", 4, ModSamples().Take(2).ToList(), [ModPattern(1, 4)], [0]));
        var badSig = Put(dir, "badsig.mod", Mod("FLT4", 4, ModSamples().Take(2).ToList(), [ModPattern(1, 4)], [0]));
        var notXm = Put(dir, "notxm.xm", Encoding.ASCII.GetBytes("this is not an xm file at all, not even close......................"));
        var text = Put(dir, "readme.txt", "hello"u8.ToArray());
        var notWav = Put(dir, "float.wav", Wav([Wave(100, 1)], 8, 8000).Select((b, i) => i == 20 ? (byte)3 : b).ToArray());  // format 3
        var r = Compare("odd_passed_over", [good, badSig, notXm, text, notWav, good], portSucceeds: false);
        Assert.Equal(4, r.Errors.Count);
        Assert.All(r.Errors, e => Assert.True(e.Contains("badsig.mod") || e.Contains("notxm.xm") || e.Contains("readme.txt") || e.Contains("float.wav"), e));
    }

    [Fact]
    public void LoopOfLengthZeroCrashesMmutilAndFailsThePort()
    {
        var dir = Dir("crash");
        // a looped XM sample whose loop has length 0: mmutil divides by the loop length
        var xm = Put(dir, "zero_loop.xm", Xm(1, [new("z", [new("z", Wave(100, 1), LoopType: 1, LoopStart: 10, LoopLength: 0)])], [new XmCell[64, 1]], [0]));
        var mm = RunMmutil([xm], Path.Combine(dir, "mmutil"));
        var port = RunPort([xm], Path.Combine(dir, "port"));
        Assert.False(mm.Ok, "mmutil 1.10.1 was expected to crash on a loop of length 0");
        Assert.False(port.Ok);
        Assert.Null(port.Bin);
        Assert.Contains("zero_loop.xm", port.Port!.Errors.Single());
    }

    [Fact]
    public void LongNamesWorkUpTo79CharactersAndCrashMmutilFrom80()
    {
        // MSL_PrintDefinition copies the name into char newtitle[64]; the devkitPro build survives the overflow up to 79
        var dir = Dir("long_name");
        foreach (var n in new[] { 80, 100 })
        {
            var wav = Put(dir, new string('n', n) + ".wav", Wav([Wave(100, 1)], 8, 8000));
            var mm = RunMmutil([wav], Path.Combine(dir, $"mmutil{n}"));
            var port = RunPort([wav], Path.Combine(dir, $"port{n}"));
            Assert.False(mm.Ok, $"mmutil 1.10.1 was expected to crash on a {n}-character name");
            Assert.False(port.Ok);
            Assert.Null(port.Bin);
        }
        var name = "Ab-9 x." + new string('q', 72);  // the name stops at the first '.': AB_9_X
        Compare("name_long_ok", [
            Put(dir, new string('m', 63) + ".wav", Wav([Wave(100, 1)], 8, 8000)),
            Put(dir, new string('k', 64) + ".wav", Wav([Wave(100, 2)], 8, 8000)),
            Put(dir, "x-" + new string('w', 77) + ".wav", Wav([Wave(100, 3)], 8, 8000)),
            Put(dir, new string('s', 79) + ".mod", Mod("M.K.", 4, ModSamples().Take(1).ToList(), [ModPattern(1, 4)], [0])),
            Put(dir, name + ".wav", Wav([Wave(100, 4)], 8, 8000)),
        ]);
    }

    [Fact]
    public void NonAsciiFileNameIsIdentical()
    {
        // mmutil gets the name in the ANSI code page and keeps such bytes as they are in the header
        var dir = Dir("non_ascii_in");
        Compare("non_ascii", [Put(dir, "éclair.wav", Wav([Wave(100, 1)], 8, 8000)), Put(dir, "café song.mod", Mod("M.K.", 4, ModSamples().Take(1).ToList(), [ModPattern(1, 4)], [0]))]);
    }

    [Fact]
    public void TruncatedFilesAreIdentical()
    {
        var dir = Dir("truncated_in");
        var mod = File.ReadAllBytes(Path.Combine(NativeBuild.RepoRoot, "runtime", "tests", "audio_basic", "audio", "tune.mod"));
        var wav = Wav([Wave(1000, 1)], 8, 8000);
        var inputs = new[]
        {
            Put(dir, "short_samples.mod", mod[..(mod.Length - 100)]),
            Put(dir, "short_data.wav", wav[..(wav.Length - 37)]),
        };
        // the MOD reads past its end (mmutil reads 0 there too, in this build); the WAV data chunk is clipped to the file
        Assert.Single(Compare("truncated_mod", [inputs[0]]).Warnings);
        Assert.Empty(Compare("truncated_wav", [inputs[1]]).Warnings);
    }

    [Fact]
    public void PortReportsNoInputsAndShortFirstName()
    {
        var dir = Dir("args");
        Assert.False(MaxmodSoundbank.Write([], Path.Combine(dir, "a.bin"), null).Success);
        var r = MaxmodSoundbank.Write(["x.h"], Path.Combine(dir, "b.bin"), null);
        Assert.False(r.Success);
        Assert.False(File.Exists(Path.Combine(dir, "b.bin")));
    }
}
