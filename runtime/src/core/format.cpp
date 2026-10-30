#include "agb/format.hpp"

namespace agb {

namespace {
struct Out {
    char* buf;
    u32 size, len = 0;
    void put(char c) {
        if (len + 1 < size) buf[len] = c;
        ++len;
    }
};

void putNumber(Out& o, u32 value, bool negative, u32 base, bool upper, int width, bool zero, bool left) {
    char digits[12];
    int n = 0;
    do {
        const u32 d = value % base;
        digits[n++] = static_cast<char>(d < 10 ? '0' + d : (upper ? 'A' : 'a') + d - 10);
        value /= base;
    } while (value && n < 12);
    const int len = n + (negative ? 1 : 0);
    if (!left && !zero)
        for (int i = len; i < width; ++i) o.put(' ');
    if (negative) o.put('-');
    if (!left && zero)
        for (int i = len; i < width; ++i) o.put('0');
    while (n) o.put(digits[--n]);
    if (left)
        for (int i = len; i < width; ++i) o.put(' ');
}
}  // namespace

int vformat(char* out, u32 size, const char* fmt, va_list args) {
    Out o{out, size};
    for (const char* p = fmt; *p; ++p) {
        if (*p != '%') {
            o.put(*p);
            continue;
        }
        ++p;
        bool left = false, zero = false;
        while (*p == '-' || *p == '0') {
            if (*p == '-') left = true;
            else zero = true;
            ++p;
        }
        int width = 0;
        while (*p >= '0' && *p <= '9') width = width * 10 + (*p++ - '0');
        const bool isLong = *p == 'l';
        if (isLong) ++p;
        switch (*p) {
            case 'd':
            case 'i': {
                const long v = isLong ? va_arg(args, long) : va_arg(args, int);
                putNumber(o, v < 0 ? static_cast<u32>(-(v + 1)) + 1 : static_cast<u32>(v), v < 0, 10, false, width, zero, left);
                break;
            }
            case 'u': putNumber(o, isLong ? va_arg(args, unsigned long) : va_arg(args, unsigned), false, 10, false, width, zero, left); break;
            case 'x': putNumber(o, isLong ? va_arg(args, unsigned long) : va_arg(args, unsigned), false, 16, false, width, zero, left); break;
            case 'X': putNumber(o, isLong ? va_arg(args, unsigned long) : va_arg(args, unsigned), false, 16, true, width, zero, left); break;
            case 'c': o.put(static_cast<char>(va_arg(args, int))); break;
            case 's': {
                const char* s = va_arg(args, const char*);
                if (!s) s = "(null)";
                int len = 0;
                while (s[len]) ++len;
                if (!left)
                    for (int i = len; i < width; ++i) o.put(' ');
                for (int i = 0; i < len; ++i) o.put(s[i]);
                if (left)
                    for (int i = len; i < width; ++i) o.put(' ');
                break;
            }
            case '%': o.put('%'); break;
            case '\0': --p; break;
            default:
                o.put('%');
                o.put(*p);
        }
    }
    if (size) out[o.len < size ? o.len : size - 1] = '\0';
    return static_cast<int>(o.len);
}

int format(char* out, u32 size, const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    const int n = vformat(out, size, fmt, args);
    va_end(args);
    return n;
}

}  // namespace agb
