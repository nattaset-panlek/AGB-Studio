#include "agb/memory.hpp"

namespace agb {

namespace {
AGB_EWRAM_BSS u8 g_screenArenaBuffer[112 * 1024] AGB_ALIGN4;
}

Arena& screenArena() {
    static Arena arena(g_screenArenaBuffer, sizeof g_screenArenaBuffer, "screen");
    return arena;
}

}  // namespace agb

// The runtime never uses the heap. These keep virtual destructors and placement new from pulling in
// malloc, and stop the game with a clear message on any accidental heap use.
void* operator new(std::size_t) { agb::debug::assertFailed(__FILE__, __LINE__, "new", "heap allocation is not supported on the GBA runtime"); }
void* operator new[](std::size_t) { agb::debug::assertFailed(__FILE__, __LINE__, "new[]", "heap allocation is not supported on the GBA runtime"); }
void operator delete(void*) noexcept {}
void operator delete(void*, std::size_t) noexcept {}
void operator delete[](void*) noexcept {}
void operator delete[](void*, std::size_t) noexcept {}

extern "C" void __cxa_pure_virtual() { agb::debug::assertFailed(__FILE__, __LINE__, "pure virtual", "pure virtual call"); }
