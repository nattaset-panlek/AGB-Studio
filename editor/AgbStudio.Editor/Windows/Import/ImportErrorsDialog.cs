using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Build;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Import;

/// <summary>
/// Import Asset · Errors (T-D24, 720 x 436; Import Asset when a file can't be converted). One card per file with the
/// problem in plain language and its fix buttons (Reduce to 16, Split into N palettes, How to convert); files that are
/// ready say how they will be converted. Import converts the ready files; files with errors are skipped (P11.64).
/// </summary>
[EditorWindow("T-D24")]
public sealed class ImportErrorsDialog : FigmaDialog
{
    readonly EditorServices _s;
    readonly StackPanel _cards = new() { Spacing = 15, Name = "ProblemCards" };
    readonly DialogFooter _footer;
    readonly DialogHeader _header;
    readonly ImportRun _run;

    public IReadOnlyList<ImportItem> Items { get; }
    public IReadOnlyList<string>? Written { get; private set; }
    public string Title => _header.Title ?? "";
    public string ConfirmText => _footer.ConfirmText ?? "";
    public bool IsImporting => _run.IsImporting;

    public ImportErrorsDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as IReadOnlyList<ImportItem> ?? []) { }

    public ImportErrorsDialog(EditorServices s, IReadOnlyList<ImportItem> items)
    {
        _s = s;
        Items = items;
        Name = "ImportErrorsDialog";
        _cards.Margin = new Thickness(20, 20, 20, 20);
        _footer = new DialogFooter { CancelText = "Cancel", ConfirmText = "Import", Hint = "Files with errors are skipped." };
        var scroll = new ScrollViewer { Content = _cards };
        _run = new ImportRun(s, scroll, _footer);
        // Cancel while it imports stops the import (G-ERR.04); otherwise it closes
        _footer.Cancel += (_, _) => { if (!_run.Cancel()) Close(null); };
        _footer.Confirm += (_, _) => _ = Import();
        Build("Import Asset", "warn", scroll, _footer, 720);
        // closing the dialog (X, Esc) while it imports stops the import too
        DetachedFromVisualTree += (_, _) => _run.Cancel();
        _header = this.GetLogicalDescendantsOfType<DialogHeader>().First();
        Refresh();
    }

    void Refresh()
    {
        var problems = Items.Count(i => i.HasProblem);
        _header.Title = problems == 1 ? "Import Asset · 1 problem" : $"Import Asset · {problems} problems";
        var ready = Items.Count(i => i.IsReady);
        _footer.ConfirmText = ready == 1 ? "Import 1 file" : $"Import {ready} files";
        _cards.Children.Clear();
        foreach (var item in Items) _cards.Children.Add(Card(item));
    }

    Control Card(ImportItem item)
    {
        var (glyph, token) = item.Error != null ? ("x", "status/danger") : item.HasProblem ? ("warn", "status/warning") : ("check", "status/success");
        var icon = new Icon { Glyph = glyph, Size = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 0, 0) }.Token(Icon.ForegroundProperty, token);
        var title = UI.Text($"{item.FileName}  ·  {item.Detected}", "Body/12 Semibold", "text/primary");
        var message = UI.Text(item.Error ?? (item.HasProblem ? item.Warning : item.Ready), "Label/11", "text/secondary").Wrap();
        message.Margin = new Thickness(0, 5, 0, 0);
        message.MaxWidth = 420;
        message.HorizontalAlignment = HorizontalAlignment.Left;
        var col = UI.Column(0, title, message);
        if (item.HasProblem && item.Fixes.Count > 0)
        {
            var buttons = UI.Row(6);
            buttons.Margin = new Thickness(0, 4, 0, 0);
            for (var i = 0; i < item.Fixes.Count; i++)
            {
                var fix = item.Fixes[i];
                var b = new AgbButton { Content = fix, Kind = i == 0 && item.Error is null ? ButtonKind.Primary : ButtonKind.Secondary, Name = "Fix_" + fix.Replace(" ", "") };
                b.Click += (_, _) => ApplyFix(item, fix);
                buttons.Children.Add(b);
            }
            col.Children.Add(buttons);
        }
        var row = new DockPanel { HorizontalSpacing = 12 };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.AddRange([icon, col]);
        var card = UI.Frame(row, "bg/base", item.Error != null ? "status/danger" : "border/default", new Thickness(1), new Thickness(13, 12, 13, 12), 8);
        card.Name = "Card_" + item.FileName;
        return card;
    }

    /// <summary>Applies a fix button; "How to convert" explains the conversion instead.</summary>
    public void ApplyFix(ImportItem item, string fix)
    {
        if (fix == "How to convert")
        {
            _s.Toasts.Show($"Open {item.FileName} in an audio editor and export it as .wav (sound effect, 11 kHz mono), or rebuild the song in a tracker and save it as .xm or .mod (music).", ToastStatus.Error, TimeSpan.FromSeconds(12));
            return;
        }
        AssetImporter.ApplyFix(item, fix);
        Refresh();
    }

    /// <summary>Import: converts the ready files; Cancel while it imports stops it and leaves the project as it was (G-ERR.04).</summary>
    public async Task<IReadOnlyList<string>?> Import()
    {
        if (_run.IsImporting) return null;
        Written = await _run.RunAsync(Items);
        if (Written != null) Close(Written);
        return Written;
    }

    /// <summary>
    /// Converts the ready files into the open project off the UI thread and reports the result in a toast and the
    /// Console; the Assets panel then lists the new files. G-ERR.04: cancelling <paramref name="ct"/> stops the import
    /// and puts the project back as it was; the result is then null.
    /// </summary>
    public static async Task<IReadOnlyList<string>?> ImportReadyAsync(EditorServices s, IEnumerable<ImportItem> items, IProgress<ImportProgress>? progress = null,
        Func<int, CancellationToken, Task>? beforeFile = null, CancellationToken ct = default)
    {
        var p = s.Project.Current;
        if (p is null) { s.Toasts.Show("Open a project before importing.", ToastStatus.Error); return []; }
        var console = SceneEditor.ConsoleLog.Of(s);
        var list = items.ToList();
        // G-ERR.03: no silent failure, every file that is not imported is named in the Console with its reason
        foreach (var item in list.Where(i => !i.IsReady))
            console.Add(ConsoleLevel.Warning, $"Import: {item.FileName} was not imported: {item.Error ?? item.Warning}");
        ImportOutcome r;
        try { r = await Task.Run(() => AssetImporter.ImportAllAsync(p, list, progress, beforeFile, ct)); }
        catch (ImportCancelledException e)
        {
            foreach (var f in e.NotRestored)
                console.Add(ConsoleLevel.Warning, $"Import cancelled: {Path.GetFileName(f)} could not be put back as it was; another program has it open. Close that program, then check the file.", f);
            console.Add(ConsoleLevel.Warning, e.NotRestored.Count == 0 ? "Import cancelled. Nothing was added to the project." : $"Import cancelled. {e.NotRestored.Count} files could not be put back as they were.");
            s.Toasts.Show(e.NotRestored.Count == 0 ? "Import cancelled. Nothing was added to the project." : "Import cancelled, but some files could not be put back. The Console names them.",
                e.NotRestored.Count == 0 ? ToastStatus.Success : ToastStatus.Error, TimeSpan.FromSeconds(4));
            return null;
        }
        foreach (var (item, e) in r.Failed)
        {
            // a file the reader refuses says how to save it again; a file system error gets the fix here
            var text = (e is InvalidDataException)
                ? $"{item.FileName} could not be imported: {e.Message}"
                : $"{item.FileName} could not be imported: {e.Message.TrimEnd('.')}. Close programs that use the file or the project folder, then import it again.";
            s.Toasts.Show(text, ToastStatus.Error);
            console.Add(ConsoleLevel.Error, "Import: " + text, item.Source);
        }
        if (r.Imported > 0) s.Toasts.Show(r.Imported == 1 ? "Imported 1 file" : $"Imported {r.Imported} files");
        if (s.GetService(typeof(SceneEditor.AssetActions)) is SceneEditor.AssetActions assets) assets.FilesChanged();
        return r.Written;
    }
}

