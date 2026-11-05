// Text windows: glyph placement (1bpp and 2bpp, shadow), typewriter reveal with blips, continue marker.
#include "agb/hw.hpp"

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/memory.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/text.hpp"

using namespace agb;
using namespace agb::render;

namespace {
// Glyph 0: 3x3 2bpp block with shades 3,2,1 per row. Glyph 1: 2x2 1bpp full.
const u8 kBitmaps[] = {
    0b11111100,  // row 0: 3,3,3 (2bpp, 6 bits used)
    0b10101000,  // row 1: 2,2,2
    0b01010100,  // row 2: 1,1,1
};
const data::Glyph kGlyphs2[] = {{0, 3, 3, 0, 0, 4}};
const data::Font kFont2{"t2", kGlyphs2, kBitmaps, 1, 2, 10, 8, false, nullptr};
const u8 kBitmaps1[] = {0b11000000, 0b11000000};
const data::Glyph kGlyphs1[] = {{0, 2, 2, 0, 0, 3}};
const data::Font kFont1{"t1", kGlyphs1, kBitmaps1, 1, 1, 10, 8, true, nullptr};
const data::PlacedGlyph kPlaced[] = {
    {0, 0, 0, 0, 0}, {0, 4, 0, 0, 1}, {0, 8, 0, 0, 2}, {0, 0, 0, 1, 3}, {0, 4, -1, 1, 3},  // last one stacks on step 3
};
const data::TextRun kRun{&kFont2, kPlaced, 5, 2, 4};
int g_blips = 0;
void blip() { ++g_blips; }
}  // namespace

AGB_TEST(glyph_pixels_2bpp_and_shadow) {
    screenArena().reset();
    bg::reset();
    bg::Config c;
    c.screenBlock = 28;
    bg::configure(2, c);
    TextBox box;
    CHECK(box.init(2, 1, 15, 20, 4, 15, TextBox::Style{1, 4, 0}));
    box.drawGlyph(kFont2, 0, 10, 5);
    CHECK_EQ(box.pixel(10, 5), 1);   // shade 3 -> ink
    CHECK_EQ(box.pixel(11, 6), 2);   // shade 2 -> ink + 1
    CHECK_EQ(box.pixel(12, 7), 3);   // shade 1 -> ink + 2
    CHECK_EQ(box.pixel(13, 5), 0);
    box.clear();
    box.drawGlyph(kFont1, 0, 0, 0);
    CHECK_EQ(box.pixel(0, 0), 1);
    CHECK_EQ(box.pixel(2, 2), 4);  // shadow one pixel down-right
    CHECK_EQ(box.pixel(1, 1), 1);  // glyph drawn over its own shadow
    box.update();
    system::waitVBlank();
    // Tiles reached VRAM and the map points at them with the text palette bank.
    const u16* map = commit::bgMap(28);
    CHECK_EQ(map[15 * 32 + 1] >> 12, 15);
}

AGB_TEST(typewriter_reveals_by_step) {
    screenArena().reset();
    bg::reset();
    bg::Config c;
    c.screenBlock = 28;
    bg::configure(2, c);
    TextBox box;
    CHECK(box.init(2, 0, 0, 10, 3, 15, TextBox::Style{}));
    box.setBlip(blip);
    g_blips = 0;
    box.show(&kRun, 1);
    box.update();  // step 0 -> 1: glyph 0
    CHECK_EQ(box.pixel(0, 0), 1);
    CHECK_EQ(box.pixel(4, 0), 0);
    box.update();
    CHECK_EQ(box.pixel(4, 0), 1);
    CHECK(!box.done());
    box.update();
    box.update();  // step 3: both glyphs of line 1 (stacked mark at dy -1)
    CHECK(box.done());
    CHECK_EQ(box.pixel(0, 10), 1);
    CHECK_EQ(box.pixel(4, 9), 1);
    CHECK_EQ(g_blips, 4);
    box.showContinueMarker(true);
    CHECK_EQ(box.pixel(10 * 8 - 9 + 2, 3 * 8 - 6 + 2), 1);
    box.show(&kRun, 0);  // instant
    CHECK(box.done());
    box.show(&kRun, 1, 1);  // scrolled to line 1: line 1 drawn at the top
    box.skip();
    CHECK_EQ(box.pixel(0, 0), 1);
    CHECK_EQ(box.pixel(8, 0), 0);
}

int main() {
    system::init();
    test::runAll();
}
