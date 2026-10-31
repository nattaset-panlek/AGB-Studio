#include "agb/system.hpp"

#include <tonc.h>

#include "agb/commit.hpp"
#include "agb/debug.hpp"

namespace agb::profile {
namespace {
AGB_EWRAM_BSS_NAMED(agb_profiler) Stats g_stats;
u32 g_acc[static_cast<int>(Category::Count)];
Category g_current = Category::Logic;
u32 g_mark = 0;
u32 g_vblankFreeMin = 0xFFFFFFFFu;
}  // namespace

u32 vblankFreeMin() { return g_vblankFreeMin; }

// Timers 2 and 3 cascade into a free-running 32-bit cycle counter.
u32 cyclesNow() {
    const u16 lo = REG_TM2CNT_L;
    const u16 hi = REG_TM3CNT_L;
    const u16 lo2 = REG_TM2CNT_L;
    return lo2 < lo ? (static_cast<u32>(REG_TM3CNT_L) << 16) | lo2 : (static_cast<u32>(hi) << 16) | lo;
}

void begin(Category c) {
    const u32 now = cyclesNow();
    g_acc[static_cast<int>(g_current)] += now - g_mark;
    g_mark = now;
    g_current = c;
}

const Stats& stats() { return g_stats; }

void startCounter() {
    REG_TM2CNT_H = 0;
    REG_TM3CNT_H = 0;
    REG_TM2CNT_L = 0;
    REG_TM3CNT_L = 0;
    REG_TM3CNT_H = TM_ENABLE | TM_CASCADE;
    REG_TM2CNT_H = TM_ENABLE;
    g_mark = cyclesNow();
    debug::channel().profiler = &g_stats;
}

void endFrame(bool overrun) {
    begin(g_current);
    u32 busy = 0;
    for (int i = 0; i < static_cast<int>(Category::Count); ++i) {
        g_stats.thisFrame[i] = g_acc[i];
        if (i != static_cast<int>(Category::Idle)) busy += g_acc[i];
        g_acc[i] = 0;
    }
    g_stats.history[g_stats.historyHead % kHistory] = busy;
    ++g_stats.historyHead;
    if (overrun) ++g_stats.overruns;
}
}  // namespace agb::profile

namespace agb::system {

namespace {
volatile u32 g_vblankCount = 0;
u32 g_frame = 0;
u32 g_lastVBlank = 0;
void (*g_vblankHooks[4])() = {};
void (*g_frameHook)() = nullptr;

void onVBlank() {
    ++g_vblankCount;
    for (auto hook : g_vblankHooks)
        if (hook) hook();
}
}  // namespace

void init() {
    // ROM wait states 3/1 with the prefetch buffer: the standard cartridge setting, about twice as fast
    // as the boot default for code and data in ROM.
    REG_WAITCNT = WS_STANDARD;
    debug::init();
    irq_init(nullptr);
    irq_add(II_VBLANK, onVBlank);
    profile::startCounter();
    commit::init();
}

void addVBlankHook(void (*hook)()) {
    for (auto& h : g_vblankHooks)
        if (h == hook) return;
    for (auto& h : g_vblankHooks)
        if (!h) {
            h = hook;
            return;
        }
    debug::log("warning: no free VBlank hook slot");
}

void removeVBlankHook(void (*hook)()) {
    for (auto& h : g_vblankHooks)
        if (h == hook) h = nullptr;
}
void setFrameHook(void (*hook)()) { g_frameHook = hook; }

void waitVBlank() {
    // A frame overruns when a VBlank already passed while the game was still working. It still waits
    // for the next VBlank, so a slow frame costs one frame of time but never tears.
    const bool overrun = g_vblankCount != g_lastVBlank;
    profile::begin(profile::Category::Idle);
    VBlankIntrWait();
    g_lastVBlank = g_vblankCount;
    profile::begin(profile::Category::Render);
    debug::tracePhase(debug::Phase::VBlankCommit);
    commit::flush();
    // VBlank lasts 68 lines of 1232 cycles from line 160; report how much of it is left. A commit that ends in the next
    // frame's drawing lines may tear the picture (G-HW.07).
    const u32 line = REG_VCOUNT;
    const u32 vblankFree = line >= 160 && line < 228 ? (228 - line) * 1232 : 0;
    const_cast<profile::Stats&>(profile::stats()).vblankFreeCycles = vblankFree;
    if (vblankFree < profile::g_vblankFreeMin) profile::g_vblankFreeMin = vblankFree;
    if (!vblankFree) debug::warnOnce(3, "the VBlank commit did not finish inside VBlank; queue fewer VRAM copies per frame");
    profile::begin(profile::Category::Audio);
    if (g_frameHook) g_frameHook();
    profile::endFrame(overrun);
    profile::begin(profile::Category::Logic);
    ++g_frame;
    debug::setFrame(g_frame);
}

u32 frame() { return g_frame; }

}  // namespace agb::system
