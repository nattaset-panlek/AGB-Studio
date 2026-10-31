// 24.8 fixed-point number: the `fixed` type of the C# scripting API (no FPU on the GBA).
#pragma once
#include <compare>
#include <type_traits>

#include "agb/bios.hpp"
#include "agb/types.hpp"

namespace agb {

struct fixed {
    static constexpr int kShift = 8;
    static constexpr s32 kOne = 1 << kShift;

    s32 raw = 0;

    constexpr fixed() = default;
    constexpr fixed(int v) : raw(v * kOne) {}
    static constexpr fixed fromRaw(s32 r) {
        fixed f;
        f.raw = r;
        return f;
    }
    // Compile-time conversion from a decimal literal (used by generated code and tests only).
    static consteval fixed lit(long double v) { return fromRaw(static_cast<s32>(v * kOne + (v >= 0 ? 0.5L : -0.5L))); }

    // Rounds toward negative infinity, like a right shift.
    constexpr int floor() const { return raw >> kShift; }
    constexpr int round() const { return (raw + kOne / 2) >> kShift; }
    constexpr int toInt() const { return raw / kOne; }  // truncates toward zero, like C# (int) casts

    constexpr fixed operator-() const { return fromRaw(-raw); }
    constexpr fixed operator+(fixed o) const { return fromRaw(raw + o.raw); }
    constexpr fixed operator-(fixed o) const { return fromRaw(raw - o.raw); }
    constexpr fixed operator*(fixed o) const { return fromRaw(static_cast<s32>((static_cast<s64>(raw) * o.raw) >> kShift)); }
    constexpr fixed operator/(fixed o) const {
        if (o.raw == 0) return fromRaw(raw >= 0 ? 0x7FFFFFFF : -0x7FFFFFFF);
        // |value| < 32768: the shifted dividend fits 32 bits, so the BIOS divide (same truncation toward zero) does it in
        // about a seventh of the time of the 64-bit library divide (core_basics fixed_point_cycle_budget).
        if (raw > -0x800000 && raw < 0x800000) {
            if (std::is_constant_evaluated()) return fromRaw((raw * kOne) / o.raw);
            return fromRaw(bios::div(raw * kOne, o.raw));
        }
        return fromRaw(static_cast<s32>((static_cast<s64>(raw) << kShift) / o.raw));
    }
    constexpr fixed operator*(int i) const { return fromRaw(raw * i); }
    constexpr fixed operator/(int i) const { return i == 0 ? fixed() : fromRaw(raw / i); }
    constexpr fixed& operator+=(fixed o) { raw += o.raw; return *this; }
    constexpr fixed& operator-=(fixed o) { raw -= o.raw; return *this; }
    constexpr fixed& operator*=(fixed o) { return *this = *this * o; }
    constexpr fixed& operator/=(fixed o) { return *this = *this / o; }
    constexpr bool operator==(const fixed&) const = default;
    constexpr auto operator<=>(const fixed&) const = default;
};

constexpr fixed abs(fixed f) { return f.raw < 0 ? -f : f; }
constexpr fixed min(fixed a, fixed b) { return a < b ? a : b; }
constexpr fixed max(fixed a, fixed b) { return a > b ? a : b; }
constexpr fixed clamp(fixed v, fixed lo, fixed hi) { return v < lo ? lo : v > hi ? hi : v; }

struct fixed2 {
    fixed x, y;
    constexpr fixed2 operator+(fixed2 o) const { return {x + o.x, y + o.y}; }
    constexpr fixed2 operator-(fixed2 o) const { return {x - o.x, y - o.y}; }
    constexpr fixed2 operator*(fixed s) const { return {x * s, y * s}; }
    constexpr fixed2& operator+=(fixed2 o) { x += o.x; y += o.y; return *this; }
    constexpr bool operator==(const fixed2&) const = default;
};

namespace math {
// sin / cos over a 256-step circle (0..255), result in fixed. Table in ROM.
fixed sin(u8 angle);
fixed cos(u8 angle);
// Integer square root of a fixed value.
fixed sqrt(fixed v);
}  // namespace math

}  // namespace agb
