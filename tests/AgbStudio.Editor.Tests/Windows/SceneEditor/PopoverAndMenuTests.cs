using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>P11.19 popovers (Video Mode, Grid &amp; Snap, Asset Picker, Component Picker, Budget Details) and P11.20 menus
/// (Add Actor, Add to Scene, Actor Context Menu, Asset Context Menu, Component Menu, Zoom, Animation List): every item.</summary>
public sealed class PopoverAndMenuTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    SceneEditorModel M => _w.M;
    SceneFile OnDisk => GameProject.Load<SceneFile>(M.ScenePath!);
    WindowContext Ctx(string id, object? args = null) => new() { Services = _w.S, Info = _w.S.Windows.Info(id), Args = args };

    /// <summary>Runs a menu entry as a click on it does.</summary>
    void Run(MenuEntry e)
    {
        switch (e)
        {
            case CommandEntry c: _w.S.Commands.Execute(c.CommandId); break;
            case RecentEntry r: r.Open.Execute(null); break;
            default: throw new InvalidOperationException(e.ToString());
        }
        Capture.Pump();
    }

    static IReadOnlyList<string> Labels(IReadOnlyList<MenuEntry> entries, CommandRegistry c) => entries.Select(e => e switch
    {
        CommandEntry ce => ce.Label ?? c.Get(ce.CommandId).Title,
        RecentEntry r => r.Name,
        SubmenuEntry s => s.Title,
        _ => "-",
    }).ToList();

    [AvaloniaFact]
    public void VideoMode_ModesAlphaAndMosaic()
    {
        _w.Click(_w.Find<Chip>("VideoModeChip"));
        var p = Assert.IsType<VideoModePopover>(_w.Win.Overlays.Open.Single());
        Assert.Equal(4, VideoModePopover.Modes.Count);
        p.Choose(3);
        Assert.Equal(3, OnDisk.VideoMode);
        _w.Click(p.Alpha);
        Assert.True(OnDisk.AlphaBlending);
        _w.Click(p.Mosaic);
        Assert.True(OnDisk.Mosaic);
        _w.S.Commands.Execute("edit.undo");
        _w.S.Commands.Execute("edit.undo");
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal((0, false, false), (OnDisk.VideoMode, OnDisk.AlphaBlending, OnDisk.Mosaic));
    }

    [AvaloniaFact]
    public void GridSnap_SizeSwitchesAndColour()
    {
        _w.Click(_w.Find<Chip>("GridChip"));
        var p = Assert.IsType<GridSnapPopover>(_w.Win.Overlays.Open.Single());
        _w.Click(p.GetVisualDescendants().OfType<Segment>().First(s => s.Name == "Size_8"));
        Assert.Equal(8, M.Grid.Size);
        Assert.Equal("Grid 8", _w.Find<Chip>("GridChip").Content);
        Assert.Equal(48, M.SnapValue(45));
        _w.Click(p.Snap);
        Assert.False(M.Grid.Snap);
        Assert.Equal(45, M.SnapValue(45));
        _w.Click(p.ShowGrid);
        Assert.False(M.Grid.ShowGrid);
        _w.Click(p.TileCollision);
        Assert.True(M.Grid.TileCollision);
        _w.Click(p.CameraBounds);
        Assert.False(M.Grid.CameraBounds);
        _w.Click(p.GetVisualDescendants().OfType<Border>().First(b => b.Name == "GridColour_2"));
        Assert.Equal(2, M.Grid.Colour);
        p.SelectSize(32);
        Assert.Equal(32, M.Grid.Size);
    }

    [AvaloniaFact]
    public void AssetPicker_SearchTabsAndPick()
    {
        var picker = new AssetPicker(_w.S, "Tilesets");
        Assert.Equal(["forest_tiles.png", "cave_tiles.png", "Platform_tiles.png"], picker.Items.Select(Path.GetFileName));
        picker.Tab = "Fonts";
        Assert.Equal(3, picker.Items.Count);
        picker.Tab = "Sprites";
        picker.Search.Text = "sli";
        Capture.Pump();
        Assert.Equal(["slime.png", "slime_king.png"], picker.Items.Select(Path.GetFileName));
        object? result = null;
        picker.Closed += r => result = r;
        picker.Pick(picker.Items[1]);
        Assert.EndsWith("slime_king.png", (string)result!);
        // Add to Scene › Prefab…: Make Prefab writes prefabs/<name>.prefab, the picker adds it to the scene
        M.SelectActor(1);
        _w.S.Commands.Execute("actor.makePrefab");
        Assert.True(File.Exists(Path.Combine(_w.Fx.Root, "prefabs", "Slime.prefab")));
        Assert.Equal("Slime", OnDisk.Actors[1].Prefab);
        _w.S.Commands.Execute("scene.addPrefab");
        Capture.Pump();
        var prefabs = Assert.IsType<AssetPicker>(_w.Win.Overlays.Open.Single());
        var count = M.Scene!.Actors.Count;
        prefabs.Pick(prefabs.Items.Single());
        Assert.Equal(count + 1, OnDisk.Actors.Count);
        Assert.Equal(("Slime (2)", "slime", "Slime"), (M.Scene.Actors[^1].Name, M.Scene.Actors[^1].Sprite, M.Scene.Actors[^1].Prefab));
    }

    [AvaloniaFact]
    public void ComponentPicker_CategoriesSearchKeyboardAndNewScript()
    {
        M.SelectActor(3);   // Question Block
        _w.S.Commands.Execute("actor.addBehaviour");
        Capture.Pump();
        var p = Assert.IsType<ComponentPicker>(_w.Win.Overlays.Open.Single());
        Assert.Equal(["Platformer Body", "Top-down Mover", "Projectile", "Health", "Damage on Touch", "Patrol", "Chase Player", "Sound on Event"],
            p.Items.Where(b => !b.Script).Select(b => b.Title));
        Assert.Contains(p.Items, b => b.Script && b.Name == "PlayerController");
        _w.Type("chase");
        Assert.Equal(["Chase Player"], p.Items.Select(b => b.Title));
        _w.Key(PhysicalKey.Enter);
        Assert.False(_w.Win.Overlays.IsOpen);
        Assert.Equal("ChasePlayer", OnDisk.Actors[3].Components[^1].Type);
        _w.S.Commands.Execute("actor.addBehaviour");
        Capture.Pump();
        p = (ComponentPicker)_w.Win.Overlays.Open.Single();
        _w.Key(PhysicalKey.ArrowDown);
        Assert.Equal(1, p.Highlight);
        _w.Click(p.GetVisualDescendants().OfType<Button>().First(b => b.Name == "NewScriptButton"));
        Assert.Equal(("T-D14", "ComponentPickerNewScript"), _w.S.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public void BudgetDetails_LiveNumbersHintAndBuildReport()
    {
        var d = new BudgetDetails(_w.S);
        var labels = d.Budget.Meters.Select(m => m.Label).ToList();
        Assert.Equal(["VRAM BG", "VRAM OBJ", "OAM", "BG palettes", "OBJ palettes", "Palette OBJ 0", "EWRAM", "IWRAM", "ROM"], labels);
        var assets = M.Assets!;
        var sprites = M.Scene!.Actors.Where(a => a.Sprite.Length > 0).Select(a => a.Sprite).Distinct().ToList();
        Assert.Equal($"{sprites.Count} / 16", d.Budget["OBJ palettes"].Text);
        Assert.Equal(sprites.Sum(s => assets.Sprite(s)!.FrameCount * assets.Sprite(s)!.TilesPerFrame * 32), (int)d.Budget["VRAM OBJ"].Used);
        Assert.Equal("not built", d.Budget["ROM"].Text);
        Assert.NotNull(d.Budget.Hint);
        Assert.Matches(@"^\w+\.png and \w+\.png could share OBJ palette \d+ — saves one palette slot\.$", d.Budget.Hint!);
        // with a build report the RAM and ROM meters show its numbers
        var report = new BuildReport { RomBytes = 412 * 1024, EwramBytes = 41 * 1024, IwramBytes = 9 * 1024 };
        Directory.CreateDirectory(M.Project!.BuildDir);
        report.Save(M.Project.BuildDir);
        var d2 = new BudgetDetails(_w.S);
        Assert.Equal(("41 / 256 KB", "9 / 32 KB", "412 KB / 32 MB"), (d2.Budget["EWRAM"].Text, d2.Budget["IWRAM"].Text, d2.Budget["ROM"].Text));
        var host = Capture.Host(d2, 420, 398);
        object? closed = "open";
        d2.Closed += r => closed = r;
        var button = d2.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "OpenBuildReport");
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Null(closed);
        Assert.Equal(("T-P11", "BudgetDetails"), _w.S.Windows.Requests[^1]);
        host.Close();
    }

    [AvaloniaFact]
    public void AddActorAndAddToScene_EveryItem()
    {
        var c = _w.S.Commands;
        Assert.Equal(["Empty Actor", "Player", "Enemy", "Pickup", "Trigger Zone", "Camera Zone", "Text Label"], Labels(MenuModel.AddActor(), c));
        var kinds = new[] { ActorKind.Empty, ActorKind.Player, ActorKind.Enemy, ActorKind.Pickup, ActorKind.TriggerZone, ActorKind.CameraZone, ActorKind.TextLabel };
        var entries = MenuModel.AddActor();
        for (var i = 0; i < entries.Count; i++)
        {
            var count = M.Scene!.Actors.Count;
            Run(entries[i]);
            Assert.Equal(count + 1, OnDisk.Actors.Count);
            Assert.Equal(kinds[i], M.Scene.Actors[^1].Kind);
            Assert.Equal(M.Scene.Actors.Count - 1, M.SelectedActorIndex);
        }
        Assert.Equal("hero", M.Scene!.Actors.Last(a => a.Kind == ActorKind.Player).Sprite);   // the project's player sheet
        Assert.Equal("Player (1)", M.Scene.Actors.Last(a => a.Kind == ActorKind.Player).Name);

        var add = SceneCommands.AddToScene(Ctx("T-M11"));
        Assert.Equal(["Empty Actor", "Player", "Enemy", "UI", "Pickup", "Trigger Zone", "Camera Zone", "Text Label", "-", "Tilemap Layer", "Prefab…"], Labels(add, c));
        // UI opens a submenu: Canvas, Panel, Text Label, Button, Image
        var ui = ((SubmenuEntry)add[3]).Entries();
        Assert.Equal(["Canvas (Root Overlay)", "Panel (Window Box)", "Text Label", "Button", "Image (UI Graphic)"], Labels(ui, c));
        Run(ui[0]); // Add Canvas
        Assert.Equal(ActorKind.Canvas, M.Scene.Actors[^1].Kind);
        // Enemy opens a submenu: a plain enemy and the enemy sheets of the project
        var enemies = ((SubmenuEntry)add[2]).Entries();
        Assert.Equal(["Enemy", "bat.png", "slime.png", "slime_king.png"], Labels(enemies, c));
        Run(enemies[1]);
        Assert.Equal(("bat", ActorKind.Enemy, "Bat"), (M.Scene.Actors[^1].Sprite, M.Scene.Actors[^1].Kind, M.Scene.Actors[^1].Name));
        // Tilemap Layer: BG3 (free) added to the map, selected
        Run(add[9]);
        var map = GameProject.Load<MapFile>(M.MapPath!);
        Assert.Equal(4, map.Layers.Count);
        Assert.Equal(3, map.Layers[3].Bg);
        Assert.Equal(new SceneItem(SceneItemKind.Layer, 3), M.Selection);
        Run(add[10]);
        Assert.Equal(("T-O04", "AddToScenePrefab"), _w.S.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public void ActorContextMenu_EveryItem()
    {
        var c = _w.S.Commands;
        M.SelectActor(0);
        var menu = SceneCommands.ActorContextMenu(Ctx("T-M12"));
        Assert.Equal(["Rename", "Duplicate", "Delete", "-", "UI", "-", "Add Behaviour…", "Edit Script", "Make Prefab", "-", "Focus in View"], Labels(menu, c));
        Assert.Equal("F2", AgbStudio.Editor.Theme.Shortcuts.Format(_w.S.Shortcuts.GestureOf("scene.rename")!));
        var count = M.Scene!.Actors.Count;
        Run(menu[1]);                                               // Duplicate
        Assert.Equal(count + 1, OnDisk.Actors.Count);
        Run(menu[2]);                                               // Delete (the duplicate is selected)
        Assert.Equal(count, OnDisk.Actors.Count);
        M.SelectActor(0);
        Run(menu[0]);                                               // Rename: inline in the Hierarchy
        Assert.NotNull(_w.All<HierarchyPanel>().Single().RenameBox);
        _w.Key(PhysicalKey.Escape);
        Run(menu[6]);                                               // Add Behaviour…
        Assert.IsType<ComponentPicker>(_w.Win.Overlays.Open.Single());
        _w.Key(PhysicalKey.Escape);
        Run(menu[7]);                                               // Edit Script: PlayerController.cs
        Assert.Equal(("T-P04", "actor.editScript"), _w.S.Windows.Requests[^1]);
        if (_w.Win.Overlays.IsOpen) _w.Key(PhysicalKey.Escape);
        Run(menu[8]);                                               // Make Prefab
        Assert.True(File.Exists(Path.Combine(_w.Fx.Root, "prefabs", "Player.prefab")));
        M.SelectActor(M.Scene.Actors.FindIndex(a => a.Name == "Goal Flag"));
        var menuFlag = SceneCommands.ActorContextMenu(Ctx("T-M12"));
        Run(menuFlag[10]);                                          // Focus in View: the flag at the centre of the screen
        Assert.Equal((224 - 120, 288 - 80), M.Camera);
    }

    [AvaloniaFact]
    public void AssetContextMenu_EveryItem()
    {
        var c = _w.S.Commands;
        var assets = AssetActions.Of(M);
        var slime = assets.Files(AssetCategory.Sprites).First(f => Path.GetFileName(f) == "slime.png");
        var menu = SceneCommands.AssetContextMenu(Ctx("T-M13", slime));
        Assert.Equal(["Open", "Rename", "Duplicate", "Delete", "-", "Find Usages", "Reimport", "Show in Explorer", "-", "Create Animation"], Labels(menu, c));
        Assert.Equal("Enter", AgbStudio.Editor.Theme.Shortcuts.Format(_w.S.Shortcuts.GestureOf("asset.open")!));
        Run(menu[0]);
        Assert.Equal(("T-P08", "AssetCard"), _w.S.Windows.Requests[^1]);
        if (_w.Win.Overlays.IsOpen) _w.Key(PhysicalKey.Escape);
        // Find Usages: Forest_01 (2), Forest_02 (1)
        Run(menu[5]);
        Assert.Equal(3, assets.Usages(assets.Selected!).Count);
        Assert.Contains(ConsoleLog.Of(_w.S).Entries, e => e.Text == "slime.png is used 3 times");
        Run(menu[6]);
        Assert.Contains(ConsoleLog.Of(_w.S).Entries, e => e.Text == "Reimported slime.png");
        var shown = new List<string>();
        _w.S.System = new SystemShell(() => _w.Win, (_, a) => shown.Add(a));
        assets.Select(slime);
        Run(menu[7]);
        Assert.Equal($"/select,\"{slime}\"", shown.Single());
        Run(menu[9]);                                               // Create Animation
        Assert.Contains(M.Project!.LoadMeta<SpriteMeta>(slime).Animations, a => a.Name == "anim");
        Assert.Equal("T-P08", _w.S.Windows.Requests[^1].Window);
        if (_w.Win.Overlays.IsOpen) _w.Key(PhysicalKey.Escape);
        Run(menu[2]);                                               // Duplicate
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(slime)!, "slime_copy.png")));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(slime)!, "slime_copy.png.meta.json")));
        Run(menu[3]);                                               // Delete (the copy is selected)
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(slime)!, "slime_copy.png")));
        _w.S.Commands.Execute("edit.undo");
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(slime)!, "slime_copy.png")));
        // Rename (F2): the file and its meta move; scenes that use the sheet follow; undo puts everything back
        assets.Select(slime);
        Assert.True(assets.Rename(assets.Selected!, "goo"));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(slime)!, "goo.png")));
        Assert.Equal("goo", OnDisk.Actors.First(a => a.Name == "Slime").Sprite);
        Assert.Equal("goo", GameProject.Load<SceneFile>(Path.Combine(M.Project.ScenesDir, "Forest_02.scene")).Actors.First(a => a.Name == "Slime").Sprite);
        _w.S.Commands.Execute("edit.undo");
        Assert.True(File.Exists(slime));
        Assert.Equal("slime", OnDisk.Actors.First(a => a.Name == "Slime").Sprite);
        Assert.Equal("slime", M.Scene!.Actors.First(a => a.Name == "Slime").Sprite);
    }

    [AvaloniaFact]
    public void ComponentMenu_EveryItem()
    {
        var c = _w.S.Commands;
        Assert.Equal(["Copy Component", "Paste Values", "Reset", "-", "Edit Script", "Remove Component"], Labels(SceneCommands.ComponentMenu(), c));
        M.SelectActor(0);
        M.SelectedComponent = 0;   // PlatformerBody
        var menu = SceneCommands.ComponentMenu();
        Run(menu[0]);
        Assert.Equal("PlatformerBody", M.ComponentClipboard!.Type);
        Run(menu[2]);                                                           // Reset: back to the defaults
        Assert.Empty(OnDisk.Actors[0].Components[0].Fields);
        Run(menu[1]);                                                           // Paste Values
        Assert.Equal("0.25", OnDisk.Actors[0].Components[0].Fields["gravity"]);
        Run(menu[4]);                                                           // Edit Script on a built-in: a notice
        Assert.Contains(_w.S.Toasts.Visible, t => t.Text.Contains("built-in"));
        M.SelectedComponent = 1;                                                // PlayerController
        Run(menu[4]);
        Assert.Equal(("T-P04", "component.editScript"), _w.S.Windows.Requests[^1]);
        if (_w.Win.Overlays.IsOpen) _w.Key(PhysicalKey.Escape);
        Run(menu[5]);                                                           // Remove Component
        Assert.Equal(["PlatformerBody"], OnDisk.Actors[0].Components.Select(x => x.Type));
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal(2, OnDisk.Actors[0].Components.Count);
    }

    [AvaloniaFact]
    public void ZoomAndAnimationList_EveryItem()
    {
        var c = _w.S.Commands;
        var zoom = SceneCommands.ZoomMenu(Ctx("T-M15"));
        Assert.Equal(["100%", "200%", "300%", "400%", "-", "Fit to Screen", "Pixel-perfect"], Labels(zoom, c));
        Assert.True(((RecentEntry)zoom[2]).IsCurrent);
        Assert.True(((RecentEntry)zoom[6]).IsCurrent);
        Run(zoom[0]);
        Assert.Equal(100, M.Zoom);
        Run(zoom[3]);
        Assert.Equal(400, M.Zoom);
        Run(zoom[5]);
        Assert.True(M.FitToScreen);
        Run(zoom[6]);
        Assert.False(M.PixelPerfect);
        Assert.Equal("Ctrl+0", AgbStudio.Editor.Theme.Shortcuts.Format(_w.S.Shortcuts.GestureOf("scene.zoom.fit")!));

        M.SelectActor(0);
        var anims = SceneCommands.AnimationList(Ctx("T-M16"));
        Assert.Equal(["idle", "run", "jump", "hurt", "-", "Edit Animations…"], Labels(anims, c));
        Assert.True(((RecentEntry)anims[0]).IsCurrent);
        Run(anims[1]);
        Assert.Equal("run", OnDisk.Actors[0].Animation);
        Run(anims[5]);
        Assert.Equal(("T-P08", "sprite.editAnimations"), _w.S.Windows.Requests[^1]);
    }
}
