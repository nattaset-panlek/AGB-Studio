// Affine map build in ARM code from IWRAM, for maps that are not stored at the hardware size (a 128 x 128 map is
// 16384 cells, too slow for one load step in Thumb).
#include "agb/types.hpp"

namespace agb::render::affine {

// The layer's tile bytes (+ `add`, the first tile of the layer's tiles) into the square map, padded with tile 0.
// Written as halfwords: VRAM ignores byte writes.
void buildMap(u16* vram, int mapSide, const u8* cells, int width, int height, int add) {
    for (int y = 0; y < mapSide; ++y) {
        u16* row = vram + y * (mapSide >> 1);
        const u8* src = y < height ? cells + y * width : nullptr;
        for (int x = 0; x < mapSide; x += 2) {
            const u32 lo = src && x < width ? static_cast<u8>(src[x] + add) : 0;
            const u32 hi = src && x + 1 < width ? static_cast<u8>(src[x + 1] + add) : 0;
            row[x >> 1] = static_cast<u16>(lo | (hi << 8));
        }
    }
}

}  // namespace agb::render::affine
