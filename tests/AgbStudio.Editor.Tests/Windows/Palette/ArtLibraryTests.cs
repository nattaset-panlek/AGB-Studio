using AgbStudio.Build.Content;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Sprite;
using AgbStudio.Editor.Windows.Palette;

namespace AgbStudio.Editor.Tests.Windows.Palette;

/// <summary>Unit tests of the palette model behind the Palette Manager and the Sprite Editor (P11.33-P11.36, P11.23).</summary>
public class ArtLibraryTests : IDisposable
{
    readonly Fixture _fx = new(withApp: false);
    readonly ArtLibrary _lib;
    readonly UndoService _undo;

    public ArtLibraryTests()
    {
        _lib = ArtFixture.Open(_fx);
        _undo = _fx.Services.Undo;
    }

    public void Dispose()
    {
        _fx.Dispose();
        GC.SuppressFinalize(this);
    }

    PaletteBank Hero => _lib.Find(BankKind.Obj, 0)!;
    EditableImage Image(string name) => _lib.Image(ArtFixture.SpritePath(_lib, name));

    [Fact]
    public void Banks_LoadedWithUsedByLists()
    {
        Assert.Equal(["BG 0 · Sky & hills", "BG 1 · Ground", "BG 2 · UI & text"], _lib.Of(BankKind.Bg).Select(b => b.Label));
        Assert.Equal(["OBJ 0 · Hero", "OBJ 1 · Enemies", "OBJ 2 · Items"], _lib.Of(BankKind.Obj).Select(b => b.Label));
        Assert.Equal("hero.png, heart.png", ArtLibrary.UsedBy(Hero));
        Assert.Equal("9 / 16", Hero.UsedText);
        Assert.Equal(Colour15.Transparent, Hero.Colors[0]);
        Assert.Same(Hero, _lib.BankOf(ArtFixture.SpritePath(_lib, "heart")));
    }

    [Fact]
    public void MergeNearDuplicates_KeepsTheFirstColourAndMovesPixels()
    {
        var bank = Hero;
        var yellow = Colour15.Of(bank.Colors[2]);
        var near = Colour15.Make(Colour15.R(yellow), Colour15.G(yellow) - 1, Colour15.B(yellow));
        Assert.True(Gba.Distance(yellow, near) <= ArtLibrary.MergeThreshold);
        bank.Colors.Add(Colour15.Argb(near));
        var hero = Image("hero");
        hero.Pixels[0] = Colour15.Argb(near);
        var original = bank.Colors.ToList();

        // merge result: index 10 merges into index 2, the rest is kept in order; same as the toolchain's MergeNear
        var (kept, map) = ArtLibrary.MergePlan(bank);
        Assert.Equal(original.Take(10), kept);
        Assert.Equal(new Dictionary<int, int> { [10] = 2 }, map);
        Assert.Equal(Quantizer.MergeNear(original.Skip(1).Select(Colour15.Of).ToList()), kept.Skip(1).Select(Colour15.Of));

        Assert.Equal(1, _lib.MergeNearDuplicates(bank));
        Assert.Equal(10, bank.Colors.Count);
        Assert.Equal(bank.Colors[2], hero.Pixels[0]);
        Assert.True(hero.IsDirty);
        _undo.Undo();
        Assert.Equal(original, bank.Colors);
        Assert.Equal(Colour15.Argb(near), hero.Pixels[0]);
    }

