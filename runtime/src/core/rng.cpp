#include "agb/rng.hpp"

namespace agb::rng {

namespace {
State g_streams[static_cast<int>(Stream::Count)];

constexpr u32 rotl(u32 x, int k) { return (x << k) | (x >> (32 - k)); }

u32 splitmix(u32& x) {
    u32 z = (x += 0x9E3779B9u);
    z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
    z = (z ^ (z >> 13)) * 0xC2B2AE35u;
    return z ^ (z >> 16);
}
}  // namespace

void seedStream(Stream s, u32 seed) {
    State& st = g_streams[static_cast<int>(s)];
    for (auto& w : st.s) w = splitmix(seed);
    if ((st.s[0] | st.s[1] | st.s[2] | st.s[3]) == 0) st.s[0] = 1;
}

void seed(u32 seed) {
    for (int i = 0; i < static_cast<int>(Stream::Count); ++i) seedStream(static_cast<Stream>(i), seed + static_cast<u32>(i) * 0x51ED270Bu);
}

u32 next(Stream s) {
    u32* x = g_streams[static_cast<int>(s)].s;
    const u32 result = rotl(x[1] * 5, 7) * 9;
    const u32 t = x[1] << 9;
    x[2] ^= x[0];
    x[3] ^= x[1];
    x[1] ^= x[2];
    x[0] ^= x[3];
    x[2] ^= t;
    x[3] = rotl(x[3], 11);
    return result;
}

u32 below(u32 bound, Stream s) {
    if (bound <= 1) return 0;
    const u32 limit = 0xFFFFFFFFu - (0xFFFFFFFFu % bound);
    u32 v;
    do v = next(s);
    while (v >= limit);
    return v % bound;
}

int range(int lo, int hi, Stream s) {
    if (hi <= lo) return lo;
    return lo + static_cast<int>(below(static_cast<u32>(hi - lo + 1), s));
}

State& state(Stream s) { return g_streams[static_cast<int>(s)]; }

}  // namespace agb::rng
