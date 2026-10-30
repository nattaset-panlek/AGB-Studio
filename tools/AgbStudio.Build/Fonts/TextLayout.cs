namespace AgbStudio.Build.Fonts;

/// <summary>Lays text out at build time (blueprint 13): Thai marks stacked, line breaks at word boundaries
/// from the dictionary, never inside a word. The runtime only places the glyphs this produces.</summary>
public static class TextLayout
{
    public sealed record Placed(int CodePoint, int X, int Dy, int Line, int Reveal);

    public sealed class Result
    {
        public List<Placed> Glyphs { get; } = new();
        public List<int> LineWidths { get; } = new();
        public int Lines => LineWidths.Count;
        public int Steps { get; set; }
        public int MaxWidth => LineWidths.Count == 0 ? 0 : LineWidths.Max();
        public HashSet<int> Missing { get; } = new();
    }

    sealed record Cluster(List<int> Cps, int Width, bool Space, bool BreakBefore, bool Forced);

    public static Result Layout(string text, GameFont font, int maxWidth, bool thaiWrap = true)
    {
        var clusters = Measure(text, font, thaiWrap);
        var result = new Result();
        // Greedy fill by words: a word = clusters from one break opportunity to the next.
        var words = new List<List<Cluster>>();
        foreach (var c in clusters)
        {
            if (words.Count == 0 || c.BreakBefore || c.Forced) words.Add(new List<Cluster>());
            words[^1].Add(c);
        }
        var line = 0;
        var x = 0;
        var step = 0;
        var lineHasContent = false;
        void NewLine()
        {
            result.LineWidths.Add(x);
            line++;
            x = 0;
            lineHasContent = false;
        }
        var placedCount = 0;
        foreach (var w in words)
        {
            if (w[0].Forced && (lineHasContent || result.LineWidths.Count < line + 1)) NewLine();
            var visible = w.Where(c => !c.Forced).ToList();
            var wordWidth = visible.Where(c => !c.Space).Sum(c => c.Width) + visible.TakeWhile(c => !c.Space).Count() * 0;
            var trailingSpace = visible.Count > 0 && visible[^1].Space ? visible[^1].Width : 0;
            var bodyWidth = visible.Sum(c => c.Width) - trailingSpace;
            if (lineHasContent && x + bodyWidth > maxWidth) NewLine();
            foreach (var c in visible)
            {
                if (c.Space && !lineHasContent) continue;  // no leading spaces on a line
                if (!c.Space && x + c.Width > maxWidth && lineHasContent && bodyWidth > maxWidth) NewLine();  // word longer than a line
                var idx = clusters.IndexOf(c);
                var nextIsSaraAm = idx >= 0 && idx + 1 < clusters.Count && clusters[idx + 1].Cps[0] == ThaiText.SaraAm;
                Place(c, font, x, line, step, result, nextIsSaraAm);
                placedCount++;
                x += c.Width;
                if (!c.Space) lineHasContent = true;
                step++;
            }
            _ = wordWidth;
        }
        // Trim trailing spaces from the last line width.
        result.LineWidths.Add(x);
        result.Steps = step;
        for (var i = 0; i < result.LineWidths.Count; i++) result.LineWidths[i] = TrimmedWidth(result, i);
        return result;
    }

    static int TrimmedWidth(Result r, int line)
    {
        var glyphs = r.Glyphs.Where(g => g.Line == line && !ThaiText.IsSpace(g.CodePoint)).ToList();
        return glyphs.Count == 0 ? 0 : glyphs.Max(g => g.X + 0);
    }

