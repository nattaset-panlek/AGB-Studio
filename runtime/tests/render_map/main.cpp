// BG allocation, tileset loading and map streaming: after scrolling across a large map, every visible
// cell in VRAM must hold the expected screen entry, and per-frame DMA stays within budget.
#include <tonc.h>

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/map.hpp"
#include "agb/render/palette.hpp"

using namespace agb;
using namespace agb::render;

namespace {
// 8 tiles of 4bpp: tile t filled with colour index t (so every tile is distinguishable).
AGB_EWRAM_BSS u32 g_tiles[8 * 8];
const u16 kColors[16] = {0, 0x001F, 0x03E0, 0x7C00, 0x7FFF, 0x4210, 0x7C1F, 0x03FF};
const data::Palette kPal{kColors, 1};
// 6 metatiles using tiles 0..7 with flips and palette offsets.
const data::Metatile kMeta[6] = {
    {{0, 1, 2, 3}}, {{4, 5, 6, 7}}, {{1 | data::kSeHFlip, 2, 3, 4}},
    {{5, 6 | data::kSeVFlip, 7, 0}}, {{2, 2, 2, 2}}, {{7, 6, 5, 4}},
};
const data::Collision kCol[6] = {};
data::Tileset g_tileset;
constexpr int kW = 100, kH = 40;
AGB_EWRAM_BSS u16 g_cells[kW * kH];
data::MapLayer g_layer;

u16 expected(int tx, int ty, const bg::TilesetPlacement& p) {
    if (tx < 0 || ty < 0 || tx >= kW * 2 || ty >= kH * 2) return 0;
    const u16 mt = g_cells[(ty / 2) * kW + tx / 2];
    const u16 se = kMeta[mt].se[(tx & 1) + (ty & 1) * 2];
    return static_cast<u16>(((data::seTile(se) + p.firstTile) & 0x3FF) | (se & 0x0C00) | ((p.paletteBank & 15) << 12));
}

void setup() {
    for (int t = 0; t < 8; ++t)
        for (int r = 0; r < 8; ++r) g_tiles[t * 8 + r] = static_cast<u32>(t) * 0x11111111u;
    g_tileset = {};
    g_tileset.gfx = {g_tiles, sizeof g_tiles, 8, 4, false};
    g_tileset.palette = &kPal;
    g_tileset.metatiles = kMeta;
    g_tileset.collision = kCol;
    g_tileset.metatileCount = 6;
    for (int y = 0; y < kH; ++y)
        for (int x = 0; x < kW; ++x) g_cells[y * kW + x] = static_cast<u16>((x * 3 + y * 7) % 6);
    g_layer = {};
    g_layer.bg = 1;
    g_layer.type = data::LayerType::Tile;
    g_layer.priority = 1;
    g_layer.parallaxX = 256;
    g_layer.parallaxY = 256;
    g_layer.width = kW;
    g_layer.height = kH;
    g_layer.cells = g_cells;
    g_layer.tileset = &g_tileset;
}
}  // namespace

AGB_TEST(bg_allocation_and_config) {
    screenArena().reset();
    bg::reset();
    palettes::reset();
    bg::setVideoMode(0);
    const int sb = bg::allocScreenBlocks(bg::Size::S64x32);
    CHECK_EQ(sb, 30);  // two blocks from the top
    const int sb2 = bg::allocScreenBlocks(bg::Size::S32x32);
    CHECK_EQ(sb2, 29);
    const auto p = bg::reserveTiles(100);
    CHECK_EQ(p.charBlock, 0);
    CHECK_EQ(p.firstTile, 0);
    const auto p2 = bg::reserveTiles(10);
    CHECK_EQ(p2.firstTile, 100);
    CHECK_EQ(bg::uniqueTilesInBlock(0), 110);
    CHECK_EQ(bg::screenBlocksUsed(), 3 + 2);  // 3 map blocks + 2 units of tile data (3520 bytes)
    bg::Config c;
    c.charBlock = 0;
    c.screenBlock = static_cast<u8>(sb);
    c.priority = 2;
    c.size = bg::Size::S64x32;
    bg::configure(1, c);
    bg::setVisible(1, true);
    CHECK_EQ(commit::reg(commit::Bg1Cnt), static_cast<u16>(BG_CBB(0) | BG_SBB(30) | BG_PRIO(2) | BG_REG_64x32));
    CHECK(commit::reg(commit::DispCnt) & DCNT_BG1);
    for (int mode = 0; mode <= 3; ++mode) {
        bg::setVideoMode(mode);
        CHECK_EQ(commit::reg(commit::DispCnt) & 7, mode);
    }
    bg::setVideoMode(0);
}

