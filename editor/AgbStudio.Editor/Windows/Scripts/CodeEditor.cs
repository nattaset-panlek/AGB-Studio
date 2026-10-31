using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Theme;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>
/// The built-in code editor of the Script Editor (P11.52): line numbers, C# colours, caret and selection, typing with
/// auto-indent and tab size, undo through the editor's undo history, wavy underlines for analyzer messages, the
/// autocomplete list and clicks on an underline (the page shows the diagnostic popup). Also used read-only for the
/// Scripting API example and the Logic Graph's Generated C# panel.
/// </summary>
public sealed class CodeEditor : Panel, IEditTarget
{
    string _text = "";
    readonly List<int> _lineStarts = [0];
    int _caret, _anchor;
    double _desiredX = -1;
    double _scrollY, _scrollX;
    readonly Dictionary<int, TextLayout> _layouts = [];
    IReadOnlyList<CodeSpan> _spans = [];
    IReadOnlyList<CodeIssue> _issues = [];
    TextEditAction? _lastEdit;
    int _lastEditUndoCount = -1;
    DateTime _lastEditTime;
    string _clipboardFallback = "";
    bool _dragging;
    readonly Control _surface;

    void Redraw() => _surface?.InvalidateVisual();

    readonly Border _completionBox;
    readonly StackPanel _completionRows = new() { Spacing = 0 };
    List<CompletionItem> _completionAll = [];
    readonly List<CompletionItem> _completionShown = [];
    int _completionStart = -1, _completionIndex;
    CancellationTokenSource? _completionCancel;

