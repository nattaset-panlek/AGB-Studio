using System.Globalization;
using AgbStudio.Build.Graphics;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;

namespace AgbStudio.Editor.Windows.Debugger;

/// <summary>One line of the GBA budget (Meter: label, bar, value text).</summary>
public sealed record BudgetLine(string Label, double Used, double Total, string Text)
{
    public double Percent => Total <= 0 ? 0 : Math.Clamp(100 * Used / Total, 0, 100);
    public ProgressStatus Status => Total > 0 && Used > Total ? ProgressStatus.Danger : Percent >= 85 ? ProgressStatus.Warn : ProgressStatus.OK;
}

/// <summary>The budget of one scene: the lines of Budget Details (T-O06) and the Inspector "GBA Budget", plus suggestions.</summary>
public sealed record BudgetSnapshot(string Scene, bool Live, IReadOnlyList<BudgetLine> Lines, IReadOnlyList<string> Suggestions)
{
    public BudgetLine? Line(string label) => Lines.FirstOrDefault(l => l.Label == label);
    public bool AllGood => Lines.All(l => l.Status != ProgressStatus.Danger);
    public string StatusText => AllGood ? "all good" : "over budget";
}

/// <summary>
/// GBA budget numbers and suggestions (P13.07): live from the running game's VRAM, OAM and palette RAM while Play
/// Mode runs, else from build/report.json; RAM from the ELF; palette-sharing suggestions from the sprites of the
/// scene. Budget Details (T-O06) reads it with <c>Services.Get&lt;BudgetService&gt;()</c> (or <see cref="Of"/>).
/// </summary>
public sealed class BudgetService
{
    readonly EditorServices _s;

    BudgetService(EditorServices s) => _s = s;

    public static BudgetService Of(EditorServices s)
    {
        if (s.GetService(typeof(BudgetService)) is BudgetService b) return b;
        b = new BudgetService(s);
        s.Set(b);
        return b;
    }

    /// <summary>The budget of the scene that runs (Play Mode) or of the Scene Editor's scene.</summary>
    public BudgetSnapshot Current()
    {
        var play = _s.GetService(typeof(PlayController)) as PlayController;
        if (play?.Core is { } core && play.Session?.Running == true)
        {
            // the core maps memory from the first frame on
            try { return Live(core, play.World.SceneName, play.Elf, play.RomPath, _s.Project.Current); }
            catch (ArgumentOutOfRangeException) { }
        }
        var scene = play?.CurrentSceneName() ?? _s.Project.Current?.Settings.LastScene ?? "";
        return FromReport(scene);
    }

    /// <summary>Live numbers read from emulator memory.</summary>
    public static BudgetSnapshot Live(LibretroCore core, string scene, ElfSymbols? elf, string? romPath, GameProject? project)
    {
        var lines = new List<BudgetLine>();
        var bgKb = UsedKb(core, GbaMemory.Vram, 64 * 1024);
        var objKb = UsedKb(core, GbaMemory.Vram + 0x10000, 32 * 1024);
        lines.Add(new BudgetLine("VRAM BG", bgKb, 64, $"{bgKb} / 64 KB"));
        lines.Add(new BudgetLine("VRAM OBJ", objKb, 32, $"{objKb} / 32 KB"));
        var oam = Inspector.Oam(core).Count;
        lines.Add(new BudgetLine("OAM", oam, 128, $"{oam} / 128"));
        var pal = Inspector.PaletteRam(core);
        var (bgBanks, objBanks, fullest, fullestColours) = Palettes(pal);
        lines.Add(new BudgetLine("BG palettes", bgBanks, 16, $"{bgBanks} / 16"));
        lines.Add(new BudgetLine("OBJ palettes", objBanks, 16, $"{objBanks} / 16"));
        if (fullest >= 0) lines.Add(new BudgetLine($"Palette OBJ {fullest}", fullestColours, 16, $"{fullestColours} / 16 colours"));
        var (ewram, iwram) = Ram(elf);
        lines.Add(new BudgetLine("EWRAM", ewram, 256, $"{ewram} / 256 KB"));
        lines.Add(new BudgetLine("IWRAM", iwram, 32, $"{iwram} / 32 KB"));
        var romKb = romPath != null && File.Exists(romPath) ? new FileInfo(romPath).Length / 1024 : 0;
        lines.Add(new BudgetLine("ROM", romKb, 32 * 1024, $"{romKb} KB / 32 MB"));
        var suggestions = project != null && scene.Length > 0 ? PaletteSuggestions(project, scene) : [];
        return new BudgetSnapshot(scene, true, lines, suggestions);
    }

    /// <summary>The scene's numbers from the last build report (no game running).</summary>
    public BudgetSnapshot FromReport(string scene)
    {
        var lines = new List<BudgetLine>();
        var r = _s.Build.LoadReport();
        var p = _s.Project.Current;
        if (r != null)
        {
            var b = r.Scenes.GetValueOrDefault(scene) ?? new BuildReport.SceneBudget();
            long bg = (b.VramBgBytes + 1023) / 1024, obj = (b.VramObjBytes + 1023) / 1024;
            lines.Add(new BudgetLine("VRAM BG", bg, 64, $"{bg} / 64 KB"));
            lines.Add(new BudgetLine("VRAM OBJ", obj, 32, $"{obj} / 32 KB"));
            lines.Add(new BudgetLine("BG palettes", b.BgPalettes, 16, $"{b.BgPalettes} / 16"));
            lines.Add(new BudgetLine("OBJ palettes", b.ObjPalettes, 16, $"{b.ObjPalettes} / 16"));
            long ew = (r.EwramBytes + 1023) / 1024, iw = (r.IwramBytes + 1023) / 1024, rom = r.RomBytes / 1024;
            lines.Add(new BudgetLine("EWRAM", ew, 256, $"{ew} / 256 KB"));
            lines.Add(new BudgetLine("IWRAM", iw, 32, $"{iw} / 32 KB"));
            lines.Add(new BudgetLine("ROM", rom, 32 * 1024, $"{rom} KB / 32 MB"));
        }
        var suggestions = p != null && scene.Length > 0 ? PaletteSuggestions(p, scene) : [];
        return new BudgetSnapshot(scene, false, lines, suggestions);
    }

