using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>Where one layer's graphics would go in VRAM.</summary>
public sealed record LayerVram(int Layer, int CharBlock, int FirstTile, int ScreenBlock, int ScreenBlockCount, bool HasGraphics, bool Fits)
{
    /// <summary>The Layers table VRAM column: "char block 0 · screen 28", "empty" or "no room".</summary>
    public string Text => !HasGraphics ? "empty" : !Fits ? "no room" : $"char block {CharBlock} · screen {ScreenBlock}";
}

/// <summary>The graphics a layer loads (a tileset or an image), for <see cref="VramPlan"/>.</summary>
public sealed record LayerGraphics(string? Key, int Tiles, int PaletteBanks, bool CollisionOnly);

/// <summary>
/// The VRAM a map's backgrounds would take, worked out with the rules of the runtime BG manager
/// (runtime/src/render/bg.cpp): layers load in file order; tiles go to the first char block (0 upward) with room after
/// what is already there, a tileset shared by layers loads once; each layer's map takes screen blocks from block 31
/// downward (64x32 = 2 blocks). Tile data and maps share the 32 units of 2 KB.
/// </summary>
public sealed class VramPlan
{
    const int UnitBytes = 2048;
    const int Units = 32;

    public IReadOnlyList<LayerVram> Layers { get; }
    /// <summary>Unique 8x8 tiles in VRAM (every loaded tileset once).</summary>
    public int UniqueTiles { get; }
    public int TileBytes { get; }
    /// <summary>Screen blocks taken by maps.</summary>
    public int ScreenBlocks { get; }
    public int PaletteBanks { get; }
    public int VramBytes => TileBytes + ScreenBlocks * UnitBytes;

    VramPlan(List<LayerVram> layers, int tiles, int tileBytes, int screenBlocks, int banks)
    {
        Layers = layers;
        UniqueTiles = tiles;
        TileBytes = tileBytes;
        ScreenBlocks = screenBlocks;
        PaletteBanks = banks;
    }

    public static int ScreenBlocksOf(BgSize s) => s switch { BgSize.S32x32 => 1, BgSize.S64x64 => 4, _ => 2 };

    public static VramPlan Compute(IReadOnlyList<LayerGraphics> layers, BgSize size, bool bpp8)
    {
        var used = new bool[Units];
        var tilesInBlock = new int[4];   // in 32-byte units, as the runtime counts them
        var loaded = new Dictionary<string, (int Cb, int First)>(StringComparer.OrdinalIgnoreCase);
        var result = new List<LayerVram>();
        int tiles = 0, tileBytes = 0, blocks = 0, banks = 0;
        var tileSize = bpp8 ? 64 : 32;

        (int Cb, int First)? Reserve(int count)
        {
            var bytes = count * tileSize;
            for (var cb = 0; cb < 4; cb++)
            {
                var startByte = tilesInBlock[cb] * 32;
                var endByte = startByte + bytes;
                if (startByte / 32 + count > 1024) continue;
                var firstUnit = cb * 8 + startByte / UnitBytes;
                var lastUnit = cb * 8 + (endByte + UnitBytes - 1) / UnitBytes;
                var ok = lastUnit <= Units;
                for (var u = firstUnit; ok && u < lastUnit; u++)
                {
                    var ownPartial = u == firstUnit && startByte % UnitBytes != 0;
                    if (!ownPartial && used[u]) ok = false;
                }
                if (!ok) continue;
                for (var u = firstUnit; u < lastUnit; u++) used[u] = true;
                tilesInBlock[cb] = endByte / 32;
                return (cb, startByte / 32 / (bpp8 ? 2 : 1));
            }
            return null;
        }

        int AllocScreen(int count)
        {
            for (var first = Units - count; first >= 0; first--)
            {
                var free = true;
                for (var u = first; u < first + count; u++) free &= !used[u];
                if (!free) continue;
                for (var u = first; u < first + count; u++) used[u] = true;
                return first;
            }
            return -1;
        }

        var sbCount = ScreenBlocksOf(size);
        for (var i = 0; i < layers.Count; i++)
        {
            var g = layers[i];
            if (g.CollisionOnly) { result.Add(new LayerVram(i, -1, 0, -1, 0, false, true)); continue; }
            if (g.Key is null) { result.Add(new LayerVram(i, -1, 0, -1, 0, false, true)); continue; }
            if (!loaded.TryGetValue(g.Key, out var place))
            {
                if (Reserve(g.Tiles) is not { } p) { result.Add(new LayerVram(i, -1, 0, -1, 0, true, false)); continue; }
                place = p;
                loaded[g.Key] = p;
                tiles += g.Tiles;
                tileBytes += g.Tiles * tileSize;
                banks += g.PaletteBanks;
            }
            var sb = AllocScreen(sbCount);
            if (sb < 0) { result.Add(new LayerVram(i, place.Cb, place.First, -1, 0, true, false)); continue; }
            blocks += sbCount;
            result.Add(new LayerVram(i, place.Cb, place.First, sb, sbCount, true, true));
        }
        return new VramPlan(result, tiles, tileBytes, blocks, banks);
    }

    /// <summary>OAM entries the scene's actors take (one per actor with a sprite).</summary>
    public static int OamEntries(SceneFile? scene) => scene?.Actors.Count(a => a.Sprite.Length > 0) ?? 0;
}
