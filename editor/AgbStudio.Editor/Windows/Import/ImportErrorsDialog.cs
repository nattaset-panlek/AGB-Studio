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

    public IReadOnlyList<ImportItem> Items { get; }
    public IReadOnlyList<string>? Written { get; private set; }
    public string Title => _header.Title ?? "";
    public string ConfirmText => _footer.ConfirmText ?? "";

    public ImportErrorsDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as IReadOnlyList<ImportItem> ?? []) { }

    public ImportErrorsDialog(EditorServices s, IReadOnlyList<ImportItem> items)
    {
        _s = s;
        Items = items;
        Name = "ImportErrorsDialog";
        _cards.Margin = new Thickness(20, 20, 20, 20);
        _footer = new DialogFooter { CancelText = "Cancel", ConfirmText = "Import", Hint = "Files with errors are skipped." };
        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += (_, _) => Import();
        Build("Import Asset", "warn", new ScrollViewer { Content = _cards }, _footer, 720);
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

    public void Import()
    {
        Written = ImportReady(_s, Items);
        Close(Written);
    }

    /// <summary>Converts the ready files into the open project and reports the result in a toast.</summary>
    public static IReadOnlyList<string> ImportReady(EditorServices s, IEnumerable<ImportItem> items)
    {
        var p = s.Project.Current;
        var written = new List<string>();
        if (p is null) { s.Toasts.Show("Open a project before importing.", ToastStatus.Error); return written; }
        var count = 0;
        var console = SceneEditor.ConsoleLog.Of(s);
        var list = items.ToList();
        // G-ERR.03: no silent failure, every file that is not imported is named in the Console with its reason
        foreach (var item in list.Where(i => !i.IsReady))
            console.Add(ConsoleLevel.Warning, $"Import: {item.FileName} was not imported: {item.Error ?? item.Warning}");
        foreach (var item in list.Where(i => i.IsReady))
        {
            try
            {
                written.AddRange(AssetImporter.Import(p, item));
                count++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
            {
                s.Toasts.Show($"{item.FileName} could not be imported: {e.Message}", ToastStatus.Error);
                console.Add(ConsoleLevel.Error, $"Import: {item.FileName} could not be imported: {e.Message}", item.Source);
            }
        }
        if (count > 0) s.Toasts.Show(count == 1 ? "Imported 1 file" : $"Imported {count} files");
        return written;
    }
}
