using System.Text;
using System.Text.RegularExpressions;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>What the Find &amp; Replace popover works on: the Script Editor page, the initial query and whether Replace was asked for.</summary>
public sealed record FindContext(ScriptEditorPage Page, string? Query, bool Replace);

public enum FindScope { ThisFile, AllScripts, WholeProject }

/// <summary>One match: file, offset and length in the file text, 1-based line / column.</summary>
public sealed record FindHit(string Path, int Start, int Length, int Line, int Column);

/// <summary>Find options of T-O11: match case (Aa), whole word (ab), regular expression (.*).</summary>
public sealed record FindQuery(string Text, bool MatchCase = false, bool WholeWord = false, bool Regex = false)
{
    /// <summary>The regular expression for the query, or null when it is empty or not a valid pattern.</summary>
    public Regex? Pattern()
    {
        if (Text.Length == 0) return null;
        var p = Regex ? Text : System.Text.RegularExpressions.Regex.Escape(Text);
        if (WholeWord) p = $@"(?<![\w]){p}(?![\w])";
        try { return new Regex(p, (MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
        catch (ArgumentException) { return null; }
    }

    public IEnumerable<Match> Matches(string text) => Pattern() is { } rx ? rx.Matches(text).Where(m => m.Length > 0) : [];

    /// <summary>Text that replaces one match ($1 groups in regular expression mode).</summary>
    public string Substitute(Match m, string replacement) => Regex ? m.Result(replacement) : replacement;
}

/// <summary>
/// The search behind Find &amp; Replace (P11.55): matches in this file, all scripts or the whole project (every text file
/// outside build/), replace one or all. Open scripts are searched and changed in their buffers (undoable, unsaved);
/// other files are rewritten on disk.
/// </summary>
public static class ProjectSearch
{
    static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".graph", ".dlg", ".scene", ".map", ".json", ".db", ".agbproj", ".sfx", ".font", ".txt", ".md", ".csv", ".editorconfig", ".props" };
    static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase) { "build", "bin", "obj", ".git", ".vs" };

    public static IReadOnlyList<string> Files(string? root, FindScope scope)
    {
        if (root is null || !Directory.Exists(root)) return [];
        var list = new List<string>();
        var stack = new Stack<string>([root]);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var d in Directory.EnumerateDirectories(dir)) if (!SkipDirs.Contains(Path.GetFileName(d))) stack.Push(d);
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                var ext = Path.GetExtension(f);
                if (scope == FindScope.AllScripts ? ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) : TextExtensions.Contains(ext)) list.Add(Path.GetFullPath(f));
            }
        }
        return list.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>All matches of <paramref name="q"/> in the given texts (path, text).</summary>
    public static List<FindHit> Search(IEnumerable<(string Path, string Text)> texts, FindQuery q)
    {
        var hits = new List<FindHit>();
        foreach (var (path, text) in texts)
        {
            int line = 1, lineStart = 0, scanned = 0;
            foreach (var m in q.Matches(text))
            {
                for (; scanned < m.Index; scanned++) if (text[scanned] == '\n') { line++; lineStart = scanned + 1; }
                hits.Add(new FindHit(path, m.Index, m.Length, line, m.Index - lineStart + 1));
            }
        }
        return hits;
    }

    /// <summary>Replaces every match in a text; returns the new text and the count.</summary>
    public static (string Text, int Count) ReplaceAll(string text, FindQuery q, string replacement)
    {
        if (q.Pattern() is not { } rx) return (text, 0);
        var count = 0;
        var result = rx.Replace(text, m => { if (m.Length == 0) return m.Value; count++; return q.Substitute(m, replacement); });
        return (result, count);
    }
}

/// <summary>
/// Find &amp; Replace (T-O11, 540 x 124; Script Editor › Ctrl+F / Ctrl+H): query with match count, Aa / ab / .* toggles,
/// previous / next / close, replacement with Replace and Replace all, Search in This file / All scripts / Whole project.
/// </summary>
[EditorWindow("T-O11")]
public sealed class FindReplacePopover : Border, IOverlayContent
{
    readonly ScriptEditorPage? _page;
    readonly EditorServices? _s;
    readonly TextBlock _count = UI.Text("", "Label/11", "text/muted");
    readonly ToggleSegment _case = new() { Content = "Aa", Name = "MatchCase" };
    readonly ToggleSegment _word = new() { Content = "ab", Name = "WholeWord" };
    readonly ToggleSegment _regex = new() { Content = ".*", Name = "Regex" };
    readonly Segment[] _scopes;
    List<FindHit> _hits = [];
    int _index = -1;

