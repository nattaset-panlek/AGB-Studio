using System.Text.RegularExpressions;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Content.Templates;
using AgbStudio.Build.Fonts;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>
/// P8.06 / G-THAI.02: Thai word wrap by dictionary at build time. Over a corpus (sentences written for this test, the
/// fixture's and the templates' Thai strings) at several box widths, every line break must fall on a word start found
/// by the ICU dictionary (or after a space, at a Thai / Latin change or a newline); the only exception is a single word
/// wider than the box. The line counts the build bakes into the ROM tables are the ones the Localization fit check shows.
/// </summary>
public sealed partial class ThaiWrapTests
{
    public static readonly string[] Sentences =
    [
        "ต้องรีบตามหาก่อนพระอาทิตย์ตก เหรียญหายไปไหนหมดนะ",
        "เจ้าสไลม์ขโมยเหรียญทองไปซ่อนไว้ในถ้ำลึกทางทิศเหนือของหมู่บ้าน",
        "ถ้าอยากข้ามสะพานไม้เก่า ต้องหากุญแจที่ซ่อนอยู่ใต้ต้นไม้ใหญ่ก่อน",
        "นกฮูกบอกว่าทางลัดไปยอดเขามีค้างคาวอาศัยอยู่เต็มไปหมด",
        "ร้านค้าของคุณป้าขายยาฟื้นพลังราคาถูกที่สุดในเมือง",
        "กระโดดให้สูงขึ้นอีกนิดแล้วจะเก็บดาวดวงสุดท้ายได้",
        "เมื่อแม่น้ำเอ่อล้น ชาวบ้านทุกคนต้องช่วยกันสร้างทำนบกั้นน้ำ",
        "ข้าเฝ้าประตูนี้มานานหลายร้อยปีแล้ว เจ้าคิดว่าจะผ่านไปได้ง่ายๆ หรือ",
        "อย่าลืมบันทึกเกมก่อนเข้าห้องของราชาสไลม์นะ",
        "หีบสมบัติใบนี้ถูกล็อกไว้ด้วยเวทมนตร์โบราณที่ไม่มีใครเคยแก้ได้",
        "พรุ่งนี้เช้าเราจะออกเดินทางไปยังเกาะที่อยู่กลางทะเลสาบ",
        "คะแนนสูงสุดของวันนี้ถูกบันทึกไว้เรียบร้อยแล้ว",
        "ลองกดปุ่มเอเพื่อกระโดด และกดปุ่มบีค้างไว้เพื่อวิ่งเร็วขึ้น",
        "ขอบคุณที่ช่วยหมู่บ้านของเราไว้ เจ้าคือวีรบุรุษตัวจริง",
        "ฝนตกหนักมากจนมองไม่เห็นทางเดินข้างหน้าเลย",
        "ราคา 120 บาทต่อชิ้น ซื้อสามชิ้นแถมฟรีอีกหนึ่งชิ้น",
        "เด็กๆ ในหมู่บ้านชอบเล่นซ่อนหาในป่าไผ่หลังบ้าน",
        "ระวังหนามแหลมบนพื้น ถ้าเหยียบเข้าไปจะเสียหัวใจหนึ่งดวง",
        "เสียงระฆังดังมาจากวัดบนเนินเขา แปลว่าใกล้ถึงเวลาเย็นแล้ว",
        "ฮอปเปอร์กับนกฮูกออกเดินทางตามหาเหรียญที่หายไปด้วยกัน",
        "การผจญภัยครั้งนี้ยังไม่จบ ยังมีความลับอีกมากมายรอให้ค้นพบ",
        "ถ้ำแห่งนี้มืดมาก ต้องจุดคบเพลิงก่อนจึงจะเดินต่อได้",
        "Level ๓: ป่าใหญ่ทางทิศตะวันออก - Forest of the East",
        "อัญมณีสีฟ้าเม็ดนี้มีพลังลึกลับซ่อนอยู่ข้างใน\nเก็บไว้ให้ดีนะ",
    ];

    static readonly int[] Widths = [64, 96, 128, 160, 224];

