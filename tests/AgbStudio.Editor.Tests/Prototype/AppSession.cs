using System.Reflection;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Build;
using AgbStudio.Editor.Tests.Windows.Database;
using AgbStudio.Editor.Tests.Windows.Launcher;
using AgbStudio.Editor.Tests.Windows.Play;
using AgbStudio.Editor.Windows.Build;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Editor.Windows.Launcher;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Editor.Windows.Settings;
using AgbStudio.Editor.Windows.WorldMap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Prototype;

/// <summary>
/// The editor as a user starts it, headless, for following Figma prototype links (T-X01..T-X03): with the Sky Hopper
/// project (the Scene Editor opens) or without a project (the Launcher opens). Nothing outside the process starts: the
/// devkitPro folder points nowhere (no tool, no version check), builds wait "in progress" until they are cancelled
/// (<see cref="BuildService.BeforeBuild"/>), audio, gamepad and cartridge devices are fakes, and the Launcher's
/// programs (git, documents, the Recycle Bin) are stubs.
/// </summary>
public sealed class AppSession : IDisposable
{
    public Fixture Fx { get; } = new();
    public MainWindow Win { get; }
    public EditorServices S => Fx.Services;
    /// <summary>What was clicked, in order (shown when a check fails).</summary>
    public List<string> Steps { get; } = [];
    /// <summary>The pages shown, in order (BACK goes to the one before the current).</summary>
    public List<string> Pages { get; } = [];
    readonly string _projectPath;
    bool _alt;

    public AppSession(bool project = true, bool light = false)
    {
        var prefs = S.Preferences.Current;
        prefs.DevkitPro = Path.Combine(Fx.Folder, "no-devkitpro");
        prefs.DevkitProNoticeShown = true;
        if (light) prefs.Theme = ThemeMode.Light;
        S.Build.BeforeBuild = ct => Task.Delay(Timeout.Infinite, ct);
        var play = PlayController.Of(S);
        play.UseTimer = false;
        play.AudioFactory = () => new CountingSink();
        play.Gamepad = () => null;
        S.Set<ICartridgeDeviceProvider>(new FakeDevices());
        var launcher = LauncherFixture.Launcher(Fx);
        // Opening Project stays on its first step until it is closed or cancelled
        launcher.BeforeOpenStep = (_, ct) => Task.Delay(Timeout.Infinite, ct);
        if (project)
        {
            Fx.OpenSkyHopper();
            // a Build Report like the Figma frame, with its ROM (Run in mGBA, Flash to cartridge)
            GroupFixture.WriteReport(S.Project.Current!);
            _projectPath = S.Project.Current!.ProjectPath;
        }
        else _projectPath = AgbStudio.Build.Content.SkyHopper.Create(Path.Combine(Fx.Folder, "SkyHopper")).ProjectPath;
        Win = new MainWindow(S, Startup.Choose(S)) { Width = 1440, Height = 900 };
        S.Windows.Opened += id => { if (S.Windows.Info(id).Kind == WindowKind.Page) Pages.Add(id); };
        Win.Show();
        Pump();
        if (Win.CurrentPageId is { } start) Pages.Insert(0, start);
    }

    public void Dispose()
    {
        LauncherService.Of(S).TutorialHost.Session.Stop();
        // dialogs and popovers still open are dismissed (as Esc does): a button that awaits its dialog's result (the
        // Inspector's Sheet dropdown) then finishes, and xUnit does not wait for it after the test
        while (Win.Overlays.IsOpen) Win.Overlays.CloseTop();
        // a build still waiting ends now ("Build cancelled.")
        S.Build.Cancel();
        Pump();
        Win.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Fx.Dispose();
    }

    public static void Pump() => Capture.Pump();

