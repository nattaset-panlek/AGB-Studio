// Basic types and section attributes shared by the whole runtime.
#pragma once
#include <cstddef>
#include <cstdint>

namespace agb {

using u8 = uint8_t;
using u16 = uint16_t;
using u32 = uint32_t;  // same type as libtonc u32 (unsigned long): format it with %lu
using u64 = uint64_t;
using s8 = int8_t;
using s16 = int16_t;
using s32 = int32_t;  // long: format with %ld
using s64 = int64_t;

}  // namespace agb

// Hot code compiled as ARM and copied to IWRAM by the boot code.
#define AGB_IWRAM_CODE __attribute__((section(".iwram"), long_call, target("arm"), noinline))
// Data placed in IWRAM / EWRAM instead of the default.
#define AGB_IWRAM_DATA __attribute__((section(".iwram")))
#define AGB_EWRAM_DATA __attribute__((section(".ewram")))
// Zero-initialised EWRAM (NOLOAD, cleared by the boot code): use for buffers so they take no ROM space.
#define AGB_EWRAM_BSS __attribute__((section(".sbss")))
#define AGB_ALIGN4 __attribute__((aligned(4)))
#define AGB_UNUSED(x) ((void)(x))
