using System.Text;

namespace AgbStudio.Build.Audio;

/// <summary>Channel budget of the audio settings (G-HW.08): Maxmod mixes the project's <c>audio.channels</c> (shown on the
/// Audio page as "Maxmod · N ch"; the runtime allocates at most 16, runtime/src/audio/audio.cpp), and every music module
/// must fit in them, because Maxmod keeps one module channel per pattern channel. Sampled effects share the same mixing channels; SFX Designer effects use the four
/// DMG channels, which Maxmod never touches.</summary>
public static class AudioChannels
{
    public const int MaxMixingChannels = 16;

    /// <summary>The rates Maxmod mixes at (kHz, mm_mixmode); Project Settings · Audio <c>mixKHz</c> must be one of them.</summary>
    public static readonly int[] MixRates = [8, 10, 13, 16, 18, 21, 27, 31];

    /// <summary>The Maxmod rate nearest to <paramref name="khz"/> (a tie takes the lower one), as the runtime picks it.</summary>
    public static int NearestMixRate(int khz) => MixRates.OrderBy(r => Math.Abs(r - khz)).ThenBy(r => r).First();

    /// <summary>A problem with the mixing rate setting, or null.</summary>
    public static string? MixRateProblem(int khz) => MixRates.Contains(khz) ? null :
        $"The project mixes audio at {khz} kHz (audio.mixKHz in the project file); Maxmod mixes at {string.Join(", ", MixRates[..^1])} or {MixRates[^1]} kHz. " +
        $"Choose one of them ({NearestMixRate(khz)} kHz is the nearest).";

    /// <summary>The channels an IT song's patterns write to: its header always lists 64. Notes ringing on past their channel (new note actions) are not counted.</summary>
    static int? ItChannels(ReadOnlySpan<byte> f)
    {
        if (f.Length < 0xC0 || !f[..4].SequenceEqual("IMPM"u8)) return null;
        int U16(ReadOnlySpan<byte> s, int o) => s[o] | s[o + 1] << 8;
        int orders = U16(f, 0x20), instruments = U16(f, 0x22), samples = U16(f, 0x24), patterns = U16(f, 0x26);
        var table = 0xC0 + orders + (instruments + samples) * 4;
        var used = new bool[64];
        Span<byte> masks = stackalloc byte[64];
        for (var i = 0; i < patterns && table + i * 4 + 4 <= f.Length; i++)
        {
            var at = f[table + i * 4] | f[table + i * 4 + 1] << 8 | f[table + i * 4 + 2] << 16 | f[table + i * 4 + 3] << 24;
            if (at <= 0 || at + 8 > f.Length) continue;
            int p = at + 8, end = Math.Min(f.Length, p + U16(f, at));
            masks.Clear();
            while (p < end)
            {
                var what = f[p++];
                if (what == 0) continue;
                var ch = (what - 1) & 63;
                used[ch] = true;
                if ((what & 128) != 0 && p < end) masks[ch] = f[p++];
                var m = masks[ch];
                p += ((m & 1) != 0 ? 1 : 0) + ((m & 2) != 0 ? 1 : 0) + ((m & 4) != 0 ? 1 : 0) + ((m & 8) != 0 ? 2 : 0);
            }
        }
        return used.Count(u => u);
    }

    /// <summary>Pattern channels of a MOD, XM, S3M or IT file, or null when the format is not recognised.</summary>
    public static int? ModuleChannels(ReadOnlySpan<byte> file, string extension)
    {
        if (extension.Equals(".s3m", StringComparison.OrdinalIgnoreCase))
        {
            // "SCRM" at offset 44; 32 channel settings at offset 64, below 16 when the channel is on.
            if (file.Length < 96 || !file.Slice(44, 4).SequenceEqual("SCRM"u8)) return null;
            var on = 0;
            foreach (var c in file.Slice(64, 32)) if (c < 16) on++;
            return on;
        }
        if (extension.Equals(".it", StringComparison.OrdinalIgnoreCase)) return ItChannels(file);
        if (extension.Equals(".xm", StringComparison.OrdinalIgnoreCase))
        {
            // "Extended Module: " header; the channel count is a 16-bit value at offset 68.
            if (file.Length < 70 || !file[..17].SequenceEqual("Extended Module: "u8)) return null;
            return file[68] | (file[69] << 8);
        }
        if (extension.Equals(".mod", StringComparison.OrdinalIgnoreCase))
        {
            // 31-sample MOD: a four-letter signature at offset 1080 names the channel count.
            if (file.Length < 1084) return null;
            var sig = Encoding.ASCII.GetString(file.Slice(1080, 4));
            if (sig is "M.K." or "M!K!" or "FLT4" or "4CHN") return 4;
            if (sig == "FLT8") return 8;
            if (sig.EndsWith("CHN", StringComparison.Ordinal) && char.IsDigit(sig[0])) return sig[0] - '0';
            if ((sig.EndsWith("CH", StringComparison.Ordinal) || sig.EndsWith("CN", StringComparison.Ordinal)) && int.TryParse(sig[..2], out var n)) return n;
            return null;
        }
        return null;
    }

    /// <summary>Problems with the channel settings and the music files (empty when everything fits).</summary>
    public static List<string> Check(int mixingChannels, IEnumerable<string> musicFiles)
    {
        var problems = new List<string>();
        if (mixingChannels is < 1 or > MaxMixingChannels)
            problems.Add($"The project mixes {mixingChannels} audio channels (audio.channels in the project file); AGB Studio supports 1 to {MaxMixingChannels}.");
        foreach (var f in musicFiles)
        {
            var channels = ModuleChannels(File.ReadAllBytes(f), Path.GetExtension(f));
            if (channels is { } c && c > mixingChannels)
                problems.Add($"Music '{Path.GetFileName(f)}' plays {c} channels but the project mixes {mixingChannels} (audio.channels in the " +
                    $"project file, \"Maxmod · {mixingChannels} ch\" on the Audio page): raise it to {c} or more, or use a module with fewer channels.");
        }
        return problems;
    }
}
