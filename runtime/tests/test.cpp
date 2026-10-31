#include "test.hpp"

#include "agb/commit.hpp"

namespace agb::test {

namespace {
Registration* g_head = nullptr;
Registration* g_tail = nullptr;
int g_failures = 0;
const char* g_current = "";
bool g_showResult = false;
}  // namespace

Registration::Registration(const char* n, TestFn f) : name(n), fn(f), next(nullptr) {
    if (g_tail) g_tail->next = this;
    else g_head = this;
    g_tail = this;
}

void fail(const char* file, int line, const char* expr) {
    const char* base = file;
    for (const char* p = file; *p; ++p)
        if (*p == '/' || *p == '\\') base = p + 1;
    debug::logf("  FAILED %s: %s(%d): %s", g_current, base, line, expr);
    ++g_failures;
}

int failures() { return g_failures; }

bool loggedSince(u32 since, const char* text) {
    const debug::Channel& ch = debug::channel();
    const u32 head = ch.logHead;
    if (head - since > debug::kLogSize) since = head - debug::kLogSize;
    for (u32 start = since; start < head; ++start) {
        u32 i = 0;
        while (text[i] && start + i < head && ch.log[(start + i) % debug::kLogSize] == text[i]) ++i;
        if (!text[i]) return true;
    }
    return false;
}

void showResultOnScreen() { g_showResult = true; }

void runAll() {
    int count = 0;
    for (Registration* r = g_head; r; r = r->next) {
        g_current = r->name;
        const int before = g_failures;
        r->fn();
        debug::logf("%s %s", g_failures == before ? "ok  " : "FAIL", r->name);
        ++count;
    }
    if (g_failures) {
        debug::logf("%d of %d tests failed", g_failures, count);
        debug::testFail("tests failed");
    } else {
        debug::logf("%d tests passed", count);
        debug::testPass();
    }
    if (g_showResult) {
        // Mode 0 with every layer and sprite off shows only the backdrop (palette entry 0).
        commit::setReg(commit::DispCnt, 0);
        commit::palette()[0] = g_failures ? 0x001F : 0x03E0;
        commit::markPaletteDirty();
    }
    for (;;) system::waitVBlank();
}

}  // namespace agb::test
