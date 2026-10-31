// P3.X1 stress picture: four scrolling tile layers (parallax x1.0, x0.5, x0.2, x1.2), 128 animated moving sprites and
// music, for 600 frames (10 s). The profiler must count no frame that missed its VBlank, every sprite must be drawn
// every frame, and the streamed maps must be right in VRAM at the end. The log ends with the profiler capture.
#include <tonc.h>

#include "../test.hpp"
#include "agb/audio.hpp"
#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/map.hpp"
#include "agb/render/palette.hpp"
#include "agb/render/sprite.hpp"
#include "soundbank.hpp"

using namespace agb;
using namespace agb::render;

extern const unsigned char agb_soundbank[];

namespace {
// ---- four layers: 8 tiles of 4bpp (tile t = colour t), 6 metatiles, 100 x 40 metatile maps -------------------------
AGB_EWRAM_BSS u32 g_tiles[8 * 8];
const u16 kColors[4][16] = {
    {0, 0x001F, 0x03E0, 0x7C00, 0x7FFF, 0x4210, 0x7C1F, 0x03FF},
    {0, 0x0010, 0x0200, 0x4000, 0x6318, 0x2108, 0x4010, 0x0210},
    {0, 0x1CE7, 0x2D6B, 0x3DEF, 0x4E73, 0x5EF7, 0x6F7B, 0x7FFF},
    {0x5294, 0x0C63, 0x18C6, 0x2529, 0x318C, 0x3DEF, 0x4A52, 0x56B5},
};
const data::Palette kPal[4] = {{kColors[0], 1}, {kColors[1], 1}, {kColors[2], 1}, {kColors[3], 1}};
// Metatile 0 is clear so the layers behind show through; the back layer (BG3) is opaque everywhere.
const data::Metatile kMeta[6] = {
    {{0, 0, 0, 0}}, {{1, 2, 3, 4}}, {{5, 6, 7, 1}}, {{2, 0, 0, 2}}, {{3, 3, 3, 3}}, {{7, 6, 5, 4}},
};
const data::Collision kCol[6] = {};
data::Tileset g_tilesets[4];
constexpr int kW = 100, kH = 40;
AGB_EWRAM_BSS u16 g_cells[4][kW * kH];
data::MapLayer g_layers[4];
MapLayerView g_views[4];
const s16 kParallax[4] = {256, 128, 51, 307};  // x1.0, x0.5, x0.2, x1.2

u16 cellAt(int layer, int x, int y) {
    if (layer == 3) return static_cast<u16>(1 + (x * 5 + y * 3) % 5);
    const int v = (x * (3 + layer) + y * (7 - layer)) % 11;
    return static_cast<u16>(v < 6 ? v : 0);
}

// ---- 128 sprites: 16 x 16, 4 animation frames --------------------------------------------------------------------
AGB_EWRAM_BSS u32 g_frames[4 * 4 * 8];
const u16 kSprColors[16] = {0, 0x7FFF, 0x001F, 0x03E0, 0x7C00};
const data::Palette kSprPal{kSprColors, 1};
const u8 kRun[] = {0, 1, 2, 3}, kRunTicks[] = {5, 5, 5, 5};
const data::AnimDef kAnims[] = {{"run", kRun, kRunTicks, 4, true, false}};
data::SpriteSheet g_sheet;
struct Mover {
    sprites::Id id;
    s16 x, y, vx, vy;
};
Mover g_movers[sprites::kMaxSprites];

void setup() {
    for (int t = 0; t < 8; ++t)
        for (int r = 0; r < 8; ++r) g_tiles[t * 8 + r] = (r & 1) ? static_cast<u32>(t) * 0x11111111u : static_cast<u32>(t) * 0x01010101u * 0x11u;
    for (int i = 0; i < 4; ++i) {
        g_tilesets[i] = {};
        g_tilesets[i].gfx = {g_tiles, sizeof g_tiles, 8, 4, false};
        g_tilesets[i].palette = &kPal[i];
        g_tilesets[i].metatiles = kMeta;
        g_tilesets[i].collision = kCol;
        g_tilesets[i].metatileCount = 6;
        for (int y = 0; y < kH; ++y)
            for (int x = 0; x < kW; ++x) g_cells[i][y * kW + x] = cellAt(i, x, y);
        g_layers[i] = {static_cast<u8>(i), data::LayerType::Tile, static_cast<u8>(i), false, kParallax[i], kParallax[i], kW, kH, g_cells[i], &g_tilesets[i]};
    }
    for (int f = 0; f < 4; ++f)
        for (int i = 0; i < 32; ++i) g_frames[f * 32 + i] = (i & 4) ? static_cast<u32>(f + 1) * 0x11111111u : 0x11111111u;
    g_sheet = {};
    g_sheet.gfx = {g_frames, sizeof g_frames, 16, 4, false};
    g_sheet.palette = &kSprPal;
    g_sheet.width = g_sheet.height = 16;
    g_sheet.shape = data::ObjShape::Square;
    g_sheet.sizeCode = 1;
    g_sheet.tilesPerFrame = 4;
    g_sheet.frameCount = 4;
    g_sheet.anims = kAnims;
    g_sheet.animCount = 1;
}
}  // namespace

