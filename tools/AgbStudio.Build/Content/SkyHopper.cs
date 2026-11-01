using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content;

/// <summary>The "Sky Hopper" fixture project (P1.05): original content shaped like the data in the Figma frames,
/// so screenshots of the editor show the same scenes, assets, tables and strings as the design.</summary>
public static class SkyHopper
{
    public static readonly string[] SceneNames = ["Title", "Forest_01", "Forest_02", "Cave_01", "Summit"];

    public static GameProject Create(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var s = new ProjectFile();
        s.General = new GeneralSettings
        {
            GameTitle = "Sky Hopper", Version = "1.0.3", RomTitle = "SKYHOPPER", GameCode = "SKYH", Maker = "GE",
            StartScene = "Title", DefaultFont = "thai_vwf", DefaultLanguage = "th", BootSplash = true, SplashSeconds = 2, SkipSplashInDebug = true,
        };
        s.Save = new SaveSettings
        {
            Type = SaveType.Sram32K, Slots = 3, Autosave = true, Checksum = true,
            Fields =
            {
                new SaveField { Name = "score", Type = "int" }, new SaveField { Name = "lives", Type = "byte" },
                new SaveField { Name = "coins", Type = "short" }, new SaveField { Name = "flags", Type = "bitset 128" },
                new SaveField { Name = "inventory", Type = "Items[16]" }, new SaveField { Name = "position", Type = "fixed2" },
            },
        };
        s.Build.RunAfterBuild = true;
        s.Scripting.Namespace = "SkyHopper";
        s.Localization = new LocalizationSettings
        {
            Source = "en", ThaiWrap = true, ShowMissingKeys = true, FailOnMissing = false,
            Languages =
            [
                new LanguageInfo { Code = "en", Name = "English", Font = "thai_vwf" },
                new LanguageInfo { Code = "th", Name = "ไทย", Font = "thai_vwf", Fallback = "en" },
                new LanguageInfo { Code = "ja", Name = "日本語", Font = "jp_kana", Fallback = "en" },
            ],
        };
        s.Tags = ["Player", "Enemy", "Coin", "Goal", "Block", "NPC"];
        s.LastScene = "Forest_01";
        var p = GameProject.Create(dir, "Sky Hopper", s);

        WriteArt(p);
        WriteMaps(p);
        WriteScenes(p);
        WriteAudio(p);
        WriteFonts(p);
        WriteStrings(p);
        WriteDatabase(p);
        WriteScripts(p);
        return p;
    }

    // ---- art -----------------------------------------------------------------------------------------

    /// <summary>Gives files distinct creation times one second apart, in the order they are stamped: the editor lists
    /// sprites (Sprite Editor) and tables (Database) in creation order, and files written within one clock tick would
    /// tie and fall back to name order. The times start an hour back, so files the user adds later come after them.</summary>
    sealed class CreationOrder
    {
        DateTime _next = DateTime.UtcNow.AddHours(-1);

        /// <summary>The art written so far, for palettes.json: project-relative path, kind and pixels.</summary>
        public List<(string Rel, BankKind Kind, Canvas Art)> Art { get; } = [];

        public void Stamp(string path)
        {
            File.SetCreationTimeUtc(path, _next);
            _next = _next.AddSeconds(1);
        }
    }

    static void Sprite(GameProject p, CreationOrder order, string name, Canvas img, SpriteMeta meta)
    {
        var path = Path.Combine(p.AssetsDir, "sprites", name + ".png");
        img.Save(path);
        order.Stamp(path);
        order.Art.Add(("assets/sprites/" + name + ".png", BankKind.Obj, img));
        GameProject.Save(GameProject.MetaPath(path), meta);
    }

    static SpriteMeta Meta(int w, int h, params AnimationData[] anims)
    {
        var m = new SpriteMeta { FrameWidth = w, FrameHeight = h, OriginX = w / 2, OriginY = h, Collider = new ColliderData { X = Math.Min(2, w / 4), Y = Math.Min(2, h / 4), W = w - 2 * Math.Min(2, w / 4), H = h - Math.Min(2, h / 4) } };
        m.Animations.AddRange(anims);
        return m;
    }

