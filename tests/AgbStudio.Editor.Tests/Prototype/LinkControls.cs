using AgbStudio.Editor.Controls;
using Avalonia.Controls;

namespace AgbStudio.Editor.Tests.Prototype;

public enum Gesture { Click, DoubleClick }

/// <summary>
/// The app's control for the node a Figma link sits on, and how a user triggers it.
/// <paramref name="Prepare"/> runs first (pick an Assets category, open a title bar menu); <paramref name="Differs"/>
/// says where the app's control is not the node Figma links (the test checks both).
/// </summary>
public sealed record AppControl(string Name, Func<AppSession, Control> Find, Gesture Gesture = Gesture.Click, Action<AppSession>? Prepare = null, string? Differs = null);

/// <summary>
/// Figma node path → app control, for every link of the prototype (T-X03). Nodes are matched by their path inside the
/// source frame and the destination; the app controls by their stable names. A link with no entry here fails the test.
/// </summary>
public static class LinkControls
{
    static AppControl Named<T>(string name, Gesture g = Gesture.Click, Action<AppSession>? prepare = null) where T : Control =>
        new(name, s => s.Named<T>(name), g, prepare);

    /// <summary>An Assets panel card: pick its category, then double-click the card (Editor Map #24: "Asset (double-click)").</summary>
    static AppControl AssetCard(string category, string card) =>
        new($"Assets › {category} › {card} (double-click)", s => s.Named<AssetCard>("Card_" + card), Gesture.DoubleClick,
            s => s.Click(s.Named<ListRow>("Category_" + category)));

    /// <summary>A title bar menu item: click the menu, then the item in its popup.</summary>
    static AppControl MenuItem(string menu, string item) =>
        new($"{menu} menu › {item}", s => s.TitleMenu(menu).Items.OfType<MenuItem>().First(m => (string?)m.Header == item), Gesture.Click,
            s => { s.Click(s.TitleMenu(menu)); Assert.True(s.TitleMenu(menu).IsSubMenuOpen, $"the {menu} menu did not open"); });

    const string SceneMenuDiffers =
        "Figma links the Scene menu title of the title bar back to the Scene Editor; in AGB Studio that title opens the " +
        "Scene menu (T-M04, as Editor Map board 2 draws it), and the Scene document tab leads back to the Scene Editor.";

    const string BuildMenuDiffers =
        "Figma links the Build menu title of the Scene Editor straight to Build ROM (T-D06); in AGB Studio that title opens the " +
        "Build menu (T-M05, as Editor Map board 2 draws it), and Build ROM is its first item.";

    /// <summary>The Launcher sidebar item that opens a window.</summary>
    static string NavLabel(string? to) => to switch
    {
        "T-P12" => "Projects", "T-P20" => "Templates", "T-P21" => "Learn", "T-D09" => "Settings",
        _ => throw new ArgumentOutOfRangeException(nameof(to), to, "no Launcher item opens it"),
    };

