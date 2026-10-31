// Sprite manager: 128 sprites, OAM contents, priority order, animation timing, shared frame cache,
// all 12 OAM shapes and affine matrices.
#include <tonc.h>

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/render/palette.hpp"
#include "agb/render/sprite.hpp"

using namespace agb;
using namespace agb::render;

namespace {
AGB_EWRAM_BSS u32 g_frames[4 * 4 * 8];  // 4 frames of 16x16 (4 tiles each)
const u16 kColors[16] = {0, 0x7FFF, 0x001F};
const data::Palette kPal{kColors, 1};
const u8 kRunFrames[] = {0, 1, 2, 3};
const u8 kRunTicks[] = {6, 6, 6, 6};
const u8 kJumpFrames[] = {1, 2};
const u8 kJumpTicks[] = {3, 3};
const data::AnimDef kAnims[] = {
    {"run", kRunFrames, kRunTicks, 4, true, false},
    {"jump", kJumpFrames, kJumpTicks, 2, false, false},
    {"bob", kRunFrames, kRunTicks, 3, true, true},
};
data::SpriteSheet g_sheet;

void setup() {
    for (int f = 0; f < 4; ++f)
        for (int i = 0; i < 32; ++i) g_frames[f * 32 + i] = static_cast<u32>(f + 1) * 0x11111111u;
    g_sheet = {};
    g_sheet.gfx = {g_frames, sizeof g_frames, 16, 4, false};
    g_sheet.palette = &kPal;
    g_sheet.width = g_sheet.height = 16;
    g_sheet.shape = data::ObjShape::Square;
    g_sheet.sizeCode = 1;
    g_sheet.tilesPerFrame = 4;
    g_sheet.frameCount = 4;
    g_sheet.anims = kAnims;
    g_sheet.animCount = 3;
    g_sheet.originX = 8;
    g_sheet.originY = 16;
}
}  // namespace

AGB_TEST(hundred_twenty_eight_sprites_share_frames) {
    palettes::reset();
    sprites::reset();
    setup();
    sprites::Id ids[128];
    for (int i = 0; i < 128; ++i) {
        ids[i] = sprites::create(&g_sheet, static_cast<u8>(i % 4));
        CHECK(ids[i] != sprites::kNone);
        sprites::setPosition(ids[i], 8 + (i % 16) * 14, 16 + (i / 16) * 18);
        sprites::setFrame(ids[i], static_cast<u16>(i % 2));
    }
    CHECK_EQ(sprites::create(&g_sheet), sprites::kNone);  // 129th is refused and reported
    const int drawn = sprites::build();
    CHECK_EQ(drawn, 128);
    CHECK_EQ(sprites::vramTilesUsed(), 8);  // two distinct frames x 4 tiles
    CHECK_EQ(static_cast<int>(sprites::frameCopiesThisFrame()), 2);
    // Sorted by priority: the first OAM entries carry priority 0.
    const commit::ObjAttr* oam = commit::oam();
    CHECK_EQ((oam[0].attr2 >> 10) & 3, 0);
    CHECK_EQ((oam[127].attr2 >> 10) & 3, 3);
    system::waitVBlank();
    // OAM in hardware matches the shadow.
    CHECK_EQ(reinterpret_cast<volatile u16*>(MEM_OAM)[0], oam[0].attr0);
    // Frame data reached OBJ VRAM.
    const u32* objv = reinterpret_cast<const u32*>(MEM_VRAM_OBJ);
    CHECK(objv[0] == 0x11111111u || objv[0] == 0x22222222u);
    // Palette loaded into an OBJ bank.
    CHECK_EQ(palettes::banksUsed(true), 1);
    for (auto id : ids) sprites::destroy(id);
    CHECK_EQ(sprites::count(), 0);
}

AGB_TEST(animation_timing_loop_once_pingpong) {
    palettes::reset();
    sprites::reset();
    setup();
    const auto s = sprites::create(&g_sheet);
    CHECK_EQ(sprites::playByName(s, "run"), 0);
    CHECK_EQ(sprites::frame(s), 0);
    for (int i = 0; i < 6; ++i) sprites::update();
    CHECK_EQ(sprites::frame(s), 1);
    for (int i = 0; i < 18; ++i) sprites::update();
    CHECK_EQ(sprites::frame(s), 0);  // looped after 24 ticks
    sprites::setSpeed(s, fixed(2));
    for (int i = 0; i < 3; ++i) sprites::update();
    CHECK_EQ(sprites::frame(s), 1);  // double speed: 6 ticks in 3 frames
    sprites::setSpeed(s, fixed(1));
    sprites::play(s, 1);
    for (int i = 0; i < 20; ++i) sprites::update();
    CHECK(sprites::animDone(s));
    CHECK_EQ(sprites::frame(s), 2);  // stays on the last frame
    sprites::play(s, 2);
    u16 seq[8];
    for (int i = 0; i < 8; ++i) {
        for (int k = 0; k < 6; ++k) sprites::update();
        seq[i] = sprites::frame(s);
    }
    // ping-pong over frames 0,1,2: 1 2 1 0 1 2 1 0
    const u16 want[8] = {1, 2, 1, 0, 1, 2, 1, 0};
    for (int i = 0; i < 8; ++i) CHECK_EQ(seq[i], want[i]);
}

