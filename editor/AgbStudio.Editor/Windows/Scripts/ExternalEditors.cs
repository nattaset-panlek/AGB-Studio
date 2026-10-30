using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>The external editors of Preferences · Editor (T-S07): Visual Studio Code, JetBrains Rider, Visual Studio, Custom….</summary>
public enum ExternalEditorKind { VsCode, Rider, VisualStudio, Custom }

/// <summary>
/// Script editor options of Preferences · Editor (T-S07, "Script editor" and "Built-in code editor") and the Script editor path
/// of Preferences · Paths (T-S08), with the Figma defaults. Stored next to preferences.json as script-editor.json so the
/// Preferences dialog and the Script Editor share them (P14.01).
/// </summary>
public sealed class ScriptEditorSettings
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Built-in / External segment.</summary>
    public bool UseExternal { get; set; } = true;
    public ExternalEditorKind Editor { get; set; } = ExternalEditorKind.VsCode;
    /// <summary>Executable chosen in Preferences · Paths › Script editor; empty = the detected one.</summary>
    public string ExecutablePath { get; set; } = "";
    /// <summary>Arguments for Custom…: {file}, {line}, {column} and {project} are replaced.</summary>
    public string CustomArguments { get; set; } = "\"{file}\"";
    /// <summary>"Open .cs with": External editor / Built-in editor (double-click on a script asset).</summary>
    public bool OpenCsExternally { get; set; } = true;
    public bool GenerateCsproj { get; set; } = true;
    public bool HotReloadOnExternalSave { get; set; } = true;
    // Built-in code editor
    public string Font { get; set; } = "JetBrains Mono";
    public int TabSize { get; set; } = 4;
    public string Theme { get; set; } = "Studio Dark";
    public bool FormatOnSave { get; set; } = true;
    public bool AutocompleteAsYouType { get; set; } = true;
    public bool UnderlineAnalyzerWarnings { get; set; } = true;

    [JsonIgnore] public string FilePath { get; private set; } = "";
    public event Action? Changed;

    public static string FileFor(PreferencesStore prefs) => Path.Combine(Path.GetDirectoryName(prefs.FilePath) ?? ".", "script-editor.json");

    public static ScriptEditorSettings Load(string path)
    {
        ScriptEditorSettings s;
        try { s = File.Exists(path) ? JsonSerializer.Deserialize<ScriptEditorSettings>(File.ReadAllText(path), Json) ?? new() : new(); }
        catch (JsonException) { s = new(); }
        s.FilePath = path;
        s.TabSize = Math.Clamp(s.TabSize, 1, 16);
        return s;
    }

    public void Save()
    {
        if (FilePath.Length == 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        Changed?.Invoke();
    }

    /// <summary>The shared settings of an editor instance (created on first use).</summary>
    public static ScriptEditorSettings Of(EditorServices s)
    {
        if (s.GetService(typeof(ScriptEditorSettings)) is ScriptEditorSettings existing) return existing;
        var created = Load(FileFor(s.Preferences));
        s.Set(created);
        return created;
    }
}

/// <summary>One external editor found on this machine (or not).</summary>
public sealed record ExternalEditorInfo(ExternalEditorKind Kind, string Name, string? Path)
{
    public bool Found => Path != null;
    /// <summary>Status line of Preferences · Paths: "Visual Studio Code found" / "Visual Studio Code not found".</summary>
    public string Status => Found ? $"{Name} found" : $"{Name} not found";
}

/// <summary>
/// Detects VS Code, Rider and Visual Studio (P14.01) and builds the command line that opens a script at a line in each of
/// them (P14.03, T-Y03). The probes (environment, files, App Paths registry) are injectable for tests.
/// </summary>
public sealed class ExternalEditors
{
    public static readonly IReadOnlyList<(ExternalEditorKind Kind, string Name)> Kinds =
    [
        (ExternalEditorKind.VsCode, "Visual Studio Code"), (ExternalEditorKind.Rider, "JetBrains Rider"),
        (ExternalEditorKind.VisualStudio, "Visual Studio"), (ExternalEditorKind.Custom, "Custom…"),
    ];

