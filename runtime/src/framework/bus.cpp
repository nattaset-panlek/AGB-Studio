#include "agb/bus.hpp"

#include "agb/debug.hpp"

namespace agb::bus {

namespace {
Command g_queue[kQueueSize];
int g_head = 0, g_count = 0;
Source g_source[kSides];
AiChooser g_ai[kSides];
s8 g_pendingChoice[kSides];
constexpr int kRecordSize = 256;
u8 g_record[kRecordSize];
int g_recordLen = 0;
const u8* g_replay = nullptr;
int g_replayLen = 0, g_replayPos = 0;

int resolved(int choice) {
    if (g_recordLen < kRecordSize) g_record[g_recordLen++] = static_cast<u8>(choice);
    return choice;
}
}  // namespace

void reset() {
    g_head = g_count = 0;
    for (int i = 0; i < kSides; ++i) {
        g_source[i] = Source::Player;
        g_ai[i] = nullptr;
        g_pendingChoice[i] = -1;
    }
    g_recordLen = 0;
    g_replay = nullptr;
    g_replayLen = g_replayPos = 0;
}

bool post(const Command& cmd) {
    if (g_count >= kQueueSize) {
        debug::warnOnce(40, "command bus queue is full");
        return false;
    }
    g_queue[(g_head + g_count) % kQueueSize] = cmd;
    ++g_count;
    return true;
}

bool idle() { return g_count == 0; }
int pendingCount() { return g_count; }
const Command* front() { return g_count ? &g_queue[g_head] : nullptr; }

void complete() {
    if (!g_count) return;
    g_head = (g_head + 1) % kQueueSize;
    --g_count;
}

void completeAll() { g_head = g_count = 0; }

void setSource(int side, Source s, AiChooser ai) {
    if (side < 0 || side >= kSides) return;
    g_source[side] = s;
    g_ai[side] = ai;
}

Source source(int side) { return side >= 0 && side < kSides ? g_source[side] : Source::Player; }

int choose(int side, const s16* options, int count) {
    if (side < 0 || side >= kSides || count <= 0) return -1;
    switch (g_source[side]) {
        case Source::AI: {
            const int c = g_ai[side] ? g_ai[side](side, options, count) : 0;
            return resolved(c < 0 ? 0 : c >= count ? count - 1 : c);
        }
        case Source::Replay:
            if (g_replay && g_replayPos < g_replayLen) return resolved(g_replay[g_replayPos++] % count);
            return resolved(0);
        default:
            if (g_pendingChoice[side] < 0) return -1;
            const int c = g_pendingChoice[side] >= count ? count - 1 : g_pendingChoice[side];
            g_pendingChoice[side] = -1;
            return resolved(c);
    }
}

void submitChoice(int side, int optionIndex) {
    if (side >= 0 && side < kSides) g_pendingChoice[side] = static_cast<s8>(optionIndex);
}

const u8* record(int* length) {
    if (length) *length = g_recordLen;
    return g_record;
}

void setReplay(const u8* data, int length) {
    g_replay = data;
    g_replayLen = length;
    g_replayPos = 0;
}

}  // namespace agb::bus
