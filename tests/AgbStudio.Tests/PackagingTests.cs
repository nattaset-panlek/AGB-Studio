using System.IO.Compression;
using AgbStudio.Build;

namespace AgbStudio.Tests;

/// <summary>P16.01: the portable package (D-19) holds what an installed editor needs and finds its own root.</summary>
public sealed class PackagingTests
{
    [Fact]
    public void PackageHoldsTheRuntimeFontsCoreAndDocsAndFindsItsRoot()
    {
        var outDir = Path.Combine(NativeBuild.RepoRoot, "build", "test-package");
        // The editor publish takes minutes; `agb package` runs it. Here the layout around it is checked.
        var zip = Packager.Package(outDir, publish: false);
        var stage = Path.Combine(outDir, Path.GetFileNameWithoutExtension(zip));
        Assert.Equal($"AGB-Studio-{Packager.Version}-win-x64.zip", Path.GetFileName(zip));
        foreach (var f in new[]
                 {
                     Packager.RootMarker, "README.txt", "bin/editor.json", "bin/release-notes.md", "runtime/include/agb/game.hpp", "runtime/src/framework/game.cpp",
                     "third_party/mgba/mgba_libretro.dll", "third_party/mgba/LICENSE-mgba.txt", "third_party/fonts/NotoSansThai-Regular.ttf",
                     "third_party/icu/thaidict.txt", "docs/THIRD_PARTY.md", "docs/RELEASE-NOTES.md", "docs/user/gba-hardware-guide.md",
                     "docs/brand/banner-hero-1600-560.png", "docs/brand/banner-github-1280-320.png", "docs/brand/banner-itch-io-630-500.png",
                     "docs/brand/banner-social-1280-640.png", "docs/brand/agb-studio.ico",
                 })
            Assert.True(File.Exists(Path.Combine(stage, f)), f + " is missing");
        // Every runtime source a game build compiles is in the package.
        Assert.Equal(NativeBuild.RuntimeSources().Count(), NativeBuild.RuntimeSources(Path.Combine(stage, "runtime")).Count());
        Assert.False(Directory.Exists(Path.Combine(stage, "runtime", "tests")));
        // An installed editor in bin/ resolves the package folder as its root.
        Assert.Equal(Path.GetFullPath(stage), NativeBuild.FindRoot(Path.Combine(stage, "bin")));
        using var z = ZipFile.OpenRead(zip);
        Assert.Contains(z.Entries, e => e.FullName.Replace('\\', '/').EndsWith("/" + Packager.RootMarker, StringComparison.Ordinal));
        // No emoji or brand slips in the shipped text (G-EMOJI, G-LEGAL.03).
        Assert.DoesNotContain(Lint.Run(Path.Combine(stage, "docs")), i => i.Rule is "no-emoji" or "product-name");
    }

    [Fact]
    public void EveryShippedLibraryHasALicenceRow()
    {
        // P16.02 / G-LEGAL.04: each DLL next to AGBStudio.exe (the package's bin/ is the editor build plus the .NET runtime)
        // belongs to a component listed in docs/THIRD_PARTY.md.
        var bin = Path.Combine(NativeBuild.RepoRoot, "editor", "AgbStudio.Editor", "bin", "Release", "net10.0");
        Assert.True(Directory.Exists(bin), "build the solution (Release) first");
        var notices = File.ReadAllText(Path.Combine(NativeBuild.RepoRoot, "docs", "THIRD_PARTY.md"));
        string? Row(string dll) => dll switch
        {
            _ when dll.StartsWith("AgbStudio", StringComparison.OrdinalIgnoreCase) || dll == "AGBStudio" => "",  // AGB Studio itself (MIT, LICENSE)
            _ when dll.StartsWith("Avalonia", StringComparison.Ordinal) => "Avalonia",
            _ when dll.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) => "Roslyn",
            _ when dll.StartsWith("System.Composition", StringComparison.Ordinal) => "System.Composition",
            _ when dll.StartsWith("Microsoft.DiaSymReader", StringComparison.Ordinal) => "DiaSymReader",
            _ when dll.StartsWith("System.", StringComparison.Ordinal) || dll.StartsWith("Microsoft.", StringComparison.Ordinal) => ".NET runtime",
            "SkiaSharp" => "SkiaSharp",
            "libSkiaSharp" => "Skia",
            "HarfBuzzSharp" => "HarfBuzzSharp",
            "libHarfBuzzSharp" => "HarfBuzz",
            "Humanizer" => "Humanizer",
            "MicroCom.Runtime" => "MicroCom",
            "av_libglesv2" => "ANGLE",
            "mgba_libretro" => "mGBA",
            "msquic" => "MsQuic",
            // the .NET runtime of a self-contained publish (bin/Release/net10.0/win-x64/publish)
            _ when dll.StartsWith("clr", StringComparison.Ordinal) || dll.StartsWith("mscor", StringComparison.Ordinal) || dll is "coreclr" or "hostfxr" or "hostpolicy" or "netstandard" or "System" or "WindowsBase" => ".NET runtime",
            _ => null,
        };
        var dlls = Directory.EnumerateFiles(bin, "*.dll", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "runtimes" + Path.DirectorySeparatorChar) || f.Contains("win-x64"))
            .Select(f => Path.GetFileNameWithoutExtension(f)).Distinct().ToList();
        Assert.NotEmpty(dlls);
        var unknown = dlls.Where(d => Row(d) == null).ToList();
        Assert.True(unknown.Count == 0, "no THIRD_PARTY.md row for: " + string.Join(", ", unknown));
        var missing = dlls.Select(Row).Where(r => r is { Length: > 0 }).Distinct().Where(r => !notices.Contains(r!, StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "THIRD_PARTY.md lacks: " + string.Join(", ", missing));
    }
}
