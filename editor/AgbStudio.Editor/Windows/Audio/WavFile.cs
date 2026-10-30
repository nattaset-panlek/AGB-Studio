using System.Buffers.Binary;
using System.Text;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>A WAV file as mono float samples (-1..1): PCM 8 / 16 / 24 / 32-bit or IEEE float, any rate and channel count.</summary>
public sealed record WavData(float[] Samples, int Rate, int Channels, int Bits)
{
    public double Seconds => Rate > 0 ? Samples.Length / (double)Rate : 0;

    public static WavData Load(string path) => Parse(File.ReadAllBytes(path));

    public static WavData Parse(byte[] d)
    {
        if (d.Length < 12 || Encoding.ASCII.GetString(d, 0, 4) != "RIFF" || Encoding.ASCII.GetString(d, 8, 4) != "WAVE")
            throw new InvalidDataException("Not a WAV file.");
        int format = 0, channels = 1, rate = 0, bits = 8;
        var o = 12;
        byte[]? data = null;
        while (o + 8 <= d.Length)
        {
            var id = Encoding.ASCII.GetString(d, o, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o + 4));
            var body = o + 8;
            if (size < 0 || body + size > d.Length) size = d.Length - body;
            if (id == "fmt ")
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(body));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(body + 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(body + 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(body + 14));
                if (format == 0xFFFE && size >= 26) format = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(body + 24));
            }
            else if (id == "data") data = d.AsSpan(body, size).ToArray();
            o = body + size + (size & 1);
        }
        if (data is null || rate <= 0 || channels <= 0) throw new InvalidDataException("The WAV file has no audio data.");
        if (format is not (1 or 3)) throw new InvalidDataException("Only PCM and float WAV files can be imported.");
        var bytes = bits / 8;
        var frames = data.Length / (bytes * channels);
        var mono = new float[frames];
        for (var f = 0; f < frames; f++)
        {
            float sum = 0;
            for (var c = 0; c < channels; c++)
            {
                var p = (f * channels + c) * bytes;
                sum += (format, bits) switch
                {
                    (1, 8) => (data[p] - 128) / 128f,
                    (1, 16) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(p)) / 32768f,
                    (1, 24) => ((data[p] | (data[p + 1] << 8) | ((sbyte)data[p + 2] << 16))) / 8388608f,
                    (1, 32) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(p)) / 2147483648f,
                    (3, 32) => BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(p)),
                    _ => throw new InvalidDataException($"{bits}-bit WAV files are not supported."),
                };
            }
            mono[f] = sum / channels;
        }
        return new WavData(mono, rate, channels, bits);
    }

    /// <summary>Linear resampling to <paramref name="rate"/>.</summary>
    public float[] Resample(int rate)
    {
        if (rate == Rate) return (float[])Samples.Clone();
        var n = (int)Math.Round(Samples.Length * (double)rate / Rate);
        var r = new float[n];
        for (var i = 0; i < n; i++)
        {
            var x = i * (double)Rate / rate;
            var k = (int)x;
            var a = k < Samples.Length ? Samples[k] : 0;
            var b = k + 1 < Samples.Length ? Samples[k + 1] : a;
            r[i] = (float)(a + (b - a) * (x - k));
        }
        return r;
    }

    /// <summary>The 11 kHz mono 8-bit form sound effects are stored in (D-14), as signed bytes.</summary>
    public sbyte[] ToGba8() => [.. Resample(11025).Select(v => (sbyte)Math.Clamp(Math.Round(v * 127), -127, 127))];
}
