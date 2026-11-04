// Bitmap video modes 3, 4 and 5 (Video Mode popover T-O02): pictures, pixels, rectangles, page flips and sprites. Each
// picture is held on screen after a "shot <name>" log line; VideoModeTests.BitmapModesMatchReferenceImages captures it
// and compares it pixel for pixel with a reference image computed on the host from the same content. The checks in this
// ROM cover registers, VRAM contents and the order of queued writes.
#include <tonc.h>

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/bitmap.hpp"
#include "agb/render/effects.hpp"
#include "agb/render/palette.hpp"
#include "agb/render/sprite.hpp"

using namespace agb;
using namespace agb::render;

namespace {
// ---- content shared with the host reference (VideoModeTests.BitmapReference) ----------------------------------------
constexpr u16 kBackdrop = 0x4010;  // a purple the pictures do not use: where the backdrop shows

u16 bgrPixel(int x, int y) { return static_cast<u16>((x >> 3) | ((y >> 3) << 5) | ((((x ^ y) >> 4) & 31) << 10)); }
// Mode 4: a 64-colour pattern with a see-through square at (8..23, 8..23).
u8 indexPixel(int x, int y) {
    if (x >= 8 && x < 24 && y >= 8 && y < 24) return 0;
    return static_cast<u8>(1 + ((x >> 2) & 7) + ((y >> 2) & 7) * 8);
}
u16 paletteColor(int index) { return static_cast<u16>(((index & 7) * 4) | ((((index >> 3) & 7) * 4) << 5) | (20 << 10)); }

// One buffer for the picture of the test that runs: prepare() writes it for a mode.
alignas(4) AGB_EWRAM_BSS u16 g_pixels[bitmap::kWidth * bitmap::kHeight];
AGB_EWRAM_BSS u16 g_palette[64];
data::Picture g_pic3, g_pic4, g_pic5;

// 16 x 16 sprite in colour 1 (white).
AGB_EWRAM_BSS u32 g_spriteTiles[4 * 8];
const u16 kSprColors[16] = {0, 0x7FFF};
const data::Palette kSprPal{kSprColors, 1};
const u8 kFrame0[] = {0}, kTicks[] = {1};
const data::AnimDef kSprAnim[] = {{"idle", kFrame0, kTicks, 1, true, false}};
data::SpriteSheet g_sheet;
bool g_made = false;

void makeContent() {
    if (g_made) return;
    g_made = true;
    for (int i = 0; i < 64; ++i) g_palette[i] = paletteColor(i + 1);
    g_pic3 = {g_pixels, nullptr, nullptr, 0, bitmap::kWidth, bitmap::kHeight};
    g_pic4 = {nullptr, reinterpret_cast<const u8*>(g_pixels), g_palette, 64, bitmap::kWidth, bitmap::kHeight};
    g_pic5 = {g_pixels, nullptr, nullptr, 0, bitmap::kMode5Width, bitmap::kMode5Height};
    for (auto& w : g_spriteTiles) w = 0x11111111u;
    g_sheet = {};
    g_sheet.gfx = {g_spriteTiles, sizeof g_spriteTiles, 4, 4, false};
    g_sheet.palette = &kSprPal;
    g_sheet.width = g_sheet.height = 16;
    g_sheet.shape = data::ObjShape::Square;
    g_sheet.sizeCode = 1;
    g_sheet.tilesPerFrame = 4;
    g_sheet.frameCount = 1;
    g_sheet.anims = kSprAnim;
    g_sheet.animCount = 1;
}

void resetVideo() {
    makeContent();
    screenArena().reset();
    effects::reset();
    sprites::reset();
    palettes::reset();
    bg::reset();
}

void prepare(int mode) {
    if (mode == 4) {
        u8* d = reinterpret_cast<u8*>(g_pixels);
        for (int y = 0; y < bitmap::kHeight; ++y)
            for (int x = 0; x < bitmap::kWidth; ++x) d[y * bitmap::kWidth + x] = indexPixel(x, y);
        return;
    }
    const int w = mode == 5 ? bitmap::kMode5Width : bitmap::kWidth, h = mode == 5 ? bitmap::kMode5Height : bitmap::kHeight;
    for (int y = 0; y < h; ++y)
        for (int x = 0; x < w; ++x) g_pixels[y * w + x] = bgrPixel(x, y);
}

void shot(const char* name) {
    palettes::update();
    sprites::build();
    system::waitVBlank();
    system::waitVBlank();
    debug::logf("shot %s", name);
    for (int i = 0; i < 4; ++i) {
        sprites::build();
        system::waitVBlank();
    }
}

u8 byteAt(int page, int x, int y) { return reinterpret_cast<const u8*>(bitmap::pageBuffer(page))[y * bitmap::kWidth + x]; }

void settle() {
    for (int i = 0; i < 8 && commit::queuedBytes(); ++i) system::waitVBlank();
}

// A sprite at (100, 60) in the current mode; its tiles must stay above the frame buffers.
sprites::Id placeSprite() {
    const sprites::Id s = sprites::create(&g_sheet, 0);
    CHECK(sprites::valid(s));
    sprites::setPosition(s, 100, 60);
    sprites::build();
    CHECK_EQ(commit::oam()[0].attr2 & 0x3FF, 512);
    return s;
}
}  // namespace

