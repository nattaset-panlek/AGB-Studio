using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Top-down Adventure template (Launcher · Templates, P15.02): walk in 8 directions around a village, talk to the
/// elder, push a crate aside to reach a key, unlock the house door and go inside.
/// </summary>
public static class TopDownTemplate
{
    public const int TGrass = 0, TPath = 1, TWater = 2, TTree = 3, TWall = 4, TRoof = 5, TFlowers = 6, TFloor = 7, TFence = 8;
    public const int VillageW = 20, VillageH = 14;

    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "ATDE", "Title", s =>
        {
            s.Save.Fields.AddRange([new SaveField { Name = "keys", Type = "int" }, new SaveField { Name = "doorOpen", Type = "bool" }]);
            s.Tags = ["Player", "NPC", "Crate", "Door", "Key"];
            s.Input[0].Action = "Talk";  // A: talk and interact
        });
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("npc.elder.1", "Welcome to the village, traveller.", "ยินดีต้อนรับสู่หมู่บ้าน นักเดินทาง", "Dialogue"),
            ("npc.elder.2", "My house key is behind the crate. Push it aside!", "กุญแจบ้านข้าอยู่หลังลังไม้ ผลักมันออกไปสิ", "Dialogue"),
            ("npc.elder.3", "Thank you! Make yourself at home.", "ขอบใจนะ ตามสบายเลย", "Dialogue"),
            ("door.locked", "The door is locked.", "ประตูล็อกอยู่", "Dialogue"),
            ("item.key", "You found the house key!", "ได้กุญแจบ้านแล้ว!", "Dialogue"),
            ("house.welcome", "A warm, quiet house.", "บ้านที่อบอุ่นและเงียบสงบ", "Dialogue"),
        ]));
        WriteArt(p);
        TemplateKit.Music(p, "village_theme", XmWriter.Tune("Village theme", 104, 4, 11, "F"));
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 96, 2, 5, "D"));
        TemplateKit.Sfx(p, "push", 180, 120, 0.15);
        TemplateKit.Sfx(p, "key", 880, 1760, 0.2);
        TemplateKit.Sfx(p, "door", 220, 440, 0.25);
        TemplateKit.Sfx(p, "talk", 700, 700, 0.04, 0.4);
        WriteMaps(p);
        WriteScenes(p);
        WriteScripts(p);
        return p;
    }

    static string RomTitle(string name)
    {
        var t = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return t.Length == 0 ? "ADVENTURE" : t[..Math.Min(12, t.Length)];
    }

    // ---- art --------------------------------------------------------------------------------------------

    static readonly uint GrassA = Canvas.Rgb(96, 184, 88), GrassB = Canvas.Rgb(80, 164, 76), PathA = Canvas.Rgb(214, 180, 120), PathB = Canvas.Rgb(190, 156, 100);
    static readonly uint Leaf = Canvas.Rgb(48, 128, 64), LeafLight = Canvas.Rgb(80, 160, 80), Trunk = Canvas.Rgb(120, 80, 48);
    static readonly uint WallC = Canvas.Rgb(232, 216, 184), WallDark = Canvas.Rgb(184, 160, 128), RoofC = Canvas.Rgb(200, 72, 64), RoofDark = Canvas.Rgb(150, 48, 48);
    static readonly uint FloorC = Canvas.Rgb(176, 128, 88), FloorDark = Canvas.Rgb(144, 100, 64), Skin = Canvas.Rgb(248, 200, 160), Cloth = Canvas.Rgb(64, 96, 200);

    public static Canvas Tiles()
    {
        var c = new Canvas(16 * 9, 16);
        void T(int i, Action<int> d) => d(i * 16);
        T(TGrass, x => { c.Rect(x, 0, 16, 16, GrassA); foreach (var (a, b) in new[] { (3, 4), (11, 2), (7, 10), (13, 12) }) c.Rect(x + a, b, 1, 2, GrassB); });
        T(TPath, x => { c.Rect(x, 0, 16, 16, PathA); foreach (var (a, b) in new[] { (2, 3), (10, 6), (5, 12), (13, 13) }) c.Rect(x + a, b, 2, 1, PathB); });
        T(TWater, x => { c.Rect(x, 0, 16, 16, PixelArt.Water); c.Rect(x + 2, 4, 5, 1, PixelArt.WaterLight); c.Rect(x + 9, 10, 5, 1, PixelArt.WaterLight); });
        T(TTree, x => { c.Rect(x, 0, 16, 16, GrassA); c.Ellipse(x + 7.5, 6.5, 7, 6.5, Leaf); c.Ellipse(x + 6, 5, 3, 2.5, LeafLight); c.Rect(x + 6, 12, 4, 4, Trunk); });
        T(TWall, x => { c.Rect(x, 0, 16, 16, WallC); c.Rect(x, 7, 16, 1, WallDark); c.Rect(x, 15, 16, 1, WallDark); c.Rect(x + 4, 3, 8, 3, PixelArt.WaterLight); });
        T(TRoof, x => { c.Rect(x, 0, 16, 16, RoofC); for (var y = 3; y < 16; y += 4) c.Rect(x, y, 16, 1, RoofDark); });
        T(TFlowers, x => { c.Rect(x, 0, 16, 16, GrassA); foreach (var (a, b) in new[] { (3, 3), (11, 5), (6, 11), (12, 12) }) { c.Rect(x + a, b, 2, 2, PixelArt.Gold); c.Set(x + a, b + 2, Leaf); } });
        T(TFloor, x => { c.Rect(x, 0, 16, 16, FloorC); c.Rect(x, 7, 16, 1, FloorDark); c.Rect(x, 15, 16, 1, FloorDark); c.Rect(x + 5, 0, 1, 7, FloorDark); c.Rect(x + 11, 8, 1, 7, FloorDark); });
        T(TFence, x => { c.Rect(x, 0, 16, 16, GrassA); c.Rect(x, 5, 16, 2, PixelArt.Wood); c.Rect(x, 10, 16, 2, PixelArt.Wood); c.Rect(x + 2, 3, 2, 12, PixelArt.Wood); c.Rect(x + 12, 3, 2, 12, PixelArt.Wood); });
        return c;
    }

    public static TilesetMeta TilesMeta() => new()
    {
        Tiles =
        {
            [TGrass] = new TileProperties { Name = "grass" },
            [TPath] = new TileProperties { Name = "path" },
            [TWater] = new TileProperties { Name = "water", Collision = CollisionType.Solid },
            [TTree] = new TileProperties { Name = "tree", Collision = CollisionType.Solid },
            [TWall] = new TileProperties { Name = "wall", Collision = CollisionType.Solid },
            [TRoof] = new TileProperties { Name = "roof", Collision = CollisionType.Solid },
            [TFlowers] = new TileProperties { Name = "flowers" },
            [TFloor] = new TileProperties { Name = "floor" },
            [TFence] = new TileProperties { Name = "fence", Collision = CollisionType.Solid },
        },
    };

    /// <summary>Top-down hero: frames down 0-1, up 2-3, side 4-5 (16 x 16).</summary>
    public static Canvas Hero()
    {
        var c = new Canvas(16 * 6, 16);
        for (var f = 0; f < 6; f++)
        {
            var x = f * 16;
            var step = f % 2;
            c.Rect(x + 4, 1, 8, 7, Skin);                       // head
            c.Rect(x + 4, 1, 8, 3, PixelArt.Ink);               // hair
            if (f < 2) { c.Set(x + 6, 5, PixelArt.Ink); c.Set(x + 9, 5, PixelArt.Ink); }
            if (f >= 4) c.Set(x + 10, 5, PixelArt.Ink);
            c.Rect(x + 3, 8, 10, 5, Cloth);                      // body
            c.Rect(x + 4 + step * 4, 13, 3, 3, PixelArt.Ink);    // feet
            c.Rect(x + 9 - step * 4, 13, 3, 2, PixelArt.Ink);
        }
        return c;
    }

    public static Canvas Elder()
    {
        var c = new Canvas(32, 16);
        for (var f = 0; f < 2; f++)
        {
            var x = f * 16;
            c.Rect(x + 4, 1 + f, 8, 7, Skin);
            c.Rect(x + 3, 1 + f, 10, 2, PixelArt.White);         // white hair
            c.Rect(x + 5, 6 + f, 6, 2, PixelArt.White);          // beard
            c.Rect(x + 3, 9, 10, 7, Canvas.Rgb(120, 72, 160));   // robe
        }
        return c;
    }

    public static Canvas Crate()
    {
        var c = new Canvas(16, 16);
        c.Rect(0, 0, 16, 16, PixelArt.Wood);
        c.Rect(0, 0, 16, 2, PixelArt.DirtDark);
        c.Rect(0, 14, 16, 2, PixelArt.DirtDark);
        c.Rect(0, 0, 2, 16, PixelArt.DirtDark);
        c.Rect(14, 0, 2, 16, PixelArt.DirtDark);
        c.Line(2, 2, 13, 13, PixelArt.DirtDark);
        return c;
    }

    public static Canvas Door()
    {
        var c = new Canvas(32, 16);
        c.Rect(0, 0, 16, 16, PixelArt.Wood);
        c.Rect(0, 0, 16, 1, PixelArt.DirtDark);
        c.Rect(7, 0, 1, 16, PixelArt.DirtDark);
        c.Rect(11, 8, 2, 2, PixelArt.Gold);
        c.Rect(16, 0, 16, 16, PixelArt.Ink);  // open: dark doorway
        return c;
    }

    public static Canvas KeyArt()
    {
        var c = new Canvas(8, 8);
        c.Ellipse(2, 3.5, 2, 2, PixelArt.Gold);
        c.Set(2, 3, 0);
        c.Rect(4, 3, 4, 1, PixelArt.Gold);
        c.Rect(6, 4, 1, 2, PixelArt.Gold);
        return c;
    }

    static void WriteArt(GameProject p)
    {
        TemplateKit.Tileset(p, "village_tiles", Tiles(), TilesMeta());
        TemplateKit.Sprite(p, "hero_topdown", Hero(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 3, Y = 8, W = 10, H = 8 },
            Animations =
            {
                new AnimationData { Name = "down", Frames = { 0, 1 }, Ticks = 10 },
                new AnimationData { Name = "up", Frames = { 2, 3 }, Ticks = 10 },
                new AnimationData { Name = "side", Frames = { 4, 5 }, Ticks = 10 },
            },
        });
        TemplateKit.Sprite(p, "elder", Elder(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 2, Y = 4, W = 12, H = 12 },
            Animations = { new AnimationData { Name = "idle", Frames = { 0, 1 }, Ticks = 30 } },
        });
        TemplateKit.Sprite(p, "crate", Crate(), new SpriteMeta { FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 0, Y = 0, W = 16, H = 16 } });
        TemplateKit.Sprite(p, "door", Door(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 0, Y = 0, W = 16, H = 16 },
            Animations = { new AnimationData { Name = "closed", Frames = { 0 }, Loop = false }, new AnimationData { Name = "open", Frames = { 1 }, Loop = false } },
        });
        TemplateKit.Sprite(p, "key", KeyArt(), new SpriteMeta { FrameWidth = 8, FrameHeight = 8, OriginX = 4, OriginY = 8, Collider = new ColliderData { X = 0, Y = 0, W = 8, H = 8 } });
    }

    // ---- maps -------------------------------------------------------------------------------------------

    /// <summary>The village: a fenced meadow with a pond, trees, a path to the elder's house (door at x 9, y 5).</summary>
    public static int[] VillageCells()
    {
        const int w = VillageW, h = VillageH;
        var c = new int[w * h];
        void S(int x, int y, int t) { if (x >= 0 && x < w && y >= 0 && y < h) c[y * w + x] = t; }
        for (var x = 0; x < w; x++) { S(x, 0, TTree); S(x, h - 1, TTree); }
        for (var y = 0; y < h; y++) { S(0, y, TTree); S(w - 1, y, TTree); }
        // House: roof rows 2-3, wall row 4-5 at x 7-11; the door actor stands at (9, 5).
        for (var x = 7; x <= 11; x++) { S(x, 2, TRoof); S(x, 3, TRoof); S(x, 4, TWall); S(x, 5, TWall); }
        S(9, 5, TPath);
        // Path from the door down to the bottom and across.
        for (var y = 6; y < h - 1; y++) S(9, y, TPath);
        for (var x = 1; x < w - 1; x++) S(x, 10, TPath);
        // Pond and flowers.
        for (var y = 7; y <= 8; y++) for (var x = 14; x <= 16; x++) S(x, y, TWater);
        foreach (var (x, y) in new[] { (3, 3), (4, 7), (12, 7), (16, 12), (2, 12) }) S(x, y, TFlowers);
        // A fenced corner with the key, open only at (4, 5); the crate stands in front of the opening at (4, 6).
        for (var x = 2; x <= 6; x++) S(x, 5, TFence);
        S(2, 6, TFence);
        S(6, 6, TFence);
        for (var y = 2; y <= 4; y++) { S(1, y, TTree); S(6, y, TFence); }
        for (var x = 1; x <= 6; x++) S(x, 1, TTree);
        S(4, 5, TPath);
        return c;
    }

    static void WriteMaps(GameProject p)
    {
        var village = new MapFile { Name = "Village", Tileset = "village_tiles", Width = VillageW, Height = VillageH, CollisionLayer = 0 };
        village.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = VillageCells().ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Village.map"), village);

        var house = new int[15 * 10];
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 15; x++)
                house[y * 15 + x] = x == 0 || x == 14 || y == 0 || y == 9 ? TWall : TFloor;
        house[9 * 15 + 7] = TFloor;  // the way out
        var houseMap = new MapFile { Name = "House", Tileset = "village_tiles", Width = 15, Height = 10, CollisionLayer = 0 };
        houseMap.Layers.Add(new MapLayerData { Name = "Floor", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = house.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "House.map"), houseMap);

        var title = new int[15 * 10];
        for (var i = 0; i < title.Length; i++) title[i] = (i / 15) is 0 or 9 || (i % 15) is 0 or 14 ? TTree : TGrass;
        var titleMap = new MapFile { Name = "Title", Tileset = "village_tiles", Width = 15, Height = 10, CollisionLayer = 0 };
        titleMap.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = title.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), titleMap);
    }

    static void WriteScenes(GameProject p)
    {
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene("Title", "#60B858", "Village", "title_theme"));
        var village = new SceneFile
        {
            Name = "Village", Width = VillageW, Height = VillageH, Map = "Village", Backdrop = "#60B858", MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "village_theme" },
            Camera = new CameraSettings { Follow = "Player", DeadZoneW = 16, DeadZoneH = 16, ClampToMap = true },
            Actors =
            {
                new ActorData
                {
                    Name = "Player", Kind = ActorKind.Player, Sprite = "hero_topdown", Animation = "down", X = 152, Y = 184, Tag = "Player",
                    Components = { new ComponentData { Type = "TopDownMover", Fields = { ["speed"] = "1.25" } } },
                },
                new ActorData { Name = "Elder", Kind = ActorKind.Empty, Sprite = "elder", Animation = "idle", X = 200, Y = 120, Tag = "NPC", Components = { new ComponentData { Type = "Npc" } } },
                new ActorData { Name = "Crate", Kind = ActorKind.Empty, Sprite = "crate", X = 72, Y = 112, Tag = "Crate", Components = { new ComponentData { Type = "PushBlock" } } },
                new ActorData { Name = "House Key", Kind = ActorKind.Pickup, Sprite = "key", X = 72, Y = 56, Tag = "Key", Components = { new ComponentData { Type = "KeyItem" } } },
                new ActorData { Name = "Door", Kind = ActorKind.Empty, Sprite = "door", Animation = "closed", X = 152, Y = 96, Tag = "Door", Components = { new ComponentData { Type = "Door" } } },
                new ActorData { Name = "House Entrance", Kind = ActorKind.TriggerZone, X = 146, Y = 88, ZoneW = 12, ZoneH = 6 },
            },
            Links = { new LinkData { From = "House Entrance", To = "House", SpawnX = 120, SpawnY = 136, Transition = Transition.Fade, Duration = 0.4 } },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Village.scene"), village);
        var house = new SceneFile
        {
            Name = "House", Width = 15, Height = 10, Map = "House", Backdrop = "#000000", MapX = 520, MapY = 60,
            Music = new MusicSettings { Track = "village_theme" },
            Events = new SceneEvents { OnEnter = "Text.Show(\"house.welcome\")" },
            Actors =
            {
                new ActorData
                {
                    Name = "Player", Kind = ActorKind.Player, Sprite = "hero_topdown", Animation = "up", X = 120, Y = 136, Tag = "Player",
                    Components = { new ComponentData { Type = "TopDownMover", Fields = { ["speed"] = "1.25" } } },
                },
                new ActorData { Name = "Way Out", Kind = ActorKind.TriggerZone, X = 120, Y = 160, ZoneW = 16, ZoneH = 6 },
            },
            Links = { new LinkData { From = "Way Out", To = "Village", SpawnX = 152, SpawnY = 120, Transition = Transition.Fade, Duration = 0.4 } },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "House.scene"), house);
    }

    // ---- scripts ----------------------------------------------------------------------------------------

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "Npc", """
using System.Threading.Tasks;

// Talks when the player stands next to it and presses A.
public class Npc : Behaviour
{
    void Start() => actor.blocking = true;

    void Update()
    {
        if (!Input.Pressed(Key.A)) return;
        Actor player = Scene.Find("Player");
        if (player == null) return;
        int dx = (player.pos.x - actor.pos.x).Floor();
        int dy = (player.pos.y - actor.pos.y).Floor();
        if (dx > -24 && dx < 24 && dy > -24 && dy < 24)
        {
            Sound.Play("talk");
            if (Game.doorOpen) Thanks();
            else Talk();
        }
    }

    [Cutscene]
    static async Task Talk()
    {
        await Say("npc.elder.1");
        await Say("npc.elder.2");
    }

    [Cutscene]
    static async Task Thanks()
    {
        await Say("npc.elder.3");
    }
}
""");
        TemplateKit.Script(p, "PushBlock", """
// A crate the player pushes one tile at a time by walking into it for a moment. It never moves into walls,
// water or other blocking actors.
public class PushBlock : Behaviour
{
    public int pushFrames = 12;
    int pushing;
    int moveLeft;
    int dirX;
    int dirY;

    void Start() => actor.blocking = true;

    void Update()
    {
        if (moveLeft > 0)
        {
            actor.pos.x += dirX * 2;
            actor.pos.y += dirY * 2;
            moveLeft -= 1;
            return;
        }
        Actor player = Scene.Find("Player");
        if (player == null) return;
        int dx = (player.pos.x - actor.pos.x).Floor();
        int dy = (player.pos.y - actor.pos.y).Floor();
        int wantX = 0;
        int wantY = 0;
        if (dy > -10 && dy < 10 && dx >= -18 && dx <= -12 && Input.Held(Key.Right)) wantX = 1;
        if (dy > -10 && dy < 10 && dx <= 18 && dx >= 12 && Input.Held(Key.Left)) wantX = -1;
        if (dx > -10 && dx < 10 && dy >= -14 && dy <= -8 && Input.Held(Key.Down)) wantY = 1;
        if (dx > -10 && dx < 10 && dy <= 18 && dy >= 12 && Input.Held(Key.Up)) wantY = -1;
        if (wantX == 0 && wantY == 0) { pushing = 0; return; }
        pushing += 1;
        if (pushing < pushFrames) return;
        pushing = 0;
        int tx = actor.pos.x.Floor() + wantX * 16;
        int ty = actor.pos.y.Floor() - 8 + wantY * 16;
        if (Scene.SolidAt(tx, ty)) return;
        dirX = wantX;
        dirY = wantY;
        moveLeft = 8;
        Sound.Play("push");
    }
}
""");
        TemplateKit.Script(p, "KeyItem", """
using System.Threading.Tasks;

// Picked up on touch: one more key for doors.
public class KeyItem : Behaviour
{
    void OnTouch(Actor other)
    {
        if (other.tag != "Player") return;
        Game.keys += 1;
        Sound.Play("key");
        actor.Destroy();
        Found();
    }

    [Cutscene]
    static async Task Found()
    {
        await Say("item.key");
    }
}
""");
        TemplateKit.Script(p, "Door", """
using System.Threading.Tasks;

// Blocks the way until the player brings a key; then it opens for good.
public class Door : Behaviour
{
    int bumps;

    void Start()
    {
        actor.blocking = !Game.doorOpen;
        if (Game.doorOpen) actor.PlayAnimation("open");
    }

    void Update()
    {
        if (Game.doorOpen) return;
        Actor player = Scene.Find("Player");
        if (player == null) return;
        int dx = (player.pos.x - actor.pos.x).Floor();
        int dy = (player.pos.y - actor.pos.y).Floor();
        if (dx < -10 || dx > 10 || dy < 8 || dy > 20 || !Input.Held(Key.Up)) { bumps = 0; return; }
        bumps += 1;
        if (bumps != 10) return;
        if (Game.keys > 0)
        {
            Game.keys -= 1;
            Game.doorOpen = true;
            actor.blocking = false;
            actor.PlayAnimation("open");
            Sound.Play("door");
        }
        else
        {
            Locked();
        }
    }

    [Cutscene]
    static async Task Locked()
    {
        await Say("door.locked");
    }
}
""");
    }
}
