using System.Windows.Input;
using Avalonia.Input;

namespace AgbStudio.Editor.Commands;

/// <summary>Where a shortcut is active. Global shortcuts conflict with every context.</summary>
public enum ShortcutContext { Global, Scene, Script, Graph }

/// <summary>
/// One command of the editor (menu item, palette entry, shortcut target). <see cref="Window"/> names the Figma window
/// (T-ID) the command opens; while that window is not built yet the command only reports the request to the router.
/// </summary>
public sealed class EditorCommand : ICommand
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Category { get; init; } = "";
    public string? Glyph { get; init; }
    public KeyGesture? DefaultGesture { get; init; }
    public ShortcutContext Context { get; init; } = ShortcutContext.Global;
    public string? Window { get; init; }
    /// <summary>False for entries that only exist inside a menu (e.g. one recent project) and should not be listed in the palette.</summary>
    public bool InPalette { get; init; } = true;

    Action? _run;
    Func<bool>? _can;

    public bool IsImplemented => _run != null;

    public EditorCommand Handle(Action run, Func<bool>? canExecute = null)
    {
        _run = run;
        _can = canExecute;
        RaiseCanExecuteChanged();
        return this;
    }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public bool CanExecute(object? parameter = null) => _can?.Invoke() ?? true;

    internal Action<EditorCommand>? Fallback { get; set; }
    internal Action<EditorCommand>? Executed { get; set; }

    public void Execute(object? parameter = null)
    {
        if (!CanExecute(parameter)) return;
        if (_run != null) _run();
        else Fallback?.Invoke(this);
        Executed?.Invoke(this);
    }

    public override string ToString() => Id;
}