/// <summary>
/// The import run of Import Asset and Import Asset · Errors (G-ERR.04, in the manner of a build in Build ROM): the
/// footer hint follows the files, Cancel (and closing the dialog) stops the import, and the project is left as it was.
/// </summary>
sealed class ImportRun
{
    readonly EditorServices _s;
    readonly DialogFooter _footer;
    readonly Control _content;
    readonly string? _hint;
    CancellationTokenSource? _cts;
    AgbButton? _confirm;

    public ImportRun(EditorServices s, Control content, DialogFooter footer)
    {
        _s = s;
        _content = content;
        _footer = footer;
        _hint = footer.Hint;
        footer.TemplateApplied += (_, e) => _confirm = e.NameScope.Find<AgbButton>("PART_Confirm");
    }

    public bool IsImporting => _cts != null;
    public Func<int, CancellationToken, Task>? BeforeFile { get; set; }

    /// <summary>Stops the running import; false when none runs.</summary>
    public bool Cancel()
    {
        if (_cts is null) return false;
        _cts.Cancel();
        return true;
    }

    public async Task<IReadOnlyList<string>?> RunAsync(IEnumerable<ImportItem> items)
    {
        _cts = new CancellationTokenSource();
        _content.IsEnabled = false;
        if (_confirm != null) _confirm.IsEnabled = false;
        _footer.Hint = "Importing…";
        var progress = new Progress<ImportProgress>(x => { if (_cts != null && x.Done < x.Total) _footer.Hint = $"Importing {x.Done + 1} of {x.Total}: {x.File}"; });
        try
        {
            var written = await ImportErrorsDialog.ImportReadyAsync(_s, items, progress, BeforeFile, _cts.Token);
            _footer.Hint = written is null ? "Import cancelled. Press Import to try again." : _hint;
            return written;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _content.IsEnabled = true;
            if (_confirm != null) _confirm.IsEnabled = true;
        }
    }
}