AGB_TEST(bg_takes_modes_up_to_5) {
    bg::reset();
    bg::setVideoMode(4);
    CHECK_EQ(bg::videoMode(), 4);
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 4);
    bg::setVideoMode(5);
    CHECK_EQ(bg::videoMode(), 5);
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 5);
    bg::setVideoMode(9);
    CHECK_EQ(bg::videoMode(), 5);
    bg::setVideoMode(0);
    CHECK_EQ(bg::videoMode(), 0);
}

AGB_TEST(mode3_picture_pixels_rectangle_and_sprite) {
    resetVideo();
    prepare(3);
    palettes::setBackdrop(kBackdrop);
    bitmap::begin(3);
    CHECK_EQ(bitmap::width(), 240);
    CHECK_EQ(bitmap::height(), 160);
    CHECK_EQ(bitmap::pageCount(), 1);
    CHECK(bitmap::showNow(g_pic3, 0));
    CHECK(!bitmap::showNow(g_pic4, 0));  // no BGR555 data
    CHECK(!bitmap::showNow(g_pic5, 0));  // Mode 5's size
    const u16* fb = bitmap::pageBuffer(0);
    CHECK_EQ(fb[159 * 240 + 239], bgrPixel(239, 159));
    CHECK(bitmap::fill(10, 20, 30, 5, 0x001F));
    CHECK(bitmap::setPixel(50, 50, 0x03E0));
    CHECK(bitmap::setPixel(51, 50, 0x7C00));
    CHECK(bitmap::setPixel(240, 0, 0x7FFF));  // clipped: nothing to do
    // Queued: nothing changes before VBlank.
    CHECK_EQ(fb[50 * 240 + 50], bgrPixel(50, 50));
    system::waitVBlank();
    CHECK_EQ(fb[50 * 240 + 50], 0x03E0);
    CHECK_EQ(fb[50 * 240 + 51], 0x7C00);
    CHECK_EQ(fb[20 * 240 + 10], 0x001F);
    CHECK_EQ(fb[20 * 240 + 40], bgrPixel(40, 20));
    // Flip means nothing with one page.
    CHECK(bitmap::flip());
    system::waitVBlank();
    CHECK_EQ(commit::reg(commit::DispCnt) & DCNT_PAGE, 0);
    const sprites::Id s = placeSprite();
    shot("bitmap3");
    sprites::destroy(s);
}

AGB_TEST(mode4_neighbour_pixels_keep_each_other) {
    resetVideo();
    palettes::setBackdrop(kBackdrop);
    bitmap::begin(4);
    CHECK_EQ(bitmap::pageCount(), 2);
    CHECK_EQ(reinterpret_cast<u32>(bitmap::pageBuffer(0)), 0x06000000u);
    CHECK_EQ(reinterpret_cast<u32>(bitmap::pageBuffer(1)), 0x0600A000u);
    CHECK_EQ(bitmap::drawPage(), 1);
    bitmap::clearNow(0, 0);
    bitmap::clearNow(0, 1);
    // Both pixels of one halfword in one frame: a read-modify-write made at the call would lose the first.
    CHECK(bitmap::setPixel(20, 30, 7));
    CHECK(bitmap::setPixel(21, 30, 9));
    // An even and an odd pixel apart, and a third write over the first.
    CHECK(bitmap::setPixel(40, 30, 3));
    CHECK(bitmap::setPixel(40, 30, 4));
    system::waitVBlank();
    CHECK_EQ(byteAt(1, 20, 30), 7);
    CHECK_EQ(byteAt(1, 21, 30), 9);
    CHECK_EQ(byteAt(1, 40, 30), 4);
    CHECK_EQ(byteAt(1, 41, 30), 0);
    CHECK_EQ(byteAt(1, 19, 30), 0);
    CHECK_EQ(byteAt(1, 22, 30), 0);
    // The other page was not touched.
    CHECK_EQ(byteAt(0, 20, 30), 0);
}

