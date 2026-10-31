using System.Text.Json;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Launcher;

public enum OpenStepState { Pending, Running, Done }

/// <summary>What Read project found: the project, its scenes, the asset files to load, the scripts and the scene to open.</summary>
public sealed record ProjectScan(GameProject Project, int Scenes, IReadOnlyList<string> Assets, int Scripts, string? LastScene)
{
    public static ProjectScan Read(string path)
    {
        var p = GameProject.Open(path);
        var scenes = p.SceneFiles().ToList();
        foreach (var f in scenes) GameProject.Load<SceneFile>(f);   // a broken scene stops the open here, before anything changes
        var names = scenes.Select(GameProject.AssetName).ToList();
        var s = p.Settings;
        var last = names.Contains(s.LastScene) ? s.LastScene : names.Contains(s.General.StartScene) ? s.General.StartScene : names.FirstOrDefault();
        var assets = p.SpriteFiles().Concat(p.TilesetFiles()).Concat(p.ImageFiles()).Concat(p.MapFiles()).Concat(p.FontFiles()).Concat(p.MusicFiles())
            .Concat(p.SoundFiles()).Concat(p.SfxFiles()).Concat(p.TableFiles()).Concat(p.DialogueFiles()).Concat(p.StringTables()).ToList();
        return new ProjectScan(p, scenes.Count, assets, p.ScriptFiles().Count(), last);
    }

