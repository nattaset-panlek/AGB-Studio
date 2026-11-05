#include "agb/input.hpp"

#include "agb/hw.hpp"

namespace agb::input {

namespace {
u16 g_cur = 0, g_prev = 0, g_repeat = 0;
u16 g_injected = 0;
bool g_hasInjected = false;
u8 g_delay = 20, g_interval = 4;
u8 g_holdFrames[10];
constexpr int kMaxActions = 16;
u16 g_actions[kMaxActions];
int g_actionCount = 0;
}  // namespace

void update() {
    g_prev = g_cur;
    if (g_hasInjected) {
        g_cur = g_injected & kAllKeys;
        g_hasInjected = false;
    } else {
        g_cur = static_cast<u16>(~REG_KEYINPUT) & kAllKeys;
    }
    g_repeat = 0;
    for (int i = 0; i < 10; ++i) {
        const u16 bit = static_cast<u16>(1 << i);
        if (!(g_cur & bit)) {
            g_holdFrames[i] = 0;
            continue;
        }
        if (g_holdFrames[i] < 255) ++g_holdFrames[i];
        const u8 h = g_holdFrames[i];
        if (h == 1 || (h > g_delay && (h - g_delay) % g_interval == 0)) g_repeat |= bit;
    }
}

void injectState(u16 keys) {
    g_injected = keys;
    g_hasInjected = true;
}

bool held(Key k) { return g_cur & static_cast<u16>(k); }
bool pressed(Key k) { return (g_cur & ~g_prev) & static_cast<u16>(k); }
bool released(Key k) { return (~g_cur & g_prev) & static_cast<u16>(k); }
bool repeated(Key k) { return g_repeat & static_cast<u16>(k); }
bool anyKey() { return (g_cur & ~g_prev) != 0; }
int axisX() { return (held(Key::Right) ? 1 : 0) - (held(Key::Left) ? 1 : 0); }
int axisY() { return (held(Key::Down) ? 1 : 0) - (held(Key::Up) ? 1 : 0); }
u16 heldMask() { return g_cur; }

void setRepeat(u8 delayFrames, u8 intervalFrames) {
    g_delay = delayFrames;
    g_interval = intervalFrames ? intervalFrames : 1;
}

void setActionMap(const u16* actionKeys, int count) {
    g_actionCount = count < kMaxActions ? count : kMaxActions;
    for (int i = 0; i < g_actionCount; ++i) g_actions[i] = actionKeys[i];
}

bool actionHeld(int a) { return a >= 0 && a < g_actionCount && (g_cur & g_actions[a]); }
bool actionPressed(int a) { return a >= 0 && a < g_actionCount && (g_cur & ~g_prev & g_actions[a]); }

u16 actionKeys(int a) { return a >= 0 && a < g_actionCount ? g_actions[a] : 0; }
int actionCount() { return g_actionCount; }

void remapAction(int a, u16 keys) {
    if (a >= 0 && a < g_actionCount) g_actions[a] = keys & kAllKeys;
}

}  // namespace agb::input
