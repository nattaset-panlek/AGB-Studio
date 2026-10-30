using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content;

/// <summary>Creates small projects from original generated content (tests, templates, samples).</summary>
public static class SampleProjects
{
    /// <summary>A two-scene platformer that uses only built-in behaviours (no scripts).</summary>
    public static GameProject CreatePlatformerMini(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var settings = new ProjectFile();
        settings.General.GameTitle = "Mini Platformer";
        settings.General.RomTitle = "MINIPLAT";
        settings.General.GameCode = "AMPE";
        settings.General.StartScene = "Forest_01";
        settings.Save.Fields.AddRange([new SaveField { Name = "score", Type = "int" }, new SaveField { Name = "lives", Type = "byte" }]);
        var p = GameProject.Create(dir, "MiniPlatformer", settings);
        WriteCommonArt(p);

        var tiles = Path.Combine(p.AssetsDir, "tilesets", "forest_tiles.png");
        PixelArt.ForestTiles().Save(tiles);
        GameProject.Save(GameProject.MetaPath(tiles), ForestTilesetMeta());

        GameProject.Save(Path.Combine(p.MapsDir, "Forest_01.map"), GroundMap("Forest_01", 30, 10, gapFrom: 12, gapTo: 13, wallX: 20));
        GameProject.Save(Path.Combine(p.MapsDir, "Forest_02.map"), GroundMap("Forest_02", 16, 10, -1, -1, -1));

        var scene1 = new SceneFile
        {
            Name = "Forest_01", Width = 30, Height = 10, Map = "Forest_01",
            Music = new MusicSettings { Track = "tune" },
            Actors =
            {
                new ActorData
                {
                    Name = "Player", Kind = ActorKind.Player, Sprite = "hero", Animation = "idle", X = 40, Y = 100, Tag = "Player",
                    Components = { new ComponentData { Type = "PlatformerBody" }, new ComponentData { Type = "Health", Fields = { ["hearts"] = "3" } } },
                },
                new ActorData
                {
                    Name = "Slime", Kind = ActorKind.Enemy, Sprite = "slime", X = 260, Y = 144, Tag = "Enemy", Priority = 2, Group = "Enemies",
                    Components = { new ComponentData { Type = "PlatformerBody" }, new ComponentData { Type = "Patrol", Fields = { ["speed"] = "0.5" } }, new ComponentData { Type = "DamageOnTouch" } },
                },
                new ActorData { Name = "Coin", Kind = ActorKind.Pickup, Sprite = "coin", X = 90, Y = 140, Tag = "Coin", Priority = 2, Group = "Coins",
                    Components = { new ComponentData { Type = "SoundOnEvent", Fields = { ["event"] = "Touch", ["sound"] = "jump" } } } },
                new ActorData { Name = "Goal Flag", Kind = ActorKind.TriggerZone, Sprite = "flag", X = 440, Y = 144, ZoneW = 16, ZoneH = 32 },
            },
            Links = { new LinkData { From = "Goal Flag", To = "Forest_02", SpawnX = 40, SpawnY = 100, Transition = Transition.Fade, Duration = 0.25 } },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Forest_01.scene"), scene1);
        var scene2 = new SceneFile
        {
            Name = "Forest_02", Width = 16, Height = 10, Map = "Forest_02",
            Actors = { new ActorData { Name = "Player", Kind = ActorKind.Player, Sprite = "hero", X = 30, Y = 30, Tag = "Player", Components = { new ComponentData { Type = "PlatformerBody" } } } },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Forest_02.scene"), scene2);
        return p;
    }

    public static void WriteCommonArt(GameProject p)
    {
        var sprites = Path.Combine(p.AssetsDir, "sprites");
        PixelArt.Hero8().Save(Path.Combine(sprites, "hero.png"));
        GameProject.Save(GameProject.MetaPath(Path.Combine(sprites, "hero.png")), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 2, Y = 2, W = 12, H = 14 },
            Animations =
            {
                new AnimationData { Name = "idle", Frames = { 0, 1 }, Ticks = 20 },
                new AnimationData { Name = "run", Frames = { 2, 3, 4, 5 }, Ticks = 6 },
                new AnimationData { Name = "jump", Frames = { 6 }, Loop = false },
                new AnimationData { Name = "hurt", Frames = { 7 }, Ticks = 6 },
            },
        });
        PixelArt.Slime4().Save(Path.Combine(sprites, "slime.png"));
        GameProject.Save(GameProject.MetaPath(Path.Combine(sprites, "slime.png")), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 12, OriginX = 8, OriginY = 12, Collider = new ColliderData { X = 2, Y = 2, W = 12, H = 10 },
            Animations = { new AnimationData { Name = "idle", Frames = { 0, 1, 2, 3 }, Ticks = 8 } },
        });
        PixelArt.Coin6().Save(Path.Combine(sprites, "coin.png"));
        GameProject.Save(GameProject.MetaPath(Path.Combine(sprites, "coin.png")), new SpriteMeta
        {
            FrameWidth = 8, FrameHeight = 8, OriginX = 4, OriginY = 8, Collider = new ColliderData { X = 0, Y = 0, W = 8, H = 8 },
            Animations = { new AnimationData { Name = "spin", Frames = { 0, 1, 2, 3, 4, 5 }, Ticks = 5 } },
        });
        PixelArt.Flag().Save(Path.Combine(sprites, "flag.png"));
        GameProject.Save(GameProject.MetaPath(Path.Combine(sprites, "flag.png")), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 32, OriginX = 4, OriginY = 32, Collider = new ColliderData { X = 0, Y = 0, W = 16, H = 32 },
        });
        var music = Path.Combine(p.AssetsDir, "audio", "music");
        var sfx = Path.Combine(p.AssetsDir, "audio", "sfx");
        File.WriteAllBytes(Path.Combine(music, "tune.mod"), Chiptune.SimpleTune("agb tune", 120));
        File.WriteAllBytes(Path.Combine(sfx, "jump.wav"), WavWriter.Encode(WavWriter.Tone(660, 0.12, 11025, square: true), 11025));
    }

    public static TilesetMeta ForestTilesetMeta() => new()
    {
        Tiles =
        {
            [PixelArt.TGrassTop] = new TileProperties { Name = "grass_top", Collision = CollisionType.Solid, Tag = "ground" },
            [PixelArt.TDirt] = new TileProperties { Name = "dirt", Collision = CollisionType.Solid },
            [PixelArt.TPlatform] = new TileProperties { Name = "platform", Collision = CollisionType.OneWay },
            [PixelArt.TLadder] = new TileProperties { Name = "ladder", Collision = CollisionType.Ladder },
            [PixelArt.TWater] = new TileProperties { Name = "water", Collision = CollisionType.Water },
            [PixelArt.TSpikes] = new TileProperties { Name = "spikes", Collision = CollisionType.Hazard },
            [PixelArt.TStone] = new TileProperties { Name = "stone", Collision = CollisionType.Solid },
            [PixelArt.TGrassLeft] = new TileProperties { Name = "grass_left", Collision = CollisionType.Solid },
            [PixelArt.TGrassRight] = new TileProperties { Name = "grass_right", Collision = CollisionType.Solid },
        },
        AutoTiles =
        {
            new AutoTileSet
            {
                Name = "Grass edges",
                Rules =
                {
                    new AutoTileRule { Name = "Top edge", Mask = [2, 0, 2, 1, 1, 1, 2, 1, 2], Tile = PixelArt.TGrassTop },
                    new AutoTileRule { Name = "Left edge", Mask = [2, 0, 2, 0, 1, 1, 2, 1, 2], Tile = PixelArt.TGrassLeft },
                    new AutoTileRule { Name = "Right edge", Mask = [2, 0, 2, 1, 1, 0, 2, 1, 2], Tile = PixelArt.TGrassRight },
                    new AutoTileRule { Name = "Inner", Mask = [2, 1, 2, 1, 1, 1, 2, 1, 2], Tile = PixelArt.TDirt },
                },
            },
        },
    };

    /// <summary>Ground along the bottom two rows with an optional gap and a two-tile wall.</summary>
    public static MapFile GroundMap(string name, int w, int h, int gapFrom, int gapTo, int wallX)
    {
        var cells = new int[w * h];
        for (var x = 0; x < w; x++)
        {
            if (x >= gapFrom && x <= gapTo) continue;
            cells[(h - 1) * w + x] = PixelArt.TGrassTop;
        }
        if (wallX >= 0)
        {
            cells[(h - 2) * w + wallX] = PixelArt.TStone;
            cells[(h - 3) * w + wallX] = PixelArt.TStone;
        }
        for (var x = 5; x <= 8 && x < w; x++) cells[(h - 4) * w + x] = PixelArt.TPlatform;
        return new MapFile
        {
            Name = name, Tileset = "forest_tiles", Width = w, Height = h, CollisionLayer = 0,
            Layers = { new MapLayerData { Name = "Ground", Bg = 1, Priority = 1, Cells = cells.ToList() } },
        };
    }
}
