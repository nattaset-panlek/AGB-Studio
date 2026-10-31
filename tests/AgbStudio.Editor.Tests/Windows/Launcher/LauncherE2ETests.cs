using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Launcher;
using AgbStudio.Editor.Windows.Menus;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Launcher;

/// <summary>Launcher E2E (P11.01 Projects, P11.02 Templates, P11.03 Learn, P11.05 Project Context Menu, P11.07 What's new).</summary>
public class LauncherE2ETests
{
    [AvaloniaFact]
    public async Task P11_01_Projects_RecentListSearchThumbnailsTimesOpenAndNewProject()
    {
        using var h = new LauncherHarness();
        var page = h.Page;
        Assert.Equal("T-P12", h.Win.CurrentPageId);
        Assert.False(h.Win.TitleBar.IsVisible);   // the Launcher has its own top bar
        Assert.Equal("v0.1 · devkitARM 16.1 found", h.Find<TextBlock>("LauncherStatus").Text);
        Assert.Equal(["Sky Hopper", "Cave Quest RPG", "Snow Run", "Dusk Novel"], page.ProjectCards.Select(c => c.NameText));
        Assert.Equal(["2 h ago", "yesterday", "3 days ago", "last week"], page.ProjectCards.Select(c => c.Meta));
        Assert.Equal(["art/scene/day", "art/scene/cave", "art/scene/snow", "art/scene/dusk"], page.ProjectCards.Select(c => c.Preview));
        Assert.True(page.ProjectCards[0].IsSelected);
        Assert.Equal(["Platformer", "Top-down Adventure", "Turn-based RPG"], h.Find<StackPanel>("HomeTemplates").Children.OfType<TemplateCard>().Select(c => c.Title));

        // search
        var search = h.Find<SearchField>("LauncherSearch");
        search.Focus();
        h.Type("snow");
        Assert.Equal(["Snow Run"], page.ProjectCards.Select(c => c.NameText));
        search.Text = "";
        Capture.Pump();
        Assert.Equal(4, page.ProjectCards.Count);

        // the name on a card comes from the project file
        var cave = h.S.Recent.Items.First(r => r.Name == "Cave Quest RPG");
        var settings = GameProject.Load<AgbStudio.Core.Project.ProjectFile>(cave.Path);
        settings.Name = "Cave Quest";
        GameProject.Save(cave.Path, settings);
        h.S.Recent.Touch("Cave Quest RPG", cave.Path);
        await LauncherHarness.Until(() => page.ProjectCards.FirstOrDefault()?.NameText == "Cave Quest");
        Assert.Equal("just now", page.ProjectCards[0].Meta);

        // click selects, double-click opens through Opening Project
        h.Click(page.ProjectCards[1]);
        Assert.Equal("Sky Hopper", h.Launcher.Selected!.Name);
        h.Launcher.BeforeOpenStep = (_, ct) => Task.Delay(Timeout.Infinite, ct);
        h.DoubleClick(page.ProjectCards[1]);
        var opening = Assert.IsType<OpeningProjectDialog>(h.Win.Overlays.Open.Single());
        Assert.Equal("Sky Hopper", opening.ProjectName);
        Assert.Contains(h.S.Windows.Requests, r => r is { Window: "T-D22", Source: "ProjectCard" });
        opening.Cancel();
        Capture.Pump();
        Assert.False(h.Win.Overlays.IsOpen);

        // Open… (T-Y01 folder picker) and New Project
        var folder = Path.GetDirectoryName(h.S.Recent.Items.First(r => r.Name == "Snow Run").Path)!;
        h.Launcher.PickFolder = _ => Task.FromResult<string?>(folder);
        h.Click(h.Find<AgbButton>("LauncherOpen"));
        await LauncherHarness.Until(() => h.Win.Overlays.IsOpen);
        Assert.Equal("Snow Run", Assert.IsType<OpeningProjectDialog>(h.Win.Overlays.Open.Single()).ProjectName);
        ((OpeningProjectDialog)h.Win.Overlays.Open.Single()).Cancel();
        Capture.Pump();
        h.Launcher.PickFolder = _ => Task.FromResult<string?>(h.Fx.Folder);
        h.Click(h.Find<AgbButton>("LauncherOpen"));
        await LauncherHarness.Until(() => h.S.Toasts.Visible.Count > 0);
        Assert.Contains("No .agbproj file", h.S.Toasts.Visible[0].Text);
        h.Click(h.Find<AgbButton>("LauncherNewProject"));
        Assert.IsType<NewProjectDialog>(h.Win.Overlays.Open.Single());
        h.Key(PhysicalKey.Escape);
        Assert.False(h.Win.Overlays.IsOpen);

        // File › Open Project… (Ctrl+O) and Help › Getting Started come back to the Launcher
        await h.S.Windows.Open("T-P01");
        Assert.True(h.Win.TitleBar.IsVisible);
        h.Key(PhysicalKey.O, RawInputModifiers.Control);
        Assert.Equal("T-P12", h.Win.CurrentPageId);
        Assert.False(h.Win.TitleBar.IsVisible);
        await h.S.Windows.Open("T-P01");
        h.S.Commands.Execute("help.gettingStarted");
        Assert.Equal("T-P12", h.Win.CurrentPageId);
    }

