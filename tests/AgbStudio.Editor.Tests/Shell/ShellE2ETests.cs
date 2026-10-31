using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Shell;

/// <summary>Headless E2E of the shell: real windows, real key presses and clicks (P10.01, P10.06-P10.08, P10.11-P10.13, P10.18, P10.20).</summary>
[Collection(EmulatorCollection.Name)]
public class ShellE2ETests : IDisposable
{
    readonly Fixture _fx = new();
    readonly MainWindow _win;

    public ShellE2ETests()
    {
        _fx.OpenSkyHopper();
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        _win.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        _fx.Services.Theme?.SetAccent(ThemeService.DefaultAccent);
        _fx.Dispose();
    }

    T Find<T>(string name) where T : Control => _win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    void Click(Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), _win)!.Value;
        _win.MouseMove(p);
        _win.MouseDown(p, MouseButton.Left);
        _win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    void Key(Key k, RawInputModifiers m = RawInputModifiers.None) { _win.KeyPressQwerty(ToPhysical(k), m); _win.KeyReleaseQwerty(ToPhysical(k), m); Capture.Pump(); }

    static PhysicalKey ToPhysical(Key k) => k switch
    {
        Avalonia.Input.Key.K => PhysicalKey.K, Avalonia.Input.Key.Down => PhysicalKey.ArrowDown, Avalonia.Input.Key.Return => PhysicalKey.Enter,
        Avalonia.Input.Key.Escape => PhysicalKey.Escape, Avalonia.Input.Key.Z => PhysicalKey.Z, Avalonia.Input.Key.V => PhysicalKey.V,
        Avalonia.Input.Key.M => PhysicalKey.M, Avalonia.Input.Key.OemQuestion => PhysicalKey.Slash, Avalonia.Input.Key.F5 => PhysicalKey.F5,
        Avalonia.Input.Key.A => PhysicalKey.A, Avalonia.Input.Key.S => PhysicalKey.S,
        _ => throw new ArgumentOutOfRangeException(nameof(k)),
    };

    [AvaloniaFact]
    public void TitleBar_HasSevenMenusAndNoBrandText()
    {
        Assert.Equal(["File", "Edit", "Assets", "Scene", "Build", "Tools", "Help"], _win.MenuBar.Items.OfType<MenuItem>().Select(m => (string)m.Header!));
        var texts = _win.TitleBar.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.DoesNotContain("AGB Studio", texts);
        Assert.Contains("Sky Hopper", texts);
        Assert.Contains("Forest_01.scene", texts);
        Assert.Equal("AGB Studio", _win.Title);
    }

    [AvaloniaFact]
    public void Menu_ClickFileOpensItAndHelpKeyboardShortcutsOpensTheDialog()
    {
        var file = _win.MenuBar.Items.OfType<MenuItem>().First();
        Click(file);
        Assert.True(file.IsSubMenuOpen);
        Assert.Equal(14, file.Items.Count);
        Key(Avalonia.Input.Key.Escape);
        _fx.Services.Commands.Execute("help.keyboardShortcuts");
        Capture.Pump();
        var dlg = Assert.IsType<KeyboardShortcutsDialog>(_win.Overlays.Open.Single());
        Assert.Equal(KeyboardShortcutsDialog.Rows.Select(r => r.Label), dlg.VisibleLabels);
        dlg.Search.Text = "grid";
        Capture.Pump();
        Assert.Equal(["Toggle grid"], dlg.VisibleLabels);
        Key(Avalonia.Input.Key.Escape);
        Assert.False(_win.Overlays.IsOpen);
    }

    [AvaloniaFact]
    public void CommandPalette_CtrlK_TypeArrowEnter()
    {
        Key(Avalonia.Input.Key.K, RawInputModifiers.Control);
        var palette = Assert.IsType<CommandPalette>(_win.Overlays.Open.Single());
        Assert.Equal(640, palette.Bounds.Width);
        palette.Query.Focus();
        _win.KeyTextInput("pla");
        Capture.Pump();
        Assert.Equal(["Play", "Play from Here", "Palette Manager", "player.png", "PlayerController.cs", "Platform_tiles.png"], palette.Items.Select(i => i.Label));
        Key(Avalonia.Input.Key.Down);
        Assert.Equal(1, palette.HighlightIndex);
        Key(Avalonia.Input.Key.Return);
        Assert.Equal("T-P02", _fx.Services.Windows.Requests[^1].Window);
        Assert.Equal("build.playFromHere", _fx.Services.Windows.Requests[^1].Source);
        // the palette closed and Play Mode (T-P02, Phase 12) replaced the workspace
        Assert.False(_win.Overlays.IsOpen);
        Assert.Equal("T-P02", _win.CurrentPageId);
        // ">" lists commands only
        Key(Avalonia.Input.Key.K, RawInputModifiers.Control);
        palette = Assert.IsType<CommandPalette>(_win.Overlays.Open.Single());
        palette.Query.Text = ">sa";
        Capture.Pump();
        Assert.All(palette.Items, i => Assert.NotNull(i.Command));
        Assert.Equal("Save", palette.Items[0].Label);
        Key(Avalonia.Input.Key.Escape);
        Assert.False(_win.Overlays.IsOpen);
    }

    [AvaloniaFact]
    public void Docking_PanelsTabsAndDirtyDot()
    {
        var ws = Assert.IsType<SceneWorkspace>(_win.CurrentPage);
        Assert.Equal(260, Find<Border>("HierarchyPanel").Bounds.Width);
        Assert.Equal(320, Find<Border>("InspectorPanel").Bounds.Width);
        Assert.Equal(250, Find<Border>("BottomPanel").Bounds.Height);
        Assert.Equal(["Assets", "Console", "Animation", "Database"], ws.PanelTabs.Select(t => t.Label));
        Click(ws.PanelTabs[1]);
        Assert.Equal("Console", ws.SelectedPanel!.Label);
        Assert.Contains(_fx.Services.Windows.Requests, r => r.Window == "T-P05");
        Click(ws.PanelTabs[3]);
        Assert.Contains(_fx.Services.Windows.Requests, r => r.Window == "T-P07");
        // the Database tab opens the Database page (T-P07); back to the Scene Editor
        Assert.Equal("T-P07", _win.CurrentPageId);
        _ = _fx.Services.Windows.Open("T-P01");
        Capture.Pump();
        var scene = _fx.Services.Documents.Open[0];
        Assert.False(Find<DocTab>("Doc_Scene").IsDirty);
        scene.IsDirty = true;
        Capture.Pump();
        Assert.True(Find<DocTab>("Doc_Scene").IsDirty);
        Click(Find<DocTab>("Doc_PlayerController.cs"));
        Assert.Equal("T-P04", _fx.Services.Windows.Requests[^1].Window);
        Assert.Equal(ShortcutContext.Script, _win.ActiveContext);
    }

    [AvaloniaFact]
    public void Shortcuts_SceneToolsUndoAndTextFieldsKeepTyping()
    {
        var ws = (SceneWorkspace)_win.CurrentPage!;
        Key(Avalonia.Input.Key.M);
        Assert.Equal(1, ws.ActiveTool);
        Key(Avalonia.Input.Key.V);
        Assert.Equal(0, ws.ActiveTool);
        var value = 0;
        _fx.Services.Undo.Do(new DelegateAction("set", () => value = 0, () => value = 1));
        Key(Avalonia.Input.Key.Z, RawInputModifiers.Control);
        Assert.Equal(0, value);
        // typing "m" in the hierarchy search does not switch tools
        var search = Find<SearchField>("HierarchySearch");
        search.Focus();
        Key(Avalonia.Input.Key.M);
        Assert.Equal(0, ws.ActiveTool);
    }

    [AvaloniaFact]
    public void Edit_RoutesToFocusedTextBoxOrEditTarget()
    {
        var target = new FakeTarget();
        _win.Edit.DefaultTarget = target;
        _win.Focus();
        _fx.Services.Commands.Execute("edit.copy");
        _fx.Services.Commands.Execute("edit.duplicate");
        _fx.Services.Commands.Execute("edit.selectAll");
        Assert.Equal(["Copy", "Duplicate", "SelectAll"], target.Calls);
        var search = Find<SearchField>("HierarchySearch");
        search.Text = "Player";
        search.Focus();
        Capture.Pump();
        _fx.Services.Commands.Execute("edit.selectAll");
        Assert.Equal("Player", search.SelectedText);
        Assert.Equal(3, target.Calls.Count);
    }

    sealed class FakeTarget : IEditTarget
    {
        public List<string> Calls { get; } = [];
        public bool CanCopy => true;
        public bool CanPaste => true;
        public void Cut() => Calls.Add("Cut");
        public void Copy() => Calls.Add("Copy");
        public void Paste() => Calls.Add("Paste");
        public void Duplicate() => Calls.Add("Duplicate");
        public void Delete() => Calls.Add("Delete");
        public void SelectAll() => Calls.Add("SelectAll");
    }

    [AvaloniaFact]
    public void Theme_DarkLightAccentScaleAndReduceMotion()
    {
        var app = Application.Current!;
        IBrush? Bg() => ThemeService.Token(_win, "bg/base");
        Assert.Equal(Color.Parse("#151619"), ((ISolidColorBrush)Bg()!).Color);
        _fx.Services.Preferences.Update(p => p.Theme = ThemeMode.Light);
        _fx.Services.Theme!.Apply(_fx.Services.Preferences.Current);
        Capture.Pump();
        Assert.Equal(ThemeVariant.Light, app.ActualThemeVariant);
        Assert.Equal(Color.Parse("#F5F6F8"), ((ISolidColorBrush)ThemeService.Token(_win, "bg/base", ThemeVariant.Light)!).Color);
        _fx.Services.Preferences.Update(p => p.Theme = ThemeMode.System);
        _fx.Services.Theme.Apply(_fx.Services.Preferences.Current);
        Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);
        _fx.Services.Preferences.Update(p => { p.Theme = ThemeMode.Dark; p.Accent = "#3ECF8E"; p.UiScalePercent = 125; p.ReduceMotion = true; });
        _fx.Services.Theme.Apply(_fx.Services.Preferences.Current);
        Capture.Pump();
        Assert.Equal(Color.Parse("#3ECF8E"), ((ISolidColorBrush)ThemeService.Token(_win, "accent/default")!).Color);
        Assert.Equal(1.25, _fx.Services.Theme.Scale);
        Assert.True(_fx.Services.Theme.ReduceMotion);
        Assert.Equal(1440 / 1.25, _win.TitleBar.Bounds.Width, 1);
        _fx.Services.Theme.SetAccent(ThemeService.DefaultAccent);
        Assert.Equal(Color.Parse("#4C8DFF"), ((ISolidColorBrush)ThemeService.Token(_win, "accent/default")!).Color);
    }

    [AvaloniaFact]
    public async Task Exit_WithUnsavedChanges_ShowsDialog_DontSaveCloses_SaveAllSaves()
    {
        var saved = 0;
        foreach (var d in _fx.Services.Documents.Open) d.SaveHandler = _ => { saved++; return Task.CompletedTask; };
        _fx.Services.Documents.Open[0].IsDirty = true;
        _fx.Services.Documents.Open[3].IsDirty = true;
        var exit = _win.ConfirmExitAsync();
        Capture.Pump();
        var dlg = Assert.IsType<UnsavedChangesDialog>(_win.Overlays.Open.Single());
        Assert.Contains("2 files have changes", string.Join(" ", dlg.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        Key(Avalonia.Input.Key.Escape);
        Assert.False(await exit);
        exit = _win.ConfirmExitAsync();
        Capture.Pump();
        Click(dlg = (UnsavedChangesDialog)_win.Overlays.Open.Single());
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Save All"));
        Assert.True(await exit);
        Assert.Equal(2, saved);
        Assert.False(_fx.Services.Documents.HasUnsaved);
        _fx.Services.Documents.Open[1].IsDirty = true;
        exit = _win.ConfirmExitAsync();
        Capture.Pump();
        dlg = (UnsavedChangesDialog)_win.Overlays.Open.Single();
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Don't Save"));
        Assert.True(await exit);
        Assert.Equal(2, saved);
    }

    [AvaloniaFact]
    public void Toasts_ShowAndCloseAndTooltipsUseTheTooltipComponent()
    {
        _fx.Services.Toasts.Show("sky_hopper.gba built · 412 KB", ToastStatus.Success, TimeSpan.Zero);
        Capture.Pump();
        var toast = _win.GetVisualDescendants().OfType<Toast>().Single();
        Assert.Equal("sky_hopper.gba built · 412 KB", toast.Text);
        Click(toast.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Close"));
        Assert.Empty(_win.GetVisualDescendants().OfType<Toast>());
        var play = Find<PlayControl>("PlayButton");
        var tip = Assert.IsType<TipContent>(ToolTip.GetTip(play));
        Assert.Equal(new TipContent("Play", "F5"), tip);
    }

    [AvaloniaFact]
    public void Thai_TypingIntoTextFieldsAndSearch()
    {
        var search = Find<SearchField>("HierarchySearch");
        search.Focus();
        _win.KeyTextInput("ผู้เล่น");
        Capture.Pump();
        Assert.Equal("ผู้เล่น", search.Text);
        var field = new TextField { Label = "Name (TH)" };
        var host = Capture.Host(field, 280, 52);
        var box = field.GetVisualDescendants().OfType<TextBox>().Single();
        box.Focus();
        host.KeyTextInput("สวัสดี");
        Capture.Pump();
        Assert.Equal("สวัสดี", field.Text);
        Capture.Save(Capture.Frame(host), Path.Combine(Repo.Evidence, "app", "P10-thai-input.png"));
        host.Close();
    }

    [AvaloniaFact]
    public void StatusBar_ShowsFixtureValuesAndRoutesClicks()
    {
        Assert.Equal("Build OK · 0.8 s", Find<StatusItem>("Status_Build").Content);
        Assert.Equal("mGBA core", Find<StatusItem>("Status_Emulator").Content);
        Click(Find<StatusItem>("Status_Emulator"));
        Assert.Equal("T-D09", _fx.Services.Windows.Requests[^1].Window);
        _fx.Services.Status.ToolchainOk = false;
        Assert.Contains("warn", Find<StatusItem>("Status_Ready").Classes);
    }
}
