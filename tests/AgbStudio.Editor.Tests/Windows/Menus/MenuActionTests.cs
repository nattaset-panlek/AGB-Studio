using AgbStudio.Core;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Settings;
using AgbStudio.Editor.Windows.Menus;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using SkiaSharp;

namespace AgbStudio.Editor.Tests.Windows.Menus;

/// <summary>P11.65-P11.67: every title bar menu item (T-M01..T-M08) acts or opens its window and is disabled when it cannot.</summary>
[Collection(EmulatorCollection.Name)]
public sealed class MenuActionTests : IDisposable
{
    readonly Fixture _fx = new();
    readonly List<string> _opened = [];
    MainWindow? _win;

    public void Dispose()
    {
        _win?.Close();
        _fx.Services.Emulator.Release();
        _fx.Dispose();
    }

    MainWindow Shell(EditorConfig? config = null, Func<string, CancellationToken, Task<string>>? fetch = null)
    {
        _fx.Services.Set(new EditorLinks { Config = config ?? new EditorConfig(), Open = _opened.Add, Fetch = fetch ?? ((_, _) => throw new InvalidOperationException("no network in tests")) });
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
        return _win;
    }

    abstract record Expect;
    /// <summary>Opens this window (registered here, or built by the named group).</summary>
    sealed record Opens(string Window, string Owner) : Expect;
    /// <summary>Runs an action handled in the editor (the main window or this group).</summary>
    sealed record Acts(string By) : Expect;
    /// <summary>Owned by another group that builds it in parallel (no window to route to).</summary>
    sealed record OtherGroup(string Owner) : Expect;

    const string Mine = "11.Q/11.S (this group)";

