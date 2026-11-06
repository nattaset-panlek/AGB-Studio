// Phase 0 hello ROM: colour bars and a line of text in mode 3, a log line through the debug channel, passes after
// 30 frames (P0.05: "a coloured screen and text"). The text uses a 5 x 7 font drawn here, so this ROM needs nothing
// but the hardware header.
#include "agb/hw.hpp"

#include "agb/debug.hpp"

namespace {
// Rows of 5 bits, top to bottom, for the letters of the message.
struct Glyph {
    char c;
    u8 rows[7];
};
constexpr Glyph kFont[] = {
    {'H', {0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11}}, {'e', {0x00, 0x00, 0x0E, 0x11, 0x1F, 0x10, 0x0E}},
    {'l', {0x0C, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E}}, {'o', {0x00, 0x00, 0x0E, 0x11, 0x11, 0x11, 0x0E}},
    {'f', {0x06, 0x09, 0x08, 0x1C, 0x08, 0x08, 0x08}}, {'r', {0x00, 0x00, 0x16, 0x19, 0x10, 0x10, 0x10}},
    {'m', {0x00, 0x00, 0x1A, 0x15, 0x15, 0x15, 0x15}}, {'A', {0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11}},
    {'G', {0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F}}, {'B', {0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E}},
    {'S', {0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E}}, {'t', {0x08, 0x08, 0x1C, 0x08, 0x08, 0x09, 0x06}},
    {'u', {0x00, 0x00, 0x11, 0x11, 0x11, 0x13, 0x0D}}, {'d', {0x01, 0x01, 0x0D, 0x13, 0x11, 0x11, 0x0F}},
    {'i', {0x04, 0x00, 0x0C, 0x04, 0x04, 0x04, 0x0E}},
};

void plot(int x, int y, COLOR c) { vid_mem[y * SCREEN_WIDTH + x] = c; }

void text(int x, int y, const char* s, COLOR c) {
    for (; *s; ++s, x += 6)
        for (const Glyph& g : kFont)
            if (g.c == *s)
                for (int r = 0; r < 7; ++r)
                    for (int b = 0; b < 5; ++b)
                        if (g.rows[r] & (0x10 >> b)) plot(x + b, y + r, c);
}
}  // namespace

int main() {
    agb::debug::init();
    irq_init(nullptr);
    irq_add(II_VBLANK, nullptr);

    REG_DISPCNT = DCNT_MODE3 | DCNT_BG2;
    const COLOR bars[4] = {RGB15(31, 0, 0), RGB15(0, 31, 0), RGB15(0, 0, 31), RGB15(31, 31, 31)};
    for (int y = 0; y < SCREEN_HEIGHT; ++y)
        for (int x = 0; x < SCREEN_WIDTH; ++x) plot(x, y, bars[x / 60]);

    // A dark strip across the bars with the text on it.
    for (int y = 68; y < 92; ++y)
        for (int x = 0; x < SCREEN_WIDTH; ++x) plot(x, y, RGB15(4, 4, 8));
    text(57, 76, "Hello from AGB Studio", RGB15(31, 31, 31));

    agb::debug::log("hello from AGB Studio runtime");
    for (uint32_t frame = 1;; ++frame) {
        VBlankIntrWait();
        agb::debug::setFrame(frame);
        if (frame == 30) agb::debug::testPass();
    }
}
