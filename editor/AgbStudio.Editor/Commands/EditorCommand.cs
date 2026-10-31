using System.Windows.Input;
using Avalonia.Input;

namespace AgbStudio.Editor.Commands;

/// <summary>
/// Where a shortcut is active. Global shortcuts conflict with every context. Scene, Script and Graph are documents of
/// the editor workspace and share one namespace (Preferences · Shortcuts reports Toggle grid against Go to line); the
/// Launcher pages replace the workspace, so their keys (F2 Rename, Enter Open) never meet the editor's.
/// </summary>
public enum ShortcutContext { Global, Scene, Script, Graph, Launcher }

/// <summary>A page that sets the shortcut context while it is shown (the Launcher pages).</summary>
public interface IShortcutScope
{
    ShortcutContext ShortcutContext { get; }
}

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
    Func<bool>? _required;

    public bool IsImplemented => _run != null;

    /// <summary>
    /// A condition on top of the handler's own (e.g. "a project is open" for the title bar menus), kept when another
    /// handler is set later.
    /// </summary>
    public EditorCommand Require(Func<bool> condition)
    {
        var before = _required;
        _required = before is null ? condition : () => before() && condition();
        RaiseCanExecuteChanged();
        return this;
    }

    public EditorCommand Handle(Action run, Func<bool>? canExecute = null)
    {
        _run = run;
        _can = canExecute;
        RaiseCanExecuteChanged();
        return this;
    }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public bool CanExecute(object? parameter = null) => (_required?.Invoke() ?? true) && (_can?.Invoke() ?? true);

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
