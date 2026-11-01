using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Puzzle template (Launcher · Templates, P15.05): pairs of gems fall into a well; move and turn them, and three or
/// more of one colour in a row or column clear. Blocks above fall into the gaps, and chains score more. The board
/// is a map layer the script edits with Scene.SetTile.
/// </summary>
public static class PuzzleTemplate
{
    public const int TEmpty = 0, TRed = 1, TYellow = 2, TGreen = 3, TBlue = 4, TPurple = 5, TWall = 6, TPanel = 7;
    public const int WellX = 1, WellW = 6, WellH = 9;   // the well on the left, the score panel on the right

    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "APZE", "Title", s =>
        {
            s.Save.Fields.AddRange([new SaveField { Name = "score", Type = "int" }, new SaveField { Name = "best", Type = "int" }]);
            s.Tags = ["Board"];
            s.Input[0].Action = "Turn";
        });
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("hud.score", "SCORE", "คะแนน", "UI & menus"),
            ("hud.next", "NEXT", "ถัดไป", "UI & menus"),
            ("hud.best", "BEST", "สูงสุด", "UI & menus"),
        ]));
        TemplateKit.Tileset(p, "gems", Tiles(), Meta());
        TemplateKit.Music(p, "puzzle_theme", XmWriter.Tune("Puzzle theme", 128, 4, 31, "D"));
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 100, 2, 29, "G"));
        TemplateKit.Sfx(p, "move", 600, 600, 0.03, 0.4);
        TemplateKit.Sfx(p, "land", 260, 180, 0.08);
        TemplateKit.Sfx(p, "clear", 700, 1600, 0.25);
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
        return t.Length == 0 ? "PUZZLE" : t[..Math.Min(12, t.Length)];
    }

    static readonly uint Night = Canvas.Rgb(28, 24, 52), Well = Canvas.Rgb(20, 18, 36), WallC = Canvas.Rgb(72, 64, 120), WallLight = Canvas.Rgb(104, 96, 160);

    /// <summary>Gem tiles: empty well, five colours, the wall and the side panel.</summary>
    public static Canvas Tiles()
    {
        var c = new Canvas(16 * 8, 16);
        c.Rect(0, 0, 16, 16, Well);
        c.Rect(7, 7, 2, 2, Night);
        uint[] colors = [Canvas.Rgb(232, 72, 72), Canvas.Rgb(248, 208, 64), Canvas.Rgb(88, 200, 96), Canvas.Rgb(72, 136, 240), Canvas.Rgb(176, 96, 224)];
        for (var k = 0; k < 5; k++)
        {
            var x = (k + 1) * 16;
            c.Rect(x, 0, 16, 16, Well);
            c.Rect(x + 1, 1, 14, 14, colors[k]);
            c.Rect(x + 3, 3, 5, 3, PixelArt.White);              // shine
            c.Rect(x + 1, 13, 14, 2, Canvas.Rgb(0, 0, 0));         // shadow edge
        }
        c.Rect(TWall * 16, 0, 16, 16, WallC);
        c.Rect(TWall * 16, 0, 16, 2, WallLight);
        c.Rect(TWall * 16 + 7, 2, 1, 14, WallLight);
        c.Rect(TPanel * 16, 0, 16, 16, Night);
        return c;
    }

    static TilesetMeta Meta() => new()
    {
        Tiles =
        {
            [TEmpty] = new TileProperties { Name = "empty" },
            [TRed] = new TileProperties { Name = "red" }, [TYellow] = new TileProperties { Name = "yellow" },
            [TGreen] = new TileProperties { Name = "green" }, [TBlue] = new TileProperties { Name = "blue" },
            [TPurple] = new TileProperties { Name = "purple" },
            [TWall] = new TileProperties { Name = "wall" }, [TPanel] = new TileProperties { Name = "panel" },
        },
    };

    static void WriteMaps(GameProject p)
    {
        var cells = new int[15 * 10];
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 15; x++)
                cells[y * 15 + x] = x == WellX - 1 || x == WellX + WellW || y == WellH ? TWall : x >= WellX && x < WellX + WellW ? TEmpty : TPanel;
        var board = new MapFile { Name = "Board", Tileset = "gems", Width = 15, Height = 10, CollisionLayer = -1 };
        board.Layers.Add(new MapLayerData { Name = "Board", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = cells.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Board.map"), board);
        var title = new MapFile { Name = "Title", Tileset = "gems", Width = 15, Height = 10, CollisionLayer = -1 };
        var t = new int[150];
        for (var i = 0; i < 150; i++) t[i] = TPanel;
        foreach (var (x, y, g) in new[] { (2, 8, TRed), (3, 8, TBlue), (3, 7, TYellow), (11, 8, TGreen), (12, 8, TPurple), (12, 7, TRed) }) t[y * 15 + x] = g;
        title.Layers.Add(new MapLayerData { Name = "Board", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = t.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), title);
    }

    static void WriteScenes(GameProject p)
    {
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene("Title", "#1C1834", "Board", "title_theme"));
        var board = new SceneFile
        {
            Name = "Board", Width = 15, Height = 10, Map = "Board", Backdrop = "#1C1834", MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "puzzle_theme" },
            Camera = new CameraSettings { Follow = "" },
            Actors =
            {
                new ActorData { Name = "Board", Kind = ActorKind.Empty, Tag = "Board", Components = { new ComponentData { Type = "Board" } } },
                new ActorData { Name = "HUD Score", Kind = ActorKind.TextLabel, X = 136, Y = 16, Text = "hud.score", Group = "HUD",
                    Components = { new ComponentData { Type = "Scoreboard" } } },
                new ActorData { Name = "HUD Next", Kind = ActorKind.TextLabel, X = 136, Y = 72, Text = "hud.next", Group = "HUD" },
                new ActorData { Name = "HUD Best", Kind = ActorKind.TextLabel, X = 136, Y = 40, Text = "hud.best", Group = "HUD" },
            },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Board.scene"), board);
    }

    // ---- README -----------------------------------------------------------------------------------------

    static void WriteReadme(GameProject p) => TemplateKit.Readme(p, "Puzzle",
        "Pairs of gems fall into the well. Line up three or more of one colour in a row or a column to clear them; gems above fall down and can clear again (a chain), which scores more. The game ends when the well is full.",
        ["D-pad left / right: move the pair", "A: turn the pair", "D-pad down: drop faster", "A after game over: back to the title", "Start: pause menu"],
        new Dictionary<string, string>
        {
            ["Title/Title"] = "The game title (string key ui.title). The Start / Continue / Options menu under it is the scene's Menu.",
            ["Board"] = "Invisible game logic: the Board script keeps the 6 x 9 grid of gems in an array and draws it by changing tiles of the BG1 Board layer (Scene.SetTile), so the gems are tiles, not sprites.",
            ["HUD Score"] = "Text label with the score. Its Scoreboard script also writes the best score into HUD Best whenever a number changes.",
            ["HUD Next"] = "Text label over the preview of the next pair (the two gem tiles under it are drawn by Board).",
            ["HUD Best"] = "Text label with the best score, kept in the save data.",
        },
        [
            "Select Board and lower `fallFrames` to make the pairs fall faster.",
            "Change the scoring in Board.ClearLines (for example a bonus for four in a row).",
            "Use five colours instead of four: change `Random.Range(1, 5)` to `Random.Range(1, 6)` (tile 5 is the purple gem).",
            "Repaint the gem tiles in the Tilemap Editor or the tileset image (assets/tilesets/gems.png).",
        ]);

    // ---- scripts ----------------------------------------------------------------------------------------

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "Board", """
// The well is 6 x 9 cells of map layer 0, starting at tile (1, 0). A pair falls from the top: Left and Right move
// it, A turns it, Down drops it faster. When it lands, gems fall into gaps, and lines of three or more of one
// colour clear; each further clear in the same turn (a chain) scores more.
// The gems are tiles: Scene.SetTile(layer, x, y, tile) changes one 16 px cell of the map while the game runs.
public class Board : Behaviour
{
    const int Left = 1;
    const int Width = 6;
    const int Height = 9;
    public int fallFrames = 30;   // frames between steps down (Inspector field)

    int[] cells = new int[54];   // the grid: 0 empty, 1-4 a gem colour (= its tile number)
    int[] marks = new int[54];
    int[] shown = new int[54];   // what the map shows now, so a redraw only touches changed cells
    int pairX;
    int pairY;
    int turn;        // 0 second gem above, 1 right, 2 below, 3 left
    int colorA;
    int colorB;
    int nextA;
    int nextB;
    int timer;
    bool over;

    // Start runs once, when the scene has loaded the actor.
    void Start()
    {
        Game.score = 0;                 // a save field (Project Settings > Save)
        nextA = Random.Range(1, 5);     // a whole number from 1 to 4
        nextB = Random.Range(1, 5);
        Spawn();
    }

    // Update runs every frame (60 times a second).
    void Update()
    {
        if (over)
        {
            if (Input.Pressed(Key.A)) Scene.Load("Title");
            return;
        }
        // Input.Pressed is true only on the frame the button goes down.
        int dx = 0;
        if (Input.Pressed(Key.Left)) dx = -1;
        if (Input.Pressed(Key.Right)) dx = 1;
        if (dx != 0 && Fits(pairX + dx, pairY, turn))
        {
            Draw(0);
            pairX += dx;
            Draw(1);
            Sound.Play("move");
        }
        if (Input.Pressed(Key.A))
        {
            int t = (turn + 1) & 3;
            if (Fits(pairX, pairY, t))
            {
                Draw(0);
                turn = t;
                Draw(1);
                Sound.Play("move");
            }
        }
        // Input.Held stays true while the button is down: holding Down makes the timer run 6 times faster.
        timer += Input.Held(Key.Down) ? 6 : 1;
        if (timer < fallFrames) return;
        timer = 0;
        if (Fits(pairX, pairY + 1, turn))
        {
            Draw(0);
            pairY += 1;
            Draw(1);
            return;
        }
        Land();
    }

    // Offsets of the second gem for each turn.
    int OtherX(int t) => t == 1 ? 1 : t == 3 ? -1 : 0;
    int OtherY(int t) => t == 0 ? -1 : t == 2 ? 1 : 0;

    bool Free(int x, int y)
    {
        if (x < 0 || x >= Width || y >= Height) return false;
        if (y < 0) return true;
        return cells[y * Width + x] == 0;
    }

    bool Fits(int x, int y, int t) => Free(x, y) && Free(x + OtherX(t), y + OtherY(t));

    void Put(int x, int y, int color)
    {
        if (y < 0) return;
        shown[y * Width + x] = color;
        Scene.SetTile(0, Left + x, y, color);   // tile 0 is the empty well
    }

    // Draws (show = 1) or erases (show = 0) the falling pair.
    void Draw(int show)
    {
        Put(pairX, pairY, show == 1 ? colorA : 0);
        Put(pairX + OtherX(turn), pairY + OtherY(turn), show == 1 ? colorB : 0);
    }

    // The next pair becomes the falling pair; a new next pair shows in the preview at tiles (9, 6) and (9, 7).
    void Spawn()
    {
        colorA = nextA;
        colorB = nextB;
        nextA = Random.Range(1, 5);
        nextB = Random.Range(1, 5);
        pairX = 2;
        pairY = 0;
        turn = 0;
        Scene.SetTile(0, 9, 6, nextB);
        Scene.SetTile(0, 9, 7, nextA);
        if (!Fits(pairX, pairY, turn))
        {
            // The well is full: game over. The best score is kept in save slot 0.
            over = true;
            if (Game.score > Game.best) Game.best = Game.score;
            Save.Write(0);
            return;
        }
        Draw(1);
    }

    void Land()
    {
        Sound.Play("land");
        if (pairY >= 0) cells[pairY * Width + pairX] = colorA;
        int ox = pairX + OtherX(turn);
        int oy = pairY + OtherY(turn);
        if (oy >= 0) cells[oy * Width + ox] = colorB;
        int chain = 1;
        Settle();
        while (ClearLines(chain))
        {
            chain += 1;
            Settle();
        }
        Redraw();
        Spawn();
    }

    // Gems fall down into empty cells below them.
    void Settle()
    {
        for (int x = 0; x < Width; x++)
        {
            int write = Height - 1;
            for (int y = Height - 1; y >= 0; y--)
            {
                int c = cells[y * Width + x];
                if (c == 0) continue;
                cells[y * Width + x] = 0;
                cells[write * Width + x] = c;
                write -= 1;
            }
        }
    }

    // Marks every run of three or more in a row or column, clears them and scores; false when nothing cleared.
    bool ClearLines(int chain)
    {
        for (int i = 0; i < 54; i++) marks[i] = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int c = cells[y * Width + x];
                if (c == 0) continue;
                // three of one colour to the right
                if (x + 2 < Width && cells[y * Width + x + 1] == c && cells[y * Width + x + 2] == c)
                {
                    int k = x;
                    while (k < Width && cells[y * Width + k] == c) { marks[y * Width + k] = 1; k++; }
                }
                // three of one colour downwards
                if (y + 2 < Height && cells[(y + 1) * Width + x] == c && cells[(y + 2) * Width + x] == c)
                {
                    int k = y;
                    while (k < Height && cells[k * Width + x] == c) { marks[k * Width + x] = 1; k++; }
                }
            }
        }
        int cleared = 0;
        for (int i = 0; i < 54; i++)
        {
            if (marks[i] == 0) continue;
            cells[i] = 0;
            cleared += 1;
        }
        if (cleared == 0) return false;
        Game.score += cleared * 10 * chain;   // the Scoreboard on HUD Score shows the new score
        Sound.Play("clear");
        return true;
    }

    // Copies the grid to the map, changing only the cells that differ (few SetTile calls per frame).
    void Redraw()
    {
        for (int i = 0; i < 54; i++)
        {
            if (shown[i] == cells[i]) continue;
            shown[i] = cells[i];
            Scene.SetTile(0, Left + i % Width, i / Width, cells[i]);
        }
    }
}
""");
        TemplateKit.Script(p, "Scoreboard", """
// On the HUD Score text label: shows the score, and the best score in HUD Best, whenever they change. Other
// scripts only change Game.score; they do not need to know about the HUD.
public class Scoreboard : Behaviour
{
    public int digits = 5;
    int shownScore = -1;
    int shownBest = -1;

    void Update()
    {
        if (Game.score != shownScore)
        {
            shownScore = Game.score;
            // SetLabelNumber writes a number with leading zeros into this text label.
            actor.SetLabelNumber(shownScore, digits);
        }
        if (Game.best != shownBest)
        {
            shownBest = Game.best;
            // Scene.Find looks an actor up by its Hierarchy name.
            Actor best = Scene.Find("HUD Best");
            if (best != null) best.SetLabelNumber(shownBest, digits);
        }
    }
}
""");
    }
}
