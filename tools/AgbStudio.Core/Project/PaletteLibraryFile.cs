namespace AgbStudio.Core.Project;

/// <summary>A palette RAM half: background (BG 0-15) or sprite (OBJ 0-15) banks.</summary>
public enum BankKind { Bg, Obj }

/// <summary>
/// assets/palettes/palettes.json (Palette Manager, T-P10): the banks of palette RAM, their names, colour order and
/// options, and the palette effects. Colours are "#RRGGBB" (the source colour; the ROM stores the nearest 15-bit value)
/// or "none" for the locked transparent index. Written and read by the editor (ArtLibrary); the build reads the art and
/// sprite meta, not this file. Schema: docs/schemas/palettes.schema.json (P1.04).
/// </summary>
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