    /// <summary>KB of a VRAM area holding data (32-byte tiles with any non-zero byte), rounded up.</summary>
    public static int UsedKb(LibretroCore core, uint address, int size)
    {
        var bytes = core.Memory(address, size);
        var used = 0;
        for (var t = 0; t < size; t += 32)
            if (bytes.Slice(t, 32).IndexOfAnyExcept((byte)0) >= 0) used++;
        return (used * 32 + 1023) / 1024;
    }

    /// <summary>Banks in use (any colour 1-15 set) for BG and OBJ, and the OBJ bank with the most colours.</summary>
    public static (int Bg, int Obj, int Fullest, int FullestColours) Palettes(ushort[] pal)
    {
        int bg = 0, obj = 0, fullest = -1, most = 0;
        for (var bank = 0; bank < 32; bank++)
        {
            var colours = 0;
            for (var i = 1; i < 16; i++) if (pal[bank * 16 + i] != 0) colours++;
            if (colours == 0) continue;
            if (bank < 16) bg++;
            else
            {
                obj++;
                if (colours + 1 > most) { most = colours + 1; fullest = bank - 16; }
            }
        }
        return (bg, obj, fullest, most);
    }

    /// <summary>EWRAM and IWRAM in KB used by the game's static data (ELF allocated sections).</summary>
    public static (int Ewram, int Iwram) Ram(ElfSymbols? elf)
    {
        if (elf is null) return (0, 0);
        long ew = 0, iw = 0;
        foreach (var s in elf.Sections.Where(s => s.Allocated && s.Size > 0))
        {
            if (s.Address is >= 0x02000000 and < 0x02040000) ew += s.Size;
            else if (s.Address is >= 0x03000000 and < 0x03008000) iw += s.Size;
        }
        return ((int)((ew + 1023) / 1024), (int)((iw + 1023) / 1024));
    }

    /// <summary>
    /// Pairs of sprites of the scene whose colours fit one 16-colour OBJ palette together. OBJ palettes are given out in
    /// the order the scene's actors spawn, one per sprite sheet (sheets with a shared palette use their owner's), so the
    /// suggestion names the bank the pair would use: "slime.png and coin.png could share OBJ palette 1 — saves one palette slot."
    /// </summary>
    public static IReadOnlyList<string> PaletteSuggestions(GameProject p, string scene)
    {
        var scenePath = Path.Combine(p.ScenesDir, scene + ".scene");
        if (!File.Exists(scenePath)) return [];
        // the live budget asks twice a second: decode the sprites again only when the scene or a sprite changed
        var stamp = p.SpriteFiles().Append(scenePath).Select(f => File.GetLastWriteTimeUtc(f).Ticks).DefaultIfEmpty(0).Max();
        lock (s_suggestions)
        {
            if (s_suggestions.TryGetValue(scenePath, out var hit) && hit.Stamp == stamp) return hit.List;
        }
        var list = ComputeSuggestions(p, scenePath);
        lock (s_suggestions) s_suggestions[scenePath] = (stamp, list);
        return list;
    }

    static readonly Dictionary<string, (long Stamp, IReadOnlyList<string> List)> s_suggestions = new(StringComparer.OrdinalIgnoreCase);

    static IReadOnlyList<string> ComputeSuggestions(GameProject p, string scenePath)
    {
        SceneFile s;
        try { s = GameProject.Load<SceneFile>(scenePath); }
        catch (System.Text.Json.JsonException) { return []; }
        var files = p.SpriteFiles().ToDictionary(GameProject.AssetName, f => f, StringComparer.Ordinal);
        var order = new List<(string Name, string File, HashSet<ushort> Colours)>();
        foreach (var a in s.Actors)
        {
            if (a.Sprite.Length == 0 || order.Any(o => o.Name == a.Sprite) || !files.TryGetValue(a.Sprite, out var file)) continue;
            var meta = p.LoadMeta<SpriteMeta>(file);
            if (meta.Palette.Length > 0) continue;  // already shares its owner's palette
            try { order.Add((a.Sprite, file, ColorImage.Load(file).Histogram().Keys.ToHashSet())); }
            catch (Exception e) when (e is IOException or InvalidDataException) { }
        }
        var list = new List<string>();
        var used = new HashSet<string>();
        for (var i = 0; i < order.Count; i++)
        {
            if (used.Contains(order[i].Name)) continue;
            for (var j = i + 1; j < order.Count; j++)
            {
                if (used.Contains(order[j].Name)) continue;
                var union = new HashSet<ushort>(order[i].Colours);
                union.UnionWith(order[j].Colours);
                if (union.Count > 15) continue;
                list.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{Path.GetFileName(order[i].File)} and {Path.GetFileName(order[j].File)} could share OBJ palette {i} — saves one palette slot."));
                used.Add(order[i].Name);
                used.Add(order[j].Name);
                break;
            }
        }
        return list;
    }
}
