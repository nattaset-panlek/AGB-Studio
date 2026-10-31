using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Scripts;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>
/// The outside programs of Preferences › Paths (T-S08): the image editor opens .aseprite files and the background
/// images in assets/images (which no AGB Studio window edits; sprites and tilesets keep opening the Sprite Editor and
/// the Tilemap Editor), Tiled opens .tmx / .tsx maps. Used by a double-click in the Assets panel and by a file result
/// of the Command Palette; when the program is not set or not found, the file opens as before.
/// </summary>
public static class ExternalTools
{
    [EditorStartup]
    public static void Wire(EditorServices s) => s.OpenWithTool = path => TryOpen(s, path);

    /// <summary>The program of Preferences › Paths that opens this file, or null.</summary>
    public static string? ProgramFor(EditorServices s, string path)
    {
        var p = s.Preferences.Current;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var images = s.Project.Current is { } project ? Path.GetFullPath(Path.Combine(project.AssetsDir, "images")) : null;
        var exe = ext switch
        {
            ".tmx" or ".tsx" => p.TiledPath,
            ".aseprite" or ".ase" => p.ImageEditorPath,
            ".png" when images != null && Path.GetFullPath(path).StartsWith(images + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) => p.ImageEditorPath,
            _ => null,
        };
        exe = Preferences.Expand(exe ?? "");
        return exe.Length > 0 && File.Exists(exe) ? exe : null;
    }

    /// <summary>Opens the file in its outside program; false when none is set for it (the caller opens it itself).</summary>
    public static bool TryOpen(EditorServices s, string path)
    {
        if (ProgramFor(s, path) is not { } exe) return false;
        ExternalEditorService.Of(s).Launch(exe, $"\"{Path.GetFullPath(path)}\"");
        return true;
    }
}
