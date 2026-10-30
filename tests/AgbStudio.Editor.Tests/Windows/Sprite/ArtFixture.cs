using AgbStudio.Build.Content;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Palette;
using SkiaSharp;

namespace AgbStudio.Editor.Tests.Windows.Sprite;

/// <summary>
/// The Sky Hopper fixture project (AgbStudio.Build SkyHopper) shaped like the Figma Sprite Editor and Palette Manager:
/// the six sprites of the Sprites list, the hero art and palette banks read from the Figma frames (pixel art and
/// palette colours are data), OBJ 0 · Hero / OBJ 1 · Enemies / OBJ 2 · Items, BG 0-2 and the palette effects.
/// </summary>
public static class ArtFixture
{
    public static readonly string[] Sprites = ["hero", "slime", "coin", "block", "heart", "cloud"];

    static SKBitmap Figma(string id) => SKBitmap.Decode(Path.Combine(Repo.FigmaEvidence, id + ".png"));

    static uint Argb(SKColor c) => 0xFF000000u | ((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue;

    static bool Near(SKColor a, int r, int g, int b) => Math.Abs(a.Red - r) <= 6 && Math.Abs(a.Green - g) <= 6 && Math.Abs(a.Blue - b) <= 6;

    /// <summary>The 16x16 hero frame of the Figma canvas (26 px per pixel at 482, 176; checker = transparent).</summary>
    public static uint[] HeroFrame()
    {
        using var bmp = Figma("T-P08");
        var px = new uint[16 * 16];
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var c = bmp.GetPixel(482 + 26 * x + 8, 176 + 26 * y + 8);
                px[y * 16 + x] = Near(c, 0x2A, 0x2C, 0x31) || Near(c, 0x23, 0x25, 0x29) ? 0 : Argb(c);
            }
        return px;
    }

    /// <summary>Colours of one row of a Figma palette RAM grid (15 px cells, 17 px apart) until the first empty cell.</summary>
    static List<uint> RamRow(SKBitmap bmp, int x0, int y0, int row)
    {
        var list = new List<uint>();
        for (var i = 0; i < 16; i++)
        {
            var c = bmp.GetPixel(x0 + 17 * i + 7, y0 + 17 * row + 7);
            if (Near(c, 0x26, 0x28, 0x2D)) break;
            list.Add(Argb(c));
        }
        return list;
    }

    static uint[] Recolour(uint[] px, IReadOnlyList<uint> palette)
    {
        var bank = new PaletteBank { Colors = [0, .. palette] };
        return px.Select(p => Colour15.IsTransparent(p) ? 0u : bank.Colors[ArtLibrary.IndexOf(bank, p)]).ToArray();
    }

    /// <summary>Creates the project in the fixture folder, opens it and returns the art library.</summary>
    public static ArtLibrary Open(Fixture fx)
    {
        var root = Path.Combine(fx.Folder, "SkyHopper");
        var p = SkyHopper.Create(root);
        var sprites = Path.Combine(p.AssetsDir, "sprites");
        foreach (var f in Directory.GetFiles(sprites, "*.png"))
            if (!Sprites.Contains(Path.GetFileNameWithoutExtension(f)))
            {
                File.Delete(f);
                File.Delete(f + ".meta.json");
            }

        using var pm = Figma("T-P10");
        var hero = RamRow(pm, 570, 234, 0);
        var enemies = RamRow(pm, 570, 234, 1);
        var items = RamRow(pm, 570, 234, 2);
        // the hero bank as the swatch row of the Palette Manager shows it (40 px swatches, 48 apart, from 276)
        hero = Enumerable.Range(1, 9).Select(i => Argb(pm.GetPixel(276 + 48 * i + 30, 157 + 12))).ToList();
        var bg = Enumerable.Range(0, 3).Select(r => RamRow(pm, 276, 234, r)).ToList();
        // BG 1 · Ground as the Palette Effect popover shows it: 12 colours from index 0, the water at 6-9
        List<uint> ground;
        using (var pe = Figma("T-O10")) ground = Enumerable.Range(0, 12).Select(i => Argb(pe.GetPixel(21 + 26 * i + 12, 183))).ToList();

        // hero: 8 frames of the Figma canvas art; idle / run / jump / hurt as in the Animation panel
        var frame = HeroFrame();
        var sheet = new uint[16 * 8 * 16];
        for (var f = 0; f < 8; f++)
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++) sheet[y * 128 + f * 16 + x] = frame[y * 16 + x];
        Png.WriteArgb(Path.Combine(sprites, "hero.png"), Recolour(sheet, hero), 128, 16);
        var heroMeta = GameProject.Load<SpriteMeta>(Path.Combine(sprites, "hero.png.meta.json"));
        heroMeta.Animations =
        [
            new AnimationData { Name = "idle", Frames = [0, 1, 2, 3, 4, 5, 6, 7], Ticks = 6, Loop = true },
            new AnimationData { Name = "run", Frames = [0, 1, 2, 3, 4, 5], Ticks = 6, Loop = true },
            new AnimationData { Name = "jump", Frames = [5, 6, 7], Ticks = 6, Loop = false },
            new AnimationData { Name = "hurt", Frames = [6, 7], Ticks = 6, Loop = false },
        ];
        GameProject.Save(Path.Combine(sprites, "hero.png.meta.json"), heroMeta);

        void Fit(string name, IReadOnlyList<uint> palette, string owner)
        {
            var path = Path.Combine(sprites, name + ".png");
            var img = Png.Read(path);
            Png.WriteArgb(path, Recolour(img.Pixels, palette), img.Width, img.Height);
            var meta = GameProject.Load<SpriteMeta>(path + ".meta.json");
            meta.Palette = owner;
            GameProject.Save(path + ".meta.json", meta);
        }
        Fit("heart", hero, "hero");
        Fit("slime", enemies, "");
        Fit("coin", items, "");
        Fit("block", items, "coin");
        Fit("cloud", items, "coin");

        static List<string> Hex(IEnumerable<uint> c) => ["none", .. c.Select(Colour15.Hex24)];
        var file = new PaletteLibraryFile
        {
            Banks =
            [
                new() { Kind = BankKind.Bg, Slot = 0, Name = "Sky & hills", Colors = Hex(bg[0]), Assets = ["assets/images/sky_bg.png", "assets/images/hills_bg.png"] },
                new() { Kind = BankKind.Bg, Slot = 1, Name = "Ground", Colors = ground.Select(Colour15.Hex24).ToList(), LockTransparent = false, Assets = ["assets/tilesets/forest_tiles.png", "assets/tilesets/cave_tiles.png"] },
                new() { Kind = BankKind.Bg, Slot = 2, Name = "UI & text", Colors = Hex(bg[2]) },
                new() { Kind = BankKind.Obj, Slot = 0, Name = "Hero", Colors = Hex(hero), Assets = ["assets/sprites/hero.png", "assets/sprites/heart.png"] },
                new() { Kind = BankKind.Obj, Slot = 1, Name = "Enemies", Colors = Hex(enemies), Assets = ["assets/sprites/slime.png"] },
                new() { Kind = BankKind.Obj, Slot = 2, Name = "Items", Colors = Hex(items), Assets = ["assets/sprites/coin.png", "assets/sprites/block.png", "assets/sprites/cloud.png"] },
            ],
            Effects = new PaletteEffectsData(),
        };
        GameProject.Save(Path.Combine(p.AssetsDir, "palettes", "palettes.json"), file);

        // the sprite previews sit on the scene backdrop (Figma: 1x / 2x / 4x boxes)
        using (var se = Figma("T-P08"))
        {
            var scenePath = Path.Combine(p.ScenesDir, p.Settings.LastScene + ".scene");
            var scene = GameProject.Load<SceneFile>(scenePath);
            scene.Backdrop = Colour15.Hex24(Argb(se.GetPixel(940, 200)));
            GameProject.Save(scenePath, scene);
        }

        fx.Services.Project.Open(root);
        return ArtLibrary.For(fx.Services);
    }

    /// <summary>The Scene document tab every page shows first.</summary>
    public static void AddSceneDocument(Fixture fx)
    {
        var p = fx.Services.Project.Current!;
        fx.Services.Documents.Add(new EditorDocument { Name = "Scene", Glyph = "map", Path = Path.Combine(p.ScenesDir, p.Settings.LastScene + ".scene") });
    }

    public static string SpritePath(ArtLibrary lib, string name) => Path.Combine(lib.Project.AssetsDir, "sprites", name + ".png");
}
