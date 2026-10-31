#include "agb/probe.hpp"

#include <tonc.h>

#include "agb/debug.hpp"
#include "agb/system.hpp"

namespace agb::dbg {

namespace detail {
AGB_EWRAM_BSS_NAMED(agb_symbols) State g_state;
bool g_stepping = false;
}  // namespace detail

using detail::g_state;
using detail::g_stepping;

namespace {
bool g_published = false;

void publish() {
    if (g_published) return;
    g_published = true;
    debug::channel().symbols = &g_state;
}
}  // namespace

State& state() {
    publish();
    return g_state;
}

void setNames(const char* const* files, const char* const* names) {
    publish();
    g_state.files = files;
    g_state.names = names;
}

void enter(u16 file, u16 name) {
    publish();
    if (g_state.depth < kStackDepth) g_state.stack[g_state.depth] = {file, name, 0, 0};
    ++g_state.depth;
}

void leave() {
    if (g_state.depth) --g_state.depth;
}

void detail::probeSlow(u16 file, u16 line) {
    publish();
    const u32 at = (static_cast<u32>(file) << 16) | line;
    bool hit = g_stepping;
    for (int i = 0; i < g_state.breakpointCount && !hit; ++i) hit = g_state.breakpoints[i] == at;
    if (!hit) return;
    g_stepping = false;
    g_state.pausedAt = at;
    g_state.pausedFrame = system::frame();
    g_state.resume = 0;
    g_state.paused = 1;
    debug::logf("breakpoint: %s:%u", g_state.files ? g_state.files[file] : "?", line);
    // Stopped inside the statement: wait for the editor. VBlank keeps interrupts (audio) alive.
    while (!g_state.resume) VBlankIntrWait();
    g_stepping = g_state.resume == 2;
    g_state.paused = 0;
}

}  // namespace agb::dbg
