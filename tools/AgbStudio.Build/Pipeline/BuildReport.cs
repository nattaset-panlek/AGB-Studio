using System.Globalization;
using System.Text.Json;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Pipeline;

/// <summary>build/report.json (Build Report T-P11, Budget Details T-O06): ROM breakdown, largest assets, RAM use,
/// build times and warnings. Sizes come from the linked ELF symbols, so they are the real numbers.</summary>
public sealed class BuildReport
{
    public string Rom { get; set; } = "";
    public string Title { get; set; } = "";
    public string GameCode { get; set; } = "";
    public string Maker { get; set; } = "";
    public string Version { get; set; } = "";
    public string SaveType { get; set; } = "";
    public string Configuration { get; set; } = "";
    public DateTime BuiltAt { get; set; }
    public long RomBytes { get; set; }
    public long RomCapacity { get; set; } = 32 * 1024 * 1024;
    public double BuildSeconds { get; set; }
    public Dictionary<string, double> StepSeconds { get; set; } = new();
    public Dictionary<string, long> Breakdown { get; set; } = new();  // Graphics, Code, Audio, Data
    public List<ReportAsset> LargestAssets { get; set; } = new();
    public long IwramBytes { get; set; }
    public long EwramBytes { get; set; }
    public long IwramCapacity { get; set; } = 32 * 1024;
    public long EwramCapacity { get; set; } = 256 * 1024;
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
        public int BgPalettes { get; set; }
        public int ObjPalettes { get; set; }
        public int Actors { get; set; }
        public List<string> Hints { get; set; } = new();
    }

    public static BuildReport Create(GameProject p, Toolchain tc, BuildResult result, BuildAssetInfo assets, BuildConfiguration config)
    {
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
        };
        var symbols = Symbols(tc, result.ElfPath);
        long graphics = 0, audio = 0, data = 0, code = 0;
        var perAsset = new Dictionary<string, (string Type, long Bytes)>();
        foreach (var (name, addr, size, kind) in symbols)
        {
            var inRom = addr >= 0x08000000 && addr < 0x0A000000;
            if (addr >= 0x03000000 && addr < 0x03008000) r.IwramBytes += size;
            if (addr >= 0x02000000 && addr < 0x02040000) r.EwramBytes += size;
            if (!inRom) continue;
            if (name.StartsWith("agbasset_sprite_", StringComparison.Ordinal) || name.StartsWith("agbasset_tileset_", StringComparison.Ordinal))
            {
                graphics += size;
                var asset = AssetName(name);
                var type = name.StartsWith("agbasset_sprite_", StringComparison.Ordinal) ? "Sprite" : "Tileset";
                perAsset[asset] = (type, perAsset.GetValueOrDefault(asset).Bytes + size);
            }
            else if (name.StartsWith("agb_soundbank", StringComparison.Ordinal)) audio += size;
            else if (name.StartsWith("agbasset_", StringComparison.Ordinal) || name.StartsWith("agbdata_", StringComparison.Ordinal)) data += size;
            else if (kind is 'T' or 't') code += size;
            else data += size;
        }
        // Audio bank split per file by source size share.
        foreach (var a in assets.Audio.Values)
        {
            var share = assets.Audio.Values.Sum(x => x.RomBytes) is var total and > 0 ? (double)a.RomBytes / total : 0;
            perAsset[a.Name] = (a.Type == "Music" ? "Music" : "Sound", (long)(audio * share));
        }
        r.Breakdown = new() { ["Graphics"] = graphics, ["Code"] = code, ["Audio"] = audio, ["Data"] = data };
        r.LargestAssets = perAsset.OrderByDescending(kv => kv.Value.Bytes).Take(10)
            .Select(kv => new ReportAsset(kv.Key, kv.Value.Type, kv.Value.Bytes, r.RomBytes > 0 ? Math.Round(100.0 * kv.Value.Bytes / r.RomBytes, 1) : 0)).ToList();
        r.Warnings.AddRange(result.Diagnostics.Where(d => d.Severity == Severity.Warning).Select(d => d.ToString()));
        if (r.RomBytes > s.Build.WarnRomMegabytes * 1024L * 1024L)
            r.Warnings.Add($"ROM is {r.RomBytes / 1024 / 1024} MB, over the {s.Build.WarnRomMegabytes} MB warning set in Project Settings · Build.");
        if (r.IwramBytes > r.IwramCapacity - 8 * 1024)
            r.Warnings.Add($"IWRAM uses {r.IwramBytes / 1024} KB of 32 KB; less than 8 KB is left for the stack.");
        r.Scenes = SceneBudgets(p, assets);
        return r;
    }

    static Dictionary<string, SceneBudget> SceneBudgets(GameProject p, BuildAssetInfo assets)
    {
        var result = new Dictionary<string, SceneBudget>();
        foreach (var f in p.SceneFiles())
        {
            var scene = GameProject.Load<SceneFile>(f);
            var b = new SceneBudget { Actors = scene.Actors.Count };
            var sprites = scene.Actors.Select(a => a.Sprite).Where(n => n.Length > 0).Distinct().ToList();
            foreach (var sp in sprites)
                if (assets.Sprites.TryGetValue(sp, out var info)) b.VramObjBytes += Math.Min(info.RomBytes, 4096);
            b.ObjPalettes = sprites.Count;
            if (scene.Map.Length > 0)
            {
                var mapPath = Path.Combine(p.MapsDir, Path.GetFileNameWithoutExtension(scene.Map) + ".map");
                if (File.Exists(mapPath))
                {
                    var map = GameProject.Load<MapFile>(mapPath);
                    if (assets.Tilesets.TryGetValue(map.Tileset, out var ts)) b.VramBgBytes = ts.Count * 32L + map.Layers.Count(l => l.Type != LayerType.CollisionOnly) * 4096L;
                    b.BgPalettes = 1;
                }
            }
            if (b.VramObjBytes > 32 * 1024) b.Hints.Add("OBJ tiles exceed 32 KB; fewer frames of large sprites can be resident.");
            if (b.ObjPalettes > 16) b.Hints.Add("More than 16 OBJ palettes: share palettes between sprites.");
            result[scene.Name] = b;
        }
        return result;
    }

    static string AssetName(string symbol)
    {
        var rest = symbol.StartsWith("agbasset_sprite_", StringComparison.Ordinal) ? symbol["agbasset_sprite_".Length..] : symbol["agbasset_tileset_".Length..];
        foreach (var suffix in new[] { "_tiles", "_colors", "_pal", "_anims", "_meta", "_col", "_tags" })
            if (rest.EndsWith(suffix, StringComparison.Ordinal)) return rest[..^suffix.Length];
        var a = rest.IndexOf("_a", StringComparison.Ordinal);
        return a > 0 && rest.Length > a + 2 && char.IsDigit(rest[a + 2]) ? rest[..a] : rest;
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
        // History (Build Report "History"): newest first, last 20 builds.
        var historyPath = Path.Combine(buildDir, "report-history.json");
        var history = File.Exists(historyPath) ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(historyPath), opts) ?? new() : new();
        history.Insert(0, new HistoryEntry(BuiltAt, RomBytes, BuildSeconds, Warnings.Count, Errors, ""));
        File.WriteAllText(historyPath, JsonSerializer.Serialize(history.Take(20).ToList(), opts));
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
