// Palette effects (fade, tint, cycle, flash), blending, mosaic, transitions, HBlank scanline DMA,
// and IWRAM code placement.
#include <tonc.h>

#include "../test.hpp"
#include "agb/commit.hpp"
#include "agb/render/effects.hpp"
#include "agb/render/palette.hpp"

using namespace agb;
using namespace agb::render;

namespace {
const u16 kColors[16] = {0, 31, 31 << 5, 31 << 10, 0x7FFF, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11};
const data::Palette kPal{kColors, 1};
}  // namespace

int hotArmAdd(int a, int b);  // hot.iwram.cpp: ARM code in IWRAM
AGB_IWRAM_CODE int hotThumbAdd(int a, int b);

AGB_TEST(fade_tint_flash) {
    palettes::reset();
    const int bank = palettes::acquire(&kPal, false);
    CHECK_EQ(bank, 0);
    palettes::update();
    system::waitVBlank();
    CHECK_EQ(pal_bg_mem[4], 0x7FFF);
    palettes::startFade(palettes::kAll, 0, 16, 8);
    for (int i = 0; i < 4; ++i) palettes::update();
    // Half way: white blended halfway to black = 15 or 16 per channel.
    const u16 mid = commit::palette()[4];
    CHECK((mid & 31) >= 15 && (mid & 31) <= 16);
    for (int i = 0; i < 4; ++i) palettes::update();
    CHECK(!palettes::fading());
    CHECK_EQ(commit::palette()[4], 0);
    palettes::startFade(palettes::kAll, 0, 0, 2);
    palettes::update();
    palettes::update();
    CHECK_EQ(commit::palette()[4], 0x7FFF);
    palettes::setTint(palettes::kAllBg, 0x001F, 16);
    palettes::update();
    CHECK_EQ(commit::palette()[3], 0x001F);
    palettes::setTint(0, 0, 0);
    palettes::flash(palettes::kAllBg, 0x03E0, 2);
    palettes::update();
    CHECK_EQ(commit::palette()[1], 0x03E0);
    CHECK_EQ(commit::palette()[0], 0);  // index 0 (transparent / backdrop) is never flashed
    palettes::update();
    palettes::update();
    CHECK_EQ(commit::palette()[1], 31);
    // Changed-this-frame bits for the Debugger.
    palettes::setBaseColor(7, 0x1234);
    palettes::update();
    CHECK(palettes::changedThisFrame()[0] & (1u << 7));
}

AGB_TEST(colour_cycle_directions) {
    palettes::reset();
    palettes::acquire(&kPal, false);
    const int slot = palettes::startCycle(false, 0, 6, 8, 2, palettes::Direction::Left);
    CHECK(slot >= 0);
    palettes::update();
    palettes::update();  // one step after 2 ticks
    CHECK_EQ(commit::palette()[6], 3);  // index 6 now shows colour from index 7
    CHECK_EQ(commit::palette()[8], 2);  // wraps to the first colour of the range
    palettes::stopCycle(slot);
    const int right = palettes::startCycle(false, 0, 6, 8, 1, palettes::Direction::Right);
    palettes::update();
    CHECK_EQ(commit::palette()[6], 4);  // index 6 shows colour from index 8
    palettes::stopCycle(right);
    palettes::update();
    CHECK_EQ(commit::palette()[6], 2);
}

AGB_TEST(blend_mosaic_windows_registers) {
    effects::reset();
    effects::setAlphaBlend(effects::kBg1, effects::kBg2 | effects::kBackdrop, 10, 6);
    CHECK_EQ(commit::reg(commit::BldCnt), static_cast<u16>(effects::kBg1 | ((effects::kBg2 | effects::kBackdrop) << 8) | BLD_STD));
    CHECK_EQ(commit::reg(commit::BldAlpha), 10 | (6 << 8));
    effects::setBrightness(effects::kBg0, -8);
    CHECK_EQ(commit::reg(commit::BldY), 8);
    CHECK_EQ(commit::reg(commit::BldCnt) & BLD_BLACK, BLD_BLACK);
    effects::setMosaic(4, 2);
    CHECK_EQ(commit::reg(commit::Mosaic), 0x1133);
    effects::setWindow0(10, 20, 100, 120, 0x1F, 0x01);
    CHECK_EQ(commit::reg(commit::Win0H), (10 << 8) | 100);
    CHECK(commit::reg(commit::DispCnt) & DCNT_WIN0);
    effects::disableWindows();
    CHECK(!(commit::reg(commit::DispCnt) & DCNT_WIN0));
    effects::reset();
}

AGB_TEST(transitions_fade_and_slide) {
    effects::reset();
    effects::beginOut(effects::Transition::Fade, 8);
    int frames = 0;
    while (!effects::transitionDone()) {
        effects::update();
        ++frames;
    }
    CHECK_EQ(frames, 8);
    CHECK_EQ(effects::transitionLevel(), 16);
    CHECK_EQ(commit::reg(commit::BldY), 16);
    effects::beginIn(effects::Transition::Fade, 4);
    while (!effects::transitionDone()) effects::update();
    CHECK_EQ(effects::transitionLevel(), 0);
    CHECK_EQ(commit::reg(commit::BldCnt), 0);
    effects::beginOut(effects::Transition::Slide, 16);
    for (int i = 0; i < 8; ++i) effects::update();
    CHECK_EQ(commit::reg(commit::Win0H) & 0xFF, 240 - 8 * 15);
    effects::beginOut(effects::Transition::Cut, 30);
    effects::update();
    CHECK(effects::transitionDone());
    effects::reset();
}

namespace {
AGB_EWRAM_BSS u32 g_lines[161];
}

AGB_TEST(scanline_dma_writes_each_line) {
    // BLDCNT/BLDALPHA is readable, so per-line values can be checked by reading it at chosen lines.
    for (int y = 0; y < 161; ++y) g_lines[y] = static_cast<u32>((y & 15) | ((15 - (y & 15)) << 8)) << 16;
    effects::startScanline(g_lines, 0x50, 1);
    system::waitVBlank();
    system::waitVBlank();
    int checked = 0, wrong = 0;
    const int lines[4] = {20, 57, 100, 140};
    for (int line : lines) {
        while (REG_VCOUNT != line) {}
        // Value for this line was written during the previous HBlank.
        const u16 got = REG_BLDALPHA;
        const u16 want = static_cast<u16>((line & 15) | ((15 - (line & 15)) << 8));
        ++checked;
        if (got != want) {
            ++wrong;
            debug::logf("  line %d: %x vs %x", line, got, want);
        }
    }
    effects::stopScanline();
    CHECK_EQ(checked, 4);
    CHECK_EQ(wrong, 0);
    CHECK(!effects::scanlineActive());
}

AGB_TEST(iwram_code_runs_from_iwram) {
    const u32 arm = reinterpret_cast<u32>(&hotArmAdd), thumb = reinterpret_cast<u32>(&hotThumbAdd);
    CHECK(arm >= 0x03000000 && arm < 0x03008000);
    CHECK((arm & 1) == 0);  // ARM state entry
    CHECK(thumb >= 0x03000000 && thumb < 0x03008000);
    CHECK_EQ(hotArmAdd(20, 22), 42);
    CHECK_EQ(hotThumbAdd(1, 2), 3);
}

AGB_IWRAM_CODE int hotThumbAdd(int a, int b) { return a + b; }

int main() {
    system::init();
    test::runAll();
}