    public static AppControl? For(ProtoLink l) => (l.FromId, l.Node, l.ToId) switch
    {
        // every dialog and popover: the header's x, the footer's Cancel
        (_, "Dialog Header / Close", null) => new("Dialog Header › x", s => s.PartOfTop<DialogHeader>("PART_Close")),
        (_, "Dialog Footer / Cancel", null) => new("Dialog Footer › Cancel", FooterCancel),
        (_, "Popover Header / Close", null) => new("Popover Header › x", s => s.PartOfTop<PopoverHeader>("PART_Close")),

        // Launcher, Launcher · Templates, Launcher · Learn (T-P12, T-P20, T-P21)
        ("T-P12", "row / Button", "T-D01") => Named<AgbButton>("LauncherNewProject"),
        ("T-P12" or "T-P20" or "T-P21", "row / column / Nav Item", var to) => Named<NavItem>("Nav_" + NavLabel(to)),
        ("T-P20", "row / column / Button", "T-D01") => Named<AgbButton>("UseTemplate"),

        // Scene Editor (T-P01)
        ("T-P01", "Title Bar / Menus / Build", "T-D06") => MenuItem("Build", "Build ROM") with { Differs = BuildMenuDiffers },
        ("T-P01", "Title Bar / Actions / Debug", "T-P19") => Named<AgbButton>("PART_Debug"),
        ("T-P01", "Toolbar / Play / Play Control", "T-P02") => Named<PlayControl>("PlayButton"),
        ("T-P01", "Toolbar / Chip", "T-O02") => Named<Chip>("VideoModeChip"),
        ("T-P01", "Body / column / Doc Tabs / Doc Tab", "T-P02") => Named<DocTab>("Doc_Game"),
        ("T-P01", "Body / column / Doc Tabs / Doc Tab", "T-P03") => Named<DocTab>("Doc_Forest_01.map"),
        ("T-P01", "Body / column / Doc Tabs / Doc Tab", "T-P04") => Named<DocTab>("Doc_PlayerController.cs"),
        ("T-P01", "Body / column / Viewport / overlay / Chip", "T-D03") => Named<Chip>("SceneSizeChip"),
        ("T-P01", "Body / column / Viewport / overlay / Chip", "T-O03") => Named<Chip>("GridChip"),
        ("T-P01", "Body / column / column / Panel Tabs / Panel Tab", "T-P07") => Named<PanelTab>("Tab_Database"),
        ("T-P01", "Body / column / column / row / Asset Grid / Asset Card", "T-P08") => AssetCard("Sprites", "hero.png"),
        ("T-P01", "Body / column / column / row / Asset Grid / Asset Card", "T-P03") => AssetCard("Tilesets", "forest_tiles.png"),
        ("T-P01", "Body / column / column / row / Asset Grid / Asset Card", "T-P09") => AssetCard("Fonts", "pixel8.font"),
        ("T-P01", "Body / Inspector / Sprite / Field", "T-O04") =>
            new("Inspector › Sprite › Sheet dropdown", s => s.Named<InspectorField>("Field_Sheet").GetLogicalOrVisualButton("PART_Dropdown")),
        ("T-P01", "Body / Inspector / column / Button", "T-O05") => Named<Button>("AddComponentButton"),
        ("T-P01", "Body / Inspector / GBA Budget", "T-O06") => Named<Button>("BudgetButton"),
        ("T-P01", "Status Bar / Status Item", "T-P11") => Named<StatusItem>("Status_Build"),
        ("T-P01", "Status Bar / Status Item", "T-D09") => Named<StatusItem>("Status_Emulator"),

        // Play Mode (T-P02): Stop, and back to the Scene Editor
        ("T-P02", "Toolbar / Play / Play Control", "T-P01") => Named<PlayControl>("StopButton"),

        // Script Editor, Sprite Editor, Tilemap Editor, Play Mode: the Scene menu title in Figma, the Scene tab in the app
        ("T-P02" or "T-P03" or "T-P04" or "T-P08", "Title Bar / Menus / Scene", "T-P01") =>
            new("Scene document tab", s => s.Named<DocTab>("Doc_Scene"), Gesture.Click, s =>
            {
                // the node Figma links: the Scene menu title opens the Scene menu and stays on the page
                var page = s.Win.CurrentPageId;
                s.Click(s.TitleMenu("Scene"));
                Assert.True(s.TitleMenu("Scene").IsSubMenuOpen, "the Scene menu did not open");
                Assert.Equal(page, s.Win.CurrentPageId);
                s.Win.MenuBar.Close();
                AppSession.Pump();
            }, SceneMenuDiffers),
        ("T-P04", "Body / Script / API quick reference / Button", "T-P18") => Named<AgbButton>("OpenScriptingApi"),

        // World Map, Font Manager, Database, Build Report
        ("T-P13", "Toolbar / Button", "T-D02") => Named<AgbButton>("NewScene"),
        ("T-P09", "Body / Fonts / column / Button", "T-D16") => Named<Button>("AddCharacters"),
        ("T-P07", "Body / column / row / Button", "T-D19") => new("Import CSV (then the CSV file in the system file dialog)",
            s => s.Named<AgbButton>("ImportCsvButton"), Gesture.Click, s =>
            {
                // Windows' Open dialog (checklist 4.11, no visual check) answers with a CSV file
                var csv = AgbStudio.Editor.Tests.Windows.Database.DataFixture.WriteItemsCsv(s.Fx.Folder);
                AgbStudio.Editor.Windows.Database.FileChooser.For(s.S).OpenOverride = (_, _) => Task.FromResult<string?>(csv);
            }),
        ("T-P11", "Toolbar / Button", "T-D06") => Named<AgbButton>("BuildAgain"),
        ("T-P11", "Body / Cartridge header / Export / column / Button", "T-P02") => Named<AgbButton>("RunInMgba"),
        ("T-P11", "Body / Cartridge header / Export / column / Button", "T-D07") => Named<AgbButton>("FlashToCartridge"),
        _ => null,
    };

    /// <summary>
    /// The footer's Cancel. About and Keyboard Shortcuts show one footer button, "Close", in Figma and in the app (the
    /// Figma footer's Cancel instance, where the link sits, is hidden): that button is the one that closes.
    /// </summary>
    static Control FooterCancel(AppSession s)
    {
        var cancel = s.PartOfTop<DialogFooter>("PART_Cancel");
        if (cancel.IsEffectivelyVisible) return cancel;
        var close = s.PartOfTop<DialogFooter>("PART_Confirm");
        Assert.Equal("Close", close.Content as string);
        return close;
    }

    static Button GetLogicalOrVisualButton(this Control c, string name) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<Button>().FirstOrDefault(b => b.Name == name)
        ?? throw new Xunit.Sdk.XunitException($"{c.Name} has no {name}");
}