AGB_TEST(map_streams_rows_and_columns) {
    screenArena().reset();
    bg::reset();
    palettes::reset();
    bg::setVideoMode(0);
    setup();
    MapLayerView view;
    CHECK(view.init(g_layer));
    view.setCamera(0, 0);
    view.drawAll();
    system::waitVBlank();
    const auto p = view.placement();
    CHECK(p.valid());
    // Palette went to a BG bank.
    CHECK_EQ(palettes::baseColor(p.paletteBank * 16 + 1), 0x001F);
    u32 maxBytes = 0;
    const int path[][2] = {{0, 0}, {37, 3}, {120, 60}, {500, 200}, {1100, 400}, {1300, 470}, {1200, 100}, {0, 0}};
    int failures = 0;
    for (const auto& target : path) {
        // Move in steps of up to 4 px per axis per frame, like a camera following the player.
        static int cx = 0, cy = 0;
        while (cx != target[0] || cy != target[1]) {
            cx += target[0] > cx ? (target[0] - cx > 4 ? 4 : target[0] - cx) : (cx - target[0] > 4 ? -4 : target[0] - cx);
            cy += target[1] > cy ? (target[1] - cy > 4 ? 4 : target[1] - cy) : (cy - target[1] > 4 ? -4 : target[1] - cy);
            view.setCamera(cx, cy);
            CHECK(view.commit());
            if (commit::queuedBytes() > maxBytes) maxBytes = commit::queuedBytes();
            system::waitVBlank();
            // Check every visible cell straight from VRAM.
            const u16* vram = commit::bgMap(view.screenBlock());
            for (int ty = cy >> 3; ty <= (cy + 159) >> 3; ++ty)
                for (int tx = cx >> 3; tx <= (cx + 239) >> 3; ++tx) {
                    const int x = tx & 63, y = ty & 31;
                    const u16 got = vram[(x >> 5) * 1024 + y * 32 + (x & 31)];
                    if (got != expected(tx, ty, p) && failures++ < 3)
                        debug::logf("  cell %d,%d at cam %d,%d: %x vs %x", tx, ty, cx, cy, got, expected(tx, ty, p));
                }
        }
    }
    CHECK_EQ(failures, 0);
    debug::logf("max map bytes per frame %lu", maxBytes);
    CHECK(maxBytes <= 2 * 1024);
}

AGB_TEST(parallax_scroll_registers) {
    screenArena().reset();
    bg::reset();
    palettes::reset();
    setup();
    g_layer.parallaxX = 51;  // x0.2
    g_layer.parallaxY = 128;  // x0.5
    MapLayerView view;
    CHECK(view.init(g_layer));
    view.setCamera(500, 200);
    CHECK_EQ(view.scrollX(), 99);
    CHECK_EQ(view.scrollY(), 100);
    CHECK_EQ(commit::reg(commit::Bg1HOfs), 99);
    CHECK_EQ(commit::reg(commit::Bg1VOfs), 100);
    g_layer.parallaxX = g_layer.parallaxY = 256;
}

int main() {
    system::init();
    test::runAll();
}
