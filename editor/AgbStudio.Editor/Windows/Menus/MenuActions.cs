using System.Text.RegularExpressions;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Menus;

/// <summary>
/// Title bar menu actions of P11.65-P11.67 (T-M01..T-M08): Tools › Screenshot / GIF, Help › Report a Bug… and Check
/// for Updates (D-13), Build › Clean Build, Assets › Find Unused Assets, and the disabled state of every menu command
/// that needs an open project, an idle build, a running game or a configured link.
/// </summary>
public static partial class MenuActions
{
    /// <summary>Menu commands that only work with a project open (their windows show project data).</summary>
    public static readonly IReadOnlyList<string> NeedProject =
    [
        "file.importAsset", "file.exportRom", "file.projectSettings",
        "assets.reimport", "assets.findUnused", "assets.optimizePalettes",
        "assets.create.sprite", "assets.create.tileset", "assets.create.font", "assets.create.palette", "assets.create.soundEffect",
        "assets.create.music", "assets.create.dataTable", "assets.create.script", "assets.create.logicGraph", "assets.create.dialogue",
        "scene.new", "scene.settings", "scene.setStart", "scene.backgroundLayers", "scene.worldMap",
        "scene.addActor.empty", "scene.addActor.player", "scene.addActor.enemy", "scene.addActor.pickup", "scene.addActor.trigger",
        "scene.addActor.camera", "scene.addActor.text",
        "build.play", "build.playFromHere", "build.buildRom", "build.buildAndRun", "build.flash", "build.report", "build.clean",
        "tools.paletteManager", "tools.fontManager", "tools.database", "tools.localization", "tools.audio", "tools.dialogueEditor",
        "tools.logicGraph", "tools.debugger",
    ];

    /// <summary>Menu commands that are disabled while a build runs.</summary>
    public static readonly IReadOnlyList<string> NeedIdleBuild = ["build.play", "build.playFromHere", "build.buildRom", "build.buildAndRun", "build.flash", "build.clean", "file.exportRom"];

