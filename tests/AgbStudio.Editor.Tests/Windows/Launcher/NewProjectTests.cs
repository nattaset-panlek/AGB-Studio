using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Launcher;
using AgbStudio.Scripting.Graph;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Launcher;

/// <summary>New Project (P11.08): fields, validation, the devkitPro warning, and every option changing the created project.</summary>
public class NewProjectTests
{
    [Fact]
    public void Rules_RomTitleGameCodeLocationAndDevkitPro()
    {
        Assert.Equal("SKYHOPPER", NewProjectRules.RomTitleFrom("Sky Hopper"));
        Assert.Equal("SUPERLONGGAM", NewProjectRules.RomTitleFrom("Super long game name"));
        Assert.Equal("SKYH", NewProjectRules.GameCodeFrom("SKYHOPPER"));
        Assert.Equal("ABXX", NewProjectRules.GameCodeFrom("AB"));
        Assert.True(NewProjectRules.IsValidRomTitle("SKYHOPPER"));
        Assert.True(NewProjectRules.IsValidRomTitle("SKY HOP-2"));
        Assert.False(NewProjectRules.IsValidRomTitle(""));
        Assert.False(NewProjectRules.IsValidRomTitle("ABCDEFGHIJKLM"));   // 13
        Assert.False(NewProjectRules.IsValidRomTitle("sky"));
        Assert.False(NewProjectRules.IsValidRomTitle("SKY!"));
        Assert.True(NewProjectRules.IsValidGameCode("SKYH"));
        Assert.False(NewProjectRules.IsValidGameCode("SKY"));
        Assert.False(NewProjectRules.IsValidGameCode("SKYHO"));
        Assert.False(NewProjectRules.IsValidGameCode("SK-H"));
        Assert.Equal(@"D:\GBAProjects\SkyHopper", NewProjectRules.FolderFrom(@"D:\GBAProjects", "Sky Hopper"));
        Assert.True(NewProjectRules.IsInside(@"D:\devkitPro\projects\SkyHopper", @"D:\devkitPro"));
        Assert.True(NewProjectRules.IsInside(@"d:\DEVKITPRO\x", @"D:\devkitPro\"));
        Assert.False(NewProjectRules.IsInside(@"D:\devkitProjects\SkyHopper", @"D:\devkitPro"));
        Assert.False(NewProjectRules.IsInside(@"D:\GBAProjects\SkyHopper", @"D:\devkitPro"));
        Assert.Equal(["None", "SRAM · 32 KB", "Flash · 64 KB", "Flash · 128 KB", "EEPROM · 8 KB"], NewProjectRules.SaveTypes.Select(NewProjectRules.SaveTypeLabel));
        Assert.False(NewProjectRules.IsUsableFolder("relative\\folder"));
        var tmp = Directory.CreateTempSubdirectory("agb-np-").FullName;
        try
        {
            Assert.True(NewProjectRules.IsUsableFolder(tmp));                          // empty
            Assert.True(NewProjectRules.IsUsableFolder(Path.Combine(tmp, "new")));     // not there yet
            File.WriteAllText(Path.Combine(tmp, "x.txt"), "");
            Assert.False(NewProjectRules.IsUsableFolder(tmp));                         // not empty
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void Catalog_EightTemplatesInFigmaOrder()
    {
        Assert.Equal(["Platformer", "Top-down Adventure", "Turn-based RPG", "Shoot 'em up", "Puzzle", "Visual Novel", "Racing (Mode 7)", "Beat 'em up"], TemplateCatalog.All.Select(t => t.Title));
        Assert.All(TemplateCatalog.All, t => Assert.True(Graphics.IconCatalog.Art(t.Preview).Width > 0));
        Assert.Equal(["Platformer", "Shoot 'em up", "Beat 'em up"], TemplateCatalog.Filter(TemplateCategory.Action, null).Select(t => t.Title));
        Assert.Equal(["Visual Novel"], TemplateCatalog.Filter(null, "portraits").Select(t => t.Title));
    }

    [AvaloniaFact]
    public void Fields_DeriveFromTheNameValidateAndWarnInsideDevkitPro()
    {
        using var h = new LauncherHarness();
        h.Key(PhysicalKey.N, RawInputModifiers.Control);   // File › New Project… from the Launcher
        var dlg = Assert.IsType<NewProjectDialog>(h.Win.Overlays.Open.Single());
        Assert.Equal(1180, dlg.Bounds.Width);
        Assert.Equal(668, dlg.Bounds.Height);
        Assert.Equal(8, dlg.Cards.Count);
        Assert.Equal("Platformer", dlg.Cards.Single(c => c.IsSelected).Title);
        Assert.Equal("Template adds: player controller, enemies, coins, HUD and a title screen.", dlg.GetVisualDescendants().OfType<DialogFooter>().Single().Hint);
        Assert.Equal("My Game", dlg.NameField.Text);
        Assert.Equal("MYGAME", dlg.RomTitle.Text);
        Assert.Equal("MYGA", dlg.GameCode.Text);
        Assert.Equal("SRAM · 32 KB", dlg.SaveTypeField.Selected);
        Assert.Equal(Path.Combine(h.Fx.Folder, "GBAProjects", "MyGame"), dlg.Location.Selected);
        Assert.Equal(@"A folder outside D:\devkitPro is safer for updates", dlg.Location.Help);
        Assert.Equal([true, true, false, true], new[] { dlg.SampleLevel, dlg.ThaiFont, dlg.LogicGraph, dlg.Git }.Select(c => c.IsChecked == true));

        // typing the name fills ROM title, game code and location
        var nameBox = Box(dlg.NameField);
        Assert.True(nameBox.IsFocused);
        nameBox.SelectAll();
        h.Type("Sky Hopper");
        Assert.Equal("SKYHOPPER", dlg.RomTitle.Text);
        Assert.Equal("SKYH", dlg.GameCode.Text);
        Assert.Equal(Path.Combine(h.Fx.Folder, "GBAProjects", "SkyHopper"), dlg.Location.Selected);

        // that folder holds the fixture's Sky Hopper: a folder that is not empty cannot be used
        var taken = Path.Combine(h.Fx.Folder, "GBAProjects", "SkyHopper");
        Assert.True(Directory.Exists(taken));
        Assert.False(Confirm(dlg).IsEnabled);
        Assert.Equal($"{taken} is not empty", dlg.Location.Help);
        Assert.Equal(Token(dlg, "status/danger"), Help(dlg.Location).Foreground);

        // Location opens the folder picker; the project folder goes inside the picked folder
        h.Launcher.PickFolder = _ => Task.FromResult<string?>(Path.Combine(h.Fx.Folder, "Elsewhere"));
        h.Click(dlg.Location.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Box"));
        Assert.Equal(Path.Combine(h.Fx.Folder, "Elsewhere", "SkyHopper"), dlg.Location.Selected);
        Assert.True(Confirm(dlg).IsEnabled);
        Assert.Equal(Token(dlg, "text/muted"), Help(dlg.Location).Foreground);

        // ROM title: 12 characters max, upper case; the game code follows until edited
        var rom = Box(dlg.RomTitle);
        rom.Focus();
        rom.SelectAll();
        h.Type("skyhopperdeluxe");
        Assert.Equal("SKYHOPPERDEL", dlg.RomTitle.Text);
        Assert.Equal("SKYH", dlg.GameCode.Text);
        Assert.True(Confirm(dlg).IsEnabled);
        rom.SelectAll();
        h.Type("sky!");
        Assert.Equal("SKY!", dlg.RomTitle.Text);
        Assert.False(Confirm(dlg).IsEnabled);
        Assert.Equal(Token(dlg, "status/danger"), Help(dlg.RomTitle).Foreground);
        rom.SelectAll();
        h.Type("SKYHOPPER");
        Assert.True(Confirm(dlg).IsEnabled);
        Assert.Equal(Token(dlg, "text/muted"), Help(dlg.RomTitle).Foreground);

        // game code: exactly 4
        var code = Box(dlg.GameCode);
        code.Focus();
        code.SelectAll();
        h.Type("skyhopper");
        Assert.Equal("SKYH", dlg.GameCode.Text);
        code.SelectAll();
        h.Type("ab");
        Assert.False(Confirm(dlg).IsEnabled);
        Assert.Equal(Token(dlg, "status/danger"), Help(dlg.GameCode).Foreground);
        code.SelectAll();
        h.Type("ABCD");
        Assert.True(Confirm(dlg).IsEnabled);

        // the devkitPro warning: inside it the hint turns into a warning
        h.Launcher.DevkitPro = Path.Combine(h.Fx.Folder, "devkitPro");
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "devkitPro", "projects"));
        Assert.True(dlg.LocationWarns);
        Assert.Equal($"A folder outside {h.Launcher.DevkitPro} is safer for updates", dlg.Location.Help);
        Assert.Equal(Token(dlg, "status/warning"), Help(dlg.Location).Foreground);
        Assert.True(Confirm(dlg).IsEnabled);
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "Elsewhere"));
        Assert.False(dlg.LocationWarns);
        Assert.Equal(Token(dlg, "text/muted"), Help(dlg.Location).Foreground);

        // Save type opens its options; picking one changes the field
        h.Click(dlg.SaveTypeField.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Box"));
        var menu = h.Win.GetVisualDescendants().OfType<ContextMenu>().Single();
        Assert.Equal(["None", "SRAM · 32 KB", "Flash · 64 KB", "Flash · 128 KB", "EEPROM · 8 KB"], menu.Items.OfType<MenuItem>().Select(m => (string)m.Header!));
        Assert.True(menu.Items.OfType<MenuItem>().Single(m => m.IsChecked).Header is "SRAM · 32 KB");
        var item = menu.Items.OfType<MenuItem>().Single(m => (string?)m.Header == "Flash · 128 KB");
        h.Click(item);
        Assert.Equal("Flash · 128 KB", dlg.SaveTypeField.Selected);
        Assert.Equal(SaveType.Flash128K, dlg.SelectedSaveType);

        // a template card changes the footer hint
        h.Click(dlg.Cards.First(c => c.Title == "Visual Novel"));
        Assert.Equal("novel", dlg.Template.Id);
        Assert.Equal("Visual Novel", dlg.Cards.Single(c => c.IsSelected).Title);
        Assert.Equal("Template adds: portraits, dialogue, choices and flags.", dlg.GetVisualDescendants().OfType<DialogFooter>().Single().Hint);
    }

    [AvaloniaFact]
    public async Task Create_PlatformerWithSampleLevelThaiFontAndGit()
    {
        using var h = new LauncherHarness();
        h.Launcher.Git = null;   // the real git on PATH
        _ = h.S.Windows.Open("T-D01", "platformer", source: "test");
        Capture.Pump();
        var dlg = (NewProjectDialog)h.Win.Overlays.Open.Single();
        dlg.NameField.Text = "Forest Run";
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "Made"));
        dlg.SetSaveType(SaveType.Flash128K);
        dlg.RomTitle.Text = "FORESTRUN";
        dlg.GameCode.Text = "FRST";
        h.Click(Confirm(dlg));
        await LauncherHarness.Until(() => h.S.Project.Current != null && !h.Win.Overlays.IsOpen);

        var p = h.S.Project.Current!;
        Assert.Equal(Path.Combine(h.Fx.Folder, "Made", "ForestRun", "ForestRun.agbproj"), p.ProjectPath);
        var s = p.Settings;
        Assert.Equal(("Forest Run", "Forest Run", "FORESTRUN", "FRST", SaveType.Flash128K), (s.Name, s.General.GameTitle, s.General.RomTitle, s.General.GameCode, s.Save.Type));
        Assert.Equal("ForestRun", s.Scripting.Namespace);
        // Sample level: the Platformer template of AgbStudio.Build writes the whole game, as a project made from it directly
        var reference = AgbStudio.Build.Content.Templates.TemplateCatalog.Find("platformer")!.Create(Path.Combine(h.Fx.Folder, "Reference"), "Reference");
        Assert.Equal(Names(reference.SceneFiles()), Names(p.SceneFiles()));
        Assert.Equal(Names(reference.ScriptFiles()), Names(p.ScriptFiles()));
        Assert.Equal(Names(reference.SpriteFiles()), Names(p.SpriteFiles()));
        Assert.Equal(Names(reference.MapFiles()), Names(p.MapFiles()));
        Assert.Equal(reference.MusicFiles().Count(), p.MusicFiles().Count());
        Assert.Contains("PlayerController", Names(p.ScriptFiles()));
        Assert.Contains(p.SceneFiles().Select(GameProject.Load<SceneFile>).SelectMany(sc => sc.Actors), a => a.Kind == ActorKind.Player);
        // its title screen is the start scene
        Assert.Equal((reference.Settings.General.StartScene, reference.Settings.General.StartScene), (s.General.StartScene, s.LastScene));
        // Thai variable-width font (the template's own)
        var font = GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font"));
        Assert.True(File.Exists(Path.Combine(p.FontsDir, font.Source)));
        Assert.Equal("thai_vwf", s.General.DefaultFont);
        // no Logic Graph examples
        Assert.Empty(p.GraphFiles());
        // Initialize git repository ran git init
        Assert.True(Directory.Exists(Path.Combine(p.Root, ".git")));
        // nothing is left of the staging folder; the project is open, first in the recent list and on the Scene Editor
        Assert.Equal(["ForestRun"], Directory.GetFileSystemEntries(Path.Combine(h.Fx.Folder, "Made")).Select(Path.GetFileName));
        Assert.Equal(p.ProjectPath, h.S.Recent.Items[0].Path);
        Assert.Equal("T-P01", h.Win.CurrentPageId);
        Assert.Equal(["Scene", "Game"], h.S.Documents.Open.Select(d => d.Name));
        Assert.Equal("art/scene/day", h.Launcher.Thumbnail(p.ProjectPath, "Forest Run"));
    }

