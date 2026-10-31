using AgbStudio.Build.Pipeline;

namespace AgbStudio.Build.Content;

/// <summary>
/// The art of the Sky Hopper fixture as the Figma frames draw it (T-P01 Forest_01, T-P08 hero.png, the Asset Card art
/// in editor/AgbStudio.Editor/Assets/Art: art-sprite-*, art-scene-day, art-tileset-forest). The pictures are the
/// project's own design; here they are pixel grids, one letter per colour. Templates and the mini platformer keep the
/// placeholder art of <see cref="PixelArt"/>.
/// </summary>
public static class SkyHopperArt
{
    // Figma colours (art-sprite-*, art-scene-day).
    public static readonly uint Ink = Canvas.Rgb(0x1B, 0x16, 0x30);
    public static readonly uint Yellow = Canvas.Rgb(0xFF, 0xD8, 0x4A);
    public static readonly uint White = Canvas.Rgb(0xFF, 0xFF, 0xFF);
    public static readonly uint Cheek = Canvas.Rgb(0xFF, 0x9F, 0xB5);
    public static readonly uint Red = Canvas.Rgb(0xFF, 0x4F, 0x6D);
    public static readonly uint Purple = Canvas.Rgb(0x8A, 0x4D, 0xFF);
    public static readonly uint SkyTop = Canvas.Rgb(0x58, 0xB4, 0xFF);
    public static readonly uint SkyMid = Canvas.Rgb(0x7C, 0xC5, 0xFF);
    public static readonly uint SkyLow = Canvas.Rgb(0xA6, 0xDB, 0xFF);
    public static readonly uint CloudShade = Canvas.Rgb(0xD6, 0xEE, 0xFF);
    public static readonly uint HillFar = Canvas.Rgb(0x86, 0xCF, 0xA6);
    public static readonly uint HillNear = Canvas.Rgb(0x3F, 0x8F, 0x74);
    public static readonly uint GrassLight = Canvas.Rgb(0xA6, 0xF0, 0x7A);
    public static readonly uint Grass = Canvas.Rgb(0x4C, 0xC3, 0x5A);
    public static readonly uint Dirt = Canvas.Rgb(0xB4, 0x70, 0x3F);
    public static readonly uint DirtEdge = Canvas.Rgb(0x9A, 0x5C, 0x33);
    public static readonly uint DirtDark = Canvas.Rgb(0x8A, 0x4F, 0x2A);
    public static readonly uint Brick = Canvas.Rgb(0xD9, 0xB7, 0x7E);
    public static readonly uint BrickLight = Canvas.Rgb(0xF3, 0xDC, 0xAE);
    public static readonly uint Mortar = Canvas.Rgb(0x9B, 0x7A, 0x46);
    public static readonly uint Gold = Canvas.Rgb(0xF7, 0xB7, 0x33);
    public static readonly uint GoldDark = Canvas.Rgb(0xA8, 0x6A, 0x12);
    public static readonly uint Pole = Canvas.Rgb(0xE9, 0xED, 0xF2);
    public static readonly uint Knob = Canvas.Rgb(0xFF, 0xD2, 0x3F);

    // Forest tileset metatiles (forest_tiles.png, left to right in the order of the Figma tileset art; 0 is empty), then
    // the two half-tile ends of a brick platform.
    public const int TGrass = 1, TDirt = 2, TBricks = 3, TBush = 4, TLeaves = 5, TStone = 6, TWater = 7, TSpikes = 8, TBricksLeft = 9, TBricksRight = 10;

