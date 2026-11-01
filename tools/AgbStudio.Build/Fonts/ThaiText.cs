using System.Text;

namespace AgbStudio.Build.Fonts;

/// <summary>Thai character classes, grapheme clusters and dictionary word segmentation (D-17: ICU word list).</summary>
public static class ThaiText
{
    public static bool IsThai(int c) => c is >= 0x0E00 and <= 0x0E7F;
    public static bool IsConsonant(int c) => c is >= 0x0E01 and <= 0x0E2E;
    // ป ฝ ฟ ฬ: tall stems that upper marks must avoid.
    public static bool IsAscender(int c) => c is 0x0E1B or 0x0E1D or 0x0E1F or 0x0E2C;
    // ฎ ฏ have descenders; ฐ ญ have tails below the baseline.
    public static bool IsDescender(int c) => c is 0x0E0E or 0x0E0F or 0x0E10 or 0x0E0D;
    public static bool IsUpperVowel(int c) => c is 0x0E31 or 0x0E34 or 0x0E35 or 0x0E36 or 0x0E37 or 0x0E47 or 0x0E4D;
    public static bool IsLowerVowel(int c) => c is 0x0E38 or 0x0E39 or 0x0E3A;
    public static bool IsToneMark(int c) => c is >= 0x0E48 and <= 0x0E4C or 0x0E4E;
    public static bool IsMark(int c) => IsUpperVowel(c) || IsLowerVowel(c) || IsToneMark(c);
    public const int SaraAm = 0x0E33, Nikhahit = 0x0E4D, SaraAa = 0x0E32;
    // ญ and ฐ drawn without their tails, for a lower vowel under them (the private-use code points Thai fonts
    // traditionally give these forms). A font may have them; TextLayout uses them when it does.
    public const int YoYingNoTail = 0xF70F, ThoThanNoTail = 0xF700;

    /// <summary>The tailless form of ญ or ฐ, or 0 for other characters.</summary>
    public static int Tailless(int c) => c switch { 0x0E0D => YoYingNoTail, 0x0E10 => ThoThanNoTail, _ => 0 };

    public static List<int> CodePoints(string s)
    {
        var list = new List<int>(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = char.ConvertToUtf32(s, i);
            if (char.IsHighSurrogate(s[i])) i++;
            list.Add(c);
        }
        return list;
    }

    /// <summary>Splits text into display clusters: a base character followed by its combining marks.</summary>
    public static List<List<int>> Clusters(IReadOnlyList<int> cps)
    {
        var clusters = new List<List<int>>();
        foreach (var c in cps)
        {
            if (IsMark(c) && clusters.Count > 0 && clusters[^1].Count > 0 && !IsSpace(clusters[^1][0])) clusters[^1].Add(c);
            else clusters.Add([c]);
        }
        return clusters;
    }

    public static bool IsSpace(int c) => c is ' ' or '\t' or 0x3000 or 0x00A0;

    // ---- dictionary --------------------------------------------------------------------------------

    sealed class Node
    {
        public Dictionary<char, Node>? Next;
        public bool Word;
    }

    static Node? s_root;
    static readonly object s_lock = new();
    public static int DictionaryWords { get; private set; }

    public static string DictionaryPath => Path.Combine(NativeBuild.RepoRoot, "third_party", "icu", "thaidict.txt");

    static Node Root()
    {
        lock (s_lock)
        {
            if (s_root != null) return s_root;
            var root = new Node();
            var count = 0;
            foreach (var raw in File.ReadLines(DictionaryPath, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var tab = line.IndexOf('\t');
                if (tab > 0) line = line[..tab];
                var n = root;
                foreach (var ch in line)
                {
                    n.Next ??= new Dictionary<char, Node>();
                    if (!n.Next.TryGetValue(ch, out var child)) n.Next[ch] = child = new Node();
                    n = child;
                }
                n.Word = true;
                count++;
            }
            DictionaryWords = count;
            return s_root = root;
        }
    }

    /// <summary>Segments a Thai run into words with maximal matching (fewest words, unknown text kept as
    /// single clusters). Returns the start index of each word in <paramref name="text"/>.</summary>
    public static List<int> WordStarts(string text)
    {
        var root = Root();
        var n = text.Length;
        // Valid break positions: not before a combining mark.
        var canBreak = new bool[n + 1];
        for (var i = 0; i <= n; i++) canBreak[i] = i == n || i == 0 || !IsMark(text[i]);
        var best = new int[n + 1];
        var unknown = new int[n + 1];
        var prev = new int[n + 1];
        for (var i = 1; i <= n; i++) best[i] = int.MaxValue;
        for (var i = 0; i < n; i++)
        {
            if (best[i] == int.MaxValue || !canBreak[i]) continue;
            var node = root;
            var matched = false;
            for (var j = i; j < n && node.Next != null && node.Next.TryGetValue(text[j], out var next); j++)
            {
                node = next;
                if (node.Word && canBreak[j + 1] && Better(best[i] + 1, unknown[i], j + 1))
                {
                    best[j + 1] = best[i] + 1;
                    unknown[j + 1] = unknown[i];
                    prev[j + 1] = i;
                    matched = true;
                }
            }
            // Unknown text: one cluster at a time, penalised so dictionary words win.
            var k = i + 1;
            while (k < n && !canBreak[k]) k++;
            if (Better(best[i] + 1, unknown[i] + 1, k) || (!matched && best[k] == int.MaxValue))
            {
                best[k] = best[i] + 1;
                unknown[k] = unknown[i] + 1;
                prev[k] = i;
            }
        }
        bool Better(int words, int unk, int at) =>
            best[at] == int.MaxValue || unk < unknown[at] || (unk == unknown[at] && words < best[at]);

        var starts = new List<int>();
        for (var pos = n; pos > 0; pos = prev[pos]) starts.Add(prev[pos]);
        starts.Reverse();
        // Merge runs of unknown clusters into one segment so names ("ฮอปเปอร์") are not split letter by letter.
        var merged = new List<int>(starts.Count);
        var prevUnknown = false;
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1] : n;
            var isUnknown = !InDictionary(text[starts[i]..end]);
            if (!(isUnknown && prevUnknown)) merged.Add(starts[i]);
            prevUnknown = isUnknown;
        }
        return merged;
    }

    public static bool InDictionary(string word)
    {
        var n = Root();
        foreach (var ch in word)
            if (n.Next == null || !n.Next.TryGetValue(ch, out n)) return false;
        return n.Word;
    }
}
