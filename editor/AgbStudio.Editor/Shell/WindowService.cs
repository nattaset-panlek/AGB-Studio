using System.Reflection;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// A page that is not a document (Debugger, World Map, Scripting API): the title bar breadcrumb shows its name while it is
/// shown, instead of the active document, so pages no longer invent documents that are not open.
/// </summary>
public interface IPageBreadcrumb
{
    string Breadcrumb { get; }
}

/// <summary>What the main window offers to <see cref="WindowService"/>.</summary>
public interface IShellHost
{
    OverlayHost Overlays { get; }
    Control? CurrentPage { get; }
    void ShowPage(string id, Control page);
    /// <summary>Opens a title bar menu or one of its submenus (T-M01..T-M10). False if the id is not one.</summary>
    bool OpenTitleMenu(string id);
}

/// <summary>
/// Opens Figma windows by id (P10.12 routing, Phase 11 plug-in point). Windows, menus and commands are discovered from
/// attributes (<see cref="EditorWindowAttribute"/>, <see cref="EditorMenuAttribute"/>, <see cref="EditorCommandAttribute"/>,
/// <see cref="CommandHandlerAttribute"/>). An id with nothing registered shows a "not built yet" notice instead of failing.
/// </summary>
public sealed class WindowService : IWindowRouter
{
    sealed record Registration(WindowInfo Info, Func<WindowContext, Control> Factory);

    readonly Dictionary<string, Registration> _windows = new(StringComparer.Ordinal);
    readonly Dictionary<string, Func<WindowContext, IReadOnlyList<MenuEntry>>> _menus = new(StringComparer.Ordinal);
    readonly Dictionary<string, Control> _pages = new(StringComparer.Ordinal);

    public EditorServices Services { get; internal set; } = null!;
    public IShellHost? Host { get; set; }
    /// <summary>Every request, in order: window id and the command or caller that asked (tests, E2E).</summary>
    public List<(string Window, string? Source)> Requests { get; } = [];
    public event Action<string>? Opened;

    /// <summary>The page shown before the current one (the Launcher's "Back to project", Back from Scripting API).</summary>
    public string? PreviousPage { get; private set; }
    /// <summary>The page shown now (set when a page or panel replaces the workspace).</summary>
    public string? CurrentPage { get; private set; }

    void Track(string id)
    {
        if (CurrentPage != null && CurrentPage != id) PreviousPage = CurrentPage;
        CurrentPage = id;
    }

    public IEnumerable<string> RegisteredIds => _windows.Keys.Concat(_menus.Keys);
    public bool IsRegistered(string id) => _windows.ContainsKey(id) || _menus.ContainsKey(id);

    public WindowInfo Info(string id) =>
        _windows.TryGetValue(id, out var r) ? r.Info : WindowCatalog.Find(id) ?? new WindowInfo(id, id, WindowKind.Dialog, 460, 0);

    public void Register(string id, Func<WindowContext, Control> factory, WindowKind? kind = null, string? title = null)
    {
        var cat = WindowCatalog.Find(id);
        var info = new WindowInfo(id, title ?? cat?.Name ?? id, kind ?? cat?.Kind ?? WindowKind.Dialog, cat?.Width ?? 0, cat?.Height ?? 0);
        _windows[id] = new Registration(info, factory);
    }

    public void RegisterMenu(string id, Func<WindowContext, IReadOnlyList<MenuEntry>> entries) => _menus[id] = entries;

