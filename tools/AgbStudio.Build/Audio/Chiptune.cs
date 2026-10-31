namespace AgbStudio.Build.Audio;

/// <summary>Writes short original chiptune modules for templates and samples (D-20).</summary>
public static class Chiptune
{
    /// <summary>A looping 2-pattern tune: lead melody, bass and a noise hi-hat; <paramref name="orders"/> plays the two
    /// patterns in turn that many times (1 plays the first only).</summary>
    public static byte[] SimpleTune(string title, int tempoBpm, int seed = 1, int orders = 2)
    {
        var mod = new ModWriter { Title = title };
        mod.Samples.Add(new("lead", ModWriter.Waveform("pulse25"), 40, 0, 32));
        mod.Samples.Add(new("bass", ModWriter.Waveform("triangle"), 56, 0, 32));
        mod.Samples.Add(new("hat", ModWriter.Noise(600, seed), 24));
        var rng = new Random(seed);
        string[] scale = ["C-2", "D-2", "E-2", "G-2", "A-2", "C-3", "D-3", "E-3"];
        string[] bassLine = ["C-1", "A-1", "F-1", "G-1"];
        var patterns = Math.Clamp(orders, 1, 2);
        for (var pat = 0; pat < patterns; pat++)
        {
            var p = mod.NewPattern();
            var note = 4;
            for (var row = 0; row < 64; row++)
            {
                if (row % 4 == 0)
                {
                    note = Math.Clamp(note + rng.Next(-2, 3), 0, scale.Length - 1);
                    p[row, 0] = new(scale[note], 1);
                }
                if (row % 16 == 0) p[row, 1] = new(bassLine[(row / 16 + pat * 2) % 4], 2);
                if (row % 8 == 4) p[row, 2] = new("C-3", 3);
            }
            // Tempo on the first row: Fxx with xx >= 32 sets BPM.
            if (pat == 0) p[0, 3] = new(null, 0, 0xF, Math.Clamp(tempoBpm, 32, 255));
            mod.Patterns.Add(p);
        }
        for (var i = 0; i < orders; i++) mod.Order.Add(i % patterns);
        return mod.Encode();
    }
}
