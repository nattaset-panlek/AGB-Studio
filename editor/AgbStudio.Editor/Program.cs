using Avalonia;
using Avalonia.Media;

namespace AgbStudio.Editor;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--mcp"))
        {
            var mcpArgs = args.Where(a => a != "--mcp").ToArray();
            AgbStudio.Mcp.Program.Main(mcpArgs).GetAwaiter().GetResult();
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Windows only (D-10). Popups are drawn in the window's overlay layer so menus and popovers share its theme and capture.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseWin32()
        .UseSkia()
        .With(new Win32PlatformOptions { OverlayPopups = true })
        .ConfigureAgbFonts()
        .LogToTrace();

    public static AppBuilder ConfigureAgbFonts(this AppBuilder builder) =>
        builder.With(new FontManagerOptions { DefaultFamilyName = Theme.Fonts.InterUri, FontFallbacks = [new FontFallback { FontFamily = Theme.Fonts.ThaiFamily }] });
}
