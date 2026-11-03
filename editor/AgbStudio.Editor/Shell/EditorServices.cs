using System.Reflection;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Services;
using Avalonia;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// The services of one editor instance and a small service locator (<see cref="Get{T}"/> / <see cref="Set{T}"/>) for
/// windows added in Phase 11. Tests build one with a temporary folder and a manual autosave timer.
/// </summary>
public sealed class EditorServices : IServiceProvider
{
    readonly Dictionary<System.Type, object> _extra = [];

    public required PreferencesStore Preferences { get; init; }
    public required RecentProjectsStore Recent { get; init; }
    public required WindowService Windows { get; init; }
    public required CommandRegistry Commands { get; init; }
    public required ShortcutService Shortcuts { get; init; }
    public required UndoService Undo { get; init; }
    public required DocumentService Documents { get; init; }
    public required ToastService Toasts { get; init; }
    public required ToolchainStatusService Toolchain { get; init; }
    public required ProjectService Project { get; init; }
    public required BuildService Build { get; init; }
    public required EmulatorHost Emulator { get; init; }
    public required ThemeService? Theme { get; init; }
    public required AutosaveService Autosave { get; init; }
    public required StatusModel Status { get; init; }

    /// <summary>The main window, for OS dialogs (set by <see cref="MainWindow"/>).</summary>
    public Avalonia.Controls.TopLevel? Top { get; set; }
    /// <summary>OS dialogs and Explorer (P10.17).</summary>
    public SystemShell System { get; set; } = new(() => null);
    /// <summary>Opens a file in the outside program Preferences › Paths names for it (image editor, Tiled); false when there is none.</summary>
    public Func<string, bool>? OpenWithTool { get; set; }

    /// <summary>Project Settings of the open project (in-memory when none is open).</summary>
    public IProjectSettingsStore ProjectSettings => Project.Settings;

    /// <param name="folder">Folder for preferences.json and recent-projects.json (default %APPDATA%\AGBStudio).</param>
    /// <param name="extensions">Extra assemblies to scan for windows, menus and commands (the editor is always scanned).</param>
    public static EditorServices Create(Application? app, string? folder = null, IRepeatingTimer? autosaveTimer = null, Func<DateTime>? clock = null, params Assembly[] extensions)
    {
        folder ??= PreferencesStore.DefaultFolder;
        var prefs = new PreferencesStore(Path.Combine(folder, "preferences.json"));
        var recent = new RecentProjectsStore(Path.Combine(folder, "recent-projects.json"), clock);
        var windows = new WindowService();
        var commands = new CommandRegistry(windows);
        Catalog.RegisterAll(commands);
        var documents = new DocumentService();
        var project = new ProjectService(recent);
        var s = new EditorServices
        {
            Preferences = prefs,
            Recent = recent,
            Windows = windows,
            Commands = commands,
            Shortcuts = new ShortcutService(commands, prefs),
            Undo = new UndoService(() => prefs.Current.UndoHistory),
            Documents = documents,
            Toasts = new ToastService(),
            // Preferences · Paths › devkitPro (empty = DEVKITPRO or D:\devkitPro)
            Toolchain = new ToolchainStatusService(() => prefs.Current.DevkitProOrNull()),
            Project = project,
            Build = new BuildService(project, () => prefs.Current.DevkitProOrNull()),
            Emulator = new EmulatorHost(),
            Theme = app is null ? null : new ThemeService(app),
            Autosave = new AutosaveService(autosaveTimer ?? new DispatcherRepeatingTimer(), () => prefs.Current.AutosaveMinutes, documents.SaveAllAsync),
            Status = new StatusModel(),
        };
        s.System = new SystemShell(() => s.Top);
        documents.DocumentActivated = doc => AgbStudio.Editor.Windows.Tilemap.PageChrome.Activate(s, doc);
        project.Changed += () =>
        {
            if (project.Current != null)
                documents.EnsureProjectDocuments(project.Current);
        };
        windows.Services = s;
        windows.Discover(commands, [typeof(EditorServices).Assembly, .. extensions]);
        return s;
    }

    public static EditorServices CreateDefault(Application app) => Create(app);

    /// <summary>Registers an extra service (e.g. a Phase 11 window's model shared with others).</summary>
    public void Set<T>(T service) where T : class => _extra[typeof(T)] = service;

    /// <summary>Any service: the built-in ones above or one added with <see cref="Set{T}"/>.</summary>
    public T Get<T>() where T : class => (T)(GetService(typeof(T)) ?? throw new InvalidOperationException("Service not registered: " + typeof(T).Name));

    public object? GetService(System.Type serviceType)
    {
        if (_extra.TryGetValue(serviceType, out var s)) return s;
        foreach (var p in typeof(EditorServices).GetProperties())
            if (p.PropertyType == serviceType && p.GetIndexParameters().Length == 0) return p.GetValue(this);
        return null;
    }
}
