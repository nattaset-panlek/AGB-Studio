using System.Globalization;
using System.Text.Json;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Pipeline;

/// <summary>build/report.json (Build Report T-P11, Budget Details T-O06): ROM breakdown, largest assets, RAM use,
/// build times and warnings. ROM and RAM numbers come from the linker map file (<see cref="LinkerMap"/>), so they are
/// the linker's numbers: the breakdown adds up to the ROM size and IWRAM / EWRAM are the static use the map shows.</summary>
public sealed class BuildReport
{
    public const long RomLimit = 32 * 1024 * 1024;  // the GBA cartridge address space (0x08000000-0x09FFFFFF)

    public string Rom { get; set; } = "";
    public string Title { get; set; } = "";
    public string GameCode { get; set; } = "";
    public string Maker { get; set; } = "";
    public string Version { get; set; } = "";
    public string SaveType { get; set; } = "";
    public string Configuration { get; set; } = "";
    public DateTime BuiltAt { get; set; }
    public long RomBytes { get; set; }
    public long RomCapacity { get; set; } = RomLimit;
    public double BuildSeconds { get; set; }
    public Dictionary<string, double> StepSeconds { get; set; } = new();
    public Dictionary<string, long> Breakdown { get; set; } = new();  // Graphics, Code, Audio, Data
    public List<ReportAsset> LargestAssets { get; set; } = new();
    public List<ReportAsset> Assets { get; set; } = new();  // every asset in the ROM, largest first
    public long IwramBytes { get; set; }
    public long EwramBytes { get; set; }
    public long IwramCapacity { get; set; } = 32 * 1024;
    public long EwramCapacity { get; set; } = 256 * 1024;
    public long SaveBytes { get; set; }       // save data per slot (GameState), see SaveLayout
    public long SaveCapacity { get; set; }    // bytes a slot can hold with this save type and slot count
    public List<string> Warnings { get; set; } = new();
    public int Errors { get; set; }
    public int AssetsConverted { get; set; }
    public int AssetsCached { get; set; }
    public Dictionary<string, SceneBudget> Scenes { get; set; } = new();

    public sealed record ReportAsset(string Name, string Type, long Bytes, double Share);

    public sealed class SceneBudget
    {
        public long VramBgBytes { get; set; }
        public long VramObjBytes { get; set; }
        public long VramTextBytes { get; set; }  // part of VramBgBytes: text layer tiles (labels, dialogue box, menu)
        public int BgPalettes { get; set; }
        public int ObjPalettes { get; set; }
        public int Actors { get; set; }
        public List<string> Hints { get; set; } = new();
    }

