using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.SceneEditor;
using AgbStudio.Editor.Windows.Launcher;
using AgbStudio.Editor.Windows.Palette;
using AgbStudio.Editor.Windows.SceneEditor;
using AgbStudio.Editor.Windows.Scripts;
using AgbStudio.Editor.Windows.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>
/// P11.62 / P10.07: the Preferences settings that were stored only now act on the editor: Appearance (T-S06) reduce
/// motion and pixel grid, Editor (T-S07) default snap, collision overlay and GBA budget in the Inspector, Paths (T-S08)
/// projects folder, image editor and Tiled, and Browse… (T-Y04, P10.17) through the file picker.
/// </summary>
public sealed class PreferenceEffectsTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    GbaScreenView Screen => _w.Find<GbaScreenView>("GbaScreen");

    [AvaloniaFact]
    public void Appearance_PixelGridAt400Percent_AndReduceMotion()
    {
        // 300%: no pixel grid; 400%: the grid between game pixels; off in Preferences: none
        Assert.False(Screen.PixelGridShown);
        _w.M.SetZoom(400);
        Capture.Pump();
        Assert.True(Screen.PixelGridShown);
        _w.S.Preferences.Update(p => p.ShowPixelGrid = false);
        Assert.False(Screen.PixelGridShown);
        _w.S.Preferences.Update(p => { p.ShowPixelGrid = true; p.PixelGridMinZoom = 500; });
        Assert.False(Screen.PixelGridShown);
        _w.M.SetZoom(500);
        Assert.True(Screen.PixelGridShown);
        // the Tilemap Editor and the Sprite Editor follow the same rule
        Assert.True(_w.S.Preferences.Current.PixelGridAt(5));
        Assert.False(_w.S.Preferences.Current.PixelGridAt(4));

        // Reduce motion: the Palette Effect preview does not play by itself
        _ = _w.S.Windows.Open("T-O10", EffectKind.Cycle, source: "test");
        Capture.Pump();
        var fx = Assert.IsType<PaletteEffectPopover>(_w.Win.Overlays.Open[^1]);
        Assert.True(fx.PreviewPlaying);
        _w.Key(PhysicalKey.Escape);
        _w.S.Preferences.Update(p => p.ReduceMotion = true);
        _ = _w.S.Windows.Open("T-O10", EffectKind.Cycle, source: "test");
        Capture.Pump();
        fx = Assert.IsType<PaletteEffectPopover>(_w.Win.Overlays.Open[^1]);
        Assert.False(fx.PreviewPlaying);
        _w.Key(PhysicalKey.Escape);
    }

    [AvaloniaFact]
    public void Editor_DefaultSnapCollisionOverlayAndBudgetInInspector()
    {
        // GBA budget in the Inspector
        Assert.True(_w.Find<Border>("BudgetSection").IsVisible);
        _w.S.Preferences.Update(p => p.ShowBudgetInInspector = false);
        Assert.False(_w.Find<Border>("BudgetSection").IsVisible);
        _w.S.Preferences.Update(p => p.ShowBudgetInInspector = true);
        Assert.True(_w.Find<Border>("BudgetSection").IsVisible);

        // collision overlay: Grid & Snap › Tile collision draws only while the preference allows it
        _w.M.Grid.TileCollision = true;
        Assert.True(_w.M.CollisionOverlayAllowed);
        _w.S.Preferences.Update(p => p.ShowCollisionOverlay = false);
        Assert.False(_w.M.CollisionOverlayAllowed);
        _w.Click(_w.Find<Chip>("GridChip"));
        var grid = Assert.IsType<GridSnapPopover>(_w.Win.Overlays.Open.Single());
        Assert.False(grid.TileCollision.IsEnabled);
        _w.Key(PhysicalKey.Escape);

        // default snap: the grid a project opens with
        _w.S.Preferences.Update(p => p.DefaultSnap = 8);
        _w.S.Project.Open(_w.S.Project.Current!.Root);
        Capture.Pump();
        Assert.Equal((8, true), (_w.M.Grid.Size, _w.M.Grid.Snap));
        Assert.Equal("Grid 8", _w.Find<Chip>("GridChip").Content);
    }

    [AvaloniaFact]
    public void Paths_ProjectsFolderImageEditorTiled_AndBrowse()
    {
        var s = _w.S;
        // projects folder: where New Project puts a project
        var projects = Path.Combine(_w.Fx.Fx.Folder, "MyGames");
        s.Preferences.Update(p => p.ProjectsFolder = projects);
        Assert.Equal(projects, LauncherService.Of(s).DefaultLocationBase());

        // image editor and Tiled: a fake program file, the launch is recorded instead of started
        var tools = Directory.CreateDirectory(Path.Combine(_w.Fx.Fx.Folder, "tools")).FullName;
        var aseprite = Path.Combine(tools, "Aseprite.exe");
        var tiled = Path.Combine(tools, "tiled.exe");
        File.WriteAllText(aseprite, "");
        File.WriteAllText(tiled, "");
        var launched = new List<(string Exe, string Args)>();
        ExternalEditorService.Of(s).Launch = (exe, args) => launched.Add((exe, args));
        var p = s.Project.Current!;
        var background = p.ImageFiles().First();
        var tmx = Path.Combine(p.MapsDir, "Forest_01.tmx");
        File.WriteAllText(tmx, "<map/>");
        // not set: nothing is launched, the files open as before
        Assert.False(ExternalTools.TryOpen(s, background));
        s.Preferences.Update(x => { x.ImageEditorPath = aseprite; x.TiledPath = tiled; });
        Assert.Equal(aseprite, ExternalTools.ProgramFor(s, background));
        Assert.Equal(tiled, ExternalTools.ProgramFor(s, tmx));
        Assert.Null(ExternalTools.ProgramFor(s, p.SpriteFiles().First()));   // sprites stay with the Sprite Editor
        // double-click on the background image card in the Assets panel
        var assets = _w.All<AssetsPanel>().Single();
        assets.Category = AssetCategory.Tilesets;
        Capture.Pump();
        var card = assets.Cards.First(c => c.Tag is AssetItem a && Path.GetFullPath(a.Path) == Path.GetFullPath(background));
        _w.DoubleClick(card);
        Assert.Contains(launched, l => l.Exe == aseprite && l.Args.Contains(Path.GetFileName(background)));
        // a .tmx from the Command Palette opens in Tiled
        _w.Key(PhysicalKey.K, RawInputModifiers.Control);
        var palette = Assert.IsType<AgbStudio.Editor.Shell.CommandPalette>(_w.Win.Overlays.Open.Single());
        palette.Query.Focus();
        _w.Type("Forest_01.tmx");
        _w.Key(PhysicalKey.Enter);
        Assert.Contains(launched, l => l.Exe == tiled && l.Args.Contains("Forest_01.tmx"));

        // T-Y04: Preferences › Paths › Browse… takes the picked file
        s.System.FilePicker = (_, _) => Task.FromResult<string?>(tiled);
        s.Commands.Execute("file.preferences");
        Capture.Pump();
        var d = Assert.IsType<PreferencesDialog>(_w.Win.Overlays.Open.Last());
        _w.Click(d.Find<NavItem>("Nav_T-S08"));
        s.Preferences.Update(x => x.TiledPath = "");
        _w.Click(d.Find<AgbButton>("TiledPath_Browse"));
        Assert.Equal(tiled, d.Find<TextField>("TiledPath").Text);
        Assert.Equal("check", ((AgbStudio.Editor.Graphics.Icon)d.Find<ContentControl>("TiledPath_Status").Content!).Glyph);
    }
}