AGB_TEST(mode4_rectangle_edges_at_every_alignment) {
    resetVideo();
    palettes::setBackdrop(kBackdrop);
    bitmap::begin(4);
    bitmap::clearNow(0, 1);
    struct Case { int x, w; };
    const Case cases[] = {{10, 4}, {11, 4}, {10, 5}, {11, 5}, {11, 1}, {12, 1}, {11, 2}};
    int y = 2;
    u8 ink = 1;
    for (const Case& c : cases) {
        CHECK(bitmap::fill(c.x, y, c.w, 3, ink));
        system::waitVBlank();
        for (int row = y - 1; row < y + 4; ++row)
            for (int x = c.x - 2; x < c.x + c.w + 2; ++x) {
                const bool inside = row >= y && row < y + 3 && x >= c.x && x < c.x + c.w;
                CHECK_EQ(byteAt(1, x, row), inside ? ink : 0);
            }
        y += 6;
        ++ink;
    }
    // Clipped at the right and bottom edges, keeping the odd column.
    bitmap::clearNow(0, 1);
    CHECK(bitmap::fill(235, 155, 50, 50, 5));
    system::waitVBlank();
    CHECK_EQ(byteAt(1, 239, 159), 5);
    CHECK_EQ(byteAt(1, 235, 155), 5);
    CHECK_EQ(byteAt(1, 234, 155), 0);
    CHECK_EQ(byteAt(1, 235, 154), 0);
}

AGB_TEST(mode4_picture_page_flip_palette_and_sprite) {
    resetVideo();
    prepare(4);
    palettes::setBackdrop(kBackdrop);
    bitmap::begin(4);
    CHECK(bitmap::showNow(g_pic4, 0));
    CHECK(bitmap::showNow(g_pic4, 1));
    CHECK(!bitmap::showNow(g_pic3, 0));  // no indices
    CHECK_EQ(palettes::baseColor(1), paletteColor(1));
    CHECK_EQ(palettes::baseColor(64), paletteColor(64));
    CHECK_EQ(palettes::baseColor(0), kBackdrop);  // index 0 stays the backdrop
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 4);
    CHECK_EQ(commit::reg(commit::DispCnt) & 0x0F00, DCNT_BG2);
    // Text, UI and tile palettes cannot take colours out of the picture in this mode.
    const u16 bank[16] = {};
    palettes::setBank(false, 0, bank);
    CHECK_EQ(palettes::baseColor(1), paletteColor(1));
    const sprites::Id s = placeSprite();
    shot("bitmap4");
    // Draw on the hidden page: nothing shows until the flip.
    bitmap::setPaletteColor(200, 0x03FF);
    CHECK_EQ(bitmap::drawPage(), 1);
    CHECK(bitmap::fill(60, 40, 31, 21, 200));
    CHECK(bitmap::setPixel(120, 100, 200));
    shot("bitmap4_hidden");
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, 0);
    CHECK_EQ(byteAt(1, 60, 40), 200);
    CHECK(bitmap::flip());
    CHECK_EQ(bitmap::drawPage(), 0);
    CHECK_EQ(bitmap::shownPage(), 1);
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, 0);  // in VBlank, through the queue
    system::waitVBlank();
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, DCNT_PAGE);
    CHECK_EQ(commit::reg(commit::DispCnt) & DCNT_PAGE, DCNT_PAGE);
    // A register change elsewhere keeps the page; a reset goes back to page 0.
    bg::setObjVisible(true);
    bg::setVisible(2, true);
    CHECK_EQ(commit::reg(commit::DispCnt) & DCNT_PAGE, DCNT_PAGE);
    shot("bitmap4_flipped");
    sprites::destroy(s);
    resetVideo();
    CHECK_EQ(commit::reg(commit::DispCnt) & DCNT_PAGE, 0);
}

