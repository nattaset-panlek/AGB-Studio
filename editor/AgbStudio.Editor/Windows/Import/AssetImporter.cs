using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Windows.Audio;

namespace AgbStudio.Editor.Windows.Import;

public enum ImportKind { SpriteSheet, Tileset, Background, Tilemap, SoundEffect, Music, Palette, Font, Script, Unsupported }

/// <summary>One file in Import Asset: what it was detected as, its per-file options and its problem, if any.</summary>
public sealed class ImportItem
{
    public required string Source { get; init; }
    public ImportKind Kind { get; set; }
    public long Size { get; init; }
    public string FileName => Path.GetFileName(Source);
    public string Name => GameProject.AssetName(Source);

    /// <summary>A problem the file cannot be imported with (MP3, unreadable data ...); it is skipped.</summary>
    public string? Error { get; set; }
    /// <summary>A problem that needs a decision (one of <see cref="Fixes"/>) before the file is imported.</summary>
    public string? Warning { get; set; }
    public IReadOnlyList<string> Fixes { get; set; } = [];
    public string? Fix { get; set; }
    public string Ready { get; set; } = "Ready.";

    // sprite sheet options (Figma: Frame size, Colours, Palette, Frames found, Trim empty frames, Create animation)
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameWidth { get; set; } = 16;
    public int FrameHeight { get; set; } = 16;
    public int Colors { get; set; }
    public string SharePalette { get; set; } = "";
    public bool TrimEmptyFrames { get; set; } = true;
    public bool CreateAnimation { get; set; } = true;
    public string AnimationName { get; set; } = "idle";
    public ColorImage? Image { get; set; }
    public uint[]? Pixels { get; set; }

    // other kinds
    public string Detail { get; set; } = "";

    public bool IsReady => Error is null && (Warning is null || Fix != null);
    public bool HasProblem => Error != null || (Warning != null && Fix is null);

    public string Detected => Kind switch
    {
        ImportKind.SpriteSheet => "Sprite sheet",
        ImportKind.Tileset => "Tileset",
        ImportKind.Background => "Background",
        ImportKind.Tilemap => "Tilemap (Tiled)",
        ImportKind.SoundEffect => "Sound effect",
        ImportKind.Music => "Music",
        ImportKind.Palette => "Palette",
        ImportKind.Font => "Font",
        ImportKind.Script => "C# script",
        _ => "Unknown",
    };

    /// <summary>Frames in the sheet at the frame size (empty frames left out when trimming).</summary>
    public int FramesFound
    {
        get
        {
            if (Image is null || FrameWidth <= 0 || FrameHeight <= 0) return 0;
            var cols = Math.Max(1, Width / FrameWidth);
            var rows = Math.Max(1, Height / FrameHeight);
            var n = 0;
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    if (!TrimEmptyFrames || !Image.IsEmpty(c * FrameWidth, r * FrameHeight, FrameWidth, FrameHeight)) n++;
            return n;
        }
    }

    public int FrameRows => Image is null || FrameHeight <= 0 ? 0 : Math.Max(1, Height / FrameHeight);
}

/// <summary>
/// Import Asset (P11.64): detects the type of each file and converts it into the project folders (D-03): sprite
/// sheets, tilesets and backgrounds (PNG), Tiled maps (TMX), sound effects (WAV -> 11 kHz mono 8-bit, D-14), music
/// (MOD / XM), palettes (.pal / .gpl), fonts and C# scripts. Problems are phrased for Import Asset · Errors.
/// </summary>
public static class AssetImporter
{
    static readonly string[] OtherAudio = [".mp3", ".ogg", ".flac", ".m4a", ".aac", ".wma", ".it", ".s3m", ".mid", ".midi", ".opus"];

