using System.Text.Json.Serialization;

namespace AgbStudio.Core.Project;

// Per-asset metadata stored next to the source file as <file>.meta.json (blueprint 11): the import
// options shown in Import Asset, Sprite Editor and Tilemap Editor.

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssetKind { Sprite, Tileset, Image, Font, Music, Sound, Palette, Data, Unknown }

public sealed class SpriteMeta
{
    public int Format { get; set; } = 1;
    public int FrameWidth { get; set; } = 16;
    public int FrameHeight { get; set; } = 16;
    public string OamSize { get; set; } = "";       // "Square 16x16"; empty = from the frame size
    public int OriginX { get; set; } = 8;
    public int OriginY { get; set; } = 16;
    public ColliderData Collider { get; set; } = new() { X = 2, Y = 2, W = 12, H = 14 };
    public string Palette { get; set; } = "";       // shared palette name (OBJ bank) or empty = own
    public bool ReduceColors { get; set; } = true;  // "Reduce to 16 (4bpp)"
    public bool TrimEmptyFrames { get; set; }
    public bool Compress { get; set; }
    public List<AnimationData> Animations { get; set; } = new();
}

public sealed class AnimationData
{
    public string Name { get; set; } = "idle";
    public List<int> Frames { get; set; } = new() { 0 };
    public int Ticks { get; set; } = 6;             // per frame; Animation panel "6 ticks ≈ 10 fps"
    public List<int>? FrameTicks { get; set; }      // per-frame override
    public bool Loop { get; set; } = true;
    public bool PingPong { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollisionType { None, Solid, OneWay, Ladder, Hazard, Water }

// <tileset>.png.meta.json: each 16x16 block of the image is one editor tile (metatile).
public sealed class TilesetMeta
{
    public int Format { get; set; } = 1;
    public Dictionary<int, TileProperties> Tiles { get; set; } = new();
    public List<AutoTileSet> AutoTiles { get; set; } = new();
    public bool Compress { get; set; } = true;
}

public sealed class TileProperties
{
    public string Name { get; set; } = "";
    public CollisionType Collision { get; set; }
    public string Tag { get; set; } = "";
    public List<int>? Animation { get; set; }        // metatile frames
    public int AnimationTicks { get; set; } = 8;
}

// Auto-tile Rules panel: a rule set chooses a tile from the 3x3 neighbourhood while painting.
public sealed class AutoTileSet
{
    public string Name { get; set; } = "Grass edges";
    public List<AutoTileRule> Rules { get; set; } = new();
}

public sealed class AutoTileRule
{
    public string Name { get; set; } = "";           // Top edge, Left edge, Right edge, Corner, Inner
    // 3x3 mask row-major: 1 = same set, 0 = not the set, 2 = any. Centre is ignored.
    public int[] Mask { get; set; } = new int[9];
    public int Tile { get; set; }
}

public sealed class MusicMeta
{
    public int Format { get; set; } = 1;
    public double Volume { get; set; } = 0.8;
    public int LoopStart { get; set; }               // pattern order
    public bool FadeOnSceneChange { get; set; } = true;
}

public sealed class SoundMeta
{
    public int Format { get; set; } = 1;
    public int Rate { get; set; } = 11025;           // resampled to 11 kHz mono 8-bit
    public double Volume { get; set; } = 1;
}

// audio/sfx/<name>.sfx (SFX Designer)
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DmgChannelKind { Square1, Square2, Wave, Noise }

public sealed class SfxFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public DmgChannelKind Channel { get; set; } = DmgChannelKind.Square1;
    public bool Bake { get; set; }                    // Bake to .wav instead of hardware playback
    public int Volume { get; set; } = 15;            // initial envelope volume 0..15
    public double Duty { get; set; } = 0.5;          // 0.125 / 0.25 / 0.5 / 0.75
    public int EnvelopeStep { get; set; } = 2;       // 0 = off
    public bool EnvelopeUp { get; set; }
    public string StartNote { get; set; } = "C5";
    public int SweepTime { get; set; } = 3;          // Square 1 only, 0 = off
    public int SweepShift { get; set; } = 3;
    public bool SweepUp { get; set; } = true;
    public double Length { get; set; } = 0.21;       // seconds
    public int[] Wave { get; set; } = new int[32];   // Wave channel samples 0..15
    public int NoiseDivider { get; set; } = 3;
    public bool NoiseShort { get; set; }
}

// fonts/<name>.font (Font Manager, New Font)
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FontSourceKind { Ttf, Bdf, Image }

public sealed class FontFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public FontSourceKind SourceKind { get; set; }
    public string Source { get; set; } = "";         // file name in fonts/
    public int Size { get; set; } = 12;              // px
    public bool Variable { get; set; } = true;       // VWF
    public int FixedWidth { get; set; } = 8;
    public int CellHeight { get; set; } = 8;         // image fonts
    public List<string> CharacterSets { get; set; } = new() { "Basic Latin" };
    public string ExtraCharacters { get; set; } = "";
    public int LineHeight { get; set; } = 14;
    public int Baseline { get; set; } = 10;
    public int Spacing { get; set; } = 1;
    public int Bpp { get; set; } = 2;                // 2bpp = 4 shades
    public bool DropShadow { get; set; }
    public bool StackUpperMarks { get; set; } = true;
    public bool PlaceLowerVowels { get; set; } = true;
    public bool ThaiWordWrap { get; set; } = true;
    public Dictionary<string, GlyphOverride> Glyphs { get; set; } = new();  // edited pixels by code point
}

public sealed class GlyphOverride
{
    public int Advance { get; set; } = -1;
    public List<string> Rows { get; set; } = new();   // "0123" digits per pixel (shade 0..3)
}

// data/<table>.db (Database, New Table, Add Field)
public sealed class TableFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "Items";
    public string KeyField { get; set; } = "id";
    public bool KeepInRom { get; set; } = true;
    public List<FieldDef> Fields { get; set; } = new();
    public List<Dictionary<string, string>> Rows { get; set; } = new();
}

public sealed class FieldDef
{
    public string Name { get; set; } = "";
    // int, fixed, bool, text, enum, sprite, sound, script, reference
    public string Type { get; set; } = "int";
    public List<string> EnumValues { get; set; } = new();
    public string Default { get; set; } = "";
    public bool Localized { get; set; }
    public bool ShowInInspector { get; set; } = true;
    public string Reference { get; set; } = "";      // referenced table
}

// text/strings.<lang>.json (Localization)
public sealed class StringTable
{
    public int Format { get; set; } = 1;
    public string Language { get; set; } = "en";
    public SortedDictionary<string, string> Strings { get; set; } = new(StringComparer.Ordinal);
}

// text/strings.meta.json: per-key notes, group, speaker and box width for the fit check.
public sealed class StringMeta
{
    public int Format { get; set; } = 1;
    public SortedDictionary<string, StringInfo> Keys { get; set; } = new(StringComparer.Ordinal);
}

public sealed class StringInfo
{
    public string Group { get; set; } = "";          // Dialogue, UI & menus, Items
    public string Speaker { get; set; } = "";
    public string Notes { get; set; } = "";
    public int BoxWidth { get; set; } = 224;          // px
    public int MaxLines { get; set; } = 3;
}
