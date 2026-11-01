using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Racing template (Launcher · Templates, P15.08): a kart drives on a circuit drawn as a Mode 7 floor (the map's
/// rotate / scale layer seen from behind the kart, Scene.Mode7). Grass slows the kart, the barrier bounces it back;
/// passing the checkpoint and then the finish line counts a lap, and the best time of three laps is saved. Two
/// computer karts drive round the centre line; they are drawn where their floor position shows on screen, in one of
/// three sizes by distance.
/// </summary>
public static class RacingTemplate
{
    public const int TGrass = 0, TRoad = 1, TKerb = 2, TFinish = 3, TCheck = 4, TWall = 5;
    public const int Side = 64;  // circuit size in 16 px tiles (an affine map of 128 x 128 hardware tiles)

    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "ARCE", "Title", s =>
        {
            s.Save.Fields.AddRange([new SaveField { Name = "best", Type = "int" }]);
            s.Tags = ["Player", "Grass", "Finish", "Check"];
            s.Input[0].Action = "Accelerate";
        });
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("hud.lap", "LAP", "รอบ", "UI & menus"),
            ("hud.time", "TIME", "เวลา", "UI & menus"),
            ("hud.best", "BEST", "ดีที่สุด", "UI & menus"),
            ("race.ready", "Ready...", "เตรียมตัว...", "UI & menus"),
            ("race.go", "GO!", "ไป!", "UI & menus"),
            ("race.finish", "Finish!", "เข้าเส้นชัย!", "UI & menus"),
            ("race.empty", " ", " ", "UI & menus"),
        ]));
        TemplateKit.Tileset(p, "circuit", Tiles(), Meta());
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        TemplateKit.Image(p, "sky", Sky());
        TemplateKit.Sprite(p, "kart", Kart(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 15,
            Animations =
            {
                new AnimationData { Name = "straight", Frames = { 0 }, Loop = false },
                new AnimationData { Name = "left", Frames = { 1 }, Loop = false },
                new AnimationData { Name = "right", Frames = { 2 }, Loop = false },
            },
        });
        TemplateKit.Sprite(p, "rival", Rival(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 15,
            Animations =
            {
                new AnimationData { Name = "near", Frames = { 0 }, Loop = false },
                new AnimationData { Name = "mid", Frames = { 1 }, Loop = false },
                new AnimationData { Name = "far", Frames = { 2 }, Loop = false },
            },
        });
        TemplateKit.Music(p, "race_theme", XmWriter.Tune("Race theme", 160, 4, 53, "C"));
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 118, 2, 59, "F"));
        TemplateKit.Sfx(p, "go", 900, 900, 0.3);
        TemplateKit.Sfx(p, "lap", 800, 1600, 0.25);
        TemplateKit.Sfx(p, "bump", 220, 90, 0.12);
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
        return t.Length == 0 ? "RACING" : t[..Math.Min(12, t.Length)];
    }

    static readonly uint Grass = Canvas.Rgb(72, 152, 64), GrassDark = Canvas.Rgb(56, 128, 52);
    static readonly uint Asphalt = Canvas.Rgb(96, 96, 108), AsphaltDark = Canvas.Rgb(84, 84, 96);
    static readonly uint KerbRed = Canvas.Rgb(216, 56, 48), Barrier = Canvas.Rgb(200, 200, 208), BarrierDark = Canvas.Rgb(128, 128, 144);

    /// <summary>Circuit tiles: grass, road, kerb, finish line, checkpoint (road with a faint line) and barrier.</summary>
    public static Canvas Tiles()
    {
        var c = new Canvas(16 * 6, 16);
        // Grass in a 8 px checker, so speed and turning show on the floor.
        c.Rect(0, 0, 16, 16, Grass);
        c.Rect(0, 0, 8, 8, GrassDark);
        c.Rect(8, 8, 8, 8, GrassDark);
        c.Rect(TRoad * 16, 0, 16, 16, Asphalt);
        c.Rect(TRoad * 16 + 3, 5, 2, 2, AsphaltDark);
        c.Rect(TRoad * 16 + 11, 12, 2, 2, AsphaltDark);
        for (var k = 0; k < 4; k++) c.Rect(TKerb * 16 + (k % 2) * 8, (k / 2) * 8, 8, 8, k is 0 or 3 ? KerbRed : PixelArt.White);
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                c.Rect(TFinish * 16 + x * 4, y * 4, 4, 4, (x + y) % 2 == 0 ? PixelArt.White : PixelArt.Ink);
        c.Rect(TCheck * 16, 0, 16, 16, Asphalt);
        c.Rect(TCheck * 16, 7, 16, 2, AsphaltDark);
        c.Rect(TWall * 16, 0, 16, 16, Barrier);
        c.Rect(TWall * 16, 12, 16, 4, BarrierDark);
        c.Rect(TWall * 16 + 7, 0, 2, 12, BarrierDark);
        return c;
    }

    static TilesetMeta Meta() => new()
    {
        Tiles =
        {
            [TGrass] = new TileProperties { Name = "grass", Tag = "Grass" },
            [TRoad] = new TileProperties { Name = "road" },
            [TKerb] = new TileProperties { Name = "kerb" },
            [TFinish] = new TileProperties { Name = "finish", Tag = "Finish" },
            [TCheck] = new TileProperties { Name = "checkpoint", Tag = "Check" },
            [TWall] = new TileProperties { Name = "barrier", Collision = CollisionType.Solid },
        },
    };

    /// <summary>Sky behind the floor (256 x 160): light towards the horizon at line 40, hills on it, grass below
    /// (seen past the edge of the circuit).</summary>
    public static Canvas Sky()
    {
        var c = new Canvas(256, 160);
        uint[] bands = [Canvas.Rgb(88, 144, 232), Canvas.Rgb(112, 168, 240), Canvas.Rgb(144, 192, 248), Canvas.Rgb(184, 216, 248)];
        for (var b = 0; b < 4; b++) c.Rect(0, b * 10, 256, 10, bands[b]);
        // Hills: two rounded ranges that repeat every 256 px, so the sky wraps as the kart turns.
        for (var x = 0; x < 256; x++)
        {
            var far = 6 + (int)(4 * Math.Sin(x * Math.PI / 32) + 3 * Math.Sin(x * Math.PI / 64 + 1));
            var near = 3 + (int)(3 * Math.Sin(x * Math.PI / 16 + 2));
            c.Rect(x, 40 - far, 1, far, Canvas.Rgb(96, 128, 168));
            c.Rect(x, 40 - near, 1, near, Canvas.Rgb(64, 120, 80));
        }
        c.Rect(0, 40, 256, 120, Grass);
        return c;
    }

    /// <summary>The kart from behind: straight, leaning left and leaning right.</summary>
    public static Canvas Kart()
    {
        var c = new Canvas(48, 16);
        for (var f = 0; f < 3; f++)
        {
            var x = f * 16;
            var lean = f == 1 ? -1 : f == 2 ? 1 : 0;
            c.Rect(x + 1, 10, 4, 5, PixelArt.Ink);                      // wheels
            c.Rect(x + 11, 10, 4, 5, PixelArt.Ink);
            c.Rect(x + 3, 8, 10, 6, Canvas.Rgb(232, 72, 56));           // body
            c.Rect(x + 3, 13, 10, 1, Canvas.Rgb(160, 40, 40));
            c.Rect(x + 5 + lean, 3, 6, 6, Canvas.Rgb(248, 208, 64));    // helmet
            c.Rect(x + 6 + lean, 5, 4, 2, Canvas.Rgb(48, 64, 112));     // visor
            c.Rect(x + 6, 11, 4, 1, Canvas.Rgb(248, 152, 64));          // exhaust glow
        }
        return c;
    }

    /// <summary>A computer kart from behind at three distances: 16, 10 and 6 pixels wide.</summary>
    public static Canvas Rival()
    {
        var c = new Canvas(48, 16);
        uint body = Canvas.Rgb(64, 120, 232), dark = Canvas.Rgb(40, 72, 160), helmet = PixelArt.White;
        c.Rect(1, 10, 4, 5, PixelArt.Ink);
        c.Rect(11, 10, 4, 5, PixelArt.Ink);
        c.Rect(3, 8, 10, 6, body);
        c.Rect(3, 13, 10, 1, dark);
        c.Rect(5, 3, 6, 6, helmet);
        c.Rect(6, 5, 4, 2, dark);
        c.Rect(16 + 3, 12, 2, 3, PixelArt.Ink);
        c.Rect(16 + 11, 12, 2, 3, PixelArt.Ink);
        c.Rect(16 + 4, 10, 8, 4, body);
        c.Rect(16 + 6, 7, 4, 4, helmet);
        c.Rect(32 + 5, 13, 6, 2, PixelArt.Ink);
        c.Rect(32 + 5, 11, 6, 3, body);
        c.Rect(32 + 7, 9, 2, 2, helmet);
        return c;
    }

    /// <summary>The circuit: a rounded rectangle with a road 5 tiles wide, kerbs on both sides, grass inside and
    /// out, and a barrier around the map. The finish line crosses the left straight, the checkpoint the right one.</summary>
    public static int[] Circuit()
    {
        var cells = new int[Side * Side];
        for (var y = 0; y < Side; y++)
            for (var x = 0; x < Side; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                double qx = Math.Abs(px - 32) - 12, qy = Math.Abs(py - 32) - 12;
                var d = Math.Sqrt(Math.Pow(Math.Max(qx, 0), 2) + Math.Pow(Math.Max(qy, 0), 2)) + Math.Min(Math.Max(qx, qy), 0) - 10;
                var t = TGrass;
                if (x == 0 || y == 0 || x == Side - 1 || y == Side - 1) t = TWall;
                else if (Math.Abs(d) < 2.6) t = y == 32 ? (x < 32 ? TFinish : TCheck) : TRoad;
                else if (Math.Abs(d) < 3.4) t = TKerb;
                cells[y * Side + x] = t;
            }
        return cells;
    }

    static void WriteMaps(GameProject p)
    {
        var cells = Circuit().ToList();
        // The floor is the rotate / scale layer (BG2, Mode 1); collision and tags come from the same cells.
        var circuit = new MapFile { Name = "Circuit", Tileset = "circuit", Width = Side, Height = Side, CollisionLayer = 2 };
        circuit.Layers.Add(new MapLayerData { Name = "Track", Type = LayerType.Affine, Bg = 2, Priority = 2, Cells = cells });
        circuit.Layers.Add(new MapLayerData { Name = "Sky", Type = LayerType.Image, Bg = 0, Priority = 3, ParallaxX = 1, ParallaxY = 0, Wrap = true, Image = "sky" });
        circuit.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.CollisionOnly, Bg = 0, Cells = cells });
        GameProject.Save(Path.Combine(p.MapsDir, "Circuit.map"), circuit);

        // Title: a flat view of the start straight.
        var title = new int[15 * 10];
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 15; x++)
                title[y * 15 + x] = y < 8 ? TGrass : y == 8 ? TKerb : x == 3 ? TFinish : TRoad;  // the menu stays on the grass
        var titleMap = new MapFile { Name = "Title", Tileset = "circuit", Width = 15, Height = 10, CollisionLayer = -1 };
        titleMap.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = title.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), titleMap);
    }

    static void WriteScenes(GameProject p)
    {
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene("Title", "#3C78B4", "Circuit", "title_theme"));
        var race = new SceneFile
        {
            Name = "Circuit", Width = Side, Height = Side, Map = "Circuit", Backdrop = "#58A040", VideoMode = 1, MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "race_theme" },
            Camera = new CameraSettings { Follow = "", ClampToMap = false },
            Actors =
            {
                new ActorData { Name = "Player", Kind = ActorKind.Player, Sprite = "kart", Animation = "straight", X = 120, Y = 136, Tag = "Player", Priority = 1,
                    Components = { new ComponentData { Type = "Kart" } } },
                new ActorData { Name = "Rival 1", Kind = ActorKind.Enemy, Sprite = "rival", Animation = "far", X = 0, Y = -32, Priority = 1, Group = "Rivals",
                    Components = { new ComponentData { Type = "Rival", Fields = { ["speed"] = "2.6", ["lane"] = "-20", ["start"] = "170" } } } },
                new ActorData { Name = "Rival 2", Kind = ActorKind.Enemy, Sprite = "rival", Animation = "far", X = 0, Y = -32, Priority = 1, Group = "Rivals",
                    Components = { new ComponentData { Type = "Rival", Fields = { ["speed"] = "2.3", ["lane"] = "20", ["start"] = "200" } } } },
                new ActorData { Name = "HUD Lap", Kind = ActorKind.TextLabel, X = 8, Y = 4, Text = "hud.lap", Group = "HUD" },
                new ActorData { Name = "HUD Time", Kind = ActorKind.TextLabel, X = 160, Y = 4, Text = "hud.time", Group = "HUD" },
                new ActorData { Name = "HUD Best", Kind = ActorKind.TextLabel, X = 160, Y = 20, Text = "hud.best", Group = "HUD" },
                new ActorData { Name = "HUD Status", Kind = ActorKind.TextLabel, X = 96, Y = 60, Text = "race.ready", Group = "HUD" },
            },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Circuit.scene"), race);
    }

    // ---- README -----------------------------------------------------------------------------------------

    static void WriteReadme(GameProject p) => TemplateKit.Readme(p, "Racing (Mode 7)",
        "Three laps of a rounded circuit against two computer karts. The track is one big rotating background (Mode 1, BG2 affine) that Scene.Mode7 tilts into a floor, redrawn every frame from the kart's position and heading; the sky scrolls sideways as the kart turns.",
        ["Up or A: accelerate", "B: brake", "D-pad left / right: steer", "Start: pause menu"],
        new Dictionary<string, string>
        {
            ["Title/Title"] = "The game title (string key ui.title). The Start / Continue / Options menu under it is the scene's Menu.",
            ["Player"] = "Your kart. The Kart script keeps its own floor position and heading, drives, counts laps with the Check and Finish tile tags, and moves the Mode 7 camera behind it; the sprite stays at the bottom middle of the screen.",
            ["Rival 1"] = "Computer kart: the Rival script drives it round the centre line at its own speed and lane, and draws it where it shows on the floor (smaller when far away).",
            ["Rival 2"] = "A slower computer kart on the other lane.",
            ["HUD Lap"] = "Text label with the lap number.",
            ["HUD Time"] = "Text label with the race time in seconds.",
            ["HUD Best"] = "Text label with the best time, kept in the save data.",
            ["HUD Status"] = "Text label in the middle of the screen: Ready, GO! and Finish!",
        },
        [
            "Select Player and change `topSpeed`, `accel` or `grassSpeed` of Kart, or `laps` for a longer race.",
            "Give a rival more `speed` in the Inspector, or copy Rival 2 (Ctrl+D) with another `lane` and `start`.",
            "Change `cameraBack` of Kart, or the height (32) and horizon (40) in the Scene.Mode7 call, to change the view.",
            "Paint the circuit tiles in the Tilemap Editor: tiles tagged Grass slow the kart, the barrier is solid.",
        ]);

    // ---- scripts ----------------------------------------------------------------------------------------

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "Kart", """
// Drives the kart on the Mode 7 floor: Up or A speeds up, B brakes, Left and Right steer. Grass slows the kart and
// the barrier bounces it back. Passing the checkpoint and then the finish line counts a lap; after the last lap
// the time is compared with the saved best.
public class Kart : Behaviour
{
    // Public fields show in the Inspector under "Kart".
    public fixed accel = 0.05;
    public fixed topSpeed = 3;
    public fixed grassSpeed = 1;
    public int laps = 3;
    public int cameraBack = 53;   // the floor point under the kart sprite (screen line 136) is this far ahead
    public fixed camX;            // the Mode 7 camera, read by the rivals to place themselves on screen
    public fixed camY;
    public int heading = 192;     // angles are 256ths of a turn: 0 looks along +x, 64 along +y, 192 up the left straight
    fixed x = 168;                // the kart's position on the floor map, in pixels
    fixed y = 568;
    fixed speed;
    int lap = 1;
    bool checkpoint;
    int timer;
    int countdown = 120;          // two seconds of "Ready..."
    int finished;
    Actor status;

    // Start runs once, when the scene has loaded the actor.
    void Start()
    {
        // Scene.Find looks an actor up by its Hierarchy name.
        status = Scene.Find("HUD Status");
        UpdateHud();
        Show();
    }

    // Update runs every frame (60 times a second).
    void Update()
    {
        if (countdown > 0)
        {
            countdown -= 1;
            if (countdown == 0)
            {
                Sound.Play("go");
                // SetLabelText puts another string key into a text label.
                if (status != null) status.SetLabelText("race.go");
            }
            Show();
            return;
        }
        if (finished > 0)
        {
            // After the finish the kart rolls out and the game goes back to the title after four seconds.
            finished += 1;
            speed = speed * 0.95;
            Move();
            if (finished == 240) Scene.Load("Title");
            Show();
            return;
        }
        timer += 1;
        if (timer == 60) if (status != null) status.SetLabelText("race.empty");
        Drive();
        Move();
        CountLaps();
        if ((timer & 15) == 0) UpdateHud();
        Show();
    }

    void Drive()
    {
        // Input.Held stays true while a button is down.
        int turn = 0;
        if (Input.Held(Key.Left)) turn = -1;
        if (Input.Held(Key.Right)) turn = 1;
        if (speed > 0.25 || speed < -0.25) heading = (heading + turn * 2) & 255;
        // The kart sprite has straight, left and right animations (Sprite Editor > Animations).
        actor.PlayAnimation(turn < 0 ? "left" : turn > 0 ? "right" : "straight");

        // Scene.TagAt reads the Tag of the tile under a point (Tileset properties: Grass, Check, Finish).
        fixed limit = Scene.TagAt(x.Floor(), y.Floor()) == "Grass" ? grassSpeed : topSpeed;
        if (Input.Held(Key.Up) || Input.Held(Key.A)) speed += accel;
        else if (speed > 0) speed -= accel / 2;
        if (Input.Held(Key.B)) speed -= accel * 2;
        if (speed > limit) speed -= accel * 2;
        if (speed < -1) speed = -1;
    }

    void Move()
    {
        // fixed.Cos and fixed.Sin take a 256ths-of-a-turn angle: one step along the heading.
        fixed nx = x + fixed.Cos(heading) * speed;
        fixed ny = y + fixed.Sin(heading) * speed;
        // Scene.SolidAt asks the collision layer (Ground) whether that point is solid: the barrier.
        if (Scene.SolidAt(nx.Floor(), ny.Floor()))
        {
            speed = -speed / 2;
            Sound.Play("bump");
            return;
        }
        x = nx;
        y = ny;
    }

    // A lap counts when the kart crosses the Finish tiles after the Check tiles (no short cuts backwards).
    void CountLaps()
    {
        if (Scene.TagAt(x.Floor(), y.Floor()) == "Check") checkpoint = true;
        if (Scene.TagAt(x.Floor(), y.Floor()) != "Finish" || !checkpoint) return;
        checkpoint = false;
        Sound.Play("lap");
        if (lap == laps)
        {
            finished = 1;
            if (status != null) status.SetLabelText("race.finish");
            // Game.best is a save field (Project Settings > Save): the best time in frames.
            if (Game.best == 0 || timer < Game.best) Game.best = timer;
            UpdateHud();
            return;
        }
        lap += 1;
        UpdateHud();
    }

    // The camera sits behind the kart; the sky scrolls with the heading (four hill ranges per turn).
    void Show()
    {
        Camera.SetPosition(heading * 4, 0);
        camX = x - fixed.Cos(heading) * cameraBack;
        camY = y - fixed.Sin(heading) * cameraBack;
        // Scene.Mode7(x, y, angle, height, horizon): draws the affine BG2 as a floor seen from (x, y) at that
        // angle, from `height` above it, with the horizon on screen line `horizon`.
        Scene.Mode7(camX, camY, heading, 32, 40);
        actor.pos.x = Camera.X + 120;
        actor.pos.y = 136;
    }

    void UpdateHud()
    {
        // SetLabelNumber writes a number with leading zeros into a text label.
        Actor lapLabel = Scene.Find("HUD Lap");
        if (lapLabel != null) lapLabel.SetLabelNumber(lap, 1);
        Actor time = Scene.Find("HUD Time");
        if (time != null) time.SetLabelNumber(timer / 60, 3);
        Actor best = Scene.Find("HUD Best");
        if (best != null) best.SetLabelNumber(Game.best / 60, 3);
    }
}
""");
        TemplateKit.Script(p, "Rival", """
// A computer kart: drives round the centre line of the circuit (a rounded rectangle: four straights of 384 pixels
// joined by quarter circles of radius 160) at its own speed, `lane` pixels to the side. It is drawn where its floor
// position shows on the Player's Mode 7 view: nearer karts lower on the screen and bigger.
public class Rival : Behaviour
{
    public fixed speed = 2.5;
    public int lane;              // pixels right (+) or left (-) of the centre line
    public fixed start;           // how far along the circuit it starts
    fixed along;
    fixed px;
    fixed py;
    int wait = 120;               // the start countdown of the Kart
    Actor kart;

    void Start()
    {
        along = start;
        kart = Scene.Find("Player");
    }

    void Update()
    {
        if (wait > 0) wait -= 1;
        else along += speed;
        if (along >= 2540) along -= 2540;   // one lap is about 2540 pixels
        Place();
        Draw();
    }

    // Floor position at distance `along` from the start of the left straight.
    void Place()
    {
        fixed rest = along;
        int piece = 0;
        while (piece < 7 && rest >= PieceLength(piece))
        {
            rest -= PieceLength(piece);
            piece += 1;
        }
        int k = piece / 2;
        int dir;
        if ((piece & 1) == 0)
        {
            // a straight
            dir = k * 64 + 192;
            px = StraightX(k) + fixed.Cos(dir) * rest;
            py = StraightY(k) + fixed.Sin(dir) * rest;
        }
        else
        {
            // a quarter circle (251 px long) round a corner centre
            int a = k * 64 + 128 + (rest * 64 / 251).Floor();
            dir = a + 64;
            px = CornerX(k) + fixed.Cos(a) * 160;
            py = CornerY(k) + fixed.Sin(a) * 160;
        }
        px -= fixed.Sin(dir) * lane;
        py += fixed.Cos(dir) * lane;
    }

    fixed PieceLength(int piece) => (piece & 1) == 0 ? 384 : 251;
    int StraightX(int k) => k == 0 ? 160 : k == 1 ? 320 : k == 2 ? 864 : 704;
    int StraightY(int k) => k == 0 ? 704 : k == 1 ? 160 : k == 2 ? 320 : 864;
    int CornerX(int k) => k == 0 || k == 3 ? 320 : 704;
    int CornerY(int k) => k < 2 ? 320 : 704;

    // Camera space: z ahead of the camera, side to its right. A floor point z ahead shows on screen line
    // horizon + height * 160 / z, and side * 160 / z pixels from the middle.
    void Draw()
    {
        if (kart == null) return;
        // Get<T>() reaches the Kart script of the Player actor to read its camera.
        Kart k = kart.Get<Kart>();
        fixed rx = px - k.camX;
        fixed ry = py - k.camY;
        fixed c = fixed.Cos(k.heading);
        fixed s = fixed.Sin(k.heading);
        fixed z = rx * c + ry * s;
        fixed side = ry * c - rx * s;
        if (z < 30 || z > 700)
        {
            actor.visible = false;   // behind the camera or too far away
            return;
        }
        int sx = 120 + (side * 160 / z).Floor();
        int sy = 40 + (5120 / z).Floor();
        actor.visible = sx > -16 && sx < 256;
        actor.pos.x = Camera.X + sx;
        actor.pos.y = sy;
        // three sizes of the rival sprite, as animations
        actor.PlayAnimation(z < 110 ? "near" : z < 240 ? "mid" : "far");
    }
}
""");
    }
}