    static List<Cluster> Measure(string text, GameFont font, bool thaiWrap)
    {
        var cps = ThaiText.CodePoints(text);
        var clusters = ThaiText.Clusters(cps);
        // Break opportunities: after spaces, around non-Thai/Thai boundaries, at Thai word starts.
        var breakBefore = new bool[clusters.Count];
        var s = string.Concat(clusters.Select(c => char.ConvertFromUtf32(c[0]) + string.Concat(c.Skip(1).Select(char.ConvertFromUtf32))));
        var offsets = new List<int>();
        var pos = 0;
        foreach (var c in clusters)
        {
            offsets.Add(pos);
            pos += c.Sum(cp => char.ConvertFromUtf32(cp).Length);
        }
        var thaiStarts = new HashSet<int>();
        if (thaiWrap)
        {
            // Segment each Thai run with the dictionary.
            var i = 0;
            while (i < clusters.Count)
            {
                if (!ThaiText.IsThai(clusters[i][0])) { i++; continue; }
                var j = i;
                while (j < clusters.Count && ThaiText.IsThai(clusters[j][0]) && clusters[j][0] != 0x0E2F && clusters[j][0] != 0x0E46) j++;
                var run = s[offsets[i]..(j < clusters.Count ? offsets[j] : s.Length)];
                foreach (var start in ThaiText.WordStarts(run)) thaiStarts.Add(offsets[i] + start);
                i = Math.Max(j, i + 1);
            }
        }
        var list = new List<Cluster>();
        for (var i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            var forced = c[0] == '\n';
            var space = ThaiText.IsSpace(c[0]);
            var prevIsSpace = i > 0 && ThaiText.IsSpace(clusters[i - 1][0]);
            var prevThai = i > 0 && ThaiText.IsThai(clusters[i - 1][0]);
            var thai = ThaiText.IsThai(c[0]);
            var bb = forced || prevIsSpace || (i > 0 && thai != prevThai && !space) || (thai && thaiStarts.Contains(offsets[i]));
            var width = forced ? 0 : font.Get(c[0])?.Advance ?? 0;
            list.Add(new Cluster(c, width, space, bb, forced));
        }
        return list;
    }

    // Places one cluster: the base at the pen, marks over it with stacking rules.
    static void Place(Cluster c, GameFont font, int x, int line, int step, Result r, bool nextIsSaraAm)
    {
        var baseCp = c.Cps[0];
        if (font.Get(baseCp) == null && !ThaiText.IsSpace(baseCp)) r.Missing.Add(baseCp);
        r.Glyphs.Add(new Placed(baseCp, x, 0, line, step));
        var pen = x + c.Width;
        // Sara am carries a nikhahit over this consonant, so a tone mark here stacks as over an upper vowel.
        var hasUpper = c.Cps.Skip(1).Any(ThaiText.IsUpperVowel) || nextIsSaraAm;
        var baseGlyph = font.Get(baseCp);
        var ascShift = ThaiText.IsAscender(baseCp) ? -Math.Max(1, font.Settings.Size / 7) : 0;
        foreach (var m in c.Cps.Skip(1))
        {
            var g = font.Get(m);
            if (g == null) { r.Missing.Add(m); continue; }
            var dx = 0;
            var dy = 0;
            if (ThaiText.IsUpperVowel(m))
            {
                if (font.Settings.StackUpperMarks) dx = ascShift;
            }
            else if (ThaiText.IsToneMark(m) && font.Settings.StackUpperMarks)
            {
                dx = ascShift;
                // Tone marks sit above an upper vowel; without one they drop to the upper-vowel height.
                var upperBottom = font.Get(0x0E34) is { } uv ? uv.Top + uv.Height : g.Top;
                if (hasUpper && g.Top + g.Height > font.UpperVowelTop - 1) dy = font.UpperVowelTop - 1 - (g.Top + g.Height);
                else if (!hasUpper && g.Top + g.Height < upperBottom - 1) dy = upperBottom - (g.Top + g.Height);
            }
            else if (ThaiText.IsLowerVowel(m) && font.Settings.PlaceLowerVowels && baseGlyph != null && ThaiText.IsDescender(baseCp))
            {
                // Below the descender of ฎ ฏ ฐ ญ.
                var baseBottom = baseGlyph.Top + baseGlyph.Height;
                if (baseBottom > font.Baseline) dy = baseBottom - g.Top + 1 - (font.Baseline - g.Top);
            }
            r.Glyphs.Add(new Placed(m, pen + dx, dy, line, step));
        }
    }

    /// <summary>The Localization fit check: lines used and the widest line against the box.</summary>
    public static (bool Fits, int Lines, int Width) Fit(string text, GameFont font, int boxWidth, int maxLines)
    {
        var r = Layout(text, font, boxWidth);
        return (r.Lines <= maxLines && r.MaxWidth <= boxWidth, r.Lines, r.MaxWidth);
    }
}
