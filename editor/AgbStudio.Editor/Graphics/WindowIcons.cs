using Avalonia.Controls;
using Avalonia.Platform;

namespace AgbStudio.Editor.Graphics;

/// <summary>
/// The AGB Studio app icon (T-R01, Figma "brand/app-icon" Light variant, 16-256 px): the .exe icon through
/// &lt;ApplicationIcon&gt; and the icon of every window that shows in the taskbar (the editor, the component gallery).
/// </summary>
public static class WindowIcons
{
    public const string Uri = "avares://AGBStudio/Assets/agb-studio.ico";

    public static WindowIcon App()
    {
        using var s = AssetLoader.Open(new System.Uri(Uri));
        return new WindowIcon(s);
    }
}
