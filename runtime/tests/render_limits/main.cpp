// G-HW.01 limits test ROM: background and sprite VRAM filled past the hardware limits at runtime (64 KB of background
// tiles in the tile modes, 32 KB of sprite tiles, 16 KB of sprite tiles in the bitmap modes). What does not fit is
// refused with its Console warning, and nothing already placed in VRAM changes.
#include <tonc.h>

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/bitmap.hpp"
#include "agb/render/palette.hpp"
#include "agb/render/sprite.hpp"

using namespace agb;
using namespace agb::render;

namespace {
constexpr int kSetTiles = 256;   // one background tile set: 8 KB of 4bpp tiles
constexpr int kFrameTiles = 64;  // one 64 x 64 sprite frame: 2 KB
constexpr int kFrames = 17;      // one more than 32 KB of sprite VRAM holds
AGB_EWRAM_BSS u32 g_data[kFrames * kFrameTiles * 8];

const u16 kColors[16] = {0, 0x7FFF, 0x001F, 0x03E0};
const data::Palette kPal{kColors, 1};
data::SpriteSheet g_sheet;

// Word `i` of the data with this seed: every set and frame differs everywhere, so an overwrite shows.
u32 word(u32 seed, u32 i) { return (seed * 0x9E3779B9u) ^ (i * 0x01000193u) ^ (i << 20); }
void fill(u32* dst, u32 seed, int words) {
    for (int i = 0; i < words; ++i) dst[i] = word(seed, static_cast<u32>(i));
}
int mismatches(const void* vram, u32 seed, int words) {
    const auto* v = static_cast<const volatile u32*>(vram);
    int n = 0;
    for (int i = 0; i < words; ++i) n += v[i] != word(seed, static_cast<u32>(i));
    return n;
}
u32 hash(const void* vram, u32 bytes) {
    const auto* v = static_cast<const volatile u32*>(vram);
    u32 h = 2166136261u;
    for (u32 i = 0; i < bytes / 4; ++i) h = (h ^ v[i]) * 16777619u;
    return h;
}

void setupSheet() {
    for (int f = 0; f < kFrames; ++f) fill(g_data + f * kFrameTiles * 8, static_cast<u32>(100 + f), kFrameTiles * 8);
    g_sheet = {};
    g_sheet.gfx = {g_data, sizeof g_data, static_cast<u16>(kFrames * kFrameTiles), 4, false};
    g_sheet.palette = &kPal;
    g_sheet.width = g_sheet.height = 64;
    g_sheet.shape = data::ObjShape::Square;
    g_sheet.sizeCode = 3;
    g_sheet.tilesPerFrame = kFrameTiles;
    g_sheet.frameCount = kFrames;
}

// One sprite per frame, all on screen at once (every frame stays in use, so none can be evicted).
void createSprites(sprites::Id* ids, int count) {
    for (int i = 0; i < count; ++i) {
        ids[i] = sprites::create(&g_sheet, 0);
        sprites::setPosition(ids[i], (i % 4) * 56, (i / 4) * 24);
        sprites::setFrame(ids[i], static_cast<u16>(i));
    }
}

// Builds the OAM until the frame copies that fit the DMA budget have all landed.
int buildSettled() {
    int drawn = 0;
    for (int f = 0; f < 4; ++f) {
        drawn = sprites::build();
        system::waitVBlank();
    }
    return drawn;
}

// Sprites drawn whose OAM tiles do not hold their own frame (OAM entry k is sprite k: same priority and order).
int wrongFrames(int drawn, int firstFrame = 0) {
    int wrong = 0;
    for (int k = 0; k < drawn; ++k) {
        const int tile = commit::oam()[k].attr2 & 0x3FF;
        wrong += mismatches(reinterpret_cast<const u8*>(commit::objTiles()) + tile * 32, static_cast<u32>(100 + firstFrame + k), kFrameTiles * 8) != 0;
    }
    return wrong;
}

void fresh() {
    bg::reset();
    palettes::reset();
    sprites::reset();
}
}  // namespace

