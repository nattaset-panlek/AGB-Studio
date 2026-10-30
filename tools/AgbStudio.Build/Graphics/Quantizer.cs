namespace AgbStudio.Build.Graphics;

/// <summary>Reduces a set of BGR555 colours to at most N (median cut on 15-bit colours, weighted by use).</summary>
public static class Quantizer
{
    public static List<ushort> Reduce(IReadOnlyDictionary<ushort, int> histogram, int maxColors)
    {
        if (histogram.Count <= maxColors) return histogram.Keys.OrderBy(c => c).ToList();
        var boxes = new List<List<KeyValuePair<ushort, int>>> { histogram.ToList() };
        while (boxes.Count < maxColors)
        {
            // Split the box with the largest weighted channel range.
            var bestIndex = -1;
            var bestScore = -1L;
            var bestChannel = 0;
            for (var i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Count < 2) continue;
                for (var ch = 0; ch < 3; ch++)
                {
                    var values = boxes[i].Select(kv => Channel(kv.Key, ch)).ToList();
                    long range = values.Max() - values.Min();
                    long score = range * boxes[i].Sum(kv => (long)kv.Value);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                        bestChannel = ch;
                    }
                }
            }
            if (bestIndex < 0) break;
            var box = boxes[bestIndex].OrderBy(kv => Channel(kv.Key, bestChannel)).ToList();
            var total = box.Sum(kv => (long)kv.Value);
            long acc = 0;
            var cut = 1;
            for (var i = 0; i < box.Count - 1; i++)
            {
                acc += box[i].Value;
                if (acc * 2 >= total) { cut = i + 1; break; }
                cut = i + 1;
            }
            boxes[bestIndex] = box.Take(cut).ToList();
            boxes.Add(box.Skip(cut).ToList());
        }
        return boxes.Where(b => b.Count > 0).Select(Average).Distinct().OrderBy(c => c).ToList();
    }

    static int Channel(ushort c, int ch) => (c >> (ch * 5)) & 31;

    static ushort Average(List<KeyValuePair<ushort, int>> box)
    {
        long w = 0, r = 0, g = 0, b = 0;
        foreach (var (c, n) in box)
        {
            w += n;
            r += (c & 31) * (long)n;
            g += ((c >> 5) & 31) * (long)n;
            b += ((c >> 10) & 31) * (long)n;
        }
        return (ushort)((int)((r + w / 2) / w) | ((int)((g + w / 2) / w) << 5) | ((int)((b + w / 2) / w) << 10));
    }

    public static int Nearest(IReadOnlyList<ushort> palette, ushort c, int start = 0)
    {
        var best = start;
        var bestD = int.MaxValue;
        for (var i = start; i < palette.Count; i++)
        {
            var d = Gba.Distance(palette[i], c);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>Merges colours closer than <paramref name="threshold"/> (Palette Manager "Merge near-duplicate colours").</summary>
    public static List<ushort> MergeNear(IReadOnlyList<ushort> colors, int threshold = 12)
    {
        var result = new List<ushort>();
        foreach (var c in colors)
            if (!result.Any(r => Gba.Distance(r, c) <= threshold)) result.Add(c);
        return result;
    }

    public static List<ushort> SortByHue(IEnumerable<ushort> colors) =>
        colors.OrderBy(c => Hue(c)).ThenBy(c => (c & 31) + ((c >> 5) & 31) + ((c >> 10) & 31)).ToList();

    static double Hue(ushort c)
    {
        double r = (c & 31) / 31.0, g = ((c >> 5) & 31) / 31.0, b = ((c >> 10) & 31) / 31.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        if (max - min < 1e-9) return -1;  // greys first
        double h = max == r ? (g - b) / (max - min) : max == g ? 2 + (b - r) / (max - min) : 4 + (r - g) / (max - min);
        return (h * 60 + 360) % 360;
    }
}
