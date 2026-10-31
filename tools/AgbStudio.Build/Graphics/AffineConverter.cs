namespace AgbStudio.Build.Graphics;

/// <summary>Turns a map layer of 16x16 metatiles into an affine layer (Add Layer "Rotate / scale layer (Mode 1-2)",
/// the Racing template's Mode 7 floor): 8bpp tiles with flips and palette banks baked in, one tile index per 8x8
/// cell. Affine maps have no flip or palette bits and hold at most 256 tiles. The map is padded to the hardware size
/// (16, 32, 64 or 128 tiles square) so the runtime copies it to VRAM as it is.</summary>
public static class AffineConverter
{
    public sealed record Result(byte[] Tiles, int TileCount, ushort[] Cells, int Width, int Height, string? Problem)
    {
        /// <summary>The hardware map: one byte per cell, two cells per halfword with the low byte first.</summary>
        public IEnumerable<int> PackedCells => Enumerable.Range(0, Cells.Length / 2).Select(i => Cells[i * 2] | Cells[i * 2 + 1] << 8);
    }

    public static Result Convert(ConvertedTileset t, IReadOnlyList<int> cells, int widthMetatiles, int heightMetatiles)
    {
        var w = 16;
        while (w < Math.Max(widthMetatiles, heightMetatiles) * 2 && w < 128) w *= 2;
        var h = w;
        var tiles = new List<byte[]> { new byte[64] };  // tile 0 stays empty: cells outside the map show it
        var lookup = new Dictionary<string, int> { [System.Convert.ToHexString(tiles[0])] = 0 };
        var outCells = new ushort[w * h];
        string? problem = null;
        for (var my = 0; my < heightMetatiles && my * 2 < h; my++)
            for (var mx = 0; mx < widthMetatiles && mx * 2 < w; mx++)
            {
                var m = cells[my * widthMetatiles + mx];
                if (m < 0 || m >= t.Metatiles.Count) continue;
                for (var q = 0; q < 4; q++)
                {
                    var se = t.Metatiles[m][q];
                    var src = t.Tiles.Tiles[se & 0x3FF];
                    var px = TileSet4.Flip(src, (se & 1 << 10) != 0, (se & 1 << 11) != 0);
                    var bank = se >> 12;
                    for (var i = 0; i < 64; i++)
                        if (px[i] != 0) px[i] = (byte)(bank * 16 + px[i]);
                    var key = System.Convert.ToHexString(px);
                    if (!lookup.TryGetValue(key, out var index))
                    {
                        if (tiles.Count == 256)
                        {
                            problem ??= "a rotate / scale layer holds at most 256 different 8×8 tiles, so the rest show as empty. Use fewer different tiles in it, or make it a normal tile layer.";
                            index = 0;
                        }
                        else
                        {
                            index = tiles.Count;
                            tiles.Add(px);
                            lookup[key] = index;
                        }
                    }
                    outCells[(my * 2 + q / 2) * w + mx * 2 + q % 2] = (ushort)index;
                }
            }
        if (Math.Max(widthMetatiles, heightMetatiles) * 2 > 128) problem ??= "a rotate / scale layer is at most 64 × 64 map tiles (1024 × 1024 pixels), so the rest is cut off. Make the map smaller, or make it a normal tile layer.";
        return new Result(tiles.SelectMany(x => x).ToArray(), tiles.Count, outCells, w, h, problem);
    }
}
