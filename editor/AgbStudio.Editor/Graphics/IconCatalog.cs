using Avalonia.Platform;

namespace AgbStudio.Editor.Graphics;

/// <summary>The 70 icon/* components (T-K04) and the art/thumb/brand components, exported from Figma as SVG.</summary>
public static class IconCatalog
{
    const string IconRoot = "avares://AGBStudio/Assets/Icons/";
    const string ArtRoot = "avares://AGBStudio/Assets/Art/";
    static readonly Dictionary<string, SvgImage> Icons = [];
    static readonly Dictionary<string, SvgImage> Arts = [];
    static string[]? _names;

    /// <summary>All icon names, e.g. "play", "folderplus".</summary>
    public static IReadOnlyList<string> Names => _names ??= AssetLoader.GetAssets(new Uri(IconRoot), null)
        .Select(u => Path.GetFileNameWithoutExtension(u.AbsolutePath)).Order(StringComparer.Ordinal).ToArray();

    public static bool Exists(string name) => Names.Contains(name);

    public static SvgImage Icon(string name)
    {
        if (!Icons.TryGetValue(name, out var img))
        {
            try
            {
                Icons[name] = img = SvgImage.Load(new Uri(IconRoot + name + ".svg"));
            }
            catch (Exception)
            {
                var fallback = name.StartsWith("eye", StringComparison.OrdinalIgnoreCase) ? "eye" : "cube";
                Icons[name] = img = SvgImage.Load(new Uri(IconRoot + fallback + ".svg"));
            }
        }
        return img;
    }

    /// <summary>Art by Figma name, e.g. "art/sprite/hero", "thumb/hero", "brand/mark", "brand/app-icon/Light".</summary>
    public static SvgImage Art(string figmaName)
    {
        if (!Arts.TryGetValue(figmaName, out var img)) Arts[figmaName] = img = SvgImage.Load(new Uri(ArtRoot + ArtFile(figmaName)));
        return img;
    }

    public static string ArtFile(string figmaName) => figmaName.ToLowerInvariant().Replace('/', '-') + ".svg";
}
