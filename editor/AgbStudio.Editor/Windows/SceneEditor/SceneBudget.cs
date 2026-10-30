using System.Globalization;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>One line of the GBA budget: label, used / capacity and how it reads.</summary>
public sealed record BudgetMeter(string Label, double Used, double Capacity, string Text)
{
    public double Percent => Capacity <= 0 ? 0 : Math.Min(100, 100 * Used / Capacity);
    public ProgressStatus Status => Capacity <= 0 ? ProgressStatus.OK : Used > Capacity ? ProgressStatus.Danger : Used >= 0.9 * Capacity ? ProgressStatus.Warn : ProgressStatus.OK;
}

/// <summary>
/// The GBA budget of the open scene (Inspector "GBA Budget", Budget Details T-O06), computed live from the converted
/// assets with the runtime's rules: BG VRAM = the layers' tiles plus their screen blocks (and the text layer), OBJ VRAM =
/// every frame of each sprite in the scene, OAM = sprites shown, palette banks as the scene loads them, the fullest OBJ
/// palette; EWRAM, IWRAM and ROM come from the last build report. Also finds two sprites that could share a palette.
/// </summary>
public sealed class SceneBudget
{
    public required IReadOnlyList<BudgetMeter> Meters { get; init; }
    public string? Hint { get; init; }
    public bool AllGood => Meters.All(m => m.Status == ProgressStatus.OK);

    public BudgetMeter this[string label] => Meters.First(m => m.Label == label);

    static string Kb(long bytes) => ((bytes + 1023) / 1024).ToString(CultureInfo.InvariantCulture);

    public static SceneBudget For(SceneEditorModel m)
    {
        var scene = m.Scene;
        var assets = m.Assets;
        var meters = new List<BudgetMeter>();
        if (scene == null || assets == null) return new SceneBudget { Meters = meters };
        var map = m.Map;

        // BG: tiles of each tileset / image once, 4 KB of screen blocks per layer (64x32), text layer 2 KB + tiles
        long bgBytes = 0;
        var bgBanks = new HashSet<int>();
        var seen = new HashSet<object>();
        var bank = 0;
        if (map != null)
        {
            var tileset = assets.Tileset(map.Tileset);
            foreach (var l in map.Layers.Take(4).Where(l => l.Type != LayerType.CollisionOnly))
            {
                object? owner = l.Type == LayerType.Image ? assets.Image(l.Image)?.Tiles : tileset;
                if (owner is not AgbStudio.Build.Graphics.ConvertedTileset t) continue;
                bgBytes += 4096;
                if (!seen.Add(t)) continue;
                bgBytes += t.Tiles.Count * 32L;
                for (var b = 0; b < t.Palette.Banks.Count; b++) bgBanks.Add(bank++);
            }
        }
        var labels = scene.Actors.Where(a => a.Kind == ActorKind.TextLabel && a.Text.Length > 0).Take(6).ToList();
        if (labels.Count > 0)
        {
            bgBytes += 2048 + 32;
            bgBanks.Add(15);
            foreach (var a in labels) bgBytes += (240 - a.X + 7) / 8 * 2 * 32L;
        }
        meters.Add(new BudgetMeter("VRAM BG", bgBytes, 64 * 1024, $"{Kb(bgBytes)} / 64 KB"));

        // OBJ: all frames of every sprite in the scene, its palette bank and colours
        var sprites = scene.Actors.Where(a => a.Sprite.Length > 0).Select(a => a.Sprite).Distinct()
            .Select(n => (Name: n, Sheet: assets.Sprite(n))).Where(x => x.Sheet != null).ToList();
        long objBytes = sprites.Sum(x => (long)x.Sheet!.FrameCount * x.Sheet.TilesPerFrame * 32);
        var objCap = scene.VideoMode >= 3 ? 16 * 1024 : 32 * 1024;
        meters.Add(new BudgetMeter("VRAM OBJ", objBytes, objCap, $"{Kb(objBytes)} / {objCap / 1024} KB"));
        var oam = scene.Actors.Count(a => a.Sprite.Length > 0 && !a.Hidden && assets.Sprite(a.Sprite) != null);
        meters.Add(new BudgetMeter("OAM", oam, 128, $"{oam} / 128"));
        meters.Add(new BudgetMeter("BG palettes", bgBanks.Count, 16, $"{bgBanks.Count} / 16"));
        meters.Add(new BudgetMeter("OBJ palettes", sprites.Count, 16, $"{sprites.Count} / 16"));
        if (sprites.Count > 0)
        {
            var fullest = sprites.Select((x, i) => (Bank: i, Colours: x.Sheet!.ColorsUsed + 1)).OrderByDescending(x => x.Colours).ThenBy(x => x.Bank).First();
            meters.Add(new BudgetMeter($"Palette OBJ {fullest.Bank}", fullest.Colours, 16, $"{fullest.Colours} / 16 colours"));
        }

        // RAM and ROM from the last build
        var report = m.Project is { } p ? BuildReport.Load(p.BuildDir) : null;
        if (report != null)
        {
            meters.Add(new BudgetMeter("EWRAM", report.EwramBytes, report.EwramCapacity, $"{Kb(report.EwramBytes)} / {report.EwramCapacity / 1024} KB"));
            meters.Add(new BudgetMeter("IWRAM", report.IwramBytes, report.IwramCapacity, $"{Kb(report.IwramBytes)} / {report.IwramCapacity / 1024} KB"));
            meters.Add(new BudgetMeter("ROM", report.RomBytes, report.RomCapacity, $"{Kb(report.RomBytes)} KB / {report.RomCapacity / 1024 / 1024} MB"));
        }
        else
        {
            meters.Add(new BudgetMeter("EWRAM", 0, 0, "not built"));
            meters.Add(new BudgetMeter("IWRAM", 0, 0, "not built"));
            meters.Add(new BudgetMeter("ROM", 0, 0, "not built"));
        }
        return new SceneBudget { Meters = meters, Hint = PaletteHint(sprites) };
    }

    /// <summary>Two sprites whose colours fit one 15-colour palette: "slime.png and coin.png could share OBJ palette 1 — saves one palette slot."</summary>
    static string? PaletteHint(List<(string Name, AgbStudio.Build.Graphics.ConvertedSprite? Sheet)> sprites)
    {
        for (var i = 0; i < sprites.Count; i++)
            for (var j = i + 1; j < sprites.Count; j++)
            {
                var a = sprites[i].Sheet!.Palette.Skip(1).Take(sprites[i].Sheet!.ColorsUsed);
                var b = sprites[j].Sheet!.Palette.Skip(1).Take(sprites[j].Sheet!.ColorsUsed);
                if (a.Union(b).Distinct().Count() <= 15)
                    return $"{sprites[i].Name}.png and {sprites[j].Name}.png could share OBJ palette {i} — saves one palette slot.";
            }
        return null;
    }
}