    [AvaloniaFact]
    public void P11_01_Navigation_SwitchesTabsAndSettingsOpensPreferences()
    {
        using var h = new LauncherHarness();
        h.Click(h.Find<NavItem>("Nav_Templates"));
        Assert.Equal("T-P20", h.Win.CurrentPageId);
        Assert.True(h.Find<NavItem>("Nav_Templates").IsSelected);
        h.Click(h.Find<NavItem>("Nav_Learn"));
        Assert.Equal("T-P21", h.Win.CurrentPageId);
        h.Click(h.Find<NavItem>("Nav_Projects"));
        Assert.Equal("T-P12", h.Win.CurrentPageId);
        h.Click(h.Find<NavItem>("Nav_Settings"));
        Assert.Equal(("T-D09", "LauncherNav_Settings"), h.S.Windows.Requests[^1]);
        h.Key(PhysicalKey.Escape);
        h.Click(h.Find<Button>("AllTemplates"));
        Assert.Equal("T-P20", h.Win.CurrentPageId);
    }

    [AvaloniaFact]
    public void P11_02_Templates_FiltersSearchDetailsAndUseThisTemplate()
    {
        using var h = new LauncherHarness("T-P20");
        var page = h.Page;
        Assert.Equal(TemplateCatalog.All.Select(t => t.Title), page.TemplateCards.Select(c => c.Title));
        Assert.Equal(8, page.TemplateCards.Count);
        Assert.Equal(["All · 8", "Action", "Adventure", "RPG", "Puzzle", "Story", "Racing"], h.Find<StackPanel>("TemplateFilters").Children.OfType<Chip>().Select(c => (string)c.Content!));
        Assert.Equal(ChipStyle.Accent, h.Find<Chip>("Filter_All").Style);
        // details of the selected template: Includes, stats
        Assert.Equal("Platformer", h.Find<TextBlock>("TemplateTitle").Text);
        Assert.Contains("Player controller with jump and coyote time", h.Texts(h.Find<StackPanel>("TemplateIncludes")));
        Assert.Contains("INCLUDES", h.Texts(h.Find<StackPanel>("TemplateDetails")));
        Assert.Contains("Mode 0 · 3 BG layers · 9 actors · 4 scripts", h.Texts(h.Find<StackPanel>("TemplateDetails")));

        h.Click(h.Find<Chip>("Filter_Action"));
        Assert.Equal(["Platformer", "Shoot 'em up", "Beat 'em up"], page.TemplateCards.Select(c => c.Title));
        Assert.Equal(ChipStyle.Accent, h.Find<Chip>("Filter_Action").Style);
        Assert.Equal(ChipStyle.Default, h.Find<Chip>("Filter_All").Style);
        h.Click(h.Find<Chip>("Filter_Racing"));
        Assert.Equal(["Racing (Mode 7)"], page.TemplateCards.Select(c => c.Title));
        Assert.Equal("Racing (Mode 7)", h.Find<TextBlock>("TemplateTitle").Text);
        h.Click(h.Find<Chip>("Filter_All"));
        var search = h.Find<SearchField>("LauncherSearch");
        search.Focus();
        h.Type("thai");
        Assert.Equal(["Visual Novel"], page.TemplateCards.Select(c => c.Title));
        search.Text = "";
        Capture.Pump();

        h.Click(page.TemplateCards.First(c => c.Title == "Turn-based RPG"));
        Assert.Equal("rpg", page.SelectedTemplate);
        Assert.Equal("Turn-based RPG", h.Find<TextBlock>("TemplateTitle").Text);
        Assert.Contains("Turn-based battles", h.Texts(h.Find<StackPanel>("TemplateIncludes")));
        h.Click(h.Find<AgbButton>("UseTemplate"));
        var dlg = Assert.IsType<NewProjectDialog>(h.Win.Overlays.Open.Single());
        Assert.Equal("rpg", dlg.Template.Id);
        Assert.True(dlg.Cards.Single(c => c.IsSelected).Title == "Turn-based RPG");
        Assert.Equal(("T-D01", "UseThisTemplate"), h.S.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public async Task P11_03_Learn_TutorialProgressDocumentationAndSamples()
    {
        using var h = new LauncherHarness("T-P21");
        LauncherFixture.TutorialProgress(h.Launcher);
        await LauncherHarness.Until(() => h.Texts(h.Find<Border>("TutorialRow_first-platformer")).Contains("40% · in progress"));
        Assert.Equal("Continue", (string)h.Find<AgbButton>("Tutorial_first-platformer").Content!);
        Assert.Equal(ButtonKind.Primary, h.Find<AgbButton>("Tutorial_first-platformer").Kind);
        Assert.Contains("20 min", h.Texts(h.Find<Border>("TutorialRow_csharp")));
        Assert.Equal("Start", (string)h.Find<AgbButton>("Tutorial_csharp").Content!);
        Assert.Contains("Done", h.Texts(h.Find<Border>("TutorialRow_palettes")));
        Assert.Equal("Review", (string)h.Find<AgbButton>("Tutorial_palettes").Content!);

        // Start opens Tutorial Steps (non-modal) and the row becomes "in progress"
        h.Click(h.Find<AgbButton>("Tutorial_csharp"));
        Assert.Contains(h.S.Windows.Requests, r => r is { Window: "T-O13", Source: "Learn_Start" });
        var steps = h.Find<TutorialStepsPopover>("TutorialSteps");
        Assert.Equal("Scripting in C# for GBA · 1 of 3", steps.Title);
        Assert.False(h.Win.Overlays.IsOpen);
        await LauncherHarness.Until(() => (string?)h.Find<AgbButton>("Tutorial_csharp").Content == "Continue");
        Assert.Contains("0% · in progress", h.Texts(h.Find<Border>("TutorialRow_csharp")));

        // Review restarts a finished tutorial from step 1
        h.Click(h.Find<AgbButton>("Tutorial_palettes"));
        Assert.Equal("Palettes & VRAM · 1 of 2", h.Find<TutorialStepsPopover>("TutorialSteps").Title);

        // documentation and community links
        h.Click(h.Find<Button>("Doc_Keyboard_shortcuts"));
        Assert.IsType<KeyboardShortcutsDialog>(h.Win.Overlays.Open.Single());
        h.Key(PhysicalKey.Escape);
        h.Click(h.Find<Button>("Doc_Scripting_API"));
        Assert.Contains(h.S.Windows.Requests, r => r is { Window: "T-P18", Source: "help.scriptingApi" });
        // the Scripting API page replaced the Launcher; back to Learn
        Assert.Equal("T-P18", h.Win.CurrentPageId);
        _ = h.S.Windows.Open("T-P21", source: "test");
        Capture.Pump();
        // Report a bug runs Help › Report a Bug…, which needs the bug page of editor.json (D-13)
        var urls = new List<string>();
        h.S.Set(new EditorLinks { Config = new EditorConfig { BugUrl = "https://bugs.example/new" }, Open = urls.Add, Fetch = (_, _) => throw new InvalidOperationException("no network in tests") });
        var ran = new List<string>();
        h.S.Commands.Executed += c => ran.Add(c.Id);
        h.Click(h.Find<Button>("Community_Report_a_bug"));
        Assert.Contains("help.reportBug", ran);
        Assert.Equal(["https://bugs.example/new"], urls);
        h.Click(h.Find<Button>("Community_Release_notes"));
        var notes = Assert.Single(h.Opened);
        Assert.Contains("## 0.1", File.ReadAllText(notes));
        h.Click(h.Find<Button>("Doc_GBA_hardware_guide"));
        Assert.Contains(h.S.Toasts.Visible, t => t.Text.Contains("hardware guide"));

        // a sample without content says so; Sky Hopper demo is made in the projects folder and opened
        h.Click(h.Find<SceneCard>("Sample_Cave_Quest"));
        await LauncherHarness.Until(() => h.S.Toasts.Visible.Any(t => t.Text == "Cave Quest is not installed."));
        h.Click(h.Find<SceneCard>("Sample_Sky_Hopper_demo"));
        await LauncherHarness.Until(() => h.S.Project.Current != null, 180000);
        Assert.Equal("Sky Hopper", h.S.Project.Name);
        Assert.StartsWith(Path.Combine(h.Fx.Folder, "GBAProjects", "SkyHopperdemo"), h.S.Project.Current!.Root);
        Assert.Equal("T-P01", h.Win.CurrentPageId);
    }

    [AvaloniaFact]
    public async Task P11_05_ProjectContextMenu_ActionsAndDeleteAsksFirst()
    {
        using var h = new LauncherHarness();
        var launched = new List<(string Exe, string Args)>();
        h.S.System = new SystemShell(() => h.Win, (exe, args) => launched.Add((exe, args)));
        var page = h.Page;
        var sky = page.ProjectCards[0];
        var skyPath = ((ProjectCard)sky.Tag!).Path;

        // the menu as Figma draws it
        var entries = ProjectContextMenu.Entries();
        var builder = new MenuBuilder(h.S.Commands, h.S.Shortcuts);
        var items = builder.BuildItems(entries).OfType<MenuItem>().ToList();
        Assert.Equal(["Open", "Show in Explorer", "Duplicate", "Rename", "Remove from list", "Delete project…"], items.Select(i => (string)i.Header!));
        Assert.Equal("Enter", AgbStudio.Editor.Theme.Shortcuts.Format(items[0].InputGesture!));
        Assert.Equal("F2", AgbStudio.Editor.Theme.Shortcuts.Format(items[3].InputGesture!));
        Assert.Contains("danger", items[5].Classes);

        // right-click selects the card and opens T-M17 at it
        h.Click(page.ProjectCards[2], MouseButton.Right);
        Assert.Equal("Snow Run", h.Launcher.Selected!.Name);
        Assert.Equal(("T-M17", "ProjectCard"), h.S.Windows.Requests[^1]);
        h.Key(PhysicalKey.Escape);
        h.Click(page.ProjectCards[0], MouseButton.Right);
        h.Key(PhysicalKey.Escape);
        Assert.True(h.S.Commands.Get("launcher.open").CanExecute());

        h.S.Commands.Execute("launcher.showInExplorer");
        Assert.Equal(("explorer.exe", $"/select,\"{skyPath}\""), launched.Single());

        // Duplicate: a copy next to it, listed first and selected
        h.S.Commands.Execute("launcher.duplicate");
        await LauncherHarness.Until(() => page.ProjectCards.Count == 5);
        Assert.Equal("Sky Hopper copy", page.ProjectCards[0].NameText);
        var copy = h.S.Recent.Items[0].Path;
        Assert.True(File.Exists(copy));
        Assert.Equal(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(skyPath))!, "SkyHoppercopy", "SkyHoppercopy.agbproj"), copy);
        Assert.Equal("Sky Hopper copy", GameProject.Load<AgbStudio.Core.Project.ProjectFile>(copy).Name);

        // Rename (F2): the name editor on the card, Enter renames the project
        h.Key(PhysicalKey.F2);
        var box = page.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "RenameBox" && t.IsVisible);
        Assert.Equal("Sky Hopper copy", box.Text);
        box.Text = "Sky Hopper 2";
        h.Key(PhysicalKey.Enter);
        await LauncherHarness.Until(() => page.ProjectCards[0].NameText == "Sky Hopper 2");
        Assert.Equal("Sky Hopper 2", GameProject.Load<AgbStudio.Core.Project.ProjectFile>(copy).Name);
        Assert.Equal("Sky Hopper 2", h.S.Recent.Items[0].Name);

