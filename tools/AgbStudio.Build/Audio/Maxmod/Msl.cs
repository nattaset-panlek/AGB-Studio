// Ported from Maxmod Utility (mmutil) 1.10.1, msl.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

using System.Text;

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>
/// The soundbank (MSL) builder. mmutil collects samples and modules in two temporary files in the working folder
/// (sampJ328G54AU3.tmp, songDJ34957FAI.tmp); here they are two in-memory files with the same bytes and positions.
/// </summary>
internal sealed class Msl(MaxmodLog log, bool header)
{
    const uint SampleHeaderSize = 12;  // GBA

    readonly CWriter _tmpSamp = new();
    readonly CWriter _tmpSong = new();
    readonly List<byte>? _header = header ? new List<byte>() : null;

    public ushort NSamps { get; private set; }
    public ushort NSongs { get; private set; }

    ushort AddSample(Sample samp)
    {
        var w = _tmpSamp;
        w.SeekEnd();
        var sampleLength = samp.SampleLength;
        w.Write32((samp.Is16Bit ? sampleLength * 2 : sampleLength) + SampleHeaderSize + 4);  // +4 for sample padding
        w.Write8(Mas.TypeSampleGba);
        w.Write8(Mas.Version);
        w.Write8((byte)(samp.Filename[0] == '#' ? 1 : 0));
        w.Write8(Mas.ByteSmasher);
        MasWriter.WriteSampleData(w, samp);
        NSamps++;
        return (ushort)(NSamps - 1);
    }

    /// <summary>MSL_AddSampleC: reuses a sample already in the bank when its length, loop and data are the same.</summary>
    ushort AddSampleC(Sample samp)
    {
        var fsize = _tmpSamp.Length;
        if (fsize == 0)
            return AddSample(samp);
        var f = new CTempReader(_tmpSamp);
        var sampId = 0;
        while (f.Tell() < fsize)
        {
            var hFilesize = f.Read32();
            f.Read32();
            var sampLen = f.Read32();
            var sampLlen = f.Read32();
            var sformat = f.Read8();
            f.Skip8(3);
            const byte targetSformat = Mas.SampFormatU8;

            if (samp.SampleLength == sampLen && (samp.LoopType != 0 ? samp.LoopEnd - samp.LoopStart : 0xFFFFFFFF) == sampLlen && sformat == targetSformat)
            {
                // verify sample data
                var match = true;
                uint st;
                if (samp.Is16Bit)
                {
                    for (st = 0; st < sampLen; st++)
                    {
                        if (f.Read16() != MasWriter.Data16(samp, st))
                        {
                            match = false;
                            break;
                        }
                    }
                }
                else
                {
                    for (st = 0; st < sampLen; st++)
                    {
                        if (f.Read8() != MasWriter.Data8(samp, st))
                        {
                            match = false;
                            break;
                        }
                    }
                }
                if (match)
                    return (ushort)sampId;
                f.Skip8(hFilesize - SampleHeaderSize - (st + 1));
            }
            else
            {
                f.Skip8(hFilesize - SampleHeaderSize);
            }
            sampId++;
        }
        return AddSample(samp);
    }

    ushort AddModule(MasModule mod)
    {
        for (var x = 0; x < mod.SampCount; x++)
        {
            var s = mod.Samples![x];
            var sampId = AddSampleC(s);
            if (s.Filename[0] == '#')
                PrintDefinition(SampleFilenameAfterHash(s), sampId, "SFX_");
            s.MslIndex = sampId;
        }
        _tmpSong.SeekEnd();
        MasWriter.WriteMas(_tmpSong, mod, true, log);
        NSongs++;
        return (ushort)(NSongs - 1);
    }

    // filename + 1 as a C string: char filename[12] is followed by zeroed struct padding.
    static byte[] SampleFilenameAfterHash(Sample s)
    {
        var n = Array.IndexOf(s.Filename, (byte)0);
        if (n < 0) n = s.Filename.Length;
        return s.Filename[1..Math.Max(1, n)];
    }

    /// <summary>
    /// The input path as mmutil's argv has it: in the Windows ANSI code page. Windows maps a character the code page
    /// lacks to a similar one or '?'; here it is always '?' (mmutil cannot open such a path anyway).
    /// </summary>
    static byte[] PathBytes(string path)
    {
        if (path.All(c => c < 128))
            return path.Select(c => (byte)c).ToArray();
        var ansi = OperatingSystem.IsWindows() ? CodePagesEncodingProvider.Instance.GetEncoding(0) : null;
        return ansi != null ? ansi.GetBytes(path) : path.Select(c => c < 128 ? (byte)c : (byte)'?').ToArray();
    }

