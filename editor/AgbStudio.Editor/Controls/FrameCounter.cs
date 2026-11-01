using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Controls;

/// <summary>
/// A chip whose text counts frames ("Frame 1234", "Paused at frame 1234"): fixed width, left-aligned text and tabular
/// digits, so a changing number never moves the toolbar groups next to it (the Play / Pause / Step group jittered).
/// </summary>
public static class FrameCounter
{
    /// <summary>OpenType "tnum": every digit has the same advance width (Inter has it).</summary>
    public static FontFeatureCollection Tabular { get; } = [FontFeature.Parse("tnum")];

    public static T Fix<T>(T chip, double width) where T : ContentControl
    {
        chip.Width = width;
        chip.HorizontalAlignment = HorizontalAlignment.Left;
        chip.HorizontalContentAlignment = HorizontalAlignment.Left;
        chip.SetValue(TextElement.FontFeaturesProperty, Tabular);
        return chip;
    }
}
