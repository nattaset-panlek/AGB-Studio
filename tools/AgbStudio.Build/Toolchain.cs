namespace AgbStudio.Build;

/// <summary>Status of one tool or library, as shown in Preferences and Project Settings · Build.</summary>
public sealed record ToolStatus(string Name, string Path, bool Found, string? Version = null);

/// <summary>Locates devkitPro and the host tools the build needs.</summary>
public sealed class Toolchain
{
    public string DevkitPro { get; }
    public string DevkitArm => Path.Combine(DevkitPro, "devkitARM");
    public string GccBin => Path.Combine(DevkitArm, "bin");
    public string Gxx => Path.Combine(GccBin, "arm-none-eabi-g++.exe");
    public string Gcc => Path.Combine(GccBin, "arm-none-eabi-gcc.exe");
    public string Objcopy => Path.Combine(GccBin, "arm-none-eabi-objcopy.exe");
    public string Nm => Path.Combine(GccBin, "arm-none-eabi-nm.exe");
    public string Gbafix => Path.Combine(DevkitPro, "tools", "bin", "gbafix.exe");
    public string Mmutil => Path.Combine(DevkitPro, "tools", "bin", "mmutil.exe");
    public string LibtoncInclude => Path.Combine(DevkitPro, "libtonc", "include");
    public string LibtoncLib => Path.Combine(DevkitPro, "libtonc", "lib");
    public string LibgbaInclude => Path.Combine(DevkitPro, "libgba", "include");
    public string LibgbaLib => Path.Combine(DevkitPro, "libgba", "lib");
    public string Ninja { get; init; }  // when missing, NativeBuild runs the same graph in-process

    public Toolchain(string? devkitPro = null)
    {
        DevkitPro = devkitPro ?? Detect() ?? @"C:\devkitPro";
        if (DevkitPro.StartsWith('/') && DevkitPro.Length > 2 && DevkitPro[2] == '/')
            DevkitPro = $"{char.ToUpperInvariant(DevkitPro[1])}:{DevkitPro[2..].Replace('/', '\\')}";
        Ninja = FindOnPath("ninja.exe") ?? "ninja.exe";
    }

    /// <summary>The devkitPro folder: DEVKITPRO (the installer sets "/opt/devkitpro", its MSYS2 name for C:\devkitPro),
    /// then C:\devkitPro and D:\devkitPro; null when none has devkitARM.</summary>
    public static string? Detect()
    {
        var env = Environment.GetEnvironmentVariable("DEVKITPRO");
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(env))
        {
            if (env.StartsWith('/') && env.Length > 2 && env[2] == '/') candidates.Add($"{char.ToUpperInvariant(env[1])}:{env[2..].Replace('/', '\\')}");
            else if (!env.StartsWith('/')) candidates.Add(env);
        }
        candidates.AddRange([@"C:\devkitPro", @"D:\devkitPro"]);
        return candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "devkitARM", "bin", "arm-none-eabi-g++.exe")));
    }

    public IReadOnlyList<ToolStatus> Check()
    {
        var list = new List<ToolStatus>
        {
            new("devkitPro", DevkitPro, Directory.Exists(DevkitPro)),
            new("devkitARM", Gxx, File.Exists(Gxx), File.Exists(Gxx) ? GccVersion() : null),
            new("libtonc", Path.Combine(LibtoncLib, "libtonc.a"), File.Exists(Path.Combine(LibtoncLib, "libtonc.a"))),
            new("maxmod", Path.Combine(LibgbaLib, "libmm.a"), File.Exists(Path.Combine(LibgbaLib, "libmm.a"))),
            new("gbafix", Gbafix, File.Exists(Gbafix)),
            new("mmutil", Mmutil, File.Exists(Mmutil)),
            new("ninja", Ninja, File.Exists(Ninja)),
        };
        return list;
    }

    public bool IsComplete => Check().All(t => t.Found);

    string? GccVersion()
    {
        var r = Proc.Run(Gcc, "-dumpversion", null, TimeSpan.FromSeconds(20));
        return r.ExitCode == 0 ? $"devkitARM {r.Output.Trim().Split('.')[0]}.{r.Output.Trim().Split('.').ElementAtOrDefault(1) ?? "0"}" : null;
    }

    public static string? FindOnPath(string exe)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                var p = Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(p)) return p;
            }
            catch (ArgumentException) { }
        }
        return null;
    }
}
