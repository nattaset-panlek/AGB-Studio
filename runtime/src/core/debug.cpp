#include "agb/debug.hpp"

#include <cstdarg>

#include "agb/format.hpp"

namespace agb::debug {

namespace {
AGB_EWRAM_BSS Channel g_channel;
u32 g_warned[4];  // bit set of warnOnce ids already reported
}  // namespace

Channel& channel() { return g_channel; }

void init() {
    const char magic[8] = {'A', 'G', 'B', 'D', 'B', 'G', '0', '1'};
    for (int i = 0; i < 8; ++i) g_channel.magic[i] = magic[i];
    g_channel.version = kChannelVersion;
    g_channel.testStatus = static_cast<u32>(TestStatus::Running);
    g_channel.frame = 0;
    g_channel.logHead = 0;
    g_channel.logSize = kLogSize;
    g_channel.hostCommand = 0;
    g_channel.hostArg = 0;
    for (auto& w : g_warned) w = 0;
}

void log(const char* text) {
#if defined(AGB_RELEASE) && !defined(AGB_KEEP_LOGS)
    AGB_UNUSED(text);
#else
    u32 head = g_channel.logHead;
    for (const char* p = text; *p; ++p) {
        g_channel.log[head % kLogSize] = *p;
        ++head;
    }
    g_channel.log[head % kLogSize] = '\n';
    ++head;
    g_channel.logHead = head;
#endif
}

void logf(const char* fmt, ...) {
#if defined(AGB_RELEASE) && !defined(AGB_KEEP_LOGS)
    AGB_UNUSED(fmt);
#else
    char buf[160];
    va_list args;
    va_start(args, fmt);
    vformat(buf, sizeof buf, fmt, args);
    va_end(args);
    log(buf);
#endif
}

void warnOnce(u32 id, const char* text) {
    const u32 word = (id / 32) % 4, bit = 1u << (id % 32);
    if (g_warned[word] & bit) return;
    g_warned[word] |= bit;
    logf("warning: %s", text);
}

void setFrame(u32 frame) { g_channel.frame = frame; }

void testPass() {
    log("TEST PASS");
    g_channel.testStatus = static_cast<u32>(TestStatus::Pass);
}

void testFail(const char* reason) {
    logf("TEST FAIL: %s", reason);
    g_channel.testStatus = static_cast<u32>(TestStatus::Fail);
}

}  // namespace agb::debug
