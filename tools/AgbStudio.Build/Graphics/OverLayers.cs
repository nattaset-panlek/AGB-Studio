using AgbStudio.Core.Project;

namespace AgbStudio.Build.Graphics;

/// <summary>Two-layer metatiles: a tile with <see cref="TileProperties.Over"/> also draws another tile of its tileset in
/// front of the actors (a treetop over the trunk you walk behind). A tile layer that uses such tiles gets one more
/// hardware layer, appended after the map's own so layer numbers and collision do not change. The build and the editor's
/// previews both expand a map here, so they agree.</summary>
public static class OverLayers
{
    /// <summary>A cell of an over layer with nothing to draw: past every metatile, so the runtime leaves it blank.</summary>
    public const int Empty = 0xFFFF;
    const int FlipBit = 0x8000;

    /// <param name="Map">The map to draw: the same object when no over layer is needed.</param>
    /// <param name="Partner">For each of the map's own layers, its over layer's index or -1.</param>
    /// <param name="Problem">Why some over tiles cannot be drawn, in words for the user; null when all can.</param>
    public sealed record Expanded(MapFile Map, int[] Partner, string? Problem);

    /// <summary>The cell an over layer shows for one cell of its layer.</summary>
    public static int OverCell(ConvertedTileset tileset, int cell)
    {
        var tile = cell & ~FlipBit;
        var over = tile >= 0 && tile < tileset.Over.Count ? tileset.Over[tile] : -1;
        return over < 0 ? Empty : over | (cell & FlipBit);
    }

    public static Expanded Expand(MapFile map, ConvertedTileset? tileset)
    {
        var partner = Enumerable.Repeat(-1, map.Layers.Count).ToArray();
        if (tileset == null || !tileset.Over.Exists(o => o >= 0)) return new Expanded(map, partner, null);
        // An affine layer means Mode 1 or 2, where only BG0 and BG1 are regular layers.
        int[] regular = map.Layers.Exists(l => l.Type == LayerType.Affine) ? [1, 0] : [3, 2, 1, 0];
        // The highest free BG: at the same priority the text layer, which takes a lower free BG, stays in front.
        var free = new Queue<int>(regular.Where(b => !map.Layers.Exists(l => l.Type != LayerType.CollisionOnly && (l.Bg & 3) == b)));
        var layers = new List<MapLayerData>(map.Layers);
        var left = 0;
        for (var i = 0; i < map.Layers.Count; i++)
        {
            var l = map.Layers[i];
            if (l.Type != LayerType.Tile) continue;
            var cells = l.Cells.Select(c => OverCell(tileset, c)).ToList();
            if (cells.TrueForAll(c => c == Empty)) continue;
            if (free.Count == 0 || layers.Count >= 4) { left++; continue; }
            partner[i] = layers.Count;
            layers.Add(new MapLayerData
            {
                Name = l.Name + " (over)", Bg = free.Dequeue(), Type = LayerType.Tile, Priority = 0,
                ParallaxX = l.ParallaxX, ParallaxY = l.ParallaxY, Wrap = l.Wrap, Cells = cells,
            });
        }
        var problem = left == 0 ? null : $"Map {map.Name} has tiles that draw over the actors, but no background layer is free for them on {left} layer(s), so that part is not shown. Use fewer layers in this map.";
        if (layers.Count == map.Layers.Count) return new Expanded(map, partner, problem);
        return new Expanded(new MapFile
        {
            Format = map.Format, Name = map.Name, Tileset = map.Tileset, Width = map.Width, Height = map.Height,
            Layers = layers, CollisionLayer = map.CollisionLayer, BgSize = map.BgSize, Bpp = map.Bpp,
        }, partner, problem);
    }
}
