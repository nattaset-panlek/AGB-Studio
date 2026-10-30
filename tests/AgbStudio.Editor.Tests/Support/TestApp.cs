using AgbStudio.Editor;
using AgbStudio.Editor.Tests.Support;
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AgbStudio.Editor.Tests.Support;

/// <summary>Headless Avalonia with real Skia rendering, so windows can be captured to PNG (SHOT, E2E).</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .ConfigureAgbFonts();
}