    /// <summary>Finds windows, menus, commands and command handlers in the given assemblies (the editor by default).</summary>
    public void Discover(CommandRegistry commands, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0) assemblies = [typeof(WindowService).Assembly];
        foreach (var t in assemblies.SelectMany(SafeTypes))
        {
            foreach (var a in t.GetCustomAttributes<EditorWindowAttribute>())
            {
                if (!typeof(Control).IsAssignableFrom(t)) throw new InvalidOperationException($"{t.Name}: [EditorWindow] needs a Control.");
                var withCtx = t.GetConstructor([typeof(WindowContext)]);
                var plain = t.GetConstructor(Type.EmptyTypes);
                if (withCtx is null && plain is null) throw new InvalidOperationException($"{t.Name}: needs a constructor () or (WindowContext).");
                Register(a.Id, ctx => (Control)(withCtx != null ? withCtx.Invoke([ctx]) : plain!.Invoke(null)),
                    (int)a.Kind >= 0 ? a.Kind : null, a.Title);
            }
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (m.GetCustomAttribute<EditorMenuAttribute>() is { } menu)
                    RegisterMenu(menu.Id, ctx => (IReadOnlyList<MenuEntry>)m.Invoke(null, m.GetParameters().Length == 1 ? [ctx] : null)!);
                if (m.GetCustomAttribute<EditorCommandAttribute>() is { } c)
                    commands.Add(new EditorCommand
                    {
                        Id = c.Id, Title = c.Title, Category = c.Category, Glyph = c.Glyph, Window = c.Window, Context = c.Context, InPalette = c.InPalette,
                        DefaultGesture = c.Keys is null ? null : Theme.Shortcuts.Parse(c.Keys),
                    }.Handle(() => Invoke(m)));
                if (m.GetCustomAttribute<CommandHandlerAttribute>() is { } h)
                    commands.Get(h.CommandId).Handle(() => Invoke(m));
                if (m.GetCustomAttribute<EditorStartupAttribute>() is not null) _startup.Add(m);
            }
        }
    }

    readonly List<MethodInfo> _startup = [];
    bool _started;

    /// <summary>Runs the <see cref="EditorStartupAttribute"/> methods once (called by the main window).</summary>
    public void RunStartup()
    {
        if (_started) return;
        _started = true;
        foreach (var m in _startup) Invoke(m);
    }

    static IEnumerable<Type> SafeTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null)!; }
    }

    void Invoke(MethodInfo m) => m.Invoke(null, m.GetParameters().Length == 1 ? [Services] : null);

    void IWindowRouter.Open(string windowId, EditorCommand source) => _ = Open(windowId, source: source.Id);

    /// <summary>Opens a window. Dialogs and popovers complete with their result when closed; pages and panels complete at once.</summary>
    public async Task<object?> Open(string id, object? args = null, Control? anchor = null, string? source = null)
    {
        Requests.Add((id, source));
        Opened?.Invoke(id);
        var info = Info(id);
        if (_menus.TryGetValue(id, out var menu)) { OpenMenu(menu, info, args, anchor); return null; }
        if (!_windows.TryGetValue(id, out var reg))
        {
            if (Host?.OpenTitleMenu(id) == true) return null;
            return Host is null ? null : await ShowStub(info, source);
        }
        if (Host is null) return null;
        Control? content = null;
        var ctx = new WindowContext
        {
            Services = Services, Info = info, Args = args, Anchor = anchor,
            Close = r => { if (content != null) Host.Overlays.Close(content, r); },
        };
        switch (info.Kind)
        {
            case WindowKind.Page:
                if (_pages.TryGetValue(id, out var page) && args != null && page is SceneWorkspace workspace) workspace.OpenWith(args);
                else if (page == null || args != null) _pages[id] = page = reg.Factory(ctx);
                Track(id);
                Host.ShowPage(id, page);
                return null;
            case WindowKind.Panel:
                Track(id);
                Host.ShowPage(id, reg.Factory(ctx));
                return null;
            default:
                content = reg.Factory(ctx);
                return await Host.Overlays.Show(content, info.Kind == WindowKind.Popover ? OverlayPlacement.TopCenter : OverlayPlacement.Center, info.Kind == WindowKind.Popover);
        }
    }

    /// <summary>The one instance of a page, made when first asked for; null when none is registered.</summary>
    public Control? PageFor(string id)
    {
        if (_pages.TryGetValue(id, out var page)) return page;
        if (!_windows.TryGetValue(id, out var reg)) return null;
        return _pages[id] = reg.Factory(new WindowContext { Services = Services, Info = reg.Info });
    }

    /// <summary>Creates a panel for a page slot (e.g. "T-P05" Console in the bottom panel); null when none is registered.</summary>
    public Control? CreatePanel(string id, object? args = null) =>
        _windows.TryGetValue(id, out var reg) ? reg.Factory(new WindowContext { Services = Services, Info = reg.Info, Args = args }) : null;

    void OpenMenu(Func<WindowContext, IReadOnlyList<MenuEntry>> entries, WindowInfo info, object? args, Control? anchor)
    {
        var ctx = new WindowContext { Services = Services, Info = info, Args = args, Anchor = anchor };
        var builder = new MenuBuilder(Services.Commands, Services.Shortcuts);
        var cm = new ContextMenu { ItemsSource = builder.BuildItems(entries(ctx)) };
        var target = anchor ?? Host?.CurrentPage;
        if (target != null) cm.Open(target);
    }

    Task<object?> ShowStub(WindowInfo info, string? source)
    {
        var view = new NotBuiltNotice(info, source);
        return Host!.Overlays.Show(view);
    }
}

/// <summary>Shown when a window id has no registered window yet (development placeholder, not a Figma design).</summary>
public sealed class NotBuiltNotice : Border, IOverlayContent
{
    public event Action<object?>? Closed;

    public NotBuiltNotice(WindowInfo info, string? source)
    {
        Name = "NotBuiltNotice";
        Tag = info.Id;
        Theme = (Avalonia.Styling.ControlTheme?)Avalonia.Application.Current?.FindResource("DialogSurface");
        Width = 460;
        var header = new DialogHeader { Title = info.Name, Glyph = "warn" };
        header.CloseRequested += (_, _) => Closed?.Invoke(null);
        var text = new TextBlock
        {
            // only reachable for an id outside the Figma windows: say it for users, without design ids
            Text = $"{info.Name} is not in this version of AGB Studio yet.",
            TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(20),
        };
        Theme_(text, "Body/12", "text/secondary");
        var footer = new DialogFooter { ConfirmText = "Close", CancelText = null };
        footer.Confirm += (_, _) => Closed?.Invoke(null);
        Child = new StackPanel { Children = { header, text, footer } };
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    static void Theme_(TextBlock t, string style, string brush)
    {
        AgbStudio.Editor.Theme.Fig.SetStyle(t, style);
        t.Bind(TextBlock.ForegroundProperty, t.GetResourceObservable(brush));
    }
}
