using System.Text.Json;
using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Palette;

public enum BankKind { Bg, Obj }

/// <summary>
/// A sprite sheet, tileset or background image loaded for editing: ARGB pixels (transparent pixels are 0). The PNG
/// on disk is the source of the art, so palette edits recolour these pixels and Save writes the PNG back.
/// </summary>
public sealed class EditableImage(string path, int width, int height, uint[] pixels)
{
    public string Path { get; } = path;
    public int Width { get; private set; } = width;
    public int Height { get; private set; } = height;
    public uint[] Pixels { get; private set; } = pixels;
    public bool IsDirty { get; set; }
    /// <summary>Increases on every change (thumbnails and canvases redraw).</summary>
    public int Version { get; private set; }
    public event Action? Changed;

    public static EditableImage Load(string path)
    {
        var png = Png.Read(path);
        var px = (uint[])png.Pixels.Clone();
        for (var i = 0; i < px.Length; i++) if (Colour15.IsTransparent(px[i])) px[i] = Colour15.Transparent;
        return new EditableImage(path, png.Width, png.Height, px);
    }

    public uint this[int x, int y]
    {
        get => x >= 0 && y >= 0 && x < Width && y < Height ? Pixels[y * Width + x] : Colour15.Transparent;
        set { if (x >= 0 && y >= 0 && x < Width && y < Height) Pixels[y * Width + x] = value; }
    }

    /// <summary>Replaces size and pixels (OAM size change, undo of a resize).</summary>
    public void Replace(int width, int height, uint[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        Touch();
    }

    /// <summary>Marks the image edited after its pixels changed.</summary>
    public void Touch()
    {
        IsDirty = true;
        Version++;
        Changed?.Invoke();
    }

    public void Save()
    {
        Png.WriteArgb(Path, Pixels, Width, Height);
        IsDirty = false;
    }
}

/// <summary>One 16-colour palette bank of palette RAM: BG 0-15 or OBJ 0-15, its colours and the assets that use it.</summary>
public sealed class PaletteBank
{
    public BankKind Kind { get; set; }
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    /// <summary>ARGB colours, index 0 first (the transparent index when <see cref="LockTransparent"/> is on). At most 16.</summary>
    public List<uint> Colors { get; set; } = [];
    /// <summary>Project-relative paths ("assets/sprites/hero.png") of the art that uses the bank.</summary>
    public List<string> Assets { get; set; } = [];
    public bool LockTransparent { get; set; } = true;
    public bool MergeNear { get; set; }
    public bool SortByHue { get; set; }

