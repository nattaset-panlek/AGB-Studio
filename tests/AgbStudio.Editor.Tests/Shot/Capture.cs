using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AgbStudio.Editor.Tests.Shot;

/// <summary>SHOT step 4 (checklist 2.5): renders a control or window headless at 100% DPI and saves a PNG.</summary>
public static class Capture
{
    /// <summary>Hosts <paramref name="content"/> in a borderless window of exactly <paramref name="width"/> x <paramref name="height"/>.</summary>
    public static Window Host(Control content, double width, double height, IBrush? background = null, ThemeVariant? theme = null)
    {
        var w = new Window
        {
            Width = width, Height = height, SizeToContent = SizeToContent.Manual,
            SystemDecorations = SystemDecorations.None, Background = background ?? Brushes.Transparent,
            Content = content, RequestedThemeVariant = theme ?? ThemeVariant.Dark,
        };
        RenderOptions.SetTextRenderingMode(w, TextRenderingMode.Antialias);
        w.Show();
        Pump();
        return w;
    }

    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public static WriteableBitmap Frame(TopLevel top)
    {
        Pump();
        return top.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered (UseHeadlessDrawing must be false)");
    }

    /// <summary>Renders a control alone at the given size into <paramref name="path"/>.</summary>
    public static string ControlToPng(Control content, double width, double height, string path, IBrush? background = null, ThemeVariant? theme = null)
    {
        var w = Host(content, width, height, background, theme);
        try { Save(Frame(w), path); }
        finally { w.Close(); }
        return path;
    }

    public static void Save(Bitmap bmp, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bmp.Save(path);
    }
}
