using System.Text;

namespace AgbStudio.Build;

/// <summary>ROM header fields written by gbafix (Project Settings · General).</summary>
public sealed record RomHeader(string Title = "AGBGAME", string GameCode = "AGBE", string Maker = "00", int Version = 0);

public enum BuildConfig { Debug, Release }

/// <summary>One ROM to build from C++ sources.</summary>
public sealed class RomTarget
{
    public required string Name { get; init; }
    public List<string> Sources { get; } = new();
    public List<string> IncludeDirs { get; } = new();
    public List<string> Defines { get; } = new();
    public List<string> ExtraObjects { get; } = new();
    public RomHeader Header { get; init; } = new();
    public BuildConfig Config { get; init; } = BuildConfig.Debug;
    public string Optimise { get; init; } = "-O2";
    public bool LinkMaxmod { get; init; } = true;
}

public sealed record NativeBuildResult(bool Success, string RomPath, string MapPath, string Log, TimeSpan Elapsed);

/// <summary>Writes a build.ninja for one or more ROM targets and runs Ninja.</summary>
public sealed class NativeBuild
{
    readonly Toolchain _tc;
    readonly string _buildDir;

    public NativeBuild(Toolchain toolchain, string buildDir)
    {
        _tc = toolchain;
        _buildDir = Path.GetFullPath(buildDir);
    }

    public static string RepoRoot
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "AGB-Studio.sln"))) dir = Path.GetDirectoryName(dir);
            return dir ?? throw new InvalidOperationException("Cannot find the AGB Studio repository root.");
        }
    }

    public static string RuntimeDir => Path.Combine(RepoRoot, "runtime");

    /// <summary>All runtime library sources (runtime/src/**/*.cpp).</summary>
    public static IEnumerable<string> RuntimeSources(string? runtimeDir = null) =>
        Directory.EnumerateFiles(Path.Combine(runtimeDir ?? RuntimeDir, "src"), "*.cpp", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal);

    public static string CommonFlags(Toolchain tc, BuildConfig config) =>
        "-mcpu=arm7tdmi -mtune=arm7tdmi -std=c++20 -fno-exceptions -fno-rtti -fno-threadsafe-statics -fno-use-cxa-atexit " +
        "-ffunction-sections -fdata-sections -Wall -Wextra -Werror -Wno-volatile " +
        $"-isystem {Q(tc.LibtoncInclude)} -isystem {Q(tc.LibgbaInclude)} " +
        (config == BuildConfig.Debug ? "-DAGB_DEBUG=1 -g" : "-DAGB_RELEASE=1 -DNDEBUG");

    public NativeBuildResult Build(IReadOnlyList<RomTarget> targets, Action<string>? onLine = null, CancellationToken cancel = default)
    {
        Directory.CreateDirectory(_buildDir);
        var ninja = new StringBuilder();
        ninja.AppendLine("ninja_required_version = 1.10");
        ninja.AppendLine($"gxx = {Q(_tc.Gxx)}");
        ninja.AppendLine($"objcopy = {Q(_tc.Objcopy)}");
        ninja.AppendLine($"gbafix = {Q(_tc.Gbafix)}");
        ninja.AppendLine("rule cxx\n  command = $gxx -MMD -MF $out.d $flags -c $in -o $out\n  depfile = $out.d\n  deps = gcc\n  description = CXX $in");
        ninja.AppendLine("rule link\n  command = $gxx -mthumb -mcpu=arm7tdmi -specs=gba.specs $in $libs -o $out -Wl,-Map,$out.map -Wl,--gc-sections\n  description = LINK $out");
        ninja.AppendLine("rule gba\n  command = cmd /c \"$objcopy -O binary $in $out && $gbafix $out $fix\"\n  description = GBAFIX $out");

        var compiled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaults = new List<string>();
        foreach (var t in targets)
        {
            var flags = CommonFlags(_tc, t.Config) + " " + t.Optimise + " " +
                string.Join(' ', t.IncludeDirs.Select(i => "-I" + Q(Path.GetFullPath(i)))) + " " +
                string.Join(' ', t.Defines.Select(d => "-D" + d));
            var flagKey = StableHash(flags);
            var objs = new List<string>(t.ExtraObjects.Select(Path.GetFullPath));
            foreach (var src in t.Sources)
            {
                var full = Path.GetFullPath(src);
                var arm = full.EndsWith(".iwram.cpp", StringComparison.OrdinalIgnoreCase);
                var obj = Path.Combine(_buildDir, "obj", flagKey, ObjName(full));
                objs.Add(obj);
                if (!compiled.Add(obj)) continue;
                ninja.AppendLine($"build {E(obj)}: cxx {E(full)}");
                ninja.AppendLine($"  flags = {(arm ? "-marm" : "-mthumb")} {flags}");
            }
            var elf = Path.Combine(_buildDir, t.Name + ".elf");
            var rom = Path.Combine(_buildDir, t.Name + ".gba");
            var libs = $"-L{Q(_tc.LibtoncLib)} -L{Q(_tc.LibgbaLib)} " + (t.LinkMaxmod ? "-lmm " : "") + "-ltonc";
            ninja.AppendLine($"build {E(elf)}: link {string.Join(' ', objs.Select(E))}");
            ninja.AppendLine($"  libs = {libs}");
            var h = t.Header;
            ninja.AppendLine($"build {E(rom)}: gba {E(elf)}");
            ninja.AppendLine($"  fix = -t{Fix(h.Title, 12)} -c{Fix(h.GameCode, 4)} -m{Fix(h.Maker, 2)} -r{h.Version}");
            defaults.Add(E(rom));
        }
        ninja.AppendLine("default " + string.Join(' ', defaults));
        var file = Path.Combine(_buildDir, "build.ninja");
        var text = ninja.ToString();
        if (!File.Exists(file) || File.ReadAllText(file) != text) File.WriteAllText(file, text);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = Proc.Run(_tc.Ninja, "-f build.ninja", _buildDir, TimeSpan.FromMinutes(10), onLine, cancel);
        var first = targets[0];
        return new NativeBuildResult(r.ExitCode == 0, Path.Combine(_buildDir, first.Name + ".gba"),
            Path.Combine(_buildDir, first.Name + ".elf.map"), r.Output, sw.Elapsed);
    }

    public static string StableHash(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) h = (h ^ c) * 16777619u;
        return h.ToString("x8");
    }

    static string ObjName(string src)
    {
        var rel = src.Replace(':', '_').Replace('\\', '_').Replace('/', '_');
        if (rel.Length > 90) rel = rel[^90..];
        return Path.ChangeExtension(rel, ".o");
    }

    static string Fix(string s, int len)
    {
        var clean = new string(s.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_').ToArray());
        if (clean.Length == 0) clean = "0";
        return clean.Length > len ? clean[..len] : clean;
    }

    static string Q(string path) => path.Contains(' ') ? $"\"{path}\"" : path;

    /// <summary>Escapes a path for a Ninja build line.</summary>
    static string E(string path) => path.Replace("$", "$$").Replace(":", "$:").Replace(" ", "$ ");
}
