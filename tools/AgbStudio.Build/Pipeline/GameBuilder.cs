using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Content;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Pipeline;

public sealed class BuildOptions
{
    public BuildConfiguration? Configuration { get; init; }
    public bool Clean { get; init; }
    /// <summary>Convert assets, compile scripts and write the generated C++, without running devkitARM (no ROM).</summary>
    public bool GenerateOnly { get; init; }
    public CancellationToken Cancel { get; init; }
}

public sealed class BuildResult
{
    public bool Success { get; set; }
    public string RomPath { get; set; } = "";
    public string ElfPath { get; set; } = "";
    public List<Diagnostic> Diagnostics { get; } = new();
    public Dictionary<string, TimeSpan> StepTimes { get; } = new();
    public int AssetsConverted { get; set; }
    public int AssetsCached { get; set; }
    public BuildReport? Report { get; set; }
    public TimeSpan Elapsed { get; set; }
    public string FailedStep { get; set; } = "";
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == Severity.Error);
}

/// <summary>Generated C++ for user scripts. The scripting compiler (tools/AgbStudio.Scripting) plugs in here.</summary>
public interface IScriptBackend
{
    // Returns the generated .cpp files and the C++ factory for each script behaviour class, or diagnostics.
    ScriptOutput Compile(GameProject project, ScriptInput input, IBuildObserver observer);
}

public sealed class ScriptInput
{
    public required string OutputDir { get; init; }
    public bool Debug { get; init; }
    public Dictionary<string, int> Sounds { get; init; } = new();
    public Dictionary<string, int> Music { get; init; } = new();
    public Dictionary<string, int> Tags { get; init; } = new();
    public Dictionary<string, int> Scenes { get; init; } = new();
    public List<SaveField> GameState { get; init; } = new();
}

public sealed class ScriptOutput
{
    public List<string> Sources { get; } = new();
    public List<string> IncludeDirs { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public int ScriptCount { get; set; }
    // Script class name -> C++ expression creating it with field overrides: (arena, fields) => code
    public Func<string, IReadOnlyDictionary<string, string>, string?> FactoryCall { get; set; } = (_, _) => null;
    public string RegistryHeader { get; set; } = "";
    // Saved flag ids after this compile (null = unchanged): the build keeps them in Project Settings · Save data.
    public Dictionary<string, int>? FlagIds { get; set; }
}

/// <summary>Builds a project into a .gba: scripts, assets, C++ codegen, devkitARM, gbafix, report.</summary>
public sealed partial class GameBuilder
{
    public const string StepScripts = "C# → C++";
    public const string StepAssets = "Convert assets";
    public const string StepCompile = "Compile (devkitARM g++ -O2)";
    public const string StepLink = "Link + gbafix";
    public const int ConverterVersion = 2;  // 2: sprites sharing a palette point at the owner's palette object

    readonly GameProject _p;
    readonly Toolchain _tc;
    readonly IBuildObserver _obs;
    public IScriptBackend? Scripts { get; set; }

    public GameBuilder(GameProject project, Toolchain toolchain, IBuildObserver? observer = null)
    {
        _p = project;
        _tc = toolchain;
        _obs = observer ?? NullObserver.Instance;
    }

    public string GenDir => Path.Combine(_p.BuildDir, "gen");
    public string RomPath => Path.Combine(_p.BuildDir, RomFileName(_p.Settings.Name));

