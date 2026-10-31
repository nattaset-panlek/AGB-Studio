using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Scripting;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Scripts;

/// <summary>Script Editor (T-P04): P11.52 code editor, P11.53 side panel, P11.54 bottom panel and status line.</summary>
public sealed class ScriptEditorTests : IDisposable
{
    readonly EditorHarness _h = new();

    public void Dispose() => _h.Dispose();

    ScriptEditorPage OpenPlayer()
    {
        _h.Open("T-P04", SkyHopperProject.Script(_h.Fx, "PlayerController"));
        var page = _h.Page<ScriptEditorPage>();
        page.AnalyzeNow();
        Capture.Pump();
        return page;
    }

    [AvaloniaFact]
    public void OpensFromTheDocTabAndListsTheScripts()
    {
        _h.S.Documents.Add(new AgbStudio.Editor.Services.EditorDocument { Name = "PlayerController.cs", Glyph = "code", Path = SkyHopperProject.Script(_h.Fx, "PlayerController") });
        Capture.Pump();
        _h.Click(_h.Find<DocTab>("Doc_PlayerController.cs"));
        var page = _h.Page<ScriptEditorPage>();
        Assert.Equal("PlayerController.cs", page.Buffer!.Name);
        // the Scripts list: open scripts first, then all 12 scripts of the fixture
        var list = _h.Find<StackPanel>("ScriptList").Children.OfType<TreeItem>().Select(t => t.Label).ToList();
        Assert.Equal(12, list.Count);
        Assert.Equal("PlayerController.cs", list[0]);
        Assert.Contains("CameraFollow.cs", list);
        _h.Click(_h.Find<TreeItem>("Script_Slime"));
        Assert.Equal("Slime.cs", _h.Page<ScriptEditorPage>().Buffer!.Name);
        Assert.Contains("public fixed speed = 0.5;", page.Editor.Text);
        Assert.Equal(Commands.ShortcutContext.Script, _h.Win.ActiveContext);
    }

    [AvaloniaFact]
    public void LineNumbersHighlightingAndBreadcrumb()
    {
        var page = OpenPlayer();
        var ed = page.Editor;
        Assert.Equal(30, ed.LineCount);
        // keywords, types, numbers and methods are coloured; `fixed` is a keyword (D-22)
        string Word(CodeSpan s) => ed.Text.Substring(s.Start, s.Length);
        Assert.Contains(ed.Spans, s => s.Kind == CodeKind.Keyword && Word(s) == "public");
        Assert.Contains(ed.Spans, s => s.Kind == CodeKind.Keyword && Word(s) == "fixed");
        Assert.Contains(ed.Spans, s => s.Kind == CodeKind.Type && Word(s) == "Behaviour");
        Assert.Contains(ed.Spans, s => s.Kind == CodeKind.Number && Word(s) == "1.5");
        Assert.Contains(ed.Spans, s => s.Kind == CodeKind.Method && Word(s) == "Pressed");
        // clicking a line moves the caret; the breadcrumb shows the member at the caret, the status line Ln / Col
        ed.MoveCaret(16, 9);
        Capture.Pump();
        Assert.Equal(["scripts", "PlayerController.cs", "Update()"], page.BreadcrumbParts);
        Assert.Equal("Ln 16, Col 9", _h.Find<StatusItem>("Status_Caret").Content);
        var p = ed.PointOf(ed.OffsetOf(8, 5));
        _h.ClickAt(_h.At(ed, new Avalonia.Point(p.X + 1, p.Y + 10)));
        Assert.Equal((8, 5), (ed.CaretLine, ed.CaretColumn));
        Assert.Equal("Ln 8, Col 5", _h.Find<StatusItem>("Status_Caret").Content);
        Assert.Equal("coyoteTime", page.BreadcrumbParts[^1]);
    }

    [AvaloniaFact]
    public void TypingAutoIndentTabSizeAndUndo()
    {
        var page = OpenPlayer();
        var ed = page.Editor;
        ed.Focus();
        ed.MoveCaret(17, 1);
        _h.Key(Key.Tab);
        _h.Key(Key.Tab);
        _h.Type("Debug.Log(\"hi\");");
        Assert.Equal("        Debug.Log(\"hi\");", ed.LineText(16));
        _h.Key(Key.Enter);
        Assert.Equal("        ", ed.LineText(17));
        Assert.True(page.Buffer!.Document.IsDirty);
        // Ctrl+Z (window shortcut) undoes the typing through the editor's undo history
        _h.Key(Key.Z, Avalonia.Input.RawInputModifiers.Control);
        _h.Key(Key.Z, Avalonia.Input.RawInputModifiers.Control);
        Assert.DoesNotContain("Debug.Log", ed.Text);
        // tab size 2 (Preferences · Editor)
        ScriptEditorSettings.Of(_h.S).TabSize = 2;
        _h.Open("T-P04", SkyHopperProject.Script(_h.Fx, "Coin"));
        var coin = _h.Page<ScriptEditorPage>();
        coin.Editor.Focus();
        coin.Editor.MoveCaret(1, 1);
        _h.Key(Key.Tab);
        Assert.StartsWith("  using", coin.Editor.LineText(0));
    }