    /// <summary>Every command of the title bar menus and what it must do.</summary>
    static readonly Dictionary<string, Expect> Table = new()
    {
        // T-M01 File (+ T-M08 Open Recent)
        ["file.newProject"] = new Opens("T-D01", "11.B New Project"),
        ["file.openProject"] = new Opens("T-P12", "11.A Launcher"),
        ["file.clearRecent"] = new Acts("MainWindow"),
        ["file.save"] = new Acts("MainWindow"),
        ["file.saveAll"] = new Acts("MainWindow"),
        ["file.importAsset"] = new Opens("T-D04", "11.R Import"),
        ["file.exportRom"] = new Opens("T-D05", "11.P Build windows"),
        ["file.projectSettings"] = new Opens("T-D08", Mine),
        ["file.preferences"] = new Opens("T-D09", Mine),
        ["file.exit"] = new Acts("MainWindow"),
        // T-M02 Edit
        ["edit.undo"] = new Acts("MainWindow"), ["edit.redo"] = new Acts("MainWindow"), ["edit.cut"] = new Acts("MainWindow"),
        ["edit.copy"] = new Acts("MainWindow"), ["edit.paste"] = new Acts("MainWindow"), ["edit.duplicate"] = new Acts("MainWindow"),
        ["edit.delete"] = new Acts("MainWindow"), ["edit.selectAll"] = new Acts("MainWindow"),
        ["edit.findInProject"] = new Opens("T-O07", "Phase 10 shell"),
        ["edit.commandPalette"] = new Opens("T-O07", "Phase 10 shell"),
        // T-M03 Assets (+ T-M09 Create)
        ["assets.reimport"] = new OtherGroup("11.C Assets panel (P11.14 / T-M13 Reimport works on the selected asset)"),
        ["assets.showInExplorer"] = new Acts("MainWindow"),
        ["assets.findUnused"] = new Acts(Mine),
        ["assets.optimizePalettes"] = new Opens("T-P10", "11.F Palette Manager"),
        ["assets.create.sprite"] = new Opens("T-P08", "11.D Sprite Editor"),
        ["assets.create.tileset"] = new Opens("T-P03", "11.E Tilemap Editor"),
        ["assets.create.font"] = new Opens("T-P09", "11.G Font Manager"),
        ["assets.create.palette"] = new Opens("T-P10", "11.F Palette Manager"),
        ["assets.create.soundEffect"] = new Opens("T-P15", "11.J Audio"),
        ["assets.create.music"] = new Opens("T-P15", "11.J Audio"),
        ["assets.create.dataTable"] = new Opens("T-P07", "11.H Database"),
        ["assets.create.script"] = new Opens("T-P04", "11.N Script Editor"),
        ["assets.create.logicGraph"] = new Opens("T-P17", "11.L Logic Graph"),
        ["assets.create.dialogue"] = new Opens("T-P16", "11.K Dialogue Editor"),
        // T-M04 Scene (+ T-M10 Add Actor)
        ["scene.new"] = new Opens("T-D02", "11.C Scene Editor"),
        ["scene.settings"] = new Opens("T-D03", "11.C Scene Editor"),
        ["scene.setStart"] = new OtherGroup("11.C Scene Editor (P11.21 Set as Start Scene)"),
        ["scene.addActor.empty"] = new OtherGroup("11.C Scene Editor (P11.20 Add Actor)"),
        ["scene.addActor.player"] = new OtherGroup("11.C Scene Editor (P11.20 Add Actor)"),
        ["scene.addActor.enemy"] = new OtherGroup("11.C Scene Editor (P11.20 Add Actor)"),
        ["scene.addActor.pickup"] = new OtherGroup("11.C Scene Editor (P11.20 Add Actor)"),
        ["scene.addActor.trigger"] = new OtherGroup("11.C Scene Editor (P11.20 Add Actor)"),
        ["scene.addActor.camera"] = new OtherGroup("11.C Scene Editor (P11.20 Add Actor)"),
        ["scene.addActor.text"] = new OtherGroup("11.C Scene Editor (P11.20 Add Actor)"),
        ["scene.backgroundLayers"] = new Opens("T-P03", "11.E Tilemap Editor"),
        ["scene.worldMap"] = new Opens("T-P13", "11.M World Map"),
        // T-M05 Build
        ["build.play"] = new Opens("T-P02", "Phase 12 Play Mode"),
        ["build.playFromHere"] = new Opens("T-P02", "Phase 12 Play Mode"),
        ["build.buildRom"] = new Opens("T-D06", "11.P Build windows"),
        ["build.buildAndRun"] = new Opens("T-P02", "Phase 12 Play Mode"),
        ["build.flash"] = new Opens("T-D07", "11.P Build windows"),
        ["build.report"] = new Opens("T-P11", "11.P Build windows"),
        ["build.clean"] = new Acts(Mine),
        // T-M06 Tools
        ["tools.paletteManager"] = new Opens("T-P10", "11.F Palette Manager"),
        ["tools.fontManager"] = new Opens("T-P09", "11.G Font Manager"),
        ["tools.database"] = new Opens("T-P07", "11.H Database"),
        ["tools.localization"] = new Opens("T-P14", "11.I Localization"),
        ["tools.audio"] = new Opens("T-P15", "11.J Audio"),
        ["tools.dialogueEditor"] = new Opens("T-P16", "11.K Dialogue Editor"),
        ["tools.logicGraph"] = new Opens("T-P17", "11.L Logic Graph"),
        ["tools.debugger"] = new Opens("T-P19", "Phase 13 Debugger"),
        ["tools.screenshot"] = new Acts(Mine),
        ["tools.layout.reset"] = new Acts(Mine),
        ["tools.layout.save"] = new Acts(Mine),
        // T-M07 Help
        ["help.gettingStarted"] = new Opens("T-P12", "11.A Launcher"),
        ["help.scriptingApi"] = new Opens("T-P18", "11.O Scripting API"),
        ["help.keyboardShortcuts"] = new Opens("T-D10", "Phase 10 shell"),
        ["help.reportBug"] = new Acts(Mine),
        ["help.checkUpdates"] = new Acts(Mine),
        ["help.about"] = new Opens("T-D11", Mine),
    };

