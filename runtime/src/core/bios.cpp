#include "agb/bios.hpp"

#include "agb/hw.hpp"

namespace agb::bios {

void lz77ToVram(const void* src, void* dst) { LZ77UnCompVram(src, dst); }
void lz77ToWram(const void* src, void* dst) { LZ77UnCompWram(src, dst); }
u32 lz77Size(const void* src) { return *static_cast<const u32*>(src) >> 8; }
s32 div(s32 num, s32 den) { return Div(num, den); }
s32 mod(s32 num, s32 den) { return DivMod(num, den); }
void halt() { Halt(); }
void fastCopy(void* dst, const void* src, u32 bytes) { CpuFastSet(src, dst, bytes / 4); }

}  // namespace agb::bios
