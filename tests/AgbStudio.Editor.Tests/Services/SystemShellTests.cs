using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Support;
using Avalonia.Headless.XUnit;

namespace AgbStudio.Editor.Tests.Services;

/// <summary>P10.17: Show in Explorer and the external editor launch (OS pickers need a desktop and are not run headless).</summary>
public class SystemShellTests
{
    [Fact]
    public void ShowInExplorer_SelectsFilesOpensFoldersRefusesMissing()
    {
        using var fx = new Fixture(withApp: false);
        var calls = new List<(string Exe, string Args)>();
        var shell = new SystemShell(() => null, (e, a) => calls.Add((e, a)));
        var file = Path.Combine(fx.Folder, "hero.png");
        File.WriteAllText(file, "");
        Assert.True(shell.ShowInExplorer(file));
        Assert.True(shell.ShowInExplorer(fx.Folder));
        Assert.False(shell.ShowInExplorer(Path.Combine(fx.Folder, "nope.png")));
        Assert.Equal([("explorer.exe", $"/select,\"{file}\""), ("explorer.exe", $"\"{fx.Folder}\"")], calls);
        shell.OpenExternally(@"C:\Tools\Code.exe", @"D:\p\PlayerController.cs", 19);
        Assert.Equal(("C:\\Tools\\Code.exe", "-g \"D:\\p\\PlayerController.cs:19\""), calls[^1]);
    }

    [AvaloniaFact]
    public void AssetsShowInExplorer_RevealsTheSelectionOrTheProject()
    {
        using var fx = new Fixture();
        var root = fx.OpenSkyHopper();
        var calls = new List<string>();
        var win = new MainWindow(fx.Services);
        fx.Services.System = new SystemShell(() => win, (_, a) => calls.Add(a));
        fx.Services.Commands.Execute("assets.showInExplorer");
        var sprite = Directory.EnumerateFiles(root, "player.png", SearchOption.AllDirectories).Single();
        fx.Services.System.Selection = () => sprite;
        fx.Services.Commands.Execute("assets.showInExplorer");
        Assert.Equal([$"\"{fx.Services.Project.Current!.Root}\"", $"/select,\"{sprite}\""], calls);
        win.Close();
    }
}