AGB_TEST(four_layers_128_sprites_and_music_at_60_fps) {
    audio::init({agb_soundbank, 8});
    setup();
    screenArena().reset();
    bg::reset();
    palettes::reset();
    sprites::reset();
    bg::setVideoMode(0);
    for (int i = 0; i < 4; ++i) {
        CHECK(g_views[i].init(g_layers[i]));
        g_views[i].setCamera(0, 0);
        g_views[i].drawAll();
    }
    for (int i = 0; i < sprites::kMaxSprites; ++i) {
        Mover& m = g_movers[i];
        m.id = sprites::create(&g_sheet, static_cast<u8>(i % 4));
        CHECK(m.id != sprites::kNone);
        m.x = static_cast<s16>((i % 16) * 15);
        m.y = static_cast<s16>((i / 16) * 20 + 8);
        m.vx = static_cast<s16>(1 + i % 3);
        m.vy = static_cast<s16>((i & 1) ? 1 : -1);
        sprites::play(m.id, 0);
        sprites::setFrame(m.id, static_cast<u16>(i % 4));
    }
    CHECK(audio::ready());
    audio::playMusic(agb_sound::MOD_TUNE);
    system::waitVBlank();
    system::waitVBlank();

    const u32 overrunsBefore = profile::stats().overruns;
    const u32 deferredBefore = commit::deferredCount();
    u32 busyMax = 0, busySum = 0, logicMax = 0, renderMax = 0, audioMax = 0, vblankFreeMin = 0xFFFFFFFFu;
    int notDrawn = 0, mapDeferred = 0;
    int camX = 0, camY = 0, dx = 2, dy = 1;
    constexpr int kFrames = 600;
    for (int f = 0; f < kFrames; ++f) {
        profile::begin(profile::Category::Logic);
        for (Mover& m : g_movers) {
            m.x = static_cast<s16>(m.x + m.vx);
            m.y = static_cast<s16>(m.y + m.vy);
            if (m.x < 0 || m.x > 224) m.vx = static_cast<s16>(-m.vx);
            if (m.y < 8 || m.y > 152) m.vy = static_cast<s16>(-m.vy);
            sprites::setPosition(m.id, m.x, m.y);
        }
        // The camera sweeps the map in both directions.
        camX += dx;
        camY += dy;
        if (camX < 0 || camX > kW * 16 * 256 / 307 - 241) dx = -dx;
        if (camY < 0 || camY > kH * 16 * 256 / 307 - 161) dy = -dy;
        sprites::update();
        profile::begin(profile::Category::Render);
        if (sprites::build() != sprites::kMaxSprites) ++notDrawn;
        palettes::update();
        for (auto& v : g_views) {
            v.setCamera(camX, camY);
            if (!v.commit()) ++mapDeferred;
        }
        system::waitVBlank();
        const auto& s = profile::stats();
        const u32 busy = s.thisFrame[0] + s.thisFrame[1] + s.thisFrame[2];
        if (f < 2) continue;  // the first frames include the set-up above
        busySum += busy;
        if (busy > busyMax) busyMax = busy;
        if (s.thisFrame[0] > logicMax) logicMax = s.thisFrame[0];
        if (s.thisFrame[1] > renderMax) renderMax = s.thisFrame[1];
        if (s.thisFrame[2] > audioMax) audioMax = s.thisFrame[2];
        if (s.vblankFreeCycles < vblankFreeMin) vblankFreeMin = s.vblankFreeCycles;
    }
    const u32 overruns = profile::stats().overruns - overrunsBefore;
    CHECK_EQ(overruns, 0u);
    CHECK_EQ(notDrawn, 0);
    CHECK_EQ(mapDeferred, 0);
    CHECK_EQ(commit::deferredCount(), deferredBefore);
    CHECK(audio::musicPlaying());
    CHECK(vblankFreeMin > 0);  // every commit finished inside VBlank
    // The streamed maps are right in VRAM for the final camera.
    int wrong = 0;
    for (int i = 0; i < 4; ++i) {
        const u16* vram = commit::bgMap(g_views[i].screenBlock());
        const int sx = g_views[i].scrollX(), sy = g_views[i].scrollY();
        for (int ty = sy >> 3; ty <= (sy + 159) >> 3; ++ty)
            for (int tx = sx >> 3; tx <= (sx + 239) >> 3; ++tx) {
                const u16 got = vram[((tx & 63) >> 5) * 1024 + (ty & 31) * 32 + (tx & 31)];
                if (got != g_views[i].cell(tx, ty)) ++wrong;
            }
    }
    CHECK_EQ(wrong, 0);
    const u32 avg = busySum / (kFrames - 2);
    debug::logf("profiler capture, %d frames: overruns %lu, busy max %lu cycles (%lu%% of 280896), average %lu (%lu%%)", kFrames - 2, overruns,
                busyMax, busyMax * 100 / profile::kCyclesPerFrame, avg, avg * 100 / profile::kCyclesPerFrame);
    debug::logf("  max per frame: logic %lu, render %lu, audio %lu; VBlank cycles left after the commit at least %lu", logicMax, renderMax,
                audioMax, vblankFreeMin);
    debug::logf("  sprites %d, OAM entries %d, OBJ tiles %d, BG layers 4, screen blocks %d", sprites::count(), sprites::kMaxSprites,
                sprites::vramTilesUsed(), bg::screenBlocksUsed());
}

int main() {
    system::init();
    test::runAll();
}
