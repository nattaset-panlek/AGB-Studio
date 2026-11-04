// Background layers: video mode, BG control, VRAM allocation of tile and map blocks, scrolling.
// Tile data is allocated upward from character block 0, maps downward from screen block 31,
// so the editor's layer table (T-P03) can show "char block N · screen N" for every layer.
#pragma once
#include "agb/data.hpp"
#include "agb/types.hpp"

namespace agb::render::bg {

enum class Size : u8 { S32x32, S64x32, S32x64, S64x64 };  // regular layers, in 8x8 tiles
enum class AffineSize : u8 { S16, S32, S64, S128 };

struct Config {
    u8 charBlock = 0;
    u8 screenBlock = 31;
    u8 priority = 0;
    Size size = Size::S32x32;
    AffineSize affineSize = AffineSize::S32;
    bool bpp8 = false;
    bool mosaic = false;
    bool wrap = true;  // affine layers only
};

// Where a tileset's hardware tiles ended up in VRAM.
struct TilesetPlacement {
    s8 charBlock = -1;
    u16 firstTile = 0;   // tile index from the char block start
    u8 paletteBank = 0;  // first BG palette bank of the tileset
    bool valid() const { return charBlock >= 0; }
};

void reset();  // frees all VRAM blocks and hides every layer
void setVideoMode(int mode);  // 0..3
int videoMode();
void configure(int bg, const Config& config);
const Config& config(int bg);
void setVisible(int bg, bool visible);
bool visible(int bg);
void setScroll(int bg, int x, int y);
void setObjVisible(bool visible);

// Loads a tileset's tiles (decompressing LZ77) and palette while the screen is blank / faded.
// Tilesets already loaded are shared. Returns an invalid placement when VRAM is full.
TilesetPlacement loadTileset(const data::Tileset* tileset);
// Loads raw tiles into a char block (image and text layers).
TilesetPlacement loadTiles(const data::TileGfx& gfx, int charBlock = -1);
// Reserves `tiles` empty tiles (text windows); returns the placement of the first.
TilesetPlacement reserveTiles(u16 tiles, bool bpp8 = false);
// From this char block on (the text layer: its map entries reach 1024 tiles from its own block).
TilesetPlacement reserveTilesFrom(u16 tiles, int firstBlock, bool bpp8 = false);
TilesetPlacement reserveFreshBlock(u16 tiles);  // the start of a block nothing else uses, else anywhere
// Tile allocation mark: overlays (pause, options) reserve tiles on top of the scene and give them back.
struct Mark {
    u16 tilesInBlock[4];
    u32 units;
};
Mark mark();
void rewind(const Mark& m);
// Screen blocks for a layer of the given size; returns the first block or -1.
int allocScreenBlocks(Size size);
int allocAffineScreenBlocks(AffineSize size);

// Advances animated tiles of every loaded tileset (called once per frame by the scene).
void animateTiles();

// Budget figures for Budget Details / Build Report (bytes and counts).
u32 tileBytesUsed();
int screenBlocksUsed();
u16 uniqueTilesInBlock(int charBlock);

int screenBlockCount(Size size);
int widthTiles(Size size);
int heightTiles(Size size);

}  // namespace agb::render::bg
