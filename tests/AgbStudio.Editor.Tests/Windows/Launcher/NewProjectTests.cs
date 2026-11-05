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
        Assert.Equal(["Platformer", "Top-down Adventure", "Turn-based RPG", "Shoot 'em up", "Puzzle", "Visual Novel", "Racing (Mode 7)", "Beat 'em up", "Blank"], TemplateCatalog.All.Select(t => t.Title));
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
        Assert.Equal(9, dlg.Cards.Count);
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
        Assert.Equal($"{taken} is not empty · change the project name or pick another location", dlg.Location.Help);
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
        // its title screen is the start scene; the editor opens on the level (T-D12: the objects to learn from)
        Assert.Equal(("Title", "Level_01"), (s.General.StartScene, s.LastScene));
        Assert.Equal((reference.Settings.General.StartScene, reference.Settings.LastScene), (s.General.StartScene, s.LastScene));
        Assert.True(File.Exists(Path.Combine(p.Root, "README.md")));
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

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_Blank_IsOneEmptyScene_WithOrWithoutTheSampleLevelBox(bool sampleLevel)
    {
        using var h = new LauncherHarness();
        h.Launcher.Git = null;
        _ = h.S.Windows.Open("T-D01", "blank", source: "test");
        Capture.Pump();
        var dlg = (NewProjectDialog)h.Win.Overlays.Open.Single();
        Assert.False(dlg.SampleLevel.IsEnabled);
        dlg.NameField.Text = "Nothing Yet";
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "Made"));
        dlg.SampleLevel.IsChecked = sampleLevel;
        dlg.ThaiFont.IsChecked = true;
        dlg.Git.IsChecked = false;
        var o = dlg.Options();
        var p = AgbStudio.Core.Project.GameProject.Open(ProjectCreator.Create(o).ProjectPath);
        var scene = Assert.Single(p.SceneFiles());
        Assert.Empty(AgbStudio.Core.Project.GameProject.Load<AgbStudio.Core.Project.SceneFile>(scene).Actors);
        Assert.Equal(AgbStudio.Core.Project.GameProject.AssetName(scene), p.Settings.General.StartScene);
        Assert.Empty(p.SpriteFiles());
        Assert.Empty(p.MusicFiles());
        Assert.Empty(p.ScriptFiles());
        Assert.True(File.Exists(Path.Combine(p.FontsDir, "thai_vwf.font")));
        await Task.CompletedTask;
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

    // ---- P15.02-P15.08: each template card makes its template game, and that game builds to a ROM ----------------

    /// <summary>P15.02: the Top-down Adventure card makes the village game (NPC, push block, key, door) and it builds.</summary>
    [AvaloniaFact]
    public async Task Create_TopDownAdventure_WritesTheTemplateGameThatBuilds()
    {
        using var h = new LauncherHarness();
        var p = await CreateFromCard(h, "Top-down Adventure", "Village Walk");
        IsTheTemplate(h, p, "topdown");
        Assert.Equal(["House", "Title", "Village"], Names(p.SceneFiles()));
        Assert.Equal(["Door", "KeyItem", "Npc", "PushBlock"], Names(p.ScriptFiles()));
        Assert.Equal(["Player", "NPC", "Crate", "Door", "Key"], p.Settings.Tags);
        Assert.Equal(["keys", "doorOpen"], p.Settings.Save.Fields.Select(f => f.Name));
        Assert.Equal("Talk", p.Settings.Input[0].Action);
        Assert.Equal("Mode 0 · 1 BG layers · 6 actors · 4 scripts", AgbStudio.Build.Content.Templates.TemplateInfo.Stats(p));
        await BuildsToARom(h, "VILLAGEWALK", "VILL");
    }

    /// <summary>P15.03: the Turn-based RPG card makes the party / battle / shop game with its Database tables, and it builds.</summary>
    [AvaloniaFact]
    public async Task Create_TurnBasedRpg_WritesTheTemplateGameThatBuilds()
    {
        using var h = new LauncherHarness();
        var p = await CreateFromCard(h, "Turn-based RPG", "Slime Quest");
        IsTheTemplate(h, p, "rpg");
        Assert.Equal(["Battle", "Meadow", "Title"], Names(p.SceneFiles()));
        Assert.Equal(["Battle", "Explorer", "Shopkeeper"], Names(p.ScriptFiles()));
        Assert.Equal(["Enemies", "Heroes", "Items"], Names(p.TableFiles()));
        Assert.Equal(["Player", "NPC", "Encounter"], p.Settings.Tags);
        Assert.Equal(["gold", "exp", "heroHp", "mageHp", "mageMp", "potions", "level", "battles"], p.Settings.Save.Fields.Select(f => f.Name));
        Assert.Equal("gold", p.Settings.Scripting.ShopCurrency);
        await BuildsToARom(h, "SLIMEQUEST", "SLIM");
    }

    /// <summary>P15.04: the Shoot 'em up card makes the scrolling stage with waves and the power-up, and it builds.</summary>
    [AvaloniaFact]
    public async Task Create_ShootEmUp_WritesTheTemplateGameThatBuilds()
    {
        using var h = new LauncherHarness();
        var p = await CreateFromCard(h, "Shoot 'em up", "Star Patrol");
        IsTheTemplate(h, p, "shmup");
        Assert.Equal(["Stage_01", "Title"], Names(p.SceneFiles()));
        Assert.Equal(["PowerUpMarker", "ShipController", "SpaceSlime", "Stage"], Names(p.ScriptFiles()));
        Assert.Equal(["Player", "Enemy", "Shot", "EnemyShot", "PowerUp"], p.Settings.Tags);
        Assert.Equal("Fire", p.Settings.Input[0].Action);
        Assert.Equal("Mode 0 · 2 BG layers · 6 actors · 4 scripts", AgbStudio.Build.Content.Templates.TemplateInfo.Stats(p));
        await BuildsToARom(h, "STARPATROL", "STAR");
    }

    /// <summary>P15.05: the Puzzle card with Sample level on makes the falling-gem game (not the empty project), and it builds.</summary>
    [AvaloniaFact]
    public async Task Create_Puzzle_WithSampleLevel_WritesTheTemplateGameThatBuilds()
    {
        using var h = new LauncherHarness();
        var p = await CreateFromCard(h, "Puzzle", "Gem Drop");
        IsTheTemplate(h, p, "puzzle");
        Assert.Equal(["Board", "Title"], Names(p.SceneFiles()));
        Assert.Equal(["Board", "Scoreboard"], Names(p.ScriptFiles()));
        Assert.Equal(["gems"], Names(p.TilesetFiles()));
        Assert.Equal(["Board"], p.Settings.Tags);
        Assert.Equal(["score", "best"], p.Settings.Save.Fields.Select(f => f.Name));
        Assert.Equal("Turn", p.Settings.Input[0].Action);
        Assert.Equal("Mode 0 · 1 BG layers · 2 actors · 2 scripts", AgbStudio.Build.Content.Templates.TemplateInfo.Stats(p));
        await BuildsToARom(h, "GEMDROP", "GEMD");
    }

    /// <summary>P15.07: the Beat 'em up card makes the pier brawl (fighter, thugs, waves), and it builds.</summary>
    [AvaloniaFact]
    public async Task Create_BeatEmUp_WritesTheTemplateGameThatBuilds()
    {
        using var h = new LauncherHarness();
        var p = await CreateFromCard(h, "Beat 'em up", "Pier Brawl");
        IsTheTemplate(h, p, "brawler");
        Assert.Equal(["Pier", "Title"], Names(p.SceneFiles()));
        Assert.Equal(["Fighter", "Thug", "Waves"], Names(p.ScriptFiles()));
        Assert.Equal(["Player", "Enemy"], p.Settings.Tags);
        Assert.Equal("Attack", p.Settings.Input[0].Action);
        Assert.Equal("Mode 0 · 3 BG layers · 3 actors · 3 scripts", AgbStudio.Build.Content.Templates.TemplateInfo.Stats(p));
        await BuildsToARom(h, "PIERBRAWL", "PIER");
    }

    /// <summary>P15.08: the Racing (Mode 7) card makes the circuit on a rotating ground plane (an affine scene), and it builds.</summary>
    [AvaloniaFact]
    public async Task Create_RacingMode7_WritesTheTemplateGameThatBuilds()
    {
        using var h = new LauncherHarness();
        var p = await CreateFromCard(h, "Racing (Mode 7)", "Kart Cup");
        IsTheTemplate(h, p, "racing");
        Assert.Equal(["Circuit", "Title"], Names(p.SceneFiles()));
        Assert.Equal(["Kart", "Rival"], Names(p.ScriptFiles()));
        Assert.Equal(1, GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, "Circuit.scene")).VideoMode);
        Assert.Equal(["Player", "Grass", "Finish", "Check"], p.Settings.Tags);
        Assert.Equal(["best"], p.Settings.Save.Fields.Select(f => f.Name));
        Assert.Equal("Accelerate", p.Settings.Input[0].Action);
        Assert.Equal("Mode 1 · 2 BG layers · 3 actors · 2 scripts", AgbStudio.Build.Content.Templates.TemplateInfo.Stats(p));
        await BuildsToARom(h, "KARTCUP", "KART");
    }

    /// <summary>New Project as a user makes it: File › New Project, the template card, the name, Location, no git, Create Project.</summary>
    static async Task<GameProject> CreateFromCard(LauncherHarness h, string card, string name)
    {
        h.Key(PhysicalKey.N, RawInputModifiers.Control);
        var dlg = Assert.IsType<NewProjectDialog>(h.Win.Overlays.Open.Single());
        var c = dlg.Cards.Single(x => x.Title == card);
        c.BringIntoView();   // the third row of cards is below the fold of the template grid
        Capture.Pump();
        h.Click(c);
        Assert.Equal(card, dlg.Cards.Single(x => x.IsSelected).Title);
        var nameBox = Box(dlg.NameField);
        nameBox.Focus();
        nameBox.SelectAll();
        h.Type(name);
        h.Launcher.PickFolder = _ => Task.FromResult<string?>(Path.Combine(h.Fx.Folder, "Made"));
        h.Click(dlg.Location.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Box"));
        var folder = Path.Combine(h.Fx.Folder, "Made", GameProject.FileName(name));
        Assert.Equal(folder, dlg.Location.Selected);
        // Sample level stays on: the template's own game is made
        Assert.True(dlg.SampleLevel.IsChecked);
        h.Click(dlg.Git);
        Assert.False(dlg.Git.IsChecked);
        h.Click(Confirm(dlg));
        await LauncherHarness.Until(() => h.S.Project.Current != null && !h.Win.Overlays.IsOpen);
        var p = h.S.Project.Current!;
        Assert.Equal(Path.Combine(folder, GameProject.FileName(name) + ".agbproj"), p.ProjectPath);
        Assert.Equal(name, p.Settings.Name);
        Assert.Equal("T-P01", h.Win.CurrentPageId);
        Assert.False(Directory.Exists(Path.Combine(p.Root, ".git")));
        return p;
    }

    /// <summary>The project holds what the AgbStudio.Build template writes, as a project made from it directly.</summary>
    static void IsTheTemplate(LauncherHarness h, GameProject p, string id)
    {
        var reference = AgbStudio.Build.Content.Templates.TemplateCatalog.Find(id)!.Create(Path.Combine(h.Fx.Folder, "Reference"), "Reference");
        Assert.Equal(Names(reference.SceneFiles()), Names(p.SceneFiles()));
        Assert.Equal(Names(reference.ScriptFiles()), Names(p.ScriptFiles()));
        Assert.Equal(Names(reference.SpriteFiles()), Names(p.SpriteFiles()));
        Assert.Equal(Names(reference.TilesetFiles()), Names(p.TilesetFiles()));
        Assert.Equal(Names(reference.MapFiles()), Names(p.MapFiles()));
        Assert.Equal(Names(reference.MusicFiles()), Names(p.MusicFiles()));
        Assert.Equal(Names(reference.SoundFiles()), Names(p.SoundFiles()));
        Assert.Equal(Names(reference.TableFiles()), Names(p.TableFiles()));
        Assert.Equal(reference.Settings.Tags, p.Settings.Tags);
        Assert.Equal(reference.Settings.Save.Fields.Select(f => f.Name), p.Settings.Save.Fields.Select(f => f.Name));
        Assert.Equal(AgbStudio.Build.Content.Templates.TemplateInfo.Stats(reference), AgbStudio.Build.Content.Templates.TemplateInfo.Stats(p));
        // its title screen is the start scene, the editor opens on its main scene (T-D12), the template's own Thai font
        // is the default font and README.md explains every object
        Assert.Equal(("Title", AgbStudio.Build.Content.Templates.TemplateKit.MainScene(reference)), (p.Settings.General.StartScene, p.Settings.LastScene));
        Assert.Equal(File.ReadAllText(Path.Combine(reference.Root, "README.md")).Replace("Reference", "", StringComparison.Ordinal),
            File.ReadAllText(Path.Combine(p.Root, "README.md")).Replace(p.Settings.General.GameTitle, "", StringComparison.Ordinal));
        Assert.Equal("thai_vwf", p.Settings.General.DefaultFont);
        Assert.Empty(p.GraphFiles());
        Assert.All(p.ScriptFiles(), f => Assert.Contains("class ", File.ReadAllText(f)));
    }

    /// <summary>Build › Build ROM (a real build with devkitPro) writes the ROM, with the ROM title and game code of New Project in its header.</summary>
    static async Task BuildsToARom(LauncherHarness h, string romTitle, string gameCode)
    {
        Assert.True(new AgbStudio.Build.Toolchain().IsComplete, "devkitPro toolchain not found");
        Assert.Equal((romTitle, gameCode), (h.S.Project.Current!.Settings.General.RomTitle, h.S.Project.Current.Settings.General.GameCode));
        h.S.ProjectSettings.Settings.Build.RunAfterBuild = false;
        h.S.Commands.Execute("build.buildRom");
        Capture.Pump();
        var d = Assert.IsType<AgbStudio.Editor.Windows.Build.BuildRomDialog>(h.Win.Overlays.Open.Single());
        await LauncherHarness.Until(() => d.Completion.IsCompleted, 900_000);
        var r = d.Completion.Result!;
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.True(File.Exists(r.RomPath), r.RomPath);
        Assert.StartsWith(Path.GetFullPath(h.S.Project.Current.BuildDir), Path.GetFullPath(r.RomPath), StringComparison.OrdinalIgnoreCase);
        var rom = File.ReadAllBytes(r.RomPath);
        Assert.Equal(romTitle, System.Text.Encoding.ASCII.GetString(rom, 0xA0, 12).TrimEnd('\0', ' '));
        Assert.Equal(gameCode, System.Text.Encoding.ASCII.GetString(rom, 0xAC, 4));
        Assert.StartsWith("Build OK", h.S.Status.Build, StringComparison.Ordinal);
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

    // ---- P10.08: New Project closes the open project, so unsaved files are asked about first (T-D12) ---------------

    /// <summary>Sky Hopper open with PlayerController.cs edited in the Script Editor and not saved; File › New Project… filled in.</summary>
    static async Task<(LauncherHarness H, NewProjectDialog Dlg, string Script, string Before)> NewProjectWithAnUnsavedScript()
    {
        var h = new LauncherHarness(startPage: null, recent: false);
        h.Fx.OpenSkyHopper();
        h.Show("T-P01");
        AgbStudio.Editor.Windows.Scripts.ScriptEditorSettings.Of(h.S).FormatOnSave = false;
        var script = Path.Combine(h.S.Project.Current!.ScriptsDir, "PlayerController.cs");
        var before = File.ReadAllText(script);
        _ = h.S.Windows.Open("T-P04", script, source: "test");
        await LauncherHarness.Until(() => h.Win.CurrentPage is AgbStudio.Editor.Windows.Scripts.ScriptEditorPage { Buffer: not null });
        var page = (AgbStudio.Editor.Windows.Scripts.ScriptEditorPage)h.Win.CurrentPage!;
        page.Editor.Focus();
        page.Editor.MoveCaret(1, page.Editor.LineText(0).Length + 1);
        h.Type(" // unsaved");
        Assert.True(page.Buffer!.Document.IsDirty);
        // File › New Project…
        var file = h.Win.MenuBar.Items.OfType<MenuItem>().Single(m => (string?)m.Header == "File");
        h.Click(file);
        h.Click(file.Items.OfType<MenuItem>().Single(m => (string?)m.Header == "New Project…"));
        var dlg = Assert.IsType<NewProjectDialog>(h.Win.Overlays.Open.Single());
        dlg.NameField.Text = "Forest Run";
        dlg.SetLocationBase(Path.Combine(h.Fx.Folder, "Made"));
        dlg.SampleLevel.IsChecked = false;
        return (h, dlg, script, before);
    }

    static UnsavedChangesDialog AskedFirst(LauncherHarness h)
    {
        var ask = Assert.IsType<UnsavedChangesDialog>(h.Win.Overlays.Open[^1]);
        Assert.Equal(("T-D12", "NewProject"), h.S.Windows.Requests[^1]);
        Assert.Equal("Save changes before closing the project?", ask.GetVisualDescendants().OfType<DialogHeader>().Single().Title);
        Assert.Contains("PlayerController.cs", ask.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        return ask;
    }

    /// <summary>
    /// P10.08 "close": Create Project with an unsaved script asks "Save changes before closing the project?". Esc keeps
    /// the open project and the New Project dialog, creates nothing and leaves the script unsaved; Create Project again
    /// and Save All saves the script, then the new project is created and opened.
    /// </summary>
    [AvaloniaFact]
    public async Task P10_08_NewProject_WithAnUnsavedFile_AsksFirst_CancelKeepsIt_SaveAllSavesThenCreates()
    {
        var (h, dlg, script, before) = await NewProjectWithAnUnsavedScript();
        using var _ = h;
        h.Click(Confirm(dlg));
        AskedFirst(h);
        h.Key(PhysicalKey.Escape);
        Assert.Same(dlg, h.Win.Overlays.Open.Single());
        Assert.Equal("Sky Hopper", h.S.Project.Name);
        Assert.False(Directory.Exists(Path.Combine(h.Fx.Folder, "Made", "ForestRun")));
        Assert.True(h.S.Documents.HasUnsaved);
        Assert.Equal(before, File.ReadAllText(script));

        h.Click(Confirm(dlg));
        var ask = AskedFirst(h);
        h.Click(ask.GetVisualDescendants().OfType<AgbButton>().Single(b => (string?)b.Content == "Save All"));
        await LauncherHarness.Until(() => h.S.Project.Name == "Forest Run" && !h.Win.Overlays.IsOpen);
        Assert.Contains("// unsaved", File.ReadAllText(script));
        Assert.Equal(Path.Combine(h.Fx.Folder, "Made", "ForestRun", "ForestRun.agbproj"), h.S.Project.Current!.ProjectPath);
        Assert.False(h.S.Documents.HasUnsaved);
        Assert.Equal("T-P01", h.Win.CurrentPageId);
    }

    /// <summary>P10.08 "close": Don't Save leaves the script on disk as it was and the new project is created and opened.</summary>
    [AvaloniaFact]
    public async Task P10_08_NewProject_WithAnUnsavedFile_DontSaveCreatesWithoutWriting()
    {
        var (h, dlg, script, before) = await NewProjectWithAnUnsavedScript();
        using var _ = h;
        h.Click(Confirm(dlg));
        var ask = AskedFirst(h);
        h.Click(ask.GetVisualDescendants().OfType<AgbButton>().Single(b => (string?)b.Content == "Don't Save"));
        await LauncherHarness.Until(() => h.S.Project.Name == "Forest Run" && !h.Win.Overlays.IsOpen);
        Assert.Equal(before, File.ReadAllText(script));
        Assert.False(h.S.Documents.HasUnsaved);
        Assert.DoesNotContain(h.S.Documents.Open, d => d.Name == "PlayerController.cs");
    }

    static string[] Names(IEnumerable<string> files) => files.Select(GameProject.AssetName).Order().ToArray();
    static IBrush? Token(Control c, string key) => c.TryFindResource(key, c.ActualThemeVariant, out var v) ? v as IBrush : null;
}
