using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AgbStudio.Build;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>A recent project as a Launcher card: name from the project file, time, thumbnail and whether it is still there.</summary>
public sealed record ProjectCard(RecentProject Recent, string Name, string Ago, string Thumbnail, bool Exists)
{
    public string Path => Recent.Path;
}

/// <summary>What is in a project: used for the empty state (T-P22), the status bar and the delete confirmation.</summary>
public sealed record ProjectContents(int Scenes, int Assets, int Scripts, int Actors, bool HasMap)
{
    /// <summary>The whole project is empty (checklist 4.9): no assets, no scripts, no actors and no tile map.</summary>
    public bool IsEmpty => Assets == 0 && Scripts == 0 && Actors == 0 && !HasMap;

    public static ProjectContents Of(GameProject p)
    {
        var assets = p.SpriteFiles().Count() + p.TilesetFiles().Count() + p.ImageFiles().Count() + p.MusicFiles().Count() + p.SoundFiles().Count()
                     + p.FontFiles().Count() + p.TableFiles().Count() + p.DialogueFiles().Count() + p.SfxFiles().Count() + p.MapFiles().Count();
        var scripts = p.ScriptFiles().Count() + p.GraphFiles().Count();
        int actors = 0, scenes = 0;
        var map = false;
        foreach (var f in p.SceneFiles())
        {
            scenes++;
            try
            {
                var s = GameProject.Load<SceneFile>(f);
                actors += s.Actors.Count;
                map |= s.Map.Length > 0;
            }
            catch (Exception e) when (e is JsonException or InvalidDataException or IOException) { }
        }
        return new ProjectContents(scenes, assets, scripts, actors, map);
    }
}

