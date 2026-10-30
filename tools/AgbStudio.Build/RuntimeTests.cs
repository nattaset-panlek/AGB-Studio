namespace AgbStudio.Build;

/// <summary>Builds the on-ROM runtime tests in runtime/tests/&lt;name&gt;/ into one ROM each.</summary>
public static class RuntimeTests
{
    public static string BuildDir => Path.Combine(NativeBuild.RepoRoot, "build", "runtime-tests");

    public static IReadOnlyList<string> Discover() =>
        Directory.EnumerateDirectories(Path.Combine(NativeBuild.RuntimeDir, "tests"))
            .Where(d => File.Exists(Path.Combine(d, "main.cpp")))
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

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
        t.IncludeDirs.Add(Path.Combine(testsDir, name));
        return t;
    }

    public static NativeBuildResult BuildAll(IEnumerable<string>? names = null, Action<string>? onLine = null)
    {
        var targets = (names ?? Discover()).Select(n => Target(n)).ToList();
        return new NativeBuild(new Toolchain(), BuildDir).Build(targets, onLine);
    }

    public static string RomPath(string name) => Path.Combine(BuildDir, name + ".gba");
}
