// Random numbers: xoshiro128** (32-bit operations only), one stream per purpose so a replay of the
// same inputs gives the same gameplay even when effects use more or fewer numbers.
#pragma once
#include "agb/types.hpp"

namespace agb::rng {

enum class Stream : u8 { Gameplay, AI, Effect, Count };

struct State {
    u32 s[4];
};

void seed(u32 seed);                 // seeds every stream from one value
void seedStream(Stream s, u32 seed);
u32 next(Stream s = Stream::Gameplay);
// Uniform integer in [0, bound) without modulo bias.
u32 below(u32 bound, Stream s = Stream::Gameplay);
int range(int lo, int hi, Stream s = Stream::Gameplay);  // inclusive
State& state(Stream s);

}  // namespace agb::rng