    [AvaloniaFact]
    public async Task AutocompleteListsApiMembersAndInsertsTheChoice()
    {
        var page = OpenPlayer();
        var ed = page.Editor;
        ed.Focus();
        ed.MoveCaret(17, 1);
        _h.Type("        Input.");
        Assert.NotNull(ed.CompletionTask);
        Phase11ScriptShots.Wait(ed.CompletionTask!);
        Capture.Pump();
        Assert.True(ed.IsCompletionOpen);
        var names = ed.CompletionItems.Select(c => c.Name).ToList();
        foreach (var m in new[] { "Pressed", "Held", "Released", "AxisX", "AxisY", "AnyKey" }) Assert.Contains(m, names);
        _h.Type("Hel");
        Assert.Equal("Held", ed.CompletionItems[0].Name);
        _h.Key(Key.Enter);
        Assert.Contains("        Input.Held", ed.Text);
        Assert.False(ed.IsCompletionOpen);
        // Ctrl+Space after a word start lists locals and API types
        var items = page.Workspace.Analysis.Complete(page.Buffer!.Path, ed.Text, ed.Text.IndexOf("body.vx", StringComparison.Ordinal));
        Assert.Contains(items, i => i.Name == "body");
        Assert.Contains(items, i => i.Name == "Sound");
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task AnalyzerUnderlineAndDiagnosticPopupQuickFix()
    {
        var page = OpenPlayer();
        var ed = page.Editor;
        var issue = Assert.Single(ed.Issues, i => i.Id == "GAE002");
        Assert.Equal((8, 5), (issue.Line, issue.Column));
        Assert.Equal("GAE002 · float is very slow on GBA (no floating-point hardware)", issue.Title);
        // a click on the underline opens the popup with the rule's text and quick fixes
        var p = ed.PointOf(issue.Start);
        _h.ClickAt(_h.At(ed, new Avalonia.Point(p.X + 8, p.Y + 10)));
        var popup = page.Popup ?? throw new InvalidOperationException("no popup");
        var until = DateTime.UtcNow.AddSeconds(30);
        while (popup.Fixes.Count == 0 && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        Assert.Equal(["Change to fixed", "Ignore"], popup.Fixes);
        Assert.Contains(popup.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("24.8 fixed-point type") == true);
        // Change to fixed: one undoable edit, the warning disappears
        var fix = popup.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Change to fixed");
        _h.Click(fix);
        var wait = DateTime.UtcNow.AddSeconds(30);
        while (!ed.Text.Contains("fixed coyoteTime = 0.1;", StringComparison.Ordinal) && DateTime.UtcNow < wait) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        Assert.Contains("    fixed coyoteTime = 0.1;\n", ed.Text);
        Assert.DoesNotContain(ed.Issues, i => i.Id == "GAE002");
        Assert.Null(page.Popup);
        _h.S.Undo.Undo();
        Assert.Contains("float coyoteTime = 0.1f;", ed.Text);
        // Ignore wraps the line in #pragma
        page.AnalyzeNow();
        Assert.True(await page.ApplyFixAsync(ed.Issues.First(i => i.Id == "GAE002"), "Ignore"));
        Assert.Contains("#pragma warning disable GAE002", ed.Text);
    }

    [AvaloniaFact]
    public async Task FormatOnSaveWritesTheFileAndRunsTheScriptCheck()
    {
        var page = OpenPlayer();
        var ed = page.Editor;
        ed.Replace(ed.Text.IndexOf("public int lives = 3;", StringComparison.Ordinal), "public int lives = 3;".Length, "public   int lives=3;");
        Assert.True(page.Buffer!.Document.IsDirty);
        await _h.S.Documents.SaveAsync(page.Buffer.Document);
        var saved = File.ReadAllText(page.Buffer.Path);
        Assert.Contains("    public int lives = 3;\n", saved);
        Assert.Contains("public int lives = 3;", ed.Text);
        Assert.Contains("[Range(0.5, 3)] public fixed speed = 1.5;", saved);
        Assert.False(page.Buffer.Document.IsDirty);
        Assert.Contains(page.Workspace.Output, l => l.Text.StartsWith("C# → C++  12 scripts translated · ", StringComparison.Ordinal));
        // format on save off: the text is saved as typed
        ScriptEditorSettings.Of(_h.S).FormatOnSave = false;
        ed.Replace(ed.Text.IndexOf("public int lives = 3;", StringComparison.Ordinal), 0, " ");
        await _h.S.Documents.SaveAsync(page.Buffer.Document);
        Assert.Contains("     public int lives = 3;", File.ReadAllText(page.Buffer.Path));
    }

    [AvaloniaFact]
    public void SidePanelAttachedToCompilesToExposedFieldsOutlineAndApi()
    {
        var page = OpenPlayer();
        Assert.Equal("Player · Forest_01", _h.Find<InspectorField>("AttachedTo").Value);
        Assert.Equal("PlayerController.cpp", _h.Find<InspectorField>("CompilesTo").Value);
        var speed = _h.Find<InspectorField>("Field_speed");
        Assert.Equal(FieldKind.Slider, speed.Kind);
        Assert.Equal("1.5", speed.Value);
        Assert.Equal("3.5", _h.Find<InspectorField>("Field_jumpPower").Value);
        Assert.Equal("3", _h.Find<InspectorField>("Field_lives").Value);
        // changing an exposed field writes the default value into the script (undoable)
        Assert.True(page.SetFieldValue("jumpPower", "4.25"));
        Assert.Contains("public fixed jumpPower = 4.25;", page.Editor.Text);
        Assert.False(page.SetFieldValue("lives", "three"));
        _h.S.Undo.Undo();
        Assert.Contains("public fixed jumpPower = 3.5;", page.Editor.Text);
        // outline: class, fields with types (coyoteTime flagged), methods; a click moves the caret
        var outline = _h.Find<StackPanel>("Outline").Children.OfType<TreeItem>().Select(t => (t.Label, t.Meta)).ToList();
        Assert.Equal(("PlayerController", "class"), outline[0]);
        Assert.Contains(("speed", "fixed"), outline);
        Assert.Contains(("coyoteTime", "float"), outline);
        Assert.Contains(outline, o => o.Label == "OnTouch(Actor)");
        Assert.Equal("warn", _h.Find<TreeItem>("Outline_coyoteTime").Glyph);
        _h.Click(_h.Find<TreeItem>("Outline_Update"));
        Assert.Equal(14, page.Editor.CaretLine);
        // API quick reference with real signatures; Open Scripting API
        Assert.NotNull(_h.TryFind<DockPanel>("Api_Input.Pressed"));
        Assert.Contains(_h.Find<DockPanel>("Api_Input.Pressed").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Input.Pressed(Key k)");
        Assert.Contains(_h.Find<DockPanel>("Api_Items.Get").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Item");
        _h.Click(_h.Find<AgbButton>("OpenScriptingApi"));
        Assert.Equal("T-P18", _h.S.Windows.Requests[^1].Window);
        Assert.IsType<ScriptingApiPage>(_h.Win.CurrentPage);
    }

    [AvaloniaFact]
    public async Task BottomPanelProblemsTerminalOutputAndStatusLine()
    {
        var page = OpenPlayer();
        var problems = _h.Find<StackPanel>("Problems").Children.OfType<ConsoleLine>().ToList();
        var line = Assert.Single(problems);
        Assert.Equal("GAE002  PlayerController.cs(8,5): float is very slow on GBA (no floating-point hardware) — use fixed", line.Text);
        Assert.Equal(ConsoleLevel.Warning, line.Level);
        Assert.Equal(1, page.BottomTabs[0].Count);
        Assert.Equal("1 warning", _h.Find<StatusItem>("Status_Problems").Content);
        Assert.Equal("C# · GBA subset", _h.Find<StatusItem>("Status_Language").Content);
        // clicking a problem goes to its line
        page.Editor.MoveCaret(1, 1);
        _h.Click(line);
        Assert.Equal((8, 5), (page.Editor.CaretLine, page.Editor.CaretColumn));
        // Terminal runs a command in the project folder
        _h.Click(page.BottomTabs[1]);
        await page.RunTerminal("echo agb-terminal-ok");
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!page.TerminalText.Any(t => t.Contains("exit code")) && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        Assert.Contains("agb-terminal-ok", page.TerminalText);
        // Output keeps the log of checks
        _h.S.Toolchain.CheckNow();
        await page.Workspace.CheckAsync();
        _h.Click(page.BottomTabs[2]);
        Assert.Contains(_h.Find<StackPanel>("Output").Children.OfType<ConsoleLine>(), c => c.Text!.StartsWith("C# → C++  12 scripts translated", StringComparison.Ordinal));
        Capture.Pump();
        Assert.StartsWith("devkitARM", (string?)_h.Find<StatusItem>("Status_Toolchain").Content);
    }

    [AvaloniaFact]
    public void GraphGeneratedScriptsAreReadOnly()
    {
        var path = Phase11ScriptShots.FigmaGraph(_h.Fx);
        var doc = AgbStudio.Editor.Windows.LogicGraph.GraphDocument.Load(path);
        doc.Save();
        _h.Open("T-P04", Path.ChangeExtension(path, ".cs"));
        var page = _h.Page<ScriptEditorPage>();
        Assert.True(page.Editor.IsReadOnly);
        page.Editor.TypeText("x");
        Assert.StartsWith("// Generated by AGB Studio Logic Graph", page.Editor.Text);
    }
}