    /// <summary>
    /// Ends the build a step started (Play, Debug) when the walk moves on, as a real build would end after a moment:
    /// it is cancelled, since this session has no toolchain, and its notices are dismissed. Play and Build ROM are
    /// greyed out while a build runs.
    /// </summary>
    public void EndBuild()
    {
        if (!S.Build.IsBuilding) return;
        S.Build.Cancel();
        Pump();
        foreach (var t in S.Toasts.Visible.ToList()) S.Toasts.Close(t);
        Pump();
        Steps.Add("build ended");
    }

    // ---- windows ------------------------------------------------------------------------------------

    /// <summary>The windows registered by the editor, by id (Figma frame of the prototype → the class the app shows).</summary>
    static readonly Lazy<IReadOnlyDictionary<string, Type>> s_types = new(() =>
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var t in typeof(MainWindow).Assembly.GetTypes())
            foreach (var a in t.GetCustomAttributes<EditorWindowAttribute>()) map.TryAdd(a.Id, t);
        return map;
    });

    /// <summary>The class registered for a window id; the light frames T-L01..T-L03 are the dark windows in Light.</summary>
    public static Type TypeOf(string id) => s_types.Value[AppWindowId(id)];

    /// <summary>T-L01..T-L03 are the Scene Editor, New Project and Preferences · Appearance in the Light theme.</summary>
    public static string AppWindowId(string id) => id switch { "T-L01" => "T-P01", "T-L02" => "T-D01", "T-L03" => "T-S06", _ => id };

    public static bool IsPage(string id) => WindowCatalog.Find(AppWindowId(id))?.Kind is WindowKind.Page;

    /// <summary>The Launcher side of the app: the windows a user reaches before a project is open.</summary>
    public static bool BeforeProject(string id) => id is "T-P12" or "T-P20" or "T-P21" or "T-D01" or "T-L02" or "T-D22" or "T-O13";

    /// <summary>The page a dialog or popover is opened over (where its buttons and menus are).</summary>
    public static string HostOf(string id) => AppWindowId(id) switch
    {
        "T-D01" or "T-D22" or "T-O13" => "T-P12",
        "T-D13" => "T-P08",
        "T-D14" => "T-P04",
        "T-D15" or "T-D16" => "T-P09",
        "T-D17" or "T-D18" or "T-D19" => "T-P07",
        "T-D20" => "T-P14",
        "T-D21" => "T-P16",
        "T-D07" => "T-P11",
        "T-O09" => "T-P13",
        "T-O10" => "T-P10",
        _ => "T-P01",
    };

    object? PageArgs(string id) => id switch
    {
        "T-P03" => S.Documents.Open.First(d => d.Name == "Forest_01.map"),
        "T-P04" => S.Documents.Open.First(d => d.Name == "PlayerController.cs"),
        _ => null,
    };

    /// <summary>What a dialog or popover is opened with, as its button in the app passes it.</summary>
    object? OverlayArgs(string id) => id switch
    {
        "T-D04" => GroupFixture.ImportFiles(Fx, S.Project.Current!),
        "T-D12" => new UnsavedChangesArgs(S.Documents.Open.Where(d => d.Path != null).Take(2).ToList(), UnsavedReason.Exit),
        "T-D19" => new ImportCsvRequest((DatabasePage)Win.CurrentPage!, DataFixture.WriteItemsCsv(Fx.Folder)),
        "T-D22" => _projectPath,
        "T-O04" => "Sprites",
        "T-O09" => new LinkSettingsArgs(((WorldMapPage)Win.CurrentPage!).Model, new LinkRef("Forest_01", 0)),
        "T-O12" => new RecordShortcutRequest("scene.toggleGrid", "Toggle grid", S.Shortcuts.GestureOf("scene.toggleGrid"), g => S.Shortcuts.FindConflicts("scene.toggleGrid", g)),
        "T-O13" => "first-platformer",
        _ => null,
    };

    /// <summary>Shows a page (if it is not shown yet) the way the app opens it.</summary>
    public void ShowPage(string id)
    {
        if (Win.CurrentPageId == id) return;
        _ = S.Windows.Open(id, PageArgs(id), source: "test");
        Pump();
        Steps.Add("show " + id);
    }

    /// <summary>
    /// Shows a link's source window: its page, or its host page with the dialog / popover on top (Record Shortcut over
    /// Preferences · Shortcuts, as the app opens it). Returns the dialog or popover.
    /// </summary>
    public Control? OpenSource(string id)
    {
        if (IsPage(id)) { ShowPage(id); return null; }
        var app = AppWindowId(id);
        ShowPage(HostOf(id));
        if (app == "T-O12") Overlay("T-S09");
        return Overlay(app);
    }

    Control Overlay(string id)
    {
        _ = S.Windows.Open(id, OverlayArgs(id), source: "test");
        Pump();
        Steps.Add("open " + id);
        var top = Win.Overlays.Open.LastOrDefault();
        Assert.True(top != null && TypeOf(id).IsInstanceOfType(top), $"{id} did not open: {Describe()}");
        return top!;
    }

    // ---- finding and clicking -----------------------------------------------------------------------

    /// <summary>The visible control with this name: in the top dialog / popover when one is open, else in the window.</summary>
    public T Named<T>(string name) where T : Control
    {
        Visual root = Win.Overlays.Open.LastOrDefault() ?? (Visual)Win;
        return root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible)
            ?? throw new Xunit.Sdk.XunitException($"no visible {typeof(T).Name} \"{name}\" in {(root == Win ? Win.CurrentPageId : root.GetType().Name)}");
    }

    /// <summary>A template part (e.g. PART_Close) of the first <typeparamref name="T"/> in the top dialog / popover.</summary>
    public Button PartOfTop<T>(string part) where T : Control
    {
        var top = Win.Overlays.Open.LastOrDefault() ?? throw new Xunit.Sdk.XunitException("no dialog or popover is open");
        var holder = top.GetVisualDescendants().OfType<T>().FirstOrDefault() ?? throw new Xunit.Sdk.XunitException($"{top.GetType().Name} has no {typeof(T).Name}");
        return holder.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == part)
            ?? throw new Xunit.Sdk.XunitException($"{typeof(T).Name} of {top.GetType().Name} has no {part}");
    }

    /// <summary>A title bar menu (File … Help).</summary>
    public MenuItem TitleMenu(string title) => Win.MenuBar.Items.OfType<MenuItem>().First(m => (string?)m.Header == title);

    /// <summary>
    /// One left click at the control, through the window (or the menu popup) it is shown in. Successive clicks land
    /// 12 px apart, so two single clicks never count as a double-click.
    /// </summary>
    public void Click(Control c)
    {
        _alt = !_alt;
        var top = TopLevel.GetTopLevel(c) ?? throw new Xunit.Sdk.XunitException($"{c.Name ?? c.GetType().Name} is not shown");
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2 + Math.Min(6, c.Bounds.Width / 2 - 1) * (_alt ? 1 : -1), c.Bounds.Height / 2), top)!.Value;
        top.MouseMove(p);
        top.MouseDown(p, MouseButton.Left);
        top.MouseUp(p, MouseButton.Left);
        Steps.Add("click " + Label(c));
        Pump();
    }

    public void DoubleClick(Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Win)!.Value;
        Win.MouseMove(p);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Steps.Add("double-click " + Label(c));
        Pump();
    }

    public void Key(PhysicalKey k)
    {
        Win.KeyPressQwerty(k, RawInputModifiers.None);
        Win.KeyReleaseQwerty(k, RawInputModifiers.None);
        Steps.Add("key " + k);
        Pump();
    }

    static string Label(Control c) => c is MenuItem m ? "menu item " + m.Header : c.Name ?? c.GetType().Name;

    /// <summary>The current page and the open dialogs / popovers, for failure messages.</summary>
    public string Describe() =>
        $"page {Win.CurrentPageId}, overlays [{string.Join(", ", Win.Overlays.Open.Select(o => o.GetType().Name))}], steps: {string.Join(" > ", Steps.TakeLast(8))}";
}