    readonly Func<string, string?> _env;
    readonly Func<string, bool> _fileExists;
    readonly Func<string, IEnumerable<string>> _dirs;
    readonly Func<string, string?> _appPath;
    readonly Func<IEnumerable<string>> _vswhere;

    public ExternalEditors(Func<string, string?>? env = null, Func<string, bool>? fileExists = null, Func<string, IEnumerable<string>>? directories = null,
        Func<string, string?>? appPath = null, Func<IEnumerable<string>>? vswhere = null)
    {
        _env = env ?? Environment.GetEnvironmentVariable;
        _fileExists = fileExists ?? File.Exists;
        _dirs = directories ?? (d => Directory.Exists(d) ? Directory.EnumerateDirectories(d) : []);
        _appPath = appPath ?? RegistryAppPath;
        _vswhere = vswhere ?? VsWhere;
    }

    public static string NameOf(ExternalEditorKind k) => Kinds.First(x => x.Kind == k).Name;

    /// <summary>Every known editor with the path found on this machine.</summary>
    public IReadOnlyList<ExternalEditorInfo> DetectAll() =>
        [Detect(ExternalEditorKind.VsCode), Detect(ExternalEditorKind.Rider), Detect(ExternalEditorKind.VisualStudio)];

    public ExternalEditorInfo Detect(ExternalEditorKind kind) => new(kind, NameOf(kind), Candidates(kind).FirstOrDefault(p => p.Length > 0 && _fileExists(p)));

    IEnumerable<string> Candidates(ExternalEditorKind kind)
    {
        string E(string name) => _env(name) ?? "";
        var local = E("LOCALAPPDATA");
        var pf = E("ProgramFiles");
        var pf86 = E("ProgramFiles(x86)");
        switch (kind)
        {
            case ExternalEditorKind.VsCode:
                if (local.Length > 0) yield return Path.Combine(local, "Programs", "Microsoft VS Code", "Code.exe");
                if (pf.Length > 0) yield return Path.Combine(pf, "Microsoft VS Code", "Code.exe");
                if (pf86.Length > 0) yield return Path.Combine(pf86, "Microsoft VS Code", "Code.exe");
                if (_appPath("Code.exe") is { } code) yield return code;
                foreach (var dir in E("PATH").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    if (dir.TrimEnd('\\', '/').EndsWith(Path.Combine("Microsoft VS Code", "bin"), StringComparison.OrdinalIgnoreCase))
                        yield return Path.Combine(Path.GetDirectoryName(dir.TrimEnd('\\', '/'))!, "Code.exe");
                break;
            case ExternalEditorKind.Rider:
                if (_appPath("rider64.exe") is { } rider) yield return rider;
                if (local.Length > 0)
                {
                    yield return Path.Combine(local, "Programs", "Rider", "bin", "rider64.exe");
                    foreach (var d in Safe(Path.Combine(local, "JetBrains", "Toolbox", "apps", "Rider", "ch-0")).OrderDescending())
                        yield return Path.Combine(d, "bin", "rider64.exe");
                }
                if (pf.Length > 0)
                    foreach (var d in Safe(Path.Combine(pf, "JetBrains")).Where(d => Path.GetFileName(d).Contains("Rider", StringComparison.OrdinalIgnoreCase)).OrderDescending())
                        yield return Path.Combine(d, "bin", "rider64.exe");
                break;
            case ExternalEditorKind.VisualStudio:
                foreach (var p in _vswhere()) yield return p;
                if (_appPath("devenv.exe") is { } devenv) yield return devenv;
                foreach (var root in new[] { pf, pf86 }.Where(r => r.Length > 0))
                    foreach (var version in new[] { "18", "2022", "2019" })
                        foreach (var edition in new[] { "Enterprise", "Professional", "Community", "Preview" })
                            yield return Path.Combine(root, "Microsoft Visual Studio", version, edition, "Common7", "IDE", "devenv.exe");
                break;
        }
    }

    IEnumerable<string> Safe(string dir)
    {
        try { return _dirs(dir).ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    static string? RegistryAppPath(string exe)
    {
        if (!OperatingSystem.IsWindows()) return null;
        foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
            if (key?.GetValue(null) is string s && s.Trim().Trim('"') is { Length: > 0 } path) return path;
        }
        return null;
    }

    static IEnumerable<string> VsWhere()
    {
        var pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "";
        var exe = Path.Combine(pf86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(exe)) return [];
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, "-all -prerelease -products * -property productPath") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (p is null) return [];
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { return []; }
    }

    /// <summary>Command line that opens <paramref name="file"/> at a line (and column) in an editor; <paramref name="project"/> is the project folder.</summary>
    public static string Arguments(ExternalEditorKind kind, string file, int line, int column = 1, string? project = null, string? custom = null)
    {
        line = Math.Max(1, line);
        column = Math.Max(1, column);
        return kind switch
        {
            // the folder makes VS Code load the generated .csproj (IntelliSense); -g goes to file:line:column
            ExternalEditorKind.VsCode => (project is null ? "" : $"\"{project}\" ") + $"-g \"{file}:{line}:{column}\"",
            ExternalEditorKind.Rider => $"--line {line} --column {column - 1} \"{file}\"",
            ExternalEditorKind.VisualStudio => $"/Edit \"{file}\" /Command \"Edit.GoTo {line}\"",
            _ => (custom ?? "\"{file}\"").Replace("{file}", file).Replace("{line}", line.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{column}", column.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{project}", project ?? ""),
        };
    }
}

/// <summary>
/// Opens scripts in the configured external editor (Script Editor › Open externally, New Script "Open in the external editor
/// after creating", Open .cs with). Regenerates the IDE project first when "Generate .csproj for IntelliSense" is on (P14.02).
/// </summary>
public sealed class ExternalEditorService
{
    readonly EditorServices _s;

