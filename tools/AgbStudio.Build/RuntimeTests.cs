namespace AgbStudio.Build;

/// <summary>Builds the on-ROM runtime tests in runtime/tests/&lt;name&gt;/ into one ROM each.</summary>
public static class RuntimeTests
{
    public static string BuildDir => Path.Combine(NativeBuild.RepoRoot, "build", "runtime-tests");

    public static IReadOnlyList<string> Discover() =>
        Directory.EnumerateDirectories(Path.Combine(NativeBuild.RuntimeDir, "tests"))
            .Where(d => File.Exists(Path.Combine(d, "main.cpp")) && !Path.GetFileName(d).StartsWith("host_", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    // Folders named host_* are ROMs driven by a C# test (they never finish on their own).
    public static IReadOnlyList<string> DiscoverHostDriven() =>
        Directory.EnumerateDirectories(Path.Combine(NativeBuild.RuntimeDir, "tests"), "host_*").Select(Path.GetFileName).OfType<string>().ToList();

    public static RomTarget Target(string name, BuildConfig config = BuildConfig.Debug)
    {
        var testsDir = Path.Combine(NativeBuild.RuntimeDir, "tests");
        var t = new RomTarget
        {
            Name = name,
            Config = config,
            Header = new RomHeader("AGBTEST", "ATST", "00", 0),
        };
        t.Sources.AddRange(NativeBuild.RuntimeSources());
        t.Sources.Add(Path.Combine(testsDir, "test.cpp"));
        t.Sources.AddRange(Directory.EnumerateFiles(Path.Combine(testsDir, name), "*.cpp").OrderBy(p => p, StringComparer.Ordinal));
        t.IncludeDirs.Add(Path.Combine(NativeBuild.RuntimeDir, "include"));
        // Tests with an audio/ folder get a soundbank (soundbank.hpp lists the ids).
        var audioDir = Path.Combine(testsDir, name, "audio");
        if (Directory.Exists(audioDir))
        {
            var gen = Path.Combine(BuildDir, "gen", name);
            var inputs = Directory.EnumerateFiles(audioDir).Where(f => f.EndsWith(".mod") || f.EndsWith(".xm") || f.EndsWith(".wav")).Order().ToList();
            var sb = Audio.SoundbankBuilder.Build(new Toolchain(), inputs, gen);
            if (!sb.Success)
                throw new InvalidOperationException($"mmutil could not make the soundbank of runtime test '{name}' from {audioDir}: " +
                    $"{sb.Log.Trim().Split('\n')[^1].Trim().TrimEnd('.')}. Check that the files there are valid MOD, XM or WAV and that devkitPro's tools folder has mmutil.");
            t.Sources.Add(sb.CppPath);
            t.IncludeDirs.Add(gen);
        }
        return t;
    }

    public static NativeBuildResult BuildAll(IEnumerable<string>? names = null, Action<string>? onLine = null)
    {
        var targets = (names ?? Discover()).Select(n => Target(n)).ToList();
        return new NativeBuild(new Toolchain(), BuildDir).Build(targets, onLine);
    }

    public static string RomPath(string name) => Path.Combine(BuildDir, name + ".gba");
}
