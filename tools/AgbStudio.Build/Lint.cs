using System.Text.RegularExpressions;

namespace AgbStudio.Build;

public sealed record LintIssue(string Rule, string File, int Line, string Text);

/// <summary>Repository rules checked in CI (P1.07): no emoji anywhere (user rule, G-EMOJI); runtime code writes
/// hardware only through the commit queues (P2.04); no float / double in runtime C++ (no FPU).</summary>
public static class Lint
{
    // Modules allowed to touch hardware registers directly.
    static readonly string[] HardwareAllowed =
    [
        "core/commit.cpp", "core/system.cpp", "core/save.cpp", "core/bios.cpp", "core/assert.cpp", "core/input.cpp",
        "render/effects.cpp", "audio/audio.cpp",
    ];

    static readonly Regex HardwareWrite = new(@"\b(REG_[A-Z0-9_]+|pal_bg_mem|pal_obj_mem|oam_mem|vid_mem|tile_mem|se_mem)\s*(\[[^\]]*\])?(\.\w+)?\s*(=|\|=|&=)(?!=)", RegexOptions.Compiled);
    static readonly Regex FloatUse = new(@"\b(float|double)\b", RegexOptions.Compiled);

    public static bool IsEmoji(int cp) =>
        cp is >= 0x1F000 and <= 0x1FAFF or >= 0x2600 and <= 0x27BF or 0xFE0F or >= 0x1F1E6 and <= 0x1F1FF or 0x200D
        or >= 0x2B00 and <= 0x2BFF or >= 0x2300 and <= 0x23FF;

    public static List<LintIssue> Run(string root)
    {
        var issues = new List<LintIssue>();
        var textExt = new[] { ".cs", ".cpp", ".hpp", ".h", ".def", ".md", ".json", ".axaml", ".xaml", ".csproj", ".props", ".txt", ".ps1", ".cmd" };
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.StartsWith("build/") || rel.StartsWith(".git/") || rel.Contains("/bin/") || rel.Contains("/obj/") || rel.StartsWith("third_party/")) continue;
            if (!textExt.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                for (var k = 0; k < line.Length; k++)
                {
                    var cp = Utf32At(line, k);
                    if (cp > 0xFFFF) k++;
                    if (IsEmoji(cp)) { issues.Add(new LintIssue("no-emoji", rel, i + 1, $"U+{cp:X4}")); break; }
                }
                if (!rel.StartsWith("runtime/src/") && !rel.StartsWith("runtime/include/")) continue;
                var code = StripComments(line);
                if (rel.EndsWith(".cpp") && !HardwareAllowed.Any(a => rel.EndsWith(a)) && HardwareWrite.IsMatch(code))
                    issues.Add(new LintIssue("hardware-through-commit", rel, i + 1, code.Trim()));
                if (FloatUse.IsMatch(code) && !code.Contains("long double"))
                    issues.Add(new LintIssue("no-float", rel, i + 1, code.Trim()));
            }
        }
        return issues;
    }

    static string StripComments(string line)
    {
        var c = line.IndexOf("//", StringComparison.Ordinal);
        var s = c >= 0 ? line[..c] : line;
        return Regex.Replace(s, "\"(?:\\\\.|[^\"\\\\])*\"", "\"\"");
    }

    static int Utf32At(string s, int i) =>
        char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? char.ConvertToUtf32(s[i], s[i + 1]) : s[i];
}
