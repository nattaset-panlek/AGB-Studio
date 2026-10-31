// Tiny on-ROM test framework. Each test ROM defines tests with AGB_TEST and calls agb::test::runAll()
// from main(); results go to the debug channel, which `agb romtest` reads.
#pragma once
#include "agb/debug.hpp"
#include "agb/system.hpp"

namespace agb::test {

using TestFn = void (*)();

struct Registration {
    const char* name;
    TestFn fn;
    Registration* next;
    Registration(const char* n, TestFn f);
};

void fail(const char* file, int line, const char* expr);
int failures();
// True when `text` was written to the debug log since `since` (a debug::channel().logHead taken before the call under
// test), as the Console shows it.
bool loggedSince(u32 since, const char* text);
// After the run, fill the screen with green (all passed) or red (a test failed), so emulators without access to the
// debug channel (VBA-M) show the result.
void showResultOnScreen();
// Runs every registered test, reports pass / fail and then keeps waiting for VBlank forever.
[[noreturn]] void runAll();

}  // namespace agb::test

#define AGB_TEST(name)                                                            \
    static void agb_test_##name();                                                \
    static ::agb::test::Registration agb_test_reg_##name(#name, agb_test_##name); \
    static void agb_test_##name()

#define CHECK(cond)                                                   \
    do {                                                              \
        if (!(cond)) ::agb::test::fail(__FILE__, __LINE__, #cond);    \
    } while (0)

#define CHECK_EQ(a, b)                                                                   \
    do {                                                                                 \
        const auto agb_a = (a);                                                          \
        const auto agb_b = (b);                                                          \
        if (!(agb_a == agb_b)) {                                                         \
            ::agb::debug::logf("  values: %d vs %d", static_cast<int>(agb_a), static_cast<int>(agb_b)); \
            ::agb::test::fail(__FILE__, __LINE__, #a " == " #b);                         \
        }                                                                                \
    } while (0)
