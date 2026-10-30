namespace AgbStudio.Build.Content;

/// <summary>A small ARGB canvas for drawing the original placeholder art of templates and samples (D-20).</summary>
public sealed class Canvas
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public Canvas(int w, int h)
    {
        Width = w;
        Height = h;
        Pixels = new uint[w * h];
    }

    public void Set(int x, int y, uint c)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height) Pixels[y * Width + x] = c;
    }

    public uint Get(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? Pixels[y * Width + x] : 0;

    public void Rect(int x, int y, int w, int h, uint c)
    {
        for (var yy = y; yy < y + h; yy++)
            for (var xx = x; xx < x + w; xx++) Set(xx, yy, c);
    }

    public void Ellipse(double cx, double cy, double rx, double ry, uint c)
    {
        for (var y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
            for (var x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                if (dx * dx + dy * dy <= 1) Set(x, y, c);
            }
    }

    public void Line(int x0, int y0, int x1, int y1, uint c)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            Set(x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>Darkens the outline of every shape (1 px border) with colour <paramref name="c"/>.</summary>
    public void Outline(uint c)
    {
        var copy = (uint[])Pixels.Clone();
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if ((copy[y * Width + x] >> 24) != 0) continue;
                bool Near(int xx, int yy) => xx >= 0 && yy >= 0 && xx < Width && yy < Height && (copy[yy * Width + xx] >> 24) != 0 && copy[yy * Width + xx] != c;
                if (Near(x - 1, y) || Near(x + 1, y) || Near(x, y - 1) || Near(x, y + 1)) Set(x, y, c);
            }
    }

    public void Blit(Canvas src, int dx, int dy)
    {
        for (var y = 0; y < src.Height; y++)
            for (var x = 0; x < src.Width; x++)
            {
                var p = src.Pixels[y * src.Width + x];
                if ((p >> 24) != 0) Set(dx + x, dy + y, p);
            }
    }

    public void Save(string path) => Core.Png.WriteArgb(path, Pixels, Width, Height);

    public static uint Rgb(int r, int g, int b) => 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
}

/// <summary>Original pixel art used by Sky Hopper and the templates. Everything is drawn here from shapes.</summary>
public static class PixelArt
{
    public static readonly uint Ink = Canvas.Rgb(30, 26, 40);
    public static readonly uint Hero = Canvas.Rgb(255, 79, 109);
    public static readonly uint HeroLight = Canvas.Rgb(255, 150, 160);
    public static readonly uint White = Canvas.Rgb(250, 250, 245);
    public static readonly uint Slime = Canvas.Rgb(90, 200, 90);
    public static readonly uint SlimeDark = Canvas.Rgb(40, 140, 60);
    public static readonly uint Gold = Canvas.Rgb(250, 200, 40);
    public static readonly uint GoldDark = Canvas.Rgb(200, 130, 20);
    public static readonly uint Grass = Canvas.Rgb(70, 180, 70);
    public static readonly uint GrassDark = Canvas.Rgb(40, 120, 50);
    public static readonly uint Dirt = Canvas.Rgb(150, 100, 60);
    public static readonly uint DirtDark = Canvas.Rgb(110, 70, 40);
    public static readonly uint Stone = Canvas.Rgb(130, 130, 140);
    public static readonly uint StoneDark = Canvas.Rgb(90, 90, 100);
    public static readonly uint Wood = Canvas.Rgb(170, 110, 60);
    public static readonly uint Water = Canvas.Rgb(60, 130, 230);
    public static readonly uint WaterLight = Canvas.Rgb(120, 180, 250);
    public static readonly uint Sky = Canvas.Rgb(120, 190, 250);
    public static readonly uint Red = Canvas.Rgb(220, 50, 50);

    /// <summary>Hero: 8 frames of 16x16 (idle 0-1, run 2-5, jump 6, hurt 7).</summary>
    public static Canvas Hero8()
    {
        var sheet = new Canvas(16 * 8, 16);
        for (var f = 0; f < 8; f++)
        {
            var c = new Canvas(16, 16);
            var bob = f is 1 ? 1 : 0;
            var bodyColor = f == 7 ? White : Hero;
            c.Ellipse(8, 8 + bob, 5.5, 5, bodyColor);
            c.Ellipse(6.5, 6.5 + bob, 2, 1.5, HeroLight);
            // eyes
            c.Rect(9, 6 + bob, 1, 2, Ink);
            c.Rect(11, 6 + bob, 1, 2, Ink);
            // feet: run frames alternate
            var step = f is >= 2 and <= 5 ? (f % 2 == 0 ? 1 : -1) : 0;
            if (f == 6)
            {
                c.Rect(4, 13, 3, 2, Ink);
                c.Rect(9, 13, 3, 2, Ink);
            }
            else
            {
                c.Rect(4 + step, 13, 3, 2, Ink);
                c.Rect(9 - step, 13, 3, 2, Ink);
            }
            c.Outline(Ink);
            sheet.Blit(c, f * 16, 0);
        }
        return sheet;
    }

    /// <summary>Slime: 4 frames of 16x12 (squash and stretch).</summary>
    public static Canvas Slime4()
    {
        var sheet = new Canvas(16 * 4, 12);
        for (var f = 0; f < 4; f++)
        {
            var c = new Canvas(16, 12);
            var squash = new[] { 0, 1, 2, 1 }[f];
            c.Ellipse(8, 7 + squash / 2.0, 6 + squash / 2.0, 4.5 - squash / 2.0, Slime);
            c.Ellipse(6, 5.5 + squash / 2.0, 1.5, 1, White);
            c.Rect(6, 6 + squash / 2, 1, 2, Ink);
            c.Rect(10, 6 + squash / 2, 1, 2, Ink);
            c.Rect(3, 10, 10, 1, SlimeDark);
            c.Outline(Ink);
            sheet.Blit(c, f * 16, 0);
        }
        return sheet;
    }

    /// <summary>Coin: 6 frames of 8x8 spinning.</summary>
    public static Canvas Coin6()
    {
        var sheet = new Canvas(8 * 6, 8);
        double[] widths = [3.5, 2.8, 1.6, 0.8, 1.6, 2.8];
        for (var f = 0; f < 6; f++)
        {
            var c = new Canvas(8, 8);
            c.Ellipse(4, 4, widths[f], 3.5, Gold);
            if (widths[f] > 1.5) c.Rect(4, 2, 1, 4, GoldDark);
            c.Outline(GoldDark);
            sheet.Blit(c, f * 8, 0);
        }
        return sheet;
    }

    public static Canvas Heart() { var c = new Canvas(8, 8); c.Ellipse(2.5, 3, 2, 2, Red); c.Ellipse(5.5, 3, 2, 2, Red); for (var y = 3; y < 8; y++) c.Rect(y - 3 + 1, y, 7 - 2 * (y - 3), 1, Red); c.Outline(Ink); return c; }

    public static Canvas Block()
    {
        var c = new Canvas(16, 16);
        c.Rect(0, 0, 16, 16, Gold);
        c.Rect(1, 1, 14, 14, GoldDark);
        c.Rect(2, 2, 12, 12, Gold);
        c.Rect(6, 4, 4, 2, White);
        c.Rect(8, 6, 2, 3, White);
        c.Rect(7, 11, 2, 2, White);
        return c;
    }

    public static Canvas Cloud() { var c = new Canvas(16, 8); c.Ellipse(5, 5, 4, 3, White); c.Ellipse(10, 4, 5, 3.5, White); c.Rect(2, 6, 12, 2, White); return c; }

    public static Canvas Flag()
    {
        var c = new Canvas(16, 32);
        c.Rect(3, 2, 2, 30, StoneDark);
        for (var y = 0; y < 8; y++) c.Rect(5, 3 + y, 9 - y, 1, Red);
        return c;
    }

    // Forest tileset metatile indices.
    public const int TEmpty = 0, TGrassTop = 1, TDirt = 2, TPlatform = 3, TLadder = 4, TWater = 5, TSpikes = 6, TStone = 7, TGrassLeft = 8, TGrassRight = 9;

    /// <summary>Forest tileset: 16x16 editor tiles in one row.</summary>
    public static Canvas ForestTiles()
    {
        var c = new Canvas(16 * 10, 16);
        void Tile(int i, Action<int> draw) => draw(i * 16);
        Tile(TGrassTop, x => { c.Rect(x, 0, 16, 16, Dirt); c.Rect(x, 0, 16, 5, Grass); c.Rect(x, 5, 16, 1, GrassDark); for (var k = 0; k < 4; k++) c.Rect(x + 2 + k * 4, 9 + k % 2 * 3, 2, 1, DirtDark); });
        Tile(TDirt, x => { c.Rect(x, 0, 16, 16, Dirt); for (var k = 0; k < 5; k++) c.Rect(x + (k * 7) % 14 + 1, (k * 5) % 14 + 1, 2, 1, DirtDark); });
        Tile(TPlatform, x => { c.Rect(x, 0, 16, 5, Wood); c.Rect(x, 5, 16, 1, DirtDark); c.Rect(x + 3, 6, 2, 3, DirtDark); c.Rect(x + 11, 6, 2, 3, DirtDark); });
        Tile(TLadder, x => { c.Rect(x + 3, 0, 2, 16, Wood); c.Rect(x + 11, 0, 2, 16, Wood); for (var k = 0; k < 4; k++) c.Rect(x + 3, 2 + k * 4, 10, 1, Wood); });
        Tile(TWater, x => { c.Rect(x, 0, 16, 16, Water); c.Rect(x + 1, 3, 5, 1, WaterLight); c.Rect(x + 9, 9, 5, 1, WaterLight); });
        Tile(TSpikes, x => { for (var k = 0; k < 4; k++) for (var y = 0; y < 8; y++) c.Rect(x + k * 4 + y / 4, 8 + y, Math.Max(1, 4 - y / 2), 1, Stone); c.Rect(x, 15, 16, 1, StoneDark); });
        Tile(TStone, x => { c.Rect(x, 0, 16, 16, Stone); c.Rect(x, 7, 16, 1, StoneDark); c.Rect(x + 7, 0, 1, 7, StoneDark); c.Rect(x + 3, 8, 1, 8, StoneDark); });
        Tile(TGrassLeft, x => { c.Rect(x, 0, 16, 16, Dirt); c.Rect(x, 0, 16, 5, Grass); c.Rect(x, 0, 3, 16, Grass); c.Rect(x, 5, 16, 1, GrassDark); });
        Tile(TGrassRight, x => { c.Rect(x, 0, 16, 16, Dirt); c.Rect(x, 0, 16, 5, Grass); c.Rect(x + 13, 0, 3, 16, Grass); c.Rect(x, 5, 16, 1, GrassDark); });
        return c;
    }
}
