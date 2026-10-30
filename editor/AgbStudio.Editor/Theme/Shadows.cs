using Avalonia.Media;

namespace AgbStudio.Editor.Theme;

/// <summary>Figma effect styles Shadow/Popover and Shadow/Dialog, and the Toast drop shadow.</summary>
public static class Shadows
{
    public static readonly BoxShadows Popover = BoxShadows.Parse(Format(8, 24, 0.45));
    public static readonly BoxShadows Dialog = BoxShadows.Parse(Format(24, 64, 0.6));
    public static readonly BoxShadows Toast = BoxShadows.Parse(Format(10, 28, 0.5));

    public static string Format(double y, double blur, double alpha) =>
        BoxShadows.Parse($"0 {y} {blur} 0 #{(int)Math.Round(alpha * 255):X2}000000").ToString();
}
