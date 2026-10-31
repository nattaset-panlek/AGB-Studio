using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Scripts;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Scripts;

/// <summary>P11.55 Find &amp; Replace, New Script, Open externally; P14.03-P14.05 external editor and hot reload.</summary>
public sealed class ScriptToolsTests : IDisposable
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
    public void CtrlF_OpensFindWithTheWordAtTheCaret_NextAndReplace()
    {
        var page = OpenPlayer();
        page.Editor.Focus();
        page.Editor.MoveCaret(6, 20);
        _h.Key(Key.F, RawInputModifiers.Control);
        Assert.Equal("T-O11", _h.S.Windows.Requests[^1].Window);
        var find = Assert.IsType<FindReplacePopover>(_h.Win.Overlays.Open.Single());
        Assert.Equal("jumpPower", find.Query.Text);
        Assert.Equal(2, find.Hits.Count);
        Assert.Equal("1 of 2", find.CountText);
        find.Step(1);
        Assert.Equal("2 of 2", find.CountText);
        Assert.Equal("jumpPower", page.Editor.SelectedText);
        Assert.Equal(20, page.Editor.CaretLine);
        // Enter in the query goes to the next match (wraps)
        find.Query.Focus();
        _h.Key(Key.Enter);
        Assert.Equal("1 of 2", find.CountText);
        // Replace one, then all (one undo step each)
        find.Replacement.Text = "jumpForce";
        _h.Click(find.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "ReplaceOne"));
        Assert.Contains("public fixed jumpForce = 3.5;", page.Editor.Text);
        Assert.Single(find.Hits);
        _h.Click(find.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "ReplaceAll"));
        Assert.DoesNotContain("jumpPower", page.Editor.Text);
        Assert.Empty(find.Hits);
        _h.S.Undo.Undo();
        Assert.Contains("body.vy = -jumpPower;", page.Editor.Text);
        _h.Key(Key.Escape);
        Assert.False(_h.Win.Overlays.IsOpen);
    }

    [AvaloniaFact]
    public void FindOptions_CaseWholeWordRegexAndScopes()
    {
        var page = OpenPlayer();
        var find = new FindReplacePopover(new FindContext(page, "speed", false), _h.S);
        Assert.Equal(2, find.Hits.Count);                    // the field and its use in Update
        find.Query.Text = "Speed";
        find.SetOptions(matchCase: true, wholeWord: false, regex: false);
        Assert.Empty(find.Hits);
        find.Query.Text = "body";
        find.SetOptions(matchCase: false, wholeWord: true, regex: false);
        Assert.Equal(5, find.Hits.Count);                    // not inside PlatformerBody
        find.Query.Text = @"jump\w+";
        find.SetOptions(matchCase: false, wholeWord: false, regex: true);
        Assert.All(find.Hits, h => Assert.Equal("jumpPower", page.Editor.Text.Substring(h.Start, h.Length)));
        find.Query.Text = "(";
        Assert.Equal("Invalid", find.CountText);
        // All scripts: every .cs of the project; Whole project: scenes and data too
        find.SetOptions(false, true, false);
        find.Query.Text = "Coin";
        find.SetScope(FindScope.AllScripts);
        Assert.Contains(find.Hits, h => h.Path.EndsWith("Coin.cs", StringComparison.Ordinal));
        Assert.All(find.Hits, h => Assert.EndsWith(".cs", h.Path));
        find.SetScope(FindScope.WholeProject);
        Assert.Contains(find.Hits, h => h.Path.EndsWith("Forest_01.scene", StringComparison.Ordinal));
        // replace all across scripts: open buffers are edited (unsaved), other files written
        find.SetScope(FindScope.AllScripts);
        find.Query.Text = "lastScore";
        find.Replacement.Text = "previousScore";
        Assert.Equal(3, find.ReplaceEverything());   // HUD.cs declares, compares and sets it
        Assert.Contains("previousScore", File.ReadAllText(SkyHopperProject.Script(_h.Fx, "HUD")));
        Assert.DoesNotContain("lastScore", File.ReadAllText(SkyHopperProject.Script(_h.Fx, "HUD")));
    }

    [AvaloniaFact]
    public async Task NewScript_CreatesFromTemplateAttachesAndOpens()
    {
        OpenPlayer();
        _h.Click(_h.Find<PanelHeader>("ScriptsHeader").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action"));
        var dlg = Assert.IsType<NewScriptDialog>(_h.Win.Overlays.Open.Single());
        Assert.Equal("T-D14", _h.S.Windows.Requests[^1].Window);
        Assert.Contains("Bat · Cave_01", dlg.AttachTo.Options!);
        dlg.AttachTo.Selected = "Bat · Cave_01";
        dlg.ClassName.Text = "PlayerController";
        Assert.Equal("PlayerController.cs already exists.", dlg.ClassName.Help);
        dlg.ClassName.Text = "2fast";
        Assert.StartsWith("Use letters", dlg.ClassName.Help);
        dlg.ClassName.Text = "BatController";
        Assert.Equal("File: scripts/BatController.cs", dlg.ClassName.Help);
        _h.Click(_h.Find<Border>("Template_Behaviour"));
        var path = await dlg.CreateAsync();
        Capture.Pump();
        Assert.NotNull(path);
        Assert.Contains("public class BatController : Behaviour", File.ReadAllText(path!));
        Assert.Contains("namespace SkyHopper;", File.ReadAllText(path!));
        var cave = GameProject.Load<SceneFile>(Path.Combine(_h.S.Project.Current!.ScenesDir, "Cave_01.scene"));
        Assert.Contains(cave.Actors.First(a => a.Name == "Bat").Components, c => c.Type == "BatController");
        var page = _h.Page<ScriptEditorPage>();
        Assert.Equal("BatController.cs", page.Buffer!.Name);
        // the other templates compile clean (P6.08)
        foreach (var kind in new[] { AgbStudio.Scripting.ScriptTemplates.Kind.StaticHelper, AgbStudio.Scripting.ScriptTemplates.Kind.SceneScript })
        {
            var p = page.Workspace.CreateScript("Made" + kind, kind, null);
            var r = page.Workspace.Analysis.Analyze(p, File.ReadAllText(p));
            Assert.DoesNotContain(r.Issues, i => i.Severity == AgbStudio.Scripting.ScriptSeverity.Error);
        }
    }

    [AvaloniaFact]
    public async Task NewScript_OpenInExternalEditorAfterCreating()
    {
        var code = FakeEditor(ExternalEditorKind.VsCode);
        var dlg = new NewScriptDialog(_h.S, "Bat · Cave_01") { };
        dlg.ClassName.Text = "BatAi";
        dlg.OpenExternally.IsChecked = true;
        var path = await dlg.CreateAsync();
        var launch = Assert.Single(ExternalEditorService.Of(_h.S).Launches);
        Assert.Equal(code, launch.Exe);
        Assert.Contains($"-g \"{path}:5:1\"", launch.Arguments);   // the class line of the template
        // the IntelliSense project was generated first (P14.02)
        Assert.True(File.Exists(AgbStudio.Scripting.CsprojGenerator.ProjectFile(_h.Root, "Sky Hopper")));
    }

    string FakeEditor(ExternalEditorKind kind)
    {
        var exe = Path.Combine(_h.Fx.Folder, kind + ".exe");
        File.WriteAllText(exe, "");
        var settings = ScriptEditorSettings.Of(_h.S);
        settings.Editor = kind;
        settings.ExecutablePath = exe;
        ExternalEditorService.Of(_h.S).Launch = (_, _) => { };
        return exe;
    }

    [AvaloniaFact]
    public void OpenExternally_StartsTheConfiguredEditorAtTheCaretLine()
    {
        var page = OpenPlayer();
        page.Editor.MoveCaret(8, 5);
        var svc = ExternalEditorService.Of(_h.S);
        foreach (var (kind, expected) in new[]
                 {
                     (ExternalEditorKind.VsCode, $"\"{_h.Root}\" -g \"{page.Buffer!.Path}:8:5\""),
                     (ExternalEditorKind.Rider, $"--line 8 --column 4 \"{page.Buffer.Path}\""),
                     (ExternalEditorKind.VisualStudio, $"/Edit \"{page.Buffer.Path}\" /Command \"Edit.GoTo 8\""),
                 })
        {
            var exe = FakeEditor(kind);
            _h.Click(_h.Find<AgbButton>("OpenExternally"));
            Assert.Equal((exe, expected), svc.Launches[^1]);
        }
        // Custom… with an argument template
        var custom = FakeEditor(ExternalEditorKind.Custom);
        ScriptEditorSettings.Of(_h.S).CustomArguments = "+{line} \"{file}\"";
        Assert.True(page.OpenExternally());
        Assert.Equal((custom, $"+8 \"{page.Buffer.Path}\""), svc.Launches[^1]);
        // "Open .cs with": External editor opens other places' scripts externally at the line; Built-in opens the Script Editor
        var vs = FakeEditor(ExternalEditorKind.VisualStudio);
        Phase11ScriptShots.Wait(ScriptEditorPage.OpenScriptFile(_h.S, SkyHopperProject.Script(_h.Fx, "Coin"), 4));
        Assert.Equal((vs, $"/Edit \"{SkyHopperProject.Script(_h.Fx, "Coin")}\" /Command \"Edit.GoTo 4\""), svc.Launches[^1]);
        ScriptEditorSettings.Of(_h.S).OpenCsExternally = false;
        Phase11ScriptShots.Wait(ScriptEditorPage.OpenScriptFile(_h.S, SkyHopperProject.Script(_h.Fx, "Coin"), 4));
        Assert.Equal(("Coin.cs", 4), (_h.Page<ScriptEditorPage>().Buffer!.Name, _h.Page<ScriptEditorPage>().Editor.CaretLine));
        page = _h.Page<ScriptEditorPage>();
        // no executable (the path set does not exist and the editor is not installed): a toast, nothing started
        ScriptEditorSettings.Of(_h.S).ExecutablePath = Path.Combine(_h.Fx.Folder, "missing.exe");
        svc.Detector = new ExternalEditors(env: _ => null, fileExists: _ => false, directories: _ => [], appPath: _ => null, vswhere: () => []);
        var count = svc.Launches.Count;
        Assert.False(page.OpenExternally());
        Assert.Equal(count, svc.Launches.Count);
        Assert.Contains(_h.S.Toasts.Visible, t => t.Status == ToastStatus.Error);
    }

    [AvaloniaFact]
    public async Task HotReload_ExternalSaveReloadsTheEditorAndChecksTheScripts()
    {
        var page = OpenPlayer();
        var path = page.Buffer!.Path;
        var text = File.ReadAllText(path).Replace("public int lives = 3;", "public int lives = 5;");
        File.WriteAllText(path, text);
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!page.Editor.Text.Contains("lives = 5", StringComparison.Ordinal) && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        Assert.Contains("public int lives = 5;", page.Editor.Text);
        Assert.False(page.Buffer.Document.IsDirty);
        while (!page.Workspace.Output.Any(l => l.Text.StartsWith("Hot reload: PlayerController", StringComparison.Ordinal)) && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        Assert.Contains(page.Workspace.Output, l => l.Text == "Hot reload: PlayerController updated");
        Assert.Contains(page.Workspace.Output, l => l.Text.StartsWith("C# → C++  12 scripts translated", StringComparison.Ordinal));
        // unsaved edits are kept when the file changes on disk
        page.Editor.Replace(0, 0, "// mine\n");
        File.WriteAllText(path, text.Replace("lives = 5", "lives = 6"));
        while (!page.Workspace.Output.Any(l => l.Text.Contains("changed on disk", StringComparison.Ordinal)) && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        Assert.StartsWith("// mine", page.Editor.Text);
        // a save from the editor itself is not an external change
        await _h.S.Documents.SaveAsync(page.Buffer.Document);
        var before = page.Workspace.Output.Count;
        page.Workspace.OnExternalSave(path);
        Assert.Equal(before, page.Workspace.Output.Count);
    }
}

/// <summary>P14.01 external editor detection and settings (UT).</summary>
public sealed class ExternalEditorDetectionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    [Fact]
    public void DetectsVsCodeInLocalAppDataPrograms()
    {
        var local = Directory.CreateTempSubdirectory("agb-local-").FullName;
        try
        {
            var code = Path.Combine(local, "Programs", "Microsoft VS Code", "Code.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(code)!);
            File.WriteAllText(code, "");
            var d = new ExternalEditors(env: n => n == "LOCALAPPDATA" ? local : null, appPath: _ => null, vswhere: () => []);
            var found = d.Detect(ExternalEditorKind.VsCode);
            Assert.True(found.Found);
            Assert.Equal(code, found.Path);
            Assert.Equal("Visual Studio Code found", found.Status);
            Assert.False(d.Detect(ExternalEditorKind.Rider).Found);
            Assert.Equal("JetBrains Rider not found", d.Detect(ExternalEditorKind.Rider).Status);
        }
        finally { Directory.Delete(local, true); }
    }

    [Fact]
    public void DetectsRiderVisualStudioFromAppPathsVsWhereAndProgramFiles()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\JB\rider64.exe", @"D:\VS\18\Community\Common7\IDE\devenv.exe", @"C:\PF\Microsoft VS Code\Code.exe",
        };
        var d = new ExternalEditors(env: n => n == "ProgramFiles" ? @"C:\PF" : null, fileExists: files.Contains,
            appPath: exe => exe == "rider64.exe" ? @"C:\JB\rider64.exe" : null, vswhere: () => [@"D:\VS\18\Community\Common7\IDE\devenv.exe"]);
        Assert.Equal(@"C:\JB\rider64.exe", d.Detect(ExternalEditorKind.Rider).Path);
        Assert.Equal(@"D:\VS\18\Community\Common7\IDE\devenv.exe", d.Detect(ExternalEditorKind.VisualStudio).Path);
        Assert.Equal(@"C:\PF\Microsoft VS Code\Code.exe", d.Detect(ExternalEditorKind.VsCode).Path);
        Assert.Equal(["Visual Studio Code", "JetBrains Rider", "Visual Studio", "Custom…"], ExternalEditors.Kinds.Select(k => k.Name));
    }

    [Fact]
    public void ThisMachine_RecordsWhichIdesAreInstalled()
    {
        // P14.07 record: which of the three IDEs exist here (VS Code / Rider / Visual Studio)
        var all = new ExternalEditors().DetectAll();
        Assert.Equal(3, all.Count);
        Assert.Equal(["Visual Studio Code", "JetBrains Rider", "Visual Studio"], all.Select(e => e.Name));
        foreach (var e in all) log.WriteLine(e.Found ? $"{e.Name}: {e.Path}" : $"{e.Name}: n/a (not installed)");
        Assert.All(all.Where(e => e.Found), e => Assert.True(File.Exists(e.Path)));
    }

    [Fact]
    public void SettingsPersistWithFigmaDefaults()
    {
        var dir = Directory.CreateTempSubdirectory("agb-se-").FullName;
        try
        {
            // the settings are the Preferences fields of T-S07 / T-S08 (preferences.json), not a file of their own
            var path = Path.Combine(dir, "preferences.json");
            var s = ScriptEditorSettings.Load(path);
            Assert.True(s.UseExternal);
            Assert.Equal(ExternalEditorKind.VsCode, s.Editor);
            Assert.Equal(4, s.TabSize);
            Assert.True(s.FormatOnSave && s.AutocompleteAsYouType && s.UnderlineAnalyzerWarnings && s.GenerateCsproj && s.HotReloadOnExternalSave);
            s.Editor = ExternalEditorKind.Rider;
            s.TabSize = 2;
            s.Save();
            var back = ScriptEditorSettings.Load(path);
            Assert.Equal(ExternalEditorKind.Rider, back.Editor);
            Assert.Equal(2, back.TabSize);
            var prefs = new AgbStudio.Editor.Services.PreferencesStore(path).Current;
            Assert.Equal(("JetBrains Rider", 2), (prefs.ExternalEditor, prefs.TabSize));
            Assert.Equal(["preferences.json"], Directory.EnumerateFiles(dir).Select(Path.GetFileName));
        }
        finally { Directory.Delete(dir, true); }
    }
}
