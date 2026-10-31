using AgbStudio.Build;
using AgbStudio.Core;
using AgbStudio.Emu;

namespace AgbStudio.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Length == 0) return Usage("agb needs a command.");
        // G-ERR.01: a command without its arguments says which ones it needs instead of an index error.
        if (s_usage.TryGetValue(args[0], out var usage) && args.Length - 1 < s_required.GetValueOrDefault(args[0]))
        {
            Console.Error.WriteLine($"error: agb {args[0]} needs more arguments.");
            Console.Error.WriteLine($"usage: agb {args[0]} {usage}");
            return 64;
        }
        try
        {
            return args[0] switch
            {
                "romtest" => RomTest(args[1..]),
                "runtime-test" => RuntimeTest(args[1..]),
                "make-test-audio" => MakeTestAudio(args[1]),
                "build" => Build(args[1..]),
                "sample" => Sample(args[1..]),
                "template" => Template(args[1..]),
                "package" => Package(args.Length > 1 ? args[1] : Path.Combine(NativeBuild.RepoRoot, "build", "package")),
                "text-preview" => TextPreview(args[1..]),
                "lint" => RunLint(args.Length > 1 ? args[1] : NativeBuild.RepoRoot),
                _ => Usage($"Unknown command '{args[0]}'."),
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 70;
        }
    }

    // The commands and their arguments, for the usage text and the missing-argument check.
    static readonly Dictionary<string, string> s_usage = new()
    {
        ["build"] = "[project folder] [--release] [--clean] [--verbose]",
        ["romtest"] = "<rom.gba> [--frames N] [--shot out.png] [--memmap] [--dumpsave]",
        ["runtime-test"] = "[test ...] [--verbose] [--shots]",
        ["sample"] = "<mini|skyhopper> <folder>",
        ["template"] = "<template id> <folder> [name]",
        ["package"] = "[output folder]",
        ["lint"] = "[repository folder]",
        ["text-preview"] = "<font.ttf> <size px> <box width> <out.png> <text...>",
        ["make-test-audio"] = "<folder>",
    };

    static readonly Dictionary<string, int> s_required = new()
    {
        ["romtest"] = 1, ["sample"] = 2, ["template"] = 2, ["text-preview"] = 5, ["make-test-audio"] = 1,
    };

    static int Usage(string message)
    {
        Console.Error.WriteLine(message + " Run one of these commands:");
        foreach (var (command, arguments) in s_usage) Console.Error.WriteLine($"  agb {command} {arguments}");
        return 64;
    }

    static int RomTest(string[] args)
    {
        var rom = args.FirstOrDefault(a => !a.StartsWith("--")) ?? throw new ArgumentException($"agb romtest needs the ROM to run. usage: agb romtest {s_usage["romtest"]}");
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
            Console.WriteLine("The runtime test ROMs could not be built (the devkitARM output is above). Fix its first error, then run agb runtime-test again.");
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

    static int Build(string[] args)
    {
        var path = args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".";
        var project = AgbStudio.Core.Project.GameProject.Open(path);
        var builder = new AgbStudio.Build.Pipeline.GameBuilder(project, new Toolchain(), new ConsoleObserver(args.Contains("--verbose")))
        {
            Scripts = new AgbStudio.Scripting.ScriptBackend(),
        };
        var result = builder.Build(new AgbStudio.Build.Pipeline.BuildOptions
        {
            Clean = args.Contains("--clean"),
            Configuration = args.Contains("--release") ? AgbStudio.Core.Project.BuildConfiguration.Release : null,
        });
        foreach (var d in result.Diagnostics) Console.WriteLine($"{d.Severity.ToString().ToLowerInvariant()}: {d}");
        Console.WriteLine(result.Success
            ? $"Build OK in {result.Elapsed.TotalSeconds:0.00} s: {result.RomPath} ({new FileInfo(result.RomPath).Length / 1024} KB; {result.AssetsConverted} assets converted, {result.AssetsCached} cached)"
            : $"Build failed at {result.FailedStep}: {result.Errors.Count()} errors above. Fix them, then run agb build again.");
        return result.Success ? 0 : 1;
    }

    static int Sample(string[] args)
    {
        var kind = args[0];
        var dir = args[1];
        var p = kind switch
        {
            "mini" => AgbStudio.Build.Content.SampleProjects.CreatePlatformerMini(dir),
            "skyhopper" => AgbStudio.Build.Content.SkyHopper.Create(dir),
            _ => throw new ArgumentException($"Unknown sample '{kind}'. Samples: mini, skyhopper."),
        };
        Console.WriteLine($"created {p.ProjectPath}");
        return 0;
    }

    // agb template <id> <dir> [name]: creates a project from a New Project template (TemplateCatalog).
    static int Template(string[] args)
    {
        var t = AgbStudio.Build.Content.Templates.TemplateCatalog.Find(args[0]) ?? throw new ArgumentException(
            $"Unknown template '{args[0]}'. Templates: {string.Join(", ", AgbStudio.Build.Content.Templates.TemplateCatalog.All.Select(x => x.Id))}.");
        var p = t.Create(args[1], args.Length > 2 ? args[2] : t.Name);
        Console.WriteLine($"created {p.ProjectPath} ({AgbStudio.Build.Content.Templates.TemplateInfo.Stats(p)})");
        return 0;
    }

    // agb package [outDir]: the portable Windows zip (D-19).
    static int Package(string outDir)
    {
        var zip = Packager.Package(outDir, Console.WriteLine);
        Console.WriteLine($"package: {zip} ({new FileInfo(zip).Length / (1024 * 1024)} MB)");
        return 0;
    }

    static int RunLint(string root)
    {
        var issues = Lint.Run(root);
        foreach (var i in issues) Console.WriteLine($"{i.File}({i.Line}): {i.Rule}: {i.Text}");
        Console.WriteLine(issues.Count == 0 ? "lint: no issues" : $"lint: {issues.Count} issues");
        return issues.Count == 0 ? 0 : 1;
    }

    // agb text-preview <font.ttf> <sizePx> <boxWidth> <out.png> <text...>
    static int TextPreview(string[] args)
    {
        var ttf = Path.GetFullPath(args[0]);
        var f = new AgbStudio.Core.Project.FontFile
        {
            Name = "preview", Source = Path.GetFileName(ttf), Size = int.Parse(args[1]), CharacterSets = ["Basic Latin", "Thai", "Symbols"],
            LineHeight = int.Parse(args[1]) + 2, Baseline = int.Parse(args[1]) - 2, DropShadow = true,
        };
        var fallback = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts", "fallback-latin.ttf");
        var font = AgbStudio.Build.Fonts.FontBuilder.Build(f, Path.GetDirectoryName(ttf)!, null, fallback);
        var width = int.Parse(args[2]);
        var text = string.Join(' ', args[4..]).Replace("|", "\n");  // '|' marks a forced line break
        var layout = AgbStudio.Build.Fonts.TextLayout.Layout(text, font, width);
        var h = Math.Max(1, layout.Lines) * f.LineHeight + 4;
        var px = AgbStudio.Build.Fonts.TextRenderer.Render(layout, font, width, h, new AgbStudio.Build.Fonts.TextRenderer.Style(1, 4, 0));
        var argb = AgbStudio.Build.Fonts.TextRenderer.ToArgb(px);
        Png.WriteArgb(args[3], AgbStudio.Build.Fonts.TextRenderer.Scale(argb, width, h, 4), width * 4, h * 4);
        Console.WriteLine($"{layout.Lines} lines, widest {layout.MaxWidth} px, {font.Glyphs.Count} glyphs, missing: {string.Join(' ', font.Missing.Take(20).Select(c => $"U+{c:X4}"))}");
        Console.WriteLine($"baseline {font.Baseline}, line height {font.LineHeight}, upper vowel top {font.UpperVowelTop}, tone top {font.ToneTop}");
        return 0;
    }

    sealed class ConsoleObserver(bool verbose) : AgbStudio.Build.Pipeline.IBuildObserver
    {
        public void StepStarted(string step, string detail) => Console.WriteLine($"> {step}");
        public void StepFinished(string step, TimeSpan elapsed, bool ok, string detail) => Console.WriteLine($"  {(ok ? "done" : "FAILED")} {step}: {detail} ({elapsed.TotalSeconds:0.00} s)");
        public void FileProgress(string step, int done, int total, string file) { if (verbose) Console.WriteLine($"  {done}/{total} {file}"); }
        public void Line(string text) { if (verbose) Console.WriteLine("  " + text); }
        public void Message(AgbStudio.Build.Pipeline.Diagnostic d) { }
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
