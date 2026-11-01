using System.Text.RegularExpressions;
using AgbStudio.Editor.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// Running AGBStudio.exe straight from a zip (Windows Explorer "Run" without extracting) starts it from a temporary copy
/// such as %TEMP%\Temp1_AGBStudio.zip\: the bundled toolchain and the third_party files are missing or half copied, and
/// the folder is deleted later. The editor says so at once instead of failing later in a build or an export.
/// </summary>
public static partial class ZipLaunch
{
    // Explorer: Temp1_name.zip; 7-Zip: 7zO1234ABCD; WinRAR: Rar$EXa1234.5678
    [GeneratedRegex(@"^(Temp\d+_.+\.zip|7z[A-Za-z0-9]+|Rar\$EX[A-Za-z0-9.]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ArchiveFolder();

    /// <summary>The zip the editor runs from (its name), or null when it runs from a normal folder.</summary>
    public static string? Detect(string baseDirectory, string tempDirectory)
    {
        var dir = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var temp = Path.GetFullPath(tempDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!dir.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
        foreach (var part in dir[(temp.Length + 1)..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (!ArchiveFolder().IsMatch(part)) continue;
            var m = Regex.Match(part, @"^Temp\d+_(.+\.zip)$", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "the archive";
        }
        return null;
    }

    /// <summary>For the running editor: the zip it runs from, or null.</summary>
    public static string? Current() => Detect(AppContext.BaseDirectory, Path.GetTempPath());
}

/// <summary>"Extract AGB Studio first": shown at start when the editor runs from inside a zip. Close quits, Continue stays.</summary>
public sealed class ZipLaunchDialog : FigmaDialog
{
    public string Message { get; }

    public ZipLaunchDialog(string zip)
    {
        Name = "ZipLaunchDialog";
        Message = $"AGB Studio is running from inside {zip}. Windows started it from a temporary folder, so the toolchain and " +
                  "other bundled files are missing, and Windows may delete the folder while you work. Close AGB Studio, " +
                  $"right-click {zip} and choose Extract All…, then start AGBStudio.exe from the extracted folder.";
        var text = UI.Text(Message, "Body/12", "text/secondary");
        text.TextWrapping = TextWrapping.Wrap;
        text.Margin = new Thickness(20);
        var footer = new DialogFooter { CancelText = "Continue anyway", ConfirmText = "Close AGB Studio", ConfirmKind = ButtonKind.Primary };
        footer.Cancel += (_, _) => Close("continue");
        footer.Confirm += (_, _) => Close("quit");
        Build("Extract AGB Studio first", "warn", text, footer, 520);
    }
}