    static void WriteArt(GameProject p)
    {
        // Created in the order of the Figma Sprite Editor list (hero, slime, coin, block, heart, cloud), then the rest.
        var order = new CreationOrder();
        // The Figma art (T-P08 hero, the Asset Card art, art-scene-day): SkyHopperArt.
        Sprite(p, order, "hero", SkyHopperArt.Hero8(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 2, Y = 2, W = 12, H = 14 },
            // Figma Animation panel: idle 8f · loop at 6 ticks (≈ 10 fps), run 6f · loop, jump 3f, hurt 2f; they share the 8 frames
            Animations =
            {
                new AnimationData { Name = "idle", Frames = { 0, 1, 2, 3, 4, 5, 6, 7 }, Ticks = 6 },
                new AnimationData { Name = "run", Frames = { 2, 3, 4, 5, 6, 7 }, Ticks = 6 },
                new AnimationData { Name = "jump", Frames = { 5, 6, 7 }, Ticks = 6, Loop = false },
                new AnimationData { Name = "hurt", Frames = { 6, 7 }, Ticks = 6, Loop = false },
            },
        });
        Sprite(p, order, "slime", SkyHopperArt.Slime4(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 12, OriginX = 8, OriginY = 12, Collider = new ColliderData { X = 2, Y = 2, W = 12, H = 10 },
            Animations = { new AnimationData { Name = "idle", Frames = { 0, 1, 2, 3 }, Ticks = 8 } },
        });
        Sprite(p, order, "coin", SkyHopperArt.Coin6(), new SpriteMeta
        {
            FrameWidth = 8, FrameHeight = 8, OriginX = 4, OriginY = 8, Collider = new ColliderData { X = 0, Y = 0, W = 8, H = 8 },
            Animations = { new AnimationData { Name = "spin", Frames = { 0, 1, 2, 3, 4, 5 }, Ticks = 5 } },
        });
        Sprite(p, order, "block", SkyHopperArt.Block(), Meta(16, 16));
        // HUD hearts are placed by their top-left corner (see HUD.cs)
        Sprite(p, order, "heart", SkyHopperArt.Heart(), new SpriteMeta { FrameWidth = 8, FrameHeight = 8, OriginX = 0, OriginY = 0, Collider = new ColliderData { X = 0, Y = 0, W = 7, H = 6 } });
        Sprite(p, order, "cloud", SkyHopperArt.Cloud(), Meta(16, 8));
        // the flag stands on its zone: origin at the bottom-left corner, so X is also the left of the Goal Flag trigger
        Sprite(p, order, "flag", SkyHopperArt.Flag(), new SpriteMeta { FrameWidth = 16, FrameHeight = 64, OriginX = 0, OriginY = 64, Collider = new ColliderData { X = 2, Y = 14, W = 12, H = 50 } });
        Sprite(p, order, "owl", Creature(PixelArt.Wood, PixelArt.Gold, 16, 4), Meta(16, 16, new AnimationData { Name = "idle", Frames = { 0, 1, 2, 3 }, Ticks = 12 }));
        Sprite(p, order, "bat", Creature(PixelArt.StoneDark, PixelArt.Red, 16, 2), Meta(16, 16, new AnimationData { Name = "fly", Frames = { 0, 1 }, Ticks = 6 }));
        Sprite(p, order, "slime_king", Creature(PixelArt.SlimeDark, PixelArt.Gold, 32, 2), Meta(32, 32, new AnimationData { Name = "idle", Frames = { 0, 1 }, Ticks = 16 }));
        Sprite(p, order, "star", Gem(PixelArt.Gold, 4), Meta(8, 8, new AnimationData { Name = "spin", Frames = { 0, 1, 2, 3 }, Ticks = 4 }));
        Sprite(p, order, "gem", Gem(PixelArt.Water, 4), Meta(8, 8, new AnimationData { Name = "shine", Frames = { 0, 1, 2, 3 }, Ticks = 8 }));
        var tiles = Path.Combine(p.AssetsDir, "tilesets");
        var forest = SkyHopperArt.ForestTiles();
        order.Art.Add(("assets/tilesets/forest_tiles.png", BankKind.Bg, forest));
        forest.Save(Path.Combine(tiles, "forest_tiles.png"));
        GameProject.Save(GameProject.MetaPath(Path.Combine(tiles, "forest_tiles.png")), ForestTilesetMeta());
        var cave = CaveTiles();
        order.Art.Add(("assets/tilesets/cave_tiles.png", BankKind.Bg, cave));
        cave.Save(Path.Combine(tiles, "cave_tiles.png"));
        GameProject.Save(GameProject.MetaPath(Path.Combine(tiles, "cave_tiles.png")), new TilesetMeta
        {
            Tiles = { [1] = new TileProperties { Name = "rock", Collision = CollisionType.Solid }, [2] = new TileProperties { Name = "ledge", Collision = CollisionType.OneWay },
                [3] = new TileProperties { Name = "lava", Collision = CollisionType.Hazard }, [4] = new TileProperties { Name = "pool", Collision = CollisionType.Water } },
        });
        var images = Path.Combine(p.AssetsDir, "images");
        // placed so the start camera of Forest_01 shows art-scene-day (the Figma Scene Editor and Play Mode)
        var sky = SkyHopperArt.Sky(StartCamera.X, StartCamera.Y, SkyParallax);
        var hills = SkyHopperArt.Hills(StartCamera.X, StartCamera.Y, HillsParallax);
        sky.Save(Path.Combine(images, "sky_bg.png"));
        hills.Save(Path.Combine(images, "hills_bg.png"));
        order.Art.Add(("assets/images/sky_bg.png", BankKind.Bg, sky));
        order.Art.Add(("assets/images/hills_bg.png", BankKind.Bg, hills));
        WritePaletteLibrary(p, order.Art);
    }

    /// <summary>assets/palettes/palettes.json (T-P10): one bank per sprite (OBJ) and per tileset / image (BG), numbered in
    /// creation order, with the colours of the art (index 0 transparent, up to 15 by use) so the Palette Manager opens on
    /// a saved library instead of banks derived from the art.</summary>
    static void WritePaletteLibrary(GameProject p, List<(string Rel, BankKind Kind, Canvas Art)> art)
    {
        var lib = new PaletteLibraryFile();
        var next = new Dictionary<BankKind, int>();
        foreach (var (rel, kind, img) in art)
        {
            var slot = next.GetValueOrDefault(kind);
            if (slot >= 16) break;
            next[kind] = slot + 1;
            var use = new Dictionary<uint, int>();
            var order = new List<uint>();
            foreach (var px in img.Pixels)
            {
                if ((px >> 24) < 0x80) continue;
                var c = px | 0xFF000000u;
                if (!use.TryGetValue(c, out var n)) order.Add(c);
                use[c] = n + 1;
            }
            var keep = order.OrderByDescending(c => use[c]).Take(15).ToHashSet();
            var name = Path.GetFileNameWithoutExtension(rel).Replace('_', ' ');
            lib.Banks.Add(new PaletteBankData
            {
                Kind = kind, Slot = slot, Name = char.ToUpperInvariant(name[0]) + name[1..],
                Colors = new[] { "none" }.Concat(order.Where(keep.Contains).Select(c => "#" + (c & 0xFFFFFFu).ToString("X6"))).ToList(),
                Assets = [rel],
            });
        }
        GameProject.Save(Path.Combine(p.PalettesDir, "palettes.json"), lib);
    }

    const double SkyParallax = 0.2, HillsParallax = 0.5;

    /// <summary>Where the Forest_01 camera starts: the player stands at the bottom-left of the 30x20 map, so the camera
    /// clamps to (0, 160) and the screen shows map rows 10-19, the picture of the Figma Scene Editor.</summary>
    public static readonly (int X, int Y) StartCamera = (0, 160);

    /// <summary>The Player's start in Forest_01: art-scene-day draws the hero at screen (40, 112); the sprite origin is (8, 16).</summary>
    public static readonly (int X, int Y) PlayerStart = (48, 288);

    /// <summary>The Sky Hopper forest tileset: the Figma tiles, and its "Grass edges" rule set (T-P26: Top edge, Left edge,
    /// Right edge, Corner and Inner, every neighbour either filled or empty).</summary>
    public static TilesetMeta ForestTilesetMeta() => new()
    {
        Tiles =
        {
            [SkyHopperArt.TGrass] = new TileProperties { Name = "grass_top", Collision = CollisionType.Solid, Tag = "ground" },
            [SkyHopperArt.TDirt] = new TileProperties { Name = "dirt", Collision = CollisionType.Solid },
            [SkyHopperArt.TBricks] = new TileProperties { Name = "bricks", Collision = CollisionType.OneWay },
            [SkyHopperArt.TBush] = new TileProperties { Name = "bush" },
            [SkyHopperArt.TLeaves] = new TileProperties { Name = "leaves" },
            [SkyHopperArt.TStone] = new TileProperties { Name = "stone", Collision = CollisionType.Solid },
            [SkyHopperArt.TWater] = new TileProperties { Name = "water", Collision = CollisionType.Water },
            [SkyHopperArt.TSpikes] = new TileProperties { Name = "spikes", Collision = CollisionType.Hazard },
            [SkyHopperArt.TBricksLeft] = new TileProperties { Name = "bricks_left", Collision = CollisionType.OneWay },
            [SkyHopperArt.TBricksRight] = new TileProperties { Name = "bricks_right", Collision = CollisionType.OneWay },
        },
        AutoTiles =
        {
            new AutoTileSet
            {
                Name = "Grass edges",
                Rules =
                {
                    new AutoTileRule { Name = "Top edge", Mask = [0, 0, 0, 1, 1, 1, 1, 1, 1], Tile = SkyHopperArt.TGrass },
                    new AutoTileRule { Name = "Left edge", Mask = [0, 1, 1, 0, 1, 1, 0, 1, 1], Tile = SkyHopperArt.TDirt },
                    new AutoTileRule { Name = "Right edge", Mask = [1, 1, 0, 1, 1, 0, 1, 1, 0], Tile = SkyHopperArt.TBricks },
                    new AutoTileRule { Name = "Corner", Mask = [0, 0, 0, 0, 1, 1, 0, 1, 1], Tile = SkyHopperArt.TBush },
                    new AutoTileRule { Name = "Inner", Mask = [1, 1, 1, 1, 1, 1, 1, 1, 1], Tile = SkyHopperArt.TLeaves },
                },
            },
        },
    };

    static Canvas Creature(uint body, uint accent, int size, int frames)
    {
        var sheet = new Canvas(size * frames, size);
        for (var f = 0; f < frames; f++)
        {
            var c = new Canvas(size, size);
            var bob = f % 2;
            c.Ellipse(size / 2.0, size / 2.0 + bob, size * 0.38, size * 0.36, body);
            c.Ellipse(size * 0.36, size * 0.42 + bob, size * 0.1, size * 0.1, PixelArt.White);
            c.Ellipse(size * 0.64, size * 0.42 + bob, size * 0.1, size * 0.1, PixelArt.White);
            c.Rect(size * 36 / 100, size * 42 / 100 + bob, 1, 1, PixelArt.Ink);
            c.Rect(size * 64 / 100, size * 42 / 100 + bob, 1, 1, PixelArt.Ink);
            c.Rect(size / 2 - 1, size * 6 / 10 + bob, 2, 1, accent);
            c.Outline(PixelArt.Ink);
            sheet.Blit(c, f * size, 0);
        }
        return sheet;
    }

    static Canvas Gem(uint color, int frames)
    {
        var sheet = new Canvas(8 * frames, 8);
        for (var f = 0; f < frames; f++)
        {
            var c = new Canvas(8, 8);
            for (var y = 0; y < 8; y++)
            {
                var w = y < 4 ? y * 2 + 2 : (7 - y) * 2;
                c.Rect(4 - w / 2, y, w, 1, color);
            }
            c.Set(3 + f % 2, 2, PixelArt.White);
            sheet.Blit(c, f * 8, 0);
        }
        return sheet;
    }

    static Canvas CaveTiles()
    {
        var c = new Canvas(16 * 5, 16);
        c.Rect(16, 0, 16, 16, PixelArt.Stone); c.Rect(16, 0, 16, 2, PixelArt.StoneDark); c.Rect(22, 8, 3, 2, PixelArt.StoneDark);
        c.Rect(32, 0, 16, 4, PixelArt.StoneDark); c.Rect(32, 4, 16, 1, PixelArt.Ink);
        c.Rect(48, 6, 16, 10, PixelArt.Red); c.Rect(48, 6, 16, 2, PixelArt.Gold);
        c.Rect(64, 4, 16, 12, PixelArt.Water); c.Rect(66, 7, 6, 1, PixelArt.WaterLight);
        return c;
    }

    // ---- maps and scenes -------------------------------------------------------------------------------

    static MapFile Level(string name, int w, int h, string tileset, int seed)
    {
        var map = SampleProjects.GroundMap(name, w, h, w / 3, w / 3 + 1, w * 2 / 3);
        map.Tileset = tileset;
        // the Sky Hopper forest tileset has grass (1) and the one-way platform (3) where PixelArt has them; its stone is 6
        if (tileset == "forest_tiles")
            for (var i = 0; i < map.Layers[0].Cells.Count; i++)
                if (map.Layers[0].Cells[i] == PixelArt.TStone) map.Layers[0].Cells[i] = SkyHopperArt.TStone;
        var rng = new Random(seed);
        var cells = map.Layers[0].Cells;
        for (var i = 0; i < 4; i++)
        {
            var x = rng.Next(2, w - 6);
            var y = rng.Next(h / 3, h - 4);
            for (var k = 0; k < 4; k++) cells[y * w + x + k] = PixelArt.TPlatform;
        }
        map.Layers[0].Name = "Ground";
        map.Layers[0].Bg = 2;
        map.Layers[0].Priority = 1;
        return map;
    }

    /// <summary>Forest_01 (30x20). Its bottom-left screen is art-scene-day: ground with a pit at columns 9-10 and the brick
    /// platform under the coins; further right a higher platform, a stone step and the door to the cave.</summary>
    static MapFile Forest01()
    {
        const int w = 30, h = 20;
        var cells = new int[w * h];
        void Set(int x, int y, int t) => cells[y * w + x] = t;
        for (var x = 0; x < w; x++)
        {
            if (x is 9 or 10) continue;
            Set(x, 18, SkyHopperArt.TGrass);
            Set(x, 19, SkyHopperArt.TDirt);
        }
        // one-way bricks, the 9 of the Tilemap Editor's Collision section: under the coins art-scene-day draws them from
        // x 88 to 136, half a tile off the grid, so a left end, two bricks and a right end; 5 further right
        Set(5, 15, SkyHopperArt.TBricksLeft);
        for (var x = 6; x <= 7; x++) Set(x, 15, SkyHopperArt.TBricks);
        Set(8, 15, SkyHopperArt.TBricksRight);
        for (var x = 15; x <= 19; x++) Set(x, 14, SkyHopperArt.TBricks);
        Set(24, 17, SkyHopperArt.TStone);
        Set(24, 16, SkyHopperArt.TStone);
        return new MapFile
        {
            Name = "Forest_01", Tileset = "forest_tiles", Width = w, Height = h, CollisionLayer = 2,
            Layers =
            {
                new MapLayerData { Name = "Sky", Bg = 0, Type = LayerType.Image, Image = "sky_bg", Priority = 3, ParallaxX = SkyParallax, ParallaxY = SkyParallax, Wrap = true },
                new MapLayerData { Name = "Hills", Bg = 1, Type = LayerType.Image, Image = "hills_bg", Priority = 2, ParallaxX = HillsParallax, ParallaxY = HillsParallax, Wrap = true },
                new MapLayerData { Name = "Ground", Bg = 2, Priority = 1, Cells = [.. cells] },
            },
        };
    }

    static void WriteMaps(GameProject p)
    {
        var forest = Forest01();
        GameProject.Save(Path.Combine(p.MapsDir, "Forest_01.map"), forest);
        GameProject.Save(Path.Combine(p.MapsDir, "Forest_02.map"), Level("Forest_02", 32, 20, "forest_tiles", 2));
        var cave = Level("Cave_01", 24, 40, "cave_tiles", 3);
        GameProject.Save(Path.Combine(p.MapsDir, "Cave_01.map"), cave);
        GameProject.Save(Path.Combine(p.MapsDir, "Summit.map"), Level("Summit", 16, 10, "forest_tiles", 4));
        var titleMap = SampleProjects.GroundMap("Title", 15, 10, -1, -1, -1);
        // Ground only (Figma "In-game · Title screen"): no floating platform behind the menu.
        for (var x = 0; x < 15; x++) titleMap.Layers[0].Cells[6 * 15 + x] = 0;
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), titleMap);
    }

    static ActorData Player(int x, int y) => new()
    {
        Name = "Player", Kind = ActorKind.Player, Sprite = "hero", Animation = "idle", X = x, Y = y, Tag = "Player",
        Components =
        {
            new ComponentData { Type = "PlatformerBody", Fields = { ["gravity"] = "0.25", ["jumpForce"] = "3.50", ["maxSpeed"] = "1.50" } },
            new ComponentData { Type = "PlayerController", Fields = { ["speed"] = "1.5", ["jumpPower"] = "3.5", ["lives"] = "3" } },
        },
    };

    static ActorData Slime(string name, int x, int y) => new()
    {
        Name = name, Kind = ActorKind.Enemy, Sprite = "slime", Animation = "idle", X = x, Y = y, Tag = "Enemy", Priority = 2, Group = "Enemies",
        Components = { new ComponentData { Type = "PlatformerBody" }, new ComponentData { Type = "Patrol", Fields = { ["speed"] = "0.5" } }, new ComponentData { Type = "DamageOnTouch" } },
    };

    static void WriteScenes(GameProject p)
    {
        var forest = new SceneFile
        {
            Name = "Forest_01", Width = 30, Height = 20, Map = "Forest_01", MapX = 260, MapY = 60, Template = "Platformer level",
            Music = new MusicSettings { Track = "bgm_forest", Volume = 0.8, FadeOnSceneChange = true },
            Events = new SceneEvents { OnEnter = "FadeIn(0.5s)", OnExit = "Save.Checkpoint()" },
            Backdrop = "#58B4FF",
            Actors =
            {
                Player(PlayerStart.X, PlayerStart.Y),
                Slime("Slime", 202, 288), Slime("Slime (1)", 300, 288),
                new ActorData { Name = "Question Block", Kind = ActorKind.Empty, Sprite = "block", X = 64, Y = 240, Tag = "Block", Components = { new ComponentData { Type = "QuestionBlock" } } },
                new ActorData { Name = "Goal Flag", Kind = ActorKind.TriggerZone, Sprite = "flag", X = 224, Y = 288, Tag = "Goal", ZoneW = 16, ZoneH = 48 },
                new ActorData { Name = "Exit → Cave_01", Kind = ActorKind.TriggerZone, X = 440, Y = 288, ZoneW = 16, ZoneH = 32 },
                // the HUD of art-scene-day: SCORE with the score (HUD.cs), and three hearts below that stay on the screen
                new ActorData { Name = "HUD · Score", Kind = ActorKind.TextLabel, X = 36, Y = 4, Text = "hud.score", Group = "HUD", Components = { new ComponentData { Type = "HUD", Fields = { ["previewNumber"] = "0120" } } } },
            },
            Links =
            {
                new LinkData { From = "Goal Flag", To = "Forest_02", SpawnAt = "Start (left edge)", SpawnX = 24, SpawnY = 288, Transition = Transition.Fade, Duration = 0.5, OnlyIf = "Game.coins >= 10" },
                new LinkData { From = "Exit → Cave_01", Kind = LinkKind.Door, To = "Cave_01", SpawnX = 32, SpawnY = 600, Transition = Transition.Fade, Duration = 0.5 },
            },
        };
        // three coins over the brick platform of the start screen (art-scene-day), two over the platform further right
        (int X, int Y)[] coins = [(100, 240), (112, 240), (124, 240), (276, 218), (292, 218)];
        for (var i = 0; i < coins.Length; i++)
            forest.Actors.Add(new ActorData { Name = i == 0 ? "Coin" : $"Coin ({i})", Kind = ActorKind.Pickup, Sprite = "coin", Animation = "spin", X = coins[i].X, Y = coins[i].Y, Tag = "Coin", Priority = 2, Group = "Coins",
                Components = { new ComponentData { Type = "Coin" } } });
        for (var i = 0; i < 3; i++)
            forest.Actors.Add(new ActorData
            {
                Name = i == 0 ? "Heart" : $"Heart ({i})", Kind = ActorKind.Empty, Sprite = "heart", X = 6 + i * 9, Y = StartCamera.Y + 6, Group = "HUD",
                Components = { new ComponentData { Type = "HUD", Fields = { ["pinToScreen"] = "true", ["screenX"] = $"{6 + i * 9}", ["screenY"] = "6" } } },
            });
        var scenes = new[]
        {
            new SceneFile { Name = "Title", Width = 15, Height = 10, Map = "Title", MapX = 40, MapY = 60, Music = new MusicSettings { Track = "bgm_title" },
                Actors = { new ActorData { Name = "Title Menu", Kind = ActorKind.TextLabel, X = 88, Y = 40, Text = "ui.title", Components = { new ComponentData { Type = "TitleMenu" } } } },
                // Figma "In-game · Title screen": Start / Continue / Options with the triangle cursor.
                Menu = new SceneMenu
                {
                    X = 88, Y = 72, Width = 96, Height = 48, Spacing = 14,
                    Items =
                    {
                        new SceneMenuItem { Text = "ui.menu.start", Command = MenuCommand.LoadScene, Scene = "Forest_01" },
                        new SceneMenuItem { Text = "ui.menu.continue", Command = MenuCommand.Continue },
                        new SceneMenuItem { Text = "ui.menu.options", Command = MenuCommand.Options },
                    },
                },
                Links = { } , Backdrop = "#78BEFA" },
            forest,
            new SceneFile { Name = "Forest_02", Width = 32, Height = 20, Map = "Forest_02", MapX = 480, MapY = 60, Music = new MusicSettings { Track = "bgm_forest" }, Backdrop = "#78BEFA",
                Actors = { Player(24, 288), Slime("Slime", 200, 304), new ActorData { Name = "Owl", Kind = ActorKind.Empty, Sprite = "owl", Animation = "idle", X = 300, Y = 288, Tag = "NPC", Components = { new ComponentData { Type = "Owl" } } },
                    new ActorData { Name = "Fall in pit", Kind = ActorKind.TriggerZone, X = 176, Y = 330, ZoneW = 32, ZoneH = 16 },
                    new ActorData { Name = "Summit Door", Kind = ActorKind.TriggerZone, X = 490, Y = 304, ZoneW = 16, ZoneH = 32 } },
                Links = { new LinkData { From = "Fall in pit", Kind = LinkKind.Respawn, To = "Forest_01", SpawnX = PlayerStart.X, SpawnY = PlayerStart.Y, Transition = Transition.Cut },
                    new LinkData { From = "Summit Door", Kind = LinkKind.Door, To = "Summit", SpawnX = 24, SpawnY = 144, Transition = Transition.Slide, Duration = 0.5 } } },
            new SceneFile { Name = "Cave_01", Width = 24, Height = 40, Map = "Cave_01", MapX = 260, MapY = 220, Music = new MusicSettings { Track = "bgm_cave" }, Backdrop = "#1E1A28",
                Actors = { Player(32, 600), new ActorData { Name = "Bat", Kind = ActorKind.Enemy, Sprite = "bat", Animation = "fly", X = 200, Y = 500, Tag = "Enemy", Components = { new ComponentData { Type = "ChasePlayer" }, new ComponentData { Type = "DamageOnTouch" } } },
                    new ActorData { Name = "Cave Exit", Kind = ActorKind.TriggerZone, X = 360, Y = 624, ZoneW = 16, ZoneH = 32 } },
                Links = { new LinkData { From = "Cave Exit", Kind = LinkKind.Door, To = "Forest_01", SpawnX = 424, SpawnY = 288, Transition = Transition.Fade } } },
            new SceneFile { Name = "Summit", Width = 16, Height = 10, Map = "Summit", MapX = 700, MapY = 60, Music = new MusicSettings { Track = "jingle_win" }, Backdrop = "#F8C8A0",
                Actors = { Player(24, 144), new ActorData { Name = "Slime King", Kind = ActorKind.Enemy, Sprite = "slime_king", Animation = "idle", X = 180, Y = 144, Tag = "Enemy", Components = { new ComponentData { Type = "SlimeKing" }, new ComponentData { Type = "Health", Fields = { ["hearts"] = "5" } } } },
                    new ActorData { Name = "Back Door", Kind = ActorKind.TriggerZone, X = 8, Y = 144, ZoneW = 8, ZoneH = 32 } },
                Links = { new LinkData { From = "Back Door", Kind = LinkKind.Door, To = "Forest_02", SpawnX = 470, SpawnY = 304, Transition = Transition.Fade } } },
        };
        foreach (var sc in scenes) GameProject.Save(Path.Combine(p.ScenesDir, sc.Name + ".scene"), sc);
    }

    // ---- audio, fonts, text ---------------------------------------------------------------------------

    static void WriteAudio(GameProject p)
    {
        var music = Path.Combine(p.AssetsDir, "audio", "music");
        // Figma Audio lengths: bgm_forest 1:24, bgm_cave 1:02, bgm_title 0:48, jingle_win 0:06
        File.WriteAllBytes(Path.Combine(music, "bgm_forest.xm"), ForestTheme());
        File.WriteAllBytes(Path.Combine(music, "bgm_title.xm"), XmWriter.Tune("bgm title", 120, 6, 12, "G"));          // 6 x 64 rows at 120 BPM: 0:48
        File.WriteAllBytes(Path.Combine(music, "bgm_cave.mod"), Chiptune.SimpleTune("bgm cave", 93, 13, orders: 6));   // 6 x 64 rows at 93 BPM: 1:02
        File.WriteAllBytes(Path.Combine(music, "jingle_win.mod"), Chiptune.SimpleTune("jingle win", 150, 14, orders: 1));  // 64 rows at 150 BPM: 0:06
        // the Audio lists are in creation order (T-P15): forest, cave, title, jingle
        var order = new CreationOrder();
        foreach (var f in new[] { "bgm_forest.xm", "bgm_cave.mod", "bgm_title.xm", "jingle_win.mod" }) order.Stamp(Path.Combine(music, f));
        GameProject.Save(GameProject.MetaPath(Path.Combine(music, "bgm_forest.xm")), new MusicMeta { Volume = 0.8, LoopStart = 4, FadeOnSceneChange = true });
        var sfx = Path.Combine(p.AssetsDir, "audio", "sfx");
        File.WriteAllBytes(Path.Combine(sfx, "jump.wav"), WavWriter.Encode(Sweep(400, 900, 0.21), 11025));
        File.WriteAllBytes(Path.Combine(sfx, "coin.wav"), WavWriter.Encode(Sweep(990, 1320, 0.12), 11025));
        File.WriteAllBytes(Path.Combine(sfx, "hurt.wav"), WavWriter.Encode(Sweep(300, 120, 0.30), 11025));
        File.WriteAllBytes(Path.Combine(sfx, "explode.wav"), WavWriter.Encode(ModWriter.Noise(7056, 5), 11025));
        File.WriteAllBytes(Path.Combine(sfx, "blip.wav"), WavWriter.Encode(WavWriter.Tone(1200, 0.03, 11025, true), 11025));
        foreach (var f in new[] { "jump.wav", "coin.wav", "hurt.wav", "explode.wav", "blip.wav" }) order.Stamp(Path.Combine(sfx, f));   // T-P15 / T-P25 list order
        foreach (var (name, ch, note, sweepUp, len) in new[] { ("jump", DmgChannelKind.Square1, "C5", true, 0.21), ("coin", DmgChannelKind.Square2, "B5", false, 0.12),
                     ("hurt", DmgChannelKind.Noise, "C3", false, 0.30), ("blip", DmgChannelKind.Square2, "C6", false, 0.03), ("laser", DmgChannelKind.Square1, "C6", false, 0.25) })
            GameProject.Save(Path.Combine(p.SfxDir, name + ".sfx"), new SfxFile
            {
                Name = name, Channel = ch, StartNote = note, SweepUp = sweepUp, Length = len, Duty = name == "laser" ? 0.125 : 0.5,
                // SFX Designer (T-P25): jump.sfx "Sweep up 2" (shift 6)
                SweepShift = name == "jump" ? 6 : 3,
                // blip is baked: blip.wav is the sample that plays (Audio · GBA channels: Square 2 "SFX coin")
                Bake = name == "blip",
            });
        foreach (var f in new[] { "jump.sfx", "coin.sfx", "hurt.sfx", "blip.sfx", "laser.sfx" }) order.Stamp(Path.Combine(p.SfxDir, f));   // T-P25 list order
    }

    // The loudness of bgm_forest from start to end as the Audio page's overview draws it (T-P15): 160 bars of 0..56 px.
    static readonly int[] ForestLoudness =
    [
        55, 56, 51, 41, 28, 15, 27, 35, 39, 38, 33, 27, 20, 16, 15, 17, 21, 27, 31, 33, 31, 25, 15, 28, 40, 50, 55, 54, 48, 36, 22, 23, 36, 45, 48, 46, 40, 31, 22, 15,
        20, 21, 18, 16, 20, 23, 22, 18, 20, 29, 39, 46, 50, 48, 41, 28, 16, 31, 44, 52, 54, 51, 43, 32, 20, 21, 28, 31, 30, 26, 21, 16, 16, 15, 20, 26, 33, 39, 41, 39,
        32, 20, 23, 37, 49, 55, 56, 51, 41, 27, 17, 29, 37, 41, 40, 35, 28, 21, 16, 16, 15, 19, 24, 28, 31, 29, 23, 16, 28, 40, 49, 54, 53, 47, 35, 20, 25, 38, 47, 50,
        48, 42, 32, 22, 16, 21, 23, 21, 17, 17, 20, 20, 17, 20, 29, 38, 45, 48, 46, 39, 27, 18, 32, 45, 53, 55, 52, 43, 31, 19, 22, 30, 33, 33, 29, 23, 18, 15, 15, 19,
    ];

    /// <summary>
    /// bgm_forest.xm as the Figma Audio page shows it (T-P15): 128 BPM at speed 6, 12 patterns (eleven of 64 rows and a
    /// 16-row turnaround, 1:24), looping from pattern 4. Six instruments walk one arpeggio (C E G C' B G E) a few steps
    /// apart, Lead / Drums / Arp on the even rows and Bass / Pad / FX on the odd rows, over the chords C C F G; the
    /// sample peaks and volumes give the mixer levels of the frame (Lead -6 dB, Bass -4, Drums -3, Pad -12), and the
    /// global volume (Gxx on the FX column) the swells of its overview.
    /// </summary>
    static byte[] ForestTheme()
    {
        var xm = new XmWriter { Title = "bgm forest", Bpm = 128, Tempo = 6, Channels = 6, Restart = 4 };
        static sbyte[] Peak(sbyte[] wave, int peak) => [.. wave.Select(v => (sbyte)Math.Round(v * peak / 100.0))];
        xm.Instruments.Add(new("Lead", ModWriter.Waveform("pulse25"), 40, true, 12));
        xm.Instruments.Add(new("Bass", Peak(ModWriter.Waveform("triangle"), 118), 44, true, -12));
        // each noise value is held for 4 samples, so every note (steps under 3) plays its loudest value (90, scaled to
        // 121) as it is: 121 / 128 x 48 / 64 is -2.99 dB
        xm.Instruments.Add(new("Drums", Peak([.. ModWriter.Noise(200, 11).SelectMany(v => Enumerable.Repeat(v, 4))], 134), 48, false));
        xm.Instruments.Add(new("Pad", Peak(ModWriter.Waveform("sine"), 40), 52, true, 0));
        xm.Instruments.Add(new("Arp", Peak(ModWriter.Waveform("square"), 50), 56, true, 24));
        xm.Instruments.Add(new("FX", Peak(ModWriter.Waveform("saw"), 50), 60, true, 12));
        int[] arpeggio = [.. new[] { "C-4", "E-4", "G-4", "C-5", "B-4", "G-4", "E-4" }.Select(XmWriter.Note)];
        int[] offset = [0, 2, 3, 5, 6, 1];   // Lead, Bass, Drums, Pad, Arp, FX
        int[] chord = [0, 0, 5, 7];
        const int patterns = 12, songRows = (patterns - 1) * 64 + 16;
        var start = 0;
        for (var pi = 0; pi < patterns; pi++)
        {
            var rows = pi < patterns - 1 ? 64 : 16;
            var pattern = xm.NewPattern(rows);
            for (var r = 0; r < rows; r++)
            {
                var step = r / 2 + 6;   // row 16: the Lead starts the arpeggio (step 14, a multiple of 7)
                for (var c = 0; c < 6; c++)
                    if (r % 2 == (c is 1 or 3 or 5 ? 1 : 0))
                        pattern[r, c] = new(arpeggio[(step + offset[c]) % 7] + chord[pi % 4], c + 1);
                var loud = ForestLoudness[(start + r) * ForestLoudness.Length / songRows];
                pattern[r, 5] = pattern[r, 5] with { Effect = 0x10, Param = Math.Clamp((int)Math.Round(64.0 * loud / 56), 1, 64) };
            }
            xm.Patterns.Add(pattern);
            xm.Order.Add(pi);
            start += rows;
        }
        return xm.Encode();
    }

    static sbyte[] Sweep(double from, double to, double seconds)
    {
        var n = (int)(seconds * 11025);
        var d = new sbyte[n];
        double phase = 0;
        for (var i = 0; i < n; i++)
        {
            var f = from + (to - from) * i / n;
            phase += f / 11025;
            d[i] = (sbyte)((phase % 1 < 0.5 ? 90 : -90) * (1 - (double)i / n));
        }
        return d;
    }

    static void WriteFonts(GameProject p)
    {
        var fonts = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts");
        File.Copy(Path.Combine(fonts, "NotoSansThai-Regular.ttf"), Path.Combine(p.FontsDir, "NotoSansThai-Regular.ttf"), true);
        File.Copy(Path.Combine(fonts, "fallback-latin.ttf"), Path.Combine(p.FontsDir, "NotoSans.ttf"), true);
        GameProject.Save(Path.Combine(p.FontsDir, "thai_vwf.font"), new FontFile
        {
            Name = "thai_vwf", Source = "NotoSansThai-Regular.ttf", Size = 12, Variable = true, CharacterSets = { "Basic Latin", "Thai", "Symbols" },
            LineHeight = 14, Baseline = 10, Spacing = 1, Bpp = 2, DropShadow = true, DrawTo = 2,   // Font Manager · Draw to "BG 2 text layer"
        });
        GameProject.Save(Path.Combine(p.FontsDir, "pixel8.font"), new FontFile
        {
            Name = "pixel8", Source = "NotoSans.ttf", Size = 8, Variable = false, FixedWidth = 8, CharacterSets = { "Basic Latin" }, LineHeight = 8, Baseline = 7, Bpp = 1,
        });
        GameProject.Save(Path.Combine(p.FontsDir, "title_big.font"), new FontFile
        {
            // Font Manager lists it as "16×16": fixed 16 pixel cells
            Name = "title_big", Source = "NotoSans.ttf", Size = 16, Variable = false, FixedWidth = 16, CharacterSets = { "Basic Latin" }, LineHeight = 18, Baseline = 14, Bpp = 2, DropShadow = true,
        });
    }

    // Figma Localization: 340 keys (Dialogue 112, UI & menus 86, Items 48, other 94); ไทย 326 / 340; 日本語 0 / 340.
    static void WriteStrings(GameProject p)
    {
        var en = new StringTable { Language = "en" };
        var th = new StringTable { Language = "th" };
        var meta = new StringMeta();
        void Add(string key, string e, string? t, string group, string speaker = "", string notes = "")
        {
            en.Strings[key] = e;
            if (t != null) th.Strings[key] = t;
            meta.Keys[key] = new StringInfo { Group = group, Speaker = speaker, Notes = notes, BoxWidth = 224, MaxLines = group == "Dialogue" ? 3 : 1 };
        }
        Add("dlg.forest.hopper_01", "Where did all the coins go?", "เหรียญหายไปไหนหมดนะ?", "Dialogue", "Hopper", "Hopper is worried; keep it short so it fits two lines.");
        Add("dlg.forest.hopper_02", "I have to find them before sunset!", "ต้องรีบตามหาก่อนพระอาทิตย์ตก!", "Dialogue", "Hopper");
        Add("dlg.forest.owl_01", "A slime took them to the cave!", "เจ้าสไลม์ขโมยไปไว้ในถ้ำน่ะสิ!", "Dialogue", "Owl");
        Add("dlg.forest.choice_cave", "Go to the cave", "ไปที่ถ้ำ", "Dialogue", "Hopper");
        Add("dlg.forest.choice_again", "Ask again", "ถามอีกครั้ง", "Dialogue", "Hopper");
        Add("ui.title", "SKY HOPPER", "สกายฮอปเปอร์", "UI & menus");
        Add("ui.menu.start", "Start", "เริ่มเกม", "UI & menus");
        Add("ui.menu.continue", "Continue", "เล่นต่อ", "UI & menus");
        Add("ui.menu.options", "Options", "ตั้งค่า", "UI & menus");
        Add("hud.score", "SCORE", "คะแนน", "UI & menus");
        Add("item.potion.name", "Potion", "ยาฟื้นพลัง", "Items");
        Add("item.potion.desc", "Restores 2 hearts. Tastes like strawberries.", "ฟื้นพลัง 2 หัวใจ รสสตรอว์เบอร์รี", "Items");
        Add("shop.welcome", "Welcome! Take a look around.", null, "Dialogue", "Shopkeeper");
        // Figma Localization lists shop.welcome and boss.summit.intro as missing in ไทย
        Add("boss.summit.intro", "You made it all the way up here?", null, "Dialogue", "Slime King");
        string[] thaiWords = ["ป่า", "ถ้ำ", "ภูเขา", "เหรียญ", "หัวใจ", "ดาว", "อัญมณี", "สไลม์", "นกฮูก", "ค้างคาว", "สะพาน", "แม่น้ำ", "ประตู", "กุญแจ", "หีบ", "ร้านค้า"];
        string[] enWords = ["forest", "cave", "mountain", "coin", "heart", "star", "gem", "slime", "owl", "bat", "bridge", "river", "door", "key", "chest", "shop"];
        var groups = new (string Prefix, string Group, int Count)[] { ("dlg.extra", "Dialogue", 112 - 7), ("ui.extra", "UI & menus", 86 - 5), ("item.extra", "Items", 48 - 2), ("misc", "", 95) };
        var missingTh = 14 - 2;  // shop.welcome and boss.summit.intro are already missing
        foreach (var (prefix, group, count) in groups)
            for (var i = 0; i < count && en.Strings.Count < 340; i++)
            {
                var w = i % enWords.Length;
                var key = $"{prefix}.{i:D3}";
                var keepTh = !(missingTh > 0 && group == "Dialogue" && i % 7 == 3);
                if (!keepTh) missingTh--;
                Add(key, $"The {enWords[w]} number {i + 1}.", keepTh ? $"{thaiWords[w]}หมายเลข {i + 1}" : null, group);
            }
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), en);
        GameProject.Save(Path.Combine(p.TextDir, "strings.th.json"), th);
        GameProject.Save(Path.Combine(p.TextDir, "strings.ja.json"), new StringTable { Language = "ja" });
        GameProject.Save(Path.Combine(p.TextDir, "strings.meta.json"), meta);
    }

    // ---- database ------------------------------------------------------------------------------------

    static void WriteDatabase(GameProject p)
    {
        var items = new TableFile
        {
            Name = "Items",
            Fields =
            {
                new FieldDef { Name = "id", Type = "enum" }, new FieldDef { Name = "name", Type = "text", Localized = true },
                new FieldDef { Name = "type", Type = "enum", EnumValues = { "Currency", "Consumable", "Key item", "Equip", "Power-up", "Material", "Treasure" } },
                new FieldDef { Name = "price", Type = "int", Default = "0" }, new FieldDef { Name = "icon", Type = "sprite" },
                new FieldDef { Name = "power", Type = "fixed" }, new FieldDef { Name = "stackable", Type = "bool" },
                new FieldDef { Name = "effect", Type = "script" }, new FieldDef { Name = "description", Type = "text", Localized = true },
            },
        };
        (string Id, string Type, string Price, string Icon, string Power, string Stack, string Effect)[] rows =
        [
            ("Coin", "Currency", "1", "coin", "", "yes", "score += 10"), ("Heart", "Consumable", "0", "heart", "1.00", "yes", "lives += 1"),
            ("Potion", "Consumable", "30", "heart", "2.00", "yes", "Heal(power)"), ("Key", "Key item", "0", "", "", "no", "Unlock(door)"),
            ("Feather", "Equip", "120", "", "0.50", "no", "doubleJump = true"), ("Bomb", "Consumable", "45", "", "3.00", "yes", "Explode(16)"),
            ("Star", "Power-up", "0", "star", "8.00", "no", "Invincible(8s)"), ("SlimeGel", "Material", "8", "", "", "yes", ""),
            ("Gem", "Treasure", "200", "gem", "", "yes", ""),
        ];
        foreach (var r in rows)
            items.Rows.Add(new() { ["id"] = r.Id, ["name"] = r.Id == "Potion" ? "item.potion.name" : $"item.extra.{items.Rows.Count:D3}", ["type"] = r.Type, ["price"] = r.Price,
                ["icon"] = r.Icon, ["power"] = r.Power, ["stackable"] = r.Stack, ["effect"] = r.Effect, ["description"] = r.Id == "Potion" ? "item.potion.desc" : "" });
        for (var i = items.Rows.Count; i < 24; i++)
            items.Rows.Add(new() { ["id"] = $"Item{i + 1}", ["name"] = $"item.extra.{i:D3}", ["type"] = "Material", ["price"] = $"{i * 5}", ["stackable"] = "yes" });
        // the file is items.db (Figma Database tab and Build Report); the table and its script class are Items
        var order = new CreationOrder();
        GameProject.Save(Path.Combine(p.DataDir, "items.db"), items);
        order.Stamp(Path.Combine(p.DataDir, "items.db"));
        void Simple(string name, int count, params (string Field, string Type)[] fields)
        {
            var t = new TableFile { Name = name, Fields = { new FieldDef { Name = "id", Type = "enum" } } };
            foreach (var (f, ty) in fields) t.Fields.Add(new FieldDef { Name = f, Type = ty });
            for (var i = 0; i < count; i++)
            {
                var row = new Dictionary<string, string> { ["id"] = name == "Enemies" ? EnemyIds[i] : $"{DataId(name)}{i + 1}" };
                foreach (var (f, ty) in fields) row[f] = ty == "fixed" ? $"{0.5 + i * 0.25:0.00}" : ty == "bool" ? (i % 2 == 0 ? "true" : "false") : $"{i * 3 + 1}";
                t.Rows.Add(row);
            }
            GameProject.Save(Path.Combine(p.DataDir, name + ".db"), t);
            order.Stamp(Path.Combine(p.DataDir, name + ".db"));
        }
        Simple("Enemies", 9, ("hp", "int"), ("speed", "fixed"), ("damage", "int"));
        Simple("Shops", 3, ("slots", "int"), ("discount", "fixed"));
        Simple("Quests", 7, ("reward", "int"), ("repeatable", "bool"));
        Simple("Levels", 5, ("par", "int"), ("stars", "int"));
        Simple("Dialogue", 112, ("speaker", "int"), ("line", "int"));
    }

    // Enemies rows by name (Figma New Table: "Scripts use it as Enemies.Get(EnemyId.Bat)")
    static readonly string[] EnemyIds = ["Bat", "Slime", "SlimeKing", "Snail", "Bee", "Crab", "Ghost", "Mole", "Frog"];

    static string DataId(string table) => table switch { "Enemies" => "Enemy", "Shops" => "Shop", "Quests" => "Quest", "Levels" => "Level", _ => "Line" };

    // ---- scripts (C# GBA subset; PlayerController is the Figma sample, including its float warning) -----------

    static void WriteScripts(GameProject p)
    {
        var dir = Path.Combine(p.ScriptsDir);
        void Script(string name, string body) => File.WriteAllText(Path.Combine(dir, name + ".cs"), "using GBA;\n\n" + body.Trim() + "\n");
        Script("PlayerController", """
public class PlayerController : Behaviour
{
    [Range(0.5, 3)] public fixed speed = 1.5;
    public fixed jumpPower = 3.5;
    public int lives = 3;
    float coyoteTime = 0.1f;

    PlatformerBody body;

    void Start() => body = Get<PlatformerBody>();

    void Update()
    {
        body.vx = Input.AxisX * speed;

        if (Input.Pressed(Key.A) && body.OnGround)
        {
            body.vy = -jumpPower;
            Sound.Play("jump");
        }
    }

    void OnTouch(Actor other)
    {
        if (other.Is<Coin>()) { Game.score += 10; other.Destroy(); }
    }
}
""");
        Script("Coin", "public class Coin : Behaviour\n{\n    void OnTouch(Actor other)\n    {\n        if (other.tag == \"Player\") Sound.Play(\"coin\");\n    }\n}");
        Script("Slime", "public class Slime : Behaviour\n{\n    public fixed speed = 0.5;\n    public int damage = 1;\n}");
        // GameState.cs also holds the items.db effect actions (Figma Database Editor, Effect column): Figma's
        // Build ROM dialog counts 12 scripts.
        Script("GameState", """
public class GameState : Behaviour
{
    void Start()
    {
        Game.lives = 3;
    }
}

// items.db effect cells call these by name.
[DatabaseActions]
public static class ItemEffects
{
    public const int door = 1;      // Unlock(door): flag 1 opens the cave door
    public static bool doubleJump;  // Feather: doubleJump = true

    public static void Unlock(int flag) { Game.flags[flag] = true; }

    public static void Explode(int radius) { Camera.Shake(radius / 4, 20); }

    public static void Invincible(fixed seconds)
    {
        var player = Scene.Find("Player");
        if (player == null) return;
        var health = player.Get<Health>();
        if (health != null) health.invincibleLeft = (int)(seconds * 60);
    }
}
""");
        // the HUD of Forest_01: on the Text Label "SCORE" and the score in 4 digits; a heart stays at its screen position
        Script("HUD", """
public class HUD : Behaviour
{
    public bool pinToScreen = false;   // hearts: stay at (screenX, screenY) on the screen while the camera moves
    public int screenX = 0;
    public int screenY = 0;
    public string previewNumber = "";   // the editor shows it after the label text (the game writes the real score)
    int lastScore = -1;

    void Update()
    {
        if (pinToScreen)
        {
            actor.pos.x = Camera.X + screenX;
            actor.pos.y = Camera.Y + screenY;
            return;
        }
        if (Game.score != lastScore)
        {
            lastScore = Game.score;
            actor.SetLabelNumber(Game.score, 4);
        }
    }
}
""");
        Script("TitleMenu", "public class TitleMenu : Behaviour\n{\n    void Update()\n    {\n        if (Input.Pressed(Key.Start)) Scene.Load(\"Forest_01\");\n    }\n}");
        Script("CameraFollow", "public class CameraFollow : Behaviour\n{\n    public int shakeFrames = 0;\n}");
        Script("QuestionBlock", "public class QuestionBlock : Behaviour\n{\n    public int coins = 3;\n    void OnTouch(Actor other)\n    {\n        if (coins > 0 && other.tag == \"Player\") { coins -= 1; Game.score += 10; Sound.Play(\"coin\"); }\n    }\n}");
        Script("GoalFlag", "public class GoalFlag : Behaviour\n{\n    public bool reached = false;\n}");
        Script("Owl", "public class Owl : Behaviour\n{\n    public int talks = 0;\n}");
        Script("SlimeKing", "public class SlimeKing : Behaviour\n{\n    public fixed speed = 0.75;\n    void Update()\n    {\n        actor.pos.x += speed;\n        if (actor.pos.x > 200 || actor.pos.x < 120) speed = -speed;\n    }\n}");
        Script("Cutscenes", "public class Cutscenes : Behaviour\n{\n    public int seen = 0;\n}");
    }
}
