using AgbStudio.Build.Fonts;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// The project's assets converted exactly as <c>GameBuilder</c> converts them (SpriteConverter, TilesetConverter,
/// image layers, fonts and the laid-out Text Label strings), cached per file and refreshed when a file changes.
/// The viewport renderer (D-18), the thumbnails and the GBA budget read their tiles, palettes and sizes from here.
/// </summary>
public sealed class SceneAssets(GameProject project)
{
    sealed record Entry<T>(long Stamp, T Value);

    readonly Dictionary<string, Entry<ConvertedSprite?>> _sprites = new(StringComparer.Ordinal);
    readonly Dictionary<string, Entry<ConvertedTileset?>> _tilesets = new(StringComparer.Ordinal);
    readonly Dictionary<string, Entry<ImageLayer?>> _images = new(StringComparer.Ordinal);
    readonly Dictionary<(string, bool), Entry<BitmapPicture?>> _pictures = new();
    readonly Dictionary<string, Entry<ColorImage?>> _pictureImages = new(StringComparer.Ordinal);

    /// <summary>Files that could not be converted (not a PNG, unreadable), with the reason.</summary>
    public Dictionary<string, string> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);

    T? Safe<T>(string path, Func<T> convert) where T : class
    {
        try
        {
            Errors.Remove(path);
            return convert();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Errors[path] = e.Message;
            return null;
        }
    }
    TextTables? _text;
    long _textStamp = -1;

    public GameProject Project { get; } = project;

    /// <summary>An image layer (sky, hills): 8x8 tiles straight to screen entries.</summary>
    public sealed record ImageLayer(ConvertedTileset Tiles, ushort[] Cells, int WidthTiles, int HeightTiles);

    public string? SpritePath(string name) => Project.SpriteFiles().FirstOrDefault(f => GameProject.AssetName(f) == name);
    public string? TilesetPath(string name) => Project.TilesetFiles().FirstOrDefault(f => GameProject.AssetName(f) == name);
    public string? ImagePath(string name) => Project.ImageFiles().FirstOrDefault(f => GameProject.AssetName(f) == name);

    static long Stamp(params string[] files)
    {
        long s = 17;
        foreach (var f in files)
            s = s * 31 + (File.Exists(f) ? File.GetLastWriteTimeUtc(f).Ticks ^ new FileInfo(f).Length : 0);
        return s;
    }

    public SpriteMeta SpriteMeta(string name) => SpritePath(name) is { } p ? Project.LoadMeta<SpriteMeta>(p) : new SpriteMeta();

    /// <summary>A sprite sheet as the build converts it; a sheet that shares a palette uses its owner's colours.</summary>
    public ConvertedSprite? Sprite(string name) => Sprite(name, 0);

    ConvertedSprite? Sprite(string name, int depth)
    {
        if (name.Length == 0 || depth > 4 || SpritePath(name) is not { } path) return null;
        var meta = Project.LoadMeta<SpriteMeta>(path);
        ConvertedSprite? owner = meta.Palette.Length > 0 && meta.Palette != name ? Sprite(meta.Palette, depth + 1) : null;
        var stamp = Stamp(path, GameProject.MetaPath(path)) ^ (owner == null ? 0 : Stamp(SpritePath(meta.Palette)!, GameProject.MetaPath(SpritePath(meta.Palette)!)));
        if (_sprites.TryGetValue(name, out var e) && e.Stamp == stamp) return e.Value;
        var s = Safe(path, () => SpriteConverter.Convert(name, ColorImage.Load(path), meta, owner?.Palette, path));
        _sprites[name] = new(stamp, s);
        return s;
    }

    public ConvertedTileset? Tileset(string name)
    {
        if (name.Length == 0 || TilesetPath(name) is not { } path) return null;
        var stamp = Stamp(path, GameProject.MetaPath(path), Project.ProjectPath);
        if (_tilesets.TryGetValue(name, out var e) && e.Stamp == stamp) return e.Value;
        var t = Safe(path, () => TilesetConverter.Convert(name, ColorImage.Load(path), Project.LoadMeta<TilesetMeta>(path), Project.Settings.Tags, 13, path));
        _tilesets[name] = new(stamp, t);
        return t;
    }

    public ImageLayer? Image(string name)
    {
        if (name.Length == 0 || ImagePath(name) is not { } path) return null;
        var stamp = Stamp(path);
        if (_images.TryGetValue(name, out var e) && e.Stamp == stamp) return e.Value;
        var img = Safe(path, () =>
        {
            var (t, cells, w, h) = TilesetConverter.ConvertImage(name, ColorImage.Load(path));
            return new ImageLayer(t, cells, w, h);
        });
        _images[name] = new(stamp, img);
        return img;
    }

    /// <summary>The pixels of a picture in a bitmap mode, from the build's conversion: 15-bit colours in Modes 3 and 5; in
    /// Mode 4 palette indices and the colours of indices 1, 2, ...</summary>
    public sealed record BitmapPicture(int Width, int Height, ushort[]? Bgr, IndexedPicture? Indexed);

    /// <summary>An image's size in pixels, or null when it is not in assets/images.</summary>
    public (int Width, int Height)? PictureSize(string name) => PictureImage(name) is { } img ? (img.Width, img.Height) : null;

    /// <summary>A picture as the mode shows it, or null: no such image, or another size than the mode shows (the build
    /// stops with an error then, and the editor shows the backdrop).</summary>
    public BitmapPicture? Picture(string name, int mode)
    {
        if (PictureImage(name) is not { } img || (img.Width, img.Height) != BitmapConverter.PictureSize(mode)) return null;
        var key = (name, mode == 4);
        var stamp = Stamp(ImagePath(name)!);
        if (_pictures.TryGetValue(key, out var e) && e.Stamp == stamp) return e.Value;
        var picture = mode == 4 ? new BitmapPicture(img.Width, img.Height, null, BitmapConverter.ToIndexed(img)) : new BitmapPicture(img.Width, img.Height, BitmapConverter.ToBgr(img), null);
        _pictures[key] = new(stamp, picture);
        return picture;
    }

    ColorImage? PictureImage(string name)
    {
        if (name.Length == 0 || ImagePath(name) is not { } path) return null;
        var stamp = Stamp(path);
        if (_pictureImages.TryGetValue(name, out var e) && e.Stamp == stamp) return e.Value;
        var img = Safe(path, () => ColorImage.Load(path));
        _pictureImages[name] = new(stamp, img);
        return img;
    }

    /// <summary>The map of a scene (maps/&lt;Map&gt;.map), or null.</summary>
    public MapFile? Map(SceneFile scene)
    {
        if (scene.Map.Length == 0) return null;
        var path = Path.Combine(Project.MapsDir, Path.GetFileNameWithoutExtension(scene.Map) + ".map");
        if (!File.Exists(path)) return null;
        // a file being edited by hand may not read yet: the panels go on without it, the Console says what is wrong
        try { return GameProject.Load<MapFile>(path); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>Fonts and string tables as the build lays them out (ConvertText): the run of a key in the
    /// game's start language, else the source language.</summary>
    public TextTables Text
    {
        get
        {
            var files = Project.StringTables().Concat(Project.FontFiles()).Append(Path.Combine(Project.TextDir, "strings.meta.json")).Append(Project.ProjectPath).ToArray();
            var stamp = Stamp(files) ^ files.Length;
            if (_text == null || stamp != _textStamp)
            {
                _text = new TextTables(Project);
                _textStamp = stamp;
            }
            return _text;
        }
    }
}

/// <summary>
/// The Localization tables, fonts and layout of GameBuilder.ConvertText: language order (source first, then by code),
/// the language font, the code points every font is built with, and TextLayout per key with the box width from
/// strings.meta.json. Text Label actors read their run from here.
/// </summary>
public sealed class TextTables
{
    readonly GameProject _p;
    readonly List<(string Lang, StringTable Table)> _order = [];
    readonly Dictionary<string, GameFont> _fonts = new(StringComparer.Ordinal);
    readonly List<int> _used;
    readonly StringMeta _meta;
    readonly int _language;

    public TextTables(GameProject p)
    {
        _p = p;
        var tables = p.StringTables().Select(f => GameProject.Load<StringTable>(f)).ToList();
        var loc = p.Settings.Localization;
        var langs = loc.Languages;
        var allText = string.Concat(tables.SelectMany(t => t.Strings.Values)) + (langs.Count > 1 ? string.Concat(langs.Select(l => l.Name)) : "");
        _used = ThaiText.CodePoints(allText).Where(c => c >= 0x20).Distinct().ToList();
        var metaPath = Path.Combine(p.TextDir, "strings.meta.json");
        _meta = File.Exists(metaPath) ? GameProject.Load<StringMeta>(metaPath) : new StringMeta();
        foreach (var t in tables.OrderBy(t => t.Language == loc.Source ? 0 : 1).ThenBy(t => t.Language, StringComparer.Ordinal))
            _order.Add((t.Language, t));
        _language = Math.Max(0, _order.FindIndex(o => o.Lang == p.Settings.General.DefaultLanguage));
    }

    public IReadOnlyList<string> Languages => _order.Select(o => o.Lang).ToList();
    public string? StartLanguage => _order.Count == 0 ? null : _order[_language].Lang;

    /// <summary>True when the key exists in any table (the build gives it a string id).</summary>
    public bool HasKey(string key) => _order.Any(o => o.Table.Strings.ContainsKey(key));

    GameFont? FontFor(string lang)
    {
        var info = _p.Settings.Localization.Languages.FirstOrDefault(l => l.Code == lang);
        var name = info?.Font is { Length: > 0 } fn ? fn : _p.Settings.General.DefaultFont;
        var files = _p.FontFiles().Select(f => (File: f, Font: GameProject.Load<FontFile>(f))).ToList();
        var pick = files.FirstOrDefault(x => (x.Font.Name.Length > 0 ? x.Font.Name : GameProject.AssetName(x.File)) == name);
        if (pick.File == null) pick = files.FirstOrDefault();
        if (pick.File == null) return null;
        if (pick.Font.Name.Length == 0) pick.Font.Name = GameProject.AssetName(pick.File);
        if (_fonts.TryGetValue(pick.Font.Name, out var f)) return f;
        f = FontBuilder.Build(pick.Font, _p.FontsDir, _used);
        _fonts[pick.Font.Name] = f;
        return f;
    }

    /// <summary>The laid-out run the game shows for a key (start language, else the fallback = source language).</summary>
    public TextRun? Run(string key, string suffix = "")
    {
        if (_order.Count == 0) return null;
        foreach (var index in new[] { _language, 0 })
        {
            var (lang, table) = _order[index];
            if (!table.Strings.TryGetValue(key, out var text)) continue;
            var font = FontFor(lang);
            if (font == null) return null;
            text += suffix;   // a label the game writes a number to: the editor shows the preview number after it
            var m = _meta.Keys.GetValueOrDefault(key);
            var layout = TextLayout.Layout(text, font, m?.BoxWidth ?? 224, _p.Settings.Localization.ThaiWrap);
            return new TextRun(text, font, layout);
        }
        return null;
    }
}

/// <summary>A string laid out for the game: font, placed glyphs and line count.</summary>
public sealed record TextRun(string Text, GameFont Font, TextLayout.Result Layout)
{
    public int Lines => Math.Max(1, Layout.Lines);
}
