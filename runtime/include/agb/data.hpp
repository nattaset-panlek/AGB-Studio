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

// A full-screen picture for the bitmap video modes (Scene › Picture, Bitmap.Show). A picture holds the forms the build
// found a use for: BGR555 pixels for Modes 3 and 5; for Mode 4, one index per pixel and the colours of indices 1...
struct Picture {
    const u16* bgr;      // width x height BGR555, row-major, word aligned; nullptr = none
    const u8* indices;   // width x height palette indices, row-major, word aligned (0 = see-through); nullptr = none
    const u16* palette;  // `colors` entries for palette indices 1...
    u16 colors;
    u16 width, height;
};

// Screen-entry bits (same as the GBA BG map format): tile index 0..1023, flips, palette bank.
constexpr u16 kSeHFlip = 1 << 10;
constexpr u16 kSeVFlip = 1 << 11;
constexpr u16 seTile(u16 se) { return se & 0x3FF; }
constexpr u16 sePal(u16 se) { return se >> 12; }

// Collision type painted on tiles in the Tilemap Editor (T-P03).
// SlopeUp rises to the right (45 degrees across the 16 px tile), SlopeDown rises to the left.
enum class Collision : u8 { None, Solid, OneWay, Ladder, Hazard, Water, SlopeUp, SlopeDown };

// A 16x16 "tile" of the editor = 2x2 hardware tiles. Screen entries are relative to the tileset:
// tile index from the tileset's first tile, palette bank from the tileset's first bank.
struct Metatile {
    u16 se[4];  // top-left, top-right, bottom-left, bottom-right
};

// Animated editor tile (Tilemap Editor tile property "Animation"): the base metatile owns 4 unique tiles at
// `baseTile`; every `ticksPerFrame` frames the next frame's 4 tiles (uncompressed, in `frameTiles`) are copied
// over them, so every copy of the tile on the map animates together.
struct TileAnim {
    u16 baseTile;          // first of the 4 tiles, relative to the tileset
    u8 frameCount;
    u8 ticksPerFrame;
    const void* frameTiles;  // frameCount * 4 tiles of 4bpp (32 bytes each)
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
    // P5.01: the shared primary tileset this one extends, or null. `gfx` then holds only this tileset's own tiles, which
    // load right after the primary's (tile indices of the metatiles count from the primary's first tile); `palette`
    // holds the primary's banks followed by the own banks.
    const Tileset* primary = nullptr;
};

enum class LayerType : u8 { Tile, Image, Affine, CollisionOnly };

struct MapLayer {
    u8 bg;              // 0..3
    LayerType type;
    u8 priority;        // 0 = front
    bool wrap;
    s16 parallaxX;      // 256 = moves with the camera, 51 = x0.2
    s16 parallaxY;
    u16 width, height;  // in metatiles (Image and Affine layers: in hardware tiles)
    // Metatile indices, row-major (Image layers: screen entries; Affine layers: one tile byte per cell, two cells
    // per u16, the low byte first, as the hardware map stores them).
    const u16* cells;
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
    const u16* ascii;  // 128 entries: glyph index for runtime ASCII text (scores), 0xFFFF = none
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

// Text tokens in a string: {0}..{3} show a value set at run time, <c1>..<c3> and </c> change the text colour. The build
// keeps kTextVarReserve pixels for a value when it breaks lines, and the runtime closes the gap on that line.
constexpr int kTextVarReserve = 56;
enum class TextMarkKind : u8 { Var, Color };
struct TextMark {
    u16 glyph;         // applies before this glyph of the run (count = after the last one)
    s16 x;             // pen x of a Var
    u8 line;
    TextMarkKind kind;
    u8 arg;            // Var: value index · Color: 0 the normal ink, 1..3 the project's rich text colours
};

struct TextRun {
    const Font* font;
    const PlacedGlyph* glyphs;
    u16 count;
    u8 lines;
    u8 steps;        // number of reveal steps
    const TextMark* marks = nullptr;
    u16 markCount = 0;
};

}  // namespace agb::data
