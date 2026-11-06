namespace AgbStudio.Build;

/// <summary>Status of one tool or library, as shown in Preferences and Project Settings · Build.</summary>
public sealed record ToolStatus(string Name, string Path, bool Found, string? Version = null);

/// <summary>
/// Locates the compiler and host tools the build needs. P24.06: AGB Studio's own toolchain (Arm GNU Toolchain in
/// <c>toolchain/</c> next to <c>bin</c>, or in the repository after <c>tools/fetch-toolchain.ps1</c>) comes first; a
/// devkitPro folder still works, and a folder set in Preferences › Paths overrides both. The runtime's start code,
/// hardware header, maxmod, the soundbank writer (P24.05) and the ROM header writer are AGB Studio's own, so only the
/// compiler comes from here.
/// </summary>
public sealed class Toolchain
{
    /// <summary>The toolchain folder: AGB Studio's own (holding <c>bin/arm-none-eabi-g++.exe</c>) or a devkitPro folder
    /// (holding <c>devkitARM/bin/arm-none-eabi-g++.exe</c>). The name stays from when only devkitPro was supported.</summary>
    public string DevkitPro { get; }
    /// <summary>True for a folder laid out as Arm's toolchain (AGB Studio's own), false for devkitPro.</summary>
    public bool BuiltIn => !File.Exists(Path.Combine(DevkitPro, "devkitARM", "bin", "arm-none-eabi-g++.exe")) && File.Exists(Path.Combine(DevkitPro, "bin", "arm-none-eabi-g++.exe"));
    /// <summary>The compiler's own folder (the one holding bin).</summary>
    public string DevkitArm => BuiltIn ? DevkitPro : Path.Combine(DevkitPro, "devkitARM");
    public string GccBin => Path.Combine(DevkitArm, "bin");
    public string Gxx => Path.Combine(GccBin, "arm-none-eabi-g++.exe");
    public string Gcc => Path.Combine(GccBin, "arm-none-eabi-gcc.exe");
    public string Objcopy => Path.Combine(GccBin, "arm-none-eabi-objcopy.exe");
    public string Nm => Path.Combine(GccBin, "arm-none-eabi-nm.exe");
    public string Ninja { get; init; }  // when missing, NativeBuild runs the same graph in-process

    public Toolchain(string? devkitPro = null)
    {
        DevkitPro = devkitPro ?? Detect() ?? @"C:\devkitPro";
        if (DevkitPro.StartsWith('/') && DevkitPro.Length > 2 && DevkitPro[2] == '/')
            DevkitPro = $"{char.ToUpperInvariant(DevkitPro[1])}:{DevkitPro[2..].Replace('/', '\\')}";
        Ninja = FindOnPath("ninja.exe") ?? "ninja.exe";
    }

    /// <summary>AGB Studio's own toolchain folder: <c>toolchain</c> in the AGB Studio folder (an install, or the repository).</summary>
    public static string? BuiltInFolder => NativeBuild.FindRoot(AppContext.BaseDirectory) is { } root ? Path.Combine(root, "toolchain") : null;

    /// <summary>The toolchain to use when none is set: AGB Studio's own, else a devkitPro folder; null when neither is there.</summary>
    public static string? Detect()
    {
        if (BuiltInFolder is { } own && File.Exists(Path.Combine(own, "bin", "arm-none-eabi-g++.exe"))) return own;
        return DetectDevkitPro();
    }

    /// <summary>A devkitPro folder: DEVKITPRO (the installer sets "/opt/devkitpro", its MSYS2 name for C:\devkitPro),
    /// then C:\devkitPro and D:\devkitPro; null when none has devkitARM. DEVKITPRO may also name an AGB-style folder.</summary>
    public static string? DetectDevkitPro()
    {
        var env = Environment.GetEnvironmentVariable("DEVKITPRO");
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(env))
        {
            if (env.StartsWith('/') && env.Length > 2 && env[2] == '/') candidates.Add($"{char.ToUpperInvariant(env[1])}:{env[2..].Replace('/', '\\')}");
            else if (!env.StartsWith('/')) candidates.Add(env);
        }
        candidates.AddRange([@"C:\devkitPro", @"D:\devkitPro"]);
        return candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "devkitARM", "bin", "arm-none-eabi-g++.exe")) || File.Exists(Path.Combine(c, "bin", "arm-none-eabi-g++.exe")));
    }

    public IReadOnlyList<ToolStatus> Check()
    {
        var maxmod = Path.Combine(NativeBuild.MaxmodDir, "source_gba", "mm_main_gba.s");
        return
        [
            new("toolchain", DevkitPro, Directory.Exists(DevkitPro)),
            new("compiler", Gxx, File.Exists(Gxx), File.Exists(Gxx) ? GccVersion() : null),
            new("maxmod", maxmod, File.Exists(maxmod)),
            new("ninja", Ninja, File.Exists(Ninja)),
        ];
    }

    public bool IsComplete => Check().All(t => t.Found || t.Name == "ninja");  // optional: without it the build runs the same graph itself

    /// <summary>Runs a tool for its version (exe, arguments); tests replace it so no process starts.</summary>
    public Func<string, string, ProcResult> RunTool { get; init; } = (exe, args) => Proc.Run(exe, args, null, TimeSpan.FromSeconds(20));

    string? GccVersion()
    {
        var r = RunTool(Gcc, "-dumpversion");
        if (r.ExitCode != 0) return null;
        var parts = r.Output.Trim().Split('.');
        return $"{(BuiltIn ? "Arm GNU Toolchain" : "devkitARM")} {parts[0]}.{parts.ElementAtOrDefault(1) ?? "0"}";
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
