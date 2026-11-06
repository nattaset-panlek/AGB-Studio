using System.Text;
using AgbStudio.Build;
using AgbStudio.Build.Audio;

namespace AgbStudio.Tests;

/// <summary>MP3, OGG and MIDI become XM songs mmutil accepts; S3M and IT reach the soundbank as they are.</summary>
public sealed class AudioConverterTests
{
    static string Fixture(string name) => Path.Combine(NativeBuild.RepoRoot, "tests", "AgbStudio.Tests", "Fixtures", "audio", name);

    static string TempDir(string name)
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "audio_" + name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void AssertBuilds(string name, params (string File, byte[] Data)[] songs)
    {
        var dir = TempDir(name);
        var inputs = new List<string>();
        foreach (var (file, data) in songs)
        {
            inputs.Add(Path.Combine(dir, file));
            File.WriteAllBytes(inputs[^1], data);
        }
        var result = SoundbankBuilder.Build(inputs, Path.Combine(dir, "out"));
        Assert.True(result.Success, result.Log);
        Assert.Equal(songs.Length, result.Music.Count);
        Assert.All(result.Music, m => Assert.True(m.Orders > 0));
    }

    [Theory]
    [InlineData("tone.mp3")]
    [InlineData("tone.ogg")]
    public void Streams_BecomeAnXmSongOfTheRightLength(string file)
    {
        var path = Fixture(file);
        Assert.InRange(AudioConverter.StreamSeconds(path), 0.9, 1.2);
        var song = AudioConverter.StreamToXm(path);
        Assert.Equal(2, AudioChannels.ModuleChannels(song.Xm, ".xm"));
        // 1 s at 16726 Hz, one byte a sample, plus the headers
        Assert.InRange(song.Xm.Length, 16000, 24000);
        AssertBuilds("stream_" + Path.GetExtension(file).TrimStart('.'), (Path.ChangeExtension(file, ".xm"), song.Xm));
    }

    [Theory]
    [InlineData("junk.mp3")]
    [InlineData("junk.ogg")]
    public void JunkStream_IsRefusedInWords(string file)
    {
        var path = Path.Combine(TempDir("junk_" + Path.GetExtension(file).TrimStart('.')), file);
        File.WriteAllBytes(path, [0x49, 0x44, 0x33, 3, 0, 0, 0, 0, 0, 0]);
        Assert.Throws<InvalidDataException>(() => AudioConverter.StreamSeconds(path));
    }

