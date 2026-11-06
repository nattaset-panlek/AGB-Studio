using System.IO.Compression;
using System.Reflection;

namespace AgbStudio.Build;

/// <summary>
/// The Windows package (D-19): a self-contained editor in <c>bin/</c> plus what it needs at run time (the runtime
/// sources games are built from, fonts, the Thai word list, the emulator core, the user docs), zipped as a portable
/// folder. Deleting the folder uninstalls it; projects live elsewhere and are never touched.
/// </summary>
public static class Packager
{
    /// <summary>File at the root of an installed copy; NativeBuild.RepoRoot finds it like the repository's .sln.</summary>
    public const string RootMarker = "agb-studio.root";

    public static string Version => typeof(Packager).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Builds the package folder and zip in <paramref name="outDir"/>; returns the zip path.</summary>
    public static string Package(string outDir, Action<string>? log = null, bool publish = true)
    {
        log ??= _ => { };
        var root = NativeBuild.RepoRoot;
        var name = $"AGB-Studio-{Version}-win-x64";
        var stage = Path.Combine(outDir, name);
        if (Directory.Exists(stage)) Directory.Delete(stage, true);
        Directory.CreateDirectory(stage);
        if (publish)
        {
            log("Publishing the editor (self-contained, win-x64)...");
            var csproj = Path.Combine(root, "editor", "AgbStudio.Editor", "AgbStudio.Editor.csproj");
            var r = Proc.Run("dotnet", $"publish \"{csproj}\" -c Release -r win-x64 --self-contained true -nologo -v q -o \"{Path.Combine(stage, "bin")}\"",
                root, TimeSpan.FromMinutes(15), log);
            if (r.ExitCode != 0)
                throw new InvalidOperationException("dotnet publish could not build the editor (its output is above), so no package was made. " +
                    "Build the solution with dotnet build to see the first error, fix it, then package again.");
        }
        log("Copying the runtime, fonts, word list, emulator core and docs...");
        CopyTree(Path.Combine(root, "runtime", "include"), Path.Combine(stage, "runtime", "include"));
        CopyTree(Path.Combine(root, "runtime", "src"), Path.Combine(stage, "runtime", "src"));
        CopyTree(Path.Combine(root, "runtime", "link"), Path.Combine(stage, "runtime", "link"));
        foreach (var d in new[] { "fonts", "icu", "mgba", "maxmod" })
            CopyTree(Path.Combine(root, "third_party", d), Path.Combine(stage, "third_party", d));
        CopyTree(Path.Combine(root, "docs", "user"), Path.Combine(stage, "docs", "user"));
        // T-R03: the banners and icons for the release page (the release notes show the hero banner).
        CopyTree(Path.Combine(root, "docs", "brand"), Path.Combine(stage, "docs", "brand"));
        // P24.07: AGB Studio's own compiler (tools/fetch-toolchain.ps1 makes it), so a game builds with nothing else installed.
        var toolchain = Path.Combine(root, "toolchain");
        if (File.Exists(Path.Combine(toolchain, "bin", "arm-none-eabi-g++.exe")))
        {
            log("Copying the toolchain (Arm GNU Toolchain)...");
            CopyTree(toolchain, Path.Combine(stage, "toolchain"));
        }
        else log("No toolchain\\ in the repository: the package needs devkitPro to build games. Run tools\\fetch-toolchain.ps1 first.");
        foreach (var f in new[] { "THIRD_PARTY.md", "RELEASE-NOTES.md" })
            File.Copy(Path.Combine(root, "docs", f), Path.Combine(stage, "docs", f), true);
        // the soundbank writer is ported from mmutil (BSD 3-clause): its notice goes with the program
        File.Copy(Path.Combine(root, "tools", "AgbStudio.Build", "Audio", "Maxmod", "LICENSE-mmutil.txt"), Path.Combine(stage, "docs", "LICENSE-mmutil.txt"), true);
        File.WriteAllText(Path.Combine(stage, RootMarker), Version + "\n");
        var bin = Path.Combine(stage, "bin");
        Directory.CreateDirectory(bin);
        // The Launcher's "What's new" reads release-notes.md next to AGBStudio.exe (P16.05).
        File.Copy(Path.Combine(root, "docs", "RELEASE-NOTES.md"), Path.Combine(bin, "release-notes.md"), true);
        // D-13: no online service is configured out of the box; editing this file points Help at real addresses.
        File.WriteAllText(Path.Combine(bin, "editor.json"),
            "{\n  \"websiteUrl\": \"\",\n  \"bugUrl\": \"\",\n  \"updateFeed\": \"\",\n  \"communityUrl\": \"\",\n  \"releaseNotesUrl\": \"\"\n}\n");
        File.WriteAllText(Path.Combine(stage, "README.txt"),
            $"AGB Studio {Version}\r\n\r\nRun bin\\AGBStudio.exe. Games are built with the compiler in toolchain\\ (Arm GNU Toolchain,\r\n" +
            "GPL; its source code is at https://developer.arm.com/downloads/-/arm-gnu-toolchain-downloads). A devkitPro folder\r\n" +
            "set in Preferences > Paths is used instead when you choose one.\r\n" +
            "To uninstall, delete this folder; your projects are not inside it.\r\n" +
            "This build is not code-signed, so Windows may ask before the first start.\r\n");
        var zip = Path.Combine(outDir, name + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        log($"Writing {zip}...");
        ZipFile.CreateFromDirectory(stage, zip, CompressionLevel.Optimal, includeBaseDirectory: true);
        return zip;
    }

    static void CopyTree(string from, string to)
    {
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), true);
    }
}
