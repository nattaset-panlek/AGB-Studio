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

    public static string RepoRoot => FindRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("Cannot find the AGB Studio repository root.");

    /// <summary>The folder above <paramref name="start"/> holding the repository (AGB-Studio.sln) or an installed copy
    /// (Packager.RootMarker next to bin, runtime and third_party); null when there is none.</summary>
    public static string? FindRoot(string start)
    {
        string? dir = Path.GetFullPath(start);
        while (dir != null && !File.Exists(Path.Combine(dir, "AGB-Studio.sln")) && !File.Exists(Path.Combine(dir, Packager.RootMarker))) dir = Path.GetDirectoryName(dir);
        return dir;
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
        var compileJobs = new List<(string Src, string Obj, string Flags)>();
        var linkJobs = new List<(string Elf, List<string> Objs, string Libs, string Rom, string Fix)>();
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
                compileJobs.Add((full, obj, $"{(arm ? "-marm" : "-mthumb")} {flags}"));
            }
            var elf = Path.Combine(_buildDir, t.Name + ".elf");
            var rom = Path.Combine(_buildDir, t.Name + ".gba");
            var libs = $"-L{Q(_tc.LibtoncLib)} -L{Q(_tc.LibgbaLib)} " + (t.LinkMaxmod ? "-lmm " : "") + "-ltonc";
            ninja.AppendLine($"build {E(elf)}: link {string.Join(' ', objs.Select(E))}");
            ninja.AppendLine($"  libs = {libs}");
            var h = t.Header;
            ninja.AppendLine($"build {E(rom)}: gba {E(elf)}");
            var fix = $"-t{Fix(h.Title, 12)} -c{Fix(h.GameCode, 4)} -m{Fix(h.Maker, 2)} -r{h.Version}";
            ninja.AppendLine($"  fix = {fix}");
            defaults.Add(E(rom));
            linkJobs.Add((elf, objs, libs, rom, fix));
        }
        ninja.AppendLine("default " + string.Join(' ', defaults));
        var file = Path.Combine(_buildDir, "build.ninja");
        var text = ninja.ToString();
        if (!File.Exists(file) || File.ReadAllText(file) != text) File.WriteAllText(file, text);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        // AGB_BUILD_JOBS limits parallel compiles (machines with little memory, many builds at once).
        var jobs = int.TryParse(Environment.GetEnvironmentVariable("AGB_BUILD_JOBS"), out var j) && j > 0 ? $"-j {j} " : "";
        var r = File.Exists(_tc.Ninja) || Toolchain.FindOnPath(_tc.Ninja) != null
            ? Proc.Run(_tc.Ninja, jobs + "-f build.ninja", _buildDir, TimeSpan.FromMinutes(10), onLine, cancel)
            : RunWithoutNinja(compileJobs, linkJobs, onLine, cancel);
        var first = targets[0];
        return new NativeBuildResult(r.ExitCode == 0, Path.Combine(_buildDir, first.Name + ".gba"),
            Path.Combine(_buildDir, first.Name + ".elf.map"), r.Output, sw.Elapsed);
    }

    // The same graph as build.ninja, run in-process when Ninja is not installed (a packaged editor on a machine with only
    // devkitPro): objects older than their source or any header in their .d file are compiled, several at a time, then
    // each ROM is linked and fixed when an object is newer. Lines look like Ninja's so progress parsing is the same.
    ProcResult RunWithoutNinja(List<(string Src, string Obj, string Flags)> compiles,
        List<(string Elf, List<string> Objs, string Libs, string Rom, string Fix)> links, Action<string>? onLine, CancellationToken cancel)
    {
        var log = new StringBuilder();
        var gate = new object();
        void Line(string text)
        {
            lock (gate)
            {
                log.AppendLine(text);
                onLine?.Invoke(text);
            }
        }
        var stale = compiles.Where(c => !UpToDate(c.Obj, c.Src)).ToList();
        var total = stale.Count + links.Count * 2;
        var done = 0;
        var failed = false;
        var jobs = int.TryParse(Environment.GetEnvironmentVariable("AGB_BUILD_JOBS"), out var j) && j > 0 ? j : Math.Max(1, Environment.ProcessorCount);
        Parallel.ForEach(stale, new ParallelOptions { MaxDegreeOfParallelism = jobs, CancellationToken = cancel }, c =>
        {
            if (Volatile.Read(ref failed)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(c.Obj)!);
            Line($"[{Interlocked.Increment(ref done)}/{total}] CXX {c.Src}");
            var r = Proc.Run(_tc.Gxx, $"-MMD -MF {Q(c.Obj + ".d")} {c.Flags} -c {Q(c.Src)} -o {Q(c.Obj)}", _buildDir, TimeSpan.FromMinutes(5), Line, cancel);
            if (r.ExitCode != 0)
            {
                Volatile.Write(ref failed, true);
                Line("FAILED: " + c.Obj);
            }
        });
        if (failed) return new ProcResult(1, log.ToString());
        foreach (var l in links)
        {
            var newest = l.Objs.Select(o => File.Exists(o) ? File.GetLastWriteTimeUtc(o) : DateTime.MaxValue).DefaultIfEmpty(DateTime.MinValue).Max();
            if (File.Exists(l.Rom) && File.Exists(l.Elf) && File.GetLastWriteTimeUtc(l.Elf) >= newest) continue;
            Line($"[{++done}/{total}] LINK {l.Elf}");
            var r = Proc.Run(_tc.Gxx, $"-mthumb -mcpu=arm7tdmi -specs=gba.specs {string.Join(' ', l.Objs.Select(Q))} {l.Libs} -o {Q(l.Elf)} -Wl,-Map,{Q(l.Elf + ".map")} -Wl,--gc-sections",
                _buildDir, TimeSpan.FromMinutes(5), Line, cancel);
            if (r.ExitCode != 0) return new ProcResult(1, log.ToString());
            Line($"[{++done}/{total}] GBAFIX {l.Rom}");
            r = Proc.Run(_tc.Objcopy, $"-O binary {Q(l.Elf)} {Q(l.Rom)}", _buildDir, TimeSpan.FromMinutes(2), Line, cancel);
            if (r.ExitCode == 0) r = Proc.Run(_tc.Gbafix, $"{Q(l.Rom)} {l.Fix}", _buildDir, TimeSpan.FromMinutes(2), Line, cancel);
            if (r.ExitCode != 0) return new ProcResult(1, log.ToString());
        }
        return new ProcResult(0, log.ToString());
    }

    // An object is current when it is newer than its source and every file its .d (gcc -MMD) lists.
    static bool UpToDate(string obj, string src)
    {
        if (!File.Exists(obj) || !File.Exists(obj + ".d")) return false;
        var t = File.GetLastWriteTimeUtc(obj);
        if (File.GetLastWriteTimeUtc(src) > t) return false;
        var deps = File.ReadAllText(obj + ".d").Replace("\\\r\n", " ").Replace("\\\n", " ");
        var colon = deps.IndexOf(": ", StringComparison.Ordinal);
        if (colon < 0) return false;
        foreach (var dep in SplitDeps(deps[(colon + 2)..]))
            if (!File.Exists(dep) || File.GetLastWriteTimeUtc(dep) > t) return false;
        return true;
    }

    // Make-style dependency list: spaces separate, "\ " is a space inside a path.
    static IEnumerable<string> SplitDeps(string s)
    {
        var cur = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length && s[i + 1] == ' ')
            {
                cur.Append(' ');
                i++;
            }
            else if (char.IsWhiteSpace(s[i]))
            {
                if (cur.Length > 0) yield return cur.ToString();
                cur.Clear();
            }
            else cur.Append(s[i]);
        }
        if (cur.Length > 0) yield return cur.ToString();
    }

    public static string StableHash(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) h = (h ^ c) * 16777619u;
        return h.ToString("x8");
    }

    internal static string ObjName(string src)
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
