using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>P5.01: a secondary tileset goes on from a shared primary: same tiles at the same indices, a tile the
/// primary has (or a flip of it) is not stored again, and new colours go into banks after the primary's.</summary>
public sealed class PrimaryTilesetTests
{
    // One image of two 16x16 metatiles side by side; each 8x8 tile filled by pattern(tileIndex) with a colour.
    static ColorImage Image(params (ushort Color, int Seed)[] metatiles)
    {
        var img = new ColorImage(16 * metatiles.Length, 16);
        for (var m = 0; m < metatiles.Length; m++)
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                {
                    var i = y * img.Width + m * 16 + x;
                    img.Colors[i] = (x + y) % (2 + metatiles[m].Seed) == 0 ? (ushort)0x7FFF : metatiles[m].Color;
                    img.Transparent[i] = false;
                }
        return img;
    }

    [Fact]
    public void SecondaryKeepsThePrimarysTilesAndBanksAndStoresOnlyNewOnes()
    {
        var primary = TilesetConverter.Convert("town", Image((0x001F, 1), (0x03E0, 2)), new TilesetMeta(), []);
        // The first metatile equals the primary's first; the second is new with a new colour.
        var sec = TilesetConverter.Convert("house", Image((0x001F, 1), (0x7C00, 3)), new TilesetMeta { Primary = "town" }, [], 13, "", primary);

        Assert.Same(primary, sec.Primary);
        Assert.Equal(primary.Tiles.Count, sec.OwnTileStart);
        Assert.Equal(primary.Palette.Banks.Count, sec.OwnBankStart);
        Assert.True(sec.OwnTileCount > 0);
        Assert.True(sec.OwnBankCount > 0);
        Assert.Equal(primary.Tiles.ToBytes(), sec.Tiles.ToBytes()[..(primary.Tiles.Count * 32)]);
        for (var b = 0; b < primary.Palette.Banks.Count; b++) Assert.Equal(primary.Palette.Banks[b], sec.Palette.Banks[b]);
        Assert.Equal(sec.Tiles.Count * 32 - primary.Tiles.Count * 32, sec.OwnTileBytes().Length);
        Assert.Equal(sec.OwnBankCount * 16, sec.OwnColors().Length);
        // The first metatile reuses the primary's tiles, so it points at the same screen entries.
        Assert.Equal(primary.Metatiles[0], sec.Metatiles[0]);
    }

    [Fact]
    public void ExtendingDoesNotChangeThePrimary()
    {
        var primary = TilesetConverter.Convert("town", Image((0x001F, 1), (0x03E0, 2)), new TilesetMeta(), []);
        var count = primary.Tiles.Count;
        var banks = primary.Palette.Banks.Count;
        _ = TilesetConverter.Convert("house", Image((0x7C00, 3), (0x7C00, 4)), new TilesetMeta { Primary = "town" }, [], 13, "", primary);
        Assert.Equal(count, primary.Tiles.Count);
        Assert.Equal(banks, primary.Palette.Banks.Count);
    }

    [Fact]
    public void WithoutAPrimaryNothingChanges()
    {
        var t = TilesetConverter.Convert("a", Image((0x001F, 1)), new TilesetMeta(), []);
        Assert.Null(t.Primary);
        Assert.Equal(0, t.OwnTileStart);
        Assert.Equal(t.Tiles.Count, t.OwnTileCount);
    }

    [Fact]
    public void PackKeepsFixedBanksAndReusesOneThatHoldsAllColours()
    {
        var fixedSets = new List<HashSet<ushort>> { new() { 1, 2, 3 } };
        var fixedBanks = PaletteBanks.Pack(fixedSets, 13)!.Value.Banks;
        var r = PaletteBanks.Pack([new HashSet<ushort> { 2, 3 }, new HashSet<ushort> { 9 }], 13, fixedBanks)!.Value;
        Assert.Equal([0, 1], r.Assign);
        Assert.Equal(2, r.Banks.Banks.Count);
        Assert.Equal(fixedBanks.Banks[0], r.Banks.Banks[0]);
    }
}
