using AgbStudio.Build.Content;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.Tilemap;
using AgbStudio.Editor.Windows.UiEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace AgbStudio.Editor.Tests.Windows.UiEditor;

/// <summary>The UI Editor (T-P27) used through its own controls on a real project.</summary>
public sealed class UiEditorTests : IDisposable
{
    readonly PageHarness _h = PageHarness.ForFonts();

    public void Dispose() => _h.Dispose();

    UiEditorPage Open()
    {
        _h.S.Commands.Execute("tools.uiEditor");
        _h.Pump();
        return Assert.IsType<UiEditorPage>(_h.Win.CurrentPage);
    }

    string PageFile(string name) => Path.Combine(_h.S.Project.Current!.UiDir, name + ".uipage");

    [AvaloniaFact]
    public void Opens_FromTheToolsMenu_AndSaysWhenThereAreNoPages()
    {
        var page = Open();
        Assert.Null(page.Library.Page);
        Assert.Contains(_h.All<TextBlock>(), t => t.Text?.Contains("no UI pages yet", StringComparison.Ordinal) == true);
    }

    [AvaloniaFact]
    public void OpensInScene_MakesThePageThatScenesStart_AndTheBuildHasIt()
    {
        var page = Open();
        page.Library.AddPreset("Title");
        _h.Pump();
        var p = _h.S.Project.Current!;
        Assert.Contains(page.Library.Page!.Root.Walk(), e => e.Name == "Continue" && e.Action.Kind == UiActionKind.ContinueGame);
        Assert.Contains(page.Library.Page!.Root.Walk(), e => e.Name == "Options" && e.Action.Kind == UiActionKind.OpenOptions);
        var scene = p.SceneFiles().Select(GameProject.AssetName).First();
        _h.All<SectionHeader>().First(x => x.Title == "Page").IsOpen = true;
        _h.Pump();
        var opens = _h.All<InspectorField>().First(f => f.Label == "Opens in scene");
        opens.BringIntoView();
        _h.Pump();
        _h.Choose(opens, scene);
        _h.Pump();
        Assert.Equal("Title", GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, scene + ".scene")).Page);
        var built = new AgbStudio.Build.Pipeline.GameBuilder(p, new AgbStudio.Build.Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.True(built.Success, string.Join('\n', built.Diagnostics));
    }

    [AvaloniaFact]
    public void Preset_CopiesAReadyMadePage_IntoTheProject_AndUndoRemovesIt()
    {
        var page = Open();
        page.Library.AddPreset("TextInput");
        _h.Pump();
        Assert.True(File.Exists(PageFile("TextInput")));
        Assert.Equal("TextInput", page.Library.PageName);
        Assert.Contains(page.Library.Page!.Root.Walk(), e => e.Kind == UiElementKind.InputField && e.Keyboard != null);
        Assert.NotNull(_h.TryFind<ListRow>("Page_TextInput"));
        _h.S.Undo.Undo();
        _h.Pump();
        Assert.False(File.Exists(PageFile("TextInput")));
    }

    [AvaloniaFact]
    public void Elements_AddSelectEdit_SaveToTheFile_AndUndo()
    {
        var page = Open();
        page.Library.NewPage();
        _h.Pump();
        page.Library.AddElement(UiElementKind.Panel);
        page.Library.AddElement(UiElementKind.Text);
        _h.Pump();
        var text = page.Library.Selected!;
        Assert.Equal(UiElementKind.Text, text.Kind);
        Assert.Equal("Panel", page.Library.ParentOf(text)!.Name);   // a new element goes inside the selected panel
        page.Library.EditSelected("Text", e => e.Text = "Hello");
        _h.Pump();
        var saved = GameProject.Load<UiPageFile>(PageFile("Page"));
        Assert.Equal("Hello", saved.Root.Children[0].Children[0].Text);
        _h.S.Undo.Undo();
        Assert.Equal("Text", GameProject.Load<UiPageFile>(PageFile("Page")).Root.Children[0].Children[0].Text);
        _h.S.Undo.Redo();
        Assert.Equal("Hello", page.Library.Selected!.Text);
    }

    [AvaloniaFact]
    public void Canvas_ClickSelects_DragMoves_HandleResizes_AsOneUndoStep()
    {
        var page = Open();
        page.Library.NewPage();
        page.Library.AddElement(UiElementKind.Panel);
        _h.Pump();
        var c = page.Canvas;
        var scale = c.Scale;
        var origin = c.TranslatePoint(new Point((c.Bounds.Width - 240 * scale) / 2, (c.Bounds.Height - 160 * scale) / 2), _h.Win)!.Value;
        Point At(int x, int y) => new(origin.X + x * scale, origin.Y + y * scale);

        _h.ClickAt(At(200, 150));   // empty: the page is selected
        Assert.Equal("Canvas", page.Library.Selected!.Name);
        _h.ClickAt(At(40, 30));     // the panel at 8,8 96 x 48
        Assert.Equal("Panel", page.Library.Selected!.Name);

        var steps = _h.S.Undo.UndoCount;
        _h.Drag(At(40, 30), At(50, 40));
        Assert.Equal((18, 18), (page.Library.Selected!.Rect.X, page.Library.Selected!.Rect.Y));
        Assert.Equal(steps + 1, _h.S.Undo.UndoCount);
        Assert.Equal(18, GameProject.Load<UiPageFile>(PageFile("Page")).Root.Children[0].Rect.X);

        _h.Drag(At(114, 66), At(124, 76));   // the bottom right handle
        Assert.Equal((106, 58), (page.Library.Selected!.Rect.W, page.Library.Selected!.Rect.H));
        _h.S.Undo.Undo();
        Assert.Equal((96, 48), (page.Library.Selected!.Rect.W, page.Library.Selected!.Rect.H));
    }

    [AvaloniaFact]
    public void Toolbox_OneClickAddsAnElement()
    {
        var page = Open();
        page.Library.NewPage();
        _h.Pump();
        _h.Click(_h.Find<Chip>("Add_Button"));
        Assert.Equal(UiElementKind.Button, page.Library.Selected!.Kind);
        Assert.Equal("Button", GameProject.Load<UiPageFile>(PageFile("Page")).Root.Children[0].Name);
    }

    [AvaloniaFact]
    public void Tree_ReorderAndDrop_MoveElements()
    {
        var page = Open();
        page.Library.NewPage();
        page.Library.AddElement(UiElementKind.Panel);
        page.Library.Select([]);
        page.Library.AddElement(UiElementKind.Text);
        page.Library.Select([]);
        page.Library.AddElement(UiElementKind.Button);
        _h.Pump();
        Assert.Equal(["Panel", "Text", "Button"], page.Library.Page!.Root.Children.Select(e => e.Name));
        page.Library.MoveSelected(-1);
        Assert.Equal(["Panel", "Button", "Text"], page.Library.Page!.Root.Children.Select(e => e.Name));
        page.Library.Reparent([1], [0], 0);   // the button goes into the panel
        Assert.Equal("Button", page.Library.Page!.Root.Children[0].Children[0].Name);
        Assert.Equal("Button", page.Library.Selected!.Name);
        page.Library.DuplicateSelected();
        Assert.Equal(["Button", "Button2"], page.Library.Page!.Root.Children[0].Children.Select(e => e.Name));
        page.Library.DeleteSelected();
        Assert.Equal(["Button"], page.Library.Page!.Root.Children[0].Children.Select(e => e.Name));
    }

    [AvaloniaFact]
    public void Inspector_TextField_ChangesTheElement_AndAnchorKeepsItInPlace()
    {
        var page = Open();
        page.Library.NewPage();
        page.Library.AddElement(UiElementKind.Text);
        _h.Pump();
        Assert.NotNull(_h.TryFind<Border>("Section_Positionandsize"));
        Assert.NotNull(_h.TryFind<Border>("Section_Text"));
        var before = UiLayoutEngine.Resolve(page.Library.Page!)[page.Library.Selected!];
        _h.Choose(_h.All<InspectorField>().First(f => f.Label == "Anchor"), "Bottom Right");
        _h.Pump();
        var r = page.Library.Selected!.Rect;
        Assert.Equal((1.0, 1.0, 1.0, 1.0), (r.AnchorMinX, r.AnchorMinY, r.PivotX, r.PivotY));
        Assert.Equal(before, UiLayoutEngine.Resolve(page.Library.Page!)[page.Library.Selected!]);
    }

    [AvaloniaFact]
    public void Renderer_DrawsLikeTheGame_AndReportsTextThatDoesNotFit()
    {
        var page = Open();
        page.Library.NewPage();
        page.Library.AddElement(UiElementKind.Panel);
        page.Library.EditSelected("Fill", e => e.Fill = "#F80000");
        _h.Pump();
        var r = new UiPageRenderer(_h.S.Project.Current!);
        var px = r.Render(page.Library.Page!, null);
        Assert.Equal(UiPageRenderer.Colour("#F80000"), px[20 * 240 + 20]);   // as the GBA shows it
        page.Library.Select([]);
        page.Library.AddElement(UiElementKind.Panel);
        page.Library.EditSelected("Text", e =>
        {
            e.Rect = UiRect.TopLeft(0, 100, 12, 16);
            e.Text = "This text is much too long for it";
            e.TextColor = "#000000";
        });
        r.Render(page.Library.Page!, null);
        Assert.Contains(r.Problems, p => p.Element.Name == page.Library.Selected!.Name && p.TextWidth > p.Width);
    }

    [AvaloniaFact]
    public void Window_RendersAPresetWithTheKeyboard_Shot()
    {
        var page = Open();
        page.Library.AddPreset("TextInput");
        _h.Pump();
        page.Library.Select(page.Library.Page!.Root.Walk().First(e => e.Kind == UiElementKind.InputField));
        _h.Pump();
        var dir = Path.Combine(Path.GetTempPath(), "agb-ui-editor-shots");
        Directory.CreateDirectory(dir);
        Capture.Save(Capture.Frame(_h.Win), Path.Combine(dir, "ui-editor-textinput.png"));
    }

    [AvaloniaFact]
    public void DraggingATextInAGroup_MovesTheGroup_AndTheNextBuildHasIt()
    {
        var page = Open();
        page.Library.AddPreset("Message");
        _h.Pump();
        var c = page.Canvas;
        var scale = c.Scale;
        var origin = c.TranslatePoint(new Point((c.Bounds.Width - 240 * scale) / 2, (c.Bounds.Height - 160 * scale) / 2), _h.Win)!.Value;
        Point At(int x, int y) => new(origin.X + x * scale, origin.Y + y * scale);
        var window = page.Library.Page!.Root.Children.Single(e => e.Name == "Window");
        var before = UiLayoutEngine.Resolve(page.Library.Page!)[window];
        _h.Drag(At(before.X + 40, before.Y + 16), At(before.X + 44, before.Y + 10));
        Assert.Equal("Window", page.Library.Selected!.Name);
        var after = UiLayoutEngine.Resolve(page.Library.Page!)[page.Library.Selected!];
        Assert.Equal((before.X + 4, before.Y - 6), (after.X, after.Y));
        var built = new AgbStudio.Build.Pipeline.GameBuilder(_h.S.Project.Current!, new AgbStudio.Build.Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.True(built.Success, string.Join('\n', built.Diagnostics));
        var generated = File.ReadAllText(Path.Combine(_h.S.Project.Current!.BuildDir, "gen", "ui_pages.cpp"));
        Assert.Contains($"\"Window\", agb::ui::ElementKind::Panel, 0, {after.X}, {after.Y},", generated);
    }
}