    public string Id => (Kind == BankKind.Obj ? "OBJ " : "BG ") + Slot;
    public string Label => Id + " · " + Name;
    /// <summary>Colours in use, not counting the locked transparent index ("9 / 16").</summary>
    public int Used => Math.Max(0, Colors.Count - (LockTransparent && Colors.Count > 0 ? 1 : 0));
    public string UsedText => $"{Used} / 16";
    public bool IsTransparentIndex(int i) => i == 0 && LockTransparent;
    public ushort Value(int i) => IsTransparentIndex(i) ? (ushort)0 : Colour15.Of(Colors[i]);
}

// assets/palettes/palettes.json (Palette Manager): the banks of palette RAM, their names, colour order and options,
// and the palette effects. Colours are "#RRGGBB" (the source colour; the ROM stores the nearest 15-bit value) or
// "none" for the locked transparent index.
public sealed class PaletteLibraryFile
{
    public int Format { get; set; } = 1;
    public List<PaletteBankData> Banks { get; set; } = [];
    public PaletteEffectsData Effects { get; set; } = new();
}

public sealed class PaletteBankData
{
    public BankKind Kind { get; set; }
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public List<string> Colors { get; set; } = [];
    public List<string> Assets { get; set; } = [];
    public bool LockTransparent { get; set; } = true;
    public bool MergeNear { get; set; }
    public bool SortByHue { get; set; }
}

public enum EffectKind { Fade, Cycle, Flash }
public enum CycleDirection { Left, Right, PingPong }

/// <summary>One palette effect (T-O10): Fade toward a colour, Cycle a range of one bank, Flash the banks.</summary>
public sealed class PaletteEffectData
{
    public EffectKind Kind { get; set; }
    public string Name { get; set; } = "";
    public BankKind BankKind { get; set; } = BankKind.Bg;
    /// <summary>Bank slot, or -1 for every bank (fade / flash).</summary>
    public int Bank { get; set; } = -1;
    public int First { get; set; } = 1;
    public int Last { get; set; } = 15;
    /// <summary>Fade / flash duration in frames, or frames per cycle step.</summary>
    public int Frames { get; set; } = 30;
    public CycleDirection Direction { get; set; } = CycleDirection.Right;
    /// <summary>Fade target / flash colour (15-bit).</summary>
    public int Color { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class PaletteEffectsData
{
    public PaletteEffectData Fade { get; set; } = new() { Kind = EffectKind.Fade, Name = "black", Frames = 30, Color = 0 };
    public PaletteEffectData Cycle { get; set; } = new() { Kind = EffectKind.Cycle, Name = "water", Bank = 1, First = 6, Last = 9, Frames = 8 };
    public PaletteEffectData Flash { get; set; } = new() { Kind = EffectKind.Flash, Name = "hit", BankKind = BankKind.Obj, Bank = 0, Frames = 8, Color = 0x7FFF };
}

/// <summary>
/// The art of the open project as the Sprite Editor (T-P08) and the Palette Manager (T-P10) edit it: images, sprite
/// meta (frames, animations, origin, collider, palette), the BG / OBJ palette banks with their used-by lists and the
/// palette effects. Every edit goes through the editor undo history. Shared through <see cref="EditorServices.Get{T}"/>.
/// </summary>
public sealed class ArtLibrary
{
    public const int MergeThreshold = 12;

    readonly UndoService _undo;
    readonly Dictionary<string, EditableImage> _images = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, SpriteMeta> _metas = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _dirtyMetas = new(StringComparer.OrdinalIgnoreCase);

    public GameProject Project { get; }
    public List<PaletteBank> Banks { get; } = [];
    public PaletteEffectsData Effects { get; private set; } = new();
    public bool IsDirty { get; private set; }
    /// <summary>Raised after any change to banks, colours, images, meta or effects (also on undo / redo).</summary>
    public event Action? Changed;

    public string FilePath => Path.Combine(Project.PalettesDir, "palettes.json");

    ArtLibrary(GameProject project, UndoService undo)
    {
        Project = project;
        _undo = undo;
    }

    /// <summary>The library of the open project (created on first use, replaced when another project opens).</summary>
    public static ArtLibrary For(EditorServices s)
    {
        var p = s.Project.Current ?? throw new InvalidOperationException("No project is open.");
        if (s.GetService(typeof(ArtLibrary)) is ArtLibrary lib && lib.Project == p) return lib;
        lib = Load(p, s.Undo);
        s.Set(lib);
        return lib;
    }

    public static ArtLibrary Load(GameProject p, UndoService undo)
    {
        var lib = new ArtLibrary(p, undo);
        if (File.Exists(lib.FilePath))
        {
            var f = GameProject.Load<PaletteLibraryFile>(lib.FilePath);
            foreach (var b in f.Banks)
                lib.Banks.Add(new PaletteBank
                {
                    Kind = b.Kind, Slot = b.Slot, Name = b.Name, LockTransparent = b.LockTransparent, MergeNear = b.MergeNear, SortByHue = b.SortByHue,
                    Colors = b.Colors.Select(c => Colour15.TryParse24(c, out var v) ? v : Colour15.Transparent).Take(16).ToList(),
                    Assets = b.Assets.Select(Norm).ToList(),
                });
            lib.Effects = f.Effects;
        }
        lib.Reconcile();
        return lib;
    }

    // ---- paths, images, meta ---------------------------------------------------------------------------

    static string Norm(string rel) => rel.Replace('\\', '/');
    public string Rel(string path) => Norm(Path.GetRelativePath(Project.Root, Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Project.Root, path))));
    public string Full(string rel) => Path.GetFullPath(Path.Combine(Project.Root, rel));

    public IReadOnlyList<string> SpriteFiles() => Project.SpriteFiles().Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList();
    IEnumerable<string> BgFiles() => Project.ImageFiles().Concat(Project.TilesetFiles()).Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

    public EditableImage Image(string path)
    {
        var full = Full(Rel(path));
        if (!_images.TryGetValue(full, out var img)) _images[full] = img = EditableImage.Load(full);
        return img;
    }

    EditableImage? TryImage(string rel)
    {
        try { return Image(rel); }
        catch (IOException) { return null; }
        catch (InvalidDataException) { return null; }
    }

