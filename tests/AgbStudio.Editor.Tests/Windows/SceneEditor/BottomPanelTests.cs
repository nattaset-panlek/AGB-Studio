using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>P11.14 Assets panel, P11.16 Console panel and P11.17 Animation panel in the bottom panel of the Scene Editor.</summary>
public sealed class BottomPanelTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    AssetsPanel Assets => _w.All<AssetsPanel>().Single();
    AssetCard Card(string name) => Assets.Cards.First(c => c.NameText == name);

    void OpenTab(string name)
    {
        _w.Click(_w.Find<PanelTab>("Tab_" + name));
        Capture.Pump();
    }

    [AvaloniaFact]
    public void Assets_CategoriesWithCountsAndCards()
    {
        var p = _w.S.Project.Current!;
        var counts = Assets.CategoryRows.ToDictionary(r => r.Label!, r => r.Meta);
        Assert.Equal(p.SpriteFiles().Count().ToString(), counts["Sprites"]);
        Assert.Equal((p.TilesetFiles().Count() + p.ImageFiles().Count()).ToString(), counts["Tilesets"]);
        Assert.Equal(p.MapFiles().Count().ToString(), counts["Maps"]);
        Assert.Equal("3", counts["Fonts"]);
        Assert.Equal("9", counts["Audio"]);
        Assert.Equal(p.ScriptFiles().Count().ToString(), counts["Scripts"]);
        Assert.Equal("6", counts["Data"]);
        // sprites in the order the game uses them; the Player's sheet is highlighted
        Assert.Equal(["hero.png", "slime.png", "block.png", "flag.png", "coin.png"], Assets.Cards.Take(5).Select(c => c.NameText));
        Assert.True(Card("hero.png").IsSelected);
        Assert.Equal("16×12 · 4 frames", Card("slime.png").Meta);
        // the category list filters the cards
        _w.Click(_w.Find<ListRow>("Category_Fonts"));
        Assert.Equal(["pixel8.font", "thai_vwf.font", "title_big.font"], Assets.Cards.Select(c => c.NameText));
        Assert.Equal("8×8 · 95 glyphs", Card("pixel8.font").Meta);
    }

    [AvaloniaFact]
    public void Assets_DoubleClickOpensTheRightEditor()
    {
        var req = _w.S.Windows.Requests;
        foreach (var (category, card, window) in new[]
                 {
                     ("Sprites", "hero.png", "T-P08"), ("Tilesets", "forest_tiles.png", "T-P03"), ("Maps", "Forest_01.map", "T-P03"),
                     ("Fonts", "pixel8.font", "T-P09"), ("Audio", "bgm_forest.xm", "T-P15"), ("Scripts", "PlayerController.cs", "T-P04"), ("Data", "Items.db", "T-P07"),
                 })
        {
            _w.Click(_w.Find<ListRow>("Category_" + category));
            _w.DoubleClick(Card(card));
            Assert.Equal((window, "AssetCard"), req[^1]);
            if (_w.Win.Overlays.IsOpen) _w.Key(PhysicalKey.Escape);
        }
        // dialogue files open the Dialogue Editor (T-P16)
        var dlg = Path.Combine(_w.S.Project.Current!.DialogueDir, "forest.dlg");
        File.WriteAllText(dlg, "{}");
        Assert.Equal("T-P16", AssetActions.EditorFor(dlg, AssetCategory.Scripts));
    }

    [AvaloniaFact]
    public void Assets_ContextMenuAndDragIntoTheScene()
    {
        _w.Click(Card("slime.png"), MouseButton.Right);
        Assert.Equal(("T-M13", "AssetCard"), _w.S.Windows.Requests[^1]);
        Assert.Equal("slime.png", AssetActions.Of(_w.M).Selected!.Name);
        Assert.Equal(AssetActions.Of(_w.M).Selected!.Path, _w.S.System.Selection!());
        _w.Key(PhysicalKey.Escape);
        // drag the coin card onto the viewport: a coin actor where it was dropped
        var count = _w.M.Scene!.Actors.Count;
        var screen = _w.Find<GbaScreenView>("GbaScreen");
        var drop = screen.TranslatePoint(screen.ToView(_w.M.Camera.X + 100, _w.M.Camera.Y + 60), _w.Win)!.Value;
        _w.Drag(_w.Center(Card("coin.png")), drop);
        Assert.Equal(count + 1, _w.M.Scene.Actors.Count);
        var a = _w.M.Scene.Actors[^1];
        Assert.Equal(("coin", ActorKind.Pickup, "spin"), (a.Sprite, a.Kind, a.Animation));
        Assert.Equal(_w.M.Scene.Actors.Count - 1, _w.M.SelectedActorIndex);
        Assert.Equal(count + 1, GameProject.Load<SceneFile>(_w.M.ScenePath!).Actors.Count);
    }

    [AvaloniaFact]
    public void Console_LinesFiltersCountsAndClickToCode()
    {
        var log = ConsoleLog.Of(_w.S);
        log.Clear();
        var file = Path.Combine(_w.Fx.Root, "scripts", "PlayerController.cs");
        var result = new BuildResult { Success = true, RomPath = Path.Combine(_w.Fx.Root, "build", "sky_hopper.gba"), Elapsed = TimeSpan.FromSeconds(0.8) };
        result.Diagnostics.Add(new Diagnostic(Severity.Warning, file, 9, 5, "GAE003", "float is slow on the GBA; use fixed"));
        result.Diagnostics.Add(new Diagnostic(Severity.Error, file, 19, 9, "", "NullReference — target was destroyed"));
        log.AddBuild(result);
        log.Info("Debug.Log: jumpPower = 3.50");
        OpenTab("Console");
        Assert.Equal(4, _w.Workspace.ConsoleCount);
        var panel = _w.All<ConsolePanel>().Single();
        Assert.Equal(["PlayerController.cs(9): GAE003 float is slow on the GBA; use fixed", "PlayerController.cs(19): NullReference — target was destroyed",
            "Build OK in 0.80 s · sky_hopper.gba 0 KB", "Debug.Log: jumpPower = 3.50"], panel.Visible.Select(e => e.Text));
        Assert.Equal("Warnings · 1", _w.Find<Chip>("Filter_Warnings").Content);
        Assert.Equal("Errors · 1", _w.Find<Chip>("Filter_Errors").Content);
        // the filters sit on the tab row, right aligned (Figma)
        var row = _w.Find<PanelTab>("Tab_Console").GetVisualAncestors().OfType<Border>().First(b => b.Height == 33);
        Assert.Same(panel.Filters, AdornerLayer.GetAdorner(row));
        void Press(string chip) { _w.Find<Chip>(chip).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Capture.Pump(); }
        Press("Filter_Errors");
        Assert.Equal(["PlayerController.cs(19): NullReference — target was destroyed"], panel.Visible.Select(e => e.Text));
        // clicking a line with a file opens the Script Editor at that line
        var line = _w.All<ConsoleLine>().Single();
        _w.Click(line);
        Assert.Equal(("T-P04", "ConsoleLine"), _w.S.Windows.Requests[^1]);
        _w.Key(PhysicalKey.Escape);
        Press("Filter_Warnings");
        Assert.Single(panel.Visible);
        Press("Filter_All");
        Assert.Equal(4, panel.Visible.Count);
    }

    [AvaloniaFact]
    public void Console_TakesBuildResultsFromTheBuildService()
    {
        var log = ConsoleLog.Of(_w.S);
        var before = log.Entries.Count;
        var r = new BuildResult { Success = false, FailedStep = GameBuilder.StepCompile };
        r.Diagnostics.Add(new Diagnostic(Severity.Error, "", 0, 0, "", "devkitARM build failed"));
        log.AddBuild(r);
        Assert.Equal(before + 2, log.Entries.Count);
        Assert.Equal("Build failed at Compile (devkitARM g++ -O2)", log.Entries[^1].Text);
        Assert.Equal(ConsoleLevel.Error, log.Entries[^1].Level);
    }

    [AvaloniaFact]
    public void Animation_ListFramesPlayStepAndOnionSkin()
    {
        OpenTab("Animation");
        var panel = _w.All<AnimationPanel>().Single();
        var list = _w.Find<StackPanel>("AnimationList").Children.OfType<ListRow>().ToList();
        Assert.Equal(["idle", "run", "jump", "hurt"], list.Select(r => r.Label));
        Assert.Equal(_w.M.Assets!.SpriteMeta("hero").Animations.Select(a => $"{a.Frames.Count}f" + (a.Loop ? " · loop" : "")), list.Select(r => r.Meta));
        Assert.True(list[0].IsSelected);
        var idle = _w.M.Assets.SpriteMeta("hero").Animations[0];
        var frames = _w.Find<StackPanel>("AnimationFrames").GetVisualDescendants().OfType<FrameThumb>().ToList();
        Assert.Equal(idle.Frames.Count, frames.Count);
        Assert.Equal($"1 · {idle.Ticks}t", frames[0].Label);
        _w.Click(list[1]);
        Assert.Equal("run", panel.Current!.Name);
        Assert.Equal(_w.M.Assets.SpriteMeta("hero").Animations[1].Frames.Count, _w.Find<StackPanel>("AnimationFrames").Children.Count);
        _w.Click(_w.Find<IconButton>("AnimStep"));
        Assert.Equal(1, panel.FrameIndex);
        Assert.True(_w.Find<StackPanel>("AnimationFrames").GetVisualDescendants().OfType<FrameThumb>().ElementAt(1).IsSelected);
        _w.Click(_w.Find<IconButton>("AnimPlay"));
        Assert.True(panel.Playing);
        _w.Click(_w.Find<IconButton>("AnimPlay"));
        Assert.False(panel.Playing);
        _w.Click(_w.Find<Chip>("OnionSkin"));
        Assert.True(panel.OnionSkin);
        Assert.Equal(ChipStyle.Accent, _w.Find<Chip>("OnionSkin").Style);
        // selecting another actor shows its sprite's animations
        _w.M.SelectActor(1);
        Capture.Pump();
        Assert.Equal(["idle"], _w.Find<StackPanel>("AnimationList").Children.OfType<ListRow>().Select(r => r.Label));
    }
}