    [Fact]
    public void MergeNearDuplicates_NeverMergesTheTransparentIndexOrDistantColours()
    {
        var bank = new PaletteBank { Colors = [0, 0xFF000000u, 0xFF080808u, 0xFFFF0000u, 0xFF00FF00u] };
        var (kept, map) = ArtLibrary.MergePlan(bank);
        Assert.Equal([0u, 0xFF000000u, 0xFFFF0000u, 0xFF00FF00u], kept);
        Assert.Equal(new Dictionary<int, int> { [2] = 1 }, map);
        bank.LockTransparent = false;
        bank.Colors[0] = 0xFF000000u;
        (kept, map) = ArtLibrary.MergePlan(bank);
        Assert.Equal([0xFF000000u, 0xFFFF0000u, 0xFF00FF00u], kept);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void SortByHue_OrdersLikeTheToolchainAndKeepsIndex0()
    {
        var before = Hero.Colors.Skip(1).Select(Colour15.Of).ToList();
        _lib.SetOption(Hero, nameof(PaletteBank.SortByHue), true);
        Assert.True(Hero.SortByHue);
        Assert.Equal(Colour15.Transparent, Hero.Colors[0]);
        Assert.Equal(Quantizer.SortByHue(before), Hero.Colors.Skip(1).Select(Colour15.Of));
        _undo.Undo();
        Assert.Equal(before, Hero.Colors.Skip(1).Select(Colour15.Of));
        Assert.False(Hero.SortByHue);
    }

    [Fact]
    public void LockIndex0_KeepsItTransparentUntilUnlocked()
    {
        _lib.SetColor(Hero, 0, 0xFF123456u);
        Assert.Equal(Colour15.Transparent, Hero.Colors[0]);
        _lib.SetOption(Hero, nameof(PaletteBank.LockTransparent), false);
        Assert.False(Hero.IsTransparentIndex(0));
        Assert.Equal("10 / 16", Hero.UsedText);
        _lib.SetColor(Hero, 0, 0xFF123456u);
        Assert.Equal(0xFF123456u, Hero.Colors[0]);
        _lib.SetOption(Hero, nameof(PaletteBank.LockTransparent), true);
        Assert.Equal(Colour15.Transparent, Hero.Colors[0]);
    }

    [Fact]
    public void SetColor_RecoloursEveryAssetOfTheBankAsOneUndoStep()
    {
        var heart = Image("heart");
        var hero = Image("hero");
        var red = Hero.Colors[4];
        var heartRed = heart.Pixels.Count(p => p == red);
        var heroRed = hero.Pixels.Count(p => p == red);
        Assert.True(heartRed > 0 && heroRed > 0);
        var steps = _undo.UndoCount;
        _lib.SetColor(Hero, 4, Colour15.Argb(Colour15.Make(31, 20, 13)));
        _lib.SetColor(Hero, 4, Colour15.Argb(Colour15.Make(31, 0, 13)));
        Assert.Equal(steps + 1, _undo.UndoCount);
        var now = Hero.Colors[4];
        Assert.Equal(heartRed, heart.Pixels.Count(p => p == now));
        Assert.Equal(heroRed, hero.Pixels.Count(p => p == now));
        _undo.Undo();
        Assert.Equal(red, Hero.Colors[4]);
        Assert.Equal(heartRed, heart.Pixels.Count(p => p == red));
        _undo.Redo();
        Assert.Equal(now, Hero.Colors[4]);
    }

    [Fact]
    public void ExportAndImport_PalAndGpl()
    {
        foreach (var ext in new[] { ".pal", ".gpl" })
        {
            var path = Path.Combine(_fx.Folder, "hero" + ext);
            ArtLibrary.Export(Hero, path);
            var read = PaletteFiles.Read(path);
            Assert.Equal(16, read.Count);
            Assert.Equal(Enumerable.Range(0, Hero.Colors.Count).Select(Hero.Value), read.Take(Hero.Colors.Count));
        }
        // import the hero colours into Enemies: index-wise, index 0 stays transparent, slime follows
        var enemies = _lib.Find(BankKind.Obj, 1)!;
        var slime = Image("slime");
        var oldSlime1 = slime.Pixels.Count(p => p == enemies.Colors[1]);
        _lib.ImportColors(enemies, Path.Combine(_fx.Folder, "hero.gpl"));
        Assert.Equal(16, enemies.Colors.Count);
        Assert.Equal(Colour15.Transparent, enemies.Colors[0]);
        Assert.Equal(Hero.Value(4), enemies.Value(4));
        Assert.Equal(oldSlime1, slime.Pixels.Count(p => Colour15.Of(p) == enemies.Value(1) && p != 0));
        _undo.Undo();
        Assert.Equal(8, enemies.Colors.Count);
    }

    [Fact]
    public void Optimize_RemovesUnusedColours()
    {
        var items = _lib.Find(BankKind.Obj, 2)!;
        items.Colors.Add(0xFF0000FFu);
        var removed = _lib.Optimize();
        Assert.True(removed >= 1);
        Assert.DoesNotContain(0xFF0000FFu, items.Colors);
    }

    [Fact]
    public void AddBank_AndAssign_WriteTheOwnerIntoSpriteMeta()
    {
        var b = _lib.AddBank(BankKind.Obj)!;
        Assert.Equal("OBJ 3 · New palette", b.Label);
        _lib.Assign(ArtFixture.SpritePath(_lib, "heart"), b);
        Assert.Equal("heart.png", ArtLibrary.UsedBy(b));
        Assert.Equal("", _lib.Meta(ArtFixture.SpritePath(_lib, "heart")).Palette);
        _lib.Assign(ArtFixture.SpritePath(_lib, "cloud"), b);
        Assert.Equal("heart", _lib.Meta(ArtFixture.SpritePath(_lib, "cloud")).Palette);
        _lib.SaveAll();
        var file = GameProject.Load<PaletteLibraryFile>(_lib.FilePath);
        Assert.Equal(["assets/sprites/heart.png", "assets/sprites/cloud.png"], file.Banks.Single(x => x.Kind == BankKind.Obj && x.Slot == 3).Assets);
        Assert.Equal("heart", GameProject.Load<SpriteMeta>(GameProject.MetaPath(ArtFixture.SpritePath(_lib, "cloud"))).Palette);
        var reloaded = ArtLibrary.Load(_lib.Project, new UndoService(10));
        Assert.Equal(_lib.Banks.Count, reloaded.Banks.Count);
        Assert.Equal(Hero.Colors, reloaded.Find(BankKind.Obj, 0)!.Colors.Select(c => c));
        _undo.Undo();
        _undo.Undo();
        _undo.Undo();
        Assert.Null(_lib.Find(BankKind.Obj, 3));
    }

    [Fact]
    public void Banks_DerivedFromTheProjectWithoutAPaletteFile()
    {
        var root = Path.Combine(_fx.Folder, "Fresh");
        var p = SkyHopper.Create(root);
        var slimeMeta = GameProject.MetaPath(Path.Combine(p.AssetsDir, "sprites", "slime.png"));
        var m = GameProject.Load<SpriteMeta>(slimeMeta);
        m.Palette = "hero";
        GameProject.Save(slimeMeta, m);
        var lib = ArtLibrary.Load(GameProject.Open(root), new UndoService(10));
        var sprites = lib.SpriteFiles().Count;
        Assert.Equal(Math.Min(16, sprites - 1), lib.Of(BankKind.Obj).Count());
        Assert.Equal("hero.png, slime.png", ArtLibrary.UsedBy(lib.BankOf(Path.Combine(p.AssetsDir, "sprites", "hero.png"))!));
        Assert.Equal(p.ImageFiles().Count() + p.TilesetFiles().Count(), lib.Of(BankKind.Bg).Count());
        Assert.All(lib.Banks, b => Assert.InRange(b.Colors.Count, 2, 16));
        Assert.All(lib.Banks, b => Assert.Equal(Colour15.Transparent, b.Colors[0]));
        // every pixel of an asset maps exactly to a colour of its bank when it has 15 colours or fewer
        var hero = lib.Image(Path.Combine(p.AssetsDir, "sprites", "hero.png"));
        var heroBank = lib.BankOf(hero.Path)!;
        Assert.All(hero.Pixels.Where(px => px != 0), px => Assert.Equal(Colour15.Of(px), heroBank.Value(ArtLibrary.IndexOf(heroBank, px))));
    }

    [Fact]
    public void Effects_FadeCycleFlashAsTheRuntime()
    {
        // runtime: cycle offset after each step (agb::render::palettes::update)
        static int Runtime(int len, CycleDirection dir, int steps)
        {
            int offset = 0, step = 1;
            for (var i = 0; i < steps; i++)
            {
                if (dir == CycleDirection.Left) offset = (offset + 1) % len;
                else if (dir == CycleDirection.Right) offset = (offset + len - 1) % len;
                else
                {
                    if (offset + step >= len || offset + step < 0) step = -step;
                    offset += step;
                }
            }
            return offset;
        }
        foreach (var dir in Enum.GetValues<CycleDirection>())
            for (var frame = 0; frame < 200; frame++)
                Assert.Equal(Runtime(4, dir, frame / 8), PaletteEffects.CycleOffset(4, 8, dir, frame));
        ushort[] bank = [0, 1, 2, 3, 4, 5];
        Assert.Equal([0, 4, 1, 2, 3, 5], PaletteEffects.Cycle(bank, 1, 4, 8, CycleDirection.Right, 8).Select(x => (int)x));
        Assert.Equal(0, PaletteEffects.FadeAmount(0, 30));
        Assert.Equal(8, PaletteEffects.FadeAmount(15, 30));
        Assert.Equal(16, PaletteEffects.FadeAmount(30, 30));
        Assert.Equal(Colour15.Make(16, 16, 16), PaletteEffects.Fade(0x7FFF, 0, 16, 32));
        Assert.True(PaletteEffects.FlashOn(7, 8));
        Assert.False(PaletteEffects.FlashOn(8, 8));
        Assert.Equal("to black · 30 frames", PaletteEffects.FadeText(_lib.Effects.Fade));
        Assert.Equal("BG 1 · 6-9 · water", PaletteEffects.CycleText(_lib.Effects.Cycle));
    }

    [Fact]
    public void Colour15_LevelsHexAnd15BitValue()
    {
        Assert.Equal((ushort)0x7FFF, Colour15.Make(31, 31, 31));
        Assert.Equal((31, 10, 13), (Colour15.R(Colour15.Of(0xFFFF4F6Du)), Colour15.G(Colour15.Of(0xFFFF4F6Du)), Colour15.B(Colour15.Of(0xFFFF4F6Du))));
        Assert.Equal("0x355F", Colour15.Hex15(Colour15.Of(0xFFFF4F6Du)));
        Assert.Equal("#FF4F6D", Colour15.Hex24(0xFFFF4F6Du));
        Assert.True(Colour15.TryParse24("#ff4f6d", out var a) && a == 0xFFFF4F6Du);
        Assert.False(Colour15.TryParse24("#FF4F6", out _));
        Assert.True(Colour15.TryParse15("0x355F", out var c) && c == 0x355F);
        Assert.False(Colour15.TryParse15("0x8000", out _));
    }
}
