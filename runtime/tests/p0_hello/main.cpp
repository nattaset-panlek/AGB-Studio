// Phase 0 hello ROM: draws colour bars in mode 3, logs through the debug channel, passes after 30 frames.
#include <tonc.h>

#include "agb/debug.hpp"

int main() {
    agb::debug::init();
    irq_init(nullptr);
    irq_add(II_VBLANK, nullptr);

    REG_DISPCNT = DCNT_MODE3 | DCNT_BG2;
    const COLOR bars[4] = {RGB15(31, 0, 0), RGB15(0, 31, 0), RGB15(0, 0, 31), RGB15(31, 31, 31)};
    for (int y = 0; y < SCREEN_HEIGHT; ++y)
        for (int x = 0; x < SCREEN_WIDTH; ++x) m3_plot(x, y, bars[x / 60]);

    agb::debug::log("hello from AGB Studio runtime");
    for (uint32_t frame = 1;; ++frame) {
        VBlankIntrWait();
        agb::debug::setFrame(frame);
        if (frame == 30) agb::debug::testPass();
    }
}
