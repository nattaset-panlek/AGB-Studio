using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace AgbStudio.Editor.Services;

/// <summary>
/// Theme Dark / Light / System, accent colour, UI scale and reduce motion (P10.07, T-S06). The token dictionaries
/// for both themes are generated from Figma; the default accent (#4C8DFF) keeps every Figma accent token unchanged.
/// </summary>
public sealed class ThemeService(Application app)
{
    public const string DefaultAccent = "#4C8DFF";
    /// <summary>The five accent swatches of Preferences · Appearance.</summary>
    public static readonly IReadOnlyList<string> AccentChoices = ["#4C8DFF", "#B392FF", "#3ECF8E", "#FF9F43", "#FF7AB6"];
    static readonly string[] AccentKeys = ["accent/default", "accent/hover", "accent/pressed", "accent/subtle", "accent/selected"];

    public event Action? Changed;
    public double Scale { get; private set; } = 1;
    public bool ReduceMotion { get; private set; }

    public static ThemeVariant VariantOf(ThemeMode m) => m switch
    {
        ThemeMode.Dark => ThemeVariant.Dark,
        ThemeMode.Light => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };

    public void Apply(Preferences p)
    {
        app.RequestedThemeVariant = VariantOf(p.Theme);
        SetAccent(p.Accent);
        Scale = Math.Clamp(p.UiScalePercent, 50, 300) / 100.0;
        ReduceMotion = p.ReduceMotion;
        app.Resources["agb/reduce-motion"] = p.ReduceMotion;
        app.Resources["agb/code-font-size"] = (double)p.CodeFontSize;
        Changed?.Invoke();
    }

    /// <summary>
    /// The default accent removes the overrides so the Figma tokens apply. Another accent overrides the five accent tokens
    /// in both themes; Figma only defines them for the default, so hover / pressed / subtle / selected are derived.
    /// </summary>
    public void SetAccent(string hex)
    {
        foreach (var k in AccentKeys) app.Resources.Remove(k);
        if (string.Equals(hex, DefaultAccent, StringComparison.OrdinalIgnoreCase)) return;
        var c = Color.Parse(hex);
        var dark = Token(app, "bg/base", app.ActualThemeVariant) is ISolidColorBrush b ? b.Color : Colors.Black;
        app.Resources["accent/default"] = new SolidColorBrush(c);
        app.Resources["accent/hover"] = new SolidColorBrush(Mix(c, Colors.White, 0.15));
        app.Resources["accent/pressed"] = new SolidColorBrush(Mix(c, Colors.Black, 0.15));
        app.Resources["accent/subtle"] = new SolidColorBrush(Color.FromArgb(36, c.R, c.G, c.B));
        app.Resources["accent/selected"] = new SolidColorBrush(Mix(c, dark, 0.55));
    }

    static Color Mix(Color a, Color b, double t) => Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    /// <summary>Resolves a token brush for the given theme (used by tests and by code that draws by hand).</summary>
    public static IBrush? Token(IResourceHost host, string key, ThemeVariant? theme = null) =>
        host.TryFindResource(key, theme ?? (host as IThemeVariantHost)?.ActualThemeVariant, out var v) ? v as IBrush : null;
}