    public static ImportItem Inspect(string file, GameProject? project)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var item = new ImportItem { Source = file, Size = File.Exists(file) ? new FileInfo(file).Length : 0 };
        try
        {
            switch (ext)
            {
                case ".png": InspectImage(item, project); break;
                case ".tmx": InspectTmx(item); break;
                case ".wav":
                    item.Kind = ImportKind.SoundEffect;
                    var w = WavData.Load(file);
                    var bytes = (int)Math.Ceiling(w.Seconds * 11025);
                    item.Ready = $"Ready · resampled to 11 kHz mono 8-bit ({Math.Max(1, (int)Math.Round(bytes / 1024.0))} KB).";
                    item.Detail = $"{w.Seconds.ToString("0.00", CultureInfo.InvariantCulture)} s";
                    break;
                case ".mod" or ".xm":
                    item.Kind = ImportKind.Music;
                    var m = TrackerModule.Load(file);
                    item.Ready = $"Ready · {m.Format}, {m.Channels} channels, {m.Order.Distinct().Count()} patterns.";
                    item.Detail = $"{m.Channels} channels";
                    break;
                case ".pal" or ".gpl":
                    item.Kind = ImportKind.Palette;
                    var colors = PaletteFiles.Read(file);
                    item.Ready = $"Ready · {colors.Count} colours.";
                    if (colors.Count > 256) item.Error = $"Has {colors.Count} colours. A GBA palette holds at most 256.";
                    break;
                case ".ttf" or ".otf" or ".bdf":
                    item.Kind = ImportKind.Font;
                    item.Ready = "Ready · rendered to a GBA font when you build.";
                    break;
                case ".cs":
                    item.Kind = ImportKind.Script;
                    item.Ready = "Ready · compiled with the project's scripts.";
                    break;
                default:
                    if (OtherAudio.Contains(ext))
                    {
                        item.Kind = ImportKind.Music;
                        item.Error = $"{ext[1..].ToUpperInvariant()} is not supported on GBA. Use .wav for sound effects or .mod / .xm for music.";
                        item.Fixes = ["How to convert"];
                    }
                    else
                    {
                        item.Kind = ImportKind.Unsupported;
                        item.Error = $"AGB Studio can't import {(ext.Length > 0 ? ext : "this kind of")} files.";
                    }
                    break;
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Xml.XmlException or FormatException or IndexOutOfRangeException or ArgumentException or UnauthorizedAccessException)
        {
            item.Error = $"{item.FileName} can't be read: {e.Message}";
        }
        return item;
    }

    static void InspectImage(ImportItem item, GameProject? project)
    {
        var png = Png.Read(item.Source);
        var img = ColorImage.FromPng(png);
        item.Image = img;
        item.Pixels = png.Pixels;
        item.Width = img.Width;
        item.Height = img.Height;
        item.Colors = img.Histogram().Count;
        var lower = item.Name.ToLowerInvariant();
        if (lower.Contains("tile")) item.Kind = ImportKind.Tileset;
        else if (img.Width >= 240 && img.Height >= 160) item.Kind = ImportKind.Background;
        else if ((img.Width > 64 || img.Height > 64) && img.Width % 16 == 0 && img.Height % 16 == 0 && img.Height > 64) item.Kind = ImportKind.Tileset;
        else item.Kind = ImportKind.SpriteSheet;
        if (item.Kind == ImportKind.SpriteSheet)
        {
            (item.FrameWidth, item.FrameHeight) = GuessFrame(img.Width, img.Height);
            var under = item.Name.LastIndexOf('_');
            item.AnimationName = under > 0 && under < item.Name.Length - 1 ? item.Name[(under + 1)..] : "idle";
            // share the palette of the sprite the name extends (hero_run -> hero) when the colours fit in one palette
            if (project != null && under > 0 && item.Colors <= 15)
            {
                var owner = item.Name[..under];
                if (project.SpriteFiles().Any(f => GameProject.AssetName(f) == owner)) item.SharePalette = owner;
            }
            if (item.FrameWidth > 64 || item.FrameHeight > 64) item.Error = $"Frames are {item.FrameWidth} × {item.FrameHeight}; the biggest GBA sprite is 64 × 64.";
            var problem = SpriteConverter.Convert(item.Name, img, new SpriteMeta { FrameWidth = Math.Min(64, item.FrameWidth), FrameHeight = Math.Min(64, item.FrameHeight), ReduceColors = false }, null, item.Source).Problems.FirstOrDefault();
            if (problem != null && item.SharePalette.Length == 0) { item.Warning = problem.Message; item.Fixes = problem.Fixes; }
            item.Ready = "Ready.";
        }
        else if (item.Kind == ImportKind.Tileset)
        {
            var t = TilesetConverter.Convert(item.Name, img, new TilesetMeta(), project?.Settings.Tags ?? [], 13, item.Source);
            if (t.Problems.FirstOrDefault() is { } p) { item.Warning = p.Message; item.Fixes = p.Fixes; }
            item.Ready = $"Ready · {t.Tiles.Count} unique tiles.";
        }
        else item.Ready = $"Ready · {img.Width} × {img.Height} background.";
    }