    /// <summary>Adds an image that is not on disk yet (New Sprite, Import); Save writes it.</summary>
    public EditableImage AddImage(string path, int w, int h, uint[] pixels)
    {
        var img = new EditableImage(Full(Rel(path)), w, h, pixels) { IsDirty = true };
        _images[img.Path] = img;
        return img;
    }

    public SpriteMeta Meta(string spritePath)
    {
        var full = Full(Rel(spritePath));
        if (!_metas.TryGetValue(full, out var m)) _metas[full] = m = Project.LoadMeta<SpriteMeta>(full);
        return m;
    }

    public void SetMeta(string spritePath, SpriteMeta meta)
    {
        var full = Full(Rel(spritePath));
        _metas[full] = meta;
        _dirtyMetas.Add(full);
    }

    public void MarkMetaDirty(string spritePath) => _dirtyMetas.Add(Full(Rel(spritePath)));

    /// <summary>Writes one sprite's meta now (new sprites, imports).</summary>
    public void SaveMeta(string spritePath)
    {
        var full = Full(Rel(spritePath));
        GameProject.Save(GameProject.MetaPath(full), Meta(full));
        _dirtyMetas.Remove(full);
    }
    public bool IsMetaDirty(string spritePath) => _dirtyMetas.Contains(Full(Rel(spritePath)));

    public static SpriteMeta Clone(SpriteMeta m) => JsonSerializer.Deserialize<SpriteMeta>(JsonSerializer.Serialize(m, GameProject.Json), GameProject.Json)!;

    // ---- banks -------------------------------------------------------------------------------------------

    public IEnumerable<PaletteBank> Of(BankKind kind) => Banks.Where(b => b.Kind == kind).OrderBy(b => b.Slot);
    public PaletteBank? Find(BankKind kind, int slot) => Banks.FirstOrDefault(b => b.Kind == kind && b.Slot == slot);
    public PaletteBank? BankOf(string assetPath)
    {
        var rel = Rel(assetPath);
        return Banks.FirstOrDefault(b => b.Assets.Contains(rel, StringComparer.OrdinalIgnoreCase));
    }

    public static string UsedBy(PaletteBank b) => string.Join(", ", b.Assets.Select(Path.GetFileName));

    /// <summary>Palette index a pixel uses in its bank: 0 for transparent, the exact 15-bit match, else the nearest colour.</summary>
    public static int IndexOf(PaletteBank bank, uint argb)
    {
        if (Colour15.IsTransparent(argb)) return 0;
        var c = Colour15.Of(argb);
        var start = bank.LockTransparent ? 1 : 0;
        for (var i = start; i < bank.Colors.Count; i++) if (Colour15.Of(bank.Colors[i]) == c) return i;
        var best = start;
        var bestD = int.MaxValue;
        for (var i = start; i < bank.Colors.Count; i++)
        {
            var d = Gba.Distance(Colour15.Of(bank.Colors[i]), c);
            if (d < bestD) { bestD = d; best = i; }
        }
        return bank.Colors.Count > start ? best : 0;
    }

    static string Pretty(string assetName)
    {
        var s = assetName.Replace('_', ' ').Replace('-', ' ').Trim();
        return s.Length == 0 ? "Palette" : char.ToUpperInvariant(s[0]) + s[1..];
    }

    int NextSlot(BankKind kind)
    {
        for (var i = 0; i < 16; i++) if (Find(kind, i) is null) return i;
        return -1;
    }

    /// <summary>Puts every sprite / background asset of the project into a bank and drops assets that are gone.</summary>
    void Reconcile()
    {
        var sprites = SpriteFiles().Select(Rel).ToList();
        var bgs = BgFiles().Select(Rel).ToList();
        var exists = sprites.Concat(bgs).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var b in Banks) b.Assets.RemoveAll(a => !exists.Contains(a));