    /// <summary>The ROM file name of a project: its name in lower-case words joined by "_" ("Sky Hopper" → "sky_hopper.gba",
    /// as Export ROM, Build ROM and the Console show it); "game.gba" when the name has no ASCII letter or digit.</summary>
    public static string RomFileName(string projectName)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in projectName + " ")
        {
            if (char.IsAsciiLetterOrDigit(c)) { word.Append(char.ToLowerInvariant(c)); continue; }
            if (word.Length > 0) words.Add(word.ToString());
            word.Clear();
        }
        var name = words.Count == 0 ? "game" : string.Join("_", words);
        return (char.IsAsciiDigit(name[0]) ? "_" + name : name) + ".gba";
    }

    // ---- state shared by the steps ----------------------------------------------------------------
    readonly Dictionary<string, ConvertedSprite> _sprites = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _paletteOwner = new(StringComparer.Ordinal);  // sprite -> sprite whose palette it uses
    readonly Dictionary<string, int> _soundIds = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> _musicIds = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> _sources = new();
    readonly List<string> _includeDirs = new();
    BuildResult _result = new();
    BuildConfiguration _config;
    AssetCache _cache = null!;
    ScriptOutput _scriptOut = new();
    public BuildAssetInfo Assets { get; } = new();

    public BuildResult Build(BuildOptions? options = null)
    {
        options ??= new BuildOptions();
        _result = new BuildResult();
        var sw = Stopwatch.StartNew();
        if (options.Clean && Directory.Exists(_p.BuildDir)) Directory.Delete(_p.BuildDir, true);
        Directory.CreateDirectory(GenDir);
        _cache = new AssetCache(_p.BuildDir);
        var config = options.Configuration ?? _p.Settings.Build.Configuration;
        _config = config;
        try
        {
            if (!Step(StepAssets, () => ConvertAssets())) return Finish(sw);
            if (!Step(StepScripts, () => CompileScripts(config))) return Finish(sw);
            Step("Generate game", () => GenerateGame());
            if (_result.Errors.Any()) return Finish(sw);
            if (options.GenerateOnly)
            {
                _cache.Save();
                _result.Success = true;
                return Finish(sw);
            }
            if (!Step(StepCompile, () => CompileAndLink(config, options.Cancel))) return Finish(sw);
            _cache.Save();
            _result.Elapsed = sw.Elapsed;
            _result.Report = BuildReport.Create(_p, _tc, _result, Assets, config);
            // G-HW.05: the linker already refuses a ROM region overflow; this catches anything that still ends up larger.
            if (BuildReport.RomSizeError(_result.Report.RomBytes) is { } tooBig)
            {
                Add(new Diagnostic(Severity.Error, _result.RomPath, 0, 0, "", tooBig));
                _result.FailedStep = StepLink;
                return Finish(sw);
            }
            _result.Report.Save(_p.BuildDir);
            foreach (var w in _result.Report.Warnings.Where(w => !_result.Diagnostics.Any(d => d.ToString() == w))) Add(new Diagnostic(Severity.Warning, "", 0, 0, "", w));
            _result.Success = true;
        }
        catch (OperationCanceledException)
        {
            Add(new Diagnostic(Severity.Error, "", 0, 0, "", "Build cancelled."));
        }
        return Finish(sw);
    }

    BuildResult Finish(Stopwatch sw)
    {
        _result.Elapsed = sw.Elapsed;
        _result.AssetsCached = _cache.Hits;
        _result.AssetsConverted = _cache.Misses;
        if (!_result.Success)
        {
            var errors = _result.Errors.ToList();
            BuildReport.AddHistory(_p.BuildDir, new BuildReport.HistoryEntry(DateTime.Now, 0, sw.Elapsed.TotalSeconds,
                _result.Diagnostics.Count(d => d.Severity == Severity.Warning), errors.Count, errors.FirstOrDefault()?.ToString() ?? ""));
        }
        return _result;
    }

    // The step running now: the compile step hands over to "Link + gbafix" when ninja starts linking.
    string _step = "";
    Stopwatch _stepWatch = new();
    // The asset file being converted, so an exception thrown while reading it (a broken PNG) names the file (G-ERR.01).
    string _reading = "";

    bool Step(string name, Func<string> body)
    {
        _step = name;
        _stepWatch = Stopwatch.StartNew();
        _reading = "";
        _obs.StepStarted(name, "");
        string detail;
        try
        {
            detail = body();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Add(new Diagnostic(Severity.Error, _reading, 0, 0, "", StepError(name, e)));
            detail = e.Message;
        }
        var ok = !_result.Errors.Any();
        _result.StepTimes[_step] = _stepWatch.Elapsed;
        _obs.StepFinished(_step, _stepWatch.Elapsed, ok, detail);
        if (!ok && _result.FailedStep.Length == 0) _result.FailedStep = _step;
        return ok;
    }

    /// <summary>G-ERR.01: the text of an exception that stopped a step. AGB Studio's own file errors (a broken PNG, a
    /// project file that is not valid JSON) already say what is wrong and what to do; others get the step and a next step.</summary>
    static string StepError(string step, Exception e) => e switch
    {
        InvalidDataException or FormatException or JsonException => e.Message,
        IOException or UnauthorizedAccessException =>
            $"{e.Message} Close other programs that use the file and check that it exists, then build again.",
        _ => $"The step '{step}' stopped: {e.Message} If the build keeps failing here, try Build › Clean Build.",
    };

    void Add(Diagnostic d)
    {
        _result.Diagnostics.Add(d);
        _obs.Message(d);
    }

    void Write(string path, string text)
    {
        SoundbankBuilder.WriteIfChanged(path, text);
        if (path.EndsWith(".cpp", StringComparison.Ordinal) && !_sources.Contains(path)) _sources.Add(path);
    }

    // ---- assets -------------------------------------------------------------------------------------

    string ConvertAssets()
    {
        var spriteFiles = _p.SpriteFiles().ToList();
        var tilesetFiles = _p.TilesetFiles().ToList();
        var total = spriteFiles.Count + tilesetFiles.Count;
        var done = 0;
        // Sprites sharing a palette are converted after their palette owner.
        var metas = spriteFiles.ToDictionary(f => f, f => _p.LoadMeta<SpriteMeta>(f));
        foreach (var f in spriteFiles.OrderBy(f => metas[f].Palette.Length > 0 ? 1 : 0))
        {
            _obs.FileProgress(StepAssets, ++done, total, Path.GetFileName(f));
            _reading = f;
            ConvertSprite(f, metas[f]);
        }
        foreach (var f in tilesetFiles)
        {
            _obs.FileProgress(StepAssets, ++done, total, Path.GetFileName(f));
            _reading = f;
            ConvertTileset(f);
        }
        foreach (var f in _p.ImageFiles())
        {
            _reading = f;
            ConvertImage(f);
        }
        _reading = "";
        ConvertAudio();
        ConvertDmgSfx();
        ConvertText();
        ConvertData();
        return $"{spriteFiles.Count} sprites · {tilesetFiles.Count} tilesets · {_fonts.Count} fonts · {_p.SoundFiles().Count()} sounds";
    }

    void ConvertSprite(string file, SpriteMeta meta)
    {
        var name = GameProject.AssetName(file);
        var id = Cpp.Ident(name);
        // A shared palette (Sprite Editor · Palette) is the owner's palette object, so the runtime gives both sprites
        // one OBJ bank (palettes::acquire is per palette).
        ushort[]? shared = null;
        string? paletteOwner = null;
        if (meta.Palette.Length > 0 && _sprites.TryGetValue(meta.Palette, out var owner))
        {
            shared = owner.Palette;
            paletteOwner = _paletteOwner.GetValueOrDefault(meta.Palette, meta.Palette);
        }
        _paletteOwner[name] = paletteOwner ?? name;
        // An Aseprite file: its canvas is the frame size and its tags are the animations (unless the Sprite Editor set some).
        AsepriteFile? ase = AsepriteFile.IsAseprite(file) ? AsepriteFile.Read(file) : null;
        var img = ase?.ToSheet() ?? ColorImage.Load(file);
        if (ase != null)
        {
            meta.FrameWidth = ase.Width;
            meta.FrameHeight = ase.Height;
        }
        var s = SpriteConverter.Convert(name, img, meta, shared, file);
        foreach (var pr in s.Problems) Add(new Diagnostic(Severity.Warning, file, 0, 0, "", pr.Message));
        _sprites[name] = s;
        var outFile = Path.Combine(GenDir, "assets", $"sprite_{id}.cpp");
        var hash = AssetCache.HashFiles([file, GameProject.MetaPath(file)], ConverterVersion, shared == null ? "" : paletteOwner + ":" + string.Join(',', shared));
        // Loaded: one frame per actor that shows it (the runtime copies frames from ROM to OBJ VRAM), and its own OBJ
        // palette bank unless it shares the owner's.
        Assets.Sprites[name] = new AssetInfo(name, "Sprite", file, s.Tiles.Length + (shared == null ? 32 : 0), s.FrameCount, $"{s.SourceWidth}×{s.SourceHeight}")
            { Vram = "OBJ", VramTileBytes = s.TilesPerFrame * 32L, PaletteBytes = shared == null ? 32 : 0 };
        if (_cache.IsCurrent("sprite:" + name, hash, [outFile]))
        {
            _sources.Add(outFile);
            return;
        }
        var sb = new StringBuilder();
        sb.Append("// Generated by AGB Studio from ").Append(Path.GetFileName(file)).Append(". Do not edit.\n#include \"agb/data.hpp\"\n");
        var palId = Cpp.Ident(paletteOwner ?? name);
        if (paletteOwner != null) sb.Append($"extern const agb::data::Palette agbasset_sprite_{palId}_pal;\n");
        sb.Append($"namespace {{\nalignas(4) ");
        Cpp.Bytes(sb, $"const unsigned int agbasset_sprite_{id}_tiles", s.Tiles);
        if (paletteOwner == null) Cpp.U16(sb, $"const unsigned short agbasset_sprite_{id}_colors", s.Palette.Select(c => (int)c));
        var anims = meta.Animations.Count > 0 ? meta.Animations : ase?.Animations(s.SourceFrames) is { Count: > 0 } tagged ? tagged
            : [new AnimationData { Name = "idle", Frames = Enumerable.Range(0, s.FrameCount).ToList() }];
        for (var i = 0; i < anims.Count; i++)
        {
            var a = anims[i];
            var frames = a.Frames.Where(f => f < s.FrameCount).ToList();
            if (frames.Count == 0) frames.Add(0);
            Cpp.U16(sb, $"const unsigned char agbasset_sprite_{id}_a{i}f", frames);
            Cpp.U16(sb, $"const unsigned char agbasset_sprite_{id}_a{i}t", frames.Select((_, k) => a.FrameTicks != null && k < a.FrameTicks.Count ? a.FrameTicks[k] : a.Ticks));
        }
        sb.Append($"const agb::data::AnimDef agbasset_sprite_{id}_anims[] = {{\n");
        for (var i = 0; i < anims.Count; i++)
        {
            var a = anims[i];
            var n = Math.Max(1, a.Frames.Count(f => f < s.FrameCount));
            sb.Append($"    {{{Cpp.Str(a.Name)}, agbasset_sprite_{id}_a{i}f, agbasset_sprite_{id}_a{i}t, {n}, {(a.Loop ? "true" : "false")}, {(a.PingPong ? "true" : "false")}}},\n");
        }
        sb.Append("};\n}  // namespace\n");
        if (paletteOwner == null) sb.Append($"extern const agb::data::Palette agbasset_sprite_{id}_pal{{agbasset_sprite_{id}_colors, 1}};\n");
        var c = meta.Collider;
        sb.Append($"extern const agb::data::SpriteSheet agbasset_sprite_{id} = {{\n");
        sb.Append($"    {{agbasset_sprite_{id}_tiles, {s.Tiles.Length}, {s.FrameCount * s.TilesPerFrame}, 4, false}},\n");
        sb.Append($"    &agbasset_sprite_{palId}_pal, {s.FrameWidth}, {s.FrameHeight}, static_cast<agb::data::ObjShape>({s.Shape}), {s.SizeCode},\n");
        sb.Append($"    {s.TilesPerFrame}, {s.FrameCount}, agbasset_sprite_{id}_anims, {anims.Count}, {meta.OriginX}, {meta.OriginY},\n");
        sb.Append($"    {c.X}, {c.Y}, {c.W}, {c.H}}};\n");
        Write(outFile, sb.ToString());
        _cache.Store("sprite:" + name, hash);
    }

    readonly Dictionary<string, ConvertedTileset> _tilesets = new(StringComparer.Ordinal);

    void ConvertTileset(string file)
    {
        var name = GameProject.AssetName(file);
        if (_tilesets.ContainsKey(name)) return;  // already built as another tileset's primary
        var id = Cpp.Ident(name);
        var meta = _p.LoadMeta<TilesetMeta>(file);
        var outFile = Path.Combine(GenDir, "assets", $"tileset_{id}.cpp");
        var img = ColorImage.Load(file);
        ConvertedTileset? primary = null;
        if (!string.IsNullOrEmpty(meta.Primary) && meta.Primary != name)
        {
            // P5.01: the primary is converted first; this tileset goes on from its tiles and palette banks. The merged
            // tileset stores only its own tiles, and the runtime loads the primary's before them (data::Tileset::primary).
            if (!_tilesets.TryGetValue(meta.Primary, out primary))
            {
                var pf = Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.png").FirstOrDefault(f => GameProject.AssetName(f) == meta.Primary);
                if (pf != null) { ConvertTileset(pf); _tilesets.TryGetValue(meta.Primary, out primary); }
            }
            if (primary == null) Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"The primary tileset '{meta.Primary}' was not found, so '{name}' is built on its own."));
        }
        var t = TilesetConverter.Convert(name, img, meta, _p.Settings.Tags, 13, file, primary);
        _tilesets[name] = t;
        foreach (var pr in t.Problems) Add(new Diagnostic(Severity.Warning, file, 0, 0, "", pr.Message));
        var tiles = t.OwnTileBytes();
        var compress = meta.Compress && _p.Settings.Build.CompressGraphics;
        var stored = compress ? Lz77.Compress(tiles) : tiles;
        // Loaded: every tile in BG VRAM (LZ77 tiles are decompressed by the BIOS straight into VRAM, no RAM buffer) and
        // its palette banks.
        Assets.Tilesets[name] = new AssetInfo(name, "Tileset", file, stored.Length + t.Palette.Banks.Count * 32 + t.Metatiles.Count * 10, t.Tiles.Count,
            $"{t.Tiles.Count} tiles · 4bpp{(t.Primary != null ? $" · {t.OwnTileCount} own, primary {t.Primary.Name}" : "")}") { Vram = "BG", VramTileBytes = t.Tiles.Count * 32L, PaletteBytes = t.Palette.Banks.Count * 32L };
        var hash = AssetCache.HashFiles([file, GameProject.MetaPath(file)], ConverterVersion, compress, string.Join(',', _p.Settings.Tags), primary == null ? "" : Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(primary.Tiles.ToBytes())));
        if (_cache.IsCurrent("tileset:" + name, hash, [outFile]))
        {
            _sources.Add(outFile);
            return;
        }
        var sb = new StringBuilder();
        sb.Append("// Generated by AGB Studio from ").Append(Path.GetFileName(file)).Append(". Do not edit.\n#include \"agb/data.hpp\"\n");
        sb.Append("namespace {\nalignas(4) ");
        Cpp.Bytes(sb, $"const unsigned int agbasset_tileset_{id}_tiles", stored);
        Cpp.U16(sb, $"const unsigned short agbasset_tileset_{id}_colors", t.Palette.Flatten().Select(c => (int)c));
        sb.Append($"const agb::data::Palette agbasset_tileset_{id}_pal{{agbasset_tileset_{id}_colors, {t.Palette.Banks.Count}}};\n");
        sb.Append($"const agb::data::Metatile agbasset_tileset_{id}_meta[] = {{\n");
        foreach (var m in t.Metatiles) sb.Append($"    {{{{{m[0]}, {m[1]}, {m[2]}, {m[3]}}}}},\n");
        sb.Append("};\n");
        sb.Append($"const agb::data::Collision agbasset_tileset_{id}_col[] = {{");
        sb.Append(string.Join(", ", t.Collision.Select(c => "agb::data::Collision::" + c)));
        sb.Append("};\n");
        Cpp.U16(sb, $"const unsigned char agbasset_tileset_{id}_tags", t.Tags.Select(x => (int)x));
        for (var a = 0; a < t.Anims.Count; a++)
        {
            sb.Append("alignas(4) ");
            Cpp.Bytes(sb, $"const unsigned int agbasset_tileset_{id}_anim{a}", t.Anims[a].FrameTiles);
        }
        if (t.Anims.Count > 0)
        {
            sb.Append($"const agb::data::TileAnim agbasset_tileset_{id}_anims[] = {{\n");
            for (var a = 0; a < t.Anims.Count; a++)
                sb.Append($"    {{{t.Anims[a].BaseTile}, {t.Anims[a].Frames}, {t.Anims[a].Ticks}, agbasset_tileset_{id}_anim{a}}},\n");
            sb.Append("};\n");
        }
        sb.Append("}  // namespace\n");
        sb.Append($"extern const agb::data::Tileset agbasset_tileset_{id} = {{\n");
        sb.Append($"    {{agbasset_tileset_{id}_tiles, {stored.Length}, {t.OwnTileCount}, 4, {(compress ? "true" : "false")}}},\n");
        sb.Append($"    &agbasset_tileset_{id}_pal, agbasset_tileset_{id}_meta, agbasset_tileset_{id}_col, agbasset_tileset_{id}_tags,\n");
        sb.Append($"    {t.Metatiles.Count}, {(t.Anims.Count > 0 ? $"agbasset_tileset_{id}_anims" : "nullptr")}, {t.Anims.Count}}};\n");
        Write(outFile, sb.ToString());
        _cache.Store("tileset:" + name, hash);
    }

    readonly Dictionary<string, (int W, int H, int Tiles, int Banks)> _images = new(StringComparer.Ordinal);

    // Image layers (sky, hills, title art): 8x8 tiles and screen entries, wrapped as a tileset without metatiles.
    void ConvertImage(string file)
    {
        var name = GameProject.AssetName(file);
        var id = Cpp.Ident(name);
        var outFile = Path.Combine(GenDir, "assets", $"image_{id}.cpp");
        var img = ColorImage.Load(file);
        var (t, cells, w, h) = TilesetConverter.ConvertImage(name, img);
        _images[name] = (w, h, t.Tiles.Count, t.Palette.Banks.Count);
        var tiles = t.Tiles.ToBytes();
        var compress = _p.Settings.Build.CompressGraphics;
        var stored = compress ? Lz77.Compress(tiles) : tiles;
        // Loaded: its tiles and palette banks; the screen blocks and map shadow belong to the map layer that shows it.
        Assets.Tilesets[name] = new AssetInfo(name, "Image", file, stored.Length + cells.Length * 2, t.Tiles.Count, $"{w}x{h} tiles")
            { Vram = "BG", VramTileBytes = t.Tiles.Count * 32L, PaletteBytes = t.Palette.Banks.Count * 32L };
        var hash = AssetCache.HashFiles([file], ConverterVersion, compress);
        if (_cache.IsCurrent("image:" + name, hash, [outFile]))
        {
            _sources.Add(outFile);
            return;
        }
        var sb = new StringBuilder();
        sb.Append("// Generated by AGB Studio from ").Append(Path.GetFileName(file)).Append(". Do not edit.\n#include \"agb/data.hpp\"\n");
        ImageTilesetCpp(sb, id, t, cells, stored, compress);
        Write(outFile, sb.ToString());
        _cache.Store("image:" + name, hash);
    }

    // An image as a tileset without metatiles plus its screen entries (Image layers, the boot splash).
    static void ImageTilesetCpp(StringBuilder sb, string id, ConvertedTileset t, ushort[] cells, byte[] stored, bool compress)
    {
        sb.Append("namespace {\nalignas(4) ");
        Cpp.Bytes(sb, $"const unsigned int agbasset_tileset_{id}_tiles", stored);
        Cpp.U16(sb, $"const unsigned short agbasset_tileset_{id}_colors", t.Palette.Flatten().Select(c => (int)c));
        sb.Append($"const agb::data::Palette agbasset_tileset_{id}_pal{{agbasset_tileset_{id}_colors, {t.Palette.Banks.Count}}};\n}}  // namespace\n");
        Cpp.U16(sb, $"extern const unsigned short agbasset_tileset_{id}_cells", cells.Select(c => (int)c));
        sb.Append($"extern const agb::data::Tileset agbasset_tileset_{id} = {{\n");
        sb.Append($"    {{agbasset_tileset_{id}_tiles, {stored.Length}, {t.Tiles.Count}, 4, {(compress ? "true" : "false")}}},\n");
        sb.Append($"    &agbasset_tileset_{id}_pal, nullptr, nullptr, nullptr, 0, nullptr, 0}};\n");
    }

    int _dmgCount;

    /// <summary>SFX Designer effects played on the DMG channels (audio/sfx/*.sfx without "Bake to .wav"; baked ones are
    /// .wav files in assets/audio/sfx): a register table in the ROM and Sound.Play ids with the high bit set.</summary>
    void ConvertDmgSfx()
    {
        // A .wav of the same name in assets/audio/sfx (baked, or replaced by a sample) is the one that plays.
        var samples = _p.SoundFiles().Select(GameProject.AssetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = _p.SfxFiles().Select(f => (File: f, Sfx: GameProject.Load<SfxFile>(f)))
            .Where(x => !x.Sfx.Bake && !samples.Contains(GameProject.AssetName(x.File))).ToList();
        _dmgCount = files.Count;
        var ids = new StringBuilder("// Generated by AGB Studio (SFX Designer effects). Do not edit.\n#pragma once\nnamespace agb_sound {\n");
        var sb = new StringBuilder("// Generated by AGB Studio (SFX Designer effects). Do not edit.\n#include \"agb/audio.hpp\"\nnamespace {\n");
        var rows = new StringBuilder();
        for (var i = 0; i < files.Count; i++)
        {
            var name = GameProject.AssetName(files[i].File);
            var img = Audio.DmgRegisters.Encode(files[i].Sfx);
            var id = 0x8000 | i;
            _soundIds[name] = id;
            ids.Append($"constexpr unsigned short SFX_{Cpp.Ident(name).ToUpperInvariant()} = {id};\n");
            if (img.Wave != null) sb.Append($"const agb::u32 kWave{i}[4] = {{{string.Join(", ", img.Wave.Select(w => $"0x{w:X8}u"))}}};\n");
            rows.Append($"    {{agb::audio::DmgChannel::{img.Channel}, 0x{img.Sweep:X4}, 0x{img.Envelope:X4}, 0x{img.Frequency:X4}, {(img.Wave != null ? $"kWave{i}" : "nullptr")}, {Math.Min(255, img.Frames)}}},  // {name}\n");
            Assets.Audio[name] = new AssetInfo(name, "Sound", files[i].File, 16, 0, $"DMG {img.Channel}");
        }
        ids.Append("}  // namespace agb_sound\n");
        sb.Append("}  // namespace\n");
        sb.Append(files.Count > 0 ? $"extern const agb::audio::DmgSfx agb_dmg_sfx[] = {{\n{rows}}};\n" : "");
        SoundbankBuilder.WriteIfChanged(Path.Combine(GenDir, "agb_dmg.hpp"), ids.ToString());
        var cpp = Path.Combine(GenDir, "assets", "dmg_sfx.cpp");
        Write(cpp, sb.ToString());
        if (files.Count > 0) _sources.Add(cpp);
    }

    /// <summary>
    /// Cache key of the soundbank: the music and sound files and the music's meta files, since MusicMeta (volume, loop
    /// start, fade on scene change) is built into the soundbank.
    /// </summary>
    public static string SoundbankKey(IReadOnlyList<string> inputs) =>
        AssetCache.HashFiles(inputs.Concat(SoundbankBuilder.MetaFiles(inputs)), ConverterVersion, "music-meta-1");

    void ConvertAudio()
    {
        var inputs = _p.MusicFiles().Concat(_p.SoundFiles()).ToList();
        if (inputs.Count == 0) return;
        // G-HW.08: every module fits in the mixing channels Maxmod is given.
        var channelProblems = Audio.AudioChannels.Check(_p.Settings.Audio.Channels, _p.MusicFiles());
        if (Audio.AudioChannels.MixRateProblem(_p.Settings.Audio.MixKHz) is { } rateProblem) channelProblems.Add(rateProblem);
        foreach (var problem in channelProblems) Add(new Diagnostic(Severity.Error, _p.ProjectPath, 0, 0, "", problem));
        if (channelProblems.Count > 0) return;
        var outDir = Path.Combine(GenDir, "audio");
        var hash = SoundbankKey(inputs);
        var cpp = Path.Combine(outDir, "soundbank.cpp");
        var hpp = Path.Combine(outDir, "soundbank.hpp");
        if (!_cache.IsCurrent("soundbank", hash, [cpp, hpp]))
        {
            var r = SoundbankBuilder.Build(_tc, inputs, outDir);
            if (!r.Success)
            {
                // mmutil converts every track in one run: its whole output goes to the log, the error names the file
                // it stopped on (the last one its output mentions) and its first error line.
                _obs.Line(r.Log.Trim());
                var lines = r.Log.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var reason = lines.FirstOrDefault(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)) ?? lines.LastOrDefault() ?? "mmutil gave no reason";
                var failed = inputs.MaxBy(f => r.Log.LastIndexOf(Path.GetFileName(f), StringComparison.OrdinalIgnoreCase));
                if (failed != null && !r.Log.Contains(Path.GetFileName(failed), StringComparison.OrdinalIgnoreCase)) failed = null;
                Add(new Diagnostic(Severity.Error, failed ?? "", 0, 0, "", $"Audio conversion failed{(failed != null ? $" for {Path.GetFileName(failed)}" : "")}: {reason.TrimEnd('.')}. " +
                    "Open the file in a tracker or audio editor, save it again as MOD, XM or WAV, and build again."));
                return;
            }
            _cache.Store("soundbank", hash);
        }
        _sources.Add(cpp);
        _includeDirs.Add(outDir);
        foreach (var line in File.ReadAllLines(hpp))
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, @"constexpr unsigned short (\w+) = (\d+);");
            if (!m.Success) continue;
            var sym = m.Groups[1].Value;
            var val = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (sym.StartsWith("MOD_", StringComparison.Ordinal)) _musicIds[sym[4..]] = val;
            else if (sym.StartsWith("SFX_", StringComparison.Ordinal)) _soundIds[sym[4..]] = val;
        }
        foreach (var f in inputs)
        {
            var n = GameProject.AssetName(f);
            Assets.Audio[n] = new AssetInfo(n, f.EndsWith(".wav") ? "Sound" : "Music", f, (int)new FileInfo(f).Length, 0, "");
        }
    }

    // ---- fonts and strings ----------------------------------------------------------------------------

    readonly Dictionary<string, Fonts.TextCodegen.FontOut> _fonts = new(StringComparer.Ordinal);
    // Each built language's strings (fallbacks filled in) with its font and box widths, for the text layer budget.
    // A string on the default width follows Project Settings · Dialogue box: inside its padding, as many lines as fit.
    int TextBoxWidth(int width)
    {
        if (width != 224 || _p.Settings.Ui.Dialogue is not { } d) return width;
        return Math.Clamp(d.W / 8 * 8 - d.PaddingLeft - d.PaddingRight, 8, 224);
    }

    int TextBoxLines(int lines, int lineHeight)
    {
        if (lines != 3 || _p.Settings.Ui.Dialogue is not { } d || lineHeight <= 0) return lines;
        return Math.Max(1, (d.H / 8 * 8 - d.PaddingTop - d.PaddingBottom) / lineHeight);
    }

    sealed record TextTable(string Language, Dictionary<string, string> Strings, Fonts.TextCodegen.FontOut Font, Func<string, int> BoxWidth);
    readonly List<TextTable> _texts = new();
    readonly List<string> _stringKeys = new();
    readonly List<string> _languages = new();
    public IReadOnlyList<string> StringKeys => _stringKeys;
    public int StringKey(string key) => _stringKeys.IndexOf(key);

    void ConvertText()
    {
        var tables = _p.StringTables().Select(f => (File: f, Table: GameProject.Load<StringTable>(f))).ToList();
        // The options screen names every language in its own script ("ไทย · English"): one key per language.
        var langs = _p.Settings.Localization.Languages;
        var projectKeys = tables.SelectMany(t => t.Table.Strings.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
        var allText = string.Concat(tables.SelectMany(t => t.Table.Strings.Values)) + (langs.Count > 1 ? string.Concat(langs.Select(l => l.Name)) : "");
        var used = Fonts.ThaiText.CodePoints(allText).Where(c => c >= 0x20).Distinct().ToList();
        foreach (var f in _p.FontFiles())
        {
            _reading = f;
            var ff = GameProject.Load<FontFile>(f);
            if (ff.Name.Length == 0) ff.Name = GameProject.AssetName(f);
            var font = Fonts.FontBuilder.Build(ff, _p.FontsDir, used);
            var fo = new Fonts.TextCodegen.FontOut { Font = font, Symbol = $"agbfont_{Cpp.Ident(ff.Name)}" };
            var cpp = Fonts.TextCodegen.FontCpp(fo);  // also fills the glyph index the strings use
            _fonts[ff.Name] = fo;
            Assets.Fonts[ff.Name] = new AssetInfo(ff.Name, "Font", f, font.RomBytes, font.Glyphs.Count, $"{font.Glyphs.Count} glyphs, {font.Missing.Count} missing");
            Write(Path.Combine(GenDir, "text", $"font_{Cpp.Ident(ff.Name)}.cpp"), cpp);
        }
        _reading = "";
        if (tables.Count == 0) return;
        var loc = _p.Settings.Localization;
        var metaPath = Path.Combine(_p.TextDir, "strings.meta.json");
        var meta = File.Exists(metaPath) ? GameProject.Load<StringMeta>(metaPath) : new StringMeta();
        // Project keys sorted (the order the scripting compiler uses for string ids), then the language names.
        _stringKeys.AddRange(projectKeys);
        if (langs.Count > 1) _stringKeys.AddRange(langs.Select(l => LanguageKey(l.Code)).Distinct().Where(k => !projectKeys.Contains(k)));
        var order = tables.OrderBy(t => t.Table.Language == loc.Source ? 0 : 1).ThenBy(t => t.Table.Language, StringComparer.Ordinal).ToList();
        foreach (var (file, table) in order)
        {
            var lang = table.Language;
            var info = loc.Languages.FirstOrDefault(l => l.Code == lang);
            var fontName = info?.Font is { Length: > 0 } fn ? fn : _p.Settings.General.DefaultFont;
            if (!_fonts.TryGetValue(fontName, out var font))
            {
                font = _fonts.Values.FirstOrDefault();
                if (font == null)
                {
                    Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Strings for '{lang}' need a font: add one in Font Manager."));
                    return;
                }
                Add(new Diagnostic(Severity.Warning, file, 0, 0, "", info?.Font is { Length: > 0 } missingFont
                    ? $"Language '{lang}' uses font '{missingFont}', which is not in fonts/, so {font.Font.Name} is used. Add the font in Font Manager, or pick another one in Localization · Languages."
                    : $"Language '{lang}' has no font set, so {font.Font.Name} is used. Pick a font for it in Localization · Languages."));
            }
            // Language names where this language's font can draw them; elsewhere the options screen shows the code.
            if (langs.Count > 1)
                foreach (var l in langs)
                    if (Fonts.ThaiText.CodePoints(l.Name).All(cp => cp < 0x20 || font.Font.Get(cp) != null)) table.Strings[LanguageKey(l.Code)] = l.Name;
            var missing = _stringKeys.Where(k => !table.Strings.ContainsKey(k) && !k.StartsWith("_lang.", StringComparison.Ordinal)).ToList();
            if (missing.Count > 0 && lang != loc.Source)
                Add(new Diagnostic(loc.FailOnMissing ? Severity.Error : Severity.Warning, file, 0, 0, "", $"{missing.Count} strings missing in {info?.Name ?? lang}. Translate them in Localization (\"Missing only\"), or set a fallback language in Localization · Languages."));
            // A missing string takes the language's own Fallback (Localization · Languages), else "[key]" in Debug
            // builds (Show missing keys), else the game's fallback language at runtime.
            var strings = new Dictionary<string, string>(table.Strings);
            var fallback = info?.Fallback is { Length: > 0 } fb && fb != lang ? tables.FirstOrDefault(t => t.Table.Language == fb).Table : null;
            if (lang != loc.Source)
                foreach (var k in missing)
                {
                    if (fallback != null && fallback.Strings.TryGetValue(k, out var text)) strings[k] = text;
                    else if (loc.ShowMissingKeys && _config == BuildConfiguration.Debug) strings[k] = "[" + k + "]";
                }
            var diags = new List<Diagnostic>();
            var cpp = Fonts.TextCodegen.StringsCpp(lang, _stringKeys, strings, font, k =>
            {
                var m = meta.Keys.GetValueOrDefault(k);
                return (TextBoxWidth(m?.BoxWidth ?? 224), TextBoxLines(m?.MaxLines ?? 3, font.Font.LineHeight));
            }, loc.ThaiWrap, diags, file);
            foreach (var d in diags) Add(d);
            Write(Path.Combine(GenDir, "text", $"strings_{Cpp.Ident(lang)}.cpp"), cpp);
            _languages.Add(lang);
            _texts.Add(new TextTable(lang, strings, font, k => TextBoxWidth(meta.Keys.GetValueOrDefault(k)?.BoxWidth ?? 224)));
        }
    }

    static string LanguageKey(string code) => "_lang." + code;

    // ---- database ------------------------------------------------------------------------------------

    public IReadOnlyList<TableFile> Tables { get; private set; } = [];

    void ConvertData()
    {
        var tables = _p.TableFiles().Select(GameProject.Load<TableFile>).ToList();
        Tables = tables;
        if (tables.Count == 0) return;
        var diags = new List<Diagnostic>();
        var o = Data.DatabaseCodegen.Generate(tables, StringKey, n => SoundId(n), n => _sprites.ContainsKey(n), diags, _p.DataDir, scriptCells: Scripts != null);
        foreach (var d in diags) Add(d);
        SoundbankBuilder.WriteIfChanged(Path.Combine(GenDir, "agb_db.hpp"), o.Header);
        foreach (var (file, text) in o.Sources) Write(Path.Combine(GenDir, "data", file), text);
        foreach (var t in tables)
            Assets.Data[t.Name] = new AssetInfo(t.Name, "Data", Path.Combine(_p.DataDir, t.Name + ".db"),
                Data.DatabaseCodegen.TableBytes(t), t.Rows.Count, $"{t.Rows.Count} rows, {t.Fields.Count} fields");
    }

    public int SoundId(string name) => _soundIds.TryGetValue(SoundKey(name), out var id) ? id : -1;
    public int MusicId(string name) => _musicIds.TryGetValue(SoundKey(name), out var id) ? id : -1;
    static string SoundKey(string name) => Cpp.Ident(Path.GetFileNameWithoutExtension(name)).ToUpperInvariant();

    // ---- scripts ------------------------------------------------------------------------------------

    string CompileScripts(BuildConfiguration config)
    {
        var scriptFiles = _p.ScriptFiles().ToList();
        if (Scripts == null)
        {
            if (scriptFiles.Count > 0)
                Add(new Diagnostic(Severity.Warning, "", 0, 0, "", $"{scriptFiles.Count} C# scripts were not compiled: this build tool has no script compiler, so the game runs without them. Build from the AGB Studio editor or with agb build."));
            return "0 scripts";
        }
        var input = new ScriptInput
        {
            OutputDir = Path.Combine(GenDir, "scripts"),
            Debug = config == BuildConfiguration.Debug,
            Sounds = new(_soundIds, StringComparer.OrdinalIgnoreCase),
            Music = new(_musicIds, StringComparer.OrdinalIgnoreCase),
            Tags = _p.Settings.Tags.Select((t, i) => (t, i + 1)).ToDictionary(x => x.t, x => x.Item2),
            Scenes = SceneNames().Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i),
            GameState = _p.Settings.Save.Fields,
        };
        _scriptOut = Scripts.Compile(_p, input, _obs);
        foreach (var d in _scriptOut.Diagnostics) Add(d);
        // Flag ids are part of the save format, so they live in the project file (versioned with the game), not in
        // build/ (P4.08).
        if (_scriptOut.FlagIds is { } ids && !SameIds(ids, _p.Settings.Save.FlagIds))
        {
            _p.Settings.Save.FlagIds = ids.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value);
            _p.SaveSettings();
        }
        _sources.AddRange(_scriptOut.Sources);
        _includeDirs.AddRange(_scriptOut.IncludeDirs);
        return $"{_scriptOut.ScriptCount} scripts";
    }

    static bool SameIds(Dictionary<string, int> a, Dictionary<string, int> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    List<string> SceneNames() => _p.SceneFiles().Select(GameProject.AssetName).ToList();

    // The scripting compiler ran and wrote its registry (scene hooks, link conditions, agb_scripts_init).
    bool ScriptsCompiled => _scriptOut.RegistryHeader.Length > 0;

    // ---- game codegen -------------------------------------------------------------------------------

    string GenerateGame()
    {
        var scenes = _p.SceneFiles().Select(f => (File: f, Scene: GameProject.Load<SceneFile>(f))).ToList();
        var names = scenes.Select(s => s.Scene.Name.Length > 0 ? s.Scene.Name : GameProject.AssetName(s.File)).ToList();
        _maps.Clear();
        foreach (var (file, scene) in scenes) GenerateScene(file, scene, names);
        GeneratePrefabs();
        GenerateUiPages(names);
        foreach (var (file, scene) in scenes) CheckHardwareLimits(file, scene);
        CheckSaveFits();
        var splash = GenerateSplash();
        var ui = GenerateUiKit(names, _hasPages || scenes.Any(s => s.Scene.Menu != null));
        GenerateGameDef(names, splash, ui);
        return $"{scenes.Count} scenes";
    }

    public const int BgVramBytes = 64 * 1024;
    public const int SceneBgBanks = 14;  // banks 14 and 15 hold the menus and the text
    public const int ObjVramBytes = 32 * 1024;        // OBJ tiles in the tile modes 0-2
    public const int ObjVramBitmapBytes = 16 * 1024;  // modes 3-5: the bitmap takes the lower half of OBJ VRAM
    public const int ObjBanks = 16;
    public const int DialogueBoxBytes = 28 * 5 * 32;  // text::BoxStyle default: 28 x 5 tiles (Project Settings · Dialogue box may differ a little)
    /// <summary>A regular (tile or image) layer: 64 x 32 screen entries (bg::Size::S64x32, two screen blocks) in BG VRAM,
    /// and the same bytes again for its map shadow in the screen arena (EWRAM).</summary>
    public const int LayerMapBytes = 64 * 32 * 2;
    /// <summary>The boot splash layer: 32 x 32 screen entries (one screen block) and its shadow.</summary>
    public const int SplashMapBytes = 32 * 32 * 2;
    /// <summary>A rotate / scale layer needs a whole empty character block for its 8bpp tiles.</summary>
    public const int AffineTileBlockBytes = 16 * 1024;
    public const int MaxTextLabels = 12;               // text::addLabel slots

    /// <summary>G-HW.01 / G-HW.03: what a scene puts in VRAM and palette RAM at once, against the hardware: background
    /// tiles, maps and the text layer (labels, dialogue box, menu) in the 64 KB of BG VRAM; the sprite frames its actors
    /// can show at once in OBJ VRAM; BG and OBJ palette banks. Over a limit the build fails, naming the scene, the
    /// numbers and what to change. The numbers go to report.json (Scene budgets).</summary>
    void CheckHardwareLimits(string file, SceneFile scene)
    {
        var tiles = 0L;
        var banks = 0;
        var mapPath = Path.Combine(_p.MapsDir, Path.GetFileNameWithoutExtension(scene.Map) + ".map");
        if (scene.Map.Length > 0 && File.Exists(mapPath))
        {
            // The same numbers as the assets' own (report.json assets): each layer's map, the tileset's tiles once,
            // each image layer's tiles.
            var map = GameProject.Load<MapFile>(mapPath);
            var tilesetLoaded = false;
            foreach (var l in map.Layers)
            {
                var cost = LayerCost(map, l);
                tiles += cost.Tiles + cost.Map;
                banks += cost.Banks;
                if (l.Type == LayerType.Image && _images.TryGetValue(l.Image, out var img))
                {
                    tiles += img.Tiles * 32L;
                    banks += img.Banks;
                }
                else if (l.Type == LayerType.Tile && !tilesetLoaded && _tilesets.TryGetValue(map.Tileset, out var ts))
                {
                    tilesetLoaded = true;
                    tiles += ts.Tiles.Count * 32L;
                    banks += ts.Palette.Banks.Count;
                }
            }
        }
        var text = TextLayerBytes(file, scene);
        tiles += 2048 + text;  // the text layer's map, its blank tile and the tiles of its boxes
        if (tiles > BgVramBytes)
            Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Scene {scene.Name} needs {tiles / 1024} KB of background VRAM; the GBA has 64 KB. " +
                "Use fewer layers, a smaller tileset, or image layers with more repeated tiles."));
        else if (tiles + _largestPageBytes > BgVramBytes)
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Scene {scene.Name} leaves too little background VRAM for the largest UI page ({_largestPageBytes / 1024} KB), so that page won't open in this scene. Make the page smaller, or use a smaller tileset."));
        if (banks > SceneBgBanks)
            Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Scene {scene.Name} needs {banks} background palettes; {SceneBgBanks} are free " +
                "(of 16, two hold the menus and text). Reduce the colours of its tileset or images (Import Asset \"Reduce to 16\")."));

        // Sprites: each actor shows one frame at a time; actors of the same sprite share a frame when they show the
        // same one (runtime frame cache), so a sprite needs at most min(actors, frames) frames at once.
        var obj = 0L;
        var palettes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in scene.Actors.Where(a => a.Sprite.Length > 0 && _sprites.ContainsKey(a.Sprite)).GroupBy(a => a.Sprite))
        {
            var s = _sprites[g.Key];
            obj += Math.Min(g.Count(), Math.Max(1, s.FrameCount)) * s.TilesPerFrame * 32L;
            palettes.Add(_paletteOwner.GetValueOrDefault(g.Key, g.Key));
        }
        var bitmap = scene.VideoMode >= 3;
        var objCap = bitmap ? ObjVramBitmapBytes : ObjVramBytes;
        if (obj > objCap)
            Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Scene {scene.Name} needs up to {(obj + 1023) / 1024} KB of OBJ VRAM for the sprite frames " +
                $"its actors show at once; the GBA has {objCap / 1024} KB{(bitmap ? " in bitmap modes" : "")}. Use fewer or smaller sprites, " +
                "or fewer different animation frames for actors that share a sprite."));
        if (palettes.Count > ObjBanks)
            Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Scene {scene.Name} needs {palettes.Count} sprite palettes; the GBA has {ObjBanks}. " +
                "Let sprites share a palette (Sprite Editor · Palette) or use fewer different sprites in the scene."));
        Assets.Scenes[scene.Name] = new SceneLimits(tiles, text, banks, obj, palettes.Count);
    }

    /// <summary>What one layer of a map takes when a scene loads it, besides the tiles of its tileset or image: the screen
    /// blocks of its map in BG VRAM and the map shadow in the screen arena (tile and image layers, MapLayerView::init);
    /// for a rotate / scale layer its 8bpp tiles (a whole character block), its one-byte-per-cell map and its palette
    /// banks (affine::load copies the map straight to VRAM, no shadow). Collision-only layers stay in ROM.</summary>
    (long Tiles, long Map, long Ram, int Banks) LayerCost(MapFile map, MapLayerData l)
    {
        switch (l.Type)
        {
            case LayerType.CollisionOnly:
                return default;
            case LayerType.Affine:
                if (!_tilesets.TryGetValue(map.Tileset, out var ts)) return default;
                var side = AffineSide(map);
                return (AffineTileBlockBytes, side * side, 0, ts.Palette.Banks.Count);
            default:
                return (0, LayerMapBytes, LayerMapBytes, 0);
        }
    }

    /// <summary>The hardware size of a rotate / scale map in 8x8 tiles (16, 32, 64 or 128; AffineConverter pads to it).</summary>
    static int AffineSide(MapFile map)
    {
        var side = 16;
        while (side < Math.Max(map.Width, map.Height) * 2 && side < 128) side *= 2;
        return side;
    }

    // Text layer tiles a scene reserves (runtime textdb.cpp / ui.cpp): the blank tile, one box per Text Label actor
    // (240 - x pixels wide, its lines high), the dialogue box when the game has text, the scene's menu panel.
    long TextLayerBytes(string file, SceneFile scene)
    {
        var bytes = 32L;
        var labels = 0;
        var textActors = scene.Actors.Where(a => !a.Hidden && (a.Kind is ActorKind.TextLabel or ActorKind.Panel) && a.Text.Length > 0 && StringKey(a.Text) >= 0).ToList();
        var footprints = new List<(ActorData Actor, int YStart, int YEnd, int HeightPx)>();

        foreach (var a in textActors)
        {
            var maxLines = 1;
            var maxFontLh = 16;
            foreach (var t in _texts)
            {
                if (t.Strings.TryGetValue(a.Text, out var s))
                {
                    var lines = Math.Max(1, Fonts.TextLayout.Layout(s, t.Font.Font, t.BoxWidth(a.Text), _p.Settings.Localization.ThaiWrap).Lines);
                    maxLines = Math.Max(maxLines, lines);
                    maxFontLh = Math.Max(maxFontLh, t.Font.Font.LineHeight);
                }
            }
            var padTop = a.Kind == ActorKind.Panel ? (a.Padding?.Top ?? 0) : 0;
            var padBottom = a.Kind == ActorKind.Panel ? (a.Padding?.Bottom ?? 0) : 0;
            var heightPx = padTop + (maxLines * maxFontLh) + padBottom;
            var heightTiles = Math.Max(1, (heightPx + 7) / 8);

            var (wx, wy) = a.GetWorldPosition(scene.Actors);
            bytes += Math.Max(1, (240 - wx + 7) / 8) * heightTiles * 32L;
            labels++;

            var yStartTile = wy / 8;
            var yEndTile = (wy + heightPx + 7) / 8;
            footprints.Add((a, yStartTile, yEndTile, heightPx));
        }

        for (var i = 0; i < footprints.Count; i++)
        {
            for (var j = i + 1; j < footprints.Count; j++)
            {
                var f1 = footprints[i];
                var f2 = footprints[j];
                var (wx1, _) = f1.Actor.GetWorldPosition(scene.Actors);
                var (wx2, _) = f2.Actor.GetWorldPosition(scene.Actors);
                var w1 = f1.Actor.Kind == ActorKind.Panel && f1.Actor.ZoneW > 0 ? f1.Actor.ZoneW : (240 - wx1);
                var w2 = f2.Actor.Kind == ActorKind.Panel && f2.Actor.ZoneW > 0 ? f2.Actor.ZoneW : (240 - wx2);
                if (wx1 < wx2 + w2 && wx2 < wx1 + w1 && f1.YStart < f2.YEnd && f2.YStart < f1.YEnd)
                {
                    Add(new Diagnostic(Severity.Warning, file, 0, 0, "",
                        $"Scene {scene.Name}: UI elements '{f1.Actor.Name}' and '{f2.Actor.Name}' share VRAM tile rows (Y {f1.Actor.Y}..{f1.Actor.Y + f1.HeightPx} vs Y {f2.Actor.Y}..{f2.Actor.Y + f2.HeightPx}). Moving or padding them vertically prevents tile clearing collisions."));
                }
            }
        }

        if (labels > MaxTextLabels)
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Scene {scene.Name} has {labels} Text Labels; {MaxTextLabels} can show at once, so the rest stay empty."));
        if (_texts.Count > 0) bytes += DialogueBoxBytes;
        if (scene.Menu is { } m)
        {
            var lh = UiFont()?.Font.LineHeight ?? 8;
            var height = Math.Max(m.Height, (Math.Max(1, m.Items.Count) - 1) * Math.Max(m.Spacing, lh) + lh);
            int tx = Math.Clamp(m.X / 8, 0, 29), ty = Math.Clamp(m.Y / 8, 0, 19);
            bytes += Math.Clamp((m.Width + 7) / 8, 1, 30 - tx) * Math.Clamp((height + 7) / 8, 1, 20 - ty) * 32L;
        }
        return bytes;
    }

    /// <summary>G-HW.06: the save data of one slot must fit the save type with its slot count; else the build fails.</summary>
    void CheckSaveFits()
    {
        var save = _p.Settings.Save;
        Assets.SaveBytes = save.Type == SaveType.None ? 0 : SaveLayout.SlotBytes(save.Fields, ScriptsCompiled);
        Assets.SaveCapacity = SaveLayout.SlotCapacity(save.Type, save.Slots);
        if (SaveLayout.Problem(save, ScriptsCompiled) is { } problem) Add(new Diagnostic(Severity.Error, _p.ProjectPath, 0, 0, "", problem));
    }

    // Project Settings · General · Boot splash: the AGB Studio splash image (Content/SplashArt).
    bool GenerateSplash()
    {
        var g = _p.Settings.General;
        if (!g.BootSplash) return false;
        var (t, cells, w, h) = TilesetConverter.ConvertImage("splash", SplashArt.ToImage(SplashArt.Draw()));
        var tiles = t.Tiles.ToBytes();
        // Shown as a 32 x 32 layer (game.cpp SplashScreen): its tiles, palette, screen block and map shadow.
        Assets.Splash = new AssetInfo("Boot splash", "Image", "", tiles.Length + cells.Length * 2 + t.Palette.Banks.Count * 32, t.Tiles.Count, $"{w}x{h} tiles")
        {
            Vram = "BG", VramTileBytes = t.Tiles.Count * 32L, VramMapBytes = SplashMapBytes, PaletteBytes = t.Palette.Banks.Count * 32L, RamBytes = SplashMapBytes,
        };
        var sb = new StringBuilder("// Generated by AGB Studio (boot splash). Do not edit.\n#include \"agb/data.hpp\"\n#include \"agb/ui.hpp\"\n");
        ImageTilesetCpp(sb, "agb_splash", t, cells, tiles, false);
        sb.Append($"extern const agb::data::MapLayer agbasset_splash_layer = {{0, agb::data::LayerType::Image, 0, false, 256, 256, {w}, {h}, agbasset_tileset_agb_splash_cells, &agbasset_tileset_agb_splash}};\n");
        sb.Append($"extern const agb::ui::Splash agb_splash = {{&agbasset_splash_layer, {Gba.ToBgr555(SplashArt.Backdrop)}, {Math.Max(1, g.SplashSeconds) * 60}}};\n");
        Write(Path.Combine(GenDir, "assets", "splash.cpp"), sb.ToString());
        return true;
    }

    // A label for the kit and scene menus: the string key when the tables have it, else ASCII text.
    string Label(string key, string ascii) => StringKey(key) is var k and >= 0 ? $"{{{k}, {Cpp.Str(ascii)}}}" : $"{{-1, {Cpp.Str(ascii)}}}";

    // The menu font: UI settings, else the default font, else the first font.
    Fonts.TextCodegen.FontOut? UiFont()
    {
        var name = _p.Settings.Ui.Font.Length > 0 ? _p.Settings.Ui.Font : _p.Settings.General.DefaultFont;
        return _fonts.TryGetValue(name, out var f) ? f : _fonts.Values.FirstOrDefault();
    }

    // Menu / UI kit: font, colours and labels of the pause menu, the options screen and scene menus.
    bool GenerateUiKit(List<string> sceneNames, bool sceneMenus)
    {
        var s = _p.Settings;
        var u = s.Ui;
        if (!u.PauseMenu && !sceneMenus) return false;
        var font = UiFont();
        if (font == null)
        {
            if (sceneMenus) Add(new Diagnostic(Severity.Warning, _p.ProjectPath, 0, 0, "", "Scene menus need a font: add one in Font Manager."));
            return false;
        }
        static ushort C(string hex) => Gba.ToBgr555(0xFF000000u | uint.Parse(hex.TrimStart('#'), NumberStyles.HexNumber));
        var actions = s.Input.Where(a => a.Button != "D-pad").ToList();
        var pause = !u.PauseMenu ? -1 : actions.FindIndex(a => a.Action == "Pause") is var pi and >= 0 ? pi : actions.FindIndex(a => a.Button == "Start");
        var title = Math.Max(0, sceneNames.IndexOf(u.TitleScene.Length > 0 ? u.TitleScene : s.General.StartScene));
        var sb = new StringBuilder("// Generated by AGB Studio (Menu / UI kit). Do not edit.\n#include \"agb/ui.hpp\"\n");
        sb.Append($"extern const agb::data::Font {font.Symbol};\nnamespace {{\n");
        sb.Append("const agb::ui::Label kActions[] = {");
        sb.Append(actions.Count == 0 ? "{-1, nullptr}" : string.Join(", ", actions.Select(a => Label("ui.action." + a.Action, a.Action))));
        sb.Append("};\nconst agb::ui::Label kLanguages[] = {");
        sb.Append(_languages.Count == 0 ? "{-1, nullptr}" : string.Join(", ", _languages.Select(l => Label(LanguageKey(l), l))));
        sb.Append("};\n}  // namespace\n");
        if (u.Dialogue is { } dlg)
        {
            static string Mix(ushort a, ushort b, int t)
            {
                int M(int shift)
                {
                    int x = a >> shift & 31, y = b >> shift & 31;
                    return x + (y - x) * t / 16;
                }
                return (M(0) | M(5) << 5 | M(10) << 10).ToString(CultureInfo.InvariantCulture);
            }
            var rich = u.RichColors.Concat(["#C83030", "#3050C8", "#208838"]).Take(3).Select(C).ToList();
            sb.Append("#include \"agb/text.hpp\"\nstatic const agb::text::BoxStyle kBox = {");
            sb.Append($"0, {dlg.X / 8}, {dlg.Y / 8}, {Math.Clamp(dlg.W / 8, 1, 30)}, {Math.Clamp(dlg.H / 8, 1, 20)}, 15, {Math.Clamp(dlg.TextSpeed, 0, 255)}, ");
            sb.Append($"{C(dlg.Paper)}, {C(dlg.Ink)}, {Mix(C(dlg.Ink), C(dlg.Paper), 6)}, {Mix(C(dlg.Ink), C(dlg.Paper), 11)}, {C(dlg.Shadow)}, {C(dlg.Border)}, ");
            sb.Append($"{dlg.PaddingLeft}, {dlg.PaddingTop}, {dlg.PaddingRight}, {dlg.PaddingBottom}, {dlg.BorderWidth}, {{{rich[0]}, {rich[1]}, {rich[2]}}}}};\n");
        }
        sb.Append("extern const agb::ui::Kit agb_ui = {\n");
        sb.Append($"    &{font.Symbol}, {C(u.Ink)}, {C(u.Muted)}, {C(u.Paper)}, {C(u.Ink)}, {C(u.Screen)}, {C(u.ScreenInk)}, {C(u.BarTrack)}, {C(u.BarFill)}, {Math.Clamp(u.Dim, 0, 16)},\n");
        sb.Append($"    {pause}, {title},\n");
        sb.Append($"    {Label("ui.pause.title", "Paused")}, {Label("ui.pause.resume", "Resume")}, {Label("ui.pause.options", "Options")}, {Label("ui.pause.quit", "Quit to title")},\n");
        sb.Append($"    {Label("ui.options.title", "Options")}, {Label("ui.options.music", "Music")}, {Label("ui.options.effects", "Sound effects")}, {Label("ui.options.language", "Language")},\n");
        sb.Append($"    {Label("ui.options.controls", "Controls")}, {Label("ui.options.hint", "A: change   B: back")}, {Label("ui.options.press", "press a button")},\n");
        sb.Append($"    kLanguages, kActions, {actions.Count}, {(s.AllowRemap ? "true" : "false")}, {(u.Dialogue != null ? "&kBox" : "nullptr")}}};\n");
        Write(Path.Combine(GenDir, "ui.cpp"), sb.ToString());
        return true;
    }

    void GenerateScene(string file, SceneFile scene, List<string> sceneNames)
    {
        var id = Cpp.Ident(GameProject.AssetName(file));
        var sb = new StringBuilder();
        var externs = new StringBuilder();
        sb.Append("// Generated by AGB Studio from ").Append(Path.GetFileName(file)).Append(". Do not edit.\n");
        sb.Append("#include \"agb/api.hpp\"\n#include \"agb/behaviours.hpp\"\n#include \"agb/game.hpp\"\n#include \"agb/ui.hpp\"\n");
        if (_scriptOut.RegistryHeader.Length > 0) sb.Append($"#include \"{_scriptOut.RegistryHeader}\"\n");
        sb.Append("namespace {\n");

        // Map layers: the map file's data is generated once and shared by every scene that shows it (P5.03).
        MapOut? map = null;
        if (scene.Map.Length > 0)
        {
            map = MapData(scene.Map, file, scene.Name);
            if (map == null) return;
            externs.Append($"extern const agb::data::MapLayer {map.Symbol}[];\n");
        }

        // Actors and their behaviours
        var actorNames = scene.Actors.Select(a => a.Name).ToList();
        var actors = new StringBuilder();
        for (var ai = 0; ai < scene.Actors.Count; ai++)
            actors.Append("    ").Append(ActorCpp(sb, externs, file, scene.Actors[ai], $"agbmake_{id}_{ai}", $"agbasset_scene_{id}_a{ai}_b", scene.Actors)).Append(",\n");
        if (scene.Actors.Count > 0) sb.Append($"const agb::ActorDef agbasset_scene_{id}_actors[] = {{\n{actors}}};\n");

        // Links
        var links = new StringBuilder();
        for (var li = 0; li < scene.Links.Count; li++)
        {
            var l = scene.Links[li];
            // An empty From is a "Condition only" link (Link Settings): it has no trigger actor.
            var from = l.From.Trim().Length == 0 ? -1 : actorNames.IndexOf(l.From);
            var to = sceneNames.IndexOf(l.To);
            if (from < 0 && l.From.Trim().Length > 0) { Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Link from '{l.From}' in {scene.Name}: no actor has that name (renamed or deleted?). Draw the link again in the World Map.")); continue; }
            if (to < 0) { Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Link to '{l.To}': no scene has that name (renamed or deleted?). Draw the link again in the World Map.")); continue; }
            if (from < 0 && l.Kind != LinkKind.Respawn && l.OnlyIf.Trim().Length == 0)
                Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Link → {l.To} has neither a From actor nor an 'Only if' condition, so it never fires."));
            var condition = "nullptr";
            if (l.OnlyIf.Length > 0 && ScriptsCompiled) condition = $"agb_link_{id}_{li}";
            else if (l.OnlyIf.Length > 0)
                Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Link {l.From} → {l.To}: the 'Only if' condition was not compiled (this build tool has no script compiler), so the link is always allowed. Build from the AGB Studio editor or with agb build."));
            var frames = (int)Math.Round(l.Duration * 60);
            links.Append($"    {{{from}, agb::LinkKind::{l.Kind}, {to}, {l.SpawnX}, {l.SpawnY}, agb::render::effects::Transition::{l.Transition}, {frames}, {condition}}},\n");
        }
        if (scene.Links.Count > 0) sb.Append($"const agb::LinkDef agbasset_scene_{id}_links[] = {{\n{links}}};\n");
        if (scene.Menu is { } menu) SceneMenuCpp(sb, file, id, menu, sceneNames);
        sb.Append("}  // namespace\n");

        var follow = actorNames.IndexOf(scene.Camera.Follow);
        var music = scene.Music.Track.Length > 0 ? MusicId(scene.Music.Track) : -1;
        if (scene.Music.Track.Length > 0 && music < 0)
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Scene {scene.Name}: music '{scene.Music.Track}' is not in assets/audio/music, so the scene plays no music. Import the track again, or pick another in Scene Settings."));
        var backdrop = Gba.ToBgr555(0xFF000000u | uint.Parse(scene.Backdrop.TrimStart('#'), NumberStyles.HexNumber));
        sb.Append($"extern const agb::SceneDef agbasset_scene_{id} = {{\n");
        sb.Append($"    {Cpp.Str(scene.Name)}, {(map?.Map.Width ?? scene.Width)}, {(map?.Map.Height ?? scene.Height)}, {scene.VideoMode},\n");
        sb.Append($"    {(map is { LayerCount: > 0 } ? map.Symbol : "nullptr")}, {map?.LayerCount ?? 0}, {map?.CollisionLayer ?? -1},\n");
        sb.Append($"    {(scene.Actors.Count > 0 ? $"agbasset_scene_{id}_actors" : "nullptr")}, {scene.Actors.Count},\n");
        sb.Append($"    {{{follow}, {scene.Camera.DeadZoneW}, {scene.Camera.DeadZoneH}, {(scene.Camera.ClampToMap ? "true" : "false")}, 0, 0}},\n");
        sb.Append($"    {music}, {(scene.Music.FadeOnSceneChange ? "true" : "false")},\n");
        sb.Append($"    {(scene.Links.Count > 0 ? $"agbasset_scene_{id}_links" : "nullptr")}, {scene.Links.Count},\n");
        // Scene hooks from the scripting compiler: Scene Settings events, then the SceneScript of the same name.
        var hooks = ScriptsCompiled ? $"agb_scene_{id}_onLoad, agb_scene_{id}_onEnter, agb_scene_{id}_onExit" : "nullptr, nullptr, nullptr";
        sb.Append($"    {hooks}, {backdrop}, {(scene.AlphaBlending ? "true" : "false")}, {(scene.Mosaic ? "true" : "false")},\n");
        // World Map inspector music Volume: the game's music volume while the scene's own track plays (0..256).
        sb.Append($"    {(scene.Menu != null ? $"&agbasset_scene_{id}_menu" : "nullptr")}, {SceneMusicVolume(scene)}}};\n");
        // Asset declarations go after the includes (first line block of sb).
        var text = sb.ToString();
        var cut = text.IndexOf("namespace {", StringComparison.Ordinal);
        Write(Path.Combine(GenDir, "scenes", $"scene_{id}.cpp"), text[..cut] + externs + text[cut..]);
    }

    /// <summary>The scene's music Volume (0..1) as SceneDef::musicVolume, 0..256.</summary>
    public static int SceneMusicVolume(SceneFile scene) => (int)Math.Round(Math.Clamp(scene.Music.Volume, 0, 1) * 256);

    /// <summary>A map file's layers in ROM: <c>agbasset_map_&lt;Map&gt;_layers</c> in gen/maps (P5.03).</summary>
    public sealed record MapOut(MapFile Map, string Symbol, int LayerCount, int CollisionLayer);

    readonly Dictionary<string, MapOut?> _maps = new(StringComparer.Ordinal);

    /// <summary>
    /// Writes a map's layers once per build (gen/maps/map_&lt;Map&gt;.cpp), however many scenes show the map, and returns
    /// the symbol of its layer table. Null when the map or its assets are missing (the error names the scene).
    /// </summary>
    MapOut? MapData(string mapName, string sceneFile, string sceneName)
    {
        var name = Path.GetFileNameWithoutExtension(mapName);
        if (_maps.TryGetValue(name, out var done)) return done;
        var mapPath = Path.Combine(_p.MapsDir, name + ".map");
        if (!File.Exists(mapPath))
        {
            Add(new Diagnostic(Severity.Error, sceneFile, 0, 0, "", $"Scene {sceneName} uses map '{mapName}', which does not exist. Pick another map in Scene Settings, or restore maps/{name}.map."));
            return null;
        }
        var map = GameProject.Load<MapFile>(mapPath);
        var id = Cpp.Ident(name);
        var sb = new StringBuilder();
        var externs = new StringBuilder();
        sb.Append("// Generated by AGB Studio from ").Append(Path.GetFileName(mapPath)).Append(". Do not edit.\n");
        sb.Append("#include \"agb/data.hpp\"\n");
        var tsId = Cpp.Ident(map.Tileset);
        // Maps made only of image layers (a visual novel backdrop) need no tileset.
        var needsTileset = map.Layers.Any(l => l.Type != LayerType.Image);
        if (needsTileset && !_tilesets.ContainsKey(map.Tileset))
        {
            Add(new Diagnostic(Severity.Error, mapPath, 0, 0, "", $"Map {map.Name} uses tileset '{map.Tileset}', which is not in assets/tilesets. Import the tileset again, or pick another one for the map."));
            _maps[name] = null;
            return null;
        }
        if (needsTileset) externs.Append($"extern const agb::data::Tileset agbasset_tileset_{tsId};\n");
        var data = new StringBuilder();
        var layers = new StringBuilder();
        for (var i = 0; i < map.Layers.Count; i++)
        {
            var l = map.Layers[i];
            if (l.Type != LayerType.Image && l.Cells.Count != map.Width * map.Height)
            {
                Add(new Diagnostic(Severity.Error, mapPath, 0, 0, "", $"Layer {l.Name} of map {map.Name} has {l.Cells.Count} tiles, but a {map.Width}×{map.Height} map needs {map.Width * map.Height}. The .map file is damaged; restore it from a backup or version control."));
                _maps[name] = null;
                return null;
            }
            var type = l.Type switch { LayerType.Image => "Image", LayerType.Affine => "Affine", LayerType.CollisionOnly => "CollisionOnly", _ => "Tile" };
            if (l.Type == LayerType.Image)
            {
                if (!_images.TryGetValue(l.Image, out var size))
                {
                    Add(new Diagnostic(Severity.Error, mapPath, 0, 0, "", $"Layer {l.Name} uses image '{l.Image}', which is not in assets/images. Import the image again, or pick another one for the layer."));
                    continue;
                }
                var imgId = Cpp.Ident(l.Image);
                externs.Append($"extern const agb::data::Tileset agbasset_tileset_{imgId};\nextern const unsigned short agbasset_tileset_{imgId}_cells[];\n");
                layers.Append($"    {{{l.Bg}, agb::data::LayerType::Image, {l.Priority}, {(l.Wrap ? "true" : "false")}, {Cpp.Parallax(l.ParallaxX)}, {Cpp.Parallax(l.ParallaxY)}, ");
                layers.Append($"{size.W}, {size.H}, agbasset_tileset_{imgId}_cells, &agbasset_tileset_{imgId}}},\n");
                continue;
            }
            if (l.Type == LayerType.Affine)
            {
                // 8bpp tiles with flips and banks baked in, one tile per 8x8 cell (affine maps have neither).
                var ts = _tilesets[map.Tileset];
                var af = AffineConverter.Convert(ts, l.Cells, map.Width, map.Height);
                if (af.Problem != null) Add(new Diagnostic(Severity.Warning, mapPath, 0, 0, "", $"Layer {l.Name}: {af.Problem}"));
                data.Append("alignas(4) ");
                Cpp.Bytes(data, $"const unsigned int agbasset_map_{id}_l{i}_tiles", af.Tiles);
                Cpp.U16(data, $"const unsigned short agbasset_map_{id}_l{i}_colors", ts.Palette.Flatten().Select(c => (int)c));
                data.Append($"const agb::data::Palette agbasset_map_{id}_l{i}_pal{{agbasset_map_{id}_l{i}_colors, {ts.Palette.Banks.Count}}};\n");
                data.Append($"const agb::data::Tileset agbasset_map_{id}_l{i}_ts{{{{agbasset_map_{id}_l{i}_tiles, {af.Tiles.Length}, {af.TileCount}, 8, false}}, ");
                data.Append($"&agbasset_map_{id}_l{i}_pal, nullptr, nullptr, nullptr, 0, nullptr, 0}};\n");
                data.Append("alignas(4) ");
                Cpp.U16(data, $"const unsigned short agbasset_map_{id}_l{i}", af.PackedCells);
                layers.Append($"    {{{l.Bg}, agb::data::LayerType::Affine, {l.Priority}, {(l.Wrap ? "true" : "false")}, {Cpp.Parallax(l.ParallaxX)}, {Cpp.Parallax(l.ParallaxY)}, ");
                layers.Append($"{af.Width}, {af.Height}, agbasset_map_{id}_l{i}, &agbasset_map_{id}_l{i}_ts}},\n");
                continue;
            }
            Cpp.U16(data, $"const unsigned short agbasset_map_{id}_l{i}", l.Cells);
            layers.Append($"    {{{l.Bg}, agb::data::LayerType::{type}, {l.Priority}, {(l.Wrap ? "true" : "false")}, {Cpp.Parallax(l.ParallaxX)}, {Cpp.Parallax(l.ParallaxY)}, ");
            layers.Append($"{map.Width}, {map.Height}, agbasset_map_{id}_l{i}, &agbasset_tileset_{tsId}}},\n");
        }
        var symbol = $"agbasset_map_{id}_layers";
        sb.Append(externs).Append("namespace {\n").Append(data).Append("}  // namespace\n");
        sb.Append($"extern const agb::data::MapLayer {symbol}[] = {{\n{(map.Layers.Count > 0 ? layers.ToString() : "    {},\n")}}};\n");
        Write(Path.Combine(GenDir, "maps", $"map_{id}.cpp"), sb.ToString());
        var collision = map.CollisionLayer >= 0 ? map.CollisionLayer : map.Layers.FindIndex(l => l.Type == LayerType.Tile);
        var result = new MapOut(map, symbol, map.Layers.Count, collision);
        // P7.12: what the map itself takes when a scene loads it (its tileset and images are assets of their own).
        long vramTiles = 0, vramMap = 0, ram = 0, rom = 20L * Math.Max(1, map.Layers.Count);  // agb::data::MapLayer: 20 bytes
        var palettes = 0;
        foreach (var l in map.Layers)
        {
            var cost = LayerCost(map, l);
            vramTiles += cost.Tiles;
            vramMap += cost.Map;
            ram += cost.Ram;
            palettes += cost.Banks;
            if (l.Type == LayerType.Affine) rom += AffineSide(map) * AffineSide(map) + cost.Banks * 32;
            else if (l.Type != LayerType.Image) rom += l.Cells.Count * 2;
        }
        Assets.Maps[name] = new AssetInfo(name, "Map", mapPath, (int)rom, map.Layers.Count, $"{map.Width}x{map.Height}, {map.Layers.Count} layers")
        {
            Vram = vramTiles + vramMap > 0 ? "BG" : "", VramTileBytes = vramTiles, VramMapBytes = vramMap, PaletteBytes = palettes * 32L, RamBytes = ram,
        };
        _maps[name] = result;
        return result;
    }

    /// <summary>
    /// The behaviour creators of one actor (scene actor or prefab) and its <c>agb::ActorDef</c> initializer.
    /// </summary>
    string ActorCpp(StringBuilder sb, StringBuilder externs, string file, ActorData a, string makePrefix, string listName, IReadOnlyList<ActorData>? sceneActors = null)
    {
        var creators = new List<string>();
        for (var ci = 0; ci < a.Components.Count; ci++)
        {
            var comp = a.Components[ci];
            var fn = $"{makePrefix}_{ci}";
            var builtin = BuiltinBehaviours.Find(comp.Type);
            if (builtin != null)
            {
                sb.Append($"agb::Behaviour* {fn}(agb::Arena& arena) {{\n    auto* b = arena.make<{builtin.CppClass}>();\n    if (!b) return nullptr;\n");
                foreach (var (k, v) in comp.Fields)
                {
                    var f = builtin.Fields.FirstOrDefault(x => x.Name == k);
                    if (f == null)
                    {
                        Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"{a.Name}: {comp.Type} has no field '{k}' (renamed or removed), so the value is ignored. Set the field again in the Inspector."));
                        continue;
                    }
                    try
                    {
                        sb.Append($"    b->{f.CppMember} = {BuiltinBehaviours.Value(f, v, n => Math.Max(0, SoundId(n)))};\n");
                    }
                    catch (FormatException)
                    {
                        Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"{a.Name}: {comp.Type}.{k} = '{v}' is not a {(f.Kind == BuiltinBehaviours.Kind.Fixed ? "number" : "whole number")}. Enter a {(f.Kind == BuiltinBehaviours.Kind.Fixed ? "number such as 1.5" : "whole number such as 3")} in the Inspector."));
                    }
                }
                sb.Append("    return b;\n}\n");
            }
            else
            {
                var call = _scriptOut.FactoryCall(comp.Type, comp.Fields);
                if (call == null)
                {
                    Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"{a.Name}: behaviour '{comp.Type}' was not found (not a built-in and no C# script defines it). Remove it from the actor in the Inspector, or create a script with class {comp.Type}."));
                    continue;
                }
                sb.Append($"agb::Behaviour* {fn}(agb::Arena& arena) {{ return {call}; }}\n");
            }
            creators.Add(fn);
        }
        if (creators.Count > 0) sb.Append($"const agb::BehaviourCreate {listName}[] = {{{string.Join(", ", creators)}}};\n");
        var sheet = "nullptr";
        if (a.Sprite.Length > 0)
        {
            if (!_sprites.ContainsKey(a.Sprite))
            {
                Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"{a.Name} uses sprite '{a.Sprite}', which is not in assets/sprites. Import the sprite again, or pick another one in the Inspector."));
            }
            else
            {
                var decl = $"extern const agb::data::SpriteSheet agbasset_sprite_{Cpp.Ident(a.Sprite)};\n";
                if (!externs.ToString().Contains(decl, StringComparison.Ordinal)) externs.Append(decl);
                sheet = $"&agbasset_sprite_{Cpp.Ident(a.Sprite)}";
            }
        }
        var box = a.Collider != null ? $"{{{a.Collider.X}, {a.Collider.Y}, {a.Collider.W}, {a.Collider.H}}}"
            : a.Kind is ActorKind.TriggerZone or ActorKind.CameraZone ? $"{{0, {-a.ZoneH}, {a.ZoneW}, {a.ZoneH}}}" : (a.Kind == ActorKind.Panel && a.ZoneW > 0 && a.ZoneH > 0 ? $"{{0, 0, {a.ZoneW}, {a.ZoneH}}}" : "{0, 0, 0, 0}");
        var tag = a.Tag.Length == 0 ? 0 : _p.Settings.Tags.IndexOf(a.Tag) + 1;
        var behaviours = creators.Count > 0 ? listName : "nullptr";
        var labelKey = a.Text.Length > 0 ? StringKey(a.Text) : -1;
        if (a.Text.Length > 0 && labelKey < 0) Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"{a.Name}: string key '{a.Text}' is not in the Localization tables, so the label stays empty. Add the key in Localization, or change the text in the Inspector."));
        var (wx, wy) = a.GetWorldPosition(sceneActors);
        var parent = a.Parent.Length > 0 && sceneActors != null ? sceneActors.ToList().FindIndex(x => x.Name == a.Parent) : -1;
        return $"{{{Cpp.Str(a.Name)}, agb::ActorKind::{a.Kind}, {sheet}, {wx}, {wy}, {Math.Max(0, tag)}, {a.Priority}, " +
               (a.Animation.Length > 0 ? Cpp.Str(a.Animation) : "nullptr") +
               $", {box}, {behaviours}, {creators.Count}, nullptr, {(a.Hidden ? "true" : "false")}, {labelKey}{(parent >= 0 ? $", {parent}" : "")}}}";
    }

    int _prefabCount;

    /// <summary>
    /// Prefabs (Make Prefab, prefabs/&lt;Name&gt;.prefab) as const actor templates in ROM (P4.07): gen/prefabs.cpp holds an
    /// <c>agb::ActorDef</c> per prefab and the <c>agb_prefabs</c> table that Scene.Spawn("Name", x, y) looks in.
    /// </summary>
    void GeneratePrefabs()
    {
        var files = _p.PrefabFiles().ToList();
        _prefabCount = 0;
        if (files.Count == 0) return;
        var sb = new StringBuilder();
        var externs = new StringBuilder();
        var table = new StringBuilder();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            var name = GameProject.AssetName(f);
            ActorData a;
            try
            {
                a = GameProject.Load<ActorData>(f);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                Add(new Diagnostic(Severity.Error, f, 0, 0, "", $"Prefab {name} could not be read: {e.Message}"));
                continue;
            }
            if (!names.Add(name)) continue;
            var id = Cpp.Ident(name);
            // The template sits at the origin: Scene.Spawn gives the position.
            var template = new ActorData
            {
                Name = a.Name, Kind = a.Kind, Sprite = a.Sprite, Animation = a.Animation, Tag = a.Tag, Priority = a.Priority, Hidden = a.Hidden,
                Collider = a.Collider, ZoneW = a.ZoneW, ZoneH = a.ZoneH, Text = a.Text, Components = a.Components,
            };
            sb.Append($"const agb::ActorDef agbasset_prefab_{id} = {ActorCpp(sb, externs, f, template, $"agbmake_prefab_{id}", $"agbasset_prefab_{id}_b")};\n");
            table.Append($"    {{{Cpp.Str(name)}, &agbasset_prefab_{id}}},\n");
            _prefabCount++;
        }
        var text = new StringBuilder("// Generated by AGB Studio from prefabs/*.prefab. Do not edit.\n");
        text.Append("#include \"agb/api.hpp\"\n#include \"agb/behaviours.hpp\"\n#include \"agb/game.hpp\"\n");
        if (_scriptOut.RegistryHeader.Length > 0) text.Append($"#include \"{_scriptOut.RegistryHeader}\"\n");
        text.Append(externs).Append("namespace {\n").Append(sb).Append("}  // namespace\n");
        text.Append($"extern const agb::PrefabDef agb_prefabs[] = {{\n{(_prefabCount > 0 ? table.ToString() : "    {nullptr, nullptr},\n")}}};\n");
        Write(Path.Combine(GenDir, "prefabs.cpp"), text.ToString());
    }

    void SceneMenuCpp(StringBuilder sb, string file, string id, SceneMenu menu, List<string> sceneNames)
    {
        var items = new List<string>();
        foreach (var it in menu.Items)
        {
            var key = StringKey(it.Text);
            if (key < 0 && it.Text.Any(ch => ch > 126))
                Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"Menu item '{it.Text}' is not a string key, and only ASCII text can be drawn without one. Add it as a string key in Localization, or use ASCII text."));
            var arg = 0;
            if (it.Command == MenuCommand.LoadScene)
            {
                arg = sceneNames.IndexOf(it.Scene);
                if (arg < 0) Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"Menu item '{it.Text}' loads '{it.Scene}', but no scene has that name (renamed or deleted?). Pick an existing scene for the item."));
            }
            var label = key >= 0 ? $"{{{key}, nullptr}}" : $"{{-1, {Cpp.Str(it.Text)}}}";
            items.Add($"    {{agb::ui::ItemKind::Action, {label}, agb::ui::Command::{it.Command}, {Math.Max(0, arg)}, nullptr, 0}},\n");
        }
        if (items.Count is 0 or > 16) Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"The scene menu has {items.Count} items; a scene menu holds 1 to 16. Add or remove items."));
        sb.Append($"const agb::ui::ItemDef agbasset_scene_{id}_menu_items[] = {{\n{string.Concat(items)}}};\n");
        // Rows are at least one font line apart and the panel grows to hold every row (Thai fonts are taller
        // than the 8 px design font).
        var lh = UiFont()?.Font.LineHeight ?? 8;
        var spacing = Math.Max(menu.Spacing, lh);
        var height = Math.Max(menu.Height, (Math.Max(1, items.Count) - 1) * spacing + lh);
        int tx = Math.Clamp(menu.X / 8, 0, 29), ty = Math.Clamp(menu.Y / 8, 0, 19);
        int tw = Math.Clamp((menu.Width + 7) / 8, 1, 30 - tx), th = Math.Clamp((height + 7) / 8, 1, 20 - ty);
        if ((height + 7) / 8 > 20 - ty) Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"The menu of {id} does not fit on screen; move it up or use fewer items."));
        sb.Append($"const agb::ui::MenuDef agbasset_scene_{id}_menu{{agbasset_scene_{id}_menu_items, {items.Count}, {tx}, {ty}, {tw}, {th}, {spacing}, {{-1, nullptr}}, false, false}};\n");
    }

    void GenerateGameDef(List<string> sceneNames, bool splash, bool ui)
    {
        var s = _p.Settings;
        var sb = new StringBuilder();
        sb.Append("// Generated by AGB Studio from ").Append(Path.GetFileName(_p.ProjectPath)).Append(". Do not edit.\n");
        sb.Append("#include \"agb/api.hpp\"\n#include \"agb/game.hpp\"\n#include \"agb_game.hpp\"\n");
        var hasAudio = _musicIds.Count + _soundIds.Count > 0;
        if (hasAudio) sb.Append("extern const unsigned char agb_soundbank[];\n");
        // MusicMeta of each module (gen/audio/soundbank.cpp): track volume, loop start, fade on scene change.
        if (_musicIds.Count > 0) sb.Append("#include \"agb/audio.hpp\"\nextern const agb::audio::MusicInfo agb_soundbank_music[];\n");
        if (_dmgCount > 0) sb.Append("#include \"agb/audio.hpp\"\nextern const agb::audio::DmgSfx agb_dmg_sfx[];\n");
        if (_prefabCount > 0) sb.Append("extern const agb::PrefabDef agb_prefabs[];\n");
        foreach (var f in _p.SceneFiles()) sb.Append($"extern const agb::SceneDef agbasset_scene_{Cpp.Ident(GameProject.AssetName(f))};\n");
        if (_languages.Count > 0) sb.Append("#include \"agb/text.hpp\"\n");
        if (splash || ui || _hasPages) sb.Append("#include \"agb/ui.hpp\"\n");
        if (_hasPages) sb.Append("extern const agb::ui::PageSet agb_pages;\n");
        if (ScriptsCompiled) sb.Append($"#include \"{_scriptOut.RegistryHeader}\"\n");
        if (splash) sb.Append("extern const agb::ui::Splash agb_splash;\n");
        if (ui) sb.Append("extern const agb::ui::Kit agb_ui;\n");
        foreach (var lang in _languages) sb.Append($"extern const agb::text::Table agbtext_{Cpp.Ident(lang)};\n");
        sb.Append("namespace {\n");
        // Save type ID string: emulators and flash carts detect the save chip from it.
        var saveId = s.Save.Type switch
        {
            SaveType.Sram32K => "SRAM_V113",
            SaveType.Flash64K => "FLASH_V126",
            SaveType.Flash128K => "FLASH1M_V103",
            SaveType.Eeprom8K => "EEPROM_V124",
            _ => "",
        };
        if (saveId.Length > 0) sb.Append($"__attribute__((used, aligned(4))) const char kSaveTypeId[] = {Cpp.Str(saveId)};\n");
        sb.Append("const agb::SceneDef* const kScenes[] = {");
        sb.Append(string.Join(", ", _p.SceneFiles().Select(f => $"&agbasset_scene_{Cpp.Ident(GameProject.AssetName(f))}")));
        if (sceneNames.Count == 0) sb.Append("nullptr");
        sb.Append("};\n");
        var actions = s.Input.Where(a => a.Button != "D-pad").ToList();
        Cpp.U16(sb, "const unsigned short kActionKeys", actions.Select(a => KeyMask(a.Button)));
        sb.Append("const void* const kText[] = {");
        sb.Append(_languages.Count == 0 ? "nullptr" : string.Join(", ", _languages.Select(l => $"&agbtext_{Cpp.Ident(l)}")));
        sb.Append("};\n}  // namespace\n");
        sb.Append("GameState agb_state;\n");
        var start = Math.Max(0, sceneNames.IndexOf(s.General.StartScene));
        var version = SaveVersion(s.Save, ScriptsCompiled);
        sb.Append("extern const agb::GameDef agb_game = {\n");
        sb.Append($"    {Cpp.Str(s.General.GameTitle)}, kScenes, {sceneNames.Count}, {start},\n");
        sb.Append($"    {{agb::save::Type::{s.Save.Type}, {Math.Max(1, s.Save.Slots)}, sizeof(GameState), {version}, {(s.Save.Checksum ? "true" : "false")}}},\n");
        sb.Append($"    &agb_state, sizeof(GameState), kActionKeys, {actions.Count},\n");
        sb.Append($"    {(hasAudio ? "agb_soundbank" : "nullptr")}, {s.Audio.Channels}, {Audio.AudioChannels.NearestMixRate(s.Audio.MixKHz)}, ");
        sb.Append($"{(ScriptsCompiled ? "agb_scripts_init" : "nullptr")}, {(s.General.SkipSplashInDebug ? "true" : "false")}, ");
        sb.Append($"{(_languages.Count > 0 ? "kText" : "nullptr")}, {_languages.Count}, 0,\n");
        var language = Math.Max(0, _languages.IndexOf(s.General.DefaultLanguage));
        sb.Append($"    {(splash ? "&agb_splash" : "nullptr")}, {(ui ? "&agb_ui" : "nullptr")}, {language}, {(s.Save.Autosave && s.Save.Type != SaveType.None ? "true" : "false")}, {(_dmgCount > 0 ? "agb_dmg_sfx" : "nullptr")}, {_dmgCount},\n");
        sb.Append($"    {(_prefabCount > 0 ? "agb_prefabs" : "nullptr")}, {_prefabCount}, {(_musicIds.Count > 0 ? "agb_soundbank_music" : "nullptr")}, {_musicIds.Count}{(_hasPages ? ", &agb_pages" : "")}}};\n");
        sb.Append("int main() {\n");
        if (saveId.Length > 0) sb.Append("    asm volatile(\"\" ::\"r\"(kSaveTypeId));\n");
        sb.Append("    agb::game::run(agb_game);\n}\n");
        Write(Path.Combine(GenDir, "game.cpp"), sb.ToString());

        // Header with ids for scripts and tests.
        var h = new StringBuilder();
        h.Append("// Generated by AGB Studio. Do not edit.\n#pragma once\n#include \"agb/api.hpp\"\n#include \"agb/script.hpp\"\n\n");
        h.Append("// Project Settings · Save data (largest fields first to avoid padding).\nstruct GameState {\n    agb::SaveHeader header;\n");
        foreach (var f in s.Save.Fields.OrderByDescending(f => Align(f.Type))) h.Append($"    {FieldDecl(f)};\n");
        // Saved script flags and variables (Event Script store ids below 0x4000).
        if (ScriptsCompiled) h.Append("    agb::script::PersistentStore scriptStore;\n");
        h.Append("};\nextern GameState agb_state;\nextern const agb::GameDef agb_game;\n\nnamespace agb_scene {\n");
        for (var i = 0; i < sceneNames.Count; i++) h.Append($"constexpr unsigned short {Cpp.Ident(sceneNames[i])} = {i};\n");
        h.Append("}\nnamespace agb_action {\n");
        for (var i = 0; i < actions.Count; i++) h.Append($"constexpr int {Cpp.Ident(actions[i].Action)} = {i};\n");
        h.Append("}\nnamespace agb_tag {\n");
        for (var i = 0; i < s.Tags.Count; i++) h.Append($"constexpr unsigned short {Cpp.Ident(s.Tags[i])} = {i + 1};\n");
        h.Append("}\nnamespace agb_text {\n");
        for (var i = 0; i < _stringKeys.Count; i++) h.Append($"constexpr unsigned short {Cpp.Ident(_stringKeys[i])} = {i};\n");
        h.Append("}\n");
        foreach (var f in _fonts.Values) h.Append($"extern const agb::data::Font {f.Symbol};\n");
        SoundbankBuilder.WriteIfChanged(Path.Combine(GenDir, "agb_game.hpp"), h.ToString());
    }

    /// <summary>
    /// The save format version (save::Config.version): a save written with another layout is not read back. It follows
    /// the save fields and whether the script store is saved; flags live in that fixed-size store under stable ids
    /// (SaveSettings.FlagIds), so adding a flag keeps the version and the existing save data (P4.08).
    /// </summary>
    public static int SaveVersion(SaveSettings save, bool scriptStore)
    {
        var layout = string.Join(";", save.Fields.Select(f => f.Name + ":" + f.Type)) + (scriptStore ? ";scriptStore" : "");
        return 1 + (int)(Fnv(layout) % 30000);
    }

    static uint Fnv(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) h = (h ^ c) * 16777619u;
        return h;
    }

    static int Align(string type)
    {
        var t = type.Trim();
        return t is "int" or "fixed" or "fixed2" ? 4 : t == "short" || t.Contains('[') ? 2 : 1;
    }

    static string FieldDecl(SaveField f)
    {
        var t = f.Type.Trim();
        var name = Cpp.Ident(f.Name);
        if (t.StartsWith("bitset", StringComparison.Ordinal)) return $"unsigned char {name}[{(int.Parse(t[6..].Trim()) + 7) / 8}]";
        var open = t.IndexOf('[');
        if (open > 0) return $"unsigned short {name}[{t[(open + 1)..t.IndexOf(']')]}]";
        return t switch
        {
            "bool" => $"bool {name}",
            "byte" => $"unsigned char {name}",
            "short" => $"short {name}",
            "fixed" => $"agb::fixed {name}",
            "fixed2" => $"agb::fixed2 {name}",
            _ => $"int {name}",
        };
    }

    static int KeyMask(string button) => button switch
    {
        "A" => 1, "B" => 2, "Select" => 4, "Start" => 8, "Right" => 16, "Left" => 32, "Up" => 64, "Down" => 128, "R" => 256, "L" => 512,
        _ => 0,
    };

    // ---- compile -------------------------------------------------------------------------------------

    /// <summary>The ROM the compile step builds (P7.11): the runtime and generated sources, the compiler settings of the
    /// configuration (Debug / Release, Project Settings · Build optimise level, keep asserts, strip Debug.Log) and the
    /// cartridge header from Project Settings · General (title, game code, maker code, version).</summary>
    public RomTarget Target(BuildConfiguration config)
    {
        var g = _p.Settings.General;
        var version = 0;
        var parts = g.Version.Split('.');
        if (parts.Length > 0) int.TryParse(parts[^1], out version);
        var target = new RomTarget
        {
            Name = Path.GetFileNameWithoutExtension(RomPath),
            Config = config == BuildConfiguration.Release ? BuildConfig.Release : BuildConfig.Debug,
            Optimise = _p.Settings.Build.Optimise,
            Header = new RomHeader(g.RomTitle, g.GameCode, g.Maker, Math.Clamp(version, 0, 255)),
        };
        target.Sources.AddRange(NativeBuild.RuntimeSources());
        target.Sources.AddRange(_sources.Distinct());
        target.IncludeDirs.Add(Path.Combine(NativeBuild.RuntimeDir, "include"));
        target.IncludeDirs.Add(GenDir);
        target.IncludeDirs.AddRange(_includeDirs.Distinct());
        if (_p.Settings.Build.KeepAsserts) target.Defines.Add("AGB_KEEP_ASSERTS=1");
        if (config == BuildConfiguration.Release && !_p.Settings.Build.StripDebugLog) target.Defines.Add("AGB_KEEP_LOGS=1");
        // Project Settings · Audio: Maxmod's buffers in runtime/src/audio/audio.cpp are sized for this rate and channel count.
        target.Defines.Add($"AGB_AUDIO_MIX_KHZ={Audio.AudioChannels.NearestMixRate(_p.Settings.Audio.MixKHz)}");
        target.Defines.Add($"AGB_AUDIO_CHANNELS={Math.Clamp(_p.Settings.Audio.Channels, 1, Audio.AudioChannels.MaxMixingChannels)}");
        return target;
    }

    string CompileAndLink(BuildConfiguration config, CancellationToken cancel)
    {
        var target = Target(config);
        var compiled = 0;
        var total = target.Sources.Count;
        var r = new NativeBuild(_tc, Path.Combine(_p.BuildDir, "obj")).Build([target], line =>
        {
            if (_step == StepCompile && line.StartsWith('[') && line.Contains("] LINK ", StringComparison.Ordinal))
            {
                _result.StepTimes[StepCompile] = _stepWatch.Elapsed;
                _obs.StepFinished(StepCompile, _stepWatch.Elapsed, true, $"{compiled} files");
                _step = StepLink;
                _stepWatch = Stopwatch.StartNew();
                _obs.StepStarted(StepLink, "");
            }
            _obs.Line(line);
            if (line.StartsWith('[') && line.Contains("CXX "))
                _obs.FileProgress(StepCompile, ++compiled, total, Path.GetFileName(line[(line.IndexOf("CXX ", StringComparison.Ordinal) + 4)..]));
            var d = ParseGccLine(line) ?? ParseLinkerLine(line);
            if (d != null) Add(d);
        }, cancel);
        cancel.ThrowIfCancellationRequested();
        if (!r.Success)
        {
            // The whole devkitARM output is already in the log line by line; the error gives its last line and a next step.
            if (!_result.Errors.Any())
            {
                var last = r.Log.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "no output";
                Add(new Diagnostic(Severity.Error, "", 0, 0, "", $"Compiling or linking the game failed, and devkitARM gave no error line AGB Studio can point to (last line: {last}). " +
                    "The full devkitARM output is in the Build ROM log (agb build --verbose prints it). If you changed native C++ code, check it; otherwise try Build › Clean Build."));
            }
            return "failed";
        }
        Directory.CreateDirectory(Path.GetDirectoryName(RomPath)!);
        // A build with nothing to do leaves the ROM as it is (the copy keeps the linked ROM's time).
        if (!File.Exists(RomPath) || new FileInfo(RomPath).Length != new FileInfo(r.RomPath).Length || File.GetLastWriteTimeUtc(RomPath) != File.GetLastWriteTimeUtc(r.RomPath))
            File.Copy(r.RomPath, RomPath, true);
        WriteNotices(Path.Combine(Path.GetDirectoryName(RomPath)!, "THIRD_PARTY.txt"));
        _result.RomPath = RomPath;
        _result.ElfPath = Path.ChangeExtension(r.RomPath, ".elf");
        return _step == StepLink ? Path.GetFileName(RomPath) : $"{compiled} files";
    }

    /// <summary>The notices of the libraries linked into every ROM (docs/THIRD_PARTY.md): libtonc (MIT) and maxmod (ISC),
    /// written next to the ROM so they travel with it.</summary>
    void WriteNotices(string path)
    {
        var sb = new StringBuilder();
        sb.Append($"{_p.Settings.General.GameTitle} was made with AGB Studio and contains the following libraries.\n\n");
        // D-05: the AGB Studio runtime inside the ROM is MIT; the same permission text covers it and libtonc.
        sb.Append("AGB Studio runtime\n------------------\nCopyright (c) 2026 AGB Studio contributors\n\n");
        sb.Append("libtonc\n-------\nCopyright (c) J Vijn\n\n");
        sb.Append("Both are under the MIT licence:\n\n");
        sb.Append("Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated\n");
        sb.Append("documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation\n");
        sb.Append("the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to\n");
        sb.Append("permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\n");
        sb.Append("The above copyright notice and this permission notice shall be included in all copies or substantial portions of\n");
        sb.Append("the Software.\n\n");
        sb.Append("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO\n");
        sb.Append("THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\n");
        sb.Append("AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,\n");
        sb.Append("TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\n");
        sb.Append("SOFTWARE.\n\n");
        var maxmod = Path.Combine(_tc.DevkitPro, "libgba", "maxmod_license.txt");
        sb.Append("maxmod\n------\n");
        sb.Append(File.Exists(maxmod) ? File.ReadAllText(maxmod).Replace("\r\n", "\n").Trim() + "\n" : "Copyright (c) 2008, Mukunda Johnson. ISC licence (see maxmod_license.txt in devkitPro).\n");
        SoundbankBuilder.WriteIfChanged(path, sb.ToString().Replace("\n", Environment.NewLine));
    }

    /// <summary>Turns "file:line:col: error: message" into a diagnostic (and maps generated C++ back via #line).</summary>
    public static Diagnostic? ParseGccLine(string line)
    {
        var m = System.Text.RegularExpressions.Regex.Match(line, @"^(.*?):(\d+):(\d+): (error|warning|fatal error): (.*)$");
        if (!m.Success) return null;
        var sev = m.Groups[4].Value == "warning" ? Severity.Warning : Severity.Error;
        return new Diagnostic(sev, m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), "", m.Groups[5].Value);
    }

    /// <summary>G-HW.04 / G-HW.05: ld's "region `rom' overflowed by N bytes" (and iwram / ewram) in plain language.</summary>
    public static Diagnostic? ParseLinkerLine(string line)
    {
        var m = System.Text.RegularExpressions.Regex.Match(line, @"region `(rom|iwram|ewram)' overflowed by (\d+) bytes");
        if (!m.Success) return null;
        var over = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var amount = over >= 1024 ? $"{(over + 1023) / 1024} KB" : $"{over} bytes";
        return new Diagnostic(Severity.Error, "", 0, 0, "", m.Groups[1].Value switch
        {
            "rom" => $"The game is {amount} larger than the 32 MB a GBA cartridge holds. Compress graphics (Project Settings · Build), " +
                     "use shorter music or fewer samples, or remove unused assets (Find Unused Assets).",
            "iwram" => $"IWRAM (32 KB) is {amount} too small for the game's fast code and variables. Use smaller arrays in scripts " +
                       "or move large data to EWRAM.",
            _ => $"EWRAM (256 KB) is {amount} too small for the game's variables. Use smaller arrays in scripts and fewer actors per scene.",
        });
    }
}

