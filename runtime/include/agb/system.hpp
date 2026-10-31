// Frame loop, interrupts and the per-frame profiler.
// Frame order (blueprint 04): waitVBlank() commits the previous frame's queued hardware writes at the
// start of VBlank, then the game reads input and builds the next frame during VDraw.
#pragma once
#include "agb/types.hpp"

namespace agb::system {

void init();
// Sleeps in the BIOS until VBlank (no busy wait), then flushes the commit queues.
void waitVBlank();
u32 frame();
// Functions called from the VBlank interrupt (audio mixer, scanline DMA restart). Up to 4.
void addVBlankHook(void (*hook)());
void removeVBlankHook(void (*hook)());
// Called once per frame from waitVBlank() after the commit (audio frame work).
void setFrameHook(void (*hook)());

}  // namespace agb::system

namespace agb::profile {

enum class Category : u8 { Logic, Render, Audio, Idle, Count };

constexpr int kHistory = 120;
constexpr u32 kCyclesPerFrame = 280896;

// Layout read by the editor Debugger (Profiler view) through debug::Channel::profiler.
struct Stats {
    u32 thisFrame[static_cast<int>(Category::Count)];  // cycles per category in the last finished frame
    u32 history[kHistory];                            // busy cycles (everything but Idle) per frame
    u32 historyHead;
    u32 vblankFreeCycles;   // cycles left in VBlank after the commit
    u32 overruns;           // frames that missed their VBlank
};

void begin(Category c);  // switches the running category; cycles accumulate into it
const Stats& stats();
u32 cyclesNow();
// Fewest VBlank cycles left after a commit since boot (0 = a commit ran past VBlank, which also logs a warning).
u32 vblankFreeMin();

}  // namespace agb::profile