        // Remove from list keeps the folder
        h.S.Commands.Execute("launcher.removeFromList");
        await LauncherHarness.Until(() => page.ProjectCards.Count == 4);
        Assert.True(File.Exists(copy));
        Assert.DoesNotContain(h.S.Recent.Items, r => r.Path == copy);

        // Delete project… asks first and says what is removed; Cancel keeps everything
        h.Click(page.ProjectCards[0]);
        Assert.Equal("Sky Hopper", h.Launcher.Selected!.Name);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(skyPath)!, "scenes", "Forest_01.scene"), "{}");
        h.S.Commands.Execute("launcher.deleteProject");
        Capture.Pump();
        var ask = Assert.IsType<DeleteProjectDialog>(h.Win.Overlays.Open.Single());
        var text = string.Join(" ", h.Texts(ask));
        Assert.Contains("Delete Sky Hopper?", text);
        Assert.Contains(Path.GetDirectoryName(skyPath)!, text);
        Assert.Contains("1 scene · 0 assets · 0 scripts", text);
        Assert.Contains("Recycle Bin", text);
        h.Click(ask.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Cancel"));
        Assert.True(File.Exists(skyPath));
        Assert.Contains(h.S.Recent.Items, r => r.Path == skyPath);
        h.S.Commands.Execute("launcher.deleteProject");
        Capture.Pump();
        ask = Assert.IsType<DeleteProjectDialog>(h.Win.Overlays.Open.Single());
        h.Click(ask.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Delete project"));
        await LauncherHarness.Until(() => !Directory.Exists(Path.GetDirectoryName(skyPath)));
        Assert.DoesNotContain(h.S.Recent.Items, r => r.Path == skyPath);
        await LauncherHarness.Until(() => page.ProjectCards.Count == 3);

        // Open (Enter) goes through Opening Project
        h.Click(page.ProjectCards[0]);
        h.Launcher.BeforeOpenStep = (_, ct) => Task.Delay(Timeout.Infinite, ct);
        h.Key(PhysicalKey.Enter);
        Assert.IsType<OpeningProjectDialog>(h.Win.Overlays.Open.Single());
    }

    [AvaloniaFact]
    public async Task P11_07_WhatsNew_ComesFromTheReleaseNotes()
    {
        var bundled = ReleaseNotes.Parse(ReleaseNotes.Bundled);
        Assert.Equal("0.1", bundled[0].Version);
        Assert.Equal(["Thai variable-width fonts", "Logic Graph ⇄ C# sync", "Flash to EZ-Flash / EverDrive"], bundled[0].Highlights);
        Assert.Equal(new Version(0, 1, 0, 0), EditorVersion.Value);
        Assert.Equal("v0.1", EditorVersion.Short);

        using (var h = new LauncherHarness())
            Assert.Equal(["• Thai variable-width fonts", "• Logic Graph ⇄ C# sync", "• Flash to EZ-Flash / EverDrive"], h.Texts(h.Find<StackPanel>("WhatsNew")));

        // a release-notes.md next to the editor replaces the bundled notes; a version newer than the editor is not shown yet
        using (var h = new LauncherHarness(startPage: null))
        {
            Directory.CreateDirectory(h.Launcher.ReleaseNotesFolder!);
            await File.WriteAllTextAsync(Path.Combine(h.Launcher.ReleaseNotesFolder!, ReleaseNotes.FileName),
                "# Notes\n\n## 9.0\n### Highlights\n- Not released yet\n\n## 0.1\n### Highlights\n- Recent projects in the Launcher\n- Tutorials that tick themselves\n\n### Other\n- More\n");
            h.Show("T-P12");
            Assert.Equal(["• Recent projects in the Launcher", "• Tutorials that tick themselves"], h.Texts(h.Find<StackPanel>("WhatsNew")));
        }
    }
}
