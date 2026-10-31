using AgbStudio.Build.Content.Templates;
using AgbStudio.Core;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content;

/// <summary>
/// The sample projects of Launcher · Learn (P15.11) besides the Sky Hopper demo: each is a template project with its
/// own look (the backgrounds and tiles recoloured for its setting) and, for Dusk Story, its own story (D-20: all
/// content is made for AGB Studio).
/// </summary>
public static class Samples
{
    /// <summary>A turn-based RPG in a cave: the RPG template with dark rock colours.</summary>
    public static GameProject CaveQuest(string dir)
    {
        var p = RpgTemplate.Create(dir, "Cave Quest");
        Recolor(p, Cave);
        return p;
    }

    /// <summary>A Mode 7 race on snow and ice: the Racing template with winter colours.</summary>
    public static GameProject SnowRun(string dir)
    {
        var p = RacingTemplate.Create(dir, "Snow Run");
        Recolor(p, Snow);
        return p;
    }

    /// <summary>A visual novel at a lighthouse at dusk: the Visual Novel template with a new story and evening colours.</summary>
    public static GameProject DuskStory(string dir)
    {
        var p = VisualNovelTemplate.Create(dir, "Dusk Story");
        Recolor(p, Dusk);
        Rewrite(p, new()
        {
            ["vn.1"] = ("The lighthouse went dark tonight.", "คืนนี้ประภาคารดับไปแล้ว"),
            ["vn.2"] = ("Someone has to climb up and light it again.", "ต้องมีใครสักคนขึ้นไปจุดไฟอีกครั้ง"),
            ["vn.3"] = ("All those stairs, in the dark...", "บันไดตั้งเยอะ แถมยังมืดอีก..."),
            ["vn.choice.cave"] = ("Climb the tower", "ปีนขึ้นหอคอย"),
            ["vn.choice.wait"] = ("Ring the bell", "ตีระฆังเตือน"),
            ["vn.cave.1"] = ("One step at a time. I can do it!", "ทีละขั้น ข้าทำได้แน่!"),
            ["vn.cave.2"] = ("Take my lantern. It never goes out.", "เอาตะเกียงของข้าไปสิ มันไม่เคยดับ"),
            ["vn.wait.1"] = ("The bell will warn the boats instead.", "เสียงระฆังจะเตือนเรือแทนก็ได้"),
            ["vn.wait.2"] = ("Clever. The sea is safe tonight.", "ฉลาดมาก คืนนี้ทะเลปลอดภัยแล้ว"),
            ["vn.end"] = ("The end... for now.", "จบแล้ว... สำหรับตอนนี้"),
        });
        return p;
    }

    // ---- looks -----------------------------------------------------------------------------------------

    static (int R, int G, int B) Rgb(uint c) => ((int)(c >> 16 & 255), (int)(c >> 8 & 255), (int)(c & 255));
    static uint Pack(uint a, double r, double g, double b) =>
        a << 24 | (uint)Math.Clamp(r, 0, 255) << 16 | (uint)Math.Clamp(g, 0, 255) << 8 | (uint)Math.Clamp(b, 0, 255);

    /// <summary>Greens become snow, greys become ice, skies turn pale; other colours cool down.</summary>
    static uint Snow(uint c)
    {
        var (r, g, b) = Rgb(c);
        var l = (r * 3 + g * 6 + b) / 10.0;
        if (g > r + 16 && g > b + 16) return Pack(c >> 24, 200 + l / 5, 214 + l / 6, 240);              // grass -> snow
        if (Math.Abs(r - g) < 16 && Math.Abs(g - b) < 24) return Pack(c >> 24, l * 0.75, l * 0.85, l * 1.1 + 20);  // grey -> ice
        if (b > r && b > g) return Pack(c >> 24, 168 + r / 4.0, 192 + g / 5.0, 232 + b / 10.0);        // sky -> pale
        return Pack(c >> 24, r * 0.85, g * 0.9, b * 0.9 + 24);
    }

    /// <summary>Darker, with a purple-brown rock tint.</summary>
    static uint Cave(uint c)
    {
        var (r, g, b) = Rgb(c);
        var l = (r * 3 + g * 6 + b) / 10.0;
        return Pack(c >> 24, l * 0.55 + 28, l * 0.45 + 16, l * 0.55 + 36);
    }

    /// <summary>Evening light: warm highlights, violet shadows.</summary>
    static uint Dusk(uint c)
    {
        var (r, g, b) = Rgb(c);
        var l = (r * 3 + g * 6 + b) / 10.0;
        return l > 150 ? Pack(c >> 24, r * 0.95 + 12, g * 0.75, b * 0.7 + 20) : Pack(c >> 24, r * 0.7 + 20, g * 0.55 + 8, b * 0.8 + 40);
    }

    /// <summary>Recolours the tilesets and background images (sprites keep their colours).</summary>
    static void Recolor(GameProject p, Func<uint, uint> map)
    {
        foreach (var file in p.TilesetFiles().Concat(p.ImageFiles()).Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
        {
            var img = Png.Read(file);
            var px = img.Pixels.Select(c => c >> 24 == 0 ? c : map(c)).ToArray();
            Png.WriteArgb(file, px, img.Width, img.Height);
        }
    }

    static void Rewrite(GameProject p, Dictionary<string, (string En, string Th)> lines)
    {
        foreach (var (lang, pick) in new (string, Func<(string En, string Th), string>)[] { ("en", l => l.En), ("th", l => l.Th) })
        {
            var path = Path.Combine(p.TextDir, $"strings.{lang}.json");
            var t = GameProject.Load<StringTable>(path);
            foreach (var (k, v) in lines) t.Strings[k] = pick(v);
            GameProject.Save(path, t);
        }
    }
}
