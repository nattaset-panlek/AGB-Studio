// Asserts: in Debug builds (and Release with AGB_KEEP_ASSERTS) a failed assert logs file:line
// to the Console through the debug channel and stops the game. Release removes them.
#pragma once

namespace agb::debug {
[[noreturn]] void assertFailed(const char* file, int line, const char* expr, const char* message);
}

#if defined(AGB_DEBUG) || defined(AGB_KEEP_ASSERTS)
#define AGB_ASSERT(cond, message) \
    ((cond) ? (void)0 : ::agb::debug::assertFailed(__FILE__, __LINE__, #cond, message))
#else
#define AGB_ASSERT(cond, message) ((void)0)
#endif
