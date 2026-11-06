// BG allocation, tileset loading and map streaming: after scrolling across a large map, every visible
// cell in VRAM must hold the expected screen entry, and per-frame DMA stays within budget.
#include "agb/hw.hpp"

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

// P5.01: a secondary tileset's own tiles load right after its primary's, and a tileset without a primary is unchanged.
AGB_TEST(secondary_tileset_loads_after_its_primary) {
    screenArena().reset();
    bg::reset();
    palettes::reset();
    bg::setVideoMode(0);
    setup();
    static u32 ownTiles[2 * 8];
    for (int t = 0; t < 2; ++t)
        for (int r = 0; r < 8; ++r) ownTiles[t * 8 + r] = static_cast<u32>(t + 8) * 0x11111111u & 0xFFFFFFFFu;
    static const u16 mergedColors[32] = {0, 0x001F, 0x03E0, 0x7C00, 0x7FFF, 0x4210, 0x7C1F, 0x03FF, 0, 0, 0, 0, 0, 0, 0, 0,
                                         0, 0x1234};
    static const data::Palette mergedPal{mergedColors, 2};
    data::Tileset secondary{};
    secondary.gfx = {ownTiles, sizeof ownTiles, 2, 4, false};
    secondary.palette = &mergedPal;
    secondary.metatiles = kMeta;
    secondary.collision = kCol;
    secondary.metatileCount = 6;
    secondary.primary = &g_tileset;
    const auto p = bg::loadTileset(&secondary);
    CHECK(p.valid());
    system::waitVBlank();
    const u32* vram = reinterpret_cast<const u32*>(commit::bgTiles(p.charBlock)) + p.firstTile * 8;
    CHECK_EQ(vram[0], 0x00000000u);
    CHECK_EQ(vram[7 * 8], 0x77777777u);
    CHECK_EQ(vram[8 * 8], 0x88888888u);
    CHECK_EQ(vram[9 * 8], 0x99999999u);
    CHECK_EQ(palettes::baseColor(p.paletteBank * 16 + 16 + 1), 0x1234);
    // Without a primary the same tileset loads on its own, eight tiles.
    const auto own = bg::loadTileset(&g_tileset);
    CHECK(own.valid());
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
    view.release();
    // x1.2 (parallax 307, the foreground layer of T-D03 / T-P03): the layer moves faster than the camera, the register
    // takes the low 9 bits, and the window it draws is the one at the faster scroll.
    screenArena().reset();
    bg::reset();
    palettes::reset();
    g_layer.parallaxX = g_layer.parallaxY = 307;
    MapLayerView front;
    CHECK(front.init(g_layer));
    front.setCamera(500, 200);
    CHECK_EQ(front.scrollX(), 599);  // 500 * 307 / 256 = 599.6
    CHECK_EQ(front.scrollY(), 239);  // 200 * 307 / 256 = 239.8
    CHECK_EQ(commit::reg(commit::Bg1HOfs), 599 & 0x1FF);
    CHECK_EQ(commit::reg(commit::Bg1VOfs), 239);
    CHECK(front.commit());
    system::waitVBlank();
    const u16* vram = commit::bgMap(front.screenBlock());
    int wrong = 0;
    for (int ty = 239 >> 3; ty <= (239 + 159) >> 3; ++ty)
        for (int tx = 599 >> 3; tx <= (599 + 239) >> 3; ++tx) {
            const int x = tx & 63, y = ty & 31;
            wrong += vram[(x >> 5) * 1024 + y * 32 + (x & 31)] != expected(tx, ty, front.placement());
        }
    CHECK_EQ(wrong, 0);
    front.release();
    g_layer.parallaxX = g_layer.parallaxY = 256;
}

