#include "agb/assert.hpp"

#include <tonc.h>

#include "agb/debug.hpp"

namespace agb::debug {

void assertFailed(const char* file, int line, const char* expr, const char* message) {
    const char* name = file;
    for (const char* p = file; *p; ++p)
        if (*p == '/' || *p == '\\') name = p + 1;
    logf("assert failed: %s(%d): %s. The game stopped here. (%s)", name, line, message, expr);
    testFail("assert");
    REG_IME = 0;
    for (;;) Halt();
}

}  // namespace agb::debug
