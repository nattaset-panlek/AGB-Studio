#include "test.hpp"

namespace agb::test {

namespace {
Registration* g_head = nullptr;
Registration* g_tail = nullptr;
int g_failures = 0;
const char* g_current = "";
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
    for (;;) system::waitVBlank();
}

}  // namespace agb::test
