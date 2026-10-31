// Phase 0 hello ROM: colour bars and a line of text in mode 3, a log line through the debug channel, passes after
// 30 frames. The text uses libtonc's text engine with its built-in Verdana 9 font (P0.05: "a coloured screen and text").
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

    // A dark strip across the bars with the text on it.
    for (int y = 68; y < 92; ++y)
        for (int x = 0; x < SCREEN_WIDTH; ++x) m3_plot(x, y, RGB15(4, 4, 8));
    tte_init_bmp_default(3);
    tte_set_ink(RGB15(31, 31, 31));
    tte_set_pos(64, 74);
    tte_write("Hello from AGB Studio");

    agb::debug::log("hello from AGB Studio runtime");
    for (uint32_t frame = 1;; ++frame) {
        VBlankIntrWait();
        agb::debug::setFrame(frame);
        if (frame == 30) agb::debug::testPass();
    }
}
