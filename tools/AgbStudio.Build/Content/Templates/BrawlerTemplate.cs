using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Beat 'em up template (Launcher · Templates, P15.07): a fighter walks along a pier at dusk, moving left, right and
/// up and down within the floor. A punches; three presses in a row make a combo (jab, jab, kick) and the kick knocks
/// enemies back. The camera stops at three points until the wave of thugs there is beaten. Health, lives and score.
/// </summary>
public static class BrawlerTemplate
{
    public const int StageWidth = 64;  // 16 px tiles: 1024 px of pier
    public const int TEmpty = 0, TPlankA = 1, TPlankB = 2, TRail = 3, TPost = 4, TEdge = 5;

    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "ABRE", "Title", s =>
        {
            s.Save.Fields.AddRange([new SaveField { Name = "score", Type = "int" }, new SaveField { Name = "lives", Type = "byte" }]);
            s.Tags = ["Player", "Enemy"];
            s.Input[0].Action = "Attack";
        });
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("hud.hp", "HP", "พลัง", "UI & menus"),
            ("hud.score", "SCORE", "คะแนน", "UI & menus"),
            ("hud.lives", "1UP", "ชีวิต", "UI & menus"),
            ("brawl.go", "GO!", "ไปต่อ!", "UI & menus"),
            ("brawl.clear", "Stage clear!", "ผ่านด่านแล้ว!", "UI & menus"),
            ("brawl.empty", " ", " ", "UI & menus"),
        ]));
        WriteArt(p);
        TemplateKit.Music(p, "pier_theme", XmWriter.Tune("Pier theme", 140, 4, 67, "A"));
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 110, 2, 71, "D"));
        TemplateKit.Sfx(p, "punch", 420, 260, 0.06);
        TemplateKit.Sfx(p, "kick", 300, 140, 0.1);
        TemplateKit.Sfx(p, "hit", 900, 300, 0.08);
        TemplateKit.Sfx(p, "hurt", 520, 80, 0.3);
        WriteScenes(p);
        WriteScripts(p);
        return p;
    }

    static string RomTitle(string name)
    {
        var t = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return t.Length == 0 ? "BRAWLER" : t[..Math.Min(12, t.Length)];
    }

    static readonly uint Wood = Canvas.Rgb(150, 100, 64), WoodDark = Canvas.Rgb(112, 72, 48), WoodLight = Canvas.Rgb(184, 132, 88);
    static readonly uint Rail = Canvas.Rgb(80, 56, 48), Skin = Canvas.Rgb(240, 192, 152), SkinDark = Canvas.Rgb(200, 144, 112);

    /// <summary>Pier tiles: empty, two plank shades, railing bar, railing post, the pier's front edge.</summary>
    public static Canvas Tiles()
    {
        var c = new Canvas(16 * 6, 16);
        foreach (var (t, light) in new[] { (TPlankA, true), (TPlankB, false) })
        {
            var x = t * 16;
            c.Rect(x, 0, 16, 16, light ? Wood : WoodDark);
            c.Rect(x, 7, 16, 1, light ? WoodDark : Canvas.Rgb(88, 56, 40));
            c.Rect(x, 15, 16, 1, light ? WoodDark : Canvas.Rgb(88, 56, 40));
            c.Rect(x + (light ? 3 : 11), 3, 1, 1, WoodLight);
        }
        c.Rect(TRail * 16, 4, 16, 2, Rail);
        c.Rect(TRail * 16, 11, 16, 2, Rail);
        c.Rect(TRail * 16, 15, 16, 1, WoodDark);
        c.Rect(TPost * 16, 4, 16, 2, Rail);
        c.Rect(TPost * 16, 11, 16, 2, Rail);
        c.Rect(TPost * 16 + 6, 0, 4, 16, Rail);
        c.Rect(TPost * 16 + 6, 0, 4, 1, WoodLight);
        c.Rect(TEdge * 16, 0, 16, 4, WoodDark);
        c.Rect(TEdge * 16, 4, 16, 12, Canvas.Rgb(56, 40, 72));
        c.Rect(TEdge * 16 + 6, 4, 4, 12, Rail);
        return c;
    }

    static TilesetMeta TilesMeta() => new()
    {
        Tiles =
        {
            [TEmpty] = new TileProperties { Name = "empty" },
            [TPlankA] = new TileProperties { Name = "plank" }, [TPlankB] = new TileProperties { Name = "plank dark" },
            [TRail] = new TileProperties { Name = "railing" }, [TPost] = new TileProperties { Name = "post" },
            [TEdge] = new TileProperties { Name = "edge" },
        },
    };

    /// <summary>Dusk sky with the sun and a city skyline (256 x 160, wraps).</summary>
    public static Canvas Dusk()
    {
        var c = new Canvas(256, 160);
        uint[] bands = [Canvas.Rgb(64, 40, 104), Canvas.Rgb(120, 56, 120), Canvas.Rgb(200, 88, 104), Canvas.Rgb(240, 144, 96)];
        for (var b = 0; b < 4; b++) c.Rect(0, b * 20, 256, 20, bands[b]);
        c.Rect(0, 80, 256, 80, bands[3]);
        c.Ellipse(176, 70, 14, 14, Canvas.Rgb(252, 216, 120));
        var rng = new Random(11);
        for (var x = 0; x < 256;)
        {
            var w = 10 + rng.Next(16);
            var h = 14 + rng.Next(30);
            c.Rect(x, 92 - h, Math.Min(w, 256 - x), h + 68, Canvas.Rgb(48, 32, 72));
            for (var wy = 92 - h + 4; wy < 88; wy += 6)
                for (var wx = x + 2; wx < x + w - 2 && wx < 254; wx += 4)
                    if (rng.Next(3) == 0) c.Rect(wx, wy, 1, 2, Canvas.Rgb(248, 208, 120));
            x += w + 1 + rng.Next(4);
        }
        return c;
    }

    /// <summary>The harbour: water with wave lines and moored boats (512 x 160, transparent above, wraps).</summary>
    public static Canvas Harbour()
    {
        var c = new Canvas(512, 160);
        c.Rect(0, 88, 512, 72, Canvas.Rgb(40, 56, 104));
        var rng = new Random(5);
        for (var i = 0; i < 60; i++) c.Rect(rng.Next(508), 92 + rng.Next(60), 4 + rng.Next(6), 1, Canvas.Rgb(232, 136, 112));
        foreach (var bx in new[] { 40, 200, 360 })
        {
            c.Rect(bx, 82, 40, 8, Canvas.Rgb(32, 24, 48));
            c.Rect(bx + 4, 90, 32, 2, Canvas.Rgb(32, 24, 48));
            c.Rect(bx + 18, 52, 2, 30, Canvas.Rgb(32, 24, 48));
            c.Rect(bx + 8, 60, 10, 18, Canvas.Rgb(200, 176, 168));
        }
        return c;
    }

    /// <summary>A fighter frame (32 x 32 cell, feet at the bottom, facing right).</summary>
    static void Figure(Canvas c, int x0, uint top, uint topDark, uint hair, int step, int arm, int leg, int lean)
    {
        var x = x0 + 12 + lean;
        // legs
        c.Rect(x + 1 + step, 22, 3, 10 - (leg > 0 ? 3 : 0), Canvas.Rgb(48, 48, 64));
        if (leg > 0) c.Rect(x + 4, 20, 12, 3, Canvas.Rgb(48, 48, 64));  // kick
        else c.Rect(x + 5 - step, 22, 3, 10, Canvas.Rgb(48, 48, 64));
        c.Rect(x + step, 30, 4, 2, PixelArt.Ink);
        if (leg == 0) c.Rect(x + 5 - step, 30, 4, 2, PixelArt.Ink);
        else c.Rect(x + 15, 19, 3, 4, PixelArt.Ink);
        // body
        c.Rect(x, 12, 9, 11, top);
        c.Rect(x, 20, 9, 2, topDark);
        // head
        c.Rect(x + 1, 3, 7, 8, Skin);
        c.Rect(x + 1, 2, 7, 3, hair);
        c.Rect(x + 6, 6, 1, 2, PixelArt.Ink);
        // arm: 0 down, 1 punch
        if (arm > 0)
        {
            c.Rect(x + 8, 13, 10, 3, top);
            c.Rect(x + 18, 12, 4, 5, Skin);
        }
        else
        {
            c.Rect(x + 6, 13, 3, 8, topDark);
            c.Rect(x + 6, 20, 3, 3, SkinDark);
        }
    }

    /// <summary>The player: idle (2), walk (2), punch, kick, hurt.</summary>
    public static Canvas Fighter()
    {
        var c = new Canvas(32 * 7, 32);
        uint top = Canvas.Rgb(40, 168, 168), dark = Canvas.Rgb(24, 112, 120), hair = Canvas.Rgb(56, 40, 32);
        Figure(c, 0, top, dark, hair, 0, 0, 0, 0);
        Figure(c, 32, top, dark, hair, 0, 0, 0, 1);
        Figure(c, 64, top, dark, hair, 2, 0, 0, 0);
        Figure(c, 96, top, dark, hair, -2, 0, 0, 0);
        Figure(c, 128, top, dark, hair, 0, 1, 0, 1);
        Figure(c, 160, top, dark, hair, 0, 0, 1, -2);
        Figure(c, 192, top, dark, hair, 1, 0, 0, -3);
        return c;
    }

    /// <summary>A thug: walk (2), wind-up, strike, hurt.</summary>
    public static Canvas Thug()
    {
        var c = new Canvas(32 * 5, 32);
        uint top = Canvas.Rgb(144, 72, 168), dark = Canvas.Rgb(96, 48, 120), hair = Canvas.Rgb(232, 200, 72);
        Figure(c, 0, top, dark, hair, 2, 0, 0, 0);
        Figure(c, 32, top, dark, hair, -2, 0, 0, 0);
        Figure(c, 64, top, dark, hair, 0, 0, 0, -3);
        Figure(c, 96, top, dark, hair, 1, 1, 0, 2);
        Figure(c, 128, top, dark, hair, 1, 0, 0, -3);
        return c;
    }

    static void WriteArt(GameProject p)
    {
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        TemplateKit.Tileset(p, "pier", Tiles(), TilesMeta());
        TemplateKit.Image(p, "dusk", Dusk());
        TemplateKit.Image(p, "harbour", Harbour());
        TemplateKit.Sprite(p, "fighter", Fighter(), new SpriteMeta
        {
            FrameWidth = 32, FrameHeight = 32, OriginX = 16, OriginY = 31, Collider = new ColliderData { X = 11, Y = 4, W = 10, H = 27 },
            Animations =
            {
                new AnimationData { Name = "idle", Frames = { 0, 1 }, Ticks = 24 },
                new AnimationData { Name = "walk", Frames = { 2, 3 }, Ticks = 8 },
                new AnimationData { Name = "punch", Frames = { 4 }, Loop = false },
                new AnimationData { Name = "kick", Frames = { 5 }, Loop = false },
                new AnimationData { Name = "hurt", Frames = { 6 }, Loop = false },
            },
        });
        TemplateKit.Sprite(p, "thug", Thug(), new SpriteMeta
        {
            FrameWidth = 32, FrameHeight = 32, OriginX = 16, OriginY = 31, Collider = new ColliderData { X = 11, Y = 4, W = 10, H = 27 },
            Animations =
            {
                new AnimationData { Name = "walk", Frames = { 0, 1 }, Ticks = 10 },
                new AnimationData { Name = "windup", Frames = { 2 }, Loop = false },
                new AnimationData { Name = "strike", Frames = { 3 }, Loop = false },
                new AnimationData { Name = "hurt", Frames = { 4 }, Loop = false },
            },
        });
    }

    /// <summary>The floor layer: open above, the railing on row 5, planks below, the pier's edge at the bottom.</summary>
    public static int[] Floor(int width)
    {
        var cells = new int[width * 10];
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < width; x++)
                cells[y * width + x] = y < 5 ? TEmpty : y == 5 ? (x % 4 == 0 ? TPost : TRail) : y == 9 ? TEdge : y % 2 == 0 ? TPlankA : TPlankB;
        return cells;
    }

    static void WriteScenes(GameProject p)
    {
        static MapFile Pier(string name, int width)
        {
            var m = new MapFile { Name = name, Tileset = "pier", Width = width, Height = 10, CollisionLayer = -1 };
            m.Layers.Add(new MapLayerData { Name = "Sky", Type = LayerType.Image, Bg = 3, Priority = 3, ParallaxX = 0.2, ParallaxY = 0, Wrap = true, Image = "dusk" });
            m.Layers.Add(new MapLayerData { Name = "Harbour", Type = LayerType.Image, Bg = 2, Priority = 2, ParallaxX = 0.5, ParallaxY = 0, Wrap = true, Image = "harbour" });
            m.Layers.Add(new MapLayerData { Name = "Floor", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = Floor(width).ToList() });
            return m;
        }
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), Pier("Title", 15));
        GameProject.Save(Path.Combine(p.MapsDir, "Pier.map"), Pier("Pier", StageWidth));
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene("Title", "#402868", "Pier", "title_theme"));
        var pier = new SceneFile
        {
            Name = "Pier", Width = StageWidth, Height = 10, Map = "Pier", Backdrop = "#402868", MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "pier_theme" },
            Camera = new CameraSettings { Follow = "", ClampToMap = true },
            Actors =
            {
                new ActorData { Name = "Fighter", Kind = ActorKind.Player, Sprite = "fighter", Animation = "idle", X = 48, Y = 128, Tag = "Player", Priority = 1,
                    Components = { new ComponentData { Type = "Fighter" } } },
                new ActorData { Name = "Stage", Kind = ActorKind.Empty, Components = { new ComponentData { Type = "Waves" } } },
                new ActorData { Name = "Thug", Kind = ActorKind.Enemy, Sprite = "thug", Animation = "walk", X = 0, Y = -64, Tag = "Enemy", Hidden = true, Group = "Templates", Priority = 1,
                    Components = { new ComponentData { Type = "Thug" } } },
                new ActorData { Name = "HUD HP", Kind = ActorKind.TextLabel, X = 8, Y = 4, Text = "hud.hp", Group = "HUD" },
                new ActorData { Name = "HUD Score", Kind = ActorKind.TextLabel, X = 80, Y = 4, Text = "hud.score", Group = "HUD" },
                new ActorData { Name = "HUD Lives", Kind = ActorKind.TextLabel, X = 184, Y = 4, Text = "hud.lives", Group = "HUD" },
                new ActorData { Name = "Go", Kind = ActorKind.TextLabel, X = 176, Y = 40, Text = "brawl.empty", Group = "HUD" },
            },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Pier.scene"), pier);
    }

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "Fighter", """
// Walks along the pier: Left and Right, and Up and Down within the floor. A punches; three presses in a row make a
// combo (jab, jab, kick) and the kick knocks enemies back. Enemies read `hitActive`, `attack` and `attackId` to take
// each hit once. Thugs call Hurt when their strike lands.
public class Fighter : Behaviour
{
    public fixed speed = 1.5;
    public int maxHp = 10;
    public int hp;
    public int attack;            // 1 and 2 jabs, 3 the kick
    public int attackId;          // a new id for every attack
    public bool hitActive;        // true on the frames an attack can hit
    public int minX;              // the screen edges while a wave holds the camera
    public int maxX = 1024;
    int attackTimer;
    int comboWindow;
    int hurtTimer;
    int invincible;

    void Start()
    {
        if (Game.lives == 0)
        {
            Game.lives = 3;
            Game.score = 0;
        }
        hp = maxHp;
        UpdateHud();
    }

    void Update()
    {
        hitActive = false;
        if (invincible > 0)
        {
            invincible -= 1;
            actor.visible = invincible == 0 || (invincible & 4) == 0;
        }
        if (hurtTimer > 0)
        {
            hurtTimer -= 1;
            return;
        }
        if (comboWindow > 0) comboWindow -= 1;
        if (attackTimer > 0)
        {
            attackTimer -= 1;
            hitActive = attackTimer >= 6 && attackTimer <= 10;
            if (attackTimer == 0) comboWindow = 18;
            return;
        }
        if (Input.Pressed(Key.A))
        {
            attack = comboWindow > 0 && attack < 3 ? attack + 1 : 1;
            attackId += 1;
            attackTimer = attack == 3 ? 18 : 12;
            comboWindow = 0;
            if (attack == 3)
            {
                actor.PlayAnimation("kick");
                Sound.Play("kick");
            }
            else
            {
                actor.PlayAnimation("punch");
                Sound.Play("punch");
            }
            return;
        }
        if (comboWindow == 0) attack = 0;
        Walk();
    }

    void Walk()
    {
        fixed dx = Input.AxisX * speed;
        fixed dy = Input.AxisY * speed * 0.75;
        actor.pos.x += dx;
        actor.pos.y += dy;
        if (actor.pos.y < 108) actor.pos.y = 108;
        if (actor.pos.y > 152) actor.pos.y = 152;
        if (actor.pos.x < minX + 8) actor.pos.x = minX + 8;
        if (actor.pos.x > maxX - 8) actor.pos.x = maxX - 8;
        if (dx > 0) actor.facingLeft = false;
        if (dx < 0) actor.facingLeft = true;
        if (dx != 0 || dy != 0) actor.PlayAnimation("walk");
        else actor.PlayAnimation("idle");
    }

    public void Hurt(int fromX)
    {
        if (invincible > 0 || hurtTimer > 0) return;
        hp -= 2;
        hurtTimer = 20;
        invincible = 60;
        attackTimer = 0;
        if (fromX < actor.pos.x.Floor()) actor.pos.x += 8;
        else actor.pos.x -= 8;
        actor.PlayAnimation("hurt");
        Sound.Play("hurt");
        if (hp <= 0)
        {
            Game.lives -= 1;
            if (Game.lives == 0)
            {
                Scene.Load("Title");
                return;
            }
            hp = maxHp;
        }
        UpdateHud();
    }

    public void UpdateHud()
    {
        Actor hpLabel = Scene.Find("HUD HP");
        if (hpLabel != null) hpLabel.SetLabelNumber(hp, 2);
        Actor score = Scene.Find("HUD Score");
        if (score != null) score.SetLabelNumber(Game.score, 6);
        Actor lives = Scene.Find("HUD Lives");
        if (lives != null) lives.SetLabelNumber(Game.lives, 1);
    }
}
""");
        TemplateKit.Script(p, "Thug", """
// Walks up beside the fighter on the same floor line and strikes after a short wind-up. The fighter's hits stun it
// and push it back (the combo kick further); after three hits it blinks out.
public class Thug : Behaviour
{
    public fixed speed = 0.75;
    public int hp = 3;
    int stun;
    int windup;
    int lastHit;
    int dying;
    Actor fighter;

    void Start()
    {
        fighter = Scene.Find("Fighter");
    }

    int Abs(int v) => v < 0 ? -v : v;

    void Update()
    {
        if (dying > 0)
        {
            dying += 1;
            actor.visible = (dying & 4) == 0;
            if (dying == 40)
            {
                Actor stage = Scene.Find("Stage");
                if (stage != null) stage.Get<Waves>().alive -= 1;
                actor.Destroy();
            }
            return;
        }
        if (fighter == null) return;
        Fighter f = fighter.Get<Fighter>();
        int x = actor.pos.x.Floor();
        int y = actor.pos.y.Floor();
        int fx = fighter.pos.x.Floor();
        int fy = fighter.pos.y.Floor();
        if (f.hitActive && f.attackId != lastHit && Abs(fy - y) <= 8)
        {
            int reach = f.attack == 3 ? 30 : 26;
            bool inFront = fighter.facingLeft ? x <= fx && x >= fx - reach : x >= fx && x <= fx + reach;
            if (inFront)
            {
                lastHit = f.attackId;
                TakeHit(f.attack == 3, fighter.facingLeft);
                return;
            }
        }
        if (stun > 0)
        {
            stun -= 1;
            return;
        }
        actor.facingLeft = fx < x;
        int dx = fx - x;
        int dy = fy - y;
        if (windup > 0)
        {
            windup -= 1;
            if (windup == 0)
            {
                actor.PlayAnimation("strike");
                stun = 16;
                if (Abs(dx) <= 26 && Abs(dy) <= 8) f.Hurt(x);
            }
            return;
        }
        if (Abs(dx) <= 22 && Abs(dy) <= 6)
        {
            windup = 24;
            actor.PlayAnimation("windup");
            return;
        }
        int tx = dx > 0 ? fx - 20 : fx + 20;
        if (tx > x + 1) actor.pos.x += speed;
        if (tx < x - 1) actor.pos.x -= speed;
        if (dy > 1) actor.pos.y += speed / 2;
        if (dy < -1) actor.pos.y -= speed / 2;
        actor.PlayAnimation("walk");
    }

    void TakeHit(bool kick, bool toLeft)
    {
        hp -= 1;
        windup = 0;
        stun = kick ? 30 : 16;
        int push = kick ? 24 : 6;
        if (toLeft) actor.pos.x -= push;
        else actor.pos.x += push;
        actor.PlayAnimation("hurt");
        Sound.Play("hit");
        Game.score += kick ? 50 : 20;
        if (hp <= 0)
        {
            dying = 1;
            Game.score += 100;
        }
        fighter.Get<Fighter>().UpdateHud();
    }
}
""");
        TemplateKit.Script(p, "Waves", """
// Moves the camera with the fighter and holds it at three points along the pier until the wave of thugs there is
// beaten (two, three, then four thugs from both sides); "GO!" shows and the camera moves on. After the last wave
// the stage is clear.
public class Waves : Behaviour
{
    public int alive;
    public int lockX = 160;       // camera x of the first wave; the next ones follow every `spacing` pixels
    public int spacing = 280;
    int wave;
    int camX;
    bool locked;
    int goTimer;
    int clearTimer;
    Actor fighter;
    Actor go;

    void Start()
    {
        fighter = Scene.Find("Fighter");
        go = Scene.Find("Go");
    }

    void Update()
    {
        if (fighter == null) return;
        Fighter f = fighter.Get<Fighter>();
        if (!locked)
        {
            int want = fighter.pos.x.Floor() - 100;
            if (want > camX) camX = want;
            if (camX > 784) camX = 784;
            if (wave < 3 && camX >= lockX)
            {
                camX = lockX;
                locked = true;
                SpawnWave();
            }
        }
        else if (alive <= 0)
        {
            locked = false;
            wave += 1;
            lockX += spacing;
            if (wave == 3)
            {
                clearTimer = 1;
                if (go != null) go.SetLabelText("brawl.clear");
            }
            else
            {
                goTimer = 90;
                if (go != null) go.SetLabelText("brawl.go");
            }
        }
        Camera.SetPosition(camX, 0);
        f.minX = camX;
        f.maxX = locked ? camX + 240 : 1024;
        if (goTimer > 0)
        {
            goTimer -= 1;
            if (goTimer == 0 && go != null) go.SetLabelText("brawl.empty");
        }
        if (clearTimer > 0)
        {
            clearTimer += 1;
            if (clearTimer == 180) Scene.Load("Title");
        }
    }

    void SpawnWave()
    {
        int n = wave + 2;
        for (int i = 0; i < n; i++)
        {
            int x = (i & 1) == 0 ? camX + 256 + i * 12 : camX - 16 - i * 12;
            Actor t = Scene.Spawn("Thug", x, 112 + (i * 17) % 40);
            if (t != null) alive += 1;
        }
    }
}
""");
    }
}
