using AgbStudio.Core.Project;

namespace AgbStudio.Build.Graphics;

/// <summary>Auto-tile rules (T-P26): when painting with a rule set, each cell of the set takes the tile of the
/// first rule whose 3x3 mask matches its neighbours (1 = same set, 0 = not the set, 2 = any; centre ignored).
/// Results are stored in the map, so the runtime never evaluates rules (P5.09).</summary>
public static class AutoTile
{
    static readonly (int Dx, int Dy)[] Offsets = [(-1, -1), (0, -1), (1, -1), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];

    public static HashSet<int> Members(AutoTileSet set) => set.Rules.Select(r => r.Tile).ToHashSet();

    /// <summary>The tile a set member at (x, y) should show. Cells outside the map count as "same set"
    /// when <paramref name="edgesAreSame"/> (ground continues past the map edge).</summary>
    public static int Resolve(IReadOnlyList<int> cells, int width, int height, int x, int y, AutoTileSet set, bool edgesAreSame = true)
    {
        var members = Members(set);
        foreach (var rule in set.Rules)
        {
            var ok = true;
            for (var i = 0; i < 9 && ok; i++)
            {
                if (i == 4 || rule.Mask.Length < 9 || rule.Mask[i] == 2) continue;
                var (dx, dy) = Offsets[i];
                int nx = x + dx, ny = y + dy;
                var same = nx < 0 || ny < 0 || nx >= width || ny >= height ? edgesAreSame : members.Contains(cells[ny * width + nx]);
                ok = (rule.Mask[i] == 1) == same;
            }
            if (ok) return rule.Tile;
        }
        return cells[y * width + x];
    }

    /// <summary>Paints (x, y) with the set and re-resolves it and its eight neighbours that belong to the set.</summary>
    public static void Paint(IList<int> cells, int width, int height, int x, int y, AutoTileSet set)
    {
        var members = Members(set);
        if (set.Rules.Count == 0) return;
        cells[y * width + x] = set.Rules[^1].Tile;  // any member marks the cell as part of the set
        foreach (var (dx, dy) in Offsets)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height || !members.Contains(cells[ny * width + nx])) continue;
            cells[ny * width + nx] = Resolve((IReadOnlyList<int>)cells, width, height, nx, ny, set);
        }
    }

    /// <summary>Erases (x, y) and re-resolves the neighbours.</summary>
    public static void Erase(IList<int> cells, int width, int height, int x, int y, AutoTileSet set, int empty = 0)
    {
        var members = Members(set);
        cells[y * width + x] = empty;
        foreach (var (dx, dy) in Offsets)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height || !members.Contains(cells[ny * width + nx])) continue;
            cells[ny * width + nx] = Resolve((IReadOnlyList<int>)cells, width, height, nx, ny, set);
        }
    }
}
