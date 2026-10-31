using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AgbStudio.Editor.Services;

/// <summary>
/// Operating-system dialogs and Explorer (P10.17, T-Y01..T-Y04, Show in Explorer in T-M03 / T-M13 / T-M17).
/// Windows open folder / file / save pickers through Avalonia's storage provider; Explorer through explorer.exe.
/// </summary>
public sealed class SystemShell(Func<TopLevel?> top, Action<string, string>? launch = null)
{
    readonly Action<string, string> _launch = launch ?? ((exe, args) => Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true }));

    /// <summary>The file or folder that "Show in Explorer" reveals (the selected asset); set by the window that owns the selection.</summary>
    public Func<string?>? Selection { get; set; }

    /// <summary>Opens Explorer with the file selected, or the folder itself. Returns false if the path does not exist.</summary>
    public bool ShowInExplorer(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (File.Exists(path)) { _launch("explorer.exe", $"/select,\"{Path.GetFullPath(path)}\""); return true; }
        if (Directory.Exists(path)) { _launch("explorer.exe", $"\"{Path.GetFullPath(path)}\""); return true; }
        return false;
    }

    IStorageProvider? Storage => top()?.StorageProvider;

    /// <summary>Replaces the operating-system folder picker (tests answer with a path instead of a window).</summary>
    public Func<string, Task<string?>>? FolderPicker { get; set; }
    /// <summary>Replaces the operating-system file picker (tests answer with a path instead of a window).</summary>
    public Func<string, (string Name, string[] Patterns)[], Task<string?>>? FilePicker { get; set; }

    /// <summary>T-Y01 Launcher › Open…: folder picker.</summary>
    public async Task<string?> PickFolderAsync(string title)
    {
        if (FolderPicker != null) return await FolderPicker(title);
        if (Storage is not { CanPickFolder: true } s) return null;
        var r = await s.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return r.Count > 0 ? r[0].TryGetLocalPath() : null;
    }

    /// <summary>T-Y04 Preferences › Paths › Browse…: file picker.</summary>
    public async Task<string?> PickFileAsync(string title, params (string Name, string[] Patterns)[] filters)
    {
        if (FilePicker != null) return await FilePicker(title, filters);
        if (Storage is not { CanOpen: true } s) return null;
        var r = await s.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title, AllowMultiple = false,
            FileTypeFilter = filters.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns }).ToList(),
        });
        return r.Count > 0 ? r[0].TryGetLocalPath() : null;
    }

    /// <summary>T-Y02 Localization › Export .csv: save dialog.</summary>
    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string extension)
    {
        if (Storage is not { CanSave: true } s) return null;
        var r = await s.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title, SuggestedFileName = suggestedName, DefaultExtension = extension });
        return r?.TryGetLocalPath();
    }

    /// <summary>T-Y03 Script Editor › Open externally: the configured editor at a file and line (VS Code syntax "-g file:line").</summary>
    public void OpenExternally(string editorExe, string file, int line) =>
        _launch(editorExe, Path.GetFileName(editorExe).StartsWith("code", StringComparison.OrdinalIgnoreCase) ? $"-g \"{file}:{line}\"" : $"\"{file}\"");
}