AGB_TEST(bg_vram_full_refuses_and_keeps_what_is_placed) {
    fresh();
    bg::setVideoMode(0);
    commit::beginStagedLoad();  // tiles load at once, as in a scene's load step
    const data::TileGfx set{g_data, kSetTiles * 32, kSetTiles, 4, false};
    bg::TilesetPlacement placed[8];
    int n = 0;
    for (int i = 0; i < 8; ++i) {
        fill(g_data, static_cast<u32>(i + 1), kSetTiles * 8);
        placed[i] = bg::loadTiles(set);
        n += placed[i].valid();
    }
    CHECK_EQ(n, 8);  // eight 8 KB sets fill the 64 KB
    CHECK_EQ(bg::tileBytesUsed(), 64u * 1024);
    // Past the limit: another set, a single tile and a map are refused, each with its Console warning.
    const u32 before = hash(commit::bgTiles(0), 64 * 1024);
    const u32 log = debug::channel().logHead;
    fill(g_data, 99, kSetTiles * 8);
    CHECK(!bg::loadTiles(set).valid());
    CHECK(!bg::reserveTiles(1).valid());
    CHECK_EQ(bg::allocScreenBlocks(bg::Size::S32x32), -1);
    CHECK(test::loggedSince(log, "warning: background memory is full; 256 tiles could not be placed, so a layer or text is missing. Use fewer layers or a smaller tileset."));
    CHECK(test::loggedSince(log, "warning: no free background memory for a layer's map, so the layer is not shown. Use fewer layers in this scene."));
    // Nothing placed was overwritten: the whole background VRAM is as it was, and every set holds its own tiles.
    CHECK_EQ(hash(commit::bgTiles(0), 64 * 1024), before);
    int wrong = 0;
    for (int i = 0; i < 8; ++i)
        wrong += mismatches(reinterpret_cast<const u8*>(commit::bgTiles(placed[i].charBlock)) + placed[i].firstTile * 32, static_cast<u32>(i + 1), kSetTiles * 8);
    CHECK_EQ(wrong, 0);
    commit::endStagedLoad();
}

AGB_TEST(bitmap_mode_leaves_no_background_vram_for_tiles) {
    fresh();
    bitmap::begin();
    const data::TileGfx set{g_data, kSetTiles * 32, kSetTiles, 4, false};
    const u32 log = debug::channel().logHead;
    CHECK(!bg::loadTiles(set).valid());
    CHECK(test::loggedSince(log, "warning: background memory is full;"));
}

AGB_TEST(obj_vram_full_refuses_a_frame_and_keeps_the_others) {
    fresh();
    bg::setVideoMode(0);
    setupSheet();
    sprites::Id ids[kFrames];
    createSprites(ids, kFrames);
    const u32 log = debug::channel().logHead;
    const int drawn = buildSettled();
    // 16 frames of 2 KB fill the 32 KB; the 17th is refused and its sprite is not drawn (no garbage tiles).
    CHECK_EQ(drawn, 16);
    CHECK_EQ(sprites::vramTilesUsed(), 1024);
    CHECK(commit::oam()[16].attr0 & ATTR0_HIDE);
    CHECK(test::loggedSince(log, "warning: sprite memory is full, so a sprite frame could not be shown. Use fewer or smaller sprites in this scene."));
    CHECK_EQ(wrongFrames(drawn), 0);
    // Destroying one sprite frees its frame for the one that was refused.
    sprites::destroy(ids[0]);
    CHECK_EQ(buildSettled(), 16);
    CHECK_EQ(sprites::vramTilesUsed(), 1024);
    CHECK_EQ(wrongFrames(16, 1), 0);
    sprites::reset();
}

AGB_TEST(bitmap_mode_sprites_get_16_kb_and_leave_the_picture_alone) {
    fresh();
    bitmap::begin();
    // A picture in the frame buffer: the fill is queued and lands over several VBlanks (76,800 bytes, 16 KB a frame).
    CHECK(bitmap::fill(0, 0, bitmap::kWidth, bitmap::kHeight, 0x1234));
    for (int i = 0; i < 6; ++i) system::waitVBlank();
    CHECK_EQ(commit::queuedBytes(), 0u);
    CHECK_EQ(bitmap::frameBuffer()[159 * 240 + 239], 0x1234);
    const u32 picture = hash(bitmap::frameBuffer(), bitmap::kWidth * bitmap::kHeight * 2);
    setupSheet();
    sprites::Id ids[9];
    createSprites(ids, 9);
    const int drawn = buildSettled();
    // OBJ tiles 512..1023 only: 8 frames of 2 KB; the 9th is refused.
    CHECK_EQ(drawn, 8);
    CHECK_EQ(sprites::vramTilesUsed(), 512);
    int low = 0;
    for (int k = 0; k < drawn; ++k) low += (commit::oam()[k].attr2 & 0x3FF) < 512;
    CHECK_EQ(low, 0);
    CHECK(commit::oam()[8].attr0 & ATTR0_HIDE);  // (its warning is once per run: the test above checks it)
    CHECK_EQ(wrongFrames(drawn), 0);
    CHECK_EQ(hash(bitmap::frameBuffer(), bitmap::kWidth * bitmap::kHeight * 2), picture);
    sprites::reset();
}

int main() {
    system::init();
    test::runAll();
}
