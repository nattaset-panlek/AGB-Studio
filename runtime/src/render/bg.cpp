#include "agb/render/bg.hpp"

#include <tonc.h>

#include "agb/bios.hpp"
#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/render/palette.hpp"

namespace agb::render::bg {

namespace {
constexpr int kUnitBytes = 2048;  // one screen block; a char block is 8 units
constexpr int kUnits = 32;

Config g_config[4];
bool g_visible[4];
bool g_objVisible = true;
int g_mode = 0;
u32 g_unitUsed = 0;    // bit per 2 KB unit
u16 g_tilesInBlock[4];  // next free tile per char block (tile units of 32 bytes)

struct LoadedTileset {
    const data::Tileset* tileset;
    TilesetPlacement placement;
    u16 animTick;
};
LoadedTileset g_loaded[8];
int g_loadedCount = 0;

void markUnits(int first, int count) {
    for (int i = first; i < first + count && i < kUnits; ++i) g_unitUsed |= 1u << i;
}

bool unitsFree(int first, int count) {
    for (int i = first; i < first + count; ++i)
        if (i < 0 || i >= kUnits || (g_unitUsed & (1u << i))) return false;
    return true;
}

void writeDispCnt() {
    u16 v = static_cast<u16>(g_mode) | DCNT_OBJ_1D | (g_objVisible ? DCNT_OBJ : 0);
    for (int i = 0; i < 4; ++i)
        if (g_visible[i]) v |= static_cast<u16>(DCNT_BG0 << i);
    commit::setReg(commit::DispCnt, v);
}

void writeControl(int bg) {
    const Config& c = g_config[bg];
    const bool affine = (g_mode == 1 && bg == 2) || (g_mode == 2 && bg >= 2);
    u16 v = static_cast<u16>(BG_CBB(c.charBlock) | BG_SBB(c.screenBlock) | BG_PRIO(c.priority));
    if (c.mosaic) v |= BG_MOSAIC;
    if (affine) {
        v |= static_cast<u16>(static_cast<u16>(c.affineSize) << 14);
        if (c.wrap) v |= BG_WRAP;
    } else {
        v |= static_cast<u16>(static_cast<u16>(c.size) << 14);
        if (c.bpp8) v |= BG_8BPP;
    }
    commit::setReg(static_cast<commit::Reg>(commit::Bg0Cnt + bg), v);
}

// Tile data for a char block may spill into the following blocks (up to 1024 tiles = 32 KB in 4bpp).
int tileUnitsFor(int charBlock, u32 endByte) {
    return charBlock * 8 + static_cast<int>((endByte + kUnitBytes - 1) / kUnitBytes);
}
}  // namespace

void reset() {
    for (int i = 0; i < 4; ++i) {
        g_config[i] = Config{};
        g_visible[i] = false;
        g_tilesInBlock[i] = 0;
    }
    g_unitUsed = 0;
    g_loadedCount = 0;
    g_objVisible = true;
    writeDispCnt();
}

void setVideoMode(int mode) {
    g_mode = mode < 0 ? 0 : mode > 3 ? 3 : mode;
    writeDispCnt();
    for (int i = 0; i < 4; ++i) writeControl(i);
}

int videoMode() { return g_mode; }

void configure(int bg, const Config& config) {
    if (bg < 0 || bg > 3) return;
    g_config[bg] = config;
    writeControl(bg);
}

const Config& config(int bg) { return g_config[bg & 3]; }

void setVisible(int bg, bool v) {
    if (bg < 0 || bg > 3) return;
    g_visible[bg] = v;
    writeDispCnt();
}

bool visible(int bg) { return g_visible[bg & 3]; }

void setObjVisible(bool v) {
    g_objVisible = v;
    writeDispCnt();
}

void setScroll(int bg, int x, int y) {
    if (bg < 0 || bg > 3) return;
    commit::setReg(static_cast<commit::Reg>(commit::Bg0HOfs + bg * 2), static_cast<u16>(x & 0x1FF));
    commit::setReg(static_cast<commit::Reg>(commit::Bg0VOfs + bg * 2), static_cast<u16>(y & 0x1FF));
}

Mark mark() {
    Mark m{};
    for (int i = 0; i < 4; ++i) m.tilesInBlock[i] = g_tilesInBlock[i];
    m.units = g_unitUsed;
    return m;
}

void rewind(const Mark& m) {
    for (int i = 0; i < 4; ++i) g_tilesInBlock[i] = m.tilesInBlock[i];
    g_unitUsed = m.units;
}

TilesetPlacement reserveTiles(u16 tiles, bool bpp8) {
    const u32 bytes = static_cast<u32>(tiles) * (bpp8 ? 64 : 32);
    for (int cb = 0; cb < 4; ++cb) {
        const u32 startByte = static_cast<u32>(g_tilesInBlock[cb]) * 32;
        const u32 endByte = startByte + bytes;
        if (startByte / 32 + tiles > 1024) continue;
        const int firstUnit = cb * 8 + static_cast<int>(startByte / kUnitBytes);
        const int lastUnit = tileUnitsFor(cb, endByte);
        // Units already used by this block's own tiles are fine; anything else must be free.
        bool ok = lastUnit <= kUnits;
        for (int u = firstUnit; ok && u < lastUnit; ++u) {
            const bool ownPartial = u == firstUnit && startByte % kUnitBytes != 0;
            if (!ownPartial && (g_unitUsed & (1u << u))) ok = false;
        }
        if (!ok) continue;
        markUnits(firstUnit, lastUnit - firstUnit);
        TilesetPlacement p;
        p.charBlock = static_cast<s8>(cb);
        p.firstTile = static_cast<u16>(startByte / 32 / (bpp8 ? 2 : 1));
        g_tilesInBlock[cb] = static_cast<u16>(endByte / 32);
        return p;
    }
    debug::logf("warning: background VRAM is full; %u tiles could not be placed", tiles);
    return {};
}

TilesetPlacement loadTiles(const data::TileGfx& gfx, int charBlock) {
    TilesetPlacement p;
    if (charBlock >= 0) {
        // Fixed block (e.g. affine maps that must use block 0): place at its current end.
        p.charBlock = static_cast<s8>(charBlock);
        p.firstTile = static_cast<u16>(g_tilesInBlock[charBlock] / (gfx.bpp == 8 ? 2 : 1));
        const u32 end = static_cast<u32>(g_tilesInBlock[charBlock]) * 32 + static_cast<u32>(gfx.tileCount) * (gfx.bpp == 8 ? 64 : 32);
        markUnits(charBlock * 8, tileUnitsFor(charBlock, end) - charBlock * 8);
        g_tilesInBlock[charBlock] = static_cast<u16>(end / 32);
    } else {
        p = reserveTiles(gfx.tileCount, gfx.bpp == 8);
        if (!p.valid()) return p;
    }
    u8* dst = reinterpret_cast<u8*>(commit::bgTiles(p.charBlock)) + static_cast<u32>(p.firstTile) * (gfx.bpp == 8 ? 64 : 32);
    if (gfx.lz77) bios::lz77ToVram(gfx.data, dst);
    else commit::copyNow(dst, gfx.data, gfx.bytes);
    return p;
}

TilesetPlacement loadTileset(const data::Tileset* ts) {
    for (int i = 0; i < g_loadedCount; ++i)
        if (g_loaded[i].tileset == ts) return g_loaded[i].placement;
    TilesetPlacement p = loadTiles(ts->gfx);
    if (!p.valid()) return p;
    const int bank = ts->palette ? palettes::acquire(ts->palette, false) : 0;
    p.paletteBank = static_cast<u8>(bank < 0 ? 0 : bank);
    if (g_loadedCount < 8) g_loaded[g_loadedCount++] = {ts, p, 0};
    return p;
}

void animateTiles() {
    for (int i = 0; i < g_loadedCount; ++i) {
        LoadedTileset& l = g_loaded[i];
        const data::Tileset* ts = l.tileset;
        if (!ts->animCount) continue;
        ++l.animTick;
        for (int a = 0; a < ts->animCount; ++a) {
            const data::TileAnim& an = ts->anims[a];
            if (!an.ticksPerFrame || l.animTick % an.ticksPerFrame) continue;
            const u32 frame = (l.animTick / an.ticksPerFrame) % an.frameCount;
            u8* dst = reinterpret_cast<u8*>(commit::bgTiles(l.placement.charBlock)) + (static_cast<u32>(l.placement.firstTile) + an.baseTile) * 32;
            commit::queueCopy(dst, static_cast<const u8*>(an.frameTiles) + frame * 4 * 32, 4 * 32);
        }
    }
}

int allocScreenBlocks(Size size) {
    const int count = screenBlockCount(size);
    for (int first = kUnits - count; first >= 0; --first)
        if (unitsFree(first, count)) {
            markUnits(first, count);
            return first;
        }
    debug::log("warning: no free screen blocks for a background layer");
    return -1;
}

int allocAffineScreenBlocks(AffineSize size) {
    static constexpr int kBytes[4] = {256, 1024, 4096, 16384};
    const int count = (kBytes[static_cast<int>(size)] + kUnitBytes - 1) / kUnitBytes;
    for (int first = kUnits - count; first >= 0; --first)
        if (unitsFree(first, count)) {
            markUnits(first, count);
            return first;
        }
    debug::log("warning: no free screen blocks for an affine layer");
    return -1;
}

u32 tileBytesUsed() {
    u32 total = 0;
    for (u16 t : g_tilesInBlock) total += static_cast<u32>(t) * 32;
    return total;
}

int screenBlocksUsed() {
    int n = 0;
    for (int i = 0; i < kUnits; ++i) n += (g_unitUsed >> i) & 1;
    return n;
}

u16 uniqueTilesInBlock(int charBlock) { return g_tilesInBlock[charBlock & 3]; }

int screenBlockCount(Size size) { return size == Size::S32x32 ? 1 : size == Size::S64x64 ? 4 : 2; }
int widthTiles(Size size) { return size == Size::S64x32 || size == Size::S64x64 ? 64 : 32; }
int heightTiles(Size size) { return size == Size::S32x64 || size == Size::S64x64 ? 64 : 32; }

}  // namespace agb::render::bg