namespace {
// ---- three layers like Sky Hopper's Forest_01, data in ROM: sky and hills are wrapping image layers (x0.2, x0.5), the
// ground is a metatile layer (x1) ----------------------------------------------------------------------------------
constexpr int kGW = 120, kGH = 40;     // ground, metatiles (1920 x 640 px)
constexpr int kImgW = 64, kImgH = 32;  // sky and hills, screen entries
struct RomMaps {
    u16 ground[kGW * kGH];
    u16 image[kImgW * kImgH];
};
constexpr RomMaps makeRomMaps() {
    RomMaps m{};
    for (int i = 0; i < kGW * kGH; ++i) m.ground[i] = static_cast<u16>((i * 7 + i / kGW) % 6);
    for (int i = 0; i < kImgW * kImgH; ++i) m.image[i] = static_cast<u16>(((i * 5 + i / kImgW) % 8) | ((i & 1) ? data::kSeHFlip : 0));
    return m;
}
constexpr RomMaps kRomMaps = makeRomMaps();
data::MapLayer g_forest[3];
MapLayerView g_forestViews[3];

u16 forestExpected(int layer, int tx, int ty) {
    const MapLayerView& v = g_forestViews[layer];
    const auto& p = v.placement();
    u16 se;
    if (layer < 2) {
        se = kRomMaps.image[((ty % kImgH + kImgH) % kImgH) * kImgW + (tx % kImgW + kImgW) % kImgW];
    } else {
        if (tx < 0 || ty < 0 || tx >= kGW * 2 || ty >= kGH * 2) return 0;
        se = kMeta[kRomMaps.ground[(ty / 2) * kGW + tx / 2]].se[(tx & 1) + (ty & 1) * 2];
    }
    return static_cast<u16>(((data::seTile(se) + p.firstTile) & 0x3FF) | (se & 0x0C00) | (((data::sePal(se) + p.paletteBank) & 15) << 12));
}

// Cells on screen that do not show what the map has at the layer's scroll registers (what the player sees).
int wrongOnScreen() {
    int wrong = 0;
    for (int i = 0; i < 3; ++i) {
        const u16* vram = commit::bgMap(g_forestViews[i].screenBlock());
        // The scroll the view shows (in map space); the registers hold its low 9 bits.
        const int px = g_forestViews[i].scrollX(), py = g_forestViews[i].scrollY();
        if (commit::reg(static_cast<commit::Reg>(commit::Bg0HOfs + i * 2)) != (px & 0x1FF) ||
            commit::reg(static_cast<commit::Reg>(commit::Bg0VOfs + i * 2)) != (py & 0x1FF))
            ++wrong;
        for (int ty = py >> 3; ty <= (py + 159) >> 3; ++ty)
            for (int tx = px >> 3; tx <= (px + 239) >> 3; ++tx) {
                const u16 got = vram[((tx & 63) >> 5) * 1024 + (ty & 31) * 32 + (tx & 31)];
                if (got != forestExpected(i, tx, ty) && wrong++ < 2) debug::logf("  layer %d cell %d,%d: %x vs %x", i, tx, ty, got, forestExpected(i, tx, ty));
            }
    }
    return wrong;
}
}  // namespace

AGB_TEST(big_camera_jump_fits_in_a_frame) {
    screenArena().reset();
    bg::reset();
    palettes::reset();
    bg::setVideoMode(0);
    setup();
    g_forest[0] = {0, data::LayerType::Image, 3, true, 51, 51, kImgW, kImgH, kRomMaps.image, &g_tileset};
    g_forest[1] = {1, data::LayerType::Image, 2, true, 128, 128, kImgW, kImgH, kRomMaps.image, &g_tileset};
    g_forest[2] = {2, data::LayerType::Tile, 1, false, 256, 256, kGW, kGH, kRomMaps.ground, &g_tileset};
    for (int i = 0; i < 3; ++i) {
        CHECK(g_forestViews[i].init(g_forest[i]));
        g_forestViews[i].setCamera(0, 0);
        g_forestViews[i].drawAll();
    }
    system::waitVBlank();
    CHECK_EQ(wrongOnScreen(), 0);
    // Camera.SetPosition / a respawn / a teleport: across the map in one frame, then a few frames of small moves.
    const int jumps[][2] = {{1500, 400}, {40, 30}, {900, 200}, {1680, 480}};
    u32 worst = 0;
    int wrong = 0, lagging = 0;
    for (const auto& j : jumps) {
        for (int f = 0; f < 8; ++f) {
            const int cx = j[0] + f * 2, cy = j[1] + f;
            const u32 t0 = profile::cyclesNow();
            for (auto& v : g_forestViews) {
                v.setCamera(cx, cy);
                v.commit();
            }
            const u32 spent = profile::cyclesNow() - t0;
            if (spent > worst) worst = spent;
            system::waitVBlank();
            wrong += wrongOnScreen();
            for (int i = 0; i < 3; ++i)
                if (g_forestViews[i].scrollX() != (cx * g_forest[i].parallaxX) >> 8 && f + 1 > lagging) lagging = f + 1;
        }
    }
    debug::logf("camera jump, 3 layers: worst frame %lu cycles (%lu%% of a frame); every layer at the camera after %d frames", worst,
                worst * 100 / profile::kCyclesPerFrame, lagging + 1);
    CHECK_EQ(wrong, 0);  // never a wrong cell on screen, also while a layer waits for its redraw
    CHECK(worst < profile::kCyclesPerFrame / 4);  // two whole-window redraws a frame (measured 54k cycles, 19%)
    CHECK(lagging <= 1);  // the third layer follows one frame later
}

int main() {
    system::init();
    // These tests load layers directly, without a scene's staged load: the whole run counts as one (P2.04).
    commit::beginStagedLoad();
    test::runAll();
}
