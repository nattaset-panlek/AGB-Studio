using Avalonia.Media;

namespace AgbStudio.Editor.Theme;

/// <summary>Bundled font families (T-K03). Latin UI text falls back to IBM Plex Sans Thai for Thai characters.</summary>
public static class Fonts
{
    const string Root = "avares://AGBStudio/Assets/Fonts/";
    public const string InterUri = Root + "Inter#Inter";
    public const string MonoUri = Root + "JetBrainsMono#JetBrains Mono";
    public const string ThaiUri = Root + "IBMPlexSansThai#IBM Plex Sans Thai";
    public const string ThaiLoopedUri = Root + "IBMPlexSansThaiLooped#IBM Plex Sans Thai Looped";
    public const string PixelUri = Root + "PressStart2P#Press Start 2P";
    public const string WordmarkUri = Root + "Nunito#Nunito";

    public static readonly FontFamily ThaiFamily = new(ThaiUri);
    public static readonly FontFamily Inter = new(InterUri + ", " + ThaiUri);
    public static readonly FontFamily Mono = new(MonoUri + ", " + ThaiUri);
    public static readonly FontFamily Thai = new(ThaiUri + ", " + InterUri);
    public static readonly FontFamily ThaiLooped = new(ThaiLoopedUri + ", " + InterUri);
    public static readonly FontFamily Pixel = new(PixelUri);
    public static readonly FontFamily Wordmark = new(WordmarkUri);

    public static FontFamily ForFigmaFamily(string family) => family switch
    {
        "Inter" => Inter,
        "JetBrains Mono" => Mono,
        "IBM Plex Sans Thai" => Thai,
        "IBM Plex Sans Thai Looped" => ThaiLooped,
        "Press Start 2P" => Pixel,
        "Nunito" => Wordmark,
        _ => throw new ArgumentException("Font not bundled: " + family),
    };

    public static FontWeight ForFigmaStyle(string style) => style switch
    {
        "Regular" => FontWeight.Regular,
        "Medium" => FontWeight.Medium,
        "Semi Bold" or "SemiBold" => FontWeight.SemiBold,
        "Bold" => FontWeight.Bold,
        "ExtraBold" => FontWeight.ExtraBold,
        _ => throw new ArgumentException("Unknown font style: " + style),
    };
}
