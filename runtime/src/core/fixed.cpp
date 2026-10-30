#include "agb/fixed.hpp"

namespace agb::math {

namespace {
// sin(i * 2pi / 256) * 256 for i in 0..64 (quarter wave), built at compile time.
constexpr s16 kQuarter[65] = {
    0,   6,   13,  19,  25,  31,  38,  44,  50,  56,  62,  68,  74,  80,  86,  92,  98,
    104, 109, 115, 121, 126, 132, 137, 142, 147, 152, 157, 162, 167, 172, 177, 181, 185,
    190, 194, 198, 202, 206, 209, 213, 216, 220, 223, 226, 229, 231, 234, 237, 239, 241,
    243, 245, 247, 248, 250, 251, 252, 253, 254, 255, 255, 256, 256, 256};
}  // namespace

fixed sin(u8 a) {
    const int q = a >> 6, i = a & 63;
    switch (q) {
        case 0: return fixed::fromRaw(kQuarter[i]);
        case 1: return fixed::fromRaw(kQuarter[64 - i]);
        case 2: return fixed::fromRaw(-kQuarter[i]);
        default: return fixed::fromRaw(-kQuarter[64 - i]);
    }
}

fixed cos(u8 a) { return sin(static_cast<u8>(a + 64)); }

fixed sqrt(fixed v) {
    if (v.raw <= 0) return fixed();
    // sqrt(raw / 256) * 256 = sqrt(raw * 256)
    u32 n = static_cast<u32>(v.raw) << 8, res = 0, bit = 1u << 30;
    while (bit > n) bit >>= 2;
    while (bit) {
        if (n >= res + bit) {
            n -= res + bit;
            res = (res >> 1) + bit;
        } else {
            res >>= 1;
        }
        bit >>= 2;
    }
    return fixed::fromRaw(static_cast<s32>(res));
}

}  // namespace agb::math