    /// <summary>Loads one asset as the editor uses it: images are decoded, data files parsed, meta files read.</summary>
    public static void Load(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".png") Png.Read(file);
        else if (ext is ".map" or ".font" or ".db" or ".dlg" or ".sfx" or ".json") { using var _ = JsonDocument.Parse(File.ReadAllText(file)); }
        else File.ReadAllBytes(file);
        var meta = GameProject.MetaPath(file);
        if (File.Exists(meta)) { using var _ = JsonDocument.Parse(File.ReadAllText(meta)); }
    }

    /// <summary>Compiles the project's scripts into a temporary folder (no change to the project or the editor's script state).
    /// Returns the number of errors.</summary>
    public int Compile()
    {
        var p = Project;
        if (!p.ScriptFiles().Any() && !p.GraphFiles().Any() && !p.DialogueFiles().Any()) return 0;
        var output = Path.Combine(Path.GetTempPath(), "agb-open-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var scenes = p.SceneFiles().Select(GameProject.AssetName).Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
            var tags = p.Settings.Tags.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);
            var input = new ScriptInput { OutputDir = output, Debug = true, Scenes = scenes, Tags = tags, GameState = p.Settings.Save.Fields };
            var r = new ScriptBackend().Compile(p, input, NullObserver.Instance);
            return r.Diagnostics.Count(d => d.Severity == Severity.Error);
        }
        finally
        {
            try { if (Directory.Exists(output)) Directory.Delete(output, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}

/// <summary>
/// Opening Project (T-D22, 480 x 322; Launcher › Project card, File › Open Recent): Read project, Load assets with the
/// count, Compile scripts and Open last scene, then the Scene Editor. Cancel (or Esc / X) stops at once and leaves the
/// editor as it was: nothing is applied until the last step. Args: the .agbproj path. Result: the path, or null.
/// </summary>
[EditorWindow("T-D22")]
public sealed class OpeningProjectDialog : Border, IOverlayContent
{
    static readonly string[] Titles = ["Read project", "Load assets", "Compile scripts", "Open last scene"];

    readonly LauncherService _launcher;
    readonly string _path;
    readonly CancellationTokenSource _cts = new();
    readonly (Icon Icon, TextBlock Title, TextBlock Detail)[] _rows = new (Icon, TextBlock, TextBlock)[4];
    readonly AgbProgress _bar = new() { Status = ProgressStatus.Accent, Margin = new Thickness(0, 4, 0, 0), IsVisible = false, Name = "AssetProgress" };
    readonly bool _autoStart;
    bool _started, _finished;

    public event Action<object?>? Closed;
    /// <summary>Completes with the project path when it is open, or null (cancelled or failed).</summary>
    public Task<string?> Completion => _done.Task;
    readonly TaskCompletionSource<string?> _done = new();
    public OpenStepState[] States { get; } = new OpenStepState[4];
    public string ProjectName { get; }

    public OpeningProjectDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as string ?? "") { }

    public OpeningProjectDialog(EditorServices s, string path, bool autoStart = true)
    {
        Name = "OpeningProjectDialog";
        _launcher = LauncherService.Of(s);
        _path = path;
        _autoStart = autoStart;
        ProjectName = NameOf(s, path);
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("DialogSurface");
        Width = 480;
        var header = new DialogHeader { Title = $"Opening {ProjectName}…", Glyph = "folder" };
        header.CloseRequested += (_, _) => Cancel();
        var steps = new StackPanel { Spacing = 14, Margin = new Thickness(20, 20, 20, 0), Name = "OpenSteps" };
        for (var i = 0; i < 4; i++)
        {
            var icon = new Icon { Size = 16, VerticalAlignment = VerticalAlignment.Top };
            var title = UI.Text(Titles[i], "Body/12 Semibold", "text/muted");
            title.VerticalAlignment = VerticalAlignment.Top;
            var detail = UI.Text(null, "Label/11", "text/muted");
            detail.Margin = new Thickness(0, 4, 0, 0);
            var text = new StackPanel { Children = { title, detail } };
            if (i == 1) text.Children.Add(_bar);
            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("16,12,*"), Name = "Step" + i };
            Grid.SetColumn(text, 2);
            grid.Children.AddRange([icon, text]);
            steps.Children.Add(grid);
            _rows[i] = (icon, title, detail);
            SetStep(i, OpenStepState.Pending, null);
        }
        var cancel = new AgbButton { Content = "Cancel", Kind = ButtonKind.Secondary, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 20, 20), Name = "OpenCancel" };
        cancel.Click += (_, _) => Cancel();
        var body = new StackPanel { Children = { steps, cancel } };
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.AddRange([header, body]);
        Child = dock;
    }

    static string NameOf(EditorServices s, string path)
    {
        try { if (File.Exists(path)) return GameProject.Load<AgbStudio.Core.Project.ProjectFile>(path).Name; }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException or UnauthorizedAccessException) { }
        return s.Recent.Items.FirstOrDefault(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase))?.Name ?? Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>Shows a step: check (done), refresh (running) or clock (pending), with its detail and the asset progress.</summary>
    public void SetStep(int i, OpenStepState state, string? detail, double? progress = null)
    {
        States[i] = state;
        var (icon, title, text) = _rows[i];
        icon.Glyph = state switch { OpenStepState.Done => "check", OpenStepState.Running => "refresh", _ => "clock" };
        icon.Token(Icon.ForegroundProperty, state switch { OpenStepState.Done => "status/success", OpenStepState.Running => "accent/default", _ => "text/muted" });
        title.Token(TextBlock.ForegroundProperty, state == OpenStepState.Pending ? "text/muted" : "text/primary");
        if (detail != null) text.Text = detail;
        text.IsVisible = !string.IsNullOrEmpty(text.Text);
        if (i == 1)
        {
            _bar.IsVisible = state == OpenStepState.Running;
            if (progress is { } p) _bar.Value = p;
        }
    }

    public string? Detail(int i) => _rows[i].Detail.Text;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_started || !_autoStart) return;
        _started = true;
        _ = RunAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (!_finished) _cts.Cancel();
    }

    /// <summary>Cancel: stops the open; the project that was open stays open.</summary>
    public void Cancel()
    {
        _cts.Cancel();
        Finish(null);
    }

    void Finish(string? result)
    {
        if (_finished) return;
        _finished = true;
        _done.TrySetResult(result);
        Closed?.Invoke(result);
    }

    Task Gate(string step, CancellationToken ct) => _launcher.BeforeOpenStep?.Invoke(step, ct) ?? Task.CompletedTask;

    public async Task RunAsync()
    {
        _started = true;
        var ct = _cts.Token;
        try
        {
            if (!await _launcher.ConfirmUnsavedAsync()) { Finish(null); return; }
            ct.ThrowIfCancellationRequested();

            SetStep(0, OpenStepState.Running, null);
            await Gate(Titles[0], ct);
            var scan = await Task.Run(() => ProjectScan.Read(_path), ct);
            ct.ThrowIfCancellationRequested();
            SetStep(0, OpenStepState.Done, $"{Path.GetFileName(scan.Project.ProjectPath)} · {scan.Scenes} {(scan.Scenes == 1 ? "scene" : "scenes")}");
            SetStep(2, OpenStepState.Pending, $"{scan.Scripts} C# {(scan.Scripts == 1 ? "script" : "scripts")}");
            SetStep(3, OpenStepState.Pending, scan.LastScene ?? "");

            var n = scan.Assets.Count;
            SetStep(1, OpenStepState.Running, $"0 / {n}", 0);
            await Gate(Titles[1], ct);
            var unreadable = 0;
            string? firstUnreadable = null;
            for (var i = 0; i < n; i++)
            {
                var file = scan.Assets[i];
                var ok = await Task.Run(() =>
                {
                    try { ProjectScan.Load(file); return true; }
                    catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or FormatException or IndexOutOfRangeException) { return false; }
                }, ct);
                ct.ThrowIfCancellationRequested();
                if (!ok) { unreadable++; firstUnreadable ??= Path.GetFileName(file); }
                SetStep(1, OpenStepState.Running, $"{i + 1} / {n} · {Path.GetFileName(file)}", 100.0 * (i + 1) / n);
            }
            SetStep(1, OpenStepState.Done, $"{n} / {n}");

            SetStep(2, OpenStepState.Running, null);
            await Gate(Titles[2], ct);
            var compile = Task.Run(() =>
            {
                try { return scan.Compile(); }
                catch (Exception e) when (e is IOException or InvalidOperationException or JsonException or InvalidDataException or FormatException) { return 1; }
            });
            await Task.WhenAny(compile, Task.Delay(Timeout.Infinite, ct));
            ct.ThrowIfCancellationRequested();
            var errors = await compile;
            SetStep(2, OpenStepState.Done, $"{scan.Scripts} C# {(scan.Scripts == 1 ? "script" : "scripts")}" + (errors > 0 ? $" · {errors} {(errors == 1 ? "error" : "errors")}" : ""));

            SetStep(3, OpenStepState.Running, null);
            await Gate(Titles[3], ct);
            ct.ThrowIfCancellationRequested();
            // the only step that changes the editor: everything before it can be cancelled without a trace
            _launcher.CompleteOpen(scan.Project.ProjectPath, scan.LastScene);
            SetStep(3, OpenStepState.Done, null);
            if (unreadable > 0)
            {
                var text = unreadable == 1
                    ? $"{firstUnreadable} of {scan.Project.Settings.Name} could not be read. Its card in Assets says why; fix the file or import it again."
                    : $"{unreadable} assets of {scan.Project.Settings.Name} could not be read (first: {firstUnreadable}). Their cards in Assets say why; fix those files or import them again.";
                _launcher.Services.Toasts.Show(text, ToastStatus.Error);
            }
            Finish(scan.Project.ProjectPath);
        }
        catch (OperationCanceledException)
        {
            Finish(null);
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            // the project file texts (GameProject.Open / Load) say what is wrong and how to fix it; a file system error gets the fix here
            _launcher.Services.Toasts.Show((e is UnauthorizedAccessException or (IOException and not FileNotFoundException))
                ? $"Could not open {ProjectName}: {e.Message.TrimEnd('.')}. Check that the project folder is still there and no other program has it open, then open it again."
                : $"Could not open {ProjectName}: {e.Message}", ToastStatus.Error);
            Finish(null);
        }
    }
}
