using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgbStudio.Editor.Services;

public enum ThemeMode { Dark, Light, System }

/// <summary>
/// Per-user preferences (P10.14), with the defaults shown in Preferences · Appearance (T-S06) and Preferences · Editor (T-S07).
/// The other Preferences tabs add their fields in Windows/Settings/PreferencesFields.cs.
/// </summary>
public sealed partial class Preferences
{
    // Appearance (T-S06)
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public string Accent { get; set; } = ThemeService.DefaultAccent;
    public int UiScalePercent { get; set; } = 100;
    public int CodeFontSize { get; set; } = 13;
    public bool ReduceMotion { get; set; }
    public bool ShowPixelGrid { get; set; } = true;
    public int PixelGridMinZoom { get; set; } = 400;

    // Editor (T-S07)
    public int AutosaveMinutes { get; set; } = 2;
    public int UndoHistory { get; set; } = 200;
    public int DefaultSnap { get; set; } = 16;
    public bool OpenLastProjectOnStart { get; set; } = true;
    public bool ShowCollisionOverlay { get; set; } = true;
    public bool ShowBudgetInInspector { get; set; } = true;

    // Shortcuts (T-S09): command id -> "Ctrl+G" (empty string = unbound); defaults are not stored
    public Dictionary<string, string> Shortcuts { get; set; } = new(StringComparer.Ordinal);

    public string? LastProject { get; set; }

    // Scene Editor workspace: the docking layout in use and the layouts saved by name (JSON of DockLayout)
    public string? SceneLayout { get; set; }
    public Dictionary<string, string> SavedLayouts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Appearance › "Show pixel grid at 400% zoom and above": true when a view at this zoom (pixels per game
    /// pixel; 4 = 400%) draws the grid between game pixels.</summary>
    public bool PixelGridAt(double scale) => ShowPixelGrid && scale * 100 >= Math.Max(100, PixelGridMinZoom);
}

/// <summary>Loads and saves <see cref="Preferences"/> as JSON, by default in %APPDATA%\AGBStudio\preferences.json.</summary>
public sealed class PreferencesStore
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public string FilePath { get; }
    public Preferences Current { get; private set; } = new();
    public event Action? Saved;

    public PreferencesStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath;
        Load();
    }

    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AGBStudio");
    public static string DefaultPath => Path.Combine(DefaultFolder, "preferences.json");

    public void Load()
    {
        if (!File.Exists(FilePath)) { Current = new Preferences(); return; }
        try { Current = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath), Json) ?? new Preferences(); }
        catch (JsonException) { Current = new Preferences(); }
        Current.Shortcuts = new Dictionary<string, string>(Current.Shortcuts, StringComparer.Ordinal);
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Json));
        File.Move(tmp, FilePath, overwrite: true);
        Saved?.Invoke();
    }

    public void Update(Action<Preferences> change)
    {
        change(Current);
        Save();
    }
}
