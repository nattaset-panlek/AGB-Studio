using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.UiEditor;

/// <summary>A smart guide in game pixels: a vertical line at x = At from y = From to To, or a horizontal one at y = At.</summary>
public readonly record struct UiGuide(bool Vertical, double At, int From, int To);

/// <summary>What a drag can line up with: an element, or the whole screen.</summary>
public readonly record struct UiSnapTarget(int Left, int Top, int Right, int Bottom);

/// <summary>Which edges of the dragged rectangle follow the pointer. A move has both edges of an axis, so its centre snaps too.</summary>
public readonly record struct UiSnapEdges(bool Left, bool Right, bool Top, bool Bottom);

/// <summary>
/// Smart guides of the UI Editor: lines a dragged element up with the edges and centres of other elements and of the
/// screen. Positions are doubled inside, so the centre of an odd-sized element stays exact; an element only snaps to a
/// line it can reach in whole pixels.
/// </summary>
public static class UiSnap
{
    public static (int Dx, int Dy, List<UiGuide> Guides) Solve(UiPixelRect start, int dx, int dy, UiSnapEdges edges, IReadOnlyList<UiSnapTarget> targets, int threshold)
    {
        int[] Lines(int a, int b, int d, bool lo, bool hi) => lo && hi ? [2 * (a + d), a + b + 2 * d, 2 * (b + d)] : lo ? [2 * (a + d)] : hi ? [2 * (b + d)] : [];
        int[] Xs(int d) => Lines(start.X, start.Right, d, edges.Left, edges.Right);
        int[] Ys(int d) => Lines(start.Y, start.Bottom, d, edges.Top, edges.Bottom);
        static int[] TargetLines(int a, int b) => [2 * a, a + b, 2 * b];

        dx += Correction(Xs(dx), targets.SelectMany(t => TargetLines(t.Left, t.Right)), threshold);
        dy += Correction(Ys(dy), targets.SelectMany(t => TargetLines(t.Top, t.Bottom)), threshold);

        var guides = new List<UiGuide>();
        var left = start.X + (edges.Left ? dx : 0);
        var right = start.Right + (edges.Right ? dx : 0);
        var top = start.Y + (edges.Top ? dy : 0);
        var bottom = start.Bottom + (edges.Bottom ? dy : 0);
        foreach (var line in Xs(dx).Distinct())
        {
            var hits = targets.Where(t => TargetLines(t.Left, t.Right).Contains(line)).ToList();
            if (hits.Count > 0) guides.Add(new UiGuide(true, line / 2.0, Math.Min(top, hits.Min(t => t.Top)), Math.Max(bottom, hits.Max(t => t.Bottom))));
        }
        foreach (var line in Ys(dy).Distinct())
        {
            var hits = targets.Where(t => TargetLines(t.Top, t.Bottom).Contains(line)).ToList();
            if (hits.Count > 0) guides.Add(new UiGuide(false, line / 2.0, Math.Min(left, hits.Min(t => t.Left)), Math.Max(right, hits.Max(t => t.Right))));
        }
        return (dx, dy, guides);
    }

    /// <summary>The whole pixels to add so the nearest line within the threshold meets its target; 0 when none is near.</summary>
    static int Correction(int[] moving, IEnumerable<int> targets, int threshold)
    {
        var best = int.MaxValue;
        var lines = targets.Distinct().ToList();
        foreach (var m in moving)
            foreach (var t in lines)
            {
                var gap = t - m;
                if (gap % 2 == 0 && Math.Abs(gap) <= 2 * threshold && Math.Abs(gap) < Math.Abs(best)) best = gap;
            }
        return best == int.MaxValue ? 0 : best / 2;
    }
}