    [AvaloniaFact]
    public async Task Create_PuzzleWithLogicGraphOnly()
    {
        using var h = new LauncherHarness();
        h.Launcher.Git = null;
        _ = h.S.Windows.Open("T-D01", "puzzle", source: "test");
        Capture.Pump();
        var dlg = (NewProjectDialog)h.Win.Overlays.Open.Single();
        dlg.NameField.Text = "Block Drop";
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "Made"));
        dlg.SampleLevel.IsChecked = false;
        dlg.ThaiFont.IsChecked = false;
        dlg.LogicGraph.IsChecked = true;
        dlg.Git.IsChecked = false;
        Assert.Equal("BLOCKDROP", dlg.RomTitle.Text);
        Assert.Equal("BLOC", dlg.GameCode.Text);
        h.Click(Confirm(dlg));
        await LauncherHarness.Until(() => h.S.Project.Current != null && !h.Win.Overlays.IsOpen);

        var p = h.S.Project.Current!;
        Assert.Equal(("Block Drop", "BLOCKDROP", "BLOC", SaveType.Sram32K), (p.Settings.Name, p.Settings.General.RomTitle, p.Settings.General.GameCode, p.Settings.Save.Type));
        Assert.Empty(p.SpriteFiles());
        Assert.Equal(["Scene_01"], p.SceneFiles().Select(GameProject.AssetName));
        Assert.Equal("Scene_01", p.Settings.General.StartScene);
        Assert.Empty(p.FontFiles());
        Assert.Equal("", p.Settings.General.DefaultFont);
        var graph = AgbStudio.Scripting.Graph.LogicGraph.Parse(File.ReadAllText(p.GraphFiles().Single()));
        Assert.Equal("PlayerLogic", graph.Class);
        Assert.Contains("class PlayerLogic", GraphCodeGen.Generate(graph, "PlayerLogic.graph"));
        Assert.False(Directory.Exists(Path.Combine(p.Root, ".git")));
        Assert.Equal("art/genre/puzzle", h.Launcher.Thumbnail(p.ProjectPath, "Block Drop"));
    }

    /// <summary>A template card makes the game of its AgbStudio.Build template (the Visual Novel card is "novel", its template "visualnovel").</summary>
    [AvaloniaFact]
    public async Task Create_VisualNovel_WritesTheTemplateGame()
    {
        using var h = new LauncherHarness();
        _ = h.S.Windows.Open("T-D01", "novel", source: "test");
        Capture.Pump();
        var dlg = (NewProjectDialog)h.Win.Overlays.Open.Single();
        dlg.NameField.Text = "Dusk Tale";
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "Made"));
        dlg.Git.IsChecked = false;
        h.Click(Confirm(dlg));
        await LauncherHarness.Until(() => h.S.Project.Current != null && !h.Win.Overlays.IsOpen);

        var p = h.S.Project.Current!;
        Assert.Equal(("Dusk Tale", "DUSKTALE"), (p.Settings.Name, p.Settings.General.RomTitle));
        var reference = AgbStudio.Build.Content.Templates.TemplateCatalog.Find("visualnovel")!.Create(Path.Combine(h.Fx.Folder, "Reference"), "Reference");
        Assert.NotEmpty(p.SceneFiles());
        Assert.NotEmpty(p.ScriptFiles());
        Assert.Equal(Names(reference.SceneFiles()), Names(p.SceneFiles()));
        Assert.Equal(Names(reference.ScriptFiles()), Names(p.ScriptFiles()));
        Assert.Equal(Names(reference.SpriteFiles()), Names(p.SpriteFiles()));
        Assert.All(p.ScriptFiles(), f => Assert.Contains("class ", File.ReadAllText(f)));
        Assert.Equal(reference.Settings.General.StartScene, p.Settings.General.StartScene);
        Assert.Equal("T-P01", h.Win.CurrentPageId);
        // every card of Launcher · Templates has its AgbStudio.Build template
        Assert.All(TemplateCatalog.All, t => Assert.NotNull(ProjectCreator.BuildTemplate(t)));
    }

    [AvaloniaFact]
    public void LightTheme_UsesTheLightTokens()
    {
        using var h = new LauncherHarness();
        h.S.Preferences.Update(p => p.Theme = ThemeMode.Light);
        h.S.Theme!.Apply(h.S.Preferences.Current);
        _ = h.S.Windows.Open("T-D01", source: "test");
        Capture.Pump();
        var dlg = (NewProjectDialog)h.Win.Overlays.Open.Single();
        Assert.Equal(ThemeVariant.Light, dlg.ActualThemeVariant);
        Assert.Equal(((ISolidColorBrush)ThemeService.Token(dlg, "bg/panel", ThemeVariant.Light)!).Color, ((ISolidColorBrush)dlg.Background!).Color);
    }

    static TextBox Box(TextField f) => f.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "PART_Box");
    static TextBlock Help(TemplatedControl f) => f.GetVisualDescendants().OfType<StackPanel>().First().Children.OfType<TextBlock>().Last();
    static Button Confirm(NewProjectDialog d) => d.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Confirm");

    static string[] Names(IEnumerable<string> files) => files.Select(GameProject.AssetName).Order().ToArray();
    static IBrush? Token(Control c, string key) => c.TryFindResource(key, c.ActualThemeVariant, out var v) ? v as IBrush : null;
}
