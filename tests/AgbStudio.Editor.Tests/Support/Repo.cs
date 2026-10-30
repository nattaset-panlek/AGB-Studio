using System.Runtime.CompilerServices;

namespace AgbStudio.Editor.Tests.Support;

/// <summary>Paths inside the repository, found from this source file's location.</summary>
public static class Repo
{
    public static string Root { get; } = FindRoot();

    static string FindRoot([CallerFilePath] string here = "")
    {
        var dir = Path.GetDirectoryName(here)!;
        while (!File.Exists(Path.Combine(dir, "AGB-Studio.sln"))) dir = Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("AGB-Studio.sln not found");
        return dir;
    }

    public static string Path_(params string[] parts) => Path.Combine([Root, .. parts]);
    public static string Editor => Path_("editor", "AgbStudio.Editor");
    public static string Evidence => Path_("docs", "evidence");
    public static string FigmaEvidence => Path_("docs", "evidence", "figma");

    /// <summary>Set AGB_REGEN=1 to rewrite generated files from the Figma export instead of only checking them.</summary>
    public static bool Regenerate => Environment.GetEnvironmentVariable("AGB_REGEN") == "1";
}