AGB_TEST(oam_shapes_flips_and_affine) {
    palettes::reset();
    sprites::reset();
    setup();
    const u8 shapes[3] = {0, 1, 2};
    for (u8 shape : shapes)
        for (u8 size = 0; size < 4; ++size) {
            g_sheet.shape = static_cast<data::ObjShape>(shape);
            g_sheet.sizeCode = size;
            const auto s = sprites::create(&g_sheet);
            sprites::setPosition(s, 100, 100);
            sprites::build();
            const commit::ObjAttr* oam = commit::oam();
            CHECK_EQ(oam[0].attr0 >> 14, shape);
            CHECK_EQ(oam[0].attr1 >> 14, size);
            sprites::destroy(s);
        }
    g_sheet.shape = data::ObjShape::Square;
    g_sheet.sizeCode = 1;
    const auto s = sprites::create(&g_sheet);
    sprites::setPosition(s, 50, 60);
    sprites::setFlip(s, true, false);
    sprites::build();
    const commit::ObjAttr* oam = commit::oam();
    CHECK(oam[0].attr1 & ATTR1_HFLIP);
    CHECK_EQ(oam[0].attr1 & 0x1FF, 42);  // 50 - originX 8
    CHECK_EQ(oam[0].attr0 & 0xFF, 44);   // 60 - originY 16
    sprites::setAffine(s, 64, fixed(2), fixed(2), true);
    sprites::build();
    CHECK((oam[0].attr0 & ATTR0_AFF_DBL) == ATTR0_AFF_DBL);
    CHECK_EQ(sprites::affineUsed(), 1);
    // 90 degrees at scale 2: pa = cos/2 = 0, pb = -sin/2 = -128
    CHECK_EQ(oam[0].fill, 0);
    CHECK_EQ(oam[1].fill, -128);
    sprites::clearAffine(s);
    CHECK_EQ(sprites::affineUsed(), 0);
    // Off-screen sprites are not drawn.
    sprites::setPosition(s, 400, 60);
    CHECK_EQ(sprites::build(), 0);
}

// True when the debug log got `needle` after total log position `head`.
bool loggedSince(u32 head, const char* needle) {
    const debug::Channel& ch = debug::channel();
    const u32 end = ch.logHead;
    if (end - head > ch.logSize) head = end - ch.logSize;
    for (u32 start = head; start < end; ++start) {
        u32 i = 0;
        while (needle[i] && start + i < end && ch.log[(start + i) % ch.logSize] == needle[i]) ++i;
        if (!needle[i]) return true;
    }
    return false;
}

AGB_TEST(overloaded_screen_line_is_reported) {
    palettes::reset();
    sprites::reset();
    setup();
    // 80 sprites of 16 pixels on the same lines need 1280 cycles; the hardware has 1210 per line.
    sprites::Id ids[80];
    for (int i = 0; i < 80; ++i) {
        ids[i] = sprites::create(&g_sheet, 0);
        sprites::setPosition(ids[i], (i % 15) * 16, 40);
    }
    const u32 head = debug::channel().logHead;
    for (int f = 0; f < 34; ++f) {
        sprites::build();
        system::waitVBlank();
    }
    CHECK(loggedSince(head, "sprite cycles"));
    // 60 sprites (960 cycles) fit: nothing more is reported.
    for (int i = 60; i < 80; ++i) sprites::destroy(ids[i]);
    const u32 head2 = debug::channel().logHead;
    for (int f = 0; f < 34; ++f) {
        sprites::build();
        system::waitVBlank();
    }
    CHECK(!loggedSince(head2, "sprite cycles"));
    for (int i = 0; i < 60; ++i) sprites::destroy(ids[i]);
}

AGB_TEST(thirty_third_affine_matrix_is_refused) {
    // OAM holds 32 affine matrices: the 33rd rotated sprite is refused (it stays a regular sprite) and reported.
    palettes::reset();
    sprites::reset();
    setup();
    sprites::Id ids[33];
    for (int i = 0; i < 33; ++i) {
        ids[i] = sprites::create(&g_sheet, 1);
        sprites::setPosition(ids[i], 20 + (i % 11) * 20, 30 + (i / 11) * 30);
    }
    const u32 head = debug::channel().logHead;
    for (int i = 0; i < 32; ++i) sprites::setAffine(ids[i], static_cast<u8>(i * 8), fixed(1), fixed(1));
    CHECK_EQ(sprites::affineUsed(), 32);
    CHECK(!loggedSince(head, "rotated or scaled"));
    sprites::setAffine(ids[32], 16, fixed(2), fixed(2));
    CHECK_EQ(sprites::affineUsed(), 32);
    CHECK(loggedSince(head, "32 sprites are already rotated or scaled"));
    CHECK_EQ(sprites::build(), 33);
    const commit::ObjAttr* oam = commit::oam();
    int regular = 0;
    u32 matrices = 0;
    for (int i = 0; i < 33; ++i) {
        if ((oam[i].attr0 & 0x0300) == ATTR0_AFF) matrices |= 1u << ((oam[i].attr1 >> 9) & 31);
        else if ((oam[i].attr0 & 0x0300) == 0) ++regular;
    }
    CHECK_EQ(matrices, 0xFFFFFFFFu);  // every matrix in use, each by one sprite
    CHECK_EQ(regular, 1);
    // A freed matrix goes to the next sprite that asks.
    sprites::clearAffine(ids[5]);
    CHECK_EQ(sprites::affineUsed(), 31);
    sprites::setAffine(ids[32], 16, fixed(2), fixed(2));
    CHECK_EQ(sprites::affineUsed(), 32);
    for (auto id : ids) sprites::destroy(id);
    CHECK_EQ(sprites::affineUsed(), 0);
}

int main() {
    system::init();
    test::runAll();
}
