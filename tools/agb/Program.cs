using AgbStudio.Build;
using AgbStudio.Emu;

namespace AgbStudio.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: agb romtest <rom.gba> [--frames N] [--shot out.png] [--memmap]");
            return 64;
        }
        try
        {
            return args[0] switch
            {
                "romtest" => RomTest(args[1..]),
                "runtime-test" => RuntimeTest(args[1..]),
                "make-test-audio" => MakeTestAudio(args[1]),
                _ => Usage($"Unknown command '{args[0]}'."),
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 70;
        }
    }

    static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        return 64;
    }

    static int RomTest(string[] args)
    {
        var rom = args.FirstOrDefault(a => !a.StartsWith("--")) ?? throw new ArgumentException("Missing ROM path.");
        var frames = int.Parse(Option(args, "--frames") ?? "600");
        var shot = Option(args, "--shot");
        using var core = new LibretroCore();
        var dump = args.Contains("--dumpsave");
        var result = RomTestRunner.Run(core, rom, new RomTestOptions
        {
            MaxFrames = frames,
            BeforeUnload = c =>
            {
                if (!dump) return;
                var save = c.SaveRam;
                Console.WriteLine($"save ram {save.Length} bytes");
                for (var i = 0; i < Math.Min(save.Length, 64); i += 16)
                    Console.WriteLine($"{i:X4}: {Convert.ToHexString(save.Slice(i, 16))}");
            },
        });
        if (args.Contains("--memmap"))
            foreach (var r in core.Regions) Console.WriteLine($"region {r.Name,-8} 0x{r.Start:X8} {r.Length / 1024} KB");
        foreach (var line in result.Log) Console.WriteLine($"rom: {line}");
        if (shot != null) Png.WriteArgb(shot, result.LastFrame, LibretroCore.Width, LibretroCore.Height);
        Console.WriteLine(result.Error ?? $"{result.Status} after {result.Frames} frames");
        return result.Passed ? 0 : result.Status == TestStatus.Fail ? 1 : 2;
    }

    static int RuntimeTest(string[] args)
    {
        var names = args.Where(a => !a.StartsWith("--")).ToList();
        if (names.Count == 0) names = RuntimeTests.Discover().ToList();
        var build = RuntimeTests.BuildAll(names);
        if (!build.Success)
        {
            Console.WriteLine(build.Log);
            Console.WriteLine("build failed");
            return 1;
        }
        Console.WriteLine($"built {names.Count} test ROMs in {build.Elapsed.TotalSeconds:0.0} s");
        var failed = 0;
        using var core = new LibretroCore();
        foreach (var name in names)
        {
            var result = RomTestRunner.Run(core, RuntimeTests.RomPath(name), new RomTestOptions { MaxFrames = 3000 });
            if (args.Contains("--verbose") || !result.Passed)
                foreach (var line in result.Log) Console.WriteLine($"  {name}: {line}");
            Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} {name} ({result.Frames} frames){(result.Error != null ? " " + result.Error : "")}");
            if (args.Contains("--shots")) Png.WriteArgb(Path.Combine(RuntimeTests.BuildDir, name + ".png"), result.LastFrame, LibretroCore.Width, LibretroCore.Height);
            if (!result.Passed) failed++;
        }
        Console.WriteLine(failed == 0 ? $"all {names.Count} runtime tests passed" : $"{failed} of {names.Count} runtime tests failed");
        return failed == 0 ? 0 : 1;
    }

    // Writes the small original audio files used by the runtime audio test.
    static int MakeTestAudio(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "beep.wav"), AgbStudio.Build.Audio.WavWriter.Encode(AgbStudio.Build.Audio.WavWriter.Tone(880, 0.15, 11025, square: true), 11025));
        var mod = new AgbStudio.Build.Audio.ModWriter { Title = "agb test tune" };
        mod.Samples.Add(new("square", AgbStudio.Build.Audio.ModWriter.Waveform("square"), 48, 0, 32));
        mod.Samples.Add(new("tri", AgbStudio.Build.Audio.ModWriter.Waveform("triangle"), 64, 0, 32));
        var p = mod.NewPattern();
        string[] melody = ["C-2", "E-2", "G-2", "C-3", "G-2", "E-2", "D-2", "B-1"];
        for (var i = 0; i < 64; i += 8)
        {
            p[i, 0] = new(melody[i / 8], 1);
            p[i, 1] = new(i % 16 == 0 ? "C-1" : "G-1", 2);
        }
        mod.Patterns.Add(p);
        mod.Order.AddRange([0, 0]);
        File.WriteAllBytes(Path.Combine(dir, "tune.mod"), mod.Encode());
        Console.WriteLine($"wrote beep.wav and tune.mod to {dir}");
        return 0;
    }

    static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
