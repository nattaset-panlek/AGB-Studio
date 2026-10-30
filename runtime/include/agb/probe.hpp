// C# source-level debugging (D-21), Debug builds only. Translated scripts call AGB_SCOPE at each method entry
// and AGB_PROBE before each statement. A probe that hits a breakpoint pauses the game inside that statement
// while the editor reads state through the emulator; the editor writes `resume` to continue or step.
#pragma once
#include "agb/types.hpp"

namespace agb::dbg {

constexpr int kMaxBreakpoints = 16;
constexpr int kStackDepth = 16;

struct Frame {
    u16 file;   // script file id (generated table)
    u16 name;   // method name id
    u16 line;   // last line reached in this method
    u16 pad;
};

// Layout read and written by the editor (tools/AgbStudio.Emu/Inspector.cs, CSharpDebugger).
struct State {
    u32 breakpoints[kMaxBreakpoints];  // (file << 16) | line
    u8 breakpointCount;
    volatile u8 paused;      // 1 while stopped at a breakpoint
    volatile u8 resume;      // host: 1 continue, 2 step to the next line
    u8 depth;                // call stack depth
    u32 pausedAt;            // (file << 16) | line
    u32 pausedFrame;
    Frame stack[kStackDepth];
    const char* const* files;  // generated names
    const char* const* names;
};

State& state();
void setNames(const char* const* files, const char* const* names);
void probe(u16 file, u16 line);
void enter(u16 file, u16 name);
void leave();

struct Scope {
    Scope(u16 file, u16 name) { enter(file, name); }
    ~Scope() { leave(); }
};

}  // namespace agb::dbg

#define AGB_CAT2(a, b) a##b
#define AGB_CAT(a, b) AGB_CAT2(a, b)
#if defined(AGB_DEBUG)
#define AGB_PROBE(file, line) ::agb::dbg::probe(file, line)
#define AGB_SCOPE(file, name) ::agb::dbg::Scope AGB_CAT(agb_scope_, __LINE__)(file, name)
#else
#define AGB_PROBE(file, line) ((void)0)
#define AGB_SCOPE(file, name) ((void)0)
#endif
