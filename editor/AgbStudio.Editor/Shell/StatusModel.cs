using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// Texts of the Scene Editor status bar (Figma: Ready · Build OK · 0.8 s · ROM 412 KB / 32 MB … 59.7 fps · CPU 38% · mGBA core).
/// Empty items are hidden; the build, ROM and fps items are filled by the build and Play Mode.
/// </summary>
public sealed class StatusModel : INotifyPropertyChanged
{
    string _ready = "Ready";
    string? _build, _rom, _performance;
    string _emulator = "mGBA core";
    bool _toolchainOk = true;

    public string Ready { get => _ready; set => Set(ref _ready, value); }
    public string? Build { get => _build; set => Set(ref _build, value); }
    public string? Rom { get => _rom; set => Set(ref _rom, value); }
    public string? Performance { get => _performance; set => Set(ref _performance, value); }
    public string Emulator { get => _emulator; set => Set(ref _emulator, value); }
    /// <summary>Colour of the Ready dot: status/success when the toolchain is complete, status/warning otherwise.</summary>
    public bool ToolchainOk { get => _toolchainOk; set => Set(ref _toolchainOk, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