    public CodeEditor()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        this.Token(BackgroundProperty, "bg/base");
        _completionBox = new Border
        {
            Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("MenuSurfaceBorder"),
            Width = 300, IsVisible = false, Child = _completionRows, Name = "Completion", Cursor = new Cursor(StandardCursorType.Arrow),
        };
        Overlay.Children.Add(_completionBox);
        _surface = new TextSurface(this);
        Children.Add(_surface);
        Children.Add(Overlay);
        ActualThemeVariantChanged += (_, _) => Invalidate();
    }

    // ---- options ---------------------------------------------------------------------------------------------

    /// <summary>Spaces per indent level (Preferences · Editor · Tab size).</summary>
    public int TabSize { get; set; } = 4;
    public bool IsReadOnly { get; set; }
    public bool ShowLineNumbers { get; set; } = true;
    /// <summary>Show the autocomplete list while typing (Preferences · Editor · Autocomplete as you type).</summary>
    public bool AutocompleteAsYouType { get; set; } = true;
    /// <summary>Wavy underlines for analyzer messages (Preferences · Editor · Underline analyzer warnings).</summary>
    public bool UnderlineIssues { get; set; } = true;
    /// <summary>Figma text style of the code (Mono/13 in the Script Editor, Mono/11 in the Generated C# panel).</summary>
    public string TextStyleName { get; set; } = "Mono/13";
    public double FontSizeOverride { get; set; }
    public Thickness TextPadding { get; set; } = new(0, 12, 0, 12);
    public double GutterWidth { get; set; } = 60;
    /// <summary>A click in the line-number gutter (1-based line): the Script Editor toggles a breakpoint there.</summary>
    public event Action<int>? GutterClicked;
    /// <summary>Lines (1-based) that show a breakpoint dot in the gutter.</summary>
    public Func<int, bool>? HasBreakpoint { get; set; }
    public UndoService? Undo { get; set; }
    /// <summary>Autocomplete source: (text, caret offset) -> items. Runs off the UI thread.</summary>
    public Func<string, int, CancellationToken, IReadOnlyList<CompletionItem>>? CompletionProvider { get; set; }

    public event Action? TextChanged;
    public event Action? CaretMoved;
    /// <summary>A click on an underlined message (or Ctrl+. on it).</summary>
    public event Action<CodeIssue>? IssueActivated;

    TextStyle Style => TextStyles.Get(TextStyleName);
    double FontSize => FontSizeOverride > 0 ? FontSizeOverride : Style.Size;
    /// <summary>The style's line height, grown or shrunk with a font size override (Preferences › Code font size).</summary>
    public double LineHeight => Style.LineHeight is { } lh ? (FontSizeOverride > 0 ? Math.Ceiling(lh * FontSize / Style.Size) : lh) : Math.Ceiling(FontSize * 1.5);
    double Gutter => ShowLineNumbers ? GutterWidth : 0;
    Typeface Face => new(FontFamilyOverride ?? Style.FontFamily, FontStyle.Normal, Style.FontWeight);

    /// <summary>Preferences › Editor › Font of the built-in code editor (null: the text style's font).</summary>
    public FontFamily? FontFamilyOverride
    {
        get => _fontFamily;
        set { if (Equals(_fontFamily, value)) return; _fontFamily = value; _layouts.Clear(); Redraw(); }
    }
    FontFamily? _fontFamily;

    // ---- text model ------------------------------------------------------------------------------------------

    public string Text
    {
        get => _text;
        set
        {
            SetTextCore(value ?? "", 0);
            _scrollY = _scrollX = 0;
            _lastEdit = null;
            TextChanged?.Invoke();
        }
    }

    public int LineCount => _lineStarts.Count;
    public int CaretOffset => _caret;
    /// <summary>1-based line and column of the caret (status line "Ln 8, Col 5").</summary>
    public int CaretLine => LineOf(_caret) + 1;
    public int CaretColumn => _caret - _lineStarts[LineOf(_caret)] + 1;
    public bool HasSelection => _caret != _anchor;
    public string SelectedText => _text[Math.Min(_caret, _anchor)..Math.Max(_caret, _anchor)];
    public IReadOnlyList<CodeIssue> Issues => _issues;

    public IReadOnlyList<CodeSpan> Spans
    {
        get => _spans;
        set { _spans = value; _layouts.Clear(); Redraw(); }
    }

    public void SetIssues(IReadOnlyList<CodeIssue> issues)
    {
        _issues = issues;
        Redraw();
    }

    void SetTextCore(string text, int caret)
    {
        _text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        _lineStarts.Clear();
        _lineStarts.Add(0);
        for (var i = 0; i < _text.Length; i++) if (_text[i] == '\n') _lineStarts.Add(i + 1);
        _caret = _anchor = Math.Clamp(caret, 0, _text.Length);
        _desiredX = -1;
        _layouts.Clear();
        Redraw();
    }

    public int LineOf(int offset)
    {
        var i = _lineStarts.BinarySearch(Math.Clamp(offset, 0, _text.Length));
        return i >= 0 ? i : ~i - 1;
    }

    public int LineStart(int line) => _lineStarts[Math.Clamp(line, 0, _lineStarts.Count - 1)];
    public int LineEnd(int line) => line + 1 < _lineStarts.Count ? _lineStarts[line + 1] - 1 : _text.Length;
    public string LineText(int line) => _text[LineStart(line)..LineEnd(line)];

    /// <summary>Offset of a 1-based line and column.</summary>
    public int OffsetOf(int line, int column) => Math.Min(LineStart(line - 1) + Math.Max(0, column - 1), LineEnd(line - 1));

    public void Select(int start, int end)
    {
        _anchor = Math.Clamp(start, 0, _text.Length);
        _caret = Math.Clamp(end, 0, _text.Length);
        _desiredX = -1;
        EnsureVisible();
        Redraw();
        CaretMoved?.Invoke();
    }

    public void MoveCaret(int line, int column) => Select(OffsetOf(line, column), OffsetOf(line, column));

    /// <summary>Replaces a range and records one undo step (code fixes, Find &amp; Replace, exposed field edits).</summary>
    public void Replace(int start, int length, string text, string undoName = "Edit code")
    {
        if (IsReadOnly) return;
        Edit(start, length, text, undoName, coalesce: false);
    }

    /// <summary>Replaces the whole text as one undoable step (format on save, quick fixes).</summary>
    public void ReplaceAll(string text, string undoName)
    {
        if (text == _text || IsReadOnly) return;
        var caretLine = CaretLine;
        var caretCol = CaretColumn;
        var before = Snapshot();
        SetTextCore(text, 0);
        _caret = _anchor = OffsetOf(Math.Min(caretLine, LineCount), caretCol);
        Record(new TextEditAction(undoName, this, before, Snapshot()), false);
        AfterEdit();
    }

    (string Text, int Caret, int Anchor) Snapshot() => (_text, _caret, _anchor);

    void Restore((string Text, int Caret, int Anchor) s)
    {
        SetTextCore(s.Text, s.Caret);
        _anchor = Math.Clamp(s.Anchor, 0, _text.Length);
        AfterEdit();
    }

    void Edit(int start, int length, string insert, string undoName, bool coalesce)
    {
        start = Math.Clamp(start, 0, _text.Length);
        length = Math.Clamp(length, 0, _text.Length - start);
        var before = Snapshot();
        var delta = insert.Length - length;
        _text = string.Concat(_text.AsSpan(0, start), insert, _text.AsSpan(start + length));
        // shift colours and underlines so the text keeps its look until the next analysis
        _spans = _spans.Where(s => s.Start + s.Length <= start || s.Start >= start + length)
            .Select(s => s.Start >= start + length ? s with { Start = s.Start + delta } : s).ToList();
        _issues = _issues.Where(i => i.End <= start || i.Start >= start + length)
            .Select(i => i.Start >= start + length ? i with { Start = i.Start + delta, End = i.End + delta } : i).ToList();
        var caret = start + insert.Length;
        SetTextCore(_text, caret);
        Record(new TextEditAction(undoName, this, before, Snapshot()), coalesce);
        AfterEdit();
    }

    void Record(TextEditAction action, bool coalesce)
    {
        if (Undo is null) return;
        var now = DateTime.UtcNow;
        if (coalesce && _lastEdit is { } last && Undo.UndoCount == _lastEditUndoCount && !Undo.CanRedo && Undo.NextUndo == last.Name && (now - _lastEditTime).TotalSeconds < 1.5)
        {
            last.After = action.After;
        }
        else
        {
            Undo.Push(action);
            _lastEdit = coalesce ? action : null;
            _lastEditUndoCount = Undo.UndoCount;
        }
        _lastEditTime = now;
    }

    void AfterEdit()
    {
        EnsureVisible();
        Redraw();
        TextChanged?.Invoke();
        CaretMoved?.Invoke();
    }

    sealed class TextEditAction(string name, CodeEditor editor, (string, int, int) before, (string, int, int) after) : IUndoableAction
    {
        public string Name { get; } = name;
        public (string Text, int Caret, int Anchor) Before { get; } = before;
        public (string Text, int Caret, int Anchor) After { get; set; } = after;
        public void Undo() => editor.Restore(Before);
        public void Redo() => editor.Restore(After);
    }

    // ---- typing ----------------------------------------------------------------------------------------------

    /// <summary>Types text at the caret (replaces the selection), as keyboard text input does.</summary>
    public void TypeText(string s)
    {
        if (IsReadOnly || s.Length == 0) return;
        var (a, b) = (Math.Min(_caret, _anchor), Math.Max(_caret, _anchor));
        // a closing brace on an otherwise blank line moves back one indent level
        if (s == "}" && a == b && LineText(LineOf(a)).Trim().Length == 0 && a - LineStart(LineOf(a)) >= TabSize && LineEnd(LineOf(a)) == a)
        {
            Edit(a - TabSize, TabSize, "}", "Typing", coalesce: true);
        }
        else Edit(a, b - a, s, "Typing", coalesce: s.Length == 1 && a == b);
        if (s.EndsWith('.')) RequestCompletion(_caret);
        else if (_completionBox.IsVisible) FilterCompletion();
        else if (AutocompleteAsYouType && s.Length == 1 && IsIdent(s[0]) && WordStart(_caret) == _caret - 1 && !InCommentOrString(_caret)) RequestCompletion(_caret - 1);
    }

    bool InCommentOrString(int offset) => _spans.Any(sp => sp.Kind is CodeKind.Comment or CodeKind.String && offset > sp.Start && offset <= sp.Start + sp.Length);

    static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

    int WordStart(int offset)
    {
        var i = offset;
        while (i > 0 && IsIdent(_text[i - 1])) i--;
        return i;
    }

    int WordEnd(int offset)
    {
        var i = offset;
        while (i < _text.Length && IsIdent(_text[i])) i++;
        return i;
    }

    void NewLine()
    {
        var line = LineOf(_caret);
        var text = LineText(line);
        var indent = text[..(text.Length - text.TrimStart(' ').Length)];
        var before = _text[LineStart(line)..Math.Min(_caret, _anchor)].TrimEnd();
        if (before.EndsWith('{')) indent += new string(' ', TabSize);
        var (a, b) = (Math.Min(_caret, _anchor), Math.Max(_caret, _anchor));
        Edit(a, b - a, "\n" + indent, "New line", coalesce: false);
    }

    void Backspace(bool word)
    {
        if (HasSelection) { DeleteSelection(); return; }
        if (_caret == 0) return;
        var start = word ? WordStart(_caret - 1 >= 0 && !IsIdent(_text[_caret - 1]) ? _caret - 1 : _caret) : _caret - 1;
        var lineStart = LineStart(LineOf(_caret));
        // in leading spaces, remove back to the previous tab stop
        if (!word && _caret > lineStart && _text[lineStart.._caret].All(c => c == ' '))
        {
            var col = _caret - lineStart;
            start = _caret - ((col - 1) % TabSize + 1);
        }
        Edit(start, _caret - start, "", "Delete", coalesce: true);
        if (_completionBox.IsVisible) FilterCompletion();
    }

    void DeleteForward(bool word)
    {
        if (HasSelection) { DeleteSelection(); return; }
        if (_caret >= _text.Length) return;
        var end = word ? Math.Max(_caret + 1, WordEnd(_caret)) : _caret + 1;
        Edit(_caret, end - _caret, "", "Delete", coalesce: false);
    }

    void DeleteSelection()
    {
        var (a, b) = (Math.Min(_caret, _anchor), Math.Max(_caret, _anchor));
        if (a != b) Edit(a, b - a, "", "Delete", coalesce: false);
    }

    void Indent(bool back)
    {
        var first = LineOf(Math.Min(_caret, _anchor));
        var last = LineOf(Math.Max(_caret, _anchor));
        if (!back && first == last && !HasSelection)
        {
            var col = _caret - LineStart(first);
            Edit(_caret, 0, new string(' ', TabSize - col % TabSize), "Typing", coalesce: true);
            return;
        }
        var before = Snapshot();
        var lines = Enumerable.Range(first, last - first + 1).Select(LineText).ToList();
        var changed = lines.Select(l => back ? l[Math.Min(l.Length - l.TrimStart(' ').Length, TabSize)..] : new string(' ', TabSize) + l).ToList();
        var start = LineStart(first);
        var end = LineEnd(last);
        var replacement = string.Join("\n", changed);
        _text = string.Concat(_text.AsSpan(0, start), replacement, _text.AsSpan(end));
        SetTextCore(_text, start);
        _anchor = start;
        _caret = start + replacement.Length;
        Record(new TextEditAction(back ? "Outdent" : "Indent", this, before, Snapshot()), false);
        AfterEdit();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrEmpty(e.Text) || IsReadOnly) return;
        var text = new string(e.Text.Where(c => c == '\t' || !char.IsControl(c)).ToArray());
        if (text.Length == 0) return;
        TypeText(text.Replace("\t", new string(' ', TabSize)));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_completionBox.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Down: HighlightCompletion(_completionIndex + 1); e.Handled = true; return;
                case Key.Up: HighlightCompletion(_completionIndex - 1); e.Handled = true; return;
                case Key.Enter or Key.Tab: CommitCompletion(); e.Handled = true; return;
                case Key.Escape: CloseCompletion(); e.Handled = true; return;
            }
        }
        switch (e.Key)
        {
            case Key.Left: MoveTo(ctrl ? WordLeft() : Math.Max(0, _caret - 1), shift, keepX: false); break;
            case Key.Right: MoveTo(ctrl ? WordRight() : Math.Min(_text.Length, _caret + 1), shift, keepX: false); break;
            case Key.Up: MoveVertical(-1, shift); break;
            case Key.Down: MoveVertical(1, shift); break;
            case Key.PageUp: MoveVertical(-VisibleLines, shift); break;
            case Key.PageDown: MoveVertical(VisibleLines, shift); break;
            case Key.Home:
                if (ctrl) MoveTo(0, shift, false);
                else
                {
                    var ls = LineStart(LineOf(_caret));
                    var first = ls + (LineText(LineOf(_caret)).Length - LineText(LineOf(_caret)).TrimStart(' ').Length);
                    MoveTo(_caret == first ? ls : first, shift, false);
                }
                break;
            case Key.End: MoveTo(ctrl ? _text.Length : LineEnd(LineOf(_caret)), shift, false); break;
            case Key.Enter when !IsReadOnly: NewLine(); break;
            case Key.Back when !IsReadOnly: Backspace(ctrl); break;
            case Key.Delete when !IsReadOnly && !ctrl && !shift: DeleteForward(false); break;
            case Key.Tab when !IsReadOnly: Indent(shift); break;
            case Key.Space when ctrl: RequestCompletion(WordStart(_caret)); break;
            case Key.OemPeriod when ctrl:
                if (IssueAt(_caret) is { } issue) IssueActivated?.Invoke(issue);
                break;
            case Key.Escape when _completionBox.IsVisible: CloseCompletion(); break;
            default: return;
        }
        e.Handled = true;
    }

    int VisibleLines => Math.Max(1, (int)((Bounds.Height - TextPadding.Top) / LineHeight) - 1);

    int WordLeft()
    {
        var i = _caret;
        while (i > 0 && char.IsWhiteSpace(_text[i - 1]) && _text[i - 1] != '\n') i--;
        if (i > 0 && !IsIdent(_text[i - 1])) return i - 1;
        return WordStart(i);
    }

    int WordRight()
    {
        var i = _caret;
        if (i < _text.Length && !IsIdent(_text[i])) i++;
        i = WordEnd(i);
        while (i < _text.Length && _text[i] == ' ') i++;
        return i;
    }

    void MoveTo(int offset, bool extend, bool keepX)
    {
        _caret = Math.Clamp(offset, 0, _text.Length);
        if (!extend) _anchor = _caret;
        if (!keepX) _desiredX = -1;
        CloseCompletion();
        EnsureVisible();
        Redraw();
        CaretMoved?.Invoke();
    }

    void MoveVertical(int lines, bool extend)
    {
        var line = LineOf(_caret);
        if (_desiredX < 0) _desiredX = XOf(_caret);
        var target = Math.Clamp(line + lines, 0, LineCount - 1);
        MoveTo(OffsetAtX(target, _desiredX), extend, keepX: true);
    }

    // ---- layout and hit testing -----------------------------------------------------------------------------

    TextLayout Layout(int line)
    {
        if (_layouts.TryGetValue(line, out var cached)) return cached;
        var start = LineStart(line);
        var text = LineText(line);
        var overrides = new List<ValueSpan<TextRunProperties>>();
        foreach (var s in _spans)
        {
            var a = Math.Max(s.Start, start);
            var b = Math.Min(s.Start + s.Length, start + text.Length);
            if (b <= a) continue;
            var brush = BrushFor(s.Kind);
            if (brush is null) continue;
            overrides.Add(new ValueSpan<TextRunProperties>(a - start, b - a, new GenericTextRunProperties(Face, FontSize, foregroundBrush: brush)));
        }
        var layout = new TextLayout(text.Length == 0 ? " " : text, Face, FontSize, Token("text/primary"), lineHeight: LineHeight, textStyleOverrides: overrides.Count == 0 ? null : overrides);
        _layouts[line] = layout;
        return layout;
    }

    IBrush? Token(string key) => ThemeService.Token(this, key);

    IBrush? BrushFor(CodeKind k) => k switch
    {
        CodeKind.Keyword => Token("lit/code-keyword"),
        CodeKind.Type => Token("lit/code-type"),
        CodeKind.Number => Token("lit/code-number"),
        CodeKind.Method => Token("status/success"),
        CodeKind.Comment => Token("text/muted"),
        _ => null,
    };

    double TextLeft => Gutter + TextPadding.Left - _scrollX;
    double LineTop(int line) => TextPadding.Top + line * LineHeight - _scrollY;

    /// <summary>X of an offset relative to the text start (no scrolling).</summary>
    double XOf(int offset)
    {
        var line = LineOf(offset);
        var col = offset - LineStart(line);
        if (col == 0) return 0;
        return Layout(line).HitTestTextPosition(col).X;
    }

    int OffsetAtX(int line, double x)
    {
        if (LineText(line).Length == 0) return LineStart(line);
        var hit = Layout(line).HitTestPoint(new Point(Math.Max(0, x), LineHeight / 2));
        var col = Math.Clamp(hit.TextPosition + (hit.IsTrailing ? 1 : 0), 0, LineText(line).Length);
        return LineStart(line) + col;
    }

    /// <summary>Text offset under a point of this control.</summary>
    public int OffsetAt(Point p)
    {
        var line = Math.Clamp((int)Math.Floor((p.Y - TextPadding.Top + _scrollY) / LineHeight), 0, LineCount - 1);
        return OffsetAtX(line, p.X - TextLeft);
    }

    /// <summary>Point (in this control) at the start of an offset's character, top of its line.</summary>
    public Point PointOf(int offset) => new(TextLeft + XOf(offset), LineTop(LineOf(offset)));

    public CodeIssue? IssueAt(int offset) => _issues.FirstOrDefault(i => offset >= i.Start && offset <= i.End && i.Severity != ScriptSeverity.Info);

    void EnsureVisible()
    {
        if (Bounds.Height <= 0) return;
        var top = TextPadding.Top + LineOf(_caret) * LineHeight;
        if (top - _scrollY < 0) _scrollY = Math.Max(0, top - TextPadding.Top);
        else if (top + LineHeight - _scrollY > Bounds.Height) _scrollY = top + LineHeight - Bounds.Height + TextPadding.Bottom;
        var x = XOf(_caret);
        var width = Bounds.Width - Gutter - 16;
        if (x - _scrollX < 0) _scrollX = Math.Max(0, x - 40);
        else if (x - _scrollX > width) _scrollX = x - width + 40;
    }

    public void ScrollToLine(int line)
    {
        _scrollY = Math.Max(0, (line - 1) * LineHeight);
        ClampScroll();
        Redraw();
    }

    void ClampScroll()
    {
        var max = Math.Max(0, TextPadding.Top + LineCount * LineHeight + TextPadding.Bottom - Bounds.Height);
        _scrollY = Math.Clamp(_scrollY, 0, max);
        _scrollX = Math.Max(0, _scrollX);
    }

    void Invalidate()
    {
        _layouts.Clear();
        Redraw();
    }

    // ---- pointer ---------------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Source is Visual v && v != this && _completionBox.IsVisible && IsInside(v, _completionBox)) return;
        Focus();
        CloseCompletion();
        var p = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;
        var offset = OffsetAt(p);
        if (ShowLineNumbers && p.X < Gutter && GutterClicked != null)
        {
            GutterClicked(LineOf(offset) + 1);
            Redraw();
            e.Handled = true;
            return;
        }
        if (e.ClickCount == 2)
        {
            _anchor = WordStart(offset);
            _caret = WordEnd(offset);
        }
        else
        {
            _caret = offset;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _anchor = offset;
            _dragging = true;
            e.Pointer.Capture(this);
        }
        _desiredX = -1;
        Redraw();
        CaretMoved?.Invoke();
        e.Handled = true;
        if (e.ClickCount == 1 && UnderlineIssues && IssueAt(offset) is { } issue && p.X >= Gutter) IssueActivated?.Invoke(issue);
    }

    static bool IsInside(Visual v, Visual container)
    {
        for (var x = v; x != null; x = Avalonia.VisualTree.VisualExtensions.GetVisualParent(x))
            if (x == container) return true;
        return false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        _caret = OffsetAt(e.GetPosition(this));
        EnsureVisible();
        Redraw();
        CaretMoved?.Invoke();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _scrollX -= e.Delta.Y * 40;
        else
        {
            _scrollY -= e.Delta.Y * LineHeight * 3;
            _scrollX -= e.Delta.X * 40;
        }
        ClampScroll();
        CloseCompletion();
        Redraw();
        e.Handled = true;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        ClampScroll();
        return size;
    }

    // ---- rendering -------------------------------------------------------------------------------------------

    /// <summary>Popups drawn over the text (autocomplete list, diagnostic popup), positioned with Canvas.Left / Top.</summary>
    public Canvas Overlay { get; } = new() { Name = "CodeOverlay" };

    sealed class TextSurface(CodeEditor owner) : Control
    {
        public override void Render(DrawingContext ctx) => owner.RenderText(ctx);
    }

    void RenderText(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var first = Math.Max(0, (int)Math.Floor((_scrollY - TextPadding.Top) / LineHeight));
        var last = Math.Min(LineCount - 1, (int)Math.Ceiling((_scrollY - TextPadding.Top + h) / LineHeight));
        var (selA, selB) = (Math.Min(_caret, _anchor), Math.Max(_caret, _anchor));
        var selection = Token("accent/selected");
        var muted = Token("text/muted");
        using (ctx.PushClip(new Rect(Gutter, 0, Math.Max(0, w - Gutter), h)))
        {
            for (var line = first; line <= last; line++)
            {
                var y = LineTop(line);
                var ls = LineStart(line);
                var le = LineEnd(line);
                if (selA != selB && selB >= ls && selA <= le)
                {
                    var a = Math.Max(selA, ls);
                    var b = Math.Min(selB, le);
                    var x1 = TextLeft + XOf(a);
                    var x2 = TextLeft + (b == le && selB > le ? XOf(le) + 6 : XOf(b));
                    ctx.FillRectangle(selection!, new Rect(x1, y, Math.Max(1, x2 - x1), LineHeight));
                }
                Layout(line).Draw(ctx, new Point(TextLeft, y));
            }
            if (UnderlineIssues)
                foreach (var issue in _issues.Where(i => i.Severity != ScriptSeverity.Info))
                {
                    var line = LineOf(issue.Start);
                    if (line < first || line > last) continue;
                    var end = Math.Min(issue.End, LineEnd(line));
                    var x1 = TextLeft + XOf(issue.Start);
                    var x2 = TextLeft + (end > issue.Start ? XOf(end) : XOf(issue.Start) + 6);
                    // Figma T-P04: the wave 4 px under the glyphs, its crests 5 px apart
                    Squiggle(ctx, x1, x2, LineTop(line) + LineHeight + 1, Token(issue.Severity == ScriptSeverity.Error ? "status/danger" : "status/warning"));
                }
            if (IsFocused && !IsReadOnly)
            {
                var p = PointOf(_caret);
                ctx.FillRectangle(Token("accent/default")!, new Rect(p.X, p.Y + 1, 1.5, LineHeight - 2));
            }
        }
        if (ShowLineNumbers)
        {
            for (var line = first; line <= last; line++)
            {
                var num = new TextLayout((line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Face, FontSize, muted, TextAlignment.Right,
                    maxWidth: GutterWidth - 16, lineHeight: LineHeight);
                num.Draw(ctx, new Point(0, LineTop(line)));
                if (HasBreakpoint?.Invoke(line + 1) == true && Token("status/danger") is { } dot)
                    ctx.DrawEllipse(dot, null, new Point(8, LineTop(line) + LineHeight / 2), 4, 4);
            }
        }
    }

    static void Squiggle(DrawingContext ctx, double x1, double x2, double y, IBrush? brush)
    {
        if (brush is null || x2 <= x1) return;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x1, y), false);
            // half waves of about 2.5 px that end exactly under the end of the word
            var n = Math.Max(1, (int)Math.Round((x2 - x1) / 2.5));
            for (var i = 1; i <= n; i++) c.LineTo(new Point(x1 + (x2 - x1) * i / n, i % 2 == 1 ? y - 2 : y));
            c.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(brush, 1), g);
    }

    // ---- autocomplete ----------------------------------------------------------------------------------------

    public bool IsCompletionOpen => _completionBox.IsVisible;
    public IReadOnlyList<CompletionItem> CompletionItems => _completionShown;
    public int CompletionIndex => _completionIndex;
    /// <summary>The running completion query (tests await it).</summary>
    public Task? CompletionTask { get; private set; }

    /// <summary>Asks the provider for items at the caret; <paramref name="wordStart"/> is where the typed prefix begins.</summary>
    public void RequestCompletion(int wordStart)
    {
        if (CompletionProvider is null || IsReadOnly) return;
        _completionCancel?.Cancel();
        var cts = _completionCancel = new CancellationTokenSource();
        var text = _text;
        var caret = _caret;
        var provider = CompletionProvider;
        _completionStart = wordStart;
        CompletionTask = Task.Run(() => provider(text, wordStart, cts.Token), cts.Token).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && !cts.IsCancellationRequested)
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (cts.IsCancellationRequested || _text != text && !_text.StartsWith(text[..Math.Min(caret, text.Length)], StringComparison.Ordinal)) return;
                    _completionAll = t.Result.ToList();
                    FilterCompletion();
                });
        }, TaskScheduler.Default);
    }

    void FilterCompletion()
    {
        if (_completionStart < 0 || _caret < _completionStart || LineOf(_caret) != LineOf(_completionStart)) { CloseCompletion(); return; }
        var prefix = _text[_completionStart.._caret];
        if (prefix.Any(c => !IsIdent(c))) { CloseCompletion(); return; }
        _completionShown.Clear();
        _completionShown.AddRange(_completionAll.Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        if (prefix.Length > 0) _completionShown.AddRange(_completionAll.Where(c => !c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && c.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase)));
        if (_completionShown.Count == 0 || _completionShown.Count == 1 && _completionShown[0].Name == prefix) { CloseCompletion(); return; }
        _completionRows.Children.Clear();
        foreach (var item in _completionShown.Take(10))
        {
            var row = new MenuRow { Label = item.Name, Glyph = GlyphOf(item.Kind), RightText = item.Detail };
            var it = item;
            row.Activated += (_, _) => { _completionIndex = _completionShown.IndexOf(it); CommitCompletion(); };
            _completionRows.Children.Add(row);
        }
        _completionIndex = 0;
        HighlightCompletion(0);
        var p = PointOf(_completionStart);
        Canvas.SetLeft(_completionBox, Math.Clamp(p.X - 8, Gutter, Math.Max(Gutter, Bounds.Width - _completionBox.Width - 4)));
        Canvas.SetTop(_completionBox, p.Y + LineHeight + 2);
        _completionBox.IsVisible = true;
    }

    static string GlyphOf(string kind) => kind switch
    {
        "method" => "code",
        "property" or "field" => "sliders",
        "namedtype" => "cube",
        "local" or "parameter" => "dots",
        _ => "dots",
    };

    void HighlightCompletion(int i)
    {
        var count = Math.Min(10, _completionShown.Count);
        if (count == 0) return;
        _completionIndex = (i % count + count) % count;
        for (var k = 0; k < _completionRows.Children.Count; k++) ((MenuRow)_completionRows.Children[k]).IsSelected = k == _completionIndex;
    }

    /// <summary>Inserts the highlighted autocomplete item in place of the typed prefix.</summary>
    public void CommitCompletion()
    {
        if (!_completionBox.IsVisible || _completionIndex >= _completionShown.Count) return;
        var name = _completionShown[_completionIndex].Name;
        var start = _completionStart;
        CloseCompletion();
        Edit(start, WordEnd(_caret) - start, name, "Autocomplete", coalesce: false);
    }

    public void CloseCompletion()
    {
        _completionCancel?.Cancel();
        _completionBox.IsVisible = false;
        _completionStart = -1;
    }

    // ---- Edit menu (IEditTarget) -----------------------------------------------------------------------------

    public bool CanCopy => true;
    public bool CanPaste => !IsReadOnly;
    IClipboard? Clipboard => TopLevel.GetTopLevel(this)?.Clipboard;

    public void Copy()
    {
        var text = HasSelection ? SelectedText : LineText(LineOf(_caret)) + "\n";
        _clipboardFallback = text;
        _ = Clipboard?.SetTextAsync(text);
    }

    public void Cut()
    {
        if (IsReadOnly) return;
        Copy();
        if (HasSelection) DeleteSelection();
        else
        {
            var line = LineOf(_caret);
            var start = LineStart(line);
            var end = Math.Min(_text.Length, LineEnd(line) + 1);
            Edit(start, end - start, "", "Cut", coalesce: false);
        }
    }

    public async void Paste()
    {
        if (IsReadOnly) return;
        string? text = null;
        try { if (Clipboard is { } cb) text = await cb.TryGetTextAsync(); }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or TimeoutException) { }
        text ??= _clipboardFallback;
        if (string.IsNullOrEmpty(text)) return;
        var (a, b) = (Math.Min(_caret, _anchor), Math.Max(_caret, _anchor));
        Edit(a, b - a, text.Replace("\r\n", "\n").Replace("\t", new string(' ', TabSize)), "Paste", coalesce: false);
    }

    /// <summary>Duplicates the selection, or the caret line below itself.</summary>
    public void Duplicate()
    {
        if (IsReadOnly) return;
        if (HasSelection)
        {
            var (a, b) = (Math.Min(_caret, _anchor), Math.Max(_caret, _anchor));
            var s = _text[a..b];
            Edit(b, 0, s, "Duplicate", coalesce: false);
            Select(b, b + s.Length);
            return;
        }
        var line = LineOf(_caret);
        var col = _caret - LineStart(line);
        Edit(LineEnd(line), 0, "\n" + LineText(line), "Duplicate", coalesce: false);
        MoveCaret(line + 2, col + 1);
    }

    public void Delete()
    {
        if (IsReadOnly) return;
        if (HasSelection) DeleteSelection();
        else DeleteForward(false);
    }

    public void SelectAll() => Select(0, _text.Length);
}
