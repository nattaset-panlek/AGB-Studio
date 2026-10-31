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
                case ".aseprite" or ".ase": InspectAseprite(item); break;
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
                    if (colors.Count > 256) item.Error = $"Has {colors.Count} colours; a GBA palette holds at most 256. Remove colours in your paint program until 256 or fewer are left, then import it again.";
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
                        item.Error = $"AGB Studio can't import {(ext.Length > 0 ? ext : "this kind of")} files. Save it as one of the kinds it imports: .png, .aseprite, .tmx, .wav, .mod, .xm, .pal, .gpl, .ttf, .otf, .bdf or .cs.";
                    }
                    break;
            }
        }
        catch (InvalidDataException e)
        {
            // the readers' own texts (PNG, WAV, MOD / XM, Tiled, palettes) say what is wrong and how to save the file again
            item.Error = $"{item.FileName} can't be read: {e.Message}";
        }
        catch (Exception e) when (e is System.Xml.XmlException or FormatException or IndexOutOfRangeException or ArgumentException or EndOfStreamException)
        {
            item.Error = $"{item.FileName} can't be read: it is damaged or not the kind of file its name says ({e.Message.TrimEnd('.')}). Save it again from the program that made it and import the new file.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            item.Error = $"{item.FileName} can't be read: {e.Message.TrimEnd('.')}. Check that the file is still there and no other program has it open, then import it again.";
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
            if (item.FrameWidth > 64 || item.FrameHeight > 64) item.Error = $"Frames are {item.FrameWidth} × {item.FrameHeight}; the biggest GBA sprite is 64 × 64. Make the sheet a grid of frames of 8, 16, 32 or 64 pixels a side in your paint program, then import it again.";
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

    /// <summary>An Aseprite file is a sprite: its canvas is the frame size, its visible layers are flattened, and the build
    /// makes the animations from its tags (AsepriteFile, P7.02), so no animation is added here.</summary>
    static void InspectAseprite(ImportItem item)
    {
        var ase = AsepriteFile.Read(item.Source);
        var img = ase.ToSheet();
        item.Image = img;
        item.Pixels = ase.SheetPixels();
        item.Width = img.Width;
        item.Height = img.Height;
        item.Colors = img.Histogram().Count;
        item.Kind = ImportKind.SpriteSheet;
        (item.FrameWidth, item.FrameHeight) = (ase.Width, ase.Height);
        item.CreateAnimation = false;
        item.AnimationName = ase.Tags.FirstOrDefault()?.Name ?? "idle";
        if (ase.Width > 64 || ase.Height > 64) item.Error = $"Frames are {ase.Width} × {ase.Height}; the biggest GBA sprite is 64 × 64. Make the canvas 64 × 64 or smaller in Aseprite, then import it again.";
        var problem = SpriteConverter.Convert(item.Name, img, new SpriteMeta { FrameWidth = Math.Min(64, ase.Width), FrameHeight = Math.Min(64, ase.Height), ReduceColors = false }, null, item.Source).Problems.FirstOrDefault();
        if (problem != null) { item.Warning = problem.Message; item.Fixes = problem.Fixes; }
        item.Ready = $"Ready · {ase.Frames.Count} frames, {ase.Tags.Count} {(ase.Tags.Count == 1 ? "tag" : "tags")}.";
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
        // the file name is in front of these texts already ("{file} can't be read: ...", "{file} could not be imported: ...")
        var map = doc.Root ?? throw new InvalidDataException("The map file is empty. Save the map again in Tiled.");
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
            var data = l.Element("data") ?? throw new InvalidDataException($"Layer \"{l.Attribute("name")?.Value}\" has no tiles. Save the map again in Tiled.");
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
        if (m.TileWidth != 16) item.Error = $"Tiles are {m.TileWidth} px; AGB Studio maps use 16 × 16 tiles. Use a 16 × 16 tileset in Tiled and save the map again.";
        else if (m.Layers.Count == 0) item.Error = "The map has no tile layers. Add one in Tiled and save the map again.";
        else if (m.Layers.Count > 4) item.Error = $"The map has {m.Layers.Count} layers; the GBA shows at most 4 backgrounds. Merge or delete layers in Tiled until 4 are left, then save the map again.";
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

    /// <summary>The OBJ palette slot a sprite owns (sprites with their own palette, in creation order as the Palette Manager numbers them), or -1.</summary>
    public static int ObjPaletteSlot(GameProject p, string sprite)
    {
        var owners = AgbStudio.Editor.Services.CreationOrder.Of(p.SpriteFiles().Where(f => p.LoadMeta<SpriteMeta>(f).Palette.Length == 0)).Select(GameProject.AssetName).ToList();
        return owners.IndexOf(sprite);
    }

    /// <summary>
    /// Converts a ready file into the project. Returns the files written. <paramref name="journal"/> keeps what each
    /// written file was before, so the import can be undone (a cancelled or failed import, G-ERR.04).
    /// </summary>
    public static IReadOnlyList<string> Import(GameProject p, ImportItem item, ImportJournal? journal = null)
    {
        if (!item.IsReady) throw new InvalidOperationException($"{item.FileName} is not ready to import.");
        var written = new List<string>();
        void Will(string path)
        {
            journal?.Before(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }
        void Save<T>(string path, T value)
        {
            Will(path);
            GameProject.Save(path, value);
            written.Add(path);
        }
        string Copy(string dir)
        {
            var dest = Path.Combine(dir, item.FileName);
            Will(dest);
            if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(item.Source), StringComparison.OrdinalIgnoreCase)) File.Copy(item.Source, dest, true);
            written.Add(dest);
            return dest;
        }
        switch (item.Kind)
        {
            case ImportKind.SpriteSheet:
                var dir = Path.Combine(p.AssetsDir, "sprites");
                if (item.Fix?.StartsWith("Split", StringComparison.Ordinal) == true) written.AddRange(SplitSprite(item, dir, journal));
                else
                {
                    var dest = Copy(dir);
                    Save(GameProject.MetaPath(dest), SpriteMeta(item, item.Fix != null || item.Colors <= 15 || item.SharePalette.Length > 0));
                }
                break;
            case ImportKind.Tileset:
                Save(GameProject.MetaPath(Copy(Path.Combine(p.AssetsDir, "tilesets"))), new TilesetMeta());
                break;
            case ImportKind.Background: Copy(Path.Combine(p.AssetsDir, "images")); break;
            case ImportKind.SoundEffect:
                var sfx = Path.Combine(p.AssetsDir, "audio", "sfx", item.FileName);
                var pcm = WavWriter.Encode(WavData.Load(item.Source).ToGba8(), 11025);
                Will(sfx);
                File.WriteAllBytes(sfx, pcm);
                written.Add(sfx);
                Save(GameProject.MetaPath(sfx), new SoundMeta());
                break;
            case ImportKind.Music:
                Save(GameProject.MetaPath(Copy(Path.Combine(p.AssetsDir, "audio", "music"))), new MusicMeta());
                break;
            case ImportKind.Palette: Copy(p.PalettesDir); break;
            case ImportKind.Font:
                var font = Copy(p.FontsDir);
                Save(Path.Combine(p.FontsDir, item.Name + ".font"), new FontFile
                {
                    Name = item.Name, Source = Path.GetFileName(font),
                    SourceKind = font.EndsWith(".bdf", StringComparison.OrdinalIgnoreCase) ? FontSourceKind.Bdf : FontSourceKind.Ttf,
                });
                break;
            case ImportKind.Script: Copy(p.ScriptsDir); break;
            case ImportKind.Tilemap: written.AddRange(ImportTmx(p, item, journal)); break;
            default: throw new InvalidOperationException($"{item.FileName} can't be imported.");
        }
        return written;
    }

    /// <summary>
    /// Imports the ready files one after the other (Import Asset and Import Asset · Errors run it off the UI thread).
    /// G-ERR.04: cancelling <paramref name="ct"/> stops between two files and puts every file the import wrote or
    /// overwrote back as it was, then throws <see cref="ImportCancelledException"/>. A file that fails is undone the
    /// same way and listed in the outcome; the others go on. <paramref name="beforeFile"/> runs before each file (tests
    /// hold an import in progress with it).
    /// </summary>
    public static async Task<ImportOutcome> ImportAllAsync(GameProject p, IReadOnlyList<ImportItem> items, IProgress<ImportProgress>? progress = null,
        Func<int, CancellationToken, Task>? beforeFile = null, CancellationToken ct = default)
    {
        var ready = items.Where(i => i.IsReady).ToList();
        var done = new List<ImportJournal>();
        var written = new List<string>();
        var failed = new List<(ImportItem, Exception)>();
        try
        {
            for (var i = 0; i < ready.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ImportProgress(i, ready.Count, ready[i].FileName));
                if (beforeFile != null) await beforeFile(i, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                var journal = new ImportJournal();
                try
                {
                    written.AddRange(Import(p, ready[i], journal));
                    done.Add(journal);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
                {
                    failed.Add((ready[i], e));
                    journal.Rollback();
                }
            }
        }
        catch (OperationCanceledException)
        {
            var left = new List<string>();
            for (var j = done.Count - 1; j >= 0; j--) left.AddRange(done[j].Rollback());
            throw new ImportCancelledException(left);
        }
        progress?.Report(new ImportProgress(ready.Count, ready.Count, ""));
        return new ImportOutcome(written, ready.Count - failed.Count, failed);
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
    static List<string> SplitSprite(ImportItem item, string dir, ImportJournal? journal)
    {
        var img = item.Image!;
        var colors = Quantizer.SortByHue(img.Histogram().Keys).ToList();
        var groups = colors.Chunk(15).Select(g => g.ToHashSet()).ToList();
        var written = new List<string>();
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var px = new uint[img.Width * img.Height];
            for (var i = 0; i < px.Length; i++)
                if (!img.Transparent[i] && groups[gi].Contains(img.Colors[i])) px[i] = item.Pixels![i] | 0xFF000000u;
            var dest = Path.Combine(dir, $"{item.Name}_{gi}.png");
            journal?.Before(dest);
            journal?.Before(GameProject.MetaPath(dest));
            Directory.CreateDirectory(dir);
            Png.WriteArgb(dest, px, img.Width, img.Height);
            GameProject.Save(GameProject.MetaPath(dest), SpriteMeta(item, true));
            written.AddRange([dest, GameProject.MetaPath(dest)]);
        }
        return written;
    }

    static IEnumerable<string> ImportTmx(GameProject p, ImportItem item, ImportJournal? journal)
    {
        var m = ReadTmx(item.Source);
        var written = new List<string>();
        var tileset = "";
        if (m.TilesetImage != null && File.Exists(m.TilesetImage))
        {
            var dest = Path.Combine(p.AssetsDir, "tilesets", Path.GetFileName(m.TilesetImage));
            journal?.Before(dest);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(m.TilesetImage, dest, true);
            if (!File.Exists(GameProject.MetaPath(dest)))
            {
                journal?.Before(GameProject.MetaPath(dest));
                GameProject.Save(GameProject.MetaPath(dest), new TilesetMeta());
            }
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
        journal?.Before(file);
        GameProject.Save(file, map);
        written.Add(file);
        return written;
    }
}

/// <summary>How far an import is: <paramref name="Done"/> of <paramref name="Total"/> files converted, <paramref name="File"/> the next one.</summary>
public readonly record struct ImportProgress(int Done, int Total, string File);

/// <summary>The end of an import: the files written, how many sources were imported, and the ones that failed with why.</summary>
public sealed record ImportOutcome(IReadOnlyList<string> Written, int Imported, IReadOnlyList<(ImportItem Item, Exception Error)> Failed);

/// <summary>An import stopped by Cancel; the project is as it was before it, except <see cref="NotRestored"/>.</summary>
public sealed class ImportCancelledException(IReadOnlyList<string> notRestored) : OperationCanceledException("Import cancelled.")
{
    /// <summary>Files that could not be put back (another program held them).</summary>
    public IReadOnlyList<string> NotRestored { get; } = notRestored;
}

/// <summary>
/// What one import changed in the project: each file it writes is recorded before it is written (its old bytes, or
/// that it was new) with the folders the write creates, so <see cref="Rollback"/> leaves the project as it was (G-ERR.04).
/// </summary>
public sealed class ImportJournal
{
    readonly List<(string Path, byte[]? Before)> _files = [];
    readonly List<string> _folders = [];
    readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Call before writing <paramref name="path"/>.</summary>
    public void Before(string path)
    {
        var full = Path.GetFullPath(path);
        if (!_seen.Add(full)) return;
        for (var dir = Path.GetDirectoryName(full); dir != null && !Directory.Exists(dir); dir = Path.GetDirectoryName(dir)) _folders.Add(dir);
        _files.Add((full, File.Exists(full) ? File.ReadAllBytes(full) : null));
    }

    /// <summary>Deletes the new files and folders and writes back the old bytes of overwritten files. Returns the files it could not put back.</summary>
    public IReadOnlyList<string> Rollback()
    {
        var left = new List<string>();
        for (var i = _files.Count - 1; i >= 0; i--)
        {
            var (path, before) = _files[i];
            try
            {
                if (before is null) { if (File.Exists(path)) File.Delete(path); }
                else File.WriteAllBytes(path, before);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { left.Add(path); }
        }
        foreach (var dir in _folders.OrderByDescending(d => d.Length))
        {
            try { if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        _files.Clear();
        _folders.Clear();
        _seen.Clear();
        return left;
    }
}
