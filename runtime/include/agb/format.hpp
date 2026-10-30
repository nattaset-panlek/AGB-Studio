// Small printf-style formatter (no newlib printf, which costs ~30 KB of ROM).
// Supports %d %i %u %x %X %s %c %% with optional '-', '0', width and the 'l' length (long is 32-bit).
#pragma once
#include <cstdarg>

#include "agb/types.hpp"

namespace agb {

int vformat(char* out, u32 size, const char* fmt, va_list args);
int format(char* out, u32 size, const char* fmt, ...) __attribute__((format(printf, 3, 4)));

}  // namespace agb
