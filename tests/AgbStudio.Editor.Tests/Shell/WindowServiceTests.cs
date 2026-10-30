using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Capture = AgbStudio.Editor.Tests.Shot.Capture;

namespace AgbStudio.Editor.Tests.Shell;

// A Phase 11 style window, command, handler and menu declared in their own file; found by discovery.
[EditorWindow("T-D11")]
public sealed class TestAboutDialog : Border, IOverlayContent
{
    public WindowContext Context { get; }
    public event Action<object?>? Closed;
    public TestAboutDialog(WindowContext ctx) { Context = ctx; Width = 460; Height = 304; Name = "TestAbout"; }
    public void Finish(object? r) => Closed?.Invoke(r);
}

[EditorWindow("T-P08")]
public sealed class TestSpriteEditorPage : Panel { public TestSpriteEditorPage() => Name = "TestSpriteEditor"; }

public static class TestExtension
{
    public static int Built;
    public static int Handled;

    [EditorCommand("test.hello", "Hello", Keys = "Ctrl+Shift+H", Glyph = "star")]
    public static void Hello(EditorServices s) => Built++;

    [CommandHandler("build.clean")]
    public static void Clean() => Handled++;

    [EditorMenu("T-M12")]
    public static IReadOnlyList<MenuEntry> ActorMenu() => [new CommandEntry("edit.duplicate"), SeparatorEntry.Instance, new CommandEntry("edit.delete", Danger: true)];
}

/// <summary>The Phase 11 plug-in point: discovery of windows, commands and menus; "not built yet" for the rest.</summary>
public class WindowServiceTests
{
    [Fact]
    public void Catalog_KnowsEveryFigmaWindow()
    {
        Assert.Equal(26 + 24 + 9 + 13 + 24, WindowCatalog.All.Count);
        Assert.Equal(WindowKind.Popover, WindowCatalog.Find("T-O07")!.Kind);
        Assert.Equal((760, 390), (WindowCatalog.Find("T-D10")!.Width, WindowCatalog.Find("T-D10")!.Height));
    }

    [Fact]
    public void Discovery_FindsShellWindowsInTheEditor()
    {
        using var fx = new Fixture(withApp: false);
        foreach (var id in new[] { "T-P01", "T-O07", "T-D10", "T-D12" }) Assert.True(fx.Services.Windows.IsRegistered(id), id);
        Assert.False(fx.Services.Windows.IsRegistered("T-P08"));
    }

    [AvaloniaFact]
    public async Task Discovery_FindsWindowsCommandsHandlersAndMenusInExtensions()
    {
        using var fx = new Fixture2(typeof(TestExtension).Assembly);
        var s = fx.Services;
        Assert.True(s.Windows.IsRegistered("T-D11"));
        Assert.True(s.Windows.IsRegistered("T-M12"));
        Assert.Equal("Ctrl+Shift+H", AgbStudio.Editor.Theme.Shortcuts.Format(s.Shortcuts.GestureOf("test.hello")!));
        s.Commands.Execute("test.hello");
        s.Commands.Execute("build.clean");
        Assert.Equal((1, 1), (TestExtension.Built, TestExtension.Handled));

        var win = new MainWindow(s);
        win.Show();
        // Help › About AGB Studio now opens the registered dialog with its context
        s.Commands.Execute("help.about");
        Capture.Pump();
        var about = Assert.IsType<TestAboutDialog>(win.Overlays.Open.Single());
        Assert.Equal("About", about.Context.Info.Name);
        about.Finish("ok");
        Assert.False(win.Overlays.IsOpen);
        // a page replaces the workspace under the title bar
        await s.Windows.Open("T-P08", source: "test");
        Assert.IsType<TestSpriteEditorPage>(win.CurrentPage);
        Assert.Equal("T-P08", win.CurrentPageId);
        win.Close();
    }

    [AvaloniaFact]
    public void Open_UnbuiltWindowShowsNotice_NeverThrows()
    {
        using var fx = new Fixture();
        var win = new MainWindow(fx.Services);
        win.Show();
        _ = fx.Services.Windows.Open("T-P19", source: "test");
        Capture.Pump();
        var notice = Assert.IsType<NotBuiltNotice>(win.Overlays.Open.Single());
        Assert.Equal("T-P19", notice.Tag);
        _ = fx.Services.Windows.Open("T-X99");
        Capture.Pump();
        Assert.Equal(2, win.Overlays.Open.Count);
        win.Close();
    }

    [Fact]
    public void ServiceLocator_ExposesProjectBuildToolchainAndEmulatorHooks()
    {
        using var fx = new Fixture(withApp: false);
        var s = fx.Services;
        Assert.Same(s.Project, s.Get<AgbStudio.Editor.Services.ProjectService>());
        Assert.Same(s.Build, s.Get<AgbStudio.Editor.Services.BuildService>());
        Assert.Same(s.Toolchain, s.Get<AgbStudio.Editor.Services.ToolchainStatusService>());
        Assert.Same(s.Emulator, s.Get<AgbStudio.Editor.Services.EmulatorHost>());
        Assert.False(s.Emulator.IsLoaded);
        var mine = new object[] { 1 };
        s.Set(mine);
        Assert.Same(mine, s.Get<object[]>());
        fx.OpenSkyHopper();
        Assert.Equal("Sky Hopper", s.Project.Current!.Settings.Name);
        Assert.Null(s.Build.LoadReport());
    }
}

/// <summary>Fixture whose services also scan an extra assembly.</summary>
public class Fixture2 : IDisposable
{
    readonly string _folder = Directory.CreateTempSubdirectory("agb-editor-test-").FullName;
    public EditorServices Services { get; }
    public Fixture2(System.Reflection.Assembly extension) =>
        Services = EditorServices.Create(Avalonia.Application.Current, Path.Combine(_folder, "appdata"), new ManualTimer(), null, extension);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch (IOException) { } }
}