        // sprites: owners first (meta.Palette empty), then the sprites that share an owner's palette
        var metas = sprites.ToDictionary(s => s, s => Meta(s), StringComparer.OrdinalIgnoreCase);
        var byName = sprites.ToDictionary(GameProject.AssetName, s => s, StringComparer.OrdinalIgnoreCase);
        var created = new HashSet<PaletteBank>();
        foreach (var s in sprites.OrderBy(s => metas[s].Palette.Length > 0 && byName.ContainsKey(metas[s].Palette) ? 1 : 0))
        {
            if (BankOf(s) != null) continue;
            var owner = metas[s].Palette;
            var target = owner.Length > 0 && byName.TryGetValue(owner, out var o) ? BankOf(o) : null;
            if (target is null)
            {
                var before = Banks.Count;
                target = NewDerivedBank(BankKind.Obj, GameProject.AssetName(s));
                if (Banks.Count > before) created.Add(target);
            }
            target.Assets.Add(s);
            if (!created.Contains(target)) AddMissingColours(target, s);
        }
        foreach (var f in bgs)
        {
            if (BankOf(f) != null) continue;
            var before = Banks.Count;
            var bank = NewDerivedBank(BankKind.Bg, GameProject.AssetName(f));
            if (Banks.Count > before) created.Add(bank);
            bank.Assets.Add(f);
            if (!created.Contains(bank)) AddMissingColours(bank, f);
        }
        foreach (var b in created) b.Colors = DeriveColors(b.Assets);
    }

    PaletteBank NewDerivedBank(BankKind kind, string assetName)
    {
        var slot = NextSlot(kind);
        if (slot < 0) return Of(kind).Last();   // palette RAM is full: share the last bank
        var b = new PaletteBank { Kind = kind, Slot = slot, Name = Pretty(assetName), Colors = [Colour15.Transparent] };
        Banks.Add(b);
        return b;
    }

    /// <summary>Colours of the assets in order of first use, reduced to 15 (median cut) when there are more.</summary>
    public List<uint> DeriveColors(IEnumerable<string> assets)
    {
        var order = new List<ushort>();
        var hist = new Dictionary<ushort, int>();
        var source = new Dictionary<ushort, Dictionary<uint, int>>();
        foreach (var a in assets)
        {
            if (TryImage(a) is not { } img) continue;
            foreach (var p in img.Pixels)
            {
                if (Colour15.IsTransparent(p)) continue;
                var c = Colour15.Of(p);
                if (!hist.TryGetValue(c, out var n)) { order.Add(c); source[c] = []; }
                hist[c] = n + 1;
                source[c][p | 0xFF000000u] = source[c].GetValueOrDefault(p | 0xFF000000u) + 1;
            }
        }
        var colors = new List<uint> { Colour15.Transparent };
        if (hist.Count <= 15) colors.AddRange(order.Select(c => source[c].MaxBy(kv => kv.Value).Key));
        else colors.AddRange(Quantizer.Reduce(hist, 15).Select(Colour15.Argb));
        return colors;
    }

    // ---- undo ----------------------------------------------------------------------------------------------

    sealed record BankState(string Name, int Slot, List<uint> Colors, List<string> Assets, bool Lock, bool Merge, bool Sort);

    sealed class Snapshot
    {
        public required List<PaletteBank> Order { get; init; }
        public required Dictionary<PaletteBank, BankState> Banks { get; init; }
        public required List<(EditableImage Image, int W, int H, uint[] Pixels)> Images { get; init; }
        public required List<(string Path, SpriteMeta Meta)> Metas { get; init; }
        public required string Effects { get; init; }
    }