/// <summary>An asset of the build: its ROM bytes as converted, and what it takes when a scene loads it (P7.12,
/// report.json <c>assets</c>): tile and map bytes in VRAM (<see cref="Vram"/> says which half), palette RAM bytes, and
/// RAM buffer bytes (the screen-arena shadow of a map layer). Assets the runtime reads from ROM take no RAM.</summary>
public sealed record AssetInfo(string Name, string Type, string File, int RomBytes, int Count, string Detail)
{
    /// <summary>"BG", "OBJ", or "" for assets that put nothing in VRAM.</summary>
    public string Vram { get; init; } = "";
    public long VramTileBytes { get; init; }
    public long VramMapBytes { get; init; }
    public long PaletteBytes { get; init; }
    public long RamBytes { get; init; }
}

/// <summary>What one scene puts in VRAM and palette RAM at once (GameBuilder.CheckHardwareLimits): BG bytes include the
/// text layer bytes.</summary>
public sealed record SceneLimits(long BgBytes, long TextBytes, int BgBanks, long ObjBytes, int ObjBanks);

public sealed class BuildAssetInfo
{
    public Dictionary<string, AssetInfo> Sprites { get; } = new();
    public Dictionary<string, AssetInfo> Tilesets { get; } = new();
    public Dictionary<string, AssetInfo> Audio { get; } = new();
    public Dictionary<string, AssetInfo> Fonts { get; } = new();
    public Dictionary<string, AssetInfo> Data { get; } = new();
    /// <summary>Maps the scenes show (one entry per map, however many scenes use it).</summary>
    public Dictionary<string, AssetInfo> Maps { get; } = new();
    /// <summary>The boot splash image and its layer, when the project shows it.</summary>
    public AssetInfo? Splash { get; set; }
    public Dictionary<string, SceneLimits> Scenes { get; } = new();
    public long SaveBytes { get; set; }
    public long SaveCapacity { get; set; }
}
