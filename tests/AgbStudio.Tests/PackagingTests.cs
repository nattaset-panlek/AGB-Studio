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
}