    [AvaloniaFact]
    public void EveryTitleBarMenuCommand_ActsOrOpensItsWindow()
    {
        var root = _fx.OpenFigmaSkyHopper().BuildDir;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "report.json"), "{}");   // Build Report is enabled once a build wrote a report
        var win = Shell(new EditorConfig { BugUrl = "https://bugs.example/new" });
        var s = _fx.Services;
        Assert.Equal(MenuModel.CommandIds().Order(), Table.Keys.Order());
        foreach (var (id, expect) in Table)
        {
            var cmd = s.Commands.Get(id);
            switch (expect)
            {
                case Opens o:
                    Assert.Equal(o.Window, cmd.Window);
                    Assert.True(cmd.CanExecute(), id + " is disabled with a project open");
                    var before = s.Windows.Requests.Count;
                    s.Commands.Execute(id);
                    Capture.Pump();
                    // the command opens its Figma window first; Create › Sprite / C# Script then put their New dialog on top
                    Assert.True(s.Windows.Requests.Count > before, id + " opened nothing");
                    Assert.Equal((o.Window, (string?)id), s.Windows.Requests[before]);
                    if (o.Owner == Mine) Assert.True(s.Windows.IsRegistered(o.Window), id);
                    while (win.Overlays.IsOpen) win.Overlays.CloseTop();
                    if (s.Build.IsBuilding)
                    {
                        // Play started a build: the game is not started, and Play / Build are enabled again when the build ends
                        (s.GetService(typeof(AgbStudio.Editor.Windows.Play.PlayController)) as AgbStudio.Editor.Windows.Play.PlayController)?.Stop();
                        Play.PlayTestSupport.WaitUntil(() => !s.Build.IsBuilding, 300);
                    }
                    break;
                case Acts:
                    Assert.True(cmd.IsImplemented, id + " has no handler");
                    break;
                case OtherGroup:
                    Assert.Null(cmd.Window);
                    break;
            }
        }
        // the title bar shows every one of them
        var items = new List<MenuItem>();
        void Walk(IEnumerable<object?> xs) { foreach (var m in xs.OfType<MenuItem>()) { items.Add(m); Walk(m.Items); } }
        Walk(win.MenuBar.Items);
        foreach (var id in Table.Keys) Assert.Contains(items, m => m.Command is EditorCommand c && c.Id == id);
    }

    // P11.65 / T-M09: Create › Tileset / Tilemap makes a new map (and a tileset when there is none); Create › Font opens New Font
    [AvaloniaFact]
    public void Create_TilesetTilemapMakesANewMap_FontOpensNewFont()
    {
        var p = _fx.OpenFigmaSkyHopper();
        var win = Shell();
        var s = _fx.Services;
        var maps = p.MapFiles().Count();
        s.Commands.Execute("assets.create.tileset");
        Capture.Pump();
        var created = Path.Combine(p.MapsDir, "Map_01.map");
        Assert.True(File.Exists(created));
        Assert.Equal(maps + 1, p.MapFiles().Count());
        var map = AgbStudio.Core.Project.GameProject.Load<AgbStudio.Core.Project.MapFile>(created);
        Assert.Equal(("Map_01", AgbStudio.Core.Project.GameProject.AssetName(p.TilesetFiles().First()), 30, 20, 0), (map.Name, map.Tileset, map.Width, map.Height, map.CollisionLayer));
        Assert.Equal(600, Assert.Single(map.Layers).Cells.Count);
        var page = Assert.IsType<AgbStudio.Editor.Windows.Tilemap.TilemapEditorPage>(win.CurrentPage);
        Assert.Equal(Path.GetFullPath(created), page.Session!.Doc.Path);
        Assert.Equal("Map_01.map", s.Documents.Active!.Name);
        // a second one gets the next free name
        s.Commands.Execute("assets.create.tileset");
        Capture.Pump();
        Assert.True(File.Exists(Path.Combine(p.MapsDir, "Map_02.map")));
        // a project without tilesets gets a blank 4 x 4 tileset with the map
        foreach (var t in p.TilesetFiles().ToList()) File.Delete(t);
        var path = AgbStudio.Editor.Windows.Tilemap.NewTilemap.Create(p);
        Assert.Equal("tiles", AgbStudio.Core.Project.GameProject.Load<AgbStudio.Core.Project.MapFile>(path).Tileset);
        var png = Png.Read(Path.Combine(p.AssetsDir, "tilesets", "tiles.png"));
        Assert.Equal((64, 64), (png.Width, png.Height));

        // Create › Font: Font Manager with New Font on top
        s.Commands.Execute("assets.create.font");
        Capture.Pump();
        Assert.Equal("T-P09", win.CurrentPageId);
        Assert.Equal(("T-D15", "assets.create.font"), s.Windows.Requests[^1]);
        Assert.IsType<AgbStudio.Editor.Windows.Font.NewFontDialog>(win.Overlays.Open[^1]);
        win.Overlays.CloseTop();
    }

    [AvaloniaFact]
    public void ProjectCommands_AreDisabledWithoutAProject_AndEnabledWhenOneOpens()
    {
        var win = Shell();
        var s = _fx.Services;
        MenuItem Item(string header) => win.MenuBar.Items.OfType<MenuItem>().SelectMany(m => m.Items.OfType<MenuItem>()).First(m => (string?)m.Header == header);
        var enabled = MenuActions.NeedProject.Where(i => Table[i] is not OtherGroup && s.Commands.Get(i).CanExecute()).ToList();
        Assert.Equal([], enabled);
        var file = win.MenuBar.Items.OfType<MenuItem>().First();
        win.Click(file);
        Assert.True(file.IsSubMenuOpen);
        Assert.False(Item("Export ROM (.gba)…").IsEffectivelyEnabled);
        Assert.False(Item("Project Settings…").IsEffectivelyEnabled);
        Assert.True(Item("Preferences…").IsEffectivelyEnabled);
        Assert.True(Item("About AGB Studio").IsEffectivelyEnabled);
        win.Key(PhysicalKey.Escape);
        // a disabled command does nothing, also from its shortcut
        var before = s.Windows.Requests.Count;
        win.Key(PhysicalKey.E, RawInputModifiers.Control);
        Assert.Equal(before, s.Windows.Requests.Count);

        _fx.OpenSkyHopper();
        Capture.Pump();
        // menu items read their command state when the menu opens
        win.Click(file);
        Assert.True(Item("Export ROM (.gba)…").IsEffectivelyEnabled);
        Assert.True(Item("Project Settings…").IsEffectivelyEnabled);
        // Build Report needs a report, Report a Bug a configured page, Screenshot / GIF a running game
        Assert.False(s.Commands.Get("build.report").CanExecute());
        Assert.False(s.Commands.Get("help.reportBug").CanExecute());
        Assert.False(s.Commands.Get("tools.screenshot").CanExecute());
        Assert.True(s.Commands.Get("help.checkUpdates").CanExecute());
    }

    // ---- P11.66 Screenshot / GIF -------------------------------------------------------------------------

    [AvaloniaFact]
    public void ScreenshotGif_F12SavesThePngThenRecordsAndSavesTheGif()
    {
        _fx.OpenSkyHopper();
        GameFrame? frame = null;
        var capture = new GameCapture(() => frame, _fx.Timer) { Folder = Path.Combine(_fx.Folder, "shots"), Clock = () => _fx.Now, BaseName = () => "sky_hopper" };
        _fx.Services.Set(capture);
        var win = Shell();
        win.Key(PhysicalKey.F12);
        Assert.False(capture.Recording);   // no game running: disabled

        uint[] Pixels(uint seed) => Enumerable.Range(0, 240 * 160).Select(i => 0xFF000000u | (uint)(i * 2654435761u ^ seed) & 0xFFFFFFu).ToArray();
        frame = new GameFrame(Pixels(1), 10);
        win.Key(PhysicalKey.F12);
        Assert.True(capture.Recording);
        Assert.True(_fx.Timer.IsRunning);
        var png = Png.Read(capture.LastPng!);
        Assert.Equal((240, 160), (png.Width, png.Height));
        Assert.Equal(frame.Value.Pixels[12345], png.Pixels[12345]);
        Assert.Equal("sky_hopper_20261031_120000.png", Path.GetFileName(capture.LastPng));
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text.StartsWith("Screenshot saved · sky_hopper_20261031_120000.png"));

        for (var i = 1; i <= 5; i++)
        {
            _fx.Now = _fx.Now.AddMilliseconds(50);
            frame = new GameFrame(Pixels((uint)i + 1), 10 + i);
            _fx.Timer.Fire();
        }
        _fx.Timer.Fire();   // same frame again: not added
        Assert.Equal(6, capture.RecordedFrames);
        win.Key(PhysicalKey.F12);
        Assert.False(capture.Recording);
        Assert.False(_fx.Timer.IsRunning);
        Assert.EndsWith(".gif", capture.LastGif);
        using (var codec = SKCodec.Create(capture.LastGif))
        {
            Assert.Equal(6, codec.FrameCount);
            Assert.Equal((240, 160), (codec.Info.Width, codec.Info.Height));
        }
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text == "GIF saved · " + Path.GetFileName(capture.LastGif));
    }

    [AvaloniaFact]
    public void ScreenshotGif_StopsByItselfAfter30Seconds()
    {
        GameFrame? frame = new GameFrame(new uint[240 * 160], 1);
        var capture = new GameCapture(() => frame, _fx.Timer) { Folder = Path.Combine(_fx.Folder, "shots"), Clock = () => _fx.Now };
        string? auto = null;
        capture.AutoStopped += p => auto = p;
        Assert.NotNull(capture.Toggle());
        _fx.Now = _fx.Now.AddSeconds(GameCapture.MaxSeconds);
        _fx.Timer.Fire();
        Assert.False(capture.Recording);
        Assert.True(File.Exists(auto));
    }

    /// <summary>A real Game view: the editor's mGBA core runs a tiny ROM that fills the screen red (mode 3).</summary>
    [AvaloniaFact]
    public void ScreenshotGif_CapturesTheEmulatorCore()
    {
        _fx.OpenSkyHopper();
        var s = _fx.Services;
        s.Set(new GameCapture(() => GameCapture.EditorFrame(s), _fx.Timer) { Folder = Path.Combine(_fx.Folder, "shots"), Clock = () => _fx.Now });
        var win = Shell();
        Assert.False(s.Commands.Get("tools.screenshot").CanExecute());
        var core = s.Emulator.Acquire();
        core.LoadRom(RedScreenRom());
        for (var i = 0; i < 10; i++) core.RunFrame();
        Assert.True(s.Commands.Get("tools.screenshot").CanExecute());
        s.Commands.Execute("tools.screenshot");
        var capture = GameCapture.Of(s);
        var png = Png.Read(capture.LastPng!);
        var c = png.Pixels[80 * 240 + 120];
        Assert.True(((c >> 16) & 0xFF) > 200 && ((c >> 8) & 0xFF) < 40 && (c & 0xFF) < 40, $"pixel {c:X8} is not red");
        for (var i = 0; i < 3; i++) { core.RunFrame(); _fx.Now = _fx.Now.AddMilliseconds(33); _fx.Timer.Fire(); }
        s.Commands.Execute("tools.screenshot");
        Assert.Equal(4, capture.LastGifFrames);
        core.UnloadRom();
    }

    /// <summary>ARM code: DISPCNT = mode 3 + BG2, fill VRAM with red (BGR555 0x001F), loop forever.</summary>
    static byte[] RedScreenRom()
    {
        var rom = new byte[1024];
        void W(int at, uint v) => BitConverter.TryWriteBytes(rom.AsSpan(at), v);
        W(0x00, 0xEA00002E);   // b 0xC0
        rom[0xB2] = 0x96;
        uint[] code =
        [
            0xE3A00404, 0xE3A01B01, 0xE3811003, 0xE1C010B0,   // r0 = DISPCNT; r1 = 0x403; strh
            0xE3A00406, 0xE3A0101F, 0xE1811801, 0xE3A02C4B,   // r0 = VRAM; r1 = red | red << 16; r2 = 19200 words
            0xE4801004, 0xE2522001, 0x1AFFFFFC, 0xEAFFFFFE,   // loop: str r1, [r0], #4; subs; bne; b .
        ];
        for (var i = 0; i < code.Length; i++) W(0xC0 + i * 4, code[i]);
        var sum = 0;
        for (var i = 0xA0; i <= 0xBC; i++) sum += rom[i];
        rom[0xBD] = (byte)(-(sum + 0x19));
        return rom;
    }

    // ---- P11.67 Report a Bug, Check for Updates (D-13) ---------------------------------------------------

    [AvaloniaFact]
    public void ReportABug_OpensTheConfiguredPage()
    {
        var win = Shell(new EditorConfig { BugUrl = "https://bugs.example/new" });
        Assert.True(_fx.Services.Commands.Execute("help.reportBug"));
        Assert.Equal(["https://bugs.example/new"], _opened);
        _ = win;
    }

    [AvaloniaFact]
    public async Task CheckForUpdates_NoSourceNewerSameAndOffline()
    {
        string? feed = null;
        var asked = new List<string>();
        var win = Shell(new EditorConfig(), (url, _) => { asked.Add(url); return feed is null ? throw new HttpRequestException("offline") : Task.FromResult(feed); });
        var s = _fx.Services;
        _ = win;
        var r = await MenuActions.CheckUpdatesAsync(s);
        const string noSource = "Could not check for updates: no update source is set. Add an updateFeed address to editor.json next to AGBStudio.exe.";
        Assert.Equal(noSource, r.Message);
        Assert.Contains(s.Toasts.Visible, t => t.Text == noSource && t.Status == ToastStatus.Error);
        Assert.Empty(asked);

        var links = new EditorLinks { Config = new EditorConfig { UpdateFeed = "https://feed.example/latest.json" }, Open = _opened.Add, Fetch = (url, _) => { asked.Add(url); return feed is null ? throw new HttpRequestException("offline") : Task.FromResult(feed); } };
        s.Set(links);
        // versions relative to this build (Directory.Build.props): a newer minor, then this version
        var current = Version.Parse(About.Version);
        var newer = $"{current.Major}.{current.Minor + 1}.0";
        feed = $$"""{ "version": "{{newer}}", "url": "https://agb.example/releases/{{newer}}" }""";
        r = await MenuActions.CheckUpdatesAsync(s);
        Assert.Equal(($"AGB Studio {newer} is available", true), (r.Message, r.Available));
        Assert.Equal($"https://agb.example/releases/{newer}", _opened[^1]);
        Assert.Equal(["https://feed.example/latest.json"], asked);

        feed = $$"""[{ "tag_name": "v{{About.Version}}", "html_url": "https://agb.example/r/{{About.Version}}" }]""";
        r = await MenuActions.CheckUpdatesAsync(s);
        Assert.Equal(($"AGB Studio is up to date ({About.Version})", false), (r.Message, r.Available));

        feed = null;
        r = await MenuActions.CheckUpdatesAsync(s);
        Assert.Equal("Could not check for updates: offline. Check the internet connection and the updateFeed address in editor.json, then try again.", r.Message);
    }

    // ---- Build › Clean Build, Assets › Find Unused Assets ----------------------------------------------

    [AvaloniaFact]
    public void CleanBuild_DeletesTheBuildFolder()
    {
        var p = _fx.OpenFigmaSkyHopper();
        Directory.CreateDirectory(Path.Combine(p.BuildDir, "gen"));
        File.WriteAllText(Path.Combine(p.BuildDir, "gen", "game.cpp"), "");
        Shell();
        Assert.True(_fx.Services.Commands.Execute("build.clean"));
        Assert.False(Directory.Exists(p.BuildDir));
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text.StartsWith("Build folder cleaned"));
    }

    [AvaloniaFact]
    public void FindUnusedAssets_ListsAssetsNothingMentions()
    {
        var p = _fx.OpenFigmaSkyHopper();
        var sprites = Path.Combine(p.AssetsDir, "sprites");
        File.Copy(Path.Combine(sprites, "hero.png"), Path.Combine(sprites, "lonely_ghost.png"));
        Shell();
        Assert.True(_fx.Services.Commands.Execute("assets.findUnused"));
        var unused = _fx.Services.Get<UnusedAssetsResult>().Files.Select(Path.GetFileName).ToList();
        Assert.Contains("lonely_ghost.png", unused);
        Assert.DoesNotContain("hero.png", unused);
        Assert.DoesNotContain("forest_tiles.png", unused);
        Assert.DoesNotContain("thai_vwf.font", unused);
        Assert.Contains(_fx.Services.Toasts.Visible, t => t.Text.Contains("lonely_ghost.png"));
    }
}
