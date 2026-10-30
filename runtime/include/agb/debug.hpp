// Debug channel shared with the editor and the headless test runner.
// The host finds it by scanning EWRAM for the magic "AGBDBG01" and reads the log ring,
// test status and frame counter through emulator memory access. Field offsets are mirrored
// in tools/AgbStudio.Emu/DebugChannel.cs; change both together.
#pragma once
#include "agb/types.hpp"

namespace agb::debug {

enum class TestStatus : u32 { Running = 0, Pass = 1, Fail = 2 };

constexpr u32 kLogSize = 4096;
constexpr u32 kChannelVersion = 2;

struct Channel {
    char magic[8];             // 0  "AGBDBG01"
    u32 version;               // 8
    volatile u32 testStatus;   // 12
    volatile u32 frame;        // 16
    volatile u32 logHead;      // 20 total bytes ever written; ring index = logHead % kLogSize
    u32 logSize;               // 24
    volatile u32 hostCommand;  // 28 written by the host (editor), cleared by the game
    volatile u32 hostArg;      // 32
    const void* actorTable;    // 36 live actor table (framework)
    volatile u32 actorCount;   // 40
    u32 actorStride;           // 44
    const void* profiler;      // 48 agb::profile::Stats
    void* debugger;            // 52 agb::dbg::State (breakpoints, pause)
    const void* symbols;       // 56 generated watch symbols
    u32 reserved;              // 60
    char log[kLogSize];        // 64
};
static_assert(__builtin_offsetof(Channel, log) == 64, "debug channel layout is shared with the host");

Channel& channel();

void init();
#if defined(AGB_RELEASE) && !defined(AGB_KEEP_LOGS)
// Release builds ("Strip Debug.Log calls"): inline no-ops, so the text never reaches the ROM.
inline void log(const char*) {}
inline void logf(const char*, ...) {}
inline void warnOnce(u32, const char*) {}
#else
void log(const char* text);
void logf(const char* fmt, ...) __attribute__((format(printf, 1, 2)));
void warnOnce(u32 id, const char* text);
#endif
void setFrame(u32 frame);
void testPass();
void testFail(const char* reason);

}  // namespace agb::debug
