using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Shoot 'em up template (Launcher · Templates, P15.04): the stage scrolls by itself over two star layers; the ship
/// moves and fires, waves of slimes fly in and fire bullet patterns (aimed fans and rings), and a power-up gives
/// a triple shot. Shots, enemies and items are copies of hidden template actors made with Scene.Spawn.
/// </summary>
public static class ShmupTemplate
{
    public const int StageWidth = 120;  // 16 px tiles: 1920 px of scrolling

    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "ASHE", "Title", s =>
        {
            s.Save.Fields.AddRange([new SaveField { Name = "score", Type = "int" }, new SaveField { Name = "lives", Type = "byte" }]);
            s.Tags = ["Player", "Enemy", "Shot", "EnemyShot", "PowerUp"];
            s.Input[0].Action = "Fire";
        });
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("hud.score", "SCORE", "คะแนน", "UI & menus"),
            ("hud.lives", "1UP", "ชีวิต", "UI & menus"),
            ("stage.clear", "Stage clear!", "ผ่านด่านแล้ว!", "UI & menus"),
        ]));
        WriteArt(p);
        TemplateKit.Music(p, "stage_theme", XmWriter.Tune("Stage theme", 150, 4, 41, "E"));
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 120, 2, 43, "A"));
        TemplateKit.Sfx(p, "shot", 1400, 900, 0.05, 0.4);
        TemplateKit.Sfx(p, "hit", 300, 120, 0.12);
        TemplateKit.Sfx(p, "powerup", 500, 1500, 0.3);
        TemplateKit.Sfx(p, "hurt", 520, 80, 0.3);
        WriteScenes(p);
        WriteScripts(p);
        return p;
    }

    static string RomTitle(string name)
    {
        var t = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return t.Length == 0 ? "SHOOTER" : t[..Math.Min(12, t.Length)];
    }

    static readonly uint Space = Canvas.Rgb(12, 12, 32), StarDim = Canvas.Rgb(96, 104, 160), StarBright = Canvas.Rgb(232, 232, 255);
    static readonly uint Hull = Canvas.Rgb(220, 224, 236), HullDark = Canvas.Rgb(120, 128, 160), Engine = Canvas.Rgb(248, 152, 64);
    static readonly uint Purple = Canvas.Rgb(176, 104, 232), PurpleDark = Canvas.Rgb(112, 56, 176);

    /// <summary>A star field (256 x 160, transparent between stars): few bright stars for near, many dim for far.</summary>
    public static Canvas Stars(int seed, int count, uint color)
    {
        var c = new Canvas(256, 160);
        var rng = new Random(seed);
        for (var i = 0; i < count; i++) c.Set(rng.Next(256), rng.Next(160), color);
        return c;
    }

    public static Canvas Ship()
    {
        var c = new Canvas(16, 8);
        c.Rect(2, 3, 11, 2, Hull);
        c.Rect(4, 1, 6, 6, Hull);
        c.Rect(12, 3, 3, 2, Hull);
        c.Rect(6, 2, 3, 2, Canvas.Rgb(96, 168, 248));   // canopy
        c.Rect(4, 6, 6, 1, HullDark);
        c.Rect(0, 3, 2, 2, Engine);
        return c;
    }

    public static Canvas Slime()
    {
        var c = new Canvas(32, 16);
        for (var f = 0; f < 2; f++)
        {
            var x = f * 16;
            c.Ellipse(x + 7.5, 9.5 - f, 7, 6 + f * 0.5, Purple);
            c.Rect(x + 1, 12, 14, 3, Purple);
            c.Rect(x + 1, 14, 14, 1, PurpleDark);
            c.Rect(x + 4, 7, 2, 3, PixelArt.Ink);
            c.Rect(x + 9, 7, 2, 3, PixelArt.Ink);
        }
        return c;
    }

    public static Canvas Shot()
    {
        var c = new Canvas(8, 8);
        c.Rect(0, 3, 8, 2, PixelArt.Gold);
        c.Rect(2, 3, 4, 1, PixelArt.White);
        return c;
    }

    public static Canvas EnemyShot()
    {
        var c = new Canvas(8, 8);
        c.Ellipse(3.5, 3.5, 3, 3, Canvas.Rgb(248, 96, 160));
        c.Rect(3, 2, 2, 2, PixelArt.White);
        return c;
    }

    public static Canvas PowerUp()
    {
        var c = new Canvas(8, 8);
        c.Rect(0, 0, 8, 8, Canvas.Rgb(64, 200, 120));
        c.Rect(2, 1, 2, 6, PixelArt.White);
        c.Rect(4, 1, 2, 1, PixelArt.White);
        c.Rect(5, 2, 1, 2, PixelArt.White);
        c.Rect(4, 4, 2, 1, PixelArt.White);
        return c;
    }

    static void WriteArt(GameProject p)
    {
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        TemplateKit.Image(p, "stars_far", Stars(3, 90, StarDim));
        TemplateKit.Image(p, "stars_near", Stars(7, 30, StarBright));
        TemplateKit.Sprite(p, "ship", Ship(), new SpriteMeta { FrameWidth = 16, FrameHeight = 8, OriginX = 8, OriginY = 4, Collider = new ColliderData { X = 4, Y = 2, W = 8, H = 4 } });
        TemplateKit.Sprite(p, "space_slime", Slime(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 8, Collider = new ColliderData { X = 2, Y = 4, W = 12, H = 10 },
            Animations = { new AnimationData { Name = "fly", Frames = { 0, 1 }, Ticks = 12 } },
        });
        TemplateKit.Sprite(p, "shot", Shot(), new SpriteMeta { FrameWidth = 8, FrameHeight = 8, OriginX = 4, OriginY = 4, Collider = new ColliderData { X = 0, Y = 3, W = 8, H = 2 } });
        TemplateKit.Sprite(p, "enemy_shot", EnemyShot(), new SpriteMeta { FrameWidth = 8, FrameHeight = 8, OriginX = 4, OriginY = 4, Collider = new ColliderData { X = 2, Y = 2, W = 4, H = 4 } });
        TemplateKit.Sprite(p, "powerup", PowerUp(), new SpriteMeta { FrameWidth = 8, FrameHeight = 8, OriginX = 4, OriginY = 4, Collider = new ColliderData { X = 0, Y = 0, W = 8, H = 8 } });
    }

    static void WriteScenes(GameProject p)
    {
        static MapFile Space(string name, int width) => new()
        {
            Name = name, Width = width, Height = 10, CollisionLayer = -1,
            Layers =
            {
                new MapLayerData { Name = "Stars far", Type = LayerType.Image, Bg = 3, Priority = 3, ParallaxX = 0.25, ParallaxY = 0, Wrap = true, Image = "stars_far" },
                new MapLayerData { Name = "Stars near", Type = LayerType.Image, Bg = 2, Priority = 2, ParallaxX = 0.5, ParallaxY = 0, Wrap = true, Image = "stars_near" },
            },
        };
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), Space("Title", 15));
        GameProject.Save(Path.Combine(p.MapsDir, "Stage_01.map"), Space("Stage_01", StageWidth));
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene("Title", "#0C0C20", "Stage_01", "title_theme"));
        // Template actors (Shot, Enemy Shot, Slime, Power-up) wait hidden above the screen; Scene.Spawn copies them.
        static ComponentData Projectile(string vx = "0") => new() { Type = "Projectile", Fields = { ["vx"] = vx } };
        var stage = new SceneFile
        {
            Name = "Stage_01", Width = StageWidth, Height = 10, Map = "Stage_01", Backdrop = "#0C0C20", MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "stage_theme" },
            Camera = new CameraSettings { Follow = "", ClampToMap = true },
            Actors =
            {
                new ActorData { Name = "Ship", Kind = ActorKind.Player, Sprite = "ship", X = 40, Y = 80, Tag = "Player", Priority = 1, Components = { new ComponentData { Type = "ShipController" } } },
                new ActorData { Name = "Stage", Kind = ActorKind.Empty, Components = { new ComponentData { Type = "Stage" } } },
                new ActorData { Name = "Shot", Kind = ActorKind.Empty, Sprite = "shot", X = 0, Y = -64, Tag = "Shot", Hidden = true, Group = "Templates", Priority = 2,
                    Components = { Projectile() } },
                new ActorData { Name = "Enemy Shot", Kind = ActorKind.Empty, Sprite = "enemy_shot", X = 0, Y = -64, Tag = "EnemyShot", Hidden = true, Group = "Templates", Priority = 2,
                    Components = { new ComponentData { Type = "Projectile", Fields = { ["hitsPlayer"] = "true", ["lifetime"] = "240" } } } },
                new ActorData { Name = "Slime", Kind = ActorKind.Enemy, Sprite = "space_slime", Animation = "fly", X = 0, Y = -64, Tag = "Enemy", Hidden = true, Group = "Templates", Priority = 2,
                    Components = { new ComponentData { Type = "SpaceSlime" } } },
                new ActorData { Name = "Power-up", Kind = ActorKind.Pickup, Sprite = "powerup", X = 0, Y = -64, Tag = "PowerUp", Hidden = true, Group = "Templates", Priority = 2,
                    Components = { new ComponentData { Type = "PowerUpMarker" } } },
                new ActorData { Name = "HUD Score", Kind = ActorKind.TextLabel, X = 8, Y = 4, Text = "hud.score", Group = "HUD" },
                new ActorData { Name = "HUD Lives", Kind = ActorKind.TextLabel, X = 176, Y = 4, Text = "hud.lives", Group = "HUD" },
            },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Stage_01.scene"), stage);
    }

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "Stage", """
// Scrolls the stage one pixel a frame and sends waves of slimes by distance; at the end the stage is clear.
public class Stage : Behaviour
{
    public int speed = 1;
    int camX;
    int wave;
    int clearTimer;

    void Start()
    {
        if (Game.lives == 0)
        {
            Game.lives = 3;
            Game.score = 0;
        }
    }

    void Update()
    {
        if (camX < 1680) camX += speed;
        Camera.SetPosition(camX, 0);
        // A wave every 160 pixels: rows of three, alternating fan shooters and ring shooters.
        if (camX >= 120 + wave * 160 && wave < 10)
        {
            int kind = wave & 1;
            for (int i = 0; i < 3; i++)
            {
                Actor slime = Scene.Spawn("Slime", camX + 256 + i * 24, 40 + i * 32);
                if (slime != null) slime.Get<SpaceSlime>().pattern = kind;
            }
            if (wave == 3 || wave == 7) Scene.Spawn("Power-up", camX + 248, 80);
            wave += 1;
        }
        if (camX >= 1680)
        {
            clearTimer += 1;
            if (clearTimer == 120) Scene.Load("Title");
        }
    }
}
""");
        TemplateKit.Script(p, "ShipController", """
// Moves inside the screen while the stage scrolls, fires with A (three shots after a power-up) and loses a life
// when an enemy or an enemy shot hits it.
public class ShipController : Behaviour
{
    public fixed speed = 2;
    public int fireDelay = 8;
    int screenX = 40;
    int screenY = 80;
    int cooldown;
    int triple;
    int invincible;

    void Start() => UpdateHud();

    void Update()
    {
        screenX += (Input.AxisX * speed).Floor();
        screenY += (Input.AxisY * speed).Floor();
        if (screenX < 8) screenX = 8;
        if (screenX > 200) screenX = 200;
        if (screenY < 24) screenY = 24;
        if (screenY > 152) screenY = 152;
        actor.pos.x = Camera.X + screenX;
        actor.pos.y = screenY;

        if (cooldown > 0) cooldown -= 1;
        if (Input.Held(Key.A) && cooldown == 0)
        {
            cooldown = fireDelay;
            Fire(0);
            if (triple > 0)
            {
                Fire(-1);
                Fire(1);
            }
            Sound.Play("shot");
        }
        if (triple > 0) triple -= 1;
        if (invincible > 0)
        {
            invincible -= 1;
            actor.visible = invincible == 0 || (invincible & 4) == 0;
        }
    }

    void Fire(int spread)
    {
        Actor shot = Scene.Spawn("Shot", actor.pos.x.Floor() + 8, actor.pos.y.Floor());
        if (shot == null) return;
        Projectile p = shot.Get<Projectile>();
        p.vx = 5;
        p.vy = spread;
    }

    void OnTouch(Actor other)
    {
        if (other.tag == "PowerUp")
        {
            triple = 600;
            Sound.Play("powerup");
            other.Destroy();
            return;
        }
        if (invincible > 0) return;
        if (other.tag != "Enemy" && other.tag != "EnemyShot") return;
        if (other.tag == "EnemyShot") other.Destroy();
        Sound.Play("hurt");
        Game.lives -= 1;
        invincible = 120;
        UpdateHud();
        if (Game.lives == 0) Scene.Load("Title");
    }

    public void UpdateHud()
    {
        Actor score = Scene.Find("HUD Score");
        if (score != null) score.SetLabelNumber(Game.score, 6);
        Actor lives = Scene.Find("HUD Lives");
        if (lives != null) lives.SetLabelNumber(Game.lives, 1);
    }
}

// A power-up drifts slowly to the left; the ship picks it up by touching it.
public class PowerUpMarker : Behaviour
{
    void Update()
    {
        actor.pos.x -= 0.5;
        if (actor.pos.x.Floor() < Camera.X - 16) actor.Destroy();
    }
}
""");
        TemplateKit.Script(p, "SpaceSlime", """
// Flies left on a wave and fires a pattern: 0 a fan of three shots, 1 a ring of six. Shots of the ship
// destroy it.
public class SpaceSlime : Behaviour
{
    public int pattern;
    public int hp = 2;
    int t;
    int baseY;

    void Start()
    {
        baseY = actor.pos.y.Floor();
        t = Random.Range(0, 60);  // slimes of one wave do not all fire on the same frame
    }

    void Update()
    {
        t += 1;
        actor.pos.x -= 1;
        actor.pos.y = baseY + (fixed.Sin(t * 4) * 16).Floor();
        if (t % 90 == 45) FirePattern();
        if (actor.pos.x.Floor() < Camera.X - 32) actor.Destroy();
    }

    void FirePattern()
    {
        if (pattern == 0)
        {
            Shoot(-2, 0);
            Shoot(-2, -1);
            Shoot(-2, 1);
        }
        else
        {
            for (int i = 0; i < 6; i++) Shoot((fixed.Cos(i * 43) * 2).Floor(), (fixed.Sin(i * 43) * 2).Floor());
        }
    }

    void Shoot(int vx, int vy)
    {
        Actor shot = Scene.Spawn("Enemy Shot", actor.pos.x.Floor(), actor.pos.y.Floor());
        if (shot == null) return;
        Projectile p = shot.Get<Projectile>();
        p.vx = vx;
        p.vy = vy;
    }

    void OnTouch(Actor other)
    {
        if (other.tag != "Shot") return;
        other.Destroy();
        hp -= 1;
        if (hp > 0) return;
        Sound.Play("hit");
        Game.score += 100;
        Actor ship = Scene.Find("Ship");
        if (ship != null) ship.Get<ShipController>().UpdateHud();
        actor.Destroy();
    }
}
""");
    }
}
