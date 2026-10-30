// BIOS calls used by the runtime (decompression, division, halt).
#pragma once
#include "agb/types.hpp"

namespace agb::bios {

// BIOS LZ77 (type 0x10) decompression; Vram variant writes 16 bits at a time.
void lz77ToVram(const void* src, void* dst);
void lz77ToWram(const void* src, void* dst);
// Size in bytes of the decompressed data, from the LZ77 header.
u32 lz77Size(const void* src);
s32 div(s32 num, s32 den);
s32 mod(s32 num, s32 den);
void halt();
void fastCopy(void* dst, const void* src, u32 bytes);  // CpuFastSet, bytes multiple of 32

}  // namespace agb::bios
