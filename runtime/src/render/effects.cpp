#include "agb/render/effects.hpp"

#include <tonc.h>

#include "agb/commit.hpp"
#include "agb/system.hpp"

namespace agb::render::effects {

namespace {
const u32* g_table = nullptr;
u16 g_ioOffset = 0;
u8 g_words = 0;

Transition g_kind = Transition::Cut;
bool g_out = false;
u16 g_frames = 0, g_elapsed = 0;
int g_level = 0;

// Restarts the HBlank DMA at the start of each frame (line 0 is written by hand in VBlank).
void vblankRestart() {
    if (!g_table) return;
    REG_DMA0CNT = 0;
    volatile u32* dst = reinterpret_cast<volatile u32*>(REG_BASE + g_ioOffset);
    for (int i = 0; i < g_words; ++i) dst[i] = g_table[i];
    REG_DMA0SAD = reinterpret_cast<u32>(g_table + g_words);
    REG_DMA0DAD = REG_BASE + g_ioOffset;
    REG_DMA0CNT = g_words | DMA_DST_RELOAD | DMA_REPEAT | DMA_32 | DMA_AT_HBLANK | DMA_ENABLE;
}

void applyTransition() {
    const int level = g_level;
    switch (g_kind) {
        case Transition::Cut:
        case Transition::Fade:
            if (level == 0) clearBlend();
            else setBrightness(kBg0 | kBg1 | kBg2 | kBg3 | kObj | kBackdrop, -level);
            break;
        case Transition::Slide: {
            // Window 0 covers the visible part; it shrinks from the right as level grows.
            const int right = 240 - level * 15;
            if (level == 0) disableWindows();
            else setWindow0(0, 0, right, 160, 0x3F, 0);
            break;
        }
    }
}
}  // namespace

void reset() {
    stopScanline();
    clearBlend();
    setMosaic(1, 1);
    disableWindows();
    g_level = 0;
    g_frames = g_elapsed = 0;
}

void setAlphaBlend(u8 a, u8 b, u8 eva, u8 evb) {
    commit::setReg(commit::BldCnt, static_cast<u16>((a & 0x3F) | ((b & 0x3F) << 8) | BLD_STD));
    commit::setReg(commit::BldAlpha, static_cast<u16>((eva > 16 ? 16 : eva) | ((evb > 16 ? 16 : evb) << 8)));
}

void setBrightness(u8 targets, int amount) {
    const u16 mode = amount >= 0 ? BLD_WHITE : BLD_BLACK;
    const int a = amount < 0 ? -amount : amount;
    commit::setReg(commit::BldCnt, static_cast<u16>((targets & 0x3F) | mode));
    commit::setReg(commit::BldY, static_cast<u16>(a > 16 ? 16 : a));
}

void clearBlend() {
    commit::setReg(commit::BldCnt, 0);
    commit::setReg(commit::BldY, 0);
}

void setMosaic(u8 bgSize, u8 objSize) {
    const int b = bgSize ? bgSize - 1 : 0, o = objSize ? objSize - 1 : 0;
    commit::setReg(commit::Mosaic, static_cast<u16>((b & 15) | ((b & 15) << 4) | ((o & 15) << 8) | ((o & 15) << 12)));
}

void setWindow0(int left, int top, int right, int bottom, u8 inside, u8 outside) {
    commit::setReg(commit::Win0H, static_cast<u16>(((left & 0xFF) << 8) | (right > 240 ? 240 : right)));
    commit::setReg(commit::Win0V, static_cast<u16>(((top & 0xFF) << 8) | (bottom > 160 ? 160 : bottom)));
    commit::setReg(commit::WinIn, static_cast<u16>(inside & 0x3F));
    commit::setReg(commit::WinOut, static_cast<u16>(outside & 0x3F));
    commit::setReg(commit::DispCnt, static_cast<u16>(commit::reg(commit::DispCnt) | DCNT_WIN0));
}

void disableWindows() {
    commit::setReg(commit::DispCnt, static_cast<u16>(commit::reg(commit::DispCnt) & ~(DCNT_WIN0 | DCNT_WIN1 | DCNT_WINOBJ)));
}

void startScanline(const u32* table, u16 ioOffset, u8 wordsPerLine) {
    g_table = table;
    g_ioOffset = ioOffset;
    g_words = wordsPerLine;
    system::addVBlankHook(vblankRestart);
}

void stopScanline() {
    REG_DMA0CNT = 0;
    g_table = nullptr;
    system::removeVBlankHook(vblankRestart);
}

bool scanlineActive() { return g_table != nullptr; }

void beginOut(Transition t, u16 frames) {
    g_kind = t;
    g_out = true;
    g_frames = t == Transition::Cut ? 1 : (frames ? frames : 1);
    g_elapsed = 0;
}

void beginIn(Transition t, u16 frames) {
    g_kind = t;
    g_out = false;
    g_frames = t == Transition::Cut ? 1 : (frames ? frames : 1);
    g_elapsed = 0;
}

void update() {
    if (g_elapsed >= g_frames) return;
    ++g_elapsed;
    const int p = g_elapsed * 16 / g_frames;
    g_level = g_out ? p : 16 - p;
    applyTransition();
}

bool transitionDone() { return g_elapsed >= g_frames; }
int transitionLevel() { return g_level; }

}  // namespace agb::render::effects