AGB_TEST(queued_flip_waits_for_the_writes_before_it) {
    resetVideo();
    bitmap::begin(4);
    bitmap::clearNow(1, 0);
    bitmap::clearNow(1, 1);
    system::waitVBlank();
    commit::setDmaBudget(4096);
    // A full clear takes ten VBlanks at this budget; the flip after it must not show the half-cleared page.
    CHECK(bitmap::clear(2));
    CHECK(bitmap::flip());
    system::waitVBlank();
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, 0);
    CHECK_EQ(byteAt(1, 0, 0), 2);
    CHECK_EQ(byteAt(1, 0, 159), 1);
    for (int i = 0; i < 16 && commit::queuedBytes(); ++i) system::waitVBlank();
    CHECK_EQ(commit::queuedBytes(), 0u);
    CHECK_EQ(byteAt(1, 239, 159), 2);
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, DCNT_PAGE);
    commit::setDmaBudget(commit::kDefaultDmaBudget);
    // A new scene's begin forgets what the old one left in the queue.
    CHECK(bitmap::clear(3));
    bitmap::begin(4);
    CHECK_EQ(commit::queuedBytes(), 0u);
    system::waitVBlank();
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, 0);
    CHECK_EQ(byteAt(1, 0, 0), 2);
    resetVideo();
}

AGB_TEST(pixel_queue_keeps_order_and_reports_when_full) {
    resetVideo();
    bitmap::begin(4);
    bitmap::clearNow(0, 1);
    // Fill, pixel, fill, pixel: the queue applies them in the order they were asked for.
    CHECK(bitmap::fill(0, 0, 8, 1, 1));
    CHECK(bitmap::setPixel(2, 0, 2));
    CHECK(bitmap::fill(4, 0, 2, 1, 3));
    CHECK(bitmap::setPixel(5, 0, 4));
    system::waitVBlank();
    CHECK_EQ(byteAt(1, 2, 0), 2);
    CHECK_EQ(byteAt(1, 4, 0), 3);
    CHECK_EQ(byteAt(1, 5, 0), 4);
    CHECK_EQ(byteAt(1, 7, 0), 1);
    // Up to the queue's size in a frame; the next one is refused, and the queue works again after VBlank.
    bool ok = true;
    for (int i = 0; i < commit::kPixelQueueSize; ++i) ok = bitmap::setPixel(i % 240, 10 + i / 240, 9) && ok;
    CHECK(ok);
    CHECK(!bitmap::setPixel(0, 20, 9));
    settle();
    CHECK_EQ(byteAt(1, 239, 10), 9);
    CHECK_EQ(byteAt(1, (commit::kPixelQueueSize - 1) % 240, 10 + (commit::kPixelQueueSize - 1) / 240), 9);
    CHECK(bitmap::setPixel(0, 20, 9));
    system::waitVBlank();
    CHECK_EQ(byteAt(1, 0, 20), 9);
    resetVideo();
}

AGB_TEST(mode5_smaller_than_the_screen_two_pages_and_sprite) {
    resetVideo();
    prepare(5);
    palettes::setBackdrop(kBackdrop);
    bitmap::begin(5);
    CHECK_EQ(bitmap::width(), 160);
    CHECK_EQ(bitmap::height(), 128);
    CHECK_EQ(bitmap::pageCount(), 2);
    CHECK_EQ(commit::reg(commit::DispCnt) & 7, 5);
    CHECK(bitmap::showNow(g_pic5, 0));
    CHECK(bitmap::showNow(g_pic5, 1));
    CHECK(!bitmap::showNow(g_pic3, 0));  // 240 x 160 does not fit
    const u16* back = bitmap::pageBuffer(1);
    CHECK_EQ(back[127 * 160 + 159], bgrPixel(159, 127));
    // The rectangle stops at the page's edges and leaves the next row's start alone.
    CHECK(bitmap::fill(150, 120, 100, 100, 0x001F));
    CHECK(bitmap::setPixel(160, 0, 0x7FFF));   // right of the page: clipped, not wrapped to the next row
    CHECK(bitmap::setPixel(0, 128, 0x7FFF));   // under the page: clipped, not into the sprite area
    system::waitVBlank();
    CHECK_EQ(back[127 * 160 + 159], 0x001F);
    CHECK_EQ(back[120 * 160 + 150], 0x001F);
    CHECK_EQ(back[121 * 160 + 0], bgrPixel(0, 121));
    CHECK_EQ(back[1 * 160 + 0], bgrPixel(0, 1));
    const sprites::Id s = placeSprite();
    shot("bitmap5");
    CHECK(bitmap::flip());
    system::waitVBlank();
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, DCNT_PAGE);
    shot("bitmap5_flipped");
    sprites::destroy(s);
    resetVideo();
}

