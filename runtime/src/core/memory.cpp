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
