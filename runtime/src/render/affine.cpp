#include "agb/render/affine.hpp"

#include "agb/bios.hpp"
#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/effects.hpp"
#include "agb/render/palette.hpp"

namespace agb::render::affine {

void buildMap(u16* vram, int mapSide, const u8* cells, int width, int height, int add);  // affine_map.iwram.cpp

namespace {
AGB_EWRAM_BSS s32 g_table[161 * 4];

// 65536 / n for n = 1..160: per-line divisions without dividing at runtime.
struct Recip {
    u32 v[161];
    constexpr Recip() : v() {
        for (int i = 1; i <= 160; ++i) v[i] = 65536u / static_cast<u32>(i);
    }
};
constexpr Recip kRecip{};

bg::AffineSize sizeFor(int tiles) {
    return tiles <= 16 ? bg::AffineSize::S16 : tiles <= 32 ? bg::AffineSize::S32 : tiles <= 64 ? bg::AffineSize::S64 : bg::AffineSize::S128;
}
}  // namespace

bool load(int bg, const data::MapLayer& layer) {
    if (!layer.tileset) return false;
    const int side = layer.width;
    const auto size = sizeFor(side);
    static constexpr int kSide[4] = {16, 32, 64, 128};
    const int mapSide = kSide[static_cast<int>(size)];
    // Affine tiles are addressed from the start of the char block, so the block must be empty.
    int charBlock = -1;
    for (int cb = 0; cb < 4 && charBlock < 0; ++cb)
        if (bg::uniqueTilesInBlock(cb) == 0) charBlock = cb;
    if (charBlock < 0) {
        debug::log("warning: no free 16 KB block of background memory for a rotate / scale layer, so it is not shown. Use fewer or smaller layers in this scene.");
        return false;
    }
    const auto placement = bg::loadTiles(layer.tileset->gfx, charBlock);
    if (layer.tileset->palette) {
        // 8bpp tiles index the whole BG palette: colour bank * 16 + n, with the build's banks counted from 0. When
        // another layer already holds bank 0, the colours go where the palette manager puts them and the tile
        // pixels move by the same number of banks.
        const int base = palettes::acquire(layer.tileset->palette, false);
        if (base > 0) {
            u16* px = reinterpret_cast<u16*>(reinterpret_cast<u8*>(commit::bgTiles(charBlock)) + placement.firstTile * 64);
            const u16 add = static_cast<u16>(base * 16);
            for (u32 i = 0; i < static_cast<u32>(layer.tileset->gfx.tileCount) * 32; ++i) {
                const u16 v = px[i];
                const u16 lo = v & 0xFF, hi = v >> 8;
                px[i] = static_cast<u16>((lo ? lo + add : 0) | ((hi ? hi + add : 0) << 8));
            }
        }
    }
    const int sb = bg::allocAffineScreenBlocks(size);
    if (sb < 0) return false;
    // The build pads maps to the hardware size, so they copy as they are; other sizes are padded here.
    const u8* bytes = reinterpret_cast<const u8*>(layer.cells);
    if (layer.width == mapSide && layer.height == mapSide && placement.firstTile == 0)
        commit::copyNow(commit::bgMap(sb), bytes, static_cast<u32>(mapSide * mapSide));
    else
        buildMap(commit::bgMap(sb), mapSide, bytes, layer.width, layer.height, placement.firstTile);
    bg::Config c;
    c.charBlock = static_cast<u8>(charBlock);
    c.screenBlock = static_cast<u8>(sb);
    c.priority = layer.priority;
    c.affineSize = size;
    c.bpp8 = true;
    c.wrap = layer.wrap;
    bg::configure(bg, c);
    bg::setVisible(bg, true);
    setTransform(bg, fixed(1), 0, fixed(), fixed(), 0, 0);
    return true;
}

void setTransform(int bg, fixed scale, u8 angle, fixed cx, fixed cy, int sx, int sy) {
    if (bg < 2) return;
    const fixed inv = scale.raw ? fixed(1) / scale : fixed(1);
    const fixed c = math::cos(angle) * inv, s = math::sin(angle) * inv;
    const s16 pa = static_cast<s16>(c.raw), pb = static_cast<s16>(-s.raw), pc = static_cast<s16>(s.raw), pd = static_cast<s16>(c.raw);
    const int base = bg == 2 ? commit::Bg2Pa : commit::Bg3Pa;
    commit::setReg(static_cast<commit::Reg>(base), static_cast<u16>(pa));
    commit::setReg(static_cast<commit::Reg>(base + 1), static_cast<u16>(pb));
    commit::setReg(static_cast<commit::Reg>(base + 2), static_cast<u16>(pc));
    commit::setReg(static_cast<commit::Reg>(base + 3), static_cast<u16>(pd));
    // Reference point: texture coordinate shown at the screen origin.
    const s32 x = cx.raw - (pa * sx + pb * sy);
    const s32 y = cy.raw - (pc * sx + pd * sy);
    commit::setBgRef(bg, x, y);
}

void mode7(const Mode7& m) {
    const fixed cs = math::cos(m.angle), sn = math::sin(m.angle);
    for (int line = 0; line < 161; ++line) {
        s32* e = g_table + line * 4;
        const int dy = line - m.horizon;
        if (dy <= 0 || line >= 160) {
            // Above the horizon: show a far-away empty part of the texture.
            e[0] = 0;
            e[1] = 0;
            e[2] = -0x100000;
            e[3] = -0x100000;
            continue;
        }
        // lam = height / dy (24.8): texture pixels per screen pixel on this line.
        const s32 lam = static_cast<s32>((static_cast<u32>(m.camHeight) * kRecip.v[dy]) >> 8);
        const s32 pa = (-sn.raw * lam) >> 8;  // d(u)/dx along the screen line = right vector * lam
        const s32 pc = (cs.raw * lam) >> 8;
        // Distance ahead: z = lam * focal (160).
        const s32 z = lam * 160;
        // Centre of the line in texture space, then step back half a screen to x = 0.
        const s32 cxr = m.camX.raw + ((cs.raw * (z >> 8)) >> 0);
        const s32 cyr = m.camY.raw + ((sn.raw * (z >> 8)) >> 0);
        e[0] = static_cast<s32>(static_cast<u16>(pa)) | 0;  // PA (PB = 0)
        e[1] = static_cast<s32>(static_cast<u16>(pc));      // PC (PD = 0)
        e[2] = cxr - pa * 120;
        e[3] = cyr - pc * 120;
    }
    effects::startScanline(reinterpret_cast<const u32*>(g_table), 0x20, 4);
}

void stopMode7() { effects::stopScanline(); }

fixed2 mode7Ground(const Mode7& m, int x, int y) {
    const s32* e = g_table + (y < 0 ? 0 : y > 160 ? 160 : y) * 4;
    AGB_UNUSED(m);
    const s32 pa = static_cast<s16>(e[0] & 0xFFFF), pc = static_cast<s16>(e[1] & 0xFFFF);
    return {fixed::fromRaw(e[2] + pa * x), fixed::fromRaw(e[3] + pc * x)};
}

const s32* mode7Table() { return g_table; }

}  // namespace agb::render::affine