    Snapshot Capture(IEnumerable<string> images, IEnumerable<string>? metas = null) => new()
    {
        Order = [.. Banks],
        Banks = Banks.ToDictionary(b => b, b => new BankState(b.Name, b.Slot, [.. b.Colors], [.. b.Assets], b.LockTransparent, b.MergeNear, b.SortByHue)),
        Images = images.Distinct(StringComparer.OrdinalIgnoreCase).Select(TryImage).OfType<EditableImage>()
            .Select(i => (i, i.Width, i.Height, (uint[])i.Pixels.Clone())).ToList(),
        Metas = (metas ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Select(m => (Full(Rel(m)), Clone(Meta(m)))).ToList(),
        Effects = JsonSerializer.Serialize(Effects, GameProject.Json),
    };

    void Restore(Snapshot s)
    {
        Banks.Clear();
        Banks.AddRange(s.Order);
        foreach (var (b, st) in s.Banks)
        {
            b.Name = st.Name;
            b.Slot = st.Slot;
            b.Colors = [.. st.Colors];
            b.Assets = [.. st.Assets];
            b.LockTransparent = st.Lock;
            b.MergeNear = st.Merge;
            b.SortByHue = st.Sort;
        }
        foreach (var (img, w, h, px) in s.Images) img.Replace(w, h, (uint[])px.Clone());
        foreach (var (path, meta) in s.Metas) SetMeta(path, Clone(meta));
        Effects = JsonSerializer.Deserialize<PaletteEffectsData>(s.Effects, GameProject.Json)!;
        Touch();
    }

    /// <summary>Runs an edit as one undo step; the images of <paramref name="banks"/> and the given sprite meta are restored on undo.</summary>
    public void Edit(string name, IEnumerable<PaletteBank> banks, Action apply, IEnumerable<string>? metas = null, IEnumerable<string>? images = null)
    {
        var paths = banks.SelectMany(b => b.Assets).Concat(images ?? []).ToList();
        var metaList = metas?.ToList() ?? [];
        var before = Capture(paths, metaList);
        apply();
        var after = Capture(paths.Concat(banks.SelectMany(b => b.Assets)), metaList);
        _undo.Push(new DelegateAction(name, () => Restore(before), () => Restore(after)));
        Touch();
    }

    void Touch()
    {
        IsDirty = true;
        Changed?.Invoke();
    }

    /// <summary>Raises <see cref="Changed"/> after a caller changed images or meta outside an <see cref="Edit"/>.</summary>
    public void NotifyChanged() => Touch();

    /// <summary>
    /// Records pixels already changed (a pencil stroke) as one undo step: <paramref name="before"/> is the image as it
    /// was when the stroke started.
    /// </summary>
    public void RecordImageEdit(string name, string path, int beforeW, int beforeH, uint[] before)
    {
        var img = Image(path);
        int afterW = img.Width, afterH = img.Height;
        var after = (uint[])img.Pixels.Clone();
        var first = (uint[])before.Clone();
        _undo.Push(new DelegateAction(name,
            () => { img.Replace(beforeW, beforeH, (uint[])first.Clone()); Touch(); },
            () => { img.Replace(afterW, afterH, (uint[])after.Clone()); Touch(); }));
        Touch();
    }

    // ---- colour edits ----------------------------------------------------------------------------------------

    sealed class ColourDrag
    {
        public required PaletteBank Bank { get; init; }
        public required int Index { get; init; }
        public required Snapshot Before { get; init; }
        public required List<(EditableImage Image, int[] Pixels)> Targets { get; init; }
        public Snapshot? After { get; set; }
        public DelegateAction? Action { get; set; }
        public int UndoCount { get; set; }
    }

    ColourDrag? _drag;

    /// <summary>
    /// Sets one colour of a bank and recolours every pixel of the bank's art that used it. Consecutive edits of the
    /// same colour (dragging a slider) are one undo step.
    /// </summary>
    public void SetColor(PaletteBank bank, int index, uint argb)
    {
        if (index < 0 || index >= bank.Colors.Count || bank.IsTransparentIndex(index)) return;
        argb |= 0xFF000000u;
        var d = _drag;
        var coalesce = d != null && d.Bank == bank && d.Index == index && d.Action != null && _undo.UndoCount == d.UndoCount
                       && _undo.NextUndo == d.Action.Name && !_undo.CanRedo;
        if (!coalesce)
        {
            var old = Colour15.Of(bank.Colors[index]);
            var targets = new List<(EditableImage, int[])>();
            foreach (var a in bank.Assets)
            {
                if (TryImage(a) is not { } img) continue;
                var idx = new List<int>();
                for (var i = 0; i < img.Pixels.Length; i++)
                    if (!Colour15.IsTransparent(img.Pixels[i]) && Colour15.Of(img.Pixels[i]) == old) idx.Add(i);
                targets.Add((img, idx.ToArray()));
            }
            d = _drag = new ColourDrag { Bank = bank, Index = index, Before = Capture(bank.Assets), Targets = targets };
        }
        bank.Colors[index] = argb;
        foreach (var (img, idx) in d!.Targets)
        {
            foreach (var i in idx) img.Pixels[i] = argb;
            img.Touch();
        }
        d.After = Capture(bank.Assets);
        if (!coalesce)
        {
            var drag = d;
            d.Action = new DelegateAction($"Edit colour {index}", () => Restore(drag.Before), () => Restore(drag.After!));
            _undo.Push(d.Action);
            d.UndoCount = _undo.UndoCount;
        }
        Touch();
    }

    /// <summary>Merge result: kept colours and, for every removed index, the index it was merged into.</summary>
    public static (List<uint> Kept, Dictionary<int, int> Map) MergePlan(PaletteBank bank, int threshold = MergeThreshold)
    {
        var kept = new List<uint>();
        var keptIndex = new List<int>();
        var map = new Dictionary<int, int>();
        for (var i = 0; i < bank.Colors.Count; i++)
        {
            if (bank.IsTransparentIndex(i)) { kept.Add(bank.Colors[i]); keptIndex.Add(i); continue; }
            var c = Colour15.Of(bank.Colors[i]);
            var into = -1;
            for (var k = 0; k < kept.Count; k++)
            {
                if (bank.IsTransparentIndex(keptIndex[k])) continue;
                if (Gba.Distance(Colour15.Of(kept[k]), c) <= threshold) { into = k; break; }
            }
            if (into < 0) { kept.Add(bank.Colors[i]); keptIndex.Add(i); }
            else map[i] = into;
        }
        return (kept, map);
    }

    /// <summary>Merges near-duplicate colours (distance within <see cref="MergeThreshold"/>); pixels move to the kept colour. Returns how many were removed.</summary>
    public int MergeNearDuplicates(PaletteBank bank, int threshold = MergeThreshold)
    {
        var (kept, map) = MergePlan(bank, threshold);
        if (map.Count == 0) return 0;
        Edit("Merge near-duplicate colours", [bank], () => ApplyRemap(bank, kept, map.ToDictionary(kv => bank.Colors[kv.Key], kv => kept[kv.Value])));
        return map.Count;
    }

    // recolours the bank's art: every pixel whose 15-bit value matches a key becomes the mapped colour
    void ApplyRemap(PaletteBank bank, List<uint> colors, Dictionary<uint, uint> remap)
    {
        var by15 = new Dictionary<ushort, uint>();
        foreach (var (from, to) in remap) by15.TryAdd(Colour15.Of(from), to | 0xFF000000u);
        foreach (var a in bank.Assets)
        {
            if (TryImage(a) is not { } img) continue;
            var changed = false;
            for (var i = 0; i < img.Pixels.Length; i++)
            {
                var p = img.Pixels[i];
                if (Colour15.IsTransparent(p) || !by15.TryGetValue(Colour15.Of(p), out var to) || to == p) continue;
                img.Pixels[i] = to;
                changed = true;
            }
            if (changed) img.Touch();
        }
        bank.Colors = colors;
    }

    static List<uint> HueOrder(PaletteBank bank)
    {
        var start = bank.LockTransparent && bank.Colors.Count > 0 ? 1 : 0;
        var head = bank.Colors.Take(start);
        var sorted = bank.Colors.Skip(start).OrderBy(c => Colour15.Hue(Colour15.Of(c)))
            .ThenBy(c => Colour15.R(Colour15.Of(c)) + Colour15.G(Colour15.Of(c)) + Colour15.B(Colour15.Of(c)));
        return [.. head, .. sorted];
    }

    public void SortByHue(PaletteBank bank) => Edit("Sort by hue", [bank], () => bank.Colors = HueOrder(bank));

    /// <summary>Tools options of a bank (Palette Manager checkboxes). Turning Merge or Sort on applies it at once.</summary>
    public void SetOption(PaletteBank bank, string option, bool on)
    {
        switch (option)
        {
            case nameof(PaletteBank.MergeNear):
                Edit("Merge near-duplicate colours", [bank], () =>
                {
                    bank.MergeNear = on;
                    if (!on) return;
                    var (kept, map) = MergePlan(bank);
                    if (map.Count > 0) ApplyRemap(bank, kept, map.ToDictionary(kv => bank.Colors[kv.Key], kv => kept[kv.Value]));
                });
                break;
            case nameof(PaletteBank.SortByHue):
                Edit("Sort by hue", [bank], () =>
                {
                    bank.SortByHue = on;
                    if (on) bank.Colors = HueOrder(bank);
                });
                break;
            case nameof(PaletteBank.LockTransparent):
                Edit("Lock index 0 as transparent", [bank], () =>
                {
                    bank.LockTransparent = on;
                    if (bank.Colors.Count == 0) bank.Colors.Add(Colour15.Transparent);
                    if (on) bank.Colors[0] = Colour15.Transparent;
                    else if (Colour15.IsTransparent(bank.Colors[0])) bank.Colors[0] = 0xFF000000u;
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(option));
        }
    }

    /// <summary>Import .pal / .gpl: the file's colours replace the bank's by index and the art is recoloured to match.</summary>
    public void ImportColors(PaletteBank bank, string path)
    {
        var file = PaletteFiles.Read(path).Take(16).Select(Colour15.Argb).ToList();
        Edit("Import palette", [bank], () =>
        {
            var colors = new List<uint>(file);
            if (bank.LockTransparent) { if (colors.Count == 0) colors.Add(0); colors[0] = Colour15.Transparent; }
            var remap = new Dictionary<uint, uint>();
            var target = new PaletteBank { Colors = colors, LockTransparent = bank.LockTransparent };
            for (var i = 0; i < bank.Colors.Count; i++)
            {
                if (bank.IsTransparentIndex(i)) continue;
                var to = i < colors.Count && !target.IsTransparentIndex(i) ? colors[i] : colors[IndexOf(target, bank.Colors[i])];
                remap.TryAdd(bank.Colors[i], to);
            }
            ApplyRemap(bank, colors, remap);
            if (bank.MergeNear)
            {
                var (kept, map) = MergePlan(bank);
                if (map.Count > 0) ApplyRemap(bank, kept, map.ToDictionary(kv => bank.Colors[kv.Key], kv => kept[kv.Value]));
            }
            if (bank.SortByHue) bank.Colors = HueOrder(bank);
        });
    }

    /// <summary>Export: JASC .pal or GIMP .gpl (by extension), 16 colours as the ROM stores them.</summary>
    public static void Export(PaletteBank bank, string path)
    {
        var colors = Enumerable.Range(0, 16).Select(i => i < bank.Colors.Count ? bank.Value(i) : (ushort)0).ToList();
        PaletteFiles.Write(path, colors);
    }

    /// <summary>Palette Banks + / Create › Palette: a new empty bank in the next free slot. Null when all 16 are used.</summary>
    public PaletteBank? AddBank(BankKind kind, string name = "New palette")
    {
        var slot = NextSlot(kind);
        if (slot < 0) return null;
        var bank = new PaletteBank { Kind = kind, Slot = slot, Name = name, Colors = [Colour15.Transparent] };
        Edit("New palette", [], () => Banks.Add(bank));
        return bank;
    }

    /// <summary>
    /// Moves an asset into a bank. For sprites the first asset of an OBJ bank owns the palette and the others share it
    /// (SpriteMeta.Palette names the owner, which the build reads). Colours the bank lacks are added while there is room.
    /// </summary>
    public void Assign(string assetPath, PaletteBank target, bool undoable = true)
    {
        var rel = Rel(assetPath);
        if (BankOf(rel) == target) return;
        var isSprite = target.Kind == BankKind.Obj;
        var affected = Banks.Where(b => b.Assets.Contains(rel, StringComparer.OrdinalIgnoreCase)).Append(target).ToList();
        var metas = isSprite ? affected.SelectMany(b => b.Assets).Append(rel).ToList() : [];
        void Apply()
        {
            foreach (var b in Banks) b.Assets.RemoveAll(a => string.Equals(a, rel, StringComparison.OrdinalIgnoreCase));
            target.Assets.Add(rel);
            AddMissingColours(target, rel);
            if (isSprite) foreach (var b in affected) SyncOwners(b);
        }
        if (undoable) Edit("Change palette", affected, Apply, metas, [rel]);
        else { Apply(); Touch(); }
    }

    /// <summary>A bank for a new asset (import, New Sprite): the next free slot, not recorded for undo. Null when palette RAM is full.</summary>
    public PaletteBank? NewBankFor(BankKind kind, string assetName)
    {
        var slot = NextSlot(kind);
        if (slot < 0) return null;
        var bank = new PaletteBank { Kind = kind, Slot = slot, Name = Pretty(assetName), Colors = [Colour15.Transparent] };
        Banks.Add(bank);
        return bank;
    }

    void AddMissingColours(PaletteBank bank, string rel)
    {
        if (bank.Colors.Count == 0) bank.Colors.Add(Colour15.Transparent);
        foreach (var c in DeriveColors([rel]).Skip(1))
        {
            if (bank.Colors.Count >= 16) break;
            var v = Colour15.Of(c);
            if (!bank.Colors.Where((_, i) => !bank.IsTransparentIndex(i)).Any(x => Colour15.Of(x) == v)) bank.Colors.Add(c);
        }
    }

    void SyncOwners(PaletteBank bank)
    {
        if (bank.Kind != BankKind.Obj || bank.Assets.Count == 0) return;
        var owner = GameProject.AssetName(bank.Assets[0]);
        for (var i = 0; i < bank.Assets.Count; i++)
        {
            var m = Meta(bank.Assets[i]);
            var want = i == 0 ? "" : owner;
            if (m.Palette == want) continue;
            m.Palette = want;
            MarkMetaDirty(bank.Assets[i]);
        }
    }

    /// <summary>Assets › Optimize Palettes…: drops colours no pixel uses and merges near-duplicates in every bank. Returns the colours removed.</summary>
    public int Optimize()
    {
        var removed = 0;
        Edit("Optimize palettes", Banks.ToList(), () =>
        {
            foreach (var b in Banks)
            {
                var used = new HashSet<ushort>();
                foreach (var a in b.Assets)
                    if (TryImage(a) is { } img)
                        foreach (var p in img.Pixels) if (!Colour15.IsTransparent(p)) used.Add(Colour15.Of(p));
                var keep = b.Colors.Where((c, i) => b.IsTransparentIndex(i) || used.Contains(Colour15.Of(c))).ToList();
                removed += b.Colors.Count - keep.Count;
                b.Colors = keep;
                var (kept, map) = MergePlan(b);
                if (map.Count == 0) continue;
                removed += map.Count;
                ApplyRemap(b, kept, map.ToDictionary(kv => b.Colors[kv.Key], kv => kept[kv.Value]));
            }
        });
        return removed;
    }

    sealed class EffectEdit
    {
        public required string Key { get; init; }
        public required DelegateAction Action { get; set; }
        public Snapshot? After { get; set; }
        public int UndoCount { get; set; }
    }

    EffectEdit? _fx;

    /// <summary>Changes the palette effects as one undo step; repeated changes with the same <paramref name="coalesceKey"/> (a slider drag) merge.</summary>
    public void SetEffects(Action<PaletteEffectsData> change, string? coalesceKey = null)
    {
        var f = _fx;
        if (coalesceKey != null && f != null && f.Key == coalesceKey && _undo.UndoCount == f.UndoCount && _undo.NextUndo == f.Action.Name && !_undo.CanRedo)
        {
            change(Effects);
            f.After = Capture([]);
            Touch();
            return;
        }
        var before = Capture([]);
        change(Effects);
        var edit = new EffectEdit { Key = coalesceKey ?? Guid.NewGuid().ToString(), Action = null!, After = Capture([]) };
        edit.Action = new DelegateAction("Palette effect", () => Restore(before), () => Restore(edit.After!));
        _undo.Push(edit.Action);
        edit.UndoCount = _undo.UndoCount;
        _fx = edit;
        Touch();
    }

    // ---- save ----------------------------------------------------------------------------------------------------

    public PaletteLibraryFile ToFile() => new()
    {
        Banks = Banks.OrderBy(b => b.Kind).ThenBy(b => b.Slot).Select(b => new PaletteBankData
        {
            Kind = b.Kind, Slot = b.Slot, Name = b.Name, LockTransparent = b.LockTransparent, MergeNear = b.MergeNear, SortByHue = b.SortByHue,
            Colors = b.Colors.Select(c => Colour15.IsTransparent(c) ? "none" : Colour15.Hex24(c)).ToList(),
            Assets = [.. b.Assets],
        }).ToList(),
        Effects = Effects,
    };

    /// <summary>Writes palettes.json, every edited image and every edited sprite meta.</summary>
    public void SaveAll()
    {
        GameProject.Save(FilePath, ToFile());
        IsDirty = false;
        foreach (var img in _images.Values.Where(i => i.IsDirty).ToList()) img.Save();
        foreach (var m in _dirtyMetas.ToList()) GameProject.Save(GameProject.MetaPath(m), _metas[m]);
        _dirtyMetas.Clear();
        Changed?.Invoke();
    }

    /// <summary>Forgets an asset that was deleted or renamed outside the editor.</summary>
    public void Forget(string path)
    {
        var full = Full(Rel(path));
        _images.Remove(full);
        _metas.Remove(full);
        _dirtyMetas.Remove(full);
    }
}