/// <summary>
/// The Launcher's shared state and actions (T-P12, T-P20, T-P21, T-M17, T-D22, T-D01): recent project cards with
/// thumbnails and times, the selected project and its context menu commands, opening (through Opening Project),
/// creating, duplicating, renaming, removing and deleting projects, and tutorial progress. One per editor
/// (<see cref="Of"/>), shared by the three Launcher pages, the dialogs and Tutorial Steps.
/// </summary>
public sealed class LauncherService
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    readonly Dictionary<string, string> _thumbnails;
    RecentProject? _selected;

    public EditorServices Services { get; }
    public string DataFolder { get; }
    public TutorialProgressStore Tutorials { get; }
    public TutorialHost TutorialHost { get; }

    /// <summary>devkitPro folder of the toolchain (the New Project location warning).</summary>
    public string DevkitPro { get; set; } = new Toolchain().DevkitPro;
    /// <summary>The top-left status line ("v0.1 · devkitARM 16.1 found"); replaceable for the Figma fixture.</summary>
    public Func<string> StatusLine { get; set; }
    /// <summary>Moves a project folder to the Recycle Bin; tests delete in their temporary folder instead.</summary>
    public Func<string, bool> DeleteFolder { get; set; } = RecycleBin.Delete;
    /// <summary>Opens a document or link with its default program (release notes, guides).</summary>
    public Action<string> OpenDocument { get; set; } = path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    /// <summary>Runs before each Opening Project step (tests hold a step to check Cancel).</summary>
    public Func<string, CancellationToken, Task>? BeforeOpenStep { get; set; }
    /// <summary>Runs git for "Initialize git repository" (args, folder); null = git on PATH.</summary>
    public Func<string, string, ProcResult>? Git { get; set; }
    /// <summary>Folder picker (T-Y01 Launcher › Open…, New Project › Location); the OS dialog by default.</summary>
    public Func<string, Task<string?>> PickFolder { get; set; }
    /// <summary>Folder that may hold release-notes.md (null = next to the executable); the What's new panel reads it.</summary>
    public string? ReleaseNotesFolder { get; set; }
    /// <summary>Folder in which samples are copied and new projects are suggested (null = next to the recent projects).</summary>
    public string? ProjectsFolder { get; set; }

    public RecentProject? Selected
    {
        get => _selected;
        set { _selected = value; SelectionChanged?.Invoke(); RefreshCommands(); }
    }

    public event Action? SelectionChanged;
    /// <summary>The page shows the inline name editor for this project (Rename, F2).</summary>
    public event Action<RecentProject>? RenameRequested;
    /// <summary>A project was created with New Project (tutorials).</summary>
    public event Action<string>? ProjectCreated;
    public event Action? Changed;
    /// <summary>Only the status line changed (the toolchain check): the page does not rebuild its cards and rows for it.</summary>
    public event Action? StatusChanged;

    LauncherService(EditorServices s)
    {
        Services = s;
        DataFolder = System.IO.Path.GetDirectoryName(s.Preferences.FilePath) ?? PreferencesStore.DefaultFolder;
        _thumbnails = Load<Dictionary<string, string>>("launcher-projects.json") ?? new(StringComparer.OrdinalIgnoreCase);
        _thumbnails = new Dictionary<string, string>(_thumbnails, StringComparer.OrdinalIgnoreCase);
        Tutorials = new TutorialProgressStore(System.IO.Path.Combine(DataFolder, "tutorials.json"));
        TutorialHost = new TutorialHost(this);
        StatusLine = DefaultStatusLine;
        PickFolder = title => s.System.PickFolderAsync(title);
        s.Recent.Changed += () => { if (_selected != null && !s.Recent.Items.Any(r => Same(r.Path, _selected.Path))) Selected = null; Changed?.Invoke(); };
        // the check ends whenever it ends (gcc -dumpversion): rebuilding the page then could swallow a click in progress
        s.Toolchain.Changed += () => StatusChanged?.Invoke();
        s.Windows.Opened += _ => RefreshCommands();
        // the context menu commands act on the selected card while a Launcher page is shown
        foreach (var (id, run) in new (string, Action)[]
                 {
                     ("launcher.open", () => _ = OpenSelectedAsync("ProjectContextMenu")), ("launcher.showInExplorer", ShowSelectedInExplorer),
                     ("launcher.duplicate", () => DuplicateSelected()), ("launcher.rename", RenameSelected),
                     ("launcher.removeFromList", RemoveSelected), ("launcher.deleteProject", () => _ = DeleteSelectedAsync()),
                 })
            s.Commands.Get(id).Handle(run, CanAct);
    }

    public static LauncherService Of(EditorServices s)
    {
        if (s.GetService(typeof(LauncherService)) is LauncherService l) return l;
        l = new LauncherService(s);
        s.Set(l);
        return l;
    }

    bool CanAct() => _selected != null && Services.Windows.Host?.CurrentPage is LauncherPage;

    void RefreshCommands()
    {
        foreach (var id in new[] { "launcher.open", "launcher.showInExplorer", "launcher.duplicate", "launcher.rename", "launcher.removeFromList", "launcher.deleteProject" })
            Services.Commands.Get(id).RaiseCanExecuteChanged();
    }

    T? Load<T>(string file) where T : class
    {
        var path = System.IO.Path.Combine(DataFolder, file);
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    void SaveThumbnails()
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(System.IO.Path.Combine(DataFolder, "launcher-projects.json"), JsonSerializer.Serialize(_thumbnails, Json));
    }

    static bool Same(string a, string b)
    {
        try { return string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch (ArgumentException) { return false; }
    }

    string DefaultStatusLine()
    {
        var arm = Services.Toolchain.Find("devkitARM");
        var tool = arm is null ? null : arm.Found ? $"{arm.Version ?? "devkitARM"} found" : "devkitARM not found";
        return tool is null ? EditorVersion.Short : $"{EditorVersion.Short} · {tool}";
    }

    // ---- cards ------------------------------------------------------------------------------------

    /// <summary>The recent projects as cards (most recent first), filtered by name.</summary>
    public IReadOnlyList<ProjectCard> Cards(string? filter = null)
    {
        var q = filter?.Trim() ?? "";
        var list = new List<ProjectCard>();
        foreach (var r in Services.Recent.Items)
        {
            var exists = File.Exists(r.Path);
            var name = r.Name;
            if (exists)
            {
                try { name = GameProject.Load<AgbStudio.Core.Project.ProjectFile>(r.Path).Name; }
                catch (Exception e) when (e is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { }
            }
            if (q.Length > 0 && !name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new ProjectCard(r, name, Services.Recent.Ago(r), Thumbnail(r.Path, name), exists));
        }
        return list;
    }

    /// <summary>The card preview: the template's scene art recorded when the project was made, else a scene art that
    /// matches the project (cave, snow, dusk), else the day scene.</summary>
    public string Thumbnail(string path, string name)
    {
        if (_thumbnails.TryGetValue(System.IO.Path.GetFullPath(path), out var art)) return art;
        var words = name;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(System.IO.Path.Combine(dir, "scenes")))
                words += " " + string.Join(" ", Directory.EnumerateFiles(System.IO.Path.Combine(dir, "scenes"), "*.scene").Select(System.IO.Path.GetFileNameWithoutExtension));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (words.Contains("cave", StringComparison.OrdinalIgnoreCase) || words.Contains("rpg", StringComparison.OrdinalIgnoreCase)) return "art/scene/cave";
        if (words.Contains("snow", StringComparison.OrdinalIgnoreCase) || words.Contains("race", StringComparison.OrdinalIgnoreCase)) return "art/scene/snow";
        if (words.Contains("dusk", StringComparison.OrdinalIgnoreCase) || words.Contains("novel", StringComparison.OrdinalIgnoreCase) || words.Contains("story", StringComparison.OrdinalIgnoreCase)) return "art/scene/dusk";
        return "art/scene/day";
    }

    public void SetThumbnail(string projectPath, string art)
    {
        _thumbnails[System.IO.Path.GetFullPath(projectPath)] = art;
        SaveThumbnails();
    }

    /// <summary>Where new projects go by default: the folder that holds the most recent project, else Documents\GBAProjects.</summary>
    public string DefaultLocationBase()
    {
        if (ProjectsFolder != null) return ProjectsFolder;
        foreach (var r in Services.Recent.Items)
        {
            var root = System.IO.Path.GetDirectoryName(r.Path);
            var parent = root is null ? null : System.IO.Path.GetDirectoryName(root);
            if (parent != null && Directory.Exists(parent)) return parent;
        }
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GBAProjects");
    }

    // ---- open -------------------------------------------------------------------------------------

    /// <summary>Opens a project through Opening Project (T-D22). Completes with true when the project is open.</summary>
    public async Task<bool> OpenAsync(string path, string source)
    {
        var r = await Services.Windows.Open("T-D22", path, source: source);
        return r is string;
    }

    public Task<bool> OpenSelectedAsync(string source) => _selected is { } s ? OpenAsync(s.Path, source) : Task.FromResult(false);

    /// <summary>Unsaved Changes (T-D12) before another project replaces the open one (opened, or a new one created). False = stay.</summary>
    public async Task<bool> ConfirmUnsavedAsync(UnsavedReason reason = UnsavedReason.OpenProject)
    {
        var docs = Services.Documents;
        if (!docs.HasUnsaved) return true;
        var result = await Services.Windows.Open("T-D12", new UnsavedChangesArgs(docs.Dirty, reason), source: reason == UnsavedReason.OpenProject ? "OpenProject" : "NewProject") as string;
        if (result == "save") { await docs.SaveAllAsync(); return true; }
        return result == "discard";
    }

    /// <summary>Makes a loaded project the open one: documents, recent list, last project, then the Scene Editor or,
    /// when the project is empty, Scene Editor · Empty project (T-P22).</summary>
    public void CompleteOpen(string projectPath, string? scene)
    {
        var docs = Services.Documents;
        foreach (var d in docs.Open.ToList()) docs.Close(d);
        Services.Undo.Clear();
        var p = Services.Project.Open(projectPath);
        Services.Preferences.Update(x => x.LastProject = p.ProjectPath);
        if (scene != null) docs.Add(new EditorDocument { Name = "Scene", Glyph = "map", Path = System.IO.Path.Combine(p.ScenesDir, scene + ".scene") }, activate: true);
        docs.Add(new EditorDocument { Name = "Game", Glyph = "pad" });
        Services.Status.Rom = BuildReportLine(p);
        _ = Services.Windows.Open(ProjectContents.Of(p).IsEmpty ? "T-P22" : "T-P01", source: "OpenProject");
    }

    /// <summary>"ROM 412 KB / 32 MB" from the last build report, or null before the first build.</summary>
    public static string? BuildReportLine(GameProject p)
    {
        var r = AgbStudio.Build.Pipeline.BuildReport.Load(p.BuildDir);
        return r is null ? null : $"ROM {Math.Max(1, (r.RomBytes + 1023) / 1024)} KB / {r.RomCapacity / (1024 * 1024)} MB";
    }

    // ---- create -----------------------------------------------------------------------------------

    /// <summary>Creates the project off the UI thread, records its thumbnail and opens it.</summary>
    public async Task<CreatedProject> CreateAsync(NewProjectOptions o)
    {
        var git = Git;
        var created = await Task.Run(() => ProjectCreator.Create(o, git));
        SetThumbnail(created.ProjectPath, o.Template.Thumbnail);
        var p = GameProject.Open(created.ProjectPath);
        CompleteOpen(created.ProjectPath, p.Settings.LastScene.Length > 0 ? p.Settings.LastScene : null);
        foreach (var w in created.Warnings) Services.Toasts.Show(w, ToastStatus.Error);
        ProjectCreated?.Invoke(created.ProjectPath);
        return created;
    }

    /// <summary>Opens a sample project of Launcher · Learn: its copy in the projects folder, made on first use.</summary>
    public async Task<bool> OpenSampleAsync(SampleInfo sample)
    {
        var folder = NewProjectRules.FolderFrom(DefaultLocationBase(), sample.Name);
        var existing = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.agbproj").FirstOrDefault() : null;
        if (existing != null) return await OpenAsync(existing, "Sample_" + sample.Name);
        var prepared = new[] { System.IO.Path.Combine(AppContext.BaseDirectory, "samples") }
            .Concat(TryRepo("samples")).Select(r => System.IO.Path.Combine(r, GameProject.FileName(sample.Name)))
            .FirstOrDefault(d => Directory.Exists(d) && Directory.EnumerateFiles(d, "*.agbproj").Any());
        if (prepared is null && sample.Create is null)
        {
            Services.Toasts.Show($"{sample.Name} is not installed.", ToastStatus.Error);
            return false;
        }
        var path = await Task.Run(() =>
        {
            if (prepared != null) ProjectCreator.CopyDirectory(prepared, folder);
            else sample.Create!(folder);
            return Directory.EnumerateFiles(folder, "*.agbproj").First();
        });
        SetThumbnail(path, sample.Preview);
        return await OpenAsync(path, "Sample_" + sample.Name);
    }

    static IEnumerable<string> TryRepo(string sub)
    {
        string? repo = null;
        try { repo = NativeBuild.RepoRoot; } catch (InvalidOperationException) { }
        if (repo != null) yield return System.IO.Path.Combine(repo, sub);
    }

    // ---- Project Context Menu (T-M17) ---------------------------------------------------------------

    public void ShowSelectedInExplorer()
    {
        if (_selected is { } s && !Services.System.ShowInExplorer(s.Path)) Services.Toasts.Show($"{s.Name} was not found.", ToastStatus.Error);
    }

    /// <summary>Copies the project folder (without build output) next to it as "Name copy" and lists it first.</summary>
    public string? DuplicateSelected() => _selected is { } s ? Duplicate(s) : null;

    public string? Duplicate(RecentProject r)
    {
        try
        {
            var src = GameProject.Open(r.Path);
            var baseName = src.Settings.Name + " copy";
            var name = baseName;
            var parent = System.IO.Path.GetDirectoryName(src.Root)!;
            var folder = NewProjectRules.FolderFrom(parent, name);
            for (var i = 2; Directory.Exists(folder) || File.Exists(folder); i++)
            {
                name = $"{baseName} {i}";
                folder = NewProjectRules.FolderFrom(parent, name);
            }
            ProjectCreator.CopyDirectory(src.Root, folder, f => string.Equals(f, src.BuildDir, StringComparison.OrdinalIgnoreCase)
                || System.IO.Path.GetFileName(f) == ".git");
            var copyFile = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(src.ProjectPath));
            var settings = GameProject.Load<AgbStudio.Core.Project.ProjectFile>(copyFile);
            settings.Name = name;
            var path = System.IO.Path.Combine(folder, GameProject.FileName(name) + ".agbproj");
            GameProject.Save(path, settings);
            if (!string.Equals(path, copyFile, StringComparison.OrdinalIgnoreCase)) File.Delete(copyFile);
            SetThumbnail(path, Thumbnail(r.Path, src.Settings.Name));
            Services.Recent.Touch(name, path);
            Selected = Services.Recent.Items.First(x => Same(x.Path, path));
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Services.Toasts.Show($"Could not duplicate {r.Name}: {e.Message}", ToastStatus.Error);
            return null;
        }
    }

    public void RenameSelected()
    {
        if (_selected is { } s) RenameRequested?.Invoke(s);
    }

    /// <summary>Renames the project (the name in its .agbproj and the recent list; the folder stays).</summary>
    public bool Rename(RecentProject r, string newName)
    {
        newName = newName.Trim();
        if (!NewProjectRules.IsValidName(newName)) return false;
        try
        {
            var settings = GameProject.Load<AgbStudio.Core.Project.ProjectFile>(r.Path);
            if (settings.Name == newName) return true;
            settings.Name = newName;
            GameProject.Save(r.Path, settings);
            var art = Thumbnail(r.Path, r.Name);
            Services.Recent.Remove(r.Path);
            Services.Recent.Touch(newName, r.Path);
            SetThumbnail(r.Path, art);
            if (Services.Project.Current is { } cur && Same(cur.ProjectPath, r.Path)) Services.Project.Open(r.Path);
            Selected = Services.Recent.Items.FirstOrDefault(x => Same(x.Path, r.Path));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Services.Toasts.Show($"Could not rename {r.Name}: {e.Message}", ToastStatus.Error);
            return false;
        }
    }

    public void RemoveSelected()
    {
        if (_selected is { } s) Services.Recent.Remove(s.Path);
    }

    /// <summary>Delete project…: asks first, saying what is removed, then moves the folder to the Recycle Bin.</summary>
    public async Task<bool> DeleteSelectedAsync()
    {
        if (_selected is not { } s || Services.Windows.Host is not { } host) return false;
        var root = System.IO.Path.GetDirectoryName(s.Path)!;
        ProjectContents? contents = null;
        try { contents = ProjectContents.Of(GameProject.Open(s.Path)); }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException) { }
        var confirm = new DeleteProjectDialog(s.Name, root, contents);
        if (await host.Overlays.Show(confirm) as string != "delete") return false;
        if (Services.Project.Current is { } cur && Same(cur.ProjectPath, s.Path))
        {
            foreach (var d in Services.Documents.Open.ToList()) Services.Documents.Close(d);
            Services.Project.Close();
        }
        if (Directory.Exists(root) && !DeleteFolder(root))
        {
            Services.Toasts.Show($"Could not delete {root}.", ToastStatus.Error);
            return false;
        }
        Services.Recent.Remove(s.Path);
        _thumbnails.Remove(System.IO.Path.GetFullPath(s.Path));
        SaveThumbnails();
        return true;
    }
}

/// <summary>Moves a folder to the Windows Recycle Bin (SHFileOperation with undo), so Delete project can be undone in Explorer.</summary>
public static class RecycleBin
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShFileOp
    {
        public IntPtr Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        public int Aborted;
        public IntPtr NameMappings;
        public string? Title;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    static extern int SHFileOperation(ref ShFileOp op);

    const uint FoDelete = 3;
    const ushort FofSilent = 0x4, FofNoConfirmation = 0x10, FofAllowUndo = 0x40, FofNoErrorUi = 0x400;

    public static bool Delete(string folder)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var op = new ShFileOp { Func = FoDelete, From = Path.GetFullPath(folder) + "\0\0", Flags = FofSilent | FofNoConfirmation | FofAllowUndo | FofNoErrorUi };
        return SHFileOperation(ref op) == 0 && op.Aborted == 0 && !Directory.Exists(folder);
    }
}