// In ROM, like a game's pictures.
alignas(4) const u16 kRomPicture[bitmap::kMode5Width * bitmap::kMode5Height] = {};

AGB_TEST(a_scene_budget_of_drawing_ends_inside_vblank) {
    // At a bitmap scene's budget, every frame of a full clear or of a picture from ROM is finished before the screen
    // draws again: a page flip after them shows no part of the page before it.
    resetVideo();
    commit::setDmaBudget(commit::kBitmapDmaBudget);
    const data::Picture rom5 = {kRomPicture, nullptr, nullptr, 0, bitmap::kMode5Width, bitmap::kMode5Height};
    const data::Picture rom4 = {nullptr, reinterpret_cast<const u8*>(kRomPicture), g_palette, 64, bitmap::kWidth, bitmap::kHeight};
    bitmap::begin(5);
    CHECK(bitmap::clear(0x1234));
    system::waitVBlank();
    CHECK(REG_VCOUNT >= 160);
    CHECK_EQ(commit::queuedBytes(), 0u);  // a whole Mode 5 page in one VBlank
    CHECK(bitmap::queuePicture(rom5));
    for (int i = 0; i < 8 && commit::queuedBytes(); ++i) {
        system::waitVBlank();
        CHECK(REG_VCOUNT >= 160);
    }
    CHECK_EQ(commit::queuedBytes(), 0u);
    bitmap::begin(4);
    CHECK(bitmap::clear(7));
    system::waitVBlank();
    CHECK(REG_VCOUNT >= 160);
    CHECK_EQ(commit::queuedBytes(), 0u);  // a whole Mode 4 page in one VBlank
    CHECK(bitmap::queuePicture(rom4));
    CHECK(bitmap::flip());
    for (int i = 0; i < 8 && commit::queuedBytes(); ++i) {
        system::waitVBlank();
        CHECK(REG_VCOUNT >= 160);
    }
    CHECK_EQ(commit::queuedBytes(), 0u);
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, DCNT_PAGE);
    commit::setDmaBudget(commit::kDefaultDmaBudget);
    resetVideo();
}

AGB_TEST(queued_picture_and_tile_modes_are_safe) {
    resetVideo();
    prepare(4);
    bitmap::begin(4);
    CHECK(bitmap::queuePicture(g_pic4));
    settle();
    CHECK_EQ(byteAt(1, 100, 100), indexPixel(100, 100));
    CHECK_EQ(byteAt(1, 8, 8), 0);
    CHECK(!bitmap::queuePicture(g_pic3));
    resetVideo();
    prepare(5);
    bitmap::begin(5);
    CHECK(bitmap::queuePicture(g_pic5));
    settle();
    CHECK_EQ(bitmap::pageBuffer(1)[10 * 160 + 10], bgrPixel(10, 10));
    resetVideo();
    // Drawing in a tile mode changes nothing: the frame buffers would land on tiles and maps.
    bg::setVideoMode(0);
    const u16 before = commit::bgTiles(0)[3];
    CHECK(bitmap::setPixel(3, 0, 0x7FFF));
    CHECK(bitmap::fill(0, 0, 8, 8, 0x7FFF));
    CHECK(bitmap::flip());
    system::waitVBlank();
    CHECK_EQ(commit::bgTiles(0)[3], before);
    CHECK_EQ(REG_DISPCNT & DCNT_PAGE, 0);
}

int main() {
    system::init();
    // These tests load pictures directly, without a scene's staged load: the whole run counts as one (P2.04).
    commit::beginStagedLoad();
    test::runAll();
}