    static GameFont Font(IEnumerable<string> corpus)
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts");
        var f = new FontFile { Name = "thai_vwf", Source = "NotoSansThai-Regular.ttf", Size = 12, CharacterSets = { "Basic Latin", "Thai", "Symbols" }, LineHeight = 14, Baseline = 10, Spacing = 1, Bpp = 2, DropShadow = true };
        return FontBuilder.Build(f, dir, ThaiText.CodePoints(string.Concat(corpus)));
    }

    /// <summary>Where a line may start, worked out from the text and the dictionary alone: after a space or newline, at a
    /// change between Thai and other text, and at each dictionary word start of a Thai run (ฯ and ๆ end a run and never
    /// start a line).</summary>
    public static HashSet<int> WordStarts(string text)
    {
        var set = new HashSet<int> { 0, text.Length };
        for (var i = 1; i < text.Length; i++)
        {
            if (ThaiText.IsSpace(text[i - 1]) || text[i - 1] == '\n') set.Add(i);
            if (ThaiText.IsThai(text[i - 1]) != ThaiText.IsThai(text[i]) && !ThaiText.IsSpace(text[i]) && !ThaiText.IsMark(text[i])) set.Add(i);
        }
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            var thai = i < text.Length && ThaiText.IsThai(text[i]) && text[i] is not ('ฯ' or 'ๆ');
            if (thai) continue;
            if (i > start)
                foreach (var w in ThaiText.WordStarts(text[start..i])) set.Add(start + w);
            if (i < text.Length && text[i] is 'ฯ' or 'ๆ') set.Add(i + 1);
            start = i + 1;
        }
        return set;
    }

    /// <summary>The text offset each line after the first starts at, read back from the placed glyphs.</summary>
    static List<int> LineStarts(string text, TextLayout.Result layout)
    {
        var starts = new List<int>();
        var pos = 0;
        for (var line = 0; line < layout.Lines; line++)
        {
            var chars = layout.Glyphs.Where(g => g.Line == line).Select(g => (char)g.CodePoint).ToList();
            if (line > 0)
            {
                while (pos < text.Length && (ThaiText.IsSpace(text[pos]) || text[pos] == '\n')) pos++;
                if (chars.Count > 0) starts.Add(pos);
            }
            foreach (var ch in chars)
            {
                while (pos < text.Length && text[pos] == '\n') pos++;
                Assert.True(pos < text.Length && text[pos] == ch, $"'{text}': line {line} does not follow the text at {pos}");
                pos++;
            }
        }
        return starts;
    }

    static List<string> Corpus()
    {
        var corpus = new List<string>(Sentences);
        var root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "thai_wrap");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        var projects = new List<GameProject> { SkyHopper.Create(Path.Combine(root, "fixture")) };
        projects.AddRange(TemplateCatalog.All.Select(t => t.Create(Path.Combine(root, t.Id), t.Name)));
        foreach (var p in projects)
            foreach (var f in p.StringTables())
                corpus.AddRange(GameProject.Load<StringTable>(f).Strings.Values.Where(s => s.Any(c => ThaiText.IsThai(c))));
        return corpus.Distinct().ToList();
    }

    [Fact]
    public void EveryLineBreakFallsOnADictionaryWordStart()
    {
        var corpus = Corpus();
        var font = Font(corpus);
        var problems = new List<string>();
        var breaks = 0;
        var forced = 0;
        foreach (var text in corpus)
        {
            var starts = WordStarts(text);
            foreach (var width in Widths)
            {
                var layout = TextLayout.Layout(text, font, width);
                foreach (var b in LineStarts(text, layout))
                {
                    breaks++;
                    if (starts.Contains(b)) continue;
                    // Inside a word: allowed only when that word alone is wider than the box.
                    int from = starts.Where(s => s < b).Max(), to = starts.Where(s => s > b).Min();
                    if (TextLayout.Layout(text[from..to].Trim(), font, 10000).MaxWidth > width)
                    {
                        forced++;
                        continue;
                    }
                    problems.Add($"{width} px: '{text[from..b]}|{text[b..to]}' in '{text}'");
                }
                // The Localization fit check uses the same layout: same lines and widths.
                var fit = TextLayout.Fit(text, font, width, 3);
                Assert.Equal((layout.Lines, layout.MaxWidth), (fit.Lines, fit.Width));
                Assert.Equal(fit.Fits, layout.Lines <= 3 && layout.MaxWidth <= width);
            }
        }
        Assert.True(problems.Count == 0, $"{problems.Count} breaks split a word:\n" + string.Join("\n", problems.Take(30)));
        Assert.True(breaks > 300, $"only {breaks} line breaks in the corpus");
        Assert.True(forced < breaks / 20, $"{forced} of {breaks} breaks were inside words wider than the box");
    }

    [Theory]
    [InlineData("ต้องรีบตามหาก่อนพระอาทิตย์ตก", 120, "ต้องรีบตามหาก่อน|พระอาทิตย์ตก")]
    [InlineData("ฮอปเปอร์กับนกฮูกออกเดินทาง", 96, "ฮอปเปอร์กับนกฮูก|ออกเดินทาง")]
    [InlineData("เด็กๆ ในหมู่บ้านชอบเล่นซ่อนหา", 96, "เด็กๆ ในหมู่บ้านชอบ|เล่นซ่อนหา")]
    public void KnownSentencesWrapAtTheirWords(string text, int width, string expected)
    {
        var font = Font([text]);
        var layout = TextLayout.Layout(text, font, width);
        var lines = Enumerable.Range(0, layout.Lines).Select(l => string.Concat(layout.Glyphs.Where(g => g.Line == l).Select(g => (char)g.CodePoint)).Trim());
        Assert.Equal(expected, string.Join("|", lines));
    }

    [Fact]
    public void UnknownNamesAreNotSplitLetterByLetter()
    {
        // "ฮอปเปอร์" (Hopper) is not in the dictionary. Its unknown letters "ฮอป" stay one segment instead of three; the
        // dictionary word "เปอร์" after them is a word of its own.
        Assert.False(ThaiText.InDictionary("ฮอปเปอร์"));
        Assert.Equal([0, 3, 8, 11, 13], ThaiText.WordStarts("ฮอปเปอร์กับนกฮูก"));  // ฮอป|เปอร์|กับ|นก|ฮูก
    }

    [GeneratedRegex(@"const agb::data::TextRun r(\d+) = \{&\w+, r\d+g, \d+, (\d+), \d+\};")]
    private static partial Regex RunLine();

    [Fact]
    public void BuiltTextRunsHaveTheLinesTheFitCheckShows()
    {
        var p = SkyHopper.Create(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "thai_wrap_build"));
        var th = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.th.json"));
        th.Strings["dlg.forest.hopper_02"] = Sentences[1];  // long enough to need three lines in a 224 px box
        th.Strings["dlg.forest.owl_01"] = Sentences[7];
        GameProject.Save(Path.Combine(p.TextDir, "strings.th.json"), th);
        var b = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() };
        var r = b.Build(new BuildOptions { GenerateOnly = true });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var cpp = File.ReadAllText(Path.Combine(p.BuildDir, "gen", "text", "strings_th.cpp"));
        var lines = RunLine().Matches(cpp).ToDictionary(m => int.Parse(m.Groups[1].Value), m => int.Parse(m.Groups[2].Value));
        var meta = GameProject.Load<StringMeta>(Path.Combine(p.TextDir, "strings.meta.json"));
        var all = string.Concat(Directory.EnumerateFiles(p.TextDir, "strings.*.json").Where(f => !f.EndsWith("meta.json")).SelectMany(f => GameProject.Load<StringTable>(f).Strings.Values))
                  + string.Concat(p.Settings.Localization.Languages.Select(l => l.Name));
        var font = FontBuilder.Build(GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font")), p.FontsDir, ThaiText.CodePoints(all).Where(c => c >= 0x20).Distinct());
        var multi = 0;
        foreach (var (key, text) in th.Strings)
        {
            var k = b.StringKey(key);
            var info = meta.Keys.GetValueOrDefault(key) ?? new StringInfo();
            var fit = TextLayout.Fit(text, font, info.BoxWidth, info.MaxLines);
            Assert.True(lines.ContainsKey(k), key);
            Assert.Equal(Math.Max(1, fit.Lines), lines[k]);
            if (fit.Lines > 1) multi++;
        }
        Assert.True(multi >= 2, $"{multi} Thai strings wrap");
    }
}
