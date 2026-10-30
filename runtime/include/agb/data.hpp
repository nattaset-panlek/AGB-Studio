// Asset data contract between the toolchain (tools/AgbStudio.Build codegen) and the runtime.
// Everything here is const data in ROM; the build writes these structs into build/gen/*.cpp.
#pragma once
#include "agb/fixed.hpp"
#include "agb/types.hpp"

namespace agb::data {

// Hardware 8x8 tiles. 4bpp tiles are 32 bytes, 8bpp tiles 64 bytes. Optionally BIOS LZ77.
struct TileGfx {
    const void* data;
    u32 bytes;        // size of `data` as stored (compressed size when lz77)
    u16 tileCount;    // decompressed tile count
    u8 bpp;           // 4 or 8
    bool lz77;
};

struct Palette {
    const u16* colors;  // BGR555, 16 per bank
    u8 banks;           // number of 16-colour banks
};

// Screen-entry bits (same as the GBA BG map format): tile index 0..1023, flips, palette bank.
constexpr u16 kSeHFlip = 1 << 10;
constexpr u16 kSeVFlip = 1 << 11;
constexpr u16 seTile(u16 se) { return se & 0x3FF; }
constexpr u16 sePal(u16 se) { return se >> 12; }

// Collision type painted on tiles in the Tilemap Editor (T-P03).
enum class Collision : u8 { None, Solid, OneWay, Ladder, Hazard, Water };

// A 16x16 "tile" of the editor = 2x2 hardware tiles. Screen entries are relative to the tileset:
// tile index from the tileset's first tile, palette bank from the tileset's first bank.
struct Metatile {
    u16 se[4];  // top-left, top-right, bottom-left, bottom-right
};

struct TileAnim {
    u16 metatile;        // metatile whose tiles animate
    u8 frameCount;
    u8 ticksPerFrame;
    const u16* frames;   // per frame: first hardware tile of the 4 replacement tiles in `gfx`
};

struct Tileset {
    TileGfx gfx;
    const Palette* palette;
    const Metatile* metatiles;
    const Collision* collision;  // per metatile
    const u8* tags;              // per metatile, game-defined tag id (0 = none)
    u16 metatileCount;
    const TileAnim* anims;
    u8 animCount;
};

enum class LayerType : u8 { Tile, Image, Affine, CollisionOnly };

struct MapLayer {
    u8 bg;              // 0..3
    LayerType type;
    u8 priority;        // 0 = front
    bool wrap;
    s16 parallaxX;      // 256 = moves with the camera, 51 = x0.2
    s16 parallaxY;
    u16 width, height;  // in metatiles (Image layers: in hardware tiles)
    const u16* cells;   // metatile indices, row-major (Image layers: screen entries)
    const Tileset* tileset;
};

// ---- sprites --------------------------------------------------------------------------------------

enum class ObjShape : u8 { Square, Wide, Tall };

struct AnimDef {
    const char* name;
    const u8* frames;    // frame indices into the sheet
    const u8* ticks;     // ticks per frame (1/60 s)
    u8 frameCount;
    bool loop;
    bool pingPong;
};

struct SpriteSheet {
    TileGfx gfx;            // frames stored one after another, each `tilesPerFrame` tiles (1D mapping)
    const Palette* palette; // one bank
    u8 width, height;       // frame size in pixels (an OAM size)
    ObjShape shape;
    u8 sizeCode;            // OAM size bits 0..3
    u16 tilesPerFrame;
    u16 frameCount;
    const AnimDef* anims;
    u8 animCount;
    s8 originX, originY;    // pivot from the top-left of the frame
    s8 colliderX, colliderY;
    u8 colliderW, colliderH;
};

// ---- text -----------------------------------------------------------------------------------------

struct Glyph {
    u16 offset;      // byte offset of the bitmap in Font::bitmaps (packed rows, 2bpp or 1bpp)
    u8 width, height;
    s8 left, top;    // drawing offset from the pen position / baseline top
    u8 advance;
};

struct Font {
    const char* name;
    const Glyph* glyphs;
    const u8* bitmaps;
    u16 glyphCount;
    u8 bpp;          // 1 or 2
    u8 lineHeight;
    u8 baseline;
    bool shadow;
};

// Text is laid out by the build (Thai stacking and dictionary line breaks happen there), so the runtime
// only places glyphs: one entry per glyph to draw, line breaks already decided.
struct PlacedGlyph {
    u16 glyph;       // index into Font::glyphs
    s16 x;           // pen x in pixels from the line start
    s8 dy;           // vertical shift for stacked marks
    u8 line;         // line number
    u8 reveal;       // character step at which the glyph appears (typewriter); marks share their base
};

struct TextRun {
    const Font* font;
    const PlacedGlyph* glyphs;
    u16 count;
    u8 lines;
    u8 steps;        // number of reveal steps
};

}  // namespace agb::data