    public static BuildReport Create(GameProject p, Toolchain tc, BuildResult result, BuildAssetInfo assets, BuildConfiguration config)
    {
        _ = tc;
        var s = p.Settings;
        var r = new BuildReport
        {
            Rom = Path.GetFileName(result.RomPath),
            Title = s.General.RomTitle,
            GameCode = s.General.GameCode,
            Maker = s.General.Maker,
            Version = s.General.Version,
            SaveType = s.Save.Type.ToString(),
            Configuration = config.ToString(),
            BuiltAt = DateTime.Now,
            RomBytes = File.Exists(result.RomPath) ? new FileInfo(result.RomPath).Length : 0,
            BuildSeconds = Math.Round(result.Elapsed.TotalSeconds, 2),
            StepSeconds = result.StepTimes.ToDictionary(k => k.Key, k => Math.Round(k.Value.TotalSeconds, 2)),
            AssetsConverted = result.AssetsConverted,
            AssetsCached = result.AssetsCached,
            Errors = result.Errors.Count(),
            SaveBytes = assets.SaveBytes,
            SaveCapacity = assets.SaveCapacity,
        };
        var mapPath = result.ElfPath + ".map";
        var perAsset = new Dictionary<string, (string Type, long Bytes)>();
        long graphics = 0, audio = 0, data = 0, code = 0, soundbank = 0, dmg = 0;
        if (File.Exists(mapPath))
        {
            var map = LinkerMap.Read(mapPath);
            var objects = GeneratedObjects(p, assets);
            foreach (var (section, bytes) in map.RomSpans())
            {
                var obj = Path.GetFileName(section.Object);
                if (objects.TryGetValue(obj, out var o))
                {
                    switch (o.Category)
                    {
                        case "Graphics": graphics += bytes; break;
                        case "Audio": audio += bytes; break;
                        default: data += bytes; break;
                    }
                    if (o.Type == "Soundbank") soundbank += bytes;
                    else if (o.Type == "Dmg") dmg += bytes;
                    else if (o.Asset.Length > 0) perAsset[o.Asset] = (o.Type, perAsset.GetValueOrDefault(o.Asset).Bytes + bytes);
                }
                else if (IsCode(section)) code += bytes;
                else data += bytes;
            }
            // The ROM header and anything before the first input section (none with devkitARM's crt0) count as code.
            code += Math.Max(0, map.RomUsed - (graphics + audio + data + code));
            r.IwramBytes = map.IwramUsed;
            r.EwramBytes = map.EwramUsed;
        }
        // The soundbank is one block: split it over its music and sound files by source size; DMG effects evenly.
        var sampled = assets.Audio.Values.Where(a => !a.File.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase)).ToList();
        var total = sampled.Sum(x => (long)x.RomBytes);
        foreach (var a in sampled)
            perAsset[a.Name] = (a.Type == "Music" ? "Music" : "Sound", total > 0 ? soundbank * a.RomBytes / total : 0);
        var effects = assets.Audio.Values.Where(a => a.File.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var a in effects) perAsset[a.Name] = ("Sound", dmg / effects.Count);
        r.Breakdown = new() { ["Graphics"] = graphics, ["Code"] = code, ["Audio"] = audio, ["Data"] = data };
        r.Assets = perAsset.OrderByDescending(kv => kv.Value.Bytes).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new ReportAsset(kv.Key, kv.Value.Type, kv.Value.Bytes, r.RomBytes > 0 ? Math.Round(100.0 * kv.Value.Bytes / r.RomBytes, 1) : 0)).ToList();
        r.LargestAssets = r.Assets.Take(10).ToList();
        r.Warnings.AddRange(result.Diagnostics.Where(d => d.Severity == Severity.Warning).Select(d => d.ToString()));
        if (RomSizeWarning(r.RomBytes, s.Build.WarnRomMegabytes) is { } romWarning) r.Warnings.Add(romWarning);
        if (r.IwramBytes > r.IwramCapacity - 8 * 1024)
            r.Warnings.Add($"IWRAM uses {r.IwramBytes / 1024} KB of 32 KB; less than 8 KB is left for the stack.");
        r.Scenes = SceneBudgets(p, assets);
        return r;
    }

    /// <summary>G-HW.05: the warning above the size set in Project Settings · Build ("Warn when ROM is over N MB").</summary>
    public static string? RomSizeWarning(long romBytes, int warnMegabytes) =>
        romBytes > warnMegabytes * 1024L * 1024L
            ? string.Create(CultureInfo.InvariantCulture, $"ROM is {romBytes / 1048576.0:0.0} MB, over the {warnMegabytes} MB warning set in Project Settings · Build.")
            : null;

    /// <summary>G-HW.05: a ROM over 32 MB cannot be addressed by the GBA; the build fails with this message.</summary>
    public static string? RomSizeError(long romBytes) =>
        romBytes > RomLimit
            ? string.Create(CultureInfo.InvariantCulture, $"The ROM is {romBytes / 1048576.0:0.0} MB; a GBA cartridge holds at most 32 MB. ") +
                "Compress graphics (Project Settings · Build), use shorter music or fewer samples, or remove unused assets (Find Unused Assets)."
            : null;

    static bool IsCode(LinkerMap.InputSection s)
    {
        var n = s.Name;
        if (n.StartsWith(".text", StringComparison.Ordinal) || n.StartsWith(".glue_7", StringComparison.Ordinal) || n.StartsWith(".ARM.ex", StringComparison.Ordinal)) return true;
        if (n is ".crt0" or ".init" or ".fini" or ".v4_bx" or ".vfp11_veneer" or ".eh_frame" or ".plt" or ".iplt") return true;
        // Code copied to IWRAM at boot (*.iwram.cpp and AGB_IWRAM_CODE functions).
        return s.Output.Name.StartsWith(".iwram", StringComparison.Ordinal) && (n.StartsWith(".iwram", StringComparison.Ordinal) || n.StartsWith(".text", StringComparison.Ordinal));
    }

    /// <summary>The object files of the generated sources that hold assets, by the file name the build gives them
    /// (NativeBuild.ObjName): category, asset type and asset name.</summary>
    static Dictionary<string, (string Category, string Type, string Asset)> GeneratedObjects(GameProject p, BuildAssetInfo assets)
    {
        var map = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);
        var gen = Path.Combine(p.BuildDir, "gen");
        if (!Directory.Exists(gen)) return map;
        static string Name(IEnumerable<string> names, string id) => names.FirstOrDefault(n => Cpp.Ident(n) == id) ?? id;
        foreach (var f in Directory.EnumerateFiles(gen, "*.cpp", SearchOption.AllDirectories))
        {
            var stem = Path.GetFileNameWithoutExtension(f);
            var dir = Path.GetFileName(Path.GetDirectoryName(f)) ?? "";
            (string, string, string)? o = (dir, stem) switch
            {
                ("assets", "splash") => ("Graphics", "Image", "Boot splash"),
                ("assets", "dmg_sfx") => ("Audio", "Dmg", ""),
                ("assets", _) when stem.StartsWith("sprite_", StringComparison.Ordinal) => ("Graphics", "Sprite", Name(assets.Sprites.Keys, stem[7..])),
                ("assets", _) when stem.StartsWith("tileset_", StringComparison.Ordinal) => ("Graphics", "Tileset", Name(assets.Tilesets.Keys, stem[8..])),
                ("assets", _) when stem.StartsWith("image_", StringComparison.Ordinal) => ("Graphics", "Tileset", Name(assets.Tilesets.Keys, stem[6..])),
                ("audio", "soundbank") => ("Audio", "Soundbank", ""),
                ("text", _) when stem.StartsWith("font_", StringComparison.Ordinal) => ("Data", "Font", Name(assets.Fonts.Keys, stem[5..])),
                ("text", _) when stem.StartsWith("strings_", StringComparison.Ordinal) => ("Data", "Text", "strings." + stem[8..]),
                ("data", _) when stem.StartsWith("db_", StringComparison.Ordinal) => ("Data", "Data", Name(assets.Data.Keys, stem[3..])),
                _ => null,
            };
            if (o != null) map[NativeBuild.ObjName(Path.GetFullPath(f))] = o.Value;
        }
        return map;
    }

    static Dictionary<string, SceneBudget> SceneBudgets(GameProject p, BuildAssetInfo assets)
    {
        var result = new Dictionary<string, SceneBudget>();
        foreach (var f in p.SceneFiles())
        {
            var scene = GameProject.Load<SceneFile>(f);
            var b = new SceneBudget { Actors = scene.Actors.Count };
            if (assets.Scenes.TryGetValue(scene.Name, out var l))
            {
                b.VramBgBytes = l.BgBytes;
                b.VramTextBytes = l.TextBytes;
                b.BgPalettes = l.BgBanks;
                b.VramObjBytes = l.ObjBytes;
                b.ObjPalettes = l.ObjBanks;
            }
            if (b.VramObjBytes > 24 * 1024) b.Hints.Add("Sprite frames use most of the 32 KB of OBJ VRAM: fewer large sprites can show at once.");
            if (b.ObjPalettes > 12) b.Hints.Add("Most of the 16 OBJ palettes are in use: share palettes between sprites (Sprite Editor · Palette).");
            result[scene.Name] = b;
        }
        return result;
    }

    public static List<(string Name, long Address, long Size, char Kind)> Symbols(Toolchain tc, string elf)
    {
        var list = new List<(string, long, long, char)>();
        if (!File.Exists(elf)) return list;
        var r = Proc.Run(tc.Nm, $"-S -C \"{elf}\"", null, TimeSpan.FromMinutes(1));
        foreach (var line in r.Output.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            if (!long.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var addr)) continue;
            if (!long.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var size)) continue;
            var name = parts[3].Trim();
            var ns = name.LastIndexOf("::", StringComparison.Ordinal);
            if (ns >= 0) name = name[(ns + 2)..];
            list.Add((name, addr, size, parts[2][0]));
        }
        return list;
    }

    public void Save(string buildDir)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(buildDir, "report.json"), JsonSerializer.Serialize(this, opts));
        AddHistory(buildDir, new HistoryEntry(BuiltAt, RomBytes, BuildSeconds, Warnings.Count, Errors, ""));
    }

    /// <summary>Build Report "History": newest first, the last 20 builds, failed builds included (no ROM size, the first error).</summary>
    public static void AddHistory(string buildDir, HistoryEntry entry)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        Directory.CreateDirectory(buildDir);
        var historyPath = Path.Combine(buildDir, "report-history.json");
        var history = File.Exists(historyPath) ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(historyPath), opts) ?? new() : new();
        history.Insert(0, entry);
        File.WriteAllText(historyPath, JsonSerializer.Serialize(history.Take(20).ToList(), opts));
    }

    public static List<HistoryEntry> LoadHistory(string buildDir)
    {
        var path = Path.Combine(buildDir, "report-history.json");
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return File.Exists(path) ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(path), opts) ?? new() : new();
    }

    public static BuildReport? Load(string buildDir)
    {
        var path = Path.Combine(buildDir, "report.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<BuildReport>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : null;
    }

    public sealed record HistoryEntry(DateTime At, long RomBytes, double Seconds, int Warnings, int Errors, string FirstError);
}