    /// <summary>
    /// Runs when the main window starts: implements the commands of this group and gives every menu command without a
    /// handler of its own an enable condition (commands other groups implement keep theirs).
    /// </summary>
    [EditorStartup]
    public static void Startup(EditorServices s)
    {
        var gated = new List<EditorCommand>();
        bool HasProject() => s.Project.Current != null;
        bool Idle() => !s.Build.IsBuilding;
        void Own(string id, Action run, Func<bool> can)
        {
            var c = s.Commands.Get(id);
            if (c.IsImplemented) return;
            c.Handle(run, can);
            gated.Add(c);
        }

        Own("tools.screenshot", () => Screenshot(s), () => GameCapture.Of(s).CanCapture);
        Own("help.reportBug", () => ReportBug(s), () => EditorLinks.Of(s).Config.BugUrl.Trim().Length > 0);
        Own("help.checkUpdates", () => _ = CheckUpdatesAsync(s), () => true);
        Own("build.clean", () => CleanBuild(s), () => HasProject() && Idle());
        Own("assets.findUnused", () => FindUnused(s), HasProject);
        Own("build.report", () => _ = s.Windows.Open("T-P11", source: "build.report"), () => HasProject() && HasReport(s));

        foreach (var id in NeedProject)
        {
            var c = s.Commands.Get(id);
            var idle = NeedIdleBuild.Contains(id);
            bool Can() => HasProject() && (!idle || Idle());
            if (c.IsImplemented)
            {
                // implemented by its window's group (Create › Sprite, Build › Play ...): still needs the project
                if (!gated.Contains(c)) { c.Require(Can); gated.Add(c); }
                continue;
            }
            if (c.Window is null) continue;
            var window = c.Window;
            c.Handle(() => _ = s.Windows.Open(window, source: id), Can);
            gated.Add(c);
        }

        void Refresh()
        {
            if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(Refresh); return; }
            foreach (var c in gated) c.RaiseCanExecuteChanged();
        }
        s.Project.Changed += Refresh;
        s.Build.Started += Refresh;
        s.Build.Finished += _ => Refresh();
        s.Windows.Opened += _ => Refresh();
        var capture = GameCapture.Of(s);
        capture.StateChanged += Refresh;
        capture.AutoStopped += path => s.Toasts.Show("GIF saved · " + Path.GetFileName(path));
        // the game starts and stops inside Play Mode; look once a second whether a frame is there
        var poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var last = false;
        poll.Tick += (_, _) => { var now = capture.CanCapture; if (now != last) { last = now; s.Commands.Get("tools.screenshot").RaiseCanExecuteChanged(); } };
        poll.Start();
        if (s.Top is Avalonia.Controls.Window w) w.Closed += (_, _) => poll.Stop();
    }

    static bool HasReport(EditorServices s) => s.Project.Current is { } p && File.Exists(Path.Combine(p.BuildDir, "report.json"));

    // ---- Tools › Screenshot / GIF (P11.66) -----------------------------------------------------------

    public static void Screenshot(EditorServices s)
    {
        var c = GameCapture.Of(s);
        var wasRecording = c.Recording;
        var path = c.Toggle();
        if (path is null)
        {
            if (!wasRecording) s.Toasts.Show("Start the game to take a screenshot.", ToastStatus.Error);
            return;
        }
        s.Toasts.Show(wasRecording
            ? $"GIF saved · {Path.GetFileName(path)}"
            : $"Screenshot saved · {Path.GetFileName(path)} · recording a GIF, press F12 to stop");
    }

    // ---- Help › Report a Bug…, Check for Updates (P11.67, D-13) ----------------------------------------

    public static void ReportBug(EditorServices s)
    {
        var links = EditorLinks.Of(s);
        if (links.Config.BugUrl.Trim().Length > 0) links.Open(links.Config.BugUrl.Trim());
    }

    public static async Task<UpdateResult> CheckUpdatesAsync(EditorServices s)
    {
        var links = EditorLinks.Of(s);
        var r = await links.CheckForUpdatesAsync(About.Version);
        s.Toasts.Show(r.Message, r.Ok ? ToastStatus.Success : ToastStatus.Error);
        if (r.Available && r.Url is { Length: > 0 } url) links.Open(url);
        return r;
    }

    // ---- Build › Clean Build ---------------------------------------------------------------------------

    /// <summary>Deletes the project's build folder (generated C++, objects, asset cache, ROM): the next build starts from scratch.</summary>
    public static void CleanBuild(EditorServices s)
    {
        if (s.Project.Current is not { } p || s.Build.IsBuilding) return;
        try
        {
            if (Directory.Exists(p.BuildDir)) Directory.Delete(p.BuildDir, true);
            s.Toasts.Show("Build folder cleaned · the next build starts from scratch");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            s.Toasts.Show($"Could not clean the build folder: {e.Message.TrimEnd('.')}. Stop the game and close programs that use files in the build folder, then try again.", ToastStatus.Error);
        }
    }

    // ---- Assets › Find Unused Assets -------------------------------------------------------------------

    public static void FindUnused(EditorServices s)
    {
        if (s.Project.Current is not { } p) return;
        var unused = UnusedAssets(p);
        s.Set(new UnusedAssetsResult(unused));
        var names = unused.Select(Path.GetFileName).ToList();
        s.Toasts.Show(unused.Count == 0 ? "No unused assets"
            : $"{unused.Count} unused asset{(unused.Count == 1 ? "" : "s")}: {string.Join(", ", names.Take(3))}{(names.Count > 3 ? ", …" : "")}");
    }

    /// <summary>
    /// Asset files (sprites, tilesets, images, music, sound effects, fonts) whose name no scene, map, script, graph,
    /// dialogue, table, sound effect, other asset's options or the project settings mention.
    /// </summary>
    public static IReadOnlyList<string> UnusedAssets(GameProject p)
    {
        var assets = p.SpriteFiles().Concat(p.TilesetFiles()).Concat(p.ImageFiles()).Concat(p.MusicFiles()).Concat(p.SoundFiles()).Concat(p.FontFiles()).ToList();
        var sources = p.SceneFiles().Concat(p.MapFiles()).Concat(p.ScriptFiles()).Concat(p.GraphFiles()).Concat(p.DialogueFiles())
            .Concat(p.TableFiles()).Concat(p.SfxFiles()).Append(p.ProjectPath)
            .Concat(Directory.Exists(p.AssetsDir) ? Directory.EnumerateFiles(p.AssetsDir, "*.meta.json", SearchOption.AllDirectories) : [])
            .Concat(Directory.Exists(p.FontsDir) ? Directory.EnumerateFiles(p.FontsDir, "*.font") : [])
            .Select(f => (File: f, Text: File.ReadAllText(f))).ToList();
        var unused = new List<string>();
        foreach (var a in assets)
        {
            var name = GameProject.AssetName(a);
            var meta = GameProject.MetaPath(a);
            var word = new Regex(@"(?<![A-Za-z0-9_])" + Regex.Escape(name) + @"(?![A-Za-z0-9_])");
            var used = sources.Any(src => !string.Equals(src.File, meta, StringComparison.OrdinalIgnoreCase)
                                          && !string.Equals(src.File, a, StringComparison.OrdinalIgnoreCase) && word.IsMatch(src.Text));
            if (!used) unused.Add(a);
        }
        return unused;
    }
}

/// <summary>The last Find Unused Assets result (the Assets panel can show or filter it).</summary>
public sealed record UnusedAssetsResult(IReadOnlyList<string> Files);