    /// <summary>Frame size of a sheet: square frames of the sheet's height when they fit in a row, else the largest GBA size that divides it.</summary>
    public static (int W, int H) GuessFrame(int width, int height)
    {
        if (height <= 64 && width % height == 0 && Gba.OamSize(height, height) != null) return (height, height);
        foreach (var (w, h) in Gba.OamSizes.OrderByDescending(s => s.W * s.H))
            if (width % w == 0 && height % h == 0 && w <= width && h <= height) return (w, h);
        return (width, height);
    }

    // ---- Tiled -----------------------------------------------------------------------------------------

    sealed record TmxMap(int Width, int Height, int TileWidth, string? TilesetImage, int FirstGid, List<(string Name, int[] Gids)> Layers);

    static TmxMap ReadTmx(string file)
    {
        var doc = XDocument.Load(file);
        var map = doc.Root ?? throw new InvalidDataException("Empty TMX file.");
        int Int(XElement e, string a) => int.Parse(e.Attribute(a)?.Value ?? "0", CultureInfo.InvariantCulture);
        var ts = map.Element("tileset");
        string? image = null;
        var firstGid = 1;
        if (ts != null)
        {
            firstGid = Int(ts, "firstgid");
            var src = ts.Attribute("source")?.Value;
            var tsx = ts;
            var baseDir = Path.GetDirectoryName(file)!;
            if (src != null && File.Exists(Path.Combine(baseDir, src)))
            {
                tsx = XDocument.Load(Path.Combine(baseDir, src)).Root!;
                baseDir = Path.GetDirectoryName(Path.Combine(baseDir, src))!;
            }
            if (tsx.Element("image")?.Attribute("source")?.Value is { } img) image = Path.GetFullPath(Path.Combine(baseDir, img));
        }
        var layers = new List<(string, int[])>();
        foreach (var l in map.Elements("layer"))
        {
            var data = l.Element("data") ?? throw new InvalidDataException("A layer has no data.");
            var enc = data.Attribute("encoding")?.Value;
            int[] gids;
            if (enc == "csv") gids = data.Value.Split([',', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries).Select(v => (int)(uint.Parse(v, CultureInfo.InvariantCulture) & 0x1FFFFFFF)).ToArray();
            else if (enc == "base64")
            {
                var bytes = Convert.FromBase64String(data.Value.Trim());
                var comp = data.Attribute("compression")?.Value;
                if (comp is "zlib" or "gzip")
                {
                    using var src = new MemoryStream(bytes);
                    using Stream z = comp == "zlib" ? new ZLibStream(src, CompressionMode.Decompress) : new GZipStream(src, CompressionMode.Decompress);
                    using var dst = new MemoryStream();
                    z.CopyTo(dst);
                    bytes = dst.ToArray();
                }
                else if (comp != null) throw new InvalidDataException($"Tiled {comp} compression is not supported; save the map as CSV.");
                gids = Enumerable.Range(0, bytes.Length / 4).Select(i => (int)(BitConverter.ToUInt32(bytes, i * 4) & 0x1FFFFFFF)).ToArray();
            }
            else gids = data.Elements("tile").Select(t => int.Parse(t.Attribute("gid")?.Value ?? "0", CultureInfo.InvariantCulture)).ToArray();
            layers.Add((l.Attribute("name")?.Value ?? $"Layer {layers.Count + 1}", gids));
        }
        return new TmxMap(Int(map, "width"), Int(map, "height"), Int(map, "tilewidth"), image, firstGid, layers);
    }

    static void InspectTmx(ImportItem item)
    {
        item.Kind = ImportKind.Tilemap;
        var m = ReadTmx(item.Source);
        if (m.TileWidth != 16) item.Error = $"Tiles are {m.TileWidth} px; AGB Studio maps use 16 × 16 tiles.";
        else if (m.Layers.Count == 0) item.Error = "The map has no tile layers.";
        else if (m.Layers.Count > 4) item.Error = $"The map has {m.Layers.Count} layers; the GBA shows at most 4 backgrounds.";
        item.Detail = $"{m.Width} × {m.Height} tiles · {m.Layers.Count} layers";
        item.Ready = $"Ready · {m.Width} × {m.Height} tiles, {m.Layers.Count} layer{(m.Layers.Count == 1 ? "" : "s")}.";
    }

    // ---- fixes and import -------------------------------------------------------------------------------

    /// <summary>Applies one of the fix buttons of Import Asset · Errors.</summary>
    public static void ApplyFix(ImportItem item, string fix)
    {
        if (fix.StartsWith("Reduce", StringComparison.Ordinal))
        {
            item.Fix = fix;
            item.Ready = "Ready · reduced to 16 colours.";
        }
        else if (fix.StartsWith("Split", StringComparison.Ordinal))
        {
            item.Fix = fix;
            item.Ready = $"Ready · split into {SplitCount(item)} sprites with one palette each, drawn on top of each other.";
        }
    }

    public static int SplitCount(ImportItem item) => Math.Max(1, (item.Colors + 14) / 15);

    /// <summary>The OBJ palette slot a sprite owns (sprites with their own palette, by name), or -1.</summary>
    public static int ObjPaletteSlot(GameProject p, string sprite)
    {
        var owners = p.SpriteFiles().Where(f => p.LoadMeta<SpriteMeta>(f).Palette.Length == 0).Select(GameProject.AssetName).Order(StringComparer.Ordinal).ToList();
        return owners.IndexOf(sprite);
    }

    /// <summary>Converts a ready file into the project. Returns the files written.</summary>
    public static IReadOnlyList<string> Import(GameProject p, ImportItem item)
    {
        if (!item.IsReady) throw new InvalidOperationException($"{item.FileName} is not ready to import.");
        var written = new List<string>();
        string Copy(string dir)
        {
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, item.FileName);
            if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(item.Source), StringComparison.OrdinalIgnoreCase)) File.Copy(item.Source, dest, true);
            written.Add(dest);
            return dest;
        }
        switch (item.Kind)
        {
            case ImportKind.SpriteSheet:
                var dir = Path.Combine(p.AssetsDir, "sprites");
                if (item.Fix?.StartsWith("Split", StringComparison.Ordinal) == true) written.AddRange(SplitSprite(item, dir));
                else
                {
                    var dest = Copy(dir);
                    GameProject.Save(GameProject.MetaPath(dest), SpriteMeta(item, item.Fix != null || item.Colors <= 15 || item.SharePalette.Length > 0));
                    written.Add(GameProject.MetaPath(dest));
                }
                break;
            case ImportKind.Tileset:
                var t = Copy(Path.Combine(p.AssetsDir, "tilesets"));
                GameProject.Save(GameProject.MetaPath(t), new TilesetMeta());
                written.Add(GameProject.MetaPath(t));
                break;
            case ImportKind.Background: Copy(Path.Combine(p.AssetsDir, "images")); break;
            case ImportKind.SoundEffect:
                var sfx = Path.Combine(p.AssetsDir, "audio", "sfx", item.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(sfx)!);
                File.WriteAllBytes(sfx, WavWriter.Encode(WavData.Load(item.Source).ToGba8(), 11025));
                GameProject.Save(GameProject.MetaPath(sfx), new SoundMeta());
                written.AddRange([sfx, GameProject.MetaPath(sfx)]);
                break;
            case ImportKind.Music:
                var music = Copy(Path.Combine(p.AssetsDir, "audio", "music"));
                GameProject.Save(GameProject.MetaPath(music), new MusicMeta());
                written.Add(GameProject.MetaPath(music));
                break;
            case ImportKind.Palette: Copy(Path.Combine(p.AssetsDir, "palettes")); break;
            case ImportKind.Font:
                var font = Copy(p.FontsDir);
                var fontFile = Path.Combine(p.FontsDir, item.Name + ".font");
                GameProject.Save(fontFile, new FontFile
                {
                    Name = item.Name, Source = Path.GetFileName(font),
                    SourceKind = font.EndsWith(".bdf", StringComparison.OrdinalIgnoreCase) ? FontSourceKind.Bdf : FontSourceKind.Ttf,
                });
                written.Add(fontFile);
                break;
            case ImportKind.Script: Copy(p.ScriptsDir); break;
            case ImportKind.Tilemap: written.AddRange(ImportTmx(p, item)); break;
            default: throw new InvalidOperationException($"{item.FileName} can't be imported.");
        }
        return written;
    }

    static SpriteMeta SpriteMeta(ImportItem item, bool reduce)
    {
        var meta = new SpriteMeta
        {
            FrameWidth = item.FrameWidth, FrameHeight = item.FrameHeight, OriginX = item.FrameWidth / 2, OriginY = item.FrameHeight,
            Collider = new ColliderData { X = Math.Min(2, item.FrameWidth / 4), Y = Math.Min(2, item.FrameHeight / 4), W = item.FrameWidth - 2 * Math.Min(2, item.FrameWidth / 4), H = item.FrameHeight - Math.Min(2, item.FrameHeight / 4) },
            Palette = item.SharePalette, ReduceColors = reduce, TrimEmptyFrames = item.TrimEmptyFrames,
        };
        if (item.CreateAnimation) meta.Animations.Add(new AnimationData { Name = item.AnimationName, Frames = [.. Enumerable.Range(0, Math.Max(1, item.FramesFound))] });
        return meta;
    }

    /// <summary>"Split into N palettes": colours sorted by hue in groups of 15; one sprite per group, layered at the same position.</summary>
    static IEnumerable<string> SplitSprite(ImportItem item, string dir)
    {
        var img = item.Image!;
        var colors = Quantizer.SortByHue(img.Histogram().Keys).ToList();
        var groups = colors.Chunk(15).Select(g => g.ToHashSet()).ToList();
        Directory.CreateDirectory(dir);
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var px = new uint[img.Width * img.Height];
            for (var i = 0; i < px.Length; i++)
                if (!img.Transparent[i] && groups[gi].Contains(img.Colors[i])) px[i] = item.Pixels![i] | 0xFF000000u;
            var dest = Path.Combine(dir, $"{item.Name}_{gi}.png");
            Png.WriteArgb(dest, px, img.Width, img.Height);
            GameProject.Save(GameProject.MetaPath(dest), SpriteMeta(item, true));
            yield return dest;
            yield return GameProject.MetaPath(dest);
        }
    }

    static IEnumerable<string> ImportTmx(GameProject p, ImportItem item)
    {
        var m = ReadTmx(item.Source);
        var written = new List<string>();
        var tileset = "";
        if (m.TilesetImage != null && File.Exists(m.TilesetImage))
        {
            var dest = Path.Combine(p.AssetsDir, "tilesets", Path.GetFileName(m.TilesetImage));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(m.TilesetImage, dest, true);
            if (!File.Exists(GameProject.MetaPath(dest))) GameProject.Save(GameProject.MetaPath(dest), new TilesetMeta());
            written.AddRange([dest, GameProject.MetaPath(dest)]);
            tileset = GameProject.AssetName(dest);
        }
        var map = new MapFile { Name = item.Name, Tileset = tileset, Width = m.Width, Height = m.Height };
        for (var i = 0; i < m.Layers.Count; i++)
        {
            var (name, gids) = m.Layers[i];
            map.Layers.Add(new MapLayerData { Name = name, Bg = i, Type = LayerType.Tile, Priority = i, Cells = [.. gids.Select(g => g == 0 ? 0 : Math.Max(0, g - m.FirstGid))] });
        }
        var file = Path.Combine(p.MapsDir, item.Name + ".map");
        GameProject.Save(file, map);
        written.Add(file);
        return written;
    }
}
