using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Play;
using AgbStudio.Editor.Windows.Menus;
using AgbStudio.Editor.Windows.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>
/// P16.01 / G-ERR.05: without devkitPro the editor says where it looked and offers Preferences · Paths and the devkitPro
/// installer page (no download); a build that cannot find a tool names its path and offers Preferences · Paths.
/// </summary>
public class ToolchainHelpTests
{
    [Fact]
    public void SearchedFolders_AreThePathsFolder_ElseTheOnesToolchainDetectTries()
    {
        var p = new Preferences();
        Assert.Equal([@"C:\devkitPro", @"D:\devkitPro"], ToolchainHelp.SearchedFolders(p, _ => null));
        // the installer's MSYS2 name is C:\devkitPro; other MSYS2 paths name their drive
        Assert.Equal([@"C:\devkitPro", @"D:\devkitPro"], ToolchainHelp.SearchedFolders(p, _ => "/opt/devkitpro"));
        Assert.Equal([@"E:\tools\devkitPro", @"C:\devkitPro", @"D:\devkitPro"], ToolchainHelp.SearchedFolders(p, _ => "/e/tools/devkitPro"));
        Assert.Equal([@"F:\dkp", @"C:\devkitPro", @"D:\devkitPro"], ToolchainHelp.SearchedFolders(p, _ => @"F:\dkp"));
        p.DevkitPro = @"G:\devkitPro";
        Assert.Equal([@"G:\devkitPro"], ToolchainHelp.SearchedFolders(p, _ => @"F:\dkp"));
    }

    static Fixture Setup(bool noticeShown, out string empty, out List<string> urls)
    {
        var fx = new Fixture();
        empty = Path.Combine(fx.Folder, "no-devkitPro");
        Directory.CreateDirectory(empty);
        var folder = empty;
        fx.Services.Preferences.Update(p => { p.DevkitPro = folder; p.DevkitProNoticeShown = noticeShown; });
        var opened = new List<string>();
        urls = opened;
        fx.Services.Set(new EditorLinks { Config = new EditorConfig(), Open = opened.Add, Fetch = (_, _) => throw new InvalidOperationException("no network in tests") });
        return fx;
    }

    static ToolchainMissingDialog WaitForDialog(MainWindow win)
    {
        PlayTestSupport.WaitUntil(() => win.Overlays.Open.OfType<ToolchainMissingDialog>().Any(), 60);
        return win.Overlays.Open.OfType<ToolchainMissingDialog>().Single();
    }

    static void Press(Control dialog, string name)
    {
        dialog.GetVisualDescendants().OfType<Button>().First(b => b.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Capture.Pump();
    }

    [AvaloniaFact]
    public void FirstRun_WithoutDevkitPro_NamesTheFoldersSearched_InstallerPage_AndPaths()
    {
        using var fx = Setup(noticeShown: false, out var empty, out var urls);
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        try
        {
            win.Show();
            var d = WaitForDialog(win);
            Assert.Equal([empty], d.Lines);
            Assert.True(fx.Services.Preferences.Current.DevkitProNoticeShown);
            // the installer page opens in the browser; nothing is downloaded
            Press(d, "DevkitProInstaller");
            Assert.Equal(["https://devkitpro.org/wiki/Getting_Started"], urls);
            Press(d, "PART_Confirm");
            PlayTestSupport.WaitUntil(() => fx.Services.Windows.Requests.Contains(("T-S08", "ToolchainMissing")), 30);
            Assert.IsType<PreferencesDialog>(win.Overlays.Open.Single());
        }
        finally { win.Close(); }
        // shown once: the next start reads that from the preferences (Build_WithAToolMissing starts that way)
        Assert.True(new PreferencesStore(fx.Services.Preferences.FilePath).Current.DevkitProNoticeShown);
    }

    [AvaloniaFact]
    public void Build_WithAToolMissing_NamesItsPathAndOffersPaths()
    {
        using var fx = Setup(noticeShown: true, out var empty, out _);
        fx.OpenSkyHopper();
        var win = new MainWindow(fx.Services) { Width = 1440, Height = 900 };
        try
        {
            win.Show();
            var r = PlayTestSupport.Wait(fx.Services.Build.BuildAsync());
            Assert.False(r.Success);
            var gxx = Path.Combine(empty, "devkitARM", "bin", "arm-none-eabi-g++.exe");
            Assert.Contains(r.Errors, e => e.Message == $"arm-none-eabi-g++ is not at {gxx}. Set the toolchain folder in Preferences › Paths, or reinstall AGB Studio.");
            Assert.Contains(("arm-none-eabi-g++", gxx), fx.Services.Build.MissingTools);
            // the build's dialog only: the first-run notice was shown before
            var d = WaitForDialog(win);
            Assert.Equal("A build tool is missing", d.GetVisualDescendants().OfType<DialogHeader>().Single().Title);
            Assert.Contains($"arm-none-eabi-g++ · {gxx}", d.Lines);
            Press(d, "PART_Confirm");
            PlayTestSupport.WaitUntil(() => fx.Services.Windows.Requests.Contains(("T-S08", "ToolchainMissing")), 30);
        }
        finally { win.Close(); }
    }
}