    /// <summary>
    /// MSL_PrintDefinition: <c>#define PREFIX + NAME \t id \r\n</c>. NAME is the file name after the last '\' or '/',
    /// up to its first '.', in capitals, with punctuation and space as '_'.
    /// </summary>
    void PrintDefinition(byte[] filename, ushort id, string prefix)
    {
        if (filename.Length == 0)  // empty string
            return;
        var s = 0;
        for (var x = 0; x < filename.Length; x++)
            if (filename[x] == '\\' || filename[x] == '/') s = x + 1;
        var newtitle = new List<byte>();
        for (var x = s; x < filename.Length; x++)
        {
            if (filename[x] == '.') break;
            int c = filename[x];
            if (c >= 128)
            {
                // a negative char in C: toupper and the range checks below leave it as it is
                newtitle.Add((byte)c);
                continue;
            }
            if (c >= 'a' && c <= 'z') c -= 'a' - 'A';
            if (c >= ' ' && c <= '/') c = '_';
            if (c >= ':' && c <= '@') c = '_';
            if (c >= '[' && c <= '`') c = '_';
            if (c >= '{') c = '_';
            newtitle.Add((byte)c);
        }
        // char newtitle[64] in mmutil: a longer name overflows it. The devkitPro build of mmutil 1.10.1 (Windows x64)
        // still writes names of up to 79 characters and crashes from 80 on.
        if (newtitle.Count >= 80)
            throw new MaxmodStopException($"the name {prefix}{Encoding.Latin1.GetString(newtitle.ToArray())} has {newtitle.Count} characters; mmutil 1.10.1 overflows a 64-byte buffer on it and crashes from 80 on");
        if (_header == null) return;
        _header.AddRange(Encoding.ASCII.GetBytes("#define " + prefix));
        _header.AddRange(newtitle);
        _header.AddRange(Encoding.ASCII.GetBytes($"\t{id}\r\n"));
    }

    public void LoadFile(string filename)
    {
        log.File = filename;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filename);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            log.Print($"Cannot open {filename} for reading! Skipping.\n");
            log.Error("cannot open the file for reading");
            return;
        }
        var f = new CReader(bytes);
        var ext = Simple.GetExt(filename);
        switch (ext)
        {
            case Simple.InputTypeMod:
            case Simple.InputTypeS3M:
            case Simple.InputTypeXm:
            case Simple.InputTypeIt:
            {
                var mod = new MasModule();
                var err = ext switch
                {
                    Simple.InputTypeMod => ModLoader.Load(mod, f, log),
                    Simple.InputTypeS3M => S3MLoader.Load(mod, f, log),
                    Simple.InputTypeXm => XmLoader.Load(mod, f, log),
                    _ => ItLoader.Load(mod, f, log),
                };
                // mmutil ignores the result and adds whatever was loaded
                if (err != Mas.ErrNone)
                    log.Error($"{Mas.ErrorText(err)} (mmutil 1.10.1 does not stop and adds what it loaded)");
                PrintDefinition(PathBytes(filename), AddModule(mod), "MOD_");
                break;
            }
            case Simple.InputTypeWav:
            {
                var wav = new Sample();
                var err = WavLoader.Load(wav, f, true);
                if (err != WavLoader.Ok)
                    log.Error($"{WavLoader.ErrorText(err)} (mmutil 1.10.1 does not stop and adds what it loaded)");
                wav.Filename[0] = (byte)'#';  // set SFX flag
                PrintDefinition(PathBytes(filename), AddSample(wav), "SFX_");
                break;
            }
            default:
                log.Print($"Unknown file {filename}...\n");
                log.Error("unknown file type (mmutil 1.10.1 skips it)");
                break;
        }
        if (f.ReadPastEnd)
            log.Warning("the file ends before the data its header describes; mmutil 1.10.1 reads undefined bytes there, this port reads 0");
    }

    /// <summary>MSL_Export: the bank header, then every sample and module aligned to 4 bytes.</summary>
    public byte[] Export()
    {
        var w = new CWriter();
        w.Write16(NSamps);
        w.Write16(NSongs);
        foreach (var c in "*maxmod*")
            w.Write8((byte)c);

        var parapSamp = new uint[NSamps];
        var parapSong = new uint[NSongs];

        // reserve space for parapointers
        for (var x = 0; x < NSamps; x++)
            w.Write32(0xAAAAAAAA);
        for (var x = 0; x < NSongs; x++)
            w.Write32(0xAAAAAAAA);

        // copy samples
        var r = new CTempReader(_tmpSamp);
        for (var x = 0; x < NSamps; x++)
        {
            w.Align32();
            parapSamp[x] = (uint)w.Tell();
            var size = r.Read32();
            w.Write32(size);
            for (uint y = 0; y < size + 4; y++)
                w.Write8(r.Read8());
        }

        r = new CTempReader(_tmpSong);
        for (var x = 0; x < NSongs; x++)
        {
            w.Align32();
            parapSong[x] = (uint)w.Tell();
            var size = r.Read32();
            w.Write32(size);
            for (uint y = 0; y < size + 4; y++)
                w.Write8(r.Read8());
        }

        w.SeekSet(0x0C);
        for (var x = 0; x < NSamps; x++)
            w.Write32(parapSamp[x]);
        for (var x = 0; x < NSongs; x++)
            w.Write32(parapSong[x]);
        return w.ToArray();
    }

    /// <summary>The header file mmutil writes: one line per module and sound effect, then the bank totals.</summary>
    public byte[] Header()
    {
        var h = new List<byte>(_header ?? []);
        h.AddRange(Encoding.ASCII.GetBytes($"#define MSL_NSONGS\t{NSongs}\r\n"));
        h.AddRange(Encoding.ASCII.GetBytes($"#define MSL_NSAMPS\t{NSamps}\r\n"));
        h.AddRange(Encoding.ASCII.GetBytes($"#define MSL_BANKSIZE\t{NSamps + NSongs}\r\n"));
        return h.ToArray();
    }
}