    static Canvas Grid(string[] rows, IReadOnlyDictionary<char, uint> colours)
    {
        var c = new Canvas(rows[0].Length, rows.Length);
        for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++)
                if (colours.TryGetValue(rows[y][x], out var v)) c.Set(x, y, v);
        return c;
    }

    // ---- sprites --------------------------------------------------------------------------------------------

    static readonly string[] HeroRows =
    [
        "................",
        ".....aaaaaa.....",
        "....abbbbbba....",
        "...abbbbbbbba...",
        "..abbbbbbbbbba..",
        "..abbcabbcabba..",
        "..abbcabbcabba..",
        "..abbbbbbbbbba..",
        "..adbbbbbbbbda..",
        "..aeeeeeeeeeea..",
        "..aeeeeeeeeeeae.",
        "...abbbbbbbba.e.",
        "...abbbbbbbba...",
        "....aaaaaaaa....",
        "....affa.affa...",
        "....aaaa.aaaa...",
    ];

    /// <summary>The yellow hero (T-P08): 8 frames of 16x16. Even frames are the Figma drawing; odd frames breathe
    /// (the body one pixel lower, the feet in place). idle plays all 8; run, jump and hurt reuse them.</summary>
    public static Canvas Hero8()
    {
        var map = new Dictionary<char, uint> { ['a'] = Ink, ['b'] = Yellow, ['c'] = White, ['d'] = Cheek, ['e'] = Red, ['f'] = Purple };
        var sheet = new Canvas(16 * 8, 16);
        for (var f = 0; f < 8; f++)
        {
            var rows = (string[])HeroRows.Clone();
            if (f % 2 == 1)
            {
                // breathe: rows 1-12 move down one pixel; the feet (13-15) stay
                for (var y = 12; y >= 1; y--) rows[y] = y == 1 ? "................" : rows[y - 1];
                rows[12] = HeroRows[11];
            }
            sheet.Blit(Grid(rows, map), f * 16, 0);
        }
        return sheet;
    }

    static readonly string[] SlimeRows =
    [
        "......aaaa......",
        "....aabbbbaa....",
        "...abbccbbbba...",
        "..abbcbbbbbbba..",
        "..abbbbbbbbbba..",
        ".abbbdabbdabbba.",
        ".abbbdabbdabbba.",
        ".abbbbbbbbbbbba.",
        "abbbbbeeeebbbbba",
        "abbbbbbbbbbbbbba",
        "aeeeeeeeeeeeeeea",
        ".aaaaaaaaaaaaaa.",
    ];

    /// <summary>The purple slime: 4 frames of 16x12 (the Figma drawing, then squashed by one and two rows).</summary>
    public static Canvas Slime4()
    {
        var map = new Dictionary<char, uint>
        {
            ['a'] = Canvas.Rgb(0x2A, 0x12, 0x40), ['b'] = Canvas.Rgb(0xB5, 0x6C, 0xFF), ['c'] = Canvas.Rgb(0xE6, 0xC9, 0xFF),
            ['d'] = White, ['e'] = Canvas.Rgb(0x7D, 0x3F, 0xC4),
        };
        var sheet = new Canvas(16 * 4, 12);
        int[] squash = [0, 1, 2, 1];
        for (var f = 0; f < 4; f++)
        {
            // squash: drop rows under the top so the body sits lower; the bottom rows stay on the ground
            var rows = SlimeRows.Where((_, y) => y < 1 || y > squash[f]).ToList();
            while (rows.Count < 12) rows.Insert(0, new string('.', 16));
            sheet.Blit(Grid([.. rows], map), f * 16, 0);
        }
        return sheet;
    }

    static readonly string[] CoinRows =
    [
        "..aaaa..",
        ".abbbba.",
        "abcbbbda",
        "abcbbbda",
        "abcbbbda",
        "abbbbbda",
        ".adddda.",
        "..aaaa..",
    ];

    /// <summary>The coin: 6 frames of 8x8 spinning (the Figma drawing narrowed to 6, 4, 2, 4 and 6 pixels).</summary>
    public static Canvas Coin6()
    {
        var map = new Dictionary<char, uint>
        {
            ['a'] = Canvas.Rgb(0x5A, 0x3A, 0x00), ['b'] = Canvas.Rgb(0xFF, 0xD2, 0x3F), ['c'] = Canvas.Rgb(0xFF, 0xF6, 0xC2), ['d'] = Canvas.Rgb(0xE0, 0x96, 0x1A),
        };
        var full = Grid(CoinRows, map);
        var sheet = new Canvas(8 * 6, 8);
        int[] widths = [8, 6, 4, 2, 4, 6];
        for (var f = 0; f < 6; f++)
        {
            var w = widths[f];
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < w; x++)
                    sheet.Set(f * 8 + (8 - w) / 2 + x, y, full.Get(x * 8 / w, y));
        }
        return sheet;
    }

    /// <summary>The ? block (16x16).</summary>
    public static Canvas Block() => Grid(
    [
        "aaaaaaaaaaaaaaaa",
        "abbbbbbbbbbbbbba",
        "abcbbbbbbbbbbcba",
        "abbbbbddddbbbbba",
        "abbbbddbbddbbbba",
        "abbbbddbbddbbbba",
        "abbbbbbbddbbbbba",
        "abbbbbbddbbbbbba",
        "abbbbbbddbbbbbba",
        "abbbbbbbbbbbbbba",
        "abbbbbbddbbbbbba",
        "abbbbbbddbbbbbba",
        "abcbbbbbbbbbbcba",
        "abbbbbbbbbbbbbba",
        "aaaaaaaaaaaaaaaa",
        "cccccccccccccccc",
    ], new Dictionary<char, uint> { ['a'] = Canvas.Rgb(0x3B, 0x23, 0x00), ['b'] = Gold, ['c'] = GoldDark, ['d'] = Canvas.Rgb(0xFF, 0xF3, 0xC4) });

    /// <summary>The HUD heart (7x6 in an 8x8 frame, drawn from the top-left corner).</summary>
    public static Canvas Heart()
    {
        var c = new Canvas(8, 8);
        c.Blit(Grid([".aa.aa.", "abaaaaa", "aaaaaaa", ".aaaaa.", "..aaa..", "...a..."],
            new Dictionary<char, uint> { ['a'] = Red, ['b'] = Canvas.Rgb(0xFF, 0xB3, 0xC1) }), 0, 0);
        return c;
    }

    static readonly string[] CloudRows = ["....aaaa......", "..aaaaaaaa.aa.", ".aaaaaaaaaaaaa", "aaaaaaaaaaaaaa", "bbbbbbbbbbbbbb"];
    static readonly Dictionary<char, uint> CloudColours = new() { ['a'] = White, ['b'] = CloudShade };

    /// <summary>The cloud sprite (14x5 in a 16x8 frame).</summary>
    public static Canvas Cloud()
    {
        var c = new Canvas(16, 8);
        c.Blit(Grid(CloudRows, CloudColours), 1, 3);
        return c;
    }

    /// <summary>The goal flag of Forest_01: a 16x64 frame, the knob 50 pixels above the bottom, the pole and the pennant.</summary>
    public static Canvas Flag()
    {
        var c = new Canvas(16, 64);
        c.Rect(2, 14, 4, 4, Knob);
        c.Rect(3, 18, 2, 46, Pole);
        for (var y = 0; y < 6; y++) c.Rect(5, 19 + y, 10 - y, 1, Red);
        return c;
    }

    // ---- backgrounds ----------------------------------------------------------------------------------------

    /// <summary>Where row 0 of the screen falls in a wrapped 256x256 image layer when the camera stands at
    /// (<paramref name="camX"/>, <paramref name="camY"/>) and the layer scrolls at <paramref name="parallax"/>.</summary>
    static (int X, int Y) Scroll(int camX, int camY, double parallax) => ((camX * Cpp.Parallax(parallax)) >> 8, (camY * Cpp.Parallax(parallax)) >> 8);

    /// <summary>BG0 "Sky" of Forest_01: three bands of blue and two clouds, placed so the start camera
    /// (<paramref name="camX"/>, <paramref name="camY"/>) shows them where art-scene-day has them.</summary>
    public static Canvas Sky(int camX, int camY, double parallax)
    {
        var (sx, sy) = Scroll(camX, camY, parallax);
        var c = new Canvas(256, 256);
        for (var y = 0; y < 256; y++)
        {
            var v = ((y - sy) % 256 + 256) % 256;   // screen row when the layer starts at sy
            c.Rect(0, y, 256, 1, v < 52 || v >= 224 ? SkyTop : v < 82 ? SkyMid : SkyLow);
        }
        var cloud = Grid(CloudRows, CloudColours);
        foreach (var (x, y) in new[] { (18, 18), (150, 36) })
            for (var yy = 0; yy < cloud.Height * 2; yy++)
                for (var xx = 0; xx < cloud.Width * 2; xx++)
                    if ((cloud.Get(xx / 2, yy / 2) >> 24) != 0) c.Set((x + sx + xx) & 255, (y + sy + yy) & 255, cloud.Get(xx / 2, yy / 2));
        return c;
    }

    // Hill tops of art-scene-day, one value per 8 pixels across the 240 pixel screen, then two more to close the wrap.
    static readonly int[] FarHills = [102, 105, 105, 103, 102, 103, 107, 110, 108, 103, 96, 96, 96, 96, 96, 96, 96, 88, 87, 90, 96, 103, 107, 107, 104, 102, 103, 105, 108, 107, 104, 103];
    static readonly int[] NearHills = [119, 121, 118, 112, 107, 107, 110, 111, 111, 110, 112, 117, 122, 122, 119, 115, 114, 115, 114, 110, 106, 106, 110, 115, 117, 116, 116, 118, 121, 121, 120, 119];

    /// <summary>BG1 "Hills" of Forest_01: light far hills, dark near hills, transparent above; placed for the start camera.</summary>
    public static Canvas Hills(int camX, int camY, double parallax)
    {
        var (sx, sy) = Scroll(camX, camY, parallax);
        var c = new Canvas(256, 256);
        for (var x = 0; x < 256; x++)
        {
            var col = (x - sx + 256) % 256 / 8;
            // down to the bottom of the image, not round the wrap (the rows above stay sky when the camera climbs)
            for (var v = FarHills[col]; v + sy < 256; v++)
                c.Set(x, v + sy, v < NearHills[col] ? HillFar : HillNear);
        }
        return c;
    }

    // ---- forest tileset ---------------------------------------------------------------------------------------

    static readonly Dictionary<char, uint> GroundColours = new()
    {
        ['d'] = GrassLight, ['e'] = Grass, ['f'] = Dirt, ['g'] = DirtEdge, ['h'] = DirtDark,
        ['b'] = BrickLight, ['c'] = Brick, ['m'] = Mortar,
    };

    static readonly string[] GrassTile =
    [
        "dddddddddddddddd", "eeeeeeeeeeeeeeee", "eeeeeeeeeeeeeeee", "eeeeeeeeeeeeeeee",
        "fffeefffffeeefff", "gfffffffffeeefff", "gfffffffffffffff", "gfffffffffffffff",
        "gfffffffffffffff", "gfffffffffffffff", "gfffffffffffffff", "gfffffffffffffff",
        "gfffhhffffffffff", "gfffhhffffffffff", "gfffffffffffffff", "gfffffffffffffff",
    ];

    static readonly string[] DirtTile =
    [
        "gfffffffffffffff", "gfffffffffffffff", "gfffffffffffffff", "gffffffffffhhfff",
        "gffffffffffhhfff", "gfffffffffffffff", "gfffffffffffffff", "gfffffffffffffff",
        "gfffffffffffffff", "gfffffffffffffff", "gfffffhhffffffff", "gfffffhhffffffff",
        "gfffffffffffffff", "gfffffffffffffff", "gfffffffffffffff", "gfffffffffffffff",
    ];

    // The brick platform of art-scene-day is 12 pixels tall: the tile leaves its top 4 rows empty.
    static readonly string[] BrickTile =
    [
        "................", "................", "................", "................",
        "bbbbbbbbbbbbbbbb", "mccccccccccccccc", "mccccccccccccccc", "mccccccccccccccc",
        "mccccccccccccccc", "mccccccccccccccc", "mmmmmmmmmmmmmmmm", "ccccccccmccccccc",
        "ccccccccmccccccc", "ccccccccmccccccc", "ccccccccmccccccc", "mmmmmmmmmmmmmmmm",
    ];

    /// <summary>The ends of a brick platform that starts and stops half a tile into a cell (art-scene-day draws its bricks
    /// from x 88 to 136): the left end keeps the right half of the brick tile, the right end the left half, so the mortar
    /// joints line up with the full tiles between them and no joint is drawn at the platform's edges.</summary>
    static string[] BrickEnd(bool left) =>
    [
        .. BrickTile.Select((row, y) =>
        {
            var half = left ? "........" + row[8..] : row[..8] + "........";
            // the lower brick row has a joint at x 8 of every tile: at the left edge of the platform it is brick
            return left && y is >= 11 and <= 14 ? half[..8] + "c" + half[9..] : half;
        }),
    ];

    /// <summary>forest_tiles.png: grass top, dirt, bricks, bush, leaves, stone, water and spikes (16x16, after the empty
    /// tile), then the left and right ends of a brick platform.</summary>
    public static Canvas ForestTiles()
    {
        var c = new Canvas(16 * 11, 16);
        c.Blit(Grid(GrassTile, GroundColours), TGrass * 16, 0);
        c.Blit(Grid(DirtTile, GroundColours), TDirt * 16, 0);
        c.Blit(Grid(BrickTile, GroundColours), TBricks * 16, 0);
        c.Blit(Grid(BrickEnd(left: true), GroundColours), TBricksLeft * 16, 0);
        c.Blit(Grid(BrickEnd(left: false), GroundColours), TBricksRight * 16, 0);
        // the other tiles reuse those colours and Figma's, 15 in all: the tileset fits one BG palette (T-P03 "BG palettes 3 / 16")
        int x;
        x = TBush * 16; c.Rect(x, 0, 16, 16, HillNear); c.Rect(x + 4, 6, 2, 2, HillFar); c.Rect(x + 10, 11, 2, 2, HillFar);
        x = TLeaves * 16; c.Rect(x, 0, 16, 16, HillFar); c.Rect(x + 3, 9, 2, 2, HillNear); c.Rect(x + 11, 4, 2, 2, HillNear);
        x = TStone * 16; c.Rect(x, 0, 16, 16, GoldDark); c.Rect(x + 1, 1, 14, 14, Gold); c.Rect(x + 2, 2, 3, 1, BrickLight);
        x = TWater * 16; c.Rect(x, 0, 16, 16, SkyMid); c.Rect(x + 1, 3, 6, 1, White); c.Rect(x + 9, 9, 5, 1, White);
        x = TSpikes * 16; c.Rect(x, 8, 16, 8, Red);
        for (var k = 0; k < 4; k++)
            for (var y = 0; y < 8; y++)
            {
                var w = 1 + y / 2;   // 1, 1, 2, 2, 3, 3, 4, 4: four spikes pointing up
                c.Rect(x + k * 4 + (4 - w) / 2, y, w, 1, White);
            }
        return c;
    }
}