    public ExternalEditorService(EditorServices s) => _s = s;

    /// <summary>Starts a process (exe, arguments); tests replace it.</summary>
    public Action<string, string> Launch { get; set; } = (exe, args) => Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
    public ExternalEditors Detector { get; set; } = new();
    /// <summary>Every launch, in order (tests and the Output log).</summary>
    public List<(string Exe, string Arguments)> Launches { get; } = [];

    public static ExternalEditorService Of(EditorServices s)
    {
        if (s.GetService(typeof(ExternalEditorService)) is ExternalEditorService existing) return existing;
        var created = new ExternalEditorService(s);
        s.Set(created);
        return created;
    }

    ScriptEditorSettings Settings => ScriptEditorSettings.Of(_s);

    /// <summary>The executable that will be used: the path set in Preferences · Paths, else the detected one.</summary>
    public string? Executable()
    {
        var set = Settings.ExecutablePath.Trim().Trim('"');
        if (set.Length > 0) return File.Exists(Environment.ExpandEnvironmentVariables(set)) ? Environment.ExpandEnvironmentVariables(set) : null;
        return Settings.Editor == ExternalEditorKind.Custom ? null : Detector.Detect(Settings.Editor).Path;
    }

    /// <summary>Opens a file at a line. Returns false (and shows a toast) when no editor executable is found.</summary>
    public bool Open(string file, int line, int column = 1)
    {
        var exe = Executable();
        if (exe is null)
        {
            _s.Toasts.Show($"{ExternalEditors.NameOf(Settings.Editor)} was not found. Set the Script editor in Preferences › Paths.", Controls.ToastStatus.Error);
            return false;
        }
        var root = _s.Project.Current?.Root;
        if (Settings.GenerateCsproj && _s.Project.Current is { } p)
        {
            try { AgbStudio.Scripting.CsprojGenerator.Write(p.Root, p.Settings.Name, ScriptAnalysis.ContextFor(p), _s.Build.Scripts.Last); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        var args = ExternalEditors.Arguments(Settings.Editor, file, line, column, root, Settings.CustomArguments);
        Launches.Add((exe, args));
        try { Launch(exe, args); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _s.Toasts.Show($"Could not start {Path.GetFileName(exe)}: {e.Message}", Controls.ToastStatus.Error);
            return false;
        }
        return true;
    }
}
