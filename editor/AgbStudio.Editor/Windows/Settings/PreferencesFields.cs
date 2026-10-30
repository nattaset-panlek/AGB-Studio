namespace AgbStudio.Editor.Services;

public enum ScriptEditorMode { BuiltIn, External }

/// <summary>
/// The Preferences fields of the Editor (script editor part), Paths and Emulator tabs (T-S07, T-S08, T-D09). Defaults are
/// the values shown in Figma; paths may hold environment variables ("%LOCALAPPDATA%\...") and are expanded when used.
/// </summary>
public sealed partial class Preferences
{
    // Editor (T-S07): Script editor
    public ScriptEditorMode ScriptEditor { get; set; } = ScriptEditorMode.External;
    public string ExternalEditor { get; set; } = "Visual Studio Code";
    public bool OpenCsExternally { get; set; } = true;
    public bool GenerateCsproj { get; set; } = true;
    public bool HotReloadOnExternalSave { get; set; } = true;

    // Editor (T-S07): Built-in code editor
    public string CodeFont { get; set; } = "JetBrains Mono";
    public int TabSize { get; set; } = 4;
    public string CodeTheme { get; set; } = "Studio Dark";
    public bool FormatOnSave { get; set; } = true;
    public bool AutocompleteAsYouType { get; set; } = true;
    public bool UnderlineAnalyzerWarnings { get; set; } = true;

    // Paths (T-S08); empty devkitPro = the DEVKITPRO variable, else D:\devkitPro
    public string DevkitPro { get; set; } = "";
    public string ProjectsFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AGB Studio");
    public string ScriptEditorPath { get; set; } = @"%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe";
    public string ImageEditorPath { get; set; } = "";
    public string TiledPath { get; set; } = "";

    // Emulator (T-D09)
    public string EmulatorCore { get; set; } = "mGBA (built in)";
    public int EmulatorScale { get; set; } = 3;
    public string EmulatorFilter { get; set; } = "Pixel-perfect";
    public bool EmulatorAudio { get; set; } = true;
    public bool PauseWhenUnfocused { get; set; }
    public string FastForwardKey { get; set; } = "Tab";
    public int FastForwardSpeed { get; set; } = 4;

    /// <summary>The devkitPro folder for the toolchain, or null for the default (DEVKITPRO, else D:\devkitPro).</summary>
    public string? DevkitProOrNull() => string.IsNullOrWhiteSpace(DevkitPro) ? null : Environment.ExpandEnvironmentVariables(DevkitPro.Trim());

    /// <summary>A path preference with its environment variables expanded ("" stays "").</summary>
    public static string Expand(string path) => string.IsNullOrWhiteSpace(path) ? "" : Environment.ExpandEnvironmentVariables(path.Trim());
}