    /// <summary>A format 1 MIDI: a tempo track and a track of (key, start, length in ticks) at 96 ticks a quarter.</summary>
    public static byte[] Midi(int bpm, params (int Key, int Start, int Length)[] notes)
    {
        using var ms = new MemoryStream();
        void Chunk(string id, byte[] body)
        {
            ms.Write(Encoding.ASCII.GetBytes(id));
            ms.Write([(byte)(body.Length >> 24), (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length]);
            ms.Write(body);
        }
        static byte[] Delta(int v)
        {
            var bytes = new List<byte> { (byte)(v & 0x7F) };
            while ((v >>= 7) > 0) bytes.Insert(0, (byte)(v & 0x7F | 0x80));
            return [.. bytes];
        }
        Chunk("MThd", [0, 1, 0, 2, 0, 96]);
        var micros = 60000000 / bpm;
        Chunk("MTrk", [0, 0xFF, 0x51, 3, (byte)(micros >> 16), (byte)(micros >> 8), (byte)micros, 0, 0xFF, 0x2F, 0]);
        var events = notes.SelectMany(n => new[] { (Tick: n.Start, On: true, n.Key), (Tick: n.Start + n.Length, On: false, n.Key) })
            .OrderBy(e => e.Tick).ThenBy(e => e.On).ToList();
        var track = new List<byte>();
        var last = 0;
        foreach (var e in events)
        {
            track.AddRange(Delta(e.Tick - last));
            last = e.Tick;
            track.AddRange([(byte)(e.On ? 0x90 : 0x80), (byte)e.Key, 100]);
        }
        track.AddRange([0, 0xFF, 0x2F, 0]);
        Chunk("MTrk", [.. track]);
        return ms.ToArray();
    }

    [Fact]
    public void Midi_BecomesAnXmSongOnTheRightRows()
    {
        var song = AudioConverter.MidiToXm(Midi(140, (60, 0, 96), (64, 96, 96), (67, 96, 96)), "tune");
        Assert.Equal(0, song.DroppedNotes);
        Assert.Equal(2, AudioChannels.ModuleChannels(song.Xm, ".xm"));
        Assert.Equal(140, BitConverter.ToUInt16(song.Xm, 78));   // BPM follows the song
        Assert.Equal(3, BitConverter.ToUInt16(song.Xm, 76));     // speed 3: eight rows a quarter
        var pattern = 60 + 276 + 9;                              // the first pattern's cells
        Assert.Equal(49, song.Xm[pattern]);                      // key 60 is C-4, row 0
        Assert.Equal(67 - 11, song.Xm[pattern + 8 * 2 * 5]);      // row 8: the channel it left takes the highest new note
        Assert.Equal(64 - 11, song.Xm[pattern + 8 * 2 * 5 + 5]); // and the next channel the other
        Assert.Equal(97, song.Xm[pattern + 16 * 2 * 5]);         // both end on row 16
        AssertBuilds("midi", ("tune.xm", song.Xm));
    }

    [Fact]
    public void Midi_MoreThanEightNotesAtOnce_LeavesTheRestOutAndSaysSo()
    {
        var chord = Enumerable.Range(0, 10).Select(i => (60 + i, 0, 96)).ToArray();
        var song = AudioConverter.MidiToXm(Midi(120, chord), "chord");
        Assert.Equal(2, song.DroppedNotes);
        Assert.Equal(8, AudioChannels.ModuleChannels(song.Xm, ".xm"));
    }

    [Fact]
    public void Midi_NotAMidiFile_IsRefusedInWords() =>
        Assert.Contains("not a MIDI song", Assert.Throws<InvalidDataException>(() => AudioConverter.MidiToXm(new byte[20], "x")).Message);

    /// <summary>A small S3M: 4 channels, one signed 8-bit sample, one pattern with one note.</summary>
    public static byte[] S3m()
    {
        var d = new byte[64 * 1024];
        Encoding.ASCII.GetBytes("test").CopyTo(d, 0);
        d[28] = 0x1A; d[29] = 16;
        d[32] = 2;              // orders
        d[34] = 1;              // samples
        d[36] = 1;              // patterns
        d[40] = 0x20; d[41] = 0x13;
        d[42] = 2;              // unsigned samples
        Encoding.ASCII.GetBytes("SCRM").CopyTo(d, 44);
        d[48] = 64; d[49] = 6; d[50] = 125; d[51] = 0xB0;
        for (var i = 0; i < 32; i++) d[64 + i] = (byte)(i < 4 ? i : 255);
        d[96] = 0; d[97] = 255;
        const int sample = 0x10, pattern = 0x20, data = 0x30;
        BitConverter.GetBytes((ushort)sample).CopyTo(d, 98);
        BitConverter.GetBytes((ushort)pattern).CopyTo(d, 100);
        var h = sample * 16;
        d[h] = 1;
        d[h + 14] = data;
        BitConverter.GetBytes(256).CopyTo(d, h + 16);
        BitConverter.GetBytes(256).CopyTo(d, h + 24);
        d[h + 28] = 64; d[h + 31] = 1;
        BitConverter.GetBytes(8363).CopyTo(d, h + 32);
        Encoding.ASCII.GetBytes("SCRS").CopyTo(d, h + 76);
        var p = pattern * 16 + 2;
        d[p++] = 0x20; d[p++] = 0x40; d[p++] = 1;   // channel 0: C-4, sample 1
        for (var row = 0; row < 64; row++) d[p++] = 0;
        BitConverter.GetBytes((ushort)(p - pattern * 16)).CopyTo(d, pattern * 16);
        for (var i = 0; i < 256; i++) d[data * 16 + i] = (byte)(128 + (i < 128 ? 60 : -60));
        return d[..(data * 16 + 256)];
    }

    /// <summary>A small IT: one uncompressed sample, one pattern writing channels 0 and 5.</summary>
    public static byte[] It()
    {
        const int order = 0xC0, samplePtr = order + 2, patternPtr = samplePtr + 4, sample = patternPtr + 4, pattern = sample + 0x50, patternData = 8;
        var cells = new byte[] { 0x81, 1 | 2, 60, 1, 0x86, 1 | 2, 64, 1, 0 };
        for (var i = 0; i < 63; i++) cells = [.. cells, (byte)0];
        var data = pattern + patternData + cells.Length;
        var d = new byte[data + 256];
        Encoding.ASCII.GetBytes("IMPM").CopyTo(d, 0);
        d[0x20] = 2;     // orders
        d[0x24] = 1;     // samples
        d[0x26] = 1;     // patterns
        d[0x28] = 0x14; d[0x29] = 2;
        d[0x2A] = 0x14; d[0x2B] = 2;
        d[0x2C] = 9;     // stereo, linear slides
        d[0x30] = 128; d[0x31] = 48; d[0x32] = 6; d[0x33] = 125;
        for (var i = 0; i < 64; i++) { d[0x40 + i] = 32; d[0x80 + i] = 64; }
        d[order] = 0; d[order + 1] = 255;
        BitConverter.GetBytes(sample).CopyTo(d, samplePtr);
        BitConverter.GetBytes(pattern).CopyTo(d, patternPtr);
        Encoding.ASCII.GetBytes("IMPS").CopyTo(d, sample);
        d[sample + 0x11] = 64;
        d[sample + 0x12] = 1 | 16;
        d[sample + 0x13] = 64;
        d[sample + 0x2E] = 1;
        BitConverter.GetBytes(256).CopyTo(d, sample + 0x30);
        BitConverter.GetBytes(256).CopyTo(d, sample + 0x38);
        BitConverter.GetBytes(8363).CopyTo(d, sample + 0x3C);
        BitConverter.GetBytes(data).CopyTo(d, sample + 0x48);
        BitConverter.GetBytes((ushort)cells.Length).CopyTo(d, pattern);
        BitConverter.GetBytes((ushort)64).CopyTo(d, pattern + 2);
        cells.CopyTo(d, pattern + patternData);
        for (var i = 0; i < 256; i++) d[data + i] = (byte)(i < 128 ? 60 : -60);
        return d;
    }

    [Fact]
    public void S3mAndIt_ReportTheirChannels_AndMmutilBuildsThem()
    {
        Assert.Equal(4, AudioChannels.ModuleChannels(S3m(), ".s3m"));
        Assert.Equal(2, AudioChannels.ModuleChannels(It(), ".it"));
        AssertBuilds("trackers", ("one.s3m", S3m()), ("two.it", It()));
    }
}
