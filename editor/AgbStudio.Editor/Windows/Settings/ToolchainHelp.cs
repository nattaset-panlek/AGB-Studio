using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Menus;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>
/// devkitPro help (P16.01, G-ERR.05). The first time the editor starts without devkitPro it says where it looked and
/// points to Preferences · Paths and the devkitPro installer page (nothing is downloaded). A build that cannot start
/// because a tool is not at its path names the path and offers Preferences · Paths.
/// </summary>
public static class ToolchainHelp
{
    /// <summary>
    /// The folders searched for devkitPro: the folder set in Preferences · Paths, else as Toolchain.Detect looks (the
    /// DEVKITPRO variable, its MSYS2 form /opt/devkitpro meaning C:\devkitPro, then C:\devkitPro and D:\devkitPro).
    /// </summary>
    public static IReadOnlyList<string> SearchedFolders(Preferences p, Func<string, string?>? env = null)
    {
        if (p.DevkitProOrNull() is { } set) return [set];
        var list = new List<string>();
        var value = (env ?? Environment.GetEnvironmentVariable)("DEVKITPRO");
        if (!string.IsNullOrEmpty(value))
        {
            // "/c/devkitPro" is C:\devkitPro; the installer's "/opt/devkitpro" is C:\devkitPro too, listed below
            if (value.StartsWith('/') && value.Length > 2 && value[2] == '/') list.Add($"{char.ToUpperInvariant(value[1])}:{value[2..].Replace('/', '\\')}");
            else if (!value.StartsWith('/')) list.Add(value);
        }
        foreach (var d in new[] { @"C:\devkitPro", @"D:\devkitPro" })
            if (!list.Contains(d, StringComparer.OrdinalIgnoreCase)) list.Add(d);
        return list;
    }

    [EditorStartup]
    public static void Startup(EditorServices s)
    {
        // the first toolchain check of the session (the main window starts it): the notice is shown once per install
        void First()
        {
            if (s.Toolchain.Tools.Count == 0) return;
            s.Toolchain.Changed -= First;
            Dispatcher.UIThread.Post(() =>
            {
                if (s.Toolchain.Find("compiler") is not { Found: false } || s.Preferences.Current.DevkitProNoticeShown) return;
                s.Preferences.Update(p => p.DevkitProNoticeShown = true);
                _ = Show(s, new ToolchainMissingDialog(s, null));
            });
        }
        s.Toolchain.Changed += First;
        s.Build.Finished += r =>
        {
            if (r.Success || s.Build.MissingTools.Count == 0) return;
            var missing = s.Build.MissingTools;
            Dispatcher.UIThread.Post(() => _ = Show(s, new ToolchainMissingDialog(s, missing)));
        };
    }

    static async Task Show(EditorServices s, ToolchainMissingDialog d)
    {
        if (s.Windows.Host is not { } host) return;
        if (await host.Overlays.Show(d) as string == "paths") _ = s.Windows.Open("T-S08", source: "ToolchainMissing");
    }
}

/// <summary>
/// "devkitPro not found" (P16.01) or "A build tool is missing" (G-ERR.05): the folders searched or the missing paths,
/// the devkitPro installer page (opened in the browser; nothing is downloaded) and Preferences · Paths. Figma has no
/// frame for it; it is built from the Dialog Header / Dialog Footer components like Delete project. Result "paths" or null.
/// </summary>
public sealed class ToolchainMissingDialog : FigmaDialog
{
    public IReadOnlyList<string> Lines { get; }

    /// <param name="missing">The tools a build could not find, or null for the first-run notice (the folders searched).</param>
    public ToolchainMissingDialog(EditorServices s, IReadOnlyList<(string Tool, string Path)>? missing)
    {
        Name = "ToolchainMissingDialog";
        var first = missing is null;
        Lines = first ? ToolchainHelp.SearchedFolders(s.Preferences.Current) : missing!.Select(m => $"{m.Tool} · {m.Path}").ToList();
        var message = UI.Text(first
            ? "AGB Studio builds games with devkitPro (devkitARM, libtonc and maxmod). It was not found in these folders:"
            : "The build could not start because these tools are not where the devkitPro folder says:", "Body/12", "text/secondary");
        message.TextWrapping = TextWrapping.Wrap;
        var list = UI.Column(4, Lines.Select(l =>
        {
            var t = UI.Text(l, "Mono/11", "text/primary");
            t.TextWrapping = TextWrapping.Wrap;
            return (Control)t;
        }).ToArray());
        var row = new DockPanel { HorizontalSpacing = 8 };
        var icon = new Icon { Glyph = "folder", Size = 14, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) }.Token(Icon.ForegroundProperty, "text/secondary");
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.AddRange([icon, list]);
        var hint = UI.Text("Install devkitPro with its installer, or set the folder it is in under Preferences › Paths.", "Label/11", "text/muted");
        hint.TextWrapping = TextWrapping.Wrap;
        var links = EditorLinks.Of(s);
        var installer = new AgbButton
        {
            Kind = ButtonKind.Secondary, Glyph = "link", Content = "devkitPro installer page", Name = "DevkitProInstaller",
            HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = links.Config.DevkitProUrl.Trim().Length > 0,
        };
        installer.Click += (_, _) => links.Open(links.Config.DevkitProUrl.Trim());
        var content = UI.Column(14, message, UI.Frame(row, "bg/base", pad: new Thickness(10, 8), radius: 6), hint, installer);
        content.Margin = new Thickness(20);
        var footer = new DialogFooter { CancelText = "Close", ConfirmText = "Open Preferences · Paths", Name = "ToolchainFooter" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => Close("paths");
        Build(first ? "devkitPro not found" : "A build tool is missing", "warn", content, footer, 500);
    }
}
