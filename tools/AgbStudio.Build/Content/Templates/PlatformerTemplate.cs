using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Platformer template (Launcher · Templates, P15.01): run, jump with coyote time and a jump buffer, stomp patrolling
/// slimes, collect coins for the score HUD, touch a checkpoint, lose lives in pits or to enemies, reach the goal.
/// Mode 0 with three BG layers (sky, hills with parallax, ground with slopes and one-way platforms), nine actors in
/// the level and four C# scripts; the title screen and pause menu are UI pages.
/// </summary>
public static class PlatformerTemplate
{
    // Tile indices of the platformer tileset (16 px editor tiles, one row).
    public const int TEmpty = 0, TGrass = 1, TDirt = 2, TPlatform = 3, TSlopeUp = 4, TSlopeDown = 5, TBlock = 6, TStone = 7;
    public const int LevelWidth = 60, LevelHeight = 10;

    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "APLE", "Title", s =>
        {
            s.Save.Fields.AddRange([
                new SaveField { Name = "score", Type = "int" }, new SaveField { Name = "checkpointX", Type = "int" },
                new SaveField { Name = "checkpointY", Type = "int" }, new SaveField { Name = "lives", Type = "byte" },
            ]);
            s.Tags = ["Player", "Enemy", "Coin", "Checkpoint"];
        });
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("hud.score", "SCORE", "คะแนน", "UI & menus"),
            ("hud.lives", "LIVES", "ชีวิต", "UI & menus"),
        ]));
        WriteArt(p);
        WriteAudio(p);
        WriteMaps(p);
        WriteScenes(p);
        WriteScripts(p);
        WriteReadme(p);
        TemplateKit.OpenOnMainScene(p);
        return p;
    }

    static string RomTitle(string name)
    {
        var t = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return (t.Length == 0 ? "PLATFORMER" : t)[..Math.Min(12, t.Length == 0 ? 10 : t.Length)];
    }

    // ---- art --------------------------------------------------------------------------------------------

    static readonly uint SkyTop = Canvas.Rgb(96, 168, 248), SkyMid = Canvas.Rgb(120, 190, 250), SkyLow = Canvas.Rgb(160, 210, 252);
    static readonly uint Hill = Canvas.Rgb(88, 176, 96), HillDark = Canvas.Rgb(56, 136, 72), HillFar = Canvas.Rgb(136, 200, 144);
    static readonly uint Brick = Canvas.Rgb(214, 140, 72), BrickDark = Canvas.Rgb(150, 86, 40);

    public static Canvas Tiles()
    {
        var c = new Canvas(16 * 8, 16);
        void GrassBand(int x0, Func<int, int> surface)
        {
            for (var x = 0; x < 16; x++)
            {
                var s = surface(x);
                c.Rect(x0 + x, s, 1, 16 - s, PixelArt.Dirt);
                c.Rect(x0 + x, s, 1, Math.Min(4, 16 - s), PixelArt.Grass);
                if (s + 4 < 16) c.Set(x0 + x, s + 4, PixelArt.GrassDark);
            }
        }
        GrassBand(TGrass * 16, _ => 0);
        for (var k = 0; k < 4; k++) c.Rect(TGrass * 16 + 2 + k * 4, 9 + k % 2 * 3, 2, 1, PixelArt.DirtDark);
        c.Rect(TDirt * 16, 0, 16, 16, PixelArt.Dirt);
        for (var k = 0; k < 5; k++) c.Rect(TDirt * 16 + (k * 7) % 14 + 1, (k * 5) % 14 + 1, 2, 1, PixelArt.DirtDark);
        c.Rect(TPlatform * 16, 0, 16, 5, PixelArt.Wood);
        c.Rect(TPlatform * 16, 5, 16, 1, PixelArt.DirtDark);
        c.Rect(TPlatform * 16 + 3, 6, 2, 3, PixelArt.DirtDark);
        c.Rect(TPlatform * 16 + 11, 6, 2, 3, PixelArt.DirtDark);
        GrassBand(TSlopeUp * 16, x => 15 - x);   // rises to the right
        GrassBand(TSlopeDown * 16, x => x);      // rises to the left
        c.Rect(TBlock * 16, 0, 16, 16, Brick);
        c.Rect(TBlock * 16, 7, 16, 1, BrickDark);
        c.Rect(TBlock * 16, 15, 16, 1, BrickDark);
        c.Rect(TBlock * 16 + 7, 0, 1, 7, BrickDark);
        c.Rect(TBlock * 16 + 3, 8, 1, 7, BrickDark);
        c.Rect(TBlock * 16 + 11, 8, 1, 7, BrickDark);
        c.Rect(TStone * 16, 0, 16, 16, PixelArt.Stone);
        c.Rect(TStone * 16, 7, 16, 1, PixelArt.StoneDark);
        c.Rect(TStone * 16 + 7, 0, 1, 7, PixelArt.StoneDark);
        return c;
    }

    public static TilesetMeta TilesMeta() => new()
    {
        Tiles =
        {
            [TGrass] = new TileProperties { Name = "grass", Collision = CollisionType.Solid, Tag = "ground" },
            [TDirt] = new TileProperties { Name = "dirt", Collision = CollisionType.Solid },
            [TPlatform] = new TileProperties { Name = "platform", Collision = CollisionType.OneWay },
            [TSlopeUp] = new TileProperties { Name = "slope_up", Collision = CollisionType.SlopeUp, Tag = "ground" },
            [TSlopeDown] = new TileProperties { Name = "slope_down", Collision = CollisionType.SlopeDown, Tag = "ground" },
            [TBlock] = new TileProperties { Name = "block", Collision = CollisionType.Solid },
            [TStone] = new TileProperties { Name = "stone", Collision = CollisionType.Solid },
        },
    };

    /// <summary>Sky with three bands and a few clouds (256 x 160, wraps).</summary>
    public static Canvas Sky()
    {
        var c = new Canvas(256, 160);
        c.Rect(0, 0, 256, 56, SkyTop);
        c.Rect(0, 56, 256, 56, SkyMid);
        c.Rect(0, 112, 256, 48, SkyLow);
        foreach (var (x, y) in new[] { (24, 24), (120, 40), (196, 16) }) c.Blit(PixelArt.Cloud(), x, y);
        return c;
    }

    /// <summary>Two rows of rolling hills at the bottom (512 x 160, transparent above, wraps).</summary>
    public static Canvas Hills()
    {
        var c = new Canvas(512, 160);
        for (var x = 0; x < 512; x++)
        {
            var far = 104 + (int)(10 * Math.Sin(x * Math.PI / 64) + 6 * Math.Sin(x * Math.PI / 23));
            c.Rect(x, far, 1, 160 - far, HillFar);
            var near = 124 + (int)(12 * Math.Sin(x * Math.PI / 96 + 1) + 4 * Math.Sin(x * Math.PI / 17));
            c.Rect(x, near, 1, 160 - near, Hill);
            c.Set(x, near, HillDark);
        }
        return c;
    }

    /// <summary>Checkpoint post: frame 0 flag down, frame 1 raised (16 x 32).</summary>
    public static Canvas Checkpoint()
    {
        var c = new Canvas(32, 32);
        for (var f = 0; f < 2; f++)
        {
            var x0 = f * 16;
            c.Rect(x0 + 3, 4, 2, 28, PixelArt.Stone);
            c.Rect(x0 + 2, 30, 4, 2, PixelArt.StoneDark);
            var y = f == 0 ? 20 : 4;
            c.Rect(x0 + 5, y, 9, 6, f == 0 ? PixelArt.StoneDark : PixelArt.Gold);
            c.Rect(x0 + 5, y + 6, 6, 1, f == 0 ? PixelArt.StoneDark : PixelArt.GoldDark);
        }
        return c;
    }

    static void WriteArt(GameProject p)
    {
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        TemplateKit.Tileset(p, "platformer_tiles", Tiles(), TilesMeta());
        TemplateKit.Image(p, "sky", Sky());
        TemplateKit.Image(p, "hills", Hills());
        TemplateKit.Sprite(p, "hero", PixelArt.Hero8(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 3, Y = 2, W = 10, H = 14 },
            Animations =
            {
                new AnimationData { Name = "idle", Frames = { 0, 1 }, Ticks = 20 },
                new AnimationData { Name = "run", Frames = { 2, 3, 4, 5 }, Ticks = 6 },
                new AnimationData { Name = "jump", Frames = { 6 }, Loop = false },
                new AnimationData { Name = "hurt", Frames = { 7 }, Ticks = 6 },
            },
        });
        TemplateKit.Sprite(p, "slime", PixelArt.Slime4(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 12, OriginX = 8, OriginY = 12, Collider = new ColliderData { X = 2, Y = 2, W = 12, H = 10 },
            Animations = { new AnimationData { Name = "idle", Frames = { 0, 1, 2, 3 }, Ticks = 8 } },
        });
        TemplateKit.Sprite(p, "coin", PixelArt.Coin6(), new SpriteMeta
        {
            FrameWidth = 8, FrameHeight = 8, OriginX = 4, OriginY = 8, Collider = new ColliderData { X = 0, Y = 0, W = 8, H = 8 },
            Animations = { new AnimationData { Name = "spin", Frames = { 0, 1, 2, 3, 4, 5 }, Ticks = 5 } },
        });
        TemplateKit.Sprite(p, "checkpoint", Checkpoint(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 32, OriginX = 4, OriginY = 32, Collider = new ColliderData { X = 0, Y = 0, W = 16, H = 32 },
            Animations = { new AnimationData { Name = "down", Frames = { 0 }, Loop = false }, new AnimationData { Name = "raised", Frames = { 1 }, Loop = false } },
        });
        TemplateKit.Sprite(p, "flag", PixelArt.Flag(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 32, OriginX = 4, OriginY = 32, Collider = new ColliderData { X = 0, Y = 0, W = 16, H = 32 },
        });
    }

    static void WriteAudio(GameProject p)
    {
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 112, 2, 3, "G"));
        TemplateKit.Music(p, "level_theme", XmWriter.Tune("Level theme", 136, 4, 7, "C"));
        TemplateKit.Sfx(p, "jump", 420, 840, 0.12);
        TemplateKit.Sfx(p, "coin", 980, 1480, 0.10);
        TemplateKit.Sfx(p, "stomp", 320, 110, 0.14);
        TemplateKit.Sfx(p, "hurt", 520, 90, 0.28);
        TemplateKit.Sfx(p, "checkpoint", 660, 1320, 0.22);
    }

    // ---- maps -------------------------------------------------------------------------------------------

    static List<MapLayerData> Backgrounds() =>
    [
        new() { Name = "Sky", Type = LayerType.Image, Bg = 3, Priority = 3, ParallaxX = 0.125, ParallaxY = 0, Wrap = true, Image = "sky" },
        new() { Name = "Hills", Type = LayerType.Image, Bg = 2, Priority = 2, ParallaxX = 0.5, ParallaxY = 0, Wrap = true, Image = "hills" },
    ];

    /// <summary>The level: flat start, a hill with slopes, one-way platforms, a pit, the checkpoint, a second hill
    /// built from blocks, more platforms and the goal.</summary>
    public static int[] LevelCells()
    {
        const int w = LevelWidth, h = LevelHeight;
        var cells = new int[w * h];
        void Set(int x, int y, int t) { if (x >= 0 && x < w && y >= 0 && y < h) cells[y * w + x] = t; }
        void Ground(int x, int top) { Set(x, top, TGrass); for (var y = top + 1; y < h; y++) Set(x, y, TDirt); }
        for (var x = 0; x < w; x++)
            if (x is < 28 or > 29) Ground(x, 8);   // pit at 28-29 (32 px, one running jump)
        // Hill: slope up at 12, plateau 13-17 one tile higher, slope down at 18.
        Set(12, 7, TSlopeUp);
        for (var x = 13; x <= 17; x++) Ground(x, 7);
        Set(18, 7, TSlopeDown);
        // One-way platforms over the pit approach.
        for (var x = 22; x <= 25; x++) Set(x, 5, TPlatform);
        // Second hill, two tiles high: slopes up at 38 and 39, plateau 40-43, slopes down at 44 and 45.
        Set(38, 7, TSlopeUp);
        Set(39, 6, TSlopeUp);
        for (var x = 40; x <= 43; x++) Ground(x, 6);
        Set(44, 6, TSlopeDown);
        Set(45, 7, TSlopeDown);
        // Blocks and a high platform near the end.
        Set(48, 4, TBlock);
        Set(49, 4, TBlock);
        for (var x = 51; x <= 53; x++) Set(x, 5, TPlatform);
        // Under every slope the hill is solid dirt.
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (cells[y * w + x] is TSlopeUp or TSlopeDown)
                    for (var yy = y + 1; yy < h; yy++) cells[yy * w + x] = TDirt;
        return cells;
    }

    static void WriteMaps(GameProject p)
    {
        var level = new MapFile { Name = "Level_01", Tileset = "platformer_tiles", Width = LevelWidth, Height = LevelHeight, CollisionLayer = 2 };
        level.Layers.AddRange(Backgrounds());
        level.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = LevelCells().ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Level_01.map"), level);

        var titleCells = new int[15 * 10];
        for (var x = 0; x < 15; x++)
        {
            titleCells[8 * 15 + x] = TGrass;
            titleCells[9 * 15 + x] = TDirt;
        }
        var title = new MapFile { Name = "Title", Tileset = "platformer_tiles", Width = 15, Height = 10, CollisionLayer = 2 };
        title.Layers.AddRange(Backgrounds());
        title.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = titleCells.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), title);
    }

    // ---- scenes -----------------------------------------------------------------------------------------

    static void WriteScenes(GameProject p)
    {
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene(p, "Title", "#78BEFA", "Level_01", "title_theme"));
        static ComponentData Body() => new() { Type = "PlatformerBody" };
        var level = new SceneFile
        {
            Hud = TemplateKit.HudPage(p, "Hud", false, ("Score", "hud.score", 8, 4, 88), ("Lives", "hud.lives", 168, 4, 64)),
            Name = "Level_01", Width = LevelWidth, Height = LevelHeight, Map = "Level_01", Backdrop = "#60A8F8", MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "level_theme" },
            Camera = new CameraSettings { Follow = "Player", DeadZoneW = 32, DeadZoneH = 24, ClampToMap = true },
            Actors =
            {
                new ActorData
                {
                    Name = "Player", Kind = ActorKind.Player, Sprite = "hero", Animation = "idle", X = 40, Y = 128, Tag = "Player",
                    Components = { Body(), new ComponentData { Type = "PlayerController" } },
                },
                new ActorData
                {
                    Name = "Slime", Kind = ActorKind.Enemy, Sprite = "slime", Animation = "idle", X = 136, Y = 128, Tag = "Enemy", Priority = 2, Group = "Enemies",
                    Components = { Body(), new ComponentData { Type = "Patrol", Fields = { ["speed"] = "0.5", ["turnAtLedges"] = "true" } }, new ComponentData { Type = "Slime" } },
                },
                new ActorData
                {
                    Name = "Slime (1)", Kind = ActorKind.Enemy, Sprite = "slime", Animation = "idle", X = 424, Y = 128, Tag = "Enemy", Priority = 2, Group = "Enemies",
                    Components = { Body(), new ComponentData { Type = "Patrol", Fields = { ["speed"] = "0.5", ["turnAtLedges"] = "true" } }, new ComponentData { Type = "Slime" } },
                },
                new ActorData { Name = "Coin", Kind = ActorKind.Pickup, Sprite = "coin", Animation = "spin", X = 248, Y = 106, Tag = "Coin", Priority = 2, Group = "Coins",
                    Components = { new ComponentData { Type = "Coin" } } },
                new ActorData { Name = "Coin (1)", Kind = ActorKind.Pickup, Sprite = "coin", Animation = "spin", X = 840, Y = 72, Tag = "Coin", Priority = 2, Group = "Coins",
                    Components = { new ComponentData { Type = "Coin" } } },
                new ActorData { Name = "Checkpoint", Kind = ActorKind.Empty, Sprite = "checkpoint", Animation = "down", X = 520, Y = 128, Tag = "Checkpoint", Priority = 2,
                    Components = { new ComponentData { Type = "Checkpoint" } } },
                new ActorData { Name = "Goal Flag", Kind = ActorKind.TriggerZone, Sprite = "flag", X = 920, Y = 128, ZoneW = 16, ZoneH = 32, Priority = 2 },
            },
            Links = { new LinkData { From = "Goal Flag", To = "Title", Transition = Transition.Fade, Duration = 0.5 } },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Level_01.scene"), level);
    }

    // ---- README -----------------------------------------------------------------------------------------

    static void WriteReadme(GameProject p) => TemplateKit.Readme(p, "Platformer",
        "Run right through Level_01, jump over the pit, stomp the slimes, pick up the coins, raise the checkpoint and touch the goal flag.",
        ["D-pad left / right: run", "A: jump (hold for a higher jump)", "Start: pause menu"],
        new Dictionary<string, string>
        {
            ["Player"] = "The hero. Platformer Body does gravity, slopes and one-way platforms; PlayerController reads the D-pad, jumps, stomps slimes and handles lives.",
            ["Slime"] = "Enemy. Patrol walks it and turns at ledges, Platformer Body keeps it on the ground, Slime holds its points. Land on it to stomp it; touch it from the side to lose a life.",
            ["Slime (1)"] = "A second slime on the far hill: copy an enemy with Ctrl+D to make more.",
            ["Coin"] = "Pickup: the Coin script adds its value to the score and removes itself.",
            ["Coin (1)"] = "A second coin on the high platform near the end.",
            ["Checkpoint"] = "Raises its flag on touch; after losing a life the player comes back here, and Continue starts here.",
            ["Goal Flag"] = "Trigger zone with a scene link: touching it fades back to the title screen. Change the link in Link Settings to add a Level_02.",
        },
        [
            "Select Player and drag the **speed** slider of PlayerController, or **Jump force** / **Gravity** of Platformer Body, then press Play.",
            "Change `points` of a Slime or `value` of a Coin in the Inspector.",
            "Add a slime: select Slime, press Ctrl+D and move the copy. Set Patrol **Turn at ledges** off to let it walk off edges.",
            "Paint more ground or one-way platforms in the Tilemap Editor (double click BG1 Ground in the Hierarchy).",
            "Set the camera dead zone in the Inspector (select Main Camera) to change how early the screen scrolls.",
        ]);

    // ---- scripts ----------------------------------------------------------------------------------------

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "PlayerController", """
// Runs and jumps with the Platformer Body on the same actor, stomps slimes, loses a life in pits.
// Coyote time: a jump still works a few frames after walking off a ledge. Jump buffer: a jump pressed a few
// frames before landing happens on landing. Releasing A early makes a short hop.
public class PlayerController : Behaviour
{
    // Public fields show in the Inspector under "PlayerController"; [Range] makes a slider.
    [Range(0.5, 3)] public fixed speed = 1.5;
    public fixed jumpPower = 3.75;
    public int coyoteFrames = 6;
    public int jumpBufferFrames = 6;

    PlatformerBody body;
    int bufferedJump;
    int invincible;
    bool jumping;

    // Start runs once, when the scene has loaded the actor.
    void Start()
    {
        // Get<T>() finds another component on this actor: the built-in Platformer Body does gravity, slopes,
        // one-way platforms and collision with the Ground layer; this script only sets its velocity.
        body = Get<PlatformerBody>();
        // Game.<field> is the save data of Project Settings > Save (score, lives, checkpointX, checkpointY).
        if (Game.lives == 0)
        {
            Game.lives = 3;
            Game.score = 0;
        }
        Game.checkpointX = actor.pos.x.Floor();   // actor.pos is the position in pixels (fixed point)
        Game.checkpointY = actor.pos.y.Floor();
        UpdateHud();
    }

    // Update runs every frame (60 times a second).
    void Update()
    {
        // Input.AxisX is -1, 0 or 1 from the D-pad; multiplying by speed gives pixels per frame.
        body.vx = Input.AxisX * speed;
        // Input.Pressed is true only on the frame the button goes down; Input.Held while it stays down.
        if (Input.Pressed(Key.A)) bufferedJump = jumpBufferFrames;
        else if (bufferedJump > 0) bufferedJump -= 1;

        if (body.OnGround) jumping = false;
        bool canJump = body.OnGround || body.FramesSinceGround <= coyoteFrames;
        if (bufferedJump > 0 && canJump && !jumping)
        {
            body.vy = -jumpPower;   // negative y is up
            bufferedJump = 0;
            jumping = true;
            Sound.Play("jump");     // a sound effect from assets/audio/sfx
        }
        if (jumping && body.vy < -1 && !Input.Held(Key.A)) body.vy = -1;

        if (invincible > 0)
        {
            invincible -= 1;
            actor.visible = invincible == 0 || (invincible & 4) == 0;   // blink while invincible
        }
        if (actor.pos.y > 180) LoseLife();   // fell into a pit (the level is 160 px high)
    }

    // OnTouch runs when this actor's collider overlaps another actor's collider.
    void OnTouch(Actor other)
    {
        // other.Is<T>() asks whether the other actor has that component.
        if (!other.Is<Slime>()) return;
        if (body.vy > 0 && actor.pos.y < other.pos.y - 4)
        {
            Slime slime = other.Get<Slime>();
            Game.score += slime.points;
            slime.Squash();
            body.vy = -jumpPower / 2;   // bounce off
            UpdateHud();
        }
        else if (invincible == 0)
        {
            LoseLife();
        }
    }

    void LoseLife()
    {
        Sound.Play("hurt");
        Game.lives -= 1;
        if (Game.lives == 0)
        {
            Scene.Load("Title");   // game over: back to the title screen
            return;
        }
        actor.pos.x = Game.checkpointX;
        actor.pos.y = Game.checkpointY;
        body.vx = 0;
        body.vy = 0;
        invincible = 90;
        UpdateHud();
    }

    public void UpdateHud()
    {
        // UI.SetNumber writes a number into an element of the HUD page (ui/Hud.uipage).
        UI.SetNumber("Score", Game.score, 4);
        UI.SetNumber("Lives", Game.lives, 1);
    }
}
""");
        TemplateKit.Script(p, "Slime", """
// An enemy the player stomps by landing on top. The built-in Patrol component walks it back and forth (it turns
// at walls and ledges) and the Platformer Body gives it gravity; this script only holds its points.
public class Slime : Behaviour
{
    public int points = 50;

    // Called by PlayerController when the player lands on it.
    public void Squash()
    {
        Sound.Play("stomp");
        actor.Destroy();   // removes the actor from the scene (its OBJ slot is freed)
    }
}
""");
        TemplateKit.Script(p, "Coin", """
// Adds to the score when the player touches it, then disappears.
public class Coin : Behaviour
{
    public int value = 10;

    void OnTouch(Actor other)
    {
        // other.tag is the Tag field of the Inspector header (Project Settings > Tags lists them).
        if (other.tag != "Player") return;
        Game.score += value;
        Sound.Play("coin");
        actor.Destroy();
        UI.SetNumber("Score", Game.score, 4);
    }
}
""");
        TemplateKit.Script(p, "Checkpoint", """
// The player comes back here after losing a life, and Continue on the title screen starts here.
public class Checkpoint : Behaviour
{
    public bool reached = false;

    void OnTouch(Actor other)
    {
        if (reached || other.tag != "Player") return;
        reached = true;
        Game.checkpointX = actor.pos.x.Floor();
        Game.checkpointY = actor.pos.y.Floor();
        actor.PlayAnimation("raised");   // an animation of the sprite (Sprite Editor > Animations)
        Sound.Play("checkpoint");
        Save.Checkpoint();               // writes the save data, so Continue resumes here
    }
}
""");
    }
}