    public event Action<object?>? Closed;
    public TextBox Query { get; } = new() { Name = "FindQuery" };
    public TextBox Replacement { get; } = new() { Name = "ReplaceText" };
    public FindScope Scope { get; private set; } = FindScope.ThisFile;
    public IReadOnlyList<FindHit> Hits => _hits;
    public int Index => _index;
    public string CountText => _count.Text ?? "";

    public FindReplacePopover(WindowContext ctx) : this(ctx.Args as FindContext, ctx.Services) { }

    public FindReplacePopover(FindContext? context, EditorServices? services = null)
    {
        _page = context?.Page;
        _s = services;
        Name = "FindReplace";
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = 540;
        // Figma: 8 px inside the border (10 at the bottom); 30 px fields, 26 px buttons, rows 6 and 8 px apart
        Padding = new Thickness(8, 8, 8, 10);

        foreach (var box in new[] { Query, Replacement })
        {
            box.Height = box == Query ? 28 : 30;
            box.Padding = new Thickness(8, 0, 8, 0);
            AgbStudio.Editor.Theme.Fig.SetStyle(box, "Mono/11");
        }
        var toggles = UI.Row(4, _case, _word, _regex);
        foreach (var t in new[] { _case, _word, _regex })
        {
            t.Width = 34;
            t.Click += (_, _) => Refresh(keepPosition: true);
        }
        _count.Margin = new Thickness(0, 0, 8, 0);
        var inBox = UI.Row(0, _count, toggles);
        inBox.VerticalAlignment = VerticalAlignment.Center;
        // the query field holds the match count and the Aa / ab / .* toggles: a Text Input frame around a bare text box
        Query.Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareTextBox");
        Query.VerticalAlignment = VerticalAlignment.Center;
        var queryDock = new DockPanel();
        DockPanel.SetDock(inBox, Dock.Right);
        queryDock.Children.AddRange([inBox, Query]);
        var queryFrame = UI.Frame(queryDock, "bg/base", "border/default", new Thickness(1), new Thickness(9, 0, 2, 0), 6).Named("FindQueryFrame");
        queryFrame.Height = 30;
        Query.GotFocus += (_, _) => queryFrame.Token(Border.BorderBrushProperty, "accent/default");
        Query.LostFocus += (_, _) => queryFrame.Token(Border.BorderBrushProperty, "border/default");
        var prev = new IconButton { Glyph = "undo", Name = "FindPrevious" };
        prev.Click += (_, _) => Step(-1);
        ToolTip.SetTip(prev, new TipContent("Previous match", "Shift+Enter"));
        var next = new IconButton { Glyph = "down", Name = "FindNext" };
        next.Click += (_, _) => Step(1);
        ToolTip.SetTip(next, new TipContent("Next match", "Enter"));
        var close = new IconButton { Glyph = "x", Name = "FindClose" };
        close.Click += (_, _) => Closed?.Invoke(null);
        var row1 = new DockPanel { HorizontalSpacing = 6 };
        var icons = UI.Row(6, prev, next, close);
        PageChrome.DockTo(row1, icons, Dock.Right);
        row1.Children.Add(queryFrame);

        var replace = new AgbButton { Content = "Replace", Kind = ButtonKind.Secondary, Height = 26, VerticalAlignment = VerticalAlignment.Center, Name = "ReplaceOne" };
        replace.Click += (_, _) => ReplaceCurrent();
        var replaceAll = new AgbButton { Content = "Replace all", Kind = ButtonKind.Primary, Height = 26, VerticalAlignment = VerticalAlignment.Center, Name = "ReplaceAll" };
        replaceAll.Click += (_, _) => ReplaceEverything();
        var row2 = new DockPanel { HorizontalSpacing = 6 };
        var buttons = UI.Row(6, replace, replaceAll);
        buttons.VerticalAlignment = VerticalAlignment.Center;
        PageChrome.DockTo(row2, buttons, Dock.Right);
        row2.Children.Add(Replacement);

        _scopes =
        [
            new Segment { Content = "This file", IsChecked = true, GroupName = "FindScope", Name = "Scope_ThisFile" },
            new Segment { Content = "All scripts", GroupName = "FindScope", Name = "Scope_AllScripts" },
            new Segment { Content = "Whole project", GroupName = "FindScope", Name = "Scope_WholeProject" },
        ];
        for (var i = 0; i < _scopes.Length; i++)
        {
            var scope = (FindScope)i;
            _scopes[i].IsCheckedChanged += (_, _) => { if (_scopes[(int)scope].IsChecked == true) { Scope = scope; Refresh(keepPosition: false); } };
        }
        var scopeGroup = UI.Frame(UI.Row(2, _scopes), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);
        var searchIn = UI.Text("Search in", "Label/11", "text/muted");
        searchIn.Margin = new Thickness(2, 0, 0, 0);
        var row3 = UI.Row(8, searchIn, scopeGroup);
        row2.Margin = new Thickness(0, 6, 0, 0);
        row3.Margin = new Thickness(0, 8, 0, 0);
        Child = UI.Column(0, row1, row2, row3);

        Query.Text = context?.Query ?? WordAtCaret();
        Query.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(keepPosition: false); };
        Query.AddHandler(KeyDownEvent, OnQueryKey, RoutingStrategies.Tunnel);
        Replacement.AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Enter) { ReplaceCurrent(); e.Handled = true; } else if (e.Key == Key.Escape) { Closed?.Invoke(null); e.Handled = true; } }, RoutingStrategies.Tunnel);
        Refresh(keepPosition: false);
        if (context?.Replace == true) AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => Replacement.Focus());
    }

    string WordAtCaret()
    {
        if (_page?.Editor is not { } ed) return "";
        var t = ed.Text;
        int a = ed.CaretOffset, b = ed.CaretOffset;
        while (a > 0 && (char.IsLetterOrDigit(t[a - 1]) || t[a - 1] == '_')) a--;
        while (b < t.Length && (char.IsLetterOrDigit(t[b]) || t[b] == '_')) b++;
        return t[a..b];
    }

    public FindQuery CurrentQuery => new(Query.Text ?? "", _case.IsChecked == true, _word.IsChecked == true, _regex.IsChecked == true);

    /// <summary>Sets the toggles (tests and keyboard users).</summary>
    public void SetOptions(bool matchCase, bool wholeWord, bool regex)
    {
        _case.IsChecked = matchCase;
        _word.IsChecked = wholeWord;
        _regex.IsChecked = regex;
        Refresh(keepPosition: false);
    }

    public void SetScope(FindScope scope) => _scopes[(int)scope].IsChecked = true;

    IEnumerable<(string Path, string Text)> Texts()
    {
        if (_page?.Buffer is not { } current) yield break;
        if (Scope == FindScope.ThisFile)
        {
            yield return (current.Path, _page.Editor.Text);
            yield break;
        }
        var root = _s?.Project.Current?.Root;
        foreach (var f in ProjectSearch.Files(root, Scope))
        {
            var open = _page.Workspace.Buffers.FirstOrDefault(b => string.Equals(b.Path, f, StringComparison.OrdinalIgnoreCase));
            string text;
            if (open == current) text = _page.Editor.Text;
            else if (open != null) text = open.Text;
            else
            {
                try { text = File.ReadAllText(f).Replace("\r\n", "\n"); }
                catch (IOException) { continue; }
            }
            yield return (f, text);
        }
    }

    void Refresh(bool keepPosition)
    {
        var q = CurrentQuery;
        var valid = q.Text.Length > 0 && q.Pattern() != null;
        _hits = valid ? ProjectSearch.Search(Texts(), q) : [];
        if (_hits.Count == 0) _index = -1;
        else if (!keepPosition || _index < 0 || _index >= _hits.Count)
        {
            // the first match at or after the caret of the current file
            var caret = _page?.Editor.CaretOffset ?? 0;
            var path = _page?.Buffer?.Path;
            var i = _hits.FindIndex(h => string.Equals(h.Path, path, StringComparison.OrdinalIgnoreCase) && h.Start + h.Length >= caret);
            _index = i >= 0 ? i : 0;
        }
        _count.Text = q.Text.Length == 0 ? "" : !valid ? "Invalid" : _hits.Count == 0 ? "No results" : $"{_index + 1} of {_hits.Count}";
    }

    void OnQueryKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter: Step(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1); e.Handled = true; break;
            case Key.Escape: Closed?.Invoke(null); e.Handled = true; break;
        }
    }

    /// <summary>Moves to the next (+1) or previous (-1) match and selects it in the editor.</summary>
    public void Step(int delta)
    {
        if (_hits.Count == 0) return;
        _index = ((_index < 0 ? 0 : _index + delta) % _hits.Count + _hits.Count) % _hits.Count;
        Reveal(_hits[_index]);
        _count.Text = $"{_index + 1} of {_hits.Count}";
    }

    void Reveal(FindHit h)
    {
        if (_page is null) return;
        if (!h.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return;
        if (_page.Buffer is null || !string.Equals(_page.Buffer.Path, h.Path, StringComparison.OrdinalIgnoreCase)) _page.Open(h.Path);
        _page.Editor.Select(h.Start, h.Start + h.Length);
    }

    /// <summary>Replaces the current match and moves to the next.</summary>
    public void ReplaceCurrent()
    {
        if (_page is null || _index < 0 || _index >= _hits.Count) return;
        var h = _hits[_index];
        var q = CurrentQuery;
        var repl = Replacement.Text ?? "";
        if (string.Equals(_page.Buffer?.Path, h.Path, StringComparison.OrdinalIgnoreCase) || h.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            Reveal(h);
            var text = _page.Editor.Text;
            var m = q.Pattern()?.Match(text, h.Start);
            if (m is null || !m.Success || m.Index != h.Start) return;
            _page.Editor.Replace(h.Start, h.Length, q.Substitute(m, repl), "Replace");
        }
        else
        {
            var text = File.ReadAllText(h.Path).Replace("\r\n", "\n");
            var m = q.Pattern()?.Match(text, h.Start);
            if (m is null || !m.Success) return;
            File.WriteAllText(h.Path, text[..h.Start] + q.Substitute(m, repl) + text[(h.Start + h.Length)..], new UTF8Encoding(false));
        }
        var keep = _index;
        Refresh(keepPosition: true);
        _index = _hits.Count == 0 ? -1 : Math.Min(keep, _hits.Count - 1);
        if (_index >= 0) { Reveal(_hits[_index]); _count.Text = $"{_index + 1} of {_hits.Count}"; }
    }

    /// <summary>Replaces every match in the scope; returns the number replaced.</summary>
    public int ReplaceEverything()
    {
        if (_page?.Buffer is not { } current) return 0;
        var q = CurrentQuery;
        var repl = Replacement.Text ?? "";
        var total = 0;
        foreach (var path in _hits.Select(h => h.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            var open = _page.Workspace.Buffers.FirstOrDefault(b => string.Equals(b.Path, path, StringComparison.OrdinalIgnoreCase));
            if (open == current)
            {
                var (text, n) = ProjectSearch.ReplaceAll(_page.Editor.Text, q, repl);
                _page.Editor.ReplaceAll(text, "Replace all");
                total += n;
            }
            else if (open != null)
            {
                var (text, n) = ProjectSearch.ReplaceAll(open.Text, q, repl);
                _page.Workspace.SetText(open, text);
                total += n;
            }
            else
            {
                var (text, n) = ProjectSearch.ReplaceAll(File.ReadAllText(path).Replace("\r\n", "\n"), q, repl);
                File.WriteAllText(path, text, new UTF8Encoding(false));
                total += n;
            }
        }
        Refresh(keepPosition: false);
        _s?.Toasts.Show($"Replaced {total} match{(total == 1 ? "" : "es")}");
        return total;
    }
}

/// <summary>A Figma Segment that toggles on its own (Aa / ab / .* of Find &amp; Replace) instead of acting as a radio button.</summary>
public sealed class ToggleSegment : Segment
{
    protected override Type StyleKeyOverride => typeof(Segment);

    public ToggleSegment() => GroupName = Guid.NewGuid().ToString("N");

    protected override void Toggle() => IsChecked = IsChecked != true;
}
